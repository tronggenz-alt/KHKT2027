#!/usr/bin/env python3
"""
Công cụ đo độ sáng ảnh chụp màn hình (Luminance Analyzer) theo chuẩn Rec. 709:
  L = 0.2126*R + 0.7152*G + 0.0722*B
Đo độ sáng trung bình toàn ảnh, tỉ lệ điểm ảnh cháy (> 0.98), tỉ lệ điểm ảnh đen (< 0.03),
và lấy mẫu các vùng trần/tường/mặt bàn theo bảng tiêu chuẩn mục 4 Art Bible.
"""

import sys
import os
from PIL import Image

def analyze_image(path):
    if not os.path.exists(path):
        return None
    im = Image.open(path).convert('RGB')
    w, h = im.size
    pixels = list(im.getdata())
    n = len(pixels)

    lum_list = []
    burnt_count = 0
    black_count = 0

    for r, g, b in pixels:
        rf = r / 255.0
        gf = g / 255.0
        bf = b / 255.0
        lum = 0.2126 * rf + 0.7152 * gf + 0.0722 * bf
        lum_list.append(lum)
        if lum > 0.98:
            burnt_count += 1
        elif lum < 0.03:
            black_count += 1

    avg_lum = sum(lum_list) / n
    burnt_pct = (burnt_count / n) * 100.0
    black_pct = (black_count / n) * 100.0

    # Lấy mẫu vùng trần (20% trên cùng của ảnh)
    ceiling_pixels = []
    for y in range(int(h * 0.05), int(h * 0.25)):
        for x in range(int(w * 0.2), int(w * 0.8)):
            r, g, b = im.getpixel((x, y))
            lum = 0.2126 * (r/255.0) + 0.7152 * (g/255.0) + 0.0722 * (b/255.0)
            ceiling_pixels.append(lum)
    ceiling_avg = sum(ceiling_pixels) / len(ceiling_pixels) if ceiling_pixels else avg_lum

    # Lấy mẫu vùng mặt bàn (vùng giữa và 1/3 dưới ảnh)
    table_pixels = []
    for y in range(int(h * 0.50), int(h * 0.75)):
        for x in range(int(w * 0.3), int(w * 0.7)):
            r, g, b = im.getpixel((x, y))
            lum = 0.2126 * (r/255.0) + 0.7152 * (g/255.0) + 0.0722 * (b/255.0)
            table_pixels.append(lum)
    table_avg = sum(table_pixels) / len(table_pixels) if table_pixels else avg_lum

    return {
        "file": os.path.basename(path),
        "avg": avg_lum,
        "ceiling": ceiling_avg,
        "table": table_avg,
        "burnt_pct": burnt_pct,
        "black_pct": black_pct
    }

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Cách dùng: do_do_sang.py <ảnh_hoặc_thư_mục>")
        sys.exit(1)

    target = sys.argv[1]
    files = []
    if os.path.isdir(target):
        files = [os.path.join(target, f) for f in sorted(os.listdir(target)) if f.lower().endswith(('.png', '.jpg'))]
    else:
        files = [target]

    print(f"{'Tên ảnh':<32} | {'Toàn ảnh':<9} | {'Trần/Tường':<10} | {'Mặt bàn':<8} | {'Cháy (>0.98)':<12} | {'Đen (<0.03)':<11}")
    print("-" * 92)
    for f in files:
        res = analyze_image(f)
        if res:
            print(f"{res['file']:<32} | {res['avg']:>8.3f} | {res['ceiling']:>9.3f}  | {res['table']:>7.3f}  | {res['burnt_pct']:>10.2f}% | {res['black_pct']:>9.2f}%")

