"""Cập nhật app máy tính CHEMISTRY: thông tin bản phát hành mới nhất cho app tự kiểm tra (desktop/cap_nhat.js).

GET /api/app/phien-ban (công khai, chỉ đọc) trả nội dung config/phien_ban_app.json sau khi kiểm tra chặt:
  {"phien_ban": "1.0.1", "ngay": "2026-10-25", "ghi_chu": ["…"], "bat_buoc_tu": null | "1.0.0",
   "tai_ve": {"mac-arm64": {"url": "https://…", "sha256": "<64 hex>", "kich_thuoc": 242263826}, "win-x64": {…}}}
App so sánh `phien_ban` với bản đang chạy; nếu mới hơn thì tải `tai_ve` đúng nền tảng và KIỂM TRA SHA-256 trước khi mở bộ cài.
Người phát hành tạo file bằng `tools/phat_hanh_app.py` (tự tính sha256 + kích thước). Không có file / dữ liệu sai → không báo bản mới.
"""
from __future__ import annotations

import json
import re
from pathlib import Path
from urllib.parse import urlparse

from fastapi import APIRouter, Response

router = APIRouter()

TEP_PHAT_HANH = Path(__file__).resolve().parent.parent / "config" / "phien_ban_app.json"
# Chỉ cho tải từ các nơi này (khớp đúng hoặc là tên miền con); khớp danh sách HOST_HOP_LE trong desktop/cap_nhat.js
HOST_TAI_VE_HOP_LE = ("github.com", "githubusercontent.com", "giai-chuoi-huu-co.onrender.com")
NEN_TANG = ("mac-arm64", "mac-x64", "win-x64")
_MAU_PHIEN_BAN = re.compile(r"^\d+\.\d+\.\d+(-[0-9A-Za-z.]+)?$")
_MAU_SHA256 = re.compile(r"^[0-9a-fA-F]{64}$")


def host_hop_le(host: str) -> bool:
    host = (host or "").lower()
    return any(host == h or host.endswith("." + h) for h in HOST_TAI_VE_HOP_LE)


def kiem_tra_ban_phat_hanh(d: object) -> tuple[dict | None, str]:
    """→ (bản phát hành đã chuẩn hoá, "") hoặc (None, lý do lỗi)."""
    if not isinstance(d, dict):
        return None, "không phải đối tượng JSON"
    pb = d.get("phien_ban")
    if not isinstance(pb, str) or not _MAU_PHIEN_BAN.match(pb):
        return None, "phien_ban phải dạng x.y.z"
    bat_buoc = d.get("bat_buoc_tu")
    if bat_buoc is not None and (not isinstance(bat_buoc, str) or not _MAU_PHIEN_BAN.match(bat_buoc)):
        return None, "bat_buoc_tu phải dạng x.y.z hoặc null"
    ghi_chu = d.get("ghi_chu", [])
    if not isinstance(ghi_chu, list) or not all(isinstance(x, str) for x in ghi_chu):
        return None, "ghi_chu phải là danh sách chuỗi"
    tai_ve_vao = d.get("tai_ve", {})
    if not isinstance(tai_ve_vao, dict):
        return None, "tai_ve phải là đối tượng"
    tai_ve: dict[str, dict] = {}
    for nen, t in tai_ve_vao.items():
        if nen not in NEN_TANG:
            return None, f"nền tảng '{nen}' không hỗ trợ (chỉ {', '.join(NEN_TANG)})"
        if not isinstance(t, dict):
            return None, f"tai_ve.{nen} sai dạng"
        u = urlparse(str(t.get("url", "")))
        if u.scheme != "https" or not host_hop_le(u.hostname or ""):
            return None, f"tai_ve.{nen}.url phải là https và thuộc {', '.join(HOST_TAI_VE_HOP_LE)}"
        if not _MAU_SHA256.match(str(t.get("sha256", ""))):
            return None, f"tai_ve.{nen}.sha256 phải là 64 ký tự hex"
        kt = t.get("kich_thuoc")
        if not isinstance(kt, int) or isinstance(kt, bool) or kt <= 0:
            return None, f"tai_ve.{nen}.kich_thuoc phải là số nguyên dương"
        tai_ve[nen] = {"url": t["url"], "sha256": str(t["sha256"]).lower(), "kich_thuoc": kt}
    return {"phien_ban": pb, "ngay": str(d.get("ngay", "")), "ghi_chu": ghi_chu, "bat_buoc_tu": bat_buoc, "tai_ve": tai_ve}, ""


def doc_ban_phat_hanh(tep: Path | None = None) -> tuple[dict | None, str]:
    tep = tep or TEP_PHAT_HANH
    try:
        d = json.loads(tep.read_text(encoding="utf-8"))
    except FileNotFoundError:
        return None, "chưa có config/phien_ban_app.json"
    except (OSError, ValueError) as e:
        return None, f"không đọc được: {e}"
    return kiem_tra_ban_phat_hanh(d)


@router.get("/api/app/phien-ban")
def phien_ban_app(response: Response):
    """Công khai: app CHEMISTRY hỏi bản mới nhất. Dữ liệu sai / chưa có → phien_ban = null (app im lặng)."""
    response.headers["Cache-Control"] = "no-store"
    ban, loi = doc_ban_phat_hanh()
    return ban if ban else {"phien_ban": None, "loi": loi}
