"""Kiểm thử điều khiển buổi học: Kiểm tra tắt AI ngay trên máy chủ, khóa hóa chất, lệnh robot từ web."""

import sqlite3
import tempfile
import time
import types
import unittest
from pathlib import Path

from argon2 import PasswordHasher
from fastapi.testclient import TestClient

from server.ai import main, robot_plan
from server.lab import buoi_hoc, db, nhip, sinh_db, tai_khoan

MK = "mat-khau-kiem-thu"


def chat_co_nhan(nhan):
    return next(c for c in db.CHEM["hoa_chat"] if nhan in c["nguy_hiem"])


def chat_an_toan():
    return next(c for c in db.CHEM["hoa_chat"] if not c["nguy_hiem"])


class NenBuoi(unittest.TestCase):
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
        self._pick = main.providers.pick
        main._cache.clear()

    def tearDown(self):
        main.providers.pick = self._pick
        tai_khoan._bam = self._bam
        db.reset_for_tests(Path(self.tmp.name) / "khac.db")
        sinh_db.reset_for_tests(Path(self.tmp.name) / "khac.db")
        nhip.xoa()
        self.tmp.cleanup()

    def phien(self, ten):
        r = self.c.post("/auth/dang-nhap", json={"ten_dang_nhap": ten, "mat_khau": MK, "kieu": "ung_dung"})
        return {"Authorization": "Bearer " + r.json()["ma_phien"]}

    def buoi(self, h=None, duong=None, **body):
        return self.c.post((duong or self.duong) + "/buoi-hoc", json=body, headers=h or self.h_gv)

    def robot(self, h=None, duong=None, **body):
        return self.c.post((duong or self.duong) + "/robot", json=body, headers=h or self.h_gv)

    def nhip(self, h, **body):
        r = self.c.post("/app/nhip", json=body, headers=h)
        self.assertEqual(r.status_code, 200, r.text)
        return r.json()

    def ai_gia(self, structured=None, text="Trả lời từ AI"):
        goi = []

        async def _text(system, user, max_tokens=400):
            goi.append(user)
            return text

        async def _structured(system, user, schema, max_tokens=800):
            goi.append(user)
            return structured

        main.providers.pick = lambda: types.SimpleNamespace(NAME="gia", model_name=lambda: "gia", text=_text, structured=_structured)
        return goi


class CheDoTest(NenBuoi):
    def test_mac_dinh_la_luyen_tap(self):
        tt = self.c.get(self.duong + "/buoi-hoc", headers=self.h_gv).json()
        self.assertEqual((tt["che_do"], tt["so_chat_khoa"], tt["tong_so_chat"]), ("luyen_tap", 0, 720))
        b = self.nhip(self.h_an)["buoi"]
        self.assertEqual((b["che_do"], b["ai_bat"], b["robot_bat"], b["phien_ban"], b["hoa_chat_khoa"]), ("luyen_tap", True, True, 0, None))

    def test_kiem_tra_tat_ai_va_robot_trong_app(self):
        self.assertEqual(self.buoi(che_do="kiem_tra", phut=45).status_code, 200)
        ket = self.nhip(self.h_an)
        b = ket["buoi"]
        self.assertEqual((b["che_do"], b["ai_bat"], b["robot_bat"]), ("kiem_tra", False, False))
        self.assertAlmostEqual(b["het_gio"] - ket["may_chu_luc"], 45 * 60, delta=3)

    def test_ket_thuc_kiem_tra(self):
        self.buoi(che_do="kiem_tra")
        self.buoi(che_do="luyen_tap")
        self.assertTrue(self.nhip(self.h_an)["buoi"]["ai_bat"])

    def test_luyen_tap_khong_co_dong_ho(self):
        self.buoi(che_do="luyen_tap", phut=30)
        self.assertEqual(self.nhip(self.h_an)["buoi"]["het_gio"], 0)

    def test_du_lieu_khong_hop_le(self):
        self.assertEqual(self.buoi(che_do="thi_cu").status_code, 422)
        self.assertEqual(self.buoi(phut=-1).status_code, 422)
        self.assertEqual(self.buoi(khoa_loai=["ma_quy"]).status_code, 422)
        self.assertEqual(self.buoi(ngoai_le=[99999]).status_code, 422)
        self.assertEqual(self.buoi(khoa_them=[99999]).status_code, 422)
        self.assertEqual(self.nhip(self.h_an)["buoi"]["phien_ban"], 0)

    def test_chi_lop_nay_bi_anh_huong(self):
        self.buoi(che_do="kiem_tra")
        self.assertTrue(self.nhip(self.h_ngoai)["buoi"]["ai_bat"])
        self.assertTrue(self.nhip(self.h_gv)["buoi"]["ai_bat"])

    def test_hoc_sinh_hai_lop_theo_lop_nghiem_hon(self):
        tai_khoan.them_vao_lop(self.lop2["id"], self.an["id"])
        self.buoi(duong=f"/api/lop/{self.lop2['id']}", h=self.h_gv2, che_do="kiem_tra")
        self.assertFalse(self.nhip(self.h_an)["buoi"]["ai_bat"])

    def test_quyen(self):
        self.assertEqual(self.buoi(h=self.h_an, che_do="luyen_tap").status_code, 403)
        self.assertEqual(self.buoi(h=self.h_gv2, che_do="kiem_tra").status_code, 404)
        self.assertEqual(self.c.get(self.duong + "/buoi-hoc", headers=self.h_gv2).status_code, 404)
        self.assertEqual(self.c.get(self.duong + "/buoi-hoc", headers=self.h_an).status_code, 403)
        self.assertEqual(self.c.get(self.duong + "/buoi-hoc").status_code, 401)


