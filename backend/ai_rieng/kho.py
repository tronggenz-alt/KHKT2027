"""Kho tri thức của AI Riêng (CSDL – bảng ar_* trong db.py): cài đặt, phiên bản kho, 4 loại dữ liệu (tài liệu → đoạn, quy tắc,
bài mẫu = thư viện lời giải, sửa sai) + chỉ mục chữ FTS5, hàng chờ báo sai, bộ đề kiểm tra, nhật ký từng lần giải.

Mọi thay đổi nội dung → tang_phien_ban() (bộ nhớ vector nạp lại; thống kê). vec = NULL nghĩa là chưa có embedding → việc nền tính sau.
"""
from __future__ import annotations

import json
import re
import time
import unicodedata
from typing import Any

from .. import db, parser

# Chủ đề gợi ý (admin gõ chủ đề khác cũng được – lưu dạng "anken, ancol")
CHU_DE = ["ankan", "anken", "ankađien", "ankin", "aren (benzen)", "dẫn xuất halogen", "ancol", "phenol", "anđehit", "xeton",
          "axit cacboxylic", "este", "lipit", "cacbohiđrat", "amin", "amino axit", "peptit – protein", "polime", "tổng hợp"]

SYSTEM_MAC_DINH = """Bạn là "AI Riêng – Gia sư Hoá Hữu Cơ" của giáo viên quản trị app. Mỗi đề gửi kèm KHO KIẾN THỨC RIÊNG mà giáo viên đã
dạy bạn: SỬA SAI [S…], QUY TẮC [Q…], BÀI MẪU [M…], TÀI LIỆU [T…].
- Ưu tiên kiến thức trong kho hơn hiểu biết chung: sửa sai > quy tắc (độ ưu tiên cao trước) > bài mẫu > tài liệu.
- Kho mâu thuẫn với hiểu biết chung → làm theo kho, ghi một dòng ngắn vào mau_thuan.
- Trình bày giống bài mẫu (cách gọi tên chất, cách ghi điều kiện trên mũi tên).
- Không bịa: phần kho không có thì làm theo CHẾ ĐỘ ghi ở cuối đề.
- [MODULE: BIỆN LUẬN CHẤT HỮU CƠ]: Với các câu hỏi xác định chất ẩn, chuỗi phản ứng, biện luận CTCT:
  Trình bày ngắn gọn bằng ký hiệu `->`, không văn mẫu, đi thẳng vào bản chất theo 4 bước:
  1. Δ & Nhóm chức: Tính Δ = (2C + 2 + N − H − X)/2. Dịch nhanh: [Dữ kiện] -> [Cấu tạo/Nhóm chức/Phản ứng].
  2. Truy vết sơ đồ: Biện luận từng nấc (Chất A -> Chất B). Chỉ nêu tác nhân, sự biến đổi khung C/nhóm chức và cơ chế trọng tâm.
  3. Tổng hợp CTCT: Liệt kê rõ CTCT thu gọn của tất cả các chất chưa biết.
  4. Kiểm chứng (Bắt buộc): Check nhanh với [CTPT, Δ, số mol/khối lượng, lập thể/đồng phân]. Lệch dữ kiện -> tự sửa trước khi chốt."""

MAC_DINH: dict[str, Any] = {
    "bat": True,                  # tắt → trang giải không còn lựa chọn "AI Riêng" (Sân tập của admin vẫn dùng được)
    "mo_cho_nguoi_dung": False,   # người dùng thường thấy lựa chọn "AI Riêng" (admin luôn thấy)
    "che_do": "bo_sung",          # nghiem_ngat: chỉ dùng kiến thức đã dạy | bo_sung: cho phép kiến thức chung (đánh dấu)
    "khi_chua_day": "bao",        # nghiêm ngặt mà chưa được dạy: bao (báo + nút AI thường) | ai_thuong (tự giải bằng AI thường)
    "top_k": 8,                   # số mục kiến thức lấy ra mỗi lần
    "mo_hinh_t2": "lite",         # tầng 2: lite (Gemini Flash-Lite, rẻ nhất) | flash | openai | claude (chỉ người có quyền Claude)
    "cho_phep_t3": True,          # RDKit báo sai / hai AI bất đồng → tầng 3: mô hình mạnh (Gemini Pro / Claude nếu có quyền), cùng kho
    "song_song_gemini": True,     # giải SONG SONG với Gemini (cùng kho kiến thức) để đối chiếu – nhanh hơn chờ tầng 3, chính xác hơn
    "cho_gemini": "khi_can",      # khi_can: AI Riêng chắc chắn (RDKit sạch + mọi bước có căn cứ trong kho) thì trả ngay, Gemini đối
                                  # chiếu ở nền · luon: luôn chờ Gemini đối chiếu xong rồi mới trả (chậm hơn, chắc hơn)
    "dung_cho_duyet": True,       # tầng 1 dùng cả lời giải AI / trích từ tài liệu ĐÃ QUA RDKit nhưng chưa duyệt
    "luu_tu_dong": True,          # lời giải AI qua RDKit → tự lưu vào thư viện (chờ duyệt)
    "bo_qua_thu_vien_khi_kiem_tra": True,  # Bộ đề kiểm tra: không lấy thẳng từ thư viện (đo đúng khả năng AI Riêng)
    "cham_bang_ai": True,         # chấm Bộ đề: chất không so được bằng SMILES / chữ → nhờ AI rẻ so
    "nguong_vector": 0.55,        # độ giống tối thiểu (cosine) để một mục được coi là liên quan
    "k_doan": 6,                  # tối đa số đoạn tài liệu trong top_k mỗi lần
    "k_kien_thuc": 8,             # tối đa số quy tắc trong top_k mỗi lần
    "system_prompt": SYSTEM_MAC_DINH,
}
_CHON = {"che_do": ("nghiem_ngat", "bo_sung"), "khi_chua_day": ("bao", "ai_thuong"), "mo_hinh_t2": ("lite", "flash", "openai", "claude"),
         "cho_gemini": ("khi_can", "luon")}
