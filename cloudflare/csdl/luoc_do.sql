-- Lược đồ D1 của verlab-db: cùng các bảng với data/lab.db của bản FastAPI (server/lab/db.py, sinh_db.py),
-- thêm ba bảng thay cho phần bản FastAPI giữ trong bộ nhớ hoặc trên đĩa: nhip, trang_thai_may, trang_bai_giang.
-- Chạy lại nhiều lần vẫn an toàn (IF NOT EXISTS).

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
CREATE TABLE IF NOT EXISTS trang_bai_giang (
    bai_id INTEGER NOT NULL, so INTEGER NOT NULL, loai TEXT NOT NULL, anh BLOB NOT NULL, PRIMARY KEY (bai_id, so)
);
CREATE TABLE IF NOT EXISTS trinh_chieu (
    lop_id INTEGER PRIMARY KEY, phien_ban INTEGER NOT NULL, loai TEXT NOT NULL, bai_id INTEGER, trang INTEGER NOT NULL DEFAULT 1,
    tieu_de TEXT DEFAULT '', noi_dung TEXT DEFAULT '', man_hinh TEXT DEFAULT '', dem_nguoc_den REAL NOT NULL DEFAULT 0, cap_nhat REAL NOT NULL
);
CREATE TABLE IF NOT EXISTS su_kien_hoc_sinh (
    id INTEGER PRIMARY KEY AUTOINCREMENT, hoc_sinh_id INTEGER NOT NULL, thoi_gian REAL NOT NULL, muc_do TEXT NOT NULL, tieu_de TEXT NOT NULL, chi_tiet TEXT DEFAULT ''
);
CREATE INDEX IF NOT EXISTS su_kien_theo_hoc_sinh ON su_kien_hoc_sinh (hoc_sinh_id, id);
CREATE TABLE IF NOT EXISTS lenh_app (
    id INTEGER PRIMARY KEY AUTOINCREMENT, lop_id INTEGER NOT NULL, hoc_sinh_id INTEGER, loai TEXT NOT NULL, noi_dung TEXT DEFAULT '', ke_hoach TEXT DEFAULT '', tao_luc REAL NOT NULL
);
CREATE INDEX IF NOT EXISTS lenh_theo_lop ON lenh_app (lop_id, id);
CREATE TABLE IF NOT EXISTS chu_y_lop (
    lop_id INTEGER PRIMARY KEY, noi_dung TEXT DEFAULT '', tu_luc REAL NOT NULL
);
CREATE TABLE IF NOT EXISTS buoi_hoc (
    lop_id INTEGER PRIMARY KEY, che_do TEXT NOT NULL, het_gio REAL NOT NULL DEFAULT 0, khoa_loai TEXT NOT NULL DEFAULT '[]',
    ngoai_le TEXT NOT NULL DEFAULT '[]', khoa_them TEXT NOT NULL DEFAULT '[]', phien_ban INTEGER NOT NULL, cap_nhat REAL NOT NULL
);
-- Nhịp app: mỗi máy một dòng, ghi đè; không có chỉ mục phụ để mỗi lần ghi chỉ tốn một dòng trong hạn mức của D1
CREATE TABLE IF NOT EXISTS nhip (
    nguoi_dung_id INTEGER PRIMARY KEY, du_lieu TEXT NOT NULL, luc REAL NOT NULL
);
CREATE TABLE IF NOT EXISTS trang_thai_may (
    ma TEXT PRIMARY KEY, du_lieu TEXT NOT NULL
);

-- Phòng sinh học tầng 2
CREATE TABLE IF NOT EXISTS sinh_dung_cu (
    ma TEXT PRIMARY KEY, so_luong INTEGER NOT NULL DEFAULT 1, hong INTEGER NOT NULL DEFAULT 0, dang_muon INTEGER NOT NULL DEFAULT 0,
    can_bao_tri INTEGER NOT NULL DEFAULT 0, vi_tri TEXT DEFAULT '', lan_hieu_chuan REAL DEFAULT 0, han_hieu_chuan REAL DEFAULT 0,
    ghi_chu TEXT DEFAULT '', cap_nhat REAL
);
CREATE TABLE IF NOT EXISTS sinh_pham (
    id TEXT PRIMARY KEY, ten TEXT NOT NULL, loai TEXT NOT NULL, tu TEXT NOT NULL, ngan TEXT DEFAULT '', so_luong REAL NOT NULL DEFAULT 0,
    don_vi TEXT NOT NULL DEFAULT '', so_lo TEXT DEFAULT '', han_dung REAL NOT NULL, cap_an_toan TEXT NOT NULL DEFAULT 'BSL-1',
    ma_chat TEXT DEFAULT '', cap_nhat REAL
);
CREATE TABLE IF NOT EXISTS nhiet_do_tu (
    id INTEGER PRIMARY KEY AUTOINCREMENT, tu TEXT NOT NULL, thoi_gian REAL NOT NULL, nhiet_do REAL NOT NULL, cua_mo INTEGER NOT NULL DEFAULT 0
);
CREATE TABLE IF NOT EXISTS dat_lich (
    id INTEGER PRIMARY KEY AUTOINCREMENT, doi_tuong TEXT NOT NULL, nguoi TEXT NOT NULL, lop TEXT NOT NULL,
    bat_dau REAL NOT NULL, ket_thuc REAL NOT NULL, muc_dich TEXT DEFAULT '', tao_luc REAL
);
CREATE TABLE IF NOT EXISTS muon_tra (
    id INTEGER PRIMARY KEY AUTOINCREMENT, ma_dung_cu TEXT NOT NULL, so_luong INTEGER NOT NULL DEFAULT 1, nguoi TEXT NOT NULL, lop TEXT NOT NULL,
    luc_muon REAL NOT NULL, han_tra REAL NOT NULL, luc_tra REAL, tinh_trang_tra TEXT DEFAULT 'chua_tra'
);
CREATE TABLE IF NOT EXISTS ket_qua_tn (
    id INTEGER PRIMARY KEY AUTOINCREMENT, hoc_sinh TEXT NOT NULL, lop TEXT NOT NULL, ban TEXT DEFAULT '', ma_bai TEXT NOT NULL,
    thoi_gian REAL NOT NULL, du_lieu TEXT NOT NULL DEFAULT '{}', diem REAL, nhan_xet TEXT DEFAULT '', hoc_sinh_id INTEGER
);
CREATE INDEX IF NOT EXISTS ket_qua_theo_hoc_sinh ON ket_qua_tn (hoc_sinh_id);
CREATE TABLE IF NOT EXISTS su_co_sinh (
    id INTEGER PRIMARY KEY AUTOINCREMENT, phong TEXT NOT NULL, loai TEXT NOT NULL, muc_do TEXT NOT NULL DEFAULT 'Info',
    mo_ta TEXT NOT NULL, xu_ly TEXT DEFAULT '', trang_thai TEXT NOT NULL DEFAULT 'moi', thoi_gian REAL NOT NULL
);
CREATE TABLE IF NOT EXISTS bao_tri (
    id INTEGER PRIMARY KEY AUTOINCREMENT, ma_dung_cu TEXT NOT NULL, loai TEXT NOT NULL, ngay REAL NOT NULL,
    nguoi TEXT NOT NULL, ket_qua TEXT NOT NULL, ghi_chu TEXT DEFAULT ''
);
