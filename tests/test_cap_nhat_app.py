"""Cập nhật app máy tính CHEMISTRY: endpoint /api/app/phien-ban (công khai), bộ kiểm tra bản phát hành, công cụ tools/phat_hanh_app.py."""
import hashlib
import json

import pytest
from starlette.testclient import TestClient

from backend import cap_nhat_app
from backend.cap_nhat_app import doc_ban_phat_hanh, host_hop_le, kiem_tra_ban_phat_hanh

SHA = "ab" * 32
HOP_LE = {
    "phien_ban": "1.2.0", "ngay": "2026-10-25", "ghi_chu": ["Sửa lỗi"], "bat_buoc_tu": None,
    "tai_ve": {"mac-arm64": {"url": "https://github.com/a/b/releases/download/v1.2.0/CHEMISTRY-mac.dmg", "sha256": SHA, "kich_thuoc": 123456}},
}


def _sua(**kv):
    d = json.loads(json.dumps(HOP_LE))
    d.update(kv)
    return d


def test_ban_phat_hanh_hop_le_va_chuan_hoa_sha_chu_thuong():
    d = _sua()
    d["tai_ve"]["mac-arm64"]["sha256"] = SHA.upper()
    ban, loi = kiem_tra_ban_phat_hanh(d)
    assert ban and not loi and ban["tai_ve"]["mac-arm64"]["sha256"] == SHA


@pytest.mark.parametrize("sai", [
    _sua(phien_ban="1.2"),
    _sua(phien_ban="v1.2.0"),
    _sua(bat_buoc_tu="x"),
    _sua(ghi_chu="không phải danh sách"),
    _sua(tai_ve={"linux-x64": HOP_LE["tai_ve"]["mac-arm64"]}),
    _sua(tai_ve={"mac-arm64": {**HOP_LE["tai_ve"]["mac-arm64"], "url": "http://github.com/a.dmg"}}),
    _sua(tai_ve={"mac-arm64": {**HOP_LE["tai_ve"]["mac-arm64"], "url": "https://evil.example.com/a.dmg"}}),
    _sua(tai_ve={"mac-arm64": {**HOP_LE["tai_ve"]["mac-arm64"], "url": "https://github.com.evil.com/a.dmg"}}),
    _sua(tai_ve={"mac-arm64": {**HOP_LE["tai_ve"]["mac-arm64"], "sha256": "123"}}),
    _sua(tai_ve={"mac-arm64": {**HOP_LE["tai_ve"]["mac-arm64"], "kich_thuoc": 0}}),
    _sua(tai_ve={"mac-arm64": {**HOP_LE["tai_ve"]["mac-arm64"], "kich_thuoc": True}}),
    [], "chuỗi", None,
])
def test_ban_phat_hanh_sai_bi_tu_choi(sai):
    ban, loi = kiem_tra_ban_phat_hanh(sai)
    assert ban is None and loi


def test_host_hop_le():
    assert host_hop_le("github.com") and host_hop_le("objects.githubusercontent.com") and host_hop_le("giai-chuoi-huu-co.onrender.com")
    assert not host_hop_le("evilgithub.com") and not host_hop_le("github.com.evil.com") and not host_hop_le("")


def test_tep_phat_hanh_that_trong_repo_hop_le():
    ban, loi = doc_ban_phat_hanh()
    assert ban is not None, loi


def test_endpoint_cong_khai_khong_can_dang_nhap(client: TestClient):
    r = client.get("/api/app/phien-ban")
    assert r.status_code == 200
    assert "no-store" in r.headers["cache-control"]
    assert r.json()["phien_ban"]


def test_endpoint_tra_noi_dung_tep_khi_hop_le(client: TestClient, tmp_path, monkeypatch):
    tep = tmp_path / "pb.json"
    tep.write_text(json.dumps(HOP_LE), encoding="utf-8")
    monkeypatch.setattr(cap_nhat_app, "TEP_PHAT_HANH", tep)
    assert client.get("/api/app/phien-ban").json() == {**HOP_LE, "tai_ve": {"mac-arm64": {**HOP_LE["tai_ve"]["mac-arm64"]}}}


@pytest.mark.parametrize("noi_dung", [None, "không phải json", json.dumps({"phien_ban": "x"})])
def test_endpoint_im_lang_khi_thieu_hoac_sai(client: TestClient, tmp_path, monkeypatch, noi_dung):
    tep = tmp_path / "pb.json"
    if noi_dung is not None:
        tep.write_text(noi_dung, encoding="utf-8")
    monkeypatch.setattr(cap_nhat_app, "TEP_PHAT_HANH", tep)
    r = client.get("/api/app/phien-ban")
    assert r.status_code == 200 and r.json()["phien_ban"] is None and r.json()["loi"]


def test_cong_cu_phat_hanh_tinh_sha256_va_kich_thuoc_that(tmp_path):
    from tools import phat_hanh_app as pha
    dmg = tmp_path / "CHEMISTRY-mac.dmg"
    dmg.write_bytes(b"noi dung bo cai" * 1000)
    url = "https://github.com/a/b/releases/download/v1.0.1/CHEMISTRY-mac.dmg"
    ra = tmp_path / "ra.json"
    pha.main(["--phien-ban", "1.0.1", "--mac-arm64", str(dmg), "--url-mac-arm64", url, "--ghi-chu", "Sửa lỗi A", "--bat-buoc-tu", "1.0.0", "--ra", str(ra)])
    d = json.loads(ra.read_text(encoding="utf-8"))
    t = d["tai_ve"]["mac-arm64"]
    assert d["phien_ban"] == "1.0.1" and d["bat_buoc_tu"] == "1.0.0" and d["ghi_chu"] == ["Sửa lỗi A"]
    assert t["sha256"] == hashlib.sha256(dmg.read_bytes()).hexdigest() and t["kich_thuoc"] == dmg.stat().st_size and t["url"] == url
    assert kiem_tra_ban_phat_hanh(d)[0] is not None


def test_cong_cu_phat_hanh_tu_choi_url_la_va_thieu_cap(tmp_path):
    from tools import phat_hanh_app as pha
    dmg = tmp_path / "a.dmg"
    dmg.write_bytes(b"x")
    ra = tmp_path / "ra.json"
    with pytest.raises(SystemExit):
        pha.main(["--phien-ban", "1.0.1", "--mac-arm64", str(dmg), "--url-mac-arm64", "https://evil.example.com/a.dmg", "--ra", str(ra)])
    with pytest.raises(SystemExit):
        pha.main(["--phien-ban", "1.0.1", "--mac-arm64", str(dmg), "--ra", str(ra)])   # thiếu --url
    assert not ra.exists()
