"""Xuất mô hình tay cầm VERA Band 2 (bản in ×1.3) sang tệp nhị phân cho Unity (trang "Tay cầm" ở sảnh, thanh góc).

Chạy:  python tools/tay_cam_band2/xuat_unity.py [--ti-le 1.3] [--xem <thư mục STL lắp ráp có sẵn>]
Ra:    Phong thi nghiem ao/Assets/Lab/MoHinh/Resources/TayCam/VeraBand2.bytes   (Unity: Resources.Load<TextAsset>("TayCam/VeraBand2"))

Không có --xem thì chạy một BẢN SAO mo_hinh_band2.py trong thư mục tạm với VERA_TI_LE (để không ghi đè tệp in trong docs/).
Chỉ lấy phần nhìn thấy từ ngoài (vỏ, mặt tối, chén, cò, tấm mu tay, chữ, nắp pin, nắp nút, quai). Cần joystick do Unity vẽ
và nghiêng theo số đo; mạch bên trong không xuất.

Hệ trục mô hình (mm): x ra trước (mũi, phía ngón trỏ), y sang trái, z dọc cán lên — trùng hệ tay cầm của firmware.
Đổi sang Unity như Frames.ToUnity: (x, y, z) -> (-y, z, x), mét, gốc đặt ở tâm hộp bao; đảo thứ tự đỉnh tam giác vì đổi
hệ thuận tay phải sang tay trái. Pháp tuyến tách ở cạnh gấp > 35° nên cạnh sắc không bị làm tròn.

Định dạng (little-endian): "VTC1", int32 số phần, 12 float32 (tâm xoay cần, trục cần n, hướng tiến t, hướng phải r; Unity),
float32 thang toạ độ (m / đơn vị int16); mỗi phần: uint8 dài tên + tên UTF-8, 4 byte RGBA, uint8 vai trò
(0 thường, 1 = chạm A (mặt tối), 2 = chạm B (cò)), int32 số đỉnh, int32 số chỉ số, mỗi đỉnh 3 int16 vị trí + 3 int8 pháp tuyến,
chỉ số uint16 (số đỉnh < 65536) hoặc uint32.
"""
import argparse
import json
import math
import os
import shutil
import struct
import subprocess
import sys
import tempfile

import numpy as np

GOC = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
RA = os.path.join(GOC, "Phong thi nghiem ao", "Assets", "Lab", "MoHinh", "Resources", "TayCam", "VeraBand2.bytes")
PHAN = {  # mã chi tiết -> vai trò
    "than_phai": 0, "than_trai": 0, "nap": 1, "chen": 0, "co": 2, "tam_mu_tay": 0, "chu": 0, "nap_pin": 0, "nut_phu": 0, "quai": 0,
}
GOC_GAP = 35.0
THANG = 0.2 / 32767  # ±0,2 m


def doc_stl(duong):
    with open(duong, "rb") as f:
        d = f.read()
    n = struct.unpack_from("<I", d, 80)[0]
    if 84 + 50 * n == len(d):
        a = np.frombuffer(d, dtype=np.dtype([("n", "<f4", 3), ("v", "<f4", (3, 3)), ("a", "<u2")]), count=n, offset=84)
        return a["v"].astype(np.float64)
    tam = []  # STL chữ
    for dong in d.decode("ascii", "ignore").splitlines():
        p = dong.split()
        if p[:1] == ["vertex"]:
            tam.append([float(x) for x in p[1:4]])
    return np.array(tam, np.float64).reshape(-1, 3, 3)


def dinh_va_phap_tuyen(tg):
    """tg: (n, 3, 3) mm. Trả về (vị trí (m, 3), pháp tuyến (m, 3), chỉ số (n, 3)) với pháp tuyến tách ở cạnh gấp."""
    tg = tg[np.linalg.norm(np.cross(tg[:, 1] - tg[:, 0], tg[:, 2] - tg[:, 0]), axis=1) > 1e-9]
    fn = np.cross(tg[:, 1] - tg[:, 0], tg[:, 2] - tg[:, 0])
    dt = np.linalg.norm(fn, axis=1, keepdims=True)
    fn_don = fn / dt
    goc = tg.reshape(-1, 3)
    khoa = np.round(goc / 1e-4).astype(np.int64)
    _, vi_tri, = np.unique(khoa, axis=0, return_inverse=True)[:2]
    vi_tri = vi_tri.reshape(-1)
    mat = np.repeat(np.arange(len(tg)), 3)
    thu_tu = np.argsort(vi_tri, kind="stable")
    vt_s, mat_s = vi_tri[thu_tu], mat[thu_tu]
    tong = fn[mat_s].copy()  # mỗi góc tự cộng pháp tuyến (có trọng số diện tích) của mặt mình
    nguong = math.cos(math.radians(GOC_GAP))
    for j in range(1, 64):
        cung = vt_s[j:] == vt_s[:-j]
        if not cung.any():
            break
        a, b = np.nonzero(cung)[0], np.nonzero(cung)[0] + j
        hop = np.einsum("ij,ij->i", fn_don[mat_s[a]], fn_don[mat_s[b]]) > nguong
        a, b = a[hop], b[hop]
        np.add.at(tong, a, fn[mat_s[b]])
        np.add.at(tong, b, fn[mat_s[a]])
    pt = np.empty_like(tong)
    pt[thu_tu] = tong
    pt /= np.linalg.norm(pt, axis=1, keepdims=True)
    k2 = np.concatenate([vi_tri[:, None], np.round(pt * 127).astype(np.int64)], axis=1)
    _, dau, nguoc = np.unique(k2, axis=0, return_index=True, return_inverse=True)
    return goc[dau], pt[dau], nguoc.reshape(-1, 3)


