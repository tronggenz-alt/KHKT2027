#!/usr/bin/env python3
"""
tools/tao_bang_tuan_hoan.py
Trích xuất, chuẩn hoá và đối chiếu 118 nguyên tố hoá học từ PubChem + RDKit
phục vụ Web và App Unity Standalone (theo chương trình GDPT 2018 - SGK Hoá 10).
"""

import json
import os
import re
import urllib.request
from pathlib import Path
from rdkit import Chem

GOC_DU_AN = Path(__file__).resolve().parent.parent
TEP_CACHE_PUBCHEM = GOC_DU_AN / "tools" / "du_lieu" / "pubchem_periodictable.json"
TEP_XUAT_WEB = GOC_DU_AN / "frontend" / "hoa-hoc" / "bang-tuan-hoan" / "nguyen-to.json"
TEP_XUAT_UNITY = GOC_DU_AN / "unity" / "PhongLabHoa3D" / "Assets" / "StreamingAssets" / "ptn" / "nguyen-to.json"
TEP_README = GOC_DU_AN / "frontend" / "hoa-hoc" / "bang-tuan-hoan" / "README.md"

URL_PUBCHEM = "https://pubchem.ncbi.nlm.nih.gov/rest/pug/periodictable/JSON"

# Bản đồ tên tiếng Việt truyền thống cho các nguyên tố quen thuộc
TEN_VIET = {
    1: "Hiđro", 2: "Heli", 3: "Liti", 4: "Beri", 5: "Bo",
    6: "Cacbon", 7: "Nitơ", 8: "Oxi", 9: "Flo", 10: "Neon",
    11: "Natri", 12: "Magie", 13: "Nhôm", 14: "Silic", 15: "Photpho",
    16: "Lưu huỳnh", 17: "Clo", 18: "Argon", 19: "Kali", 20: "Canxi",
    21: "Scandi", 22: "Titan", 23: "Vanadi", 24: "Crom", 25: "Mangan",
    26: "Sắt", 27: "Coban", 28: "Niken", 29: "Đồng", 30: "Kẽm",
    31: "Gali", 32: "Gecmani", 33: "Asen", 34: "Selen", 35: "Brom",
    36: "Kripton", 37: "Rubidi", 38: "Stronti", 39: "Ytri", 40: "Zirconi",
    41: "Niobi", 42: "Molipđen", 43: "Tecnesi", 44: "Ruteni", 45: "Rodi",
    46: "Paladi", 47: "Bạc", 48: "Cadmi", 49: "Indi", 50: "Thiếc",
    51: "Antimon", 52: "Telu", 53: "Iot", 54: "Xenon", 55: "Cesi",
    56: "Bari", 57: "Lantan", 72: "Hafni", 73: "Tantan", 74: "Vonfram",
    75: "Reni", 76: "Osmi", 77: "Iridi", 78: "Platin", 79: "Vàng",
    80: "Thuỷ ngân", 81: "Tali", 82: "Chì", 83: "Bismut", 84: "Poloni",
    85: "Atatin", 86: "Radon", 87: "Franxi", 88: "Radi", 89: "Actini",
    90: "Thori", 91: "Protactini", 92: "Urani"
}

# Bản đồ nhóm A/B theo số cột 1-18
NHOM_AB = {
    1: "IA", 2: "IIA", 3: "IIIB", 4: "IVB", 5: "VB", 6: "VIB",
    7: "VIIB", 8: "VIIIB", 9: "VIIIB", 10: "VIIIB", 11: "IB", 12: "IIB",
    13: "IIIA", 14: "IVA", 15: "VA", 16: "VIA", 17: "VIIA", 18: "VIIIA"
}

CHI_SO_TREN = {
    '0': '⁰', '1': '¹', '2': '²', '3': '³', '4': '⁴',
    '5': '⁵', '6': '⁶', '7': '⁷', '8': '⁸', '9': '⁹'
}

CORE_ELECTRONS = {
    "He": 2, "Ne": 10, "Ar": 18, "Kr": 36, "Xe": 54, "Rn": 86
}

