"""Nhật ký hoạt động + quyền admin (/admin, /admin/api/*) + khoá/mở khoá + chấm thông báo + admin_cli.py."""
import json
import time

import pytest

from backend import db, nhat_ky
from backend.bao_mat import bam_mat_khau
from backend.config import settings
IP_KHACH = "203.0.113.50"  # = conftest.IP_KHACH (IP của client trong test)

MK = "mat-khau-an-toan-123"
UA_IPHONE = "Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1"
UA_WIN = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36"

GET_ADMIN = ["/admin/api/tong-quan", "/admin/api/online", "/admin/api/hoat-dong", "/admin/api/nguoi-dung", "/admin/api/nguoi-dung/1",
             "/admin/api/canh-bao", "/admin/api/bieu-do", "/admin/api/luong?mot_lan=1"]
POST_ADMIN = [("/admin/api/nguoi-dung/1/khoa", {"khoa": True}), ("/admin/api/da-xem", {"id": 1}), ("/admin/api/nhan-thanh-vien", {"nhan": "x"})]


def _h(c, **them):
    return {"X-CSRF-Token": c.get("/api/toi").json()["csrf"], **them}


def _dn(c, email, mk=MK, ua=UA_WIN):
    return c.post("/auth/dang-nhap-mat-khau", json={"email": email, "mat_khau": mk}, headers=_h(c, **{"User-Agent": ua}))


def _xuat(c):
    c.post("/auth/dang-xuat", headers=_h(c, Accept="application/json"))


@pytest.fixture
def app(client, hop_thu, monkeypatch):
    monkeypatch.setattr(settings, "google_client_id", "")
    monkeypatch.setattr(settings, "google_client_secret", "")
    thuong = db.tao_tai_khoan_email("hoc.sinh@example.com", "Học Sinh", bam_mat_khau(MK), da_xac_thuc=True)
    admin = db.tao_tai_khoan_email("admin@example.com", "Quản Trị", bam_mat_khau(MK), da_xac_thuc=True)
    db.dat_admin(admin.id, True)
    client.thuong, client.admin = thuong, admin
    return client


# ================================================================ quyền: chỉ admin
def test_chua_dang_nhap_401_va_trang_admin_chuyen_ve_dang_nhap(app):
    r = app.get("/admin", follow_redirects=False)
    assert r.status_code == 303 and r.headers["location"] == "/dang-nhap?tiep=%2Fadmin"
    for url in GET_ADMIN:
        assert app.get(url).status_code == 401, url


def test_tai_khoan_thuong_bi_403_moi_api_admin(app):
    assert _dn(app, "hoc.sinh@example.com").json()["ok"]
    assert app.get("/admin").status_code == 403
    for url in GET_ADMIN:
        r = app.get(url)
        assert r.status_code == 403 and r.json()["ma"] == "khong_phai_admin", url
    h = _h(app)
    for url, body in POST_ADMIN:
        assert app.post(url, json=body, headers=h).status_code == 403, url
    assert not db.lay_nguoi_dung(1).bi_khoa
    toi = app.get("/api/toi").json()
    assert toi["hoat_dong_moi"] == 0 and not toi["la_chu_app"] and toi["nhan_thanh_vien"] == "thành viên"
    assert "/admin" not in app.get("/").text and "/admin" not in app.get("/cai-dat").text.split("muc-hoat-dong")[0]


def test_admin_vao_duoc_trang_va_moi_api(app):
    _dn(app, "admin@example.com")
    r = app.get("/admin")
    assert r.status_code == 200 and "/static/admin.js?v=" in r.text and r.headers["cache-control"] == "private, no-store"
    for url in GET_ADMIN:
        assert app.get(url).status_code == 200, url


def test_post_admin_can_csrf(app):
    _dn(app, "admin@example.com")
    assert app.post("/admin/api/nguoi-dung/1/khoa", json={"khoa": True}).status_code == 403
    assert not db.lay_nguoi_dung(app.thuong.id).bi_khoa


