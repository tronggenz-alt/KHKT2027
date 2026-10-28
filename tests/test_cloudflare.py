"""Chạy sau Cloudflare (Tunnel/proxy): header chống cache, JS/CSS có phiên bản, nút đăng nhập khi chưa bật Google."""
import hashlib
import re

from backend import pipeline
from backend.config import FRONTEND_DIR, settings

QUA_CLOUDFLARE = {"X-Forwarded-For": "203.0.113.9", "CF-Connecting-IP": "203.0.113.9", "X-Forwarded-Proto": "https"}


def test_html_va_static_luon_hoi_lai_server(client, dang_nhap):
    dang_nhap(client)
    for url in ("/", "/cai-dat", "/dang-nhap?tiep=/x", "/static/app.js", "/static/tai-khoan.js", "/static/style.css"):
        r = client.get(url, follow_redirects=False)
        assert r.headers["cache-control"] == "no-cache", url


def test_api_khong_duoc_cache_o_cloudflare(client, dang_nhap, monkeypatch):
    """Ảnh/ZIP của từng người (đuôi .png/.zip Cloudflare mặc định cache) phải là private, no-store."""
    h = {"X-CSRF-Token": dang_nhap(client)}
    monkeypatch.setattr(pipeline, "chay", lambda job, *a, **k: setattr(job, "xong", True))
    job_id = client.post("/api/giai", data={"chuoi": "CH4 → C2H2", "demo": "true"}, headers=h).json()["job_id"]
    (pipeline.JOBS[job_id].thu_muc / "dap_an.png").write_bytes(b"png")
    for url in ("/api/toi", "/api/lich-su", f"/api/tep/{job_id}/dap_an.png", "/api/suc-khoe"):
        assert client.get(url).headers["cache-control"] == "private, no-store", url


def test_js_css_gan_phien_ban_theo_noi_dung(client, dang_nhap):
    dang_nhap(client)
    html = client.get("/").text
    for ten in ("app.js", "tai-khoan.js", "style.css"):
        v = hashlib.sha256((FRONTEND_DIR / ten).read_bytes()).hexdigest()[:10]
        assert f'"/static/{ten}?v={v}"' in html, ten
    assert not re.search(r'"/static/[\w.-]+\.(?:js|css)"', html)  # không còn URL không có phiên bản


def test_qua_cloudflare_chua_bat_google_van_dang_nhap_duoc(client, monkeypatch):
    monkeypatch.setattr(settings, "google_client_id", "")
    monkeypatch.setattr(settings, "google_client_secret", "")
    toi = client.get("/api/toi", headers=QUA_CLOUDFLARE).json()
    assert not toi["dang_nhap"] and not toi["dang_nhap_google"] and toi["qua_proxy"] and not toi["che_do_cuc_bo"]
    assert toi["oauth_redirect_uri"].endswith("/auth/callback")
    r = client.get("/", headers=QUA_CLOUDFLARE, follow_redirects=False)
    assert r.status_code == 303 and r.headers["location"] == "/dang-nhap?tiep=%2F"  # mở web là gặp trang Đăng nhập
    r = client.get("/auth/dang-nhap", headers=QUA_CLOUDFLARE, follow_redirects=False)
    assert r.status_code == 303 and r.headers["location"].startswith("/dang-nhap")  # Google chưa bật → về trang Đăng nhập email


def test_oauth_redirect_uri_theo_cau_hinh(client, monkeypatch):
    monkeypatch.setattr(settings, "oauth_redirect_uri", "https://hoa.example.com/auth/callback")
    assert client.get("/api/toi").json()["oauth_redirect_uri"] == "https://hoa.example.com/auth/callback"


def test_thanh_tren_khong_bao_gio_trong():
    """tai-khoan.js: chưa đăng nhập / lỗi API đều vẽ nút Đăng nhập → /dang-nhap (không còn innerHTML = "")."""
    js = (FRONTEND_DIR / "tai-khoan.js").read_text(encoding="utf-8")
    assert 'khu.innerHTML = ""' not in js
    assert '"/dang-nhap?tiep="' in js and "khu.innerHTML = nutDangNhap()" in js
