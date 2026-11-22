#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
ve_posters.py - Vẽ 7 áp phích dự án nghiên cứu học sinh (Poster0 .. Poster6)
Độ phân giải cao 1024 x 1408, tỉ lệ chuẩn 1 : 1.375.
Tiêu đề khớp 100% với LabSceneBuilder.Intl.cs, font Be Vietnam Pro, nội dung khoa học chuẩn xác.
"""

import os
import math
from PIL import Image, ImageDraw, ImageFont
import ve_tranh_hd as vth

POSTER_DEFS = [
    {
        "id": 0,
        "vi": "Axit – Bazơ quanh ta",
        "en": "Acids & Bases in Daily Life",
        "color": (0, 143, 153),
        "desc": "Nghiên cứu chỉ thị màu tự nhiên từ bắp cải tím và đo pH các dung dịch gia dụng.",
        "diagram_type": "ph_scale"
    },
    {
        "id": 1,
        "vi": "Pin điện hóa tự chế",
        "en": "DIY Galvanic Cells & Electrochemistry",
        "color": (234, 88, 12),
        "desc": "Thiết lập pin điện hóa Daniell (Zn - Cu) và pin điện phân từ củ quả.",
        "diagram_type": "galvanic_cell"
    },
    {
        "id": 2,
        "vi": "Hóa học xanh",
        "en": "12 Principles of Green Chemistry",
        "color": (22, 163, 74),
        "desc": "Định hướng phát triển bền vững: tối ưu kinh tế nguyên tử và dung môi an toàn.",
        "diagram_type": "green_chem"
    },
    {
        "id": 3,
        "vi": "Xu hướng tuần hoàn",
        "en": "Periodic Trends in Elements",
        "color": (217, 119, 6),
        "desc": "Quy luật biến đổi bán kính nguyên tử, độ âm điện và năng lượng ion hóa.",
        "diagram_type": "trends"
    },
    {
        "id": 4,
        "vi": "Tốc độ phản ứng",
        "en": "Chemical Kinetics & Reaction Rates",
        "color": (37, 99, 235),
        "desc": "Ảnh hưởng của nồng độ, nhiệt độ và chất xúc tác lên năng lượng hoạt hóa.",
        "diagram_type": "kinetics"
    },
    {
        "id": 5,
        "vi": "Polime & nhựa tái chế",
        "en": "Polymers & Plastic Recycling",
        "color": (147, 51, 234),
        "desc": "Phân loại mã nhựa SPI 1-7, tổng hợp nhựa sinh học PLA tự phân hủy.",
        "diagram_type": "polymers"
    },
    {
        "id": 6,
        "vi": "Hội thi KHKT 2026",
        "en": "National Science Fair 2026",
        "color": (219, 39, 119),
        "desc": "Quy trình nghiên cứu khoa học STEM thực nghiệm và phương pháp kiểm định.",
        "diagram_type": "stem_fair"
    }
]

def draw_card(draw, box, title, color):
    x0, y0, x1, y1 = box
    vth.draw_rounded_rect(draw, [x0, y0, x1, y1], radius=10, fill=(255, 255, 255), outline=(226, 232, 240), width=2)
    # Thanh tiêu đề card
    th = 38
    draw.rounded_rectangle([x0, y0, x1, y0 + th], radius=8, fill=(color[0], color[1], color[2], 25))
    draw.line([(x0, y0 + th), (x1, y0 + th)], fill=(color[0], color[1], color[2]), width=2)
    f_card = vth.get_font(18, bold=True)
    draw.text((x0 + 16, y0 + th // 2), title, fill=(color[0], color[1], color[2]), font=f_card, anchor="lm")

def draw_diagram_ph(draw, box):
    x0, y0, x1, y1 = box
    w = x1 - x0
    h = y1 - y0
    # Dải màu thang pH (0 đến 14)
    ph_y0 = y0 + 35
    ph_h = 32
    colors_ph = [
        (220, 38, 38),   # 0: Đỏ đậm
        (239, 68, 68),   # 1
        (249, 115, 22),  # 2: Cam đỏ (chanh pH 2)
        (245, 158, 11),  # 3: Cam (giấm pH 3)
        (250, 204, 21),  # 4: Vàng cam
        (234, 179, 8),   # 5: Vàng
        (132, 204, 22),  # 6: Vàng xanh (sữa pH 6.5)
        (34, 197, 94),   # 7: Xanh lục trung tính (nước cất pH 7)
        (20, 184, 166),  # 8: Xanh ngọc
        (6, 182, 212),   # 9: Xanh lơ (baking soda pH 8.5)
        (59, 130, 246),  # 10: Xanh dương (xà phòng pH 10)
        (99, 102, 241),  # 11: Chàm
        (139, 92, 246),  # 12: Tím nhạt
        (168, 85, 247),  # 13: Tím (thuốc tẩy pH 13)
        (109, 40, 217)   # 14: Tím đậm (xút NaOH pH 14)
    ]
    seg_w = w / 15
    f_ph = vth.get_font(13, bold=True)
    for i in range(15):
        sx0 = x0 + i * seg_w
        sx1 = sx0 + seg_w
        draw.rectangle([sx0, ph_y0, sx1, ph_y0 + ph_h], fill=colors_ph[i])
        draw.text((sx0 + seg_w / 2, ph_y0 + ph_h / 2), str(i), fill=(255, 255, 255), font=f_ph, anchor="mm")

    f_lbl = vth.get_font(14, bold=False)
    draw.text((x0 + 10, ph_y0 - 15), "Axit mạnh (pH < 3)", fill=(220, 38, 38), font=f_lbl, anchor="lm")
    draw.text((x0 + w / 2, ph_y0 - 15), "Trung tính (pH = 7)", fill=(22, 163, 74), font=f_lbl, anchor="mm")
    draw.text((x1 - 10, ph_y0 - 15), "Bazơ mạnh (pH > 11)", fill=(109, 40, 217), font=f_lbl, anchor="rm")

    # Mẫu chất chỉ thị
    substances = [
        ("Nước chanh", "pH 2.2", 2.2),
        ("Giấm ăn", "pH 2.9", 2.9),
        ("Cà phê", "pH 5.0", 5.0),
        ("Nước cất", "pH 7.0", 7.0),
        ("Baking soda", "pH 8.4", 8.4),
        ("Xà phòng", "pH 10.0", 10.0),
        ("Dung dịch NaOH", "pH 13.0", 13.0)
    ]
    f_sub = vth.get_font(12, bold=False)
    for name, ph_str, ph_val in substances:
        pos_x = x0 + (ph_val / 14.0) * (w - 20) + 10
        draw.line([(pos_x, ph_y0 + ph_h), (pos_x, ph_y0 + ph_h + 18)], fill=(71, 85, 105), width=1)
        draw.text((pos_x, ph_y0 + ph_h + 28), name, fill=(30, 41, 59), font=f_sub, anchor="mm")
        draw.text((pos_x, ph_y0 + ph_h + 44), ph_str, fill=(100, 116, 139), font=f_sub, anchor="mm")

def draw_diagram_galvanic(draw, box):
    x0, y0, x1, y1 = box
    # Cốc Anot Zn bên trái
    w_beaker = 160
    h_beaker = 150
    b1_x0 = x0 + 60
    b1_y0 = y0 + 50
    draw.rectangle([b1_x0, b1_y0, b1_x0 + w_beaker, b1_y0 + h_beaker], fill=(224, 242, 254), outline=(56, 189, 248), width=3)
    # Điện cực Kẽm (Zn)
    draw.rectangle([b1_x0 + 40, b1_y0 - 25, b1_x0 + 75, b1_y0 + 120], fill=(148, 163, 184), outline=(100, 116, 139), width=2)

    # Cốc Catot Cu bên phải
    b2_x0 = x1 - 60 - w_beaker
    b2_y0 = y0 + 50
    draw.rectangle([b2_x0, b2_y0, b2_x0 + w_beaker, b2_y0 + h_beaker], fill=(254, 243, 199), outline=(245, 158, 11), width=3)
    # Điện cực Đồng (Cu)
    draw.rectangle([b2_x0 + w_beaker - 75, b2_y0 - 25, b2_x0 + w_beaker - 40, b2_y0 + 120], fill=(217, 119, 6), outline=(180, 83, 9), width=2)

    # Cầu muối (Salt bridge) uốn cong chữ U
    draw.rectangle([b1_x0 + 100, b1_y0 + 20, b2_x0 + 60, b1_y0 + 50], fill=(241, 245, 249), outline=(148, 163, 184), width=2)
    draw.rectangle([b1_x0 + 100, b1_y0 + 20, b1_x0 + 130, b1_y0 + 110], fill=(241, 245, 249), outline=(148, 163, 184), width=2)
    draw.rectangle([b2_x0 + 30, b2_y0 + 20, b2_x0 + 60, b2_y0 + 110], fill=(241, 245, 249), outline=(148, 163, 184), width=2)

    # Dây dẫn và Vôn kế (Voltmeter)
    wire_y = y0 + 10
    e1_x = b1_x0 + 57
    e2_x = b2_x0 + w_beaker - 57
    v_cx = (e1_x + e2_x) // 2
    draw.line([(e1_x, b1_y0 - 25), (e1_x, wire_y), (v_cx - 25, wire_y)], fill=(30, 41, 59), width=3)
    draw.line([(v_cx + 25, wire_y), (e2_x, wire_y), (e2_x, b2_y0 - 25)], fill=(30, 41, 59), width=3)
    # Vôn kế tròn
    draw.ellipse([v_cx - 28, wire_y - 28, v_cx + 28, wire_y + 28], fill=(255, 255, 255), outline=(30, 41, 59), width=3)
    draw.text((v_cx, wire_y), "+1.10 V", fill=(220, 38, 38), font=vth.get_font(13, bold=True), anchor="mm")

    f_chem = vth.get_font(14, bold=True)
    draw.text((b1_x0 + 57, b1_y0 + 135), "Zn (Anot, -)", fill=(71, 85, 105), font=f_chem, anchor="mm")
    draw.text((b2_x0 + w_beaker - 57, b2_y0 + 135), "Cu (Catot, +)", fill=(180, 83, 9), font=f_chem, anchor="mm")
    draw.text((v_cx, b1_y0 + 35), "Cầu muối KNO₃", fill=(100, 116, 139), font=vth.get_font(12, bold=False), anchor="mm")

    draw.text((b1_x0 + w_beaker // 2, b1_y0 + h_beaker + 20), "Zn²⁺ + 2e⁻ ← Zn", fill=(2, 132, 199), font=vth.get_font(13, bold=False), anchor="mm")
    draw.text((b2_x0 + w_beaker // 2, b2_y0 + h_beaker + 20), "Cu²⁺ + 2e⁻ → Cu", fill=(217, 119, 6), font=vth.get_font(13, bold=False), anchor="mm")

def draw_diagram_green_chem(draw, box):
    x0, y0, x1, y1 = box
    cx = (x0 + x1) // 2
    cy = (y0 + y1) // 2
    r_outer = 110
    r_inner = 55
    # Vòng tròn trung tâm
    draw.ellipse([cx - r_inner, cy - r_inner, cx + r_inner, cy + r_inner], fill=(220, 252, 231), outline=(22, 163, 74), width=3)
    f_ctr = vth.get_font(15, bold=True)
    draw.text((cx, cy - 8), "12 NGUYÊN TẮC", fill=(21, 128, 61), font=f_ctr, anchor="mm")
    draw.text((cx, cy + 12), "HÓA HỌC XANH", fill=(22, 163, 74), font=f_ctr, anchor="mm")

    # 12 cánh nguyên tắc
    principles = [
        "1. Phòng ngừa chất thải",
        "2. Kinh tế nguyên tử",
        "3. Tổng hợp ít độc hại",
        "4. Hóa chất an toàn",
        "5. Dung môi an toàn",
        "6. Tiết kiệm năng lượng",
        "7. Nguyên liệu tái tạo",
        "8. Giảm dẫn xuất",
        "9. Sử dụng xúc tác",
        "10. Tự phân hủy sinh học",
        "11. Phân tích thời gian thực",
        "12. Ngừa tai nạn hóa chất"
    ]
    f_p = vth.get_font(12, bold=False)
    for i, p_txt in enumerate(principles):
        angle = (i * 30 - 90) * math.pi / 180.0
        px = cx + math.cos(angle) * (r_outer + 30)
        py = cy + math.sin(angle) * (r_outer + 30)
        # Chấm tròn
        dot_x = cx + math.cos(angle) * r_outer
        dot_y = cy + math.sin(angle) * r_outer
        draw.line([(cx + math.cos(angle) * r_inner, cy + math.sin(angle) * r_inner), (dot_x, dot_y)], fill=(187, 247, 208), width=2)
        draw.ellipse([dot_x - 6, dot_y - 6, dot_x + 6, dot_y + 6], fill=(34, 197, 94))
        # Nhãn
        align = "mm"
        if px > cx + 15: align = "lm"
        elif px < cx - 15: align = "rm"
        draw.text((px, py), p_txt, fill=(30, 41, 59), font=f_p, anchor=align)

def draw_diagram_trends(draw, box):
    x0, y0, x1, y1 = box
    w = x1 - x0
    h = y1 - y0
    # Khung mô phỏng bảng tuần hoàn
    vth.draw_rounded_rect(draw, [x0 + 40, y0 + 30, x1 - 40, y1 - 40], radius=8, fill=(248, 250, 252), outline=(203, 213, 225), width=2)

    f_tr = vth.get_font(14, bold=True)
    # Mũi tên Bán kính nguyên tử (Atomic Radius): Tăng từ Phải -> Trái, Trên -> Dưới
    draw.line([(x1 - 80, y0 + 60), (x0 + 80, y0 + 60)], fill=(225, 29, 72), width=4)
    draw.polygon([(x0 + 70, y0 + 60), (x0 + 85, y0 + 53), (x0 + 85, y0 + 67)], fill=(225, 29, 72))
    draw.text(((x0 + x1) // 2, y0 + 45), "Bán kính nguyên tử tăng dần ←", fill=(225, 29, 72), font=f_tr, anchor="mm")

    # Mũi tên Độ âm điện & Năng lượng ion hóa: Tăng từ Dưới -> Trên, Trái -> Phải
    draw.line([(x0 + 80, y1 - 70), (x1 - 80, y1 - 70)], fill=(2, 132, 199), width=4)
    draw.polygon([(x1 - 70, y1 - 70), (x1 - 85, y1 - 77), (x1 - 85, y1 - 63)], fill=(2, 132, 199))
    draw.text(((x0 + x1) // 2, y1 - 85), "Độ âm điện & Năng lượng ion hóa tăng dần →", fill=(2, 132, 199), font=f_tr, anchor="mm")

    # Mũi tên dọc bên trái: Bán kính tăng đi xuống
    draw.line([(x0 + 65, y0 + 90), (x0 + 65, y1 - 100)], fill=(225, 29, 72), width=4)
    draw.polygon([(x0 + 65, y1 - 90), (x0 + 58, y1 - 105), (x0 + 72, y1 - 105)], fill=(225, 29, 72))

    # Mũi tên dọc bên phải: Năng lượng ion hóa tăng đi lên
    draw.line([(x1 - 65, y1 - 100), (x1 - 65, y0 + 90)], fill=(2, 132, 199), width=4)
    draw.polygon([(x1 - 65, y0 + 80), (x1 - 72, y0 + 95), (x1 - 58, y0 + 95)], fill=(2, 132, 199))

    # Minh họa các nguyên tố cực trị
    f_el = vth.get_font(13, bold=False)
    draw.text((x0 + 120, y1 - 130), "Cs, Fr: Bán kính lớn nhất, tính kim loại mạnh nhất", fill=(225, 29, 72), font=f_el, anchor="lm")
    draw.text((x1 - 120, y0 + 100), "F: Độ âm điện lớn nhất (3.98), tính phi kim mạnh nhất", fill=(2, 132, 199), font=f_el, anchor="rm")
    draw.text((x1 - 120, y0 + 125), "He: Năng lượng ion hóa I₁ cao nhất (2372 kJ/mol)", fill=(100, 116, 139), font=f_el, anchor="rm")

def draw_diagram_kinetics(draw, box):
    x0, y0, x1, y1 = box
    # Đồ thị Năng lượng hoạt hóa (Activation Energy)
    origin_x = x0 + 70
    origin_y = y1 - 50
    axis_w = (x1 - x0) - 140
    axis_h = (y1 - y0) - 90

    # Trục tọa độ
    draw.line([(origin_x, origin_y), (origin_x + axis_w, origin_y)], fill=(71, 85, 105), width=2)
    draw.line([(origin_x, origin_y), (origin_x, origin_y - axis_h)], fill=(71, 85, 105), width=2)
    draw.text((origin_x + axis_w, origin_y + 16), "Tiến trình phản ứng", fill=(71, 85, 105), font=vth.get_font(12, bold=False), anchor="rm")
    draw.text((origin_x - 10, origin_y - axis_h), "Năng lượng (kJ/mol)", fill=(71, 85, 105), font=vth.get_font(12, bold=False), anchor="rb")

    # Mức năng lượng chất phản ứng
    react_y = origin_y - 45
    draw.line([(origin_x, react_y), (origin_x + 60, react_y)], fill=(30, 41, 59), width=3)
    draw.text((origin_x + 30, react_y - 12), "Chất tham gia (A + B)", fill=(30, 41, 59), font=vth.get_font(12, bold=True), anchor="mm")

    # Mức năng lượng sản phẩm
    prod_y = origin_y - 20
    draw.line([(origin_x + axis_w - 70, prod_y), (origin_x + axis_w, prod_y)], fill=(30, 41, 59), width=3)
    draw.text((origin_x + axis_w - 35, prod_y - 12), "Sản phẩm (C + D)", fill=(30, 41, 59), font=vth.get_font(12, bold=True), anchor="mm")

    # Đường cong không có xúc tác (Đỉnh cao Ea)
    peak1_x = origin_x + axis_w // 2 - 20
    peak1_y = origin_y - axis_h + 20
    pts_curve1 = []
    for step in range(101):
        t = step / 100.0
        px = origin_x + 60 + t * (axis_w - 130)
        # Đường cong Gauss / Parabol
        py = react_y - (react_y - peak1_y) * math.sin(t * math.pi) - (prod_y - react_y) * (t ** 2)
        pts_curve1.append((px, py))
    draw.line(pts_curve1, fill=(220, 38, 38), width=3)
    draw.text((peak1_x, peak1_y - 14), "Ea (Không xúc tác)", fill=(220, 38, 38), font=vth.get_font(13, bold=True), anchor="mm")

    # Đường cong CÓ xúc tác (Đỉnh thấp Ea')
    peak2_y = origin_y - axis_h + 65
    pts_curve2 = []
    for step in range(101):
        t = step / 100.0
        px = origin_x + 60 + t * (axis_w - 130)
        py = react_y - (react_y - peak2_y) * math.sin(t * math.pi) - (prod_y - react_y) * (t ** 2)
        pts_curve2.append((px, py))
    draw.line(pts_curve2, fill=(22, 163, 74), width=3)
    draw.text((peak1_x, peak2_y + 16), "Ea' (Có xúc tác)", fill=(22, 163, 74), font=vth.get_font(13, bold=True), anchor="mm")

def draw_diagram_polymers(draw, box):
    x0, y0, x1, y1 = box
    w = x1 - x0
    # Vẽ 7 mã tái chế SPI (1-7)
    spi_codes = [
        ("1", "PETE", "Chai nước khoáng, nước ngọt"),
        ("2", "HDPE", "Chai sữa, bình dầu gội"),
        ("3", "PVC", "Ống nước, màng bọc"),
        ("4", "LDPE", "Túi nilon, màng bọc"),
        ("5", "PP", "Hộp đựng thực phẩm, cốc"),
        ("6", "PS", "Hộp xốp, dao nĩa nhựa"),
        ("7", "OTHER", "Nhựa sinh học PLA, polycarbonate")
    ]
    f_code = vth.get_font(18, bold=True)
    f_poly = vth.get_font(13, bold=True)
    f_ex = vth.get_font(11, bold=False)

    for i, (code, poly_name, ex_str) in enumerate(spi_codes):
        cx = x0 + 40 + (i % 4) * 225 if i < 4 else (x0 + 80 + (i - 4) * 280)
        cy = y0 + 50 if i < 4 else (y0 + 160)
        # Tam giác mũi tên tái chế
        vth.draw_rounded_rect(draw, [cx - 30, cy - 30, cx + 30, cy + 30], radius=10, fill=(241, 245, 249), outline=(147, 51, 234), width=2)
        draw.text((cx, cy), code, fill=(147, 51, 234), font=f_code, anchor="mm")
        draw.text((cx + 42, cy - 8), poly_name, fill=(30, 41, 59), font=f_poly, anchor="lm")
        draw.text((cx + 42, cy + 12), ex_str, fill=(100, 116, 139), font=f_ex, anchor="lm")

def draw_diagram_stem(draw, box):
    x0, y0, x1, y1 = box
    w = x1 - x0
    # Quy trình 5 bước KHKT
    steps = [
        ("1. ĐẶT VẤN ĐỀ", "Xác định mục tiêu & câu hỏi nghiên cứu"),
        ("2. GIẢ THUYẾT", "Xây dựng mô hình & biến số thực nghiệm"),
        ("3. THỰC NGHIỆM", "Thu thập dữ liệu tại phòng thí nghiệm VERA"),
        ("4. PHÂN TÍCH", "Xử lý thống kê & đánh giá sai số"),
        ("5. KẾT LUẬN", "Báo cáo đề tài & triển khai ứng dụng")
    ]
    f_st = vth.get_font(14, bold=True)
    f_sub = vth.get_font(11, bold=False)
    bx_w = (w - 80) // 5
    for i, (st_t, st_d) in enumerate(steps):
        bx0 = x0 + 20 + i * (bx_w + 12)
        bx1 = bx0 + bx_w
        by0 = y0 + 35
        by1 = by0 + 90
        vth.draw_rounded_rect(draw, [bx0, by0, bx1, by1], radius=8, fill=(253, 242, 248), outline=(219, 39, 119), width=2)
        draw.text((bx0 + bx_w // 2, by0 + 22), st_t, fill=(219, 39, 119), font=f_st, anchor="mm")
        # Chia dòng mô tả
        words = st_d.split(" ")
        line1 = " ".join(words[:len(words)//2])
        line2 = " ".join(words[len(words)//2:])
        draw.text((bx0 + bx_w // 2, by0 + 50), line1, fill=(71, 85, 105), font=f_sub, anchor="mm")
        draw.text((bx0 + bx_w // 2, by0 + 68), line2, fill=(71, 85, 105), font=f_sub, anchor="mm")

        if i < 4:
            # Mũi tên liên kết
            arr_x = bx1 + 6
            draw.text((arr_x, by0 + 45), "→", fill=(219, 39, 119), font=vth.get_font(18, bold=True), anchor="mm")

    # Biểu đồ cột minh họa kết quả nghiên cứu
    bar_y0 = y0 + 150
    draw.text((x0 + 30, bar_y0), "KẾT QUẢ ĐỐI CHỨNG VÀ HIỆU SUẤT ĐỀ TÀI (N = 50 LẦN THỬ)", fill=(30, 41, 59), font=vth.get_font(14, bold=True), anchor="lm")
    bar_data = [("Mẫu đối chứng A", 62, (148, 163, 184)), ("Mẫu thử nghiệm B", 84, (244, 114, 182)), ("Phương pháp tối ưu C", 96, (219, 39, 119))]
    for idx, (b_name, b_val, b_col) in enumerate(bar_data):
        by = bar_y0 + 30 + idx * 30
        draw.text((x0 + 30, by), b_name, fill=(71, 85, 105), font=vth.get_font(12, bold=False), anchor="lm")
        draw.rectangle([x0 + 220, by - 8, x0 + 220 + b_val * 6, by + 8], fill=b_col)
        draw.text((x0 + 230 + b_val * 6, by), f"{b_val}%", fill=(30, 41, 59), font=vth.get_font(12, bold=True), anchor="lm")

def generate_poster(pdef):
    W, H = 1024, 1408
    img = Image.new("RGB", (W, H), (248, 250, 252))
    draw = ImageDraw.Draw(img)

    pid = pdef["id"]
    color = pdef["color"]

    # 1. Header
    banner_h = vth.draw_header_banner(draw, W, H, pdef["vi"].upper(), pdef["en"].upper(), color, "DỰ ÁN NGHIÊN CỨU HỌC SINH")

    # 2. Card 1: Giới thiệu & Mục tiêu (Introduction)
    margin = 40
    card1_y0 = banner_h + 30
    card1_h = 230
    draw_card(draw, [margin, card1_y0, W - margin, card1_y0 + card1_h], "1. TỔNG QUAN & MỤC TIÊU NGHIÊN CỨU", color)
    f_body = vth.get_font(16, bold=False)
    intro_lines = [
        f"• Đề tài: {pdef['vi']} ({pdef['en']}).",
        f"• Tóm tắt: {pdef['desc']}",
        "• Mục tiêu thực nghiệm: Khảo sát quy luật hóa lý, đối chiếu lý thuyết SGK với thực tế,",
        "  tính toán hiệu suất phản ứng và đề xuất giải pháp ứng dụng thực tiễn bền vững.",
        "• Địa điểm thực hiện: Phòng Thí Nghiệm Hóa Học Ảo VERA Lab · Thiết bị chuẩn Retina HD."
    ]
    for idx, line in enumerate(intro_lines):
        draw.text((margin + 20, card1_y0 + 55 + idx * 32), line, fill=(51, 65, 85), font=f_body)

    # 3. Card 2: Sơ đồ & Dữ liệu thực nghiệm (Diagram / Experimental Data)
    card2_y0 = card1_y0 + card1_h + 25
    card2_h = 380
    draw_card(draw, [margin, card2_y0, W - margin, card2_y0 + card2_h], "2. SƠ ĐỒ THỰC NGHIỆM & KẾT QUẢ ĐO ĐẠC", color)

    dtype = pdef["diagram_type"]
    diag_box = [margin + 15, card2_y0 + 45, W - margin - 15, card2_y0 + card2_h - 15]
    if dtype == "ph_scale":
        draw_diagram_ph(draw, diag_box)
    elif dtype == "galvanic_cell":
        draw_diagram_galvanic(draw, diag_box)
    elif dtype == "green_chem":
        draw_diagram_green_chem(draw, diag_box)
    elif dtype == "trends":
        draw_diagram_trends(draw, diag_box)
    elif dtype == "kinetics":
        draw_diagram_kinetics(draw, diag_box)
    elif dtype == "polymers":
        draw_diagram_polymers(draw, diag_box)
    elif dtype == "stem_fair":
        draw_diagram_stem(draw, diag_box)

    # 4. Card 3: Thảo luận & Kết luận (Discussion & Conclusion)
    card3_y0 = card2_y0 + card2_h + 25
    card3_h = 240
    draw_card(draw, [margin, card3_y0, W - margin, card3_y0 + card3_h], "3. THẢO LUẬN KẾT QUẢ & KẾT LUẬN", color)
    conclusion_lines = [
        "• Kết quả thực nghiệm hoàn toàn khớp với dự đoán lý thuyết với độ tin cậy cao (p < 0.05).",
        "• Sai số ngẫu nhiên được kiểm soát chặt chẽ qua n = 5 lần lặp lại độc lập.",
        "• Góp phần giáo dục tư duy STEM, khuyến khích học sinh tự khám phá thế giới tự nhiên.",
        "• Tác giả: Nhóm học sinh nghiên cứu VERA Lab · Giáo viên hướng dẫn: TS. Trần Trọng."
    ]
    for idx, line in enumerate(conclusion_lines):
        draw.text((margin + 20, card3_y0 + 58 + idx * 38), line, fill=(51, 65, 85), font=f_body)

    # 5. Footer
    vth.draw_footer_banner(draw, W, H, f"VERA LAB · HỘI THI KHOA HỌC KỸ THUẬT QUỐC GIA 2026 · POSTER {pid}")

    out_file = os.path.join(vth.TEXTURES_DIR, f"Poster{pid}.png")
    img.save(out_file, "PNG", optimize=True)
    print(f"Đã tạo poster: {out_file} (1024x1408)")

def generate_all_posters():
    for pdef in POSTER_DEFS:
        generate_poster(pdef)

if __name__ == "__main__":
    generate_all_posters()
