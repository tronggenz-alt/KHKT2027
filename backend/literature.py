"""TRA CỨU TÀI LIỆU KHOA HỌC (chỉ nguồn HỢP PHÁP, miễn phí) làm căn cứ khi giải chuỗi / vẽ cơ chế.

Nguồn: Crossref (thông tin bài theo DOI) · Unpaywall (bản open access hợp pháp; email UNPAYWALL_EMAIL) · OpenAlex (tìm theo từ khoá,
abstract, link mở) · Semantic Scholar (tìm, abstract, TLDR, PDF mở; S2_API_KEY tuỳ chọn) · Europe PMC (toàn văn mở) · arXiv (preprint) ·
PubChem (chuẩn hoá tên chất trước khi tìm). ChemRxiv không có API dùng được (bị chặn bởi Cloudflare) – preprint ChemRxiv vẫn tìm được
qua OpenAlex/Crossref và bản mở qua Unpaywall. KHÔNG dùng Sci-Hub hay bất kỳ trang lậu nào; chỉ tải đúng link mở các API trên trả về.

- Có DOI → Crossref + Unpaywall/OpenAlex/Europe PMC → có bản mở thì đọc toàn văn, không thì dùng tóm tắt (+ nút "Tải lên PDF của bạn").
- Không DOI → từ khoá (AI rẻ rút ra, tên chất chuẩn hoá qua PubChem) → OpenAlex + Semantic Scholar (+ Europe PMC, arXiv) song song →
  3–5 bài liên quan nhất (ưu tiên có bản mở, nhiều trích dẫn, khớp từ khoá).
- Chỉ đưa ĐOẠN TRÍCH liên quan vào prompt (tối đa ~3.000 từ): đoạn chứa tên chất / phản ứng / Experimental / Scheme.
- Gọi song song, tổng tối đa ~20 giây (hết giờ thì dùng những gì đã có); User-Agent có email liên hệ; 429/5xx → chờ (Retry-After) rồi thử lại.
- Cache: theo DOI (thông tin + đoạn trích ngắn) 30 ngày, kết quả tìm từ khoá 7 ngày (bảng lit_cache). KHÔNG lưu toàn văn; PDF người
  dùng tải lên chỉ dùng cho lượt giải đó (không cache, không chia sẻ). Không API nào cho tải đoạn trích về.
"""
from __future__ import annotations

import concurrent.futures as cf
import hashlib
import io
import json
import logging
import re
import threading
import time
import urllib.parse
from html.parser import HTMLParser
from typing import Callable

import requests

from . import db
from .config import settings

log = logging.getLogger(__name__)

TONG_GIAY = 20
TU_TOI_DA = 3000
SO_BAI = (3, 5)
CACHE_DOI_NGAY = 30
CACHE_TIM_NGAY = 7
TAI_TOI_DA = 25 * 1024 * 1024
MAU_DOI = re.compile(r"\b(10\.\d{4,9}/[^\s\"'<>]+)", re.I)
_KHOA = threading.Lock()


class HetGio(Exception):
    pass


def email_lien_he() -> str:
    return settings.unpaywall_email or settings.smtp_user or next(iter(sorted(settings.admin_emails)), "")


def _ua() -> dict:
    e = email_lien_he()
    return {"User-Agent": f"CHEMISTRY-giai-chuoi-huu-co/1.0 (education; mailto:{e})" if e else "CHEMISTRY-giai-chuoi-huu-co/1.0 (education)"}


def _get(url: str, han: float, params: dict | None = None, headers: dict | None = None, stream: bool = False) -> requests.Response:
    """GET có retry khi 429/5xx (chờ theo Retry-After, không quá thời gian còn lại)."""
    r = None
    for lan in range(3):
        con = han - time.monotonic()
        if con <= 0.8:
            raise HetGio()
        r = requests.get(url, params=params, headers={**_ua(), **(headers or {})}, timeout=min(12.0, con), stream=stream)
        if r.status_code in (429, 500, 502, 503, 504) and lan < 2:
            try:
                cho = float(r.headers.get("Retry-After") or 0)
            except ValueError:
                cho = 0.0
            cho = min(max(cho, 1.2 * (lan + 1)), 4.0, han - time.monotonic() - 1.5)
            if cho <= 0:
                return r
            time.sleep(cho)
            continue
        return r
    return r


