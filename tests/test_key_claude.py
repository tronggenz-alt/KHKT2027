"""Key Claude dùng chung (CLAUDE_ACCESS_KEY): nhập đúng key → mở Claude; sai 5 lần/giờ (tài khoản + IP) → khoá 1 giờ; giới hạn bài/ngày;
đổi key → mất quyền; admin theo dõi / thu hồi; key không bao giờ lộ ra API, nhật ký, log.

AI giả ở tầng gọi API (dùng lại AIGia của test_chon_mo_hinh) – quyền, pipeline, chuyển dự phòng, ghi chi phí chạy thật.
"""
import hmac
import logging
from pathlib import Path
from types import SimpleNamespace

import pytest

from backend import db, key_claude, llm, main, ma_claude, pipeline
from backend.bao_mat import gioi_han_tan_suat
from backend.config import settings
from test_chon_mo_hinh import CHUOI, AIGia, _cho_xong, _dang_nhap_email, _han

KEY = "khoa-thu-Kc9"          # key giả cho test (không phải key thật)
KEY_MOI = "khoa-moi-Zq4"
GOC = Path(__file__).resolve().parent.parent


@pytest.fixture
def ai(monkeypatch, cau_hinh, tmp_path):
    monkeypatch.setattr(settings, "gemini_model_lite", "lite-test")
    monkeypatch.setattr(settings, "gemini_model", "flash-test")
    monkeypatch.setattr(settings, "gemini_model_hsg", "pro-test")
    monkeypatch.setattr(settings, "model", "claude-test")
    monkeypatch.setattr(settings, "claude_access_key", KEY)
    monkeypatch.setattr(llm, "_claude_tam_dung_den", 0.0)
    monkeypatch.setattr(llm, "_claude_ly_do_tam_dung", "")
    monkeypatch.setattr(pipeline, "OUTPUT_DIR", tmp_path / "out")
    return AIGia(monkeypatch)


def _giai(c, **them):
    gioi_han_tan_suat.xoa()
    r = c.post("/api/giai", data={"chuoi": CHUOI, "dung_claude": "true", **them}, headers=c.h)
    assert r.status_code == 200, r.text
    return r.json(), _cho_xong(r.json()["job_id"])


def _su_kien(*ten):
    with db.ket_noi() as con:
        return [r[0] for r in con.execute(f"SELECT event FROM activity_log WHERE event IN ({','.join('?' * len(ten))}) ORDER BY id", ten)]


# ================================================================ so key, vân tay
def test_so_key_bang_compare_digest_va_van_tay(cau_hinh, monkeypatch):
    monkeypatch.setattr(settings, "claude_access_key", KEY)
    goi = []
    that = hmac.compare_digest
    monkeypatch.setattr(key_claude.hmac, "compare_digest", lambda a, b: goi.append(1) or that(a, b))
    assert key_claude._dung_key(KEY) and key_claude._dung_key(f"  {KEY} ")
    assert not key_claude._dung_key(KEY.upper()) and not key_claude._dung_key("") and not key_claude._dung_key(KEY + "x")
    assert len(goi) == 5  # mọi lần so đều qua hàm so sánh thời gian hằng
    vt = key_claude.van_tay(KEY)
    assert len(vt) == 64 and KEY not in vt and vt == key_claude.van_tay_hien_tai() != key_claude.van_tay(KEY_MOI)
    monkeypatch.setattr(settings, "claude_access_key", "")
    assert not key_claude.bat() and not key_claude._dung_key("")  # chưa đặt key → không ai mở được


# ================================================================ chưa mở → không công tắc, API đòi Claude bị từ chối
def test_chua_mo_khong_co_quyen_api_doi_claude_bi_tu_choi_dung_gemini(ai, client, monkeypatch):
    ai.muc_do = "de"
    hs = _dang_nhap_email(monkeypatch, client, "hs@example.com")
    toi = hs.get("/api/toi").json()
    assert toi["claude_key"]["da_mo"] is False and toi["claude"]["co_the_dung"] is False  # → giao diện không hiện công tắc
    d, job = _giai(hs)
    assert d["dung_claude"] is False and "từ chối dùng Claude" in d["thong_bao_claude"]
    assert job.loi is None and job.ket_qua["mo_hinh"] == "Gemini (lite-test)" and job.ket_qua["dung_claude"] is False
    assert not any(g[0] == "claude" for g in ai.goi)


