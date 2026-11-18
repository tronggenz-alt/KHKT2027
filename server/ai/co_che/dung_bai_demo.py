"""Dựng toàn bộ bài demo cơ chế: kiểm tra RDKit → .3d.json + .giay.json.

    python -m server.ai.co_che.dung_bai_demo [ma ...] [--khong-web]

Đọc   <VERA_DU_LIEU_RIENG>/co_che/bai_demo/<ma>/{bai.json, co_che.json}
Ghi   build/co_che/<ma>/{<ma>.bai.json, <ma>.3d.json, <ma>.giay.json} (+ .3d.js, .giay.js cho trang xem thử web)
      build/co_che/muc_luc.json (danh sách bài, kết quả kiểm tra)
Thư mục build bị git bỏ qua; bước đóng gói (.verapack) đọc từ đây.
Bài nào không qua co_che.kiem_tra (hoặc nhánh sai không áp được mũi tên) → dừng với lỗi, không ghi bài đó.
"""
from __future__ import annotations

import json
import sys
import time
from pathlib import Path

from . import co_che as cc_mod
from . import dung_kich_ban_3d as d3
from .duong_dan import thu_muc_bai_demo, thu_muc_build
from .hoc_ve import ve_theo_mau


class LoiBaiDemo(RuntimeError):
    pass


def kiem_tra_bai(co_che: dict) -> list[str]:
    """co_che.kiem_tra + nhánh sai: mũi tên của nhánh phải áp được (về giấy là hợp lệ, chỉ bị chặn về không gian)."""
    loi = cc_mod.kiem_tra(cc_mod.tu_json(co_che, "mau"))
    for ns in co_che.get("nhanh_sai", []):
        k = int(ns.get("tu_buoc", 1))
        try:
            mol = cc_mod.tu_smiles(co_che["buoc"][k - 1]["chat"] + list(ns.get("them", [])))
            cc_mod.ap_mui_ten(mol, [cc_mod.MuiTen.tu_dict(x) for x in ns["mui_ten"]])
        except (cc_mod.LoiCoChe, KeyError, IndexError) as e:
            loi.append(f"Nhánh sai '{ns.get('ten')}': {e}")
    return loi


def ds_bai(goc: Path | None = None) -> list[str]:
    goc = goc or thu_muc_bai_demo()
    return sorted(p.parent.name for p in goc.glob("*/co_che.json"))


def dung_mot(ma: str, ra: Path, web: bool = True) -> dict:
    thu_muc = thu_muc_bai_demo() / ma
    co_che = json.loads((thu_muc / "co_che.json").read_text(encoding="utf-8"))
    bai = json.loads((thu_muc / "bai.json").read_text(encoding="utf-8")) if (thu_muc / "bai.json").exists() else {"ma": ma}
    loi = kiem_tra_bai(co_che)
    if loi:
        raise LoiBaiDemo(f"{ma}: không qua kiểm tra RDKit:\n- " + "\n- ".join(loi))
    t0 = time.time()
    kb = d3.dung(co_che)
    t3d = time.time() - t0
    sd = ve_theo_mau.tao_so_do(co_che)
    tgiay = time.time() - t0 - t3d
    o = ra / ma
    o.mkdir(parents=True, exist_ok=True)
    (o / f"{ma}.bai.json").write_text(json.dumps(bai, ensure_ascii=False, indent=1), encoding="utf-8")
    d3.ghi(kb, o / f"{ma}.3d.json", ban_web=web)
    (o / f"{ma}.giay.json").write_text(json.dumps(sd, ensure_ascii=False), encoding="utf-8")
    if web:
        (o / f"{ma}.giay.js").write_text("window.SO_DO_TU_DONG = " + json.dumps(sd, ensure_ascii=False) + ";\n", encoding="utf-8")
    return {"ma": ma, "ten": co_che.get("ten", ma), "loai": co_che.get("loai", ""), "lop": bai.get("lop"), "de": bai.get("de", ""),
            "so_buoc": len(co_che["buoc"]), "so_trang_thai": len(kb["trang_thai"]), "nhanh_sai": len(kb.get("nhanh_sai", [])),
            "kiem_tra": "đạt", "giay_3d": round(t3d, 2), "giay_ve": round(tgiay, 2)}


def main(argv: list[str]) -> int:
    web = "--khong-web" not in argv
    chon = [a for a in argv if not a.startswith("--")]
    ma_ds = chon or ds_bai()
    if not ma_ds:
        print(f"Không thấy bài demo trong {thu_muc_bai_demo()} (đặt VERA_DU_LIEU_RIENG).")
        return 1
    ra = thu_muc_build()
    muc_luc, hong = [], []
    for ma in ma_ds:
        try:
            r = dung_mot(ma, ra, web)
        except LoiBaiDemo as e:
            hong.append(str(e))
            print("✗", e)
            continue
        muc_luc.append(r)
        print(f"✓ {ma:<20} {r['so_buoc']} bước, {r['so_trang_thai']} trạng thái 3D, nhánh sai {r['nhanh_sai']}  "
              f"(3D {r['giay_3d']} s, giấy {r['giay_ve']} s)")
    if not chon:
        (ra / "muc_luc.json").write_text(json.dumps({"bai": muc_luc}, ensure_ascii=False, indent=1), encoding="utf-8")
    print(f"→ {ra}  ({len(muc_luc)} bài đạt, {len(hong)} bài lỗi)")
    return 1 if hong else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
