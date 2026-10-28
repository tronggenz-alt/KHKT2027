"""Tra cứu tài liệu khoa học (literature.py): DOI mở → toàn văn, DOI trả phí → tóm tắt + nút tải PDF, tìm theo từ khoá 3–5 bài,
cache DOI / tìm kiếm, PDF người dùng không cache, 429 → chờ rồi thử lại, tắt công tắc → không gọi API; tích hợp vào giải chuỗi.
HTTP giả lập (thay literature._get) – không gọi mạng."""
import json
import time
from types import SimpleNamespace

import pytest

from backend import db, giai_nhanh, literature as L, llm, pipeline
from backend.ai.providers.gemini import chat as gemini_chat, client as gemini_client
from backend.config import settings
from test_chon_mo_hinh import CHUOI, AIGia, _giai

DOI_MO, DOI_PHI = "10.1234/mo.2020.1", "10.1021/ja00073a029"
DOAN = ("In the Experimental section the pinacol rearrangement of 2,3-dimethylbutane-2,3-diol with sulfuric acid gives pinacolone "
        "through a tertiary carbocation and a 1,2-methyl shift, as shown in Scheme 1. ")


def tao_pdf(so_dong: int = 60) -> bytes:
    """PDF tối giản có chữ (để pypdf đọc) – mỗi dòng một câu về phản ứng pinacol."""
    dong = "".join(f"BT /F1 9 Tf 40 {780 - i * 12} Td (Line {i}: {DOAN[:110]}) Tj ET\n" for i in range(so_dong))
    noi_dung = dong.encode("latin-1")
    obj = [b"<</Type/Catalog/Pages 2 0 R>>", b"<</Type/Pages/Kids[3 0 R]/Count 1>>",
           b"<</Type/Page/Parent 2 0 R/MediaBox[0 0 612 792]/Contents 4 0 R/Resources<</Font<</F1 5 0 R>>>>>>",
           b"<</Length %d>>stream\n" % len(noi_dung) + noi_dung + b"endstream", b"<</Type/Font/Subtype/Type1/BaseFont/Helvetica>>"]
    out, vi_tri = b"%PDF-1.4\n", []
    for i, o in enumerate(obj, 1):
        vi_tri.append(len(out))
        out += b"%d 0 obj\n" % i + o + b"\nendobj\n"
    xref = len(out)
    out += b"xref\n0 %d\n0000000000 65535 f \n" % (len(obj) + 1) + b"".join(b"%010d 00000 n \n" % v for v in vi_tri)
    return out + b"trailer<</Size %d/Root 1 0 R>>\nstartxref\n%d\n%%%%EOF\n" % (len(obj) + 1, xref)


class Tl:
    def __init__(self, ma=200, j=None, text="", noi_dung=b"", loai="application/json", headers=None):
        self.status_code, self._j, self.text, self._b = ma, j, text, noi_dung
        self.headers = {"content-type": loai, **(headers or {})}
        self.encoding = "utf-8"

    def json(self):
        return self._j

    def iter_content(self, _n):
        yield self._b


class MangGia:
    """Bộ định tuyến HTTP giả: ghi lại URL đã gọi; trả dữ liệu theo nguồn."""

    def __init__(self, monkeypatch):
        self.goi: list[str] = []
        self.loi_429 = 0
        monkeypatch.setattr(L.requests, "get", self.get)  # giả ở tầng requests → logic chờ / thử lại khi 429 của _get chạy thật
        monkeypatch.setattr(L, "pubchem_chuan_hoa", lambda ten, han: [ten, f"{ten} (IUPAC)"])

    def get(self, url, params=None, headers=None, timeout=None, stream=False):
        assert headers["User-Agent"].isascii() and "mailto:lien-he@example.com" in headers["User-Agent"]
        self.goi.append(url)
        if "crossref.org/works/" in url:
            doi = url.rsplit("/works/", 1)[1]
            return Tl(j={"message": {"title": [f"Bài {doi}"], "author": [{"family": "Nguyen"}, {"family": "Tran"}], "container-title": ["J. Org. Chem."],
                                     "issued": {"date-parts": [[2020]]}, "abstract": f"<jats:p>Tóm tắt {doi} về pinacol rearrangement.</jats:p>",
                                     "is-referenced-by-count": 40}})
        if "unpaywall" in url:
            mo = DOI_MO in url
            return Tl(j={"is_oa": mo, "best_oa_location": {"url_for_pdf": "https://oa.example/bai.pdf"} if mo else None})
        if "openalex.org/works/doi:" in url or "semanticscholar.org/graph/v1/paper/DOI" in url:
            return Tl(404)
        if "europepmc" in url and "search" in url:
            return Tl(j={"resultList": {"result": []}})
        if url == "https://oa.example/bai.pdf":
            return Tl(noi_dung=tao_pdf(), loai="application/pdf")
        if "openalex.org/works" in url:
            return Tl(j={"results": [
                {"doi": "https://doi.org/10.1/a", "title": "Pinacol rearrangement mechanism study", "publication_year": 2019, "cited_by_count": 120,
                 "open_access": {"is_oa": True, "oa_url": "https://oa.example/bai.pdf"}, "abstract_inverted_index": {"Pinacol": [0], "study": [1]},
                 "authorships": [{"author": {"display_name": "A. Le"}}], "primary_location": {"source": {"display_name": "Org. Lett."}}},
                {"doi": "https://doi.org/10.1/b", "title": "Unrelated polymer paper", "publication_year": 2001, "cited_by_count": 3,
                 "open_access": {"is_oa": False}, "abstract_inverted_index": None, "authorships": [], "primary_location": {}},
                {"doi": "https://doi.org/10.1/c", "title": "Acid catalysed pinacol rearrangement kinetics", "publication_year": 1990, "cited_by_count": 15,
                 "open_access": {"is_oa": False}, "abstract_inverted_index": {"kinetics": [0]}, "authorships": [], "primary_location": {}}]})
        if "semanticscholar.org/graph/v1/paper/search" in url:
            if self.loi_429:
                self.loi_429 -= 1
                return Tl(429, headers={"Retry-After": "0"})
            return Tl(j={"data": [{"title": "Pinacol rearrangement mechanism study", "externalIds": {"DOI": "10.1/A"}, "citationCount": 130,
                                   "abstract": "Đầy đủ hơn: pinacol rearrangement via carbocation, methyl shift, pinacolone.", "authors": [], "year": 2019},
                                  {"title": "Semipinacol rearrangement review", "externalIds": {"DOI": "10.1/d"}, "citationCount": 300,
                                   "abstract": "Review of semipinacol rearrangement", "authors": [{"name": "B. Pham"}], "venue": "Chem. Rev.", "year": 2011}]})
        if "arxiv" in url:
            return Tl(text="<feed></feed>", loai="application/atom+xml")
        return Tl(404)


