"""Bước 4 – Kiểm tra bằng RDKit: SMILES hợp lệ, cân bằng nguyên tố và điện tích, tính nhất quán của chuỗi."""
from __future__ import annotations

import re
from collections import Counter
from dataclasses import dataclass, field

from rdkit import Chem, RDLogger

from .schemas import ChatPhanUng, LoiGiai, PhanTichDe

RDLogger.DisableLog("rdApp.*")

_EPS = 1e-6


def mol_tu_smiles(smiles: str):
    """Trả về (mol, thông báo lỗi)."""
    s = (smiles or "").strip()
    if not s:
        return None, "SMILES trống"
    mol = Chem.MolFromSmiles(s)
    if mol is None:
        return None, f"SMILES không hợp lệ: {s}"
    return mol, ""


def dem_nguyen_to(mol) -> tuple[Counter, int]:
    """Đếm số nguyên tử mỗi nguyên tố (kể cả H ẩn) và tổng điện tích. Bỏ qua nguyên tử * (đầu nối polime)."""
    c: Counter = Counter()
    charge = 0
    for atom in mol.GetAtoms():
        if atom.GetAtomicNum() == 0:
            continue
        c[atom.GetSymbol()] += 1
        h = atom.GetTotalNumHs()
        if h:
            c["H"] += h
        charge += atom.GetFormalCharge()
    return c, charge


def cong_thuc_hill(c: Counter) -> str:
    """Counter → công thức kiểu Hill (C trước, H sau, còn lại theo bảng chữ cái)."""
    def part(el: str, n: float) -> str:
        n = round(n, 3)
        if abs(n) < _EPS:
            return ""
        num = "" if abs(n - 1) < _EPS else (str(int(n)) if abs(n - round(n)) < _EPS else str(n))
        return f"{el}{num}"

    keys = sorted(c)
    if "C" in c:
        keys = ["C"] + (["H"] if "H" in c else []) + [k for k in keys if k not in ("C", "H")]
    return "".join(part(k, c[k]) for k in keys) or "∅"


_NGUYEN_TO = frozenset(Chem.GetPeriodicTable().GetElementSymbol(z) for z in range(1, 119))
_TOKEN_CT = re.compile(r"([A-Z][a-z]?)(\d*)|([(\[])|([)\]])(\d*)")


def cong_thuc_tu_chu(text: str) -> Counter | None:
    """Đếm nguyên tố của công thức viết bằng chữ: "C3H6O", "CH3COOC2H5", "CH2=CH–CH3", "(CH3COO)2Ca".

    Trả về None nếu không phải công thức (kí hiệu X, tên chất, polime "(C6H10O5)n", muối ngậm nước...).
    """
    t = re.sub(r"[\s=≡–—\-]", "", (text or "").translate(str.maketrans("₀₁₂₃₄₅₆₇₈₉", "0123456789")))
    if not t or not t[0].isupper() and t[0] not in "([":
        return None
    ngan: list[Counter] = [Counter()]
    vi_tri = 0
    for m in _TOKEN_CT.finditer(t):
        if m.start() != vi_tri:
            return None
        vi_tri = m.end()
        el, n, mo, dong, n_dong = m.groups()
        if el:
            if el not in _NGUYEN_TO:
                return None
            ngan[-1][el] += int(n or 1)
        elif mo:
            ngan.append(Counter())
        else:
            if len(ngan) == 1:
                return None
            nhom = ngan.pop()
            for k, v in nhom.items():
                ngan[-1][k] += v * int(n_dong or 1)
    if vi_tri != len(t) or len(ngan) != 1 or not ngan[0]:
        return None
    return ngan[0]


def ctpt_smiles(smiles: str) -> str:
    """CTPT kiểu Hill từ SMILES (bỏ qua đầu nối * của polime); "" nếu SMILES sai."""
    mol, _ = mol_tu_smiles(smiles)
    if mol is None:
        return ""
    return cong_thuc_hill(dem_nguyen_to(mol)[0])


