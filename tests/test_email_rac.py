"""Chặn email ảo / rác khi đăng ký (email_rac.py): email tạm thời, danh sách chặn / cho phép, MX, tên ngẫu nhiên (nghi ngờ),
ô bẫy bot, 3 lần thử / IP / giờ, xoá tài khoản chưa xác minh sau 24 giờ, cập nhật danh sách, CLI. Không gọi DNS / mạng thật."""
import time
from types import SimpleNamespace

import pytest

import admin_cli
from backend import db, email_rac
from backend.bao_mat import bam_mat_khau
from backend.config import settings
from backend.email_rac import _tra_mx_dns as TRA_MX_THAT  # hàm thật (conftest thay bằng DNS giả cho mọi test khác)

MK = "mat-khau-an-toan-123"


def _h(c):
    return {"X-CSRF-Token": c.get("/api/toi").json()["csrf"]}


def _dk(c, email="ban@example.com", **them):
    return c.post("/auth/dang-ky", json={"email": email, "mat_khau": MK, "ten": "Bạn", **them}, headers=_h(c))


def _chan(ly_do=None):
    return [x for x in email_rac.danh_sach_chan() if ly_do is None or x["ly_do"] == ly_do]


@pytest.fixture
def app(client, hop_thu, monkeypatch):
    monkeypatch.setattr(settings, "google_client_id", "")
    monkeypatch.setattr(settings, "google_client_secret", "")
    return client


def _admin(c, dang_nhap, monkeypatch):
    """Đăng nhập Google giả bằng một tài khoản rồi cấp admin."""
    monkeypatch.setattr(settings, "google_client_id", "id-test.apps.googleusercontent.com")
    monkeypatch.setattr(settings, "google_client_secret", "secret-test")
    dang_nhap(c, "admin-1", "admin@example.com")
    db.dat_admin(db.tim_theo_email("admin@example.com").id, True)
    return {"X-CSRF-Token": c.get("/api/toi").json()["csrf"]}


# ================================================================ 1. email tạm thời
@pytest.mark.parametrize("email", ["a@mailinator.com", "b@10minutemail.com", "c@guerrillamail.com", "d@yopmail.com", "e@abc.mailinator.com",
                                   "f@tempmail.de", "g@temp-mail.io", "h@yopmail.fr", "i@sub.tempmail.xyz"])
def test_chan_email_tam_thoi_ke_ca_moi_duoi_va_ten_mien_con(app, hop_thu, email):
    r = _dk(app, email=email)
    assert r.status_code == 400 and r.json()["ma"] == "email_tam" and "email thật" in r.json()["loi"]
    assert not hop_thu and db.tim_theo_email(email) is None
    x = _chan("email_tam")[0]
    assert x["email"] == email and x["ip"] == "203.0.113.50" and x["luc"]  # ghi log: email, IP, lý do, thời gian


def test_email_that_khong_bi_chan_nham():
    for email in ("a@gmail.com", "b@outlook.com", "c@yahoo.com", "d@icloud.com", "e@hcmus.edu.vn", "f@fpt.edu.vn", "g@mail.com"):
        assert not email_rac.la_email_tam(email) and not email_rac.kiem_tra(email).chan, email


def test_danh_sach_tu_github_du_lon():
    assert len(email_rac.danh_sach_tam()) > 9000  # danh sách mở disposable-email-domains, không phải vài tên miền viết tay


