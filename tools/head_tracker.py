"""Điều khiển góc nhìn bằng ĐẦU qua webcam (không cần dán marker).

Nhận diện khuôn mặt bằng YuNet (cv2.FaceDetectorYN, mô hình tools/mo_hinh/face_detection_yunet_2023mar.onnx),
lấy 5 điểm: 2 mắt, mũi, 2 khóe miệng. Từ đó ước lượng:
  • yaw  (độ, dương = người dùng quay đầu sang PHẢI của họ): mũi lệch khỏi giữa hai mắt;
  • pitch (thô, chưa trừ tư thế giữa): mũi thấp / cao so với đường hai mắt;
  • e    (khoảng cách hai mắt / bề rộng ảnh, đã bù góc quay): đầu càng gần webcam thì e càng lớn.
Lọc rung bằng bộ lọc One Euro rồi gửi JSON qua UDP cho Unity (HeadTracking.cs, cổng 9051).
Unity tự căn tư thế giữa (nhìn thẳng 2 giây) và đổi ra: quay đầu → xoay góc nhìn, ghé gần → phóng to.

    python tools/head_tracker.py --hien            # tự chạy, có cửa sổ xem webcam + khung mặt (để thử)
    python tools/head_tracker.py --thu 8           # chạy 8 giây không cửa sổ, in thống kê (không lưu ảnh)
    (Unity tự chạy: python tools/head_tracker.py --cong 9051 --cha <pid Unity>)

Cần: opencv-python ≥ 4.8 (có FaceDetectorYN), numpy.
Giới hạn: quay đầu quá ~45° hoặc che mặt thì mất dấu (gửi ok=0); phòng quá tối nhận diện kém.
"""

from __future__ import annotations

import argparse
import json
import math
import os
import socket
import sys
import time
from pathlib import Path

import cv2
import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
from webcam_tracker import OneEuro  # noqa: E402  (bộ lọc dùng chung với bản theo dõi marker)

MO_HINH = Path(__file__).resolve().parent / "mo_hinh" / "face_detection_yunet_2023mar.onnx"
# Mũi nhô trước đường hai mắt khoảng 0,45 lần khoảng cách hai mắt: quay đầu θ thì mũi lệch r = 0,45·tanθ (theo khoảng cách hai mắt)
NHO_MUI = 0.45
RONG_NHAN = 320  # bề rộng ảnh đưa vào YuNet (thu nhỏ cho nhanh; các đại lượng đều chia theo bề rộng nên không đổi)


def tu_the(diem: np.ndarray, rong_anh: float) -> tuple[float, float, float]:
    """5 điểm YuNet [mắt phải, mắt trái, mũi, khóe miệng phải, khóe miệng trái] (x, y ảnh, chưa lật)
    → (yaw độ, pitch thô, e). "Phải / trái" là của người trong ảnh: mắt phải của họ nằm bên TRÁI ảnh."""
    mp, mt, mui = diem[0], diem[1], diem[2]
    v = mt - mp                      # từ mắt phải sang mắt trái của người: hướng +x ảnh khi nhìn thẳng
    d = float(np.hypot(*v))
    if d < 1e-6:
        return 0.0, 0.0, 0.0
    nghieng = math.atan2(v[1], v[0])  # đầu nghiêng (roll): xoay ngược để đo trong hệ của khuôn mặt
    c, s = math.cos(-nghieng), math.sin(-nghieng)
    o = mui - (mp + mt) / 2
    ox, oy = c * o[0] - s * o[1], s * o[0] + c * o[1]
    # quay đầu sang phải của người dùng: mũi dịch về bên phải của họ = bên trái ảnh (ox âm)
    yaw = -math.degrees(math.atan2(ox / d, NHO_MUI))
    pitch = oy / d                   # mũi dưới đường mắt (dương); cúi xuống thì tăng, ngẩng lên thì giảm
    e = d / max(1e-6, math.cos(math.radians(yaw))) / rong_anh
    return yaw, pitch, e


def con_song(pid: int) -> bool:
    """Tiến trình cha (Unity) còn chạy không. Windows: OpenProcess + GetExitCodeProcess (không dùng os.kill)."""
    if pid <= 0:
        return True
    if os.name == "nt":
        import ctypes
        k = ctypes.windll.kernel32
        h = k.OpenProcess(0x1000, False, pid)  # PROCESS_QUERY_LIMITED_INFORMATION
        if not h:
            return False
        code = ctypes.c_ulong()
        ok = k.GetExitCodeProcess(h, ctypes.byref(code))
        k.CloseHandle(h)
        return bool(ok) and code.value == 259  # STILL_ACTIVE
    try:
        os.kill(pid, 0)
        return True
    except OSError:
        return False


