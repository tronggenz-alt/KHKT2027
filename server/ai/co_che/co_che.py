"""Lõi cơ chế phản ứng của VERA Lab (lấy từ backend/co_che.py của app CHEMISTRY, bỏ phần gọi AI): định dạng cơ chế,
bộ đẩy electron và kiểm tra tự động bằng RDKit (bảo toàn nguyên tử, điện tích, hoá trị).

Định dạng một cơ chế (JSON; SMILES có atom map để mũi tên trỏ đúng nguyên tử, giữ lập thể @, @@, /, \\):
{
  "ten": "SN2", "loai": "SN2", "nguon": "ai", "dieu_kien": "...", "giai_thich": "vì sao chọn vị trí đó, sản phẩm chính/phụ",
  "nhan_hien_thi": [{"map": 9, "nhan": "B:"}],                       # nguyên tử giả [*:9] hiện là R, Ar, B:, Nu⁻, E⁺...
  "buoc": [{"chat": ["[OH-:1]", "[CH3:2][CH2:3][Br:4]"],
            "mui_ten": [{"tu": {"loai": "cap_e", "nguyen_tu": 1}, "den": {"loai": "nguyen_tu", "nguyen_tu": 3}, "kieu": "day_du"},
                        {"tu": {"loai": "lien_ket", "nguyen_tu": [3, 4]}, "den": {"loai": "nguyen_tu", "nguyen_tu": 4}}],
            "trang_thai_chuyen_tiep": true, "buoc_cham": false, "thuan_nghich": false, "nhan_trung_gian": "",
            "cong_huong": ["SMILES các cấu trúc cộng hưởng của tiểu phân trong bước"], "chuyen_h": [{"tu": 5, "den": 7}],
            "ghi_chu": "...", "dieu_kien": "", "delta": {"3": "+", "4": "-"}}],
  "san_pham": ["CCO", "[Br-]"]
}
Mũi tên: kieu "day_du" = một CẶP electron, "nua_dau" (fish-hook) = MỘT electron (gốc tự do), "vong" = cặp e trong vòng pericyclic.
Bộ đẩy electron đếm electron: nguồn (cặp e/e độc thân của nguyên tử, hoặc electron của liên kết) mất n e; đích nhận n e:
  nguyên tử → nguyên tử khác   : electron vào liên kết mới giữa hai nguyên tử
  liên kết → một đầu của nó     : electron thành cặp e/e độc thân của nguyên tử đó
  bất kỳ → liên kết [X, Y]      : electron vào liên kết X–Y (tăng bậc/tạo mới)
Liên kết phải còn số electron chẵn; điện tích hình thức và số e độc thân tự tính lại. "chuyen_h": chuyển H⁺ rút gọn (~H⁺).
Số H của nguyên tử có map giữ nguyên trừ chuyen_h; H nào di chuyển bằng mũi tên phải là nguyên tử riêng ([H:5]).
"""
from __future__ import annotations

import logging
import re
from collections import Counter
from dataclasses import dataclass, field

from rdkit import Chem, RDLogger


log = logging.getLogger(__name__)
RDLogger.DisableLog("rdApp.*")

SO_LAN_SUA = 3
KIEU_MUI_TEN = ("day_du", "nua_dau", "vong")
LOAI_CO_CHE = {  # gợi ý loại cơ chế người dùng chọn (không bắt buộc) → tên gửi kèm cho AI
    "SN1": "thế nucleophin SN1", "SN2": "thế nucleophin SN2", "E1": "tách E1", "E2": "tách E2", "E1CB": "tách E1cB",
    "AE": "cộng electrophin vào liên kết bội", "SEAR": "thế electrophin vòng thơm", "SNAR": "thế nucleophin vòng thơm",
    "AN": "cộng nucleophin vào nhóm C=O", "THE_ACYL": "thế nucleophin ở nhóm axyl (este hoá, thuỷ phân…)",
    "GOC_TU_DO": "gốc tự do", "PERICYCLIC": "pericyclic (Diels–Alder, chuyển vị sigmatropic…)", "CHUYEN_VI": "có chuyển vị",
}


class LoiCoChe(ValueError):
    """Lỗi hiện cho người dùng (tiếng Việt)."""

    def __init__(self, thong_diep: str, loai: str = "co_che"):
        super().__init__(thong_diep)
        self.thong_diep, self.loai = thong_diep, loai


# ================================================================ trạng thái phân tử (gộp mọi chất của một bước)
# Các bước giữ dạng Kekulé cố định (không cho RDKit tự chuyển sang thơm rồi Kekulé hoá lại theo cách khác) để mũi tên
# từ "liên kết đôi C1=C2" của vòng benzen luôn trỏ đúng liên kết; chỉ khi so sánh trạng thái mới quy về dạng thơm.
_KEKULE = Chem.SanitizeFlags.SANITIZE_ALL ^ Chem.SanitizeFlags.SANITIZE_SETAROMATICITY
_BANG_TUAN_HOAN = Chem.GetPeriodicTable()


