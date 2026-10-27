"""Chuyển danh mục hóa chất và thiết bị (CSV) thành dữ liệu cho phòng thí nghiệm ảo.

    python tools/chuyen_hoa_chat.py <hoa_chat.csv> <thiet_bi.csv>

Ghi ra:
    data/chem/hoa_chat.json, data/chem/thiet_bi.json             (bản gốc, kiểm thử Python đọc)
    Phong thi nghiem ao/Assets/Lab/Data/*.json                   (Unity đọc bằng JsonUtility)

Với mỗi hóa chất, script suy ra:
  - tên có dấu, công thức, nhóm bảo quản (tủ axit, tủ bazơ, tủ chất dễ cháy, kệ muối theo gốc axit...)
  - vị trí: kệ, tầng, ô (để tìm kiếm trong phòng hóa chất)
  - thành phần khi hòa tan (ion hoặc phân tử, tính theo 1 mol chất) dùng mã loài của bộ máy hóa học
    Unity (Assets/Scripts/Simulation/Chem/Species.cs). Kiểm thử đối chiếu mã loài với file C# đó.
  - khối lượng mol, nồng độ gợi ý (mol/l), nhãn tính chất cho phản ứng hữu cơ, nhãn nguy hiểm.
"""

from __future__ import annotations

import csv
import json
import math
import re
import sys
import unicodedata
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
UNITY_DATA = ROOT / "Phong thi nghiem ao" / "Assets" / "Lab" / "Data"

# ---------------------------------------------------------------------------
# Đọc CSV (vài dòng có dấu phẩy trong tên, ví dụ "Chi(II,IV) oxit", "Butadien-1,3")
# ---------------------------------------------------------------------------


def read_rows(path: Path, n_cols: int, name_col: int = 1) -> list[list[str]]:
    rows = []
    with path.open(encoding="utf-8-sig") as f:
        lines = [ln.rstrip("\n\r") for ln in f if ln.strip()]
    for ln in lines[1:]:
        parts = next(csv.reader([ln]))
        if len(parts) > n_cols:  # dấu phẩy thừa nằm trong tên
            extra = len(parts) - n_cols
            parts = parts[:name_col] + [",".join(parts[name_col:name_col + extra + 1])] + parts[name_col + extra + 1:]
        rows.append([p.strip() for p in parts])
    return rows


# ---------------------------------------------------------------------------
# Thêm dấu tiếng Việt cho tên (CSV viết không dấu)
# ---------------------------------------------------------------------------

PHRASES = [
    ("Cacbon than chi", "Cacbon (than chì)"), ("Cacbon kim cuong", "Cacbon (kim cương)"),
    ("Kem danh rang", "Kem đánh răng"), ("Nuoc oxy gia", "Nước oxi già"), ("Nuoc nang", "Nước nặng"),
    ("Nuoc cat", "Nước cất"), ("Nuoc deion", "Nước khử ion"), ("Nuoc bien nhan tao", "Nước biển nhân tạo"),
    ("Nuoc muoi sinh ly", "Nước muối sinh lý"), ("Nuoc Javen", "Nước Gia-ven"), ("Nuoc brom", "Nước brom"),
    ("Nuoc clo", "Nước clo"), ("Luu huynh", "Lưu huỳnh"), ("Thuy ngan", "Thủy ngân"), ("Photpho do", "Photpho đỏ"),
    ("Photpho trang", "Photpho trắng"), ("Giay quy tim", "Giấy quỳ tím"), ("Giay do pH", "Giấy đo pH"),
    ("Giay loc", "Giấy lọc"), ("Giay cobalt clorua", "Giấy coban clorua"), ("Giay chi thi am", "Giấy chỉ thị ẩm"),
    ("metyl da cam", "metyl da cam"), ("metyl do", "metyl đỏ"), ("alizarin vang", "alizarin vàng"),
    ("Thuoc thu", "Thuốc thử"), ("Long trang trung", "Lòng trắng trứng"), ("Mo dong vat", "Mỡ động vật"),
    ("Dau an", "Dầu ăn"), ("Dau hoa", "Dầu hỏa"), ("Ete dau hoa", "Ete dầu hỏa"), ("Dau diesel", "Dầu diesel"),
    ("Dau goi", "Dầu gội"), ("Cao su tu nhien", "Cao su tự nhiên"), ("Keo dan", "Keo dán"), ("Muc in", "Mực in"),
    ("Than hoat tinh", "Than hoạt tính"), ("Bong thuy tinh", "Bông thủy tinh"), ("Da bot", "Đá bọt"),
    ("Giam an", "Giấm ăn"), ("Con 70", "Cồn 70°"), ("Con 90", "Cồn 90°"), ("Thuoc tim", "Thuốc tím"),
    ("Muoi iot", "Muối iot"), ("Phan dam ure", "Phân đạm urê"), ("Phan lan nung chay", "Phân lân nung chảy"),
    ("Phan kali", "Phân kali"), ("Superphotphat don", "Supephotphat đơn"), ("Superphotphat kep", "Supephotphat kép"),
    ("Thach cao", "Thạch cao"), ("Voi song", "Vôi sống"), ("Voi toi", "Vôi tôi"), ("Xi mang", "Xi măng"),
    ("Thuy tinh long", "Thủy tinh lỏng"), ("Thuoc nhuan trang", "Thuốc nhuận tràng"), ("Xa phong", "Xà phòng"),
    ("Chat giat bot", "Bột giặt"), ("Muoi Mohr", "Muối Mohr"), ("Phen chua", "Phèn chua"), ("Phen amoni", "Phèn amoni"),
    ("Phen crom", "Phèn crom"), ("ngam nuoc", "ngậm nước"), ("Sat tu oxit", "Sắt từ oxit"), ("Tinh bot", "Tinh bột"),
    ("hồ tinh bột", "hồ tinh bột"), ("Axit sunfuro", "Axit sunfurơ"), ("Cobalt(II) clorua", "Coban(II) clorua"),
    ("Khi ", "Khí "), ("Dung dich", "Dung dịch"), ("Axit tactric", "Axit tactric"), ("Nuoc", "Nước"),
]
WORDS = {
    "Nhom": "Nhôm", "Sat": "Sắt", "Kem": "Kẽm", "Dong": "Đồng", "Bac": "Bạc", "Vang": "Vàng", "Chi": "Chì",
    "Thiec": "Thiếc", "Nito": "Nitơ", "Ure": "Urê", "ran": "rắn", "khan": "khan", "vien": "viên", "Muoi": "Muối",
    "Chat": "Chất", "Andehit": "Anđehit",
}


def add_diacritics(name: str) -> str:
    s = name
    for a, b in PHRASES:
        s = s.replace(a, b)
    out = []
    for tok in re.split(r"(\W+)", s):
        base = tok
        if base in WORDS:
            tok = WORDS[base]
        else:
            m = re.match(r"^(Chi|Sat|Dong|Bac|Thiec|Nhom|Kem)(\(.*)?$", base)
            if m and m.group(1) in WORDS:
                tok = WORDS[m.group(1)] + (m.group(2) or "")
        out.append(tok)
    s = "".join(out)
    s = re.sub(r"(?i)hidr", lambda m: "Hiđr" if m.group(0)[0] == "H" else "hiđr", s)
    s = re.sub(r"andehit", "anđehit", s)
    s = re.sub(r"(?<![A-Za-zÀ-ỹ])([Dd])i(?=[a-zđ])(?!esel)", lambda m: ("Đ" if m.group(1) == "D" else "đ") + "i", s)
    s = re.sub(r"ozo\b", "ozơ", s)
    s = s.replace("Nitơ oxit", "Nitơ oxit").replace("Đinito", "Đinitơ")
    return s


CONC_PHRASES = [
    ("nuoc voi trong bao hoa", "nước vôi trong bão hòa"), ("bao hoa", "bão hòa"), ("ket tua", "kết tủa"),
    ("sinh ly", "sinh lý"), ("tuong duong", "tương đương"), ("thuy tinh long", "thủy tinh lỏng"),
    ("dau chuoi", "dầu chuối"), ("(bang)", "(băng)"), ("giam an", "giấm ăn"), ("dung dich", "dung dịch"),
    ("pha moi", "pha mới"), ("khu mau", "khử màu"), ("thu pH", "thử pH"), ("ho tinh bot", "hồ tinh bột"),
    ("70% bot", "70% bột"), ("hoat dong", "hoạt động"), ("(vien)", "(viên)"), ("nuoc Javen", "nước Gia-ven"),
    ("trong", "trong"),
]


def conc_text(s: str) -> str:
    for a, b in CONC_PHRASES:
        s = s.replace(a, b)
    return s


def strip_accents(s: str) -> str:
    s = s.replace("đ", "d").replace("Đ", "D")
    return "".join(c for c in unicodedata.normalize("NFD", s) if unicodedata.category(c) != "Mn")


# ---------------------------------------------------------------------------
# Công thức: phân tích, khối lượng mol
# ---------------------------------------------------------------------------

