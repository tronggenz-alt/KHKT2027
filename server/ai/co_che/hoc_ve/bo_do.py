"""Bộ đo "độ giống đáp án" – công cụ ADMIN chạy dòng lệnh (không hiện cho học sinh).

    python -m server.ai.co_che.hoc_ve.bo_do "<thư mục đề>" [--so-mau 600]   → $VERA_DU_LIEU_RIENG/co_che/kho_mau/bao_cao_bo_do.json (+ in tóm tắt)

Cách đo:
  1. Đọc mọi phân tử hữu cơ trong hình ChemDraw của đáp án (đã có toạ độ người ra đề vẽ).
  2. Chia THEO FILE: ~75% file để học (kho mẫu + nguyên tắc chung), ~25% file để chấm – máy chưa từng thấy các file này.
  3. Mỗi phân tử chấm: giấu toạ độ gốc, cho máy tự vẽ, so với bản gốc (chuẩn hoá liên kết = 1, xét mọi đối xứng):
       giống hướng : lệch toạ độ (không xoay) < 0,5 → đặt trên trang giống hệt đáp án;
       giống dáng  : lệch sau khi xoay/lật tối ưu < 0,5 → cùng cách bày, chỉ khác hướng;
       lỗi dễ nhìn : nét cắt nhau + nguyên tử chen sát.
  4. Tách điểm theo: khung đã gặp (có trong kho mẫu) / khung mới (đo TƯ DUY vẽ) / mạch hở.
  So 3 cách: RDKit mặc định, chỉ kho mẫu, kho mẫu + tư duy vẽ.
"""
from __future__ import annotations

import hashlib
import json
import math
import random
import statistics
import sys
import time
import unicodedata
from collections import defaultdict
from pathlib import Path

import numpy as np
from rdkit import Chem, RDLogger

from ..duong_dan import thu_muc_kho_mau
from . import doc_cdx
from . import hoc_mau
from . import tu_duy_ve as td

RDLogger.DisableLog("rdApp.*")


def doc_tap(goc: Path) -> list[dict]:
    da, tap = set(), []
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
            for m in doc_cdx.cac_manh(cay):
                r = hoc_mau.thanh_mol(m)
                if r is None:
                    continue
                mol, xy, _ = r
                L = td.lien_ket(mol)
                if not len(L) or mol.GetNumAtoms() > 40:
                    continue
                tap.append({"tep": ten, "mol": mol, "P": td.chuan_hoa(xy, L)})
    return tap


def lech(m, A: np.ndarray, B: np.ndarray, doi_xung, xoay: bool) -> float:
    A = A - A.mean(0)
    B = B - B.mean(0)
    tot = 1e9
    for g in doi_xung:
        X = A[list(g)]
        if xoay:
            for lat in (1, -1):
                Y = X * np.array([lat, 1])
                U, _, Vt = np.linalg.svd(Y.T @ B)
                R = U @ Vt
                if np.linalg.det(R) < 0:
                    U[:, -1] *= -1
                    R = U @ Vt
                tot = min(tot, float(np.sqrt(((Y @ R - B) ** 2).sum(1).mean())))
        else:
            tot = min(tot, float(np.sqrt(((X - B) ** 2).sum(1).mean())))
    return tot