def _mol(smiles: str):
    m = Chem.MolFromSmiles(smiles, sanitize=False)
    if m is None:
        return None
    try:
        Chem.SanitizeMol(m, _KEKULE)
    except Exception:  # noqa: BLE001
        return None
    return m


def tu_smiles(ds: list[str]) -> Chem.RWMol:
    """Danh sách SMILES có map → một RWMol (nhiều mảnh). Lỗi → LoiCoChe nêu SMILES sai."""
    tong = Chem.RWMol()
    for s in ds:
        m = _mol(s)
        if m is None:
            raise LoiCoChe(f"SMILES không hợp lệ (cú pháp hoặc hoá trị): {s}")
        tong.InsertMol(m)
    return tong


def ban_do(mol) -> dict[int, int]:
    """atom map → chỉ số nguyên tử."""
    return {a.GetAtomMapNum(): a.GetIdx() for a in mol.GetAtoms() if a.GetAtomMapNum()}


def cac_manh_smiles(mol, giu_map: bool = True) -> list[str]:
    """Các mảnh của trạng thái → SMILES. giu_map: dạng Kekulé có map (dữ liệu cơ chế); không: SMILES chuẩn (thơm, không map)."""
    out = []
    for frag in Chem.GetMolFrags(mol, asMols=True, sanitizeFrags=False):
        m = Chem.Mol(frag)
        try:
            if giu_map:
                Chem.SanitizeMol(m, _KEKULE)
                s = Chem.MolToSmiles(m, kekuleSmiles=True)
            else:
                for a in m.GetAtoms():
                    a.SetAtomMapNum(0)
                Chem.SanitizeMol(m)
                s = Chem.MolToSmiles(Chem.MolFromSmiles(Chem.MolToSmiles(m)))  # bỏ H tường minh không cần thiết
        except Exception:  # noqa: BLE001 – không hợp hoá trị
            s = Chem.MolToSmiles(m)
        out.append(s)
    return out


def _cap(a: int, b: int) -> tuple[int, int]:
    return (min(a, b), max(a, b))


@dataclass
class MuiTen:
    tu_loai: str            # "cap_e" | "lien_ket"
    tu: tuple[int, ...]     # (A,) hoặc (A, B) – atom map
    den_loai: str           # "nguyen_tu" | "lien_ket"
    den: tuple[int, ...]
    kieu: str = "day_du"    # "day_du" (2 e) | "nua_dau" (1 e, fish-hook) | "vong" (2 e, pericyclic)

    @classmethod
    def tu_dict(cls, d: dict) -> "MuiTen":
        def doc(x, ten):
            if not isinstance(x, dict) or "loai" not in x or "nguyen_tu" not in x:
                raise LoiCoChe(f"Mũi tên thiếu '{ten}.loai' hoặc '{ten}.nguyen_tu': {d}")
            nt = x["nguyen_tu"]
            nt = tuple(int(v) for v in (nt if isinstance(nt, (list, tuple)) else [nt]))
            return x["loai"], nt

        tl, t = doc(d.get("tu"), "tu")
        dl, de = doc(d.get("den"), "den")
        kieu = str(d.get("kieu") or "day_du")
        if tl not in ("cap_e", "lien_ket") or dl not in ("nguyen_tu", "lien_ket") or kieu not in KIEU_MUI_TEN:
            raise LoiCoChe(f"Loại đầu/cuối hoặc kiểu mũi tên không hợp lệ: {d}")
        if (tl == "cap_e") != (len(t) == 1) or (dl == "nguyen_tu") != (len(de) == 1):
            raise LoiCoChe(f"Mũi tên: cap_e/nguyen_tu cần 1 nguyên tử, lien_ket cần 2 nguyên tử: {d}")
        return cls(tl, t, dl, de, kieu)

    def to_dict(self) -> dict:
        def ghi(loai, nt):
            return {"loai": loai, "nguyen_tu": nt[0] if len(nt) == 1 else list(nt)}
        return {"tu": ghi(self.tu_loai, self.tu), "den": ghi(self.den_loai, self.den), "kieu": self.kieu}

    @property
    def so_e(self) -> int:
        return 1 if self.kieu == "nua_dau" else 2

    def dong_e(self) -> tuple[dict[tuple[int, int], int], dict[int, int]]:
        """Electron mũi tên chuyển: ({liên kết: ±e}, {nguyên tử: ±e không liên kết})."""
        n = self.so_e
        lk: dict[tuple[int, int], int] = {}
        nb: dict[int, int] = {}

        def cong(d, k, v):
            d[k] = d.get(k, 0) + v

        if self.tu_loai == "cap_e":
            (a,) = self.tu
            cong(nb, a, -n)
        else:
            a, b = self.tu
            if a == b:
                raise LoiCoChe(f"Liên kết nguồn {a}–{b} không hợp lệ")
            cong(lk, _cap(a, b), -n)
        if self.den_loai == "nguyen_tu":
            (c,) = self.den
            if self.tu_loai == "cap_e":
                if c == a:
                    raise LoiCoChe(f"Mũi tên không được trỏ về chính nguyên tử xuất phát: {self.to_dict()}")
                cong(lk, _cap(a, c), n)  # cặp e / e độc thân của A tạo liên kết A–C
            else:
                if c not in self.tu:
                    raise LoiCoChe(f"Mũi tên từ liên kết {self.tu[0]}–{self.tu[1]} tới nguyên tử {c} ngoài liên kết: hãy dùng "
                                   f"den loai lien_ket [{self.tu[0]} hoặc {self.tu[1]}, {c}] để chỉ rõ liên kết mới")
                cong(nb, c, n)
        else:
            x, y = self.den
            if x == y:
                raise LoiCoChe(f"Liên kết đích {x}–{y} không hợp lệ")
            if self.tu_loai == "cap_e" and self.tu[0] not in self.den:
                raise LoiCoChe(f"Cặp e của nguyên tử {self.tu[0]} chỉ có thể tới liên kết chứa chính nó: {self.to_dict()}")
            if self.tu_loai == "lien_ket" and (set(self.tu) == set(self.den) or not set(self.tu) & set(self.den)):
                raise LoiCoChe(f"Mũi tên giữa hai liên kết phải có đúng một nguyên tử chung: {self.to_dict()}")
            cong(lk, _cap(x, y), n)
        return lk, nb