# ================================================================ 2. danh sách chặn / cho phép của admin
def test_admin_them_chan_va_cho_phep(app, hop_thu, dang_nhap, monkeypatch):
    h = _admin(app, dang_nhap, monkeypatch)
    assert app.post("/admin/api/email-rac/quy-tac", json={"ten_mien": "badco.vn", "loai": "chan"}, headers=h).status_code == 200
    assert app.post("/admin/api/email-rac/quy-tac", json={"ten_mien": "spam.*", "loai": "chan", "ghi_chu": "mọi đuôi"}, headers=h).status_code == 200
    assert app.post("/admin/api/email-rac/quy-tac", json={"ten_mien": "a b", "loai": "chan"}, headers=h).status_code == 400
    d = app.post("/admin/api/email-rac/quy-tac", json={"ten_mien": "mailinator.com", "loai": "cho"}, headers=h).json()
    assert {(x["ten_mien"], x["loai"]) for x in d["quy_tac"]} == {("badco.vn", "chan"), ("spam.*", "chan"), ("mailinator.com", "cho")}
    app.post("/auth/dang-xuat", headers={**h, "Accept": "application/json"})
    for email in ("a@badco.vn", "b@mail.badco.vn", "c@spam.net"):  # (3 lần – lần thứ 4 sẽ vướng giới hạn 3 lần thử/IP/giờ)
        r = _dk(app, email=email)
        assert r.status_code == 400 and r.json()["ma"] == "ten_mien_bi_chan", email
    assert email_rac.kiem_tra("d@spam.io").ma == "ten_mien_bi_chan"
    assert email_rac.kiem_tra("x@mailinator.com").chan is False  # whitelist thắng danh sách email tạm thời
    assert email_rac.xoa_quy_tac("mailinator.com") and email_rac.kiem_tra("x@mailinator.com").ma == "email_tam"


def test_chi_admin_xem_va_sua_danh_sach(app):
    assert app.get("/admin/api/email-rac").status_code in (401, 403)
    assert app.post("/admin/api/email-rac/quy-tac", json={"ten_mien": "x.com"}, headers=_h(app)).status_code in (401, 403)


# ================================================================ 3. định dạng + MX
@pytest.mark.parametrize("email", ["khong-co-a-cong", "a@b", "a..b@gmail.com", ".a@gmail.com", "a.@gmail.com", "a@-x.com", "a b@gmail.com"])
def test_sai_dinh_dang(app, email):
    r = _dk(app, email=email)
    assert r.status_code == 400 and r.json()["ma"] == "email_sai"


def test_khong_co_mx_hoac_ten_mien_khong_ton_tai_bi_tu_choi_loi_dns_thi_cho_qua(app, hop_thu, monkeypatch):
    dns = {"khongthu.vn": "khong_mx", "khongtontai-abc.vn": "khong_ton_tai", "dnsloi.vn": "loi"}
    monkeypatch.setattr(email_rac, "_tra_mx_dns", lambda mien: dns.get(mien, "co"))
    r = _dk(app, email="a@khongthu.vn")
    assert r.status_code == 400 and r.json()["ma"] == "khong_mx" and "không nhận được thư" in r.json()["loi"]
    r = _dk(app, email="b@khongtontai-abc.vn")
    assert r.status_code == 400 and r.json()["ma"] == "khong_ton_tai" and "khongtontai-abc.vn" in r.json()["loi"]
    assert _dk(app, email="c@dnsloi.vn").status_code == 200  # DNS lỗi (mạng) → không chặn nhầm người thật
    assert {x["ly_do"] for x in _chan()} == {"khong_mx", "khong_ton_tai"}


def test_tra_mx_that_voi_dns_gia(monkeypatch):
    import dns.exception
    import dns.resolver

    def tra(mien, loai, lifetime):
        assert loai == "MX"
        if mien == "khongco.vn":
            raise dns.resolver.NXDOMAIN()
        if mien == "khongmx.vn":
            raise dns.resolver.NoAnswer()
        if mien == "cham.vn":
            raise dns.exception.Timeout()
        return [SimpleNamespace(exchange="." if mien == "nullmx.vn" else "mx.google.com.")]

    monkeypatch.setattr(dns.resolver, "resolve", tra)
    assert [TRA_MX_THAT(m) for m in ("gmail.com", "khongco.vn", "khongmx.vn", "cham.vn", "nullmx.vn")] == \
        ["co", "khong_ton_tai", "khong_mx", "loi", "khong_mx"]


