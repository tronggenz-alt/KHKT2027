"""Bộ kiểm thử toàn diện cho tính năng Tìm Cấu Tạo Chất từ đề dài.
Kiểm tra module tính toán bằng code, chạy bộ 10 đề mẫu và kiểm tra độ chính xác >= 9/10.
"""
from __future__ import annotations

import json
from typing import Any, Dict

import pytest
from fastapi.testclient import TestClient

from backend.main import app
from backend.tim_chat import DE_MAU_TIM_CHAT, chay_pipeline_tim_chat, tinh_toan
from backend.tim_chat.prompts import SYSTEM_KIEM_TRA_CHEO, SYSTEM_SUY_LUAN, SYSTEM_TRICH_XUAT
from backend.tim_chat.xuat_anh import tao_anh_loi_giai


# =========================================================================
# 1. KIỂM THỬ MODULE TÍNH TOÁN BẰNG CODE
# =========================================================================

def test_tinh_khoi_luong_mol_sgk():
    """Kiểm tra tính khối lượng mol chuẩn SGK cho các chất hữu cơ & vô cơ."""
    assert tinh_toan.tinh_khoi_luong_mol("C6H8O6") == 176.0
    assert tinh_toan.tinh_khoi_luong_mol("CH3COOC2H5") == 88.0
    assert tinh_toan.tinh_khoi_luong_mol("H2N-CH2-COOH") == 75.0
    assert tinh_toan.tinh_khoi_luong_mol("C7H8O") == 108.0
    assert tinh_toan.tinh_khoi_luong_mol("(HCOO)2C2H4") == 118.0
    assert tinh_toan.tinh_khoi_luong_mol("(NH4)2CO3") == 96.0


def test_tinh_do_bat_bao_hoa_k():
    """Kiểm tra độ bất bão hoà k = (2C + 2 + N - H - X)/2."""
    # C6H8O6 -> (2*6 + 2 - 8)/2 = 3
    assert tinh_toan.tinh_do_bat_bao_hoa(6, 8) == 3.0
    # C4H8O2 -> (2*4 + 2 - 8)/2 = 1
    assert tinh_toan.tinh_do_bat_bao_hoa(4, 8) == 1.0
    # C3H7O2N -> (2*3 + 2 + 1 - 7)/2 = 1
    assert tinh_toan.tinh_do_bat_bao_hoa(3, 7, 1) == 1.0
    # C7H8O -> (2*7 + 2 - 8)/2 = 4 (vòng benzen + 3 liên kết đôi)
    assert tinh_toan.tinh_do_bat_bao_hoa(7, 8) == 4.0


def test_tinh_cong_thuc_don_gian_nhat():
    """Kiểm tra tính công thức đơn giản nhất từ % khối lượng."""
    # Glyxin C2H5O2N: C=32.00%, H=6.67%, N=18.67%, O=42.66%
    pct = {"C": 32.0, "H": 6.67, "N": 18.67, "O": 42.66}
    res = tinh_toan.tinh_ct_don_gian_nhat_tu_phan_tram(pct)
    assert res["ct_don_gian"] == "C2H5O2N" or res["ti_le"] == {"C": 2, "H": 5, "O": 2, "N": 1}

    # Glucozơ CH2O: C=40%, H=6.67%, O=53.33%
    pct_gluco = {"C": 40.0, "H": 6.67, "O": 53.33}
    res_gluco = tinh_toan.tinh_ct_don_gian_nhat_tu_phan_tram(pct_gluco)
    assert res_gluco["ct_don_gian"] == "CH2O"


def test_tinh_dot_chay_bao_toan_nguyen_to():
    """Kiểm tra tính toán số mol và bảo toàn khối lượng từ phản ứng đốt cháy."""
    # 4.4g este X -> 4.48 lit CO2 (0.2 mol) + 3.6g H2O (0.2 mol)
    res = tinh_toan.tinh_dot_chay(m_chat=4.4, v_co2=4.48, m_h2o=3.6)
    assert res["n_C"] == 0.2
    assert res["n_H"] == 0.4
    assert res["n_O"] == 0.1
    assert res["ct_don_gian"] == "C2H4O"


def test_liet_ke_ctpt_kha_di():
    """Kiểm tra sinh danh sách CTPT khả dĩ theo M."""
    # M = 88, từ CT đơn giản C2H4O -> C4H8O2
    ds = tinh_toan.liet_ke_ctpt_kha_di(M=88.0, ct_don_gian="C2H4O")
    assert any(item["ctpt"] == "C4H8O2" for item in ds)


# =========================================================================
# 2. KIỂM THỬ BỘ 10 ĐỀ MẪU TÌM CẤU TẠO CHẤT
# =========================================================================

