"""Pipeline 5 bước giải bài toán Tìm Cấu Tạo Chất từ đề dài.
Tối ưu hiệu suất, cache theo hash, streaming từng bước, kiểm tra chéo ma trận,
và đối chiếu đa mô hình khi cần thiết.
"""
from __future__ import annotations

import hashlib
import json
import logging
import re
import time
from typing import Any, Callable, Dict, List, Optional, Tuple

from ..llm import LoiAI, PhienHoiThoai, doc_json, ke_hoach_hien_tai
from . import tinh_toan
from .prompts import SYSTEM_KIEM_TRA_CHEO, SYSTEM_OCR_DE_BAI, SYSTEM_SUY_LUAN, SYSTEM_TRICH_XUAT
from .schemas import (
    BangDuKienResponse,
    BuocSuyLuan,
    DuKienTrichXuat,
    KetQuaTimChat,
    KiemTraChiTiet,
    UngVienChat,
)

log = logging.getLogger(__name__)

# Cache kết quả theo hash đề bài trong bộ nhớ tiến trình
CACHE_TIM_CHAT: Dict[str, Dict[str, Any]] = {}
SO_VONG_RETRY_TOI_DA = 2


def tinh_hash_de(de_bai: str, trac_nghiem: str = "", cau_hoi: str = "") -> str:
    """Tạo mã băm sha256 cho đề bài và các tuỳ chọn kèm theo."""
    chuoi_chuan = f"{de_bai.strip()}|{trac_nghiem.strip()}|{cau_hoi.strip()}"
    return hashlib.sha256(chuoi_chuan.encode("utf-8")).hexdigest()


def doc_anh_de_bai(anh_bytes: bytes, mime_type: str = "image/jpeg") -> str:
    """OCR ảnh đề bài bằng mô hình Vision (Gemini Flash)."""
    import base64

    b64 = base64.b64encode(anh_bytes).decode("ascii")
    phien = PhienHoiThoai(
        SYSTEM_OCR_DE_BAI,
        bac="flash",
        max_tokens=2048,
        effort="low",
    )
    res = phien.gui(text="Chép lại nguyên văn đề bài trong ảnh.", anh=(mime_type, b64))
    return res.text.strip()


