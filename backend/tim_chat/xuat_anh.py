"""Tạo ảnh PNG lời giải chuẩn mực cho bài toán Tìm Cấu Tạo Chất.
Phong cách Giấy & Mực cao cấp, font rõ nét, hiển thị đầy đủ 4 phần:
(1) Bảng dữ kiện, (2) Suy luận, (3) Kiểm tra chéo, (4) Đáp án cuối.
"""
from __future__ import annotations

import io
from pathlib import Path
from typing import Any, Dict, List

from PIL import Image, ImageDraw, ImageFont

from ..config import FONT_BOLD, FONT_REGULAR
from .schemas import KetQuaTimChat

RONG = 1600
LE = 60
NOI_DUNG = RONG - 2 * LE

# Bảng màu trang nhã
MAU_NEN = (255, 255, 255)
MAU_CHU = (20, 24, 33)
MAU_PHU = (100, 110, 130)
MAU_NHAN = (22, 101, 52)      # Xanh lá sẫm
MAU_VIEN = (226, 232, 240)
MAU_NEN_THE = (248, 250, 252)
MAU_XANH_OK = (22, 163, 74)
MAU_DO_NO = (220, 38, 38)
MAU_CAM = (217, 119, 6)


def _lay_font(co: int, dam: bool = False) -> ImageFont.FreeTypeFont:
    ds = FONT_BOLD if dam else FONT_REGULAR
    for f in ds:
        if Path(f).exists():
            try:
                return ImageFont.truetype(f, co)
            except Exception:
                pass
    return ImageFont.load_default()


def _ngat_dong(draw: ImageDraw.ImageDraw, text: str, font: ImageFont.FreeTypeFont, max_w: int) -> List[str]:
    tu_list = text.split()
    dong_list: List[str] = []
    dong_hien_tai: List[str] = []

    for tu in tu_list:
        dong_thu = " ".join(dong_hien_tai + [tu])
        bbox = draw.textbbox((0, 0), dong_thu, font=font)
        w = bbox[2] - bbox[0]
        if w <= max_w:
            dong_hien_tai.append(tu)
        else:
            if dong_hien_tai:
                dong_list.append(" ".join(dong_hien_tai))
                dong_hien_tai = [tu]
            else:
                dong_list.append(tu)
                dong_hien_tai = []
    if dong_hien_tai:
        dong_list.append(" ".join(dong_hien_tai))
    return dong_list or [""]


