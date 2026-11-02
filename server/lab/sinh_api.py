"""FastAPI Router cho phòng thí nghiệm sinh học Tầng 2 (/lab/sinh và /sinh).
Tuân thủ hợp đồng giao diện - backend (HOP_DONG_GIAO_DIEN_BACKEND.md).
"""

from __future__ import annotations

import csv
import io
import time
from pathlib import Path
from typing import Any, List, Optional

from fastapi import APIRouter, HTTPException, Query, Response
from fastapi.responses import FileResponse, HTMLResponse
from pydantic import BaseModel, Field

from . import sinh_db

router = APIRouter()

ROOT = Path(__file__).resolve().parents[2]
WEB_SINH_HTML = ROOT / "server" / "web" / "sinh.html"


# ==============================================================================
# PYDANTIC SCHEMAS
# ==============================================================================

class DungCuPatch(BaseModel):
    so_luong: Optional[int] = None
    hong: Optional[int] = None
    dang_muon: Optional[int] = None
    can_bao_tri: Optional[int] = None
    vi_tri: Optional[str] = None
    lan_hieu_chuan: Optional[float] = None
    han_hieu_chuan: Optional[float] = None
    ghi_chu: Optional[str] = None


class SinhPhamCreate(BaseModel):
    id: str
    ten: str
    loai: str
    tu: str
    ngan: Optional[str] = ""
    so_luong: float = 0.0
    don_vi: str = ""
    so_lo: Optional[str] = ""
    han_dung: Optional[float] = None
    han_dung_ngay: Optional[float] = None
    cap_an_toan: str = "BSL-1"
    ma_chat: Optional[str] = ""


class SinhPhamPatch(BaseModel):
    ten: Optional[str] = None
    loai: Optional[str] = None
    tu: Optional[str] = None
    ngan: Optional[str] = None
    so_luong: Optional[float] = None
    don_vi: Optional[str] = None
    so_lo: Optional[str] = None
    han_dung: Optional[float] = None
    cap_an_toan: Optional[str] = None
    ma_chat: Optional[str] = None


class SinhPhamXuatNhap(BaseModel):
    so_luong: float
    nguoi: str = ""
    ghi_chu: str = ""


class NhietDoCreate(BaseModel):
    tu: str
    nhiet_do: float
    cua_mo: int = 0
    thoi_gian: Optional[float] = None


class DatLichCreate(BaseModel):
    doi_tuong: str
    nguoi: str
    lop: str
    bat_dau: float
    ket_thuc: float
    muc_dich: Optional[str] = ""


class DatLichPatch(BaseModel):
    doi_tuong: Optional[str] = None
    nguoi: Optional[str] = None
    lop: Optional[str] = None
    bat_dau: Optional[float] = None
    ket_thuc: Optional[float] = None
    muc_dich: Optional[str] = None


class MuonTraCreate(BaseModel):
    ma_dung_cu: str
    so_luong: int = 1
    nguoi: str
    lop: str
    han_tra: Optional[float] = None


class MuonTraPatch(BaseModel):
    luc_tra: Optional[float] = None
    tinh_trang_tra: str = "tot"


class KetQuaCreate(BaseModel):
    hoc_sinh: str
    lop: str
    ban: Optional[str] = ""
    ma_bai: str
    thoi_gian: Optional[float] = None
    du_lieu: Any = Field(default_factory=dict)
    diem: Optional[float] = None
    nhan_xet: Optional[str] = ""


class KetQuaPatch(BaseModel):
    diem: Optional[float] = None
    nhan_xet: Optional[str] = None
    du_lieu: Optional[Any] = None


class SuCoCreate(BaseModel):
    phong: str
    loai: str
    muc_do: Optional[str] = "Info"
    mo_ta: str
    xu_ly: Optional[str] = ""
    trang_thai: Optional[str] = "moi"
    thoi_gian: Optional[float] = None


class SuCoPatch(BaseModel):
    muc_do: Optional[str] = None
    mo_ta: Optional[str] = None
    xu_ly: Optional[str] = None
    trang_thai: Optional[str] = None


