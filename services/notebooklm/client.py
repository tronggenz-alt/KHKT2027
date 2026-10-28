"""Client giao tiếp với Google NotebookLM không chính thức (dựa trên Cookie và Batchexecute RPC).

Mọi kết nối chỉ chạy phía server. Cookie được giải mã trong bộ nhớ, không bao giờ gửi ra trình duyệt.
Khi cookie hết hạn (Google session hết hạn hoặc bị thu hồi), client trả về mã lỗi thích hợp để hệ thống
đánh dấu tài khoản "Cần đăng nhập lại" thay vì làm gián đoạn ứng dụng.
"""
from __future__ import annotations

import json
import logging
import re
import urllib.parse
from dataclasses import asdict, dataclass, field
from typing import Any

import httpx

log = logging.getLogger(__name__)

NOTEBOOKLM_BASE_URL = "https://notebooklm.google.com"
BATCHEXECUTE_URL = "https://notebooklm.google.com/_/LabsTailwindUi/data/batchexecute"

# Các RPC IDs đã biết của Google NotebookLM
RPC_LIST_NOTEBOOKS = "wXbhsf"       # Danh sách notebooks
RPC_GET_NOTEBOOK = "rLM1te"         # Chi tiết notebook & nguồn
RPC_QUERY_NOTEBOOK = "kFdrF"        # Đặt câu hỏi trong notebook
RPC_USER_PROFILE = "hT52We"         # Thông tin người dùng


class LoiNotebookLM(Exception):
    """Lỗi khi kết nối hoặc tương tác với Google NotebookLM."""

    def __init__(self, message: str, code: str = "loi_ket_noi", status_code: int = 400):
        super().__init__(message)
        self.message = message
        self.code = code
        self.status_code = status_code


class LoiCookieHetHan(LoiNotebookLM):
    """Cookie Google đã hết hạn hoặc không hợp lệ, cần đăng nhập lại."""

    def __init__(self, message: str = "Cookie Google đã hết hạn. Vui lòng đăng nhập lại."):
        super().__init__(message, code="cookie_het_han", status_code=401)


@dataclass
class SourceInfo:
    id: str
    title: str
    type: str = "text"              # pdf | web | youtube | doc | text
    url: str = ""
    word_count: int = 0
    snippet: str = ""
    created_at: str = ""


@dataclass
class NoteInfo:
    id: str
    title: str
    content: str = ""
    created_at: str = ""


@dataclass
class NotebookInfo:
    id: str
    title: str
    description: str = ""
    sources_count: int = 0
    notes_count: int = 0
    updated_at: str = ""
    sources: list[SourceInfo] = field(default_factory=list)
    notes: list[NoteInfo] = field(default_factory=list)


@dataclass
class QueryResult:
    answer: str
    citations: list[dict[str, Any]] = field(default_factory=list)


def parse_cookie_input(raw: str) -> str:
    """Chuẩn hoá cookie (chuỗi header, "cookie: …", JSON Cookie-Editor / object, curl, dòng bị ngắt) → "tên=giá_trị; …".
    Giữ tên hàm cũ cho tương thích; logic nằm ở services/notebooklm/cookie.py. Không đọc được → trả chuỗi đã bỏ khoảng trắng."""
    from .cookie import normalize_cookies
    try:
        return normalize_cookies(raw)[0]
    except LoiNotebookLM:
        return (raw or "").strip()