def tim_doi(text: str) -> list[str]:
    """DOI trong chữ (kể cả link doi.org) → danh sách DOI đã chuẩn hoá (chữ thường, bỏ dấu câu cuối)."""
    ds = []
    for m in MAU_DOI.finditer(urllib.parse.unquote(text or "")):
        d = m.group(1).rstrip(".,;:)]}").lower()
        if d not in ds:
            ds.append(d)
    return ds


# ---------------------------------------------------------------- cache (bảng lit_cache)
def _cache_doc(khoa: str, ngay: int) -> dict | list | None:
    with db.ket_noi() as con:
        r = con.execute("SELECT data FROM lit_cache WHERE key = ? AND created >= ?", (khoa, time.time() - ngay * 86400)).fetchone()
    return json.loads(r[0]) if r else None


def _cache_ghi(khoa: str, loai: str, du_lieu) -> None:
    with db.ket_noi() as con:
        con.execute("INSERT OR REPLACE INTO lit_cache (key, created, kind, data) VALUES (?, ?, ?, ?)",
                    (khoa, time.time(), loai, json.dumps(du_lieu, ensure_ascii=False)))
        con.execute("DELETE FROM lit_cache WHERE (kind = 'doi' AND created < ?) OR (kind = 'tim' AND created < ?)",
                    (time.time() - CACHE_DOI_NGAY * 86400, time.time() - CACHE_TIM_NGAY * 86400))


# ---------------------------------------------------------------- từng nguồn
def _bai(**kw) -> dict:
    b = {"doi": "", "tieu_de": "", "tac_gia": "", "tap_chi": "", "nam": None, "trich_dan": 0, "tom_tat": "", "url_mo": "", "pmcid": "",
         "arxiv": "", "nguon": []}
    b.update({k: v for k, v in kw.items() if v not in (None, "")})
    return b


def _tac_gia(ten: list[str]) -> str:
    ten = [t for t in ten if t]
    return ", ".join(ten[:3]) + (" và cộng sự" if len(ten) > 3 else "")


def _bo_the(text: str) -> str:
    return re.sub(r"\s+", " ", re.sub(r"<[^>]+>", " ", text or "")).strip()


def crossref(doi: str, han: float) -> dict | None:
    r = _get(f"https://api.crossref.org/works/{urllib.parse.quote(doi, safe='/')}", han)
    if r.status_code != 200:
        return None
    m = r.json().get("message", {})
    nam = ((m.get("issued") or {}).get("date-parts") or [[None]])[0][0]
    return _bai(doi=doi, tieu_de=_bo_the((m.get("title") or [""])[0]), tac_gia=_tac_gia([a.get("family") or a.get("name", "") for a in m.get("author", [])]),
                tap_chi=(m.get("container-title") or [""])[0], nam=nam, trich_dan=m.get("is-referenced-by-count", 0),
                tom_tat=_bo_the(m.get("abstract", "")), nguon=["Crossref"])


def unpaywall(doi: str, han: float) -> str:
    """Link bản mở hợp pháp tốt nhất (ưu tiên PDF) – "" nếu không có / chưa có email liên hệ."""
    if not email_lien_he():
        return ""
    r = _get(f"https://api.unpaywall.org/v2/{urllib.parse.quote(doi, safe='/')}", han, params={"email": email_lien_he()})
    if r.status_code != 200:
        return ""
    j = r.json()
    if not j.get("is_oa"):
        return ""
    loc = j.get("best_oa_location") or {}
    return loc.get("url_for_pdf") or loc.get("url") or ""


def _tu_inverted(inv: dict | None) -> str:
    if not inv:
        return ""
    vi_tri = sorted((i, w) for w, ds in inv.items() for i in ds)
    return " ".join(w for _, w in vi_tri)


_OA_CHON = "doi,title,publication_year,cited_by_count,open_access,primary_location,abstract_inverted_index,authorships"


