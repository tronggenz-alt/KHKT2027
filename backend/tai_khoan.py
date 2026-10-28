"""Tài khoản người dùng: chọn key AI cho từng yêu cầu, lượt dùng thử, lưu/kiểm tra/xoá key Gemini riêng (BYOK).

Quy tắc chọn key khi giải bài (chon_khoa_giai):
1. Người dùng có key Gemini riêng → LUÔN dùng key đó (key lỗi/hết hạn mức thì báo lỗi, không chuyển sang key của chủ app).
2. Chưa có key → dùng thử bằng key của chủ app (.env), tối đa FREE_TRIAL_LIMIT bài/người; hết lượt → yêu cầu nhập key riêng.
3. Chủ app (chế độ cục bộ hoặc email trong ADMIN_EMAILS) → key của chủ app, không giới hạn.
4. Kì cựu (vai trò do chủ app cấp) → key của chủ app, không giới hạn (không có quyền quản trị).
"""
from __future__ import annotations

import logging
import re
from dataclasses import dataclass
from typing import Callable

from . import db
from .bao_mat import LoiBaoMat, bon_ky_tu_cuoi, giai_ma_key, ma_hoa_key, ma_hoa_san_sang
from .config import settings
from .db import NguoiDung
from .llm import KHOA_CHU_APP, KHOA_KHONG_CO, KhoaAI, kiem_tra_key_gemini

log = logging.getLogger(__name__)

# Lỗi do phía app (AI của chủ app hết tiền/quá tải/mất mạng...) → trả lại lượt dùng thử; lỗi do đề bài thì không.
LOI_PHIA_APP = {"het_tien", "key", "quyen", "model", "thieu_key", "gioi_han", "qua_tai", "mang", "chua_day"}  # chua_day: AI Riêng chưa được dạy


class LoiTaiKhoan(Exception):
    """Lỗi hiện cho người dùng (tiếng Việt) kèm mã để giao diện xử lý (vd mở trang Cài đặt) và mã HTTP."""

    def __init__(self, thong_diep: str, ma: str = "", status: int = 400):
        super().__init__(thong_diep)
        self.ma = ma
        self.status = status


def la_chu_app(u: NguoiDung) -> bool:
    # ADMIN_EMAILS chỉ áp dụng cho tài khoản Google (email đã được Google xác minh); tài khoản email + mật khẩu
    # không xác minh email nên ai cũng có thể đăng ký bằng email của chủ app → không được tính là chủ app.
    return u.cuc_bo or u.la_admin or (u.dang_nhap_bang == "google" and (u.email or "").lower() in settings.admin_emails)


def co_key_chu_app() -> bool:
    return bool(settings.api_key or settings.gemini_api_key or (settings.openai_api_key and settings.openai_model))


def luot_con_lai(u: NguoiDung) -> int:
    return max(0, settings.free_trial_limit - u.luot_da_dung)


def _khoa_ca_nhan(u: NguoiDung) -> KhoaAI:
    try:
        key = giai_ma_key(u.key_ma_hoa or "")
    except LoiBaoMat as e:
        raise LoiTaiKhoan(str(e), "key_hong", 409) from e
    return KhoaAI("ca_nhan", key, u.key_cuoi)


@dataclass
class LuaChonKhoa:
    khoa: KhoaAI
    nguon: str  # "ca_nhan" | "dung_thu" | "chu_app" | "demo"
    da_tru_luot: bool = False


