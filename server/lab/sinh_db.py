"""Cơ sở dữ liệu quản lý phòng thí nghiệm sinh học (VERA Lab Tầng 2).
Sử dụng chung file SQLite với Tầng 1 (mặc định data/lab.db hoặc KHKT_DB).

8 bảng quản lý:
    sinh_dung_cu    tồn kho, tình trạng hiệu chuẩn, bảo trì 108 dụng cụ sinh học
    sinh_pham       kho hóa chất, mẫu vật tươi, kit PCR, enzyme, ladder (BSL-1)
    nhiet_do_tu     lịch sử nhiệt độ và cảm biến mở cửa các tủ bảo quản/nuôi cấy
    dat_lich        lịch đăng ký mượn phòng, thiết bị, tủ vô trùng
    muon_tra        sổ theo dõi mượn/trả dụng cụ học sinh theo lớp
    ket_qua_tn      kết quả làm bài thực hành sinh học của học sinh (15 bài)
    su_co_sinh      nhật ký ghi nhận sự cố thao tác vô trùng và an toàn sinh học
    bao_tri         lịch sử kiểm định, hiệu chuẩn, bảo dưỡng định kỳ thiết bị
"""

from __future__ import annotations

import json
import os
import sqlite3
import threading
import time
from pathlib import Path
from typing import Any

ROOT = Path(__file__).resolve().parents[2]
DB_PATH = Path(os.environ.get("KHKT_DB", ROOT / "data" / "lab.db"))

DATA_SINH = ROOT / "data" / "sinh"
DUNG_CU_PATH = DATA_SINH / "dung_cu.json"
SINH_PHAM_PATH = DATA_SINH / "sinh_pham_mac_dinh.json"

SCHEMA = """
CREATE TABLE IF NOT EXISTS sinh_dung_cu (
    ma TEXT PRIMARY KEY,
    so_luong INTEGER NOT NULL DEFAULT 1,
    hong INTEGER NOT NULL DEFAULT 0,
    dang_muon INTEGER NOT NULL DEFAULT 0,
    can_bao_tri INTEGER NOT NULL DEFAULT 0,
    vi_tri TEXT DEFAULT '',
    lan_hieu_chuan REAL DEFAULT 0,
    han_hieu_chuan REAL DEFAULT 0,
    ghi_chu TEXT DEFAULT '',
    cap_nhat REAL
);

CREATE TABLE IF NOT EXISTS sinh_pham (
    id TEXT PRIMARY KEY,
    ten TEXT NOT NULL,
    loai TEXT NOT NULL,
    tu TEXT NOT NULL,
    ngan TEXT DEFAULT '',
    so_luong REAL NOT NULL DEFAULT 0,
    don_vi TEXT NOT NULL DEFAULT '',
    so_lo TEXT DEFAULT '',
    han_dung REAL NOT NULL,
    cap_an_toan TEXT NOT NULL DEFAULT 'BSL-1',
    ma_chat TEXT DEFAULT '',
    cap_nhat REAL
);

CREATE TABLE IF NOT EXISTS nhiet_do_tu (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    tu TEXT NOT NULL,
    thoi_gian REAL NOT NULL,
    nhiet_do REAL NOT NULL,
    cua_mo INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS dat_lich (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    doi_tuong TEXT NOT NULL,
    nguoi TEXT NOT NULL,
    lop TEXT NOT NULL,
    bat_dau REAL NOT NULL,
    ket_thuc REAL NOT NULL,
    muc_dich TEXT DEFAULT '',
    tao_luc REAL
);

CREATE TABLE IF NOT EXISTS muon_tra (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    ma_dung_cu TEXT NOT NULL,
    so_luong INTEGER NOT NULL DEFAULT 1,
    nguoi TEXT NOT NULL,
    lop TEXT NOT NULL,
    luc_muon REAL NOT NULL,
    han_tra REAL NOT NULL,
    luc_tra REAL,
    tinh_trang_tra TEXT DEFAULT 'chua_tra'
);

CREATE TABLE IF NOT EXISTS ket_qua_tn (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    hoc_sinh TEXT NOT NULL,
    lop TEXT NOT NULL,
    ban TEXT DEFAULT '',
    ma_bai TEXT NOT NULL,
    thoi_gian REAL NOT NULL,
    du_lieu TEXT NOT NULL DEFAULT '{}',
    diem REAL,
    nhan_xet TEXT DEFAULT '',
    hoc_sinh_id INTEGER
);

CREATE TABLE IF NOT EXISTS su_co_sinh (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    phong TEXT NOT NULL,
    loai TEXT NOT NULL,
    muc_do TEXT NOT NULL DEFAULT 'Info',
    mo_ta TEXT NOT NULL,
    xu_ly TEXT DEFAULT '',
    trang_thai TEXT NOT NULL DEFAULT 'moi',
    thoi_gian REAL NOT NULL
);

CREATE TABLE IF NOT EXISTS bao_tri (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    ma_dung_cu TEXT NOT NULL,
    loai TEXT NOT NULL,
    ngay REAL NOT NULL,
    nguoi TEXT NOT NULL,
    ket_qua TEXT NOT NULL,
    ghi_chu TEXT DEFAULT ''
);
"""