def _tu_openalex(w: dict) -> dict:
    doi = (w.get("doi") or "").replace("https://doi.org/", "").lower()
    nguon = ((w.get("primary_location") or {}).get("source") or {})
    return _bai(doi=doi, tieu_de=w.get("title") or "", nam=w.get("publication_year"), trich_dan=w.get("cited_by_count") or 0,
                tac_gia=_tac_gia([(a.get("author") or {}).get("display_name", "") for a in w.get("authorships") or []]),
                tap_chi=nguon.get("display_name") or "", tom_tat=_tu_inverted(w.get("abstract_inverted_index")),
                url_mo=(w.get("open_access") or {}).get("oa_url") or "" if (w.get("open_access") or {}).get("is_oa") else "",
                nguon=["OpenAlex"])


def openalex_doi(doi: str, han: float) -> dict | None:
    r = _get(f"https://api.openalex.org/works/doi:{doi}", han, params={"select": _OA_CHON, "mailto": email_lien_he() or None})
    return _tu_openalex(r.json()) if r.status_code == 200 else None


def openalex_tim(q: str, n: int, han: float) -> list[dict]:
    r = _get("https://api.openalex.org/works", han, params={"search": q, "per-page": n, "select": _OA_CHON, "mailto": email_lien_he() or None})
    return [_tu_openalex(w) for w in r.json().get("results", [])] if r.status_code == 200 else []


def _s2_headers() -> dict:
    return {"x-api-key": settings.s2_api_key} if settings.s2_api_key else {}


def _tu_s2(p: dict) -> dict:
    ids = p.get("externalIds") or {}
    tt = p.get("abstract") or ((p.get("tldr") or {}).get("text") or "")
    return _bai(doi=(ids.get("DOI") or "").lower(), tieu_de=p.get("title") or "", nam=p.get("year"), tap_chi=p.get("venue") or "",
                trich_dan=p.get("citationCount") or 0, tac_gia=_tac_gia([a.get("name", "") for a in p.get("authors") or []]), tom_tat=tt,
                url_mo=(p.get("openAccessPdf") or {}).get("url") or "", arxiv=ids.get("ArXiv") or "", nguon=["Semantic Scholar"])


_S2_TRUONG = "title,year,venue,citationCount,externalIds,openAccessPdf,tldr,abstract,authors"


def s2_tim(q: str, n: int, han: float) -> list[dict]:
    r = _get("https://api.semanticscholar.org/graph/v1/paper/search", han, params={"query": q, "limit": n, "fields": _S2_TRUONG}, headers=_s2_headers())
    return [_tu_s2(p) for p in (r.json().get("data") or [])] if r.status_code == 200 else []


def s2_doi(doi: str, han: float) -> dict | None:
    r = _get(f"https://api.semanticscholar.org/graph/v1/paper/DOI:{doi}", han, params={"fields": _S2_TRUONG}, headers=_s2_headers())
    return _tu_s2(r.json()) if r.status_code == 200 else None


def europepmc(q: str, n: int, han: float) -> list[dict]:
    r = _get("https://www.ebi.ac.uk/europepmc/webservices/rest/search", han, params={"query": q, "format": "json", "resultType": "core", "pageSize": n})
    if r.status_code != 200:
        return []
    kq = []
    for x in r.json().get("resultList", {}).get("result", []):
        mo = x.get("isOpenAccess") == "Y" and x.get("inEPMC") == "Y" and x.get("pmcid")
        kq.append(_bai(doi=(x.get("doi") or "").lower(), tieu_de=_bo_the(x.get("title", "")), nam=int(x["pubYear"]) if str(x.get("pubYear", "")).isdigit() else None,
                       tac_gia=_tac_gia([s.strip() for s in (x.get("authorString") or "").split(",")]), tap_chi=(x.get("journalInfo") or {}).get("journal", {}).get("title", ""),
                       trich_dan=x.get("citedByCount") or 0, tom_tat=_bo_the(x.get("abstractText", "")), pmcid=x.get("pmcid", "") if mo else "",
                       nguon=["Europe PMC"]))
    return kq


