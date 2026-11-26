"""Mục "Mô hình 3D" của màn Giải bài tập (VERA), viết lại từ backend/mo_hinh_3d.py của app CHEMISTRY.

Tên / công thức / SMILES → nhận diện chất → cấu dạng 3D (RDKit ETKDG + MMFF) → nguyên tử, liên kết cho app vẽ.
Thứ tự nhận diện (không cần đăng nhập, không cần key):
  1. bảng chất SGK của giai_chuoi (tên tiếng Việt, tên IUPAC, công thức thu gọn) – đã kiểm chứng;
  2. SMILES hợp lệ do người dùng gõ;
  3. tên tiếng Anh qua PubChem (chỉ gửi tên chất, có thời hạn chờ); không có mạng thì bỏ qua.
"""
from __future__ import annotations

import base64
import io
import json
import re
import os
import ssl
import sys
import urllib.parse
import urllib.request

from rdkit import Chem, RDLogger
from rdkit.Chem import AllChem, Descriptors, Draw
from rdkit.Chem.rdMolDescriptors import CalcMolFormula

from .giai_chuoi import nhan_chat

RDLogger.DisableLog("rdApp.*")

PUBCHEM = "https://pubchem.ncbi.nlm.nih.gov/rest/pug/compound/name/{}/property/Title,IsomericSMILES,IUPACName/JSON"
PUBCHEM_SMI = "https://pubchem.ncbi.nlm.nih.gov/rest/pug/compound/smiles/{}/property/Title,IUPACName/JSON"
SO_NGUYEN_TU_TOI_DA = 160
MAU = ["ethanol", "acetic acid", "benzene", "glucose", "aspirin", "caffeine", "cyclohexane", "propan-2-ol"]


class LoiMoHinh(ValueError):
    pass


def _ssl() -> ssl.SSLContext:
    """Python cài từ python.org (Mac) không đọc kho chứng chỉ hệ thống: dùng certifi nếu có."""
    try:
        import certifi
        return ssl.create_default_context(cafile=certifi.where())
    except Exception:  # noqa: BLE001
        return ssl.create_default_context()


def _pubchem(ten: str, cho: float = 6.0) -> dict | None:
    """Tên tiếng Anh → {smiles, iupac, cid, title} qua PubChem; lỗi mạng / không thấy → None."""
    if not re.fullmatch(r"[A-Za-z0-9 ,\-()'\[\]]{2,80}", ten):
        return None
    url = PUBCHEM.format(urllib.parse.quote(ten.strip()))
    try:
        req = urllib.request.Request(url, headers={"User-Agent": "VERA-Lab/1.0"})
        with urllib.request.urlopen(req, timeout=cho, context=_ssl()) as r:
            p = json.loads(r.read().decode("utf-8"))["PropertyTable"]["Properties"][0]
        smi = p.get("IsomericSMILES") or p.get("SMILES") or ""
        if not smi:
            return None
        return {"smiles": smi, "iupac": p.get("IUPACName", ""), "cid": p.get("CID"), "title": p.get("Title", "")}
    except Exception:  # noqa: BLE001 – không có mạng hoặc PubChem không biết tên này
        return None


_cache_pc_smi: dict[str, dict | None] = {}


def _pubchem_theo_smiles(smiles: str, cho: float = 5.0) -> dict | None:
    """Tra CID / IUPAC / Title trên PubChem theo SMILES; bỏ qua khi chạy kiểm thử."""
    if "unittest" in sys.modules or os.environ.get("VERA_TAT_PUBCHEM_3D"):
        return None
    if smiles in _cache_pc_smi:
        return _cache_pc_smi[smiles]
    res = None
    try:
        url = PUBCHEM_SMI.format(urllib.parse.quote(smiles, safe=""))
        req = urllib.request.Request(url, headers={"User-Agent": "VERA-Lab/1.0"})
        with urllib.request.urlopen(req, timeout=cho, context=_ssl()) as r:
            p = json.loads(r.read().decode("utf-8"))["PropertyTable"]["Properties"][0]
        if p.get("CID"):
            res = {"cid": p.get("CID"), "iupac": p.get("IUPACName", ""), "title": p.get("Title", "")}
    except Exception:  # noqa: BLE001
        res = None
    _cache_pc_smi[smiles] = res
    return res


