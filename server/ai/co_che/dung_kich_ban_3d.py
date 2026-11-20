"""Dựng kịch bản 3D cho trình phát cơ chế từ một cơ chế JSON (định dạng co_che.py).

    python -m server.ai.co_che.dung_kich_ban_3d <co_che.json> [thư mục ra]   → <tên>.3d.json (+ .3d.js cho bản thử web)

Hợp đồng dữ liệu: nguyen_mau/co_che_3d/HOP_DONG_APP.md, mục 1.

Cách dựng:
- Nhóm giữ chỗ [*:n] có nhãn CO2Me/CO2Et được thay bằng nhóm ester thật để hình 3D đủ nguyên tử; nhãn khác → CH3.
- Mọi nguyên tử nặng có atom map; H mang map riêng ([H:6]) có id m6; H còn lại đặt tên theo nguyên tử mang nó
  (H<map cha>_<k>) nên giữ được danh tính qua các bước. Khi chuyển H⁺ rút gọn (chuyen_h), H được giữ tên cũ.
- Trạng thái đầu: dựng từng chất bằng ETKDG + MMFF/UFF; chất kia đặt ở phía "mở" của nguyên tử bị tấn công
  (sau lưng nhóm rời đi, vuông góc mặt phẳng liên kết π…), lùi xa rồi tiến lại gần.
- Trạng thái sau mỗi bước: tính bằng bộ đẩy electron (co_che.ap_mui_ten), lấy NGUYÊN toạ độ trước làm điểm xuất phát
  rồi tối ưu theo liên kết mới, từng mảnh, nên nguyên tử dịch chuyển liên tục, không nhảy cấu dạng.
- Chất mới vào ở bước sau (H₂O, HSO₄⁻, Cl₂…): thêm trạng thái "xa" rồi "gần"; chất phụ tách ra thì biến mất dần.
- Nhánh sai (nhanh_sai): cấu hình tấn công thử + màn chắn (chỉ hiện khi đang phát nhánh sai).
"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

import numpy as np
from rdkit import Chem, RDLogger
from rdkit.Chem import AllChem, rdMolTransforms
from rdkit.Geometry import Point3D

from . import co_che as cc_mod

RDLogger.DisableLog("rdApp.*")

ESTER = {"CO2Me": "[C:{m}](=[O:{m}1])([O:{m}2][CH3:{m}3])", "CO2Et": "[C:{m}](=[O:{m}1])([O:{m}2][CH2:{m}3][CH3:{m}4])"}
KHOANG_GAN = 2.9      # Å: nguyên tử tấn công cách nguyên tử đích lúc "tiến lại gần"
KHOANG_XA = 4.5       # Å: lùi thêm ở trạng thái "xa"


def day_du(smiles: str, nhan: dict[int, str] | None = None) -> str:
    """[*:8] → nhóm thật theo nhãn hiển thị (ester có map 8, 81, 82, 83; nhãn khác → CH3)."""
    nhan = nhan or {}

    def thay(x):
        m = int(x.group(1))
        return ESTER.get(nhan.get(m, "CO2Me"), "[CH3:{m}]").format(m=m)
    return re.sub(r"\[\*:(\d+)\]", thay, smiles)


def dung_mol(ds: list[str], nhan: dict[int, str] | None = None) -> Chem.Mol:
    ps = Chem.SmilesParserParams()
    ps.removeHs = False                                 # giữ H mang map ([H:6]) là nguyên tử riêng
    mol = Chem.MolFromSmiles(".".join(day_du(s, nhan) for s in ds), ps)
    if mol is None:
        raise ValueError(f"SMILES sai: {ds}")
    return Chem.AddHs(mol)


def ten_nguyen_tu(mol) -> list[str]:
    """id ổn định: m<map> cho nguyên tử có map (kể cả H mang map), H<map cha>_<k> cho H thường."""
    ten, dem = [], {}
    for a in mol.GetAtoms():
        if a.GetAtomMapNum():
            ten.append(f"m{a.GetAtomMapNum()}")
        elif a.GetAtomicNum() == 1 and a.GetDegree():
            cha = a.GetNeighbors()[0].GetAtomMapNum()
            k = dem.get(cha, 0)
            dem[cha] = k + 1
            ten.append(f"H{cha}_{k}")
        else:
            raise ValueError(f"Nguyên tử {a.GetSymbol()} chưa có atom map")
    return ten


def toi_uu(mol, lan: int = 4000) -> None:
    """MMFF94 cho chất bền (vòng căng chính xác hơn); ion/gốc tự do MMFF không hỗ trợ → UFF."""
    if mol.GetNumAtoms() < 2:
        return
    co_goc = any(a.GetNumRadicalElectrons() for a in mol.GetAtoms())
    ff = None
    if not co_goc and AllChem.MMFFHasAllMoleculeParams(mol):
        ff = AllChem.MMFFGetMoleculeForceField(mol, AllChem.MMFFGetMoleculeProperties(mol))
    if ff is None and AllChem.UFFHasAllMoleculeParams(mol):
        ff = AllChem.UFFGetMoleculeForceField(mol)
    if ff is None:
        return
    ff.Initialize()
    for _ in range(20):            # Minimize trả 1 khi chưa hội tụ
        if ff.Minimize(maxIts=lan) == 0:
            break


def nhung(mol, seed: int = 7) -> None:
    if mol.GetNumAtoms() == 1:
        conf = Chem.Conformer(1)
        conf.SetAtomPosition(0, Point3D(0, 0, 0))
        mol.AddConformer(conf, assignId=True)
        return
    ps = AllChem.ETKDGv3()
    ps.randomSeed = seed
    if AllChem.EmbedMolecule(mol, ps) != 0:
        ps.useRandomCoords = True
        if AllChem.EmbedMolecule(mol, ps) != 0:
            raise RuntimeError("Không dựng được 3D")
    toi_uu(mol)


def toa_do(mol, ten: list[str] | None = None) -> dict[str, np.ndarray]:
    c = mol.GetConformer()
    return {t: np.array(c.GetAtomPosition(i)) for i, t in enumerate(ten or ten_nguyen_tu(mol))}


def dat_toa_do(mol, td: dict[str, np.ndarray], ten: list[str]) -> None:
    """Đặt toạ độ theo tên; nguyên tử chưa có toạ độ (H mới) đặt cạnh nguyên tử mang nó."""
    conf = Chem.Conformer(mol.GetNumAtoms())
    rng = np.random.default_rng(3)
    for i, t in enumerate(ten):
        if t in td:
            p = td[t]
        else:
            a = mol.GetAtomWithIdx(i)
            cha = [n.GetIdx() for n in a.GetNeighbors() if ten[n.GetIdx()] in td]
            goc = td[ten[cha[0]]] if cha else np.zeros(3)
            v = rng.normal(size=3)
            p = goc + v / np.linalg.norm(v) * 1.05
        conf.SetAtomPosition(i, Point3D(*map(float, p)))
    mol.RemoveAllConformers()
    mol.AddConformer(conf, assignId=True)


def kabsch(P: np.ndarray, Q: np.ndarray):
    """R, t để R·P + t khớp Q (bình phương nhỏ nhất)."""
    pc, qc = P.mean(0), Q.mean(0)
    if len(P) < 3:
        return np.eye(3), qc - pc
    H = (P - pc).T @ (Q - qc)
    U, _, Vt = np.linalg.svd(H)
    d = np.sign(np.linalg.det(Vt.T @ U.T)) or 1.0
    R = Vt.T @ np.diag([1, 1, d]) @ U.T
    return R, qc - R @ pc


def can_theo(td: dict, goc: dict) -> dict:
    chung = [k for k in td if k in goc]
    if not chung:
        return td
    R, t = kabsch(np.array([td[k] for k in chung]), np.array([goc[k] for k in chung]))
    return {k: R @ v + t for k, v in td.items()}


def xoay_tu_den(u: np.ndarray, v: np.ndarray) -> np.ndarray:
    """Ma trận quay đưa vectơ đơn vị u về v (Rodrigues)."""
    u, v = u / np.linalg.norm(u), v / np.linalg.norm(v)
    c = float(u @ v)
    if c > 1 - 1e-9:
        return np.eye(3)
    if c < -1 + 1e-9:
        tr = np.cross(u, [1, 0, 0]) if abs(u[0]) < .9 else np.cross(u, [0, 1, 0])
        tr /= np.linalg.norm(tr)
        return 2 * np.outer(tr, tr) - np.eye(3)
    w = np.cross(u, v)
    K = np.array([[0, -w[2], w[1]], [w[2], 0, -w[0]], [-w[1], w[0], 0]])
    return np.eye(3) + K + K @ K * (1 / (1 + c))


_BTH = Chem.GetPeriodicTable()


def hop_ly(mol, lech: float = .14) -> bool:
    """Mọi liên kết dài trong khoảng ±14% tổng bán kính cộng hoá trị (đã trừ phần rút ngắn của liên kết bội)."""
    c = mol.GetConformer()
    for b in mol.GetBonds():
        i, j = b.GetBeginAtomIdx(), b.GetEndAtomIdx()
        r = _BTH.GetRcovalent(mol.GetAtomWithIdx(i).GetAtomicNum()) + _BTH.GetRcovalent(mol.GetAtomWithIdx(j).GetAtomicNum())
        r *= {1.0: 1.0, 2.0: .87, 3.0: .78}.get(b.GetBondTypeAsDouble(), .93)
        d = (c.GetAtomPosition(i) - c.GetAtomPosition(j)).Length()
        if abs(d / r - 1) > lech:
            return False
    return True


def dung_lai_gan_nhat(ds: list[str], truoc: dict, nhan: dict, so: int = 30) -> dict:
    """Dựng lại bằng ETKDG nhiều cấu dạng, chọn cấu dạng (sau khi căn) gần trạng thái trước nhất."""
    mol = dung_mol(ds, nhan)
    ten = ten_nguyen_tu(mol)
    ps = AllChem.ETKDGv3()
    ps.randomSeed = 11
    cids = list(AllChem.EmbedMultipleConfs(mol, so, ps))
    tot, tot_d = None, 1e9
    for cid in cids:
        m1 = Chem.Mol(mol, confId=cid)
        m1.RemoveAllConformers()
        m1.AddConformer(Chem.Conformer(mol.GetConformer(cid)), assignId=True)
        toi_uu(m1)
        if not hop_ly(m1):
            continue
        td = can_theo(toa_do(m1, ten), truoc)
        d = sum(float(np.sum((td[k] - truoc[k]) ** 2)) for k in td if k in truoc)
        if d < tot_d:
            tot, tot_d = td, d
    if tot is None:
        raise RuntimeError(f"Không dựng được cấu trúc hợp lý cho {ds}")
    return tot


def mo_ta(mol, ten: list[str]) -> dict:
    lk = []
    for b in mol.GetBonds():
        bac = {Chem.BondType.SINGLE: 1, Chem.BondType.DOUBLE: 2, Chem.BondType.TRIPLE: 3}.get(b.GetBondType(), 1)
        lk.append([ten[b.GetBeginAtomIdx()], ten[b.GetEndAtomIdx()], bac])
    goc = {ten[a.GetIdx()]: a.GetNumRadicalElectrons() for a in mol.GetAtoms() if a.GetNumRadicalElectrons()}
    dien_tich = {ten[a.GetIdx()]: a.GetFormalCharge() for a in mol.GetAtoms() if a.GetFormalCharge()}
    return {"lien_ket": lk, "goc_tu_do": goc, "dien_tich": dien_tich}


# ---------------------------------------------------------------------------- hình học đặt chất
def huong_mo(mol, ten: list[str], td: dict, idx: int) -> np.ndarray | None:
    """Hướng "mở" để tiếp cận nguyên tử idx: sau lưng các nhóm thế (sp3), vuông góc mặt phẳng (sp2), vuông góc trục (sp)."""
    p = td[ten[idx]]
    ke = [td[ten[n.GetIdx()]] for n in mol.GetAtomWithIdx(idx).GetNeighbors()]
    if not ke:
        return None
    if len(ke) == 1:
        v = p - ke[0]
    elif len(ke) == 2:
        a, b = ke[0] - p, ke[1] - p
        cos = float(a @ b / (np.linalg.norm(a) * np.linalg.norm(b) + 1e-9))
        if cos < -.98:                                   # thẳng hàng (alkyne, NO₂⁺): vuông góc trục
            v = np.cross(a, [0, 0, 1]) if abs(a[2]) < .9 * np.linalg.norm(a) else np.cross(a, [1, 0, 0])
        else:
            v = p - (ke[0] + ke[1]) / 2
    else:
        tb = np.mean(ke, axis=0)
        lech = p - tb
        if len(ke) == 3 and np.linalg.norm(lech) < .25:   # sp2 phẳng: vuông góc mặt phẳng
            v = np.cross(ke[1] - ke[0], ke[2] - ke[0])
        else:
            v = lech
    n = np.linalg.norm(v)
    return v / n if n > 1e-6 else None


class CanhDung:
    """Toạ độ hiện tại + phân tử RDKit của trạng thái (để tính hướng mở)."""

    def __init__(self, mol, ten: list[str], td: dict):
        self.mol, self.ten, self.td = mol, ten, td
        self.chi_so = {t: i for i, t in enumerate(ten)}

    def huong(self, ten_nt: str) -> np.ndarray:
        v = huong_mo(self.mol, self.ten, self.td, self.chi_so[ten_nt])
        return v if v is not None else np.array([1.0, 0, 0])


def dat_canh(manh_td: dict, manh_mol, manh_ten: list[str], nt_manh: str, dich: np.ndarray, v: np.ndarray, d: float) -> dict:
    """Xoay mảnh cho hướng mở của nt_manh quay về phía đích (-v), rồi đặt nt_manh tại dich + v·d."""
    u = huong_mo(manh_mol, manh_ten, manh_td, manh_ten.index(nt_manh))
    goc = manh_td[nt_manh]
    R = xoay_tu_den(u, -v) if u is not None else np.eye(3)
    dat = dich + v * d
    return {k: R @ (x - goc) + dat for k, x in manh_td.items()}


def cap_hinh_thanh(mui_ten: list[dict], chuyen_h: list[dict] | None = None) -> list[tuple[int, int, int]]:
    """(nguyên tử nguồn, nguyên tử đích, độ ưu tiên) của các liên kết sắp hình thành; chuyển H⁺ rút gọn cũng tính."""
    ra = []
    for m in mui_ten:
        tu, de = m["tu"], m["den"]
        t = tu["nguyen_tu"] if isinstance(tu["nguyen_tu"], list) else [tu["nguyen_tu"]]
        d = de["nguyen_tu"] if isinstance(de["nguyen_tu"], list) else [de["nguyen_tu"]]
        if tu["loai"] == "cap_e" and de["loai"] == "nguyen_tu":
            ra.append((t[0], d[0], 0))
        elif de["loai"] == "lien_ket":
            ra.append((d[0], d[1], 1))
    for x in chuyen_h or []:
        ra.append((int(x["den"]), int(x["tu"]), 2))
    return ra


def _manh(mol) -> list[list[int]]:
    return [list(f) for f in Chem.GetMolFrags(mol, asMols=False, sanitizeFrags=False)]


def _sub_mol(ds: list[str], nhan: dict, ten_can: set[str]):
    """Phân tử con của các mảnh có tên trong ten_can (dựng 3D riêng)."""
    ra = []
    for s in ds:
        m = dung_mol([s], nhan)
        t = ten_nguyen_tu(m)
        if set(t) & ten_can:
            ra.append((s, m, t))
    return ra


def dat_chat_moi(mol, ten, td_biet: dict, ds: list[str], nhan: dict, cap: list[tuple[int, int, int]]) -> tuple[dict, dict]:
    """Đặt các mảnh CHƯA có toạ độ cạnh nguyên tử chúng sắp liên kết. Trả (toạ độ gần, toạ độ xa)."""
    gan, xa = dict(td_biet), dict(td_biet)
    canh = CanhDung(mol, ten, td_biet)
    chua = set(ten) - set(td_biet)
    so_le = 0
    tam = np.mean(list(td_biet.values()), axis=0) if td_biet else np.zeros(3)
    for s, m, t in _sub_mol(ds, nhan, chua):
        if set(t) & set(td_biet):
            continue                                     # mảnh đã có một phần toạ độ: dat_toa_do lo phần H mới
        nhung(m)
        tdm = toa_do(m, t)
        da_dat = False
        for a, b, _ in sorted(cap, key=lambda x: x[2]):
            ta, tb = f"m{a}", f"m{b}"
            for moi, cu in ((ta, tb), (tb, ta)):
                if moi in tdm and cu in gan and cu not in tdm:
                    v = canh.huong(cu) if cu in canh.chi_so and cu in td_biet else np.array([1.0, 0, 0])
                    g = dat_canh(tdm, m, t, moi, gan[cu], v, KHOANG_GAN)
                    gan.update(g)
                    xa.update({k: x + v * KHOANG_XA for k, x in g.items()})
                    da_dat = True
                    break
            if da_dat:
                break
        if not da_dat:                                   # chất khán giả: đặt lệch sang bên
            so_le += 1
            r = max((np.linalg.norm(x - tam) for x in gan.values()), default=0) + 3.5
            dich = tam + np.array([0, r, 0]) * (1 if so_le % 2 else -1)
            c = np.mean(list(tdm.values()), axis=0)
            g = {k: x - c + dich for k, x in tdm.items()}
            gan.update(g)
            xa.update({k: x + np.array([0, 3.0, 0]) * (1 if so_le % 2 else -1) for k, x in g.items()})
    return gan, xa


def doi_ten_h(ten: list[str], truoc: dict, chuyen_h: list[dict] | None) -> list[str]:
    """Chuyển H⁺ rút gọn: H mới ở nguyên tử nhận lấy lại tên H cũ đã mất ở nguyên tử cho."""
    if not chuyen_h:
        return ten
    moi = [t for t in ten if t not in truoc and t.startswith("H")]
    mat = [t for t in truoc if t not in ten and t.startswith("H")]
    ten = list(ten)
    for x in chuyen_h:
        cho, nhan = int(x["tu"]), int(x["den"])
        a = next((t for t in mat if t.startswith(f"H{cho}_")), None)
        b = next((t for t in moi if t.startswith(f"H{nhan}_")), None)
        if a and b:
            ten[ten.index(b)] = a
            mat.remove(a)
            moi.remove(b)
    return ten


class BoDatTen:
    """Tên nguyên tử bền qua các bước: tên gốc (ten_nguyen_tu) + bí danh do chuyển H⁺ rút gọn sinh ra."""

    def __init__(self):
        self.bi_danh: dict[str, str] = {}

    def dat(self, mol, truoc: dict | None = None, chuyen_h=None) -> tuple[list[str], dict[str, str]]:
        goc = ten_nguyen_tu(mol)
        t = [self.bi_danh.get(x, x) for x in goc]
        if truoc is not None and chuyen_h:
            t = doi_ten_h(t, truoc, chuyen_h)
            for g, x in zip(goc, t):
                if g != x:
                    self.bi_danh[g] = x
        return t, dict(zip(goc, t))


def tinh_trang_thai(ds: list[str], truoc: dict, nhan: dict, bo_ten: BoDatTen, chuyen_h=None) -> tuple[Chem.Mol, list[str], dict]:
    """Toạ độ cho trạng thái ds xuất phát từ toạ độ trước: tối ưu từng mảnh, căn về chỗ cũ."""
    mol = dung_mol(ds, nhan)
    ten, doi = bo_ten.dat(mol, truoc, chuyen_h)
    td: dict[str, np.ndarray] = {}
    for s in ds:
        m = dung_mol([s], nhan)
        t = [doi[x] for x in ten_nguyen_tu(m)]
        if not set(t) & set(truoc):
            continue
        dat_toa_do(m, truoc, t)
        goc = toa_do(m, t)
        toi_uu(m)
        if hop_ly(m):
            td.update(can_theo(toa_do(m, t), {k: v for k, v in goc.items() if k in truoc}))
        else:
            lai = dung_lai_gan_nhat([s], truoc, nhan)
            td.update({doi[k]: v for k, v in lai.items()})
    return mol, ten, td


def ap_dung_goi_y(mol, ten: list[str], goi_y: dict) -> None:
    """goi_y_3d.nhi_dien: [[a, b, c, d, độ]] (atom map) – đặt góc nhị diện rồi tối ưu (giữ cực tiểu gần nhất)."""
    ci = {t: i for i, t in enumerate(ten)}
    conf = mol.GetConformer()
    for a, b, c, d, g in goi_y.get("nhi_dien", []):
        ids = [ci.get(f"m{x}") for x in (a, b, c, d)]
        if None in ids:
            continue
        try:
            rdMolTransforms.SetDihedralDeg(conf, *ids, float(g))
        except (ValueError, RuntimeError):
            continue
    toi_uu(mol)


# ---------------------------------------------------------------------------- dựng cả kịch bản
def dung(cc: dict) -> dict:
    nhan = {int(x["map"]): x["nhan"] for x in cc.get("nhan_hien_thi", [])}
    buoc = cc["buoc"]
    trang: list[dict] = []
    cac_buoc: list[dict] = []
    nt: dict[str, dict] = {}
    chi_so_buoc: dict[int, int] = {}                     # bước (1-based) → chỉ số trạng thái nơi mũi tên bắt đầu
    canh_cua: dict[int, CanhDung] = {}

    def them(ten_tt: str, mol, ten: list[str], td: dict) -> int:
        for t, a in zip(ten, mol.GetAtoms()):
            nt.setdefault(t, {"e": a.GetSymbol()})
        trang.append({"ten": ten_tt, "toa_do": {k: td[k] for k in ten}, **mo_ta(mol, ten)})
        return len(trang) - 1

    # trạng thái đầu: chất đầu tiên trong "chat" làm gốc, các chất khác đặt theo mũi tên bước 1
    ds0 = buoc[0]["chat"]
    goc_s, goc_m, goc_t = ds0[0], dung_mol([ds0[0]], nhan), None
    nhung(goc_m)
    goc_t = ten_nguyen_tu(goc_m)
    if cc.get("goi_y_3d"):
        ap_dung_goi_y(goc_m, goc_t, cc["goi_y_3d"])
    td_goc = toa_do(goc_m, goc_t)
    bo_ten = BoDatTen()
    mol0 = dung_mol(ds0, nhan)
    ten0, _ = bo_ten.dat(mol0)
    cap1 = cap_hinh_thanh(buoc[0]["mui_ten"], buoc[0].get("chuyen_h"))
    gan, xa = dat_chat_moi(mol0, ten0, td_goc, ds0, nhan, cap1)
    if len(ds0) > 1:
        them("Các chất ban đầu", mol0, ten0, xa)
        i = them("Tiến lại gần", mol0, ten0, gan)
        cac_buoc.append({"tu": 0, "den": 1, "mui_ten": [], "ten": "Các chất tiến lại gần", "ghi_chu": ""})
    else:
        i = them("Chất ban đầu", mol0, ten0, gan)
    truoc = gan
    canh_cua[1] = CanhDung(mol0, ten0, gan)
    chi_so_buoc[1] = i

    for k, b in enumerate(buoc, 1):
        # áp mũi tên bằng bộ đẩy electron → các chất sau bước k
        mt = [cc_mod.MuiTen.tu_dict(x) for x in b["mui_ten"]]
        ch = [(int(x["tu"]), int(x["den"])) for x in b.get("chuyen_h", [])]
        sau = cc_mod.ap_mui_ten(cc_mod.tu_smiles(b["chat"]), mt, chuyen_h=ch)
        ds_sau = cc_mod.cac_manh_smiles(sau)
        mol, ten, td = tinh_trang_thai(ds_sau, truoc, nhan, bo_ten, b.get("chuyen_h"))
        if k < len(buoc):
            ten_tt = buoc[k].get("nhan_trung_gian") or f"Sau bước {k}"
        else:
            ten_tt = "Sản phẩm"
        j = them(ten_tt[:1].upper() + ten_tt[1:], mol, ten, td)
        cac_buoc.append({"tu": chi_so_buoc[k], "den": j, "mui_ten": b["mui_ten"], "ten": f"Bước {k}", "ghi_chu": b.get("ghi_chu", ""),
                         "trang_thai_chuyen_tiep": bool(b.get("trang_thai_chuyen_tiep")), "buoc_cham": bool(b.get("buoc_cham")),
                         "thuan_nghich": bool(b.get("thuan_nghich")), "chuyen_h": b.get("chuyen_h", [])})
        truoc = td
        if k == len(buoc):
            break
        # chuẩn bị bước k+1: thêm chất mới / bỏ chất phụ đã tách
        ds_ke = buoc[k]["chat"]
        mol_ke = dung_mol(ds_ke, nhan)
        ten_ke, _ = bo_ten.dat(mol_ke)
        if set(ten_ke) == set(ten):
            chi_so_buoc[k + 1] = j
            canh_cua[k + 1] = CanhDung(mol, ten, td)
            continue
        biet = {t: truoc[t] for t in ten_ke if t in truoc}
        cap = cap_hinh_thanh(buoc[k]["mui_ten"], buoc[k].get("chuyen_h"))
        gan, xa = dat_chat_moi(mol_ke, ten_ke, biet, ds_ke, nhan, cap)
        # H mới trong mảnh đã biết (hiếm): đặt cạnh nguyên tử mang nó
        if set(ten_ke) - set(gan):
            tmp = Chem.Mol(mol_ke)
            dat_toa_do(tmp, gan, ten_ke)
            gan = toa_do(tmp, ten_ke)
            xa.update({t: gan[t] for t in ten_ke if t not in xa})
        co_moi = any(t not in truoc for t in ten_ke)
        if co_moi:
            them("Chất mới tham gia", mol_ke, ten_ke, xa)
            cac_buoc.append({"tu": j, "den": len(trang) - 1, "mui_ten": [], "ten": "Chất mới tham gia", "ghi_chu": ""})
            j2 = them("Tiến lại gần", mol_ke, ten_ke, gan)
            cac_buoc.append({"tu": j2 - 1, "den": j2, "mui_ten": [], "ten": "Tiến lại gần", "ghi_chu": ""})
        else:
            j2 = them("Tách sản phẩm phụ", mol_ke, ten_ke, gan)
            cac_buoc.append({"tu": j, "den": j2, "mui_ten": [], "ten": "Tách sản phẩm phụ", "ghi_chu": ""})
        truoc = gan
        chi_so_buoc[k + 1] = j2
        canh_cua[k + 1] = CanhDung(mol_ke, ten_ke, gan)

    nhanh = []
    for ns in cc.get("nhanh_sai", []) or []:            # nhánh sai do AI viết: lỗi thì bỏ nhánh đó, không làm hỏng cả bài
        try:
            nhanh.append(dung_nhanh_sai(ns, trang, chi_so_buoc, canh_cua, nhan, nt))
        except Exception:  # noqa: BLE001
            continue

    # đưa tâm về gốc toạ độ (cả nhánh sai, màn chắn)
    tam = np.mean([v for s in trang for v in s["toa_do"].values()], axis=0)
    tron = lambda v: [round(float(x), 3) for x in v]
    for s in trang:
        s["toa_do"] = {k: tron(v - tam) for k, v in s["toa_do"].items()}
    for n in nhanh:
        n["toa_do"] = {k: tron(v - tam) for k, v in n["toa_do"].items()}
        n["man_chan"]["tam"] = tron(n["man_chan"]["tam"] - tam)
        n["man_chan"]["phap_tuyen"] = tron(n["man_chan"]["phap_tuyen"])
    ra = {"ten": cc["ten"], "loai": cc.get("loai", ""), "dieu_kien": cc.get("dieu_kien", ""), "giai_thich": cc.get("giai_thich", ""),
          "nguyen_tu": nt, "trang_thai": trang, "buoc": cac_buoc}
    if nhanh:
        ra["nhanh_sai"] = nhanh
    return ra


def dung_nhanh_sai(ns: dict, trang: list[dict], chi_so_buoc: dict, canh_cua: dict, nhan: dict, nt: dict) -> dict:
    """Nhánh sai: chất tấn công tiến tới nguyên tử đích nhưng bị màn chắn chặn (cản trở không gian, sai hình học)."""
    k = int(ns.get("tu_buoc", 1))
    i = chi_so_buoc[k]
    canh = canh_cua[k]
    td = {t: np.array(v, float) for t, v in trang[i]["toa_do"].items()}
    tc, dich = (f"m{x}" for x in ns["dich"])
    v = canh.huong(dich)
    p_dich = td[dich]
    them_nt = {}
    for s in ns.get("them", []):
        m = dung_mol([s], nhan)
        t = ten_nguyen_tu(m)
        nhung(m)
        g = dat_canh(toa_do(m, t), m, t, tc if tc in t else t[0], p_dich, v, 3.3)
        td.update(g)
        for tt, a in zip(t, m.GetAtoms()):
            them_nt[tt] = {"e": a.GetSymbol()}
            nt.setdefault(tt, {"e": a.GetSymbol()})
    if not ns.get("them"):                               # chất tấn công đã có: dời cả mảnh của nó tới phía nguyên tử đích
        mol, ten = canh.mol, canh.ten
        ci = canh.chi_so[tc]
        manh = next(f for f in _manh(mol) if ci in f)
        ten_m = [ten[x] for x in manh]
        sub = {t: td[t] for t in ten_m}
        u = sub[tc] - np.mean(list(sub.values()), axis=0)
        R = xoay_tu_den(u, -v) if np.linalg.norm(u) > 1e-6 else np.eye(3)
        goc = sub[tc]
        td.update({t: R @ (x - goc) + p_dich + v * 2.6 for t, x in sub.items()})
    return {"tu_trang_thai": i, "ten": ns.get("ten", "Nhánh sai"), "ly_do": ns.get("ly_do", ""), "kieu_chan": ns.get("kieu_chan", ""),
            "nguyen_tu_them": them_nt, "toa_do": td, "mui_ten": ns.get("mui_ten", []),
            "man_chan": {"tam": p_dich + v * 1.55, "phap_tuyen": v, "ban_kinh": 1.3}}


def dung_tep(duong_dan: Path) -> dict:
    return dung(json.loads(Path(duong_dan).read_text(encoding="utf-8")))


def ghi(kb: dict, ra: Path, ban_web: bool = True) -> None:
    ra.parent.mkdir(parents=True, exist_ok=True)
    ra.write_text(json.dumps(kb, ensure_ascii=False), encoding="utf-8")                       # cho app (Unity)
    if ban_web:
        ra.with_suffix(".js").write_text("window.KICH_BAN = " + json.dumps(kb, ensure_ascii=False) + ";\n", encoding="utf-8")


if __name__ == "__main__":
    p = Path(sys.argv[1])
    thu_muc = Path(sys.argv[2]) if len(sys.argv) > 2 else p.parent
    kb = dung_tep(p)
    ten = p.parent.name if p.name == "co_che.json" else p.stem
    ghi(kb, thu_muc / f"{ten}.3d.json")
    for s in kb["trang_thai"]:
        print(f"{s['ten']:<26} {len(s['toa_do'])} nguyên tử, {len(s['lien_ket'])} liên kết, gốc tự do: {s['goc_tu_do']}")
    print("→", thu_muc / f"{ten}.3d.json")