class ChanAiTest(NenBuoi):
    def goi_ca_bon(self, h):
        return {
            "tro_ly": self.c.post("/tro-ly", json={"cau_hoi": "Làm sao?"}, headers=h).json(),
            "ke_hoach": self.c.post("/robot/ke-hoach", json={"cau_hoi": "Làm giúp"}, headers=h).json(),
            "chu_thich": self.c.post("/chu-thich", json={"tieu_de": "Kết tủa", "chi_tiet": "soạn sẵn"}, headers=h).json(),
            "hoi": self.c.post("/hoi", json={"cau_hoi": "Vì sao?", "ngu_canh": {}}, headers=h).json(),
        }

    def test_dang_kiem_tra_hoc_sinh_khong_goi_duoc_ai(self):
        goi = self.ai_gia({"tra_loi": "AI", "hanh_dong": [], "ke_hoach": []})
        self.buoi(che_do="kiem_tra")
        kq = self.goi_ca_bon(self.h_an)
        self.assertEqual(goi, [])  # không tốn lượt gọi AI nào
        self.assertEqual({v["nguon"] for v in kq.values()}, {"khoa"})
        self.assertEqual(kq["tro_ly"]["tra_loi"], buoi_hoc.LOI_AI_TAT)
        self.assertEqual(kq["ke_hoach"]["ke_hoach"], [])
        self.assertEqual(kq["chu_thich"]["giai_thich"], "")

    def test_het_kiem_tra_goi_lai_duoc(self):
        goi = self.ai_gia({"tra_loi": "AI", "hanh_dong": [], "ke_hoach": []})
        self.buoi(che_do="kiem_tra")
        self.goi_ca_bon(self.h_an)
        self.buoi(che_do="luyen_tap")
        kq = self.goi_ca_bon(self.h_an)
        self.assertEqual(kq["tro_ly"]["nguon"], "gia")
        self.assertGreater(len(goi), 0)

    def test_lop_khac_va_giao_vien_khong_bi_chan(self):
        self.ai_gia({"tra_loi": "AI", "hanh_dong": [], "ke_hoach": []})
        self.buoi(che_do="kiem_tra")
        self.assertEqual(self.goi_ca_bon(self.h_ngoai)["tro_ly"]["nguon"], "gia")
        self.assertEqual(self.goi_ca_bon(self.h_gv)["tro_ly"]["nguon"], "gia")

    def test_dung_thu_khong_thuoc_lop_nao_khong_bi_chan(self):
        self.ai_gia({"tra_loi": "AI", "hanh_dong": [], "ke_hoach": []})
        self.buoi(che_do="kiem_tra")
        cung_may = TestClient(main.app, base_url="http://127.0.0.1:8000", client=("127.0.0.1", 50000), raise_server_exceptions=False)
        self.assertEqual(cung_may.post("/tro-ly", json={"cau_hoi": "x"}).json()["nguon"], "gia")


