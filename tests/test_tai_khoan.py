"""Đăng nhập + key Gemini riêng (BYOK): mã hoá key, chọn đúng key, giới hạn dùng thử, CSRF, giới hạn tần suất, quyền xem lịch sử.
Không gọi mạng: Google OAuth, Gemini và pipeline giải bài đều là hàm giả."""
import logging
import sqlite3
import threading
import time
from types import SimpleNamespace

import pytest

from backend import auth, bao_mat, db, llm, pipeline, tai_khoan
from backend.ai.providers.claude import chat as claude_chat
from backend.ai.providers.gemini import chat as gemini_chat, client as gemini_client
from collections import OrderedDict
from backend.bao_mat import GioiHanTanSuat, LoiBaoMat
from backend.config import AnKeyTrongLog, settings

KEY = "AIzaSyD-khoa-gia-cua-nguoi-dung-0123456789"
KEY_OK = {"hop_le": True, "het_han_muc": False, "thong_diep": "Key hợp lệ", "mo_hinh": "gemini-test"}


def _nguoi(sub="google-1", email="a@example.com") -> db.NguoiDung:
    return db.luu_nguoi_dung(sub, email, "A")


def _co_key(u: db.NguoiDung, key: str = KEY) -> db.NguoiDung:
    tai_khoan.luu_key(u, key, kiem_tra=lambda k: KEY_OK)
    return db.lay_nguoi_dung(u.id)


# ================================================================ mã hoá / giải mã key
def test_ma_hoa_roi_giai_ma_dung_key(cau_hinh):
    ban_ma = bao_mat.ma_hoa_key(KEY)
    assert KEY not in ban_ma and "khoa-gia" not in ban_ma
    assert bao_mat.giai_ma_key(ban_ma) == KEY
    assert bao_mat.ma_hoa_key(KEY) != ban_ma  # có nonce ngẫu nhiên: cùng key mỗi lần mã hoá một khác


def test_doi_khoa_chinh_thi_khong_giai_ma_duoc(cau_hinh, monkeypatch):
    ban_ma = bao_mat.ma_hoa_key(KEY)
    monkeypatch.setattr(settings, "app_secret_key", "z" * 48)
    with pytest.raises(LoiBaoMat, match="nhập lại key"):
        bao_mat.giai_ma_key(ban_ma)


def test_ban_ma_bi_sua_thi_bao_loi(cau_hinh):
    ban_ma = bao_mat.ma_hoa_key(KEY)
    sua = ban_ma[:-6] + ("A" if ban_ma[-6] != "A" else "B") + ban_ma[-5:]
    with pytest.raises(LoiBaoMat):
        bao_mat.giai_ma_key(sua)


@pytest.mark.parametrize("khoa_chinh", ["", "ngan-qua"])
def test_env_khong_co_khoa_thi_tu_tao_tep_khoa(cau_hinh, monkeypatch, csdl_tam, khoa_chinh):
    """.env chưa có APP_SECRET_KEY (hoặc quá ngắn) → app tự tạo data/app_secret.key (quyền 600) và dùng mãi khoá đó."""
    monkeypatch.setattr(settings, "app_secret_key", khoa_chinh)
    tep = bao_mat.TEP_KHOA_CHINH
    assert not tep.exists() and bao_mat.ma_hoa_san_sang() and tep.is_file()
    assert oct(tep.stat().st_mode & 0o777) == "0o600" and len(tep.read_text().strip()) >= 32
    ban_ma = bao_mat.ma_hoa_key(KEY)
    monkeypatch.setattr(bao_mat, "_khoa_tu_tep", {})  # như khởi động lại app: đọc lại từ file
    assert bao_mat.giai_ma_key(ban_ma) == KEY
    assert tai_khoan.luu_key(_nguoi(), KEY, kiem_tra=lambda k: KEY_OK)["ok"]


