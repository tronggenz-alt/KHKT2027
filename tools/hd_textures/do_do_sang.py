#!/usr/bin/env python3
"""
Công cụ đo độ sáng ảnh chụp VERA Lab HD theo tiêu chuẩn Mục 4:
  - Độ sáng trung bình (luminance Y = 0.2126 R + 0.7152 G + 0.0722 B).
  - Tỷ lệ điểm ảnh cháy (> 0.98) - ngưỡng đạt < 3.0%.
  - Tỷ lệ điểm ảnh đen (< 0.03) - ngưỡng đạt < 2.0%.
  - Đo lường phân vùng (Trần/Tường: 0.55 - 0.75; Mặt bàn: 0.60 - 0.80).
"""

import sys
import os
import numpy as np
from PIL import Image

def analyze_brightness(image_path):
    if not os.path.exists(image_path):
        return None

    img = Image.open(image_path).convert('RGB')
    arr = np.array(img, dtype=np.float32) / 255.0

    # Luminance tiêu chuẩn sRGB
    lum = 0.2126 * arr[:, :, 0] + 0.7152 * arr[:, :, 1] + 0.0722 * arr[:, :, 2]

    h, w = lum.shape
    total_pixels = h * w

    avg_lum = float(np.mean(lum))
    burnt_pct = float(np.sum(lum > 0.98) / total_pixels * 100.0)
    black_pct = float(np.sum(lum < 0.03) / total_pixels * 100.0)

    # Phân vùng ước tính:
    # Trần / Tường trên: 35% chiều cao phía trên
    ceiling_lum = float(np.mean(lum[:int(h * 0.35), :]))
    # Bàn thí nghiệm / Mặt thao tác: 35% - 75% chiều cao giữa
    table_lum = float(np.mean(lum[int(h * 0.35):int(h * 0.75), :]))

    return {
        "path": image_path,
        "avg_lum": avg_lum,
        "ceiling_lum": ceiling_lum,
        "table_lum": table_lum,
        "burnt_pct": burnt_pct,
        "black_pct": black_pct,
        "pass_burnt": burnt_pct < 3.0,
        "pass_black": black_pct < 2.0,
    }

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: do_do_sang.py <image_path_or_dir>")
        sys.exit(1)

    target = sys.argv[1]
    if os.path.isdir(target):
        files = sorted([os.path.join(target, f) for f in os.listdir(target) if f.lower().endswith(('.png', '.jpg'))])
        print(f"{'Ảnh':<35} | {'TB':<6} | {'Trần/Tường':<10} | {'Mặt bàn':<8} | {'Cháy %':<8} | {'Đen %':<8} | Đánh giá")
        print("-" * 95)
        for f in files:
            res = analyze_brightness(f)
            if res:
                fname = os.path.basename(f)
                status = "ĐẠT" if (res["pass_burnt"] and res["pass_black"]) else "CHƯA ĐẠT"
                print(f"{fname:<35} | {res['avg_lum']:<6.3f} | {res['ceiling_lum']:<10.3f} | {res['table_lum']:<8.3f} | {res['burnt_pct']:<7.2f}% | {res['black_pct']:<7.2f}% | {status}")
    else:
        res = analyze_brightness(target)
        if res:
            print(f"Ảnh: {res['path']}")
            print(f"  • Độ sáng trung bình toàn ảnh: {res['avg_lum']:.3f}")
            print(f"  • Độ sáng vùng trần/tường: {res['ceiling_lum']:.3f} (Mục tiêu: 0.55 - 0.75)")
            print(f"  • Độ sáng vùng bàn thí nghiệm: {res['table_lum']:.3f} (Mục tiêu: 0.60 - 0.80)")
            print(f"  • Tỉ lệ cháy sáng (> 0.98): {res['burnt_pct']:.2f}% (Mục tiêu: < 3%)")
            print(f"  • Tỉ lệ tối đen (< 0.03): {res['black_pct']:.2f}% (Mục tiêu: < 2%)")
