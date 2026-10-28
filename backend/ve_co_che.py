"""Vẽ cơ chế phản ứng: RDKit vẽ cấu tạo (công thức khung, Kekulé) ra SVG, sau đó vẽ thêm lên SVG:
mũi tên cong (Bézier) đẩy electron, cặp e tự do (2 chấm), điện tích ⊕/⊖ trong vòng tròn, δ+/δ−, mũi tên phản ứng kèm điều kiện,
trạng thái chuyển tiếp (ngoặc vuông + ‡, liên kết đang tạo/đứt nét đứt). Các bước xếp ngang, quá rộng thì xuống dòng.
Cùng bố cục đó được xuất ra .cdxml (ChemDraw XML) để mở và chỉnh từng phần trong ChemDraw.
"""
from __future__ import annotations

import math
import re
from dataclasses import dataclass, field
from xml.sax.saxutils import escape

from rdkit import Chem
from rdkit.Chem import rdDepictor
from rdkit.Chem.Draw import rdMolDraw2D
from rdkit.Geometry import Point2D

from . import renderer
from .chemdraw import _nhan_nguyen_tu, _rong_chu
from .co_che import CoChe, MuiTen, ap_mui_ten, tu_smiles

BOND_PX = 30.0          # độ dài liên kết (px) – hình gọn như SGK
FONT_PX = 14            # cỡ chữ nhãn nguyên tử
NET = 1.7               # độ dày nét sắc nét
L = 1.5                 # độ dài liên kết trong toạ độ RDKit
RONG_TOI_DA = 1080.0    # px: quá rộng thì xuống dòng
MAU_MUI_TEN = "#b91c1c" # đỏ đậm tương phản cao cho mũi tên electron
MAU_CHU = "#1b2621"
FONT = "Helvetica, Arial, sans-serif"

# đơn vị toạ độ (bond = 1.5)
GAP_CONG = 1.6          # chỗ cho dấu "+" giữa các chất (cộng thêm lề nhãn của hai mảnh)
DAI_MUI_TEN = 2.6       # mũi tên phản ứng
LE_DONG = 1.7           # khoảng trên/dưới mỗi hàng (chỗ cho mũi tên cong, chữ)


# ================================================================ dữ liệu bố cục
@dataclass
class _Khoi:
    loai: str                                   # "trang_thai" | "ts" | "mui_ten"
    mol: Chem.Mol | None = None                 # mol của khối (nhiều mảnh, có toạ độ, có map)
    mui_ten: list[MuiTen] = field(default_factory=list)
    delta: dict[int, str] = field(default_factory=dict)
    cap_e_them: list[int] = field(default_factory=list)
    net_dut: set[tuple[int, int]] = field(default_factory=set)   # (map, map) liên kết vẽ nét đứt (TS)
    nhan: str = ""                              # điều kiện trên mũi tên phản ứng
    so: str = ""                                # số bước dưới mũi tên phản ứng
    thuan_nghich: bool = False                  # mũi tên thuận nghịch ⇌
    buoc_cham: bool = False                     # bước chậm
    nhan_trung_gian: str = ""                   # nhãn chất trung gian
    rong: float = 0.0
    tren: float = 0.0                           # chiều cao trên/dưới đường giữa
    duoi: float = 0.0
    x: float = 0.0                              # vị trí (đơn vị toạ độ) sau khi xếp hàng
    y: float = 0.0
    cong: list[float] = field(default_factory=list)   # toạ độ x các dấu "+" (tương đối khối)
    chi_so: dict[int, int] = field(default_factory=dict)  # map → chỉ số nguyên tử trong mol vẽ chung


def _toa_do_manh(m: Chem.Mol) -> None:
    """Toạ độ 2D kiểu SGK cho một mảnh (giữ nguyên liên kết Kekulé và H có map) – dùng các bước xếp của renderer."""
    if m.GetNumAtoms() == 1:
        conf = Chem.Conformer(1)
        conf.SetAtomPosition(0, (0.0, 0.0, 0.0))
        m.RemoveAllConformers()
        m.AddConformer(conf, assignId=True)
        return
    renderer._toa_do_2d(m)
    try:
        if not (renderer._xep_mach_zigzag(m) or renderer._xep_vong_nhanh_zigzag(m)):
            renderer._xoay_ngang(m)
    except Exception:  # noqa: BLE001 – cấu tạo lạ: giữ toạ độ RDKit
        pass
    renderer._chuan_do_dai_lien_ket(m)


def _bbox(m: Chem.Mol) -> tuple[float, float, float, float]:
    c = m.GetConformer()
    xs = [c.GetAtomPosition(i).x for i in range(m.GetNumAtoms())]
    ys = [c.GetAtomPosition(i).y for i in range(m.GetNumAtoms())]
    return min(xs), max(xs), min(ys), max(ys)


def _dich(m: Chem.Mol, dx: float, dy: float) -> None:
    c = m.GetConformer()
    for i in range(m.GetNumAtoms()):
        p = c.GetAtomPosition(i)
        c.SetAtomPosition(i, (p.x + dx, p.y + dy, 0.0))


def _co_nhan(a: Chem.Atom) -> bool:
    return a.GetSymbol() != "C" or a.GetDegree() == 0 or a.HasProp("atomLabel")


def _gop_h(mol: Chem.Mol, giu: set[int]) -> Chem.Mol:
    """H tách riêng mà không mũi tên nào dùng tới ở bước này → gộp lại vào nguyên tử mang nó (vẽ gọn: CH3 thay vì C–H)."""
    rw = Chem.RWMol(mol)
    xoa = []
    for a in rw.GetAtoms():
        if a.GetAtomicNum() == 1 and a.GetDegree() == 1 and a.GetAtomMapNum() not in giu and a.GetFormalCharge() == 0:
            nb = a.GetNeighbors()[0]
            nb.SetNumExplicitHs(nb.GetNumExplicitHs() + 1)
            xoa.append(a.GetIdx())
    for i in sorted(xoa, reverse=True):
        rw.RemoveAtom(i)
    return rw.GetMol()


