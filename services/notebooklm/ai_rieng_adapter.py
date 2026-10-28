"""Tích hợp NotebookLM với AI Riêng (Gia sư Hoá Hữu Cơ).

1. Nhập toàn bộ tài liệu nguồn / ghi chú của một NotebookLM vào kho tri thức AI Riêng (ar_tai_lieu & ar_doan).
2. Cho phép AI Riêng truy vấn trực tiếp NotebookLM khi giải chuỗi phản ứng để bổ sung ngữ cảnh chuyên sâu.
"""
from __future__ import annotations

import logging
from typing import Any

from backend.ai_rieng import kho as ai_kho
from backend.ai_rieng import xu_ly as ai_xu_ly
from .manager import (
    hoi_notebook,
    lay_chi_tiet_notebook,
    lay_tai_khoan,
    lay_tai_khoan_mac_dinh,
)

log = logging.getLogger(__name__)


def nhap_notebook_vao_ai_rieng(account_id: int, notebook_id: str, chu_de: str = "NotebookLM", dong_bo_tu_dong: bool = False) -> dict[str, Any]:
    """Nhập nội dung nguồn và ghi chú từ NotebookLM vào kho tri thức của AI Riêng."""
    acc = lay_tai_khoan(account_id)
    if not acc:
        raise ValueError(f"Không tìm thấy tài khoản NotebookLM #{account_id}")

    nb = lay_chi_tiet_notebook(account_id, notebook_id)
    if not nb:
        raise ValueError(f"Không tìm thấy notebook {notebook_id}")

    ten_nb = nb.get("title") or f"Notebook {notebook_id[:8]}"
    email = acc.get("email") or "Google"
    ten_tl = f"[NotebookLM · {acc['ten_goi_nho']}] {ten_nb}"
    nguon_text = f"NotebookLM · {acc['ten_goi_nho']} ({email}) · ID: {notebook_id}"

    # Tạo các đoạn văn bản (paragraphs) từ các nguồn và ghi chú
    doan_list: list[tuple[int | None, str]] = []

    # 1. Đoạn tổng quan
    mo_ta = nb.get("description", "").strip()
    intro = f"Tài liệu trích từ NotebookLM: {ten_nb}.\nTài khoản: {acc['ten_goi_nho']} ({email})."
    if mo_ta:
        intro += f"\nMô tả: {mo_ta}"
    doan_list.append((None, intro))

    # 2. Danh sách các nguồn tài liệu
    sources = nb.get("sources", [])
    if sources:
        ds_ten = [f"- {s.get('title', 'Tài liệu')} (Loại: {s.get('type', 'văn bản')})" for s in sources]
        doan_list.append((None, f"Danh sách tài liệu nguồn trong notebook {ten_nb}:\n" + "\n".join(ds_ten)))

        for s in sources:
            stitle = s.get("title", "Tài liệu")
            stype = s.get("type", "văn bản")
            ssnippet = s.get("snippet", "").strip()
            stext = f"Tài liệu nguồn: {stitle}\nLoại: {stype}"
            if ssnippet:
                stext += f"\nNội dung: {ssnippet}"
            doan_list.append((None, stext))

    # 3. Danh sách các ghi chú trong notebook
    notes = nb.get("notes", [])
    for n in notes:
        ntitle = n.get("title", "Ghi chú")
        ncontent = n.get("content", "").strip()
        doan_list.append((None, f"Ghi chú trong NotebookLM: {ntitle}\n{ncontent}"))

    # Thêm tài liệu vào kho AI Riêng
    tid = ai_kho.tao_tai_lieu(
        ten=ten_tl,
        loai="link",
        nguon=nguon_text,
        chu_de=chu_de or "NotebookLM",
        co_loi_giai=False,
    )

    # Lưu toàn bộ đoạn văn bản
    so_doan = ai_kho.luu_doan(tid, doan_list)
    ai_kho.cap_nhat_tai_lieu(tid, trang_thai="da_nap", so_doan=so_doan)

    # Đánh thức tiến trình nhúng vector nền cho các đoạn vừa thêm
    try:
        ai_xu_ly.danh_thuc()
    except Exception as e:
        log.warning("Kích hoạt nhúng vector nền thất bại: %s", e)

    return {
        "ok": True,
        "tai_lieu_id": tid,
        "ten": ten_tl,
        "nguon": nguon_text,
        "so_doan": so_doan,
        "notebook_id": notebook_id,
        "account_id": account_id,
    }


def hoi_them_notebooklm(
    arg1: str | int | None = None,
    arg2: str | int | None = None,
    arg3: str | None = None,
    *,
    cau_hoi: str | None = None,
    account_id: int | None = None,
    notebook_id: str | None = None,
) -> str:
    """Truy vấn câu hỏi tới NotebookLM để lấy thêm tri thức phục vụ lời giải của AI Riêng.
    Hỗ trợ linh hoạt các cách gọi:
    1. hoi_them_notebooklm(account_id, notebook_id, cau_hoi)
    2. hoi_them_notebooklm(cau_hoi, account_id, notebook_id)
    """
    if isinstance(arg1, int):
        account_id = account_id or arg1
        notebook_id = notebook_id or (str(arg2) if arg2 is not None else "")
        cau_hoi = cau_hoi or (str(arg3) if arg3 is not None else "")
    elif isinstance(arg1, str) and not arg3 and not isinstance(arg2, str):
        cau_hoi = cau_hoi or arg1
        account_id = account_id or (arg2 if isinstance(arg2, int) else None)
    elif isinstance(arg1, str) and arg2 and arg3:
        if arg1.isdigit():
            account_id = account_id or int(arg1)
            notebook_id = notebook_id or str(arg2)
            cau_hoi = cau_hoi or str(arg3)
        else:
            cau_hoi = cau_hoi or arg1
            account_id = account_id or (int(arg2) if str(arg2).isdigit() else None)
            notebook_id = notebook_id or str(arg3)
    else:
        cau_hoi = cau_hoi or (str(arg1) if arg1 is not None else "")

    cau_hoi = (cau_hoi or "").strip()
    if not cau_hoi:
        return ""

    if not account_id:
        acc = lay_tai_khoan_mac_dinh()
        if not acc:
            return ""
        account_id = acc["id"]

    if not notebook_id:
        from .manager import lay_danh_sach_cache_notebooks
        nbs = lay_danh_sach_cache_notebooks(account_id)
        if not nbs:
            return ""
        notebook_id = nbs[0]["notebook_id"]

    try:
        res = hoi_notebook(account_id, notebook_id, cau_hoi)
        tra_loi = res.get("cau_tra_loi", "").strip()
        return tra_loi
    except Exception as e:
        log.warning("Hỏi thêm NotebookLM thất bại: %s", e)
        return ""
