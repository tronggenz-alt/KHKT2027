"""Chương trình nhận diện theo dõi ĐẦU, TRÒNG MẮT và CỬ CHỈ TAY qua Google MediaPipe.

Gửi gói JSON qua UDP cho Unity (HeadTracking.cs và MediaPipeHandSource.cs, cổng 9051):
  - Hướng đầu: yaw, pitch, roll (độ)
  - Khoảng cách: dist_cm (ước lượng theo tròng mắt 11.7 mm), e (tỉ lệ hai mắt cũ)
  - Tròng mắt: gaze_x, gaze_y (-1..1, đã bù hướng đầu), blink_l, blink_r (0/1)
  - Bàn tay: danh sách hands [{ben, ok, x, y, co, pinch, cu_chi, tin}]
  - FPS và nguồn ("mp")

Các chế độ:
  --che-do muot      : ưu tiên FPS và độ trễ thấp nhất (mặc định)
  --che-do can-bang  : giảm tải cho máy yếu
  --che-do tiet-kiem : cho laptop chạy pin / máy rất yếu
  --khong-tay        : tắt nhận diện tay, chỉ theo dõi đầu
  --gpu              : bật GPU delegate (Mac Metal / Linux)
  --hien             : hiện cửa sổ xem trực tiếp kèm điểm mốc và HUD
  --thu N            : chạy N giây không cửa sổ, in thống kê chi tiết từng khâu
"""

from __future__ import annotations

import argparse
import json
import math
import os
import socket
import sys
import threading
import time
from pathlib import Path
from typing import Any, Optional

import cv2
import numpy as np
import psutil

# Đường dẫn mô-đun nội bộ
THU_MUC_GOC = Path(__file__).resolve().parent
sys.path.insert(0, str(THU_MUC_GOC))

from chon_camera import chon_webcam_may  # noqa: E402
from mediapipe_tinh import (  # noqa: E402
    BoLocChopMat,
    BoLocChopMatHaiLan,
    BoLocCuChi,
    tao_goi_udp,
    dong_goi_diem_moc,
    dong_goi_tay3d,
    gan_ben_theo_pose,
    mang_diem,
    CHI_SO_CANH_TAY,
    tinh_blendshapes,
    tinh_gaze,
    tinh_khoang_cach_iris,
    tinh_tay,
    tinh_tu_the_dau,
)
from webcam_tracker import OneEuro  # noqa: E402

import mediapipe as mp
from mediapipe.tasks import python as mp_python
from mediapipe.tasks.python import vision

MO_HINH_MAT = THU_MUC_GOC / "mo_hinh" / "face_landmarker.task"
MO_HINH_TAY = THU_MUC_GOC / "mo_hinh" / "gesture_recognizer.task"
MO_HINH_POSE = THU_MUC_GOC / "mo_hinh" / "pose_landmarker_lite.task"


def con_song(pid: int) -> bool:
    """Tiến trình cha (Unity) còn chạy không."""
    if pid <= 0:
        return True
    if os.name == "nt":
        import ctypes
        k = ctypes.windll.kernel32
        h = k.OpenProcess(0x1000, False, pid)
        if not h:
            return False
        code = ctypes.c_ulong()
        ok = k.GetExitCodeProcess(h, ctypes.byref(code))
        k.CloseHandle(h)
        return bool(ok) and code.value == 259
    try:
        os.kill(pid, 0)
        return True
    except OSError:
        return False