def _lat_cho_dung_huong(manh: list[Chem.Mol], k: _Khoi) -> None:
    """Lật ngang từng mảnh để đầu nhận/cho electron quay về phía mảnh kia, nhóm rời (nhận cặp e từ liên kết) quay ra xa."""
    cua = {}
    for j, f in enumerate(manh):
        for a in f.GetAtoms():
            cua[a.GetAtomMapNum()] = j
    muon: dict[int, list[tuple[int, int, float]]] = {j: [] for j in range(len(manh))}
    for mt in k.mui_ten:
        for s_ in mt.tu:
            for d_ in mt.den:
                js, jd = cua.get(s_), cua.get(d_)
                if js is None or jd is None or js == jd:
                    continue
                muon[js].append((s_, 1 if jd > js else -1, 1.0))
                muon[jd].append((d_, 1 if js > jd else -1, 1.0))
        if mt.tu_loai == "lien_ket" and mt.den_loai == "nguyen_tu" and cua.get(mt.den[0]) is not None:
            j = cua[mt.den[0]]
            muon[j].append((mt.den[0], 1 if j > 0 else -1, 0.6))  # nhóm rời: phía không bị tấn công
    for j, f in enumerate(manh):
        if not muon[j] or f.GetNumAtoms() < 2:
            continue
        conf = f.GetConformer()
        x0, x1, _, _ = _bbox(f)
        rong = (x1 - x0) or 1.0
        vi_tri = {a.GetAtomMapNum(): conf.GetAtomPosition(a.GetIdx()).x for a in f.GetAtoms()}

        def diem(lat: bool) -> float:
            tong = 0.0
            for m, huong, w in muon[j]:
                if m not in vi_tri:
                    continue
                t = ((x0 + x1 - vi_tri[m]) if lat else vi_tri[m]) - x0
                t /= rong
                tong += w * (t if huong > 0 else 1 - t)
            return tong

        if diem(True) > diem(False) + 1e-6:
            for i in range(f.GetNumAtoms()):
                pt = conf.GetAtomPosition(i)
                conf.SetAtomPosition(i, (x0 + x1 - pt.x, pt.y, 0.0))


def _xep_trang_thai(mol: Chem.Mol, k: _Khoi) -> None:
    """Các mảnh của một trạng thái xếp ngang, cách nhau dấu "+"; gốc khối ở (0, 0) giữa chiều cao."""
    giu = {m for mt in k.mui_ten for m in (*mt.tu, *mt.den)} | set(k.delta) | set(k.cap_e_them)
    mol = _gop_h(mol, giu)
    manh = [Chem.Mol(f) for f in Chem.GetMolFrags(mol, asMols=True, sanitizeFrags=False)]
    dung = {m for mt in k.mui_ten for m in (*mt.tu, *mt.den)}
    tham_gia = [f for f in manh if any(a.GetAtomMapNum() in dung for a in f.GetAtoms())]
    manh = tham_gia + [f for f in manh if f not in tham_gia] if k.mui_ten else manh
    for f in manh:
        _toa_do_manh(f)
    _lat_cho_dung_huong(manh, k)
    x, ds = 0.8, []  # lề hai bên khối: chỗ cho δ, ⊕/⊖ ở nguyên tử sát mép
    for i, f in enumerate(manh):
        x0, x1, y0, y1 = _bbox(f)
        conf = f.GetConformer()

        def le(mep: float) -> float:  # nguyên tử có chữ ở sát mép → chừa chỗ cho chữ (và chữ H)
            gan = [a for a in f.GetAtoms() if abs(conf.GetAtomPosition(a.GetIdx()).x - mep) < 0.35]
            if any(_co_nhan(a) and a.GetTotalNumHs() for a in gan):
                return 1.05
            return 0.7 if any(_co_nhan(a) for a in gan) else 0.3
        le_trai, le_phai = le(x0), le(x1)
        _dich(f, x - x0 + le_trai, -(y0 + y1) / 2)
        x += (x1 - x0) + le_trai + le_phai
        ds.append(f)
        if i < len(manh) - 1:
            k.cong.append(x + GAP_CONG / 2)
            x += GAP_CONG
    tong = Chem.RWMol()
    for f in ds:
        tong.InsertMol(f)
    k.mol = tong.GetMol()
    _, _, y0, y1 = _bbox(k.mol)
    k.rong, k.tren, k.duoi = x + 0.8, max(y1, 0.6) + 0.3, max(-y0, 0.6) + 0.3


