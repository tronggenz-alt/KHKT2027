#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
ve_bang_tuan_hoan.py - Vẽ Bảng tuần hoàn các nguyên tố hóa học độ phân giải cao 1824 x 1152
Chuẩn IUPAC, đầy đủ 118 nguyên tố, phân loại màu theo khối, số hiệu, ký hiệu, tên gọi và nguyên tử khối.
"""

import os
from PIL import Image, ImageDraw, ImageFont
import ve_tranh_hd as vth

ELEMENTS = [
    # (Z, Sym, Name, Mass, Col, Row, Category)
    (1, "H", "Hiđro", "1.008", 1, 1, "nonmetal"),
    (2, "He", "Heli", "4.003", 18, 1, "noble"),
    (3, "Li", "Liti", "6.94", 1, 2, "alkali"),
    (4, "Be", "Beri", "9.012", 2, 2, "alkaline"),
    (5, "B", "Bo", "10.81", 13, 2, "metalloid"),
    (6, "C", "Cacbon", "12.01", 14, 2, "nonmetal"),
    (7, "N", "Nitơ", "14.01", 15, 2, "nonmetal"),
    (8, "O", "Oxi", "16.00", 16, 2, "nonmetal"),
    (9, "F", "Flo", "19.00", 17, 2, "halogen"),
    (10, "Ne", "Neon", "20.18", 18, 2, "noble"),
    (11, "Na", "Natri", "22.99", 1, 3, "alkali"),
    (12, "Mg", "Magie", "24.31", 2, 3, "alkaline"),
    (13, "Al", "Nhôm", "26.98", 13, 3, "post_trans"),
    (14, "Si", "Silic", "28.09", 14, 3, "metalloid"),
    (15, "P", "Photpho", "30.97", 15, 3, "nonmetal"),
    (16, "S", "Lưu huỳnh", "32.06", 16, 3, "nonmetal"),
    (17, "Cl", "Clo", "35.45", 17, 3, "halogen"),
    (18, "Ar", "Agon", "39.95", 18, 3, "noble"),
    (19, "K", "Kali", "39.10", 1, 4, "alkali"),
    (20, "Ca", "Canxi", "40.08", 2, 4, "alkaline"),
    (21, "Sc", "Scandi", "44.96", 3, 4, "trans"),
    (22, "Ti", "Titan", "47.87", 4, 4, "trans"),
    (23, "V", "Vanadi", "50.94", 5, 4, "trans"),
    (24, "Cr", "Crom", "52.00", 6, 4, "trans"),
    (25, "Mn", "Mangan", "54.94", 7, 4, "trans"),
    (26, "Fe", "Sắt", "55.85", 8, 4, "trans"),
    (27, "Co", "Coban", "58.93", 9, 4, "trans"),
    (28, "Ni", "Niken", "58.69", 10, 4, "trans"),
    (29, "Cu", "Đồng", "63.55", 11, 4, "trans"),
    (30, "Zn", "Kẽm", "65.38", 12, 4, "trans"),
    (31, "Ga", "Gali", "69.72", 13, 4, "post_trans"),
    (32, "Ge", "Gecmani", "72.63", 14, 4, "metalloid"),
    (33, "As", "Asen", "74.92", 15, 4, "metalloid"),
    (34, "Se", "Selen", "78.97", 16, 4, "nonmetal"),
    (35, "Br", "Brom", "79.90", 17, 4, "halogen"),
    (36, "Kr", "Kripton", "83.80", 18, 4, "noble"),
    (37, "Rb", "Rubiđi", "85.47", 1, 5, "alkali"),
    (38, "Sr", "Stronti", "87.62", 2, 5, "alkaline"),
    (39, "Y", "Ytri", "88.91", 3, 5, "trans"),
    (40, "Zr", "Ziriconi", "91.22", 4, 5, "trans"),
    (41, "Nb", "Niobi", "92.91", 5, 5, "trans"),
    (42, "Mo", "Molipđen", "95.95", 6, 5, "trans"),
    (43, "Tc", "Tecnexi", "(98)", 7, 5, "trans"),
    (44, "Ru", "Ruteni", "101.1", 8, 5, "trans"),
    (45, "Rh", "Rodi", "102.9", 9, 5, "trans"),
    (46, "Pd", "Palađi", "106.4", 10, 5, "trans"),
    (47, "Ag", "Bạc", "107.9", 11, 5, "trans"),
    (48, "Cd", "Cadimi", "112.4", 12, 5, "trans"),
    (49, "In", "Inđi", "114.8", 13, 5, "post_trans"),
    (50, "Sn", "Thiếc", "118.7", 14, 5, "post_trans"),
    (51, "Sb", "Antimon", "121.8", 15, 5, "metalloid"),
    (52, "Te", "Telu", "127.6", 16, 5, "metalloid"),
    (53, "I", "Iot", "126.9", 17, 5, "halogen"),
    (54, "Xe", "Xenon", "131.3", 18, 5, "noble"),
    (55, "Cs", "Xesi", "132.9", 1, 6, "alkali"),
    (56, "Ba", "Bari", "137.3", 2, 6, "alkaline"),
    # 57-71: Lanthanides (La-Lu)
    (57, "La", "Lantan", "138.9", 3, 8, "lanthanide"),
    (58, "Ce", "Xeri", "140.1", 4, 8, "lanthanide"),
    (59, "Pr", "Praseođim", "140.9", 5, 8, "lanthanide"),
    (60, "Nd", "Neođim", "144.2", 6, 8, "lanthanide"),
    (61, "Pm", "Prometi", "(145)", 7, 8, "lanthanide"),
    (62, "Sm", "Samari", "150.4", 8, 8, "lanthanide"),
    (63, "Eu", "Europi", "152.0", 9, 8, "lanthanide"),
    (64, "Gd", "Gađolini", "157.3", 10, 8, "lanthanide"),
    (65, "Tb", "Tecbi", "158.9", 11, 8, "lanthanide"),
    (66, "Dy", "Đisprosi", "162.5", 12, 8, "lanthanide"),
    (67, "Ho", "Honmi", "164.9", 13, 8, "lanthanide"),
    (68, "Er", "Erbi", "167.3", 14, 8, "lanthanide"),
    (69, "Tm", "Tuli", "168.9", 15, 8, "lanthanide"),
    (70, "Yb", "Ytecbi", "173.1", 16, 8, "lanthanide"),
    (71, "Lu", "Luteti", "175.0", 17, 8, "lanthanide"),
    (72, "Hf", "Hafni", "178.5", 4, 6, "trans"),
    (73, "Ta", "Tantan", "180.9", 5, 6, "trans"),
    (74, "W", "Vonfram", "183.8", 6, 6, "trans"),
    (75, "Re", "Reni", "186.2", 7, 6, "trans"),
    (76, "Os", "Osimi", "190.2", 8, 6, "trans"),
    (77, "Ir", "Iriđi", "192.2", 9, 6, "trans"),
    (78, "Pt", "Bạch kim", "195.1", 10, 6, "trans"),
    (79, "Au", "Vàng", "197.0", 11, 6, "trans"),
    (80, "Hg", "Thủy ngân", "200.6", 12, 6, "trans"),
    (81, "Tl", "Tali", "204.4", 13, 6, "post_trans"),
    (82, "Pb", "Chì", "207.2", 14, 6, "post_trans"),
    (83, "Bi", "Bismut", "209.0", 15, 6, "post_trans"),
    (84, "Po", "Poloni", "(209)", 16, 6, "metalloid"),
    (85, "At", "Astatin", "(210)", 17, 6, "halogen"),
    (86, "Rn", "Radon", "(222)", 18, 6, "noble"),
    (87, "Fr", "Franxi", "(223)", 1, 7, "alkali"),
    (88, "Ra", "Rađi", "(226)", 2, 7, "alkaline"),
    # 89-103: Actinides (Ac-Lr)
    (89, "Ac", "Actini", "(227)", 3, 9, "actinide"),
    (90, "Th", "Thori", "232.0", 4, 9, "actinide"),
    (91, "Pa", "Protactini", "231.0", 5, 9, "actinide"),
    (92, "U", "Urani", "238.0", 6, 9, "actinide"),
    (93, "Np", "Neptuni", "(237)", 7, 9, "actinide"),
    (94, "Pu", "Plutoni", "(244)", 8, 9, "actinide"),
    (95, "Am", "Amerixi", "(243)", 9, 9, "actinide"),
    (96, "Cm", "Curi", "(247)", 10, 9, "actinide"),
    (97, "Bk", "Bơkeli", "(247)", 11, 9, "actinide"),
    (98, "Cf", "Californi", "(251)", 12, 9, "actinide"),
    (99, "Es", "Einsteini", "(252)", 13, 9, "actinide"),
    (100, "Fm", "Fecmi", "(257)", 14, 9, "actinide"),
    (101, "Md", "Menđelevi", "(258)", 15, 9, "actinide"),
    (102, "No", "Nobeli", "(259)", 16, 9, "actinide"),
    (103, "Lr", "Lorenxi", "(266)", 17, 9, "actinide"),
    (104, "Rf", "Rơngơni", "(267)", 4, 7, "trans"),
    (105, "Db", "Đubni", "(268)", 5, 7, "trans"),
    (106, "Sg", "Sibogi", "(269)", 6, 7, "trans"),
    (107, "Bh", "Bohri", "(270)", 7, 7, "trans"),
    (108, "Hs", "Hassi", "(270)", 8, 7, "trans"),
    (109, "Mt", "Meitneri", "(278)", 9, 7, "trans"),
    (110, "Ds", "Đacmstati", "(281)", 10, 7, "trans"),
    (111, "Rg", "Roentgeni", "(282)", 11, 7, "trans"),
    (112, "Cn", "Copexnici", "(285)", 12, 7, "trans"),
    (113, "Nh", "Nihoni", "(286)", 13, 7, "post_trans"),
    (114, "Fl", "Flerovi", "(289)", 14, 7, "post_trans"),
    (115, "Mc", "Moscovi", "(290)", 15, 7, "post_trans"),
    (116, "Lv", "Lipexmo", "(293)", 16, 7, "post_trans"),
    (117, "Ts", "Tennessin", "(294)", 17, 7, "halogen"),
    (118, "Og", "Oganesson", "(294)", 18, 7, "noble"),
]

COLORS = {
    "alkali":      ((255, 224, 230), (225, 29, 72)),    # Hồng kiềm
    "alkaline":    ((254, 237, 215), (217, 119, 6)),    # Cam kiềm thổ
    "trans":       ((224, 242, 254), (2, 132, 199)),     # Xanh dương chuyển tiếp
    "post_trans":  ((220, 252, 231), (22, 163, 74)),    # Xanh lá hậu chuyển tiếp
    "metalloid":   ((254, 249, 195), (202, 138, 4)),    # Vàng á kim
    "nonmetal":    ((237, 233, 254), (124, 58, 237)),   # Tím phi kim
    "halogen":     ((254, 240, 138), (161, 98, 7)),     # Vàng đậm halogen
    "noble":       ((241, 245, 249), (71, 85, 105)),    # Xám khí hiếm
    "lanthanide":  ((254, 215, 226), (190, 24, 93)),    # Hồng lantan
    "actinide":    ((254, 202, 202), (185, 28, 28)),    # Đỏ actini
}

def generate_periodic_table():
    W, H = 1824, 1152
    img = Image.new("RGB", (W, H), (248, 250, 252))
    draw = ImageDraw.Draw(img)

    # 1. Header Banner
    banner_h = 100
    for y in range(banner_h):
        t = y / banner_h
        r = int(15 + 10 * t)
        g = int(23 + 20 * t)
        b = int(42 + 35 * t)
        draw.line([(0, y), (W, y)], fill=(r, g, b))
    draw.line([(0, banner_h), (W, banner_h)], fill=(14, 116, 144), width=3)

    f_title = vth.get_font(34, bold=True)
    draw.text((W // 2, 38), "BẢNG TUẦN HOÀN CÁC NGUYÊN TỐ HÓA HỌC", fill=(255, 255, 255), font=f_title, anchor="mm")
    f_sub = vth.get_font(18, bold=False)
    draw.text((W // 2, 75), "PERIODIC TABLE OF THE ELEMENTS · TIÊU CHUẨN QUỐC TẾ IUPAC", fill=(203, 213, 225), font=f_sub, anchor="mm")

    # 2. Grid Layout
    left_m = 48
    top_m = 120
    cell_w = 94
    cell_h = 96
    spacing_x = 4
    spacing_y = 4

    # Nhãn số nhóm (Groups 1..18)
    f_group = vth.get_font(13, bold=True)
    for col in range(1, 19):
        gx = left_m + (col - 1) * (cell_w + spacing_x) + cell_w // 2
        draw.text((gx, top_m - 12), str(col), fill=(100, 116, 139), font=f_group, anchor="mm")

    # Nhãn chu kì (Periods 1..7)
    f_period = vth.get_font(14, bold=True)
    for row in range(1, 8):
        py = top_m + (row - 1) * (cell_h + spacing_y) + cell_h // 2
        draw.text((left_m - 20, py), str(row), fill=(100, 116, 139), font=f_period, anchor="mm")

    # 3. Vẽ 118 nguyên tố
    f_z = vth.get_font(13, bold=False)
    f_sym = vth.get_font(28, bold=True)
    f_name = vth.get_font(11, bold=False)
    f_mass = vth.get_font(11, bold=False)

    for (z, sym, name, mass, col, row, cat) in ELEMENTS:
        # Nếu row 8, 9 (Lantan, Actini) dời xuống dưới có khoảng cách
        actual_row = row if row <= 7 else (row + 0.3)
        x0 = left_m + (col - 1) * (cell_w + spacing_x)
        y0 = top_m + (actual_row - 1) * (cell_h + spacing_y)
        x1 = x0 + cell_w
        y1 = y0 + cell_h

        bg_col, border_col = COLORS[cat]
        # Hộp nguyên tố có bo góc
        vth.draw_rounded_rect(draw, [x0, y0, x1, y1], radius=6, fill=bg_col, outline=border_col, width=2)

        # Số hiệu nguyên tử (góc trái trên)
        draw.text((x0 + 8, y0 + 12), str(z), fill=(30, 41, 59), font=f_z, anchor="lm")

        # Ký hiệu hóa học (chính giữa)
        draw.text((x0 + cell_w // 2, y0 + 42), sym, fill=(15, 23, 42), font=f_sym, anchor="mm")

        # Tên nguyên tố (tiếng Việt chuẩn)
        draw.text((x0 + cell_w // 2, y0 + 68), name, fill=(51, 65, 85), font=f_name, anchor="mm")

        # Nguyên tử khối
        draw.text((x0 + cell_w // 2, y0 + 83), mass, fill=(100, 116, 139), font=f_mass, anchor="mm")

    # Khung chú dẫn ở vị trí La-Lu và Ac-Lr trong bảng chính
    for (label, row, col_range) in [("57-71\n* La-Lu", 6, 3), ("89-103\n** Ac-Lr", 7, 3)]:
        x0 = left_m + (col_range - 1) * (cell_w + spacing_x)
        y0 = top_m + (row - 1) * (cell_h + spacing_y)
        x1 = x0 + cell_w
        y1 = y0 + cell_h
        cat = "lanthanide" if "La" in label else "actinide"
        bg_col, border_col = COLORS[cat]
        vth.draw_rounded_rect(draw, [x0, y0, x1, y1], radius=6, fill=bg_col, outline=border_col, width=2)
        f_lbl = vth.get_font(13, bold=True)
        draw.text((x0 + cell_w // 2, y0 + cell_h // 2), label, fill=border_col, font=f_lbl, anchor="mm")

    # Nhãn hàng họ Lantan / Actini
    f_block = vth.get_font(14, bold=True)
    draw.text((left_m + 2 * (cell_w + spacing_x) - 15, top_m + 7.3 * (cell_h + spacing_y) + cell_h // 2), "* Họ Lantan:", fill=COLORS["lanthanide"][1], font=f_block, anchor="rm")
    draw.text((left_m + 2 * (cell_w + spacing_x) - 15, top_m + 8.3 * (cell_h + spacing_y) + cell_h // 2), "** Họ Actini:", fill=COLORS["actinide"][1], font=f_block, anchor="rm")

    # 4. Bảng chú giải (Legend) ở góc trống phía trên (cột 3..12, hàng 1..3)
    leg_x0 = left_m + 2 * (cell_w + spacing_x) + 20
    leg_y0 = top_m + 8
    leg_w = 900
    leg_h = 160
    vth.draw_rounded_rect(draw, [leg_x0, leg_y0, leg_x0 + leg_w, leg_y0 + leg_h], radius=8, fill=(255, 255, 255), outline=(203, 213, 225), width=2)

    f_leg_title = vth.get_font(15, bold=True)
    draw.text((leg_x0 + 20, leg_y0 + 22), "CHÚ THÍCH PHÂN LOẠI NGUYÊN TỐ (IUPAC CATEGORIES)", fill=(15, 23, 42), font=f_leg_title, anchor="lm")

    categories = [
        ("alkali", "Kim loại kiềm"),
        ("alkaline", "Kim loại kiềm thổ"),
        ("trans", "Kim loại chuyển tiếp"),
        ("post_trans", "Kim loại hậu chuyển tiếp"),
        ("metalloid", "Á kim (Bán dẫn)"),
        ("nonmetal", "Phi kim hoạt động"),
        ("halogen", "Halogen (Nhóm 17)"),
        ("noble", "Khí hiếm (Nhóm 18)"),
        ("lanthanide", "Họ Lantan (Đất hiếm)"),
        ("actinide", "Họ Actini (Phóng xạ)")
    ]

    f_leg_item = vth.get_font(12, bold=False)
    for idx, (cat_key, cat_name) in enumerate(categories):
        col_idx = idx // 2
        row_idx = idx % 2
        ix = leg_x0 + 25 + col_idx * 175
        iy = leg_y0 + 58 + row_idx * 45
        bg, border = COLORS[cat_key]
        draw.rectangle([ix, iy - 10, ix + 24, iy + 14], fill=bg, outline=border, width=2)
        draw.text((ix + 34, iy + 2), cat_name, fill=(30, 41, 59), font=f_leg_item, anchor="lm")

    # Ô mẫu giải thích
    sample_x = leg_x0 + leg_w - 140
    sample_y = leg_y0 + 15
    sample_w = 110
    sample_h = 130
    vth.draw_rounded_rect(draw, [sample_x, sample_y, sample_x + sample_w, sample_y + sample_h], radius=6, fill=COLORS["trans"][0], outline=COLORS["trans"][1], width=2)
    draw.text((sample_x + 10, sample_y + 14), "26", fill=(30, 41, 59), font=vth.get_font(12, bold=False), anchor="lm")
    draw.text((sample_x + sample_w // 2, sample_y + 48), "Fe", fill=(15, 23, 42), font=vth.get_font(32, bold=True), anchor="mm")
    draw.text((sample_x + sample_w // 2, sample_y + 80), "Sắt", fill=(51, 65, 85), font=vth.get_font(13, bold=False), anchor="mm")
    draw.text((sample_x + sample_w // 2, sample_y + 102), "55.85", fill=(100, 116, 139), font=vth.get_font(12, bold=False), anchor="mm")

    # Footer
    vth.draw_footer_banner(draw, W, H, "VERA LAB · BẢNG TRA CỨU HÓA HỌC CHUẨN QUỐC GIA 2026 · ĐỘ PHÂN GIẢI CAO RETINA")

    out_path = os.path.join(vth.TEXTURES_DIR, "BangTuanHoan.png")
    img.save(out_path, "PNG", optimize=True)
    print(f"Đã tạo thành công: {out_path} ({W}x{H})")

if __name__ == "__main__":
    generate_periodic_table()
