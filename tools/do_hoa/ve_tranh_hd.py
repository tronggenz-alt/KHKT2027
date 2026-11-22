#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
ve_tranh_hd.py - Bộ sinh ảnh độ phân giải cao cho tranh tường, áp phích, bảng tuần hoàn,
biểu tượng cảnh báo GHS, và các chi tiết bề mặt của dự án VERA Lab HD.

Tuân thủ: docs/PROMPT_NANG_CAP_BE_MAT.md (Giai đoạn 5 & Mục 4F).
- Phông chữ: Be Vietnam Pro (Regular & SemiBold).
- Nội dung khoa học chính xác 100%.
- Giữ nguyên tên file và file .meta.
"""

import os
import sys
import math
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.abspath(os.path.join(SCRIPT_DIR, "../.."))
TEXTURES_DIR = os.path.join(PROJECT_ROOT, "Phong thi nghiem ao/Assets/Lab/Textures")
FONTS_DIR = os.path.join(PROJECT_ROOT, "Phong thi nghiem ao/Assets/Resources/GiaoDien/Phong")
NHA_HOA_HOC_DIR = os.path.join(PROJECT_ROOT, "Phong thi nghiem ao/Assets/Lab/NhaHoaHoc")

FONT_REGULAR_PATH = os.path.join(FONTS_DIR, "BeVietnamPro-Regular.ttf")
FONT_SEMIBOLD_PATH = os.path.join(FONTS_DIR, "BeVietnamPro-SemiBold.ttf")

def get_font(size, bold=False):
    path = FONT_SEMIBOLD_PATH if bold else FONT_REGULAR_PATH
    try:
        return ImageFont.truetype(path, int(size))
    except Exception:
        return ImageFont.load_default()

def draw_rounded_rect(draw, box, radius, fill=None, outline=None, width=1):
    x0, y0, x1, y1 = box
    draw.rounded_rectangle([x0, y0, x1, y1], radius=radius, fill=fill, outline=outline, width=width)

def draw_header_banner(draw, width, height, title_vi, title_en, color, subtitle=None):
    # Dải đầu trang trí chuyển sắc nhẹ
    banner_h = int(height * 0.12)
    for y in range(banner_h):
        factor = 1.0 - (y / banner_h) * 0.2
        r = int(color[0] * factor)
        g = int(color[1] * factor)
        b = int(color[2] * factor)
        draw.line([(0, y), (width, y)], fill=(r, g, b))

    # Viền nhấn phía dưới banner
    draw.line([(0, banner_h), (width, banner_h)], fill=(int(color[0]*0.7), int(color[1]*0.7), int(color[2]*0.7)), width=3)

    # Chữ tiêu đề Tiếng Việt
    font_title = get_font(banner_h * 0.36, bold=True)
    draw.text((width // 2, banner_h * 0.32), title_vi, fill=(255, 255, 255), font=font_title, anchor="mm")

    # Chữ phụ đề Tiếng Anh
    sub_text = title_en if not subtitle else f"{title_en} · {subtitle}"
    font_sub = get_font(banner_h * 0.18, bold=False)
    draw.text((width // 2, banner_h * 0.72), sub_text, fill=(235, 245, 255), font=font_sub, anchor="mm")

    return banner_h

def draw_footer_banner(draw, width, height, project_text="VERA LAB · DỰ ÁN KHOA HỌC KỸ THUẬT 2026"):
    footer_h = int(height * 0.04)
    y0 = height - footer_h
    draw.rectangle([0, y0, width, height], fill=(240, 243, 246))
    draw.line([(0, y0), (width, y0)], fill=(210, 218, 226), width=2)
    font_ft = get_font(footer_h * 0.45, bold=False)
    draw.text((width // 2, y0 + footer_h // 2), project_text, fill=(100, 116, 139), font=font_ft, anchor="mm")

print("ve_tranh_hd core module loaded.")