def _khoi_ts(truoc: Chem.Mol, sau: Chem.Mol) -> _Khoi:
    """Trạng thái chuyển tiếp: cấu tạo trước + liên kết đang tạo (có ở sau) – liên kết tạo/đứt vẽ nét đứt, điện tích thành δ."""
    bd_t = {a.GetAtomMapNum(): a.GetIdx() for a in truoc.GetAtoms() if a.GetAtomMapNum()}
    bd_s = {a.GetAtomMapNum(): a.GetIdx() for a in sau.GetAtoms() if a.GetAtomMapNum()}
    ts = Chem.RWMol(truoc)

    def lk(m, bd):
        return {tuple(sorted((m.GetAtomWithIdx(b.GetBeginAtomIdx()).GetAtomMapNum(), m.GetAtomWithIdx(b.GetEndAtomIdx()).GetAtomMapNum())))
                for b in m.GetBonds()}

    lk_t, lk_s = lk(truoc, bd_t), lk(sau, bd_s)
    tao, dut = lk_s - lk_t, lk_t - lk_s
    doi = {m for ab in tao | dut for m in ab}
    for a, b in tao:
        ts.AddBond(bd_t[a], bd_t[b], Chem.BondType.SINGLE)
    ts = Chem.RWMol(_gop_h(ts.GetMol(), doi))  # H không tham gia: gộp lại
    bd_t = {a.GetAtomMapNum(): a.GetIdx() for a in ts.GetAtoms() if a.GetAtomMapNum()}
    k = _Khoi("ts", net_dut=tao | dut)
    for at in ts.GetAtoms():
        m = at.GetAtomMapNum()
        q_t = at.GetFormalCharge()
        q_s = sau.GetAtomWithIdx(bd_s[m]).GetFormalCharge() if m in bd_s else q_t
        if m in doi and (q_t or q_s):
            k.delta[m] = "-" if min(q_t, q_s) < 0 else "+"
        at.SetFormalCharge(0)
        at.SetNoImplicit(True)
    ts.UpdatePropertyCache(strict=False)
    Chem.FastFindRings(ts)
    # nguyên tử trung tâm (vừa tạo vừa đứt liên kết, vd C của SN2): Nu – C – X thẳng hàng, ba nhóm còn lại xoè hình chữ Y
    tam = next((m for m in doi if any(m in ab for ab in tao) and any(m in ab for ab in dut)), None)
    coord_map = {}
    if tam is not None:
        nu = next(x for ab in tao if tam in ab for x in ab if x != tam)
        roi = next(x for ab in dut if tam in ab for x in ab if x != tam)
        it = bd_t[tam]
        # hiện H của nguyên tử trung tâm (SGK vẽ rõ H ở trạng thái chuyển tiếp)
        so_h = ts.GetAtomWithIdx(it).GetNumExplicitHs()
        for _ in range(so_h):
            hi = ts.AddAtom(Chem.Atom(1))
            ts.GetAtomWithIdx(hi).SetNoImplicit(True)
            ts.AddBond(it, hi, Chem.BondType.SINGLE)
        ts.GetAtomWithIdx(it).SetNumExplicitHs(0)
        coord_map[it] = Point2D(0, 0)
        coord_map[bd_t[nu]] = Point2D(-1.25 * L, 0)
        coord_map[bd_t[roi]] = Point2D(1.25 * L, 0)
        khac = [nb.GetIdx() for nb in ts.GetAtomWithIdx(it).GetNeighbors() if nb.GetIdx() not in (bd_t[nu], bd_t[roi])]
        for nb, goc in zip(khac, (90, 230, 310)):
            coord_map[nb] = Point2D(L * math.cos(math.radians(goc)), L * math.sin(math.radians(goc)))
    m = ts.GetMol()
    co_cg = rdDepictor.GetPreferCoordGen()
    rdDepictor.SetPreferCoordGen(False)  # CoordGen bỏ qua coordMap → dùng bộ dựng toạ độ gốc của RDKit
    try:
        rdDepictor.Compute2DCoords(m, coordMap=coord_map or None)
    finally:
        rdDepictor.SetPreferCoordGen(co_cg)
    x0, x1, y0, y1 = _bbox(m)
    _dich(m, -x0 + 0.9, -(y0 + y1) / 2)  # chừa chỗ cho ngoặc vuông
    k.mol = m
    k.rong, k.tren, k.duoi = (x1 - x0) + 1.8 + 0.5, (y1 - y0) / 2 + 0.7, (y1 - y0) / 2 + 0.7
    return k


def _cac_khoi(cc: CoChe) -> list[_Khoi]:
    ds: list[_Khoi] = []
    for i, b in enumerate(cc.buoc):
        mol = tu_smiles(b.chat)
        k = _Khoi("trang_thai", mui_ten=b.mui_ten, delta=dict(b.delta), cap_e_them=list(b.cap_e),
                  nhan_trung_gian=b.nhan_trung_gian)
        _xep_trang_thai(mol, k)
        if b.nhan_trung_gian:
            k.duoi += 0.5
        ds.append(k)
        nhan = b.dieu_kien or (cc.dieu_kien if i == 0 else "")
        so = f"({i + 1})" + (" (chậm)" if b.buoc_cham else "")
        chuyen_h = getattr(b, "chuyen_h", [])
        if b.trang_thai_chuyen_tiep:
            ds.append(_Khoi("mui_ten", nhan=nhan, so=so, thuan_nghich=b.thuan_nghich, buoc_cham=b.buoc_cham))
            ds.append(_khoi_ts(mol, ap_mui_ten(mol, b.mui_ten, chuyen_h=chuyen_h)))
            ds.append(_Khoi("mui_ten", thuan_nghich=b.thuan_nghich))
        else:
            ds.append(_Khoi("mui_ten", nhan=nhan, so=so, thuan_nghich=b.thuan_nghich, buoc_cham=b.buoc_cham))
    k = _Khoi("trang_thai")
    _xep_trang_thai(tu_smiles(cc.san_pham), k)
    ds.append(k)
    for k in ds:
        if k.loai == "mui_ten":
            k.rong = max(DAI_MUI_TEN, (_rong_px(k.nhan, 11) + 18) / (BOND_PX / L), (_rong_px(k.so, 10) + 18) / (BOND_PX / L)) + 0.4
            k.tren = k.duoi = 0.8
    return ds


def _rong_px(text: str, size: float) -> float:
    return _rong_chu(text, size) * 0.98 if text else 0.0


def _xep_hang(ds: list[_Khoi]) -> tuple[float, float]:
    """Xếp khối trái → phải, quá RONG_TOI_DA thì xuống dòng (mũi tên phản ứng được ở cuối hàng). Trả (rộng, cao) đơn vị."""
    rong_toi_da = RONG_TOI_DA / (BOND_PX / L)
    hang: list[list[_Khoi]] = [[]]
    x = 0.0
    for k in ds:
        if hang[-1] and k.loai != "mui_ten" and x + k.rong > rong_toi_da:
            hang.append([])
            x = 0.0
        hang[-1].append(k)
        x += k.rong
    y = 0.0
    rong = 0.0
    for h in hang:
        tren = max(k.tren for k in h) + LE_DONG
        duoi = max(k.duoi for k in h) + LE_DONG
        x = 0.0
        for k in h:
            k.x, k.y = x, -(y + tren)  # toạ độ RDKit: y hướng lên
            x += k.rong
        rong = max(rong, x)
        y += tren + duoi
    return rong, y


# ================================================================ vẽ
@dataclass
class _Ve:
    """Kết quả vẽ: SVG + hình học (px) dùng chung cho xuất CDXML."""
    svg: str
    rong: float
    cao: float
    nguyen_tu: list[dict]          # {x, y, sym, nhan, q, khoi}
    lien_ket: list[dict]           # {a, b, bac, dut}
    mui_ten_cong: list[dict]       # {s, c, e}
    mui_ten_pu: list[dict]         # {x0, x1, y, nhan, so}
    chu: list[dict]                # {x, y, text, size, can}
    ngoac: list[dict]              # {x0, y0, x1, y1}
    cap_e: list[dict]              # {x, y, goc}
    dien_tich: list[dict]          # {x, y, dau}
    bond_px: float


def _goc(dx: float, dy: float) -> float:
    return math.degrees(math.atan2(dy, dx)) % 360


def _lech(a: float, b: float) -> float:
    d = abs(a - b) % 360
    return min(d, 360 - d)


