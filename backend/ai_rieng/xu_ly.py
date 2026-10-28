"""Việc nền của AI Riêng (một thread, không làm treo trang): xử lý hàng chờ tài liệu (đọc → tách đoạn → [trích bài chuỗi có lời
giải → bài mẫu chờ duyệt]) rồi nhúng các mục chưa có vector. Hàng chờ nằm trong CSDL (trang_thai) nên app khởi động lại vẫn làm tiếp.
AI dùng ở đây (đọc ảnh / trang scan, trích bài) chạy bằng key MÁY CHỦ và ghi chi phí như mọi lượt gọi khác (buoc ai_rieng_*).
"""
from __future__ import annotations

import logging
import re
import threading
import time
from pathlib import Path

from .. import db, giai_nhanh
from ..llm import KHOA_CHU_APP, KeHoachAI, LoiAI, PhienHoiThoai, doc_json, dung_ke_hoach, dung_khoa
from ..schemas import LoiGiaiGon
from . import kho, nhung, tach
from .song_song import ban_do

log = logging.getLogger(__name__)

_su_kien = threading.Event()
_khoa = threading.Lock()
_luong: threading.Thread | None = None
_nhung_loi = {"luc": 0.0, "ly_do": ""}
CHO_NHUNG_LAI = 600          # Gemini lỗi khi nhúng → thử lại sau 10 phút
CUA_SO_TRICH = 7000          # ký tự mỗi lần nhờ AI trích bài chuỗi có lời giải
TRICH_CUNG_LUC = 3           # số phần tài liệu AI trích cùng lúc

SYSTEM_TRICH = """Bạn đọc một đoạn tài liệu Hoá hữu cơ tiếng Việt và TRÍCH các bài CHUỖI PHẢN ỨNG ĐÃ CÓ LỜI GIẢI / ĐÁP ÁN trong đoạn đó.
- Chỉ lấy bài mà tài liệu đã giải (có chất / phương trình / đáp án). Bài chưa có lời giải trong đoạn → BỎ QUA, không tự giải.
- de: đề nguyên văn một dòng (các chất nối " → ", điều kiện trong ngoặc sau mũi tên). trang: số trang nếu đoạn có ghi "[Trang n]".
- chuoi: mọi chất của chuỗi (ki_hieu đúng như đề, ctct thu gọn, ten, smiles hợp lệ RDKit); buoc: mỗi mũi tên một phần tử (tu, den,
  phan_ung ngắn, dieu_kien) THEO LỜI GIẢI CỦA TÀI LIỆU; dap_an: một dòng tóm tắt.
- Không có bài nào → bai = []."""
SCHEMA_TRICH = {
    "type": "object",
    "properties": {"bai": {"type": "array", "items": {
        "type": "object",
        "properties": {"de": {"type": "string"}, "trang": {"type": "integer"},
                       "chuoi": giai_nhanh.SCHEMA_GON["properties"]["chuoi"], "buoc": giai_nhanh.SCHEMA_GON["properties"]["buoc"],
                       "dap_an": {"type": "string"}},
        "required": ["de", "chuoi", "buoc", "dap_an"]}}},
    "required": ["bai"],
}


def thu_muc_tep() -> Path:
    """File gốc của tài liệu: cạnh CSDL (data/ai_rieng – không commit; test: thư mục tạm)."""
    d = db.DUONG_DAN.parent / "ai_rieng"
    d.mkdir(parents=True, exist_ok=True)
    return d


def danh_thuc() -> None:
    _su_kien.set()


def chay_nen() -> None:
    """Khởi động thread nền (một lần). Tài liệu đang tách dở → xếp hàng lại."""
    global _luong
    with _khoa:
        if _luong is not None and _luong.is_alive():
            return
        if n := kho.dat_lai_dang_tach():
            log.info("AI Riêng: %d tài liệu đang tách dở → xếp hàng lại", n)
        kho.dong_lan_kiem_tra_do()
        _luong = threading.Thread(target=_vong, daemon=True, name="ai-rieng-nen")
        _luong.start()


def _vong() -> None:
    while True:
        try:
            xu_ly_het()
        except Exception:  # noqa: BLE001 – việc nền không bao giờ được chết
            log.exception("AI Riêng: lỗi việc nền")
        _su_kien.wait(60)
        _su_kien.clear()


