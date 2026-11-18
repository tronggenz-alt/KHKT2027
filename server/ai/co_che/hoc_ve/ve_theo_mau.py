"""Vẽ sơ đồ cơ chế "trên giấy" tự động theo kho mẫu học từ đáp án (kho_mau/khung_mau_dap_an.json trong thư mục dữ liệu riêng).

    python ve_theo_mau.py ../du_lieu/propellane_dmad_A4.json   → ../du_lieu/propellane_dmad_A4.giay.js (window.SO_DO_TU_DONG)

Từng chất:
  1. Tìm các hệ vòng; hệ vòng nào có trong kho mẫu thì thử MỌI cách vẽ đã học (tối đa 3) × mọi đối xứng của khung,
     cố định toạ độ vòng (coordMap) cho RDKit bày phần còn lại; hệ vòng lớn nhất giữ đúng hướng trên trang như đáp án,
     các hệ vòng phụ được xoay cho khớp chỗ RDKit đặt.
  2. Chấm từng phương án: nét cắt nhau, nguyên tử chen sát, tỉ lệ dùng của cách vẽ trong đáp án → chọn phương án tốt nhất.
  3. Chất không vòng: RDKit bày rồi xoay cho trục dài nằm ngang.
Nhãn: dị tố ghi kèm H (OH, NH2…), nhóm viết gọn theo nhan_hien_thi; nhãn nằm bên trái chỗ gắn thì đảo chiều
(CO2Me → MeO2C, OH → HO) như đáp án. Electron độc thân: chấm ở phía trống. Mũi tên cong đặt theo atom map, cong ra phía ngoài.
"""
from __future__ import annotations

import json
import math
import re
import sys
from pathlib import Path

import numpy as np
from rdkit import Chem, RDLogger
from rdkit.Chem import rdDepictor
from rdkit.Geometry import Point2D, Point3D

from . import hoc_mau
from ..duong_dan import doc_kho_mau

RDLogger.DisableLog("rdApp.*")
rdDepictor.SetPreferCoordGen(True)
# Kho mẫu học từ đáp án HSG là dữ liệu KÍN: nằm ở thư mục dữ liệu riêng (VERA_DU_LIEU_RIENG), không có trong kho git.
# Không có kho thì vẫn vẽ được nhờ tư duy vẽ (nguyên tắc chung), chỉ kém giống đáp án hơn ở các khung đã gặp.
_KHO_GOC = doc_kho_mau()
KHO = {k["khoa"]: k for k in _KHO_GOC.get("khung", [])}            # hệ vòng trơ
KHO_VO = {k["khoa"]: k for k in _KHO_GOC.get("khung_vo", [])}      # hệ vòng + nguyên tử gắn trực tiếp (ưu tiên)
CHI_SO_DUOI = str.maketrans("0123456789", "₀₁₂₃₄₅₆₇₈₉")
DAO_CHIEU = {"CO2Me": "MeO2C", "CO2Et": "EtO2C", "CO2H": "HO2C", "OMe": "MeO", "OEt": "EtO", "OH": "HO", "NH2": "H2N",
             "SH": "HS", "NO2": "O2N", "CN": "NC", "OAc": "AcO", "OBn": "BnO", "OTs": "TsO", "CHO": "OHC", "COOH": "HOOC"}


def _mol(smiles: str):
    m = Chem.MolFromSmiles(smiles, sanitize=False)
    m.UpdatePropertyCache(strict=False)
    Chem.FastFindRings(m)
    return m


def _toa_do(m) -> np.ndarray:
    c = m.GetConformer()
    return np.array([[c.GetAtomPosition(i).x, c.GetAtomPosition(i).y] for i in range(m.GetNumAtoms())])


def _diem_xau(m, P: np.ndarray) -> float:
    """Điểm phạt độ khó đọc: nét cắt nhau (nặng), nguyên tử không liên kết chen sát nhau."""
    lk = [(b.GetBeginAtomIdx(), b.GetEndAtomIdx()) for b in m.GetBonds()]
    phat = 0.0
    for i in range(len(lk)):
        for j in range(i + 1, len(lk)):
            a, b = lk[i]; c, d = lk[j]
            if len({a, b, c, d}) < 4:
                continue
            p1, p2, p3, p4 = P[a], P[b], P[c], P[d]
            den = (p2[0] - p1[0]) * (p4[1] - p3[1]) - (p2[1] - p1[1]) * (p4[0] - p3[0])
            if abs(den) < 1e-9:
                continue
            t = ((p3[0] - p1[0]) * (p4[1] - p3[1]) - (p3[1] - p1[1]) * (p4[0] - p3[0])) / den
            u = ((p3[0] - p1[0]) * (p2[1] - p1[1]) - (p3[1] - p1[1]) * (p2[0] - p1[0])) / den
            if 0 < t < 1 and 0 < u < 1:
                phat += 10
    # nét lệch khỏi lưới góc 30° (hình ChemDraw trong đáp án gần như luôn nằm trên lưới này)
    for a, b in lk:
        g = math.degrees(math.atan2(P[b][1] - P[a][1], P[b][0] - P[a][0])) % 30
        phat += min(g, 30 - g) / 30
    for a, b in lk:                       # nét dài/ngắn bất thường (mẫu ghép sai chỗ)
        d = float(np.linalg.norm(P[a] - P[b])) / 1.5
        if abs(d - 1) > .25:
            phat += 5
    gan = {(min(a, b), max(a, b)) for a, b in lk}
    n = len(P)
    for i in range(n):
        for j in range(i + 1, n):
            if (i, j) not in gan and np.linalg.norm(P[i] - P[j]) < .55 * 1.5:
                phat += 3
    return phat


def _kabsch(A: np.ndarray, B: np.ndarray):
    """R, t (cho phép lật gương) để A·R + t khớp B."""
    ca, cb = A.mean(0), B.mean(0)
    U, _, Vt = np.linalg.svd((A - ca).T @ (B - cb))
    R = U @ Vt
    return R, cb - ca @ R


def bay_theo_mau(smiles: str, m=None) -> np.ndarray:
    """Toạ độ 2D (đơn vị RDKit, liên kết 1,5) của một chất theo kho mẫu đáp án (cách bày của bản thử 15/11)."""
    m = m if m is not None else _mol(smiles)
    he = sorted((h for h in hoc_mau.he_vong(m) if len(h) >= 3), key=len, reverse=True)
    if not he:
        rdDepictor.Compute2DCoords(m)
        P = _toa_do(m)
        if len(P) > 1:                                    # trục dài nằm ngang
            Q = P - P.mean(0)
            _, _, Vt = np.linalg.svd(Q)
            g = math.atan2(Vt[0][1], Vt[0][0])
            R = np.array([[math.cos(-g), -math.sin(-g)], [math.sin(-g), math.cos(-g)]])
            P = Q @ R.T
        return P

    def ung_vien(hv):
        """Các phương án đặt hệ vòng: mẫu kèm nhóm gắn (cố định cả hướng nhánh) trước, rồi mẫu khung trơ."""
        ra = []
        for vo, kho, thuong in ((True, KHO_VO, 0), (False, KHO, 1.5)):
            khoa, thu_tu, q = hoc_mau.khoa_va_thu_tu(m, hv, vo)
            mau = kho.get(khoa)
            if not mau:
                continue
            dx = hoc_mau.doi_xung_giu_loai(q, 200)
            for cv in mau.get("cach_ve") or [{"ty_le": mau["thong_nhat"], "toa_do": mau["toa_do"]}]:
                T = np.array(cv["toa_do"]) * 1.5
                for g in dx:
                    ra.append(({thu_tu[g[i]]: T[i] for i in range(len(g))}, cv["ty_le"] - thuong / 3))
        return ra

    tot, tot_d = None, 1e9
    chinh = ung_vien(he[0])
    if not chinh:
        rdDepictor.Compute2DCoords(m)
        tot = _toa_do(m)
    for cm, ty_le in chinh:
        try:
            _bay_co_dinh(m, coordMap={k: Point2D(*v) for k, v in cm.items()})
        except Exception:  # noqa: BLE001
            continue
        P = _toa_do(m)
        d = _diem_xau(m, P) + 2 * (1 - ty_le)
        if d < tot_d:
            tot, tot_d = (P, cm), d
    if isinstance(tot, tuple):
        P, cm = tot
    else:
        P, cm = tot, {}
    # hệ vòng phụ: xoay mẫu cho khớp chỗ RDKit đã đặt, cố định thêm rồi bày lại
    for hv in he[1:]:
        uv = ung_vien(hv)
        if not uv or not cm:
            continue
        ds = sorted(hv)
        tot2 = min(((cm2, ty) for cm2, ty in uv), key=lambda x: _sai_lech(x[0], P, ds))
        A = np.array([tot2[0][i] for i in ds])
        R, t = _kabsch(A, P[ds])
        for i in ds:
            cm[i] = tot2[0][i] @ R + t
        _bay_co_dinh(m, coordMap={k: Point2D(*v) for k, v in cm.items()})
        P = _toa_do(m)
    return P


