#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
ve_man_hinh_va_co.py - Vẽ cờ Việt Nam, nền màn hình, màn giải phẫu, ảnh điện di gel và dải sọc màu
Chuẩn độ phân giải Retina HD:
- CoVietNam.png : 1200 x 800
- NenManHinh.png: 1280 x 720
- T2_ManGiaiPhau.png: 512 x 1024
- T2_AnhGel.png: 512 x 384
- SocMau.png: 128 x 512
"""

import os
import math
from PIL import Image, ImageDraw, ImageFont
import ve_tranh_hd as vth

def generate_flag_vietnam():
    W, H = 1200, 800
    # Nền cờ đỏ chuẩn quốc gia (Pantone 186 C / RGB: 218, 37, 29)
    img = Image.new("RGB", (W, H), (218, 37, 29))
    draw = ImageDraw.Draw(img)

    cx = W // 2
    cy = H // 2
    # Bán kính đường tròn ngoại tiếp bằng 2/5 chiều dài cờ: 2R = 2/5 * 1200 = 480 => R = 240
    R = int(W * 0.2)
    # Tỉ lệ bán kính trong của ngôi sao 5 cánh: r = R * (sqrt(5)-1) / (2 * sqrt(5) + 2) = R * 0.381966
    r = R * 0.381966

    pts = []
    for i in range(10):
        # Đỉnh đầu tiên hướng thẳng lên trên (-pi/2)
        ang = -math.pi / 2 + i * math.pi / 5
        rad = R if i % 2 == 0 else r
        px = cx + math.cos(ang) * rad
        py = cy + math.sin(ang) * rad
        pts.append((px, py))

    # Màu vàng sao (RGB: 255, 255, 0 / 255, 222, 0)
    draw.polygon(pts, fill=(255, 222, 0))

    out_path = os.path.join(vth.TEXTURES_DIR, "CoVietNam.png")
    img.save(out_path, "PNG", optimize=True)
    print(f"Đã tạo: {out_path} ({W}x{H})")

def generate_screen_background():
    W, H = 1280, 720
    img = Image.new("RGB", (W, H), (11, 19, 43))
    draw = ImageDraw.Draw(img)

    # 1. Gradient nền từ xanh đen đến xanh dương đậm
    for y in range(H):
        t = y / H
        r = int(11 + 18 * t)
        g = int(19 + 32 * t)
        b = int(43 + 65 * t)
        draw.line([(0, y), (W, y)], fill=(r, g, b))

    # 2. Lưới kỹ thuật mờ
    grid_spacing = 40
    for x in range(0, W, grid_spacing):
        draw.line([(x, 0), (x, H)], fill=(28, 48, 86), width=1)
    for y in range(0, H, grid_spacing):
        draw.line([(0, y), (W, y)], fill=(28, 48, 86), width=1)

    # 3. Dải tiêu đề phát sáng ở trên
    banner_h = 80
    for y in range(banner_h):
        t = y / banner_h
        r = int(14 + (30 - 14) * t)
        g = int(116 + (144 - 116) * t)
        b = int(144 + (255 - 144) * t)
        draw.line([(0, y), (W, y)], fill=(r, g, b))
    draw.line([(0, banner_h), (W, banner_h)], fill=(56, 189, 248), width=3)

    f_title = vth.get_font(28, bold=True)
    draw.text((40, banner_h // 2), "VERA LAB · HỆ THỐNG MÔ PHỎNG VÀ THỰC NGHIỆM KHOA HỌC", fill=(255, 255, 255), font=f_title, anchor="lm")
    f_stat = vth.get_font(16, bold=False)
    draw.text((W - 40, banner_h // 2), "ĐÃ KẾT NỐI HỆ THỐNG · 2026", fill=(224, 242, 254), font=f_stat, anchor="rm")

    # 4. Họa tiết trung tâm: Biểu tượng nguyên tử và mạch điện
    cx = W // 2
    cy = H // 2 + 30
    draw.ellipse([cx - 14, cy - 14, cx + 14, cy + 14], fill=(56, 189, 248))
    # 3 quỹ đạo elip electron
    for ang_deg in [0, 60, 120]:
        rad = ang_deg * math.pi / 180.0
        # Vẽ elip xoay
        pts = []
        for step in range(72):
            t_step = step * math.pi / 36
            ex = 160 * math.cos(t_step)
            ey = 60 * math.sin(t_step)
            rot_x = cx + ex * math.cos(rad) - ey * math.sin(rad)
            rot_y = cy + ex * math.sin(rad) + ey * math.cos(rad)
            pts.append((rot_x, rot_y))
        draw.line(pts + [pts[0]], fill=(56, 189, 248), width=2)

    f_ver = vth.get_font(20, bold=True)
    draw.text((cx, cy + 180), "VERA LAB WORKSTATION OS", fill=(148, 163, 184), font=f_ver, anchor="mm")
    draw.text((cx, cy + 210), "Hệ điều hành trạm làm việc phòng thí nghiệm chuẩn quốc tế", fill=(100, 116, 139), font=vth.get_font(14, bold=False), anchor="mm")

    out_path = os.path.join(vth.TEXTURES_DIR, "NenManHinh.png")
    img.save(out_path, "PNG", optimize=True)
    print(f"Đã tạo: {out_path} ({W}x{H})")

def generate_anatomy_screen():
    W, H = 512, 1024
    img = Image.new("RGB", (W, H), (8, 15, 26))
    draw = ImageDraw.Draw(img)

    # 1. Header bàn giải phẫu
    head_h = 70
    draw.rectangle([0, 0, W, head_h], fill=(15, 23, 42))
    draw.line([(0, head_h), (W, head_h)], fill=(14, 165, 233), width=2)
    draw.text((20, 24), "BÀN GIẢI PHẪU ẢO ANATOMAGE", fill=(255, 255, 255), font=vth.get_font(17, bold=True))
    draw.text((20, 48), "VIRTUAL DISSECTION TABLE 3D", fill=(148, 163, 184), font=vth.get_font(12, bold=False))

    # 2. Thanh công cụ bên trái (Lớp giải phẫu)
    bar_w = 140
    draw.rectangle([0, head_h, bar_w, H], fill=(15, 23, 42))
    draw.line([(bar_w, head_h), (bar_w, H)], fill=(30, 41, 59), width=2)

    layers = [
        ("BỘ XƯƠNG", "Skeletal", True),
        ("CƠ BẮP", "Muscular", False),
        ("MẠCH MÁU", "Vascular", False),
        ("NỘI TẠNG", "Organs", False),
        ("THẦN KINH", "Nervous", False),
        ("MẶT CẮT CT", "CT Slice", False)
    ]
    f_btn = vth.get_font(13, bold=True)
    f_sub = vth.get_font(11, bold=False)
    for idx, (l_vi, l_en, active) in enumerate(layers):
        by = head_h + 30 + idx * 75
        btn_bg = (14, 165, 233) if active else (30, 41, 59)
        txt_col = (255, 255, 255) if active else (148, 163, 184)
        vth.draw_rounded_rect(draw, [15, by, bar_w - 15, by + 55], radius=6, fill=btn_bg)
        draw.text((bar_w // 2, by + 18), l_vi, fill=txt_col, font=f_btn, anchor="mm")
        draw.text((bar_w // 2, by + 36), l_en, fill=txt_col, font=f_sub, anchor="mm")

    # 3. Khung hiển thị mô hình giải phẫu chính (bên phải)
    cx = (bar_w + W) // 2
    cy = (head_h + H) // 2 - 40

    # Lưới đo hệ mét
    for gx in range(bar_w + 30, W, 40):
        draw.line([(gx, head_h), (gx, H - 80)], fill=(15, 30, 45), width=1)
    for gy in range(head_h + 20, H - 80, 40):
        draw.line([(bar_w, gy), (W, gy)], fill=(15, 30, 45), width=1)

    # Hình bóng bộ xương người chuẩn (Human skeleton model representation)
    # Hộp sọ (Skull)
    draw.ellipse([cx - 30, cy - 280, cx + 30, cy - 210], fill=(226, 232, 240), outline=(56, 189, 248), width=2)
    # Cột sống (Spine)
    draw.line([(cx, cy - 210), (cx, cy + 50)], fill=(203, 213, 225), width=8)
    # Lồng ngực và xương sườn (Ribcage)
    for ry in range(-180, -40, 22):
        rib_w = 60 + abs(ry + 110) * -0.35
        draw.arc([cx - rib_w, cy + ry - 12, cx + rib_w, cy + ry + 12], start=0, end=180, fill=(203, 213, 225), width=3)
    # Xương chậu (Pelvis)
    draw.polygon([(cx - 55, cy + 20), (cx + 55, cy + 20), (cx + 35, cy + 65), (cx - 35, cy + 65)], fill=(226, 232, 240))

    # Xương chi trên (Tay)
    for side in [-1, 1]:
        # Cánh tay
        draw.line([(cx + side * 45, cy - 160), (cx + side * 90, cy - 60)], fill=(203, 213, 225), width=6)
        # Cẳng tay
        draw.line([(cx + side * 90, cy - 60), (cx + side * 115, cy + 40)], fill=(203, 213, 225), width=5)

    # Xương chi dưới (Chân)
    for side in [-1, 1]:
        # Xương đùi (Femur)
        draw.line([(cx + side * 30, cy + 65), (cx + side * 45, cy + 220)], fill=(203, 213, 225), width=7)
        # Khớp gối
        draw.ellipse([cx + side * 45 - 8, cy + 220 - 8, cx + side * 45 + 8, cy + 220 + 8], fill=(56, 189, 248))
        # Cẳng chân (Tibia/Fibula)
        draw.line([(cx + side * 45, cy + 220), (cx + side * 50, cy + 360)], fill=(203, 213, 225), width=6)

    # Thước đo laser cắt lớp (Cross-section scanner laser line)
    scan_y = cy - 20
    draw.line([(bar_w, scan_y), (W, scan_y)], fill=(239, 68, 68), width=2)
    draw.text((W - 15, scan_y - 12), "Mặt cắt Z: 1.42 m", fill=(239, 68, 68), font=vth.get_font(12, bold=True), anchor="rm")

    # 4. Thanh trạng thái ở dưới
    draw.rectangle([bar_w, H - 75, W, H], fill=(15, 23, 42))
    draw.line([(bar_w, H - 75), (W, H - 75)], fill=(30, 41, 59), width=2)
    draw.text((bar_w + 20, H - 48), "Lớp hiển thị: BỘ XƯƠNG (SKELETAL) · ĐỘ PHÂN GIẢI 0.2 mm", fill=(56, 189, 248), font=vth.get_font(12, bold=True))
    draw.text((bar_w + 20, H - 25), "Chế độ tương tác: Xoay 3D, bóc tách mô mềm và đo kích thước nhân trắc.", fill=(148, 163, 184), font=vth.get_font(11, bold=False))

    out_path = os.path.join(vth.TEXTURES_DIR, "T2_ManGiaiPhau.png")
    img.save(out_path, "PNG", optimize=True)
    print(f"Đã tạo: {out_path} ({W}x{H})")

def generate_gel_electrophoresis():
    W, H = 512, 384
    # Nền gel agarose phát huỳnh quang dưới đèn UV
    img = Image.new("RGB", (W, H), (15, 12, 25))
    draw = ImageDraw.Draw(img)

    # 1. Các giếng tra mẫu ở trên (6 wells)
    well_y = 45
    well_w = 40
    well_h = 14
    for lane in range(6):
        wx = 65 + lane * 70
        draw.rectangle([wx, well_y, wx + well_w, well_y + well_h], fill=(45, 40, 65), outline=(75, 70, 105), width=1)
        # Số thứ tự giếng
        draw.text((wx + well_w // 2, well_y - 15), f"L{lane+1}" if lane > 0 else "Ladder", fill=(148, 163, 184), font=vth.get_font(11, bold=True), anchor="mm")

    # 2. Vạch chuẩn ADN Ladder ở Giếng 1 (Lane 0)
    ladder_wx = 65
    ladder_bands = [
        (3000, 75, 0.4),
        (2000, 105, 0.5),
        (1500, 140, 0.6),
        (1000, 180, 0.9),  # Vạch chuẩn sáng 1000bp
        (750, 220, 0.7),
        (500, 265, 0.95),  # Vạch chuẩn sáng 500bp
        (250, 310, 0.6),
        (100, 345, 0.4)
    ]
    f_bp = vth.get_font(10, bold=False)
    for bp, by, intensity in ladder_bands:
        # Vạch sáng phát huỳnh quang
        glow_alpha = int(255 * intensity)
        glow_col = (int(255 * intensity), int(220 * intensity), int(255 * intensity))
        draw.rectangle([ladder_wx + 2, by - 3, ladder_wx + well_w - 2, by + 3], fill=glow_col)
        # Nhãn bp bên trái
        draw.text((ladder_wx - 10, by), f"{bp} bp", fill=(148, 163, 184), font=f_bp, anchor="rm")

    # 3. Các vạch mẫu thử ở Giếng 2..6
    sample_bands = [
        # Lane 1 (L2): Đối chứng âm (không có vạch)
        [],
        # Lane 2 (L3): Sản phẩm PCR mẫu 1 (vạch 500bp)
        [(500, 265, 0.9)],
        # Lane 3 (L4): Sản phẩm PCR mẫu 2 (vạch 500bp)
        [(500, 265, 0.95)],
        # Lane 4 (L5): Cắt enzyme giới hạn (2 vạch: 750bp và 250bp)
        [(750, 220, 0.75), (250, 310, 0.7)],
        # Lane 5 (L6): Đột biến gen / đoạn chèn (vạch 1000bp)
        [(1000, 180, 0.85)]
    ]
    for lane_idx, b_list in enumerate(sample_bands):
        wx = 65 + (lane_idx + 1) * 70
        for bp, by, intensity in b_list:
            glow_col = (int(255 * intensity), int(220 * intensity), int(255 * intensity))
            draw.rectangle([wx + 2, by - 3, wx + well_w - 2, by + 3], fill=glow_col)

    # 4. Nhãn ghi chú điện di ở góc dưới
    draw.text((W - 20, H - 20), "AGAROSE GEL 1.5% · ETHIDIUM BROMIDE 0.5 µg/mL · 100V, 45 MIN", fill=(100, 116, 139), font=vth.get_font(10, bold=False), anchor="rm")

    out_path = os.path.join(vth.TEXTURES_DIR, "T2_AnhGel.png")
    img.save(out_path, "PNG", optimize=True)
    print(f"Đã tạo: {out_path} ({W}x{H})")

def generate_stripe_pattern():
    # SocMau: 128 x 512
    W, H = 128, 512
    # Nền trắng ấm
    img = Image.new("RGB", (W, H), (242, 240, 235))
    draw = ImageDraw.Draw(img)

    # 4 dải màu supergraphic phân khu:
    # 1. Zone Lab (Xanh ngọc / Teal): 40..120
    draw.rectangle([0, 40, W, 120], fill=(0, 143, 153))
    # 2. Zone Analysis (Xanh dương / Blue): 152..232
    draw.rectangle([0, 152, W, 232], fill=(37, 99, 235))
    # 3. Zone Store (Cam đất / Amber): 264..344
    draw.rectangle([0, 264, W, 344], fill=(217, 119, 6))
    # 4. Zone Robot (Tím / Purple): 376..456
    draw.rectangle([0, 376, W, 456], fill=(147, 51, 234))

    out_path = os.path.join(vth.TEXTURES_DIR, "SocMau.png")
    img.save(out_path, "PNG", optimize=True)
    print(f"Đã tạo: {out_path} ({W}x{H})")

if __name__ == "__main__":
    generate_flag_vietnam()
    generate_screen_background()
    generate_anatomy_screen()
    generate_gel_electrophoresis()
    generate_stripe_pattern()
