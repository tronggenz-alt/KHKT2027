"""Kiểm thử giải ĐỀ MỚI (server/ai/co_che/giai_de.py, POST /co-che/giai-de) – không gọi mạng: AI được thay bằng hàm giả trả JSON
đúng/sai theo kịch bản; kiểm vòng sửa lỗi (RDKit), đầu ra 3D/giấy, đọc ảnh đề, lỗi ảnh, thiếu khóa, nạp .env.

    .venv/bin/python -m unittest tests.test_co_che_giai_de
"""

import asyncio
import base64
import copy
import io
import json
import os
import tempfile
import time
import unittest
from pathlib import Path
from unittest import mock

from fastapi.testclient import TestClient

from server.ai.co_che import giai_de
from server.ai.co_che.may_chu import app, nap_env

VI_DU = Path(__file__).resolve().parents[1] / "server" / "ai" / "co_che" / "vi_du" / "sn2_nho.json"
DUNG = json.loads(VI_DU.read_text(encoding="utf-8"))


def co_che_sai() -> dict:
    """Mũi tên trỏ sai nguyên tử (OH⁻ tấn công C2 thay vì C3 mang Br) → RDKit báo sản phẩm không khớp / hoá trị sai."""
    d = copy.deepcopy(DUNG)
    d["buoc"][0]["mui_ten"][0]["den"]["nguyen_tu"] = 2
    return d


class AIGia:
    """Trả lần lượt các câu trả lời đã định; ghi lại mọi lần gọi (system, messages, schema)."""

    def __init__(self, *tra):
        self.tra = list(tra)
        self.goi: list[tuple[str, list, dict]] = []

    async def __call__(self, system, messages, schema):
        self.goi.append((system, copy.deepcopy(messages), schema))
        x = self.tra.pop(0) if len(self.tra) > 1 else self.tra[0]
        return x if isinstance(x, str) else json.dumps(x, ensure_ascii=False)


def anh_png(rong=640, cao=360, sang=True) -> bytes:
    from PIL import Image, ImageDraw
    img = Image.new("RGB", (rong, cao), "white" if sang else (5, 5, 5))
    ImageDraw.Draw(img).text((20, 20), "CH3CH2Br + NaOH -> ?", fill="black")
    buf = io.BytesIO()
    img.save(buf, "PNG")
    return buf.getvalue()


DOC_ANH_DUNG = {"de_bai": "Câu 1. Viết cơ chế phản ứng CH3CH2Br + NaOH loãng.",
                "phan_ung": [{"so_cau": "Câu 1", "chat_dau": ["CCBr", "[Na+].[OH-]"], "tac_nhan_dieu_kien": "NaOH loãng, t°",
                              "san_pham": [""], "yeu_cau": "vẽ cơ chế", "goi_y_loai": "SN2"}],
                "khong_chac_chan": [], "chat_luong_anh": {"doc_duoc": True, "van_de": "", "mo_ta": ""}}


def chay(coro):
    return asyncio.run(coro)