def main() -> None:
    goc = Path(sys.argv[1])
    so_mau = int(sys.argv[sys.argv.index("--so-mau") + 1]) if "--so-mau" in sys.argv else 600
    t0 = time.time()
    tap = doc_tap(goc)
    cham = lambda r: int(hashlib.md5(r["tep"].encode()).hexdigest(), 16) % 4 == 0
    hoc, thu = [r for r in tap if not cham(r)], [r for r in tap if cham(r)]

    # ---- học trên phần "học"
    xh = {"khung": defaultdict(list), "khung_vo": defaultdict(list)}
    qm = {"khung": {}, "khung_vo": {}}
    for r in hoc:
        m, P = r["mol"], r["P"]
        for hv in hoc_mau.he_vong(m):
            if 3 <= len(hv) <= 24:
                tam = P[sorted(hv)].mean(0)
                for muc, vo in (("khung", False), ("khung_vo", True)):
                    k, tt, q = hoc_mau.khoa_va_thu_tu(m, hv, vo)
                    xh[muc][k].append((P[tt] - tam, ""))
                    qm[muc].setdefault(k, q)
    kho = {k: hoc_mau.chon_cach_ve(k, ds, qm["khung"][k]) for k, ds in xh["khung"].items() if len(ds) >= 3}
    kho_vo = {k: hoc_mau.chon_cach_ve(k, ds, qm["khung_vo"][k]) for k, ds in xh["khung_vo"].items() if len(ds) >= 2}
    luat = td.hoc_luat([(r["mol"], r["P"]) for r in hoc])
    tu_duy = td.TuDuy(kho, kho_vo, luat)

    # ---- chấm trên phần "thử"
    random.seed(7)
    mau = random.sample(thu, min(so_mau, len(thu)))
    cach = {"rdkit_mac_dinh": dict(dung_mau=False, dung_tu_duy=False),
            "chi_kho_mau": dict(dung_mau=True, dung_tu_duy=False),
            "kho_mau_va_tu_duy": dict(dung_mau=True, dung_tu_duy=True)}
    kq = {c: defaultdict(lambda: {"so": 0, "giong_huong": 0, "giong_dang": 0, "lech": [], "loi": 0}) for c in cach}
    te_nhat = defaultdict(list)
    for r in mau:
        m, G = r["mol"], r["P"]
        he = [h for h in hoc_mau.he_vong(m) if 3 <= len(h) <= 24]
        if not he:
            nhom = "mach_ho"
        else:
            nhom = "khung_da_gap" if all(hoc_mau.khoa_va_thu_tu(m, h)[0] in kho for h in he) else "khung_moi"
        dx = m.GetSubstructMatches(m, uniquify=False, maxMatches=120, useChirality=False) or [tuple(range(m.GetNumAtoms()))]
        L = td.lien_ket(m)
        for c, tham_so in cach.items():
            P = tu_duy.ve(Chem.Mol(m), **tham_so)
            lh, ld = lech(m, P, G, dx, False), lech(m, P, G, dx, True)
            for nh in (nhom, "tat_ca"):
                o = kq[c][nh]
                o["so"] += 1; o["giong_huong"] += lh < .5; o["giong_dang"] += ld < .5; o["lech"].append(lh)
                o["loi"] += td.so_net_cat(P, L) + td.so_cap_sat(P, L)
            if c == "kho_mau_va_tu_duy" and he and ld >= .5:
                te_nhat[hoc_mau.khoa_va_thu_tu(m, max(he, key=len))[0]].append(ld)

    bao_cao = {
        "thoi_diem": time.strftime("%Y-%m-%d %H:%M"), "giay": round(time.time() - t0),
        "so_phan_tu": {"hoc": len(hoc), "thu": len(thu), "da_cham": len(mau)},
        "kho": {"khung": len(kho), "khung_vo": len(kho_vo)}, "nguyen_tac_hoc_duoc": luat,
        "ket_qua": {c: {nh: {"so": o["so"], "giong_huong_%": round(100 * o["giong_huong"] / o["so"], 1),
                             "giong_dang_%": round(100 * o["giong_dang"] / o["so"], 1),
                             "lech_trung_vi": round(statistics.median(o["lech"]), 2), "loi_de_nhin_tb": round(o["loi"] / o["so"], 2)}
                        for nh, o in v.items()} for c, v in kq.items()},
        "khung_ve_sai_nhieu_nhat": sorted(((k, len(v)) for k, v in te_nhat.items()), key=lambda x: -x[1])[:15],
    }
    ra = thu_muc_kho_mau() / "bao_cao_bo_do.json"
    ra.write_text(json.dumps(bao_cao, ensure_ascii=False, indent=1))
    print(f"học {len(hoc)} phân tử, chấm {len(mau)}/{len(thu)} phân tử của các file máy chưa thấy ({bao_cao['giay']} s)")
    print("nguyên tắc học được:", json.dumps(luat, ensure_ascii=False))
    print(f"{'cách vẽ':<20}{'nhóm':<14}{'số':>5}{'giống hướng':>13}{'giống dáng':>12}{'lỗi/hình':>10}")
    for c, v in bao_cao["ket_qua"].items():
        for nh in ("tat_ca", "khung_da_gap", "khung_moi", "mach_ho"):
            if nh in v:
                o = v[nh]
                print(f"{c:<20}{nh:<14}{o['so']:>5}{o['giong_huong_%']:>12}%{o['giong_dang_%']:>11}%{o['loi_de_nhin_tb']:>10}")
    print("khung vẽ sai nhiều nhất:", bao_cao["khung_ve_sai_nhieu_nhat"][:8])
    print("→", ra)


