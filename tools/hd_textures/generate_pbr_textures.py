#!/usr/bin/env python3
"""
Bộ sinh texture PBR VERA Lab HD chuẩn hóa (gọi sinh_pbr_sach.py).
Loại bỏ 100% các hàm sin giải tích gây sọc.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from sinh_pbr_sach import generate_all

if __name__ == "__main__":
    generate_all()
