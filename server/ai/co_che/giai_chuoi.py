"""Giải chuỗi phản ứng hữu cơ cho VERA: tách chuỗi, nhận chất, tra bước đã kiểm chứng, gắn cơ chế mẫu nếu khớp.

Phần lõi lấy ý tưởng từ app CHEMISTRY (nhánh chemistry: backend/parser.py tách chuỗi, backend/giai_nhanh.py lời dặn
SYSTEM_GIAI_GON + kiểm tra RDKit "đúng công thức đề cho"), viết lại gọn, KHÔNG import chéo.

Hai tầng:
  1. Tại máy, không cần mạng: bảng chất thường gặp (SGK 11–12) + bảng biến đổi đã kiểm chứng (phản ứng, điều kiện, loại,
     mã cơ chế demo). Ba chuỗi mẫu (CHUOI_MAU) giải trọn ở tầng này.
  2. Bước/chất chưa có trong bảng: nhờ AI (tại máy trước, trực tuyến sau) trả JSON gọn; SMILES phải hợp lệ RDKit và đúng
     công thức đề cho. Mọi phần do AI đưa ra ghi "chưa kiểm chứng".
"""
from __future__ import annotations

import json
import re
import unicodedata

from rdkit import Chem, RDLogger
from rdkit.Chem.rdMolDescriptors import CalcMolFormula

RDLogger.DisableLog("rdApp.*")

CHUOI_MAU = [
    "C2H4 -> C2H5OH -> CH3CHO -> CH3COOH -> CH3COOC2H5",
    "CH4 -> CH3Cl -> CH3OH -> HCHO",
    "C3H6 -> CH3CHBrCH3 -> CH3CH(OH)CH3 -> CH3COCH3",
]

# ki hiệu (đã chuẩn hoá: bỏ dấu cách, gạch nối, chữ hoa) → (SMILES, tên SGK 2018, CTCT thu gọn)
_CHAT = [
    (("CH4", "METAN", "METHANE"), "C", "methane", "CH₄"),
    (("C2H6", "ETAN", "ETHANE", "CH3CH3"), "CC", "ethane", "CH₃–CH₃"),
    (("C2H4", "CH2CH2", "ETILEN", "ETHYLENE", "ETHENE"), "C=C", "ethene (etilen)", "CH₂=CH₂"),
    (("C2H2", "CHCH", "AXETILEN", "ACETYLENE", "ETHYNE"), "C#C", "ethyne (axetilen)", "CH≡CH"),
    (("C3H6", "CH2CHCH3", "PROPEN", "PROPENE", "PROPILEN"), "C=CC", "propene", "CH₂=CH–CH₃"),
    (("C3H8", "PROPAN", "PROPANE"), "CCC", "propane", "CH₃–CH₂–CH₃"),
    (("CH3CL", "CLOMETAN", "CHLOROMETHANE"), "CCl", "chloromethane", "CH₃Cl"),
    (("CH3BR", "BROMMETAN", "BROMOMETHANE"), "CBr", "bromomethane", "CH₃Br"),
    (("C2H5CL", "CH3CH2CL"), "CCCl", "chloroethane", "CH₃–CH₂Cl"),
    (("C2H5BR", "CH3CH2BR"), "CCBr", "bromoethane", "CH₃–CH₂Br"),
    (("CH3CHBRCH3", "2BROMPROPAN", "2BROMOPROPANE", "CH3CH(BR)CH3"), "CC(C)Br", "2-bromopropane", "CH₃–CHBr–CH₃"),
    (("CH3OH", "METANOL", "METHANOL"), "CO", "methanol", "CH₃OH"),
    (("C2H5OH", "CH3CH2OH", "ETANOL", "ETHANOL", "ANCOLETYLIC", "RUOUETYLIC"), "CCO", "ethanol", "CH₃–CH₂OH"),
    (("CH3CH(OH)CH3", "PROPAN2OL", "2PROPANOL", "ANCOLISOPROPYLIC"), "CC(C)O", "propan-2-ol", "CH₃–CH(OH)–CH₃"),
    (("HCHO", "FOMANDEHIT", "FORMALDEHYDE", "METANAL"), "C=O", "methanal (formaldehyde)", "HCHO"),
    (("CH3CHO", "ANDEHITAXETIC", "ACETALDEHYDE", "ETANAL"), "CC=O", "ethanal (acetaldehyde)", "CH₃CHO"),
    (("CH3COCH3", "AXETON", "ACETONE", "PROPANON", "PROPANONE"), "CC(C)=O", "propanone (acetone)", "CH₃–CO–CH₃"),
    (("HCOOH", "AXITFOMIC", "FORMICACID"), "O=CO", "methanoic acid (formic acid)", "HCOOH"),
    (("CH3COOH", "AXITAXETIC", "ACETICACID", "ETHANOICACID"), "CC(=O)O", "ethanoic acid (acetic acid)", "CH₃COOH"),
    (("CH3COOC2H5", "ETYLAXETAT", "ETHYLACETATE"), "CCOC(C)=O", "ethyl acetate", "CH₃COOC₂H₅"),
    (("C6H6", "BENZEN", "BENZENE"), "c1ccccc1", "benzene", "C₆H₆"),
    (("C6H5NO2", "NITROBENZEN", "NITROBENZENE"), "O=[N+]([O-])c1ccccc1", "nitrobenzene", "C₆H₅NO₂"),
    (("C6H5NH2", "ANILIN", "ANILINE"), "Nc1ccccc1", "aniline", "C₆H₅NH₂"),
    (("C6H5BR", "BROMBENZEN", "BROMOBENZENE"), "Brc1ccccc1", "bromobenzene", "C₆H₅Br"),
]
_TRA_CHAT = {k: (s, t, c) for ks, s, t, c in _CHAT for k in ks}

