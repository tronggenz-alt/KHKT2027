"""Router AI + ChatGPT (backend/ai): thứ tự claude → gemini → openai theo từng hệ thống, bỏ qua AI chưa có key / không có quyền,
tự chuyển AI kế tiếp khi lỗi (ghi nhật ký ai_fallbacks), ChatGPT qua Responses API (ảnh, JSON schema, streaming, lỗi). Không gọi mạng."""
from types import SimpleNamespace

import httpx
import openai
import pytest

from backend import db, llm
from backend.ai import NHA_CUNG_CAP, router
from backend.ai.providers.claude import chat as claude_chat
from backend.ai.providers.gemini import chat as gemini_chat
from backend.ai.providers.openai import chat as openai_chat, client as openai_client
from backend.config import settings


class _QuyenGia:
    ma_id = None

    def giu(self):
        return True


def _ev(loai, **kw):
    return SimpleNamespace(type=loai, **kw)


def _xong(text="", model="gpt-test", vao=11, ra=7, nghi=3, trang_thai="completed", ly_do=""):
    u = SimpleNamespace(input_tokens=vao, output_tokens=ra, output_tokens_details=SimpleNamespace(reasoning_tokens=nghi))
    return _ev("response.completed" if trang_thai == "completed" else "response.incomplete",
               response=SimpleNamespace(status=trang_thai, model=model, usage=u, output_text=text,
                                        incomplete_details=SimpleNamespace(reason=ly_do) if ly_do else None))


class _OpenAIGia:
    """Client OpenAI giả: responses.create(**kw) → luồng sự kiện (hoặc ném lỗi). Ghi lại mọi lần gọi."""

    def __init__(self, tra_loi):
        self.goi = []
        self.responses = SimpleNamespace(create=self._create)
        self._tra_loi = tra_loi

    def _create(self, **kw):
        self.goi.append(kw)
        r = self._tra_loi(kw)
        if isinstance(r, Exception):
            raise r
        return iter(r)


def _gemini_ok(text="gemini-ok"):
    content = SimpleNamespace(parts=[SimpleNamespace(text=text, thought=False, function_call=None)])
    return lambda **kw: (SimpleNamespace(candidates=[SimpleNamespace(content=content, finish_reason="STOP")], usage_metadata=None),
                         "gemini-test", kw["contents"])


def _loi_http(lop, ma, thong_diep="x"):
    req = httpx.Request("POST", "https://api.openai.com/v1/responses")
    return lop(thong_diep, response=httpx.Response(ma, request=req), body=None)


@pytest.fixture(autouse=True)
def _cau_hinh(monkeypatch):
    monkeypatch.setattr(settings, "api_key", "sk-ant-test")
    monkeypatch.setattr(settings, "gemini_api_key", "gm-test")
    monkeypatch.setattr(settings, "gemini_fallback", True)
    monkeypatch.setattr(settings, "openai_api_key", "sk-proj-test")
    monkeypatch.setattr(settings, "openai_model", "gpt-test")
    monkeypatch.setattr(settings, "openai_temperature", None)
    monkeypatch.setattr(llm, "_claude_tam_dung_den", 0.0)
    monkeypatch.setattr(llm, "_claude_ly_do_tam_dung", "")


def _du_phong():
    with db.ket_noi() as con:
        return [tuple(r) for r in con.execute("SELECT he_thong, tu, sang FROM ai_fallbacks ORDER BY id")]