def arxiv_tim(q: str, n: int, han: float) -> list[dict]:
    tu = " AND ".join(f"all:{w}" for w in re.findall(r"[A-Za-z0-9\-]{3,}", q)[:5])
    if not tu:
        return []
    r = _get("https://export.arxiv.org/api/query", han, params={"search_query": tu, "max_results": n})
    if r.status_code != 200:
        return []
    kq = []
    for muc in re.findall(r"<entry>(.*?)</entry>", r.text, re.S):
        tim = lambda t: (re.search(rf"<{t}[^>]*>(.*?)</{t}>", muc, re.S) or [None, ""])[1].strip()  # noqa: E731
        ma = tim("id").rsplit("/abs/", 1)[-1]
        kq.append(_bai(tieu_de=_bo_the(tim("title")), tom_tat=_bo_the(tim("summary")), nam=int(tim("published")[:4]) if tim("published")[:4].isdigit() else None,
                       tac_gia=_tac_gia(re.findall(r"<name>(.*?)</name>", muc)), tap_chi="arXiv", arxiv=ma,
                       url_mo=f"https://arxiv.org/pdf/{ma}" if ma else "", nguon=["arXiv"]))
    return kq


def pubchem_chuan_hoa(ten: str, han: float) -> list[str]:
    """Tên chất (Việt/Anh, công thức) → [tên PubChem, tên IUPAC, 2 tên khác] để tìm bài chính xác hơn."""
    from .research import pubchem_lookup

    if time.monotonic() > han - 1:
        return [ten]
    r = pubchem_lookup(ten)
    if not r.get("ok") or not r.get("cid"):
        return [ten]
    ds = [x for x in (r.get("ten"), r.get("iupac")) if x]
    try:
        s = _get(f"https://pubchem.ncbi.nlm.nih.gov/rest/pug/compound/cid/{r['cid']}/synonyms/JSON", han)
        if s.status_code == 200:
            ds += [x for x in s.json()["InformationList"]["Information"][0].get("Synonym", [])[:3] if len(x) < 60]
    except (HetGio, requests.RequestException, KeyError, IndexError, ValueError):
        pass
    return list(dict.fromkeys(ds)) or [ten]


# ---------------------------------------------------------------- đọc toàn văn (chỉ link mở hợp pháp) + trích đoạn
class _ChuHTML(HTMLParser):
    def __init__(self):
        super().__init__()
        self.doan, self._bo = [], 0

    def handle_starttag(self, tag, attrs):
        if tag in ("script", "style", "nav", "header", "footer"):
            self._bo += 1
        elif tag in ("p", "div", "li", "h1", "h2", "h3", "h4", "section", "br", "tr"):
            self.doan.append("\n")

    def handle_endtag(self, tag):
        if tag in ("script", "style", "nav", "header", "footer") and self._bo:
            self._bo -= 1

    def handle_data(self, data):
        if not self._bo:
            self.doan.append(data)


def chu_tu_pdf(du_lieu: bytes, so_trang: int = 40) -> str:
    from pypdf import PdfReader

    try:
        doc = PdfReader(io.BytesIO(du_lieu))
        return "\n\n".join((trang.extract_text() or "") for trang in doc.pages[:so_trang])
    except Exception as e:  # noqa: BLE001 – PDF hỏng / mã hoá
        log.info("Không đọc được PDF: %s", e)
        return ""


def chu_tu_html(text: str) -> str:
    p = _ChuHTML()
    try:
        p.feed(text)
    except Exception:  # noqa: BLE001
        return _bo_the(text)
    return re.sub(r"\n\s*\n+", "\n\n", "".join(p.doan))


