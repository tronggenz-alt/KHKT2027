"""API key Claude RIÊNG của người dùng: lưu (kiểm tra max_tokens 1, mã hoá), dùng Claude không cần Key Claude chung, gọi bằng key của họ,
không trừ lượt / không dùng key máy chủ, nhật ký ghi "key người dùng" (claude_rieng), key lỗi → Gemini."""
import logging

import anthropic
import httpx
import pytest

from backend import bao_mat, claude_rieng, db, key_claude, llm
from backend.ai.providers.claude import chat as claude_chat
from backend.config import settings
from test_chon_mo_hinh import CHUOI, AIGia, _cho_xong, _dang_nhap_email
from backend.bao_mat import gioi_han_tan_suat
from backend import pipeline

KEY = "sk-ant-api03-" + "A1b2C3d4" * 10 + "-wxyz"


@pytest.fixture
def ai(monkeypatch, cau_hinh, tmp_path):
    for ten, gt in (("gemini_model_lite", "lite-test"), ("gemini_model", "flash-test"), ("gemini_model_hsg", "pro-test"), ("model", "claude-test")):
        monkeypatch.setattr(settings, ten, gt)
    monkeypatch.setattr(llm, "_claude_tam_dung_den", 0.0)
    monkeypatch.setattr(llm, "_claude_ly_do_tam_dung", "")
    monkeypatch.setattr(llm, "_tam_dung_rieng", {})
    monkeypatch.setattr(pipeline, "OUTPUT_DIR", tmp_path / "out")
    monkeypatch.setattr(settings, "free_trial_limit", 20)
    g = AIGia(monkeypatch)
    g.api_key: list = []
    goc = g._claude

    def claude(**kw):
        g.api_key.append(kw.get("api_key"))
        return goc(**kw)

    monkeypatch.setattr(claude_chat, "goi_claude", claude)
    monkeypatch.setattr(claude_rieng, "kiem_tra", lambda k: {"hop_le": True})
    return g


def _giai(c, **them):
    gioi_han_tan_suat.xoa()
    r = c.post("/api/giai", data={"chuoi": CHUOI, "dung_claude": "true", **them}, headers=c.h)
    assert r.status_code == 200, r.text
    return r.json(), _cho_xong(r.json()["job_id"])


def test_luu_kiem_tra_ma_hoa_khong_tra_key(ai, client, monkeypatch, caplog):
    caplog.set_level(logging.DEBUG)
    hs = _dang_nhap_email(monkeypatch, client, "hs@example.com")
    claude_rieng.cap_quyen(hs.uid, True)  # admin đã bật quyền dùng key riêng cho tài khoản
    r = hs.post("/api/claude-rieng", json={"key": "abc"}, headers=hs.h)
    assert r.status_code == 400 and "sk-ant-api03" in r.json()["loi"]
    monkeypatch.setattr(claude_rieng, "kiem_tra", lambda k: {"hop_le": False, "thong_diep": "Key không hợp lệ hoặc đã bị thu hồi."})
    r = hs.post("/api/claude-rieng", json={"key": KEY}, headers=hs.h)
    assert r.status_code == 400 and r.json()["loi"] == "Key không hợp lệ hoặc đã bị thu hồi." and not hs.get("/api/toi").json()["claude_rieng"]["co"]
    monkeypatch.setattr(claude_rieng, "kiem_tra", lambda k: {"hop_le": True, "canh_bao": "Key hợp lệ nhưng tài khoản Anthropic của bạn đã HẾT TÍN DỤNG"})
    d = hs.post("/api/claude-rieng", json={"key": f"  {KEY} "}, headers=hs.h).json()
    assert d["co"] and d["cuoi"] == "wxyz" and "HẾT TÍN DỤNG" in d["canh_bao"]
    toi = hs.get("/api/toi").text
    assert KEY not in toi and '"cuoi":"wxyz"' in toi.replace(" ", "")
    with db.ket_noi() as con:
        enc = con.execute("SELECT claude_key_enc FROM users WHERE email = 'hs@example.com'").fetchone()[0]
    assert enc and KEY not in enc and bao_mat.giai_ma_key_claude(enc) == KEY
    assert KEY not in caplog.text
    assert hs.delete("/api/claude-rieng", headers=hs.h).json()["co"] is False


