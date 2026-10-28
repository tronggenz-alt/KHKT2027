"""Chọn mô hình theo độ khó (Flash-Lite / Flash / Pro / Claude), mã kích hoạt Claude, theo dõi chi phí AI.

AI giả ở TẦNG GỌI API (claude_chat.goi_claude, gemini_chat._goi_gemini) – PhienHoiThoai, pipeline, chuyển dự phòng, ghi chi phí đều chạy thật.
"""
import copy
import json
import re
import time
from datetime import date, timedelta
from pathlib import Path
from types import SimpleNamespace

import pytest

from backend import chi_phi_ai, db, do_kho, giai_nhanh, llm, ma_claude, parser, pipeline, research, solver
from backend.ai.providers.claude import chat as claude_chat
from backend.ai.providers.gemini import chat as gemini_chat, client as gemini_client
from backend.schemas import LoiGiai
from backend.bao_mat import bam_mat_khau, gioi_han_tan_suat
from backend.config import settings

DEMO = Path(__file__).resolve().parent.parent / "backend" / "demo_data"
BAI = json.loads((DEMO / "metan_etyl_axetat.json").read_text(encoding="utf-8"))
CHUOI = "CH4 → C2H2 → CH3CHO → C2H5OH → CH3COOH → CH3COOC2H5"
MAU_MA = re.compile(r"^CLD-[2-9A-HJ-NP-Z]{4}-[2-9A-HJ-NP-Z]{4}$")


def _phan_tich_json():
    sol = BAI["loi_giai"]
    return json.dumps({
        "de_bai": CHUOI,
        "cac_chat": [{"ki_hieu": n["ki_hieu"], "la_chat_an": False, "ten_goi_y": n["ten"], "ten_tieng_anh": n["ten"]} for n in sol["so_do"]],
        "cac_buoc": [{"so": b["so"], "chat_dau": b["chat_dau"], "chat_sau": b["chat_sau"], "dieu_kien_de_cho": ""} for b in sol["cac_buoc"]],
        "ghi_chu": "",
    }, ensure_ascii=False)


