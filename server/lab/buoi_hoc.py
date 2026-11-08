"""Điều khiển buổi học: chế độ Luyện tập hay Kiểm tra, khóa hóa chất nguy hiểm, ra lệnh cho robot trong app.

    GET, POST /api/lop/{id}/buoi-hoc     xem, đổi chế độ và danh sách hóa chất bị khóa
    POST /api/lop/{id}/robot             ra lệnh cho robot trong app của cả lớp hoặc một học sinh

Máy chủ tự chặn được phần đi qua nó: khi lớp đang Kiểm tra, các lời gọi AI của học sinh bị từ chối ngay tại đây (main.py);
kế hoạch robot do AI lập không được dùng hóa chất đang khóa. App nhận trạng thái qua POST /app/nhip để khóa nốt phần chạy trên máy học sinh.
"""

from __future__ import annotations

import json
import time

from fastapi import APIRouter, HTTPException, Request
from pydantic import BaseModel, Field

from server.ai import robot_plan

from . import db, nhip, tai_khoan

router = APIRouter()

CHE_DO = ("luyen_tap", "kiem_tra")
LOI_AI_TAT = "Lớp đang làm bài kiểm tra nên giáo viên đã tắt trợ lý AI."
LOAI_NGUY_HIEM = {"doc": "Độc", "an_mon": "Ăn mòn", "de_chay": "Dễ cháy", "oxi_hoa": "Oxi hóa mạnh", "phan_ung_nuoc": "Phản ứng mạnh với nước", "no": "Dễ nổ"}
GIAY_GIU_LENH_ROBOT = 30  # máy mở sau đó không chạy lại lệnh robot cũ


# ---------------------------------------------------------------------------
# Trạng thái buổi học
# ---------------------------------------------------------------------------

def _hang(lop_id: int) -> dict | None:
    rows = db.query("SELECT * FROM buoi_hoc WHERE lop_id=?", (lop_id,))
    return rows[0] if rows else None


def _tap_khoa(r: dict | None) -> set[int]:
    """Hóa chất có nhãn nguy hiểm thuộc loại đã chọn, trừ ngoại lệ, cộng các chất khóa thêm."""
    if r is None:
        return set()
    loai = set(json.loads(r["khoa_loai"]))
    ngoai_le = set(json.loads(r["ngoai_le"]))
    tu_nhan = {c["stt"] for c in db.CHEM["hoa_chat"] if loai & set(c["nguy_hiem"])}
    return (tu_nhan - ngoai_le) | set(json.loads(r["khoa_them"]))


def _hang_cua_nguoi(nd: dict | None) -> list[dict]:
    if not nd or nd["vai_tro"] != "hoc_sinh":
        return []
    lop = [l["id"] for l in tai_khoan.cong_khai(nd)["lop"]]
    if not lop:
        return []
    return db.query(f"SELECT * FROM buoi_hoc WHERE lop_id IN ({','.join('?' * len(lop))})", tuple(lop))


def ai_bi_tat(nd: dict | None) -> bool:
    """Học sinh thuộc nhiều lớp thì chỉ cần một lớp đang Kiểm tra là tắt."""
    return any(r["che_do"] == "kiem_tra" for r in _hang_cua_nguoi(nd))


def hoa_chat_bi_khoa(nd: dict | None) -> set[int]:
    khoa: set[int] = set()
    for r in _hang_cua_nguoi(nd):
        khoa |= _tap_khoa(r)
    return khoa


def cho_app(nd: dict, da_buoi: int) -> dict:
    """Danh sách hóa chất khóa có thể dài nên chỉ gửi khi app chưa có đúng phiên bản này."""
    rows = _hang_cua_nguoi(nd)
    phien_ban = max((r["phien_ban"] for r in rows), default=0)
    kiem_tra = any(r["che_do"] == "kiem_tra" for r in rows)
    het_gio = max((r["het_gio"] for r in rows if r["che_do"] == "kiem_tra"), default=0)
    return {"phien_ban": phien_ban, "che_do": "kiem_tra" if kiem_tra else "luyen_tap", "ai_bat": not kiem_tra, "robot_bat": not kiem_tra, "het_gio": het_gio,
            "hoa_chat_khoa": sorted(hoa_chat_bi_khoa(nd)) if da_buoi != phien_ban else None}


