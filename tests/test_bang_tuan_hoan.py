"""
tests/test_bang_tuan_hoan.py
Kiểm thử bộ dữ liệu chuẩn 118 nguyên tố hoá học phục vụ Web và App Unity Standalone.
"""

import json
from pathlib import Path
import pytest
from rdkit import Chem

GOC_DU_AN = Path(__file__).resolve().parent.parent
TEP_WEB = GOC_DU_AN / "frontend" / "hoa-hoc" / "bang-tuan-hoan" / "nguyen-to.json"
TEP_UNITY = GOC_DU_AN / "unity" / "PhongLabHoa3D" / "Assets" / "StreamingAssets" / "ptn" / "nguyen-to.json"

@pytest.fixture(scope="module")
def danh_sach_nguyen_to():
    assert TEP_WEB.exists(), f"Không tìm thấy tệp {TEP_WEB}"
    assert TEP_UNITY.exists(), f"Không tìm thấy tệp {TEP_UNITY}"
    with open(TEP_WEB, "r", encoding="utf-8") as f:
        data_web = json.load(f)
    with open(TEP_UNITY, "r", encoding="utf-8") as f:
        data_unity = json.load(f)
    assert len(data_web) == 118
    assert len(data_unity) == 118
    return data_web

def test_du_118_nguyen_to_khong_trung(danh_sach_nguyen_to):
    assert len(danh_sach_nguyen_to) == 118
    ds_z = [nt["z"] for nt in danh_sach_nguyen_to]
    assert len(set(ds_z)) == 118
    assert min(ds_z) == 1
    assert max(ds_z) == 118
    assert ds_z == list(range(1, 119))

def test_khop_rdkit_toan_bo(danh_sach_nguyen_to):
    pt = Chem.GetPeriodicTable()
    for nt in danh_sach_nguyen_to:
        z = nt["z"]
        sym = nt["ki_hieu"]
        assert sym == pt.GetElementSymbol(z)
        if z <= 92 and z not in (43, 61):
            mass_rdkit = pt.GetAtomicWeight(z)
            mass_data = nt["nguyen_tu_khoi_so"]
            assert abs(mass_data - mass_rdkit) <= 0.1

def test_vi_tri_nguyen_to_mau(danh_sach_nguyen_to):
    map_sym = {nt["ki_hieu"]: nt for nt in danh_sach_nguyen_to}

    # H (chu kì 1, IA)
    assert map_sym["H"]["chu_ky"] == 1
    assert map_sym["H"]["nhom_ab"] == "IA"
    assert map_sym["H"]["nhom"] == 1

    # He (1, VIIIA)
    assert map_sym["He"]["chu_ky"] == 1
    assert map_sym["He"]["nhom_ab"] == "VIIIA"
    assert map_sym["He"]["nhom"] == 18

    # Na (3, IA)
    assert map_sym["Na"]["chu_ky"] == 3
    assert map_sym["Na"]["nhom_ab"] == "IA"

    # Fe (4, VIIIB)
    assert map_sym["Fe"]["chu_ky"] == 4
    assert map_sym["Fe"]["nhom_ab"] == "VIIIB"

    # Cu (4, IB)
    assert map_sym["Cu"]["chu_ky"] == 4
    assert map_sym["Cu"]["nhom_ab"] == "IB"

    # Br (4, VIIA)
    assert map_sym["Br"]["chu_ky"] == 4
    assert map_sym["Br"]["nhom_ab"] == "VIIA"

    # I (5, VIIA)
    assert map_sym["I"]["chu_ky"] == 5
    assert map_sym["I"]["nhom_ab"] == "VIIA"

def test_trang_thai_chat_long_25C(danh_sach_nguyen_to):
    map_sym = {nt["ki_hieu"]: nt for nt in danh_sach_nguyen_to}
    assert map_sym["Hg"]["trang_thai_25C"] == "lỏng"
    assert map_sym["Br"]["trang_thai_25C"] == "lỏng"

def test_lanthanide_actinide(danh_sach_nguyen_to):
    for nt in danh_sach_nguyen_to:
        z = nt["z"]
        if 58 <= z <= 71:
            assert nt["loai"] == "lanthanide"
            assert nt["nhom_ab"] == "Lanthanide"
            assert nt["khoi"] == "f"
        elif 90 <= z <= 103:
            assert nt["loai"] == "actinide"
            assert nt["nhom_ab"] == "Actinide"
            assert nt["khoi"] == "f"

def test_cau_hinh_electron_va_chi_so_tren(danh_sach_nguyen_to):
    core_electrons = {"He": 2, "Ne": 10, "Ar": 18, "Kr": 36, "Xe": 54, "Rn": 86}
    import re

    for nt in danh_sach_nguyen_to:
        cfg = nt["cau_hinh_e_chuan"]
        z = nt["z"]
        if cfg:
            tong_e = 0
            m_core = re.search(r'\[([A-Za-z]+)\]', cfg)
            if m_core:
                tong_e += core_electrons.get(m_core.group(1), 0)
            for m in re.findall(r'[spdf](\d+)', cfg):
                tong_e += int(m)
            assert tong_e == z, f"Tổng e lệch ở Z={z}: tính được {tong_e}, kỳ vọng {z}"
        
        # Kiểm tra chỉ số trên
        cfg_unicode = nt["cau_hinh_e"]
        if cfg_unicode:
            # Không còn chữ số ASCII sau phân lớp s, p, d, f
            assert not re.search(r'[spdf][0-9]', cfg_unicode), f"Còn chữ số thường ở Z={z}: {cfg_unicode}"
            assert not re.search(r'[spdf][0-9]', cfg_unicode), f"Còn chữ số thường ở Z={z}: {cfg_unicode}"
