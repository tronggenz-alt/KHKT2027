"""Kiểm thử mô-đun cơ chế (server/ai/co_che): kiểm tra RDKit, kịch bản 3D đúng HOP_DONG_APP.md, sơ đồ giấy không chồng nhãn.

- Ví dụ SN2 nhỏ nằm trong kho (server/ai/co_che/vi_du/sn2_nho.json) nên test luôn chạy được.
- Có thư mục dữ liệu kín (VERA_DU_LIEU_RIENG/co_che/bai_demo) thì chạy thêm cả 8 bài demo; không có thì bỏ qua.
"""

import copy
import json
import unittest
from pathlib import Path

from server.ai.co_che import co_che, dung_bai_demo, dung_kich_ban_3d as d3
from server.ai.co_che.duong_dan import co_du_lieu_rieng, thu_muc_bai_demo
from server.ai.co_che.hoc_ve import bo_do, ve_theo_mau

VI_DU = Path(__file__).resolve().parents[1] / "server" / "ai" / "co_che" / "vi_du" / "sn2_nho.json"
TAM_BAI = ["sn2_ch3br", "sn1_tbubr", "e2_2_brombutan", "cong_hbr_propen", "clo_hoa_metan", "nitro_hoa_benzen",
           "este_hoa_fischer", "hsg_propellane_a4"]


def kiem_hop_dong_3d(tc: unittest.TestCase, kb: dict) -> None:
    """.3d.json theo HOP_DONG_APP.md mục 1."""
    for k in ("ten", "giai_thich", "nguyen_tu", "trang_thai", "buoc"):
        tc.assertIn(k, kb)
    nt = kb["nguyen_tu"]
    tc.assertTrue(all("e" in v for v in nt.values()))
    for s in kb["trang_thai"]:
        for k in ("ten", "toa_do", "lien_ket", "goc_tu_do"):
            tc.assertIn(k, s)
        for i, xyz in s["toa_do"].items():
            tc.assertIn(i, nt)
            tc.assertEqual(len(xyz), 3)
        for a, b, bac in s["lien_ket"]:
            tc.assertIn(a, s["toa_do"])
            tc.assertIn(b, s["toa_do"])
            tc.assertIn(bac, (1, 2, 3))
        for i in s["goc_tu_do"]:
            tc.assertIn(i, s["toa_do"])
    n = len(kb["trang_thai"])
    co_mui_ten = 0
    for b in kb["buoc"]:
        tc.assertTrue(0 <= b["tu"] < n and 0 <= b["den"] < n)
        for k in ("ten", "ghi_chu", "mui_ten"):
            tc.assertIn(k, b)
        tt = kb["trang_thai"][b["tu"]]["toa_do"]
        for mt in b["mui_ten"]:
            co_mui_ten += 1
            for dau in ("tu", "den"):
                x = mt[dau]["nguyen_tu"]
                for m in (x if isinstance(x, list) else [x]):
                    tc.assertIn(f"m{m}", tt, f"mũi tên trỏ m{m} không có ở trạng thái {b['tu']}")
    tc.assertGreater(co_mui_ten, 0)
    for ns in kb.get("nhanh_sai", []):
        tc.assertIn(ns["tu_trang_thai"], range(n))
        tc.assertEqual(len(ns["man_chan"]["tam"]), 3)


def kiem_giay(tc: unittest.TestCase, sd: dict, ten: str) -> None:
    for k in ("khung", "nt", "lk", "mt", "pu"):
        tc.assertIn(k, sd)
    r = bo_do.do_de_nhin(sd)
    tc.assertEqual(r["chu_de_chu"], 0, f"{ten}: nhãn chồng nhãn")
    tc.assertEqual(r["chu_de_net"], 0, f"{ten}: nhãn đè nét")
    tc.assertEqual(r["cat_nhau"], 0, f"{ten}: nét cắt nhau")


