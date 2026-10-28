"""CSDL SQLite (data/app.db): bảng users (người dùng + key Gemini đã mã hoá + lượt dùng thử) và solutions (lịch sử bài giải);
email_codes (mã xác thực email, chỉ lưu bản băm), activity_log (nhật ký đăng ký/đăng nhập… – nhat_ky.py), name_changes (lịch sử đổi tên),
app_settings (cài đặt chung do admin đổi, vd chữ trên nhãn "thành viên").

Mỗi lần gọi mở một kết nối ngắn (an toàn khi dùng từ nhiều thread: request FastAPI + thread giải bài).
Key Gemini chỉ được lưu ở dạng mã hoá (bao_mat.ma_hoa_key) + 4 ký tự cuối để hiển thị.
"""
from __future__ import annotations

import json
import logging
import sqlite3
import time
from contextlib import contextmanager
from dataclasses import dataclass, field
from pathlib import Path

from .config import DATA_DIR

DUONG_DAN = DATA_DIR / "app.db"
SUB_CUC_BO = "cuc-bo"  # người dùng của "chế độ cục bộ" (chưa cấu hình đăng nhập Google, mở app trên chính máy này)
SUB_EMAIL = "email:"   # tài khoản email + mật khẩu: google_sub = "email:<email viết thường>" (Google: sub của Google)

