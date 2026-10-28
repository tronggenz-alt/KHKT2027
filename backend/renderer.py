"""Bước 5 – Vẽ công thức cấu tạo bằng RDKit và xuất .mol/.sdf.

Kiểu công thức khung (skeletal): mạch C là các nét gạch đơn/đôi/ba xếp zigzag 120°, không ghi chữ C và H gắn C;
chỉ ghi nguyên tố khác C, H (OH, NH2, Br, O...). Nguyên tử có liên kết ba (hoặc hai liên kết đôi liền) thẳng hàng.
Mạch hở nằm ngang đọc từ trái sang theo công thức thu gọn; vòng đứng đỉnh nhọn lên trên, nhánh zigzag sang phải.
Nhóm nitro thu gọn thành NO2. Độ dài liên kết cố định để mọi chất cùng tỉ lệ.
"""
from __future__ import annotations

import io
import logging
import math
from collections import deque
from pathlib import Path

from PIL import Image, ImageChops
from rdkit import Chem, RDLogger
from rdkit.Chem import rdAbbreviations, rdDepictor
from rdkit.Chem.Draw import rdMolDraw2D

from .config import FONT_REGULAR

RDLogger.DisableLog("rdApp.*")
rdDepictor.SetPreferCoordGen(True)
log = logging.getLogger(__name__)

# Kích thước vẽ cố định cho mọi chất (ảnh đáp án rộng 1920 px), tỉ lệ gần chuẩn ACS 1996 của ChemDraw cho công thức khung
BOND_PX = 40           # độ dài liên kết (pixel)
CO_CHU_PX = 22         # cỡ chữ nhãn nguyên tử (pixel)
NET_PX = 1.6           # độ dày nét (pixel)
_LE = 14               # lề trắng sau khi cắt
_ABBR = rdAbbreviations.ParseAbbreviations(
    "NO2 [N+](=O)[O-] NO<sub>2</sub> O<sub>2</sub>N\nSO3H S(=O)(=O)[OH] SO<sub>3</sub>H HO<sub>3</sub>S"
)


def _font() -> str | None:
    for f in FONT_REGULAR:
        if Path(f).exists() and f.endswith(".ttf"):
            return f
    return None


def chuan_bi_mol(smiles: str, viet_tat: bool = True, cong_thuc: str = "", mau=None):
    """SMILES → mol đã có toạ độ 2D và nhãn kiểu SGK. Trả về None nếu SMILES sai.

    viet_tat=True: thu gọn nhóm NO2/SO3H thành nhãn (chỉ dùng để vẽ ảnh; CDXML/MOL cần đủ nguyên tử).
    cong_thuc: công thức thu gọn (vd CH3COOC2H5) – dùng để chọn chiều viết mạch giống SGK.
    mau: mol (đã chuẩn bị) của chất đứng trước trong chuỗi – lật hình cho phần khung chung nằm cùng hướng với chất trước.
    """
    mol = Chem.MolFromSmiles((smiles or "").strip())
    if mol is None or mol.GetNumAtoms() == 0:  # SMILES rỗng (vd chất giữ chỗ) → RDKit trả phân tử 0 nguyên tử
        return None
    if viet_tat:
        try:
            mol = rdAbbreviations.CondenseMolAbbreviations(mol, _ABBR, maxCoverage=0.6)
        except Exception:  # noqa: BLE001
            pass
    _toa_do_2d(mol)
    kieu_sgk = _xep_mach_zigzag(mol, cong_thuc) or _xep_vong_nhanh_zigzag(mol)
    if not kieu_sgk:
        _xoay_ngang(mol)
    _chuan_do_dai_lien_ket(mol)
    if mau is not None:
        # hình đã xếp chuẩn (mạch ngang đọc từ trái sang, vòng bên trái) chỉ được lật dọc để không đảo thứ tự viết
        can_theo_mau(mol, mau, lat_ngang=not kieu_sgk)
    return mol