class AIGia:
    """Trả lời theo system prompt; ghi lại (nhà cung cấp, mô hình, system) của mọi lần gọi."""

    def __init__(self, monkeypatch):
        self.goi: list[tuple[str, str, str]] = []
        self.muc_do = "de"
        self.claude_loi: llm.LoiAI | None = None
        self.giai_sai = 0      # số lần giải đầu tiên trả lời sai cân bằng (RDKit bắt)
        self.json_hong = 0     # số lần giải đầu tiên trả JSON hỏng
        monkeypatch.setattr(claude_chat, "goi_claude", self._claude)
        monkeypatch.setattr(gemini_chat, "_goi_gemini", self._gemini)
        monkeypatch.setattr(research, "pubchem_lookup", lambda q: {"ok": True, "truy_van": q, "ten": q, "smiles": "C#C"})
        monkeypatch.setattr(research, "tra_cuu_truoc_pubchem", lambda de, emit: {})

    def _tra_loi(self, system: str) -> str:
        if system == do_kho.SYSTEM_PHAN_LOAI:
            return json.dumps({"muc_do": self.muc_do, "ly_do": "phân loại giả"})
        if system == parser.SYSTEM_PHAN_TICH:
            return _phan_tich_json()
        if system == research.SYSTEM_TRA_CUU:
            return "GHI CHÚ TRA CỨU: đủ dữ liệu."
        if system == giai_nhanh.SYSTEM_GIAI_GON:  # luồng mới: một lần gọi, JSON gọn
            if self.json_hong > 0:
                self.json_hong -= 1
                return "đây không phải JSON"
            gon = giai_nhanh.gon_tu_loi_giai(LoiGiai.model_validate(BAI["loi_giai"]))
            gon["luu_y"], gon["dap_an"] = ["Cộng nước vào acetylene: enol chuyển vị thành aldehyde"], "CH3COOC2H5 (ethyl acetate)"
            if self.giai_sai > 0:
                self.giai_sai -= 1
                gon["chuoi"][1]["smiles"] = "C(#C"  # SMILES hỏng → RDKit bắt
            return json.dumps(gon, ensure_ascii=False)
        if system == giai_nhanh.SYSTEM_PT:
            return json.dumps({"phuong_trinh": [{"buoc": b["so"], "phuong_trinh": b["phuong_trinh"], "ghi_chu": ""}
                                                for b in BAI["loi_giai"]["cac_buoc"]]}, ensure_ascii=False)
        if system == solver.SYSTEM_GIAI:
            if self.json_hong > 0:
                self.json_hong -= 1
                return "đây không phải JSON"
            sol = copy.deepcopy(BAI["loi_giai"])
            if self.giai_sai > 0:
                self.giai_sai -= 1
                sol["cac_buoc"][0]["chat_tham_gia"][0]["he_so"] = 1  # CH4 → C2H2 + 3H2 chưa cân bằng
            return json.dumps(sol, ensure_ascii=False)
        return "ok"

    def _claude(self, **kw):
        model = kw.get("model") or settings.model
        self.goi.append(("claude", model, kw["system"]))
        if self.claude_loi is not None:
            raise self.claude_loi
        return SimpleNamespace(content=[SimpleNamespace(type="text", text=self._tra_loi(kw["system"]))], stop_reason="end_turn",
                               model=model, usage=SimpleNamespace(input_tokens=1000, output_tokens=300))

    def _gemini(self, **kw):
        model = kw["uu_tien"]
        self.goi.append(("gemini", model, kw["system"]))
        part = SimpleNamespace(text=self._tra_loi(kw["system"]), thought=False, function_call=None)
        resp = SimpleNamespace(candidates=[SimpleNamespace(content=SimpleNamespace(parts=[part]), finish_reason="STOP")],
                               usage_metadata=SimpleNamespace(prompt_token_count=500, candidates_token_count=100, thoughts_token_count=40))
        return resp, model, kw["contents"]

    def theo(self, system: str) -> list[tuple[str, str]]:
        return [(n, m) for n, m, s in self.goi if s == system]


@pytest.fixture
def ai(monkeypatch, cau_hinh, tmp_path):
    monkeypatch.setattr(settings, "gemini_model_lite", "lite-test")
    monkeypatch.setattr(settings, "gemini_model", "flash-test")
    monkeypatch.setattr(settings, "gemini_model_hsg", "pro-test")
    monkeypatch.setattr(settings, "model", "claude-test")
    monkeypatch.setattr(llm, "_claude_tam_dung_den", 0.0)
    monkeypatch.setattr(llm, "_claude_ly_do_tam_dung", "")
    monkeypatch.setattr(pipeline, "OUTPUT_DIR", tmp_path / "out")
    return AIGia(monkeypatch)


def _giai(muc_do_toi_thieu: str = "", claude=None, uid=None) -> tuple[pipeline.Job, llm.KeHoachAI]:
    job = pipeline.tao_job(uid)
    kh = llm.KeHoachAI(loai="giai", user_id=uid, claude=claude)
    pipeline.chay(job, CHUOI, None, hien_chrome=False, muc_do_toi_thieu=muc_do_toi_thieu, ke_hoach=kh)
    assert job.loi is None, job.loi
    return job, kh


def _usage(**dk) -> list[dict]:
    with db.ket_noi() as con:
        rows = [dict(r) for r in con.execute("SELECT * FROM ai_usage ORDER BY id")]
    return [r for r in rows if all(r[k] == v for k, v in dk.items())]


# ================================================================ chọn mô hình theo độ khó
G = giai_nhanh.SYSTEM_GIAI_GON


