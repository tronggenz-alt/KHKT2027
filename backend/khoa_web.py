"""Khoá truy cập web bằng mã 4 số cho các địa chỉ IP mới; các IP được miễn (cấu hình trực tiếp hoặc lấy từ lịch sử đăng nhập
của tài khoản được chỉ định trong CSDL) được vào thẳng không cần nhập mã.

- Không ảnh hưởng `/api/suc-khoe` (Render health check) và `/favicon.ico`.
- Kiểm tra mã 4 số hoàn toàn ở backend (constant-time), có giới hạn tần suất theo IP chống dò mã (`gioi_han_tan_suat["khoa_web"]`).
- Khi nhập đúng mã: cấp cookie `HttpOnly` có chữ ký HMAC-SHA256 (suy từ khoá chính của app) hiệu lực 7 ngày; đổi mã sẽ tự vô hiệu cookie cũ.
"""
from __future__ import annotations

import hashlib
import hmac
import html
import ipaddress
import json
import logging
import math
import re
import secrets
import threading
import time
from http.cookies import SimpleCookie
from types import SimpleNamespace
from urllib.parse import parse_qs

from fastapi import Request
from fastapi.responses import HTMLResponse, JSONResponse
from starlette.datastructures import Headers

from . import bao_mat, db
from .bao_mat import gioi_han_tan_suat
from .config import settings
from .dia_chi_ip import chuan_ip, get_client_ip

log = logging.getLogger(__name__)

TEN_COOKIE = "khoa_web_mo"
DUONG_DAN_MO = "/khoa-web/mo"
TTL_GIAY = 7 * 86400
_MA_BON_SO = re.compile(r"^\d{4}$")
_DUONG_DAN_BO_QUA = {"/api/suc-khoe", "/favicon.ico"}

_khoa_bo_nho = threading.Lock()
_bo_nho_ip_user: dict[tuple[str, int], frozenset[str]] = {}


def xoa_bo_nho_ip() -> None:
    """Xoá bộ nhớ đệm IP được miễn (dùng khi kiểm thử hoặc đổi CSDL)."""
    with _khoa_bo_nho:
        _bo_nho_ip_user.clear()


def dang_bat() -> bool:
    """Khoá web chỉ bật khi KHOA_WEB_BAT=1, không chạy chế độ desktop app và đã có mã (hoặc mã băm)."""
    return bool(
        settings.khoa_web_bat
        and not settings.che_do_app
        and ((settings.khoa_web_ma or "").strip() or (settings.khoa_web_ma_bam or "").strip())
    )


def _van_tay_ma() -> str:
    goc = (settings.khoa_web_ma or "").strip() or (settings.khoa_web_ma_bam or "").strip()
    return hashlib.sha256(f"kw-fp:{goc}".encode("utf-8")).hexdigest()[:10]


def kiem_ma(ma: str) -> bool:
    """Kiểm tra mã 4 số ở server (so sánh hằng thời gian, không bao giờ gửi mã gốc về trình duyệt)."""
    s = (ma or "").strip()
    if not _MA_BON_SO.fullmatch(s):
        return False
    ma_goc = (settings.khoa_web_ma or "").strip()
    if ma_goc:
        return secrets.compare_digest(s, ma_goc)
    ma_bam = (settings.khoa_web_ma_bam or "").strip()
    if ":" in ma_bam:
        muoi, bam_dich = ma_bam.split(":", 1)
        if muoi and bam_dich:
            bam_tinh = hashlib.sha256(f"{muoi}:{s}".encode("utf-8")).hexdigest()
            return secrets.compare_digest(bam_tinh, bam_dich.lower())
    return False


def _khoa_ky() -> bytes:
    if bao_mat.ma_hoa_san_sang():
        return bao_mat._suy_khoa(b"khoa-web/v1")
    return bao_mat.khoa_ky_phien().encode("utf-8")