class NotebookLMClient:
    """Client giao tiếp với NotebookLM của Google bằng cookie."""

    def __init__(self, cookie_str: str, timeout: float = 20.0):
        self.cookie_str = parse_cookie_input(cookie_str)
        self.timeout = timeout
        self._xsrf_token: str | None = None
        self._user_email: str = ""
        self.ly_do = ""   # lý do lần kiểm tra cuối thất bại: thieu | het_han | tu_choi | http | mang | khac
        self._user_name: str = ""
        self._user_picture: str = ""

    def _headers(self) -> dict[str, str]:
        return {
            "User-Agent": (
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 "
                "(KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36"
            ),
            "Cookie": self.cookie_str,
            "X-Same-Domain": "1",
            "Origin": NOTEBOOKLM_BASE_URL,
            "Referer": f"{NOTEBOOKLM_BASE_URL}/",
            "Accept-Language": "vi,en-US;q=0.9,en;q=0.8",
        }

    def kiem_tra_ket_noi(self) -> tuple[bool, str, dict[str, str]]:
        """Kiểm tra xem cookie còn hiệu lực không và lấy thông tin người dùng.
        Trả về (hop_le: bool, loi: str, thong_tin: {email, name, picture})
        """
        self.ly_do = ""
        if not self.cookie_str:
            self.ly_do = "thieu"
            return False, "Chưa cung cấp cookie Google", {}

        # Kiểm tra sự hiện diện của các cookie cốt lõi của Google
        cookie_keys = {part.split("=")[0].strip() for part in self.cookie_str.split(";") if "=" in part}
        core_keys = {"SID", "__Secure-1PSID", "__Secure-3PSID", "SAPISID"}
        if not (core_keys & cookie_keys):
            self.ly_do = "thieu"
            return False, "Thiếu cookie phiên Google quan trọng (cần ít nhất SID hoặc __Secure-1PSID)", {}

        try:
            with httpx.Client(headers=self._headers(), timeout=self.timeout, follow_redirects=True) as client:
                resp = client.get(f"{NOTEBOOKLM_BASE_URL}/")
                url_str = str(resp.url)

                # Nếu bị chuyển hướng về trang đăng nhập của Google
                if "accounts.google.com" in url_str and ("signin" in url_str or "ServiceLogin" in url_str):
                    self.ly_do = "het_han"
                    return False, "Cookie đã hết hạn hoặc phiên đăng nhập Google bị đăng xuất", {}

                if resp.status_code in (401, 403):
                    self.ly_do = "tu_choi"
                    return False, f"Google từ chối xác thực (HTTP {resp.status_code})", {}

                if resp.status_code != 200:
                    self.ly_do = "http"
                    return False, f"Không kết nối được NotebookLM (HTTP {resp.status_code})", {}

                html = resp.text
                # Trích xuất XSRF token (SNlM0e) từ HTML
                m_at = re.search(r'''"SNlM0e"\s*:\s*"([^"]+)"''', html)
                if m_at:
                    self._xsrf_token = m_at.group(1)
                else:
                    m_at2 = re.search(r'''\["SNlM0e",null,"([^"]+)"\]''', html)
                    if m_at2:
                        self._xsrf_token = m_at2.group(1)

                # Trích xuất thông tin tài khoản Google từ HTML nếu có
                profile = {}
                m_email = re.search(r'''["']([a-zA-Z0-9._%+-]+@(?:gmail\.com|[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}))["']''', html)
                if m_email:
                    profile["email"] = m_email.group(1)
                    self._user_email = profile["email"]

                m_name = re.search(r'''"display_name"\s*:\s*"([^"]+)"''', html) or re.search(r'''"name"\s*:\s*"([^"]+)"''', html)
                if m_name:
                    profile["name"] = m_name.group(1)
                    self._user_name = profile["name"]

                m_pic = re.search(r'''"(https://lh3\.googleusercontent\.com/[^"]+)"''', html)
                if m_pic:
                    profile["picture"] = m_pic.group(1)
                    self._user_picture = profile["picture"]

                return True, "", profile

        except httpx.TimeoutException:
            self.ly_do = "mang"
            return False, "Quá thời gian kết nối tới Google (timeout)", {}
        except httpx.NetworkError as e:
            self.ly_do = "mang"
            return False, f"Lỗi mạng khi kết nối Google ({type(e).__name__})", {}
        except Exception as e:  # noqa: BLE001
            log.warning("Lỗi kiểm tra kết nối NotebookLM: %s", type(e).__name__)   # không ghi chi tiết (có thể chứa header)
            self.ly_do = "khac"
            return False, f"Lỗi không xác định khi kiểm tra kết nối ({type(e).__name__})", {}

    def _lay_xsrf_token(self) -> str:
        if self._xsrf_token:
            return self._xsrf_token
        ok, msg, _ = self.kiem_tra_ket_noi()
        if not ok or not self._xsrf_token:
            raise LoiCookieHetHan(msg or "Không lấy được XSRF token từ NotebookLM")
        return self._xsrf_token

    def _call_rpc(self, rpc_id: str, payload_json_str: str) -> Any:
        """Gửi Batchexecute RPC tới Google LabsTailwindUi."""
        token = self._lay_xsrf_token()
        data_req = [[[rpc_id, payload_json_str, None, "generic"]]]
        body = {
            "f.req": json.dumps(data_req, separators=(",", ":")),
            "at": token,
        }
        headers = self._headers()
        headers["Content-Type"] = "application/x-www-form-urlencoded;charset=UTF-8"

        try:
            with httpx.Client(headers=headers, timeout=self.timeout) as client:
                res = client.post(BATCHEXECUTE_URL, data=body)
                if res.status_code in (401, 403):
                    raise LoiCookieHetHan()
                if res.status_code != 200:
                    raise LoiNotebookLM(f"RPC {rpc_id} trả về mã lỗi HTTP {res.status_code}")

                # Phân tích phản hồi batchexecute của Google:
                # Phản hồi có dạng: )]}'\n\n[...chuncks...]
                text = res.text
                if text.startswith(")]}'"):
                    text = text[4:].strip()

                lines = [line.strip() for line in text.splitlines() if line.strip()]
                for line in lines:
                    try:
                        chunk = json.loads(line)
                        if isinstance(chunk, list):
                            for item in chunk:
                                if isinstance(item, list) and len(item) >= 3 and item[0] == "wrb.fr" and item[1] == rpc_id:
                                    raw_data = item[2]
                                    if isinstance(raw_data, str):
                                        return json.loads(raw_data)
                                    return raw_data
                    except (json.JSONDecodeError, ValueError):
                        continue

                return None
        except httpx.TimeoutException as e:
            raise LoiNotebookLM("Yêu cầu tới NotebookLM bị quá thời gian (timeout)") from e
        except httpx.HTTPError as e:
            raise LoiNotebookLM(f"Lỗi giao tiếp HTTP: {e}") from e

    def list_notebooks(self) -> list[NotebookInfo]:
        """Liệt kê tất cả notebooks của tài khoản."""
        try:
            raw = self._call_rpc(RPC_LIST_NOTEBOOKS, "[]")
            if not raw or not isinstance(raw, list):
                # Fallback: phân tích từ HTML trang chủ nếu RPC chưa trả về đúng
                return self._parse_notebooks_from_home()

            notebooks = []
            # Duyệt qua các phần tử trả về từ RPC wXbhsf
            for item in raw[0] if len(raw) > 0 and isinstance(raw[0], list) else raw:
                if isinstance(item, list) and len(item) >= 2 and isinstance(item[0], str):
                    nb_id = item[0]
                    nb_title = str(item[1]) if item[1] else "Notebook không tên"
                    nb_desc = str(item[2]) if len(item) > 2 and item[2] else ""
                    notebooks.append(NotebookInfo(
                        id=nb_id,
                        title=nb_title,
                        description=nb_desc,
                        sources_count=0,
                        notes_count=0,
                    ))
            return notebooks or self._parse_notebooks_from_home()
        except LoiCookieHetHan:
            raise
        except Exception as e:
            log.warning("list_notebooks RPC thất bại, thử fallback HTML: %s", e)
            return self._parse_notebooks_from_home()

    def _parse_notebooks_from_home(self) -> list[NotebookInfo]:
        """Phân tích danh sách notebook được nhúng sẵn trong HTML trang chủ."""
        try:
            with httpx.Client(headers=self._headers(), timeout=self.timeout) as client:
                res = client.get(f"{NOTEBOOKLM_BASE_URL}/")
                if res.status_code in (401, 403):
                    raise LoiCookieHetHan()
                html = res.text
                notebooks = []
                # Tìm các liên kết /notebook/
                found_ids = set()
                matches = re.finditer(r'''/notebook/([a-zA-Z0-9_-]{10,})''', html)
                for m in matches:
                    nid = m.group(1)
                    if nid not in found_ids:
                        found_ids.add(nid)
                        notebooks.append(NotebookInfo(id=nid, title=f"Notebook ({nid[:8]})"))
                return notebooks
        except LoiCookieHetHan:
            raise
        except Exception as e:
            log.error("Không thể đọc danh sách notebook từ trang chủ: %s", e)
            return []

    def get_notebook(self, notebook_id: str) -> NotebookInfo:
        """Lấy chi tiết một notebook bao gồm danh sách nguồn tài liệu và ghi chú."""
        payload = json.dumps([notebook_id])
        try:
            raw = self._call_rpc(RPC_GET_NOTEBOOK, payload)
            title = f"Notebook {notebook_id[:8]}"
            sources: list[SourceInfo] = []
            notes: list[NoteInfo] = []

            if raw and isinstance(raw, list):
                if len(raw) > 0 and isinstance(raw[0], list) and len(raw[0]) > 1:
                    title = str(raw[0][1]) if raw[0][1] else title

                # Phân tích nguồn (sources) nếu có trong payload
                for s in self._extract_sources_from_raw(raw):
                    sources.append(s)

            return NotebookInfo(
                id=notebook_id,
                title=title,
                sources_count=len(sources),
                notes_count=len(notes),
                sources=sources,
                notes=notes,
            )
        except Exception as e:
            log.warning("get_notebook RPC thất bại: %s", e)
            return NotebookInfo(id=notebook_id, title=f"Notebook ({notebook_id[:8]})")

    def _extract_sources_from_raw(self, raw: Any) -> list[SourceInfo]:
        """Trích xuất danh sách tài liệu từ cấu trúc phản hồi của NotebookLM."""
        sources = []
        try:
            # Thuật toán tìm kiếm sâu các phần tử có định dạng source
            def scan(obj):
                if isinstance(obj, list):
                    if len(obj) >= 3 and isinstance(obj[0], str) and isinstance(obj[1], str) and len(obj[0]) > 10:
                        # Nhận diện cấu trúc source [id, title, type/url...]
                        sid, stitle = obj[0], obj[1]
                        stype = "text"
                        low = stitle.lower()
                        if ".pdf" in low:
                            stype = "pdf"
                        elif "youtube.com" in low or "youtu.be" in low:
                            stype = "youtube"
                        elif "http://" in low or "https://" in low:
                            stype = "web"
                        sources.append(SourceInfo(id=sid, title=stitle, type=stype))
                    for item in obj:
                        scan(item)
                elif isinstance(obj, dict):
                    for v in obj.values():
                        scan(v)

            scan(raw)
        except Exception:  # noqa: BLE001
            pass
        return sources

    def query(self, notebook_id: str, question: str) -> QueryResult:
        """Đặt câu hỏi cho notebook và nhận phản hồi tổng hợp kèm trích dẫn."""
        payload = json.dumps([notebook_id, question, None, None])
        try:
            raw = self._call_rpc(RPC_QUERY_NOTEBOOK, payload)
            answer = ""
            citations = []
            if raw and isinstance(raw, list):
                # Tìm chuỗi phản hồi văn bản trong kết quả
                for item in raw:
                    if isinstance(item, str) and len(item) > 10 and not item.startswith("http"):
                        answer = item
                        break
                    if isinstance(item, list):
                        for sub in item:
                            if isinstance(sub, str) and len(sub) > 20:
                                answer = sub
                                break

            if not answer:
                answer = f"NotebookLM đã nhận câu hỏi: '{question}'. Chưa có phản hồi từ máy chủ."

            return QueryResult(answer=answer, citations=citations)
        except Exception as e:
            log.error("Query NotebookLM thất bại: %s", e)
            raise LoiNotebookLM(f"Không thể truy vấn NotebookLM: {e}") from e
