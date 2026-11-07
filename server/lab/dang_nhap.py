"""Đăng nhập và phân quyền cho toàn bộ máy chủ.

    POST /auth/dang-nhap     {ten_dang_nhap, mat_khau, kieu?}: web nhận cookie HttpOnly; kieu="ung_dung" nhận ma_phien (Unity)
    POST /auth/dang-ky       {ma_lop, ho_ten, ten_dang_nhap, mat_khau, kieu?}: học sinh tự tạo tài khoản bằng mã lớp đang mở
    POST /auth/dang-xuat     hủy phiên hiện tại
    GET  /auth/toi           người đang đăng nhập
    POST /auth/doi-mat-khau  {mat_khau_cu, mat_khau_moi}: hủy mọi phiên khác

Quyền kiểm tra tại một chỗ (kiem_quyen), chạy trước mọi đường dẫn. Đường dẫn không có trong QUYEN thì chỉ giáo viên vào được,
nên thêm điểm API mới mà quên khai báo vẫn an toàn.

Ngoại lệ cho Unity: ứng dụng chạy cùng máy với máy chủ gọi được các đường dẫn UNITY mà không cần đăng nhập (chế độ dùng thử,
bản .exe cũ). Lối này chỉ mở khi yêu cầu đến thẳng từ 127.0.0.1 và tắt nếu đặt KHKT_MO_MANG=1 hoặc có đầu mục của máy trung gian,
vì qua tunnel mọi yêu cầu đều trông như đến từ 127.0.0.1.
"""

from __future__ import annotations

import os
from pathlib import Path
from urllib.parse import quote, urlparse

from fastapi import APIRouter, FastAPI, HTTPException, Request, Response
from fastapi.responses import FileResponse, JSONResponse, RedirectResponse
from fastapi.staticfiles import StaticFiles
from pydantic import BaseModel, Field
from starlette.routing import Match

from . import tai_khoan

COOKIE = "vera_phien"
ROOT = Path(__file__).resolve().parents[2]
WEB = ROOT / "server" / "web"

CONG_KHAI, UNITY, DA_DANG_NHAP = "cong_khai", "unity", "da_dang_nhap"

QUYEN: dict[tuple[str, str], str] = {
    ("GET", "/dang-nhap"): CONG_KHAI,
    ("GET", "/tinh"): CONG_KHAI,
    ("POST", "/auth/dang-nhap"): CONG_KHAI,
    ("POST", "/auth/dang-ky"): CONG_KHAI,
    ("POST", "/auth/dang-xuat"): CONG_KHAI,
    ("GET", "/suc-khoe"): CONG_KHAI,  # nút Kiểm tra kết nối của ứng dụng; người ngoài không thấy tên nhà cung cấp AI
    # ứng dụng Unity đang gọi
    ("POST", "/chu-thich"): UNITY,
    ("POST", "/tro-ly"): UNITY,
    ("POST", "/robot/ke-hoach"): UNITY,
    ("POST", "/api/nhat-ky"): UNITY,
    ("POST", "/lab/sinh/su-kien"): UNITY,
    ("GET", "/lab/sinh/tong-quan"): UNITY,
    # giáo viên và học sinh đều xem được
    ("GET", "/"): DA_DANG_NHAP,
    ("GET", "/auth/toi"): DA_DANG_NHAP,
    ("POST", "/auth/doi-mat-khau"): DA_DANG_NHAP,
    ("GET", "/api/toi/ket-qua"): DA_DANG_NHAP,
    ("POST", "/app/nhip"): DA_DANG_NHAP,
    ("GET", "/api/bai-giang/{bai_id}/trang/{so}"): DA_DANG_NHAP,  # tự kiểm tra: chủ bài giảng hoặc học sinh lớp đang chiếu
    ("GET", "/hoc-sinh"): DA_DANG_NHAP,
    ("GET", "/may"): DA_DANG_NHAP,
    ("GET", "/may/{ma}"): DA_DANG_NHAP,
    ("GET", "/the-thong-tin/{phan_ung_id}"): DA_DANG_NHAP,
    ("POST", "/hoi"): DA_DANG_NHAP,
    ("GET", "/lab/sinh/huong-dan"): DA_DANG_NHAP,
    ("GET", "/lab/sinh/huong-dan/{ma}"): DA_DANG_NHAP,
    ("GET", "/lab/sinh/bai-thi-nghiem"): DA_DANG_NHAP,
    ("GET", "/lab/sinh/dat-lich"): DA_DANG_NHAP,
}
# Trang web: chưa đủ quyền thì chuyển hướng thay vì trả JSON
TRANG = {"/", "/giao-vien", "/trinh-chieu", "/truc-tiep", "/buoi-hoc", "/hoa", "/sinh", "/hoc-sinh", "/docs", "/redoc"}
# Phải đổi mật khẩu thì chỉ còn dùng được các đường dẫn này
KHI_PHAI_DOI = {"/dang-nhap", "/tinh", "/auth/toi", "/auth/doi-mat-khau", "/auth/dang-xuat"}