_BAC = {1: Chem.BondType.SINGLE, 2: Chem.BondType.DOUBLE, 3: Chem.BondType.TRIPLE}


def _bac_lk(mol, i: int) -> int:
    return int(round(sum(b.GetBondTypeAsDouble() for b in mol.GetAtomWithIdx(i).GetBonds())))


def lien_ket_doi(mui_ten: list[MuiTen], chuyen_h=()) -> set[int]:
    """Các nguyên tử có liên kết thay đổi (trung tâm phản ứng) trong một bước."""
    out: set[int] = set()
    for mt in mui_ten:
        lk, _ = mt.dong_e()
        for a, b in lk:
            out |= {a, b}
    for x, y in chuyen_h:
        out |= {x, y}
    return out


def ap_mui_ten(mol, mui_ten: list[MuiTen], kiem_hoa_tri: bool = True, chuyen_h=()) -> Chem.RWMol:
    """Áp các mũi tên (và chuyển proton rút gọn) của một bước lên trạng thái → trạng thái sau."""
    rw = Chem.RWMol(mol)
    try:
        Chem.Kekulize(rw, clearAromaticFlags=True)
    except Exception:  # noqa: BLE001
        pass
    bd = ban_do(rw)
    tong_lk: dict[tuple[int, int], int] = {}
    tong_nb: dict[int, int] = {}
    for mt in mui_ten:
        for m in set(mt.tu) | set(mt.den):
            if m not in bd:
                raise LoiCoChe(f"Mũi tên nhắc tới nguyên tử map {m} không có trong các chất của bước này")
        lk, nb = mt.dong_e()
        for k, v in lk.items():
            tong_lk[k] = tong_lk.get(k, 0) + v
        for k, v in nb.items():
            tong_nb[k] = tong_nb.get(k, 0) + v
    # electron không liên kết ban đầu (để kiểm tra "không còn electron để cho" và tính e độc thân)
    nb0: dict[int, int | None] = {}
    for a in rw.GetAtoms():
        v = _BANG_TUAN_HOAN.GetNOuterElecs(a.GetAtomicNum()) if a.GetAtomicNum() > 0 else None
        nb0[a.GetIdx()] = None if v is None else v - a.GetFormalCharge() - _bac_lk(rw, a.GetIdx()) - a.GetTotalNumHs()
    d_bac: dict[int, int] = {}
    for (a, b), de in tong_lk.items():
        if de == 0:
            continue
        ia, ib = bd[a], bd[b]
        bond = rw.GetBondBetweenAtoms(ia, ib)
        e = (int(bond.GetBondTypeAsDouble()) * 2 if bond else 0) + de
        if e < 0:
            raise LoiCoChe(f"Mũi tên lấy electron từ liên kết {a}–{b} không tồn tại (hoặc lấy quá số electron của nó)")
        if e % 2:
            raise LoiCoChe(f"Liên kết {a}–{b} còn lẻ electron – thiếu mũi tên nửa đầu (1 e) để ghép cặp")
        bac = e // 2
        if bac > 3:
            raise LoiCoChe(f"Liên kết {a}–{b} vượt quá bậc 3")
        cu = int(bond.GetBondTypeAsDouble()) if bond else 0
        for i in (ia, ib):
            d_bac[i] = d_bac.get(i, 0) + bac - cu
        if bond is None:
            rw.AddBond(ia, ib, _BAC[bac])
        elif bac == 0:
            rw.RemoveBond(ia, ib)
        else:
            bond.SetBondType(_BAC[bac])
    for i in set(d_bac) | {bd[m] for m in tong_nb}:
        a = rw.GetAtomWithIdx(i)
        dnb = tong_nb.get(a.GetAtomMapNum(), 0)
        a.SetFormalCharge(a.GetFormalCharge() - dnb - d_bac.get(i, 0))
        if nb0[i] is not None:
            nb = nb0[i] + dnb
            if nb < 0:
                raise LoiCoChe(f"Nguyên tử {a.GetSymbol()}{a.GetAtomMapNum()} không còn electron không liên kết để cho")
            a.SetNumRadicalElectrons(nb % 2)
        a.SetChiralTag(Chem.ChiralType.CHI_UNSPECIFIED)  # trung tâm phản ứng: cấu hình mới do bước sau ghi
    for x, y in chuyen_h:  # chuyển H⁺ rút gọn: X mất H (giữ cặp e, điện tích −1), Y nhận H⁺ (điện tích +1)
        if x not in bd or y not in bd:
            raise LoiCoChe(f"Chuyển proton nhắc tới nguyên tử map {x if x not in bd else y} không có trong bước này")
        ax, ay = rw.GetAtomWithIdx(bd[x]), rw.GetAtomWithIdx(bd[y])
        if ax.GetTotalNumHs() < 1:
            raise LoiCoChe(f"Chuyển proton: nguyên tử {x} không có H để cho")
        ax.SetNumExplicitHs(ax.GetTotalNumHs() - 1)
        ax.SetFormalCharge(ax.GetFormalCharge() - 1)
        ay.SetNumExplicitHs(ay.GetTotalNumHs() + 1)
        ay.SetFormalCharge(ay.GetFormalCharge() + 1)
    for b in rw.GetBonds():  # lập thể E/Z của liên kết đổi bậc không còn giá trị
        ma, mb = rw.GetAtomWithIdx(b.GetBeginAtomIdx()).GetAtomMapNum(), rw.GetAtomWithIdx(b.GetEndAtomIdx()).GetAtomMapNum()
        if _cap(ma, mb) in tong_lk:
            b.SetStereo(Chem.BondStereo.STEREONONE)
    for a in rw.GetAtoms():  # H do map cố định: không cho RDKit tự thêm H
        a.SetNoImplicit(True)
    if kiem_hoa_tri:
        try:
            Chem.SanitizeMol(rw, _KEKULE)
        except Exception as e:  # noqa: BLE001
            raise LoiCoChe(f"Sau khi áp mũi tên, cấu tạo sai hoá trị: {str(e).splitlines()[0]}") from e
    return rw


