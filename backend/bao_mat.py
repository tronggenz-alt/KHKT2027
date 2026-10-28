"""Bảo mật: mã hoá key Gemini của người dùng (Fernet), khoá ký cookie phiên, băm mật khẩu (scrypt), mã CSRF, giới hạn tần suất.

Mọi khoá con đều suy ra từ một khoá chính bằng HKDF-SHA256, mỗi mục đích một `info` riêng. Khoá chính = APP_SECRET_KEY trong .env
nếu có (≥ 32 ký tự), không thì app TỰ TẠO một lần và lưu ở data/app_secret.key (quyền 600) – dùng được ngay, không cần cấu hình.
Đổi/xoá khoá chính là mọi key Gemini đã lưu không giải mã được nữa (người dùng phải nhập lại) và mọi phiên đăng nhập bị huỷ.
"""
from __future__ import annotations

import base64
import hashlib
import hmac
import logging
import os
import secrets
import threading
import time
from collections import defaultdict, deque
from pathlib import Path

from cryptography.fernet import Fernet, InvalidToken, MultiFernet
from cryptography.hazmat.primitives import hashes
from cryptography.hazmat.primitives.kdf.hkdf import HKDF

from .config import DATA_DIR, settings

log = logging.getLogger(__name__)

DO_DAI_KHOA_CHINH_TOI_THIEU = 32
TEP_KHOA_CHINH = DATA_DIR / "app_secret.key"  # khoá chính tự tạo khi .env chưa có APP_SECRET_KEY (không commit)
_khoa_tu_tep: dict[Path, str] = {}
_khoa_tep_lock = threading.Lock()


class LoiBaoMat(Exception):
    """Lỗi cấu hình/giải mã; thông điệp tiếng Việt hiện được cho người dùng (không chứa key)."""


# ---------------------------------------------------------------- khoá chính (APP_SECRET_KEY hoặc data/app_secret.key)
def _khoa_chinh() -> str:
    if len(settings.app_secret_key) >= DO_DAI_KHOA_CHINH_TOI_THIEU:
        return settings.app_secret_key
    tep = TEP_KHOA_CHINH
    with _khoa_tep_lock:
        if tep in _khoa_tu_tep:
            return _khoa_tu_tep[tep]
        try:
            if not tep.is_file():
                tep.parent.mkdir(parents=True, exist_ok=True)
                fd = os.open(tep, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)  # chỉ chủ máy đọc được
                with os.fdopen(fd, "w") as f:
                    f.write(secrets.token_urlsafe(48) + "\n")
                log.info("Đã tự tạo khoá chính %s (giữ file này; xoá/đổi là mọi key đã lưu phải nhập lại)", tep)
            khoa = tep.read_text(encoding="utf-8").strip()
        except OSError as e:
            log.error("Không tạo/đọc được khoá chính %s: %s", tep, e)
            return ""
        if len(khoa) < DO_DAI_KHOA_CHINH_TOI_THIEU:
            log.error("Khoá chính %s quá ngắn – bỏ qua", tep)
            return ""
        _khoa_tu_tep[tep] = khoa
        return khoa


def ma_hoa_san_sang() -> bool:
    return bool(_khoa_chinh())


def _suy_khoa(muc_dich: bytes) -> bytes:
    khoa = _khoa_chinh()
    if not khoa:
        raise LoiBaoMat(f"App không tạo được khoá mã hoá ({TEP_KHOA_CHINH}) nên chưa lưu được key – kiểm tra quyền ghi thư mục data/.")
    return HKDF(algorithm=hashes.SHA256(), length=32, salt=None, info=b"giai-chuoi-huu-co/" + muc_dich).derive(khoa.encode("utf-8"))


def _fernet() -> Fernet:
    return Fernet(base64.urlsafe_b64encode(_suy_khoa(b"gemini-key/v1")))


def ma_hoa_key(key: str) -> str:
    """Mã hoá key (Fernet = AES-128-CBC + HMAC-SHA256, có nonce ngẫu nhiên) → chuỗi lưu vào CSDL."""
    return _fernet().encrypt(key.encode("utf-8")).decode("ascii")


def giai_ma_key(ban_ma: str) -> str:
    try:
        return _fernet().decrypt(ban_ma.encode("ascii")).decode("utf-8")
    except (InvalidToken, ValueError) as e:
        raise LoiBaoMat("Không giải mã được key Gemini đã lưu (khoá chính của app đã bị đổi?). Hãy vào Cài đặt nhập lại key.") from e


def _fernet_claude() -> MultiFernet:
    """Mã hoá API key Claude riêng: khoá từ USER_KEY_SECRET (nếu đặt) + khoá suy từ khoá chính (để key đã lưu trước khi đặt
    USER_KEY_SECRET vẫn giải mã được). Mã hoá luôn bằng khoá đầu tiên."""
    ds = []
    if settings.user_key_secret:
        ds.append(Fernet(base64.urlsafe_b64encode(HKDF(algorithm=hashes.SHA256(), length=32, salt=None,
                                                       info=b"giai-chuoi-huu-co/user-key/v1").derive(settings.user_key_secret.encode()))))
    if ma_hoa_san_sang():
        ds.append(Fernet(base64.urlsafe_b64encode(_suy_khoa(b"claude-key/v1"))))
    if not ds:
        raise LoiBaoMat("App chưa có khoá mã hoá (USER_KEY_SECRET hoặc khoá chính) nên chưa lưu được key Claude.")
    return MultiFernet(ds)


