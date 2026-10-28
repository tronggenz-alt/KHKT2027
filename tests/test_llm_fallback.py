"""Kiểm tra cơ chế tự chuyển Claude → Gemini Pro (không gọi mạng: thay hàm gọi API bằng hàm giả).
Claude chỉ được gọi khi kế hoạch của yêu cầu có quyền Claude (admin / mã Claude) và bậc là "claude"."""
from types import SimpleNamespace

import pytest

from backend import llm
from backend.ai.providers.claude import chat as claude_chat
from backend.ai.providers.gemini import chat as gemini_chat, client as gemini_client
from backend.config import settings


def _gemini_tra_loi(*parts, finish="STOP"):
    content = SimpleNamespace(parts=list(parts))
    return SimpleNamespace(candidates=[SimpleNamespace(content=content, finish_reason=finish)], usage_metadata=None)


def _text(t):
    return SimpleNamespace(text=t, thought=False, function_call=None)


def _call(name, args):
    return SimpleNamespace(text=None, thought=False, function_call=SimpleNamespace(id=None, name=name, args=args))


class _QuyenGia:
    ma_id = None

    def giu(self):
        return True


@pytest.fixture
def co_claude():
    """Kế hoạch: bậc claude + có quyền Claude (như admin)."""
    with llm.dung_ke_hoach(llm.KeHoachAI(bac="claude", claude=_QuyenGia())) as kh:
        yield kh


@pytest.fixture(autouse=True)
def _cau_hinh(monkeypatch):
    monkeypatch.setattr(settings, "api_key", "sk-ant-test")
    monkeypatch.setattr(settings, "gemini_api_key", "gm-test")
    monkeypatch.setattr(settings, "gemini_fallback", True)
    monkeypatch.setattr(llm, "_claude_tam_dung_den", 0.0)
    monkeypatch.setattr(llm, "_claude_ly_do_tam_dung", "")


def test_claude_het_tien_chuyen_sang_gemini_pro(monkeypatch, co_claude):
    def claude_loi(**kw):
        raise llm.LoiAI("Tài khoản Anthropic đã hết tín dụng", "het_tien")

    goi_gemini = []

    def gemini_ok(**kw):
        goi_gemini.append(kw)
        return _gemini_tra_loi(_text('{"ok": true}')), "gemini-test", kw["contents"]

    monkeypatch.setattr(claude_chat, "goi_claude", claude_loi)
    monkeypatch.setattr(gemini_chat, "_goi_gemini", gemini_ok)
    bao = []
    phien = llm.PhienHoiThoai("sys", output_schema={"type": "object"}, bao=bao.append)
    ph = phien.gui(text="xin chào")

    assert ph.nha_cung_cap == "gemini" and ph.text == '{"ok": true}'
    assert phien.nha_cung_cap == "gemini" and phien.bac == "pro"
    assert any("Gemini Pro" in m and "không trừ lượt" in m for m in bao)
    assert goi_gemini[0]["output_schema"] == {"type": "object"} and goi_gemini[0]["uu_tien"] == settings.gemini_model_hsg
    assert co_claude.claude_thanh_cong is False  # Claude không trả lời được → không trừ lượt mã
    # Lỗi tài khoản → các phiên mới bỏ qua Claude (tạm dừng), dùng Gemini Pro
    p2 = llm.PhienHoiThoai("sys")
    assert p2.nha_cung_cap == "gemini" and p2.bac == "pro" and "tạm dừng" in p2.loi_claude


def test_loi_tam_thoi_khong_tam_dung_claude(monkeypatch, co_claude):
    monkeypatch.setattr(claude_chat, "goi_claude", lambda **kw: (_ for _ in ()).throw(llm.LoiAI("quá tải", "qua_tai")))
    monkeypatch.setattr(gemini_chat, "_goi_gemini", lambda **kw: (_gemini_tra_loi(_text("ok")), "gemini-test", kw["contents"]))
    assert llm.PhienHoiThoai("sys").gui(text="a").nha_cung_cap == "gemini"
    assert llm.PhienHoiThoai("sys").nha_cung_cap == "claude"


def test_khong_co_gemini_thi_bao_loi_claude(monkeypatch, co_claude):
    monkeypatch.setattr(settings, "gemini_api_key", "")
    monkeypatch.setattr(claude_chat, "goi_claude", lambda **kw: (_ for _ in ()).throw(llm.LoiAI("hết tín dụng", "het_tien")))
    with pytest.raises(llm.LoiAI) as e:
        llm.PhienHoiThoai("sys").gui(text="a")
    assert "GEMINI_API_KEY" in str(e.value)


def test_vong_cong_cu_tren_gemini(monkeypatch, co_claude):
    """Chuyển giữa chừng vòng tool-use: lịch sử Claude được làm phẳng, sau đó Gemini gọi công cụ bản địa."""
    luot = {"n": 0}

    def claude_mot_lan(**kw):
        luot["n"] += 1
        if luot["n"] == 1:
            block = SimpleNamespace(type="tool_use", id="toolu_1", name="pubchem_lookup", input={"ten_hoac_cong_thuc": "methane"})
            return SimpleNamespace(content=[block], stop_reason="tool_use", model="claude-test")
        raise llm.LoiAI("hết tín dụng", "het_tien")

    gemini_luot = []

    def gemini(**kw):
        gemini_luot.append(list(kw["contents"]))
        if len(gemini_luot) == 1:
            return _gemini_tra_loi(_call("web_research", {"cau_hoi": "Methane", "nguon": "vi.wikipedia"})), "gemini-test", kw["contents"]
        return _gemini_tra_loi(_text("GHI CHÚ TRA CỨU: xong")), "gemini-test", kw["contents"]

    monkeypatch.setattr(claude_chat, "goi_claude", claude_mot_lan)
    monkeypatch.setattr(gemini_chat, "_goi_gemini", gemini)
    phien = llm.PhienHoiThoai("sys", tools=[{"name": "pubchem_lookup", "input_schema": {}}])
    ph = phien.gui(text="bắt đầu")
    assert ph.nha_cung_cap == "claude" and ph.cong_cu[0].ten == "pubchem_lookup"

    ph = phien.gui(ket_qua_cong_cu=[(ph.cong_cu[0], {"ok": True, "smiles": "C"})])
    assert ph.nha_cung_cap == "gemini" and ph.cong_cu[0].ten == "web_research"
    # lịch sử dựng lại: user, model (đã làm phẳng), user (kết quả công cụ dạng văn bản)
    roles = [c.role for c in gemini_luot[0]]
    assert roles == ["user", "model", "user"]
    assert "pubchem_lookup" in gemini_luot[0][1].parts[0].text

    ph = phien.gui(ket_qua_cong_cu=[(ph.cong_cu[0], {"ok": True})])
    assert ph.text.startswith("GHI CHÚ") and not ph.cong_cu
    last = gemini_luot[1][-1]
    assert last.parts[0].function_response.name == "web_research"


def test_bo_additional_properties():
    s = {"type": "object", "additionalProperties": False, "properties": {"a": {"type": "array", "items": {"type": "object", "additionalProperties": False}}}}
    out = gemini_chat._bo_additional_properties(s)
    assert "additionalProperties" not in out
    assert "additionalProperties" not in out["properties"]["a"]["items"]
