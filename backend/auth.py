"""Đăng nhập (email + mật khẩu, hoặc Google OAuth 2.0 / OpenID Connect qua Authlib khi đã cấu hình) + API tài khoản:
/api/toi, /api/key, /api/lich-su. Trang /dang-nhap (main.py) hiện cho mọi người chưa đăng nhập khi mở app.

- Phiên đăng nhập: cookie ký bằng khoá suy ra từ khoá chính (bao_mat; Starlette SessionMiddleware, httponly, secure, samesite=lax)
  chỉ chứa id người dùng + mã CSRF; dữ liệu người dùng nằm trong SQLite (db.py).
- Mọi yêu cầu thay đổi dữ liệu (POST/DELETE) phải gửi mã CSRF (header X-CSRF-Token hoặc ô form csrf_token).
- Chế độ cục bộ: chưa cấu hình GOOGLE_CLIENT_ID/SECRET và trình duyệt mở app trên chính máy chạy app (127.0.0.1)
  → coi là chủ app, không cần đăng nhập (giữ cách dùng cũ trên máy cá nhân). Truy cập từ máy khác thì vẫn phải đăng nhập.
- Tài khoản email + mật khẩu phải XÁC THỰC EMAIL (mã 6 số, xac_thuc_email.py) mới đăng nhập/dùng thử được; admin được miễn.
  Chống tạo hàng loạt: mỗi email (chuẩn hoá) một tài khoản, chặn email tạm thời, ≤ 3 tài khoản/IP/ngày, Cloudflare Turnstile.
- Mọi lần đăng ký/đăng nhập (đúng, sai)/đăng xuất/xác thực email được ghi vào nhật ký (nhat_ky.py) để admin xem.
"""
from __future__ import annotations

import logging
import math
import re
import time
import unicodedata
from urllib.parse import quote

from authlib.integrations.starlette_client import OAuth, OAuthError
from fastapi import APIRouter, Depends, HTTPException, Request
from fastapi.responses import RedirectResponse
from pydantic import BaseModel, Field

from . import claude_rieng, db, email_rac, key_claude, ma_claude, ma_giao_dien, nhat_ky, tai_khoan, xac_thuc_email
from .xac_thuc_email import LoiXacThuc
from .bao_mat import MAT_KHAU_TOI_THIEU, bam_mat_khau, csrf_hop_le, gioi_han_tan_suat, kiem_mat_khau, ma_csrf
from .config import settings
from .db import NguoiDung
from .dia_chi_ip import get_client_ip

log = logging.getLogger(__name__)

router = APIRouter()
oauth = OAuth()
if settings.dang_nhap_google:
    oauth.register(
        name="google",
        client_id=settings.google_client_id,
        client_secret=settings.google_client_secret,
        server_metadata_url="https://accounts.google.com/.well-known/openid-configuration",
        client_kwargs={"scope": "openid email profile"},
    )

_MAY_NAY = {"127.0.0.1", "::1", "localhost"}


# ---------------------------------------------------------------- người dùng hiện tại
def che_do_cuc_bo(request: Request) -> bool:
    if settings.dang_nhap_google or not settings.che_do_cuc_bo:   # CHE_DO_CUC_BO=0 trên máy chủ: không ai được vào thẳng
        return False
    host = request.client.host if request.client else ""
    return host in _MAY_NAY and "x-forwarded-for" not in request.headers and "forwarded" not in request.headers


def cap_admin_theo_cau_hinh(u: NguoiDung | None = None) -> int:
    """CAP_ADMIN_EMAIL (máy chủ không có Shell để chạy quan_tri): tài khoản email + mật khẩu đã XÁC THỰC email trùng địa chỉ đó → admin.
    Bắt buộc đã xác thực: admin được miễn bước xác thực, nên không cấp cho tài khoản ai đó đăng ký trước bằng email của chủ app."""
    email = settings.cap_admin_email
    if not email:
        return 0
    n = 0
    for x in [u] if u is not None else db.danh_sach_nguoi_dung():
        if (x.email or "").strip().lower() == email and x.dang_nhap_bang == "email" and x.da_xac_thuc_email and not x.la_admin:
            db.dat_admin(x.id, True)
            log.info("Đã cấp quyền admin cho tài khoản #%d theo CAP_ADMIN_EMAIL", x.id)
            n += 1
    return n


def nguoi_dung_hien_tai(request: Request) -> NguoiDung | None:
    uid = request.session.get("uid")
    if uid is not None:
        u = db.lay_nguoi_dung(int(uid))
        if u is not None and not u.cuc_bo and not u.bi_khoa and not u.can_xac_thuc:
            nhat_ky.cap_nhat_online(u, request)
            return u
        request.session.pop("uid", None)  # người dùng đã bị xoá / bị admin khoá / chưa xác thực email → đăng nhập lại
    if che_do_cuc_bo(request):
        u = db.nguoi_dung_cuc_bo()
        nhat_ky.cap_nhat_online(u, request)
        return u
    return None


def can_dang_nhap(request: Request) -> NguoiDung:
    u = nguoi_dung_hien_tai(request)
    if u is None:
        raise HTTPException(401, {"loi": "Bạn cần đăng nhập bằng Google để dùng chức năng này.", "ma": "can_dang_nhap"})
    return u


async def kiem_csrf(request: Request) -> None:
    gui_len = request.headers.get("x-csrf-token")
    if not gui_len and request.headers.get("content-type", "").startswith(("application/x-www-form-urlencoded", "multipart/form-data")):
        gui_len = (await request.form()).get("csrf_token")
    if not csrf_hop_le(request.session, gui_len):
        raise HTTPException(403, {"loi": "Phiên làm việc đã hết hạn hoặc yêu cầu không hợp lệ (CSRF). Hãy tải lại trang rồi thử lại.",
                                  "ma": "csrf"})


