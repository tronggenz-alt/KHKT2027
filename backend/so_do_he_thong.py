"""Sơ đồ hệ thống cho /admin/mindmap.

- Cấu trúc sơ đồ nằm trong config/so_do_he_thong.json (thêm hệ thống mới = sửa file đó). Node mô hình chỉ ghi `model_tu` (tên thuộc
  tính trong config.py) → server điền TÊN MÔ HÌNH THẬT đang cấu hình trong .env, nên sơ đồ luôn khớp với code đang chạy.
- Nhánh AI của mỗi hệ thống (Giải chuỗi / 3D / Cơ chế) dựng THEO THỨ TỰ của router (backend/ai/routing.json + /admin → Thứ tự AI):
  Chính / Dự phòng 1 / Dự phòng 2, nối fallback theo thứ tự – không vẽ tay.
- Số lần gọi hôm nay (+ số lần lỗi) đếm từ bảng ai_usage theo bộ lọc "dem" (kind / step) và đúng mô hình của node.
- Sức khoẻ (nhẹ, cache 5 phút): CHỈ hỏi thông tin mô hình (Anthropic models.retrieve, Gemini models.get – không tốn token),
  cộng thêm: Claude đang tạm dừng sau lỗi tài khoản (llm.py), hoặc lần gọi gần nhất trong 24 giờ bị lỗi (vd hết tín dụng)
  → đỏ. Chỉ xét key của MÁY CHỦ (bỏ qua lượt gọi bằng key riêng của người dùng). Không bao giờ trả key về trình duyệt.
"""
from __future__ import annotations

import copy
import json
import logging
import threading
import time
from concurrent.futures import ThreadPoolExecutor

from . import db, llm
from .ai import NHA_CUNG_CAP
from .ai import router as ai_router
from .config import BASE_DIR, GEMINI_MODEL_DU_PHONG, settings

log = logging.getLogger(__name__)

TEP_CAU_HINH = BASE_DIR / "config" / "so_do_he_thong.json"
CACHE_GIAY = 300
GAN_DAY_GIAY = 24 * 3600   # lần gọi gần nhất trong 24 giờ bị lỗi (vd hết tín dụng – ping không phát hiện được) → đỏ
PING_GIAY = 8.0

_cache: dict = {"luc": 0.0, "kq": None}
VAI_THEO_VI_TRI = ("chinh", "du_phong_1", "du_phong_2")
THIEU_KEY = {"claude": "Máy chủ chưa có ANTHROPIC_API_KEY", "gemini": "Máy chủ chưa có GEMINI_API_KEY",
             "openai": "Chưa có OPENAI_API_KEY hoặc OPENAI_MODEL (nháy đúp cau-hinh-openai.command)"}
_khoa = threading.Lock()


def doc_cau_hinh() -> dict:
    return json.loads(TEP_CAU_HINH.read_text(encoding="utf-8"))


def ten_mo_hinh(model_tu: str) -> list[str]:
    """Tên mô hình THẬT của một node (đọc từ settings / .env)."""
    if model_tu == "gemini_du_phong":
        return list(GEMINI_MODEL_DU_PHONG)
    gia_tri = getattr(settings, model_tu, "")
    return [gia_tri] if gia_tri else []


def dung_nhanh_ai(ht: dict) -> None:
    """Hệ thống có "ai": tạo node nhà cung cấp theo thứ tự router (Chính / Dự phòng 1 / Dự phòng 2 + fallback), con = các mô hình."""
    if "ai" not in ht:
        return
    thu_tu = ai_router.thu_tu(ht["id"])
    dung = [t for t in thu_tu if ht["ai"].get(t, {}).get("con")]  # AI thật sự dùng ở hệ thống này (vd 3D không dùng Claude)
    nhanh = []
    for ten in thu_tu:
        c, i = ht["ai"].get(ten, {}), dung.index(ten) if ten in dung else -1
        nhanh.append({"id": f"{ht['id']}_{ten}", "ai": ten, "nha_cung_cap_node": True, "ten": ai_router.TEN_AI[ten],
                      "vai_tro": VAI_THEO_VI_TRI[min(i, 2)] if i >= 0 else "khong_dung", "dieu_kien": c.get("dieu_kien", ""),
                      "con": c.get("con", []), "fallback": [f"{ht['id']}_{dung[i + 1]}"] if 0 <= i < len(dung) - 1 else []})
    ht["con"] = nhanh + ht.get("khac", [])
    ht.pop("ai", None)
    ht.pop("khac", None)


def ai_khong_dung() -> dict[str, list[str]]:
    """{hệ thống: [AI có trong thứ tự nhưng KHÔNG dùng ở hệ thống đó]} theo config (để /admin → Thứ tự AI ghi "bỏ qua")."""
    return {ht["id"]: [t for t in ai_router.CAC_AI if not ht["ai"].get(t, {}).get("con")]
            for ht in doc_cau_hinh().get("he_thong", []) if "ai" in ht}


def _cac_node(node: dict):
    yield node
    for c in node.get("con", []):
        yield from _cac_node(c)


