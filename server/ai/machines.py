"""Quy trình vận hành (SOP) các máy phân tích, phụ trợ (data/may/*.json) để AI trợ lý và robot hướng dẫn đúng tài liệu của phòng lab.

Câu hỏi của học sinh nhắc tới máy nào (theo tên, mã, từ khóa) thì tóm tắt nguyên lý, quy trình, lỗi hay gặp, an toàn của máy đó
được đưa vào ngữ cảnh gửi mô hình. Không có máy nào khớp thì không thêm gì.
"""

from __future__ import annotations

import json
import re
import unicodedata
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
MAY_DIR = ROOT / "data" / "may"

# từ khóa (không dấu, chữ thường) → mã máy
ALIASES: dict[str, list[str]] = {
    "uv_vis": ["uv-vis", "uv vis", "uvvis", "quang pho hap thu phan tu", "quang pho kha kien", "tu ngoai kha kien", "lambert", "beer"],
    "aas": ["aas", "hap thu nguyen tu", "ngon lua axetilen"],
    "ftir": ["ftir", "hong ngoai", "atr", "giao thoa"],
    "gc_ms": ["gc-ms", "gc ms", "gcms", "sac ky khi", "khoi pho"],
    "hplc": ["hplc", "sac ky long"],
    "chuan_do_dien_the": ["chuan do dien the", "chuan do tu dong", "dien cuc ph", "titrator"],
    "xrd": ["xrd", "nhieu xa tia x", "nhieu xa"],
    "sem": ["sem", "hien vi dien tu", "eds"],
    "nmr": ["nmr", "cong huong tu"],
    "nhiet_luong": ["bom nhiet luong", "nhiet luong", "nhiet dot chay"],
    "tu_say": ["tu say", "do am", "chat kho"],
    "lo_nung": ["lo nung", "tro tong so", "nung voi"],
    "may_cat_nuoc": ["may cat nuoc", "nuoc cat", "do dan dien"],
    "noi_hap": ["noi hap", "tiet trung", "autoclave"],
}


def strip(s: str) -> str:
    """Bỏ dấu, chữ thường, mọi ký tự không phải chữ / số thành dấu cách."""
    s = unicodedata.normalize("NFD", s.lower()).replace("đ", "d")
    s = "".join(c for c in s if unicodedata.category(c) != "Mn")
    return re.sub(r"[^a-z0-9]+", " ", s).strip()


def load() -> dict[str, dict]:
    docs = {}
    for f in sorted(MAY_DIR.glob("*.json")):
        try:
            d = json.loads(f.read_text(encoding="utf-8"))
        except (OSError, ValueError):
            continue
        if "quy_trinh" in d:
            docs[d["ma"]] = d
    return docs


MACHINES = load()


def find(text: str, limit: int = 2) -> list[dict]:
    """Các máy được nhắc tới trong câu (khớp từ khóa dài trước để 'nhiet luong' không lẫn với từ ngắn)."""
    t = " " + strip(text or "") + " "
    hits: list[tuple[int, str]] = []
    for ma, words in ALIASES.items():
        if ma not in MACHINES:
            continue
        keys = [strip(k) for k in words] + [strip(MACHINES[ma]["ten"]), ma.replace("_", " ")]
        best = max((len(k) for k in keys if k and f" {k} " in t), default=0)
        if best:
            hits.append((best, ma))
    hits.sort(reverse=True)
    return [MACHINES[ma] for _, ma in hits[:limit]]


def summary(d: dict) -> str:
    """Tóm tắt gọn một máy cho ngữ cảnh mô hình (khoảng 1500 ký tự)."""
    lines = [f"MÁY: {d['ten']} (thông số theo {d['may_mau'].split('(')[0].strip()})"]
    lines.append("Nguyên lý: " + " ".join(d["nguyen_ly"][:3]))
    steps = []
    for i, s in enumerate(d["quy_trinh"], 1):
        loi = f" (lỗi hay gặp: {s['loi']})" if s.get("loi") else ""
        steps.append(f"{i}. {s['ten']}: {s['viec']}{loi}")
    lines.append("Quy trình:\n" + "\n".join(steps))
    lines.append("An toàn: " + " ".join(d["an_toan"]))
    return "\n".join(lines)


def context(*texts: str) -> str:
    docs = find(" ".join(t for t in texts if t))
    return "\n\n".join(summary(d) for d in docs)


def listing() -> list[dict]:
    return [{"ma": d["ma"], "ten": d["ten"], "may_mau": d["may_mau"], "so_buoc": len(d["quy_trinh"])} for d in MACHINES.values()]