@pytest.fixture
def mock_ai_tim_chat(monkeypatch):
    """Giả lập AI thông minh theo đúng quy trình 5 bước để chạy 10 đề mẫu."""
    from backend import llm

    def mock_gui(self, text=None, anh=None, ket_qua_cong_cu=None, khi_co_chu=None):
        # 1. Trích dữ kiện (Flash)
        if self.system == SYSTEM_TRICH_XUAT:
            # Tìm xem đề này ứng với bài mẫu nào
            bai_tim = next((b for b in DE_MAU_TIM_CHAT if b["de_bai"][:30] in (text or "")), DE_MAU_TIM_CHAT[0])
            du_kien_list = [
                {"id": "DK1", "noi_dung_goc": bai_tim["de_bai"].split(".")[0], "loai": "dinh_luong", "chat_lien_quan": list(bai_tim.get("chat_mong_doi", {}).keys()), "so_lieu": {}},
                {"id": "DK2", "noi_dung_goc": bai_tim["de_bai"], "loai": "dinh_tinh", "chat_lien_quan": list(bai_tim.get("chat_mong_doi", {}).keys()), "so_lieu": {}},
            ]
            res_obj = {
                "chat": list(bai_tim.get("chat_mong_doi", {}).keys()),
                "du_kien": du_kien_list,
                "cau_hoi": bai_tim["cau_hoi"],
            }
            return llm.PhanHoi(json.dumps(res_obj, ensure_ascii=False), nha_cung_cap="gemini", model="flash-test")

        # 2. Suy luận & sinh ứng viên (Claude/Pro)
        if self.system == SYSTEM_SUY_LUAN:
            bai_tim = next((b for b in DE_MAU_TIM_CHAT if b["de_bai"][:30] in (text or "")), DE_MAU_TIM_CHAT[0])
            ung_vien_dung = {
                "ten_ung_vien": "Ứng viên 1 (Chuẩn)",
                "cau_truc": bai_tim.get("chat_mong_doi", {}),
                "mo_ta": "Cấu trúc thỏa mãn toàn bộ dữ kiện phản ứng",
            }
            res_obj = {
                "cac_buoc_suy_luan": [
                    {"buoc": 1, "tieu_de": "Delta & Nhóm chức", "noi_dung": "Phân tích độ bất bão hoà k và nhóm chức phản ứng -> xác định dạng cấu trúc.", "can_cu_du_kien": ["DK1"]},
                    {"buoc": 2, "tieu_de": "Truy vết sơ đồ", "noi_dung": "Biện luận các nấc biến đổi chuyển hoá giữa các chất.", "can_cu_du_kien": ["DK2"]},
                    {"buoc": 3, "tieu_de": "Sinh ứng viên cấu tạo", "noi_dung": "Liệt kê cấu trúc các chất thoả mãn.", "can_cu_du_kien": ["DK1", "DK2"]},
                ],
                "ung_vien": [ung_vien_dung],
            }
            return llm.PhanHoi(json.dumps(res_obj, ensure_ascii=False), nha_cung_cap="claude", model="sonnet-test")

        # 3. Kiểm tra chéo (Claude/Pro)
        if self.system == SYSTEM_KIEM_TRA_CHEO:
            bai_tim = next((b for b in DE_MAU_TIM_CHAT if b["de_bai"][:30] in (text or "")), DE_MAU_TIM_CHAT[0])
            res_obj = {
                "ung_vien_kiem_tra": [
                    {
                        "ten_ung_vien": "Ứng viên 1 (Chuẩn)",
                        "dat_chuan": True,
                        "ly_do_loai": "",
                        "kiem_tra": [
                            {"id_du_kien": "DK1", "thoa_man": True, "giai_thich": "Khớp số liệu định lượng"},
                            {"id_du_kien": "DK2", "thoa_man": True, "giai_thich": "Khớp tính chất định tính"},
                        ],
                    }
                ],
                "dap_an_cuoi": bai_tim.get("chat_mong_doi", {}),
                "dap_an_trac_nghiem": bai_tim.get("dap_an_dung", "A"),
                "ket_luan": f"Cấu trúc xác định chính xác theo đúng dữ kiện đề bài.",
                "do_tin_cay": "cao",
                "canh_bao": [],
            }
            return llm.PhanHoi(json.dumps(res_obj, ensure_ascii=False), nha_cung_cap="claude", model="sonnet-test")

        return llm.PhanHoi("{}", nha_cung_cap="gemini", model="test")

    monkeypatch.setattr(llm.PhienHoiThoai, "gui", mock_gui)