# ============================================================================ bộ chấm "dễ nhìn" của sơ đồ giấy
# Cùng luật với kiemTraDeNhin trong nguyen_mau/co_che_3d/ve_giay.js (bản web), thêm: chữ trên/dưới mũi tên phản ứng,
# độ lệch khỏi lưới góc 30°, độ đều độ dài liên kết. Không cần đáp án: chấm được mọi .giay.json (8 bài demo, chất lạ).
KY_TU = .17


def _nua_rong(t: str) -> float:
    return len(t) * .17 + .1 if t else 0.0


def _hop_nhan(sd: dict) -> dict:
    """Hộp chữ của từng nhãn nguyên tử (cách căn như ve_giay.js: nhãn nhiều ký tự gắn 1 nét → kéo ra phía ngoài)."""
    ke = defaultdict(list)
    for l in sd["lk"]:
        ke[l[0]].append(l[1]); ke[l[1]].append(l[0])
    hop = {}
    for k, v in sd["nt"].items():
        if len(v) < 3 or not v[2]:
            continue
        x, y, t = v[0], v[1], v[2]
        w = 2 * _nua_rong(t)
        can = "giua"
        if len(t) > 1 and len(ke[k]) == 1:
            can = "phai" if sd["nt"][ke[k][0]][0] > x + .3 else "trai"
        if can == "trai":
            x0, x1 = x - KY_TU, x - KY_TU + w
        elif can == "phai":
            x0, x1 = x + KY_TU - w, x + KY_TU
        else:
            x0, x1 = x - w / 2, x + w / 2
        hop[k] = (x0, x1, y - .24, y + .24)
    return hop


def _giao(p1, p2, p3, p4):
    d = (p2[0] - p1[0]) * (p4[1] - p3[1]) - (p2[1] - p1[1]) * (p4[0] - p3[0])
    if abs(d) < 1e-9:
        return None
    t = ((p3[0] - p1[0]) * (p4[1] - p3[1]) - (p3[1] - p1[1]) * (p4[0] - p3[0])) / d
    u = ((p3[0] - p1[0]) * (p2[1] - p1[1]) - (p3[1] - p1[1]) * (p2[0] - p1[0])) / d
    if 1e-6 < t < 1 - 1e-6 and 1e-6 < u < 1 - 1e-6:
        return (p1[0] + t * (p2[0] - p1[0]), p1[1] + t * (p2[1] - p1[1]))
    return None


def _trong(p, h, le: float = 0.0) -> bool:
    return h[0] - le < p[0] < h[1] + le and h[2] - le < p[1] < h[3] + le


def _bezier(m, w):
    return tuple((1 - w) ** 2 * m["tu"][i] + 2 * w * (1 - w) * m["qua"][i] + w * w * m["den"][i] for i in range(2))