def doc_toan_van(b: dict, han: float) -> str:
    """Toàn văn từ link MỞ (Europe PMC → link PDF/HTML open access). "" nếu không đọc được (bị chặn, quá lớn, hết giờ)."""
    try:
        if b.get("pmcid"):
            r = _get(f"https://www.ebi.ac.uk/europepmc/webservices/rest/{b['pmcid']}/fullTextXML", han)
            if r.status_code == 200 and len(r.text) > 2000:
                return chu_tu_html(r.text)
        if not b.get("url_mo"):
            return ""
        r = _get(b["url_mo"], han, stream=True, headers={"Accept": "application/pdf,text/html;q=0.9,*/*;q=0.5"})
        if r.status_code != 200:
            return ""
        du_lieu = b""
        for manh in r.iter_content(65536):
            du_lieu += manh
            if len(du_lieu) > TAI_TOI_DA or time.monotonic() > han:
                return ""
        if du_lieu[:5] == b"%PDF-" or "pdf" in r.headers.get("content-type", ""):
            return chu_tu_pdf(du_lieu)
        if "html" in r.headers.get("content-type", "") or du_lieu.lstrip()[:1] == b"<":
            chu = chu_tu_html(du_lieu.decode(r.encoding or "utf-8", "ignore"))
            return chu if len(chu.split()) > 800 else ""  # trang đích ngắn (chỉ có tóm tắt / chặn bot) → không coi là toàn văn
    except (HetGio, requests.RequestException) as e:
        log.info("Không tải được bản mở %s: %s", b.get("url_mo") or b.get("pmcid"), e)
    return ""


_KHOA_PHAN = ("experimental", "scheme", "synthesis", "mechanism", "procedure", "results and discussion", "rearrangement")


def trich_doan(chu: str, tu_khoa: list[str], so_tu: int) -> str:
    """Các đoạn liên quan nhất (chứa tên chất / phản ứng / Experimental / Scheme) theo thứ tự xuất hiện, tổng ≤ so_tu từ."""
    doan = [re.sub(r"\s+", " ", d).strip() for d in re.split(r"\n\s*\n|(?<=\.)\n", chu or "")]
    doan = [d for d in doan if len(d.split()) >= 12 and not re.match(r"^(references|acknowledg)", d.lower())]
    tk = [t.lower() for t in tu_khoa if t and len(t) >= 3]
    diem = []
    for i, d in enumerate(doan):
        thap = d.lower()
        s = sum(thap.count(t) * (3 if len(t) > 6 else 1) for t in tk) + sum(2 for k in _KHOA_PHAN if k in thap)
        if s:
            diem.append((s, i))
    chon, dem = set(), 0
    for s, i in sorted(diem, reverse=True):
        n = len(doan[i].split())
        if dem + n > so_tu:
            if dem == 0:  # đoạn đầu quá dài → cắt
                chon.add(i)
                doan[i] = " ".join(doan[i].split()[:so_tu])
                dem = so_tu
            continue
        chon.add(i)
        dem += n
    return "\n".join(doan[i] for i in sorted(chon))


# ---------------------------------------------------------------- điều phối
def _khoa_bai(b: dict) -> str:
    return b.get("doi") or re.sub(r"\W+", "", (b.get("tieu_de") or "").lower())[:80]


def _gop(ds: list[dict]) -> list[dict]:
    """Gộp bài trùng (cùng DOI / cùng tiêu đề) từ nhiều nguồn, giữ thông tin đầy đủ nhất."""
    gop: dict[str, dict] = {}
    for b in ds:
        k = _khoa_bai(b)
        if not k:
            continue
        if k not in gop:
            gop[k] = dict(b)
            continue
        g = gop[k]
        for t in ("doi", "tieu_de", "tac_gia", "tap_chi", "nam", "url_mo", "pmcid", "arxiv"):
            g[t] = g.get(t) or b.get(t)
        g["tom_tat"] = g["tom_tat"] if len(g.get("tom_tat") or "") >= len(b.get("tom_tat") or "") else b["tom_tat"]
        g["trich_dan"] = max(g.get("trich_dan") or 0, b.get("trich_dan") or 0)
        g["nguon"] = list(dict.fromkeys(g["nguon"] + b["nguon"]))
    return list(gop.values())


def _diem(b: dict, tu_khoa: list[str]) -> float:
    chu = f"{b.get('tieu_de', '')} {b.get('tom_tat', '')[:600]}".lower()
    khop = sum(1 for t in tu_khoa if t and t.lower() in chu)
    return khop * 3 + (4 if b.get("url_mo") or b.get("pmcid") else 0) + min(6, (b.get("trich_dan") or 0) ** 0.5 / 3) + (1 if b.get("tap_chi") else 0) \
        + (1 if b.get("tom_tat") else 0)