def gioi_han(hanh_dong: str, theo_ip: bool = False):
    """Dependency: giới hạn tần suất theo người dùng đã đăng nhập (hoặc theo IP).
    theo_ip=True cho đăng nhập/đăng ký: luôn tính theo IP (mỗi lần đăng ký là một người dùng mới nên không thể tính theo người dùng)."""

    def _kiem(request: Request) -> None:
        uid = request.session.get("uid")
        ip = f"ip{get_client_ip(request)}"
        ai = ip if theo_ip or uid is None else f"u{uid}"
        cho = gioi_han_tan_suat.kiem_tra(hanh_dong, ai)
        if cho is not None:
            giay = max(1, math.ceil(cho))
            if hanh_dong == "sinh_hoc_anh" and cho >= 3600:
                msg = "Bạn đã dùng hết 20 lượt giải thích ảnh hôm nay."
            else:
                msg = f"Bạn thao tác quá nhanh – hãy thử lại sau {giay} giây."
            raise HTTPException(429, {"loi": msg, "ma": "gioi_han_tan_suat"},
                                headers={"Retry-After": str(giay)})

    return Depends(_kiem)



def loi_tai_khoan(e: tai_khoan.LoiTaiKhoan) -> HTTPException:
    return HTTPException(e.status, {"loi": str(e), "ma": e.ma})


# ---------------------------------------------------------------- đăng nhập / đăng xuất
def duong_dan_an_toan(p: str | None) -> str:
    """Chỉ cho quay về đường dẫn nội bộ (chống open redirect)."""
    return p if p and p.startswith("/") and not p.startswith("//") and "\\" not in p else "/"


def dia_chi_callback(request: Request) -> str:
    """redirect_uri gửi cho Google. Sau Cloudflare Tunnel/proxy: uvicorn đọc X-Forwarded-Proto/For từ 127.0.0.1 (mặc định)
    nên ra https://<tên miền>/auth/callback; proxy ở máy khác thì đặt OAUTH_REDIRECT_URI trong .env."""
    return settings.oauth_redirect_uri or str(request.url_for("auth_callback"))


def _dat_phien(request: Request, u: NguoiDung, tiep: str | None) -> str:
    """Phiên mới sau khi đăng nhập (chống session fixation): chỉ giữ id người dùng + mã CSRF mới."""
    request.session.clear()
    request.session["uid"] = u.id
    ma_csrf(request.session)
    nhat_ky.cap_nhat_online(u, request, ep=True)
    log.info("Người dùng %s đăng nhập (%s)", u.id, u.dang_nhap_bang)
    return duong_dan_an_toan(tiep)


# ---- email + mật khẩu (không cần cấu hình Google; email phải được xác thực bằng mã 6 số)
_MAU_EMAIL = re.compile(r"[^@\s]{1,64}@[^@\s]{1,190}\.[A-Za-z]{2,24}")
DANG_KY_MOI_IP_MOI_NGAY = 3


class DangKyVao(BaseModel):
    email: str = Field("", max_length=254)
    mat_khau: str = Field("", max_length=200)
    ten: str = Field("", max_length=80)
    tiep: str = Field("/", max_length=500)
    captcha: str = Field("", max_length=4096)  # token Cloudflare Turnstile
    website: str = Field("", max_length=500)   # ô BẪY (honeypot) – ẩn với người thật nên luôn trống; bot tự điền → chặn


class DangNhapVao(BaseModel):
    email: str = Field("", max_length=254)
    mat_khau: str = Field("", max_length=200)
    tiep: str = Field("/", max_length=500)


def loi_xac_thuc(e: LoiXacThuc) -> HTTPException:
    noi_dung = {"loi": str(e), "ma": e.ma}
    if e.cho:
        noi_dung["cho"] = e.cho
    return HTTPException(e.status, noi_dung, headers={"Retry-After": str(e.cho)} if e.cho else None)


def _cho_xac_thuc(request: Request, u: NguoiDung, tiep: str | None) -> dict:
    """Gửi mã (nếu vừa gửi < 60 giây thì dùng lại mã đó) và ghi vào phiên tài khoản đang chờ xác thực."""
    try:
        cho = xac_thuc_email.gui_ma(u.email, "xac_thuc", u.id, u.ten)
    except LoiXacThuc as e:
        if e.ma != "cho_gui_lai":
            raise loi_xac_thuc(e) from e
        cho = e.cho or xac_thuc_email.CHO_GUI_LAI  # mã gửi trước đó vẫn còn hiệu lực
    request.session["cho_xac_thuc"] = {"uid": u.id, "tiep": duong_dan_an_toan(tiep)}
    return {"ok": False, "can_xac_thuc": True, "email": u.email, "cho_gui_lai": cho,
            "thong_bao": f"Đã gửi mã 6 số tới {u.email}. Mã có hiệu lực {xac_thuc_email.HIEU_LUC // 60} phút."}


def _so_dang_ky_tu_ip(ip: str) -> list[float]:
    with db.ket_noi() as con:
        return [r[0] for r in con.execute(
            "SELECT ts FROM activity_log WHERE event = 'dang_ky' AND method = 'email' AND ip = ? AND ts >= ? ORDER BY ts",
            (ip, time.time() - 86400))]


# lý do bị chặn (email_rac) → mã lỗi trả về giao diện (giữ mã cũ để giao diện / test cũ vẫn đúng)
_MA_LOI_CHAN = {"dinh_dang": "email_sai", "qua_nhieu_lan_thu": "dang_ky_qua_nhieu", "qua_gioi_han_ngay": "dang_ky_qua_nhieu"}


def _chan_dang_ky(email: str, ip: str, ly_do: str, status: int = 400, loi: str = "", chi_tiet: str = "",
                  cho: int | None = None) -> HTTPException:
    """Ghi nhật ký chặn (email, IP, lý do, lúc) rồi trả lỗi tiếng Việt cho người dùng."""
    email_rac.ghi_chan(email, ip, ly_do, chi_tiet)
    noi_dung = {"loi": loi or email_rac.THONG_BAO[ly_do], "ma": _MA_LOI_CHAN.get(ly_do, ly_do)}
    if cho:
        noi_dung["cho"] = cho
    return HTTPException(status, noi_dung, headers={"Retry-After": str(cho)} if cho else None)


