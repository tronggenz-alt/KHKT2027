"""Dạng đề cho chất đầu + tác nhân trên mũi tên (+ CTPT), và chế độ "Đề khó (HSG)". Không gọi mạng/AI."""
import copy
import json
from pathlib import Path
from types import SimpleNamespace

import pytest

from backend import llm, pipeline, solver
from backend.ai.providers.gemini import chat as gemini_chat, client as gemini_client
from backend.config import settings
from backend.parser import tach_chuoi, tach_tac_nhan
from backend.research import KetQuaTraCuu
from backend.schemas import BuocTrongDe, ChatTrongDe, LoiGiai, PhanTichDe
from backend.validator import cong_thuc_hill, cong_thuc_tu_chu, danh_sach_loi, kiem_tra_loi_giai

DEMO = Path(__file__).resolve().parent.parent / "backend" / "demo_data"
BAI = json.loads((DEMO / "propen_tac_nhan.json").read_text(encoding="utf-8"))


@pytest.mark.parametrize(
    "de, chat, tac_nhan",
    [
        ("CH2=CH–CH3 →[HBr] A →[NaOH, t°] B", ["CH2=CH–CH3", "A", "B"], ["HBr", "NaOH, t°"]),
        ("C2H5OH -(CuO, t°)-> X -(AgNO3/NH3, t°)-> Y", ["C2H5OH", "X", "Y"], ["CuO, t°", "AgNO3/NH3, t°"]),
        ("CH2=CH-CH3 --HBr--> A --> B", ["CH2=CH-CH3", "A", "B"], ["HBr", ""]),
        ("CH2=CH-CH3 -> A -[Br2]-> B", ["CH2=CH-CH3", "A", "B"], ["", "Br2"]),
        ("CH4 → C2H2 (1) → CH3CHO", ["CH4", "C2H2 (1)", "CH3CHO"], ["", ""]),
    ],
)
def test_tach_chuoi_co_tac_nhan(de, chat, tac_nhan):
    assert tach_chuoi(de) == chat
    assert tach_tac_nhan(de) == tac_nhan


@pytest.mark.parametrize(
    "chu, ctpt",
    [("C3H6O", "C3H6O"), ("CH3COOC2H5", "C4H8O2"), ("CH2=CH–CH3", "C3H6"), ("CH≡CH", "C2H2"),
     ("(CH3COO)2Ca", "C4H6CaO4"), ("CH3-CH(OH)-CH3", "C3H8O"), ("C₂H₅OH", "C2H6O")],
)
def test_cong_thuc_tu_chu(chu, ctpt):
    assert cong_thuc_hill(cong_thuc_tu_chu(chu)) == ctpt


@pytest.mark.parametrize("chu", ["X", "A", "Toluen", "glucozơ", "(C6H10O5)n", "[C6H7O2(OH)3]n", "CuSO4.5H2O", ""])
def test_khong_phai_cong_thuc(chu):
    assert cong_thuc_tu_chu(chu) is None


def _de_va_loi_giai():
    return pipeline._de_tu_bai_demo(copy.deepcopy(BAI)), LoiGiai.model_validate(copy.deepcopy(BAI["loi_giai"]))


def test_bai_mau_tac_nhan_hop_le_va_dien_ctpt():
    de, sol = _de_va_loi_giai()
    assert de.cac_chat[3].ctpt_de_cho == "C3H6O" and de.cac_chat[3].la_chat_an
    assert de.cac_buoc[0].dieu_kien_de_cho == "HBr"
    assert danh_sach_loi(kiem_tra_loi_giai(sol, de)) == []
    assert [c.ctpt for c in sol.chat_an] == ["C3H7Br", "C3H8O", "C3H6O", "C4H7NO"]


def test_sai_ctpt_de_cho_bi_phat_hien():
    de, sol = _de_va_loi_giai()
    # AI chọn nhầm C là propan-2-ol (C3H8O) trong khi đề cho C3H6O
    sol.chat_an[2].smiles = "CC(C)O"
    loi = danh_sach_loi(kiem_tra_loi_giai(sol, de))
    assert any("chất 'C': đề cho CTPT C3H6O" in x and "C3H8O" in x for x in loi), loi


def test_sai_cong_thuc_chat_da_biet_bi_phat_hien():
    de = PhanTichDe(de_bai="C2H5OH → CH3CHO",
                    cac_chat=[ChatTrongDe(ki_hieu="C2H5OH"), ChatTrongDe(ki_hieu="CH3CHO")],
                    cac_buoc=[BuocTrongDe(so=1, chat_dau="C2H5OH", chat_sau="CH3CHO")])
    sol = LoiGiai.model_validate({
        "so_do": [{"ki_hieu": "C2H5OH", "smiles": "CCO"}, {"ki_hieu": "CH3CHO", "smiles": "CC(=O)O"}],
        "cac_buoc": [{"so": 1, "chat_dau": "C2H5OH", "chat_sau": "CH3CHO", "phuong_trinh": "",
                      "chat_tham_gia": [{"ten": "a", "smiles": "CCO"}, {"ten": "O2", "smiles": "O=O"}],
                      "san_pham": [{"ten": "b", "smiles": "CC(=O)O"}, {"ten": "H2O", "smiles": "O"}]}],
    })
    loi = danh_sach_loi(kiem_tra_loi_giai(sol, de))
    assert any("'CH3CHO': công thức đề viết C2H4O" in x for x in loi), loi


