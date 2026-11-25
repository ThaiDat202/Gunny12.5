#!/usr/bin/env python3
"""Patch các lỗi GM: amulet auto-equip, potential equipped-only, remove place check."""
import pathlib
import re
import sys

ROOT = pathlib.Path(r"C:\Gunny\Gunny12.6\Full Source")
GM = ROOT / r"Road.Service\Game.Service.GMService\GmApiActions.cs"
SVC = pathlib.Path(r"C:\Gunny\ToolGunny\GunnyPanel\src\GunnyPanel\Services\GmToolService.cs")
LAT = ROOT / r"game.server\Game.Server.Packets.Client\LatentEnergyHandler.cs"


def read(p):
    return p.read_text(encoding="utf-8-sig")


def write(p, text):
    p.write_text(text, encoding="utf-8-sig")


def patch_gm():
    text = read(GM)
    original = text

    # 1) log mỗi lần apply
    marker = "        if (player != null) result.Snapshot = TrySnapshot(player);\n        return result;"
    if marker in text and "log.Info(\"[GMApi] apply" not in text:
        text = text.replace(marker,
            "        if (player != null) result.Snapshot = TrySnapshot(player);\n"
            "        try { log.Info($\"[GMApi] apply user={userId} feature={feature} online={result.Online} ok={result.Ok} msg={result.Message}\"); } catch { }\n"
            "        return result;", 1)

    # 2) amulet: tìm bùa ở ô 18 / equip / túi và tự trang bị
    if "private static ItemInfo FindAmulet" not in text:
        helper = '''
    /// <summary>Tìm Bùa Hộ Thân (390101): ô 18, ô trang bị khác, hoặc trong túi → tự trang bị vào ô 18.</summary>
    private static ItemInfo FindAmulet(GamePlayer player)
    {
        var at18 = player.EquipBag.GetItemAt(18);
        if (at18 != null && at18.TemplateID == 390101) return at18;

        // đang trang bị ở ô khác (0..30)
        foreach (var it in player.EquipBag.GetItems(0, 31))
        {
            if (it != null && it.TemplateID == 390101) return it;
        }

        // nằm trong túi (31..80) → thử trang bị vào ô 18
        foreach (var it in player.EquipBag.GetItems(31, 81))
        {
            if (it == null || it.TemplateID != 390101) continue;
            if (player.EquipBag.GetItemAt(18) == null)
            {
                try { player.EquipBag.MoveItem(it.Place, 18, it.Count); } catch { }
                var moved = player.EquipBag.GetItemAt(18);
                if (moved != null && moved.TemplateID == 390101) return moved;
            }
            return it;
        }
        return null;
    }

'''
        anchor = "    private static void ApplyAmuletWash(ItemInfo amulet)"
        if anchor in text:
            text = text.replace(anchor, helper + anchor, 1)

    text = text.replace("var amulet = player.EquipBag.GetItemAt(18);", "var amulet = FindAmulet(player);")

    # 3) Phụ Ma/Đúc Hồn: chỉ cần item nằm trong dải ô trang bị, bỏ điều kiện Place
    text = re.sub(r"\n\s*if \(item\.Place < 0 \|\| item\.Place >= 31\) return false;", "", text)

    if text == original:
        print("GM: no changes")
    else:
        write(GM, text)
        print("GM patched")


def patch_service():
    text = read(SVC)
    original = text
    text = text.replace("AND TemplateID = 390101 AND BagType = 0 AND Place = 18",
                        "AND TemplateID = 390101 AND BagType = 0")
    if text == original:
        print("SVC: no changes")
    else:
        write(SVC, text)
        print("SVC patched")


def patch_latent():
    text = read(LAT)
    original = text
    old = """\t\tItemInfo itemAt = client.Player.GetItemAt((eBageType)num2, place);
\t\tif (!itemAt.CanLatentEnergy())
\t\t{
\t\t\tclient.Out.SendMessage(eMessageType.Normal, LanguageMgr.GetTranslation("LatentEnergyHandler.Msg1"));
\t\t\treturn 0;
\t\t}"""
    new = """\t\tItemInfo itemAt = client.Player.GetItemAt((eBageType)num2, place);
\t\t// Chỉ cho phép nâng Tiềm Năng cho trang bị ĐANG TRANG BỊ thuộc Kính/Mắt/Mặt/Tóc/Set áo/Set cánh.
\t\tif (itemAt == null || num2 != 0 || place < 0 || place >= 31 || itemAt.Template == null)
\t\t{
\t\t\tclient.Out.SendMessage(eMessageType.Normal, LanguageMgr.GetTranslation("LatentEnergyHandler.Msg1"));
\t\t\treturn 0;
\t\t}
\t\tint latentCategory = itemAt.Template.CategoryID;
\t\tif (latentCategory != 2 && latentCategory != 3 && latentCategory != 4 &&
\t\t    latentCategory != 6 && latentCategory != 13 && latentCategory != 15)
\t\t{
\t\t\tclient.Out.SendMessage(eMessageType.Normal, LanguageMgr.GetTranslation("LatentEnergyHandler.Msg1"));
\t\t\treturn 0;
\t\t}
\t\tif (!itemAt.CanLatentEnergy())
\t\t{
\t\t\tclient.Out.SendMessage(eMessageType.Normal, LanguageMgr.GetTranslation("LatentEnergyHandler.Msg1"));
\t\t\treturn 0;
\t\t}"""
    if old in text:
        text = text.replace(old, new, 1)
        write(LAT, text)
        print("Latent patched")
    else:
        print("Latent: pattern not found (file may use different whitespace)")


if __name__ == "__main__":
    patch_gm()
    patch_service()
    patch_latent()
