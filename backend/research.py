"""Bước 2 – Tra cứu: PubChem PUG REST + Playwright điều khiển Chrome đọc các trang được phép.

Claude được cấp 2 công cụ (tool-use): pubchem_lookup và web_research.
"""
from __future__ import annotations

import json
import logging
import re
import time
import urllib.parse
from concurrent.futures import ThreadPoolExecutor
from dataclasses import dataclass, field
from typing import Any, Callable

import requests

from .llm import GoiCongCu, PhienHoiThoai
from .schemas import PhanTichDe

log = logging.getLogger(__name__)

Emit = Callable[..., None]


def co_google_chrome() -> bool:
    """Kiểm tra máy tính hiện tại có cài đặt Google Chrome hay không (dùng cho Demo / Tra cứu có đầu)."""
    import os
    import shutil
    import platform
    from pathlib import Path
    hdt = platform.system()
    if hdt == "Darwin":
        if Path("/Applications/Google Chrome.app").exists():
            return True
        return shutil.which("google-chrome") is not None or shutil.which("chrome") is not None
    elif hdt == "Windows":
        for p in [
            r"C:\Program Files\Google\Chrome\Application\chrome.exe",
            r"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
            os.path.expandvars(r"%LOCALAPPDATA%\Google\Chrome\Application\chrome.exe"),
        ]:
            if Path(p).exists():
                return True
        return shutil.which("chrome") is not None
    return shutil.which("google-chrome") is not None or shutil.which("chrome") is not None

# ---------------------------------------------------------------- PubChem
PUG = "https://pubchem.ncbi.nlm.nih.gov/rest/pug/compound"
_PROPS = "Title,IUPACName,MolecularFormula,SMILES,ConnectivitySMILES"
_FORMULA_RE = re.compile(r"^(?:[A-Z][a-z]?\d*)+$")
_pubchem_cache: dict[str, dict] = {}


def _pubchem_get(url: str) -> list[dict] | None:
    r = requests.get(url, timeout=12, headers={"User-Agent": "GiaiChuoiHuuCo/1.0"})
    if r.status_code == 404:
        return None
    r.raise_for_status()
    return r.json().get("PropertyTable", {}).get("Properties", [])


def _chuan_hoa_pubchem(p: dict) -> dict:
    cid = p.get("CID")
    return {
        "cid": cid,
        "ten": p.get("Title", ""),
        "iupac": p.get("IUPACName", ""),
        "cong_thuc_phan_tu": p.get("MolecularFormula", ""),
        "smiles": p.get("SMILES") or p.get("IsomericSMILES") or p.get("CanonicalSMILES") or p.get("ConnectivitySMILES", ""),
        "url": f"https://pubchem.ncbi.nlm.nih.gov/compound/{cid}" if cid else "",
    }


def pubchem_lookup(ten_hoac_cong_thuc: str) -> dict:
    """Tra PubChem theo tên (ưu tiên tên tiếng Anh) hoặc công thức phân tử."""
    q = (ten_hoac_cong_thuc or "").strip()
    if not q:
        return {"ok": False, "thong_bao": "Thiếu tên hoặc công thức."}
    key = q.lower()
    if key in _pubchem_cache:
        return _pubchem_cache[key]
    try:
        props = _pubchem_get(f"{PUG}/name/{urllib.parse.quote(q, safe='')}/property/{_PROPS}/JSON")
        if props:
            kq = {"ok": True, "truy_van": q, **_chuan_hoa_pubchem(props[0])}
        elif _FORMULA_RE.match(q):
            props = _pubchem_get(f"{PUG}/fastformula/{q}/property/{_PROPS}/JSON?MaxRecords=5")
            if props:
                kq = {
                    "ok": True,
                    "truy_van": q,
                    "thong_bao": "Tra theo công thức phân tử, có nhiều đồng phân – hãy chọn đúng chất.",
                    "ung_vien": [_chuan_hoa_pubchem(p) for p in props],
                }
            else:
                kq = {"ok": False, "truy_van": q, "thong_bao": "PubChem không có công thức này."}
        else:
            kq = {"ok": False, "truy_van": q, "thong_bao": "PubChem không tìm thấy tên này (hãy thử tên tiếng Anh)."}
    except requests.RequestException as e:
        log.warning("PubChem lỗi với %r: %s", q, e)
        return {"ok": False, "truy_van": q, "thong_bao": f"Lỗi kết nối PubChem: {e.__class__.__name__}"}
    _pubchem_cache[key] = kq
    return kq


