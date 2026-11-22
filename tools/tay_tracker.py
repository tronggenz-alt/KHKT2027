"""Tracker bàn tay riêng cho VERA Lab, theo cách của hand_rects.py: nhẹ và mượt.

- Camera chạy luồng riêng, luôn lấy khung mới nhất (không xếp hàng).
- Chỉ chạy HandLandmarker (chế độ VIDEO, có bám theo) trên ảnh đã lật như gương, thu về PROC_W điểm ảnh.
- Gán trái / phải theo vị trí cũ; thao tác (tay_thao_tac.py) bỏ phiếu 5 khung.
- Lọc 1€ trên 21 điểm ảnh và 21 điểm 3D; gửi UDP cho Unity (mặc định cổng 9054, cùng gói với ArmTracking).

Chạy: python tools/tay_tracker.py [--cong 9054] [--cha <pid Unity>] [--hien]
"""
from __future__ import annotations

import argparse
import ctypes
import json
import math
import os
import socket
import sys
import threading
import time
from pathlib import Path

import cv2
import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
from mediapipe.tasks.python import BaseOptions, vision  # noqa: E402
import mediapipe as mp  # noqa: E402
from tay_thao_tac import BoPhieu, GanBen, phan_loai  # noqa: E402
from webcam_tracker import OneEuro  # noqa: E402

GOC = Path(__file__).resolve().parent
MO_HINH = GOC / "mo_hinh" / "hand_landmarker.task"
PROC_W = 320
CAP_W, CAP_H = 640, 480


class DocCamera:
    """Đọc camera ở luồng riêng, chỉ giữ khung mới nhất."""

    def __init__(self, chi_so: int):
        self.cap = cv2.VideoCapture(chi_so, cv2.CAP_DSHOW) if os.name == "nt" else cv2.VideoCapture(chi_so)
        if not self.cap.isOpened():
            self.cap = cv2.VideoCapture(chi_so)
        self.cap.set(cv2.CAP_PROP_FRAME_WIDTH, CAP_W)
        self.cap.set(cv2.CAP_PROP_FRAME_HEIGHT, CAP_H)
        self.cap.set(cv2.CAP_PROP_BUFFERSIZE, 1)
        self.khung = None
        self.khoa = threading.Lock()
        self.chay = True
        threading.Thread(target=self._vong, daemon=True).start()

    def _vong(self):
        while self.chay:
            ok, f = self.cap.read()
            if ok:
                with self.khoa:
                    self.khung = f
            else:
                time.sleep(0.005)

    def doc(self):
        with self.khoa:
            f, self.khung = self.khung, None
        return f

    def dong(self):
        self.chay = False
        time.sleep(0.05)
        self.cap.release()


def cha_con_song(pid: int) -> bool:
    if pid <= 0:
        return True
    if os.name == "nt":
        k = ctypes.windll.kernel32
        h = k.OpenProcess(0x1000, False, pid)
        if not h:
            return False
        ma = ctypes.c_ulong()
        k.GetExitCodeProcess(h, ctypes.byref(ma))
        k.CloseHandle(h)
        return ma.value == 259
    try:
        os.kill(pid, 0)
        return True
    except OSError:
        return False