def can_theo_mau(mol, mau, lat_ngang: bool = True) -> bool:
    """Lật hình mol (dọc; và ngang nếu lat_ngang) để phần khung chung lớn nhất (MCS) trùng hướng với hình mau.

    Trả về True nếu đã lật. Dùng để các chất trong một chuỗi được vẽ "theo cấu trúc chất ban đầu".
    """
    from rdkit.Chem import rdFMCS

    if mau is None or mol.GetNumAtoms() < 3 or mau.GetNumAtoms() < 3 or not mau.GetNumConformers():
        return False
    try:
        kq = rdFMCS.FindMCS([mau, mol], timeout=1, atomCompare=rdFMCS.AtomCompare.CompareElements,
                            bondCompare=rdFMCS.BondCompare.CompareAny, ringMatchesRingOnly=True, completeRingsOnly=True)
    except Exception:  # noqa: BLE001
        return False
    if kq.numAtoms < 3 or not kq.smartsString:
        return False
    q = Chem.MolFromSmarts(kq.smartsString)
    khop_mau = mau.GetSubstructMatch(q)
    khop_moi = mol.GetSubstructMatches(q, uniquify=False, maxMatches=48)
    if not khop_mau or not khop_moi:
        return False
    cm, cn = mau.GetConformer(), mol.GetConformer()

    def tam(pts):
        x = sum(p[0] for p in pts) / len(pts)
        y = sum(p[1] for p in pts) / len(pts)
        return [(p[0] - x, p[1] - y) for p in pts]

    Q = tam([(cm.GetAtomPosition(i).x, cm.GetAtomPosition(i).y) for i in khop_mau])
    phep = [(1, 1), (1, -1)] + ([(-1, 1), (-1, -1)] if lat_ngang else [])
    tot = None
    for khop in khop_moi:
        P = tam([(cn.GetAtomPosition(i).x, cn.GetAtomPosition(i).y) for i in khop])
        for sx, sy in phep:
            d = sum((sx * px - qx) ** 2 + (sy * py - qy) ** 2 for (px, py), (qx, qy) in zip(P, Q))
            # ưu tiên giữ nguyên (không lật) khi lệch như nhau
            if tot is None or d < tot[0] - 1e-6:
                tot = (d, sx, sy)
    _, sx, sy = tot
    if (sx, sy) == (1, 1):
        return False
    for i in range(mol.GetNumAtoms()):
        p = cn.GetAtomPosition(i)
        cn.SetAtomPosition(i, (sx * p.x, sy * p.y, 0.0))
    return True


def _chuan_do_dai_lien_ket(mol, dai: float = 1.5) -> None:
    """Đưa độ dài liên kết trung bình về 1.5 (CoordGen cho vòng ~1.0) để mọi chất cùng tỉ lệ."""
    import math

    if mol.GetNumBonds() == 0:
        return
    conf = mol.GetConformer()
    tong = 0.0
    for b in mol.GetBonds():
        a, c = conf.GetAtomPosition(b.GetBeginAtomIdx()), conf.GetAtomPosition(b.GetEndAtomIdx())
        tong += math.hypot(a.x - c.x, a.y - c.y)
    tb = tong / mol.GetNumBonds()
    if tb < 1e-6:
        return
    k = dai / tb
    for i in range(mol.GetNumAtoms()):
        p = conf.GetAtomPosition(i)
        conf.SetAtomPosition(i, (p.x * k, p.y * k, 0.0))