def test_user_key_secret_va_key_cu_van_giai_ma(cau_hinh, monkeypatch):
    cu = bao_mat.ma_hoa_key_claude(KEY)  # chưa đặt USER_KEY_SECRET → khoá suy từ khoá chính
    monkeypatch.setattr(settings, "user_key_secret", "bi-mat-rieng-cho-key-nguoi-dung-0123456789")
    moi = bao_mat.ma_hoa_key_claude(KEY)
    assert bao_mat.giai_ma_key_claude(cu) == KEY == bao_mat.giai_ma_key_claude(moi)
    monkeypatch.setattr(settings, "user_key_secret", "")
    with pytest.raises(bao_mat.LoiBaoMat):
        bao_mat.giai_ma_key_claude(moi)  # bỏ USER_KEY_SECRET → key mã hoá bằng nó không đọc được (người dùng nhập lại)


def test_key_rieng_giai_bang_claude_cua_ho_khong_tru_luot(ai, client, monkeypatch):
    ai.muc_do = "de"
    monkeypatch.setattr(settings, "claude_access_key", "")   # không có Key Claude chung
    monkeypatch.setattr(settings, "api_key", "")             # máy chủ thậm chí không có ANTHROPIC_API_KEY
    hs = _dang_nhap_email(monkeypatch, client, "hs@example.com")
    claude_rieng.cap_quyen(hs.uid, True)  # admin đã bật quyền dùng key riêng cho tài khoản
    hs.post("/api/claude-rieng", json={"key": KEY}, headers=hs.h)
    q, tb = key_claude.chon_quyen(db.tim_theo_email("hs@example.com"))
    assert isinstance(q, claude_rieng.QuyenRieng) and tb == "" and KEY not in repr(q)
    d, job = _giai(hs)
    assert d["dung_claude"] is True and job.ket_qua["mo_hinh"] == "Claude (claude-test)"
    assert ai.api_key and all(k == KEY for k in ai.api_key)  # mọi lần gọi Claude đều bằng key của người dùng
    with db.ket_noi() as con:
        rows = {tuple(r) for r in con.execute("SELECT provider, key_source FROM ai_usage WHERE provider = 'claude'")}
        assert rows == {("claude", "claude_rieng")} and con.execute("SELECT COUNT(*) FROM claude_key_uses").fetchone()[0] == 0
    assert not any("Đã dùng 1 lượt Claude" in e["thong_diep"] or "trừ 1 lượt mã Claude" in e["thong_diep"] for e in job.su_kien)


def test_key_rieng_loi_bao_va_chuyen_gemini_khong_dung_key_may_chu(ai, client, monkeypatch):
    ai.muc_do = "hsg"
    ai.claude_loi = llm.LoiAI("Tài khoản Anthropic của bạn đã hết tín dụng (nạp thêm tại platform.claude.com).", "het_tien")
    hs = _dang_nhap_email(monkeypatch, client, "hs@example.com")
    claude_rieng.cap_quyen(hs.uid, True)  # admin đã bật quyền dùng key riêng cho tài khoản
    hs.post("/api/claude-rieng", json={"key": KEY}, headers=hs.h)
    d, job = _giai(hs)
    assert job.loi is None and job.ket_qua["mo_hinh"] == "Gemini (pro-test)"
    assert any("Key Claude của bạn lỗi" in e["thong_diep"] and "không dùng key Claude của máy chủ" in e["thong_diep"] for e in job.su_kien)
    assert None not in ai.api_key and llm._claude_tam_dung_den == 0.0  # không gọi key máy chủ, không tạm dừng Claude máy chủ
    n = len(ai.api_key)
    d, job = _giai(hs)  # trong 10 phút: key riêng đang lỗi → Gemini ngay, không gọi lại
    assert len(ai.api_key) == n and any("key Claude của bạn đang lỗi" in e["thong_diep"] for e in job.su_kien)


def test_kiem_tra_that_anh_xa_loi(monkeypatch):
    def loi(cls, ma, thong_diep="x"):
        return cls(thong_diep, response=httpx.Response(ma, request=httpx.Request("POST", "https://api.anthropic.com/v1/messages")), body=None)

    class Client:
        def __init__(self, loi_):
            self.messages = self
            self._loi = loi_

        def create(self, **kw):
            assert kw["max_tokens"] == 1
            if self._loi:
                raise self._loi

    for e, hop_le in ((loi(anthropic.AuthenticationError, 401), False), (loi(anthropic.BadRequestError, 400, "Your credit balance is too low"), True),
                      (None, True)):
        monkeypatch.setattr(claude_rieng.anthropic, "Anthropic", lambda **kw: Client(e))
        assert claude_rieng.kiem_tra(KEY)["hop_le"] is hop_le
    monkeypatch.setattr(claude_rieng.anthropic, "Anthropic", lambda **kw: Client(loi(anthropic.InternalServerError, 529)))
    with pytest.raises(claude_rieng.LoiKeyRieng):
        claude_rieng.kiem_tra(KEY)


