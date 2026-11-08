"""Trình chiếu: giáo viên tải bài giảng lên web, chọn trang; app của học sinh trong lớp hiện trang đó lên màn hình trong phòng ảo.

    GET, POST /api/bai-giang, DELETE /api/bai-giang/{id}     bài giảng của giáo viên (PDF hoặc ảnh)
    GET  /api/bai-giang/{id}/trang/{so}                      ảnh một trang: chủ bài giảng, học sinh lớp đang chiếu bài đó
    GET, POST /api/lop/{id}/trinh-chieu                      đang chiếu gì, bao nhiêu máy đã hiện; đổi trang, đưa chữ, tắt

App nhận việc phải hiện qua POST /app/nhip (server/lab/truc_tiep.py). Mọi trang được đưa về ảnh JPEG 1600×900, nên tải lên dạng gì thì màn hình trong app cũng không bị méo.
"""

from __future__ import annotations

import io
import re
import shutil
import time
from pathlib import Path

import pypdfium2
from fastapi import APIRouter, File, Form, HTTPException, Request, UploadFile
from fastapi.responses import FileResponse
from PIL import Image, UnidentifiedImageError
from pydantic import BaseModel, Field

from . import db, nhip, tai_khoan
from .nhip import GIAY_CON_MO, xoa as xoa_nhip  # noqa: F401  (kiểm thử dùng qua module này)

router = APIRouter()

KHO_RONG, KHO_CAO = 1600, 900
TOI_DA_MB = 60
TOI_DA_TRANG = 200
MAN_HINH_MAC_DINH = ["tuong_video", "t2_tuong_video"]
_MAN_HINH_HOP_LE = re.compile(r"(t2_)?(tuong_video|ban|hanh_lang)(_\d+)?")
Image.MAX_IMAGE_PIXELS = 40_000_000  # ảnh lớn hơn thế này bị coi là ảnh bom, không giải nén


def thu_muc_bai(bai_id: int) -> Path:
    return db.DB_PATH.parent / "bai_giang" / str(bai_id)


# ---------------------------------------------------------------------------
# Bài giảng
# ---------------------------------------------------------------------------

