"""Kiểm thử đăng nhập và phân quyền: mật khẩu, phiên, giới hạn sai, quyền từng đường dẫn, lối riêng của Unity."""

import json
import os
import sqlite3
import tempfile
import unittest
from pathlib import Path
from unittest import mock

from argon2 import PasswordHasher
from fastapi.testclient import TestClient
from starlette.routing import Mount

from server.ai import main
from server.lab import dang_nhap, db, quan_tri, sinh_db, tai_khoan

MK_GV, MK_HS = "mat-khau-giao-vien", "mat-khau-hoc-sinh"

CONG_KHAI = {("POST", "/auth/dang-nhap"), ("POST", "/auth/dang-ky"), ("POST", "/auth/dang-xuat"), ("GET", "/dang-nhap"), ("GET", "/suc-khoe")}
UNITY = {("POST", "/chu-thich"), ("POST", "/tro-ly"), ("POST", "/robot/ke-hoach"), ("POST", "/api/nhat-ky"), ("POST", "/lab/sinh/su-kien"), ("GET", "/lab/sinh/tong-quan")}
HOC_SINH_DUOC = CONG_KHAI | UNITY | {
    ("GET", "/"), ("GET", "/auth/toi"), ("POST", "/auth/doi-mat-khau"), ("GET", "/api/toi/ket-qua"), ("POST", "/app/nhip"), ("GET", "/api/bai-giang/{bai_id}/trang/{so}"), ("GET", "/hoc-sinh"), ("GET", "/may"), ("GET", "/may/{ma}"),
    ("GET", "/the-thong-tin/{phan_ung_id}"), ("POST", "/hoi"), ("GET", "/lab/sinh/huong-dan"), ("GET", "/lab/sinh/huong-dan/{ma}"),
    ("GET", "/lab/sinh/bai-thi-nghiem"), ("GET", "/lab/sinh/dat-lich"),
}
TU_CHUYEN_HUONG = {"/", "/hoc-sinh"}  # hai trang tự đưa về trang chủ theo vai trò, kiểm tra riêng ở test_trang_chuyen_huong_theo_quyen


def moi_duong_dan():
    """(phương thức, mẫu đường dẫn) của mọi điểm cuối trong ứng dụng, kể cả /docs, /openapi.json."""
    for r in main.app.routes:
        if isinstance(r, Mount):
            continue
        for m in r.methods - {"HEAD"}:
            yield m, r.path


def dien_tham_so(mau: str) -> str:
    return mau.replace("{ma}", "khong_co").replace("{phan_ung_id}", "khong_co").replace("{id}", "999").replace("{stt}", "999").replace("{key_id}", "999").replace("{lop_id}", "999").replace("{hs_id}", "999").replace("{bai_id}", "999").replace("{so}", "999")


