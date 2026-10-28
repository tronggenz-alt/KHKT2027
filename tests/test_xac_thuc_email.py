"""Xác thực email (mã 6 số) khi đăng ký / tài khoản cũ / quên mật khẩu + chống tạo tài khoản hàng loạt + đổi tên + nhãn thành viên."""
import logging
import sqlite3
import time

import pytest

from backend import auth, db, nhat_ky, xac_thuc_email
from backend.bao_mat import bam_mat_khau
from backend.config import settings

MK = "mat-khau-an-toan-123"


def _h(c):
    return {"X-CSRF-Token": c.get("/api/toi").json()["csrf"]}


def _dk(c, email="ban@example.com", mk=MK, ten="Bạn", **them):
    return c.post("/auth/dang-ky", json={"email": email, "mat_khau": mk, "ten": ten, **them}, headers=_h(c))


def _dn(c, email="ban@example.com", mk=MK):
    return c.post("/auth/dang-nhap-mat-khau", json={"email": email, "mat_khau": mk}, headers=_h(c))


def _ma(c, ma):
    return c.post("/auth/xac-thuc-email", json={"ma": ma}, headers=_h(c))


def _xuat(c):
    c.post("/auth/dang-xuat", headers={**_h(c), "Accept": "application/json"})


def _lui_gio_gui(email, giay):
    """Giả lập thời gian trôi: các mã đã gửi cho email lùi về quá khứ `giay` giây."""
    with db.ket_noi() as con:
        con.execute("UPDATE email_codes SET created = created - ?, expires = expires - ? WHERE email_norm = ?",
                    (giay, giay, db.email_chuan(email)))


@pytest.fixture
def app(client, hop_thu, monkeypatch):
    monkeypatch.setattr(settings, "google_client_id", "")
    monkeypatch.setattr(settings, "google_client_secret", "")
    return client


# ================================================================ đăng ký → mã → kích hoạt
def test_dang_ky_gui_ma_xac_thuc_roi_moi_dang_nhap(app, hop_thu, csdl_tam, caplog):
    caplog.set_level(logging.DEBUG)
    r = _dk(app)
    d = r.json()
    assert r.status_code == 200 and d["can_xac_thuc"] and d["cho_gui_lai"] == 60 and "tiep" not in d
    assert len(hop_thu) == 1 and hop_thu[0]["den"] == "ban@example.com"
    ma = hop_thu.ma("ban@example.com")
    assert ma not in r.text  # mã chỉ có trong email
    with sqlite3.connect(csdl_tam) as con:
        dong = con.execute("SELECT code_hash FROM email_codes").fetchall()
        assert len(dong) == 1 and ma not in dong[0][0] and len(dong[0][0]) == 64  # chỉ lưu HMAC
        assert ma not in " ".join(str(x) for row in con.execute("SELECT * FROM activity_log") for x in row)
    assert ma not in caplog.text
    # chưa xác thực: chưa đăng nhập, không vào được app, không dùng thử được
    toi = app.get("/api/toi").json()
    assert not toi["dang_nhap"] and app.get("/", follow_redirects=False).status_code == 303
    assert app.post("/api/giai", data={"chuoi": "CH4 -> C2H2"}, headers=_h(app)).status_code == 401
    # đăng nhập bằng mật khẩu đúng khi chưa xác thực → được yêu cầu nhập mã (không gửi mã mới trong 60 giây)
    r = _dn(app)
    assert r.status_code == 200 and r.json()["can_xac_thuc"] and len(hop_thu) == 1
    # sai mã → còn 4 lần; đúng mã → đăng nhập
    r = _ma(app, "000000" if ma != "000000" else "111111")
    assert r.status_code == 400 and r.json()["ma"] == "ma_sai" and "còn 4" in r.json()["loi"]
    r = _ma(app, ma)
    assert r.status_code == 200 and r.json() == {"ok": True, "tiep": "/"}
    toi = app.get("/api/toi").json()
    assert toi["dang_nhap"] and toi["nguoi_dung"]["email"] == "ban@example.com" and toi["dung_thu"]["con_lai"] == 3
    assert _ma(app, ma).status_code == 400  # mã đã dùng
    _xuat(app)
    assert _dn(app).json() == {"ok": True, "tiep": "/"}  # lần sau đăng nhập thẳng


