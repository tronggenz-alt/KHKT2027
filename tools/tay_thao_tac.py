"""Nhận diện thao tác bàn tay từ 21 điểm MediaPipe (hàm thuần, dễ kiểm thử).

Thao tác:
- "chum":      chụm đầu ngón cái với ngón trỏ, các ngón khác không nắm hết (cầm dụng cụ nhỏ, bấm chọn)
- "nam_doc":   nắm tay, lòng bàn tay quay ngang sang bên, ngón cái ở trên (cầm vật cao)
- "nam_ngang": nắm tay, lòng bàn tay úp xuống hoặc ngửa lên (cầm lọ, cốc)
- "ut":        chỉ giơ ngón út (bật, dùng máy)
- "tro":       chỉ giơ ngón trỏ (con trỏ)
- "mo":        xòe cả bàn tay (đặt vật xuống, hủy)
- "khac":      tư thế khác
"""
from __future__ import annotations

import math
from collections import Counter, deque
from typing import Any

EXT_RATIO = 1.12      # ngón duỗi nếu |cổ tay - đầu ngón| > EXT_RATIO * |cổ tay - khớp PIP| (như hand_rects.py)
PINCH_ON = 0.30       # khoảng cách đầu ngón cái - đầu ngón trỏ / cỡ lòng bàn tay: dưới mức này là chụm
PINCH_OFF = 0.42      # trên mức này là thả; hai ngưỡng cách nhau để không chớp tắt
INDEX_REACH = 0.45   # đầu ngón trỏ cách gốc ngón trỏ ít nhất 0,45 cỡ lòng bàn tay (nắm tay thì nhỏ hơn)
NGANG_NGUONG = 0.55   # |thành phần đứng của pháp tuyến lòng bàn tay| trên mức này là nắm ngang


def _p(lm: Any, i: int) -> tuple[float, float, float]:
    q = lm[i]
    if hasattr(q, "x"):
        return float(q.x), float(q.y), float(getattr(q, "z", 0.0))
    return float(q[0]), float(q[1]), float(q[2]) if len(q) > 2 else 0.0


def _d2(lm: Any, a: int, b: int, w: float = 1.0, h: float = 1.0) -> float:
    pa, pb = _p(lm, a), _p(lm, b)
    return math.hypot((pa[0] - pb[0]) * w, (pa[1] - pb[1]) * h)


def ngon_duoi(lm: Any, w: float = 1.0, h: float = 1.0) -> tuple[bool, bool, bool, bool]:
    """(trỏ, giữa, áp út, út) duỗi hay không, đo trên ảnh theo đúng tỉ lệ khung."""
    def ext(tip: int, pip: int) -> bool:
        return _d2(lm, 0, tip, w, h) > EXT_RATIO * _d2(lm, 0, pip, w, h)
    return ext(8, 6), ext(12, 10), ext(16, 14), ext(20, 18)


def ti_le_chum(lm: Any, w: float = 1.0, h: float = 1.0) -> float:
    return _d2(lm, 4, 8, w, h) / (_d2(lm, 0, 9, w, h) + 1e-6)


def phap_tuyen_long_ban_tay(world: Any) -> tuple[float, float, float]:
    """Pháp tuyến lòng bàn tay (đơn vị) từ điểm 3D thế giới: cổ tay 0, gốc ngón trỏ 5, gốc ngón út 17."""
    o, a, b = _p(world, 0), _p(world, 5), _p(world, 17)
    u = (a[0] - o[0], a[1] - o[1], a[2] - o[2])
    v = (b[0] - o[0], b[1] - o[1], b[2] - o[2])
    n = (u[1] * v[2] - u[2] * v[1], u[2] * v[0] - u[0] * v[2], u[0] * v[1] - u[1] * v[0])
    m = math.sqrt(n[0] ** 2 + n[1] ** 2 + n[2] ** 2) + 1e-9
    return n[0] / m, n[1] / m, n[2] / m


