"""Mô hình 3D: nhận tên / công thức / SMILES hoặc ảnh một chất → nhận diện (PubChem, AI hỗ trợ) → cấu dạng 3D.

Cấu dạng 3D ưu tiên lấy của PubChem (record_type=3d, đã tối ưu MMFF94s); không có thì RDKit tự dựng (ETKDG + MMFF94).
Kết quả trả về dạng MOL/SDF có toạ độ 3D và đủ nguyên tử H để giao diện (3Dmol.js) hiển thị và cho tải về.
"""
from __future__ import annotations

import base64
import json
import logging
import re
import time
from typing import Callable

import requests
from pydantic import BaseModel, ValidationError
from rdkit import Chem
from rdkit.Chem import AllChem, Descriptors, rdMolDescriptors

from . import db
from .llm import LoiAI, PhienHoiThoai, ai_san_sang, doc_json, khoa_hien_tai
from .parser import chuan_bi_anh, chuan_hoa
from .renderer import ve_phan_tu
from .research import PUG, _chuan_hoa_pubchem, pubchem_lookup

log = logging.getLogger(__name__)

SO_NGUYEN_TU_TOI_DA = 250   # nguyên tử nặng – lớn hơn thì dựng 3D chậm và khó xem
_SMILES_RO = re.compile(r"[=#()\[\]@/\\%]|^[cnosp]|[cnos][0-9]|[0-9][cnos]")  # dấu hiệu chắc chắn là SMILES
_cache: dict[str, dict] = {}


def _doc_cache_db(khoa: str) -> dict | None:
    """Đọc dữ liệu 3D đã tính toán từ CSDL SQLite (bảng mo_hinh_3d_cache, giữ trong 30 ngày)."""
    try:
        with db.ket_noi() as con:
            r = con.execute("SELECT data FROM mo_hinh_3d_cache WHERE key = ? AND created >= ?",
                            (khoa, time.time() - 30 * 86400)).fetchone()
            if r:
                return json.loads(r[0])
    except Exception as e:
        log.warning("Đọc cache 3D SQLite lỗi: %s", e)
    return None


def _ghi_cache_db(khoa: str, smiles: str, du_lieu: dict) -> None:
    """Lưu dữ liệu 3D vào SQLite để tái sử dụng ngay lập tức mà không cần gọi lại PubChem/RDKit."""
    try:
        with db.ket_noi() as con:
            con.execute("INSERT OR REPLACE INTO mo_hinh_3d_cache (key, smiles, created, data) VALUES (?, ?, ?, ?)",
                        (khoa, smiles, time.time(), json.dumps(du_lieu, ensure_ascii=False)))
            con.execute("DELETE FROM mo_hinh_3d_cache WHERE created < ?", (time.time() - 30 * 86400,))
    except Exception as e:
        log.warning("Ghi cache 3D SQLite lỗi: %s", e)



class NhanDien(BaseModel):
    ten: str = ""
    ten_tieng_anh: str = ""
    smiles: str = ""
    ghi_chu: str = ""


SCHEMA_NHAN_DIEN = {
    "type": "object",
    "properties": {"ten": {"type": "string"}, "ten_tieng_anh": {"type": "string"},
                   "smiles": {"type": "string"}, "ghi_chu": {"type": "string"}},
    "required": ["ten", "ten_tieng_anh", "smiles", "ghi_chu"],
    "additionalProperties": False,
}

SYSTEM_NHAN_DIEN = """Bạn là chuyên gia hoá học. Từ văn bản hoặc ảnh người dùng gửi (tên chất tiếng Việt/tiếng Anh, công thức thu gọn
như CH3COOC2H5, công thức cấu tạo vẽ tay hoặc in, ảnh đề bài...), hãy xác định MỘT chất cần dựng mô hình 3D.
- ten: tên theo SGK Hoá học Việt Nam hiện hành (danh pháp IUPAC, vd "ethanol", "acetic acid", "benzene"); kèm tên thường dùng nếu có.
- ten_tieng_anh: tên tiếng Anh (IUPAC hoặc tên thông dụng) để tra PubChem.
- smiles: SMILES chuẩn của chất (có lập thể nếu đề/ảnh thể hiện rõ).
- ghi_chu: ghi ngắn nếu ảnh/đề có nhiều chất (đã chọn chất nào và vì sao) hoặc có điểm chưa chắc chắn; không có thì để trống.
Nếu không nhận ra chất nào thì để smiles rỗng và giải thích trong ghi_chu."""


