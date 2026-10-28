"""IP thật của người dùng (get_client_ip: CF-Connecting-IP → X-Forwarded-For → X-Real-IP, chỉ tin proxy tin cậy) + vị trí / nhà mạng
tra offline + hiển thị trên /admin, admin_cli.py + cảnh báo đăng nhập từ nơi khác thường lệ."""
import sqlite3
import time
from types import SimpleNamespace

import pytest
from starlette.datastructures import Headers

from backend import db, dia_chi_ip, nhat_ky
from backend.bao_mat import bam_mat_khau
from backend.config import settings
from backend.dia_chi_ip import KHONG_XAC_DINH, get_client_ip

MK = "mat-khau-an-toan-123"
CF = "172.70.10.20"  # nằm trong dải IP Cloudflare (172.64.0.0/13)


def _yc(truc_tiep, **header):
    return SimpleNamespace(client=SimpleNamespace(host=truc_tiep) if truc_tiep else None,
                           headers=Headers({k.replace("_", "-"): v for k, v in header.items()}))


@pytest.fixture(autouse=True)
def _mac_dinh(monkeypatch):
    monkeypatch.setattr(settings, "trusted_proxies", "127.0.0.1,::1,cloudflare")


# ================================================================ get_client_ip
def test_qua_cloudflare_tunnel_lay_cf_connecting_ip():
    # cloudflared kết nối từ 127.0.0.1, luôn gửi kèm X-Forwarded-For
    yc = _yc("127.0.0.1", cf_connecting_ip="113.161.1.2", x_forwarded_for="113.161.1.2")
    assert get_client_ip(yc) == "113.161.1.2"
    assert get_client_ip(_yc(CF, cf_connecting_ip="2405:4802:71b6:f0a0::1")) == "2405:4802:71b6:f0a0::1"  # Cloudflare → máy chủ


def test_thu_tu_uu_tien_cf_roi_x_forwarded_for_roi_x_real_ip():
    assert get_client_ip(_yc(CF, cf_connecting_ip="1.2.3.4", x_forwarded_for="5.6.7.8", x_real_ip="9.9.9.9")) == "1.2.3.4"
    assert get_client_ip(_yc(CF, x_forwarded_for="5.6.7.8", x_real_ip="9.9.9.9")) == "5.6.7.8"
    assert get_client_ip(_yc(CF, x_real_ip="9.9.9.9")) == "9.9.9.9"
    assert get_client_ip(_yc(CF)) == CF  # proxy không gửi header nào → IP kết nối trực tiếp


def test_truy_cap_thang_bo_qua_header_gia():
    gia = {"cf_connecting_ip": "8.8.8.8", "x_forwarded_for": "8.8.8.8", "x_real_ip": "8.8.8.8"}
    assert get_client_ip(_yc("203.0.113.7", **gia)) == "203.0.113.7"   # người dùng gọi thẳng máy chủ
    assert get_client_ip(_yc("192.168.1.9", **gia)) == "192.168.1.9"   # máy khác trong mạng nhà
    # mở thẳng trên máy chạy app (localhost:8000) kèm CF-Connecting-IP giả, không có X-Forwarded-For → không phải proxy → bỏ qua
    assert get_client_ip(_yc("127.0.0.1", cf_connecting_ip="8.8.8.8")) == "127.0.0.1"
    assert get_client_ip(_yc("::1", x_real_ip="8.8.8.8")) == "::1"


def test_x_forwarded_for_khong_bi_lua_bang_ip_chen_vao_dau():
    # người dùng tự gửi "X-Forwarded-For: 1.1.1.1", proxy nối IP thật vào cuối
    assert get_client_ip(_yc("127.0.0.1", x_forwarded_for="1.1.1.1, 113.161.1.2")) == "113.161.1.2"
    # chuỗi proxy tin cậy: bỏ qua từ phải sang trái
    assert get_client_ip(_yc("127.0.0.1", x_forwarded_for="113.161.1.2, 172.70.1.1")) == "113.161.1.2"
    assert get_client_ip(_yc("127.0.0.1", x_forwarded_for="113.161.1.2 , 127.0.0.1")) == "113.161.1.2"


