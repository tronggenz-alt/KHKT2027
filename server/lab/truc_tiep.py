"""Theo dõi trực tiếp: giáo viên thấy học sinh trong lớp đang ở đâu, làm gì, làm sai gì; gửi thông báo và yêu cầu cả lớp dừng lại.

    POST /app/nhip                       app gọi khoảng 2 giây một lần: báo vị trí, việc đang làm; nhận trình chiếu, thông báo, lệnh robot, yêu cầu chú ý, trạng thái buổi học
    GET  /api/mat-bang                   các khu của tòa nhà để vẽ sơ đồ (cùng bảng với ZoneMap.cs trong Unity)
    GET  /api/lop/{id}/truc-tiep         từng học sinh trong lớp và các sự kiện mới
    POST /api/lop/{id}/thong-bao         nhắn cả lớp hoặc một học sinh
    POST /api/lop/{id}/chu-y             bật, tắt "Tất cả chú ý"

Sự kiện lấy từ dữ liệu app vốn đã gửi lên (/api/nhat-ky, /lab/sinh/su-kien), gắn với tài khoản theo mã phiên nên học sinh không ghi thay người khác được.
"""

from __future__ import annotations

import json
import time

from fastapi import APIRouter, HTTPException, Query, Request
from pydantic import BaseModel, Field

from . import buoi_hoc, db, nhip, tai_khoan, trinh_chieu

router = APIRouter()

GIAY_TINH_LOI = 3 * 3600   # đếm cảnh báo trong buổi học gần nhất
GIAY_GIU_THONG_BAO = 300   # máy mở sau chừng này giây không nhận lại thông báo cũ
GIU_SU_KIEN_NGAY = 30

KHU = [
    {"ma": "phong_lab", "ten": "Phòng thí nghiệm", "x0": 0, "z0": 0, "x1": 20, "z1": 15, "tang": 1},
    {"ma": "kho_hoa_chat", "ten": "Phòng hóa chất", "x0": 20, "z0": 0, "x1": 27, "z1": 15, "tang": 1},
    {"ma": "hanh_lang", "ten": "Hành lang", "x0": 0, "z0": -2.2, "x1": 27, "z1": 0, "tang": 1},
    {"ma": "phong_may", "ten": "Phòng máy phân tích", "x0": 0, "z0": -9, "x1": 13.5, "z1": -2.2, "tang": 1},
    {"ma": "phong_robot", "ten": "Phòng tự động hóa & robot", "x0": 13.5, "z0": -9, "x1": 27, "z1": -2.2, "tang": 1},
    {"ma": "phong_sinh", "ten": "Phòng thí nghiệm sinh học", "x0": 0, "z0": 0, "x1": 20, "z1": 15, "tang": 2},
    {"ma": "phong_vi_sinh", "ten": "Phòng vi sinh", "x0": 20, "z0": 7.5, "x1": 27, "z1": 15, "tang": 2},
    {"ma": "kho_sinh_pham", "ten": "Kho sinh phẩm", "x0": 20, "z0": 0, "x1": 27, "z1": 7.5, "tang": 2},
    {"ma": "hanh_lang_t2", "ten": "Hành lang tầng 2", "x0": 0, "z0": -3.6, "x1": 27, "z1": 0, "tang": 2},
    {"ma": "phong_sinh_phan_tu", "ten": "Phòng sinh học phân tử", "x0": 0, "z0": -9, "x1": 13.5, "z1": -3.6, "tang": 2},
    {"ma": "nha_kinh", "ten": "Nhà kính thực vật", "x0": 13.5, "z0": -9, "x1": 27, "z1": -3.6, "tang": 2},
    {"ma": "hanh_lang_dong", "ten": "Hành lang đông tầng 2", "x0": 27, "z0": 6, "x1": 30, "z1": 15, "tang": 2},
    {"ma": "loi_thang", "ten": "Cầu thang và thang máy", "x0": 27, "z0": -2.2, "x1": 30, "z1": 6, "tang": 0},  # 0: cả hai tầng
]


def khu_tai(tang: int | None, x: float | None, z: float | None) -> dict | None:
    """Giống ZoneMap.At trong Unity. None: ngoài trời hoặc app chưa báo vị trí."""
    if x is None or z is None:
        return None
    for k in KHU:
        if k["tang"] in (0, tang) and k["x0"] <= x < k["x1"] and k["z0"] <= z < k["z1"]:
            return k
    return None


# ---------------------------------------------------------------------------
# Sự kiện của học sinh
# ---------------------------------------------------------------------------

_MUC_DO = {"danger": "nguy_hiem", "nguy_hiem": "nguy_hiem", "warning": "canh_bao", "canh_bao": "canh_bao"}