def test_de_sgk_phan_loai_de_va_giai_bang_flash_lite(ai):
    job, _ = _giai()
    assert job.muc_do == "de" and job.ket_qua["muc_do"] == "de" and job.ket_qua["ten_muc_do"] == "Dễ"
    assert job.muc_tm == "nhanh" and job.ket_qua["muc_thong_minh"] == "nhanh"   # Dễ → mức Nhanh
    assert ai.theo(do_kho.SYSTEM_PHAN_LOAI) == [("gemini", "lite-test")]   # phân loại bằng mô hình rẻ nhất
    assert ai.theo(G) == [("gemini", "lite-test")]                       # MỘT lần gọi gộp phân tích + tra cứu + giải
    assert not ai.theo(parser.SYSTEM_PHAN_TICH) and not ai.theo(research.SYSTEM_TRA_CUU) and not ai.theo(solver.SYSTEM_GIAI)
    assert not [g for g in ai.goi if g[0] == "claude"]
    assert any(e.get("muc_do") == "de" for e in job.su_kien) and any(e.get("muc_tm") == "nhanh" for e in job.su_kien)
    giai = _usage(step="giai")
    assert giai and giai[0]["model"] == "lite-test" and giai[0]["tier"] == "lite" and giai[0]["level"] == "de"
    assert (giai[0]["tokens_in"], giai[0]["tokens_out"], giai[0]["tokens_think"]) == (500, 140, 40)  # đầu ra kể cả suy nghĩ
    assert {r["step"] for r in _usage()} == {"phan_loai", "giai"}
    assert job.ket_qua["tokens"] == {"so_lan": 2, "vao": 1000, "ra": 280}


def test_trung_binh_giai_bang_flash(ai):
    ai.muc_do = "trung_binh"
    job, _ = _giai()
    assert ai.theo(G) == [("gemini", "flash-test")] and job.muc_tm == "can_bang"


def test_hsg_khong_co_ma_giai_bang_gemini_pro_khong_goi_claude(ai):
    ai.muc_do = "hsg"
    job, kh = _giai()
    assert job.muc_do == "hsg" and job.muc_tm == "ky" and ai.theo(G) == [("gemini", "pro-test")]
    assert not [g for g in ai.goi if g[0] == "claude"] and not kh.claude_thanh_cong


def test_hsg_co_quyen_claude_giai_bang_claude(ai):
    ai.muc_do = "hsg"
    job, kh = _giai(claude=ma_claude.QuyenClaude(la_admin=True))
    assert ai.theo(G) == [("claude", "claude-test")]
    assert kh.claude_thanh_cong and job.ket_qua["dung_claude"] is True and job.ket_qua["mo_hinh"] == "Claude (claude-test)"
    assert ai.theo(do_kho.SYSTEM_PHAN_LOAI) == [("gemini", "lite-test")]  # phân loại vẫn dùng Gemini rẻ
    r = _usage(provider="claude")[0]
    assert (r["model"], r["tokens_in"], r["tokens_out"], r["ok"], r["tier"]) == ("claude-test", 1000, 300, 1, "claude")


def test_de_de_co_quyen_claude_van_khong_dung_claude(ai):
    _giai(claude=ma_claude.QuyenClaude(la_admin=True))  # tự động + chưa bật "Dùng Claude": Dễ → Nhanh (Flash-Lite)
    assert not [g for g in ai.goi if g[0] == "claude"]


def test_claude_loi_tu_chuyen_gemini_pro_va_bao_nguoi_dung(ai):
    ai.muc_do = "hsg"
    ai.claude_loi = llm.LoiAI("Tài khoản Anthropic đã hết tín dụng", "het_tien")
    job, kh = _giai(claude=ma_claude.QuyenClaude(la_admin=True))
    assert ai.theo(G) == [("claude", "claude-test"), ("gemini", "pro-test")]
    assert any("chuyển sang Gemini Pro" in e["thong_diep"] for e in job.su_kien)
    assert not kh.claude_thanh_cong and job.ket_qua["dung_claude"] is False
    loi = _usage(provider="claude")[0]
    assert loi["ok"] == 0 and loi["error"] == "het_tien"


