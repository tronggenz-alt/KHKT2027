"""Lớp học: giáo viên quản lý lớp của mình, học sinh xem kết quả của mình.

Mọi đường dẫn /api/lop chỉ giáo viên vào được (bảng quyền ở server/lab/dang_nhap.py). Lớp của giáo viên khác trả 404
như không tồn tại. Mật khẩu tạm chỉ trả về đúng một lần lúc tạo hoặc đặt lại, CSDL chỉ giữ bản băm.
"""

from __future__ import annotations

import csv
import io
import json
import re
from datetime import datetime

from fastapi import APIRouter, HTTPException, Query, Request, Response
from pydantic import BaseModel, Field

from . import sinh_db, tai_khoan
from .api import fold

router = APIRouter(prefix="/api")

TOI_DA_DONG = 200


class TaoLop(BaseModel):
    ten: str = Field(max_length=120)
    nam_hoc: str = Field(default="", max_length=20)


class SuaLop(BaseModel):
    ten: str | None = Field(default=None, max_length=120)
    nam_hoc: str | None = Field(default=None, max_length=20)
    ma_lop_mo: bool | None = None


class ThemHocSinh(BaseModel):
    danh_sach: str = Field(max_length=40000)  # mỗi dòng: "Họ tên" hoặc "Họ tên, tên đăng nhập"


def _lop(request: Request, lop_id: int) -> dict:
    lop = tai_khoan.lop_cua_giao_vien(lop_id, request.state.nguoi_dung["id"])
    if lop is None:
        raise HTTPException(404, "Không có lớp này")
    return lop


def _hoc_sinh_trong_lop(lop_id: int, hs_id: int) -> dict:
    nd = tai_khoan.tim_theo_id(hs_id) if tai_khoan.co_trong_lop(lop_id, hs_id) else None
    if nd is None:
        raise HTTPException(404, "Học sinh này không có trong lớp")
    return nd


@router.get("/lop")
def ds_lop(request: Request) -> list[dict]:
    return [{**l, "ma_lop_mo": bool(l["ma_lop_mo"])} for l in tai_khoan.ds_lop_cua_giao_vien(request.state.nguoi_dung["id"])]


@router.post("/lop")
def tao_lop(body: TaoLop, request: Request) -> dict:
    try:
        lop = tai_khoan.tao_lop(body.ten, body.nam_hoc, request.state.nguoi_dung["id"])
    except tai_khoan.LoiTaiKhoan as e:
        raise HTTPException(422, str(e))
    return {**lop, "ma_lop_mo": bool(lop["ma_lop_mo"]), "so_hoc_sinh": 0}


@router.patch("/lop/{lop_id}")
def sua_lop(lop_id: int, body: SuaLop, request: Request) -> dict:
    _lop(request, lop_id)
    try:
        tai_khoan.sua_lop(lop_id, body.ten, body.nam_hoc, body.ma_lop_mo)
    except tai_khoan.LoiTaiKhoan as e:
        raise HTTPException(422, str(e))
    lop = _lop(request, lop_id)
    return {**lop, "ma_lop_mo": bool(lop["ma_lop_mo"])}


@router.get("/lop/{lop_id}/hoc-sinh")
def ds_hoc_sinh(lop_id: int, request: Request) -> list[dict]:
    _lop(request, lop_id)
    return [{**h, "phai_doi_mat_khau": bool(h["phai_doi_mat_khau"])} for h in tai_khoan.hoc_sinh_cua_lop(lop_id)]


def _ten_dang_nhap_tu_ho_ten(ho_ten: str) -> str:
    """"Nguyễn Văn An" thành nguyen.van.an; trùng thì thêm số: nguyen.van.an2."""
    goc = ".".join(re.findall(r"[a-z0-9]+", fold(ho_ten)))[:28].strip(".")
    goc = goc if len(goc) >= 3 else f"hs.{goc}".strip(".")
    ten, so = goc, 1
    while tai_khoan.tim_theo_ten(ten):
        so += 1
        ten = f"{goc}{so}"
    return ten


