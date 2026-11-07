"""Xuất mô hình VERA Arc cho trang giới thiệu, dựng lại từ mo_hinh_arc.py, không ghi đè gói STL.

Chạy:  python tools/tay_cam_arc/xuat_web.py
Ra: trang_gioi_thieu/mo_hinh/arc.bin + arc.json (canh3d.js đọc)

arc.bin: mỗi đỉnh 10 byte (x, y, z int16 tính theo 1/256 mm; nx, ny, nz int8; 1 byte đệm), rồi tới chỉ số tam giác của chi tiết đó.
arc.json: mỗi chi tiết ghi ma, ten, mau, tach (mm), lk (linh kiện), v [vị trí, số đỉnh], i [vị trí, số chỉ số, số byte mỗi chỉ số].
"""
import json
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mo_hinh_arc as arc  # noqa: E402

THU_MUC = os.path.join(arc.GOC, "trang_gioi_thieu", "mo_hinh")
GOC_GAY = 38.0          # hai mặt lệch nhau quá góc này thì giữ cạnh sắc


def ma_hoa(m):
    """Trả về (đỉnh 10 byte, chỉ số tam giác), đỉnh bị tách ở cạnh sắc để bóng đổ đúng."""
    mesh = m.calculate_normals(0, GOC_GAY).to_mesh(0)
    d = np.asarray(mesh.vert_properties, dtype=np.float64)
    f = np.asarray(mesh.tri_verts, dtype=np.int64).reshape(-1)
    so = np.round(d[:, :3] * 256)
    if np.abs(so).max() > 32767:
        raise ValueError(f"tọa độ vượt int16: {np.abs(d[:, :3]).max():.1f} mm")
    n = d[:, 3:6].copy()
    # manifold để pháp tuyến bằng 0 ở vài đỉnh, tính lại từ các mặt kề (theo diện tích)
    hong = np.linalg.norm(n, axis=1) < 0.5
    if hong.any():
        tam_giac = f.reshape(-1, 3)
        p = d[:, :3][tam_giac]
        mat = np.cross(p[:, 1] - p[:, 0], p[:, 2] - p[:, 0])
        cong = np.zeros_like(n)
        for k in range(3):
            np.add.at(cong, tam_giac[:, k], mat)
        n[hong] = cong[hong]
    n = n / np.maximum(np.linalg.norm(n, axis=1, keepdims=True), 1e-9)
    rec =np.zeros(len(d), dtype=[("p", "<i2", 3), ("n", "i1", 3), ("dem", "i1")])
    rec["p"] = so.astype(np.int16)
    rec["n"] = np.round(n * 127).astype(np.int8)
    return rec.tobytes(), f


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    ds = [(ma, ten, mau, m, tach, False) for ma, ten, mau, m, _, tach, _ in arc.chi_tiet_in()]
    ds += [(ma, ten, mau, m, arc.TACH_LK[ma], True) for ma, ten, mau, m in arc.khoi_linh_kien()]
    ds.append(("chu", "Logo và chữ VERA LAB", "#04141a", arc.khoi_nhan(arc.SAU_CHU, -0.05), list(46 * arc.w_vong), False))

    bb = None
    tt, nhi, vi_tri = [], bytearray(), 0
    for ma, ten, mau, m, tach, lk in ds:
        dinh, chi_so = ma_hoa(m)
        if len(dinh) // 10 < 65536:
            cs, b = chi_so.astype("<u2").tobytes(), 2
        else:
            cs, b = chi_so.astype("<u4").tobytes(), 4
        # Int16Array, Uint32Array của canh3d.js cần vị trí chia hết cho số byte
        dem = (-vi_tri) % 4
        nhi += bytes(dem)
        vi_tri += dem
        v = [vi_tri, len(dinh) // 10]
        nhi += dinh
        vi_tri += len(dinh)
        dem = (-vi_tri) % b
        nhi += bytes(dem)
        vi_tri += dem
        i = [vi_tri, len(chi_so), b]
        nhi += cs
        vi_tri += len(cs)
        tt.append({"ma": ma, "ten": ten, "mau": mau, "tach": [round(float(x), 1) for x in tach], "lk": lk, "v": v, "i": i})
        hop = m.bounding_box()
        hop = np.array(hop)
        bb = hop if bb is None else np.array([*np.minimum(bb[:3], hop[:3]), *np.maximum(bb[3:], hop[3:])])
        print(f"{ma:10s} {v[1]:6d} đỉnh {i[1] // 3:7d} tam giác")

    os.makedirs(THU_MUC, exist_ok=True)
    with open(os.path.join(THU_MUC, "arc.bin"), "wb") as fh:
        fh.write(nhi)
    with open(os.path.join(THU_MUC, "arc.json"), "w", encoding="utf-8") as fh:
        json.dump(tt, fh, ensure_ascii=False)
    print(f"arc.bin {len(nhi) / 1e6:.2f} MB, hộp bao x {bb[0]:.1f}..{bb[3]:.1f}, y {bb[1]:.1f}..{bb[4]:.1f}, z {bb[2]:.1f}..{bb[5]:.1f} mm")


if __name__ == "__main__":
    main()
