"""Kiểm tra vẽ cấu tạo, xuất mol/sdf/cdxml và ảnh đáp án."""
import json
import math
import xml.dom.minidom
from pathlib import Path

import pytest
from PIL import Image
from rdkit import Chem
from rdkit.Chem.rdMolDescriptors import CalcMolFormula

from backend.chemdraw import tao_cdxml
from backend.image_export import RONG, tach_cong_thuc, tao_anh_dap_an
from backend.renderer import chuan_bi_mol, doan_so_do, ve_phan_tu, xuat_mol, xuat_sdf
from backend.schemas import LoiGiai

DEMO = Path(__file__).resolve().parent.parent / "backend" / "demo_data"


def _bai(ten="metan_etyl_axetat"):
    data = json.loads((DEMO / f"{ten}.json").read_text(encoding="utf-8"))
    return data["de_bai"], LoiGiai.model_validate(data["loi_giai"])


@pytest.mark.parametrize("smiles", ["C", "C#C", "CC=O", "CCO", "CC(=O)O", "CC(=O)OCC", "c1ccccc1", "Nc1ccccc1",
                                    "O=[N+]([O-])c1ccccc1", "*OC1C(CO)OC(*)C(O)C1O", "[H][H]", "[Na+].[OH-]"])
def test_ve_phan_tu(smiles):
    img = ve_phan_tu(smiles)
    assert img is not None and img.mode == "RGB"
    assert 30 < img.width < 900 and 30 < img.height < 900
    assert img.getpixel((0, 0)) == (255, 255, 255)  # nền trắng


def test_smiles_sai_tra_ve_none():
    assert ve_phan_tu("C1CC") is None


def test_do_dai_lien_ket_dong_nhat():
    for smi in ["c1ccccc1", "CCO", "OCC1OC(O)C(O)C(O)C1O"]:
        m = chuan_bi_mol(smi)
        c = m.GetConformer()
        for b in m.GetBonds():
            p, q = c.GetAtomPosition(b.GetBeginAtomIdx()), c.GetAtomPosition(b.GetEndAtomIdx())
            assert math.isclose(math.hypot(p.x - q.x, p.y - q.y), 1.5, rel_tol=0.08)


def test_mach_thang_theo_cong_thuc():
    m = chuan_bi_mol("CCOC(C)=O", cong_thuc="CH3COOC2H5")
    c = m.GetConformer()
    # Mạch chính nằm ngang: C(=O) (nguyên tử 3) ở bên trái O este (nguyên tử 2)
    assert c.GetAtomPosition(3).x < c.GetAtomPosition(2).x
    assert abs(c.GetAtomPosition(5).y) > 1.0  # =O dựng đứng


def test_tach_cong_thuc():
    assert tach_cong_thuc("CH3COOH") == [("CH", "n"), ("3", "sub"), ("COOH", "n")]
    assert tach_cong_thuc("2CH4")[0] == ("2CH", "n")
    assert ("n", "sub") in tach_cong_thuc("(C6H10O5)n")
    assert ("2+", "sup") in tach_cong_thuc("Mn2+, t°")
    assert tach_cong_thuc("NH4+") == [("NH", "n"), ("4", "sub"), ("+", "sup")]
    assert tach_cong_thuc("SO42-") == [("SO", "n"), ("4", "sub"), ("2-", "sup")]
    assert tach_cong_thuc("Fe3+")[-1] == ("3+", "sup")
    assert tach_cong_thuc("C6H5NH3+ + OH-")[-1] == ("-", "sup")


def test_doan_so_do_chuoi_thang():
    _, sol = _bai()
    doan = doan_so_do(sol)
    assert len(doan) == 1 and doan[0]["dau"].ki_hieu == "CH4" and len(doan[0]["buoc"]) == 5


def test_xuat_mol_sdf(tmp_path):
    assert xuat_mol("CCO", "ethanol", tmp_path / "e.mol")
    assert Chem.MolFromMolFile(str(tmp_path / "e.mol")).GetNumAtoms() == 3
    n = xuat_sdf([{"ki_hieu": "A", "ten": "ethanol", "smiles": "CCO"}, {"ki_hieu": "B", "ten": "sai", "smiles": "C1C"}], tmp_path / "a.sdf")
    assert n == 1


@pytest.mark.parametrize("ten", [p.stem for p in sorted(DEMO.glob("*.json"))])
def test_cdxml_doc_lai_duoc(tmp_path, ten):
    _, sol = _bai(ten)
    f = tmp_path / "so_do.cdxml"
    assert tao_cdxml(sol, f)
    xml.dom.minidom.parse(str(f))
    mols = [m for m in Chem.MolsFromCDXMLFile(str(f)) if m is not None]
    assert len(mols) == len(sol.so_do)
    if ten == "metan_etyl_axetat":
        assert [CalcMolFormula(m) for m in mols] == ["CH4", "C2H2", "C2H4O", "C2H6O", "C2H4O2", "C4H8O2"]


