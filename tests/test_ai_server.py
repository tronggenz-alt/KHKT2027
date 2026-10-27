"""Kiểm thử máy chủ AI với nhà cung cấp giả (không gọi mạng, không cần khóa API)."""

import types
import unittest

from fastapi.testclient import TestClient

from server.ai import main


def fake_provider(structured_reply=None, text_reply="Giải thích thử."):
    calls = []

    async def text(system, user, max_tokens=400):
        calls.append(("text", system, user))
        return text_reply

    async def structured(system, user, schema, max_tokens=800):
        calls.append(("structured", system, user, schema))
        return structured_reply

    mod = types.SimpleNamespace(NAME="gia", model_name=lambda: "mo-hinh-gia", text=text, structured=structured)
    return mod, calls


class AiServerTest(unittest.TestCase):
    def setUp(self):
        self.client = TestClient(main.app)
        self._pick = main.providers.pick
        main._cache.clear()

    def tearDown(self):
        main.providers.pick = self._pick

    def test_khong_co_ai(self):
        main.providers.pick = lambda: None
        self.assertFalse(self.client.get("/suc-khoe").json()["ok"])
        r = self.client.post("/chu-thich", json={"tieu_de": "Kết tủa trắng AgCl", "chi_tiet": "soạn sẵn"}).json()
        self.assertEqual(r["giai_thich"], "soạn sẵn")

    def test_chu_thich_gui_du_kien(self):
        mod, calls = fake_provider()
        main.providers.pick = lambda: mod
        r = self.client.post("/chu-thich", json={"tieu_de": "Kết tủa trắng AgCl", "phuong_trinh": "Ag⁺ + Cl⁻ → AgCl↓",
                                                 "thanh_phan": ["AgCl↓"], "ph": 6.5}).json()
        self.assertEqual(r["nguon"], "llm")
        self.assertIn("AgCl", calls[0][2])
        r2 = self.client.post("/chu-thich", json={"tieu_de": "Kết tủa trắng AgCl", "phuong_trinh": "Ag⁺ + Cl⁻ → AgCl↓",
                                                  "thanh_phan": ["AgCl↓"]}).json()
        self.assertEqual(r2["nguon"], "cache")

    def test_tro_ly_doi_ten_hoa_chat_thanh_so_thu_tu(self):
        mod, _ = fake_provider({"tra_loi": "Mình mang CuSO4 ra ngay.", "hanh_dong": [
            {"loai": "mang_hoa_chat", "hoa_chat": "CuSO4"},
            {"loai": "bat_tu_hut", "hoa_chat": ""},
            {"loai": "xoa_phong_lab", "hoa_chat": ""},  # hành động lạ bị bỏ
        ]})
        main.providers.pick = lambda: mod
        r = self.client.post("/tro-ly", json={"cau_hoi": "lấy đồng sunfat", "ngu_canh": "tự do"}).json()
        self.assertEqual([a["loai"] for a in r["hanh_dong"]], ["mang_hoa_chat", "bat_tu_hut"])
        self.assertEqual(r["hanh_dong"][0]["stt"], 253)

    def test_tim_hoa_chat_khong_dau(self):
        self.assertEqual(main.find_chemical("kali pemanganat")["stt"], 338)
        self.assertEqual(main.find_chemical("NaOH")["stt"], 124)
        self.assertIsNone(main.find_chemical("xyz không có"))

    def test_luoc_do_hanh_dong_hop_le(self):
        s = main.ASSIST_SCHEMA
        self.assertFalse(s["additionalProperties"])
        self.assertEqual(set(s["required"]), {"tra_loi", "hanh_dong"})


if __name__ == "__main__":
    unittest.main()
