"""Mục 05 Cơ chế – nhập đề bằng hình ảnh: kiểm tra ảnh, đọc ảnh bằng AI (giả ở tầng gọi API), RDKit kiểm SMILES + đọc lại,
chọn mô hình theo quy tắc chung (Flash; HSG → Pro; Key Claude → Claude), lỗi ảnh, nút Sửa chất, ghi chi phí."""
import io
import json
import shutil
import subprocess
import sys
from types import SimpleNamespace

import pytest
from PIL import Image, ImageDraw

from backend import co_che_anh, db, llm, mo_hinh_3d
from backend.ai.providers.claude import chat as claude_chat
from backend.ai.providers.gemini import chat as gemini_chat, client as gemini_client
from backend.bao_mat import gioi_han_tan_suat
from backend.config import settings
from test_chon_mo_hinh import _dang_nhap_email

KEY = "khoa-thu-Kc9"


def _anh(fmt="PNG", size=(900, 500), mau=(255, 255, 255), chu="CH3CH2Br + NaOH -> ?") -> bytes:
    img = Image.new("RGB", size, mau)
    ImageDraw.Draw(img).text((40, 40), chu, fill=(0, 0, 0))
    buf = io.BytesIO()
    img.save(buf, fmt)
    return buf.getvalue()


def _kq(**them) -> dict:
    d = {"de_bai": "Câu 1. Viết cơ chế phản ứng CH3CH2Br + NaOH.",
         "phan_ung": [{"so_cau": "Câu 1", "chat_dau": ["CCBr", "[OH-]"], "tac_nhan_dieu_kien": "H2O, t°", "san_pham": ["CCO", ""],
                       "yeu_cau": "vẽ cơ chế", "goi_y_loai": "SN2"}],
         "cau_truc_trong_anh": [], "muc_do": "trung_binh", "khong_chac_chan": [],
         "chat_luong_anh": {"doc_duoc": True, "van_de": "", "mo_ta": ""}}
    d.update(them)
    return d


class AIDocAnh:
    """AI giả: trả lần lượt các JSON trong `tra_loi` (phần tử cuối dùng lại); ghi (nhà cung cấp, mô hình, số ảnh)."""

    def __init__(self, monkeypatch):
        self.goi: list[tuple[str, str, int]] = []
        self.tra_loi: list[dict | str] = [_kq()]
        self.claude_loi: llm.LoiAI | None = None
        monkeypatch.setattr(claude_chat, "goi_claude", self._claude)
        monkeypatch.setattr(gemini_chat, "_goi_gemini", self._gemini)

    def _tiep(self) -> str:
        x = self.tra_loi.pop(0) if len(self.tra_loi) > 1 else self.tra_loi[0]
        return x if isinstance(x, str) else json.dumps(x, ensure_ascii=False)

    def _claude(self, **kw):
        anh = sum(1 for m in kw["messages"] if m["role"] == "user" for c in (m["content"] if isinstance(m["content"], list) else [])
                  if isinstance(c, dict) and c.get("type") == "image")
        self.goi.append(("claude", kw.get("model") or settings.model, anh))
        if self.claude_loi is not None:
            raise self.claude_loi
        return SimpleNamespace(content=[SimpleNamespace(type="text", text=self._tiep())], stop_reason="end_turn",
                               model=kw.get("model") or settings.model, usage=SimpleNamespace(input_tokens=2000, output_tokens=400))

    def _gemini(self, **kw):
        anh = sum(1 for c in kw["contents"] for p in (getattr(c, "parts", None) or []) if getattr(p, "inline_data", None) is not None)
        self.goi.append(("gemini", kw["uu_tien"], anh))
        part = SimpleNamespace(text=self._tiep(), thought=False, function_call=None)
        resp = SimpleNamespace(candidates=[SimpleNamespace(content=SimpleNamespace(parts=[part]), finish_reason="STOP")],
                               usage_metadata=SimpleNamespace(prompt_token_count=1500, candidates_token_count=300, thoughts_token_count=50))
        return resp, kw["uu_tien"], kw["contents"]


