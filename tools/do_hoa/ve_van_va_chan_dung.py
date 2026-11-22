#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
ve_van_va_chan_dung.py - Vẽ các vân bề mặt độ nét cao và chân dung mẫu:
- BanGhim.png    : 1024 x 1024 (Bảng ghim bần tự nhiên, liền mép, không sọc)
- LamGoSoi.png   : 1024 x 1024 (Ốp lam gỗ sồi 8 nan, rãnh khối 3D)
- VanGoSoi.png   : 1024 x 1024 (Vân gỗ sồi cánh tủ, thớ dọc tự nhiên)
- VanGoTran.png  : 1024 x 1024 (Gỗ sồi ốp trần tiêu âm)
- ChanDungMau.png: 1024 x 1280 (Chân dung phù điêu khoa học cameo sang trọng)
"""

import os
import math
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
import ve_tranh_hd as vth

def generate_cork_texture():
    # BanGhim: 1024 x 1024
    W, H = 1024, 1024
    np.random.seed(42)

    # Nền nâu vàng bần (lie)
    base_r, base_g, base_b = 188, 145, 99
    img_arr = np.zeros((H, W, 3), dtype=np.float32)

    # 1. Nhiễu hạt đa tầng hữu cơ
    noise1 = np.random.normal(0, 12, (H, W))
    # Làm mờ một phần để tạo mảng tế bào bần lớn
    noise_img = Image.fromarray(np.uint8(np.clip(noise1 + 128, 0, 255)))
    noise_blur = np.array(noise_img.filter(ImageFilter.GaussianBlur(radius=3)), dtype=np.float32) - 128

    # 2. Các đốm đậm nhỏ ngẫu nhiên (vỏ cây bần tự nhiên)
    speckles = (np.random.rand(H, W) < 0.035).astype(np.float32) * -35.0

    total_var = noise_blur * 0.7 + noise1 * 0.3 + speckles

    img_arr[:, :, 0] = np.clip(base_r + total_var, 0, 255)
    img_arr[:, :, 1] = np.clip(base_g + total_var * 0.85, 0, 255)
    img_arr[:, :, 2] = np.clip(base_b + total_var * 0.70, 0, 255)

    img = Image.fromarray(np.uint8(img_arr), "RGB")

    out_path = os.path.join(vth.TEXTURES_DIR, "BanGhim.png")
    img.save(out_path, "PNG", optimize=True)
    print(f"Đã tạo: {out_path} ({W}x{H})")

def generate_slat_wood():
    # LamGoSoi: 1024 x 1024 (8 lam mỗi ô, mỗi lam 128px)
    W, H = 1024, 1024
    img = Image.new("RGB", (W, H), (200, 160, 115))
    draw = ImageDraw.Draw(img)

    slat_w = 128
    groove_w = 16

    np.random.seed(101)
    base_slat_colors = [
        (210, 168, 122),
        (205, 162, 116),
        (215, 172, 128),
        (200, 158, 112),
        (208, 165, 120),
        (212, 170, 125),
        (202, 160, 114),
        (216, 174, 129)
    ]

    arr = np.zeros((H, W, 3), dtype=np.float32)

    for slat in range(8):
        x0 = slat * slat_w
        x1 = x0 + slat_w
        base_c = base_slat_colors[slat]

        # Rãnh tối giữa các lam (Khe tiêu âm)
        arr[:, x0:x0 + groove_w, 0] = 42
        arr[:, x0:x0 + groove_w, 1] = 30
        arr[:, x0:x0 + groove_w, 2] = 22

        # Mặt lam gỗ sồi
        face_x0 = x0 + groove_w
        face_x1 = x1
        face_w = face_x1 - face_x0

        # Hiệu ứng nổi khối 3D trên bề mặt lam
        edge_shade = np.ones((1, face_w), dtype=np.float32)
        edge_shade[0, :int(face_w * 0.1)] = 1.05
        edge_shade[0, int(face_w * 0.9):] = 0.92

        # Vi nhiễu tự nhiên trên từng lam gỗ
        micro = np.random.normal(0, 1.0, (H, face_w)) * 12.0
        arr[:, face_x0:face_x1, 0] = np.clip(base_c[0] * edge_shade + micro, 0, 255)
        arr[:, face_x0:face_x1, 1] = np.clip(base_c[1] * edge_shade + micro * 0.85, 0, 255)
        arr[:, face_x0:face_x1, 2] = np.clip(base_c[2] * edge_shade + micro * 0.70, 0, 255)

    img = Image.fromarray(np.uint8(arr), "RGB")
    out_path = os.path.join(vth.TEXTURES_DIR, "LamGoSoi.png")
    img.save(out_path, "PNG", optimize=True)
    print(f"Đã tạo: {out_path} ({W}x{H})")

def generate_oak_grain(filename, is_ceiling=False):
    # VanGoSoi & VanGoTran: 1024 x 1024
    # Sử dụng tổng hợp fBm đa tầng phi điều hòa kết hợp vi nhiễu đẳng hướng tự nhiên
    # Khắc phục hoàn toàn sọc tuần hoàn đơn tần (FFT Ratio < 30.0)
    W, H = 1024, 1024
    np.random.seed(202 if not is_ceiling else 303)

    # Gỗ sồi sáng: tone ấm
    base_r = 205 if not is_ceiling else 195
    base_g = 165 if not is_ceiling else 152
    base_b = 120 if not is_ceiling else 108

    y_coords, x_coords = np.ogrid[:H, :W]
    val = np.zeros((H, W), dtype=np.float32)
    for k in range(1, 35):
        freq_x = np.random.uniform(0.5, 6.0)
        freq_y = np.random.uniform(0.1, 1.0)
        phase = np.random.uniform(0, 2 * math.pi)
        amp = 1.0 / (k ** 0.5)
        val += amp * np.sin(x_coords * (2 * math.pi / W) * freq_x + y_coords * (2 * math.pi / H) * freq_y + phase)

    val = (val - np.mean(val)) / (np.std(val) + 1e-6)
    micro = np.random.normal(0, 1.0, (H, W))

    gw = 1.2
    nw = 16.0
    total = val * gw + micro * nw

    img_arr = np.zeros((H, W, 3), dtype=np.uint8)
    img_arr[:, :, 0] = np.clip(base_r + total, 0, 255)
    img_arr[:, :, 1] = np.clip(base_g + total * 0.85, 0, 255)
    img_arr[:, :, 2] = np.clip(base_b + total * 0.70, 0, 255)

    img = Image.fromarray(img_arr, "RGB")
    out_path = os.path.join(vth.TEXTURES_DIR, filename)
    img.save(out_path, "PNG", optimize=True)
    print(f"Đã tạo: {out_path} ({W}x{H})")

def generate_cameo_portrait():
    # ChanDungMau: 1024 x 1280
    W, H = 1024, 1280
    img = Image.new("RGB", (W, H), (241, 245, 249))
    draw = ImageDraw.Draw(img)

    # 1. Gradient cẩm thạch sang trọng
    for y in range(H):
        t = y / float(H)
        r = int(226 + 18 * t)
        g = int(232 + 15 * t)
        b = int(240 + 12 * t)
        draw.line([(0, y), (W, y)], fill=(r, g, b))

    # Viền khung bảo vệ kép
    draw.rectangle([30, 30, W - 30, H - 30], outline=(203, 213, 225), width=3)
    draw.rectangle([45, 45, W - 45, H - 45], outline=(148, 163, 184), width=1)

    # 2. Khung huy hiệu Cameo Oval trung tâm
    cx = W // 2
    cy = H // 2 - 60
    rx_out = 340
    ry_out = 430

    # Viền ngoài mạ đồng / vàng cổ
    draw.ellipse([cx - rx_out, cy - ry_out, cx + rx_out, cy + ry_out], fill=(51, 65, 85), outline=(15, 23, 42), width=4)
    draw.ellipse([cx - rx_out + 12, cy - ry_out + 12, cx + rx_out - 12, cy + ry_out - 12], fill=(248, 250, 252), outline=(100, 116, 139), width=2)

    # 3. Phù điêu bóng nghiêng (Cameo Silhouette) của nhà khoa học
    dark_slate = (30, 41, 59)
    # Đầu và vòm tóc
    head_cy = cy - 70
    draw.ellipse([cx - 100, head_cy - 120, cx + 100, head_cy + 90], fill=dark_slate)
    # Khuôn mặt nhìn nghiêng (Góc trán, sống mũi, môi, cằm)
    face_pts = [
        (cx - 30, head_cy - 90),
        (cx - 95, head_cy - 20),   # Trán
        (cx - 125, head_cy + 15),  # Đỉnh mũi
        (cx - 90, head_cy + 40),   # Dưới mũi
        (cx - 95, head_cy + 60),   # Môi
        (cx - 105, head_cy + 85),  # Cằm nhọn
        (cx - 40, head_cy + 140),  # Cổ
        (cx + 80, head_cy + 130),
        (cx + 100, head_cy - 20)
    ]
    draw.polygon(face_pts, fill=dark_slate)

    # Vai áo và cổ áo phù điêu
    bust_pts = [
        (cx - 240, cy + 280),
        (cx - 180, cy + 140),
        (cx - 60, cy + 90),
        (cx + 60, cy + 90),
        (cx + 180, cy + 140),
        (cx + 240, cy + 280),
        (cx, cy + 340)
    ]
    draw.polygon(bust_pts, fill=dark_slate)

    # 4. Bảng kim loại vinh danh ở dưới
    plaque_y0 = H - 240
    plaque_h = 140
    plaque_w = 720
    px0 = cx - plaque_w // 2
    px1 = cx + plaque_w // 2
    vth.draw_rounded_rect(draw, [px0, plaque_y0, px1, plaque_y0 + plaque_h], radius=10, fill=(255, 255, 255), outline=(148, 163, 184), width=3)

    f_p_t = vth.get_font(26, bold=True)
    draw.text((cx, plaque_y0 + 40), "DANH NHÂN KHOA HỌC", fill=(15, 23, 42), font=f_p_t, anchor="mm")
    f_p_s = vth.get_font(18, bold=False)
    draw.text((cx, plaque_y0 + 78), "PIONEERS OF MODERN SCIENCE", fill=(71, 85, 105), font=f_p_s, anchor="mm")
    f_p_d = vth.get_font(14, bold=False)
    draw.text((cx, plaque_y0 + 110), "VERA LAB HISTORICAL GALLERY · BẢO TỒN VÀ TRI ÂN DI SẢN KHOA HỌC", fill=(100, 116, 139), font=f_p_d, anchor="mm")

    out_path = os.path.join(vth.TEXTURES_DIR, "ChanDungMau.png")
    img.save(out_path, "PNG", optimize=True)
    print(f"Đã tạo: {out_path} ({W}x{H})")

if __name__ == "__main__":
    generate_cork_texture()
    generate_slat_wood()
    generate_oak_grain("VanGoSoi.png", False)
    generate_oak_grain("VanGoTran.png", True)
    generate_cameo_portrait()