# (SMILES đầu, SMILES cuối) chuẩn RDKit → (phản ứng, điều kiện, loại, mã cơ chế demo, mức khớp)
#   khớp "dung_mau": đúng phản ứng của bài demo; "cung_loai": cùng kiểu cơ chế, khác chất (thay chất vào mẫu)
_BUOC = {
    ("C=C", "CCO"): ("cộng H₂O (hydrat hoá)", "H₂O, xt H₂SO₄ loãng hoặc H₃PO₄, t°", "AE", "cong_hbr_propen", "cung_loai"),
    ("CCO", "CC=O"): ("oxi hoá không hoàn toàn bằng CuO", "CuO, t°", "OXI_HOA", None, None),
    ("CC=O", "CC(=O)O"): ("oxi hoá aldehyde thành acid", "O₂, xt Mn²⁺, t°", "OXI_HOA", None, None),
    ("CC(=O)O", "CCOC(C)=O"): ("ester hoá với ethanol", "C₂H₅OH, H₂SO₄ đặc, t° (thuận nghịch)", "THE_ACYL", "este_hoa_fischer", "dung_mau"),
    ("C", "CCl"): ("thế Cl (gốc tự do)", "Cl₂, ánh sáng (tỉ lệ 1:1)", "GOC_TU_DO", "clo_hoa_metan", "dung_mau"),
    ("CCl", "CO"): ("thế Cl bằng OH (SN2)", "NaOH loãng, t°", "SN2", "sn2_ch3br", "cung_loai"),
    ("CBr", "CO"): ("thế Br bằng OH (SN2)", "NaOH loãng, t°", "SN2", "sn2_ch3br", "dung_mau"),
    ("CO", "C=O"): ("oxi hoá không hoàn toàn bằng CuO", "CuO, t°", "OXI_HOA", None, None),
    ("C=CC", "CC(C)Br"): ("cộng HBr (Markovnikov)", "HBr", "AE", "cong_hbr_propen", "dung_mau"),
    ("CC(C)Br", "CC(C)O"): ("thế Br bằng OH", "NaOH loãng, t°", "SN", "sn2_ch3br", "cung_loai"),
    ("CC(C)Br", "C=CC"): ("tách HBr", "KOH/C₂H₅OH, t°", "E2", "e2_2_brombutan", "cung_loai"),
    ("CC(C)O", "CC(C)=O"): ("oxi hoá không hoàn toàn bằng CuO", "CuO, t°", "OXI_HOA", None, None),
    ("CCO", "C=C"): ("tách nước", "H₂SO₄ đặc, 170 °C", "TACH", None, None),
    ("C#C", "C=C"): ("cộng H₂ một nấc", "H₂, xt Pd/PbCO₃, t°", "CONG_H2", None, None),
    ("C=C", "CC"): ("cộng H₂", "H₂, xt Ni, t°", "CONG_H2", None, None),
    ("C=C", "CCBr"): ("cộng HBr", "HBr", "AE", "cong_hbr_propen", "cung_loai"),
    ("CCBr", "CCO"): ("thế Br bằng OH (SN2)", "NaOH loãng, t°", "SN2", "sn2_ch3br", "cung_loai"),
    ("CCBr", "C=C"): ("tách HBr", "KOH/C₂H₅OH, t°", "E2", "e2_2_brombutan", "cung_loai"),
    ("c1ccccc1", "O=[N+]([O-])c1ccccc1"): ("nitro hoá", "HNO₃ đặc, H₂SO₄ đặc, t°", "SEAR", "nitro_hoa_benzen", "dung_mau"),
    ("O=[N+]([O-])c1ccccc1", "Nc1ccccc1"): ("khử nhóm nitro", "Fe/HCl (hoặc Zn/HCl), t°", "KHU", None, None),
    ("c1ccccc1", "Brc1ccccc1"): ("thế Br vào vòng", "Br₂, xt Fe (FeBr₃), t°", "SEAR", "nitro_hoa_benzen", "cung_loai"),
    ("CC", "CCCl"): ("thế Cl (gốc tự do)", "Cl₂, ánh sáng", "GOC_TU_DO", "clo_hoa_metan", "cung_loai"),
}