_lock = threading.Lock()
_conn: sqlite3.Connection | None = None

# Bộ nhớ đệm danh mục tĩnh đọc từ JSON
_DUNG_CU_CACHE: dict[str, dict] = {}
_BAI_THI_NGHIEM_CACHE: list[dict] = []


def _load_static_catalogs() -> None:
    global _DUNG_CU_CACHE, _BAI_THI_NGHIEM_CACHE
    if not _DUNG_CU_CACHE and DUNG_CU_PATH.exists():
        try:
            data = json.loads(DUNG_CU_PATH.read_text(encoding="utf-8"))
            _DUNG_CU_CACHE = {d["ma"]: d for d in data.get("dung_cu", [])}
        except Exception:
            pass

    bai_path = DATA_SINH / "bai_thi_nghiem.json"
    if not _BAI_THI_NGHIEM_CACHE and bai_path.exists():
        try:
            data = json.loads(bai_path.read_text(encoding="utf-8"))
            _BAI_THI_NGHIEM_CACHE = data.get("bai_thi_nghiem", [])
        except Exception:
            pass


def connect() -> sqlite3.Connection:
    global _conn
    with _lock:
        if _conn is None:
            DB_PATH.parent.mkdir(parents=True, exist_ok=True)
            _conn = sqlite3.connect(DB_PATH, check_same_thread=False)
            _conn.row_factory = sqlite3.Row
            _conn.executescript(SCHEMA)
            # CSDL tạo trước khi có tài khoản: bản ghi cũ để trống hoc_sinh_id
            if "hoc_sinh_id" not in {r[1] for r in _conn.execute("PRAGMA table_info(ket_qua_tn)")}:
                _conn.execute("ALTER TABLE ket_qua_tn ADD COLUMN hoc_sinh_id INTEGER")
            _seed(_conn)
        return _conn


def reset_for_tests(path: Path) -> None:
    global _conn, DB_PATH
    with _lock:
        if _conn is not None:
            _conn.close()
        _conn = None
        DB_PATH = path


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