class NenDangNhap(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.db_path = Path(self.tmp.name) / "lab.db"
        db.reset_for_tests(self.db_path)
        sinh_db.reset_for_tests(self.db_path)
        self._bam = tai_khoan._bam
        tai_khoan._bam = PasswordHasher(time_cost=1, memory_cost=1024, parallelism=1)  # băm nhẹ cho kiểm thử chạy nhanh
        self.gv = tai_khoan.tao_nguoi_dung("gv.an", "Giáo viên An", "giao_vien", MK_GV)
        self.hs = tai_khoan.tao_nguoi_dung("hs.binh", "Học sinh Bình", "hoc_sinh", MK_HS)
        self.lop = tai_khoan.tao_lop("10A1", "2026-2027", self.gv["id"])
        tai_khoan.them_vao_lop(self.lop["id"], self.hs["id"])
        self.web = self.tao_client()

    def tearDown(self):
        tai_khoan._bam = self._bam
        db.reset_for_tests(Path(self.tmp.name) / "khac.db")
        sinh_db.reset_for_tests(Path(self.tmp.name) / "khac.db")
        self.tmp.cleanup()

    def tao_client(self, ip="192.168.1.50", base="http://192.168.1.12:8000"):
        return TestClient(main.app, base_url=base, client=(ip, 50000), follow_redirects=False, raise_server_exceptions=False)

    def may_chu(self):
        """Máy khách chạy cùng máy với máy chủ, như ứng dụng Unity."""
        return self.tao_client("127.0.0.1", "http://127.0.0.1:8000")

    def dang_nhap(self, client, ten, mk, **them):
        return client.post("/auth/dang-nhap", json={"ten_dang_nhap": ten, "mat_khau": mk, **them})

    def bearer(self, ten, mk):
        r = self.dang_nhap(self.tao_client(), ten, mk, kieu="ung_dung")
        self.assertEqual(r.status_code, 200, r.text)
        return {"Authorization": "Bearer " + r.json()["ma_phien"]}


class MatKhauTest(NenDangNhap):
    def test_bam_argon2id_khong_luu_tho(self):
        row = db.query("SELECT mat_khau_bam FROM nguoi_dung WHERE id=?", (self.hs["id"],))[0]
        self.assertTrue(row["mat_khau_bam"].startswith("$argon2id$"))
        self.assertNotIn(MK_HS, row["mat_khau_bam"])
        self.assertNotIn(MK_HS.encode(), self.db_path.read_bytes())

    def test_tham_so_bam_mac_dinh_du_manh(self):
        self.assertGreaterEqual(self._bam.memory_cost, 19456)  # mức tối thiểu OWASP cho Argon2id: 19 MiB

    def test_cung_mat_khau_khac_ma_bam(self):
        hs2 = tai_khoan.tao_nguoi_dung("hs.cuong", "Học sinh Cường", "hoc_sinh", MK_HS)
        rows = db.query("SELECT mat_khau_bam FROM nguoi_dung WHERE id IN (?,?)", (self.hs["id"], hs2["id"]))
        self.assertNotEqual(rows[0]["mat_khau_bam"], rows[1]["mat_khau_bam"])

    def test_api_khong_tra_ma_bam(self):
        for r in (self.dang_nhap(self.web, "hs.binh", MK_HS), self.web.get("/auth/toi")):
            self.assertNotIn("argon2", r.text)
            self.assertNotIn("mat_khau", r.text.replace("phai_doi_mat_khau", ""))

    def test_mat_khau_yeu_bi_tu_choi(self):
        for ten, mk in (("hs.a1", "ngan"), ("hs.a123456", "hs.a123456"), ("hs.a2", "x" * 129)):
            with self.assertRaises(tai_khoan.LoiTaiKhoan):
                tai_khoan.tao_nguoi_dung(ten, "Học sinh", "hoc_sinh", mk)

    def test_ten_trung_khong_phan_biet_hoa_thuong(self):
        with self.assertRaises(tai_khoan.TenDaCo):
            tai_khoan.tao_nguoi_dung("HS.Binh", "Người khác", "hoc_sinh", MK_HS)


class DangNhapWebTest(NenDangNhap):
    def test_ca_hai_vai_tro_dang_nhap_dang_xuat(self):
        for ten, mk, vai_tro in (("gv.an", MK_GV, "giao_vien"), ("hs.binh", MK_HS, "hoc_sinh")):
            with self.subTest(vai_tro):
                c = self.tao_client()
                self.assertEqual(c.get("/auth/toi").status_code, 401)
                r = self.dang_nhap(c, ten, mk)
                self.assertEqual(r.status_code, 200)
                self.assertEqual(r.json()["nguoi_dung"]["vai_tro"], vai_tro)
                self.assertEqual(c.get("/auth/toi").json()["ten_dang_nhap"], ten)
                self.assertEqual(c.post("/auth/dang-xuat").status_code, 200)
                self.assertEqual(c.get("/auth/toi").status_code, 401)

    def test_dang_xuat_huy_phien_tren_may_chu(self):
        self.dang_nhap(self.web, "hs.binh", MK_HS)
        ma = self.web.cookies.get(dang_nhap.COOKIE)
        self.web.post("/auth/dang-xuat")
        self.assertEqual(db.query("SELECT COUNT(*) AS n FROM phien")[0]["n"], 0)
        c = self.tao_client()
        c.cookies.set(dang_nhap.COOKIE, ma)  # giữ lại cookie cũ vẫn không vào được
        self.assertEqual(c.get("/auth/toi").status_code, 401)

    def test_cookie_httponly_samesite(self):
        tieu_de = self.dang_nhap(self.web, "hs.binh", MK_HS).headers["set-cookie"].lower()
        self.assertIn("httponly", tieu_de)
        self.assertIn("samesite=lax", tieu_de)
        self.assertNotIn("secure", tieu_de)  # LAN chạy HTTP
        tieu_de = self.dang_nhap(self.tao_client(base="https://vera.example"), "hs.binh", MK_HS).headers["set-cookie"].lower()
        self.assertIn("secure", tieu_de)

    def test_web_khong_nhan_ma_phien_trong_noi_dung(self):
        r = self.dang_nhap(self.web, "hs.binh", MK_HS)
        self.assertNotIn("ma_phien", r.json())

    def test_csdl_chi_giu_ban_bam_cua_ma_phien(self):
        self.dang_nhap(self.web, "hs.binh", MK_HS)
        ma = self.web.cookies.get(dang_nhap.COOKIE)
        self.assertNotEqual(db.query("SELECT ma_bam FROM phien")[0]["ma_bam"], ma)
        self.assertNotIn(ma.encode(), self.db_path.read_bytes())

    def test_sai_mat_khau_va_sai_ten_bao_giong_nhau(self):
        a = self.dang_nhap(self.web, "hs.binh", "mat-khau-sai")
        b = self.dang_nhap(self.web, "khong.co.ai", "mat-khau-sai")
        self.assertEqual((a.status_code, a.json()), (b.status_code, b.json()))
        self.assertEqual(a.status_code, 401)
        self.assertIsNone(self.web.cookies.get(dang_nhap.COOKIE))

    def test_phien_het_han(self):
        self.dang_nhap(self.web, "hs.binh", MK_HS)
        db.execute("UPDATE phien SET het_han=1")
        self.assertEqual(self.web.get("/auth/toi").status_code, 401)

    def test_trang_chuyen_huong_theo_quyen(self):
        r = self.web.get("/")
        self.assertEqual((r.status_code, r.headers["location"]), (303, "/dang-nhap?tiep=%2F"))
        for trang in ("/giao-vien", "/hoa", "/sinh", "/hoc-sinh", "/docs"):
            self.assertEqual(self.web.get(trang).headers["location"], "/dang-nhap?tiep=" + trang.replace("/", "%2F"), trang)
        self.assertEqual(self.web.get("/dang-nhap").status_code, 200)
        self.dang_nhap(self.web, "hs.binh", MK_HS)
        for trang in ("/", "/giao-vien", "/hoa", "/sinh", "/docs"):
            r = self.web.get(trang)
            self.assertEqual((r.status_code, r.headers["location"]), (303, "/hoc-sinh"), trang)
        self.assertEqual(self.web.get("/hoc-sinh").status_code, 200)
        self.dang_nhap(self.web, "gv.an", MK_GV)
        for trang in ("/", "/hoc-sinh"):
            r = self.web.get(trang)
            self.assertEqual((r.status_code, r.headers["location"]), (303, "/giao-vien"), trang)
        for trang in ("/giao-vien", "/hoa", "/sinh", "/docs"):
            self.assertEqual(self.web.get(trang).status_code, 200, trang)

    def test_trang_sau_dang_nhap_deu_co_dang_xuat_va_xu_ly_het_phien(self):
        self.dang_nhap(self.web, "gv.an", MK_GV)
        for trang in ("/giao-vien", "/hoa", "/sinh"):
            r = self.web.get(trang)
            self.assertIn("/tinh/phien.js", r.text, trang)
            self.assertIn("data-dang-xuat", r.text, trang)
        c = self.tao_client()
        self.dang_nhap(c, "hs.binh", MK_HS)
        r = c.get("/hoc-sinh")
        self.assertIn("/tinh/phien.js", r.text)
        self.assertIn("data-dang-xuat", r.text)
        self.assertEqual(self.tao_client().get("/tinh/phien.js").status_code, 200)

    def test_hai_trang_cu_chi_con_giao_dien_sang(self):
        self.dang_nhap(self.web, "gv.an", MK_GV)
        for trang in ("/hoa", "/sinh"):
            r = self.web.get(trang)
            self.assertNotIn("prefers-color-scheme", r.text, trang)
            self.assertRegex(r.text, r"--accent: #0b63d1|/tinh/vera\.css", trang)  # màu riêng của trang, hoặc dùng chung vera.css sau khi thiết kế lại


class PhienUngDungTest(NenDangNhap):
    def test_nhan_ma_phien_khong_nhan_cookie(self):
        r = self.dang_nhap(self.web, "hs.binh", MK_HS, kieu="ung_dung")
        self.assertEqual(sorted(r.json()), ["het_han", "ma_phien", "nguoi_dung"])
        self.assertNotIn("set-cookie", r.headers)
        nd = r.json()["nguoi_dung"]
        self.assertEqual(sorted(nd), ["ho_ten", "id", "lop", "phai_doi_mat_khau", "ten_dang_nhap", "vai_tro"])
        self.assertIsInstance(nd["id"], int)
        self.assertEqual(nd["lop"][0]["ten"], "10A1")
        self.assertIsInstance(nd["lop"][0]["id"], int)

    def test_bearer_dung_duoc_va_dang_xuat_thu_hoi(self):
        h = self.bearer("hs.binh", MK_HS)
        c = self.tao_client()
        self.assertEqual(c.get("/auth/toi", headers=h).json()["ten_dang_nhap"], "hs.binh")
        c.post("/auth/dang-xuat", headers=h)
        self.assertEqual(c.get("/auth/toi", headers=h).status_code, 401)

    def test_ma_sai_tra_401(self):
        self.assertEqual(self.web.get("/auth/toi", headers={"Authorization": "Bearer ma-tu-che"}).status_code, 401)

    def test_han_truot_khi_dung(self):
        h = self.bearer("hs.binh", MK_HS)
        db.execute("UPDATE phien SET het_han=het_han-86400")
        cu = db.query("SELECT het_han FROM phien")[0]["het_han"]
        self.web.get("/auth/toi", headers=h)
        self.assertGreater(db.query("SELECT het_han FROM phien")[0]["het_han"], cu)

    def test_han_phien_web_khong_truot(self):
        self.dang_nhap(self.web, "hs.binh", MK_HS)
        db.execute("UPDATE phien SET het_han=het_han-3600")
        cu = db.query("SELECT het_han FROM phien")[0]["het_han"]
        self.web.get("/auth/toi")
        self.assertEqual(db.query("SELECT het_han FROM phien")[0]["het_han"], cu)


class GioiHanSaiTest(NenDangNhap):
    def test_sai_5_lan_bi_khoa_ca_khi_dung_mat_khau(self):
        for _ in range(tai_khoan.SAI_TOI_DA):
            self.assertEqual(self.dang_nhap(self.web, "hs.binh", "sai-mat-khau").status_code, 401)
        r = self.dang_nhap(self.web, "hs.binh", MK_HS)
        self.assertEqual(r.status_code, 429)
        self.assertGreater(int(r.headers["retry-after"]), 0)

    def test_may_khac_khong_bi_khoa(self):
        for _ in range(tai_khoan.SAI_TOI_DA):
            self.dang_nhap(self.web, "hs.binh", "sai-mat-khau")
        self.assertEqual(self.dang_nhap(self.tao_client("192.168.1.51"), "hs.binh", MK_HS).status_code, 200)

    def test_het_thoi_gian_khoa_duoc_thu_lai(self):
        for _ in range(tai_khoan.SAI_TOI_DA):
            self.dang_nhap(self.web, "hs.binh", "sai-mat-khau")
        db.execute("UPDATE gioi_han_sai SET khoa_den=1")
        self.assertEqual(self.dang_nhap(self.web, "hs.binh", MK_HS).status_code, 200)

    def test_dang_nhap_dung_xoa_dem_sai(self):
        for _ in range(tai_khoan.SAI_TOI_DA - 1):
            self.dang_nhap(self.web, "hs.binh", "sai-mat-khau")
        self.dang_nhap(self.web, "hs.binh", MK_HS)
        self.assertEqual(self.dang_nhap(self.web, "hs.binh", "sai-mat-khau").status_code, 401)

    def test_khong_luu_chu_nguoi_dung_go(self):
        self.dang_nhap(self.web, "mat-khau-go-nham-o", "x")
        self.assertNotIn(b"mat-khau-go-nham-o", self.db_path.read_bytes())


class DoiMatKhauTest(NenDangNhap):
    def doi(self, client, cu, moi, **kw):
        return client.post("/auth/doi-mat-khau", json={"mat_khau_cu": cu, "mat_khau_moi": moi}, **kw)

    def test_doi_xong_chi_mat_khau_moi_dung_duoc(self):
        self.dang_nhap(self.web, "hs.binh", MK_HS)
        self.assertEqual(self.doi(self.web, MK_HS, "mat-khau-moi-123").status_code, 200)
        self.assertEqual(self.dang_nhap(self.tao_client(), "hs.binh", MK_HS).status_code, 401)
        self.assertEqual(self.dang_nhap(self.tao_client(), "hs.binh", "mat-khau-moi-123").status_code, 200)

    def test_sai_mat_khau_cu_tra_400_khong_phai_het_phien(self):
        self.dang_nhap(self.web, "hs.binh", MK_HS)
        self.assertEqual(self.doi(self.web, "sai-mat-khau", "mat-khau-moi-123").status_code, 400)
        self.assertEqual(self.web.get("/auth/toi").status_code, 200)

    def test_doi_mat_khau_thu_hoi_phien_khac_giu_phien_nay(self):
        khac = self.bearer("hs.binh", MK_HS)
        self.dang_nhap(self.web, "hs.binh", MK_HS)
        self.doi(self.web, MK_HS, "mat-khau-moi-123")
        self.assertEqual(self.web.get("/auth/toi").status_code, 200)
        self.assertEqual(self.tao_client().get("/auth/toi", headers=khac).status_code, 401)

    def test_doi_qua_bearer(self):
        h = self.bearer("hs.binh", MK_HS)
        self.assertEqual(self.doi(self.tao_client(), MK_HS, "mat-khau-moi-123", headers=h).status_code, 200)
        self.assertEqual(self.tao_client().get("/auth/toi", headers=h).status_code, 200)

    def test_mat_khau_moi_khong_hop_le(self):
        self.dang_nhap(self.web, "hs.binh", MK_HS)
        self.assertEqual(self.doi(self.web, MK_HS, "ngan").status_code, 422)
        self.assertEqual(self.doi(self.web, MK_HS, MK_HS).status_code, 422)

    def test_doan_mat_khau_cu_bi_khoa(self):
        self.dang_nhap(self.web, "hs.binh", MK_HS)
        for _ in range(tai_khoan.SAI_TOI_DA):
            self.doi(self.web, "sai-mat-khau", "mat-khau-moi-123")
        self.assertEqual(self.doi(self.web, MK_HS, "mat-khau-moi-123").status_code, 429)

    def test_mat_khau_tam_buoc_doi_truoc(self):
        tai_khoan.tao_nguoi_dung("hs.tam", "Học sinh Tạm", "hoc_sinh", "mat-khau-tam-1", phai_doi=True)
        r = self.dang_nhap(self.web, "hs.tam", "mat-khau-tam-1")
        self.assertTrue(r.json()["nguoi_dung"]["phai_doi_mat_khau"])
        self.assertEqual(self.web.get("/may").status_code, 403)
        self.assertTrue(self.web.get("/may").json()["phai_doi_mat_khau"])
        self.assertEqual(self.web.get("/hoc-sinh").headers["location"], "/dang-nhap")
        self.assertEqual(self.web.get("/auth/toi").status_code, 200)
        self.assertEqual(self.doi(self.web, "mat-khau-tam-1", "mat-khau-moi-123").status_code, 200)
        self.assertEqual(self.web.get("/may").status_code, 200)
        self.assertFalse(self.web.get("/auth/toi").json()["phai_doi_mat_khau"])


class DangKyMaLopTest(NenDangNhap):
    def dk(self, client=None, **sua):
        body = {"ma_lop": self.lop["ma_lop"], "ho_ten": "Học sinh Mới", "ten_dang_nhap": "hs.moi", "mat_khau": "mat-khau-moi-123", **sua}
        return (client or self.web).post("/auth/dang-ky", json=body)

    def test_dung_ma_tao_hoc_sinh_trong_lop_va_dang_nhap_luon(self):
        r = self.dk()
        self.assertEqual(r.status_code, 200, r.text)
        nd = r.json()["nguoi_dung"]
        self.assertEqual(nd["vai_tro"], "hoc_sinh")
        self.assertEqual([l["id"] for l in nd["lop"]], [self.lop["id"]])
        self.assertEqual(self.web.get("/auth/toi").json()["ten_dang_nhap"], "hs.moi")

    def test_ma_lop_khong_phan_biet_hoa_thuong(self):
        self.assertEqual(self.dk(ma_lop=self.lop["ma_lop"].lower()).status_code, 200)

    def test_ung_dung_nhan_ma_phien(self):
        r = self.dk(kieu="ung_dung")
        self.assertIn("ma_phien", r.json())
        self.assertNotIn("set-cookie", r.headers)

    def test_khong_tu_nang_quyen(self):
        r = self.dk(vai_tro="giao_vien")
        self.assertEqual(r.json()["nguoi_dung"]["vai_tro"], "hoc_sinh")
        self.assertEqual(self.web.get("/api/khoa").status_code, 403)

    def test_sai_ma_dong_ma_trung_ten(self):
        self.assertEqual(self.dk(ma_lop="SAIMA2").status_code, 403)
        self.assertEqual(self.dk(ten_dang_nhap="hs.binh").status_code, 409)
        self.assertEqual(self.dk(mat_khau="ngan").status_code, 422)
        db.execute("UPDATE lop SET ma_lop_mo=0")
        self.assertEqual(self.dk().status_code, 403)
        self.assertEqual(db.query("SELECT COUNT(*) AS n FROM nguoi_dung WHERE ten_dang_nhap='hs.moi'")[0]["n"], 0)

    def test_do_ma_lop_bi_khoa(self):
        for _ in range(10):
            self.assertEqual(self.dk(ma_lop="SAIMA2").status_code, 403)
        self.assertEqual(self.dk().status_code, 429)


class QuyenTest(NenDangNhap):
    def goi(self, client, m, mau, **kw):
        return client.request(m, dien_tham_so(mau), **kw)

    def test_chua_dang_nhap_chi_vao_duoc_duong_cong_khai(self):
        for m, mau in moi_duong_dan():
            with self.subTest(f"{m} {mau}"):
                r = self.goi(self.tao_client(), m, mau)
                if (m, mau) in CONG_KHAI:
                    self.assertNotIn(r.status_code, (401, 403, 303))
                else:
                    self.assertIn(r.status_code, (401, 303))

    def test_hoc_sinh_khong_vao_duoc_phan_giao_vien(self):
        h = self.bearer("hs.binh", MK_HS)
        for m, mau in moi_duong_dan():
            if mau in ("/auth/dang-xuat", "/"):  # dang-xuat sẽ hủy luôn phiên đang dùng để thử
                continue
            with self.subTest(f"{m} {mau}"):
                r = self.goi(self.tao_client(), m, mau, headers=h)
                if (m, mau) in HOC_SINH_DUOC:
                    self.assertNotIn(r.status_code, (401, 403, 303))
                else:
                    self.assertIn(r.status_code, (403, 303))

    def test_giao_vien_vao_duoc_moi_duong(self):
        h = self.bearer("gv.an", MK_GV)
        for m, mau in moi_duong_dan():
            if mau == "/auth/dang-xuat" or mau in TU_CHUYEN_HUONG:
                continue
            with self.subTest(f"{m} {mau}"):
                self.assertNotIn(self.goi(self.tao_client(), m, mau, headers=h).status_code, (401, 403, 303))

    def test_duong_dan_khong_ton_tai_cung_bi_chan(self):
        self.assertEqual(self.tao_client().get("/khong-co-duong-nay").status_code, 401)
        self.assertEqual(self.tao_client().get("/openapi.json").status_code, 401)
        self.assertEqual(self.tao_client().get("/khong-co-duong-nay", headers=self.bearer("hs.binh", MK_HS)).status_code, 403)

    def test_bang_quyen_khong_tu_rong_ra(self):
        khai_bao = {k for k, v in dang_nhap.QUYEN.items() if k != ("GET", "/tinh")}
        self.assertEqual(khai_bao, HOC_SINH_DUOC)

    def test_hoc_sinh_khong_sua_duoc_kho_va_diem(self):
        kq = sinh_db.add_ket_qua({"hoc_sinh": "Học sinh Bình", "lop": "10A1", "ma_bai": "b1", "diem": 5})
        truoc = db.query("SELECT so_luong FROM ton_kho WHERE stt=1")[0]["so_luong"]
        h = self.bearer("hs.binh", MK_HS)
        c = self.tao_client()
        self.assertEqual(c.patch(f"/lab/sinh/ket-qua/{kq['id']}", json={"diem": 10}, headers=h).status_code, 403)
        self.assertEqual(c.patch("/api/hoa-chat/1", json={"them": -5}, headers=h).status_code, 403)
        self.assertEqual(c.get("/api/khoa", headers=h).status_code, 403)
        self.assertEqual(c.get("/lab/sinh/ket-qua", headers=h).status_code, 403)
        self.assertEqual(sinh_db.get_ket_qua_by_id(kq["id"])["diem"], 5)
        self.assertEqual(db.query("SELECT so_luong FROM ton_kho WHERE stt=1")[0]["so_luong"], truoc)

    def test_trinh_duyet_khong_giu_trang_va_du_lieu(self):
        h = self.bearer("gv.an", MK_GV)
        c = self.tao_client()
        for r in (c.get("/", headers=h), c.get("/api/khoa", headers=h), c.get("/auth/toi", headers=h), c.get("/"), c.get("/api/khoa"), c.get("/dang-nhap")):
            self.assertEqual(r.headers.get("cache-control"), "no-store", r.request.url)
        self.assertEqual(c.get("/tinh/vera.css").headers.get("cache-control"), "no-cache")

    def test_tep_tinh_cong_khai_khong_lo_trang_quan_ly(self):
        self.assertEqual(self.tao_client().get("/tinh/vera.css").status_code, 200)
        self.assertIn(self.tao_client().get("/tinh/%2e%2e/index.html").status_code, (401, 404))
        self.assertIn(self.tao_client().get("/tinh/..%2findex.html").status_code, (401, 404))


class NguonYeuCauTest(NenDangNhap):
    def test_trang_khac_khong_ghi_duoc_bang_cookie(self):
        self.dang_nhap(self.web, "gv.an", MK_GV)
        r = self.web.patch("/api/hoa-chat/1", json={"them": 1}, headers={"Origin": "https://trang-la.example"})
        self.assertEqual(r.status_code, 403)
        r = self.web.patch("/api/hoa-chat/1", json={"them": 1}, headers={"Origin": "http://192.168.1.12:8000"})
        self.assertEqual(r.status_code, 200)

    def test_trang_khac_khong_dung_duoc_loi_cua_unity(self):
        r = self.may_chu().post("/chu-thich", json={"tieu_de": "x"}, headers={"Origin": "https://trang-la.example"})
        self.assertEqual(r.status_code, 403)

    def test_ung_dung_khong_gui_origin_van_chay(self):
        self.assertEqual(self.tao_client().get("/auth/toi", headers=self.bearer("hs.binh", MK_HS)).status_code, 200)


class LoiUnityTest(NenDangNhap):
    def setUp(self):
        super().setUp()
        self._pick = main.providers.pick
        main.providers.pick = lambda: None

    def tearDown(self):
        main.providers.pick = self._pick
        super().tearDown()

    def test_cung_may_goi_duoc_khong_can_dang_nhap(self):
        c = self.may_chu()
        self.assertEqual(c.post("/chu-thich", json={"tieu_de": "x", "chi_tiet": "y"}).status_code, 200)
        self.assertEqual(c.post("/tro-ly", json={"cau_hoi": "x"}).status_code, 200)
        self.assertEqual(c.post("/robot/ke-hoach", json={"cau_hoi": "x"}).status_code, 200)
        self.assertEqual(c.post("/api/nhat-ky", json={"tieu_de": "x"}).status_code, 200)
        self.assertEqual(c.post("/lab/sinh/su-kien", json={"su_kien": []}).status_code, 200)
        self.assertEqual(c.get("/lab/sinh/tong-quan").status_code, 200)
        self.assertEqual(c.get("/suc-khoe").json()["nha_cung_cap"], "khong")

    def test_cung_may_khong_vao_duoc_phan_khac(self):
        c = self.may_chu()
        for mau in ("/api/khoa", "/api/hoa-chat", "/lab/sinh/ket-qua", "/may", "/auth/toi"):
            self.assertEqual(c.get(mau).status_code, 401, mau)
        self.assertEqual(c.get("/").status_code, 303)

    def test_may_khac_khong_co_loi_nay(self):
        c = self.tao_client()
        self.assertEqual(c.post("/chu-thich", json={"tieu_de": "x"}).status_code, 401)
        self.assertEqual(c.post("/lab/sinh/su-kien", json={"su_kien": []}).status_code, 401)

    def test_tat_khi_mo_mang(self):
        with mock.patch.dict(os.environ, {"KHKT_MO_MANG": "1"}):
            self.assertEqual(self.may_chu().post("/chu-thich", json={"tieu_de": "x"}).status_code, 401)

    def test_tat_khi_qua_may_trung_gian(self):
        for h in ("X-Forwarded-For", "CF-Connecting-IP", "Forwarded"):
            self.assertEqual(self.may_chu().post("/chu-thich", json={"tieu_de": "x"}, headers={h: "203.0.113.9"}).status_code, 401, h)

    def test_chan_doi_ten_mien_dns_rebinding(self):
        c = self.tao_client("127.0.0.1", "http://trang-la.example:8000")
        self.assertEqual(c.post("/chu-thich", json={"tieu_de": "x"}).status_code, 401)

    def test_ma_phien_sai_khong_roi_xuong_loi_unity(self):
        r = self.may_chu().post("/chu-thich", json={"tieu_de": "x"}, headers={"Authorization": "Bearer het-han"})
        self.assertEqual(r.status_code, 401)

    def test_suc_khoe_khong_lo_nha_cung_cap_cho_nguoi_ngoai(self):
        main.providers.pick = lambda: mock.Mock(NAME="gia", model_name=lambda: "mo-hinh-gia")
        ngoai = self.tao_client().get("/suc-khoe")
        self.assertEqual(ngoai.status_code, 200)
        self.assertEqual(ngoai.json(), {"ok": True, "nha_cung_cap": "", "mo_hinh": ""})
        trong = self.tao_client().get("/suc-khoe", headers=self.bearer("hs.binh", MK_HS)).json()
        self.assertEqual(trong["nha_cung_cap"], "gia")
        self.assertEqual(self.may_chu().get("/suc-khoe").json()["mo_hinh"], "mo-hinh-gia")


class DuLieuChoHocSinhTest(NenDangNhap):
    """Các đường dẫn học sinh được gọi nhưng chỉ nhận phần được phép xem."""

    def setUp(self):
        super().setUp()
        self.hs_h = self.bearer("hs.binh", MK_HS)
        self.gv_h = self.bearer("gv.an", MK_GV)

    def goi(self, duong, h):
        r = self.tao_client().get(duong, headers=h)
        self.assertEqual(r.status_code, 200, duong)
        return r.json()

    def test_bai_thi_nghiem_khong_lo_dap_an(self):
        hs = self.goi("/lab/sinh/bai-thi-nghiem", self.hs_h)["muc"]
        self.assertGreater(len(hs), 0)
        chu = json.dumps(hs, ensure_ascii=False)
        for truong in ("dap_an", "ket_qua_mong_doi", "dieu_kien", "sinh_pham"):
            self.assertNotIn(truong, chu)
        self.assertTrue(hs[0]["buoc"][0]["mo_ta"])
        self.assertTrue(hs[0]["cau_hoi"][0]["hoi"])
        gv = self.goi("/lab/sinh/bai-thi-nghiem", self.gv_h)["muc"]
        self.assertIn("dap_an", gv[0]["cau_hoi"][0])  # trang của giáo viên vẫn đủ

    def test_lich_phong_khong_lo_ten_nguoi_dat(self):
        r = self.tao_client().post("/lab/sinh/dat-lich", headers=self.gv_h, json={"doi_tuong": "may_pcr", "nguoi": "Giáo viên An", "lop": "10A1", "bat_dau": 2e9, "ket_thuc": 2e9 + 3600})
        self.assertEqual(r.status_code, 200, r.text)
        hs = self.goi("/lab/sinh/dat-lich", self.hs_h)["muc"]
        self.assertEqual(hs[0]["lop"], "10A1")
        self.assertNotIn("nguoi", hs[0])
        self.assertEqual(self.goi("/lab/sinh/dat-lich", self.gv_h)["muc"][0]["nguoi"], "Giáo viên An")

    def test_tong_quan_sinh_chi_giao_vien_thay_so_lieu(self):
        self.assertEqual(self.goi("/lab/sinh/tong-quan", self.hs_h), {"ket_noi": True})
        self.assertEqual(self.may_chu().get("/lab/sinh/tong-quan").json(), {"ket_noi": True})
        for i in range(12):
            sinh_db.add_su_co({"phong": "phong_sinh", "loai": "thu", "mo_ta": f"sự cố {i}"})
        gv = self.goi("/lab/sinh/tong-quan", self.gv_h)
        self.assertEqual((len(gv["su_co_moi"]), gv["so_su_co_moi"]), (10, 12))  # danh sách giới hạn 10 dòng, số đếm vẫn đúng

    def test_danh_sach_huong_dan_khong_co_ton_kho(self):
        ds = self.goi("/lab/sinh/huong-dan", self.hs_h)
        self.assertEqual(ds["tong"], 108)
        self.assertEqual(sorted(ds["muc"][0]), ["ma", "phan_loai", "ten"])
        self.assertEqual(self.tao_client().get("/lab/sinh/dung-cu", headers=self.hs_h).status_code, 403)

    def test_hoc_sinh_doc_duoc_huong_dan(self):
        ds = self.goi("/lab/sinh/huong-dan", self.hs_h)["muc"]
        self.assertTrue(self.goi(f"/lab/sinh/huong-dan/{ds[0]['ma']}", self.hs_h)["cach_dung"])
        may = self.goi("/may", self.hs_h)
        self.assertTrue(self.goi(f"/may/{may[0]['ma']}", self.hs_h)["quy_trinh"])


class DuLieuTheoTaiKhoanTest(NenDangNhap):
    GIA_MAO ={"hoc_sinh": "Người khác", "lop": "12A9", "ma_bai": "b1", "diem": 10, "nhan_xet": "Tự khen", "hoc_sinh_id": 999, "du_lieu": {"a": 1}}

    def gui(self, client, **kw):
        r = client.post("/lab/sinh/su-kien", json={"su_kien": [{"loai": "ket_qua_tn", "du_lieu": self.GIA_MAO}]}, **kw)
        self.assertEqual(r.json(), {"nhan": 1, "loi": []})
        return sinh_db.list_ket_qua()[0]

    def test_hoc_sinh_khong_tu_cham_khong_gia_ten(self):
        kq = self.gui(self.tao_client(), headers=self.bearer("hs.binh", MK_HS))
        self.assertEqual((kq["hoc_sinh"], kq["lop"], kq["hoc_sinh_id"]), ("Học sinh Bình", "10A1", self.hs["id"]))
        self.assertIsNone(kq["diem"])
        self.assertEqual(kq["nhan_xet"], "")

    def test_hoc_sinh_chua_vao_lop(self):
        tai_khoan.tao_nguoi_dung("hs.le", "Học sinh Lẻ", "hoc_sinh", MK_HS)
        kq = self.gui(self.tao_client(), headers=self.bearer("hs.le", MK_HS))
        self.assertEqual(kq["lop"], "Chưa vào lớp")

    def test_unity_khong_dang_nhap_khong_ghi_diem(self):
        kq = self.gui(self.may_chu())
        self.assertIsNone(kq["diem"])
        self.assertIsNone(kq["hoc_sinh_id"])
        self.assertEqual(kq["hoc_sinh"], "Người khác")

    def test_giao_vien_ghi_duoc_diem(self):
        self.assertEqual(self.gui(self.tao_client(), headers=self.bearer("gv.an", MK_GV))["diem"], 10)

    def test_nhat_ky_ghi_dung_nguon_hoc_sinh(self):
        self.tao_client().post("/api/nhat-ky", json={"tieu_de": "x", "nguon": "giao_vien"}, headers=self.bearer("hs.binh", MK_HS))
        self.assertEqual(db.query("SELECT nguon FROM nhat_ky ORDER BY id DESC LIMIT 1")[0]["nguon"], "hoc_sinh:hs.binh")
        self.may_chu().post("/api/nhat-ky", json={"tieu_de": "y", "nguon": "phong_lab_ao"})
        self.assertEqual(db.query("SELECT nguon FROM nhat_ky ORDER BY id DESC LIMIT 1")[0]["nguon"], "phong_lab_ao")


class NangCapCsdlTest(unittest.TestCase):
    def test_csdl_cu_duoc_them_bang_va_cot_khong_mat_du_lieu(self):
        with tempfile.TemporaryDirectory() as tmp:
            duong_dan = Path(tmp) / "cu.db"
            conn = sqlite3.connect(duong_dan)
            conn.executescript("""CREATE TABLE ket_qua_tn (id INTEGER PRIMARY KEY AUTOINCREMENT, hoc_sinh TEXT NOT NULL, lop TEXT NOT NULL, ban TEXT DEFAULT '',
                ma_bai TEXT NOT NULL, thoi_gian REAL NOT NULL, du_lieu TEXT NOT NULL DEFAULT '{}', diem REAL, nhan_xet TEXT DEFAULT '');
                INSERT INTO ket_qua_tn (hoc_sinh, lop, ma_bai, thoi_gian) VALUES ('Bản ghi cũ', '10A1', 'b1', 1);""")
            conn.commit()
            conn.close()
            try:
                sinh_db.reset_for_tests(duong_dan)
                db.reset_for_tests(duong_dan)
                kq = sinh_db.list_ket_qua()[0]
                self.assertEqual(kq["hoc_sinh"], "Bản ghi cũ")
                self.assertIsNone(kq["hoc_sinh_id"])
                self.assertEqual(db.query("SELECT COUNT(*) AS n FROM nguoi_dung")[0]["n"], 0)
            finally:
                sinh_db.reset_for_tests(Path(tmp) / "khac.db")
                db.reset_for_tests(Path(tmp) / "khac.db")


class TaiKhoanMauTest(NenDangNhap):
    def setUp(self):
        super().setUp()
        self.tep = Path(self.tmp.name) / "mat_khau.json"  # không đụng tệp mật khẩu thật của máy này

    def nap(self):
        lop = quan_tri.nap_mau(tep_mat_khau=self.tep)
        return lop, json.loads(self.tep.read_text(encoding="utf-8"))

    def test_nap_mau_dang_nhap_duoc_chay_lai_khong_trung(self):
        lop, tep = self.nap()
        lop2, tep2 = self.nap()
        self.assertEqual(tep, tep2)  # chạy lại không đổi mật khẩu, mã lớp
        self.assertEqual([l["id"] for l in lop], [l["id"] for l in lop2])
        mau = json.loads(quan_tri.MAU.read_text(encoding="utf-8"))
        for u in mau["giao_vien"] + mau["hoc_sinh"]:
            self.assertEqual(self.dang_nhap(self.tao_client(), u["ten_dang_nhap"], tep["mat_khau"][u["ten_dang_nhap"]]).status_code, 200, u["ten_dang_nhap"])
        self.assertEqual(db.query("SELECT COUNT(*) AS n FROM nguoi_dung")[0]["n"], 2 + len(mau["giao_vien"]) + len(mau["hoc_sinh"]))
        self.assertEqual(sorted(tep["ma_lop"].values()), sorted(l["ma_lop"] for l in lop))

    def test_tep_trong_git_khong_co_mat_khau(self):
        mau = json.loads(quan_tri.MAU.read_text(encoding="utf-8"))
        for u in mau["giao_vien"] + mau["hoc_sinh"]:
            self.assertNotIn("mat_khau", u)
        for l in mau["lop"]:
            self.assertNotIn("ma_lop", l)

    def test_tep_mat_khau_bi_git_bo_qua(self):
        goc = quan_tri.MAU.parents[1]
        self.assertIn(f"data/{quan_tri.TEP_MAT_KHAU.name}", (goc / ".gitignore").read_text(encoding="utf-8").splitlines())

    def test_may_khac_ra_mat_khau_khac(self):
        _, tep = self.nap()
        self.tep.unlink()
        _, tep2 = self.nap()  # mất tệp thì đặt lại, không để tài khoản không ai đăng nhập được
        for ten, mk in tep["mat_khau"].items():
            self.assertNotEqual(mk, tep2["mat_khau"][ten])
        self.assertEqual(self.dang_nhap(self.tao_client(), "gv.thu1", tep["mat_khau"]["gv.thu1"]).status_code, 401)
        self.assertEqual(self.dang_nhap(self.tao_client(), "gv.thu1", tep2["mat_khau"]["gv.thu1"]).status_code, 200)

    def test_doi_mat_khau_roi_nap_lai_van_khop_tep(self):
        _, tep = self.nap()
        tai_khoan.dat_mat_khau(tai_khoan.tim_theo_ten("hs.thu1")["id"], "mat-khau-nguoi-thu-doi")
        h = self.bearer("hs.thu1", "mat-khau-nguoi-thu-doi")
        _, tep2 = self.nap()
        self.assertEqual(self.dang_nhap(self.tao_client(), "hs.thu1", tep2["mat_khau"]["hs.thu1"]).status_code, 200)
        self.assertEqual(self.tao_client().get("/auth/toi", headers=h).status_code, 401)  # phiên cũ bị hủy
        self.assertEqual(tep["mat_khau"]["gv.thu1"], tep2["mat_khau"]["gv.thu1"])

    def test_tu_choi_khi_mo_mang(self):
        with mock.patch.dict(os.environ, {"KHKT_MO_MANG": "1"}), self.assertRaises(tai_khoan.LoiTaiKhoan):
            quan_tri.nap_mau(tep_mat_khau=self.tep)
        self.assertFalse(self.tep.exists())

    def test_web_khong_tao_duoc_giao_vien(self):
        cac_duong = {r.path for r in main.app.routes if not isinstance(r, Mount)}
        self.assertFalse({"/auth/tao-giao-vien", "/nguoi-dung"} & cac_duong)


if __name__ == "__main__":
    unittest.main()