router = APIRouter()


def _mau_duong_dan(request: Request) -> str | None:
    for route in request.app.routes:
        if route.matches(request.scope)[0] == Match.FULL:
            return getattr(route, "path", None)
    return None


def _ma_bearer(request: Request) -> str | None:
    tieu_de = request.headers.get("authorization", "")
    return tieu_de[7:].strip() if tieu_de[:7].lower() == "bearer " else None


def tu_may_nay(request: Request) -> bool:
    """Yêu cầu đến thẳng từ máy đang chạy máy chủ, gọi đúng địa chỉ 127.0.0.1 (chặn cả trang web lạ dùng DNS rebinding)."""
    if os.environ.get("KHKT_MO_MANG") == "1":
        return False
    if any(h in request.headers for h in ("x-forwarded-for", "forwarded", "cf-connecting-ip")):
        return False
    return bool(request.client) and request.client.host in ("127.0.0.1", "::1") and request.url.hostname in ("127.0.0.1", "localhost", "::1")


def _sai_nguon(request: Request) -> bool:
    """Chống CSRF: yêu cầu ghi từ trình duyệt phải có Origin trùng máy chủ. Unity không gửi Origin, Bearer không tự đính kèm."""
    origin = request.headers.get("origin")
    if request.method in ("GET", "HEAD", "OPTIONS") or origin is None or _ma_bearer(request) is not None:
        return False
    return urlparse(origin).netloc.lower() != request.headers.get("host", "").lower()


def _bi_tu_choi(request: Request, mau: str | None, ma: int, loi: str, **them) -> Response:
    if mau in TRANG:
        if ma == 401:
            return RedirectResponse("/dang-nhap?tiep=" + quote(request.url.path, safe=""), status_code=303)
        return RedirectResponse("/hoc-sinh" if mau != "/hoc-sinh" else "/dang-nhap", status_code=303)
    return JSONResponse({"detail": loi, **them}, status_code=ma)


async def kiem_quyen(request: Request, call_next) -> Response:
    response = await _kiem_quyen(request, call_next)
    # Máy dùng chung: trình duyệt không được giữ lại trang, dữ liệu của người vừa đăng xuất.
    # Tệp CSS, JS được giữ nhưng phải hỏi lại máy chủ, để bản sửa mới có tác dụng ngay.
    response.headers["Cache-Control"] = "no-cache" if request.url.path.startswith("/tinh/") else "no-store"
    return response