def _bay_co_dinh(m, coordMap):
    rdDepictor.SetPreferCoordGen(False)
    try:
        rdDepictor.Compute2DCoords(m, coordMap=coordMap)
    finally:
        rdDepictor.SetPreferCoordGen(True)


def _sai_lech(cm, P, ds):
    A = np.array([cm[i] for i in ds])
    R, t = _kabsch(A, P[ds])
    return float(((A @ R + t - P[ds]) ** 2).sum())


# ---------------------------------------------------------------------------- dựng sơ đồ cả cơ chế
def _nhan(a, m, P, i, nhan_map: dict) -> str:
    mp = a.GetAtomMapNum()
    if a.GetAtomicNum() == 0:
        t = nhan_map.get(mp, "R")
    elif a.GetAtomicNum() == 6:
        return ""
    else:
        h = a.GetTotalNumHs()
        t = a.GetSymbol() + ("H" if h == 1 else f"H{h}" if h > 1 else "")
    hx = [P[n.GetIdx()][0] for n in a.GetNeighbors()]
    if hx and P[i][0] < min(hx) - .3 and t in DAO_CHIEU:   # nhãn nằm bên trái chỗ gắn → đảo chiều như đáp án
        t = DAO_CHIEU[t]
    return t.translate(CHI_SO_DUOI)


def _phia_trong(P: np.ndarray, i: int, ke: list[int]) -> np.ndarray:
    v = np.zeros(2)
    for j in ke:
        d = P[i] - P[j]
        v += d / (np.linalg.norm(d) + 1e-9)
    if np.linalg.norm(v) < 1e-6:
        v = np.array([0, -1.0])
    return v / np.linalg.norm(v)


# ============================================================================ TƯ DUY VẼ (18/11): bày chất
# Thứ tự: kho mẫu (khung đã gặp) → khung dựng sẵn (lồng [1.1.1] kiểu "lều") → tư duy vẽ (TuDuy: nguyên tắc chung, xoay 12
# góc × lật) → mạch hở: CoordGen (zigzag 120°) rồi xoay cho nét nằm trên lưới 30° và trục dài nằm ngang.
from ..duong_dan import doc_nguyen_tac_ve  # noqa: E402
from . import tu_duy_ve as td  # noqa: E402
from . import thu_gon as tg  # noqa: E402
from .bo_do import _bezier, _giao, _hop_nhan, _trong  # noqa: E402

_TU_DUY = td.TuDuy(KHO, KHO_VO, doc_nguyen_tac_ve())
# khung "lều" của đáp án cho [1.1.1]propellane / bicyclo[1.1.1]pentane (ve_giay.js KHUNG_MAU.leu), đơn vị liên kết = 1
LEU = {"T": (.07, 1.55), "L": (-1.55, -.13), "R": (1.08, -.81), "B1": (.47, .34), "B2": (0, -.27)}


def _khung_leu(m, hv: set[int]):
    """Hệ vòng là bicyclo[1.1.1] (có/không liên kết trung tâm) → các cách gán đỉnh lều (coordMap, cặp nét 'sau')."""
    if len(hv) != 5:
        return []
    ke = {i: [n.GetIdx() for n in m.GetAtomWithIdx(i).GetNeighbors() if n.GetIdx() in hv] for i in hv}
    dau = [i for i in hv if len(ke[i]) >= 3]
    cau = [i for i in hv if i not in dau]
    if len(dau) != 2 or len(cau) != 3 or any(set(dau) - set(ke[c]) for c in cau):
        return []
    import itertools
    ra = []
    for b1, b2 in (dau, dau[::-1]):
        for t, l, r in itertools.permutations(cau):
            cm = {b1: LEU["B1"], b2: LEU["B2"], t: LEU["T"], l: LEU["L"], r: LEU["R"]}
            ra.append(({k: np.array(v) * 1.5 for k, v in cm.items()}, {(min(l, b1), max(l, b1))}))
    return ra


def _xoay_luoi(P: np.ndarray, L: np.ndarray) -> np.ndarray:
    """Mạch hở: chọn góc xoay (và lật) cho nét nằm trên lưới 30° và trục dài gần nằm ngang."""
    if not len(L):
        return P
    Q = P - P.mean(0)
    goc = np.degrees(np.arctan2(*(Q[L[:, 1]] - Q[L[:, 0]]).T[::-1]))
    tot, tot_d = Q, 1e9
    for a in set(np.round(goc, 3)):
        for k in range(12):
            g = math.radians(k * 30 - a)
            R = np.array([[math.cos(g), -math.sin(g)], [math.sin(g), math.cos(g)]])
            X = Q @ R.T
            gg = np.degrees(np.arctan2(*(X[L[:, 1]] - X[L[:, 0]]).T[::-1])) % 30
            lech = float((np.minimum(gg, 30 - gg) / 15).mean())
            d = 10 * lech + 2 * (1 - td.truc_ngang(X)) + .05 * (k % 6)
            if d < tot_d - 1e-9:
                tot, tot_d = X, d
    return tot


def bay_chat_chi_tiet(smiles: str, m=None) -> tuple[np.ndarray, set]:
    """Toạ độ 2D (đơn vị RDKit 1,5) + các cặp nguyên tử vẽ nét 'sau' (cầu phía sau của khung lều)."""
    m = m if m is not None else _mol(smiles)
    if m.GetNumAtoms() == 1:                              # ion/nguyên tử đứng riêng (H⁺, Cl•, Br⁻)
        conf = Chem.Conformer(1)
        m.RemoveAllConformers()
        m.AddConformer(conf, assignId=True)
        return np.zeros((1, 2)), set()
    L = td.lien_ket(m)
    he = sorted((h for h in hoc_mau.he_vong(m) if len(h) >= 3), key=len, reverse=True)
    if not he:
        rdDepictor.SetPreferCoordGen(True)
        rdDepictor.Compute2DCoords(m)
        return _xoay_luoi(_toa_do(m), L), set()
    ung_vien = []                                         # (P, cặp 'sau', điểm phạt)
    sach = lambda P: td.so_net_cat(P / 1.5, L) + td.so_cap_sat(P / 1.5, L) == 0
    try:
        P = bay_theo_mau(smiles, m)
        ung_vien.append((P, set(), _diem_xau(m, P) + (0 if any(hoc_mau.khoa_va_thu_tu(m, h, v)[0] in k for h in he[:1]
                                                                for v, k in ((True, KHO_VO), (False, KHO))) else 3)))
    except Exception:  # noqa: BLE001
        pass
    for h in he:
        for cm, sau in _khung_leu(m, h):
            try:
                _bay_co_dinh(m, coordMap={k: Point2D(*v) for k, v in cm.items()})
            except Exception:  # noqa: BLE001
                continue
            P = _toa_do(m)
            ung_vien.append((P, sau, _diem_xau(m, P) - 2))
    if not ung_vien or not sach(min(ung_vien, key=lambda x: x[2])[0]):
        P = _TU_DUY.ve(Chem.Mol(m)) * 1.5             # tư duy vẽ: khung lạ, hoặc mẫu gây nét cắt / nguyên tử chen sát
        ung_vien.append((P, set(), _diem_xau(m, P)))
    P, sau, _ = min(ung_vien, key=lambda x: x[2])
    return P, sau