def test_ip_khong_hop_le_ghi_khong_xac_dinh():
    assert get_client_ip(_yc(CF, cf_connecting_ip="<script>")) == KHONG_XAC_DINH
    assert get_client_ip(_yc("127.0.0.1", x_forwarded_for="abc, 1.2.3.4.5")) == KHONG_XAC_DINH
    assert get_client_ip(_yc("testclient")) == KHONG_XAC_DINH
    assert get_client_ip(_yc(None)) == KHONG_XAC_DINH
    assert get_client_ip(None) == ""


def test_chuan_hoa_ipv4_ipv6_cong():
    assert get_client_ip(_yc(CF, cf_connecting_ip="2405:4802:00:0::0001")) == "2405:4802::1"
    assert get_client_ip(_yc(CF, cf_connecting_ip="::ffff:113.161.1.2")) == "113.161.1.2"
    assert get_client_ip(_yc(CF, x_forwarded_for="113.161.1.2:51234")) == "113.161.1.2"
    assert get_client_ip(_yc(CF, x_forwarded_for="[2405:4802::1]:443")) == "2405:4802::1"


def test_trusted_proxies_trong_env(monkeypatch):
    yc = _yc("10.20.30.40", x_forwarded_for="113.161.1.2")  # vd Render: proxy nội bộ 10.x
    assert get_client_ip(yc) == "10.20.30.40"
    monkeypatch.setattr(settings, "trusted_proxies", "127.0.0.1, ::1, 10.0.0.0/8, cloudflare, khong-phai-ip")
    assert get_client_ip(yc) == "113.161.1.2"
    monkeypatch.setattr(settings, "trusted_proxies", "10.0.0.0/8")  # bỏ "cloudflare" → không tin Cloudflare nữa
    assert get_client_ip(_yc(CF, cf_connecting_ip="1.2.3.4")) == CF


# ================================================================ vị trí + nhà mạng (offline)
class _DocGia:
    def __init__(self, du_lieu, loai="GeoLite2-City"):
        self.du_lieu, self.loai = du_lieu, loai

    def get(self, ip):
        return self.du_lieu.get(ip)

    def metadata(self):
        return SimpleNamespace(database_type=self.loai)


@pytest.fixture
def geo(monkeypatch):
    city = _DocGia({
        "113.161.1.2": {"country": {"iso_code": "VN"}, "subdivisions": [{"names": {"en": "Hanoi"}}], "city": {"names": {"en": "Hanoi"}}},
        "171.224.1.1": {"country": {"iso_code": "VN"}, "subdivisions": [{"names": {"en": "Ho Chi Minh"}}],
                        "city": {"names": {"en": "Thu Duc"}}},
        "42.112.1.1": {"country": {"iso_code": "VN"}, "subdivisions": [{"names": {"en": "Thua Thien-Hue"}}]},
        "14.1.1.1": {"country": {"iso_code": "VN"}, "subdivisions": [{"names": {"en": "Da Nang"}}]},
        "8.8.8.8": {"country": {"iso_code": "US", "names": {"en": "United States"}}, "city": {"names": {"en": "Mountain View"}}},
        "103.1.1.1": {"country": {"iso_code": "SG", "names": {"en": "Singapore"}}, "city": {"names": {"en": "Singapore"}}},
    })
    asn = _DocGia({"113.161.1.2": {"autonomous_system_organization": "VNPT Corp"},
                   "171.224.1.1": {"autonomous_system_organization": "Viettel Group"},
                   "42.112.1.1": {"autonomous_system_organization": "FPT Telecom Company"},
                   "8.8.8.8": {"autonomous_system_organization": "Google LLC"},
                   "103.1.1.1": {"autonomous_system_organization": "Some Hosting Pte. Ltd."}}, "GeoLite2-ASN")
    monkeypatch.setattr(dia_chi_ip, "_doc", lambda loai: city if loai == "city" else asn)
    dia_chi_ip.tra_ip.cache_clear()
    yield
    dia_chi_ip.tra_ip.cache_clear()