# ---------------------------------------------------------------- Chrome (Playwright)
ALLOWED_HOSTS = {"vi.wikipedia.org", "en.wikipedia.org", "pubchem.ncbi.nlm.nih.gov", "chem.libretexts.org"}
NGUON = {
    "vi.wikipedia": ("https://vi.wikipedia.org/", "#searchInput"),
    "en.wikipedia": ("https://en.wikipedia.org/", "#searchInput"),
    "pubchem": ("https://pubchem.ncbi.nlm.nih.gov/", "#pubchem-quick-search"),
    "libretexts": ("https://chem.libretexts.org/", "input[type=search], input[name=q], input[name=query]"),
}
MAX_TRANG = 8          # tối đa số lần tải trang cho một bài
MAX_TRANG_HSG = 14     # chế độ "Đề khó (HSG)": cần tra nhiều phản ứng có tên hơn
TIMEOUT_TRANG = 20_000  # ms cho mỗi lần tải trang
TIMEOUT_CAU_HOI = 60    # giây cho mỗi lần web_research

_JS_TRICH_DOAN = """
() => {
  const root = document.querySelector('#mw-content-text .mw-parser-output')
            || document.querySelector('main') || document.body;
  root.querySelectorAll('.chemf').forEach(c => {
    c.replaceWith(document.createTextNode((c.textContent || '').replace(/\\s+/g, '')));
  });
  root.querySelectorAll('.mwe-math-element').forEach(m => {
    const img = m.querySelector('img');
    let t = img ? (img.getAttribute('alt') || '') : (m.textContent || '');
    t = t.replace(/\\\\(displaystyle|mathrm|mathrel|ce|text|rm|,|;|!)/g, ' ')
         .replace(/\\\\(longrightarrow|rightarrow|xrightarrow|to)/g, '→')
         .replace(/\\\\(rightleftharpoons|leftrightarrows)/g, '⇌').replace(/\\\\equiv/g, '≡')
         .replace(/[{}_^]/g, '').replace(/\\s+/g, ' ').trim();
    const inline = m.parentElement && /^(P|LI|DD|TD|SPAN|A|B|I|EM|STRONG)$/.test(m.parentElement.tagName);
    if (inline) { m.replaceWith(document.createTextNode(' ' + t + ' ')); }
    else { const p = document.createElement('p'); p.textContent = t; m.replaceWith(p); }
  });
  const out = [];
  let h2 = '', h3 = '';
  root.querySelectorAll('p, li, dd, td, h2, h3').forEach(e => {
    if (e.closest('.navbox, .reflist, .references, .infobox, .sidebar, .hatnote, #toc, .mw-references-wrap')) return;
    const t = (e.innerText || '').replace(/\\[\\d+\\]/g, '').replace(/\\[sửa[^\\]]*\\]|\\[edit\\]/gi, '').replace(/\\s+/g, ' ').trim();
    if (e.tagName === 'H2') { h2 = t; h3 = ''; return; }
    if (e.tagName === 'H3') { h3 = t; return; }
    if (t.length >= 12) out.push({t: t, h: h3 ? h2 + ' › ' + h3 : h2});
  });
  return out;
}
"""