_BANG = """
CREATE TABLE IF NOT EXISTS users (
    id               INTEGER PRIMARY KEY AUTOINCREMENT,
    google_sub       TEXT NOT NULL UNIQUE,
    email            TEXT NOT NULL DEFAULT '',
    name             TEXT NOT NULL DEFAULT '',
    picture          TEXT NOT NULL DEFAULT '',
    gemini_key_enc   TEXT,
    gemini_key_last4 TEXT,
    password_hash    TEXT,
    is_admin         INTEGER NOT NULL DEFAULT 0,
    trial_used       INTEGER NOT NULL DEFAULT 0,
    created_at       TEXT NOT NULL,
    last_login_at    TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS solutions (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id     INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    job_id      TEXT NOT NULL UNIQUE,
    problem     TEXT NOT NULL DEFAULT '',
    hsg         INTEGER NOT NULL DEFAULT 0,
    demo        INTEGER NOT NULL DEFAULT 0,
    key_source  TEXT NOT NULL DEFAULT '',
    status      TEXT NOT NULL DEFAULT 'dang_giai',
    model       TEXT NOT NULL DEFAULT '',
    error       TEXT NOT NULL DEFAULT '',
    result_json TEXT,
    created_at  TEXT NOT NULL,
    finished_at TEXT
);
CREATE INDEX IF NOT EXISTS solutions_user ON solutions(user_id, id DESC);
CREATE TABLE IF NOT EXISTS email_codes (
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    email_norm TEXT NOT NULL,
    purpose    TEXT NOT NULL,
    user_id    INTEGER REFERENCES users(id) ON DELETE CASCADE,
    code_hash  TEXT NOT NULL,
    created    REAL NOT NULL,
    expires    REAL NOT NULL,
    attempts   INTEGER NOT NULL DEFAULT 0,
    status     TEXT NOT NULL DEFAULT 'cho'
);
CREATE INDEX IF NOT EXISTS email_codes_email ON email_codes(email_norm, created);
CREATE TABLE IF NOT EXISTS activity_log (
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    ts         REAL NOT NULL,
    event      TEXT NOT NULL,
    user_id    INTEGER,
    target_id  INTEGER,
    email      TEXT NOT NULL DEFAULT '',
    name       TEXT NOT NULL DEFAULT '',
    method     TEXT NOT NULL DEFAULT '',
    ip         TEXT NOT NULL DEFAULT '',
    user_agent TEXT NOT NULL DEFAULT '',
    device     TEXT NOT NULL DEFAULT '',
    detail     TEXT NOT NULL DEFAULT '',
    ip_v       INTEGER NOT NULL DEFAULT 0,
    geo_qg     TEXT NOT NULL DEFAULT '',
    geo_noi    TEXT NOT NULL DEFAULT '',
    geo_mang   TEXT NOT NULL DEFAULT ''
);
CREATE INDEX IF NOT EXISTS activity_ts ON activity_log(ts);
CREATE INDEX IF NOT EXISTS activity_user ON activity_log(user_id, id);
CREATE INDEX IF NOT EXISTS activity_ip ON activity_log(ip, ts);
CREATE INDEX IF NOT EXISTS activity_event ON activity_log(event, ts);
CREATE TABLE IF NOT EXISTS name_changes (
    id       INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id  INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    old_name TEXT NOT NULL DEFAULT '',
    new_name TEXT NOT NULL DEFAULT '',
    ts       REAL NOT NULL,
    ip       TEXT NOT NULL DEFAULT ''
);
CREATE INDEX IF NOT EXISTS name_changes_user ON name_changes(user_id, ts);
CREATE TABLE IF NOT EXISTS app_settings (
    key   TEXT PRIMARY KEY,
    value TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS claude_codes (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    code_hash    TEXT NOT NULL UNIQUE,
    hint         TEXT NOT NULL DEFAULT '',
    uses_total   INTEGER NOT NULL,
    uses_used    INTEGER NOT NULL DEFAULT 0,
    uses_held    INTEGER NOT NULL DEFAULT 0,
    expires      TEXT NOT NULL,
    max_accounts INTEGER NOT NULL DEFAULT 1,
    note         TEXT NOT NULL DEFAULT '',
    created      REAL NOT NULL,
    created_by   INTEGER,
    revoked      REAL
);
CREATE TABLE IF NOT EXISTS claude_code_users (
    code_id  INTEGER NOT NULL REFERENCES claude_codes(id) ON DELETE CASCADE,
    user_id  INTEGER NOT NULL UNIQUE REFERENCES users(id) ON DELETE CASCADE,
    attached REAL NOT NULL,
    PRIMARY KEY (code_id, user_id)
);
CREATE TABLE IF NOT EXISTS claude_code_fails (
    id      INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id INTEGER,
    ip      TEXT NOT NULL DEFAULT '',
    ts      REAL NOT NULL
);
CREATE INDEX IF NOT EXISTS claude_code_fails_user ON claude_code_fails(user_id, ts);
CREATE INDEX IF NOT EXISTS claude_code_fails_ip ON claude_code_fails(ip, ts);
CREATE TABLE IF NOT EXISTS ai_usage (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    ts           REAL NOT NULL,
    user_id      INTEGER,
    job_id       TEXT NOT NULL DEFAULT '',
    kind         TEXT NOT NULL DEFAULT '',
    step         TEXT NOT NULL DEFAULT '',
    level        TEXT NOT NULL DEFAULT '',
    tier         TEXT NOT NULL DEFAULT '',
    provider     TEXT NOT NULL DEFAULT '',
    model        TEXT NOT NULL DEFAULT '',
    key_source   TEXT NOT NULL DEFAULT '',
    tokens_in    INTEGER NOT NULL DEFAULT 0,
    tokens_out   INTEGER NOT NULL DEFAULT 0,
    tokens_think INTEGER NOT NULL DEFAULT 0,
    upgraded     INTEGER NOT NULL DEFAULT 0,
    ok           INTEGER NOT NULL DEFAULT 1,
    error        TEXT NOT NULL DEFAULT '',
    code_id      INTEGER
);
CREATE INDEX IF NOT EXISTS ai_usage_ts ON ai_usage(ts);
CREATE INDEX IF NOT EXISTS ai_usage_user ON ai_usage(user_id, ts);
CREATE TABLE IF NOT EXISTS claude_key_users (
    user_id     INTEGER PRIMARY KEY REFERENCES users(id) ON DELETE CASCADE,
    activated   REAL NOT NULL,
    fingerprint TEXT NOT NULL DEFAULT '',
    revoked     REAL,
    revoked_by  INTEGER
);
CREATE TABLE IF NOT EXISTS claude_key_fails (
    id      INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id INTEGER,
    ip      TEXT NOT NULL DEFAULT '',
    ts      REAL NOT NULL
);
CREATE INDEX IF NOT EXISTS claude_key_fails_user ON claude_key_fails(user_id, ts);
CREATE INDEX IF NOT EXISTS claude_key_fails_ip ON claude_key_fails(ip, ts);
CREATE TABLE IF NOT EXISTS claude_key_uses (
    id      INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id INTEGER NOT NULL,
    day     TEXT NOT NULL,
    ts      REAL NOT NULL,
    kind    TEXT NOT NULL DEFAULT '',
    job_id  TEXT NOT NULL DEFAULT '',
    done    INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS claude_key_uses_user ON claude_key_uses(user_id, day);
CREATE TABLE IF NOT EXISTS giai_cache (
    key     TEXT PRIMARY KEY,
    created REAL NOT NULL,
    level   TEXT NOT NULL DEFAULT '',
    data    TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS lit_cache (
    key     TEXT PRIMARY KEY,
    created REAL NOT NULL,
    kind    TEXT NOT NULL DEFAULT '',
    data    TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS mo_hinh_3d_cache (
    key     TEXT PRIMARY KEY,
    smiles  TEXT NOT NULL DEFAULT '',
    created REAL NOT NULL,
    data    TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS mo_hinh_3d_smiles ON mo_hinh_3d_cache(smiles);
CREATE TABLE IF NOT EXISTS claude_nap (
    id       INTEGER PRIMARY KEY AUTOINCREMENT,
    ts       REAL NOT NULL,
    usd      REAL NOT NULL,
    ghi_chu  TEXT NOT NULL DEFAULT '',
    admin_id INTEGER
);
-- chặn email ảo / rác (email_rac.py): nhật ký các lần bị chặn (giữ 90 ngày) + danh sách chặn / cho phép của admin
CREATE TABLE IF NOT EXISTS email_blocks (
    id     INTEGER PRIMARY KEY AUTOINCREMENT,
    ts     REAL NOT NULL,
    email  TEXT NOT NULL DEFAULT '',
    ip     TEXT NOT NULL DEFAULT '',
    reason TEXT NOT NULL,              -- email_tam / ten_mien_bi_chan / khong_mx / honeypot / captcha / qua_nhieu_lan_thu …
    detail TEXT NOT NULL DEFAULT '',
    action TEXT NOT NULL DEFAULT 'dang_ky'
);
CREATE INDEX IF NOT EXISTS email_blocks_ip ON email_blocks(ip, ts);
-- router AI (ai/router.py): mỗi lần AI lỗi và chuyển sang AI dự phòng (giữ 30 ngày)
CREATE TABLE IF NOT EXISTS ai_fallbacks (
    id       INTEGER PRIMARY KEY AUTOINCREMENT,
    ts       REAL NOT NULL,
    he_thong TEXT NOT NULL,
    tu       TEXT NOT NULL,
    sang     TEXT NOT NULL,
    ly_do    TEXT NOT NULL DEFAULT '',
    user_id  INTEGER,
    job_id   TEXT NOT NULL DEFAULT ''
);
-- AI Riêng (backend/ai_rieng): kho tri thức admin dạy (tài liệu → đoạn, quy tắc, bài mẫu = thư viện lời giải, sửa sai), báo sai của
-- người dùng (hàng chờ), bộ đề kiểm tra + lịch sử điểm, nhật ký từng lần giải (tầng thư viện / kiến thức / mô hình mạnh). vec = float32
CREATE TABLE IF NOT EXISTS ar_tai_lieu (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    ten          TEXT NOT NULL,
    loai         TEXT NOT NULL,               -- pdf | docx | anh | link | van_ban
    nguon        TEXT NOT NULL DEFAULT '',    -- tên file gốc / URL
    tep          TEXT NOT NULL DEFAULT '',    -- file gốc trong <thư mục CSDL>/ai_rieng/ (để xử lý lại)
    chu_de       TEXT NOT NULL DEFAULT '',
    co_loi_giai  INTEGER NOT NULL DEFAULT 0,  -- có đề + lời giải chuỗi → trích thành bài mẫu chờ duyệt
    trang_thai   TEXT NOT NULL DEFAULT 'cho', -- cho | dang_tach | da_nap | loi
    tien_do      TEXT NOT NULL DEFAULT '',
    loi          TEXT NOT NULL DEFAULT '',
    so_doan      INTEGER NOT NULL DEFAULT 0,
    so_bai_trich INTEGER NOT NULL DEFAULT 0,
    bat          INTEGER NOT NULL DEFAULT 1,
    kich_thuoc   INTEGER NOT NULL DEFAULT 0,
    tao_luc      REAL NOT NULL,
    xong_luc     REAL
);
CREATE TABLE IF NOT EXISTS ar_doan (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    tai_lieu_id INTEGER NOT NULL REFERENCES ar_tai_lieu(id) ON DELETE CASCADE,
    thu_tu      INTEGER NOT NULL,
    trang       INTEGER,
    noi_dung    TEXT NOT NULL,
    vec         BLOB
);
CREATE INDEX IF NOT EXISTS ar_doan_tl ON ar_doan(tai_lieu_id, thu_tu);
CREATE TABLE IF NOT EXISTS ar_kien_thuc (
    id       INTEGER PRIMARY KEY AUTOINCREMENT,
    tieu_de  TEXT NOT NULL,
    noi_dung TEXT NOT NULL,
    chu_de   TEXT NOT NULL DEFAULT '',
    uu_tien  INTEGER NOT NULL DEFAULT 3,      -- 1 (thấp) … 5 (cao)
    bat      INTEGER NOT NULL DEFAULT 1,
    vec      BLOB,
    tao_luc  REAL NOT NULL,
    sua_luc  REAL NOT NULL
);
CREATE TABLE IF NOT EXISTS ar_bai_mau (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    de          TEXT NOT NULL,
    de_khoa     TEXT NOT NULL,                -- đề đã chuẩn hoá (khớp thư viện – tầng 1)
    loi_giai    TEXT NOT NULL,                -- JSON gọn: chuoi[], buoc[] (+ phuong_trinh), luu_y[], dap_an
    chu_de      TEXT NOT NULL DEFAULT '',
    trang_thai  TEXT NOT NULL DEFAULT 'duyet', -- duyet | cho_duyet | sai
    nguon       TEXT NOT NULL DEFAULT 'tay',   -- tay | san_tap | ai | tai_lieu | nhap
    tai_lieu_id INTEGER,
    trang       INTEGER,
    rdkit_ok    INTEGER NOT NULL DEFAULT 0,
    mo_hinh     TEXT NOT NULL DEFAULT '',
    nghi_van    TEXT NOT NULL DEFAULT '',     -- Gemini đối chiếu cho kết quả khác → không dùng ở tầng 1 khi chưa duyệt
    so_lan_dung INTEGER NOT NULL DEFAULT 0,
    bat         INTEGER NOT NULL DEFAULT 1,
    vec         BLOB,
    tao_luc     REAL NOT NULL,
    sua_luc     REAL NOT NULL
);
CREATE INDEX IF NOT EXISTS ar_bai_mau_khoa ON ar_bai_mau(de_khoa);
CREATE TABLE IF NOT EXISTS ar_sua_sai (
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    de         TEXT NOT NULL,
    de_khoa    TEXT NOT NULL,
    sai        TEXT NOT NULL DEFAULT '',      -- AI đã trả lời sai thế nào
    dung       TEXT NOT NULL,                 -- đáp án đúng
    ly_do      TEXT NOT NULL DEFAULT '',
    chu_de     TEXT NOT NULL DEFAULT '',
    bao_sai_id INTEGER,
    bat        INTEGER NOT NULL DEFAULT 1,
    vec        BLOB,
    tao_luc    REAL NOT NULL,
    sua_luc    REAL NOT NULL
);
CREATE INDEX IF NOT EXISTS ar_sua_sai_khoa ON ar_sua_sai(de_khoa);
CREATE TABLE IF NOT EXISTS ar_bao_sai (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    ts          REAL NOT NULL,
    user_id     INTEGER,
    job_id      TEXT NOT NULL,
    de          TEXT NOT NULL,
    mo_hinh     TEXT NOT NULL DEFAULT '',
    loi_giai    TEXT NOT NULL DEFAULT '',     -- JSON gọn của lời giải bị báo sai
    ghi_chu     TEXT NOT NULL DEFAULT '',
    trang_thai  TEXT NOT NULL DEFAULT 'cho',  -- cho | da_sua | bo_qua
    xu_ly_luc   REAL,
    sua_sai_id  INTEGER,
    UNIQUE (user_id, job_id)
);
CREATE TABLE IF NOT EXISTS ar_bo_de (
    id      INTEGER PRIMARY KEY AUTOINCREMENT,
    de      TEXT NOT NULL,
    dap_an  TEXT NOT NULL,                    -- JSON [{ki_hieu, chat (công thức / tên), smiles?}]
    chu_de  TEXT NOT NULL DEFAULT '',
    bat     INTEGER NOT NULL DEFAULT 1,
    tao_luc REAL NOT NULL
);
CREATE TABLE IF NOT EXISTS ar_kiem_tra (
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    bat_dau    REAL NOT NULL,
    xong       REAL,
    trang_thai TEXT NOT NULL DEFAULT 'dang_chay', -- dang_chay | xong | loi
    so_cau     INTEGER NOT NULL DEFAULT 0,
    da_cham    INTEGER NOT NULL DEFAULT 0,
    so_dung    INTEGER NOT NULL DEFAULT 0,
    cau_hinh   TEXT NOT NULL DEFAULT '',
    chi_tiet   TEXT NOT NULL DEFAULT '[]'
);
CREATE TABLE IF NOT EXISTS ar_lan_giai (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    ts          REAL NOT NULL,
    user_id     INTEGER,
    job_id      TEXT NOT NULL DEFAULT '',
    tang        TEXT NOT NULL,                -- thu_vien | kien_thuc | manh | chua_day | loi
    bai_mau_id  INTEGER,
    mo_hinh     TEXT NOT NULL DEFAULT '',
    so_muc      INTEGER NOT NULL DEFAULT 0,
    tokens      INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS ar_lan_giai_ts ON ar_lan_giai(ts);
-- tìm theo chữ (công thức như CH3COOH, HgSO4 khớp chính xác); loai: doan | kien_thuc | bai_mau | sua_sai
CREATE VIRTUAL TABLE IF NOT EXISTS ar_fts USING fts5(noi_dung, loai UNINDEXED, ref_id UNINDEXED, tokenize = 'unicode61 remove_diacritics 2');
-- mã giao diện (ma_giao_dien.py): chỉ lưu HMAC; mỗi mã → một theme, hạn dùng + số lượt tối đa; lượt dùng theo tài khoản; lần nhập sai
CREATE TABLE IF NOT EXISTS theme_codes (
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    code_hash  TEXT NOT NULL UNIQUE,
    code_enc   TEXT NOT NULL DEFAULT '',  -- mã gốc MÃ HOÁ (Fernet, khoá chính) – chỉ để admin xem lại / chép đưa người khác
    hint       TEXT NOT NULL DEFAULT '',
    theme_id   TEXT NOT NULL,
    max_uses   INTEGER,                -- NULL = không giới hạn
    used       INTEGER NOT NULL DEFAULT 0,
    expires    TEXT,                   -- YYYY-MM-DD (dùng được hết ngày đó), NULL = không hạn
    revoked_at REAL,
    note       TEXT NOT NULL DEFAULT '',
    created    REAL NOT NULL,
    admin_id   INTEGER
);
CREATE TABLE IF NOT EXISTS theme_code_uses (
    code_id INTEGER NOT NULL,
    user_id INTEGER NOT NULL,
    ts      REAL NOT NULL,
    ip      TEXT NOT NULL DEFAULT '',
    PRIMARY KEY (code_id, user_id)
);
CREATE TABLE IF NOT EXISTS theme_code_fails (
    user_id INTEGER,
    ip      TEXT NOT NULL DEFAULT '',
    ts      REAL NOT NULL
);
CREATE TABLE IF NOT EXISTS email_domain_rules (
    domain   TEXT PRIMARY KEY,         -- tên miền hoặc mẫu có dấu * (vd tempmail.*, *.xyz)
    kind     TEXT NOT NULL,            -- "chan" (blacklist) | "cho" (whitelist)
    note     TEXT NOT NULL DEFAULT '',
    created  REAL NOT NULL,
    admin_id INTEGER
);
CREATE TABLE IF NOT EXISTS notebooklm_accounts (
    id               INTEGER PRIMARY KEY AUTOINCREMENT,
    ten_goi_nho      TEXT NOT NULL,
    email            TEXT NOT NULL DEFAULT '',
    picture          TEXT NOT NULL DEFAULT '',
    cookie_enc       TEXT NOT NULL,
    status           TEXT NOT NULL DEFAULT 'dang_hoat_dong',
    error_msg        TEXT NOT NULL DEFAULT '',
    is_default       INTEGER NOT NULL DEFAULT 0,
    so_notebook      INTEGER NOT NULL DEFAULT 0,
    last_sync_at     REAL,
    created_at       REAL NOT NULL,
    updated_at       REAL NOT NULL
);
CREATE TABLE IF NOT EXISTS notebooklm_cache (
    id               INTEGER PRIMARY KEY AUTOINCREMENT,
    account_id       INTEGER NOT NULL REFERENCES notebooklm_accounts(id) ON DELETE CASCADE,
    notebook_id      TEXT NOT NULL,
    title            TEXT NOT NULL DEFAULT '',
    description      TEXT NOT NULL DEFAULT '',
    sources_json     TEXT NOT NULL DEFAULT '[]',
    notes_json       TEXT NOT NULL DEFAULT '[]',
    so_nguon         INTEGER NOT NULL DEFAULT 0,
    updated_at       REAL NOT NULL,
    UNIQUE(account_id, notebook_id)
);
CREATE INDEX IF NOT EXISTS idx_notebooklm_cache_acc ON notebooklm_cache(account_id);
CREATE TABLE IF NOT EXISTS sinh_hoc_phan_hoi (
    id               INTEGER PRIMARY KEY AUTOINCREMENT,
    ts               REAL NOT NULL,
    user_id          INTEGER,
    cau_hoi          TEXT NOT NULL,
    cau_tra_loi      TEXT NOT NULL,
    hai_long         INTEGER NOT NULL,
    ly_do            TEXT NOT NULL DEFAULT '',
    chi_tiet         TEXT NOT NULL DEFAULT ''
);
CREATE INDEX IF NOT EXISTS idx_sinh_hoc_phan_hoi_ts ON sinh_hoc_phan_hoi(ts);
-- Lộ trình học Sinh (chỉ admin – backend/lo_trinh.py): mỗi dòng = một bài đã đánh dấu hoàn thành
CREATE TABLE IF NOT EXISTS lo_trinh_tien_do (
    user_id          INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    bai_id           TEXT NOT NULL,
    xong_luc         REAL NOT NULL,
    PRIMARY KEY (user_id, bai_id)
);
-- Mã đăng nhập một lần cho ứng dụng desktop (PROMPT_XUAT_APP_CHEMISTRY.md)
CREATE TABLE IF NOT EXISTS app_login_codes (
    code        TEXT PRIMARY KEY,
    user_id     INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    created_at  REAL NOT NULL,
    expires_at  REAL NOT NULL,
    used        INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS idx_app_login_codes_exp ON app_login_codes(expires_at, used);
"""