# ---------------------------------------------------------------- PubChem
def _pubchem_theo_smiles(smiles: str) -> dict | None:
    """Tra PubChem theo cấu trúc (SMILES gửi trong thân POST để không lỗi kí tự # / +)."""
    try:
        r = requests.post(f"{PUG}/smiles/property/Title,IUPACName,MolecularFormula,MolecularWeight,SMILES/JSON",
                          data={"smiles": smiles}, timeout=6, headers={"User-Agent": "GiaiChuoiHuuCo/1.0"})
        if r.status_code == 404:
            return None
        r.raise_for_status()
        props = r.json().get("PropertyTable", {}).get("Properties", [])
    except (requests.RequestException, ValueError) as e:
        log.warning("PubChem (SMILES) lỗi: %s", e)
        return None
    if not props or not props[0].get("CID"):
        return None
    return _chuan_hoa_pubchem(props[0])


def _pubchem_sdf_3d(cid: int) -> str | None:
    try:
        r = requests.get(f"{PUG}/cid/{cid}/record/SDF", params={"record_type": "3d"}, timeout=6,
                         headers={"User-Agent": "GiaiChuoiHuuCo/1.0"})
    except requests.RequestException as e:
        log.warning("PubChem 3D lỗi: %s", e)
        return None
    return r.text if r.status_code == 200 and ("V2000" in r.text or "V3000" in r.text) else None


# ---------------------------------------------------------------- nhận diện chất
def _hoi_ai(van_ban: str, anh: bytes | None, bao: Callable[[str], None] | None) -> NhanDien:
    yeu_cau = "Xác định chất cần dựng mô hình 3D."
    if van_ban:
        yeu_cau += f"\n\nNgười dùng nhập: {van_ban}"
    anh_b64 = chuan_bi_anh(anh) if anh else None
    if anh:
        yeu_cau += "\n\nChất nằm trong ảnh đính kèm" + (" (văn bản ở trên là ghi chú thêm)." if van_ban else ".")
    phien = PhienHoiThoai(SYSTEM_NHAN_DIEN, output_schema=SCHEMA_NHAN_DIEN, effort="low", max_tokens=4000, bao=bao)
    ph = phien.gui(text=yeu_cau, anh=anh_b64)
    try:
        return NhanDien.model_validate(doc_json(ph.text))
    except (ValueError, ValidationError) as e:
        raise LoiAI("AI trả về kết quả nhận diện không đúng định dạng. Vui lòng thử lại.") from e


def _ai_san_sang() -> bool:
    return ai_san_sang()  # theo key của yêu cầu (key riêng / key chủ app / không được dùng AI)