def test_vi_tri_tieng_viet_va_nha_mang(geo):
    assert dia_chi_ip.mo_ta("113.161.1.2") == "113.161.1.2 · Hà Nội · VNPT"
    assert dia_chi_ip.mo_ta("171.224.1.1") == "171.224.1.1 · TP.HCM · Viettel"
    assert dia_chi_ip.mo_ta("42.112.1.1") == "42.112.1.1 · Thừa Thiên Huế · FPT"
    assert dia_chi_ip.mo_ta("8.8.8.8") == "8.8.8.8 · Mountain View, Hoa Kỳ · Google"
    assert dia_chi_ip.mo_ta("103.1.1.1") == "103.1.1.1 · Singapore · Some Hosting Pte"
    assert dia_chi_ip.mo_ta("1.0.0.9") == "1.0.0.9"  # không có trong cơ sở dữ liệu
    assert "GeoLite2" in dia_chi_ip.nguon_du_lieu()


def test_ip_noi_bo_va_ghi_truoc_khi_sua(geo):
    for ip in ("192.168.1.5", "10.0.0.3", "127.0.0.1", "::1", "172.16.4.4"):
        assert dia_chi_ip.mo_ta(ip) == f"{ip} · mạng nội bộ", ip
        assert dia_chi_ip.thong_tin(ip)["noi_bo"]
    assert dia_chi_ip.mo_ta("127.0.0.1", cu=True) == "127.0.0.1 · ghi trước khi sửa"
    assert dia_chi_ip.thong_tin("127.0.0.1", cu=True)["cu"]
    assert dia_chi_ip.mo_ta(KHONG_XAC_DINH) == KHONG_XAC_DINH and dia_chi_ip.mo_ta("") == ""


def test_thieu_file_mmdb_thi_bo_qua_khong_loi(monkeypatch, tmp_path):
    monkeypatch.setattr(settings, "geoip_city_db", str(tmp_path / "khong-co.mmdb"))
    monkeypatch.setattr(settings, "geoip_asn_db", str(tmp_path / "khong-co-asn.mmdb"))
    monkeypatch.setattr(dia_chi_ip, "_lan_kiem", 0.0)
    dia_chi_ip.tra_ip.cache_clear()
    assert dia_chi_ip.tra_ip("113.161.1.2")["vi_tri"] == "" and dia_chi_ip.mo_ta("113.161.1.2") == "113.161.1.2"
    assert dia_chi_ip.nguon_du_lieu() == ""
    (tmp_path / "hong.mmdb").write_bytes(b"khong phai mmdb")  # file hỏng cũng bỏ qua
    monkeypatch.setattr(settings, "geoip_city_db", str(tmp_path / "hong.mmdb"))
    monkeypatch.setattr(dia_chi_ip, "_lan_kiem", 0.0)
    dia_chi_ip.tra_ip.cache_clear()
    assert dia_chi_ip.mo_ta("113.161.1.2") == "113.161.1.2"
    monkeypatch.setattr(dia_chi_ip, "_lan_kiem", 0.0)
    dia_chi_ip.tra_ip.cache_clear()


def test_ten_nha_mang():
    assert [dia_chi_ip.ten_nha_mang(x) for x in ("VNPT Corp", "Viettel Group", "FPT Telecom Company", "MOBIFONE Corporation",
                                                 "CMC Telecom", "SaiGon Tourist cable Televition", "Google LLC")] == \
        ["VNPT", "Viettel", "FPT", "MobiFone", "CMC", "SCTV", "Google"]


# ================================================================ app: nhật ký ghi đúng IP thật
def _h(c, **them):
    return {"X-CSRF-Token": c.get("/api/toi", headers=them).json()["csrf"], **them}