# Cột thêm cho bảng users ở các bản sau (CSDL cũ được ALTER TABLE khi mở lần đầu)
_COT_THEM = {
    "password_hash": "TEXT",
    "is_admin": "INTEGER NOT NULL DEFAULT 0",
    "email_norm": "TEXT NOT NULL DEFAULT ''",          # email đã chuẩn hoá để chặn trùng (Gmail: bỏ dấu chấm, phần +...)
    "email_verified": "INTEGER NOT NULL DEFAULT 0",    # đã nhập đúng mã gửi qua email (Google: Google đã xác minh)
    "locked": "INTEGER NOT NULL DEFAULT 0",            # admin khoá tài khoản
    "locked_at": "TEXT",
    "register_ip": "TEXT NOT NULL DEFAULT ''",
    "last_seen": "REAL",                               # lần cuối có yêu cầu (epoch) → "đang online"
    "last_ip": "TEXT NOT NULL DEFAULT ''",
    "last_ua": "TEXT NOT NULL DEFAULT ''",
    "activity_seen_id": "INTEGER NOT NULL DEFAULT 0",  # admin: id nhật ký đã xem → chấm thông báo
    "is_veteran": "INTEGER NOT NULL DEFAULT 0",        # vai trò "kì cựu": giải không giới hạn bằng key chủ app, miễn xác thực email
    "claude_key_enc": "TEXT",                          # API key Claude RIÊNG của người dùng (Fernet – bao_mat.ma_hoa_key_claude)
    "claude_key_last4": "TEXT",
    "claude_key_checked": "REAL",                      # lúc kiểm tra key (gọi thử max_tokens 1)
    "allow_own_claude_key": "INTEGER NOT NULL DEFAULT 0",  # admin BẬT cho dùng API key Claude riêng (admin luôn được)
    "allow_own_claude_key_at": "REAL",                 # lúc admin bật
    "allow_own_claude_key_by": "INTEGER",              # admin nào bật
    "own_claude_key_off_at": "REAL",                   # lúc admin TẮT – key mã hoá giữ 30 ngày rồi xoá hẳn
    "use_own_claude_key": "INTEGER NOT NULL DEFAULT 1",    # công tắc "Dùng key của tôi" của người dùng
    "claude_key_error": "TEXT NOT NULL DEFAULT ''",    # lỗi gần nhất khi gọi bằng key riêng (hết tiền / bị thu hồi…) → "Key lỗi"
    "own_claude_key_req_at": "REAL",                   # người dùng bấm "Gửi yêu cầu cho admin" (xoá khi admin duyệt / từ chối)
    "verify_deadline": "REAL",                         # đăng ký mới: quá lúc này mà chưa xác minh email → tự xoá (NULL = tài khoản cũ, không xoá)
    "suspect_reason": "TEXT NOT NULL DEFAULT ''",      # email "nghi ngờ" (tên trông ngẫu nhiên…) – không chặn, chờ admin duyệt
    "suspect_at": "REAL",
    "suspect_review": "TEXT NOT NULL DEFAULT ''",      # '' = chờ duyệt · 'ok' = admin cho qua · 'khoa' = admin khoá
    "suspect_by": "INTEGER",                           # admin đã duyệt
}