def test_sai_ma_5_lan_thi_khoa_ma(app, hop_thu):
    _dk(app)
    ma = hop_thu.ma("ban@example.com")
    sai = str((int(ma) + 1) % 10**6).zfill(6)
    kq = [_ma(app, sai).json()["ma"] for _ in range(5)]
    assert kq == ["ma_sai"] * 4 + ["ma_bi_khoa"]
    r = _ma(app, ma)  # mã đúng cũng không dùng được nữa
    assert r.status_code == 400 and r.json()["ma"] == "ma_bi_khoa"
    _lui_gio_gui("ban@example.com", 61)
    assert app.post("/auth/gui-lai-ma", headers=_h(app)).status_code == 200
    assert _ma(app, hop_thu.ma("ban@example.com")).status_code == 200


def test_ma_het_han_sau_10_phut(app, hop_thu):
    _dk(app)
    ma = hop_thu.ma("ban@example.com")
    _lui_gio_gui("ban@example.com", 601)
    r = _ma(app, ma)
    assert r.status_code == 400 and r.json()["ma"] == "ma_het_han"


def test_gui_lai_ma_cho_60_giay_va_toi_da_5_ma_mot_ngay(app, hop_thu):
    _dk(app)
    r = app.post("/auth/gui-lai-ma", headers=_h(app))
    assert r.status_code == 429 and r.json()["ma"] == "cho_gui_lai" and 55 <= r.json()["cho"] <= 60
    ma_cu = hop_thu.ma("ban@example.com")
    for _ in range(4):  # lần gửi thứ 2..5
        _lui_gio_gui("ban@example.com", 61)
        assert app.post("/auth/gui-lai-ma", headers=_h(app)).status_code == 200
    assert len(hop_thu) == 5
    _lui_gio_gui("ban@example.com", 61)
    r = app.post("/auth/gui-lai-ma", headers=_h(app))
    assert r.status_code == 429 and "5 mã trong 24 giờ" in r.json()["loi"]
    _lui_gio_gui("ban@example.com", 3600)  # 1 giờ sau vẫn chưa được (giới hạn theo NGÀY)
    assert app.post("/auth/gui-lai-ma", headers=_h(app)).status_code == 429
    _lui_gio_gui("ban@example.com", 86400)  # 24 giờ sau → gửi được
    assert app.post("/auth/gui-lai-ma", headers=_h(app)).status_code == 200
    assert _ma(app, ma_cu).status_code == 400  # gửi mã mới → mã cũ hết hiệu lực (trừ khi trùng ngẫu nhiên)
    assert _ma(app, hop_thu.ma("ban@example.com")).status_code == 200


# ================================================================ chống tạo hàng loạt
@pytest.mark.parametrize("bien_the", ["Ban.Ab@gmail.com", "banab+abc@gmail.com", "b.a.n.a.b+x@googlemail.com", "BANAB@GMAIL.COM"])
def test_moi_email_mot_tai_khoan_ke_ca_bien_the_gmail(app, hop_thu, bien_the):
    assert _dk(app, email="banab@gmail.com").status_code == 200
    r = _dk(app, email=bien_the)
    assert r.status_code == 409 and r.json()["ma"] == "email_da_co"
    assert len(hop_thu) == 1


@pytest.mark.parametrize("email", ["a@mailinator.com", "b@yopmail.com", "c@10minutemail.com", "d@abc.guerrillamail.com", "e@tempmail.com"])
def test_chan_email_tam_thoi(app, hop_thu, email):
    r = _dk(app, email=email)
    assert r.status_code == 400 and r.json()["ma"] == "email_tam" and not hop_thu


def test_email_that_khong_bi_chan_nham():
    for email in ("a@gmail.com", "b@outlook.com", "c@yahoo.com", "d@icloud.com", "e@hcmus.edu.vn", "f@fpt.edu.vn"):
        assert not xac_thuc_email.la_email_tam(email), email


