"""Xuất mô hình VERA Band 2 cho trang giới thiệu, đọc từ STL tư thế lắp, không ghi đè gói in.

Chạy:  python tools/tay_cam_band2/xuat_web.py
Vào:   docs/tay-cam-band-2/xem/*.stl + canh.json (màu, tên, hướng tách rời)
Ra:    trang_gioi_thieu/mo_hinh/band2.bin + band2.json (canh3d.js đọc), cùng định dạng với arc.bin của VERA Arc:
  band2.bin: mỗi đỉnh 10 byte (x, y, z int16 tính theo 1/256 mm; nx, ny, nz int8; 1 byte đệm), rồi chỉ số tam giác.
  band2.json: mỗi chi tiết ghi ma, ten, mau, tach (mm), lk (linh kiện), v [vị trí, số đỉnh], i [vị trí, số chỉ số, số byte].
"""
import json
import os
import sys

import numpy as np

GOC = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
VAO = os.path.join(GOC, "docs", "tay-cam-band-2", "xem")
RA = os.path.join(GOC, "trang_gioi_thieu", "mo_hinh")
GOC_GAY = 38.0  # hai mặt lệch nhau quá góc này thì giữ cạnh sắc


def doc_stl(duong):
    """Đọc STL nhị phân (hoặc ASCII), trả về mảng tam giác (n, 3, 3) mm."""
    with open(duong, "rb") as fh:
        du_lieu = fh.read()
    if len(du_lieu) >= 84:
        n = int.from_bytes(du_lieu[80:84], "little")
        if 84 + 50 * n == len(du_lieu):
            rec = np.frombuffer(du_lieu, dtype=[("n", "<f4", 3), ("v", "<f4", (3, 3)), ("a", "<u2")], count=n, offset=84)
            return rec["v"].astype(np.float64)
    dinh = [list(map(float, d.split()[1:4])) for d in du_lieu.decode("ascii", "ignore").splitlines() if d.strip().startswith("vertex")]
    return np.array(dinh, dtype=np.float64).reshape(-1, 3, 3)


def ma_hoa(tg):
    """Hàn đỉnh trùng, tính pháp tuyến mượt nhưng giữ cạnh sắc quá GOC_GAY. Trả về (đỉnh 10 byte, chỉ số, hộp bao)."""
    p = tg.reshape(-1, 3)
    khoa = np.round(p * 1e3).astype(np.int64)
    _, ma_dinh = np.unique(khoa, axis=0, return_inverse=True)
    ma_dinh = ma_dinh.reshape(-1, 3)
    mat = np.cross(tg[:, 1] - tg[:, 0], tg[:, 2] - tg[:, 0])  # độ dài = 2 lần diện tích
    dt = np.linalg.norm(mat, axis=1)
    giu = dt > 1e-9
    tg, mat, dt, ma_dinh = tg[giu], mat[giu], dt[giu], ma_dinh[giu]
    n_mat = mat / dt[:, None]

    # Mỗi góc tam giác: cộng pháp tuyến các mặt kề cùng đỉnh mà lệch dưới GOC_GAY so với mặt của góc đó
    so_tg = len(tg)
    goc_dinh = ma_dinh.reshape(-1)                      # đỉnh hàn của từng góc
    goc_mat = np.repeat(np.arange(so_tg), 3)            # mặt của từng góc
    thu_tu = np.argsort(goc_dinh, kind="stable")
    dinh_sx, mat_sx = goc_dinh[thu_tu], goc_mat[thu_tu]
    ranh = np.flatnonzero(np.r_[True, dinh_sx[1:] != dinh_sx[:-1], True])
    cos_gay = np.cos(np.radians(GOC_GAY))
    n_goc = np.zeros((len(goc_dinh), 3))
    for a, b in zip(ranh[:-1], ranh[1:]):
        cac_mat = mat_sx[a:b]
        nn = n_mat[cac_mat]
        w = mat[cac_mat]                                # trọng số theo diện tích
        ke = (nn @ nn.T) > cos_gay                      # (k, k): mặt j góp vào góc của mặt i
        tong = ke.astype(np.float64) @ w
        n_goc[thu_tu[a:b]] = tong
    n_goc /= np.maximum(np.linalg.norm(n_goc, axis=1, keepdims=True), 1e-12)

    vi_tri = np.round(tg.reshape(-1, 3) * 256)
    if np.abs(vi_tri).max() > 32767:
        raise ValueError(f"tọa độ vượt int16: {np.abs(tg).max():.1f} mm")
    ban_ghi = np.zeros(len(vi_tri), dtype=[("p", "<i2", 3), ("n", "i1", 3), ("dem", "i1")])
    ban_ghi["p"] = vi_tri.astype(np.int16)
    ban_ghi["n"] = np.round(n_goc * 127).astype(np.int8)
    # Gộp lại các góc có cùng vị trí và cùng pháp tuyến đã lượng tử
    duy_nhat, chi_so = np.unique(ban_ghi.view(np.uint8).reshape(-1, 10), axis=0, return_inverse=True)
    hop = np.r_[tg.reshape(-1, 3).min(0), tg.reshape(-1, 3).max(0)]
    return duy_nhat.tobytes(), chi_so.reshape(-1), hop


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    with open(os.path.join(VAO, "canh.json"), encoding="utf-8") as fh:
        canh = json.load(fh)
    tt, nhi, vi_tri, bb = [], bytearray(), 0, None
    for ct in canh["chi_tiet"]:
        duong = os.path.join(VAO, ct["ma"] + ".stl")
        if not os.path.exists(duong):
            print("thiếu", duong)
            continue
        dinh, chi_so, hop = ma_hoa(doc_stl(duong))
        if len(dinh) // 10 < 65536:
            cs, b = chi_so.astype("<u2").tobytes(), 2
        else:
            cs, b = chi_so.astype("<u4").tobytes(), 4
        dem = (-vi_tri) % 4  # Int16Array, Uint32Array cần vị trí chia hết cho số byte
        nhi += bytes(dem); vi_tri += dem
        v = [vi_tri, len(dinh) // 10]
        nhi += dinh; vi_tri += len(dinh)
        dem = (-vi_tri) % b
        nhi += bytes(dem); vi_tri += dem
        i = [vi_tri, len(chi_so), b]
        nhi += cs; vi_tri += len(cs)
        tt.append({"ma": ct["ma"], "ten": ct["ten"], "mau": ct["mau"], "tach": [round(float(x), 1) for x in ct["tach"]],
                   "lk": bool(ct.get("linh_kien", False)), "v": v, "i": i})
        if not ct.get("linh_kien"):
            bb = hop if bb is None else np.r_[np.minimum(bb[:3], hop[:3]), np.maximum(bb[3:], hop[3:])]
        print(f"{ct['ma']:11s} {v[1]:6d} đỉnh {i[1] // 3:7d} tam giác")
    os.makedirs(RA, exist_ok=True)
    with open(os.path.join(RA, "band2.bin"), "wb") as fh:
        fh.write(nhi)
    with open(os.path.join(RA, "band2.json"), "w", encoding="utf-8") as fh:
        json.dump(tt, fh, ensure_ascii=False)
    print(f"band2.bin {len(nhi) / 1e6:.2f} MB, hộp bao vỏ x {bb[0]:.1f}..{bb[3]:.1f}, y {bb[1]:.1f}..{bb[4]:.1f}, z {bb[2]:.1f}..{bb[5]:.1f} mm")


if __name__ == "__main__":
    main()