_STOP = set(
    "và của các cho với từ là có được trong một những này khi theo như ra vào thì "
    "the of and to in for from with by a an on is are".split()
)
_BOOST = ("điều chế", "phản ứng", "xúc tác", "nhiệt độ", "°c", "→", "⇌", "preparation", "production",
          "reaction", "catalyst", "synthesis", "produced")
_MUC_BO_QUA = re.compile(r"information sources|license|tham khảo|chú thích|liên kết ngoài|references|external links", re.I)
_MUC_QUAN_TRONG = re.compile(r"điều chế|sản xuất|tổng hợp|phản ứng|tính chất hóa học|production|synthesis|preparation|reactions", re.I)


def _khoa(tu: str) -> str:
    """Khoá so khớp gần đúng giữa chính tả cũ/mới (axetilen ~ acetylen, metan ~ methan)."""
    import unicodedata

    t = unicodedata.normalize("NFD", tu.lower().replace("đ", "d"))
    t = "".join(c for c in t if unicodedata.category(c) != "Mn")
    for a, b in (("th", "t"), ("ph", "f"), ("x", "c"), ("k", "c"), ("y", "i")):
        t = t.replace(a, b)
    return t.rstrip("e")[:6]


# Từ chung không dùng để so khớp tiêu đề bài viết
_KHOA_CHUNG = {
    _khoa(w)
    for w in "điều chế phản ứng sản xuất tổng hợp tính chất hoá hóa học từ thành và của cách công nghiệp phòng thí nghiệm "
    "production preparation synthesis reaction reactions properties from to of and the industrial".split()
}


def _tu_khoa(cau_hoi: str) -> list[str]:
    words = re.findall(r"[\wÀ-ỹ]+", cau_hoi.lower())
    return [_khoa(w) for w in words if w not in _STOP and len(w) > 1]


def chon_doan_lien_quan(doan: list, cau_hoi: str, toi_da: int = 6, max_chars: int = 3000) -> str:
    """Chọn các đoạn liên quan nhất tới câu hỏi (giữ thứ tự xuất hiện, kèm tên mục)."""
    kws = set(_tu_khoa(cau_hoi))
    scored = []
    for i, d in enumerate(doan):
        t, h = (d.get("t", ""), d.get("h", "")) if isinstance(d, dict) else (str(d), "")
        if _MUC_BO_QUA.search(h):
            continue
        low = t.lower()
        s = 2 * len({_khoa(w) for w in re.findall(r"[\wÀ-ỹ]+", low)} & kws)
        s += sum(2 for b in _BOOST if b in low)
        if h and _MUC_QUAN_TRONG.search(h):
            s += 3
        if i == 0:
            s += 1  # đoạn mở đầu thường định nghĩa chất
        scored.append((s, i, f"[{h}] {t}" if h else t))
    top = sorted(scored, key=lambda x: (-x[0], x[1]))[:toi_da]
    top = [x for x in top if x[0] > 0] or scored[:2]
    text = "\n".join(d for _, _, d in sorted(top, key=lambda x: x[1]))
    return text[:max_chars]


