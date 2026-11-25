#!/usr/bin/env python3
"""Fix isWeddingRing (chỉ đúng vật định tình) + auto-equip đúng ô cho nhẫn."""
import pathlib
import re

EQUIP = pathlib.Path(r"C:\Gunny\Gunny12.6\Full Source\SqlDataProvider\SqlDataProvider.Data\Equip.cs")
GM = pathlib.Path(r"C:\Gunny\Gunny12.6\Full Source\Road.Service\Game.Service.GMService\GmApiActions.cs")


def main() -> int:
    # 1) Equip.isWeddingRing: default phải false
    text = EQUIP.read_text(encoding="utf-8-sig")
    old = """	public static bool isWeddingRing(ItemTemplateInfo info)
	{
		switch (info.TemplateID)
		{
		case 9022:
		case 9122:
		case 9222:
		case 9322:
		case 9422:
		case 9522:
			return true;
		default:
			return true;
		}
	}"""
    new = """	public static bool isWeddingRing(ItemTemplateInfo info)
	{
		switch (info.TemplateID)
		{
		case 9022:
		case 9122:
		case 9222:
		case 9322:
		case 9422:
		case 9522:
			return true;
		default:
			return false;
		}
	}"""
    if old in text:
        text = text.replace(old, new, 1)
        EQUIP.write_text(text, encoding="utf-8-sig")
        print("Equip.isWeddingRing fixed")
    else:
        print("Equip: pattern not found")

    # 2) GmApiActions: auto-equip nhẫn đúng ô
    text = GM.read_text(encoding="utf-8-sig")
    start = text.find("    private static int EnsureRingsEquipped")
    if start < 0:
        print("GM: EnsureRingsEquipped not found")
        return 1
    end = text.find("\n    private static void PhuMa", start)
    if end < 0:
        print("GM: PhuMa anchor not found")
        return 1
    new_helper = '''    /// <summary>Ô hợp lệ cho từng loại: vòng tay 7-8, nhẫn 9-10, vật định tình chỉ vào ô Nhẫn Cầu Hôn (16).</summary>
    private static int[] RingSlotsFor(ItemTemplateInfo temp)
    {
        int cat = temp.CategoryID;
        if (cat == 8 || cat == 28) return new[] { 7, 8 };
        if (cat == 9 || cat == 29)
            return Equip.isWeddingRing(temp) ? new[] { 16 } : new[] { 9, 10 };
        return new int[0];
    }

    /// <summary>
    /// Tự trang bị Nhẫn/Vòng tay/Nhẫn cầu hôn đang nằm trong túi (slot 31..80) vào đúng ô trang bị.
    /// Vật định tình chỉ vào ô 16; nhẫn thường chỉ 9/10; vòng tay 7/8.
    /// Trả về số trang bị đã ở trong ô sau khi xử lý.
    /// </summary>
    private static int EnsureRingsEquipped(GamePlayer player)
    {
        var bag = player.EquipBag;
        foreach (var item in bag.GetItems(31, 81))
        {
            if (item == null || item.Template == null) continue;
            var slots = RingSlotsFor(item.Template);
            if (slots.Length == 0) continue;
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
    text = text[:start] + new_helper + text[end + 1:]
    # ensure using for Equip
    if "using SqlDataProvider.Data;" not in text:
        print("GM: missing using SqlDataProvider.Data (Equip may not resolve)")
    GM.write_text(text, encoding="utf-8-sig")
    print("GM EnsureRingsEquipped rewritten")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