@router.post("/auth/dang-ky", dependencies=[Depends(kiem_csrf), gioi_han("dang_ky", theo_ip=True)])
def dang_ky(vao: DangKyVao, request: Request):
    """Đăng ký email + mật khẩu. MỌI kiểm tra ở server: ô bẫy bot → 3 tài khoản/IP/ngày → 3 lần thử/IP/giờ → định dạng → mật khẩu →
    Turnstile → email rác (danh sách chặn/cho phép, email tạm thời, MX) → trùng email (Gmail đã chuẩn hoá)."""
    email = vao.email.strip().lower()
    ip = get_client_ip(request)
    if vao.website.strip():
        raise _chan_dang_ky(email, ip, "honeypot", chi_tiet=f"ô bẫy: {vao.website.strip()[:80]}")
    lan = _so_dang_ky_tu_ip(ip)
    if len(lan) >= DANG_KY_MOI_IP_MOI_NGAY:  # đã tạo đủ 3 tài khoản trong 24 giờ
        cho = max(60, int(lan[-DANG_KY_MOI_IP_MOI_NGAY] + 86400 - time.time()))
        raise _chan_dang_ky(email, ip, "qua_gioi_han_ngay", 429, cho=cho,
                            loi=f"Mỗi mạng (IP) chỉ được tạo tối đa {DANG_KY_MOI_IP_MOI_NGAY} tài khoản mỗi ngày – "
                                f"hãy thử lại sau khoảng {math.ceil(cho / 3600)} giờ.")
    if email_rac.so_lan_thu_gio(ip) >= email_rac.THU_DANG_KY_MOI_GIO:  # 3 lần thử / giờ (kể cả lần bị chặn vì email rác, bot)
        raise _chan_dang_ky(email, ip, "qua_nhieu_lan_thu", 429, cho=3600)
    if not email_rac.dinh_dang_hop_le(email):
        raise _chan_dang_ky(email, ip, "dinh_dang")
    if len(vao.mat_khau) < MAT_KHAU_TOI_THIEU:
        raise HTTPException(400, {"loi": f"Mật khẩu cần ít nhất {MAT_KHAU_TOI_THIEU} ký tự.", "ma": "mat_khau_ngan"})
    if vao.mat_khau.strip().lower() == email:
        raise HTTPException(400, {"loi": "Mật khẩu không được trùng email.", "ma": "mat_khau_yeu"})
    try:
        xac_thuc_email.kiem_turnstile(vao.captcha, ip)
    except LoiXacThuc as e:
        email_rac.ghi_chan(email, ip, "captcha", str(e))
        raise loi_xac_thuc(e) from e
    kt = email_rac.kiem_tra(email)  # danh sách chặn / cho phép của admin, email tạm thời, bản ghi MX
    if kt.chan:
        raise _chan_dang_ky(email, ip, kt.ma, loi=kt.loi)
    if db.tim_theo_email(email) is not None:
        raise HTTPException(409, {"loi": "Email này đã có tài khoản (với Gmail: kể cả khi viết khác dấu chấm hoặc thêm “+…”). "
                                         "Hãy chọn “Đăng nhập”; quên mật khẩu thì bấm “Quên mật khẩu?”.", "ma": "email_da_co"})
    if not settings.gui_email_san_sang:
        raise HTTPException(503, {"loi": "Máy chủ chưa cấu hình gửi email nên chưa tạo được tài khoản mới (cần xác thực email) – "
                                         "hãy báo chủ app.", "ma": "chua_cau_hinh_email"})
    ten = re.sub(r"\s+", " ", vao.ten).strip() or email.split("@")[0]
    u = db.tao_tai_khoan_email(email, ten, bam_mat_khau(vao.mat_khau), ip)
    if u is None:
        raise HTTPException(409, {"loi": "Email này đã có tài khoản – hãy chọn “Đăng nhập”.", "ma": "email_da_co"})
    try:
        kq = _cho_xac_thuc(request, u, vao.tiep)
    except HTTPException:
        db.xoa_nguoi_dung(u.id)  # không gửi được mã → huỷ tài khoản vừa tạo để người dùng thử lại sau
        raise
    nhat_ky.ghi("dang_ky", request, u, phuong_thuc="email", chi_tiet="chờ xác thực email")
    email_rac.dat_han_xac_minh(u.id)  # quá 24 giờ chưa xác minh → tự xoá (việc nền email_rac.mot_vong)
    if kt.nghi_ngo:  # không chặn – đánh dấu để admin duyệt ở /admin → Email rác
        email_rac.danh_dau_nghi_ngo(u.id, kt.nghi_ngo)
        nhat_ky.ghi("email_nghi_ngo", request, u, phuong_thuc="email", chi_tiet=kt.nghi_ngo)
    return kq


@router.post("/auth/dang-nhap-mat-khau", dependencies=[Depends(kiem_csrf), gioi_han("dang_nhap_mk", theo_ip=True)])
def dang_nhap_mat_khau(vao: DangNhapVao, request: Request):
    email = vao.email.strip().lower()
    cho = gioi_han_tan_suat.kiem_tra("dang_nhap_email", email)
    if cho is not None:
        giay = max(1, math.ceil(cho))
        nhat_ky.ghi("dang_nhap_sai", request, email=email, phuong_thuc="email", chi_tiet="quá nhiều lần thử")
        raise HTTPException(429, {"loi": f"Đăng nhập sai quá nhiều lần – thử lại sau {math.ceil(giay / 60)} phút.", "ma": "gioi_han_tan_suat"},
                            headers={"Retry-After": str(giay)})
    u = db.lay_theo_sub(db.SUB_EMAIL + email) if email else None
    if u is None and email:  # gõ email khác dạng (Gmail có dấu chấm/+…) vẫn tìm được tài khoản
        cung = db.tim_theo_email(email)
        u = cung if cung is not None and cung.dang_nhap_bang == "email" else None
    if not kiem_mat_khau(vao.mat_khau, u.mat_khau_bam if u else None):  # luôn băm thử → không lộ email có tồn tại hay không qua thời gian
        nhat_ky.ghi("dang_nhap_sai", request, u, email=email, phuong_thuc="email",
                    chi_tiet="sai mật khẩu" if u else "email chưa có tài khoản")
        raise HTTPException(401, {"loi": "Email hoặc mật khẩu không đúng.", "ma": "sai_mat_khau"})
    if u.bi_khoa:
        nhat_ky.ghi("dang_nhap_sai", request, u, phuong_thuc="email", chi_tiet="tài khoản bị khoá")
        raise HTTPException(403, {"loi": "Tài khoản này đã bị admin khoá. Hãy liên hệ admin để được mở khoá.", "ma": "tai_khoan_bi_khoa"})
    if u.can_xac_thuc:
        return _cho_xac_thuc(request, u, vao.tiep)
    db.ghi_dang_nhap(u.id)
    nhat_ky.ghi("dang_nhap", request, u, phuong_thuc="email")
    return {"ok": True, "tiep": _dat_phien(request, u, vao.tiep)}