def xu_ly_het() -> None:
    """Xử lý hết hàng chờ tài liệu, rồi nhúng các mục còn thiếu vector (test gọi trực tiếp)."""
    while (tl := kho.tai_lieu_cho_xu_ly()) is not None:
        xu_ly_tai_lieu(tl["id"])
    if time.time() - _nhung_loi["luc"] >= CHO_NHUNG_LAI and nhung.co_the():
        try:
            while nhung.nhung_con_thieu(200):
                pass
            _nhung_loi.update(luc=0.0, ly_do="")
        except LoiAI as e:
            _nhung_loi.update(luc=time.time(), ly_do=str(e))
            log.warning("AI Riêng: chưa nhúng được (%s) – thử lại sau %d phút", e, CHO_NHUNG_LAI // 60)


def trang_thai_nhung() -> dict:
    return {"co_the": nhung.co_the(), "loi": _nhung_loi["ly_do"], "thu_lai_luc": _nhung_loi["luc"] + CHO_NHUNG_LAI if _nhung_loi["luc"] else 0}


def xu_ly_tai_lieu(tid: int) -> None:
    tl = kho.lay_tai_lieu(tid)
    if tl is None:
        return
    kho.cap_nhat_tai_lieu(tid, trang_thai="dang_tach", tien_do="Đang đọc…", loi="")

    def tien_do(m: str) -> None:
        kho.cap_nhat_tai_lieu(tid, tien_do=m[:200])

    try:
        du_lieu = Path(tl["tep"]).read_bytes() if tl["tep"] else b""
        loai = tl["loai"]
        if loai == "link":
            tien_do("Đang tải trang…")
            loai, du_lieu, _url = tach.tai_link(tl["nguon"])  # link: tải lại mỗi lần xử lý (không lưu bản tải về)
        with dung_khoa(KHOA_CHU_APP), dung_ke_hoach(KeHoachAI(loai="giai", buoc="ai_rieng_nap")):
            trang_chu = tach.doc_noi_dung(loai, du_lieu, tien_do)
            tien_do("Đang tách đoạn…")
            doan = tach.tach_doan(trang_chu)
            if not doan:
                raise tach.LoiTaiLieu("Không có chữ nào để nạp.")
            kho.luu_doan(tid, doan)
            so_bai = 0
            if tl["co_loi_giai"]:
                so_bai = trich_bai(tid, trang_chu, tien_do)
        kho.cap_nhat_tai_lieu(tid, trang_thai="da_nap", so_doan=len(doan), so_bai_trich=so_bai, xong_luc=time.time(),
                              tien_do=f"{len(doan)} đoạn" + (f" · {so_bai} bài mẫu chờ duyệt" if tl["co_loi_giai"] else ""))
        log.info("AI Riêng: đã nạp tài liệu %s (%d đoạn, %d bài trích)", tid, len(doan), so_bai)
    except (tach.LoiTaiLieu, LoiAI, OSError) as e:
        kho.cap_nhat_tai_lieu(tid, trang_thai="loi", loi=str(e)[:500], tien_do="", xong_luc=time.time())
        log.warning("AI Riêng: tài liệu %s lỗi: %s", tid, e)
    except Exception as e:  # noqa: BLE001
        log.exception("AI Riêng: tài liệu %s lỗi không mong muốn", tid)
        kho.cap_nhat_tai_lieu(tid, trang_thai="loi", loi=f"Lỗi không mong muốn: {type(e).__name__}", tien_do="", xong_luc=time.time())


def _cua_so(trang_chu: list[tuple[int | None, str]]) -> list[str]:
    """Ghép chữ theo trang (đánh dấu [Trang n]) thành các cửa sổ ~CUA_SO_TRICH ký tự (không cắt giữa trang nếu được)."""
    out, hien = [], ""
    for trang, chu in trang_chu:
        khoi = (f"[Trang {trang}]\n" if trang else "") + (chu or "")
        while len(khoi) > CUA_SO_TRICH:
            out.append(khoi[:CUA_SO_TRICH])
            khoi = khoi[CUA_SO_TRICH - 600:]  # chồng một chút để không mất bài nằm ở chỗ cắt
        if hien and len(hien) + len(khoi) > CUA_SO_TRICH:
            out.append(hien)
            hien = ""
        hien += ("\n\n" if hien else "") + khoi
    if hien.strip():
        out.append(hien)
    return out


def trich_bai(tid: int, trang_chu: list[tuple[int | None, str]], tien_do=lambda _m: None) -> int:
    """Nhờ AI trích các bài chuỗi CÓ lời giải trong tài liệu → bài mẫu "chờ duyệt" (đã qua RDKit thì tầng 1 dùng được ngay nếu bật)."""
    tl = kho.lay_tai_lieu(tid) or {}
    cua_so = [c for c in _cua_so(trang_chu) if re.search(r"→|->|⟶|=>", c)]  # không có mũi tên → không có chuỗi
    so = 0
    da_co: set[str] = set()
    tien_do(f"AI trích bài chuỗi có lời giải ({len(cua_so)} phần, song song {min(TRICH_CUNG_LUC, max(1, len(cua_so)))} phần một lúc)")

    def trich(chu: str) -> list:
        ph = PhienHoiThoai(SYSTEM_TRICH, output_schema=SCHEMA_TRICH, effort="low", max_tokens=12000, bac="flash").gui(
            text="Đoạn tài liệu:\n" + chu)
        return (doc_json(ph.text) or {}).get("bai", [])

    for i, ds in enumerate(ban_do(trich, cua_so, TRICH_CUNG_LUC), 1):
        if isinstance(ds, Exception):
            log.info("AI Riêng: không trích được cửa sổ %d của tài liệu %s: %s", i, tid, ds)
            continue
        for bai in ds:
            de = giai_nhanh.chuan_de(str(bai.get("de", "")))
            if not de or kho.khoa_de(de) in da_co:
                continue
            try:
                gon = LoiGiaiGon.model_validate({"chuoi": bai.get("chuoi", []), "buoc": bai.get("buoc", []), "dap_an": bai.get("dap_an", "")})
            except ValueError:
                continue
            if not gon.chuoi or not gon.buoc:
                continue
            loi, _cb, _tb = giai_nhanh.kiem_tra(gon, de)
            try:
                kho.tao_bai_mau(de, gon.model_dump(), chu_de=tl.get("chu_de", ""), trang_thai="cho_duyet", nguon="tai_lieu", tai_lieu_id=tid,
                                trang=bai.get("trang") if isinstance(bai.get("trang"), int) else None, rdkit_ok=not loi,
                                mo_hinh=f"trích từ «{tl.get('ten', '')}»")
            except ValueError:
                continue
            da_co.add(kho.khoa_de(de))
            so += 1
    return so
