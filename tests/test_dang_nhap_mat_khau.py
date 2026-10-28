"""Đăng nhập khi mở app: trang /dang-nhap, tài khoản email + mật khẩu (không cần cấu hình Google), bảo mật kèm theo."""
import sqlite3
from types import SimpleNamespace

import pytest

from backend import auth, bao_mat, db, tai_khoan
from backend.config import settings

MK = "mat-khau-an-toan-123"


def _csrf(c):
    return {"X-CSRF-Token": c.get("/api/toi").json()["csrf"]}


_HOP = []  # hộp thư giả của test đang chạy (fixture khong_google)


def _dang_ky(c, email="ban@example.com", mk=MK, ten="Bạn", tiep="/"):
    """Đăng ký + nhập mã xác thực email (từ hộp thư giả) → trả phản hồi cuối (đăng ký lỗi thì trả lỗi đó)."""
    r = c.post("/auth/dang-ky", json={"email": email, "mat_khau": mk, "ten": ten, "tiep": tiep}, headers=_csrf(c))
    if r.status_code != 200 or not r.json().get("can_xac_thuc"):
        return r
    return c.post("/auth/xac-thuc-email", json={"ma": _HOP[-1].ma(email)}, headers=_csrf(c))


def _dang_nhap(c, email="ban@example.com", mk=MK, tiep="/"):
    return c.post("/auth/dang-nhap-mat-khau", json={"email": email, "mat_khau": mk, "tiep": tiep}, headers=_csrf(c))


@pytest.fixture
def khong_google(monkeypatch, cau_hinh, hop_thu):
    """Như máy của người dùng: .env chưa có GOOGLE_CLIENT_ID/SECRET (đã cấu hình gửi email – hộp thư giả)."""
    monkeypatch.setattr(settings, "google_client_id", "")
    monkeypatch.setattr(settings, "google_client_secret", "")
    _HOP[:] = [hop_thu]


# ================================================================ mở app → trang đăng nhập
def test_mo_app_chua_dang_nhap_thi_vao_trang_dang_nhap(client, khong_google):
    for url, tiep in (("/", "%2F"), ("/?bai=20261010-120000-abcdef", "%2F%3Fbai%3D20261010-120000-abcdef"), ("/cai-dat", "%2Fcai-dat")):
        r = client.get(url, follow_redirects=False)
        assert r.status_code == 303 and r.headers["location"] == f"/dang-nhap?tiep={tiep}", url
    r = client.get("/dang-nhap")
    assert r.status_code == 200 and 'id="form-dang-nhap"' in r.text and "/static/dang-nhap.js?v=" in r.text


def test_dang_ky_xong_vao_thang_app(client, khong_google):
    r = _dang_ky(client, tiep="/?bai=x")
    assert r.status_code == 200 and r.json() == {"ok": True, "tiep": "/?bai=x"}
    toi = client.get("/api/toi").json()
    assert toi["dang_nhap"] and toi["nguoi_dung"]["dang_nhap_bang"] == "email" and toi["nguoi_dung"]["ten"] == "Bạn"
    assert toi["nguon_key_giai"] == "dung_thu" and toi["dung_thu"]["con_lai"] == 3
    assert client.get("/", follow_redirects=False).status_code == 200
    r = client.get("/dang-nhap?tiep=/cai-dat", follow_redirects=False)  # đã đăng nhập → không hiện lại trang đăng nhập
    assert r.status_code == 303 and r.headers["location"] == "/cai-dat"


def test_dang_xuat_roi_dang_nhap_lai(client, khong_google):
    _dang_ky(client)
    client.post("/auth/dang-xuat", headers={**_csrf(client), "Accept": "application/json"})
    assert not client.get("/api/toi").json()["dang_nhap"]
    assert _dang_nhap(client, email="  BAN@Example.com ").status_code == 200  # email không phân biệt hoa thường
    assert client.get("/api/toi").json()["nguoi_dung"]["email"] == "ban@example.com"