def nhan_dien(van_ban: str = "", anh: bytes | None = None, smiles: str = "",
              bao: Callable[[str], None] | None = None) -> dict:
    """→ {smiles, ten, ten_tieng_anh, pubchem (dict|None), cach, canh_bao[]}."""
    canh_bao: list[str] = []
    t = chuan_hoa(van_ban or "")
    if smiles.strip():                                     # gọi từ kết quả giải chuỗi: đã có SMILES
        return {"smiles": smiles.strip(), "ten": t, "ten_tieng_anh": "", "pubchem": None, "cach": "SMILES từ lời giải",
                "canh_bao": canh_bao}

    if t and not anh:
        # SMILES trước (vd "CCO", "CC=C", "c1ccccc1"): tên PubChem đôi khi trùng chữ viết tắt ("CCO" là tên một thuốc).
        # Công thức phân tử (C2H6O, CH3COOH) không phải SMILES hợp lệ hoặc có chữ số → không rơi vào nhánh này.
        if " " not in t and (_SMILES_RO.search(t) or not re.search(r"\d", t)) and Chem.MolFromSmiles(t) is not None:
            return {"smiles": t, "ten": "", "ten_tieng_anh": "", "pubchem": None, "cach": "SMILES", "canh_bao": canh_bao}
        r = pubchem_lookup(t)
        if r.get("ok") and r.get("smiles"):
            return {"smiles": r["smiles"], "ten": r.get("ten", ""), "ten_tieng_anh": r.get("ten", ""), "pubchem": r,
                    "cach": "PubChem (theo tên)", "canh_bao": canh_bao}

    if not t and not anh:
        raise LoiAI("Hãy nhập tên, công thức hoặc SMILES của chất, hoặc tải ảnh.")

    if _ai_san_sang():
        nd = _hoi_ai(t, anh, bao)
        if nd.ghi_chu:
            canh_bao.append(nd.ghi_chu)
        r = pubchem_lookup(nd.ten_tieng_anh) if nd.ten_tieng_anh else {}
        if r.get("ok") and r.get("smiles"):
            return {"smiles": r["smiles"], "ten": nd.ten or r.get("ten", ""), "ten_tieng_anh": nd.ten_tieng_anh,
                    "pubchem": r, "cach": "AI nhận diện → PubChem", "canh_bao": canh_bao}
        if nd.smiles and Chem.MolFromSmiles(nd.smiles) is not None:
            return {"smiles": nd.smiles, "ten": nd.ten, "ten_tieng_anh": nd.ten_tieng_anh, "pubchem": None,
                    "cach": "AI nhận diện", "canh_bao": canh_bao}
        raise LoiAI("Không nhận ra chất. " + (nd.ghi_chu or "Hãy nhập tên tiếng Anh hoặc SMILES."))

    # không có AI: thử tra theo công thức phân tử (có thể nhiều đồng phân)
    r = pubchem_lookup(t) if t else {}
    if r.get("ok") and r.get("ung_vien"):
        uv = r["ung_vien"][0]
        canh_bao.append(f"Tra theo công thức phân tử: PubChem có nhiều đồng phân, đã chọn {uv.get('ten')}. "
                        "Nhập tên hoặc SMILES để chọn đúng chất.")
        return {"smiles": uv["smiles"], "ten": uv.get("ten", ""), "ten_tieng_anh": uv.get("ten", ""), "pubchem": uv,
                "cach": "PubChem (theo công thức)", "canh_bao": canh_bao}
    if khoa_hien_tai().nguon == "khong_co":
        can = "Đọc ảnh" if anh else "Nhận diện tên tiếng Việt/tên thông dụng"
        raise LoiAI(f"{can} cần AI: hãy đăng nhập và thêm key Gemini trong Cài đặt (hoặc còn lượt dùng thử). "
                    "Không cần AI nếu nhập tên tiếng Anh (vd ethanol) hoặc SMILES (vd CCO).")
    raise LoiAI("Không nhận ra chất (PubChem không có tên này và chưa có API key AI để đọc). "
                "Hãy nhập tên tiếng Anh (vd ethanol) hoặc SMILES (vd CCO).")


