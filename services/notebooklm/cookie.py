"""Chuẩn hoá + kiểm tra cookie Google cho NotebookLM – chạy ở BACKEND trước mọi bước khác.

normalize_cookies(input) nhận được:
  1. chuỗi header  "SID=...; HSID=...; SSID=..."
  2. chuỗi có tiền tố thừa  "cookie: SID=..." / "Cookie: SID=..." (kể cả dán cả khối Request Headers, lệnh curl)
  3. JSON Cookie-Editor / EditThisCookie  [{"name", "value", "domain", ...}] – chỉ lấy cookie có domain *.google.com
  4. JSON object  {"SID": "...", "HSID": "..."}
và tự: bỏ khoảng trắng / xuống dòng / ngoặc kép thừa ở hai đầu, nối lại dòng bị ngắt giữa chừng khi copy,
cookie trùng tên giữ giá trị cuối. Trả (chuỗi "tên=giá_trị; …", {tên: giá_trị}).

BẢO MẬT: module này KHÔNG BAO GIỜ ghi / trả / đưa vào thông báo lỗi giá trị cookie – chỉ tên cookie và số lượng.
"""
from __future__ import annotations

import json
import re
from typing import Any

from .client import LoiNotebookLM

TOI_DA_KY_TU = 20_000
BAT_BUOC = ("SID", "HSID", "SSID", "APISID", "SAPISID")
MOT_TRONG = ("__Secure-1PSID", "__Secure-3PSID")       # cần ít nhất một trong hai
TEN_THIEU_PSID = "__Secure-1PSID hoặc __Secure-3PSID"

THONG_BAO_THIEU = "Cookie thiếu: {ds}. Hãy copy TOÀN BỘ giá trị dòng 'cookie' trong Request Headers, hoặc dùng Cookie-Editor → Export → JSON."
THONG_BAO_MOT_GIA_TRI = "Bạn mới dán giá trị của một cookie. Cần dán cả chuỗi dạng tên=giá_trị; tên=giá_trị."
THONG_BAO_HET_HAN = "Cookie đã hết hạn hoặc không hợp lệ. Hãy đăng nhập lại NotebookLM rồi lấy cookie mới."
THONG_BAO_EMAIL = "Email không hợp lệ."

_TEN_HOP_LE = re.compile(r"^[A-Za-z0-9!#$%&'*+\-.^_`|~]+$")          # ký tự được phép trong tên cookie (RFC 6265 token)
_DAU_CAP = re.compile(r"^[A-Za-z0-9!#$%&'*+\-.^_`|~]+=")             # dòng bắt đầu bằng "tên=" → cặp mới, không phải phần nối
_DONG_HEADER = re.compile(r"^\s*[A-Za-z][A-Za-z0-9-]*\s*:\s", re.M)  # "Host: …", "user-agent: …" (dán cả khối Request Headers)
_DONG_COOKIE = re.compile(r"^\s*cookie\s*:\s*(.*)$", re.I | re.M)
_CURL_HEADER = re.compile(r"""(?:-H|--header)\s+(['"])\s*cookie\s*:\s*(.*?)\1""", re.I | re.S)
_CURL_COOKIE = re.compile(r"""(?:-b|--cookie)\s+(['"])(.*?)\1""", re.S)
_THUOC_TINH = {"path", "domain", "expires", "max-age", "secure", "httponly", "samesite", "priority", "partitioned", "sameparty", "version", "comment"}
_EMAIL = re.compile(r"^[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}$")


class LoiCookie(LoiNotebookLM):
    """Lỗi cookie có mã chuẩn (MISSING_COOKIES, COOKIE_VALUE_ONLY, …), thông báo tiếng Việt và danh sách cookie còn thiếu."""

    def __init__(self, message: str, code: str, missing: list[str] | None = None, status_code: int = 422):
        super().__init__(message, code=code, status_code=status_code)
        self.missing = missing or []


def _bo_ngoac(s: str) -> str:
    s = s.strip()
    while len(s) >= 2 and s[0] == s[-1] and s[0] in "\"'`":
        s = s[1:-1].strip()
    return s


def _la_google(domain: Any) -> bool:
    d = str(domain or "").strip().lstrip(".").lower()
    return not d or d == "google.com" or d.endswith(".google.com")      # notebooklm.google.com, notebook.google.com, .google.com…


def _tu_json(du_lieu: Any) -> dict[str, str]:
    kq: dict[str, str] = {}
    if isinstance(du_lieu, dict) and isinstance(du_lieu.get("cookies"), list):
        du_lieu = du_lieu["cookies"]
    if isinstance(du_lieu, dict) and "name" in du_lieu and "value" in du_lieu:
        du_lieu = [du_lieu]
    if isinstance(du_lieu, list):                                        # Cookie-Editor / EditThisCookie
        for muc in du_lieu:
            if not isinstance(muc, dict) or "name" not in muc or "value" not in muc:
                continue
            if not _la_google(muc.get("domain", muc.get("host", ""))):
                continue
            ten = str(muc["name"]).strip()
            if _TEN_HOP_LE.match(ten):
                kq[ten] = str(muc["value"]).strip()
    elif isinstance(du_lieu, dict):                                      # {"SID": "...", …}
        for ten, gt in du_lieu.items():
            ten = str(ten).strip()
            if _TEN_HOP_LE.match(ten) and isinstance(gt, (str, int, float)) and not isinstance(gt, bool):
                kq[ten] = str(gt).strip()
    return kq


