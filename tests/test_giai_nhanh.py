"""Giải nhanh dạng chuỗi: thanh mức độ thông minh, quyền mức Tối đa, cache 7 ngày, "Xem phương trình chi tiết" chỉ gọi AI khi bấm,
streaming Gemini (gộp mảnh), mức suy nghĩ theo .env, prompt caching Claude. AI giả ở tầng gọi API (AIGia)."""
from types import SimpleNamespace

import pytest

from backend import do_kho, giai_nhanh, key_claude, llm, ma_claude, pipeline
from backend.ai.providers.claude import chat as claude_chat
from backend.ai.providers.gemini import chat as gemini_chat, client as gemini_client
from backend.bao_mat import gioi_han_tan_suat
from backend.config import settings
from test_chon_mo_hinh import CHUOI, AIGia, _cho_xong, _dang_nhap_email, _giai

G = giai_nhanh.SYSTEM_GIAI_GON
KEY = "khoa-thu-Kc9"


@pytest.fixture
def ai(monkeypatch, cau_hinh, tmp_path):
    for ten, gt in (("gemini_model_lite", "lite-test"), ("gemini_model", "flash-test"), ("gemini_model_hsg", "pro-test"),
                    ("model", "claude-test"), ("claude_model_manh", "claude-manh-test"), ("claude_access_key", KEY),
                    ("free_trial_limit", 30)):
        monkeypatch.setattr(settings, ten, gt)
    monkeypatch.setattr(llm, "_claude_tam_dung_den", 0.0)
    monkeypatch.setattr(pipeline, "OUTPUT_DIR", tmp_path / "out")
    return AIGia(monkeypatch)


# ================================================================ chọn mức + cấu hình theo .env
def test_chon_muc_va_quyen_toi_da(cau_hinh, monkeypatch):
    key = key_claude.QuyenKey(user_id=1)
    ma = ma_claude.QuyenClaude(ma_id=1)  # mã Claude: chỉ bài HSG
    assert giai_nhanh.chon_muc("", "de", None, False) == ("nhanh", False, "")
    assert giai_nhanh.chon_muc("", "trung_binh", None, False)[:2] == ("can_bang", False)
    assert giai_nhanh.chon_muc("", "hsg", None, False)[:2] == ("ky", False)
    assert giai_nhanh.chon_muc("", "de", key, True)[:2] == ("ky", True)        # bật "Dùng Claude" → ít nhất Kỹ
    assert giai_nhanh.chon_muc("nhanh", "de", key, True)[:2] == ("nhanh", False)  # tự chọn Nhanh → Gemini Flash-Lite
    muc, claude, tb = giai_nhanh.chon_muc("toi_da", "de", None, False)
    assert (muc, claude) == ("ky", False) and "Tối đa chỉ dành" in tb
    assert giai_nhanh.chon_muc("toi_da", "trung_binh", key, False)[:2] == ("toi_da", True)
    assert giai_nhanh.chon_muc("ky", "trung_binh", ma, True)[:2] == ("ky", False)  # mã Claude: đề không HSG → Gemini Pro
    assert giai_nhanh.chon_muc("ky", "hsg", ma, True)[:2] == ("ky", True)
    monkeypatch.setattr(settings, "thinking_ky", 777)
    monkeypatch.setattr(settings, "max_tokens_toi_da", 999)
    assert giai_nhanh.cau_hinh_muc("ky", False).suy_nghi == 777
    c = giai_nhanh.cau_hinh_muc("toi_da", True)
    assert (c.bac, c.max_tokens, c.claude_model, c.kiem_tra) == ("claude", 999, settings.claude_model_manh, "ky")
    assert (giai_nhanh.cau_hinh_muc("nhanh", True).bac, giai_nhanh.cau_hinh_muc("nhanh", True).kiem_tra) == ("lite", "bo")


