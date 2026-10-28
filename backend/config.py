"""Cấu hình chung: đọc .env, đường dẫn thư mục, font, logging."""
from __future__ import annotations

import logging
import os
import re
from dataclasses import dataclass, field
from logging.handlers import RotatingFileHandler
from pathlib import Path

from dotenv import dotenv_values

BASE_DIR = Path(__file__).resolve().parent.parent
FRONTEND_DIR = BASE_DIR / "frontend"
# STORAGE_DIR (biến môi trường): đặt dữ liệu ra ổ đĩa riêng khi chạy trên máy chủ (vd Render: /var/data) – trống = trong thư mục dự án
_LUU_TRU = Path(os.environ["STORAGE_DIR"]) if os.environ.get("STORAGE_DIR", "").strip() else BASE_DIR
OUTPUT_DIR = _LUU_TRU / "outputs"
LOGS_DIR = _LUU_TRU / "logs"
DATA_DIR = _LUU_TRU / "data"          # CSDL SQLite người dùng + lịch sử (không commit)
ENV_FILE = BASE_DIR / ".env"

DEFAULT_MODEL = "claude-sonnet-5-5"
# Ba bậc Gemini theo độ khó đề (do_kho.py): Dễ → Flash-Lite, Trung bình → Flash, HSG → Pro.
# Đổi bằng GEMINI_MODEL_LITE / GEMINI_MODEL_FLASH / GEMINI_MODEL_PRO trong .env (không viết cứng tên mô hình trong code).
DEFAULT_GEMINI_MODEL_LITE = "gemini-flash-lite-latest"
# Bí danh luôn trỏ tới Gemini Flash mới nhất; nếu không có thì llm.py thử các tên trong GEMINI_MODEL_DU_PHONG
DEFAULT_GEMINI_MODEL = "gemini-flash-latest"
# Chế độ "Đề khó (HSG)": ưu tiên mô hình Pro (suy luận tốt hơn, chậm hơn); hết hạn mức thì lùi về GEMINI_MODEL
DEFAULT_GEMINI_MODEL_HSG = "gemini-pro-latest"
# (gemini-2.5-* không còn cho tài khoản mới – 10/2026)
# gemini-flash-lite-latest: dự phòng cuối cùng (hạn mức miễn phí rộng) khi các bản Flash đã hết lượt trong ngày
GEMINI_MODEL_DU_PHONG = ["gemini-3.6-flash", "gemini-3-flash-preview", "gemini-flash-lite-latest"]
# Mô hình Claude mạnh nhất – dùng cho việc khó (cơ chế mức HSG quốc gia); đổi bằng CLAUDE_MODEL_MANH trong .env
DEFAULT_CLAUDE_MODEL_MANH = "claude-opus-5-5"

# Font hỗ trợ tiếng Việt (theo thứ tự ưu tiên): macOS, rồi Linux (máy chủ Docker: fonts-liberation2 ≈ Arial, fonts-dejavu-core)
_LIBERATION = "/usr/share/fonts/truetype/liberation2/LiberationSans"
_DEJAVU = "/usr/share/fonts/truetype/dejavu/DejaVuSans"
FONT_REGULAR = [
    "/System/Library/Fonts/Supplemental/Arial.ttf",
    "/Library/Fonts/Arial Unicode.ttf",
    "/System/Library/Fonts/Supplemental/Arial Unicode.ttf",
    "/System/Library/Fonts/Helvetica.ttc",
    f"{_LIBERATION}-Regular.ttf",
    f"{_DEJAVU}.ttf",
]
FONT_BOLD = [
    "/System/Library/Fonts/Supplemental/Arial Bold.ttf",
    "/System/Library/Fonts/Helvetica.ttc",
    f"{_LIBERATION}-Bold.ttf",
    f"{_DEJAVU}-Bold.ttf",
]
FONT_ITALIC = [
    "/System/Library/Fonts/Supplemental/Arial Italic.ttf",
    "/System/Library/Fonts/Supplemental/Arial.ttf",
    f"{_LIBERATION}-Italic.ttf",
    f"{_DEJAVU}.ttf",
]
FONT_FALLBACK = [
    "/Library/Fonts/Arial Unicode.ttf",
    "/System/Library/Fonts/Supplemental/Arial Unicode.ttf",
    f"{_DEJAVU}.ttf",
]