def _dau_ngay() -> float:
    t = time.localtime()
    return time.mktime((t.tm_year, t.tm_mon, t.tm_mday, 0, 0, 0, 0, 0, -1))


def _khop(r, dem: dict, ai: str = "", models: list[str] | None = None) -> bool:
    if dem.get("kind") and r["kind"] not in dem["kind"]:
        return False
    if dem.get("step") and r["step"] not in dem["step"]:
        return False
    if ai and r["provider"] != ai:
        return False
    return not models or any(r["model"] == m or r["model"].startswith(m + "-") for m in models)


def du_lieu() -> dict:
    """Cấu hình sơ đồ + tên mô hình thật + số lần gọi / lỗi hôm nay cho từng node (và tổng mỗi hệ thống)."""
    cfg = copy.deepcopy(doc_cau_hinh())
    with db.ket_noi() as con:
        dong = con.execute("SELECT kind, step, provider, model, ok, COUNT(*) AS n FROM ai_usage WHERE ts >= ? "
                           "GROUP BY kind, step, provider, model, ok", (_dau_ngay(),)).fetchall()

    def dem(bo_loc: dict, ai: str = "", models: list[str] | None = None) -> tuple[int, int]:
        ds = [r for r in dong if _khop(r, bo_loc, ai, models)]
        return sum(r["n"] for r in ds), sum(r["n"] for r in ds if not r["ok"])

    def xu_ly(node: dict, bo_loc: dict) -> None:
        bo_loc = node.get("dem") or bo_loc
        for c in node.get("con", []):
            xu_ly(c, bo_loc)
        if node.get("nha_cung_cap_node"):  # node nhà cung cấp: mô hình = của các con, lượt gọi = mọi lượt của nhà cung cấp trong hệ thống
            node["models"] = list(dict.fromkeys(m for c in node["con"] for m in c.get("models", [])))
            node["goi_hom_nay"], node["loi_hom_nay"] = dem(bo_loc, node["ai"]) if bo_loc else (0, 0)
        elif node.get("ai"):
            node["models"] = ten_mo_hinh(node.get("model_tu", ""))
            node["goi_hom_nay"], node["loi_hom_nay"] = dem(bo_loc, node["ai"], node["models"]) if bo_loc else (0, 0)
        elif node.get("dem"):
            node["goi_hom_nay"], node["loi_hom_nay"] = dem(bo_loc)

    for ht in cfg["he_thong"]:
        if ht["id"] == "notebooklm":
            try:
                from .services import notebooklm as nlm_service
                nhanh_nlm = nlm_service.lay_nhanh_mindmap()
                ht["con"] = nhanh_nlm.get("con", [])
                ht["danh_sach_tai_khoan"] = nhanh_nlm.get("danh_sach_tai_khoan", [])
                ht["tai_khoan_hien_tai"] = nhanh_nlm.get("tai_khoan_hien_tai")
                ht["status"] = nhanh_nlm.get("status", "ok")
                ht["mo_ta"] = nhanh_nlm.get("mo_ta", ht.get("mo_ta", ""))
                ht["che_do"] = nhanh_nlm.get("che_do", "mot")
            except Exception as e:
                log.warning("Không thể nạp động nhánh NotebookLM: %s", e)
        dung_nhanh_ai(ht)
        xu_ly(ht, {})
        if "goi_hom_nay" not in ht:  # hệ thống không có bộ lọc riêng (Hạ tầng chung): cộng các nhánh con
            ht["goi_hom_nay"] = sum(c.get("goi_hom_nay", 0) for c in ht.get("con", []))
            ht["loi_hom_nay"] = sum(c.get("loi_hom_nay", 0) for c in ht.get("con", []))
    for ten, ncc in cfg["nha_cung_cap"].items():
        ncc["co_key"] = NHA_CUNG_CAP[ten].co_key() if ten in NHA_CUNG_CAP else False
        ncc["thieu"] = "" if ncc["co_key"] else THIEU_KEY.get(ten, "Chưa cấu hình")
    cfg["thu_tu_ai"] = ai_router.tat_ca()
    cfg["tong_goi_hom_nay"] = sum(r["n"] for r in dong)
    cfg["cap_nhat_luc"] = time.strftime("%H:%M:%S")
    return cfg


# ---------------------------------------------------------------- sức khoẻ (health-check nhẹ)
def _ping_claude(model: str) -> None:
    """Kiểm tra mô hình và số dư tín dụng bằng cách gọi thử 1 token. Lỗi → ném exception."""
    import anthropic
    client = anthropic.Anthropic(api_key=settings.api_key, timeout=PING_GIAY, max_retries=0)
    client.models.retrieve(model)
    client.messages.create(
        model=model,
        max_tokens=1,
        messages=[{"role": "user", "content": "1"}],
    )


def _ping_gemini(model: str) -> None:
    from google import genai
    from google.genai import types
    client = genai.Client(api_key=settings.gemini_api_key, http_options=types.HttpOptions(timeout=int(PING_GIAY * 1000)))
    client.models.get(model=model)  # (giữ biến client: client tạm bị thu hồi sẽ tự đóng trước khi gửi)


