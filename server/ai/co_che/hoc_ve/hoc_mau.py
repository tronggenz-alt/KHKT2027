"""Học cách vẽ từ đáp án: quét hình ChemDraw trong file Word (.docx, .doc), gom theo hệ vòng, chọn cách vẽ đa số làm mẫu.

    python -m server.ai.co_che.hoc_ve.hoc_mau "<thư mục đề>" ...  → $VERA_DU_LIEU_RIENG/co_che/kho_mau/khung_mau_dap_an.json (+ in tóm tắt)

- Chỉ lấy phân tử hữu cơ (≥ 3 C, có liên kết); bỏ thư mục "(Bản sao)" và hình trùng (cùng nội dung CDX).
- Toạ độ chuẩn hoá: độ dài liên kết trung vị của phân tử = 1, trục y hướng lên, tâm hệ vòng ở gốc. KHÔNG xoay,
  vì hướng đặt khung trên trang giấy (benzene đỉnh nhọn hay cạnh phẳng ở trên, propellane kiểu "lều"…) chính là quy ước cần học.
- Hệ vòng = các vòng dùng chung ≥ 2 nguyên tử (ngưng tụ, cầu); vòng spiro tách riêng.
- Hai mức mẫu:
    khung     : chỉ hệ vòng (mọi nguyên tử là C, mọi liên kết đơn) → benzene và cyclohexane dùng chung lục giác;
    khung_vo  : hệ vòng + các nguyên tử gắn trực tiếp vào vòng (giữ bậc liên kết ra ngoài) → học được hướng đặt vòng
                theo nhóm thế, vd vòng 4 cạnh mang =CH2 thì đáp án vẽ hình thoi "=◇" cho =CH2 nằm ngang.
- Mỗi mẫu: ghép từng lần xuất hiện vào nhau theo mọi đối xứng của đồ thị, gom cụm theo độ lệch toạ độ (RMSD < 0,3),
  lưu tối đa 3 cách vẽ phổ biến (phần tử trung tâm của cụm) kèm tỉ lệ dùng.
"""
from __future__ import annotations

import hashlib
import json
import statistics
import sys
import unicodedata
from collections import Counter, defaultdict
from pathlib import Path

import numpy as np
from rdkit import Chem, RDLogger

from ..duong_dan import thu_muc_kho_mau
from . import doc_cdx

RDLogger.DisableLog("rdApp.*")
NHOM_VIET_TAT = (4, 5, 7)          # loại nút ChemDraw: nickname, fragment, generic → nhãn viết gọn


def thanh_mol(m: dict):
    """mảnh CDX → (RWMol không sanitize, toạ độ N×2, danh sách nhãn) hoặc None."""
    ids = list(m["nut"])
    if len(ids) < 3 or not m["lk"]:
        return None
    rw = Chem.RWMol()
    chi_so = {}
    for k in ids:
        n = m["nut"][k]
        z = 0 if n["loai"] in NHOM_VIET_TAT else (n["z"] if 0 < n["z"] < 119 else 6)
        a = Chem.Atom(z)
        a.SetNoImplicit(True)
        chi_so[k] = rw.AddAtom(a)
    da = set()
    for b in m["lk"]:
        i, j = chi_so[b["a"]], chi_so[b["b"]]
        if i == j or (min(i, j), max(i, j)) in da:
            continue
        da.add((min(i, j), max(i, j)))
        rw.AddBond(i, j, {2: Chem.BondType.DOUBLE, 4: Chem.BondType.TRIPLE}.get(b["bac"], Chem.BondType.SINGLE))
    if sum(1 for k in ids if m["nut"][k]["z"] == 6 and m["nut"][k]["loai"] not in NHOM_VIET_TAT) < 3:
        return None
    rw.UpdatePropertyCache(strict=False)
    Chem.FastFindRings(rw)
    xy = np.array([[m["nut"][k]["x"], m["nut"][k]["y"]] for k in ids])
    nhan = [m["nut"][k]["nhan"] for k in ids]
    return rw, xy, nhan


def he_vong(mol) -> list[set[int]]:
    vong = [set(r) for r in mol.GetRingInfo().AtomRings()]
    nhom: list[set[int]] = []
    for r in vong:
        gop = [g for g in nhom if len(g & r) >= 2]
        for g in gop:
            nhom.remove(g)
            r = r | g
        nhom.append(r)
    return nhom