def ma_hoa_key_claude(key: str) -> str:
    return _fernet_claude().encrypt(key.encode("utf-8")).decode("ascii")


def giai_ma_key_claude(ban_ma: str) -> str:
    try:
        return _fernet_claude().decrypt(ban_ma.encode("ascii")).decode("utf-8")
    except (InvalidToken, ValueError) as e:
        raise LoiBaoMat("Không giải mã được API key Claude đã lưu (khoá mã hoá đã đổi?) – hãy nhập lại key trong Cài đặt.") from e


def _fernet_notebooklm() -> Fernet:
    return Fernet(base64.urlsafe_b64encode(_suy_khoa(b"notebooklm-cookie/v1")))


def ma_hoa_cookie_notebooklm(cookie: str) -> str:
    """Mã hoá cookie Google NotebookLM (AES-128-CBC + HMAC-SHA256) trước khi lưu CSDL."""
    return _fernet_notebooklm().encrypt(cookie.encode("utf-8")).decode("ascii")


def giai_ma_cookie_notebooklm(ban_ma: str) -> str:
    """Giải mã cookie Google NotebookLM."""
    try:
        return _fernet_notebooklm().decrypt(ban_ma.encode("ascii")).decode("utf-8")
    except (InvalidToken, ValueError) as e:
        raise LoiBaoMat("Không giải mã được cookie NotebookLM đã lưu. Hãy cập nhật lại cookie trong Admin.") from e


def bon_ky_tu_cuoi(key: str) -> str:
    return key[-4:] if len(key) >= 8 else ""


def khoa_ky_phien() -> str:
    """Khoá ký cookie phiên (suy từ khoá chính → khởi động lại app vẫn giữ đăng nhập). Không có khoá chính → khoá tạm theo tiến trình."""
    if ma_hoa_san_sang():
        return base64.urlsafe_b64encode(_suy_khoa(b"session/v1")).decode("ascii")
    log.warning("Không có khoá chính → cookie phiên dùng khoá tạm, khởi động lại app sẽ phải đăng nhập lại.")
    return secrets.token_urlsafe(32)


def bam_ma_email(email_chuan: str, muc_dich: str, ma: str) -> str:
    """Mã xác thực email chỉ được lưu dạng HMAC-SHA256 (khoá suy từ khoá chính) – CSDL bị lộ cũng không đọc được mã."""
    du_lieu = f"{muc_dich}|{email_chuan}|{ma}".encode("utf-8")
    return hmac.new(_suy_khoa(b"ma-email/v1"), du_lieu, hashlib.sha256).hexdigest()


# ---------------------------------------------------------------- mật khẩu (tài khoản email)
_SCRYPT = {"n": 2**14, "r": 8, "p": 1}
MAT_KHAU_TOI_THIEU = 8


def bam_mat_khau(mat_khau: str) -> str:
    """scrypt (n=2^14, r=8, p=1) + muối ngẫu nhiên 16 byte → "scrypt$n$r$p$muối$băm" (base64)."""
    muoi = secrets.token_bytes(16)
    bam = hashlib.scrypt(mat_khau.encode("utf-8"), salt=muoi, dklen=32, **_SCRYPT)
    b64 = lambda b: base64.b64encode(b).decode("ascii")  # noqa: E731
    return f"scrypt${_SCRYPT['n']}${_SCRYPT['r']}${_SCRYPT['p']}${b64(muoi)}${b64(bam)}"


_BAM_GIA = bam_mat_khau(secrets.token_urlsafe(12))  # để thời gian trả lời như nhau khi email không tồn tại


def kiem_mat_khau(mat_khau: str, da_bam: str | None) -> bool:
    try:
        ten, n, r, p, muoi, bam = (da_bam or _BAM_GIA).split("$")
        tinh = hashlib.scrypt(mat_khau.encode("utf-8"), salt=base64.b64decode(muoi), dklen=32, n=int(n), r=int(r), p=int(p))
    except (ValueError, TypeError):
        return False
    return ten == "scrypt" and da_bam is not None and secrets.compare_digest(tinh, base64.b64decode(bam))


# ---------------------------------------------------------------- CSRF (mã đồng bộ lưu trong phiên)
def ma_csrf(session: dict) -> str:
    """Mã CSRF của phiên (tạo nếu chưa có). Trình duyệt gửi lại qua header X-CSRF-Token hoặc ô form csrf_token."""
    if not session.get("csrf"):
        session["csrf"] = secrets.token_urlsafe(32)
    return session["csrf"]