def test_toi_da_3_dang_ky_moi_ip_moi_ngay(app, hop_thu):
    assert [_dk(app, email=f"u{i}@example.com").status_code for i in range(3)] == [200] * 3
    r = _dk(app, email="u3@example.com")
    assert r.status_code == 429 and r.json()["ma"] == "dang_ky_qua_nhieu" and len(hop_thu) == 3
    assert db.tim_theo_email("u3@example.com") is None
    with db.ket_noi() as con:  # 24 giờ sau → được đăng ký tiếp
        con.execute("UPDATE activity_log SET ts = ts - 86401 WHERE event = 'dang_ky'")
    assert _dk(app, email="u3@example.com").status_code == 200


def test_turnstile(app, hop_thu, monkeypatch):
    monkeypatch.setattr(settings, "turnstile_site_key", "site")
    monkeypatch.setattr(settings, "turnstile_secret_key", "secret")
    assert app.get("/api/toi").json()["turnstile_site_key"] == "site"
    r = _dk(app)
    assert r.status_code == 400 and r.json()["ma"] == "captcha" and not hop_thu
    goi = []

    class TL:
        def __init__(self, ok):
            self.ok = ok

        def json(self):
            return {"success": self.ok}

    monkeypatch.setattr(xac_thuc_email.requests, "post", lambda url, data, timeout: goi.append(data) or TL(data["response"] == "tot"))
    assert _dk(app, captcha="xau").json()["ma"] == "captcha"
    assert _dk(app, captcha="tot").status_code == 200 and goi[-1]["secret"] == "secret"


def test_chua_cau_hinh_email_thi_khong_tao_tai_khoan(app, monkeypatch):
    monkeypatch.setattr(settings, "smtp_user", "")
    r = _dk(app)
    assert r.status_code == 503 and r.json()["ma"] == "chua_cau_hinh_email" and db.tim_theo_email("ban@example.com") is None


def test_gui_email_loi_thi_huy_tai_khoan_vua_tao(app, monkeypatch):
    def hong(*a):
        raise xac_thuc_email.LoiXacThuc("Không gửi được email", "gui_email_loi", 502)

    monkeypatch.setattr(xac_thuc_email, "gui_thu", hong)
    assert _dk(app).status_code == 502 and db.tim_theo_email("ban@example.com") is None


# ================================================================ tài khoản cũ (trước khi có xác thực) + admin miễn
def test_tai_khoan_cu_phai_xac_thuc_o_lan_dang_nhap_toi(app, hop_thu):
    cu = db.tao_tai_khoan_email("cu@example.com", "Cũ", bam_mat_khau(MK))  # như tài khoản tạo từ bản trước
    assert cu.can_xac_thuc
    app.get("/api/toi")
    with app as c:  # phiên đăng nhập cũ còn hạn → bị đăng xuất
        c.cookies.clear()
    r = _dn(app, "cu@example.com")
    assert r.json()["can_xac_thuc"] and hop_thu[-1]["den"] == "cu@example.com"
    assert _ma(app, hop_thu.ma("cu@example.com")).status_code == 200
    assert not db.lay_nguoi_dung(cu.id).can_xac_thuc


def test_phien_cu_cua_tai_khoan_chua_xac_thuc_bi_huy(app, hop_thu):
    cu = db.tao_tai_khoan_email("cu@example.com", "Cũ", bam_mat_khau(MK), da_xac_thuc=True)
    assert _dn(app, "cu@example.com").json()["ok"]
    with db.ket_noi() as con:
        con.execute("UPDATE users SET email_verified = 0 WHERE id = ?", (cu.id,))
    assert not app.get("/api/toi").json()["dang_nhap"]


def test_admin_duoc_mien_xac_thuc(app, hop_thu):
    a = db.tao_tai_khoan_email("admin@example.com", "Admin", bam_mat_khau(MK))
    db.dat_admin(a.id, True)
    assert _dn(app, "admin@example.com").json() == {"ok": True, "tiep": "/"} and not hop_thu


