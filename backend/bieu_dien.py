"""Các dạng biểu diễn lập thể cho mục Cơ chế: Newman, ghế cyclohexan, Fischer, Haworth (ngoài dạng nêm mặc định).

Mỗi dạng dựng từ cấu dạng 3D thật của phân tử (RDKit ETKDG + MMFF) rồi chiếu xuống mặt phẳng; sau đó ĐỌC NGƯỢC lập thể
từ chính hình vẽ (toạ độ giả-3D suy từ quy ước của dạng đó: Fischer ngang = hướng về người xem, Newman trước/sau, ghế/Haworth
trên/dưới) và so với nhãn R/S của SMILES gốc – sai thì thử bản đối xứng, vẫn sai thì không dùng dạng đó (ve_co_che giữ dạng nêm).

Kết quả `BieuDien`: toạ độ (đơn vị RDKit, liên kết ≈ 1.5) cho MỌI nguyên tử của mảnh đã thêm H, các nét/vòng tròn/chữ cần vẽ,
các nguyên tử bị ẩn (gộp vào chữ nhóm thế) và nguyên tử có chữ (để mũi tên dừng trước chữ).
"""
from __future__ import annotations

import math
from dataclasses import dataclass, field

import numpy as np
from rdkit import Chem
from rdkit.Chem import AllChem, rdMolTransforms

L = 1.5
DANG = ("net", "newman", "ghe", "fischer", "haworth")
TEN_DANG = {"net": "Nêm (mặc định)", "newman": "Newman", "ghe": "Ghế cyclohexan", "fischer": "Fischer", "haworth": "Haworth"}


@dataclass
class BieuDien:
    dang: str
    mol: Chem.Mol                                        # mảnh đã thêm H (H mới không có map)
    toa_do: dict[int, tuple[float, float]]               # chỉ số nguyên tử → (x, y)
    net: list[tuple[tuple[float, float], tuple[float, float], str]] = field(default_factory=list)  # (đầu, cuối, "thuong"|"dam")
    vong_tron: list[tuple[float, float, float]] = field(default_factory=list)
    chu: list[tuple[float, float, str]] = field(default_factory=list)          # chữ nhóm thế (căn giữa)
    an: set[int] = field(default_factory=set)            # nguyên tử gộp vào chữ của nhóm thế
    co_chu: set[int] = field(default_factory=set)        # nguyên tử mang chữ


# ---------------------------------------------------------------- tiện ích
def _cip(mol) -> dict[int, str]:
    m = Chem.Mol(mol)
    try:
        Chem.SanitizeMol(m)
        from rdkit.Chem import rdCIPLabeler
        rdCIPLabeler.AssignCIPLabels(m)
    except Exception:  # noqa: BLE001
        return {}
    return {a.GetIdx(): a.GetProp("_CIPCode") for a in m.GetAtoms() if a.HasProp("_CIPCode")}


def _doc_nguoc(mol_h, toa_do_3d: dict[int, tuple[float, float, float]], tam: list[int]) -> bool:
    """Lập thể đọc từ toạ độ giả-3D của hình có khớp R/S gốc ở các tâm `tam` không."""
    goc = _cip(mol_h)
    if not tam or not all(t in goc for t in tam):
        return False
    m = Chem.Mol(mol_h)
    conf = Chem.Conformer(m.GetNumAtoms())
    conf.Set3D(True)
    for i in range(m.GetNumAtoms()):
        x, y, z = toa_do_3d.get(i, (0.0, 0.0, 0.0))
        conf.SetAtomPosition(i, (float(x), float(y), float(z)))
    m.RemoveAllConformers()
    m.AddConformer(conf, assignId=True)
    for a in m.GetAtoms():
        a.SetChiralTag(Chem.ChiralType.CHI_UNSPECIFIED)
    try:
        Chem.AssignStereochemistryFrom3D(m)
    except Exception:  # noqa: BLE001
        return False
    moi = _cip(m)
    return all(moi.get(t) == goc[t] for t in tam)


def _tam_lap_the(mol) -> list[int]:
    return [a.GetIdx() for a in mol.GetAtoms()
            if a.GetChiralTag() in (Chem.ChiralType.CHI_TETRAHEDRAL_CW, Chem.ChiralType.CHI_TETRAHEDRAL_CCW)]


