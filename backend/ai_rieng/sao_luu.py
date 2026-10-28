"""Sao lưu / phục hồi toàn bộ kho tri thức AI Riêng thành MỘT file JSON (chỉ chữ – không kèm vector, không kèm file gốc:
nhập vào thì việc nền tự tính lại embedding). Gồm: cài đặt, tài liệu (+ các đoạn đã tách), quy tắc, bài mẫu, sửa sai, bộ đề."""
from __future__ import annotations

import json
import time

from .. import db
from . import kho

DINH_DANG = "chemistry-ai-rieng"
PHIEN_BAN = 1


def xuat() -> dict:
    with db.ket_noi() as con:
        tai_lieu = []
        for t in con.execute("SELECT * FROM ar_tai_lieu WHERE trang_thai = 'da_nap' ORDER BY id"):
            doan = [{"thu_tu": d["thu_tu"], "trang": d["trang"], "noi_dung": d["noi_dung"]}
                    for d in con.execute("SELECT thu_tu, trang, noi_dung FROM ar_doan WHERE tai_lieu_id = ? ORDER BY thu_tu", (t["id"],))]
            tai_lieu.append({k: t[k] for k in ("ten", "loai", "nguon", "chu_de", "co_loi_giai", "bat")} | {"doan": doan})
        kien_thuc = [{k: r[k] for k in ("tieu_de", "noi_dung", "chu_de", "uu_tien", "bat")}
                     for r in con.execute("SELECT * FROM ar_kien_thuc ORDER BY id")]
        bai_mau = [{k: r[k] for k in ("de", "chu_de", "trang_thai", "nguon", "trang", "rdkit_ok", "mo_hinh", "bat")} | {"loi_giai": json.loads(r["loi_giai"])}
                   for r in con.execute("SELECT * FROM ar_bai_mau ORDER BY id")]
        sua_sai = [{k: r[k] for k in ("de", "sai", "dung", "ly_do", "chu_de", "bat")} for r in con.execute("SELECT * FROM ar_sua_sai ORDER BY id")]
        bo_de = [{k: r[k] for k in ("de", "chu_de", "bat")} | {"dap_an": json.loads(r["dap_an"])}
                 for r in con.execute("SELECT * FROM ar_bo_de ORDER BY id")]
    return {"dinh_dang": DINH_DANG, "phien_ban": PHIEN_BAN, "xuat_luc": time.strftime("%Y-%m-%d %H:%M:%S"), "cai_dat": kho.cai_dat(),
            "tai_lieu": tai_lieu, "kien_thuc": kien_thuc, "bai_mau": bai_mau, "sua_sai": sua_sai, "bo_de": bo_de}


def nhap(du_lieu: dict, thay_the: bool = False, nhap_cai_dat: bool = False) -> dict:
    """Gộp (mặc định – bỏ qua mục trùng) hoặc THAY TOÀN BỘ kho. Trả số mục đã nhập theo loại."""
    if not isinstance(du_lieu, dict) or du_lieu.get("dinh_dang") != DINH_DANG:
        raise ValueError("File không phải bản sao lưu AI Riêng.")
    if int(du_lieu.get("phien_ban", 0)) > PHIEN_BAN:
        raise ValueError("Bản sao lưu được tạo bởi phiên bản app mới hơn.")
    if thay_the:
        with db.ket_noi() as con:
            for bang in ("ar_doan", "ar_tai_lieu", "ar_kien_thuc", "ar_bai_mau", "ar_sua_sai", "ar_bo_de"):
                con.execute(f"DELETE FROM {bang}")
            con.execute("DELETE FROM ar_fts")
    dem = {"tai_lieu": 0, "kien_thuc": 0, "bai_mau": 0, "sua_sai": 0, "bo_de": 0, "bo_qua": 0}
    with db.ket_noi() as con:
        da_co_tl = {(r[0], r[1]) for r in con.execute("SELECT ten, nguon FROM ar_tai_lieu")}
        da_co_kt = {(r[0], r[1]) for r in con.execute("SELECT tieu_de, noi_dung FROM ar_kien_thuc")}
        da_co_bm = {r[0] for r in con.execute("SELECT de_khoa FROM ar_bai_mau WHERE trang_thai != 'sai'")}
        da_co_ss = {(r[0], r[1]) for r in con.execute("SELECT de_khoa, dung FROM ar_sua_sai")}
        da_co_bd = {r[0] for r in con.execute("SELECT de FROM ar_bo_de")}
    for t in du_lieu.get("tai_lieu", []):
        if (t.get("ten"), t.get("nguon", "")) in da_co_tl or not t.get("doan"):
            dem["bo_qua"] += 1
            continue
        tid = kho.tao_tai_lieu(t["ten"], t.get("loai", "van_ban"), t.get("nguon", ""), "", t.get("chu_de", ""), bool(t.get("co_loi_giai")))
        n = kho.luu_doan(tid, [(d.get("trang"), d["noi_dung"]) for d in t["doan"] if d.get("noi_dung")])
        kho.cap_nhat_tai_lieu(tid, trang_thai="da_nap", so_doan=n, bat=int(t.get("bat", 1)), xong_luc=time.time(), tien_do=f"{n} đoạn (nhập)")
        dem["tai_lieu"] += 1
    for k in du_lieu.get("kien_thuc", []):
        if (k.get("tieu_de"), k.get("noi_dung")) in da_co_kt:
            dem["bo_qua"] += 1
            continue
        kho.tao_kien_thuc(k["tieu_de"], k["noi_dung"], k.get("chu_de", ""), int(k.get("uu_tien", 3)), bool(k.get("bat", 1)))
        dem["kien_thuc"] += 1
    # sửa sai TRƯỚC bài mẫu: tạo sửa sai sẽ đánh dấu sai các bài mẫu cùng đề – bài mẫu nhập sau giữ đúng trạng thái đã xuất
    for s in du_lieu.get("sua_sai", []):
        if (kho.khoa_de(s.get("de", "")), s.get("dung")) in da_co_ss:
            dem["bo_qua"] += 1
            continue
        sid = kho.tao_sua_sai(s["de"], s["dung"], s.get("ly_do", ""), s.get("sai", ""), s.get("chu_de", ""))
        if not s.get("bat", 1):
            kho.sua_sua_sai(sid, s["de"], s["dung"], s.get("ly_do", ""), s.get("sai", ""), s.get("chu_de", ""), bat=False)
        dem["sua_sai"] += 1
    for b in du_lieu.get("bai_mau", []):
        if b.get("trang_thai") != "sai" and kho.khoa_de(b.get("de", "")) in da_co_bm:
            dem["bo_qua"] += 1
            continue
        kho.tao_bai_mau(b["de"], b["loi_giai"], b.get("chu_de", ""), b.get("trang_thai", "duyet"), b.get("nguon", "nhap"), None, b.get("trang"),
                        bool(b.get("rdkit_ok")), b.get("mo_hinh", ""), bool(b.get("bat", 1)))
        dem["bai_mau"] += 1
    for c in du_lieu.get("bo_de", []):
        if c.get("de") in da_co_bd:
            dem["bo_qua"] += 1
            continue
        kho.tao_cau_hoi(c["de"], c["dap_an"], c.get("chu_de", ""), bool(c.get("bat", 1)))
        dem["bo_de"] += 1
    if nhap_cai_dat and isinstance(du_lieu.get("cai_dat"), dict):
        kho.dat_cai_dat(du_lieu["cai_dat"])
    kho.tang_phien_ban()
    return dem