def tao_anh_loi_giai(kq: KetQuaTimChat) -> bytes:
    """Vẽ toàn bộ lời giải bài toán tìm cấu tạo chất thành ảnh PNG."""
    # Đo đạc chiều cao trước để khởi tạo ảnh vừa vặn
    font_tieu_de = _lay_font(32, dam=True)
    font_nhan = _lay_font(20, dam=True)
    font_chu = _lay_font(18, dam=False)
    font_nho = _lay_font(15, dam=False)
    font_mono = _lay_font(18, dam=True)

    # Ước lượng chiều cao cần thiết
    img_tam = Image.new("RGB", (RONG, 100), MAU_NEN)
    draw_tam = ImageDraw.Draw(img_tam)

    y = 60
    y += 50  # Header
    y += 20

    # Đề bài
    y += 30
    dong_de = _ngat_dong(draw_tam, f"Đề bài: {kq.de_bai}", font_chu, NOI_DUNG - 40)
    y += len(dong_de) * 26 + 30

    # Phần 1: Bảng dữ kiện
    y += 40
    for dk in kq.bang_du_kien:
        dong_dk = _ngat_dong(draw_tam, f"[{dk.id}] {dk.noi_dung_goc}", font_chu, NOI_DUNG - 80)
        y += len(dong_dk) * 24 + 12
    y += 20

    # Phần 2: Suy luận & Tính toán
    y += 40
    for b in kq.cac_buoc_suy_luan:
        y += 28
        dong_sl = _ngat_dong(draw_tam, b.noi_dung, font_chu, NOI_DUNG - 60)
        y += len(dong_sl) * 24 + 16
    y += 20

    # Phần 3: Bảng kiểm tra chéo
    y += 40
    y += 35 + len(kq.bang_kiem_tra) * 45 + 20

    # Phần 4: Đáp án cuối
    y += 40
    y += len(kq.dap_an) * 35 + 80
    y += 80  # Footer

    cao_tong = max(1200, y)

    # Khởi tạo ảnh thật
    img = Image.new("RGB", (RONG, cao_tong), MAU_NEN)
    draw = ImageDraw.Draw(img)

    curr_y = 50

    # 1. HEADER
    draw.text((LE, curr_y), "CHEMISTRY · TÌM CẤU TẠO CHẤT TỪ ĐỀ DÀI", font=font_tieu_de, fill=MAU_NHAN)
    badge_text = f"Độ tin cậy: {kq.do_tin_cay.upper()}"
    b_color = MAU_XANH_OK if kq.do_tin_cay == "cao" else (MAU_CAM if kq.do_tin_cay == "trung_binh" else MAU_DO_NO)
    draw.rectangle((RONG - LE - 220, curr_y + 4, RONG - LE, curr_y + 36), fill=b_color)
    draw.text((RONG - LE - 210, curr_y + 8), badge_text, font=font_nhan, fill=(255, 255, 255))
    curr_y += 60

    # Viền ngăn
    draw.line((LE, curr_y, RONG - LE, curr_y), fill=MAU_VIEN, width=2)
    curr_y += 20

    # Hộp đề bài
    draw.rectangle((LE, curr_y, RONG - LE, curr_y + len(dong_de) * 26 + 24), fill=MAU_NEN_THE, outline=MAU_VIEN, width=1)
    text_y = curr_y + 12
    for line in dong_de:
        draw.text((LE + 16, text_y), line, font=font_chu, fill=MAU_CHU)
        text_y += 26
    curr_y += len(dong_de) * 26 + 40

    # 2. PHẦN 1: BẢNG DỮ KIỆN
    draw.text((LE, curr_y), "1. BẢNG DỮ KIỆN ĐÃ ĐỐI CHIẾU", font=font_nhan, fill=MAU_NHAN)
    curr_y += 32
    for dk in kq.bang_du_kien:
        loai_badge = "[Định lượng]" if dk.loai == "dinh_luong" else "[Định tính]"
        dong_dk = _ngat_dong(draw, f"• {dk.id} {loai_badge}: {dk.noi_dung_goc}", font_chu, NOI_DUNG - 40)
        for line in dong_dk:
            draw.text((LE + 16, curr_y), line, font=font_chu, fill=MAU_CHU)
            curr_y += 24
        curr_y += 6
    curr_y += 20

    # 3. PHẦN 2: SUY LUẬN & TÍNH TOÁN
    draw.text((LE, curr_y), "2. SUY LUẬN BẢN CHẤT HOÁ HỌC (4 BƯỚC →)", font=font_nhan, fill=MAU_NHAN)
    curr_y += 32
    for b in kq.cac_buoc_suy_luan:
        draw.text((LE + 16, curr_y), f"Bước {b.buoc}: {b.tieu_de}", font=font_nhan, fill=MAU_CHU)
        curr_y += 26
        dong_sl = _ngat_dong(draw, b.noi_dung, font_chu, NOI_DUNG - 60)
        for line in dong_sl:
            draw.text((LE + 32, curr_y), line, font=font_chu, fill=MAU_CHU)
            curr_y += 24
        curr_y += 10
    curr_y += 20

    # 4. PHẦN 3: BẢNG KIỂM TRA CHÉO
    draw.text((LE, curr_y), "3. MA TRẬN KIỂM TRA CHÉO DỮ KIỆN", font=font_nhan, fill=MAU_NHAN)
    curr_y += 32

    # Vẽ bảng header
    uv_names = [uv.ten_ung_vien for uv in kq.ung_vien[:3]]  # vẽ tối đa 3 cột ứng viên
    col_dk_w = 400
    col_uv_w = (NOI_DUNG - col_dk_w) // max(1, len(uv_names))

    draw.rectangle((LE, curr_y, RONG - LE, curr_y + 36), fill=MAU_NEN_THE, outline=MAU_VIEN, width=1)
    draw.text((LE + 12, curr_y + 8), "Dữ kiện (ID)", font=font_nhan, fill=MAU_CHU)
    for i, uv_name in enumerate(uv_names):
        draw.text((LE + col_dk_w + i * col_uv_w + 12, curr_y + 8), uv_name, font=font_nhan, fill=MAU_CHU)
    curr_y += 36

    # Hàng dữ liệu kiểm tra chéo
    for row in kq.bang_kiem_tra:
        draw.rectangle((LE, curr_y, RONG - LE, curr_y + 40), fill=MAU_NEN, outline=MAU_VIEN, width=1)
        draw.text((LE + 12, curr_y + 10), row.get("id_du_kien", ""), font=font_mono, fill=MAU_CHU)
        for i, uv_name in enumerate(uv_names):
            res_uv = row.get("ket_qua_ung_vien", {}).get(uv_name, {})
            ok = res_uv.get("thoa_man", True)
            dau = "✓ Thỏa mãn" if ok else "✗ Vi phạm"
            mau_dau = MAU_XANH_OK if ok else MAU_DO_NO
            draw.text((LE + col_dk_w + i * col_uv_w + 12, curr_y + 10), dau, font=font_nhan, fill=mau_dau)
        curr_y += 40
    curr_y += 30

    # 5. PHẦN 4: ĐÁP ÁN CUỐI CÙNG
    draw.rectangle((LE, curr_y, RONG - LE, curr_y + len(kq.dap_an) * 36 + 60), fill=MAU_NEN_THE, outline=MAU_NHAN, width=2)
    draw.text((LE + 20, curr_y + 16), "4. ĐÁP ÁN CẤU TẠO CHUẨN XÁC", font=font_nhan, fill=MAU_NHAN)
    ans_y = curr_y + 48
    for chat, ctct in kq.dap_an.items():
        draw.text((LE + 30, ans_y), f"• Chất {chat}:", font=font_nhan, fill=MAU_CHU)
        draw.text((LE + 180, ans_y), ctct, font=font_mono, fill=MAU_NHAN)
        ans_y += 32
    curr_y += len(kq.dap_an) * 36 + 80

    # Chân trang
    draw.text((LE, curr_y), "CHEMISTRY App · Giải đề Hóa hữu cơ chuẩn mực THPT & HSG", font=font_nho, fill=MAU_PHU)

    # Xuất ra PNG bytes
    buf = io.BytesIO()
    img.save(buf, format="PNG", quality=95)
    return buf.getvalue()
