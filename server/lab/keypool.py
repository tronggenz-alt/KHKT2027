"""Mạng lưới khóa API: nhiều khóa của nhiều nhà cung cấp (Groq, DeepSeek, Gemini) dùng chung như một.

Mỗi lần gọi chọn khóa còn dùng được theo ưu tiên (số nhỏ trước), cùng ưu tiên thì khóa lâu chưa dùng nhất
(xoay vòng để chia đều hạn mức miễn phí). Cả ba nhà cung cấp có API kiểu OpenAI nên dùng chung một cách gọi.

Mô hình không gắn cứng: với mỗi khóa, hỏi nhà cung cấp danh sách mô hình đang có (GET /models, nhớ 6 giờ),
bỏ các mô hình không phải chat (embedding, giọng nói, ảnh, kiểm duyệt...), xếp mô hình mạnh nhất lên đầu
(strength(): phiên bản mới hơn, dòng pro/ultra, nhiều tham số hơn) rồi thử lần lượt:
  - mô hình bị gỡ / không tồn tại (404, "model not found", "no longer available") -> bỏ 1 ngày, lấy lại danh sách;
  - mô hình hết hạn mức (429, hạn mức tính riêng từng mô hình) -> nghỉ theo Retry-After, thử mô hình kế tiếp;
  - mô hình chạy quá lâu, lỗi máy chủ, không nhận tham số -> nghỉ ngắn, thử mô hình kế tiếp;
  - khóa sai (401/403) -> khóa nghỉ 1 giờ; lỗi kết nối -> khóa nghỉ ngắn; cả hai chuyển sang khóa kế tiếp.
Người dùng có thể ghim một mô hình cho khóa (cột mo_hinh): mô hình đó được thử trước, lỗi thì vẫn tự chuyển.

Khóa lưu trong data/lab.db trên máy này, không bao giờ gửi cho ứng dụng Unity hay trình duyệt (web chỉ thấy dạng che).
"""

from __future__ import annotations

import json
import math
import re
import time

import httpx

from . import db

# du_phong: chỉ dùng khi không lấy được danh sách mô hình từ nhà cung cấp (mất mạng tạm thời, API /models lỗi)
PROVIDERS = {
    "groq": {"ten": "Groq", "base_url": "https://api.groq.com/openai/v1", "mo_hinh": "tự chọn mạnh nhất", "dau_khoa": "gsk_",
             "du_phong": ["openai/gpt-oss-120b", "llama-3.3-70b-versatile", "llama-3.1-8b-instant"]},
    "deepseek": {"ten": "DeepSeek", "base_url": "https://api.deepseek.com/v1", "mo_hinh": "tự chọn mạnh nhất", "dau_khoa": "sk-",
                 "du_phong": ["deepseek-chat"]},
    "gemini": {"ten": "Gemini", "base_url": "https://generativelanguage.googleapis.com/v1beta/openai", "mo_hinh": "tự chọn mạnh nhất",
               "dau_khoa": "AIza", "du_phong": ["gemini-flash-latest", "gemini-flash-lite-latest"]},
}

TIMEOUT = httpx.Timeout(30.0, connect=10.0)  # một lần gọi; quá lâu (máy chủ nhà cung cấp quá tải) thì chuyển mô hình khác
DEADLINE_S = 80.0       # tổng thời gian thử mọi khóa / mô hình cho một câu hỏi (Unity chờ kế hoạch robot tối đa 90 giây)
MODELS_TTL = 6 * 3600   # nhớ danh sách mô hình 6 giờ
MAX_MODELS_PER_KEY = 5  # mỗi khóa thử tối đa chừng này mô hình rồi chuyển khóa
THINK_ROOM = 2048       # mô hình đang suy nghĩ: thêm chỗ cho phần suy nghĩ (tính vào max_tokens)