def test_rdkit_bat_loi_thi_sua_theo_muc(ai):
    # Nhanh: bỏ kiểm tra AI – lỗi RDKit chỉ được đánh dấu
    ai.giai_sai = 1
    job, _ = _giai()
    assert ai.theo(G) == [("gemini", "lite-test")] and any("C2H2" in x for x in job.ket_qua["canh_bao"])
    # Cân bằng: mô hình rẻ (Flash-Lite, phiên mới) sửa
    ai.goi.clear()
    ai.muc_do, ai.giai_sai = "trung_binh", 1
    job, _ = _giai()
    assert ai.theo(G) == [("gemini", "flash-test"), ("gemini", "lite-test")] and job.ket_qua["canh_bao"] == []
    assert any("nhờ AI sửa (mô hình rẻ)" in e["thong_diep"] for e in job.su_kien)
    # Kỹ: chính mô hình đã giải (cùng phiên) sửa
    ai.goi.clear()
    ai.muc_do, ai.giai_sai = "hsg", 1
    job, _ = _giai()
    assert ai.theo(G) == [("gemini", "pro-test"), ("gemini", "pro-test")] and job.ket_qua["canh_bao"] == []


def test_json_hong_2_lan_thi_len_muc(ai):
    ai.json_hong = 2
    job, kh = _giai()
    assert ai.theo(G) == [("gemini", "lite-test"), ("gemini", "lite-test"), ("gemini", "flash-test")]
    assert any("Kết quả hỏng → giải lại ở mức Cân bằng" in e["thong_diep"] for e in job.su_kien) and kh.nang_cap
    assert _usage(step="giai", model="flash-test")[0]["upgraded"] == 1


def test_khong_len_claude_khi_khong_co_ma(ai):
    ai.muc_do, ai.giai_sai = "hsg", 5
    job, _ = _giai()
    assert {m for _, m in ai.theo(G)} == {"pro-test"} and len(ai.theo(G)) == 3  # Kỹ: sửa tối đa 2 vòng trong phiên Pro
    assert not [g for g in ai.goi if g[0] == "claude"] and job.ket_qua["canh_bao"]


def test_nang_muc_thu_cong(ai):
    job, _ = _giai(muc_do_toi_thieu="hsg")
    assert job.muc_do == "hsg" and job.muc_tm == "ky" and ai.theo(G) == [("gemini", "pro-test")]
    assert any("Mức độ đề: Dễ" in e["thong_diep"] and "bạn chọn nâng lên HSG" in e["thong_diep"] for e in job.su_kien)


def test_ham_bac_va_muc():
    assert do_kho.cao_hon("de", "hsg") == "hsg" and do_kho.cao_hon("trung_binh", "") == "trung_binh"
    assert [do_kho.bac_tiep(b, False) for b in ("lite", "flash", "pro")] == ["flash", "pro", None]
    assert do_kho.bac_tiep("pro", True) == "claude" and do_kho.bac_tiep("claude", True) is None
    assert do_kho.bac_giai("hsg", True) == "claude" and do_kho.bac_giai("hsg", False) == "pro" and do_kho.bac_giai("de", True) == "lite"


def test_phan_loai_loi_thi_dung_muc_trung_binh(ai, monkeypatch):
    monkeypatch.setattr(gemini_chat, "_goi_gemini", lambda **kw: (_ for _ in ()).throw(llm.LoiAI("quá tải", "qua_tai")))
    kq = do_kho.phan_loai("đề gì đó")
    assert kq["muc_do"] == "trung_binh" and kq["loi"]