def _seed(conn: sqlite3.Connection) -> None:
    now = time.time()
    _load_static_catalogs()

    # 1. Khởi tạo kho dụng cụ từ dung_cu.json
    have_dc = {r[0] for r in conn.execute("SELECT ma FROM sinh_dung_cu")}
    dc_rows = []
    if DUNG_CU_PATH.exists():
        try:
            dc_data = json.loads(DUNG_CU_PATH.read_text(encoding="utf-8"))
            for d in dc_data.get("dung_cu", []):
                ma = d.get("ma")
                if not ma or ma in have_dc:
                    continue
                so_luong = int(d.get("so_luong", 1))
                vi_tri = str(d.get("vi_tri", ""))
                dc_rows.append((ma, so_luong, 0, 0, 0, vi_tri, 0.0, 0.0, "", now))
        except Exception:
            pass
    if dc_rows:
        conn.executemany(
            "INSERT INTO sinh_dung_cu (ma, so_luong, hong, dang_muon, can_bao_tri, vi_tri, lan_hieu_chuan, han_hieu_chuan, ghi_chu, cap_nhat) "
            "VALUES (?,?,?,?,?,?,?,?,?,?)",
            dc_rows,
        )

    # 2. Khởi tạo kho sinh phẩm từ sinh_pham_mac_dinh.json
    have_sp = {r[0] for r in conn.execute("SELECT id FROM sinh_pham")}
    sp_rows = []
    if SINH_PHAM_PATH.exists():
        try:
            sp_data = json.loads(SINH_PHAM_PATH.read_text(encoding="utf-8"))
            for s in sp_data.get("sinh_pham", []):
                sid = s.get("id")
                if not sid or sid in have_sp:
                    continue
                ten = s.get("ten", "")
                loai = s.get("loai", "hoa_chat_sinh")
                tu = s.get("tu", "ke")
                ngan = s.get("ngan", "")
                so_luong = float(s.get("so_luong", 0))
                don_vi = s.get("don_vi", "")
                so_lo = s.get("so_lo", "")
                han_dung_ngay = float(s.get("han_dung_ngay", 180))
                han_dung = now + han_dung_ngay * 86400.0
                cap_an_toan = s.get("cap_an_toan", "BSL-1")
                ma_chat = s.get("ma_chat", "")
                sp_rows.append((sid, ten, loai, tu, ngan, so_luong, don_vi, so_lo, han_dung, cap_an_toan, ma_chat, now))
        except Exception:
            pass
    if sp_rows:
        conn.executemany(
            "INSERT INTO sinh_pham (id, ten, loai, tu, ngan, so_luong, don_vi, so_lo, han_dung, cap_an_toan, ma_chat, cap_nhat) "
            "VALUES (?,?,?,?,?,?,?,?,?,?,?,?)",
            sp_rows,
        )

    # 3. Mẫu nhiệt độ tủ ban đầu nếu chưa có
    count_temp = conn.execute("SELECT COUNT(*) FROM nhiet_do_tu").fetchone()[0]
    if count_temp == 0:
        default_temps = [
            ("tu_mat_4", now - 3600, 4.2, 0),
            ("tu_mat_4", now - 600, 4.0, 0),
            ("tu_dong_am20", now - 3600, -20.5, 0),
            ("tu_dong_am20", now - 600, -20.1, 0),
            ("tu_am_37", now - 3600, 37.0, 0),
            ("tu_am_37", now - 600, 37.1, 0),
        ]
        conn.executemany(
            "INSERT INTO nhiet_do_tu (tu, thoi_gian, nhiet_do, cua_mo) VALUES (?,?,?,?)",
            default_temps,
        )

    conn.commit()


# ==============================================================================
# HÀM TRỢ GIÚP CRUD CHO CÁC BẢNG TẦNG 2
# ==============================================================================

def get_static_dung_cu(ma: str) -> dict | None:
    _load_static_catalogs()
    return _DUNG_CU_CACHE.get(ma)


def get_all_static_dung_cu() -> list[dict]:
    _load_static_catalogs()
    return list(_DUNG_CU_CACHE.values())


def get_all_static_bai_thi_nghiem() -> list[dict]:
    _load_static_catalogs()
    return _BAI_THI_NGHIEM_CACHE


