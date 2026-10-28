"""Kiểm tra validator: SMILES, cân bằng nguyên tố/điện tích, tính nhất quán chuỗi."""
import json
from pathlib import Path

import pytest

from backend.schemas import ChatPhanUng as C
from backend.schemas import LoiGiai
from backend.validator import cong_thuc_hill, danh_sach_loi, dem_nguyen_to, kiem_tra_can_bang, kiem_tra_loi_giai, mol_tu_smiles

DEMO = Path(__file__).resolve().parent.parent / "backend" / "demo_data"


def test_dem_nguyen_to_ke_ca_h_an():
    mol, _ = mol_tu_smiles("CCO")
    c, q = dem_nguyen_to(mol)
    assert dict(c) == {"C": 2, "H": 6, "O": 1} and q == 0
    assert cong_thuc_hill(c) == "C2H6O"


def test_smiles_sai():
    mol, err = mol_tu_smiles("C1CC")
    assert mol is None and "không hợp lệ" in err
    assert mol_tu_smiles("")[1] == "SMILES trống"


@pytest.mark.parametrize(
    "trai, phai",
    [
        ([C(ten="CH4", smiles="C", he_so=2)], [C(ten="C2H2", smiles="C#C"), C(ten="H2", smiles="[H][H]", he_so=3)]),
        ([C(ten="C2H2", smiles="C#C"), C(ten="H2O", smiles="O")], [C(ten="CH3CHO", smiles="CC=O")]),
        ([C(ten="axit", smiles="CC(=O)O"), C(ten="ancol", smiles="CCO")], [C(ten="este", smiles="CC(=O)OCC"), C(ten="H2O", smiles="O")]),
        ([C(ten="C6H12O6", smiles="OCC1OC(O)C(O)C(O)C1O")], [C(ten="C2H5OH", smiles="CCO", he_so=2), C(ten="CO2", smiles="O=C=O", he_so=2)]),
        ([C(ten="CH3CHO", smiles="CC=O", he_so=2), C(ten="O2", smiles="O=O")], [C(ten="CH3COOH", smiles="CC(=O)O", he_so=2)]),
    ],
)
def test_phuong_trinh_can_bang(trai, phai):
    kq = kiem_tra_can_bang(trai, phai)
    assert kq.can_bang, kq.loi


def test_phat_hien_chua_can_bang():
    kq = kiem_tra_can_bang([C(ten="CH4", smiles="C")], [C(ten="C2H2", smiles="C#C"), C(ten="H2", smiles="[H][H]", he_so=3)])
    assert not kq.can_bang
    assert "chưa cân bằng nguyên tố" in kq.loi[0] and "C2H8" in kq.loi[0]


def test_can_bang_dien_tich():
    # CH3COOH + NaOH → CH3COONa + H2O (dạng ion)
    ok = kiem_tra_can_bang([C(ten="a", smiles="CC(=O)O"), C(ten="b", smiles="[Na+].[OH-]")],
                           [C(ten="c", smiles="CC(=O)[O-].[Na+]"), C(ten="d", smiles="O")])
    assert ok.can_bang
    sai = kiem_tra_can_bang([C(ten="a", smiles="CC(=O)O")], [C(ten="c", smiles="CC(=O)[O-]"), C(ten="h", smiles="[H]")])
    assert any("điện tích" in x for x in sai.loi)


def test_polime_bo_qua_dau_noi():
    trai = [C(ten="tinh bột", smiles="*OC1C(CO)OC(*)C(O)C1O"), C(ten="nước", smiles="O")]
    phai = [C(ten="glucose", smiles="OCC1OC(O)C(O)C(O)C1O")]
    assert kiem_tra_can_bang(trai, phai).can_bang


def test_he_so_am_va_smiles_sai_trong_phuong_trinh():
    kq = kiem_tra_can_bang([C(ten="x", smiles="C", he_so=0)], [C(ten="y", smiles="C1CC")])
    assert not kq.can_bang
    assert any("hệ số" in x for x in kq.loi) and any("không hợp lệ" in x for x in kq.loi)


@pytest.mark.parametrize("f", sorted(DEMO.glob("*.json")), ids=lambda p: p.stem)
def test_loi_giai_mau_hop_le(f):
    sol = LoiGiai.model_validate(json.loads(f.read_text(encoding="utf-8"))["loi_giai"])
    assert danh_sach_loi(kiem_tra_loi_giai(sol)) == []


def test_chat_dau_khong_co_trong_phuong_trinh():
    data = json.loads((DEMO / "metan_etyl_axetat.json").read_text(encoding="utf-8"))["loi_giai"]
    data["cac_buoc"][1]["chat_tham_gia"][0]["smiles"] = "C=C"  # sai chất đầu (và mất cân bằng)
    sol = LoiGiai.model_validate(data)
    kq = kiem_tra_loi_giai(sol)
    assert 2 in kq["buoc"]
    assert sol.cac_buoc[1].can_kiem_tra
    assert any("chất đầu 'C2H2'" in x for x in kq["buoc"][2])