# ================================================================ 4. tên ngẫu nhiên → nghi ngờ (không chặn), admin duyệt
@pytest.mark.parametrize("email", ["xk29fjq8zz@gmail.com", "qwrtzpxk@gmail.com", "a1b2c3d4e5@gmail.com", "djtm3c4u@gmail.com"])
def test_ten_ngau_nhien_nghi_ngo(email):
    assert email_rac.ly_do_nghi_ngo(email), email


@pytest.mark.parametrize("email", ["nguyenvana@gmail.com", "toannguyen.180982@gmail.com", "tranductrong6a5@gmail.com", "0912345678@gmail.com",
                                   "lamnguyen77799@gmail.com", "hs1.thu@example.com", "john.smith+abc@outlook.com", "khanhlinh2008@yahoo.com"])
def test_ten_binh_thuong_khong_nghi_ngo(email):
    assert not email_rac.ly_do_nghi_ngo(email), (email, email_rac.diem_ngau_nhien(email))


def test_dang_ky_ten_ngau_nhien_van_tao_va_admin_duyet(app, hop_thu, dang_nhap, monkeypatch):
    assert _dk(app, email="xk29fjq8zz@gmail.com").status_code == 200  # không chặn hẳn
    u = db.tim_theo_email("xk29fjq8zz@gmail.com")
    assert _dk(app, email="zq8mxk2v7w@gmail.com").status_code == 200
    u2 = db.tim_theo_email("zq8mxk2v7w@gmail.com")
    h = _admin(app, dang_nhap, monkeypatch)
    d = app.get("/admin/api/email-rac", headers=h).json()
    assert {x["id"] for x in d["nghi_ngo"]} == {u.id, u2.id} and "ngẫu nhiên" in d["nghi_ngo"][0]["ly_do"]
    nd = {x["id"]: x for x in app.get("/admin/api/nguoi-dung", headers=h).json()["nguoi_dung"]}
    assert nd[u.id]["nghi_ngo"] and not nd[db.tim_theo_email("admin@example.com").id]["nghi_ngo"]
    d = app.post(f"/admin/api/email-rac/nghi-ngo/{u.id}", json={"khoa": False}, headers=h).json()
    assert [x["id"] for x in d["nghi_ngo"]] == [u2.id] and not db.lay_nguoi_dung(u.id).bi_khoa
    app.post(f"/admin/api/email-rac/nghi-ngo/{u2.id}", json={"khoa": True}, headers=h)
    assert db.lay_nguoi_dung(u2.id).bi_khoa
    su_kien = [x["su_kien"] for x in app.get("/admin/api/hoat-dong?loai=spam", headers=h).json()["su_kien"]]
    assert su_kien.count("email_nghi_ngo") == 2 and su_kien.count("duyet_nghi_ngo") == 2


# ================================================================ 5. bot: ô bẫy + 3 lần thử / IP / giờ
def test_o_bay_honeypot(app, hop_thu):
    r = _dk(app, email="bot@gmail.com", website="http://spam.example")
    assert r.status_code == 400 and r.json()["ma"] == "honeypot" and not hop_thu and db.tim_theo_email("bot@gmail.com") is None
    assert _chan("honeypot")[0]["email"] == "bot@gmail.com"


def test_toi_da_3_lan_thu_dang_ky_moi_ip_moi_gio(app, hop_thu):
    for i in range(3):  # bot thử 3 email tạm → bị chặn, mỗi lần đều tính
        assert _dk(app, email=f"bot{i}@mailinator.com").status_code == 400
    r = _dk(app, email="nguoi.that@gmail.com")
    assert r.status_code == 429 and r.json()["ma"] == "dang_ky_qua_nhieu" and "1 giờ" in r.json()["loi"]
    assert db.tim_theo_email("nguoi.that@gmail.com") is None and _chan("qua_nhieu_lan_thu")
    with db.ket_noi() as con:  # 1 giờ sau → được thử lại
        con.execute("UPDATE email_blocks SET ts = ts - 3601")
    assert _dk(app, email="nguoi.that@gmail.com").status_code == 200


