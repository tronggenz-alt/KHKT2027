"""Theo dõi chi phí AI: mỗi lần gọi AI trong một yêu cầu (llm.PhienHoiThoai có kế hoạch) ghi một dòng vào bảng ai_usage –
người dùng, loại yêu cầu, bước, mức độ đề, bậc + mô hình đã trả lời, token vào/ra (lấy từ phản hồi API), có nâng cấp không,
thành công hay lỗi. Thống kê cho trang /admin (mục "Chi phí") và `admin_cli.py usage`.

Tiền chỉ là ƯỚC TÍNH theo bảng giá trả phí (10/2026); gọi bằng key Gemini riêng của người dùng không tính vào tiền của chủ app,
gói miễn phí của Google thì thực tế không mất tiền.
"""
from __future__ import annotations

import logging
import time

from . import db

log = logging.getLogger(__name__)

# (chuỗi có trong tên mô hình, USD / 1 triệu token vào, USD / 1 triệu token ra) – bảng giá trả phí 10/2026, chỉ để ước tính.
# Bí danh *-latest không cho biết bản cụ thể → dùng giá bản mới nhất cùng dòng (Pro: 3.1 Pro; Flash: 3.6/3.8 Flash; Lite: 3.1 Flash-Lite).
BANG_GIA = (
    ("gpt-6.1-sol", 2.0, 10.0), ("gpt", 2.0, 10.0),  # ChatGPT: giá gpt-6.1-sol theo trang theo dõi công khai (10/2026) – ước tính
    ("claude-opus", 4.0, 20.0), ("claude-sonnet", 2.0, 10.0), ("claude-haiku", 0.10, 0.50), ("claude", 2.0, 10.0),
    ("flash-lite", 0.25, 1.50), ("flash", 0.75, 3.75), ("pro", 2.0, 12.0),
)
TEN_MUC = {"de": "Dễ", "trung_binh": "Trung bình", "hsg": "HSG", "": "chưa phân loại"}


def gia(model: str) -> tuple[float, float]:
    m = (model or "").lower()
    for mau, vao, ra in BANG_GIA:
        if mau in m:
            return vao, ra
    return 0.0, 0.0


def uoc_tinh_usd(model: str, tokens_in: int, tokens_out: int) -> float:
    vao, ra = gia(model)
    return (tokens_in * vao + tokens_out * ra) / 1_000_000


def ghi(kh, *, nha_cung_cap: str, model: str, bac: str, nguon_key: str, tokens_in: int = 0, tokens_out: int = 0,
        tokens_think: int = 0, loi: Exception | None = None) -> None:
    ma = getattr(kh.claude, "ma_id", None) if nha_cung_cap == "claude" and kh.claude is not None else None
    with db.ket_noi() as con:
        con.execute(
            "INSERT INTO ai_usage (ts, user_id, job_id, kind, step, level, tier, provider, model, key_source, tokens_in, tokens_out, "
            "tokens_think, upgraded, ok, error, code_id) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
            (time.time(), kh.user_id, kh.job_id, kh.loai, kh.buoc, kh.muc_do, bac, nha_cung_cap, model or "", nguon_key,
             int(tokens_in), int(tokens_out), int(tokens_think), int(kh.nang_cap), int(loi is None),
             (getattr(loi, "loai", "") or type(loi).__name__ if loi else "")[:40], ma))


TEN_MUC_TM = {"nhanh": "Nhanh", "can_bang": "Cân bằng", "ky": "Kỹ", "toi_da": "Tối đa"}
THU_TU_TM = {"": 0, "nhanh": 1, "can_bang": 2, "ky": 3, "toi_da": 4}


def tong_job(job_id: str) -> dict:
    """Tổng lượt gọi + token AI của một bài (để ghi kèm thời gian giải, so sánh trước/sau)."""
    with db.ket_noi() as con:
        r = con.execute("SELECT COUNT(*), COALESCE(SUM(tokens_in), 0), COALESCE(SUM(tokens_out), 0) FROM ai_usage WHERE job_id = ?",
                        (job_id,)).fetchone()
    return {"so_lan": r[0], "vao": int(r[1]), "ra": int(r[2])}


def _tu(so_ngay: int) -> float:
    """Mốc bắt đầu: 1 = từ 0 giờ hôm nay; 7/30 = từ 0 giờ của 6/29 ngày trước."""
    t = time.localtime()
    dau_ngay = time.mktime((t.tm_year, t.tm_mon, t.tm_mday, 0, 0, 0, 0, 0, -1))
    return dau_ngay - (max(1, so_ngay) - 1) * 86400


