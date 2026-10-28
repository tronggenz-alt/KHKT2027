"""
tests/test_app_auth.py
Kiểm thử luồng cấp và đổi mã đăng nhập cho ứng dụng desktop CHEMISTRY (GĐ3 PROMPT_XUAT_APP_CHEMISTRY.md)
và cơ chế thay thế vendor CDN khi chạy offline.
"""
import time
import pytest
from starlette.testclient import TestClient

from backend import db
from backend.config import settings
from backend.main import _trang, app


@pytest.fixture
def client_app(client: TestClient):
    return client


def test_tao_ma_va_doi_ma_thanh_cong(client_app: TestClient):
    # 1. Tạo tài khoản mẫu
    user = db.luu_nguoi_dung("app_sub_123", "test_app_user@example.com", "App User")
    uid = user.id

    # 2. Tạo mã đăng nhập trực tiếp từ db
    code = db.tao_ma_dang_nhap_app(uid, ttl_giay=120)
    assert code.startswith("chem_")
    assert len(code) > 20

    # 3. Đổi mã thành công
    res = client_app.post("/api/app/doi-ma", json={"ma": code})
    assert res.status_code == 200
    data = res.json()
    assert data["ok"] is True
    assert data["user"]["id"] == uid
    assert data["user"]["email"] == "test_app_user@example.com"
    assert "csrf" in data


def test_doi_ma_dung_lai_that_bai(client_app: TestClient):
    user = db.luu_nguoi_dung("app_sub_456", "test_app_user2@example.com", "App User 2")
    uid = user.id
    code = db.tao_ma_dang_nhap_app(uid, ttl_giay=120)

    # Đổi lần 1: thành công
    res1 = client_app.post("/api/app/doi-ma", json={"ma": code})
    assert res1.status_code == 200

    # Đổi lần 2: phải thất bại (single use)
    res2 = client_app.post("/api/app/doi-ma", json={"ma": code})
    assert res2.status_code == 400
    assert "đã được sử dụng" in res2.text


def test_doi_ma_het_han_that_bai(client_app: TestClient):
    user = db.luu_nguoi_dung("app_sub_789", "test_app_user3@example.com", "App User 3")
    uid = user.id
    # Tạo mã với TTL âm (-1 giây)
    code = db.tao_ma_dang_nhap_app(uid, ttl_giay=-1)

    res = client_app.post("/api/app/doi-ma", json={"ma": code})
    assert res.status_code == 400
    assert "đã hết hạn" in res.text


def test_doi_ma_khong_ton_tai(client_app: TestClient):
    res = client_app.post("/api/app/doi-ma", json={"ma": "chem_ma_khong_ton_tai_1234567890"})
    assert res.status_code == 400
    assert "không tồn tại" in res.text


def test_api_tao_ma_can_dang_nhap(client_app: TestClient):
    # Chưa đăng nhập gọi /api/app/ma-dang-nhap -> 401 hoặc lỗi đăng nhập
    res = client_app.post("/api/app/ma-dang-nhap")
    assert res.status_code in {401, 403}


def test_thay_the_vendor_offline():
    # Khi che_do_app = False: giữ nguyên CDN
    settings.che_do_app = False
    html_web = _trang("cai-dat.html").body.decode("utf-8")
    assert "https://cdn.tailwindcss.com/3.4.16" in html_web

    # Khi che_do_app = True: thay bằng /static/vendor/...
    settings.che_do_app = True
    try:
        html_app = _trang("cai-dat.html").body.decode("utf-8")
        assert "https://cdn.tailwindcss.com/3.4.16" not in html_app
        assert "/static/vendor/tailwind.3.4.16.js" in html_app
    finally:
        settings.che_do_app = False