class MaVao(BaseModel):
    ma: str = Field("", max_length=20)


@router.post("/auth/xac-thuc-email", dependencies=[Depends(kiem_csrf), gioi_han("xac_thuc", theo_ip=True)])
def xac_thuc_email_dang_ky(vao: MaVao, request: Request):
    """Nhập mã 6 số (sau Đăng ký, hoặc khi tài khoản cũ đăng nhập lần đầu) → kích hoạt + đăng nhập."""
    cho = request.session.get("cho_xac_thuc") or {}
    u = db.lay_nguoi_dung(int(cho["uid"])) if cho.get("uid") else None
    if u is None:
        raise HTTPException(400, {"loi": "Phiên xác thực đã hết – hãy đăng nhập lại để nhận mã mới.", "ma": "het_phien_xac_thuc"})
    try:
        xac_thuc_email.kiem_ma(u.email, "xac_thuc", vao.ma)
    except LoiXacThuc as e:
        nhat_ky.ghi("xac_thuc_sai", request, u, phuong_thuc="ma_email", chi_tiet=str(e))
        raise loi_xac_thuc(e) from e
    db.dat_xac_thuc_email(u.id)
    nhat_ky.ghi("xac_thuc_email", request, u, phuong_thuc="ma_email")
    if cap_admin_theo_cau_hinh(db.lay_nguoi_dung(u.id)):
        u = db.lay_nguoi_dung(u.id)
    if u.bi_khoa:
        raise HTTPException(403, {"loi": "Email đã được xác thực nhưng tài khoản đang bị admin khoá.", "ma": "tai_khoan_bi_khoa"})
    db.ghi_dang_nhap(u.id)
    nhat_ky.ghi("dang_nhap", request, u, phuong_thuc="email", chi_tiet="ngay sau khi xác thực email")
    return {"ok": True, "tiep": _dat_phien(request, u, cho.get("tiep"))}


@router.post("/auth/gui-lai-ma", dependencies=[Depends(kiem_csrf), gioi_han("gui_ma", theo_ip=True)])
def gui_lai_ma(request: Request):
    cho = request.session.get("cho_xac_thuc") or {}
    u = db.lay_nguoi_dung(int(cho["uid"])) if cho.get("uid") else None
    if u is None:
        raise HTTPException(400, {"loi": "Phiên xác thực đã hết – hãy đăng nhập lại để nhận mã mới.", "ma": "het_phien_xac_thuc"})
    try:
        return {"ok": True, "cho_gui_lai": xac_thuc_email.gui_ma(u.email, "xac_thuc", u.id, u.ten),
                "thong_bao": f"Đã gửi mã mới tới {u.email}."}
    except LoiXacThuc as e:
        raise loi_xac_thuc(e) from e


class QuenMatKhauVao(BaseModel):
    email: str = Field("", max_length=254)


class DatLaiMatKhauVao(BaseModel):
    email: str = Field("", max_length=254)
    ma: str = Field("", max_length=20)
    mat_khau_moi: str = Field("", max_length=200)
    tiep: str = Field("/", max_length=500)


def _tai_khoan_mat_khau(email: str) -> NguoiDung | None:
    u = db.tim_theo_email(email) if _MAU_EMAIL.fullmatch(email) else None
    return u if u is not None and u.dang_nhap_bang == "email" else None


@router.post("/auth/quen-mat-khau", dependencies=[Depends(kiem_csrf), gioi_han("gui_ma", theo_ip=True)])
def quen_mat_khau(vao: QuenMatKhauVao, request: Request):
    """Gửi mã đặt lại mật khẩu. Trả lời giống nhau dù email có tài khoản hay không (không lộ ai đã đăng ký)."""
    email = vao.email.strip().lower()
    if not _MAU_EMAIL.fullmatch(email):
        raise HTTPException(400, {"loi": "Email không hợp lệ.", "ma": "email_sai"})
    u = _tai_khoan_mat_khau(email)
    if u is not None and not u.bi_khoa:
        try:
            xac_thuc_email.gui_ma(u.email, "dat_lai_mk", u.id, u.ten)
        except LoiXacThuc as e:
            raise loi_xac_thuc(e) from e
    elif not settings.gui_email_san_sang:
        raise loi_xac_thuc(LoiXacThuc("Máy chủ chưa cấu hình gửi email nên chưa gửi được mã – hãy báo chủ app.", "chua_cau_hinh_email", 503))
    return {"ok": True, "cho_gui_lai": xac_thuc_email.CHO_GUI_LAI,
            "thong_bao": f"Nếu {email} có tài khoản, mã 6 số đã được gửi tới email đó (hiệu lực {xac_thuc_email.HIEU_LUC // 60} phút)."}


@router.post("/auth/dat-lai-mat-khau", dependencies=[Depends(kiem_csrf), gioi_han("xac_thuc", theo_ip=True)])
def dat_lai_mat_khau(vao: DatLaiMatKhauVao, request: Request):
    email = vao.email.strip().lower()
    if len(vao.mat_khau_moi) < MAT_KHAU_TOI_THIEU:
        raise HTTPException(400, {"loi": f"Mật khẩu mới cần ít nhất {MAT_KHAU_TOI_THIEU} ký tự.", "ma": "mat_khau_ngan"})
    u = _tai_khoan_mat_khau(email)
    if u is None or u.bi_khoa:
        raise HTTPException(400, {"loi": "Mã không đúng hoặc đã hết hạn – hãy bấm “Gửi lại mã”.", "ma": "ma_sai"})
    try:
        xac_thuc_email.kiem_ma(u.email, "dat_lai_mk", vao.ma)
    except LoiXacThuc as e:
        nhat_ky.ghi("xac_thuc_sai", request, u, phuong_thuc="ma_email", chi_tiet="đặt lại mật khẩu: " + str(e))
        raise loi_xac_thuc(e) from e
    db.dat_mat_khau(u.id, bam_mat_khau(vao.mat_khau_moi))
    db.dat_xac_thuc_email(u.id)  # nhận được mã qua email = sở hữu email
    db.ghi_dang_nhap(u.id)
    nhat_ky.ghi("dat_lai_mat_khau", request, u, phuong_thuc="ma_email")
    nhat_ky.ghi("dang_nhap", request, u, phuong_thuc="email", chi_tiet="sau khi đặt lại mật khẩu")
    return {"ok": True, "tiep": _dat_phien(request, u, vao.tiep)}