# ---------------------------------------------------------------- dựng 3D
def dung_3d_rdkit(smiles: str):
    """SMILES → mol có H và toạ độ 3D (ETKDG v3 + tối ưu MMFF94, không được thì UFF). None nếu không dựng được.
    Đối với phân tử linh động, tìm kiếm đa cấu dạng (5 conformers) và chọn cấu dạng có thế năng tối ưu nhất.
    """
    mol = Chem.MolFromSmiles(smiles)
    if mol is None:
        return None
    mol = Chem.AddHs(mol)
    ts = AllChem.ETKDGv3()
    ts.randomSeed = 20261010

    # Phân tử linh động: sinh 5 cấu dạng rồi chọn cấu dạng có thế năng thấp nhất
    heavy_atoms = mol.GetNumHeavyAtoms()
    num_rotatable = rdMolDescriptors.CalcNumRotatableBonds(mol)

    if heavy_atoms <= 60 and num_rotatable > 0:
        cids = AllChem.EmbedMultipleConfs(mol, numConfs=5, params=ts)
        if not cids:
            ts.useRandomCoords = True
            cids = AllChem.EmbedMultipleConfs(mol, numConfs=5, params=ts)
        if cids:
            best_cid = cids[0]
            best_energy = float("inf")
            has_mmff = AllChem.MMFFHasAllMoleculeParams(mol)
            for cid in cids:
                try:
                    if has_mmff:
                        prop = AllChem.MMFFGetMoleculeProperties(mol)
                        ff = AllChem.MMFFGetMoleculeForceField(mol, prop, confId=cid)
                        if ff:
                            ff.Minimize(maxIts=500)
                            energy = ff.CalcEnergy()
                            if energy < best_energy:
                                best_energy = energy
                                best_cid = cid
                    else:
                        ff = AllChem.UFFGetMoleculeForceField(mol, confId=cid)
                        if ff:
                            ff.Minimize(maxIts=500)
                            energy = ff.CalcEnergy()
                            if energy < best_energy:
                                best_energy = energy
                                best_cid = cid
                except Exception:
                    pass
            best_conf = Chem.Conformer(mol.GetConformer(best_cid))
            mol.RemoveAllConformers()
            mol.AddConformer(best_conf, assignId=True)
    else:
        if AllChem.EmbedMolecule(mol, ts) != 0:
            ts.useRandomCoords = True
            if AllChem.EmbedMolecule(mol, ts) != 0:
                return None
        try:
            if AllChem.MMFFHasAllMoleculeParams(mol):
                AllChem.MMFFOptimizeMolecule(mol, maxIters=500)
            else:
                AllChem.UFFOptimizeMolecule(mol, maxIters=500)
        except Exception:  # noqa: BLE001 – toạ độ ETKDG vẫn dùng được
            pass

    # Tính điện tích từng phần Gasteiger để phục vụ tooltip và hiển thị bề mặt
    try:
        AllChem.ComputeGasteigerCharges(mol)
    except Exception:
        pass

    return mol


def _cung_chat(mol_3d, smiles: str) -> bool:
    """Cấu dạng PubChem có đúng là chất này không (so công thức phân tử)."""
    goc = Chem.MolFromSmiles(smiles)
    return goc is not None and rdMolDescriptors.CalcMolFormula(goc) == rdMolDescriptors.CalcMolFormula(mol_3d)


def _anh_2d(smiles: str) -> str:
    import io
    img = ve_phan_tu(smiles)
    if img is None:
        return ""
    buf = io.BytesIO()
    img.save(buf, "PNG", optimize=True)
    return "data:image/png;base64," + base64.b64encode(buf.getvalue()).decode()


def _trich_thong_tin_nguyen_tu(mol_3d) -> list[dict]:
    """Trích xuất danh sách nguyên tử, toạ độ 3D và điện tích từng phần để frontend tương tác."""
    ds = []
    if not mol_3d or not mol_3d.GetNumConformers():
        return ds
    conf = mol_3d.GetConformer()
    for i, a in enumerate(mol_3d.GetAtoms()):
        q = None
        if a.HasProp("_GasteigerCharge"):
            try:
                val = a.GetDoubleProp("_GasteigerCharge")
                if val == val and not abs(val) > 100:  # kiểm tra NaN / Inf
                    q = round(val, 3)
            except Exception:
                pass
        pos = conf.GetAtomPosition(i)
        ds.append({
            "idx": i,
            "elem": a.GetSymbol(),
            "atomic_num": a.GetAtomicNum(),
            "x": round(pos.x, 3),
            "y": round(pos.y, 3),
            "z": round(pos.z, 3),
            "charge": q,
        })
    return ds