def _noi_dong(s: str) -> str:
    """Nối các dòng bị ngắt khi copy: dòng mới bắt đầu bằng "tên=" là cặp mới; còn lại là phần tiếp của giá trị trước."""
    dong = [x.strip() for x in s.split("\n") if x.strip()]
    if not dong:
        return ""
    kq = dong[0]
    for x in dong[1:]:
        kq += ("; " + x) if (kq.rstrip().endswith(";") or _DAU_CAP.match(x)) else x
    return kq


def normalize_cookies(dau_vao: Any) -> tuple[str, dict[str, str]]:
    """Mọi định dạng → ("tên=giá_trị; …", {tên: giá_trị}). Lỗi → LoiCookie (không chứa giá trị cookie)."""
    if isinstance(dau_vao, (list, dict)):
        cookies = _tu_json(dau_vao)
    else:
        s = str(dau_vao or "").replace("\r\n", "\n").replace("\r", "\n")
        if len(s) > TOI_DA_KY_TU:
            raise LoiCookie(f"Chuỗi cookie quá dài (tối đa {TOI_DA_KY_TU:,} ký tự).".replace(",", "."), "COOKIE_TOO_LONG")
        s = _bo_ngoac(s)
        if not s:
            raise LoiCookie("Chưa dán cookie.", "EMPTY_COOKIE")
        if s[0] in "[{":
            try:
                cookies = _tu_json(json.loads(s))
            except ValueError as e:
                raise LoiCookie("JSON cookie bị lỗi định dạng – hãy Export lại từ Cookie-Editor (Export → JSON) rồi dán nguyên văn.",
                                "INVALID_COOKIE_JSON") from e
        else:
            m = _CURL_HEADER.search(s) or _CURL_COOKIE.search(s)
            if m:                                                          # lệnh curl (Copy as cURL)
                s = m.group(2)
            elif len(_DONG_HEADER.findall(s)) >= 2 and _DONG_COOKIE.search(s):   # dán cả khối Request Headers → chỉ lấy dòng cookie
                s = "; ".join(x.strip() for x in _DONG_COOKIE.findall(s))
            s = re.sub(r"^\s*cookie\s*:\s*", "", _bo_ngoac(s), flags=re.I)   # bỏ tiền tố "cookie:" / "Cookie:"
            s = _noi_dong(s)
            cookies = {}
            for phan in s.split(";"):
                phan = phan.strip()
                if "=" not in phan:
                    continue
                ten, gt = phan.split("=", 1)
                ten, gt = ten.strip(), gt.strip()
                if not ten or ten.lower() in _THUOC_TINH or not _TEN_HOP_LE.match(ten):
                    continue
                cookies[ten] = gt                                          # trùng tên → giá trị cuối thắng
            if not cookies and "=" not in s:
                raise LoiCookie(THONG_BAO_MOT_GIA_TRI, "COOKIE_VALUE_ONLY")
    if not cookies:
        raise LoiCookie("Không đọc được cookie nào. Hãy dán chuỗi dạng tên=giá_trị; tên=giá_trị hoặc JSON từ Cookie-Editor.", "INVALID_COOKIE_FORMAT")
    return "; ".join(f"{k}={v}" for k, v in cookies.items()), cookies


def cookie_thieu(cookies: dict[str, str]) -> list[str]:
    thieu = [t for t in BAT_BUOC if not cookies.get(t)]
    if not any(cookies.get(t) for t in MOT_TRONG):
        thieu.append(TEN_THIEU_PSID)
    return thieu


def chuan_hoa_va_kiem_tra(dau_vao: Any) -> tuple[str, dict[str, str]]:
    """normalize_cookies + bắt buộc đủ SID, HSID, SSID, APISID, SAPISID và __Secure-1PSID hoặc __Secure-3PSID."""
    chuoi, cookies = normalize_cookies(dau_vao)
    thieu = cookie_thieu(cookies)
    if thieu:
        raise LoiCookie(THONG_BAO_THIEU.format(ds=", ".join(thieu)), "MISSING_COOKIES", missing=thieu)
    return chuoi, cookies


def kiem_tra_email(email: str | None) -> str:
    """Email không bắt buộc: trống → "" (lấy từ Google sau khi kết nối); có nhập thì phải đúng định dạng."""
    e = (email or "").strip()
    if e and not _EMAIL.match(e):
        raise LoiCookie(THONG_BAO_EMAIL, "INVALID_EMAIL")
    return e.lower()
