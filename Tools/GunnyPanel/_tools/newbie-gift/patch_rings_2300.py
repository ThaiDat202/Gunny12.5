#!/usr/bin/env python3
"""Auto-equip nhẫn/vòng tay/nhẫn cầu hôn trước khi Phụ Ma / Đúc Hồn."""
import pathlib
import re

GM = pathlib.Path(r"C:\Gunny\Gunny12.6\Full Source\Road.Service\Game.Service.GMService\GmApiActions.cs")


def main() -> int:
    text = GM.read_text(encoding="utf-8-sig")
    original = text

    if "private static int EnsureRingsEquipped" not in text:
        helper = '''
    /// <summary>
    /// Tự trang bị Nhẫn/Vòng tay/Nhẫn cầu hôn đang nằm trong túi (slot 31..80) vào ô trang bị
    /// tương ứng, để Phụ Ma/Đúc Hồn hoạt động cả khi người chơi chỉ mới sở hữu item.
    /// Trả về số trang bị đã ở trong ô sau khi xử lý.
    /// </summary>
    private static int EnsureRingsEquipped(GamePlayer player)
    {
        var bag = player.EquipBag;
        int[] slots = { 7, 8, 9, 10, 16 };
        foreach (var item in bag.GetItems(31, 81))
        {
            if (item == null || item.Template == null) continue;
            int cat = item.Template.CategoryID;
            if (cat != 8 && cat != 9 && cat != 28 && cat != 29) continue;
            foreach (var slot in slots)
            {
                if (bag.GetItemAt(slot) != null) continue;
                if (!bag.CanEquipSlotContains(slot, item.Template)) continue;
                try
                {
                    if (bag.MoveItem(item.Place, slot, item.Count)) break;
                }
                catch { }
            }
        }

        int count = 0;
        foreach (var item in bag.GetItems(0, 31))
        {
            if (item == null || item.Template == null) continue;
            int cat = item.Template.CategoryID;
            if (cat == 8 || cat == 9 || cat == 28 || cat == 29) count++;
        }
        return count;
    }

'''
        anchor = "    private static void PhuMa(GamePlayer player)"
        if anchor in text:
            text = text.replace(anchor, helper + anchor, 1)

    # PhuMa/DucHon: ensure rings equipped first
    if "if (EnsureRingsEquipped(player) == 0)" not in text:
        text = text.replace(
            "    private static void PhuMa(GamePlayer player)\n    {\n",
            "    private static void PhuMa(GamePlayer player)\n    {\n        if (EnsureRingsEquipped(player) == 0) throw new GmActionException(\"Bạn chưa trang bị nhẫn và vòng tay.\");\n", 1)
        text = text.replace(
            "    private static void DucHon(GamePlayer player)\n    {\n",
            "    private static void DucHon(GamePlayer player)\n    {\n        if (EnsureRingsEquipped(player) == 0) throw new GmActionException(\"Bạn chưa trang bị nhẫn và vòng tay.\");\n", 1)

    if text == original:
        print("no changes")
        return 1
    GM.write_text(text, encoding="utf-8-sig")
    print("patched EnsureRingsEquipped")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
