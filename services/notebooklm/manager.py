"""Quản lý nhiều tài khoản Google NotebookLM trong CSDL SQLite.

- Cookie: chuẩn hoá + kiểm tra đủ (cookie.py), mã hoá AES-256-GCM (ma_hoa.py, khoá NOTEBOOKLM_COOKIE_KEY) rồi mới lưu.
  Không bao giờ trả cookie về trình duyệt, không ghi giá trị cookie vào log (chỉ tên + số lượng).
- Hỗ trợ đổi tài khoản linh hoạt, chọn tài khoản mặc định, đồng bộ cache danh sách notebook và tài liệu.
- Quét và cập nhật trạng thái kết nối (dang_hoat_dong | can_dang_nhap_lai | loi).
"""
from __future__ import annotations

import json
import logging
import time
from typing import Any

from backend import db
from .client import LoiCookieHetHan, LoiNotebookLM, NotebookInfo, NotebookLMClient
from .cookie import THONG_BAO_HET_HAN, LoiCookie, chuan_hoa_va_kiem_tra, kiem_tra_email
from .ma_hoa import giai_ma as giai_ma_cookie_notebooklm, ma_hoa as ma_hoa_cookie_notebooklm

# Trường được phép trả về giao diện (KHÔNG có cookie). Danh sách tài khoản chỉ cần: tên, email, trạng thái, ngày thêm (+ id, mặc định, số notebook).
_TRUONG_AN_TOAN = ("id", "ten_goi_nho", "email", "status", "error_msg", "is_default", "so_notebook", "last_sync_at", "created_at")
_LY_DO_TU_CHOI = ("het_han", "tu_choi", "thieu")   # Google không nhận cookie → báo "hết hạn / không hợp lệ"

log = logging.getLogger(__name__)


def _an_toan(r: dict | Any) -> dict:
    """Chỉ giữ các trường an toàn để trả về giao diện – không bao giờ có cookie (kể cả bản mã)."""
    d = dict(r)
    kq = {k: d.get(k) for k in _TRUONG_AN_TOAN}
    kq["is_default"] = int(kq.get("is_default") or 0)
    return kq


def _ten_cookie(cookies: dict[str, str]) -> str:
    return f"{len(cookies)} cookie ({', '.join(sorted(cookies))})"     # chỉ tên + số lượng, không giá trị


def _kiem_tra_voi_google(cookie_chuan: str) -> tuple[str, str, dict]:
    """→ (status, error_msg, profile). Google từ chối / hết hạn → LoiCookie COOKIE_EXPIRED (không lưu);
    lỗi mạng / lỗi khác → vẫn lưu với trạng thái "loi" để thử lại sau."""
    client = NotebookLMClient(cookie_chuan)
    ok, msg, profile = client.kiem_tra_ket_noi()
    if ok:
        return "dang_hoat_dong", "", profile
    if client.ly_do in _LY_DO_TU_CHOI:
        raise LoiCookie(THONG_BAO_HET_HAN, "COOKIE_EXPIRED")
    return "loi", msg or "Chưa kết nối được tới Google – thử lại sau.", {}


def ds_tai_khoan() -> list[dict]:
    """Danh sách tất cả tài khoản NotebookLM đã lưu."""
    with db.ket_noi() as con:
        rows = con.execute("SELECT * FROM notebooklm_accounts ORDER BY is_default DESC, id ASC").fetchall()
    return [_an_toan(r) for r in rows]


def lay_tai_khoan(acc_id: int) -> dict | None:
    """Lấy thông tin an toàn của 1 tài khoản."""
    with db.ket_noi() as con:
        r = con.execute("SELECT * FROM notebooklm_accounts WHERE id = ?", (acc_id,)).fetchone()
    return _an_toan(r) if r else None