def test_sai_mat_khau_hay_trung_email_khong_tinh_la_lan_thu(app, hop_thu):
    assert _dk(app, email="a@gmail.com").status_code == 200
    for _ in range(3):
        assert _dk(app, email="a@gmail.com").status_code == 409
        assert app.post("/auth/dang-ky", json={"email": "b@gmail.com", "mat_khau": "ngan", "ten": "B"}, headers=_h(app)).status_code == 400
    assert _dk(app, email="b@gmail.com").status_code == 200


# ================================================================ 6. Gmail chuẩn hoá (cột email_norm UNIQUE)
def test_gmail_chuan_hoa_va_unique():
    assert {db.email_chuan(e) for e in ("A.B.C@gmail.com", "abc+123@gmail.com", "a.b.c+x@googlemail.com", "ABC@GMAIL.COM")} == {"abc@gmail.com"}
    db.tao_tai_khoan_email("a.b.c@gmail.com", "A", bam_mat_khau(MK))
    assert db.tao_tai_khoan_email("abc+1@googlemail.com", "B", bam_mat_khau(MK)) is None  # UNIQUE trong CSDL, không chỉ ở code
    with db.ket_noi() as con:
        assert any(r[1] == "users_email_norm" and r[2] == 1 for r in con.execute("PRAGMA index_list(users)"))


# ================================================================ 7. xoá tài khoản chưa xác minh sau 24 giờ (chỉ tài khoản đăng ký mới)
def test_xoa_tai_khoan_chua_xac_minh_sau_24_gio(app, hop_thu):
    assert _dk(app, email="moi@gmail.com").status_code == 200
    assert _dk(app, email="da.xac.minh@gmail.com").status_code == 200
    db.dat_xac_thuc_email(db.tim_theo_email("da.xac.minh@gmail.com").id)
    cu = db.tao_tai_khoan_email("cu.chua.xac.minh@gmail.com", "Cũ", bam_mat_khau(MK))  # tài khoản cũ: không có hạn xác minh
    email_rac.mot_vong()
    assert db.tim_theo_email("moi@gmail.com") is not None  # chưa đủ 24 giờ
    with db.ket_noi() as con:
        con.execute("UPDATE users SET verify_deadline = verify_deadline - 86401 WHERE verify_deadline IS NOT NULL")
    email_rac.mot_vong()  # (mạng bị chặn trong test → cập nhật danh sách thất bại êm, không ảnh hưởng việc xoá)
    assert db.tim_theo_email("moi@gmail.com") is None
    assert db.tim_theo_email("da.xac.minh@gmail.com") is not None and db.lay_nguoi_dung(cu.id) is not None
    with db.ket_noi() as con:
        assert con.execute("SELECT COUNT(*) FROM activity_log WHERE event = 'xoa_chua_xac_minh'").fetchone()[0] == 1


def test_tai_khoan_cu_dung_email_tam_van_dang_nhap_binh_thuong(app):
    db.tao_tai_khoan_email("cu@mailinator.com", "Cũ", bam_mat_khau(MK), da_xac_thuc=True)
    r = app.post("/auth/dang-nhap-mat-khau", json={"email": "cu@mailinator.com", "mat_khau": MK}, headers=_h(app))
    assert r.status_code == 200 and r.json()["ok"]


