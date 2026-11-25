#!/usr/bin/env python3
"""Quét file client (SWF/XML/TXT/JS) tìm chuỗi UTF-8/UTF-16 liên quan hoạt động."""
import pathlib
import sys

ROOT = pathlib.Path(r"C:\Gunny\Gunny12.6\WebSite")

NEEDLES = [
    "ActiveList.xml",
    "ActiveList",
    "ActivePullDown",
    "UserGetActiveState",
    "Hoạt động hấp dẫn",
    "Hấp dẫn",
    "Quà Tân Thủ",
    "Quà Vui Vẻ",
    "GmActivityInfo",
    "Nhận thưởng",
]


def encodings_for(text: str):
    yield text.encode("utf-8")
    yield text.encode("utf-16-le")
    yield text.encode("utf-16-be")


def main() -> int:
    max_size = 30 * 1024 * 1024
    count = 0
    for p in ROOT.rglob("*"):
        if not p.is_file():
            continue
        try:
            if p.stat().st_size > max_size:
                continue
            data = p.read_bytes()
        except Exception:
            continue
        for needle in NEEDLES:
            for enc in encodings_for(needle):
                if enc in data:
                    print(f"{p.relative_to(ROOT)}\t{needle}\t{ 'utf-8' if enc==needle.encode('utf-8') else ('utf-16-le' if enc==needle.encode('utf-16-le') else 'utf-16-be') }")
                    count += 1
                    break
    print(f"matches={count}", file=sys.stderr)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