@dataclass
class TraCuuWeb:
    hien_chrome: bool = False
    emit: Emit | None = None
    so_trang: int = 0
    max_trang: int = MAX_TRANG
    nguon_da_doc: list[str] = field(default_factory=list)
    _pw: Any = None
    _browser: Any = None
    _page: Any = None

    # --- trình duyệt
    def _mo(self):
        if self._page is not None:
            return self._page
        from playwright.sync_api import sync_playwright

        self._pw = sync_playwright().start()
        try:
            self._browser = self._pw.chromium.launch(
                channel="chrome", headless=not self.hien_chrome, slow_mo=120 if self.hien_chrome else 0
            )
        except Exception as e:
            self._pw.stop()
            self._pw = None
            raise RuntimeError("Không mở được Google Chrome qua Playwright") from e
        ctx = self._browser.new_context(locale="vi-VN", viewport={"width": 1280, "height": 900})
        ctx.set_default_timeout(TIMEOUT_TRANG)
        if not self.hien_chrome:
            ctx.route(
                "**/*",
                lambda route: route.abort()
                if route.request.resource_type in {"image", "media", "font"}
                else route.continue_(),
            )
        self._page = ctx.new_page()
        return self._page

    def dong(self):
        try:
            if self._browser:
                self._browser.close()
        finally:
            if self._pw:
                self._pw.stop()
            self._pw = self._browser = self._page = None

    # --- tiện ích
    def _dem_trang(self):
        self.so_trang += 1
        if self.so_trang > self.max_trang:
            raise _HetLuot()

    def _thong_bao(self, msg: str, url: str | None = None):
        if self.emit:
            self.emit("tra_cuu", msg, url=url)

    @staticmethod
    def _host_ok(url: str) -> bool:
        return urllib.parse.urlparse(url).hostname in ALLOWED_HOSTS

    def _goto(self, page, url: str):
        if not self._host_ok(url):
            raise ValueError(f"Trang không nằm trong danh sách được phép: {url}")
        self._dem_trang()
        self._thong_bao(f"Đang mở {url}", url)
        page.goto(url, wait_until="domcontentloaded")

    @staticmethod
    def _on_dinh(page):
        """Đợi trang ổn định (trang có thể chuyển hướng thêm một lần sau khi gửi tìm kiếm)."""
        try:
            page.wait_for_load_state("load", timeout=TIMEOUT_TRANG)
        except Exception:  # noqa: BLE001
            pass
        page.wait_for_timeout(400)

    def _eval(self, page, fn, *args):
        for lan in range(3):
            try:
                return fn(*args)
            except Exception as e:  # noqa: BLE001
                if "context was destroyed" not in str(e) or lan == 2:
                    raise
                self._on_dinh(page)

    @staticmethod
    def _bi_chan(page) -> bool:
        try:
            title = (page.title() or "").lower()
            body = (page.inner_text("body", timeout=3000) or "")[:3000].lower()
        except Exception:  # noqa: BLE001
            return False
        dau_hieu = ("captcha", "could not be satisfied", "access denied", "just a moment", "verify you are human",
                    "unusual traffic", "kiểm tra trình duyệt", "checking your browser")
        return any(d in title or d in body for d in dau_hieu)

    def _doc_trang(self, page, cau_hoi: str) -> dict | None:
        if "pubchem" in page.url:
            page.wait_for_timeout(2500)  # trang PubChem dựng nội dung bằng JavaScript
        if self._bi_chan(page):
            self._thong_bao(f"Trang yêu cầu xác minh (CAPTCHA) hoặc chặn truy cập tự động – bỏ qua: {page.url}")
            return None
        doan = self._eval(page, page.evaluate, _JS_TRICH_DOAN)
        if not doan:
            self._thong_bao(f"Không đọc được nội dung (trang có thể yêu cầu xác minh) – bỏ qua: {page.url}")
            return None
        self._thong_bao(f"Đang đọc: {page.url}", page.url)
        if page.url not in self.nguon_da_doc:
            self.nguon_da_doc.append(page.url)
        return {"url": page.url, "tieu_de": page.title(), "doan_trich": chon_doan_lien_quan(doan, cau_hoi)}

    def _tim_bang_o_tim_kiem(self, page, nguon: str, cau_hoi: str):
        home, selector = NGUON[nguon]
        host = urllib.parse.urlparse(home).hostname
        if urllib.parse.urlparse(page.url).hostname != host or not page.query_selector(selector):
            self._goto(page, home)
        if self._bi_chan(page):
            raise _BiChan(page.url)
        box = page.locator(selector).first
        box.wait_for(state="visible", timeout=8000)
        box.click()
        if self.hien_chrome:
            box.fill("")
            box.press_sequentially(cau_hoi, delay=35)
        else:
            box.fill(cau_hoi)
        self._dem_trang()
        with page.expect_navigation(wait_until="domcontentloaded", timeout=TIMEOUT_TRANG):
            box.press("Enter")
        self._on_dinh(page)
        if "pubchem" in host:
            page.wait_for_timeout(3500)

    def _lien_ket_ket_qua(self, page, nguon: str, cau_hoi: str) -> list[str]:
        """Liên kết kết quả tìm kiếm, xếp theo mức trùng khớp giữa tiêu đề và câu hỏi."""
        if "wikipedia" in nguon:
            sel = ".mw-search-result-heading a"
        elif nguon == "pubchem":
            sel = "a[href*='/compound/']"
        else:
            sel = "main a[href], .search a[href]"
        items = self._eval(page, page.eval_on_selector_all, sel, "els => els.slice(0, 10).map(e => [e.href, e.innerText || ''])")
        kws = {k for k in _tu_khoa(cau_hoi) if k not in _KHOA_CHUNG}
        ranked = []
        for i, (href, title) in enumerate(items):
            if not self._host_ok(href):
                continue
            ten = re.split(r"\s*\(đề mục|\s*\(section", title.strip())[0]
            khoa_ten = {_khoa(w) for w in re.findall(r"[\wÀ-ỹ]+", ten.lower())} - _KHOA_CHUNG
            khop = len(khoa_ten & kws)
            du = bool(khoa_ten) and khoa_ten <= kws  # mọi từ của tiêu đề đều có trong câu hỏi
            ranked.append((not du, -khop, i, href, ten))
        ranked.sort()
        if "wikipedia" in nguon and ranked and ranked[0][0]:
            # Không tiêu đề nào khớp câu hỏi: không phí lượt đọc trang, để Claude đổi từ khoá
            raise _KhongKhop([t for *_, t in ranked[:6]])
        return list(dict.fromkeys(r[3] for r in ranked))

    def _tim_va_doc(self, page, nguon: str, tu_khoa: str, cau_hoi: str, bat_dau: float) -> list[dict]:
        self._thong_bao(f"Tìm “{tu_khoa}” trên {nguon}")
        self._tim_bang_o_tim_kiem(page, nguon, tu_khoa)
        la_trang_ket_qua = (
            "search=" in page.url or "Special:Search" in page.url or "%C4%90%E1%BA%B7c_bi%E1%BB%87t" in page.url
            or nguon in ("pubchem", "libretexts")
        )
        if not la_trang_ket_qua:  # ô tìm kiếm đưa thẳng tới bài viết
            r = self._doc_trang(page, cau_hoi)
            return [r] if r else []
        # Chỉ đọc kết quả khớp nhất chưa đọc; nếu bị chặn/lỗi mới thử kết quả kế tiếp
        links = [u for u in self._lien_ket_ket_qua(page, nguon, tu_khoa) if u not in self.nguon_da_doc]
        for url in links[:2]:
            if time.time() - bat_dau > TIMEOUT_CAU_HOI or self.so_trang >= self.max_trang:
                break
            self._goto(page, url)
            r = self._doc_trang(page, cau_hoi)
            if r:
                return [r]
        return []

    @staticmethod
    def _phan_ten_chat(cau_hoi: str) -> str:
        """Phần tên chất ở đầu câu hỏi: 'Acetaldehyde điều chế' → 'Acetaldehyde'."""
        words = cau_hoi.split()
        out = []
        for w in words:
            if _khoa(w) in _KHOA_CHUNG or w.lower() in _STOP:
                break
            out.append(w)
        return " ".join(out)

    def web_research(self, cau_hoi: str, nguon: str = "vi.wikipedia") -> dict:
        nguon = nguon if nguon in NGUON else "vi.wikipedia"
        if self.so_trang >= self.max_trang:
            return {"ok": False, "thong_bao": f"Đã dùng hết {self.max_trang} lượt tải trang cho bài này."}
        bat_dau = time.time()
        ket_qua: list[dict] = []
        try:
            page = self._mo()
            try:
                ket_qua = self._tim_va_doc(page, nguon, cau_hoi, cau_hoi, bat_dau)
            except _KhongKhop:
                ten = self._phan_ten_chat(cau_hoi)
                if not ten or ten == cau_hoi.strip():
                    raise
                ket_qua = self._tim_va_doc(page, nguon, ten, cau_hoi, bat_dau)
        except _HetLuot:
            return {"ok": bool(ket_qua), "ket_qua": ket_qua, "thong_bao": f"Đã đạt giới hạn {self.max_trang} trang."}
        except _KhongKhop as e:
            return {"ok": False, "thong_bao": "Không có kết quả nào khớp tên chất. Hãy tìm lại bằng tên chuẩn của bài viết "
                    "(vd 'Ethyl acetat' trên vi.wikipedia hoặc tên tiếng Anh trên en.wikipedia).", "cac_ket_qua": e.args[0]}
        except _BiChan as e:
            return {"ok": False, "thong_bao": f"{nguon} chặn truy cập tự động (không vượt CAPTCHA) – hãy dùng nguồn khác.", "url": str(e)}
        except RuntimeError as e:
            return {"ok": False, "thong_bao": str(e)}
        except Exception as e:  # noqa: BLE001 – lỗi mạng/timeout của trang web không được làm hỏng cả bài
            log.warning("web_research lỗi (%s): %s", nguon, e)
            if not ket_qua:
                return {"ok": False, "thong_bao": f"Lỗi khi đọc {nguon}: {e.__class__.__name__}"}
        if not ket_qua:
            return {"ok": False, "thong_bao": "Không tìm thấy trang phù hợp."}
        return {"ok": True, "ket_qua": ket_qua}