def _khop_kho(anh: Image.Image) -> Image.Image:
    """Thu vừa khung 16:9 trên nền trắng, giữ nguyên tỉ lệ."""
    anh = anh.convert("RGB")
    anh.thumbnail((KHO_RONG, KHO_CAO))
    if anh.width < KHO_RONG and anh.height < KHO_CAO:  # ảnh nhỏ: phóng lên cho vừa, màn hình trong phòng rất lớn
        ti_le = min(KHO_RONG / anh.width, KHO_CAO / anh.height)
        anh = anh.resize((round(anh.width * ti_le), round(anh.height * ti_le)))
    nen = Image.new("RGB", (KHO_RONG, KHO_CAO), "white")
    nen.paste(anh, ((KHO_RONG - anh.width) // 2, (KHO_CAO - anh.height) // 2))
    return nen


def _trang_cua_tep(du_lieu: bytes, ten_tep: str):
    """Lần lượt từng ảnh trang của một tệp. Nhận dạng theo nội dung, không tin đuôi tệp."""
    if du_lieu[:5] == b"%PDF-":
        try:
            pdf = pypdfium2.PdfDocument(du_lieu)
        except pypdfium2.PdfiumError:
            raise HTTPException(422, f"Không đọc được PDF {ten_tep} (hỏng hoặc có mật khẩu)") from None
        if len(pdf) > TOI_DA_TRANG:
            raise HTTPException(422, f"Mỗi bài giảng tối đa {TOI_DA_TRANG} trang")
        for trang in pdf:
            ti_le = min(KHO_RONG / max(trang.get_width(), 1), KHO_CAO / max(trang.get_height(), 1))
            yield trang.render(scale=ti_le).to_pil()
        return
    try:
        anh = Image.open(io.BytesIO(du_lieu))
        anh.load()
    except (UnidentifiedImageError, Image.DecompressionBombError, OSError):
        raise HTTPException(422, f"{ten_tep} không phải PDF hoặc ảnh dùng được") from None
    yield anh


def _bai_cua_toi(request: Request, bai_id: int) -> dict:
    rows = db.query("SELECT * FROM bai_giang WHERE id=? AND giao_vien_id=?", (bai_id, request.state.nguoi_dung["id"]))
    if not rows:
        raise HTTPException(404, "Không có bài giảng này")
    return rows[0]


@router.get("/api/bai-giang")
def ds_bai_giang(request: Request) -> list[dict]:
    return db.query("SELECT id, ten, so_trang, tao_luc FROM bai_giang WHERE giao_vien_id=? ORDER BY id DESC", (request.state.nguoi_dung["id"],))


@router.post("/api/bai-giang")
def tai_bai_giang(request: Request, ten: str = Form(max_length=120), tep: list[UploadFile] = File()) -> dict:
    """Tải một PDF hoặc nhiều ảnh (xếp theo tên tệp). Chỉ giữ ảnh đã chuyển đổi, không giữ tệp gốc."""
    ten = ten.strip()
    if not ten:
        raise HTTPException(422, "Hãy đặt tên bài giảng")
    con_lai = TOI_DA_MB * 1024 * 1024
    trang: list[bytes] = []
    for t in sorted(tep, key=lambda f: f.filename or ""):
        du_lieu = t.file.read(con_lai + 1)
        con_lai -= len(du_lieu)
        if con_lai < 0:
            raise HTTPException(413, f"Bài giảng lớn quá {TOI_DA_MB} MB")
        for anh in _trang_cua_tep(du_lieu, t.filename or "tệp"):
            if len(trang) >= TOI_DA_TRANG:
                raise HTTPException(422, f"Mỗi bài giảng tối đa {TOI_DA_TRANG} trang")
            ra = io.BytesIO()
            _khop_kho(anh).save(ra, "JPEG", quality=85)
            trang.append(ra.getvalue())
    if not trang:
        raise HTTPException(422, "Tệp không có trang nào")
    cur = db.execute("INSERT INTO bai_giang (giao_vien_id, ten, so_trang, tao_luc) VALUES (?,?,?,?)",
                     (request.state.nguoi_dung["id"], ten, len(trang), time.time()))
    thu_muc = thu_muc_bai(cur.lastrowid)
    thu_muc.mkdir(parents=True, exist_ok=True)
    for so, du_lieu in enumerate(trang, 1):
        (thu_muc / f"{so}.jpg").write_bytes(du_lieu)
    return {"id": cur.lastrowid, "ten": ten, "so_trang": len(trang)}


@router.delete("/api/bai-giang/{bai_id}")
def xoa_bai_giang(bai_id: int, request: Request) -> dict:
    _bai_cua_toi(request, bai_id)
    for r in db.query("SELECT lop_id FROM trinh_chieu WHERE bai_id=? AND loai='slide'", (bai_id,)):
        _ghi(r["lop_id"], "tat")
    db.execute("DELETE FROM bai_giang WHERE id=?", (bai_id,))
    shutil.rmtree(thu_muc_bai(bai_id), ignore_errors=True)
    return {"ok": True}


@router.get("/api/bai-giang/{bai_id}/trang/{so}")
def anh_trang(bai_id: int, so: int, request: Request) -> FileResponse:
    nd = request.state.nguoi_dung
    duoc_xem = db.query("SELECT 1 FROM bai_giang WHERE id=? AND giao_vien_id=?", (bai_id, nd["id"])) or db.query(
        """SELECT 1 FROM trinh_chieu t JOIN thanh_vien_lop m ON m.lop_id=t.lop_id
           WHERE t.bai_id=? AND t.loai='slide' AND m.hoc_sinh_id=?""", (bai_id, nd["id"]))
    tep = thu_muc_bai(bai_id) / f"{so}.jpg"
    if not duoc_xem or not tep.is_file():
        raise HTTPException(404, "Không có trang này")
    return FileResponse(tep, media_type="image/jpeg")


# ---------------------------------------------------------------------------
# Đang chiếu
# ---------------------------------------------------------------------------

class DoiTrinhChieu(BaseModel):
    loai: str  # slide | chu | tat
    bai_id: int | None = None
    trang: int = 1
    tieu_de: str = Field(default="", max_length=200)
    noi_dung: str = Field(default="", max_length=2000)
    man_hinh: list[str] = Field(default_factory=lambda: list(MAN_HINH_MAC_DINH), max_length=20)
    dem_nguoc_giay: int = Field(default=0, ge=0, le=7200)


def _ghi(lop_id: int, loai: str, bai_id: int | None = None, trang: int = 1, tieu_de: str = "", noi_dung: str = "",
         man_hinh: list[str] | None = None, dem_nguoc_den: float = 0) -> None:
    """phien_ban luôn tăng, để app biết có việc mới mà không phải so từng trường."""
    cu = db.query("SELECT phien_ban FROM trinh_chieu WHERE lop_id=?", (lop_id,))
    now = time.time()
    phien_ban = max(int(now * 1000), cu[0]["phien_ban"] + 1 if cu else 0)
    db.execute("INSERT OR REPLACE INTO trinh_chieu (lop_id, phien_ban, loai, bai_id, trang, tieu_de, noi_dung, man_hinh, dem_nguoc_den, cap_nhat) VALUES (?,?,?,?,?,?,?,?,?,?)",
               (lop_id, phien_ban, loai, bai_id, trang, tieu_de, noi_dung, ",".join(man_hinh or MAN_HINH_MAC_DINH), dem_nguoc_den, now))


def cho_app(r: dict | None) -> dict:
    if r is None or r["loai"] == "tat":
        return {"phien_ban": r["phien_ban"] if r else 0, "loai": "tat", "tieu_de": "", "noi_dung": "", "anh": "", "man_hinh": [], "dem_nguoc_den": 0}
    anh = f"/api/bai-giang/{r['bai_id']}/trang/{r['trang']}" if r["loai"] == "slide" else ""
    return {"phien_ban": r["phien_ban"], "loai": r["loai"], "tieu_de": r["tieu_de"], "noi_dung": r["noi_dung"], "anh": anh,
            "man_hinh": r["man_hinh"].split(","), "dem_nguoc_den": r["dem_nguoc_den"]}


def _lop_cua_toi(request: Request, lop_id: int) -> dict:
    lop = tai_khoan.lop_cua_giao_vien(lop_id, request.state.nguoi_dung["id"])
    if lop is None:
        raise HTTPException(404, "Không có lớp này")
    return lop


def _trang_thai_lop(lop_id: int) -> dict:
    rows = db.query("SELECT * FROM trinh_chieu WHERE lop_id=?", (lop_id,))
    r = rows[0] if rows else None
    dang = cho_app(r)
    if r and r["loai"] == "slide":
        bai = db.query("SELECT ten, so_trang FROM bai_giang WHERE id=?", (r["bai_id"],))
        dang.update(bai_id=r["bai_id"], trang=r["trang"], ten_bai=bai[0]["ten"] if bai else "", so_trang=bai[0]["so_trang"] if bai else 0)
    hoc_sinh = tai_khoan.hoc_sinh_cua_lop(lop_id)
    now = time.time()
    cac_nhip = [nhip.doc(h["id"]) for h in hoc_sinh]
    dang_mo = [n for n in cac_nhip if n and now - n["luc"] < GIAY_CON_MO]
    da_hien = [n for n in dang_mo if dang["phien_ban"] and n["da_hien"] == dang["phien_ban"]]
    tre = [n["do_tre_ms"] for n in da_hien if n.get("do_tre_ms") is not None]
    return {"dang_chieu": dang, "so_hoc_sinh": len(hoc_sinh), "may_dang_mo": len(dang_mo), "may_da_hien": len(da_hien),
            "do_tre_ms": round(sum(tre) / len(tre)) if tre else None}


@router.get("/api/lop/{lop_id}/trinh-chieu")
def xem_trinh_chieu(lop_id: int, request: Request) -> dict:
    _lop_cua_toi(request, lop_id)
    return _trang_thai_lop(lop_id)


@router.post("/api/lop/{lop_id}/trinh-chieu")
def doi_trinh_chieu(lop_id: int, body: DoiTrinhChieu, request: Request) -> dict:
    _lop_cua_toi(request, lop_id)
    for m in body.man_hinh:
        if not _MAN_HINH_HOP_LE.fullmatch(m):
            raise HTTPException(422, f"Không có màn hình '{m}'")
    if not body.man_hinh and body.loai != "tat":
        raise HTTPException(422, "Hãy chọn ít nhất một màn hình")
    dem_nguoc_den = time.time() + body.dem_nguoc_giay if body.dem_nguoc_giay else 0
    if body.loai == "slide":
        bai = _bai_cua_toi(request, body.bai_id or 0)
        if not 1 <= body.trang <= bai["so_trang"]:
            raise HTTPException(422, f"Bài giảng này chỉ có {bai['so_trang']} trang")
        _ghi(lop_id, "slide", bai["id"], body.trang, f"{bai['ten']} · Trang {body.trang}/{bai['so_trang']}", "", body.man_hinh, dem_nguoc_den)
    elif body.loai == "chu":
        if not body.tieu_de.strip():
            raise HTTPException(422, "Hãy nhập tiêu đề")
        _ghi(lop_id, "chu", None, 1, body.tieu_de.strip(), body.noi_dung.strip(), body.man_hinh, dem_nguoc_den)
    elif body.loai == "tat":
        _ghi(lop_id, "tat")
    else:
        raise HTTPException(422, "Loại phải là slide, chu hoặc tat")
    return _trang_thai_lop(lop_id)


def viec_cua_lop(lop: list[int]) -> dict | None:
    """Lớp mới đổi gần nhất trong các lớp người này thuộc về (học sinh) hoặc đang dạy (giáo viên); lớp còn chiếu được ưu tiên hơn lớp đã tắt."""
    if not lop:
        return None
    rows = db.query(f"SELECT * FROM trinh_chieu WHERE lop_id IN ({','.join('?' * len(lop))}) ORDER BY (loai='tat'), cap_nhat DESC LIMIT 1", tuple(lop))
    return rows[0] if rows else None