@pytest.fixture
def mang(monkeypatch, cau_hinh):
    monkeypatch.setattr(settings, "unpaywall_email", "lien-he@example.com")
    return MangGia(monkeypatch)


def test_tim_doi_trong_chu():
    assert L.tim_doi("Xem https://doi.org/10.1021/ja00073a029. và doi:10.3390/molecules25030515)") == [
        "10.1021/ja00073a029", "10.3390/molecules25030515"]
    assert L.tim_doi("CH4 → C2H2 → CH3CHO") == []


def test_doi_mo_doc_toan_van_va_cache(mang):
    kq = L.tra_cuu({"doi": [DOI_MO], "chat": ["pinacol"], "phan_ung": ["pinacol rearrangement"]})
    b = kq["bai"][0]
    assert (b["doi"], b["toan_van"], b["can_tai_pdf"], b["url"]) == (DOI_MO, True, False, f"https://doi.org/{DOI_MO}")
    assert "Unpaywall" in b["nguon_api"] and "Experimental" in kq["khoi_prompt"] and kq["so_tu"] <= L.TU_TOI_DA
    assert "trich" not in b and "tom_tat" not in b  # giao diện không nhận đoạn trích / toàn văn
    n = len(mang.goi)
    t = time.monotonic()
    kq2 = L.tra_cuu({"doi": [DOI_MO], "chat": ["pinacol"]})
    assert len(mang.goi) == n and kq2["tu_cache"] and kq2["bai"][0]["toan_van"] and time.monotonic() - t < 0.5
    with db.ket_noi() as con:
        luu = json.loads(con.execute("SELECT data FROM lit_cache WHERE key = ?", (f"doi:{DOI_MO}",)).fetchone()[0])
    assert len(luu["trich"].split()) <= L.TU_TOI_DA and "toan_van" in luu  # chỉ lưu đoạn trích ngắn, không lưu toàn văn


def test_doi_tra_phi_chi_tom_tat_va_pdf_nguoi_dung(mang):
    kq = L.tra_cuu({"doi": [DOI_PHI], "chat": ["pinacol"]})
    b = kq["bai"][0]
    assert (b["toan_van"], b["can_tai_pdf"]) == (False, True) and "Tóm tắt" in kq["khoi_prompt"]
    kq = L.tra_cuu({"doi": [], "chat": ["pinacol"]}, pdf=(tao_pdf(), DOI_PHI))  # người dùng tải PDF họ có quyền đọc
    b = kq["bai"][0]
    assert (b["toan_van"], b["nguon_toan_van"]) == (True, "PDF bạn tải lên") and "Experimental" in kq["khoi_prompt"]
    with db.ket_noi() as con:  # PDF người dùng KHÔNG vào cache (cache DOI vẫn là bản tóm tắt)
        luu = json.loads(con.execute("SELECT data FROM lit_cache WHERE key = ?", (f"doi:{DOI_PHI}",)).fetchone()[0])
    assert luu["toan_van"] is False and "Experimental" not in luu["trich"]