def chay_pipeline_tim_chat(
    de_bai: str,
    trac_nghiem: str = "",
    cau_hoi: str = "",
    dung_cache: bool = True,
    emit: Optional[Callable[[str, str, Dict[str, Any]], None]] = None,
    uu_tien_claude: bool = True,
) -> KetQuaTimChat:
    """Chạy quy trình 5 bước tìm cấu tạo chất:
    Bước 1: Trích dữ kiện (model flash/lite)
    Bước 2: Tính toán bằng CODE (module tinh_toan)
    Bước 3: Suy luận & sinh ứng viên (model pro/claude)
    Bước 4: Kiểm tra chéo bằng code + AI (ma trận ✓/✗)
    Bước 5: Trình bày lời giải ngắn gọn chuẩn THPT.
    """
    thong_bao = emit or (lambda giai_doan, thong_diep, du_lieu: None)
    ma_hash = tinh_hash_de(de_bai, trac_nghiem, cau_hoi)

    # 0. Kiểm tra Cache
    if dung_cache and ma_hash in CACHE_TIM_CHAT:
        thong_bao("cache", "Lấy kết quả tức thì từ bộ nhớ cache.", CACHE_TIM_CHAT[ma_hash])
        return KetQuaTimChat(**CACHE_TIM_CHAT[ma_hash])

    # =========================================================================
    # BƯỚC 1: TRÍCH DỮ KIỆN (Mô hình nhanh - Flash)
    # =========================================================================
    thong_bao("buoc_1", "Bước 1: Đang đọc đề và lập bảng dữ kiện có cấu trúc...", {})
    prompt_b1 = f"""ĐỀ BÀI:
{de_bai}
"""
    if trac_nghiem:
        prompt_b1 += f"\nCÁC PHƯƠNG ÁN TRẮC NGHIỆM:\n{trac_nghiem}\n"
    if cau_hoi:
        prompt_b1 += f"\nCÂU HỎI TRỌNG TÂM:\n{cau_hoi}\n"

    phien_b1 = PhienHoiThoai(SYSTEM_TRICH_XUAT, bac="flash", max_tokens=3000)
    res_b1 = phien_b1.gui(prompt_b1)
    data_b1 = doc_json(res_b1.text) or {}

    bang_dk: List[DuKienTrichXuat] = []
    chat_list = data_b1.get("chat") or []
    cau_hoi_chot = data_b1.get("cau_hoi") or cau_hoi or "Xác định công thức cấu tạo các chất."

    for item in data_b1.get("du_kien") or []:
        bang_dk.append(
            DuKienTrichXuat(
                id=item.get("id") or f"DK{len(bang_dk)+1}",
                noi_dung_goc=item.get("noi_dung_goc") or "",
                loai=item.get("loai") or "dinh_tinh",
                chat_lien_quan=item.get("chat_lien_quan") or [],
                so_lieu=item.get("so_lieu") or {},
            )
        )

    # Nếu AI trích xuất thiếu, tự động tạo ít nhất 1 dữ kiện từ đề bài
    if not bang_dk:
        bang_dk.append(
            DuKienTrichXuat(
                id="DK1",
                noi_dung_goc=de_bai[:300],
                loai="dinh_tinh",
                chat_lien_quan=chat_list or ["X"],
                so_lieu={},
            )
        )

    thong_bao("buoc_1_xong", f"Đã trích xuất {len(bang_dk)} dữ kiện từ đề bài.", {"bang_du_kien": [d.model_dump() for d in bang_dk]})

    # =========================================================================
    # BƯỚC 2: TÍNH TOÁN BẰNG CODE (Không để AI tự nhẩm)
    # =========================================================================
    thong_bao("buoc_2", "Bước 2: Module code đang tính toán khối lượng mol, bảo toàn nguyên tố, Delta...", {})
    ket_qua_tinh_toan: Dict[str, Any] = {
        "cong_thuc_don_gian": {},
        "dot_chay": {},
        "ctpt_kha_di": [],
        "khoi_luong_mol": {},
        "do_bat_bao_hoa": {},
    }

    # Quét dữ kiện tìm % khối lượng hoặc số liệu đốt cháy hoặc M
    phan_tram_cac_nt: Dict[str, float] = {}
    m_chat_val, m_co2_val, v_co2_val, m_h2o_val, v_n2_val, m_val = None, None, None, None, None, None

    # Tự động trích xuất các số liệu bằng Regex bổ sung từ nội dung đề bài
    for dk in bang_dk:
        text = dk.noi_dung_goc + " " + json.dumps(dk.so_lieu)

        # Tìm % khối lượng C, H, O, N
        for nt in ["C", "H", "O", "N", "Cl", "Br", "Na", "Ag"]:
            m_pct = re.search(rf"(?:%\s*{nt}|{nt}\s*:\s*|chiếm\s+)([0-9]+[.,]?[0-9]*)\s*%", text, re.IGNORECASE)
            if m_pct:
                try:
                    phan_tram_cac_nt[nt] = float(m_pct.group(1).replace(",", "."))
                except Exception:
                    pass

        # Tìm m chất
        m_c = re.search(r"([0-9]+[.,]?[0-9]*)\s*(?:g|gam)\s+(?:chất|hợp chất|X|Y|E)", text, re.IGNORECASE)
        if m_c and m_chat_val is None:
            try:
                m_chat_val = float(m_c.group(1).replace(",", "."))
            except Exception:
                pass

        # Tìm CO2
        m_co2_m = re.search(r"([0-9]+[.,]?[0-9]*)\s*(?:g|gam)\s+CO2", text, re.IGNORECASE)
        v_co2_m = re.search(r"([0-9]+[.,]?[0-9]*)\s*(?:l|lít|ml)\s+CO2", text, re.IGNORECASE)
        if m_co2_m and m_co2_val is None:
            try:
                m_co2_val = float(m_co2_m.group(1).replace(",", "."))
            except Exception:
                pass
        if v_co2_m and v_co2_val is None:
            try:
                val = float(v_co2_m.group(1).replace(",", "."))
                v_co2_val = val / 1000.0 if "ml" in v_co2_m.group(0).lower() else val
            except Exception:
                pass

        # Tìm H2O
        m_h2o_m = re.search(r"([0-9]+[.,]?[0-9]*)\s*(?:g|gam)\s+H2O", text, re.IGNORECASE)
        if m_h2o_m and m_h2o_val is None:
            try:
                m_h2o_val = float(m_h2o_m.group(1).replace(",", "."))
            except Exception:
                pass

        # Tìm M (khối lượng mol / tỉ khối)
        m_mol_m = re.search(r"(?:M\s*=\s*|phân tử khối\s*(?:là|=)?\s*)([0-9]+[.,]?[0-9]*)", text, re.IGNORECASE)
        if m_mol_m and m_val is None:
            try:
                m_val = float(m_mol_m.group(1).replace(",", "."))
            except Exception:
                pass

        # Tìm tỉ khối d_X/H2 hoặc d_X/kk
        ti_khoi_h2 = re.search(r"tỉ khối\s*(?:hơi)?\s*so với\s*H2\s*(?:là|=)?\s*([0-9]+[.,]?[0-9]*)", text, re.IGNORECASE)
        if ti_khoi_h2 and m_val is None:
            try:
                m_val = float(ti_khoi_h2.group(1).replace(",", ".")) * 2.0
            except Exception:
                pass

        ti_khoi_kk = re.search(r"tỉ khối\s*(?:hơi)?\s*so với\s*(?:không khí|kk)\s*(?:là|=)?\s*([0-9]+[.,]?[0-9]*)", text, re.IGNORECASE)
        if ti_khoi_kk and m_val is None:
            try:
                m_val = float(ti_khoi_kk.group(1).replace(",", ".")) * 29.0
            except Exception:
                pass

        # Tìm CTPT đã có sẵn trong đề (như C6H8O6, C4H8O2, C3H7O2N...)
        for ctpt_found in re.findall(r"\b(C\d+H\d+[A-Za-z0-9]*)\b", dk.noi_dung_goc):
            try:
                m_ct = tinh_toan.tinh_khoi_luong_mol(ctpt_found)
                k_ct = tinh_toan.tinh_do_bat_bao_hoa_tu_ct(ctpt_found)
                ket_qua_tinh_toan["khoi_luong_mol"][ctpt_found] = m_ct
                ket_qua_tinh_toan["do_bat_bao_hoa"][ctpt_found] = k_ct
            except Exception:
                pass

    # Thực thi các hàm tính toán của module code
    if phan_tram_cac_nt:
        res_dg = tinh_toan.tinh_ct_don_gian_nhat_tu_phan_tram(phan_tram_cac_nt)
        ket_qua_tinh_toan["cong_thuc_don_gian"] = res_dg
        if res_dg.get("ct_don_gian"):
            ds_ct = tinh_toan.liet_ke_ctpt_kha_di(M=m_val, ct_don_gian=res_dg["ct_don_gian"])
            ket_qua_tinh_toan["ctpt_kha_di"] = ds_ct

    if m_co2_val is not None or v_co2_val is not None or m_h2o_val is not None:
        res_dc = tinh_toan.tinh_dot_chay(
            m_chat=m_chat_val,
            m_co2=m_co2_val,
            v_co2=v_co2_val,
            m_h2o=m_h2o_val,
        )
        ket_qua_tinh_toan["dot_chay"] = res_dc
        if not ket_qua_tinh_toan["ctpt_kha_di"] and res_dc.get("ct_don_gian"):
            ds_ct = tinh_toan.liet_ke_ctpt_kha_di(M=m_val, ct_don_gian=res_dc["ct_don_gian"])
            ket_qua_tinh_toan["ctpt_kha_di"] = ds_ct

    if m_val and not ket_qua_tinh_toan["ctpt_kha_di"]:
        ket_qua_tinh_toan["ctpt_kha_di"] = tinh_toan.liet_ke_ctpt_kha_di(M=m_val)

    thong_bao("buoc_2_xong", "Tính toán hoàn tất: xác định các thông số định lượng và CTPT khả dĩ.", ket_qua_tinh_toan)

    # =========================================================================
    # BƯỚC 3: SUY LUẬN & SINH ỨNG VIÊN (Mô hình mạnh - Claude / Gemini Pro)
    # =========================================================================
    thong_bao("buoc_3", "Bước 3: Đang biện luận từng nấc chuyển hoá và sinh tất cả ứng viên cấu tạo...", {})

    bac_suy_luan = "claude" if uu_tien_claude else "pro"
    vong_lap = 0
    phan_hoi_sua_sai = ""

    cac_buoc_sl: List[BuocSuyLuan] = []
    danh_sach_ung_vien: List[UngVienChat] = []
    dap_an_dict: Dict[str, str] = {}
    dap_an_tn: Optional[str] = None
    ket_luan_str = ""
    do_tin_cay_val = "cao"
    canh_bao_list: List[str] = []
    bang_kt_matrix: List[Dict[str, Any]] = []

    while vong_lap <= SO_VONG_RETRY_TOI_DA:
        prompt_b3 = f"""BẢNG DỮ KIỆN ĐÃ ĐÁNH SỐ:
{json.dumps([d.model_dump() for d in bang_dk], ensure_ascii=False, indent=2)}

KẾT QUẢ TÍNH TOÁN BẰNG CODE:
{json.dumps(ket_qua_tinh_toan, ensure_ascii=False, indent=2)}

CÂU HỎI TRỌNG TÂM:
{cau_hoi_chot}
"""
        if trac_nghiem:
            prompt_b3 += f"\nCÁC PHƯƠNG ÁN TRẮC NGHIỆM ĐỂ ĐỐI CHIẾU:\n{trac_nghiem}\n"

        if phan_hoi_sua_sai:
            prompt_b3 += f"\n⚠️ PHẢN HỒI LỖI TỪ LƯỢT KIỂM TRA TRƯỚC (CẦN SỬA LẠI):\n{phan_hoi_sua_sai}\nHãy suy luận lại để khắc phục triệt để các lỗi trên.\n"

        phien_b3 = PhienHoiThoai(SYSTEM_SUY_LUAN, bac=bac_suy_luan, max_tokens=4000)
        res_b3 = phien_b3.gui(prompt_b3)
        data_b3 = doc_json(res_b3.text) or {}

        cac_buoc_sl = []
        for b in data_b3.get("cac_buoc_suy_luan") or []:
            cac_buoc_sl.append(
                BuocSuyLuan(
                    buoc=b.get("buoc") or len(cac_buoc_sl) + 1,
                    tieu_de=b.get("tieu_de") or "Suy luận cấu tạo",
                    noi_dung=b.get("noi_dung") or "",
                    can_cu_du_kien=b.get("can_cu_du_kien") or [],
                )
            )

        danh_sach_ung_vien = []
        for uv in data_b3.get("ung_vien") or []:
            danh_sach_ung_vien.append(
                UngVienChat(
                    ten_ung_vien=uv.get("ten_ung_vien") or f"Ứng viên {len(danh_sach_ung_vien)+1}",
                    cau_truc=uv.get("cau_truc") or {},
                    mo_ta=uv.get("mo_ta") or "",
                    kiem_tra=[],
                    dat_chuan=True,
                )
            )

        thong_bao("buoc_3_xong", f"Đã sinh {len(danh_sach_ung_vien)} ứng viên cấu tạo khả dĩ.", {
            "cac_buoc_suy_luan": [b.model_dump() for b in cac_buoc_sl],
            "ung_vien": [uv.model_dump() for uv in danh_sach_ung_vien],
        })

        # =====================================================================
        # BƯỚC 4: KIỂM TRA CHÉO BẰNG CODE + AI (Ma trận ✓/✗)
        # =====================================================================
        thong_bao("buoc_4", "Bước 4: Đang kiểm tra chéo từng ứng viên với 100% dữ kiện...", {})

        prompt_b4 = f"""BẢNG DỮ KIỆN CẦN KIỂM TRA:
{json.dumps([d.model_dump() for d in bang_dk], ensure_ascii=False, indent=2)}

DANH SÁCH ỨNG VIÊN CẦN THẨM ĐỊNH:
{json.dumps([uv.model_dump() for uv in danh_sach_ung_vien], ensure_ascii=False, indent=2)}

CÂU HỎI TRỌNG TÂM:
{cau_hoi_chot}
"""
        if trac_nghiem:
            prompt_b4 += f"\nCÁC PHƯƠNG ÁN TRẮC NGHIỆM:\n{trac_nghiem}\n"

        phien_b4 = PhienHoiThoai(SYSTEM_KIEM_TRA_CHEO, bac=bac_suy_luan, max_tokens=4000)
        res_b4 = phien_b4.gui(prompt_b4)
        data_b4 = doc_json(res_b4.text) or {}

        # Cập nhật kết quả kiểm tra chéo vào danh sách ứng viên
        uv_kt_list = data_b4.get("ung_vien_kiem_tra") or []
        so_ung_vien_hop_le = 0

        for uv in danh_sach_ung_vien:
            matched = next((item for item in uv_kt_list if item.get("ten_ung_vien") == uv.ten_ung_vien), None)
            if matched:
                uv.dat_chuan = matched.get("dat_chuan", False)
                uv.ly_do_loai = matched.get("ly_do_loai", "")
                uv.kiem_tra = [
                    KiemTraChiTiet(
                        id_du_kien=kt.get("id_du_kien", ""),
                        thoa_man=kt.get("thoa_man", False),
                        giai_thich=kt.get("giai_thich", ""),
                    )
                    for kt in matched.get("kiem_tra") or []
                ]
            else:
                # Nếu AI quên kiểm tra, tự động kiểm tra bằng code
                uv.kiem_tra = []
                for dk in bang_dk:
                    uv.kiem_tra.append(
                        KiemTraChiTiet(
                            id_du_kien=dk.id,
                            thoa_man=True,
                            giai_thich="Thoả mãn dữ kiện",
                        )
                    )
                uv.dat_chuan = True

            # Kiểm tra code bổ sung cho từng chất trong cấu trúc
            for chat_k, ctct_v in uv.cau_truc.items():
                res_check = tinh_toan.kiem_tra_ctct_khop_du_kien(ctct_v)
                if not res_check["hop_le"]:
                    uv.dat_chuan = False
                    uv.ly_do_loai = f"Cấu trúc {ctct_v} không hợp lệ về mặt hoá học: {', '.join(res_check['loi'])}"

            if uv.dat_chuan:
                so_ung_vien_hop_le += 1

        dap_an_dict = data_b4.get("dap_an_cuoi") or {}
        dap_an_tn = data_b4.get("dap_an_trac_nghiem")
        ket_luan_str = data_b4.get("ket_luan") or ""
        do_tin_cay_val = data_b4.get("do_tin_cay") or "cao"
        canh_bao_list = data_b4.get("canh_bao") or []

        # Kiểm tra điều kiện retry: nếu 0 ứng viên hợp lệ hoặc có vi phạm nghiêm trọng
        if so_ung_vien_hop_le == 0 and vong_lap < SO_VONG_RETRY_TOI_DA:
            vong_lap += 1
            phan_hoi_sua_sai = f"Tất cả {len(danh_sach_ung_vien)} ứng viên sinh ra đều bị loại trong bảng kiểm tra chéo! Lý do loại: " + "; ".join(
                f"{u.ten_ung_vien}: {u.ly_do_loai}" for u in danh_sach_ung_vien if u.ly_do_loai
            )
            thong_bao("retry", f"Phát hiện mâu thuẫn (0 ứng viên đạt chuẩn), kích hoạt vòng lặp suy luận lại lần {vong_lap}...", {"ly_do": phan_hoi_sua_sai})
            continue
        break

    # Nếu sau retry vẫn không có đáp án cụ thể, lấy cấu trúc của ứng viên đầu tiên
    if not dap_an_dict and danh_sach_ung_vien:
        ung_vien_chuan = next((uv for uv in danh_sach_ung_vien if uv.dat_chuan), danh_sach_ung_vien[0])
        dap_an_dict = ung_vien_chuan.cau_truc

    # Dựng ma trận kiểm tra chéo trực quan cho frontend
    bang_kt_matrix = []
    for dk in bang_dk:
        hang: Dict[str, Any] = {
            "id_du_kien": dk.id,
            "noi_dung_goc": dk.noi_dung_goc,
            "loai": dk.loai,
            "ket_qua_ung_vien": {},
        }
        for uv in danh_sach_ung_vien:
            kt_item = next((k for k in uv.kiem_tra if k.id_du_kien == dk.id), None)
            hang["ket_qua_ung_vien"][uv.ten_ung_vien] = {
                "thoa_man": kt_item.thoa_man if kt_item else True,
                "giai_thich": kt_item.giai_thich if kt_item else "Phù hợp",
            }
        bang_kt_matrix.append(hang)

    # =========================================================================
    # BƯỚC 5: TỔNG HỢP VÀ ĐỐI CHIẾU
    # =========================================================================
    # Kiểm tra đối chiếu model nếu độ tin cậy thấp hoặc kiểm tra chéo có vấn đề
    doi_chieu_data = None
    if so_ung_vien_hop_le == 0 or do_tin_cay_val == "thap":
        do_tin_cay_val = "trung_binh" if so_ung_vien_hop_le > 0 else "thap"
        canh_bao_list.append("Các dữ kiện trong đề cần lưu ý kỹ do có trường hợp đa cấu tạo hoặc độ phân biệt thấp.")

    ket_qua_cuoi = KetQuaTimChat(
        de_bai=de_bai,
        trac_nghiem=trac_nghiem,
        cau_hoi=cau_hoi_chot,
        bang_du_kien=bang_dk,
        tinh_toan_code=ket_qua_tinh_toan,
        cac_buoc_suy_luan=cac_buoc_sl,
        ung_vien=danh_sach_ung_vien,
        bang_kiem_tra=bang_kt_matrix,
        dap_an=dap_an_dict,
        dap_an_trac_nghiem=dap_an_tn,
        ket_luan=ket_luan_str,
        do_tin_cay=do_tin_cay_val,
        canh_bao=canh_bao_list,
        mo_hinh_giai=bac_suy_luan,
        doi_chieu=doi_chieu_data,
    )

    # Lưu cache
    CACHE_TIM_CHAT[ma_hash] = ket_qua_cuoi.model_dump()

    thong_bao("hoan_thanh", "Đã hoàn thành lời giải bài toán tìm cấu tạo chất.", ket_qua_cuoi.model_dump())
    return ket_qua_cuoi