_models: dict[str, tuple[float, list[str]]] = {}         # khóa -> (lúc lấy, danh sách đã xếp hạng)
_cool: dict[tuple[str, str], tuple[float, str]] = {}     # (khóa, mô hình) -> (nghỉ đến, lý do)
_last: dict[int, str] = {}                               # id khóa -> mô hình vừa trả lời được


def reset_cache() -> None:
    _models.clear()
    _cool.clear()
    _last.clear()


# ---------------------------------------------------------------------------
# Xếp hạng mô hình
# ---------------------------------------------------------------------------

# Không phải mô hình chat văn bản (hoặc là hệ thống tác tử / bản chưng cất yếu)
_NOT_CHAT = re.compile(r"embed|tts|whisper|audio|speech|transcri|guard|moderat|image|imagen|veo|lyria|banana|aqa|"
                       r"(^|-)live(-|$)|native|computer-use|robotics|compound|playai|orpheus|distil|ocr|rerank|"
                       r"research|learnlm|clip|dall")
_TIER = {"ultra": 30, "pro": 25, "opus": 25, "max": 15, "maverick": 10, "large": 10, "reasoner": 10, "thinking": 5,
         "lite": -15, "mini": -12, "nano": -20, "small": -12, "tiny": -20, "instant": -12, "scout": -8, "air": -8,
         "preview": -2, "exp": -2, "experimental": -2}
# Dòng mô hình không ghi số phiên bản trong tên (hoặc số phiên bản khác thang): chỉnh để so được với nhau
_FAMILY = {"gpt-oss": 35, "kimi": 25, "deepseek": 40, "gemma": -30, "allam": -20, "saba": -10}


def _parse(mid: str) -> tuple[float | None, float | None, float | None, set[str]]:
    """(phiên bản, số tham số tỉ, số chuyên gia MoE, các từ) từ tên như llama-4-maverick-17b-128e-instruct."""
    version = size = experts = None
    words = set()
    for tok in re.split(r"[-_/:]", mid):
        if re.fullmatch(r"\d+(?:\.\d+)?b", tok):
            size = float(tok[:-1])
        elif re.fullmatch(r"\d+e", tok):
            experts = float(tok[:-1])
        elif re.fullmatch(r"\d+(?:\.\d+)?", tok):
            if version is None and float(tok) < 20:  # bỏ số ngày tháng như 0905, 001
                version = float(tok)
        else:
            m = re.fullmatch(r"[a-z]+(\d+(?:\.\d+)?)", tok)  # qwen3, k2, gemma2, v3
            if m and version is None and float(m.group(1)) < 20:
                version = float(m.group(1))
            words.add(tok)
    return version, size, experts, words


def strength(mid: str, latest_version: float = 0) -> float:
    """Điểm mạnh ước lượng từ tên mô hình (chỉ để xếp thứ tự thử, không cần chính xác tuyệt đối)."""
    m = mid.lower()
    version, size, experts, words = _parse(m)
    if version is None and "latest" in words:
        version = latest_version  # bí danh "...-latest" trỏ tới bản mới nhất
    s = 10 * (version or 0)
    s += sum(v for w, v in _TIER.items() if w in words)
    s += sum(v for f, v in _FAMILY.items() if f in m)
    if size:
        s += 3 * math.log2(max(size, 1))
    if experts:
        s += 1.5 * math.log2(max(experts, 1))
    return s


def rank_models(provider: str, ids: list[str]) -> list[str]:
    """Lọc mô hình chat và xếp mạnh trước."""
    names = []
    for i in ids:
        i = i.removeprefix("models/")
        if i and not _NOT_CHAT.search(i.lower()) and i not in names:
            names.append(i)
    latest = max((_parse(n.lower())[0] or 0 for n in names), default=0)
    return sorted(names, key=lambda n: (-strength(n, latest), n))