class BaoTriCreate(BaseModel):
    ma_dung_cu: str
    loai: str
    ngay: Optional[float] = None
    nguoi: str
    ket_qua: str
    ghi_chu: Optional[str] = ""


class SuKienItem(BaseModel):
    loai: str
    thoi_gian: Optional[float] = None
    du_lieu: Any = Field(default_factory=dict)


class SuKienBatch(BaseModel):
    su_kien: List[SuKienItem] = Field(default_factory=list)


# ==============================================================================
# ENDPOINTS /lab/sinh
# ==============================================================================

@router.get("/lab/sinh/tong-quan")
def get_tong_quan() -> dict:
    return sinh_db.get_tong_quan()


@router.get("/lab/sinh/dung-cu")
def get_dung_cu(
    phong: str = Query("", description="Lọc theo phòng"),
    phan_loai: str = Query("", description="Lọc theo phân loại"),
    tim: str = Query("", description="Tìm kiếm từ khóa"),
) -> dict:
    items = sinh_db.list_dung_cu(phong=phong, phan_loai=phan_loai, tim=tim)
    return {"muc": items, "tong": len(items)}


@router.patch("/lab/sinh/dung-cu/{ma}")
def patch_dung_cu(ma: str, body: DungCuPatch) -> dict:
    static = sinh_db.get_static_dung_cu(ma)
    if not static:
        raise HTTPException(status_code=404, detail=f"Không tìm thấy dụng cụ '{ma}'")
    updates = body.model_dump(exclude_unset=True)
    res = sinh_db.patch_dung_cu(ma, updates)
    # Gộp thông tin tĩnh
    merged = dict(static)
    merged.update(res)
    return merged


@router.get("/lab/sinh/sinh-pham")
def get_sinh_pham(
    tu: str = Query("", description="Lọc theo tủ"),
    loai: str = Query("", description="Lọc theo loại"),
    tim: str = Query("", description="Tìm kiếm mã hoặc tên"),
) -> dict:
    items = sinh_db.list_sinh_pham(tu=tu, loai=loai, tim=tim)
    return {"muc": items, "tong": len(items)}


@router.post("/lab/sinh/sinh-pham")
def post_sinh_pham(body: SinhPhamCreate) -> dict:
    data = body.model_dump()
    if data.get("han_dung") is None:
        han_ngay = data.get("han_dung_ngay") or 180.0
        data["han_dung"] = time.time() + han_ngay * 86400.0
    try:
        return sinh_db.add_sinh_pham(data)
    except ValueError as e:
        raise HTTPException(status_code=422, detail=str(e))


@router.patch("/lab/sinh/sinh-pham/{id}")
def patch_sinh_pham(id: str, body: SinhPhamPatch) -> dict:
    updates = body.model_dump(exclude_unset=True)
    try:
        res = sinh_db.patch_sinh_pham(id, updates)
        if not res:
            raise HTTPException(status_code=404, detail=f"Không tìm thấy sinh phẩm '{id}'")
        return res
    except ValueError as e:
        raise HTTPException(status_code=422, detail=str(e))


@router.delete("/lab/sinh/sinh-pham/{id}")
def delete_sinh_pham(id: str) -> dict:
    ok = sinh_db.delete_sinh_pham(id)
    if not ok:
        raise HTTPException(status_code=404, detail=f"Không tìm thấy sinh phẩm '{id}'")
    return {"thanh_cong": True, "id": id}


@router.post("/lab/sinh/sinh-pham/{id}/xuat")
def post_sinh_pham_xuat(id: str, body: SinhPhamXuatNhap) -> dict:
    try:
        return sinh_db.change_sinh_pham_stock(id, -abs(body.so_luong), body.nguoi, body.ghi_chu)
    except KeyError:
        raise HTTPException(status_code=404, detail=f"Không tìm thấy sinh phẩm '{id}'")


@router.post("/lab/sinh/sinh-pham/{id}/nhap")
def post_sinh_pham_nhap(id: str, body: SinhPhamXuatNhap) -> dict:
    try:
        return sinh_db.change_sinh_pham_stock(id, abs(body.so_luong), body.nguoi, body.ghi_chu)
    except KeyError:
        raise HTTPException(status_code=404, detail=f"Không tìm thấy sinh phẩm '{id}'")


