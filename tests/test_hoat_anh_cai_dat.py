"""/admin → Cài đặt chung: công tắc "Hoạt ảnh Cài đặt" (lưu CSDL, áp dụng ngay qua /api/toi, chỉ admin đổi được)."""
from pathlib import Path

from test_chon_mo_hinh import _dang_nhap_email

GOC = Path(__file__).resolve().parent.parent / "frontend"


def test_cong_tac_hoat_anh_cai_dat(cau_hinh, client, monkeypatch):
    hs = _dang_nhap_email(monkeypatch, client, "hs@example.com")
    assert hs.get("/api/toi").json()["hoat_anh_cai_dat"] is True  # mặc định bật
    assert hs.post("/admin/api/cai-dat-chung", json={"hoat_anh_cai_dat": False}, headers=hs.h).status_code == 403
    ad = _dang_nhap_email(monkeypatch, client, "admin@example.com", admin=True)
    assert ad.post("/admin/api/cai-dat-chung", json={"hoat_anh_cai_dat": False}, headers=ad.h).json() == {"hoat_anh_cai_dat": False}
    assert hs.get("/api/toi").json()["hoat_anh_cai_dat"] is False  # áp dụng ngay, không cần khởi động lại
    assert ad.get("/admin/api/cai-dat-chung").json()["hoat_anh_cai_dat"] is False
    ad.post("/admin/api/cai-dat-chung", json={"hoat_anh_cai_dat": True}, headers=ad.h)
    assert hs.get("/api/toi").json()["hoat_anh_cai_dat"] is True


def test_css_ton_trong_giam_chuyen_dong_va_khong_them_thu_vien():
    css = (GOC / "style.css").read_text(encoding="utf-8")
    assert "prefers-reduced-motion: reduce" in css and "@keyframes ha-quet" in css and "@keyframes ha-vien" in css
    html = (GOC / "cai-dat.html").read_text(encoding="utf-8")
    assert "cho-hoat-anh" in html and html.count("<script src=") == 4  # tailwind + tai-khoan + hoat-dong + cai-dat: không thêm thư viện