ATOMIC = {
    "H": 1.008, "D": 2.014, "He": 4.003, "Li": 6.94, "Be": 9.012, "B": 10.81, "C": 12.011, "N": 14.007, "O": 15.999,
    "F": 18.998, "Na": 22.99, "Mg": 24.305, "Al": 26.982, "Si": 28.085, "P": 30.974, "S": 32.06, "Cl": 35.45,
    "K": 39.098, "Ca": 40.078, "Ti": 47.867, "V": 50.942, "Cr": 51.996, "Mn": 54.938, "Fe": 55.845, "Co": 58.933,
    "Ni": 58.693, "Cu": 63.546, "Zn": 65.38, "As": 74.922, "Se": 78.971, "Br": 79.904, "Sr": 87.62, "Zr": 91.224,
    "Mo": 95.95, "Ag": 107.868, "Cd": 112.414, "Sn": 118.71, "Sb": 121.76, "Te": 127.6, "I": 126.904, "Ba": 137.327,
    "W": 183.84, "Au": 196.967, "Hg": 200.592, "Pb": 207.2, "Bi": 208.98,
}


def _clean_formula(f: str) -> str:
    f = f.replace("[", "(").replace("]", ")").replace("o-", "")
    f = re.sub(r"\.x?H2O", "", f) if ".xH2O" in f else f
    return f


def element_counts(formula: str) -> dict[str, float] | None:
    """Đếm nguyên tố, hiểu ngoặc và nước kết tinh ("CuSO4.5H2O"). None nếu không phải công thức."""
    f = _clean_formula(formula)
    if not f or f == "-" or "n" in re.sub(r"[A-Z][a-z]?", "", f) or "=" in f and False:
        return None
    f = f.replace("=", "").replace("-", "")
    total: dict[str, float] = {}
    for part in f.split("."):
        m = re.match(r"^(\d+)(.*)$", part)
        mult = int(m.group(1)) if m else 1
        body = m.group(2) if m else part
        counts = _parse_group(body)
        if counts is None:
            return None
        for k, v in counts.items():
            total[k] = total.get(k, 0) + v * mult
    return total


def _parse_group(s: str) -> dict[str, float] | None:
    stack: list[dict[str, float]] = [{}]
    i = 0
    while i < len(s):
        c = s[i]
        if c == "(":
            stack.append({})
            i += 1
        elif c == ")":
            i += 1
            m = re.match(r"\d+", s[i:])
            k = int(m.group(0)) if m else 1
            i += len(m.group(0)) if m else 0
            top = stack.pop()
            for e, v in top.items():
                stack[-1][e] = stack[-1].get(e, 0) + v * k
        else:
            m = re.match(r"([A-Z][a-z]?)(\d*)", s[i:])
            if not m or m.group(1) not in ATOMIC:
                return None
            stack[-1][m.group(1)] = stack[-1].get(m.group(1), 0) + (int(m.group(2)) if m.group(2) else 1)
            i += len(m.group(0))
    return stack[0] if len(stack) == 1 else None


def molar_mass(formula: str) -> float | None:
    c = element_counts(formula)
    if not c:
        return None
    return round(sum(ATOMIC[e] * n for e, n in c.items()), 3)


# ---------------------------------------------------------------------------
# Thành phần khi hòa tan: ion / phân tử (mã loài của Species.cs)
# ---------------------------------------------------------------------------

# Cation kim loại: (kí hiệu) -> {điện tích: mã loài}
CATIONS = {
    "Li": {1: "Li+"}, "Na": {1: "Na+"}, "K": {1: "K+"}, "Mg": {2: "Mg2+"}, "Ca": {2: "Ca2+"}, "Sr": {2: "Sr2+"},
    "Ba": {2: "Ba2+"}, "Al": {3: "Al3+"}, "Fe": {2: "Fe2+", 3: "Fe3+"}, "Cu": {2: "Cu2+"}, "Zn": {2: "Zn2+"},
    "Pb": {2: "Pb2+"}, "Ag": {1: "Ag+"}, "Hg": {2: "Hg2+"}, "Ni": {2: "Ni2+"}, "Co": {2: "Co2+"}, "Mn": {2: "Mn2+"},
    "Cr": {3: "Cr3+"}, "Sn": {2: "Sn2+", 4: "Sn4+"}, "Cd": {2: "Cd2+"}, "Be": {2: "Be2+"}, "Bi": {3: "Bi3+"},
    "Ti": {4: "Ti4+"},
}
# Gốc axit vô cơ: chuỗi công thức -> (mã loài, điện tích của gốc khi tạo muối)
ANIONS = {
    "Cl": ("Cl-", 1), "Br": ("Br-", 1), "I": ("I-", 1), "F": ("F", 1), "NO3": ("NO3-", 1), "NO2": ("NO2", 1),
    "ClO4": ("ClO4-", 1), "ClO3": ("ClO3-", 1), "ClO": ("ClO", 1), "OCl": ("ClO", 1), "BrO3": ("BrO3-", 1),
    "IO3": ("IO3-", 1), "SCN": ("SCN-", 1), "CN": ("CN", 1), "MnO4": ("MnO4-", 1), "OH": (None, 1),
    "SO4": ("SO4", 2), "HSO4": ("SO4", 1), "SO3": ("SO3", 2), "HSO3": ("SO3", 1), "S2O3": ("S2O32-", 2),
    "S2O5": ("SO3", 2), "S2O8": ("S2O82-", 2), "S": ("S", 2), "HS": ("S", 1), "CO3": ("CO3", 2), "HCO3": ("CO3", 1),
    "PO4": ("PO4", 3), "HPO4": ("PO4", 2), "H2PO4": ("PO4", 1), "P2O7": ("P2O74-", 4), "P3O10": ("P3O105-", 5),
    "SiO3": ("SiO3", 2), "B4O7": ("B", 2), "CrO4": ("CrVI", 2), "Cr2O7": ("CrVI", 2), "SiF6": ("SiF62-", 2),
}
# Số loài gốc ứng với 1 gốc trong công thức (Cr2O7 -> 2 CrVI, S2O5 -> 2 SO3, B4O7 -> 4 B)
ANION_MULT = {"Cr2O7": 2, "S2O5": 2, "B4O7": 4}

# Hợp chất cần chỉ định tay (thành phần hoặc chất rắn đặc biệt)
OVERRIDE: dict[str, list[tuple[str, float]]] = {
    "Hg2Cl2": [("Hg2Cl2", 1)], "CuCl": [("CuCl(s)", 1)], "CuI": [("CuI(s)", 1)], "FeS2": [("FeS2(s)", 1)],
    "Na3AlF6": [("Na3AlF6(s)", 1)], "CaC2": [("CaC2(s)", 1)], "Al4C3": [("Al4C3(s)", 1)], "Mg3N2": [("Mg3N2(s)", 1)],
    "Ca3N2": [("Ca3N2(s)", 1)], "NaNH2": [("NaNH2(s)", 1)], "NaH": [("NaH(s)", 1)], "LiAlH4": [("LiAlH4(s)", 1)],
    "NaBH4": [("NaBH4(s)", 1)], "Na2O2": [("Na2O2(s)", 1)], "K2O2": [("K2O2(s)", 1)], "BaO2": [("BaO2(s)", 1)],
    "K4[Fe(CN)6]": [("K+", 4), ("FeCN64-", 1)], "K3[Fe(CN)6]": [("K+", 3), ("FeCN63-", 1)],
    "K3[Co(CN)6]": [("K+", 3), ("CoCN63-", 1)], "K2MnO4": [("K+", 2), ("MnO42-", 1)],
    "NaKC4H4O6": [("Na+", 1), ("K+", 1), ("Tart", 1)], "CaS": [("Ca2+", 1), ("S", 1)],
    "Na2SiF6": [("Na+", 2), ("SiF62-", 1)], "AgOH": [("Ag+", 1)], "NH4OH": [("NH3", 1)], "NH3": [("NH3", 1)],
    "H2O2": [("H2O2", 1)], "CaSO4.2H2O": [("CaSO4", 1)], "SiO2.xH2O": [("SiO2gel(s)", 1)],
    "HgSO4": [("Hg2+", 1), ("SO4", 1)], "Ag2SO4": [("Ag+", 2), ("SO4", 1)], "(NH2)2CO": [("ure", 1)],
}

CATEGORY = {
    "Don chat": "Đơn chất", "Kim loai": "Kim loại", "Axit vo co": "Axit vô cơ", "Muoi": "Muối",
    "Muoi huu co": "Muối hữu cơ", "Chi thi": "Chất chỉ thị", "Thuoc thu": "Thuốc thử", "Hidrocacbon": "Hiđrocacbon",
    "Andehit": "Anđehit", "Axit huu co": "Axit hữu cơ", "Cacbohidrat": "Cacbohiđrat", "Polyme": "Polime",
    "Dung moi": "Dung môi", "Khi": "Khí", "Ran": "Chất rắn", "Dung dich": "Dung dịch",
    "Muoi oxy hoa": "Muối có tính oxi hóa", "Hon hop": "Hỗn hợp", "Axit beo": "Axit béo", "Chat beo": "Chất béo",
    "Muoi tinh the": "Muối kết tinh", "Vat lieu": "Vật liệu", "Muoi kép": "Muối kép", "Chat khu": "Chất khử",
    "Oxit trung tinh": "Oxit trung tính", "Phuc chat": "Phức chất", "Chất oxy hoa": "Chất oxi hóa",
    "Chat hut am": "Chất hút ẩm", "Dan xuat": "Dẫn xuất của xenlulozơ", "Chat chi thi am": "Chất chỉ thị ẩm",
    "Chat hut nuoc": "Chất hút nước", "Chat hap phu": "Chất hấp phụ", "Oxit hon hop": "Oxit hỗn tạp",
    "Hop chat N": "Hợp chất chứa nitơ",
}

