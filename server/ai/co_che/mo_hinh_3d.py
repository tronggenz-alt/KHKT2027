"""Mục "Mô hình 3D" của màn Giải bài tập (VERA), viết lại từ backend/mo_hinh_3d.py của app CHEMISTRY.

Tên / công thức / SMILES → nhận diện chất → cấu dạng 3D (RDKit ETKDG + MMFF) → nguyên tử, liên kết cho app vẽ.
Thứ tự nhận diện (không cần đăng nhập, không cần key):
  1. bảng chất SGK của giai_chuoi (tên tiếng Việt, tên IUPAC, công thức thu gọn) – đã kiểm chứng;
  2. SMILES hợp lệ do người dùng gõ;
  3. tên tiếng Anh qua PubChem (chỉ gửi tên chất, có thời hạn chờ); không có mạng thì bỏ qua.
"""
from __future__ import annotations

import json
import re
import os
import ssl
import urllib.parse
import urllib.request

from rdkit import Chem, RDLogger
from rdkit.Chem import AllChem, Descriptors
from rdkit.Chem.rdMolDescriptors import CalcMolFormula

from .giai_chuoi import nhan_chat

RDLogger.DisableLog("rdApp.*")

PUBCHEM = "https://pubchem.ncbi.nlm.nih.gov/rest/pug/compound/name/{}/property/IsomericSMILES,IUPACName/JSON"
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


def _pubchem(ten: str, cho: float = 6.0) -> tuple[str, str] | None:
    """Tên tiếng Anh → (SMILES, tên IUPAC) qua PubChem; lỗi mạng / không thấy → None."""
    if not re.fullmatch(r"[A-Za-z0-9 ,\-()'\[\]]{2,80}", ten):
        return None
    url = PUBCHEM.format(urllib.parse.quote(ten.strip()))
    try:
        req = urllib.request.Request(url, headers={"User-Agent": "VERA-Lab/1.0"})
        with urllib.request.urlopen(req, timeout=cho, context=_ssl()) as r:
            p = json.loads(r.read().decode("utf-8"))["PropertyTable"]["Properties"][0]
        smi = p.get("IsomericSMILES") or p.get("SMILES") or ""
        return (smi, p.get("IUPACName", "")) if smi else None
    except Exception:  # noqa: BLE001 – không có mạng hoặc PubChem không biết tên này
        return None


def nhan_dien(van_ban: str) -> dict:
    """→ {"smiles", "ten", "nguon"}; không nhận ra → LoiMoHinh."""
    q = (van_ban or "").strip()
    if not q:
        raise LoiMoHinh("Hãy nhập tên, công thức hoặc SMILES của chất.")
    c = nhan_chat(q)
    if c["smiles"]:
        return {"smiles": c["smiles"], "ten": c["ten"] or q, "nguon": "bang_sgk" if c["kiem_chung"] == "da_kiem_chung" else "smiles"}
    if re.fullmatch(r"[A-Za-z0-9@+\-\[\]()=#/\\%.]+", q):    # "CCO", "c1ccccc1": SMILES trùng dạng công thức
        m = Chem.MolFromSmiles(q)
        if m is not None:
            return {"smiles": Chem.MolToSmiles(m), "ten": q, "nguon": "smiles"}
    pc = _pubchem(q)
    if pc:
        ten = f"{q} ({pc[1]})" if pc[1] and pc[1].lower() != q.lower() else q
        return {"smiles": pc[0], "ten": ten, "nguon": "pubchem"}
    raise LoiMoHinh(f"Chưa nhận ra chất \"{q}\". Thử tên tiếng Anh (ethanol), công thức thu gọn (CH3COOH) hoặc SMILES (CCO).")


PUBCHEM_3D = "https://pubchem.ncbi.nlm.nih.gov/rest/pug/compound/smiles/{}/SDF?record_type=3d"
_cache_3d: dict[str, object] = {}


def _pubchem_3d(smiles: str, cho: float = 8.0):
    """Cấu dạng 3D CHUẨN của PubChem (như trình xem 3Dmol của CHEMISTRY) → Mol có H và toạ độ; lỗi/không mạng → None."""
    import sys
    import urllib.parse
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


def dung_3d(smiles: str, ten: str = "", nguon: str = "") -> dict:
    mol = Chem.MolFromSmiles(smiles)
    if mol is None:
        raise LoiMoHinh("SMILES không hợp lệ.")
    pc = _pubchem_3d(Chem.MolToSmiles(mol))
    if pc is not None and pc.GetNumAtoms() <= SO_NGUYEN_TU_TOI_DA:
        return _ra_ket_qua(mol, pc, ten, nguon, "pubchem_3d")
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
    return _ra_ket_qua(mol, mh, ten, nguon, "rdkit")


def _ra_ket_qua(mol, mh, ten: str, nguon: str, nguon_3d: str) -> dict:
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
    return {
        "ten": ten, "smiles": Chem.MolToSmiles(mol), "nguon": nguon,
        "cong_thuc": CalcMolFormula(mol), "khoi_luong": round(Descriptors.MolWt(mol), 2),
        "so_nguyen_tu": mh.GetNumAtoms(), "nguyen_tu": nguyen_tu, "lien_ket": lien_ket, "nguon_3d": nguon_3d,
    }


def tao_mo_hinh(van_ban: str) -> dict:
    nd = nhan_dien(van_ban)
    return dung_3d(nd["smiles"], nd["ten"], nd["nguon"])