def _may(ip_truc_tiep):
    from fastapi.testclient import TestClient

    from backend.main import app
    return TestClient(app, base_url="https://testserver", client=(ip_truc_tiep, 40000))


@pytest.fixture
def ung_dung(client, hop_thu, monkeypatch, geo):
    monkeypatch.setattr(settings, "google_client_id", "")
    monkeypatch.setattr(settings, "google_client_secret", "")
    hs = db.tao_tai_khoan_email("hoc.sinh@example.com", "Học Sinh", bam_mat_khau(MK), da_xac_thuc=True)
    ad = db.tao_tai_khoan_email("admin@example.com", "Quản Trị", bam_mat_khau(MK), da_xac_thuc=True)
    db.dat_admin(ad.id, True)
    return SimpleNamespace(hs=hs, ad=ad)


def _dang_nhap(c, email, **h):
    return c.post("/auth/dang-nhap-mat-khau", json={"email": email, "mat_khau": MK}, headers=_h(c, **h))


def test_nhat_ky_ghi_ip_that_qua_tunnel_kem_vi_tri(ung_dung):
    tunnel = _may("127.0.0.1")  # cloudflared → app
    qua_cf = {"CF-Connecting-IP": "113.161.1.2", "X-Forwarded-For": "113.161.1.2", "X-Forwarded-Proto": "https"}
    assert _dang_nhap(tunnel, "hoc.sinh@example.com", **qua_cf).json()["ok"]
    d = nhat_ky.danh_sach(loai="login")[0]
    assert d["ip"] == "113.161.1.2" and d["ip_mo_ta"] == "113.161.1.2 · Hà Nội · VNPT" and not d["ip_info"]["cu"]
    with db.ket_noi() as con:
        r = con.execute("SELECT ip_v, geo_qg, geo_noi, geo_mang FROM activity_log WHERE id = ?", (d["id"],)).fetchone()
    assert tuple(r) == (1, "VN", "Hà Nội", "VNPT")  # lưu lúc ghi, không phải tra lại
    assert db.lay_nguoi_dung(ung_dung.hs.id).ip_dang_ky == "" and nhat_ky.online()[0]["ip"] == "113.161.1.2"
    # sai mật khẩu từ điện thoại 4G (Viettel) → IP khác
    tunnel2 = _may("127.0.0.1")
    tunnel2.post("/auth/dang-nhap-mat-khau", json={"email": "hoc.sinh@example.com", "mat_khau": "sai-sai-sai"},
                 headers=_h(tunnel2, **{"CF-Connecting-IP": "171.224.1.1", "X-Forwarded-For": "171.224.1.1"}))
    sai = nhat_ky.danh_sach(loai="fail")[0]
    assert sai["ip_mo_ta"] == "171.224.1.1 · TP.HCM · Viettel"


def test_localhost_kem_cf_connecting_ip_gia_bi_bo_qua(ung_dung):
    c = _may("127.0.0.1")
    assert _dang_nhap(c, "hoc.sinh@example.com", **{"CF-Connecting-IP": "8.8.8.8"}).json()["ok"]
    d = nhat_ky.danh_sach(loai="login")[0]
    assert d["ip"] == "127.0.0.1" and d["ip_mo_ta"] == "127.0.0.1 · mạng nội bộ"
    c2 = _may("203.0.113.9")  # gọi thẳng máy chủ từ Internet, giả cả 3 header
    _dang_nhap(c2, "admin@example.com", **{"CF-Connecting-IP": "8.8.8.8", "X-Forwarded-For": "8.8.8.8", "X-Real-IP": "8.8.8.8"})
    assert nhat_ky.danh_sach(loai="login")[0]["ip"] == "203.0.113.9"