def bay_chat(smiles: str) -> np.ndarray:
    return bay_chat_chi_tiet(smiles)[0]


# ============================================================================ nhãn, điện tích, lập thể
DIEN_TICH = {1: "⁺", -1: "⁻", 2: "²⁺", -2: "²⁻", 3: "³⁺", -3: "³⁻"}


def _dao_nhan(t: str) -> str:
    """Nhãn nằm bên trái chỗ gắn: CO2Me → MeO2C, OH → HO, NH3⁺ → H3N⁺, CH3 → H3C (như đáp án)."""
    if t in DAO_CHIEU:
        return DAO_CHIEU[t]
    m = re.fullmatch(r"([A-Z][a-z]?)(H\d*)([⁺⁻²³]*)", t)
    return f"{m.group(2)}{m.group(1)}{m.group(3)}" if m else t


def _nhan_nt(a, m, P, nhan_map: dict, ghi_c: bool) -> str:
    """Nhãn nguyên tử (chưa đổi chỉ số dưới): dị tố kèm H và điện tích; C chỉ ghi khi ghi_c (chất nhỏ, C không nối C)."""
    mp, z = a.GetAtomMapNum(), a.GetAtomicNum()
    q = DIEN_TICH.get(a.GetFormalCharge(), "")
    ke = [P[n.GetIdx()][0] for n in a.GetNeighbors()]
    ben_trai = bool(ke) and P[a.GetIdx()][0] < min(ke) - .2
    if z == 0:
        t = nhan_map.get(mp, "R")
        return _dao_nhan(t) if ben_trai else t
    h = a.GetTotalNumHs()
    hh = "H" if h == 1 else f"H{h}" if h > 1 else ""
    if z == 6 and not ghi_c:
        return ""
    t = a.GetSymbol() + hh + q
    if ben_trai:
        t = _dao_nhan(t) if not q or t[:-len(q)] not in DAO_CHIEU else DAO_CHIEU[t[:-len(q)]] + q
    return t


def _ghi_c(m, a) -> bool:
    nang = sum(1 for x in m.GetAtoms() if x.GetAtomicNum() != 1)
    co_c_ke = any(n.GetAtomicNum() != 1 for n in a.GetNeighbors())
    return nang <= 2 or not co_c_ke


def _net_lap_the(m, P: np.ndarray) -> dict[tuple[int, int], str]:
    """Nét đậm / nét gạch cho tâm lập thể (RDKit WedgeMolBonds trên toạ độ 2D đã bày). Khoá (đầu hẹp, đầu rộng)."""
    if not any(a.GetChiralTag() != Chem.ChiralType.CHI_UNSPECIFIED for a in m.GetAtoms()):
        return {}
    try:
        m2 = Chem.Mol(m)
        Chem.SanitizeMol(m2)
        Chem.AssignStereochemistry(m2, cleanIt=True, force=True)
        conf = Chem.Conformer(m2.GetNumAtoms())
        for i, (x, y) in enumerate(P):
            conf.SetAtomPosition(i, Point3D(float(x), float(y), 0.0))
        m2.RemoveAllConformers()
        m2.AddConformer(conf, assignId=True)
        Chem.WedgeMolBonds(m2, m2.GetConformer())
    except Exception:  # noqa: BLE001
        return {}
    ra = {}
    for b in m2.GetBonds():
        d = b.GetBondDir()
        if d in (Chem.BondDir.BEGINWEDGE, Chem.BondDir.BEGINDASH):
            ra[(b.GetBeginAtomIdx(), b.GetEndAtomIdx())] = "dam" if d == Chem.BondDir.BEGINWEDGE else "gach"
    return ra


class Chat:
    """Một chất đã bày trên giấy (toạ độ theo độ dài liên kết = 1, tâm khung bao ở gốc)."""

    def __init__(self, smiles: str, nhan_map: dict, truoc: dict | None = None, thu_gon: bool = False,
                 giu: frozenset = frozenset()):
        m = _mol(smiles)
        try:
            Chem.Kekulize(m, clearAromaticFlags=True)    # vòng thơm vẽ nét đôi xen kẽ như SGK
        except Exception:  # noqa: BLE001
            pass
        self.an: set[int] = set()                         # H gộp vào nhãn (không vẽ)
        self.thu_gon = thu_gon and tg.dung_duoc(m)
        self.nhan_map = nhan_map
        if self.thu_gon:                                  # công thức thu gọn kiểu SGK (18/11)
            m = tg.tach_h_di_to(m)
            self.m = m
            self.an = tg.h_an(m, set(giu))
            self.sau = set()
            self.P = tg.bay(m, self.an, set(giu), truoc)
            self._dat_nhan()
            self.lap_the = {}
            return
        self.m = m
        P, self.sau = bay_chat_chi_tiet(smiles, m)
        P = P / 1.5
        P = P - P.mean(0) if len(P) else P
        if truoc is not None:
            P = _can_theo_truoc(m, P, truoc)
        self.P = P
        self.nhan_map = nhan_map
        self._dat_nhan()
        self._go_chong()
        self.lap_the = _net_lap_the(m, self.P)

    # --- nhãn và đẩy nhãn khi chồng
    def _dat_nhan(self):
        m, P = self.m, self.P
        self.nhan = {}
        for a in m.GetAtoms():
            if self.thu_gon:
                if a.GetIdx() in self.an:
                    continue
                t = tg.nhan(a, self.an)
                ke = [P[n.GetIdx()][0] for n in a.GetNeighbors() if n.GetIdx() not in self.an]
                q = DIEN_TICH.get(a.GetFormalCharge(), "")
                if (len(ke) == 1 and P[a.GetIdx()][0] < ke[0] - .2) or \
                        (not ke and a.GetSymbol() in ("O", "S") and tg.so_h(a, self.an) >= 2):   # H₂O, H₃O⁺ như SGK
                    t = _dao_nhan(t) if not q else _dao_nhan(t[:-len(q)]) + q
            else:
                t = _nhan_nt(a, m, P, self.nhan_map, _ghi_c(m, a))
            if t:
                self.nhan[a.GetIdx()] = t.translate(CHI_SO_DUOI)

    def hien(self, i: int) -> bool:
        return i not in self.an

    def so_do_rieng(self, tien_to: str = "a", dx: float = 0.0, dy: float = 0.0) -> dict:
        sd = {"nt": {}, "lk": []}
        for i in range(self.m.GetNumAtoms()):
            if i in self.an:
                continue
            sd["nt"][f"{tien_to}{i}"] = [float(self.P[i][0] + dx), float(self.P[i][1] + dy)] + ([self.nhan[i]] if i in self.nhan else [])
        for b in self.m.GetBonds():
            i, j = b.GetBeginAtomIdx(), b.GetEndAtomIdx()
            if i in self.an or j in self.an:
                continue
            bac = {Chem.BondType.DOUBLE: 2, Chem.BondType.TRIPLE: 3}.get(b.GetBondType(), 1)
            sd["lk"].append([f"{tien_to}{i}", f"{tien_to}{j}", bac])
        return sd

    def _so_loi(self) -> int:
        from .bo_do import do_de_nhin
        r = do_de_nhin(self.so_do_rieng())
        return r["cat_nhau"] * 2 + r["chu_de_net"] + r["chu_de_chu"]

    def _go_chong(self, so_vong: int = 3):
        """Nhãn đè nét / đè nhãn: xoay nét của nguyên tử đầu mạch có nhãn quanh nguyên tử kề (±30°…±120°)."""
        loi = self._so_loi()
        for _ in range(so_vong):
            if not loi:
                return
            cai_thien = False
            for i in list(self.nhan):
                a = self.m.GetAtomWithIdx(i)
                if a.GetDegree() != 1:
                    continue
                j = a.GetNeighbors()[0].GetIdx()
                goc_p = self.P[i].copy()
                tot = (loi, goc_p)
                for g in (30, -30, 60, -60, 90, -90, 120, -120, 180):
                    c, s = math.cos(math.radians(g)), math.sin(math.radians(g))
                    v = goc_p - self.P[j]
                    self.P[i] = self.P[j] + np.array([c * v[0] - s * v[1], s * v[0] + c * v[1]])
                    self._dat_nhan()
                    d = self._so_loi()
                    if d < tot[0]:
                        tot = (d, self.P[i].copy())
                self.P[i] = tot[1]
                self._dat_nhan()
                if tot[0] < loi:
                    loi, cai_thien = tot[0], True
            if not cai_thien:
                return

    # --- khung bao (kể cả hộp chữ) để xếp chất cạnh nhau
    def khung(self) -> tuple[float, float, float, float]:
        sd = self.so_do_rieng()
        xs, ys = list(self.P[:, 0]) if len(self.P) else [0.0], list(self.P[:, 1]) if len(self.P) else [0.0]
        for h in _hop_nhan(sd).values():
            xs += [h[0], h[1]]; ys += [h[2], h[3]]
        return min(xs) - .15, max(xs) + .15, min(ys) - .2, max(ys) + .2


