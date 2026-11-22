"""Kiểm thử danh mục dụng cụ sinh học tầng 2 và dữ liệu sinh học (data/sinh/*.json).

Kiểm tra:
- Khớp 100% 108 mã dụng cụ với bảng trong phong_sinh/HOP_DONG_GIAO_DIEN_BACKEND.md.
- Đủ các trường bắt buộc, thông số có nguồn URL https thật hoặc mo_phong: true.
- Các file tieu_ban.json, chung_vi_sinh.json, moi_truong.json, bai_thi_nghiem.json, sinh_pham_mac_dinh.json.
- Tất cả chủng vi sinh BSL-1.
- Mọi mã dụng cụ và sinh phẩm nhắc tới trong 15 bài thí nghiệm đều tồn tại.
"""

from __future__ import annotations

import json
import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DATA_SINH = ROOT / "data" / "sinh"
HOP_DONG = ROOT / "phong_sinh" / "HOP_DONG_GIAO_DIEN_BACKEND.md"
THIET_BI_T1 = ROOT / "data" / "chem" / "thiet_bi.json"
MAY_DIR = ROOT / "data" / "may"

CAC_PHONG_HOP_LE = {
    "phong_sinh",
    "phong_vi_sinh",
    "kho_sinh_pham",
    "hanh_lang_t2",
    "phong_sinh_phan_tu",
    "nha_kinh",
    "hanh_lang_dong",
}


def doc_bang_hop_dong() -> dict[str, dict]:
    """Phân tích các bảng mục 2 trong HOP_DONG_GIAO_DIEN_BACKEND.md lấy 108 mã dụng cụ."""
    if not HOP_DONG.exists():
        raise FileNotFoundError(f"Không tìm thấy hợp đồng tại {HOP_DONG}")

    text = HOP_DONG.read_text(encoding="utf-8")
    m2 = re.search(r"## 2\..*?(?=## 3\.)", text, re.DOTALL)
    if not m2:
        raise ValueError("Không tìm thấy mục 2 trong hợp đồng")

    danh_muc = {}
    for line in m2.group(0).splitlines():
        line = line.strip()
        if not line.startswith("|") or line.startswith("|---") or "Thành phần Unity" in line or "| Mã |" in line:
            continue
        parts = [p.strip() for p in line.strip("|").split("|")]
        if len(parts) < 6:
            continue

        ma = parts[0].strip("`")
        ten = parts[1]
        phong_raw = parts[2]
        unity_raw = parts[3]
        may_raw = parts[4].strip("`") if parts[4].strip() else None
        muc = parts[5].strip()

        # Chuẩn hóa thanh_phan_unity: nếu không có namespace (không có dấu '.') thì thêm Khkt.Demo.Bio.
        def _chuan_hoa_unity(m: re.Match) -> str:
            val = m.group(1)
            if "." in val:
                return val
            return f"Khkt.Demo.Bio.{val}"

        unity_norm = re.sub(r"`([^`]+)`", _chuan_hoa_unity, unity_raw)

        danh_muc[ma] = {
            "ma": ma,
            "ten": ten,
            "phong_raw": phong_raw,
            "thanh_phan_unity": unity_norm,
            "may_man_hinh": may_raw,
            "muc_dac_ta": muc,
        }

    return danh_muc


