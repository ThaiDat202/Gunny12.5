#!/usr/bin/env python3
"""Recon: check which local WebSite\\Flash files exist on VNG CDN (HEAD request).

Outputs JSONL + summary JSON into data/rip/_recon.
"""
import json
import os
import sys
import time
import urllib.request
import urllib.error
from concurrent.futures import ThreadPoolExecutor, as_completed

ROOT = r"C:\Gunny\Gunny12.6\WebSite\Flash"
BASE = "https://gunny.vcdn.vn/flash/"
OUTDIR = r"C:\Gunny\ToolGunny\GunnyPanel\data\rip\_recon"
OUTJSONL = os.path.join(OUTDIR, "cdn-scan.jsonl")
OUTSUM = os.path.join(OUTDIR, "cdn-scan-summary.json")
WORKERS = 16
TIMEOUT = 25
UA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) GunnyPanel/1.0"


def list_files(root):
    out = []
    for dirpath, _dirnames, filenames in os.walk(root):
        for fn in filenames:
            full = os.path.join(dirpath, fn)
            rel = os.path.relpath(full, root).replace(os.sep, "/")
            try:
                size = os.path.getsize(full)
            except OSError:
                size = -1
            out.append((rel, size))
    return sorted(out)


def head(url):
    req = urllib.request.Request(url, method="HEAD", headers={"User-Agent": UA})
    try:
        with urllib.request.urlopen(req, timeout=TIMEOUT) as r:
            length = r.headers.get("Content-Length")
            ctype = r.headers.get("Content-Type", "")
            return r.status, int(length) if length and length.isdigit() else None, ctype, ""
    except urllib.error.HTTPError as e:
        return e.code, None, "", ""
    except Exception as e:  # noqa: BLE001
        return 0, None, "", str(e)


def main():
    os.makedirs(OUTDIR, exist_ok=True)
    files = list_files(ROOT)
    total = len(files)
    print(f"local files: {total}", flush=True)
    stats = {"total": total, "ok": 0, "missing": 0, "error": 0, "ok_bytes": 0, "local_bytes": 0}
    missing = []
    errors = []
    started = time.time()
    with open(OUTJSONL, "w", encoding="utf-8") as fh, ThreadPoolExecutor(max_workers=WORKERS) as pool:
        futures = {pool.submit(head, BASE + rel): (rel, local_size) for rel, local_size in files}
        done = 0
        for fut in as_completed(futures):
            rel, local_size = futures[fut]
            status, rlen, ctype, err = fut.result()
            done += 1
            stats["local_bytes"] += max(0, local_size)
            rec = {"path": rel, "local": local_size, "status": status, "cdn": rlen, "type": ctype}
            if status == 200:
                stats["ok"] += 1
                stats["ok_bytes"] += rlen or 0
                if rlen is not None and rlen != local_size:
                    rec["diff"] = True
            elif status in (403, 404, 410):
                stats["missing"] += 1
                missing.append(rec)
            else:
                stats["error"] += 1
                rec["err"] = err
                errors.append(rec)
            fh.write(json.dumps(rec, ensure_ascii=False) + "\n")
            if done % 100 == 0:
                elapsed = time.time() - started
                print(f"{done}/{total} ok={stats['ok']} missing={stats['missing']} err={stats['error']} ({elapsed:.0f}s)", flush=True)
    summary = {
        "base": BASE,
        "root": ROOT,
        "workers": WORKERS,
        "stats": stats,
        "missing": sorted(m["path"] for m in missing),
        "errors": errors[:200],
        "duration_sec": round(time.time() - started, 1),
    }
    with open(OUTSUM, "w", encoding="utf-8") as fh:
        json.dump(summary, fh, ensure_ascii=False, indent=2)
    print(json.dumps({k: v for k, v in summary.items() if k not in ("missing",)}, ensure_ascii=False, indent=2), flush=True)
    print(f"missing: {len(missing)} -> {OUTSUM}", flush=True)


if __name__ == "__main__":
    sys.exit(main())
