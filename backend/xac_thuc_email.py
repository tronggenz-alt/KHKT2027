"""Xác thực email bằng mã 6 số (đăng ký, tài khoản cũ chưa xác thực, quên mật khẩu) + chống tạo tài khoản hàng loạt.

- Mã: 6 chữ số ngẫu nhiên (secrets), hiệu lực 10 phút, CHỈ lưu bản băm HMAC (bao_mat.bam_ma_email) – không ghi log, không trả về trình duyệt.
  Gửi mã mới → mã cũ cùng mục đích hết hiệu lực. Nhập sai 5 lần → khoá mã đó, phải gửi mã mới.
- Gửi lại: mỗi email tối thiểu 60 giây/lần, tối đa 5 mã / 24 giờ (đếm trong CSDL nên khởi động lại app vẫn giữ).
- Gửi bằng Resend (RESEND_API_KEY) hoặc SMTP (SMTP_USER/SMTP_PASS, vd Gmail + App Password) – cấu hình trong .env.
- Cloudflare Turnstile ở form đăng ký (TURNSTILE_*). Chặn email tạm thời / rác: email_rac.py.
"""
from __future__ import annotations

import logging
import secrets
import smtplib
import ssl
import time
from email.message import EmailMessage
from email.utils import formataddr, make_msgid

import requests

from . import db, email_rac
from .bao_mat import bam_ma_email
from .config import settings

log = logging.getLogger(__name__)

HIEU_LUC = 600          # mã có hiệu lực 10 phút
CHO_GUI_LAI = 60        # giây giữa hai lần gửi cho cùng một email
GUI_TOI_DA_MOT_NGAY = 5  # số mã gửi tối đa cho một email trong 24 giờ
SAI_TOI_DA = 5          # nhập sai bao nhiêu lần thì khoá mã

MUC_DICH = {"xac_thuc": "xác thực email", "dat_lai_mk": "đặt lại mật khẩu"}


class LoiXacThuc(Exception):
    """Thông điệp tiếng Việt cho người dùng + mã lỗi + mã HTTP (+ số giây phải chờ nếu có)."""

    def __init__(self, thong_diep: str, ma: str, status: int = 400, cho: int | None = None):
        super().__init__(thong_diep)
        self.ma = ma
        self.status = status
        self.cho = cho


# ---------------------------------------------------------------- email tạm thời (giữ tên hàm cũ – logic ở email_rac.py)
def la_email_tam(email: str) -> bool:
    return email_rac.la_email_tam(email)


# ---------------------------------------------------------------- Cloudflare Turnstile
URL_TURNSTILE = "https://challenges.cloudflare.com/turnstile/v0/siteverify"


def kiem_turnstile(token: str, ip: str = "") -> None:
    """Chưa cấu hình TURNSTILE_* thì bỏ qua. Có cấu hình: token sai/thiếu → lỗi; không gọi được Cloudflare → lỗi (không cho qua)."""
    if not settings.turnstile_bat:
        return
    if not token:
        raise LoiXacThuc("Hãy hoàn thành ô xác minh “Tôi không phải robot” trước.", "captcha")
    try:
        r = requests.post(URL_TURNSTILE, data={"secret": settings.turnstile_secret_key, "response": token[:2048], "remoteip": ip},
                          timeout=10)
        kq = r.json()
    except (requests.RequestException, ValueError) as e:
        log.warning("Không kiểm tra được Turnstile: %s", type(e).__name__)
        raise LoiXacThuc("Không kiểm tra được ô xác minh (lỗi mạng) – hãy thử lại.", "captcha_loi_mang", 503) from e
    if not kq.get("success"):
        log.info("Turnstile từ chối: %s", kq.get("error-codes"))
        raise LoiXacThuc("Xác minh “Tôi không phải robot” không thành công hoặc đã hết hạn – hãy làm lại.", "captcha")