def test_mac_dinh_khong_dung_claude(ai):
    p = llm.PhienHoiThoai("sys")  # không có kế hoạch: Gemini bậc flash
    assert p.nha_cung_cap == "gemini" and p.gemini_uu_tien == "flash-test"
    p.gui(text="xin chào")
    assert _usage() == []  # gọi ngoài kế hoạch (không gắn yêu cầu nào) → không ghi chi phí
    with llm.dung_ke_hoach(llm.KeHoachAI(bac="claude", user_id=7)):  # xin bậc claude nhưng không có quyền → Gemini Pro
        p = llm.PhienHoiThoai("sys")
    assert p.nha_cung_cap == "gemini" and p.bac == "pro" and "chưa mở Claude" in p.loi_claude
    p.gui(text="xin chào")  # phiên thuộc kế hoạch (kể cả gọi sau khối with) → ghi chi phí
    assert not [g for g in ai.goi if g[0] == "claude"]
    assert [(r["user_id"], r["tier"], r["model"]) for r in _usage()] == [(7, "pro", "pro-test")]


# ================================================================ mã Claude
def _nguoi(email="hs@example.com"):
    return db.tao_tai_khoan_email(email, "Học Sinh", "x", da_xac_thuc=True)


def _han(ngay=30):
    return (date.today() + timedelta(days=ngay)).isoformat()


def test_tao_ma_chi_luu_ban_bam(cau_hinh):
    ma, ma_id = ma_claude.tao_ma(20, _han(), 1, "cho Lan")
    assert MAU_MA.fullmatch(ma)
    with db.ket_noi() as con:
        r = dict(con.execute("SELECT * FROM claude_codes WHERE id = ?", (ma_id,)).fetchone())
    assert ma not in json.dumps(r) and ma.replace("-", "") not in json.dumps(r) and r["hint"] == ma[-4:]
    assert r["code_hash"] == ma_claude.bam(ma) == ma_claude.bam(ma.lower().replace("-", " "))  # nhập kiểu nào cũng khớp
    ds = ma_claude.danh_sach()
    assert ds[0]["goi_y"] == f"CLD-…-{ma[-4:]}" and ma not in json.dumps(ds)
    with pytest.raises(ma_claude.LoiMaClaude):
        ma_claude.tao_ma(5, (date.today() - timedelta(days=1)).isoformat())


def test_giu_tru_tra_luot_nguyen_tu(cau_hinh):
    ma, ma_id = ma_claude.tao_ma(2, _han())
    u = _nguoi()
    ma_claude.gan_ma(u, ma)
    q1, q2 = ma_claude.quyen(u), ma_claude.quyen(u)
    assert q1.giu() and q1.giu() and q2.giu()  # giữ 1 lượt mỗi yêu cầu (giữ lại lần nữa không tốn thêm)
    assert ma_claude.trang_thai(u)["con_luot"] == 0 and ma_claude.quyen(u) is None  # 2 bài đang giải giữ hết 2 lượt
    q3 = ma_claude.QuyenClaude(ma_id=ma_id)
    assert not q3.giu()  # không vượt tổng lượt kể cả khi gọi đồng thời
    assert q1.chot() and not q1.chot()  # bài 1 giải xong bằng Claude → trừ hẳn (một lần)
    q2.tra()  # bài 2 lỗi → trả lượt
    tt = ma_claude.trang_thai(u)
    assert tt["con_luot"] == 1 and tt["co_the_dung"]
    x = ma_claude.tim(str(ma_id))
    assert (x["da_dung"], x["dang_giu"], x["con_lai"]) == (1, 0, 1)
    assert ma_claude.QuyenClaude(la_admin=True).giu()  # admin không cần mã


def test_ma_het_han_thu_hoi_du_tai_khoan_cong_luot(cau_hinh):
    ma, ma_id = ma_claude.tao_ma(1, _han(), so_tai_khoan=1)
    a, b = _nguoi("a@example.com"), _nguoi("b@example.com")
    ma_claude.gan_ma(a, ma)
    with pytest.raises(ma_claude.LoiMaClaude, match="đủ số tài khoản"):
        ma_claude.gan_ma(b, ma)
    assert ma_claude.them_luot(ma_id, 4)["tong_luot"] == 5
    with db.ket_noi() as con:
        con.execute("UPDATE claude_codes SET expires = ? WHERE id = ?", ((date.today() - timedelta(days=1)).isoformat(), ma_id))
    assert ma_claude.trang_thai(a)["trang_thai"] == "het_han" and ma_claude.quyen(a) is None
    with db.ket_noi() as con:
        con.execute("UPDATE claude_codes SET expires = ? WHERE id = ?", (_han(), ma_id))
    assert ma_claude.quyen(a) is not None
    ma_claude.thu_hoi(ma_id)
    assert ma_claude.quyen(a) is None and ma_claude.trang_thai(a)["ten_trang_thai"] == "đã thu hồi"


