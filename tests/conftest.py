"""Fixture dùng chung: CSDL tạm, cấu hình đăng nhập giả, client HTTPS (cookie phiên có cờ Secure) + đăng nhập Google giả,
hộp thư giả (mã xác thực email không gửi SMTP thật)."""
import re
from types import SimpleNamespace

import pytest
from fastapi.testclient import TestClient

from backend import auth, bao_mat, db, email_rac, literature, so_do_he_thong, xac_thuc_email
from backend.bao_mat import gioi_han_tan_suat
from backend.config import settings


@pytest.fixture(autouse=True)
def _khong_dung_csdl_that(tmp_path_factory, monkeypatch):
    """Lưới an toàn cho MỌI test: CSDL + khoá chính luôn là bản tạm (test không fixture csdl_tam cũng không ghi data/app.db thật,
    vd nhật ký chi phí ai_usage, cache giải bài). Cache giải bài mặc định tắt (test cache tự bật)."""
    tam = tmp_path_factory.mktemp("csdl")
    monkeypatch.setattr(db, "DUONG_DAN", tam / "app.db")
    monkeypatch.setattr(bao_mat, "TEP_KHOA_CHINH", tam / "app_secret.key")
    monkeypatch.setattr(bao_mat, "_khoa_tu_tep", {})
    monkeypatch.setattr(settings, "cache_giai_ngay", 0)
    # tra cứu tài liệu: test không tự bật theo mức + KHÔNG BAO GIỜ gọi mạng thật (test tra cứu tự giả lập requests.get)
    monkeypatch.setattr(settings, "tra_cuu_tu_dong", False)
    monkeypatch.setattr(literature.requests, "get", lambda *a, **k: (_ for _ in ()).throw(literature.requests.ConnectionError("test: không gọi mạng")))
    # email rác: KHÔNG tra DNS thật (mọi tên miền coi như có MX – test MX tự thay _tra_mx_dns), không tải danh sách từ GitHub,
    # bản tải về nằm ở thư mục tạm (không đụng data/email_tam_thoi.txt thật)
    monkeypatch.setattr(email_rac, "_tra_mx_dns", lambda mien: "co")
    monkeypatch.setattr(email_rac, "_cache_mx", {})
    monkeypatch.setattr(email_rac, "TEP_CAP_NHAT", tam / "email_tam_thoi.txt")
    # ChatGPT: test KHÔNG BAO GIỜ gọi OpenAI thật dù .env đã có key (test router tự đặt key giả + client giả)
    monkeypatch.setattr(settings, "openai_api_key", "")
    monkeypatch.setattr(settings, "openai_model", "")
    # sơ đồ hệ thống: health-check KHÔNG gọi Anthropic / Google thật (test tự thay hàm ping khi cần), không dùng cache của test trước
    monkeypatch.setattr(so_do_he_thong, "_ping_claude", lambda model: None)
    monkeypatch.setattr(so_do_he_thong, "_ping_gemini", lambda model: None)
    monkeypatch.setattr(so_do_he_thong, "_ping_openai", lambda model: None)
    monkeypatch.setattr(so_do_he_thong, "_cache", {"luc": 0.0, "kq": None})
    # AI Riêng: không gọi API embedding thật, không chạy việc nền (test gọi xu_ly.xu_ly_het() trực tiếp)
    from backend.ai_rieng import nhung as ar_nhung, xu_ly as ar_xu_ly
    from backend.llm import LoiAI

    monkeypatch.setattr(ar_nhung, "nhung", lambda ds, truy_van=False: (_ for _ in ()).throw(LoiAI("test: không gọi embedding", "khac")))
    monkeypatch.setattr(ar_xu_ly, "chay_nen", lambda: None)
    from backend.ai_rieng import kho as ar_kho, song_song as ar_song_song

    monkeypatch.setitem(ar_kho.MAC_DINH, "song_song_gemini", False)  # test bật riêng khi cần (và chờ làn nền xong)
    monkeypatch.setattr(email_rac.requests, "get", lambda *a, **k: (_ for _ in ()).throw(email_rac.requests.ConnectionError("test: không gọi mạng")))
    yield
    assert ar_song_song.cho_nen_xong(10), "còn làn Gemini đối chiếu chạy nền sau test"