def do_de_nhin(sd: dict) -> dict:
    """Đếm lỗi dễ nhìn của một sơ đồ giấy → {loai: số} + điểm 0–100 (100 = không lỗi)."""
    nt = {k: (v[0], v[1]) for k, v in sd["nt"].items()}
    lk = [(l[0], l[1], (l[2] if len(l) > 2 else 1)) for l in sd["lk"]]
    hop = _hop_nhan(sd)
    # hộp chữ khác: điều kiện trên/dưới mũi tên phản ứng, nhãn chất A1…
    hop_khac = []
    for p in sd.get("pu", []):
        xm = (p["x1"] + p["x2"]) / 2
        for t, dy in ((p.get("tren"), .45), (p.get("duoi"), -.55)):
            if t:
                w = len(t) * .13 + .1
                hop_khac.append((xm - w, xm + w, p["y"] + dy - .2, p["y"] + dy + .2))
    for x, y, t in sd.get("ten", []):
        hop_khac.append((x - len(t) * .15, x + len(t) * .15, y - .22, y + .22))
    loi = {"cat_nhau": 0, "chu_de_net": 0, "chu_de_chu": 0, "mui_ten_cat": 0, "mui_ten_de_chu": 0, "mui_ten_sat": 0, "goc_hep": 0}
    for i in range(len(lk)):
        for j in range(i + 1, len(lk)):
            a, b, ka = lk[i]; c, d, kb = lk[j]
            if len({a, b, c, d}) < 4 or ka == "sau" or kb == "sau":
                continue
            if _giao(nt[a], nt[b], nt[c], nt[d]):
                loi["cat_nhau"] += 1
    for k, h in hop.items():
        for a, b, _ in lk:
            if k in (a, b):
                continue
            if any(_trong((nt[a][0] + (nt[b][0] - nt[a][0]) * q / 20, nt[a][1] + (nt[b][1] - nt[a][1]) * q / 20), h) for q in range(1, 20)):
                loi["chu_de_net"] += 1
    ds_hop = list(hop.values()) + hop_khac
    for i in range(len(ds_hop)):
        for j in range(i + 1, len(ds_hop)):
            h, g = ds_hop[i], ds_hop[j]
            if h[0] < g[1] and g[0] < h[1] and h[2] < g[3] and g[2] < h[3]:
                loi["chu_de_chu"] += 1
    mt = sd.get("mt", [])
    for m in mt:
        tu, den = m["tu"], m["den"]
        xa = lambda p: math.dist(p, tu) > .4 and math.dist(p, den) > .4
        truoc = _bezier(m, 0)
        cat, de = set(), set()
        for q in range(1, 31):
            p = _bezier(m, q / 30)
            for li, (a, b, _) in enumerate(lk):
                g = _giao(truoc, p, nt[a], nt[b])
                if g and xa(g):
                    cat.add(li)
            for hi, h in enumerate(ds_hop):
                if _trong(p, h) and xa(p):
                    de.add(hi)
            truoc = p
        loi["mui_ten_cat"] += len(cat)
        loi["mui_ten_de_chu"] += len(de)
    for i in range(len(mt)):
        for j in range(i + 1, len(mt)):
            B = [_bezier(mt[j], q / 20) for q in range(21)]
            sat = sum(1 for q in range(3, 18) if any(math.dist(_bezier(mt[i], q / 20), r) < .18 for r in B))
            loi["mui_ten_sat"] += sat >= 4
    huong = defaultdict(list)
    for a, b, _ in lk:
        huong[a].append(math.atan2(nt[b][1] - nt[a][1], nt[b][0] - nt[a][0]))
        huong[b].append(math.atan2(nt[a][1] - nt[b][1], nt[a][0] - nt[b][0]))
    for hs in huong.values():
        for i in range(len(hs)):
            for j in range(i + 1, len(hs)):
                g = abs(math.degrees(hs[i] - hs[j])) % 360
                loi["goc_hep"] += min(g, 360 - g) < 14
    # phong cách: lệch lưới 30° (0 = đúng lưới, 1 = lệch 15°), độ dài liên kết không đều (hệ số biến thiên)
    goc = [math.degrees(math.atan2(nt[b][1] - nt[a][1], nt[b][0] - nt[a][0])) % 30 for a, b, _ in lk]
    lech = statistics.mean(min(g, 30 - g) / 15 for g in goc) if goc else 0.0
    dai = [math.dist(nt[a], nt[b]) for a, b, _ in lk]
    cv = statistics.pstdev(dai) / statistics.mean(dai) if len(dai) > 1 else 0.0
    phat = (10 * loi["cat_nhau"] + 5 * (loi["chu_de_net"] + loi["chu_de_chu"]) + 4 * (loi["mui_ten_cat"] + loi["mui_ten_de_chu"])
            + 3 * (loi["mui_ten_sat"] + loi["goc_hep"]) + 10 * lech + 20 * cv)
    return {**loi, "lech_luoi_30": round(lech, 3), "lech_do_dai": round(cv, 3), "diem": round(max(0.0, 100 - phat), 1)}


