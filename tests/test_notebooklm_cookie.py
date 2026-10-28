"""Cookie Google cho NotebookLM: chuẩn hoá mọi định dạng (normalize_cookies), bắt buộc đủ cookie, mã hoá AES-256-GCM,
API admin không còn lỗi 422 do lệch tên trường, lỗi theo mẫu {code, message, missing}, KHÔNG lộ giá trị cookie.
Toàn bộ là cookie GIẢ."""
import json
import logging
from unittest.mock import patch

import pytest

from backend import bao_mat, db
from backend.config import settings
from services.notebooklm import manager
from services.notebooklm.cookie import LoiCookie, chuan_hoa_va_kiem_tra, normalize_cookies
from services.notebooklm.ma_hoa import TIEN_TO, giai_ma, ma_hoa

DU = {"SID": "gia-sid-111", "HSID": "gia-hsid-222", "SSID": "gia-ssid-333", "APISID": "gia-apisid-444",
      "SAPISID": "gia-sapisid-555", "__Secure-1PSID": "gia-1psid-666", "NID": "gia-nid-777"}
CHUOI = "; ".join(f"{k}={v}" for k, v in DU.items())
GIA_TRI = list(DU.values())


@pytest.fixture(autouse=True)
def khoa_va_bang(monkeypatch):
    monkeypatch.setattr(settings, "notebooklm_cookie_key", "khoa-thu-nghiem-chi-dung-cho-test-0123456789")
    with db.ket_noi() as con:
        con.execute("DELETE FROM notebooklm_cache")
        con.execute("DELETE FROM notebooklm_accounts")
    yield


# ---------------------------------------------------------------- normalize_cookies: 6 trường hợp bắt buộc
def test_1_chuoi_header_chuan():
    chuoi, d = normalize_cookies(CHUOI)
    assert d == DU and chuoi == CHUOI


def test_2_co_tien_to_cookie():
    for tien_to in ("cookie: ", "Cookie: ", "COOKIE:"):
        assert normalize_cookies(tien_to + CHUOI)[1] == DU
    assert normalize_cookies('"cookie: ' + CHUOI + '"\n')[1] == DU          # ngoặc kép + xuống dòng thừa


def test_3_json_cookie_editor_chi_lay_google():
    xuat = [{"name": k, "value": v, "domain": ".google.com", "path": "/", "secure": True} for k, v in DU.items()]
    xuat += [{"name": "SID", "value": "cua-youtube", "domain": ".youtube.com"},          # domain khác → bỏ
             {"name": "OSID", "value": "gia-osid", "domain": "notebooklm.google.com"},  # notebooklm.google.com → nhận
             {"name": "NB", "value": "gia-nb", "domain": "notebook.google.com"}]
    d = normalize_cookies(json.dumps(xuat))[1]
    assert d["SID"] == "gia-sid-111" and d["OSID"] == "gia-osid" and d["NB"] == "gia-nb" and len(d) == len(DU) + 2


def test_4_json_object():
    assert normalize_cookies(json.dumps(DU))[1] == DU


def test_5_chuoi_bi_xuong_dong_giua_chung():
    gay = "SID=gia-sid-1\n11; HSID=gia-hsid-222;\nSSID=gia-ssid-333; APISID=gia-api\nsid-444; SAPISID=gia-sapisid-555\n__Secure-1PSID=gia-1psid-666; NID=gia-nid-777"
    assert normalize_cookies(gay)[1] == DU


def test_6_chi_mot_gia_tri_le_bao_loi_dung():
    with pytest.raises(LoiCookie) as e:
        normalize_cookies("g.a000xYz-gia-tri-le-chi-co-mot-cookie")
    assert e.value.code == "COOKIE_VALUE_ONLY"
    assert str(e.value) == "Bạn mới dán giá trị của một cookie. Cần dán cả chuỗi dạng tên=giá_trị; tên=giá_trị."


def test_trung_ten_giu_gia_tri_cuoi_va_khoi_request_headers():
    assert normalize_cookies("SID=cu; HSID=a; SID=moi")[1]["SID"] == "moi"
    khoi = "GET / HTTP/2\nhost: notebooklm.google.com\naccept: text/html\ncookie: " + CHUOI + "\nuser-agent: Mozilla/5.0"
    assert normalize_cookies(khoi)[1] == DU
    curl = f"curl 'https://notebooklm.google.com/' -H 'accept: */*' -H 'cookie: {CHUOI}'"
    assert normalize_cookies(curl)[1] == DU