# ---------------------------------------------------------------- gửi email
def _noi_dung(ma: str, muc_dich: str, ten: str) -> tuple[str, str, str]:
    viec = MUC_DICH[muc_dich]
    tieu_de = "CHEMISTRY – mã " + viec
    chao = f"Chào {ten}," if ten else "Chào bạn,"
    van_ban = (f"{chao}\n\nMã {viec} CHEMISTRY của bạn là: {ma}\n\nMã có hiệu lực trong {HIEU_LUC // 60} phút. "
               "Đừng chia sẻ mã này với ai.\nNếu bạn không yêu cầu, hãy bỏ qua email này.\n\n— CHEMISTRY")
    html = (f'<div style="font-family:Arial,sans-serif;font-size:15px;color:#1b2621;line-height:1.5">'
            f"<p>{chao}</p><p>Mã {viec} <b>CHEMISTRY</b> của bạn là:</p>"
            f'<p style="font-size:30px;font-weight:700;letter-spacing:6px;margin:12px 0">{ma}</p>'
            f"<p>Mã có hiệu lực trong {HIEU_LUC // 60} phút. Đừng chia sẻ mã này với ai.<br>"
            "Nếu bạn không yêu cầu, hãy bỏ qua email này.</p><p>— CHEMISTRY</p></div>")
    return tieu_de, van_ban, html


def khoa_brevo(tho: str) -> tuple[str, str]:
    """BREVO_API_KEY → (key dùng được, dạng). Nhận key thường "xkeysib-…" và key MCP của Brevo (base64 của JSON {"api_key": "xkeysib-…"});
    bỏ dấu cách / ngoặc kép thừa khi dán. "dạng" chỉ để ghi nhật ký chẩn đoán – không chứa nội dung key."""
    k = (tho or "").strip().strip('"').strip("'").strip()
    if k.startswith("xkeysib-"):
        return k, "xkeysib"
    if k.startswith("xsmtpsib-"):
        return k, "xsmtpsib (SMTP key – không dùng được cho API)"
    try:
        import base64
        import json as _json
        trong = _json.loads(base64.b64decode(k + "=" * (-len(k) % 4)).decode("utf-8"))
        if isinstance(trong, dict) and str(trong.get("api_key", "")).startswith("xkeysib-"):
            return str(trong["api_key"]), "MCP (base64)"
    except (ValueError, UnicodeDecodeError):
        pass
    return k, f"lạ (dài {len(k)} ký tự)"