def mo_camera(chi_so: int) -> cv2.VideoCapture:
    cap = cv2.VideoCapture(chi_so, cv2.CAP_DSHOW) if os.name == "nt" else cv2.VideoCapture(chi_so)
    if not cap.isOpened():
        cap = cv2.VideoCapture(chi_so)
    cap.set(cv2.CAP_PROP_FRAME_WIDTH, 640)
    cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 480)
    return cap


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--cam", type=int, default=0, help="số thứ tự webcam")
    ap.add_argument("--cong", type=int, default=9051, help="cổng UDP gửi cho Unity")
    ap.add_argument("--may", default="127.0.0.1")
    ap.add_argument("--hien", action="store_true", help="hiện cửa sổ webcam kèm khung mặt và số đo")
    ap.add_argument("--thu", type=float, default=0, help="chạy N giây không cửa sổ rồi in thống kê")
    ap.add_argument("--cha", type=int, default=0, help="pid Unity: Unity tắt thì chương trình tự thoát")
    a = ap.parse_args()

    if not MO_HINH.exists():
        sys.exit(f"Thiếu mô hình {MO_HINH} (xem tools/mo_hinh/NGUON.txt)")
    cap = mo_camera(a.cam)
    if not cap.isOpened():
        sys.exit(f"Không mở được webcam số {a.cam}")
    ok, khung = cap.read()
    if not ok:
        sys.exit("Webcam không trả về hình")
    h0, w0 = khung.shape[:2]
    ty_le = RONG_NHAN / w0
    nho = (RONG_NHAN, int(round(h0 * ty_le)))
    det = cv2.FaceDetectorYN.create(str(MO_HINH), "", nho, score_threshold=0.75)
    loc = OneEuro(min_cutoff=0.7, beta=0.02)  # lọc mạnh hơn khi đầu đứng yên (bớt rung góc quay)

    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    dich = (a.may, a.cong)
    t0 = time.time()
    so_khung = so_thay = 0
    gan_day: list[tuple[float, float, float]] = []
    kiem_cha = 0.0
    print(f"Đang theo dõi đầu: webcam {a.cam} {w0}×{h0}, gửi UDP {dich[0]}:{dich[1]}", flush=True)
    while True:
        ok, khung = cap.read()
        if not ok:
            time.sleep(0.05)
            continue
        now = time.time()
        so_khung += 1
        _, mat = det.detect(cv2.resize(khung, nho))
        goi = {"ok": 0, "t": now}
        if mat is not None and len(mat):
            m = max(mat, key=lambda r: r[2] * r[3])  # mặt to nhất (người ngồi trước máy)
            diem = m[4:14].reshape(5, 2).astype(np.float64)
            yaw, pitch, e = tu_the(diem, RONG_NHAN)
            yaw, pitch, e = (float(x) for x in loc(np.array([yaw, pitch, e]), now))
            so_thay += 1
            gan_day.append((yaw, pitch, e))
            goi = {"ok": 1, "yaw": round(yaw, 2), "pitch": round(pitch, 4), "e": round(e, 5),
                   "x": round(float(m[0] + m[2] / 2) / RONG_NHAN, 4), "y": round(float(m[1] + m[3] / 2) / nho[1], 4),
                   "diem": round(float(m[14]), 3), "t": now}
            if a.hien:
                x, y, w, h = (m[:4] / ty_le).astype(int)
                cv2.rectangle(khung, (x, y), (x + w, y + h), (0, 200, 0), 2)
                for p in (diem / ty_le).astype(int):
                    cv2.circle(khung, tuple(p), 3, (0, 0, 255), -1)
                cv2.putText(khung, f"yaw {yaw:+.0f}  pitch {pitch:+.2f}  e {e:.3f}", (10, 25), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 255, 255), 2)
        sock.sendto(json.dumps(goi).encode("utf-8"), dich)

        if a.hien:
            cv2.imshow("Theo doi dau (Esc de thoat)", khung)
            if cv2.waitKey(1) == 27:
                break
        if a.thu and now - t0 >= a.thu:
            break
        if a.cha and now - kiem_cha > 1.0:
            kiem_cha = now
            if not con_song(a.cha):
                break

    cap.release()
    if a.hien:
        cv2.destroyAllWindows()
    if a.thu:
        dt = max(1e-6, time.time() - t0)
        print(f"{so_khung} khung trong {dt:.1f} s ({so_khung / dt:.1f} khung/s), thấy mặt {so_thay}/{so_khung} khung")
        if gan_day:
            arr = np.array(gan_day)
            print(f"yaw trung bình {arr[:, 0].mean():+.1f}° (dao động {arr[:, 0].std():.2f}°), "
                  f"pitch thô {arr[:, 1].mean():+.3f}, e {arr[:, 2].mean():.4f} (dao động {arr[:, 2].std() / max(1e-9, arr[:, 2].mean()) * 100:.1f}%)")


if __name__ == "__main__":
    main()
