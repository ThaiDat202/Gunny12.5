using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using Bussiness.Managers;
using Game.Base.Packets;
using Game.Server.GameObjects;
using Game.Server.GameUtils;
using SqlDataProvider.Data;

namespace Game.Server.Packets.Client;

/// <summary>
/// Ngọc Hồn Vũ Khí theo cơ chế VNG:
/// - 7 ô Ngọc Thuộc Tính + 1 ô Ngọc Kỹ Năng
/// - Mặc định mở 1 ô; mở thêm bằng Dùi Huyền Tinh
/// - Ngọc thuộc tính tối đa 3 viên cùng loại
/// - Nâng cấp: Mảnh Ngọc Lam (-4700) cho thuộc tính, Mảnh Ngọc Lục (-4800) cho kỹ năng
/// Packet 693: Req_Info/Hole_Info/Open_Hole/Hole_Equip/Hole_UnEquip/upStoneLevel
/// </summary>
[PacketHandler(693, "Ngọc Hồn Vũ Khí")]
public class GemstoneHandler : IPacketHandler
{
    private static readonly int[] HoleExps = { 0, 2000, 6000, 11000, 17000, 25000, 35000, 11000 };
    private static readonly int[] UpgradeNeed = { 0, 200, 500, 800, 1200, 1600, 2300, 3000, 4000, 5000, 0 };
    private const int HoleItem = 12262;          // Dùi Huyền Tinh
    private const int AttrMaterial = -4700;      // Mảnh Ngọc Lam
    private const int SkillMaterial = -4800;     // Mảnh Ngọc Lục
    private const int OpenHolesGold = 200;
    private const int SkillGemMin = 47013;
    private const int SkillGemMax = 47027;
    private const int MaxSameGem = 3;

    private sealed class HoleData
    {
        public int OpenHoleIndex = 2;
        public int CurHoleExp;
        public int SkillHoleExp;
        public List<int> Holes = new List<int> { 0, 0, -1, -1, -1, -1, -1, -1, -1 };
    }

    public int HandlePacket(GameClient client, GSPacketIn packet)
    {
        byte cmd = packet.ReadByte();
        GamePlayer player = client.Player;
        switch (cmd)
        {
            case 1: SendStoneInfo(player); break;
            case 2: OpenHole(player, packet.ReadInt(), packet.ReadInt()); break;
            case 3: InsertStone(player, packet.ReadInt(), packet.ReadInt()); break;
            case 4: UnEquipStone(player, packet.ReadInt()); break;
            case 5: SendHoleInfo(player); break;
            case 6: UpgradeStone(player, packet.ReadInt()); break;
        }
        return 0;
    }

    private static string HoleConnectionString
    {
        get
        {
            var cs = ConfigurationManager.ConnectionStrings["PlayerData"];
            if (cs != null && !string.IsNullOrWhiteSpace(cs.ConnectionString)) return cs.ConnectionString;
            return "Data Source=DESKTOP-SMLR4VU;Initial Catalog=Db_Tank;Persist Security Info=True;User ID=sa;Password=123456;";
        }
    }

    private static bool IsSkillGem(int templateId)
    {
        return templateId >= SkillGemMin && templateId <= SkillGemMax;
    }

    private static int GetGemLevel(UserGemStone stone)
    {
        try
        {
            string[] parts = (stone.FigSpiritIdValue ?? "0,0,0").Split('|');
            if (parts.Length > 0 && int.TryParse(parts[0].Split(',')[0], out int level)) return level;
        }
        catch { }
        return 0;
    }

    private static string SetGemLevel(string value, int level)
    {
        string[] slots = (value ?? "0,0,0|0,0,1|0,0,2").Split('|');
        string[] first = (slots.Length > 0 ? slots[0] : "0,0,0").Split(',');
        while (first.Length < 3) first = first.Concat(new[] { "0" }).ToArray();
        first[0] = level.ToString();
        slots[0] = string.Join(",", first);
        return string.Join("|", slots);
    }