# ================================================================ API: chỉ backend quyết định Claude
def _dang_nhap_email(monkeypatch, client, email: str, admin: bool = False):
    from fastapi.testclient import TestClient

    from backend.main import app

    monkeypatch.setattr(settings, "google_client_id", "")
    monkeypatch.setattr(settings, "google_client_secret", "")
    u = db.tim_theo_email(email) or db.tao_tai_khoan_email(email, email.split("@")[0], bam_mat_khau("mat-khau-123"), da_xac_thuc=True)
    if admin:
        db.dat_admin(u.id, True)
    c = TestClient(app, base_url="https://testserver", client=("203.0.113.60" if admin else "203.0.113.61", 50000))
    h = {"X-CSRF-Token": c.get("/api/toi").json()["csrf"]}
    assert c.post("/auth/dang-nhap-mat-khau", json={"email": email, "mat_khau": "mat-khau-123"}, headers=h).json()["ok"]
    c.h = {"X-CSRF-Token": c.get("/api/toi").json()["csrf"]}
    c.uid = u.id
    return c


def _cho_xong(job_id: str):
    for _ in range(400):
        job = pipeline.JOBS[job_id]
        if job.xong:
            with db.ket_noi() as con:
                if con.execute("SELECT status FROM solutions WHERE job_id = ?", (job_id,)).fetchone()[0] != "dang_giai":
                    return job
        time.sleep(0.025)
    raise AssertionError("bài chưa giải xong")