class DoiMatKhauVao(BaseModel):
    mat_khau_cu: str = Field("", max_length=200)
    mat_khau_moi: str = Field("", max_length=200)


@router.post("/auth/doi-mat-khau", dependencies=[Depends(kiem_csrf), gioi_han("dang_nhap_mk", theo_ip=True)])
def doi_mat_khau(vao: DoiMatKhauVao, request: Request):
    u = can_dang_nhap(request)
    if u.dang_nhap_bang != "email":
        raise HTTPException(400, {"loi": "Tài khoản này không đăng nhập bằng mật khẩu.", "ma": "khong_co_mat_khau"})
    if not kiem_mat_khau(vao.mat_khau_cu, u.mat_khau_bam):
        raise HTTPException(401, {"loi": "Mật khẩu hiện tại không đúng.", "ma": "sai_mat_khau"})
    if len(vao.mat_khau_moi) < MAT_KHAU_TOI_THIEU:
        raise HTTPException(400, {"loi": f"Mật khẩu mới cần ít nhất {MAT_KHAU_TOI_THIEU} ký tự.", "ma": "mat_khau_ngan"})
    db.dat_mat_khau(u.id, bam_mat_khau(vao.mat_khau_moi))
    return {"ok": True}


# ---- Google (chỉ khi .env có GOOGLE_CLIENT_ID/SECRET)
@router.get("/auth/dang-nhap", dependencies=[gioi_han("dang_nhap", theo_ip=True)])
async def dang_nhap(request: Request, tiep: str = "/"):
    if not settings.dang_nhap_google:
        return RedirectResponse("/dang-nhap?tiep=" + quote(duong_dan_an_toan(tiep), safe=""), status_code=303)
    request.session["sau_dang_nhap"] = duong_dan_an_toan(tiep)
    return await oauth.google.authorize_redirect(request, dia_chi_callback(request), prompt="select_account")


@router.get("/auth/callback", name="auth_callback")
async def auth_callback(request: Request):
    if not settings.dang_nhap_google:
        raise HTTPException(503, "Chủ app chưa cấu hình đăng nhập Google.")
    try:
        token = await oauth.google.authorize_access_token(request)
    except OAuthError as e:
        log.warning("Đăng nhập Google thất bại: %s", e.error)
        return RedirectResponse("/dang-nhap?loi=google", status_code=303)
    info = token.get("userinfo") or {}
    sub = str(info.get("sub") or "")
    if not sub or not info.get("email_verified") or sub.startswith((db.SUB_EMAIL, db.SUB_CUC_BO)):
        return RedirectResponse("/dang-nhap?loi=google", status_code=303)
    email = info.get("email", "")
    moi = db.lay_theo_sub(sub) is None
    cung_email = db.tim_theo_email(email) if moi else None
    if cung_email is not None and not cung_email.cuc_bo:
        # mỗi email một tài khoản: email này đã có tài khoản (email + mật khẩu) → Google đã xác minh email nên đăng nhập vào tài khoản đó
        u = cung_email
        if u.dang_nhap_bang == "email" and not u.da_xac_thuc_email:
            db.dat_mat_khau(u.id, None)  # mật khẩu do người CHƯA chứng minh sở hữu email đặt → bỏ (muốn dùng lại: "Quên mật khẩu")
        db.dat_xac_thuc_email(u.id)
        db.ghi_dang_nhap(u.id)
    else:
        kt = email_rac.kiem_tra(email, kiem_mx_dns=False) if moi else email_rac.KetQua()  # Google đã xác minh hộp thư → không cần MX
        if kt.ma in ("email_tam", "ten_mien_bi_chan"):
            email_rac.ghi_chan(email, get_client_ip(request), "ten_mien_google", kt.ma, hanh_dong="google")
            return RedirectResponse("/dang-nhap?loi=email_tam", status_code=303)
        u = db.luu_nguoi_dung(sub, email, info.get("name", ""), info.get("picture", ""))
        if moi:
            nhat_ky.ghi("dang_ky", request, u, phuong_thuc="google")
            if kt.nghi_ngo:
                email_rac.danh_dau_nghi_ngo(u.id, kt.nghi_ngo)
                nhat_ky.ghi("email_nghi_ngo", request, u, phuong_thuc="google", chi_tiet=kt.nghi_ngo)
    if u.bi_khoa:
        nhat_ky.ghi("dang_nhap_sai", request, u, phuong_thuc="google", chi_tiet="tài khoản bị khoá")
        return RedirectResponse("/dang-nhap?loi=khoa", status_code=303)
    nhat_ky.ghi("dang_nhap", request, u, phuong_thuc="google")
    return RedirectResponse(_dat_phien(request, u, request.session.get("sau_dang_nhap")), status_code=303)


@router.post("/auth/dang-xuat", dependencies=[Depends(kiem_csrf)])
async def dang_xuat(request: Request):
    uid = request.session.get("uid")
    u = db.lay_nguoi_dung(int(uid)) if uid is not None else None
    if u is not None and not u.cuc_bo:
        nhat_ky.ghi("dang_xuat", request, u, phuong_thuc=u.dang_nhap_bang)
        nhat_ky.quen_online(u.id)
    request.session.clear()
    if "application/json" in request.headers.get("accept", ""):
        return {"ok": True}
    return RedirectResponse("/", status_code=303)


