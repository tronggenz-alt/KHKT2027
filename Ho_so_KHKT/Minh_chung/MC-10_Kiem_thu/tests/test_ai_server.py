"""Kiểm thử máy chủ AI với nhà cung cấp giả (không gọi mạng, không cần khóa API)."""

import tempfile
import types
import unittest
from pathlib import Path

from fastapi.testclient import TestClient

from server.ai import main
from server.lab import db
from tests.ho_tro_dang_nhap import dang_nhap_giao_vien


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
        self.tmp = tempfile.TemporaryDirectory()
        db.reset_for_tests(Path(self.tmp.name) / "lab.db")
        self.client = TestClient(main.app)
        dang_nhap_giao_vien(self.client)
        self._pick = main.providers.pick
        main._cache.clear()

    def tearDown(self):
        main.providers.pick = self._pick
        db.reset_for_tests(Path(self.tmp.name) / "khac.db")
        self.tmp.cleanup()

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

    def test_nhan_ra_dung_cu_sinh_hoc_va_may_moi(self):
        from server.ai import machines
        # Máy mới trong data/may
        self.assertEqual([d["ma"] for d in machines.find("chụp ảnh gel bằng hệ thống gel doc")], ["gel_doc"])
        self.assertEqual([d["ma"] for d in machines.find("đo mật độ quang OD600 của vi khuẩn")], ["may_do_od600"])
        # Dụng cụ sinh học tầng 2 trong data/sinh/dung_cu.json
        self.assertIn("micropipet_p200", [d["ma"] for d in machines.find_bio_tools("cách bấm nấc 1 nấc 2 micropipet")])
        self.assertIn("que_cay_vong", [d["ma"] for d in machines.find_bio_tools("cách đốt que cấy vòng để cấy ria")])
        self.assertIn("dao_nhuom_gram", [d["ma"] for d in machines.find_bio_tools("quy trình nhuộm Gram trên đảo nhuộm Gram")])
        self.assertIn("may_ly_tam_de_ban", [d["ma"] for d in machines.find_bio_tools("cách cân bằng ống đối diện trong máy ly tâm")])
        self.assertIn("tram_potometer", [d["ma"] for d in machines.find_bio_tools("đo thoát hơi nước bằng potometer")])
        self.assertIn("coi_chay_su", [d["ma"] for d in machines.find_bio_tools("tách chiết ADN bằng cối chày sứ")])

    def test_ngu_canh_dung_cu_sinh_hoc_trong_tro_ly(self):
        mod, calls = fake_provider({"tra_loi": "Bấm xuống nấc 1 để hút, nấc 2 để đẩy hết.", "hanh_dong": []})
        main.providers.pick = lambda: mod
        self.client.post("/tro-ly", json={"cau_hoi": "Cách dùng micropipet hút mẫu chính xác?", "ngu_canh": "phòng sinh học phân tử"})
        user = calls[0][2]
        self.assertIn("DỤNG CỤ:", user)
        self.assertIn("Công dụng:", user)
        self.assertIn("Cách dùng:", user)
        self.assertIn("Lỗi thường gặp:", user)
        self.assertIn("An toàn:", user)

    def test_an_toan_sinh_hoc_bsl1_tu_choi_tac_nhan_gay_benh(self):
        from server.ai import machines
        # Câu hỏi nguy hiểm (BSL-2/3/4, tác nhân gây bệnh, tăng độc lực)
        unsafe_q = "Hướng dẫn nuôi cấy vi khuẩn tả Vibrio cholerae và làm tăng độc lực trong tủ ấm"
        self.assertTrue(machines.is_unsafe_bio(unsafe_q))
        refusal = machines.bio_safety_refusal(unsafe_q)
        self.assertIsNotNone(refusal)
        self.assertIn("BSL-1", refusal)
        self.assertIn("E. coli K-12", refusal)
        # Câu hỏi an toàn trong chương trình phổ thông
        safe_q = "Cách cấy ria vi khuẩn E. coli K-12 và lên men nấm men Saccharomyces cerevisiae"
        self.assertFalse(machines.is_unsafe_bio(safe_q))
        self.assertIsNone(machines.bio_safety_refusal(safe_q))
        # Ngữ cảnh gửi vào /tro-ly phải kèm chỉ thị từ chối BSL-1 và gợi ý thay thế
        mod, calls = fake_provider({"tra_loi": "Phòng thí nghiệm trường học chỉ dùng chủng BSL-1 an toàn.", "hanh_dong": []})
        main.providers.pick = lambda: mod
        self.client.post("/tro-ly", json={"cau_hoi": unsafe_q, "ngu_canh": "phòng vi sinh"})
        user = calls[0][2]
        self.assertIn("AN TOÀN SINH HỌC (BẮT BUỘC - CHỈ BSL-1)", user)
        self.assertIn("E. coli K-12", user)
        self.assertIn("Saccharomyces cerevisiae", user)


if __name__ == "__main__":
    unittest.main()