KHOA_CAI_DAT = "ai_rieng"
KHOA_PHIEN_BAN = "ai_rieng_phien_ban"


# ================================================================ cài đặt + phiên bản kho
def cai_dat() -> dict:
    try:
        luu = json.loads(db.cai_dat(KHOA_CAI_DAT, "{}") or "{}")
    except ValueError:
        luu = {}
    cd = {**MAC_DINH, **{k: v for k, v in luu.items() if k in MAC_DINH}}
    return {**cd, **_ten_giao_dien(cd)}


# Tên trường của form Cài đặt ở /admin/ai-rieng (ai-rieng.js) ↔ khoá cài đặt của backend
_NGHIEM_NGAT = {"tat": ("bo_sung", None), "canh_bao": ("nghiem_ngat", "ai_thuong"), "tu_choi": ("nghiem_ngat", "bao")}
_MO_HINH = {"": "lite", "gemini": "flash", "openai": "openai", "claude": "claude"}


def _ten_giao_dien(cd: dict) -> dict:
    nghiem = "tat" if cd["che_do"] == "bo_sung" else ("canh_bao" if cd["khi_chua_day"] == "ai_thuong" else "tu_choi")
    return {"tu_luu_thu_vien": cd["luu_tu_dong"], "che_do_nghiem_ngat": nghiem,
            "mo_hinh": {v: k for k, v in _MO_HINH.items()}.get(cd["mo_hinh_t2"], "")}


def _tu_giao_dien(moi: dict) -> dict:
    """Form gửi tên trường giao diện → đổi sang khoá backend (khoá backend gửi thẳng vẫn được)."""
    moi = dict(moi or {})
    if "tu_luu_thu_vien" in moi:
        moi.setdefault("luu_tu_dong", moi.pop("tu_luu_thu_vien"))
    if "che_do_nghiem_ngat" in moi:
        che_do, khi = _NGHIEM_NGAT.get(moi.pop("che_do_nghiem_ngat"), (None, None))
        if che_do:
            moi.setdefault("che_do", che_do)
        if khi:
            moi.setdefault("khi_chua_day", khi)
    if "mo_hinh" in moi:
        m = _MO_HINH.get(moi.pop("mo_hinh") or "")
        if m:
            moi.setdefault("mo_hinh_t2", m)
    if "k_doan" in moi or "k_kien_thuc" in moi:  # tổng số mục mỗi lần = đoạn + quy tắc + chỗ cho sửa sai / bài mẫu
        kd, kk = int(moi.get("k_doan") or MAC_DINH["k_doan"]), int(moi.get("k_kien_thuc") or MAC_DINH["k_kien_thuc"])
        moi.setdefault("top_k", min(30, kd + kk + 4))
    return moi


def dat_cai_dat(moi: dict) -> dict:
    """Kiểm tra + lưu (chỉ các khoá đã biết). Sai kiểu / ngoài danh sách → ValueError (tiếng Việt)."""
    cd = {k: v for k, v in cai_dat().items() if k in MAC_DINH}
    for k, v in _tu_giao_dien(moi).items():
        if k not in MAC_DINH:
            continue
        mac = MAC_DINH[k]
        if k in _CHON and v not in _CHON[k]:
            raise ValueError(f"Giá trị không hợp lệ cho {k}.")
        if isinstance(mac, bool):
            v = bool(v)
        elif k in ("top_k", "k_doan", "k_kien_thuc"):
            v = int(v)
            if not 1 <= v <= 30:
                raise ValueError("Số mục kiến thức mỗi lần phải từ 1 đến 30.")
        elif k == "nguong_vector":
            v = float(v)
            if not 0 <= v <= 0.95:
                raise ValueError("Ngưỡng liên quan phải từ 0 đến 0,95.")
        elif k == "system_prompt":
            v = str(v).strip() or SYSTEM_MAC_DINH
            if len(v) > 6000:
                raise ValueError("System prompt tối đa 6.000 ký tự.")
        cd[k] = v
    db.dat_cai_dat(KHOA_CAI_DAT, json.dumps({k: cd[k] for k in MAC_DINH}, ensure_ascii=False))
    return cai_dat()


def phien_ban() -> int:
    try:
        return int(db.cai_dat(KHOA_PHIEN_BAN, "0") or 0)
    except ValueError:
        return 0


def tang_phien_ban() -> int:
    with db.ket_noi() as con:
        r = con.execute("SELECT value FROM app_settings WHERE key = ?", (KHOA_PHIEN_BAN,)).fetchone()
        moi = int(r[0] or 0) + 1 if r else 1
        con.execute("INSERT OR REPLACE INTO app_settings (key, value) VALUES (?, ?)", (KHOA_PHIEN_BAN, str(moi)))
    return moi


# ================================================================ chuẩn hoá
_FORMULA = re.compile(r"\d|[A-Z].*[A-Z]")


def khoa_de(de: str) -> str:
    """Khoá khớp thư viện: bỏ khoảng trắng, thống nhất mũi tên / chỉ số dưới; giữ hoa-thường của công thức (CO ≠ Co), tên chất thì
    không phân biệt hoa-thường ("Etilen" = "etilen")."""
    t = unicodedata.normalize("NFC", parser.chuan_hoa(de or ""))
    t = re.sub(r"\s*(?:-+>|=+>|⟶|→|➝|➔)\s*", "→", t)
    t = re.sub(r"[.;,\s]+$", "", t.strip())
    t = re.sub(r"[\w']+", lambda m: m.group(0) if _FORMULA.search(m.group(0)) else m.group(0).casefold(), t)
    return re.sub(r"\s+", "", t)