def test_nang_cap_csdl_google_coi_nhu_da_xac_thuc(tmp_path, monkeypatch):
    duong_dan = tmp_path / "cu.db"
    with sqlite3.connect(duong_dan) as con:
        con.execute("CREATE TABLE users (id INTEGER PRIMARY KEY AUTOINCREMENT, google_sub TEXT NOT NULL UNIQUE, "
                    "email TEXT NOT NULL DEFAULT '', name TEXT NOT NULL DEFAULT '', picture TEXT NOT NULL DEFAULT '', "
                    "gemini_key_enc TEXT, gemini_key_last4 TEXT, password_hash TEXT, is_admin INTEGER NOT NULL DEFAULT 0, "
                    "trial_used INTEGER NOT NULL DEFAULT 0, created_at TEXT NOT NULL, last_login_at TEXT NOT NULL)")
        con.execute("INSERT INTO users (google_sub, email, created_at, last_login_at) VALUES ('g-1', 'A.B@gmail.com', 't', 't')")
        con.execute("INSERT INTO users (google_sub, email, created_at, last_login_at) VALUES ('email:c@x.com', 'c@x.com', 't', 't')")
    monkeypatch.setattr(db, "DUONG_DAN", duong_dan)
    g, e = db.lay_nguoi_dung(1), db.lay_nguoi_dung(2)
    assert g.da_xac_thuc_email and not g.can_xac_thuc and g.email_chuan == "ab@gmail.com"
    assert e.can_xac_thuc and not e.bi_khoa


# ================================================================ quên mật khẩu
def test_quen_mat_khau(app, hop_thu):
    _dk(app)
    _ma(app, hop_thu.ma("ban@example.com"))
    _xuat(app)
    r = app.post("/auth/quen-mat-khau", json={"email": "ban@example.com"}, headers=_h(app))
    assert r.status_code == 429 and r.json()["ma"] == "cho_gui_lai"  # 60 giây/lần cho mỗi email, mọi loại mã
    _lui_gio_gui("ban@example.com", 61)
    r1 = app.post("/auth/quen-mat-khau", json={"email": "BAN@example.com"}, headers=_h(app))
    r2 = app.post("/auth/quen-mat-khau", json={"email": "khong-co@example.com"}, headers=_h(app))
    assert r1.status_code == r2.status_code == 200 and r1.json()["cho_gui_lai"] == r2.json()["cho_gui_lai"]
    assert len(hop_thu) == 2 and "đặt lại mật khẩu" in hop_thu[-1]["tieu_de"]
    ma = hop_thu.ma("ban@example.com")
    dat = lambda m, mk="moi-moi-moi-1": app.post("/auth/dat-lai-mat-khau", json={"email": "ban@example.com", "ma": m, "mat_khau_moi": mk},
                                               headers=_h(app))
    assert dat(ma, "ngan").status_code == 400
    assert dat("999999" if ma != "999999" else "888888").json()["ma"] == "ma_sai"
    assert dat(ma).json() == {"ok": True, "tiep": "/"} and app.get("/api/toi").json()["dang_nhap"]
    _xuat(app)
    assert _dn(app).status_code == 401 and _dn(app, mk="moi-moi-moi-1").json()["ok"]
    assert [d["su_kien"] for d in nhat_ky.danh_sach(loai="password")] == ["dat_lai_mat_khau"]


def test_quen_mat_khau_xac_thuc_luon_email_chua_xac_thuc(app, hop_thu):
    u = db.tao_tai_khoan_email("cu@example.com", "Cũ", bam_mat_khau(MK))
    app.post("/auth/quen-mat-khau", json={"email": "cu@example.com"}, headers=_h(app))
    r = app.post("/auth/dat-lai-mat-khau", json={"email": "cu@example.com", "ma": hop_thu.ma("cu@example.com"), "mat_khau_moi": "moi-moi-moi-1"},
                 headers=_h(app))
    assert r.json()["ok"] and db.lay_nguoi_dung(u.id).da_xac_thuc_email


