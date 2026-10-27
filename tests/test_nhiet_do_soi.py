"""Kiểm thử dữ liệu nhiệt độ sôi (data/chem/nhiet_do_soi.json) dùng cho chưng cất, đun hồi lưu, cô cạn (giai đoạn C3)."""

import json
import unicodedata
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "data/chem/nhiet_do_soi.json"
UNITY = ROOT / "Phong thi nghiem ao/Assets/Lab/Data/nhiet_do_soi.json"
BP = json.loads(SRC.read_text(encoding="utf-8"))
CHEM = {c["stt"]: c for c in json.loads((ROOT / "data/chem/hoa_chat.json").read_text(encoding="utf-8"))["hoa_chat"]}


def fold(s: str) -> str:
    s = s.replace("đ", "d").replace("Đ", "D")
    return "".join(ch for ch in unicodedata.normalize("NFD", s) if unicodedata.category(ch) != "Mn").lower().strip()


class NhietDoSoiTest(unittest.TestCase):
    def test_moi_chat_co_trong_danh_muc_va_dung_ten(self):
        seen = set()
        for c in BP["chat"]:
            self.assertIn(c["stt"], CHEM, c["ten"])
            self.assertEqual(fold(c["ten"]), fold(CHEM[c["stt"]]["ten"]), c["stt"])
            self.assertNotIn(c["stt"], seen)
            seen.add(c["stt"])

    def test_gia_tri_hop_ly(self):
        for c in BP["chat"]:
            self.assertTrue(-50 <= c["ts"] <= 400, c["ten"])
            if "dh" in c:
                self.assertTrue(15 <= c["dh"] <= 70, c["ten"])

    def test_cac_chat_chung_cat_quen_thuoc(self):
        ts = {c["ten"]: c["ts"] for c in BP["chat"]}
        self.assertAlmostEqual(ts["Etanol"], 78.4, delta=0.5)
        self.assertAlmostEqual(ts["Nước"], 100.0, delta=0.01)
        self.assertLess(ts["Ete etylic"], ts["Axeton"])
        self.assertLess(ts["Axeton"], ts["Etanol"])
        self.assertLess(ts["Etyl axetat"], ts["Axit axetic"])
        self.assertLess(ts["Etanol"], ts["Isoamyl axetat"])

    def test_chat_long_de_bay_hoi_deu_co_nhiet_do_soi(self):
        have = {c["stt"] for c in BP["chat"]}
        for stt, c in CHEM.items():
            if c["trang_thai"] == "long" and c.get("cong_thuc") and ("de_chay" in c["the"] or "ancol" in c["the"] or "este" in c["the"]):
                self.assertIn(stt, have, c["ten"])

    def test_ban_unity_giong_ban_goc(self):
        self.assertEqual(json.loads(UNITY.read_text(encoding="utf-8")), BP)


if __name__ == "__main__":
    unittest.main()
