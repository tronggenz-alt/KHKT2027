"""Sơ đồ hệ thống (/admin/mindmap): chỉ admin, tên mô hình THẬT lấy từ settings (.env), đếm lượt gọi hôm nay, health-check nhẹ.
Không gọi mạng (conftest thay hàm ping)."""
import time

import pytest

from backend import db, llm, so_do_he_thong as sd
from backend.config import GEMINI_MODEL_DU_PHONG, settings


def _cac_node(n):
    yield n
    for c in n.get("con", []):
        yield from _cac_node(c)


def _tat_ca(cfg):
    return [x for ht in cfg["he_thong"] for x in _cac_node(ht)]


@pytest.fixture
def admin(client, dang_nhap):
    dang_nhap(client, "admin-1", "admin@example.com")
    db.dat_admin(db.tim_theo_email("admin@example.com").id, True)
    return client


def _ghi(kind, provider, model, ok=1, step="giai", ts=None, nguon="chu_app", loi=""):
    with db.ket_noi() as con:
        con.execute("INSERT INTO ai_usage (ts, kind, step, provider, model, key_source, ok, error) VALUES (?, ?, ?, ?, ?, ?, ?, ?)",
                    (ts or time.time(), kind, step, provider, model, nguon, ok, loi))


def test_cau_hinh_hop_le():
    cfg = sd.doc_cau_hinh()
    assert [h["id"] for h in cfg["he_thong"]] == ["giai_chuoi", "mo_hinh_3d", "co_che", "ha_tang", "notebooklm"]
    assert set(cfg["nha_cung_cap"]) == {"claude", "gemini", "openai"}
    ids = []
    for ht in cfg["he_thong"]:
        nodes = [n for c in ht.get("ai", {}).values() for x in c.get("con", []) for n in _cac_node(x)]
        nodes += [n for x in ht.get("khac", []) + ht.get("con", []) for n in _cac_node(x)]
        ids += [n["id"] for n in nodes]
        for n in nodes:
            if n.get("ai"):
                assert n["ai"] in cfg["nha_cung_cap"] and (n["model_tu"] == "gemini_du_phong" or hasattr(settings, n["model_tu"])), n["id"]
        if "ai" in ht:  # nhánh AI: đủ 3 nhà cung cấp của router (thứ tự do router quyết định, không ghi trong file)
            assert set(ht["ai"]) == {"claude", "gemini", "openai"} and all("dieu_kien" in c for c in ht["ai"].values())
        assert any(x.get("ai") or x.get("khong_ai") for x in nodes) or "ai" in ht, ht["id"]
    assert len(ids) == len(set(ids))


def test_nhanh_ai_theo_thu_tu_router(admin, monkeypatch):
    from backend.ai import router as ai_router
    monkeypatch.setattr(settings, "openai_api_key", "sk-proj-test")
    monkeypatch.setattr(settings, "openai_model", "gpt-test")
    d = admin.get("/admin/api/so-do").json()
    gc = next(h for h in d["he_thong"] if h["id"] == "giai_chuoi")
    ncc = [c for c in gc["con"] if c.get("nha_cung_cap_node")]
    assert [(c["ai"], c["vai_tro"], c["fallback"]) for c in ncc] == [
        ("claude", "chinh", ["giai_chuoi_gemini"]), ("gemini", "du_phong_1", ["giai_chuoi_openai"]), ("openai", "du_phong_2", [])]
    assert ncc[2]["models"] == ["gpt-test"] and [x["id"] for x in ncc[2]["con"]] == ["gc_openai"]
    assert d["nha_cung_cap"]["openai"]["co_key"] is True and d["thu_tu_ai"]["giai_chuoi"]["thu_tu"] == ["claude", "gemini", "openai"]
    ai_router.dat_thu_tu("co_che", ["openai", "claude", "gemini"])  # admin đổi → sơ đồ đổi theo (đọc trực tiếp router)
    cc = next(h for h in admin.get("/admin/api/so-do").json()["he_thong"] if h["id"] == "co_che")
    assert [(c["ai"], c["vai_tro"]) for c in cc["con"] if c.get("nha_cung_cap_node")] == [("openai", "chinh"), ("claude", "du_phong_1"), ("gemini", "du_phong_2")]
    # 3D không dùng Claude (config: con rỗng) → vai trò tính trong các AI thật sự dùng, fallback bỏ qua Claude
    m3 = next(h for h in admin.get("/admin/api/so-do").json()["he_thong"] if h["id"] == "mo_hinh_3d")
    assert [(c["ai"], c["vai_tro"], c["fallback"]) for c in m3["con"] if c.get("nha_cung_cap_node")] == [
        ("claude", "khong_dung", []), ("gemini", "chinh", ["mo_hinh_3d_openai"]), ("openai", "du_phong_1", [])]
    monkeypatch.setattr(settings, "openai_api_key", "")
    assert admin.get("/admin/api/so-do").json()["nha_cung_cap"]["openai"]["thieu"].startswith("Chưa có OPENAI_API_KEY")