# ================================================================ quyền theo tài khoản (admin bật / tắt), yêu cầu kích hoạt, CLI
def _admin(monkeypatch, client):
    return _dang_nhap_email(monkeypatch, client, "admin@example.com", admin=True)


def _su_kien(*ten):
    with db.ket_noi() as con:
        return [r[0] for r in con.execute(f"SELECT event FROM activity_log WHERE event IN ({','.join('?' * len(ten))}) ORDER BY id", ten)]


def test_chua_duoc_bat_an_muc_403_va_gui_yeu_cau(ai, client, monkeypatch):
    hs = _dang_nhap_email(monkeypatch, client, "hs@example.com")
    assert hs.get("/api/toi").json()["claude_rieng"] == {"duoc_phep": False, "co": False, "cuoi": "", "da_yeu_cau": False, "yeu_cau_luc": ""}
    for phuong_thuc, url, body in (("post", "/api/claude-rieng", {"key": KEY}), ("delete", "/api/claude-rieng", None),
                                   ("post", "/api/claude-rieng/dung", {"dung": True})):
        r = getattr(hs, phuong_thuc)(url, **({"json": body} if body else {}), headers=hs.h)
        assert r.status_code == 403 and r.json()["ma"] == "khong_duoc_phep", url
    d = hs.post("/api/claude-rieng/yeu-cau", headers=hs.h).json()
    assert d["moi"] is True and d["da_yeu_cau"] and d["yeu_cau_luc"]
    assert hs.post("/api/claude-rieng/yeu-cau", headers=hs.h).json()["moi"] is False  # đang chờ → không ghi lại
    ad = _admin(monkeypatch, client)
    k = next(x for x in ad.get("/admin/api/nguoi-dung").json()["nguoi_dung"] if x["id"] == hs.uid)["key_claude_rieng"]
    assert (k["bat"], k["trang_thai"], bool(k["yeu_cau_luc"])) == (False, "chua_nhap", True)
    assert ad.get("/api/toi").json()["hoat_dong_moi"] >= 1  # chấm đỏ báo admin
    assert hs.post(f"/admin/api/nguoi-dung/{hs.uid}/key-claude-rieng", json={"bat": True}, headers=hs.h).status_code == 403
    d = ad.post(f"/admin/api/nguoi-dung/{hs.uid}/key-claude-rieng", json={"bat": False, "tu_choi": True}, headers=ad.h).json()
    assert d["yeu_cau_luc"] == "" and _su_kien("yeu_cau_key_claude_rieng", "tu_choi_key_claude_rieng") == [
        "yeu_cau_key_claude_rieng", "tu_choi_key_claude_rieng"]
    assert ad.post(f"/admin/api/nguoi-dung/{ad.uid}/key-claude-rieng", json={"bat": True}, headers=ad.h).status_code == 400  # admin: luôn được


def test_bat_tat_bat_lai_30_ngay_va_dung_key_cua_toi(ai, client, monkeypatch):
    ai.muc_do = "hsg"
    monkeypatch.setattr(settings, "claude_access_key", "")
    hs = _dang_nhap_email(monkeypatch, client, "hs@example.com")
    ad = _admin(monkeypatch, client)
    d = ad.post(f"/admin/api/nguoi-dung/{hs.uid}/key-claude-rieng", json={"bat": True}, headers=ad.h).json()
    assert d["bat"] and d["bat_boi"] == "admin@example.com" and d["bat_luc"]
    assert hs.post("/api/claude-rieng", json={"key": KEY}, headers=hs.h).json()["co"]
    toi = hs.get("/api/toi").json()["claude_rieng"]
    assert (toi["duoc_phep"], toi["co"], toi["dung"]) == (True, True, True)
    _, job = _giai(hs)
    assert job.ket_qua["mo_hinh"] == "Claude (claude-test)" and ai.api_key[-1] == KEY
    with db.ket_noi() as con:
        assert {r[0] for r in con.execute("SELECT key_source FROM ai_usage WHERE provider = 'claude'")} == {"claude_rieng"}  # "key người dùng"
    # người dùng tắt "Dùng key của tôi" → giải theo quy tắc bình thường (không key chung → Gemini)
    hs.post("/api/claude-rieng/dung", json={"dung": False}, headers=hs.h)
    n = len(ai.api_key)
    d, job = _giai(hs)
    assert len(ai.api_key) == n and job.ket_qua["mo_hinh"] == "Gemini (pro-test)" and d["dung_claude"] is False
    hs.post("/api/claude-rieng/dung", json={"dung": True}, headers=hs.h)
    # admin TẮT → mục ẩn, key vô hiệu ngay nhưng còn trong CSDL
    ad.post(f"/admin/api/nguoi-dung/{hs.uid}/key-claude-rieng", json={"bat": False}, headers=ad.h)
    assert hs.get("/api/toi").json()["claude_rieng"]["duoc_phep"] is False
    d, job = _giai(hs)
    assert len(ai.api_key) == n and d["dung_claude"] is False
    k = ad.get(f"/admin/api/nguoi-dung/{hs.uid}").json()["key_claude_rieng"]
    assert (k["bat"], k["trang_thai"], k["xoa_sau_ngay"]) == (False, "da_nhap", claude_rieng.GIU_KHI_TAT_NGAY)
    # bật lại trong 30 ngày → key cũ dùng tiếp
    ad.post(f"/admin/api/nguoi-dung/{hs.uid}/key-claude-rieng", json={"bat": True}, headers=ad.h)
    _, job = _giai(hs)
    assert ai.api_key[-1] == KEY and job.ket_qua["dung_claude"] is True
    # tắt quá 30 ngày → xoá hẳn
    ad.post(f"/admin/api/nguoi-dung/{hs.uid}/key-claude-rieng", json={"bat": False}, headers=ad.h)
    with db.ket_noi() as con:
        con.execute("UPDATE users SET own_claude_key_off_at = own_claude_key_off_at - 31 * 86400 WHERE id = ?", (hs.uid,))
    assert claude_rieng.don_het_han() == 1
    ad.post(f"/admin/api/nguoi-dung/{hs.uid}/key-claude-rieng", json={"bat": True}, headers=ad.h)
    assert hs.get("/api/toi").json()["claude_rieng"]["co"] is False
    assert _su_kien("bat_key_claude_rieng", "tat_key_claude_rieng") == ["bat_key_claude_rieng", "tat_key_claude_rieng"] * 2 + ["bat_key_claude_rieng"]


