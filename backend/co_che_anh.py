"""Mục 05 "Cơ chế" – NHẬP ĐỀ BẰNG HÌNH ẢNH: đọc 1–4 ảnh đề bằng AI có thị giác → JSON (đề, các phản ứng dạng SMILES, cấu trúc
trong ảnh, mức độ, chỗ không chắc) → RDKit kiểm từng SMILES (sai → AI đọc lại, tối đa 2 lần) → người dùng XÁC NHẬN / sửa rồi mới vẽ
cơ chế bằng đúng luồng đang có (/api/co-che → co_che.tao_co_che → kiểm tra → ve_co_che).

- Mô hình theo quy tắc chung của app (kế hoạch AI của yêu cầu): mặc định Gemini Flash; AI thấy đề mức HSG → đọc lại bằng Gemini Pro
  (người có mã Claude + bật "Dùng Claude": Claude); đã mở Claude bằng Key Claude / admin + bật "Dùng Claude" → Claude đọc ngay.
  Claude lỗi → PhienHoiThoai tự chuyển Gemini và báo. Mọi lần gọi được ghi chi phí (ai_usage) như các lần gọi AI khác.
- Ảnh chỉ nằm trong bộ nhớ của yêu cầu, KHÔNG lưu xuống đĩa (HEIC: đổi sang JPG trong thư mục tạm, xoá ngay sau khi đổi).
"""
from __future__ import annotations

import io
import logging
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path
from typing import Callable

from PIL import Image, ImageStat, UnidentifiedImageError
from pydantic import BaseModel, Field, ValidationError
from rdkit import Chem, RDLogger

from . import do_kho
from .co_che import LOAI_CO_CHE
from .llm import LoiAI, PhienHoiThoai, doc_json, ke_hoach_hien_tai, mo_ta_mo_hinh, ten_ai
from .mo_hinh_3d import _anh_2d, nhan_dien
from .parser import chuan_bi_anh

log = logging.getLogger(__name__)
RDLogger.DisableLog("rdApp.*")

SO_ANH_TOI_DA = 4
ANH_TOI_DA = 10 * 1024 * 1024    # mỗi ảnh (trình duyệt đã nén về ~1600 px nên thường < 1 MB)
CANH_NHO_NHAT = 200              # px – nhỏ hơn thì không đủ nét để đọc công thức
DO_SANG_TOI_THIEU = 28           # độ sáng trung bình (0–255) – thấp hơn = ảnh quá tối
SO_LAN_DOC_LAI = 2               # SMILES sai → gửi lỗi cho AI đọc lại tối đa 2 lần
DINH_DANG = {"JPEG": "JPG", "MPO": "JPG", "PNG": "PNG", "WEBP": "WEBP", "HEIF": "HEIC"}
_HEIC = (b"heic", b"heix", b"hevc", b"hevx", b"heim", b"heis", b"mif1", b"msf1", b"heif")
VAN_DE_ANH = ("", "khong_phai_de_hoa", "mo", "toi", "bi_cat", "khac")


class LoiAnh(LoiAI):
    """Lỗi do ảnh (định dạng, kích thước, mờ, không phải đề hoá) – báo người dùng bằng tiếng Việt, mã HTTP riêng."""

    def __init__(self, thong_diep: str, ma: str = "anh_khong_hop_le", status: int = 400):
        super().__init__(thong_diep, "anh")
        self.ma, self.status = ma, status