def chu_tim(text: str) -> str:
    """Chữ đưa vào FTS (và câu tìm): chỉ số dưới → số, đ → d (FTS chỉ bỏ dấu thanh, không đổi đ)."""
    return parser.chuan_hoa(text or "").replace("đ", "d").replace("Đ", "D")


def tom_tat_loi_giai(lg: dict) -> str:
    """Lời giải gọn (dict) → vài dòng chữ: dùng trong prompt (bài mẫu few-shot), FTS và embedding."""
    chat = "; ".join(f"{c.get('ki_hieu', '')} = {c.get('ctct') or c.get('smiles', '')}" + (f" ({c['ten']})" if c.get("ten") else "")
                     for c in lg.get("chuoi", []))
    buoc = "\n".join(f"  {i}. {b.get('tu', '')} → {b.get('den', '')}: {b.get('phan_ung', '')}"
                     + (f" [{b['dieu_kien']}]" if b.get("dieu_kien") else "") + (f" – {b['phuong_trinh']}" if b.get("phuong_trinh") else "")
                     for i, b in enumerate(lg.get("buoc", []), 1))
    dong = [f"Các chất: {chat}"] if chat else []
    if buoc:
        dong.append("Các bước:\n" + buoc)
    if lg.get("luu_y"):
        dong.append("Lưu ý: " + "; ".join(lg["luu_y"]))
    if lg.get("dap_an"):
        dong.append("Đáp án: " + lg["dap_an"])
    return "\n".join(dong)


def _hang(r) -> dict:
    d = dict(r)
    d.pop("vec", None)
    return d


# ================================================================ FTS (chỉ mục chữ)
def _fts_ghi(con, loai: str, ref_id: int, noi_dung: str) -> None:
    con.execute("DELETE FROM ar_fts WHERE loai = ? AND ref_id = ?", (loai, ref_id))
    con.execute("INSERT INTO ar_fts (noi_dung, loai, ref_id) VALUES (?, ?, ?)", (chu_tim(noi_dung), loai, ref_id))


def _fts_xoa(con, loai: str, ref_id: int) -> None:
    con.execute("DELETE FROM ar_fts WHERE loai = ? AND ref_id = ?", (loai, ref_id))


def chu_cua(loai: str, d: dict, ten_tai_lieu: str = "") -> str:
    """Chữ đại diện của một mục (FTS + embedding)."""
    if loai == "doan":
        return (f"{ten_tai_lieu}\n" if ten_tai_lieu else "") + d["noi_dung"]
    if loai == "kien_thuc":
        return f"{d['tieu_de']}\n{d['noi_dung']}\nChủ đề: {d.get('chu_de', '')}"
    if loai == "bai_mau":
        lg = d["loi_giai"] if isinstance(d["loi_giai"], dict) else json.loads(d["loi_giai"] or "{}")
        return f"Đề: {d['de']}\n{tom_tat_loi_giai(lg)}"
    return f"Đề: {d['de']}\nSai: {d.get('sai', '')}\nĐúng: {d['dung']}\nLý do: {d.get('ly_do', '')}"


# ================================================================ tài liệu + đoạn
def tao_tai_lieu(ten: str, loai: str, nguon: str = "", tep: str = "", chu_de: str = "", co_loi_giai: bool = False,
                 kich_thuoc: int = 0) -> int:
    with db.ket_noi() as con:
        cur = con.execute("INSERT INTO ar_tai_lieu (ten, loai, nguon, tep, chu_de, co_loi_giai, kich_thuoc, tao_luc) "
                          "VALUES (?, ?, ?, ?, ?, ?, ?, ?)", (ten[:200], loai, nguon[:500], tep, chu_de[:200], int(co_loi_giai),
                                                              kich_thuoc, time.time()))
        return int(cur.lastrowid)


def ds_tai_lieu() -> list[dict]:
    with db.ket_noi() as con:
        return [dict(r) for r in con.execute("SELECT * FROM ar_tai_lieu ORDER BY id DESC")]


def lay_tai_lieu(tid: int) -> dict | None:
    with db.ket_noi() as con:
        r = con.execute("SELECT * FROM ar_tai_lieu WHERE id = ?", (tid,)).fetchone()
    return dict(r) if r else None


def cap_nhat_tai_lieu(tid: int, **truong) -> None:
    cho_phep = {"ten", "chu_de", "bat", "trang_thai", "tien_do", "loi", "so_doan", "so_bai_trich", "xong_luc", "tep", "co_loi_giai"}
    truong = {k: v for k, v in truong.items() if k in cho_phep}
    if not truong:
        return
    with db.ket_noi() as con:
        con.execute(f"UPDATE ar_tai_lieu SET {', '.join(f'{k} = ?' for k in truong)} WHERE id = ?", (*truong.values(), tid))
    if {"bat", "chu_de", "ten"} & truong.keys():
        tang_phien_ban()


def xoa_tai_lieu(tid: int) -> dict | None:
    d = lay_tai_lieu(tid)
    if d is None:
        return None
    with db.ket_noi() as con:
        for (did,) in con.execute("SELECT id FROM ar_doan WHERE tai_lieu_id = ?", (tid,)).fetchall():
            _fts_xoa(con, "doan", did)
        con.execute("DELETE FROM ar_doan WHERE tai_lieu_id = ?", (tid,))
        con.execute("DELETE FROM ar_tai_lieu WHERE id = ?", (tid,))
    tang_phien_ban()
    return d