class _HetLuot(Exception):
    pass


class _BiChan(Exception):
    pass


class _KhongKhop(Exception):
    pass


# ---------------------------------------------------------------- Vòng tra cứu với Claude
TOOLS = [
    {
        "name": "pubchem_lookup",
        "description": (
            "Tra PubChem (PUG REST) để lấy tên IUPAC, công thức phân tử và SMILES của một chất. "
            "Đầu vào nên là tên tiếng Anh (vd 'acetaldehyde', 'ethyl acetate') hoặc công thức phân tử (vd 'C2H4O'). "
            "Đây là nguồn ưu tiên cho SMILES."
        ),
        "strict": True,
        "input_schema": {
            "type": "object",
            "properties": {
                "ten_hoac_cong_thuc": {"type": "string", "description": "Tên tiếng Anh hoặc công thức phân tử"}
            },
            "required": ["ten_hoac_cong_thuc"],
            "additionalProperties": False,
        },
    },
    {
        "name": "web_research",
        "description": (
            "Mở Google Chrome, dùng ô tìm kiếm của chính trang nguồn rồi trả về các đoạn liên quan kèm URL. "
            "Nguồn: vi.wikipedia, en.wikipedia, pubchem, libretexts (hai nguồn sau có thể chặn trình duyệt tự động – "
            "khi đó dùng pubchem_lookup). Câu hỏi nên ngắn, bắt đầu bằng tên chất "
            "(vd 'Acetylen điều chế', 'Ethyl acetate production'). Mỗi bài tối đa 8 lần tải trang."
        ),
        "strict": True,
        "input_schema": {
            "type": "object",
            "properties": {
                "cau_hoi": {"type": "string", "description": "Từ khoá/câu hỏi ngắn"},
                "nguon": {"type": "string", "enum": ["vi.wikipedia", "en.wikipedia", "pubchem", "libretexts"]},
            },
            "required": ["cau_hoi", "nguon"],
            "additionalProperties": False,
        },
    },
]