def _diem_khop(mol, mach: list[int], adj: dict, cong_thuc: str) -> int:
    """Độ dài tiền tố chung giữa chuỗi nhóm dọc mạch (CH3, C, O, ...) và công thức thu gọn."""
    import re as _re

    ct = _re.sub(r"[^A-Za-z0-9]", "", cong_thuc or "")
    if not ct:
        return 0
    trong_mach = set(mach)
    chu = ""
    for idx in mach:
        a = mol.GetAtomWithIdx(idx)
        h = a.GetTotalNumHs()
        chu += a.GetSymbol() + ("" if h == 0 else "H" if h == 1 else f"H{h}")
        for v in adj[idx]:  # nhánh (=O, –OH...) viết ngay sau nguyên tử mang nhánh
            if v not in trong_mach:
                b = mol.GetAtomWithIdx(v)
                hb = b.GetTotalNumHs()
                chu += b.GetSymbol() + ("" if hb == 0 else "H" if hb == 1 else f"H{hb}")
    n = 0
    for x, y in zip(chu, ct):
        if x != y:
            break
        n += 1
    return n


# ---------------------------------------------------------------- xếp hình kiểu công thức khung (zigzag 120°)
L_LK = 1.5  # độ dài liên kết trong toạ độ RDKit


def _vec(goc: float) -> tuple[float, float]:
    r = math.radians(goc)
    return math.cos(r), math.sin(r)


def _thang_hang(mol, idx: int) -> bool:
    """Nguyên tử có liên kết ba, hoặc hai liên kết đôi liền (allene, CO2) → hai liên kết của nó thẳng hàng (180°)."""
    a = mol.GetAtomWithIdx(idx)
    bac = [b.GetBondTypeAsDouble() for b in a.GetBonds()]
    return a.GetDegree() == 2 and (3.0 in bac or bac.count(2.0) == 2)


def _nhanh_doi(mol, idx: int) -> bool:
    """Nguyên tử khác C ở đầu mút nối bằng liên kết đôi/ba (=O, ≡N): nên làm nhánh, không làm đầu mạch chính."""
    a = mol.GetAtomWithIdx(idx)
    return a.GetDegree() == 1 and a.GetBonds()[0].GetBondTypeAsDouble() >= 2 and a.GetSymbol() != "C"


def _kich_thuoc(adj, u: int, cha: int, trong: set) -> int:
    seen, q = {u, cha}, [u]
    while q:
        x = q.pop()
        for y in adj[x]:
            if y in trong and y not in seen:
                seen.add(y)
                q.append(y)
    return len(seen) - 1


def _goc_khe_lon_nhat(goc_co: list[float], k: int) -> list[float]:
    """k hướng mới chia đều khe góc lớn nhất giữa các liên kết đã có (độ)."""
    if not goc_co:
        return [360.0 * j / k for j in range(k)]
    g = sorted(x % 360 for x in goc_co)
    khe = [((g[(i + 1) % len(g)] - g[i]) % 360 or 360, g[i]) for i in range(len(g))]
    rong, dau = max(khe)
    return [dau + rong * (j + 1) / (k + 1) for j in range(k)]


def _dat_cay(mol, adj, pos: dict, u: int, cha, goc: float, re: int, trong: set, uu_tien: set) -> None:
    """Đặt (đệ quy) các nguyên tử con của u theo zigzag. u nhận liên kết từ cha theo hướng goc (độ);
    re = ±1: phía rẽ của liên kết kế tiếp (đổi dấu sau mỗi lần rẽ → zigzag). uu_tien: nguyên tử mạch chính (đi trước)."""
    con = [v for v in adj[u] if v in trong and v not in pos]
    if not con:
        return
    con.sort(key=lambda v: (v not in uu_tien, -_kich_thuoc(adj, v, u, trong), v))
    co = [] if cha is None else [goc + 180]
    huong: list[float] = []
    if _thang_hang(mol, u) and cha is not None:
        huong = [goc]                                  # sp: đi thẳng
    elif cha is None:
        huong = [goc + 60 * re] + [goc + 60 * re + 120 * (j + 1) for j in range(len(con) - 1)]
    else:
        huong = [goc + 60 * re]                        # mạch tiếp tục zigzag
    if len(con) > len(huong):
        huong += _goc_khe_lon_nhat(co + huong, len(con) - len(huong))
    for v, a in zip(con, huong):
        x, y = pos[u]
        dx, dy = _vec(a)
        pos[v] = (x + dx * L_LK, y + dy * L_LK)
        lech = (a - goc + 540) % 360 - 180            # rẽ trái (+) hay phải (−) so với hướng tới
        re_moi = -1 if lech > 1e-6 else (1 if lech < -1e-6 else re)
        _dat_cay(mol, adj, pos, v, u, a, re_moi, trong, uu_tien)


