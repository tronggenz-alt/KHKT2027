"""ChemDraw: sinh file .cdxml sơ đồ chuỗi (cấu tạo + mũi tên + điều kiện) và mở bằng ChemDraw trên macOS."""
from __future__ import annotations

import logging
import re
import subprocess
from pathlib import Path
from xml.sax.saxutils import escape

from rdkit import Chem

from .renderer import chuan_bi_mol, doan_so_do

log = logging.getLogger(__name__)

BOND_PT = 14.4                 # độ dài liên kết mặc định của ChemDraw (point)
SCALE = BOND_PT / 1.5          # toạ độ RDKit: liên kết dài ~1.5
TRANG_RONG = 540.0             # bề rộng vùng vẽ (7.5 inch)
LE = 36.0
CO_CHU = 10.0                  # cỡ chữ nhãn nguyên tử


def _rong_chu(text: str, size: float = CO_CHU) -> float:
    """Ước lượng bề rộng chuỗi (Arial) theo point."""
    w = 0.0
    for ch in text:
        if ch.isdigit():
            w += 0.42
        elif ch.isupper():
            w += 0.70
        elif ch in " ,.()°":
            w += 0.30
        else:
            w += 0.52
    return w * size


def _gach_dong(text: str, toi_da: int) -> list[str]:
    words, dong, cur = text.split(), [], ""
    for w in words:
        if cur and len(cur) + 1 + len(w) > toi_da:
            dong.append(cur)
            cur = w
        else:
            cur = f"{cur} {w}".strip()
    if cur:
        dong.append(cur)
    return dong or [""]


_CONG_THUC = re.compile(r"[A-Z][A-Za-z]*\d[A-Za-z0-9()]*|\([A-Z][A-Za-z0-9]*\)\d*")


def _runs(text: str, size: float) -> str:
    """Chia văn bản thành các <s>: công thức (H2SO4) dùng kiểu Formula (face=96) để số thành chỉ số dưới."""
    out, pos = [], 0
    for m in _CONG_THUC.finditer(text):
        if m.start() > pos:
            out.append((text[pos : m.start()], 0))
        out.append((m.group(0), 96))
        pos = m.end()
    if pos < len(text):
        out.append((text[pos:], 0))
    return "".join(f'<s font="3" size="{size:g}" face="{face}">{escape(t)}</s>' for t, face in out if t)


class _Ids:
    def __init__(self):
        self.n = 1000

    def __call__(self) -> int:
        self.n += 1
        return self.n


def _nhan_nguyen_tu(atom, ve_trai: bool) -> str | None:
    """Nhãn hiển thị của nút (None = để dạng khung)."""
    sym = atom.GetSymbol()
    h = atom.GetTotalNumHs()
    q = atom.GetFormalCharge()
    if atom.GetAtomicNum() == 0:
        return "*"
    if sym == "C" and q == 0 and atom.GetDegree() > 0:
        return None  # công thức khung: C (và H gắn C) không ghi chữ
    phan_h = "" if h == 0 else ("H" if h == 1 else f"H{h}")
    dau = "" if q == 0 else ("+" if q == 1 else "-" if q == -1 else f"{abs(q)}{'+' if q > 0 else '-'}")
    if sym == "C":
        return f"C{phan_h}{dau}"
    return f"{phan_h}{sym}{dau}" if ve_trai and phan_h else f"{sym}{phan_h}{dau}"