def test_khoa_trong_env_duoc_uu_tien(cau_hinh, monkeypatch):
    ban_ma = bao_mat.ma_hoa_key(KEY)  # khoá trong .env ("k" * 48)
    monkeypatch.setattr(settings, "app_secret_key", "")
    with pytest.raises(LoiBaoMat):  # khoá tự tạo khác khoá .env
        bao_mat.giai_ma_key(ban_ma)


def test_khong_tao_duoc_tep_khoa_thi_bao_loi(cau_hinh, monkeypatch, tmp_path):
    monkeypatch.setattr(settings, "app_secret_key", "")
    (tmp_path / "khong-ghi").mkdir(mode=0o500)
    monkeypatch.setattr(bao_mat, "TEP_KHOA_CHINH", tmp_path / "khong-ghi" / "app_secret.key")
    assert not bao_mat.ma_hoa_san_sang()
    goi = []
    with pytest.raises(tai_khoan.LoiTaiKhoan) as e:
        tai_khoan.luu_key(_nguoi(), KEY, kiem_tra=lambda k: goi.append(k) or KEY_OK)
    assert e.value.ma == "chua_co_khoa_chinh" and goi == []  # không tốn lượt gọi thử khi chắc chắn không lưu được


def test_csdl_chi_luu_ban_ma_va_4_ky_tu_cuoi(cau_hinh, csdl_tam):
    u = _co_key(_nguoi())
    assert u.key_cuoi == KEY[-4:] and u.co_key
    with sqlite3.connect(csdl_tam) as con:
        enc, last4 = con.execute("SELECT gemini_key_enc, gemini_key_last4 FROM users WHERE id = ?", (u.id,)).fetchone()
    assert KEY not in enc and last4 == KEY[-4:]
    assert bao_mat.giai_ma_key(enc) == KEY
    with sqlite3.connect(csdl_tam) as con:  # toàn bộ file CSDL không chứa key trần
        con.execute("PRAGMA wal_checkpoint(FULL)")
    assert KEY.encode() not in csdl_tam.read_bytes()


def test_key_bi_google_tu_choi_thi_khong_luu(cau_hinh):
    u = _nguoi()
    with pytest.raises(tai_khoan.LoiTaiKhoan, match="không hợp lệ"):
        tai_khoan.luu_key(u, KEY, kiem_tra=lambda k: {"hop_le": False, "thong_diep": "Key không hợp lệ"})
    assert not db.lay_nguoi_dung(u.id).co_key


@pytest.mark.parametrize("vao, ra", [(f"  {KEY}\n", KEY), (f'"{KEY}"', KEY), (f"GEMINI_API_KEY={KEY}", KEY)])
def test_chuan_hoa_key(vao, ra):
    assert tai_khoan.chuan_hoa_key(vao) == ra


@pytest.mark.parametrize("vao", ["", "ngan", "AIza có dấu cách ở giữa 1234567890", "x" * 300])
def test_key_sai_dinh_dang(vao):
    with pytest.raises(tai_khoan.LoiTaiKhoan):
        tai_khoan.chuan_hoa_key(vao)


def test_log_khong_ghi_key(caplog):
    rec = logging.LogRecord("x", logging.INFO, "f", 1, "gọi Gemini bằng %s thất bại", (KEY,), None)
    AnKeyTrongLog().filter(rec)
    assert KEY not in rec.getMessage() and "***" in rec.getMessage()


def test_khoa_ai_khong_lo_key_khi_in():
    k = llm.KhoaAI("ca_nhan", KEY, KEY[-4:])
    assert KEY not in repr(k) and KEY not in str(k) and KEY[-4:] in k.mo_ta


# ================================================================ chọn đúng key
def test_co_key_rieng_thi_luon_dung_key_rieng(cau_hinh):
    u = _co_key(_nguoi())
    lc = tai_khoan.chon_khoa_giai(u)
    assert lc.nguon == "ca_nhan" and lc.khoa.nguon == "ca_nhan" and lc.khoa.gemini_api_key == KEY and not lc.da_tru_luot
    assert db.lay_nguoi_dung(u.id).luot_da_dung == 0  # có key riêng thì không trừ lượt dùng thử