def _khong_chong(mol, pos: dict) -> bool:
    lien_ket = {frozenset((b.GetBeginAtomIdx(), b.GetEndAtomIdx())) for b in mol.GetBonds()}
    ds = list(pos.items())
    for i in range(len(ds)):
        for j in range(i + 1, len(ds)):
            (a, p), (b, q) = ds[i], ds[j]
            if frozenset((a, b)) not in lien_ket and math.dist(p, q) < 0.62 * L_LK:
                return False
    return True


def _ghi_toa_do(mol, pos: dict) -> None:
    conf = mol.GetConformer()
    for i, (x, y) in pos.items():
        conf.SetAtomPosition(i, (x, y, 0.0))


def _xep_mach_zigzag(mol, cong_thuc: str = "") -> bool:
    """Phân tử không vòng: mạch dài nhất zigzag nằm ngang, đọc từ trái sang theo công thức thu gọn; nhánh chĩa ra khe lớn nhất.

    Trả về False nếu không áp dụng (có vòng, nhiều mảnh, 1 nguyên tử, quá lớn, bị chồng).
    """
    n = mol.GetNumAtoms()
    if n < 2 or n > 40 or mol.GetRingInfo().NumRings() or len(Chem.GetMolFrags(mol)) > 1:
        return False
    adj = {a.GetIdx(): [x.GetIdx() for x in a.GetNeighbors()] for a in mol.GetAtoms()}

    def xa_nhat(goc):
        prev, dist, q = {goc: None}, {goc: 0}, deque([goc])
        while q:
            u = q.popleft()
            for v in adj[u]:
                if v not in dist:
                    dist[v], prev[v] = dist[u] + 1, u
                    q.append(v)
        cuoi = max(dist, key=lambda k: (dist[k] - (0.5 if _nhanh_doi(mol, k) else 0), -k))
        return cuoi, prev

    a, _ = xa_nhat(next((i for i in range(n) if not _nhanh_doi(mol, i)), 0))
    b, prev = xa_nhat(a)
    mach = []
    while b is not None:
        mach.append(b)
        b = prev[b]
    # chiều viết: khớp công thức thu gọn (CH3COOC2H5 → CH3–C(=O)–O–...), nếu không thì đầu mạch bên trái là C
    xuoi, nguoc = _diem_khop(mol, mach, adj, cong_thuc), _diem_khop(mol, mach[::-1], adj, cong_thuc)
    if nguoc > xuoi or (nguoc == xuoi and mol.GetAtomWithIdx(mach[0]).GetSymbol() != "C"
                        and mol.GetAtomWithIdx(mach[-1]).GetSymbol() == "C"):
        mach.reverse()

    # toàn mạch thẳng hàng (C2H2, HC≡C–C≡CH...) → nằm ngang; còn lại zigzag ±30°
    thang = all(_thang_hang(mol, i) for i in mach[1:-1]) and len(mach) > 1 and any(
        mol.GetBondBetweenAtoms(mach[i], mach[i + 1]).GetBondTypeAsDouble() >= 2 for i in range(len(mach) - 1))
    pos = {mach[0]: (0.0, 0.0)}
    goc0 = 0.0 if thang else 30.0
    _dat_cay(mol, adj, pos, mach[0], None, goc0 - 60, 1, set(range(n)), set(mach))
    if len(pos) != n or not _khong_chong(mol, pos):
        return False
    _ghi_toa_do(mol, pos)
    return True