def luu_doan(tid: int, doan: list[tuple[int | None, str]]) -> int:
    """Thay toàn bộ đoạn của một tài liệu (xử lý lại)."""
    ten = (lay_tai_lieu(tid) or {}).get("ten", "")
    with db.ket_noi() as con:
        for (did,) in con.execute("SELECT id FROM ar_doan WHERE tai_lieu_id = ?", (tid,)).fetchall():
            _fts_xoa(con, "doan", did)
        con.execute("DELETE FROM ar_doan WHERE tai_lieu_id = ?", (tid,))
        for i, (trang, noi_dung) in enumerate(doan, 1):
            cur = con.execute("INSERT INTO ar_doan (tai_lieu_id, thu_tu, trang, noi_dung) VALUES (?, ?, ?, ?)", (tid, i, trang, noi_dung))
            _fts_ghi(con, "doan", int(cur.lastrowid), chu_cua("doan", {"noi_dung": noi_dung}, ten))
    tang_phien_ban()
    return len(doan)


def ds_doan(tid: int) -> list[dict]:
    with db.ket_noi() as con:
        return [{**_hang(r), "co_vec": r["vec"] is not None} for r in
                con.execute("SELECT * FROM ar_doan WHERE tai_lieu_id = ? ORDER BY thu_tu", (tid,))]


def sua_doan(did: int, noi_dung: str) -> bool:
    noi_dung = noi_dung.strip()
    with db.ket_noi() as con:
        r = con.execute("SELECT d.id, t.ten FROM ar_doan d JOIN ar_tai_lieu t ON t.id = d.tai_lieu_id WHERE d.id = ?", (did,)).fetchone()
        if r is None or not noi_dung:
            return False
        con.execute("UPDATE ar_doan SET noi_dung = ?, vec = NULL WHERE id = ?", (noi_dung, did))
        _fts_ghi(con, "doan", did, chu_cua("doan", {"noi_dung": noi_dung}, r["ten"]))
    tang_phien_ban()
    return True


def xoa_doan(did: int) -> bool:
    with db.ket_noi() as con:
        r = con.execute("SELECT tai_lieu_id FROM ar_doan WHERE id = ?", (did,)).fetchone()
        if r is None:
            return False
        _fts_xoa(con, "doan", did)
        con.execute("DELETE FROM ar_doan WHERE id = ?", (did,))
        con.execute("UPDATE ar_tai_lieu SET so_doan = (SELECT COUNT(*) FROM ar_doan WHERE tai_lieu_id = ?) WHERE id = ?",
                    (r["tai_lieu_id"], r["tai_lieu_id"]))
    tang_phien_ban()
    return True


def tai_lieu_cho_xu_ly() -> dict | None:
    with db.ket_noi() as con:
        r = con.execute("SELECT * FROM ar_tai_lieu WHERE trang_thai = 'cho' ORDER BY id LIMIT 1").fetchone()
    return dict(r) if r else None


def dat_lai_dang_tach() -> int:
    """Khởi động: tài liệu đang tách dở (app tắt giữa chừng) → xếp hàng lại."""
    with db.ket_noi() as con:
        return con.execute("UPDATE ar_tai_lieu SET trang_thai = 'cho', tien_do = '' WHERE trang_thai = 'dang_tach'").rowcount


# ================================================================ quy tắc / kiến thức
def _kiem_kien_thuc(tieu_de: str, noi_dung: str, uu_tien: int) -> tuple[str, str, int]:
    tieu_de, noi_dung = (tieu_de or "").strip(), (noi_dung or "").strip()
    if not tieu_de or not noi_dung:
        raise ValueError("Cần tiêu đề và nội dung.")
    if len(tieu_de) > 300 or len(noi_dung) > 8000:
        raise ValueError("Tiêu đề tối đa 300 ký tự, nội dung tối đa 8.000 ký tự.")
    return tieu_de, noi_dung, min(5, max(1, int(uu_tien or 3)))


def tao_kien_thuc(tieu_de: str, noi_dung: str, chu_de: str = "", uu_tien: int = 3, bat: bool = True) -> int:
    tieu_de, noi_dung, uu_tien = _kiem_kien_thuc(tieu_de, noi_dung, uu_tien)
    bay_gio = time.time()
    with db.ket_noi() as con:
        cur = con.execute("INSERT INTO ar_kien_thuc (tieu_de, noi_dung, chu_de, uu_tien, bat, tao_luc, sua_luc) VALUES (?, ?, ?, ?, ?, ?, ?)",
                          (tieu_de, noi_dung, (chu_de or "")[:200], uu_tien, int(bat), bay_gio, bay_gio))
        kid = int(cur.lastrowid)
        _fts_ghi(con, "kien_thuc", kid, chu_cua("kien_thuc", {"tieu_de": tieu_de, "noi_dung": noi_dung, "chu_de": chu_de}))
    tang_phien_ban()
    return kid


def sua_kien_thuc(kid: int, tieu_de: str, noi_dung: str, chu_de: str = "", uu_tien: int = 3, bat: bool = True) -> bool:
    tieu_de, noi_dung, uu_tien = _kiem_kien_thuc(tieu_de, noi_dung, uu_tien)
    with db.ket_noi() as con:
        cu = con.execute("SELECT tieu_de, noi_dung, chu_de FROM ar_kien_thuc WHERE id = ?", (kid,)).fetchone()
        if cu is None:
            return False
        doi_chu = (cu["tieu_de"], cu["noi_dung"], cu["chu_de"]) != (tieu_de, noi_dung, chu_de or "")
        con.execute(f"UPDATE ar_kien_thuc SET tieu_de = ?, noi_dung = ?, chu_de = ?, uu_tien = ?, bat = ?, sua_luc = ?"
                    f"{', vec = NULL' if doi_chu else ''} WHERE id = ?", (tieu_de, noi_dung, (chu_de or "")[:200], uu_tien, int(bat),
                                                                         time.time(), kid))
        _fts_ghi(con, "kien_thuc", kid, chu_cua("kien_thuc", {"tieu_de": tieu_de, "noi_dung": noi_dung, "chu_de": chu_de}))
    tang_phien_ban()
    return True


