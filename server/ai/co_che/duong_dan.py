"""Đường dẫn dùng chung của mô-đun cơ chế.

Dữ liệu KÍN (kho mẫu học từ đáp án HSG, báo cáo bộ đo, bộ bài demo) KHÔNG nằm trong kho git công khai mà ở thư mục
riêng trên máy: biến môi trường VERA_DU_LIEU_RIENG, mặc định ~/Developer/VERA_du_lieu_rieng. Cấu trúc:

    <VERA_DU_LIEU_RIENG>/co_che/kho_mau/khung_mau_dap_an.json   kho mẫu (khung vòng đã học)
    <VERA_DU_LIEU_RIENG>/co_che/kho_mau/bao_cao_bo_do.json      báo cáo bộ đo + nguyên tắc vẽ học được (CHỈ ADMIN)
    <VERA_DU_LIEU_RIENG>/co_che/bai_demo/<ma>/bai.json          đề, mục tiêu, câu hỏi kiểm tra…
    <VERA_DU_LIEU_RIENG>/co_che/bai_demo/<ma>/co_che.json       cơ chế theo định dạng co_che.py

Kết quả dựng (.3d.json, .giay.json) ra thư mục build/co_che ở gốc kho (git bỏ qua) hoặc VERA_CO_CHE_BUILD.
Dùng pathlib nên chạy được trên macOS, Windows.
"""
from __future__ import annotations

import json
import os
from functools import lru_cache
from pathlib import Path

GOC_KHO = Path(__file__).resolve().parents[3]          # gốc kho git (chứa server/)
THU_MUC_MA = Path(__file__).resolve().parent


def thu_muc_rieng() -> Path:
    return Path(os.environ.get("VERA_DU_LIEU_RIENG") or Path.home() / "Developer" / "VERA_du_lieu_rieng").expanduser()


def thu_muc_co_che_rieng() -> Path:
    return thu_muc_rieng() / "co_che"


def thu_muc_kho_mau() -> Path:
    return thu_muc_co_che_rieng() / "kho_mau"


def thu_muc_bai_demo() -> Path:
    return thu_muc_co_che_rieng() / "bai_demo"


def thu_muc_build() -> Path:
    return Path(os.environ.get("VERA_CO_CHE_BUILD") or GOC_KHO / "build" / "co_che").expanduser()


def co_du_lieu_rieng() -> bool:
    return thu_muc_bai_demo().is_dir()


@lru_cache(maxsize=1)
def doc_kho_mau() -> dict:
    """Kho mẫu khung vòng; không có (máy khác, CI) → kho rỗng."""
    p = thu_muc_kho_mau() / "khung_mau_dap_an.json"
    try:
        return json.loads(p.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return {"khung": [], "khung_vo": []}


@lru_cache(maxsize=1)
def doc_nguyen_tac_ve() -> dict:
    """Nguyên tắc vẽ (thống kê phong cách) học từ đáp án, lưu trong báo cáo bộ đo; không có → giá trị mặc định hợp lý."""
    mac_dinh = {"ty_le_luoi_30": .9, "vong_dinh_nhon": {"6": .5}, "ty_le_truc_ngang": .7,
                "rong_tren_tong_trung_vi": .6, "lech_nhanh_trung_vi_do": 0}
    p = thu_muc_kho_mau() / "bao_cao_bo_do.json"
    try:
        return json.loads(p.read_text(encoding="utf-8")).get("nguyen_tac_hoc_duoc") or mac_dinh
    except (OSError, ValueError):
        return mac_dinh