# Cột thêm cho bảng activity_log: ip_v = 1 khi IP lấy bằng dia_chi_ip.get_client_ip (0 = ghi trước khi sửa cách lấy IP);
# geo_* = quốc gia (mã ISO), tỉnh/thành gần đúng, nhà mạng tra offline lúc ghi
_COT_NHAT_KY = {
    "target_id": "INTEGER",
    "ip_v": "INTEGER NOT NULL DEFAULT 0",
    "geo_qg": "TEXT NOT NULL DEFAULT ''",
    "geo_noi": "TEXT NOT NULL DEFAULT ''",
    "geo_mang": "TEXT NOT NULL DEFAULT ''",
}

# Cột thêm cho bảng solutions: level = mức độ đề đã phân loại (de | trung_binh | hsg), claude = bài giải bằng Claude (trừ lượt mã)
_COT_BAI_GIAI = {
    "level": "TEXT NOT NULL DEFAULT ''",
    "claude": "INTEGER NOT NULL DEFAULT 0",
    "smart_level": "TEXT NOT NULL DEFAULT ''",   # mức độ thông minh: nhanh | can_bang | ky | toi_da ("" = bài giải theo luồng cũ)
    "solve_seconds": "REAL",                       # thời gian giải (giây) – so sánh trước/sau
    "cached": "INTEGER NOT NULL DEFAULT 0",        # trả từ cache (không gọi AI)
    "lit_papers": "INTEGER NOT NULL DEFAULT -1",   # tra cứu tài liệu: số bài dùng làm căn cứ (-1 = không tra cứu)
    "lit_tokens": "INTEGER NOT NULL DEFAULT 0",    # ước tính token phần tài liệu đưa vào prompt
}

