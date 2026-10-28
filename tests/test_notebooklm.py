"""Kiểm thử tự động cho hệ thống kết nối NotebookLM và Mindmap/AI Riêng."""
import json
import pytest
from unittest.mock import MagicMock, patch

from backend import bao_mat, db
from backend.config import settings
from services.notebooklm.client import parse_cookie_input, NotebookLMClient
from services.notebooklm.manager import (
    them_tai_khoan,
    ds_tai_khoan,
    lay_tai_khoan,
    cap_nhat_tai_khoan,
    dat_mac_dinh,
    xoa_tai_khoan,
    dong_bo_tai_khoan,
    kiem_tra_tai_khoan,
    lay_danh_sach_cache_notebooks,
    lay_chi_tiet_notebook,
    hoi_notebook,
)
from services.notebooklm.mindmap_adapter import lay_nhanh_mindmap
from services.notebooklm.ai_rieng_adapter import nhap_notebook_vao_ai_rieng, hoi_them_notebooklm


def cookie_gia(sid="dummy"):
    """Bộ cookie GIẢ đủ các tên bắt buộc (SID, HSID, SSID, APISID, SAPISID, __Secure-1PSID)."""
    return f"SID={sid}; HSID=h-gia; SSID=s-gia; APISID=a-gia; SAPISID=sa-gia; __Secure-1PSID=p-gia"


@pytest.fixture(autouse=True)
def clean_db(monkeypatch):
    """Khởi tạo và làm sạch bảng notebooklm trước và sau mỗi test."""
    monkeypatch.setattr(settings, "notebooklm_cookie_key", "khoa-thu-nghiem-chi-dung-cho-test-0123456789")
    with db.ket_noi() as con:
        con.execute("DELETE FROM notebooklm_cache")
        con.execute("DELETE FROM notebooklm_accounts")
    yield
    with db.ket_noi() as con:
        con.execute("DELETE FROM notebooklm_cache")
        con.execute("DELETE FROM notebooklm_accounts")


def test_ma_hoa_giai_ma_cookie():
    cookie_goc = "SID=abc123xyz; HSID=def456; SSID=ghi789; OSID=jkl012;"
    ma_hoa = bao_mat.ma_hoa_cookie_notebooklm(cookie_goc)
    assert ma_hoa != cookie_goc
    assert isinstance(ma_hoa, str)

    giai_ma = bao_mat.giai_ma_cookie_notebooklm(ma_hoa)
    assert giai_ma == cookie_goc


def test_parse_cookie_input():
    # 1. Dạng chuỗi cookie chuẩn
    c1 = "SID=abc; HSID=def; SSID=ghi;"
    p1 = parse_cookie_input(c1)
    assert "SID=abc" in p1
    assert "HSID=def" in p1
    assert "SSID=ghi" in p1

    # 2. Dạng JSON mảng EditThisCookie
    json_str = json.dumps([
        {"name": "SID", "value": "json_sid"},
        {"name": "HSID", "value": "json_hsid"}
    ])
    p2 = parse_cookie_input(json_str)
    assert "SID=json_sid" in p2
    assert "HSID=json_hsid" in p2

    # 3. Dạng lệnh curl
    curl_str = "curl 'https://notebooklm.google.com/' -H 'cookie: SID=curl_sid; OSID=curl_osid'"
    p3 = parse_cookie_input(curl_str)
    assert "SID=curl_sid" in p3
    assert "OSID=curl_osid" in p3


@patch("services.notebooklm.client.NotebookLMClient.kiem_tra_ket_noi")
@patch("services.notebooklm.client.NotebookLMClient.list_notebooks")
def test_quan_ly_tai_khoan_crud(mock_list, mock_check):
    mock_check.return_value = (True, "", {
        "email": "teacher.chemistry@gmail.com",
        "name": "Thầy Giáo Hoá",
        "picture": "https://avatar.google.com/test.jpg",
    })
    mock_list.return_value = [
        {"notebook_id": "nb_123", "title": "Hoá Hữu Cơ 12", "description": "Ôn thi THPT", "so_nguon": 2}
    ]

    # 1. Thêm tài khoản
    acc = them_tai_khoan("Google Giáo Viên", cookie_gia("dummy"), is_default=True)
    assert acc["id"] is not None
    assert acc["ten_goi_nho"] == "Google Giáo Viên"
    assert acc["email"] == "teacher.chemistry@gmail.com"
    assert acc["is_default"] == 1
    assert acc["status"] == "dang_hoat_dong"

    # 2. Danh sách tài khoản
    ds = ds_tai_khoan()
    assert len(ds) == 1
    assert ds[0]["id"] == acc["id"]

    # 3. Thêm tài khoản thứ 2
    mock_check.return_value = (True, "", {
        "email": "research.lab@gmail.com",
        "name": "Lab Nghiên Cứu",
        "picture": "",
    })
    acc2 = them_tai_khoan("Lab Nghiên Cứu", cookie_gia("dummy2"), is_default=False)
    ds = ds_tai_khoan()
    assert len(ds) == 2

    # 4. Đặt mặc định
    dat_mac_dinh(acc2["id"])
    assert lay_tai_khoan(acc2["id"])["is_default"] == 1
    assert lay_tai_khoan(acc["id"])["is_default"] == 0

    # 5. Cập nhật tài khoản
    cap_nhat_tai_khoan(acc["id"], ten_goi_nho="Google Thầy Giáo Đổi Tên")
    acc_updated = lay_tai_khoan(acc["id"])
    assert acc_updated["ten_goi_nho"] == "Google Thầy Giáo Đổi Tên"

    # 6. Xóa tài khoản
    assert xoa_tai_khoan(acc2["id"]) is True
    assert len(ds_tai_khoan()) == 1


