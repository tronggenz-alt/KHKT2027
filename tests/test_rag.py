"""Kiểm thử RAG: cắt đoạn, tìm từ khóa không dấu, tìm lai với vector giả, lưu/nạp kho, nối vào máy chủ AI (không gọi mạng)."""

import asyncio
import json
import os
import tempfile
import types
import unittest
from pathlib import Path

from fastapi.testclient import TestClient

from server.ai import main, providers, rag
from server.lab import db
from tests.ho_tro_dang_nhap import dang_nhap_giao_vien

MAU = """---
mon: hoa
lop: 10
loai: ly_thuyet
chu_de: Phản ứng oxi hóa – khử
tu_khoa: [chất khử, chất oxi hóa]
nguon:
  - Chương trình GDPT 2018
  - https://example.org
---

# Phản ứng oxi hóa – khử

Mở đầu ngắn về phản ứng oxi hóa – khử trong chương trình lớp 10, nói về sự nhường và nhận electron giữa các chất.

## Chất khử và chất oxi hóa

Chất khử là chất nhường electron, số oxi hóa của nó tăng. Chất oxi hóa là chất nhận electron, số oxi hóa giảm.
Ví dụ trong phản ứng Fe + CuSO4 → FeSO4 + Cu thì Fe là chất khử.

## Cân bằng bằng phương pháp thăng bằng electron

Bước 1 xác định số oxi hóa, bước 2 viết quá trình oxi hóa và quá trình khử, bước 3 tìm hệ số sao cho tổng electron nhường bằng tổng electron nhận.
"""


def doan(nguon, tieu_de, noi_dung, vec=None, mon="hoa"):
    return rag.Doan(nguon=nguon, tieu_de=tieu_de, noi_dung=noi_dung, mon=mon, vec=vec)