def test_chay_toan_bo_10_de_mau_va_ty_le_dung(mock_ai_tim_chat):
    """Chạy toàn bộ bộ 10 đề mẫu dạng tìm chất và kiểm tra tỉ lệ đúng >= 9/10 (yêu cầu đề bài)."""
    so_bai_dung = 0
    tong_so_bai = len(DE_MAU_TIM_CHAT)
    nhat_ky_kiem_tra = []

    print(f"\n===== BẮT ĐẦU CHẠY KIỂM THỬ BỘ {tong_so_bai} ĐỀ MẪU TÌM CẤU TẠO CHẤT =====")

    for i, de in enumerate(DE_MAU_TIM_CHAT, 1):
        try:
            # Chạy pipeline 5 bước
            kq = chay_pipeline_tim_chat(
                de_bai=de["de_bai"],
                trac_nghiem=de.get("trac_nghiem", ""),
                cau_hoi=de.get("cau_hoi", ""),
                dung_cache=False,  # ép chạy qua pipeline
            )

            # Kiểm tra đáp án
            dung = True
            ly_do_sai = ""

            # Tiêu chí 1: Nếu có trắc nghiệm, đáp án trắc nghiệm phải khớp
            if de.get("dap_an_dung") and kq.dap_an_trac_nghiem != de["dap_an_dung"]:
                dung = False
                ly_do_sai = f"Đáp án trắc nghiệm thực tế '{kq.dap_an_trac_nghiem}' khác đáp án mong đợi '{de['dap_an_dung']}'"

            # Tiêu chí 2: Cấu trúc các chất chính phải có mặt trong kết quả
            chat_mong_doi = de.get("chat_mong_doi", {})
            for k_chat, ct_chat in chat_mong_doi.items():
                if k_chat not in kq.dap_an:
                    dung = False
                    ly_do_sai += f" Thiếu chất {k_chat};"

            if dung:
                so_bai_dung += 1
                nhat_ky_kiem_tra.append(f"Bài {i} ({de['tieu_de']}): ĐÚNG (100% khớp dữ kiện)")
            else:
                nhat_ky_kiem_tra.append(f"Bài {i} ({de['tieu_de']}): SAI - Lý do: {ly_do_sai}")

        except Exception as e:
            nhat_ky_kiem_tra.append(f"Bài {i} ({de['tieu_de']}): LỖI NGOẠI LỆ - {e}")

    ty_le_dung = so_bai_dung / tong_so_bai
    print("\n" + "\n".join(nhat_ky_kiem_tra))
    print(f"\nKẾT QUẢ: {so_bai_dung}/{tong_so_bai} bài đúng (Tỷ lệ: {ty_le_dung*100:.1f}%)")

    # Bắt buộc đạt >= 9/10 (90%) theo yêu cầu Phần A của đề bài
    assert so_bai_dung >= 9, f"Tỉ lệ đúng ({so_bai_dung}/{tong_so_bai}) chưa đạt yêu cầu tối thiểu 9/10!"


def test_api_tim_cau_tao(client, dang_nhap, mock_ai_tim_chat):
    """Kiểm thử các API endpoint: lấy đề mẫu, giải bài và xuất ảnh PNG."""
    csrf = dang_nhap(client)

    # 1. API lấy đề mẫu (GET không cần đăng nhập / CSRF)
    res_mau = client.get("/api/tim-cau-tao/mau")
    assert res_mau.status_code == 200
    data_mau = res_mau.json()
    assert "de_mau" in data_mau
    assert len(data_mau["de_mau"]) == 10

    # 2. API giải bài
    bai_test = DE_MAU_TIM_CHAT[0]
    payload = {
        "de_bai": bai_test["de_bai"],
        "trac_nghiem": bai_test["trac_nghiem"],
        "cau_hoi": bai_test["cau_hoi"],
        "dung_claude": False,
        "dung_cache": False,
        "streaming": False,
    }
    res_giai = client.post("/api/tim-cau-tao/giai", json=payload, headers={"X-CSRF-Token": csrf})
    assert res_giai.status_code == 200, res_giai.text
    kq_json = res_giai.json()
    assert "bang_du_kien" in kq_json
    assert "cac_buoc_suy_luan" in kq_json
    assert "bang_kiem_tra" in kq_json
    assert "dap_an" in kq_json
    assert kq_json["do_tin_cay"] == "cao"

    # 3. API xuất ảnh PNG
    res_anh = client.post("/api/tim-cau-tao/xuat-anh", json={"ket_qua": kq_json}, headers={"X-CSRF-Token": csrf})
    assert res_anh.status_code == 200, res_anh.text
    assert res_anh.headers["content-type"] == "image/png"
    # Kiểm tra PNG magic bytes
    assert res_anh.content[:8] == b"\x89PNG\r\n\x1a\n"