# ================================================================ nhật ký
def test_ghi_nhat_ky_dang_nhap_dung_sai_dang_xuat_va_admin_thay_ngay(app):
    assert _dn(app, "hoc.sinh@example.com", mk="sai-sai-sai", ua=UA_IPHONE).status_code == 401
    assert _dn(app, "khong-co@example.com", ua=UA_IPHONE).status_code == 401
    assert _dn(app, "hoc.sinh@example.com", ua=UA_IPHONE).json()["ok"]
    _xuat(app)
    _dn(app, "admin@example.com")
    ds = app.get("/admin/api/hoat-dong").json()["su_kien"]
    su = [(d["su_kien"], d["email"]) for d in ds]
    assert su[:5] == [("dang_nhap", "admin@example.com"), ("dang_xuat", "hoc.sinh@example.com"), ("dang_nhap", "hoc.sinh@example.com"),
                      ("dang_nhap_sai", "khong-co@example.com"), ("dang_nhap_sai", "hoc.sinh@example.com")]
    sai = ds[4]
    assert sai["ten"] == "Học Sinh" and sai["phuong_thuc"] == "email" and sai["ip"] == IP_KHACH
    assert sai["thiet_bi"] == "Safari · iPhone" and sai["chi_tiet"] == "sai mật khẩu" and ds[3]["user_id"] is None
    # lọc theo người dùng / loại / ngày
    loc = app.get("/admin/api/hoat-dong", params={"nguoi": "hoc.sinh", "loai": "fail"}).json()["su_kien"]
    assert [d["su_kien"] for d in loc] == ["dang_nhap_sai"]
    assert len(app.get("/admin/api/hoat-dong", params={"ngay": time.strftime("%Y-%m-%d")}).json()["su_kien"]) == 5
    assert app.get("/admin/api/hoat-dong", params={"ngay": "2001-01-01"}).json()["su_kien"] == []
    # nhật ký không chứa mật khẩu
    with db.ket_noi() as con:
        tat_ca = " ".join(str(x) for r in con.execute("SELECT * FROM activity_log") for x in r)
    assert MK not in tat_ca and "sai-sai-sai" not in tat_ca


def test_dang_ky_va_xac_thuc_duoc_ghi(app, hop_thu):
    app.post("/auth/dang-ky", json={"email": "moi@example.com", "mat_khau": MK, "ten": "Mới"}, headers=_h(app))
    app.post("/auth/xac-thuc-email", json={"ma": hop_thu.ma("moi@example.com")}, headers=_h(app))
    su = [d["su_kien"] for d in nhat_ky.danh_sach()]
    assert su[:3] == ["dang_nhap", "xac_thuc_email", "dang_ky"]
    dk = nhat_ky.danh_sach(loai="register")[0]
    assert "tai_khoan_moi" in dk["co"] and dk["chi_tiet"] == "chờ xác thực email"


def test_dang_nhap_sai_nhieu_lan_va_nhieu_tai_khoan_cung_ip_duoc_danh_dau(app):
    for _ in range(3):
        _dn(app, "hoc.sinh@example.com", mk="sai-mat-khau")
    for i in range(3):
        db.tao_tai_khoan_email(f"u{i}@example.com", f"U{i}", bam_mat_khau(MK), da_xac_thuc=True)
        _dn(app, f"u{i}@example.com")
    sai = nhat_ky.danh_sach(loai="fail")[0]
    assert "sai_nhieu" in sai["co"] and "nhieu_tai_khoan_ip" in sai["co"]
    cb = nhat_ky.canh_bao()
    assert cb["dang_nhap_sai_nhieu"][0]["email"] == "hoc.sinh@example.com" and cb["dang_nhap_sai_nhieu"][0]["so_lan"] == 3
    assert cb["nhieu_tai_khoan_cung_ip"][0]["ip"] == IP_KHACH and cb["nhieu_tai_khoan_cung_ip"][0]["so_tai_khoan"] == 3