class TestGiaiDe(unittest.TestCase):
    def test_dung_ngay_ra_3d_va_giay(self):
        ai = AIGia(DUNG)
        kq = chay(giai_de.giai_de("Thủy phân bromoethane bằng NaOH loãng", goi_ai=ai))
        self.assertEqual(len(ai.goi), 1)
        self.assertIs(ai.goi[0][2], giai_de.SCHEMA_CO_CHE)
        self.assertEqual(kq["phien_ban"], 1)
        self.assertTrue(kq["ma"].startswith("de_"))
        kb, sd, bai = kq["kich_ban_3d"], kq["so_do_giay"], kq["bai"]
        self.assertGreaterEqual(len(kb["trang_thai"]), 2)
        self.assertTrue(kb["buoc"])
        self.assertTrue(kb["nguyen_tu"])
        self.assertIn("giai_thich", kb)
        self.assertTrue(sd)                                   # sơ đồ giấy có nội dung
        self.assertEqual(bai["de"], "Thủy phân bromoethane bằng NaOH loãng")
        self.assertEqual(len(bai["cac_buoc"]), 1)
        self.assertEqual(kq["thong_tin"]["so_lan_sua"], 0)
        self.assertEqual(kq["thong_tin"]["kiem_tra"], "đạt")
        json.dumps(kq, ensure_ascii=False)                    # JSON thường, tuần tự hoá được

    def test_vong_sua_loi(self):
        self.assertTrue(giai_de.kiem_tra_co_che(co_che_sai()))
        ai = AIGia("không phải JSON", co_che_sai(), DUNG)
        kq = chay(giai_de.giai_de("CH3CH2Br + OH-", goi_ai=ai))
        self.assertEqual(len(ai.goi), 3)
        self.assertEqual(kq["thong_tin"]["so_lan_sua"], 2)
        # lần sửa gửi lại cả hội thoại + lỗi RDKit cụ thể
        sys2, msgs2, _ = ai.goi[1]
        self.assertEqual([m["role"] for m in msgs2], ["user", "assistant", "user"])
        self.assertIn("JSON không đúng định dạng", msgs2[-1]["content"])
        msgs3 = ai.goi[2][1]
        self.assertEqual(len(msgs3), 5)
        self.assertIn("RDKit", msgs3[-1]["content"])
        self.assertTrue(kq["kich_ban_3d"]["trang_thai"])

    def test_sai_mai_bao_loi_422(self):
        ai = AIGia(co_che_sai())
        with self.assertRaises(giai_de.LoiGiaiDe) as cm:
            chay(giai_de.giai_de("CH3CH2Br + OH-", goi_ai=ai))
        self.assertEqual(cm.exception.status, 422)
        self.assertEqual(cm.exception.ma, "co_che_sai")
        self.assertTrue(cm.exception.chi_tiet)
        self.assertEqual(len(ai.goi), giai_de.SO_LAN_SUA + 1)

    def test_smiles_go_thang(self):
        ai = AIGia(DUNG)
        chay(giai_de.giai_de("CCBr.[Na+].[OH-]>>CCO", goi_ai=ai))
        yc = ai.goi[0][1][0]["content"]
        self.assertIn("Chất tham gia (SMILES): CCBr, [OH-]", yc)       # bỏ Na⁺ khán giả
        self.assertIn("Sản phẩm (SMILES): CCO", yc)

    def test_anh_de(self):
        ai = AIGia({**DOC_ANH_DUNG, "phan_ung": [{**DOC_ANH_DUNG["phan_ung"][0], "chat_dau": ["CC(Br", "[OH-]"]}]},  # SMILES hỏng
                   DOC_ANH_DUNG, DUNG)
        kq = chay(giai_de.giai_de("", anh=anh_png(), goi_ai=ai))
        self.assertEqual(len(ai.goi), 3)
        self.assertIs(ai.goi[0][2], giai_de.SCHEMA_DOC_ANH)
        noi_dung = ai.goi[0][1][0]["content"]
        self.assertEqual(noi_dung[0]["type"], "image")
        self.assertEqual(noi_dung[0]["source"]["media_type"], "image/png")
        self.assertIn("RDKit KHÔNG đọc được", ai.goi[1][1][-1]["content"])
        yc = ai.goi[2][1][0]["content"]
        self.assertIn("CCBr, [OH-]", yc)
        self.assertIn("thế nucleophin SN2", yc)
        self.assertEqual(kq["bai"]["de"], DOC_ANH_DUNG["de_bai"])
        self.assertEqual(kq["doc_anh"]["phan_ung"][0]["so_cau"], "Câu 1")

    def test_anh_khong_phai_de_hoa(self):
        ai = AIGia({"de_bai": "", "phan_ung": [], "khong_chac_chan": [],
                    "chat_luong_anh": {"doc_duoc": False, "van_de": "khong_phai_de_hoa", "mo_ta": "Ảnh là phong cảnh."}})
        with self.assertRaises(giai_de.LoiGiaiDe) as cm:
            chay(giai_de.giai_de("", anh=anh_png(), goi_ai=ai))
        self.assertEqual(cm.exception.thong_diep, "Ảnh là phong cảnh.")

    def test_kiem_tra_anh(self):
        for du, ma in ((b"", "anh_rong"), (b"khong phai anh" * 10, "sai_dinh_dang"), (anh_png(100, 80), "anh_qua_nho"),
                       (anh_png(sang=False), "anh_qua_toi")):
            with self.assertRaises(giai_de.LoiGiaiDe) as cm:
                giai_de.chuan_bi_anh(du)
            self.assertEqual(cm.exception.ma, ma)
        with mock.patch.object(giai_de, "ANH_TOI_DA", 1000):
            with self.assertRaises(giai_de.LoiGiaiDe) as cm:
                giai_de.chuan_bi_anh(anh_png())
            self.assertEqual(cm.exception.status, 413)
        kieu, b64 = giai_de.chuan_bi_anh(anh_png(3000, 1000))     # ảnh lớn → thu về ≤ 1600 px JPEG
        self.assertEqual(kieu, "image/jpeg")
        from PIL import Image
        self.assertLessEqual(max(Image.open(io.BytesIO(base64.b64decode(b64))).size), giai_de.CANH_LON_NHAT)

    def test_thieu_khoa(self):
        with mock.patch.dict(os.environ, {"ANTHROPIC_API_KEY": "", "ANTHROPIC_AUTH_TOKEN": ""}):
            with self.assertRaises(giai_de.LoiGiaiDe) as cm:
                chay(giai_de.goi_claude("s", [{"role": "user", "content": "x"}], {}))
        self.assertEqual(cm.exception.status, 503)
        self.assertIn("ANTHROPIC_API_KEY", cm.exception.thong_diep)

    def test_doc_json(self):
        self.assertEqual(giai_de.doc_json('```json\n{"a": 1}\n```'), {"a": 1})
        self.assertEqual(giai_de.doc_json('Đây: {"a": 2} xong'), {"a": 2})
        with self.assertRaises(ValueError):
            giai_de.doc_json("không có")


