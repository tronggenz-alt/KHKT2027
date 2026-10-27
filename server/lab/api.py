"""API của web quản lý phòng thí nghiệm (dưới /api)."""

from __future__ import annotations

import time
import unicodedata

from fastapi import APIRouter, HTTPException
from pydantic import BaseModel

from . import db, keypool

router = APIRouter(prefix="/api")


def fold(s: str) -> str:
    s = (s or "").replace("đ", "d").replace("Đ", "D")
    return "".join(c for c in unicodedata.normalize("NFD", s) if unicodedata.category(c) != "Mn").lower()


def _chem_row(stock: dict) -> dict:
    c = db.CHEM_BY_STT[stock["stt"]]
    return {
        "stt": c["stt"], "ten": c["ten"], "cong_thuc": c["cong_thuc"], "nhom": c["nhom"], "phan_loai": c["phan_loai"],
        "trang_thai": c["trang_thai"], "nong_do": c["nong_do"], "vi_tri": c["ma_vi_tri"], "nguy_hiem": c["nguy_hiem"],
        "so_luong": stock["so_luong"], "don_vi": stock["don_vi"], "toi_thieu": stock["toi_thieu"],
        "han_dung": stock["han_dung"], "ghi_chu": stock["ghi_chu"], "sap_het": stock["so_luong"] <= stock["toi_thieu"],
    }


@router.get("/tong-quan")
def tong_quan() -> dict:
    stock = db.query("SELECT * FROM ton_kho")
    low = [s for s in stock if s["so_luong"] <= s["toi_thieu"]]
    eq = db.query("SELECT * FROM thiet_bi")
    groups = {g["ma"]: {"ten": g["ten"], "mau": g["mau"], "so_luong": g["so_luong"]} for g in db.CHEM["nhom"]}
    since = time.time() - 7 * 86400
    incidents = db.query("SELECT COUNT(*) AS n FROM nhat_ky WHERE thoi_gian>? AND muc_do IN ('Warning','Danger')", (since,))[0]["n"]
    keys = keypool.list_keys()
    return {
        "so_hoa_chat": len(stock), "sap_het": len(low), "so_ke": len(db.CHEM["ke"]),
        "so_thiet_bi": len(eq), "tong_dung_cu": sum(e["so_luong"] for e in eq), "dung_cu_hong": sum(e["hong"] for e in eq),
        "dang_muon": sum(e["dang_muon"] for e in eq), "su_co_7_ngay": incidents,
        "khoa_api": len(keys), "khoa_dung_duoc": sum(1 for k in keys if k["bat"] and k["dang_nghi_giay"] == 0 and k["trang_thai"] != "khoa_sai"),
        "nhom": groups, "ke": db.CHEM["ke"],
        "sap_het_ds": [_chem_row(s) for s in low[:12]],
    }


@router.get("/hoa-chat")
def hoa_chat(q: str = "", nhom: str = "", sap_het: bool = False, trang: int = 1, moi_trang: int = 50) -> dict:
    rows = db.query("SELECT * FROM ton_kho ORDER BY stt")
    words = fold(q).split()  # mọi từ đều phải có: "dong sunfat" khớp "Đồng(II) sunfat"
    out = []
    for s in rows:
        c = db.CHEM_BY_STT.get(s["stt"])
        if c is None:
            continue
        if nhom and c["nhom"] != nhom:
            continue
        if sap_het and s["so_luong"] > s["toi_thieu"]:
            continue
        hay = f"{c['ten_khong_dau']} {(c['cong_thuc'] or '').lower()} {c['ma_vi_tri'].lower()} #{c['stt']}"
        if words and not all(w in hay or w == str(c["stt"]) for w in words):
            continue
        out.append(_chem_row(s))
    total = len(out)
    start = max(0, (trang - 1) * moi_trang)
    return {"tong": total, "trang": trang, "moi_trang": moi_trang, "ds": out[start:start + moi_trang]}


class SuaTonKho(BaseModel):
    so_luong: float | None = None
    toi_thieu: float | None = None
    han_dung: str | None = None
    ghi_chu: str | None = None
    them: float | None = None  # nhập thêm (+) hoặc xuất dùng (-)


@router.patch("/hoa-chat/{stt}")
def sua_hoa_chat(stt: int, body: SuaTonKho) -> dict:
    rows = db.query("SELECT * FROM ton_kho WHERE stt=?", (stt,))
    if not rows:
        raise HTTPException(404, "Không có hóa chất này")
    s = rows[0]
    q = s["so_luong"]
    if body.them:
        q = max(0.0, q + body.them)
        c = db.CHEM_BY_STT[stt]
        db.log("kho", f"{'Nhập' if body.them > 0 else 'Xuất'} {abs(body.them):g} {s['don_vi']} {c['ten']}", f"Còn {q:g} {s['don_vi']}", nguon="web")
    if body.so_luong is not None:
        q = max(0.0, body.so_luong)
    db.execute("UPDATE ton_kho SET so_luong=?, toi_thieu=?, han_dung=?, ghi_chu=?, cap_nhat=? WHERE stt=?",
               (q, body.toi_thieu if body.toi_thieu is not None else s["toi_thieu"], body.han_dung if body.han_dung is not None else s["han_dung"],
                body.ghi_chu if body.ghi_chu is not None else s["ghi_chu"], time.time(), stt))
    return _chem_row(db.query("SELECT * FROM ton_kho WHERE stt=?", (stt,))[0])