def _ping_openai(model: str) -> None:
    NHA_CUNG_CAP["openai"].kiem_tra_suc_khoe(model)


def _mo_ta_loi(e: Exception) -> str:
    ten = type(e).__name__
    ma = getattr(e, "status_code", None) or getattr(e, "code", None)
    s = str(e).lower()
    if "credit balance" in s or "too low" in s or "plans & billing" in s:
        return "Tài khoản Anthropic đã hết tín dụng (cần nạp thêm tại platform.claude.com)"
    if ma == 404 or "NotFound" in ten:
        return "Không có mô hình này (kiểm tra tên trong .env)"
    if ma in (401, 403) or "Authentication" in ten or "PermissionDenied" in ten:
        return "Key không hợp lệ hoặc không có quyền"
    if "Connection" in ten or "Timeout" in ten:
        return "Không kết nối được tới máy chủ AI"
    return f"{ten}" + (f" ({ma})" if ma else "")


def _cac_mo_hinh() -> list[tuple[str, str]]:
    cap = set()
    for ht in doc_cau_hinh()["he_thong"]:
        nodes = [n for c in ht.get("ai", {}).values() for x in c.get("con", []) for n in _cac_node(x)] + list(_cac_node(ht))
        for n in nodes:
            if n.get("ai") and n.get("model_tu"):
                cap.update((n["ai"], m) for m in ten_mo_hinh(n.get("model_tu", "")))
    return sorted(cap)


def _kiem_mot(ncc: str, model: str) -> dict:
    if ncc == "claude" and not settings.api_key:
        return {"trang_thai": "tat", "chi_tiet": "Máy chủ chưa có ANTHROPIC_API_KEY"}
    if ncc == "gemini" and not settings.gemini_api_key:
        return {"trang_thai": "tat", "chi_tiet": "Máy chủ chưa có GEMINI_API_KEY"}
    if ncc == "openai" and not settings.openai_api_key:
        return {"trang_thai": "tat", "chi_tiet": "Máy chủ chưa có OPENAI_API_KEY"}
    try:
        {"claude": _ping_claude, "gemini": _ping_gemini, "openai": _ping_openai}[ncc](model)
        return {"trang_thai": "ok", "chi_tiet": "Mô hình sẵn sàng (đã hỏi thông tin mô hình)"}
    except Exception as e:  # noqa: BLE001
        log.warning("Health-check %s %s: %s", ncc, model, type(e).__name__)
        return {"trang_thai": "loi", "chi_tiet": _mo_ta_loi(e)}


def suc_khoe(lam_moi: bool = False) -> dict:
    """{"luc", "mo_hinh": {"claude:<model>": {"trang_thai": ok|loi|tat, "chi_tiet"}}} – cache 5 phút ("Làm mới" bỏ qua cache)."""
    with _khoa:
        if not lam_moi and _cache["kq"] is not None and time.time() - _cache["luc"] < CACHE_GIAY:
            kq = _cache["kq"]
        else:
            cap = _cac_mo_hinh()
            with ThreadPoolExecutor(max_workers=max(1, min(8, len(cap)))) as ex:
                ket = list(ex.map(lambda c: _kiem_mot(*c), cap))
            kq = {"luc": time.strftime("%H:%M:%S"), "mo_hinh": {f"{n}:{m}": k for (n, m), k in zip(cap, ket)}}
            _cache.update(luc=time.time(), kq=kq)
    kq = copy.deepcopy(kq)
    # lỗi thật gần đây (không cache): Claude đang tạm dừng sau lỗi tài khoản, hoặc lần gọi gần nhất trong 24 giờ bị lỗi
    with db.ket_noi() as con:
        gan = con.execute("SELECT provider, model, ok, error, ts FROM ai_usage WHERE ts >= ? AND key_source NOT IN ('claude_rieng', 'ca_nhan') "
                          "ORDER BY id", (time.time() - GAN_DAY_GIAY,)).fetchall()
    cuoi: dict[str, dict] = {}
    for r in gan:
        cuoi[f"{r['provider']}:{r['model']}"] = dict(r)
    for khoa, tt in kq["mo_hinh"].items():
        ncc, model = khoa.split(":", 1)
        if ncc == "claude" and tt["trang_thai"] == "ok" and time.time() < llm._claude_tam_dung_den:
            tt.update(trang_thai="loi", chi_tiet=f"Claude đang tạm dừng: {llm._claude_ly_do_tam_dung}"[:200])
            continue
        r = next((v for k, v in cuoi.items() if k == khoa or k.startswith(khoa + "-")), None)
        if r is not None:
            tt["lan_cuoi"] = time.strftime("%H:%M", time.localtime(r["ts"]))
            if not r["ok"] and tt["trang_thai"] == "ok":
                tt.update(trang_thai="loi", chi_tiet=f"Lần gọi gần nhất ({tt['lan_cuoi']}) lỗi: {(r['error'] or '?')[:160]}")
    return kq