def _can_theo_truoc(m, P: np.ndarray, truoc: dict) -> np.ndarray:
    """Xoay/lật chất cho phần chung (theo atom map) trùng hướng chất vẽ trước; góc làm tròn bội 30°."""
    chung = [(a.GetIdx(), truoc[a.GetAtomMapNum()]) for a in m.GetAtoms() if a.GetAtomMapNum() in truoc]
    if len(chung) < 3:
        return P
    A = np.array([P[i] for i, _ in chung]); B = np.array([q for _, q in chung])
    A0, B0 = A - A.mean(0), B - B.mean(0)
    if not np.any(A0) or not np.any(B0):
        return P
    tot = None
    for lat in (1, -1):
        A1 = A0 * np.array([lat, 1])
        g = math.atan2(float((A1[:, 0] * B0[:, 1] - A1[:, 1] * B0[:, 0]).sum()), float((A1 * B0).sum()))
        if not math.isfinite(g):
            continue
        g = round(math.degrees(g) / 30) * 30
        c, s = math.cos(math.radians(g)), math.sin(math.radians(g))
        R = np.array([[c, -s], [s, c]])
        d = float(((A1 @ R.T - B0) ** 2).sum())
        if tot is None or d < tot[0]:
            tot = (d, lat, R)
    if tot is None:
        return P
    _, lat, R = tot
    return ((P - P.mean(0)) * np.array([lat, 1])) @ R.T


def _phia_trong2(P: np.ndarray, i: int, ke: list[int], tranh: list[np.ndarray] = ()) -> np.ndarray:
    """Hướng trống quanh nguyên tử i (xa các nét, xa các điểm cần tránh)."""
    if not ke:
        v = np.array([0, 1.0])
    else:
        v = _phia_trong(P, i, ke)
    tot, tot_d = v, -1e9
    for g in range(0, 360, 30):
        u = np.array([math.cos(math.radians(g)), math.sin(math.radians(g))])
        d = 2 * float(u @ v) - sum(max(0.0, float(u @ ((t - P[i]) / (np.linalg.norm(t - P[i]) + 1e-9)))) for t in tranh)
        for j in ke:
            w = P[j] - P[i]
            d -= 3 * max(0.0, float(u @ w / (np.linalg.norm(w) + 1e-9)) - .5)
        if d > tot_d:
            tot, tot_d = u, d
    return tot


# ============================================================================ dựng sơ đồ cả cơ chế (18/11: kiểu đáp án bài thi)
# Như bản demo đã duyệt (nguyen_mau/co_che_3d/sn2_sn1.html, chế độ Giấy):
#   - mỗi bước là MỘT phương trình: các chất tham gia bước (bỏ chất đứng ngoài như Br⁻) → sản phẩm thật của bước (áp mũi tên
#     bằng co_che.ap_mui_ten; hai Cl• giống nhau gộp thành "2 Cl•");
#   - các bước gom thành GIAI ĐOẠN có tiêu đề in nghiêng gạch chân lượn sóng ("Cơ chế SN1:", "Khơi mào:"…); nhánh sai đứng
#     trước, nền nhấn, mũi tên gạch có dấu ✕ ("Không theo SN2 (C bậc III bị cản trở không gian):");
#   - chất mạch hở nhỏ vẽ công thức THU GỌN (thu_gon.py), có cặp e tự do trên dị tố tham gia mũi tên, điện tích trong vòng tròn;
#   - mũi tên cong xuất phát từ cặp e (hoặc chấm e độc thân); "chậm"/"nhanh" trên mũi tên phản ứng; phông có chân.
from .. import co_che as cc_mod  # noqa: E402

RONG_HANG = 24.0         # quá bề rộng này thì xuống hàng (đơn vị độ dài liên kết)
TIEU_DE_LOAI = {
    "SN1": "Cơ chế SN1:", "SN2": "Cơ chế SN2:", "E1": "Cơ chế E1:", "E2": "Cơ chế E2:", "E1CB": "Cơ chế E1cB:",
    "AE": "Cơ chế cộng electrophin (AE):", "AN": "Cơ chế cộng nucleophin (AN):", "AR": "Cơ chế cộng gốc tự do (AR):",
    "SEAR": "Cơ chế thế electrophin vào vòng thơm (SEAr):", "SNAR": "Cơ chế thế nucleophin vào vòng thơm (SNAr):",
    "THE_ACYL": "Cơ chế thế nucleophin vào nhóm acyl:", "GOC_TU_DO": "Cơ chế gốc tự do:", "SR": "Cơ chế thế gốc tự do (SR):",
    "PERICYCLIC": "Cơ chế pericyclic:", "CHUYEN_VI": "Cơ chế (có chuyển vị):",
}
LY_DO_CHAN = {"can_tro_khong_gian": "bị cản trở không gian", "khong_anti_periplanar": "H không ở vị trí anti so với nhóm rời đi"}


def _so_e(m):
    return len(m["tu"]["nguyen_tu"]) if isinstance(m["tu"]["nguyen_tu"], list) else 1


def _maps(s: str) -> set[int]:
    return {int(x) for x in re.findall(r":(\d+)\]", s)}


