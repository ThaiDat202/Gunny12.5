#!/usr/bin/env python3
"""Giải nén SWF (CWS) + quét chuỗi liên quan hoạt động; ghi kết quả UTF-8."""
import pathlib
import sys
import zlib

ROOT = pathlib.Path(r"C:\Gunny\Gunny12.6\WebSite")
OUT = pathlib.Path(r"C:\Gunny\ToolGunny\GunnyPanel\data\_activity_debug\swf_scan.txt")

NEEDLES = [
    "ActiveList.xml",
    "ActiveList.ashx",
    "ActivePullDown.ashx",
    "ActivePullDown",
    "UserGetActiveState.ashx",
    "Hoạt động hấp dẫn",
    "Hấp dẫn",
    "Quà Tân Thủ",
    "Quà Vui Vẻ",
    "GmActivityInfo.xml",
    "ActiveEventsInfo",
    "Nhận thưởng",
]


def swf_bodies(path: pathlib.Path):
    data = path.read_bytes()
    yield "raw", data
    if data[:3] == b"CWS":
        try:
            yield "inflated", zlib.decompress(data[8:])
        except Exception:
            pass
    elif data[:3] == b"ZWS":
        try:
            yield "lzma", zlib.decompress(data[12:])
        except Exception:
            pass


def encodings(text: str):
    return [
        ("utf-8", text.encode("utf-8")),
        ("utf-16-le", text.encode("utf-16-le")),
    ]


def main() -> int:
    lines = []
    files = 0
    for p in ROOT.rglob("*"):
        if not p.is_file():
            continue
        try:
            if p.stat().st_size > 60 * 1024 * 1024:
                continue
            if p.suffix.lower() not in (".swf", ".xml", ".txt", ".js", ".as", ".json", ".htm", ".html", ".php", ".aspx", ".ashx"):
                continue
            files += 1
            for kind, body in swf_bodies(p):
                for needle in NEEDLES:
                    for enc_name, enc in encodings(needle):
                        if enc in body:
                            lines.append(f"{p.relative_to(ROOT)}\t{kind}\t{needle}\t{enc_name}")
                            break
        except Exception as exc:  # noqa: BLE001
            lines.append(f"ERR {p}: {exc}")
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text("\n".join(lines), encoding="utf-8")
    print(f"files={files} matches={len(lines)} -> {OUT}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