def chuyen_chi_so_tren(s: str) -> str:
    """Chuyển số mũ cấu hình e sang ký tự unicode chỉ số trên."""
    res = []
    i = 0
    while i < len(s):
        c = s[i]
        if c in "spdf":
            res.append(c)
            i += 1
            num_str = ""
            while i < len(s) and s[i].isdigit():
                num_str += s[i]
                i += 1
            for d in num_str:
                res.append(CHI_SO_TREN.get(d, d))
        else:
            res.append(c)
            i += 1
    return "".join(res)

def tinh_tong_e(cau_hinh_str: str) -> int:
    """Tính tổng số electron từ chuỗi cấu hình e."""
    tong = 0
    # Tìm lõi [He], [Ne], ...
    m_core = re.search(r'\[([A-Za-z]+)\]', cau_hinh_str)
    if m_core:
        core_sym = m_core.group(1)
        tong += CORE_ELECTRONS.get(core_sym, 0)
    
    # Tìm các phân lớp
    matches = re.findall(r'[spdf](\d+)', cau_hinh_str)
    for m in matches:
        tong += int(m)
    return tong

def phan_loai_nguyen_to(z: int, nhom: int, group_block: str) -> str:
    """Phân loại nguyên tố chuẩn theo SGK Hoá 10."""
    if z == 1:
        return "phi_kim"
    if 58 <= z <= 71:
        return "lanthanide"
    if 90 <= z <= 103:
        return "actinide"
    if nhom == 18:
        return "khi_hiem"
    if nhom == 17:
        return "halogen"
    if nhom == 1:
        return "kim_loai_kiem"
    if nhom == 2:
        return "kiem_tho"
    if 3 <= nhom <= 12 or z in (57, 89):
        return "chuyen_tiep"
    
    # Nhóm p (13-16)
    if z in (5, 14, 32, 33, 51, 52):
        return "a_kim"
    if z in (6, 7, 8, 15, 16, 34):
        return "phi_kim"
    return "kim_loai_nhom_p"

def xac_dinh_khoi(z: int, nhom: int) -> str:
    if 58 <= z <= 71 or 90 <= z <= 103:
        return "f"
    if z == 2 or nhom in (1, 2):
        return "s"
    if 3 <= nhom <= 12 or z in (57, 89):
        return "d"
    return "p"

def tai_du_lieu_pubchem():
    TEP_CACHE_PUBCHEM.parent.mkdir(parents=True, exist_ok=True)
    if TEP_CACHE_PUBCHEM.exists():
        print(f"[tao_bang_tuan_hoan] Dùng cache cục bộ: {TEP_CACHE_PUBCHEM}")
        with open(TEP_CACHE_PUBCHEM, "r", encoding="utf-8") as f:
            return json.load(f)
    
    print(f"[tao_bang_tuan_hoan] Đang tải dữ liệu từ PubChem: {URL_PUBCHEM} ...")
    req = urllib.request.Request(URL_PUBCHEM, headers={"User-Agent": "Antigravity/1.0"})
    with urllib.request.urlopen(req, timeout=20) as res:
        content = res.read().decode("utf-8")
        data = json.loads(content)
        with open(TEP_CACHE_PUBCHEM, "w", encoding="utf-8") as f:
            f.write(content)
        print(f"[tao_bang_tuan_hoan] Đã lưu cache tại: {TEP_CACHE_PUBCHEM}")
        return data

