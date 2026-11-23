"""Mục Mô hình 3D (màn Giải bài tập): nhận diện chất và dựng 3D bằng RDKit, không cần mạng."""
import math
import unittest
from unittest import mock

from fastapi.testclient import TestClient

from server.ai.co_che import mo_hinh_3d
from server.ai.co_che.may_chu import app


class TestMoHinh3D(unittest.TestCase):
    def setUp(self):
        self.khong_mang = mock.patch.object(mo_hinh_3d, "_pubchem", return_value=None)
        self.khong_mang.start()

    def tearDown(self):
        self.khong_mang.stop()

    def test_ten_viet_va_cong_thuc_thu_gon(self):
        for q, ct in [("etanol", "C2H6O"), ("CH3COOH", "C2H4O2"), ("benzen", "C6H6"), ("axeton", "C3H6O")]:
            r = mo_hinh_3d.tao_mo_hinh(q)
            self.assertEqual(r["cong_thuc"], ct, q)
            self.assertEqual(r["nguon"], "bang_sgk")

    def test_smiles_trung_dang_cong_thuc(self):
        r = mo_hinh_3d.tao_mo_hinh("CCO")
        self.assertEqual(r["cong_thuc"], "C2H6O")
        self.assertEqual(r["nguon"], "smiles")

    def test_hinh_hoc_hop_ly(self):
        r = mo_hinh_3d.tao_mo_hinh("ethanol")
        self.assertEqual(r["so_nguyen_tu"], 9)                       # có H
        nt = r["nguyen_tu"]
        for lk in r["lien_ket"]:
            a, b = nt[lk["a"]], nt[lk["b"]]
            d = math.dist((a["x"], a["y"], a["z"]), (b["x"], b["y"], b["z"]))
            self.assertTrue(0.9 < d < 1.7, f"độ dài liên kết {d:.2f} Å")

    def test_vong_thom_don_doi_xen_ke(self):
        bac = sorted(lk["bac"] for lk in mo_hinh_3d.tao_mo_hinh("benzen")["lien_ket"] if lk["thom"])
        self.assertEqual(bac, [1, 1, 1, 2, 2, 2])

    def test_khong_nhan_ra(self):
        with self.assertRaises(mo_hinh_3d.LoiMoHinh):
            mo_hinh_3d.tao_mo_hinh("chất không có thật")

    def test_api(self):
        c = TestClient(app)
        r = c.post("/co-che/mo-hinh-3d", json={"van_ban": "CH3COOH"})
        self.assertEqual(r.status_code, 200)
        self.assertEqual(r.json()["cong_thuc"], "C2H4O2")
        self.assertEqual(c.post("/co-che/mo-hinh-3d", json={"van_ban": "xyz chưa biết"}).status_code, 422)
        self.assertIn("ethanol", c.get("/co-che/mo-hinh-3d/mau").json())


if __name__ == "__main__":
    unittest.main()
