"""Bộ đề kiểm tra AI Riêng: chạy cả bộ (thread nền, tuần tự), chấm tự động, lưu lịch sử điểm.

Chấm từng chất theo kí hiệu (A, B, X…): SMILES đáp án → so cấu tạo chuẩn hoá bằng RDKit · công thức / tên → so chữ đã chuẩn hoá với
ctct / tên AI ghi · khác CÔNG THỨC PHÂN TỬ (đếm nguyên tố từ công thức cấu tạo thu gọn) → sai · còn lại (tên, đồng phân cùng CTPT)
→ nhờ AI rẻ so (nếu bật) hoặc "chưa chấm được".
"""
from __future__ import annotations

import logging
import re
import threading
import time
from collections import Counter

from rdkit import Chem
from rdkit.Chem import rdMolDescriptors

from ..llm import KHOA_CHU_APP, KeHoachAI, LoiAI, PhienHoiThoai, doc_json, dung_ke_hoach, dung_khoa
from ..schemas import LoiGiaiGon
from . import giai as ai_rieng_giai, kho
from .song_song import ban_do

CAU_CUNG_LUC = 2  # số câu của Bộ đề giải cùng lúc

log = logging.getLogger(__name__)
_dang_chay = threading.Lock()
_NGUYEN_TO = {Chem.GetPeriodicTable().GetElementSymbol(z) for z in range(1, 119)}


# ---------------------------------------------------------------- so sánh chất
def _chuan_chu(t: str) -> str:
    t = (t or "").translate(str.maketrans("₀₁₂₃₄₅₆₇₈₉", "0123456789")).replace("đ", "d").replace("Đ", "D")
    return re.sub(r"[\s\-–—=≡·.()\[\]]", "", t).casefold()


def _smiles_chuan(s: str, lap_the: bool = True) -> str | None:
    m = Chem.MolFromSmiles(s or "") if s else None
    return Chem.MolToSmiles(m, isomericSmiles=lap_the) if m is not None else None


def dem_nguyen_to(ctct: str) -> Counter | None:
    """Công thức (cấu tạo thu gọn / phân tử) → đếm nguyên tố: CH3-CH2-OH, (CH3)2CHOH, CH2=CH-COOH, C6H5ONa. Không phải công thức → None."""
    t = re.sub(r"[\s\-–—=≡·]", "", (ctct or "").translate(str.maketrans("₀₁₂₃₄₅₆₇₈₉", "0123456789")))
    if not t or not re.fullmatch(r"[A-Za-z0-9()\[\]]+", t) or not re.match(r"[A-Z(\[]", t):
        return None
    vi_tri = 0

    def nhom() -> Counter:
        nonlocal vi_tri
        c: Counter = Counter()
        while vi_tri < len(t):
            ch = t[vi_tri]
            if ch in "([":
                vi_tri += 1
                trong = nhom()
                if vi_tri >= len(t) or t[vi_tri] not in ")]":
                    raise ValueError
                vi_tri += 1
                so = re.match(r"\d*", t[vi_tri:]).group(0)
                vi_tri += len(so)
                for k, v in trong.items():
                    c[k] += v * int(so or 1)
            elif ch in ")]":
                return c
            else:
                m = re.match(r"([A-Z][a-z]?)(\d*)", t[vi_tri:])
                if not m:
                    raise ValueError
                if m.group(1) not in _NGUYEN_TO:
                    raise ValueError
                c[m.group(1)] += int(m.group(2) or 1)
                vi_tri += len(m.group(0))
        return c

    try:
        kq = nhom()
        return kq if vi_tri == len(t) and kq else None
    except (ValueError, RuntimeError):
        return None


def _ctpt_smiles(s: str) -> Counter | None:
    m = Chem.MolFromSmiles(s or "") if s else None
    if m is None:
        return None
    ct = rdMolDescriptors.CalcMolFormula(Chem.AddHs(m))
    return dem_nguyen_to(re.sub(r"[+\-]\d*$", "", ct))


