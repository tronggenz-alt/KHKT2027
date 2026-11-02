#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Chạy đối chứng âm kiểm chứng toàn vẹn (Mục 4.5):
Tạo 2 thư mục vòng giả lập vong_fake_A và vong_fake_B, trong đó vong_fake_B dùng lại ảnh cũ của vong_fake_A (trùng mã băm SHA-256).
Chạy kiem_chung.py để chứng minh công cụ phát hiện LỖI TÁI SỬ DỤNG ẢNH CŨ và trả mã thoát != 0.
"""

import os
import sys
import shutil
import json
import hashlib
import subprocess

PROJECT_ROOT = "/Users/trantrong/VERALabHD"
TEST_BASE = os.path.join(PROJECT_ROOT, "docs/do-hoa-hd/tinh-chinh/doi_chung_am_vong")
VONG_A = os.path.join(TEST_BASE, "vong_fake_A")
VONG_B = os.path.join(TEST_BASE, "vong_fake_B")

def run():
    print("=== ĐỐI CHỨNG ÂM 3: TÁI SỬ DỤNG ẢNH CŨ (TRÙNG SHA-256) ===")
    if os.path.exists(TEST_BASE):
        shutil.rmtree(TEST_BASE)
    os.makedirs(VONG_A, exist_ok=True)
    os.makedirs(VONG_B, exist_ok=True)

    try:
        # Lấy 2 ảnh từ anh_goc làm mẫu
        sample_src = os.path.join(PROJECT_ROOT, "docs/do-hoa-hd/anh_goc/landing")
        samples = [f for f in os.listdir(sample_src) if f.endswith(".png")][:2]

        hashes = {}
        for s in samples:
            src_f = os.path.join(sample_src, s)
            dst_a = os.path.join(VONG_A, s)
            dst_b = os.path.join(VONG_B, s)
            shutil.copyfile(src_f, dst_a)
            shutil.copyfile(src_f, dst_b)
            with open(src_f, "rb") as f:
                hashes[s] = hashlib.sha256(f.read()).hexdigest()

        # Tạo manifest và so_do giả lập
        manifest = {
            "vong": "vong_fake_B",
            "commit": "mock_commit",
            "sha256_anh": hashes
        }
        with open(os.path.join(VONG_B, "manifest.json"), "w", encoding="utf-8") as f:
            json.dump(manifest, f)

        # Chạy do_anh để sinh so_do thật cho VONG_B
        subprocess.run([
            "/Users/trantrong/hand3d-env/bin/python3",
            os.path.join(PROJECT_ROOT, "tools/do_hoa/do_anh.py"),
            "-i", VONG_B,
            "-o", os.path.join(VONG_B, "so_do.json")
        ], check=True, stdout=subprocess.DEVNULL)

        # Tạo file unity.log không lỗi
        with open(os.path.join(VONG_B, "unity.log"), "w", encoding="utf-8") as f:
            f.write("KhktPlayVerify completed without errors.\nExiting batchmode successfully.\n")

        # Chạy kiem_chung.py so sánh VONG_B với VONG_A (vòng trước)
        print("[*] Đang chạy kiem_chung.py đối chứng...")
        proc = subprocess.run([
            "/Users/trantrong/hand3d-env/bin/python3",
            os.path.join(PROJECT_ROOT, "tools/do_hoa/kiem_chung.py"),
            "--vong", VONG_B,
            "--prev", VONG_A,
            "--git-dir", PROJECT_ROOT
        ], stdout=subprocess.PIPE, stderr=subprocess.PIPE, universal_newlines=True)

        print(proc.stdout)
        if proc.stderr:
            print("STDERR:", proc.stderr)
        print(f"Mã thoát kiem_chung.py: {proc.returncode}")

        if proc.returncode != 0 and "TÁI SỬ DỤNG ẢNH CŨ" in proc.stdout:
            print("\n>>> KẾT QUẢ: ĐỐI CHỨNG ÂM THÀNH CÔNG (Công cụ phát hiện đúng LỖI DÙNG LẠI ẢNH CŨ và trả mã thoát != 0)")
            return True
        else:
            print("\n>>> KẾT QUẢ: THẤT BẠI - Công cụ không phát hiện được lỗi dùng lại ảnh!")
            return False

    finally:
        if os.path.exists(TEST_BASE):
            shutil.rmtree(TEST_BASE)
            print("[*] Đã dọn dẹp thư mục thử nghiệm tạm.")

if __name__ == "__main__":
    success = run()
    sys.exit(0 if success else 1)
