"""Kiểm thử lớp học: giáo viên chỉ quản lý lớp của mình, mật khẩu tạm, kết quả và chấm điểm theo lớp."""

import tempfile
import unittest
from pathlib import Path

from argon2 import PasswordHasher
from fastapi.testclient import TestClient

from server.ai import main
from server.lab import dang_nhap, db, quan_tri, sinh_db, tai_khoan

MK = "mat-khau-kiem-thu"


class NenLop(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.db_path = Path(self.tmp.name) / "lab.db"
        db.reset_for_tests(self.db_path)
        sinh_db.reset_for_tests(self.db_path)
        self._bam = tai_khoan._bam
        tai_khoan._bam = PasswordHasher(time_cost=1, memory_cost=1024, parallelism=1)
        self.c = TestClient(main.app, base_url="http://192.168.1.12:8000", client=("192.168.1.50", 50000), follow_redirects=False, raise_server_exceptions=False)
        self.gv1 = tai_khoan.tao_nguoi_dung("gv.mot", "Giáo viên Một", "giao_vien", MK)
        self.gv2 = tai_khoan.tao_nguoi_dung("gv.hai", "Giáo viên Hai", "giao_vien", MK)
        self.hs = tai_khoan.tao_nguoi_dung("hs.an", "Học sinh An", "hoc_sinh", MK)
        self.lop = tai_khoan.tao_lop("10A1", "2026-2027", self.gv1["id"])
        tai_khoan.them_vao_lop(self.lop["id"], self.hs["id"])
        self.h1 = self.phien("gv.mot")
        self.h2 = self.phien("gv.hai")
        self.hh = self.phien("hs.an")

    def tearDown(self):
        tai_khoan._bam = self._bam
        db.reset_for_tests(Path(self.tmp.name) / "khac.db")
        sinh_db.reset_for_tests(Path(self.tmp.name) / "khac.db")
        self.tmp.cleanup()

    def phien(self, ten, mk=MK):
        r = self.c.post("/auth/dang-nhap", json={"ten_dang_nhap": ten, "mat_khau": mk, "kieu": "ung_dung"})
        self.assertEqual(r.status_code, 200, r.text)
        return {"Authorization": "Bearer " + r.json()["ma_phien"]}

    def duong(self, sau=""):
        return f"/api/lop/{self.lop['id']}{sau}"

    def them(self, danh_sach, h=None):
        return self.c.post(self.duong("/hoc-sinh"), json={"danh_sach": danh_sach}, headers=h or self.h1)

    def nop(self, h, **them):
        du_lieu = {"ma_bai": "te_bao_hanh", "du_lieu": {"tom_tat": "Thấy rõ nhân tế bào"}, **them}
        r = self.c.post("/lab/sinh/su-kien", json={"su_kien": [{"loai": "ket_qua_tn", "du_lieu": du_lieu}]}, headers=h)
        self.assertEqual(r.json(), {"nhan": 1, "loi": []})
        return sinh_db.list_ket_qua(ma_bai=du_lieu["ma_bai"])[0]


class LopTest(NenLop):
    def test_tao_lop_va_danh_sach(self):
        r = self.c.post("/api/lop", json={"ten": " 10A2 ", "nam_hoc": "2026-2027"}, headers=self.h1)
        self.assertEqual(r.status_code, 200, r.text)
        ds = {l["ten"]: l for l in self.c.get("/api/lop", headers=self.h1).json()}
        self.assertEqual(ds["10A1"]["so_hoc_sinh"], 1)
        self.assertEqual(ds["10A2"]["so_hoc_sinh"], 0)
        self.assertIs(ds["10A2"]["ma_lop_mo"], True)
        self.assertEqual(len(ds["10A2"]["ma_lop"]), 6)

    def test_lop_cua_nguoi_tao(self):
        self.c.post("/api/lop", json={"ten": "Lớp của Hai"}, headers=self.h2)
        self.assertEqual([l["ten"] for l in self.c.get("/api/lop", headers=self.h2).json()], ["Lớp của Hai"])
        self.assertEqual([l["ten"] for l in self.c.get("/api/lop", headers=self.h1).json()], ["10A1"])

    def test_ten_lop_khong_hop_le(self):
        self.assertEqual(self.c.post("/api/lop", json={"ten": "   "}, headers=self.h1).status_code, 422)
        self.assertEqual(self.c.patch(self.duong(), json={"ten": ""}, headers=self.h1).status_code, 422)

    def test_doi_ten_va_dong_mo_ma_lop(self):
        dk = {"ma_lop": self.lop["ma_lop"], "ho_ten": "Học sinh Mới", "ten_dang_nhap": "hs.moi", "mat_khau": MK}
        self.assertEqual(self.c.patch(self.duong(), json={"ten": "10A9", "ma_lop_mo": False}, headers=self.h1).json()["ten"], "10A9")
        self.assertEqual(self.c.post("/auth/dang-ky", json=dk).status_code, 403)
        self.assertIs(self.c.patch(self.duong(), json={"ma_lop_mo": True}, headers=self.h1).json()["ma_lop_mo"], True)
        self.assertEqual(self.c.post("/auth/dang-ky", json=dk).status_code, 200)
        self.c.cookies.clear()
        self.assertIn("hs.moi", [h["ten_dang_nhap"] for h in self.c.get(self.duong("/hoc-sinh"), headers=self.h1).json()])

    def test_giao_vien_khac_khong_dung_duoc_lop_nay(self):
        hs = self.hs["id"]
        for m, d, kw in (("PATCH", self.duong(), {"json": {"ten": "Hack"}}), ("GET", self.duong("/hoc-sinh"), {}),
                         ("POST", self.duong("/hoc-sinh"), {"json": {"danh_sach": "Người Lạ"}}),
                         ("DELETE", self.duong(f"/hoc-sinh/{hs}"), {}), ("POST", self.duong(f"/hoc-sinh/{hs}/dat-lai-mat-khau"), {}),
                         ("GET", self.duong("/ket-qua"), {}), ("GET", self.duong("/ket-qua.csv"), {})):
            self.assertEqual(self.c.request(m, d, headers=self.h2, **kw).status_code, 404, f"{m} {d}")
        self.assertEqual(db.query("SELECT ten FROM lop WHERE id=?", (self.lop["id"],))[0]["ten"], "10A1")
        self.assertIsNone(tai_khoan.tim_theo_ten("nguoi.la"))
        self.phien("hs.an")  # mật khẩu học sinh chưa bị đổi

    def test_hoc_sinh_khong_dung_duoc_api_lop(self):
        for m, d in (("GET", "/api/lop"), ("POST", "/api/lop"), ("GET", self.duong("/hoc-sinh")), ("GET", self.duong("/ket-qua")),
                     ("POST", self.duong(f"/hoc-sinh/{self.hs['id']}/dat-lai-mat-khau"))):
            self.assertEqual(self.c.request(m, d, headers=self.hh).status_code, 403, f"{m} {d}")

    def test_chua_dang_nhap_bi_chan(self):
        self.assertEqual(self.c.get("/api/lop").status_code, 401)
        self.assertEqual(self.c.get("/api/toi/ket-qua").status_code, 401)


class ThemHocSinhTest(NenLop):
    def test_dan_danh_sach_sinh_ten_va_mat_khau_tam(self):
        r = self.them("Nguyễn Văn Đức\nTrần Thị Bích\n\nLê Ánh Dương\n")
        self.assertEqual(r.status_code, 200, r.text)
        tao = r.json()["tao"]
        self.assertEqual([t["ten_dang_nhap"] for t in tao], ["nguyen.van.duc", "tran.thi.bich", "le.anh.duong"])
        self.assertEqual(r.json()["loi"], [])
        for t in tao:
            self.assertGreaterEqual(len(t["mat_khau_tam"]), 8)
        self.assertEqual(len({t["mat_khau_tam"] for t in tao}), 3)
        ds = {h["ten_dang_nhap"]: h for h in self.c.get(self.duong("/hoc-sinh"), headers=self.h1).json()}
        self.assertIs(ds["nguyen.van.duc"]["phai_doi_mat_khau"], True)

    def test_mat_khau_tam_khong_luu_tho_va_khong_tra_lai(self):
        t = self.them("Nguyễn Văn Đức").json()["tao"][0]
        self.assertNotIn(t["mat_khau_tam"].encode(), self.db_path.read_bytes())
        self.assertTrue(tai_khoan.tim_theo_ten(t["ten_dang_nhap"])["mat_khau_bam"].startswith("$argon2id$"))
        self.assertNotIn(t["mat_khau_tam"], self.c.get(self.duong("/hoc-sinh"), headers=self.h1).text)
        self.assertNotIn("mat_khau_bam", self.c.get(self.duong("/hoc-sinh"), headers=self.h1).text)

    def test_mat_khau_tam_phai_doi_truoc_khi_dung(self):
        t = self.them("Nguyễn Văn Đức").json()["tao"][0]
        h = self.phien(t["ten_dang_nhap"], t["mat_khau_tam"])
        self.assertEqual(self.c.get("/may", headers=h).status_code, 403)
        self.c.post("/auth/doi-mat-khau", json={"mat_khau_cu": t["mat_khau_tam"], "mat_khau_moi": "mat-khau-rieng-1"}, headers=h)
        self.assertEqual(self.c.get("/may", headers=h).status_code, 200)

    def test_mat_khau_tam_khong_co_ky_tu_de_nham(self):
        chu = "".join(tai_khoan.sinh_mat_khau_tam() for _ in range(300))
        self.assertFalse(set(chu) & set("l1io0"))

    def test_trung_ho_ten_them_so(self):
        tao = self.them("Nguyễn Văn An\nNguyễn Văn An").json()["tao"]
        self.assertEqual([t["ten_dang_nhap"] for t in tao], ["nguyen.van.an", "nguyen.van.an2"])

    def test_ten_dang_nhap_tu_chon_nhieu_cach_tach(self):
        tao = self.them("Một Hai,mot.hai\nBa Bốn\tba.bon\nNăm Sáu; nam.sau").json()["tao"]
        self.assertEqual([t["ten_dang_nhap"] for t in tao], ["mot.hai", "ba.bon", "nam.sau"])

    def test_hoc_sinh_da_co_tai_khoan_duoc_dua_vao_lop(self):
        tai_khoan.tao_nguoi_dung("hs.co.san", "Học Sinh Có Sẵn", "hoc_sinh", MK)
        r = self.them("hoc sinh có sẵn, hs.co.san").json()
        self.assertEqual((r["tao"], r["loi"]), ([], []))
        self.assertEqual(r["them"][0]["ten_dang_nhap"], "hs.co.san")
        self.assertNotIn("mat_khau_tam", r["them"][0])
        self.phien("hs.co.san")  # mật khẩu giữ nguyên

    def test_khong_them_nham_nguoi_khac(self):
        r = self.them("Người Khác, hs.an\nGiáo Viên Hai, gv.hai").json()
        self.assertEqual(len(r["loi"]), 2)
        self.assertEqual(r["them"], [])
        self.assertNotIn("Giáo viên Hai", [h["ho_ten"] for h in self.c.get(self.duong("/hoc-sinh"), headers=self.h1).json()])

    def test_dong_loi_khong_lam_hong_dong_khac(self):
        r = self.them("A\nNguyễn Văn An\nHọ Tên, ten co dau cach\n@@@").json()
        self.assertEqual([t["ten_dang_nhap"] for t in r["tao"]], ["nguyen.van.an"])
        self.assertEqual(len(r["loi"]), 3)
        self.assertTrue(all(l["ly_do"] for l in r["loi"]))

    def test_gioi_han_danh_sach(self):
        self.assertEqual(self.them("  \n ").status_code, 422)
        self.assertEqual(self.them("\n".join(f"Học Sinh {i}" for i in range(201))).status_code, 422)


class GoVaDatLaiTest(NenLop):
    def test_go_khoi_lop_giu_tai_khoan_va_ket_qua(self):
        kq = self.nop(self.hh)
        self.assertEqual(self.c.delete(self.duong(f"/hoc-sinh/{self.hs['id']}"), headers=self.h1).status_code, 200)
        self.assertEqual(self.c.get(self.duong("/hoc-sinh"), headers=self.h1).json(), [])
        self.assertEqual(self.c.get("/api/toi/ket-qua", headers=self.hh).json()[0]["id"], kq["id"])
        self.assertEqual(self.c.get(f"/lab/sinh/ket-qua/{kq['id']}", headers=self.h1).status_code, 404)  # không còn là học sinh của mình
        self.assertEqual(self.c.delete(self.duong(f"/hoc-sinh/{self.hs['id']}"), headers=self.h1).status_code, 404)

    def test_dat_lai_mat_khau(self):
        r = self.c.post(self.duong(f"/hoc-sinh/{self.hs['id']}/dat-lai-mat-khau"), headers=self.h1)
        self.assertEqual(r.status_code, 200, r.text)
        tam = r.json()["mat_khau_tam"]
        self.assertEqual(self.c.get("/auth/toi", headers=self.hh).status_code, 401)  # phiên cũ bị hủy
        self.assertEqual(self.c.post("/auth/dang-nhap", json={"ten_dang_nhap": "hs.an", "mat_khau": MK}).status_code, 401)
        r = self.c.post("/auth/dang-nhap", json={"ten_dang_nhap": "hs.an", "mat_khau": tam, "kieu": "ung_dung"})
        self.assertTrue(r.json()["nguoi_dung"]["phai_doi_mat_khau"])

    def test_khong_dat_lai_duoc_nguoi_ngoai_lop(self):
        for ten_nguoi in (self.gv2, tai_khoan.tao_nguoi_dung("hs.ngoai", "Học sinh Ngoài", "hoc_sinh", MK)):
            r = self.c.post(self.duong(f"/hoc-sinh/{ten_nguoi['id']}/dat-lai-mat-khau"), headers=self.h1)
            self.assertEqual(r.status_code, 404)
            self.phien(ten_nguoi["ten_dang_nhap"])

    def test_lenh_may_chu_dat_lai_mat_khau_giao_vien(self):
        quan_tri.dat_lai_mat_khau(self.gv1, "mat-khau-moi-gv-1")
        self.assertEqual(self.c.get("/auth/toi", headers=self.h1).status_code, 401)
        self.phien("gv.mot", "mat-khau-moi-gv-1")
        with self.assertRaises(tai_khoan.LoiTaiKhoan):
            quan_tri.dat_lai_mat_khau(self.gv1, "ngan")


class KetQuaTest(NenLop):
    def test_mot_vong_nop_xem_cham(self):
        kq = self.nop(self.hh)
        ds = self.c.get(self.duong("/ket-qua"), headers=self.h1).json()
        self.assertEqual(len(ds), 1)
        self.assertEqual((ds[0]["ho_ten"], ds[0]["ten_bai"], ds[0]["tom_tat"]), ("Học sinh An", "Quan sát tế bào biểu bì vảy hành dưới kính hiển vi", "Thấy rõ nhân tế bào"))
        self.assertIsNone(self.c.get("/api/toi/ket-qua", headers=self.hh).json()[0]["diem"])
        r = self.c.patch(f"/lab/sinh/ket-qua/{kq['id']}", json={"diem": 8.5, "nhan_xet": "Làm tốt"}, headers=self.h1)
        self.assertEqual(r.status_code, 200, r.text)
        cua_toi = self.c.get("/api/toi/ket-qua", headers=self.hh).json()[0]
        self.assertEqual((cua_toi["diem"], cua_toi["nhan_xet"]), (8.5, "Làm tốt"))

    def test_diem_ngoai_thang_10_bi_tu_choi(self):
        kq = self.nop(self.hh)
        for diem in (-1, 10.5):
            self.assertEqual(self.c.patch(f"/lab/sinh/ket-qua/{kq['id']}", json={"diem": diem}, headers=self.h1).status_code, 422)
        self.assertEqual(self.c.patch(f"/lab/sinh/ket-qua/{kq['id']}", json={"diem": None}, headers=self.h1).status_code, 200)

    def test_giao_vien_khac_khong_thay_khong_cham_duoc(self):
        kq = self.nop(self.hh)
        self.assertEqual(self.c.get("/lab/sinh/ket-qua", headers=self.h2).json()["tong"], 0)
        self.assertEqual(self.c.get(f"/lab/sinh/ket-qua/{kq['id']}", headers=self.h2).status_code, 404)
        self.assertEqual(self.c.patch(f"/lab/sinh/ket-qua/{kq['id']}", json={"diem": 1}, headers=self.h2).status_code, 404)
        self.assertNotIn("Học sinh An", self.c.get("/lab/sinh/ket-qua.csv", headers=self.h2).content.decode("utf-8-sig"))
        self.assertEqual(self.c.get("/lab/sinh/ket-qua", headers=self.h1).json()["tong"], 1)
        self.assertIsNone(sinh_db.get_ket_qua_by_id(kq["id"])["diem"])

    def test_ket_qua_khong_gan_tai_khoan_giao_vien_nao_cung_xem_duoc(self):
        kq = sinh_db.add_ket_qua({"hoc_sinh": "Học sinh", "lop": "10A1", "ma_bai": "b1"})
        for h in (self.h1, self.h2):
            self.assertEqual(self.c.get(f"/lab/sinh/ket-qua/{kq['id']}", headers=h).status_code, 200)

    def test_hoc_sinh_hai_lop_ca_hai_giao_vien_deu_thay(self):
        lop2 = tai_khoan.tao_lop("CLB Sinh", "", self.gv2["id"])
        tai_khoan.them_vao_lop(lop2["id"], self.hs["id"])
        kq = self.nop(self.hh)
        self.assertEqual(self.c.get(f"/api/lop/{lop2['id']}/ket-qua", headers=self.h2).json()[0]["id"], kq["id"])
        self.assertEqual(self.c.get(self.duong("/ket-qua"), headers=self.h1).json()[0]["id"], kq["id"])

    def test_hoc_sinh_chi_thay_ket_qua_cua_minh(self):
        hs2 = tai_khoan.tao_nguoi_dung("hs.binh", "Học sinh Bình", "hoc_sinh", MK)
        tai_khoan.them_vao_lop(self.lop["id"], hs2["id"])
        h2 = self.phien("hs.binh")
        kq = self.nop(self.hh)
        self.assertEqual(self.c.get("/api/toi/ket-qua", headers=h2).json(), [])
        self.assertEqual(self.c.get(f"/lab/sinh/ket-qua/{kq['id']}", headers=h2).status_code, 403)
        self.assertEqual(self.c.patch(f"/lab/sinh/ket-qua/{kq['id']}", json={"diem": 10}, headers=self.hh).status_code, 403)

    def test_loc_theo_bai_va_hoc_sinh(self):
        self.nop(self.hh)
        self.nop(self.hh, ma_bai="bai_khac")
        duong = self.duong("/ket-qua")
        self.assertEqual(len(self.c.get(duong, headers=self.h1).json()), 2)
        self.assertEqual(len(self.c.get(duong, params={"ma_bai": "bai_khac"}, headers=self.h1).json()), 1)
        self.assertEqual(len(self.c.get(duong, params={"hoc_sinh_id": self.hs["id"]}, headers=self.h1).json()), 2)
        self.assertEqual(self.c.get(duong, params={"hoc_sinh_id": self.gv2["id"]}, headers=self.h1).json(), [])

    def test_giao_vien_khong_tu_gan_ket_qua_cho_hoc_sinh(self):
        kq = self.nop(self.h1, hoc_sinh_id=self.hs["id"], hoc_sinh="Giả mạo", lop="10A1")
        self.assertIsNone(kq["hoc_sinh_id"])
        self.assertEqual(self.c.get("/api/toi/ket-qua", headers=self.hh).json(), [])

    def test_csv_tieng_viet_va_chan_cong_thuc_excel(self):
        xau = tai_khoan.tao_nguoi_dung("hs.xau", "=HYPERLINK(1)", "hoc_sinh", MK)
        tai_khoan.them_vao_lop(self.lop["id"], xau["id"])
        self.nop(self.phien("hs.xau"))
        r = self.c.get(self.duong("/ket-qua.csv"), headers=self.h1)
        self.assertEqual(r.status_code, 200)
        self.assertTrue(r.content.startswith(b"\xef\xbb\xbf"))
        chu = r.content.decode("utf-8-sig")
        self.assertIn("Học sinh,Lớp,Bài", chu)
        self.assertRegex(chu, r"\d{2}/\d{2}/\d{4} \d{2}:\d{2}")  # thời gian đọc được, không phải số Unix
        self.assertIn("'=HYPERLINK(1)", chu)
        self.assertNotIn(",=HYPERLINK", chu)
        self.assertIn("'=HYPERLINK(1)", self.c.get("/lab/sinh/ket-qua.csv", headers=self.h1).content.decode("utf-8-sig"))


class TrangTest(NenLop):
    def test_trang_co_tab_ket_qua(self):
        self.assertIn('id="tab-ket-qua"', self.c.get("/giao-vien", headers=self.h1).text)
        self.assertIn('id="tab-ket-qua"', self.c.get("/hoc-sinh", headers=self.hh).text)

    def test_cookie_web_dung_duoc_api_lop(self):
        self.c.post("/auth/dang-nhap", json={"ten_dang_nhap": "gv.mot", "mat_khau": MK})
        self.assertIn(dang_nhap.COOKIE, self.c.cookies)
        self.assertEqual(self.c.get("/api/lop").status_code, 200)
        r = self.c.post("/api/lop", json={"ten": "Lớp mới"}, headers={"Origin": "https://trang-la.example"})
        self.assertEqual(r.status_code, 403)
        self.c.post("/auth/dang-xuat")


if __name__ == "__main__":
    unittest.main()