SYSTEM_TRA_CUU = """Bạn là trợ lý tra cứu Hoá học cho giáo viên THPT Việt Nam. Bạn chưa cần giải chuỗi; nhiệm vụ là thu thập dữ liệu đáng tin để bước sau giải.
Dữ liệu PubChem của các chất đã biết đã được tra sẵn và gửi kèm.
- Với chất ẩn (X, Y, A...): suy luận ứng viên hợp lý theo chương trình THPT (từ chất trước, tác nhân/điều kiện trên mũi tên và CTPT đề cho nếu có) rồi dùng pubchem_lookup (tên tiếng Anh) để lấy SMILES.
- Dùng web_research (ưu tiên vi.wikipedia, sau đó en.wikipedia) để kiểm chứng điều kiện phản ứng của các bước quan trọng hoặc chưa chắc chắn. Gọi web_research ít nhất 1 lần và nhiều nhất 3 lần; câu hỏi ngắn, chứa tên chất.
- Có thể gọi nhiều công cụ song song trong một lượt.
Khi đủ dữ liệu, KHÔNG gọi thêm công cụ mà viết "GHI CHÚ TRA CỨU" ngắn gọn gồm: mỗi chất (kí hiệu, tên, SMILES, nguồn); mỗi bước (phản ứng dự kiến, điều kiện, URL nguồn nếu có)."""