# ---------------------------------------------------------------- tài khoản
@router.get("/api/toi")
def toi(request: Request):
    """Thông tin người dùng hiện tại + mã CSRF cho các yêu cầu POST/DELETE. Không bao giờ trả key."""
    u = nguoi_dung_hien_tai(request)
    return {
        **tai_khoan.thong_tin(u),
        "dang_nhap_google": settings.dang_nhap_google,
        "che_do_cuc_bo": che_do_cuc_bo(request),
        "qua_proxy": "x-forwarded-for" in request.headers or "cf-connecting-ip" in request.headers,
        "oauth_redirect_uri": dia_chi_callback(request),  # để chủ app khai báo đúng trong Google Cloud Console
        "csrf": ma_csrf(request.session),
        "nhan_thanh_vien": nhat_ky.nhan_thanh_vien(),
        "turnstile_site_key": settings.turnstile_site_key if settings.turnstile_bat else "",
        "gui_email": settings.gui_email_san_sang,
        "hoat_dong_moi": nhat_ky.so_moi_cho_admin(u) if u is not None and tai_khoan.la_chu_app(u) else 0,
        "claude": ma_claude.trang_thai(u, get_client_ip(request)),
        "claude_key": key_claude.trang_thai(u, get_client_ip(request)),  # Key Claude dùng chung (không bao giờ chứa key)
        "claude_rieng": claude_rieng.trang_thai(u),  # API key Claude riêng: chỉ có / 4 ký tự cuối
        "hoat_anh_cai_dat": db.cai_dat("hoat_anh_cai_dat", "1") == "1",  # admin bật/tắt cho mọi người (/admin → Cài đặt chung)
        "ai_rieng": _ai_rieng_cho(u),  # lựa chọn "AI Riêng" ở trang giải (admin luôn có; người dùng khi admin đã mở)
        "co_openai": bool(settings.openai_api_key and settings.openai_model),
    }


def _ai_rieng_cho(u) -> dict:
    from .ai_rieng import kho as ai_rieng_kho

    cd = ai_rieng_kho.cai_dat()
    mo = bool(cd["mo_cho_nguoi_dung"])
    admin = u is not None and tai_khoan.la_chu_app(u)
    return {"co": u is not None and bool(cd["bat"]) and (admin or mo), "mo_cho_nguoi_dung": mo}


# ---- đổi tên hiển thị
TEN_TOI_THIEU, TEN_TOI_DA, DOI_TEN_MOI_NGAY = 2, 30, 3
_KY_TU_TEN_THEM = set(" .-_'")
_TEN_DANH_RIENG = {"admin", "administrator", "quantri", "quantrivien", "chuapp", "root", "moderator", "mod"}


class MaClaudeVao(BaseModel):
    ma: str = Field("", max_length=64)


@router.get("/api/ma-claude")
def ma_claude_cua_toi(request: Request, u: NguoiDung = Depends(can_dang_nhap)):
    """Cài đặt → Mã Claude: dùng được Claude không, còn bao nhiêu lượt, hết hạn ngày nào (không bao giờ trả mã gốc)."""
    return ma_claude.trang_thai(u, get_client_ip(request))


@router.post("/api/ma-claude", dependencies=[Depends(kiem_csrf), gioi_han("ma_claude")])
def nhap_ma_claude(vao: MaClaudeVao, request: Request, u: NguoiDung = Depends(can_dang_nhap)):
    """Nhập mã kích hoạt Claude → gắn vào tài khoản. Sai 5 lần trong 1 giờ → khoá ô nhập 1 giờ (kiểm tra ở backend)."""
    ip = get_client_ip(request)
    try:
        tt = ma_claude.gan_ma(u, vao.ma, ip)
    except ma_claude.LoiMaClaude as e:
        if str(e).startswith("Mã không đúng"):  # một lần nhập sai thật (bị khoá từ trước thì không ghi)
            nhat_ky.ghi("nhap_sai_ma_claude", request, u, phuong_thuc="web", chi_tiet=str(e)[:120])
        headers = {"Retry-After": str(e.cho)} if e.cho else None
        raise HTTPException(e.status, {"loi": str(e), "ma": e.ma, **({"cho": e.cho} if e.cho else {})}, headers=headers) from e
    nhat_ky.ghi("gan_ma_claude", request, u, phuong_thuc="web", chi_tiet=f"{tt.get('goi_y', '')} · còn {tt.get('con_luot')} lượt")
    return {"ok": True, **tt}


@router.delete("/api/ma-claude", dependencies=[Depends(kiem_csrf)])
def go_ma_claude(request: Request, u: NguoiDung = Depends(can_dang_nhap)):
    ma_claude.go_ma(u)
    return {"ok": True, **ma_claude.trang_thai(u, get_client_ip(request))}


# ---- Key Claude dùng chung (key_claude.py): key chỉ nằm trong .env, KHÔNG trả về, KHÔNG ghi nhật ký / log
class KeyClaudeVao(BaseModel):
    key: str = Field("", max_length=200)


@router.get("/api/key-claude")
def key_claude_cua_toi(request: Request, u: NguoiDung = Depends(can_dang_nhap)):
    """Cài đặt → Key Claude: đã mở Claude chưa, kích hoạt lúc nào, hôm nay còn bao nhiêu lượt."""
    return key_claude.trang_thai(u, get_client_ip(request))


@router.post("/api/key-claude", dependencies=[Depends(kiem_csrf), gioi_han("key_claude")])
def kich_hoat_key_claude(vao: KeyClaudeVao, request: Request, u: NguoiDung = Depends(can_dang_nhap)):
    """Nhập Key Claude → mở Claude cho tài khoản. Sai 5 lần trong 1 giờ (theo tài khoản và theo IP) → khoá ô nhập 1 giờ."""
    ip = get_client_ip(request)
    try:
        tt = key_claude.kich_hoat(u, vao.key, ip)
    except key_claude.LoiKeyClaude as e:
        if str(e).startswith("Key không đúng"):  # một lần nhập sai thật (KHÔNG ghi chuỗi đã nhập)
            nhat_ky.ghi("nhap_sai_key_claude", request, u, phuong_thuc="web")
            if e.ma == "key_claude_bi_khoa":
                nhat_ky.ghi("khoa_key_claude", request, u, phuong_thuc="web", chi_tiet="sai 5 lần trong 1 giờ → khoá ô nhập 1 giờ")
        headers = {"Retry-After": str(e.cho)} if e.cho else None
        raise HTTPException(e.status, {"loi": str(e), "ma": e.ma, **({"cho": e.cho} if e.cho else {})}, headers=headers) from e
    if not tt.get("la_admin"):
        nhat_ky.ghi("kich_hoat_key_claude", request, u, phuong_thuc="web", chi_tiet=f"tối đa {key_claude.gioi_han_ngay()} bài/ngày")
    return {"ok": True, **tt}