class TestDungCuSinh(unittest.TestCase):
    def setUp(self):
        self.hop_dong = doc_bang_hop_dong()

    def test_01_hop_dong_co_dung_108_ma(self):
        self.assertEqual(len(self.hop_dong), 108, f"Hợp đồng cần có đúng 108 mã dụng cụ, hiện có {len(self.hop_dong)}")

    def test_02_dung_cu_json_ton_tai_va_du_108_ma(self):
        f = DATA_SINH / "dung_cu.json"
        self.assertTrue(f.exists(), "Chưa tạo data/sinh/dung_cu.json")
        data = json.loads(f.read_text(encoding="utf-8"))
        self.assertEqual(data.get("phien_ban"), 1)
        ds = data.get("dung_cu", [])
        self.assertEqual(len(ds), 108, f"Cần đúng 108 dụng cụ, tìm thấy {len(ds)}")

        ma_json = [d["ma"] for d in ds]
        self.assertEqual(len(ma_json), len(set(ma_json)), "Có mã dụng cụ bị trùng trong dung_cu.json")
        self.assertEqual(set(ma_json), set(self.hop_dong.keys()), "Danh sách mã dụng cụ không khớp bảng hợp đồng")

    def test_03_thuoc_tinh_tung_dung_cu(self):
        f = DATA_SINH / "dung_cu.json"
        if not f.exists():
            self.skipTest("Chưa có data/sinh/dung_cu.json")
        data = json.loads(f.read_text(encoding="utf-8"))
        ds = data.get("dung_cu", [])

        stt_t1_hop_le = set()
        if THIET_BI_T1.exists():
            tb_data = json.loads(THIET_BI_T1.read_text(encoding="utf-8"))
            stt_t1_hop_le = {t["stt"] for t in tb_data.get("thiet_bi", [])}

        cac_khoa_bat_buoc = {
            "ma", "ten", "ten_tieng_anh", "phan_loai", "phong", "so_luong", "vi_tri",
            "cong_dung", "thong_so", "cach_dung", "loi_thuong_gap", "an_toan",
            "hieu_chuan_ngay", "thanh_phan_unity", "may_man_hinh", "muc_dac_ta"
        }

        for d in ds:
            ma = d.get("ma")
            with self.subTest(ma=ma):
                for k in cac_khoa_bat_buoc:
                    self.assertIn(k, d, f"Thiếu khóa {k} ở dụng cụ {ma}")

                hd = self.hop_dong[ma]
                self.assertEqual(d["thanh_phan_unity"], hd["thanh_phan_unity"], f"thanh_phan_unity không khớp hợp đồng ở {ma}")
                self.assertEqual(d["may_man_hinh"], hd["may_man_hinh"], f"may_man_hinh không khớp hợp đồng ở {ma}")
                self.assertEqual(d["muc_dac_ta"], hd["muc_dac_ta"], f"muc_dac_ta không khớp hợp đồng ở {ma}")

                # Kiểm tra phòng
                self.assertIsInstance(d["phong"], list)
                self.assertGreaterEqual(len(d["phong"]), 1)
                for p in d["phong"]:
                    self.assertIn(p, CAC_PHONG_HOP_LE, f"Phòng {p} không thuộc ZoneMap tầng 2")

                self.assertGreaterEqual(d["so_luong"], 1)
                self.assertIsInstance(d["hieu_chuan_ngay"], int)
                self.assertGreaterEqual(d["hieu_chuan_ngay"], 0)

                # Kiểm tra cách dùng, lỗi, an toàn
                self.assertIsInstance(d["cach_dung"], list)
                self.assertGreaterEqual(len(d["cach_dung"]), 1, f"Thiếu bước cách dùng ở {ma}")
                self.assertIsInstance(d["loi_thuong_gap"], list)
                self.assertGreaterEqual(len(d["loi_thuong_gap"]), 1, f"Thiếu lỗi thường gặp ở {ma}")
                self.assertIsInstance(d["an_toan"], list)
                self.assertGreaterEqual(len(d["an_toan"]), 1, f"Thiếu lưu ý an toàn ở {ma}")

                # Kiểm tra thông số (ít nhất 2 thông số có nguồn URL https thật hoặc mo_phong: true)
                self.assertIsInstance(d["thong_so"], list)
                self.assertGreaterEqual(len(d["thong_so"]), 2, f"Cần ít nhất 2 thông số ở {ma}")
                for ts in d["thong_so"]:
                    self.assertTrue(ts.get("ten"), f"Thông số thiếu tên ở {ma}")
                    self.assertTrue(ts.get("gia_tri"), f"Thông số thiếu giá trị ở {ma}")
                    has_url = isinstance(ts.get("nguon"), str) and ts["nguon"].startswith("https://")
                    is_sim = ts.get("mo_phong") is True
                    self.assertTrue(has_url or is_sim, f"Thông số {ts.get('ten')} ở {ma} phải có URL https hoặc mo_phong: true")

                # Kiểm tra stt_thiet_bi_tang_1 nếu có
                if "stt_thiet_bi_tang_1" in d:
                    self.assertIn(d["stt_thiet_bi_tang_1"], stt_t1_hop_le, f"stt_thiet_bi_tang_1 {d['stt_thiet_bi_tang_1']} không tồn tại ở {ma}")

                # Kiểm tra hồ sơ máy nếu có may_man_hinh
                if d["may_man_hinh"]:
                    may_file = MAY_DIR / f"{d['may_man_hinh']}.json"
                    self.assertTrue(may_file.exists(), f"Thiếu hồ sơ máy data/may/{d['may_man_hinh']}.json cho dụng cụ {ma}")