def _khoa_manh(m, lap_the: bool = False) -> str:
    m = Chem.Mol(m)
    try:
        Chem.SanitizeMol(m)
    except Exception:  # noqa: BLE001
        pass
    return Chem.MolToSmiles(m, isomericSmiles=lap_the)


def khoa_trang_thai(mol) -> list[str]:
    """Khoá so sánh hai trạng thái: SMILES chuẩn (thơm, CÓ map, KHÔNG lập thể) của từng mảnh, đã sắp xếp."""
    return sorted(_khoa_manh(f) for f in Chem.GetMolFrags(mol, asMols=True, sanitizeFrags=False))


# ================================================================ lập thể theo map (không phụ thuộc cách viết SMILES)
def _lap_the(mol) -> tuple[dict[int, int], dict[tuple[int, int], str]]:
    """({map tâm lập thể: chẵn/lẻ theo thứ tự map các nhóm thế}, {(map, map) liên kết đôi: "E"/"Z" theo CIP})."""
    m = Chem.Mol(mol)
    try:
        Chem.SanitizeMol(m)
    except Exception:  # noqa: BLE001
        return {}, {}
    mh = Chem.AddHs(m)  # H tường minh: mọi tâm có đủ 4 nhóm thế để so chẵn/lẻ
    tam: dict[int, int] = {}
    for a in mh.GetAtoms():
        tag = a.GetChiralTag()
        if a.GetAtomMapNum() and tag in (Chem.ChiralType.CHI_TETRAHEDRAL_CW, Chem.ChiralType.CHI_TETRAHEDRAL_CCW):
            ds = [nb.GetAtomMapNum() for nb in a.GetNeighbors()]
            if len(ds) != 4 or len(set(ds)) < 3:
                continue
            ds = [x if x else -1 for x in ds]  # H không map
            dao = sum(1 for i in range(4) for j in range(i + 1, 4) if ds[i] > ds[j])
            tam[a.GetAtomMapNum()] = (1 if tag == Chem.ChiralType.CHI_TETRAHEDRAL_CW else -1) * (1 if dao % 2 == 0 else -1)
    _, doi = nhan_cip(m)
    return tam, doi