@router.get("/thiet-bi")
def thiet_bi() -> list[dict]:
    out = []
    for r in db.query("SELECT * FROM thiet_bi ORDER BY stt"):
        e = db.EQUIP_BY_STT[r["stt"]]
        out.append({**e, **r, "san_sang": r["so_luong"] - r["hong"] - r["dang_muon"]})
    return out


class SuaThietBi(BaseModel):
    so_luong: int | None = None
    hong: int | None = None
    dang_muon: int | None = None
    vi_tri: str | None = None
    ghi_chu: str | None = None


@router.patch("/thiet-bi/{stt}")
def sua_thiet_bi(stt: int, body: SuaThietBi) -> dict:
    rows = db.query("SELECT * FROM thiet_bi WHERE stt=?", (stt,))
    if not rows:
        raise HTTPException(404, "Không có thiết bị này")
    r = {**rows[0], **{k: v for k, v in body.model_dump().items() if v is not None}}
    r["hong"] = max(0, min(r["hong"], r["so_luong"]))
    r["dang_muon"] = max(0, min(r["dang_muon"], r["so_luong"] - r["hong"]))
    db.execute("UPDATE thiet_bi SET so_luong=?, hong=?, dang_muon=?, vi_tri=?, ghi_chu=?, cap_nhat=? WHERE stt=?",
               (r["so_luong"], r["hong"], r["dang_muon"], r["vi_tri"], r["ghi_chu"], time.time(), stt))
    return {**db.EQUIP_BY_STT[stt], **r}


@router.get("/nhat-ky")
def nhat_ky(loai: str = "", gioi_han: int = 200) -> list[dict]:
    if loai:
        return db.query("SELECT * FROM nhat_ky WHERE loai=? ORDER BY id DESC LIMIT ?", (loai, gioi_han))
    return db.query("SELECT * FROM nhat_ky ORDER BY id DESC LIMIT ?", (gioi_han,))


class GhiNhatKy(BaseModel):
    loai: str = "ghi_chu"
    tieu_de: str
    noi_dung: str = ""
    muc_do: str = "Info"
    nguon: str = "web"


@router.post("/nhat-ky")
def ghi_nhat_ky(body: GhiNhatKy) -> dict:
    return {"id": db.log(body.loai, body.tieu_de[:300], body.noi_dung[:2000], body.muc_do, body.nguon)}


# ---------------------------------------------------------------------------
# Mạng lưới khóa API
# ---------------------------------------------------------------------------

@router.get("/khoa")
def ds_khoa() -> dict:
    return {"nha_cung_cap": keypool.PROVIDERS, "ds": keypool.list_keys()}


class ThemKhoa(BaseModel):
    nha_cung_cap: str
    khoa: str
    nhan: str = ""
    mo_hinh: str = ""
    uu_tien: int = 10


@router.post("/khoa")
def them_khoa(body: ThemKhoa) -> dict:
    try:
        return {"id": keypool.add_key(body.nha_cung_cap, body.khoa, body.nhan, body.mo_hinh, body.uu_tien)}
    except ValueError as e:
        raise HTTPException(400, str(e))


class SuaKhoa(BaseModel):
    nhan: str | None = None
    mo_hinh: str | None = None
    uu_tien: int | None = None
    bat: bool | None = None


@router.patch("/khoa/{key_id}")
def sua_khoa(key_id: int, body: SuaKhoa) -> dict:
    keypool.update_key(key_id, **body.model_dump())
    return {"ok": True}


@router.delete("/khoa/{key_id}")
def xoa_khoa(key_id: int) -> dict:
    keypool.delete_key(key_id)
    return {"ok": True}


@router.post("/khoa/{key_id}/thu")
async def thu_khoa(key_id: int) -> dict:
    try:
        return await keypool.test_key(key_id)
    except ValueError as e:
        raise HTTPException(404, str(e))


class HoiThu(BaseModel):
    cau_hoi: str


@router.post("/ai/thu")
async def ai_thu(body: HoiThu) -> dict:
    try:
        res = await keypool.chat([{"role": "system", "content": "Bạn là trợ giảng hóa học THPT. Trả lời ngắn bằng tiếng Việt."},
                                  {"role": "user", "content": body.cau_hoi}], 400)
    except keypool.AllKeysFailed as e:
        raise HTTPException(503, str(e))
    return {"tra_loi": res["message"].get("content", ""), "nha_cung_cap": res["nha_cung_cap"], "mo_hinh": res["mo_hinh"], "khoa_id": res["khoa_id"]}
