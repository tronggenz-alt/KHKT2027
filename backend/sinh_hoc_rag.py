"""Hệ thống RAG cho AI Bio-Tutor của Phòng Lab Sinh học 3D.

Quản lý kho tài liệu SGK chuẩn GDPT 2018 trong thư mục `knowledge/`:
- Chia nhỏ tài liệu thành các đoạn ~300-500 từ theo đề mục / ngữ cảnh.
- Lưu trữ và lập chỉ mục trong SQLite (`data/app.db`).
- Truy xuất 4 đoạn liên quan nhất theo câu hỏi học sinh và chèn vào thẻ <tai_lieu>.
- Hỗ trợ thêm, sửa, xóa, và cập nhật chỉ mục từ trang quản trị (Admin).
"""
from __future__ import annotations

import glob
import math
import os
import re
import sqlite3
import time
from pathlib import Path
from typing import Any

from .db import _ket_noi

THU_MUC_KNOWLEDGE = Path(__file__).resolve().parent.parent / "knowledge"


def khoi_tao_bang_rag(conn: sqlite3.Connection) -> None:
    """Tạo bảng lưu trữ các đoạn tài liệu RAG sinh học nếu chưa có."""
    conn.execute("""
        CREATE TABLE IF NOT EXISTS sinh_hoc_knowledge_chunks (
            id TEXT PRIMARY KEY,
            topic TEXT NOT NULL,
            title TEXT NOT NULL,
            chunk_text TEXT NOT NULL,
            words_count INTEGER NOT NULL,
            updated_at REAL NOT NULL
        )
    """)
    conn.execute("""
        CREATE TABLE IF NOT EXISTS sinh_hoc_phan_hoi (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            ts REAL NOT NULL,
            user_id INTEGER,
            cau_hoi TEXT NOT NULL,
            cau_tra_loi TEXT NOT NULL,
            hai_long INTEGER NOT NULL,
            ly_do TEXT,
            chi_tiet TEXT
        )
    """)


def _bo_dau_tieng_viet(s: str) -> str:
    """Loại bỏ dấu tiếng Việt để so sánh tìm kiếm mờ."""
    bang_chuyen = {
        'à': 'a', 'á': 'a', 'ả': 'a', 'ã': 'a', 'ạ': 'a',
        'ă': 'a', 'ằ': 'a', 'ắ': 'a', 'ẳ': 'a', 'ẵ': 'a', 'ặ': 'a',
        'â': 'a', 'ầ': 'a', 'ấ': 'a', 'ẩ': 'a', 'ẫ': 'a', 'ậ': 'a',
        'đ': 'd',
        'è': 'e', 'é': 'e', 'ẻ': 'e', 'ẽ': 'e', 'ẹ': 'e',
        'ê': 'e', 'ề': 'e', 'ế': 'e', 'ể': 'e', 'ễ': 'e', 'ệ': 'e',
        'ì': 'i', 'í': 'i', 'ỉ': 'i', 'ĩ': 'i', 'ị': 'i',
        'ò': 'o', 'ó': 'o', 'ỏ': 'o', 'õ': 'o', 'ọ': 'o',
        'ô': 'o', 'ồ': 'o', 'ố': 'o', 'ổ': 'o', 'ỗ': 'o', 'ộ': 'o',
        'ơ': 'o', 'ờ': 'o', 'ớ': 'o', 'ở': 'o', 'ỡ': 'o', 'ợ': 'o',
        'ù': 'u', 'ú': 'u', 'ủ': 'u', 'ũ': 'u', 'ụ': 'u',
        'ư': 'u', 'ừ': 'u', 'ứ': 'u', 'ử': 'u', 'ữ': 'u', 'ự': 'u',
        'ỳ': 'y', 'ý': 'y', 'ỷ': 'y', 'ỹ': 'y', 'ỵ': 'y',
    }
    s = s.lower()
    res = []
    for c in s:
        res.append(bang_chuyen.get(c, c))
    return "".join(res)


def _tach_tu_khoa(text: str) -> list[str]:
    """Tách các từ và cụm từ khóa có nghĩa."""
    clean = re.sub(r"[^\w\s\d]", " ", text.lower())
    tu = clean.split()
    dung = {"va", "hay", "la", "cua", "cac", "nhung", "co", "o", "trong", "cho", "voi", "nhu", "thi", "de", "do", "duoc", "mot", "nhieu"}
    return [t for t in tu if len(t) >= 2 and t not in dung]