# ================================================================ thứ tự (routing.json + đè ở CSDL)
def test_thu_tu_mac_dinh_va_doi_theo_he_thong():
    assert router.mac_dinh() == {k: ["claude", "gemini", "openai"] for k in router.HE_THONG}
    assert router.dat_thu_tu("mo_hinh_3d", ["claude", "openai", "gemini"]) == ["claude", "openai", "gemini"]
    assert router.thu_tu("mo_hinh_3d") == ["claude", "openai", "gemini"] and router.thu_tu("giai_chuoi") == ["claude", "gemini", "openai"]
    assert router.tat_ca()["mo_hinh_3d"]["da_doi"] is True
    for sai in (["claude", "gemini"], ["claude", "claude", "openai"], ["claude", "gemini", "grok"]):
        with pytest.raises(ValueError):
            router.dat_thu_tu("co_che", sai)
    assert router.ve_mac_dinh("mo_hinh_3d") == ["claude", "gemini", "openai"]
    assert [router.he_thong_cua(x) for x in ("giai", "phuong_trinh", "3d", "co_che", "co_che_anh", "khac")] == \
        ["giai_chuoi", "giai_chuoi", "mo_hinh_3d", "co_che", "co_che", "giai_chuoi"]


def test_ca_ba_provider_cung_giao_dien():
    from backend.ai import AIProvider
    assert all(isinstance(p, AIProvider) for p in NHA_CUNG_CAP.values())
    assert [p.ten_hien_thi for p in NHA_CUNG_CAP.values()] == ["Claude", "Gemini", "ChatGPT"]


# ================================================================ chuỗi dự phòng (đặc tả: tắt Claude → Gemini; tắt cả Gemini → ChatGPT)
def test_tat_key_claude_thi_dung_gemini(monkeypatch):
    monkeypatch.setattr(settings, "api_key", "")
    monkeypatch.setattr(gemini_chat, "_goi_gemini", _gemini_ok())
    monkeypatch.setattr(claude_chat, "goi_claude", lambda **kw: pytest.fail("không được gọi Claude khi chưa có key"))
    with llm.dung_ke_hoach(llm.KeHoachAI(loai="giai", bac="claude", claude=_QuyenGia())):
        p = llm.PhienHoiThoai("sys")
        assert p.chuoi == ["gemini", "openai"] and "ANTHROPIC_API_KEY" in p.loi_claude
        assert p.gui(text="a").nha_cung_cap == "gemini"


def test_tat_ca_claude_va_gemini_thi_dung_chatgpt(monkeypatch):
    monkeypatch.setattr(settings, "api_key", "")
    monkeypatch.setattr(settings, "gemini_api_key", "")
    gia = _OpenAIGia(lambda kw: [_ev("response.output_text.delta", delta='{"ok"'), _ev("response.output_text.delta", delta=": 1}"), _xong()])
    monkeypatch.setattr(openai_client, "lay_client", lambda: gia)
    with llm.dung_ke_hoach(llm.KeHoachAI(loai="co_che", user_id=None)):
        p = llm.PhienHoiThoai("hệ thống", output_schema={"type": "object"})
        assert p.chuoi == ["openai"]
        ph = p.gui(text="vẽ cơ chế")
    assert (ph.nha_cung_cap, ph.text, ph.model, ph.tokens_in, ph.tokens_out, ph.tokens_think) == ("openai", '{"ok": 1}', "gpt-test", 11, 7, 3)
    kw = gia.goi[0]
    assert kw["model"] == "gpt-test" and kw["instructions"] == "hệ thống" and kw["stream"] is True
    assert kw["text"]["format"]["type"] == "json_schema" and kw["text"]["format"]["schema"] == {"type": "object"}
    assert llm.mo_ta_mo_hinh(ph) == "ChatGPT (gpt-test)"