def ghi_su_kien(nd: dict | None, muc_do: str, tieu_de: str, chi_tiet: str = "", luc: float | None = None) -> None:
    """Ghi vào dòng sự kiện của lớp. Chỉ tài khoản học sinh: giáo viên thử và chế độ dùng thử không thuộc lớp nào."""
    if not nd or nd["vai_tro"] != "hoc_sinh":
        return
    now = time.time()
    db.execute("DELETE FROM su_kien_hoc_sinh WHERE thoi_gian<?", (now - GIU_SU_KIEN_NGAY * 86400,))
    db.execute("INSERT INTO su_kien_hoc_sinh (hoc_sinh_id, thoi_gian, muc_do, tieu_de, chi_tiet) VALUES (?,?,?,?,?)",
               (nd["id"], min(luc or now, now), _MUC_DO.get((muc_do or "").lower(), "thong_tin"), (tieu_de or "")[:300], (chi_tiet or "")[:1000]))


# ---------------------------------------------------------------------------
# Nhịp của app
# ---------------------------------------------------------------------------

class Nhip(BaseModel):
    da_hien: int = 0        # phiên bản trình chiếu đang hiện
    da_nhan_lenh: int = 0   # mã lệnh mới nhất đã nhận (thông báo, robot)
    da_buoi: int = 0        # phiên bản trạng thái buổi học app đang giữ
    dang_khoa: bool = False  # đang khóa điều khiển vì "Tất cả chú ý"
    tang: int | None = Field(default=None, ge=1, le=2)
    x: float | None = Field(default=None, ge=-500, le=500)
    z: float | None = Field(default=None, ge=-500, le=500)
    dang_cam: str = Field(default="", max_length=80)
    dang_lam: str = Field(default="", max_length=160)


def _lenh_cho(nd: dict, lop: list[int], da_nhan: int, now: float) -> list[dict]:
    if nd["vai_tro"] != "hoc_sinh" or not lop:
        return []
    rows = db.query(f"""SELECT id, loai, noi_dung, ke_hoach FROM lenh_app
                        WHERE lop_id IN ({','.join('?' * len(lop))}) AND (hoc_sinh_id IS NULL OR hoc_sinh_id=?) AND id>?
                        AND ((loai='robot' AND tao_luc>?) OR (loai<>'robot' AND tao_luc>?))
                        ORDER BY id LIMIT 20""", (*lop, nd["id"], da_nhan, now - buoi_hoc.GIAY_GIU_LENH_ROBOT, now - GIAY_GIU_THONG_BAO))
    out = []
    for r in rows:
        ke_hoach = r.pop("ke_hoach")
        out.append({**r, "ke_hoach": json.loads(ke_hoach)} if r["loai"] == "robot" else r)
    return out


def _chu_y_cho(nd: dict, lop: list[int]) -> dict:
    if nd["vai_tro"] == "hoc_sinh" and lop:
        rows = db.query(f"SELECT noi_dung FROM chu_y_lop WHERE lop_id IN ({','.join('?' * len(lop))}) ORDER BY tu_luc DESC LIMIT 1", tuple(lop))
        if rows:
            return {"bat": True, "noi_dung": rows[0]["noi_dung"]}
    return {"bat": False, "noi_dung": ""}


@router.post("/app/nhip")
def nhip_app(body: Nhip, request: Request) -> dict:
    nd, now = request.state.nguoi_dung, time.time()
    lop = [l["id"] for l in tai_khoan.cong_khai(nd)["lop"]]
    viec = trinh_chieu.viec_cua_lop(lop)
    cu = nhip.doc(nd["id"])
    do_tre = cu.get("do_tre_ms")
    if viec and body.da_hien == viec["phien_ban"] and cu.get("da_hien") != body.da_hien:
        do_tre = round((now - viec["cap_nhat"]) * 1000)  # từ lúc giáo viên bấm tới lúc app báo đã hiện
    nhip.ghi(nd["id"], {**body.model_dump(), "luc": now, "do_tre_ms": do_tre})
    return {"may_chu_luc": now, "trinh_chieu": trinh_chieu.cho_app(viec), "lenh": _lenh_cho(nd, lop, body.da_nhan_lenh, now), "chu_y": _chu_y_cho(nd, lop),
            "buoi": buoi_hoc.cho_app(nd, body.da_buoi)}


# ---------------------------------------------------------------------------
# Phía giáo viên
# ---------------------------------------------------------------------------

class ThongBao(BaseModel):
    noi_dung: str = Field(max_length=300)
    hoc_sinh_id: int | None = None  # bỏ trống: cả lớp


class ChuY(BaseModel):
    bat: bool
    noi_dung: str = Field(default="", max_length=200)


def _lop_cua_toi(request: Request, lop_id: int) -> dict:
    lop = tai_khoan.lop_cua_giao_vien(lop_id, request.state.nguoi_dung["id"])
    if lop is None:
        raise HTTPException(404, "Không có lớp này")
    return lop


@router.get("/api/mat-bang")
def mat_bang() -> list[dict]:
    return KHU


