"""Kiểm thử trình chiếu: tải bài giảng, đổi trang, app nhận đúng việc, ai được xem gì, đếm máy đã hiện."""

import io
import tempfile
import time
import unittest
from pathlib import Path
from unittest import mock

import pypdfium2
from argon2 import PasswordHasher
from fastapi.testclient import TestClient
from PIL import Image

from server.ai import main
from server.lab import db, sinh_db, tai_khoan, trinh_chieu

MK = "mat-khau-kiem-thu"


def tao_pdf(so_trang=3, rong=842, cao=595) -> bytes:
    doc = pypdfium2.PdfDocument.new()
    for _ in range(so_trang):
        doc.new_page(rong, cao)
    ra = io.BytesIO()
    doc.save(ra)
    return ra.getvalue()


def tao_anh(rong=400, cao=300, kieu="PNG") -> bytes:
    ra = io.BytesIO()
    Image.new("RGB", (rong, cao), "red").save(ra, kieu)
    return ra.getvalue()


class NenTrinhChieu(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.db_path = Path(self.tmp.name) / "lab.db"
        db.reset_for_tests(self.db_path)
        sinh_db.reset_for_tests(self.db_path)
        trinh_chieu.xoa_nhip()
        self._bam = tai_khoan._bam
        tai_khoan._bam = PasswordHasher(time_cost=1, memory_cost=1024, parallelism=1)
        self.c = TestClient(main.app, base_url="http://192.168.1.12:8000", client=("192.168.1.50", 50000), follow_redirects=False, raise_server_exceptions=False)
        self.gv = tai_khoan.tao_nguoi_dung("gv.mot", "Giáo viên Một", "giao_vien", MK)
        self.gv2 = tai_khoan.tao_nguoi_dung("gv.hai", "Giáo viên Hai", "giao_vien", MK)
        self.hs = tai_khoan.tao_nguoi_dung("hs.an", "Học sinh An", "hoc_sinh", MK)
        self.hs_ngoai = tai_khoan.tao_nguoi_dung("hs.ngoai", "Học sinh Ngoài", "hoc_sinh", MK)
        self.lop = tai_khoan.tao_lop("10A1", "", self.gv["id"])
        self.lop2 = tai_khoan.tao_lop("10B1", "", self.gv2["id"])
        tai_khoan.them_vao_lop(self.lop["id"], self.hs["id"])
        tai_khoan.them_vao_lop(self.lop2["id"], self.hs_ngoai["id"])
        self.h_gv, self.h_gv2 = self.phien("gv.mot"), self.phien("gv.hai")
        self.h_hs, self.h_ngoai = self.phien("hs.an"), self.phien("hs.ngoai")

    def tearDown(self):
        tai_khoan._bam = self._bam
        db.reset_for_tests(Path(self.tmp.name) / "khac.db")
        sinh_db.reset_for_tests(Path(self.tmp.name) / "khac.db")
        trinh_chieu.xoa_nhip()
        self.tmp.cleanup()

    def phien(self, ten):
        r = self.c.post("/auth/dang-nhap", json={"ten_dang_nhap": ten, "mat_khau": MK, "kieu": "ung_dung"})
        return {"Authorization": "Bearer " + r.json()["ma_phien"]}

    def tai(self, tep=None, ten="Bài giảng", h=None):
        tep = tep if tep is not None else [("bai.pdf", tao_pdf())]
        return self.c.post("/api/bai-giang", data={"ten": ten}, files=[("tep", t) for t in tep], headers=h or self.h_gv)

    def chieu(self, lop=None, h=None, **body):
        return self.c.post(f"/api/lop/{(lop or self.lop)['id']}/trinh-chieu", json=body, headers=h or self.h_gv)

    def nhip(self, h, da_hien=0):
        r = self.c.post("/app/nhip", json={"da_hien": da_hien}, headers=h)
        self.assertEqual(r.status_code, 200, r.text)
        return r.json()["trinh_chieu"]


class TaiBaiGiangTest(NenTrinhChieu):
    def test_tai_pdf_thanh_anh_16_9(self):
        r = self.tai()
        self.assertEqual(r.status_code, 200, r.text)
        self.assertEqual(r.json()["so_trang"], 3)
        anh = self.c.get(f"/api/bai-giang/{r.json()['id']}/trang/2", headers=self.h_gv)
        self.assertEqual((anh.status_code, anh.headers["content-type"]), (200, "image/jpeg"))
        self.assertEqual(Image.open(io.BytesIO(anh.content)).size, (1600, 900))
        self.assertEqual(self.c.get(f"/api/bai-giang/{r.json()['id']}/trang/4", headers=self.h_gv).status_code, 404)

    def test_chi_giu_anh_khong_giu_tep_goc(self):
        bai = self.tai().json()["id"]
        thu_muc = self.db_path.parent / "bai_giang" / str(bai)
        self.assertEqual(sorted(p.name for p in thu_muc.iterdir()), ["1.jpg", "2.jpg", "3.jpg"])

    def test_nhieu_anh_xep_theo_ten_tep_va_khong_meo(self):
        r = self.tai([("b.png", tao_anh()), ("a.jpg", tao_anh(300, 800, "JPEG"))])
        self.assertEqual(r.json()["so_trang"], 2)
        anh = Image.open(io.BytesIO(self.c.get(f"/api/bai-giang/{r.json()['id']}/trang/1", headers=self.h_gv).content))
        self.assertEqual(anh.size, (1600, 900))
        trai = anh.convert("RGB").getpixel((5, 450))  # ảnh dọc "a.jpg" là trang 1: hai bên là nền trắng, không bị kéo giãn
        giua = anh.convert("RGB").getpixel((800, 450))
        self.assertGreater(trai[1], 240)
        self.assertLess(giua[1], 40)

    def test_tu_choi_tep_khong_dung(self):
        self.assertEqual(self.tai([("ghi_chu.txt", b"khong phai anh")]).status_code, 422)
        self.assertEqual(self.tai([("gia.pdf", b"%PDF-1.7 hong")]).status_code, 422)
        self.assertEqual(self.tai(ten="   ").status_code, 422)
        self.assertEqual(db.query("SELECT COUNT(*) AS n FROM bai_giang")[0]["n"], 0)

    def test_gioi_han_dung_luong_va_so_trang(self):
        with mock.patch.object(trinh_chieu, "TOI_DA_MB", 0):
            self.assertEqual(self.tai().status_code, 413)
        with mock.patch.object(trinh_chieu, "TOI_DA_TRANG", 2):
            self.assertEqual(self.tai().status_code, 422)

    def test_anh_qua_lon_bi_tu_choi(self):
        with mock.patch.object(Image, "MAX_IMAGE_PIXELS", 100):
            self.assertEqual(self.tai([("to.png", tao_anh(400, 300))]).status_code, 422)

    def test_danh_sach_rieng_tung_giao_vien(self):
        self.tai(ten="Bài của Một")
        self.assertEqual([b["ten"] for b in self.c.get("/api/bai-giang", headers=self.h_gv).json()], ["Bài của Một"])
        self.assertEqual(self.c.get("/api/bai-giang", headers=self.h_gv2).json(), [])

    def test_quyen_tai_va_xoa(self):
        bai = self.tai().json()["id"]
        self.assertEqual(self.tai(h=self.h_hs).status_code, 403)
        self.assertEqual(self.c.get("/api/bai-giang", headers=self.h_hs).status_code, 403)
        self.assertEqual(self.c.delete(f"/api/bai-giang/{bai}", headers=self.h_hs).status_code, 403)
        self.assertEqual(self.c.delete(f"/api/bai-giang/{bai}", headers=self.h_gv2).status_code, 404)
        self.assertEqual(self.c.get("/api/bai-giang").status_code, 401)
        self.assertTrue((self.db_path.parent / "bai_giang" / str(bai) / "1.jpg").exists())


class ChieuTest(NenTrinhChieu):
    def setUp(self):
        super().setUp()
        self.bai = self.tai(ten="Chuẩn độ").json()["id"]

    def test_app_nhan_trang_dang_chieu_va_tai_duoc_anh(self):
        self.assertEqual(self.nhip(self.h_hs)["loai"], "tat")
        r = self.chieu(loai="slide", bai_id=self.bai, trang=2, man_hinh=["tuong_video"])
        self.assertEqual(r.status_code, 200, r.text)
        viec = self.nhip(self.h_hs)
        self.assertEqual((viec["loai"], viec["man_hinh"], viec["tieu_de"]), ("slide", ["tuong_video"], "Chuẩn độ · Trang 2/3"))
        self.assertEqual(viec["anh"], f"/api/bai-giang/{self.bai}/trang/2")
        self.assertEqual(self.c.get(viec["anh"], headers=self.h_hs).status_code, 200)

    def test_phien_ban_luon_tang(self):
        ds = []
        for trang in (1, 2, 1):
            self.chieu(loai="slide", bai_id=self.bai, trang=trang)
            ds.append(self.nhip(self.h_hs)["phien_ban"])
        self.chieu(loai="tat")
        ds.append(self.nhip(self.h_hs)["phien_ban"])
        self.assertEqual(ds, sorted(set(ds)))

    def test_hoc_sinh_lop_khac_khong_thay(self):
        self.chieu(loai="slide", bai_id=self.bai, trang=1)
        self.assertEqual(self.nhip(self.h_ngoai)["loai"], "tat")
        self.assertEqual(self.c.get(f"/api/bai-giang/{self.bai}/trang/1", headers=self.h_ngoai).status_code, 404)
        self.assertEqual(self.c.get(f"/api/bai-giang/{self.bai}/trang/1", headers=self.h_gv2).status_code, 404)

    def test_tat_chieu_thi_hoc_sinh_het_quyen_xem_anh(self):
        self.chieu(loai="slide", bai_id=self.bai, trang=1)
        self.assertEqual(self.c.get(f"/api/bai-giang/{self.bai}/trang/1", headers=self.h_hs).status_code, 200)
        self.chieu(loai="tat")
        viec = self.nhip(self.h_hs)
        self.assertEqual((viec["loai"], viec["anh"], viec["man_hinh"]), ("tat", "", []))
        self.assertEqual(self.c.get(f"/api/bai-giang/{self.bai}/trang/1", headers=self.h_hs).status_code, 404)

    def test_dua_chu_va_dem_nguoc(self):
        r = self.chieu(loai="chu", tieu_de="Thảo luận", noi_dung="Vì sao phải lắc bình nón?", dem_nguoc_giay=60)
        self.assertEqual(r.status_code, 200, r.text)
        ket = self.c.post("/app/nhip", json={}, headers=self.h_hs).json()
        viec = ket["trinh_chieu"]
        self.assertEqual((viec["loai"], viec["tieu_de"], viec["noi_dung"], viec["anh"]), ("chu", "Thảo luận", "Vì sao phải lắc bình nón?", ""))
        self.assertAlmostEqual(viec["dem_nguoc_den"] - ket["may_chu_luc"], 60, delta=2)

    def test_du_lieu_khong_hop_le(self):
        self.assertEqual(self.chieu(loai="slide", bai_id=self.bai, trang=4).status_code, 422)
        self.assertEqual(self.chieu(loai="slide", bai_id=self.bai, trang=0).status_code, 422)
        self.assertEqual(self.chieu(loai="chu", tieu_de="  ").status_code, 422)
        self.assertEqual(self.chieu(loai="la").status_code, 422)
        self.assertEqual(self.chieu(loai="slide", bai_id=self.bai, man_hinh=[]).status_code, 422)
        self.assertEqual(self.chieu(loai="slide", bai_id=self.bai, man_hinh=["../../etc"]).status_code, 422)
        self.assertEqual(self.chieu(loai="chu", tieu_de="x", dem_nguoc_giay=-1).status_code, 422)
        self.assertEqual(self.nhip(self.h_hs)["loai"], "tat")

    def test_ma_man_hinh_hop_le(self):
        for m in ("tuong_video", "tuong_video_2", "ban", "ban_3", "hanh_lang", "t2_tuong_video", "t2_ban_1", "t2_hanh_lang"):
            self.assertEqual(self.chieu(loai="slide", bai_id=self.bai, man_hinh=[m]).status_code, 200, m)

    def test_khong_chieu_duoc_bai_hoac_lop_cua_nguoi_khac(self):
        self.assertEqual(self.chieu(lop=self.lop2, loai="slide", bai_id=self.bai).status_code, 404)
        self.assertEqual(self.chieu(lop=self.lop2, h=self.h_gv2, loai="slide", bai_id=self.bai).status_code, 404)
        self.assertEqual(self.c.get(f"/api/lop/{self.lop['id']}/trinh-chieu", headers=self.h_gv2).status_code, 404)
        self.assertEqual(self.chieu(h=self.h_hs, loai="tat").status_code, 403)
        self.assertEqual(self.c.get(f"/api/lop/{self.lop['id']}/trinh-chieu", headers=self.h_hs).status_code, 403)

    def test_hoc_sinh_hai_lop_thay_lop_moi_doi_nhat(self):
        tai_khoan.them_vao_lop(self.lop2["id"], self.hs["id"])
        self.chieu(loai="slide", bai_id=self.bai, trang=1)
        self.assertEqual(self.nhip(self.h_hs)["loai"], "slide")
        self.chieu(lop=self.lop2, h=self.h_gv2, loai="chu", tieu_de="Câu hỏi lớp B")
        self.assertEqual(self.nhip(self.h_hs)["tieu_de"], "Câu hỏi lớp B")
        self.chieu(lop=self.lop2, h=self.h_gv2, loai="tat")  # lớp B tắt thì quay lại lớp A còn đang chiếu
        self.assertEqual(self.nhip(self.h_hs)["loai"], "slide")

    def test_giao_vien_mo_app_cung_thay_lop_minh(self):
        self.chieu(loai="slide", bai_id=self.bai, trang=3)
        self.assertEqual(self.nhip(self.h_gv)["anh"], f"/api/bai-giang/{self.bai}/trang/3")
        self.assertEqual(self.nhip(self.h_gv2)["loai"], "tat")

    def test_xoa_bai_dang_chieu_thi_tat_luon(self):
        self.chieu(loai="slide", bai_id=self.bai, trang=1)
        self.assertEqual(self.c.delete(f"/api/bai-giang/{self.bai}", headers=self.h_gv).status_code, 200)
        self.assertEqual(self.nhip(self.h_hs)["loai"], "tat")
        self.assertFalse((self.db_path.parent / "bai_giang" / str(self.bai)).exists())

    def test_chua_dang_nhap_khong_goi_duoc_nhip(self):
        self.assertEqual(self.c.post("/app/nhip", json={}).status_code, 401)
        cung_may = TestClient(main.app, base_url="http://127.0.0.1:8000", client=("127.0.0.1", 50000), raise_server_exceptions=False)
        self.assertEqual(cung_may.post("/app/nhip", json={}).status_code, 401)  # không nằm trong lối riêng của Unity


class DemMayTest(NenTrinhChieu):
    def setUp(self):
        super().setUp()
        self.bai = self.tai().json()["id"]
        self.duong = f"/api/lop/{self.lop['id']}/trinh-chieu"

    def trang_thai(self):
        return self.c.get(self.duong, headers=self.h_gv).json()

    def test_dem_may_mo_va_may_da_hien(self):
        self.assertEqual((self.trang_thai()["so_hoc_sinh"], self.trang_thai()["may_dang_mo"]), (1, 0))
        pb = self.chieu(loai="slide", bai_id=self.bai).json()["dang_chieu"]["phien_ban"]
        self.nhip(self.h_hs)
        t = self.trang_thai()
        self.assertEqual((t["may_dang_mo"], t["may_da_hien"], t["do_tre_ms"]), (1, 0, None))
        self.nhip(self.h_hs, pb)
        t = self.trang_thai()
        self.assertEqual((t["may_dang_mo"], t["may_da_hien"]), (1, 1))
        self.assertGreaterEqual(t["do_tre_ms"], 0)

    def test_do_tre_khong_tang_theo_cac_nhip_sau(self):
        pb = self.chieu(loai="slide", bai_id=self.bai).json()["dang_chieu"]["phien_ban"]
        self.nhip(self.h_hs, pb)
        dau = self.trang_thai()["do_tre_ms"]
        with mock.patch("server.lab.trinh_chieu.time.time", return_value=time.time() + 5):
            self.nhip(self.h_hs, pb)
        self.assertEqual(self.trang_thai()["do_tre_ms"], dau)

    def test_doi_trang_thi_chua_tinh_la_da_hien(self):
        pb = self.chieu(loai="slide", bai_id=self.bai, trang=1).json()["dang_chieu"]["phien_ban"]
        self.nhip(self.h_hs, pb)
        self.chieu(loai="slide", bai_id=self.bai, trang=2)
        self.assertEqual(self.trang_thai()["may_da_hien"], 0)

    def test_may_im_lang_bi_coi_la_tat(self):
        self.nhip(self.h_hs)
        self.assertEqual(self.trang_thai()["may_dang_mo"], 1)
        with mock.patch("server.lab.trinh_chieu.time.time", return_value=time.time() + trinh_chieu.GIAY_CON_MO + 1):
            self.assertEqual(self.trang_thai()["may_dang_mo"], 0)

    def test_may_ngoai_lop_khong_tinh(self):
        self.nhip(self.h_ngoai)
        self.assertEqual(self.trang_thai()["may_dang_mo"], 0)

    def test_trang_thai_cho_biet_vi_tri_trong_bai(self):
        self.chieu(loai="slide", bai_id=self.bai, trang=2)
        d = self.trang_thai()["dang_chieu"]
        self.assertEqual((d["bai_id"], d["trang"], d["so_trang"], d["ten_bai"]), (self.bai, 2, 3, "Bài giảng"))


class TepVaTrangTest(NenTrinhChieu):
    def test_thu_muc_anh_khong_len_git(self):
        goc = Path(trinh_chieu.__file__).resolve().parents[2]
        self.assertIn("data/bai_giang/", (goc / ".gitignore").read_text(encoding="utf-8").splitlines())

    def test_trang_dieu_khien_chi_cho_giao_vien(self):
        self.assertEqual(self.c.get("/trinh-chieu").headers["location"], "/dang-nhap?tiep=%2Ftrinh-chieu")
        self.assertEqual(self.c.get("/trinh-chieu", headers=self.h_hs).headers["location"], "/hoc-sinh")
        r = self.c.get("/trinh-chieu", headers=self.h_gv)
        self.assertEqual(r.status_code, 200)
        self.assertIn("/tinh/phien.js", r.text)
        self.assertIn("data-dang-xuat", r.text)


if __name__ == "__main__":
    unittest.main()
