"""Dựng, kiểm tra và thử kho RAG Hóa học – Sinh học (data/rag.db).

    .venv/bin/python tools/xay_kho_rag.py                 dựng kho: cắt đoạn + nhúng vector bge-m3 (Ollama)
    .venv/bin/python tools/xay_kho_rag.py --khong-vector  dựng kho chỉ có chỉ mục từ khóa
    .venv/bin/python tools/xay_kho_rag.py --kiem-tra      chấm khả năng tìm đúng tài liệu (data/kien_thuc/_kiem_tra/*.json)
    .venv/bin/python tools/xay_kho_rag.py --kiem-tai-lieu kiểm tra quy cách các file .md
    .venv/bin/python tools/xay_kho_rag.py --hoi "chất khử là gì"   xem 5 đoạn tìm được cho một câu hỏi

Cần Ollama đang chạy và đã `ollama pull bge-m3` (đổi bằng RAG_EMBED_URL, RAG_EMBED_MODEL).
Dựng lại chỉ nhúng các đoạn mới hoặc đã sửa; đoạn không đổi lấy lại vector cũ.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
import time
from collections import Counter, defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))

from server.ai import rag  # noqa: E402

KHOA_BAT_BUOC = ("mon", "lop", "loai", "chu_de", "tu_khoa", "nguon")


def dung(khong_vector: bool, lo: int) -> int:
    t0 = time.time()
    doan = rag.cat_tat_ca()
    print(f"Cắt được {len(doan)} đoạn từ {len({d.nguon for d in doan})} nguồn ({time.time() - t0:.1f} s).")
    mo_hinh = ""
    if not khong_vector:
        mo_hinh = rag.embed_model()
        try:
            rag.nhung(["thử"], timeout=30)
        except Exception as e:
            print(f"Không gọi được {rag.embed_url()} ({type(e).__name__}): dựng kho không có vector.\n"
                  f"  Cài: brew install ollama && brew services start ollama && ollama pull {mo_hinh}")
            mo_hinh = ""
    if mo_hinh:
        cu = rag.vector_cu(rag.db_path(), mo_hinh)
        can = [d for d in doan if d.bam not in cu]
        for d in doan:
            d.vec = cu.get(d.bam)
        print(f"Dùng lại {len(doan) - len(can)} vector, cần nhúng {len(can)} đoạn bằng {mo_hinh}…")
        t1 = time.time()
        for i in range(0, len(can), lo):
            nhom = can[i:i + lo]
            for d, v in zip(nhom, rag.nhung([d.van_ban() for d in nhom])):
                d.vec = v
            xong = i + len(nhom)
            if xong % (lo * 10) == 0 or xong == len(can):
                print(f"  {xong}/{len(can)} ({time.time() - t1:.0f} s)")
    rag.luu(doan, rag.db_path(), mo_hinh)
    dem = Counter(d.nguon.split("/")[1] if d.nguon.startswith("kien_thuc/") else d.nguon.split("/")[0] for d in doan)
    print(f"Đã ghi {rag.db_path()} ({rag.db_path().stat().st_size / 1e6:.1f} MB): " + ", ".join(f"{k} {v}" for k, v in sorted(dem.items())))
    return 0


def kiem_tai_lieu() -> int:
    loi = 0
    tong_tu, so_tep = 0, 0
    for p in sorted(rag.KIEN_THUC.rglob("*.md")):
        if p.name.lower() == "readme.md":
            continue
        so_tep += 1
        text = p.read_text(encoding="utf-8")
        meta, body = rag.doc_front_matter(text)
        ten = p.relative_to(rag.KIEN_THUC).as_posix()
        thieu = [k for k in KHOA_BAT_BUOC if k not in meta]
        if thieu:
            print(f"{ten}: thiếu khóa {', '.join(thieu)}")
            loi += 1
        sections = re.split(r"^## ", body, flags=re.M)[1:]
        if not sections:
            print(f"{ten}: không có mục ##")
            loi += 1
        ngan_toi_thieu = 60 if meta.get("loai") == "tra_cuu" else 120
        for s in sections:
            head, _, content = s.partition("\n")
            n = len(content.split())
            tong_tu += n
            if n < ngan_toi_thieu or n > 550:
                print(f"{ten}: mục «{head.strip()[:60]}» có {n} từ")
        if not any(s.lower().startswith("hỏi đáp nhanh") for s in sections) and meta.get("loai") in ("ly_thuyet", "chuyen_de"):
            print(f"{ten}: thiếu mục Hỏi đáp nhanh")
    for p in sorted((rag.KIEN_THUC / "_kiem_tra").glob("*.json")):
        try:
            items = json.loads(p.read_text(encoding="utf-8"))
        except ValueError as e:
            print(f"{p.name}: JSON lỗi {e}")
            loi += 1
            continue
        for it in items:
            if not (rag.KIEN_THUC / it.get("tep", "")).exists():
                print(f"{p.name}: tep không tồn tại: {it.get('tep')}")
                loi += 1
    print(f"{so_tep} file, {tong_tu:,} từ trong các mục; {loi} lỗi nặng.")
    return 1 if loi else 0


def kiem_tra(chi_tiet: bool) -> int:
    kho = rag.nap(rag.db_path()) or rag.Kho(rag.cat_tat_ca())
    cau = []
    for p in sorted((rag.KIEN_THUC / "_kiem_tra").glob("*.json")):
        for it in json.loads(p.read_text(encoding="utf-8")):
            cau.append((p.stem, it))
    if not cau:
        print("Chưa có câu kiểm tra trong data/kien_thuc/_kiem_tra/.")
        return 1
    qvecs = [None] * len(cau)
    if kho.co_vector:
        try:
            qvecs = []
            for i in range(0, len(cau), 32):
                qvecs += rag.nhung([it["cau_hoi"] for _, it in cau[i:i + 32]])
        except Exception as e:
            print(f"Không nhúng được câu hỏi ({type(e).__name__}): chỉ chấm từ khóa.")
            qvecs = [None] * len(cau)
    che_do = {"tu_khoa": lambda q, v: [kho.doan[i] for i, _ in kho.bm25(q, 5)]}
    if kho.co_vector and qvecs[0] is not None:
        che_do["vector"] = lambda q, v: [kho.doan[i] for i, _ in kho.vector(v, 5)]
        che_do["lai"] = lambda q, v: kho.tim(q, 5, v)
    print(f"Kho: {len(kho.doan)} đoạn, vector: {'có (' + kho.mo_hinh_vec + ')' if kho.co_vector else 'không'}; {len(cau)} câu hỏi.\n")
    print(f"{'chế độ':<10}{'đúng@1':>9}{'đúng@5':>9}{'MRR':>8}")
    ket_qua = {}
    for ten, fn in che_do.items():
        h1 = h5 = mrr = 0.0
        theo_bo = defaultdict(lambda: [0, 0])
        truot = []
        for (bo, it), v in zip(cau, qvecs):
            dich = "kien_thuc/" + it["tep"]
            got = [d.nguon for d in fn(it["cau_hoi"], v)]
            rank = got.index(dich) + 1 if dich in got else 0
            h1 += rank == 1
            h5 += rank > 0
            mrr += 1 / rank if rank else 0
            theo_bo[bo][0] += rank > 0
            theo_bo[bo][1] += 1
            if not rank:
                truot.append((it["cau_hoi"], it["tep"], got[:2]))
        n = len(cau)
        print(f"{ten:<10}{h1 / n:>9.0%}{h5 / n:>9.0%}{mrr / n:>8.2f}")
        ket_qua[ten] = (theo_bo, truot)
    tot_nhat = "lai" if "lai" in ket_qua else "tu_khoa"
    theo_bo, truot = ket_qua[tot_nhat]
    print(f"\nTheo bộ câu hỏi (chế độ {tot_nhat}, đúng@5):")
    for bo, (dung_, tong) in sorted(theo_bo.items()):
        print(f"  {bo:<22}{dung_}/{tong}")
    if truot:
        print(f"\n{len(truot)} câu không tìm thấy đúng file" + ("" if chi_tiet else " (thêm --chi-tiet để xem hết)") + ":")
        for q, tep, got in truot[: None if chi_tiet else 12]:
            print(f"  - {q}\n      cần {tep}; được {', '.join(got) or '(không có)'}")
    return 0


def hoi(cau: str, k: int) -> int:
    kho = rag.nap(rag.db_path()) or rag.Kho(rag.cat_tat_ca())
    v = None
    if kho.co_vector:
        try:
            v = rag.nhung([cau])[0]
        except Exception:
            print("(không nhúng được câu hỏi: chỉ tìm từ khóa)")
    for i, d in enumerate(kho.tim(cau, k, v), 1):
        print(f"[{i}] {d.tieu_de}\n    ({d.nguon})\n    {d.noi_dung[:300].replace(chr(10), ' ')}…\n")
    return 0


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--khong-vector", action="store_true", help="không nhúng vector")
    ap.add_argument("--lo", type=int, default=32, help="số đoạn mỗi lần gửi Ollama")
    ap.add_argument("--kiem-tra", action="store_true", help="chấm khả năng tìm đúng file")
    ap.add_argument("--chi-tiet", action="store_true", help="in mọi câu bị trượt")
    ap.add_argument("--kiem-tai-lieu", action="store_true", help="kiểm tra quy cách file .md")
    ap.add_argument("--hoi", help="thử một câu hỏi")
    ap.add_argument("-k", type=int, default=5)
    a = ap.parse_args()
    if a.kiem_tai_lieu:
        return kiem_tai_lieu()
    if a.kiem_tra:
        return kiem_tra(a.chi_tiet)
    if a.hoi:
        return hoi(a.hoi, a.k)
    return dung(a.khong_vector, a.lo)


if __name__ == "__main__":
    sys.exit(main())