ORGANIC_SALTS = {  # muối hữu cơ: công thức -> thành phần
    "CH3COONa": [("Na+", 1), ("Ac", 1)], "CH3COOK": [("K+", 1), ("Ac", 1)], "CH3COONH4": [("NH3", 1), ("Ac", 1)],
    "(CH3COO)2Ca": [("Ca2+", 1), ("Ac", 2)], "(CH3COO)2Mg": [("Mg2+", 1), ("Ac", 2)],
    "(CH3COO)2Ba": [("Ba2+", 1), ("Ac", 2)], "(CH3COO)2Pb": [("Pb2+", 1), ("Ac", 2)],
    "(CH3COO)2Cu": [("Cu2+", 1), ("Ac", 2)], "(CH3COO)2Zn": [("Zn2+", 1), ("Ac", 2)],
    "CH3COOAg": [("Ag+", 1), ("Ac", 1)], "HCOONa": [("Na+", 1), ("Fo", 1)], "HCOONH4": [("NH3", 1), ("Fo", 1)],
    "(COONa)2": [("Na+", 2), ("Ox", 1)], "(COOK)2": [("K+", 2), ("Ox", 1)], "(COONH4)2": [("NH3", 2), ("Ox", 1)],
    "C6H5COONa": [("Na+", 1), ("Bz", 1)], "C6H5COOK": [("K+", 1), ("Bz", 1)], "C6H5COONH4": [("NH3", 1), ("Bz", 1)],
    "CH3CH(OH)COONa": [("Na+", 1), ("Lac", 1)], "Na3C6H5O7": [("Na+", 3), ("Cit", 1)],
    "K3C6H5O7": [("K+", 3), ("Cit", 1)], "(NH4)3C6H5O7": [("NH3", 3), ("Cit", 1)], "Na2C6H6O7": [("Na+", 2), ("Cit", 1)],
    "(CH3COO)2Pb.3H2O": [("Pb2+", 1), ("Ac", 2)],
}

# Axit vô cơ: công thức -> thành phần (proton không ghi: pH tính từ bảo toàn điện tích)
ACIDS = {
    "HCl": [("Cl-", 1)], "HBr": [("Br-", 1)], "HI": [("I-", 1)], "HF": [("F", 1)], "H2SO4": [("SO4", 1)],
    "H2SO3": [("SO3", 1)], "HNO3": [("NO3-", 1)], "HNO2": [("NO2", 1)], "H3PO4": [("PO4", 1)], "H3PO3": [("PO3", 1)],
    "H3PO2": [("PO2", 1)], "H2CO3": [("CO3", 1)], "H2S": [("S", 1)], "HClO": [("ClO", 1)], "HClO2": [("ClO2", 1)],
    "HClO3": [("ClO3-", 1)], "HClO4": [("ClO4-", 1)], "HBrO3": [("BrO3-", 1)], "HIO3": [("IO3-", 1)],
    "H2SiO3": [("SiO3", 1)], "H3BO3": [("B", 1)], "H2CrO4": [("CrVI", 1)], "H2Cr2O7": [("CrVI", 2)],
    "HMnO4": [("MnO4-", 1)], "HCN": [("CN", 1)], "HSCN": [("SCN-", 1)],
}

# Oxit: công thức -> thành phần (oxit bazơ tan = cation; oxit axit tan = gốc axit; không tan = chất rắn)
OXIDES = {
    "Na2O": [("Na+", 2)], "K2O": [("K+", 2)], "Li2O": [("Li+", 2)], "CaO": [("Ca2+", 1)], "BaO": [("Ba2+", 1)],
    "SrO": [("Sr2+", 1)], "SO2": [("SO3", 1)], "SO3": [("SO4", 1)], "CO2": [("CO3", 1)], "P2O5": [("PO4", 2)],
    "P2O3": [("PO3", 2)], "N2O5": [("NO3-", 2)], "N2O3": [("NO2", 2)], "NO2": [("NO3-", 0.5), ("NO2", 0.5)],
    "Mn2O7": [("MnO4-", 2)], "CrO3": [("CrVI", 1)], "B2O3": [("B", 2)], "Cl2O": [("ClO", 2)],
    "Cl2O7": [("ClO4-", 2)], "I2O5": [("IO3-", 2)],
}
SOLID_OXIDES = {"MgO", "FeO", "Fe2O3", "Fe3O4", "CuO", "Cu2O", "ZnO", "PbO", "PbO2", "Pb3O4", "SnO", "SnO2", "MnO",
                "MnO2", "CrO", "Cr2O3", "NiO", "CoO", "TiO2", "SiO2", "Ag2O", "HgO", "CdO", "BeO", "ZrO2", "V2O5",
                "MoO3", "WO3", "Sb2O3", "Bi2O3", "SeO2", "TeO2", "As2O3", "As2O5", "Al2O3"}
GAS_INERT = {"H2", "O2", "N2", "F2", "CO", "NO", "N2O", "CH4", "C2H6", "C3H8", "C4H10", "C3H6"}

ELEMENT_SOLIDS = {"Na": "Na(s)", "K": "K(s)", "Li": "Li(s)", "Mg": "Mg(s)", "Al": "Al(s)", "Fe": "Fe(s)", "Zn": "Zn(s)",
                  "Cu": "Cu(s)", "Ag": "Ag(s)", "Au": "Au(s)", "Pb": "Pb(s)", "Sn": "Sn(s)", "Ni": "Ni(s)", "Co": "Co(s)",
                  "Mn": "Mn(s)", "Cr": "Cr(s)", "Ca": "Ca(s)", "Ba": "Ba(s)", "Hg": "Hg(l)", "Ti": "Ti(s)", "W": "W(s)",
                  "S": "S(s)", "C": "C(s)", "B": "B(s)", "Si": "Si(s)", "As": "As(s)", "I2": "I2", "Br2": "Br2",
                  "Cl2": "Cl2"}

INDICATORS = {
    "phenolphtalein": "pp", "metyl da cam": "mo", "metyl do": "mr", "bromothymol": "btb", "bromocresol": "bcg",
    "thymol xanh": "tb", "alizarin": "ay", "tinh bot": "starch",
}
REAGENTS = {  # thuốc thử pha sẵn: tên (không dấu, chữ thường) -> (thành phần trên 1 lít, nồng độ để ghi "C" = 1)
    "fehling a": [("Cu2+", 0.28), ("SO4", 0.28)],
    "fehling b": [("Na+", 3.2), ("K+", 0.7), ("Tart", 0.7)],
    "tollens": [("Ag+", 0.1), ("NO3-", 0.1), ("NH3", 0.6)],
    "biuret": [("Na+", 1.0), ("Cu2+", 0.01), ("SO4", 0.01)],
    "ninhydrin": [("ninhydrin", 0.006)],
    "lugol": [("I2", 0.05), ("K+", 0.3), ("I-", 0.3)],
    "benedict": [("Cu2+", 0.07), ("SO4", 0.07), ("Na+", 1.6), ("Cit", 0.6), ("CO3", 0.5)],
    "millon": [("Hg2+", 0.5), ("NO3-", 2.5)],
    "molisch": [("naphtol", 0.35)],
    "schiff": [("schiff", 0.003)],
    "nessler": [("nessler", 0.09), ("K+", 2.5)],
    "nuoc brom": [("Br2", 0.2)],
    "nuoc clo": [("Cl2", 0.1)],
    "dung dich iot (i2/ki)": [("I2", 0.05), ("K+", 0.3), ("I-", 0.3)],
    "diphenylamin": [("diphenylamin", 0.06), ("SO4", 18)],
    "axit picric": [("picric", 0.044)],
}
HOUSEHOLD = {  # tên (không dấu) -> (thành phần trên 1 lít hoặc 1 mol rắn, trạng thái, khối lượng mol)
    "giam an": ([("Ac", 0.83)], "dd"), "nuoc muoi sinh ly": ([("Na+", 0.154), ("Cl-", 0.154)], "dd"),
    "nuoc bien nhan tao": ([("Na+", 0.6), ("Cl-", 0.6)], "dd"), "thuoc tim": ([("K+", 0.0063), ("MnO4-", 0.0063)], "dd"),
    "nuoc javen": ([("Na+", 0.7), ("ClO", 0.7), ("Cl-", 0.7)], "dd"), "baking soda": ([("Na+", 1), ("CO3", 1)], "ran"),
    "muoi iot": ([("Na+", 1), ("Cl-", 1)], "ran"), "phan dam ure": ([("ure", 1)], "ran"),
    "phan lan nung chay": ([("Ca3(PO4)2", 1)], "ran"), "phan kali": ([("K+", 1), ("Cl-", 1)], "ran"),
    "superphotphat don": ([("Ca2+", 1), ("PO4", 2)], "ran"), "superphotphat kep": ([("Ca2+", 1), ("PO4", 2)], "ran"),
    "thuy tinh long": ([("Na+", 6), ("SiO3", 3)], "dd"), "xa phong": ([("xaphong", 1)], "ran"),
    "chat giat bot": ([("xaphong", 1)], "ran"), "kem danh rang": ([("CaCO3", 1)], "ran"),
    "cloramin b": ([("Na+", 1), ("ClO", 1)], "ran"), "nuoc cat": ([], "long"), "nuoc deion": ([], "long"),
    "xi mang": ([("Ca2+", 1), ("SiO2(s)", 1)], "ran"),
}
HOUSEHOLD_M = {"baking soda": 84.007, "muoi iot": 58.44, "phan dam ure": 60.06, "phan lan nung chay": 310.18,
               "phan kali": 74.55, "superphotphat don": 234.05, "superphotphat kep": 234.05, "xa phong": 306.5,
               "chat giat bot": 348.5, "kem danh rang": 100.09, "cloramin b": 213.6, "xi mang": 172.2}