class KhoaHoaChatTest(NenBuoi):
    def test_khoa_theo_loai_gui_danh_sach_cho_app(self):
        doc = chat_co_nhan("doc")
        an_toan = chat_an_toan()
        tt = self.buoi(khoa_loai=["doc"]).json()
        so_doc = sum(1 for c in db.CHEM["hoa_chat"] if "doc" in c["nguy_hiem"])
        self.assertEqual(tt["so_chat_khoa"], so_doc)
        b = self.nhip(self.h_an)["buoi"]
        self.assertIn(doc["stt"], b["hoa_chat_khoa"])
        self.assertNotIn(an_toan["stt"], b["hoa_chat_khoa"])

    def test_ngoai_le_va_khoa_them(self):
        doc = chat_co_nhan("doc")
        an_toan = chat_an_toan()
        tt = self.buoi(khoa_loai=["doc"], ngoai_le=[doc["stt"]], khoa_them=[an_toan["stt"]]).json()
        self.assertEqual(tt["ngoai_le"][0]["stt"], doc["stt"])
        self.assertTrue(tt["ngoai_le"][0]["ten"])
        b = self.nhip(self.h_an)["buoi"]
        self.assertNotIn(doc["stt"], b["hoa_chat_khoa"])
        self.assertIn(an_toan["stt"], b["hoa_chat_khoa"])

    def test_chi_gui_danh_sach_khi_co_thay_doi(self):
        self.buoi(khoa_loai=["no"])
        b = self.nhip(self.h_an)["buoi"]
        self.assertGreater(len(b["hoa_chat_khoa"]), 0)
        b2 = self.nhip(self.h_an, da_buoi=b["phien_ban"])["buoi"]
        self.assertIsNone(b2["hoa_chat_khoa"])
        self.assertEqual(b2["phien_ban"], b["phien_ban"])
        self.buoi(khoa_loai=["no", "doc"])
        b3 = self.nhip(self.h_an, da_buoi=b["phien_ban"])["buoi"]
        self.assertGreater(b3["phien_ban"], b["phien_ban"])
        self.assertGreater(len(b3["hoa_chat_khoa"]), len(b["hoa_chat_khoa"]))

    def test_ke_hoach_ai_dung_lai_o_hoa_chat_bi_khoa(self):
        doc = chat_co_nhan("doc")
        an_toan = chat_an_toan()
        self.ai_gia({"tra_loi": "Mình làm nhé.", "ke_hoach": [
            {"ky_nang": "lay_hoa_chat", "hoa_chat": an_toan["cong_thuc"] or an_toan["ten"], "doi_tuong": "", "dich": "", "so_luong": 0, "loi": ""},
            {"ky_nang": "lay_hoa_chat", "hoa_chat": doc["ten"], "doi_tuong": "", "dich": "", "so_luong": 0, "loi": ""},
            {"ky_nang": "noi", "hoa_chat": "", "doi_tuong": "", "dich": "", "so_luong": 0, "loi": "Xong"},
        ]})
        main.find_chemical, cu = (lambda q: doc if q == doc["ten"] else an_toan), main.find_chemical
        try:
            self.buoi(khoa_them=[doc["stt"]], ngoai_le=[])
            r = self.c.post("/robot/ke-hoach", json={"cau_hoi": "Làm đi"}, headers=self.h_an).json()
        finally:
            main.find_chemical = cu
        self.assertEqual([b["ky_nang"] for b in r["ke_hoach"]], ["lay_hoa_chat"])
        self.assertTrue(any("đang bị giáo viên khóa" in x for x in r["bo_qua"]))
        self.assertIn("đang bị giáo viên khóa", r["tra_loi"])

    def test_tro_ly_khong_mang_hoa_chat_bi_khoa(self):
        doc = chat_co_nhan("doc")
        self.ai_gia({"tra_loi": "Mình mang ra.", "hanh_dong": [{"loai": "mang_hoa_chat", "hoa_chat": doc["ten"]}]})
        main.find_chemical, cu = (lambda q: doc), main.find_chemical
        try:
            self.buoi(khoa_them=[doc["stt"]])
            bi_khoa = self.c.post("/tro-ly", json={"cau_hoi": "Mang ra"}, headers=self.h_an).json()
            duoc = self.c.post("/tro-ly", json={"cau_hoi": "Mang ra"}, headers=self.h_ngoai).json()
        finally:
            main.find_chemical = cu
        self.assertEqual(bi_khoa["hanh_dong"], [])
        self.assertIn("khóa", bi_khoa["tra_loi"])
        self.assertEqual(duoc["hanh_dong"][0]["stt"], doc["stt"])

    def test_loai_nguy_hiem_dem_dung(self):
        tt = self.c.get(self.duong + "/buoi-hoc", headers=self.h_gv).json()
        dem = {l["ma"]: l["so_chat"] for l in tt["loai_nguy_hiem"]}
        self.assertEqual(set(dem), set(buoi_hoc.LOAI_NGUY_HIEM))
        self.assertEqual(dem["no"], sum(1 for c in db.CHEM["hoa_chat"] if "no" in c["nguy_hiem"]))