# Nhà cung cấp email cho phép thêm "+nhãn" trước @ (cùng một hộp thư); Gmail còn bỏ qua dấu chấm
_MIEN_GMAIL = {"gmail.com", "googlemail.com"}
_MIEN_DAU_CONG = _MIEN_GMAIL | {"outlook.com", "hotmail.com", "live.com", "icloud.com", "me.com", "proton.me", "protonmail.com"}


def email_chuan(email: str) -> str:
    """Khoá so sánh email (không phân biệt hoa thường): Gmail bỏ dấu chấm và phần "+..." trước @ → a.b+x@gmail.com = ab@gmail.com."""
    email = (email or "").strip().lower()
    if email.count("@") != 1:
        return email
    ten, mien = email.split("@")
    if mien in _MIEN_DAU_CONG:
        ten = ten.split("+", 1)[0]
    if mien in _MIEN_GMAIL:
        ten, mien = ten.replace(".", ""), "gmail.com"
    return f"{ten}@{mien}"


def _nang_cap(con: sqlite3.Connection) -> None:
    """Nâng cấp CSDL tạo từ bản cũ: thêm cột, điền email chuẩn hoá, tài khoản Google coi như đã xác thực email."""
    cot = {r[1] for r in con.execute("PRAGMA table_info(users)")}
    for ten, kieu in _COT_THEM.items():
        if ten not in cot:
            con.execute(f"ALTER TABLE users ADD COLUMN {ten} {kieu}")
    cot = {r[1] for r in con.execute("PRAGMA table_info(activity_log)")}
    for ten, kieu in _COT_NHAT_KY.items():
        if ten not in cot:
            con.execute(f"ALTER TABLE activity_log ADD COLUMN {ten} {kieu}")
    cot = {r[1] for r in con.execute("PRAGMA table_info(solutions)")}
    for ten, kieu in _COT_BAI_GIAI.items():
        if ten not in cot:
            con.execute(f"ALTER TABLE solutions ADD COLUMN {ten} {kieu}")
    if "nghi_van" not in {r[1] for r in con.execute("PRAGMA table_info(ar_bai_mau)")}:  # AI Riêng: đối chiếu Gemini
        con.execute("ALTER TABLE ar_bai_mau ADD COLUMN nghi_van TEXT NOT NULL DEFAULT ''")
    if "code_enc" not in {r[1] for r in con.execute("PRAGMA table_info(theme_codes)")}:  # bảng mã giao diện tạo trước khi có cột này
        con.execute("ALTER TABLE theme_codes ADD COLUMN code_enc TEXT NOT NULL DEFAULT ''")
    for r in con.execute("SELECT id, email FROM users WHERE email_norm = '' AND email <> ''").fetchall():
        con.execute("UPDATE users SET email_norm = ? WHERE id = ?", (email_chuan(r["email"]), r["id"]))
    con.execute("UPDATE users SET email_verified = 1 WHERE email_verified = 0 AND google_sub NOT LIKE ? AND google_sub <> ?",
                (SUB_EMAIL + "%", SUB_CUC_BO))
    try:  # mỗi email (đã chuẩn hoá) chỉ một tài khoản
        con.execute("CREATE UNIQUE INDEX IF NOT EXISTS users_email_norm ON users(email_norm) WHERE email_norm <> ''")
    except sqlite3.IntegrityError:
        logging.getLogger(__name__).warning("CSDL cũ có hai tài khoản trùng email (sau chuẩn hoá) – chỉ chặn trùng ở code")
        con.execute("CREATE INDEX IF NOT EXISTS users_email_norm_thuong ON users(email_norm)")


