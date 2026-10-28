"""Router AI: chọn AI nào chạy và thứ tự dự phòng cho TỪNG hệ thống (Giải chuỗi, Mô hình 3D, Cơ chế).

- Thứ tự mặc định: backend/ai/routing.json (claude → gemini → openai). Admin đổi ở /admin → Thứ tự AI → lưu app_settings
  "thu_tu_ai" (JSON) đè lên file; "Về mặc định" xoá phần đè.
- llm.PhienHoiThoai dựng chuỗi AI cho một phiên = thứ tự này, BỎ QUA AI không dùng được (chưa có key, người dùng không có quyền
  Claude, key Gemini riêng của người dùng → không dùng key máy chủ…). AI đang chạy lỗi / hết hạn mức / quá thời gian → AI kế tiếp,
  ghi nhật ký (bảng ai_fallbacks + log): hệ thống nào, AI nào lỗi, lý do, AI nào thay thế.
"""
from __future__ import annotations

import json
import logging
import time
from pathlib import Path

from .. import db

log = logging.getLogger(__name__)

TEP_MAC_DINH = Path(__file__).with_name("routing.json")
CAC_AI = ("claude", "gemini", "openai")
TEN_AI = {"claude": "Claude", "gemini": "Gemini", "openai": "ChatGPT"}
HE_THONG = {"giai_chuoi": "Giải chuỗi", "mo_hinh_3d": "Mô hình 3D", "co_che": "Cơ chế"}
# loại yêu cầu (KeHoachAI.loai) → hệ thống
THEO_LOAI = {"giai": "giai_chuoi", "phuong_trinh": "giai_chuoi", "3d": "mo_hinh_3d", "co_che": "co_che", "co_che_anh": "co_che", "sinh_hoc": "mo_hinh_3d", "sinh_hoc_anh": "mo_hinh_3d"}

KHOA_CAI_DAT = "thu_tu_ai"
GIU_NHAT_KY_NGAY = 30


def hop_le(ds) -> bool:
    return isinstance(ds, list) and sorted(ds) == sorted(CAC_AI)


def mac_dinh(he_thong: str | None = None):
    """Thứ tự trong routing.json (cả bảng, hoặc của một hệ thống)."""
    try:
        cfg = json.loads(TEP_MAC_DINH.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        log.exception("Không đọc được %s – dùng claude → gemini → openai", TEP_MAC_DINH)
        cfg = {}
    bang = {k: (cfg.get(k) if hop_le(cfg.get(k)) else list(CAC_AI)) for k in HE_THONG}
    return bang if he_thong is None else bang.get(he_thong, list(CAC_AI))


def _de() -> dict:
    try:
        d = json.loads(db.cai_dat(KHOA_CAI_DAT, "") or "{}")
        return d if isinstance(d, dict) else {}
    except ValueError:
        return {}


def thu_tu(he_thong: str) -> list[str]:
    """Thứ tự đang dùng của một hệ thống (admin đã đổi → theo CSDL, không thì routing.json)."""
    ds = _de().get(he_thong)
    return list(ds) if hop_le(ds) else mac_dinh(he_thong)


def tat_ca() -> dict:
    de = _de()
    return {k: {"ten": ten, "thu_tu": thu_tu(k), "mac_dinh": mac_dinh(k), "da_doi": hop_le(de.get(k))} for k, ten in HE_THONG.items()}


def dat_thu_tu(he_thong: str, ds: list[str]) -> list[str]:
    if he_thong not in HE_THONG:
        raise ValueError("Hệ thống không hợp lệ.")
    if not hop_le(ds):
        raise ValueError("Thứ tự phải gồm đủ và không trùng: claude, gemini, openai.")
    de = _de()
    de[he_thong] = list(ds)
    db.dat_cai_dat(KHOA_CAI_DAT, json.dumps(de))
    return thu_tu(he_thong)


def ve_mac_dinh(he_thong: str) -> list[str]:
    de = _de()
    de.pop(he_thong, None)
    db.dat_cai_dat(KHOA_CAI_DAT, json.dumps(de))
    return thu_tu(he_thong)


def he_thong_cua(loai: str | None) -> str:
    """Loại yêu cầu (KeHoachAI.loai) → hệ thống; không rõ → Giải chuỗi."""
    return THEO_LOAI.get(loai or "", "giai_chuoi")


# ---------------------------------------------------------------- nhật ký chuyển AI dự phòng
def ghi_du_phong(he_thong: str, tu: str, sang: str, ly_do: str, user_id: int | None = None, job_id: str = "") -> None:
    log.warning("Dự phòng AI [%s]: %s lỗi (%s) → chuyển sang %s", he_thong, tu, ly_do[:200], sang)
    try:
        with db.ket_noi() as con:
            con.execute("INSERT INTO ai_fallbacks (ts, he_thong, tu, sang, ly_do, user_id, job_id) VALUES (?, ?, ?, ?, ?, ?, ?)",
                        (time.time(), he_thong, tu, sang, (ly_do or "")[:300], user_id, job_id or ""))
            con.execute("DELETE FROM ai_fallbacks WHERE ts < ?", (time.time() - GIU_NHAT_KY_NGAY * 86400,))
    except Exception:  # noqa: BLE001 – ghi nhật ký lỗi không được làm hỏng bài giải
        log.exception("Không ghi được nhật ký dự phòng AI")


def ds_du_phong(gioi_han: int = 30) -> list[dict]:
    with db.ket_noi() as con:
        rows = con.execute("SELECT * FROM ai_fallbacks ORDER BY id DESC LIMIT ?", (gioi_han,)).fetchall()
    return [{"luc": time.strftime("%Y-%m-%d %H:%M:%S", time.localtime(r["ts"])), "he_thong": HE_THONG.get(r["he_thong"], r["he_thong"]),
             "tu": TEN_AI.get(r["tu"], r["tu"]), "sang": TEN_AI.get(r["sang"], r["sang"]), "ly_do": r["ly_do"], "user_id": r["user_id"]}
            for r in rows]