def ds_kien_thuc(chu_de: str = "", q: str = "") -> list[dict]:
    sql, ts = "SELECT * FROM ar_kien_thuc WHERE 1 = 1", []
    if chu_de:
        sql += " AND (',' || REPLACE(chu_de, ', ', ',') || ',') LIKE ?"
        ts.append(f"%,{chu_de},%")
    if q:
        sql += " AND (tieu_de LIKE ? OR noi_dung LIKE ?)"
        ts += [f"%{q}%", f"%{q}%"]
    with db.ket_noi() as con:
        return [{**_hang(r), "co_vec": r["vec"] is not None} for r in con.execute(sql + " ORDER BY uu_tien DESC, id DESC", ts)]


def xoa_kien_thuc(kid: int) -> bool:
    with db.ket_noi() as con:
        n = con.execute("DELETE FROM ar_kien_thuc WHERE id = ?", (kid,)).rowcount
        _fts_xoa(con, "kien_thuc", kid)
    if n:
        tang_phien_ban()
    return bool(n)


# ================================================================ bài mẫu (= thư viện lời giải, tầng 1)
def _loi_giai_hop_le(lg: dict) -> dict:
    if not isinstance(lg, dict) or not lg.get("chuoi") or not lg.get("buoc"):
        raise ValueError("Lời giải cần ít nhất một chất và một bước.")
    return {"chuoi": lg["chuoi"], "buoc": lg["buoc"], "luu_y": list(lg.get("luu_y") or []), "dap_an": str(lg.get("dap_an") or "")}


def tao_bai_mau(de: str, loi_giai: dict, chu_de: str = "", trang_thai: str = "duyet", nguon: str = "tay", tai_lieu_id: int | None = None,
                trang: int | None = None, rdkit_ok: bool = False, mo_hinh: str = "", bat: bool = True) -> int:
    de = (de or "").strip()
    if not de:
        raise ValueError("Cần đề bài.")
    lg = _loi_giai_hop_le(loi_giai)
    bay_gio = time.time()
    with db.ket_noi() as con:
        cur = con.execute(
            "INSERT INTO ar_bai_mau (de, de_khoa, loi_giai, chu_de, trang_thai, nguon, tai_lieu_id, trang, rdkit_ok, mo_hinh, bat, tao_luc, sua_luc) "
            "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
            (de[:4000], khoa_de(de), json.dumps(lg, ensure_ascii=False), (chu_de or "")[:200], trang_thai, nguon, tai_lieu_id, trang,
             int(rdkit_ok), mo_hinh[:120], int(bat), bay_gio, bay_gio))
        bid = int(cur.lastrowid)
        _fts_ghi(con, "bai_mau", bid, chu_cua("bai_mau", {"de": de, "loi_giai": lg}))
    tang_phien_ban()
    return bid


def sua_bai_mau(bid: int, de: str, loi_giai: dict, chu_de: str = "", trang_thai: str | None = None, rdkit_ok: bool | None = None,
                bat: bool = True) -> bool:
    de = (de or "").strip()
    if not de:
        raise ValueError("Cần đề bài.")
    lg = _loi_giai_hop_le(loi_giai)
    with db.ket_noi() as con:
        cu = con.execute("SELECT trang_thai, rdkit_ok FROM ar_bai_mau WHERE id = ?", (bid,)).fetchone()
        if cu is None:
            return False
        con.execute("UPDATE ar_bai_mau SET de = ?, de_khoa = ?, loi_giai = ?, chu_de = ?, trang_thai = ?, rdkit_ok = ?, bat = ?, "
                    "vec = NULL, sua_luc = ? WHERE id = ?",
                    (de[:4000], khoa_de(de), json.dumps(lg, ensure_ascii=False), (chu_de or "")[:200], trang_thai or cu["trang_thai"],
                     int(cu["rdkit_ok"] if rdkit_ok is None else rdkit_ok), int(bat), time.time(), bid))
        _fts_ghi(con, "bai_mau", bid, chu_cua("bai_mau", {"de": de, "loi_giai": lg}))
    tang_phien_ban()
    return True


def _bai_mau(r) -> dict:
    d = _hang(r)
    d["loi_giai"] = json.loads(d["loi_giai"] or "{}")
    d["co_vec"] = r["vec"] is not None
    return d


def lay_bai_mau(bid: int) -> dict | None:
    with db.ket_noi() as con:
        r = con.execute("SELECT * FROM ar_bai_mau WHERE id = ?", (bid,)).fetchone()
    return _bai_mau(r) if r else None


def ds_bai_mau(trang_thai: str = "", q: str = "", chu_de: str = "", gioi_han: int = 500) -> list[dict]:
    sql, ts = "SELECT * FROM ar_bai_mau WHERE 1 = 1", []
    if trang_thai:
        sql += " AND trang_thai = ?"
        ts.append(trang_thai)
    if chu_de:
        sql += " AND (',' || REPLACE(chu_de, ', ', ',') || ',') LIKE ?"
        ts.append(f"%,{chu_de},%")
    if q:
        sql += " AND (de LIKE ? OR loi_giai LIKE ?)"
        ts += [f"%{q}%", f"%{q}%"]
    with db.ket_noi() as con:
        return [_bai_mau(r) for r in con.execute(sql + " ORDER BY id DESC LIMIT ?", (*ts, gioi_han))]