@dataclass
class NguoiDung:
    id: int
    google_sub: str
    email: str = ""
    ten: str = ""
    anh: str = ""
    key_ma_hoa: str | None = field(default=None, repr=False)
    key_cuoi: str = ""
    luot_da_dung: int = 0
    mat_khau_bam: str | None = field(default=None, repr=False)
    la_admin: bool = False  # chủ app cấp bằng `python -m backend.quan_tri` (quan-tri.command) – không dựa vào email
    email_chuan: str = ""
    da_xac_thuc_email: bool = False
    bi_khoa: bool = False
    tao_luc: str = ""
    dang_nhap_cuoi: str = ""
    ip_dang_ky: str = ""
    hoat_dong_cuoi: float | None = None
    da_xem_nhat_ky: int = 0
    la_ki_cuu: bool = False  # vai trò "kì cựu" (cấp bằng quan-tri.command): không có quyền quản trị

    @property
    def co_key(self) -> bool:
        return bool(self.key_ma_hoa)

    @property
    def can_xac_thuc(self) -> bool:
        """Tài khoản email + mật khẩu chưa xác thực email → chưa được đăng nhập/dùng thử (admin và kì cựu được miễn)."""
        return self.dang_nhap_bang == "email" and not self.da_xac_thuc_email and not self.la_admin and not self.la_ki_cuu

    @property
    def cuc_bo(self) -> bool:
        return self.google_sub == SUB_CUC_BO

    @property
    def dang_nhap_bang(self) -> str:
        """"google" | "email" (email + mật khẩu, email KHÔNG được xác minh) | "cuc_bo"."""
        if self.cuc_bo:
            return "cuc_bo"
        return "email" if self.google_sub.startswith(SUB_EMAIL) else "google"


def _bay_gio() -> str:
    return time.strftime("%Y-%m-%d %H:%M:%S")


_da_tao: set[Path] = set()


@contextmanager
def _ket_noi():
    duong_dan = DUONG_DAN
    duong_dan.parent.mkdir(parents=True, exist_ok=True)
    con = sqlite3.connect(duong_dan, timeout=15)
    con.row_factory = sqlite3.Row
    try:
        con.execute("PRAGMA foreign_keys = ON")
        if duong_dan not in _da_tao:
            con.execute("PRAGMA journal_mode = WAL")
            con.executescript(_BANG)
            with con:
                _nang_cap(con)
            _da_tao.add(duong_dan)
        with con:  # tự commit / rollback
            yield con
    finally:
        con.close()


def _nguoi_dung(row: sqlite3.Row | None) -> NguoiDung | None:
    if row is None:
        return None
    return NguoiDung(id=row["id"], google_sub=row["google_sub"], email=row["email"], ten=row["name"], anh=row["picture"],
                     key_ma_hoa=row["gemini_key_enc"], key_cuoi=row["gemini_key_last4"] or "", luot_da_dung=row["trial_used"],
                     mat_khau_bam=row["password_hash"], la_admin=bool(row["is_admin"]), email_chuan=row["email_norm"],
                     da_xac_thuc_email=bool(row["email_verified"]), bi_khoa=bool(row["locked"]), tao_luc=row["created_at"],
                     dang_nhap_cuoi=row["last_login_at"], ip_dang_ky=row["register_ip"], hoat_dong_cuoi=row["last_seen"],
                     da_xem_nhat_ky=row["activity_seen_id"], la_ki_cuu=bool(row["is_veteran"]))


# ---------------------------------------------------------------- users
def luu_nguoi_dung(google_sub: str, email: str = "", ten: str = "", anh: str = "") -> NguoiDung:
    """Tạo hoặc cập nhật người dùng theo `sub` của Google (mã định danh cố định, không đổi khi đổi email).
    Email Google đã được Google xác minh → email_verified = 1. Người dùng đã tự đổi tên trong app thì giữ tên đó."""
    with _ket_noi() as con:
        t = _bay_gio()
        sql = ("INSERT INTO users (google_sub, email, email_norm, email_verified, name, picture, created_at, last_login_at) "
               "VALUES (?, ?, ?, ?, ?, ?, ?, ?) "
               "ON CONFLICT(google_sub) DO UPDATE SET email = excluded.email, email_norm = excluded.email_norm, "
               "email_verified = excluded.email_verified, picture = excluded.picture, last_login_at = excluded.last_login_at, "
               "name = CASE WHEN EXISTS (SELECT 1 FROM name_changes WHERE user_id = users.id) THEN users.name ELSE excluded.name END")
        xac_minh = 0 if google_sub == SUB_CUC_BO else 1
        try:
            con.execute(sql, (google_sub, email, email_chuan(email), xac_minh, ten, anh, t, t))
        except sqlite3.IntegrityError:  # email này đã thuộc một tài khoản khác → vẫn lưu, không giữ khoá chống trùng
            con.execute(sql, (google_sub, email, "", xac_minh, ten, anh, t, t))
        return _nguoi_dung(con.execute("SELECT * FROM users WHERE google_sub = ?", (google_sub,)).fetchone())


def tao_tai_khoan_email(email: str, ten: str, mat_khau_bam: str, ip: str = "", da_xac_thuc: bool = False) -> NguoiDung | None:
    """Tạo tài khoản email + mật khẩu (đã băm), mặc định CHƯA xác thực email. Email (đã chuẩn hoá) đã có tài khoản → None."""
    with _ket_noi() as con:
        t = _bay_gio()
        try:
            con.execute(
                "INSERT INTO users (google_sub, email, email_norm, email_verified, name, password_hash, register_ip, created_at, last_login_at) "
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)",
                (SUB_EMAIL + email, email, email_chuan(email), int(da_xac_thuc), ten, mat_khau_bam, ip, t, t),
            )
        except sqlite3.IntegrityError:
            return None
        return _nguoi_dung(con.execute("SELECT * FROM users WHERE google_sub = ?", (SUB_EMAIL + email,)).fetchone())


def tim_theo_email(email: str) -> NguoiDung | None:
    """Tài khoản có cùng email (so sánh sau chuẩn hoá: không phân biệt hoa thường, Gmail bỏ dấu chấm/+...).
    Có cả tài khoản email + mật khẩu lẫn Google (CSDL cũ) → ưu tiên tài khoản email + mật khẩu."""
    norm = email_chuan(email)
    if not norm:
        return None
    with _ket_noi() as con:
        row = con.execute("SELECT * FROM users WHERE email_norm = ? ORDER BY google_sub NOT LIKE ?, id LIMIT 1",
                          (norm, SUB_EMAIL + "%")).fetchone()
    return _nguoi_dung(row)