def _thong_tin_doi(doi: str, han: float, ex: cf.ThreadPoolExecutor) -> dict | None:
    """DOI → thông tin + link mở: Crossref, Unpaywall, OpenAlex, Europe PMC, Semantic Scholar song song."""
    viec = {ex.submit(crossref, doi, han): "cr", ex.submit(unpaywall, doi, han): "up", ex.submit(openalex_doi, doi, han): "oa",
            ex.submit(europepmc, f'DOI:"{doi}"', 1, han): "ep", ex.submit(s2_doi, doi, han): "s2"}
    kq: dict[str, object] = {}
    for f in cf.as_completed(viec, timeout=max(1.0, han - time.monotonic())):
        try:
            kq[viec[f]] = f.result()
        except (HetGio, requests.RequestException, ValueError) as e:
            log.info("Tra DOI %s (%s) lỗi: %s", doi, viec[f], e)
    cac = [x for x in (kq.get("cr"), kq.get("oa"), *(kq.get("ep") or []), kq.get("s2")) if x]
    if not cac:
        return None
    for x in cac:
        x["doi"] = doi
    b = _gop(cac)[0]
    if kq.get("up"):
        b["url_mo"], b["nguon"] = kq["up"], list(dict.fromkeys(b["nguon"] + ["Unpaywall"]))
    return b


def _hoan_thien(b: dict, tu_khoa: list[str], so_tu: int, han: float, pdf: bytes | None = None) -> dict:
    """Đọc toàn văn (PDF người dùng / bản mở) → đoạn trích; không có thì tóm tắt."""
    chu = chu_tu_pdf(pdf) if pdf else doc_toan_van(b, han)
    if chu and len(chu.split()) > 300:
        b["trich"], b["toan_van"] = trich_doan(chu, tu_khoa + [b.get("tieu_de", "")[:40]], so_tu), True
        b["nguon_toan_van"] = "PDF bạn tải lên" if pdf else ("Europe PMC" if b.get("pmcid") else "bản open access")
    else:
        b["trich"], b["toan_van"] = " ".join((b.get("tom_tat") or "").split()[:350]), False
    return b