def test_ma_2_luot_giai_2_bai_hsg_bang_claude_lan_3_ve_gemini_pro(ai, client, monkeypatch):
    ai.muc_do = "hsg"
    monkeypatch.setattr(settings, "free_trial_limit", 10)  # 4 bài (bài bị từ chối Claude vẫn giải bằng Gemini) > 3 lượt dùng thử
    ad = _dang_nhap_email(monkeypatch, client, "admin@example.com", admin=True)
    hs = _dang_nhap_email(monkeypatch, client, "hs@example.com")
    assert hs.get("/api/toi").json()["claude"]["co_the_dung"] is False
    # gọi thẳng API giải xin Claude khi chưa có mã / Key Claude → backend TỪ CHỐI Claude, bài vẫn giải bằng Gemini (kèm thông báo)
    r = hs.post("/api/giai", data={"chuoi": CHUOI, "dung_claude": "true"}, headers=hs.h)
    assert r.status_code == 200 and r.json()["dung_claude"] is False and "từ chối dùng Claude" in r.json()["thong_bao_claude"]
    job = _cho_xong(r.json()["job_id"])
    assert job.ket_qua["mo_hinh"] == "Gemini (pro-test)" and not any(g[0] == "claude" for g in ai.goi)
    assert any("từ chối dùng Claude" in e["thong_diep"] for e in job.su_kien)
    ma = ad.post("/admin/api/ma-claude", json={"so_luot": 2, "het_han": _han(), "so_tai_khoan": 1, "ghi_chu": "cho HS"}, headers=ad.h).json()["ma_goc"]
    d = hs.post("/api/ma-claude", json={"ma": ma.lower()}, headers=hs.h).json()
    assert d["ok"] and d["con_luot"] == 2 and d["het_han"] == _han()
    assert hs.get("/api/toi").json()["claude"] == {**hs.get("/api/toi").json()["claude"], "co_the_dung": True, "con_luot": 2}
    for con_lai in (1, 0):
        gioi_han_tan_suat.xoa()
        job = _cho_xong(hs.post("/api/giai", data={"chuoi": CHUOI, "dung_claude": "true"}, headers=hs.h).json()["job_id"])
        assert job.loi is None and job.ket_qua["dung_claude"] is True and job.ket_qua["mo_hinh"] == "Claude (claude-test)"
        assert any(f"Đã trừ 1 lượt mã Claude (còn {con_lai} lượt)" in e["thong_diep"] for e in job.su_kien)
        assert hs.get("/api/toi").json()["claude"].get("con_luot") == con_lai
    n_claude = len([g for g in ai.goi if g[0] == "claude"])
    gioi_han_tan_suat.xoa()
    r = hs.post("/api/giai", data={"chuoi": CHUOI, "dung_claude": "true"}, headers=hs.h)  # lượt thứ 3: mã hết lượt
    assert r.status_code == 200
    job = _cho_xong(r.json()["job_id"])
    assert job.ket_qua["mo_hinh"] == "Gemini (pro-test)" and job.ket_qua["dung_claude"] is False
    assert any("hết lượt – không dùng Claude" in e["thong_diep"] for e in job.su_kien)
    assert len([g for g in ai.goi if g[0] == "claude"]) == n_claude
    with db.ket_noi() as con:
        assert tuple(con.execute("SELECT level, claude FROM solutions WHERE job_id = ?", (job.id,)).fetchone()) == ("hsg", 0)
    # mã không đủ lượt vẫn hiện trong trang admin: đã dùng 2/2
    x = ad.get("/admin/api/ma-claude").json()["ma"][0]
    assert (x["da_dung"], x["con_lai"], x["trang_thai"]) == (2, 0, "het_luot") and x["nguoi_dung"][0]["so_bai"] == 2
    # trang Chi phí: đúng mô hình + token của các lần trên
    cp = ad.get("/admin/api/chi-phi?ngay=1").json()
    theo = {r["model"]: r for r in cp["theo_mo_hinh"]}
    assert theo["claude-test"]["so_lan"] == 2 and theo["claude-test"]["tokens_in"] == 2000 and theo["claude-test"]["tokens_out"] == 600
    assert theo["pro-test"]["so_lan"] == 2 and theo["lite-test"]["so_lan"] == 4  # mỗi bài 1 lần phân loại (Flash-Lite)
    assert cp["claude_nhieu_nhat"][0]["email"] == "hs@example.com" and cp["claude_nhieu_nhat"][0]["so_bai"] == 2
    assert {m["muc_do"]: m["so_bai"] for m in cp["ti_le_muc_do"]}["hsg"] == 4 and cp["bai_claude"] == 2


def test_nhap_sai_ma_5_lan_bi_khoa_1_gio(ai, client, monkeypatch):
    ma, _ = ma_claude.tao_ma(3, _han())
    hs = _dang_nhap_email(monkeypatch, client, "hs@example.com")
    for i in range(4):
        r = hs.post("/api/ma-claude", json={"ma": "CLD-AAAA-BBB" + "23456789"[i]}, headers=hs.h)
        assert r.status_code == 400 and f"còn {4 - i} lần" in r.json()["loi"]
    r = hs.post("/api/ma-claude", json={"ma": "CLD-AAAA-BBBB"}, headers=hs.h)
    assert r.status_code == 429 and r.json()["ma"] == "ma_claude_bi_khoa" and int(r.headers["Retry-After"]) > 3000
    r = hs.post("/api/ma-claude", json={"ma": ma}, headers=hs.h)  # đang bị khoá: mã ĐÚNG cũng không nhận
    assert r.status_code == 429
    assert hs.get("/api/toi").json()["claude"]["khoa_nhap_giay"] > 3000
    with db.ket_noi() as con:
        assert con.execute("SELECT COUNT(*) FROM activity_log WHERE event = 'nhap_sai_ma_claude'").fetchone()[0] == 5
        con.execute("UPDATE claude_code_fails SET ts = ts - 7200")  # 2 giờ sau
    r = hs.post("/api/ma-claude", json={"ma": ma}, headers=hs.h)
    assert r.status_code == 200 and r.json()["con_luot"] == 3