def xoa_nguoi_dung(uid: int) -> None:
    with _ket_noi() as con:
        con.execute("DELETE FROM users WHERE id = ?", (uid,))


def dat_xac_thuc_email(uid: int) -> None:
    with _ket_noi() as con:
        con.execute("UPDATE users SET email_verified = 1 WHERE id = ?", (uid,))


def dat_khoa(uid: int, khoa: bool) -> None:
    with _ket_noi() as con:
        con.execute("UPDATE users SET locked = ?, locked_at = ? WHERE id = ?", (int(khoa), _bay_gio() if khoa else None, uid))


def doi_ten(uid: int, ten_cu: str, ten_moi: str, ip: str = "") -> None:
    with _ket_noi() as con:
        con.execute("UPDATE users SET name = ? WHERE id = ?", (ten_moi, uid))
        con.execute("INSERT INTO name_changes (user_id, old_name, new_name, ts, ip) VALUES (?, ?, ?, ?, ?)",
                    (uid, ten_cu, ten_moi, time.time(), ip))


def lan_doi_ten(uid: int, tu: float) -> list[float]:
    """Thời điểm các lần đổi tên từ `tu` (epoch) đến nay, cũ trước."""
    with _ket_noi() as con:
        return [r[0] for r in con.execute("SELECT ts FROM name_changes WHERE user_id = ? AND ts >= ? ORDER BY ts", (uid, tu))]


def lich_su_doi_ten(uid: int, gioi_han: int = 50) -> list[dict]:
    with _ket_noi() as con:
        rows = con.execute("SELECT old_name, new_name, ts, ip FROM name_changes WHERE user_id = ? ORDER BY ts DESC LIMIT ?",
                           (uid, gioi_han)).fetchall()
    return [{"ten_cu": r["old_name"], "ten_moi": r["new_name"], "ts": r["ts"], "ip": r["ip"]} for r in rows]


def cai_dat(khoa: str, mac_dinh: str = "") -> str:
    with _ket_noi() as con:
        row = con.execute("SELECT value FROM app_settings WHERE key = ?", (khoa,)).fetchone()
    return row["value"] if row else mac_dinh


def dat_cai_dat(khoa: str, gia_tri: str) -> None:
    with _ket_noi() as con:
        con.execute("INSERT INTO app_settings (key, value) VALUES (?, ?) ON CONFLICT(key) DO UPDATE SET value = excluded.value",
                    (khoa, gia_tri))


def ghi_hoat_dong_cuoi(uid: int, ip: str, ua: str) -> None:
    with _ket_noi() as con:
        con.execute("UPDATE users SET last_seen = ?, last_ip = ?, last_ua = ? WHERE id = ?", (time.time(), ip, ua[:300], uid))


def dat_da_xem_nhat_ky(uid: int, log_id: int) -> None:
    with _ket_noi() as con:
        con.execute("UPDATE users SET activity_seen_id = MAX(activity_seen_id, ?) WHERE id = ?", (log_id, uid))


def lay_theo_sub(google_sub: str) -> NguoiDung | None:
    with _ket_noi() as con:
        return _nguoi_dung(con.execute("SELECT * FROM users WHERE google_sub = ?", (google_sub,)).fetchone())


def dat_admin(uid: int, bat: bool) -> None:
    with _ket_noi() as con:
        con.execute("UPDATE users SET is_admin = ? WHERE id = ?", (int(bat), uid))


def dat_ki_cuu(uid: int, bat: bool) -> None:
    with _ket_noi() as con:
        con.execute("UPDATE users SET is_veteran = ? WHERE id = ?", (int(bat), uid))


def dat_mat_khau(uid: int, mat_khau_bam: str) -> None:
    with _ket_noi() as con:
        con.execute("UPDATE users SET password_hash = ? WHERE id = ?", (mat_khau_bam, uid))


def danh_sach_nguoi_dung() -> list[NguoiDung]:
    with _ket_noi() as con:
        return [_nguoi_dung(r) for r in con.execute("SELECT * FROM users ORDER BY id")]


def ghi_dang_nhap(uid: int) -> None:
    with _ket_noi() as con:
        con.execute("UPDATE users SET last_login_at = ? WHERE id = ?", (_bay_gio(), uid))


def lay_nguoi_dung(uid: int) -> NguoiDung | None:
    with _ket_noi() as con:
        return _nguoi_dung(con.execute("SELECT * FROM users WHERE id = ?", (uid,)).fetchone())


def nguoi_dung_cuc_bo() -> NguoiDung:
    with _ket_noi() as con:
        row = con.execute("SELECT * FROM users WHERE google_sub = ?", (SUB_CUC_BO,)).fetchone()
    return _nguoi_dung(row) or luu_nguoi_dung(SUB_CUC_BO, "", "Chủ app (máy này)")


def dat_key_claude(uid: int, key_ma_hoa: str | None, key_cuoi: str = "") -> None:
    """API key Claude riêng (đã mã hoá) của người dùng; None = xoá."""
    with _ket_noi() as con:
        con.execute("UPDATE users SET claude_key_enc = ?, claude_key_last4 = ?, claude_key_checked = ? WHERE id = ?",
                    (key_ma_hoa, key_cuoi if key_ma_hoa else None, time.time() if key_ma_hoa else None, uid))


def lay_key_claude(uid: int) -> tuple[str | None, str, float | None]:
    """→ (bản mã, 4 ký tự cuối, lúc kiểm tra)."""
    with _ket_noi() as con:
        r = con.execute("SELECT claude_key_enc, claude_key_last4, claude_key_checked FROM users WHERE id = ?", (uid,)).fetchone()
    return (r[0], r[1] or "", r[2]) if r else (None, "", None)


def dat_key(uid: int, key_ma_hoa: str | None, key_cuoi: str = "") -> None:
    """Lưu key đã mã hoá (hoặc xoá khi key_ma_hoa=None)."""
    with _ket_noi() as con:
        con.execute("UPDATE users SET gemini_key_enc = ?, gemini_key_last4 = ? WHERE id = ?",
                    (key_ma_hoa, key_cuoi if key_ma_hoa else None, uid))