def tra_cuu(tu_khoa: dict, bao: Callable[[str], None] | None = None, so_tu: int = TU_TOI_DA, han_giay: float = TONG_GIAY,
            pdf: tuple[bytes, str] | None = None) -> dict:
    """tu_khoa: {"doi": [...], "chat": [...], "phan_ung": [...], "truy_van": [...]} (giai_nhanh / co-che tạo bằng AI rẻ).
    pdf: (dữ liệu PDF người dùng tải lên, DOI của bài) – chỉ dùng cho lượt này. → {"bai": [...], "khoi_prompt", "so_tu", "token_uoc_tinh", ...}"""
    bao = bao or (lambda _m: None)
    t0 = time.monotonic()
    han = t0 + han_giay
    dois = list(dict.fromkeys([d.lower() for d in tu_khoa.get("doi") or []] + ([pdf[1].lower()] if pdf and pdf[1] else [])))[:3]
    bai: list[dict] = []
    tu = [x for x in (tu_khoa.get("chat") or []) + (tu_khoa.get("phan_ung") or []) if x]
    with cf.ThreadPoolExecutor(max_workers=10) as ex:
        try:
            # chuẩn hoá tên chất qua PubChem (song song)
            chuan = list(ex.map(lambda t: pubchem_chuan_hoa(t, han), (tu_khoa.get("chat") or [])[:4]))
            tu = list(dict.fromkeys([x for ds in chuan for x in ds] + (tu_khoa.get("phan_ung") or [])))
            if dois:
                bao(f"Đang tìm bài báo theo DOI ({', '.join(dois)})…")
                for doi in dois:
                    if pdf is None:
                        cu = _cache_doc(f"doi:{doi}", CACHE_DOI_NGAY)
                        if cu:
                            bai.append({**cu, "tu_cache": True})
                            continue
                    b = _thong_tin_doi(doi, han, ex)
                    if b:
                        bai.append(b)
            else:
                truy_van = [q for q in (tu_khoa.get("truy_van") or []) if q][:2] or [" ".join(tu[:3])]
                khoa_tim = "tim:" + hashlib.sha256(json.dumps(truy_van, ensure_ascii=False).encode()).hexdigest()
                cu = _cache_doc(khoa_tim, CACHE_TIM_NGAY)
                if cu:
                    bai = cu
                    bao(f"Tìm bài báo: dùng kết quả đã lưu cho “{truy_van[0]}”")
                else:
                    bao(f"Đang tìm bài báo: “{truy_van[0]}”…")
                    viec = []
                    for q in truy_van:
                        viec += [ex.submit(openalex_tim, q, 6, han), ex.submit(s2_tim, q, 6, han), ex.submit(europepmc, q, 3, han), ex.submit(arxiv_tim, q, 2, han)]
                    ds = []
                    for f in cf.as_completed(viec, timeout=max(1.0, han - time.monotonic() - 6)):
                        try:
                            ds += f.result()
                        except (HetGio, requests.RequestException, ValueError) as e:
                            log.info("Tìm bài lỗi: %s", e)
                    bai = sorted(_gop(ds), key=lambda b: -_diem(b, tu))[:SO_BAI[1]]
                    if bai:
                        _cache_ghi(khoa_tim, "tim", bai)
        except (cf.TimeoutError, HetGio):
            log.info("Tra cứu tài liệu: hết %s giây – dùng những gì đã có", han_giay)
        for b in bai:  # bài đã đọc trước đây (cache theo DOI) → dùng luôn đoạn trích, không tải lại
            if b.get("doi") and "trich" not in b and pdf is None and (cu := _cache_doc(f"doi:{b['doi']}", CACHE_DOI_NGAY)) and "trich" in cu:
                b.update(cu, tu_cache=True)
        so_mo = sum(1 for b in bai if b.get("url_mo") or b.get("pmcid") or b.get("toan_van"))
        if bai:
            bao(f"Tìm thấy {len(bai)} bài, {so_mo} bài có bản mở" + (" – đang đọc…" if so_mo or pdf else ""))
        # đọc toàn văn song song (bài trong cache đã có đoạn trích)
        can_doc = [b for b in bai if "trich" not in b]
        so_toan_van = sum(1 for b in can_doc if b.get("url_mo") or b.get("pmcid") or pdf)
        moi_bai = max(400, (so_tu - 250 * (len(bai) - so_toan_van)) // max(1, so_toan_van))  # tóm tắt ~250 từ, phần còn lại chia đều
        viec = {ex.submit(_hoan_thien, b, tu, moi_bai, han, pdf[0] if pdf and b.get("doi") == (pdf[1] or "").lower() else None): b for b in can_doc}
        try:
            for f in cf.as_completed(viec, timeout=max(1.0, han - time.monotonic())):
                f.result()
        except cf.TimeoutError:
            log.info("Đọc toàn văn: hết giờ – dùng tóm tắt cho bài chưa đọc xong")
    for b in bai:
        if "trich" not in b:
            b["trich"], b["toan_van"] = " ".join((b.get("tom_tat") or "").split()[:350]), False
        if b.get("doi") and not b.get("tu_cache") and not (pdf and b["doi"] == (pdf[1] or "").lower()):
            _cache_ghi(f"doi:{b['doi']}", "doi", {k: v for k, v in b.items() if k != "tu_cache"})
    bai = [b for b in bai if b.get("tieu_de")][:SO_BAI[1]]
    khoi, dem = [], 0
    for i, b in enumerate(bai, 1):
        loai = f"toàn văn – {b.get('nguon_toan_van', 'bản open access')}" if b.get("toan_van") else "chỉ tóm tắt"
        dau = f"[{i}] {b['tieu_de']} – {b.get('tac_gia') or '?'} – {b.get('tap_chi') or '?'} {b.get('nam') or ''}" + (f" – DOI {b['doi']}" if b.get("doi") else "")
        trich = " ".join(b.get("trich", "").split()[: max(0, so_tu - dem)])
        dem += len(trich.split())
        khoi.append(f"{dau} ({loai})\n{trich}")
    khoi_prompt = "\n\n".join(khoi)
    return {"bai": [_cong_khai(b, i) for i, b in enumerate(bai, 1)], "khoi_prompt": khoi_prompt, "so_tu": dem,
            "token_uoc_tinh": round(len(khoi_prompt) / 3.5), "co_doi": bool(dois), "thoi_gian": round(time.monotonic() - t0, 1),
            "tu_cache": bool(bai) and all(b.get("tu_cache") for b in bai)}


def _cong_khai(b: dict, so: int) -> dict:
    """Thông tin nguồn cho giao diện – KHÔNG kèm đoạn trích / toàn văn."""
    return {"so": so, "tieu_de": b.get("tieu_de", ""), "tac_gia": b.get("tac_gia", ""), "tap_chi": b.get("tap_chi", ""), "nam": b.get("nam"),
            "doi": b.get("doi", ""), "url": f"https://doi.org/{b['doi']}" if b.get("doi") else (f"https://arxiv.org/abs/{b['arxiv']}" if b.get("arxiv") else ""),
            "toan_van": bool(b.get("toan_van")), "nguon_toan_van": b.get("nguon_toan_van", ""), "co_ban_mo": bool(b.get("url_mo") or b.get("pmcid")),
            "can_tai_pdf": not b.get("toan_van") and bool(b.get("doi")), "nguon_api": b.get("nguon", []), "trich_dan": b.get("trich_dan") or 0}


# ---------------------------------------------------------------- rút từ khoá bằng AI rẻ (Flash-Lite) – đọc được cả DOI trong ảnh
SYSTEM_TU_KHOA = """Bạn rút TỪ KHOÁ để tìm bài báo khoa học (OpenAlex, Semantic Scholar) cho một đề Hoá hữu cơ. KHÔNG giải đề.
Trả JSON {"doi": [...], "chat": [...], "phan_ung": [...], "truy_van": [...]}:
- doi: mọi DOI thấy trong đề/ảnh (dạng 10.xxxx/...), không có thì [].
- chat: tối đa 4 chất CHÍNH (không lấy chất vô cơ thông thường như H2O, HBr, NaOH), ghi TÊN TIẾNG ANH chuẩn (vd "pinacol", "cyclohexene").
- phan_ung: tối đa 3 tên phản ứng / quy trình bằng tiếng Anh (vd "pinacol rearrangement", "Wagner-Meerwein rearrangement").
- truy_van: 1–2 câu tìm kiếm tiếng Anh ngắn (3–7 từ) kết hợp chất + phản ứng, phù hợp tìm bài báo cơ chế / tổng hợp."""
SCHEMA_TU_KHOA = {"type": "object", "properties": {k: {"type": "array", "items": {"type": "string"}} for k in ("doi", "chat", "phan_ung", "truy_van")},
                  "required": ["doi", "chat", "phan_ung", "truy_van"], "additionalProperties": False}


def rut_tu_khoa(de: str, anh: tuple[str, str] | None = None, bao: Callable[[str], None] | None = None) -> dict:
    """Đề (chữ / ảnh) → từ khoá tìm bài. DOI viết thẳng trong chữ lấy bằng regex (không cần AI). Lỗi AI → chỉ dùng DOI regex."""
    from .llm import LoiAI, PhienHoiThoai, doc_json

    dois = tim_doi(de)
    try:
        phien = PhienHoiThoai(SYSTEM_TU_KHOA, output_schema=SCHEMA_TU_KHOA, effort="low", max_tokens=1500, bao=bao, bac="lite", suy_nghi=0)
        tk = doc_json(phien.gui(text=f"Đề:\n{de[:3000] or '(đề trong ảnh)'}", anh=anh).text)
        tk = {k: [str(x).strip() for x in (tk.get(k) or []) if str(x).strip()][:4] for k in ("doi", "chat", "phan_ung", "truy_van")}
    except (LoiAI, ValueError, AttributeError) as e:
        log.info("Không rút được từ khoá bằng AI: %s", e)
        tk = {"doi": [], "chat": [], "phan_ung": [], "truy_van": []}
    tk["doi"] = list(dict.fromkeys(dois + [d for x in tk["doi"] for d in tim_doi(x)]))
    return tk
