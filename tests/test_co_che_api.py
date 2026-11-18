"""Kiểm thử máy chủ cơ chế (server/ai/co_che/may_chu.py) bằng FastAPI TestClient – khép kín: thư mục dữ liệu riêng, build và
khóa đều tạm; AI tại máy và trực tuyến được thay bằng hàm giả (không gọi mạng)."""

import asyncio
import json
import os
import shutil
import tempfile
import unittest
from pathlib import Path
from unittest import mock

from fastapi.testclient import TestClient

from server.ai.co_che import dong_goi, dung_bai_demo, giai_chuoi, giai_thich, tri_thuc
from server.ai.co_che.may_chu import app

VI_DU = Path(__file__).resolve().parents[1] / "server" / "ai" / "co_che" / "vi_du" / "sn2_nho.json"


class TestMayChuCoChe(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.tmp = tempfile.mkdtemp(prefix="vera_co_che_")
        goc = Path(cls.tmp)
        bai = goc / "rieng" / "co_che" / "bai_demo" / "sn2_nho"
        bai.mkdir(parents=True)
        shutil.copy(VI_DU, bai / "co_che.json")
        (bai / "bai.json").write_text(json.dumps({"ma": "sn2_nho", "de": "Thủy phân bromoethane bằng NaOH loãng.", "lop": 11,
                                                  "cac_buoc": [{"ten": "Bước 1", "giai_thich": "OH⁻ tấn công từ phía sau."}]},
                                                 ensure_ascii=False), encoding="utf-8")
        cls.env = mock.patch.dict(os.environ, {"VERA_DU_LIEU_RIENG": str(goc / "rieng"), "VERA_CO_CHE_BUILD": str(goc / "build"),
                                               "VERA_KHOA_CO_CHE": str(goc / "khoa" / "k.key"), "RAG_TAT": "1"})
        cls.env.start()
        assert dung_bai_demo.main([]) == 0
        dong_goi.doc_khoa(tao_neu_chua_co=True)
        tri_thuc.dat_kho(None)
        cls.c = TestClient(app)

    @classmethod
    def tearDownClass(cls):
        cls.env.stop()
        tri_thuc.dat_kho(None)
        shutil.rmtree(cls.tmp, ignore_errors=True)

    def test_suc_khoe(self):
        r = self.c.get("/co-che/suc-khoe")
        self.assertEqual(r.status_code, 200)
        self.assertEqual(r.json()["so_bai"], 1)

    def test_danh_sach_bai(self):
        r = self.c.get("/co-che/bai")
        self.assertEqual([b["ma"] for b in r.json()], ["sn2_nho"])

    def test_goi_bai_giai_duoc(self):
        r = self.c.get("/co-che/bai/sn2_nho")
        self.assertEqual(r.status_code, 200)
        self.assertEqual(r.content[:5], b"VERA1")
        d = dong_goi.mo_goi(r.content, dong_goi.doc_khoa())
        self.assertEqual(d["ma"], "sn2_nho")
        for k in ("bai", "kich_ban_3d", "so_do_giay", "_d"):
            self.assertIn(k, d)

    def test_goi_bai_khong_co(self):
        self.assertEqual(self.c.get("/co-che/bai/khong_co").status_code, 404)
        self.assertEqual(self.c.get("/co-che/bai/..%2Fx").status_code, 404)

    def test_chuoi_mau_giai_khong_can_mang(self):
        self.assertEqual(len(self.c.get("/co-che/chuoi-mau").json()), 3)
        with mock.patch.object(giai_chuoi, "_nho_ai", side_effect=AssertionError("không được gọi AI")):
            for ch in giai_chuoi.CHUOI_MAU:
                r = self.c.post("/co-che/giai-chuoi", json={"chuoi": ch})
                self.assertEqual(r.status_code, 200, ch)
                kq = r.json()
                self.assertTrue(kq["da_kiem_chung"], ch)
                self.assertTrue(any(b["ma_co_che"] for b in kq["buoc"]), ch)

    def test_giai_chuoi_vi_du_de(self):
        r = self.c.post("/co-che/giai-chuoi", json={"chuoi": "C2H4 -> C2H5OH -> CH3CHO -> CH3COOH"})
        b = r.json()["buoc"]
        self.assertEqual([x["den"] for x in b], ["C2H5OH", "CH3CHO", "CH3COOH"])
        self.assertIn("CuO", b[1]["dieu_kien"])

    def test_giai_chuoi_la_ghi_chua_kiem_chung(self):
        async def ai_gia(chat, chuoi):
            return {"chuoi": [{"ki_hieu": "X", "ten": "ethanal", "smiles": "CC=O"}], "buoc": [{"tu": "C2H5OH", "den": "X", "phan_ung": "oxi hoá"}]}
        with mock.patch.object(giai_chuoi, "_nho_ai", ai_gia):
            kq = self.c.post("/co-che/giai-chuoi", json={"chuoi": "C2H5OH -> X"}).json()
        self.assertFalse(kq["da_kiem_chung"])
        self.assertEqual(kq["chat"][1]["kiem_chung"], "chua_kiem_chung")
        self.assertEqual(kq["buoc"][0]["phan_ung"], "oxi hoá không hoàn toàn bằng CuO")   # CCO → CC=O tra được trong bảng
        self.assertEqual(kq["buoc"][0]["kiem_chung"], "chua_kiem_chung")                # nhưng X do AI nhận: chưa kiểm chứng

    def test_giai_chuoi_sai_dinh_dang(self):
        self.assertEqual(self.c.post("/co-che/giai-chuoi", json={"chuoi": "C2H4"}).status_code, 422)

    def test_giai_thich_khong_co_ai_tra_loi_mau(self):
        with mock.patch.object(giai_thich, "goi_tai_may", side_effect=ConnectionError("tắt")), \
                mock.patch("server.ai.providers.pick", return_value=None):
            kq = self.c.post("/co-che/giai-thich", json={"cau_hoi": "Vì sao tấn công từ phía sau?", "ma_bai": "sn2_nho"}).json()
        self.assertEqual(kq["kiem_chung"], "mau")
        self.assertIn("OH⁻", kq["tra_loi"])

    def test_giai_thich_ai_tai_may_co_nguon(self):
        async def gia(system, user, max_tokens=400):
            self.assertIn("KHÔNG tự đề xuất cơ chế mới", system)
            return "OH⁻ tấn công C từ phía sau [1].", "vera-hoa-3b"
        with mock.patch.object(giai_thich, "goi_tai_may", gia):
            kq = self.c.post("/co-che/giai-thich", json={"cau_hoi": "SN2 là gì?", "ma_bai": "sn2_nho"}).json()
        self.assertEqual(kq["nha_cung_cap"], "tai_may")
        self.assertEqual(kq["kiem_chung"], "ai_theo_tai_lieu")

    def test_giai_thich_ai_de_xuat_co_che_sai_bi_thay(self):
        sai = json.dumps({"buoc": [{"chat": ["[OH-:1]", "[CH3:2][Br:3]"],
                                    "mui_ten": [{"tu": {"loai": "cap_e", "nguyen_tu": 1}, "den": {"loai": "nguyen_tu", "nguyen_tu": 2}}]}],
                          "san_pham": ["[CH3:2][OH:1]", "[Br-:3]"]})

        async def gia(system, user, max_tokens=400):
            return "Cơ chế: " + sai, "vera-hoa-3b"
        with mock.patch.object(giai_thich, "goi_tai_may", gia):
            kq = self.c.post("/co-che/giai-thich", json={"cau_hoi": "Viết cơ chế", "ma_bai": "sn2_nho"}).json()
        self.assertEqual(kq["kiem_chung"], "mau_do_ai_truot")
        self.assertNotIn("[OH-:1]", kq["tra_loi"])


class TestCauHinhMay(unittest.TestCase):
    def test_chon_theo_phan_cung(self):
        from server.ai.cau_hinh_may import chon
        self.assertEqual(chon(8, 0)[1], "vera-hoa-3b")
        self.assertEqual(chon(16, 0)[1], "qwen2.5:7b-instruct")
        self.assertEqual(chon(64, 24)[1], "vera-hoa-32b")
        self.assertLess(chon(8, 0)[2], chon(64, 24)[2])                    # máy yếu thử ít cấu dạng hơn

    def test_ghi_de_bien_moi_truong(self):
        from server.ai.cau_hinh_may import thong_so
        with mock.patch.dict(os.environ, {"VERA_RAM_GB": "32", "VERA_SO_CAU_DANG": "5", "VERA_MO_HINH": "x"}):
            ts = thong_so()
        self.assertEqual((ts.ram_gb, ts.so_cau_dang, ts.mo_hinh), (32.0, 5, "x"))
        with mock.patch.dict(os.environ, {"VERA_RAM_GB": "16", "VERA_VRAM_GB": "0"}):
            self.assertEqual(thong_so().mo_hinh, "qwen2.5:7b-instruct")

    def test_doc_phan_cung_khong_loi(self):
        from server.ai.cau_hinh_may import doc_phan_cung
        chip, ram, gpu, vram = doc_phan_cung()
        self.assertGreater(ram, 0)


if __name__ == "__main__":
    unittest.main()