@pytest.mark.parametrize("email, mk, ma", [("khong-phai-email", MK, "email_sai"), ("a@b.co", "ngan", "mat_khau_ngan"),
                                            ("abcdefgh@b.co", "ABCDEFGH@b.co", "mat_khau_yeu")])
def test_dang_ky_du_lieu_sai(client, khong_google, email, mk, ma):
    r = _dang_ky(client, email=email, mk=mk)
    assert r.status_code == 400 and r.json()["ma"] == ma and not client.get("/api/toi").json()["dang_nhap"]


def test_email_da_co_tai_khoan(client, khong_google):
    _dang_ky(client)
    r = _dang_ky(client, email="Ban@Example.com", mk="mat-khau-khac-456")
    assert r.status_code == 409 and r.json()["ma"] == "email_da_co"
    client.post("/auth/dang-xuat", headers={**_csrf(client), "Accept": "application/json"})
    assert _dang_nhap(client, mk="mat-khau-khac-456").status_code == 401  # mật khẩu cũ vẫn giữ nguyên


def test_sai_mat_khau_va_email_khong_ton_tai_bao_giong_nhau(client, khong_google):
    _dang_ky(client)
    client.post("/auth/dang-xuat", headers={**_csrf(client), "Accept": "application/json"})
    a, b = _dang_nhap(client, mk="sai-mat-khau"), _dang_nhap(client, email="khong-co@example.com")
    assert a.status_code == b.status_code == 401 and a.json() == b.json()


def test_mat_khau_chi_luu_ban_bam(client, khong_google, csdl_tam):
    _dang_ky(client)
    with sqlite3.connect(csdl_tam) as con:
        bam, = con.execute("SELECT password_hash FROM users WHERE email = 'ban@example.com'").fetchone()
    assert bam.startswith("scrypt$") and MK not in bam
    assert bao_mat.kiem_mat_khau(MK, bam) and not bao_mat.kiem_mat_khau(MK + "x", bam)


def test_can_csrf(client, khong_google):
    assert client.post("/auth/dang-ky", json={"email": "a@b.co", "mat_khau": MK}).status_code == 403
    assert client.post("/auth/dang-nhap-mat-khau", json={"email": "a@b.co", "mat_khau": MK}).status_code == 403


def test_chong_do_mat_khau_theo_email(client, khong_google):
    _dang_ky(client)
    client.post("/auth/dang-xuat", headers={**_csrf(client), "Accept": "application/json"})
    ma = [_dang_nhap(client, mk=f"sai-{i}-xxxx").status_code for i in range(9)]
    assert ma[:8] == [401] * 8 and ma[8] == 429
    bao_mat.gioi_han_tan_suat.quy_tac = {**bao_mat.QUY_TAC_TAN_SUAT, "dang_nhap_mk": []}  # bỏ giới hạn theo IP để chắc chắn bị chặn theo email
    try:
        assert _dang_nhap(client).status_code == 429  # mật khẩu đúng cũng phải đợi
    finally:
        bao_mat.gioi_han_tan_suat.quy_tac = bao_mat.QUY_TAC_TAN_SUAT


def test_chong_tao_tai_khoan_hang_loat(client, khong_google):
    r = [_dang_ky(client, email=f"u{i}@example.com") for i in range(4)]
    assert [x.status_code for x in r] == [200, 200, 200, 429] and r[3].json()["ma"] == "dang_ky_qua_nhieu"


def test_tiep_chi_cho_duong_dan_noi_bo(client, khong_google):
    assert _dang_ky(client, tiep="https://evil.com").json()["tiep"] == "/"


def test_admin_emails_khong_ap_dung_cho_tai_khoan_email(client, khong_google, monkeypatch):
    """Tài khoản email không xác minh email → đăng ký bằng email của chủ app cũng KHÔNG thành chủ app."""
    monkeypatch.setattr(settings, "admin_emails", frozenset({"chu@example.com"}))
    _dang_ky(client, email="chu@example.com")
    toi = client.get("/api/toi").json()
    assert not toi["la_chu_app"] and toi["nguon_key_giai"] == "dung_thu"
    google = db.luu_nguoi_dung("google-chu", "chu@example.com", "Chủ")
    assert tai_khoan.la_chu_app(google) and not tai_khoan.la_chu_app(db.lay_theo_sub("email:chu@example.com"))