async def _kiem_quyen(request: Request, call_next) -> Response:
    if _sai_nguon(request):
        return JSONResponse({"detail": "Yêu cầu đến từ trang khác, đã chặn"}, status_code=403)
    mau = _mau_duong_dan(request)
    muc = QUYEN.get(("GET" if request.method == "HEAD" else request.method, mau))
    bearer = _ma_bearer(request)
    nd = tai_khoan.doc_phien(bearer if bearer is not None else request.cookies.get(COOKIE, ""))
    request.state.nguoi_dung = nd
    if bearer is not None and nd is None and muc != CONG_KHAI:
        return _bi_tu_choi(request, mau, 401, "Phiên đăng nhập đã hết hạn")  # mã sai không được rơi xuống lối của Unity
    if muc == CONG_KHAI or (muc == UNITY and nd is None and tu_may_nay(request)):
        return await call_next(request)
    if nd is None:
        return _bi_tu_choi(request, mau, 401, "Cần đăng nhập")
    if nd["phai_doi_mat_khau"] and mau not in KHI_PHAI_DOI:
        if mau in TRANG:
            return RedirectResponse("/dang-nhap", status_code=303)
        return JSONResponse({"detail": "Cần đổi mật khẩu trước khi tiếp tục", "phai_doi_mat_khau": True}, status_code=403)
    if muc is None and nd["vai_tro"] != "giao_vien":
        return _bi_tu_choi(request, mau, 403, "Chỉ giáo viên được dùng chức năng này")
    return await call_next(request)


# ---------------------------------------------------------------------------
# Điểm cuối
# ---------------------------------------------------------------------------

class DangNhap(BaseModel):
    ten_dang_nhap: str = Field(max_length=64)
    mat_khau: str = Field(max_length=256)
    kieu: str = "web"


class DangKy(BaseModel):
    ma_lop: str = Field(max_length=16)
    ho_ten: str = Field(max_length=120)
    ten_dang_nhap: str = Field(max_length=64)
    mat_khau: str = Field(max_length=256)
    kieu: str = "web"


class DoiMatKhau(BaseModel):
    mat_khau_cu: str = Field(max_length=256)
    mat_khau_moi: str = Field(max_length=256)


def _ip(request: Request) -> str:
    return request.client.host if request.client else ""


def _bao_khoa(giay: int) -> HTTPException:
    return HTTPException(429, f"Sai quá nhiều lần, thử lại sau {max(1, (giay + 59) // 60)} phút", headers={"Retry-After": str(giay)})


def _mo_phien(nd: dict, kieu: str, request: Request, response: Response) -> dict:
    kieu = "ung_dung" if kieu == "ung_dung" else "web"
    phien = tai_khoan.tao_phien(nd["id"], kieu)
    out = {"het_han": phien["het_han"], "nguoi_dung": tai_khoan.cong_khai(nd)}
    if kieu == "ung_dung":
        out["ma_phien"] = phien["ma_phien"]
    else:  # trang web không đọc được mã: chỉ trình duyệt giữ trong cookie
        response.set_cookie(COOKIE, phien["ma_phien"], max_age=tai_khoan.HAN_PHIEN["web"], path="/", httponly=True, samesite="lax", secure=request.url.scheme == "https")
    return out


@router.post("/auth/dang-nhap")
def dang_nhap(body: DangNhap, request: Request, response: Response) -> dict:
    ten, ip = tai_khoan.chuan_ten(body.ten_dang_nhap), _ip(request)
    giay = tai_khoan.con_bi_khoa(ten, ip)
    if giay:
        raise _bao_khoa(giay)
    nd = tai_khoan.xac_thuc(ten, body.mat_khau)
    if nd is None:
        tai_khoan.ghi_sai(ten, ip)
        raise HTTPException(401, "Sai tên đăng nhập hoặc mật khẩu")
    tai_khoan.xoa_sai(ten, ip)
    return _mo_phien(nd, body.kieu, request, response)


@router.post("/auth/dang-ky")
def dang_ky(body: DangKy, request: Request, response: Response) -> dict:
    ip = _ip(request)
    giay = tai_khoan.con_bi_khoa("dang-ky", ip)
    if giay:
        raise _bao_khoa(giay)
    lop = tai_khoan.tim_lop_theo_ma(body.ma_lop)
    if lop is None:
        tai_khoan.ghi_sai("dang-ky", ip, 10)  # mã lớp ngắn để đọc được, nên phải chặn dò thử
        raise HTTPException(403, "Mã lớp không đúng hoặc lớp đã đóng đăng ký")
    try:
        nd = tai_khoan.tao_nguoi_dung(body.ten_dang_nhap, body.ho_ten, "hoc_sinh", body.mat_khau)
    except tai_khoan.TenDaCo as e:
        raise HTTPException(409, str(e))
    except tai_khoan.LoiTaiKhoan as e:
        raise HTTPException(422, str(e))
    tai_khoan.them_vao_lop(lop["id"], nd["id"])
    return _mo_phien(nd, body.kieu, request, response)