def chon_khoa_giai(u: NguoiDung, demo: bool = False) -> LuaChonKhoa:
    """Chọn key cho một bài giải; dùng thử thì trừ lượt ngay (nguyên tử) để không vượt giới hạn khi gửi nhiều bài cùng lúc."""
    if demo:
        return LuaChonKhoa(KHOA_KHONG_CO, "demo")  # demo không gọi AI → không tốn lượt
    if u.co_key:
        return LuaChonKhoa(_khoa_ca_nhan(u), "ca_nhan")
    if la_chu_app(u) or u.la_ki_cuu:
        return LuaChonKhoa(KHOA_CHU_APP, "chu_app")
    if not co_key_chu_app():
        raise LoiTaiKhoan("App chưa có key AI của chủ app nên không có lượt dùng thử. Hãy thêm key Gemini của bạn trong Cài đặt.",
                          "can_key", 403)
    if not db.dung_luot_thu(u.id, settings.free_trial_limit):
        raise LoiTaiKhoan(
            f"Bạn đã dùng hết {settings.free_trial_limit} bài dùng thử. Hãy thêm key Gemini của bạn trong Cài đặt để giải tiếp "
            "(lấy miễn phí tại aistudio.google.com, khoảng 1 phút).", "het_luot_dung_thu", 403)
    return LuaChonKhoa(KHOA_CHU_APP, "dung_thu", da_tru_luot=True)


def chon_khoa_3d(u: NguoiDung | None) -> KhoaAI:
    """Mục 3D chỉ cần AI khi phải đọc ảnh/tên tiếng Việt (một yêu cầu nhỏ): không trừ lượt dùng thử,
    nhưng chỉ dùng key của chủ app khi người dùng còn lượt; chưa đăng nhập thì không dùng AI."""
    if u is None:
        return KHOA_KHONG_CO
    if u.co_key:
        return _khoa_ca_nhan(u)
    if la_chu_app(u) or u.la_ki_cuu or luot_con_lai(u) > 0:
        return KHOA_CHU_APP
    return KHOA_KHONG_CO


def chon_khoa_sinh_hoc(u: NguoiDung | None) -> KhoaAI:
    """AI Bio-Tutor (Phòng Lab Sinh học 3D): mỗi câu hỏi là một lần gọi AI rẻ, đã giới hạn tần suất ("sinh_hoc") nên người đã đăng nhập
    được dùng key của chủ app mà không trừ lượt dùng thử; có key Gemini riêng thì dùng key riêng. Chưa đăng nhập → không dùng AI."""
    if u is None:
        return KHOA_KHONG_CO
    if u.co_key:
        return _khoa_ca_nhan(u)
    return KHOA_CHU_APP if co_key_chu_app() else KHOA_KHONG_CO


def chon_khoa_doc_anh(u: NguoiDung | None) -> KhoaAI:
    """Chọn key cho tính năng đọc ảnh sinh học: người dùng có key riêng thì dùng key riêng,
    khách vãng lai hoặc đã đăng nhập chưa có key thì dùng key chủ app (đã giới hạn 20 ảnh/ngày)."""
    if u is not None and u.co_key:
        return _khoa_ca_nhan(u)
    return KHOA_CHU_APP if co_key_chu_app() else KHOA_KHONG_CO



def ket_thuc_bai(lua_chon: LuaChonKhoa, uid: int, job_id: str, ket_qua: dict | None, loi: str | None, loai_loi: str = "",
                 muc_do: str = "", claude: bool = False) -> None:
    """Ghi kết quả vào lịch sử (kèm mức độ đề, có giải bằng Claude không); bài dùng thử lỗi vì phía app thì trả lại lượt."""
    db.ket_thuc_bai_giai(job_id, ket_qua, loi, muc_do=muc_do, claude=claude)
    if lua_chon.da_tru_luot and loi and loai_loi in LOI_PHIA_APP:
        db.hoan_luot_thu(uid)
        log.info("Trả lại lượt dùng thử cho người dùng %s (bài %s lỗi phía app: %s)", uid, job_id, loai_loi)


def nguon_key_giai(u: NguoiDung | None) -> str:
    """Key sẽ dùng cho bài kế tiếp (để giao diện hiển thị)."""
    if u is None:
        return "can_dang_nhap"
    if u.co_key:
        return "ca_nhan"
    if la_chu_app(u):
        return "chu_app"
    if u.la_ki_cuu:
        return "ki_cuu"
    return "dung_thu" if co_key_chu_app() and luot_con_lai(u) > 0 else "het_luot"