def so_chat(mong_doi: dict, chat_ai: dict | None) -> tuple[str, str]:
    """→ (dung | sai | chua_ro | thieu, cách chấm)."""
    if chat_ai is None:
        return "thieu", "AI không có chất này"
    smiles_md = mong_doi.get("smiles", "")
    if smiles_md:
        a, b = _smiles_chuan(smiles_md), _smiles_chuan(chat_ai.get("smiles", ""))
        if a is None:
            return "chua_ro", "SMILES đáp án không hợp lệ"
        if b is None:
            return "sai", "SMILES của AI không hợp lệ"
        if a == b or (_smiles_chuan(smiles_md, False) == _smiles_chuan(chat_ai.get("smiles", ""), False) and "@" not in smiles_md
                      and "/" not in smiles_md and "\\" not in smiles_md):
            return "dung", "SMILES (RDKit)"
        return "sai", "SMILES (RDKit)"
    chat = mong_doi.get("chat", "")
    if _chuan_chu(chat) in {_chuan_chu(chat_ai.get(k, "")) for k in ("ctct", "ten", "smiles") if chat_ai.get(k)}:
        return "dung", "trùng chữ"
    a = _smiles_chuan(chat) if re.fullmatch(r"[A-Za-z0-9@+\-\[\]()=#/\\%.]+", chat or "") and re.search(r"[a-z]", chat or "") is None else None
    if a is not None and a == _smiles_chuan(chat_ai.get("smiles", "")):
        return "dung", "SMILES (RDKit)"
    md, ai = dem_nguyen_to(chat), _ctpt_smiles(chat_ai.get("smiles", "")) or dem_nguyen_to(chat_ai.get("ctct", ""))
    if md is not None and ai is not None and md != ai:
        return "sai", "khác công thức phân tử"
    return "chua_ro", "cùng CTPT / là tên chất – cần so cấu tạo"


SYSTEM_CHAM = """So từng cặp: chất trong ĐÁP ÁN (công thức hoặc tên tiếng Việt, cũ hay mới) và chất AI trả lời (ctct, tên, SMILES).
dung = true khi là CÙNG một chất (cùng cấu tạo; tên cũ / mới / IUPAC khác nhau vẫn là cùng chất). Trả JSON {"ket_qua": [{"ki_hieu", "dung"}]}."""
SCHEMA_CHAM = {"type": "object", "properties": {"ket_qua": {"type": "array", "items": {
    "type": "object", "properties": {"ki_hieu": {"type": "string"}, "dung": {"type": "boolean"}}, "required": ["ki_hieu", "dung"]}}},
    "required": ["ket_qua"]}


def _kh(k: str) -> str:
    return re.sub(r"\s+", "", k or "").upper()


def cham(dap_an: list[dict], gon: LoiGiaiGon | None, dung_ai: bool) -> dict:
    """→ {"dung": bool, "chat": [{ki_hieu, mong_doi, ai, ket_qua, cach}]}."""
    theo_kh = {_kh(c.ki_hieu): c.model_dump() for c in (gon.chuoi if gon else [])}
    chat = []
    for md in dap_an:
        ai = theo_kh.get(_kh(md["ki_hieu"]))
        kq, cach = so_chat(md, ai)
        chat.append({"ki_hieu": md["ki_hieu"], "mong_doi": md.get("chat") or md.get("smiles"),
                     "ai": (ai or {}).get("ctct") or (ai or {}).get("smiles") or "", "ai_ten": (ai or {}).get("ten", ""),
                     "ket_qua": kq, "cach": cach})
    chua_ro = [c for c in chat if c["ket_qua"] == "chua_ro" and c["ai"]]
    if chua_ro and dung_ai:
        try:
            ph = PhienHoiThoai(SYSTEM_CHAM, output_schema=SCHEMA_CHAM, effort="low", max_tokens=1200, bac="lite").gui(
                text="\n".join(f"{c['ki_hieu']}: ĐÁP ÁN = {c['mong_doi']} | AI = {c['ai']} ({c['ai_ten']})" for c in chua_ro))
            theo = {_kh(x["ki_hieu"]): bool(x["dung"]) for x in (doc_json(ph.text) or {}).get("ket_qua", [])}
            for c in chua_ro:
                if _kh(c["ki_hieu"]) in theo:
                    c["ket_qua"], c["cach"] = ("dung" if theo[_kh(c["ki_hieu"])] else "sai"), "AI so (mô hình rẻ)"
        except (LoiAI, ValueError) as e:
            log.info("Không nhờ AI chấm được: %s", e)
    return {"dung": bool(chat) and all(c["ket_qua"] == "dung" for c in chat), "chat": chat}