def chuan(smiles: str) -> str:
    m = Chem.MolFromSmiles(smiles)
    return Chem.MolToSmiles(m) if m is not None else ""


_BUOC_CHUAN = {(chuan(a), chuan(b)): v for (a, b), v in _BUOC.items()}


def _khoa(ki_hieu: str) -> str:
    s = unicodedata.normalize("NFD", ki_hieu)
    s = "".join(c for c in s if unicodedata.category(c) != "Mn").replace("đ", "d").replace("Đ", "D")
    s = s.translate(str.maketrans("₀₁₂₃₄₅₆₇₈₉", "0123456789"))
    return re.sub(r"[\s\-–—=≡]", "", s).upper()


def tach_chuoi(text: str) -> list[str]:
    """'A -> B → C' (cũng nhận =>, ⟶, ->) → ['A', 'B', 'C']."""
    phan = re.split(r"\s*(?:-{1,2}>|→|⟶|=>|⇒)\s*", (text or "").strip())
    return [p.strip() for p in phan if p.strip()]


def nhan_chat(ki_hieu: str) -> dict:
    """Ký hiệu → chất. Có trong bảng: đã kiểm chứng. Không có nhưng là SMILES hợp lệ: nhận SMILES. Còn lại: chưa biết."""
    t = _TRA_CHAT.get(_khoa(ki_hieu))
    if t:
        s, ten, ctct = t
        m = Chem.MolFromSmiles(s)
        return {"ki_hieu": ki_hieu, "smiles": chuan(s), "ten": ten, "ctct": ctct, "cong_thuc": CalcMolFormula(m), "kiem_chung": "da_kiem_chung"}
    m = Chem.MolFromSmiles(ki_hieu) if re.fullmatch(r"[A-Za-z0-9@+\-\[\]()=#/\\%.]+", ki_hieu) else None
    if m is not None and not re.fullmatch(r"(?:[A-Z][a-z]?\d*)+", ki_hieu):
        return {"ki_hieu": ki_hieu, "smiles": Chem.MolToSmiles(m), "ten": "", "ctct": "", "cong_thuc": CalcMolFormula(m),
                "kiem_chung": "smiles_nguoi_dung"}
    return {"ki_hieu": ki_hieu, "smiles": "", "ten": "", "ctct": "", "cong_thuc": "", "kiem_chung": "chua_kiem_chung"}