def test_muc_thu_cong_va_toi_da_qua_api(ai, client, monkeypatch):
    hs = _dang_nhap_email(monkeypatch, client, "hs@example.com")

    def giai(**form):
        gioi_han_tan_suat.xoa()
        r = hs.post("/api/giai", data={"chuoi": CHUOI, **form}, headers=hs.h)
        assert r.status_code == 200, r.text
        return r.json(), _cho_xong(r.json()["job_id"])

    d, job = giai(muc_thong_minh="can_bang")
    assert job.muc_tm == "can_bang" and ai.theo(G)[-1] == ("gemini", "flash-test")
    assert not ai.theo(do_kho.SYSTEM_PHAN_LOAI)  # tự chọn mức → không cần phân loại
    assert job.ket_qua["muc_thong_minh"] == "can_bang" and d["thong_bao_claude"] == ""
    d, job = giai(muc_thong_minh="toi_da")  # chưa có quyền Claude → hạ về Kỹ (Gemini Pro)
    assert job.muc_tm == "ky" and ai.theo(G)[-1] == ("gemini", "pro-test")
    assert any("Tối đa chỉ dành" in e["thong_diep"] for e in job.su_kien)
    hs.post("/api/key-claude", json={"key": KEY}, headers=hs.h)
    d, job = giai(muc_thong_minh="toi_da")  # có Key Claude: Tối đa = Claude mạnh nhất, kể cả khi chưa bật công tắc
    assert job.muc_tm == "toi_da" and ai.theo(G)[-2:] == [("claude", "claude-manh-test")] * 2 and job.ket_qua["dung_claude"] is True
    assert len([g for g in ai.theo(G) if g[1] == "claude-manh-test"]) == 2  # Tối đa: luôn rà soát thêm một lượt


# ================================================================ cache 7 ngày
def test_cache_cung_de_cung_muc_khong_goi_ai(ai, monkeypatch):
    monkeypatch.setattr(settings, "cache_giai_ngay", 7)
    job1, _ = _giai()
    n = len(ai.goi)
    job2, _ = _giai()  # cùng đề, cùng mức (tự động) → không gọi AI nào (kể cả phân loại)
    assert len(ai.goi) == n and job2.ket_qua["tu_cache"] is True and job1.ket_qua["tu_cache"] is False
    assert job2.ket_qua["gon"] == job1.ket_qua["gon"] and (job2.thu_muc / "dap_an.png").exists()
    assert any("dùng lại kết quả đã lưu" in e["thong_diep"] for e in job2.su_kien)
    job3 = pipeline.tao_job()
    pipeline.chay(job3, "  CH4 → C2H2 →  CH3CHO → C2H5OH → CH3COOH → CH3COOC2H5 ", None, False, muc_thong_minh="nhanh",
                  ke_hoach=llm.KeHoachAI(loai="giai"))
    assert job3.ket_qua["tu_cache"] is True  # chữ được chuẩn hoá (khoảng trắng) trước khi băm; "nhanh" = mức tự động đã chọn
    job4 = pipeline.tao_job()
    pipeline.chay(job4, CHUOI, None, False, muc_thong_minh="can_bang", ke_hoach=llm.KeHoachAI(loai="giai"))
    assert job4.ket_qua["tu_cache"] is False and ai.theo(G)[-1] == ("gemini", "flash-test")  # khác mức → giải lại
    monkeypatch.setattr(settings, "cache_giai_ngay", 0)
    job5, _ = _giai()
    assert job5.ket_qua["tu_cache"] is False


# ================================================================ phương trình chi tiết: chỉ khi bấm
def test_xem_phuong_trinh_chi_tiet_chi_goi_ai_khi_bam(ai, client, monkeypatch):
    hs = _dang_nhap_email(monkeypatch, client, "hs@example.com")
    r = hs.post("/api/giai", data={"chuoi": CHUOI}, headers=hs.h)
    job = _cho_xong(r.json()["job_id"])
    assert all(b["phuong_trinh"] == "" for b in job.ket_qua["loi_giai"]["cac_buoc"]) and "phuong_trinh" not in job.ket_qua
    assert not ai.theo(giai_nhanh.SYSTEM_PT)
    d = hs.post(f"/api/phuong-trinh/{job.id}", headers=hs.h).json()
    assert len(d["phuong_trinh"]) == 5 and "CH4" in d["phuong_trinh"][0]["phuong_trinh"] and d["da_luu"] is False
    assert ai.theo(giai_nhanh.SYSTEM_PT) == [("gemini", "flash-test")]
    d = hs.post(f"/api/phuong-trinh/{job.id}", headers=hs.h).json()
    assert d["da_luu"] is True and len(ai.theo(giai_nhanh.SYSTEM_PT)) == 1  # lần sau trả bản đã lưu
    assert hs.get(f"/api/ket-qua/{job.id}").json()["phuong_trinh"][0]["buoc"] == 1
    khac = _dang_nhap_email(monkeypatch, client, "khac@example.com")
    assert khac.post(f"/api/phuong-trinh/{job.id}", headers=khac.h).status_code == 404