def test_claude_loi_gemini_loi_chatgpt_tra_loi_va_ghi_nhat_ky(monkeypatch):
    monkeypatch.setattr(claude_chat, "goi_claude", lambda **kw: (_ for _ in ()).throw(llm.LoiAI("Tài khoản Anthropic đã hết tín dụng", "het_tien")))
    monkeypatch.setattr(gemini_chat, "_goi_gemini", lambda **kw: (_ for _ in ()).throw(llm.LoiAI("Gemini: hết hạn mức", "gioi_han")))
    gia = _OpenAIGia(lambda kw: [_ev("response.output_text.delta", delta="ok"), _xong()])
    monkeypatch.setattr(openai_client, "lay_client", lambda: gia)
    bao = []
    with llm.dung_ke_hoach(llm.KeHoachAI(loai="giai", user_id=7, job_id="j1", bac="claude", claude=_QuyenGia())) as kh:
        p = llm.PhienHoiThoai("sys", bao=bao.append)
        assert p.chuoi == ["claude", "gemini", "openai"]
        ph = p.gui(text="giải chuỗi")
    assert ph.nha_cung_cap == "openai" and kh.claude_thanh_cong is False
    assert any("Gemini Pro" in m and "không trừ lượt" in m for m in bao) and any("Gemini lỗi" in m and "ChatGPT" in m for m in bao)
    assert _du_phong() == [("giai_chuoi", "claude", "gemini"), ("giai_chuoi", "gemini", "openai")]
    assert router.ds_du_phong()[0]["sang"] == "ChatGPT" and "hết hạn mức" in router.ds_du_phong()[0]["ly_do"]
    # ChatGPT nhận lại toàn bộ lịch sử (không giữ trạng thái)
    assert gia.goi[0]["input"][0]["role"] == "user" and gia.goi[0]["input"][0]["content"][0]["text"] == "giải chuỗi"


def test_tat_ca_loi_thi_bao_du_ba_loi(monkeypatch):
    monkeypatch.setattr(claude_chat, "goi_claude", lambda **kw: (_ for _ in ()).throw(llm.LoiAI("Claude quá tải", "qua_tai")))
    monkeypatch.setattr(gemini_chat, "_goi_gemini", lambda **kw: (_ for _ in ()).throw(llm.LoiAI("Gemini lỗi mạng", "mang")))
    monkeypatch.setattr(openai_client, "lay_client", lambda: _OpenAIGia(lambda kw: _loi_http(openai.RateLimitError, 429, "insufficient_quota")))
    with llm.dung_ke_hoach(llm.KeHoachAI(loai="giai", bac="claude", claude=_QuyenGia())):
        with pytest.raises(llm.LoiAI) as e:
            llm.PhienHoiThoai("sys").gui(text="a")
    assert "Claude quá tải" in str(e.value) and "Gemini dự phòng cũng lỗi" in str(e.value) and "ChatGPT dự phòng cũng lỗi" in str(e.value)
    assert e.value.loai == "het_tien"


def test_chua_co_key_chatgpt_thi_bo_qua(monkeypatch):
    monkeypatch.setattr(settings, "openai_api_key", "")
    monkeypatch.setattr(gemini_chat, "_goi_gemini", lambda **kw: (_ for _ in ()).throw(llm.LoiAI("Gemini: hết hạn mức", "gioi_han")))
    p = llm.PhienHoiThoai("sys")
    assert p.chuoi == ["gemini"] and NHA_CUNG_CAP["openai"].ly_do_bo_qua(p) == "chưa có OPENAI_API_KEY"
    with pytest.raises(llm.LoiAI, match="hết hạn mức"):
        p.gui(text="a")  # lỗi như trước khi có ChatGPT, không lỗi app
    monkeypatch.setattr(settings, "openai_api_key", "sk-proj-test")
    monkeypatch.setattr(settings, "openai_model", "")
    assert llm.PhienHoiThoai("sys").chuoi == ["gemini"]


def test_key_gemini_rieng_khong_dung_chatgpt_may_chu(monkeypatch):
    monkeypatch.setattr(gemini_chat, "_goi_gemini", lambda **kw: (_ for _ in ()).throw(llm.LoiAI("key của bạn hết hạn mức", "gioi_han")))
    monkeypatch.setattr(openai_client, "lay_client", lambda: pytest.fail("không được dùng ChatGPT của máy chủ cho người dùng key riêng"))
    with llm.dung_khoa(llm.KhoaAI("ca_nhan", "AIza-key-rieng", "abcd")):
        p = llm.PhienHoiThoai("sys")
        assert p.chuoi == ["gemini"]
        with pytest.raises(llm.LoiAI):
            p.gui(text="a")