def tim_khop(de: str, dung_cho_duyet: bool) -> dict | None:
    """Tầng 1: lời giải trong thư viện cho ĐÚNG đề này (đã chuẩn hoá). Ưu tiên bài đã duyệt; chờ duyệt chỉ khi đã qua RDKit."""
    trang_thai = "('duyet', 'cho_duyet')" if dung_cho_duyet else "('duyet')"
    with db.ket_noi() as con:
        r = con.execute(f"SELECT * FROM ar_bai_mau WHERE de_khoa = ? AND bat = 1 AND trang_thai IN {trang_thai} "
                        "AND (trang_thai = 'duyet' OR (rdkit_ok = 1 AND nghi_van = '')) ORDER BY trang_thai = 'duyet' DESC, id DESC LIMIT 1",
                        (khoa_de(de),)).fetchone()
    return _bai_mau(r) if r else None


def dat_nghi_van(bid: int, ly_do: str) -> None:
    """Gemini đối chiếu (chạy nền) cho kết quả khác → lời giải chờ duyệt này KHÔNG được dùng ở tầng 1 cho tới khi admin duyệt."""
    with db.ket_noi() as con:
        con.execute("UPDATE ar_bai_mau SET nghi_van = ?, sua_luc = ? WHERE id = ? AND trang_thai != 'duyet'", (ly_do[:1000], time.time(), bid))
    tang_phien_ban()


def co_trong_thu_vien(de: str) -> bool:
    with db.ket_noi() as con:
        return con.execute("SELECT 1 FROM ar_bai_mau WHERE de_khoa = ? AND trang_thai != 'sai' LIMIT 1", (khoa_de(de),)).fetchone() is not None


def tang_lan_dung(bid: int) -> None:
    with db.ket_noi() as con:
        con.execute("UPDATE ar_bai_mau SET so_lan_dung = so_lan_dung + 1 WHERE id = ?", (bid,))


def duyet_bai_mau(ids: list[int], trang_thai: str = "duyet") -> int:
    if trang_thai not in ("duyet", "cho_duyet", "sai") or not ids:
        return 0
    with db.ket_noi() as con:
        n = con.execute(f"UPDATE ar_bai_mau SET trang_thai = ?, nghi_van = CASE WHEN ? = 'duyet' THEN '' ELSE nghi_van END, sua_luc = ? "
                        f"WHERE id IN ({','.join('?' * len(ids))})", (trang_thai, trang_thai, time.time(), *ids)).rowcount
    tang_phien_ban()
    return n


def danh_dau_sai(de: str) -> int:
    """Admin xác nhận đề này bị giải sai → mọi lời giải trong thư viện cho đúng đề đó không được dùng lại (tầng 1)."""
    with db.ket_noi() as con:
        n = con.execute("UPDATE ar_bai_mau SET trang_thai = 'sai', sua_luc = ? WHERE de_khoa = ? AND trang_thai != 'sai'",
                        (time.time(), khoa_de(de))).rowcount
    if n:
        tang_phien_ban()
    return n


def xoa_bai_mau(bid: int) -> bool:
    with db.ket_noi() as con:
        n = con.execute("DELETE FROM ar_bai_mau WHERE id = ?", (bid,)).rowcount
        _fts_xoa(con, "bai_mau", bid)
    if n:
        tang_phien_ban()
    return bool(n)


# ================================================================ sửa sai
def tao_sua_sai(de: str, dung: str, ly_do: str = "", sai: str = "", chu_de: str = "", bao_sai_id: int | None = None) -> int:
    de, dung = (de or "").strip(), (dung or "").strip()
    if not de or not dung:
        raise ValueError("Cần đề và đáp án đúng.")
    bay_gio = time.time()
    with db.ket_noi() as con:
        cur = con.execute("INSERT INTO ar_sua_sai (de, de_khoa, sai, dung, ly_do, chu_de, bao_sai_id, tao_luc, sua_luc) "
                          "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)",
                          (de[:4000], khoa_de(de), (sai or "")[:6000], dung[:6000], (ly_do or "")[:3000], (chu_de or "")[:200],
                           bao_sai_id, bay_gio, bay_gio))
        sid = int(cur.lastrowid)
        _fts_ghi(con, "sua_sai", sid, chu_cua("sua_sai", {"de": de, "sai": sai, "dung": dung, "ly_do": ly_do}))
    danh_dau_sai(de)
    tang_phien_ban()
    return sid


def sua_sua_sai(sid: int, de: str, dung: str, ly_do: str = "", sai: str = "", chu_de: str = "", bat: bool = True) -> bool:
    de, dung = (de or "").strip(), (dung or "").strip()
    if not de or not dung:
        raise ValueError("Cần đề và đáp án đúng.")
    with db.ket_noi() as con:
        n = con.execute("UPDATE ar_sua_sai SET de = ?, de_khoa = ?, sai = ?, dung = ?, ly_do = ?, chu_de = ?, bat = ?, vec = NULL, sua_luc = ? "
                        "WHERE id = ?", (de[:4000], khoa_de(de), (sai or "")[:6000], dung[:6000], (ly_do or "")[:3000],
                                         (chu_de or "")[:200], int(bat), time.time(), sid)).rowcount
        if n:
            _fts_ghi(con, "sua_sai", sid, chu_cua("sua_sai", {"de": de, "sai": sai, "dung": dung, "ly_do": ly_do}))
    if n:
        tang_phien_ban()
    return bool(n)


def ds_sua_sai(q: str = "") -> list[dict]:
    sql, ts = "SELECT * FROM ar_sua_sai", []
    if q:
        sql += " WHERE de LIKE ? OR dung LIKE ? OR ly_do LIKE ?"
        ts = [f"%{q}%"] * 3
    with db.ket_noi() as con:
        return [{**_hang(r), "co_vec": r["vec"] is not None} for r in con.execute(sql + " ORDER BY id DESC", ts)]


def xoa_sua_sai(sid: int) -> bool:
    with db.ket_noi() as con:
        n = con.execute("DELETE FROM ar_sua_sai WHERE id = ?", (sid,)).rowcount
        _fts_xoa(con, "sua_sai", sid)
    if n:
        tang_phien_ban()
    return bool(n)


