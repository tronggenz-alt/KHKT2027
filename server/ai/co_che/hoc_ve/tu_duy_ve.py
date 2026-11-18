"""Tư duy vẽ: chọn cách bày một phân tử giống đáp án nhất, kể cả khi khung CHƯA có trong kho mẫu.

Hai nguồn kiến thức, đều học từ hình ChemDraw trong đáp án:
  1. Kho mẫu (nhớ): khung / khung kèm nhóm gắn đã gặp → đặt đúng như đa số đáp án vẽ.
  2. Nguyên tắc chung (tư duy) – thống kê trên mọi hình, dùng được cho khung lạ:
       - nét nằm trên lưới góc 30°;
       - vòng đơn n cạnh đặt đỉnh nhọn hay cạnh phẳng lên trên (theo đa số của từng cỡ vòng);
       - trục dài phân tử nằm ngang, hình bày bề ngang;
       - nhánh ra khỏi vòng chia đôi góc ngoài;
       - không nét cắt nhau, không nguyên tử chen sát.
Cách chọn: sinh nhiều phương án (theo kho mẫu; CoordGen và bộ bày RDKit thường × 12 góc xoay × lật), tính "điểm phạt
phong cách" theo các nguyên tắc trên, lấy phương án phạt ít nhất. Toạ độ ra theo độ dài liên kết = 1.
"""
from __future__ import annotations

import math
from collections import defaultdict

import numpy as np
from rdkit import Chem
from rdkit.Chem import rdDepictor
from rdkit.Geometry import Point2D

from . import hoc_mau


# ------------------------------------------------------------------ hình học dùng chung
def lien_ket(m) -> np.ndarray:
    return np.array([[b.GetBeginAtomIdx(), b.GetEndAtomIdx()] for b in m.GetBonds()], dtype=int).reshape(-1, 2)


def so_net_cat(P: np.ndarray, L: np.ndarray) -> int:
    """Số cặp liên kết cắt nhau (không chung đầu), tính bằng numpy."""
    if len(L) < 2:
        return 0
    A, B = P[L[:, 0]], P[L[:, 1]]
    d = B - A
    i, j = np.triu_indices(len(L), 1)
    chung = (L[i, 0] == L[j, 0]) | (L[i, 0] == L[j, 1]) | (L[i, 1] == L[j, 0]) | (L[i, 1] == L[j, 1])
    den = d[i, 0] * d[j, 1] - d[i, 1] * d[j, 0]
    ok = (np.abs(den) > 1e-9) & ~chung
    w = A[j] - A[i]
    t = np.where(ok, (w[:, 0] * d[j, 1] - w[:, 1] * d[j, 0]) / np.where(ok, den, 1), -1)
    u = np.where(ok, (w[:, 0] * d[i, 1] - w[:, 1] * d[i, 0]) / np.where(ok, den, 1), -1)
    return int(((t > .01) & (t < .99) & (u > .01) & (u < .99)).sum())