def tao_cookie(bay_gio: float | None = None) -> str:
    het_han = int((time.time() if bay_gio is None else bay_gio) + TTL_GIAY)
    noi_dung = f"v1.{het_han}.{_van_tay_ma()}"
    chu_ky = hmac.new(_khoa_ky(), noi_dung.encode("ascii"), hashlib.sha256).hexdigest()
    return f"{noi_dung}.{chu_ky}"


def cookie_hop_le(gia_tri: str | None, bay_gio: float | None = None) -> bool:
    if not gia_tri or len(gia_tri) > 256:
        return False
    phan = gia_tri.split(".")
    if len(phan) != 4 or phan[0] != "v1":
        return False
    try:
        het_han = int(phan[1])
    except ValueError:
        return False
    now = time.time() if bay_gio is None else bay_gio
    if het_han <= now:
        return False
    if not secrets.compare_digest(phan[2], _van_tay_ma()):
        return False
    noi_dung = f"{phan[0]}.{phan[1]}.{phan[2]}"
    ky_dung = hmac.new(_khoa_ky(), noi_dung.encode("ascii"), hashlib.sha256).hexdigest()
    return hmac.compare_digest(phan[3], ky_dung)


def _ip_mien_tu_csdl(uid: int) -> frozenset[str]:
    """Lấy IP đăng nhập gốc của tài khoản `uid` từ CSDL (activity_log.dang_nhap đầu tiên + users.register_ip).
    Lưu đệm sau khi tìm thấy để mỗi request kiểm tra O(1) không phải chạm SQLite."""
    if uid <= 0:
        return frozenset()
    khoa = (str(db.DUONG_DAN), uid)
    with _khoa_bo_nho:
        da_co = _bo_nho_ip_user.get(khoa)
        if da_co:
            return da_co
    ips: set[str] = set()
    try:
        with db.ket_noi() as con:
            r_log = con.execute(
                "SELECT ip FROM activity_log WHERE user_id = ? AND event = 'dang_nhap' AND ip <> '' "
                "ORDER BY ts ASC, id ASC LIMIT 1",
                (uid,),
            ).fetchone()
            if r_log and (ip := chuan_ip(r_log["ip"])):
                ips.add(ip)
            r_u = con.execute("SELECT register_ip FROM users WHERE id = ?", (uid,)).fetchone()
            if r_u and (ip := chuan_ip(r_u["register_ip"])):
                ips.add(ip)
    except Exception:  # noqa: BLE001
        log.exception("Không đọc được IP được miễn khoá web cho user #%d", uid)
        return frozenset()
    kq = frozenset(ips)
    if kq:
        with _khoa_bo_nho:
            _bo_nho_ip_user[khoa] = kq
    return kq


def _cac_mang_mien_cau_hinh() -> tuple:
    ds = []
    for phan in re.split(r"[,;\s]+", settings.khoa_web_mien_ip or ""):
        if not phan:
            continue
        try:
            ds.append(ipaddress.ip_network(phan, strict=False))
        except ValueError:
            log.warning("KHOA_WEB_MIEN_IP: bỏ qua %r (không phải IP hay dải CIDR)", phan)
    return tuple(ds)


def ip_duoc_mien(ip: str) -> bool:
    """True nếu `ip` thuộc IP được miễn (không phải nhập mã khoá web)."""
    ip_c = chuan_ip(ip)
    if not ip_c:
        return False
    dc = ipaddress.ip_address(ip_c)
    for mang in _cac_mang_mien_cau_hinh():
        if dc in mang:
            return True
    if settings.khoa_web_mien_user_id > 0 and ip_c in _ip_mien_tu_csdl(settings.khoa_web_mien_user_id):
        return True
    return False