SYSTEM_DOC_ANH = """Bạn đọc ẢNH ĐỀ Hoá hữu cơ (chương trình THPT Việt Nam, kể cả đề HSG tỉnh/quốc gia) để app vẽ CƠ CHẾ phản ứng. KHÔNG giải đề.
Trả về DUY NHẤT một JSON đúng schema:
- de_bai: chép lại TOÀN VĂN đề (chữ in và chữ viết tay), giữ đánh số câu; công thức viết dạng chữ (CH3CH2Br, C6H5-CH=CH2…).
- phan_ung: mỗi phản ứng / câu hỏi cần vẽ cơ chế là một phần tử:
  so_cau ("Câu 1", "2a"…, không đánh số thì "1", "2"… theo thứ tự); chat_dau: SMILES các chất phản ứng VÀ tác nhân dạng tiểu phân thật
  sự phản ứng (NaOH → [OH-]; NaOCH3 → C[O-]; HBr → Br; Br2 → BrBr; H2O → O); tac_nhan_dieu_kien: chữ ghi trên/dưới mũi tên (xúc tác,
  dung môi, nhiệt độ, as, H+…); san_pham: SMILES sản phẩm đề đã cho – sản phẩm đề BẮT TÌM (chưa biết, X, Y…) ghi chuỗi rỗng "";
  yeu_cau: đề yêu cầu gì ("vẽ cơ chế", "giải thích sản phẩm chính", "so sánh tốc độ"…);
  goi_y_loai: mã loại cơ chế ĐÚNG về hoá học trong danh sách {DS_LOAI} (vd dẫn xuất bậc ba + H2O → SN1; đề hỏi "SN1 hay SN2" thì vẫn
  chọn loại đúng); không thuộc danh sách / không chắc → "".
- cau_truc_trong_anh: MỌI công thức cấu tạo được VẼ trong ảnh: vi_tri ("ảnh 1, chất A", "hình bên trái câu 2"…) + smiles.
- muc_do: "de" (phản ứng SGK cơ bản) | "trung_binh" (SN1/SN2/E1/E2, cộng electrophin, este hoá… cần biện luận) | "hsg" (cơ chế nhiều bước,
  chuyển vị, lập thể phức tạp, phản ứng có tên nâng cao, pericyclic, gốc tự do…). Phân vân thì chọn mức CAO hơn.
- khong_chac_chan: những chỗ đọc KHÔNG rõ – mỗi mục ghi rõ câu nào, chất nào, KÈM SMILES đã đoán và lý do (nét mờ, bị che, viết tay khó đọc…).
- chat_luong_anh: doc_duoc (đọc được nội dung hoá học không); van_de: "" | "khong_phai_de_hoa" (ảnh phong cảnh, người, đề môn khác…) | "mo" |
  "toi" | "bi_cat" | "khac"; mo_ta: MỘT câu tiếng Việt cụ thể cho người dùng, vd "Ảnh hơi mờ, không đọc được công thức ở câu 2 — hãy chụp
  gần và rõ hơn." (để trống nếu ảnh tốt).
Đọc công thức cấu tạo: dạng khung (mỗi đỉnh/đầu mút là C, đủ H), thu gọn, nêm đậm/nêm gạch (ghi lập thể @/@@), liên kết đôi E/Z (/ \\),
Newman, ghế, Fischer, Haworth (chuyển thành SMILES có lập thể đúng); điện tích, cặp e, ion; mũi tên phản ứng (→, ⇌) và điều kiện trên mũi tên.
SMILES phải hợp lệ với RDKit (đúng hoá trị, ion có ngoặc vuông: [Na+], [OH-], [NH4+]). Không bịa chất không có trong ảnh."""
SYSTEM_DOC_ANH = SYSTEM_DOC_ANH.replace("{DS_LOAI}", "; ".join(f"{k} = {v}" for k, v in LOAI_CO_CHE.items()))