# ---------------------------------------------------------------------------
# Phía giáo viên
# ---------------------------------------------------------------------------

class DoiBuoi(BaseModel):
    che_do: str = "luyen_tap"
    phut: int = Field(default=0, ge=0, le=300)
    khoa_loai: list[str] = Field(default_factory=list, max_length=10)
    ngoai_le: list[int] = Field(default_factory=list, max_length=720)
    khoa_them: list[int] = Field(default_factory=list, max_length=720)


class LenhRobot(BaseModel):
    hanh_dong: str
    khoa: str = Field(default="", max_length=60)   # thí nghiệm mẫu
    stt: int = 0                                   # hóa chất
    loi: str = Field(default="", max_length=300)   # câu robot nói
    hoc_sinh_id: int | None = None


def _lop(request: Request, lop_id: int) -> dict:
    lop = tai_khoan.lop_cua_giao_vien(lop_id, request.state.nguoi_dung["id"])
    if lop is None:
        raise HTTPException(404, "Không có lớp này")
    return lop


def _chat(stt: int) -> dict:
    c = db.CHEM_BY_STT.get(stt)
    return {"stt": stt, "ten": c["ten"], "cong_thuc": c["cong_thuc"]} if c else {"stt": stt, "ten": f"Chất {stt}", "cong_thuc": ""}


def _lenh_robot_cuoi(lop_id: int) -> dict | None:
    rows = db.query("SELECT id, hoc_sinh_id, noi_dung, tao_luc FROM lenh_app WHERE lop_id=? AND loai='robot' ORDER BY id DESC LIMIT 1", (lop_id,))
    if not rows:
        return None
    r = rows[0]
    now = time.time()
    da_nhan = 0
    for h in tai_khoan.hoc_sinh_cua_lop(lop_id):
        n = nhip.doc(h["id"])
        if n and now - n["luc"] < nhip.GIAY_CON_MO and r["hoc_sinh_id"] in (None, h["id"]) and n.get("da_nhan_lenh", 0) >= r["id"]:
            da_nhan += 1
    return {**r, "da_nhan": da_nhan}


def _trang_thai(lop_id: int) -> dict:
    r = _hang(lop_id)
    khoa = _tap_khoa(r)
    now = time.time()
    hoc_sinh = tai_khoan.hoc_sinh_cua_lop(lop_id)
    nhips = [nhip.doc(h["id"]) for h in hoc_sinh]
    return {"che_do": r["che_do"] if r else "luyen_tap", "het_gio": r["het_gio"] if r else 0, "may_chu_luc": now,
            "khoa_loai": json.loads(r["khoa_loai"]) if r else [],
            "ngoai_le": [_chat(s) for s in json.loads(r["ngoai_le"])] if r else [],
            "khoa_them": [_chat(s) for s in json.loads(r["khoa_them"])] if r else [],
            "so_chat_khoa": len(khoa), "tong_so_chat": len(db.CHEM["hoa_chat"]),
            "so_hoc_sinh": len(hoc_sinh), "may_dang_mo": sum(1 for n in nhips if n and now - n["luc"] < nhip.GIAY_CON_MO),
            "hoc_sinh": [{"id": h["id"], "ho_ten": h["ho_ten"]} for h in hoc_sinh],
            "lenh_robot_cuoi": _lenh_robot_cuoi(lop_id),
            "loai_nguy_hiem": [{"ma": ma, "ten": ten, "so_chat": sum(1 for c in db.CHEM["hoa_chat"] if ma in c["nguy_hiem"])} for ma, ten in LOAI_NGUY_HIEM.items()],
            "thi_nghiem_mau": [{"khoa": k, "ten": t} for k, t in robot_plan.TEMPLATES.items()]}


@router.get("/api/lop/{lop_id}/buoi-hoc")
def xem_buoi(lop_id: int, request: Request) -> dict:
    _lop(request, lop_id)
    return _trang_thai(lop_id)