def test_gioi_han_dang_ky_theo_ip_that(client, hop_thu, monkeypatch):
    monkeypatch.setattr(settings, "google_client_id", "")
    monkeypatch.setattr(settings, "google_client_secret", "")
    tunnel = _may("127.0.0.1")

    def dk(i, ip):
        h = {"CF-Connecting-IP": ip, "X-Forwarded-For": ip}
        return tunnel.post("/auth/dang-ky", json={"email": f"hs{i}@example.com", "mat_khau": MK, "ten": f"HS {i}"}, headers=_h(tunnel, **h))

    for i in range(3):
        assert dk(i, "113.161.1.2").status_code == 200
    r = dk(3, "113.161.1.2")
    assert r.status_code == 429 and "3 tài khoản" in r.json()["loi"]
    assert dk(4, "171.224.1.1").status_code == 200  # người khác (IP khác) qua cùng tunnel 127.0.0.1 vẫn đăng ký được


# ================================================================ /admin: bấm IP → mọi tài khoản dùng IP đó
def test_admin_xem_tai_khoan_theo_ip(ung_dung):
    tunnel = _may("127.0.0.1")
    vnpt = {"CF-Connecting-IP": "113.161.1.2", "X-Forwarded-For": "113.161.1.2"}
    _dang_nhap(tunnel, "hoc.sinh@example.com", **vnpt)
    tunnel.post("/auth/dang-nhap-mat-khau", json={"email": "la@example.com", "mat_khau": "sai-sai-sai"}, headers=_h(tunnel, **vnpt))
    ad = _may("127.0.0.1")
    _dang_nhap(ad, "admin@example.com", **vnpt)
    d = ad.get("/admin/api/ip", params={"ip": "113.161.1.2"}, headers=vnpt).json()
    assert d["ip_info"]["mo_ta"] == "113.161.1.2 · Hà Nội · VNPT"
    assert {x["email"] for x in d["tai_khoan"]} == {"hoc.sinh@example.com", "admin@example.com"}
    assert d["dang_nhap_sai"][0]["email"] == "la@example.com" and d["gan_day"] and "GeoLite2" in d["nguon_vi_tri_ip"]
    assert ad.get("/admin/api/ip", params={"ip": "khong-phai-ip"}, headers=vnpt).status_code == 400
    assert ad.get("/admin/api/hoat-dong", params={"ip": "113.161.1.2"}, headers=vnpt).json()["su_kien"]
    bang = {x["email"]: x for x in ad.get("/admin/api/nguoi-dung", headers=vnpt).json()["nguoi_dung"]}
    assert bang["hoc.sinh@example.com"]["ip_cuoi_info"]["mo_ta"] == "113.161.1.2 · Hà Nội · VNPT"
    hs = _may("127.0.0.1")
    _dang_nhap(hs, "hoc.sinh@example.com", **vnpt)
    assert hs.get("/admin/api/ip", params={"ip": "113.161.1.2"}, headers=vnpt).status_code == 403


# ================================================================ cảnh báo: đăng nhập từ nơi khác thường lệ
def _ghi_dang_nhap(uid, ip, luc):
    t = dia_chi_ip.tra_ip(ip)
    with db.ket_noi() as con:
        return con.execute("INSERT INTO activity_log (ts, event, user_id, email, name, method, ip, ip_v, geo_qg, geo_noi, geo_mang) "
                           "VALUES (?, 'dang_nhap', ?, 'hoc.sinh@example.com', 'Học Sinh', 'email', ?, 1, ?, ?, ?)",
                           (luc, uid, ip, t["qg"], t["vi_tri"], t["nha_mang"])).lastrowid