def test_google_lien_ket_tai_khoan_cung_email_va_bo_mat_khau_chua_xac_thuc(client, cau_hinh, dang_nhap):
    u = db.tao_tai_khoan_email("ab@gmail.com", "Kẻ chiếm", bam_mat_khau(MK))  # ai đó đăng ký trước bằng email của người khác
    dang_nhap(client, sub="google-that", email="a.b@gmail.com")
    toi = client.get("/api/toi").json()
    assert toi["dang_nhap"] and toi["nguoi_dung"]["email"] == "ab@gmail.com"
    moi = db.lay_nguoi_dung(u.id)
    assert moi.da_xac_thuc_email and moi.mat_khau_bam is None  # mật khẩu của người chưa chứng minh email bị bỏ
    assert len(db.danh_sach_nguoi_dung()) == 1


# ================================================================ nhãn "thành viên"
def test_nhan_thanh_vien(app, hop_thu, dang_nhap):
    assert app.get("/api/toi").json()["nhan_thanh_vien"] == "thành viên"
    _dk(app)
    _ma(app, hop_thu.ma("ban@example.com"))
    h = _h(app)
    assert app.post("/admin/api/nhan-thanh-vien", json={"nhan": "học sinh"}, headers=h).status_code == 403
    a = db.tao_tai_khoan_email("admin@example.com", "Admin", bam_mat_khau(MK))
    db.dat_admin(a.id, True)
    _xuat(app)
    _dn(app, "admin@example.com")
    h = _h(app)
    assert app.post("/admin/api/nhan-thanh-vien", json={"nhan": "   "}, headers=h).status_code == 400
    assert app.post("/admin/api/nhan-thanh-vien", json={"nhan": "  học   sinh "}, headers=h).json() == {"ok": True, "nhan": "học sinh"}
    toi = app.get("/api/toi").json()
    assert toi["nhan_thanh_vien"] == "học sinh" and toi["la_chu_app"]


# ================================================================ đổi tên
def _dang_nhap_ban(app, hop_thu):
    _dk(app)
    _ma(app, hop_thu.ma("ban@example.com"))
    return _h(app)


@pytest.mark.parametrize("ten, ma", [("A", "ten_do_dai"), ("x" * 31, "ten_do_dai"), ("Trọng 😀", "ten_ky_tu"), ("<b>Trọng</b>", "ten_ky_tu"),
                                     ("12345", "ten_ky_tu"), ("Bạn", "ten_giong")])
def test_doi_ten_sai(app, hop_thu, ten, ma):
    h = _dang_nhap_ban(app, hop_thu)
    r = app.post("/api/doi-ten", json={"ten": ten}, headers=h)
    assert r.status_code == 400 and r.json()["ma"] == ma


def test_doi_ten_tieng_viet_cat_khoang_trang_va_gioi_han_3_lan_ngay(app, hop_thu):
    h = _dang_nhap_ban(app, hop_thu)
    r = app.post("/api/doi-ten", json={"ten": "   Nguyễn   Thị  Ánh  "}, headers=h)
    assert r.json() == {"ok": True, "ten": "Nguyễn Thị Ánh", "con_lai_hom_nay": 2}
    assert app.get("/api/toi").json()["nguoi_dung"]["ten"] == "Nguyễn Thị Ánh"
    assert app.post("/api/doi-ten", json={"ten": "Đặng Văn Lâm"}, headers=h).status_code == 200
    assert app.post("/api/doi-ten", json={"ten": "Lê O'Neil-Trần"}, headers=h).status_code == 200
    r = app.post("/api/doi-ten", json={"ten": "Tên Thứ Tư"}, headers=h)
    assert r.status_code == 429 and r.json()["ma"] == "doi_ten_qua_nhieu"
    u = db.tim_theo_email("ban@example.com")
    ls = db.lich_su_doi_ten(u.id)
    assert [x["ten_moi"] for x in ls] == ["Lê O'Neil-Trần", "Đặng Văn Lâm", "Nguyễn Thị Ánh"] and ls[-1]["ten_cu"] == "Bạn"
    with db.ket_noi() as con:  # 24 giờ sau → đổi tiếp được
        con.execute("UPDATE name_changes SET ts = ts - 86401")
    assert app.post("/api/doi-ten", json={"ten": "Tên Thứ Tư"}, headers=h).status_code == 200