def test_api_thu_tu_ai(admin, dang_nhap, client):
    d = admin.get("/admin/api/thu-tu-ai").json()
    assert d["he_thong"]["mo_hinh_3d"]["thu_tu"] == ["claude", "gemini", "openai"] and d["timeout_giay"] == 30
    assert set(d["nha_cung_cap"]) == {"claude", "gemini", "openai"} and "sk-" not in str(d)
    assert d["khong_dung"] == {"giai_chuoi": [], "mo_hinh_3d": ["claude"], "co_che": []}
    h = {"X-CSRF-Token": admin.get("/api/toi").json()["csrf"]}
    r = admin.post("/admin/api/thu-tu-ai", json={"he_thong": "mo_hinh_3d", "thu_tu": ["claude", "openai", "gemini"]}, headers=h)
    assert r.status_code == 200 and r.json()["he_thong"]["mo_hinh_3d"] == {"ten": "Mô hình 3D", "thu_tu": ["claude", "openai", "gemini"],
                                                                         "mac_dinh": ["claude", "gemini", "openai"], "da_doi": True}
    assert admin.post("/admin/api/thu-tu-ai", json={"he_thong": "mo_hinh_3d", "thu_tu": ["claude", "claude", "gemini"]}, headers=h).status_code == 400
    assert admin.post("/admin/api/thu-tu-ai", json={"he_thong": "khong_co", "thu_tu": ["claude", "gemini", "openai"]}, headers=h).status_code == 400
    r = admin.post("/admin/api/thu-tu-ai", json={"he_thong": "mo_hinh_3d", "thu_tu": None}, headers=h)
    assert r.json()["he_thong"]["mo_hinh_3d"]["da_doi"] is False
    with db.ket_noi() as con:
        nk = [r[0] for r in con.execute("SELECT detail FROM activity_log WHERE event = 'doi_thu_tu_ai' ORDER BY id")]
    assert nk == ["Mô hình 3D: Claude → ChatGPT → Gemini", "Mô hình 3D: Claude → Gemini → ChatGPT (mặc định)"]
    dang_nhap(client, "hs-9", "hs9@example.com")
    assert client.get("/admin/api/thu-tu-ai").status_code == 403


def test_trang_va_api_chi_admin(client, dang_nhap):
    r = client.get("/admin/mindmap", follow_redirects=False)
    assert r.status_code == 303 and "tiep=%2Fadmin%2Fmindmap" in r.headers["location"]
    dang_nhap(client, "hs-1", "hs@example.com")
    assert client.get("/admin/mindmap").status_code == 403
    assert client.get("/admin/api/so-do").status_code == 403 and client.get("/admin/api/so-do/suc-khoe").status_code == 403


