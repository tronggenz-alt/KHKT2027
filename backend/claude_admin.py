"""Admin: theo dõi TIỀN + TOKEN của Claude API máy chủ qua Admin API của Anthropic (ANTHROPIC_ADMIN_KEY = sk-ant-admin01-…).

- GET /v1/organizations/usage_report/messages (token theo mô hình, theo ngày) + /v1/organizations/cost_report (chi phí USD theo ngày;
  `amount` là chuỗi số thập phân tính bằng CENT). Ngày theo UTC (giờ của Anthropic).
- Anthropic không có API trả số dư → admin tự ghi các lần nạp tiền (bảng claude_nap); số dư ƯỚC TÍNH = tổng đã nạp − tổng chi phí
  (cost_report) kể từ ngày nạp đầu tiên. Cảnh báo đỏ khi dưới ngưỡng admin đặt (app_settings "claude_canh_bao_usd", mặc định 2 USD).
- Kết quả Admin API lưu cache trong bộ nhớ 10 phút (nút "Làm mới" bỏ qua cache). Không có admin key → chỉ số liệu nhật ký nội bộ (ai_usage).
"""
from __future__ import annotations

import logging
import threading
import time
from datetime import datetime, timedelta, timezone

import requests

from . import chi_phi_ai, db
from .config import settings

log = logging.getLogger(__name__)

API = "https://api.anthropic.com/v1/organizations"
CACHE_GIAY = 600
SO_NGAY = 30
NGUONG_MAC_DINH = 2.0
HUONG_DAN = [
    "Admin API chỉ có ở tài khoản TỔ CHỨC (Organization) trên platform.claude.com – tài khoản cá nhân cần tạo tổ chức trước.",
    "platform.claude.com → Settings → Admin keys → Create admin key (chỉ admin của tổ chức tạo được) → chép key dạng sk-ant-admin01-…",
    "Nháy đúp cau-hinh-claude.command (thư mục Antigravity) → dán Admin key (không hiện trên màn hình) – script tự nhận ra loại key, ghi .env + khởi động lại app.",
    "Admin key KHÁC ANTHROPIC_API_KEY (key gọi Claude) – chỉ dùng để đọc báo cáo chi phí, không dùng để giải bài.",
]
_cache: dict[str, tuple[float, dict]] = {}
_khoa_cache = threading.Lock()


class LoiAdminAPI(Exception):
    pass


def co_admin_key() -> bool:
    return bool(settings.anthropic_admin_key)


# ---------------------------------------------------------------- nạp tiền + ngưỡng cảnh báo (CSDL)
def nguong_canh_bao() -> float:
    try:
        return float(db.cai_dat("claude_canh_bao_usd", str(NGUONG_MAC_DINH)))
    except ValueError:
        return NGUONG_MAC_DINH


def dat_nguong(usd: float) -> float:
    if not 0 <= usd <= 100000:
        raise ValueError("Ngưỡng cảnh báo phải từ 0 đến 100000 USD.")
    db.dat_cai_dat("claude_canh_bao_usd", f"{usd:g}")
    return usd


def them_nap(usd: float, ghi_chu: str = "", admin_id: int | None = None) -> int:
    if not 0 < usd <= 100000:
        raise ValueError("Số tiền nạp phải lớn hơn 0 và không quá 100000 USD.")
    with db.ket_noi() as con:
        return con.execute("INSERT INTO claude_nap (ts, usd, ghi_chu, admin_id) VALUES (?, ?, ?, ?)",
                           (time.time(), round(usd, 2), (ghi_chu or "").strip()[:200], admin_id)).lastrowid


def xoa_nap(nap_id: int) -> bool:
    with db.ket_noi() as con:
        return con.execute("DELETE FROM claude_nap WHERE id = ?", (nap_id,)).rowcount == 1