def lay_tai_khoan_mac_dinh() -> dict | None:
    """Lấy tài khoản mặc định (hoặc tài khoản đầu tiên)."""
    with db.ket_noi() as con:
        r = con.execute("SELECT * FROM notebooklm_accounts WHERE is_default = 1 LIMIT 1").fetchone()
        if not r:
            r = con.execute("SELECT * FROM notebooklm_accounts ORDER BY id ASC LIMIT 1").fetchone()
    return _an_toan(r) if r else None


def _lay_client_tai_khoan(acc_id: int) -> tuple[NotebookLMClient, dict]:
    """Giải mã cookie và dựng NotebookLMClient cho tài khoản."""
    with db.ket_noi() as con:
        r = con.execute("SELECT * FROM notebooklm_accounts WHERE id = ?", (acc_id,)).fetchone()
    if not r:
        raise LoiNotebookLM(f"Không tìm thấy tài khoản NotebookLM #{acc_id}", code="khong_ton_tai", status_code=404)

    acc = dict(r)
    cookie_str = giai_ma_cookie_notebooklm(acc["cookie_enc"])
    client = NotebookLMClient(cookie_str)
    return client, acc


def them_tai_khoan(ten_goi_nho: str | None, raw_cookie: str | Any, is_default: bool = False, skip_check: bool = False,
                   email: str | None = None) -> dict:
    """Thêm tài khoản Google NotebookLM: chuẩn hoá + kiểm tra đủ cookie → kiểm tra với Google (lấy email nếu bỏ trống)
    → mã hoá AES-GCM → lưu. Lỗi cookie / email → LoiCookie (422, mã chuẩn + thông báo tiếng Việt)."""
    cookie_chuan, cookies = chuan_hoa_va_kiem_tra(raw_cookie)
    email = kiem_tra_email(email)
    status, error_msg, profile = ("dang_hoat_dong", "", {}) if skip_check else _kiem_tra_voi_google(cookie_chuan)
    email = email or (profile.get("email") or "").strip().lower()
    picture = profile.get("picture", "")
    ten = (ten_goi_nho or "").strip() or email or "Tài khoản Google"
    cookie_enc = ma_hoa_cookie_notebooklm(cookie_chuan)
    now = time.time()

    with db.ket_noi() as con:
        # Nếu chưa có tài khoản nào thì tài khoản đầu tiên luôn là mặc định
        so_hien_co = con.execute("SELECT COUNT(*) FROM notebooklm_accounts").fetchone()[0]
        mac_dinh = 1 if (is_default or so_hien_co == 0) else 0

        if mac_dinh:
            con.execute("UPDATE notebooklm_accounts SET is_default = 0")

        cur = con.execute(
            """INSERT INTO notebooklm_accounts
               (ten_goi_nho, email, picture, cookie_enc, status, error_msg, is_default, so_notebook, created_at, updated_at)
               VALUES (?, ?, ?, ?, ?, ?, ?, 0, ?, ?)""",
            (ten[:100], email, picture, cookie_enc, status, error_msg, mac_dinh, now, now),
        )
        new_id = int(cur.lastrowid)
    log.info("Đã thêm tài khoản NotebookLM #%d: %s, trạng thái %s", new_id, _ten_cookie(cookies), status)

    # Nếu kết nối thành công, thử đồng bộ danh sách notebook ngay
    if status == "dang_hoat_dong" and not skip_check:
        try:
            dong_bo_tai_khoan(new_id)
        except Exception as e:
            log.warning("Đồng bộ notebook lần đầu cho tài khoản #%d thất bại: %s", new_id, type(e).__name__)

    return lay_tai_khoan(new_id)  # type: ignore[return-value]


