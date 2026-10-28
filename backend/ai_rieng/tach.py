"""Đọc tài liệu cho AI Riêng → chữ theo trang → tách đoạn (~650 token, chồng lấp ~15%).

PDF (pypdf; trang scan không có chữ → AI đọc ảnh trang) · DOCX (đọc XML trong file, không cần thư viện) · ảnh (AI đọc chữ – máy
không có Tesseract) · link web (chỉ http/https, chặn địa chỉ nội bộ, ≤ 8 MB) · văn bản dán vào.
"""
from __future__ import annotations

import io
import ipaddress
import re
import socket
import zipfile
from typing import Callable
from urllib.parse import urljoin, urlparse
from xml.etree import ElementTree as ET

from ..llm import LoiAI

KY_TU_MOI_TOKEN = 4          # tiếng Việt + công thức: ~4 ký tự / token
DOAN_TOKEN = 650
CHONG_LAP = 0.15
LINK_TOI_DA = 8 * 1024 * 1024
TRANG_OCR_TOI_DA = 150        # trang scan tối đa nhờ AI đọc mỗi tài liệu (chi phí)
OCR_CUNG_LUC = 4              # số trang scan AI đọc cùng lúc


class LoiTaiLieu(Exception):
    pass


# ---------------------------------------------------------------- tách đoạn
def _don_vi(text: str, toi_da: int) -> list[str]:
    """Đoạn văn → câu khi quá dài; câu vẫn quá dài → cắt cứng."""
    out: list[str] = []
    for doan in re.split(r"\n\s*\n|\n(?=\s*(?:[-•*]|\d+[.)]|[a-zđ][.)])\s)", text):
        doan = re.sub(r"[ \t]+", " ", doan).strip()
        if not doan:
            continue
        if len(doan) <= toi_da:
            out.append(doan)
            continue
        for cau in re.split(r"(?<=[.!?;:])\s+", doan):
            while len(cau) > toi_da:
                out.append(cau[:toi_da])
                cau = cau[toi_da:]
            if cau.strip():
                out.append(cau.strip())
    return out


def tach_doan(trang_chu: list[tuple[int | None, str]], so_token: int = DOAN_TOKEN, chong: float = CHONG_LAP) -> list[tuple[int | None, str]]:
    """[(trang, chữ)] → [(trang bắt đầu, đoạn)]; mỗi đoạn ~so_token token, đoạn sau lặp lại ~chong phần cuối đoạn trước."""
    muc_tieu = so_token * KY_TU_MOI_TOKEN
    lap = int(muc_tieu * chong)
    don_vi = [(trang, dv) for trang, chu in trang_chu for dv in _don_vi(chu or "", muc_tieu)]
    out: list[tuple[int | None, str]] = []
    hien: list[tuple[int | None, str]] = []
    dai, co_moi = 0, False  # co_moi: có đơn vị mới sau lần cắt trước (không thì phần còn lại chỉ là phần chồng lấp)
    for trang, dv in don_vi:
        if hien and co_moi and dai + len(dv) > muc_tieu:
            out.append((hien[0][0], "\n".join(x[1] for x in hien)))
            giu: list[tuple[int | None, str]] = []
            n = 0
            for x in reversed(hien):  # chồng lấp: giữ các đơn vị cuối tổng ≤ lap ký tự
                if n + len(x[1]) > lap:
                    break
                giu.insert(0, x)
                n += len(x[1])
            if not giu:  # đơn vị cuối dài hơn phần chồng lấp → lấy phần đuôi của nó (bắt đầu ở đầu một từ)
                duoi = hien[-1][1][-lap:]
                duoi = duoi[duoi.find(" ") + 1:] if " " in duoi else duoi
                giu, n = [(hien[-1][0], duoi)], len(duoi)
            hien, dai, co_moi = giu, n, False
        hien.append((trang, dv))
        dai += len(dv) + 1
        co_moi = True
    if hien and co_moi:
        out.append((hien[0][0], "\n".join(x[1] for x in hien)))
    return out


# ---------------------------------------------------------------- PDF
def doc_pdf(du_lieu: bytes) -> list[tuple[int, str | None, bytes | None]]:
    """[(số trang, chữ hoặc None, ảnh trang scan nếu không có chữ)]."""
    from pypdf import PdfReader

    try:
        doc = PdfReader(io.BytesIO(du_lieu))
        if doc.is_encrypted:
            doc.decrypt("")
        out = []
        for i, trang in enumerate(doc.pages, 1):
            chu = (trang.extract_text() or "").strip()
            if len(chu) >= 40:
                out.append((i, chu, None))
                continue
            anh = None
            try:
                ds = sorted(trang.images, key=lambda im: len(im.data), reverse=True)
                anh = ds[0].data if ds else None
            except Exception:  # noqa: BLE001 – ảnh lạ pypdf không đọc được
                anh = None
            out.append((i, chu or None, anh))
        return out
    except Exception as e:  # noqa: BLE001
        raise LoiTaiLieu(f"Không đọc được PDF ({type(e).__name__}) – file hỏng hoặc có mật khẩu.") from e


