"""Công thức THU GỌN kiểu SGK / đáp án bài thi (18/11): mọi C ghi nhãn (CH₃, CH₂, CH, C), mạch chính nằm ngang, nhánh
dựng đứng lên/xuống (như H₃C–C(CH₃)₂–Br trong bản demo đã duyệt nguyen_mau/co_che_3d/sn2_sn1.html).

Dùng cho chất mạch hở, không có nhóm viết tắt (R, CO₂Me…), ≤ 12 nguyên tử nặng. Chất có vòng vẫn vẽ khung xương (ve_theo_mau).

Quy tắc bày:
  1. Mạch chính = đường dài nhất (ưu tiên đầu mạch là dị tố, tránh H và =O ở đầu mạch), dị tố/halogen đặt bên phải.
  2. Nhánh của nguyên tử trong mạch: lên, xuống (đầu mạch: thêm hướng ra ngoài); nhánh con đi thẳng tiếp, nhánh cháu rẽ ngang.
  3. Hai nguyên tử cùng tham gia mũi tên ở hai C kề nhau (Br và H trong E2) đặt khác phía (anti) như đáp án.
  4. Khoảng cách theo bề rộng nhãn để nét luôn còn ≥ 0,6 đơn vị.
  5. H tường minh (có map) không tham gia mũi tên của bước → gộp vào nhãn (CH₃, OH); H của dị tố giữa mạch → nét H dựng đứng.
Toạ độ trả về theo độ dài liên kết = 1, trục y hướng lên, tâm ở gốc.
"""
from __future__ import annotations

import itertools
import math

import numpy as np
from rdkit import Chem

BANG = Chem.GetPeriodicTable()
KY_TU = .17
DIEN_TICH = {1: "⁺", -1: "⁻", 2: "²⁺", -2: "²⁻", 3: "³⁺", -3: "³⁻"}
PHI_KIM = {1, 5, 6, 7, 8, 9, 14, 15, 16, 17, 34, 35, 53}
DI_TO_CAP = {7, 8, 9, 15, 16, 17, 34, 35, 53}       # dị tố hiện cặp electron tự do


def nua_rong(t: str) -> float:
    """Nửa bề rộng nhãn (đơn vị độ dài liên kết) – như nuaRong của ve_giay.js."""
    return len(t) * .17 + .1 if t else 0.0


def bo_dien_tich(t: str) -> str:
    return t.rstrip("⁺⁻²³")


# ------------------------------------------------------------------ electron
def e_khong_lien_ket(a) -> int | None:
    """Số electron không liên kết (đếm như co_che.ap_mui_ten): e hoá trị − điện tích − tổng bậc liên kết − số H gắn."""
    z = a.GetAtomicNum()
    if z not in PHI_KIM:
        return None
    bac = sum(b.GetBondTypeAsDouble() for b in a.GetBonds())
    return int(round(BANG.GetNOuterElecs(z) - a.GetFormalCharge() - bac - a.GetTotalNumHs()))


def so_goc(a) -> int:
    nb = e_khong_lien_ket(a)
    return nb % 2 if nb and nb > 0 else 0