def nhan_dien(van_ban: str) -> dict:
    """→ {"smiles", "ten", "nguon", ...}; không nhận ra → LoiMoHinh."""
    q = (van_ban or "").strip()
    if not q:
        raise LoiMoHinh("Hãy nhập tên, công thức hoặc SMILES của chất.")
    c = nhan_chat(q)
    if c["smiles"]:
        la_sgk = c["kiem_chung"] == "da_kiem_chung"
        return {
            "smiles": c["smiles"], "ten": c["ten"] or q,
            "nguon": "bang_sgk" if la_sgk else "smiles",
            "cach_nhan_dien": "Bảng SGK (đã kiểm chứng)" if la_sgk else "SMILES",
        }
    if re.fullmatch(r"[A-Za-z0-9@+\-\[\]()=#/\\%.]+", q):    # "CCO", "c1ccccc1": SMILES trùng dạng công thức
        m = Chem.MolFromSmiles(q)
        if m is not None:
            return {"smiles": Chem.MolToSmiles(m), "ten": q, "nguon": "smiles", "cach_nhan_dien": "SMILES"}
    pc = _pubchem(q)
    if pc:
        if isinstance(pc, tuple):  # tương thích kiểm thử cũ
            smi, iupac, cid, title = pc[0], pc[1], None, ""
        else:
            smi, iupac, cid, title = pc.get("smiles", ""), pc.get("iupac", ""), pc.get("cid"), pc.get("title", "")
        ten = title or (f"{q} ({iupac})" if iupac and iupac.lower() != q.lower() else q)
        return {"smiles": smi, "ten": ten, "ten_tieng_anh": title or q, "iupac": iupac, "cid": cid,
                "nguon": "pubchem", "cach_nhan_dien": "PubChem (theo tên)"}
    raise LoiMoHinh(f"Chưa nhận ra chất \"{q}\". Thử tên tiếng Anh (ethanol), công thức thu gọn (CH3COOH) hoặc SMILES (CCO).")


PUBCHEM_3D = "https://pubchem.ncbi.nlm.nih.gov/rest/pug/compound/smiles/{}/SDF?record_type=3d"
_cache_3d: dict[str, object] = {}


def _pubchem_3d(smiles: str, cho: float = 8.0):
    """Cấu dạng 3D CHUẨN của PubChem (như trình xem 3Dmol của CHEMISTRY) → Mol có H và toạ độ; lỗi/không mạng → None."""
    if "unittest" in sys.modules or os.environ.get("VERA_TAT_PUBCHEM_3D"):
        return None                                       # kiểm thử: không gọi mạng, toạ độ RDKit cố định
    if smiles in _cache_3d:
        return _cache_3d[smiles]
    m = None
    try:
        req = urllib.request.Request(PUBCHEM_3D.format(urllib.parse.quote(smiles, safe="")), headers={"User-Agent": "VERA-Lab"})
        with urllib.request.urlopen(req, timeout=cho, context=_ssl()) as r:
            sdf = r.read().decode("utf-8", "replace")
        m = Chem.MolFromMolBlock(sdf.split("$$$$")[0], removeHs=False)
        if m is not None and m.GetNumConformers() == 0:
            m = None
    except Exception:  # noqa: BLE001
        m = None
    _cache_3d[smiles] = m
    return m


def _anh_2d_base64(mol) -> str:
    """Vẽ công thức khung 2D sắc nét (PNG base64) cho cột Thông tin chất."""
    try:
        m2 = Chem.Mol(mol)
        AllChem.Compute2DCoords(m2)
        img = Draw.MolToImage(m2, size=(280, 180))
        buf = io.BytesIO()
        img.save(buf, format="PNG", optimize=True)
        return base64.b64encode(buf.getvalue()).decode("ascii")
    except Exception:  # noqa: BLE001
        return ""