def _them_h_3d(frag) -> Chem.Mol | None:
    """Mảnh (giữ map, lập thể) + H tường minh + cấu dạng 3D (ETKDG, MMFF)."""
    m = Chem.Mol(frag)
    try:
        Chem.SanitizeMol(m)
    except Exception:  # noqa: BLE001
        return None
    mh = Chem.AddHs(m)
    p = AllChem.ETKDGv3()
    p.randomSeed = 20261011
    if AllChem.EmbedMolecule(mh, p) != 0:
        return None
    try:
        AllChem.MMFFOptimizeMolecule(mh, maxIters=400)
    except Exception:  # noqa: BLE001
        pass
    return mh


def nhan_nhom(mol, i: int, cha: int) -> tuple[str, set[int]]:
    """Chữ thu gọn của nhóm thế bắt đầu ở nguyên tử i (đi ra xa nguyên tử cha) và tập nguyên tử của nhóm."""
    nhom, st = set(), [i]
    while st:
        u = st.pop()
        if u in nhom:
            continue
        nhom.add(u)
        st.extend(nb.GetIdx() for nb in mol.GetAtomWithIdx(u).GetNeighbors() if nb.GetIdx() != cha and nb.GetIdx() not in nhom)
    a = mol.GetAtomWithIdx(i)
    nang = [x for x in nhom if mol.GetAtomWithIdx(x).GetAtomicNum() > 1]
    if a.GetAtomicNum() == 1:
        return "H", nhom
    h = sum(1 for nb in a.GetNeighbors() if nb.GetAtomicNum() == 1) + a.GetNumExplicitHs()
    q = a.GetFormalCharge()
    dau = "" if not q else ("⁺" if q > 0 else "⁻")
    sh = "" if h == 0 else ("H" if h == 1 else f"H{h}")
    if len(nang) == 1:
        return f"{a.GetSymbol()}{sh}{dau}", nhom
    # nhóm quen thuộc
    sub = Chem.RWMol()
    ds = sorted(nang)
    for x in ds:
        at = mol.GetAtomWithIdx(x)
        na = Chem.Atom(at.GetAtomicNum())
        na.SetFormalCharge(at.GetFormalCharge())
        sub.AddAtom(na)
    for b in mol.GetBonds():
        u, v = b.GetBeginAtomIdx(), b.GetEndAtomIdx()
        if u in ds and v in ds:
            sub.AddBond(ds.index(u), ds.index(v), b.GetBondType())
    try:
        Chem.SanitizeMol(sub)
        s = Chem.MolToSmiles(sub)
    except Exception:  # noqa: BLE001
        s = ""
    quen = {"CO": "CH2OH", "C=O": "CHO", "O=CO": "COOH", "CC": "C2H5", "COC": "CH2OCH3", "OC": "OCH3",
            "c1ccccc1": "Ph", "CC(C)C": "iBu", "CC(C)(C)C": "tBu", "CC(=O)O": "COOH", "N": "NH2", "C#N": "CN",
            "O=[N+][O-]": "NO2", "CCC": "C3H7", "CC(C)": "iPr", "CC=O": "CH2CHO", "C(=O)OC": "COOCH3"}
    if s in quen and quen[s]:
        return quen[s], nhom
    # công thức Hill của nhóm
    dem: dict[str, int] = {}
    for x in nhom:
        sym = mol.GetAtomWithIdx(x).GetSymbol()
        dem[sym] = dem.get(sym, 0) + 1
        dem["H"] = dem.get("H", 0) + mol.GetAtomWithIdx(x).GetNumExplicitHs()
    thu_tu = ["C", "H"] + sorted(k for k in dem if k not in ("C", "H"))
    return "".join(f"{k}{dem[k] if dem[k] > 1 else ''}" for k in thu_tu if dem.get(k)), nhom


