"""Học và chấm theo SƠ ĐỒ (chuỗi chất trong cùng một hình ChemDraw) – công cụ ADMIN chạy dòng lệnh.

    python -m server.ai.co_che.hoc_ve.so_do "<thư mục đề>" [--so-so-do 150]   → $VERA_DU_LIEU_RIENG/co_che/kho_mau/bao_cao_so_do.json (+ in tóm tắt)

Trong đáp án, cùng một chất được vẽ xoay hay lật tuỳ NGỮ CẢNH. Ba quy tắc ngữ cảnh học từ ~75% file:
  giu_huong          : phần khung chung (MCS) của chất sau đặt cùng hướng với chất trước;
  phan_doi_ve_mui_ten: ở chất trước, phần sắp thay đổi (ngoài khung chung) nằm phía mũi tên (bên phải);
  phan_moi_tu_mui_ten: ở chất sau, phần mới sinh ra nằm phía mũi tên đi tới (bên trái).
Vẽ cả sơ đồ: mỗi chất chọn trong các phương án của tu_duy_ve theo điểm phong cách + ba quy tắc trên (trọng số theo tỉ lệ
học được). Chấm trên ~25% file chưa thấy: so từng chất với đáp án, so với cách vẽ từng chất riêng lẻ.
"""
from __future__ import annotations

import hashlib
import json
import random
import statistics
import sys
import time
import unicodedata
from collections import defaultdict
from pathlib import Path

import numpy as np
from rdkit import Chem, RDLogger
from rdkit.Chem import rdFMCS

from ..duong_dan import thu_muc_kho_mau
from . import bo_do
from . import doc_cdx
from . import hoc_mau
from . import tu_duy_ve as td

RDLogger.DisableLog("rdApp.*")


def doc_so_do(goc: Path) -> list[dict]:
    """Mỗi hình ChemDraw có ≥ 2 chất hữu cơ → chuỗi chất theo thứ tự đọc (hàng trên xuống, trái sang phải)."""
    da, ds = set(), []
    for p in sorted([*goc.glob("**/*.docx"), *goc.glob("**/*.doc")]):
        ten = unicodedata.normalize("NFC", str(p))
        if "Bản sao" in ten or "/~$" in ten:
            continue
        for _, cdx in doc_cdx.chemdraw_trong_file(p):
            h = hashlib.md5(cdx).hexdigest()
            if h in da:
                continue
            da.add(h)
            cay = doc_cdx.doc_cay(cdx)
            if cay is None:
                continue
            chat = []
            for m in doc_cdx.cac_manh(cay):
                r = hoc_mau.thanh_mol(m)
                if r is None:
                    continue
                mol, xy, _ = r
                L = td.lien_ket(mol)
                if not len(L) or mol.GetNumAtoms() > 40:
                    continue
                dai = np.median(np.linalg.norm(xy[L[:, 0]] - xy[L[:, 1]], axis=1))
                if dai < 1e-6:
                    continue
                c = xy.mean(0)
                chat.append({"mol": mol, "P": td.chuan_hoa(xy, L), "cx": c[0] / dai, "cy": c[1] / dai})
            if len(chat) >= 2:
                chat.sort(key=lambda c: (round(-c["cy"] / 5), c["cx"]))
                ds.append({"tep": ten, "chat": chat})
    return ds


def khung_chung(a, b):
    """Cặp chỉ số nguyên tử của phần khung chung lớn nhất (≥ 4 nguyên tử) giữa hai chất; None nếu không có."""
    try:
        r = rdFMCS.FindMCS([a, b], timeout=1, atomCompare=rdFMCS.AtomCompare.CompareElements,
                           bondCompare=rdFMCS.BondCompare.CompareAny, ringMatchesRingOnly=True)
    except Exception:  # noqa: BLE001
        return None
    if r.canceled and r.numAtoms < 4 or r.numAtoms < 4:
        return None
    q = Chem.MolFromSmarts(r.smartsString)
    if q is None:
        return None
    ia, ib = a.GetSubstructMatch(q), b.GetSubstructMatch(q)
    if not ia or not ib:
        return None
    return list(zip(ia, ib))


def lech_khung(Pa, Pb, cap) -> float:
    A = np.array([Pa[i] for i, _ in cap]); B = np.array([Pb[j] for _, j in cap])
    return float(np.sqrt((((A - A.mean(0)) - (B - B.mean(0))) ** 2).sum(1).mean()))


def phia_phan_khac(P, n, chi_so_chung) -> float:
    """dx từ tâm khung chung tới tâm phần ngoài khung chung (> 0: phần khác nằm bên phải)."""
    chung = set(chi_so_chung)
    ngoai = [i for i in range(n) if i not in chung]
    if not ngoai or not chung:
        return 0.0
    return float(P[ngoai, 0].mean() - P[list(chung), 0].mean())