# ---------------------------------------------------------------- chạy cả bộ đề
def bat_dau() -> int:
    """Tạo một lần kiểm tra và chạy nền. ValueError nếu đang có lần khác chạy / bộ đề trống."""
    cau_hoi = kho.ds_cau_hoi(chi_bat=True)
    if not cau_hoi:
        raise ValueError("Bộ đề trống – thêm đề có đáp án trước.")
    if kho.dang_kiem_tra() or _dang_chay.locked():
        raise ValueError("Đang có một lần kiểm tra chạy – đợi xong đã.")
    cd = kho.cai_dat()
    lid = kho.tao_lan_kiem_tra(len(cau_hoi), {k: cd[k] for k in ("che_do", "mo_hinh_t2", "cho_phep_t3", "top_k",
                                                                 "bo_qua_thu_vien_khi_kiem_tra")} | {"phien_ban_kho": kho.phien_ban()})
    threading.Thread(target=chay, args=(lid, cau_hoi), daemon=True, name=f"ai-rieng-kiem-tra-{lid}").start()
    return lid


def chay(lid: int, cau_hoi: list[dict]) -> None:
    cd = kho.cai_dat()
    chi_tiet: list[dict] = []
    khoa_ghi = threading.Lock()

    def mot_cau(c: dict) -> None:
        t0 = time.time()
        muc = {"id": c["id"], "de": c["de"], "chu_de": c["chu_de"]}
        try:
            with dung_khoa(KHOA_CHU_APP), dung_ke_hoach(KeHoachAI(loai="giai", buoc="ai_rieng_kiem_tra")):
                kq = ai_rieng_giai.giai(c["de"], bo_qua_thu_vien=cd["bo_qua_thu_vien_khi_kiem_tra"], ghi_thu_vien=False)
                ket = cham(c["dap_an"], kq.gon, cd["cham_bang_ai"])
            muc.update(dung=ket["dung"] and kq.tang != "chua_day", chat=ket["chat"], tang=kq.tang, mo_hinh=kq.mo_hinh,
                       chua_duoc_day=kq.chua_duoc_day, da_dung=kq.da_dung,
                       doi_chieu={k: kq.doi_chieu.get(k) for k in ("ket_luan", "do_tin_cay", "khac", "chon")})
        except LoiAI as e:
            muc.update(dung=False, loi=str(e)[:300], chat=[])
        muc["giay"] = round(time.time() - t0, 1)
        with khoa_ghi:  # ghi tiến độ ngay khi xong từng câu (giao diện hiện dần), giữ thứ tự theo bộ đề
            chi_tiet.append(muc)
            chi_tiet.sort(key=lambda x: x["id"])
            kho.cap_nhat_lan_kiem_tra(lid, chi_tiet)

    with _dang_chay:
        try:
            for loi in ban_do(mot_cau, cau_hoi, CAU_CUNG_LUC):  # vài câu cùng lúc (mỗi câu có thể có làn Gemini đối chiếu)
                if isinstance(loi, Exception):
                    raise loi
            kho.cap_nhat_lan_kiem_tra(lid, chi_tiet, xong=True)
        except Exception:  # noqa: BLE001
            log.exception("AI Riêng: lần kiểm tra %s lỗi", lid)
            kho.cap_nhat_lan_kiem_tra(lid, chi_tiet, loi=True)


def theo_chu_de(chi_tiet: list[dict]) -> list[dict]:
    """Tỉ lệ đúng theo chủ đề (chủ đề sai nhiều trước) – để dạy thêm đúng chỗ."""
    dem: dict[str, list[int]] = {}
    for x in chi_tiet:
        for cd in [s.strip() for s in (x.get("chu_de") or "chưa gắn chủ đề").split(",") if s.strip()] or ["chưa gắn chủ đề"]:
            d = dem.setdefault(cd, [0, 0])
            d[0] += bool(x.get("dung"))
            d[1] += 1
    return sorted(({"chu_de": k, "dung": v[0], "tong": v[1], "ti_le": round(100 * v[0] / v[1])} for k, v in dem.items()),
                  key=lambda x: (x["ti_le"], -x["tong"]))