def test_canh_bao_dang_nhap_tu_quoc_gia_va_tinh_thanh_khac_thuong_le(ung_dung):
    uid, bay_gio = ung_dung.hs.id, time.time()
    for k in range(4):
        _ghi_dang_nhap(uid, "113.161.1.2", bay_gio - 20 * 86400 + k)  # thường lệ: Hà Nội
    binh_thuong = _ghi_dang_nhap(uid, "113.161.1.2", bay_gio - 3600)
    tinh_khac = _ghi_dang_nhap(uid, "14.1.1.1", bay_gio - 1800)       # Đà Nẵng
    nuoc_khac = _ghi_dang_nhap(uid, "103.1.1.1", bay_gio - 60)        # Singapore
    cb = nhat_ky.canh_bao()
    la = {x["id"]: x for x in cb["vi_tri_la"]}
    assert set(la) == {tinh_khac, nuoc_khac} and binh_thuong not in la
    assert la[nuoc_khac]["loai"] == "quoc_gia" and la[nuoc_khac]["noi"] == "Singapore" and la[nuoc_khac]["thuong_le"] == "Việt Nam"
    assert la[tinh_khac]["loai"] == "thanh_pho" and la[tinh_khac]["noi"] == "Đà Nẵng" and la[tinh_khac]["thuong_le"] == "Hà Nội"
    assert cb["tong"] >= 2
    dong = {d["id"]: d for d in nhat_ky.danh_sach(loai="login")}
    assert "vi_tri_la" in dong[nuoc_khac]["co"] and "vi_tri_la" not in dong[binh_thuong]["co"]


def test_nguoi_moi_chua_co_thuong_le_thi_khong_canh_bao(ung_dung):
    _ghi_dang_nhap(ung_dung.hs.id, "113.161.1.2", time.time() - 100)
    _ghi_dang_nhap(ung_dung.hs.id, "103.1.1.1", time.time() - 50)
    assert nhat_ky.canh_bao()["vi_tri_la"] == []


# ================================================================ IP cũ (ghi trước khi sửa) + nâng cấp CSDL cũ
def test_csdl_cu_them_cot_va_ip_127_ghi_truoc_khi_sua(tmp_path, monkeypatch):
    duong = tmp_path / "cu.db"
    con = sqlite3.connect(duong)
    con.executescript("""
        CREATE TABLE activity_log (id INTEGER PRIMARY KEY AUTOINCREMENT, ts REAL NOT NULL, event TEXT NOT NULL, user_id INTEGER,
          target_id INTEGER, email TEXT NOT NULL DEFAULT '', name TEXT NOT NULL DEFAULT '', method TEXT NOT NULL DEFAULT '',
          ip TEXT NOT NULL DEFAULT '', user_agent TEXT NOT NULL DEFAULT '', device TEXT NOT NULL DEFAULT '', detail TEXT NOT NULL DEFAULT '');
    """)
    con.execute("INSERT INTO activity_log (ts, event, user_id, email, ip) VALUES (?, 'khoa', 1, '', '127.0.0.1')", (time.time(),))
    con.commit()
    con.close()
    monkeypatch.setattr(db, "DUONG_DAN", duong)
    d = nhat_ky.danh_sach()[0]
    assert d["ip"] == "127.0.0.1" and d["ip_mo_ta"] == "127.0.0.1 · ghi trước khi sửa" and d["ip_info"]["cu"]  # giữ nguyên IP cũ
    nhat_ky.ghi("dang_nhap", ip="127.0.0.1", email="x@example.com")
    moi = nhat_ky.danh_sach()[0]
    assert moi["ip_mo_ta"] == "127.0.0.1 · mạng nội bộ" and not moi["ip_info"]["cu"]


# ================================================================ admin_cli.py
def test_admin_cli_hien_vi_tri_va_lenh_ip(ung_dung, capsys):
    import admin_cli

    tunnel = _may("127.0.0.1")
    _dang_nhap(tunnel, "hoc.sinh@example.com", **{"CF-Connecting-IP": "171.224.1.1", "X-Forwarded-For": "171.224.1.1"})
    admin_cli.con_sole.width = 220
    assert admin_cli.main(["log", "--type", "login"]) == 0
    assert "171.224.1.1 · TP.HCM · Viettel" in capsys.readouterr().out
    assert admin_cli.main(["ip", "171.224.1.1"]) == 0
    ra = capsys.readouterr().out
    assert "hoc.sinh@example.com" in ra and "TP.HCM" in ra and "Viettel" in ra
    assert admin_cli.main(["ip", "khong-phai-ip"]) == 2
    assert admin_cli.main(["users"]) == 0 and "171.224.1.1 · TP.HCM · Viettel" in capsys.readouterr().out