class LenhRobotTest(NenBuoi):
    def test_lam_mau_toi_ca_lop_dung_dinh_dang_ke_hoach(self):
        r = self.robot(hanh_dong="lam_mau", khoa="nhan_biet_cu2")
        self.assertEqual(r.status_code, 200, r.text)
        for h in (self.h_an, self.h_binh):
            lenh = self.nhip(h)["lenh"]
            self.assertEqual((lenh[0]["loai"], lenh[0]["id"]), ("robot", r.json()["id"]))
            self.assertIn("Nhận biết", lenh[0]["noi_dung"])
            buoc = lenh[0]["ke_hoach"][0]
            self.assertEqual((buoc["ky_nang"], buoc["doi_tuong"]), ("thi_nghiem_mau", "nhan_biet_cu2"))
            self.assertEqual(set(buoc), {"ky_nang", "doi_tuong", "dich", "so_luong", "stt", "loi"})

    def test_moi_thi_nghiem_mau_deu_ra_lenh_duoc(self):
        for khoa in robot_plan.TEMPLATES:
            self.assertEqual(self.robot(hanh_dong="lam_mau", khoa=khoa).json()["ke_hoach"][0]["doi_tuong"], khoa)

    def test_lay_hoa_chat_theo_so_thu_tu(self):
        c = chat_an_toan()
        buoc = self.robot(hanh_dong="lay_hoa_chat", stt=c["stt"]).json()["ke_hoach"][0]
        self.assertEqual((buoc["ky_nang"], buoc["stt"], buoc["doi_tuong"]), ("lay_hoa_chat", c["stt"], f"hoa_chat:{c['stt']}"))

    def test_cac_lenh_an_toan_va_noi(self):
        for hd in ("tat_lua", "bat_tu_hut", "don_ban"):
            self.assertEqual(self.robot(hanh_dong=hd).json()["ke_hoach"][0]["ky_nang"], hd)
        buoc = self.robot(hanh_dong="noi", loi="Các em chú ý").json()["ke_hoach"][0]
        self.assertEqual((buoc["ky_nang"], buoc["loi"]), ("noi", "Các em chú ý"))

    def test_gui_rieng_mot_hoc_sinh(self):
        self.robot(hanh_dong="don_ban", hoc_sinh_id=self.binh["id"])
        self.assertEqual(self.nhip(self.h_an)["lenh"], [])
        self.assertEqual(len(self.nhip(self.h_binh)["lenh"]), 1)

    def test_lenh_cu_khong_chay_lai_khi_mo_may(self):
        self.robot(hanh_dong="don_ban")
        db.execute("UPDATE lenh_app SET tao_luc=?", (time.time() - buoi_hoc.GIAY_GIU_LENH_ROBOT - 1,))
        self.assertEqual(self.nhip(self.h_an)["lenh"], [])

    def test_da_nhan_thi_khong_chay_lai_va_dem_may(self):
        ma = self.robot(hanh_dong="don_ban").json()["id"]
        self.assertEqual(len(self.nhip(self.h_an)["lenh"]), 1)
        self.assertEqual(self.nhip(self.h_an, da_nhan_lenh=ma)["lenh"], [])
        cuoi = self.c.get(self.duong + "/buoi-hoc", headers=self.h_gv).json()["lenh_robot_cuoi"]
        self.assertEqual((cuoi["da_nhan"], cuoi["noi_dung"]), (1, "Dọn bàn"))

    def test_lenh_robot_khong_vao_muc_thong_bao(self):
        self.robot(hanh_dong="don_ban")
        self.assertIsNone(self.c.get(self.duong + "/truc-tiep", headers=self.h_gv).json()["thong_bao_cuoi"])

    def test_giao_vien_chay_duoc_robot_ca_khi_dang_kiem_tra(self):
        self.buoi(che_do="kiem_tra")
        self.assertEqual(self.robot(hanh_dong="tat_lua").status_code, 200)
        self.assertEqual(self.nhip(self.h_an)["lenh"][0]["ke_hoach"][0]["ky_nang"], "tat_lua")

    def test_lenh_khong_hop_le(self):
        self.assertEqual(self.robot(hanh_dong="lam_mau", khoa="khong_co").status_code, 422)
        self.assertEqual(self.robot(hanh_dong="lay_hoa_chat", stt=99999).status_code, 422)
        self.assertEqual(self.robot(hanh_dong="noi", loi="  ").status_code, 422)
        self.assertEqual(self.robot(hanh_dong="rot").status_code, 422)  # chỉ các lệnh có trong danh sách của giáo viên
        self.assertEqual(self.robot(hanh_dong="don_ban", hoc_sinh_id=self.ngoai["id"]).status_code, 404)
        self.assertEqual(db.query("SELECT COUNT(*) AS n FROM lenh_app")[0]["n"], 0)

    def test_quyen(self):
        self.assertEqual(self.robot(h=self.h_an, hanh_dong="don_ban").status_code, 403)
        self.assertEqual(self.robot(h=self.h_gv2, hanh_dong="don_ban").status_code, 404)
        self.assertEqual(self.c.post(self.duong + "/robot", json={"hanh_dong": "don_ban"}).status_code, 401)


