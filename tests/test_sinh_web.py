"""Kiểm thử toàn diện API FastAPI Tầng 2 phòng sinh học (tests/test_sinh_web.py).
Bảo đảm tuân thủ đúng mọi yêu cầu trong HOP_DONG_GIAO_DIEN_BACKEND.md và PROMPT_DUNG_CU_BACKEND.md.
"""

import tempfile
import time
import unittest
from pathlib import Path

from fastapi.testclient import TestClient

from server.ai.main import app
from server.lab import sinh_db


class TestSinhWeb(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.db_path = Path(self.tmp.name) / "test_sinh_web.db"
        sinh_db.reset_for_tests(self.db_path)
        self.client = TestClient(app)

    def tearDown(self):
        sinh_db.reset_for_tests(Path(self.tmp.name) / "khac.db")
        self.tmp.cleanup()

    def test_01_sinh_page(self):
        res = self.client.get("/sinh")
        self.assertEqual(res.status_code, 200)
        self.assertIn("text/html", res.headers.get("content-type", ""))
        self.assertIn("Phòng Lab Sinh học VERA", res.text)

    def test_02_tong_quan(self):
        res = self.client.get("/lab/sinh/tong-quan")
        self.assertEqual(res.status_code, 200)
        data = res.json()
        self.assertIn("dung_cu", data)
        self.assertIn("sinh_pham", data)
        self.assertIn("tu", data)
        self.assertIn("su_co_moi", data)
        self.assertIn("lich_hom_nay", data)
        self.assertGreaterEqual(data["dung_cu"]["tong"], 108)

    def test_03_dung_cu_and_patch(self):
        res = self.client.get("/lab/sinh/dung-cu")
        self.assertEqual(res.status_code, 200)
        data = res.json()
        self.assertEqual(data["tong"], 108)
        self.assertEqual(len(data["muc"]), 108)

        # Lọc theo phòng
        res_phong = self.client.get("/lab/sinh/dung-cu?phong=phong_sinh")
        self.assertEqual(res_phong.status_code, 200)
        self.assertGreater(res_phong.json()["tong"], 0)

        # PATCH cập nhật dụng cụ
        res_patch = self.client.patch("/lab/sinh/dung-cu/kinh_hien_vi", json={
            "hong": 2,
            "can_bao_tri": 1,
            "ghi_chu": "Bàn sa trượt kẹt nhẹ"
        })
        self.assertEqual(res_patch.status_code, 200)
        p_data = res_patch.json()
        self.assertEqual(p_data["hong"], 2)
        self.assertEqual(p_data["can_bao_tri"], 1)

        # 404 với mã không tồn tại
        res_404 = self.client.patch("/lab/sinh/dung-cu/ma_khong_ton_tai", json={"hong": 1})
        self.assertEqual(res_404.status_code, 404)

    def test_04_sinh_pham_crud_and_bsl1_enforcement(self):
        # 1. Danh sách sinh phẩm
        res = self.client.get("/lab/sinh/sinh-pham")
        self.assertEqual(res.status_code, 200)
        data = res.json()
        self.assertGreaterEqual(data["tong"], 35)

        # 2. Thêm mới sinh phẩm BSL-1 hợp lệ
        res_post = self.client.post("/lab/sinh/sinh-pham", json={
            "id": "sp_api_test_01",
            "ten": "Mẫu vi khuẩn Lactobacillus thử nghiệm",
            "loai": "chung_vi_sinh",
            "tu": "tu_mat_4",
            "so_luong": 15.0,
            "don_vi": "ống",
            "cap_an_toan": "BSL-1"
        })
        self.assertEqual(res_post.status_code, 200)
        self.assertEqual(res_post.json()["id"], "sp_api_test_01")

        # 3. Chặn cấp an toàn không phải BSL-1 (HTTP 422)
        res_bsl2 = self.client.post("/lab/sinh/sinh-pham", json={
            "id": "sp_nguy_hiem",
            "ten": "Mẫu giả định gây bệnh",
            "loai": "chung_vi_sinh",
            "tu": "tu_dong_am20",
            "so_luong": 1.0,
            "don_vi": "ống",
            "cap_an_toan": "BSL-2"
        })
        self.assertEqual(res_bsl2.status_code, 422)
        self.assertIn("BSL-1", res_bsl2.json()["detail"])

        # 4. Xuất / Nhập kho sinh phẩm
        res_xuat = self.client.post("/lab/sinh/sinh-pham/sp_api_test_01/xuat", json={
            "so_luong": 5.0,
            "nguoi": "Học sinh A",
            "ghi_chu": "Làm thí nghiệm lên men"
        })
        self.assertEqual(res_xuat.status_code, 200)
        self.assertEqual(res_xuat.json()["so_luong"], 10.0)

        res_nhap = self.client.post("/lab/sinh/sinh-pham/sp_api_test_01/nhap", json={
            "so_luong": 20.0,
            "nguoi": "Cán bộ phòng thí nghiệm",
            "ghi_chu": "Nhập thêm lô mới"
        })
        self.assertEqual(res_nhap.status_code, 200)
        self.assertEqual(res_nhap.json()["so_luong"], 30.0)

        # 5. PATCH và DELETE
        res_patch = self.client.patch("/lab/sinh/sinh-pham/sp_api_test_01", json={"ngan": "Ngăn K03"})
        self.assertEqual(res_patch.status_code, 200)
        self.assertEqual(res_patch.json()["ngan"], "Ngăn K03")

        res_del = self.client.delete("/lab/sinh/sinh-pham/sp_api_test_01")
        self.assertEqual(res_del.status_code, 200)

    def test_05_nhiet_do(self):
        res_post = self.client.post("/lab/sinh/nhiet-do", json={
            "tu": "tu_mat_4",
            "nhiet_do": 4.1,
            "cua_mo": 0
        })
        self.assertEqual(res_post.status_code, 200)

        res_get = self.client.get("/lab/sinh/nhiet-do?tu=tu_mat_4")
        self.assertEqual(res_get.status_code, 200)
        self.assertGreater(res_get.json()["tong"], 0)

    def test_06_dat_lich_conflict_409(self):
        now = time.time()
        t_start = now + 10000
        t_end = now + 14000

        # Lần 1: Thành công
        res1 = self.client.post("/lab/sinh/dat-lich", json={
            "doi_tuong": "tu_an_toan_sinh_hoc",
            "nguoi": "Giáo viên Hóa Sinh",
            "lop": "10A1",
            "bat_dau": t_start,
            "ket_thuc": t_end,
            "muc_dich": "Thực hành vi sinh"
        })
        self.assertEqual(res1.status_code, 200)
        lid = res1.json()["id"]

        # Lần 2: Trùng khung giờ -> Trả HTTP 409 kèm mảng trung
        res_conflict = self.client.post("/lab/sinh/dat-lich", json={
            "doi_tuong": "tu_an_toan_sinh_hoc",
            "nguoi": "Học sinh B",
            "lop": "10A2",
            "bat_dau": t_start + 1000,
            "ket_thuc": t_end + 1000,
            "muc_dich": "Cấy ria"
        })
        self.assertEqual(res_conflict.status_code, 409)
        detail = res_conflict.json()["detail"]
        self.assertIn("trung", detail)
        self.assertEqual(len(detail["trung"]), 1)

        # Xóa lịch
        res_del = self.client.delete(f"/lab/sinh/dat-lich/{lid}")
        self.assertEqual(res_del.status_code, 200)

    def test_07_muon_tra_and_results(self):
        # Mượn trả
        res_m = self.client.post("/lab/sinh/muon-tra", json={
            "ma_dung_cu": "hop_petri",
            "so_luong": 10,
            "nguoi": "Nhóm 1",
            "lop": "10A3"
        })
        self.assertEqual(res_m.status_code, 200)
        mid = res_m.json()["id"]

        res_tra = self.client.patch(f"/lab/sinh/muon-tra/{mid}", json={
            "tinh_trang_tra": "tot"
        })
        self.assertEqual(res_tra.status_code, 200)

        # Kết quả thí nghiệm
        res_kq = self.client.post("/lab/sinh/ket-qua", json={
            "hoc_sinh": "Phạm Minh D",
            "lop": "10A3",
            "ban": "Bàn 04",
            "ma_bai": "te_bao_hanh",
            "du_lieu": {"thao_tac_dung": True, "do_phong_dai": "400x"},
            "diem": 9.5,
            "nhan_xet": "Thực hiện tiêu bản rất đẹp"
        })
        self.assertEqual(res_kq.status_code, 200)
        kid = res_kq.json()["id"]

        res_kq_get = self.client.get(f"/lab/sinh/ket-qua/{kid}")
        self.assertEqual(res_kq_get.status_code, 200)
        self.assertEqual(res_kq_get.json()["hoc_sinh"], "Phạm Minh D")

    def test_08_ket_qua_csv_with_utf8_bom(self):
        # Tạo sẵn kết quả có dấu tiếng Việt
        self.client.post("/lab/sinh/ket-qua", json={
            "hoc_sinh": "Nguyễn Hoàng Nam",
            "lop": "10 chuyên Sinh",
            "ban": "Bàn 01",
            "ma_bai": "quang_hop_rong",
            "diem": 10.0,
            "nhan_xet": "Đếm bọt khí chính xác"
        })

        res = self.client.get("/lab/sinh/ket-qua.csv")
        self.assertEqual(res.status_code, 200)
        self.assertIn("text/csv", res.headers.get("content-type", ""))
        raw_bytes = res.content
        # Bắt buộc bắt đầu bằng ký tự UTF-8 BOM (\xef\xbb\xbf)
        self.assertTrue(raw_bytes.startswith(b"\xef\xbb\xbf"), "File CSV xuất ra phải có tiền tố UTF-8 BOM")
        text = raw_bytes.decode("utf-8-sig")
        self.assertIn("Nguyễn Hoàng Nam", text)
        self.assertIn("10 chuyên Sinh", text)

    def test_09_su_co_and_bao_tri(self):
        # Sự cố
        res_sc = self.client.post("/lab/sinh/su-co", json={
            "phong": "phong_chinh",
            "loai": "nhiem_vi_sinh",
            "muc_do": "Thấp",
            "mo_ta": "Đĩa đối chứng mọc 1 khuẩn lạc tạp",
            "xu_ly": "Khử trùng bề mặt tủ cấy"
        })
        self.assertEqual(res_sc.status_code, 200)

        # Bảo trì
        res_bt = self.client.post("/lab/sinh/bao-tri", json={
            "ma_dung_cu": "noi_hap",
            "loai": "kiem_dinh_ap_suat",
            "nguoi": "Thanh tra an toàn",
            "ket_qua": "Van an toàn xả đúng 1,05 bar",
            "ghi_chu": "Đạt chuẩn an toàn"
        })
        self.assertEqual(res_bt.status_code, 200)

    def test_10_catalogs_and_machine_status(self):
        # Hướng dẫn dụng cụ
        res_hd = self.client.get("/lab/sinh/huong-dan/kinh_hien_vi")
        self.assertEqual(res_hd.status_code, 200)
        self.assertEqual(res_hd.json()["ma"], "kinh_hien_vi")

        # 15 bài thí nghiệm
        res_bai = self.client.get("/lab/sinh/bai-thi-nghiem")
        self.assertEqual(res_bai.status_code, 200)
        self.assertEqual(res_bai.json()["tong"], 16)

        # Trạng thái máy
        res_may = self.client.get("/lab/sinh/trang-thai-may")
        self.assertEqual(res_may.status_code, 200)
        self.assertEqual(res_may.json()["tong"], 9)  # 8 máy chính + trạm ghép gen

    def test_11_batch_su_kien(self):
        res = self.client.post("/lab/sinh/su-kien", json={
            "su_kien": [
                {
                    "loai": "nhiet_do",
                    "thoi_gian": time.time(),
                    "du_lieu": {"tu": "tu_am_37", "nhiet_do": 37.2, "cua_mo": 0}
                },
                {
                    "loai": "su_co",
                    "thoi_gian": time.time(),
                    "du_lieu": {
                        "phong": "phong_chinh",
                        "loai": "do_dung_dich",
                        "mo_ta": "Rơi vỡ ống nghiệm chứa dịch đường"
                    }
                },
                {
                    "loai": "ket_qua",
                    "thoi_gian": time.time(),
                    "du_lieu": {
                        "hoc_sinh": "Trần Văn C",
                        "lop": "11B2",
                        "ma_bai": "len_men_ruou",
                        "diem": 8.5
                    }
                }
            ]
        })
        self.assertEqual(res.status_code, 200)
        data = res.json()
        self.assertEqual(data["nhan"], 3)
        self.assertEqual(len(data["loi"]), 0)

    def test_11b_su_kien_dung_hop_dong_unity(self):
        """Đúng tên sự kiện BioLabSync.cs gửi (hợp đồng mục 5): trước đây bị bỏ qua lặng lẽ."""
        res = self.client.post("/lab/sinh/su-kien", json={"su_kien": [
            {"loai": "ket_qua_tn", "du_lieu": {"hoc_sinh": "Học sinh", "lop": "12A1", "ban": "B1",
                                               "ma_bai": "ghep_gen_adn_tai_to_hop", "du_lieu": {"khuan_lac_trang": 41}}},
            {"loai": "nhiet_do_tu", "du_lieu": {"tu": "tu_mat_4", "nhiet_do": 4.6, "cua_mo": True}},
            {"loai": "trang_thai_may", "du_lieu": {"ma": "may_pcr", "trang_thai": "dang_chay",
                                                   "mo_ta": "Chu kỳ 12/30", "tien_do": 0.4}},
            {"loai": "dung_cu_hong", "du_lieu": {"ma": "micropipet_p200", "so_luong": 1, "ly_do": "Đặt nằm ngang khi còn dịch"}},
            {"loai": "can_bao_tri", "du_lieu": {"ma": "may_pcr", "ly_do": "Nắp nhiệt không lên 105 °C"}},
            {"loai": "sinh_pham_xuat", "du_lieu": {"id": "sp_xanh_methylen_1", "so_luong": 1, "nguoi": "Học sinh"}},
            {"loai": "khong_co_loai_nay", "du_lieu": {}},
            {"loai": "trang_thai_may", "du_lieu": {"ma": "may_pcr", "trang_thai": "bay_bong"}},
        ]})
        self.assertEqual(res.status_code, 200)
        data = res.json()
        self.assertEqual(data["nhan"], 6)
        self.assertEqual([l["chi_so"] for l in data["loi"]], [6, 7])

        kq = self.client.get("/lab/sinh/ket-qua", params={"ma_bai": "ghep_gen_adn_tai_to_hop"}).json()
        self.assertEqual(kq["tong"], 1)
        nd = self.client.get("/lab/sinh/nhiet-do", params={"tu": "tu_mat_4"}).json()["muc"]
        self.assertTrue(any(abs(r["nhiet_do"] - 4.6) < 1e-6 and r["cua_mo"] == 1 for r in nd))
        may = {m["ma"]: m for m in self.client.get("/lab/sinh/trang-thai-may").json()["muc"]}
        self.assertEqual(may["may_pcr"]["trang_thai"], "dang_chay")
        self.assertAlmostEqual(may["may_pcr"]["tien_do"], 0.4)
        self.assertEqual(may["gel_doc"]["nguon"], "mac_dinh")
        dc = {d["ma"]: d for d in self.client.get("/lab/sinh/dung-cu").json()["muc"]}
        self.assertEqual(dc["micropipet_p200"]["hong"], 1)
        self.assertEqual(dc["may_pcr"]["can_bao_tri"], 1)
        su_co = self.client.get("/lab/sinh/su-co").json()["muc"]
        self.assertTrue(any(r["loai"] == "vo_dung_cu" for r in su_co))

    def test_12_no_secret_leakage(self):
        # Kiểm tra không có khóa bí mật hoặc secret nào bị lộ trong phản hồi API
        secret = "gsk_SECRETKEY_BI_MAT_12345678"
        for endpoint in [
            "/lab/sinh/tong-quan",
            "/lab/sinh/dung-cu",
            "/lab/sinh/sinh-pham",
            "/lab/sinh/bai-thi-nghiem",
            "/lab/sinh/trang-thai-may",
        ]:
            res = self.client.get(endpoint)
            text = res.text
            self.assertNotIn(secret, text)
            self.assertNotIn("AIzaSy", text)


if __name__ == "__main__":
    unittest.main()