def chon_camera(chi_dinh: str) -> int:
    if chi_dinh.isdigit():
        return int(chi_dinh)
    try:
        from chon_camera import chon_webcam_may
        return chon_webcam_may(chi_dinh)[0]
    except Exception:
        return 0


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--cong", type=int, default=9054)
    ap.add_argument("--cha", type=int, default=0)
    ap.add_argument("--cam", default="may")
    ap.add_argument("--hien", action="store_true")
    ap.add_argument("--thu", type=float, default=0.0, help="chạy N giây rồi in thống kê")
    a = ap.parse_args()

    tay = vision.HandLandmarker.create_from_options(vision.HandLandmarkerOptions(
        base_options=BaseOptions(model_asset_path=str(MO_HINH)),
        running_mode=vision.RunningMode.VIDEO, num_hands=2,
        min_hand_detection_confidence=0.6, min_hand_presence_confidence=0.5, min_tracking_confidence=0.5))
    cam = DocCamera(chon_camera(a.cam))
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    dich = ("127.0.0.1", a.cong)
    gan = GanBen()
    phieu = {"trai": BoPhieu(), "phai": BoPhieu()}
    chum = {"trai": False, "phai": False}
    loc_anh: dict[str, OneEuro] = {}
    loc_3d: dict[str, OneEuro] = {}
    ts_truoc, t_kiem, t0 = -1, 0.0, time.time()
    dem, thay, ms_tong = 0, 0, 0.0
    print(f"Tracker tay: gửi tới 127.0.0.1:{a.cong}", flush=True)
    try:
        while True:
            now = time.time()
            if a.cha and now - t_kiem > 1.0:
                t_kiem = now
                if not cha_con_song(a.cha):
                    break
            if a.thu and now - t0 > a.thu:
                break
            f = cam.doc()
            if f is None:
                time.sleep(0.002)
                continue
            t_bd = time.perf_counter()
            f = cv2.flip(f, 1)
            h0, w0 = f.shape[:2]
            nho = cv2.resize(f, (PROC_W, int(PROC_W * h0 / w0)), interpolation=cv2.INTER_AREA)
            img = mp.Image(image_format=mp.ImageFormat.SRGB, data=np.ascontiguousarray(cv2.cvtColor(nho, cv2.COLOR_BGR2RGB)))
            t = time.perf_counter()
            ts = max(int(t * 1000), ts_truoc + 1)
            ts_truoc = ts
            kq = tay.detect_for_video(img, ts)
            lms = kq.hand_landmarks or []
            wls = kq.hand_world_landmarks or []
            # bỏ phát hiện quá nhỏ (nhiễu)
            chon = [k for k, lm in enumerate(lms) if math.hypot(lm[9].x - lm[0].x, lm[9].y - lm[0].y) >= 0.04]
            ben = gan.gan([(lms[k][0].x, lms[k][0].y) for k in chon], now)
            goi = []
            for k, s in zip(chon, ben):
                if s not in ("trai", "phai"):
                    continue
                lm, wl = lms[k], (wls[k] if k < len(wls) else None)
                tt, chum[s] = phan_loai(lm, wl, chum[s], w0, h0)
                tt = phieu[s].them(tt)
                if s not in loc_anh:
                    loc_anh[s] = OneEuro(min_cutoff=1.0, beta=1.0)   # beta theo tọa độ 0..1: 20 thì nhiễu cũng mở hết bộ lọc
                    loc_3d[s] = OneEuro(min_cutoff=1.0, beta=4.0)
                anh = loc_anh[s](np.array([v for q in lm for v in (q.x, q.y, q.z)]), now)
                the_gioi = loc_3d[s](np.array([v for q in wl for v in (q.x, q.y, q.z)]), now) if wl is not None else np.zeros(63)
                # x, y cổ tay theo ảnh chưa lật (như mediapipe_tracker) để ArmRig dùng chung một quy ước
                goi.append({
                    "ben": s,
                    # 3D của ảnh đã lật: đổi dấu x để khớp quy ước gói cũ (ảnh chưa lật), ArmTracking sẽ đổi dấu cả ba trục
                    "w": [int(round(v * 1000)) * (-1 if j % 3 == 0 else 1) for j, v in enumerate(the_gioi)],
                    "x": int(round((1 - anh[0]) * 1000)), "y": int(round(anh[1] * 1000)),
                    "p": int(chum[s]), "g": tt,
                    "lm": [int(round(v * 1000)) for v in anh],
                })
            for s in ("trai", "phai"):
                if s not in ben:
                    phieu[s].xoa()
                    chum[s] = False
            sock.sendto(json.dumps({"t": round(now, 3), "tay": goi, "canh": []}, separators=(",", ":")).encode(), dich)
            dem += 1
            thay += 1 if goi else 0
            ms_tong += (time.perf_counter() - t_bd) * 1000
            if a.hien:
                for g in goi:
                    pts = g["lm"]
                    for j in range(21):
                        cv2.circle(f, (int(pts[j * 3] / 1000 * w0), int(pts[j * 3 + 1] / 1000 * h0)), 3, (0, 220, 255), -1)
                    cv2.putText(f, f"{g['ben']}: {g['g']}", (int(pts[0] / 1000 * w0), int(pts[1] / 1000 * h0) + 24),
                                cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 255, 0), 2)
                cv2.imshow("Tracker tay VERA", f)
                if cv2.waitKey(1) & 0xFF in (27, ord("q")):
                    break
    finally:
        tay.close()
        cam.dong()
        if a.hien:
            cv2.destroyAllWindows()
        giay = max(1e-3, time.time() - t0)
        print(f"Đã xử lý {dem} khung trong {giay:.0f} s ({dem / giay:.1f} khung/giây), thấy tay {100 * thay / max(1, dem):.0f}%, "
              f"xử lý trung bình {ms_tong / max(1, dem):.1f} ms/khung", flush=True)


if __name__ == "__main__":
    main()