def get_tong_quan() -> dict:
    now = time.time()
    # Thống kê dụng cụ
    dc_list = query("SELECT * FROM sinh_dung_cu")
    tong_dc = sum(r["so_luong"] for r in dc_list)
    hong_dc = sum(r["hong"] for r in dc_list)
    dang_muon_dc = sum(r["dang_muon"] for r in dc_list)
    can_bao_tri_dc = sum(r["can_bao_tri"] for r in dc_list)
    qua_han_hieu_chuan_dc = sum(1 for r in dc_list if r["han_hieu_chuan"] > 0 and r["han_hieu_chuan"] < now)

    # Thống kê sinh phẩm
    sp_list = query("SELECT * FROM sinh_pham")
    sap_het = [r for r in sp_list if r["so_luong"] <= 2.0]
    sap_het_han = [r for r in sp_list if now <= r["han_dung"] <= now + 30 * 86400.0]
    qua_han = [r for r in sp_list if r["han_dung"] < now]

    # Nhiệt độ tủ mới nhất
    tu_names = ["tu_mat_4", "tu_dong_am20", "tu_am_37"]
    tu_status = []
    for t in tu_names:
        row = query("SELECT * FROM nhiet_do_tu WHERE tu=? ORDER BY thoi_gian DESC LIMIT 1", (t,))
        if row:
            r = row[0]
            temp = r["nhiet_do"]
            # Cảnh báo nhiệt độ lệch
            warn = False
            if t == "tu_mat_4" and not (2.0 <= temp <= 8.0):
                warn = True
            elif t == "tu_dong_am20" and not (-25.0 <= temp <= -15.0):
                warn = True
            elif t == "tu_am_37" and not (35.0 <= temp <= 39.0):
                warn = True
            if r["cua_mo"] == 1:
                warn = True
            tu_status.append({
                "tu": t,
                "nhiet_do": temp,
                "thoi_gian": r["thoi_gian"],
                "canh_bao": warn,
                "cua_mo": bool(r["cua_mo"]),
            })
        else:
            tu_status.append({"tu": t, "nhiet_do": 0.0, "thoi_gian": now, "canh_bao": False, "cua_mo": False})

    # Sự cố mới
    su_co_moi = query("SELECT * FROM su_co_sinh WHERE trang_thai IN ('moi', 'dang_xu_ly') ORDER BY thoi_gian DESC LIMIT 10")
    so_su_co_moi = query("SELECT COUNT(*) AS n FROM su_co_sinh WHERE trang_thai IN ('moi', 'dang_xu_ly')")[0]["n"]

    # Lịch hôm nay (bắt đầu trong ngày hoặc kéo dài qua ngày hôm nay)
    start_of_day = now - (now % 86400)
    end_of_day = start_of_day + 86400
    lich_hom_nay = query(
        "SELECT * FROM dat_lich WHERE (bat_dau >= ? AND bat_dau < ?) OR (bat_dau < ? AND ket_thuc > ?) ORDER BY bat_dau ASC",
        (start_of_day, end_of_day, start_of_day, start_of_day),
    )

    return {
        "dung_cu": {
            "tong": tong_dc,
            "hong": hong_dc,
            "dang_muon": dang_muon_dc,
            "can_bao_tri": can_bao_tri_dc,
            "qua_han_hieu_chuan": qua_han_hieu_chuan_dc,
        },
        "sinh_pham": {
            "sap_het": sap_het,
            "sap_het_han": sap_het_han,
            "qua_han": qua_han,
        },
        "tu": tu_status,
        "su_co_moi": su_co_moi,
        "so_su_co_moi": so_su_co_moi,
        "lich_hom_nay": lich_hom_nay,
    }


def list_dung_cu(phong: str = "", phan_loai: str = "", tim: str = "") -> list[dict]:
    _load_static_catalogs()
    db_rows = {r["ma"]: r for r in query("SELECT * FROM sinh_dung_cu")}
    out = []
    w_query = (tim or "").strip().lower()

    for ma, static_item in _DUNG_CU_CACHE.items():
        if phong:
            p_val = static_item.get("phong")
            if isinstance(p_val, list):
                if phong not in p_val:
                    continue
            elif p_val != phong:
                continue

        if phan_loai and static_item.get("phan_loai") != phan_loai:
            continue

        ten = static_item.get("ten", "")
        if w_query:
            text = f"{ma} {ten} {static_item.get('cong_dung', '')}".lower()
            if w_query not in text:
                continue

        db_item = db_rows.get(ma, {})
        row = dict(static_item)
        row["so_luong"] = db_item.get("so_luong", static_item.get("so_luong", 1))
        row["hong"] = db_item.get("hong", 0)
        row["dang_muon"] = db_item.get("dang_muon", 0)
        row["can_bao_tri"] = db_item.get("can_bao_tri", 0)
        row["vi_tri"] = db_item.get("vi_tri", static_item.get("vi_tri", ""))
        row["lan_hieu_chuan"] = db_item.get("lan_hieu_chuan", 0.0)
        row["han_hieu_chuan"] = db_item.get("han_hieu_chuan", 0.0)
        row["ghi_chu"] = db_item.get("ghi_chu", "")
        row["cap_nhat"] = db_item.get("cap_nhat", 0.0)
        out.append(row)

    out.sort(key=lambda x: x.get("ma", ""))
    return out


