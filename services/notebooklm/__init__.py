"""Dịch vụ kết nối Google NotebookLM (hỗ trợ đa tài khoản Google và tích hợp Mindmap, AI Riêng).

Được bọc trong một module riêng `services/notebooklm` để dễ dàng nâng cấp hoặc thay thế bằng API chính thức
(NotebookLM Enterprise) trong tương lai mà không ảnh hưởng tới các phần khác của hệ thống.
"""
from __future__ import annotations

from .ai_rieng_adapter import hoi_them_notebooklm, nhap_notebook_vao_ai_rieng
from .client import (
    LoiCookieHetHan,
    LoiNotebookLM,
    NotebookInfo,
    NotebookLMClient,
    QueryResult,
    SourceInfo,
    parse_cookie_input,
)
from .manager import (
    cap_nhat_tai_khoan,
    dat_mac_dinh,
    dong_bo_tai_khoan,
    ds_tai_khoan,
    hoi_notebook,
    kiem_tra_tai_khoan,
    lay_chi_tiet_notebook,
    lay_danh_sach_cache_notebooks,
    lay_tai_khoan,
    lay_tai_khoan_mac_dinh,
    them_tai_khoan,
    xoa_tai_khoan,
)
from .mindmap_adapter import lay_nhanh_mindmap

__all__ = [
    "NotebookLMClient",
    "LoiNotebookLM",
    "LoiCookieHetHan",
    "NotebookInfo",
    "SourceInfo",
    "QueryResult",
    "parse_cookie_input",
    "them_tai_khoan",
    "ds_tai_khoan",
    "lay_tai_khoan",
    "lay_tai_khoan_mac_dinh",
    "cap_nhat_tai_khoan",
    "xoa_tai_khoan",
    "dat_mac_dinh",
    "dong_bo_tai_khoan",
    "kiem_tra_tai_khoan",
    "lay_danh_sach_cache_notebooks",
    "lay_chi_tiet_notebook",
    "hoi_notebook",
    "lay_nhanh_mindmap",
    "nhap_notebook_vao_ai_rieng",
    "hoi_them_notebooklm",
]