class TestDuLieuSinhHoc(unittest.TestCase):
    """Kiểm thử các file dữ liệu sinh học khác (data/sinh/*.json) ở Giai đoạn B2."""

    def test_04_tieu_ban_json(self):
        f = DATA_SINH / "tieu_ban.json"
        if not f.exists():
            self.skipTest("Chưa có data/sinh/tieu_ban.json (GĐ B2)")
        data = json.loads(f.read_text(encoding="utf-8"))
        self.assertEqual(data.get("phien_ban"), 1)
        tb_list = data.get("tieu_ban", [])
        self.assertGreaterEqual(len(tb_list), 10)
        for tb in tb_list:
            for k in ("ma", "ten", "loai", "nhuom", "kich_thuoc_te_bao_um", "bo_phan_chu_thich"):
                self.assertIn(k, tb)
            self.assertIn(tb["loai"], ("tu_lam", "co_dinh"))
            self.assertIn(tb["nhuom"], ("khong", "xanh_methylen", "lugol", "gram", "he"))
            self.assertEqual(len(tb["kich_thuoc_te_bao_um"]), 2)
            for bp in tb["bo_phan_chu_thich"]:
                self.assertIn("ten", bp)
                self.assertTrue(0 <= bp["x"] <= 1)
                self.assertTrue(0 <= bp["y"] <= 1)

    def test_05_chung_vi_sinh_bsl1(self):
        f = DATA_SINH / "chung_vi_sinh.json"
        if not f.exists():
            self.skipTest("Chưa có data/sinh/chung_vi_sinh.json (GĐ B2)")
        data = json.loads(f.read_text(encoding="utf-8"))
        self.assertEqual(data.get("phien_ban"), 1)
        chungs = data.get("chung_vi_sinh", [])
        self.assertGreaterEqual(len(chungs), 5)
        for c in chungs:
            self.assertEqual(c.get("cap_an_toan"), "BSL-1", f"Chủng {c.get('ma')} phải là BSL-1")
            for k in ("ma", "ten_khoa_hoc", "ten", "gram", "hinh_dang", "thoi_gian_the_he_phut", "nhiet_do_toi_uu_c", "khuan_lac"):
                self.assertIn(k, c)
            self.assertIn(c["gram"], ("duong", "am", "khong_ap_dung"))
            self.assertIn(c["hinh_dang"], ("que", "cau", "nam_men"))
            kl = c["khuan_lac"]
            self.assertEqual(len(kl["mau"]), 3)

    def test_06_moi_truong_json(self):
        f = DATA_SINH / "moi_truong.json"
        if not f.exists():
            self.skipTest("Chưa có data/sinh/moi_truong.json (GĐ B2)")
        data = json.loads(f.read_text(encoding="utf-8"))
        self.assertEqual(data.get("phien_ban"), 1)
        mts = data.get("moi_truong", [])
        self.assertGreaterEqual(len(mts), 5)
        for m in mts:
            for k in ("ma", "ten", "loai", "thanh_phan", "mau", "do_trong", "ph"):
                self.assertIn(k, m)
            self.assertIn(m["loai"], ("thach", "long"))

    def test_07_bai_thi_nghiem_json(self):
        f = DATA_SINH / "bai_thi_nghiem.json"
        if not f.exists():
            self.skipTest("Chưa có data/sinh/bai_thi_nghiem.json (GĐ B2)")
        data = json.loads(f.read_text(encoding="utf-8"))
        self.assertEqual(data.get("phien_ban"), 1)
        bais = data.get("bai_thi_nghiem", [])
        self.assertEqual(len(bais), 16, "15 bài theo hợp đồng + bài ghép gen (phòng ghép mã ADN)")

        dc_f = DATA_SINH / "dung_cu.json"
        dung_cu_keys = set()
        if dc_f.exists():
            dung_cu_keys = {d["ma"] for d in json.loads(dc_f.read_text(encoding="utf-8")).get("dung_cu", [])}

        sp_f = DATA_SINH / "sinh_pham_mac_dinh.json"
        sp_keys = set()
        if sp_f.exists():
            sp_keys = {s["id"] for s in json.loads(sp_f.read_text(encoding="utf-8")).get("sinh_pham", [])}

        for b in bais:
            for k in ("ma", "ten", "lop", "sgk", "muc_tieu", "dung_cu", "sinh_pham", "buoc", "ket_qua_mong_doi", "cau_hoi", "nguon"):
                self.assertIn(k, b)
            self.assertTrue(3 <= len(b["cau_hoi"]) <= 5)
            # Kiểm tra mọi mã dụng cụ và sinh phẩm nhắc trong bài đều tồn tại
            if dung_cu_keys:
                for dc in b["dung_cu"]:
                    self.assertIn(dc, dung_cu_keys, f"Bài {b['ma']} nhắc dụng cụ chưa có: {dc}")
            if sp_keys:
                for sp in b["sinh_pham"]:
                    self.assertIn(sp, sp_keys, f"Bài {b['ma']} nhắc sinh phẩm chưa có: {sp}")

    def test_08_sinh_pham_mac_dinh_json(self):
        f = DATA_SINH / "sinh_pham_mac_dinh.json"
        if not f.exists():
            self.skipTest("Chưa có data/sinh/sinh_pham_mac_dinh.json (GĐ B2)")
        data = json.loads(f.read_text(encoding="utf-8"))
        self.assertEqual(data.get("phien_ban"), 1)
        sps = data.get("sinh_pham", [])
        self.assertGreaterEqual(len(sps), 20)

        chung_f = DATA_SINH / "chung_vi_sinh.json"
        chung_keys = set()
        if chung_f.exists():
            chung_keys = {c["ma"] for c in json.loads(chung_f.read_text(encoding="utf-8")).get("chung_vi_sinh", [])}

        for s in sps:
            for k in ("id", "ten", "loai", "tu", "ngan", "so_luong", "don_vi", "so_lo", "han_dung_ngay", "cap_an_toan"):
                self.assertIn(k, s)
            self.assertTrue(s["id"].startswith("sp_"))
            self.assertEqual(s["cap_an_toan"], "BSL-1")
            self.assertIn(s["tu"], ("tu_mat_4", "tu_dong_am20", "ke"))
            if s.get("ma_chung") and chung_keys:
                self.assertIn(s["ma_chung"], chung_keys, f"Sinh phẩm {s['id']} tham chiếu ma_chung không tồn tại: {s['ma_chung']}")


if __name__ == "__main__":
    unittest.main()