def khoa_va_thu_tu(mol, nguyen_tu: set[int], vo: bool = False):
    """SMILES đồ thị chính tắc + thứ tự nguyên tử theo hạng chính tắc + mol đồ thị.

    vo=False: chỉ hệ vòng (C, liên kết đơn). vo=True: thêm nguyên tử gắn trực tiếp (dạng *) và giữ bậc liên kết ra ngoài.
    """
    them = set()
    if vo:
        for i in nguyen_tu:
            for n in mol.GetAtomWithIdx(i).GetNeighbors():
                if n.GetIdx() not in nguyen_tu:
                    them.add(n.GetIdx())
    ds = sorted(nguyen_tu | them)
    q = Chem.RWMol()
    moi = {i: q.AddAtom(Chem.Atom(0 if i in them else 6)) for i in ds}
    for b in mol.GetBonds():
        i, j = b.GetBeginAtomIdx(), b.GetEndAtomIdx()
        if i in moi and j in moi and not (i in them and j in them):
            ra_ngoai = i in them or j in them
            q.AddBond(moi[i], moi[j], b.GetBondType() if ra_ngoai and b.GetBondType() in (Chem.BondType.DOUBLE, Chem.BondType.TRIPLE) else Chem.BondType.SINGLE)
    q = q.GetMol()
    q.UpdatePropertyCache(strict=False)
    Chem.FastFindRings(q)
    hang = list(Chem.CanonicalRankAtoms(q, breakTies=True))
    thu_tu = [ds[i] for i in sorted(range(len(ds)), key=lambda i: hang[i])]
    return Chem.MolToSmiles(q), thu_tu, q


def doi_xung_giu_loai(q, toi_da: int = 200) -> list[tuple[int, ...]]:
    """Các phép đối xứng của đồ thị mẫu giữ nguyên loại nút: nguyên tử gắn ngoài (dạng *, số hiệu 0) chỉ khớp với nút
    gắn ngoài. (Nguyên tử * trong RDKit khớp mọi nguyên tử: không lọc thì nhóm thế có thể bị đặt vào vị trí trong vòng.)"""
    loai = [a.GetAtomicNum() for a in q.GetAtoms()]
    return [g for g in q.GetSubstructMatches(q, uniquify=False, maxMatches=toi_da, useChirality=False)
            if all(loai[i] == loai[j] for i, j in enumerate(g))]