def nhan_cip(mol) -> tuple[dict[int, str], dict[tuple[int, int], str]]:
    """Nhãn R/S theo map và E/Z theo cặp map (để vẽ và để đối chiếu sau khi xoay/lật/đổi dạng)."""
    m = Chem.Mol(mol)
    try:
        Chem.SanitizeMol(m)
        from rdkit.Chem import rdCIPLabeler
        rdCIPLabeler.AssignCIPLabels(m)
    except Exception:  # noqa: BLE001
        return {}, {}
    rs = {a.GetAtomMapNum(): a.GetProp("_CIPCode") for a in m.GetAtoms() if a.GetAtomMapNum() and a.HasProp("_CIPCode")}
    ez = {_cap(b.GetBeginAtom().GetAtomMapNum(), b.GetEndAtom().GetAtomMapNum()): b.GetProp("_CIPCode")
          for b in m.GetBonds() if b.HasProp("_CIPCode") and b.GetBeginAtom().GetAtomMapNum() and b.GetEndAtom().GetAtomMapNum()}
    return rs, ez


# ================================================================ cơ chế
@dataclass
class Buoc:
    chat: list[str]
    mui_ten: list[MuiTen] = field(default_factory=list)
    trang_thai_chuyen_tiep: bool = False
    ghi_chu: str = ""
    dieu_kien: str = ""
    delta: dict[int, str] = field(default_factory=dict)       # map → "+" | "-"
    cap_e: list[int] = field(default_factory=list)              # map hiện thêm cặp e tự do (ngoài cặp e xuất phát của mũi tên)
    buoc_cham: bool = False                                     # giai đoạn chậm (quyết định tốc độ)
    thuan_nghich: bool = False                                  # mũi tên ⇌
    nhan_trung_gian: str = ""                                   # nhãn chất trung gian TẠO RA ở bước này
    cong_huong: list[str] = field(default_factory=list)         # cấu trúc cộng hưởng (SMILES có map) của tiểu phân trong bước
    chuyen_h: list[tuple[int, int]] = field(default_factory=list)  # chuyển H⁺ rút gọn (~H⁺): (map cho, map nhận)


@dataclass
class CoChe:
    ten: str
    loai: str
    nguon: str                     # "ai" (mọi cơ chế đều do AI tạo rồi kiểm tra)
    buoc: list[Buoc]
    san_pham: list[str]            # SMILES có map (trạng thái cuối)
    dieu_kien: str = ""
    canh_bao: list[str] = field(default_factory=list)
    giai_thich: str = ""           # hướng phản ứng: vì sao chọn vị trí đó, sản phẩm chính/phụ
    nhan_hien_thi: dict[int, str] = field(default_factory=dict)  # map → nhãn rút gọn (R, Ar, B:, Nu⁻, E⁺…)
    loi_kiem_tra: list[str] = field(default_factory=list)
    buoc_loi: list[int] = field(default_factory=list)
    da_kiem_tra: bool = False

    @property
    def nhan_kiem_tra(self) -> str:
        return "AI tạo — đã kiểm tra tự động" if self.da_kiem_tra and not self.loi_kiem_tra else "AI tạo — chưa qua kiểm tra"

    def to_json(self) -> dict:
        def sp_sach(s):
            m = _mol(s)
            return cac_manh_smiles(m, giu_map=False)[0] if m is not None else s

        def buoc_json(b: Buoc) -> dict:
            d = {"chat": b.chat, "mui_ten": [m.to_dict() for m in b.mui_ten], "trang_thai_chuyen_tiep": b.trang_thai_chuyen_tiep,
                 "buoc_cham": b.buoc_cham, "thuan_nghich": b.thuan_nghich, "nhan_trung_gian": b.nhan_trung_gian,
                 "cong_huong": b.cong_huong, "ghi_chu": b.ghi_chu}
            if b.chuyen_h:
                d["chuyen_h"] = [{"tu": x, "den": y} for x, y in b.chuyen_h]
            if b.dieu_kien:
                d["dieu_kien"] = b.dieu_kien
            if b.delta:
                d["delta"] = {str(k): v for k, v in b.delta.items()}
            return d
        return {
            "ten": self.ten, "loai": self.loai, "nguon": self.nguon, "dieu_kien": self.dieu_kien, "giai_thich": self.giai_thich,
            "nhan_hien_thi": [{"map": k, "nhan": v} for k, v in self.nhan_hien_thi.items()],
            "buoc": [buoc_json(b) for b in self.buoc],
            "san_pham": [sp_sach(s) for s in self.san_pham],
            "san_pham_map": self.san_pham,
            "kiem_tra": {"nhan": self.nhan_kiem_tra, "dat": self.da_kiem_tra and not self.loi_kiem_tra,
                         "loi": self.loi_kiem_tra, "buoc_loi": self.buoc_loi},
        }