def _tach_dong(dong: str) -> tuple[str, str]:
    """Dán từ Excel thì cột cách nhau bằng tab; gõ tay thì dấu phẩy hoặc chấm phẩy."""
    phan = [p.strip() for p in re.split(r"[\t,;]", dong, maxsplit=1)]
    return phan[0], phan[1] if len(phan) > 1 else ""


@router.post("/lop/{lop_id}/hoc-sinh")
def them_hoc_sinh(lop_id: int, body: ThemHocSinh, request: Request) -> dict:
    """Thêm từng dòng một: dòng lỗi không làm hỏng các dòng khác."""
    _lop(request, lop_id)
    dong_ds = [d.strip() for d in body.danh_sach.splitlines() if d.strip()]
    if not dong_ds:
        raise HTTPException(422, "Danh sách đang trống")
    if len(dong_ds) > TOI_DA_DONG:
        raise HTTPException(422, f"Mỗi lần thêm tối đa {TOI_DA_DONG} học sinh")
    tao, them, loi = [], [], []
    for dong in dong_ds:
        ho_ten, ten = _tach_dong(dong)
        try:
            co_san = tai_khoan.tim_theo_ten(ten) if ten else None
            if co_san is not None:
                # Học sinh đã có tài khoản: đưa vào lớp, giữ nguyên mật khẩu. Họ tên phải khớp để không thêm nhầm người.
                if co_san["vai_tro"] != "hoc_sinh" or fold(co_san["ho_ten"]) != fold(ho_ten):
                    raise tai_khoan.LoiTaiKhoan("Tên đăng nhập này đã có người khác dùng")
                tai_khoan.them_vao_lop(lop_id, co_san["id"])
                them.append({"id": co_san["id"], "ho_ten": co_san["ho_ten"], "ten_dang_nhap": co_san["ten_dang_nhap"]})
                continue
            if not 2 <= len(ho_ten) <= 80:
                raise tai_khoan.LoiTaiKhoan("Họ tên cần từ 2 đến 80 ký tự")
            mat_khau = tai_khoan.sinh_mat_khau_tam()
            nd = tai_khoan.tao_nguoi_dung(ten or _ten_dang_nhap_tu_ho_ten(ho_ten), ho_ten, "hoc_sinh", mat_khau, phai_doi=True)
            tai_khoan.them_vao_lop(lop_id, nd["id"])
            tao.append({"id": nd["id"], "ho_ten": nd["ho_ten"], "ten_dang_nhap": nd["ten_dang_nhap"], "mat_khau_tam": mat_khau})
        except tai_khoan.LoiTaiKhoan as e:
            loi.append({"dong": dong[:120], "ly_do": str(e)})
    return {"tao": tao, "them": them, "loi": loi}


@router.delete("/lop/{lop_id}/hoc-sinh/{hs_id}")
def go_hoc_sinh(lop_id: int, hs_id: int, request: Request) -> dict:
    """Chỉ gỡ khỏi lớp: tài khoản và kết quả đã có của học sinh vẫn còn."""
    _lop(request, lop_id)
    _hoc_sinh_trong_lop(lop_id, hs_id)
    tai_khoan.go_khoi_lop(lop_id, hs_id)
    return {"ok": True}


@router.post("/lop/{lop_id}/hoc-sinh/{hs_id}/dat-lai-mat-khau")
def dat_lai_mat_khau(lop_id: int, hs_id: int, request: Request) -> dict:
    _lop(request, lop_id)
    hs = _hoc_sinh_trong_lop(lop_id, hs_id)
    mat_khau = tai_khoan.sinh_mat_khau_tam()
    tai_khoan.dat_mat_khau(hs_id, mat_khau, phai_doi=True)
    tai_khoan.huy_phien_khac(hs_id)
    return {"id": hs_id, "ho_ten": hs["ho_ten"], "ten_dang_nhap": hs["ten_dang_nhap"], "mat_khau_tam": mat_khau}