def _ctpt_can_kiem(c) -> tuple[Counter | None, str]:
    """(CTPT cần khớp, nguồn) của một chất trong đề: CTPT đề cho kèm chất ẩn, hoặc chính công thức chất đã biết."""
    if (c.ctpt_de_cho or "").strip():
        return cong_thuc_tu_chu(c.ctpt_de_cho), "đề cho CTPT"
    kh = (c.ki_hieu or "").strip()
    # Kí hiệu ẩn một chữ (A, B, C, X, Y, Y1...) có thể trùng kí hiệu nguyên tố – không coi là công thức
    if c.la_chat_an or re.fullmatch(r"[A-Z][a-z]?[0-9']*", kh):
        return None, ""
    return cong_thuc_tu_chu(kh), "công thức đề viết"


def _khoa_ki_hieu(text: str) -> str:
    """"C (C3H6O)" / "C: C3H6O" → "C" để so kí hiệu giữa đề và lời giải."""
    t = re.sub(r"\s*(?:\([^()]*\)|:.*)$", "", (text or "").strip()) or (text or "").strip()
    return re.sub(r"\s+", "", t).upper()


def kiem_tra_ctpt(sol: LoiGiai, de: PhanTichDe) -> list[str]:
    """So CTPT (RDKit) của từng chất trong lời giải với CTPT đề cho / công thức đề viết."""
    smiles_theo_ki_hieu: dict[str, list[str]] = {}
    for x in list(sol.so_do) + list(sol.chat_an):  # kiểm tra cả so_do lẫn bảng chất ẩn
        s = (x.smiles or "").strip()
        ds = smiles_theo_ki_hieu.setdefault(_khoa_ki_hieu(x.ki_hieu), [])
        if s and s not in ds:
            ds.append(s)
    loi = []
    for c in de.cac_chat:
        can, nguon = _ctpt_can_kiem(c)
        if can is None:
            continue
        for smiles in smiles_theo_ki_hieu.get(_khoa_ki_hieu(c.ki_hieu), []):
            mol, _ = mol_tu_smiles(smiles)
            if mol is None or "*" in smiles:
                continue  # lỗi SMILES đã được báo ở chỗ khác; polime không so CTPT
            co = dem_nguyen_to(mol)[0]
            if +co != +can:
                loi.append(f"chất '{c.ki_hieu}': {nguon} {cong_thuc_hill(can)} nhưng SMILES {smiles} có CTPT "
                           f"{cong_thuc_hill(co)} – hãy chọn lại cấu tạo cho đúng")
    return loi


@dataclass
class KetQuaCanBang:
    can_bang: bool
    ve_trai: Counter
    ve_phai: Counter
    dien_tich_trai: float = 0
    dien_tich_phai: float = 0
    loi: list[str] = field(default_factory=list)


def _cong_ve(chat: list[ChatPhanUng], ten_ve: str) -> tuple[Counter, float, list[str]]:
    tong: Counter = Counter()
    dien_tich = 0.0
    loi = []
    for x in chat:
        if x.he_so is None or x.he_so <= 0:
            loi.append(f"hệ số của '{x.ten}' ở {ten_ve} phải lớn hơn 0")
            continue
        mol, err = mol_tu_smiles(x.smiles)
        if mol is None:
            loi.append(f"'{x.ten}': {err}")
            continue
        c, q = dem_nguyen_to(mol)
        for k, v in c.items():
            tong[k] += v * x.he_so
        dien_tich += q * x.he_so
    return tong, dien_tich, loi