# ================================================================ chi tiết một mục (link "mở mục" từ lời giải)
def chi_tiet_muc(loai: str, mid: int) -> dict | None:
    bang = {"doan": "ar_doan", "kien_thuc": "ar_kien_thuc", "bai_mau": "ar_bai_mau", "sua_sai": "ar_sua_sai"}.get(loai)
    if bang is None:
        return None
    if loai == "bai_mau":
        return lay_bai_mau(mid)
    with db.ket_noi() as con:
        r = con.execute(f"SELECT * FROM {bang} WHERE id = ?", (mid,)).fetchone()
        if r is None:
            return None
        d = _hang(r)
        if loai == "doan":
            t = con.execute("SELECT id, ten, nguon, loai FROM ar_tai_lieu WHERE id = ?", (d["tai_lieu_id"],)).fetchone()
            d["tai_lieu"] = dict(t) if t else None
    return d


# ================================================================ báo sai (người dùng → hàng chờ admin)
def tao_bao_sai(user_id: int | None, job_id: str, de: str, mo_hinh: str, loi_giai: dict | None, ghi_chu: str) -> int | None:
    """None = người này đã báo sai bài này rồi."""
    try:
        with db.ket_noi() as con:
            cur = con.execute("INSERT INTO ar_bao_sai (ts, user_id, job_id, de, mo_hinh, loi_giai, ghi_chu) VALUES (?, ?, ?, ?, ?, ?, ?)",
                              (time.time(), user_id, job_id, (de or "")[:4000], (mo_hinh or "")[:120],
                               json.dumps(loi_giai or {}, ensure_ascii=False), (ghi_chu or "")[:2000]))
            return int(cur.lastrowid)
    except db.sqlite3.IntegrityError:
        return None


def ds_bao_sai(trang_thai: str = "cho") -> list[dict]:
    with db.ket_noi() as con:
        rows = con.execute("SELECT b.*, u.email, u.name AS ten FROM ar_bao_sai b LEFT JOIN users u ON u.id = b.user_id "
                           + ("WHERE b.trang_thai = ? " if trang_thai else "") + "ORDER BY b.id DESC LIMIT 300",
                           (trang_thai,) if trang_thai else ()).fetchall()
    out = []
    for r in rows:
        d = dict(r)
        d["loi_giai"] = json.loads(d["loi_giai"] or "{}")
        out.append(d)
    return out


def lay_bao_sai(bid: int) -> dict | None:
    with db.ket_noi() as con:
        r = con.execute("SELECT * FROM ar_bao_sai WHERE id = ?", (bid,)).fetchone()
    if r is None:
        return None
    d = dict(r)
    d["loi_giai"] = json.loads(d["loi_giai"] or "{}")
    return d


def xu_ly_bao_sai(bid: int, trang_thai: str, sua_sai_id: int | None = None) -> bool:
    with db.ket_noi() as con:
        return con.execute("UPDATE ar_bao_sai SET trang_thai = ?, xu_ly_luc = ?, sua_sai_id = ? WHERE id = ?",
                           (trang_thai, time.time(), sua_sai_id, bid)).rowcount > 0


def so_bao_sai_cho() -> int:
    with db.ket_noi() as con:
        return con.execute("SELECT COUNT(*) FROM ar_bao_sai WHERE trang_thai = 'cho'").fetchone()[0]


# ================================================================ bộ đề kiểm tra
def _dap_an_hop_le(dap_an: list) -> list[dict]:
    out = []
    for x in dap_an or []:
        kh, chat, smiles = str(x.get("ki_hieu", "")).strip(), str(x.get("chat", "")).strip(), str(x.get("smiles", "")).strip()
        if kh and (chat or smiles):
            out.append({"ki_hieu": kh[:40], "chat": chat[:300], "smiles": smiles[:500]})
    if not out:
        raise ValueError("Cần ít nhất một chất trong đáp án (kí hiệu + công thức / tên hoặc SMILES).")
    return out


def tao_cau_hoi(de: str, dap_an: list, chu_de: str = "", bat: bool = True) -> int:
    de = (de or "").strip()
    if not de:
        raise ValueError("Cần đề bài.")
    with db.ket_noi() as con:
        return int(con.execute("INSERT INTO ar_bo_de (de, dap_an, chu_de, bat, tao_luc) VALUES (?, ?, ?, ?, ?)",
                               (de[:4000], json.dumps(_dap_an_hop_le(dap_an), ensure_ascii=False), (chu_de or "")[:200], int(bat),
                                time.time())).lastrowid)


def sua_cau_hoi(cid: int, de: str, dap_an: list, chu_de: str = "", bat: bool = True) -> bool:
    de = (de or "").strip()
    if not de:
        raise ValueError("Cần đề bài.")
    with db.ket_noi() as con:
        return con.execute("UPDATE ar_bo_de SET de = ?, dap_an = ?, chu_de = ?, bat = ? WHERE id = ?",
                           (de[:4000], json.dumps(_dap_an_hop_le(dap_an), ensure_ascii=False), (chu_de or "")[:200], int(bat), cid)).rowcount > 0


def ds_cau_hoi(chi_bat: bool = False) -> list[dict]:
    with db.ket_noi() as con:
        rows = con.execute("SELECT * FROM ar_bo_de" + (" WHERE bat = 1" if chi_bat else "") + " ORDER BY id").fetchall()
    return [{**dict(r), "dap_an": json.loads(r["dap_an"])} for r in rows]


def xoa_cau_hoi(cid: int) -> bool:
    with db.ket_noi() as con:
        return con.execute("DELETE FROM ar_bo_de WHERE id = ?", (cid,)).rowcount > 0