MAX_LUOT_TRA_CUU = 6


@dataclass
class KetQuaTraCuu:
    ghi_chu: str = ""
    pubchem: dict[str, dict] = field(default_factory=dict)
    trich_doan: list[dict] = field(default_factory=list)
    nguon: list[str] = field(default_factory=list)

    def tom_tat(self, max_chars: int = 9000) -> str:
        """Văn bản gọn để gửi cho bước giải."""
        parts = ["## Dữ liệu PubChem"]
        for q, r in self.pubchem.items():
            if r.get("ok") and r.get("smiles"):
                parts.append(f"- {q}: {r.get('ten')} | IUPAC {r.get('iupac')} | {r.get('cong_thuc_phan_tu')} | SMILES {r.get('smiles')} | {r.get('url')}")
            elif r.get("ung_vien"):
                cands = "; ".join(f"{c['ten']} ({c['smiles']})" for c in r["ung_vien"])
                parts.append(f"- {q}: nhiều ứng viên: {cands}")
            else:
                parts.append(f"- {q}: không tìm thấy")
        if self.trich_doan:
            parts.append("\n## Trích đoạn web")
            for t in self.trich_doan:
                parts.append(f"[{t['url']}]\n{t['doan_trich'][:1500]}")
        if self.ghi_chu:
            parts.append("\n## Ghi chú tra cứu của trợ lý\n" + self.ghi_chu)
        return "\n".join(parts)[:max_chars]


def _thuc_hien_tool(name: str, args: dict, web: TraCuuWeb, kq: KetQuaTraCuu, emit: Emit) -> dict:
    name = (name or "").lower()
    if name == "pubchem_lookup":
        q = str(args.get("ten_hoac_cong_thuc", ""))
        emit("tra_cuu", f"PubChem: tra “{q}”")
        r = pubchem_lookup(q)
        kq.pubchem[q] = r
        if r.get("url"):
            emit("tra_cuu", f"PubChem: {q} → {r.get('ten')} ({r.get('smiles')})", url=r["url"])
            if r["url"] not in kq.nguon:
                kq.nguon.append(r["url"])
        return r
    if name == "web_research":
        r = web.web_research(str(args.get("cau_hoi", "")), str(args.get("nguon", "vi.wikipedia")))
        for t in r.get("ket_qua", []):
            kq.trich_doan.append(t)
            if t["url"] not in kq.nguon:
                kq.nguon.append(t["url"])
        if not r.get("ok"):
            emit("tra_cuu", r.get("thong_bao", "Không lấy được dữ liệu web."))
        return r
    return {"ok": False, "thong_bao": f"Không có công cụ '{name}'. Chỉ có: pubchem_lookup, web_research."}