class LuongDocWebcam(threading.Thread):
    """Luồng đọc webcam liên tục vào bộ đệm 1 khung hình duy nhất."""

    def __init__(self, chi_so_cam: int, rong: int = 640, cao: int = 480):
        super().__init__(daemon=True, name="LuongDocWebcam")
        self.chi_so_cam = chi_so_cam
        self.rong = rong
        self.cao = cao
        self.cap: Optional[cv2.VideoCapture] = None
        self.dang_chay = False
        self.tam_dung_flag = False
        self.lock = threading.Lock()
        self.khung_moi: Optional[np.ndarray] = None
        self.ts_khung: float = 0.0
        self.so_khung_doc = 0
        self.fps_webcam_that = 0.0
        self._t0 = time.time()
        self.mo_camera()

    def mo_camera(self) -> bool:
        if sys.platform == "darwin":
            self.cap = cv2.VideoCapture(self.chi_so_cam, cv2.CAP_AVFOUNDATION)
        elif os.name == "nt":
            self.cap = cv2.VideoCapture(self.chi_so_cam, cv2.CAP_MSMF)
            if not self.cap.isOpened():
                self.cap = cv2.VideoCapture(self.chi_so_cam, cv2.CAP_DSHOW)
        else:
            self.cap = cv2.VideoCapture(self.chi_so_cam)

        if not self.cap.isOpened():
            return False

        self.cap.set(cv2.CAP_PROP_BUFFERSIZE, 1)
        self.cap.set(cv2.CAP_PROP_FRAME_WIDTH, self.rong)
        self.cap.set(cv2.CAP_PROP_FRAME_HEIGHT, self.cao)
        # Thử xin định dạng MJPG nếu webcam hỗ trợ
        try:
            fourcc = cv2.VideoWriter_fourcc(*"MJPG")
            self.cap.set(cv2.CAP_PROP_FOURCC, fourcc)
        except Exception:
            pass

        # Warmup đọc vài khung
        for _ in range(5):
            ok, _ = self.cap.read()
            if ok:
                break
            time.sleep(0.04)

        return True

    def dong_camera(self) -> None:
        if self.cap is not None:
            try:
                self.cap.release()
            except Exception:
                pass
            self.cap = None

    def run(self) -> None:
        self.dang_chay = True
        dem_cuc_bo = 0
        t_do_fps = time.time()

        while self.dang_chay:
            if self.tam_dung_flag:
                if self.cap is not None:
                    self.dong_camera()
                time.sleep(0.05)
                continue

            if self.cap is None:
                if not self.mo_camera():
                    time.sleep(0.2)
                    continue

            ok, f = self.cap.read()
            if not ok or f is None:
                time.sleep(0.01)
                continue

            now = time.time()
            with self.lock:
                self.khung_moi = f
                self.ts_khung = now
                self.so_khung_doc += 1

            dem_cuc_bo += 1
            if now - t_do_fps >= 1.0:
                self.fps_webcam_that = dem_cuc_bo / (now - t_do_fps)
                dem_cuc_bo = 0
                t_do_fps = now

    def lay_khung(self) -> tuple[Optional[np.ndarray], float]:
        with self.lock:
            return self.khung_moi, self.ts_khung

    def tam_dung(self) -> None:
        self.tam_dung_flag = True

    def tiep_tuc(self) -> None:
        self.tam_dung_flag = False

    def dung(self) -> None:
        self.dang_chay = False
        self.dong_camera()


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--cam", default="may", help="chọn camera ('may' = tự chọn webcam máy, hoặc số)")
    parser.add_argument("--cong", type=int, default=9051, help="cổng UDP gửi cho Unity (mặc định 9051)")
    parser.add_argument("--may", default="127.0.0.1", help="địa chỉ máy nhận UDP")
    parser.add_argument("--hien", action="store_true", help="hiện cửa sổ webcam xem trực tiếp")
    parser.add_argument("--thu", type=float, default=0.0, help="chạy N giây kiểm thử đo hiệu năng rồi in thống kê")
    parser.add_argument("--cha", type=int, default=0, help="pid Unity: Unity tắt thì tự thoát")
    parser.add_argument("--hfov", type=float, default=60.0, help="góc nhìn ngang của webcam (độ, mặc định 60.0)")
    parser.add_argument("--che-do", choices=["muot", "can-bang", "tiet-kiem"], default="muot", help="chế độ hiệu năng")
    parser.add_argument("--khong-tay", action="store_true", help="tắt nhận diện tay")
    parser.add_argument("--khong-pose", action="store_true", help="tắt Pose (vai, khuỷu cho cánh tay ảo)")
    parser.add_argument("--gpu", action="store_true", help="bật GPU delegate (Metal Mac / Linux)")
    parser.add_argument("--chop-2-lan", action="store_true", help="bật xác nhận bằng chớp mắt hai lần nhanh")
    parser.add_argument("--pose", action="store_true", help="bật ước lượng tư thế thân người Pose Landmarker (tắt mặc định)")
    args = parser.parse_args()

    # Nâng mức ưu tiên tiến trình trên Windows
    if os.name == "nt":
        try:
            p = psutil.Process()
            p.nice(psutil.ABOVE_NORMAL_PRIORITY_CLASS)
        except Exception:
            pass

    # Socket UDP gửi kết quả
    sock_gui = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock_gui.setblocking(False)
    dich_gui = (args.may, args.cong)
    dich_diem_moc = (args.may, args.cong + 2)  # chỉ gửi khi bảng F9 của Unity đang mở
    diem_moc_den = 0.0

    # Socket UDP nghe lệnh điều khiển (ví dụ phím F8 tắt/bật gửi lệnh tạm dừng)
    sock_lenh = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock_lenh.setblocking(False)
    try:
        sock_lenh.bind(("127.0.0.1", args.cong + 1))
    except Exception:
        pass

    # 1. Chọn camera máy tính hợp lệ
    chi_so_cam, ten_cam, thong_tin_cam = chon_webcam_may(args.cam)
    if chi_so_cam is None:
        # báo đúng lý do cho Unity (trước đây mọi lỗi khác "không thấy" đều bị báo nhầm là chặn camera điện thoại)
        loi = "da_chan_camera_dien_thoai" if thong_tin_cam == "da_chan_camera_dien_thoai" else ("khong_thay_webcam_may" if "khong_thay" in thong_tin_cam else thong_tin_cam)
        sock_gui.sendto(json.dumps({"ok": 0, "loi": loi}).encode("utf-8"), dich_gui)
        sys.exit(f"Từ chối mở camera: {thong_tin_cam} (tên camera: '{ten_cam}')")

    print(f"Đã chọn webcam: '{ten_cam}' (OpenCV index {chi_so_cam}) - {thong_tin_cam}", flush=True)

    # 2. Khởi tạo luồng webcam
    rong_mong_muon = 640 if args.che_do in ("muot", "can-bang") else 320
    cao_mong_muon = 480 if args.che_do in ("muot", "can-bang") else 240
    luong_cam = LuongDocWebcam(chi_so_cam, rong_mong_muon, cao_mong_muon)
    luong_cam.start()

    # 3. Cấu hình mô hình MediaPipe
    if not MO_HINH_MAT.exists() or not MO_HINH_TAY.exists():
        sys.exit("Thiếu tệp mô hình trong tools/mo_hinh/. Hãy chạy tools/tai_mo_hinh_mediapipe.py trước.")

    delegate = mp_python.BaseOptions.Delegate.GPU if args.gpu and sys.platform == "darwin" else mp_python.BaseOptions.Delegate.CPU
    base_face = mp_python.BaseOptions(model_asset_path=str(MO_HINH_MAT), delegate=delegate)
    co_blendshapes = (args.che_do != "tiet-kiem")

    face_options = vision.FaceLandmarkerOptions(
        base_options=base_face,
        output_face_blendshapes=co_blendshapes,
        output_facial_transformation_matrixes=True,
        num_faces=1,
        running_mode=vision.RunningMode.VIDEO,
    )
    face_landmarker = vision.FaceLandmarker.create_from_options(face_options)

    gesture_recognizer: Optional[vision.GestureRecognizer] = None
    if not args.khong_tay:
        so_tay = 1 if args.che_do == "tiet-kiem" else 2
        mo_hinh_tay_path = THU_MUC_GOC / "mo_hinh" / "gesture_recognizer_lab.task"
        if mo_hinh_tay_path.exists():
            print(f"Sử dụng mô hình cử chỉ tùy biến: {mo_hinh_tay_path.name}")
        else:
            mo_hinh_tay_path = MO_HINH_TAY
        base_hand = mp_python.BaseOptions(model_asset_path=str(mo_hinh_tay_path), delegate=delegate)
        gesture_options = vision.GestureRecognizerOptions(
            base_options=base_hand,
            num_hands=so_tay,
            min_hand_detection_confidence=0.5,
            min_hand_presence_confidence=0.5,
            min_tracking_confidence=0.5,
            running_mode=vision.RunningMode.VIDEO,
        )
        gesture_recognizer = vision.GestureRecognizer.create_from_options(gesture_options)

    # Pose bản nhẹ cho vai, khuỷu (cánh tay ảo). Tắt khi không nhận diện tay hoặc thiếu mô hình.
    pose_landmarker = None
    if not args.khong_tay and not args.khong_pose and MO_HINH_POSE.exists():
        pose_landmarker = vision.PoseLandmarker.create_from_options(vision.PoseLandmarkerOptions(
            base_options=mp_python.BaseOptions(model_asset_path=str(MO_HINH_POSE), delegate=delegate),
            num_poses=1,
            min_pose_detection_confidence=0.5,
            min_pose_presence_confidence=0.5,
            min_tracking_confidence=0.5,
            running_mode=vision.RunningMode.VIDEO,
        ))
    dich_tay3d = (args.may, args.cong + 3)
    # Bộ lọc 1€ cho điểm 3D: beta theo đơn vị mét. Đo trên dữ liệu giả lập: beta nhỏ làm trễ 80-125 ms,
    # beta lớn hơn giữ gần đủ biên độ khi tay chạy nhanh mà đứng yên vẫn hết rung.
    loc_tay3d: dict[str, OneEuro] = {}
    loc_canh: dict[str, OneEuro] = {}

    # 4. Bộ lọc One Euro và bộ lọc trạng thái
    loc_dau = OneEuro(min_cutoff=0.7, beta=0.03)          # yaw, pitch, roll
    loc_khoang_cach = OneEuro(min_cutoff=0.5, beta=0.01)  # dist_cm, e
    loc_gaze = OneEuro(min_cutoff=0.8, beta=0.05)         # gaze_x, gaze_y
    loc_tay: dict[str, OneEuro] = {}                      # ben -> OneEuro(x, y, co)
    loc_chop = BoLocChopMat(nguong_dong=0.45, nguong_mo=0.25)
    loc_chop_2lan = BoLocChopMatHaiLan() if args.chop_2_lan else None
    loc_cu_chi = BoLocCuChi(nguong_tin=0.6, thoi_gian_giu_s=0.150)
    trang_thai_pinch: dict[str, bool] = {"trai": False, "phai": False}

    # 5. Khởi động (Warmup) mô hình
    img_warmup = np.zeros((cao_mong_muon, rong_mong_muon, 4 if args.gpu else 3), dtype=np.uint8)
    fmt = mp.ImageFormat.SRGBA if args.gpu else mp.ImageFormat.SRGB
    mp_warmup = mp.Image(image_format=fmt, data=img_warmup)
    face_landmarker.detect_for_video(mp_warmup, 1)
    if gesture_recognizer:
        gesture_recognizer.recognize_for_video(mp_warmup, 1)

    print(f"MediaPipe Tracker sẵn sàng (Chế độ: {args.che_do}, GPU: {args.gpu}, Cổng UDP: {args.cong})", flush=True)

    # Thống kê hiệu năng
    t0_chay = time.time()
    so_khung_xu_ly = 0
    so_thay_mat = 0
    so_thay_tay = 0
    khung_bo_qua = 0

    thoi_gian_doc: list[float] = []
    thoi_gian_doi_mau: list[float] = []
    thoi_gian_mat: list[float] = []
    thoi_gian_tay: list[float] = []
    thoi_gian_loc: list[float] = []
    thoi_gian_gui: list[float] = []
    thoi_gian_tong: list[float] = []

    last_ts_khung = 0.0
    last_hand_seen_time = time.time()
    fps_nhan_dien = 0.0
    t_do_fps_nd = time.time()
    dem_nd = 0
    t_kiem_cha = time.time()
    last_active_time = time.time()
    dang_tam_dung = False
    t_khung_cuoi = time.time()   # lúc nhận khung hình gần nhất (để báo Unity khi webcam không lên hình)
    t_bao_loi = 0.0

    che_do_hien_tai = args.che_do
    process_info = psutil.Process()
    process_info.cpu_percent()  # lần gọi đầu luôn trả 0: mồi trước để số đo cuối có nghĩa
    ts_ms_truoc = 1             # dấu thời gian VIDEO mode phải tăng ngặt (khung khởi động dùng 1)
    ket_qua_tay_cu = None       # khung bỏ qua nhận diện tay thì dùng lại kết quả gần nhất (tránh tay nhấp nháy)

    try:
        while True:
            t_loop_start = time.perf_counter()
            now_real = time.time()

            # Kiểm tra tiến trình cha (Unity)
            if args.cha and now_real - t_kiem_cha >= 1.0:
                t_kiem_cha = now_real
                if not con_song(args.cha):
                    print("Tiến trình Unity đã đóng. Thoát MediaPipe Tracker.")
                    break

            # Kiểm tra lệnh UDP từ Unity (tạm dừng / tiếp tục)
            try:
                data_lenh, _ = sock_lenh.recvfrom(512)
                if data_lenh:
                    cmd = json.loads(data_lenh.decode("utf-8"))
                    lenh = cmd.get("lenh", "")
                    if lenh == "tam_dung":
                        dang_tam_dung = True
                        luong_cam.tam_dung()
                        print("[MediaPipe] Nhận lệnh tạm dừng: tắt webcam.")
                    elif lenh == "diem_moc_bat":
                        # Bảng F9 đang mở: Unity nhắc lại mỗi giây, thôi nhắc thì tự tắt
                        diem_moc_den = now_real + 3.0
                    elif lenh in ("tiep_tuc", "chay"):
                        dang_tam_dung = False
                        luong_cam.tiep_tuc()
                        last_active_time = now_real
                        print("[MediaPipe] Nhận lệnh tiếp tục: bật lại webcam.")
            except (BlockingIOError, socket.error):
                pass
            except Exception:
                pass

            if dang_tam_dung:
                if now_real - last_active_time > 60.0:
                    print("Tạm dừng quá 60s không có hoạt động. Tự thoát để giải phóng tài nguyên.")
                    break
                time.sleep(0.04)
                continue

            last_active_time = now_real

            # Lấy khung hình mới nhất từ luồng đọc
            t_doc_start = time.perf_counter()
            khung, ts_khung = luong_cam.lay_khung()
            t_doc_end = time.perf_counter()

            if khung is None or ts_khung == last_ts_khung:
                # webcam bị macOS chặn quyền / đang bận: không treo im lặng mà báo Unity mỗi giây, --thu vẫn kết thúc đúng hạn
                if now_real - t_khung_cuoi > 3.0 and now_real - t_bao_loi > 1.0:
                    t_bao_loi = now_real
                    try:
                        sock_gui.sendto(json.dumps({"ok": 0, "loi": "webcam_khong_len_hinh", "t": round(now_real, 3), "nguon": "mp"}).encode("utf-8"), dich_gui)
                    except Exception:
                        pass
                    if luong_cam.so_khung_doc == 0:
                        print("Chưa nhận được khung hình nào từ webcam (kiểm tra quyền Camera trong Cài đặt hệ thống).", flush=True)
                if args.thu > 0 and (now_real - t0_chay) >= args.thu:
                    break
                time.sleep(0.002)
                continue
            t_khung_cuoi = now_real

            # Bỏ khung cũ nếu có
            if last_ts_khung > 0 and (ts_khung - last_ts_khung) > 0.10:
                khung_bo_qua += 1
            last_ts_khung = ts_khung
            so_khung_xu_ly += 1

            t_doc_ms = (t_doc_end - t_doc_start) * 1000

            # Thu nhỏ ảnh về bề rộng mục tiêu
            t_doi_start = time.perf_counter()
            h_raw, w_raw = khung.shape[:2]
            w_target = 640 if che_do_hien_tai == "muot" else (480 if che_do_hien_tai == "can-bang" else 320)
            if w_raw != w_target:
                scale = w_target / w_raw
                khung_nho = cv2.resize(khung, (w_target, int(round(h_raw * scale))))
            else:
                khung_nho = khung

            if args.gpu:
                rgb_or_rgba = cv2.cvtColor(khung_nho, cv2.COLOR_BGR2RGBA)
                mp_img = mp.Image(image_format=mp.ImageFormat.SRGBA, data=np.ascontiguousarray(rgb_or_rgba))
            else:
                rgb_or_rgba = cv2.cvtColor(khung_nho, cv2.COLOR_BGR2RGB)
                mp_img = mp.Image(image_format=mp.ImageFormat.SRGB, data=np.ascontiguousarray(rgb_or_rgba))

            t_doi_ms = (time.perf_counter() - t_doi_start) * 1000

            # Đồng hồ đơn điệu: time.time() có thể lùi khi đồng bộ giờ, MediaPipe sẽ báo lỗi dấu thời gian không tăng
            ts_ms = max(ts_ms_truoc + 1, int(time.monotonic() * 1000))
            ts_ms_truoc = ts_ms

            # Chạy song song FaceLandmarker và GestureRecognizer
            res_face = [None]
            res_hand = [None]
            res_pose = [None]
            t_mat_ms = 0.0
            t_tay_ms = 0.0

            def _chay_mat():
                nonlocal t_mat_ms
                t_m0 = time.perf_counter()
                res_face[0] = face_landmarker.detect_for_video(mp_img, ts_ms)
                t_mat_ms = (time.perf_counter() - t_m0) * 1000

            def _chay_tay():
                nonlocal t_tay_ms
                if gesture_recognizer is None:
                    return
                # Tối ưu: nếu không thấy tay > 1s, chạy nửa tần số (mỗi 2 khung chạy 1)
                chay_nua = (now_real - last_hand_seen_time > 1.0) or (che_do_hien_tai == "tiet-kiem")
                if chay_nua and (so_khung_xu_ly % 2 != 0):
                    res_hand[0] = ket_qua_tay_cu
                    return
                t_t0 = time.perf_counter()
                res_hand[0] = gesture_recognizer.recognize_for_video(mp_img, ts_ms)
                t_tay_ms = (time.perf_counter() - t_t0) * 1000

            th_mat = threading.Thread(target=_chay_mat)
            def _chay_pose():
                if pose_landmarker is not None:
                    res_pose[0] = pose_landmarker.detect_for_video(mp_img, ts_ms)

            th_pose = threading.Thread(target=_chay_pose)
            th_pose.start()
            th_tay = threading.Thread(target=_chay_tay)
            th_mat.start()
            th_tay.start()
            th_mat.join()
            th_tay.join()
            th_pose.join()
            ket_qua_tay_cu = res_hand[0]

            t_loc_start = time.perf_counter()

            # 6. Xử lý kết quả khuôn mặt
            co_mat = False
            lm_mat = None
            yaw, pitch, roll, dist_z = 0.0, 0.0, 0.0, 0.0
            dist_cm, e = 0.0, 0.0
            gaze_x, gaze_y = 0.0, 0.0
            blink_l, blink_r = 0, 0
            xac_nhan_val = 0  # phải có giá trị cả khi không thấy mặt, nếu không gói UDP đầu tiên làm chương trình sập

            face_output = res_face[0]
            if face_output and face_output.face_landmarks:
                co_mat = True
                so_thay_mat += 1
                lm_mat = face_output.face_landmarks[0]

                # Hướng đầu từ ma trận 4x4
                if face_output.facial_transformation_matrixes:
                    mat_4x4 = face_output.facial_transformation_matrixes[0]
                    yaw, pitch, roll, dist_z = tinh_tu_the_dau(mat_4x4)

                # Khoảng cách tròng mắt và e
                d_iris, e_raw = tinh_khoang_cach_iris(lm_mat, hfov_deg=args.hfov)
                # Bù góc quay yaw cho e kiểu cũ
                e = e_raw / max(1e-6, math.cos(math.radians(yaw)))
                # Ưu tiên dist_iris kết hợp z ổn định
                dist_cm = d_iris if d_iris > 0 else dist_z

                # Hướng nhìn gaze
                gaze_x, gaze_y = tinh_gaze(lm_mat, yaw_deg=yaw, pitch_deg=pitch)

                # Chớp mắt và hướng nhìn từ blendshapes
                if face_output.face_blendshapes:
                    g_bs_x, g_bs_y, bl_bs, br_bs = tinh_blendshapes(
                        face_output.face_blendshapes[0],
                        yaw_deg=yaw,
                        pitch_deg=pitch,
                    )
                    # Kết hợp trích xuất tròng mắt hình học và blendshapes (50/50)
                    gaze_x = float(0.5 * gaze_x + 0.5 * g_bs_x)
                    gaze_y = float(0.5 * gaze_y + 0.5 * g_bs_y)
                    blink_l, blink_r = loc_chop.cap_nhat(float(bl_bs), float(br_bs))

                    if loc_chop_2lan is not None and loc_chop_2lan.cap_nhat(blink_l, blink_r, now_real):
                        xac_nhan_val = 1

                # Lọc One Euro cho các đại lượng đầu
                yaw, pitch, roll = (float(v) for v in loc_dau(np.array([yaw, pitch, roll]), now_real))
                dist_cm, e = (float(v) for v in loc_khoang_cach(np.array([dist_cm, e]), now_real))
                gaze_x, gaze_y = (float(v) for v in loc_gaze(np.array([gaze_x, gaze_y]), now_real))

            # 7. Xử lý kết quả bàn tay
            hands_list: list[dict[str, Any]] = []
            tay_tho: list[tuple[str, Any]] = []
            hand_output = res_hand[0]
            pose_out = res_pose[0]
            pose_anh = pose_out.pose_landmarks[0] if pose_out and pose_out.pose_landmarks else None
            pose_tg = pose_out.pose_world_landmarks[0] if pose_out and pose_out.pose_world_landmarks else None
            ben_pose = None
            if hand_output and hand_output.hand_landmarks:
                ben_pose = gan_ben_theo_pose([(float(h[0].x), float(h[0].y)) for h in hand_output.hand_landmarks], pose_anh)
            tay3d: list[dict[str, Any]] = []
            if hand_output and hand_output.hand_landmarks:
                last_hand_seen_time = now_real
                so_thay_tay += 1
                for idx_tay, lm_tay in enumerate(hand_output.hand_landmarks):
                    # Xác định tay trái / phải
                    ben = "phai"
                    if idx_tay < len(hand_output.handedness):
                        label = hand_output.handedness[idx_tay][0].category_name.lower()
                        # MediaPipe gán nhãn theo góc nhìn người trong ảnh
                        ben = "phai" if "right" in label else "trai"

                    if ben_pose:
                        ben = ben_pose[idx_tay]
                    tay_tho.append((ben, lm_tay))
                    if idx_tay < len(hand_output.hand_world_landmarks):
                        if ben not in loc_tay3d:
                            loc_tay3d[ben] = OneEuro(min_cutoff=1.0, beta=4.0)
                        tay3d.append({"ben": ben, "w": loc_tay3d[ben](mang_diem(hand_output.hand_world_landmarks[idx_tay]), now_real),
                                      "x": float(lm_tay[0].x), "y": float(lm_tay[0].y)})
                    prev_p = trang_thai_pinch.get(ben, False)
                    x_t, y_t, co_t, pinch_val, r_pinch = tinh_tay(lm_tay, prev_pinch=prev_p)
                    trang_thai_pinch[ben] = bool(pinch_val)

                    # Lọc One Euro bàn tay
                    if ben not in loc_tay:
                        loc_tay[ben] = OneEuro(min_cutoff=1.0, beta=0.05)
                    x_t, y_t, co_t = (float(v) for v in loc_tay[ben](np.array([x_t, y_t, co_t]), now_real))

                    # Cử chỉ
                    cu_chi_ten = ""
                    tin_c_chi = 0.0
                    if idx_tay < len(hand_output.gestures) and hand_output.gestures[idx_tay]:
                        g_top = hand_output.gestures[idx_tay][0]
                        cu_chi_ten = g_top.category_name
                        tin_c_chi = float(g_top.score)

                    cu_chi_chot = loc_cu_chi.cap_nhat(ben, cu_chi_ten, tin_c_chi, now_real)

                    hands_list.append({
                        "ben": ben,
                        "ok": 1,
                        "x": round(x_t, 3),
                        "y": round(y_t, 3),
                        "co": round(co_t, 3),
                        "pinch": int(pinch_val),
                        "cu_chi": cu_chi_chot,
                        "tin": round(tin_c_chi, 2),
                    })

            t_loc_ms = (time.perf_counter() - t_loc_start) * 1000

            # 8. Đóng gói và gửi UDP
            t_gui_start = time.perf_counter()
            goi = tao_goi_udp(
                ok=1 if co_mat else 0,
                yaw=yaw,
                pitch=pitch,
                roll=roll,
                e=e,
                dist_cm=dist_cm,
                gaze_x=gaze_x,
                gaze_y=gaze_y,
                blink_l=blink_l,
                blink_r=blink_r,
                fps=fps_nhan_dien,
                hands=hands_list,
                t=now_real,
                nguon="mp",
                xac_nhan=xac_nhan_val,
            )
            data_bytes = json.dumps(goi).encode("utf-8")
            try:
                sock_gui.sendto(data_bytes, dich_gui)
                # cánh tay ảo: chụm, cử chỉ lấy từ gói chính để hai nơi dùng cùng một kết quả
                for h in tay3d:
                    g = next((x for x in hands_list if x["ben"] == h["ben"]), None)
                    if g:
                        h["p"], h["g"] = g["pinch"], g["cu_chi"]
                canh = []
                if pose_tg is not None and pose_anh is not None:
                    for ben, ids in CHI_SO_CANH_TAY.items():
                        if ben not in loc_canh:
                            loc_canh[ben] = OneEuro(min_cutoff=0.7, beta=6.0)
                        canh.append({"ben": ben, "d": loc_canh[ben](mang_diem(pose_tg, ids), now_real),
                                     "tin": [float(getattr(pose_anh[i], "visibility", 1.0) or 0.0) for i in ids]})
                if tay3d or canh:
                    sock_gui.sendto(json.dumps(dong_goi_tay3d(tay3d, canh, now_real), separators=(",", ":")).encode("utf-8"), dich_tay3d)
                if now_real < diem_moc_den:
                    goi_moc = dong_goi_diem_moc(lm_mat, tay_tho, w_raw / max(1, h_raw), now_real)
                    sock_gui.sendto(json.dumps(goi_moc, separators=(",", ":")).encode("utf-8"), dich_diem_moc)
            except Exception:
                pass
            t_gui_ms = (time.perf_counter() - t_gui_start) * 1000

            t_tong_ms = (time.perf_counter() - t_loop_start) * 1000

            # Cập nhật thống kê
            dem_nd += 1
            if now_real - t_do_fps_nd >= 1.0:
                fps_nhan_dien = dem_nd / (now_real - t_do_fps_nd)
                dem_nd = 0
                t_do_fps_nd = now_real

                # Tự động hạ chế độ khi chính khâu nhận diện chậm (fps nhận diện thấp hơn hẳn fps webcam).
                # Webcam tự hạ 15 fps lúc thiếu sáng thì không hạ chế độ: máy vẫn xử lý kịp.
                cham_hon_webcam = fps_nhan_dien + 5.0 < luong_cam.fps_webcam_that
                if che_do_hien_tai == "muot" and fps_nhan_dien < 25.0 and cham_hon_webcam and so_khung_xu_ly > 30 and args.thu == 0:
                    che_do_hien_tai = "can-bang"
                    print(f"⚠️ FPS nhận diện đạt {fps_nhan_dien:.1f} < 25. Tự động hạ xuống chế độ 'can-bang'.", flush=True)

            thoi_gian_doc.append(t_doc_ms)
            thoi_gian_doi_mau.append(t_doi_ms)
            thoi_gian_mat.append(t_mat_ms)
            thoi_gian_tay.append(t_tay_ms)
            thoi_gian_loc.append(t_loc_ms)
            thoi_gian_gui.append(t_gui_ms)
            thoi_gian_tong.append(t_tong_ms)

            # 9. Hiển thị trực quan (--hien)
            if args.hien:
                h_k, w_k = khung.shape[:2]
                if co_mat:
                    cv2.putText(khung, f"Yaw: {yaw:+.1f}  Pitch: {pitch:+.1f}  Roll: {roll:+.1f}", (15, 30), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 255, 0), 2)
                    cv2.putText(khung, f"Khoang cach: {dist_cm:.1f} cm  Gaze: ({gaze_x:+.2f}, {gaze_y:+.2f})", (15, 60), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 255, 255), 2)
                else:
                    cv2.putText(khung, "Khong thay mat", (15, 30), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 0, 255), 2)

                cv2.putText(khung, f"FPS webcam: {luong_cam.fps_webcam_that:.1f}  FPS AI: {fps_nhan_dien:.1f}", (15, h_k - 20), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (200, 200, 200), 2)

                for h_item in hands_list:
                    hx, hy = int(h_item["x"] * w_k), int(h_item["y"] * h_k)
                    p_txt = " [PINCH]" if h_item["pinch"] else ""
                    cv2.putText(khung, f"{h_item['ben']}: {h_item['cu_chi']}{p_txt}", (max(10, hx - 50), max(20, hy - 20)), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (255, 100, 100), 2)
                    cv2.circle(khung, (hx, hy), 6, (0, 255, 0) if h_item["pinch"] else (0, 0, 255), -1)

                cv2.imshow("VERA Lab - MediaPipe Tracker (Esc de thoat)", khung)
                if cv2.waitKey(1) == 27:
                    break

            # Kiểm tra thời gian chạy thử (--thu N)
            if args.thu > 0 and (now_real - t0_chay) >= args.thu:
                break

    finally:
        luong_cam.dung()
        face_landmarker.close()
        if gesture_recognizer:
            gesture_recognizer.close()
        sock_gui.close()
        sock_lenh.close()
        if args.hien:
            cv2.destroyAllWindows()

    def tk(ds: list[float]) -> str:
        return f"trung bình {np.mean(ds):.2f} ms | p95 {np.percentile(ds, 95):.2f} ms" if ds else "không có số liệu (chưa nhận khung hình)"

    # In báo cáo nếu có --thu
    if args.thu > 0:
        tong_s = max(1e-6, time.time() - t0_chay)
        cpu_pct = process_info.cpu_percent()
        print("\n================== KẾT QUẢ THỬ NGHIỆM MEDIAPIPE TRACKER ==================")
        print(f"Thời gian: {tong_s:.2f} s | Chế độ: {che_do_hien_tai} | Camera: {ten_cam}")
        print(f"FPS webcam thật: {luong_cam.so_khung_doc / tong_s:.1f} fps (đọc được {luong_cam.so_khung_doc} khung)")
        print(f"FPS nhận diện:   {so_khung_xu_ly / tong_s:.1f} fps (xử lý {so_khung_xu_ly} khung, bỏ qua {khung_bo_qua} khung)")
        print(f"Tỉ lệ thấy mặt:  {so_thay_mat}/{so_khung_xu_ly} ({so_thay_mat / max(1, so_khung_xu_ly) * 100:.1f}%)")
        print(f"Tỉ lệ thấy tay:  {so_thay_tay}/{so_khung_xu_ly} ({so_thay_tay / max(1, so_khung_xu_ly) * 100:.1f}%)")
        print(f"CPU tiến trình:  {cpu_pct:.1f}%")
        print("------------------ THỜI GIAN TỪNG KHÂU (ms) ------------------")
        print(f"  • Đọc ảnh (camera):  {tk(thoi_gian_doc)}")
        print(f"  • Đổi màu & resize:  {tk(thoi_gian_doi_mau)}")
        print(f"  • Mặt (FaceLand):    {tk(thoi_gian_mat)}")
        print(f"  • Tay (GestureRec):  {tk(thoi_gian_tay)}")
        print(f"  • Lọc rung OneEuro:  {tk(thoi_gian_loc)}")
        print(f"  • Gửi gói UDP:       {tk(thoi_gian_gui)}")
        print(f"  • TỔNG KHÂU XỬ LÝ:   {tk(thoi_gian_tong)}")
        print("========================================================================\n")


if __name__ == "__main__":
    main()
