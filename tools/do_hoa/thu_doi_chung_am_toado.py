#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Chạy đối chứng âm kiểm tra tọa độ (Mục 4.5):
Dời thử 1 vật trong toa_do_goc.json đi 5 cm (0.05 m),
chạy HdCoordinateTool.CompareBatch để chứng minh công cụ phát hiện LỖI và trả mã thoát != 0,
sau đó khôi phục lại file gốc.
"""

import os
import sys
import json
import shutil
import subprocess

PROJECT_ROOT = "/Users/trantrong/VERALabHD"
JSON_PATH = os.path.join(PROJECT_ROOT, "docs/do-hoa-hd/toa_do_goc.json")
BAK_PATH = os.path.join(PROJECT_ROOT, "docs/do-hoa-hd/toa_do_goc.json.bak")
UNITY_PATH = "/Users/trantrong/Documents/6000.6.7f1/Unity.app/Contents/MacOS/Unity"
LOG_PATH = os.path.join(PROJECT_ROOT, "logs/negative_coord_test.log")

def run():
    print("=== ĐỐI CHỨNG ÂM 2: DỜI THỬ 1 VẬT 5 CM ===")
    shutil.copyfile(JSON_PATH, BAK_PATH)
    try:
        with open(JSON_PATH, "r", encoding="utf-8-sig") as f:
            data = json.load(f)

        # Tìm 1 vật thực trong danh sách (không bị ignore)
        modified = False
        target_path = ""
        for rec in data["records"]:
            if "Bàn thí nghiệm 1" in rec["path"] and "Ống buret" in rec["path"]:
                target_path = rec["path"]
                print(f"Đối tượng thử nghiệm: {target_path}")
                print(f"Vị trí ban đầu: {rec['pos']}")
                rec["pos"][0] += 0.05  # Dời 5 cm theo trục X
                print(f"Vị trí sau khi dời 5 cm: {rec['pos']}")
                modified = True
                break

        if not modified:
            # Fallback lấy vật đầu tiên
            rec = data["records"][50]
            target_path = rec["path"]
            rec["pos"][0] += 0.05

        with open(JSON_PATH, "w", encoding="utf-8") as f:
            json.dump(data, f, ensure_ascii=False)

        print("[*] Đã sửa toa_do_goc.json. Đang chạy HdCoordinateTool.CompareBatch...")
        cmd = [
            UNITY_PATH,
            "-batchmode",
            "-quit",
            "-projectPath", os.path.join(PROJECT_ROOT, "Phong thi nghiem ao"),
            "-executeMethod", "Khkt.EditorTools.HdCoordinateTool.CompareBatch",
            "-logFile", LOG_PATH
        ]
        proc = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        print(f"Mã thoát Unity: {proc.returncode}")

        # Đọc log
        error_lines = []
        if os.path.exists(LOG_PATH):
            with open(LOG_PATH, "r", encoding="utf-8", errors="ignore") as f:
                for line in f:
                    if "[HD-TOA-DO]" in line:
                        error_lines.append(line.strip())

        print("Đầu ra trích xuất từ log:")
        for l in error_lines[-10:]:
            print("  ", l)

        if proc.returncode != 0 and any("LỖI" in l for l in error_lines):
            print("\n>>> KẾT QUẢ: ĐỐI CHỨNG ÂM THÀNH CÔNG (Công cụ phát hiện đúng LỖI SAI LỆCH 5 cm và trả mã thoát != 0)")
            return True
        else:
            print("\n>>> KẾT QUẢ: THẤT BẠI - Công cụ không phát hiện được lỗi!")
            return False

    finally:
        if os.path.exists(BAK_PATH):
            shutil.copyfile(BAK_PATH, JSON_PATH)
            os.remove(BAK_PATH)
            print("[*] Đã khôi phục nguyên vẹn toa_do_goc.json gốc.")

if __name__ == "__main__":
    success = run()
    sys.exit(0 if success else 1)