def test_chua_co_key_thi_dung_thu_bang_key_chu_app(cau_hinh):
    u = _nguoi()
    lc = tai_khoan.chon_khoa_giai(u)
    assert lc.nguon == "dung_thu" and lc.khoa is llm.KHOA_CHU_APP and lc.da_tru_luot
    assert db.lay_nguoi_dung(u.id).luot_da_dung == 1


def test_chu_app_va_demo_khong_tru_luot(cau_hinh, monkeypatch):
    monkeypatch.setattr(settings, "admin_emails", frozenset({"chu@example.com"}))
    chu = _nguoi("google-chu", "Chu@Example.com")
    assert tai_khoan.chon_khoa_giai(chu).nguon == "chu_app"
    u = _nguoi()
    assert tai_khoan.chon_khoa_giai(u, demo=True).khoa.nguon == "khong_co"
    assert db.lay_nguoi_dung(chu.id).luot_da_dung == 0 and db.lay_nguoi_dung(u.id).luot_da_dung == 0


def test_phien_ai_voi_key_rieng_chi_goi_gemini_bang_key_do(cau_hinh, monkeypatch):
    monkeypatch.setattr(claude_chat, "goi_claude", lambda **kw: pytest.fail("không được gọi Claude của chủ app"))
    goi = []

    def gemini(**kw):
        goi.append(kw["khoa"])
        return SimpleNamespace(candidates=[SimpleNamespace(content=SimpleNamespace(parts=[
            SimpleNamespace(text="ok", thought=False, function_call=None)]), finish_reason="STOP")], usage_metadata=None), "m", kw["contents"]

    monkeypatch.setattr(gemini_chat, "_goi_gemini", gemini)
    with llm.dung_khoa(llm.KhoaAI("ca_nhan", KEY, KEY[-4:])):
        phien = llm.PhienHoiThoai("sys")
        assert phien.nha_cung_cap == "gemini" and llm.ai_san_sang()
    assert phien.gui(text="xin chào").text == "ok"  # gọi sau khi ra khỏi khối with vẫn dùng key của lúc tạo phiên
    assert goi[0].gemini_api_key == KEY
    # ngoài khối with: trở lại key của chủ app
    # ngoài khối with: trở lại key của chủ app – mặc định Gemini (Claude chỉ khi kế hoạch có quyền Claude)
    assert llm.khoa_hien_tai() is llm.KHOA_CHU_APP and llm.PhienHoiThoai("sys").nha_cung_cap == "gemini"


def _client_gemini_gia(monkeypatch, loi_theo_key: dict[str, tuple[int, str] | None]):
    """genai.Client giả theo api_key: lỗi cố định cho mọi mô hình (None = trả lời được). Ghi lại (key, model) đã gọi."""
    from google.genai import errors

    da_goi = []

    def tao(api_key):
        class Models:
            def generate_content(self, model, contents, config):
                da_goi.append((api_key, model))
                loi = loi_theo_key.get(api_key)
                if loi:
                    raise errors.APIError(loi[0], {"error": {"message": loi[1], "status": "X"}})
                return SimpleNamespace(model=model)

        return SimpleNamespace(models=Models())

    monkeypatch.setattr(gemini_client, "_tao_client_gemini", tao)
    monkeypatch.setattr(gemini_client, "_gemini_client", None)
    monkeypatch.setattr(gemini_client, "_client_ca_nhan", OrderedDict())
    monkeypatch.setattr(gemini_chat, "_het_han_muc_ca_nhan", {})
    monkeypatch.setattr(gemini_chat, "_model_dung_ca_nhan", {})
    monkeypatch.setattr(gemini_chat, "_gemini_het_han_muc", {})
    monkeypatch.setattr(gemini_chat, "_gemini_model_dung", "")
    monkeypatch.setattr(gemini_chat, "_ngu", lambda s: None)
    return da_goi


