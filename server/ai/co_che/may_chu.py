"""Máy chủ cơ chế VERA (FastAPI), chạy riêng ở cổng 8010 để không trùng app CHEMISTRY (LaunchAgent vn.giaichuoihuuco.app
dùng cổng 8000, cũng là cổng mặc định của máy chủ AI phòng lab server/ai/main.py).

    python -m server.ai.co_che.may_chu                 → http://127.0.0.1:8010/co-che/...
    VERA_CO_CHE_HOST=0.0.0.0 VERA_CO_CHE_PORT=8010     (cho iPad/Windows trong mạng LAN gọi tới)

Trước khi tạo app, nạp .env ở gốc kho (đọc tay, không cần python-dotenv) CHỈ các biến BIEN_ENV; biến đã có trong môi trường
được giữ nguyên; bỏ qua khi chạy kiểm thử (unittest/pytest, VERA_KHONG_NAP_ENV=1). ANTHROPIC_API_KEY (Claude, gọi trước) và GEMINI_API_KEY (dự phòng) dùng cho POST /co-che/giai-de.

Chỉ phục vụ dữ liệu đã dựng (gói mã hóa) và lời giải thích; kho mẫu, tư duy vẽ, bộ đo và RAG nằm ở máy này.
"""
from __future__ import annotations

import os
import sys
from pathlib import Path

CONG_MAC_DINH = 8010
BIEN_ENV = ("ANTHROPIC_API_KEY", "ANTHROPIC_AUTH_TOKEN", "CLAUDE_MODEL", "CLAUDE_MODEL_CO_CHE", "CLAUDE_EFFORT_CO_CHE", "GEMINI_API_KEY",
            "GEMINI_MODEL_CO_CHE", "VERA_GIAI_DE_CHO")


def nap_env(tep: Path | None = None, bien: tuple[str, ...] = BIEN_ENV) -> list[str]:
    """Đọc KEY=giá trị trong .env (bỏ dòng trống, #, 'export ', dấu nháy quanh giá trị); chỉ đặt biến trong `bien` mà môi trường
    chưa có. Trả tên các biến đã nạp (không trả giá trị)."""
    tep = tep or Path(__file__).resolve().parents[3] / ".env"
    try:
        dong = tep.read_text(encoding="utf-8").splitlines()
    except OSError:
        return []
    da_nap = []
    for d in dong:
        d = d.strip()
        if not d or d.startswith("#") or "=" not in d:
            continue
        if d.startswith("export "):
            d = d[7:].lstrip()
        k, v = d.split("=", 1)
        k, v = k.strip(), v.strip()
        if len(v) >= 2 and v[0] == v[-1] and v[0] in "'\"":
            v = v[1:-1]
        elif " #" in v:
            v = v.split(" #", 1)[0].rstrip()
        if k in bien and v and not os.environ.get(k):
            os.environ[k] = v
            da_nap.append(k)
    return da_nap


def _dang_kiem_thu() -> bool:
    return bool(os.environ.get("VERA_KHONG_NAP_ENV")) or "unittest" in (sys.argv[0] if sys.argv else "") or "pytest" in sys.modules


if not _dang_kiem_thu():          # kiểm thử không được thấy khóa thật (không gọi mạng)
    nap_env()

from fastapi import FastAPI  # noqa: E402

from .api import router  # noqa: E402

app = FastAPI(title="VERA – máy chủ cơ chế phản ứng")
app.include_router(router)


def main() -> None:
    import uvicorn
    uvicorn.run(app, host=os.environ.get("VERA_CO_CHE_HOST", "127.0.0.1"), port=int(os.environ.get("VERA_CO_CHE_PORT", CONG_MAC_DINH)))


if __name__ == "__main__":
    main()