class TestNhaCungCap(unittest.TestCase):
    """Claude trước; Claude không có khóa / 401 → Gemini (REST giả lập qua _post_gemini); cả hai hỏng → 503 nêu cả hai."""

    def setUp(self):
        giai_de._claude_hong = ""
        giai_de._da_dung.clear()
        giai_de._gemini_tam_bo.clear()

    def tearDown(self):
        giai_de._claude_hong = ""

    @staticmethod
    def _gemini_tra(ds):
        goi = []

        def post(mo_hinh, than, cho):
            goi.append((mo_hinh, than))
            return ds.pop(0)
        return goi, post

    def test_claude_hong_sang_gemini(self):
        goi, post = self._gemini_tra([(404, {"error": {"message": "not found"}}),
                                      (200, {"candidates": [{"content": {"parts": [{"text": "nghĩ…", "thought": True},
                                                                                     {"text": json.dumps(DUNG)}]}}]})])

        async def claude_401(*a):
            raise giai_de.LoiGiaiDe("Khóa Claude (ANTHROPIC_API_KEY) không hợp lệ.", "sai_key", 503)
        with mock.patch.object(giai_de, "goi_claude", claude_401), mock.patch.object(giai_de, "_post_gemini", post), \
                mock.patch.dict(os.environ, {"GEMINI_API_KEY": "k-gia", "GEMINI_MODEL_CO_CHE": ""}):
            kq = chay(giai_de.giai_de("Thủy phân bromoethane", dung_cache=False))
        self.assertEqual([g[0] for g in goi], ["gemini-pro-latest", "gemini-flash-latest"])   # 404 → mô hình sau
        than = goi[1][1]
        self.assertEqual(than["generationConfig"]["responseMimeType"], "application/json")
        self.assertNotIn("additionalProperties", json.dumps(than["generationConfig"]["responseJsonSchema"]))
        self.assertEqual(kq["thong_tin"]["mo_hinh"], "gemini-flash-latest")
        self.assertTrue(kq["kich_ban_3d"]["trang_thai"])
        self.assertTrue(giai_de._claude_hong)                  # lần sau bỏ qua Claude
        self.assertNotIn("gemini-pro-latest", giai_de._gemini_tam_bo)       # 404 không tạm bỏ
        giai_de._gemini_tam_bo["gemini-pro-latest"] = time.time() + 60    # 429/quá giờ → bỏ qua mô hình đó
        self.assertEqual(giai_de.ds_mo_hinh_gemini()[0], "gemini-flash-latest")

    def test_anh_sang_inline_data(self):
        nd = giai_de._noi_dung_gemini([{"role": "user", "content": [
            {"type": "image", "source": {"type": "base64", "media_type": "image/png", "data": "QUJD"}}, {"type": "text", "text": "đọc"}]},
            {"role": "assistant", "content": "{}"}])
        self.assertEqual(nd[0]["parts"][0], {"inline_data": {"mime_type": "image/png", "data": "QUJD"}})
        self.assertEqual(nd[1]["role"], "model")

    def test_ca_hai_hong(self):
        async def claude_thieu(*a):
            raise giai_de.LoiGiaiDe("Chưa có khóa Claude.", "thieu_key", 503)
        goi, post = self._gemini_tra([(400, {"error": {"message": "API key not valid. Please pass a valid API key."}})])
        with mock.patch.object(giai_de, "goi_claude", claude_thieu), mock.patch.object(giai_de, "_post_gemini", post), \
                mock.patch.dict(os.environ, {"GEMINI_API_KEY": "k-gia"}):
            with self.assertRaises(giai_de.LoiGiaiDe) as cm:
                chay(giai_de.giai_de("x", dung_cache=False))
        self.assertEqual(cm.exception.status, 503)
        self.assertIn("Claude", cm.exception.thong_diep)
        self.assertIn("Gemini", cm.exception.thong_diep)

    def test_claude_loi_khac_khong_doi(self):
        async def claude_qua_tai(*a):
            raise giai_de.LoiGiaiDe("Claude quá tải.", "qua_tai", 503)
        with mock.patch.object(giai_de, "goi_claude", claude_qua_tai):
            with self.assertRaises(giai_de.LoiGiaiDe) as cm:
                chay(giai_de.goi_ai_mac_dinh("s", [{"role": "user", "content": "x"}], {}))
        self.assertEqual(cm.exception.ma, "qua_tai")
        self.assertFalse(giai_de._claude_hong)