def patch_dung_cu(ma: str, fields: dict) -> dict:
    allowed = {"so_luong", "hong", "dang_muon", "can_bao_tri", "vi_tri", "lan_hieu_chuan", "han_hieu_chuan", "ghi_chu"}
    updates = {k: v for k, v in fields.items() if k in allowed}
    if not updates:
        row = query("SELECT * FROM sinh_dung_cu WHERE ma=?", (ma,))
        return row[0] if row else {}

    updates["cap_nhat"] = time.time()
    set_clause = ", ".join(f"{k} = ?" for k in updates)
    values = tuple(updates.values()) + (ma,)
    execute(f"UPDATE sinh_dung_cu SET {set_clause} WHERE ma=?", values)
    row = query("SELECT * FROM sinh_dung_cu WHERE ma=?", (ma,))
    return row[0] if row else {}


def list_sinh_pham(tu: str = "", loai: str = "", tim: str = "") -> list[dict]:
    conditions = []
    args = []
    if tu:
        conditions.append("tu = ?")
        args.append(tu)
    if loai:
        conditions.append("loai = ?")
        args.append(loai)
    if tim:
        conditions.append("(id LIKE ? OR ten LIKE ? OR so_lo LIKE ?)")
        like_arg = f"%{tim.strip()}%"
        args.extend([like_arg, like_arg, like_arg])

    where = f"WHERE {' AND '.join(conditions)}" if conditions else ""
    return query(f"SELECT * FROM sinh_pham {where} ORDER BY id ASC", tuple(args))


def add_sinh_pham(item: dict) -> dict:
    cap = item.get("cap_an_toan", "BSL-1")
    if cap != "BSL-1":
        raise ValueError(f"Chỉ cho phép sinh phẩm cấp an toàn sinh học BSL-1 (nhận '{cap}').")

    sid = item["id"]
    now = time.time()
    han_dung = float(item.get("han_dung", now + 180 * 86400.0))
    execute(
        "INSERT OR REPLACE INTO sinh_pham (id, ten, loai, tu, ngan, so_luong, don_vi, so_lo, han_dung, cap_an_toan, ma_chat, cap_nhat) "
        "VALUES (?,?,?,?,?,?,?,?,?,?,?,?)",
        (
            sid,
            item["ten"],
            item["loai"],
            item["tu"],
            item.get("ngan", ""),
            float(item.get("so_luong", 0)),
            item.get("don_vi", ""),
            item.get("so_lo", ""),
            han_dung,
            "BSL-1",
            item.get("ma_chat", ""),
            now,
        ),
    )
    return query("SELECT * FROM sinh_pham WHERE id=?", (sid,))[0]


def patch_sinh_pham(sid: str, fields: dict) -> dict:
    if "cap_an_toan" in fields and fields["cap_an_toan"] != "BSL-1":
        raise ValueError(f"Chỉ cho phép cấp an toàn BSL-1 (nhận '{fields['cap_an_toan']}').")

    allowed = {"ten", "loai", "tu", "ngan", "so_luong", "don_vi", "so_lo", "han_dung", "cap_an_toan", "ma_chat"}
    updates = {k: v for k, v in fields.items() if k in allowed}
    if updates:
        updates["cap_nhat"] = time.time()
        set_clause = ", ".join(f"{k} = ?" for k in updates)
        values = tuple(updates.values()) + (sid,)
        execute(f"UPDATE sinh_pham SET {set_clause} WHERE id=?", values)
    row = query("SELECT * FROM sinh_pham WHERE id=?", (sid,))
    return row[0] if row else {}


def delete_sinh_pham(sid: str) -> bool:
    cur = execute("DELETE FROM sinh_pham WHERE id=?", (sid,))
    return cur.rowcount > 0


def change_sinh_pham_stock(sid: str, delta: float, nguoi: str, ghi_chu: str) -> dict:
    rows = query("SELECT * FROM sinh_pham WHERE id=?", (sid,))
    if not rows:
        raise KeyError(f"Không tìm thấy sinh phẩm id={sid}")
    cur_qty = rows[0]["so_luong"]
    new_qty = max(0.0, cur_qty + delta)
    execute("UPDATE sinh_pham SET so_luong=?, cap_nhat=? WHERE id=?", (new_qty, time.time(), sid))
    return query("SELECT * FROM sinh_pham WHERE id=?", (sid,))[0]


def list_nhiet_do(tu: str = "", tu_luc: float = 0.0) -> list[dict]:
    conditions = []
    args = []
    if tu:
        conditions.append("tu = ?")
        args.append(tu)
    if tu_luc > 0.0:
        conditions.append("thoi_gian >= ?")
        args.append(tu_luc)
    where = f"WHERE {' AND '.join(conditions)}" if conditions else ""
    return query(f"SELECT * FROM nhiet_do_tu {where} ORDER BY thoi_gian ASC", tuple(args))


