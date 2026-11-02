#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Tạo các ảnh đối chứng âm (Negative Controls) để kiểm thử tính nghiêm ngặt của do_anh.py:
1. anh_hong_tim.png: Màu hồng tím thiếu shader (Magenta)
2. anh_lam_mo.png: Ảnh mờ nhòe nặng (Laplacian variance cực thấp)
3. anh_tang_bao_hoa.png: Ảnh bão hòa màu quá cao, chói neon
4. anh_toan_den.png: Toàn màu đen (100% điểm đen)
"""

import os
import cv2
import numpy as np

def tao_doi_chung_am(output_dir):
    os.makedirs(output_dir, exist_ok=True)
    w, h = 1600, 900

    # 1. Ảnh hồng tím (Missing shader)
    # RGB: (255, 0, 255) -> BGR: (255, 0, 255)
    img_magenta = np.zeros((h, w, 3), dtype=np.uint8)
    img_magenta[:, :] = (255, 0, 255)
    cv2.imwrite(os.path.join(output_dir, "anh_hong_tim.png"), img_magenta)

    # 2. Ảnh làm mờ (Blurry image)
    # Tạo ảnh có chi tiết rồi làm mờ cực nặng
    img_blur = np.random.randint(50, 200, (h, w, 3), dtype=np.uint8)
    img_blur = cv2.GaussianBlur(img_blur, (101, 101), 50)
    cv2.imwrite(os.path.join(output_dir, "anh_lam_mo.png"), img_blur)

    # 3. Ảnh tăng bão hòa (Oversaturated)
    # HSV: Hue đa dạng, Saturation = 255, Value = 255
    hsv_sat = np.zeros((h, w, 3), dtype=np.uint8)
    # gradient hue
    x_coords = np.linspace(0, 179, w, dtype=np.uint8)
    hsv_sat[:, :, 0] = np.tile(x_coords, (h, 1))
    hsv_sat[:, :, 1] = 255
    hsv_sat[:, :, 2] = 255
    img_sat = cv2.cvtColor(hsv_sat, cv2.COLOR_HSV2BGR)
    cv2.imwrite(os.path.join(output_dir, "anh_tang_bao_hoa.png"), img_sat)

    # 4. Ảnh toàn đen (All black)
    img_black = np.zeros((h, w, 3), dtype=np.uint8)
    cv2.imwrite(os.path.join(output_dir, "anh_toan_den.png"), img_black)

    print(f"Đã tạo 4 ảnh đối chứng âm tại: {output_dir}")

if __name__ == "__main__":
    out_dir = os.path.join(os.path.dirname(__file__), "doi_chung_am")
    tao_doi_chung_am(out_dir)