async def _list_models(row: dict, client: httpx.AsyncClient) -> list[str]:
    """Danh sách mô hình của khóa (đã xếp hạng), nhớ MODELS_TTL. Khóa sai thì ném _BadKey."""
    key = row["khoa"]
    cached = _models.get(key)
    if cached and time.time() - cached[0] < MODELS_TTL:
        return cached[1]
    p = PROVIDERS[row["nha_cung_cap"]]
    ids: list[str] = []
    try:
        r = await client.get(p["base_url"] + "/models", headers={"Authorization": f"Bearer {key}"})
        if _bad_key(r):
            raise _BadKey(f"{r.status_code} khóa không hợp lệ hoặc hết quyền")
        if r.status_code == 200:
            ids = [str(m.get("id", "")) for m in r.json().get("data", []) if isinstance(m, dict)]
    except (httpx.HTTPError, ValueError, AttributeError):
        ids = []
    ranked = rank_models(row["nha_cung_cap"], ids) or list(p["du_phong"])
    # lấy danh sách thất bại: dùng danh sách dự phòng, 5 phút sau hỏi lại
    _models[key] = (time.time() if ids else time.time() - MODELS_TTL + 300, ranked)
    return ranked


class _BadKey(Exception):
    pass


class AllKeysFailed(RuntimeError):
    pass


def mask(key: str) -> str:
    return key[:4] + "…" + key[-4:] if len(key) > 10 else "…"


def public_row(r: dict) -> dict:
    now = time.time()
    key = r["khoa"]
    r = dict(r)
    r["khoa"] = mask(key)
    r["ten_nha_cung_cap"] = PROVIDERS.get(r["nha_cung_cap"], {}).get("ten", r["nha_cung_cap"])
    r["dang_nghi_giay"] = max(0, round(r["nghi_den"] - now))
    r["mo_hinh_dang_dung"] = _last.get(r["id"], "")
    # thứ tự sẽ thử (mạnh trước), kèm mô hình đang nghỉ và lý do; rỗng nếu chưa hỏi danh sách lần nào
    order = []
    for m in _order(r, _models.get(key, (0, []))[1])[:8]:
        until, why = _cool.get((key, m), (0, ""))
        order.append({"ten": m, "nghi_giay": max(0, round(until - now)), "ly_do": why if until > now else ""})
    r["thu_tu_mo_hinh"] = order
    return r


def list_keys() -> list[dict]:
    return [public_row(r) for r in db.query("SELECT * FROM khoa_api ORDER BY uu_tien, id")]


def add_key(nha_cung_cap: str, khoa: str, nhan: str = "", mo_hinh: str = "", uu_tien: int = 10) -> int:
    if nha_cung_cap not in PROVIDERS:
        raise ValueError("Nhà cung cấp phải là: " + ", ".join(PROVIDERS))
    khoa = khoa.strip()
    p = PROVIDERS[nha_cung_cap]
    if "://" in khoa or any(ch.isspace() for ch in khoa):
        raise ValueError("Đây là đường link hoặc đoạn chữ, không phải khóa API. Bấm nút sao chép khóa trên trang của "
                         f"{p['ten']} rồi dán lại (khóa {p['ten']} bắt đầu bằng {p['dau_khoa']}…)")
    if len(khoa) < 20:
        raise ValueError("Khóa API quá ngắn, có thể chưa sao chép hết")
    if not khoa.startswith(p["dau_khoa"]):
        raise ValueError(f"Khóa {p['ten']} phải bắt đầu bằng {p['dau_khoa']}… Kiểm tra lại đã chọn đúng nhà cung cấp chưa")
    cur = db.execute("INSERT INTO khoa_api (nha_cung_cap, nhan, khoa, mo_hinh, uu_tien, tao_luc) VALUES (?,?,?,?,?,?)",
                     (nha_cung_cap, nhan.strip(), khoa, mo_hinh.strip(), int(uu_tien), time.time()))
    return cur.lastrowid


def update_key(key_id: int, **fields) -> None:
    allowed = {"nhan", "mo_hinh", "uu_tien", "bat"}
    sets = {k: v for k, v in fields.items() if k in allowed and v is not None}
    if "bat" in sets:
        sets["bat"] = 1 if sets["bat"] else 0
        if sets["bat"]:
            sets["nghi_den"] = 0
    if not sets:
        return
    cols = ", ".join(f"{k}=?" for k in sets)
    db.execute(f"UPDATE khoa_api SET {cols} WHERE id=?", (*sets.values(), key_id))