# Axit hữu cơ: mã gốc dùng chung với muối (Ac, Fo, Ox...) hoặc Ka để Unity tạo loài axit yếu riêng
ORGANIC_ACID_SPECIES = {"CH3COOH": "Ac", "HCOOH": "Fo", "(COOH)2": "Ox", "(COOH)2.2H2O": "Ox", "C6H8O7": "Cit",
                        "C4H6O6": "Tart", "C6H5COOH": "Bz", "CH3CH(OH)COOH": "Lac"}
ORGANIC_KA = {"C2H5COOH": 1.3e-5, "C3H7COOH": 1.5e-5, "C4H9COOH": 1.4e-5, "HOOCCH2COOH": 1.5e-3,
              "HOOC(CH2)2COOH": 6.2e-5, "CH2=CHCOOH": 5.6e-5, "HOOCCH2CH(OH)COOH": 4e-4, "C6H4(COOH)2": 1.1e-3,
              "C6H4(OH)COOH": 1.1e-3, "C5H4N4O3": 1.3e-6, "C15H31COOH": 1e-5, "C17H35COOH": 1e-5,
              "C17H33COOH": 1e-5, "C17H31COOH": 1e-5, "C11H23COOH": 1e-5, "HOOC(CH2)2CH(NH2)COOH": 4.5e-5,
              "C4H7NO4": 1e-4}
AMINE_KB = {"CH3NH2": 4.4e-4, "C2H5NH2": 4.5e-4, "C3H7NH2": 4.7e-4, "(CH3)2NH": 5.4e-4, "(CH3)3N": 6.3e-5,
            "C6H5NH2": 4.2e-10, "C6H5NHCH3": 7e-10, "NH2CH2CH2NH2": 8.5e-5, "NH2(CH2)6NH2": 5e-4, "C5H5N": 1.7e-9,
            "(C2H5)3N": 5.6e-4}
DENSITY = {"Br2": 3.1, "Hg": 13.53, "H2O": 1.0, "D2O": 1.107, "SnCl4": 2.23, "TiCl4": 1.73, "Mn2O7": 2.4, "SO3": 1.92,
           "Cl2O7": 1.9, "C5H12": 0.626, "C6H14": 0.655, "C7H16": 0.684, "C8H18": 0.703, "C5H8": 0.681, "C6H6": 0.876,
           "C6H5CH3": 0.867, "C6H4(CH3)2": 0.86, "C6H5C2H5": 0.867, "C6H5CH=CH2": 0.906, "C6H5CH(CH3)2": 0.862,
           "C6H12": 0.779, "C7H14": 0.77, "CH2Cl2": 1.33, "CHCl3": 1.49, "CCl4": 1.59, "C2H4Cl2": 1.25,
           "C6H5Cl": 1.11, "C6H4Cl2": 1.3, "C6H5Br": 1.5, "CH3I": 2.28, "C2H4Br2": 2.18, "CH2=CHCH2Cl": 0.94,
           "C2H5Br": 1.46, "C2H5I": 1.95, "C6H5CH2Cl": 1.1, "CH3OH": 0.792, "C2H5OH": 0.789, "C3H7OH": 0.803,
           "(CH3)2CHOH": 0.786, "C4H9OH": 0.81, "(CH3)2CHCH2OH": 0.802, "(CH3)3COH": 0.775, "C5H11OH": 0.811,
           "HOCH2CH2OH": 1.11, "CH3CH(OH)CH2OH": 1.036, "C3H5(OH)3": 1.26, "C4H10O2": 1.0, "CH3C6H4OH": 1.03,
           "C6H5CH2OH": 1.04, "C6H11OH": 0.96, "CH2=CHCH2OH": 0.85, "CH3CHO": 0.784, "C2H5CHO": 0.81,
           "C3H7CHO": 0.8, "CH2=CHCHO": 0.84, "C6H5CHO": 1.04, "OHC(CH2)3CHO": 1.06, "(CH3)2CO": 0.784,
           "C2H5COCH3": 0.805, "C3H7COCH3": 0.81, "C6H10O": 0.95, "C6H5COCH3": 1.03, "HCOOH": 1.22,
           "CH3COOH": 1.05, "C2H5COOH": 0.99, "C3H7COOH": 0.96, "C4H9COOH": 0.94, "CH2=CHCOOH": 1.05,
           "CH3CH(OH)COOH": 1.21, "C17H33COOH": 0.895, "C17H31COOH": 0.9, "C6H5NH2": 1.02, "C6H5NHCH3": 0.99,
           "NH2CH2CH2NH2": 0.9, "C5H5N": 0.98, "(C2H5)3N": 0.726, "C3H7NH2": 0.72, "C2H5OC2H5": 0.713,
           "(CH3)2SO": 1.1, "HCON(CH3)2": 0.944, "C4H8O": 0.889, "C4H8O2": 1.03, "CH3CN": 0.786, "CS2": 1.26,
           "C6H5NO2": 1.2, "(C17H33COO)3C3H5": 0.91}
LIGHT_SENSITIVE = {"AgNO3", "KMnO4", "H2O2", "HNO3", "I2", "Br2", "KI", "AgCl", "AgBr", "AgI", "CHCl3", "Na2S2O3"}

# ---------------------------------------------------------------------------
# Nhóm bảo quản (thứ tự = thứ tự kệ trong phòng hóa chất)
# ---------------------------------------------------------------------------

GROUPS = [
    # mã, tên nhóm, kiểu tủ, màu nhãn
    ("AX", "Axit vô cơ", "tu_axit", "#E5484D"),
    ("BZ", "Bazơ và kiềm", "tu_bazo", "#3E8BFF"),
    ("MOX", "Chất oxi hóa", "tu_oxh", "#F2994A"),
    ("MCL", "Muối halogenua (clorua, bromua, iotua, florua)", "ke", "#2BB673"),
    ("MSO", "Muối sunfat, sunfit, sunfua, thiosunfat", "ke", "#2BB673"),
    ("MNO", "Muối nitrat, nitrit", "ke", "#2BB673"),
    ("MCO", "Muối cacbonat, silicat, borat", "ke", "#2BB673"),
    ("MPO", "Muối photphat", "ke", "#2BB673"),
    ("MKH", "Muối khác, phức chất, muối kép", "ke", "#2BB673"),
    ("MHC", "Muối hữu cơ", "ke", "#26A69A"),
    ("OX", "Oxit", "ke", "#8D6E63"),
    ("KL", "Kim loại", "ke", "#90A4AE"),
    ("PK", "Phi kim, halogen", "tu_oxh", "#B39DDB"),
    ("CT", "Chất chỉ thị", "tu_kinh", "#EC4899"),
    ("TT", "Thuốc thử", "tu_kinh", "#A855F7"),
    ("DD", "Dung dịch pha sẵn", "ke", "#06B6D4"),
    ("HC", "Hiđrocacbon, dẫn xuất halogen", "tu_chay", "#F5C518"),
    ("AN", "Ancol, phenol, anđehit, xeton", "tu_chay", "#F5C518"),
    ("ES", "Este, chất béo, dung môi", "tu_chay", "#F5C518"),
    ("HH", "Axit hữu cơ, amin, amino axit", "ke", "#7E57C2"),
    ("CB", "Cacbohiđrat, protein", "ke", "#7E57C2"),
    ("PL", "Polime, vật liệu, chất hút ẩm", "ke", "#78909C"),
    ("GD", "Hóa chất đời sống, phân bón", "ke", "#66BB6A"),
    ("KH", "Khí (bình khí nhỏ)", "khi", "#94A3B8"),
]
UNIT_SIZE = {"ke": (6, 9), "tu_axit": (6, 9), "tu_bazo": (6, 9), "tu_oxh": (6, 9), "tu_chay": (6, 9),
             "tu_kinh": (6, 9), "khi": (4, 8)}  # (số tầng, số ô mỗi tầng)

OXIDIZER_NAMES = ("pemanganat", "cromat", "dicromat", "clorat", "perclorat", "hipoclorit", "pesunfat", "peroxit",
                  "manganat")