def test_key_rieng_het_han_muc_bao_loi_khong_chuyen_sang_key_chu_app(cau_hinh, monkeypatch):
    da_goi = _client_gemini_gia(monkeypatch, {
        KEY: (429, "Quota exceeded for metric: generate_content_free_tier_requests, limit: 20. "
                   "GenerateRequestsPerDayPerProjectPerModel-FreeTier retryDelay: '50000s'"),
    })
    monkeypatch.setattr(claude_chat, "goi_claude", lambda **kw: pytest.fail("không được gọi Claude của chủ app"))
    with llm.dung_khoa(llm.KhoaAI("ca_nhan", KEY, KEY[-4:])), pytest.raises(llm.LoiAI) as e:
        llm.PhienHoiThoai("sys").gui(text="giải giúp")
    assert e.value.loai == "gioi_han"
    assert str(e.value).startswith("Key Gemini của bạn (…6789) đã hết hạn mức") and "không dùng key của chủ app" in str(e.value)
    assert da_goi and all(k == KEY for k, _ in da_goi)  # mọi lần gọi đều bằng key riêng (đã thử hết các mô hình)
    assert gemini_chat._gemini_het_han_muc == {}  # hạn mức của key chủ app không bị ảnh hưởng


def test_key_rieng_sai_bao_ro_tieng_viet(cau_hinh, monkeypatch):
    da_goi = _client_gemini_gia(monkeypatch, {KEY: (400, "API key not valid. Please pass a valid API key.")})
    with llm.dung_khoa(llm.KhoaAI("ca_nhan", KEY, "6789")), pytest.raises(llm.LoiAI) as e:
        llm.PhienHoiThoai("sys").gui(text="a")
    assert e.value.loai == "key" and "Key Gemini của bạn (…6789) không hợp lệ" in str(e.value) and "Cài đặt" in str(e.value)
    assert {k for k, _ in da_goi} == {KEY}


def test_dung_thu_van_dung_key_chu_app(cau_hinh, monkeypatch):
    da_goi = _client_gemini_gia(monkeypatch, {})
    monkeypatch.setattr(settings, "api_key", "")  # chủ app chỉ có Gemini
    with llm.dung_khoa(llm.KHOA_CHU_APP):
        assert llm.PhienHoiThoai("sys").nha_cung_cap == "gemini"
        gemini_chat._goi_gemini(system="s", contents=[], max_tokens=10, tools=None, output_schema=None, khoa=llm.khoa_hien_tai())
    assert da_goi[0][0] == "gm-chu-app"


def test_kiem_tra_key_gemini(monkeypatch):
    _client_gemini_gia(monkeypatch, {KEY: (400, f"API key not valid: {KEY}")})
    kq = llm.kiem_tra_key_gemini(KEY)
    assert kq["hop_le"] is False and KEY not in str(kq)
    _client_gemini_gia(monkeypatch, {KEY: (429, "quota")})
    kq = llm.kiem_tra_key_gemini(KEY)
    assert kq["hop_le"] is True and kq["het_han_muc"]
    _client_gemini_gia(monkeypatch, {})
    assert llm.kiem_tra_key_gemini(KEY)["hop_le"] is True


# ================================================================ giới hạn dùng thử
def test_dung_thu_toi_da_free_trial_limit(cau_hinh):
    u = _nguoi()
    for _ in range(3):
        assert tai_khoan.chon_khoa_giai(db.lay_nguoi_dung(u.id)).nguon == "dung_thu"
    with pytest.raises(tai_khoan.LoiTaiKhoan) as e:
        tai_khoan.chon_khoa_giai(db.lay_nguoi_dung(u.id))
    assert e.value.ma == "het_luot_dung_thu" and e.value.status == 403 and "Cài đặt" in str(e.value)
    assert db.lay_nguoi_dung(u.id).luot_da_dung == 3
    # thêm key riêng → giải tiếp được bằng key riêng
    assert tai_khoan.chon_khoa_giai(_co_key(db.lay_nguoi_dung(u.id))).nguon == "ca_nhan"