class TestApiGiaiDe(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.c = TestClient(app)

    def _gia(self, *tra):
        ai = AIGia(*tra)
        thuc = giai_de.giai_de

        async def boc(van_ban="", anh=None, loai="", **_):
            return await thuc(van_ban, anh, loai, goi_ai=ai)
        return ai, mock.patch.object(giai_de, "giai_de", boc)

    def test_json_chu(self):
        ai, p = self._gia(DUNG)
        with p:
            r = self.c.post("/co-che/giai-de", json={"van_ban": "CH3CH2Br + NaOH loãng"})
        self.assertEqual(r.status_code, 200, r.text)
        d = r.json()
        self.assertIn("kich_ban_3d", d)
        self.assertIn("so_do_giay", d)

    def test_json_anh_base64_va_multipart(self):
        ai, p = self._gia(DOC_ANH_DUNG, DUNG)
        with p:
            r = self.c.post("/co-che/giai-de", json={"van_ban": "", "anh_base64": "data:image/png;base64," + base64.b64encode(anh_png()).decode()})
        self.assertEqual(r.status_code, 200, r.text)
        ai, p = self._gia(DOC_ANH_DUNG, DUNG)
        with p:
            r = self.c.post("/co-che/giai-de", data={"van_ban": "câu 1"}, files={"anh": ("de.png", anh_png(), "image/png")})
        self.assertEqual(r.status_code, 200, r.text)
        self.assertEqual(r.json()["doc_anh"]["de_bai"], DOC_ANH_DUNG["de_bai"])

    def test_loi(self):
        r = self.c.post("/co-che/giai-de", json={"van_ban": ""})
        self.assertEqual(r.status_code, 400)
        self.assertEqual(r.json()["detail"]["ma"], "thieu_de")
        r = self.c.post("/co-che/giai-de", json={"anh_base64": "@@@"})
        self.assertEqual(r.status_code, 400)
        r = self.c.post("/co-che/giai-de", content=b"x" * 10, headers={"content-type": "application/json",
                                                                      "content-length": str(giai_de.ANH_TOI_DA * 2)})
        self.assertEqual(r.status_code, 413)
        ai, p = self._gia(co_che_sai())
        with p:
            r = self.c.post("/co-che/giai-de", json={"van_ban": "CH3CH2Br + OH-"})
        self.assertEqual(r.status_code, 422)
        self.assertTrue(r.json()["detail"]["chi_tiet"])

    def test_qua_gio(self):
        async def cham(*a, **k):
            await asyncio.sleep(5)
        with mock.patch.object(giai_de, "giai_de", cham), mock.patch.dict(os.environ, {"VERA_GIAI_DE_CHO": "0.2"}):
            r = self.c.post("/co-che/giai-de", json={"van_ban": "x"})
        self.assertEqual(r.status_code, 504)


class TestNapEnv(unittest.TestCase):
    def test_chi_nap_bien_can(self):
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / ".env"
            p.write_text("# chú thích\nexport ANTHROPIC_API_KEY='k-gia'\nGEMINI_API_KEY=khac\nCLAUDE_MODEL=m-gia # ghi chú\n"
                         "CLAUDE_EFFORT_CO_CHE=\n", encoding="utf-8")
            with mock.patch.dict(os.environ, {"ANTHROPIC_API_KEY": "", "CLAUDE_MODEL": "da-co"}, clear=False):
                os.environ.pop("GEMINI_API_KEY_THU", None)
                da = nap_env(p, ("ANTHROPIC_API_KEY", "CLAUDE_MODEL", "GEMINI_API_KEY_THU", "CLAUDE_EFFORT_CO_CHE"))
                self.assertEqual(da, ["ANTHROPIC_API_KEY"])
                self.assertEqual(os.environ["ANTHROPIC_API_KEY"], "k-gia")
                self.assertEqual(os.environ["CLAUDE_MODEL"], "da-co")          # biến đã có được giữ
        self.assertEqual(nap_env(Path("/khong/co/.env")), [])


if __name__ == "__main__":
    unittest.main()
