"""Kiểm thử danh mục hóa chất / thiết bị sinh từ CSV (tools/chuyen_hoa_chat.py)."""

import json
import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CHEM = json.loads((ROOT / "data/chem/hoa_chat.json").read_text(encoding="utf-8"))
EQUIP = json.loads((ROOT / "data/chem/thiet_bi.json").read_text(encoding="utf-8"))
SPECIES_CS = (ROOT / "Phong thi nghiem ao/Assets/Scripts/Simulation/Chem/Species.cs").read_text(encoding="utf-8")


def species_ids() -> set[str]:
    ids = set(re.findall(r'(?:Ion|Weak|Mol|Ppt|Metal|AddRaw|Hydroxide)\("([^"]+)"', SPECIES_CS))
    ids |= set(re.findall(r'Hydroxide\("[^"]+", "([^"]+)"', SPECIES_CS))
    ids |= set(re.findall(r'\("([^"]+\((?:s|l)\))", "', SPECIES_CS))
    ids |= set(re.findall(r'\("(mo|mr|btb|bcg|tb|ay|univ|starch)", "', SPECIES_CS))
    ids |= {"pp"}  # phenolphtalein: trường indicatorDrops của Solution
    return ids


class HoaChatTest(unittest.TestCase):
    def test_du_720_chat_va_80_thiet_bi(self):
        self.assertEqual(len(CHEM["hoa_chat"]), 720)
        self.assertEqual(len(EQUIP["thiet_bi"]), 80)
        self.assertEqual([c["stt"] for c in CHEM["hoa_chat"]], list(range(1, 721)))

    def test_moi_chat_co_vi_tri_rieng(self):
        seen = set()
        shelves = {k["ma"]: k for k in CHEM["ke"]}
        for c in CHEM["hoa_chat"]:
            v = c["vi_tri"]
            self.assertIn(v["ke"], shelves, c["ten"])
            k = shelves[v["ke"]]
            self.assertTrue(1 <= v["tang"] <= k["so_tang"] and 1 <= v["o"] <= k["so_o"], c["ten"])
            key = (v["ke"], v["tang"], v["o"])
            self.assertNotIn(key, seen, c["ten"])
            seen.add(key)

    def test_nhom_logic(self):
        by = {c["stt"]: c for c in CHEM["hoa_chat"]}
        self.assertEqual(by[98]["nhom"], "AX")       # HCl: tủ axit
        self.assertEqual(by[124]["nhom"], "BZ")      # NaOH: tủ bazơ
        self.assertEqual(by[338]["nhom"], "MOX")     # KMnO4: chất oxi hóa
        self.assertEqual(by[149]["nhom"], "MCL")     # NaCl: muối halogenua
        self.assertEqual(by[253]["nhom"], "MSO")     # CuSO4: muối sunfat
        self.assertEqual(by[468]["nhom"], "AN")      # etanol: tủ chất dễ cháy
        self.assertEqual(by[651]["nhom"], "KH")      # khí Cl2
        groups = {g["ma"]: g for g in CHEM["nhom"]}
        self.assertEqual(groups["HC"]["kieu_tu"], "tu_chay")

    def test_thanh_phan_dung_ma_loai_cua_unity(self):
        ids = species_ids()
        for c in CHEM["hoa_chat"]:
            for part in c["tp"]:
                self.assertIn(part["s"], ids, f"{c['stt']} {c['ten']}: loài {part['s']} chưa có trong Species.cs")

    def test_thanh_phan_muoi(self):
        by = {c["stt"]: {p["s"]: p["n"] for p in c["tp"]} for c in CHEM["hoa_chat"]}
        self.assertEqual(by[250], {"Al3+": 2, "SO4": 3})                 # Al2(SO4)3
        self.assertEqual(by[159], {"Fe3+": 1, "Cl-": 3})                 # FeCl3
        self.assertEqual(by[338], {"K+": 1, "MnO4-": 1})                 # KMnO4 (Mn thuộc gốc)
        self.assertEqual(by[342], {"K+": 2, "CrVI": 2})                  # K2Cr2O7
        self.assertEqual(by[385], {"NH3": 2, "Fe2+": 1, "SO4": 2})       # muối Mohr
        self.assertEqual(by[289], {"Na+": 1, "CO3": 1})                  # NaHCO3

    def test_ten_co_dau_va_tim_khong_dau(self):
        by = {c["stt"]: c for c in CHEM["hoa_chat"]}
        self.assertEqual(by[20]["ten"], "Đồng")
        self.assertEqual(by[253]["ten"], "Đồng(II) sunfat")
        self.assertEqual(by[98]["ten"], "Axit clohiđric")
        self.assertEqual(by[253]["ten_khong_dau"], "dong(ii) sunfat")

    def test_khoi_luong_mol_va_nong_do(self):
        by = {c["stt"]: c for c in CHEM["hoa_chat"]}
        self.assertAlmostEqual(by[383]["M"], 249.69, delta=0.1)          # CuSO4.5H2O
        self.assertAlmostEqual(by[98]["C"], 0.1)                         # HCl: ưu tiên 0,1 M
        self.assertAlmostEqual(by[102]["C"], 0.1)
        self.assertGreater(by[176]["C"], 0.5)                            # nước Gia-ven 5 % ≈ 0,67 M

    def test_thiet_bi_co_dau(self):
        names = {e["ten"] for e in EQUIP["thiet_bi"]}
        self.assertIn("Cốc có mỏ", names)
        self.assertIn("Tủ hút khí độc", names)


if __name__ == "__main__":
    unittest.main()