def _chia_nho_markdown(noi_dung: str, topic: str) -> list[dict[str, Any]]:
    """Chia một file markdown thành các đoạn ngữ cảnh ~300-500 từ."""
    cac_doan: list[dict[str, Any]] = []
    muc_lon = re.split(r"\n(?=##?\s+)", noi_dung)
    
    tieu_de_file = ""
    if muc_lon and muc_lon[0].startswith("# "):
        tieu_de_file = muc_lon[0].split("\n")[0].replace("#", "").strip()
        muc_lon = muc_lon[1:]

    idx = 0
    for muc in muc_lon:
        muc = muc.strip()
        if not muc or "CẦN GIÁO VIÊN DUYỆT" in muc and len(muc) < 50:
            continue
        
        lines = muc.split("\n")
        tieu_de_muc = lines[0].replace("#", "").strip() if lines else "Tổng quan"
        noi_dung_muc = "\n".join(lines[1:]).strip() if len(lines) > 1 else muc
        
        words = muc.split()
        if len(words) <= 550:
            idx += 1
            cac_doan.append({
                "id": f"{topic}_{idx}",
                "topic": topic,
                "title": f"{tieu_de_file} - {tieu_de_muc}" if tieu_de_file else tieu_de_muc,
                "chunk_text": muc,
                "words_count": len(words),
            })
        else:
            # Đoạn dài hơn 550 từ: chia theo các tiểu mục hoặc đoạn văn
            tieu_muc = re.split(r"\n(?=###?\s+|\d+\.\s+)", muc)
            sub_chunk = ""
            for p in tieu_muc:
                p = p.strip()
                if not p:
                    continue
                if len((sub_chunk + "\n\n" + p).split()) > 500 and sub_chunk:
                    idx += 1
                    cac_doan.append({
                        "id": f"{topic}_{idx}",
                        "topic": topic,
                        "title": f"{tieu_de_file} - {tieu_de_muc}" if tieu_de_file else tieu_de_muc,
                        "chunk_text": sub_chunk.strip(),
                        "words_count": len(sub_chunk.split()),
                    })
                    sub_chunk = p
                else:
                    sub_chunk = (sub_chunk + "\n\n" + p).strip() if sub_chunk else p
            
            if sub_chunk:
                idx += 1
                cac_doan.append({
                    "id": f"{topic}_{idx}",
                    "topic": topic,
                    "title": f"{tieu_de_file} - {tieu_de_muc}" if tieu_de_file else tieu_de_muc,
                    "chunk_text": sub_chunk.strip(),
                    "words_count": len(sub_chunk.split()),
                })
    return cac_doan


def cap_nhat_chi_muc() -> dict[str, Any]:
    """Quét toàn bộ thư mục `knowledge/`, chia nhỏ và nạp vào SQLite."""
    os.makedirs(THU_MUC_KNOWLEDGE, exist_ok=True)
    files = glob.glob(str(THU_MUC_KNOWLEDGE / "*.md"))
    
    tong_doan = 0
    now = time.time()
    
    with _ket_noi() as conn:
        khoi_tao_bang_rag(conn)
        conn.execute("DELETE FROM sinh_hoc_knowledge_chunks")
        
        for fpath in files:
            fname = os.path.basename(fpath)
            topic = os.path.splitext(fname)[0]
            try:
                with open(fpath, "r", encoding="utf-8") as f:
                    noi_dung = f.read()
                doans = _chia_nho_markdown(noi_dung, topic)
                for d in doans:
                    conn.execute("""
                        INSERT OR REPLACE INTO sinh_hoc_knowledge_chunks (id, topic, title, chunk_text, words_count, updated_at)
                        VALUES (?, ?, ?, ?, ?, ?)
                    """, (d["id"], d["topic"], d["title"], d["chunk_text"], d["words_count"], now))
                    tong_doan += 1
            except Exception as e:
                print(f"Lỗi đọc file knowledge {fname}: {e}")
                
    return {
        "so_file": len(files),
        "tong_doan": tong_doan,
        "thoi_gian": now,
        "danh_sach_file": [os.path.basename(p) for p in files],
    }


def _tep_moi_nhat() -> float:
    """Thời điểm sửa gần nhất của các file knowledge/*.md (0 nếu chưa có file)."""
    return max((p.stat().st_mtime for p in THU_MUC_KNOWLEDGE.glob("*.md")), default=0.0)