# ================================================================ nhập key
def test_nhap_dung_key_mo_claude_giai_moi_muc_bang_claude(ai, client, monkeypatch):
    ai.muc_do = "de"  # đề Dễ: không có key → Flash-Lite; có key + bật công tắc → Claude
    hs = _dang_nhap_email(monkeypatch, client, "hs@example.com")
    r = hs.post("/api/key-claude", json={"key": KEY}, headers=hs.h)
    assert r.status_code == 200 and r.json()["da_mo"] is True and r.json()["con_hom_nay"] == 10
    k = hs.get("/api/toi").json()["claude_key"]
    assert (k["trang_thai"], k["da_mo"], k["gioi_han"]) == ("da_mo", True, 10) and k["kich_hoat_luc"]
    d, job = _giai(hs)
    assert d["dung_claude"] is True and d["thong_bao_claude"] == ""
    assert job.ket_qua["mo_hinh"] == "Claude (claude-test)" and job.ket_qua["dung_claude"] is True
    assert any("Mức Kỹ (tự động theo độ khó) → Claude" in e["thong_diep"] for e in job.su_kien)  # bật Dùng Claude → ít nhất Kỹ
    assert any("Đã dùng 1 lượt Claude hôm nay (còn 9/10 lượt)" in e["thong_diep"] for e in job.su_kien)
    assert ("claude", "claude-test") in [(n, m) for n, m, _ in ai.goi]
    assert hs.get("/api/key-claude").json()["con_hom_nay"] == 9
    # tắt công tắc → Gemini theo độ khó như bình thường, không tính lượt
    d, job = _giai(hs, dung_claude="false")
    assert d["dung_claude"] is False and job.ket_qua["mo_hinh"] == "Gemini (lite-test)"
    assert hs.get("/api/key-claude").json()["con_hom_nay"] == 9
    with db.ket_noi() as con:
        assert tuple(con.execute("SELECT COUNT(*), SUM(done) FROM claude_key_uses").fetchone()) == (1, 1)
        assert con.execute("SELECT claude FROM solutions WHERE job_id = ?", (job.id,)).fetchone()[0] == 0
    assert _su_kien("kich_hoat_key_claude") == ["kich_hoat_key_claude"]
    # gỡ key → mất quyền, ghi nhật ký
    assert hs.delete("/api/key-claude", headers=hs.h).json()["da_mo"] is False
    assert _su_kien("go_key_claude") == ["go_key_claude"]
    d, _ = _giai(hs)
    assert d["dung_claude"] is False


def test_nhap_sai_5_lan_bi_khoa_1_gio_theo_tai_khoan(ai, client, monkeypatch):
    hs = _dang_nhap_email(monkeypatch, client, "hs@example.com")
    for i in range(4):
        r = hs.post("/api/key-claude", json={"key": f"sai-{i}"}, headers=hs.h)
        assert r.status_code == 400 and r.json()["loi"].startswith("Key không đúng") and f"còn {4 - i} lần" in r.json()["loi"]
    r = hs.post("/api/key-claude", json={"key": "sai-5"}, headers=hs.h)
    assert r.status_code == 429 and r.json()["ma"] == "key_claude_bi_khoa" and int(r.headers["Retry-After"]) > 3000
    gioi_han_tan_suat.xoa()
    r = hs.post("/api/key-claude", json={"key": KEY}, headers=hs.h)  # đang bị khoá: key ĐÚNG cũng không nhận
    assert r.status_code == 429 and hs.get("/api/toi").json()["claude_key"]["khoa_nhap_giay"] > 3000
    assert _su_kien("nhap_sai_key_claude", "khoa_key_claude") == ["nhap_sai_key_claude"] * 5 + ["khoa_key_claude"]
    with db.ket_noi() as con:
        con.execute("UPDATE claude_key_fails SET ts = ts - 7200")  # 2 giờ sau
    r = hs.post("/api/key-claude", json={"key": KEY}, headers=hs.h)
    assert r.status_code == 200 and r.json()["da_mo"] is True