def add_nhiet_do(tu: str, nhiet_do: float, cua_mo: int = 0, thoi_gian: float = 0.0) -> dict:
    now = thoi_gian if thoi_gian > 0.0 else time.time()
    cur = execute(
        "INSERT INTO nhiet_do_tu (tu, thoi_gian, nhiet_do, cua_mo) VALUES (?,?,?,?)",
        (tu, now, float(nhiet_do), int(cua_mo)),
    )
    return query("SELECT * FROM nhiet_do_tu WHERE id=?", (cur.lastrowid,))[0]


def list_dat_lich(doi_tuong: str = "") -> list[dict]:
    if doi_tuong:
        return query("SELECT * FROM dat_lich WHERE doi_tuong=? ORDER BY bat_dau ASC", (doi_tuong,))
    return query("SELECT * FROM dat_lich ORDER BY bat_dau ASC")


def check_dat_lich_conflict(doi_tuong: str, bat_dau: float, ket_thuc: float, exclude_id: int = 0) -> list[dict]:
    sql = "SELECT * FROM dat_lich WHERE doi_tuong = ? AND id != ? AND max(bat_dau, ?) < min(ket_thuc, ?)"
    # Trong SQLite chuẩn: bat_dau < ? AND ket_thuc > ?
    sql = "SELECT * FROM dat_lich WHERE doi_tuong = ? AND id != ? AND bat_dau < ? AND ket_thuc > ?"
    return query(sql, (doi_tuong, exclude_id, ket_thuc, bat_dau))


def add_dat_lich(item: dict) -> tuple[dict | None, list[dict]]:
    doi_tuong = item["doi_tuong"]
    bat_dau = float(item["bat_dau"])
    ket_thuc = float(item["ket_thuc"])
    if ket_thuc <= bat_dau:
        raise ValueError("Thời gian kết thúc phải sau thời gian bắt đầu.")

    conflicts = check_dat_lich_conflict(doi_tuong, bat_dau, ket_thuc)
    if conflicts:
        return None, conflicts

    now = time.time()
    cur = execute(
        "INSERT INTO dat_lich (doi_tuong, nguoi, lop, bat_dau, ket_thuc, muc_dich, tao_luc) VALUES (?,?,?,?,?,?,?)",
        (doi_tuong, item["nguoi"], item["lop"], bat_dau, ket_thuc, item.get("muc_dich", ""), now),
    )
    return query("SELECT * FROM dat_lich WHERE id=?", (cur.lastrowid,))[0], []


def patch_dat_lich(id: int, item: dict) -> tuple[dict | None, list[dict]]:
    cur_rows = query("SELECT * FROM dat_lich WHERE id=?", (id,))
    if not cur_rows:
        return None, []
    current = cur_rows[0]

    doi_tuong = item.get("doi_tuong", current["doi_tuong"])
    bat_dau = float(item.get("bat_dau", current["bat_dau"]))
    ket_thuc = float(item.get("ket_thuc", current["ket_thuc"]))
    if ket_thuc <= bat_dau:
        raise ValueError("Thời gian kết thúc phải sau thời gian bắt đầu.")

    conflicts = check_dat_lich_conflict(doi_tuong, bat_dau, ket_thuc, exclude_id=id)
    if conflicts:
        return None, conflicts

    allowed = {"doi_tuong", "nguoi", "lop", "bat_dau", "ket_thuc", "muc_dich"}
    updates = {k: v for k, v in item.items() if k in allowed}
    if updates:
        set_clause = ", ".join(f"{k} = ?" for k in updates)
        values = tuple(updates.values()) + (id,)
        execute(f"UPDATE dat_lich SET {set_clause} WHERE id=?", values)
    return query("SELECT * FROM dat_lich WHERE id=?", (id,))[0], []


def delete_dat_lich(id: int) -> bool:
    cur = execute("DELETE FROM dat_lich WHERE id=?", (id,))
    return cur.rowcount > 0