def ve(cc: CoChe) -> _Ve:
    khoi = _cac_khoi(cc)
    rong_u, cao_u = _xep_hang(khoi)
    s = BOND_PX / L

    # ---- gộp mọi mảnh vào một mol, vẽ một lần bằng RDKit
    tong = Chem.RWMol()
    for k in khoi:
        if k.mol is None:
            continue
        m = Chem.Mol(k.mol)
        _dich(m, k.x, k.y)
        goc = tong.GetNumAtoms()
        tong.InsertMol(m)
        k.chi_so = {a.GetAtomMapNum(): goc + a.GetIdx() for a in m.GetAtoms() if a.GetAtomMapNum()}
        k.goc = goc  # type: ignore[attr-defined]
    ve_mol = Chem.RWMol(tong)
    can_nhan = {a.GetIdx() for a in tong.GetAtoms() if a.GetSymbol() == "C" and a.GetFormalCharge() and a.GetDegree() <= 1}
    for k in khoi:  # C cần ghi chữ: C mang cặp e tấn công (vd :C≡N⁻), C mang điện ở đầu mạch
        for mt in k.mui_ten:
            if mt.tu_loai == "cap_e":
                can_nhan.add(k.chi_so.get(mt.tu[0]))
    an_lk = []
    for k in khoi:
        for a, b in k.net_dut:
            if a in k.chi_so and b in k.chi_so:
                an_lk.append((k.chi_so[a], k.chi_so[b]))
    for i, j in an_lk:
        if ve_mol.GetBondBetweenAtoms(i, j) is not None:
            ve_mol.RemoveBond(i, j)
    for at in ve_mol.GetAtoms():
        at.SetAtomMapNum(0)
        at.SetFormalCharge(0)
        at.SetNoImplicit(True)
        if at.GetIdx() in can_nhan and at.GetSymbol() == "C":
            h = at.GetTotalNumHs()
            at.SetProp("atomLabel", "C" + ("" if h == 0 else "H" if h == 1 else f"H<sub>{h}</sub>"))
    ve_mol.UpdatePropertyCache(strict=False)
    Chem.FastFindRings(ve_mol)
    pad = 26.0
    W, H = rong_u * s + 2 * pad, cao_u * s + 2 * pad
    d = rdMolDraw2D.MolDraw2DSVG(int(math.ceil(W)), int(math.ceil(H)))
    o = d.drawOptions()
    o.fixedBondLength = s
    o.fixedFontSize = FONT_PX
    o.bondLineWidth = NET
    o.scaleBondWidth = False
    o.prepareMolsBeforeDrawing = False
    o.padding = 0.0
    o.additionalAtomLabelPadding = 0.12
    o.useBWAtomPalette()
    d.DrawMolecule(ve_mol)
    d.FinishDrawing()
    svg = d.GetDrawingText()

    n = ve_mol.GetNumAtoms()
    conf = tong.GetConformer()
    px = [(d.GetDrawCoords(i).x, d.GetDrawCoords(i).y) for i in range(n)]
    # phép đổi toạ độ RDKit → px (bình phương tối thiểu): px = a·x + bx, py = −a·y + by
    xs = [conf.GetAtomPosition(i).x for i in range(n)]
    ys = [conf.GetAtomPosition(i).y for i in range(n)]
    mx, my = sum(xs) / n, sum(ys) / n
    mpx, mpy = sum(p[0] for p in px) / n, sum(p[1] for p in px) / n
    var = sum((x - mx) ** 2 + (y - my) ** 2 for x, y in zip(xs, ys))
    a = (sum((x - mx) * (p[0] - mpx) - (y - my) * (p[1] - mpy) for x, y, p in zip(xs, ys, px)) / var) if var > 1e-9 else s
    bx, by = mpx - a * mx, mpy + a * my
    B = a * L  # độ dài liên kết thực tế (px)

    def P(x: float, y: float) -> tuple[float, float]:
        return a * x + bx, -a * y + by

    # ---- thông tin từng nguyên tử để đặt chấm/điện tích tránh liên kết và chữ
    ke: dict[int, list[int]] = {i: [] for i in range(n)}
    for b in tong.GetBonds():
        ke[b.GetBeginAtomIdx()].append(b.GetEndAtomIdx())
        ke[b.GetEndAtomIdx()].append(b.GetBeginAtomIdx())
    for i, j in an_lk:
        if j not in ke[i]:
            ke[i].append(j)
            ke[j].append(i)
    co_nhan = [_co_nhan(ve_mol.GetAtomWithIdx(i)) or i in can_nhan for i in range(n)]
    ban_dau: dict[int, list[float]] = {}
    phia_h: dict[int, int] = {}  # +1: chữ H bên phải, −1: bên trái
    for i in range(n):
        goc_lk = [_goc(px[j][0] - px[i][0], px[j][1] - px[i][1]) for j in ke[i]]
        at = ve_mol.GetAtomWithIdx(i)
        if co_nhan[i] and at.GetTotalNumHs() > 0:  # RDKit: H bên phải, trừ khi liên kết đều đi sang phải hoặc nguyên tử đứng riêng
            trai = not goc_lk or all(math.cos(math.radians(g)) > 0.2 for g in goc_lk)
            phia_h[i] = -1 if trai else 1
            goc_lk += [180.0, 155.0, 205.0] if trai else [0.0, 25.0, 335.0]
        ban_dau[i] = goc_lk

    def ban_kinh(i: int, goc: float) -> float:
        if not co_nhan[i]:
            return 0.32 * B
        rx, ry = 0.52 * B, 0.42 * B
        if i in phia_h and math.cos(math.radians(goc)) * phia_h[i] > 0.3:
            rx += 0.45 * B
        t = math.radians(goc)
        return rx * ry / math.hypot(ry * math.cos(t), rx * math.sin(t))

    def cho_trong(i: int, uu_tien: float | None) -> float:
        tot, goc_tot = 1e9, 0.0
        for g in range(0, 360, 10):
            phat = sum(max(0.0, 50 - _lech(g, b)) * 3 for b in ban_dau[i])
            lech = _lech(g, uu_tien) if uu_tien is not None else (0 if g == 270 else 15)  # mặc định: phía trên
            if phat + lech < tot:
                tot, goc_tot = phat + lech, float(g)
        ban_dau[i].append(goc_tot)
        return goc_tot

    def diem(i: int, goc: float, them: float = 0.0) -> tuple[float, float]:
        r = ban_kinh(i, goc) + them
        return px[i][0] + r * math.cos(math.radians(goc)), px[i][1] + r * math.sin(math.radians(goc))

    lop: list[str] = []
    out = _Ve(svg="", rong=W, cao=H, nguyen_tu=[], lien_ket=[], mui_ten_cong=[], mui_ten_pu=[], chu=[], ngoac=[],
              cap_e=[], dien_tich=[], bond_px=B)
    for i in range(n):
        at = tong.GetAtomWithIdx(i)
        out.nguyen_tu.append({"x": px[i][0], "y": px[i][1], "atom": at, "co_nhan": co_nhan[i],
                              "nhan": ve_mol.GetAtomWithIdx(i).GetProp("atomLabel") if ve_mol.GetAtomWithIdx(i).HasProp("atomLabel") else None})
    for b in tong.GetBonds():
        i, j = b.GetBeginAtomIdx(), b.GetEndAtomIdx()
        out.lien_ket.append({"a": i, "b": j, "bac": int(b.GetBondTypeAsDouble()), "dut": (i, j) in an_lk or (j, i) in an_lk})
    for i, j in an_lk:
        if not tong.GetBondBetweenAtoms(i, j):
            out.lien_ket.append({"a": i, "b": j, "bac": 1, "dut": True})

    # ---- liên kết nét đứt (trạng thái chuyển tiếp)
    for i, j in an_lk:
        (x1, y1), (x2, y2) = px[i], px[j]
        dx, dy = x2 - x1, y2 - y1
        dd = math.hypot(dx, dy) or 1
        c1 = (0.3 * B if co_nhan[i] else 0) / dd
        c2 = (0.3 * B if co_nhan[j] else 0) / dd
        lop.append(f'<line x1="{x1 + dx * c1:.1f}" y1="{y1 + dy * c1:.1f}" x2="{x2 - dx * c2:.1f}" y2="{y2 - dy * c2:.1f}" '
                   f'stroke="{MAU_CHU}" stroke-width="{NET}" stroke-dasharray="3.2 2.6"/>')

    def chu(x, y, text, size=11, can="middle", nghieng=False, mau=MAU_CHU):
        lop.append(f'<text x="{x:.1f}" y="{y:.1f}" font-family="{FONT}" font-size="{size}" text-anchor="{can}" fill="{mau}"'
                   f'{" font-style=" + chr(34) + "italic" + chr(34) if nghieng else ""}>{escape(text)}</text>')
        out.chu.append({"x": x, "y": y, "text": text, "size": size, "can": can})

    def cap_e_ve(i: int, goc: float) -> tuple[float, float]:
        cx, cy = diem(i, goc, 1.5)
        nx, ny = -math.sin(math.radians(goc)), math.cos(math.radians(goc))
        for k_ in (-1, 1):
            lop.append(f'<circle cx="{cx + nx * 2.9 * k_:.1f}" cy="{cy + ny * 2.9 * k_:.1f}" r="1.75" fill="{MAU_CHU}"/>')
        out.cap_e.append({"x": cx, "y": cy, "goc": goc})
        return cx, cy

    def dien_tich_ve(i: int, q: int) -> None:
        g = cho_trong(i, 315)
        cx, cy = diem(i, g, 3.5)
        r = 5.4
        lop.append(f'<circle cx="{cx:.1f}" cy="{cy:.1f}" r="{r}" fill="#fff" stroke="{MAU_CHU}" stroke-width="0.9"/>')
        lop.append(f'<line x1="{cx - 3:.1f}" y1="{cy:.1f}" x2="{cx + 3:.1f}" y2="{cy:.1f}" stroke="{MAU_CHU}" stroke-width="1"/>')
        if q > 0:
            lop.append(f'<line x1="{cx:.1f}" y1="{cy - 3:.1f}" x2="{cx:.1f}" y2="{cy + 3:.1f}" stroke="{MAU_CHU}" stroke-width="1"/>')
        if abs(q) > 1:
            chu(cx + r + 1, cy - r + 2, str(abs(q)), 9, "start")
        out.dien_tich.append({"x": cx, "y": cy, "dau": q})

    def delta_ve(i: int, dau: str) -> None:
        g = cho_trong(i, 270)
        cx, cy = diem(i, g, 7)
        chu(cx, cy + 4, "δ" + ("+" if dau == "+" else "−"), 11, "middle", nghieng=True)

    can_tranh_idx: list[int] = []

    def mui_ten_cong(sx, sy, tx, ty, r_dich: float, can_tranh: list[tuple[float, float]], kieu: str = "day_du") -> None:
        """Bézier bậc 2 từ (sx, sy) tới điểm cách (tx, ty) r_dich px; uốn về phía thoáng hơn. kieu: 'day_du' (2e) | 'nua_dau' (1e) | 'vong'."""
        dx, dy = tx - sx, ty - sy
        dai = math.hypot(dx, dy) or 1.0
        nx, ny = -dy / dai, dx / dai
        h = min(max(0.38 * dai, 15.0), 46.0) if dai > 1.1 * B else 0.62 * B  # mũi tên ngắn: uốn hẳn ra ngoài
        mx, my = (sx + tx) / 2, (sy + ty) / 2
        tot = None
        for sgn in (1, -1):
            cx, cy = mx + sgn * nx * h, my + sgn * ny * h
            chat = sum(math.exp(-math.hypot(cx - x, cy - y) / (0.7 * B)) for x, y in can_tranh)
            if tot is None or chat < tot[0]:
                tot = (chat, cx, cy)
        _, cx, cy = tot
        ux, uy = tx - cx, ty - cy
        du = math.hypot(ux, uy) or 1.0
        ux, uy = ux / du, uy / du
        ex, ey = tx - ux * r_dich, ty - uy * r_dich
        dau_dai, dau_rong = 7.6, 5.4
        lx, ly = ex - ux * dau_dai * 0.8, ey - uy * dau_dai * 0.8
        lop.append(f'<path d="M{sx:.1f},{sy:.1f} Q{cx:.1f},{cy:.1f} {lx:.1f},{ly:.1f}" fill="none" stroke="{MAU_MUI_TEN}" '
                   f'stroke-width="1.8" stroke-linecap="round"/>')
        bx_, by_ = ex - ux * dau_dai, ey - uy * dau_dai
        px_, py_ = -uy * dau_rong / 2, ux * dau_rong / 2
        if kieu == "nua_dau":
            # mũi tên nửa đầu (fish-hook / 1 e): chỉ vẽ một cánh ngạnh
            lop.append(f'<path d="M{ex:.1f},{ey:.1f} L{bx_ + px_:.1f},{by_ + py_:.1f} L{bx_:.1f},{by_:.1f} Z" fill="{MAU_MUI_TEN}"/>')
        else:
            lop.append(f'<path d="M{ex:.1f},{ey:.1f} L{bx_ + px_:.1f},{by_ + py_:.1f} L{bx_ - px_:.1f},{by_ - py_:.1f} Z" fill="{MAU_MUI_TEN}"/>')
        out.mui_ten_cong.append({"s": (sx, sy), "c": (cx, cy), "e": (ex, ey), "kieu": kieu})
        # đánh dấu hướng đường cong đi qua quanh các nguyên tử gần đó → điện tích/δ đặt sau sẽ né
        for t in (0.15, 0.3, 0.5, 0.7, 0.85, 1.0):
            qx = (1 - t) ** 2 * sx + 2 * (1 - t) * t * cx + t * t * ex
            qy = (1 - t) ** 2 * sy + 2 * (1 - t) * t * cy + t * t * ey
            for i in can_tranh_idx:
                if math.hypot(qx - px[i][0], qy - px[i][1]) < 0.95 * B:
                    ban_dau[i].append(_goc(qx - px[i][0], qy - px[i][1]))

    # ---- từng khối
    for k in khoi:
        if k.loai == "mui_ten":
            x0, yc = P(k.x + 0.25, k.y)
            x1, _ = P(k.x + k.rong - 0.25, k.y)
            if k.thuan_nghich:
                # ⇌: hai nửa mũi tên ngược chiều
                y_tren, y_duoi = yc - 2.8, yc + 2.8
                lop.append(f'<line x1="{x0:.1f}" y1="{y_tren:.1f}" x2="{x1 - 6:.1f}" y2="{y_tren:.1f}" stroke="{MAU_CHU}" stroke-width="{NET}"/>')
                lop.append(f'<path d="M{x1:.1f},{y_tren:.1f} L{x1 - 7:.1f},{y_tren - 3.8:.1f} L{x1 - 7:.1f},{y_tren:.1f} Z" fill="{MAU_CHU}"/>')
                lop.append(f'<line x1="{x0 + 6:.1f}" y1="{y_duoi:.1f}" x2="{x1:.1f}" y2="{y_duoi:.1f}" stroke="{MAU_CHU}" stroke-width="{NET}"/>')
                lop.append(f'<path d="M{x0:.1f},{y_duoi:.1f} L{x0 + 7:.1f},{y_duoi + 3.8:.1f} L{x0 + 7:.1f},{y_duoi:.1f} Z" fill="{MAU_CHU}"/>')
            else:
                lop.append(f'<line x1="{x0:.1f}" y1="{yc:.1f}" x2="{x1 - 8:.1f}" y2="{yc:.1f}" stroke="{MAU_CHU}" stroke-width="{NET}"/>')
                lop.append(f'<path d="M{x1:.1f},{yc:.1f} L{x1 - 9:.1f},{yc - 4.4:.1f} L{x1 - 9:.1f},{yc + 4.4:.1f} Z" fill="{MAU_CHU}"/>')
            out.mui_ten_pu.append({"x0": x0, "x1": x1, "y": yc, "nhan": k.nhan, "so": k.so, "thuan_nghich": k.thuan_nghich})
            if k.nhan:
                chu((x0 + x1) / 2, yc - 7, k.nhan, 11)
            if k.so:
                chu((x0 + x1) / 2, yc + 15, k.so, 10, mau="#6b756e")
            continue
        for xc in k.cong:
            cx, cy = P(k.x + xc, k.y)
            chu(cx, cy + 5, "+", 15)
        idx = list(range(k.goc, k.goc + k.mol.GetNumAtoms()))  # type: ignore[attr-defined]
        if k.nhan_trung_gian:
            xs_ = [px[i][0] for i in idx]
            ys_ = [px[i][1] for i in idx]
            cx_tg = (min(xs_) + max(xs_)) / 2
            cy_tg = max(ys_) + 13
            chu(cx_tg, cy_tg, f"({k.nhan_trung_gian})", 10, "middle", nghieng=True, mau="#4b5563")
        if k.loai == "ts":
            xs_ = [px[i][0] for i in idx]
            ys_ = [px[i][1] for i in idx]
            gx0, gx1 = min(xs_) - 0.95 * B, max(xs_) + 0.95 * B
            gy0, gy1 = min(ys_) - 0.62 * B, max(ys_) + 0.62 * B
            for xb, chieu in ((gx0, 1), (gx1, -1)):
                lop.append(f'<path d="M{xb + chieu * 5:.1f},{gy0:.1f} L{xb:.1f},{gy0:.1f} L{xb:.1f},{gy1:.1f} L{xb + chieu * 5:.1f},{gy1:.1f}" '
                           f'fill="none" stroke="{MAU_CHU}" stroke-width="{NET}"/>')
            chu(gx1 + 4, gy0 + 6, "‡", 15, "start")
            out.ngoac.append({"x0": gx0, "y0": gy0, "x1": gx1, "y1": gy1})
        can_tranh_idx[:] = idx
        # mũi tên cong (cặp e xuất phát đặt về phía đích)
        tranh = [px[i] for i in idx]
        for mt in k.mui_ten:
            if any(m not in k.chi_so for m in (*mt.tu, *mt.den)):
                continue
            if mt.den_loai == "nguyen_tu":
                dich = k.chi_so[mt.den[0]]
                tx, ty = px[dich]
                tu_lk_ve_chinh_no = mt.tu_loai == "lien_ket" and mt.den[0] in mt.tu
                r_dich = (0.42 if co_nhan[dich] else 0.22) * B if tu_lk_ve_chinh_no else (0.5 if co_nhan[dich] else 0.24) * B
            else:
                ia, ib = k.chi_so[mt.den[0]], k.chi_so[mt.den[1]]
                if tong.GetBondBetweenAtoms(ia, ib) is not None:  # tăng bậc liên kết có sẵn: trỏ vào giữa liên kết
                    tx, ty = (px[ia][0] + px[ib][0]) / 2, (px[ia][1] + px[ib][1]) / 2
                    r_dich = 3.0
                else:  # liên kết mới: trỏ tới nguyên tử "mới" (không thuộc liên kết nguồn)
                    moi_ = ib if mt.den[0] in mt.tu else ia
                    tx, ty = px[moi_]
                    r_dich = (0.5 if co_nhan[moi_] else 0.24) * B
            if mt.tu_loai == "cap_e":
                i0 = k.chi_so[mt.tu[0]]
                sx, sy = cap_e_ve(i0, cho_trong(i0, _goc(tx - px[i0][0], ty - px[i0][1])))
            else:
                ia, ib = k.chi_so[mt.tu[0]], k.chi_so[mt.tu[1]]
                sx, sy = (px[ia][0] + px[ib][0]) / 2, (px[ia][1] + px[ib][1]) / 2
            mui_ten_cong(sx, sy, tx, ty, r_dich, tranh, kieu=mt.kieu)
        # điện tích (không vẽ ở TS: thay bằng δ), rồi δ, rồi cặp e thêm – đều né liên kết, chữ H và đường mũi tên
        if k.loai == "trang_thai":
            for i in idx:
                q = tong.GetAtomWithIdx(i).GetFormalCharge()
                if q:
                    dien_tich_ve(i, q)
        for m, dau in k.delta.items():
            if m in k.chi_so:
                delta_ve(k.chi_so[m], dau)
        for m in k.cap_e_them:
            if m in k.chi_so:
                cap_e_ve(k.chi_so[m], cho_trong(k.chi_so[m], None))

    # ---- ghép SVG: nền trắng + hình RDKit + lớp vẽ thêm; thêm viewBox để co giãn được
    than = re.sub(r"<\?xml[^>]*>\s*", "", svg)
    than = re.sub(r"<svg\b[^>]*>", (f'<svg xmlns="http://www.w3.org/2000/svg" xmlns:rdkit="http://www.rdkit.org/xml" '
                                     f'xmlns:xlink="http://www.w3.org/1999/xlink" version="1.1" width="{W:.0f}" height="{H:.0f}" '
                                     f'viewBox="0 0 {W:.0f} {H:.0f}">'), than, count=1)
    than = than.replace("</svg>", f'<g class="lop-co-che">{"".join(lop)}</g></svg>')
    than = than.replace("<svg", '<svg role="img"', 1)
    out.svg = than if "<rect" in than[:600] else than.replace(">", f'><rect width="{W:.0f}" height="{H:.0f}" fill="#fff"/>', 1)
    return out