def dung_3d(smiles: str, ten: str = "", nguon: str = "", meta: dict | None = None) -> dict:
    mol = Chem.MolFromSmiles(smiles)
    if mol is None:
        raise LoiMoHinh("SMILES không hợp lệ.")
    smi_chuan = Chem.MolToSmiles(mol)
    info = dict(meta or {})
    if not info.get("cid"):
        pc_smi = _pubchem_theo_smiles(smi_chuan)
        if pc_smi:
            info.setdefault("cid", pc_smi.get("cid"))
            if not info.get("iupac"):
                info["iupac"] = pc_smi.get("iupac", "")
            if not info.get("ten_tieng_anh"):
                info["ten_tieng_anh"] = pc_smi.get("title", "")
    pc = _pubchem_3d(smi_chuan)
    if pc is not None and pc.GetNumAtoms() <= SO_NGUYEN_TU_TOI_DA:
        return _ra_ket_qua(mol, pc, ten, nguon, "pubchem_3d", info)
    mh = Chem.AddHs(mol)
    if mh.GetNumAtoms() > SO_NGUYEN_TU_TOI_DA:
        raise LoiMoHinh(f"Phân tử quá lớn để xem 3D (tối đa {SO_NGUYEN_TU_TOI_DA} nguyên tử kể cả H).")
    ts = AllChem.ETKDGv3()
    ts.randomSeed = 7
    if AllChem.EmbedMolecule(mh, ts) != 0:
        ts.useRandomCoords = True
        if AllChem.EmbedMolecule(mh, ts) != 0:
            raise LoiMoHinh("Không dựng được cấu dạng 3D cho chất này.")
    try:
        AllChem.MMFFOptimizeMolecule(mh, maxIters=400)
    except Exception:  # noqa: BLE001 – không có tham số MMFF thì giữ cấu dạng ETKDG
        pass
    return _ra_ket_qua(mol, mh, ten, nguon, "rdkit", info)


def _ra_ket_qua(mol, mh, ten: str, nguon: str, nguon_3d: str, info: dict | None = None) -> dict:
    info = info or {}
    conf = mh.GetConformer()
    tam = [sum(conf.GetAtomPosition(i)[k] for i in range(mh.GetNumAtoms())) / mh.GetNumAtoms() for k in range(3)]
    nguyen_tu = []
    for a in mh.GetAtoms():
        p = conf.GetAtomPosition(a.GetIdx())
        nguyen_tu.append({"e": a.GetSymbol(), "x": round(p.x - tam[0], 4), "y": round(p.y - tam[1], 4), "z": round(p.z - tam[2], 4),
                          "dt": a.GetFormalCharge()})
    ke = Chem.Mol(mh)
    Chem.Kekulize(ke, clearAromaticFlags=False)                 # vòng thơm vẽ đơn – đôi xen kẽ như SGK
    lien_ket = [{"a": b.GetBeginAtomIdx(), "b": b.GetEndAtomIdx(), "bac": int(b.GetBondTypeAsDouble()), "thom": b.GetIsAromatic()}
                for b in ke.GetBonds()]
    cid = info.get("cid")
    url = f"https://pubchem.ncbi.nlm.nih.gov/compound/{cid}" if cid else ""
    if nguon_3d == "pubchem_3d":
        nguon_3d_mo_ta = f"PubChem 3D conformer (CID {cid})" if cid else "PubChem 3D conformer"
    else:
        nguon_3d_mo_ta = "RDKit (ETKDG v3 + tối ưu MMFF94)"
    cach = info.get("cach_nhan_dien") or (
        "Bảng SGK (đã kiểm chứng)" if nguon == "bang_sgk" else "PubChem (theo tên)" if nguon == "pubchem" else "SMILES"
    )
    mh_xuat = Chem.Mol(mh)
    mh_xuat.SetProp("_Name", ten or Chem.MolToSmiles(mol))
    return {
        "ten": ten,
        "ten_tieng_anh": info.get("ten_tieng_anh", ""),
        "iupac": info.get("iupac", "") or ten,
        "smiles": Chem.MolToSmiles(mol),
        "nguon": nguon,
        "cach_nhan_dien": cach,
        "cid": cid,
        "url": url,
        "cong_thuc": CalcMolFormula(mol),
        "khoi_luong": round(Descriptors.MolWt(mol), 2),
        "so_nguyen_tu": mh.GetNumAtoms(),
        "nguyen_tu": nguyen_tu,
        "lien_ket": lien_ket,
        "nguon_3d": nguon_3d,
        "nguon_3d_mo_ta": nguon_3d_mo_ta,
        "anh_2d": _anh_2d_base64(mol),
        "sdf": Chem.MolToMolBlock(mh_xuat) + "$$$$\n",
        "xyz": Chem.MolToXYZBlock(mh_xuat),
    }


def tao_mo_hinh(van_ban: str) -> dict:
    nd = nhan_dien(van_ban)
    return dung_3d(nd["smiles"], nd["ten"], nd["nguon"], nd)