# ---------------------------------------------------------------- key Gemini của người dùng
_MAU_KEY = re.compile(r"[A-Za-z0-9_\-.]{20,200}")


def chuan_hoa_key(key: str) -> str:
    key = (key or "").strip().strip('"').strip("'").strip()
    if key.upper().startswith("GEMINI_API_KEY="):
        key = key.split("=", 1)[1].strip()
    if not key:
        raise LoiTaiKhoan("Hãy dán key Gemini vào ô trước.", "key_trong")
    if not _MAU_KEY.fullmatch(key):
        raise LoiTaiKhoan("Key không đúng định dạng: key Gemini là một chuỗi liền (thường bắt đầu bằng AIza…), không có dấu cách.",
                          "key_sai_dinh_dang")
    return key


def luu_key(u: NguoiDung, key: str, kiem_tra: Callable[[str], dict] | None = None) -> dict:
    """Kiểm tra key (gọi thử) → mã hoá → lưu. Key bị Google từ chối thì không lưu. Không trả key về."""
    kiem_tra = kiem_tra or kiem_tra_key_gemini
    key = chuan_hoa_key(key)
    if not ma_hoa_san_sang():
        raise LoiTaiKhoan("App chưa tạo được khoá mã hoá (data/app_secret.key) nên chưa lưu được key – chủ app kiểm tra quyền ghi thư mục data/.",
                          "chua_co_khoa_chinh", 503)
    kq = kiem_tra(key)
    if kq.get("hop_le") is False:
        raise LoiTaiKhoan(kq.get("thong_diep") or "Key không hợp lệ.", "key_khong_hop_le")
    db.dat_key(u.id, ma_hoa_key(key), bon_ky_tu_cuoi(key))
    return {"ok": True, "cuoi": bon_ky_tu_cuoi(key), "kiem_tra": _kq_an_toan(kq)}


def kiem_tra_key(u: NguoiDung, key_moi: str = "", kiem_tra: Callable[[str], dict] | None = None) -> dict:
    """Kiểm tra key vừa dán (chưa lưu), hoặc key đã lưu nếu ô trống."""
    kiem_tra = kiem_tra or kiem_tra_key_gemini
    if (key_moi or "").strip():
        key, nguon = chuan_hoa_key(key_moi), "moi"
    elif u.co_key:
        key, nguon = _khoa_ca_nhan(u).gemini_api_key, "da_luu"
    else:
        raise LoiTaiKhoan("Chưa có key để kiểm tra: hãy dán key Gemini vào ô.", "key_trong")
    return {**_kq_an_toan(kiem_tra(key)), "nguon": nguon, "cuoi": bon_ky_tu_cuoi(key)}


def xoa_key(u: NguoiDung) -> None:
    db.dat_key(u.id, None)


def _kq_an_toan(kq: dict) -> dict:
    return {k: kq.get(k) for k in ("hop_le", "het_han_muc", "thong_diep", "mo_hinh")}


def thong_tin(u: NguoiDung | None) -> dict:
    """Thông tin tài khoản cho giao diện – KHÔNG có key, chỉ 4 ký tự cuối."""
    return {
        "dang_nhap": u is not None,
        "nguoi_dung": None if u is None else {"ten": u.ten, "email": u.email, "anh": u.anh, "cuc_bo": u.cuc_bo,
                                              "dang_nhap_bang": u.dang_nhap_bang},
        "la_chu_app": bool(u and la_chu_app(u)),
        "la_ki_cuu": bool(u and u.la_ki_cuu and not la_chu_app(u)),
        "key": {"co": bool(u and u.co_key), "cuoi": u.key_cuoi if u and u.co_key else ""},
        "dung_thu": {"gioi_han": settings.free_trial_limit, "da_dung": u.luot_da_dung if u else 0,
                     "con_lai": luot_con_lai(u) if u else settings.free_trial_limit},
        "nguon_key_giai": nguon_key_giai(u),
        "ma_hoa_san_sang": ma_hoa_san_sang(),
        "co_key_chu_app": co_key_chu_app(),
    }