def _maps_mt(mts: list, chi_day_du: bool = False, chi_nguon: bool = False) -> set[int]:
    ra = set()
    for x in mts:
        if chi_day_du and x.get("kieu", "day_du") == "nua_dau":
            continue
        for d in (("tu",) if chi_nguon else ("tu", "den")):
            v = x[d]["nguyen_tu"]
            ra |= {int(z) for z in (v if isinstance(v, list) else [v])}
    return ra


def _gop(ds: list[str]) -> list[tuple[str, int]]:
    """Gộp chất trùng (bỏ map): Cl• + Cl• → 2 Cl•."""
    ra, khoa = [], []
    for s in ds:
        try:
            m = _mol(s)
            for a in m.GetAtoms():
                a.SetAtomMapNum(0)
            k = Chem.MolToSmiles(m)
        except Exception:  # noqa: BLE001
            k = s
        if k in khoa:
            ra[khoa.index(k)][1] += 1
        else:
            khoa.append(k)
            ra.append([s, 1])
    return [(s, n) for s, n in ra]


def _chuyen_h(b: dict) -> list[tuple[int, int]]:
    ra = []
    for x in b.get("chuyen_h") or []:
        try:
            ra.append((int(x["tu"]), int(x["den"])))
        except (KeyError, TypeError, ValueError):
            continue
    return ra


def _phuong_trinh(b: dict, sau: list[str]) -> tuple[list[str], list[tuple[str, int]]]:
    """Một bước → (chất tham gia, [(sản phẩm, hệ số)]). Sản phẩm tính bằng cách áp mũi tên (đúng cả chất phụ như HCl)."""
    mts = b.get("mui_ten") or []
    ch = _chuyen_h(b)
    dung = _maps_mt(mts) | {v for c in ch for v in c}
    if not dung:
        return list(b["chat"]), _gop(list(sau))
    trai = [s for s in b["chat"] if _maps(s) & dung] or list(b["chat"])
    try:
        rw = cc_mod.ap_mui_ten(cc_mod.tu_smiles(trai), [cc_mod.MuiTen.tu_dict(x) for x in mts], chuyen_h=ch)
        phai = cc_mod.cac_manh_smiles(rw)
    except Exception:  # noqa: BLE001
        cu = set().union(*(_maps(s) for s in trai))
        phai = [s for s in sau if _maps(s) & cu] or list(sau)
    return trai, _gop(phai)


def _co_goc(ds: list[str]) -> bool:
    for s in ds:
        try:
            if any(tg.so_goc(a) for a in _mol(s).GetAtoms()):
                return True
        except Exception:  # noqa: BLE001
            continue
    return False


def _chon_thu_gon(co_che: dict, tt: list[list[str]]) -> bool:
    """Kiểu SGK (thu gọn) trừ khi là bài kiểu HSG: có nhóm viết tắt (R, CO₂Me) hoặc khung nhiều vòng → khung xương."""
    k = co_che.get("kieu_ve")
    if k in ("thu_gon", "khung"):
        return k == "thu_gon"
    if co_che.get("nhan_hien_thi"):
        return False
    for ds in tt:
        for s in ds:
            try:
                m = _mol(s)
            except Exception:  # noqa: BLE001
                continue
            if any(a.GetAtomicNum() == 0 for a in m.GetAtoms()) or m.GetRingInfo().NumRings() >= 2:
                return False
    return True


def _tieu_de_chinh(co_che: dict) -> str:
    if co_che.get("tieu_de_giay"):
        return str(co_che["tieu_de_giay"])
    m = re.search(r"→\s*([A-Z][A-Za-z0-9]{0,3})\s*$", str(co_che.get("ten") or ""))
    if m:
        return f"Cơ chế hình thành {m.group(1)}:"
    return TIEU_DE_LOAI.get(str(co_che.get("loai") or "").upper(), "Cơ chế phản ứng:")


def _tieu_de_sai(ns: dict, trai: list[str]) -> str:
    if ns.get("tieu_de"):
        return str(ns["tieu_de"])
    ten = str(ns.get("ten") or "Nhánh sai")
    kieu = ns.get("kieu_chan", "")
    ly = LY_DO_CHAN.get(kieu, "")
    dich = ns.get("dich") or []
    try:
        mol = cc_mod.tu_smiles(trai)
        bd = cc_mod.ban_do(mol)
        if kieu == "can_tro_khong_gian" and len(dich) > 1 and int(dich[1]) in bd:
            n_c = sum(1 for n in mol.GetAtomWithIdx(bd[int(dich[1])]).GetNeighbors() if n.GetAtomicNum() == 6)
            if n_c >= 2:
                ly = f"C bậc {'III' if n_c >= 3 else 'II'} bị cản trở không gian"
        if kieu == "khong_anti_periplanar":
            for x in ns.get("mui_ten") or []:
                if x["tu"]["loai"] == "lien_ket" and x["den"]["loai"] == "nguyen_tu" and int(x["den"]["nguyen_tu"]) in bd:
                    ly = f"H không ở vị trí anti so với {mol.GetAtomWithIdx(bd[int(x['den']['nguyen_tu'])]).GetSymbol()}"
    except Exception:  # noqa: BLE001
        pass
    m = re.match(r"(.+?)\s+bị chặn", ten)
    dau = f"Không theo {m.group(1)}" if m else ten
    return f"{dau} ({ly}):" if ly else f"{dau}:"


def _sd_rong() -> dict:
    return {"nt": {}, "lk": [], "goc": [], "dt": [], "dt_vong": [], "cap_e": [], "mt": [], "pu": [], "chu": [], "ten": []}