def _read_env() -> dict[str, str]:
    """Đọc .env; chấp nhận cả trường hợp người dùng chỉ dán mỗi key (không có 'TÊN_BIẾN=').

    Dòng key trần bắt đầu bằng 'sk-ant-' → ANTHROPIC_API_KEY; dòng key trần khác (≥ 30 ký tự, không dấu cách) → GEMINI_API_KEY.
    """
    values: dict[str, str] = {}
    key_tran: list[str] = []
    if ENV_FILE.exists():
        for k, v in dotenv_values(ENV_FILE).items():
            if v is None and k:
                key_tran.append(k.strip())
            elif v is not None:
                values[k] = v.strip()
    for k in key_tran:
        if k.startswith("sk-ant-"):
            values.setdefault("ANTHROPIC_API_KEY", k)
        elif len(k) >= 30 and " " not in k and not values.get("GEMINI_API_KEY"):
            values["GEMINI_API_KEY"] = k
    # Biến môi trường của hệ thống được ưu tiên hơn .env
    for k in ("ANTHROPIC_API_KEY", "CLAUDE_MODEL", "CLAUDE_EFFORT", "CLAUDE_FALLBACK",
              "GEMINI_API_KEY", "GOOGLE_API_KEY", "GEMINI_MODEL", "GEMINI_MODEL_HSG", "GEMINI_FALLBACK",
              "GEMINI_MODEL_LITE", "GEMINI_MODEL_FLASH", "GEMINI_MODEL_PRO",
              "GOOGLE_CLIENT_ID", "GOOGLE_CLIENT_SECRET", "APP_SECRET_KEY", "FREE_TRIAL_LIMIT", "COOKIE_SECURE",
              "OAUTH_REDIRECT_URI", "ADMIN_EMAILS", "CLAUDE_MODEL_MANH",
              "SMTP_HOST", "SMTP_PORT", "SMTP_USER", "SMTP_PASS", "EMAIL_FROM", "RESEND_API_KEY", "BREVO_API_KEY",
              "TURNSTILE_SITE_KEY", "TURNSTILE_SECRET_KEY", "TRUSTED_PROXIES", "GEOIP_CITY_DB", "GEOIP_ASN_DB",
              "CLAUDE_ACCESS_KEY", "CLAUDE_DAILY_LIMIT", "ANTHROPIC_ADMIN_KEY", "USER_KEY_SECRET",
              "THINKING_NHANH", "THINKING_CAN_BANG", "THINKING_KY", "THINKING_TOI_DA", "MAX_TOKENS_NHANH", "MAX_TOKENS_CAN_BANG",
              "MAX_TOKENS_KY", "MAX_TOKENS_TOI_DA", "CLAUDE_EFFORT_KY", "CLAUDE_EFFORT_TOI_DA", "CACHE_GIAI_NGAY",
              "UNPAYWALL_EMAIL", "S2_API_KEY", "TRA_CUU_TU_DONG",
              "OPENAI_API_KEY", "OPENAI_MODEL", "OPENAI_TEMPERATURE", "OPENAI_MAX_TOKENS", "AI_TIMEOUT_GIAY", "AI_TIMEOUT_DAI_GIAY",
              "EMBED_MODEL", "EMBED_DIM", "CHE_DO_CUC_BO", "CAP_ADMIN_EMAIL", "NOTEBOOKLM_COOKIE_KEY",
              "CHE_DO_APP"):
        if os.environ.get(k):
            values[k] = os.environ[k].strip()
    return values


