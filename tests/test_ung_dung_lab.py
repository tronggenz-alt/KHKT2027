"""Kiểm thử cấu hình và phân quyền ứng dụng máy tính PhongLabHoa3D (GĐ6).
- Mặc định tắt (bat=False), không lộ link tải về.
- Phân quyền: chưa đăng nhập -> 401; học sinh gọi admin API -> 403; thiếu CSRF -> 403.
- Khi admin bật kèm CSRF -> ghi nhật ký hoạt động, học sinh nhận được link tải và phiên bản.
- Khi tắt -> link tải bị ẩn hoàn toàn đối với học sinh.
"""
import json
import pytest
from backend import db, nhat_ky, phong_thi_nghiem


def _admin(client, dang_nhap):
    csrf = dang_nhap(client, "app-admin", "app.admin@vi-du.vn")
    db.dat_admin(db.tim_theo_email("app.admin@vi-du.vn").id, True)
    return {"X-CSRF-Token": csrf}


def test_ung_dung_lab_mac_dinh_tat_va_phan_quyen(client, dang_nhap):
    # Chưa đăng nhập
    assert client.get("/admin/api/ung-dung-lab").status_code == 401
    assert client.get("/api/phong-thi-nghiem/ung-dung").status_code == 401

    # Học sinh bình thường
    csrf = dang_nhap(client, "app-hs", "app.hs@vi-du.vn")
    assert client.get("/admin/api/ung-dung-lab").status_code == 403
    assert client.post("/admin/api/ung-dung-lab", headers={"X-CSRF-Token": csrf},
                       json={"bat": True, "url_mac": "https://example.com/mac.zip"}).status_code == 403

    # Học sinh xem trạng thái ứng dụng khi đang tắt: không lộ link
    ch = client.get("/api/phong-thi-nghiem/ung-dung").json()
    assert ch["bat"] is False
    assert ch["url_mac"] == ""
    assert ch["url_win"] == ""


def test_admin_bat_ung_dung_lab_va_hoc_sinh_nhan_duoc(client, dang_nhap):
    client.cookies.clear()
    h = _admin(client, dang_nhap)

    # Xem trạng thái admin
    d = client.get("/admin/api/ung-dung-lab").json()
    assert d["bat"] is False

    # Gọi thiếu CSRF -> 403
    assert client.post("/admin/api/ung-dung-lab", json={"bat": True}).status_code == 403

    # Admin bật hợp lệ
    du_lieu = {
        "bat": True,
        "url_mac": "https://github.com/org/repo/releases/download/v1.0.0/PhongLabHoa3D-mac.zip",
        "url_win": "https://github.com/org/repo/releases/download/v1.0.0/PhongLabHoa3D-win.zip",
        "phien_ban": "1.0.0"
    }
    r = client.post("/admin/api/ung-dung-lab", headers=h, json=du_lieu)
    assert r.status_code == 200
    assert r.json()["bat"] is True
    assert r.json()["url_mac"] == du_lieu["url_mac"]

    # Kiểm tra lưu bền trong db và nhật ký
    luu = json.loads(db.cai_dat(phong_thi_nghiem.KHOA_UNG_DUNG))
    assert luu["bat"] is True
    assert luu["phien_ban"] == "1.0.0"

    with db.ket_noi() as con:
        dem = con.execute("SELECT COUNT(*) FROM activity_log WHERE event = 'doi_ung_dung_lab'").fetchone()[0]
        assert dem >= 1

    # Học sinh xem: bây giờ nhận được link tải
    client.cookies.clear()
    dang_nhap(client, "app-hs2", "app.hs2@vi-du.vn")
    ch = client.get("/api/phong-thi-nghiem/ung-dung").json()
    assert ch["bat"] is True
    assert ch["url_mac"] == du_lieu["url_mac"]
    assert ch["phien_ban"] == "1.0.0"

    # Admin tắt lại
    client.cookies.clear()
    h = _admin(client, dang_nhap)
    r_tat = client.post("/admin/api/ung-dung-lab", headers=h, json={"bat": False, "url_mac": "", "url_win": "", "phien_ban": "1.0.0"})
    assert r_tat.status_code == 200
    assert r_tat.json()["bat"] is False

    # Học sinh xem lại: link bị ẩn
    client.cookies.clear()
    dang_nhap(client, "app-hs2", "app.hs2@vi-du.vn")
    ch_sau = client.get("/api/phong-thi-nghiem/ung-dung").json()
    assert ch_sau["bat"] is False
    assert ch_sau["url_mac"] == ""