@router.get("/lab/sinh/nhiet-do")
def get_nhiet_do(
    tu: str = Query("", description="Mã tủ"),
    tu_luc: float = Query(0.0, description="Lọc từ thời điểm Unix seconds"),
) -> dict:
    items = sinh_db.list_nhiet_do(tu=tu, tu_luc=tu_luc)
    return {"muc": items, "tong": len(items)}


@router.post("/lab/sinh/nhiet-do")
def post_nhiet_do(body: NhietDoCreate) -> dict:
    return sinh_db.add_nhiet_do(body.tu, body.nhiet_do, body.cua_mo, body.thoi_gian or 0.0)


@router.get("/lab/sinh/dat-lich")
def get_dat_lich(doi_tuong: str = Query("", description="Lọc theo đối tượng")) -> dict:
    items = sinh_db.list_dat_lich(doi_tuong=doi_tuong)
    return {"muc": items, "tong": len(items)}


@router.post("/lab/sinh/dat-lich")
def post_dat_lich(body: DatLichCreate) -> dict:
    try:
        res, conflicts = sinh_db.add_dat_lich(body.model_dump())
        if conflicts:
            raise HTTPException(
                status_code=409,
                detail={"detail": "Trùng lịch đặt với lịch đã có", "trung": conflicts},
            )
        return res
    except ValueError as e:
        raise HTTPException(status_code=422, detail=str(e))


@router.patch("/lab/sinh/dat-lich/{id}")
def patch_dat_lich(id: int, body: DatLichPatch) -> dict:
    try:
        res, conflicts = sinh_db.patch_dat_lich(id, body.model_dump(exclude_unset=True))
        if conflicts:
            raise HTTPException(
                status_code=409,
                detail={"detail": "Trùng lịch đặt với lịch đã có", "trung": conflicts},
            )
        if not res:
            raise HTTPException(status_code=404, detail=f"Không tìm thấy lịch id={id}")
        return res
    except ValueError as e:
        raise HTTPException(status_code=422, detail=str(e))


@router.delete("/lab/sinh/dat-lich/{id}")
def delete_dat_lich(id: int) -> dict:
    ok = sinh_db.delete_dat_lich(id)
    if not ok:
        raise HTTPException(status_code=404, detail=f"Không tìm thấy lịch id={id}")
    return {"thanh_cong": True, "id": id}


@router.get("/lab/sinh/muon-tra")
def get_muon_tra(
    ma_dung_cu: str = Query("", description="Lọc theo mã dụng cụ"),
    nguoi: str = Query("", description="Lọc theo người mượn"),
) -> dict:
    items = sinh_db.list_muon_tra(ma_dung_cu=ma_dung_cu, nguoi=nguoi)
    return {"muc": items, "tong": len(items)}


@router.post("/lab/sinh/muon-tra")
def post_muon_tra(body: MuonTraCreate) -> dict:
    return sinh_db.add_muon_tra(body.model_dump())


@router.patch("/lab/sinh/muon-tra/{id}")
def patch_muon_tra(id: int, body: MuonTraPatch) -> dict:
    res = sinh_db.patch_muon_tra(id, body.model_dump(exclude_unset=True))
    if not res:
        raise HTTPException(status_code=404, detail=f"Không tìm thấy bản ghi mượn trả id={id}")
    return res


@router.get("/lab/sinh/ket-qua")
def get_ket_qua(
    lop: str = Query("", description="Lọc theo lớp"),
    ma_bai: str = Query("", description="Lọc theo mã bài thí nghiệm"),
    hoc_sinh: str = Query("", description="Lọc theo tên học sinh"),
) -> dict:
    items = sinh_db.list_ket_qua(lop=lop, ma_bai=ma_bai, hoc_sinh=hoc_sinh)
    return {"muc": items, "tong": len(items)}


@router.get("/lab/sinh/ket-qua/{id}")
def get_ket_qua_item(id: int) -> dict:
    item = sinh_db.get_ket_qua_by_id(id)
    if not item:
        raise HTTPException(status_code=404, detail=f"Không tìm thấy kết quả id={id}")
    return item