def lich_su_nap() -> list[dict]:
    with db.ket_noi() as con:
        rows = con.execute("SELECT n.*, u.email FROM claude_nap n LEFT JOIN users u ON u.id = n.admin_id ORDER BY n.ts DESC").fetchall()
    return [{"id": r["id"], "usd": r["usd"], "ghi_chu": r["ghi_chu"], "luc": time.strftime("%Y-%m-%d %H:%M", time.localtime(r["ts"])),
             "ngay_utc": datetime.fromtimestamp(r["ts"], timezone.utc).date().isoformat(), "admin": r["email"] or "(dòng lệnh)"} for r in rows]


# ---------------------------------------------------------------- Admin API (phân trang, cache 10 phút)
def _goi(duong: str, params: dict) -> list[dict]:
    du_lieu: list[dict] = []
    trang = None
    for _ in range(40):
        p = dict(params, **({"page": trang} if trang else {}))
        try:
            r = requests.get(f"{API}/{duong}", params=p, timeout=25,
                             headers={"x-api-key": settings.anthropic_admin_key, "anthropic-version": "2023-06-01"})
        except requests.RequestException as e:
            raise LoiAdminAPI(f"Không kết nối được Admin API của Anthropic ({type(e).__name__}).") from e
        if r.status_code in (401, 403):
            raise LoiAdminAPI("ANTHROPIC_ADMIN_KEY không hợp lệ hoặc không có quyền (cần Admin key sk-ant-admin01-… của tổ chức).")
        if r.status_code >= 400:
            try:
                tin = r.json().get("error", {}).get("message", "")
            except ValueError:
                tin = ""
            raise LoiAdminAPI(f"Admin API báo lỗi {r.status_code}{': ' + tin[:200] if tin else ''}.")
        j = r.json()
        du_lieu += j.get("data") or []
        if not j.get("has_more") or not j.get("next_page"):
            break
        trang = j["next_page"]
    return du_lieu


def _rfc(d: datetime) -> str:
    return d.strftime("%Y-%m-%dT00:00:00Z")


def _lay_api(tu_ngay: datetime) -> dict:
    """Gọi Admin API: chi phí theo ngày từ `tu_ngay` (UTC) + token theo mô hình 30 ngày."""
    den = datetime.now(timezone.utc) + timedelta(days=1)
    chi_phi = _goi("cost_report", {"starting_at": _rfc(tu_ngay), "ending_at": _rfc(den), "bucket_width": "1d", "limit": 31})
    tu_30 = datetime.now(timezone.utc) - timedelta(days=SO_NGAY - 1)
    dung = _goi("usage_report/messages", {"starting_at": _rfc(tu_30), "ending_at": _rfc(den), "bucket_width": "1d", "limit": 31,
                                          "group_by[]": ["model"]})
    theo_ngay: dict[str, float] = {}
    for b in chi_phi:
        ngay = (b.get("starting_at") or "")[:10]
        theo_ngay[ngay] = theo_ngay.get(ngay, 0.0) + sum(float(x.get("amount") or 0) for x in b.get("results") or []) / 100  # cent → USD
    mo_hinh: dict[str, dict] = {}
    for b in dung:
        for x in b.get("results") or []:
            m = mo_hinh.setdefault(x.get("model") or "(không rõ)", {"model": x.get("model") or "(không rõ)", "tokens_in": 0, "tokens_out": 0,
                                                                  "cache_doc": 0, "cache_ghi": 0})
            cc = x.get("cache_creation") or {}
            m["tokens_in"] += int(x.get("uncached_input_tokens") or 0)
            m["cache_ghi"] += int(cc.get("ephemeral_1h_input_tokens") or 0) + int(cc.get("ephemeral_5m_input_tokens") or 0)
            m["cache_doc"] += int(x.get("cache_read_input_tokens") or 0)
            m["tokens_out"] += int(x.get("output_tokens") or 0)
    return {"theo_ngay": theo_ngay, "mo_hinh": sorted(mo_hinh.values(), key=lambda m: -(m["tokens_in"] + m["tokens_out"]))}