def _ghi_chat(sd: dict, c: Chat, dx: float, dy: float, tt: str, cap_maps: set[int], huong: dict,
              cap_vt: dict, goc_vt: dict) -> None:
    """Ghi một chất vào sơ đồ: nút, nét, cặp e, e độc thân, điện tích (vòng tròn + kiểu cũ)."""
    P = c.P + (dx, dy)
    m = c.m
    ke_hien = lambda i: [n.GetIdx() for n in m.GetAtomWithIdx(i).GetNeighbors() if c.hien(n.GetIdx())]
    net = []
    for b in m.GetBonds():
        i, j = b.GetBeginAtomIdx(), b.GetEndAtomIdx()
        if not (c.hien(i) and c.hien(j)):
            continue
        net.append((P[i], P[j]))
        bac = {Chem.BondType.DOUBLE: 2, Chem.BondType.TRIPLE: 3}.get(b.GetBondType(), 1)
        ls = c.lap_the.get((i, j)) or c.lap_the.get((j, i))
        if ls:
            a0, a1 = (i, j) if (i, j) in c.lap_the else (j, i)
            sd["lk"].append([f"{tt}{a0}", f"{tt}{a1}", ls])
            continue
        e = [f"{tt}{i}", f"{tt}{j}"]
        if (min(i, j), max(i, j)) in c.sau:
            e.append("sau")
        elif bac > 1:
            e.append(bac)
            if b.IsInRing():               # nét đôi thứ hai phía trong vòng
                vong = min((r for r in m.GetRingInfo().AtomRings() if i in r and j in r), key=len)
                e.append([round(float(z), 3) for z in P[list(vong)].mean(0)])
        sd["lk"].append(e)
    tranh = []
    for a in m.GetAtoms():
        i = a.GetIdx()
        if not c.hien(i):
            continue
        sd["nt"][f"{tt}{i}"] = [round(float(P[i][0]), 3), round(float(P[i][1]), 3)] + ([c.nhan[i]] if i in c.nhan else [])
    # cặp electron tự do: dị tố có nhãn, tham gia mũi tên đầy đủ của bước (hoặc khai báo cap_e)
    for a in m.GetAtoms():
        i, mp = a.GetIdx(), a.GetAtomMapNum()
        if not c.hien(i) or i not in c.nhan or a.GetAtomicNum() not in tg.DI_TO_CAP or not mp or mp not in cap_maps:
            continue
        if tg.so_goc(a):
            continue                                   # gốc tự do: chỉ vẽ chấm e độc thân như SGK
        ke = ke_hien(i)
        t = tg.bo_dien_tich(c.nhan[i])
        can = tg.can_nhan(t, len(ke), float(P[i][0]), float(P[ke[0]][0]) if len(ke) == 1 else None)
        h = huong.get(mp)
        cap = tg.cho_cap_e(P, i, ke, t, a.GetSymbol(), can, tg.so_cap(a), None if h is None else h - P[i])
        for x, y, g in cap:
            sd["cap_e"].append([x, y, g])
            cap_vt.setdefault(mp, []).append((x, y, g))
            tranh.append((x, y))
    # electron độc thân
    for a in m.GetAtoms():
        i = a.GetIdx()
        if not c.hien(i) or not tg.so_goc(a):
            continue
        ke = ke_hien(i)
        mp = a.GetAtomMapNum()
        tr = [np.array(huong[mp]) for _ in (0,)] if mp in huong else []
        v = _phia_trong2(P, i, ke, [P[i] - (t - P[i]) for t in tr])
        r = .45 if i in c.nhan else .3
        if i in c.nhan and abs(v[0]) > .6:
            r = len(tg.bo_dien_tich(c.nhan[i])) * .17 + .25
        q = [round(float(z), 3) for z in P[i] + v * r]
        sd["goc"].append(q)
        goc_vt.setdefault(mp, []).append(tuple(q))
        tranh.append(tuple(q))
    # điện tích: vòng tròn ⊕/⊖ (mới) + kiểu cũ (nhãn có ⁺⁻, "dt" cho C không nhãn) để app cũ vẫn đọc được
    for a in m.GetAtoms():
        i, q = a.GetIdx(), a.GetFormalCharge()
        if not c.hien(i) or not q:
            continue
        ke = ke_hien(i)
        t = c.nhan.get(i, "")
        can = tg.can_nhan(tg.bo_dien_tich(t), len(ke), float(P[i][0]), float(P[ke[0]][0]) if len(ke) == 1 else None)
        khac = [tuple(P[j]) for j in c.nhan if j != i and c.hien(j)]
        x, y = tg.cho_dien_tich(P, i, ke, t, can, net, tranh + khac)
        sd["dt_vong"].append([x, y, ("+" if q > 0 else "−") if abs(q) == 1 else f"{abs(q)}{'+' if q > 0 else '−'}"])
        tranh.append((x, y))
        if i not in c.nhan:
            v = _phia_trong2(P, i, ke)
            sd["dt"].append([round(float(P[i][0] + v[0] * .38), 3), round(float(P[i][1] + v[1] * .38), 3), DIEN_TICH.get(q, "")])


def _do_rong_pu(p: dict) -> float:
    return max(1.6, len(p.get("tren") or "") * .26 + .5, len(p.get("duoi") or "") * .26 + .5)


def _ve_phuong_trinh(pt: dict, chats_trai: list, chats_phai: list, nhan_map: dict, so: int) -> list[dict]:
    """Một phương trình → các hàng (sơ đồ cục bộ, y quanh 0). Mũi tên cong của bước vẽ trên vế trái."""
    hang = []

    def hang_moi():
        h = {"sd": _sd_rong(), "x": .6}
        hang.append(h)
        return h

    h = hang_moi()
    vt: dict[int, tuple] = {}
    dat_trai = []
    for j, c in enumerate(chats_trai):
        x0, x1, y0, y1 = c.khung()
        if j:
            if pt.get("gach"):
                h["x"] += .9
            else:
                h["sd"]["chu"].append([round(h["x"] + .1, 3), 0.0, "+"])
                h["x"] += .55
        dx, dy = h["x"] - x0, -(y0 + y1) / 2
        dat_trai.append((c, dx, dy, f"p{so}t{j}_"))
        for a in c.m.GetAtoms():
            if a.GetAtomMapNum() and c.hien(a.GetIdx()):
                vt[a.GetAtomMapNum()] = (c.P[a.GetIdx()] + (dx, dy), a.GetIdx() in c.nhan, c, a.GetIdx(), dx, dy)
        h["x"] += x1 - x0 + .35
    # hướng tấn công của từng nguồn cặp e (để cặp e đặt về phía đích)
    huong = {}
    for x in pt["mt"]:
        if x["tu"]["loai"] in ("cap_e", "nguyen_tu"):
            a = x["tu"]["nguyen_tu"]
            a = a[0] if isinstance(a, list) else a
            d = x["den"]["nguyen_tu"]
            d = [z for z in (d if isinstance(d, list) else [d]) if z != a]
            if d and d[-1] in vt:
                huong[int(a)] = vt[d[-1]][0]
    cap_vt, goc_vt = {}, {}
    for c, dx, dy, tt in dat_trai:
        _ghi_chat(h["sd"], c, dx, dy, tt, pt["cap"], huong, cap_vt, goc_vt)
    # mũi tên phản ứng + vế phải
    if not pt.get("gach"):
        p = {}
        if pt.get("tren"):
            p["tren"] = pt["tren"] if len(pt["tren"]) <= 22 else pt["tren"][:21] + "…"
        if pt.get("toc"):
            p["tren" if "tren" not in p else "duoi"] = pt["toc"]
        if pt.get("thuan_nghich"):
            p["thuan_nghich"] = True
        dai = _do_rong_pu(p)
        p.update({"x1": round(h["x"], 3), "x2": round(h["x"] + dai, 3), "y": 0.0})
        h["sd"]["pu"].append(p)
        h["x"] += dai + .5
        rong_phai = sum(c.khung()[1] - c.khung()[0] + .9 + (.45 if n > 1 else 0) for c, n in chats_phai)
        hp = h if h["x"] + rong_phai <= RONG_HANG + 1 else hang_moi()
        if hp is not h:
            hp["x"] = 2.4
        for j, (c, n) in enumerate(chats_phai):
            x0, x1, y0, y1 = c.khung()
            if j:
                hp["sd"]["chu"].append([round(hp["x"] + .1, 3), 0.0, "+"])
                hp["x"] += .55
            if n > 1:
                hp["sd"]["chu"].append([round(hp["x"] + .15, 3), 0.0, str(n)])
                hp["x"] += .45
            dx, dy = hp["x"] - x0, -(y0 + y1) / 2
            _ghi_chat(hp["sd"], c, dx, dy, f"p{so}p{j}_", pt["cap"], {}, {}, {})
            hp["x"] += x1 - x0 + .35
    # mũi tên cong (trên vế trái)
    sd = h["sd"]
    hop_chu = list(_hop_nhan(sd).values())
    for p in sd["pu"]:
        xm = (p["x1"] + p["x2"]) / 2
        for t, dyy in ((p.get("tren"), .45), (p.get("duoi"), -.55)):
            if t:
                w = len(t) * .13 + .1
                hop_chu.append((xm - w, xm + w, p["y"] + dyy - .2, p["y"] + dyy + .2))
    for x0, y0, _ in sd["chu"]:
        hop_chu.append((x0 - .2, x0 + .2, y0 - .2, y0 + .2))
    net = [(np.array(sd["nt"][l[0]][:2]), np.array(sd["nt"][l[1]][:2])) for l in sd["lk"]]
    tam = np.mean([v[0] for v in vt.values()], axis=0) if vt else np.zeros(2)
    cheo_xong = False
    for x in pt["mt"]:
        ve = _mui_ten(x, vt, tam, net, hop_chu, sd["mt"], cap_vt, goc_vt)
        if not ve:
            continue
        if pt.get("gach"):
            ve["gach"] = True
            nguon = x["tu"]["nguyen_tu"]
            nguon = nguon[0] if isinstance(nguon, list) else nguon
            if not cheo_xong and (pt.get("cheo") is None or int(nguon) == pt["cheo"]):
                ve["cheo"] = True
                cheo_xong = True
        sd["mt"].append(ve)
    return hang


