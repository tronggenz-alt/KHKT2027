"""Kho kiến thức RAG cho cơ chế: 8 bài demo + quy tắc cơ chế đã kiểm chứng (đều ở thư mục dữ liệu riêng).

    python -m server.ai.co_che.tri_thuc        dựng chỉ mục (BM25 + vector bge-m3 qua Ollama nếu có) → <riêng>/co_che/rag_co_che.db

Dùng lại server/ai/rag.py (Doan, Kho, luu, nap, nhung). Chỉ mục nằm ở thư mục riêng, không vào git. Chưa dựng chỉ mục thì
máy chủ tự cắt đoạn lúc chạy và chỉ tìm bằng từ khóa (BM25).
"""
from __future__ import annotations

import json
import sys
import time
from pathlib import Path

from server.ai import rag

from .duong_dan import thu_muc_bai_demo, thu_muc_co_che_rieng


def duong_dan_chi_muc() -> Path:
    return thu_muc_co_che_rieng() / "rag_co_che.db"


def _doan_bai(thu_muc: Path) -> list[rag.Doan]:
    ma = thu_muc.name
    try:
        bai = json.loads((thu_muc / "bai.json").read_text(encoding="utf-8"))
        cc = json.loads((thu_muc / "co_che.json").read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return []
    nguon = f"co_che/bai_demo/{ma}"
    ten = cc.get("ten", ma)
    lop = int(bai.get("lop") or 0)
    ra = []
    tong = [f"Đề: {bai.get('de', '')}", f"Loại cơ chế: {cc.get('loai', '')}. Điều kiện: {bai.get('dieu_kien', cc.get('dieu_kien', ''))}.",
            f"Hướng phản ứng: {cc.get('giai_thich', '')}",
            "Mục tiêu: " + "; ".join(bai.get("muc_tieu", []))]
    ra.append(rag.Doan(nguon, f"{ten} › Tổng quan", "\n".join(tong), "hoa", lop, "co_che"))
    buoc = []
    for i, b in enumerate(bai.get("cac_buoc", []), 1):
        buoc.append(f"{b.get('ten', f'Bước {i}')}: {b.get('giai_thich', '')}")
    for ns in bai.get("nhanh_sai", []):
        buoc.append(f"Nhánh sai – {ns.get('ten', '')}: {ns.get('ly_do', '')}")
    ra.append(rag.Doan(nguon, f"{ten} › Các bước", "\n".join(buoc), "hoa", lop, "co_che"))
    hoi = []
    for q in bai.get("cau_hoi", []):
        dung = q["lua_chon"][q["dap_an"]] if 0 <= q.get("dap_an", -1) < len(q.get("lua_chon", [])) else ""
        hoi.append(f"Hỏi: {q['hoi']} – Đáp: {dung}. {q.get('giai_thich', '')}")
    hoi.append("Lỗi hay gặp: " + "; ".join(bai.get("loi_hay_gap", [])))
    ra.append(rag.Doan(nguon, f"{ten} › Câu hỏi và lỗi hay gặp", "\n".join(hoi), "hoa", lop, "co_che"))
    return ra


def cat_tat_ca() -> list[rag.Doan]:
    doan: list[rag.Doan] = []
    goc = thu_muc_bai_demo()
    if goc.is_dir():
        for t in sorted(p for p in goc.iterdir() if p.is_dir()):
            doan += _doan_bai(t)
    tt = thu_muc_co_che_rieng() / "tri_thuc"
    if tt.is_dir():
        for p in sorted(tt.rglob("*.md")):
            doan += rag.cat_markdown(p, thu_muc_co_che_rieng())
    return doan


_kho: rag.Kho | None = None


def kho() -> rag.Kho:
    global _kho
    if _kho is None:
        _kho = rag.nap(duong_dan_chi_muc()) or rag.Kho(cat_tat_ca())
    return _kho


def dat_kho(k: rag.Kho | None) -> None:
    global _kho
    _kho = k


async def tim(cau: str, k: int = 4) -> list[rag.Doan]:
    kh = kho()
    if not kh.doan or not (cau or "").strip():
        return []
    qvec = await rag.nhung_cau_hoi(cau) if kh.co_vector else None
    return kh.tim(cau, k, qvec)


def dung_chi_muc() -> int:
    doan = cat_tat_ca()
    p = duong_dan_chi_muc()
    cu = rag.vector_cu(p, rag.embed_model())
    can = [d for d in doan if d.bam not in cu]
    for d in doan:
        d.vec = cu.get(d.bam)
    mo_hinh = rag.embed_model()
    try:
        for i in range(0, len(can), 16):
            vs = rag.nhung([d.van_ban() for d in can[i:i + 16]])
            for d, v in zip(can[i:i + 16], vs):
                d.vec = v
    except Exception as e:  # noqa: BLE001 – không có Ollama: lưu kho chỉ có BM25
        print(f"Không nhúng được vector ({e}); lưu kho chỉ dùng từ khóa.")
        for d in doan:
            d.vec = None
        mo_hinh = ""
    rag.luu(doan, p, mo_hinh)
    print(f"{len(doan)} đoạn, vector: {bool(mo_hinh)} → {p}")
    return len(doan)


if __name__ == "__main__":
    t0 = time.time()
    dung_chi_muc()
    print(f"{time.time() - t0:.1f} s")
    sys.exit(0)