# ---- API key Claude RIÊNG của người dùng (claude_rieng.py): lưu mã hoá, chỉ trả 4 ký tự cuối, KHÔNG ghi log
class KeyClaudeRiengVao(BaseModel):
    key: str = Field("", max_length=300)


@router.post("/api/claude-rieng", dependencies=[Depends(kiem_csrf), gioi_han("luu_key")])
def luu_claude_rieng(vao: KeyClaudeRiengVao, u: NguoiDung = Depends(can_dang_nhap)):
    """Lưu & kiểm tra: gọi thử Claude (max_tokens 1) bằng key → hợp lệ thì mã hoá + lưu."""
    try:
        return {"ok": True, **claude_rieng.luu(u, vao.key)}
    except claude_rieng.LoiKeyRieng as e:
        raise HTTPException(e.status, {"loi": str(e), "ma": e.ma}) from e


@router.delete("/api/claude-rieng", dependencies=[Depends(kiem_csrf)])
def xoa_claude_rieng(u: NguoiDung = Depends(can_dang_nhap)):
    try:
        claude_rieng.xoa(u)
    except claude_rieng.LoiKeyRieng as e:
        raise HTTPException(e.status, {"loi": str(e), "ma": e.ma}) from e
    return {"ok": True, **claude_rieng.trang_thai(u)}


@router.post("/api/claude-rieng/yeu-cau", dependencies=[Depends(kiem_csrf), gioi_han("luu_key")])
def yeu_cau_claude_rieng(request: Request, u: NguoiDung = Depends(can_dang_nhap)):
    """Tài khoản chưa được bật: gửi yêu cầu cho admin kích hoạt API key Claude riêng (ghi nhật ký → admin thấy ở /admin)."""
    try:
        moi = claude_rieng.yeu_cau(u)
    except claude_rieng.LoiKeyRieng as e:
        raise HTTPException(e.status, {"loi": str(e), "ma": e.ma}) from e
    if moi:
        nhat_ky.ghi("yeu_cau_key_claude_rieng", request, u, phuong_thuc="web")
    return {"ok": True, "moi": moi, **claude_rieng.trang_thai(u)}


# ---- Mã giao diện (nút "+K"): admin cấp mã → đổi theme. Kiểm tra mã ở server (CSDL chỉ lưu HMAC), trả themeId
class MaGiaoDienVao(BaseModel):
    code: str = Field("", max_length=60)


@router.post("/api/theme/redeem", dependencies=[Depends(kiem_csrf), gioi_han("ma_giao_dien")])
def doi_ma_giao_dien(vao: MaGiaoDienVao, request: Request, u: NguoiDung = Depends(can_dang_nhap)):
    ip = get_client_ip(request)
    try:
        kq = ma_giao_dien.doi_ma(u, vao.code, ip)
    except ma_giao_dien.LoiMaGiaoDien as e:
        if e.ma == "sai":
            nhat_ky.ghi("nhap_sai_ma_giao_dien", request, u, phuong_thuc="web")
        raise HTTPException(e.status, {"loi": str(e), "ma": e.ma, **({"cho": e.cho} if e.cho else {})},
                            headers={"Retry-After": str(e.cho)} if e.cho else None) from e
    nhat_ky.ghi("doi_giao_dien", request, u, phuong_thuc="web", chi_tiet=kq["ten_theme"])
    return {"ok": True, "themeId": kq["theme_id"], "ten": kq["ten_theme"]}


class DungKeyRiengVao(BaseModel):
    dung: bool


@router.post("/api/claude-rieng/dung", dependencies=[Depends(kiem_csrf)])
def dung_claude_rieng(vao: DungKeyRiengVao, u: NguoiDung = Depends(can_dang_nhap)):
    """Công tắc "Dùng key của tôi" (chỉ tài khoản được admin bật)."""
    try:
        return {"ok": True, **claude_rieng.dat_dung(u, vao.dung)}
    except claude_rieng.LoiKeyRieng as e:
        raise HTTPException(e.status, {"loi": str(e), "ma": e.ma}) from e


@router.delete("/api/key-claude", dependencies=[Depends(kiem_csrf)])
def go_key_claude(request: Request, u: NguoiDung = Depends(can_dang_nhap)):
    """Người dùng tự tắt Claude (gỡ key)."""
    if key_claude.trang_thai(u).get("trang_thai") in ("da_mo", "key_cu", "tat"):
        key_claude.go_key(u)
        nhat_ky.ghi("go_key_claude", request, u, phuong_thuc="web")
    return {"ok": True, **key_claude.trang_thai(u, get_client_ip(request))}


class DoiTenVao(BaseModel):
    ten: str = Field("", max_length=200)


def khoa_ten(ten: str) -> str:
    """So sánh tên: bỏ dấu tiếng Việt, không phân biệt hoa thường, bỏ khoảng trắng/ký hiệu ("Trọng Admin" ~ "trong admin")."""
    tach = unicodedata.normalize("NFD", ten.replace("đ", "d").replace("Đ", "D"))
    return "".join(c for c in tach.casefold() if c.isalnum() and not unicodedata.combining(c))


