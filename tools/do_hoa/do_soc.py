#!/usr/bin/env python3
"""
Công cụ đo sọc và hoa văn lặp định kỳ (FFT Spectral Peak-to-Median Ratio)
cho dự án VERA Lab HD theo tiêu chuẩn docs/PROMPT_NANG_CAP_BE_MAT.md.

Phương pháp:
  1. Chuyển ảnh (hoặc vùng crop) sang ảnh thang độ xám (Grayscale).
  2. Áp cửa sổ 2D Hanning để triệt tiêu hiện tượng rò rỉ phổ do biên ảnh (spectral leakage).
  3. Biến đổi Fourier 2 chiều (2D FFT) và dịch tâm phổ tần số về giữa (fftshift).
  4. Khử thành phần DC và tần số cực thấp gần tâm (bán kính r = 5 pixel).
  5. Tính tỉ số giữa đỉnh phổ biên độ cao nhất (peak) và trung vị phổ (median):
     Ratio = Peak / Median
  6. Đối chứng mốc chuẩn:
     • PlasterWall001_Normal.png : ~371.700  -> CÓ SỌC (FAIL)
     • OakWood051_Normal.png     : ~17.076   -> CÓ SỌC (FAIL)
     • Normal_Plaster.png        : ~11.52    -> SẠCH (PASS)
     -> Ngưỡng chốt chẩn đoán: THRESHOLD = 30.0
        Ratio < 30.0  : SẠCH (Không có sọc hay hoa văn tuần hoàn)
        Ratio >= 30.0 : CÓ SỌC (Hoa văn sin, ô lưới hoặc sọc lặp bất thường)
"""

import os
import sys
import argparse
import json
import glob
import numpy as np
from PIL import Image

DEFAULT_THRESHOLD = 30.0
DC_RADIUS = 5

def tinh_ti_so_fft(img_input, crop_box=None, r_dc=DC_RADIUS):
    """
    Tính tỉ số đỉnh phổ / trung vị phổ (Peak-to-Median Ratio) bằng 2D FFT.
    img_input: đường dẫn file ảnh hoặc đối tượng PIL Image.
    crop_box: tuple (x, y, w, h) hoặc (left, upper, right, lower) nếu cần cắt vùng.
    """
    if isinstance(img_input, str):
        im = Image.open(img_input).convert("L")
    else:
        im = img_input.convert("L")

    if crop_box is not None:
        if len(crop_box) == 4:
            x, y, w, h = crop_box
            im = im.crop((x, y, x + w, y + h))

    arr = np.array(im, dtype=float)
    # Ảnh phẳng tuyệt đối (không có biến thiên)
    if np.std(arr) < 1e-4:
        return 0.0, 0.0, 0.0

    h, w = arr.shape
    # Cửa sổ Hanning 2D chống rò rỉ biên
    win = np.outer(np.hanning(h), np.hanning(w))
    arr_centered = (arr - np.mean(arr)) * win
    f_shift = np.fft.fftshift(np.fft.fft2(arr_centered))
    mag = np.abs(f_shift)

    cy, cx = h // 2, w // 2
    y_grid, x_grid = np.ogrid[:h, :w]
    dc_mask = (x_grid - cx) ** 2 + (y_grid - cy) ** 2 <= r_dc ** 2
    mag[dc_mask] = 0.0

    non_zero = mag[mag > 0]
    if len(non_zero) == 0:
        return 0.0, 0.0, 0.0

    peak = float(np.max(non_zero))
    median = float(np.median(non_zero))
    ratio = float(peak / (median + 1e-8))
    return ratio, peak, median