def tao_lan_kiem_tra(so_cau: int, cau_hinh: dict) -> int:
    with db.ket_noi() as con:
        return int(con.execute("INSERT INTO ar_kiem_tra (bat_dau, so_cau, cau_hinh) VALUES (?, ?, ?)",
                               (time.time(), so_cau, json.dumps(cau_hinh, ensure_ascii=False))).lastrowid)


def cap_nhat_lan_kiem_tra(lid: int, chi_tiet: list[dict], xong: bool = False, loi: bool = False) -> None:
    so_dung = sum(1 for x in chi_tiet if x.get("dung"))
    with db.ket_noi() as con:
        con.execute("UPDATE ar_kiem_tra SET chi_tiet = ?, da_cham = ?, so_dung = ?, trang_thai = ?, xong = ? WHERE id = ?",
                    (json.dumps(chi_tiet, ensure_ascii=False), len(chi_tiet), so_dung, "loi" if loi else ("xong" if xong else "dang_chay"),
                     time.time() if xong or loi else None, lid))


def ds_lan_kiem_tra(gioi_han: int = 50) -> list[dict]:
    with db.ket_noi() as con:
        rows = con.execute("SELECT id, bat_dau, xong, trang_thai, so_cau, da_cham, so_dung, cau_hinh FROM ar_kiem_tra ORDER BY id DESC LIMIT ?",
                           (gioi_han,)).fetchall()
    return [{**dict(r), "cau_hinh": json.loads(r["cau_hinh"] or "{}")} for r in rows]


def lay_lan_kiem_tra(lid: int) -> dict | None:
    with db.ket_noi() as con:
        r = con.execute("SELECT * FROM ar_kiem_tra WHERE id = ?", (lid,)).fetchone()
    return {**dict(r), "chi_tiet": json.loads(r["chi_tiet"]), "cau_hinh": json.loads(r["cau_hinh"] or "{}")} if r else None


def dang_kiem_tra() -> bool:
    with db.ket_noi() as con:
        return con.execute("SELECT 1 FROM ar_kiem_tra WHERE trang_thai = 'dang_chay' AND bat_dau > ?", (time.time() - 6 * 3600,)).fetchone() is not None


def dong_lan_kiem_tra_do() -> None:
    """Khởi động: lần kiểm tra đang chạy dở (app tắt giữa chừng) → đánh dấu lỗi."""
    with db.ket_noi() as con:
        con.execute("UPDATE ar_kiem_tra SET trang_thai = 'loi', xong = ? WHERE trang_thai = 'dang_chay'", (time.time(),))


# ================================================================ nhật ký giải + thống kê
def ghi_lan_giai(tang: str, user_id: int | None = None, job_id: str = "", bai_mau_id: int | None = None, mo_hinh: str = "",
                 so_muc: int = 0, tokens: int = 0) -> None:
    with db.ket_noi() as con:
        con.execute("INSERT INTO ar_lan_giai (ts, user_id, job_id, tang, bai_mau_id, mo_hinh, so_muc, tokens) VALUES (?, ?, ?, ?, ?, ?, ?, ?)",
                    (time.time(), user_id, job_id, tang, bai_mau_id, mo_hinh[:120], so_muc, tokens))


def thong_ke(ngay: int = 30) -> dict:
    tu = time.time() - ngay * 86400
    with db.ket_noi() as con:
        dem = {r[0]: r[1] for r in con.execute("SELECT trang_thai, COUNT(*) FROM ar_bai_mau GROUP BY trang_thai")}
        so = {
            "tai_lieu": con.execute("SELECT COUNT(*) FROM ar_tai_lieu").fetchone()[0],
            "tai_lieu_dang_xu_ly": con.execute("SELECT COUNT(*) FROM ar_tai_lieu WHERE trang_thai IN ('cho', 'dang_tach')").fetchone()[0],
            "doan": con.execute("SELECT COUNT(*) FROM ar_doan").fetchone()[0],
            "kien_thuc": con.execute("SELECT COUNT(*) FROM ar_kien_thuc").fetchone()[0],
            "bai_mau_duyet": dem.get("duyet", 0), "bai_mau_cho_duyet": dem.get("cho_duyet", 0), "bai_mau_sai": dem.get("sai", 0),
            "sua_sai": con.execute("SELECT COUNT(*) FROM ar_sua_sai").fetchone()[0],
            "bao_sai_cho": con.execute("SELECT COUNT(*) FROM ar_bao_sai WHERE trang_thai = 'cho'").fetchone()[0],
            "bo_de": con.execute("SELECT COUNT(*) FROM ar_bo_de").fetchone()[0],
            "chua_nhung": sum(con.execute(f"SELECT COUNT(*) FROM {b} WHERE vec IS NULL").fetchone()[0]
                              for b in ("ar_doan", "ar_kien_thuc", "ar_bai_mau", "ar_sua_sai")),
        }
        tang = {r[0]: {"so": r[1], "tokens": r[2] or 0} for r in
                con.execute("SELECT tang, COUNT(*), SUM(tokens) FROM ar_lan_giai WHERE ts >= ? GROUP BY tang", (tu,))}
    ai = [tang.get(t, {"so": 0, "tokens": 0}) for t in ("kien_thuc", "manh")]
    so_goi_ai = sum(x["so"] for x in ai)
    tb = round(sum(x["tokens"] for x in ai) / so_goi_ai) if so_goi_ai else 0
    tong = sum(x["so"] for x in tang.values())
    thu_vien = tang.get("thu_vien", {"so": 0})["so"]
    return {"so": so, "ngay": ngay, "tang": tang, "tong_lan_giai": tong, "lan_thu_vien": thu_vien,
            "ti_le_thu_vien": round(100 * thu_vien / tong) if tong else 0, "token_tb_moi_lan_ai": tb,
            "token_tiet_kiem_uoc_tinh": thu_vien * tb, "phien_ban": phien_ban()}