def csrf_hop_le(session: dict, gui_len: str | None) -> bool:
    dung = session.get("csrf")
    return bool(dung and gui_len) and secrets.compare_digest(str(dung), str(gui_len))


# ---------------------------------------------------------------- giới hạn tần suất (trong bộ nhớ, theo người dùng/IP)
# hành động → [(số lần tối đa, trong bao nhiêu giây), ...]
QUY_TAC_TAN_SUAT: dict[str, list[tuple[int, float]]] = {
    "giai": [(4, 60), (30, 3600)],
    "3d": [(20, 60), (200, 3600)],
    "kiem_tra_key": [(6, 60), (30, 3600)],
    "luu_key": [(10, 60)],
    "dang_nhap": [(20, 60)],
    "dang_ky": [(10, 3600), (30, 86400)],         # gửi form đăng ký: theo IP (3 lần thử/IP/giờ + 3 TÀI KHOẢN/IP/ngày ở CSDL – email_rac / auth.py)
    "dang_nhap_mk": [(10, 60), (40, 3600)],       # đăng nhập mật khẩu: theo IP
    "dang_nhap_email": [(8, 900)],                # đăng nhập mật khẩu: theo từng email (chống dò mật khẩu)
    "xac_thuc": [(15, 600), (60, 86400)],         # nhập mã email / đặt lại mật khẩu: theo IP (mỗi mã còn tối đa 5 lần sai)
    "gui_ma": [(10, 3600)],                       # gửi lại mã / quên mật khẩu: theo IP (mỗi email: 60 giây/lần, 5 mã/24 giờ)
    "doi_ten": [(10, 60)],
    "quan_tri": [(120, 60)],
    "bao_sai": [(5, 60), (30, 3600)],  # người dùng báo lời giải sai (AI Riêng – hàng chờ admin)
    "chemdraw": [(10, 60)],
    "co_che": [(10, 60), (80, 3600)],
    "co_che_anh": [(6, 60), (60, 3600)],          # mục Cơ chế: đọc ảnh đề (1–4 ảnh/lần)
    "sinh_hoc": [(6, 60), (40, 3600), (150, 86400)],  # AI Bio-Tutor của Phòng Lab Sinh học 3D (key chủ app – chặn spam tốn tiền)
    "sinh_hoc_anh": [(2, 60), (10, 3600), (20, 86400)],  # Giải thích từ ảnh sinh học 3D (tối đa 20 ảnh/ngày)
    "phuong_trinh": [(10, 60), (100, 3600)],      # "Xem phương trình chi tiết" (một lần gọi AI rẻ cho mỗi bài, lưu lại)
    "ma_giao_dien": [(10, 60), (40, 3600)],       # nhập mã giao diện (+K) – khoá 5 lần sai/giờ nằm ở CSDL (ma_giao_dien.py)
    "ma_claude": [(10, 60)],                      # nhập mã Claude (khoá 5 lần sai/giờ nằm ở CSDL – ma_claude.py)
    "key_claude": [(10, 60)],                     # nhập Key Claude (khoá 5 lần sai/giờ theo tài khoản + IP nằm ở CSDL – key_claude.py)
    "doi_ma_app": [(10, 60)],                     # đổi mã đăng nhập desktop app (10 lần/phút theo IP)
}


class GioiHanTanSuat:
    """Cửa sổ trượt: ghi thời điểm từng lần gọi theo (hành động, người gọi)."""

    def __init__(self, quy_tac: dict[str, list[tuple[int, float]]] | None = None, dong_ho=time.monotonic):
        self.quy_tac = quy_tac if quy_tac is not None else QUY_TAC_TAN_SUAT
        self._dong_ho = dong_ho
        self._lan: dict[tuple[str, str], deque[float]] = defaultdict(deque)
        self._khoa = threading.Lock()

    def kiem_tra(self, hanh_dong: str, ai: str) -> float | None:
        """Được phép → ghi lại lần gọi, trả None. Vượt giới hạn → trả số giây phải chờ (không ghi)."""
        cac_quy_tac = self.quy_tac.get(hanh_dong)
        if not cac_quy_tac:
            return None
        bay_gio = self._dong_ho()
        dai_nhat = max(giay for _, giay in cac_quy_tac)
        with self._khoa:
            q = self._lan[(hanh_dong, ai)]
            while q and q[0] <= bay_gio - dai_nhat:
                q.popleft()
            cho = 0.0
            for so_lan, giay in cac_quy_tac:
                trong_cua_so = [t for t in q if t > bay_gio - giay]
                if len(trong_cua_so) >= so_lan:
                    cho = max(cho, trong_cua_so[-so_lan] + giay - bay_gio)
            if cho > 0:
                return cho
            q.append(bay_gio)
            if len(self._lan) > 10_000:  # dọn người gọi đã im lâu
                for k in [k for k, v in self._lan.items() if not v or v[-1] <= bay_gio - 3600]:
                    del self._lan[k]
            return None

    def xoa(self) -> None:
        with self._khoa:
            self._lan.clear()


gioi_han_tan_suat = GioiHanTanSuat()