def test_free_trial_limit_doc_tu_cau_hinh(cau_hinh, monkeypatch):
    monkeypatch.setattr(settings, "free_trial_limit", 1)
    u = _nguoi()
    tai_khoan.chon_khoa_giai(u)
    with pytest.raises(tai_khoan.LoiTaiKhoan):
        tai_khoan.chon_khoa_giai(db.lay_nguoi_dung(u.id))
    assert tai_khoan.thong_tin(db.lay_nguoi_dung(u.id))["dung_thu"] == {"gioi_han": 1, "da_dung": 1, "con_lai": 0}


def test_gui_cung_luc_khong_vuot_gioi_han(cau_hinh):
    u = _nguoi()
    ket_qua = []
    luong = [threading.Thread(target=lambda: ket_qua.append(db.dung_luot_thu(u.id, 3))) for _ in range(12)]
    for t in luong:
        t.start()
    for t in luong:
        t.join()
    assert ket_qua.count(True) == 3 and db.lay_nguoi_dung(u.id).luot_da_dung == 3


@pytest.mark.parametrize("loai, hoan", [("gioi_han", True), ("het_tien", True), ("qua_tai", True), ("khac", False), ("", False)])
def test_tra_lai_luot_khi_loi_phia_app(cau_hinh, loai, hoan):
    u = _nguoi()
    lc = tai_khoan.chon_khoa_giai(u)
    db.tao_bai_giai(u.id, "20261010-120000-abcdef", "CH4 → C2H2", False, False, lc.nguon)
    tai_khoan.ket_thuc_bai(lc, u.id, "20261010-120000-abcdef", None, "lỗi", loai)
    assert db.lay_nguoi_dung(u.id).luot_da_dung == (0 if hoan else 1)


def test_chu_app_khong_co_key_thi_khong_co_dung_thu(cau_hinh, monkeypatch):
    monkeypatch.setattr(settings, "api_key", "")
    monkeypatch.setattr(settings, "gemini_api_key", "")
    with pytest.raises(tai_khoan.LoiTaiKhoan) as e:
        tai_khoan.chon_khoa_giai(_nguoi())
    assert e.value.ma == "can_key"


def test_3d_dung_key_nao(cau_hinh, monkeypatch):
    assert tai_khoan.chon_khoa_3d(None).nguon == "khong_co"
    u = _nguoi()
    assert tai_khoan.chon_khoa_3d(u) is llm.KHOA_CHU_APP  # còn lượt dùng thử: được nhờ AI chủ app (không trừ lượt)
    monkeypatch.setattr(settings, "free_trial_limit", 0)
    assert tai_khoan.chon_khoa_3d(u).nguon == "khong_co"
    assert tai_khoan.chon_khoa_3d(_co_key(u)).gemini_api_key == KEY


# ================================================================ API: đăng nhập, CSRF, tần suất, quyền
def _pipeline_gia(monkeypatch, loi: llm.LoiAI | None = None):
    """pipeline.chay giả: ghi lại key đang dùng, trả kết quả nhỏ (hoặc lỗi)."""
    da_chay = []

    def chay(job, chuoi, anh, hien_chrome, demo=False, hsg=False, **kw):
        da_chay.append(llm.khoa_hien_tai())
        if loi:
            job.loi, job.loai_loi = str(loi), loi.loai
        else:
            job.ket_qua = {"job_id": job.id, "mo_hinh": "Gemini (gia)", "de_bai": chuoi}
        if kw.get("truoc_khi_xong"):
            kw["truoc_khi_xong"](job, kw["ke_hoach"])
        job.xong = True

    monkeypatch.setattr(pipeline, "chay", chay)
    return da_chay


def _doi_xong(job_id: str, uid: int):
    for _ in range(200):
        bai = [b for b in db.lich_su(uid) if b["job_id"] == job_id]
        if bai and bai[0]["trang_thai"] != "dang_giai":
            return bai[0]
        time.sleep(0.01)
    raise AssertionError("bài giải không kết thúc")