def test_tao_hang_loat_trong_10_phut(csdl_tam):
    for i in range(5):
        nhat_ky.ghi("dang_ky", None, email=f"x{i}@a.com", phuong_thuc="email", ip=f"1.1.1.{i}", ua="")
    loat = nhat_ky.canh_bao()["tao_hang_loat"]
    assert loat and loat[0]["so_dang_ky"] == 5 and len(loat[0]["ips"]) == 5


def test_online_tong_quan_bieu_do_chi_tiet(app):
    _dn(app, "hoc.sinh@example.com")
    app.post("/api/doi-ten", json={"ten": "Bạn Học Sinh"}, headers=_h(app))
    _xuat(app)
    _dn(app, "admin@example.com")
    on = app.get("/admin/api/online").json()["online"]
    assert [d["email"] for d in on] == ["admin@example.com"]  # đăng xuất → không còn online
    tq = app.get("/admin/api/tong-quan").json()
    assert tq["dang_nhap_hom_nay"] == 2 and tq["online"] == 1 and tq["tong_nguoi_dung"] == 2
    bd = app.get("/admin/api/bieu-do").json()["ngay"]
    assert len(bd) == 14 and bd[-1] == {"ngay": time.strftime("%Y-%m-%d"), "dang_nhap": 2, "dang_ky": 0}
    ct = app.get(f"/admin/api/nguoi-dung/{app.thuong.id}").json()
    assert ct["ten"] == "Bạn Học Sinh" and ct["vai_tro"] == "thành viên" and ct["bai_giai"]["xong"] == 0
    assert [x["ten_moi"] for x in ct["doi_ten"]] == ["Bạn Học Sinh"]
    assert {d["su_kien"] for d in ct["lich_su"]} >= {"dang_nhap", "dang_xuat", "doi_ten"}
    nd = {d["email"]: d for d in app.get("/admin/api/nguoi-dung").json()["nguoi_dung"]}
    assert nd["admin@example.com"]["vai_tro"] == "admin" and nd["hoc.sinh@example.com"]["xac_thuc"] == "roi"
    assert all("mat_khau" not in k and "key_ma" not in k for d in nd.values() for k in d)
    assert app.get("/admin/api/nguoi-dung/999").status_code == 404


# ================================================================ khoá / mở khoá
def test_khoa_thi_khong_dang_nhap_duoc_mo_khoa_thi_duoc(app):
    from fastapi.testclient import TestClient

    from backend.main import app as ung_dung

    hs = TestClient(ung_dung, base_url="https://testserver")  # trình duyệt của học sinh, đang đăng nhập
    assert _dn(hs, "hoc.sinh@example.com").json()["ok"] and hs.get("/api/toi").json()["dang_nhap"]
    _dn(app, "admin@example.com")
    h = _h(app)
    assert app.post(f"/admin/api/nguoi-dung/{app.admin.id}/khoa", json={"khoa": True}, headers=h).status_code == 400  # không tự khoá
    r = app.post(f"/admin/api/nguoi-dung/{app.thuong.id}/khoa", json={"khoa": True}, headers=h)
    assert r.json() == {"ok": True, "bi_khoa": True}
    assert not hs.get("/api/toi").json()["dang_nhap"]  # phiên đang mở bị đăng xuất ngay
    r = _dn(hs, "hoc.sinh@example.com")
    assert r.status_code == 403 and r.json()["ma"] == "tai_khoan_bi_khoa"
    assert app.post(f"/admin/api/nguoi-dung/{app.thuong.id}/khoa", json={"khoa": False}, headers=h).json()["bi_khoa"] is False
    assert _dn(hs, "hoc.sinh@example.com").json()["ok"]
    hd = app.get("/admin/api/hoat-dong", params={"loai": "admin"}).json()["su_kien"]
    assert [d["su_kien"] for d in hd] == ["mo_khoa", "khoa"] and hd[1]["doi_tuong"] == app.thuong.id and hd[1]["email"] == "admin@example.com"
    assert hd[1]["ten_phuong_thuc"] == "trang admin"