def gui_thu(den: str, tieu_de: str, van_ban: str, html: str) -> None:
    """Gửi một email: Resend (RESEND_API_KEY) → Brevo (BREVO_API_KEY) → SMTP. Lỗi → LoiXacThuc (không lộ mật khẩu/key trong log).
    Máy chủ chặn cổng SMTP (vd Render Free) → dùng Brevo: API web, miễn phí, gửi được từ địa chỉ Gmail đã xác minh, không cần tên miền."""
    if settings.brevo_api_key and not settings.resend_api_key:
        khoa, dang = khoa_brevo(settings.brevo_api_key)
        try:
            r = requests.post("https://api.brevo.com/v3/smtp/email", timeout=20,
                              headers={"api-key": khoa, "accept": "application/json"},
                              json={"sender": {"name": "CHEMISTRY", "email": settings.email_from or settings.smtp_user},
                                    "to": [{"email": den}], "subject": tieu_de, "textContent": van_ban, "htmlContent": html})
        except requests.RequestException as e:
            log.warning("Brevo: lỗi mạng %s", type(e).__name__)
            raise LoiXacThuc("Không gửi được email (lỗi mạng) – hãy thử lại sau ít phút.", "gui_email_loi", 502) from e
        if r.status_code >= 300:
            try:   # lời giải thích của Brevo (vd "unrecognised IP address", "Key not found") – không chứa key
                ly_do = str(r.json().get("message", ""))[:300]
            except ValueError:
                ly_do = ""
            log.warning("Brevo từ chối gửi email: HTTP %s – %s (BREVO_API_KEY dạng %s)", r.status_code,
                        ly_do.replace(khoa, "***"), dang)
            raise LoiXacThuc("Máy chủ email từ chối gửi – báo chủ app kiểm tra cấu hình BREVO_API_KEY/EMAIL_FROM.", "gui_email_loi", 502)
        return
    if settings.resend_api_key:
        try:
            r = requests.post("https://api.resend.com/emails", timeout=20,
                              headers={"Authorization": f"Bearer {settings.resend_api_key}"},
                              json={"from": formataddr(("CHEMISTRY", settings.email_from)), "to": [den],
                                    "subject": tieu_de, "text": van_ban, "html": html})
        except requests.RequestException as e:
            log.warning("Resend: lỗi mạng %s", type(e).__name__)
            raise LoiXacThuc("Không gửi được email (lỗi mạng) – hãy thử lại sau ít phút.", "gui_email_loi", 502) from e
        if r.status_code >= 300:
            log.warning("Resend từ chối gửi email: HTTP %s", r.status_code)
            raise LoiXacThuc("Máy chủ email từ chối gửi – báo chủ app kiểm tra cấu hình RESEND_API_KEY/EMAIL_FROM.", "gui_email_loi", 502)
        return
    thu = EmailMessage()
    thu["Subject"] = tieu_de
    thu["From"] = formataddr(("CHEMISTRY", settings.email_from or settings.smtp_user))
    thu["To"] = den
    thu["Message-ID"] = make_msgid(domain=(settings.email_from or settings.smtp_user).rsplit("@", 1)[-1] or None)
    thu.set_content(van_ban)
    thu.add_alternative(html, subtype="html")
    try:
        ngu_canh = ssl.create_default_context()
        if settings.smtp_port == 465:
            may = smtplib.SMTP_SSL(settings.smtp_host, settings.smtp_port, timeout=20, context=ngu_canh)
        else:
            may = smtplib.SMTP(settings.smtp_host, settings.smtp_port, timeout=20)
        with may:
            if settings.smtp_port != 465:
                may.ehlo()  # phải EHLO trước thì mới biết máy chủ có STARTTLS không
                if may.has_extn("starttls"):
                    may.starttls(context=ngu_canh)
                    may.ehlo()
                elif settings.smtp_host not in ("127.0.0.1", "localhost", "::1"):
                    raise LoiXacThuc("Máy chủ email không hỗ trợ mã hoá (STARTTLS) – app không gửi mật khẩu SMTP qua kết nối không mã hoá. "
                                     "Báo chủ app kiểm tra SMTP_HOST/SMTP_PORT.", "gui_email_loi", 502)
            if settings.smtp_user and settings.smtp_pass:
                try:
                    may.login(settings.smtp_user, settings.smtp_pass)
                except smtplib.SMTPServerDisconnected as e:  # Gmail: từ chối AUTH PLAIN (535) rồi đóng kết nối khi thử AUTH LOGIN
                    raise smtplib.SMTPAuthenticationError(535, b"dong ket noi khi dang nhap") from e
            may.send_message(thu)
    except smtplib.SMTPAuthenticationError as e:
        log.warning("SMTP: đăng nhập thất bại (mã %s) – kiểm tra SMTP_USER/SMTP_PASS (App Password)", e.smtp_code)
        raise LoiXacThuc("Máy chủ email của app đăng nhập không thành công – báo chủ app kiểm tra SMTP_USER/SMTP_PASS.",
                         "gui_email_loi", 502) from e
    except smtplib.SMTPRecipientsRefused as e:
        raise LoiXacThuc("Địa chỉ email này không nhận thư – hãy kiểm tra lại email.", "email_khong_nhan", 400) from e
    except (smtplib.SMTPException, OSError) as e:
        log.warning("SMTP: không gửi được email: %s", type(e).__name__)
        raise LoiXacThuc("Không gửi được email (lỗi máy chủ email/mạng) – hãy thử lại sau ít phút.", "gui_email_loi", 502) from e


# ---------------------------------------------------------------- mã xác thực
def _lan_gui(email_chuan: str, tu: float) -> list[float]:
    with db.ket_noi() as con:
        return [r[0] for r in con.execute(
            "SELECT created FROM email_codes WHERE email_norm = ? AND created >= ? AND status <> 'loi' ORDER BY created",
            (email_chuan, tu))]


def cho_gui_lai(email_chuan: str) -> int:
    """Số giây còn phải chờ trước khi được gửi mã mới cho email này (0 = gửi được ngay)."""
    bay_gio = time.time()
    lan = _lan_gui(email_chuan, bay_gio - 86400)
    cho = 0.0
    if lan:
        cho = max(cho, lan[-1] + CHO_GUI_LAI - bay_gio)
    if len(lan) >= GUI_TOI_DA_MOT_NGAY:
        cho = max(cho, lan[-GUI_TOI_DA_MOT_NGAY] + 86400 - bay_gio)
    return max(0, int(cho + 0.999))


