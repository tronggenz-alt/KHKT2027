#!/usr/bin/env python3
"""
tools/do_hoa/ghep_anh.py
Ghép ảnh so sánh song song giữa bản tốt nhất (hd-tot-nhat) và bản mới (vong_NN).
Hỗ trợ thu nhỏ kích thước để tiết kiệm dung lượng đĩa cho các vòng trung gian.
"""

import sys
import os
import argparse
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

def ghep_hai_anh(path_a, path_b, out_path, nhan_a="BẢN CŨ / TỐT NHẤT", nhan_b="BẢN MỚI", scale=0.5):
    if not os.path.exists(path_a) or not os.path.exists(path_b):
        return False
    
    img_a = Image.open(path_a).convert("RGB")
    img_b = Image.open(path_b).convert("RGB")
    
    if scale != 1.0:
        new_w = int(img_a.width * scale)
        new_h = int(img_a.height * scale)
        img_a = img_a.resize((new_w, new_h), Image.Resampling.BILINEAR)
        img_b = img_b.resize((new_w, new_h), Image.Resampling.BILINEAR)
    
    w, h = img_a.size
    banner_h = 32
    canvas = Image.new("RGB", (w * 2 + 4, h + banner_h), (25, 25, 28))
    
    # Paste images
    canvas.paste(img_a, (0, banner_h))
    canvas.paste(img_b, (w + 4, banner_h))
    
    # Draw banners
    draw = ImageDraw.Draw(canvas)
    draw.text((10, 8), f"{nhan_a} (Trái)", fill=(200, 200, 200))
    draw.text((w + 14, 8), f"{nhan_b} (Phải)", fill=(100, 220, 255))
    
    # Save optimized JPEG to save space
    canvas.save(out_path, format="JPEG", quality=85)
    return True

def main():
    parser = argparse.ArgumentParser(description="Ghép ảnh so sánh 2 vòng.")
    parser.add_argument("--truoc", "-a", required=True, help="Thư mục bản trước/tốt nhất")
    parser.add_argument("--sau", "-b", required=True, help="Thư mục bản mới")
    parser.add_argument("--out", "-o", required=True, help="Thư mục xuất ảnh ghép")
    parser.add_argument("--nhan-a", default="BẢN TỐT NHẤT", help="Nhãn ảnh A")
    parser.add_argument("--nhan-b", default="BẢN MỚI", help="Nhãn ảnh B")
    parser.add_argument("--scale", type=float, default=0.5, help="Tỉ lệ co ảnh (mặc định 0.5)")
    args = parser.parse_args()
    
    dir_a = Path(args.truoc)
    dir_b = Path(args.sau)
    dir_out = Path(args.out)
    dir_out.mkdir(parents=True, exist_ok=True)
    
    files_a = set(f.name for f in dir_a.glob("*.png"))
    files_b = set(f.name for f in dir_b.glob("*.png"))
    common = sorted(files_a.intersection(files_b))
    
    print(f"[*] Ghép {len(common)} ảnh so sánh từ {dir_a.name} và {dir_b.name}...")
    for fname in common:
        out_name = Path(fname).stem + "_so_sanh.jpg"
        ghep_hai_anh(
            dir_a / fname,
            dir_b / fname,
            dir_out / out_name,
            nhan_a=args.nhan_a,
            nhan_b=args.nhan_b,
            scale=args.scale
        )
    print(f"[OK] Đã xuất {len(common)} ảnh so sánh vào: {dir_out}")

if __name__ == "__main__":
    main()