def test_nhap_sai_5_lan_cung_ip_nhieu_tai_khoan_bi_khoa(ai, client, monkeypatch):
    nguoi = [db.tao_tai_khoan_email(f"hs{i}@example.com", f"hs{i}", "x", da_xac_thuc=True) for i in range(6)]
    for i in range(4):
        with pytest.raises(key_claude.LoiKeyClaude) as e:
            key_claude.kich_hoat(nguoi[i], "doan-mo", "198.51.100.7")
        assert e.value.ma == "key_claude_sai"
    with pytest.raises(key_claude.LoiKeyClaude) as e:
        key_claude.kich_hoat(nguoi[4], "doan-mo", "198.51.100.7")
    assert e.value.ma == "key_claude_bi_khoa"
    with pytest.raises(key_claude.LoiKeyClaude) as e:  # tài khoản thứ 6, cùng IP, key đúng → vẫn bị khoá
        key_claude.kich_hoat(nguoi[5], KEY, "198.51.100.7")
    assert e.value.ma == "key_claude_bi_khoa"
    assert key_claude.kich_hoat(nguoi[5], KEY, "198.51.100.8")["da_mo"] is True  # IP khác, tài khoản chưa sai lần nào


# ================================================================ giới hạn bài/ngày, Claude lỗi
def test_qua_gioi_han_ngay_tu_chuyen_gemini(ai, client, monkeypatch):
    monkeypatch.setattr(settings, "claude_daily_limit", 2)
    ai.muc_do = "trung_binh"
    hs = _dang_nhap_email(monkeypatch, client, "hs@example.com")
    assert hs.post("/api/key-claude", json={"key": KEY}, headers=hs.h).status_code == 200
    for con in (1, 0):
        d, job = _giai(hs)
        assert job.ket_qua["mo_hinh"] == "Claude (claude-test)"
        assert any(f"còn {con}/2 lượt" in e["thong_diep"] for e in job.su_kien)
    n_claude = len([g for g in ai.goi if g[0] == "claude"])
    d, job = _giai(hs)
    assert d["dung_claude"] is False and d["thong_bao_claude"] == "Hôm nay đã hết lượt Claude, đang dùng Gemini."
    assert job.ket_qua["mo_hinh"] == "Gemini (flash-test)" and len([g for g in ai.goi if g[0] == "claude"]) == n_claude
    assert any("Hôm nay đã hết lượt Claude, đang dùng Gemini" in e["thong_diep"] for e in job.su_kien)
    # giữ lượt nguyên tử: hai yêu cầu cùng lúc không vượt giới hạn
    monkeypatch.setattr(settings, "claude_daily_limit", 3)
    q1, q2 = key_claude.QuyenKey(user_id=hs.uid), key_claude.QuyenKey(user_id=hs.uid)
    assert q1.giu() is True and q2.giu() is False
    q1.tra()
    assert q2.giu() is True and q2.chot() is True and key_claude.da_dung_hom_nay(hs.uid) == 3


def test_claude_loi_tu_chuyen_gemini_khong_tinh_luot(ai, client, monkeypatch):
    ai.muc_do = "hsg"
    ai.claude_loi = llm.LoiAI("Tài khoản Anthropic đã hết tín dụng.", "het_tien")
    hs = _dang_nhap_email(monkeypatch, client, "hs@example.com")
    hs.post("/api/key-claude", json={"key": KEY}, headers=hs.h)
    d, job = _giai(hs)
    assert d["dung_claude"] is True and job.loi is None and job.ket_qua["mo_hinh"] == "Gemini (pro-test)"
    assert any("Claude lỗi" in e["thong_diep"] and "Gemini Pro" in e["thong_diep"] for e in job.su_kien)
    assert key_claude.da_dung_hom_nay(hs.uid) == 0  # không trừ lượt hôm nay