def test_google_tai_khoan_moi_ten_mien_bi_chan(client, monkeypatch):
    def thu(sub, email):
        async def lay_token(_request):
            return {"userinfo": {"sub": sub, "email": email, "email_verified": True, "name": "X", "picture": ""}}
        monkeypatch.setattr("backend.auth.oauth", SimpleNamespace(google=SimpleNamespace(authorize_access_token=lay_token)))
        return client.get("/auth/callback", follow_redirects=False)

    r = thu("g-1", "a@yopmail.com")
    assert r.status_code == 303 and r.headers["location"].endswith("loi=email_tam") and db.lay_theo_sub("g-1") is None
    assert _chan("ten_mien_google")[0]["chi_tiet"] == "email_tam"
    assert thu("g-2", "xk29fjq8zz@gmail.com").status_code == 303
    assert db.lay_theo_sub("g-2") is not None and email_rac.danh_sach_nghi_ngo()[0]["email"] == "xk29fjq8zz@gmail.com"


# ================================================================ 8. cập nhật danh sách từ GitHub (giả lập)
def test_cap_nhat_danh_sach(monkeypatch):
    class R:
        def __init__(self, text, status=200):
            self.text, self.status_code = text, status

    ds = [f"rac{i}.example" for i in range(1500)]
    monkeypatch.setattr(email_rac.requests, "get", lambda url, timeout, headers: R("# chú thích\n" + "\n".join(ds)))
    assert email_rac.can_cap_nhat()
    assert email_rac.cap_nhat_danh_sach() == 1500
    assert email_rac.la_email_tam("a@rac7.example") and email_rac.la_email_tam("a@mailinator.com")  # bản mới + bản đi kèm
    assert not email_rac.can_cap_nhat() and email_rac.thong_tin_danh_sach()["cap_nhat_luc"]
    monkeypatch.setattr(email_rac.requests, "get", lambda url, timeout, headers: R("vai.ten.mien\n"))
    with pytest.raises(email_rac.LoiCapNhat):
        email_rac.cap_nhat_danh_sach()  # bản tải về quá ngắn → coi là hỏng, giữ bản cũ
    assert email_rac.la_email_tam("a@rac7.example")
    monkeypatch.setattr(email_rac.requests, "get", lambda url, timeout, headers: R("", 503))
    with pytest.raises(email_rac.LoiCapNhat):
        email_rac.cap_nhat_danh_sach()


# ================================================================ 9. dòng lệnh
def test_cli_email(app, hop_thu, capsys):
    db.danh_sach_nguoi_dung()  # tạo CSDL tạm (CLI báo lỗi nếu chưa có CSDL)
    assert admin_cli.main(["email", "block", "rac.vn", "--ghi-chu", "thử"]) == 0
    assert admin_cli.main(["email", "allow", "truong.edu.vn"]) == 0
    assert admin_cli.main(["email", "block", "khong hop le"]) == 1
    assert admin_cli.main(["email", "rules"]) == 0
    out = capsys.readouterr().out
    assert "rac.vn" in out and "truong.edu.vn" in out
    assert admin_cli.main(["email", "check", "a@rac.vn"]) == 1 and "BỊ CHẶN" in capsys.readouterr().out
    assert admin_cli.main(["email", "check", "a.b.c+1@googlemail.com"]) == 0 and "abc@gmail.com" in capsys.readouterr().out
    assert _dk(app, email="x@rac.vn").status_code == 400
    assert admin_cli.main(["email", "blocked"]) == 0 and "x@rac.vn" in capsys.readouterr().out
    assert admin_cli.main(["email", "remove", "rac.vn"]) == 0 and not email_rac.kiem_tra("x@rac.vn").chan
    assert _dk(app, email="xk29fjq8zz@gmail.com").status_code == 200
    assert admin_cli.main(["email", "suspect"]) == 0 and "xk29fjq8zz" in capsys.readouterr().out
    assert admin_cli.main(["email", "approve", "xk29fjq8zz@gmail.com"]) == 0 and not email_rac.danh_sach_nghi_ngo()
    with db.ket_noi() as con:
        con.execute("UPDATE users SET verify_deadline = ?", (time.time() - 1,))
    assert admin_cli.main(["email", "purge"]) == 0 and db.tim_theo_email("xk29fjq8zz@gmail.com") is None