# ---------------------------------------------------------------- DOCX
_W = "{http://schemas.openxmlformats.org/wordprocessingml/2006/main}"


def doc_docx(du_lieu: bytes) -> str:
    """Đoạn văn + bảng (mỗi hàng một dòng, ô cách nhau " | "); chỉ số dưới/trên giữ nguyên ký tự."""
    try:
        with zipfile.ZipFile(io.BytesIO(du_lieu)) as z:
            xml = z.read("word/document.xml")
    except (zipfile.BadZipFile, KeyError) as e:
        raise LoiTaiLieu("File Word không hợp lệ (cần .docx, không phải .doc cũ).") from e
    goc = ET.fromstring(xml)
    than = goc.find(f"{_W}body")
    dong: list[str] = []

    def chu_p(p) -> str:
        phan = []
        for el in p.iter():
            if el.tag == f"{_W}t":
                phan.append(el.text or "")
            elif el.tag == f"{_W}tab":
                phan.append("\t")
            elif el.tag in (f"{_W}br", f"{_W}cr"):
                phan.append("\n")
        return "".join(phan)

    for khoi in (than if than is not None else []):
        if khoi.tag == f"{_W}p":
            dong.append(chu_p(khoi))
        elif khoi.tag == f"{_W}tbl":
            for tr in khoi.iter(f"{_W}tr"):
                o = [" ".join(chu_p(p) for p in tc.iter(f"{_W}p")).strip() for tc in tr.iter(f"{_W}tc")]
                dong.append(" | ".join(o))
            dong.append("")
    return "\n".join(dong)


# ---------------------------------------------------------------- link web (chống SSRF)
def _dia_chi_an_toan(host: str) -> None:
    try:
        ds = {ai[4][0] for ai in socket.getaddrinfo(host, None)}
    except socket.gaierror as e:
        raise LoiTaiLieu(f"Không tìm thấy trang {host}.") from e
    for ip in ds:
        a = ipaddress.ip_address(ip.split("%")[0])
        if a.is_private or a.is_loopback or a.is_link_local or a.is_reserved or a.is_multicast or a.is_unspecified:
            raise LoiTaiLieu("Không được tải từ địa chỉ nội bộ.")


def tai_link(url: str) -> tuple[str, bytes, str]:
    """→ (loại "pdf" | "html" | "txt", nội dung, URL cuối). Tự theo tối đa 4 lần chuyển hướng, kiểm tra địa chỉ ở MỖI lần."""
    import requests

    for _ in range(5):
        p = urlparse(url)
        if p.scheme not in ("http", "https") or not p.hostname or (p.port not in (None, 80, 443)):
            raise LoiTaiLieu("Link phải là http(s):// tới trang web công khai.")
        _dia_chi_an_toan(p.hostname)
        r = requests.get(url, timeout=(8, 25), stream=True, allow_redirects=False,
                         headers={"User-Agent": "Mozilla/5.0 (CHEMISTRY AI Rieng)", "Accept": "text/html,application/pdf;q=0.9,*/*;q=0.5"})
        if r.is_redirect or r.status_code in (301, 302, 303, 307, 308):
            url = urljoin(url, r.headers.get("location", ""))
            r.close()
            continue
        if r.status_code >= 400:
            raise LoiTaiLieu(f"Trang trả lỗi {r.status_code}.")
        du_lieu = b""
        for khoi in r.iter_content(65536):
            du_lieu += khoi
            if len(du_lieu) > LINK_TOI_DA:
                raise LoiTaiLieu("Trang quá lớn (tối đa 8 MB).")
        kieu = r.headers.get("content-type", "").lower()
        if "pdf" in kieu or du_lieu[:5] == b"%PDF-":
            return "pdf", du_lieu, url
        if "html" in kieu or du_lieu.lstrip()[:1] == b"<":
            return "html", du_lieu, url
        if kieu.startswith("text/"):
            return "txt", du_lieu, url
        raise LoiTaiLieu(f"Không đọc được loại nội dung {kieu or 'không rõ'}.")
    raise LoiTaiLieu("Trang chuyển hướng quá nhiều lần.")