_NGUYEN_TO = {Chem.GetPeriodicTable().GetElementSymbol(z) for z in range(1, 119)}


def _cong_thuc_de_cho(ki_hieu: str) -> str:
    """Ký hiệu dạng công thức phân tử thuần (C2H6O) → chuẩn Hill để so; ký hiệu khác → ''."""
    k = _khoa(ki_hieu)
    if not re.fullmatch(r"(?:[A-Z][a-z]?\d*)+", ki_hieu.replace(" ", "")):
        return ""
    dem: dict[str, int] = {}
    for el, n in re.findall(r"([A-Z][a-z]?)(\d*)", ki_hieu.replace(" ", "")):
        if el not in _NGUYEN_TO:
            return ""                                           # "X", "A1"… là ký hiệu chất ẩn, không phải công thức
        dem[el] = dem.get(el, 0) + int(n or 1)
    thu_tu = (["C", "H"] if "C" in dem else []) + sorted(e for e in dem if e not in ("C", "H") or "C" not in dem)
    return "".join(f"{e}{dem[e] if dem[e] > 1 else ''}" for e in thu_tu if e in dem) if k else ""


def buoc_tu_bang(a: dict, b: dict) -> dict | None:
    v = _BUOC_CHUAN.get((a["smiles"], b["smiles"]))
    if not v:
        return None
    pu, dk, loai, ma, khop = v
    return {"phan_ung": pu, "dieu_kien": dk, "loai": loai, "ma_co_che": ma, "khop_mau": khop, "kiem_chung": "da_kiem_chung"}


SYSTEM_GIAI = """Bạn là giáo viên Hoá hữu cơ THPT Việt Nam (chương trình GDPT 2018). Cho chuỗi phản ứng, CHỈ trả về MỘT JSON gọn:
{"chuoi":[{"ki_hieu","ten","smiles"}], "buoc":[{"tu","den","phan_ung","dieu_kien"}]}
- chuoi: mọi chất theo thứ tự, ki_hieu giữ đúng như đề; ten theo IUPAC/SGK 2018; smiles hợp lệ với RDKit, đúng công thức đề cho.
- buoc: mỗi mũi tên một phần tử; phan_ung thật ngắn ("cộng HBr (Markovnikov)", "oxi hoá bằng CuO"); dieu_kien ngắn ("CuO, t°").
- Chất đã cho sẵn trong ĐÃ BIẾT thì giữ nguyên. Không viết văn xuôi, không viết phương trình."""


_CHUOI = {"type": "string"}
SCHEMA_GIAI = {
    "type": "object",
    "properties": {
        "chuoi": {"type": "array", "items": {"type": "object", "properties": {"ki_hieu": _CHUOI, "ten": _CHUOI, "smiles": _CHUOI},
                                             "required": ["ki_hieu", "ten", "smiles"], "additionalProperties": False}},
        "buoc": {"type": "array", "items": {"type": "object", "properties": {"tu": _CHUOI, "den": _CHUOI, "phan_ung": _CHUOI,
                                                                             "dieu_kien": _CHUOI},
                                            "required": ["tu", "den", "phan_ung", "dieu_kien"], "additionalProperties": False}},
    },
    "required": ["chuoi", "buoc"],
    "additionalProperties": False,
}