def list_muon_tra(ma_dung_cu: str = "", nguoi: str = "") -> list[dict]:
    conditions = []
    args = []
    if ma_dung_cu:
        conditions.append("ma_dung_cu = ?")
        args.append(ma_dung_cu)
    if nguoi:
        conditions.append("nguoi = ?")
        args.append(nguoi)
    where = f"WHERE {' AND '.join(conditions)}" if conditions else ""
    return query(f"SELECT * FROM muon_tra {where} ORDER BY luc_muon DESC", tuple(args))


def add_muon_tra(item: dict) -> dict:
    now = time.time()
    ma = item["ma_dung_cu"]
    sl = int(item.get("so_luong") or 1)
    ht_val = item.get("han_tra")
    han_tra = float(ht_val) if ht_val is not None else (now + 7200.0)
    cur = execute(
        "INSERT INTO muon_tra (ma_dung_cu, so_luong, nguoi, lop, luc_muon, han_tra, tinh_trang_tra) VALUES (?,?,?,?,?,?,?)",
        (ma, sl, item["nguoi"], item["lop"], now, han_tra, "chua_tra"),
    )
    # Tăng dang_muon trong sinh_dung_cu
    execute("UPDATE sinh_dung_cu SET dang_muon = dang_muon + ? WHERE ma = ?", (sl, ma))
    return query("SELECT * FROM muon_tra WHERE id=?", (cur.lastrowid,))[0]


def patch_muon_tra(id: int, item: dict) -> dict:
    cur_rows = query("SELECT * FROM muon_tra WHERE id=?", (id,))
    if not cur_rows:
        return {}
    current = cur_rows[0]

    lt_val = item.get("luc_tra")
    luc_tra = float(lt_val) if lt_val is not None else time.time()
    tinh_trang = item.get("tinh_trang_tra") or "tot"

    execute("UPDATE muon_tra SET luc_tra = ?, tinh_trang_tra = ? WHERE id = ?", (luc_tra, tinh_trang, id))

    # Nếu trước đó chưa trả mà nay trả: giảm dang_muon, nếu hỏng thì tăng hong
    if current["tinh_trang_tra"] == "chua_tra":
        ma = current["ma_dung_cu"]
        sl = current["so_luong"]
        execute("UPDATE sinh_dung_cu SET dang_muon = max(0, dang_muon - ?) WHERE ma = ?", (sl, ma))
        if tinh_trang == "hong":
            execute("UPDATE sinh_dung_cu SET hong = hong + ? WHERE ma = ?", (sl, ma))

    return query("SELECT * FROM muon_tra WHERE id=?", (id,))[0]


def list_ket_qua(lop: str = "", ma_bai: str = "", hoc_sinh: str = "") -> list[dict]:
    conditions = []
    args = []
    if lop:
        conditions.append("lop = ?")
        args.append(lop)
    if ma_bai:
        conditions.append("ma_bai = ?")
        args.append(ma_bai)
    if hoc_sinh:
        conditions.append("hoc_sinh LIKE ?")
        args.append(f"%{hoc_sinh.strip()}%")
    where = f"WHERE {' AND '.join(conditions)}" if conditions else ""
    return query(f"SELECT * FROM ket_qua_tn {where} ORDER BY thoi_gian DESC", tuple(args))


def list_ket_qua_theo_hoc_sinh(hoc_sinh_ids: list[int], ma_bai: str = "") -> list[dict]:
    if not hoc_sinh_ids:
        return []
    dieu_kien = f"hoc_sinh_id IN ({','.join('?' * len(hoc_sinh_ids))})"
    args = list(hoc_sinh_ids)
    if ma_bai:
        dieu_kien += " AND ma_bai = ?"
        args.append(ma_bai)
    return query(f"SELECT * FROM ket_qua_tn WHERE {dieu_kien} ORDER BY thoi_gian DESC", tuple(args))


def get_ket_qua_by_id(id: int) -> dict | None:
    rows = query("SELECT * FROM ket_qua_tn WHERE id=?", (id,))
    return rows[0] if rows else None


