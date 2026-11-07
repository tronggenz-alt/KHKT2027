#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
ve_posters_sinh_hoc.py - Vẽ 4 áp phích sinh học tầng 2:
T2_ApPhichTeBao.png, T2_ApPhichChuKy.png, T2_ApPhichCaySuSong.png, T2_ApPhichADN.png
Độ phân giải 1024 x 1408, font Be Vietnam Pro, nội dung khoa học sinh học chính xác.
"""

import os
import math
from PIL import Image, ImageDraw, ImageFont
import ve_tranh_hd as vth

def draw_card(draw, box, title, color):
    x0, y0, x1, y1 = box
    vth.draw_rounded_rect(draw, [x0, y0, x1, y1], radius=10, fill=(255, 255, 255), outline=(226, 232, 240), width=2)
    th = 36
    draw.rounded_rectangle([x0, y0, x1, y0 + th], radius=8, fill=(color[0], color[1], color[2], 25))
    draw.line([(x0, y0 + th), (x1, y0 + th)], fill=(color[0], color[1], color[2]), width=2)
    f_card = vth.get_font(17, bold=True)
    draw.text((x0 + 16, y0 + th // 2), title, fill=(color[0], color[1], color[2]), font=f_card, anchor="lm")

# -----------------------------------------------------------------------------
# 1. T2_ApPhichTeBao (Cấu trúc tế bào động vật và thực vật)
# -----------------------------------------------------------------------------
def generate_cell_poster():
    W, H = 1024, 1408
    img = Image.new("RGB", (W, H), (248, 250, 252))
    draw = ImageDraw.Draw(img)
    color = (13, 148, 136) # Teal đậm

    banner_h = vth.draw_header_banner(draw, W, H, "CẤU TRÚC TẾ BÀO SINH HỌC", "CELL STRUCTURE & ORGANELLES", color, "TẾ BÀO ĐỘNG VẬT & THỰC VẬT")

    margin = 40

    # Khung 1: Tế bào động vật (Animal Cell)
    c1_y0 = banner_h + 20
    c1_h = 490
    draw_card(draw, [margin, c1_y0, W - margin, c1_y0 + c1_h], "I. CẤU TRÚC TẾ BÀO ĐỘNG VẬT (ANIMAL CELL)", color)

    # Vẽ tế bào động vật dạng elip mềm mại
    cx_a = margin + 280
    cy_a = c1_y0 + 260
    # Màng tế bào
    draw.ellipse([cx_a - 220, cy_a - 170, cx_a + 220, cy_a + 170], fill=(254, 242, 242), outline=(248, 113, 113), width=4)
    # Tế bào chất
    draw.ellipse([cx_a - 212, cy_a - 162, cx_a + 212, cy_a + 162], fill=(255, 247, 237))
    # Nhân tế bào (Nucleus)
    nuc_x = cx_a - 30
    nuc_y = cy_a - 10
    draw.ellipse([nuc_x - 70, nuc_y - 65, nuc_x + 70, nuc_y + 65], fill=(224, 231, 255), outline=(99, 102, 241), width=3)
    # Nhân con (Nucleolus)
    draw.ellipse([nuc_x + 10, nuc_y + 5, nuc_x + 40, nuc_y + 35], fill=(67, 56, 202))
    # Ti thể (Mitochondria) - 3 cái
    for mx, my, ang in [(cx_a + 120, cy_a - 60, 25), (cx_a - 120, cy_a + 80, -35), (cx_a + 90, cy_a + 80, 40)]:
        draw.ellipse([mx - 35, my - 18, mx + 35, my + 18], fill=(254, 215, 170), outline=(234, 88, 12), width=2)
        draw.line([(mx - 20, my), (mx + 20, my)], fill=(234, 88, 12), width=2)
    # Bộ máy Golgi (Golgi apparatus)
    for gy_off in [-15, -5, 5, 15]:
        draw.arc([cx_a - 160, cy_a - 80 + gy_off, cx_a - 80, cy_a - 30 + gy_off], start=200, end=340, fill=(244, 63, 94), width=3)

    # Chú thích nhãn tế bào động vật (bên phải)
    labels_a = [
        ("• Màng sinh chất (Plasma membrane):", "Bảo vệ, trao đổi chất chọn lọc"),
        ("• Tế bào chất (Cytoplasm):", "Môi trường chứa bào quan và chuyển hóa"),
        ("• Nhân (Nucleus):", "Chứa ADN di truyền, điều khiển tế bào"),
        ("• Ti thể (Mitochondria):", "Nhà máy năng lượng tổng hợp ATP"),
        ("• Bộ máy Golgi:", "Hoàn thiện, đóng gói và xuất protein"),
        ("• Lưới nội chất (ER):", "Tổng hợp lipid, protein và giải độc")
    ]
    f_lbl_t = vth.get_font(14, bold=True)
    f_lbl_d = vth.get_font(13, bold=False)
    for idx, (lt, ld) in enumerate(labels_a):
        ly = c1_y0 + 75 + idx * 65
        draw.text((W - margin - 430, ly), lt, fill=(15, 23, 42), font=f_lbl_t)
        draw.text((W - margin - 430, ly + 22), ld, fill=(71, 85, 105), font=f_lbl_d)

    # Khung 2: Tế bào thực vật (Plant Cell)
    c2_y0 = c1_y0 + c1_h + 20
    c2_h = 510
    draw_card(draw, [margin, c2_y0, W - margin, c2_y0 + c2_h], "II. CẤU TRÚC TẾ BÀO THỰC VẬT (PLANT CELL)", color)

    # Vẽ tế bào thực vật dạng hình lục giác / hộp có thành dày
    cx_p = margin + 280
    cy_p = c2_y0 + 265
    # Thành tế bào xenlulozơ (Cell Wall) dày màu xanh lá
    vth.draw_rounded_rect(draw, [cx_p - 220, cy_p - 175, cx_p + 220, cy_p + 175], radius=25, fill=(220, 252, 231), outline=(22, 163, 74), width=7)
    # Màng sinh chất bên trong
    vth.draw_rounded_rect(draw, [cx_p - 208, cy_p - 163, cx_p + 208, cy_p + 163], radius=18, fill=(240, 253, 244), outline=(134, 239, 172), width=2)
    # Không bào trung tâm lớn (Large Central Vacuole)
    draw.ellipse([cx_p - 140, cy_p - 120, cx_p + 60, cy_p + 110], fill=(224, 242, 254), outline=(56, 189, 248), width=3)
    draw.text((cx_p - 40, cy_p - 5), "Không bào trung tâm\n(chứa dịch và sắc tố)", fill=(2, 132, 199), font=vth.get_font(13, bold=True), anchor="mm")
    # Lục lạp (Chloroplasts) - 4 hạt màu xanh lá đậm
    for cl_x, cl_y in [(cx_p + 120, cy_p - 80), (cx_p + 130, cy_p + 70), (cx_p - 160, cy_p - 110), (cx_p - 150, cy_p + 110)]:
        draw.ellipse([cl_x - 30, cl_y - 18, cl_x + 30, cl_y + 18], fill=(34, 197, 94), outline=(21, 128, 61), width=2)
        # Hạt Grana xếp lớp
        for gx in [-12, 0, 12]:
            draw.line([(cl_x + gx, cl_y - 8), (cl_x + gx, cl_y + 8)], fill=(20, 83, 45), width=2)
    # Nhân tế bào dạt về góc
    draw.ellipse([cx_p + 80, cy_p - 15, cx_p + 180, cy_p + 85], fill=(224, 231, 255), outline=(99, 102, 241), width=3)
    draw.text((cx_p + 130, cy_p + 35), "Nhân", fill=(67, 56, 202), font=vth.get_font(13, bold=True), anchor="mm")

    # Chú thích nhãn tế bào thực vật (bên phải)
    labels_p = [
        ("• Thành tế bào (Cell wall):", "Cấu tạo từ xenlulozơ, duy trì hình dạng vững chắc"),
        ("• Không bào trung tâm (Vacuole):", "Duy trì áp suất trương nước, chứa chất dự trữ"),
        ("• Lục lạp (Chloroplast):", "Chứa chất diệp lục, thực hiện quang hợp tạo đường"),
        ("• Khác biệt then chốt:", "Tế bào thực vật có thành cứng & lục lạp,"),
        (" ", "tế bào động vật có trung thể và màng linh hoạt.")
    ]
    for idx, (lt, ld) in enumerate(labels_p):
        ly = c2_y0 + 85 + idx * 75
        draw.text((W - margin - 430, ly), lt, fill=(15, 23, 42), font=f_lbl_t)
        if ld:
            draw.text((W - margin - 430, ly + 22), ld, fill=(71, 85, 105), font=f_lbl_d)

    vth.draw_footer_banner(draw, W, H, "VERA LAB · TÀI LIỆU DẠY HỌC SINH HỌC TẾ BÀO · CHUẨN RETINA HD")
    out_file = os.path.join(vth.TEXTURES_DIR, "T2_ApPhichTeBao.png")
    img.save(out_file, "PNG", optimize=True)
    print(f"Đã tạo: {out_file} (1024x1408)")

# -----------------------------------------------------------------------------
# 2. T2_ApPhichChuKy (Chu kỳ tế bào & phân bào nguyên phân)
# -----------------------------------------------------------------------------
def generate_cycle_poster():
    W, H = 1024, 1408
    img = Image.new("RGB", (W, H), (248, 250, 252))
    draw = ImageDraw.Draw(img)
    color = (2, 132, 199) # Blue URP

    banner_h = vth.draw_header_banner(draw, W, H, "CHU KỲ TẾ BÀO VÀ PHÂN BÀO", "THE CELL CYCLE & MITOSIS", color, "KỲ TRUNG GIAN & PHÂN BÀO NGUYÊN PHÂN")
    margin = 40

    # Khung sơ đồ chu kỳ tròn
    c_y0 = banner_h + 20
    c_h = 700
    draw_card(draw, [margin, c_y0, W - margin, c_y0 + c_h], "SƠ ĐỒ CÁC PHA TRONG CHU KỲ TẾ BÀO", color)

    cx = W // 2
    cy = c_y0 + 360
    r_out = 230
    r_in = 130

    # Vẽ các cung tròn đại diện cho G1, S, G2, M
    # Góc theo độ: G1 (0..140), S (140..250), G2 (250..315), M (315..360)
    phases = [
        (315, 360, (239, 68, 68), "Pha M (Nguyên phân)", "Phân chia nhân & TBC"),
        (0, 140, (34, 197, 94), "Pha G₁ (Sinh trưởng)", "Tổng hợp protein, bào quan"),
        (140, 250, (59, 130, 246), "Pha S (Nhân đôi)", "Nhân đôi ADN & NST"),
        (250, 315, (234, 179, 8), "Pha G₂ (Chuẩn bị)", "Kiểm tra sai sót ADN")
    ]

    # Vẽ vành khuyên màu
    for a_start, a_end, col, p_title, p_sub in phases:
        for deg in range(a_start, a_end):
            rad = (deg - 90) * math.pi / 180.0
            x_outer = cx + math.cos(rad) * r_out
            y_outer = cy + math.sin(rad) * r_out
            x_inner = cx + math.cos(rad) * r_in
            y_inner = cy + math.sin(rad) * r_in
            draw.line([(x_inner, y_inner), (x_outer, y_outer)], fill=col, width=3)

    # Vẽ tâm trung tâm
    draw.ellipse([cx - r_in + 5, cy - r_in + 5, cx + r_in - 5, cy + r_in - 5], fill=(255, 255, 255), outline=(203, 213, 225), width=2)
    draw.text((cx, cy - 25), "KỲ TRUNG GIAN", fill=(30, 41, 59), font=vth.get_font(18, bold=True), anchor="mm")
    draw.text((cx, cy + 5), "(Chiếm ~90% chu kỳ)", fill=(100, 116, 139), font=vth.get_font(14, bold=False), anchor="mm")
    draw.text((cx, cy + 30), "INTERPHASE", fill=(71, 85, 105), font=vth.get_font(13, bold=True), anchor="mm")

    # Điểm kiểm soát Checkpoints
    f_cp = vth.get_font(12, bold=True)
    draw.rectangle([cx + 170, cy - 100, cx + 290, cy - 65], fill=(254, 226, 226), outline=(239, 68, 68), width=2)
    draw.text((cx + 230, cy - 82), "Checkpoint G₁/S", fill=(185, 28, 28), font=f_cp, anchor="mm")

    draw.rectangle([cx - 290, cy - 100, cx - 170, cy - 65], fill=(254, 240, 138), outline=(202, 138, 4), width=2)
    draw.text((cx - 230, cy - 82), "Checkpoint G₂/M", fill=(161, 98, 7), font=f_cp, anchor="mm")

    # Nhãn các pha xung quanh vành khuyên
    f_p_t = vth.get_font(16, bold=True)
    f_p_s = vth.get_font(13, bold=False)
    # Pha M (đỉnh trên)
    draw.text((cx, cy - r_out - 20), "PHA M (MITOSIS)", fill=(220, 38, 38), font=f_p_t, anchor="mm")
    draw.text((cx, cy - r_out - 40), "Kì đầu → Kì giữa → Kì sau → Kì cuối & Phân chia tế bào chất", fill=(100, 116, 139), font=f_p_s, anchor="mm")

    # Pha G1 (bên phải)
    draw.text((cx + r_out + 20, cy + 50), "PHA G₁", fill=(21, 128, 61), font=f_p_t, anchor="lm")
    draw.text((cx + r_out + 20, cy + 75), "Tế bào sinh trưởng", fill=(71, 85, 105), font=f_p_s, anchor="lm")
    draw.text((cx + r_out + 20, cy + 95), "Gia tăng thể tích & protein", fill=(100, 116, 139), font=f_p_s, anchor="lm")

    # Pha S (bên dưới)
    draw.text((cx, cy + r_out + 25), "PHA S (SYNTHESIS)", fill=(2, 132, 199), font=f_p_t, anchor="mm")
    draw.text((cx, cy + r_out + 48), "Nhân đôi ADN & Nhiễm sắc thể kép", fill=(71, 85, 105), font=f_p_s, anchor="mm")

    # Pha G2 (bên trái)
    draw.text((cx - r_out - 20, cy - 20), "PHA G₂", fill=(161, 98, 7), font=f_p_t, anchor="rm")
    draw.text((cx - r_out - 20, cy + 5), "Chuẩn bị thoi phân bào", fill=(71, 85, 105), font=f_p_s, anchor="rm")
    draw.text((cx - r_out - 20, cy + 25), "Sửa sai hỏng gen", fill=(100, 116, 139), font=f_p_s, anchor="rm")

    # Khung tóm tắt 4 kỳ nguyên phân
    c2_y0 = c_y0 + c_h + 20
    c2_h = 300
    draw_card(draw, [margin, c2_y0, W - margin, c2_y0 + c2_h], "CÁC KỲ CỦA NGUYÊN PHÂN (MITOSIS)", color)

    stages = [
        ("KÌ ĐẦU (Prophase)", "Nhiễm sắc thể co xoắn,\nmàng nhân biến mất,\nthoi vô sắc hình thành."),
        ("KÌ GIỮA (Metaphase)", "NST kép co ngắn cực đại,\nxếp thành 1 hàng trên\nmặt phẳng xích đạo."),
        ("KÌ SAU (Anaphase)", "Từng NST kép tách ở tâm động\nthành 2 NST đơn đi về\nhai cực tế bào."),
        ("KÌ CUỐI (Telophase)", "Màng nhân tái lập, NST dãn xoắn,\nphân chia tế bào chất\ntạo 2 tế bào con giống mẹ.")
    ]
    f_st_t = vth.get_font(14, bold=True)
    f_st_d = vth.get_font(12, bold=False)
    box_w = (W - 2 * margin - 50) // 4
    for idx, (st_t, st_d) in enumerate(stages):
        bx0 = margin + 15 + idx * (box_w + 10)
        by0 = c2_y0 + 55
        vth.draw_rounded_rect(draw, [bx0, by0, bx0 + box_w, by0 + 210], radius=8, fill=(248, 250, 252), outline=(226, 232, 240), width=2)
        draw.text((bx0 + box_w // 2, by0 + 24), st_t, fill=(2, 132, 199), font=f_st_t, anchor="mm")
        # Chia dòng nội dung
        for line_idx, line_str in enumerate(st_d.split("\n")):
            draw.text((bx0 + box_w // 2, by0 + 65 + line_idx * 24), line_str, fill=(51, 65, 85), font=f_st_d, anchor="mm")

    vth.draw_footer_banner(draw, W, H, "VERA LAB · TÀI LIỆU DẠY HỌC SINH HỌC TẾ BÀO · CHUẨN RETINA HD")
    out_file = os.path.join(vth.TEXTURES_DIR, "T2_ApPhichChuKy.png")
    img.save(out_file, "PNG", optimize=True)
    print(f"Đã tạo: {out_file} (1024x1408)")

# -----------------------------------------------------------------------------
# 3. T2_ApPhichCaySuSong (Cây phát sinh sự sống 3 lãnh giới)
# -----------------------------------------------------------------------------
def generate_tree_of_life_poster():
    W, H = 1024, 1408
    img = Image.new("RGB", (W, H), (248, 250, 252))
    draw = ImageDraw.Draw(img)
    color = (21, 128, 61) # Green

    banner_h = vth.draw_header_banner(draw, W, H, "CÂY PHÁT SINH SỰ SỐNG", "THE PHYLOGENETIC TREE OF LIFE", color, "HỆ THỐNG 3 LÃNH GIỚI (CARL WOESE, 1990)")
    margin = 40

    c_y0 = banner_h + 20
    c_h = 1020
    draw_card(draw, [margin, c_y0, W - margin, c_y0 + c_h], "TIẾN HÓA VÀ CÁC NHÁNH SINH GIỚI TỪ TỔ TIÊN CHUNG (LUCA)", color)

    # Thân cây chính ở đáy: LUCA
    cx_root = W // 2
    cy_root = c_y0 + c_h - 100
    draw.rectangle([cx_root - 14, cy_root - 100, cx_root + 14, cy_root], fill=(120, 53, 15))
    draw.ellipse([cx_root - 55, cy_root - 20, cx_root + 55, cy_root + 40], fill=(254, 243, 199), outline=(217, 119, 6), width=3)
    draw.text((cx_root, cy_root + 10), "LUCA\n(Tổ tiên chung)", fill=(180, 83, 9), font=vth.get_font(13, bold=True), anchor="mm")

    # Ba nhánh lớn:
    # 1. Vi khuẩn (Bacteria) rẽ sang trái
    # 2. Cổ khuẩn (Archaea) ở giữa
    # 3. Sinh vật nhân thực (Eukaryota) rẽ sang phải
    b_cx = margin + 170
    a_cx = W // 2
    e_cx = W - margin - 170
    branch_top_y = c_y0 + 160

    # Vẽ đường thân cây cong mượt mà
    # Nhánh Vi khuẩn
    draw.line([(cx_root, cy_root - 100), (b_cx, branch_top_y + 140)], fill=(2, 132, 199), width=8)
    # Nhánh Cổ khuẩn
    draw.line([(cx_root, cy_root - 100), (a_cx, branch_top_y + 140)], fill=(234, 88, 12), width=8)
    # Nhánh Nhân thực
    draw.line([(a_cx, branch_top_y + 240), (e_cx, branch_top_y + 140)], fill=(22, 163, 74), width=8)

    # Khung Lãnh giới Vi khuẩn (Bacteria)
    vth.draw_rounded_rect(draw, [margin + 15, branch_top_y - 80, margin + 285, branch_top_y + 130], radius=10, fill=(239, 246, 255), outline=(59, 130, 246), width=3)
    draw.text((margin + 150, branch_top_y - 50), "LÃNH GIỚI VI KHUẨN\n(BACTERIA)", fill=(29, 78, 216), font=vth.get_font(15, bold=True), anchor="mm")
    bacteria_sub = ["• Vi khuẩn Lam (Cyanobacteria)", "• Vi khuẩn Gram dương / âm", "• Xoắn khuẩn Spirochaetes", "• Proteobacteria"]
    for idx, b_item in enumerate(bacteria_sub):
        draw.text((margin + 30, branch_top_y + idx * 26), b_item, fill=(30, 41, 59), font=vth.get_font(13, bold=False))

    # Khung Lãnh giới Cổ khuẩn (Archaea)
    vth.draw_rounded_rect(draw, [a_cx - 135, branch_top_y - 80, a_cx + 135, branch_top_y + 130], radius=10, fill=(255, 247, 237), outline=(249, 115, 22), width=3)
    draw.text((a_cx, branch_top_y - 50), "LÃNH GIỚI CỔ KHUẨN\n(ARCHAEA)", fill=(194, 65, 12), font=vth.get_font(15, bold=True), anchor="mm")
    archaea_sub = ["• Cổ khuẩn ưa nhiệt (Thermophile)", "• Cổ khuẩn ưa mặn (Halophile)", "• Cổ khuẩn sinh metan", "• Thành tế bào không Peptidoglycan"]
    for idx, a_item in enumerate(archaea_sub):
        draw.text((a_cx - 120, branch_top_y + idx * 26), a_item, fill=(30, 41, 59), font=vth.get_font(13, bold=False))

    # Khung Lãnh giới Sinh vật nhân thực (Eukaryota)
    vth.draw_rounded_rect(draw, [e_cx - 145, branch_top_y - 80, e_cx + 145, branch_top_y + 240], radius=10, fill=(240, 253, 244), outline=(34, 197, 94), width=3)
    draw.text((e_cx, branch_top_y - 50), "LÃNH GIỚI NHÂN THỰC\n(EUKARYOTA)", fill=(21, 128, 61), font=vth.get_font(15, bold=True), anchor="mm")
    euk_sub = [
        ("1. GIỚI ĐỘNG VẬT (Animalia):", "Đa bào, dị dưỡng, chuyển động"),
        ("2. GIỚI THỰC VẬT (Plantae):", "Quang hợp tự dưỡng, vách xenlulozơ"),
        ("3. GIỚI NẤM (Fungi):", "Dị dưỡng hoại sinh, vách kitin"),
        ("4. NGUYÊN SINH VẬT (Protista):", "Tảo đơn bào, trùng roi, amip")
    ]
    for idx, (et, ed) in enumerate(euk_sub):
        draw.text((e_cx - 130, branch_top_y - 5 + idx * 56), et, fill=(22, 101, 52), font=vth.get_font(13, bold=True))
        draw.text((e_cx - 130, branch_top_y + 18 + idx * 56), ed, fill=(71, 85, 105), font=vth.get_font(12, bold=False))

    # Hộp Thuyết nội cộng sinh (Endosymbiosis Theory)
    endo_y = c_y0 + 580
    vth.draw_rounded_rect(draw, [margin + 20, endo_y, W - margin - 20, endo_y + 180], radius=10, fill=(254, 252, 232), outline=(234, 179, 8), width=2)
    draw.text((margin + 40, endo_y + 25), "★ THUYẾT NỘI CỘNG SINH (ENDOSYMBIOTIC THEORY - LYNN MARGULIS)", fill=(161, 98, 7), font=vth.get_font(15, bold=True))
    endo_lines = [
        "• Ti thể (Mitochondria) có nguồn gốc từ vi khuẩn hiếu khí bị tế bào nhân thực cổ nuốt vào nhưng không tiêu hóa.",
        "• Lục lạp (Chloroplast) có nguồn gốc từ vi khuẩn lam quang hợp (Cyanobacteria).",
        "• Bằng chứng: Ti thể và lục lạp đều có ADN vòng kép riêng, ribosome 70S và màng kép tương tự vi khuẩn."
    ]
    for idx, eline in enumerate(endo_lines):
        draw.text((margin + 40, endo_y + 60 + idx * 36), eline, fill=(51, 65, 85), font=vth.get_font(13, bold=False))

    vth.draw_footer_banner(draw, W, H, "VERA LAB · TÀI LIỆU DẠY HỌC SINH HỌC TIẾN HÓA · CHUẨN RETINA HD")
    out_file = os.path.join(vth.TEXTURES_DIR, "T2_ApPhichCaySuSong.png")
    img.save(out_file, "PNG", optimize=True)
    print(f"Đã tạo: {out_file} (1024x1408)")

# -----------------------------------------------------------------------------
# 4. T2_ApPhichADN (Cấu trúc phân tử ADN xoắn kép)
# -----------------------------------------------------------------------------
def generate_dna_poster():
    W, H = 1024, 1408
    img = Image.new("RGB", (W, H), (248, 250, 252))
    draw = ImageDraw.Draw(img)
    color = (124, 58, 237) # Tím đậm

    banner_h = vth.draw_header_banner(draw, W, H, "CẤU TRÚC PHÂN TỬ ADN", "DNA DOUBLE HELIX STRUCTURE", color, "MÔ HÌNH WATSON - CRICK (1953)")
    margin = 40

    c_y0 = banner_h + 20
    c_h = 750
    draw_card(draw, [margin, c_y0, W - margin, c_y0 + c_h], "SƠ ĐỒ CHUỖI XOẮN KÉP VÀ CÁC CẶP BAZƠ BỔ SUNG", color)

    cx = margin + 280
    dna_y0 = c_y0 + 50
    dna_h = 660

    # Vẽ chuỗi xoắn kép ADN
    steps = 400
    phase_factor = 0.02
    for s in range(steps):
        y = dna_y0 + (s / steps) * dna_h
        phase = s * 0.08
        x1 = cx + math.sin(phase) * 120
        x2 = cx - math.sin(phase) * 120
        depth = math.cos(phase)

        # Cầu nối liên kết hiđro giữa các cặp base
        if s % 12 == 0:
            pair_type = (s // 12) % 2 # 0: A-T (2 H-bonds), 1: G-C (3 H-bonds)
            # Màu cho từng nửa bazơ
            mid_x = (x1 + x2) / 2
            col_left = (239, 68, 68) if pair_type == 0 else (34, 197, 94)
            col_right = (59, 130, 246) if pair_type == 0 else (245, 158, 11)
            draw.line([(x1, y), (mid_x, y)], fill=col_left, width=6)
            draw.line([(mid_x, y), (x2, y)], fill=col_right, width=6)
            # Điểm nối liên kết
            draw.line([(mid_x - 3, y), (mid_x + 3, y)], fill=(255, 255, 255), width=3)

        # Mạch đường - photphat (Sugar-Phosphate Backbone)
        col1 = (147, 51, 234) if depth > 0 else (192, 132, 252)
        col2 = (147, 51, 234) if depth <= 0 else (192, 132, 252)
        draw.ellipse([x1 - 8, y - 8, x1 + 8, y + 8], fill=col1)
        draw.ellipse([x2 - 8, y - 8, x2 + 8, y + 8], fill=col2)

    # Chú thích cấu trúc ADN bên phải
    f_p_t = vth.get_font(15, bold=True)
    f_p_d = vth.get_font(13, bold=False)
    features = [
        ("1. Chiều đối song song (Antiparallel):", "Một mạch có chiều 5' → 3', mạch kia có chiều 3' → 5'."),
        ("2. Khung xương Đường - Photphat:", "Đường đêoxiribôzơ liên kết với gốc photphat ở phía ngoài."),
        ("3. Nguyên tắc bổ sung (Base Pairing):", "• Adenin (A) liên kết Timin (T) bằng 2 liên kết hiđro.\n• Guanin (G) liên kết Xitôzin (C) bằng 3 liên kết hiđro."),
        ("4. Kích thước chu kỳ xoắn:", "• Chiều dài 1 chu kỳ xoắn = 3.4 nm (gồm 10 cặp nucleotit).\n• Đường kính phân tử = 2.0 nm.\n• Khoảng cách giữa 2 cặp base = 0.34 nm.")
    ]
    for idx, (ft, fd) in enumerate(features):
        fy = c_y0 + 75 + idx * 150
        draw.text((W - margin - 430, fy), ft, fill=(109, 40, 217), font=f_p_t)
        for l_idx, line in enumerate(fd.split("\n")):
            draw.text((W - margin - 430, fy + 26 + l_idx * 26), line, fill=(51, 65, 85), font=f_p_d)

    # Chú giải màu bazơ nitơ
    b_leg_y = c_y0 + c_h - 75
    draw.rectangle([W - margin - 430, b_leg_y, W - margin - 20, b_leg_y + 55], fill=(245, 243, 255), outline=(196, 181, 253), width=2)
    bases = [("Adenin (A)", (239, 68, 68)), ("Timin (T)", (59, 130, 246)), ("Guanin (G)", (34, 197, 94)), ("Xitôzin (C)", (245, 158, 11))]
    for b_idx, (b_name, b_col) in enumerate(bases):
        bx = (W - margin - 415) + (b_idx % 2) * 200
        by = b_leg_y + 14 + (b_idx // 2) * 24
        draw.rectangle([bx, by - 6, bx + 16, by + 6], fill=b_col)
        draw.text((bx + 24, by), b_name, fill=(30, 41, 59), font=vth.get_font(12, bold=True), anchor="lm")

    # Khung hóa học Nucleotide ở dưới
    c2_y0 = c_y0 + c_h + 20
    c2_h = 250
    draw_card(draw, [margin, c2_y0, W - margin, c2_y0 + c2_h], "ĐƠN PHÂN NUCLEOTIT VÀ Ý NGHĨA SINH HỌC", color)
    nuc_lines = [
        "• Mỗi nucleotit gồm 3 thành phần: Gốc Axit Photphoric (H₃PO₄), Đường Đêoxiribôzơ (C₅H₁₀O₄) và Bazơ Nitơ (A, T, G, C).",
        "• Hệ quả nguyên tắc bổ sung: Số lượng A = T, G = C; do đó tổng số (A + G) = (T + C).",
        "• Tính đa dạng và đặc thù của ADN được quy định bởi số lượng, thành phần và trật tự sắp xếp của các nucleotit.",
        "• ADN là vật chất mang thông tin di truyền ở cấp độ phân tử của tất cả các sinh vật có cấu tạo tế bào."
    ]
    for idx, n_line in enumerate(nuc_lines):
        draw.text((margin + 20, c2_y0 + 55 + idx * 42), n_line, fill=(51, 65, 85), font=vth.get_font(13, bold=False))

    vth.draw_footer_banner(draw, W, H, "VERA LAB · TÀI LIỆU DẠY HỌC SINH HỌC PHÂN TỬ · CHUẨN RETINA HD")
    out_file = os.path.join(vth.TEXTURES_DIR, "T2_ApPhichADN.png")
    img.save(out_file, "PNG", optimize=True)
    print(f"Đã tạo: {out_file} (1024x1408)")

if __name__ == "__main__":
    generate_cell_poster()
    generate_cycle_poster()
    generate_tree_of_life_poster()
    generate_dna_poster()