def lay_doan_tai_lieu(cau_hoi: str, toi_da: int = 4) -> list[dict[str, Any]]:
    """Tìm kiếm BM25 / từ khóa các đoạn tài liệu phù hợp nhất cho câu hỏi."""
    with _ket_noi() as conn:
        khoi_tao_bang_rag(conn)
        
        # Chưa có dữ liệu, hoặc file knowledge/*.md mới hơn lần index cuối (sửa tay / bản web mới) → index lại
        cur = conn.execute("SELECT COUNT(*), COALESCE(MAX(updated_at), 0) FROM sinh_hoc_knowledge_chunks")
        count, lan_index = cur.fetchone()
    if count == 0 or _tep_moi_nhat() > lan_index:
        cap_nhat_chi_muc()
        
    with _ket_noi() as conn:
        cur = conn.execute("SELECT id, topic, title, chunk_text FROM sinh_hoc_knowledge_chunks")
        tat_ca = cur.fetchall()
        
    if not tat_ca:
        return []
        
    tu_khoi_dau = _tach_tu_khoa(cau_hoi)
    tu_khong_dau = _tach_tu_khoa(_bo_dau_tieng_viet(cau_hoi))
    
    # Tính điểm liên quan cho từng đoạn
    ket_qua: list[tuple[float, dict[str, Any]]] = []
    for cid, topic, title, text in tat_ca:
        text_lower = text.lower()
        text_khong_dau = _bo_dau_tieng_viet(text_lower)
        title_lower = title.lower()
        title_khong_dau = _bo_dau_tieng_viet(title_lower)
        
        diem = 0.0
        
        # Điểm khớp chính xác câu hỏi
        if cau_hoi.strip().lower() in text_lower:
            diem += 20.0
            
        for t in tu_khoi_dau:
            if t in title_lower:
                diem += 5.0
            if t in text_lower:
                diem += 1.5 + math.log(1 + text_lower.count(t))
                
        for t in tu_khong_dau:
            if t in title_khong_dau:
                diem += 3.0
            if t in text_khong_dau:
                diem += 1.0
                
        # Ưu tiên theo chủ đề khớp
        topic_ten = topic.replace("-", " ")
        if topic_ten in cau_hoi.lower() or topic in cau_hoi.lower():
            diem += 6.0
            
        if diem > 0:
            ket_qua.append((diem, {
                "id": cid,
                "topic": topic,
                "title": title,
                "chunk_text": text
            }))
            
    ket_qua.sort(key=lambda x: x[0], reverse=True)
    return [item[1] for item in ket_qua[:toi_da]]


def dinh_dang_tai_lieu_prompt(doans: list[dict[str, Any]]) -> str:
    """Format danh sách các đoạn tài liệu RAG vào thẻ <tai_lieu>."""
    if not doans:
        return ""
    cac_phan = []
    for i, d in enumerate(doans, 1):
        cac_phan.append(f"--- ĐOẠN {i} [{d.get('title', d.get('topic', ''))}] ---\n{d.get('chunk_text', '').strip()}")
    noi_dung = "\n\n".join(cac_phan)
    return (
        f"<tai_lieu>\n"
        f"KHO TÀI LIỆU CHUẨN SGK GDPT 2018 (CHỈ TRẢ LỜI DỰA TRÊN TÀI LIỆU NÀY, KHÔNG ĐOÁN HAY TỰ BỊA SỐ LIỆU):\n"
        f"{noi_dung}\n"
        f"</tai_lieu>"
    )


# =====================================================================================================================
# CÁC HÀM QUẢN TRỊ FILE KNOWLEDGE (DÙNG CHO TRANG ADMIN)
# =====================================================================================================================
def danh_sach_file_knowledge() -> list[dict[str, Any]]:
    """Lấy danh sách các file markdown trong knowledge/ kèm thống kê số từ và thời gian sửa."""
    os.makedirs(THU_MUC_KNOWLEDGE, exist_ok=True)
    ket_qua = []
    for fpath in sorted(glob.glob(str(THU_MUC_KNOWLEDGE / "*.md"))):
        fname = os.path.basename(fpath)
        stat = os.stat(fpath)
        try:
            with open(fpath, "r", encoding="utf-8") as f:
                content = f.read()
            so_tu = len(content.split())
            can_duyet = "CẦN GIÁO VIÊN DUYỆT" in content
        except Exception:
            so_tu = 0
            can_duyet = False
        ket_qua.append({
            "name": fname,
            "topic": os.path.splitext(fname)[0],
            "size": stat.st_size,
            "words": so_tu,
            "can_duyet": can_duyet,
            "mtime": stat.st_mtime,
        })
    return ket_qua


def doc_file_knowledge(ten: str) -> str:
    """Đọc nội dung một file markdown trong knowledge/."""
    ten = os.path.basename(ten)
    if not ten.endswith(".md"):
        ten += ".md"
    fpath = THU_MUC_KNOWLEDGE / ten
    if not fpath.exists():
        raise FileNotFoundError(f"Không tìm thấy tài liệu {ten}")
    with open(fpath, "r", encoding="utf-8") as f:
        return f.read()


def ghi_file_knowledge(ten: str, noi_dung: str) -> dict[str, Any]:
    """Ghi hoặc cập nhật file markdown trong knowledge/."""
    ten = os.path.basename(ten)
    if not ten.endswith(".md"):
        ten += ".md"
    fpath = THU_MUC_KNOWLEDGE / ten
    with open(fpath, "w", encoding="utf-8") as f:
        f.write(noi_dung)
    # Tự động đồng bộ lại chỉ mục
    return cap_nhat_chi_muc()


def xoa_file_knowledge(ten: str) -> dict[str, Any]:
    """Xóa file markdown trong knowledge/ và reindex."""
    ten = os.path.basename(ten)
    if not ten.endswith(".md"):
        ten += ".md"
    fpath = THU_MUC_KNOWLEDGE / ten
    if fpath.exists():
        os.remove(fpath)
    return cap_nhat_chi_muc()
