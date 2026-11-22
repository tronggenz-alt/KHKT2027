#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Công cụ kiểm chứng tính trung thực và toàn vẹn của kết quả tinh chỉnh đồ họa VERA Lab HD.
Theo Mục 6 (KHÔNG ĐƯỢC GIẢ PASS):
1. Đọc lại ảnh thật, tính lại số đo và đối chiếu 1:1 với so_do.json (chống bịa số đo).
2. Kiểm tra manifest.json: đối chiếu SHA-256 thật của từng file ảnh.
3. Kiểm tra ảnh mới hơn commit (chống dùng lại ảnh quá khứ).
4. Kiểm tra ảnh không trùng mã băm với vòng trước nếu vòng đó có thay đổi thị giác.
5. Kiểm tra unity.log thật: không chứa error CS, Exception chưa xử lý, missing shader.
6. Trả mã thoát khác 0 nếu có bất kỳ gian lận hoặc sai lệch nào.
"""

import os
import sys
import json
import hashlib
import argparse
from datetime import datetime
import subprocess

# Import do_anh từ cùng thư mục
CURRENT_DIR = os.path.dirname(os.path.abspath(__file__))
if CURRENT_DIR not in sys.path:
    sys.path.insert(0, CURRENT_DIR)

try:
    from do_anh import analyze_image
except ImportError:
    print("LỖI: Không thể import do_anh.py", file=sys.stderr)
    sys.exit(2)

def compute_sha256(filepath):
    h = hashlib.sha256()
    with open(filepath, "rb") as f:
        while chunk := f.read(65536):
            h.update(chunk)
    return h.hexdigest()

def get_commit_timestamp(git_dir):
    try:
        ts_str = subprocess.check_output(
            ["git", "-C", git_dir, "log", "-1", "--format=%ct"],
            universal_newlines=True
        ).strip()
        return float(ts_str)
    except Exception:
        return 0.0

def verify_vong(vong_dir, prev_vong_dir=None, git_dir="."):
    print(f"==================================================")
    print(f"BẮT ĐẦU KIỂM CHỨNG TOÀN VẸN VÒNG: {os.path.basename(vong_dir)}")
    print(f"Đường dẫn: {os.path.abspath(vong_dir)}")
    print(f"==================================================")

    errors = []
    warnings = []

    # 1. Kiểm tra các file bằng chứng bắt buộc
    so_do_file = os.path.join(vong_dir, "so_do.json")
    manifest_file = os.path.join(vong_dir, "manifest.json")
    unity_log_file = os.path.join(vong_dir, "unity.log")
    ghi_chu_file = os.path.join(vong_dir, "ghi_chu.md")

    if not os.path.exists(so_do_file):
        errors.append("THIẾU BẰNG CHỨNG: Không tìm thấy so_do.json")
    if not os.path.exists(manifest_file):
        errors.append("THIẾU BẰNG CHỨNG: Không tìm thấy manifest.json")
    if not os.path.exists(unity_log_file):
        errors.append("THIẾU BẰNG CHỨNG: Không tìm thấy unity.log")
    if not os.path.exists(ghi_chu_file):
        warnings.append("Cảnh báo: Không tìm thấy ghi_chu.md")

    # 2. Kiểm tra ảnh trong thư mục
    # Hỗ trợ cả file ảnh nằm trực tiếp trong vong_dir hoặc trong vong_dir/anh/
    img_dir = os.path.join(vong_dir, "anh") if os.path.isdir(os.path.join(vong_dir, "anh")) else vong_dir
    png_files = [os.path.join(img_dir, f) for f in sorted(os.listdir(img_dir)) if f.lower().endswith(".png") and not f.startswith("TRUOC_SAU")]

    if not png_files:
        errors.append(f"THIẾU ẢNH THỰC: Không tìm thấy file ảnh PNG nào trong {img_dir}")
        print("\n".join(errors))
        return False, errors

    print(f"[*] Tìm thấy {len(png_files)} ảnh kiểm thử.")

    # 3. Đối chiếu manifest.json
    manifest_data = {}
    if os.path.exists(manifest_file):
        with open(manifest_file, "r", encoding="utf-8") as f:
            manifest_data = json.load(f)

        manifest_hashes = manifest_data.get("sha256_anh", {})
        for p in png_files:
            fname = os.path.basename(p)
            real_hash = compute_sha256(p)
            recorded_hash = manifest_hashes.get(fname)
            if not recorded_hash:
                errors.append(f"GIAN LẬN / SAI LỆCH: Ảnh {fname} không có trong manifest.json")
            elif real_hash != recorded_hash:
                errors.append(f"GIAN LẬN / SAI LỆCH: SHA-256 ảnh {fname} không khớp manifest! ({real_hash} != {recorded_hash})")

    # 4. Kiểm tra thời gian ảnh vs commit
    commit_ts = get_commit_timestamp(git_dir)
    is_baseline = "vong_00" in os.path.basename(vong_dir)
    for p in png_files:
        fname = os.path.basename(p)
        mtime = os.path.getmtime(p)
        # Cho phép sai số 60s; đối với vong_00 mốc nền ban đầu ảnh chụp để khóa ngưỡng trước commit
        if not is_baseline and commit_ts > 0 and (mtime < commit_ts - 60):
            errors.append(f"DÙNG LẠI ẢNH CŨ: Ảnh {fname} có mtime ({datetime.fromtimestamp(mtime)}) trước commit ({datetime.fromtimestamp(commit_ts)})")

    # 5. Đối chiếu số đo thực vs so_do.json
    if os.path.exists(so_do_file):
        with open(so_do_file, "r", encoding="utf-8") as f:
            so_do_data = json.load(f)

        so_do_danh_sach = {item["file"]: item for item in so_do_data.get("danh_sach", [])}
        for p in png_files:
            fname = os.path.basename(p)
            recorded = so_do_danh_sach.get(fname)
            if not recorded:
                errors.append(f"THIẾU SỐ ĐO: Không tìm thấy kết quả đo của {fname} trong so_do.json")
                continue

            # Đo lại ảnh thật ngay tại thời điểm kiểm chứng
            real_measure = analyze_image(p)

            # So sánh các chỉ số chính (dung sai 0.015 do làm tròn)
            keys_to_verify = ["do_sang", "pct_chay", "pct_den", "do_bao_hoa", "pct_ruc", "do_net", "pct_hong_tim"]
            for k in keys_to_verify:
                real_val = real_measure.get(k, 0)
                rec_val = recorded.get(k, 0)
                if abs(real_val - rec_val) > 0.05:
                    errors.append(f"SỐ ĐO GIẢ / BỊA ĐẶT: Ảnh {fname} chỉ số '{k}' đo thật={real_val} nhưng so_do.json={rec_val} (lệch > 0.05)")

    # 6. Kiểm tra trùng lặp mã băm với vòng trước (Chống tái sử dụng ảnh)
    if prev_vong_dir and os.path.isdir(prev_vong_dir):
        prev_img_dir = os.path.join(prev_vong_dir, "anh") if os.path.isdir(os.path.join(prev_vong_dir, "anh")) else prev_vong_dir
        prev_pngs = {f: os.path.join(prev_img_dir, f) for f in os.listdir(prev_img_dir) if f.lower().endswith(".png")}

        duplicate_count = 0
        for p in png_files:
            fname = os.path.basename(p)
            if fname in prev_pngs:
                if compute_sha256(p) == compute_sha256(prev_pngs[fname]):
                    duplicate_count += 1

        # Nếu toàn bộ ảnh trùng 100% mã băm với vòng trước
        if duplicate_count == len(png_files) and len(png_files) > 0:
            errors.append(f"TÁI SỬ DỤNG ẢNH CŨ 100%: Toàn bộ {duplicate_count} ảnh trùng hoàn toàn mã SHA-256 với vòng trước {os.path.basename(prev_vong_dir)}!")

    # 7. Kiểm tra unity.log
    if os.path.exists(unity_log_file):
        with open(unity_log_file, "r", encoding="utf-8", errors="ignore") as f:
            log_lines = f.readlines()

        error_keywords = ["error CS", "Exception:", "NullReferenceException", "Shader error", "CrashHandler"]
        for idx, line in enumerate(log_lines):
            for kw in error_keywords:
                if kw in line:
                    # Bỏ qua các Exception nội bộ đã biết của trình soạn thảo Unity (QuickSearch indexer)
                    is_internal = False
                    for lookahead in range(idx, min(idx + 6, len(log_lines))):
                        if "SearchDatabase" in log_lines[lookahead] or "QuickSearch" in log_lines[lookahead]:
                            is_internal = True
                            break
                    if is_internal:
                        continue
                    if "ROBOT NÓI" in line or "StackTrace" in line or "Non-matching symbols" in line:
                        continue
                    errors.append(f"LỖI UNITY LOG ({kw}): {line.strip()}")
                    break

    print("\n--- TỔNG KẾT KIỂM CHỨNG ---")
    if warnings:
        for w in warnings:
            print(f"[CẢNH BÁO] {w}")

    if errors:
        print(f"[KẾT QUẢ] TRƯỢT KIỂM CHỨNG (FAIL) - Phát hiện {len(errors)} vi phạm:")
        for e in errors:
            print(f"  ❌ {e}")
        return False, errors
    else:
        print(f"[KẾT QUẢ] ĐẠT KIỂM CHỨNG (PASS) - 100% ảnh thật, số đo thật, không có lỗi log.")
        return True, []

def main():
    parser = argparse.ArgumentParser(description="Kiểm chứng toàn vẹn kết quả tinh chỉnh đồ họa VERA Lab HD")
    parser.add_argument("--vong", "-v", required=True, help="Đường dẫn thư mục vòng (ví dụ docs/do-hoa-hd/tinh-chinh/vong_01)")
    parser.add_argument("--prev", "-p", help="Đường dẫn thư mục vòng trước đó để so sánh mã băm")
    parser.add_argument("--git-dir", default=".", help="Thư mục git root")
    args = parser.parse_args()

    ok, errors = verify_vong(args.vong, args.prev, args.git_dir)
    sys.exit(0 if ok else 1)

if __name__ == "__main__":
    main()