def dung_luot_thu(uid: int, gioi_han: int) -> bool:
    """Trừ một lượt dùng thử nếu còn (một câu lệnh UPDATE có điều kiện → không vượt giới hạn khi gửi nhiều bài cùng lúc)."""
    with _ket_noi() as con:
        cur = con.execute("UPDATE users SET trial_used = trial_used + 1 WHERE id = ? AND trial_used < ?", (uid, gioi_han))
        return cur.rowcount == 1


def hoan_luot_thu(uid: int) -> None:
    """Trả lại lượt dùng thử khi bài lỗi vì phía app (AI của chủ app quá tải/hết hạn mức...), không phải lỗi của người dùng."""
    with _ket_noi() as con:
        con.execute("UPDATE users SET trial_used = MAX(trial_used - 1, 0) WHERE id = ?", (uid,))


# ---------------------------------------------------------------- solutions
def tao_bai_giai(uid: int, job_id: str, de_bai: str, hsg: bool, demo: bool, nguon_key: str) -> None:
    with _ket_noi() as con:
        con.execute(
            "INSERT INTO solutions (user_id, job_id, problem, hsg, demo, key_source, created_at) VALUES (?, ?, ?, ?, ?, ?, ?)",
            (uid, job_id, de_bai[:4000], int(hsg), int(demo), nguon_key, _bay_gio()),
        )


def ket_thuc_bai_giai(job_id: str, ket_qua: dict | None, loi: str | None, muc_do: str = "", claude: bool = False) -> None:
    with _ket_noi() as con:
        con.execute(
            "UPDATE solutions SET status = ?, model = ?, error = ?, result_json = ?, finished_at = ?, "
            "level = CASE WHEN ? <> '' THEN ? ELSE level END, claude = ?, smart_level = ?, solve_seconds = ?, cached = ?, "
            "lit_papers = ?, lit_tokens = ? WHERE job_id = ?",
            ("loi" if loi else "xong", (ket_qua or {}).get("mo_hinh", ""), loi or "",
             json.dumps(ket_qua, ensure_ascii=False) if ket_qua else None, _bay_gio(), muc_do, muc_do, int(claude),
             (ket_qua or {}).get("muc_thong_minh", ""), (ket_qua or {}).get("thoi_gian_giai"), int(bool((ket_qua or {}).get("tu_cache"))),
             ((ket_qua or {}).get("tra_cuu_tl") or {}).get("so_bai", -1), ((ket_qua or {}).get("tra_cuu_tl") or {}).get("token", 0), job_id),
        )


def chu_bai_giai(job_id: str) -> int | None:
    with _ket_noi() as con:
        row = con.execute("SELECT user_id FROM solutions WHERE job_id = ?", (job_id,)).fetchone()
    return row["user_id"] if row else None


def cap_nhat_ket_qua(job_id: str, ket_qua: dict) -> None:
    """Ghi lại kết quả đã có thêm phần sinh sau (vd phương trình chi tiết) – không đổi trạng thái / thời gian."""
    with _ket_noi() as con:
        con.execute("UPDATE solutions SET result_json = ? WHERE job_id = ?", (json.dumps(ket_qua, ensure_ascii=False), job_id))


def ket_qua_da_luu(job_id: str, uid: int) -> dict | None:
    """Kết quả đã lưu của bài giải – chỉ khi bài thuộc về người dùng uid."""
    with _ket_noi() as con:
        row = con.execute("SELECT status, error, result_json FROM solutions WHERE job_id = ? AND user_id = ?",
                          (job_id, uid)).fetchone()
    if row is None:
        return None
    return {"trang_thai": row["status"], "loi": row["error"],
            "ket_qua": json.loads(row["result_json"]) if row["result_json"] else None}


def lich_su(uid: int, gioi_han: int = 50) -> list[dict]:
    """Các bài đã giải của đúng người dùng uid (mới nhất trước)."""
    with _ket_noi() as con:
        rows = con.execute(
            "SELECT job_id, problem, hsg, demo, key_source, status, model, error, created_at, finished_at "
            "FROM solutions WHERE user_id = ? ORDER BY id DESC LIMIT ?", (uid, gioi_han),
        ).fetchall()
    return [{"job_id": r["job_id"], "de_bai": r["problem"], "hsg": bool(r["hsg"]), "demo": bool(r["demo"]),
             "nguon_key": r["key_source"], "trang_thai": r["status"], "mo_hinh": r["model"], "loi": r["error"],
             "tao_luc": r["created_at"], "xong_luc": r["finished_at"]} for r in rows]


ket_noi = _ket_noi  # cho nhat_ky.py / xac_thuc_email.py (cùng CSDL, cùng cách mở kết nối ngắn)


def tao_ma_dang_nhap_app(user_id: int, ttl_giay: int = 120) -> str:
    """Tạo mã đăng nhập một lần (sống ttl_giay) cho app desktop."""
    import secrets
    code = f"chem_{secrets.token_urlsafe(32)}"
    now = time.time()
    exp = now + ttl_giay
    with _ket_noi() as con:
        con.execute(
            "INSERT INTO app_login_codes (code, user_id, created_at, expires_at, used) VALUES (?, ?, ?, ?, 0)",
            (code, user_id, now, exp)
        )
    return code


def doi_ma_dang_nhap_app(code: str) -> tuple[bool, str, int | None]:
    """Đổi mã đăng nhập lấy user_id. Trả về (thành_công, lý_do, user_id)."""
    if not code or not isinstance(code, str):
        return False, "Mã không hợp lệ", None
    now = time.time()
    code_clean = code.strip()
    with _ket_noi() as con:
        # Dọn dẹp mã cũ quá 1 ngày
        con.execute("DELETE FROM app_login_codes WHERE expires_at < ?", (now - 86400,))
        row = con.execute(
            "SELECT user_id, expires_at, used FROM app_login_codes WHERE code = ?",
            (code_clean,)
        ).fetchone()
        if not row:
            return False, "Mã đăng nhập không tồn tại hoặc không hợp lệ", None
        uid, exp, used = row["user_id"], row["expires_at"], row["used"]
        if used == 1:
            return False, "Mã đăng nhập đã được sử dụng", None
        if now > exp:
            return False, "Mã đăng nhập đã hết hạn", None
        con.execute("UPDATE app_login_codes SET used = 1 WHERE code = ?", (code_clean,))
        return True, "Thành công", uid