def so_cap(a) -> int:
    nb = e_khong_lien_ket(a)
    return min(4, nb // 2) if nb and nb > 0 else 0


# ------------------------------------------------------------------ chọn chế độ
def dung_duoc(m) -> bool:
    """Chất vẽ thu gọn được: mạch hở, không có nguyên tử giả (R, CO₂Me…), ≤ 12 nguyên tử nặng."""
    if any(a.GetAtomicNum() == 0 for a in m.GetAtoms()):
        return False
    if m.GetRingInfo().NumRings():
        return False
    if any(a.GetChiralTag() != Chem.ChiralType.CHI_UNSPECIFIED for a in m.GetAtoms()):
        return False                                   # tâm lập thể: vẽ khung có nêm
    return sum(1 for a in m.GetAtoms() if a.GetAtomicNum() > 1) <= 12


def tach_h_di_to(m):
    """Dị tố giữa mạch (≥ 2 nguyên tử nặng kề) còn H ẩn → thêm nguyên tử H tường minh (vẽ nét H dựng đứng: –O⁺(H)–)."""
    can = [a.GetIdx() for a in m.GetAtoms() if a.GetAtomicNum() in (7, 8, 16) and a.GetTotalNumHs() > 0
           and sum(1 for n in a.GetNeighbors() if n.GetAtomicNum() > 1) >= 2]
    if not can:
        return m
    rw = Chem.RWMol(m)
    for i in can:
        a = rw.GetAtomWithIdx(i)
        h = a.GetTotalNumHs()
        a.SetNumExplicitHs(0)
        a.SetNoImplicit(True)
        for _ in range(h):
            j = rw.AddAtom(Chem.Atom(1))
            rw.GetAtomWithIdx(j).SetNoImplicit(True)
            rw.AddBond(i, j, Chem.BondType.SINGLE)
    m2 = rw.GetMol()
    m2.UpdatePropertyCache(strict=False)
    return m2


def h_an(m, giu: set[int]) -> set[int]:
    """H tường minh có map nhưng không tham gia mũi tên của bước → ẩn (gộp vào nhãn của nguyên tử mang nó)."""
    if m.GetNumAtoms() <= 1:
        return set()
    ra = set()
    for a in m.GetAtoms():
        if a.GetAtomicNum() != 1 or a.GetDegree() != 1 or a.GetFormalCharge():
            continue
        if a.GetAtomMapNum() and a.GetAtomMapNum() in giu:
            continue
        if not a.GetAtomMapNum() and a.GetNeighbors()[0].GetAtomicNum() in (7, 8, 16) and \
                sum(1 for n in a.GetNeighbors()[0].GetNeighbors() if n.GetAtomicNum() > 1) >= 2:
            continue                                   # H do tach_h_di_to thêm: giữ để vẽ dựng đứng
        if a.GetNeighbors()[0].GetAtomicNum() in (1, 9, 17, 35, 53):
            continue                                   # H–H, H–Cl, H–Br: giữ nét như SGK
        ra.add(a.GetIdx())
    return ra


def so_h(a, an: set[int]) -> int:
    return a.GetTotalNumHs() + sum(1 for n in a.GetNeighbors() if n.GetIdx() in an)


def nhan(a, an: set[int]) -> str:
    """Nhãn thu gọn (chưa đổi chỉ số dưới, còn điện tích): CH3, CH2, C⁺, OH⁻, Br, H."""
    h = so_h(a, an)
    hh = "" if h == 0 else "H" if h == 1 else f"H{h}"
    return a.GetSymbol() + hh + DIEN_TICH.get(a.GetFormalCharge(), "")


# ------------------------------------------------------------------ bày
def _cat_ngang(t: str, bac: int) -> float:
    """Đoạn nét bị chữ che theo phương ngang (như hàm cat của ve_giay.js)."""
    if not t:
        return 0.0
    if len(t) > 1 and bac == 1:
        return KY_TU + .1
    return nua_rong(t) + .02


def _uu_tien_dau(m, i: int) -> int:
    z = m.GetAtomWithIdx(i).GetAtomicNum()
    return -1 if z == 1 else 2 if z not in (6, 1) else 1


def bay(m, an: set[int], uu_tien: set[int], truoc: dict | None = None) -> np.ndarray:
    """Toạ độ thu gọn (độ dài liên kết = 1). uu_tien: map tham gia mũi tên (đặt anti); truoc: map → toạ độ chất trước."""
    n = m.GetNumAtoms()
    P = np.zeros((n, 2))
    hien = [i for i in range(n) if i not in an]
    ke = {i: [x.GetIdx() for x in m.GetAtomWithIdx(i).GetNeighbors() if x.GetIdx() not in an] for i in hien}
    t = {i: nhan(m.GetAtomWithIdx(i), an) for i in hien}
    if len(hien) <= 1:
        return P
    mp = {i: m.GetAtomWithIdx(i).GetAtomMapNum() for i in range(n)}

    def gap(a, b, h):
        if h[1] == 0:
            return max(1.0, _cat_ngang(t[a], len(ke[a])) + _cat_ngang(t[b], len(ke[b])) + .6)
        return 1.25

    def duong(u):
        cha, hang = {u: None}, [u]
        while hang:
            x = hang.pop(0)
            for y in ke[x]:
                if y not in cha:
                    cha[y] = x
                    hang.append(y)
        return cha

    def diem_duong(p):
        s = 10 * len(p)
        for e in {p[0], p[-1]}:
            a = m.GetAtomWithIdx(e)
            if a.GetAtomicNum() == 1 and len(hien) > 2:
                s -= 6
            if a.GetAtomicNum() not in (1, 6):
                s += 2                                   # dị tố / halogen ở đầu mạch (H₃C–C–Br như đáp án)
            if a.GetAtomicNum() == 8 and len(p) > 1 and any(b.GetBondTypeAsDouble() == 2 for b in a.GetBonds()):
                s -= 3
        return s

    cac_duong = []
    for u in hien:
        cha = duong(u)
        for v in hien:
            if v not in cha:
                continue
            p, x = [], v
            while x is not None:
                p.append(x)
                x = cha[x]
            cac_duong.append(p[::-1])
    tot_d = max(diem_duong(p) for p in cac_duong)
    ung_vien = [p for p in cac_duong if diem_duong(p) == tot_d]

    def dung(p):
        Q = {p[0]: np.zeros(2)}
        for a, b in zip(p, p[1:]):
            Q[b] = Q[a] + np.array([gap(a, b, (1, 0)), 0.0])
        trong = set(p)
        phia: dict[int, int] = {}                      # chỉ số trong mạch → phía (+1 lên / −1 xuống) của nhánh ưu tiên
        phat = 0.0

        def dat_cay(j, cha, h):
            Q[j] = Q[cha] + np.array(h, float) * gap(cha, j, h)
            con = [c for c in ke[j] if c != cha]
            vuong = [(1, 0), (-1, 0)] if h[0] == 0 else [(0, 1), (0, -1)]
            for c, d in zip(con, [h] + vuong):
                dat_cay(c, j, d)

        for k, i in enumerate(p):
            nh = [j for j in ke[i] if j not in trong]
            if not nh:
                continue
            huong = [(0, 1), (0, -1)] + ([(-1, 0)] if k == 0 else []) + ([(1, 0)] if k == len(p) - 1 else [])
            tot = None
            for gan in itertools.permutations(huong, len(nh)):
                d = 0.0
                for j, h in zip(nh, gan):
                    if h[1] and mp[j] in uu_tien:
                        for kk in (k - 1, k + 1):
                            if phia.get(kk) == h[1]:
                                d += 1                  # hai nguyên tử phản ứng ở hai C kề cùng phía → nên anti
                    if h[1] < 0 and m.GetAtomWithIdx(j).GetAtomicNum() > 1 and \
                            any(m.GetAtomWithIdx(x).GetAtomicNum() == 1 for x, g in zip(nh, gan) if g[1] > 0):
                        d += .2                          # nhóm nặng lên trên, H xuống dưới
                    d += .01 * (h[1] < 0) + .02 * (h[0] != 0)
                if tot is None or d < tot[0]:
                    tot = (d, gan)
            phat += tot[0]
            for j, h in zip(nh, tot[1]):
                if h[1] and mp[j] in uu_tien:
                    phia[k] = h[1]
                dat_cay(j, i, h)
        X = np.array([Q[i] for i in hien])
        for a_, b_ in itertools.combinations(range(len(hien)), 2):
            if np.linalg.norm(X[a_] - X[b_]) < .8:
                phat += 10                               # hai nguyên tử chồng nhau
        return Q, phat

    chon = None
    for p in ung_vien:
        Q, phat = dung(p)
        X = np.array([Q[i] for i in hien])
        X = X - X.mean(0)
        Q = {i: X[k] for k, i in enumerate(hien)}
        for sx, sy in ((1, 1), (-1, 1), (1, -1), (-1, -1)):
            R = {i: q * (sx, sy) for i, q in Q.items()}
            d = phat * 5
            chung = [i for i in hien if truoc and mp[i] in truoc]
            if len(chung) >= 2:
                A = np.array([R[i] for i in chung]); B = np.array([truoc[mp[i]] for i in chung])
                d += float(((A - A.mean(0) - (B - B.mean(0))) ** 2).sum())
            else:
                # không có chất trước: đầu mạch quan trọng (dị tố) bên phải, nhánh lên trên
                trai, phai = min(hien, key=lambda i: R[i][0]), max(hien, key=lambda i: R[i][0])
                d += .5 * (_uu_tien_dau(m, trai) > _uu_tien_dau(m, phai)) + .3 * (sx < 0) + .2 * (sy < 0)
            if chon is None or d < chon[0] - 1e-9:
                chon = (d, R)
    R = chon[1]
    for i in hien:
        P[i] = R[i]
    for i in an:
        P[i] = P[m.GetAtomWithIdx(i).GetNeighbors()[0].GetIdx()]
    return P


# ------------------------------------------------------------------ cặp electron, điện tích vòng tròn
NHO = set("₀₁₂₃₄₅₆₇₈₉⁺⁻²³")


def rong_chu(t: str) -> float:
    """Bề rộng thật ước lượng của chữ: ký tự thường 0,34, chỉ số dưới/mũ 0,2."""
    return sum(.2 if ch in NHO else .34 for ch in t)


def vung_chu(x: float, y: float, t: str, can: str) -> tuple[float, float]:
    """Mép trái/phải của chữ (không tính lề) theo cách căn của ve_giay.js."""
    w = rong_chu(t)
    if can == "trai":
        x0 = x - KY_TU
    elif can == "phai":
        x0 = x + KY_TU - w
    else:
        x0 = x - w / 2
    return x0, x0 + w


def can_nhan(t: str, bac: int, x: float, x_ke: float | None) -> str:
    if len(t) > 1 and bac == 1 and x_ke is not None:
        return "phai" if x_ke > x + .3 else "trai"
    return "giua"


def cho_cap_e(P: np.ndarray, i: int, ke: list[int], t: str, ky_hieu: str, can: str, so: int,
              huong_uu: np.ndarray | None = None) -> list[tuple[float, float, float]]:
    """Vị trí các cặp e quanh ký hiệu nguyên tố trong nhãn: [(x, y, góc độ hướng ra)]. Chỉ dùng 4 hướng trên/dưới/trái/phải
    như bài thi; bỏ hướng có nét liên kết hoặc có chữ (H của OH…)."""
    if so <= 0:
        return []
    x, y = float(P[i][0]), float(P[i][1])
    tt = bo_dien_tich(t)
    vt = tt.find(ky_hieu) if ky_hieu in tt else 0
    x0, _ = vung_chu(x, y, tt, can)
    cx = x0 + rong_chu(tt[:vt]) + len(ky_hieu) * .17
    nua = len(ky_hieu) * .17
    o = [(cx, y + .4, 90.0), (cx, y - .4, 270.0)]
    if vt == 0:
        o.append((cx - nua - .13, y, 180.0))
    if vt + len(ky_hieu) >= len(tt):
        o.append((cx + nua + .13, y, 0.0))
    con = []
    for px, py, g in o:
        u = np.array([math.cos(math.radians(g)), math.sin(math.radians(g))])
        if any(float(u @ ((P[j] - P[i]) / (np.linalg.norm(P[j] - P[i]) + 1e-9))) > .64 for j in ke):
            continue
        con.append((px, py, g))
    if huong_uu is not None and np.linalg.norm(huong_uu) > 1e-6:
        h = huong_uu / np.linalg.norm(huong_uu)
        con.sort(key=lambda c: -(math.cos(math.radians(c[2])) * h[0] + math.sin(math.radians(c[2])) * h[1]))
    return [(round(px, 3), round(py, 3), g) for px, py, g in con[:so]]


def _kc_doan(p, a, b) -> float:
    p, a, b = np.asarray(p, float), np.asarray(a, float), np.asarray(b, float)
    d = b - a
    L = float(d @ d)
    if L < 1e-12:
        return float(np.linalg.norm(p - a))
    s = max(0.0, min(1.0, float((p - a) @ d) / L))
    return float(np.linalg.norm(p - (a + s * d)))


def cho_dien_tich(P: np.ndarray, i: int, ke: list[int], t: str, can: str, net: list, diem_tranh: list) -> tuple[float, float]:
    """Tâm vòng tròn điện tích ⊕/⊖: góc trên-phải của nhãn (như đáp án), đổi góc khác nếu vướng nét, cặp e, chữ."""
    x, y = float(P[i][0]), float(P[i][1])
    if t:
        x0, x1 = vung_chu(x, y, bo_dien_tich(t), can)
        ung = [(x1 + .3, y + .5), (x0 - .3, y + .5), (x1 + .3, y - .5), (x0 - .3, y - .5), ((x0 + x1) / 2, y + .7)]
    else:
        ung = [(x + .45 * math.cos(math.radians(g)), y + .45 * math.sin(math.radians(g))) for g in (45, 135, -45, -135, 90, -90, 0, 180)]
    tot, tot_d = ung[0], -1e9
    for k, c in enumerate(ung):
        d = min([_kc_doan(c, a, b) for a, b in net] + [math.dist(c, q) for q in diem_tranh] + [9.0])
        d = min(d, .45) - .02 * k                      # đủ xa (≥ 0,5) thì giữ thứ tự ưu tiên
        if d > tot_d:
            tot, tot_d = c, d
    return round(tot[0], 3), round(tot[1], 3)
