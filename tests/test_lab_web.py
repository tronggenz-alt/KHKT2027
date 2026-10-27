"""Kiểm thử web quản lý, mạng lưới khóa API và kế hoạch robot (không gọi mạng thật, CSDL tạm)."""

import asyncio
import json
import tempfile
import time
import types
import unittest
from pathlib import Path

import httpx
from fastapi.testclient import TestClient

from server.ai import main, providers, robot_plan
from server.lab import db, keypool


def ok_reply(text="OK"):
    return httpx.Response(200, json={"choices": [{"message": {"role": "assistant", "content": text}}]})


# Danh sách mô hình giả cho GET /models của từng nhà cung cấp
MODELS = {
    "api.groq.com": ["llama-3.3-70b-versatile", "llama-3.1-8b-instant", "whisper-large-v3"],
    "api.deepseek.com": ["deepseek-chat"],
    "generativelanguage.googleapis.com": ["models/gemini-3.8-flash", "models/gemini-3.8-flash-lite", "models/text-embedding-004"],
}


def with_models(handler, models=None):
    """Trả lời GET /models bằng danh sách giả, còn lại chuyển cho handler."""
    def h(req):
        if req.method == "GET" and req.url.path.endswith("/models"):
            ids = (models or MODELS)[req.url.host]
            return httpx.Response(200, json={"object": "list", "data": [{"id": i} for i in ids]})
        return handler(req)
    return h