def group_of(r: dict) -> str:
    cat, state, f, name = r["cat"], r["state"], r["formula"], r["ascii"]
    if state == "khi" or cat == "Khi":
        return "KH"
    if name.startswith("dung dich ") and r["stt"] >= 702:
        return "DD"
    if cat == "Axit vo co":
        return "DD" if "10%" in name else "AX"
    if cat in ("Bazơ", "Bazơ lưỡng tính", "Bazơ yếu"):
        return "DD" if "10%" in name else "BZ"
    if cat in ("Muoi oxy hoa", "Chất oxy hoa") or any(w in name for w in OXIDIZER_NAMES):
        return "MOX"
    if cat in ("Muoi", "Muoi tinh the", "Muoi kép", "Phuc chat", "Chat hut nuoc"):
        if cat in ("Muoi kép", "Phuc chat"):
            return "MKH"
        if re.search(r"(clorua|bromua|iodua|florua|criolit)", name):
            return "MCL"
        if re.search(r"(sunfat|sunfit|sunfua|thiosunfat|pyrosunfit|pyrit)", name):
            return "MSO"
        if re.search(r"(nitrat|nitrit)", name):
            return "MNO"
        if re.search(r"(cacbonat|silicat|borat|borax)", name):
            return "MCO"
        if "photphat" in name:
            return "MPO"
        return "MKH"
    if cat == "Muoi huu co":
        return "MHC"
    if cat.startswith("Oxit"):
        return "OX"
    if cat == "Kim loai":
        return "KL"
    if cat == "Don chat":
        return "PK"
    if cat in ("Chi thi", "Chat chi thi am"):
        return "CT"
    if cat == "Thuoc thu":
        return "TT"
    if cat in ("Hidrocacbon", "Dẫn xuất halogen"):
        return "HC"
    if cat in ("Ancol", "Phenol", "Andehit", "Xeton"):
        return "AN"
    if cat in ("Este", "Chat beo", "Dung moi"):
        return "ES"
    if cat in ("Axit huu co", "Axit beo", "Amin", "Amino axit", "Hop chat N"):
        return "DD" if "10%" in name or r["stt"] >= 702 else "HH"
    if cat in ("Cacbohidrat", "Protein", "Dan xuat"):
        return "CB"
    if cat in ("Polyme", "Vat lieu", "Chat hut am", "Chat hap phu"):
        return "PL"
    if cat == "Dung dich" and r["formula"] == "H2O2":
        return "MOX"
    return "GD"


# ---------------------------------------------------------------------------
# Trạng thái, nồng độ
# ---------------------------------------------------------------------------

STATE = {"Khi": "khi", "Long": "long", "Ran": "ran", "Dung dich": "dd", "Ket tua": "ket_tua", "Giay": "giay",
         "Huyền phù": "ket_tua", "Khi/Dung dich": "dd", "Dung dich keo": "dd"}


def parse_conc(text: str, M: float | None, state: str) -> tuple[float, list[float]]:
    """Nồng độ gợi ý (mol/l) từ chuỗi như "0.1M; 1M; 37% (~12M)" hoặc "5% (nuoc Javen)". Trả (mặc định, danh sách)."""
    vals: list[float] = []
    for part in text.split(";"):
        part = part.strip()
        m = re.search(r"~\s*([\d.]+)(?:-[\d.]+)?\s*M", part)
        if m:
            vals.append(float(m.group(1)))
            continue
        m = re.match(r"^([\d.]+)\s*M\b", part)
        if m:
            vals.append(float(m.group(1)))
            continue
        m = re.match(r"^([\d.]+)%", part)
        if m and M and state == "dd":
            pct = float(m.group(1))
            vals.append(round(pct * 10 / M, 4))  # % khối lượng/thể tích, coi khối lượng riêng ≈ 1
        m = re.search(r"\(([\d.]+)M\)", part)
        if m:
            vals.append(float(m.group(1)))
    if not vals:
        return (0.0, [])
    # Bàn học sinh dùng dung dịch loãng: ưu tiên 0,1 M hoặc 1 M nếu có
    pref = next((v for v in vals if abs(v - 0.1) < 1e-9), None) or next((v for v in vals if abs(v - 1) < 1e-9), None)
    return (pref if pref else vals[0], sorted(set(vals)))


# ---------------------------------------------------------------------------
# Thành phần
# ---------------------------------------------------------------------------

def split_salt(formula: str) -> list[tuple[str, float]] | None:
    """Muối vô cơ: tách cation (kim loại, NH4) và gốc axit theo bảo toàn điện tích."""
    f = re.sub(r"\.\d*H2O$", "", formula)
    f = re.sub(r"(?<!\()NH4", "(NH4)", f)
    tokens: list[tuple[str, int]] = []
    i = 0
    while i < len(f):
        if f[i] == "(":
            j = f.index(")", i)
            grp = f[i + 1:j]
            m = re.match(r"\d+", f[j + 1:])
            k = int(m.group(0)) if m else 1
            tokens.append((grp, k))
            i = j + 1 + (len(m.group(0)) if m else 0)
        else:
            m = re.match(r"([A-Z][a-z]?)(\d*)", f[i:])
            if not m:
                return None
            tokens.append((m.group(1), int(m.group(2)) if m.group(2) else 1))
            i += len(m.group(0))
    lead = 0
    while lead < len(tokens) and (tokens[lead][0] in CATIONS or tokens[lead][0] == "NH4"):
        lead += 1
    # KMnO4: Mn đứng sau K nhưng thuộc gốc MnO4, nên thử từ cách tách nhiều cation nhất trở xuống
    for cut in range(lead, 0, -1):
        rest = tokens[cut:]
        if not rest:
            continue
        if len(rest) == 1 and rest[0][0] in ANIONS:
            anion, a_count = rest[0]
        else:
            anion = "".join(t + (str(n) if n > 1 else "") for t, n in rest)
            a_count = 1
            if anion not in ANIONS:
                continue
        res = _assign(tokens[:cut], anion, a_count)
        if res:
            return res
    return None


def _assign(cations: list[tuple[str, int]], anion: str, a_count: int) -> list[tuple[str, float]] | None:
    sp, charge = ANIONS[anion]
    neg = charge * a_count
    out: list[tuple[str, float]] = []
    fixed = 0
    variable = None
    for sym, n in cations:
        if sym == "NH4":
            out.append(("NH3", n))
            fixed += n
        elif len(CATIONS[sym]) == 1:
            z = next(iter(CATIONS[sym]))
            out.append((CATIONS[sym][z], n))
            fixed += z * n
        else:
            variable = (sym, n)
    if variable:
        sym, n = variable
        z = (neg - fixed) / n
        if z not in CATIONS[sym]:
            return None
        out.append((CATIONS[sym][int(z)], n))
    elif fixed != neg:
        return None
    if sp:
        out.append((sp, a_count * ANION_MULT.get(anion, 1)))
    return out


def composition(r: dict) -> tuple[list[tuple[str, float]], str | None]:
    """(thành phần trên 1 mol hoặc 1 lít, ghi chú). Rỗng = chất trơ (chỉ là vật liệu, không phản ứng)."""
    f, cat, name = r["formula"], r["cat"], r["ascii"]
    for key, comp in REAGENTS.items():
        if name.startswith(("thuoc thu " + key, key)):
            return comp, "per_liter"
    for key, (comp, _) in HOUSEHOLD.items():
        if name == key:
            return comp, "per_liter" if HOUSEHOLD[key][1] == "dd" else None
    if name.startswith(("con 70", "con 90")):
        return [], "ethanol"
    for key, sp in INDICATORS.items():
        if key in name and cat == "Chi thi":
            return [(sp, 1)], "indicator"
    if f in ("H2O", "D2O"):
        return [], None
    if f in OVERRIDE:
        return OVERRIDE[f], None
    if f in ORGANIC_SALTS:
        return ORGANIC_SALTS[f], None
    if cat == "Axit vo co" or f in ACIDS:
        if f in ACIDS:
            return ACIDS[f], None
    if f in OXIDES and (cat.startswith("Oxit") or cat in ("Khi", "Ran", "Chat hut am")):
        return OXIDES[f], None
    if f in SOLID_OXIDES:
        return [(f + "(s)", 1)], None
    if f in ORGANIC_ACID_SPECIES:
        return [(ORGANIC_ACID_SPECIES[f], 1)], None
    if cat == "Khi" or r["state"] == "khi":
        if f in ("NH3",):
            return [("NH3", 1)], None
        if f == "H2S":
            return [("S", 1)], None
        if f in ("Cl2",):
            return [("Cl2", 1)], None
        if f in GAS_INERT:
            return [], "inert_gas"
    if f == "C" and "hoat tinh" in name:
        return [("Cact(s)", 1)], None
    if f in ELEMENT_SOLIDS:
        return [(ELEMENT_SOLIDS[f], 1)], None
    if f == "P":
        return [("Pw(s)" if "trang" in name else "P(s)", 1)], None
    if cat in ("Bazơ", "Bazơ lưỡng tính") and "OH" in f:
        m = re.match(r"^([A-Z][a-z]?)\(?OH\)?(\d*)$", f)
        if m and m.group(1) in CATIONS:
            z = int(m.group(2)) if m.group(2) else 1
            if z in CATIONS[m.group(1)]:
                return [(CATIONS[m.group(1)][z], 1)], None
    if cat in ("Muoi", "Muoi tinh the", "Muoi kép", "Chat hut nuoc", "Chat hut am", "Chat chi thi am", "Muoi oxy hoa",
               "Ran"):
        s = split_salt(f)
        if s:
            return s, None
        if f == "TiCl4":
            return [("Ti4+", 1), ("Cl-", 4)], None
        if f == "SnCl4":
            return [("Sn4+", 1), ("Cl-", 4)], None
    return [], None


