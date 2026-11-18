"""Máy chủ cơ chế VERA (FastAPI), chạy riêng ở cổng 8010 để không trùng app CHEMISTRY (LaunchAgent vn.giaichuoihuuco.app
dùng cổng 8000, cũng là cổng mặc định của máy chủ AI phòng lab server/ai/main.py).

    python -m server.ai.co_che.may_chu                 → http://127.0.0.1:8010/co-che/...
    VERA_CO_CHE_HOST=0.0.0.0 VERA_CO_CHE_PORT=8010     (cho iPad/Windows trong mạng LAN gọi tới)

Chỉ phục vụ dữ liệu đã dựng (gói mã hóa) và lời giải thích; kho mẫu, tư duy vẽ, bộ đo và RAG nằm ở máy này.
"""
from __future__ import annotations

import os

from fastapi import FastAPI

from .api import router

CONG_MAC_DINH = 8010

app = FastAPI(title="VERA – máy chủ cơ chế phản ứng")
app.include_router(router)


def main() -> None:
    import uvicorn
    uvicorn.run(app, host=os.environ.get("VERA_CO_CHE_HOST", "127.0.0.1"), port=int(os.environ.get("VERA_CO_CHE_PORT", CONG_MAC_DINH)))


if __name__ == "__main__":
    main()