def test_co_che_bat_claude_dung_claude_va_tinh_luot(ai, client, monkeypatch):
    nhan = []

    def tao_gia(*_a, **_k):
        ph = llm.PhienHoiThoai("system cơ chế giả")
        nhan.append(ph.nha_cung_cap)
        ph.gui(text="vẽ cơ chế")
        return SimpleNamespace(ten="SN2", loai="SN2", nguon="ai", canh_bao=[], buoc=[], to_json=lambda: {})

    monkeypatch.setattr(main, "tao_co_che", tao_gia)
    monkeypatch.setattr(main, "ve_co_che", lambda cc: SimpleNamespace(svg="<svg/>"))
    monkeypatch.setattr(main, "xuat_cdxml", lambda cc, h: "<CDXML/>")
    hs = _dang_nhap_email(monkeypatch, client, "hs@example.com")
    r = hs.post("/api/co-che", json={"van_ban": "CH3CH2Br + OH-", "dung_claude": True}, headers=hs.h)
    assert r.status_code == 200 and nhan == ["gemini"] and r.json()["dung_claude"] is False
    assert any("từ chối dùng Claude" in t for t in r.json()["thong_bao_claude"])
    hs.post("/api/key-claude", json={"key": KEY}, headers=hs.h)
    gioi_han_tan_suat.xoa()
    r = hs.post("/api/co-che", json={"van_ban": "CH3CH2Br + OH-", "dung_claude": True}, headers=hs.h)
    assert r.status_code == 200 and nhan[-1] == "claude" and r.json()["dung_claude"] is True and r.json()["thong_bao_claude"] == []
    with db.ket_noi() as con:
        assert tuple(con.execute("SELECT kind, done FROM claude_key_uses").fetchone()) == ("co_che", 1)
    gioi_han_tan_suat.xoa()
    r = hs.post("/api/co-che", json={"van_ban": "CH3CH2Br + OH-", "dung_claude": False}, headers=hs.h)
    assert nhan[-1] == "gemini" and key_claude.da_dung_hom_nay(hs.uid) == 1


# ================================================================ đổi key → mất quyền
def test_doi_key_tai_khoan_cu_mat_quyen_phai_nhap_key_moi(ai, client, monkeypatch, caplog):
    hs = _dang_nhap_email(monkeypatch, client, "hs@example.com")
    assert hs.post("/api/key-claude", json={"key": KEY}, headers=hs.h).json()["da_mo"] is True
    monkeypatch.setattr(settings, "claude_access_key", KEY_MOI)  # chủ app đổi key trong .env + khởi động lại
    with caplog.at_level(logging.INFO, logger="backend.key_claude"):
        key_claude.don_khi_khoi_dong()
    assert "1 tài khoản kích hoạt bằng key cũ đã mất quyền" in caplog.text
    k = hs.get("/api/toi").json()["claude_key"]
    assert (k["da_mo"], k["trang_thai"]) == (False, "key_cu")
    d, job = _giai(hs)
    assert d["dung_claude"] is False and "Key Claude đã được đổi" in d["thong_bao_claude"]
    assert not any(g[0] == "claude" for g in ai.goi)
    assert hs.post("/api/key-claude", json={"key": KEY}, headers=hs.h).status_code == 400  # key cũ không còn đúng
    assert hs.post("/api/key-claude", json={"key": KEY_MOI}, headers=hs.h).json()["da_mo"] is True
    assert key_claude.danh_sach()[0]["sai_30_ngay"] == 1


# ================================================================ admin
def test_admin_luon_dung_claude_khong_can_key(ai, client, monkeypatch):
    ai.muc_do = "de"
    monkeypatch.setattr(settings, "claude_access_key", "")  # kể cả khi chưa bật Key Claude
    ad = _dang_nhap_email(monkeypatch, client, "admin@example.com", admin=True)
    k = ad.get("/api/toi").json()["claude_key"]
    assert k["la_admin"] is True and k["da_mo"] is True
    d, job = _giai(ad)
    assert d["dung_claude"] is True and job.ket_qua["mo_hinh"] == "Claude (claude-test)"
    with db.ket_noi() as con:
        assert con.execute("SELECT COUNT(*) FROM claude_key_uses").fetchone()[0] == 0  # admin không giới hạn