def delete_key(key_id: int) -> None:
    db.execute("DELETE FROM khoa_api WHERE id=?", (key_id,))


def available() -> bool:
    """Có ít nhất một khóa đang bật và không trong thời gian nghỉ."""
    return bool(db.query("SELECT 1 FROM khoa_api WHERE bat=1 AND nghi_den<=? LIMIT 1", (time.time(),)))


def _candidates() -> list[dict]:
    now = time.time()
    return db.query("SELECT * FROM khoa_api WHERE bat=1 AND nghi_den<=? ORDER BY uu_tien, dung_luc", (now,))


def _mark(key_id: int, trang_thai: str, loi: str = "", nghi: float = 0, ok: bool = False, tre_ms: float = 0) -> None:
    now = time.time()
    if ok:
        db.execute("UPDATE khoa_api SET trang_thai=?, loi_gan_nhat='', so_lan_goi=so_lan_goi+1, dung_luc=?, tre_ms=?, nghi_den=0 WHERE id=?",
                   (trang_thai, now, tre_ms, key_id))
    else:
        db.execute("UPDATE khoa_api SET trang_thai=?, loi_gan_nhat=?, so_lan_loi=so_lan_loi+1, dung_luc=?, nghi_den=? WHERE id=?",
                   (trang_thai, loi[:300], now, now + nghi, key_id))


def _order(row: dict, ranked: list[str]) -> list[str]:
    """Mô hình ghim (nếu có) trước, rồi danh sách tự xếp hạng."""
    pinned = (row.get("mo_hinh") or "").strip()
    return ([pinned] if pinned else []) + [m for m in ranked if m != pinned]


def _bad_key(r: httpx.Response) -> bool:
    """Khóa sai: 401/403, hoặc Gemini trả 400 "API key not valid / Please pass a valid API key"."""
    return r.status_code in (401, 403) or (r.status_code == 400 and "api key" in r.text.lower())


def _model_gone(r: httpx.Response) -> bool:
    """Mô hình bị gỡ / không tồn tại / khóa không được dùng mô hình này."""
    t = r.text.lower()
    if r.status_code == 404:
        return True
    if r.status_code != 400 or "model" not in t:
        return False
    if any(w in t for w in ("thinking", "reasoning", "budget", "response_format", "json", "schema", "tool")):
        return False  # lỗi vì tham số (mức suy nghĩ, cách xin JSON...): đổi tham số chứ không bỏ mô hình
    return any(w in t for w in ("not found", "does not exist", "not supported", "no longer available", "decommissioned",
                                "deprecated", "not available", "only supports"))  # "only supports Interactions API"


def _formats(provider: str, json_mode: bool, schema: dict | None) -> list[dict | None]:
    """Các cách xin JSON, thử lần lượt nếu mô hình không nhận cách trước (lỗi 400).
    Gemini: JSON theo lược đồ (bảo đảm đúng cú pháp), không được thì theo lời dặn. Groq, DeepSeek: json_object."""
    if not json_mode:
        return [None]
    if provider == "gemini":
        strict = [{"type": "json_schema", "json_schema": {"name": "ket_qua", "strict": True, "schema": schema}}] if schema else []
        return strict + [None]
    return [{"type": "json_object"}]


def _reasoning(provider: str, model: str) -> list[str | None]:
    """Các mức suy nghĩ thử lần lượt (None = không gửi tham số). Tắt / giảm suy nghĩ cho nhanh và đỡ tốn max_tokens;
    mô hình chỉ chạy được khi có suy nghĩ (ví dụ Gemini pro) báo lỗi 400 thì lùi sang mức kế tiếp."""
    m = model.lower()
    if provider == "gemini":
        return ["none", "minimal", "low", None]  # mỗi dòng Gemini nhận một số mức khác nhau
    if provider == "groq" and "gpt-oss" in m:
        return ["low", None]
    if provider == "groq" and "qwen3" in m:
        return ["none", None]
    return [None]


