"""Mục "Mô hình 3D": nhận diện chất và dựng cấu dạng 3D (không gọi mạng/AI: PubChem và AI được thay bằng hàm giả)."""
import pytest
from rdkit import Chem

from backend import mo_hinh_3d
from backend.llm import LoiAI


@pytest.fixture(autouse=True)
def khong_mang(monkeypatch):
    """Mặc định: PubChem không có gì, không có AI."""
    monkeypatch.setattr(mo_hinh_3d, "pubchem_lookup", lambda q: {"ok": False, "thong_bao": "không có"})
    monkeypatch.setattr(mo_hinh_3d, "_pubchem_theo_smiles", lambda s: None)
    monkeypatch.setattr(mo_hinh_3d, "_pubchem_sdf_3d", lambda cid: None)
    monkeypatch.setattr(mo_hinh_3d, "_ai_san_sang", lambda: False)
    monkeypatch.setattr(mo_hinh_3d, "_cache", {})


def test_dung_3d_rdkit_co_H_va_toa_do_3d():
    mol = mo_hinh_3d.dung_3d_rdkit("CC(=O)O")
    assert mol.GetNumAtoms() == 8  # 4 nguyên tử nặng + 4 H
    conf = mol.GetConformer()
    assert conf.Is3D() and len({round(conf.GetAtomPosition(i).z, 2) for i in range(mol.GetNumAtoms())}) > 1


@pytest.mark.parametrize("vao, smiles", [("CCO", "CCO"), ("c1ccccc1", "c1ccccc1"), ("C#N", "C#N"), ("CC(=O)OCC", "CC(=O)OCC")])
def test_nhan_dien_smiles_truoc_khi_tra_ten(vao, smiles, monkeypatch):
    goi = []
    monkeypatch.setattr(mo_hinh_3d, "pubchem_lookup", lambda q: goi.append(q) or {"ok": True, "smiles": "C"})
    nd = mo_hinh_3d.nhan_dien(vao)
    assert nd["smiles"] == smiles and nd["cach"] == "SMILES" and goi == []  # "CCO" không bị PubChem hiểu nhầm là tên thuốc


def test_nhan_dien_ten_qua_pubchem(monkeypatch):
    monkeypatch.setattr(mo_hinh_3d, "pubchem_lookup",
                        lambda q: {"ok": True, "ten": "Ethanol", "smiles": "CCO", "cid": 702, "url": "u"} if q == "ethanol" else {})
    nd = mo_hinh_3d.nhan_dien("ethanol")
    assert nd["smiles"] == "CCO" and nd["cach"] == "PubChem (theo tên)"


def test_nhan_dien_ten_tieng_viet_nho_ai(monkeypatch):
    monkeypatch.setattr(mo_hinh_3d, "_ai_san_sang", lambda: True)
    monkeypatch.setattr(mo_hinh_3d, "_hoi_ai", lambda t, anh, bao: mo_hinh_3d.NhanDien(
        ten="acetic acid (axit axetic)", ten_tieng_anh="acetic acid", smiles="CC(=O)O"))
    monkeypatch.setattr(mo_hinh_3d, "pubchem_lookup",
                        lambda q: {"ok": True, "ten": "Acetic Acid", "smiles": "CC(=O)O", "cid": 176} if q == "acetic acid" else {})
    nd = mo_hinh_3d.nhan_dien("axit axetic")
    assert nd["smiles"] == "CC(=O)O" and nd["cach"] == "AI nhận diện → PubChem" and nd["pubchem"]["cid"] == 176


def test_khong_ai_khong_nhan_ra_thi_bao_loi():
    with pytest.raises(LoiAI, match="Không nhận ra chất"):
        mo_hinh_3d.nhan_dien("axit lactic gi do")


def test_tao_mo_hinh_3d_tu_dung_bang_rdkit_khi_khong_co_pubchem():
    kq = mo_hinh_3d.tao_mo_hinh_3d("CC(C)(O)C#N")
    assert kq["cong_thuc"] == "C4H7NO" and kq["nguon_3d"].startswith("RDKit")
    mol = Chem.MolFromMolBlock(kq["sdf"], removeHs=False)
    assert mol is not None and mol.GetNumAtoms() == kq["so_nguyen_tu"] == 13 and mol.GetConformer().Is3D()
    assert kq["xyz"].splitlines()[0].strip() == "13"
    assert kq["anh_2d"].startswith("data:image/png;base64,")