def tu_json(d: dict, nguon: str = "ai") -> CoChe:
    """JSON (của AI hoặc dán lại từ "Copy JSON") → CoChe; tương thích dữ liệu cũ (không có các trường mới); lỗi cấu trúc → LoiCoChe."""
    if not isinstance(d, dict) or not isinstance(d.get("buoc"), list) or not d["buoc"]:
        raise LoiCoChe("JSON cơ chế cần có 'buoc' là danh sách các bước")
    buoc = []
    for i, b in enumerate(d["buoc"], 1):
        if not isinstance(b, dict) or not isinstance(b.get("chat"), list) or not b["chat"]:
            raise LoiCoChe(f"Bước {i}: thiếu 'chat' (danh sách SMILES)")
        mt = [MuiTen.tu_dict(x) for x in (b.get("mui_ten") or [])]
        delta = {int(k): str(v) for k, v in (b.get("delta") or {}).items() if str(v) in ("+", "-")}
        ch = []
        for x in b.get("chuyen_h") or []:
            try:
                ch.append((int(x["tu"]), int(x["den"])))
            except (KeyError, TypeError, ValueError) as e:
                raise LoiCoChe(f"Bước {i}: 'chuyen_h' cần dạng {{tu: map, den: map}}") from e
        buoc.append(Buoc([str(s) for s in b["chat"]], mt, bool(b.get("trang_thai_chuyen_tiep")), str(b.get("ghi_chu") or ""),
                         str(b.get("dieu_kien") or ""), delta, [int(x) for x in b.get("cap_e") or []],
                         bool(b.get("buoc_cham")), bool(b.get("thuan_nghich")), str(b.get("nhan_trung_gian") or ""),
                         [str(s) for s in b.get("cong_huong") or [] if str(s).strip()], ch))
    nhan = {}
    for x in d.get("nhan_hien_thi") or []:
        try:
            nhan[int(x["map"])] = str(x["nhan"])[:8]
        except (KeyError, TypeError, ValueError):
            continue
    sp = d.get("san_pham_map") or d.get("san_pham") or []
    return CoChe(str(d.get("ten") or "Cơ chế"), str(d.get("loai") or ""), nguon, buoc, [str(s) for s in sp],
                 str(d.get("dieu_kien") or ""), giai_thich=str(d.get("giai_thich") or ""), nhan_hien_thi=nhan)


def _mo_ta_manh(m) -> str:
    q = sum(a.GetFormalCharge() for a in m.GetAtoms())
    ct: Counter = Counter()
    for a in m.GetAtoms():
        ct[a.GetSymbol()] += 1
        ct["H"] += a.GetTotalNumHs()
    cong_thuc = "".join(f"{k}{v if v > 1 else ''}" for k, v in sorted(ct.items()) if v)
    return f"{_khoa_manh(m)} ({cong_thuc}, điện tích {q:+d})"


