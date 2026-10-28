"""Chép dữ liệu máy phân tích (data/may/**.json) sang project Unity (Assets/Lab/Data/May/), giữ nguyên cấu trúc thư mục.

    python tools/chep_du_lieu_may.py
"""

from __future__ import annotations

import shutil
from pathlib import Path

GOC = Path(__file__).resolve().parents[1]
NGUON = GOC / "data" / "may"
DICH = GOC / "Phong thi nghiem ao" / "Assets" / "Lab" / "Data" / "May"


def chep() -> list[Path]:
    da_chep = []
    for f in sorted(NGUON.rglob("*.json")):
        d = DICH / f.relative_to(NGUON)
        d.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(f, d)
        da_chep.append(d)
    return da_chep


if __name__ == "__main__":
    for p in chep():
        print("→", p.relative_to(GOC))