def tra_cuu_truoc_pubchem(de: PhanTichDe, emit: Emit) -> dict[str, dict]:
    """Tra PubChem song song cho các chất đã biết trong đề."""
    queries = []
    for c in de.cac_chat:
        if c.la_chat_an:
            continue
        q = c.ten_tieng_anh.strip() or c.ki_hieu.strip()
        if q and q not in queries:
            queries.append(q)
    out: dict[str, dict] = {}
    if not queries:
        return out
    with ThreadPoolExecutor(max_workers=4) as ex:
        for q, r in zip(queries, ex.map(pubchem_lookup, queries)):
            out[q] = r
            if r.get("url"):
                emit("tra_cuu", f"PubChem: {q} → {r.get('ten')} ({r.get('smiles')})", url=r["url"])
            else:
                emit("tra_cuu", f"PubChem: {q} – {r.get('thong_bao', 'không tìm thấy')}")
    return out


YEU_CAU_TRA_CUU_HSG = """ĐỀ MỨC HỌC SINH GIỎI (tỉnh/quốc gia): được gọi web_research tới 5 lần.
- Trước hết tự suy luận cấu tạo từng chất ẩn (độ bất bão hoà từ CTPT, tác nhân trên mũi tên, chất cuối nếu đề cho).
- Tra en.wikipedia các phản ứng có tên / phản ứng của nhóm chức với tác nhân ở các bước khó (vd "Grignard reaction", "Ozonolysis",
  "Aldol condensation", "Diels–Alder reaction", "Cannizzaro reaction", "Hofmann rearrangement", "Pinacol rearrangement", "Wittig reaction").
- Dùng pubchem_lookup để lấy SMILES của các ứng viên chất ẩn (tên IUPAC tiếng Anh)."""


def tra_cuu(de: PhanTichDe, hien_chrome: bool, emit: Emit, hsg: bool = False) -> KetQuaTraCuu:
    kq = KetQuaTraCuu()
    kq.pubchem = tra_cuu_truoc_pubchem(de, emit)
    kq.nguon = [r["url"] for r in kq.pubchem.values() if r.get("url")]

    web = TraCuuWeb(hien_chrome=hien_chrome, emit=emit, max_trang=MAX_TRANG_HSG if hsg else MAX_TRANG)
    du_lieu = {
        "de_bai": de.de_bai,
        "cac_buoc": [b.model_dump() for b in de.cac_buoc],
        "cac_chat": [c.model_dump() for c in de.cac_chat],
        "pubchem_da_tra": kq.pubchem,
    }
    # mô hình theo bậc của kế hoạch (pipeline đặt: đề Dễ → lite, còn lại → flash; tra cứu chỉ gọi công cụ, không cần Pro)
    phien = PhienHoiThoai(SYSTEM_TRA_CUU, tools=TOOLS, effort="high" if hsg else "medium", max_tokens=16000,
                          bao=lambda m: emit("tra_cuu", m))
    try:  # luôn đóng Chrome kể cả khi lỗi
        yeu_cau = "Đề và dữ liệu đã có (JSON):\n" + json.dumps(du_lieu, ensure_ascii=False, indent=1)
        if hsg:
            yeu_cau += "\n\n" + YEU_CAU_TRA_CUU_HSG
        ph = phien.gui(text=yeu_cau)
        for luot in range(MAX_LUOT_TRA_CUU + 1):
            if not ph.cong_cu:
                break
            ket_qua: list[tuple[GoiCongCu, dict]] = []
            for gc in ph.cong_cu:
                if luot >= MAX_LUOT_TRA_CUU:
                    r = {"ok": False, "thong_bao": "Đã hết lượt tra cứu. Hãy viết GHI CHÚ TRA CỨU ngay."}
                else:
                    r = _thuc_hien_tool(gc.ten, gc.tham_so, web, kq, emit)
                ket_qua.append((gc, r))
            ph = phien.gui(ket_qua_cong_cu=ket_qua)
        kq.ghi_chu = ph.text
    finally:
        web.dong()
    return kq