def chuan_hoa_ten(ten: str) -> str:
    """Cắt khoảng trắng thừa, 2–30 ký tự, chỉ chữ (có dấu tiếng Việt), số, khoảng trắng và . - _ ' → lỗi thì HTTPException 400."""
    ten = re.sub(r"\s+", " ", unicodedata.normalize("NFC", ten or "")).strip()
    if not TEN_TOI_THIEU <= len(ten) <= TEN_TOI_DA:
        raise HTTPException(400, {"loi": f"Tên cần từ {TEN_TOI_THIEU} đến {TEN_TOI_DA} ký tự.", "ma": "ten_do_dai"})
    if any(not (c.isalnum() or unicodedata.category(c).startswith("M") or c in _KY_TU_TEN_THEM) for c in ten):
        raise HTTPException(400, {"loi": "Tên chỉ gồm chữ (có dấu tiếng Việt), số, khoảng trắng và các dấu . - _ '", "ma": "ten_ky_tu"})
    if not any(c.isalpha() for c in ten):
        raise HTTPException(400, {"loi": "Tên phải có ít nhất một chữ cái.", "ma": "ten_ky_tu"})
    return ten


@router.post("/api/doi-ten", dependencies=[Depends(kiem_csrf), gioi_han("doi_ten")])
def doi_ten(vao: DoiTenVao, request: Request, u: NguoiDung = Depends(can_dang_nhap)):
    ten = chuan_hoa_ten(vao.ten)
    if ten == u.ten:
        raise HTTPException(400, {"loi": "Tên mới giống tên hiện tại.", "ma": "ten_giong"})
    khoa = khoa_ten(ten)
    la_admin = tai_khoan.la_chu_app(u)
    if not la_admin and (khoa in _TEN_DANH_RIENG or any(
            khoa == khoa_ten(a.ten) for a in db.danh_sach_nguoi_dung() if a.id != u.id and tai_khoan.la_chu_app(a))):
        raise HTTPException(400, {"loi": "Tên này trùng với tên của admin – hãy chọn tên khác.", "ma": "ten_trung_admin"})
    lan = [] if u.la_ki_cuu else db.lan_doi_ten(u.id, time.time() - 86400)  # kì cựu: không giới hạn số lần
    if len(lan) >= DOI_TEN_MOI_NGAY:
        cho = max(60, int(lan[-DOI_TEN_MOI_NGAY] + 86400 - time.time()))
        luc = time.strftime("%H:%M %d/%m", time.localtime(time.time() + cho))
        raise HTTPException(429, {"loi": f"Mỗi ngày chỉ được đổi tên {DOI_TEN_MOI_NGAY} lần – hãy thử lại sau {luc}.",
                                  "ma": "doi_ten_qua_nhieu", "cho": cho}, headers={"Retry-After": str(cho)})
    db.doi_ten(u.id, u.ten, ten, get_client_ip(request))
    nhat_ky.ghi("doi_ten", request, u, ten=ten, phuong_thuc="web", chi_tiet=f"{u.ten} → {ten}")
    return {"ok": True, "ten": ten, "con_lai_hom_nay": -1 if u.la_ki_cuu else DOI_TEN_MOI_NGAY - len(lan) - 1}


class KeyVao(BaseModel):
    key: str = Field("", max_length=300)


@router.get("/api/key")
def xem_key(u: NguoiDung = Depends(can_dang_nhap)):
    return {"co": u.co_key, "cuoi": u.key_cuoi if u.co_key else ""}


@router.post("/api/key", dependencies=[Depends(kiem_csrf), gioi_han("luu_key")])
def luu_key(vao: KeyVao, u: NguoiDung = Depends(can_dang_nhap)):
    try:
        return tai_khoan.luu_key(u, vao.key)
    except tai_khoan.LoiTaiKhoan as e:
        raise loi_tai_khoan(e) from e


@router.post("/api/key/kiem-tra", dependencies=[Depends(kiem_csrf), gioi_han("kiem_tra_key")])
def kiem_tra_key(vao: KeyVao, u: NguoiDung = Depends(can_dang_nhap)):
    try:
        return tai_khoan.kiem_tra_key(u, vao.key)
    except tai_khoan.LoiTaiKhoan as e:
        raise loi_tai_khoan(e) from e


@router.delete("/api/key", dependencies=[Depends(kiem_csrf)])
def xoa_key(u: NguoiDung = Depends(can_dang_nhap)):
    tai_khoan.xoa_key(u)
    return {"ok": True}


@router.get("/api/lich-su")
def lich_su(u: NguoiDung = Depends(can_dang_nhap)):
    """Chỉ các bài của chính người dùng đang đăng nhập."""
    return {"bai": db.lich_su(u.id)}


# ---------------------------------------------------------------- Đăng nhập cho Desktop App (CHEMISTRY.app)
class DoiMaAppPayload(BaseModel):
    ma: str = Field(..., min_length=10, max_length=120)


@router.post("/api/app/ma-dang-nhap", dependencies=[Depends(kiem_csrf)])
def tao_ma_dang_nhap(request: Request, u: NguoiDung = Depends(can_dang_nhap)):
    """Cấp mã một lần (TTL 120s) từ phiên web để chuyển vào ứng dụng desktop CHEMISTRY.app."""
    ma = db.tao_ma_dang_nhap_app(u.id, 120)
    chuyen_huong = f"chemistry://dang-nhap?ma={ma}"
    return {
        "ma": ma,
        "het_han_giay": 120,
        "chuyen_huong": chuyen_huong,
    }


@router.post("/api/app/doi-ma", dependencies=[gioi_han("doi_ma_app", theo_ip=True)])
def doi_ma_dang_nhap(payload: DoiMaAppPayload, request: Request):
    """Đổi mã một lần lấy phiên đăng nhập cho ứng dụng desktop CHEMISTRY."""
    ok, ly_do, uid = db.doi_ma_dang_nhap_app(payload.ma)
    if not ok or uid is None:
        raise HTTPException(status_code=400, detail=ly_do)

    u = db.lay_nguoi_dung(uid)
    if u is None:
        raise HTTPException(status_code=400, detail="Không tìm thấy người dùng")

    # Thiết lập phiên đăng nhập
    request.session.clear()
    request.session["uid"] = u.id
    csrf = ma_csrf(request.session)

    nhat_ky.ghi("dang_nhap", request, u, phuong_thuc="desktop_app_code", chi_tiet="Đăng nhập qua mã ứng dụng desktop")

    return {
        "ok": True,
        "csrf": csrf,
        "user": {
            "id": u.id,
            "email": u.email,
            "name": u.ten,
            "is_admin": bool(u.la_admin),
        }
    }