@dataclass
class Settings:
    api_key: str            # ANTHROPIC_API_KEY
    model: str              # CLAUDE_MODEL
    effort: str             # CLAUDE_EFFORT
    fallback: bool          # CLAUDE_FALLBACK: dự phòng phía máy chủ Anthropic (giữa các mô hình Claude)
    gemini_api_key: str = ""     # GEMINI_API_KEY (hoặc GOOGLE_API_KEY)
    gemini_model: str = DEFAULT_GEMINI_MODEL   # bậc FLASH (đề Trung bình): GEMINI_MODEL_FLASH (tên cũ: GEMINI_MODEL)
    gemini_fallback: bool = True  # GEMINI_FALLBACK: tự chuyển Claude → Gemini khi Claude lỗi/hết tiền
    gemini_model_hsg: str = DEFAULT_GEMINI_MODEL_HSG  # bậc PRO (đề HSG): GEMINI_MODEL_PRO (tên cũ: GEMINI_MODEL_HSG)
    gemini_model_lite: str = DEFAULT_GEMINI_MODEL_LITE  # bậc LITE (đề Dễ, phân loại độ khó): GEMINI_MODEL_LITE
    claude_model_manh: str = DEFAULT_CLAUDE_MODEL_MANH  # CLAUDE_MODEL_MANH: Claude mạnh nhất (cơ chế HSG)
    # ----- Đăng nhập Google + key Gemini riêng của từng người (BYOK)
    google_client_id: str = ""       # GOOGLE_CLIENT_ID (OAuth 2.0, Google Cloud Console)
    google_client_secret: str = ""   # GOOGLE_CLIENT_SECRET
    app_secret_key: str = ""         # APP_SECRET_KEY: khoá chính để mã hoá key người dùng + ký cookie phiên
    free_trial_limit: int = 3        # FREE_TRIAL_LIMIT: số bài dùng thử bằng key của chủ app cho người chưa có key
    cookie_secure: bool = True       # COOKIE_SECURE: cookie phiên chỉ gửi qua HTTPS (localhost vẫn được trên Chrome/Firefox)
    oauth_redirect_uri: str = ""     # OAUTH_REDIRECT_URI: để trống = tự lấy theo địa chỉ đang mở (…/auth/callback)
    admin_emails: frozenset[str] = frozenset()  # ADMIN_EMAILS: email chủ app, dùng key của chủ app không giới hạn
    # ----- Gửi mã xác thực email (đăng ký / quên mật khẩu): SMTP (vd Gmail + App Password) hoặc Resend
    smtp_host: str = "smtp.gmail.com"  # SMTP_HOST
    smtp_port: int = 587               # SMTP_PORT (587 = STARTTLS, 465 = SSL)
    smtp_user: str = ""                # SMTP_USER: địa chỉ Gmail gửi mã
    smtp_pass: str = ""                # SMTP_PASS: App Password 16 ký tự (KHÔNG phải mật khẩu Gmail)
    email_from: str = ""               # EMAIL_FROM: người gửi hiển thị (mặc định = SMTP_USER)
    resend_api_key: str = ""           # RESEND_API_KEY: có thì gửi qua Resend thay cho SMTP (EMAIL_FROM phải thuộc tên miền đã xác minh)
    # BREVO_API_KEY: gửi qua API web của Brevo (miễn phí 300 thư/ngày) – dùng khi máy chủ chặn cổng SMTP (Render Free);
    # EMAIL_FROM = địa chỉ đã xác minh trong Brevo (Senders), không cần tên miền riêng
    brevo_api_key: str = field(default="", repr=False)
    # ----- Cloudflare Turnstile (captcha miễn phí) ở form đăng ký; để trống = tắt
    turnstile_site_key: str = ""       # TURNSTILE_SITE_KEY
    turnstile_secret_key: str = ""     # TURNSTILE_SECRET_KEY
    # ----- IP thật của người dùng (dia_chi_ip.py)
    trusted_proxies: str = "127.0.0.1,::1,cloudflare"  # TRUSTED_PROXIES: IP/dải (CIDR) proxy được tin header IP; "cloudflare" = dải IP Cloudflare
    geoip_city_db: str = ""            # GEOIP_CITY_DB: file .mmdb vị trí (GeoLite2-City / DB-IP City Lite); trống = tự tìm trong data/geoip/
    geoip_asn_db: str = ""             # GEOIP_ASN_DB: file .mmdb nhà mạng (GeoLite2-ASN / DB-IP ASN Lite); trống = tự tìm trong data/geoip/
    # ----- Key Claude dùng chung (key_claude.py): người nhập đúng key được giải bằng Claude của máy chủ; KHÔNG in / gửi frontend / ghi log
    claude_access_key: str = field(default="", repr=False)  # CLAUDE_ACCESS_KEY: trống = tắt (không ai mở được Claude bằng key)
    claude_daily_limit: int = 10       # CLAUDE_DAILY_LIMIT: số bài Claude tối đa mỗi người mỗi ngày khi mở bằng key
    # ----- Admin API của Anthropic (claude_admin.py): đọc chi phí + token của Claude máy chủ; KHÔNG dùng để gọi Claude
    anthropic_admin_key: str = field(default="", repr=False)  # ANTHROPIC_ADMIN_KEY: sk-ant-admin01-… (trống = chỉ nhật ký nội bộ)
    # USER_KEY_SECRET: khoá mã hoá (Fernet) API key Claude riêng của người dùng; trống = suy từ khoá chính của app (vẫn mã hoá)
    user_key_secret: str = field(default="", repr=False)
    # ----- Thanh "mức độ thông minh" khi giải chuỗi (giai_nhanh.py): Nhanh · Cân bằng · Kỹ · Tối đa
    # THINKING_*: ngân sách suy nghĩ (token) – Gemini 2.5: thinking_budget; Gemini 3: đổi sang thinking_level (0 → minimal, ≤2048 → low,
    # ≤8192 → medium, lớn hơn → high). CLAUDE_EFFORT_*: output_config.effort của Claude. MAX_TOKENS_*: giới hạn đầu ra mỗi mức.
    thinking_nhanh: int = 0
    thinking_can_bang: int = 1024
    thinking_ky: int = 4096
    thinking_toi_da: int = 16384
    max_tokens_nhanh: int = 8000
    max_tokens_can_bang: int = 12000
    max_tokens_ky: int = 24000
    max_tokens_toi_da: int = 32000
    claude_effort_ky: str = "medium"
    claude_effort_toi_da: str = "high"
    cache_giai_ngay: int = 7           # CACHE_GIAI_NGAY: cùng đề + cùng mức → trả kết quả đã lưu trong N ngày (0 = tắt)
    # ----- Tra cứu tài liệu khoa học (literature.py – chỉ API hợp pháp, miễn phí)
    unpaywall_email: str = ""          # UNPAYWALL_EMAIL: email liên hệ cho Unpaywall/Crossref/OpenAlex (trống = dùng SMTP_USER)
    s2_api_key: str = field(default="", repr=False)  # S2_API_KEY: key Semantic Scholar (tuỳ chọn – không có thì hay bị giới hạn 429)
    tra_cuu_tu_dong: bool = True       # TRA_CUU_TU_DONG: công tắc "Tự động" bật tra cứu ở mức Kỹ / Tối đa (0 = chỉ khi người dùng bật / có DOI)
    # ----- ChatGPT (OpenAI) – AI thứ 3 trong chuỗi dự phòng (backend/ai/providers/openai). Thiếu key HOẶC model → tự bỏ qua
    openai_api_key: str = field(default="", repr=False)  # OPENAI_API_KEY: chỉ dùng ở server, không bao giờ gửi frontend / ghi log
    openai_model: str = ""             # OPENAI_MODEL: tên model (không viết cứng trong code) – xem backend/ai/providers/openai/README.md
    openai_temperature: float | None = None  # OPENAI_TEMPERATURE: trống = mặc định của model (model suy luận không nhận nhiệt độ)
    openai_max_tokens: int = 0         # OPENAI_MAX_TOKENS: trần token đầu ra cho ChatGPT (0 = theo từng yêu cầu như Claude / Gemini)
    # ----- Thời gian chờ AI (router chuyển sang AI kế tiếp khi quá thời gian)
    ai_timeout_giay: int = 30          # AI_TIMEOUT_GIAY: Claude / ChatGPT – quá N giây KHÔNG nhận được dữ liệu (kết nối / giữa hai đoạn streaming)
    ai_timeout_dai_giay: int = 300     # AI_TIMEOUT_DAI_GIAY: Gemini – tổng thời gian tối đa mỗi lần gọi (Gemini trả cả khối, không đo được "im lặng")
    # ----- AI Riêng (backend/ai_rieng): embedding để tìm kiến thức theo nghĩa (Gemini, key máy chủ); thiếu → chỉ tìm theo chữ (FTS5)
    embed_model: str = "gemini-embedding-2"  # EMBED_MODEL
    embed_dim: int = 768               # EMBED_DIM: số chiều vector (Gemini cho phép cắt ngắn 768 / 1536 / 3072)
    # CHE_DO_CUC_BO: 0 = tắt hẳn "chế độ cục bộ" (máy chạy app vào thẳng quyền chủ app) – BẮT BUỘC 0 khi chạy trên máy chủ
    che_do_cuc_bo: bool = True
    che_do_app: bool = False           # CHE_DO_APP: chạy trong ứng dụng desktop Electron
    # CAP_ADMIN_EMAIL: máy chủ không có Shell (Render Free) → tài khoản email + mật khẩu ĐÃ XÁC THỰC email trùng địa chỉ này thành admin
    cap_admin_email: str = ""
    # NOTEBOOKLM_COOKIE_KEY: khoá AES-256-GCM mã hoá cookie Google NotebookLM (services/notebooklm/ma_hoa.py); KHÔNG in / gửi frontend / ghi log
    notebooklm_cookie_key: str = field(default="", repr=False)

    @property
    def dang_nhap_google(self) -> bool:
        """Đã cấu hình đăng nhập Google? Chưa thì app chạy "chế độ cục bộ" (chỉ cho máy này, xem auth.py)."""
        return bool(self.google_client_id and self.google_client_secret)

    @property
    def gui_email_san_sang(self) -> bool:
        return (bool(self.resend_api_key and self.email_from) or bool(self.brevo_api_key and (self.email_from or self.smtp_user))
                or bool(self.smtp_user and self.smtp_pass))

    @property
    def turnstile_bat(self) -> bool:
        return bool(self.turnstile_site_key and self.turnstile_secret_key)