class CatDoanTest(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.data = Path(self.tmp.name)
        (self.data / "kien_thuc" / "hoa" / "lop10").mkdir(parents=True)
        self.tep = self.data / "kien_thuc" / "hoa" / "lop10" / "04-phan-ung-oxi-hoa-khu.md"
        self.tep.write_text(MAU, encoding="utf-8")

    def tearDown(self):
        self.tmp.cleanup()

    def test_front_matter(self):
        meta, body = rag.doc_front_matter(MAU)
        self.assertEqual(meta["mon"], "hoa")
        self.assertEqual(meta["tu_khoa"], ["chất khử", "chất oxi hóa"])
        self.assertEqual(meta["nguon"], ["Chương trình GDPT 2018", "https://example.org"])
        self.assertTrue(body.startswith("# Phản ứng"))

    def test_cat_theo_muc(self):
        ds = rag.cat_markdown(self.tep, self.data)
        tieu_de = [d.tieu_de for d in ds]
        self.assertEqual(len(ds), 3)  # giới thiệu + 2 mục
        self.assertIn("Hóa học – lớp 10: Phản ứng oxi hóa – khử › Chất khử và chất oxi hóa", tieu_de)
        self.assertTrue(all(d.nguon == "kien_thuc/hoa/lop10/04-phan-ung-oxi-hoa-khu.md" for d in ds))
        self.assertIn("Từ khóa: chất khử", ds[0].noi_dung)

    def test_muc_qua_dai_bi_tach(self):
        dai = "\n\n".join(f"Đoạn văn số {i} " + "nội dung " * 60 for i in range(12))
        self.tep.write_text(MAU + "\n## Mục rất dài về số oxi hóa\n\n" + dai, encoding="utf-8")
        ds = [d for d in rag.cat_markdown(self.tep, self.data) if "Mục rất dài" in d.tieu_de]
        self.assertGreater(len(ds), 1)
        self.assertTrue(all(len(d.noi_dung) <= rag.DOAN_TOI_DA for d in ds))
        self.assertIn("(phần 1/", ds[0].tieu_de)

    def test_bo_qua_readme_va_kiem_tra(self):
        (self.data / "kien_thuc" / "README.md").write_text("# Hướng dẫn\n\n## Mục\n" + "chữ " * 80, encoding="utf-8")
        (self.data / "kien_thuc" / "_kiem_tra").mkdir()
        (self.data / "kien_thuc" / "_kiem_tra" / "x.md").write_text("## A\n" + "chữ " * 80, encoding="utf-8")
        ds = rag.cat_tat_ca(self.data / "kien_thuc", self.data)
        self.assertEqual({d.nguon for d in ds}, {"kien_thuc/hoa/lop10/04-phan-ung-oxi-hoa-khu.md"})

    def test_du_lieu_du_an(self):
        ds = rag.cat_du_lieu(rag.ROOT / "data")
        self.assertGreater(len(ds), 1000)
        kho = rag.Kho(ds)
        top = kho.tim("lọ NaOH để ở kệ nào", 3)
        self.assertTrue(top and top[0].nguon == "chem/hoa_chat.json" and "NaOH" in top[0].tieu_de)
        self.assertIn("Vị trí trong phòng hóa chất", top[0].noi_dung)


class TimKiemTest(unittest.TestCase):
    def test_go_khong_dau_van_tim_duoc(self):
        kho = rag.Kho([doan("a.md", "Chất khử", "Chất khử nhường electron, số oxi hóa tăng."),
                       doan("b.md", "Tế bào nhân sơ", "Vi khuẩn là tế bào nhân sơ, chưa có màng nhân.", mon="sinh")])
        self.assertEqual(kho.tim("chat khu la gi", 1)[0].nguon, "a.md")
        self.assertEqual(kho.tim("TẾ BÀO NHÂN SƠ", 1)[0].nguon, "b.md")
        self.assertEqual(kho.tim("H₂SO₄", 1), [])  # không có gì khớp thì không trả bừa

    def test_loc_theo_mon(self):
        kho = rag.Kho([doan("a.md", "Enzyme", "enzyme xúc tác", mon="sinh"), doan("b.md", "Chất xúc tác", "enzyme là chất xúc tác sinh học")])
        self.assertEqual([d.nguon for d in kho.tim("enzyme", 5, mon="hoa")], ["b.md"])

    def test_tim_lai_dung_vector(self):
        # từ khóa không khớp, chỉ vector chỉ ra đoạn đúng
        kho = rag.Kho([doan("a.md", "Quang hợp", "Lục lạp hấp thụ ánh sáng", vec=[1.0, 0.0]),
                       doan("b.md", "Hô hấp tế bào", "Ti thể tạo ATP", vec=[0.0, 1.0])])
        self.assertTrue(kho.co_vector)
        self.assertEqual(kho.tim("cây làm thức ăn thế nào", 1, qvec=[0.9, 0.1])[0].nguon, "a.md")

    def test_moi_file_toi_da_hai_doan(self):
        kho = rag.Kho([doan("a.md", f"Muối {i}", "muối ăn natri clorua") for i in range(4)] + [doan("b.md", "Muối", "natri clorua muối")])
        nguon = [d.nguon for d in kho.tim("muối natri clorua", 5)]
        self.assertEqual(nguon.count("a.md"), 2)
        self.assertIn("b.md", nguon)

    def test_dinh_dang_theo_ngan_sach(self):
        ds = [doan("a.md", f"Đoạn {i}", "x" * 1000) for i in range(5)]
        txt = rag.dinh_dang(ds, max_ky_tu=2500)
        self.assertLessEqual(len(txt), 2600)
        self.assertTrue(txt.startswith("[1] Đoạn 0"))
        self.assertEqual(rag.dinh_dang([]), "")


class LuuNapTest(unittest.TestCase):
    def test_luu_nap_va_dung_lai_vector(self):
        with tempfile.TemporaryDirectory() as t:
            p = Path(t) / "rag.db"
            ds = [doan("a.md", "Chất khử", "nhường electron", vec=[0.6, 0.8]), doan("b.md", "Chất oxi hóa", "nhận electron", vec=[1.0, 0.0])]
            rag.luu(ds, p, "bge-m3")
            kho = rag.nap(p)
            self.assertEqual(kho.nguon, "db")
            self.assertTrue(kho.co_vector)
            self.assertEqual(kho.mo_hinh_vec, "bge-m3")
            self.assertAlmostEqual(kho.doan[0].vec[1], 0.8, places=5)
            cu = rag.vector_cu(p, "bge-m3")
            self.assertEqual(set(cu), {ds[0].bam, ds[1].bam})
            self.assertEqual(rag.vector_cu(p, "mo-hinh-khac"), {})
            self.assertIsNone(rag.nap(Path(t) / "khong-co.db"))


def fake_provider(reply):
    calls = []

    async def text(system, user, max_tokens=400):
        calls.append(("text", system, user))
        return "Trả lời thử."

    async def structured(system, user, schema, max_tokens=800):
        calls.append(("structured", system, user))
        return reply

    return types.SimpleNamespace(NAME="gia", model_name=lambda: "gia", text=text, structured=structured), calls


class MayChuTest(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        db.reset_for_tests(Path(self.tmp.name) / "lab.db")
        self.client = TestClient(main.app)
        dang_nhap_giao_vien(self.client)
        self._pick = main.providers.pick
        main._cache.clear()
        rag.dat_kho(rag.Kho([doan("kien_thuc/hoa/lop10/04.md", "Hóa học – lớp 10: Phản ứng oxi hóa – khử › Chất khử",
                                  "Chất khử là chất nhường electron, số oxi hóa tăng.")]))

    def tearDown(self):
        main.providers.pick = self._pick
        rag.dat_kho(None)
        os.environ.pop("RAG_TAT", None)
        db.reset_for_tests(Path(self.tmp.name) / "khac.db")
        self.tmp.cleanup()

    def test_tro_ly_kem_tai_lieu(self):
        mod, calls = fake_provider({"tra_loi": "Chất khử nhường electron.", "hanh_dong": []})
        main.providers.pick = lambda: mod
        r = self.client.post("/tro-ly", json={"cau_hoi": "chất khử là gì vậy"}).json()
        self.assertEqual(r["tra_loi"], "Chất khử nhường electron.")
        self.assertIn("TÀI LIỆU THAM KHẢO", calls[0][2])
        self.assertIn("số oxi hóa tăng", calls[0][2])

    def test_hoi_kem_tai_lieu(self):
        mod, calls = fake_provider(None)
        main.providers.pick = lambda: mod
        self.client.post("/hoi", json={"cau_hoi": "chất khử là gì", "ngu_canh": {}})
        self.assertIn("TÀI LIỆU THAM KHẢO", calls[0][2])

    def test_khong_lien_quan_thi_khong_them(self):
        mod, calls = fake_provider({"tra_loi": "ok", "hanh_dong": []})
        main.providers.pick = lambda: mod
        self.client.post("/tro-ly", json={"cau_hoi": "xin chào robot"})
        self.assertNotIn("TÀI LIỆU THAM KHẢO", calls[0][2])

    def test_tat_rag(self):
        os.environ["RAG_TAT"] = "1"
        mod, calls = fake_provider({"tra_loi": "ok", "hanh_dong": []})
        main.providers.pick = lambda: mod
        self.client.post("/tro-ly", json={"cau_hoi": "chất khử là gì"})
        self.assertNotIn("TÀI LIỆU THAM KHẢO", calls[0][2])
        self.assertEqual(asyncio.run(rag.tim("chất khử")), [])

    def test_suc_khoe_bao_rag(self):
        main.providers.pick = lambda: None
        r = self.client.get("/suc-khoe").json()
        self.assertEqual(r["rag"]["so_doan"], 1)
        self.assertFalse(r["rag"]["vector"])


class ThuTuNhaCungCapTest(unittest.TestCase):
    def setUp(self):
        self.goc = {n: (m.available, os.environ.get(k)) for n, m, k in
                    [("keypool", providers.keypool, "AI_THU_TU"), ("claude", providers.claude, "AI_PROVIDER"), ("openai", providers.openai_compat, "")]}
        for m in (providers.keypool, providers.claude, providers.openai_compat):
            m.available = lambda: True
        os.environ.pop("AI_PROVIDER", None)

    def tearDown(self):
        providers.keypool.available = self.goc["keypool"][0]
        providers.claude.available = self.goc["claude"][0]
        providers.openai_compat.available = self.goc["openai"][0]
        os.environ.pop("AI_THU_TU", None)

    def test_mac_dinh_khoa_truoc(self):
        os.environ.pop("AI_THU_TU", None)
        self.assertEqual([p.NAME for p in providers.pick().items], [providers.keypool.NAME, "claude", "openai_compat"])

    def test_32b_truoc_api_sau(self):
        os.environ["AI_THU_TU"] = "openai_compat, mang_luoi_khoa ,claude"
        self.assertEqual([p.NAME for p in providers.pick().items], ["openai_compat", providers.keypool.NAME, "claude"])

    def test_chi_neu_mot_nha_thi_cac_nha_khac_van_du_phong(self):
        os.environ["AI_THU_TU"] = "openai_compat"
        self.assertEqual(providers.pick().items[0].NAME, "openai_compat")
        self.assertEqual(len(providers.pick().items), 3)


class KhoThatTest(unittest.TestCase):
    """Tài liệu thật trong data/kien_thuc đúng quy cách (bỏ qua khi chưa có file nào)."""

    def test_quy_cach(self):
        teps = [p for p in rag.KIEN_THUC.rglob("*.md") if p.name.lower() != "readme.md"]
        if not teps:
            self.skipTest("chưa có tài liệu")
        for p in teps:
            meta, body = rag.doc_front_matter(p.read_text(encoding="utf-8"))
            with self.subTest(tep=p.name):
                for k in ("mon", "lop", "loai", "chu_de"):
                    self.assertIn(k, meta)
                self.assertIn("\n## ", "\n" + body)
        for p in (rag.KIEN_THUC / "_kiem_tra").glob("*.json"):
            for it in json.loads(p.read_text(encoding="utf-8")):
                with self.subTest(kiem_tra=p.name, cau=it.get("cau_hoi")):
                    self.assertTrue((rag.KIEN_THUC / it["tep"]).exists())


if __name__ == "__main__":
    unittest.main()
