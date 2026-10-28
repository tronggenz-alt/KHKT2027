"""AI Bio-Tutor qua máy chủ (/api/sinh-hoc/hoi): cần đăng nhập + CSRF, key chỉ ở server, lọc danh sách id, giới hạn tần suất."""
import json

from backend import sinh_hoc_ai
from backend.ai.types import LoiAI, PhanHoi


def ma_loi(r):
    d = r.json()
    return d.get("ma") or (d.get("detail") or {}).get("ma")


class PhienGia:
    lan_cuoi: dict = {}

    def __init__(self, system, **kw):
        PhienGia.lan_cuoi = {"system": system, **kw}

    def gui(self, text="", anh=None):
        PhienGia.lan_cuoi["text"] = text
        return PhanHoi(text="```json\n" + json.dumps({"answer_html": "<p>Máu từ phổi về <b>tâm nhĩ trái</b> qua van hai lá.</p>",
                                                       "action": {"tap_trung": ["tam_nhi_trai"], "co_quan": "tim"}}) + "\n```",
                       nha_cung_cap="gemini", model="gemini-flash-test")


def test_can_dang_nhap_va_csrf(client, dang_nhap, monkeypatch):
    monkeypatch.setattr(sinh_hoc_ai, "PhienHoiThoai", PhienGia)
    assert client.post("/api/sinh-hoc/hoi", json={"cau_hoi": "tim"}).status_code in (401, 403)
    dang_nhap(client)
    assert client.post("/api/sinh-hoc/hoi", json={"cau_hoi": "tim"}).status_code == 403  # thiếu CSRF


def test_tra_loi_qua_may_chu(client, dang_nhap, monkeypatch):
    monkeypatch.setattr(sinh_hoc_ai, "PhienHoiThoai", PhienGia)
    h = {"X-CSRF-Token": dang_nhap(client)}
    r = client.post("/api/sinh-hoc/hoi", headers=h, json={
        "cau_hoi": "Máu từ phổi về tim đi vào ngăn nào?",
        "lich_su": [{"vai": "nguoi", "chu": "xin chào"}, {"vai": "ai", "chu": "Chào bạn"}],
        "bo_phan": [{"id": "tam_nhi_trai", "vi": "Tâm nhĩ trái"}, {"id": "BAD id;drop", "vi": "x"}, {"id": "van_hai_la", "vi": "Van\nhai lá"}],
        "duong": ["oxy", "phoi_ve_tim", "<script>"], "vi_mo": ["pho_nang"], "co_quan": ["tim", "nao"]})
    assert r.status_code == 200
    d = r.json()
    assert d["action"] == {"tap_trung": ["tam_nhi_trai"], "co_quan": "tim"} and "tâm nhĩ trái" in d["answer_html"] and d["ai"] == "gemini"
    sys = PhienGia.lan_cuoi["system"]
    assert "tam_nhi_trai (Tâm nhĩ trái)" in sys and "van_hai_la (Van hai lá)" in sys        # tên đã bỏ ký tự điều khiển
    assert "BAD" not in sys and "<script>" not in sys and "phoi_ve_tim" in sys and "co_quan hợp lệ: tim | nao" in sys
    assert "Học sinh: xin chào" in PhienGia.lan_cuoi["text"] and "Bio-Tutor: Chào bạn" in PhienGia.lan_cuoi["text"]


def test_loi_ai_tra_502(client, dang_nhap, monkeypatch):
    class PhienLoi(PhienGia):
        def gui(self, text="", anh=None):
            raise LoiAI("Gemini đang quá tải")

    monkeypatch.setattr(sinh_hoc_ai, "PhienHoiThoai", PhienLoi)
    h = {"X-CSRF-Token": dang_nhap(client)}
    r = client.post("/api/sinh-hoc/hoi", headers=h, json={"cau_hoi": "phổi"})
    assert r.status_code == 502 and ma_loi(r) == "loi_ai"


def test_khong_co_key_may_chu(client, dang_nhap, monkeypatch):
    from backend.config import settings
    for k in ("api_key", "gemini_api_key", "openai_api_key"):
        monkeypatch.setattr(settings, k, "")
    monkeypatch.setattr(sinh_hoc_ai, "PhienHoiThoai", PhienGia)
    h = {"X-CSRF-Token": dang_nhap(client)}
    r = client.post("/api/sinh-hoc/hoi", headers=h, json={"cau_hoi": "phổi"})
    assert r.status_code == 503 and ma_loi(r) == "khong_co_ai"


def test_gioi_han_tan_suat(client, dang_nhap, monkeypatch):
    monkeypatch.setattr(sinh_hoc_ai, "PhienHoiThoai", PhienGia)
    h = {"X-CSRF-Token": dang_nhap(client)}
    ma = [client.post("/api/sinh-hoc/hoi", headers=h, json={"cau_hoi": f"câu {i}"}).status_code for i in range(8)]
    assert ma[:6] == [200] * 6 and ma[6] == 429