def cap_nhat_tai_khoan(
    acc_id: int,
    ten_goi_nho: str | None = None,
    raw_cookie: str | Any | None = None,
    is_default: bool | None = None,
    email: str | None = None,
) -> dict:
    """Cập nhật tên, cookie mới (chuẩn hoá + kiểm tra như khi thêm), email hoặc cờ mặc định."""
    acc = lay_tai_khoan(acc_id)
    if not acc:
        raise LoiNotebookLM(f"Không tìm thấy tài khoản #{acc_id}", code="NOT_FOUND", status_code=404)

    cap_nhat: dict[str, Any] = {}
    now = time.time()

    if ten_goi_nho is not None:
        cap_nhat["ten_goi_nho"] = (ten_goi_nho.strip() or acc["ten_goi_nho"])[:100]
    if email is not None and email.strip():
        cap_nhat["email"] = kiem_tra_email(email)

    co_cookie = raw_cookie is not None and (not isinstance(raw_cookie, str) or raw_cookie.strip())
    if co_cookie:
        cookie_chuan, cookies = chuan_hoa_va_kiem_tra(raw_cookie)
        status, error_msg, profile = _kiem_tra_voi_google(cookie_chuan)
        cap_nhat["cookie_enc"] = ma_hoa_cookie_notebooklm(cookie_chuan)
        cap_nhat["status"] = status
        cap_nhat["error_msg"] = error_msg
        if profile.get("email") and not cap_nhat.get("email") and not acc.get("email"):
            cap_nhat["email"] = profile["email"].strip().lower()
        if profile.get("picture"):
            cap_nhat["picture"] = profile["picture"]
        log.info("Cập nhật cookie tài khoản NotebookLM #%d: %s, trạng thái %s", acc_id, _ten_cookie(cookies), status)

    with db.ket_noi() as con:
        if is_default:
            con.execute("UPDATE notebooklm_accounts SET is_default = 0")
            cap_nhat["is_default"] = 1
        elif is_default is False:
            cap_nhat["is_default"] = 0

        if cap_nhat:
            cap_nhat["updated_at"] = now
            clauses = [f"{k} = ?" for k in cap_nhat]
            con.execute(f"UPDATE notebooklm_accounts SET {', '.join(clauses)} WHERE id = ?", (*cap_nhat.values(), acc_id))

    return lay_tai_khoan(acc_id)  # type: ignore[return-value]


def xoa_tai_khoan(acc_id: int) -> bool:
    """Xoá một tài khoản và toàn bộ cache liên quan."""
    with db.ket_noi() as con:
        r = con.execute("SELECT is_default FROM notebooklm_accounts WHERE id = ?", (acc_id,)).fetchone()
        if not r:
            return False
        la_mac_dinh = bool(r[0])

        con.execute("DELETE FROM notebooklm_cache WHERE account_id = ?", (acc_id,))
        con.execute("DELETE FROM notebooklm_accounts WHERE id = ?", (acc_id,))

        # Nếu vừa xoá tài khoản mặc định, gán tài khoản kế tiếp làm mặc định
        if la_mac_dinh:
            con.execute("UPDATE notebooklm_accounts SET is_default = 1 WHERE id = (SELECT id FROM notebooklm_accounts ORDER BY id ASC LIMIT 1)")

    return True


def dat_mac_dinh(acc_id: int) -> bool:
    """Đặt tài khoản acc_id làm mặc định."""
    with db.ket_noi() as con:
        r = con.execute("SELECT id FROM notebooklm_accounts WHERE id = ?", (acc_id,)).fetchone()
        if not r:
            return False
        con.execute("UPDATE notebooklm_accounts SET is_default = 0")
        con.execute("UPDATE notebooklm_accounts SET is_default = 1, updated_at = ? WHERE id = ?", (time.time(), acc_id))
    return True