def hoc_quy_tac(so_do: list[dict]) -> dict:
    giu, phai, trai = [], [], []
    for sd in so_do:
        for a, b in zip(sd["chat"], sd["chat"][1:]):
            cap = khung_chung(a["mol"], b["mol"])
            if not cap:
                continue
            giu.append(lech_khung(a["P"], b["P"], cap) < .5)
            if b["cx"] - a["cx"] > 2:                               # xếp ngang: a bên trái mũi tên, b bên phải
                da = phia_phan_khac(a["P"], a["mol"].GetNumAtoms(), [i for i, _ in cap])
                db = phia_phan_khac(b["P"], b["mol"].GetNumAtoms(), [j for _, j in cap])
                if abs(da) > .3:
                    phai.append(da > 0)
                if abs(db) > .3:
                    trai.append(db < 0)
    f = lambda x: round(float(np.mean(x)), 3) if x else .5
    return {"giu_huong": f(giu), "phan_doi_ve_mui_ten": f(phai), "phan_moi_tu_mui_ten": f(trai),
            "so_cap": {"giu_huong": len(giu), "phan_doi": len(phai), "phan_moi": len(trai)}}


def ve_so_do(chat: list, tu_duy: td.TuDuy, qt: dict, so_huong_dau: int = 8) -> list[np.ndarray]:
    """Vẽ cả chuỗi. Mỗi chất chọn phương án theo điểm phong cách + quy tắc ngữ cảnh (trọng số và CHIỀU theo tỉ lệ học
    được). Chọn cả chuỗi cùng lúc: thử nhiều hướng cho chất đầu, mỗi hướng dựng cả chuỗi theo sau, lấy chuỗi tổng điểm thấp nhất
    (tránh chất đầu đặt sai kéo cả chuỗi sai theo)."""
    w_giu = 3 * max(0.0, qt["giu_huong"] - .3)          # vừa đủ để tỉ lệ giữ hướng gần mức đáp án (~65%), không ép quá
    p_doi, p_moi = qt["phan_doi_ve_mui_ten"], qt["phan_moi_tu_mui_ten"]
    w_doi, chieu_doi = 3 * abs(p_doi - .5), (1 if p_doi >= .5 else -1)      # +1: phần sắp đổi nên nằm bên phải
    w_moi, chieu_moi = 3 * abs(p_moi - .5), (-1 if p_moi >= .5 else 1)      # +1: phần mới nên nằm bên phải
    caps = [None] + [khung_chung(a["mol"], b["mol"]) for a, b in zip(chat, chat[1:])]
    pa = [tu_duy.cac_phuong_an(Chem.Mol(c["mol"])) for c in chat]

    def diem(k, x, truoc):
        P, d = x
        n = chat[k]["mol"].GetNumAtoms()
        if caps[k] is not None and truoc is not None:
            d += w_giu * lech_khung(truoc, P, caps[k])
            db = phia_phan_khac(P, n, [j for _, j in caps[k]])
            if abs(db) > .3 and db * chieu_moi < 0:
                d += w_moi
        if k + 1 < len(chat) and caps[k + 1] is not None:
            da = phia_phan_khac(P, n, [i for i, _ in caps[k + 1]])
            if abs(da) > .3 and da * chieu_doi < 0:
                d += w_doi
        return d

    if not pa[0]:
        return [np.zeros((c["mol"].GetNumAtoms(), 2)) for c in chat]
    dau = sorted(pa[0], key=lambda x: diem(0, x, None))[:so_huong_dau]
    tot, tot_d = None, 1e18
    for x0 in dau:
        chuoi, tong = [x0[0]], diem(0, x0, None)
        for k in range(1, len(chat)):
            if not pa[k]:
                chuoi.append(np.zeros((chat[k]["mol"].GetNumAtoms(), 2)))
                continue
            x = min(pa[k], key=lambda y: diem(k, y, chuoi[-1]))
            chuoi.append(x[0]); tong += diem(k, x, chuoi[-2])
        if tong < tot_d:
            tot, tot_d = chuoi, tong
    return tot


