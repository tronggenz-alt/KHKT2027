"""Lộ trình học Sinh (chỉ admin – /admin → Lộ trình học): danh sách bài ở config/lo_trinh_sinh_hoc.json,
tiến độ (bài đã xong) lưu CSDL bảng lo_trinh_tien_do theo từng tài khoản → có trong bản sao lưu app.db."""
from __future__ import annotations

import json
import time
from pathlib import Path
from typing import Any

from . import db

GOC = Path(__file__).resolve().parent.parent
TEP_LO_TRINH = GOC / "config" / "lo_trinh_sinh_hoc.json"
TEP_CO_QUAN = GOC / "frontend" / "sinh-hoc" / "kien-thuc" / "danh-muc.json"

_bo_nho: dict[Path, tuple[float, Any]] = {}


class LoiLoTrinh(Exception):
    def __init__(self, thong_bao: str, status: int = 400):
        super().__init__(thong_bao)
        self.status = status


def _doc_json(tep: Path) -> Any:
    """Đọc JSON, nhớ theo thời điểm sửa file → sửa file là có hiệu lực ngay, không cần khởi động lại."""
    mtime = tep.stat().st_mtime
    luu = _bo_nho.get(tep)
    if luu and luu[0] == mtime:
        return luu[1]
    du_lieu = json.loads(tep.read_text(encoding="utf-8"))
    _bo_nho[tep] = (mtime, du_lieu)
    return du_lieu


def danh_sach_bai() -> dict[str, Any]:
    return _doc_json(TEP_LO_TRINH)


def _ten_co_quan() -> dict[str, dict[str, str]]:
    try:
        return {c["id"]: {"ten": c["ten"], "icon": c.get("icon", "")} for c in _doc_json(TEP_CO_QUAN)["co_quan"]}
    except (OSError, ValueError, KeyError):
        return {}


def _id_bai() -> set[str]:
    return {b["id"] for ch in danh_sach_bai()["chuong"] for b in ch["bai"]}


def tien_do(uid: int) -> dict[str, Any]:
    """Lộ trình kèm trạng thái từng bài của tài khoản uid + số bài xong / tổng / % (toàn bộ và từng chương)."""
    with db.ket_noi() as con:
        da_xong = {r["bai_id"]: r["xong_luc"] for r in con.execute("SELECT bai_id, xong_luc FROM lo_trinh_tien_do WHERE user_id = ?", (uid,))}
    co_quan = _ten_co_quan()
    lt = danh_sach_bai()
    chuong, tong, xong = [], 0, 0
    for ch in lt["chuong"]:
        bai = [{"id": b["id"], "ten": b["ten"], "xong": b["id"] in da_xong, "xong_luc": da_xong.get(b["id"]),
                "co_quan": [{"id": c, **co_quan[c]} for c in b.get("co_quan", []) if c in co_quan]} for b in ch["bai"]]
        so_xong = sum(b["xong"] for b in bai)
        chuong.append({"id": ch["id"], "ten": ch["ten"], "bai": bai, "tong": len(bai), "xong": so_xong})
        tong += len(bai)
        xong += so_xong
    return {"ten": lt["ten"], "chuong": chuong, "tong": tong, "xong": xong, "phan_tram": round(100 * xong / tong) if tong else 0}


def danh_dau(uid: int, bai_id: str, xong: bool) -> dict[str, Any]:
    if bai_id not in _id_bai():
        raise LoiLoTrinh("Không có bài này trong lộ trình.", 404)
    with db.ket_noi() as con:
        if xong:
            con.execute("INSERT OR IGNORE INTO lo_trinh_tien_do (user_id, bai_id, xong_luc) VALUES (?, ?, ?)", (uid, bai_id, time.time()))
        else:
            con.execute("DELETE FROM lo_trinh_tien_do WHERE user_id = ? AND bai_id = ?", (uid, bai_id))
    return tien_do(uid)
