"""Cơ sở dữ liệu quản lý phòng thí nghiệm (SQLite, một file: data/lab.db).

Bảng:
    ton_kho     tồn kho từng hóa chất (theo số thứ tự trong data/chem/hoa_chat.json)
    thiet_bi    số lượng, tình trạng từng thiết bị (theo data/chem/thiet_bi.json)
    nhat_ky     nhật ký: hiện tượng, sự cố từ phòng lab ảo, ghi chú của giáo viên, nhập/xuất kho
    khoa_api    mạng lưới khóa API (Groq, DeepSeek, Gemini...) - chỉ nằm trên máy này
    nguoi_dung, phien, lop, thanh_vien_lop, gioi_han_sai    tài khoản giáo viên, học sinh (server/lab/tai_khoan.py)
    bai_giang, trinh_chieu    bài giảng và trang đang chiếu của từng lớp (server/lab/trinh_chieu.py), ảnh ở data/bai_giang/
    su_kien_hoc_sinh, lenh_app, chu_y_lop    theo dõi trực tiếp (server/lab/truc_tiep.py); vị trí học sinh không lưu ở đây, chỉ trong bộ nhớ
    buoi_hoc    chế độ Luyện tập, Kiểm tra và hóa chất bị khóa của từng lớp (server/lab/buoi_hoc.py)

Danh mục (tên, công thức, vị trí kệ) vẫn đọc từ JSON; CSDL chỉ giữ phần thay đổi theo thời gian.
"""

from __future__ import annotations

import json
import os
import sqlite3
import threading
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DB_PATH = Path(os.environ.get("KHKT_DB", ROOT / "data" / "lab.db"))
CHEM = json.loads((ROOT / "data/chem/hoa_chat.json").read_text(encoding="utf-8"))
EQUIP = json.loads((ROOT / "data/chem/thiet_bi.json").read_text(encoding="utf-8"))
CHEM_BY_STT = {c["stt"]: c for c in CHEM["hoa_chat"]}
EQUIP_BY_STT = {e["stt"]: e for e in EQUIP["thiet_bi"]}

SCHEMA = """
CREATE TABLE IF NOT EXISTS ton_kho (
    stt INTEGER PRIMARY KEY, so_luong REAL NOT NULL, don_vi TEXT NOT NULL,
    toi_thieu REAL NOT NULL, han_dung TEXT DEFAULT '', ghi_chu TEXT DEFAULT '', cap_nhat REAL
);
CREATE TABLE IF NOT EXISTS thiet_bi (
    stt INTEGER PRIMARY KEY, so_luong INTEGER NOT NULL, hong INTEGER NOT NULL DEFAULT 0,
    dang_muon INTEGER NOT NULL DEFAULT 0, vi_tri TEXT DEFAULT '', ghi_chu TEXT DEFAULT '', cap_nhat REAL
);
CREATE TABLE IF NOT EXISTS nhat_ky (
    id INTEGER PRIMARY KEY AUTOINCREMENT, thoi_gian REAL NOT NULL, loai TEXT NOT NULL,
    muc_do TEXT NOT NULL DEFAULT 'Info', tieu_de TEXT NOT NULL, noi_dung TEXT DEFAULT '', nguon TEXT DEFAULT ''
);
CREATE TABLE IF NOT EXISTS khoa_api (
    id INTEGER PRIMARY KEY AUTOINCREMENT, nha_cung_cap TEXT NOT NULL, nhan TEXT DEFAULT '',
    khoa TEXT NOT NULL, mo_hinh TEXT DEFAULT '', uu_tien INTEGER NOT NULL DEFAULT 10, bat INTEGER NOT NULL DEFAULT 1,
    trang_thai TEXT NOT NULL DEFAULT 'chua_thu', loi_gan_nhat TEXT DEFAULT '', so_lan_goi INTEGER NOT NULL DEFAULT 0,
    so_lan_loi INTEGER NOT NULL DEFAULT 0, nghi_den REAL NOT NULL DEFAULT 0, dung_luc REAL NOT NULL DEFAULT 0,
    tre_ms REAL NOT NULL DEFAULT 0, tao_luc REAL
);
CREATE TABLE IF NOT EXISTS nguoi_dung (
    id INTEGER PRIMARY KEY AUTOINCREMENT, ten_dang_nhap TEXT NOT NULL UNIQUE, ho_ten TEXT NOT NULL,
    vai_tro TEXT NOT NULL CHECK (vai_tro IN ('giao_vien', 'hoc_sinh')), mat_khau_bam TEXT NOT NULL,
    phai_doi_mat_khau INTEGER NOT NULL DEFAULT 0, tao_luc REAL NOT NULL
);
CREATE TABLE IF NOT EXISTS phien (
    ma_bam TEXT PRIMARY KEY, nguoi_dung_id INTEGER NOT NULL, kieu TEXT NOT NULL, tao_luc REAL NOT NULL, het_han REAL NOT NULL
);
CREATE TABLE IF NOT EXISTS lop (
    id INTEGER PRIMARY KEY AUTOINCREMENT, ten TEXT NOT NULL, nam_hoc TEXT DEFAULT '', giao_vien_id INTEGER NOT NULL,
    ma_lop TEXT NOT NULL UNIQUE, ma_lop_mo INTEGER NOT NULL DEFAULT 1, tao_luc REAL NOT NULL
);
CREATE TABLE IF NOT EXISTS thanh_vien_lop (
    lop_id INTEGER NOT NULL, hoc_sinh_id INTEGER NOT NULL, them_luc REAL NOT NULL, PRIMARY KEY (lop_id, hoc_sinh_id)
);
CREATE TABLE IF NOT EXISTS gioi_han_sai (
    khoa TEXT PRIMARY KEY, so_lan INTEGER NOT NULL, lan_cuoi REAL NOT NULL, khoa_den REAL NOT NULL DEFAULT 0
);
CREATE TABLE IF NOT EXISTS bai_giang (
    id INTEGER PRIMARY KEY AUTOINCREMENT, giao_vien_id INTEGER NOT NULL, ten TEXT NOT NULL, so_trang INTEGER NOT NULL, tao_luc REAL NOT NULL
);
CREATE TABLE IF NOT EXISTS trinh_chieu (
    lop_id INTEGER PRIMARY KEY, phien_ban INTEGER NOT NULL, loai TEXT NOT NULL, bai_id INTEGER, trang INTEGER NOT NULL DEFAULT 1,
    tieu_de TEXT DEFAULT '', noi_dung TEXT DEFAULT '', man_hinh TEXT DEFAULT '', dem_nguoc_den REAL NOT NULL DEFAULT 0, cap_nhat REAL NOT NULL
);
CREATE TABLE IF NOT EXISTS su_kien_hoc_sinh (
    id INTEGER PRIMARY KEY AUTOINCREMENT, hoc_sinh_id INTEGER NOT NULL, thoi_gian REAL NOT NULL, muc_do TEXT NOT NULL, tieu_de TEXT NOT NULL, chi_tiet TEXT DEFAULT ''
);
CREATE TABLE IF NOT EXISTS lenh_app (
    id INTEGER PRIMARY KEY AUTOINCREMENT, lop_id INTEGER NOT NULL, hoc_sinh_id INTEGER, loai TEXT NOT NULL, noi_dung TEXT DEFAULT '', ke_hoach TEXT DEFAULT '', tao_luc REAL NOT NULL
);
CREATE TABLE IF NOT EXISTS chu_y_lop (
    lop_id INTEGER PRIMARY KEY, noi_dung TEXT DEFAULT '', tu_luc REAL NOT NULL
);
CREATE TABLE IF NOT EXISTS buoi_hoc (
    lop_id INTEGER PRIMARY KEY, che_do TEXT NOT NULL, het_gio REAL NOT NULL DEFAULT 0, khoa_loai TEXT NOT NULL DEFAULT '[]',
    ngoai_le TEXT NOT NULL DEFAULT '[]', khoa_them TEXT NOT NULL DEFAULT '[]', phien_ban INTEGER NOT NULL, cap_nhat REAL NOT NULL
);
"""