def test_google_khong_chiem_duoc_tai_khoan_email(client, cau_hinh, hop_thu, monkeypatch):
    async def lay_token(_request):
        return {"userinfo": {"sub": "email:ban@example.com", "email": "ban@example.com", "email_verified": True}}

    _HOP[:] = [hop_thu]
    _dang_ky(client)
    client.post("/auth/dang-xuat", headers={**_csrf(client), "Accept": "application/json"})
    monkeypatch.setattr(auth, "oauth", SimpleNamespace(google=SimpleNamespace(authorize_access_token=lay_token)))
    r = client.get("/auth/callback", follow_redirects=False)
    assert r.headers["location"] == "/dang-nhap?loi=google" and not client.get("/api/toi").json()["dang_nhap"]


def test_tai_khoan_email_dung_duoc_byok_va_dung_thu(client, khong_google, monkeypatch):
    _dang_ky(client)
    h = _csrf(client)
    monkeypatch.setattr(tai_khoan, "kiem_tra_key_gemini", lambda k: {"hop_le": True, "thong_diep": "ok"})
    r = client.post("/api/key", json={"key": "AIzaSyD-khoa-gia-0123456789-abcdefghij"}, headers=h)
    assert r.status_code == 200 and client.get("/api/toi").json()["nguon_key_giai"] == "ca_nhan"


def test_csdl_cu_duoc_nang_cap_cot_mat_khau(tmp_path, monkeypatch):
    duong_dan = tmp_path / "cu.db"
    with sqlite3.connect(duong_dan) as con:  # bảng users kiểu cũ (chưa có password_hash)
        con.execute("CREATE TABLE users (id INTEGER PRIMARY KEY AUTOINCREMENT, google_sub TEXT NOT NULL UNIQUE, "
                    "email TEXT NOT NULL DEFAULT '', name TEXT NOT NULL DEFAULT '', picture TEXT NOT NULL DEFAULT '', "
                    "gemini_key_enc TEXT, gemini_key_last4 TEXT, trial_used INTEGER NOT NULL DEFAULT 0, "
                    "created_at TEXT NOT NULL, last_login_at TEXT NOT NULL)")
        con.execute("INSERT INTO users (google_sub, email, created_at, last_login_at) VALUES ('g-1', 'a@x.com', 't', 't')")
    monkeypatch.setattr(db, "DUONG_DAN", duong_dan)
    assert db.lay_nguoi_dung(1).email == "a@x.com"
    assert db.tao_tai_khoan_email("b@x.com", "B", "scrypt$...") is not None
    assert not db.lay_nguoi_dung(1).la_admin  # cột is_admin được thêm với mặc định 0


# ================================================================ admin (quan_tri) + đổi mật khẩu
def test_tao_admin_bang_cong_cu_quan_tri(client, khong_google, capsys):
    from backend import quan_tri

    assert quan_tri.chay(["tao-admin", "Admin@Example.com", "--ten", "Quản trị", "--mat-khau-ngau-nhien"]) == 0
    ra = capsys.readouterr().out
    mk = ra.split("Mật khẩu: ")[1].split()[0]
    u = db.lay_theo_sub("email:admin@example.com")
    assert u.la_admin and tai_khoan.la_chu_app(u) and u.ten == "Quản trị" and mk not in (u.mat_khau_bam or "")
    assert _dang_nhap(client, email="admin@example.com", mk=mk).status_code == 200
    toi = client.get("/api/toi").json()
    assert toi["la_chu_app"] and toi["nguon_key_giai"] == "chu_app"  # admin: key của chủ app, không giới hạn
    assert quan_tri.chay(["bo-admin", "admin@example.com"]) == 0 and not db.lay_theo_sub("email:admin@example.com").la_admin
    assert quan_tri.chay(["cap-admin", "khong-co@example.com"]) == 1


