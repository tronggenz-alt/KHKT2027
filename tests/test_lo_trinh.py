"""Lộ trình học Sinh (/admin → Lộ trình học): dữ liệu config/lo_trinh_sinh_hoc.json hợp lệ, chỉ admin xem / đánh dấu,
cần CSRF, tiến độ % đúng và lưu riêng theo từng tài khoản admin."""
import json

from backend import db, lo_trinh

LT = json.loads(lo_trinh.TEP_LO_TRINH.read_text(encoding="utf-8"))
CO_QUAN = {c["id"] for c in json.loads(lo_trinh.TEP_CO_QUAN.read_text(encoding="utf-8"))["co_quan"]}
TONG = sum(len(ch["bai"]) for ch in LT["chuong"])


def test_du_lieu_hop_le():
    ids_chuong = [ch["id"] for ch in LT["chuong"]]
    ids_bai = [b["id"] for ch in LT["chuong"] for b in ch["bai"]]
    assert len(ids_chuong) == len(set(ids_chuong)) and len(ids_bai) == len(set(ids_bai)), "trùng id"
    assert TONG >= 10
    for ch in LT["chuong"]:
        assert ch["ten"].strip() and ch["bai"]
        for b in ch["bai"]:
            assert b["ten"].strip() and b["id"].replace("-", "").isalnum()
            assert set(b["co_quan"]) <= CO_QUAN, f"{b['id']}: cơ quan không có trong danh-muc.json"


def _admin(client, dang_nhap, sub="lt-admin", email="lt.admin@vi-du.vn"):
    csrf = dang_nhap(client, sub, email)
    db.dat_admin(db.tim_theo_email(email).id, True)
    return {"X-CSRF-Token": csrf}


def test_xem_va_danh_dau(client, dang_nhap):
    h = _admin(client, dang_nhap)
    d = client.get("/admin/api/lo-trinh").json()
    assert d["tong"] == TONG and d["xong"] == 0 and d["phan_tram"] == 0
    tim = next(b for ch in d["chuong"] for b in ch["bai"] if b["id"] == "tim-hoat-dong")
    assert tim["xong"] is False and tim["co_quan"] == [{"id": "tim", "ten": "Tim", "icon": "🫀"}]

    d = client.post("/admin/api/lo-trinh/tim-hoat-dong", headers=h, json={"xong": True}).json()
    assert d["xong"] == 1 and d["phan_tram"] == round(100 / TONG)
    c1 = next(ch for ch in d["chuong"] if ch["id"] == "c1")
    assert c1["xong"] == 1 and next(b for b in c1["bai"] if b["id"] == "tim-hoat-dong")["xong_luc"]
    # bấm lại "xong" lần nữa không nhân đôi; bỏ tích thì trở về 0
    assert client.post("/admin/api/lo-trinh/tim-hoat-dong", headers=h, json={"xong": True}).json()["xong"] == 1
    assert client.post("/admin/api/lo-trinh/tim-hoat-dong", headers=h, json={"xong": False}).json()["xong"] == 0

    r = client.post("/admin/api/lo-trinh/khong-co-bai-nay", headers=h, json={"xong": True})
    assert r.status_code == 404 and r.json()["ma"] == "khong_co_bai"
    assert client.post("/admin/api/lo-trinh/tim-hoat-dong", json={"xong": True}).status_code == 403   # thiếu CSRF


def test_tien_do_rieng_tung_admin_va_du_100(client, dang_nhap):
    h = _admin(client, dang_nhap)
    for ch in LT["chuong"]:
        for b in ch["bai"]:
            client.post(f"/admin/api/lo-trinh/{b['id']}", headers=h, json={"xong": True})
    assert client.get("/admin/api/lo-trinh").json()["phan_tram"] == 100
    client.cookies.clear()
    _admin(client, dang_nhap, "lt-admin-2", "lt.admin2@vi-du.vn")
    assert client.get("/admin/api/lo-trinh").json()["xong"] == 0


def test_chi_admin(client, dang_nhap):
    csrf = dang_nhap(client, "lt-hs", "lt.hoc.sinh@vi-du.vn")
    assert client.get("/admin/api/lo-trinh").status_code == 403
    assert client.post("/admin/api/lo-trinh/tim-hoat-dong", headers={"X-CSRF-Token": csrf}, json={"xong": True}).status_code == 403
    client.cookies.clear()
    assert client.get("/admin/api/lo-trinh").status_code == 401