def kiem_tra_tai_khoan(acc_id: int) -> dict:
    """Kiểm tra trực tiếp cookie của tài khoản và cập nhật trạng thái."""
    client, acc = _lay_client_tai_khoan(acc_id)
    ok, msg, profile = client.kiem_tra_ket_noi()
    if not ok and client.ly_do in _LY_DO_TU_CHOI:
        msg = THONG_BAO_HET_HAN

    status = "dang_hoat_dong" if ok else ("can_dang_nhap_lai" if client.ly_do in _LY_DO_TU_CHOI else "loi")
    now = time.time()
    with db.ket_noi() as con:
        con.execute(
            """UPDATE notebooklm_accounts
               SET status = ?, error_msg = ?, updated_at = ?,
                   email = CASE WHEN ? != '' THEN ? ELSE email END,
                   picture = CASE WHEN ? != '' THEN ? ELSE picture END
               WHERE id = ?""",
            (status, msg, now, profile.get("email", ""), profile.get("email", ""), profile.get("picture", ""), profile.get("picture", ""), acc_id),
        )
    return lay_tai_khoan(acc_id)  # type: ignore[return-value]


def dong_bo_tai_khoan(acc_id: int) -> dict:
    """Đồng bộ danh sách notebook và tài liệu của tài khoản vào CSDL."""
    client, acc = _lay_client_tai_khoan(acc_id)
    now = time.time()

    try:
        notebooks = client.list_notebooks()
    except LoiCookieHetHan as e:
        with db.ket_noi() as con:
            con.execute("UPDATE notebooklm_accounts SET status = 'can_dang_nhap_lai', error_msg = ?, updated_at = ? WHERE id = ?",
                        (str(e), now, acc_id))
        raise

    with db.ket_noi() as con:
        for nb in notebooks:
            nb_id = getattr(nb, "id", None) or (nb.get("id") if isinstance(nb, dict) else None) or (nb.get("notebook_id") if isinstance(nb, dict) else "")
            nb_title = getattr(nb, "title", None) or (nb.get("title") if isinstance(nb, dict) else "")
            nb_desc = getattr(nb, "description", None) or (nb.get("description") if isinstance(nb, dict) else "")

            # Lấy chi tiết nguồn của từng notebook nếu cần
            try:
                ct = client.get_notebook(nb_id)
                ct_sources = getattr(ct, "sources", None) or (ct.get("sources") if isinstance(ct, dict) else [])
                ct_notes = getattr(ct, "notes", None) or (ct.get("notes") if isinstance(ct, dict) else [])
                ct_title = getattr(ct, "title", None) or (ct.get("title") if isinstance(ct, dict) else "")

                sources_list = [asdict(s) if hasattr(s, "__dataclass_fields__") else dict(s) for s in ct_sources]
                notes_list = [asdict(n) if hasattr(n, "__dataclass_fields__") else dict(n) for n in ct_notes]

                sources_json = json.dumps(sources_list, ensure_ascii=False)
                notes_json = json.dumps(notes_list, ensure_ascii=False)
                so_nguon = len(sources_list)
                title = ct_title or nb_title
            except Exception:  # noqa: BLE001
                sources_json = "[]"
                notes_json = "[]"
                so_nguon = 0
                title = nb_title

            con.execute(
                """INSERT INTO notebooklm_cache (account_id, notebook_id, title, description, sources_json, notes_json, so_nguon, updated_at)
                   VALUES (?, ?, ?, ?, ?, ?, ?, ?)
                   ON CONFLICT(account_id, notebook_id) DO UPDATE SET
                   title = excluded.title, description = excluded.description,
                   sources_json = excluded.sources_json, notes_json = excluded.notes_json,
                   so_nguon = excluded.so_nguon, updated_at = excluded.updated_at""",
                (acc_id, nb_id, title, nb_desc, sources_json, notes_json, so_nguon, now),
            )

        # Cập nhật số notebook và trạng thái
        con.execute(
            """UPDATE notebooklm_accounts
               SET so_notebook = ?, last_sync_at = ?, status = 'dang_hoat_dong', error_msg = '', updated_at = ?
               WHERE id = ?""",
            (len(notebooks), now, now, acc_id),
        )

    return {
        "ok": True,
        "account_id": acc_id,
        "notebook_count": len(notebooks),
        "so_notebook": len(notebooks),
        "last_sync_at": now,
        "notebooks": lay_danh_sach_cache_notebooks(acc_id),
    }