def _goc_mat_phang(p: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
    """(tâm, pháp tuyến) mặt phẳng trung bình qua các điểm."""
    tam = p.mean(axis=0)
    _, _, vt = np.linalg.svd(p - tam)
    return tam, vt[2]


# ---------------------------------------------------------------- Newman
def newman(frag, a_map: int, b_map: int, xoay_doi: tuple[int, int, int, int] | None = None) -> BieuDien | None:
    """Chiếu Newman dọc liên kết a→b (a phía trước). xoay_doi=(H, b, a, X) bằng map: đặt H–Cb–Ca–X đối lệch 180° (anti-periplanar, E2)."""
    mh = _them_h_3d(frag)
    if mh is None:
        return None
    bd = {at.GetAtomMapNum(): at.GetIdx() for at in mh.GetAtoms() if at.GetAtomMapNum()}
    if a_map not in bd or b_map not in bd:
        return None
    ia, ib = bd[a_map], bd[b_map]
    if mh.GetBondBetweenAtoms(ia, ib) is None or mh.GetBondBetweenAtoms(ia, ib).IsInRing():
        return None
    conf = mh.GetConformer()
    if xoay_doi and all(m in bd for m in xoay_doi):
        try:
            rdMolTransforms.SetDihedralDeg(conf, bd[xoay_doi[0]], bd[xoay_doi[1]], bd[xoay_doi[2]], bd[xoay_doi[3]], 180.0)
        except Exception:  # noqa: BLE001
            pass
    p = conf.GetPositions()
    w = p[ia] - p[ib]
    w /= np.linalg.norm(w)
    truoc = [nb.GetIdx() for nb in mh.GetAtomWithIdx(ia).GetNeighbors() if nb.GetIdx() != ib]
    sau = [nb.GetIdx() for nb in mh.GetAtomWithIdx(ib).GetNeighbors() if nb.GetIdx() != ia]
    # trục u: hình chiếu của nhóm thế trước đầu tiên (hướng lên); v = w × u
    for dao in (False, True):
        r0 = p[truoc[0]] - p[ia]
        u = r0 - np.dot(r0, w) * w
        u /= np.linalg.norm(u)
        v = np.cross(w, u)
        if dao:
            v = -v
        R_TRUOC, R_VONG, R_SAU = 1.55, 0.62, 1.55
        toa, toa3 = {}, {}
        toa[ia] = (0.0, 0.0)
        toa[ib] = (0.0, 0.0)
        toa3[ia] = (0.0, 0.0, 0.5)
        toa3[ib] = (0.0, 0.0, -0.5)
        net, chu, an, co_chu = [], [], set(), set()
        for nhom_ds, r, z in ((truoc, R_TRUOC, 0.85), (sau, R_SAU, -0.85)):
            goc_nt = ia if z > 0 else ib
            for j in nhom_ds:
                d = p[j] - p[goc_nt]
                x, y = float(np.dot(d, v)), float(np.dot(d, u))
                n = math.hypot(x, y) or 1.0
                x, y = x / n, y / n
                if z > 0:
                    net.append(((0.0, 0.0), (x * r * 0.86, y * r * 0.86), "thuong"))
                else:
                    net.append(((x * R_VONG, y * R_VONG), (x * r * 0.86, y * r * 0.86), "thuong"))
                nhan, nhom = nhan_nhom(mh, j, goc_nt)
                toa[j] = (x * r, y * r)
                toa3[j] = (x * r, y * r, z)
                chu.append((x * r, y * r, nhan))
                co_chu.add(j)
                for k in nhom - {j}:
                    toa[k] = (x * r, y * r)
                    toa3[k] = (x * r * 1.1, y * r * 1.1, z * 1.1)
                    an.add(k)
        if _doc_nguoc(mh, toa3, [t for t in (ia, ib) if t in _tam_lap_the(mh)] or [ia]) or not [t for t in (ia, ib) if t in _tam_lap_the(mh)]:
            for i in range(mh.GetNumAtoms()):
                toa.setdefault(i, (0.0, 0.0))
            return BieuDien("newman", mh, toa, net, [(0.0, 0.0, R_VONG)], chu, an, co_chu)
    return None


# ---------------------------------------------------------------- ghế cyclohexan
def ghe(frag, giu: set[int] = frozenset()) -> BieuDien | None:
    """Vòng 6 cạnh no dạng ghế, nhóm thế trục (axial)/xích đạo (equatorial) theo cấu dạng 3D thật. giu: map cần hiện H."""
    mh = _them_h_3d(frag)
    if mh is None:
        return None
    vong = next((list(r) for r in mh.GetRingInfo().AtomRings()
                 if len(r) == 6 and all(not mh.GetAtomWithIdx(i).GetIsAromatic() and
                                        all(b.GetBondTypeAsDouble() == 1.0 for b in mh.GetAtomWithIdx(i).GetBonds() if b.IsInRing())
                                        for i in r)), None)
    if vong is None:
        return None
    p = mh.GetConformer().GetPositions()
    tam, n = _goc_mat_phang(p[vong])
    # quay: pháp tuyến → z, nguyên tử vòng đầu tiên → trục −x; rồi nghiêng 72° quanh x để thấy dạng ghế
    z = n / np.linalg.norm(n)
    x0 = p[vong[0]] - tam
    x0 = -(x0 - np.dot(x0, z) * z)
    x0 /= np.linalg.norm(x0)
    y0 = np.cross(z, x0)
    Rm = np.vstack([x0, y0, z])
    q = (p - tam) @ Rm.T
    t = math.radians(-72)
    nghieng = np.array([[1, 0, 0], [0, math.cos(t), -math.sin(t)], [0, math.sin(t), math.cos(t)]])
    q = q @ nghieng.T
    k = L / 1.53
    toa = {i: (float(q[i][0] * k), float(q[i][1] * k)) for i in range(mh.GetNumAtoms())}
    toa3 = {i: (float(q[i][0]), float(q[i][1]), float(q[i][2])) for i in range(mh.GetNumAtoms())}
    tam_lt = [i for i in _tam_lap_the(mh) if i in vong]
    if tam_lt and not _doc_nguoc(mh, toa3, tam_lt):
        return None  # phép quay không làm đổi lập thể – sai ở đây là lỗi dựng hình
    net, chu, an, co_chu = [], [], set(), set()
    for j in range(6):
        u, v = vong[j], vong[(j + 1) % 6]
        net.append((toa[u], toa[v], "thuong"))
    map_giu = set(giu)
    for i in vong:
        at = mh.GetAtomWithIdx(i)
        ngoai = [nb.GetIdx() for nb in at.GetNeighbors() if nb.GetIdx() not in vong]
        co_the = any(mh.GetAtomWithIdx(j).GetAtomicNum() > 1 for j in ngoai) or at.GetAtomMapNum() in map_giu
        for j in ngoai:
            nhan, nhom = nhan_nhom(mh, j, i)
            if nhan == "H" and not co_the:
                an.add(j)
                toa[j] = toa[i]
                continue
            dx, dy = toa[j][0] - toa[i][0], toa[j][1] - toa[i][1]
            dd = math.hypot(dx, dy) or 1.0
            dau = (toa[i][0] + dx / dd * L * 0.95, toa[i][1] + dy / dd * L * 0.95)
            net.append((toa[i], (toa[i][0] + dx / dd * L * 0.7, toa[i][1] + dy / dd * L * 0.7), "thuong"))
            toa[j] = dau
            chu.append((dau[0], dau[1], nhan))
            co_chu.add(j)
            for kk in nhom - {j}:
                toa[kk] = dau
                an.add(kk)
    for i in range(mh.GetNumAtoms()):
        if i not in vong and i not in co_chu:
            an.add(i)
    return BieuDien("ghe", mh, toa, net, [], chu, an, co_chu)


# ---------------------------------------------------------------- Fischer
def fischer(frag) -> BieuDien | None:
    """Mạch chứa các tâm lập thể dựng đứng (đầu bị oxi hoá nhiều hơn ở trên); nhóm ngang hướng về người xem."""
    m = Chem.Mol(frag)
    try:
        Chem.SanitizeMol(m)
    except Exception:  # noqa: BLE001
        return None
    mh = Chem.AddHs(m)
    tam = _tam_lap_the(mh)
    if not tam or any(mh.GetAtomWithIdx(i).IsInRing() for i in tam):
        return None
    # mạch C dài nhất đi qua mọi tâm
    c = [a.GetIdx() for a in mh.GetAtoms() if a.GetSymbol() == "C" and not a.IsInRing()]
    tot = None
    for u in c:
        for v in c:
            if u >= v:
                continue
            duong = list(Chem.GetShortestPath(mh, u, v))
            if all(t in duong for t in tam) and all(mh.GetAtomWithIdx(x).GetSymbol() == "C" for x in duong):
                if tot is None or len(duong) > len(tot):
                    tot = duong
    if tot is None:
        return None

    def oxi(i):
        return sum(1 for nb in mh.GetAtomWithIdx(i).GetNeighbors() if nb.GetSymbol() in ("O", "N"))
    if oxi(tot[-1]) > oxi(tot[0]):
        tot = tot[::-1]
    toa, toa3, net, chu, an, co_chu = {}, {}, [], [], set(), set()
    for k, i in enumerate(tot):
        toa[i] = (0.0, -k * L)
        toa3[i] = (0.0, -k * 1.0, 0.0)
    for k in range(len(tot) - 1):
        net.append((toa[tot[k]], toa[tot[k + 1]], "thuong"))
    # hai đầu mạch: chữ nhóm cuối (vd CHO, CH2OH)
    for dau, ke in ((tot[0], tot[1]), (tot[-1], tot[-2])):
        if dau not in tam:
            nhan, nhom = nhan_nhom(mh, dau, ke)
            chu.append((toa[dau][0], toa[dau][1], nhan))
            co_chu.add(dau)
            for kk in nhom - {dau}:
                toa[kk] = toa[dau]
                toa3[kk] = toa3[dau]
                an.add(kk)
    for k, i in enumerate(tot):
        ngoai = [nb.GetIdx() for nb in mh.GetAtomWithIdx(i).GetNeighbors() if nb.GetIdx() not in tot]
        if i in (tot[0], tot[-1]) and i not in tam:
            continue
        if len(ngoai) != 2:
            if len(ngoai) and i not in tam:  # C không phải tâm (vd CH2 giữa mạch): gộp vào nhãn
                for j in ngoai:
                    toa[j] = toa[i]
                    toa3[j] = toa3[i]
                    an.add(j)
            continue
        tren = tot[k - 1] if k > 0 else None
        duoi = tot[k + 1] if k + 1 < len(tot) else None
        for thu in (ngoai, ngoai[::-1]):
            tam3 = dict(toa3)
            y = toa3[i][1]
            if tren is not None:
                tam3[tren] = (0.0, y + 1.0, -0.7)
            if duoi is not None:
                tam3[duoi] = (0.0, y - 1.0, -0.7)
            tam3[thu[0]] = (-1.0, y, 0.7)
            tam3[thu[1]] = (1.0, y, 0.7)
            if _doc_nguoc(mh, tam3, [i]):
                ngoai = thu
                break
        else:
            return None
        for j, sx in zip(ngoai, (-1, 1)):
            nhan, nhom = nhan_nhom(mh, j, i)
            dau = (sx * L * 1.05, toa[i][1])
            net.append((toa[i], (sx * L * 0.62, toa[i][1]), "thuong"))
            toa[j] = dau
            toa3[j] = (sx * 1.0, toa3[i][1], 0.7)
            chu.append((dau[0], dau[1], nhan))
            co_chu.add(j)
            for kk in nhom - {j}:
                toa[kk] = dau
                an.add(kk)
    for i in range(mh.GetNumAtoms()):
        toa.setdefault(i, (0.0, 0.0))
    return BieuDien("fischer", mh, toa, net, [], chu, an, co_chu)


# ---------------------------------------------------------------- Haworth
_HAWORTH6 = [(1.05, 0.62), (1.95, 0.0), (1.05, -0.62), (-1.05, -0.62), (-1.95, 0.0), (-1.05, 0.62)]  # O, C1, C2, C3, C4, C5
_HAWORTH5 = [(0.0, 0.75), (1.55, 0.05), (0.95, -0.7), (-0.95, -0.7), (-1.55, 0.05)]                 # O, C1, C2, C3, C4


def haworth(frag) -> BieuDien | None:
    """Vòng đường (5/6 cạnh có 1 O): vòng phối cảnh (cạnh trước nét đậm), nhóm thế trên/dưới theo cấu dạng 3D."""
    mh = _them_h_3d(frag)
    if mh is None:
        return None
    vong = next((list(r) for r in mh.GetRingInfo().AtomRings()
                 if len(r) in (5, 6) and sum(mh.GetAtomWithIdx(i).GetSymbol() == "O" for i in r) == 1), None)
    if vong is None:
        return None
    o = next(i for i in vong if mh.GetAtomWithIdx(i).GetSymbol() == "O")
    j0 = vong.index(o)
    thu_tu = vong[j0:] + vong[:j0]
    mau = _HAWORTH6 if len(vong) == 6 else _HAWORTH5
    p = mh.GetConformer().GetPositions()
    _, n = _goc_mat_phang(p[vong])
    tam_lt = [i for i in _tam_lap_the(mh) if i in vong]
    for chieu in (thu_tu, [thu_tu[0]] + thu_tu[1:][::-1]):
        for dau_n in (1, -1):
            nn = n * dau_n
            toa, toa3 = {}, {}
            for i, (x, y) in zip(chieu, mau):
                toa[i] = (x * L / 1.1, y * L / 1.1)
                toa3[i] = (x, y, 0.0)
            len_xuong = {}
            for i in chieu:
                for nb in mh.GetAtomWithIdx(i).GetNeighbors():
                    j = nb.GetIdx()
                    if j in vong:
                        continue
                    len_xuong[j] = (i, 1 if float(np.dot(p[j] - p[i], nn)) > 0 else -1)
            for j, (i, sz) in len_xuong.items():
                toa3[j] = (toa3[i][0], toa3[i][1], 1.0 * sz)
            if tam_lt and not _doc_nguoc(mh, toa3, tam_lt):
                continue
            net, chu, an, co_chu = [], [], set(), set()
            k = len(chieu)
            for a in range(k):
                u, v = chieu[a], chieu[(a + 1) % k]
                truoc = toa[u][1] < 0.1 and toa[v][1] < 0.1  # cạnh phía trước (dưới hình) – nét đậm
                net.append((toa[u], toa[v], "dam" if truoc else "thuong"))
            for j, (i, sz) in len_xuong.items():
                nhan, nhom = nhan_nhom(mh, j, i)
                dau = (toa[i][0], toa[i][1] + sz * L * 0.95)
                net.append((toa[i], (toa[i][0], toa[i][1] + sz * L * 0.65), "thuong"))
                toa[j] = dau
                chu.append((dau[0], dau[1], nhan))
                co_chu.add(j)
                for kk in nhom - {j}:
                    toa[kk] = dau
                    an.add(kk)
            for i in range(mh.GetNumAtoms()):
                toa.setdefault(i, (0.0, 0.0))
            co_chu.add(o)
            chu.append((toa[o][0], toa[o][1], "O"))
            return BieuDien("haworth", mh, toa, net, [], chu, an, co_chu)
    return None


# ---------------------------------------------------------------- dạng nào dùng được cho một mảnh
def cac_dang(frag, mui_ten_maps: set[int] = frozenset()) -> list[str]:
    """Các dạng biểu diễn hợp với mảnh (luôn có "net")."""
    out = ["net"]
    m = Chem.Mol(frag)
    try:
        Chem.SanitizeMol(m)
    except Exception:  # noqa: BLE001
        return out
    ri = m.GetRingInfo()
    tam = _tam_lap_the(m)
    lk_cc = [b for b in m.GetBonds() if b.GetBondType() == Chem.BondType.SINGLE and not b.IsInRing()
             and b.GetBeginAtom().GetSymbol() == "C" and b.GetEndAtom().GetSymbol() == "C"
             and b.GetBeginAtom().GetDegree() > 1 and b.GetEndAtom().GetDegree() > 1
             and b.GetBeginAtom().GetHybridization() == Chem.HybridizationType.SP3
             and b.GetEndAtom().GetHybridization() == Chem.HybridizationType.SP3]
    if lk_cc and (tam or mui_ten_maps):
        out.append("newman")
    if any(len(r) == 6 and all(not m.GetAtomWithIdx(i).GetIsAromatic() for i in r) for r in ri.AtomRings()):
        out.append("ghe")
    if tam and not any(m.GetAtomWithIdx(i).IsInRing() for i in tam):
        out.append("fischer")
    if any(len(r) in (5, 6) and sum(m.GetAtomWithIdx(i).GetSymbol() == "O" for i in r) == 1 for r in ri.AtomRings()) and len(tam) >= 2:
        out.append("haworth")
    return out


def lien_ket_newman(frag, mui_ten_maps: set[int]) -> tuple[int, int] | None:
    """Chọn liên kết C–C để chiếu Newman: ưu tiên liên kết giữa hai nguyên tử tham gia phản ứng, rồi giữa hai tâm lập thể."""
    m = Chem.Mol(frag)
    try:
        Chem.SanitizeMol(m)
    except Exception:  # noqa: BLE001
        return None
    tam = set(_tam_lap_the(m))
    tot, diem_tot = None, -1
    for b in m.GetBonds():
        a, c = b.GetBeginAtom(), b.GetEndAtom()
        if b.GetBondType() != Chem.BondType.SINGLE or b.IsInRing() or a.GetSymbol() != "C" or c.GetSymbol() != "C":
            continue
        if a.GetDegree() < 2 or c.GetDegree() < 2 or not a.GetAtomMapNum() or not c.GetAtomMapNum():
            continue
        diem = 3 * ((a.GetAtomMapNum() in mui_ten_maps) + (c.GetAtomMapNum() in mui_ten_maps)) + (a.GetIdx() in tam) + (c.GetIdx() in tam)
        if diem > diem_tot:
            tot, diem_tot = (a.GetAtomMapNum(), c.GetAtomMapNum()), diem
    return tot
