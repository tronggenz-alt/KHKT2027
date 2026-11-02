#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Công cụ đo lường chất lượng hình ảnh đồ họa PBR (VERA Lab HD).
Tính toán:
- Độ sáng trung bình (Mean Luminance in [0, 1])
- % điểm cháy (Luminance > 0.98)
- % điểm đen (Luminance < 0.03)
- Độ bão hòa trung bình (Mean Saturation in [0, 1])
- % điểm rực (S > 0.6 và V > 0.5)
- Độ nét (Phương sai Laplacian)
- Mức quầng quanh cạnh (Edge halo overshoot metric)
- % điểm màu hồng tím vật liệu lỗi (Magenta missing shader)
"""

import os
import sys
import json
import argparse
import numpy as np
import cv2

REGIONS = {
    "toan_anh": (0.0, 0.0, 1.0, 1.0),
    "trung_tam": (0.25, 0.25, 0.75, 0.75),
    "nua_tren": (0.0, 0.0, 1.0, 0.5),
    "nua_duoi": (0.0, 0.5, 1.0, 1.0),
    "nua_trai": (0.0, 0.0, 0.5, 1.0),
    "nua_phai": (0.5, 0.0, 1.0, 1.0)
}

def analyze_crop(bgr_crop):
    h, w = bgr_crop.shape[:2]
    total_pixels = h * w
    if total_pixels == 0:
        return {}

    rgb = cv2.cvtColor(bgr_crop, cv2.COLOR_BGR2RGB)
    gray = cv2.cvtColor(bgr_crop, cv2.COLOR_BGR2GRAY)

    # 1. Luminance [0, 1]
    lum = (0.2126 * rgb[:, :, 0] + 0.7152 * rgb[:, :, 1] + 0.0722 * rgb[:, :, 2]) / 255.0
    mean_lum = float(np.mean(lum))
    pct_chay = float(np.mean(lum > 0.98) * 100.0)
    pct_den = float(np.mean(lum < 0.03) * 100.0)

    # 2. HSV: Saturation & Value [0, 1], Hue [0, 360]
    hsv = cv2.cvtColor(bgr_crop, cv2.COLOR_BGR2HSV)
    hue = hsv[:, :, 0].astype(np.float32) * 2.0  # 0..360
    sat = hsv[:, :, 1].astype(np.float32) / 255.0  # 0..1
    val = hsv[:, :, 2].astype(np.float32) / 255.0  # 0..1

    mean_sat = float(np.mean(sat))
    # Điểm rực: S > 0.6 và V > 0.5
    pct_ruc = float(np.mean((sat > 0.6) & (val > 0.5)) * 100.0)

    # 3. Độ nét: Phương sai Laplacian
    laplacian_var = float(cv2.Laplacian(gray, cv2.CV_64F).var())

    # 4. Mức quầng quanh cạnh (Edge halo overshoot metric)
    # Tìm cạnh sắc nét bằng Canny
    edges = cv2.Canny(gray, 60, 160)
    if np.count_nonzero(edges) > 50:
        kernel3 = np.ones((3, 3), np.uint8)
        dilated_edge = cv2.dilate(edges, kernel3, iterations=1)
        halo_zone = cv2.dilate(dilated_edge, kernel3, iterations=2) - dilated_edge
        # Đo cường độ biến thiên / vọt lố trong vùng quầng
        lap = np.abs(cv2.Laplacian(gray.astype(np.float32), cv2.CV_32F))
        halo_level = float(np.mean(lap[halo_zone > 0])) if np.count_nonzero(halo_zone) > 0 else 0.0
    else:
        halo_level = 0.0

    # 5. % điểm màu hồng tím của vật liệu lỗi (Missing shader magenta)
    # Magenta đặc trưng: Hue 280°–340°, Saturation >= 0.60, Value >= 0.40
    is_magenta = (hue >= 280.0) & (hue <= 340.0) & (sat >= 0.60) & (val >= 0.40)
    pct_hong_tim = float(np.mean(is_magenta) * 100.0)

    return {
        "do_sang": round(mean_lum, 4),
        "pct_chay": round(pct_chay, 3),
        "pct_den": round(pct_den, 3),
        "do_bao_hoa": round(mean_sat, 4),
        "pct_ruc": round(pct_ruc, 3),
        "do_net": round(laplacian_var, 2),
        "muc_quang": round(halo_level, 3),
        "pct_hong_tim": round(pct_hong_tim, 4)
    }

def analyze_image(img_path):
    bgr = cv2.imread(img_path)
    if bgr is None:
        raise ValueError(f"Không thể đọc ảnh: {img_path}")
    h, w = bgr.shape[:2]

    res = {
        "file": os.path.basename(img_path),
        "duong_dan": os.path.abspath(img_path),
        "kich_thuoc": [w, h],
        "vung": {}
    }

    for region_name, (x0_pct, y0_pct, x1_pct, y1_pct) in REGIONS.items():
        x0, y0 = int(x0_pct * w), int(y0_pct * h)
        x1, y1 = int(x1_pct * w), int(y1_pct * h)
        crop = bgr[y0:y1, x0:x1]
        res["vung"][region_name] = analyze_crop(crop)

    # Số đo toàn ảnh ở cấp gốc để truy cập nhanh
    for k, v in res["vung"]["toan_anh"].items():
        res[k] = v

    return res

def check_thresholds(analysis_results, thresholds):
    """
    Kiểm tra danh sách kết quả đo với ngưỡng.
    Trả về (is_pass, list_of_violations).
    """
    violations = []

    # Ngưỡng mặc định nếu không truyền
    max_hong_tim = thresholds.get("max_pct_hong_tim", 0.0)
    max_chay = thresholds.get("max_pct_chay", 3.0)
    max_den = thresholds.get("max_pct_den", 2.0)
    min_do_sang = thresholds.get("min_do_sang", 0.05)
    max_do_sang = thresholds.get("max_do_sang", 0.95)
    max_ruc = thresholds.get("max_pct_ruc", 15.0)
    max_bao_hoa = thresholds.get("max_do_bao_hoa", 0.45)
    min_do_net = thresholds.get("min_do_net", 30.0)
    max_muc_quang = thresholds.get("max_muc_quang", 25.0)

    for item in analysis_results:
        fname = item["file"]
        t = item["vung"]["toan_anh"] if "toan_anh" in item.get("vung", {}) else item

        if t.get("pct_hong_tim", 0) > max_hong_tim:
            violations.append(f"[{fname}] LỖI HỒNG TÍM (Missing Shader): {t['pct_hong_tim']}% > {max_hong_tim}%")
        if t.get("pct_chay", 0) > max_chay:
            violations.append(f"[{fname}] LỖI CHÁY SÁNG: {t['pct_chay']}% > {max_chay}%")
        if t.get("pct_den", 0) > max_den:
            violations.append(f"[{fname}] LỖI TỐI ĐEN: {t['pct_den']}% > {max_den}%")
        if t.get("do_sang", 0) < min_do_sang:
            violations.append(f"[{fname}] LỖI QUÁ TỐI: độ sáng {t['do_sang']} < {min_do_sang}")
        if t.get("do_sang", 0) > max_do_sang:
            violations.append(f"[{fname}] LỖI QUÁ CHÓI: độ sáng {t['do_sang']} > {max_do_sang}")
        if t.get("pct_ruc", 0) > max_ruc:
            violations.append(f"[{fname}] LỖI QUÁ RỰC MÀU: {t['pct_ruc']}% > {max_ruc}%")
        if t.get("do_bao_hoa", 0) > max_bao_hoa:
            violations.append(f"[{fname}] LỖI BÃO HÒA CAO: {t['do_bao_hoa']} > {max_bao_hoa}")
        if t.get("do_net", 0) < min_do_net:
            violations.append(f"[{fname}] LỖI QUÁ MỜ (Độ nét thấp): {t['do_net']} < {min_do_net}")
        if t.get("muc_quang", 0) > max_muc_quang:
            violations.append(f"[{fname}] LỖI QUẦNG CẠNH (Edge Haloing): {t['muc_quang']} > {max_muc_quang}")

    is_pass = len(violations) == 0
    return is_pass, violations

def main():
    parser = argparse.ArgumentParser(description="Đo thông số đồ họa hình ảnh VERA Lab HD")
    parser.add_argument("--input", "-i", required=True, help="Đường dẫn file ảnh hoặc thư mục chứa ảnh PNG")
    parser.add_argument("--out", "-o", help="Đường dẫn xuất file JSON kết quả")
    parser.add_argument("--thresholds", "-t", help="File JSON chứa các ngưỡng kiểm tra")
    parser.add_argument("--check-only", action="store_true", help="Chỉ kiểm tra ngưỡng và trả exit code")
    args = parser.parse_args()

    files = []
    if os.path.isdir(args.input):
        for f in sorted(os.listdir(args.input)):
            if f.lower().endswith(".png") or f.lower().endswith(".jpg"):
                files.append(os.path.join(args.input, f))
    elif os.path.isfile(args.input):
        files.append(args.input)
    else:
        print(f"Lỗi: Không tìm thấy đường dẫn đầu vào: {args.input}", file=sys.stderr)
        sys.exit(2)

    if not files:
        print(f"Lỗi: Không tìm thấy file ảnh PNG nào trong {args.input}", file=sys.stderr)
        sys.exit(2)

    results = []
    for f in files:
        res = analyze_image(f)
        results.append(res)

    output_data = {
        "so_luong_anh": len(results),
        "danh_sach": results
    }

    # Tính trung bình toàn bộ ảnh
    keys_to_avg = ["do_sang", "pct_chay", "pct_den", "do_bao_hoa", "pct_ruc", "do_net", "muc_quang", "pct_hong_tim"]
    avg_stats = {}
    for k in keys_to_avg:
        vals = [r[k] for r in results]
        avg_stats[k] = round(float(np.mean(vals)), 4)
    output_data["trung_binh"] = avg_stats

    thresholds = {}
    if args.thresholds and os.path.exists(args.thresholds):
        with open(args.thresholds, "r", encoding="utf-8") as tf:
            thresholds = json.load(tf)
        output_data["nguong_kiem_tra"] = thresholds

    is_pass, violations = check_thresholds(results, thresholds)
    output_data["ket_qua"] = "DAT" if is_pass else "TRUOT"
    output_data["vi_pham"] = violations

    if args.out:
        os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
        with open(args.out, "w", encoding="utf-8") as out_f:
            json.dump(output_data, out_f, ensure_ascii=False, indent=2)
        print(f"Đã lưu kết quả đo vào: {args.out}")

    print(f"=== KẾT QUẢ ĐO ({len(results)} ảnh) ===")
    print(f"Độ sáng TB: {avg_stats['do_sang']} | Cháy: {avg_stats['pct_chay']}% | Đen: {avg_stats['pct_den']}%")
    print(f"Bão hòa TB: {avg_stats['do_bao_hoa']} | Rực: {avg_stats['pct_ruc']}% | Hồng tím: {avg_stats['pct_hong_tim']}%")
    print(f"Độ nét TB: {avg_stats['do_net']} | Quầng cạnh TB: {avg_stats['muc_quang']}")
    print(f"TRẠNG THÁI: {'ĐẠT (PASS)' if is_pass else 'TRƯỢT (FAIL)'}")

    if not is_pass:
        print(f"Số lỗi vi phạm ngưỡng ({len(violations)}):")
        for v in violations[:10]:
            print(f"  - {v}")
        if len(violations) > 10:
            print(f"  ... và {len(violations) - 10} lỗi khác")
        sys.exit(1)
    else:
        sys.exit(0)

if __name__ == "__main__":
    main()