def _xep_vong_nhanh_zigzag(mol) -> bool:
    """Phân tử có vòng: giữ vòng như RDKit xếp, xoay để nhánh lớn nhất chĩa ra góc 30° (vòng đứng đỉnh nhọn lên trên),
    mọi nhánh không vòng nối vào vòng bằng một liên kết được vẽ lại zigzag ra ngoài – kiểu sách giáo khoa.

    Trả về False nếu không áp dụng (không có vòng, nhiều mảnh, xếp xong bị chồng).
    """
    ri = mol.GetRingInfo()
    n = mol.GetNumAtoms()
    if not ri.NumRings() or len(Chem.GetMolFrags(mol)) > 1 or n > 80:
        return False
    trong_vong = {i for r in ri.AtomRings() for i in r}
    adj = {a.GetIdx(): [x.GetIdx() for x in a.GetNeighbors()] for a in mol.GetAtoms()}

    # các nhánh không vòng nối với đúng một nguyên tử vòng
    da_xet: set[int] = set()
    nhanh: list[tuple[int, int, list[int]]] = []  # (nguyên tử vòng, đầu nhánh, các nguyên tử nhánh)
    for goc in range(n):
        if goc in trong_vong or goc in da_xet:
            continue
        cum, q = [], deque([goc])
        da_xet.add(goc)
        while q:
            u = q.popleft()
            cum.append(u)
            for v in adj[u]:
                if v not in trong_vong and v not in da_xet:
                    da_xet.add(v)
                    q.append(v)
        noi = [(u, v) for u in cum for v in adj[u] if v in trong_vong]
        if len(noi) == 1:
            nhanh.append((noi[0][1], noi[0][0], cum))

    conf = mol.GetConformer()
    p = {i: (conf.GetAtomPosition(i).x, conf.GetAtomPosition(i).y) for i in range(n)}
    dai = [math.dist(p[b.GetBeginAtomIdx()], p[b.GetEndAtomIdx()]) for b in mol.GetBonds()]
    k = L_LK / (sum(dai) / len(dai)) if dai and sum(dai) > 1e-6 else 1.0

    def tam_vong(i):
        vong = min((r for r in ri.AtomRings() if i in r), key=len)
        return sum(p[j][0] for j in vong) / len(vong), sum(p[j][1] for j in vong) / len(vong)

    # xoay: nguyên tử vòng mang nhánh lớn nhất có hướng xuyên tâm 30°; vòng không nhánh: một đỉnh hướng lên (90°)
    if nhanh:
        vong_noi = max(nhanh, key=lambda t: (len(t[2]), -t[0]))[0]
        dich = 30.0
    else:
        vong_noi = min(ri.AtomRings(), key=len)[0]
        dich = 90.0
    tx, ty = tam_vong(vong_noi)
    goc = math.radians(dich) - math.atan2(p[vong_noi][1] - ty, p[vong_noi][0] - tx)
    c, s_ = math.cos(goc), math.sin(goc)
    pos = {i: (((x - tx) * c - (y - ty) * s_) * k, ((x - tx) * s_ + (y - ty) * c) * k) for i, (x, y) in p.items()}
    p = pos

    # vẽ lại từng nhánh: liên kết đầu theo hướng xuyên tâm, sau đó zigzag
    moi = {i: pos[i] for i in trong_vong}
    for vn, dau, cum in sorted(nhanh, key=lambda t: -len(t[2])):
        tx, ty = tam_vong(vn)
        huong = math.degrees(math.atan2(pos[vn][1] - ty, pos[vn][0] - tx))
        x, y = pos[vn]
        dx, dy = _vec(huong)
        moi[dau] = (x + dx * L_LK, y + dy * L_LK)
        re = -1 if math.sin(math.radians(huong)) >= 0 else 1   # nhánh phía trên rẽ xuống trước → zigzag quanh hướng ra
        _dat_cay(mol, adj, moi, dau, vn, huong, re, set(cum), set())
    for i in range(n):  # nguyên tử ngoài vòng không thuộc nhánh đơn (cầu nối giữa hai vòng...) giữ toạ độ RDKit
        moi.setdefault(i, pos[i])
    if not _khong_chong(mol, moi):
        if not _khong_chong(mol, pos):
            return False
        moi = pos
    _ghi_toa_do(mol, moi)
    return True


