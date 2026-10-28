"""Schemas dữ liệu cho tính năng Tìm Cấu Tạo Chất."""
from __future__ import annotations

from typing import Any, Dict, List, Optional
from pydantic import BaseModel, Field


class DuKienTrichXuat(BaseModel):
    id: str = Field(..., description="Mã định danh dữ kiện, ví dụ: DK1, DK2...")
    noi_dung_goc: str = Field(..., description="Trích nguyên văn đoạn câu trong đề bài")
    loai: str = Field(..., description="'dinh_luong' (có số liệu) hoặc 'dinh_tinh' (tính chất, phản ứng, màu sắc...)")
    chat_lien_quan: List[str] = Field(default_factory=list, description="Kí hiệu các chất liên quan: X, Y, Z, E, T...")
    so_lieu: Dict[str, Any] = Field(default_factory=dict, description="Các số liệu trích xuất được (nếu có): m, V, %, tỉ lệ...")


class BangDuKienResponse(BaseModel):
    chat: List[str] = Field(default_factory=list, description="Danh sách các chất ẩn / chất cần tìm trong đề")
    du_kien: List[DuKienTrichXuat] = Field(default_factory=list, description="Danh sách các dữ kiện đã đánh số")
    cau_hoi: str = Field("", description="Yêu cầu / câu hỏi cụ thể của đề bài")


class KiemTraChiTiet(BaseModel):
    id_du_kien: str = Field(..., description="ID dữ kiện tương ứng (DK1, DK2...)")
    thoa_man: bool = Field(..., description="True (✓) nếu khớp, False (✗) nếu vi phạm")
    giai_thich: str = Field("", description="Lý do chi tiết vì sao thoả mãn hoặc vi phạm dữ kiện này")


class UngVienChat(BaseModel):
    ten_ung_vien: str = Field(..., description="Tên định danh ứng viên, ví dụ 'Ứng viên 1', 'Đồng phân A'...")
    cau_truc: Dict[str, str] = Field(..., description="CTCT thu gọn của các chất, ví dụ: {'X': 'CH3COOC2H5'}")
    mo_ta: str = Field("", description="Mô tả cấu trúc và đặc điểm")
    kiem_tra: List[KiemTraChiTiet] = Field(default_factory=list, description="Kết quả kiểm tra với từng dữ kiện DK1, DK2...")
    dat_chuan: bool = Field(True, description="True nếu đạt ✓ toàn bộ 100% dữ kiện")
    ly_do_loai: str = Field("", description="Nếu không đạt, ghi rõ lý do loại")


class BuocSuyLuan(BaseModel):
    buoc: int = Field(..., description="Số thứ tự bước (1, 2, 3...)")
    tieu_de: str = Field(..., description="Tiêu đề bước (vd: Tính Delta & Định danh nhóm chức)")
    noi_dung: str = Field(..., description="Nội dung suy luận bằng ký hiệu ->, không văn mẫu")
    can_cu_du_kien: List[str] = Field(default_factory=list, description="Các ID dữ kiện làm căn cứ (DK1, DK2...)")


class KetQuaTimChat(BaseModel):
    de_bai: str = Field(..., description="Đề bài gốc")
    trac_nghiem: Optional[str] = Field(None, description="Các phương án trắc nghiệm A/B/C/D nếu có")
    cau_hoi: Optional[str] = Field(None, description="Câu hỏi trọng tâm")

    # 4 Phần hiển thị chính
    bang_du_kien: List[DuKienTrichXuat] = Field(default_factory=list, description="Phần 1: Bảng dữ kiện trích nguyên văn")
    tinh_toan_code: Dict[str, Any] = Field(default_factory=dict, description="Số liệu tính toán từ module code")
    cac_buoc_suy_luan: List[BuocSuyLuan] = Field(default_factory=list, description="Phần 2: Các bước suy luận 4 bước ->")
    ung_vien: List[UngVienChat] = Field(default_factory=list, description="Các ứng viên cấu tạo đã sinh ra")
    bang_kiem_tra: List[Dict[str, Any]] = Field(default_factory=list, description="Phần 3: Bảng ma trận kiểm tra chéo")

    # Phần 4: Đáp án cuối
    dap_an: Dict[str, str] = Field(default_factory=dict, description="CTCT thu gọn của từng chất X, Y, Z...")
    dap_an_trac_nghiem: Optional[str] = Field(None, description="Phương án đúng A/B/C/D (nếu có)")
    ket_luan: str = Field("", description="Lời giải tóm tắt ngắn gọn cuối cùng")

    # Mức độ tin cậy và Cảnh báo
    do_tin_cay: str = Field("cao", description="'cao' | 'trung_binh' | 'thap'")
    canh_bao: List[str] = Field(default_factory=list, description="Cảnh báo dữ kiện chưa dùng hoặc mâu thuẫn nếu có")
    mo_hinh_giai: str = Field("", description="Tên mô hình AI đã tham gia giải")
    doi_chieu: Optional[Dict[str, Any]] = Field(None, description="Thông tin đối chiếu 2 model nếu có bất đồng")