@pytest.fixture
def ai(monkeypatch, cau_hinh):
    monkeypatch.setattr(settings, "gemini_model_lite", "lite-test")
    monkeypatch.setattr(settings, "gemini_model", "flash-test")
    monkeypatch.setattr(settings, "gemini_model_hsg", "pro-test")
    monkeypatch.setattr(settings, "model", "claude-test")
    monkeypatch.setattr(settings, "claude_access_key", KEY)
    monkeypatch.setattr(llm, "_claude_tam_dung_den", 0.0)
    monkeypatch.setattr(llm, "_claude_ly_do_tam_dung", "")
    return AIDocAnh(monkeypatch)


def _doc(c, ds=None, **form):
    gioi_han_tan_suat.xoa()
    ds = ds if ds is not None else [("de.png", _anh(), "image/png")]
    return c.post("/api/co-che/doc-anh", files=[("anh", x) for x in ds], data=form, headers=c.h)


# ================================================================ kiểm tra ảnh
def test_kiem_tra_anh_dinh_dang_kich_thuoc_do_sang(cau_hinh):
    for fmt in ("PNG", "JPEG", "WEBP"):
        assert co_che_anh.kiem_tra_anh(_anh(fmt))
    for data, ma in ((_anh("GIF"), "sai_dinh_dang"), (b"khong phai anh", "sai_dinh_dang"), (b"", "anh_rong"),
                     (_anh(size=(150, 120)), "anh_qua_nho"), (_anh(mau=(5, 5, 5), chu=""), "anh_qua_toi"),
                     (b"\0" * (co_che_anh.ANH_TOI_DA + 1), "anh_qua_lon")):
        with pytest.raises(co_che_anh.LoiAnh) as e:
            co_che_anh.kiem_tra_anh(data, "x.bin", 2)
        assert e.value.ma == ma and str(e.value).startswith("Ảnh 2 (x.bin)")
    assert "chỉ nhận JPG, PNG, WEBP, HEIC" in str(pytest.raises(co_che_anh.LoiAnh, co_che_anh.kiem_tra_anh, _anh("GIF")).value)


@pytest.mark.skipif(sys.platform != "darwin" or not shutil.which("sips"), reason="cần sips (macOS) để tạo ảnh HEIC thử")
def test_heic_doi_sang_jpg(cau_hinh, tmp_path):
    (tmp_path / "a.png").write_bytes(_anh())
    subprocess.run(["sips", "-s", "format", "heic", str(tmp_path / "a.png"), "--out", str(tmp_path / "a.heic")], capture_output=True, check=True)
    heic = (tmp_path / "a.heic").read_bytes()
    assert co_che_anh._la_heic(heic) and not co_che_anh._la_heic(_anh())
    jpg = co_che_anh.kiem_tra_anh(heic, "IMG_0001.HEIC")
    assert Image.open(io.BytesIO(jpg)).format == "JPEG"