_CHAT = {"type": "array", "items": {"type": "string"}}
SCHEMA_DOC_ANH = {
    "type": "object",
    "properties": {
        "de_bai": {"type": "string"},
        "phan_ung": {"type": "array", "items": {
            "type": "object",
            "properties": {"so_cau": {"type": "string"}, "chat_dau": _CHAT, "tac_nhan_dieu_kien": {"type": "string"}, "san_pham": _CHAT,
                           "yeu_cau": {"type": "string"}, "goi_y_loai": {"type": "string", "enum": ["", *LOAI_CO_CHE]}},
            "required": ["so_cau", "chat_dau", "tac_nhan_dieu_kien", "san_pham", "yeu_cau", "goi_y_loai"], "additionalProperties": False}},
        "cau_truc_trong_anh": {"type": "array", "items": {
            "type": "object", "properties": {"vi_tri": {"type": "string"}, "smiles": {"type": "string"}},
            "required": ["vi_tri", "smiles"], "additionalProperties": False}},
        "muc_do": {"type": "string", "enum": list(do_kho.MUC_DO)},
        "khong_chac_chan": {"type": "array", "items": {"type": "string"}},
        "chat_luong_anh": {"type": "object", "properties": {
            "doc_duoc": {"type": "boolean"}, "van_de": {"type": "string", "enum": list(VAN_DE_ANH)}, "mo_ta": {"type": "string"}},
            "required": ["doc_duoc", "van_de", "mo_ta"], "additionalProperties": False},
    },
    "required": ["de_bai", "phan_ung", "cau_truc_trong_anh", "muc_do", "khong_chac_chan", "chat_luong_anh"],
    "additionalProperties": False,
}


class _PhanUng(BaseModel):
    so_cau: str = ""
    chat_dau: list[str | None] = Field(default_factory=list)
    tac_nhan_dieu_kien: str = ""
    san_pham: list[str | None] = Field(default_factory=list)
    yeu_cau: str = ""
    goi_y_loai: str = ""   # gợi ý loại cơ chế (mã của co_che.LOAI_CO_CHE) – người dùng xác nhận / đổi ở bước xác nhận


class _CauTruc(BaseModel):
    vi_tri: str = ""
    smiles: str | None = ""


class _ChatLuong(BaseModel):
    doc_duoc: bool = True
    van_de: str = ""
    mo_ta: str = ""


class DocAnh(BaseModel):
    de_bai: str = ""
    phan_ung: list[_PhanUng] = Field(default_factory=list)
    cau_truc_trong_anh: list[_CauTruc] = Field(default_factory=list)
    muc_do: str = "trung_binh"
    khong_chac_chan: list[str] = Field(default_factory=list)
    chat_luong_anh: _ChatLuong = Field(default_factory=_ChatLuong)


# ---------------------------------------------------------------- ảnh vào
def _la_heic(data: bytes) -> bool:
    return data[4:8] == b"ftyp" and data[8:12] in _HEIC


def _heic_sang_jpg(data: bytes) -> bytes:
    """HEIC (iPhone) → JPG: pillow-heif nếu đã cài, không thì `sips` có sẵn trên macOS (thư mục tạm, xoá ngay)."""
    try:
        import pillow_heif  # type: ignore[import-not-found]

        buf = io.BytesIO()
        pillow_heif.open_heif(data).to_pillow().convert("RGB").save(buf, "JPEG", quality=92)
        return buf.getvalue()
    except ImportError:
        pass
    except Exception as e:  # noqa: BLE001
        raise LoiAnh("Không chuyển được ảnh HEIC sang JPG – hãy chọn ảnh JPG/PNG.", "heic_loi") from e
    if sys.platform != "darwin" or not shutil.which("sips"):
        raise LoiAnh("Máy chủ chưa đọc được ảnh HEIC – hãy chọn ảnh JPG/PNG (iPhone: Cài đặt → Camera → Định dạng → Tương thích nhất).",
                     "heic_loi")
    with tempfile.TemporaryDirectory(prefix="co_che_anh_") as d:
        vao, ra = Path(d) / "anh.heic", Path(d) / "anh.jpg"
        vao.write_bytes(data)
        try:
            subprocess.run(["sips", "-s", "format", "jpeg", str(vao), "--out", str(ra)], capture_output=True, timeout=60, check=True)
            return ra.read_bytes()
        except (subprocess.SubprocessError, OSError) as e:
            raise LoiAnh("Không chuyển được ảnh HEIC sang JPG – hãy chọn ảnh JPG/PNG.", "heic_loi") from e