def ve_svg(cc: CoChe) -> str:
    return ve(cc).svg


# ================================================================ ChemDraw (.cdxml)
def _cung_tron(s, c, e):
    """Bézier bậc 2 (s, c, e) → cung tròn qua s và e có cùng độ phồng: (tâm, bán kính, góc (độ, có dấu))."""
    (sx, sy), (cx, cy), (ex, ey) = s, c, e
    mx, my = (sx + ex) / 2, (sy + ey) / 2
    dx, dy = ex - sx, ey - sy
    day = math.hypot(dx, dy) or 1.0
    nx, ny = -dy / day, dx / day
    ben = (cx - mx) * nx + (cy - my) * ny          # độ lệch điểm điều khiển (có dấu)
    sag = max(abs(ben) / 2, 1.0)                    # độ phồng của Bézier = 1/2 độ lệch điểm điều khiển
    r = (day ** 2 / 4 + sag ** 2) / (2 * sag)
    dau = 1 if ben > 0 else -1
    tx, ty = mx - dau * nx * (r - sag), my - dau * ny * (r - sag)
    a0 = math.atan2(sy - ty, sx - tx)
    a1 = math.atan2(ey - ty, ex - tx)
    goc = math.degrees(a1 - a0)
    goc = (goc + 180) % 360 - 180
    # ChemDraw (đã thử trên ChemDraw 20): cung dựng từ Head3D quanh Center3D; AngularSize > 0 = đi từ đuôi tới đầu NGƯỢC chiều
    # kim đồng hồ trên màn hình (toạ độ y hướng xuống) → đổi dấu góc đo theo toạ độ màn hình
    return (tx, ty), r, -goc