def _toa_do_2d(mol) -> None:
    """Toạ độ 2D; CoordGen đôi khi trả NaN với phân tử rất nhỏ (H2) → dùng bộ vẽ gốc của RDKit."""
    import math

    rdDepictor.Compute2DCoords(mol)
    conf = mol.GetConformer()
    if any(math.isnan(conf.GetAtomPosition(i).x) for i in range(mol.GetNumAtoms())):
        rdDepictor.SetPreferCoordGen(False)
        try:
            rdDepictor.Compute2DCoords(mol)
        finally:
            rdDepictor.SetPreferCoordGen(True)


def _xoay_ngang(mol) -> None:
    """Xoay phân tử để chiều dài nhất nằm ngang (chuỗi C thẳng hàng như SGK)."""
    import math

    conf = mol.GetConformer()
    pts = [conf.GetAtomPosition(i) for i in range(mol.GetNumAtoms())]
    if len(pts) < 2:
        return
    best = (0.0, 0.0)
    for i, a in enumerate(pts):
        for b in pts[i + 1 :]:
            d = (a.x - b.x) ** 2 + (a.y - b.y) ** 2
            if d > best[0]:
                best = (d, math.atan2(b.y - a.y, b.x - a.x))
    goc = -best[1]
    c, s = math.cos(goc), math.sin(goc)
    for i, p in enumerate(pts):
        conf.SetAtomPosition(i, (p.x * c - p.y * s, p.x * s + p.y * c, 0.0))


def _cat_vien(img: Image.Image) -> Image.Image:
    bg = Image.new(img.mode, img.size, (255, 255, 255))
    bbox = ImageChops.difference(img, bg).getbbox()
    if not bbox:
        return img
    l, t, r, b = bbox
    w, h = img.size
    return img.crop((max(l - _LE, 0), max(t - _LE, 0), min(r + _LE, w), min(b + _LE, h)))


def ve_phan_tu(smiles: str, bond_px: int = BOND_PX, cong_thuc: str = "", mau=None) -> Image.Image | None:
    """Vẽ một chất thành ảnh RGB nền trắng đã cắt viền (None nếu SMILES sai). mau: xem chuan_bi_mol."""
    mol = chuan_bi_mol(smiles, cong_thuc=cong_thuc, mau=mau)
    if mol is None:
        return None
    return ve_mol(mol, bond_px)


def ve_mol(mol, bond_px: int = BOND_PX) -> Image.Image:
    """Vẽ mol đã chuẩn bị (chuan_bi_mol) thành ảnh RGB nền trắng đã cắt viền."""
    k = bond_px / BOND_PX
    conf = mol.GetConformer()
    xs = [conf.GetAtomPosition(i).x for i in range(mol.GetNumAtoms())]
    ys = [conf.GetAtomPosition(i).y for i in range(mol.GetNumAtoms())]
    scale = bond_px / 1.5  # toạ độ RDKit: liên kết dài ~1.5
    w = int((max(xs) - min(xs)) * scale + 6 * bond_px)
    h = int((max(ys) - min(ys)) * scale + 4 * bond_px)

    d = rdMolDraw2D.MolDraw2DCairo(w, h)
    o = d.drawOptions()
    o.fixedBondLength = bond_px
    o.fixedFontSize = max(10, round(CO_CHU_PX * k))
    o.bondLineWidth = NET_PX * k
    o.scaleBondWidth = False
    o.multipleBondOffset = 0.2
    o.padding = 0.02
    o.additionalAtomLabelPadding = 0.1
    o.dummiesAreAttachments = True  # đầu nối polime vẽ dạng nét lượn
    o.clearBackground = True
    o.setBackgroundColour((1, 1, 1, 1))
    o.updateAtomPalette({7: (0.12, 0.29, 0.75), 8: (0.82, 0.12, 0.12), 17: (0.1, 0.55, 0.15), 16: (0.75, 0.55, 0.0)})
    f = _font()
    if f:
        o.fontFile = f
    try:
        rdMolDraw2D.PrepareAndDrawMolecule(d, mol, kekulize=True)
    except Exception:  # noqa: BLE001 – một số ion/kim loại không kekulize được
        rdMolDraw2D.PrepareAndDrawMolecule(d, mol, kekulize=False)
    d.FinishDrawing()
    img = Image.open(io.BytesIO(d.GetDrawingText())).convert("RGB")
    return _cat_vien(img)