def kiem_tra_anh(data: bytes, ten: str = "", so: int = 1) -> bytes:
    """Kiểm tra một ảnh (dung lượng, định dạng, kích thước, độ sáng); HEIC → JPG. Trả về dữ liệu ảnh đọc được bằng Pillow."""
    nhan = f"Ảnh {so}" + (f" ({ten})" if ten else "")
    if not data:
        raise LoiAnh(f"{nhan} rỗng.", "anh_rong")
    if len(data) > ANH_TOI_DA:
        raise LoiAnh(f"{nhan} quá lớn ({len(data) / 1024 / 1024:.1f} MB) – tối đa 10 MB mỗi ảnh.", "anh_qua_lon", 413)
    if _la_heic(data):
        data = _heic_sang_jpg(data)
    try:
        img = Image.open(io.BytesIO(data))
        img.load()
    except (UnidentifiedImageError, OSError, Image.DecompressionBombError) as e:
        raise LoiAnh(f"{nhan}: không phải ảnh hoặc định dạng không hỗ trợ – chỉ nhận JPG, PNG, WEBP, HEIC.", "sai_dinh_dang", 415) from e
    if img.format not in DINH_DANG:
        raise LoiAnh(f"{nhan}: định dạng {img.format} không hỗ trợ – chỉ nhận JPG, PNG, WEBP, HEIC.", "sai_dinh_dang", 415)
    if min(img.size) < CANH_NHO_NHAT:
        raise LoiAnh(f"{nhan} quá nhỏ ({img.size[0]}×{img.size[1]} px) – không đủ nét để đọc công thức, hãy chụp lại gần hơn.", "anh_qua_nho")
    xam = img.convert("L")
    xam.thumbnail((256, 256))
    if ImageStat.Stat(xam).mean[0] < DO_SANG_TOI_THIEU:
        raise LoiAnh(f"{nhan} quá tối – hãy chụp lại nơi đủ sáng.", "anh_qua_toi", 422)
    return data


# ---------------------------------------------------------------- đọc ảnh bằng AI + RDKit
def _doc_ket_qua(text: str) -> DocAnh:
    try:
        kq = DocAnh.model_validate(doc_json(text))
    except (ValueError, ValidationError) as e:
        raise LoiAI("AI trả kết quả đọc ảnh không đúng định dạng JSON.", "dinh_dang") from e
    if kq.muc_do not in do_kho.MUC_DO:
        kq.muc_do = "trung_binh"
    return kq


def _smiles_sai(kq: DocAnh) -> list[str]:
    """Mọi SMILES khác rỗng mà RDKit không đọc được → danh sách mô tả để gửi AI đọc lại."""
    sai = []
    for p in kq.phan_ung:
        for vai, ds in (("chất đầu", p.chat_dau), ("sản phẩm", p.san_pham)):
            for i, s in enumerate(ds, 1):
                if (s or "").strip() and Chem.MolFromSmiles(s.strip()) is None:
                    sai.append(f"{p.so_cau or '?'} – {vai} {i}: {s.strip()}")
    for c in kq.cau_truc_trong_anh:
        if (c.smiles or "").strip() and Chem.MolFromSmiles(c.smiles.strip()) is None:
            sai.append(f"cấu trúc ở {c.vi_tri or '?'}: {c.smiles.strip()}")
    return sai


