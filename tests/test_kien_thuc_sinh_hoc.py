"""Kiểm thử tự động dữ liệu kiến thức 15 cơ quan sinh học 3D (GĐ4).

Chạy:
    python -m unittest tests/test_kien_thuc_sinh_hoc.py
"""

from __future__ import annotations

import json
import unittest
from pathlib import Path

GOC = Path(__file__).resolve().parents[1]
THU_MUC_STREAMING = (
    GOC / "Phong thi nghiem ao" / "Assets" / "StreamingAssets" / "data" / "sinh" / "kien_thuc"
)
DANH_SACH_15_CO_QUAN = [
    "tim",
    "phoi",
    "nao",
    "gan",
    "than",
    "da-day",
    "ruot-non",
    "ruot-gia",
    "co",
    "xuong",
    "da",
    "mach-mau",
    "tuy-song",
    "lach",
    "tuy",
]


class KienThucSinhHocTest(unittest.TestCase):
    def test_thu_muc_va_danh_muc_ton_tai(self):
        self.assertTrue(THU_MUC_STREAMING.is_dir(), f"Thư mục không tồn tại: {THU_MUC_STREAMING}")
        danh_muc_file = THU_MUC_STREAMING / "danh-muc.json"
        self.assertTrue(danh_muc_file.is_file(), "Thiếu file danh-muc.json")

        with open(danh_muc_file, encoding="utf-8") as f:
            data = json.load(f)
        self.assertIn("co_quan", data)
        self.assertEqual(len(data["co_quan"]), 15, "danh-muc.json phải chứa đúng 15 cơ quan")

    def test_du_15_file_co_quan(self):
        for cq in DANH_SACH_15_CO_QUAN:
            fp = THU_MUC_STREAMING / f"{cq}.json"
            self.assertTrue(fp.is_file(), f"Thiếu file cơ quan: {fp.name}")

    def test_cau_truc_noi_dung_co_quan(self):
        cac_truong_bat_buoc = [
            "id",
            "ten",
            "chuong_trinh",
            "vi_tri",
            "cau_tao",
            "chuc_nang",
            "co_che",
            "lien_he",
            "diem_thi",
            "trac_nghiem",
        ]

        for cq in DANH_SACH_15_CO_QUAN:
            fp = THU_MUC_STREAMING / f"{cq}.json"
            with open(fp, encoding="utf-8") as f:
                data = json.load(f)

            for truong in cac_truong_bat_buoc:
                self.assertIn(truong, data, f"File {cq}.json thiếu trường bắt buộc '{truong}'")

            # Kiểm tra mảng trắc nghiệm
            trac_nghiem = data["trac_nghiem"]
            self.assertIsInstance(trac_nghiem, list, f"{cq}.json: 'trac_nghiem' phải là danh sách")
            self.assertGreaterEqual(
                len(trac_nghiem), 3, f"{cq}.json: cần tối thiểu 3 câu hỏi trắc nghiệm"
            )

            for i, q in enumerate(trac_nghiem):
                self.assertIn("cau", q, f"{cq}.json câu {i}: thiếu 'cau'")
                self.assertIn("lua_chon", q, f"{cq}.json câu {i}: thiếu 'lua_chon'")
                self.assertEqual(
                    len(q["lua_chon"]), 4, f"{cq}.json câu {i}: phải có 4 phương án A, B, C, D"
                )
                self.assertIn("dap_an", q, f"{cq}.json câu {i}: thiếu 'dap_an'")
                self.assertIn(
                    q["dap_an"].upper(),
                    ["A", "B", "C", "D"],
                    f"{cq}.json câu {i}: đáp án không hợp lệ",
                )
                self.assertIn("giai_thich", q, f"{cq}.json câu {i}: thiếu 'giai_thich'")

    def test_file_meta_day_du(self):
        for cq in DANH_SACH_15_CO_QUAN:
            meta_fp = THU_MUC_STREAMING / f"{cq}.json.meta"
            self.assertTrue(meta_fp.is_file(), f"Thiếu file meta: {meta_fp.name}")

        self.assertTrue((THU_MUC_STREAMING / "danh-muc.json.meta").is_file())
        self.assertTrue((THU_MUC_STREAMING / "thuat-ngu.json.meta").is_file())


if __name__ == "__main__":
    unittest.main()