def test_ki_hieu_mot_chu_khong_bi_hieu_la_nguyen_to():
    # "B", "C", "Y" trùng kí hiệu nguyên tố nhưng là chất ẩn → không so CTPT
    de, sol = _de_va_loi_giai()
    for c in de.cac_chat:
        c.la_chat_an = False
        if c.ki_hieu != "C":
            c.ctpt_de_cho = ""
    assert danh_sach_loi(kiem_tra_loi_giai(sol, de)) == []


def test_demo_so_khop_ca_tac_nhan():
    chuoi = "CH2=CH–CH3 →[HBr] A →[NaOH, t°] B →[CuO, t°] C (C3H6O) →[HCN] D"
    assert pipeline.tim_bai_demo(chuoi)["de_bai"] == BAI["de_bai"]
    assert pipeline.tim_bai_demo(chuoi.replace("[HBr]", "[HCl]")) is None
    assert pipeline.tim_bai_demo("CH4 → C2H2 → CH3CHO → C2H5OH → CH3COOH → CH3COOC2H5") is not None


def test_che_do_hsg_dung_effort_cao_va_gemini_pro(monkeypatch):
    monkeypatch.setattr(settings, "effort", "medium")
    with llm.dung_ke_hoach(llm.KeHoachAI(bac="pro")):  # pipeline đặt bậc pro cho đề HSG
        phien = solver.tao_phien(hsg=True)
    assert phien.effort == "high" and phien.gemini_uu_tien == settings.gemini_model_hsg
    assert solver.tao_phien().gemini_uu_tien == settings.gemini_model  # không có kế hoạch: bậc flash
    de, _ = _de_va_loi_giai()
    assert "HỌC SINH GIỎI" in solver._tao_yeu_cau(de, KetQuaTraCuu(), hsg=True)
    assert "HỌC SINH GIỎI" not in solver._tao_yeu_cau(de, KetQuaTraCuu())


def test_gemini_pro_het_han_muc_thi_lui_ve_mo_hinh_thuong(monkeypatch):
    from google.genai import errors

    da_goi = []

    class Models:
        def generate_content(self, model, contents, config):
            da_goi.append(model)
            if model == "pro-test":
                raise errors.APIError(429, {"error": {"message": "quota", "status": "RESOURCE_EXHAUSTED"}})
            return SimpleNamespace(model=model)

    monkeypatch.setattr(gemini_client, "_gemini", lambda: SimpleNamespace(models=Models()))
    monkeypatch.setattr(gemini_chat, "_gemini_model_dung", "")
    monkeypatch.setattr(gemini_chat, "_gemini_het_han_muc", {})
    monkeypatch.setattr(settings, "gemini_model", "flash-test")
    monkeypatch.setattr(gemini_chat, "_ngu", lambda s: None)
    bao = []
    _, model, contents = gemini_chat._goi_gemini(system="s", contents=["cu"], max_tokens=10, tools=None, output_schema=None,
                                         uu_tien="pro-test", bao=bao.append, lam_phang=lambda: ["phang"])
    assert model == "flash-test" and da_goi == ["pro-test", "flash-test"]
    assert contents == ["phang"]  # đổi mô hình giữa phiên → lịch sử được làm phẳng
    assert any("chuyển sang flash-test" in m for m in bao)


def _goi_gia(monkeypatch, loi_theo_mo_hinh):
    """Client Gemini giả: loi_theo_mo_hinh[model] = danh sách lỗi lần lượt (None = thành công)."""
    from google.genai import errors

    da_goi, da_ngu = [], []

    class Models:
        def generate_content(self, model, contents, config):
            da_goi.append(model)
            ds = loi_theo_mo_hinh.get(model, [])
            loi = ds.pop(0) if ds else None
            if loi:
                raise errors.APIError(loi[0], {"error": {"message": loi[1], "status": "X"}})
            return SimpleNamespace(model=model)

    monkeypatch.setattr(gemini_client, "_gemini", lambda: SimpleNamespace(models=Models()))
    monkeypatch.setattr(gemini_chat, "_gemini_model_dung", "")
    monkeypatch.setattr(gemini_chat, "GEMINI_MODEL_DU_PHONG", ["du-phong"])
    monkeypatch.setattr(settings, "gemini_model", "chinh")
    monkeypatch.setattr(gemini_chat, "_ngu", da_ngu.append)
    monkeypatch.setattr(gemini_chat, "_gemini_het_han_muc", {})
    return da_goi, da_ngu