@pytest.fixture
def csdl_tam(tmp_path, monkeypatch):
    monkeypatch.setattr(db, "DUONG_DAN", tmp_path / "app.db")
    monkeypatch.setattr(bao_mat, "TEP_KHOA_CHINH", tmp_path / "app_secret.key")  # không đụng data/app_secret.key thật
    monkeypatch.setattr(bao_mat, "_khoa_tu_tep", {})
    gioi_han_tan_suat.xoa()
    yield tmp_path / "app.db"
    gioi_han_tan_suat.xoa()


@pytest.fixture
def cau_hinh(monkeypatch, csdl_tam):
    """Đã cấu hình đăng nhập Google + khoá chính; chủ app có key; dùng thử 3 bài."""
    monkeypatch.setattr(settings, "google_client_id", "id-test.apps.googleusercontent.com")
    monkeypatch.setattr(settings, "google_client_secret", "secret-test")
    monkeypatch.setattr(settings, "app_secret_key", "k" * 48)
    monkeypatch.setattr(settings, "free_trial_limit", 3)
    monkeypatch.setattr(settings, "admin_emails", frozenset())
    monkeypatch.setattr(settings, "api_key", "sk-ant-test")
    monkeypatch.setattr(settings, "gemini_api_key", "gm-chu-app")
    monkeypatch.setattr(settings, "gemini_fallback", True)
    monkeypatch.setattr(settings, "claude_access_key", "")  # test không bao giờ dùng Key Claude thật trong .env
    monkeypatch.setattr(settings, "claude_daily_limit", 10)
    monkeypatch.setattr(settings, "cache_giai_ngay", 0)  # test giải lại cùng đề nhiều lần; test cache tự bật lại
    monkeypatch.setattr(settings, "anthropic_admin_key", "")
    for ten in ("smtp_user", "smtp_pass", "email_from", "resend_api_key", "brevo_api_key", "turnstile_site_key", "turnstile_secret_key"):
        monkeypatch.setattr(settings, ten, "")  # test KHÔNG BAO GIỜ gửi email/gọi Turnstile thật dù .env đã cấu hình
    return settings


class HopThu(list):
    """Thư "đã gửi" (không qua SMTP). ma(email) → mã 6 số trong thư mới nhất gửi tới email đó."""

    def ma(self, email: str) -> str:
        for t in reversed(self):
            if t["den"].lower() == email.lower():
                return re.search(r"\b(\d{6})\b", t["van_ban"]).group(1)
        raise AssertionError(f"không có thư nào gửi tới {email}")


@pytest.fixture
def hop_thu(monkeypatch, cau_hinh):
    """Bật gửi email (giả): app coi như đã cấu hình SMTP; thư được giữ lại trong danh sách để test đọc mã."""
    hop = HopThu()
    monkeypatch.setattr(settings, "smtp_user", "app@example.com")
    monkeypatch.setattr(settings, "smtp_pass", "mat-khau-ung-dung")
    monkeypatch.setattr(xac_thuc_email, "gui_thu",
                        lambda den, tieu_de, van_ban, html: hop.append({"den": den, "tieu_de": tieu_de, "van_ban": van_ban, "html": html}))
    return hop


IP_KHACH = "203.0.113.50"  # IP (dải dành cho tài liệu) của "trình duyệt" trong test; mặc định TestClient là "testclient" – không phải IP


@pytest.fixture
def client(cau_hinh):
    from backend.main import app

    return TestClient(app, base_url="https://testserver", client=(IP_KHACH, 50000))  # truy cập thẳng từ một IP công cộng


@pytest.fixture
def dang_nhap(monkeypatch):
    """dang_nhap(client, sub, email) → đăng nhập qua /auth/callback với Google giả; trả mã CSRF."""

    def _dang_nhap(c: TestClient, sub: str = "google-1", email: str = "a@example.com", xac_minh: bool = True) -> str:
        async def lay_token(_request):
            return {"userinfo": {"sub": sub, "email": email, "email_verified": xac_minh, "name": "Người " + sub, "picture": ""}}

        monkeypatch.setattr(auth, "oauth", SimpleNamespace(google=SimpleNamespace(authorize_access_token=lay_token)))
        r = c.get("/auth/callback", follow_redirects=False)
        assert r.status_code == 303
        return c.get("/api/toi").json()["csrf"]

    return _dang_nhap
