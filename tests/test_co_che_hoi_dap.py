"""Kiểm thử giải thích chi tiết, hỏi đáp, báo sai, đọc chuỗi từ ảnh (server/ai/co_che/hoi_dap.py) – không gọi mạng.

    .venv/bin/python -m unittest tests.test_co_che_hoi_dap
"""

import asyncio
import base64
import io
import json
import os
import tempfile
import unittest
from pathlib import Path
from unittest import mock

from fastapi.testclient import TestClient

from server.ai.co_che import giai_de, hoi_dap
from server.ai.co_che.may_chu import app

BUOC = [{"ten": "Tấn công", "ghi_chu": "OH⁻ tấn công C từ phía sau."}, {"ten": "Rời nhóm", "ghi_chu": "Br⁻ rời đi."}]


def ai_gia(tra: dict):
    async def goi(system, messages, schema):
        goi.lan.append((system, messages, schema))
        return json.dumps(tra, ensure_ascii=False)
    goi.lan = []
    return goi


async def ai_thieu_khoa(system, messages, schema):
    raise giai_de.LoiGiaiDe("Chưa có khóa.", "thieu_key", 503)


def anh_png() -> bytes:
    from PIL import Image
    b = io.BytesIO()
    Image.new("RGB", (640, 320), "white").save(b, "PNG")
    return b.getvalue()


class GiaiThichChiTiet(unittest.TestCase):
    def test_giu_dung_so_buoc(self):
        goi = ai_gia({"tong_quan": "SN2 một giai đoạn.", "buoc": [{"so": 1, "tieu_de": "Nucleophin tấn công", "noi_dung": "A"},
                      {"so": 9, "tieu_de": "Thừa", "noi_dung": "B"}], "luu_y": ["x", "y", "z", "w"]})
        d = asyncio.run(hoi_dap.giai_thich_chi_tiet("SN2", "đề", "SN2", BUOC, goi))
        self.assertEqual([b["so"] for b in d["buoc"]], [1, 2])
        self.assertEqual(d["buoc"][0]["tieu_de"], "Nucleophin tấn công")
        self.assertEqual(d["buoc"][1]["noi_dung"], "Br⁻ rời đi.")          # bước AI bỏ sót lấy ghi chú gốc
        self.assertEqual(len(d["luu_y"]), 3)
        self.assertEqual(d["nguon"], "ai")
        self.assertIn("1. Tấn công", goi.lan[0][1][0]["content"])

    def test_khong_co_ai_tra_theo_mau(self):
        d = asyncio.run(hoi_dap.giai_thich_chi_tiet("SN2", "", "SN2", BUOC, ai_thieu_khoa))
        self.assertEqual(d["nguon"], "mau")
        self.assertEqual(len(d["buoc"]), 2)
        self.assertIn("khóa", d["ly_do"])

    def test_thieu_buoc(self):
        with self.assertRaises(giai_de.LoiGiaiDe):
            asyncio.run(hoi_dap.giai_thich_chi_tiet("SN2", "", "", [], ai_gia({})))


class HoiDap(unittest.TestCase):
    def test_kem_bai_va_lich_su(self):
        goi = ai_gia({"tra_loi": "Vì Br là nhóm rời tốt (Bước 2).", "goi_y": ["Vì sao không SN1?"]})
        lich = [{"vai": "ai", "noi_dung": "chào"}, {"vai": "hoc_sinh", "noi_dung": "Bước 1 là gì?"}, {"vai": "ai", "noi_dung": "Tấn công."}]
        d = asyncio.run(hoi_dap.hoi_dap("Vì sao Br rời?", "SN2", "đề", BUOC, lich, "", goi))
        self.assertEqual(d["nguon"], "ai")
        tin = goi.lan[0][1]
        self.assertEqual(tin[0]["role"], "user")
        self.assertIn("BÀI ĐANG XEM", tin[0]["content"])
        self.assertEqual(tin[-1]["content"], "Vì sao Br rời?")
        self.assertEqual([m["role"] for m in tin], ["user", "assistant", "user"])

    def test_khong_co_ai(self):
        async def rag_hong(*a, **k):
            raise RuntimeError("không có Ollama")
        with mock.patch("server.ai.co_che.giai_thich.giai_thich", rag_hong):
            d = asyncio.run(hoi_dap.hoi_dap("Vì sao?", "SN2", "", BUOC, [], "", ai_thieu_khoa))
        self.assertEqual(d["nguon"], "mau")
        self.assertIn("Bước 1", d["tra_loi"])


class BaoSai(unittest.TestCase):
    def test_ghi_jsonl_va_api(self):
        with tempfile.TemporaryDirectory() as t, mock.patch.dict(os.environ, {"VERA_BAO_SAI_DIR": t}):
            kq = hoi_dap.bao_sai("sn2_ch3br", "SN2", "Mũi tên bước 1 trỏ sai", "giay", 1)
            self.assertTrue(kq["ok"])
            r = TestClient(app).post("/co-che/bao-sai", json={"ten": "Chuỗi", "mo_ta": "Bước 2 sai điều kiện", "nguon": "chuoi"})
            self.assertEqual(r.status_code, 200, r.text)
            dong = json.loads((Path(t) / "co_che.jsonl").read_text(encoding="utf-8").splitlines()[0])
            self.assertEqual(dong["ma_bai"], "sn2_ch3br")
            self.assertEqual(dong["buoc"], 1)
            self.assertTrue((Path(t) / "chuoi.jsonl").exists())
            self.assertEqual(TestClient(app).post("/co-che/bao-sai", json={"mo_ta": "ab"}).status_code, 400)
            self.assertEqual(TestClient(app).post("/co-che/bao-sai", json={"mo_ta": "abcdef", "nguon": "../x"}).status_code, 422)


class DocChuoiAnh(unittest.TestCase):
    def test_doc_va_giai(self):
        goi = ai_gia({"chuoi": "C2H4 → C2H5OH -> CH3CHO", "ly_do": ""})
        self.assertEqual(asyncio.run(hoi_dap.doc_chuoi_tu_anh(anh_png(), goi)), "C2H4 -> C2H5OH -> CH3CHO")
        self.assertEqual(goi.lan[0][1][0]["content"][0]["type"], "image")

    def test_anh_khong_ro(self):
        with self.assertRaises(giai_de.LoiGiaiDe):
            asyncio.run(hoi_dap.doc_chuoi_tu_anh(anh_png(), ai_gia({"chuoi": "", "ly_do": "mờ"})))

    def test_api_giai_chuoi_bang_anh(self):
        async def doc(anh, goi_ai=None):
            return "C2H4 -> C2H5OH -> CH3CHO"
        with mock.patch.object(hoi_dap, "doc_chuoi_tu_anh", doc):
            r = TestClient(app).post("/co-che/giai-chuoi", json={"anh_base64": base64.b64encode(anh_png()).decode(), "muc_do": "hsg"})
        self.assertEqual(r.status_code, 200, r.text)
        d = r.json()
        self.assertEqual(d["chuoi_doc_tu_anh"], "C2H4 -> C2H5OH -> CH3CHO")
        self.assertEqual(d["tuy_chon"]["muc_do"], "hsg")
        self.assertEqual(TestClient(app).post("/co-che/giai-chuoi", json={"chuoi": ""}).status_code, 422)


if __name__ == "__main__":
    unittest.main()