    private static HoleData GetHoleData(GamePlayer player)
    {
        var data = new HoleData();
        using (var conn = new SqlConnection(HoleConnectionString))
        {
            conn.Open();
            using (var cmd = new SqlCommand("SELECT OpenHoleIndex, CurHoleExp, SkillHoleExp, HoleDic FROM Sys_Users_SoulRing_Hole_Info WHERE UserID=@uid", conn))
            {
                cmd.Parameters.AddWithValue("@uid", player.PlayerId);
                using (var rd = cmd.ExecuteReader())
                {
                    if (rd.Read())
                    {
                        data.OpenHoleIndex = Convert.ToInt32(rd["OpenHoleIndex"]);
                        data.CurHoleExp = Convert.ToInt32(rd["CurHoleExp"]);
                        data.SkillHoleExp = Convert.ToInt32(rd["SkillHoleExp"]);
                        string raw = rd["HoleDic"] == DBNull.Value ? "" : rd["HoleDic"].ToString();
                        string[] parts = (raw ?? "").Split(',');
                        for (int i = 1; i <= 8; i++)
                            data.Holes[i] = i - 1 < parts.Length && int.TryParse(parts[i - 1], out int v) ? v : -1;
                    }
                }
            }
            using (var cmd = new SqlCommand("IF NOT EXISTS (SELECT 1 FROM Sys_Users_SoulRing_Hole_Info WHERE UserID=@uid) INSERT INTO Sys_Users_SoulRing_Hole_Info (UserID,OpenHoleIndex,CurHoleExp,SkillHoleExp,HoleDic,NeedItemCount1,NeedItemCount2,TemplateId) VALUES (@uid,2,0,0,@dic,0,0,0)", conn))
            {
                cmd.Parameters.AddWithValue("@uid", player.PlayerId);
                cmd.Parameters.AddWithValue("@dic", string.Join(",", data.Holes.Skip(1)));
                cmd.ExecuteNonQuery();
            }
        }
        return data;
    }

    private static void SaveHoleData(GamePlayer player, HoleData data)
    {
        try
        {
            using (var conn = new SqlConnection(HoleConnectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand("IF EXISTS (SELECT 1 FROM Sys_Users_SoulRing_Hole_Info WHERE UserID=@uid) UPDATE Sys_Users_SoulRing_Hole_Info SET OpenHoleIndex=@idx,CurHoleExp=@exp,SkillHoleExp=@sexp,HoleDic=@dic WHERE UserID=@uid ELSE INSERT INTO Sys_Users_SoulRing_Hole_Info (UserID,OpenHoleIndex,CurHoleExp,SkillHoleExp,HoleDic,NeedItemCount1,NeedItemCount2,TemplateId) VALUES (@uid,@idx,@exp,@sexp,@dic,0,0,0)", conn))
                {
                    cmd.Parameters.AddWithValue("@uid", player.PlayerId);
                    cmd.Parameters.AddWithValue("@idx", data.OpenHoleIndex);
                    cmd.Parameters.AddWithValue("@exp", data.CurHoleExp);
                    cmd.Parameters.AddWithValue("@sexp", data.SkillHoleExp);
                    cmd.Parameters.AddWithValue("@dic", string.Join(",", data.Holes.Skip(1)));
                    cmd.ExecuteNonQuery();
                }
            }
        }
        catch (Exception ex) { Console.WriteLine("GemstoneHandler.SaveHoleData: " + ex.Message); }
    }

    private static void SendStoneInfo(GamePlayer player)
    {
        var stones = player.GemStone ?? new List<UserGemStone>();
        GSPacketIn p = new GSPacketIn(693);
        p.WriteByte(1);
        p.WriteInt(player.GetItemCount(AttrMaterial));
        p.WriteInt(player.GetItemCount(SkillMaterial));
        p.WriteInt(stones.Count);
        foreach (UserGemStone stone in stones)
        {
            p.WriteInt(stone.ID);
            p.WriteInt(stone.FigSpiritId);
            p.WriteInt(GetGemLevel(stone));
            p.WriteBoolean(stone.EquipPlace >= 1 && stone.EquipPlace <= 8);
        }
        player.SendTCP(p);
    }

    private static void SendHoleInfo(GamePlayer player)
    {
        HoleData data = GetHoleData(player);
        GSPacketIn p = new GSPacketIn(693);
        p.WriteByte(5);
        p.WriteInt(data.OpenHoleIndex);
        p.WriteInt(data.CurHoleExp);
        p.WriteInt(data.SkillHoleExp);
        for (int i = 1; i <= 8; i++) p.WriteInt(data.Holes[i]);
        player.SendTCP(p);
    }