async def _nho_ai(chat: list[dict], chuoi: str) -> dict | None:
    from . import giai_de
    from .giai_thich import goi_tai_may
    da_biet = [{"ki_hieu": c["ki_hieu"], "smiles": c["smiles"]} for c in chat if c["smiles"]]
    user = f"CHUỖI: {chuoi}\nĐÃ BIẾT: {json.dumps(da_biet, ensure_ascii=False)}"
    if giai_de.khoa_rieng.get() is not None:        # khóa AI riêng app gửi kèm → dùng trước; hỏng thì như cũ (tại máy → máy chủ)
        try:
            return giai_de.doc_json(await giai_de.goi_ai_rieng(SYSTEM_GIAI, [{"role": "user", "content": user}], SCHEMA_GIAI))
        except (giai_de.LoiGiaiDe, ValueError):
            pass
    try:
        text, _ = await goi_tai_may(SYSTEM_GIAI, user, 600)
    except Exception:  # noqa: BLE001
        from server.ai import providers
        ch = providers.pick()
        if ch is None:
            return None
        try:
            text = await ch.text(SYSTEM_GIAI, user, 600)
        except Exception:  # noqa: BLE001
            return None
    m = re.search(r"\{[\s\S]*\}", text)
    try:
        return json.loads(m.group(0)) if m else None
    except ValueError:
        return None


async def giai(chuoi: str, cho_phep_ai: bool = True) -> dict:
    ki = tach_chuoi(chuoi)
    if len(ki) < 2:
        return {"chuoi": chuoi, "chat": [], "buoc": [], "loi": "Chuỗi cần ít nhất hai chất, nối bằng -> hoặc →"}
    chat = [nhan_chat(k) for k in ki]
    ai_dung = False
    if cho_phep_ai and any(not c["smiles"] for c in chat) or cho_phep_ai and any(
            buoc_tu_bang(chat[i], chat[i + 1]) is None for i in range(len(chat) - 1)):
        kq = await _nho_ai(chat, chuoi)
        if kq:
            ai_dung = True
            theo_ki = {str(x.get("ki_hieu", "")).strip(): x for x in kq.get("chuoi", []) if isinstance(x, dict)}
            for c in chat:
                x = theo_ki.get(c["ki_hieu"])
                if c["smiles"] or not x:
                    continue
                m = Chem.MolFromSmiles(str(x.get("smiles", "")))
                if m is None:
                    continue
                ct = _cong_thuc_de_cho(c["ki_hieu"])
                if ct and CalcMolFormula(m) != ct:
                    continue                                   # AI sai công thức đề cho: bỏ
                c.update({"smiles": Chem.MolToSmiles(m), "ten": str(x.get("ten", "")), "cong_thuc": CalcMolFormula(m),
                          "kiem_chung": "chua_kiem_chung"})
            ai_buoc = [x for x in kq.get("buoc", []) if isinstance(x, dict)]
        else:
            ai_buoc = []
    else:
        ai_buoc = []
    buoc = []
    for i in range(len(chat) - 1):
        a, b = chat[i], chat[i + 1]
        r = buoc_tu_bang(a, b) if a["smiles"] and b["smiles"] else None
        if r is None:
            x = next((y for y in ai_buoc if str(y.get("tu")) == a["ki_hieu"] and str(y.get("den")) == b["ki_hieu"]), None)
            if x is None and i < len(ai_buoc):
                x = ai_buoc[i]
            r = {"phan_ung": str((x or {}).get("phan_ung", "")) or "chưa xác định", "dieu_kien": str((x or {}).get("dieu_kien", "")),
                 "loai": "", "ma_co_che": None, "khop_mau": None, "kiem_chung": "chua_kiem_chung"}
        if a["kiem_chung"] != "da_kiem_chung" or b["kiem_chung"] != "da_kiem_chung":
            r["kiem_chung"] = "chua_kiem_chung"                  # chất do AI/người dùng đưa: bước cũng chưa kiểm chứng
        r.update({"tu": a["ki_hieu"], "den": b["ki_hieu"]})
        if r["ma_co_che"]:
            r["goi"] = f"/co-che/bai/{r['ma_co_che']}"
        buoc.append(r)
    return {"chuoi": chuoi, "chat": chat, "buoc": buoc, "ai": ai_dung,
            "da_kiem_chung": all(c["kiem_chung"] == "da_kiem_chung" for c in chat) and all(b["kiem_chung"] == "da_kiem_chung" for b in buoc)}