def chon_cach_ve(khoa: str, ds: list, q) -> dict:
    """Gom cụm các lần xuất hiện của một mẫu → tối đa 3 cách vẽ phổ biến."""
    doi_xung = q.GetSubstructMatches(q, uniquify=False, maxMatches=500, useChirality=False)

    def lech(A, B):
        return min(float(np.sqrt(((A[list(g)] - B) ** 2).sum(1).mean())) for g in doi_xung)
    mau_thu = ds if len(ds) <= 160 else ds[:: len(ds) // 160 + 1]
    cum = []
    for P, ng in mau_thu:
        for c in cum:
            if lech(P, c["dai_dien"]) < .3:
                c["thanh_vien"].append((P, ng))
                break
        else:
            cum.append({"dai_dien": P, "thanh_vien": [(P, ng)]})
    cum.sort(key=lambda c: -len(c["thanh_vien"]))
    tron = lambda P: [[round(float(x), 3), round(float(y), 3)] for x, y in P]
    cach_ve = []
    for c in cum[:3]:
        if len(c["thanh_vien"]) < 2 and cach_ve:
            break
        tv = c["thanh_vien"][:40]
        tam = min(tv, key=lambda a: sum(lech(a[0], b[0]) for b in tv))[0]
        cach_ve.append({"ty_le": round(len(c["thanh_vien"]) / len(mau_thu), 2), "toa_do": tron(tam)})
    return {
        "khoa": khoa, "so_nguyen_tu": q.GetNumAtoms(), "so_lan": len(ds),
        "thong_nhat": cach_ve[0]["ty_le"], "so_cach_ve": len(cum), "cach_ve": cach_ve, "toa_do": cach_ve[0]["toa_do"],
        "canh": [[b.GetBeginAtomIdx(), b.GetEndAtomIdx()] for b in q.GetBonds()],
        "nguon": sorted({ng for _, ng in cum[0]["thanh_vien"]})[:5],
    }


def main(thu_muc: list[str]) -> None:
    da_xem, so_file, so_hinh = set(), 0, 0
    theo_thu_muc = Counter()
    xuat_hien = {"khung": defaultdict(list), "khung_vo": defaultdict(list)}
    mau_q = {"khung": {}, "khung_vo": {}}
    do_dai_pt, nhan_dem, mui_ten_dem = [], Counter(), Counter()
    for td in thu_muc:
        goc = Path(td)
        tep = sorted([*goc.glob("**/*.docx"), *goc.glob("**/*.doc")])
        for p in tep:
            ten = unicodedata.normalize("NFC", str(p))
            if "Bản sao" in ten or "/~$" in ten:
                continue
            so_file += 1
            nhom = unicodedata.normalize("NFC", p.relative_to(goc).parts[0]) if len(p.relative_to(goc).parts) > 1 else goc.name
            for _, cdx in doc_cdx.chemdraw_trong_file(p):
                h = hashlib.md5(cdx).hexdigest()
                if h in da_xem:
                    continue
                da_xem.add(h)
                cay = doc_cdx.doc_cay(cdx)
                if cay is None:
                    continue
                so_hinh += 1
                theo_thu_muc[nhom] += 1
                for mt in doc_cdx.cac_mui_ten(cay):
                    mui_ten_dem["cong (cơ chế)" if mt["cong"] else "thẳng (phản ứng)"] += 1
                for m in doc_cdx.cac_manh(cay):
                    r = thanh_mol(m)
                    if r is None:
                        continue
                    mol, xy, nhan = r
                    dai = [np.linalg.norm(xy[b.GetBeginAtomIdx()] - xy[b.GetEndAtomIdx()]) for b in mol.GetBonds()]
                    dai = [d for d in dai if d > 1e-3]
                    if not dai:
                        continue
                    L = statistics.median(dai)
                    do_dai_pt.append(L)
                    for t in nhan:
                        if t and len(t) > 1:
                            nhan_dem[t] += 1
                    xy = xy / L
                    nguon = Path(ten).parent.name[:60]
                    for hv in he_vong(mol):
                        if len(hv) < 3 or len(hv) > 24:
                            continue
                        tam = xy[sorted(hv)].mean(0)
                        for muc, vo in (("khung", False), ("khung_vo", True)):
                            khoa, thu_tu, q = khoa_va_thu_tu(mol, hv, vo)
                            xuat_hien[muc][khoa].append((xy[thu_tu] - tam, nguon))
                            mau_q[muc].setdefault(khoa, q)

    kq = {}
    for muc, nguong in (("khung", 3), ("khung_vo", 2)):
        kq[muc] = [chon_cach_ve(k, ds, mau_q[muc][k])
                   for k, ds in sorted(xuat_hien[muc].items(), key=lambda kv: -len(kv[1])) if len(ds) >= nguong]
    tom_tat = {
        "so_file": so_file, "so_hinh_chemdraw": so_hinh, "theo_thu_muc": dict(theo_thu_muc.most_common()),
        "so_khung": len(kq["khung"]), "so_khung_vo": len(kq["khung_vo"]),
        "do_dai_lien_ket_pt": round(statistics.median(do_dai_pt), 2) if do_dai_pt else None,
        "nhan_pho_bien": nhan_dem.most_common(80), "mui_ten": dict(mui_ten_dem),
    }
    ra = thu_muc_kho_mau() / "khung_mau_dap_an.json"
    ra.write_text(json.dumps({"tom_tat": tom_tat, **kq}, ensure_ascii=False, indent=1))
    print(f"{so_file} file, {so_hinh} hình ChemDraw (đã bỏ trùng) → {len(kq['khung'])} khung, {len(kq['khung_vo'])} khung kèm nhóm gắn")
    print("theo thư mục:", ", ".join(f"{k}: {v}" for k, v in theo_thu_muc.most_common()))
    print("độ dài liên kết trung vị:", tom_tat["do_dai_lien_ket_pt"], "pt | mũi tên:", dict(mui_ten_dem))
    print("nhãn hay dùng:", ", ".join(f"{t}×{c}" for t, c in nhan_dem.most_common(25)))
    print("→", ra)


if __name__ == "__main__":
    main(sys.argv[1:])