def test_uu_tien_cau_dang_3d_cua_pubchem(monkeypatch):
    sdf = Chem.MolToMolBlock(mo_hinh_3d.dung_3d_rdkit("CCO"))
    monkeypatch.setattr(mo_hinh_3d, "_pubchem_theo_smiles", lambda s: {"cid": 702, "ten": "Ethanol", "url": "u", "iupac": "ethanol"})
    monkeypatch.setattr(mo_hinh_3d, "_pubchem_sdf_3d", lambda cid: sdf)
    kq = mo_hinh_3d.tao_mo_hinh_3d("CCO")
    assert kq["nguon_3d"] == "PubChem 3D conformer (CID 702)" and kq["ten"] == "Ethanol" and kq["cid"] == 702


def test_pubchem_3d_sai_chat_thi_dung_rdkit(monkeypatch):
    sdf_khac = Chem.MolToMolBlock(mo_hinh_3d.dung_3d_rdkit("CCC"))
    monkeypatch.setattr(mo_hinh_3d, "_pubchem_theo_smiles", lambda s: {"cid": 1, "ten": "X"})
    monkeypatch.setattr(mo_hinh_3d, "_pubchem_sdf_3d", lambda cid: sdf_khac)
    assert mo_hinh_3d.tao_mo_hinh_3d("CCO")["nguon_3d"].startswith("RDKit")


def test_polime_khong_dung_3d():
    with pytest.raises(LoiAI, match="polime"):
        mo_hinh_3d.tao_mo_hinh_3d("", smiles="*OC1C(CO)OC(*)C(O)C1O")


def test_api_3d(client):
    """Không cần đăng nhập với SMILES/tên tiếng Anh, nhưng vẫn phải có mã CSRF."""
    assert client.post("/api/3d", data={"van_ban": "CCO"}).status_code == 403
    h = {"X-CSRF-Token": client.get("/api/toi").json()["csrf"]}
    r = client.post("/api/3d", data={"van_ban": "CCO"}, headers=h)
    assert r.status_code == 200 and r.json()["cong_thuc"] == "C2H6O"
    r = client.post("/api/3d", data={"van_ban": ""}, headers=h)
    assert r.status_code == 400 and "Hãy nhập" in r.json()["loi"]


def test_3d_chua_dang_nhap_thi_khong_dung_ai(client, monkeypatch):
    monkeypatch.setattr(mo_hinh_3d, "_ai_san_sang", mo_hinh_3d.ai_san_sang)  # theo key của yêu cầu (bỏ hàm giả)
    monkeypatch.setattr(mo_hinh_3d, "_hoi_ai", lambda *a: pytest.fail("không được gọi AI khi chưa đăng nhập"))
    h = {"X-CSRF-Token": client.get("/api/toi").json()["csrf"]}
    r = client.post("/api/3d", data={"van_ban": "rượu gì đó"}, headers=h)
    assert r.status_code == 400 and "đăng nhập" in r.json()["loi"]


def test_cache_sqlite_va_thong_tin_nguyen_tu():
    # Lần 1: Dựng mô hình và lưu cache
    kq1 = mo_hinh_3d.tao_mo_hinh_3d("CC(=O)O")
    assert "nguyen_tu" in kq1 and len(kq1["nguyen_tu"]) == 8
    cac_elem = {nt["elem"] for nt in kq1["nguyen_tu"]}
    assert "C" in cac_elem and "O" in cac_elem and "H" in cac_elem
    # Có toạ độ x, y, z
    assert all("x" in nt and "y" in nt and "z" in nt for nt in kq1["nguyen_tu"])

    # Xoá cache bộ nhớ để kiểm tra cache SQLite
    mo_hinh_3d._cache.clear()
    kq2 = mo_hinh_3d.tao_mo_hinh_3d("CC(=O)O")
    assert kq2["cong_thuc"] == kq1["cong_thuc"]
    assert kq2["smiles"] == kq1["smiles"]