def gui_ma(email: str, muc_dich: str, uid: int | None, ten: str = "") -> int:
    """Tạo + gửi mã mới (mã cũ cùng mục đích hết hiệu lực). Trả số giây đến lúc được bấm "Gửi lại mã"."""
    if muc_dich not in MUC_DICH:
        raise ValueError(muc_dich)
    if not settings.gui_email_san_sang:
        raise LoiXacThuc("Máy chủ chưa cấu hình gửi email (SMTP) nên chưa gửi được mã xác thực – hãy báo chủ app.",
                         "chua_cau_hinh_email", 503)
    norm = db.email_chuan(email)
    cho = cho_gui_lai(norm)
    if cho > 0:
        nhieu = len(_lan_gui(norm, time.time() - 86400)) >= GUI_TOI_DA_MOT_NGAY
        sau = f"{(cho + 3599) // 3600} giờ" if cho > 3600 else f"{(cho + 59) // 60} phút"
        raise LoiXacThuc(
            f"Đã gửi {GUI_TOI_DA_MOT_NGAY} mã trong 24 giờ cho email này – hãy thử lại sau {sau}." if nhieu
            else f"Vừa gửi mã – hãy chờ {cho} giây rồi mới gửi lại.", "cho_gui_lai", 429, cho)
    ma = f"{secrets.randbelow(10**6):06d}"
    bay_gio = time.time()
    with db.ket_noi() as con:
        con.execute("UPDATE email_codes SET status = 'thay_the' WHERE email_norm = ? AND purpose = ? AND status = 'cho'",
                    (norm, muc_dich))
        ma_id = con.execute(
            "INSERT INTO email_codes (email_norm, purpose, user_id, code_hash, created, expires) VALUES (?, ?, ?, ?, ?, ?)",
            (norm, muc_dich, uid, bam_ma_email(norm, muc_dich, ma), bay_gio, bay_gio + HIEU_LUC)).lastrowid
    try:
        gui_thu(email, *_noi_dung(ma, muc_dich, ten))
    except LoiXacThuc:
        with db.ket_noi() as con:  # gửi hỏng: không tính vào giới hạn gửi lại, mã không dùng được
            con.execute("UPDATE email_codes SET status = 'loi' WHERE id = ?", (ma_id,))
        raise
    log.info("Đã gửi mã %s cho người dùng %s", muc_dich, uid)
    return CHO_GUI_LAI


def kiem_ma(email: str, muc_dich: str, ma: str) -> int | None:
    """Mã đúng → đánh dấu đã dùng, trả user_id lúc gửi. Sai/hết hạn/bị khoá → LoiXacThuc (số lần sai được lưu trước khi báo lỗi)."""
    ma = "".join(c for c in (ma or "") if c.isdigit())
    norm = db.email_chuan(email)
    loi: LoiXacThuc | None = None
    with db.ket_noi() as con:  # lỗi ném ra TRONG khối này sẽ rollback → chỉ ném sau khi đã ghi
        row = con.execute("SELECT * FROM email_codes WHERE email_norm = ? AND purpose = ? AND status <> 'loi' "
                          "ORDER BY id DESC LIMIT 1", (norm, muc_dich)).fetchone()
        if row is None or row["status"] in ("da_dung", "thay_the", "het_han"):
            loi = LoiXacThuc("Chưa có mã đang hiệu lực – hãy bấm “Gửi lại mã”.", "ma_het_han")
        elif row["status"] == "khoa":
            loi = LoiXacThuc(f"Đã nhập sai {SAI_TOI_DA} lần nên mã bị khoá – hãy bấm “Gửi lại mã” để nhận mã mới.", "ma_bi_khoa")
        elif row["expires"] < time.time():
            con.execute("UPDATE email_codes SET status = 'het_han' WHERE id = ?", (row["id"],))
            loi = LoiXacThuc(f"Mã đã hết hạn ({HIEU_LUC // 60} phút) – hãy bấm “Gửi lại mã”.", "ma_het_han")
        elif len(ma) != 6 or not secrets.compare_digest(bam_ma_email(norm, muc_dich, ma), row["code_hash"]):
            sai = row["attempts"] + 1
            khoa = sai >= SAI_TOI_DA
            con.execute("UPDATE email_codes SET attempts = ?, status = ? WHERE id = ?", (sai, "khoa" if khoa else "cho", row["id"]))
            loi = (LoiXacThuc(f"Sai mã {SAI_TOI_DA} lần – mã đã bị khoá, hãy bấm “Gửi lại mã” để nhận mã mới.", "ma_bi_khoa") if khoa
                   else LoiXacThuc(f"Mã không đúng – còn {SAI_TOI_DA - sai} lần thử.", "ma_sai"))
        else:
            con.execute("UPDATE email_codes SET status = 'da_dung' WHERE id = ?", (row["id"],))
    if loi is not None:
        raise loi
    return row["user_id"]


def don_dep(giu_ngay: int = 2) -> None:
    with db.ket_noi() as con:
        con.execute("DELETE FROM email_codes WHERE created < ?", (time.time() - giu_ngay * 86400,))