def _bat(v: str | None, mac_dinh: str = "1") -> bool:
    return (v or mac_dinh).strip().lower() not in {"0", "false", "no", "off"}


def _so(v: str | None, mac_dinh: int) -> int:
    try:
        return max(0, int((v or "").strip()))
    except ValueError:
        return mac_dinh


def _thuc(v: str | None) -> float | None:
    try:
        return float(v) if v not in (None, "") else None
    except ValueError:
        return None


def load_settings() -> Settings:
    env = _read_env()
    effort = env.get("CLAUDE_EFFORT", "medium").lower()
    if effort not in {"low", "medium", "high", "xhigh", "max"}:
        effort = "medium"
    che_do_app = _bat(env.get("CHE_DO_APP"), "0")
    che_do_cuc_bo = True if che_do_app else _bat(env.get("CHE_DO_CUC_BO"))
    cookie_secure = False if che_do_app else _bat(env.get("COOKIE_SECURE"))
    return Settings(
        api_key=env.get("ANTHROPIC_API_KEY", ""),
        model=env.get("CLAUDE_MODEL") or DEFAULT_MODEL,
        effort=effort,
        fallback=_bat(env.get("CLAUDE_FALLBACK")),
        gemini_api_key=env.get("GEMINI_API_KEY") or env.get("GOOGLE_API_KEY", ""),
        gemini_model=env.get("GEMINI_MODEL_FLASH") or env.get("GEMINI_MODEL") or DEFAULT_GEMINI_MODEL,
        gemini_fallback=_bat(env.get("GEMINI_FALLBACK")),
        gemini_model_hsg=env.get("GEMINI_MODEL_PRO") or env.get("GEMINI_MODEL_HSG") or DEFAULT_GEMINI_MODEL_HSG,
        gemini_model_lite=env.get("GEMINI_MODEL_LITE") or DEFAULT_GEMINI_MODEL_LITE,
        claude_model_manh=env.get("CLAUDE_MODEL_MANH") or DEFAULT_CLAUDE_MODEL_MANH,
        google_client_id=env.get("GOOGLE_CLIENT_ID", ""),
        google_client_secret=env.get("GOOGLE_CLIENT_SECRET", ""),
        app_secret_key=env.get("APP_SECRET_KEY", ""),
        free_trial_limit=_so(env.get("FREE_TRIAL_LIMIT"), 3),
        cookie_secure=cookie_secure,
        oauth_redirect_uri=env.get("OAUTH_REDIRECT_URI", ""),
        admin_emails=frozenset(e.strip().lower() for e in env.get("ADMIN_EMAILS", "").split(",") if e.strip()),
        smtp_host=env.get("SMTP_HOST") or "smtp.gmail.com",
        smtp_port=_so(env.get("SMTP_PORT"), 587) or 587,
        smtp_user=env.get("SMTP_USER", ""),
        smtp_pass=(env.get("SMTP_PASS", "").replace(" ", "")  # App Password của Google hiện dạng "abcd efgh ijkl mnop"
                   if "gmail" in (env.get("SMTP_HOST") or "smtp.gmail.com") else env.get("SMTP_PASS", "")),
        email_from=env.get("EMAIL_FROM") or env.get("SMTP_USER", ""),
        resend_api_key=env.get("RESEND_API_KEY", ""),
        brevo_api_key=env.get("BREVO_API_KEY", ""),
        turnstile_site_key=env.get("TURNSTILE_SITE_KEY", ""),
        turnstile_secret_key=env.get("TURNSTILE_SECRET_KEY", ""),
        trusted_proxies=env.get("TRUSTED_PROXIES") or "127.0.0.1,::1,cloudflare",
        geoip_city_db=env.get("GEOIP_CITY_DB", ""),
        geoip_asn_db=env.get("GEOIP_ASN_DB", ""),
        claude_access_key=env.get("CLAUDE_ACCESS_KEY", ""),
        claude_daily_limit=_so(env.get("CLAUDE_DAILY_LIMIT"), 10),
        anthropic_admin_key=env.get("ANTHROPIC_ADMIN_KEY", ""),
        user_key_secret=env.get("USER_KEY_SECRET", ""),
        thinking_nhanh=_so(env.get("THINKING_NHANH"), 0),
        thinking_can_bang=_so(env.get("THINKING_CAN_BANG"), 1024),
        thinking_ky=_so(env.get("THINKING_KY"), 4096),
        thinking_toi_da=_so(env.get("THINKING_TOI_DA"), 16384),
        max_tokens_nhanh=_so(env.get("MAX_TOKENS_NHANH"), 8000) or 8000,
        max_tokens_can_bang=_so(env.get("MAX_TOKENS_CAN_BANG"), 12000) or 12000,
        max_tokens_ky=_so(env.get("MAX_TOKENS_KY"), 24000) or 24000,
        max_tokens_toi_da=_so(env.get("MAX_TOKENS_TOI_DA"), 32000) or 32000,
        claude_effort_ky=(env.get("CLAUDE_EFFORT_KY") or "medium").lower(),
        claude_effort_toi_da=(env.get("CLAUDE_EFFORT_TOI_DA") or "high").lower(),
        cache_giai_ngay=_so(env.get("CACHE_GIAI_NGAY"), 7),
        unpaywall_email=env.get("UNPAYWALL_EMAIL", ""),
        s2_api_key=env.get("S2_API_KEY", ""),
        tra_cuu_tu_dong=_bat(env.get("TRA_CUU_TU_DONG")),
        openai_api_key=env.get("OPENAI_API_KEY", ""),
        openai_model=env.get("OPENAI_MODEL", ""),
        openai_temperature=_thuc(env.get("OPENAI_TEMPERATURE")),
        openai_max_tokens=_so(env.get("OPENAI_MAX_TOKENS"), 0),
        ai_timeout_giay=_so(env.get("AI_TIMEOUT_GIAY"), 30) or 30,
        ai_timeout_dai_giay=_so(env.get("AI_TIMEOUT_DAI_GIAY"), 300) or 300,
        embed_model=env.get("EMBED_MODEL") or "gemini-embedding-2",
        embed_dim=_so(env.get("EMBED_DIM"), 768) or 768,
        che_do_cuc_bo=che_do_cuc_bo,
        che_do_app=che_do_app,
        cap_admin_email=env.get("CAP_ADMIN_EMAIL", "").strip().lower(),
        notebooklm_cookie_key=env.get("NOTEBOOKLM_COOKIE_KEY", ""),
    )