@router.post("/lab/sinh/ket-qua")
def post_ket_qua(body: KetQuaCreate) -> dict:
    return sinh_db.add_ket_qua(body.model_dump())


@router.patch("/lab/sinh/ket-qua/{id}")
def patch_ket_qua(id: int, body: KetQuaPatch) -> dict:
    res = sinh_db.patch_ket_qua(id, body.model_dump(exclude_unset=True))
    if not res:
        raise HTTPException(status_code=404, detail=f"Không tìm thấy kết quả id={id}")
    return res


@router.get("/lab/sinh/ket-qua.csv")
def get_ket_qua_csv() -> Response:
    """Xuất danh sách kết quả thực hành ra định dạng CSV UTF-8 có BOM."""
    items = sinh_db.list_ket_qua()
    output = io.StringIO()
    writer = csv.writer(output)
    writer.writerow(["ID", "Học sinh", "Lớp", "Bàn", "Mã bài", "Thời gian", "Điểm", "Nhận xét", "Dữ liệu JSON"])
    for r in items:
        writer.writerow([
            r.get("id", ""),
            r.get("hoc_sinh", ""),
            r.get("lop", ""),
            r.get("ban", ""),
            r.get("ma_bai", ""),
            r.get("thoi_gian", ""),
            r.get("diem", ""),
            r.get("nhan_xet", ""),
            r.get("du_lieu", ""),
        ])

    csv_text = output.getvalue()
    # Thêm ký tự UTF-8 BOM (\xef\xbb\xbf) để Excel trên Windows/macOS mở không bị lỗi font tiếng Việt
    bom_csv_bytes = b"\xef\xbb\xbf" + csv_text.encode("utf-8")
    return Response(
        content=bom_csv_bytes,
        media_type="text/csv; charset=utf-8",
        headers={"Content-Disposition": "attachment; filename=ket_qua_sinh_hoc.csv"},
    )


@router.get("/lab/sinh/su-co")
def get_su_co(
    phong: str = Query("", description="Lọc theo phòng"),
    trang_thai: str = Query("", description="Lọc theo trạng thái"),
) -> dict:
    items = sinh_db.list_su_co(phong=phong, trang_thai=trang_thai)
    return {"muc": items, "tong": len(items)}


@router.post("/lab/sinh/su-co")
def post_su_co(body: SuCoCreate) -> dict:
    return sinh_db.add_su_co(body.model_dump())


@router.patch("/lab/sinh/su-co/{id}")
def patch_su_co(id: int, body: SuCoPatch) -> dict:
    res = sinh_db.patch_su_co(id, body.model_dump(exclude_unset=True))
    if not res:
        raise HTTPException(status_code=404, detail=f"Không tìm thấy sự cố id={id}")
    return res


@router.get("/lab/sinh/bao-tri")
def get_bao_tri(ma_dung_cu: str = Query("", description="Mã dụng cụ")) -> dict:
    items = sinh_db.list_bao_tri(ma_dung_cu=ma_dung_cu)
    return {"muc": items, "tong": len(items)}


@router.post("/lab/sinh/bao-tri")
def post_bao_tri(body: BaoTriCreate) -> dict:
    return sinh_db.add_bao_tri(body.model_dump())


@router.get("/lab/sinh/huong-dan/{ma}")
def get_huong_dan(ma: str) -> dict:
    static = sinh_db.get_static_dung_cu(ma)
    if not static:
        raise HTTPException(status_code=404, detail=f"Không tìm thấy hướng dẫn cho dụng cụ '{ma}'")
    return static


@router.get("/lab/sinh/bai-thi-nghiem")
def get_bai_thi_nghiem() -> dict:
    items = sinh_db.get_all_static_bai_thi_nghiem()
    return {"muc": items, "tong": len(items)}


