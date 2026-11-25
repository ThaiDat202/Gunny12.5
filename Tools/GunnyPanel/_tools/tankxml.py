#!/usr/bin/env python3
"""Giải nén / nén XML của Gunny (zlib, header 78 DA)."""
import sys
import zlib
import pathlib


def decompress(src: str, dst: str) -> None:
    data = pathlib.Path(src).read_bytes()
    out = zlib.decompress(data)
    pathlib.Path(dst).write_bytes(out)
    print(f"decompress {src} -> {dst} ({len(data)} -> {len(out)} bytes)")


def compress(src: str, dst: str, level: int = 9) -> None:
    data = pathlib.Path(src).read_bytes()
    out = zlib.compress(data, level)
    pathlib.Path(dst).write_bytes(out)
    print(f"compress {src} -> {dst} ({len(data)} -> {len(out)} bytes)")


if __name__ == "__main__":
    cmd = sys.argv[1] if len(sys.argv) > 1 else ""
    if cmd == "decompress" and len(sys.argv) >= 4:
        decompress(sys.argv[2], sys.argv[3])
    elif cmd == "compress" and len(sys.argv) >= 4:
        level = int(sys.argv[4]) if len(sys.argv) > 4 else 9
        compress(sys.argv[2], sys.argv[3], level)
    else:
        print("usage: tankxml.py decompress <src> <dst> | compress <src> <dst> [level]")
        sys.exit(1)