def sang_unity(v):
    return np.stack([-v[:, 1], v[:, 2], v[:, 0]], axis=1)


def mau(hex_):
    h = hex_.lstrip("#")
    return bytes([int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), 255])


def main():
    ts = argparse.ArgumentParser()
    ts.add_argument("--ti-le", type=float, default=1.3)
    ts.add_argument("--xem", help="thư mục STL lắp ráp có sẵn (bỏ trống: dựng lại trong thư mục tạm)")
    a = ts.parse_args()
    sys.stdout.reconfigure(encoding="utf-8")
    tam_thoi = None
    xem = a.xem
    if not xem:
        tam_thoi = tempfile.mkdtemp(prefix="vera_unity_")
        os.makedirs(os.path.join(tam_thoi, "tools", "tay_cam_band2"))
        shutil.copy(os.path.join(GOC, "tools", "tay_cam_band2", "mo_hinh_band2.py"), os.path.join(tam_thoi, "tools", "tay_cam_band2"))
        xem = os.path.join(tam_thoi, "xem")
        os.makedirs(xem)
        env = dict(os.environ, VERA_TI_LE=str(a.ti_le), PYTHONIOENCODING="utf-8")
        subprocess.run([sys.executable, os.path.join(tam_thoi, "tools", "tay_cam_band2", "mo_hinh_band2.py"), xem], env=env, check=True,
                       stdout=subprocess.DEVNULL)
    with open(os.path.join(xem, "canh.json"), encoding="utf-8") as f:
        canh = {c["ma"]: c for c in json.load(f)["chi_tiet"]}

    phan = []
    for ma, vai in PHAN.items():
        tg = doc_stl(os.path.join(xem, ma + ".stl"))
        phan.append((ma, vai, tg))
    tat_ca = np.concatenate([p[2].reshape(-1, 3) for p in phan if p[0] != "quai"])
    tam = (tat_ca.min(0) + tat_ca.max(0)) / 2

    # cần joystick: tâm mặt bầu dục và pháp tuyến mặt đỉnh (giống mo_hinh_band2.py), toạ độ đã phóng
    K = a.ti_le
    ng = math.radians(20.0)
    n_mat = np.array([-math.sin(ng), 0, math.cos(ng)])
    t_mat = np.array([math.cos(ng), 0, math.sin(ng)])
    tam_can = K * (np.array([8.0, 0, 96.0]) - 5.0 * t_mat)
    truc = tam_can - 11.0 * n_mat  # khớp xoay của KY-023 nằm dưới mặt khoảng 11 mm
    phai = np.array([0.0, -1.0, 0.0])
    meta = [sang_unity(((truc - tam) / 1000)[None])[0], sang_unity(n_mat[None])[0], sang_unity(t_mat[None])[0], sang_unity(phai[None])[0]]

    buf = bytearray(b"VTC1")
    buf += struct.pack("<i", len(phan))
    for v in meta:
        buf += struct.pack("<3f", *v)
    buf += struct.pack("<f", THANG)
    tong_tg = 0
    for ma, vai, tg in phan:
        p, pt, idx = dinh_va_phap_tuyen(tg)
        p = sang_unity((p - tam) / 1000)
        pt = sang_unity(pt)
        idx = idx[:, [0, 2, 1]]  # đổi chiều quấn
        q = np.round(p / THANG).astype(np.int16)
        nq = np.clip(np.round(pt * 127), -127, 127).astype(np.int8)
        ten = canh[ma]["ten"].encode("utf-8")[:255]
        buf += struct.pack("<B", len(ten)) + ten + mau(canh[ma]["mau"]) + struct.pack("<B", vai)
        buf += struct.pack("<ii", len(p), idx.size)
        dinh = np.zeros(len(p), dtype=np.dtype([("p", "<i2", 3), ("n", "i1", 3)]))
        dinh["p"], dinh["n"] = q, nq
        buf += dinh.tobytes()
        buf += idx.astype("<u2" if len(p) < 65536 else "<u4").tobytes()
        tong_tg += len(idx)
        print(f"{ma:12s} {len(idx):6d} tam giác {len(p):6d} đỉnh  vai trò {vai}")
    os.makedirs(os.path.dirname(RA), exist_ok=True)
    with open(RA, "wb") as f:
        f.write(buf)
    co = tat_ca.max(0) - tat_ca.min(0)
    print(f"Tổng {tong_tg} tam giác, cỡ {co[0]:.0f} × {co[1]:.0f} × {co[2]:.0f} mm, tệp {len(buf) / 1024:.0f} KB -> {os.path.relpath(RA, GOC)}")
    if tam_thoi:
        shutil.rmtree(tam_thoi, ignore_errors=True)


if __name__ == "__main__":
    main()