def kiem_tra(cc: CoChe) -> list[str]:
    """Kiểm tra bằng RDKit (rỗng = đạt), ghi cc.buoc_loi:
    - SMILES hợp lệ (cú pháp + hoá trị), atom map không trùng, mũi tên trỏ nguyên tử/liên kết có thật, liên kết không lẻ electron;
    - áp mũi tên (+ ~H⁺) của bước n ra ĐÚNG các chất của bước n+1 (bước cuối → sản phẩm): bảo toàn nguyên tử, H, điện tích;
      bước sau được thêm chất MỚI (toàn map mới) và được bỏ sản phẩm phụ đã tách ra;
    - lập thể: tâm lập thể / liên kết đôi KHÔNG tham gia phản ứng giữ nguyên cấu hình giữa hai bước;
    - cấu trúc cộng hưởng: cùng nguyên tử, cùng số H, cùng tổng điện tích với tiểu phân tương ứng."""
    loi: list[str] = []
    buoc_loi: set[int] = set()

    def bao(i, x):
        loi.append(f"Bước {i}: {x}")
        buoc_loi.add(i)

    for i, b in enumerate(cc.buoc, 1):
        try:
            mol = tu_smiles(b.chat)
        except LoiCoChe as e:
            bao(i, e)
            continue
        maps = [a.GetAtomMapNum() for a in mol.GetAtoms() if a.GetAtomMapNum()]
        if len(maps) != len(set(maps)):
            bao(i, "atom map bị trùng giữa các chất")
            continue
        if not b.mui_ten and not b.chuyen_h:
            bao(i, "chưa có mũi tên cong nào (hoặc chuyen_h)")
            continue
        bd = ban_do(mol)
        for mt in b.mui_ten:  # nguồn là liên kết thì liên kết phải có thật
            if mt.tu_loai == "lien_ket" and (any(m not in bd for m in mt.tu) or mol.GetBondBetweenAtoms(bd[mt.tu[0]], bd[mt.tu[1]]) is None):
                bao(i, f"mũi tên xuất phát từ liên kết {mt.tu[0]}–{mt.tu[1]} không tồn tại")
        # cộng hưởng
        manh = Chem.GetMolFrags(mol, asMols=True, sanitizeFrags=False)
        manh_bd = [{a.GetAtomMapNum() for a in f.GetAtoms()} for f in manh]
        for s in b.cong_huong:
            ch = _mol(s)
            if ch is None:
                bao(i, f"cấu trúc cộng hưởng không hợp lệ: {s}")
                continue
            ms = {a.GetAtomMapNum() for a in ch.GetAtoms()}
            khop = next((j for j, x in enumerate(manh_bd) if x == ms), None)
            if khop is None:
                bao(i, f"cấu trúc cộng hưởng {s} không cùng bộ nguyên tử (map) với chất nào trong bước")
                continue
            goc = manh[khop]
            h_goc = {a.GetAtomMapNum(): a.GetTotalNumHs() for a in goc.GetAtoms()}
            h_ch = {a.GetAtomMapNum(): a.GetTotalNumHs() for a in ch.GetAtoms()}
            if h_goc != h_ch or sum(a.GetFormalCharge() for a in goc.GetAtoms()) != sum(a.GetFormalCharge() for a in ch.GetAtoms()):
                bao(i, f"cấu trúc cộng hưởng {s} khác số H hoặc tổng điện tích (cộng hưởng chỉ dời electron, không dời nguyên tử)")
        try:
            sau = ap_mui_ten(mol, b.mui_ten, chuyen_h=b.chuyen_h)
        except LoiCoChe as e:
            bao(i, e)
            continue
        ke = cc.buoc[i].chat if i < len(cc.buoc) else cc.san_pham
        ten_ke = f"bước {i + 1}" if i < len(cc.buoc) else "sản phẩm"
        try:
            mol_ke = tu_smiles(ke)
        except LoiCoChe as e:
            bao(min(i + 1, len(cc.buoc)), e)
            continue
        cu = set(maps)
        manh_sau = {frozenset(a.GetAtomMapNum() for a in f.GetAtoms()): f
                    for f in Chem.GetMolFrags(sau, asMols=True, sanitizeFrags=False)}
        for f in Chem.GetMolFrags(mol_ke, asMols=True, sanitizeFrags=False):
            ms = frozenset(a.GetAtomMapNum() for a in f.GetAtoms())
            if not ms & cu:
                continue  # chất mới tham gia ở bước sau
            f_tinh = manh_sau.get(ms)
            if f_tinh is None:
                bao(i, f"áp mũi tên KHÔNG tạo ra chất {_mo_ta_manh(f)} mà {ten_ke} ghi (sai nhóm nguyên tử: thiếu/thừa liên kết "
                       f"giữa các phần). Các chất tính được: {'; '.join(_mo_ta_manh(x) for x in manh_sau.values())}")
            elif _khoa_manh(f_tinh) != _khoa_manh(f):
                bao(i, f"áp mũi tên ra {_mo_ta_manh(f_tinh)} nhưng {ten_ke} ghi {_mo_ta_manh(f)} "
                       "(khác liên kết, số H hoặc điện tích – không bảo toàn)")
        # lập thể ở chỗ không phản ứng phải giữ nguyên
        doi = lien_ket_doi(b.mui_ten, b.chuyen_h)
        tam_t, db_t = _lap_the(mol)
        tam_s, db_s = _lap_the(mol_ke)
        bd_ke = ban_do(mol_ke)
        for m, p in tam_t.items():
            if m in doi or m not in bd_ke:
                continue
            if m not in tam_s:
                bao(i, f"{ten_ke} bỏ mất cấu hình của tâm lập thể (map {m}) dù nguyên tử này không tham gia phản ứng")
            elif tam_s[m] != p:
                bao(i, f"cấu hình tâm lập thể map {m} bị đảo ở {ten_ke} dù nguyên tử này không tham gia phản ứng")
        for k, v in db_t.items():
            if set(k) & doi:
                continue
            if k in db_s and db_s[k] != v:
                bao(i, f"liên kết đôi {k[0]}={k[1]} đổi {v}→{db_s[k]} ở {ten_ke} dù không tham gia phản ứng")
    cc.buoc_loi = sorted(buoc_loi)
    return loi