def kiem_tra_can_bang(chat_tham_gia: list[ChatPhanUng], san_pham: list[ChatPhanUng]) -> KetQuaCanBang:
    trai, q_trai, loi1 = _cong_ve(chat_tham_gia, "vế trái")
    phai, q_phai, loi2 = _cong_ve(san_pham, "vế phải")
    loi = loi1 + loi2
    if not chat_tham_gia:
        loi.append("thiếu chất tham gia")
    if not san_pham:
        loi.append("thiếu sản phẩm")
    if not loi:
        lech = {el: phai.get(el, 0) - trai.get(el, 0) for el in set(trai) | set(phai)}
        lech = {el: d for el, d in lech.items() if abs(d) > _EPS}
        if lech:
            chi_tiet = ", ".join(f"{el} {'+' if d > 0 else ''}{round(d, 3):g}" for el, d in sorted(lech.items()))
            loi.append(
                f"chưa cân bằng nguyên tố: vế trái {cong_thuc_hill(trai)}, vế phải {cong_thuc_hill(phai)} "
                f"(vế phải lệch {chi_tiet})"
            )
        if abs(q_trai - q_phai) > _EPS:
            loi.append(f"chưa cân bằng điện tích: vế trái {q_trai:g}, vế phải {q_phai:g}")
    return KetQuaCanBang(not loi, trai, phai, q_trai, q_phai, loi)


def _khoa_smiles(smiles: str) -> str | None:
    mol, _ = mol_tu_smiles(smiles)
    return Chem.MolToSmiles(mol, isomericSmiles=False) if mol is not None else None


def kiem_tra_loi_giai(sol: LoiGiai, de: PhanTichDe | None = None) -> dict:
    """Kiểm tra toàn bộ lời giải.

    Trả về {"chung": [lỗi chung], "buoc": {số bước: [lỗi]}}; điền luôn Buoc.loi / Buoc.can_kiem_tra.
    """
    chung: list[str] = []
    theo_buoc: dict[int, list[str]] = {}

    nut = {}
    for n in sol.so_do:
        mol, err = mol_tu_smiles(n.smiles)
        if mol is None:
            chung.append(f"so_do '{n.ki_hieu}': {err}")
        n.ctpt = cong_thuc_hill(dem_nguyen_to(mol)[0]) if mol is not None else ""
        nut[n.ki_hieu.strip()] = n
    for x in sol.chat_an:
        x.ctpt = ""
        if x.smiles.strip():
            mol, err = mol_tu_smiles(x.smiles)
            if mol is None:
                chung.append(f"chất ẩn '{x.ki_hieu}': {err}")
            else:
                x.ctpt = cong_thuc_hill(dem_nguyen_to(mol)[0])
        else:
            chung.append(f"chất ẩn '{x.ki_hieu}' chưa có SMILES")

    if not sol.cac_buoc:
        chung.append("lời giải không có bước nào")
    if de is not None and de.cac_buoc and len(sol.cac_buoc) != len(de.cac_buoc):
        chung.append(f"đề có {len(de.cac_buoc)} bước nhưng lời giải có {len(sol.cac_buoc)} bước")
    if de is not None:
        chung.extend(kiem_tra_ctpt(sol, de))

    for b in sol.cac_buoc:
        loi: list[str] = []
        kq = kiem_tra_can_bang(b.chat_tham_gia, b.san_pham)
        loi.extend(kq.loi)
        if sol.so_do:
            for vai_tro, ki_hieu, ds, ten_ds in (
                ("chất đầu", b.chat_dau, b.chat_tham_gia, "chat_tham_gia"),
                ("chất sau", b.chat_sau, b.san_pham, "san_pham"),
            ):
                n = nut.get(ki_hieu.strip())
                if n is None:
                    loi.append(f"{vai_tro} '{ki_hieu}' không có trong so_do")
                    continue
                k = _khoa_smiles(n.smiles)
                if k and k not in {_khoa_smiles(x.smiles) for x in ds}:
                    loi.append(f"{vai_tro} '{ki_hieu}' (SMILES {n.smiles}) không có trong {ten_ds}")
        b.loi = loi
        b.can_kiem_tra = bool(loi)
        if loi:
            theo_buoc[b.so] = loi
    return {"chung": chung, "buoc": theo_buoc}


def danh_sach_loi(kq: dict) -> list[str]:
    out = list(kq["chung"])
    for so, loi in sorted(kq["buoc"].items()):
        out.extend(f"Bước {so}: {x}" for x in loi)
    return out