def tao_mo_hinh_3d(van_ban: str = "", anh: bytes | None = None, smiles: str = "",
                   bao: Callable[[str], None] | None = None) -> dict:
    khoa = f"{smiles.strip()}|{van_ban.strip()}" if not anh else ""
    if khoa:
        if khoa in _cache:
            return _cache[khoa]
        cached_db = _doc_cache_db(khoa)
        if cached_db:
            _cache[khoa] = cached_db
            return cached_db

    nd = nhan_dien(van_ban, anh, smiles, bao)
    goc = Chem.MolFromSmiles(nd["smiles"])
    if goc is None:
        raise LoiAI(f"SMILES không hợp lệ: {nd['smiles']}")
    if any(a.GetAtomicNum() == 0 for a in goc.GetAtoms()):
        raise LoiAI("Đây là polime (mắt xích có đầu nối *) – hãy nhập monome hoặc một phân tử cụ thể để dựng 3D.")
    if goc.GetNumHeavyAtoms() > SO_NGUYEN_TU_TOI_DA:
        raise LoiAI(f"Phân tử quá lớn ({goc.GetNumHeavyAtoms()} nguyên tử nặng) để dựng 3D trong app.")
    smiles_chuan = Chem.MolToSmiles(goc)

    # Kiểm tra xem cấu trúc SMILES chuẩn này đã có trong cache chưa
    khoa_smiles = f"smiles:{smiles_chuan}"
    cached_smiles = _cache.get(khoa_smiles) or _doc_cache_db(khoa_smiles)
    if cached_smiles and not anh:
        res = dict(cached_smiles)
        if nd.get("ten"):
            res["ten"] = nd["ten"]
        if nd.get("cach"):
            res["cach_nhan_dien"] = nd["cach"]
        if khoa:
            _cache[khoa] = res
            _ghi_cache_db(khoa, smiles_chuan, res)
        return res

    pc = nd["pubchem"] or _pubchem_theo_smiles(smiles_chuan)
    canh_bao = list(nd["canh_bao"])
    mol3d, nguon = None, ""
    if pc and pc.get("cid"):
        sdf = _pubchem_sdf_3d(pc["cid"])
        if sdf:
            m = Chem.MolFromMolBlock(sdf, removeHs=False)
            if m is not None and m.GetNumConformers() and m.GetConformer().Is3D() and _cung_chat(m, smiles_chuan):
                mol3d, nguon = m, f"PubChem 3D conformer (CID {pc['cid']})"
                try:
                    AllChem.ComputeGasteigerCharges(mol3d)
                except Exception:
                    pass

    if mol3d is None:
        mol3d = dung_3d_rdkit(smiles_chuan)
        if mol3d is None:
            raise LoiAI("Không dựng được cấu dạng 3D cho chất này.")
        nguon = "RDKit (ETKDG v3 + tối ưu MMFF94)"
        if pc and pc.get("cid"):
            canh_bao.append("PubChem không có cấu dạng 3D cho chất này – app tự dựng bằng RDKit.")

    ten = nd["ten"] or (pc or {}).get("ten", "") or smiles_chuan
    mol3d.SetProp("_Name", ten)
    nguyen_tu_list = _trich_thong_tin_nguyen_tu(mol3d)

    kq = {
        "ten": ten,
        "ten_tieng_anh": nd["ten_tieng_anh"] or (pc or {}).get("ten", ""),
        "iupac": (pc or {}).get("iupac", ""),
        "cong_thuc": rdMolDescriptors.CalcMolFormula(goc),
        "khoi_luong": round(Descriptors.MolWt(goc), 2),
        "smiles": smiles_chuan,
        "cid": (pc or {}).get("cid"),
        "url": (pc or {}).get("url", ""),
        "cach_nhan_dien": nd["cach"],
        "nguon_3d": nguon,
        "so_nguyen_tu": mol3d.GetNumAtoms(),
        "sdf": Chem.MolToMolBlock(mol3d) + "$$$$\n",
        "xyz": Chem.MolToXYZBlock(mol3d),
        "anh_2d": _anh_2d(smiles_chuan),
        "nguyen_tu": nguyen_tu_list,
        "canh_bao": canh_bao,
    }
    if khoa:
        _cache[khoa] = kq
        _ghi_cache_db(khoa, smiles_chuan, kq)
    _cache[khoa_smiles] = kq
    _ghi_cache_db(khoa_smiles, smiles_chuan, kq)
    return kq