def test_doi_ten_khong_trung_ten_admin(app, hop_thu):
    a = db.tao_tai_khoan_email("admin@example.com", "Trọng Quản Trị", bam_mat_khau(MK))
    db.dat_admin(a.id, True)
    h = _dang_nhap_ban(app, hop_thu)
    for ten in ("trong quan tri", "TRỌNG  QUẢN TRỊ", "Admin"):
        r = app.post("/api/doi-ten", json={"ten": ten}, headers=h)
        assert r.status_code == 400 and r.json()["ma"] == "ten_trung_admin", ten


def test_doi_ten_can_dang_nhap_va_csrf(app):
    assert app.post("/api/doi-ten", json={"ten": "Tên Mới"}).status_code == 403
    assert app.post("/api/doi-ten", json={"ten": "Tên Mới"}, headers=_h(app)).status_code == 401


def test_google_dang_nhap_lai_khong_ghi_de_ten_da_doi(client, cau_hinh, dang_nhap):
    dang_nhap(client, sub="g-1", email="g@example.com")
    h = _h(client)
    assert client.post("/api/doi-ten", json={"ten": "Tên Tự Đặt"}, headers=h).status_code == 200
    dang_nhap(client, sub="g-1", email="g@example.com")
    assert client.get("/api/toi").json()["nguoi_dung"]["ten"] == "Tên Tự Đặt"


def test_khoa_ten_bo_dau():
    assert auth.khoa_ten("Đặng  Trọng-Admin") == auth.khoa_ten("dang trong admin") == "dangtrongadmin"


def test_don_dep_ma_cu(csdl_tam):
    with db.ket_noi() as con:
        con.execute("INSERT INTO email_codes (email_norm, purpose, code_hash, created, expires) VALUES ('a@b.c', 'xac_thuc', 'x', ?, ?)",
                    (time.time() - 3 * 86400, time.time()))
    xac_thuc_email.don_dep()
    with db.ket_noi() as con:
        assert con.execute("SELECT COUNT(*) FROM email_codes").fetchone()[0] == 0


# ================================================================ gửi SMTP: EHLO → STARTTLS → EHLO → đăng nhập (lỗi thật đã gặp với Gmail)
class _SMTPGia:
    """Giả lập máy chủ SMTP: chỉ công bố STARTTLS sau EHLO; ghi lại thứ tự lệnh."""
    lenh: list = []
    co_tls = True
    dong_khi_dang_nhap = False

    def __init__(self, host, port, timeout=None):
        self.esmtp_features = {}
        _SMTPGia.lenh = [("ket_noi", host, port)]

    def __enter__(self):
        return self

    def __exit__(self, *a):
        _SMTPGia.lenh.append("quit")

    def ehlo(self):
        _SMTPGia.lenh.append("ehlo")
        self.esmtp_features = {"starttls": ""} if _SMTPGia.co_tls else {}

    def has_extn(self, ten):
        return ten in self.esmtp_features

    def starttls(self, context=None):
        _SMTPGia.lenh.append("starttls")

    def login(self, u, p):
        if _SMTPGia.dong_khi_dang_nhap:
            import smtplib
            raise smtplib.SMTPServerDisconnected("Connection unexpectedly closed")
        _SMTPGia.lenh.append(("login", u))

    def send_message(self, thu):
        _SMTPGia.lenh.append(("gui", thu["To"]))


@pytest.fixture
def smtp_gia(cau_hinh, monkeypatch):
    import smtplib
    monkeypatch.setattr(smtplib, "SMTP", _SMTPGia)
    monkeypatch.setattr(settings, "smtp_host", "smtp.gmail.com")
    monkeypatch.setattr(settings, "smtp_port", 587)
    monkeypatch.setattr(settings, "smtp_user", "app@gmail.com")
    monkeypatch.setattr(settings, "smtp_pass", "abcdefghijklmnop")
    _SMTPGia.co_tls, _SMTPGia.dong_khi_dang_nhap = True, False
    return _SMTPGia


