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

    def test_danh_sach_va_ho_so_may(self):
        ds = self.client.get("/may").json()
        self.assertGreaterEqual(len(ds), 14)
        self.assertIn("noi_hap", [m["ma"] for m in ds])
        self.assertIn("kinh_hien_vi", [m["ma"] for m in ds])
        self.assertIn("may_pcr", [m["ma"] for m in ds])
        d = self.client.get("/may/xrd").json()
        self.assertEqual(d["ma"], "xrd")
        self.assertTrue(d["quy_trinh"])
        d_bio = self.client.get("/may/kinh_hien_vi").json()
        self.assertEqual(d_bio["ma"], "kinh_hien_vi")
        self.assertTrue(d_bio["quy_trinh"])
        self.assertEqual(self.client.get("/may/khong_co").status_code, 404)

    def test_tro_ly_kem_quy_trinh_may(self):
        mod, calls = fake_provider({"tra_loi": "Đuổi hết không khí trước đã.", "hanh_dong": []})
        main.providers.pick = lambda: mod
        self.client.post("/tro-ly", json={"cau_hoi": "Dùng nồi hấp tiệt trùng thế nào?", "ngu_canh": "phòng máy"})
        user = calls[0][2]
        self.assertIn("QUY TRÌNH MÁY", user)
        self.assertIn("Đuổi không khí", user)  # bước của nồi hấp theo data/may/noi_hap.json
        calls.clear()
        self.client.post("/tro-ly", json={"cau_hoi": "lấy đồng sunfat", "ngu_canh": "tự do"})
        self.assertNotIn("QUY TRÌNH MÁY", calls[0][2])

    def test_nhan_ra_may_trong_cau_hoi(self):
        from server.ai import machines
        self.assertEqual([d["ma"] for d in machines.find("cách dùng máy SEM?")], ["sem"])
        self.assertEqual([d["ma"] for d in machines.find("đo phổ hồng ngoại")], ["ftir"])
        self.assertEqual([d["ma"] for d in machines.find("GC-MS rượu")], ["gc_ms"])
        self.assertEqual([d["ma"] for d in machines.find("cách lấy nét kính hiển vi quang học?")], ["kinh_hien_vi"])
        self.assertEqual([d["ma"] for d in machines.find("cài đặt nhiệt độ máy PCR nhân bản gen")], ["may_pcr"])
        self.assertEqual(machines.find("pha dung dịch NaCl"), [])

    def test_luoc_do_hanh_dong_hop_le(self):
        s = main.ASSIST_SCHEMA
        self.assertFalse(s["additionalProperties"])
        self.assertEqual(set(s["required"]), {"tra_loi", "hanh_dong"})


if __name__ == "__main__":
    unittest.main()