def lay_danh_sach_cache_notebooks(acc_id: int) -> list[dict]:
    """Lấy danh sách notebooks từ cache CSDL."""
    with db.ket_noi() as con:
        rows = con.execute("SELECT * FROM notebooklm_cache WHERE account_id = ? ORDER BY title ASC", (acc_id,)).fetchall()
    res = []
    for r in rows:
        d = dict(r)
        d["sources"] = json.loads(d.get("sources_json") or "[]")
        d["notes"] = json.loads(d.get("notes_json") or "[]")
        res.append(d)
    return res


def lay_chi_tiet_notebook(acc_id: int, notebook_id: str) -> dict | None:
    """Lấy chi tiết 1 notebook từ cache hoặc tải trực tiếp."""
    with db.ket_noi() as con:
        r = con.execute("SELECT * FROM notebooklm_cache WHERE account_id = ? AND notebook_id = ?", (acc_id, notebook_id)).fetchone()
    if r:
        d = dict(r)
        d["sources"] = json.loads(d.get("sources_json") or "[]")
        d["notes"] = json.loads(d.get("notes_json") or "[]")
        return d

    # Nếu chưa có trong cache, thử tải trực tiếp
    try:
        client, _ = _lay_client_tai_khoan(acc_id)
        ct = client.get_notebook(notebook_id)
        now = time.time()
        ct_sources = getattr(ct, "sources", None) or (ct.get("sources") if isinstance(ct, dict) else [])
        ct_notes = getattr(ct, "notes", None) or (ct.get("notes") if isinstance(ct, dict) else [])
        ct_title = getattr(ct, "title", None) or (ct.get("title") if isinstance(ct, dict) else "")
        ct_desc = getattr(ct, "description", None) or (ct.get("description") if isinstance(ct, dict) else "")

        sources_list = [asdict(s) if hasattr(s, "__dataclass_fields__") else dict(s) for s in ct_sources]
        notes_list = [asdict(n) if hasattr(n, "__dataclass_fields__") else dict(n) for n in ct_notes]

        sources_json = json.dumps(sources_list, ensure_ascii=False)
        notes_json = json.dumps(notes_list, ensure_ascii=False)
        with db.ket_noi() as con:
            con.execute(
                """INSERT OR REPLACE INTO notebooklm_cache
                   (account_id, notebook_id, title, description, sources_json, notes_json, so_nguon, updated_at)
                   VALUES (?, ?, ?, ?, ?, ?, ?, ?)""",
                (acc_id, notebook_id, ct_title, ct_desc, sources_json, notes_json, len(sources_list), now),
            )
        return {
            "account_id": acc_id,
            "notebook_id": notebook_id,
            "title": ct_title,
            "description": ct_desc,
            "sources": sources_list,
            "notes": notes_list,
            "so_nguon": len(sources_list),
            "updated_at": now,
        }
    except Exception as e:
        log.warning("Không thể lấy chi tiết notebook %s: %s", notebook_id, e)
        return None


def hoi_notebook(acc_id: int, notebook_id: str, cau_hoi: str) -> dict:
    """Gửi câu hỏi tới một notebook cụ thể."""
    cau_hoi = (cau_hoi or "").strip()
    if not cau_hoi:
        raise LoiNotebookLM("Câu hỏi không được để trống", code="thieu_cau_hoi")

    client, acc = _lay_client_tai_khoan(acc_id)
    kq = client.query(notebook_id, cau_hoi)
    tra_loi = getattr(kq, "answer", None) or (kq.get("answer") if isinstance(kq, dict) else None) or (kq.get("tra_loi") if isinstance(kq, dict) else "")
    trich_dan = getattr(kq, "citations", None) or (kq.get("citations") if isinstance(kq, dict) else [])
    return {
        "cau_hoi": cau_hoi,
        "cau_tra_loi": tra_loi,
        "trich_dan": trich_dan,
        "account_id": acc_id,
        "notebook_id": notebook_id,
    }