def test_thu_tu_rieng_cho_mo_hinh_3d(monkeypatch):
    router.dat_thu_tu("mo_hinh_3d", ["openai", "gemini", "claude"])
    gia = _OpenAIGia(lambda kw: [_xong(text="ethanol")])
    monkeypatch.setattr(openai_client, "lay_client", lambda: gia)
    monkeypatch.setattr(gemini_chat, "_goi_gemini", lambda **kw: pytest.fail("3D: ChatGPT đứng đầu"))
    with llm.dung_ke_hoach(llm.KeHoachAI(loai="3d")):
        p = llm.PhienHoiThoai("sys")
        ph = p.gui(text="nhận diện", anh=("image/jpeg", "QUJD"))
    assert p.he_thong == "mo_hinh_3d" and ph.text == "ethanol"
    anh = gia.goi[0]["input"][0]["content"][1]
    assert anh == {"type": "input_image", "image_url": "data:image/jpeg;base64,QUJD"}
    with llm.dung_ke_hoach(llm.KeHoachAI(loai="giai")):
        assert llm.PhienHoiThoai("sys").chuoi == ["gemini", "openai"]  # hệ thống khác vẫn theo thứ tự mặc định


def test_khong_dung_chatgpt_cho_phien_co_cong_cu():
    p = llm.PhienHoiThoai("sys", tools=[{"name": "pubchem_lookup", "input_schema": {"type": "object"}}])
    assert "openai" not in p.chuoi


# ================================================================ ChatGPT: tham số, streaming, lỗi
def test_chatgpt_streaming_suy_nghi_va_bo_tham_so_khong_nhan(monkeypatch):
    monkeypatch.setattr(settings, "openai_temperature", 0.2)
    lan = []

    def tra_loi(kw):
        lan.append(dict(kw))
        if "temperature" in kw:
            return _loi_http(openai.BadRequestError, 400, "Unsupported parameter: 'temperature' is not supported with this model.")
        return [_ev("response.output_text.delta", delta="Xin "), _ev("response.output_text.delta", delta="chào"), _xong(ra=2048, trang_thai="incomplete", ly_do="max_output_tokens")]

    monkeypatch.setattr(openai_client, "lay_client", lambda: _OpenAIGia(tra_loi))
    chu = []
    ph = openai_chat.goi_openai(system="s", input=[], max_tokens=2048, effort="high", khi_co_chu=chu.append)
    assert ph.text == "Xin chào" and ph.bi_cat is True and chu == [None, "Xin ", "chào"]
    assert lan[0]["reasoning"] == {"effort": "high"} and lan[0]["temperature"] == 0.2 and "temperature" not in lan[1]


@pytest.mark.parametrize("loi, loai", [
    (_loi_http(openai.AuthenticationError, 401), "key"),
    (_loi_http(openai.NotFoundError, 404), "model"),
    (_loi_http(openai.RateLimitError, 429, "Rate limit reached"), "gioi_han"),
    (_loi_http(openai.RateLimitError, 429, "You exceeded your current quota (insufficient_quota)"), "het_tien"),
    (_loi_http(openai.InternalServerError, 500), "qua_tai"),
    (openai.APITimeoutError(request=httpx.Request("POST", "https://api.openai.com")), "timeout"),
    (httpx.ReadTimeout("im lặng"), "timeout"),
])
def test_chatgpt_loi_sang_tieng_viet(monkeypatch, loi, loai):
    monkeypatch.setattr(openai_client, "lay_client", lambda: _OpenAIGia(lambda kw: loi))
    with pytest.raises(llm.LoiAI) as e:
        openai_chat.goi_openai(system="s", input=[], max_tokens=100)
    assert e.value.loai == loai and "sk-" not in str(e.value)