@router.post("/api/lop/{lop_id}/buoi-hoc")
def doi_buoi(lop_id: int, body: DoiBuoi, request: Request) -> dict:
    _lop(request, lop_id)
    if body.che_do not in CHE_DO:
        raise HTTPException(422, "Chế độ phải là luyen_tap hoặc kiem_tra")
    for ma in body.khoa_loai:
        if ma not in LOAI_NGUY_HIEM:
            raise HTTPException(422, f"Không có loại nguy hiểm '{ma}'")
    for stt in [*body.ngoai_le, *body.khoa_them]:
        if stt not in db.CHEM_BY_STT:
            raise HTTPException(422, f"Không có hóa chất số {stt}")
    cu = _hang(lop_id)
    now = time.time()
    db.execute("INSERT OR REPLACE INTO buoi_hoc (lop_id, che_do, het_gio, khoa_loai, ngoai_le, khoa_them, phien_ban, cap_nhat) VALUES (?,?,?,?,?,?,?,?)",
               (lop_id, body.che_do, now + body.phut * 60 if body.phut and body.che_do == "kiem_tra" else 0,
                json.dumps(sorted(set(body.khoa_loai))), json.dumps(sorted(set(body.ngoai_le))), json.dumps(sorted(set(body.khoa_them))),
                max(int(now * 1000), cu["phien_ban"] + 1 if cu else 0), now))
    return _trang_thai(lop_id)


def _ke_hoach(body: LenhRobot) -> tuple[list[dict], str]:
    """Đổi lệnh của giáo viên thành đúng các bước robot đang chạy, kiểm tra bằng cùng hàm với kế hoạch do AI lập."""
    if body.hanh_dong == "lam_mau":
        ten = robot_plan.TEMPLATES.get(body.khoa)
        if ten is None:
            raise HTTPException(422, "Không có thí nghiệm mẫu này")
        buoc, nhan = {"ky_nang": "thi_nghiem_mau", "hoa_chat": body.khoa}, f"Làm mẫu: {ten}"
    elif body.hanh_dong == "lay_hoa_chat":
        if body.stt not in db.CHEM_BY_STT:
            raise HTTPException(422, "Không có hóa chất này")
        buoc, nhan = {"ky_nang": "lay_hoa_chat", "hoa_chat": str(body.stt)}, f"Lấy {db.CHEM_BY_STT[body.stt]['ten']} ra bàn"
    elif body.hanh_dong in ("tat_lua", "bat_tu_hut", "don_ban"):
        buoc, nhan = {"ky_nang": body.hanh_dong}, {"tat_lua": "Tắt mọi nguồn lửa", "bat_tu_hut": "Bật tủ hút", "don_ban": "Dọn bàn"}[body.hanh_dong]
    elif body.hanh_dong == "noi":
        if not body.loi.strip():
            raise HTTPException(422, "Hãy nhập câu robot sẽ nói")
        buoc, nhan = {"ky_nang": "noi", "loi": body.loi.strip()}, f"Robot nói: {body.loi.strip()}"
    else:
        raise HTTPException(422, "Không có lệnh này")
    theo_so = lambda q: db.CHEM_BY_STT.get(int(q)) if str(q).isdigit() else None
    ke_hoach, bo = robot_plan.validate([buoc], set(), theo_so)
    if bo or not ke_hoach:
        raise HTTPException(422, "; ".join(bo) or "Không tạo được lệnh")
    return ke_hoach, nhan


@router.post("/api/lop/{lop_id}/robot")
def ra_lenh_robot(lop_id: int, body: LenhRobot, request: Request) -> dict:
    _lop(request, lop_id)
    if body.hoc_sinh_id is not None and not tai_khoan.co_trong_lop(lop_id, body.hoc_sinh_id):
        raise HTTPException(404, "Học sinh này không có trong lớp")
    ke_hoach, nhan = _ke_hoach(body)
    cur = db.execute("INSERT INTO lenh_app (lop_id, hoc_sinh_id, loai, noi_dung, ke_hoach, tao_luc) VALUES (?,?,?,?,?,?)",
                     (lop_id, body.hoc_sinh_id, "robot", nhan, json.dumps(ke_hoach, ensure_ascii=False), time.time()))
    return {"id": cur.lastrowid, "noi_dung": nhan, "ke_hoach": ke_hoach}