# ================================================================ đọc ảnh qua API
def test_doc_anh_1_phan_ung_flash_kiem_smiles_ghi_chi_phi(ai, client, monkeypatch):
    hs = _dang_nhap_email(monkeypatch, client, "hs@example.com")
    ds = [("trang1.jpg", _anh("JPEG"), "image/jpeg"), ("trang2.webp", _anh("WEBP"), "image/webp")]
    r = _doc(hs, ds, van_ban="vẽ cơ chế câu 1")
    assert r.status_code == 200, r.text
    d = r.json()
    assert ai.goi == [("gemini", "flash-test", 2)]  # một lần đọc, đủ 2 ảnh, bậc Flash
    assert d["muc_do"] == "trung_binh" and d["ten_muc_do"] == "Trung bình" and d["mo_hinh"] == "Gemini (flash-test)"
    p = d["phan_ung"][0]
    assert [c["smiles"] for c in p["chat_dau"]] == ["CCBr", "[OH-]"] and all(c["hop_le"] and c["anh"].startswith("data:image/png")
                                                                             for c in p["chat_dau"])
    assert p["san_pham"][0]["smiles"] == "CCO" and p["san_pham"][1] == {"smiles": None, "hop_le": True, "khong_chac": False, "anh": ""}
    assert p["tac_nhan_dieu_kien"] == "H2O, t°" and d["khong_chac_chan"] == [] and d["so_anh"] == 2
    assert p["goi_y_loai"] == "SN2" and "SN2" in co_che_anh.SCHEMA_DOC_ANH["properties"]["phan_ung"]["items"]["properties"]["goi_y_loai"]["enum"]
    assert "{DS_LOAI}" not in co_che_anh.SYSTEM_DOC_ANH and "SN1 = " in co_che_anh.SYSTEM_DOC_ANH
    assert d["json_ai"]["phan_ung"][0]["san_pham"] == ["CCO", None]  # sản phẩm đề bắt tìm = null
    with db.ket_noi() as con:
        r = con.execute("SELECT kind, step, tier, provider, model, tokens_in, tokens_out, ok FROM ai_usage").fetchall()
    assert [tuple(x) for x in r] == [("co_che_anh", "doc_anh", "flash", "gemini", "flash-test", 1500, 350, 1)]


def test_smiles_sai_doc_lai_toi_da_2_lan_roi_to_vang(ai, client, monkeypatch):
    hs = _dang_nhap_email(monkeypatch, client, "hs@example.com")
    sai = _kq(phan_ung=[{"so_cau": "Câu 2", "chat_dau": ["CC(C)(C)(C)(C)Br"], "tac_nhan_dieu_kien": "", "san_pham": [""], "yeu_cau": ""}])
    ai.tra_loi = [sai, _kq()]  # lần 2 đã sửa
    d = _doc(hs).json()
    assert len(ai.goi) == 2 and d["phan_ung"][0]["chat_dau"][0]["smiles"] == "CCBr" and not d["khong_chac_chan"]
    ai.goi.clear()
    ai.tra_loi = [sai]  # sai mãi → 1 lần đọc + 2 lần đọc lại, rồi tô vàng
    d = _doc(hs).json()
    c = d["phan_ung"][0]["chat_dau"][0]
    assert len(ai.goi) == 3 and (c["hop_le"], c["khong_chac"], c["anh"]) == (False, True, "")
    assert any("CC(C)(C)(C)(C)Br" in x and "Sửa" in x for x in d["khong_chac_chan"])
    assert any("RDKit không đọc được 1 SMILES" in x for x in d["tien_trinh"])


def test_de_hsg_doc_lai_bang_pro(ai, client, monkeypatch):
    hs = _dang_nhap_email(monkeypatch, client, "hs@example.com")
    ai.tra_loi = [_kq(muc_do="hsg", khong_chac_chan=["Câu 1 chất đầu 1 (CCBr): nét nêm khó đọc"])]
    d = _doc(hs).json()
    assert [(n, m) for n, m, _ in ai.goi] == [("gemini", "flash-test"), ("gemini", "pro-test")]
    assert d["muc_do"] == "hsg" and d["mo_hinh"] == "Gemini (pro-test)" and any("đọc lại bằng Gemini Pro" in x for x in d["tien_trinh"])
    assert d["phan_ung"][0]["chat_dau"][0]["khong_chac"] is True  # SMILES có trong "khong_chac_chan" → tô vàng
    assert d["phan_ung"][0]["chat_dau"][1]["khong_chac"] is False