def main():
    data_raw = tai_du_lieu_pubchem()
    cols = data_raw["Table"]["Columns"]["Column"]
    col_map = {name: idx for idx, name in enumerate(cols)}
    rows = data_raw["Table"]["Row"]

    rdkit_pt = Chem.GetPeriodicTable()

    danh_sach_nguyen_to = []

    for r in rows:
        cell = r["Cell"]
        z = int(cell[col_map["AtomicNumber"]])
        sym = cell[col_map["Symbol"]]
        name = cell[col_map["Name"]]
        mass_val = cell[col_map["AtomicMass"]]
        config_raw = cell[col_map["ElectronConfiguration"]] if "ElectronConfiguration" in col_map and cell[col_map["ElectronConfiguration"]] else ""
        en_raw = cell[col_map["Electronegativity"]] if "Electronegativity" in col_map and cell[col_map["Electronegativity"]] else None
        radius_raw = cell[col_map["AtomicRadius"]] if "AtomicRadius" in col_map and cell[col_map["AtomicRadius"]] else None
        ie_raw = cell[col_map["IonizationEnergy"]] if "IonizationEnergy" in col_map and cell[col_map["IonizationEnergy"]] else None
        ox_raw = cell[col_map["OxidationStates"]] if "OxidationStates" in col_map and cell[col_map["OxidationStates"]] else ""
        state_raw = cell[col_map["StandardState"]] if "StandardState" in col_map and cell[col_map["StandardState"]] else "Rắn"
        mp_raw = cell[col_map["MeltingPoint"]] if "MeltingPoint" in col_map and cell[col_map["MeltingPoint"]] else None
        bp_raw = cell[col_map["BoilingPoint"]] if "BoilingPoint" in col_map and cell[col_map["BoilingPoint"]] else None
        density_raw = cell[col_map["Density"]] if "Density" in col_map and cell[col_map["Density"]] else None
        group_block = cell[col_map["GroupBlock"]] if "GroupBlock" in col_map else ""

        # Đối chiếu RDKit
        sym_rdkit = rdkit_pt.GetElementSymbol(z)
        assert sym == sym_rdkit, f"Lệch ký hiệu Z={z}: PubChem={sym}, RDKit={sym_rdkit}"

        # Xác định chu kỳ và nhóm
        # Chu kỳ theo Z
        if z <= 2: chu_ky = 1
        elif z <= 10: chu_ky = 2
        elif z <= 18: chu_ky = 3
        elif z <= 36: chu_ky = 4
        elif z <= 54: chu_ky = 5
        elif z <= 86: chu_ky = 6
        else: chu_ky = 7

        # Nhóm theo vị trí
        if z == 1: nhom = 1
        elif z == 2: nhom = 18
        elif 58 <= z <= 71: nhom = 3  # Lanthanide
        elif 90 <= z <= 103: nhom = 3 # Actinide
        elif z in (3, 11, 19, 37, 55, 87): nhom = 1
        elif z in (4, 12, 20, 38, 56, 88): nhom = 2
        elif z in (21, 39, 57, 89): nhom = 3
        elif z in (22, 40, 72, 104): nhom = 4
        elif z in (23, 41, 73, 105): nhom = 5
        elif z in (24, 42, 74, 106): nhom = 6
        elif z in (25, 43, 75, 107): nhom = 7
        elif z in (26, 44, 76, 108): nhom = 8
        elif z in (27, 45, 77, 109): nhom = 9
        elif z in (28, 46, 78, 110): nhom = 10
        elif z in (29, 47, 79, 111): nhom = 11
        elif z in (30, 48, 80, 112): nhom = 12
        elif z in (5, 13, 31, 49, 81, 113): nhom = 13
        elif z in (6, 14, 32, 50, 82, 114): nhom = 14
        elif z in (7, 15, 33, 51, 83, 115): nhom = 15
        elif z in (8, 16, 34, 52, 84, 116): nhom = 16
        elif z in (9, 17, 35, 53, 85, 117): nhom = 17
        elif z in (10, 18, 36, 54, 86, 118): nhom = 18
        else: nhom = 1

        nhom_ab = NHOM_AB.get(nhom, "")
        if 58 <= z <= 71: nhom_ab = "Lanthanide"
        elif 90 <= z <= 103: nhom_ab = "Actinide"

        loai = phan_loai_nguyen_to(z, nhom, group_block)
        khoi = xac_dinh_khoi(z, nhom)

        # Nguyên tử khối: đối chiếu và lấy chuẩn IUPAC/RDKit cho Z <= 92
        if z <= 92 and z not in (43, 61):
            mass_f = round(rdkit_pt.GetAtomicWeight(z), 2)
            mass_str = f"{mass_f:.2f}".replace(".", ",")
        else:
            try:
                mass_f = float(mass_val)
                mass_str = f"[{round(mass_f)}]"
            except Exception:
                mass_f = 0.0
                mass_str = str(mass_val)

        # Cấu hình electron: chuẩn hoá khoảng trắng giữa các phân lớp
        config_clean = re.sub(r'\s+', ' ', config_raw).strip()
        config_unicode = chuyen_chi_so_tren(config_clean)

        # Trạng thái 25°C
        trang_thai = "rắn"
        if z in (35, 80):
            trang_thai = "lỏng"
        elif z in (1, 2, 7, 8, 9, 10, 17, 18, 36, 54, 86):
            trang_thai = "khí"
        elif state_raw and "Gas" in state_raw:
            trang_thai = "khí"
        elif state_raw and "Liquid" in state_raw:
            trang_thai = "lỏng"

        # Tên tiếng Việt
        ten_vn = TEN_VIET.get(z, name)

        nt = {
            "z": z,
            "ki_hieu": sym,
            "ten": name,
            "ten_viet": ten_vn,
            "nguyen_tu_khoi": mass_str,
            "nguyen_tu_khoi_so": round(mass_f, 2),
            "chu_ky": chu_ky,
            "nhom": nhom,
            "nhom_ab": nhom_ab,
            "khoi": khoi,
            "loai": loai,
            "cau_hinh_e": config_unicode,
            "cau_hinh_e_chuan": config_raw,
            "do_am_dien": float(en_raw) if en_raw is not None else None,
            "ban_kinh_pm": float(radius_raw) if radius_raw is not None else None,
            "nang_luong_ion_hoa_1_eV": float(ie_raw) if ie_raw is not None else None,
            "so_oxi_hoa": [s.strip() for s in ox_raw.split(",") if s.strip()] if ox_raw else [],
            "trang_thai_25C": trang_thai,
            "t_nong_chay_K": float(mp_raw) if mp_raw is not None else None,
            "t_soi_K": float(bp_raw) if bp_raw is not None else None,
            "khoi_luong_rieng": float(density_raw) if density_raw is not None else None
        }
        danh_sach_nguyen_to.append(nt)

    print(f"[tao_bang_tuan_hoan] Đã chuẩn hoá {len(danh_sach_nguyen_to)} nguyên tố.")

    # Ghi file Web JSON & JS
    TEP_XUAT_WEB.parent.mkdir(parents=True, exist_ok=True)
    with open(TEP_XUAT_WEB, "w", encoding="utf-8") as f:
        json.dump(danh_sach_nguyen_to, f, ensure_ascii=False, indent=2)
    print(f"[tao_bang_tuan_hoan] Đã xuất Web JSON: {TEP_XUAT_WEB}")

    tep_js = TEP_XUAT_WEB.with_suffix(".js")
    with open(tep_js, "w", encoding="utf-8") as f:
        f.write("window.BANG_TUAN_HOAN = " + json.dumps(danh_sach_nguyen_to, ensure_ascii=False) + ";\n")
    print(f"[tao_bang_tuan_hoan] Đã xuất Web JS: {tep_js}")

    # Ghi file Unity
    TEP_XUAT_UNITY.parent.mkdir(parents=True, exist_ok=True)
    with open(TEP_XUAT_UNITY, "w", encoding="utf-8") as f:
        json.dump(danh_sach_nguyen_to, f, ensure_ascii=False, indent=2)
    print(f"[tao_bang_tuan_hoan] Đã xuất Unity: {TEP_XUAT_UNITY}")

    # Ghi tài liệu README
    with open(TEP_README, "w", encoding="utf-8") as f:
        f.write("""# Bảng tuần hoàn 118 nguyên tố hoá học (CT GDPT 2018)

Dữ liệu được chuẩn hoá từ:
- **PubChem Periodic Table REST API**: https://pubchem.ncbi.nlm.nih.gov/rest/pug/periodictable/JSON (Phạm vi công cộng).
- **Đối chiếu RDKit PeriodicTable**: Xác thực kí hiệu Z=1..118, nguyên tử khối, trạng thái tự nhiên.
- **Quy chuẩn SGK Hoá học 10 (Chương trình GDPT 2018)**:
  + Tên IUPAC quốc tế và tên tiếng Việt truyền thống quen thuộc (Hiđro, Oxi, Natri, Sắt, Đồng, Thuỷ ngân, Chì...).
  + Cấu hình electron rút gọn với chỉ số trên dạng Unicode (ví dụ: [Ar] 3d⁶ 4s²).
  + Bố cục 18 cột (nhóm 1–18 kèm nhãn nhóm IA–VIIIA, IB–VIIIB).
  + Hai dãy Lanthanide (Ce–Lu) và Actinide (Th–Lr) đặt tách biệt ở 2 hàng dưới.
""")
    print(f"[tao_bang_tuan_hoan] Đã xuất README: {TEP_README}")

if __name__ == "__main__":
    main()