def add_ket_qua(item: dict) -> dict:
    t_val = item.get("thoi_gian")
    now = float(t_val) if t_val is not None else time.time()
    du_lieu = item.get("du_lieu") or "{}"
    if isinstance(du_lieu, dict):
        du_lieu = json.dumps(du_lieu, ensure_ascii=False)
    diem = float(item["diem"]) if "diem" in item and item["diem"] is not None else None
    cur = execute(
        "INSERT INTO ket_qua_tn (hoc_sinh, lop, ban, ma_bai, thoi_gian, du_lieu, diem, nhan_xet, hoc_sinh_id) VALUES (?,?,?,?,?,?,?,?,?)",
        (item["hoc_sinh"], item["lop"], item.get("ban", ""), item["ma_bai"], now, du_lieu, diem, item.get("nhan_xet", ""), item.get("hoc_sinh_id")),
    )
    return query("SELECT * FROM ket_qua_tn WHERE id=?", (cur.lastrowid,))[0]


def patch_ket_qua(id: int, item: dict) -> dict:
    allowed = {"diem", "nhan_xet", "du_lieu"}
    updates = {}
    for k in allowed:
        if k in item:
            val = item[k]
            if k == "du_lieu" and isinstance(val, dict):
                val = json.dumps(val, ensure_ascii=False)
            updates[k] = val
    if updates:
        set_clause = ", ".join(f"{k} = ?" for k in updates)
        values = tuple(updates.values()) + (id,)
        execute(f"UPDATE ket_qua_tn SET {set_clause} WHERE id=?", values)
    return query("SELECT * FROM ket_qua_tn WHERE id=?", (id,))[0]


def list_su_co(phong: str = "", trang_thai: str = "") -> list[dict]:
    conditions = []
    args = []
    if phong:
        conditions.append("phong = ?")
        args.append(phong)
    if trang_thai:
        conditions.append("trang_thai = ?")
        args.append(trang_thai)
    where = f"WHERE {' AND '.join(conditions)}" if conditions else ""
    return query(f"SELECT * FROM su_co_sinh {where} ORDER BY thoi_gian DESC", tuple(args))


def add_su_co(item: dict) -> dict:
    t_val = item.get("thoi_gian")
    now = float(t_val) if t_val is not None else time.time()
    cur = execute(
        "INSERT INTO su_co_sinh (phong, loai, muc_do, mo_ta, xu_ly, trang_thai, thoi_gian) VALUES (?,?,?,?,?,?,?)",
        (
            item["phong"],
            item["loai"],
            item.get("muc_do") or "Info",
            item["mo_ta"],
            item.get("xu_ly") or "",
            item.get("trang_thai") or "moi",
            now,
        ),
    )
    return query("SELECT * FROM su_co_sinh WHERE id=?", (cur.lastrowid,))[0]


def patch_su_co(id: int, item: dict) -> dict:
    allowed = {"mo_ta", "xu_ly", "trang_thai", "muc_do"}
    updates = {k: v for k, v in item.items() if k in allowed}
    if updates:
        set_clause = ", ".join(f"{k} = ?" for k in updates)
        values = tuple(updates.values()) + (id,)
        execute(f"UPDATE su_co_sinh SET {set_clause} WHERE id=?", values)
    return query("SELECT * FROM su_co_sinh WHERE id=?", (id,))[0]


def list_bao_tri(ma_dung_cu: str = "") -> list[dict]:
    if ma_dung_cu:
        return query("SELECT * FROM bao_tri WHERE ma_dung_cu=? ORDER BY ngay DESC", (ma_dung_cu,))
    return query("SELECT * FROM bao_tri ORDER BY ngay DESC")


def add_bao_tri(item: dict) -> dict:
    ng_val = item.get("ngay")
    now = float(ng_val) if ng_val is not None else time.time()
    ma = item["ma_dung_cu"]
    loai = item["loai"]
    cur = execute(
        "INSERT INTO bao_tri (ma_dung_cu, loai, ngay, nguoi, ket_qua, ghi_chu) VALUES (?,?,?,?,?,?)",
        (ma, loai, now, item["nguoi"], item["ket_qua"], item.get("ghi_chu") or ""),
    )
    # Cập nhật lan_hieu_chuan / han_hieu_chuan nếu là hiệu chuẩn
    if "hieu_chuan" in loai.lower():
        execute(
            "UPDATE sinh_dung_cu SET lan_hieu_chuan = ?, han_hieu_chuan = ?, can_bao_tri = 0 WHERE ma = ?",
            (now, now + 180 * 86400.0, ma),
        )
    return query("SELECT * FROM bao_tri WHERE id=?", (cur.lastrowid,))[0]