# ---------------------------------------------------------------------------
# Kết quả thí nghiệm
# ---------------------------------------------------------------------------

def _ten_bai() -> dict[str, str]:
    return {b["ma"]: b["ten"] for b in sinh_db.get_all_static_bai_thi_nghiem()}


def _tom_tat(du_lieu: str) -> str:
    try:
        return str(json.loads(du_lieu).get("tom_tat") or "")
    except (ValueError, AttributeError):
        return ""


def _hang_ket_qua(k: dict, ten_bai: dict[str, str]) -> dict:
    return {"id": k["id"], "hoc_sinh_id": k["hoc_sinh_id"], "lop": k["lop"], "ma_bai": k["ma_bai"], "ten_bai": ten_bai.get(k["ma_bai"], k["ma_bai"]),
            "thoi_gian": k["thoi_gian"], "tom_tat": _tom_tat(k["du_lieu"]), "diem": k["diem"], "nhan_xet": k["nhan_xet"]}


def _ket_qua_cua_lop(lop_id: int, ma_bai: str = "", hoc_sinh_id: int = 0) -> list[dict]:
    ho_ten = {h["id"]: h["ho_ten"] for h in tai_khoan.hoc_sinh_cua_lop(lop_id)}
    ids = [hoc_sinh_id] if hoc_sinh_id in ho_ten else ([] if hoc_sinh_id else list(ho_ten))
    ten_bai = _ten_bai()
    return [{**_hang_ket_qua(k, ten_bai), "ho_ten": ho_ten[k["hoc_sinh_id"]]} for k in sinh_db.list_ket_qua_theo_hoc_sinh(ids, ma_bai)]


@router.get("/lop/{lop_id}/ket-qua")
def ket_qua_lop(lop_id: int, request: Request, ma_bai: str = Query(""), hoc_sinh_id: int = Query(0)) -> list[dict]:
    _lop(request, lop_id)
    return _ket_qua_cua_lop(lop_id, ma_bai, hoc_sinh_id)


def o_csv(gia_tri) -> str:
    """Ô bắt đầu bằng = + - @ bị Excel coi là công thức; họ tên do học sinh tự gõ nên phải vô hiệu hóa."""
    chu = "" if gia_tri is None else str(gia_tri)
    return "'" + chu if chu[:1] in ("=", "+", "-", "@", "\t", "\r") else chu


@router.get("/lop/{lop_id}/ket-qua.csv")
def ket_qua_lop_csv(lop_id: int, request: Request) -> Response:
    lop = _lop(request, lop_id)
    out = io.StringIO()
    viet = csv.writer(out)
    viet.writerow(["Học sinh", "Lớp", "Bài", "Thời gian", "Tóm tắt", "Điểm", "Nhận xét"])
    for k in _ket_qua_cua_lop(lop_id):
        gio = datetime.fromtimestamp(k["thoi_gian"]).strftime("%d/%m/%Y %H:%M")
        viet.writerow([o_csv(x) for x in (k["ho_ten"], lop["ten"], k["ten_bai"], gio, k["tom_tat"], k["diem"], k["nhan_xet"])])
    return Response(content=b"\xef\xbb\xbf" + out.getvalue().encode("utf-8"), media_type="text/csv; charset=utf-8",
                    headers={"Content-Disposition": f"attachment; filename=ket_qua_lop_{lop_id}.csv"})


@router.get("/toi/ket-qua")
def ket_qua_cua_toi(request: Request) -> list[dict]:
    ten_bai = _ten_bai()
    return [_hang_ket_qua(k, ten_bai) for k in sinh_db.list_ket_qua_theo_hoc_sinh([request.state.nguoi_dung["id"]])]