def test_chua_dang_nhap_khong_giai_duoc(client):
    csrf = client.get("/api/toi").json()["csrf"]
    r = client.post("/api/giai", data={"chuoi": "CH4 → C2H2"}, headers={"X-CSRF-Token": csrf})
    assert r.status_code == 401 and r.json()["ma"] == "can_dang_nhap"


def test_thieu_hoac_sai_csrf_bi_chan(client, dang_nhap, monkeypatch):
    _pipeline_gia(monkeypatch)
    csrf = dang_nhap(client)
    assert client.post("/api/giai", data={"chuoi": "CH4 → C2H2"}).json()["ma"] == "csrf"
    assert client.post("/api/giai", data={"chuoi": "CH4 → C2H2"}, headers={"X-CSRF-Token": "sai"}).status_code == 403
    assert client.post("/api/key", json={"key": KEY}).status_code == 403
    assert client.delete("/api/key").status_code == 403
    assert client.post("/auth/dang-xuat").status_code == 403
    assert client.post("/auth/dang-xuat", data={"csrf_token": csrf}, follow_redirects=False).status_code == 303  # form HTML


def test_dang_nhap_cap_phien_moi_va_dang_xuat(client, dang_nhap):
    r = client.get("/api/toi")
    truoc = r.json()
    assert not truoc["dang_nhap"]
    cookie = r.headers["set-cookie"].lower()
    assert cookie.startswith("giai_chuoi_phien=") and "httponly" in cookie and "secure" in cookie and "samesite=lax" in cookie
    csrf = dang_nhap(client)
    toi = client.get("/api/toi").json()
    assert toi["dang_nhap"] and toi["nguoi_dung"]["email"] == "a@example.com" and toi["csrf"] == csrf != truoc["csrf"]
    assert toi["nguon_key_giai"] == "dung_thu" and toi["dung_thu"]["con_lai"] == 3
    client.post("/auth/dang-xuat", headers={"X-CSRF-Token": csrf, "Accept": "application/json"})
    assert not client.get("/api/toi").json()["dang_nhap"]


def test_email_chua_xac_minh_khong_dang_nhap_duoc(client, monkeypatch):
    async def lay_token(_request):
        return {"userinfo": {"sub": "x", "email": "x@example.com", "email_verified": False}}

    monkeypatch.setattr(auth, "oauth", SimpleNamespace(google=SimpleNamespace(authorize_access_token=lay_token)))
    r = client.get("/auth/callback", follow_redirects=False)
    assert r.headers["location"] == "/dang-nhap?loi=google" and not client.get("/api/toi").json()["dang_nhap"]


@pytest.mark.parametrize("tiep, ra", [("/cai-dat", "/cai-dat"), ("//evil.com", "/"), ("https://evil.com", "/"), ("/\\evil", "/")])
def test_chong_chuyen_huong_ra_ngoai(tiep, ra):
    assert auth.duong_dan_an_toan(tiep) == ra


def test_giai_qua_api_dung_thu_roi_het_luot(client, dang_nhap, monkeypatch):
    da_chay = _pipeline_gia(monkeypatch)
    h = {"X-CSRF-Token": dang_nhap(client)}
    uid = db.luu_nguoi_dung("google-1").id
    for i in range(3):
        r = client.post("/api/giai", data={"chuoi": f"CH4 → C2H2 #{i}"}, headers=h)
        assert r.status_code == 200 and r.json()["nguon_key"] == "dung_thu"
        _doi_xong(r.json()["job_id"], uid)
    bao_mat.gioi_han_tan_suat.xoa()
    r = client.post("/api/giai", data={"chuoi": "CH4 → C2H2"}, headers=h)
    assert r.status_code == 403 and r.json()["ma"] == "het_luot_dung_thu"
    assert all(k is llm.KHOA_CHU_APP for k in da_chay) and len(da_chay) == 3
    # nhập key riêng → giải bằng key riêng
    monkeypatch.setattr(tai_khoan, "kiem_tra_key_gemini", lambda k: KEY_OK)
    r = client.post("/api/key", json={"key": KEY}, headers=h)
    assert r.status_code == 200 and r.json()["cuoi"] == KEY[-4:] and KEY not in r.text
    r = client.post("/api/giai", data={"chuoi": "CH4 → C2H2"}, headers=h)
    assert r.json()["nguon_key"] == "ca_nhan"
    _doi_xong(r.json()["job_id"], uid)
    assert da_chay[-1].nguon == "ca_nhan" and da_chay[-1].gemini_api_key == KEY