def main() -> None:
    goc = Path(sys.argv[1])
    so = int(sys.argv[sys.argv.index("--so-so-do") + 1]) if "--so-so-do" in sys.argv else 150
    t0 = time.time()
    tat_ca = doc_so_do(goc)
    cham = lambda t: int(hashlib.md5(t.encode()).hexdigest(), 16) % 4 == 0
    hoc = [s for s in tat_ca if not cham(s["tep"])]
    thu = [s for s in tat_ca if cham(s["tep"])]

    # kho mẫu + nguyên tắc chung học từ các chất trong sơ đồ phần "học" (giống bo_do)
    xh = {"khung": defaultdict(list), "khung_vo": defaultdict(list)}
    qm = {"khung": {}, "khung_vo": {}}
    phan_tu_hoc = [c for s in hoc for c in s["chat"]] + [r for r in bo_do.doc_tap(goc) if not cham(r["tep"])]
    for c in phan_tu_hoc:
        m, P = c["mol"], c["P"]
        for hv in hoc_mau.he_vong(m):
            if 3 <= len(hv) <= 24:
                tam = P[sorted(hv)].mean(0)
                for muc, vo in (("khung", False), ("khung_vo", True)):
                    k, tt, q = hoc_mau.khoa_va_thu_tu(m, hv, vo)
                    xh[muc][k].append((P[tt] - tam, ""))
                    qm[muc].setdefault(k, q)
    kho = {k: hoc_mau.chon_cach_ve(k, d, qm["khung"][k]) for k, d in xh["khung"].items() if len(d) >= 3}
    kho_vo = {k: hoc_mau.chon_cach_ve(k, d, qm["khung_vo"][k]) for k, d in xh["khung_vo"].items() if len(d) >= 2}
    tu_duy = td.TuDuy(kho, kho_vo, td.hoc_luat([(c["mol"], c["P"]) for c in phan_tu_hoc]))
    random.seed(11)
    qt = hoc_quy_tac(random.sample(hoc, min(400, len(hoc))))

    mau = random.sample(thu, min(so, len(thu)))
    kq = {c: {"so": 0, "giong_huong": 0, "giong_dang": 0, "loi": 0, "giu_huong_cap": [], } for c in ("tung_chat", "theo_so_do")}
    for s in mau:
        chat = s["chat"]
        ban = {"tung_chat": [tu_duy.ve(Chem.Mol(c["mol"])) for c in chat], "theo_so_do": ve_so_do(chat, tu_duy, qt)}
        caps = [khung_chung(a["mol"], b["mol"]) for a, b in zip(chat, chat[1:])]
        for ten, ds in ban.items():
            o = kq[ten]
            for c, P in zip(chat, ds):
                m = c["mol"]
                dx = m.GetSubstructMatches(m, uniquify=False, maxMatches=120, useChirality=False) or [tuple(range(m.GetNumAtoms()))]
                L = td.lien_ket(m)
                o["so"] += 1
                o["giong_huong"] += bo_do.lech(m, P, c["P"], dx, False) < .5
                o["giong_dang"] += bo_do.lech(m, P, c["P"], dx, True) < .5
                o["loi"] += td.so_net_cat(P, L) + td.so_cap_sat(P, L)
            for (Pa, Pb, cap) in zip(ds, ds[1:], caps):
                if cap:
                    o["giu_huong_cap"].append(lech_khung(Pa, Pb, cap) < .5)

    bao_cao = {
        "thoi_diem": time.strftime("%Y-%m-%d %H:%M"), "giay": round(time.time() - t0),
        "so_so_do": {"hoc": len(hoc), "thu": len(thu), "da_cham": len(mau)},
        "quy_tac_ngu_canh": qt,
        "ket_qua": {t: {"so_chat": o["so"], "giong_huong_%": round(100 * o["giong_huong"] / max(o["so"], 1), 1),
                        "giong_dang_%": round(100 * o["giong_dang"] / max(o["so"], 1), 1),
                        "loi_de_nhin_tb": round(o["loi"] / max(o["so"], 1), 2),
                        "cap_giu_huong_%": round(100 * float(np.mean(o["giu_huong_cap"])), 1) if o["giu_huong_cap"] else None}
                    for t, o in kq.items()},
    }
    ra = thu_muc_kho_mau() / "bao_cao_so_do.json"
    ra.write_text(json.dumps(bao_cao, ensure_ascii=False, indent=1))
    print(f"{len(tat_ca)} sơ đồ ({len(hoc)} học / {len(thu)} thử), chấm {len(mau)} sơ đồ, {bao_cao['giay']} s")
    print("quy tắc ngữ cảnh học được:", json.dumps(qt, ensure_ascii=False))
    print(f"{'cách vẽ':<14}{'số chất':>8}{'giống hướng':>13}{'giống dáng':>12}{'lỗi/hình':>10}{'cặp giữ hướng':>15}")
    for t, o in bao_cao["ket_qua"].items():
        print(f"{t:<14}{o['so_chat']:>8}{o['giong_huong_%']:>12}%{o['giong_dang_%']:>11}%{o['loi_de_nhin_tb']:>10}{str(o['cap_giu_huong_%']) + '%':>15}")
    print("→", ra)


if __name__ == "__main__":
    main()