def test_key_loi_hien_cho_admin_va_admin_dung_key_rieng_truoc(ai, client, monkeypatch):
    ai.muc_do = "hsg"
    hs = _dang_nhap_email(monkeypatch, client, "hs@example.com")
    claude_rieng.cap_quyen(hs.uid, True)
    hs.post("/api/claude-rieng", json={"key": KEY}, headers=hs.h)
    ai.claude_loi = llm.LoiAI("Tài khoản Anthropic của bạn đã hết tín dụng.", "het_tien")
    _giai(hs)
    k = claude_rieng.trang_thai_admin(hs.uid)
    assert (k["trang_thai"], k["ten_trang_thai"]) == ("loi", "Key lỗi") and "hết tín dụng" in k["loi"]
    ai.claude_loi = None
    monkeypatch.setattr(llm, "_tam_dung_rieng", {})
    _giai(hs)
    assert claude_rieng.trang_thai_admin(hs.uid)["trang_thai"] == "da_nhap"  # chạy lại được → bỏ nhãn lỗi
    ad = _admin(monkeypatch, client)  # admin có key riêng + "Dùng key của tôi" → dùng key riêng trước key máy chủ
    ad.post("/api/claude-rieng", json={"key": KEY.replace("wxyz", "admn")}, headers=ad.h)
    _giai(ad)
    assert ai.api_key[-1].endswith("admn")


def test_cli_ownkey(ai, client, monkeypatch, capsys):
    import admin_cli

    admin_cli.con_sole.width = 220
    hs = _dang_nhap_email(monkeypatch, client, "hs@example.com")
    hs.post("/api/claude-rieng/yeu-cau", headers=hs.h)
    assert admin_cli.main(["ownkey", "list"]) == 0
    assert "ĐANG YÊU CẦU" in capsys.readouterr().out
    assert admin_cli.main(["ownkey", "on", "hs@example.com"]) == 0 and claude_rieng.duoc_phep(db.lay_nguoi_dung(hs.uid))
    assert "BẬT" in capsys.readouterr().out
    assert admin_cli.main(["ownkey", "list"]) == 0
    ra = capsys.readouterr().out
    assert "hs@example.com" in ra and "BẬT" in ra and "ĐANG YÊU CẦU" not in ra  # duyệt xong thì hết yêu cầu
    assert admin_cli.main(["ownkey", "off", str(hs.uid)]) == 0 and not claude_rieng.duoc_phep(db.lay_nguoi_dung(hs.uid))
    assert admin_cli.main(["ownkey", "on", "khong-co@example.com"]) == 1
    with db.ket_noi() as con:
        assert [tuple(r) for r in con.execute("SELECT event, method FROM activity_log WHERE event LIKE '%key_claude_rieng' AND method = 'cli'")] == [
            ("bat_key_claude_rieng", "cli"), ("tat_key_claude_rieng", "cli")]