def test_tim_tu_khoa_gop_xep_hang_va_429(mang):
    mang.loi_429 = 1  # Semantic Scholar 429 lần đầu → chờ rồi thử lại
    kq = L.tra_cuu({"doi": [], "chat": ["pinacol"], "phan_ung": ["pinacol rearrangement"], "truy_van": ["pinacol rearrangement mechanism"]})
    ds = kq["bai"]
    assert 3 <= len(ds) <= 5 and len({b["doi"] for b in ds}) == len(ds)  # gộp trùng DOI (10.1/a = 10.1/A)
    dau = ds[0]
    assert dau["doi"] == "10.1/a" and dau["toan_van"] and set(dau["nguon_api"]) >= {"OpenAlex", "Semantic Scholar"}  # có bản mở + khớp → đầu
    assert sum("semanticscholar" in u for u in mang.goi) == 2
    n = len(mang.goi)
    L.tra_cuu({"doi": [], "chat": ["pinacol"], "phan_ung": ["pinacol rearrangement"], "truy_van": ["pinacol rearrangement mechanism"]})
    assert len(mang.goi) == n  # cache tìm kiếm (7 ngày) + đoạn trích theo DOI → không gọi lại


def test_het_gio_dung_nhung_gi_da_co(mang, monkeypatch):
    def cham(url, **kw):
        time.sleep(0.3)
        return MangGia.get(mang, url, **kw)
    monkeypatch.setattr(L.requests, "get", cham)
    t = time.monotonic()
    L.tra_cuu({"doi": [], "chat": ["pinacol"], "truy_van": ["pinacol"]}, han_giay=2)
    assert time.monotonic() - t < 4


# ================================================================ tích hợp vào giải chuỗi
@pytest.fixture
def ai(monkeypatch, cau_hinh, tmp_path):
    for ten, gt in (("gemini_model_lite", "lite-test"), ("gemini_model", "flash-test"), ("gemini_model_hsg", "pro-test"), ("model", "claude-test")):
        monkeypatch.setattr(settings, ten, gt)
    monkeypatch.setattr(llm, "_claude_tam_dung_den", 0.0)
    monkeypatch.setattr(pipeline, "OUTPUT_DIR", tmp_path / "out")
    g = AIGia(monkeypatch)
    g.prompt = []
    goc = g._gemini

    def gemini(**kw):
        g.prompt.append(" ".join(getattr(p, "text", "") or "" for c in kw["contents"] for p in (getattr(c, "parts", None) or [])))
        return goc(**kw)

    monkeypatch.setattr(gemini_chat, "_goi_gemini", gemini)
    return g


def _chay(muc="", tl="", chuoi=CHUOI):
    job = pipeline.tao_job()
    pipeline.chay(job, chuoi, None, False, muc_thong_minh=muc, tra_cuu_tai_lieu=tl, ke_hoach=llm.KeHoachAI(loai="giai"))
    assert job.loi is None, job.loi
    return job


def test_tat_cong_tac_khong_goi_api_tai_lieu(ai, monkeypatch):
    monkeypatch.setattr(L, "_get", lambda *a, **k: pytest.fail("không được gọi API tài liệu"))
    monkeypatch.setattr(L, "rut_tu_khoa", lambda *a, **k: pytest.fail("không được rút từ khoá"))
    for muc, tl in (("ky", "0"), ("nhanh", ""), ("can_bang", "")):  # tắt hẳn; tự động ở mức thấp, đề không DOI
        job = _chay(muc, tl)
        assert job.ket_qua["tai_lieu"] == [] and job.ket_qua["tra_cuu_tl"]["bat"] is False
        assert not any("TÀI LIỆU THAM KHẢO" in p for p in ai.prompt)


def test_bat_tra_cuu_dua_tai_lieu_vao_prompt_va_ket_qua(ai, mang, monkeypatch):
    monkeypatch.setattr(settings, "tra_cuu_tu_dong", True)
    monkeypatch.setattr(L, "rut_tu_khoa", lambda de, anh=None, bao=None: {"doi": L.tim_doi(de), "chat": ["pinacol"], "phan_ung": [],
                                                                          "truy_van": ["pinacol rearrangement mechanism"]})
    job = _chay("ky", "")  # tự động: mức Kỹ → bật
    kq = job.ket_qua
    assert 3 <= len(kq["tai_lieu"]) <= 5 and kq["tra_cuu_tl"]["bat"] and kq["tra_cuu_tl"]["token"] > 0
    assert any("TÀI LIỆU THAM KHẢO" in p and "can_cu" in p for p in ai.prompt)
    assert kq["loi_giai"]["nguon_tham_khao"][0] == "https://doi.org/10.1/a"  # chân ảnh đáp án
    assert any("Tìm thấy" in e["thong_diep"] and "bản mở" in e["thong_diep"] for e in job.su_kien if e["giai_doan"] == "tra_cuu")
    job = _chay("nhanh", "", chuoi=f"{CHUOI} (theo doi:{DOI_MO})")  # đề có DOI → tự bật kể cả mức Nhanh
    assert job.ket_qua["tai_lieu"][0]["doi"] == DOI_MO and job.ket_qua["tai_lieu"][0]["toan_van"]


def test_giai_nhanh_schema_co_can_cu_khi_co_tai_lieu():
    assert "can_cu" in giai_nhanh.SCHEMA_GON_TL["properties"]["buoc"]["items"]["required"]
    assert "can_cu" not in giai_nhanh.SCHEMA_GON["properties"]["buoc"]["items"]["properties"]
