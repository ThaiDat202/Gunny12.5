import pathlib

p = pathlib.Path(r"C:\Gunny\ToolGunny\GunnyPanel\_tools\newbie-gift\verify.ps1")
text = p.read_text(encoding="utf-8-sig")

text = text.replace("Sys_NewbieGiftItems 162 dòng", "Sys_NewbieGiftItems 160 dòng")

old_check = "Check 'XML vẫn giữ Quà Vui Vẻ' ($xml -like '*Quà Vui Vẻ*')"
new_check = "Check 'XML không còn Quà Vui Vẻ' (-not ($xml -like '*Quà Vui Vẻ*'))"
text = text.replace(old_check, new_check)

p.write_text(text, encoding="utf-8-sig")
print("fixed")
