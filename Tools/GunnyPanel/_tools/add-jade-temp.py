import zlib
import pathlib

FILES = [
    r"C:\Gunny\Gunny12.9\Website\Request\TS_JadeTemp.xml",
    r"C:\Gunny\Gunny13.0\Website\Request\TS_JadeTemp.xml",
]
NAMES = {
    100001: ("Chiến hồn tấn công", "Attack"),
    100002: ("Chiến hồn phòng thủ", "Defenc"),
    100003: ("Chiến hồn nhanh nhẹn", "Agility"),
    100004: ("Chiến hồn may mắn", "Lucky"),
    100005: ("Chiến hồn máu", "HP"),
}

for path in FILES:
    p = pathlib.Path(path)
    if not p.exists():
        print("missing", path)
        continue
    original = p.read_bytes()
    data = zlib.decompress(original, 15).decode("utf-8")
    if 'TemplateId="100001"' in data:
        print("already entries", path)
        continue
    added = []
    for tid in range(100001, 100006):
        name, stat = NAMES[tid]
        for lv in range(1, 11):
            val = 100 * lv if stat == "HP" else 20 * lv
            attrs = {
                "Attack": "0",
                "Defenc": "0",
                "Agility": "0",
                "Lucky": "0",
                "HP": "0",
                "MagicAttack": "0",
                "MagicDefenc": "0",
            }
            attrs[stat] = str(val)
            line = (
                '  <Item TemplateId="%d" TemplateName="%s Lv%d" Types="3" Level="%d" '
                'NeedItem="-4700" NeedItemCount="0" Attack="%s" Defenc="%s" Agility="%s" '
                'Lucky="%s" HP="%s" MagicAttack="%s" MagicDefenc="%s" FireAtck="0" '
                'WateAtck="0" WindAtck="0" LandAtck="0" FireDefc="0" WateDefc="0" '
                'WindDefc="0" LandDefc="0" SkillId="0" Description="Chiến hồn Ngọc - dùng cho Khâm Ngọc" '
                'LighAtck="0" DrakAtck="0" LighDefc="0" DrakDefc="0" />'
            ) % (
                tid,
                name,
                lv,
                lv,
                attrs["Attack"],
                attrs["Defenc"],
                attrs["Agility"],
                attrs["Lucky"],
                attrs["HP"],
                attrs["MagicAttack"],
                attrs["MagicDefenc"],
            )
            added.append(line)
    data = data.replace("</Result>", "\n".join(added) + "\n</Result>")
    backup = p.with_suffix(".xml.before-gem")
    if not backup.exists():
        backup.write_bytes(original)
    p.write_bytes(zlib.compress(data.encode("utf-8"), 9))
    print("updated", path, "entries", len(added))
