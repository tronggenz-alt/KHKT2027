"""Nhịp của app: mỗi máy đang mở báo mình đứng ở đâu, đang làm gì, đã hiện và đã nhận tới đâu.

Chỉ giữ trong bộ nhớ, không ghi đĩa: 2 giây một lần mỗi máy thì không đáng ghi, và vị trí của học sinh không nên lưu lại lâu dài.
Máy chủ khởi động lại thì mất, nhịp sau lại có.
"""

from __future__ import annotations

import threading

GIAY_CON_MO = 8  # không thấy nhịp chừng này giây thì coi như máy đã tắt

_khoa = threading.Lock()
_nhip: dict[int, dict] = {}


def ghi(nguoi_dung_id: int, moi: dict) -> None:
    with _khoa:
        _nhip[nguoi_dung_id] = moi


def doc(nguoi_dung_id: int) -> dict:
    with _khoa:
        return dict(_nhip.get(nguoi_dung_id, {}))


def xoa() -> None:
    with _khoa:
        _nhip.clear()