def ghi_log_khoi_dong() -> None:
    if not dang_bat():
        return
    so_db = len(_ip_mien_tu_csdl(settings.khoa_web_mien_user_id)) if settings.khoa_web_mien_user_id > 0 else 0
    so_env = len(_cac_mang_mien_cau_hinh())
    log.info("Khoá truy cập web đang BẬT (miễn %d IP từ CSDL + %d mục cấu hình; IP mới phải nhập mã 4 số)", so_db, so_env)


def _doc_cookie_tu_scope(scope) -> str | None:
    raw = Headers(scope=scope).get("cookie", "")
    if not raw:
        return None
    ck = SimpleCookie()
    try:
        ck.load(raw)
    except Exception:  # noqa: BLE001
        return None
    m = ck.get(TEN_COOKIE)
    return m.value if m else None


def trang_khoa_html(cau_hoi: str) -> str:
    ch = html.escape(cau_hoi or "Ngày sinh của chủ app là gì")
    return f"""<!doctype html>
<html lang="vi">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <meta name="robots" content="noindex, nofollow">
  <title>Khoá truy cập · CHEMISTRY</title>
  <link rel="icon" href="data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='none' stroke='%23e2e8f0' stroke-width='1.8' stroke-linecap='round' stroke-linejoin='round'%3E%3Crect x='5' y='11' width='14' height='10' rx='2.5'/%3E%3Cpath d='M8 11V7.5a4 4 0 0 1 8 0V11'/%3E%3C/svg%3E">
  <style>
    :root {{
      --bg1: #070a10;
      --bg2: #111827;
      --card: rgba(17, 22, 33, 0.88);
      --border: rgba(255, 255, 255, 0.10);
      --text: #f8fafc;
      --muted: #94a3b8;
      --accent: #38bdf8;
      --accent-glow: rgba(56, 189, 248, 0.25);
      --danger: #f87171;
      --ok: #34d399;
    }}
    * {{ box-sizing: border-box; }}
    html, body {{
      margin: 0;
      min-height: 100%;
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;
      color: var(--text);
      background:
        radial-gradient(60rem 36rem at 20% 10%, rgba(56, 189, 248, 0.12), transparent 60%),
        radial-gradient(50rem 34rem at 85% 90%, rgba(99, 102, 241, 0.14), transparent 60%),
        linear-gradient(155deg, var(--bg1), var(--bg2));
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 20px;
    }}
    .card {{
      width: 100%;
      max-width: 410px;
      padding: 36px 28px 30px;
      border-radius: 22px;
      background: var(--card);
      border: 1px solid var(--border);
      box-shadow:
        0 24px 64px rgba(0, 0, 0, 0.58),
        inset 0 1px 0 rgba(255, 255, 255, 0.08);
      backdrop-filter: blur(18px);
      -webkit-backdrop-filter: blur(18px);
      text-align: center;
    }}
    .icon-wrap {{
      width: 58px;
      height: 58px;
      margin: 0 auto 18px;
      border-radius: 16px;
      display: grid;
      place-items: center;
      background: linear-gradient(180deg, rgba(255, 255, 255, 0.08), rgba(255, 255, 255, 0.02));
      border: 1px solid rgba(255, 255, 255, 0.12);
      box-shadow: 0 10px 24px rgba(0, 0, 0, 0.35);
    }}
    .icon-wrap svg {{ width: 26px; height: 26px; color: var(--accent); }}
    .badge {{
      display: inline-block;
      font-size: 11px;
      font-weight: 600;
      letter-spacing: 0.14em;
      text-transform: uppercase;
      color: var(--muted);
      margin-bottom: 10px;
    }}
    h1 {{
      margin: 0 0 8px;
      font-size: 21px;
      line-height: 1.35;
      font-weight: 700;
      letter-spacing: -0.01em;
    }}
    .sub {{
      margin: 0 0 26px;
      font-size: 13.5px;
      color: var(--muted);
      line-height: 1.5;
    }}
    .pin-row {{
      display: flex;
      justify-content: center;
      gap: 12px;
      margin-bottom: 22px;
    }}
    .pin-row.shake {{
      animation: shake 0.36s cubic-bezier(.36,.07,.19,.97) both;
    }}
    @keyframes shake {{
      10%, 90% {{ transform: translateX(-2px); }}
      20%, 80% {{ transform: translateX(4px); }}
      30%, 50%, 70% {{ transform: translateX(-6px); }}
      40%, 60% {{ transform: translateX(6px); }}
    }}
    .pin {{
      width: 58px;
      height: 64px;
      border-radius: 14px;
      border: 1.5px solid rgba(255, 255, 255, 0.14);
      background: rgba(8, 12, 20, 0.72);
      color: var(--text);
      font-size: 26px;
      font-weight: 700;
      text-align: center;
      outline: none;
      caret-color: var(--accent);
      transition: border-color 0.16s ease, box-shadow 0.16s ease, transform 0.12s ease;
    }}
    .pin:focus {{
      border-color: var(--accent);
      box-shadow: 0 0 0 3px var(--accent-glow);
      transform: translateY(-1px);
    }}
    .pin.err {{
      border-color: rgba(248, 113, 113, 0.75);
      box-shadow: 0 0 0 3px rgba(248, 113, 113, 0.18);
    }}
    .btn {{
      width: 100%;
      height: 46px;
      border-radius: 12px;
      border: 1px solid rgba(255, 255, 255, 0.14);
      background: linear-gradient(180deg, #1f2937, #111827);
      color: #f8fafc;
      font-size: 14.5px;
      font-weight: 600;
      cursor: pointer;
      box-shadow: 0 6px 18px rgba(0, 0, 0, 0.42), inset 0 1px 0 rgba(255, 255, 255, 0.12);
      transition: transform 0.12s ease, box-shadow 0.16s ease, opacity 0.16s ease, border-color 0.16s ease;
    }}
    .btn:hover:not(:disabled) {{
      border-color: rgba(56, 189, 248, 0.55);
      transform: translateY(-1px);
      box-shadow: 0 10px 24px rgba(0, 0, 0, 0.5), inset 0 1px 0 rgba(255, 255, 255, 0.16);
    }}
    .btn:active:not(:disabled) {{ transform: translateY(0); }}
    .btn:disabled {{ opacity: 0.6; cursor: default; }}
    .msg {{
      min-height: 20px;
      margin: 14px 0 0;
      font-size: 13px;
      color: var(--danger);
    }}
    .msg.ok {{ color: var(--ok); }}
  </style>
</head>
<body>
  <main class="card">
    <div class="icon-wrap" aria-hidden="true">
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">
        <rect x="5" y="11" width="14" height="10" rx="2.5"></rect>
        <path d="M8 11V7.5a4 4 0 0 1 8 0V11"></path>
      </svg>
    </div>
    <div class="badge">CHEMISTRY · KHOÁ TRUY CẬP</div>
    <h1>{ch}</h1>
    <p class="sub">Nhập mật khẩu 4 số để vào web</p>
    <form id="f" autocomplete="off" novalidate>
      <div class="pin-row" id="row">
        <input class="pin" type="password" inputmode="numeric" pattern="[0-9]*" maxlength="1" aria-label="Số 1" autofocus>
        <input class="pin" type="password" inputmode="numeric" pattern="[0-9]*" maxlength="1" aria-label="Số 2">
        <input class="pin" type="password" inputmode="numeric" pattern="[0-9]*" maxlength="1" aria-label="Số 3">
        <input class="pin" type="password" inputmode="numeric" pattern="[0-9]*" maxlength="1" aria-label="Số 4">
      </div>
      <button type="submit" class="btn" id="btn">Mở khoá</button>
      <p class="msg" id="msg" role="alert" aria-live="polite"></p>
    </form>
  </main>
  <script>
    (function () {{
      var inputs = Array.from(document.querySelectorAll('.pin'));
      var row = document.getElementById('row');
      var btn = document.getElementById('btn');
      var msg = document.getElementById('msg');
      var busy = false;

      function getPin() {{ return inputs.map(function (i) {{ return i.value; }}).join(''); }}
      function setErr(t) {{
        msg.className = 'msg';
        msg.textContent = t || '';
        inputs.forEach(function (i) {{ i.classList.toggle('err', !!t); }});
        if (t) {{
          row.classList.remove('shake');
          void row.offsetWidth;
          row.classList.add('shake');
        }}
      }}
      function clearPins() {{
        inputs.forEach(function (i) {{ i.value = ''; }});
        inputs[0].focus();
      }}
      async function submitPin() {{
        if (busy) return;
        var ma = getPin();
        if (!/^\\d{{4}}$/.test(ma)) {{
          setErr('Vui lòng nhập đủ 4 chữ số.');
          var empty = inputs.find(function (i) {{ return !i.value; }});
          if (empty) empty.focus();
          return;
        }}
        busy = true;
        btn.disabled = true;
        btn.textContent = 'Đang kiểm tra…';
        setErr('');
        try {{
          var r = await fetch('{DUONG_DAN_MO}', {{
            method: 'POST',
            headers: {{ 'Content-Type': 'application/json' }},
            credentials: 'same-origin',
            body: JSON.stringify({{ ma: ma }})
          }});
          var d = await r.json().catch(function () {{ return {{}}; }});
          if (r.ok && d.ok) {{
            msg.className = 'msg ok';
            msg.textContent = 'Đã mở khoá — đang vào web…';
            btn.textContent = 'Đã mở khoá';
            location.reload();
            return;
          }}
          setErr(d.loi || 'Mật khẩu 4 số không đúng.');
          clearPins();
        }} catch (_) {{
          setErr('Không kết nối được máy chủ, vui lòng thử lại.');
        }} finally {{
          busy = false;
          btn.disabled = false;
          btn.textContent = 'Mở khoá';
        }}
      }}

      inputs.forEach(function (el, idx) {{
        el.addEventListener('input', function () {{
          var digits = el.value.replace(/\\D/g, '');
          if (digits.length > 1) {{
            for (var k = 0; k < digits.length && idx + k < inputs.length; k++) {{
              inputs[idx + k].value = digits[k];
            }}
            var next = Math.min(inputs.length - 1, idx + digits.length);
            inputs[next].focus();
          }} else {{
            el.value = digits;
            if (digits && idx < inputs.length - 1) inputs[idx + 1].focus();
          }}
          setErr('');
          if (getPin().length === 4) submitPin();
        }});
        el.addEventListener('keydown', function (e) {{
          if (e.key === 'Backspace' && !el.value && idx > 0) {{
            inputs[idx - 1].value = '';
            inputs[idx - 1].focus();
          }} else if (e.key === 'ArrowLeft' && idx > 0) {{
            inputs[idx - 1].focus();
          }} else if (e.key === 'ArrowRight' && idx < inputs.length - 1) {{
            inputs[idx + 1].focus();
          }}
        }});
        el.addEventListener('paste', function (e) {{
          var txt = (e.clipboardData || window.clipboardData).getData('text') || '';
          var d = txt.replace(/\\D/g, '').slice(0, 4);
          if (!d) return;
          e.preventDefault();
          for (var k = 0; k < inputs.length; k++) inputs[k].value = d[k] || '';
          if (d.length === 4) submitPin();
          else inputs[Math.min(3, d.length)].focus();
        }});
      }});
      document.getElementById('f').addEventListener('submit', function (e) {{
        e.preventDefault();
        submitPin();
      }});
    }})();
  </script>
</body>
</html>"""


