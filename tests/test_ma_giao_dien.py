"""Mã giao diện (nút +K): admin cấp mã (CSDL chỉ lưu HMAC), POST /api/theme/redeem → themeId; lỗi sai / hết hạn / hết lượt / thu hồi;
mỗi tài khoản 1 lượt; khoá sau 5 lần sai; mã mẫu SAKURA2026; không có mã nào trong code frontend."""
import sqlite3
from pathlib import Path

import admin_cli
from backend import db, ma_giao_dien as gd


def _h(c):
    return {"X-CSRF-Token": c.get("/api/toi").json()["csrf"]}


def _doi(c, ma):
    return c.post("/api/theme/redeem", json={"code": ma}, headers=_h(c))


def test_admin_tao_ma_chi_luu_hmac_va_doi_ma(client, dang_nhap, csdl_tam):
    dang_nhap(client, "admin-1", "admin@example.com")
    db.dat_admin(db.tim_theo_email("admin@example.com").id, True)
    h = _h(client)
    assert client.post("/admin/api/ma-giao-dien", json={"theme_id": "khong-co"}, headers=h).status_code == 400
    d = client.post("/admin/api/ma-giao-dien", json={"theme_id": "sakura", "ma": "Hoa-Xuan 2027", "so_luot": 3, "ghi_chu": "lớp 12A"}, headers=h).json()
    assert d["ma_goc"] == "Hoa-Xuan 2027" and d["ma"][0]["goi_y"] == "HOA…27" and d["ma"][0]["toi_da"] == 3
    assert d["ma"][0]["ma"] == "Hoa-Xuan 2027"  # admin xem lại / chép được mã để đưa người khác
    ngau_nhien = client.post("/admin/api/ma-giao-dien", json={"theme_id": "sakura"}, headers=h).json()["ma_goc"]
    assert ngau_nhien.startswith("GD-") and len(ngau_nhien) == 12
    assert client.post("/admin/api/ma-giao-dien", json={"theme_id": "sakura", "ma": "hoaxuan2027"}, headers=h).status_code == 400  # trùng
    assert [x["ma"] for x in client.get("/admin/api/ma-giao-dien").json()["ma"]] == [ngau_nhien, "Hoa-Xuan 2027"]
    with sqlite3.connect(csdl_tam) as con:
        bang = " ".join(str(x) for r in con.execute("SELECT * FROM theme_codes") for x in r)
        nk = " ".join(str(x) for r in con.execute("SELECT * FROM activity_log") for x in r)
    assert "HOAXUAN" not in bang.upper() and "Hoa-Xuan" not in bang and "Hoa-Xuan" not in nk and ngau_nhien not in bang  # HMAC + bản mã hoá, nhật ký không có mã
    # mã 3 lượt: admin + 2 người dùng → người thứ 4 hết lượt; nhập lại không tốn lượt
    r = _doi(client, "hoaxuan2027")
    assert r.status_code == 200 and r.json()["themeId"] == "sakura"
    for sub, email in (("hs-1", "a@example.com"), ("hs-2", "b@example.com")):
        dang_nhap(client, sub, email)
        assert _doi(client, " hoa-xuan-2027 ").json()["themeId"] == "sakura"
    assert _doi(client, "HOAXUAN2027").status_code == 200  # nhập lại: vẫn được, không tốn lượt
    dang_nhap(client, "hs-3", "c@example.com")
    r = _doi(client, "HOAXUAN2027")
    assert r.status_code == 400 and r.json()["ma"] == "het_luot" and "hết lượt" in r.json()["loi"]


def test_loi_sai_het_han_thu_hoi_va_khoa(client, dang_nhap):
    gd.tao_ma("sakura", "CU2020", het_han="2020-01-01")
    _, ma_id = gd.tao_ma("sakura", "BITHUHOI")
    gd.thu_hoi(ma_id)
    dang_nhap(client, "hs-1", "a@example.com")
    assert client.post("/api/theme/redeem", json={"code": "CU2020"}).status_code == 403  # thiếu CSRF
    r = _doi(client, "CU2020")
    assert r.status_code == 400 and r.json()["ma"] == "het_han" and "2020-01-01" in r.json()["loi"]
    assert _doi(client, "bithuhoi").json()["ma"] == "thu_hoi"
    assert _doi(client, "").json()["ma"] == "trong"
    for i in range(5):
        r = _doi(client, f"SAI{i}XX")
        assert r.status_code == 400 and r.json()["ma"] == "sai"
    r = _doi(client, "CU2020")
    assert r.status_code == 429 and r.json()["ma"] == "bi_khoa" and r.headers["Retry-After"]


def test_nguoi_thuong_khong_xem_duoc_danh_sach_ma(client, dang_nhap):
    gd.tao_ma("sakura", "BIMAT2026")
    dang_nhap(client, "hs-1", "a@example.com")
    assert client.get("/admin/api/ma-giao-dien").status_code == 403
    assert "BIMAT2026" not in client.get("/api/toi").text


def test_ma_mau_cu_duoc_dien_ban_ma_hoa():
    gd.tao_ma_mau()
    with db.ket_noi() as con:
        con.execute("UPDATE theme_codes SET code_enc = ''")  # giả lập mã mẫu tạo từ bản trước (chưa có bản mã hoá)
    assert gd.danh_sach()[0]["ma"] == ""
    gd.tao_ma_mau()  # lần khởi động sau: điền bổ sung
    assert gd.danh_sach()[0]["ma"] == gd.MA_MAU


def test_chua_dang_nhap_khong_doi_duoc(client):
    assert client.post("/api/theme/redeem", json={"code": "X"}, headers=_h(client)).status_code == 401


def test_ma_mau_sakura_tao_mot_lan(client, dang_nhap):
    assert gd.tao_ma_mau() is True and gd.tao_ma_mau() is False
    assert len([x for x in gd.danh_sach() if x["theme_id"] == "sakura"]) == 1
    dang_nhap(client, "hs-1", "a@example.com")
    assert _doi(client, "sakura2026").json() == {"ok": True, "themeId": "sakura", "ten": "Hoa anh đào"}


def test_khong_co_ma_trong_code_frontend():
    for tep in Path("frontend").rglob("*"):
        if tep.suffix in (".js", ".html", ".css"):
            assert gd.MA_MAU not in tep.read_text(encoding="utf-8", errors="ignore").upper(), tep


def test_cli_theme(client, capsys):
    db.danh_sach_nguoi_dung()
    assert admin_cli.main(["theme", "create", "--theme", "sakura", "--ma", "CLI2026", "--luot", "5", "--het-han", "2030-12-31"]) == 0
    assert "CLI2026" in capsys.readouterr().out
    assert admin_cli.main(["theme", "list"]) == 0 and "Hoa anh đào" in capsys.readouterr().out
    assert admin_cli.main(["theme", "revoke", "cli2026"]) == 0 and gd.danh_sach()[0]["trang_thai"] == "thu_hoi"
    assert admin_cli.main(["theme", "revoke", "cli2026"]) == 1
