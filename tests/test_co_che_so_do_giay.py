"""Sơ đồ GIẤY kiểu đáp án bài thi (18/11): công thức thu gọn, cặp e tự do, điện tích trong vòng, giai đoạn có tiêu đề,
nhánh sai có nền nhấn, chậm/nhanh. Chỉ dùng ví dụ công khai (SMILES viết trong test); 8 bài demo chạy khi có dữ liệu kín."""

import json
import math
import unittest
from pathlib import Path

from rdkit import Chem

from server.ai.co_che.duong_dan import co_du_lieu_rieng, thu_muc_bai_demo
from server.ai.co_che.hoc_ve import bo_do, thu_gon as tg, ve_theo_mau as vt

VI_DU = Path(__file__).resolve().parents[1] / "server" / "ai" / "co_che" / "vi_du" / "sn2_nho.json"
MT = lambda tu, den, kieu="day_du": {"tu": tu, "den": den, "kieu": kieu}
CAP = lambda m: {"loai": "cap_e", "nguyen_tu": m}
LK = lambda a, b: {"loai": "lien_ket", "nguyen_tu": [a, b]}
NT = lambda m: {"loai": "nguyen_tu", "nguyen_tu": m}

SN1 = {
    "ten": "SN1 thử", "loai": "SN1", "dieu_kien": "H₂O, t°",
    "buoc": [
        {"chat": ["[CH3:1][C:2]([CH3:3])([CH3:4])[Br:5]"], "mui_ten": [MT(LK(2, 5), NT(5))], "buoc_cham": True},
        {"chat": ["[CH3:1][C+:2]([CH3:3])[CH3:4]", "[Br-:5]", "[OH2:6]"], "mui_ten": [MT(CAP(6), NT(2))]},
        {"chat": ["[CH3:1][C:2]([CH3:3])([CH3:4])[OH2+:6]", "[Br-:5]", "[OH2:7]"], "mui_ten": [], "chuyen_h": [{"tu": 6, "den": 7}]},
    ],
    "san_pham": ["[CH3:1][C:2]([CH3:3])([CH3:4])[OH:6]", "[Br-:5]", "[OH3+:7]"],
    "nhanh_sai": [{"tu_buoc": 1, "ten": "SN2 bị chặn", "kieu_chan": "can_tro_khong_gian", "them": ["[OH2:6]"],
                   "mui_ten": [MT(CAP(6), NT(2)), MT(LK(2, 5), NT(5))], "dich": [6, 2]}],
}
CLO = {
    "ten": "Thế gốc thử", "loai": "GOC_TU_DO", "dieu_kien": "hν",
    "buoc": [
        {"chat": ["[Cl:1][Cl:2]"], "mui_ten": [MT(LK(1, 2), NT(1), "nua_dau"), MT(LK(1, 2), NT(2), "nua_dau")]},
        {"chat": ["[Cl:1]", "[CH3:3][H:4]"], "mui_ten": [MT(CAP(1), LK(1, 4), "nua_dau"), MT(LK(3, 4), LK(1, 4), "nua_dau"),
                                                         MT(LK(3, 4), NT(3), "nua_dau")]},
        {"chat": ["[CH3:3]", "[Cl:5][Cl:6]"], "mui_ten": [MT(CAP(3), LK(3, 5), "nua_dau"), MT(LK(5, 6), LK(3, 5), "nua_dau"),
                                                         MT(LK(5, 6), NT(6), "nua_dau")]},
        {"chat": ["[Cl:6]", "[CH3:7]"], "mui_ten": [MT(CAP(6), LK(6, 7), "nua_dau"), MT(CAP(7), LK(6, 7), "nua_dau")]},
    ],
    "san_pham": ["[CH3:7][Cl:6]"],
}


def mol(s):
    m = Chem.MolFromSmiles(s, sanitize=False)
    m.UpdatePropertyCache(strict=False)
    return m


class TestElectron(unittest.TestCase):
    def test_dem_cap_va_goc(self):
        self.assertEqual(tg.so_cap(mol("[Br-]").GetAtomWithIdx(0)), 4)
        self.assertEqual(tg.so_cap(mol("[OH-]").GetAtomWithIdx(0)), 3)
        self.assertEqual(tg.so_cap(mol("C[OH2+]").GetAtomWithIdx(1)), 1)
        self.assertEqual(tg.so_goc(mol("[Cl]").GetAtomWithIdx(0)), 1)
        self.assertEqual(tg.so_goc(mol("[CH3]").GetAtomWithIdx(0)), 1)
        self.assertEqual(tg.so_goc(mol("C[CH+]C").GetAtomWithIdx(1)), 0)


class TestThuGon(unittest.TestCase):
    def test_tbubr_nhu_dap_an(self):
        """H₃C–C(CH₃)₂–Br: Br nằm ngang bên phải C, hai CH₃ dựng đứng trên/dưới (như bản demo)."""
        c = vt.Chat("[CH3:1][C:2]([CH3:3])([CH3:4])[Br:5]", {}, None, True, frozenset({2, 5}))
        self.assertTrue(c.thu_gon)
        P = c.P
        self.assertAlmostEqual(P[4][1], P[1][1], places=3)          # Br cùng hàng với C
        self.assertGreater(P[4][0], P[1][0])
        doc = sorted([P[2][1] - P[1][1], P[3][1] - P[1][1], P[0][1] - P[1][1]])
        self.assertLess(doc[0], -1)
        self.assertGreater(doc[2], 1)
        self.assertEqual(sorted(c.nhan.values()), sorted(["H₃C", "C", "CH₃", "CH₃", "Br"]))

    def test_h_gop_vao_nhan(self):
        c = vt.Chat("[CH3:1][CH:2]([Br:5])[C:3]([H:6])([H:8])[CH3:4]", {}, None, True, frozenset({2, 3, 5, 6, 7}))
        self.assertIn(c.m.GetAtomWithIdx(6).GetIdx(), c.an | {6})
        nhan = set(c.nhan.values())
        self.assertIn("CH", nhan)                                    # C3 còn một H ẩn (H8) → CH, H6 vẽ riêng
        self.assertIn("H", nhan)

    def test_vong_va_lap_the_van_ve_khung(self):
        self.assertFalse(vt.Chat("c1ccccc1", {}, None, True).thu_gon)
        self.assertFalse(vt.Chat("C[C@@H](Br)CC", {}, None, True).thu_gon)