async def _xu_ly_mo_khoa(request: Request, ip: str) -> JSONResponse:
    cho = gioi_han_tan_suat.kiem_tra("khoa_web", f"ip:{ip}")
    if cho is not None:
        giay = max(1, math.ceil(cho))
        return JSONResponse(
            {"ok": False, "loi": f"Bạn nhập sai quá nhiều lần – hãy thử lại sau {giay} giây.", "ma": "gioi_han_tan_suat"},
            status_code=429,
            headers={"Retry-After": str(giay), "Cache-Control": "private, no-store"},
        )
    raw = await request.body()
    if len(raw) > 4096:
        return JSONResponse({"ok": False, "loi": "Yêu cầu không hợp lệ."}, status_code=400,
                            headers={"Cache-Control": "private, no-store"})
    ma = ""
    ct = (request.headers.get("content-type") or "").lower()
    if "application/json" in ct:
        try:
            data = json.loads(raw.decode("utf-8"))
            if isinstance(data, dict):
                ma = str(data.get("ma") or "")
        except Exception:  # noqa: BLE001
            ma = ""
    else:
        parsed = parse_qs(raw.decode("utf-8", "ignore"))
        ma = (parsed.get("ma") or [""])[0]

    if not kiem_ma(ma):
        return JSONResponse(
            {"ok": False, "loi": "Mật khẩu 4 số không đúng. Vui lòng thử lại.", "ma": "sai_ma"},
            status_code=401,
            headers={"Cache-Control": "private, no-store"},
        )
    res = JSONResponse({"ok": True}, status_code=200, headers={"Cache-Control": "private, no-store"})
    res.set_cookie(
        key=TEN_COOKIE,
        value=tao_cookie(),
        max_age=TTL_GIAY,
        path="/",
        httponly=True,
        samesite="lax",
        secure=settings.cookie_secure,
    )
    return res