def luu_anh_phan_tu(smiles: str, path: Path, bond_px: int = BOND_PX, cong_thuc: str = "") -> bool:
    img = ve_phan_tu(smiles, bond_px, cong_thuc)
    if img is None:
        return False
    img.save(path, "PNG", optimize=True)
    return True


# ---------------------------------------------------------------- .mol / .sdf
def mol_block(smiles: str, ten: str = "") -> str | None:
    mol = Chem.MolFromSmiles((smiles or "").strip())
    if mol is None:
        return None
    rdDepictor.Compute2DCoords(mol)
    if ten:
        mol.SetProp("_Name", ten)
    return Chem.MolToMolBlock(mol)


def xuat_mol(smiles: str, ten: str, path: Path) -> bool:
    block = mol_block(smiles, ten)
    if block is None:
        return False
    path.write_text(block, encoding="utf-8")
    return True


def xuat_sdf(chat: list[dict], path: Path) -> int:
    """chat: [{ki_hieu, ten, smiles, cong_thuc}] → file .sdf gộp; trả về số chất ghi được."""
    n = 0
    with Chem.SDWriter(str(path)) as w:
        for c in chat:
            mol = Chem.MolFromSmiles((c.get("smiles") or "").strip())
            if mol is None:
                continue
            rdDepictor.Compute2DCoords(mol)
            mol.SetProp("_Name", c.get("ten") or c.get("ki_hieu", ""))
            for k in ("ki_hieu", "ten", "cong_thuc"):
                if c.get(k):
                    mol.SetProp(k, str(c[k]))
            mol.SetProp("smiles", c["smiles"])
            w.write(mol)
            n += 1
    return n


# ---------------------------------------------------------------- bố cục sơ đồ chuỗi (dùng chung cho PNG và CDXML)
def doan_so_do(sol) -> list[dict]:
    """Chia các bước thành các đoạn nối tiếp nhau.

    Trả về [{"dau": NutSoDo, "buoc": [(Buoc, NutSoDo chất sau), ...]}]. Chuỗi thẳng → 1 đoạn; chuỗi nhánh → nhiều đoạn.
    """
    from .schemas import NutSoDo

    theo_ki_hieu = {n.ki_hieu.strip(): n for n in sol.so_do}

    def nut(ki_hieu: str, ds) -> NutSoDo:
        k = (ki_hieu or "").strip()
        if k in theo_ki_hieu:
            return theo_ki_hieu[k]
        x = ds[0] if ds else None  # thiếu so_do: lấy chất đầu tiên của vế tương ứng
        return NutSoDo(ki_hieu=k or (x.ten if x else "?"), cong_thuc=k, ten=x.ten if x else "", smiles=x.smiles if x else "")

    doan: list[dict] = []
    for b in sorted(sol.cac_buoc, key=lambda b: b.so):
        dau = nut(b.chat_dau, b.chat_tham_gia)
        sau = nut(b.chat_sau, b.san_pham)
        if doan and doan[-1]["buoc"][-1][1].ki_hieu.strip() == dau.ki_hieu.strip():
            doan[-1]["buoc"].append((b, sau))
        else:
            doan.append({"dau": dau, "buoc": [(b, sau)]})
    return doan
