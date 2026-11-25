using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Bussiness;
using Game.Base.Packets;
using Game.Server.GameObjects;
using SqlDataProvider.Data;

namespace Game.Server.Packets.Client;

/// <summary>
/// Gia Công Trang Sức (packet 682): Tăng bậc + Tẩy luyện.
/// </summary>
[PacketHandler(682, "Gia Công Trang Sức")]
public class CultureHandler : IPacketHandler
{
    private const int MachiningItem = 12249; // Phỉ Thúy
    private const int QuenchingItem = 12250; // Mã Não
    private const int SkillStone = 12251;    // Hắc Diệu
    private const int SkillStoneCost = 10;   // JewelSkillStoneCost

    private static readonly ConcurrentDictionary<long, string> PendingReset = new ConcurrentDictionary<long, string>();

    public int HandlePacket(GameClient client, GSPacketIn packet)
    {
        byte cmd = packet.ReadByte();
        GamePlayer player = client.Player;
        if (player.PlayerCharacter.Grade < 15)
        {
            player.SendMessage("Cần đạt cấp 15 để sử dụng Gia Công Trang Sức.");
            return 0;
        }
        switch (cmd)
        {
            case 1:
                SendInfo(player);
                break;
            case 2:
                {
                    int place = packet.ReadInt();
                    bool all = packet.ReadBoolean();
                    DoMachining(player, place, all);
                    break;
                }
            case 3:
                {
                    int place = packet.ReadInt();
                    bool all = packet.ReadBoolean();
                    DoQuenching(player, place, all);
                    break;
                }
            case 4:
                {
                    int place = packet.ReadInt();
                    int lockCount = packet.ReadInt();
                    var locked = new HashSet<int>();
                    for (int i = 0; i < lockCount; i++) locked.Add(packet.ReadInt());
                    DoReset(player, place, locked);
                    break;
                }
            case 5:
                {
                    int place = packet.ReadInt();
                    bool accept = packet.ReadBoolean();
                    DoConfirm(player, place, accept);
                    break;
                }
        }
        return 0;
    }

    private static JewelCultureInfo GetCulture(GamePlayer player, int place)
    {
        if (player.JewelCulture == null) player.JewelCulture = new Dictionary<int, JewelCultureInfo>();
        if (!player.JewelCulture.TryGetValue(place, out JewelCultureInfo info))
        {
            info = JewelCultureMgr.GetOrCreate(player.PlayerId, place);
            player.JewelCulture[place] = info;
        }
        return info;
    }

    private static void SendInfo(GamePlayer player)
    {
        GSPacketIn p = new GSPacketIn(682);
        p.WriteByte(1);
        var list = player.JewelCulture != null ? player.JewelCulture.Values.ToList() : new List<JewelCultureInfo>();
        p.WriteInt(list.Count);
        foreach (JewelCultureInfo info in list)
        {
            p.WriteInt(info.Place);
            p.WriteInt(info.MachiningExp);
            p.WriteInt(info.QuenchingExp);
            p.WriteString(info.SkillStr ?? "");
        }
        player.SendTCP(p);
    }

    private static int GetJewelryLevel(ItemInfo item)
    {
        if (item == null) return 0;
        int level = item.StrengthenLevel;
        if (item.Template != null && item.Template.Property1 > level) level = item.Template.Property1;
        return level;
    }

    private static void DoMachining(GamePlayer player, int place, bool all)
    {
        ItemInfo item = player.StoreBag.GetItemAt(0);
        if (item == null || item.Template == null)
        {
            player.SendMessage("Hãy đặt trang sức cần gia công vào ô.");
            return;
        }
        int count = all ? player.GetItemCount(MachiningItem) : 1;
        if (count <= 0)
        {
            player.SendMessage("Không đủ Phỉ Thúy.");
            return;
        }
        if (!player.RemoveTemplate(MachiningItem, count)) return;
        JewelCultureInfo info = GetCulture(player, place);
        if (info.MachiningExp < JewelCultureMgr.GetMachiningMaxExp())
        {
            info.MachiningExp += 10 * count;
            if (info.MachiningExp > JewelCultureMgr.GetMachiningMaxExp())
                info.MachiningExp = JewelCultureMgr.GetMachiningMaxExp();
        }
        JewelCultureMgr.Save(info);
        SendInfo(player);
        try { player.EquipBag.UpdatePlayerProperties(); } catch { }
        if (item.Place >= 7 && item.Place <= 10)
            player.SendMessage("Tăng bậc thành công: Lv." + JewelCultureMgr.GetMachiningLevel(info.MachiningExp));
    }