class TestSoDoKieuBaiThi(unittest.TestCase):
    def test_sn2_cong_khai(self):
        sd = vt.tao_so_do(json.loads(VI_DU.read_text(encoding="utf-8")))
        self.assertEqual(sd["phong"], "co_chan")
        self.assertEqual(sd["kieu_cong_thuc"], "thu_gon")
        self.assertEqual(sd["giai_doan"][0]["tieu_de"], "Cơ chế SN2:")
        nhan = [v[2] for v in sd["nt"].values() if len(v) > 2]
        self.assertIn("H₃C", nhan)
        self.assertIn("CH₂", nhan)
        self.assertEqual(sorted(x[2] for x in sd["dt_vong"]), ["−", "−"])     # OH⁻ và Br⁻
        self.assertGreaterEqual(len(sd["cap_e"]), 3 + 3 + 4)
        # mũi tên của OH⁻ xuất phát sát một cặp e
        dau = sd["mt"][0]["tu"]
        self.assertLess(min(math.dist(dau, c[:2]) for c in sd["cap_e"]), .3)
        self.assertNotIn("duoi", sd["pu"][0])                        # cơ chế một bước: không ghi "chậm"

    def test_sn1_nhanh_sai_va_giai_doan(self):
        sd = vt.tao_so_do(SN1)
        gd = sd["giai_doan"]
        self.assertEqual(gd[0]["nen"], "sai")
        self.assertEqual(gd[0]["tieu_de"], "Không theo SN2 (C bậc III bị cản trở không gian):")
        self.assertEqual(gd[1]["tieu_de"], "Cơ chế SN1:")
        self.assertEqual(gd[1]["buoc"], [0, 1, 2])
        self.assertTrue(gd[0]["khung"][3] > gd[1]["khung"][3])       # nhánh sai đứng trước (ở trên)
        gach = [m for m in sd["mt"] if m.get("gach")]
        self.assertEqual(len(gach), 2)
        self.assertEqual(sum(1 for m in gach if m.get("cheo")), 1)
        tren = [p.get("tren") for p in sd["pu"]]
        self.assertEqual(tren[0], "H₂O, t°")
        self.assertEqual(sd["pu"][0].get("duoi"), "chậm")
        self.assertEqual(sd["pu"][1].get("tren"), "nhanh")
        r = bo_do.do_de_nhin(sd)
        self.assertEqual(r["cat_nhau"] + r["chu_de_net"] + r["chu_de_chu"], 0)

    def test_moi_buoc_mot_phuong_trinh(self):
        trai, phai = vt._phuong_trinh(SN1["buoc"][1], SN1["buoc"][2]["chat"])
        self.assertEqual(len(trai), 2)                                # Br⁻ đứng ngoài bị bỏ
        self.assertEqual(len(phai), 1)
        trai, phai = vt._phuong_trinh(CLO["buoc"][0], CLO["buoc"][1]["chat"])
        self.assertEqual(phai, [("[Cl:1]", 2)] if phai[0][0] == "[Cl:1]" else [("[Cl:2]", 2)])   # Cl₂ → 2 Cl•
        trai, phai = vt._phuong_trinh(CLO["buoc"][1], CLO["buoc"][2]["chat"])
        self.assertEqual(len(phai), 2)                                # CH₃• + HCl (HCl không có ở trạng thái sau)

    def test_chuoi_goc_tu_do(self):
        sd = vt.tao_so_do(CLO)
        self.assertEqual([g["tieu_de"] for g in sd["giai_doan"]], ["Khơi mào:", "Phát triển mạch:", "Tắt mạch:"])
        self.assertTrue(sd.get("goc"))
        self.assertIn("2", [c[2] for c in sd.get("chu", [])])


@unittest.skipUnless(co_du_lieu_rieng(), "không có thư mục dữ liệu kín VERA_DU_LIEU_RIENG/co_che/bai_demo")
class TestTamBaiGiay(unittest.TestCase):
    def test_tam_bai(self):
        for p in sorted(thu_muc_bai_demo().glob("*/co_che.json")):
            with self.subTest(bai=p.parent.name):
                sd = vt.tao_so_do(json.loads(p.read_text(encoding="utf-8")))
                self.assertTrue(sd["giai_doan"])
                self.assertTrue(all(g["tieu_de"].endswith(":") for g in sd["giai_doan"]))
                r = bo_do.do_de_nhin(sd)
                self.assertEqual(r["cat_nhau"] + r["chu_de_chu"], 0)
                x0, x1, y0, y1 = sd["khung"]
                for v in sd["nt"].values():
                    self.assertTrue(x0 <= v[0] <= x1 and y0 <= v[1] <= y1)


if __name__ == "__main__":
    unittest.main()