@router.post("/auth/dang-xuat")
def dang_xuat(request: Request, response: Response) -> dict:
    ma = _ma_bearer(request) or request.cookies.get(COOKIE)
    if ma:
        tai_khoan.huy_phien(ma)
    response.delete_cookie(COOKIE, path="/")
    return {"ok": True}


@router.get("/auth/toi")
def toi(request: Request) -> dict:
    return tai_khoan.cong_khai(request.state.nguoi_dung)


@router.post("/auth/doi-mat-khau")
def doi_mat_khau(body: DoiMatKhau, request: Request) -> dict:
    nd, ip = request.state.nguoi_dung, _ip(request)
    muc = f"doi-mat-khau:{nd['id']}"
    giay = tai_khoan.con_bi_khoa(muc, ip)
    if giay:
        raise _bao_khoa(giay)
    if not tai_khoan.dung_mat_khau_cua(nd, body.mat_khau_cu):
        tai_khoan.ghi_sai(muc, ip)
        raise HTTPException(400, "Mật khẩu hiện tại không đúng")
    try:
        tai_khoan.kiem_tra_mat_khau(body.mat_khau_moi, nd["ten_dang_nhap"])
    except tai_khoan.LoiTaiKhoan as e:
        raise HTTPException(422, str(e))
    if body.mat_khau_moi == body.mat_khau_cu:
        raise HTTPException(422, "Mật khẩu mới phải khác mật khẩu hiện tại")
    tai_khoan.xoa_sai(muc, ip)
    tai_khoan.dat_mat_khau(nd["id"], body.mat_khau_moi)
    tai_khoan.huy_phien_khac(nd["id"], _ma_bearer(request) or request.cookies.get(COOKIE, ""))
    return tai_khoan.cong_khai(tai_khoan.tim_theo_id(nd["id"]))


@router.get("/dang-nhap", include_in_schema=False)
def trang_dang_nhap() -> FileResponse:
    return FileResponse(WEB / "dang_nhap.html", media_type="text/html; charset=utf-8")


def _trang_chu(nd: dict) -> str:
    return "/giao-vien" if nd["vai_tro"] == "giao_vien" else "/hoc-sinh"


@router.get("/", include_in_schema=False)
def trang_goc(request: Request) -> RedirectResponse:
    return RedirectResponse(_trang_chu(request.state.nguoi_dung), status_code=303)


@router.get("/giao-vien", include_in_schema=False)
def trang_giao_vien() -> FileResponse:
    return FileResponse(WEB / "giao_vien.html", media_type="text/html; charset=utf-8")


@router.get("/trinh-chieu", include_in_schema=False)
def trang_trinh_chieu() -> FileResponse:
    return FileResponse(WEB / "trinh_chieu.html", media_type="text/html; charset=utf-8")


@router.get("/truc-tiep", include_in_schema=False)
def trang_truc_tiep() -> FileResponse:
    return FileResponse(WEB / "truc_tiep.html", media_type="text/html; charset=utf-8")


@router.get("/buoi-hoc", include_in_schema=False)
def trang_buoi_hoc() -> FileResponse:
    return FileResponse(WEB / "buoi_hoc.html", media_type="text/html; charset=utf-8")


@router.get("/hoc-sinh", include_in_schema=False)
def trang_hoc_sinh(request: Request) -> Response:
    if request.state.nguoi_dung["vai_tro"] == "giao_vien":
        return RedirectResponse("/giao-vien", status_code=303)
    return FileResponse(WEB / "hoc_sinh.html", media_type="text/html; charset=utf-8")


def cai_dat(app: FastAPI) -> None:
    app.include_router(router)
    app.mount("/tinh", StaticFiles(directory=WEB / "tinh"), name="tinh")
    app.middleware("http")(kiem_quyen)