def _khung_sd(sd: dict) -> tuple[float, float, float, float]:
    xs, ys = [], []
    for v in sd["nt"].values():
        xs.append(v[0]); ys.append(v[1])
    for x, y, *_ in sd["goc"] + sd["dt_vong"] + sd["cap_e"] + sd["chu"]:
        xs.append(x); ys.append(y)
    for h in _hop_nhan(sd).values():
        xs += [h[0], h[1]]; ys += [h[2], h[3]]
    for p in sd["pu"]:
        xs += [p["x1"], p["x2"]]
        ys += [p["y"] + (.75 if p.get("tren") else .2), p["y"] - (.8 if p.get("duoi") else .2)]
    for m in sd["mt"]:
        for w in range(11):
            x, y = _bezier(m, w / 10)
            xs.append(x); ys.append(y)
    if not xs:
        return 0, 0, 0, 0
    return min(xs), max(xs), min(ys) - .3, max(ys) + .3


def _doi(sd: dict, dy: float) -> None:
    for v in sd["nt"].values():
        v[1] = round(v[1] + dy, 3)
    for k in ("goc", "dt", "dt_vong", "cap_e", "chu", "ten"):
        for v in sd[k]:
            v[1] = round(v[1] + dy, 3)
    for p in sd["pu"]:
        p["y"] = round(p["y"] + dy, 3)
    for m in sd["mt"]:
        for k in ("tu", "qua", "den"):
            m[k][1] = round(m[k][1] + dy, 3)


def tao_so_do(co_che: dict) -> dict:
    """co_che.json → sơ đồ giấy (.giay.json, hợp đồng HOP_DONG_APP.md mục 2 + mục "18/11 – giấy kiểu bài thi")."""
    nhan_map = {int(x["map"]): x["nhan"] for x in co_che.get("nhan_hien_thi", [])}
    cac_buoc = co_che["buoc"]
    tt = [b["chat"] for b in cac_buoc] + [co_che["san_pham"]]
    thu_gon = _chon_thu_gon(co_che, tt)
    co_cham = len(cac_buoc) > 1 and any(b.get("buoc_cham") for b in cac_buoc)

    # 1. phương trình: nhánh sai trước, rồi từng bước
    pts, gd = [], []
    for j, ns in enumerate(co_che.get("nhanh_sai") or []):
        k = int(ns.get("tu_buoc", 1)) - 1
        if not 0 <= k < len(cac_buoc) or not ns.get("mui_ten"):
            continue
        mts = ns["mui_ten"]
        dung = _maps_mt(mts)
        trai = list(ns.get("them") or []) + [s for s in cac_buoc[k]["chat"] if _maps(s) & dung]
        dich = ns.get("dich") or []
        pts.append({"trai": trai, "phai": [], "mt": mts, "gach": True, "cheo": int(dich[0]) if dich else None,
                    "giu": dung, "cap": _maps_mt(mts, True), "buoc": k})
        gd.append({"tieu_de": _tieu_de_sai(ns, trai), "nen": "sai", "pt": [len(pts) - 1], "buoc": [], "nhanh_sai": j})
    dau_chinh = len(pts)
    for k, b in enumerate(cac_buoc):
        trai, phai = _phuong_trinh(b, tt[k + 1])
        mts = b.get("mui_ten") or []
        chm = {v for c in _chuyen_h(b) for v in c}
        tren = b.get("dieu_kien") or (co_che.get("dieu_kien", "") if k == 0 else "")
        if not tren and chm and not mts:
            tren = "chuyển H⁺"
        toc = "chậm" if co_cham and b.get("buoc_cham") else "nhanh" if co_cham else ""
        pts.append({"trai": trai, "phai": phai, "mt": mts, "gach": False, "giu": _maps_mt(mts) | chm,
                    "cap": _maps_mt(mts, True) | {int(x) for x in b.get("cap_e") or []} | chm, "buoc": k,
                    "tren": tren, "toc": toc, "thuan_nghich": bool(b.get("thuan_nghich"))})
    if not cac_buoc:                                     # một chất đứng riêng (so_do_mot_chat)
        pts.append({"trai": list(co_che["san_pham"]), "phai": [], "mt": [], "gach": True, "giu": set(), "cap": set(), "buoc": None})
        gd.append({"tieu_de": "", "pt": [0], "buoc": []})
    # 2. giai đoạn của cơ chế chính: chuỗi gốc tự do → Khơi mào / Phát triển mạch / Tắt mạch; còn lại một giai đoạn
    if cac_buoc:
        nhom = []
        for i in range(dau_chinh, len(pts)):
            g0, g1 = _co_goc(pts[i]["trai"]), _co_goc([s for s, _ in pts[i]["phai"]])
            nhom.append("Khơi mào:" if not g0 and g1 else "Tắt mạch:" if g0 and not g1 else "Phát triển mạch:" if g0 else None)
        chuoi = str(co_che.get("loai", "")).upper() in ("GOC_TU_DO", "SR") and nhom and nhom[0] == "Khơi mào:" and \
            len(pts[dau_chinh]["trai"]) == 1 and None not in nhom
        if chuoi:
            for i, t in enumerate(nhom):
                if i and t == nhom[i - 1]:
                    gd[-1]["pt"].append(dau_chinh + i)
                    gd[-1]["buoc"].append(pts[dau_chinh + i]["buoc"])
                else:
                    gd.append({"tieu_de": t, "pt": [dau_chinh + i], "buoc": [pts[dau_chinh + i]["buoc"]]})
        else:
            gd.append({"tieu_de": _tieu_de_chinh(co_che), "pt": list(range(dau_chinh, len(pts))),
                       "buoc": list(range(len(cac_buoc)))})
            if dau_chinh:
                gd[-1]["nen"] = "chon"

    # 3. bày chất theo thứ tự đọc (chất sau giữ hướng chất trước theo atom map) và xếp hàng
    truoc: dict[int, np.ndarray] = {}

    def chat(smi, giu):
        c = Chat(smi, nhan_map, truoc, thu_gon, frozenset(giu))
        truoc.update({a.GetAtomMapNum(): c.P[a.GetIdx()].copy() for a in c.m.GetAtoms() if a.GetAtomMapNum()})
        return c

    sd = _sd_rong()
    y_tren = 0.0
    giai_doan = []
    for g in gd:
        g_top = y_tren
        if g.get("tieu_de"):
            tieu = {"tieu_de": g["tieu_de"], "x": .3, "y": round(y_tren - .45, 3)}
            y_tren -= 1.05
        else:
            tieu = None
        for i in g["pt"]:
            pt = pts[i]
            ct = [chat(s, pt["giu"]) for s in pt["trai"]]
            cp = [(chat(s, ()), n) for s, n in pt["phai"]]        # vế phải: H gộp vào nhãn (CH₃–C⁺H–CH₃, H₂O)
            for h in _ve_phuong_trinh(pt, ct, cp, nhan_map, i):
                x0, x1, y0, y1 = _khung_sd(h["sd"])
                _doi(h["sd"], y_tren - y1)
                y_tren -= (y1 - y0) + .35
                for k in sd:
                    if isinstance(sd[k], dict):
                        sd[k].update(h["sd"][k])
                    else:
                        sd[k] += h["sd"][k]
        if tieu is not None:
            e = {"tieu_de": tieu["tieu_de"], "xy": [tieu["x"], tieu["y"]], "y0": round(y_tren + .1, 3), "y1": round(g_top, 3),
                 "buoc": [b for b in g["buoc"] if b is not None]}
            if g.get("nen"):
                e["nen"] = g["nen"]
            if "nhanh_sai" in g:
                e["nhanh_sai"] = g["nhanh_sai"]
            giai_doan.append(e)
        y_tren -= .45

    # 4. khung bao, giai đoạn trải hết bề ngang
    x0, x1, y0, y1 = _khung_sd(sd)
    for e in giai_doan:
        x1 = max(x1, e["xy"][0] + len(e["tieu_de"]) * .2)
        y1 = max(y1, e["y1"])
    sd["khung"] = [round(min(x0, 0) - .5, 2), round(x1 + .8, 2), round(y0 - .6, 2), round(y1 + .3, 2)]
    for e in giai_doan:
        e["khung"] = [round(sd["khung"][0] + .15, 2), round(sd["khung"][1] - .15, 2), e.pop("y0"), e.pop("y1")]
    if giai_doan:
        sd["giai_doan"] = giai_doan
    sd["phong"] = "co_chan"
    sd["kieu_cong_thuc"] = "thu_gon" if thu_gon else "khung"
    for k in ("goc", "dt", "dt_vong", "cap_e", "ten", "chu"):
        if not sd[k]:
            del sd[k]
    return sd


