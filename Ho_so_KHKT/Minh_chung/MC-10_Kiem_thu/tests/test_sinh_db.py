"""Kiểm thử cơ sở dữ liệu SQLite tầng 2 phòng sinh (server/lab/sinh_db.py)."""

import tempfile
import time
import unittest
from pathlib import Path

from server.lab import sinh_db


class TestSinhDb(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.db_path = Path(self.tmp.name) / "test_sinh.db"
        sinh_db.reset_for_tests(self.db_path)

    def tearDown(self):
        sinh_db.reset_for_tests(Path(self.tmp.name) / "khac.db")
        self.tmp.cleanup()

    def test_01_seed_data(self):
        conn = sinh_db.connect()
        tables = {r[0] for r in conn.execute("SELECT name FROM sqlite_master WHERE type='table'").fetchall()}
        expected_tables = {
            "sinh_dung_cu", "sinh_pham", "nhiet_do_tu", "dat_lich",
            "muon_tra", "ket_qua_tn", "su_co_sinh", "bao_tri"
        }
        for t in expected_tables:
            self.assertIn(t, tables, f"Thiếu bảng {t} trong CSDL")

        # Kiểm tra tự động nạp từ dung_cu.json (đủ 108 dụng cụ)
        dc_count = conn.execute("SELECT COUNT(*) FROM sinh_dung_cu").fetchone()[0]
        self.assertEqual(dc_count, 108, f"Phải nạp đủ 108 dụng cụ sinh học, được {dc_count}")

        # Kiểm tra tự động nạp sinh phẩm (ít nhất 35 mặt hàng)
        sp_count = conn.execute("SELECT COUNT(*) FROM sinh_pham").fetchone()[0]
        self.assertGreaterEqual(sp_count, 35)

    def test_02_tong_quan(self):
        tq = sinh_db.get_tong_quan()
        self.assertIn("dung_cu", tq)
        self.assertIn("sinh_pham", tq)
        self.assertIn("tu", tq)
        self.assertIn("su_co_moi", tq)
        self.assertIn("lich_hom_nay", tq)
        self.assertGreaterEqual(tq["dung_cu"]["tong"], 108)
        self.assertGreaterEqual(len(tq["sinh_pham"]["qua_han"]), 1)  # Có sp_kit_ly_giai_het_han

    def test_03_dung_cu_crud(self):
        items = sinh_db.list_dung_cu()
        self.assertEqual(len(items), 108)

        # Tìm kiếm theo tên
        filtered = sinh_db.list_dung_cu(tim="micropipet")
        self.assertGreater(len(filtered), 0)

        # Sửa trạng thái dụng cụ
        updated = sinh_db.patch_dung_cu("kinh_hien_vi", {"hong": 1, "can_bao_tri": 1, "ghi_chu": "Lệch thị kính"})
        self.assertEqual(updated["hong"], 1)
        self.assertEqual(updated["can_bao_tri"], 1)
        self.assertEqual(updated["ghi_chu"], "Lệch thị kính")

    def test_04_sinh_pham_crud_va_an_toan_bsl1(self):
        # Thêm sinh phẩm BSL-1 hợp lệ
        item = {
            "id": "sp_test_bsl1",
            "ten": "Mẫu vi sinh BSL-1 thử nghiệm",
            "loai": "chung_vi_sinh",
            "tu": "tu_mat_4",
            "so_luong": 10.0,
            "don_vi": "ống",
            "cap_an_toan": "BSL-1",
        }
        added = sinh_db.add_sinh_pham(item)
        self.assertEqual(added["id"], "sp_test_bsl1")

        # Từ chối sinh phẩm khác BSL-1 (ví dụ BSL-2)
        bad_item = dict(item)
        bad_item["id"] = "sp_test_bsl2"
        bad_item["cap_an_toan"] = "BSL-2"
        with self.assertRaises(ValueError):
            sinh_db.add_sinh_pham(bad_item)

        # Xuất/nhập kho sinh phẩm
        changed = sinh_db.change_sinh_pham_stock("sp_test_bsl1", -3.0, "Gv A", "Làm mẫu thực hành")
        self.assertEqual(changed["so_luong"], 7.0)

        # Xóa sinh phẩm
        self.assertTrue(sinh_db.delete_sinh_pham("sp_test_bsl1"))

    def test_05_dat_lich_va_trung_lich(self):
        now = time.time()
        # Đặt lịch 1: 14:00 - 16:00
        t1_start = now + 3600
        t1_end = now + 7200
        res1, conf1 = sinh_db.add_dat_lich({
            "doi_tuong": "tu_an_toan_sinh_hoc",
            "nguoi": "Nguyễn Văn A",
            "lop": "10A1",
            "bat_dau": t1_start,
            "ket_thuc": t1_end,
            "muc_dich": "Thực hành cấy ria vi sinh"
        })
        self.assertIsNotNone(res1)
        self.assertEqual(len(conf1), 0)

        # Đặt lịch trùng: 15:00 - 17:00 trên cùng đối tượng -> phát hiện trùng lịch
        t2_start = now + 5400
        t2_end = now + 9000
        res2, conf2 = sinh_db.add_dat_lich({
            "doi_tuong": "tu_an_toan_sinh_hoc",
            "nguoi": "Trần Thị B",
            "lop": "10A2",
            "bat_dau": t2_start,
            "ket_thuc": t2_end,
            "muc_dich": "Thực hành đổ đĩa"
        })
        self.assertIsNone(res2)
        self.assertEqual(len(conf2), 1)
        self.assertEqual(conf2[0]["id"], res1["id"])

    def test_06_muon_tra_va_quan_ly_hong(self):
        # Mượn 2 kính hiển vi
        m = sinh_db.add_muon_tra({
            "ma_dung_cu": "kinh_hien_vi",
            "so_luong": 2,
            "nguoi": "Lê Văn C",
            "lop": "11B1"
        })
        dc = sinh_db.query("SELECT * FROM sinh_dung_cu WHERE ma='kinh_hien_vi'")[0]
        self.assertEqual(dc["dang_muon"], 2)

        # Trả đồ: 1 hỏng -> tình trạng trả ghi nhận hỏng
        sinh_db.patch_muon_tra(m["id"], {"tinh_trang_tra": "hong"})
        dc_after = sinh_db.query("SELECT * FROM sinh_dung_cu WHERE ma='kinh_hien_vi'")[0]
        self.assertEqual(dc_after["dang_muon"], 0)
        self.assertEqual(dc_after["hong"], 2)

    def test_07_nhiet_do_su_co_bao_tri(self):
        # Nhiệt độ
        t = sinh_db.add_nhiet_do("tu_mat_4", 4.5, 0)
        self.assertEqual(t["nhiet_do"], 4.5)

        # Sự cố
        sc = sinh_db.add_su_co({
            "phong": "phong_chinh",
            "loai": "do_vo",
            "muc_do": "Trung bình",
            "mo_ta": "Vỡ lam kính chứa mẫu biểu bì hành",
            "xu_ly": "Dùng chổi gom mảnh vỡ vào thùng rác sắc nhọn"
        })
        self.assertEqual(sc["trang_thai"], "moi")
        sc_patched = sinh_db.patch_su_co(sc["id"], {"trang_thai": "da_xu_ly"})
        self.assertEqual(sc_patched["trang_thai"], "da_xu_ly")

        # Bảo trì / hiệu chuẩn
        bt = sinh_db.add_bao_tri({
            "ma_dung_cu": "micropipet_p1000",
            "loai": "hieu_chuan_dinh_ky",
            "nguoi": "Kỹ thuật viên K01",
            "ket_qua": "Đạt chuẩn sai số ISO 8655-2"
        })
        self.assertEqual(bt["ma_dung_cu"], "micropipet_p1000")
        dc_pipe = sinh_db.query("SELECT * FROM sinh_dung_cu WHERE ma='micropipet_p1000'")[0]
        self.assertGreater(dc_pipe["lan_hieu_chuan"], 0)


if __name__ == "__main__":
    unittest.main()