def _doc(anh: list[tuple[str, str]], van_ban: str, bac: str, bao: Callable[[str], None]) -> tuple[DocAnh, str]:
    phien = PhienHoiThoai(SYSTEM_DOC_ANH, output_schema=SCHEMA_DOC_ANH, effort="medium", max_tokens=16000, bao=bao, bac=bac)
    yeu_cau = f"Đọc {len(anh)} ảnh đề đính kèm (theo thứ tự ảnh 1 → {len(anh)}) và trả JSON."
    if van_ban.strip():
        yeu_cau += f"\nNgười dùng ghi thêm: {van_ban.strip()[:600]}"
    ph = phien.gui(text=yeu_cau, anh=anh)
    try:
        kq = _doc_ket_qua(ph.text)
    except LoiAI:
        bao("Kết quả đọc ảnh không đúng định dạng → nhờ AI trả lại JSON")
        ph = phien.gui(text="Câu trả lời trước không phải JSON hợp lệ theo schema. Hãy trả lại DUY NHẤT JSON đó.")
        kq = _doc_ket_qua(ph.text)
    for lan in range(1, SO_LAN_DOC_LAI + 1):
        sai = _smiles_sai(kq)
        if not sai:
            break
        bao(f"RDKit không đọc được {len(sai)} SMILES → nhờ AI xem lại ảnh (lần {lan}/{SO_LAN_DOC_LAI})")
        ph = phien.gui(text="RDKit KHÔNG đọc được các SMILES sau (sai hoá trị / cú pháp):\n- " + "\n- ".join(sai)
                       + "\nHãy nhìn lại ảnh, sửa các SMILES đó và trả lại TOÀN BỘ JSON (cùng schema). Chỗ vẫn không chắc thì ghi vào khong_chac_chan.")
        try:
            kq = _doc_ket_qua(ph.text)
        except LoiAI:
            break  # giữ kết quả trước, SMILES sai sẽ bị tô vàng
    return kq, mo_ta_mo_hinh(ph)


def _chat(smiles: str | None, nghi: list[str]) -> dict:
    """Một chất cho bước xác nhận: SMILES (None = đề bắt tìm), hợp lệ không, có trong danh sách không chắc không, hình vẽ RDKit."""
    s = (smiles or "").strip()
    if not s:
        return {"smiles": None, "hop_le": True, "khong_chac": False, "anh": ""}
    hop_le = Chem.MolFromSmiles(s) is not None
    try:
        hinh = _anh_2d(s) if hop_le else ""
    except Exception:  # noqa: BLE001 – vẽ hỏng không làm hỏng cả kết quả
        log.exception("Không vẽ được %s", s)
        hinh = ""
    return {"smiles": s, "hop_le": hop_le, "khong_chac": not hop_le or (len(s) >= 3 and any(s in n for n in nghi)), "anh": hinh}


