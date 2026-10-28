# tests/test_sinh_hoc_rag_phan_hoi.py
"""
Kiểm thử hệ thống RAG Sinh học, lưu phản hồi học sinh và API quản trị kho kiến thức.
"""
import pytest
from backend.sinh_hoc_rag import (
    cap_nhat_chi_muc,
    lay_doan_tai_lieu,
    dinh_dang_tai_lieu_prompt,
    danh_sach_file_knowledge,
    doc_file_knowledge,
    ghi_file_knowledge,
    xoa_file_knowledge,
)


def test_rag_tim_kiem():
    """Kiểm tra RAG tìm đúng đoạn tài liệu cho các từ khoá tuần hoàn và hô hấp."""
    kq_tim = lay_doan_tai_lieu("chu kì tim 0,8s tâm thất co bao nhiêu", toi_da=4)
    assert len(kq_tim) > 0
    noi_dung = " ".join([d["chunk_text"] for d in kq_tim])
    assert "0,8" in noi_dung or "tim" in noi_dung

    prompt_rag = dinh_dang_tai_lieu_prompt(kq_tim)
    assert "<tai_lieu>" in prompt_rag
    assert "</tai_lieu>" in prompt_rag


def test_crud_knowledge_file():
    """Kiểm tra quản trị CRUD file trong kho kiến thức."""
    files = danh_sach_file_knowledge()
    assert len(files) >= 4
    names = [f["name"] for f in files]
    assert "tuan-hoan.md" in names
    assert "ho-hap.md" in names

    # Đọc thử file tuan-hoan
    nd = doc_file_knowledge("tuan-hoan.md")
    assert nd is not None
    assert "TUẦN HOÀN" in nd.upper()

    # Thử tạo file tạm và xoá
    ten_moi = "test-chu-de-moi.md"
    kq_ghi = ghi_file_knowledge(ten_moi, "# Chủ đề test mới\nKiến thức test.")
    assert "so_file" in kq_ghi
    assert kq_ghi["so_file"] >= len(names)

    nd_moi = doc_file_knowledge(ten_moi)
    assert "Chủ đề test mới" in nd_moi

    kq_xoa = xoa_file_knowledge(ten_moi)
    assert "so_file" in kq_xoa
    with pytest.raises(Exception):
        doc_file_knowledge(ten_moi)


def test_api_phan_hoi(client, dang_nhap):
    """Kiểm tra API nhận phản hồi học sinh 👍 / 👎."""
    h = {"X-CSRF-Token": dang_nhap(client)}
    res = client.post(
        "/api/sinh-hoc/phan-hoi",
        headers=h,
        json={
            "cau_hoi": "Máu ở động mạch phổi giàu hay nghèo O2?",
            "cau_tra_loi": "Máu ở động mạch phổi là máu nghèo O2, giàu CO2.",
            "vote": "tot",
            "ly_do": "",
            "chi_tiet": "",
        },
    )
    assert res.status_code == 200
    data = res.json()
    assert data.get("ok") is True

    # Gửi phản hồi chưa tốt (👎)
    res_sai = client.post(
        "/api/sinh-hoc/phan-hoi",
        headers=h,
        json={
            "cau_hoi": "Câu hỏi test sai",
            "cau_tra_loi": "Câu trả lời test",
            "vote": "chua_tot",
            "ly_do": "sai_kien_thuc",
            "chi_tiet": "Thiếu số liệu chu kì tim",
        },
    )
    assert res_sai.status_code == 200
    assert res_sai.json().get("ok") is True
