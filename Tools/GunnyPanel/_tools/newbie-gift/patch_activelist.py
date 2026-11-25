#!/usr/bin/env python3
"""Thêm hoạt động 'Quà Tân Thủ' vào ActiveList.xml của client (file nén zlib)."""
import pathlib
import shutil
import sys
import time
import zlib

PATH = pathlib.Path(sys.argv[1] if len(sys.argv) > 1 else r"C:\Gunny\Gunny12.6\WebSite\Request\ActiveList.xml")

ITEM = (
    '  <Item ActiveID="2026110701" Description="" '
    'Content="Nhận quà tân thủ (mỗi nhân vật chỉ nhận 1 lần)." '
    'AwardContent="Quà Tân Thủ: Nón/Áo/Vũ khí +15, 4 chỉ số +50, dát vàng 365 ngày; '
    'Nhẫn/Vòng tay/Nhẫn Cầu Hôn +50; Vũ khí phụ +15 +50; Kính/Tóc/Mặt/Mắt/Set/Dây chuyền/'
    'Cánh/Bong bóng/Huy chương/Bùa hộ thân; PET + thức ăn + vũ khí/nón/áo PET; Thần Hộ Mệnh 5 Sao." '
    'HasKey="3" EndDate="2035-12-31 23:59:59" IsOnly="1" StartDate="2026-11-07 00:00:00" '
    'Title="Quà Tân Thủ" Type="1" ActiveType="0" ActionTimeContent="" IsAdvance="false" '
    'GoodsExchangeTypes="0" GoodsExchangeNum="0" limitType="0" limitValue="0" IsShow="true" IconID="5" />\n'
)


def main() -> int:
    data = PATH.read_bytes()
    text = zlib.decompress(data).decode("utf-8")
    if "2026110701" in text:
        print("already patched")
        return 0
    marker = "</Result>"
    if marker not in text:
        print("Result marker not found", file=sys.stderr)
        return 2
    text = text.replace(marker, ITEM + marker, 1)
    bak = PATH.with_suffix(PATH.suffix + ".bak-" + time.strftime("%Y%m%d-%H%M%S"))
    shutil.copy2(PATH, bak)
    PATH.write_bytes(zlib.compress(text.encode("utf-8"), 9))
    print(f"patched {PATH} (backup {bak.name})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