def kiem_tra_doi_chung(base_dir=None):
    """
    Xác nhận đối chứng theo Mục 4:
    - PlasterWall001_Normal.png: CÓ SỌC
    - OakWood051_Normal.png: CÓ SỌC
    - Normal_Plaster.png: SẠCH
    """
    if base_dir is None:
        base_dir = os.path.abspath(os.path.join(os.path.dirname(__file__), "../../Phong thi nghiem ao/Assets/Lab/HD/Textures"))

    targets = [
        ("PlasterWall001_Normal.png", True),
        ("OakWood051_Normal.png", True),
        ("Normal_Plaster.png", False)
    ]

    print("=" * 70)
    print("KIỂM TRA ĐỐI CHỨNG THƯỚC ĐO FFT (NGƯỠNG: 30.0)")
    print("=" * 70)
    all_pass = True
    results = {}

    for name, expect_striped in targets:
        path = os.path.join(base_dir, name)
        if not os.path.exists(path):
            print(f"❌ Không tìm thấy file đối chứng: {path}")
            all_pass = False
            continue

        ratio, peak, med = tinh_ti_so_fft(path)
        is_striped = ratio >= DEFAULT_THRESHOLD
        status = "CÓ SỌC" if is_striped else "SẠCH"
        correct = (is_striped == expect_striped)
        icon = "✅" if correct else "❌"
        print(f"{icon} {name:28s} : Ratio = {ratio:12.2f} (Peak={peak:.1f}, Med={med:.2f}) -> {status}")
        results[name] = {"ratio": ratio, "peak": peak, "median": med, "status": status, "correct": correct}
        if not correct:
            all_pass = False

    print("-" * 70)
    if all_pass:
        print("✅ KẾT QUẢ ĐỐI CHỨNG: TẤT CẢ ĐẠT YÊU CẦU MỤC 4. Ngưỡng 30.0 chuẩn xác.")
    else:
        print("❌ KẾT QUẢ ĐỐI CHỨNG KHÔNG ĐẠT!")
    print("=" * 70)
    return all_pass, results

def main():
    parser = argparse.ArgumentParser(description="Đo sọc và hoa văn lặp bằng FFT (VERA Lab HD)")
    parser.add_argument("--file", help="Đường dẫn đến file ảnh cần phân tích")
    parser.add_argument("--dir", help="Thư mục chứa các ảnh cần phân tích")
    parser.add_argument("--crop", nargs=4, type=int, metavar=("X", "Y", "W", "H"), help="Cắt vùng ảnh (x y w h)")
    parser.add_argument("--threshold", type=float, default=DEFAULT_THRESHOLD, help=f"Ngưỡng sọc (mặc định: {DEFAULT_THRESHOLD})")
    parser.add_argument("--test-baseline", action="store_true", help="Chạy bài kiểm tra đối chứng mốc gốc")
    parser.add_argument("--json", help="Đường dẫn lưu kết quả phân tích dưới dạng JSON")
    args = parser.parse_args()

    if args.test_baseline:
        success, _ = kiem_tra_doi_chung()
        sys.exit(0 if success else 1)

    if not args.file and not args.dir:
        # Mặc định chạy kiểm tra đối chứng nếu không có đối số
        success, _ = kiem_tra_doi_chung()
        sys.exit(0 if success else 1)

    results = []

    if args.file:
        files = [args.file]
    else:
        files = sorted(glob.glob(os.path.join(args.dir, "*.png")) + glob.glob(os.path.join(args.dir, "*.jpg")))

    print(f"Phân tích {len(files)} ảnh với ngưỡng = {args.threshold:.1f}:")
    for f in files:
        fname = os.path.basename(f)
        try:
            ratio, peak, med = tinh_ti_so_fft(f, crop_box=args.crop)
            is_striped = ratio >= args.threshold
            status = "CÓ SỌC" if is_striped else "SẠCH"
            tag = "❌ SỌC" if is_striped else "✅ SẠCH"
            print(f"  {tag:8s} | {fname:36s} : Ratio = {ratio:10.2f} (Peak={peak:8.1f}, Med={med:6.2f})")
            results.append({
                "file": f,
                "name": fname,
                "ratio": ratio,
                "peak": peak,
                "median": med,
                "has_stripes": is_striped,
                "status": status
            })
        except Exception as e:
            print(f"  ⚠️ LỖI   | {fname:36s} : {e}")
            results.append({"file": f, "name": fname, "error": str(e)})

    if args.json:
        os.makedirs(os.path.dirname(os.path.abspath(args.json)), exist_ok=True)
        with open(args.json, "w", encoding="utf-8") as fp:
            json.dump({"threshold": args.threshold, "results": results}, fp, indent=2, ensure_ascii=False)
        print(f"Đã lưu kết quả JSON vào: {args.json}")

if __name__ == "__main__":
    main()