@patch("services.notebooklm.client.NotebookLMClient.kiem_tra_ket_noi")
@patch("services.notebooklm.client.NotebookLMClient.list_notebooks")
@patch("services.notebooklm.client.NotebookLMClient.get_notebook")
def test_dong_bo_va_cache_notebook(mock_get, mock_list, mock_check):
    mock_check.return_value = (True, "", {
        "email": "teacher@gmail.com",
        "name": "Teacher",
        "picture": "",
    })
    mock_list.return_value = [
        {"notebook_id": "nb_001", "title": "Ancol & Phenol", "description": "Tài liệu chuyên đề", "so_nguon": 2}
    ]
    mock_get.return_value = {
        "notebook_id": "nb_001",
        "title": "Ancol & Phenol",
        "description": "Tài liệu chuyên đề",
        "sources": [
            {"id": "src_1", "title": "SGK Hoá 12.pdf", "type": "pdf", "url": "https://sgk.vn/12.pdf"},
            {"id": "src_2", "title": "Phản ứng oxi hoá ancol", "type": "web", "url": "https://wiki.vn/ancol"}
        ],
        "notes": [
            {"id": "note_1", "title": "Lưu ý quy tắc Zaitsev", "content": "Tách nước ưu tiên tạo anken bền hơn"}
        ]
    }

    acc = them_tai_khoan("TK Dong Bo", cookie_gia("test"))
    res = dong_bo_tai_khoan(acc["id"])
    assert res["ok"] is True
    assert res["notebook_count"] == 1

    # Kiểm tra cache
    nbs = lay_danh_sach_cache_notebooks(acc["id"])
    assert len(nbs) == 1
    assert nbs[0]["notebook_id"] == "nb_001"
    assert nbs[0]["title"] == "Ancol & Phenol"

    detail = lay_chi_tiet_notebook(acc["id"], "nb_001")
    assert detail is not None
    assert len(detail["sources"]) == 2
    assert len(detail["notes"]) == 1


@patch("services.notebooklm.client.NotebookLMClient.kiem_tra_ket_noi")
@patch("services.notebooklm.client.NotebookLMClient.list_notebooks")
@patch("services.notebooklm.client.NotebookLMClient.get_notebook")
def test_mindmap_adapter(mock_get, mock_list, mock_check):
    mock_check.return_value = (True, "", {"email": "test@gmail.com", "name": "Test", "picture": ""})
    mock_list.return_value = [{"notebook_id": "nb_01", "title": "Este - Lipit", "description": "", "so_nguon": 1}]
    mock_get.return_value = {
        "notebook_id": "nb_01",
        "title": "Este - Lipit",
        "description": "",
        "sources": [{"id": "s1", "title": "Bảng đồng phân este.pdf", "type": "pdf"}],
        "notes": []
    }

    # Khi chưa có tài khoản nào
    nhanh_trong = lay_nhanh_mindmap()
    assert nhanh_trong["id"] == "notebooklm"
    assert nhanh_trong["status"] == "chua_co_tai_khoan"

    # Sau khi thêm tài khoản và đồng bộ
    acc = them_tai_khoan("TK 1", cookie_gia("mock"), is_default=True)
    dong_bo_tai_khoan(acc["id"])

    # Chế độ 1 tài khoản
    nhanh_mot = lay_nhanh_mindmap(acc["id"], tat_ca=False)
    assert nhanh_mot["id"] == "notebooklm"
    assert nhanh_mot["che_do"] == "mot"
    assert len(nhanh_mot["con"]) == 1
    assert nhanh_mot["con"][0]["is_notebook"] is True
    assert nhanh_mot["con"][0]["notebook_id"] == "nb_01"
    # Có node con là nguồn tài liệu
    assert len(nhanh_mot["con"][0]["con"]) == 1
    assert nhanh_mot["con"][0]["con"][0]["loai_nguon"] == "pdf"

    # Chế độ tất cả tài khoản
    nhanh_tat_ca = lay_nhanh_mindmap(tat_ca=True)
    assert nhanh_tat_ca["che_do"] == "tat_ca"
    assert len(nhanh_tat_ca["con"]) == 1  # 1 tài khoản
    assert nhanh_tat_ca["con"][0]["is_account"] is True