def _so_do_chat(smiles: str) -> dict:
    """Sơ đồ một chất (để chấm chất lạ): dùng ve_theo_mau.so_do_mot_chat nếu có, không thì bay_chat + nhãn (bản cũ)."""
    from . import ve_theo_mau as vt
    if hasattr(vt, "so_do_mot_chat"):
        return vt.so_do_mot_chat(smiles)
    m = vt._mol(smiles)
    P = vt.bay_chat(smiles) / 1.5
    sd = {"nt": {}, "lk": []}
    for a in m.GetAtoms():
        t = vt._nhan(a, m, P, a.GetIdx(), {})
        sd["nt"][f"a{a.GetIdx()}"] = [float(P[a.GetIdx()][0]), float(P[a.GetIdx()][1])] + ([t] if t else [])
    for b in m.GetBonds():
        bac = {Chem.BondType.DOUBLE: 2, Chem.BondType.TRIPLE: 3}.get(b.GetBondType(), 1)
        sd["lk"].append([f"a{b.GetBeginAtomIdx()}", f"a{b.GetEndAtomIdx()}"] + ([bac] if bac > 1 else []))
    return sd


def main_de_nhin(argv: list[str]) -> None:
    """python -m server.ai.co_che.hoc_ve.bo_do --de-nhin [--ten truoc|sau] : chấm 8 bài demo + 20 chất ngẫu nhiên."""
    from ..duong_dan import thu_muc_bai_demo
    from . import ve_theo_mau as vt
    nhan = argv[argv.index("--ten") + 1] if "--ten" in argv else "sau"
    kq = {"thoi_diem": time.strftime("%Y-%m-%d %H:%M"), "bai": {}, "chat": {}}
    for p in sorted(thu_muc_bai_demo().glob("*/co_che.json")):
        try:
            sd = vt.tao_so_do(json.loads(p.read_text(encoding="utf-8")))
            kq["bai"][p.parent.name] = do_de_nhin(sd)
        except Exception as e:  # noqa: BLE001 – bản cũ có thể lỗi: ghi lại, điểm 0
            kq["bai"][p.parent.name] = {"loi_ve": f"{type(e).__name__}: {e}", "diem": 0.0}
    ds = json.loads((Path(__file__).with_name("chat_do_thu.json")).read_text(encoding="utf-8"))["chat"][:20]
    for stt, ten, smi in ds:
        try:
            kq["chat"][ten] = do_de_nhin(_so_do_chat(smi))
        except Exception as e:  # noqa: BLE001
            kq["chat"][ten] = {"loi_ve": f"{type(e).__name__}: {e}", "diem": 0.0}
    tb = lambda d: round(statistics.mean(v["diem"] for v in d.values()), 1)
    kq["tong"] = {"bai_tb": tb(kq["bai"]), "chat_tb": tb(kq["chat"])}
    ra = thu_muc_kho_mau() / f"bao_cao_de_nhin_{nhan}.json"
    ra.write_text(json.dumps(kq, ensure_ascii=False, indent=1), encoding="utf-8")
    for nhom in ("bai", "chat"):
        for k, v in kq[nhom].items():
            chi_tiet = v.get("loi_ve") or " ".join(f"{a}={b}" for a, b in v.items() if a != "diem" and b)
            print(f"{nhom:<5}{k:<22}{v['diem']:>6}  {chi_tiet[:110]}")
    print("trung bình:", kq["tong"], "→", ra)


if __name__ == "__main__":
    if "--de-nhin" in sys.argv:
        main_de_nhin(sys.argv[1:])
    else:
        main()