# Trạng thái mặc định của 8 máy chính khi Unity chưa gửi gì (hoặc chưa mở app)
_MAY_MAC_DINH = [
    ("kinh_hien_vi", "Kính hiển vi quang học (29 máy)"),
    ("tu_an_toan_sinh_hoc", "Tủ an toàn sinh học cấp II A2"),
    ("tu_am_vi_sinh", "Tủ ấm vi sinh 37 °C"),
    ("noi_hap", "Nồi hấp tiệt trùng 121 °C"),
    ("may_pcr", "Máy luân nhiệt PCR 96 giếng"),
    ("be_dien_di", "Bể điện di ngang + bộ nguồn"),
    ("gel_doc", "Buồng chụp gel UV / Blue LED"),
    ("may_do_od600", "Máy đo quang phổ OD600"),
    ("tram_ghep_gen", "Trạm ghép gen (ADN tái tổ hợp)"),
]
_TRANG_THAI_HOP_LE = {"san_sang", "dang_chay", "canh_bao", "hong"}
# Hợp đồng mục 5: trạng thái máy chỉ giữ trong bộ nhớ máy chủ (không ghi CSDL)
_trang_thai_may: dict[str, dict] = {}


def ghi_trang_thai_may(ma: str, trang_thai: str, mo_ta: str = "", tien_do: float = 0.0, luc: float | None = None) -> dict:
    if not ma:
        raise ValueError("thiếu mã máy 'ma'")
    if trang_thai not in _TRANG_THAI_HOP_LE:
        raise ValueError(f"trạng thái '{trang_thai}' không hợp lệ (san_sang | dang_chay | canh_bao | hong)")
    muc = {
        "ma": ma,
        "trang_thai": trang_thai,
        "mo_ta": mo_ta,
        "tien_do": max(0.0, min(1.0, float(tien_do))),
        "cap_nhat": luc or time.time(),
        "nguon": "unity",
    }
    _trang_thai_may[ma] = muc
    return muc


@router.get("/lab/sinh/trang-thai-may")
def get_trang_thai_may() -> dict:
    """Trạng thái các máy sinh học chính: số Unity gửi lên (sự kiện trang_thai_may) đè lên trạng thái mặc định."""
    muc = []
    da_co = set()
    for ma, ten in _MAY_MAC_DINH:
        if ma in _trang_thai_may:
            muc.append(dict(_trang_thai_may[ma]))
        else:
            muc.append({"ma": ma, "trang_thai": "san_sang", "mo_ta": f"{ten}: chưa có số liệu từ phòng ảo", "tien_do": 0.0, "cap_nhat": 0.0, "nguon": "mac_dinh"})
        da_co.add(ma)
    for ma, item in _trang_thai_may.items():
        if ma not in da_co:
            muc.append(dict(item))
    return {"muc": muc, "tong": len(muc)}