def doc_anh(ds_anh: list[tuple[bytes, str]], van_ban: str = "", bao: Callable[[str], None] | None = None) -> dict:
    """[(dữ liệu ảnh, tên tệp)] (+ chữ kèm, vd "vẽ cơ chế câu 2") → kết quả đọc cho bước xác nhận. Lỗi ảnh → LoiAnh."""
    bao = bao or (lambda _m: None)
    if not ds_anh:
        raise LoiAnh("Hãy chọn ít nhất một ảnh đề.", "thieu_anh")
    if len(ds_anh) > SO_ANH_TOI_DA:
        raise LoiAnh(f"Tối đa {SO_ANH_TOI_DA} ảnh mỗi lần (đang có {len(ds_anh)}).", "qua_nhieu_anh")
    anh = [chuan_bi_anh(kiem_tra_anh(d, ten, i), "JPEG") for i, (d, ten) in enumerate(ds_anh, 1)]

    kh = ke_hoach_hien_tai()
    co_claude = kh is not None and kh.claude is not None
    moi_muc = co_claude and bool(getattr(kh.claude, "moi_muc", False))
    bac = "claude" if moi_muc else "flash"
    if kh is not None:
        kh.buoc, kh.bac = "doc_anh", bac
    bao(f"Đọc {len(anh)} ảnh bằng {ten_ai(bac)}…")
    kq, mo_hinh = _doc(anh, van_ban, bac, bao)
    dich = do_kho.bac_giai(kq.muc_do, co_claude, moi_muc)
    if do_kho.THU_TU_BAC.index(dich) > do_kho.THU_TU_BAC.index(bac):
        bao(f"Đề mức {do_kho.TEN_MUC[kq.muc_do]} → đọc lại bằng {ten_ai(dich)}")
        if kh is not None:
            kh.bac, kh.nang_cap = dich, True
        kq, mo_hinh = _doc(anh, van_ban, dich, bao)
    if kh is not None:
        kh.muc_do = kq.muc_do

    cl = kq.chat_luong_anh
    co_hoa = bool(kq.phan_ung or kq.cau_truc_trong_anh)
    if cl.van_de == "khong_phai_de_hoa" or not (co_hoa or kq.de_bai.strip()):
        raise LoiAnh(cl.mo_ta or "Ảnh không phải đề Hoá học (không thấy phản ứng hay công thức nào) – hãy chọn đúng ảnh đề.",
                     "anh_khong_lien_quan", 422)
    if not co_hoa:
        raise LoiAnh((cl.mo_ta or "Không đọc được phản ứng hay công thức nào trong ảnh").rstrip(".")
                     + (" — hãy chụp gần và rõ hơn." if cl.van_de in ("mo", "toi", "bi_cat") or not cl.mo_ta else "."),
                     "anh_khong_doc_duoc", 422)

    sai_con_lai = _smiles_sai(kq)
    nghi = list(dict.fromkeys(x.strip() for x in kq.khong_chac_chan if x.strip()))
    nghi += [f"{s} – RDKit không đọc được SMILES này, hãy bấm Sửa." for s in sai_con_lai]
    phan_ung = [{"so_cau": p.so_cau or str(i), "yeu_cau": p.yeu_cau, "tac_nhan_dieu_kien": p.tac_nhan_dieu_kien,
                 "goi_y_loai": p.goi_y_loai if p.goi_y_loai in LOAI_CO_CHE else "",
                 "chat_dau": [_chat(s, nghi) for s in p.chat_dau if (s or "").strip()],
                 "san_pham": [_chat(s, nghi) for s in p.san_pham]}
                for i, p in enumerate(kq.phan_ung, 1)]
    return {
        "de_bai": kq.de_bai, "phan_ung": phan_ung,
        "cau_truc_trong_anh": [{"vi_tri": c.vi_tri, **_chat(c.smiles, nghi)} for c in kq.cau_truc_trong_anh if (c.smiles or "").strip()],
        "muc_do": kq.muc_do, "ten_muc_do": do_kho.TEN_MUC[kq.muc_do], "khong_chac_chan": nghi,
        "canh_bao": [cl.mo_ta] if cl.mo_ta and cl.van_de in ("mo", "toi", "bi_cat", "khac") else [],
        "mo_hinh": mo_hinh, "so_anh": len(anh),
        "json_ai": {**kq.model_dump(exclude={"chat_luong_anh"}),  # JSON đúng mẫu đặc tả (sản phẩm đề bắt tìm = null)
                    "phan_ung": [{**p.model_dump(), "san_pham": [(s or "").strip() or None for s in p.san_pham]} for p in kq.phan_ung]},
    }


def nhan_chat(chuoi: str) -> dict:
    """Nút "Sửa" ở bước xác nhận: SMILES hoặc tên chất (Việt/Anh – PubChem, AI) → {smiles, ten, anh, cach, canh_bao}."""
    t = (chuoi or "").strip()
    if not t:
        raise LoiAnh("Hãy nhập SMILES hoặc tên chất.", "thieu_chat")
    if " " not in t and Chem.MolFromSmiles(t) is not None:
        return {"smiles": t, "ten": "", "anh": _anh_2d(t), "cach": "SMILES", "canh_bao": []}
    nd = nhan_dien(van_ban=t)
    return {"smiles": nd["smiles"], "ten": nd.get("ten", ""), "anh": _anh_2d(nd["smiles"]), "cach": nd.get("cach", ""),
            "canh_bao": nd.get("canh_bao", [])}