# Nhãn tính chất cho phản ứng hữu cơ (bộ máy C# dùng): khong_no, ank_1_in, andehit, poliol, phenol, anilin,
# tinh_bot, protein, amino_axit, este, chat_beo, ancol, khong_tan, nang (nặng hơn nước), de_chay, cacbohidrat
def organic_tags(r: dict) -> list[str]:
    f, cat, name = r["formula"], r["cat"], r["ascii"]
    t: set[str] = set()
    if cat == "Hidrocacbon":
        t |= {"de_chay", "khong_tan"}
        if any(k in name for k in ("etilen", "propilen", "butilen", "butadien", "isopren", "styren")):
            t.add("khong_no")
        if any(k in name for k in ("axetilen", "propin")):
            t |= {"khong_no", "ank_1_in"}
        if any(k in name for k in ("toluen", "xilen", "etylbenzen", "cumen")):
            t.add("aren_ankyl")
    if cat == "Dẫn xuất halogen":
        t |= {"khong_tan"}
        if "vinyl" in name or "allyl" in name or "tetrafloetylen" in name:
            t.add("khong_no")
    if cat == "Ancol":
        t |= {"ancol", "de_chay"}
        if any(k in name for k in ("glicol", "glixerol")):
            t.add("poliol")
        if any(k in name for k in ("butan-1-ol", "butan-2-ol", "isobutanol", "pentan", "benzylic", "xiclohexanol")):
            t.add("khong_tan")
        if "allylic" in name:
            t.add("khong_no")
    if cat == "Phenol":
        t |= {"phenol"}
    if cat == "Andehit":
        t |= {"andehit", "de_chay"}
        if "acrolein" in name:
            t.add("khong_no")
        if "benzandehit" in name:
            t.add("khong_tan")
    if cat == "Xeton":
        t |= {"de_chay"}
        if any(k in name for k in ("axetophenon", "benzophenon", "xiclohexanon")):
            t.add("khong_tan")
    if cat == "Axit huu co" and ("formic" in name):
        t.add("andehit")
    if "acrylic" in name or "oleic" in name or "linoleic" in name:
        t.add("khong_no")
    if cat == "Axit beo":
        t.add("khong_tan")
    if cat == "Amino axit":
        t.add("amino_axit")
    if cat == "Amin":
        t.add("amin")
        if "anilin" in name:
            t |= {"anilin", "khong_tan"}
    if cat == "Este":
        t |= {"este", "de_chay", "khong_tan"}
        if "format" in name:
            t.add("andehit")
        if "acrylat" in name:
            t.add("khong_no")
    if cat == "Chat beo":
        t |= {"este", "chat_beo", "khong_tan"}
        if any(k in name for k in ("triolein", "dau an")):
            t.add("khong_no")
    if cat == "Cacbohidrat":
        t.add("cacbohidrat")
        if any(k in name for k in ("glucoz", "mantoz", "lactoz")):
            t |= {"andehit", "poliol"}
        if "fructoz" in name:
            t |= {"poliol", "andehit_kiem"}
        if "saccaroz" in name:
            t.add("poliol")
        if any(k in name for k in ("tinh bot", "glycogen", "dextrin")):
            t.add("tinh_bot")
        if any(k in name for k in ("xenluloz", "chitin")):
            t.add("khong_tan")
    if cat == "Protein":
        t.add("protein")
    if cat == "Polyme":
        t.add("khong_tan")
    if cat == "Dung moi" and r["formula"] not in ("H2O", "D2O") and name not in ("nuoc cat", "nuoc deion"):
        t.add("de_chay")
        if any(k in name for k in ("dau", "xang", "ete", "disunfua", "nitrobenzen")):
            t.add("khong_tan")
    if "khong_tan" in t and DENSITY.get(f, 0.9) > 1.05:
        t.add("nang")
    return sorted(t)


TOXIC = {"CO", "NO2", "NO", "N2O3", "SO2", "H2S", "Cl2", "F2", "Br2", "HCN", "CH3OH", "C6H6", "C6H5NH2", "C6H5NO2",
         "CHCl3", "CCl4", "CH2Cl2", "CS2", "HCHO", "CH3CHO", "CH2=CHCHO", "C6H5OH", "CH3C6H4OH", "C6H4(OH)2",
         "(COOH)2", "(COOH)2.2H2O", "CH3Cl", "CH3Br", "CH3I", "C2H3Cl", "C2H4Br2", "C2H4Cl2", "HF", "NaF", "KF",
         "NaNO2", "KNO2", "NH4NO2", "BaCl2", "Ba(NO3)2", "Ba(OH)2", "BaBr2", "BaI2", "(CH3COO)2Ba", "BaO", "Cl2O",
         "NH3", "SeO2", "TeO2", "Sb2O3", "V2O5", "C5H5N", "Mn2O7", "C2H5Cl"}
CORROSIVE = {"HCl", "HBr", "HI", "HF", "H2SO4", "HNO3", "HClO4", "H3PO4", "NaOH", "KOH", "LiOH", "Br2", "NH3",
             "CH3COOH", "HCOOH", "C6H5OH", "Na2O2", "K2O2", "CaO", "Na2O", "K2O", "P2O5", "SO3", "Cl2O7", "H2O2",
             "FeCl3", "AlCl3", "SnCl4", "TiCl4", "Na2S", "K2S", "NaClO", "Ca(OCl)2", "Na2SiO3", "Ba(OH)2", "CrO3"}
OXIDIZING = ("MnO4", "Cr2O7", "CrO4", "CrO3", "ClO3", "ClO4", "OCl", "ClO", "NO3", "S2O8", "O2", "H2O2", "BrO3", "IO3",
             "PbO2", "Mn2O7")
FLAMMABLE = {"H2", "CH4", "C2H6", "C3H8", "C4H10", "C2H4", "C3H6", "C4H8", "C2H2", "C3H4", "(CH3)2C=CH2",
             "CH2=CH-CH=CH2", "Na", "K", "Li", "Mg", "P", "S", "CO", "H2S", "NaH", "LiAlH4", "NaBH4", "CaC2", "Al4C3",
             "HCHO", "CH3Cl", "C2H5Cl", "C2H3Cl", "CH3NH2", "(CH3)2NH", "(CH3)3N"}


def hazards(r: dict, tags: list[str]) -> list[str]:
    f = r["formula"]
    name = r["ascii"]
    h = set()
    elems = element_counts(f) or {} if f else {}
    if f in TOXIC or any(e in elems for e in ("Hg", "Pb", "Cd", "Be", "As")) or re.search(r"Cr2?O[47]|CrO3|CN", f) \
            or "photpho trang" in name or "formalin" in r["conc"]:
        h.add("doc")
    if f in CORROSIVE or (r["cat"] == "Axit vo co" and any(x in r["conc"] for x in ("%", "6M"))):
        h.add("an_mon")
    if f and any(x in f for x in OXIDIZING) and r["cat"] not in ("Este", "Dan xuat"):
        h.add("oxi_hoa")
    if f in ("Cl2", "F2", "Br2", "HNO3", "Na2O2", "K2O2", "BaO2"):
        h.add("oxi_hoa")
    if "de_chay" in tags or f in FLAMMABLE:
        h.add("de_chay")
    if f in ("Na", "K", "Li", "Ca", "Ba", "CaC2", "NaH", "LiAlH4", "Na2O2", "K2O2", "Al4C3", "Mg3N2", "Ca3N2",
             "NaNH2"):
        h.add("phan_ung_nuoc")
    if f in ("KClO3", "NH4ClO4", "(NH4)2Cr2O7", "NH4NO3") or "trinitrat" in name:
        h.add("no")
    return sorted(h)


# Màu hiển thị (dung dịch hoặc bột) để dựng lọ trong kệ. Bộ máy C# tự tính màu thật khi phản ứng.
SOLID_COLOR = {
    "CuSO4.5H2O": "#2F7FD8", "CuSO4": "#EEEEEE", "CuO": "#1D1D1D", "Cu2O": "#B5372A", "Fe2O3": "#8E2E1C",
    "Fe3O4": "#222222", "FeO": "#2B2B2B", "MnO2": "#2A2420", "KMnO4": "#4A0E4E", "K2Cr2O7": "#E8731A",
    "Na2Cr2O7": "#E8731A", "(NH4)2Cr2O7": "#E8731A", "K2CrO4": "#F2C400", "Na2CrO4": "#F2C400", "CrO3": "#9E1B1B",
    "Cr2O3": "#3E7B3A", "S": "#F7E03C", "I2": "#3B2448", "P": "#9B2D2D", "C": "#222222", "PbO": "#E3B23C",
    "PbO2": "#3B2A20", "Pb3O4": "#D0461F", "HgO": "#E2731F", "Ag2O": "#3A2D26", "NiO": "#5E7D3B", "CoO": "#3B4A3B",
    "CoCl2": "#2D5DD8", "FeSO4.7H2O": "#A8D8B0", "FeCl3": "#8A4B1B", "NiSO4": "#3AA35C", "CoSO4": "#C2426A",
    "CuCl2": "#3A9AA8", "Cu": "#B87333", "Au": "#D4AF37", "Fe": "#7A7A7A", "Zn": "#A8B0B6", "Mg": "#C9CDD0",
    "Al": "#C8CCD0", "Ag": "#C0C0C0", "Pb": "#6B6E73", "Sn": "#B9BDC2", "Na": "#D9D9D9", "K": "#D9D9D9",
    "V2O5": "#E08A1E", "WO3": "#D9C93A", "CdS": "#F2B705", "PbI2": "#F2C400", "AgI": "#E9D96A", "AgBr": "#EDE6A8",
    "PbCrO4": "#F2C400", "BaCrO4": "#F2D23C", "Ag2CrO4": "#9A3324", "CuS": "#1A1A1A", "PbS": "#1A1A1A",
    "Ag2S": "#1A1A1A", "HgS": "#1A1A1A", "FeS": "#1A1A1A", "Fe(OH)3": "#9C4A1A", "Cu(OH)2": "#3C8FD8",
    "Ni(OH)2": "#5BA35B", "Co(OH)2": "#C0607A", "Cr(OH)3": "#6E8F6A", "Fe(OH)2": "#C9D8B8", "CuCO3": "#3E9F86",
    "Fe(NH4)2(SO4)2.6H2O": "#A8D8B0", "KCr(SO4)2.12H2O": "#5B2A6E", "K4[Fe(CN)6]": "#F2D23C",
    "K3[Fe(CN)6]": "#D97A1E", "TiO2": "#F7F7F7", "SiO2.xH2O": "#4E8DF5", "CuSO4": "#EEEEEE",
}
SOLUTION_COLOR = {"Cu2+": "#3C8FD8", "Fe3+": "#C08A2A", "Fe2+": "#B9DAB0", "Ni2+": "#4CAF6A", "Co2+": "#D86A8A",
                  "MnO4-": "#7A1A8A", "MnO42-": "#2E8B57", "CrVI": "#E8A01A", "Cr3+": "#4E8D6A", "I2": "#8A4A1A",
                  "Br2": "#C2571A", "Cl2": "#C9D86A", "FeCN64-": "#E9D96A", "FeCN63-": "#E39A2A", "pp": "#F4F4F4",
                  "mo": "#E36A2A", "mr": "#D23A3A", "btb": "#3A6AD2", "bcg": "#3A6AD2", "tb": "#E9D96A",
                  "ay": "#E9D96A", "starch": "#EEEEEE"}