def test_ten_mo_hinh_that_tu_settings_va_dem_hom_nay(admin, monkeypatch):
    monkeypatch.setattr(settings, "model", "claude-sonnet-thu")
    monkeypatch.setattr(settings, "gemini_model_lite", "gemini-lite-thu")
    _ghi("giai", "gemini", "gemini-lite-thu", step="phan_loai")
    _ghi("giai", "gemini", "gemini-lite-thu", ok=0)
    _ghi("giai", "claude", "claude-sonnet-thu-20261001")   # Claude trả tên có hậu tố ngày
    _ghi("co_che", "gemini", "gemini-lite-thu", step="tra_cuu")
    _ghi("giai", "gemini", "gemini-lite-thu", ts=time.time() - 3 * 86400)  # hôm trước: không tính
    r = admin.get("/admin/api/so-do")
    assert r.status_code == 200
    d = r.json()
    n = {x["id"]: x for x in _tat_ca(d)}
    assert n["gc_lite"]["models"] == ["gemini-lite-thu"] and (n["gc_lite"]["goi_hom_nay"], n["gc_lite"]["loi_hom_nay"]) == (2, 1)
    assert n["gc_sonnet"]["models"] == ["claude-sonnet-thu"] and n["gc_sonnet"]["goi_hom_nay"] == 1
    assert n["gc_du_phong"]["models"] == list(GEMINI_MODEL_DU_PHONG)
    assert n["cc_lite"]["goi_hom_nay"] == 1 and n["ht_tra_cuu_ai"]["goi_hom_nay"] == 1   # theo kind / step
    assert n["giai_chuoi"]["goi_hom_nay"] == 3 and d["tong_goi_hom_nay"] == 4
    assert n["ht_xuat_ai"]["khong_ai"] and "models" not in n["ht_xuat_ai"]


def test_suc_khoe(admin, monkeypatch):
    monkeypatch.setattr(settings, "api_key", "sk-ant-test")
    monkeypatch.setattr(settings, "gemini_api_key", "gm-test")
    monkeypatch.setattr(settings, "model", "claude-a")
    monkeypatch.setattr(settings, "claude_model_manh", "claude-b")

    def ping_gemini(model):
        if model == settings.gemini_model_hsg:
            raise type("NotFoundError", (Exception,), {})("x")

    monkeypatch.setattr(sd, "_ping_gemini", ping_gemini)
    monkeypatch.setattr(settings, "openai_api_key", "sk-proj-test")
    monkeypatch.setattr(settings, "openai_model", "gpt-test")
    monkeypatch.setattr(sd, "_ping_openai", lambda model: None)
    _ghi("giai", "claude", "claude-b", ok=0, loi="Tài khoản Anthropic hết tín dụng")
    _ghi("giai", "claude", "claude-a", ok=0, nguon="claude_rieng", loi="key của người dùng")  # key riêng: không tính cho máy chủ
    mh = admin.get("/admin/api/so-do/suc-khoe").json()["mo_hinh"]
    assert mh["claude:claude-a"]["trang_thai"] == "ok"
    assert mh["claude:claude-b"]["trang_thai"] == "loi" and "hết tín dụng" in mh["claude:claude-b"]["chi_tiet"]
    assert mh[f"gemini:{settings.gemini_model_hsg}"]["trang_thai"] == "loi" and "Không có mô hình" in mh[f"gemini:{settings.gemini_model_hsg}"]["chi_tiet"]
    assert mh[f"gemini:{settings.gemini_model_lite}"]["trang_thai"] == "ok"
    assert mh["openai:gpt-test"]["trang_thai"] == "ok"
    monkeypatch.setattr(llm, "_claude_tam_dung_den", time.time() + 60)
    monkeypatch.setattr(llm, "_claude_ly_do_tam_dung", "hết tiền")
    assert admin.get("/admin/api/so-do/suc-khoe").json()["mo_hinh"]["claude:claude-a"]["trang_thai"] == "loi"  # cache ping nhưng lỗi gần đây tính lại
    monkeypatch.setattr(settings, "api_key", "")
    tt = admin.get("/admin/api/so-do/suc-khoe?lam_moi=1").json()["mo_hinh"]["claude:claude-a"]
    assert tt["trang_thai"] == "tat" and "ANTHROPIC_API_KEY" in tt["chi_tiet"] and "sk-ant" not in str(tt)


def test_suc_khoe_claude_het_tin_dung(admin, monkeypatch):
    monkeypatch.setattr(settings, "api_key", "sk-ant-test")
    monkeypatch.setattr(settings, "model", "claude-test")

    def ping_het_tien(model):
        raise type("BadRequestError", (Exception,), {"status_code": 400})("Your credit balance is too low to access the Anthropic API.")

    monkeypatch.setattr(sd, "_ping_claude", ping_het_tien)
    tt = admin.get("/admin/api/so-do/suc-khoe?lam_moi=1").json()["mo_hinh"]["claude:claude-test"]
    assert tt["trang_thai"] == "loi"
    assert "hết tín dụng" in tt["chi_tiet"]