    private static void OpenHole(GamePlayer player, int count, int holeType)
    {
        if (count <= 0) count = 1;
        HoleData data = GetHoleData(player);
        int haveItem = player.GetItemCount(HoleItem);
        int useItem = Math.Min(count, haveItem);
        if (useItem > 0)
        {
            if (!player.RemoveTemplate(HoleItem, useItem)) return;
        }
        else
        {
            int gold = OpenHolesGold * count;
            if (player.PlayerCharacter.Gold < gold) { player.SendMessage("Không đủ Dùi Huyền Tinh hoặc Gold để mở ô Ngọc."); return; }
            player.RemoveGold(gold);
        }

        bool paidByGold = useItem == 0;
        if (paidByGold)
        {
            if (holeType == 2) { data.Holes[8] = 0; data.SkillHoleExp = 0; }
            else if (data.OpenHoleIndex >= 1 && data.OpenHoleIndex <= 7)
            {
                data.Holes[data.OpenHoleIndex] = 0;
                data.OpenHoleIndex++;
                data.CurHoleExp = 0;
            }
        }
        else
        {
            int addExp = Math.Max(1, count) * 10;
            if (holeType == 2)
            {
                data.SkillHoleExp += addExp;
                if (data.SkillHoleExp >= HoleExps[7]) { data.SkillHoleExp -= HoleExps[7]; data.Holes[8] = 0; }
            }
            else
            {
                data.CurHoleExp += addExp;
                while (data.OpenHoleIndex >= 1 && data.OpenHoleIndex <= 7)
                {
                    int idx = data.OpenHoleIndex;
                    int need = HoleExps[idx];
                    if (data.CurHoleExp < need) break;
                    data.CurHoleExp -= need;
                    data.Holes[idx] = 0;
                    data.OpenHoleIndex++;
                }
            }
        }
        SaveHoleData(player, data);
        SendHoleInfo(player);
        SendStoneInfo(player);
    }

    private static void SetGemHole(GamePlayer player, UserGemStone gem, int hole)
    {
        try
        {
            using (var conn = new SqlConnection(HoleConnectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand("UPDATE Sys_User_Gemstone SET EquipPlace=@place WHERE ID=@id AND UserID=@uid", conn))
                {
                    cmd.Parameters.AddWithValue("@place", hole);
                    cmd.Parameters.AddWithValue("@id", gem.ID);
                    cmd.Parameters.AddWithValue("@uid", player.PlayerId);
                    cmd.ExecuteNonQuery();
                }
            }
            gem.EquipPlace = hole;
        }
        catch (Exception ex) { Console.WriteLine("GemstoneHandler.SetGemHole: " + ex.Message); }
    }