def thong_ke(so_ngay: int = 1) -> dict:
    """Số lượt + token theo từng mô hình, theo bậc, ai dùng Claude nhiều nhất, tỉ lệ đề Dễ / Trung bình / HSG."""
    tu = _tu(so_ngay)
    with db.ket_noi() as con:
        theo_mo_hinh = [dict(r) for r in con.execute(
            "SELECT provider, model, COUNT(*) AS so_lan, SUM(ok) AS thanh_cong, COUNT(*) - SUM(ok) AS loi, "
            "SUM(tokens_in) AS tokens_in, SUM(tokens_out) AS tokens_out, SUM(tokens_think) AS tokens_think, SUM(upgraded) AS nang_cap, "
            "SUM(CASE WHEN key_source IN ('ca_nhan', 'claude_rieng') THEN 1 ELSE 0 END) AS key_rieng, "
            "SUM(CASE WHEN key_source IN ('ca_nhan', 'claude_rieng') THEN 0 ELSE tokens_in END) AS tin_chu_app, "
            "SUM(CASE WHEN key_source IN ('ca_nhan', 'claude_rieng') THEN 0 ELSE tokens_out END) AS tout_chu_app "
            "FROM ai_usage WHERE ts >= ? GROUP BY provider, model ORDER BY so_lan DESC", (tu,))]
        theo_bac = {r["tier"]: dict(r) for r in con.execute(
            "SELECT tier, COUNT(*) AS so_lan, SUM(tokens_in) AS tokens_in, SUM(tokens_out) AS tokens_out FROM ai_usage "
            "WHERE ts >= ? GROUP BY tier", (tu,))}
        claude = [dict(r) for r in con.execute(
            "SELECT a.user_id, u.name AS ten, u.email, COUNT(*) AS so_lan, SUM(a.ok) AS thanh_cong, SUM(a.tokens_in) AS tokens_in, "
            "SUM(a.tokens_out) AS tokens_out, COUNT(DISTINCT CASE WHEN a.ok = 1 AND a.job_id <> '' THEN a.job_id END) AS so_bai "
            "FROM ai_usage a LEFT JOIN users u ON u.id = a.user_id WHERE a.ts >= ? AND a.provider = 'claude' "
            "GROUP BY a.user_id ORDER BY so_lan DESC LIMIT 10", (tu,))]
        tu_chu = time.strftime("%Y-%m-%d %H:%M:%S", time.localtime(tu))
        muc_do = {r["level"]: r["n"] for r in con.execute(
            "SELECT level, COUNT(*) AS n FROM solutions WHERE created_at >= ? AND demo = 0 GROUP BY level", (tu_chu,))}
        bai_claude = con.execute("SELECT COUNT(*) FROM solutions WHERE created_at >= ? AND claude = 1", (tu_chu,)).fetchone()[0]
        tl = con.execute("SELECT COUNT(*), AVG(lit_papers), COALESCE(SUM(lit_tokens), 0) FROM solutions WHERE created_at >= ? AND lit_papers >= 0",
                         (tu_chu,)).fetchone()
        # thời gian + token mỗi bài theo mức độ thông minh ("" = luồng cũ nhiều bước) – so sánh trước / sau
        theo_muc_tm = [dict(r) for r in con.execute(
            "SELECT s.smart_level AS muc, COUNT(*) AS so_bai, SUM(s.cached) AS tu_cache, "
            "AVG(COALESCE(s.solve_seconds, (julianday(s.finished_at) - julianday(s.created_at)) * 86400)) AS tb_giay, "
            "AVG(COALESCE(a.tok, 0)) AS tb_token, AVG(COALESCE(a.lan, 0)) AS tb_lan_goi FROM solutions s "
            "LEFT JOIN (SELECT job_id, SUM(tokens_in + tokens_out) AS tok, COUNT(*) AS lan FROM ai_usage WHERE job_id <> '' GROUP BY job_id) a "
            "ON a.job_id = s.job_id WHERE s.created_at >= ? AND s.demo = 0 AND s.status = 'xong' GROUP BY s.smart_level", (tu_chu,))]
    tong_usd = 0.0
    for r in theo_mo_hinh:
        for k in ("thanh_cong", "loi", "tokens_in", "tokens_out", "tokens_think", "nang_cap", "key_rieng", "tin_chu_app", "tout_chu_app"):
            r[k] = int(r[k] or 0)
        r["usd"] = round(uoc_tinh_usd(r["model"], r["tin_chu_app"], r["tout_chu_app"]), 4)
        tong_usd += r["usd"]
    for r in claude:
        r["usd"] = round(uoc_tinh_usd("claude-sonnet", int(r["tokens_in"] or 0), int(r["tokens_out"] or 0)), 4)
    tong_bai = sum(v for k, v in muc_do.items() if k in TEN_MUC and k)
    ti_le = [{"muc_do": k, "ten": TEN_MUC[k], "so_bai": muc_do.get(k, 0),
              "phan_tram": round(100 * muc_do.get(k, 0) / tong_bai) if tong_bai else 0} for k in ("de", "trung_binh", "hsg")]
    return {
        "so_ngay": so_ngay, "tu": tu_chu,
        "theo_mo_hinh": theo_mo_hinh,
        "theo_bac": theo_bac,
        "claude_nhieu_nhat": claude,
        "ti_le_muc_do": ti_le, "bai_chua_phan_loai": muc_do.get("", 0), "bai_claude": bai_claude,
        "tra_cuu_tai_lieu": {"so_bai_giai": tl[0], "tb_so_bai_bao": round(tl[1] or 0, 1), "tong_token": int(tl[2])},
        "theo_muc_thong_minh": sorted(({**r, "ten": TEN_MUC_TM.get(r["muc"], "Luồng cũ (trước khi gộp)"),
                                        "tb_giay": round(r["tb_giay"] or 0, 1), "tb_token": round(r["tb_token"] or 0),
                                        "tb_lan_goi": round(r["tb_lan_goi"] or 0, 1), "tu_cache": int(r["tu_cache"] or 0)} for r in theo_muc_tm),
                                      key=lambda r: THU_TU_TM.get(r["muc"], -1)),
        "tong": {"so_lan": sum(r["so_lan"] for r in theo_mo_hinh), "tokens_in": sum(r["tokens_in"] for r in theo_mo_hinh),
                 "tokens_out": sum(r["tokens_out"] for r in theo_mo_hinh), "usd": round(tong_usd, 4)},
        "ghi_chu": "Tiền là ước tính theo bảng giá trả phí 10/2026 (chỉ tính lượt dùng key của chủ app); gói miễn phí của Google thì không mất tiền.",
    }