@pytest.mark.parametrize("ten", [p.stem for p in sorted(DEMO.glob("*.json"))])
def test_anh_dap_an(tmp_path, ten):
    de, sol = _bai(ten)
    out = tao_anh_dap_an(sol, de, tmp_path / "dap_an.png", mo_hinh="test")
    img = Image.open(out)
    assert img.width == RONG == 1920
    assert 600 < img.height < 4000
    assert img.convert("RGB").getpixel((5, img.height - 5)) == (255, 255, 255)


@pytest.mark.parametrize("smiles", ["CC(Br)c1ccccc1", "OC(C(=O)O)c1ccccc1", "C=Cc1ccccc1", "CC(O)c1ccc(Cl)cc1", "CCCc1ccccc1"])
def test_vong_benzen_nhanh_zigzag_ben_phai(smiles):
    """Công thức khung: vòng đứng đỉnh nhọn lên trên, nhánh lớn nhất ra ở đỉnh 30° rồi zigzag sang phải."""
    from backend.renderer import chuan_bi_mol

    mol = chuan_bi_mol(smiles)
    conf = mol.GetConformer()
    vong = [i for i in range(mol.GetNumAtoms()) if mol.GetAtomWithIdx(i).IsInRing()]
    noi = next(i for i in vong if any(not x.IsInRing() and x.GetSymbol() == "C" for x in mol.GetAtomWithIdx(i).GetNeighbors()))
    dau = next(x.GetIdx() for x in mol.GetAtomWithIdx(noi).GetNeighbors() if not x.IsInRing() and x.GetSymbol() == "C")
    p = lambda i: conf.GetAtomPosition(i)  # noqa: E731
    tx = sum(p(i).x for i in vong) / len(vong)
    ty = sum(p(i).y for i in vong) / len(vong)
    assert math.isclose(math.degrees(math.atan2(p(noi).y - ty, p(noi).x - tx)), 30, abs_tol=1)   # đỉnh 30°
    assert math.isclose(math.degrees(math.atan2(p(dau).y - p(noi).y, p(dau).x - p(noi).x)), 30, abs_tol=1)
    tiep = [x.GetIdx() for x in mol.GetAtomWithIdx(dau).GetNeighbors() if x.GetIdx() != noi]
    assert all(p(t).x > p(dau).x for t in tiep)          # nhánh đi tiếp sang phải (zigzag), không quay về vòng


def test_cong_thuc_khung_khong_ghi_chu_C():
    """Chỉ ghi nguyên tố khác C, H: nguyên tử C không có nhãn hiển thị; mạch zigzag 120°, liên kết ba thẳng hàng."""
    m = chuan_bi_mol("CCCC#CC")
    assert not any(a.HasProp("_displayLabel") for a in m.GetAtoms())
    c = m.GetConformer()
    xy = lambda i: (c.GetAtomPosition(i).x, c.GetAtomPosition(i).y)  # noqa: E731

    def goc(a, b, d):
        (ax, ay), (bx, by), (dx, dy) = xy(a), xy(b), xy(d)
        v1, v2 = (ax - bx, ay - by), (dx - bx, dy - by)
        return math.degrees(math.acos((v1[0] * v2[0] + v1[1] * v2[1]) / (math.hypot(*v1) * math.hypot(*v2))))

    assert math.isclose(goc(0, 1, 2), 120, abs_tol=1) and math.isclose(goc(1, 2, 3), 120, abs_tol=1)
    assert math.isclose(goc(2, 3, 4), 180, abs_tol=1) and math.isclose(goc(3, 4, 5), 180, abs_tol=1)


def test_can_theo_mau_lat_cho_trung_huong_chat_truoc():
    """Chất sau được lật để phần khung chung trùng hướng với chất trước ("theo cấu trúc chất ban đầu")."""
    from rdkit import Chem

    from backend.renderer import can_theo_mau

    smi = "OC(=O)c1ccccc1O"  # salicylic acid
    mau = chuan_bi_mol(smi)
    c = mau.GetConformer()
    for i in range(mau.GetNumAtoms()):  # mẫu là hình lật dọc
        p = c.GetAtomPosition(i)
        c.SetAtomPosition(i, (p.x, -p.y, 0.0))
    mol = chuan_bi_mol(smi)
    assert can_theo_mau(mol, mau)
    cm, cn = mau.GetConformer(), mol.GetConformer()
    assert all(abs(cm.GetAtomPosition(i).y - cn.GetAtomPosition(i).y) < 1e-6 for i in range(mol.GetNumAtoms()))
    assert not can_theo_mau(mol, Chem.Mol(mol))  # đã cùng hướng thì giữ nguyên


def test_mach_ho_khong_dao_thu_tu_viet():
    # CH3CHO → C2H5OH: mạch hở kiểu SGK chỉ được lật dọc, đầu CH3 vẫn ở bên trái
    mau = chuan_bi_mol("CC=O", cong_thuc="CH3CHO")
    mol = chuan_bi_mol("CCO", cong_thuc="C2H5OH", mau=mau)
    c = mol.GetConformer()
    assert c.GetAtomPosition(0).x < c.GetAtomPosition(2).x