settings = load_settings()


_MAU_KEY = re.compile(r"AIza[0-9A-Za-z_\-]{30,}|sk-ant-[0-9A-Za-z_\-]{16,}|sk-(?:proj-|svcacct-)?[0-9A-Za-z_\-]{20,}|GOCSPX-[0-9A-Za-z_\-]{16,}")


class AnKeyTrongLog(logging.Filter):
    """Phòng hờ: thay mọi chuỗi trông giống API key / client secret trong log bằng "***" (không bao giờ ghi key vào log)."""

    def filter(self, record: logging.LogRecord) -> bool:
        msg = record.getMessage()
        if _MAU_KEY.search(msg):
            record.msg, record.args = _MAU_KEY.sub("***", msg), None
        return True


def setup_logging() -> None:
    LOGS_DIR.mkdir(parents=True, exist_ok=True)
    root = logging.getLogger()
    if any(isinstance(h, RotatingFileHandler) for h in root.handlers):
        return
    fmt = logging.Formatter("%(asctime)s %(levelname)s [%(name)s] %(message)s")
    fh = RotatingFileHandler(LOGS_DIR / "app.log", maxBytes=2_000_000, backupCount=3, encoding="utf-8")
    fh.setFormatter(fmt)
    sh = logging.StreamHandler()
    sh.setFormatter(fmt)
    for h in (fh, sh):
        h.addFilter(AnKeyTrongLog())
    root.addHandler(fh)
    root.addHandler(sh)
    root.setLevel(logging.INFO)
    # Thư viện HTTP ghi log rất nhiều ở mức INFO
    for noisy in ("httpx", "httpx2", "httpcore", "anthropic"):
        logging.getLogger(noisy).setLevel(logging.WARNING)