def test_thieu_cookie_bat_buoc_va_khong_lo_gia_tri():
    with pytest.raises(LoiCookie) as e:
        chuan_hoa_va_kiem_tra("SID=gia-sid-111; HSID=gia-hsid-222; APISID=x; SAPISID=y")
    assert e.value.code == "MISSING_COOKIES" and e.value.status_code == 422
    assert e.value.missing == ["SSID", "__Secure-1PSID hoặc __Secure-3PSID"]
    assert str(e.value).startswith("Cookie thiếu: SSID, __Secure-1PSID hoặc __Secure-3PSID. Hãy copy TOÀN BỘ giá trị dòng 'cookie'")
    assert "gia-sid-111" not in str(e.value)
    chuan_hoa_va_kiem_tra(CHUOI.replace("__Secure-1PSID", "__Secure-3PSID"))    # 3PSID thay cho 1PSID cũng được


def test_qua_dai():
    with pytest.raises(LoiCookie) as e:
        normalize_cookies("SID=" + "x" * 20_001)
    assert e.value.code == "COOKIE_TOO_LONG"


# ---------------------------------------------------------------- mã hoá AES-256-GCM
def test_aes_gcm_va_doc_duoc_du_lieu_cu(monkeypatch):
    bm = ma_hoa(CHUOI)
    assert bm.startswith(TIEN_TO) and "gia-sid" not in bm and ma_hoa(CHUOI) != bm   # nonce ngẫu nhiên
    assert giai_ma(bm) == CHUOI
    assert giai_ma(bao_mat.ma_hoa_cookie_notebooklm(CHUOI)) == CHUOI                   # bản cũ (Fernet) vẫn đọc được
    monkeypatch.setattr(settings, "notebooklm_cookie_key", "khoa-khac-hoan-toan-cung-du-32-ky-tu-nhe")
    with pytest.raises(Exception) as e:
        giai_ma(bm)
    assert getattr(e.value, "code", "") == "DECRYPT_FAILED"
    monkeypatch.setattr(settings, "notebooklm_cookie_key", "")
    with pytest.raises(Exception) as e:
        ma_hoa(CHUOI)
    assert getattr(e.value, "code", "") == "ENCRYPTION_KEY_MISSING" and e.value.status_code == 503


# ---------------------------------------------------------------- API admin
@pytest.fixture
def admin(client, dang_nhap):
    csrf = dang_nhap(client, "nlm-admin", "admin.nlm@vi-du.vn")
    db.dat_admin(db.tim_theo_email("admin.nlm@vi-du.vn").id, True)
    return {"X-CSRF-Token": csrf}


def _khong_lo(r):
    van_ban = r.text
    for gt in GIA_TRI:
        assert gt not in van_ban, "phản hồi chứa giá trị cookie!"