class TestCoCheViDu(unittest.TestCase):
    """Luôn chạy: ví dụ SN2 công khai."""

    @classmethod
    def setUpClass(cls):
        cls.cc = json.loads(VI_DU.read_text(encoding="utf-8"))

    def test_kiem_tra_rdkit_dat(self):
        self.assertEqual(co_che.kiem_tra(co_che.tu_json(self.cc)), [])

    def test_kiem_tra_bat_sai_dien_tich(self):
        sai = copy.deepcopy(self.cc)
        sai["san_pham"] = ["[CH3:2][CH2:3][OH:1]", "[Br:4]"]          # Br trung hoà: không bảo toàn điện tích
        self.assertTrue(co_che.kiem_tra(co_che.tu_json(sai)))

    def test_kiem_tra_bat_sai_mui_ten(self):
        sai = copy.deepcopy(self.cc)
        sai["buoc"][0]["mui_ten"] = sai["buoc"][0]["mui_ten"][:1]       # thiếu mũi tên C–Br đứt → C hoá trị 5
        self.assertTrue(co_che.kiem_tra(co_che.tu_json(sai)))

    def test_kich_ban_3d_dung_hop_dong(self):
        kb = d3.dung(self.cc)
        kiem_hop_dong_3d(self, kb)
        self.assertEqual(kb["trang_thai"][0]["ten"], "Các chất ban đầu")
        cuoi = {tuple(sorted(l[:2])) for l in kb["trang_thai"][-1]["lien_ket"]}
        self.assertIn(("m1", "m3"), cuoi)                                # liên kết C–O mới
        self.assertNotIn(("m3", "m4"), cuoi)                             # C–Br đã đứt

    def test_so_do_giay_khong_chong_nhan(self):
        kiem_giay(self, ve_theo_mau.tao_so_do(self.cc), "sn2_nho")

    def test_nhan_co_dien_tich(self):
        sd = ve_theo_mau.tao_so_do(self.cc)
        nhan = [v[2] for v in sd["nt"].values() if len(v) > 2]
        self.assertIn("OH⁻", nhan)
        self.assertIn("Br⁻", nhan)

    def test_net_dam_gach_cho_tam_lap_the(self):
        sd = ve_theo_mau.so_do_mot_chat("C[C@@H](Br)CC")
        self.assertTrue(any(len(l) > 2 and l[2] in ("dam", "gach") for l in sd["lk"]))


@unittest.skipUnless(co_du_lieu_rieng(), "không có thư mục dữ liệu kín VERA_DU_LIEU_RIENG/co_che/bai_demo")
class TestTamBaiDemo(unittest.TestCase):
    """Chỉ chạy trên máy có dữ liệu kín: 8 bài demo."""

    def test_du_tam_bai(self):
        co = set(dung_bai_demo.ds_bai())
        self.assertTrue(set(TAM_BAI) <= co, f"thiếu: {set(TAM_BAI) - co}")

    def test_tung_bai(self):
        for ma in TAM_BAI:
            with self.subTest(bai=ma):
                cc = json.loads((thu_muc_bai_demo() / ma / "co_che.json").read_text(encoding="utf-8"))
                self.assertEqual(dung_bai_demo.kiem_tra_bai(cc), [])
                kiem_hop_dong_3d(self, d3.dung(cc))
                kiem_giay(self, ve_theo_mau.tao_so_do(cc), ma)
                bai = json.loads((thu_muc_bai_demo() / ma / "bai.json").read_text(encoding="utf-8"))
                for k in ("de", "lop", "muc_tieu", "dieu_kien", "cac_buoc", "cau_hoi", "loi_hay_gap", "nguon"):
                    self.assertIn(k, bai)
                self.assertEqual(len(bai["cau_hoi"]), 3)
                for q in bai["cau_hoi"]:
                    self.assertIn(q["dap_an"], range(len(q["lua_chon"])))

    def test_nhanh_sai_co_man_chan(self):
        for ma in ("sn1_tbubr", "e2_2_brombutan"):
            cc = json.loads((thu_muc_bai_demo() / ma / "co_che.json").read_text(encoding="utf-8"))
            kb = d3.dung(cc)
            self.assertEqual(len(kb.get("nhanh_sai", [])), 1, ma)


if __name__ == "__main__":
    unittest.main()