def so_cap_sat(P: np.ndarray, L: np.ndarray, nguong: float = .55) -> int:
    n = len(P)
    if n < 2:
        return 0
    D = np.linalg.norm(P[:, None] - P[None], axis=2)
    M = np.ones((n, n), bool)
    np.fill_diagonal(M, False)
    M[L[:, 0], L[:, 1]] = M[L[:, 1], L[:, 0]] = False
    return int(((D < nguong) & M).sum() // 2)


def chuan_hoa(P: np.ndarray, L: np.ndarray) -> np.ndarray:
    d = np.linalg.norm(P[L[:, 0]] - P[L[:, 1]], axis=1) if len(L) else np.array([1.0])
    d = d[d > 1e-6]
    k = np.median(d) if len(d) else 1.0
    return (P - P.mean(0)) / k


def dinh_nhon(P: np.ndarray, vong) -> bool:
    ys = np.sort(P[list(vong), 1])
    return ys[-1] - ys[-2] > .3


def truc_ngang(P: np.ndarray) -> float:
    """|cos| góc giữa trục dài và phương ngang (1 = nằm ngang)."""
    Q = P - P.mean(0)
    if len(Q) < 2 or not np.any(Q):
        return 1.0
    _, _, Vt = np.linalg.svd(Q, full_matrices=False)
    return abs(Vt[0][0])


def nhanh_chinh(m, P: np.ndarray):
    """Hướng (độ, 0 = sang phải, ngược chiều kim đồng hồ) của nhánh LỚN NHẤT ra khỏi hệ vòng lớn nhất, đo từ tâm vòng
    tới nguyên tử đầu nhánh. None nếu phân tử không có vòng hoặc vòng không có nhánh."""
    he = sorted((h for h in hoc_mau.he_vong(m) if len(h) >= 3), key=len, reverse=True)
    if not he:
        return None
    vong = he[0]
    tot, co = None, 0
    for i in vong:
        for n in m.GetAtomWithIdx(i).GetNeighbors():
            j = n.GetIdx()
            if j in vong:
                continue
            da, ngan = {j}, [j]                     # cỡ nhánh: số nguyên tử đi tới được mà không qua vòng
            while ngan:
                x = ngan.pop()
                for y in m.GetAtomWithIdx(x).GetNeighbors():
                    k = y.GetIdx()
                    if k not in da and k not in vong:
                        da.add(k); ngan.append(k)
            if len(da) > co:
                tot, co = j, len(da)
    if tot is None:
        return None
    v = P[tot] - P[sorted(vong)].mean(0)
    return math.degrees(math.atan2(v[1], v[0])) % 360


# ------------------------------------------------------------------ học nguyên tắc chung từ các hình đáp án
def hoc_luat(tap: list[tuple]) -> dict:
    """tap: [(mol, P chuẩn hoá)] → thống kê phong cách."""
    luoi, tong, nhon, dem_vong, ngang, rong_cao, lech_nhanh = 0, 0, defaultdict(int), defaultdict(int), [], [], []
    huong = np.ones(12)                                          # làm trơn Laplace
    for m, P in tap:
        g = nhanh_chinh(m, P)
        if g is not None:
            huong[int(((g + 15) % 360) // 30)] += 1
        L = lien_ket(m)
        if not len(L):
            continue
        g = np.degrees(np.arctan2(*(P[L[:, 1]] - P[L[:, 0]]).T[::-1])) % 30
        luoi += int((np.minimum(g, 30 - g) < 3).sum()); tong += len(L)
        ri = m.GetRingInfo()
        for v in ri.AtomRings():
            if all(ri.NumAtomRings(a) == 1 for a in v):          # vòng đơn đứng riêng
                dem_vong[len(v)] += 1
                nhon[len(v)] += dinh_nhon(P, v)
        ngang.append(truc_ngang(P))
        w, h = np.ptp(P[:, 0]), np.ptp(P[:, 1])
        if w + h > 0:
            rong_cao.append(w / (w + h))
        for a in m.GetAtoms():
            if not a.IsInRing() or a.GetDegree() != 3:
                continue
            ke = [n.GetIdx() for n in a.GetNeighbors()]
            ngoai = [k for k in ke if not m.GetBondBetweenAtoms(a.GetIdx(), k).IsInRing()]
            trong = [k for k in ke if k not in ngoai]
            if len(ngoai) == 1 and len(trong) == 2:
                lech_nhanh.append(_lech_phan_giac(P, a.GetIdx(), trong, ngoai[0]))
    return {
        "ty_le_luoi_30": round(luoi / max(tong, 1), 3),
        "vong_dinh_nhon": {str(k): round(nhon[k] / dem_vong[k], 3) for k in sorted(dem_vong) if dem_vong[k] >= 10},
        "so_vong_don": {str(k): dem_vong[k] for k in sorted(dem_vong) if dem_vong[k] >= 10},
        "ty_le_truc_ngang": round(float(np.mean([x > .966 for x in ngang])), 3) if ngang else 0,
        "rong_tren_tong_trung_vi": round(float(np.median(rong_cao)), 3) if rong_cao else .5,
        "lech_nhanh_trung_vi_do": round(float(np.median(lech_nhanh)), 1) if lech_nhanh else 0,
        # xác suất nhánh chính chĩa về 12 hướng 0°, 30°, …, 330° (0° = sang phải)
        "huong_nhanh_chinh": [round(float(x), 4) for x in huong / huong.sum()],
    }


def _lech_phan_giac(P, i, trong, ngoai) -> float:
    a = P[trong[0]] - P[i]; b = P[trong[1]] - P[i]
    a /= np.linalg.norm(a) + 1e-9; b /= np.linalg.norm(b) + 1e-9
    pg = -(a + b)
    if np.linalg.norm(pg) < 1e-6:
        return 0.0
    pg /= np.linalg.norm(pg)
    c = P[ngoai] - P[i]; c /= np.linalg.norm(c) + 1e-9
    return math.degrees(math.acos(max(-1, min(1, float(pg @ c)))))


# ------------------------------------------------------------------ bộ vẽ
class TuDuy:
    def __init__(self, kho_khung: dict, kho_vo: dict, luat: dict):
        self.kho, self.kho_vo, self.luat = kho_khung, kho_vo, luat

    # điểm phạt phong cách (P đã chuẩn hoá liên kết = 1)
    def phat(self, m, P: np.ndarray, L: np.ndarray) -> float:
        lu = self.luat
        p = 10 * so_net_cat(P, L) + 3 * so_cap_sat(P, L)
        if len(L):
            g = np.degrees(np.arctan2(*(P[L[:, 1]] - P[L[:, 0]]).T[::-1])) % 30
            p += 2 * lu.get("ty_le_luoi_30", .8) * float((np.minimum(g, 30 - g) / 15).mean()) * 3
        ri = m.GetRingInfo()
        for v in ri.AtomRings():
            tl = lu.get("vong_dinh_nhon", {}).get(str(len(v)))
            if tl is not None and all(ri.NumAtomRings(a) == 1 for a in v):
                muon_nhon = tl >= .5
                if dinh_nhon(P, v) != muon_nhon:
                    p += 4 * abs(tl - .5)
        p += 2 * lu.get("ty_le_truc_ngang", .5) * (1 - truc_ngang(P))
        if lu.get("huong_nhanh_chinh"):                          # nhánh chính chĩa hướng quen thuộc của đáp án
            g = nhanh_chinh(m, P)
            if g is not None:
                pr = lu["huong_nhanh_chinh"][int(((g + 15) % 360) // 30)]
                p += 1.5 * -math.log(pr / max(lu["huong_nhanh_chinh"]))
        w, h = np.ptp(P[:, 0]), np.ptp(P[:, 1])
        if w + h > 0:
            p += max(0.0, lu.get("rong_tren_tong_trung_vi", .55) - w / (w + h)) * 2
        for a in m.GetAtoms():
            if a.IsInRing() and a.GetDegree() == 3:
                ke = [n.GetIdx() for n in a.GetNeighbors()]
                ngoai = [k for k in ke if not m.GetBondBetweenAtoms(a.GetIdx(), k).IsInRing()]
                trong = [k for k in ke if k not in ngoai]
                if len(ngoai) == 1 and len(trong) == 2:
                    p += _lech_phan_giac(P, a.GetIdx(), trong, ngoai[0]) / 60
        return p

    def _toa_do(self, m) -> np.ndarray:
        c = m.GetConformer()
        return np.array([[c.GetAtomPosition(i).x, c.GetAtomPosition(i).y] for i in range(m.GetNumAtoms())])

    def _bay(self, m, coordgen: bool, coordMap=None):
        rdDepictor.SetPreferCoordGen(coordgen)
        try:
            if coordMap:
                rdDepictor.Compute2DCoords(m, coordMap={k: Point2D(*v) for k, v in coordMap.items()})
            else:
                rdDepictor.Compute2DCoords(m)
            return self._toa_do(m)
        except Exception:  # noqa: BLE001
            return None
        finally:
            rdDepictor.SetPreferCoordGen(True)

    def phuong_an_mau(self, m):
        """Phương án theo kho mẫu (hệ vòng lớn nhất): (coordMap, thưởng)."""
        he = sorted((h for h in hoc_mau.he_vong(m) if 3 <= len(h) <= 24), key=len, reverse=True)
        if not he:
            return []
        ra = []
        for vo, kho, tru in ((True, self.kho_vo, 0), (False, self.kho, .5)):
            khoa, thu_tu, q = hoc_mau.khoa_va_thu_tu(m, he[0], vo)
            mau = kho.get(khoa)
            if not mau:
                continue
            dx = hoc_mau.doi_xung_giu_loai(q, 48)
            for cv in mau["cach_ve"]:
                T = np.array(cv["toa_do"]) * 1.5
                for g in dx:
                    ra.append(({thu_tu[g[i]]: T[i] for i in range(len(g))}, cv["ty_le"] - tru))
        return ra

    def ve(self, m, dung_mau: bool = True, dung_tu_duy: bool = True) -> np.ndarray:
        """Thứ tự ưu tiên như người vẽ: khung đã gặp → theo mẫu; mạch hở → cách bày tự nhiên; tư duy chỉ quyết
        khi khung mới, hoặc khi phương án theo mẫu / tự nhiên gây lỗi dễ nhìn (nét cắt, nguyên tử chen sát)."""
        L = lien_ket(m)
        ung_vien = []                                                    # (P chuẩn hoá, thưởng)
        sach = lambda P: so_net_cat(P, L) + so_cap_sat(P, L) == 0
        if dung_mau:
            for cm, thuong in self.phuong_an_mau(m)[:60]:
                P = self._bay(m, False, cm)
                if P is not None:
                    ung_vien.append((chuan_hoa(P, L), thuong))
            if ung_vien and dung_tu_duy:
                tot = min(ung_vien, key=lambda x: self.phat(m, x[0], L) - 6 * x[1])[0]
                if sach(tot):
                    return tot
        if dung_tu_duy and not ung_vien and not any(3 <= len(h) for h in hoc_mau.he_vong(m)):
            P = self._bay(m, True)                                       # mạch hở: giữ cách bày tự nhiên nếu sạch
            if P is not None and sach(chuan_hoa(P, L)):
                return chuan_hoa(P, L)
        if dung_tu_duy or not ung_vien:
            for cg in ((True, False) if dung_tu_duy else (True,)):
                P = self._bay(m, cg)
                if P is None:
                    continue
                P = chuan_hoa(P, L)
                goc = range(0, 360, 30) if dung_tu_duy else (0,)
                for d in goc:
                    c, s = math.cos(math.radians(d)), math.sin(math.radians(d))
                    R = np.array([[c, -s], [s, c]])
                    for lat in ((1, -1) if dung_tu_duy else (1,)):
                        # hướng gốc của bộ bày (không xoay, không lật) được cộng điểm nhẹ: thường đã là hướng đọc tự nhiên
                        ung_vien.append(((P * np.array([lat, 1])) @ R.T, .4 if (d == 0 and lat == 1) else 0.0))
        if not ung_vien:
            return np.zeros((m.GetNumAtoms(), 2))
        if not dung_tu_duy and not dung_mau:
            return ung_vien[0][0]
        # kho mẫu (hướng đáp án đã chọn) được ưu tiên mạnh; tư duy chỉ quyết khi không có mẫu hoặc mẫu gây lỗi dễ nhìn
        return min(ung_vien, key=lambda x: self.phat(m, x[0], L) - 6 * x[1])[0]

    def cac_phuong_an(self, m) -> list[tuple[np.ndarray, float]]:
        """Mọi phương án kèm điểm (càng thấp càng tốt) – để bước SƠ ĐỒ chọn theo ngữ cảnh.
        Gồm: theo kho mẫu (giữ hướng đã học); bản xoay/lật của phương án mẫu tốt nhất (phạt thêm 1 vì rời hướng đã học);
        CoordGen và bộ bày thường × 12 góc × lật."""
        L = lien_ket(m)
        ra = []
        for cm, th in self.phuong_an_mau(m)[:60]:
            P = self._bay(m, False, cm)
            if P is not None:
                P = chuan_hoa(P, L)
                ra.append((P, self.phat(m, P, L) - 6 * th))
        nguon = []
        if ra:
            nguon.append((min(ra, key=lambda x: x[1])[0], 1.0))
        for cg in (True, False):
            P = self._bay(m, cg)
            if P is not None:
                nguon.append((chuan_hoa(P, L), 0.0))
        for P0, them in nguon:
            for d in range(0, 360, 30):
                c, s = math.cos(math.radians(d)), math.sin(math.radians(d))
                R = np.array([[c, -s], [s, c]])
                for lat in (1, -1):
                    if them and d == 0 and lat == 1:
                        continue
                    P = (P0 * np.array([lat, 1])) @ R.T
                    ra.append((P, self.phat(m, P, L) + them - (.4 if (d == 0 and lat == 1) else 0)))
        return ra