def _ap_su_kien(loai: str, dl: dict, t: float) -> None:
    """Ghi một sự kiện Unity gửi lên theo bảng 'Loại sự kiện' (HOP_DONG_GIAO_DIEN_BACKEND.md mục 5)."""
    if loai == "su_co":
        sinh_db.add_su_co({
            "phong": dl.get("phong", "phong_sinh"),
            "loai": dl.get("loai", "su_kien_lab"),
            "muc_do": dl.get("muc_do", "Info"),
            "mo_ta": dl.get("mo_ta", str(dl)),
            "xu_ly": dl.get("xu_ly", ""),
            "trang_thai": dl.get("trang_thai", "moi"),
            "thoi_gian": t,
        })
    elif loai == "dung_cu_hong":
        ma = dl.get("ma", "")
        static = sinh_db.get_static_dung_cu(ma)
        if not static:
            raise ValueError(f"không có dụng cụ '{ma}'")
        hien = sinh_db.query("SELECT hong FROM sinh_dung_cu WHERE ma=?", (ma,))
        hong = (hien[0]["hong"] if hien else 0) + max(1, int(dl.get("so_luong", 1)))
        sinh_db.patch_dung_cu(ma, {"hong": hong})
        phong = dl.get("phong") or static.get("phong") or "phong_sinh"
        if isinstance(phong, list):  # dung_cu.json ghi "phong" là danh sách các phòng có dụng cụ này
            phong = phong[0] if phong else "phong_sinh"
        sinh_db.add_su_co({
            "phong": phong,
            "loai": "vo_dung_cu",
            "muc_do": "canh_bao",
            "mo_ta": f"[{ma}] {dl.get('ly_do', 'Dụng cụ hỏng trong phòng ảo')}",
            "thoi_gian": t,
        })
    elif loai == "can_bao_tri":
        ma = dl.get("ma", "")
        if not sinh_db.get_static_dung_cu(ma):
            raise ValueError(f"không có dụng cụ '{ma}'")
        sinh_db.patch_dung_cu(ma, {"can_bao_tri": 1, "ghi_chu": dl.get("ly_do", "")})
    elif loai in ("sinh_pham_xuat", "sinh_pham_nhap"):
        so_luong = abs(float(dl.get("so_luong", 1)))
        delta = -so_luong if loai == "sinh_pham_xuat" else so_luong
        try:
            sinh_db.change_sinh_pham_stock(dl.get("id", ""), delta, dl.get("nguoi", "Học sinh"), dl.get("ghi_chu", ""))
        except KeyError as e:
            raise ValueError(str(e))
    elif loai in ("nhiet_do_tu", "nhiet_do"):
        sinh_db.add_nhiet_do(dl.get("tu", "tu_mat_4"), float(dl.get("nhiet_do", 4.0)), int(bool(dl.get("cua_mo", 0))), t)
    elif loai == "trang_thai_may":
        ghi_trang_thai_may(dl.get("ma", ""), dl.get("trang_thai", "san_sang"), dl.get("mo_ta", ""), float(dl.get("tien_do", 0.0)), t)
    elif loai in ("ket_qua_tn", "ket_qua"):
        sinh_db.add_ket_qua({
            "hoc_sinh": dl.get("hoc_sinh", "Học sinh"),
            "lop": dl.get("lop", "Lớp"),
            "ban": dl.get("ban", ""),
            "ma_bai": dl.get("ma_bai", "bai_chua_dat_ten"),
            "thoi_gian": t,
            "du_lieu": dl.get("du_lieu", {}),
            "diem": dl.get("diem"),
            "nhan_xet": dl.get("nhan_xet", ""),
        })
    else:
        raise ValueError(f"loại sự kiện '{loai}' không có trong hợp đồng")


@router.post("/lab/sinh/su-kien")
def post_su_kien(batch: SuKienBatch) -> dict:
    nhan = 0
    loi = []
    for i, item in enumerate(batch.su_kien):
        try:
            dl = item.du_lieu if isinstance(item.du_lieu, dict) else {}
            _ap_su_kien(item.loai, dl, item.thoi_gian or time.time())
            nhan += 1
        except Exception as e:
            loi.append({"chi_so": i, "ly_do": str(e)})

    return {"nhan": nhan, "loi": loi}


@router.get("/sinh")
def get_sinh_page() -> Response:
    """Phục vụ trang web quản lý phòng thí nghiệm sinh học (server/web/sinh.html)."""
    if WEB_SINH_HTML.exists():
        return FileResponse(WEB_SINH_HTML)

    fallback_html = """<!DOCTYPE html>
<html lang="vi">
<head>
    <meta charset="UTF-8">
    <title>Phòng Lab Sinh học VERA - Tầng 2</title>
    <style>
        body { font-family: system-ui, sans-serif; background: #0b1320; color: #e2e8f0; padding: 40px; text-align: center; }
        .card { max-width: 650px; margin: 40px auto; background: #1e293b; padding: 32px; border-radius: 12px; border: 1px solid #334155; }
        h1 { color: #38bdf8; }
        a { color: #4ade80; text-decoration: none; font-weight: bold; }
    </style>
</head>
<body>
    <div class="card">
        <h1>Phòng Lab Sinh học VERA (Tầng 2)</h1>
        <p>Hệ thống Backend FastAPI và CSDL SQLite đã sẵn sàng phục vụ 108 dụng cụ và 15 bài thí nghiệm sinh học chuẩn SGK.</p>
        <p>Tài liệu API Swagger UI đầy đủ tại: <a href="/docs">/docs</a></p>
    </div>
</body>
</html>"""
    return HTMLResponse(content=fallback_html, status_code=200)
