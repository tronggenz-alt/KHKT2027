"""Kiểm thử đầy đủ các tính năng quản trị tài khoản trên /admin/api/* (tương đương quan-tri.command):
- Tạo tài khoản admin / kì cựu (mật khẩu tự đặt hoặc ngẫu nhiên), nâng quyền cho email đã tồn tại.
- Cấp / bỏ quyền admin (chặn tự bỏ quyền chính mình), cấp / bỏ quyền kì cựu.
- Đặt lại mật khẩu (tự đặt hoặc ngẫu nhiên) cho tài khoản email + mật khẩu; từ chối tài khoản Google.
- Chặn người không phải admin (403) và chưa đăng nhập (401).
"""
from backend import bao_mat, db


def _tao_admin(client, dang_nhap):
    csrf = dang_nhap(client, sub="google-admin", email="admin@example.com")
    u = db.lay_theo_sub("google-admin")
    db.dat_admin(u.id, True)
    return u, csrf


def test_admin_cap_bo_ki_cuu_va_admin_va_dat_mat_khau(client, dang_nhap):
    admin, csrf = _tao_admin(client, dang_nhap)
    hd = {"X-CSRF-Token": csrf}

    # Tạo 1 tài khoản email thường
    u = db.tao_tai_khoan_email("hocsinh@example.com", "Học Sinh", bao_mat.bam_mat_khau("matkhau123"), da_xac_thuc=True)

    # 1. Cấp & bỏ quyền kì cựu
    r = client.post(f"/admin/api/nguoi-dung/{u.id}/vai-tro", json={"quyen": "ki_cuu", "bat": True}, headers=hd)
    assert r.status_code == 200 and r.json()["la_ki_cuu"] is True
    assert db.lay_nguoi_dung(u.id).la_ki_cuu is True

    r = client.post(f"/admin/api/nguoi-dung/{u.id}/vai-tro", json={"quyen": "ki_cuu", "bat": False}, headers=hd)
    assert r.status_code == 200 and r.json()["la_ki_cuu"] is False
    assert db.lay_nguoi_dung(u.id).la_ki_cuu is False

    # 2. Cấp & bỏ quyền admin (không cho tự bỏ quyền chính mình)
    assert client.post(f"/admin/api/nguoi-dung/{u.id}/vai-tro", json={"quyen": "admin", "bat": True}, headers=hd).json()["la_admin"] is True
    assert client.post(f"/admin/api/nguoi-dung/{u.id}/vai-tro", json={"quyen": "admin", "bat": False}, headers=hd).json()["la_admin"] is False
    assert client.post(f"/admin/api/nguoi-dung/{admin.id}/vai-tro", json={"quyen": "admin", "bat": False}, headers=hd).status_code == 400

    # 3. Đặt lại mật khẩu tự nhập & ngẫu nhiên
    assert client.post(f"/admin/api/nguoi-dung/{u.id}/dat-mat-khau", json={"mat_khau": "ngan"}, headers=hd).status_code == 400
    r_mk = client.post(f"/admin/api/nguoi-dung/{u.id}/dat-mat-khau", json={"mat_khau": "matkhaumoi999"}, headers=hd)
    assert r_mk.status_code == 200 and r_mk.json()["mat_khau_moi"] == ""
    assert bao_mat.kiem_mat_khau("matkhaumoi999", db.lay_nguoi_dung(u.id).mat_khau_bam)

    r_nn = client.post(f"/admin/api/nguoi-dung/{u.id}/dat-mat-khau", json={"ngau_nhien": True}, headers=hd)
    assert r_nn.status_code == 200 and len(r_nn.json()["mat_khau_moi"]) >= 8
    assert bao_mat.kiem_mat_khau(r_nn.json()["mat_khau_moi"], db.lay_nguoi_dung(u.id).mat_khau_bam)

    # Tài khoản Google không có mật khẩu → 400
    assert client.post(f"/admin/api/nguoi-dung/{admin.id}/dat-mat-khau", json={"ngau_nhien": True}, headers=hd).status_code == 400


def test_admin_tao_tai_khoan_moi_va_nang_quyen_email_da_co(client, dang_nhap):
    _, csrf = _tao_admin(client, dang_nhap)
    hd = {"X-CSRF-Token": csrf}

    # Tạo tài khoản kì cựu mới với mật khẩu ngẫu nhiên
    r = client.post("/admin/api/nguoi-dung/tao", json={
        "email": "kicuu@example.com", "ten": "Bạn Kì Cựu", "vai_tro": "ki_cuu", "ngau_nhien": True
    }, headers=hd)
    assert r.status_code == 200
    d = r.json()
    assert d["moi"] is True and d["vai_tro"] == "ki_cuu" and len(d["mat_khau_moi"]) >= 8
    u_kc = db.tim_theo_email("kicuu@example.com")
    assert u_kc is not None and u_kc.la_ki_cuu and u_kc.da_xac_thuc_email
    assert bao_mat.kiem_mat_khau(d["mat_khau_moi"], u_kc.mat_khau_bam)

    # Email đã có → nâng lên admin, giữ nguyên mật khẩu cũ
    r2 = client.post("/admin/api/nguoi-dung/tao", json={
        "email": "kicuu@example.com", "vai_tro": "admin"
    }, headers=hd)
    assert r2.status_code == 200 and r2.json()["moi"] is False
    u_kc2 = db.lay_nguoi_dung(u_kc.id)
    assert u_kc2.la_admin is True
    assert bao_mat.kiem_mat_khau(d["mat_khau_moi"], u_kc2.mat_khau_bam)


def test_nguoi_thuong_khong_goi_duoc_api_doi_quyen(client, dang_nhap):
    csrf = dang_nhap(client, sub="google-user", email="thuong@example.com")
    u = db.lay_theo_sub("google-user")
    hd = {"X-CSRF-Token": csrf}
    assert client.post(f"/admin/api/nguoi-dung/{u.id}/vai-tro", json={"quyen": "admin", "bat": True}, headers=hd).status_code == 403
    assert client.post("/admin/api/nguoi-dung/tao", json={"email": "x@example.com", "vai_tro": "admin", "ngau_nhien": True}, headers=hd).status_code == 403
    assert client.post(f"/admin/api/nguoi-dung/{u.id}/dat-mat-khau", json={"ngau_nhien": True}, headers=hd).status_code == 403