_lock = threading.Lock()
_conn: sqlite3.Connection | None = None


def default_stock(c: dict) -> tuple[float, str, float]:
    """Lượng ban đầu cho một chất: (số lượng, đơn vị, mức tối thiểu cần nhập thêm)."""
    state = c["trang_thai"]
    if state == "khi":
        return 2, "bình", 1
    if state == "giay":
        return 5, "hộp", 2
    if state in ("dd", "long"):
        return 500, "ml", 100
    return 250, "g", 50


def connect() -> sqlite3.Connection:
    global _conn
    with _lock:
        if _conn is None:
            DB_PATH.parent.mkdir(parents=True, exist_ok=True)
            _conn = sqlite3.connect(DB_PATH, check_same_thread=False)
            _conn.row_factory = sqlite3.Row
            _conn.executescript(SCHEMA)
            # CSDL tạo trước khi có lệnh robot từ web
            if "ke_hoach" not in {r[1] for r in _conn.execute("PRAGMA table_info(lenh_app)")}:
                _conn.execute("ALTER TABLE lenh_app ADD COLUMN ke_hoach TEXT DEFAULT ''")
            _seed(_conn)
        return _conn


def reset_for_tests(path: Path) -> None:
    """Dùng file CSDL khác (kiểm thử)."""
    global _conn, DB_PATH
    with _lock:
        if _conn is not None:
            _conn.close()
        _conn = None
        DB_PATH = path


def _seed(conn: sqlite3.Connection) -> None:
    now = time.time()
    have = {r[0] for r in conn.execute("SELECT stt FROM ton_kho")}
    rows = []
    for c in CHEM["hoa_chat"]:
        if c["stt"] in have:
            continue
        q, unit, low = default_stock(c)
        rows.append((c["stt"], q, unit, low, now))
    conn.executemany("INSERT INTO ton_kho (stt, so_luong, don_vi, toi_thieu, cap_nhat) VALUES (?,?,?,?,?)", rows)
    have = {r[0] for r in conn.execute("SELECT stt FROM thiet_bi")}
    conn.executemany("INSERT INTO thiet_bi (stt, so_luong, cap_nhat) VALUES (?,?,?)",
                     [(e["stt"], 10 if e["phan_loai"] in ("Dụng cụ thủy tinh", "Dụng cụ đo lường", "Dụng cụ hỗ trợ", "Vật tư tiêu hao") else 2, now)
                      for e in EQUIP["thiet_bi"] if e["stt"] not in have])
    conn.commit()


def execute(sql: str, args: tuple = ()) -> sqlite3.Cursor:
    conn = connect()
    with _lock:
        cur = conn.execute(sql, args)
        conn.commit()
        return cur


def query(sql: str, args: tuple = ()) -> list[dict]:
    conn = connect()
    with _lock:
        return [dict(r) for r in conn.execute(sql, args).fetchall()]


def log(loai: str, tieu_de: str, noi_dung: str = "", muc_do: str = "Info", nguon: str = "") -> int:
    cur = execute("INSERT INTO nhat_ky (thoi_gian, loai, muc_do, tieu_de, noi_dung, nguon) VALUES (?,?,?,?,?,?)",
                  (time.time(), loai, muc_do, tieu_de, noi_dung, nguon))
    return cur.lastrowid