@patch("services.notebooklm.client.NotebookLMClient.list_notebooks", return_value=[])
@patch("services.notebooklm.client.NotebookLMClient.kiem_tra_ket_noi")
def test_luu_va_ket_noi_dung_du_lieu_giao_dien_gui(mock_kt, _ds, client, admin, caplog):
    mock_kt.return_value = (True, "", {"email": "Giao.Vien@Gmail.com", "picture": "https://lh3.googleusercontent.com/gia"})
    caplog.set_level(logging.DEBUG)
    # ĐÚNG như admin.js gửi: raw_cookie nguyên văn (có tiền tố, xuống dòng), email để trống
    r = client.post("/admin/api/notebooklm/accounts", headers=admin,
                    json={"ten_goi_nho": "Google Chính", "raw_cookie": "cookie: " + CHUOI + "\n", "email": "", "is_default": True})
    assert r.status_code == 200, r.text
    _khong_lo(r)
    acc = r.json()
    assert acc["email"] == "giao.vien@gmail.com" and acc["status"] == "dang_hoat_dong" and "cookie_enc" not in acc
    with db.ket_noi() as con:
        luu = con.execute("SELECT cookie_enc FROM notebooklm_accounts WHERE id = ?", (acc["id"],)).fetchone()[0]
    assert luu.startswith(TIEN_TO) and giai_ma(luu) == CHUOI
    # danh sách: chỉ trường an toàn
    ds = client.get("/admin/api/notebooklm/accounts").json()["tai_khoan"]
    assert set(ds[0]) <= {"id", "ten_goi_nho", "email", "status", "error_msg", "is_default", "so_notebook", "last_sync_at", "created_at"}
    # Kiểm tra kết nối → "Đã kết nối"
    kq = client.post(f"/admin/api/notebooklm/accounts/{acc['id']}/check", headers=admin).json()
    assert kq["ket_noi"] is True and kq["message"] == "Đã kết nối"
    # Sửa: dán cookie mới (JSON object) → được cập nhật thật (trước đây bị bỏ qua)
    moi = dict(DU, SID="gia-sid-moi-999")
    r = client.put(f"/admin/api/notebooklm/accounts/{acc['id']}", headers=admin, json={"raw_cookie": json.dumps(moi)})
    assert r.status_code == 200
    with db.ket_noi() as con:
        luu = con.execute("SELECT cookie_enc FROM notebooklm_accounts WHERE id = ?", (acc["id"],)).fetchone()[0]
    assert "gia-sid-moi-999" in giai_ma(luu)
    # nhật ký: chỉ tên + số lượng cookie
    nhat_ky = caplog.text
    assert "7 cookie" in nhat_ky and all(gt not in nhat_ky for gt in GIA_TRI + ["gia-sid-moi-999"])


@patch("services.notebooklm.client.NotebookLMClient.kiem_tra_ket_noi", return_value=(True, "", {}))
def test_loi_ro_rang_theo_mau_chung(_kt, client, admin):
    def gui(**than):
        return client.post("/admin/api/notebooklm/accounts", headers=admin, json={"ten_goi_nho": "X", **than})
    r = gui(raw_cookie="SID=gia-sid-111; HSID=gia-hsid-222")
    assert r.status_code == 422
    d = r.json()
    assert d["code"] == "MISSING_COOKIES" and d["missing"][:3] == ["SSID", "APISID", "SAPISID"] and d["message"].startswith("Cookie thiếu:")
    _khong_lo(r)
    r = gui(raw_cookie="g.a000-gia-tri-le")
    assert r.status_code == 422 and r.json()["code"] == "COOKIE_VALUE_ONLY"
    r = gui(raw_cookie=CHUOI, email="khong-phai-email")
    assert r.status_code == 422 and r.json()["message"] == "Email không hợp lệ."
    _khong_lo(r)
    r = gui(raw_cookie=CHUOI, ten_goi_nho=123456)                     # sai kiểu → lỗi gọn, KHÔNG trả lại dữ liệu đã gửi
    assert r.status_code == 422 and r.json()["code"] == "INVALID_INPUT"
    _khong_lo(r)
    r = gui(cookie=CHUOI)                                            # tên trường cũ vẫn nhận
    assert r.status_code == 200


def test_google_tu_choi_cookie_thi_khong_luu(client, admin):
    def kt_het_han(self):
        self.ly_do = "het_han"
        return False, "Cookie đã hết hạn hoặc phiên đăng nhập Google bị đăng xuất", {}
    with patch("services.notebooklm.client.NotebookLMClient.kiem_tra_ket_noi", kt_het_han):
        r = client.post("/admin/api/notebooklm/accounts", headers=admin, json={"raw_cookie": CHUOI})
    assert r.status_code == 422 and r.json()["code"] == "COOKIE_EXPIRED"
    assert r.json()["message"] == "Cookie đã hết hạn hoặc không hợp lệ. Hãy đăng nhập lại NotebookLM rồi lấy cookie mới."
    assert manager.ds_tai_khoan() == []


def test_chi_admin(client, dang_nhap):
    csrf = dang_nhap(client, "hoc-sinh", "hoc.sinh.nlm@vi-du.vn")
    h = {"X-CSRF-Token": csrf}
    assert client.get("/admin/api/notebooklm/accounts").status_code == 403
    assert client.post("/admin/api/notebooklm/accounts", headers=h, json={"raw_cookie": CHUOI}).status_code == 403
