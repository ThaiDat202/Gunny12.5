import pathlib

p = pathlib.Path(r"C:\Gunny\Gunny13.0\Full Source\game.server\Game.Server.GameUtils\PlayerEquipInventory.cs")
t = p.read_text(encoding="utf-8-sig")
start = t.index("\tpublic void AddBaseGemstoneProperty(")
end = t.rfind("\n}")
assert end > start
new = '''\tpublic void AddBaseGemstoneProperty(ItemInfo item, ref int attack, ref int defence, ref int agility, ref int lucky, ref int hp)
\t{
\t\tif (item == null || item.Template == null) return;
\t\t// Ngọc Hồn Vũ Khí chỉ áp dụng cho Vũ Khí (CategoryID 7)
\t\tif (item.Template.CategoryID != 7) return;

\t\tdouble mult = GetGemWeaponMultiplier(item.Template.Attack);
\t\tforeach (UserGemStone gem in m_player.GemStone)
\t\t{
\t\t\tif (gem == null || gem.EquipPlace < 1 || gem.EquipPlace > 7) continue;
\t\t\tint lv = 0;
\t\t\ttry
\t\t\t{
\t\t\t\tlv = Convert.ToInt32((gem.FigSpiritIdValue ?? "0,0,0").Split('|')[0].Split(',')[0]);
\t\t\t}
\t\t\tcatch { continue; }
\t\t\tif (lv <= 0) continue;
\t\t\tswitch (gem.FigSpiritId)
\t\t\t{
\t\t\t\tcase 100001: // legacy tấn công
\t\t\t\tcase 47034:  // Ngọc-Công Sáng
\t\t\t\tcase 47035:  // Ngọc-Công Tối
\t\t\t\t\tattack += (int)Math.Round(30 * lv * mult);
\t\t\t\t\tbreak;
\t\t\t\tcase 100002: // legacy phòng thủ
\t\t\t\tcase 47028:  // Ngọc-Phòng Thủ
\t\t\t\t\tdefence += (int)Math.Round(30 * lv * mult);
\t\t\t\t\tbreak;
\t\t\t\tcase 100003: // legacy nhanh nhẹn
\t\t\t\t\tagility += (int)Math.Round(30 * lv * mult);
\t\t\t\t\tbreak;
\t\t\t\tcase 100004: // legacy may mắn
\t\t\t\tcase 47001:  // Ngọc-May Mắn
\t\t\t\t\tlucky += (int)Math.Round(30 * lv * mult);
\t\t\t\t\tbreak;
\t\t\t\tcase 100005: // legacy máu
\t\t\t\tcase 47003:  // Ngọc-HP
\t\t\t\t\thp += (int)Math.Round(100 * lv * mult);
\t\t\t\t\tbreak;
\t\t\t\tcase 47002:  // Ma Công
\t\t\t\t\tattack += (int)Math.Round(15 * lv * mult);
\t\t\t\t\tbreak;
\t\t\t\tcase 47004:  // Ma Kháng
\t\t\t\t\tdefence += (int)Math.Round(15 * lv * mult);
\t\t\t\t\tbreak;
\t\t\t}
\t\t}
\t}

\tprivate static double GetGemWeaponMultiplier(int damage)
\t{
\t\tif (damage <= 204) return 1.00;
\t\tif (damage <= 234) return 1.05;
\t\tif (damage <= 246) return 1.10;
\t\tif (damage <= 259) return 1.15;
\t\tif (damage <= 269) return 1.20;
\t\tif (damage <= 284) return 1.25;
\t\tif (damage <= 287) return 1.30;
\t\treturn 1.40;
\t}

'''
t = t[:start] + new + "}"
p.write_text(t, encoding="utf-8-sig")
print("gemstone stats method replaced official")