def chu_html(du_lieu: bytes) -> str:
    from ..literature import chu_tu_html

    m = re.search(rb'charset=["\']?([\w-]+)', du_lieu[:3000])
    try:
        text = du_lieu.decode(m.group(1).decode() if m else "utf-8", "ignore")
    except LookupError:
        text = du_lieu.decode("utf-8", "ignore")
    return chu_tu_html(text)


# ---------------------------------------------------------------- ảnh → chữ (AI thị giác)
SYSTEM_OCR = """Chép lại NGUYÊN VĂN toàn bộ chữ trong ảnh (tài liệu Hoá học tiếng Việt), giữ thứ tự đọc và xuống dòng. Công thức hoá học
viết liền với chỉ số là số thường (CH3COOH, C2H5OH); mũi tên phản ứng viết "→", điều kiện trên mũi tên ghi trong ngoặc ngay sau mũi tên,
vd "C2H5OH →(H2SO4 đặc, 170°C) C2H4". Bảng: mỗi hàng một dòng, ô cách nhau " | ". Không bình luận, không thêm gì ngoài chữ trong ảnh."""


def anh_sang_chu(du_lieu: bytes) -> str:
    """Một ảnh (PNG/JPG/WEBP/HEIC) → chữ, nhờ AI rẻ đọc (key máy chủ do người gọi đặt bằng dung_khoa)."""
    from ..co_che_anh import _heic_sang_jpg, _la_heic
    from ..llm import PhienHoiThoai
    from ..parser import chuan_bi_anh

    if _la_heic(du_lieu):
        du_lieu = _heic_sang_jpg(du_lieu)
    try:
        anh = chuan_bi_anh(du_lieu, "JPEG")
    except LoiAI as e:
        raise LoiTaiLieu("Không đọc được ảnh (cần PNG / JPG / WEBP / HEIC).") from e
    ph = PhienHoiThoai(SYSTEM_OCR, effort="low", max_tokens=8000, bac="flash").gui(text="Chép lại chữ trong ảnh.", anh=anh)
    return ph.text.strip()


# ---------------------------------------------------------------- tổng hợp: file / link / văn bản → [(trang, chữ)]
def doc_noi_dung(loai: str, du_lieu: bytes, tien_do: Callable[[str], None] | None = None) -> list[tuple[int | None, str]]:
    bao = tien_do or (lambda _m: None)
    if loai == "pdf":
        trang = doc_pdf(du_lieu)
        can_doc = [(so, anh) for so, chu, anh in trang if not chu and anh is not None][:TRANG_OCR_TOI_DA]
        doc_duoc: dict[int, str] = {}
        if can_doc:  # trang scan: AI đọc SONG SONG (OCR_CUNG_LUC trang một lúc) – nhanh hơn đọc lần lượt nhiều lần
            from .song_song import ban_do

            bao(f"AI đọc {len(can_doc)} trang scan (song song {min(OCR_CUNG_LUC, len(can_doc))} trang một lúc)")
            for (so, _anh), kq in zip(can_doc, ban_do(lambda x: anh_sang_chu(x[1]), can_doc, OCR_CUNG_LUC)):
                if isinstance(kq, Exception):
                    bao(f"Trang {so}: không đọc được ({kq})")
                    kq = ""
                doc_duoc[so] = kq
        out: list[tuple[int | None, str]] = [(so, chu or doc_duoc.get(so, "")) for so, chu, _anh in trang if chu or so in doc_duoc]
        if not any(c for _, c in out):
            raise LoiTaiLieu("PDF không có chữ đọc được (trang scan không trích được ảnh).")
        return out
    if loai == "docx":
        return [(None, doc_docx(du_lieu))]
    if loai == "anh":
        bao("AI đang đọc chữ trong ảnh")
        return [(None, anh_sang_chu(du_lieu))]
    if loai == "html":
        return [(None, chu_html(du_lieu))]
    return [(None, du_lieu.decode("utf-8", "ignore"))]


def loai_tu_ten(ten: str, du_lieu: bytes) -> str:
    duoi = ten.lower().rsplit(".", 1)[-1] if "." in ten else ""
    if du_lieu[:5] == b"%PDF-" or duoi == "pdf":
        return "pdf"
    if duoi == "docx" or (du_lieu[:2] == b"PK" and duoi not in ("xlsx", "zip")):
        return "docx"
    if duoi in ("png", "jpg", "jpeg", "webp", "heic", "heif") or du_lieu[:8] == b"\x89PNG\r\n\x1a\n" or du_lieu[:3] == b"\xff\xd8\xff":
        return "anh"
    if duoi in ("txt", "md", "csv"):
        return "van_ban"
    if duoi in ("html", "htm"):
        return "html"
    raise LoiTaiLieu(f"Chưa hỗ trợ loại file .{duoi or '?'} (dùng PDF, DOCX, ảnh, TXT).")