def _thinks(provider: str, model: str, effort: str | None) -> bool:
    m = model.lower()
    if effort == "none":
        return False
    return provider == "gemini" or "reasoner" in m or "gpt-oss" in m or "qwen3" in m or "r1" in m.split("-")


def _body(row: dict, model: str, messages: list[dict], max_tokens: int, fmt: dict | None, effort: str | None,
          tools: list | None) -> dict:
    room = THINK_ROOM if _thinks(row["nha_cung_cap"], model, effort) else 0
    body = {"model": model, "messages": messages, "max_tokens": max_tokens + room, "temperature": 0.3}
    if fmt:
        body["response_format"] = fmt
    if effort:
        body["reasoning_effort"] = effort
    if tools:
        body["tools"] = tools
        body["tool_choice"] = "auto"
    return body


def _set_cool(row: dict, model: str, seconds: float, why: str) -> None:
    _cool[(row["khoa"], model)] = (time.time() + seconds, " ".join(why.split())[:160])


async def _post(row: dict, model: str, messages, max_tokens, json_mode, schema, tools, client) -> httpx.Response:
    """Gửi một yêu cầu; 400 vì tham số chưa được hỗ trợ (cách xin JSON, mức suy nghĩ) thì đổi tham số gửi lại."""
    p = PROVIDERS[row["nha_cung_cap"]]
    formats, efforts = _formats(row["nha_cung_cap"], json_mode, schema), _reasoning(row["nha_cung_cap"], model)
    fi = ri = 0
    while True:
        r = await client.post(p["base_url"] + "/chat/completions",
                              json=_body(row, model, messages, max_tokens, formats[fi], efforts[ri], tools),
                              headers={"Authorization": f"Bearer {row['khoa']}"})
        if r.status_code != 400 or _bad_key(r) or _model_gone(r):
            return r
        t = r.text.lower()
        if any(w in t for w in ("thinking", "budget", "reasoning")) and ri + 1 < len(efforts):
            ri += 1
        elif fi + 1 < len(formats):
            fi += 1
        elif ri + 1 < len(efforts):
            ri += 1
        else:
            return r