def dung_tu_mui_ten(ten: str, loai: str, chat_dau: list[str] | Chem.RWMol,
                    cac_buoc: list[dict], dieu_kien: str = "") -> CoChe:
    """Dựng cơ chế từ trạng thái đầu + mũi tên từng bước (chất các bước sau do bộ đẩy electron tự tính).
    Dùng để kiểm thử/dựng lại cơ chế (không phải thư viện mẫu). cac_buoc: [{"mui_ten", "ghi_chu", "them": [Mol|SMILES], ...}]."""
    mol = Chem.RWMol(chat_dau) if not isinstance(chat_dau, list) else tu_smiles(chat_dau)
    buoc = []
    for b in cac_buoc:
        for s in b.get("them", []):
            mol.InsertMol(s if isinstance(s, Chem.Mol) else tu_smiles([s]))
        buoc.append(Buoc(cac_manh_smiles(mol), b["mui_ten"], b.get("trang_thai_chuyen_tiep", False), b.get("ghi_chu", ""),
                         b.get("dieu_kien", ""), b.get("delta", {}), b.get("cap_e", []), b.get("buoc_cham", False),
                         b.get("thuan_nghich", False), b.get("nhan_trung_gian", ""), b.get("cong_huong", []), b.get("chuyen_h", [])))
        mol = ap_mui_ten(mol, b["mui_ten"], chuyen_h=b.get("chuyen_h", []))
    return CoChe(ten, loai, "ai", buoc, cac_manh_smiles(mol), dieu_kien)


# ================================================================ điều phối
_SMILES_RO = re.compile(r"[\[\]=#@()\\/]|^[BCNOSPFIcnosp][a-zA-Z0-9]*$")


def _doc_smiles_truc_tiep(van_ban: str) -> tuple[list[str], list[str]] | None:
    """'CCBr.[OH-]>>CCO.[Br-]' hoặc 'CCBr + [OH-] -> CCO + [Br-]' toàn SMILES → (chất, sản phẩm); không phải SMILES → None."""
    t = van_ban.strip()
    if not t:
        return None
    if ">>" in t:
        trai, phai = t.split(">>", 1)
    else:
        phan = re.split(r"\s*(?:→|->|=>|⟶)\s*", t, maxsplit=1)
        trai, phai = phan[0], (phan[1] if len(phan) > 1 else "")

    def tach(s):
        ds = [x for x in re.split(r"\s+\+\s+|\.", s.strip()) if x.strip()] if s.strip() else []
        return [x.strip() for x in ds]

    chat, sp = tach(trai), tach(phai)
    for x in chat + sp:
        if " " in x or Chem.MolFromSmiles(x) is None or not (_SMILES_RO.search(x) or not re.search(r"\d", x)):
            return None
    return (chat, sp) if chat else None


_KHAN_GIA = {"[Na+]", "[K+]", "[Li+]", "[Cs+]", "[Ca+2]", "[Mg+2]"}
_KIEM = {"Li", "Na", "K", "Cs"}


def lam_sach(ds: list[str]) -> list[str]:
    """Tách SMILES nhiều mảnh, bỏ ion khán giả (Na⁺, K⁺...), NaOH/KOH viết cộng hoá trị → [OH-]; bỏ SMILES sai/trùng."""
    out: list[str] = []
    for s in ds:
        for x in (s or "").split("."):
            x = x.strip()
            m = Chem.MolFromSmiles(x) if x else None
            if m is None:
                continue
            chuan = Chem.MolToSmiles(m)
            if chuan in _KHAN_GIA:
                continue
            if m.GetNumAtoms() == 2 and {a.GetSymbol() for a in m.GetAtoms()} & _KIEM and any(a.GetSymbol() == "O" for a in m.GetAtoms()):
                chuan = "[OH-]"  # NaOH/KOH viết cộng hoá trị ([Na]O)
            if chuan not in out:
                out.append(chuan)
    return out