    private static void DoQuenching(GamePlayer player, int place, bool all)
    {
        ItemInfo item = player.StoreBag.GetItemAt(0);
        if (item == null || item.Template == null)
        {
            player.SendMessage("Hãy đặt trang sức cần tẩy luyện vào ô.");
            return;
        }
        JewelCultureInfo info = GetCulture(player, place);
        int jewelrySlots = JewelCultureMgr.GetOpenedByJewelryLevel(GetJewelryLevel(item));
        if (jewelrySlots <= 0)
        {
            player.SendMessage("Cần nâng cấp trang sức lên +2 trở lên để mở dòng Tẩy luyện.");
            return;
        }
        int currentSlots = JewelCultureMgr.GetQuenchingSlots(JewelCultureMgr.GetQuenchingLevel(info.QuenchingExp));
        if (currentSlots >= jewelrySlots || info.QuenchingExp >= JewelCultureMgr.GetQuenchingMaxExp())
        {
            player.SendMessage("Tẩy luyện đã đạt tối đa theo cấp trang sức.");
            return;
        }
        int nextExp = JewelCultureMgr.GetQuenchingNextExp(info.QuenchingExp);
        int count = all ? player.GetItemCount(QuenchingItem) : 1;
        int needExp = Math.Max(0, nextExp - info.QuenchingExp);
        int maxUse = (needExp + 9) / 10;
        if (count > maxUse) count = maxUse;
        if (count <= 0)
        {
            player.SendMessage("Không đủ Mã Não.");
            return;
        }
        if (!player.RemoveTemplate(QuenchingItem, count)) return;
        info.QuenchingExp = Math.Min(nextExp, info.QuenchingExp + 10 * count);
        int slots = Math.Min(JewelCultureMgr.GetQuenchingSlots(JewelCultureMgr.GetQuenchingLevel(info.QuenchingExp)), jewelrySlots);
        info.SkillStr = JewelCultureMgr.EnsureSkillStr(slots, info.SkillStr);
        JewelCultureMgr.Save(info);
        SendInfo(player);
        try { player.EquipBag.UpdatePlayerProperties(); } catch { }
        player.SendMessage("Tẩy luyện: mở được " + JewelCultureMgr.GetQuenchingSlots(JewelCultureMgr.GetQuenchingLevel(info.QuenchingExp)) + " ô.");
    }

    private static void DoReset(GamePlayer player, int place, HashSet<int> locked)
    {
        int lockCount = locked == null ? 0 : locked.Count;
        JewelCultureInfo info = GetCulture(player, place);
        ItemInfo item = player.StoreBag.GetItemAt(0);
        int jewelrySlots = item == null ? 0 : JewelCultureMgr.GetOpenedByJewelryLevel(GetJewelryLevel(item));
        int slots = Math.Min(JewelCultureMgr.GetQuenchingSlots(JewelCultureMgr.GetQuenchingLevel(info.QuenchingExp)), jewelrySlots);
        if (slots <= 0)
        {
            player.SendMessage("Cần mở dòng Tẩy luyện trước (trang sức +2 trở lên).");
            return;
        }
        if (lockCount >= slots)
        {
            player.SendMessage("Chỉ được khóa tối đa " + (slots - 1) + " dòng (số dòng đã mở - 1).");
            return;
        }
        string proposed = JewelCultureMgr.GenerateOneSkillStr(slots, info.SkillStr, locked);
        if (proposed == (info.SkillStr ?? ""))
        {
            player.SendMessage("Tất cả dòng đã mở đang bị khóa, không thể tẩy.");
            return;
        }
        int cost = SkillStoneCost * (1 + lockCount);
        if (player.GetItemCount(SkillStone) < cost)
        {
            player.SendMessage("Không đủ Hắc Diệu (cần " + cost + " cho " + lockCount + " thuộc tính khóa).");
            return;
        }
        if (!player.RemoveTemplate(SkillStone, cost)) return;
        PendingReset[((long)player.PlayerId << 32) ^ (uint)place] = proposed;
        GSPacketIn p = new GSPacketIn(682);
        p.WriteByte(4);
        p.WriteInt(place);
        p.WriteString(proposed);
        player.SendTCP(p);
    }

    private static void DoConfirm(GamePlayer player, int place, bool accept)
    {
        long key = ((long)player.PlayerId << 32) ^ (uint)place;
        PendingReset.TryRemove(key, out string proposed);
        JewelCultureInfo info = GetCulture(player, place);
        if (accept && proposed != null)
        {
            info.SkillStr = proposed;
            JewelCultureMgr.Save(info);
        }
        GSPacketIn p = new GSPacketIn(682);
        p.WriteByte(5);
        p.WriteInt(place);
        p.WriteBoolean(accept);
        p.WriteString(info.SkillStr ?? "");
        player.SendTCP(p);
        try { player.EquipBag.UpdatePlayerProperties(); } catch { }
    }
}