@patch("services.notebooklm.client.NotebookLMClient.kiem_tra_ket_noi")
@patch("services.notebooklm.client.NotebookLMClient.list_notebooks")
@patch("services.notebooklm.client.NotebookLMClient.get_notebook")
def test_nhap_notebook_vao_ai_rieng(mock_get, mock_list, mock_check):
    mock_check.return_value = (True, "", {"email": "ai.doc@gmail.com", "name": "AI Doc", "picture": ""})
    mock_list.return_value = [{"notebook_id": "nb_ar", "title": "Phản ứng thế Halogen", "description": "", "so_nguon": 1}]
    mock_get.return_value = {
        "notebook_id": "nb_ar",
        "title": "Phản ứng thế Halogen",
        "description": "Cơ chế thế gốc tự do SR của ankan",
        "sources": [
            {"id": "src_ar1", "title": "Ankan SR.txt", "type": "text", "content": "Phản ứng clo hoá metan tạo CH3Cl, CH2Cl2, CHCl3, CCl4."}
        ],
        "notes": [
            {"id": "note_ar1", "title": "Điều kiện ánh sáng", "content": "Cần askt (ánh sáng khuếch tán) để phân cắt đồng ly phân tử Cl2."}
        ]
    }

    acc = them_tai_khoan("TK AI Rieng", cookie_gia("test"))
    dong_bo_tai_khoan(acc["id"])

    # Nhập vào AI Riêng
    res = nhap_notebook_vao_ai_rieng(acc["id"], "nb_ar", dong_bo_tu_dong=True)
    assert res["ok"] is True
    assert res["tai_lieu_id"] > 0
    assert res["so_doan"] >= 2  # nguồn + ghi chú

    # Kiểm tra trong DB ar_tai_lieu và ar_doan
    with db.ket_noi() as con:
        tl = con.execute("SELECT * FROM ar_tai_lieu WHERE id = ?", (res["tai_lieu_id"],)).fetchone()
        assert tl is not None
        assert "NotebookLM" in tl["ten"]
        assert "TK AI Rieng" in tl["ten"]
        assert "Phản ứng thế Halogen" in tl["ten"]

        doan_rows = con.execute("SELECT * FROM ar_doan WHERE tai_lieu_id = ?", (res["tai_lieu_id"],)).fetchall()
        assert len(doan_rows) >= 2


@patch("services.notebooklm.client.NotebookLMClient.query")
def test_hoi_them_notebooklm(mock_query):
    mock_query.return_value = {
        "tra_loi": "Phản ứng C2H4 + H2O tạo ra C2H5OH trong môi trường axit H2SO4 hoặc H3PO4 ở nhiệt độ 300°C.",
        "citations": ["Tài liệu Hoá học Đại Cương tr. 45"],
        "references": []
    }

    with patch("services.notebooklm.client.NotebookLMClient.kiem_tra_ket_noi", return_value=(True, "", {"email": "q@gmail.com", "name": "Q"})):
        acc = them_tai_khoan("TK Query", cookie_gia("dummy"))

    tra_loi = hoi_them_notebooklm(acc["id"], "nb_dummy", "Điều kiện phản ứng hidrat hoá etilen là gì?")
    assert "C2H5OH" in tra_loi
    assert "H2SO4" in tra_loi


def test_tuy_chon_hoi_notebooklm_trong_giai():
    from backend.ai_rieng import giai
    with patch("backend.ai_rieng.giai.doc_de_trong_anh", return_value="C2H4 → C2H5OH"), \
         patch("backend.services.notebooklm.hoi_them_notebooklm", return_value="C2H4 + H2O -> C2H5OH xúc tác H+"), \
         patch("backend.ai_rieng.kho.tim_khop", return_value=None), \
         patch("backend.ai_rieng.tim.tim_kien_thuc", return_value=[]), \
         patch("backend.giai_nhanh.giai") as mock_giai, \
         patch("backend.giai_nhanh.kiem_tra", return_value=([], [], {})):

        mock_gon = MagicMock()
        mock_gon.buoc = []
        mock_gon.chua_duoc_day = []
        mock_giai.return_value = (mock_gon, MagicMock(), "gemini-test")

        kq = giai.giai(
            de="C2H4 → C2H5OH",
            hoi_notebooklm=True,
            notebook_id="nb_test_123",
            account_id=1,
            ghi_thu_vien=False
        )
        assert kq is not None
        # Mục tra cứu có chứa đoạn từ NotebookLM
        assert any("NotebookLM" in m.tieu_de for m in kq.muc)