def test_admin_theo_doi_thu_hoi_mo_lai(ai, client, monkeypatch):
    hs = _dang_nhap_email(monkeypatch, client, "hs@example.com")
    for url in ("/admin/api/key-claude/1/thu-hoi", "/admin/api/key-claude/1/mo-lai"):
        assert hs.post(url, json={}, headers=hs.h).status_code == 403
    assert hs.get("/admin/api/key-claude").status_code == 403
    hs.post("/api/key-claude", json={"key": "sai"}, headers=hs.h)
    hs.post("/api/key-claude", json={"key": KEY}, headers=hs.h)
    _giai(hs)
    ad = _dang_nhap_email(monkeypatch, client, "admin@example.com", admin=True)
    d = ad.get("/admin/api/key-claude").json()
    assert d["cau_hinh"] == {"bat": True, "gioi_han_ngay": 10}
    x = d["nguoi_dung"][0]
    assert (x["email"], x["trang_thai"], x["hom_nay"], x["tong"], x["sai_30_ngay"], x["sai_24h"]) == ("hs@example.com", "da_mo", 1, 1, 1, 1)
    assert x["kich_hoat_luc"]
    assert ad.post(f"/admin/api/key-claude/{hs.uid}/thu-hoi", json={}, headers=ad.h).json()["trang_thai"] == "bi_thu_hoi"
    assert hs.get("/api/toi").json()["claude_key"]["da_mo"] is False
    d, _ = _giai(hs)
    assert d["dung_claude"] is False and "thu hồi" in d["thong_bao_claude"]
    gioi_han_tan_suat.xoa()
    r = hs.post("/api/key-claude", json={"key": KEY}, headers=hs.h)  # tự kích hoạt lại: không được
    assert r.status_code == 403 and r.json()["ma"] == "key_claude_bi_thu_hoi"
    hs.delete("/api/key-claude", headers=hs.h)  # gỡ key không xoá được dấu thu hồi
    assert hs.post("/api/key-claude", json={"key": KEY}, headers=hs.h).status_code == 403
    assert ad.post(f"/admin/api/key-claude/{hs.uid}/mo-lai", json={}, headers=ad.h).json()["trang_thai"] == "chua_mo"
    assert hs.post("/api/key-claude", json={"key": KEY}, headers=hs.h).json()["da_mo"] is True
    assert ad.post(f"/admin/api/key-claude/{ad.uid}/thu-hoi", json={}, headers=ad.h).status_code == 400  # admin: không thu hồi
    assert _su_kien("thu_hoi_key_claude", "mo_lai_key_claude") == ["thu_hoi_key_claude", "mo_lai_key_claude"]


def test_admin_cli_claude_key(ai, capsys):
    import admin_cli

    admin_cli.con_sole.width = 240
    u = db.tao_tai_khoan_email("lan@example.com", "Lan", "x", da_xac_thuc=True)
    key_claude.kich_hoat(u, KEY)
    assert admin_cli.main(["claude-key", "list"]) == 0
    ra = capsys.readouterr().out
    assert "BẬT" in ra and "lan@example.com" in ra and "đã mở Claude" in ra and "0/10" in ra and KEY not in ra
    assert admin_cli.main(["claude-key", "revoke", "lan@example.com", "-y"]) == 0
    assert key_claude.trang_thai(u)["trang_thai"] == "bi_thu_hoi"
    assert admin_cli.main(["key", "allow", str(u.id)]) == 0 and key_claude.trang_thai(u)["trang_thai"] == "chua_mo"
    assert admin_cli.main(["claude-key", "revoke", "khong-co@example.com", "-y"]) == 1
    with db.ket_noi() as con:
        assert [tuple(r) for r in con.execute("SELECT event, method FROM activity_log WHERE event LIKE '%key_claude' ORDER BY id")] == [
            ("thu_hoi_key_claude", "cli"), ("mo_lai_key_claude", "cli")]


# ================================================================ key không bao giờ lộ
def test_key_khong_lo_ra_api_nhat_ky_log_frontend(ai, client, monkeypatch, caplog):
    caplog.set_level(logging.DEBUG)
    hs = _dang_nhap_email(monkeypatch, client, "hs@example.com")
    ad = _dang_nhap_email(monkeypatch, client, "admin@example.com", admin=True)
    tra_ve = [hs.post("/api/key-claude", json={"key": KEY + "x"}, headers=hs.h).text,
              hs.post("/api/key-claude", json={"key": KEY}, headers=hs.h).text,
              hs.get("/api/toi").text, hs.get("/api/key-claude").text, hs.get("/api/suc-khoe").text,
              ad.get("/admin/api/key-claude").text, ad.get("/admin/api/hoat-dong?gioi_han=200").text]
    tra_ve.append(_giai(hs)[0].__repr__())
    for t in tra_ve:
        assert KEY not in t
    with db.ket_noi() as con:
        for bang in ("activity_log", "claude_key_users", "claude_key_fails", "claude_key_uses", "ai_usage", "app_settings"):
            for r in con.execute(f"SELECT * FROM {bang}"):
                assert not any(KEY in str(v) for v in tuple(r)), bang
    assert KEY not in caplog.text and KEY not in repr(settings)
    for f in (GOC / "frontend").rglob("*"):
        if f.is_file() and f.suffix in (".js", ".html", ".css"):
            assert "CLAUDE_ACCESS_KEY=" not in f.read_text(encoding="utf-8", errors="ignore")
