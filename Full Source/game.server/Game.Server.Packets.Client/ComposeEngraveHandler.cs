using System;
using Bussiness;
using Bussiness.Managers;
using EntityDatabase.ServerModels;
using Game.Base.Packets;
using Game.Server.GameObjects;
using SqlDataProvider.Data;

namespace Game.Server.Packets.Client;

/// <summary>
/// Khắc Đá Hợp Thành (packet 700).
/// - Trang bị phải hợp thành đủ 4 chỉ số 50.
/// - Mỗi lần khắc dùng Đá Khắc 12264 + Đá Hợp Thành trong StoreBag để tăng tỷ lệ.
/// - Bùa May Mắn-Siêu 12265 tăng 10% tỷ lệ.
/// - Chỉ số khắc tối đa = 50 (hợp thành) + 50 (khắc) = 100 mỗi chỉ số.
/// </summary>
[PacketHandler(700, "Khắc Đá Hợp Thành")]
public class ComposeEngraveHandler : IPacketHandler
{
    private static readonly Random random = new Random();

    private static readonly int[][] RateStoneIds =
    {
        null,
        new[] { 11001, 11002, 11003, 11004, 11030 }, // Tấn công
        new[] { 11005, 11006, 11007, 11008, 11031 }, // Phòng thủ
        new[] { 11009, 11010, 11011, 11012, 11032 }, // Nhanh nhẹn
        new[] { 11013, 11014, 11015, 11016, 11033 }  // May mắn
    };

    public int HandlePacket(GameClient client, GSPacketIn packet)
    {
        GamePlayer player = client.Player;
        int type = packet.ReadInt();       // 1..4
        bool useLucky = packet.ReadBoolean();

        GSPacketIn result = new GSPacketIn(700, player.PlayerCharacter.ID);
        if (type < 1 || type > 4)
        {
            player.SendMessage("Loại khắc không hợp lệ.");
            result.WriteByte(1);
            player.SendTCP(result);
            return 0;
        }

        ItemInfo equip = player.StoreBag.GetItemAt(1);
        if (equip == null || equip.Template == null || !equip.Template.CanCompose)
        {
            player.SendMessage("Hãy đặt trang bị cần khắc vào ô Trang bị.");
            result.WriteByte(1);
            player.SendTCP(result);
            return 0;
        }

        if (equip.AttackCompose < 50 || equip.DefendCompose < 50 ||
            equip.AgilityCompose < 50 || equip.LuckCompose < 50)
        {
            player.SendMessage("Trang bị phải hợp thành đủ +50 cả 4 chỉ số mới có thể khắc.");
            result.WriteByte(1);
            player.SendTCP(result);
            return 0;
        }

        int types = 115 + type; // 116: tấn công, 117: phòng thủ, 118: nhanh nhẹn, 119: may mắn
        int stat = GetStat(equip, type);
        if (stat >= 100)
        {
            player.SendMessage("Chỉ số này đã đạt mức khắc tối đa.");
            result.WriteByte(1);
            player.SendTCP(result);
            return 0;
        }

        TS_UpgradeTemplate current = FindByStat(types, stat);
        TS_UpgradeTemplate next = current == null ? null : ItemMgr.GetComposeUpgradeInfo(types, current.Grades + 1);
        if (current == null || next == null)
        {
            player.SendMessage("Không tìm thấy cấu hình khắc phù hợp.");
            result.WriteByte(1);
            player.SendTCP(result);
            return 0;
        }

        int needStone = current.Param1;
        if (needStone > 0 && player.GetItemCount(12264) < needStone)
        {
            player.SendMessage("Không đủ Đá Khắc.");
            result.WriteByte(1);
            player.SendTCP(result);
            return 0;
        }

        int points = 0;
        for (int slot = 2; slot <= 5; slot++)
        {
            ItemInfo material = player.StoreBag.GetItemAt(slot);
            if (material == null || material.Template == null) continue;
            if (!IsRateStone(type, material.TemplateID)) continue;
            points += material.Template.Property5;
        }

        int rateDiv = current.Param2;
        if (rateDiv <= 0) rateDiv = 1000;
        double rate = points * 100.0 / rateDiv;
        if (useLucky && player.GetItemCount(12265) > 0) rate *= 1.1;
        if (rate > 100) rate = 100;

        // Tiêu hao nguyên liệu
        if (needStone > 0 && !player.RemoveTemplate(12264, needStone))
        {
            player.SendMessage("Không thể tiêu hao Đá Khắc.");
            result.WriteByte(1);
            player.SendTCP(result);
            return 0;
        }
        for (int slot = 2; slot <= 5; slot++)
        {
            ItemInfo material = player.StoreBag.GetItemAt(slot);
            if (material == null || material.Template == null) continue;
            if (!IsRateStone(type, material.TemplateID)) continue;
            player.StoreBag.RemoveTemplate(material.TemplateID, 1);
        }
        if (useLucky) player.RemoveTemplate(12265, 1);

        bool success = random.Next(10000) < rate * 100.0;
        if (success)
        {
            SetStat(equip, type, 50 + next.Data);
            equip.IsBinds = true;
            player.StoreBag.UpdateItem(equip);
            player.SendMessage("Khắc thành công! " + next.TemplateName);
            result.WriteByte(0);
        }
        else
        {
            player.SendMessage("Khắc thất bại.");
            result.WriteByte(1);
        }
        player.SendTCP(result);
        return 0;
    }

    private static int GetStat(ItemInfo item, int type)
    {
        switch (type)
        {
            case 1: return item.AttackCompose;
            case 2: return item.DefendCompose;
            case 3: return item.AgilityCompose;
            default: return item.LuckCompose;
        }
    }

    private static void SetStat(ItemInfo item, int type, int value)
    {
        switch (type)
        {
            case 1: item.AttackCompose = value; break;
            case 2: item.DefendCompose = value; break;
            case 3: item.AgilityCompose = value; break;
            default: item.LuckCompose = value; break;
        }
    }

    private static TS_UpgradeTemplate FindByStat(int types, int stat)
    {
        if (stat < 50) return null;
        int data = stat - 50;
        for (int grade = 0; grade <= 10; grade++)
        {
            TS_UpgradeTemplate info = ItemMgr.GetComposeUpgradeInfo(types, grade);
            if (info != null && info.Data == data) return info;
        }
        return null;
    }

    private static bool IsRateStone(int type, int templateId)
    {
        int[] ids = RateStoneIds[type];
        if (ids == null) return false;
        return Array.IndexOf(ids, templateId) >= 0;
    }
}