def color_of(r: dict, comp: list[tuple[str, float]]) -> str:
    f = r["formula"]
    if f in SOLID_COLOR and r["state"] in ("ran", "ket_tua"):
        return SOLID_COLOR[f]
    for sp, _ in comp:
        if sp in SOLUTION_COLOR and r["state"] in ("dd", "long"):
            return SOLUTION_COLOR[sp]
    if f in SOLID_COLOR:
        return SOLID_COLOR[f]
    if r["state"] in ("ran", "ket_tua", "giay"):
        return "#F2F2F0"
    return "#DCEBFA"


def container_type(r: dict, group: str) -> str:
    if r["state"] == "khi":
        return "binh_khi"
    if r["state"] == "giay":
        return "hop_giay"
    if group == "CT" and r["state"] == "dd":
        return "lo_nho_giot"
    if r["state"] in ("ran", "ket_tua"):
        return "lo_rong"
    if r["formula"] in LIGHT_SENSITIVE or group in ("MOX",):
        return "chai_nau"
    return "chai"


# ---------------------------------------------------------------------------
# Thiết bị
# ---------------------------------------------------------------------------

EQUIP_NAMES = {
    "Coc co mo": "Cốc có mỏ", "Binh tam giac": "Bình tam giác (bình nón)", "Binh cau day bang": "Bình cầu đáy bằng",
    "Binh cau day tron": "Bình cầu đáy tròn", "Binh dinh muc": "Bình định mức", "Ong nghiem": "Ống nghiệm",
    "Gia ong nghiem": "Giá ống nghiệm", "Kep ong nghiem": "Kẹp ống nghiệm", "Ong dong": "Ống đong",
    "Pipet bau": "Pipet bầu", "Pipet chia do": "Pipet chia độ", "Micro pipet": "Micropipet",
    "Qua bop cao su": "Quả bóp cao su", "Buret": "Buret", "Kep buret": "Kẹp buret", "Pheu chiet": "Phễu chiết",
    "Pheu loc": "Phễu lọc", "Giay loc": "Giấy lọc", "Pheu Buchner": "Phễu Buchner", "Binh hut loc": "Bình hút lọc",
    "Den con": "Đèn cồn", "Den Bunsen": "Đèn Bunsen", "Kieng ba chan": "Kiềng ba chân", "Luoi amiang": "Lưới tản nhiệt",
    "Gia do": "Giá đỡ (giá sắt)", "Vong sat": "Vòng sắt", "Kep van nang": "Kẹp vạn năng", "Chen nung": "Chén nung",
    "Nap chen nung": "Nắp chén nung", "Kep chen nung": "Kẹp chén nung", "Tam giac dat set": "Tam giác sứ",
    "Dua thuy tinh": "Đũa thủy tinh", "Thia xuc hoa chat": "Thìa xúc hóa chất", "Can dien tu": "Cân điện tử",
    "Can ky thuat": "Cân phân tích", "Nhiet ke": "Nhiệt kế", "Giay do pH": "Giấy đo pH", "May do pH": "Máy đo pH",
    "Bep dien": "Bếp điện", "May khuay tu": "Máy khuấy từ", "Thanh khuay tu": "Con khuấy từ",
    "Binh tia": "Bình tia", "Bat su": "Bát sứ", "Mat kinh dong ho": "Mặt kính đồng hồ", "Dia Petri": "Đĩa Petri",
    "Coi va chay su": "Cối và chày sứ", "Ong sinh hanh": "Ống sinh hàn", "Bo chung cat": "Bộ chưng cất",
    "Binh hut am": "Bình hút ẩm", "Kinh bao ho": "Kính bảo hộ", "Gang tay cao su": "Găng tay cao su",
    "Ao choang thi nghiem": "Áo choàng thí nghiệm", "Tu hut khi doc": "Tủ hút khí độc",
    "Voinuoc rua mat khan": "Vòi rửa mắt khẩn cấp", "Voinuoc sen khan": "Vòi sen khẩn cấp",
    "Binh chua chat thai": "Bình chứa chất thải", "Binh cuu hoa": "Bình chữa cháy", "Chan dap": "Cân đĩa",
    "Binh tam giac co nhanh": "Bình tam giác có nhánh", "Ong nghiem ly tam": "Ống ly tâm",
    "May ly tam": "Máy ly tâm", "Bep tu": "Bếp bọc gia nhiệt", "Bo dieu che khi": "Bộ điều chế khí (bình Kíp)",
    "Binh chua khi": "Bình chứa khí", "Van giam ap": "Van giảm áp", "Ong dan khi": "Ống dẫn khí",
    "Bom hut chan khong": "Bơm hút chân không", "May do do dan dien": "Máy đo độ dẫn điện",
    "May quang pho": "Máy quang phổ", "May do do duc": "Máy so màu", "Bo do nhiet do dien tu": "Nhiệt kế điện tử",
    "Binh dinh muc 100mL": "Bình định mức 100 mL", "Buret tu dong": "Buret tự động", "Bo chuan do": "Bộ chuẩn độ",
    "May do oxy hoa tan": "Máy đo oxi hòa tan", "May do do pH deo": "Máy đo pH cầm tay",
    "Can bang dien tu": "Cân điện tử bàn", "Bo ong nghiem mau": "Bộ ống so màu", "Binh rua khi": "Bình rửa khí",
    "Binh an toan": "Bình an toàn",
}
EQUIP_TEXT = [("Dung va tron", "Đựng và trộn"), ("chat long", "chất lỏng"), ("Dung tron va chuan do", "Đựng, trộn, chuẩn độ"),
              ("Dun hoi luu va chung cat", "Đun hồi lưu, chưng cất"), ("Dun deu", "Đun nóng đều"),
              ("Pha dung dich chinh xac", "Pha dung dịch chính xác"), ("Phan ung luong nho", "Phản ứng lượng nhỏ"),
              ("Giữ ong nghiem", "Giữ ống nghiệm"), ("Kep ong khi dun", "Kẹp ống khi đun"),
              ("Do the tich", "Đo thể tích"), ("Lay the tich chinh xac", "Lấy thể tích chính xác"),
              ("Lay the tich bien doi", "Lấy thể tích tùy chọn"), ("Lay the tich sieu nho", "Lấy thể tích rất nhỏ"),
              ("Hut chat long", "Hút chất lỏng"), ("Chuan do the tich", "Chuẩn độ thể tích"), ("Giữ buret", "Giữ buret"),
              ("Chiet tach", "Chiết tách"), ("Loc chat long", "Lọc chất lỏng"), ("Loc chat ran", "Lọc chất rắn"),
              ("Loc chan khong", "Lọc chân không"), ("Dun nong nhiet do thap", "Đun nóng nhiệt độ thấp"),
              ("Dun nong nhiet do cao", "Đun nóng nhiệt độ cao"), ("Do dung cu khi dun", "Đỡ dụng cụ khi đun"),
              ("Phan bo nhiet", "Phân bố nhiệt"), ("Giữ dung cu", "Giữ dụng cụ"), ("Do binh cau", "Đỡ bình cầu"),
              ("Kep dung cu", "Kẹp dụng cụ"), ("Nung o nhiet do cao", "Nung ở nhiệt độ cao"),
              ("Day chen nung", "Đậy chén nung"), ("Gap chen nung", "Gắp chén nung"), ("Do chen nung", "Đỡ chén nung"),
              ("Khuay dung dich", "Khuấy dung dịch"), ("Lay hoa chat ran", "Lấy hóa chất rắn"),
              ("Can khoi luong lon", "Cân khối lượng lớn"), ("Can khoi luong", "Cân khối lượng"),
              ("Can chinh xac cao", "Cân chính xác cao"), ("Do nhiet do chinh xac", "Đo nhiệt độ chính xác"),
              ("Do nhiet do", "Đo nhiệt độ"), ("Do pH nhanh", "Đo pH nhanh"), ("Do pH chinh xac", "Đo pH chính xác"),
              ("Do pH di dong", "Đo pH di động"), ("Dun nong co dieu chinh", "Đun nóng có điều chỉnh"),
              ("Khuay tu", "Khuấy từ"), ("Rua dung cu", "Rửa dụng cụ"), ("Co can dung dich", "Cô cạn dung dịch"),
              ("Day coc va can hoa chat", "Đậy cốc, cân hóa chất"), ("Chua mau va nuoi cay", "Chứa mẫu, nuôi cấy"),
              ("Nghien chat ran", "Nghiền chất rắn"), ("Lam lanh hoi", "Làm lạnh hơi"), ("Tach chat long", "Tách chất lỏng"),
              ("Bao quan hoa chat kho", "Bảo quản hóa chất khô"), ("Bao ve mat", "Bảo vệ mắt"), ("Bao ve tay", "Bảo vệ tay"),
              ("Bao ve quan ao", "Bảo vệ quần áo"), ("Loai bo khi doc", "Hút khí độc ra ngoài"),
              ("Rua mat khan cap", "Rửa mắt khẩn cấp"), ("Rua toan than", "Rửa toàn thân"), ("Chua chat thai", "Chứa chất thải"),
              ("Chua chay", "Chữa cháy"), ("Ly tam mau", "Ly tâm mẫu"), ("Tach chat ran long", "Tách rắn - lỏng"),
              ("Dun binh cau", "Đun bình cầu"), ("Dieu che khi trong phong", "Điều chế khí trong phòng thí nghiệm"),
              ("Chua khi nen", "Chứa khí nén"), ("Dieu chinh ap suat", "Điều chỉnh áp suất"), ("Dan khi", "Dẫn khí"),
              ("Tao chan khong", "Tạo chân không"), ("Do do dan", "Đo độ dẫn điện"), ("Do hap thu anh sang", "Đo độ hấp thụ ánh sáng"),
              ("Do mau", "Đo màu"), ("Pha dung dich chuan", "Pha dung dịch chuẩn"), ("Chuan do tu dong", "Chuẩn độ tự động"),
              ("Chuan do hoan chinh", "Chuẩn độ hoàn chỉnh"), ("Do oxy hoa tan", "Đo oxi hòa tan"), ("So mau", "So màu"),
              ("Rua khi", "Rửa khí"), ("Giam ap suat", "Giảm áp suất"), ("Co dinh", "Cố định"), ("Kim loai", "Kim loại"),
              ("Go hoac kim loai", "Gỗ hoặc kim loại"), ("Trong suot", "Trong suốt"), ("Thuy tinh/mem", "Thủy tinh / ống mềm"),
              ("Vai/nhua", "Vải / nhựa"), ("CO2/bot", "CO2 / bọt"), ("Dai 0-14", "Dải 0-14"), ("3 van", "3 van"),
              ("lo", "lỗ"), ("den", "đến"), ("Phu hop chen", "Vừa chén"), ("Kim loai/nhua", "Kim loại / nhựa")]