    public static void ValidateWeaponSkill(GamePlayer player)
    {
        try
        {
            if (player == null || player.EquipBag == null) return;
            ItemInfo weapon = player.EquipBag.GetItemAt(6);
            if (weapon == null || weapon.Template == null) return;
            int weaponType = weapon.Template.Property8;
            if (weaponType < 1 || weaponType > 5) return;
            UserGemStone skillGem = (player.GemStone ?? new List<UserGemStone>()).FirstOrDefault(g => g.EquipPlace == 8);
            if (skillGem == null) return;
            if (skillGem.FigSpiritId < 47013 || skillGem.FigSpiritId > 47027) return;
            int skillType = (skillGem.FigSpiritId - 47013) / 3 + 1;
            if (skillType == weaponType) return;
            skillGem.EquipPlace = 0;
            try
            {
                using (var conn = new SqlConnection(HoleConnectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand("UPDATE Sys_User_Gemstone SET EquipPlace=0 WHERE ID=@id AND UserID=@uid", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", skillGem.ID);
                        cmd.Parameters.AddWithValue("@uid", player.PlayerId);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch { }
            HoleData data = GetHoleData(player);
            if (data.Holes[8] == skillGem.ID)
            {
                data.Holes[8] = 0;
                SaveHoleData(player, data);
            }
            SendHoleInfo(player);
            SendStoneInfo(player);
            player.SendMessage("Ngọc Kỹ Năng không phù hợp hệ vũ khí, đã tự động tháo.");
        }
        catch (Exception ex) { Console.WriteLine("GemstoneHandler.ValidateWeaponSkill: " + ex.Message); }
    }

    private static void InsertStone(GamePlayer player, int stoneId, int hole)
    {
        if (hole < 1 || hole > 8) return;
        HoleData data = GetHoleData(player);
        if (data.Holes[hole] != 0) return;
        UserGemStone gem = (player.GemStone ?? new List<UserGemStone>()).FirstOrDefault(s => s.ID == stoneId);
        if (gem == null) return;
        if (gem.EquipPlace >= 1 && gem.EquipPlace <= 8) return;
        if (data.Holes.Skip(1).Any(x => x == stoneId)) return;

        bool skill = IsSkillGem(gem.FigSpiritId);
        if (hole == 8)
        {
            if (!skill) { player.SendMessage("Ô số 8 chỉ dùng cho Ngọc Kỹ Năng."); return; }
        }
        else
        {
            if (skill) { player.SendMessage("Ngọc Kỹ Năng chỉ khảm được vào ô số 8."); return; }
            int same = 0;
            for (int i = 1; i <= 7; i++)
            {
                if (data.Holes[i] <= 0) continue;
                UserGemStone g = (player.GemStone ?? new List<UserGemStone>()).FirstOrDefault(x => x.ID == data.Holes[i]);
                if (g != null && g.FigSpiritId == gem.FigSpiritId) same++;
            }
            if (same >= MaxSameGem) { player.SendMessage("Mỗi loại Ngọc thuộc tính chỉ khảm tối đa 3 viên."); return; }
        }

        data.Holes[hole] = gem.ID;
        SetGemHole(player, gem, hole);
        SaveHoleData(player, data);
        SendHoleInfo(player);
        SendStoneInfo(player);
        try { player.EquipBag.UpdatePlayerProperties(); player.SendUpdatePublicPlayer(); } catch { }
    }

    private static void UnEquipStone(GamePlayer player, int slot)
    {
        if (slot < 0 || slot > 8) return;
        HoleData data = GetHoleData(player);
        var slots = slot == 0 ? Enumerable.Range(1, 8) : new[] { slot };
        foreach (int s in slots)
        {
            int gemId = data.Holes[s];
            data.Holes[s] = 0;
            if (gemId > 0)
            {
                UserGemStone gem = (player.GemStone ?? new List<UserGemStone>()).FirstOrDefault(x => x.ID == gemId);
                if (gem != null) SetGemHole(player, gem, 0);
            }
        }
        SaveHoleData(player, data);
        SendHoleInfo(player);
        SendStoneInfo(player);
        try { player.EquipBag.UpdatePlayerProperties(); player.SendUpdatePublicPlayer(); } catch { }
    }

    private static void UpgradeStone(GamePlayer player, int stoneId)
    {
        UserGemStone gem = (player.GemStone ?? new List<UserGemStone>()).FirstOrDefault(s => s.ID == stoneId);
        if (gem == null) return;
        int level = GetGemLevel(gem);
        if (level >= 10) { player.SendMessage("Ngọc đã đạt cấp tối đa."); SendStoneInfo(player); return; }
        int material = IsSkillGem(gem.FigSpiritId) ? SkillMaterial : AttrMaterial;
        int need = UpgradeNeed[level];
        if (player.GetItemCount(material) < need)
        {
            player.SendMessage(IsSkillGem(gem.FigSpiritId) ? "Không đủ Mảnh Ngọc Lục." : "Không đủ Mảnh Ngọc Lam.");
            return;
        }
        if (!player.RemoveTemplate(material, need)) return;
        level++;
        gem.FigSpiritIdValue = SetGemLevel(gem.FigSpiritIdValue, level);
        try
        {
            using (var conn = new SqlConnection(HoleConnectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand("UPDATE Sys_User_Gemstone SET FigSpiritIdValue=@v WHERE ID=@id AND UserID=@uid", conn))
                {
                    cmd.Parameters.AddWithValue("@v", gem.FigSpiritIdValue);
                    cmd.Parameters.AddWithValue("@id", gem.ID);
                    cmd.Parameters.AddWithValue("@uid", player.PlayerId);
                    cmd.ExecuteNonQuery();
                }
            }
            player.EquipBag.UpdatePlayerProperties();
        }
        catch (Exception ex) { Console.WriteLine("GemstoneHandler.UpgradeStone: " + ex.Message); }
        try { player.EquipBag.UpdatePlayerProperties(); player.SendUpdatePublicPlayer(); } catch { }
        SendStoneInfo(player);
        SendHoleInfo(player);
    }
}