def _manh_cdxml(smiles: str, x0: float, yc: float, ids: _Ids, cong_thuc: str = "", mol=None) -> tuple[str, float, float, float]:
    """Một chất → các <fragment>. Trả về (xml, bề rộng, nửa chiều cao trên, nửa chiều cao dưới).

    mol: mol đã chuẩn bị sẵn (đã lật theo chất trước trong chuỗi); không có thì tự chuẩn bị từ SMILES.
    """
    mol = mol if mol is not None else chuan_bi_mol(smiles, viet_tat=False, cong_thuc=cong_thuc)
    if mol is None:
        return "", 0.0, 0.0, 0.0
    try:
        Chem.Kekulize(mol, clearAromaticFlags=True)
    except Exception:  # noqa: BLE001
        pass
    conf = mol.GetConformer()
    pts = [(conf.GetAtomPosition(i).x * SCALE, -conf.GetAtomPosition(i).y * SCALE) for i in range(mol.GetNumAtoms())]
    pad = 10.0  # chừa chỗ cho nhãn ở hai đầu
    minx, maxx = min(p[0] for p in pts), max(p[0] for p in pts)
    miny, maxy = min(p[1] for p in pts), max(p[1] for p in pts)
    cy = (miny + maxy) / 2
    dx, dy = x0 + pad - minx, yc - cy
    pos = [(x + dx, y + dy) for x, y in pts]

    node_id = {}
    xml = []
    for frag in Chem.GetMolFrags(mol):
        fid = ids()
        parts = [f'<fragment id="{fid}">']
        for i in frag:
            a = mol.GetAtomWithIdx(i)
            nid = ids()
            node_id[i] = nid
            x, y = pos[i]
            attrs = [f'id="{nid}"', f'p="{x:.2f} {y:.2f}"']
            if a.GetAtomicNum() == 0:
                attrs.append('NodeType="ExternalConnectionPoint"')
            elif a.GetAtomicNum() != 6:
                attrs.append(f'Element="{a.GetAtomicNum()}"')
            if a.GetFormalCharge():
                attrs.append(f'Charge="{a.GetFormalCharge()}"')
            ve_trai = bool(a.GetNeighbors()) and all(pos[nb.GetIdx()][0] > x + 1 for nb in a.GetNeighbors())
            nhan = _nhan_nguyen_tu(a, ve_trai)
            if nhan is not None and a.GetAtomicNum() != 0:
                attrs.append(f'NumHydrogens="{a.GetTotalNumHs()}"')
            if nhan is None:
                parts.append(f"<n {' '.join(attrs)}/>")
                continue
            # Đặt chữ sao cho kí hiệu nguyên tố nằm đúng vị trí nút
            sym = "*" if a.GetAtomicNum() == 0 else a.GetSymbol()
            truoc = nhan[: nhan.index(sym)] if sym in nhan else ""
            tx = x - _rong_chu(truoc) - _rong_chu(sym[0]) / 2
            just = ' LabelJustification="Right"' if truoc else ""
            parts.append(
                f"<n {' '.join(attrs)}><t p=\"{tx:.2f} {y + 3.5:.2f}\"{just}>"
                f'<s font="3" size="{CO_CHU:g}" face="96">{escape(nhan)}</s></t></n>'
            )
        for bond in mol.GetBonds():
            i, j = bond.GetBeginAtomIdx(), bond.GetEndAtomIdx()
            if i not in frag:
                continue
            order = {2.0: ' Order="2"', 3.0: ' Order="3"'}.get(bond.GetBondTypeAsDouble(), "")
            parts.append(f'<b id="{ids()}" B="{node_id[i]}" E="{node_id[j]}"{order}/>')
        parts.append("</fragment>")
        xml.append("".join(parts))
    rong = (maxx - minx) + 2 * pad
    return "\n".join(xml), rong, cy - miny + 8, maxy - cy + 8