def test_khong_khoa_duoc_admin_khac(app):
    a2 = db.tao_tai_khoan_email("admin2@example.com", "A2", bam_mat_khau(MK), da_xac_thuc=True)
    db.dat_admin(a2.id, True)
    _dn(app, "admin@example.com")
    r = app.post(f"/admin/api/nguoi-dung/{a2.id}/khoa", json={"khoa": True}, headers=_h(app))
    assert r.status_code == 400 and not db.lay_nguoi_dung(a2.id).bi_khoa


# ================================================================ chấm thông báo + luồng SSE
def test_cham_thong_bao_khi_co_nguoi_moi_dang_nhap(app):
    _dn(app, "admin@example.com")
    assert app.get("/api/toi").json()["hoat_dong_moi"] == 0  # chính admin đăng nhập không tính
    from fastapi.testclient import TestClient

    from backend.main import app as ung_dung

    _dn(TestClient(ung_dung, base_url="https://testserver"), "hoc.sinh@example.com")
    assert app.get("/api/toi").json()["hoat_dong_moi"] == 1
    moi_nhat = app.get("/admin/api/hoat-dong").json()["id_moi_nhat"]
    assert app.post("/admin/api/da-xem", json={"id": moi_nhat}, headers=_h(app)).json() == {"ok": True}
    assert app.get("/api/toi").json()["hoat_dong_moi"] == 0


def test_luong_sse_tra_su_kien_moi(app):
    _dn(app, "admin@example.com")
    moc = nhat_ky.id_moi_nhat()
    _dn(app, "hoc.sinh@example.com", mk="sai-roi-nhe")
    with app.stream("GET", f"/admin/api/luong?sau={moc}&mot_lan=1") as r:
        assert r.headers["content-type"].startswith("text/event-stream")
        noi_dung = "".join(r.iter_text())
    du_lieu = [json.loads(dong[6:]) for dong in noi_dung.splitlines() if dong.startswith("data: {") and '"su_kien"' in dong]
    assert [d["su_kien"] for d in du_lieu] == ["dang_nhap_sai"] and du_lieu[0]["email"] == "hoc.sinh@example.com"


def test_xoa_nhat_ky_cu_hon_90_ngay(csdl_tam):
    nhat_ky.ghi("dang_nhap", None, email="cu@x.com", ip="1.1.1.1", ua="")
    nhat_ky.ghi("dang_nhap", None, email="moi@x.com", ip="1.1.1.1", ua="")
    with db.ket_noi() as con:
        con.execute("UPDATE activity_log SET ts = ts - 91 * 86400 WHERE email = 'cu@x.com'")
    assert nhat_ky.don_dep() == 1 and [d["email"] for d in nhat_ky.danh_sach()] == ["moi@x.com"]


@pytest.mark.parametrize("ua, mong", [
    (UA_WIN, "Chrome · Windows"), (UA_IPHONE, "Safari · iPhone"),
    ("Mozilla/5.0 (Linux; Android 14; SM-A546E) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0 Mobile Safari/537.36", "Chrome · Android (điện thoại)"),
    ("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0 Safari/537.36 Edg/141.0", "Edge · macOS"),
    ("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) coc_coc_browser/130.0 Chrome/130.0 Safari/537.36", "Cốc Cốc · Windows"),
    ("Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Mobile/15E148 Zalo iOS/450", "Zalo · iPhone"),
    ("", "không rõ"),
])
def test_thiet_bi_tu_user_agent(ua, mong):
    assert nhat_ky.thiet_bi(ua) == mong


# ================================================================ admin_cli.py
@pytest.fixture
def cli(app, monkeypatch):
    import admin_cli
    from rich.console import Console

    ra = Console(record=True, width=220, force_terminal=False)
    monkeypatch.setattr(admin_cli, "con_sole", ra)

    def chay(*args, nhap="y"):
        monkeypatch.setattr("builtins.input", lambda *_: nhap)
        ma = admin_cli.main(list(args))
        return ma, ra.export_text(clear=True)

    return chay