class TempDb(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        db.reset_for_tests(Path(self.tmp.name) / "lab.db")
        keypool.reset_cache()

    def tearDown(self):
        db.reset_for_tests(Path(self.tmp.name) / "khac.db")
        keypool.reset_cache()
        self.tmp.cleanup()


class KeyPoolTest(TempDb):
    def run_chat(self, handler, models=None, raw=False, **kw):
        async def go():
            async with httpx.AsyncClient(transport=httpx.MockTransport(handler if raw else with_models(handler, models))) as c:
                return await keypool.chat([{"role": "user", "content": "xin chào"}], 20, client=c, **kw)
        return asyncio.run(go())

    def test_xoay_vong_khoa_cung_uu_tien(self):
        a = keypool.add_key("groq", "gsk_aaaaaaaaaaaaaaaa")
        b = keypool.add_key("groq", "gsk_bbbbbbbbbbbbbbbb")
        used = [self.run_chat(lambda r: ok_reply())["khoa_id"] for _ in range(4)]
        self.assertEqual(sorted(set(used)), sorted({a, b}))
        self.assertNotEqual(used[0], used[1])  # khóa vừa dùng xuống cuối hàng

    def test_429_nghi_va_chuyen_khoa_khac(self):
        a = keypool.add_key("groq", "gsk_aaaaaaaaaaaaaaaa", uu_tien=1)
        b = keypool.add_key("gemini", "AIzaSyBBBBBBBBBBBBBBBB", uu_tien=2)

        def handler(req):
            if req.headers["authorization"].endswith("aaaa"):
                return httpx.Response(429, headers={"retry-after": "30"})
            assert "generativelanguage.googleapis.com" in str(req.url)
            return ok_reply("chào")

        res = self.run_chat(handler)
        self.assertEqual((res["khoa_id"], res["nha_cung_cap"]), (b, "gemini"))
        row = db.query("SELECT * FROM khoa_api WHERE id=?", (a,))[0]
        self.assertEqual(row["trang_thai"], "gioi_han")
        self.assertGreater(row["nghi_den"], time.time() + 20)
        self.assertEqual(self.run_chat(lambda r: ok_reply())["khoa_id"], b)  # khóa a đang nghỉ

    def test_khoa_sai_bi_khoa_mot_gio(self):
        a = keypool.add_key("deepseek", "sk-xxxxxxxxxxxxxxxxxxxxxxxx")
        with self.assertRaises(keypool.AllKeysFailed):
            self.run_chat(lambda r: httpx.Response(401, json={"error": "invalid"}))
        row = db.query("SELECT * FROM khoa_api WHERE id=?", (a,))[0]
        self.assertEqual(row["trang_thai"], "khoa_sai")
        self.assertGreater(row["nghi_den"], time.time() + 3000)
        self.assertFalse(keypool.available())
        keypool.update_key(a, bat=True)  # bật lại tay thì hết nghỉ
        self.assertTrue(keypool.available())

    def test_khoa_tat_khong_duoc_dung(self):
        a = keypool.add_key("groq", "gsk_aaaaaaaaaaaaaaaa")
        keypool.update_key(a, bat=False)
        with self.assertRaises(keypool.AllKeysFailed):
            self.run_chat(lambda r: ok_reply())

    def test_khong_nhan_nha_cung_cap_la_hay_khoa_ngan(self):
        with self.assertRaises(ValueError):
            keypool.add_key("openai", "sk-xxxxxxxxxxxxxxxxxxxx")
        with self.assertRaises(ValueError):
            keypool.add_key("groq", "ngan")

    def test_chan_dan_nham_duong_link_hay_sai_nha_cung_cap(self):
        with self.assertRaisesRegex(ValueError, "đường link"):
            keypool.add_key("gemini", "https://aistudio.google.com/apikey")
        with self.assertRaisesRegex(ValueError, "AIza"):
            keypool.add_key("gemini", "gsk_aaaaaaaaaaaaaaaaaaaaaaaa")  # khóa Groq chọn nhầm Gemini
        self.assertEqual(db.query("SELECT COUNT(*) AS n FROM khoa_api")[0]["n"], 0)

    def test_gemini_400_khoa_sai_va_than_yeu_cau(self):
        a = keypool.add_key("gemini", "AIzaSyAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")
        bodies = []

        def handler(req):
            bodies.append(json.loads(req.content))
            return httpx.Response(400, json=[{"error": {"code": 400, "message": "Please pass a valid API key", "status": "INVALID_ARGUMENT"}}])

        with self.assertRaisesRegex(keypool.AllKeysFailed, "khóa không hợp lệ"):
            self.run_chat(handler, json_mode=True)
        self.assertEqual(db.query("SELECT trang_thai FROM khoa_api WHERE id=?", (a,))[0]["trang_thai"], "khoa_sai")
        self.assertNotIn("response_format", bodies[0])  # Gemini: JSON theo lời dặn, không gửi json_object
        self.assertEqual(bodies[0]["reasoning_effort"], "none")

    def test_gemini_luoc_do_khong_nhan_thi_thu_lai_khong_luoc_do(self):
        keypool.add_key("gemini", "AIzaSyAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")
        bodies = []

        def handler(req):
            b = json.loads(req.content)
            bodies.append(b)
            if "response_format" in b:
                return httpx.Response(400, json=[{"error": {"code": 400, "message": "Invalid JSON payload: schema", "status": "INVALID_ARGUMENT"}}])
            return ok_reply('{"tra_loi": "ok", "ke_hoach": []}')

        res = self.run_chat(handler, json_mode=True, schema=robot_plan.PLAN_SCHEMA)
        self.assertEqual(bodies[0]["response_format"]["type"], "json_schema")
        self.assertNotIn("response_format", bodies[1])
        self.assertEqual(res["message"]["content"], '{"tra_loi": "ok", "ke_hoach": []}')
        self.assertEqual(db.query("SELECT trang_thai FROM khoa_api")[0]["trang_thai"], "tot")

    def test_doc_json_bi_cat_cut(self):
        full = {"tra_loi": "Mình minh họa chuẩn độ.", "ke_hoach": [
            {"ky_nang": "noi", "doi_tuong": "", "dich": "", "so_luong": 0, "hoa_chat": "", "loi": "Bước 1, nói rõ {ngoặc} trong chuỗi"},
            {"ky_nang": "rot", "doi_tuong": "coc_1", "dich": "binh_non_1", "so_luong": 25, "hoa_chat": "", "loi": ""}]}
        text = "```json\n" + json.dumps(full, ensure_ascii=False, indent=2) + "\n```"
        self.assertEqual(keypool.loads_lenient(text), full)
        cut = json.dumps(full, ensure_ascii=False)[:-30]  # hết max_tokens giữa bước 2
        part = keypool.loads_lenient(cut)
        self.assertEqual(part["tra_loi"], full["tra_loi"])
        self.assertEqual(part["ke_hoach"], full["ke_hoach"][:1])
        self.assertEqual(keypool.loads_lenient('{"tra_loi": "a", "ke_hoach": [],}'), {"tra_loi": "a", "ke_hoach": []})
        with self.assertRaises(ValueError):
            keypool.loads_lenient("không có json")

    def test_groq_van_dung_json_object(self):
        keypool.add_key("groq", "gsk_aaaaaaaaaaaaaaaa")
        bodies = []
        self.run_chat(lambda req: bodies.append(json.loads(req.content)) or ok_reply("{}"), json_mode=True)
        self.assertEqual(bodies[0]["response_format"], {"type": "json_object"})
        self.assertNotIn("reasoning_effort", bodies[0])

    def test_structured_doc_json(self):
        keypool.add_key("groq", "gsk_aaaaaaaaaaaaaaaa")

        async def go():
            async with httpx.AsyncClient(transport=httpx.MockTransport(lambda r: ok_reply('```json\n{"tra_loi": "x", "ke_hoach": []}\n```'))) as c:
                orig = keypool.chat

                async def chat(messages, max_tokens=600, json_mode=False, tools=None, client=None, schema=None):
                    assert json_mode and schema == {"type": "object"}
                    return await orig(messages, max_tokens, json_mode, tools, c, schema)
                keypool.chat = chat
                try:
                    return await keypool.structured("hệ", "người", {"type": "object"})
                finally:
                    keypool.chat = orig
        self.assertEqual(asyncio.run(go()), {"tra_loi": "x", "ke_hoach": []})

    # ----- Tự chọn mô hình mạnh nhất, tự chuyển khi lỗi -----

    def test_xep_hang_mo_hinh(self):
        gemini = keypool.rank_models("gemini", [
            "models/gemini-2.5-flash", "models/gemini-3.8-flash", "models/gemini-3.8-flash-lite", "models/gemini-3.5-pro-preview",
            "models/gemini-pro-latest", "models/gemma-3-27b-it", "models/text-embedding-004", "models/gemini-3.8-flash-preview-tts",
            "models/imagen-4.0-generate-001", "models/gemini-2.5-flash-native-audio", "models/gemini-3.8-flash-live"])
        self.assertEqual(gemini[:3], ["gemini-pro-latest", "gemini-3.5-pro-preview", "gemini-3.8-flash"])
        self.assertLess(gemini.index("gemini-3.8-flash"), gemini.index("gemini-3.8-flash-lite"))
        self.assertLess(gemini.index("gemini-3.8-flash"), gemini.index("gemini-2.5-flash"))
        self.assertEqual(gemini[-1], "gemma-3-27b-it")
        for bad in ("text-embedding-004", "gemini-3.8-flash-preview-tts", "imagen-4.0-generate-001",
                    "gemini-2.5-flash-native-audio", "gemini-3.8-flash-live"):
            self.assertNotIn(bad, gemini)
        groq = keypool.rank_models("groq", ["llama-3.1-8b-instant", "openai/gpt-oss-120b", "openai/gpt-oss-20b", "whisper-large-v3",
                                            "meta-llama/llama-guard-4-12b", "llama-3.3-70b-versatile", "groq/compound"])
        self.assertEqual(groq[0], "openai/gpt-oss-120b")
        self.assertEqual(groq[-1], "llama-3.1-8b-instant")
        self.assertEqual(len(groq), 4)
        self.assertEqual(keypool.rank_models("deepseek", ["deepseek-chat", "deepseek-reasoner"])[0], "deepseek-reasoner")

    def test_mo_hinh_bi_go_404_thi_chuyen_mo_hinh_khac(self):
        a = keypool.add_key("gemini", "AIzaSyAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")
        models = {"generativelanguage.googleapis.com": ["models/gemini-2.5-flash", "models/gemini-3.8-flash"]}
        asked = []

        def handler(req):
            m = json.loads(req.content)["model"]
            asked.append(m)
            if m == "gemini-3.8-flash":
                return httpx.Response(404, json=[{"error": {"code": 404, "message": "This model models/gemini-3.8-flash is no longer available to new users."}}])
            return ok_reply("chào")

        res = self.run_chat(handler, models)
        self.assertEqual(asked, ["gemini-3.8-flash", "gemini-2.5-flash"])  # mạnh nhất trước, bị gỡ thì lùi
        self.assertEqual((res["khoa_id"], res["mo_hinh"]), (a, "gemini-2.5-flash"))
        asked.clear()
        self.run_chat(handler, models)
        self.assertEqual(asked, ["gemini-2.5-flash"])  # mô hình bị gỡ đang nghỉ, không gọi lại
        row = keypool.list_keys()[0]
        self.assertEqual(row["mo_hinh_dang_dung"], "gemini-2.5-flash")
        self.assertIn("không còn", row["thu_tu_mo_hinh"][0]["ly_do"])

    def test_groq_mo_hinh_khong_ton_tai(self):
        keypool.add_key("groq", "gsk_aaaaaaaaaaaaaaaa")

        def handler(req):
            if json.loads(req.content)["model"] == "llama-3.3-70b-versatile":
                return httpx.Response(404, json={"error": {"message": "The model `llama-3.3-70b-versatile` does not exist or you do not have access to it.",
                                                           "type": "invalid_request_error", "code": "model_not_found"}})
            return ok_reply()
        self.assertEqual(self.run_chat(handler)["mo_hinh"], "llama-3.1-8b-instant")

    def test_429_cua_mot_mo_hinh_thi_dung_mo_hinh_khac_cung_khoa(self):
        a = keypool.add_key("gemini", "AIzaSyAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")

        def handler(req):
            if json.loads(req.content)["model"] == "gemini-3.8-flash":
                return httpx.Response(429, headers={"retry-after": "40"}, json=[{"error": {"message": "Quota exceeded, limit: 20"}}])
            return ok_reply()
        res = self.run_chat(handler)
        self.assertEqual((res["khoa_id"], res["mo_hinh"]), (a, "gemini-3.8-flash-lite"))
        self.assertTrue(keypool.available())  # khóa vẫn dùng được, chỉ mô hình kia nghỉ

    def test_mo_hinh_ghim_loi_van_tu_chuyen(self):
        keypool.add_key("groq", "gsk_aaaaaaaaaaaaaaaa", mo_hinh="llama-3.3-70b-specdec")
        asked = []

        def handler(req):
            m = json.loads(req.content)["model"]
            asked.append(m)
            return httpx.Response(400, json={"error": {"message": f"model {m} has been decommissioned"}}) if m.endswith("specdec") else ok_reply()
        self.assertEqual(self.run_chat(handler)["mo_hinh"], "llama-3.3-70b-versatile")
        self.assertEqual(asked[0], "llama-3.3-70b-specdec")  # mô hình ghim được thử trước

    def test_gemini_pro_khong_tat_duoc_suy_nghi(self):
        keypool.add_key("gemini", "AIzaSyAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")
        bodies = []

        def handler(req):
            b = json.loads(req.content)
            bodies.append(b)
            if b.get("reasoning_effort") == "none":
                return httpx.Response(400, json=[{"error": {"message": "Budget 0 is invalid. This model only works in thinking mode."}}])
            if b.get("reasoning_effort") == "minimal":
                return httpx.Response(400, json=[{"error": {"message": "Thinking level MINIMAL is not supported for this model."}}])
            return ok_reply()
        res = self.run_chat(handler, {"generativelanguage.googleapis.com": ["models/gemini-3.5-pro"]})
        self.assertEqual(res["mo_hinh"], "gemini-3.5-pro")
        self.assertEqual([b.get("reasoning_effort") for b in bodies], ["none", "minimal", "low"])
        self.assertEqual(bodies[0]["max_tokens"], 20)
        self.assertEqual(bodies[2]["max_tokens"], 20 + keypool.THINK_ROOM)  # chừa chỗ cho phần suy nghĩ

    def test_mo_hinh_chi_ho_tro_api_khac_bi_bo_qua(self):
        keypool.add_key("gemini", "AIzaSyAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")

        def handler(req):
            if json.loads(req.content)["model"] == "gemini-3.8-flash":
                return httpx.Response(400, json=[{"error": {"code": 400, "message": "This model only supports Interactions API."}}])
            return ok_reply()
        self.assertEqual(self.run_chat(handler)["mo_hinh"], "gemini-3.8-flash-lite")
        self.assertIn("không còn", keypool.list_keys()[0]["thu_tu_mo_hinh"][0]["ly_do"])

    def test_khong_lay_duoc_danh_sach_thi_dung_du_phong(self):
        keypool.add_key("deepseek", "sk-xxxxxxxxxxxxxxxxxxxxxxxx")

        def handler(req):
            if req.method == "GET":
                return httpx.Response(500)
            return ok_reply()
        self.assertEqual(self.run_chat(handler, raw=True)["mo_hinh"], keypool.PROVIDERS["deepseek"]["du_phong"][0])

    def test_mo_hinh_tra_loi_qua_lau_thi_chuyen(self):
        keypool.add_key("groq", "gsk_aaaaaaaaaaaaaaaa")

        def handler(req):
            if json.loads(req.content)["model"] == "llama-3.3-70b-versatile":
                raise httpx.ReadTimeout("chậm", request=req)
            return ok_reply()
        self.assertEqual(self.run_chat(handler)["mo_hinh"], "llama-3.1-8b-instant")


class LabApiTest(TempDb):
    def setUp(self):
        super().setUp()
        self.client = TestClient(main.app)

    def test_trang_web(self):
        r = self.client.get("/")
        self.assertEqual(r.status_code, 200)
        self.assertIn("Mạng lưới khóa AI", r.text)

    def test_tong_quan_va_tim_kiem(self):
        d = self.client.get("/api/tong-quan").json()
        self.assertEqual(d["so_hoa_chat"], len(db.CHEM["hoa_chat"]))
        self.assertEqual(d["sap_het"], 0)
        r = self.client.get("/api/hoa-chat", params={"q": "dong sunfat"}).json()
        self.assertIn(253, [c["stt"] for c in r["ds"]])

    def test_xuat_kho_ghi_nhat_ky_va_bao_sap_het(self):
        r = self.client.patch("/api/hoa-chat/253", json={"them": -450}).json()
        self.assertEqual(r["so_luong"], 50)
        self.assertTrue(r["sap_het"])
        self.assertEqual(self.client.get("/api/tong-quan").json()["sap_het"], 1)
        log = self.client.get("/api/nhat-ky", params={"loai": "kho"}).json()
        self.assertIn("Xuất 450", log[0]["tieu_de"])

    def test_thiet_bi_gioi_han_so_hong(self):
        r = self.client.patch("/api/thiet-bi/1", json={"so_luong": 4, "hong": 9}).json()
        self.assertEqual((r["so_luong"], r["hong"]), (4, 4))

    def test_khoa_khong_bao_gio_lo_ra(self):
        secret = "gsk_SECRETSECRETSECRET1234"
        self.assertEqual(self.client.post("/api/khoa", json={"nha_cung_cap": "groq", "khoa": secret}).status_code, 200)
        text = self.client.get("/api/khoa").text + self.client.get("/api/tong-quan").text
        self.assertNotIn(secret, text)
        self.assertIn("gsk_…1234", text)
        self.assertEqual(self.client.post("/api/khoa", json={"nha_cung_cap": "abc", "khoa": secret}).status_code, 400)

    def test_ghi_nhat_ky_tu_unity(self):
        self.client.post("/api/nhat-ky", json={"loai": "su_co", "tieu_de": "Khí Cl2 thoát ra", "muc_do": "Warning", "nguon": "phong_lab_ao"})
        self.assertEqual(self.client.get("/api/tong-quan").json()["su_co_7_ngay"], 1)


class RobotPlanTest(TempDb):
    IDS = {"ong_nghiem_1", "coc_1", "cho_dat_1", "may_1"}

    def test_kiem_tra_ke_hoach(self):
        plan = [
            {"ky_nang": "lay_hoa_chat", "doi_tuong": "", "dich": "", "so_luong": 0, "hoa_chat": "CuSO4", "loi": ""},
            {"ky_nang": "rot", "doi_tuong": "hoa_chat:CuSO4", "dich": "ong_nghiem_1", "so_luong": 500, "hoa_chat": "", "loi": ""},
            {"ky_nang": "bay_len_troi", "doi_tuong": "", "dich": "", "so_luong": 0, "hoa_chat": "", "loi": ""},
            {"ky_nang": "cam", "doi_tuong": "binh_khong_co", "dich": "", "so_luong": 0, "hoa_chat": "", "loi": ""},
            {"ky_nang": "dat", "doi_tuong": "", "dich": "dau_do", "so_luong": 0, "hoa_chat": "", "loi": ""},
            {"ky_nang": "nho_giot", "doi_tuong": "hoa_chat:NaOH", "dich": "ong_nghiem_1", "so_luong": 0, "hoa_chat": "", "loi": ""},
            {"ky_nang": "bat_tat", "doi_tuong": "may_1", "dich": "", "so_luong": 0, "hoa_chat": "", "loi": ""},
        ]
        steps, dropped = robot_plan.validate(plan, self.IDS, main.find_chemical)
        self.assertEqual([s["ky_nang"] for s in steps], ["lay_hoa_chat", "rot", "dat", "nho_giot", "bat_tat"])
        self.assertEqual(steps[0]["stt"], 253)
        self.assertEqual(steps[1]["doi_tuong"], "hoa_chat:253")
        self.assertEqual(steps[1]["so_luong"], 100)  # giới hạn rót
        self.assertEqual(steps[2]["dich"], "ban_nguoi_dung")
        self.assertEqual(steps[3]["doi_tuong"], "hoa_chat:124")
        self.assertEqual(steps[3]["so_luong"], 1)
        self.assertEqual(len(dropped), 2)

    def test_tim_hoa_chat_ten_tieng_anh_va_cong_thuc_khac_cach_viet(self):
        f = lambda q: main.find_chemical(q)["ten"]  # noqa: E731
        self.assertEqual(f("oxalic acid"), "Axit oxalic")            # tiếng Anh, đảo thứ tự từ
        self.assertEqual(f("acid oxalic"), "Axit oxalic")
        self.assertEqual(f("potassium permanganate"), "Kali pemanganat")
        self.assertEqual(f("copper(II) sulfate")[:12], "Đồng(II) sun")
        self.assertIn(main.find_chemical("H2C2O4")["stt"], (509, 708))  # = (COOH)2 trong danh mục
        self.assertEqual(f("KMnO4"), "Kali pemanganat")               # ưu tiên dung dịch để rót được
        self.assertEqual(f("kali pemanganat rắn"), "Kali pemanganat rắn")
        self.assertEqual(main.formula_key("(COOH)2"), main.formula_key("H2C2O4"))
        self.assertEqual(main.formula_key("CuSO4.5H2O"), main.formula_key("CuSO4"))
        self.assertEqual(main.formula_key("Axit"), "")

    def test_bo_buoc_bao_ro_hoa_chat_khong_co(self):
        plan = [
            {"ky_nang": "lay_hoa_chat", "doi_tuong": "", "dich": "", "so_luong": 0, "hoa_chat": "oxalic acid", "loi": ""},
            {"ky_nang": "rot", "doi_tuong": "hoa_chat:oxalic acid", "dich": "ong_nghiem_1", "so_luong": 2, "hoa_chat": "", "loi": ""},
            {"ky_nang": "rot", "doi_tuong": "hoa_chat:thuoc thu khong ton tai xyz", "dich": "ong_nghiem_1", "so_luong": 2, "hoa_chat": "", "loi": ""},
            {"ky_nang": "noi", "doi_tuong": "", "dich": "", "so_luong": 0, "hoa_chat": "", "loi": "Thuốc tím mất màu."},
        ]
        steps, dropped = robot_plan.validate(plan, self.IDS, main.find_chemical)
        self.assertEqual([s["ky_nang"] for s in steps], ["lay_hoa_chat", "rot", "noi"])
        self.assertEqual(steps[1]["doi_tuong"], f"hoa_chat:{steps[0]['stt']}")
        self.assertEqual(dropped, ["bước 3: hóa chất 'thuoc thu khong ton tai xyz' không có trong danh mục"])

    def test_toi_da_12_buoc(self):
        plan = [{"ky_nang": "noi", "doi_tuong": "", "dich": "", "so_luong": 0, "hoa_chat": "", "loi": f"bước {i}"} for i in range(30)]
        self.assertEqual(len(robot_plan.validate(plan, set(), main.find_chemical)[0]), robot_plan.MAX_STEPS)

    def test_ky_nang_moi_va_gioi_han(self):
        plan = [
            {"ky_nang": "dun_nong", "doi_tuong": "ong_nghiem_1", "dich": "bep_la", "so_luong": 500, "hoa_chat": "", "loi": ""},
            {"ky_nang": "tia_nuoc", "doi_tuong": "coc_1", "dich": "", "so_luong": 0, "hoa_chat": "", "loi": ""},
            {"ky_nang": "phan_tich", "doi_tuong": "ong_nghiem_1", "dich": "may_1", "so_luong": 0, "hoa_chat": "", "loi": ""},
            {"ky_nang": "do_ph", "doi_tuong": "", "dich": "", "so_luong": 0, "hoa_chat": "", "loi": ""},
            {"ky_nang": "suc_khi", "doi_tuong": "hoa_chat:Khí NH3", "dich": "coc_1", "so_luong": 99, "hoa_chat": "", "loi": ""},
            {"ky_nang": "chuan_do", "doi_tuong": "", "dich": "", "so_luong": 0, "hoa_chat": "", "loi": ""},
        ]
        steps, dropped = robot_plan.validate(plan, self.IDS, main.find_chemical)
        self.assertEqual([s["ky_nang"] for s in steps], ["dun_nong", "tia_nuoc", "phan_tich", "suc_khi", "chuan_do"])
        self.assertEqual((steps[0]["so_luong"], steps[0]["dich"]), (100, ""))  # trần 100 °C, mã bếp lạ thì để robot chọn
        self.assertEqual((steps[1]["dich"], steps[1]["so_luong"]), ("coc_1", 5))
        self.assertEqual(steps[2]["dich"], "may_1")
        self.assertEqual(steps[3]["doi_tuong"], "hoa_chat:648")
        self.assertEqual(steps[3]["so_luong"], 30)
        self.assertEqual(len(dropped), 1)  # do_ph thiếu mã vật

    def test_thi_nghiem_mau(self):
        self.assertEqual(robot_plan.find_template("nhận biết Cu2+"), "nhan_biet_cu2")
        self.assertEqual(robot_plan.find_template("Chuẩn độ"), "chuan_do")
        self.assertEqual(robot_plan.find_template("kẽm với axit"), "kim_loai_axit")
        self.assertEqual(robot_plan.find_template("ly_tam_ket_tua"), "ly_tam_ket_tua")
        self.assertIsNone(robot_plan.find_template("chế tạo pháo hoa"))
        plan = [{"ky_nang": "thi_nghiem_mau", "doi_tuong": "", "dich": "", "so_luong": 0, "hoa_chat": "Nhận biết Fe3+", "loi": ""},
                {"ky_nang": "thi_nghiem_mau", "doi_tuong": "", "dich": "", "so_luong": 0, "hoa_chat": "bom khói", "loi": ""}]
        steps, dropped = robot_plan.validate(plan, set(), main.find_chemical)
        self.assertEqual(steps[0]["doi_tuong"], "nhan_biet_fe3")
        self.assertEqual(len(dropped), 1)

    def test_khoa_mau_trung_voi_unity(self):
        cs = (Path(__file__).resolve().parents[1] / "Phong thi nghiem ao/Assets/Scripts/Demo/RobotDemos.cs").read_text(encoding="utf-8")
        import re
        unity_all = set(re.findall(r'\("([a-z0-9_]+)", "', cs.split("public static readonly")[1].split("};")[0]))
        unity_cases = set(re.findall(r'case "([a-z0-9_]+)":', cs))
        self.assertEqual(unity_all, set(robot_plan.TEMPLATES))
        self.assertEqual(unity_cases, set(robot_plan.TEMPLATES))

    def test_ky_nang_trung_voi_unity(self):
        root = Path(__file__).resolve().parents[1] / "Phong thi nghiem ao/Assets/Scripts/Demo"
        import re
        cs = (root / "LabRobot.cs").read_text(encoding="utf-8") + (root / "LabRobot.Skills.cs").read_text(encoding="utf-8")
        handled = set(re.findall(r'case "([a-z_]+)":', cs))
        self.assertEqual(set(robot_plan.SKILLS) - handled, set())

    def test_luoc_do_khop_ky_nang(self):
        item = robot_plan.PLAN_SCHEMA["properties"]["ke_hoach"]["items"]
        self.assertEqual(set(item["properties"]["ky_nang"]["enum"]), set(robot_plan.SKILLS))
        self.assertEqual(set(item["required"]), set(item["properties"]))

    def test_anh_chup_canh_thanh_van_ban(self):
        text = robot_plan.format_scene({"khu_vuc": "Bàn 4", "vat": [{"id": "coc_1", "ten": "Cốc 100 ml", "loai": "coc", "chua": "trống", "khoang_cach": 0.62}]})
        self.assertIn("coc_1 | Cốc 100 ml | coc | 0.6 m | trống", text)


class RobotEndpointTest(TempDb):
    def setUp(self):
        super().setUp()
        self.client = TestClient(main.app)
        self._pick = main.providers.pick

    def tearDown(self):
        main.providers.pick = self._pick
        super().tearDown()

    def test_ke_hoach_qua_kiem_tra_va_ghi_nhat_ky(self):
        seen = {}

        async def structured(system, user, schema, max_tokens=800):
            seen["user"], seen["schema"] = user, schema
            return {"tra_loi": "Mình rót CuSO4 vào ống 1.", "ke_hoach": [
                {"ky_nang": "rot", "doi_tuong": "hoa_chat:253", "dich": "ong_nghiem_1", "so_luong": 2, "hoa_chat": "", "loi": ""},
                {"ky_nang": "cam", "doi_tuong": "khong_co", "dich": "", "so_luong": 0, "hoa_chat": "", "loi": ""}]}

        main.providers.pick = lambda: types.SimpleNamespace(NAME="gia", structured=structured)
        scene = {"khu_vuc": "Bàn định tính", "vat": [{"id": "ong_nghiem_1", "ten": "Ống nghiệm 1", "loai": "ong_nghiem"}]}
        r = self.client.post("/robot/ke-hoach", json={"cau_hoi": "rót đồng sunfat", "the_gioi": scene}).json()
        self.assertIn("ong_nghiem_1 | Ống nghiệm 1", seen["user"])
        self.assertIs(seen["schema"], robot_plan.PLAN_SCHEMA)
        self.assertEqual([s["ky_nang"] for s in r["ke_hoach"]], ["rot"])
        self.assertEqual(r["ke_hoach"][0]["so_luong"], 2)
        self.assertEqual(len(r["bo_qua"]), 1)
        log = self.client.get("/api/nhat-ky", params={"loai": "robot"}).json()
        self.assertEqual(log[0]["muc_do"], "Warning")

    def test_khong_co_ai(self):
        main.providers.pick = lambda: None
        r = self.client.post("/robot/ke-hoach", json={"cau_hoi": "lấy NaOH"}).json()
        self.assertEqual((r["nguon"], r["ke_hoach"]), ("khong", []))

    def test_chuoi_nha_cung_cap_tu_chuyen_khi_loi(self):
        async def broken(*a):
            raise keypool.AllKeysFailed("hết khóa")

        async def good(system, user, schema, max_tokens=800):
            return {"tra_loi": "ok", "ke_hoach": []}

        chain = providers.Chain([types.SimpleNamespace(NAME="mang_luoi_khoa", structured=broken, model_name=lambda: "a"),
                                 types.SimpleNamespace(NAME="claude", structured=good, model_name=lambda: "b")])
        self.assertEqual(asyncio.run(chain.structured("s", "u", {})), {"tra_loi": "ok", "ke_hoach": []})
        self.assertEqual(chain.NAME, "claude")


if __name__ == "__main__":
    unittest.main()
