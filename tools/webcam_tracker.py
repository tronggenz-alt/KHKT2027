"""THỬ NGHIỆM (tầng nâng cấp 1): dùng webcam laptop theo dõi VỊ TRÍ kính và tay cầm.

Dán marker ArUco lên mặt trước kính VR Box (id 0) và đầu tay cầm (id 1). Webcam
đặt trước mặt người dùng. Chương trình ước lượng vị trí 3D từng marker và gửi qua
UDP cho Unity (UdpPoseReceiver.cs). Hướng xoay vẫn lấy từ IMU vì IMU nhanh và mượt
hơn; webcam chỉ bổ sung vị trí, thứ mà IMU không đo được.

    python tools/webcam_tracker.py --tao-marker          # tạo marker_0.png, marker_1.png để in
    python tools/webcam_tracker.py --cam 0 --co 0.05     # marker in cạnh 5 cm
    python tools/webcam_tracker.py --anh-thu anh.png     # thử trên một ảnh

Cần: pip install opencv-contrib-python numpy
Giới hạn đã biết: mất dấu khi marker quay nghiêng quá ~60° so với camera hoặc bị
nhòe do chuyển động nhanh; phòng tối thì nhiễu nhiều. Xem docs/01-de-xuat-dieu-khien.md.
"""

from __future__ import annotations

import argparse
import json
import math
import socket
import time

import cv2
import numpy as np

TEN = {0: "dau", 1: "tay"}
DICT = cv2.aruco.DICT_4X4_50


class OneEuro:
    """Bộ lọc One Euro: đứng yên thì lọc mạnh (hết rung), di chuyển nhanh thì lọc nhẹ (ít trễ)."""

    def __init__(self, min_cutoff: float = 1.0, beta: float = 0.5, d_cutoff: float = 1.0) -> None:
        self.min_cutoff, self.beta, self.d_cutoff = min_cutoff, beta, d_cutoff
        self.x = None
        self.dx = None
        self.t = None

    @staticmethod
    def _alpha(cutoff: float, dt: float) -> float:
        tau = 1.0 / (2 * math.pi * cutoff)
        return 1.0 / (1.0 + tau / dt)

    def __call__(self, x: np.ndarray, t: float) -> np.ndarray:
        if self.x is None:
            self.x, self.dx, self.t = x, np.zeros_like(x), t
            return x
        dt = max(1e-3, t - self.t)
        self.t = t
        dx = (x - self.x) / dt
        a_d = self._alpha(self.d_cutoff, dt)
        self.dx = a_d * dx + (1 - a_d) * self.dx
        cutoff = self.min_cutoff + self.beta * float(np.linalg.norm(self.dx))
        a = self._alpha(cutoff, dt)
        self.x = a * x + (1 - a) * self.x
        return self.x


def camera_matrix(w: int, h: int, hfov_deg: float) -> np.ndarray:
    """Ma trận camera gần đúng từ góc nhìn ngang (chưa hiệu chuẩn bàn cờ)."""
    f = (w / 2) / math.tan(math.radians(hfov_deg) / 2)
    return np.array([[f, 0, w / 2], [0, f, h / 2], [0, 0, 1]], dtype=np.float64)


def detect(gray: np.ndarray, detector, K: np.ndarray, size_m: float) -> dict[int, np.ndarray]:
    """Trả về {id: vị trí (x, y, z) trong hệ camera OpenCV, mét}."""
    s = size_m / 2
    obj = np.array([[-s, s, 0], [s, s, 0], [s, -s, 0], [-s, -s, 0]], dtype=np.float64)
    corners, ids, _ = detector.detectMarkers(gray)
    out: dict[int, np.ndarray] = {}
    if ids is None:
        return out
    for c, i in zip(corners, ids.flatten()):
        ok, rvec, tvec = cv2.solvePnP(obj, c.reshape(4, 2).astype(np.float64), K, None,
                                      flags=cv2.SOLVEPNP_IPPE_SQUARE)
        if ok:
            out[int(i)] = tvec.flatten()
    return out


def to_unity(p_cam: np.ndarray) -> list[float]:
    """Camera OpenCV (x phải, y xuống, z ra xa) -> Unity khi người dùng nhìn về phía camera."""
    return [float(-p_cam[0]), float(-p_cam[1]), float(-p_cam[2])]


def make_markers(px: int = 600) -> None:
    d = cv2.aruco.getPredefinedDictionary(DICT)
    for i, name in TEN.items():
        img = cv2.aruco.generateImageMarker(d, i, px)
        img = cv2.copyMakeBorder(img, px // 8, px // 8, px // 8, px // 8, cv2.BORDER_CONSTANT, value=255)
        cv2.imwrite(f"marker_{i}.png", img)
        print(f"marker_{i}.png -> dán lên {'kính' if name == 'dau' else 'tay cầm'}; in sao cho ô đen cạnh đúng --co")


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--cam", type=int, default=0)
    ap.add_argument("--co", type=float, default=0.05, help="cạnh ô đen của marker khi in (mét)")
    ap.add_argument("--hfov", type=float, default=65.0, help="góc nhìn ngang của webcam (độ)")
    ap.add_argument("--udp", default="127.0.0.1:9050", help="địa chỉ Unity nhận dữ liệu")
    ap.add_argument("--tao-marker", action="store_true")
    ap.add_argument("--anh-thu", help="chạy nhận diện trên một ảnh rồi thoát")
    ap.add_argument("--khong-hien", action="store_true", help="không mở cửa sổ xem trước")
    args = ap.parse_args()

    if args.tao_marker:
        make_markers()
        return

    detector = cv2.aruco.ArucoDetector(cv2.aruco.getPredefinedDictionary(DICT), cv2.aruco.DetectorParameters())

    if args.anh_thu:
        img = cv2.imread(args.anh_thu, cv2.IMREAD_GRAYSCALE)
        K = camera_matrix(img.shape[1], img.shape[0], args.hfov)
        for i, p in detect(img, detector, K, args.co).items():
            print(TEN.get(i, i), "cách camera", f"{np.linalg.norm(p):.3f} m", "unity", to_unity(p))
        return

    host, port = args.udp.split(":")
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    cap = cv2.VideoCapture(args.cam, cv2.CAP_DSHOW)
    cap.set(cv2.CAP_PROP_FPS, 60)
    filters = {i: OneEuro() for i in TEN}
    n, t0 = 0, time.time()
    while True:
        ok, frame = cap.read()
        if not ok:
            break
        t = time.time()
        gray = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)
        K = camera_matrix(gray.shape[1], gray.shape[0], args.hfov)
        for i, p in detect(gray, detector, K, args.co).items():
            if i not in TEN:
                continue
            p = filters[i](p, t)
            msg = {"id": TEN[i], "p": to_unity(p), "t": t}
            sock.sendto(json.dumps(msg).encode(), (host, int(port)))
            cv2.putText(frame, f"{TEN[i]} z={p[2]:.2f}m", (10, 30 + 30 * i), cv2.FONT_HERSHEY_SIMPLEX, 0.8, (0, 255, 0), 2)
        n += 1
        if t - t0 > 2:
            print(f"\r{n / (t - t0):.1f} khung/s", end="", flush=True)
            n, t0 = 0, t
        if not args.khong_hien:
            cv2.imshow("webcam_tracker (Esc de thoat)", frame)
            if cv2.waitKey(1) == 27:
                break
    cap.release()
    cv2.destroyAllWindows()


if __name__ == "__main__":
    main()