async def _try_key(row: dict, messages, max_tokens, json_mode, schema, tools, client, tried: list[str],
                   deadline: float) -> dict | None:
    """Thử các mô hình của một khóa, mạnh trước. Trả kết quả, hoặc None để chuyển khóa kế tiếp."""
    p = PROVIDERS[row["nha_cung_cap"]]
    tag = f"{p['ten']}#{row['id']}"
    try:
        ranked = await _list_models(row, client)
    except _BadKey as e:
        _mark(row["id"], "khoa_sai", str(e), 3600)
        tried.append(f"{tag}: khóa không hợp lệ")
        return None
    now = time.time()
    order = [m for m in _order(row, ranked) if _cool.get((row["khoa"], m), (0, ""))[0] <= now][:MAX_MODELS_PER_KEY]
    status, last_err = "loi", "mọi mô hình đang nghỉ"
    for model in order:
        if time.time() > deadline:
            break
        t0 = time.time()
        try:
            r = await _post(row, model, messages, max_tokens, json_mode, schema, tools, client)
        except httpx.ReadTimeout:
            _set_cool(row, model, 600, "trả lời quá lâu")
            tried.append(f"{tag} {model}: quá lâu")
            status, last_err = "loi", f"{model} trả lời quá lâu"
            continue
        except httpx.HTTPError as e:
            _mark(row["id"], "loi_mang", type(e).__name__, 20)
            tried.append(f"{tag}: lỗi mạng")
            return None
        if _bad_key(r):
            _mark(row["id"], "khoa_sai", f"{r.status_code} khóa không hợp lệ hoặc hết quyền", 3600)
            tried.append(f"{tag}: khóa không hợp lệ")
            return None
        if r.status_code == 429:
            wait = float(r.headers.get("retry-after", "60") or 60)
            if "limit: 0" in r.text or "limit:0" in r.text:
                wait = 6 * 3600  # gói miễn phí không có hạn mức cho mô hình này
            _set_cool(row, model, min(max(wait, 5), 6 * 3600), "429 hết hạn mức")
            tried.append(f"{tag} {model}: 429")
            status, last_err = "gioi_han", f"429 quá giới hạn ({model})"
            continue
        if _model_gone(r):
            _set_cool(row, model, 86400, f"{r.status_code} mô hình không còn")
            _models[row["khoa"]] = (0, ranked)  # lần sau hỏi lại danh sách mô hình (vẫn giữ để hiện trên web)
            tried.append(f"{tag} {model}: không còn")
            status, last_err = "loi", f"{model} không còn ({r.status_code})"
            continue
        if r.status_code >= 400:
            _set_cool(row, model, 30 if r.status_code >= 500 else 600, f"{r.status_code} {r.text[:120]}")
            tried.append(f"{tag} {model}: lỗi {r.status_code}")
            status, last_err = "loi", f"{r.status_code} {r.text[:200]}"
            continue
        try:
            choice = r.json()["choices"][0]
            msg = choice["message"]
        except (ValueError, KeyError, IndexError, TypeError):
            _set_cool(row, model, 60, "phản hồi không đúng định dạng")
            tried.append(f"{tag} {model}: phản hồi lạ")
            continue
        cut = choice.get("finish_reason") == "length"
        if cut and not (msg.get("content") or "").strip() and not msg.get("tool_calls"):
            _set_cool(row, model, 300, "hết max_tokens khi đang suy nghĩ")
            tried.append(f"{tag} {model}: hết token")
            continue
        _mark(row["id"], "tot", ok=True, tre_ms=(time.time() - t0) * 1000)
        _last[row["id"]] = model
        return {"message": msg, "nha_cung_cap": row["nha_cung_cap"], "mo_hinh": model, "khoa_id": row["id"], "cat_cut": cut}
    # khóa không còn mô hình nào trả lời được: khóa nghỉ tới khi mô hình đầu tiên hết nghỉ
    soonest = min((_cool.get((row["khoa"], m), (0, ""))[0] for m in _order(row, ranked)), default=0)
    _mark(row["id"], status, last_err, max(5.0, soonest - time.time()))
    if not order:
        tried.append(f"{tag}: mọi mô hình đang nghỉ")
    return None


async def chat(messages: list[dict], max_tokens: int = 600, json_mode: bool = False, tools: list | None = None,
               client: httpx.AsyncClient | None = None, schema: dict | None = None) -> dict:
    """Gọi chat/completions qua khóa và mô hình còn dùng được (mô hình mạnh trước, lỗi thì tự chuyển).
    Trả {"message": {...}, "nha_cung_cap", "mo_hinh", "khoa_id", "cat_cut": bị cắt vì hết max_tokens}."""
    tried: list[str] = []
    rows = _candidates()
    if not rows:
        raise AllKeysFailed("Không có khóa nào đang dùng được (chưa thêm khóa, hoặc tất cả đang nghỉ)")
    deadline = time.time() + DEADLINE_S
    own = client is None
    client = client or httpx.AsyncClient(timeout=TIMEOUT)
    try:
        for row in rows:
            if time.time() > deadline:
                tried.append("hết thời gian chờ")
                break
            res = await _try_key(row, messages, max_tokens, json_mode, schema, tools, client, tried, deadline)
            if res:
                return res
    finally:
        if own:
            await client.aclose()
    raise AllKeysFailed("Mọi khóa đều lỗi: " + "; ".join(tried))