def _mui_ten(mt: dict, vt: dict, tam: np.ndarray, net: list, hop_chu: list, da_ve: list,
             cap_vt: dict | None = None, goc_vt: dict | None = None) -> dict | None:
    """Một mũi tên cong: điểm đầu ở cặp e đã vẽ (hướng về đích), chấm e độc thân, hoặc giữa liên kết; điểm cuối dừng trước
    nguyên tử đích / giữa liên kết mới; điểm điều khiển chọn trong nhiều phương án (hai phía × bốn độ cong) để ít cắt nét,
    không đè chữ, không sát mũi tên khác."""
    lay = lambda x: [int(z) for z in x] if isinstance(x, list) else [int(x)]
    tu_l, tu = mt["tu"]["loai"], lay(mt["tu"]["nguyen_tu"])
    de_l, de = mt["den"]["loai"], lay(mt["den"]["nguyen_tu"])
    if any(m not in vt for m in tu + de):
        return None
    diem = lambda m: vt[m][0]
    ban_kinh = lambda m: (len(vt[m][2].nhan.get(vt[m][3], "")) * .12 + .22) if vt[m][1] else .12
    if tu_l in ("cap_e", "nguyen_tu"):
        a = tu[0]
        dich = diem(de[-1] if de[-1] != a else de[0])
        ud = dich - diem(a)
        ud = ud / (np.linalg.norm(ud) + 1e-9)
        nguon = ((goc_vt or {}).get(a) if mt.get("kieu") == "nua_dau" else None) or (cap_vt or {}).get(a) or (goc_vt or {}).get(a)
        if nguon:
            def huong_cap(q):
                v = np.array(q[:2]) - diem(a)
                return v / (np.linalg.norm(v) + 1e-9)
            q = max(nguon, key=lambda q: float(huong_cap(q) @ ud))
            A = np.array(q[:2]) + huong_cap(q) * .13
        else:
            c = vt[a][2]
            P = c.P + (vt[a][4], vt[a][5])
            ke = [n.GetIdx() for n in c.m.GetAtomWithIdx(vt[a][3]).GetNeighbors() if c.hien(n.GetIdx())]
            v = _phia_trong2(P, vt[a][3], ke, [dich])
            A = diem(a) + v * (ban_kinh(a) + .12)
    else:
        A = (diem(tu[0]) + diem(tu[1])) / 2
    tach = tu_l == "lien_ket" and de_l == "nguyen_tu" and de[0] in tu      # liên kết đứt, cặp e về nguyên tử
    if de_l == "nguyen_tu":
        c0 = de[0]
        u = A - diem(c0)
        B = diem(c0) + u / (np.linalg.norm(u) + 1e-9) * (ban_kinh(c0) + .1)
    else:
        x, y = de
        chung = set(tu) & {x, y}
        if chung:                                   # liên kết mới từ nguyên tử nguồn: đầu mũi tên nghiêng về nguyên tử kia
            goc_nt = next(iter(chung))
            khac = y if goc_nt == x else x
            B = diem(goc_nt) * .4 + diem(khac) * .6
        else:
            B = (diem(x) + diem(y)) / 2
    d = B - A
    L = float(np.linalg.norm(d))
    if L < 1e-6:
        return None
    n = np.array([-d[1], d[0]]) / L
    g = (A + B) / 2
    tot, tot_d = None, 1e9
    A0, B0 = A, B
    for s in (1, -1):
        if tach and mt.get("kieu") == "nua_dau" and (n * s)[1] < -1e-6:
            continue                                # phân cắt đồng li: hai mũi tên móc câu cùng toả lên phía trên
        if tach:                                    # như đáp án: từ giữa nét, vòng lên, chạm mép trên của nhãn nguyên tử
            c0 = de[0]
            r = ban_kinh(c0) + .1
            g0 = math.atan2(*(A0 - diem(c0))[::-1])
            huong_b = [np.array([math.cos(g0 + k * math.radians(65)), math.sin(g0 + k * math.radians(65))]) for k in (1, -1)]
            B = diem(c0) + max(huong_b, key=lambda v: float(v @ (n * s))) * max(r, .38)
            A = A0 + n * s * .08
        else:
            A, B = A0, B0
        g = (A + B) / 2
        for f in (.35, .55, .8, 1.1):
            h = max(.55, f * L)
            C = g + n * s * h
            m = {"tu": A, "qua": C, "den": B}
            phat = .15 * f + (0 if float(np.dot(n * s, g - tam)) < 0 else .6)
            truoc = A
            for q in range(1, 31):
                p = np.array(_bezier(m, q / 30))
                xa = math.dist(p, A) > .35 and math.dist(p, B) > .3
                for a0, b0 in net:
                    if xa and _giao(truoc, p, a0, b0):
                        phat += 3
                for hp in hop_chu:
                    if xa and _trong(p, hp):
                        phat += 4
                for m2 in da_ve:
                    if xa and any(math.dist(p, _bezier(m2, w / 12)) < .2 for w in range(2, 11)):
                        phat += 1
                truoc = p
            if phat < tot_d:
                tot, tot_d = (A, C, B), phat
    tron = lambda v: [round(float(z), 3) for z in v]
    A, C, B = tot
    return {"tu": tron(A), "qua": tron(C), "den": tron(B), "kieu": mt.get("kieu", "day_du")}


def so_do_mot_chat(smiles: str, nhan_map: dict | None = None) -> dict:
    """Sơ đồ giấy của một chất (để xem/chấm riêng)."""
    sd = tao_so_do({"buoc": [], "san_pham": [smiles], "nhan_hien_thi": [{"map": k, "nhan": v} for k, v in (nhan_map or {}).items()]})
    return sd


if __name__ == "__main__":
    p = Path(sys.argv[1])
    sd = tao_so_do(json.loads(p.read_text(encoding="utf-8")))
    ra = p.with_suffix(".giay.json") if p.name != "co_che.json" else p.with_name(p.parent.name + ".giay.json")
    ra.write_text(json.dumps(sd, ensure_ascii=False), encoding="utf-8")
    print(f"{len(sd['nt'])} nút, {len(sd['lk'])} liên kết, {len(sd['mt'])} mũi tên → {ra}")