# ================================================================ llm: suy nghĩ theo mức, streaming Gemini, prompt caching Claude
class _ClientGia:
    def __init__(self, loi_dau=None):
        self.cfg = []
        self.models = self
        self._loi = loi_dau

    def generate_content_stream(self, model, contents, config):
        self.cfg.append((model, config))
        if self._loi is not None:
            loi, self._loi = self._loi, None
            raise loi
        from google.genai import types
        for i, (chu, nghi) in enumerate((("suy nghĩ…", True), ('{"chuoi": [', False), ('{"ki_hieu": "A"}], "buoc": []}', False))):
            yield SimpleNamespace(candidates=[SimpleNamespace(content=types.Content(role="model", parts=[types.Part(
                text=chu, thought=nghi, thought_signature=b"chu-ky" if i == 2 else None)]), finish_reason="STOP" if i == 2 else None)],
                usage_metadata=SimpleNamespace(prompt_token_count=10, candidates_token_count=5, thoughts_token_count=3) if i == 2 else None)


def test_gemini_streaming_va_muc_suy_nghi(cau_hinh, monkeypatch):
    from google.genai import errors

    nhan: list = []
    c = _ClientGia()
    monkeypatch.setattr(gemini_client, "_gemini", lambda: c)
    monkeypatch.setattr(gemini_chat, "_gemini_het_han_muc", {})
    resp, model, _ = gemini_chat._goi_gemini(system="s", contents=[], max_tokens=100, tools=None, output_schema=None, uu_tien="gemini-3-flash",
                                     suy_nghi=1024, khi_co_chu=nhan.append)
    assert nhan == [None, '{"chuoi": [', '{"ki_hieu": "A"}], "buoc": []}']  # bỏ phần suy nghĩ, None = bắt đầu
    phan = resp.candidates[0].content.parts
    assert phan[0].text == '{"chuoi": [{"ki_hieu": "A"}], "buoc": []}' and phan[0].thought_signature == b"chu-ky"
    assert c.cfg[0][1].thinking_config.thinking_level.value == "LOW" and resp.usage_metadata.thoughts_token_count == 3
    gemini_chat._goi_gemini(system="s", contents=[], max_tokens=100, tools=None, output_schema=None, uu_tien="gemini-2.5-flash", suy_nghi=0,
                    khi_co_chu=lambda _x: None)
    assert c.cfg[-1][1].thinking_config.thinking_budget == 0
    # mô hình không nhận cấu hình suy nghĩ → gọi lại không có thinking_config
    c2 = _ClientGia(loi_dau=errors.ClientError(400, {"error": {"message": "thinking_level is not supported", "status": "INVALID_ARGUMENT"}}))
    monkeypatch.setattr(gemini_client, "_gemini", lambda: c2)
    gemini_chat._goi_gemini(system="s", contents=[], max_tokens=100, tools=None, output_schema=None, uu_tien="gemini-x", suy_nghi=20000,
                    khi_co_chu=lambda _x: None)
    assert c2.cfg[0][1].thinking_config.thinking_level.value == "HIGH" and c2.cfg[1][1].thinking_config is None


def test_doc_dan_json_dang_stream():
    thay = []
    dd = giai_nhanh.DocDan(lambda c, b: thay.append((len(c), len(b))))
    chu = '{"chuoi": [{"ki_hieu": "A", "ctct": "", "ten": "", "smiles": "C"}, {"ki_hieu": "B", "ctct": "", "ten": "", "smiles": "CC"}],' \
          ' "buoc": [{"tu": "A", "den": "B", "phan_ung": "x", "dieu_kien": ""}], "luu_y": [], "dap_an": ""}'
    for i in range(0, len(chu), 9):
        dd.nhan(chu[i:i + 9])
    assert thay == [(1, 0), (2, 0), (2, 1)]
    dd.nhan(None)  # thử lại → đọc lại từ đầu
    dd.nhan(chu)
    assert thay[-1] == (2, 1) and len(thay) == 4