def test_chatgpt_tu_choi_va_chua_dat_model(monkeypatch):
    monkeypatch.setattr(openai_client, "lay_client", lambda: _OpenAIGia(lambda kw: [_xong(trang_thai="incomplete", ly_do="content_filter")]))
    with pytest.raises(llm.LoiAI) as e:
        openai_chat.goi_openai(system="s", input=[], max_tokens=100)
    assert e.value.loai == "tu_choi"
    monkeypatch.setattr(settings, "openai_model", "")
    with pytest.raises(llm.LoiAI, match="OPENAI_MODEL"):
        openai_chat.goi_openai(system="s", input=[], max_tokens=100)


def test_chi_phi_ghi_nha_cung_cap_openai(monkeypatch):
    monkeypatch.setattr(settings, "gemini_api_key", "")
    monkeypatch.setattr(openai_client, "lay_client", lambda: _OpenAIGia(lambda kw: [_xong(text="ok")]))
    with llm.dung_ke_hoach(llm.KeHoachAI(loai="giai", user_id=3)):
        llm.PhienHoiThoai("sys").gui(text="a")
    with db.ket_noi() as con:
        r = con.execute("SELECT provider, model, tokens_in, tokens_out, ok FROM ai_usage").fetchone()
    assert tuple(r) == ("openai", "gpt-test", 11, 7, 1)


def test_tao_client_sdk_that_khong_loi(monkeypatch):
    """Dựng client SDK THẬT (không gọi mạng) với timeout của app: SDK anthropic mới chạy trên httpx2 và từ chối httpx.Timeout –
    test giả ở trên không bắt được lỗi này (lần chạy thật đầu tiên: TypeError khi tạo client Claude)."""
    from backend.ai.providers.claude import client as claude_client

    monkeypatch.setattr(settings, "api_key", "sk-ant-api03-test")
    monkeypatch.setattr(settings, "openai_api_key", "sk-proj-test")
    monkeypatch.setattr(claude_client, "_claude_client", None)
    monkeypatch.setattr(openai_client, "_client", None)
    c = claude_client.claude_cua(None)
    assert c.timeout.read == settings.ai_timeout_giay and c.timeout.connect == 10.0
    assert claude_client.claude_cua("sk-ant-api03-rieng").timeout.read == settings.ai_timeout_giay
    assert openai_client.lay_client().timeout.read == settings.ai_timeout_giay


def test_ten_ai_theo_thu_tu(monkeypatch):
    """Tiến trình hiện đúng AI sẽ giải trước: mặc định như cũ (TEN_BAC); không có Gemini → ChatGPT; admin đặt ChatGPT trước → ChatGPT."""
    with llm.dung_khoa(llm.KhoaAI("chu_app")):
        with llm.dung_ke_hoach(llm.KeHoachAI(loai="giai", claude=_QuyenGia())):
            assert (llm.ten_ai("flash"), llm.ten_ai("claude")) == ("Gemini Flash", "Claude")
            router.dat_thu_tu("giai_chuoi", ["openai", "gemini", "claude"])
            assert (llm.ten_ai("flash"), llm.ten_ai("claude")) == ("ChatGPT", "ChatGPT")
            router.dat_thu_tu("giai_chuoi", ["gemini", "claude", "openai"])
            assert llm.ten_ai("claude") == "Gemini Pro"
        router.ve_mac_dinh("giai_chuoi")
        with llm.dung_ke_hoach(llm.KeHoachAI(loai="giai")):
            monkeypatch.setattr(settings, "gemini_api_key", "")
            assert llm.ten_ai("flash") == "ChatGPT"
            monkeypatch.setattr(settings, "openai_api_key", "")
            assert llm.ten_ai("flash") == "Gemini Flash"  # không AI nào → tên cũ (lỗi sẽ báo khi gọi)
