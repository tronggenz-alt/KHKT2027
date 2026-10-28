"""Kiểm thử dữ liệu máy phân tích (data/may): đủ phần cho học sinh, mọi thông số có nguồn, số liệu khớp nguồn đã trích."""

import json
import unittest
from pathlib import Path

GOC = Path(__file__).resolve().parents[1]
MAY = GOC / "data" / "may"
UNITY = GOC / "Phong thi nghiem ao" / "Assets" / "Lab" / "Data" / "May"


def doc(p: Path):
    return json.loads(p.read_text(encoding="utf-8"))


class DuLieuMayTest(unittest.TestCase):
    def test_moi_may_du_phan(self):
        files = sorted(MAY.glob("*.json"))
        self.assertGreaterEqual(len(files), 2)
        for f in files:
            m = doc(f)
            with self.subTest(may=f.name):
                for k in ("ma", "ten", "may_mau", "nguyen_ly", "bo_phan", "thong_so", "quy_trinh", "an_toan", "thuat_ngu", "cau_hoi_on_tap"):
                    self.assertIn(k, m)
                self.assertEqual(m["ma"], f.stem)
                self.assertGreaterEqual(len(m["nguyen_ly"]), 3)
                self.assertTrue(3 <= len(m["cau_hoi_on_tap"]) <= 6)
                for b in m["bo_phan"]:
                    self.assertTrue(b["ma"] and b["ten"] and b["chuc_nang"])
                ma_buoc = [s["ma"] for s in m["quy_trinh"]]
                self.assertEqual(len(ma_buoc), len(set(ma_buoc)))
                for s in m["quy_trinh"]:
                    self.assertTrue(s["ten"] and s["viec"] and s["ly_do"])

    def test_moi_thong_so_co_nguon_hoac_ghi_mo_phong(self):
        for f in sorted(MAY.glob("*.json")):
            for t in doc(f)["thong_so"]:
                with self.subTest(may=f.name, thong_so=t["ten"]):
                    if t.get("mo_phong"):
                        continue
                    self.assertTrue(t.get("nguon", "").startswith("https://"), t)

    def test_pho_uv_vis_co_nguon_va_dung_so_lieu(self):
        uv = doc(MAY / "tinh_chat" / "uv_vis.json")
        by = {c["id"]: c for c in uv["chat"]}
        for c in uv["chat"]:
            with self.subTest(chat=c["id"]):
                self.assertTrue(c["nguon"].startswith("https://"))
                self.assertTrue("dai" in c or "diem" in c)
                for d in c.get("dai", []):
                    self.assertTrue(190 <= d["l"] <= 1100 and d["eps"] > 0 and d["sigma"] > 0)
        self.assertAlmostEqual(by["MnO4-"]["dai"][0]["eps"], 2240, delta=1)
        self.assertEqual(by["MnO4-"]["dai"][0]["l"], 525)
        # NIST SRM 935a: εa(350 nm) = 10,672 kg·g⁻¹·cm⁻¹ ở 0,020 g/kg; theo mol Cr = εa × 294,18 / 2
        diem = dict(by["CrVI"]["diem"])
        self.assertAlmostEqual(diem[350], 10.672 * 294.18 / 2, delta=1.5)
        self.assertAlmostEqual(diem[257], 14.262 * 294.18 / 2, delta=1.5)
        self.assertEqual(by["FeSCN"]["dai"][0]["l"], 447)
        # [Cu(NH3)4]2+: dải Gauss đỉnh 610 nm phải cho ε(640 nm) ≈ 77
        import math
        d = by["CuNH3"]["dai"][0]
        e640 = d["eps"] * math.exp(-((640 - d["l"]) ** 2) / (2 * d["sigma"] ** 2))
        self.assertAlmostEqual(e640, 77, delta=3)

    def test_bang_aas_dung_so_cua_hang(self):
        aas = doc(MAY / "tinh_chat" / "aas_nguyen_to.json")
        self.assertTrue(aas["nguon"].startswith("https://"))
        by = {e["ky_hieu"]: e for e in aas["nguyen_to"]}
        for e in aas["nguyen_to"]:
            with self.subTest(nguyen_to=e["ky_hieu"]):
                self.assertTrue(190 <= e["l"] <= 860)
                self.assertIn(e["khe"], (0.2, 0.5, 1.0))
                self.assertLess(e["khoang"][0], e["khoang"][1])
                self.assertIn(e["ngon_lua"], ("khong_khi", "n2o"))
        self.assertEqual((by["Cu"]["l"], by["Cu"]["khe"], by["Cu"]["khoang"]), (324.7, 0.5, [0.03, 10]))
        self.assertEqual((by["Zn"]["l"], by["Zn"]["khe"], by["Zn"]["khoang"]), (213.9, 1.0, [0.01, 2]))
        self.assertEqual((by["Ni"]["l"], by["Ni"]["khoang"]), (232.0, [0.1, 20]))
        self.assertTrue(by["Al"].get("chi_n2o"))

    def test_thu_vien_gc_ms_theo_nist(self):
        gc = doc(MAY / "tinh_chat" / "gc_ms.json")
        by = {c["id"]: c for c in gc["chat"]}
        for c in gc["chat"]:
            with self.subTest(chat=c["id"]):
                self.assertTrue(c["nguon"].startswith("https://webbook.nist.gov/"))
                mz = [p[0] for p in c["pho"]]
                self.assertEqual(mz, sorted(mz))
                self.assertEqual(max(p[1] for p in c["pho"]), 999)
                self.assertTrue(300 <= c["ri"] <= 1000)
        # pic cơ bản theo phổ EI của NIST
        base = {k: max(v["pho"], key=lambda p: p[1])[0] for k, v in by.items()}
        self.assertEqual((base["metanol"], base["etanol"], base["axeton"], base["etyl_axetat"], base["isoamyl_axetat"]), (31, 31, 43, 43, 43))
        # ion phân tử của etanol (46) và axit axetic (60) có trong phổ
        self.assertIn(46, [p[0] for p in by["etanol"]["pho"]])
        self.assertIn(60, [p[0] for p in by["axit_axetic"]["pho"]])
        # thứ tự chỉ số lưu trên cột 5% phenyl (trung vị NIST)
        ri = [by[k]["ri"] for k in ("metanol", "etanol", "axeton", "etyl_axetat", "axit_axetic", "isoamyl_ancol", "isoamyl_axetat")]
        self.assertEqual(ri, sorted(ri))
        self.assertEqual((by["etanol"]["ri"], by["isoamyl_axetat"]["ri"]), (445, 876))

    def test_thu_vien_ftir_hplc(self):
        ir = doc(MAY / "tinh_chat" / "ftir.json")
        self.assertTrue(ir["nguon_bang"].startswith("https://"))
        for c in ir["chat"]:
            with self.subTest(chat=c["id"]):
                self.assertIn(c["kieu"], ("long", "tan"))
                for nu, a, w in c["dai"] + c.get("dai_ion", []):
                    self.assertTrue(400 <= nu <= 4000 and 0 < a <= 1 and 5 <= w <= 600)
        by = {c["id"]: c for c in ir["chat"]}
        # C=O este 1735 – 1750, C=O axit 1700 – 1725, COO⁻ 1540 – 1610 (bảng tần số nhóm chức)
        self.assertTrue(any(1735 <= d[0] <= 1750 for d in by["isoamyl_axetat"]["dai"]))
        self.assertTrue(any(1700 <= d[0] <= 1725 for d in by["axit_axetic"]["dai"]))
        self.assertTrue(any(1540 <= d[0] <= 1610 for d in by["axit_axetic"]["dai_ion"]))
        lc = doc(MAY / "tinh_chat" / "hplc.json")
        ids = {m["id"]: m for m in lc["phuong_phap"]}
        self.assertEqual(set(ids), {"axit_huu_co", "duong", "bao_quan"})
        self.assertEqual(ids["duong"]["detector"], "ri")
        # thứ tự ra trên cột NH₂ như tài liệu Supelco: fructozơ < glucozơ < saccarozơ < mantozơ < lactozơ
        k = [c["log_kw"] for c in ids["duong"]["chat"]]
        self.assertEqual([c["id"] for c in ids["duong"]["chat"]], ["fructozo", "glucozo", "saccarozo", "mantozo", "lactozo"])
        self.assertEqual(k, sorted(k))

    def test_ban_unity_giong_ban_goc(self):
        for f in sorted(MAY.rglob("*.json")):
            with self.subTest(file=str(f.relative_to(MAY))):
                u = UNITY / f.relative_to(MAY)
                self.assertTrue(u.exists(), f"chưa chép sang Unity: chạy python tools/chep_du_lieu_may.py")
                self.assertEqual(doc(u), doc(f))


if __name__ == "__main__":
    unittest.main()
