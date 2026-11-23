"""Kiểm thử khoá truy cập web 4 số (backend/khoa_web.py):
- Mặc định tắt ở môi trường cục bộ; khi bật, IP mới phải nhập đúng mã 4 số mới vào được web.
- IP đăng nhập gốc của tài khoản được chỉ định trong CSDL (KHOA_WEB_MIEN_USER_ID) được vào thẳng không cần mã.
- /api/suc-khoe luôn mở cho Render health check; chống giả mạo X-Forwarded-For; giới hạn tốc độ chống dò mã.
"""
import hashlib
import time

from fastapi.testclient import TestClient

from backend import db, khoa_web, nhat_ky
from backend.bao_mat import bam_mat_khau, gioi_han_tan_suat
from backend.config import settings
from backend.main import app


def _bat_khoa(monkeypatch, *, uid_mien: int = 0, mien_ip: str = "", ma: str = "", ma_bam: str = ""):
    khoa_web.xoa_bo_nho_ip()
    gioi_han_tan_suat.xoa()
    monkeypatch.setattr(settings, "khoa_web_bat", True)
    monkeypatch.setattr(settings, "khoa_web_mien_user_id", uid_mien)
    monkeypatch.setattr(settings, "khoa_web_mien_ip", mien_ip)
    monkeypatch.setattr(settings, "khoa_web_ma", ma)
    monkeypatch.setattr(settings, "khoa_web_ma_bam", ma_bam)
    monkeypatch.setattr(settings, "khoa_web_cau_hoi", "Ngày sinh của chủ app là gì")


def test_khoa_web_ip_moi_phai_nhap_ma_va_mo_duoc(monkeypatch, cau_hinh):
    bam = "kwv1-test:" + hashlib.sha256(b"kwv1-test:2468").hexdigest()
    _bat_khoa(monkeypatch, ma_bam=bam)
    c = TestClient(app, base_url="https://testserver", client=("198.51.100.20", 50000))

    # Health check luôn mở cho Render
    assert c.get("/api/suc-khoe").status_code == 200

    # IP mới vào trang chủ / đăng nhập / API / static đều bị khoá
    r_home = c.get("/", follow_redirects=False)
    assert r_home.status_code == 403
    assert "Ngày sinh của chủ app là gì" in r_home.text
    assert "2468" not in r_home.text
    assert c.get("/dang-nhap", follow_redirects=False).status_code == 403
    assert c.get("/api/toi").status_code == 403
    assert c.get("/static/dang-nhap.js").status_code == 403

    # Nhập sai mã → 401, không có cookie mở khoá
    r_sai = c.post("/khoa-web/mo", json={"ma": "0000"})
    assert r_sai.status_code == 401
    assert khoa_web.TEN_COOKIE not in r_sai.cookies
    assert c.get("/api/toi").status_code == 403

    # Nhập đúng mã 2468 → 200 + cấp cookie HttpOnly → vào web bình thường
    r_dung = c.post("/khoa-web/mo", json={"ma": "2468"})
    assert r_dung.status_code == 200 and r_dung.json()["ok"] is True
    assert khoa_web.TEN_COOKIE in c.cookies
    assert c.get("/dang-nhap", follow_redirects=False).status_code == 200
    assert c.get("/api/toi").status_code == 200
    assert c.get("/static/dang-nhap.js").status_code == 200


def test_khoa_web_mien_ip_tai_khoan_trong_csdl_va_chong_gia_mao(monkeypatch, cau_hinh):
    u = db.tao_tai_khoan_email("user@example.com", "Người Dùng Thử", bam_mat_khau("mat-khau-123"), ip="203.0.113.88", da_xac_thuc=True)
    nhat_ky.ghi("dang_nhap", u=u, phuong_thuc="email", ip="203.0.113.88", ua="Chrome")
    _bat_khoa(monkeypatch, uid_mien=u.id, ma="2468")

    # Đúng IP đăng nhập gốc của tài khoản → vào thẳng KHÔNG hiện khoá
    c_mien = TestClient(app, base_url="https://testserver", client=("203.0.113.88", 50000))
    assert c_mien.get("/dang-nhap", follow_redirects=False).status_code == 200
    assert c_mien.get("/api/toi").status_code == 200

    # IP khác kể cả tự chèn X-Forwarded-For / CF-Connecting-IP giả mạo vẫn bị khoá
    c_la = TestClient(app, base_url="https://testserver", client=("198.51.100.99", 50000))
    r_gia = c_la.get(
        "/dang-nhap",
        headers={"X-Forwarded-For": "203.0.113.88", "CF-Connecting-IP": "203.0.113.88"},
        follow_redirects=False,
    )
    assert r_gia.status_code == 403
    assert "Ngày sinh của chủ app là gì" in r_gia.text


def test_khoa_web_cookie_gia_het_han_va_gioi_han_tan_suat(monkeypatch, cau_hinh):
    _bat_khoa(monkeypatch, ma="2468")
    c = TestClient(app, base_url="https://testserver", client=("198.51.100.77", 50000))

    # Cookie giả hoặc hết hạn đều bị chặn
    c.cookies.set(khoa_web.TEN_COOKIE, "v1.9999999999.bad.deadbeef")
    assert c.get("/api/toi").status_code == 403
    assert not khoa_web.cookie_hop_le(khoa_web.tao_cookie(bay_gio=time.time() - khoa_web.TTL_GIAY - 5))

    # Nhập sai quá 10 lần trong 5 phút → bị chặn 429 chống dò mã 4 số
    c.cookies.clear()
    for _ in range(10):
        assert c.post("/khoa-web/mo", json={"ma": "9999"}).status_code == 401
    assert c.post("/khoa-web/mo", json={"ma": "2468"}).status_code == 429