def _noi_bo() -> dict:
    """Nhật ký chi phí của app (ai_usage) cho Claude bằng key MÁY CHỦ, 30 ngày – để so với số liệu của Anthropic."""
    tu = time.time() - SO_NGAY * 86400
    with db.ket_noi() as con:
        rows = [dict(r) for r in con.execute(
            "SELECT model, COUNT(*) AS so_lan, SUM(1 - ok) AS loi, SUM(tokens_in) AS tokens_in, SUM(tokens_out) AS tokens_out "
            "FROM ai_usage WHERE provider = 'claude' AND key_source <> 'claude_rieng' AND ts >= ? GROUP BY model ORDER BY so_lan DESC", (tu,))]
    for r in rows:
        r["usd"] = round(chi_phi_ai.uoc_tinh_usd(r["model"] or "claude", int(r["tokens_in"] or 0), int(r["tokens_out"] or 0)), 4)
    return {"theo_mo_hinh": rows, "tong_usd": round(sum(r["usd"] for r in rows), 4),
            "tokens_in": sum(int(r["tokens_in"] or 0) for r in rows), "tokens_out": sum(int(r["tokens_out"] or 0) for r in rows)}


def bao_cao(lam_moi: bool = False) -> dict:
    """Số liệu cho /admin → Claude API và `admin_cli.py claude`."""
    nap = lich_su_nap()
    tong_nap = round(sum(n["usd"] for n in nap), 2)
    kq = {"co_admin_key": co_admin_key(), "nap": nap, "tong_nap": tong_nap, "nguong": nguong_canh_bao(), "noi_bo": _noi_bo(),
          "huong_dan": [] if co_admin_key() else HUONG_DAN}
    if not co_admin_key():
        return kq
    hom_nay = datetime.now(timezone.utc).replace(hour=0, minute=0, second=0, microsecond=0)
    tu_ngay = min([hom_nay - timedelta(days=SO_NGAY - 1)] + [datetime.fromisoformat(n["ngay_utc"]).replace(tzinfo=timezone.utc) for n in nap])
    tu_ngay = max(tu_ngay, hom_nay - timedelta(days=730))
    khoa = tu_ngay.date().isoformat()
    with _khoa_cache:
        luc, api = _cache.get(khoa, (0.0, None))
        if lam_moi or api is None or time.time() - luc > CACHE_GIAY:
            try:
                api, luc = _lay_api(tu_ngay), time.time()
                _cache.clear()
                _cache[khoa] = (luc, api)
            except LoiAdminAPI as e:
                log.warning("Admin API Anthropic: %s", e)
                return {**kq, "loi": str(e)}
    ngay = api["theo_ngay"]
    ngay_30 = [(hom_nay - timedelta(days=i)).date().isoformat() for i in range(SO_NGAY - 1, -1, -1)]
    tong = lambda so: round(sum(ngay.get(d, 0.0) for d in ngay_30[-so:]), 4)  # noqa: E731
    da_dung = round(sum(v for d, v in ngay.items() if not nap or d >= min(n["ngay_utc"] for n in nap)), 4)
    so_du = round(tong_nap - da_dung, 2) if nap else None
    return {**kq, "cap_nhat_luc": time.strftime("%H:%M:%S", time.localtime(luc)), "cache_con_giay": max(0, int(CACHE_GIAY - (time.time() - luc))),
            "hom_nay": tong(1), "ngay_7": tong(7), "ngay_30": tong(30), "da_dung_tu_lan_nap_dau": da_dung, "so_du_uoc_tinh": so_du,
            "canh_bao": so_du is not None and so_du < nguong_canh_bao(),
            "bieu_do": [{"ngay": d, "usd": round(ngay.get(d, 0.0), 4)} for d in ngay_30], "mo_hinh": api["mo_hinh"]}
