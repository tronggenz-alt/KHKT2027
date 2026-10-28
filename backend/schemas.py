"""Mô hình dữ liệu (pydantic) và JSON schema gửi cho Claude (structured outputs)."""
from __future__ import annotations

from pydantic import BaseModel, Field


# ---------- Bước 1: phân tích đề ----------
class ChatTrongDe(BaseModel):
    ki_hieu: str
    la_chat_an: bool = False
    ten_goi_y: str = ""
    ten_tieng_anh: str = ""
    ctpt_de_cho: str = ""  # công thức phân tử đề cho kèm chất ẩn, vd "C3H6O" trong "C (C3H6O)"


class BuocTrongDe(BaseModel):
    so: int
    chat_dau: str
    chat_sau: str
    dieu_kien_de_cho: str = ""  # tác nhân + điều kiện đề ghi trên mũi tên, vd "HBr", "NaOH, t°"


class PhanTichDe(BaseModel):
    de_bai: str
    cac_chat: list[ChatTrongDe]
    cac_buoc: list[BuocTrongDe]
    ghi_chu: str = ""


# ---------- Bước 3: lời giải ----------
class ChatPhanUng(BaseModel):
    ten: str
    smiles: str
    he_so: float = 1


class Buoc(BaseModel):
    so: int
    chat_dau: str = ""
    chat_sau: str = ""
    phuong_trinh: str = ""          # kết quả dạng CHUỖI (giai_nhanh.py) không có phương trình – chỉ sinh khi bấm "Xem phương trình chi tiết"
    chat_tham_gia: list[ChatPhanUng] = Field(default_factory=list)
    san_pham: list[ChatPhanUng] = Field(default_factory=list)
    dieu_kien: str = ""
    loai_phan_ung: str = ""
    giai_thich: str = ""
    # Do validator điền, không yêu cầu Claude trả về
    can_kiem_tra: bool = False
    loi: list[str] = Field(default_factory=list)


class NutSoDo(BaseModel):
    ki_hieu: str
    cong_thuc: str = ""
    ten: str = ""
    smiles: str = ""
    ctpt: str = ""  # do validator điền (RDKit, kiểu Hill)


class ChatAn(BaseModel):
    ki_hieu: str
    ten: str = ""
    smiles: str = ""
    cong_thuc: str = ""
    ctpt: str = ""  # do validator điền (RDKit, kiểu Hill)


class LoiGiai(BaseModel):
    so_do: list[NutSoDo] = Field(default_factory=list)
    cac_buoc: list[Buoc]
    chat_an: list[ChatAn] = Field(default_factory=list)
    nguon_tham_khao: list[str] = Field(default_factory=list)
    ghi_chu: str = ""
    luu_y: list[str] = Field(default_factory=list)   # kết quả dạng chuỗi: "Lưu ý" ngắn (≤ ~5 ý)
    dap_an: str = ""                                 # kết quả dạng chuỗi: đáp án gọn


# ---------- Kết quả dạng CHUỖI (gộp phân tích + tra cứu + giải trong một lần gọi AI – giai_nhanh.py) ----------
class ChatChuoi(BaseModel):
    ki_hieu: str
    ctct: str = ""      # công thức cấu tạo thu gọn
    ten: str = ""
    smiles: str = ""


class BuocChuoi(BaseModel):
    tu: str
    den: str
    phan_ung: str = ""   # tên / loại phản ứng, vd "cộng HBr (Markovnikov)"
    dieu_kien: str = ""
    can_cu: str = ""     # có tra cứu tài liệu: "[1]" (lấy từ bài 1) | "suy luận" (AI tự suy luận)


class LoiGiaiGon(BaseModel):
    chuoi: list[ChatChuoi]
    buoc: list[BuocChuoi]
    luu_y: list[str] = Field(default_factory=list)
    dap_an: str = ""
    chua_duoc_day: list[str] = Field(default_factory=list)  # AI Riêng (nghiêm ngặt): bước kho kiến thức chưa dạy
    mau_thuan: list[str] = Field(default_factory=list)      # AI Riêng: chỗ kho mâu thuẫn hiểu biết chung (đã theo kho)


# ---------- JSON schema cho structured outputs ----------
def _obj(props: dict, required: list[str] | None = None) -> dict:
    return {
        "type": "object",
        "properties": props,
        "required": required if required is not None else list(props),
        "additionalProperties": False,
    }


_S = {"type": "string"}

SCHEMA_PHAN_TICH = _obj(
    {
        "de_bai": _S,
        "cac_chat": {
            "type": "array",
            "items": _obj(
                {
                    "ki_hieu": _S,
                    "la_chat_an": {"type": "boolean"},
                    "ten_goi_y": _S,
                    "ten_tieng_anh": _S,
                    "ctpt_de_cho": _S,
                }
            ),
        },
        "cac_buoc": {
            "type": "array",
            "items": _obj(
                {
                    "so": {"type": "integer"},
                    "chat_dau": _S,
                    "chat_sau": _S,
                    "dieu_kien_de_cho": _S,
                }
            ),
        },
        "ghi_chu": _S,
    }
)

_CHAT = _obj({"ten": _S, "smiles": _S, "he_so": {"type": "number"}})

SCHEMA_LOI_GIAI = _obj(
    {
        "so_do": {
            "type": "array",
            "items": _obj({"ki_hieu": _S, "cong_thuc": _S, "ten": _S, "smiles": _S}),
        },
        "cac_buoc": {
            "type": "array",
            "items": _obj(
                {
                    "so": {"type": "integer"},
                    "chat_dau": _S,
                    "chat_sau": _S,
                    "phuong_trinh": _S,
                    "chat_tham_gia": {"type": "array", "items": _CHAT},
                    "san_pham": {"type": "array", "items": _CHAT},
                    "dieu_kien": _S,
                    "loai_phan_ung": _S,
                    "giai_thich": _S,
                }
            ),
        },
        "chat_an": {
            "type": "array",
            "items": _obj({"ki_hieu": _S, "ten": _S, "smiles": _S, "cong_thuc": _S}),
        },
        "nguon_tham_khao": {"type": "array", "items": _S},
        "ghi_chu": _S,
    }
)
