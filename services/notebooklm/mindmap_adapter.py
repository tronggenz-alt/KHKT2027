"""Chuyển đổi dữ liệu tài khoản và NotebookLM sang cấu trúc node của Sơ đồ tư duy (Mindmap).

Cung cấp cấu trúc phân cấp linh hoạt:
- Chế độ 1 tài khoản: Nhánh NotebookLM → Danh sách Notebooks → Nguồn tài liệu & Ghi chú
- Chế độ "Hiện tất cả tài khoản": Nhánh NotebookLM → Các tài khoản (phân màu) → Notebooks → Nguồn & Ghi chú
"""
from __future__ import annotations

import logging
from typing import Any

from .manager import (
    ds_tai_khoan,
    lay_chi_tiet_notebook,
    lay_danh_sach_cache_notebooks,
    lay_tai_khoan,
    lay_tai_khoan_mac_dinh,
)

log = logging.getLogger(__name__)

# Bảng màu đại diện cho các tài khoản Google khác nhau trong mindmap
MAU_TAI_KHOAN = [
    "#38BDF8",  # Xanh dương sáng (Sky)
    "#34D399",  # Xanh ngọc (Emerald)
    "#F472B6",  # Hồng (Pink)
    "#FBBF24",  # Vàng hổ phách (Amber)
    "#A78BFA",  # Tím (Purple)
    "#FB923C",  # Cam (Orange)
    "#818CF8",  # Xanh chàm (Indigo)
    "#2DD4BF",  # Mòng két (Teal)
]

BIEU_TUONG_NGUON = {
    "pdf": "pdf",
    "web": "web",
    "youtube": "youtube",
    "doc": "doc",
    "text": "text",
    "note": "note",
}


def _tao_node_nguon(src: dict[str, Any], acc_id: int, nb_id: str) -> dict:
    loai = src.get("type", "text").lower()
    return {
        "id": f"nlm_src_{acc_id}_{nb_id}_{src.get('id', '0')[:8]}",
        "ten": src.get("title", "Tài liệu"),
        "loai_nguon": loai,
        "bieu_tuong": BIEU_TUONG_NGUON.get(loai, "text"),
        "is_source": True,
        "url": src.get("url", ""),
        "khong_ai": True,
    }


def _tao_node_notebook(nb: dict[str, Any], acc_id: int, mau_acc: str) -> dict:
    sources = nb.get("sources", [])
    notes = nb.get("notes", [])
    con = []

    # Thêm các nguồn tài liệu
    for s in sources:
        con.append(_tao_node_nguon(s, acc_id, nb["notebook_id"]))

    # Thêm các ghi chú
    for n in notes:
        con.append({
            "id": f"nlm_note_{acc_id}_{nb['notebook_id']}_{n.get('id', '0')[:8]}",
            "ten": n.get("title", "Ghi chú"),
            "loai_nguon": "note",
            "bieu_tuong": "note",
            "is_note": True,
            "khong_ai": True,
        })

    return {
        "id": f"nlm_nb_{acc_id}_{nb['notebook_id']}",
        "notebook_id": nb["notebook_id"],
        "account_id": acc_id,
        "ten": nb.get("title", "Notebook"),
        "description": nb.get("description", ""),
        "so_nguon": nb.get("so_nguon", len(sources)),
        "so_note": len(notes),
        "is_notebook": True,
        "mau": mau_acc,
        "con": con,
    }


def lay_nhanh_mindmap(account_id: int | None = None, tat_ca: bool = False) -> dict:
    """Tạo cấu trúc nhánh NotebookLM cho Mindmap."""
    tai_khoan_list = ds_tai_khoan()
    if not tai_khoan_list:
        return {
            "id": "notebooklm",
            "ten": "NotebookLM",
            "mau": "#4285F4",
            "bieu_tuong": "notebooklm",
            "phia": "trai",
            "mo_ta": "Google NotebookLM · Chưa có tài khoản nào được kết nối",
            "status": "chua_co_tai_khoan",
            "danh_sach_tai_khoan": [],
            "tai_khoan_hien_tai": None,
            "con": [{
                "id": "nlm_chua_co",
                "ten": "Chưa kết nối tài khoản Google",
                "mo_ta": "Vào Admin → NotebookLM để thêm tài khoản",
                "khong_ai": True,
                "con": [],
            }],
        }

    # Chế độ xem TẤT CẢ tài khoản
    if tat_ca:
        con_accounts = []
        for i, acc in enumerate(tai_khoan_list):
            color = MAU_TAI_KHOAN[i % len(MAU_TAI_KHOAN)]
            nbs = lay_danh_sach_cache_notebooks(acc["id"])
            con_nbs = [_tao_node_notebook(nb, acc["id"], color) for nb in nbs]

            con_accounts.append({
                "id": f"nlm_acc_{acc['id']}",
                "account_id": acc["id"],
                "ten": acc["ten_goi_nho"],
                "email": acc.get("email", ""),
                "picture": acc.get("picture", ""),
                "status": acc.get("status", "dang_hoat_dong"),
                "is_account": True,
                "mau": color,
                "con": con_nbs or [{
                    "id": f"nlm_empty_{acc['id']}",
                    "ten": "(Chưa có notebook nào)",
                    "khong_ai": True,
                    "con": [],
                }],
            })

        return {
            "id": "notebooklm",
            "ten": "NotebookLM",
            "mau": "#4285F4",
            "bieu_tuong": "notebooklm",
            "phia": "trai",
            "mo_ta": f"Google NotebookLM · Xem tất cả {len(tai_khoan_list)} tài khoản",
            "status": "ok",
            "che_do": "tat_ca",
            "danh_sach_tai_khoan": tai_khoan_list,
            "tai_khoan_hien_tai": "tat_ca",
            "con": con_accounts,
        }

    # Chế độ xem 1 tài khoản cụ thể (hoặc tài khoản mặc định)
    cur_acc = None
    if account_id:
        cur_acc = lay_tai_khoan(account_id)
    if not cur_acc:
        cur_acc = lay_tai_khoan_mac_dinh() or tai_khoan_list[0]

    color = MAU_TAI_KHOAN[0]
    nbs = lay_danh_sach_cache_notebooks(cur_acc["id"])
    con_nbs = [_tao_node_notebook(nb, cur_acc["id"], color) for nb in nbs]

    return {
        "id": "notebooklm",
        "ten": "NotebookLM",
        "mau": "#4285F4",
        "bieu_tuong": "notebooklm",
        "phia": "trai",
        "mo_ta": f"Google NotebookLM · {cur_acc['ten_goi_nho']} ({cur_acc.get('email', 'chưa rõ email')})",
        "status": cur_acc.get("status", "dang_hoat_dong"),
        "error_msg": cur_acc.get("error_msg", ""),
        "che_do": "mot",
        "danh_sach_tai_khoan": tai_khoan_list,
        "tai_khoan_hien_tai": cur_acc["id"],
        "cur_account": cur_acc,
        "con": con_nbs or [{
            "id": f"nlm_empty_{cur_acc['id']}",
            "ten": "(Chưa có notebook nào)",
            "mo_ta": "Bấm 'Đồng bộ lại' trong Admin hoặc thêm notebook trên web",
            "khong_ai": True,
            "con": [],
        }],
    }