class CsdlVaTrangTest(NenBuoi):
    def test_csdl_cu_duoc_them_cot(self):
        duong = Path(self.tmp.name) / "cu.db"
        conn = sqlite3.connect(duong)
        conn.executescript("""CREATE TABLE lenh_app (id INTEGER PRIMARY KEY AUTOINCREMENT, lop_id INTEGER NOT NULL, hoc_sinh_id INTEGER, loai TEXT NOT NULL, noi_dung TEXT DEFAULT '', tao_luc REAL NOT NULL);
            INSERT INTO lenh_app (lop_id, loai, noi_dung, tao_luc) VALUES (1, 'thong_bao', 'Tin cũ', 1);""")
        conn.commit()
        conn.close()
        db.reset_for_tests(duong)
        self.assertEqual(db.query("SELECT noi_dung, ke_hoach FROM lenh_app"), [{"noi_dung": "Tin cũ", "ke_hoach": ""}])

    def test_trang_dieu_khien_chi_cho_giao_vien(self):
        self.assertEqual(self.c.get("/buoi-hoc").headers["location"], "/dang-nhap?tiep=%2Fbuoi-hoc")
        self.assertEqual(self.c.get("/buoi-hoc", headers=self.h_an).headers["location"], "/hoc-sinh")
        r = self.c.get("/buoi-hoc", headers=self.h_gv)
        self.assertEqual(r.status_code, 200)
        self.assertIn("/tinh/phien.js", r.text)
        self.assertIn("data-dang-xuat", r.text)


if __name__ == "__main__":
    unittest.main()