def xuat_cdxml(cc: CoChe, hinh: _Ve | None = None) -> str:
    """Xuất cơ chế ra CDXML: phân tử (node/bond có toạ độ, điện tích, liên kết nét đứt), mũi tên cong (<arrow> dạng cung,
    đầu Full), mũi tên phản ứng, điều kiện, dấu +, ngoặc vuông + ‡, cặp e (2 chấm tròn), δ."""
    hinh = hinh or ve(cc)
    k = 14.4 / hinh.bond_px                      # px → point (liên kết chuẩn ChemDraw 14.4 pt)
    ids = iter(range(1000, 100000))
    objs: list[str] = []

    def P(x, y):
        return f"{x * k:.2f} {y * k:.2f}"

    # phân tử: mỗi mảnh nối nhau là một <fragment>
    n = len(hinh.nguyen_tu)
    ke: dict[int, set[int]] = {i: set() for i in range(n)}
    for b in hinh.lien_ket:
        ke[b["a"]].add(b["b"])
        ke[b["b"]].add(b["a"])
    da = set()
    node = {}
    for goc in range(n):
        if goc in da:
            continue
        nhom, st = [], [goc]
        while st:
            i = st.pop()
            if i in da:
                continue
            da.add(i)
            nhom.append(i)
            st.extend(ke[i] - da)
        parts = [f'<fragment id="{next(ids)}">']
        for i in sorted(nhom):
            nt = hinh.nguyen_tu[i]
            at = nt["atom"]
            nid = next(ids)
            node[i] = nid
            attrs = [f'id="{nid}"', f'p="{P(nt["x"], nt["y"])}"']
            if at.GetAtomicNum() != 6:
                attrs.append(f'Element="{at.GetAtomicNum()}"')
            if at.GetFormalCharge():
                attrs.append(f'Charge="{at.GetFormalCharge()}"')
            # như hình SVG (RDKit): nguyên tử đứng riêng hoặc liên kết đều đi sang phải → chữ H đứng trước (HO⁻, HO–)
            ve_trai = not ke[i] or all(hinh.nguyen_tu[j]["x"] > nt["x"] + 1 for j in ke[i])
            nhan = _nhan_nguyen_tu(at, ve_trai)
            if nhan is None and (nt["nhan"] or not ke[i]):
                nhan = "C" + ("" if not at.GetTotalNumHs() else "H" if at.GetTotalNumHs() == 1 else f"H{at.GetTotalNumHs()}")
            if nhan is None:
                parts.append(f"<n {' '.join(attrs)}/>")
                continue
            attrs.append(f'NumHydrogens="{at.GetTotalNumHs()}"')
            sym = at.GetSymbol()
            truoc = nhan[: nhan.index(sym)] if sym in nhan else ""
            tx = nt["x"] * k - _rong_chu(truoc) - _rong_chu(sym[0]) / 2
            just = ' LabelJustification="Right"' if truoc else ""
            parts.append(f'<n {" ".join(attrs)}><t p="{tx:.2f} {nt["y"] * k + 3.5:.2f}"{just}>'
                         f'<s font="3" size="10" face="96">{escape(nhan)}</s></t></n>')
        for b in hinh.lien_ket:
            if b["a"] in node and b["b"] in node and b["a"] in nhom:
                bac = {2: ' Order="2"', 3: ' Order="3"'}.get(b["bac"], "")
                dut = ' Display="Dash"' if b["dut"] else ""
                parts.append(f'<b id="{next(ids)}" B="{node[b["a"]]}" E="{node[b["b"]]}"{bac}{dut}/>')
        parts.append("</fragment>")
        objs.append("".join(parts))

    # mũi tên cong: <arrow> dạng cung tròn (AngularSize + Center3D + trục), đầu mũi tên đầy (hoặc nửa đầu)
    for m in hinh.mui_ten_cong:
        (tx, ty), r, goc = _cung_tron(m["s"], m["c"], m["e"])
        sx, sy = m["s"]
        ex, ey = m["e"]
        xs, ys = [sx, ex, m["c"][0]], [sy, ey, m["c"][1]]
        dau_kieu = "HalfLeft" if m.get("kieu") == "nua_dau" else "Full"
        objs.append(
            f'<arrow id="{next(ids)}" BoundingBox="{min(xs) * k:.2f} {min(ys) * k:.2f} {max(xs) * k:.2f} {max(ys) * k:.2f}" '
            f'FillType="None" ArrowheadHead="{dau_kieu}" ArrowheadType="Solid" HeadSize="1000" ArrowheadCenterSize="875" '
            f'ArrowheadWidth="250" AngularSize="{goc:.2f}" Head3D="{P(ex, ey)} 0" Tail3D="{P(sx, sy)} 0" '
            f'Center3D="{P(tx, ty)} 0" MajorAxisEnd3D="{P(tx + r, ty)} 0" MinorAxisEnd3D="{P(tx, ty + r)} 0" color="4"/>'
        )
    # mũi tên phản ứng + chữ
    for m in hinh.mui_ten_pu:
        objs.append(
            f'<arrow id="{next(ids)}" BoundingBox="{m["x0"] * k:.2f} {(m["y"] - 3) * k:.2f} {m["x1"] * k:.2f} {(m["y"] + 3) * k:.2f}" '
            f'FillType="None" ArrowheadHead="Full" ArrowheadType="Solid" HeadSize="1000" ArrowheadCenterSize="875" '
            f'ArrowheadWidth="250" Head3D="{P(m["x1"], m["y"])} 0" Tail3D="{P(m["x0"], m["y"])} 0"/>'
        )
    for t in hinh.chu:
        just = {"middle": ' Justification="Center"', "start": "", "end": ' Justification="Right"'}[t["can"]]
        objs.append(f'<t id="{next(ids)}" p="{P(t["x"], t["y"])}"{just} InterpretChemically="no">'
                    f'<s font="3" size="{max(7.0, t["size"] * k * 0.95):.1f}" face="0">{escape(t["text"])}</s></t>')
    for g in hinh.ngoac:
        objs.append(f'<graphic id="{next(ids)}" BoundingBox="{g["x0"] * k:.2f} {g["y0"] * k:.2f} {g["x1"] * k:.2f} {g["y1"] * k:.2f}" '
                    f'GraphicType="Bracket" BracketType="SquarePair"/>')
    for c in hinh.cap_e:  # cặp e = 2 chấm tròn tô đặc (ChemDraw 20 không hiện ký hiệu SymbolType="LonePair" đứng riêng)
        nx, ny = -math.sin(math.radians(c["goc"])), math.cos(math.radians(c["goc"]))
        for dau in (-1, 1):
            x, y = (c["x"] + nx * 2.9 * dau) * k, (c["y"] + ny * 2.9 * dau) * k
            r = 0.95
            objs.append(f'<graphic id="{next(ids)}" BoundingBox="{x - r:.2f} {y - r:.2f} {x + r:.2f} {y + r:.2f}" GraphicType="Oval" '
                        f'OvalType="Filled" Center3D="{x:.2f} {y:.2f} 0" MajorAxisEnd3D="{x + r:.2f} {y:.2f} 0" '
                        f'MinorAxisEnd3D="{x:.2f} {y + r:.2f} 0"/>')
    W, H = hinh.rong * k, hinh.cao * k
    return (
        '<?xml version="1.0" encoding="UTF-8" ?>\n'
        '<!DOCTYPE CDXML SYSTEM "http://www.cambridgesoft.com/xml/cdxml.dtd" >\n'
        f'<CDXML CreationProgram="CHEMISTRY" Name="{escape(cc.ten)}" BoundingBox="0 0 {W:.0f} {H:.0f}" '
        'LabelFont="3" LabelSize="10" LabelFace="96" CaptionFont="3" CaptionSize="10" HashSpacing="2.7" MarginWidth="1.6" '
        'LineWidth="0.6" BoldWidth="2" BondLength="14.4" BondSpacing="18" ChainAngle="120">\n'
        '<colortable><color r="1" g="1" b="1"/><color r="0" g="0" b="0"/><color r="0.753" g="0.224" b="0.169"/></colortable>\n'
        '<fonttable><font id="3" charset="utf-8" name="Arial"/></fonttable>\n'
        f'<page id="1" BoundingBox="0 0 {W:.0f} {H:.0f}" HeaderPosition="36" FooterPosition="36" WidthPages="1" '
        f'HeightPages="{max(1, int(H // 720) + 1)}">\n' + "\n".join(objs) + "\n</page>\n</CDXML>\n"
    )