def test_admin_api_ma_claude_chi_admin(ai, client, monkeypatch):
    hs = _dang_nhap_email(monkeypatch, client, "hs@example.com")
    for url, body in (("/admin/api/ma-claude", {"so_luot": 1, "het_han": _han()}), ("/admin/api/ma-claude/1/thu-hoi", {}),
                      ("/admin/api/ma-claude/1/them-luot", {"so": 1})):
        assert hs.post(url, json=body, headers=hs.h).status_code == 403, url
    assert hs.get("/admin/api/ma-claude").status_code == 403 and hs.get("/admin/api/chi-phi").status_code == 403
    ad = _dang_nhap_email(monkeypatch, client, "admin@example.com", admin=True)
    d = ad.post("/admin/api/ma-claude", json={"so_luot": 5, "het_han": _han(), "ghi_chu": "cho Lan"}, headers=ad.h).json()
    assert MAU_MA.fullmatch(d["ma_goc"])
    assert d["ma_goc"] not in ad.get("/admin/api/ma-claude").text  # danh sách không bao giờ có mã gốc
    assert ad.post(f"/admin/api/ma-claude/{d['id']}/them-luot", json={"so": 3}, headers=ad.h).json()["tong_luot"] == 8
    assert ad.post(f"/admin/api/ma-claude/{d['id']}/thu-hoi", json={}, headers=ad.h).json()["trang_thai"] == "thu_hoi"
    with db.ket_noi() as con:
        ev = [r[0] for r in con.execute("SELECT event FROM activity_log WHERE event LIKE '%ma_claude' ORDER BY id")]
    assert ev == ["tao_ma_claude", "them_luot_ma_claude", "thu_hoi_ma_claude"]


def test_admin_cli_code_va_usage(ai, capsys):
    import admin_cli

    admin_cli.con_sole.width = 240
    with db.ket_noi():  # tạo CSDL tạm (admin_cli báo lỗi nếu chưa có CSDL)
        pass
    assert admin_cli.main(["code", "create", "--luot", "5", "--ghi-chu", "cho Lan"]) == 0
    ra = capsys.readouterr().out
    ma = re.search(r"CLD-[2-9A-HJ-NP-Z]{4}-[2-9A-HJ-NP-Z]{4}", ra).group()
    assert "chỉ hiện ĐÚNG MỘT LẦN" in ra
    assert admin_cli.main(["code", "list"]) == 0
    ra = capsys.readouterr().out
    assert "cho Lan" in ra and ma not in ra and f"CLD-…-{ma[-4:]}" in ra
    assert admin_cli.main(["code", "add", "1", "3"]) == 0 and "còn 8/8" in capsys.readouterr().out
    assert admin_cli.main(["code", "revoke", ma, "-y"]) == 0 and ma_claude.tim("1")["trang_thai"] == "thu_hoi"
    assert admin_cli.main(["code", "revoke", "99"]) == 1
    capsys.readouterr()
    _giai(uid=None)
    assert admin_cli.main(["usage", "--week"]) == 0
    ra = capsys.readouterr().out
    assert "lite-test" in ra and "Mức độ đề 7 ngày" in ra
    with db.ket_noi() as con:
        assert con.execute("SELECT COUNT(*) FROM activity_log WHERE event = 'tao_ma_claude' AND method = 'cli'").fetchone()[0] == 1


def test_uoc_tinh_tien():
    assert chi_phi_ai.gia("gemini-flash-lite-latest") == (0.25, 1.5) and chi_phi_ai.gia("gemini-pro-latest") == (2.0, 12.0)
    assert chi_phi_ai.gia("claude-sonnet-5-5") == (2.0, 10.0) and chi_phi_ai.gia("la") == (0.0, 0.0)
    assert round(chi_phi_ai.uoc_tinh_usd("claude-sonnet-5-5", 1_000_000, 100_000), 4) == 3.0
