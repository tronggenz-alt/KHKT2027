"""Kiểm thử theo dõi trực tiếp: vị trí, sự kiện gắn đúng học sinh, thông báo, "Tất cả chú ý", phạm vi của giáo viên."""

import re
import tempfile
import time
import unittest
from pathlib import Path
from unittest import mock

from argon2 import PasswordHasher
from fastapi.testclient import TestClient

from server.ai import main
from server.lab import db, nhip, sinh_db, tai_khoan, truc_tiep

MK = "mat-khau-kiem-thu"
GOC = Path(truc_tiep.__file__).resolve().parents[2]


class NenTrucTiep(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        duong = Path(self.tmp.name) / "lab.db"
        db.reset_for_tests(duong)
        sinh_db.reset_for_tests(duong)
        nhip.xoa()
        self._bam = tai_khoan._bam
        tai_khoan._bam = PasswordHasher(time_cost=1, memory_cost=1024, parallelism=1)
        self.c = TestClient(main.app, base_url="http://192.168.1.12:8000", client=("192.168.1.50", 50000), follow_redirects=False, raise_server_exceptions=False)
        self.gv = tai_khoan.tao_nguoi_dung("gv.mot", "Giáo viên Một", "giao_vien", MK)
        self.gv2 = tai_khoan.tao_nguoi_dung("gv.hai", "Giáo viên Hai", "giao_vien", MK)
        self.an = tai_khoan.tao_nguoi_dung("hs.an", "Học sinh An", "hoc_sinh", MK)
        self.binh = tai_khoan.tao_nguoi_dung("hs.binh", "Học sinh Bình", "hoc_sinh", MK)
        self.ngoai = tai_khoan.tao_nguoi_dung("hs.ngoai", "Học sinh Ngoài", "hoc_sinh", MK)
        self.lop = tai_khoan.tao_lop("10A1", "", self.gv["id"])
        self.lop2 = tai_khoan.tao_lop("10B1", "", self.gv2["id"])
        tai_khoan.them_vao_lop(self.lop["id"], self.an["id"])
        tai_khoan.them_vao_lop(self.lop["id"], self.binh["id"])
        tai_khoan.them_vao_lop(self.lop2["id"], self.ngoai["id"])
        self.h_gv, self.h_gv2 = self.phien("gv.mot"), self.phien("gv.hai")
        self.h_an, self.h_binh, self.h_ngoai = self.phien("hs.an"), self.phien("hs.binh"), self.phien("hs.ngoai")
        self.duong = f"/api/lop/{self.lop['id']}"

    def tearDown(self):
        tai_khoan._bam = self._bam
        db.reset_for_tests(Path(self.tmp.name) / "khac.db")
        sinh_db.reset_for_tests(Path(self.tmp.name) / "khac.db")
        nhip.xoa()
        self.tmp.cleanup()

    def phien(self, ten):
        r = self.c.post("/auth/dang-nhap", json={"ten_dang_nhap": ten, "mat_khau": MK, "kieu": "ung_dung"})
        return {"Authorization": "Bearer " + r.json()["ma_phien"]}

    def nhip(self, h, **body):
        r = self.c.post("/app/nhip", json=body, headers=h)
        self.assertEqual(r.status_code, 200, r.text)
        return r.json()

    def xem(self, sau_id=0):
        r = self.c.get(self.duong + "/truc-tiep", params={"sau_id": sau_id}, headers=self.h_gv)
        self.assertEqual(r.status_code, 200, r.text)
        return r.json()

    def hs(self, tt, nd):
        return next(h for h in tt["hoc_sinh"] if h["id"] == nd["id"])


class MatBangTest(NenTrucTiep):
    def test_khu_trung_voi_bang_trong_unity(self):
        cs = (GOC / "Phong thi nghiem ao/Assets/Scripts/Rendering/ZoneMap.cs").read_text(encoding="utf-8")
        hang = re.findall(r'new Zone \{ id = "(\w+)", name = "([^"]+)".*?MinMaxRect\(([^)]+)\), floor = (-?\d)', cs)
        self.assertEqual(len(hang), len(truc_tiep.KHU))
        doi_so = lambda t: -3.6 if "F2CorridorZ" in t else float(t.strip().rstrip("f"))
        for (ma, ten, rect, tang), k in zip(hang, truc_tiep.KHU):
            x0, z0, x1, z1 = map(doi_so, rect.split(","))
            self.assertEqual((ma, ten, x0, z0, x1, z1, int(tang) + 1), (k["ma"], k["ten"], k["x0"], k["z0"], k["x1"], k["z1"], k["tang"]), ma)

    def test_tim_khu_theo_tang_va_toa_do(self):
        self.assertEqual(truc_tiep.khu_tai(1, 5, 5)["ma"], "phong_lab")
        self.assertEqual(truc_tiep.khu_tai(2, 5, 5)["ma"], "phong_sinh")
        self.assertEqual(truc_tiep.khu_tai(1, 28, 2)["ma"], "loi_thang")
        self.assertEqual(truc_tiep.khu_tai(2, 28, 2)["ma"], "loi_thang")
        self.assertEqual(truc_tiep.khu_tai(1, 2, -3)["ma"], "phong_may")
        self.assertEqual(truc_tiep.khu_tai(2, 2, -3)["ma"], "hanh_lang_t2")
        self.assertIsNone(truc_tiep.khu_tai(1, -5, 5))
        self.assertIsNone(truc_tiep.khu_tai(None, None, None))

    def test_mat_bang_chi_cho_giao_vien(self):
        self.assertEqual(self.c.get("/api/mat-bang", headers=self.h_gv).status_code, 200)
        self.assertEqual(self.c.get("/api/mat-bang", headers=self.h_an).status_code, 403)
        self.assertEqual(self.c.get("/api/mat-bang").status_code, 401)


class ViTriTest(NenTrucTiep):
    def test_giao_vien_thay_hoc_sinh_dang_o_dau(self):
        self.nhip(self.h_an, tang=1, x=5.5, z=7.25, dang_cam="Ống nghiệm 1", dang_lam="Rót dung dịch")
        tt = self.xem()
        an = self.hs(tt, self.an)
        self.assertEqual((tt["so_hoc_sinh"], tt["may_dang_mo"]), (2, 1))
        self.assertEqual((an["dang_mo"], an["tang"], an["x"], an["z"]), (True, 1, 5.5, 7.25))
        self.assertEqual((an["khu"], an["ten_khu"], an["dang_cam"], an["dang_lam"]), ("phong_lab", "Phòng thí nghiệm", "Ống nghiệm 1", "Rót dung dịch"))
        binh = self.hs(tt, self.binh)
        self.assertEqual((binh["dang_mo"], binh["x"], binh["giay_truoc"]), (False, None, None))

    def test_may_tat_thi_khong_hien_vi_tri_cu(self):
        self.nhip(self.h_an, tang=1, x=5, z=5, dang_cam="Ống nghiệm")
        with mock.patch("server.lab.truc_tiep.time.time", return_value=time.time() + nhip.GIAY_CON_MO + 1):
            an = self.hs(self.xem(), self.an)
        self.assertEqual((an["dang_mo"], an["x"], an["khu"], an["dang_cam"]), (False, None, "", ""))
        self.assertGreater(an["giay_truoc"], nhip.GIAY_CON_MO)

    def test_ngoai_toa_nha(self):
        self.nhip(self.h_an, tang=1, x=-20, z=5)
        an = self.hs(self.xem(), self.an)
        self.assertEqual((an["khu"], an["ten_khu"]), ("", "Ngoài trời"))

    def test_vi_tri_khong_ghi_xuong_csdl(self):
        self.nhip(self.h_an, tang=1, x=11.125, z=7.625, dang_lam="Việc rất riêng")
        duong = Path(self.tmp.name) / "lab.db"
        self.assertNotIn("Việc rất riêng".encode(), duong.read_bytes())

    def test_du_lieu_nhip_khong_hop_le(self):
        for body in ({"x": 99999}, {"tang": 9}, {"dang_lam": "x" * 161}):
            self.assertEqual(self.c.post("/app/nhip", json=body, headers=self.h_an).status_code, 422, body)

    def test_khong_gui_vi_tri_van_nhan_viec(self):
        self.assertEqual(self.nhip(self.h_an)["chu_y"], {"bat": False, "noi_dung": ""})
        self.assertEqual(self.hs(self.xem(), self.an)["ten_khu"], "")


class SuKienTest(NenTrucTiep):
    def nhat_ky(self, h, **them):
        body = {"loai": "su_co", "tieu_de": "Khí độc Cl2 thoát ra", "noi_dung": "Trong cốc 1", "muc_do": "Danger", "nguon": "phong_lab_ao", **them}
        self.assertEqual(self.c.post("/api/nhat-ky", json=body, headers=h).status_code, 200)

    def su_kien_sinh(self, h, loai, du_lieu):
        r = self.c.post("/lab/sinh/su-kien", json={"su_kien": [{"loai": loai, "du_lieu": du_lieu}]}, headers=h)
        self.assertEqual(r.json()["loi"], [])

    def test_su_co_hoa_gan_dung_hoc_sinh(self):
        self.nhat_ky(self.h_an)
        tt = self.xem()
        k = tt["su_kien"][0]
        self.assertEqual((k["ho_ten"], k["muc_do"], k["tieu_de"], k["chi_tiet"]), ("Học sinh An", "nguy_hiem", "Khí độc Cl2 thoát ra", "Trong cốc 1"))
        self.assertEqual(self.hs(tt, self.an)["so_nguy_hiem"], 1)
        self.assertEqual(self.hs(tt, self.binh)["so_nguy_hiem"], 0)

    def test_su_co_sinh_vao_cung_dong(self):
        self.su_kien_sinh(self.h_binh, "su_co", {"phong": "phong_sinh", "loai": "dung_lai_dau_con", "muc_do": "canh_bao", "mo_ta": "[micropipet] Dùng lại đầu côn"})
        tt = self.xem()
        self.assertEqual((tt["su_kien"][0]["ho_ten"], tt["su_kien"][0]["muc_do"]), ("Học sinh Bình", "canh_bao"))
        self.assertEqual(self.hs(tt, self.binh)["so_canh_bao"], 1)

    def test_nop_ket_qua_hien_trong_dong(self):
        self.su_kien_sinh(self.h_an, "ket_qua_tn", {"ma_bai": "te_bao_hanh", "du_lieu": {"tom_tat": "x"}})
        k = self.xem()["su_kien"][0]
        self.assertEqual(k["muc_do"], "thong_tin")
        self.assertIn("Quan sát tế bào biểu bì vảy hành", k["tieu_de"])

    def test_hien_tuong_binh_thuong_khong_tinh_la_loi(self):
        self.nhat_ky(self.h_an, loai="hien_tuong", tieu_de="Kết tủa xanh", muc_do="Info")
        tt = self.xem()
        self.assertEqual(tt["su_kien"][0]["muc_do"], "thong_tin")
        self.assertEqual(self.hs(tt, self.an)["so_canh_bao"] + self.hs(tt, self.an)["so_nguy_hiem"], 0)

    def test_giao_vien_va_dung_thu_khong_vao_dong(self):
        self.nhat_ky(self.h_gv)
        cung_may = TestClient(main.app, base_url="http://127.0.0.1:8000", client=("127.0.0.1", 50000), raise_server_exceptions=False)
        self.assertEqual(cung_may.post("/api/nhat-ky", json={"tieu_de": "x", "muc_do": "Danger"}).status_code, 200)
        self.assertEqual(self.xem()["su_kien"], [])
        self.assertEqual(db.query("SELECT COUNT(*) AS n FROM su_kien_hoc_sinh")[0]["n"], 0)

    def test_hoc_sinh_khong_ghi_thay_nguoi_khac(self):
        self.c.post("/api/nhat-ky", json={"tieu_de": "x", "muc_do": "Danger", "hoc_sinh_id": self.binh["id"]}, headers=self.h_an)
        self.assertEqual(self.xem()["su_kien"][0]["ho_ten"], "Học sinh An")

    def test_chi_lay_su_kien_moi(self):
        self.nhat_ky(self.h_an, tieu_de="Một")
        cuoi = self.xem()["su_kien"][-1]["id"]
        self.assertEqual(self.xem(cuoi)["su_kien"], [])
        self.nhat_ky(self.h_an, tieu_de="Hai")
        self.assertEqual([k["tieu_de"] for k in self.xem(cuoi)["su_kien"]], ["Hai"])
        self.assertEqual([k["tieu_de"] for k in self.xem()["su_kien"]], ["Một", "Hai"])

    def test_loi_cu_khong_tinh_vao_buoi_nay(self):
        self.nhat_ky(self.h_an)
        db.execute("UPDATE su_kien_hoc_sinh SET thoi_gian=?", (time.time() - truc_tiep.GIAY_TINH_LOI - 10,))
        self.assertEqual(self.hs(self.xem(), self.an)["so_nguy_hiem"], 0)

    def test_su_kien_qua_cu_bi_xoa(self):
        self.nhat_ky(self.h_an, tieu_de="Rất cũ")
        db.execute("UPDATE su_kien_hoc_sinh SET thoi_gian=?", (time.time() - (truc_tiep.GIU_SU_KIEN_NGAY + 1) * 86400,))
        self.nhat_ky(self.h_an, tieu_de="Mới")
        self.assertEqual([r["tieu_de"] for r in db.query("SELECT tieu_de FROM su_kien_hoc_sinh")], ["Mới"])

    def test_giao_vien_khac_khong_thay_su_kien_lop_nay(self):
        self.nhat_ky(self.h_ngoai, tieu_de="Của lớp khác")
        self.assertEqual(self.xem()["su_kien"], [])


class ThongBaoTest(NenTrucTiep):
    def gui(self, h=None, duong=None, **body):
        return self.c.post((duong or self.duong) + "/thong-bao", json=body, headers=h or self.h_gv)

    def test_ca_lop_nhan_va_bao_da_nhan(self):
        ma = self.gui(noi_dung="Cả lớp dọn bàn").json()["id"]
        for h in (self.h_an, self.h_binh):
            self.assertEqual(self.nhip(h)["lenh"], [{"id": ma, "loai": "thong_bao", "noi_dung": "Cả lớp dọn bàn"}])
        self.assertEqual(self.xem()["thong_bao_cuoi"]["da_nhan"], 0)
        self.nhip(self.h_an, da_nhan_lenh=ma)
        tb = self.xem()["thong_bao_cuoi"]
        self.assertEqual((tb["da_nhan"], tb["gui_toi"], tb["noi_dung"]), (1, "Cả lớp", "Cả lớp dọn bàn"))

    def test_da_nhan_thi_khong_gui_lai(self):
        ma = self.gui(noi_dung="Một lần").json()["id"]
        self.assertEqual(len(self.nhip(self.h_an)["lenh"]), 1)
        self.assertEqual(self.nhip(self.h_an, da_nhan_lenh=ma)["lenh"], [])

    def test_nhan_rieng_mot_hoc_sinh(self):
        self.gui(noi_dung="Bình đeo kính vào", hoc_sinh_id=self.binh["id"])
        self.assertEqual(self.nhip(self.h_an)["lenh"], [])
        self.assertEqual(self.nhip(self.h_binh)["lenh"][0]["noi_dung"], "Bình đeo kính vào")
        self.assertEqual(self.xem()["thong_bao_cuoi"]["gui_toi"], "Học sinh Bình")

    def test_lop_khac_va_giao_vien_khong_nhan(self):
        self.gui(noi_dung="Của lớp 10A1")
        self.assertEqual(self.nhip(self.h_ngoai)["lenh"], [])
        self.assertEqual(self.nhip(self.h_gv)["lenh"], [])

    def test_thong_bao_cu_khong_hien_lai_khi_mo_may(self):
        self.gui(noi_dung="Sáng nay")
        db.execute("UPDATE lenh_app SET tao_luc=?", (time.time() - truc_tiep.GIAY_GIU_THONG_BAO - 1,))
        self.assertEqual(self.nhip(self.h_an)["lenh"], [])

    def test_du_lieu_va_quyen(self):
        self.assertEqual(self.gui(noi_dung="   ").status_code, 422)
        self.assertEqual(self.gui(noi_dung="x" * 301).status_code, 422)
        self.assertEqual(self.gui(noi_dung="x", hoc_sinh_id=self.ngoai["id"]).status_code, 404)
        self.assertEqual(self.gui(h=self.h_gv2, noi_dung="x").status_code, 404)
        self.assertEqual(self.gui(h=self.h_an, noi_dung="x").status_code, 403)
        self.assertEqual(db.query("SELECT COUNT(*) AS n FROM lenh_app")[0]["n"], 0)


class ChuYTest(NenTrucTiep):
    def dat(self, bat, h=None, **them):
        return self.c.post(self.duong + "/chu-y", json={"bat": bat, **them}, headers=h or self.h_gv)

    def test_bat_tat_va_dem_may_da_khoa(self):
        self.assertEqual(self.dat(True, noi_dung="Nhìn lên bảng").status_code, 200)
        self.assertEqual(self.nhip(self.h_an)["chu_y"], {"bat": True, "noi_dung": "Nhìn lên bảng"})
        tt = self.xem()
        self.assertEqual((tt["chu_y"]["bat"], tt["chu_y"]["may_da_khoa"]), (True, 0))
        self.nhip(self.h_an, dang_khoa=True)
        self.assertEqual(self.xem()["chu_y"]["may_da_khoa"], 1)
        self.dat(False)
        self.assertEqual(self.nhip(self.h_an)["chu_y"]["bat"], False)
        self.assertFalse(self.xem()["chu_y"]["bat"])

    def test_noi_dung_mac_dinh(self):
        self.dat(True)
        self.assertEqual(self.nhip(self.h_an)["chu_y"]["noi_dung"], "Tất cả chú ý lên giáo viên")

    def test_may_mo_sau_van_bi_khoa(self):
        self.dat(True)
        nhip.xoa()  # như máy chủ vừa khởi động lại, hoặc học sinh vừa mở app
        self.assertTrue(self.nhip(self.h_binh)["chu_y"]["bat"])

    def test_khong_lan_sang_lop_khac_va_giao_vien(self):
        self.dat(True)
        self.assertFalse(self.nhip(self.h_ngoai)["chu_y"]["bat"])
        self.assertFalse(self.nhip(self.h_gv)["chu_y"]["bat"])

    def test_quyen(self):
        self.assertEqual(self.dat(True, h=self.h_an).status_code, 403)
        self.assertEqual(self.dat(True, h=self.h_gv2).status_code, 404)
        self.assertFalse(self.nhip(self.h_an)["chu_y"]["bat"])


class QuyenTrangTest(NenTrucTiep):
    def test_chi_giao_vien_cua_lop_xem_duoc(self):
        self.nhip(self.h_an, tang=1, x=5, z=5)
        self.assertEqual(self.c.get(self.duong + "/truc-tiep", headers=self.h_gv2).status_code, 404)
        self.assertEqual(self.c.get(self.duong + "/truc-tiep", headers=self.h_an).status_code, 403)
        self.assertEqual(self.c.get(self.duong + "/truc-tiep").status_code, 401)

    def test_trang_theo_doi(self):
        self.assertEqual(self.c.get("/truc-tiep").headers["location"], "/dang-nhap?tiep=%2Ftruc-tiep")
        self.assertEqual(self.c.get("/truc-tiep", headers=self.h_an).headers["location"], "/hoc-sinh")
        r = self.c.get("/truc-tiep", headers=self.h_gv)
        self.assertEqual(r.status_code, 200)
        self.assertIn("/tinh/phien.js", r.text)
        self.assertIn("data-dang-xuat", r.text)

    def test_app_khong_dang_nhap_khong_gui_duoc_nhip(self):
        self.assertEqual(self.c.post("/app/nhip", json={"x": 1}).status_code, 401)


if __name__ == "__main__":
    unittest.main()