def test_key_rieng_loi_thi_bao_loi_va_khong_tru_luot(client, dang_nhap, monkeypatch):
    _pipeline_gia(monkeypatch, loi=llm.LoiAI("Key Gemini của bạn đã hết hạn mức", "gioi_han"))
    monkeypatch.setattr(tai_khoan, "kiem_tra_key_gemini", lambda k: KEY_OK)
    h = {"X-CSRF-Token": dang_nhap(client)}
    client.post("/api/key", json={"key": KEY}, headers=h)
    r = client.post("/api/giai", data={"chuoi": "CH4 → C2H2"}, headers=h)
    uid = db.luu_nguoi_dung("google-1").id
    bai = _doi_xong(r.json()["job_id"], uid)
    assert bai["trang_thai"] == "loi" and "hết hạn mức" in bai["loi"] and db.lay_nguoi_dung(uid).luot_da_dung == 0


def test_api_key_khong_bao_gio_tra_key(client, dang_nhap, monkeypatch):
    monkeypatch.setattr(tai_khoan, "kiem_tra_key_gemini", lambda k: KEY_OK)
    h = {"X-CSRF-Token": dang_nhap(client)}
    assert client.post("/api/key", json={"key": KEY}, headers=h).status_code == 200
    for url in ("/api/key", "/api/toi", "/api/lich-su"):
        assert KEY not in client.get(url).text
    assert client.get("/api/key").json() == {"co": True, "cuoi": KEY[-4:]}
    kt = client.post("/api/key/kiem-tra", json={"key": ""}, headers=h).json()  # ô trống → kiểm tra key đã lưu
    assert kt["hop_le"] and kt["nguon"] == "da_luu" and KEY not in str(kt)
    assert client.delete("/api/key", headers=h).json() == {"ok": True}
    assert client.get("/api/key").json() == {"co": False, "cuoi": ""}


def test_gioi_han_tan_suat_api(client, dang_nhap, monkeypatch):
    monkeypatch.setattr(tai_khoan, "kiem_tra_key_gemini", lambda k: KEY_OK)
    h = {"X-CSRF-Token": dang_nhap(client)}
    trang_thai = [client.post("/api/key/kiem-tra", json={"key": KEY}, headers=h).status_code for _ in range(8)]
    assert trang_thai[:6] == [200] * 6 and trang_thai[6] == 429
    r = client.post("/api/key/kiem-tra", json={"key": KEY}, headers=h)
    assert r.json()["ma"] == "gioi_han_tan_suat" and int(r.headers["Retry-After"]) >= 1


def test_gioi_han_tan_suat_cua_so_truot():
    gio = [0.0]
    g = GioiHanTanSuat({"x": [(2, 10)]}, dong_ho=lambda: gio[0])
    assert g.kiem_tra("x", "a") is None and g.kiem_tra("x", "a") is None
    assert g.kiem_tra("x", "a") == pytest.approx(10)
    assert g.kiem_tra("x", "b") is None  # mỗi người một hạn mức
    gio[0] = 10.5
    assert g.kiem_tra("x", "a") is None


