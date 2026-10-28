"""/admin → Claude API máy chủ: Admin API Anthropic (giả lập requests.get), số dư ước tính, cache 10 phút, chỉ admin, CLI."""
from datetime import datetime, timedelta, timezone

import pytest

from backend import claude_admin, db
from backend.config import settings
from test_chon_mo_hinh import _dang_nhap_email


def _ngay(i: int) -> str:  # i ngày trước (UTC)
    return (datetime.now(timezone.utc) - timedelta(days=i)).strftime("%Y-%m-%dT00:00:00Z")


class AdminAPIGia:
    def __init__(self, monkeypatch):
        self.goi: list[tuple[str, dict]] = []
        self.ma = 200
        monkeypatch.setattr(claude_admin.requests, "get", self.get)

    def get(self, url, params=None, timeout=None, headers=None):
        assert headers["x-api-key"] == settings.anthropic_admin_key and headers["anthropic-version"] == "2023-06-01"
        self.goi.append((url.rsplit("organizations/", 1)[1], dict(params)))
        if self.ma != 200:
            return _Tl(self.ma, {"error": {"message": "invalid x-api-key"}})
        if url.endswith("cost_report"):
            if params.get("page") is None:  # trang 1: hôm qua 150 cent; còn trang 2
                return _Tl(200, {"data": [{"starting_at": _ngay(1), "results": [{"amount": "100.0", "currency": "USD"}, {"amount": "50", "currency": "USD"}]},
                                          {"starting_at": _ngay(10), "results": [{"amount": "300", "currency": "USD"}]}],
                                 "has_more": True, "next_page": "p2"})
            return _Tl(200, {"data": [{"starting_at": _ngay(0), "results": [{"amount": "25.5", "currency": "USD"}]}], "has_more": False, "next_page": None})
        return _Tl(200, {"data": [{"starting_at": _ngay(0), "results": [
            {"model": "claude-sonnet-5-5", "uncached_input_tokens": 1000, "output_tokens": 300, "cache_read_input_tokens": 50,
             "cache_creation": {"ephemeral_5m_input_tokens": 20, "ephemeral_1h_input_tokens": 0}}]},
            {"starting_at": _ngay(2), "results": [{"model": "claude-sonnet-5-5", "uncached_input_tokens": 500, "output_tokens": 100,
                                                    "cache_read_input_tokens": 0, "cache_creation": {}}]}], "has_more": False})


class _Tl:
    def __init__(self, ma, j):
        self.status_code, self._j = ma, j

    def json(self):
        return self._j


@pytest.fixture
def api(monkeypatch, cau_hinh):
    monkeypatch.setattr(claude_admin, "_cache", {})
    monkeypatch.setattr(settings, "anthropic_admin_key", "sk-ant-admin01-thu")
    return AdminAPIGia(monkeypatch)


def test_chua_co_admin_key_chi_noi_bo(cau_hinh, monkeypatch):
    monkeypatch.setattr(settings, "anthropic_admin_key", "")
    monkeypatch.setattr(claude_admin.requests, "get", lambda *a, **k: pytest.fail("không được gọi Admin API"))
    d = claude_admin.bao_cao()
    assert d["co_admin_key"] is False and len(d["huong_dan"]) == 4 and "noi_bo" in d and "so_du_uoc_tinh" not in d


def test_chi_phi_so_du_canh_bao_cache(api):
    claude_admin.them_nap(5, "thẻ Visa")
    d = claude_admin.bao_cao()
    # cent → USD: hôm nay 0.255; hôm qua 1.5; 10 ngày trước 3.0
    assert (d["hom_nay"], d["ngay_7"], d["ngay_30"]) == (0.255, 1.755, 4.755)
    assert d["da_dung_tu_lan_nap_dau"] == 0.255 and d["so_du_uoc_tinh"] == 4.75 and d["canh_bao"] is False  # nạp hôm nay → chỉ tính từ hôm nay
    assert d["mo_hinh"] == [{"model": "claude-sonnet-5-5", "tokens_in": 1500, "tokens_out": 400, "cache_doc": 50, "cache_ghi": 20}]
    assert len(d["bieu_do"]) == 30 and d["bieu_do"][-1]["usd"] == 0.255
    assert [g[0] for g in api.goi] == ["cost_report", "cost_report", "usage_report/messages"] and api.goi[1][1]["page"] == "p2"
    assert api.goi[2][1]["group_by[]"] == ["model"] and api.goi[0][1]["bucket_width"] == "1d"
    claude_admin.bao_cao()
    assert len(api.goi) == 3  # trong 10 phút: dùng cache
    claude_admin.dat_nguong(10)
    d = claude_admin.bao_cao(lam_moi=True)
    assert len(api.goi) == 6 and d["canh_bao"] is True  # 4.75 < 10 → cảnh báo đỏ
    with db.ket_noi() as con:
        con.execute("UPDATE claude_nap SET ts = ts - 20 * 86400")  # nạp từ 20 ngày trước → tính cả 4.755
    d = claude_admin.bao_cao(lam_moi=True)
    assert d["da_dung_tu_lan_nap_dau"] == 4.755 and d["so_du_uoc_tinh"] == pytest.approx(0.245, abs=0.006)


def test_admin_key_sai_bao_loi(api):
    api.ma = 401
    d = claude_admin.bao_cao()
    assert "ANTHROPIC_ADMIN_KEY không hợp lệ" in d["loi"] and "noi_bo" in d


def test_api_chi_admin_nap_xoa_nguong(api, client, monkeypatch):
    hs = _dang_nhap_email(monkeypatch, client, "hs@example.com")
    assert hs.get("/admin/api/claude").status_code == 403
    assert hs.post("/admin/api/claude/nap", json={"usd": 5}, headers=hs.h).status_code == 403
    assert hs.post("/admin/api/claude/canh-bao", json={"usd": 1}, headers=hs.h).status_code == 403
    ad = _dang_nhap_email(monkeypatch, client, "admin@example.com", admin=True)
    d = ad.post("/admin/api/claude/nap", json={"usd": 20, "ghi_chu": "lần 1"}, headers=ad.h).json()
    assert d["tong_nap"] == 20 and d["nap"][0]["admin"] == "admin@example.com" and d["so_du_uoc_tinh"] == 19.75
    assert ad.post("/admin/api/claude/nap", json={"usd": -1}, headers=ad.h).status_code == 422
    assert ad.post("/admin/api/claude/canh-bao", json={"usd": 25}, headers=ad.h).json()["canh_bao"] is True
    d = ad.post(f"/admin/api/claude/nap/{d['nap'][0]['id']}/xoa", json={}, headers=ad.h).json()
    assert d["tong_nap"] == 0 and d["so_du_uoc_tinh"] is None
    assert "sk-ant-admin01-thu" not in ad.get("/admin/api/claude?lam_moi=1").text


def test_admin_cli_claude(api, capsys):
    import admin_cli

    admin_cli.con_sole.width = 200
    with db.ket_noi():
        pass
    assert admin_cli.main(["claude", "--nap", "3", "--ghi-chu", "thử"]) == 0
    ra = capsys.readouterr().out
    assert "Số dư ƯỚC TÍNH" in ra and "$2.75" in ra and "claude-sonnet-5-5" in ra and "sk-ant-admin01" not in ra