def test_key_claude_bat_dung_claude_doc_anh_loi_thi_chuyen_gemini(ai, client, monkeypatch):
    hs = _dang_nhap_email(monkeypatch, client, "hs@example.com")
    d = _doc(hs, dung_claude="true").json()  # chưa mở Claude → từ chối, Gemini đọc
    assert ai.goi[-1][0] == "gemini" and any("từ chối dùng Claude" in t for t in d["thong_bao_claude"])
    assert hs.post("/api/key-claude", json={"key": KEY}, headers=hs.h).json()["da_mo"]
    ai.goi.clear()
    d = _doc(hs, dung_claude="true").json()
    assert ai.goi == [("claude", "claude-test", 1)] and d["mo_hinh"] == "Claude (claude-test)" and d["dung_claude"] is True
    ai.goi.clear()
    ai.claude_loi = llm.LoiAI("Máy chủ Claude đang quá tải hoặc gặp lỗi.", "qua_tai")
    d = _doc(hs, dung_claude="true").json()
    assert [g[0] for g in ai.goi] == ["claude", "gemini"] and d["dung_claude"] is False
    assert any("Claude lỗi" in t for t in d["thong_bao_claude"])


def test_anh_khong_lien_quan_mo_qua_nhieu_sai_dinh_dang(ai, client, monkeypatch):
    hs = _dang_nhap_email(monkeypatch, client, "hs@example.com")
    ai.tra_loi = [_kq(de_bai="", phan_ung=[], chat_luong_anh={"doc_duoc": False, "van_de": "khong_phai_de_hoa",
                                                                "mo_ta": "Ảnh là phong cảnh núi, không phải đề Hoá học."})]
    r = _doc(hs)
    assert r.status_code == 422 and r.json() == {"loi": "Ảnh là phong cảnh núi, không phải đề Hoá học.", "ma": "anh_khong_lien_quan"}
    ai.tra_loi = [_kq(phan_ung=[], de_bai="Câu 2 ...", chat_luong_anh={"doc_duoc": False, "van_de": "mo",
                                                                         "mo_ta": "Ảnh hơi mờ, không đọc được công thức ở câu 2."})]
    r = _doc(hs)
    assert r.status_code == 422 and r.json()["loi"] == "Ảnh hơi mờ, không đọc được công thức ở câu 2 — hãy chụp gần và rõ hơn."
    ai.tra_loi = [_kq(chat_luong_anh={"doc_duoc": True, "van_de": "bi_cat", "mo_ta": "Ảnh bị cắt mất phần cuối câu 1."})]
    assert _doc(hs).json()["canh_bao"] == ["Ảnh bị cắt mất phần cuối câu 1."]  # đọc được một phần → cảnh báo, vẫn xác nhận
    n = len(ai.goi)
    r = _doc(hs, [(f"{i}.png", _anh(), "image/png") for i in range(5)])
    assert r.status_code == 400 and r.json()["ma"] == "qua_nhieu_anh"
    r = _doc(hs, [("a.gif", _anh("GIF"), "image/gif")])
    assert r.status_code == 415 and "GIF" in r.json()["loi"]
    r = _doc(hs, [("toi.jpg", _anh("JPEG", mau=(3, 3, 3), chu=""), "image/jpeg")])
    assert r.status_code == 422 and "quá tối" in r.json()["loi"]
    assert len(ai.goi) == n  # lỗi ảnh phát hiện trước khi gọi AI
    assert client.post("/api/co-che/doc-anh", files=[("anh", ("a.png", _anh(), "image/png"))]).status_code in (401, 403)


def test_nut_sua_chat_smiles_hoac_ten(ai, client, monkeypatch):
    hs = _dang_nhap_email(monkeypatch, client, "hs@example.com")
    d = hs.post("/api/co-che/chat", json={"chuoi": "CC(C)=C"}, headers=hs.h).json()
    assert d["smiles"] == "CC(C)=C" and d["cach"] == "SMILES" and d["anh"].startswith("data:image/png")
    monkeypatch.setattr(mo_hinh_3d, "pubchem_lookup", lambda q: {"ok": True, "smiles": "CC=C", "ten": "propene"} if q == "propene" else {})
    d = hs.post("/api/co-che/chat", json={"chuoi": "propene"}, headers=hs.h).json()
    assert d["smiles"] == "CC=C" and d["cach"].startswith("PubChem")
    r = hs.post("/api/co-che/chat", json={"chuoi": ""}, headers=hs.h)
    assert r.status_code == 400