def test_cli_users_online_log_user(app, cli):
    _dn(app, "hoc.sinh@example.com", ua=UA_IPHONE)
    app.post("/api/doi-ten", json={"ten": "Tên Mới Nè"}, headers=_h(app))
    _dn(app, "admin@example.com", mk="sai-ne-sai-ne")
    ma, ra = cli("users")
    assert ma == 0 and "hoc.sinh@example.com" in ra and "thành viên" in ra and "admin" in ra and "Tên Mới Nè" in ra
    ma, ra = cli("online")
    assert "hoc.sinh@example.com" in ra and "Safari · iPhone" in ra
    ma, ra = cli("log", "--type", "fail", "--today")
    assert "Đăng nhập sai" in ra and "admin@example.com" in ra and "hoc.sinh" not in ra
    ma, ra = cli("log", "--user", "hoc.sinh@example.com", "--limit", "5")
    assert "Đổi tên" in ra and "Đăng nhập" in ra and "admin@example.com" not in ra
    assert cli("log", "--type", "bay-ba")[0] == 2
    ma, ra = cli("user", "hoc.sinh@example.com")
    assert ma == 0 and "Lịch sử đăng nhập" in ra and "Học Sinh" in ra and "Tên Mới Nè" in ra and "Bài đã giải: 0" in ra
    assert cli("user", "khong-co@x.com")[0] == 1


def test_cli_lock_unlock_hoi_xac_nhan(app, cli):
    ma, ra = cli("lock", "hoc.sinh@example.com", nhap="n")
    assert ma == 1 and "Đã huỷ" in ra and not db.lay_nguoi_dung(app.thuong.id).bi_khoa
    ma, ra = cli("lock", "hoc.sinh@example.com", nhap="y")
    assert ma == 0 and db.lay_nguoi_dung(app.thuong.id).bi_khoa
    assert _dn(app, "hoc.sinh@example.com").status_code == 403
    assert cli("lock", "admin@example.com")[0] == 1  # không khoá admin
    assert cli("unlock", "hoc.sinh@example.com")[0] == 0 and _dn(app, "hoc.sinh@example.com").json()["ok"]
    hd = nhat_ky.danh_sach(loai="admin")
    assert [d["su_kien"] for d in hd] == ["mo_khoa", "khoa"] and hd[0]["phuong_thuc"] == "cli" and hd[0]["ten"] == "admin (dòng lệnh)"


def test_cli_suspicious_va_live(app, cli, monkeypatch):
    ma, ra = cli("suspicious")
    assert "Không có dấu hiệu" in ra
    for _ in range(3):
        _dn(app, "hoc.sinh@example.com", mk="sai-mat-khau")
    ma, ra = cli("suspicious")
    assert "Đăng nhập sai nhiều lần" in ra and "hoc.sinh@example.com" in ra
    # live: sự kiện xảy ra trong lúc đang theo dõi được in ngay
    import admin_cli

    lan = {"n": 0}

    def ngu(_giay):
        lan["n"] += 1
        if lan["n"] == 1:
            _dn(app, "hoc.sinh@example.com", ua=UA_IPHONE)

    monkeypatch.setattr(admin_cli.time, "sleep", ngu)
    ma, ra = cli("live", "--so-vong", "3")
    dong = [d for d in ra.splitlines() if "hoc.sinh@example.com" in d]
    assert ma == 0 and len(dong) == 1 and "Đăng nhập" in dong[0] and "Safari · iPhone" in dong[0] and IP_KHACH in dong[0]


def test_cli_help_tieng_viet(capsys):
    import admin_cli

    with pytest.raises(SystemExit):
        admin_cli.main(["--help"])
    ra = capsys.readouterr().out
    assert "Cách dùng:" in ra and "theo dõi trực tiếp" in ra and "suspicious" in ra
    with pytest.raises(SystemExit):
        admin_cli.main(["lock", "--help"])
    ra = capsys.readouterr().out
    assert "Cách dùng: admin_cli.py lock" in ra and "xác nhận y/n" in ra and "Cách dùng: Cách dùng" not in ra