def test_tai_khoan_ki_cuu(client, khong_google, capsys, monkeypatch):
    """Kì cựu: nhãn "kì cựu", miễn xác thực email, giải không giới hạn bằng key chủ app, đổi tên không giới hạn, KHÔNG có quyền quản trị."""
    from backend import nhat_ky, quan_tri

    monkeypatch.setattr(settings, "free_trial_limit", 1)
    assert quan_tri.chay(["tao-ki-cuu", "kicuu@chemistry.local", "--ten", "Kì Cựu", "--mat-khau-ngau-nhien"]) == 0
    mk = capsys.readouterr().out.split("Mật khẩu: ")[1].split()[0]
    u = db.lay_theo_sub("email:kicuu@chemistry.local")
    assert u.la_ki_cuu and not u.la_admin and not tai_khoan.la_chu_app(u) and not u.da_xac_thuc_email and not u.can_xac_thuc
    assert _dang_nhap(client, email="kicuu@chemistry.local", mk=mk).status_code == 200  # không phải nhập mã email
    toi = client.get("/api/toi").json()
    assert toi["la_ki_cuu"] and not toi["la_chu_app"] and toi["nguon_key_giai"] == "ki_cuu"
    for _ in range(3):  # quá FREE_TRIAL_LIMIT vẫn giải bằng key chủ app, không trừ lượt
        assert tai_khoan.chon_khoa_giai(db.lay_nguoi_dung(u.id)).nguon == "chu_app"
    assert db.lay_nguoi_dung(u.id).luot_da_dung == 0
    h = _csrf(client)
    for i in range(5):  # thành viên thường chỉ 3 lần/ngày
        r = client.post("/api/doi-ten", json={"ten": f"Kì Cựu Số {i}"}, headers=h)
        assert r.status_code == 200, r.json()
    assert client.get("/admin/api/tong-quan").status_code == 403 and client.get("/admin").status_code == 403
    assert next(d for d in nhat_ky.nguoi_dung_bang() if d["id"] == u.id)["vai_tro"] == "kì cựu"
    assert "kì cựu" in quan_tri.danh_sach()
    assert quan_tri.chay(["bo-ki-cuu", "kicuu@chemistry.local"]) == 0 and db.lay_nguoi_dung(u.id).can_xac_thuc


def test_dat_lai_mat_khau_khi_quen(client, khong_google, capsys):
    from backend import quan_tri

    _dang_ky(client)
    assert quan_tri.chay(["dat-mat-khau", "ban@example.com", "--mat-khau-ngau-nhien"]) == 0
    mk = capsys.readouterr().out.split("Mật khẩu: ")[1].split()[0]
    client.post("/auth/dang-xuat", headers={**_csrf(client), "Accept": "application/json"})
    assert _dang_nhap(client).status_code == 401 and _dang_nhap(client, mk=mk).status_code == 200


def test_doi_mat_khau(client, khong_google):
    _dang_ky(client)
    h = _csrf(client)
    r = client.post("/auth/doi-mat-khau", json={"mat_khau_cu": "sai-sai-sai", "mat_khau_moi": "moi-moi-moi-1"}, headers=h)
    assert r.status_code == 401
    r = client.post("/auth/doi-mat-khau", json={"mat_khau_cu": MK, "mat_khau_moi": "ngan"}, headers=h)
    assert r.status_code == 400
    assert client.post("/auth/doi-mat-khau", json={"mat_khau_cu": MK, "mat_khau_moi": "moi-moi-moi-1"}, headers=h).json() == {"ok": True}
    client.post("/auth/dang-xuat", headers={**_csrf(client), "Accept": "application/json"})
    assert _dang_nhap(client).status_code == 401 and _dang_nhap(client, mk="moi-moi-moi-1").status_code == 200