def test_smtp_ehlo_roi_starttls_roi_dang_nhap(smtp_gia):
    xac_thuc_email.gui_thu("ban@example.com", "t", "v", "<p>v</p>")
    assert smtp_gia.lenh == [("ket_noi", "smtp.gmail.com", 587), "ehlo", "starttls", "ehlo", ("login", "app@gmail.com"),
                             ("gui", "ban@example.com"), "quit"]


def test_smtp_khong_gui_mat_khau_qua_ket_noi_khong_ma_hoa(smtp_gia):
    smtp_gia.co_tls = False
    with pytest.raises(xac_thuc_email.LoiXacThuc, match="STARTTLS"):
        xac_thuc_email.gui_thu("ban@example.com", "t", "v", "<p>v</p>")
    assert not any(isinstance(x, tuple) and x[0] == "login" for x in smtp_gia.lenh)


def test_smtp_sai_app_password_bao_ro(smtp_gia):
    smtp_gia.dong_khi_dang_nhap = True  # Gmail: 535 với PLAIN rồi đóng kết nối
    with pytest.raises(xac_thuc_email.LoiXacThuc, match="SMTP_USER/SMTP_PASS"):
        xac_thuc_email.gui_thu("ban@example.com", "t", "v", "<p>v</p>")


def test_gui_qua_brevo_khi_may_chu_chan_smtp(monkeypatch):
    """Render Free chặn cổng SMTP → gửi mã qua API web của Brevo (BREVO_API_KEY), người gửi = EMAIL_FROM đã xác minh trong Brevo."""
    import smtplib
    from backend import xac_thuc_email
    from backend.config import settings

    monkeypatch.setattr(settings, "brevo_api_key", "brevo-gia")
    monkeypatch.setattr(settings, "email_from", "chu.app@gmail.com")
    monkeypatch.setattr(settings, "smtp_user", "")
    monkeypatch.setattr(settings, "smtp_pass", "")
    assert settings.gui_email_san_sang
    gui = {}

    class Tra:
        status_code = 201

        @staticmethod
        def json():
            return {"code": "unauthorized", "message": "unrecognised IP address"}

    def post(url, headers=None, json=None, timeout=None):
        gui.update(url=url, headers=headers, json=json)
        return Tra()

    monkeypatch.setattr(xac_thuc_email.requests, "post", post)
    monkeypatch.setattr(smtplib, "SMTP", lambda *a, **k: (_ for _ in ()).throw(AssertionError("không được dùng SMTP")))
    xac_thuc_email.gui_thu("hoc.sinh@example.com", "Mã xác thực", "123456", "<b>123456</b>")
    assert gui["url"] == "https://api.brevo.com/v3/smtp/email" and gui["headers"]["api-key"] == "brevo-gia"
    assert gui["json"]["sender"]["email"] == "chu.app@gmail.com" and gui["json"]["to"] == [{"email": "hoc.sinh@example.com"}]

    Tra.status_code = 401   # key sai → báo lỗi tiếng Việt, không lộ key
    try:
        xac_thuc_email.gui_thu("hoc.sinh@example.com", "Mã", "1", "1")
        raise AssertionError("phải báo lỗi")
    except xac_thuc_email.LoiXacThuc as e:
        assert "BREVO_API_KEY" in str(e) and "brevo-gia" not in str(e)


def test_nhan_ca_key_mcp_cua_brevo():
    import base64
    import json
    from backend.xac_thuc_email import khoa_brevo
    assert khoa_brevo("  xkeysib-abc123  ") == ("xkeysib-abc123", "xkeysib")
    mcp = base64.b64encode(json.dumps({"api_key": "xkeysib-tu-mcp"}).encode()).decode()
    assert khoa_brevo(mcp) == ("xkeysib-tu-mcp", "MCP (base64)")
    assert khoa_brevo("xsmtpsib-zzz")[1].startswith("xsmtpsib")
    k, dang = khoa_brevo("ma-bi-mat-la")
    assert "ma-bi-mat-la" not in dang and "dài" in dang