def test_khong_xem_duoc_bai_cua_nguoi_khac(client, dang_nhap, monkeypatch, cau_hinh):
    from fastapi.testclient import TestClient

    from backend.main import app

    _pipeline_gia(monkeypatch)
    h_a = {"X-CSRF-Token": dang_nhap(client, "google-a", "a@example.com")}
    r = client.post("/api/giai", data={"chuoi": "CH4 → C2H2"}, headers=h_a)
    job_id = r.json()["job_id"]
    uid_a = db.luu_nguoi_dung("google-a", "a@example.com").id
    _doi_xong(job_id, uid_a)
    (pipeline.JOBS[job_id].thu_muc / "dap_an.png").write_bytes(b"png")
    assert client.get(f"/api/ket-qua/{job_id}").status_code == 200
    assert client.get(f"/api/tep/{job_id}/dap_an.png").status_code == 200

    b = TestClient(app, base_url="https://testserver")
    h_b = {"X-CSRF-Token": dang_nhap(b, "google-b", "b@example.com")}
    for url in (f"/api/ket-qua/{job_id}", f"/api/tien-trinh/{job_id}", f"/api/tep/{job_id}/dap_an.png", f"/api/tai/{job_id}/png"):
        assert b.get(url).status_code == 404, url
    assert b.post(f"/api/chemdraw/{job_id}", headers=h_b).status_code == 404
    assert b.get("/api/lich-su").json() == {"bai": []}
    assert [x["job_id"] for x in client.get("/api/lich-su").json()["bai"]] == [job_id]
    # chưa đăng nhập: không xem được gì
    k = TestClient(app, base_url="https://testserver")
    assert k.get(f"/api/ket-qua/{job_id}").status_code == 401 and k.get("/api/lich-su").status_code == 401


def test_ket_qua_doc_lai_tu_lich_su_sau_khi_khoi_dong_lai(client, dang_nhap, monkeypatch):
    _pipeline_gia(monkeypatch)
    h = {"X-CSRF-Token": dang_nhap(client)}
    job_id = client.post("/api/giai", data={"chuoi": "CH4 → C2H2"}, headers=h).json()["job_id"]
    _doi_xong(job_id, db.luu_nguoi_dung("google-1").id)
    pipeline.JOBS.pop(job_id)  # như server vừa khởi động lại
    r = client.get(f"/api/ket-qua/{job_id}")
    assert r.status_code == 200 and r.json()["job_id"] == job_id


def test_chemdraw_chi_cho_chu_app(client, dang_nhap, monkeypatch):
    _pipeline_gia(monkeypatch)
    h = {"X-CSRF-Token": dang_nhap(client)}
    job_id = client.post("/api/giai", data={"chuoi": "CH4 → C2H2"}, headers=h).json()["job_id"]
    r = client.post(f"/api/chemdraw/{job_id}", headers=h)
    assert r.status_code == 403 and "máy chạy app" in r.json()["loi"]


def test_che_do_cuc_bo_chi_cho_may_nay(monkeypatch, csdl_tam):
    monkeypatch.setattr(settings, "google_client_id", "")
    may_nay = SimpleNamespace(client=SimpleNamespace(host="127.0.0.1"), headers={}, session={})
    may_khac = SimpleNamespace(client=SimpleNamespace(host="203.0.113.5"), headers={}, session={})
    qua_proxy = SimpleNamespace(client=SimpleNamespace(host="127.0.0.1"), headers={"x-forwarded-for": "203.0.113.5"}, session={})
    assert auth.che_do_cuc_bo(may_nay) and not auth.che_do_cuc_bo(may_khac) and not auth.che_do_cuc_bo(qua_proxy)
    u = auth.nguoi_dung_hien_tai(may_nay)
    assert u.cuc_bo and tai_khoan.la_chu_app(u) and tai_khoan.chon_khoa_giai(u).nguon == "chu_app"
    assert auth.nguoi_dung_hien_tai(may_khac) is None
    monkeypatch.setattr(settings, "google_client_id", "id")
    monkeypatch.setattr(settings, "google_client_secret", "s")
    assert not auth.che_do_cuc_bo(may_nay)  # đã cấu hình Google thì ai cũng phải đăng nhập