def test_claude_prompt_caching_system_dai(cau_hinh, monkeypatch):
    goi = {}

    def stream(use_fallback, kwargs, api_key=None, khi_co_chu=None):
        goi.update(kwargs)
        return SimpleNamespace(content=[], stop_reason="end_turn", model="m", usage=None)

    monkeypatch.setattr(claude_chat, "_claude_stream", stream)
    claude_chat.goi_claude(system=G, messages=[{"role": "user", "content": "x"}])
    assert goi["system"] == [{"type": "text", "text": G, "cache_control": {"type": "ephemeral"}}]  # phần cố định dài → cache
    claude_chat.goi_claude(system="ngắn", messages=[{"role": "user", "content": "x"}])
    assert goi["system"] == "ngắn"


def test_kiem_tra_rdkit_khong_goi_ai():
    gon = giai_nhanh.LoiGiaiGon.model_validate({
        "chuoi": [{"ki_hieu": "C2H4", "ctct": "CH2=CH2", "ten": "ethylene", "smiles": "C=C"},
                  {"ki_hieu": "X (C2H5Br)", "ctct": "", "ten": "", "smiles": "CCBr"},
                  {"ki_hieu": "C2H5OH", "ctct": "", "ten": "", "smiles": "CCC"}],
        "buoc": [{"tu": "C2H4", "den": "X (C2H5Br)", "phan_ung": "cộng", "dieu_kien": "HBr"},
                 {"tu": "X (C2H5Br)", "den": "C2H5OH", "phan_ung": "thế", "dieu_kien": "NaOH, t°"},
                 {"tu": "C2H5OH", "den": "Q", "phan_ung": "", "dieu_kien": ""}], "luu_y": [], "dap_an": ""})
    loi, canh_bao, theo_buoc = giai_nhanh.kiem_tra(gon, "C2H4 → X → C2H5OH")
    assert any("C2H5OH" in x and "C3H8" in x for x in loi)       # công thức đề viết khác SMILES
    assert 2 in theo_buoc and "Q" in theo_buoc[2][0]               # bước dùng kí hiệu lạ
    gon.buoc[0].dieu_kien = "t°"
    _, canh_bao, _ = giai_nhanh.kiem_tra(gon, "")
    assert any("nguyên tố Br" in x for x in canh_bao)              # Br không đến từ tác nhân → cảnh báo nhẹ (không gọi AI)


def test_kiem_tra_tung_buoc_khop_chuoi_cua_de():
    gon = giai_nhanh.LoiGiaiGon.model_validate({
        "chuoi": [{"ki_hieu": k, "ctct": "", "ten": "", "smiles": sm} for k, sm in
                  (("C2H5OH", "CCO"), ("CH3COOH", "CC(=O)O"), ("CH3COOC2H5", "CCOC(C)=O"))],
        "buoc": [{"tu": "C2H5OH", "den": "CH3COOH", "phan_ung": "oxi hoá", "dieu_kien": "O2, men giấm"},
                 {"tu": "CH3COOH", "den": "C2H5OH", "phan_ung": "ester hoá", "dieu_kien": "C2H5OH, H2SO4 đặc"}],
        "luu_y": [], "dap_an": ""})
    loi, _, theo_buoc = giai_nhanh.kiem_tra(gon, "C2H5OH → CH3COOH → CH3COOC2H5")
    assert theo_buoc == {1: ["Bước 2: đề là CH3COOH → CH3COOC2H5 nhưng lời giải ghi CH3COOH → C2H5OH"]} and loi  # lỗi thật → gọi AI sửa
    gon.buoc[1].den = "CH3COOC2H5"
    assert giai_nhanh.kiem_tra(gon, "C2H5OH → CH3COOH → CH3COOC2H5")[0] == []
    assert giai_nhanh.kiem_tra(gon, "Cho X (C2H6O) → Y → Z, tìm X, Y, Z")[2] == {}  # đề không phải chuỗi chữ thẳng → bỏ qua