async def test_key(key_id: int) -> dict:
    """Thử một khóa bằng một câu hỏi rất ngắn: hỏi lại danh sách mô hình, rồi dùng mô hình mạnh nhất trả lời được."""
    rows = db.query("SELECT * FROM khoa_api WHERE id=?", (key_id,))
    if not rows:
        raise ValueError("Không có khóa này")
    row = rows[0]
    _models.pop(row["khoa"], None)
    for k in [k for k in _cool if k[0] == row["khoa"]]:
        del _cool[k]
    tried: list[str] = []
    t0 = time.time()
    async with httpx.AsyncClient(timeout=TIMEOUT) as client:
        res = await _try_key(row, [{"role": "user", "content": "Trả lời đúng một từ: OK"}], 5, False, None, None, client,
                             tried, time.time() + DEADLINE_S)
    ranked = _models.get(row["khoa"], (0, []))[1]
    if res:
        return {"ok": True, "tre_ms": round((time.time() - t0) * 1000), "mo_hinh": res["mo_hinh"], "so_mo_hinh": len(ranked),
                "bo_qua": tried}
    return {"ok": False, "loi": "; ".join(tried) or "không rõ", "so_mo_hinh": len(ranked)}


# ---------------------------------------------------------------------------
# Giao diện nhà cung cấp cho server/ai (giống providers.claude / openai_compat)
# ---------------------------------------------------------------------------

NAME = "mang_luoi_khoa"


def model_name() -> str:
    rows = _candidates()
    if not rows:
        return ""
    r = rows[0]
    m = _last.get(r["id"]) or (r["mo_hinh"] or "").strip() or next(iter(_models.get(r["khoa"], (0, []))[1]), "tự chọn mạnh nhất")
    return f"{PROVIDERS[r['nha_cung_cap']]['ten']} {m}"


async def text(system: str, user: str, max_tokens: int = 400) -> str:
    res = await chat([{"role": "system", "content": system}, {"role": "user", "content": user}], max_tokens)
    return (res["message"].get("content") or "").strip()


async def structured(system: str, user: str, schema: dict, max_tokens: int = 800) -> dict:
    hint = ("\n\nTrả lời DUY NHẤT bằng một đối tượng JSON gọn (không xuống dòng thụt lề, không bọc markdown) theo lược đồ: "
            + json.dumps(schema, ensure_ascii=False))
    res = await chat([{"role": "system", "content": system + hint}, {"role": "user", "content": user}], max_tokens,
                     json_mode=True, schema=schema)
    return loads_lenient(res["message"].get("content") or "")


def loads_lenient(text: str) -> dict:
    """Đọc đối tượng JSON trong câu trả lời của mô hình: bỏ ```json bao ngoài, dấu phẩy thừa; nếu bị cắt cụt
    (hết max_tokens) thì giữ phần trọn vẹn cuối cùng, ví dụ các bước kế hoạch đã viết xong."""
    start = (text or "").find("{")
    if start < 0:
        raise ValueError("không có JSON trong câu trả lời")
    s = text[start:]
    end = s.rfind("}")
    whole = s[:end + 1] if end >= 0 else s
    for cand in (whole, re.sub(r",\s*([}\]])", r"\1", whole)):
        try:
            v = json.loads(cand)
            if isinstance(v, dict):
                return v
        except ValueError:
            pass
    # Cắt cụt: duyệt ngoài chuỗi, nhớ mỗi chỗ vừa đóng xong một khối và các ngoặc còn mở lúc đó
    stack, cuts, in_str, esc = [], [], False, False
    for i, ch in enumerate(s):
        if in_str:
            if esc:
                esc = False
            elif ch == "\\":
                esc = True
            elif ch == '"':
                in_str = False
        elif ch == '"':
            in_str = True
        elif ch in "{[":
            stack.append("}" if ch == "{" else "]")
        elif ch in "}]" and stack:
            stack.pop()
            cuts.append((i, "".join(reversed(stack))))
    for i, closers in reversed(cuts):
        try:
            v = json.loads(s[:i + 1] + closers)
            if isinstance(v, dict):
                return v
        except ValueError:
            continue
    raise ValueError("JSON bị cắt cụt, không đọc được")
