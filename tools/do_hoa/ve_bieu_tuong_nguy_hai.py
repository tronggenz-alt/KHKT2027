#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
ve_bieu_tuong_nguy_hai.py - Vẽ 4 biểu tượng cảnh báo GHS (GHS0..3) và Biểu tượng Biohazard
Độ phân giải cao 512 x 512, chuẩn hình học quốc tế, nền trong suốt RGBA ngoài hình thoi.
"""

import os
import math
from PIL import Image, ImageDraw
import ve_tranh_hd as vth

def draw_ghs_diamond(draw, size=512):
    cx = size // 2
    cy = size // 2
    # Bán kính hình thoi (Manhattan distance |x-cx| + |y-cy|)
    r_outer = size * 0.47
    r_inner = size * 0.40

    diamond_outer = [(cx, cy - r_outer), (cx + r_outer, cy), (cx, cy + r_outer), (cx - r_outer, cy)]
    diamond_inner = [(cx, cy - r_inner), (cx + r_inner, cy), (cx, cy + r_inner), (cx - r_inner, cy)]

    # Vẽ viền đỏ GHS
    draw.polygon(diamond_outer, fill=(204, 0, 0, 255))
    # Vẽ nền trắng bên trong
    draw.polygon(diamond_inner, fill=(255, 255, 255, 255))
    return cx, cy

def draw_flame(draw, cx, cy):
    # GHS0: Ngọn lửa
    black = (17, 24, 39, 255)
    # Vạch ngang làm giá đỡ ngọn lửa ở dưới
    bar_w = 170
    bar_h = 16
    draw.rectangle([cx - bar_w // 2, cy + 95, cx + bar_w // 2, cy + 95 + bar_h], fill=black)

    # Thân ngọn lửa: đa giác mô tả ngọn lửa bốc lên
    flame_pts = [
        (cx - 75, cy + 90),
        (cx - 70, cy + 50),
        (cx - 85, cy + 20),
        (cx - 65, cy - 10),
        (cx - 75, cy - 40),
        (cx - 45, cy - 70),
        (cx - 50, cy - 110),
        (cx - 15, cy - 65),
        (cx, cy - 130),       # Đỉnh ngọn lửa cao nhất
        (cx + 25, cy - 75),
        (cx + 55, cy - 90),
        (cx + 45, cy - 40),
        (cx + 75, cy - 10),
        (cx + 55, cy + 30),
        (cx + 70, cy + 65),
        (cx + 65, cy + 90),
    ]
    draw.polygon(flame_pts, fill=black)

    # Lõi rỗng giữa ngọn lửa (khoét màu trắng)
    white = (255, 255, 255, 255)
    core_pts = [
        (cx - 20, cy + 90),
        (cx - 28, cy + 65),
        (cx - 15, cy + 40),
        (cx - 25, cy + 15),
        (cx, cy - 25),        # Đỉnh lõi
        (cx + 22, cy + 20),
        (cx + 12, cy + 50),
        (cx + 22, cy + 75),
        (cx + 18, cy + 90)
    ]
    draw.polygon(core_pts, fill=white)

def draw_corrosive(draw, cx, cy):
    # GHS1: Ăn mòn
    black = (17, 24, 39, 255)

    # 1. Ống nghiệm trái rót xuống thanh kim loại
    t1_x = cx - 90
    t1_y = cy - 60
    draw.polygon([(t1_x, t1_y - 45), (t1_x + 50, t1_y - 20), (t1_x + 25, t1_y + 30), (t1_x - 25, t1_y + 5)], fill=black)
    # Giọt chất lỏng rơi
    draw.ellipse([t1_x + 35, t1_y + 45, t1_x + 50, t1_y + 70], fill=black)

    # Thanh kim loại bị khoét ăn mòn ở dưới
    metal_y = cy + 45
    draw.rectangle([cx - 140, metal_y, cx - 20, metal_y + 35], fill=black)
    # Vết khoét lõm bốc khói
    draw.ellipse([cx - 75, metal_y - 10, cx - 40, metal_y + 15], fill=(255, 255, 255, 255))

    # 2. Ống nghiệm phải rót xuống bàn tay
    t2_x = cx + 80
    t2_y = cy - 60
    draw.polygon([(t2_x, t2_y - 45), (t2_x - 50, t2_y - 20), (t2_x - 25, t2_y + 30), (t2_x + 25, t2_y + 5)], fill=black)
    # Giọt chất lỏng rơi
    draw.ellipse([t2_x - 50, t2_y + 45, t2_x - 35, t2_y + 70], fill=black)

    # Bàn tay người đưa ra bị ăn mòn
    hand_pts = [
        (cx + 150, metal_y + 40),
        (cx + 35, metal_y + 40),
        (cx + 20, metal_y + 20),
        (cx + 40, metal_y + 10),
        (cx + 60, metal_y + 25),
        (cx + 90, metal_y + 10),
        (cx + 150, metal_y + 20)
    ]
    draw.polygon(hand_pts, fill=black)
    # Vết ăn mòn trên tay
    draw.ellipse([cx + 50, metal_y + 10, cx + 75, metal_y + 30], fill=(255, 255, 255, 255))

def draw_skull(draw, cx, cy):
    # GHS2: Độc cấp tính (Đầu lâu xương chéo)
    black = (17, 24, 39, 255)
    white = (255, 255, 255, 255)

    # Hai thanh xương chéo phía sau
    bw = 28
    diag_len = 160
    draw.line([(cx - diag_len, cy - diag_len * 0.7), (cx + diag_len, cy + diag_len * 0.7)], fill=black, width=bw)
    draw.line([(cx - diag_len, cy + diag_len * 0.7), (cx + diag_len, cy - diag_len * 0.7)], fill=black, width=bw)
    # Đầu tròn của xương chéo
    for ex, ey in [(-diag_len, -diag_len*0.7), (diag_len, diag_len*0.7), (-diag_len, diag_len*0.7), (diag_len, -diag_len*0.7)]:
        draw.ellipse([cx + ex - 18, cy + ey - 18, cx + ex + 18, cy + ey + 18], fill=black)

    # Vòm sọ đầu lâu
    draw.ellipse([cx - 85, cy - 110, cx + 85, cy + 25], fill=black)
    # Phần hàm dưới
    draw.rectangle([cx - 45, cy + 10, cx + 45, cy + 70], fill=black)

    # Hốc mắt (màu trắng)
    draw.ellipse([cx - 55, cy - 45, cx - 12, cy + 5], fill=white)
    draw.ellipse([cx + 12, cy - 45, cx + 55, cy + 5], fill=white)

    # Hốc mũi (tam giác trắng lộn ngược)
    draw.polygon([(cx, cy + 15), (cx - 12, cy + 35), (cx + 12, cy + 35)], fill=white)

    # Các kẽ răng
    for rx in [-28, -14, 0, 14, 28]:
        draw.line([(cx + rx, cy + 45), (cx + rx, cy + 65)], fill=white, width=4)

def draw_exclamation(draw, cx, cy):
    # GHS3: Dấu chấm than cảnh báo
    black = (17, 24, 39, 255)

    # Thanh dọc thuôn nhọn
    bar_pts = [
        (cx - 24, cy - 115),
        (cx + 24, cy - 115),
        (cx + 15, cy + 25),
        (cx - 15, cy + 25)
    ]
    draw.polygon(bar_pts, fill=black)
    # Bo tròn đỉnh trên
    draw.ellipse([cx - 24, cy - 130, cx + 24, cy - 100], fill=black)

    # Dấu chấm tròn ở dưới
    draw.ellipse([cx - 24, cy + 65, cx + 24, cy + 113], fill=black)

def generate_ghs_symbols():
    symbols = [
        ("GHS0.png", draw_flame),
        ("GHS1.png", draw_corrosive),
        ("GHS2.png", draw_skull),
        ("GHS3.png", draw_exclamation)
    ]
    size = 512
    for name, draw_func in symbols:
        img = Image.new("RGBA", (size, size), (255, 255, 255, 0))
        draw = ImageDraw.Draw(img)
        cx, cy = draw_ghs_diamond(draw, size)
        draw_func(draw, cx, cy)

        out_path = os.path.join(vth.TEXTURES_DIR, name)
        img.save(out_path, "PNG", optimize=True)
        print(f"Đã tạo: {out_path} ({size}x{size} RGBA)")

def generate_biohazard():
    size = 512
    # Nền cam an toàn sinh học quốc tế
    img = Image.new("RGBA", (size, size), (247, 140, 26, 255))
    draw = ImageDraw.Draw(img)

    cx = size // 2
    cy = size // 2
    black = (15, 23, 42, 255)
    orange = (247, 140, 26, 255)

    # Biểu tượng 3 vòng cung Biohazard
    r_center = 42
    r_trefoil = 120
    r_outer_loop = 110
    r_inner_loop = 72

    # 1. Vòng tròn đen trung tâm lớn
    draw.ellipse([cx - r_center - 35, cy - r_center - 35, cx + r_center + 35, cy + r_center + 35], fill=black)
    # 2. Ba vòng cung lớn tỏa ra 3 hướng 120 độ
    for i in range(3):
        ang = (i * 120 - 90) * math.pi / 180.0
        ox = cx + math.cos(ang) * 75
        oy = cy + math.sin(ang) * 75
        draw.ellipse([ox - r_outer_loop, oy - r_outer_loop, ox + r_outer_loop, oy + r_outer_loop], outline=black, width=32)

    # 3. Vòng tròn rỗng trung tâm
    draw.ellipse([cx - r_center, cy - r_center, cx + r_center, cy + r_center], fill=orange)

    # 4. Ba khe cắt hình tròn tạo hình đặc trưng
    for i in range(3):
        ang = (i * 120 - 90) * math.pi / 180.0
        ox = cx + math.cos(ang) * 60
        oy = cy + math.sin(ang) * 60
        draw.ellipse([ox - 45, oy - 45, ox + 45, oy + 45], fill=orange)

    # 5. Vòng tròn nhỏ giữa tâm
    draw.ellipse([cx - 18, cy - 18, cx + 18, cy + 18], fill=black)
    draw.ellipse([cx - 6, cy - 6, cx + 6, cy + 6], fill=orange)

    # Viền bao ngoài đậm
    draw.rectangle([10, 10, size - 10, size - 10], outline=(180, 83, 9, 255), width=6)

    out_path = os.path.join(vth.TEXTURES_DIR, "T2_BieuTuongBiohazard.png")
    img.save(out_path, "PNG", optimize=True)
    print(f"Đã tạo: {out_path} ({size}x{size})")

if __name__ == "__main__":
    generate_ghs_symbols()
    generate_biohazard()