def test_gemini_qua_tai_thu_lai_cung_mo_hinh(monkeypatch):
    da_goi, da_ngu = _goi_gia(monkeypatch, {"chinh": [(503, "high demand"), (503, "high demand")]})
    _, model, _ = gemini_chat._goi_gemini(system="s", contents=[], max_tokens=10, tools=None, output_schema=None)
    assert model == "chinh" and da_goi == ["chinh"] * 3 and da_ngu == [4, 8]


def test_gemini_qua_tai_mai_thi_doi_mo_hinh(monkeypatch):
    da_goi, _ = _goi_gia(monkeypatch, {"chinh": [(503, "x")] * 3})
    _, model, _ = gemini_chat._goi_gemini(system="s", contents=[], max_tokens=10, tools=None, output_schema=None)
    assert model == "du-phong" and da_goi == ["chinh"] * 3 + ["du-phong"]


def test_gemini_429_cho_ngan_roi_thu_lai(monkeypatch):
    da_goi, da_ngu = _goi_gia(monkeypatch, {"chinh": [(429, "Please retry in 12.5s.")]})
    _, model, _ = gemini_chat._goi_gemini(system="s", contents=[], max_tokens=10, tools=None, output_schema=None)
    assert model == "chinh" and da_ngu == [13.5]


def test_gemini_het_han_muc_ca_hai_mo_hinh_bao_loi(monkeypatch):
    _goi_gia(monkeypatch, {"chinh": [(429, "quota limit: 0")], "du-phong": [(429, "quota limit: 0")]})
    with pytest.raises(llm.LoiAI) as e:
        gemini_chat._goi_gemini(system="s", contents=[], max_tokens=10, tools=None, output_schema=None)
    assert e.value.loai == "gioi_han"


def test_mo_hinh_het_han_muc_bi_bo_qua_lan_sau(monkeypatch):
    da_goi, _ = _goi_gia(monkeypatch, {"pro": [(429, "quota limit: 0")]})
    gemini_chat._goi_gemini(system="s", contents=[], max_tokens=10, tools=None, output_schema=None, uu_tien="pro")
    da_goi.clear()
    _, model, _ = gemini_chat._goi_gemini(system="s", contents=[], max_tokens=10, tools=None, output_schema=None, uu_tien="pro")
    assert model == "chinh" and da_goi == ["chinh"]


@pytest.mark.parametrize(
    "de_cho, ai, ket_qua",
    [("HBr", "nhiệt độ phòng", "HBr, nhiệt độ phòng"), ("NaOH, t°", "t°", "NaOH, t°"), ("CuO, t°", "to", "CuO, t°"),
     ("HCN", "OH-", "HCN, OH-"), ("1) O3; 2) Zn/H2O", "", "1) O3; 2) Zn/H2O"), ("", "Ni, t°", "Ni, t°"),
     ("Br2, as, 1:1", "ánh sáng", "Br2, as, 1:1"), ("CuO", "đun nóng", "CuO, đun nóng")],
)
def test_giu_tac_nhan_de_cho_tren_mui_ten(de_cho, ai, ket_qua):
    de, sol = _de_va_loi_giai()
    de.cac_buoc[0].dieu_kien_de_cho = de_cho
    sol.cac_buoc[0].dieu_kien = ai
    pipeline.giu_tac_nhan_de_cho(sol, de)
    assert sol.cac_buoc[0].dieu_kien == ket_qua


def test_dep_cong_thuc():
    from backend.image_export import dep_cong_thuc
    assert dep_cong_thuc("CH3-CHBr-CH3 + OH- → propan-2-ol") == "CH3–CHBr–CH3 + OH- → propan-2-ol"
    assert dep_cong_thuc("CH3-C(OH)(CN)-CH3") == "CH3–C(OH)(CN)–CH3"


def test_bao_loi_ro_khi_goi_mien_phi(monkeypatch):
    _goi_gia(monkeypatch, {
        "pro": [(429, "Quota exceeded for metric: generate_content_free_tier_requests, limit: 0, model: pro")],
        "chinh": [(429, "Quota exceeded for metric: generate_content_free_tier_requests, limit: 20. Please retry in 17h22m49s. "
                        "GenerateRequestsPerDayPerProjectPerModel-FreeTier retryDelay: '62569s'")],
    })
    bao = []
    _, model, _ = gemini_chat._goi_gemini(system="s", contents=[], max_tokens=10, tools=None, output_schema=None,
                                  uu_tien="pro", bao=bao.append)
    assert model == "du-phong"
    assert any("gói miễn phí không được dùng mô hình này" in m for m in bao), bao
    assert any("hết lượt trong ngày" in m and "17 giờ" in m for m in bao), bao
    assert gemini_chat._gemini_het_han_muc["chinh"] - __import__("time").time() > 60000  # bỏ qua tới khi có lượt mới
