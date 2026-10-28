"""Phòng thí nghiệm Hoá học ảo (/hoa-hoc/phong-thi-nghiem): công tắc bản Unity 3D cho mọi người + trạng thái bản build.

- Công tắc lưu ở app_settings khoá "phong_thi_nghiem" = {"unity_bat": false} (mặc định TẮT; có trong app.db nên được sao lưu).
- Bản build Unity WebGL đặt ở THU_MUC_BUILD (frontend/unity-lab/Build/): đủ loader + data + framework + wasm mới coi là "sẵn sàng".
  Unity xuất tên dạng <Ten>.loader.js, <Ten>.data[.gz|.br|.unityweb], <Ten>.framework.js[...], <Ten>.wasm[...].
- THU_MUC_BUILD là biến của module để test monkeypatch.
"""
from __future__ import annotations

import json
from pathlib import Path
from typing import Any

from . import db
from .config import FRONTEND_DIR

KHOA = "phong_thi_nghiem"
THU_MUC_BUILD: Path = FRONTEND_DIR / "unity-lab" / "Build"
URL_BUILD = "/static/unity-lab/Build/"
_DUOI_NEN = ("", ".gz", ".br", ".unityweb")


def doc_cau_hinh() -> dict[str, Any]:
    try:
        d = json.loads(db.cai_dat(KHOA, "{}") or "{}")
    except ValueError:
        d = {}
    return {"unity_bat": bool(d.get("unity_bat", False))}


def dat_unity(bat: bool) -> dict[str, Any]:
    d = doc_cau_hinh()
    d["unity_bat"] = bool(bat)
    db.dat_cai_dat(KHOA, json.dumps(d, ensure_ascii=False))
    return d


def tep_build() -> dict[str, str] | None:
    """Tên 4 tệp của bản build (đường dẫn web) nếu đủ, ngược lại None."""
    thu_muc = THU_MUC_BUILD
    if not thu_muc.is_dir():
        return None
    for loader in sorted(thu_muc.glob("*.loader.js")):
        ten = loader.name[: -len(".loader.js")]
        tep = {"loader": loader.name}
        for khoa, goc in (("data", f"{ten}.data"), ("framework", f"{ten}.framework.js"), ("wasm", f"{ten}.wasm")):
            co = next((goc + d for d in _DUOI_NEN if (thu_muc / (goc + d)).is_file()), None)
            if not co:
                break
            tep[khoa] = co
        else:
            return {k: URL_BUILD + v for k, v in tep.items()}
    return None


def unity_san_sang() -> bool:
    return tep_build() is not None


def dung_luong_build() -> int:
    """Tổng dung lượng (byte) thư mục build – 0 nếu chưa có."""
    if not THU_MUC_BUILD.is_dir():
        return 0
    return sum(p.stat().st_size for p in THU_MUC_BUILD.rglob("*") if p.is_file())


def trang_thai_admin() -> dict[str, Any]:
    return {**doc_cau_hinh(), "unity_san_sang": unity_san_sang(), "dung_luong_build": dung_luong_build(),
            "thu_muc_build": "frontend/unity-lab/Build/", "huong_dan": "unity/README.md"}


def cau_hinh_cho(la_admin: bool) -> dict[str, Any]:
    """Cấu hình gửi trang Phòng thí nghiệm. Chỉ lộ đường dẫn bản build khi người này được phép dùng (đã bật, hoặc là admin)."""
    ch = doc_cau_hinh()
    tep = tep_build()
    kq: dict[str, Any] = {"unity_bat": ch["unity_bat"], "unity_san_sang": tep is not None, "la_admin": la_admin}
    if tep and (ch["unity_bat"] or la_admin):
        kq["unity_tep"] = tep
    return kq


# ==============================================================================
# ỨNG DỤNG MÁY TÍNH STANDALONE (PhongLabHoa3D macOS & Windows)
# ==============================================================================

KHOA_UNG_DUNG = "ung_dung_lab"


def doc_ung_dung() -> dict[str, Any]:
    """Cấu hình ứng dụng máy tính lưu trong app_settings."""
    try:
        d = json.loads(db.cai_dat(KHOA_UNG_DUNG, "{}") or "{}")
    except ValueError:
        d = {}
    return {
        "bat": bool(d.get("bat", False)),
        "url_mac": str(d.get("url_mac", "")),
        "url_win": str(d.get("url_win", "")),
        "phien_ban": str(d.get("phien_ban", "1.0.0")),
    }


def dat_ung_dung(bat: bool, url_mac: str = "", url_win: str = "", phien_ban: str = "1.0.0") -> dict[str, Any]:
    """Admin cập nhật cài đặt ứng dụng máy tính."""
    d = {
        "bat": bool(bat),
        "url_mac": (url_mac or "").strip(),
        "url_win": (url_win or "").strip(),
        "phien_ban": (phien_ban or "1.0.0").strip(),
    }
    db.dat_cai_dat(KHOA_UNG_DUNG, json.dumps(d, ensure_ascii=False))
    return d


def ung_dung_cho_nguoi_dung() -> dict[str, Any]:
    """Thông tin ứng dụng trả cho người dùng. Nếu đang tắt (bat=False), không lộ link tải."""
    d = doc_ung_dung()
    if not d["bat"]:
        return {"bat": False, "url_mac": "", "url_win": "", "phien_ban": ""}
    return d