EQUIP_KIND = {"Dung cu thuy tinh": "Dụng cụ thủy tinh", "Dung cu do luong": "Dụng cụ đo lường",
              "Dung cu ho tro": "Dụng cụ hỗ trợ", "Vat tu tieu hao": "Vật tư tiêu hao", "Dung cu loc": "Dụng cụ lọc",
              "Dung cu dun nong": "Dụng cụ đun nóng", "Dung cu nung": "Dụng cụ nung", "Thiet bi do luong": "Thiết bị đo",
              "Thiet bi ho tro": "Thiết bị hỗ trợ", "Dung cu su": "Dụng cụ sứ", "Dung cu": "Dụng cụ",
              "Bo thiet bi": "Bộ thiết bị", "Dung cu bao quan": "Dụng cụ bảo quản", "An toan": "Bảo hộ",
              "Thiet bi an toan": "Thiết bị an toàn", "Thiet bi": "Thiết bị", "Dung cu an toan": "Dụng cụ an toàn"}


def equip_text(s: str) -> str:
    for a, b in EQUIP_TEXT:
        s = s.replace(a, b)
    return s


# ---------------------------------------------------------------------------
# Chính
# ---------------------------------------------------------------------------

def build(chem_csv: Path, equip_csv: Path) -> tuple[dict, dict]:
    chems = []
    for stt, name, formula, cat, state, conc in read_rows(chem_csv, 6):
        r = {"stt": int(stt), "plain": name, "formula": formula, "cat": cat, "state": STATE.get(state, "ran"),
             "conc": conc, "ascii": strip_accents(name).lower()}
        if r["state"] == "dd" and cat in ("Khi",):
            r["state"] = "khi"
        chems.append(r)

    groups = {g[0]: {"ma": g[0], "ten": g[1], "kieu_tu": g[2], "mau": g[3], "so_luong": 0} for g in GROUPS}
    out = []
    for r in chems:
        f = r["formula"] if r["formula"] != "-" else ""
        M = molar_mass(f) if f else None
        if r["ascii"] in HOUSEHOLD_M:
            M = HOUSEHOLD_M[r["ascii"]]
        comp, note = composition(r)
        tags = organic_tags(r)
        g = group_of(r)
        C, Cs = parse_conc(r["conc"], M, r["state"])
        if note in ("per_liter", "indicator"):
            C, Cs = 1.0, [1.0]
        if note == "ethanol":
            pct = 0.7 if "70" in r["plain"] else 0.9
            C = round(pct * 789 / 46.07, 3)
            Cs = [C]
        rec = {
            "stt": r["stt"],
            "ten": add_diacritics(r["plain"]),
            "ten_khong_dau": strip_accents(add_diacritics(r["plain"])).lower(),
            "cong_thuc": f,
            "phan_loai": CATEGORY.get(r["cat"], r["cat"]),
            "nhom": g,
            "trang_thai": r["state"],
            "dang_chua": "",
            "nong_do": conc_text(r["conc"]),
            "C": C,
            "cac_C": Cs,
            "M": M or 0.0,
            "d": DENSITY.get(f, 1.0 if r["state"] in ("dd",) else 0.9),
            "tp": [{"s": s, "n": n} for s, n in comp],
            "tp_theo_lit": note in ("per_liter", "indicator"),
            "ghi_chu": note or "",
            "the": tags,
            "nguy_hiem": [],
            "ka": ORGANIC_KA.get(f, 0.0),
            "kb": AMINE_KB.get(f, 0.0),
            "mau": "",
            "vi_tri": {"ke": "", "tang": 0, "o": 0},
            "ma_vi_tri": "",
        }
        if note == "ethanol":
            rec["the"] = ["ancol", "de_chay"]
            rec["ghi_chu"] = "ethanol"
        rec["nguy_hiem"] = hazards(r, rec["the"])
        rec["mau"] = color_of(r, comp)
        rec["dang_chua"] = container_type(r, g)
        out.append(rec)

    # Xếp kệ: mỗi nhóm bắt đầu kệ mới, trong nhóm sắp theo tên (học sinh dễ dò), kệ chia tầng/ô
    shelves = []
    order = [g[0] for g in GROUPS]
    for code in order:
        items = sorted((c for c in out if c["nhom"] == code), key=lambda c: (c["dang_chua"] == "lo_rong", c["ten_khong_dau"]))
        if not items:
            continue
        kind = groups[code]["kieu_tu"]
        levels, slots = UNIT_SIZE[kind]
        per_unit = levels * slots
        n_units = math.ceil(len(items) / per_unit)
        for u in range(n_units):
            ma = f"K{len(shelves) + 1:02d}"
            part = items[u * per_unit:(u + 1) * per_unit]
            label = groups[code]["ten"] + (f" ({u + 1}/{n_units})" if n_units > 1 else "")
            shelves.append({"ma": ma, "nhom": code, "ten": label, "kieu_tu": kind, "so_tang": levels, "so_o": slots,
                            "so_luong": len(part)})
            for i, c in enumerate(part):
                tang = levels - (i // slots)  # xếp từ tầng trên xuống (tầng 1 là tầng dưới cùng)
                o = i % slots + 1
                c["vi_tri"] = {"ke": ma, "tang": tang, "o": o}
                c["ma_vi_tri"] = f"{ma}-{tang}-{o:02d}"
        groups[code]["so_luong"] = len(items)

    chem_data = {"phien_ban": 1, "nhom": [groups[g[0]] for g in GROUPS if groups[g[0]]["so_luong"] > 0],
                 "ke": shelves, "hoa_chat": out}

    equipment = []
    for stt, name, en, kind, use, spec in read_rows(equip_csv, 6):
        equipment.append({
            "stt": int(stt), "ten": EQUIP_NAMES.get(name, add_diacritics(name)), "ten_tieng_anh": en.strip(),
            "phan_loai": EQUIP_KIND.get(kind, kind), "cong_dung": equip_text(use), "thong_so": equip_text(spec),
        })
    return chem_data, {"phien_ban": 1, "thiet_bi": equipment}


def main(argv: list[str]) -> None:
    if len(argv) != 3:
        print(__doc__)
        sys.exit(1)
    chem, equip = build(Path(argv[1]), Path(argv[2]))
    for folder in (ROOT / "data" / "chem", UNITY_DATA):
        folder.mkdir(parents=True, exist_ok=True)
        (folder / "hoa_chat.json").write_text(json.dumps(chem, ensure_ascii=False, indent=1), encoding="utf-8")
        (folder / "thiet_bi.json").write_text(json.dumps(equip, ensure_ascii=False, indent=1), encoding="utf-8")
    n_comp = sum(1 for c in chem["hoa_chat"] if c["tp"] or c["ghi_chu"])
    print(f"{len(chem['hoa_chat'])} hóa chất, {len(chem['ke'])} kệ, {n_comp} chất có thành phần phản ứng; "
          f"{len(equip['thiet_bi'])} thiết bị")


if __name__ == "__main__":
    main(sys.argv)