def phan_loai(lm: Any, world: Any = None, dang_chum: bool = False, w: float = 1.0, h: float = 1.0) -> tuple[str, bool]:
    """Trả về (thao tác, đang chụm). dang_chum là trạng thái chụm khung trước, dùng cho ngưỡng trễ."""
    i, m, r, p = ngon_duoi(lm, w, h)
    palm = _d2(lm, 0, 9, w, h) + 1e-6
    ratio = _d2(lm, 4, 8, w, h) / palm
    # chụm: đầu ngón cái chạm đầu ngón trỏ VÀ đầu ngón trỏ còn cách gốc ngón (không gập hẳn như khi nắm).
    # Xét trước "nắm": chụm mà ba ngón kia co thì cả bốn ngón đều "không duỗi", trước đây bị nhầm thành nắm tay.
    reach = _d2(lm, 8, 5, w, h) / palm > INDEX_REACH
    chum = reach and ratio < (PINCH_OFF if dang_chum else PINCH_ON)
    nam = not (i or m or r or p)
    if chum:
        return "chum", True
    if nam:
        if world is not None:
            n = phap_tuyen_long_ban_tay(world)
            return ("nam_ngang" if abs(n[1]) > NGANG_NGUONG else "nam_doc"), False
        return "nam_ngang", False
    if p and not (i or m or r):
        return "ut", False
    if i and not (m or r or p):
        return "tro", False
    if i and m and r and p:
        return "mo", False
    return "khac", False


class BoPhieu:
    """Bỏ phiếu số đông trên vài khung gần nhất để thao tác không nhảy qua lại (như hand_rects.py)."""

    def __init__(self, n: int = 5):
        self.ls: deque[str] = deque(maxlen=n)

    def them(self, tt: str) -> str:
        self.ls.append(tt)
        return Counter(self.ls).most_common(1)[0][0]

    def xoa(self) -> None:
        self.ls.clear()


class GanBen:
    """Gán bàn tay vào bên trái / phải theo vị trí cũ (ảnh đã lật như gương), không tin nhãn của mô hình."""

    def __init__(self, nho_giay: float = 1.5, xa_nhat: float = 0.30, tre_giua: float = 0.06):
        self.nho, self.xa, self.tre = nho_giay, xa_nhat, tre_giua
        self.cuoi: dict[str, tuple[float, float] | None] = {"trai": None, "phai": None}
        self.luc: dict[str, float] = {"trai": -1e9, "phai": -1e9}
        self.mot: str | None = None

    def gan(self, co_tay: list[tuple[float, float]], t: float) -> list[str]:
        if not co_tay:
            self.mot = None
            return []
        moi = {s: self.cuoi[s] is not None and t - self.luc[s] < self.nho for s in ("trai", "phai")}

        def d(c, s):
            q = self.cuoi[s]
            return math.hypot(c[0] - q[0], c[1] - q[1])

        if len(co_tay) >= 2:
            a, b = co_tay[0], co_tay[1]
            if moi["trai"] and moi["phai"]:
                giu = d(a, "trai") + d(b, "phai") <= d(a, "phai") + d(b, "trai")
            else:
                giu = a[0] <= b[0]
            ben = ["trai", "phai"] if giu else ["phai", "trai"]
            ben += ["khac"] * (len(co_tay) - 2)
            self.mot = None
        else:
            c = co_tay[0]
            s = None
            gan = [x for x in ("trai", "phai") if moi[x]]
            if gan:
                tot = min(gan, key=lambda x: d(c, x))
                if d(c, tot) < self.xa:
                    s = tot
            if s is None:
                s = self.mot if self.mot and abs(c[0] - 0.5) < self.tre else ("trai" if c[0] < 0.5 else "phai")
            self.mot = s
            ben = [s]
        for c, s in zip(co_tay, ben):
            if s in self.cuoi:
                self.cuoi[s] = c
                self.luc[s] = t
        return ben