class KhoaWebMiddleware:
    """ASGI middleware ngoài cùng: miễn cho IP đã đăng ký (vd tài khoản được chỉ định), buộc IP mới nhập mã 4 số."""

    def __init__(self, app):
        self.app = app

    async def __call__(self, scope, receive, send):
        loai = scope.get("type")
        if loai not in ("http", "websocket") or not dang_bat():
            return await self.app(scope, receive, send)

        duong_dan = scope.get("path", "")
        if loai == "websocket":
            cli = scope.get("client")
            req_ws = SimpleNamespace(
                client=SimpleNamespace(host=cli[0]) if cli else None,
                headers=Headers(scope=scope),
            )
            if ip_duoc_mien(get_client_ip(req_ws)) or cookie_hop_le(_doc_cookie_tu_scope(scope)):
                return await self.app(scope, receive, send)
            await send({"type": "websocket.close", "code": 4403})
            return

        phuong_thuc = (scope.get("method") or "GET").upper()
        if duong_dan in _DUONG_DAN_BO_QUA and phuong_thuc in ("GET", "HEAD"):
            return await self.app(scope, receive, send)

        req = Request(scope, receive)
        ip = get_client_ip(req)

        if duong_dan == DUONG_DAN_MO and phuong_thuc == "POST":
            res = await _xu_ly_mo_khoa(req, ip)
            return await res(scope, receive, send)

        if ip_duoc_mien(ip) or cookie_hop_le(req.cookies.get(TEN_COOKIE)):
            return await self.app(scope, receive, send)

        accept = (req.headers.get("accept") or "").lower()
        la_api = duong_dan.startswith(("/api/", "/auth/", "/admin/api/", "/static/")) or (
            "application/json" in accept and "text/html" not in accept
        )
        if phuong_thuc in ("GET", "HEAD") and not la_api:
            res = HTMLResponse(trang_khoa_html(settings.khoa_web_cau_hoi), status_code=403,
                               headers={"Cache-Control": "private, no-store"})
        else:
            res = JSONResponse(
                {"loi": "Vui lòng nhập mật khẩu 4 số để mở khoá web.", "ma": "can_mo_khoa"},
                status_code=403,
                headers={"Cache-Control": "private, no-store"},
            )
        return await res(scope, receive, send)
