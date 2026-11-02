#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Tạo manifest.json cho một thư mục vòng tinh chỉnh (Mục 6):
Ghi nhận commit hash, thời gian, và SHA-256 của từng ảnh PNG.
"""

import os
import sys
import json
import hashlib
import subprocess
from datetime import datetime

def make_manifest(vong_dir, git_dir="."):
    png_files = sorted([f for f in os.listdir(vong_dir) if f.lower().endswith(".png") and not f.startswith("TRUOC_SAU")])
    if not png_files:
        print(f"Lỗi: Không có ảnh PNG trong {vong_dir}", file=sys.stderr)
        return False

    try:
        commit_hash = subprocess.check_output(
            ["git", "-C", git_dir, "rev-parse", "HEAD"],
            universal_newlines=True
        ).strip()
    except Exception:
        commit_hash = "chua_commit"

    hashes = {}
    for fname in png_files:
        fpath = os.path.join(vong_dir, fname)
        h = hashlib.sha256()
        with open(fpath, "rb") as f:
            while chunk := f.read(65536):
                h.update(chunk)
        hashes[fname] = h.hexdigest()

    manifest_data = {
        "vong": os.path.basename(os.path.abspath(vong_dir)),
        "commit": commit_hash,
        "thoi_gian": datetime.now().isoformat(),
        "tong_so_anh": len(png_files),
        "sha256_anh": hashes
    }

    out_path = os.path.join(vong_dir, "manifest.json")
    with open(out_path, "w", encoding="utf-8") as f:
        json.dump(manifest_data, f, ensure_ascii=False, indent=2)

    print(f"Đã tạo manifest.json ({len(png_files)} ảnh) tại: {out_path}")
    return True

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Sử dụng: tao_manifest.py <duong_dan_vong>")
        sys.exit(1)
    make_manifest(sys.argv[1])