def tao_cdxml(sol, path: Path) -> bool:
    """Xuất sơ đồ chuỗi ra .cdxml. Trả về False nếu không có chất nào vẽ được."""
    ids = _Ids()
    objs: list[str] = []
    y = LE + 40
    co_chat = False
    for doan in doan_so_do(sol):
        x = LE
        hang: list[tuple[str, object]] = [("nut", doan["dau"])]
        for buoc, sau in doan["buoc"]:
            hang += [("ten", buoc), ("nut", sau)]
        # chuẩn bị hình từng chất (chất sau lật theo khung chung với chất trước) + ước lượng chiều cao hàng = chất cao nhất
        cao = 60.0
        mols: dict[int, object] = {}
        mau = None
        for loai, obj in hang:
            if loai == "nut":
                m = chuan_bi_mol(obj.smiles, viet_tat=False, cong_thuc=obj.cong_thuc, mau=mau)
                mols[id(obj)] = m
                mau = m if m is not None else mau
                if m is not None:
                    c = m.GetConformer()
                    ys = [c.GetAtomPosition(i).y * SCALE for i in range(m.GetNumAtoms())]
                    cao = max(cao, max(ys) - min(ys) + 50)
        yc = y + cao / 2
        for loai, obj in hang:
            if loai == "nut":
                xml, rong, tren, duoi = _manh_cdxml(obj.smiles, x, yc, ids, obj.cong_thuc, mols.get(id(obj)))
                if not xml:  # SMILES lỗi: chỉ ghi kí hiệu
                    rong, duoi = _rong_chu(obj.ki_hieu, 12) + 10, 8
                    objs.append(f'<t id="{ids()}" p="{x + 5:.2f} {yc + 4:.2f}">{_runs(obj.ki_hieu, 12)}</t>')
                else:
                    co_chat = True
                    objs.append(xml)
                ten = f"{obj.ki_hieu}: {obj.ten}" if obj.ki_hieu and obj.ki_hieu != obj.cong_thuc and len(obj.ki_hieu) <= 3 else obj.ten
                objs.append(
                    f'<t id="{ids()}" p="{x + rong / 2:.2f} {yc + duoi + 12:.2f}" Justification="Center" '
                    f'InterpretChemically="no">{_runs(ten or obj.ki_hieu, 8)}</t>'
                )
                x += rong + 6
            else:
                buoc = obj
                dk = f"({buoc.so}) {buoc.dieu_kien}".strip()
                dong = _gach_dong(dk, 22)
                dai = max(54.0, max(_rong_chu(d, 8) for d in dong) + 12)
                if x + dai + 60 > TRANG_RONG + LE:  # xuống hàng, mũi tên mở đầu hàng mới
                    y += cao + 60
                    yc = y + cao / 2
                    x = LE
                aid = ids()
                objs.append(
                    f'<arrow id="{aid}" BoundingBox="{x:.2f} {yc - 3:.2f} {x + dai:.2f} {yc + 3:.2f}" FillType="None" '
                    f'ArrowheadHead="Full" ArrowheadType="Solid" HeadSize="1000" ArrowheadCenterSize="875" '
                    f'ArrowheadWidth="250" Head3D="{x + dai:.2f} {yc:.2f} 0" Tail3D="{x:.2f} {yc:.2f} 0"/>'
                )
                for k, d in enumerate(reversed(dong)):
                    objs.append(
                        f'<t id="{ids()}" p="{x + dai / 2:.2f} {yc - 6 - 10 * k:.2f}" Justification="Center" '
                        f'InterpretChemically="no">{_runs(d, 8)}</t>'
                    )
                x += dai + 6
        y += cao + 60
    if not co_chat:
        return False
    cao_trang = y + LE
    doc = (
        '<?xml version="1.0" encoding="UTF-8" ?>\n'
        '<!DOCTYPE CDXML SYSTEM "http://www.cambridgesoft.com/xml/cdxml.dtd" >\n'
        f'<CDXML CreationProgram="CHEMISTRY" BoundingBox="0 0 {TRANG_RONG + 2 * LE:.0f} {cao_trang:.0f}" '
        'LabelFont="3" LabelSize="10" LabelFace="96" CaptionFont="3" CaptionSize="10" HashSpacing="2.7" '
        'MarginWidth="1.6" LineWidth="0.6" BoldWidth="2" BondLength="14.4" BondSpacing="18" ChainAngle="120">\n'
        '<fonttable><font id="3" charset="utf-8" name="Arial"/></fonttable>\n'
        f'<page id="1" BoundingBox="0 0 {TRANG_RONG + 2 * LE:.0f} {cao_trang:.0f}" HeaderPosition="36" FooterPosition="36" '
        f'WidthPages="1" HeightPages="{max(1, int(cao_trang // 720) + 1)}">\n'
        + "\n".join(objs)
        + "\n</page>\n</CDXML>\n"
    )
    path.write_text(doc, encoding="utf-8")
    return True


# ---------------------------------------------------------------- mở bằng ChemDraw
def tim_chemdraw() -> str | None:
    """Đường dẫn ứng dụng ChemDraw (vd /Applications/ChemDraw 20.0.app) hoặc None."""
    for goc in (Path("/Applications"), Path.home() / "Applications"):
        if goc.exists():
            for app in sorted(goc.glob("ChemDraw*.app"), reverse=True):
                return str(app)
    try:
        out = subprocess.run(
            ["mdfind", "kMDItemCFBundleIdentifier == 'com.perkinelmer.ChemDraw*' || kMDItemCFBundleIdentifier == 'com.cambridgesoft.ChemDraw*'"],
            capture_output=True, text=True, timeout=5,
        ).stdout.split("\n")
        return next((p for p in out if p.endswith(".app")), None)
    except Exception:  # noqa: BLE001
        return None


def mo_bang_chemdraw(path: Path) -> dict:
    """Chạy `open -a ChemDraw <file>`; không có ChemDraw thì trả thông báo nhẹ nhàng."""
    if not path.exists():
        return {"ok": False, "thong_bao": "Chưa có file để mở."}
    r = subprocess.run(["open", "-a", "ChemDraw", str(path)], capture_output=True, text=True, timeout=15)
    if r.returncode == 0:
        return {"ok": True, "thong_bao": f"Đã mở {path.name} bằng ChemDraw."}
    app = tim_chemdraw()
    if app:
        r = subprocess.run(["open", "-a", app, str(path)], capture_output=True, text=True, timeout=15)
        if r.returncode == 0:
            return {"ok": True, "thong_bao": f"Đã mở {path.name} bằng {Path(app).stem}."}
    log.info("Không mở được ChemDraw: %s", r.stderr.strip())
    return {"ok": False, "thong_bao": "Không tìm thấy ChemDraw trên máy. Bạn vẫn có thể tải ZIP và mở file .cdxml/.sdf bằng phần mềm khác."}