def _hang_hoc_sinh(h: dict, dem: dict[int, dict], now: float) -> dict:
    n = nhip.doc(h["id"])
    dang_mo = bool(n) and now - n["luc"] < nhip.GIAY_CON_MO
    khu = khu_tai(n.get("tang"), n.get("x"), n.get("z")) if dang_mo else None
    so = dem.get(h["id"], {})
    return {"id": h["id"], "ho_ten": h["ho_ten"], "ten_dang_nhap": h["ten_dang_nhap"], "dang_mo": dang_mo,
            "giay_truoc": round(now - n["luc"]) if n else None,
            # máy đã tắt thì không hiện vị trí cũ
            "tang": n.get("tang") if dang_mo else None, "x": n.get("x") if dang_mo else None, "z": n.get("z") if dang_mo else None,
            "khu": khu["ma"] if khu else "", "ten_khu": khu["ten"] if khu else ("Ngoài trời" if dang_mo and n.get("x") is not None else ""),
            "dang_cam": n.get("dang_cam", "") if dang_mo else "", "dang_lam": n.get("dang_lam", "") if dang_mo else "",
            "so_canh_bao": so.get("canh_bao", 0), "so_nguy_hiem": so.get("nguy_hiem", 0)}


@router.get("/api/lop/{lop_id}/truc-tiep")
def truc_tiep(lop_id: int, request: Request, sau_id: int = Query(0, ge=0)) -> dict:
    _lop_cua_toi(request, lop_id)
    now = time.time()
    ds = tai_khoan.hoc_sinh_cua_lop(lop_id)
    ten = {h["id"]: h["ho_ten"] for h in ds}
    ids = list(ten)
    su_kien, dem = [], {}
    if ids:
        q = ",".join("?" * len(ids))
        for r in db.query(f"""SELECT hoc_sinh_id, muc_do, COUNT(*) AS n FROM su_kien_hoc_sinh
                              WHERE hoc_sinh_id IN ({q}) AND thoi_gian>? AND muc_do<>'thong_tin' GROUP BY hoc_sinh_id, muc_do""", (*ids, now - GIAY_TINH_LOI)):
            dem.setdefault(r["hoc_sinh_id"], {})[r["muc_do"]] = r["n"]
        su_kien = db.query(f"""SELECT * FROM su_kien_hoc_sinh WHERE hoc_sinh_id IN ({q}) AND id>? ORDER BY id DESC LIMIT 40""", (*ids, sau_id))
        su_kien = [{**k, "ho_ten": ten[k["hoc_sinh_id"]]} for k in reversed(su_kien)]
    hoc_sinh = [_hang_hoc_sinh(h, dem, now) for h in ds]
    dang_mo = [h["id"] for h in hoc_sinh if h["dang_mo"]]
    chu_y = db.query("SELECT noi_dung, tu_luc FROM chu_y_lop WHERE lop_id=?", (lop_id,))
    cuoi = db.query("SELECT id, hoc_sinh_id, noi_dung, tao_luc FROM lenh_app WHERE lop_id=? AND loai='thong_bao' ORDER BY id DESC LIMIT 1", (lop_id,))
    thong_bao = None
    if cuoi:
        nhan = [i for i in dang_mo if (cuoi[0]["hoc_sinh_id"] in (None, i)) and nhip.doc(i).get("da_nhan_lenh", 0) >= cuoi[0]["id"]]
        thong_bao = {**cuoi[0], "da_nhan": len(nhan), "gui_toi": ten.get(cuoi[0]["hoc_sinh_id"], "Cả lớp")}
    return {"may_chu_luc": now, "so_hoc_sinh": len(ds), "may_dang_mo": len(dang_mo), "hoc_sinh": hoc_sinh, "su_kien": su_kien,
            "chu_y": {"bat": bool(chu_y), "noi_dung": chu_y[0]["noi_dung"] if chu_y else "", "tu_luc": chu_y[0]["tu_luc"] if chu_y else 0,
                      "may_da_khoa": sum(1 for i in dang_mo if nhip.doc(i).get("dang_khoa"))},
            "thong_bao_cuoi": thong_bao}


@router.post("/api/lop/{lop_id}/thong-bao")
def gui_thong_bao(lop_id: int, body: ThongBao, request: Request) -> dict:
    _lop_cua_toi(request, lop_id)
    noi_dung = body.noi_dung.strip()
    if not noi_dung:
        raise HTTPException(422, "Hãy nhập nội dung")
    if body.hoc_sinh_id is not None and not tai_khoan.co_trong_lop(lop_id, body.hoc_sinh_id):
        raise HTTPException(404, "Học sinh này không có trong lớp")
    cur = db.execute("INSERT INTO lenh_app (lop_id, hoc_sinh_id, loai, noi_dung, tao_luc) VALUES (?,?,?,?,?)",
                     (lop_id, body.hoc_sinh_id, "thong_bao", noi_dung, time.time()))
    return {"id": cur.lastrowid}


@router.post("/api/lop/{lop_id}/chu-y")
def dat_chu_y(lop_id: int, body: ChuY, request: Request) -> dict:
    _lop_cua_toi(request, lop_id)
    if body.bat:
        db.execute("INSERT OR REPLACE INTO chu_y_lop (lop_id, noi_dung, tu_luc) VALUES (?,?,?)", (lop_id, body.noi_dung.strip() or "Tất cả chú ý lên giáo viên", time.time()))
    else:
        db.execute("DELETE FROM chu_y_lop WHERE lop_id=?", (lop_id,))
    return {"bat": body.bat}
