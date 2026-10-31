"""Kiểm thử phần tính toán thuần MediaPipe (không cần webcam).

Theo phong cách tests/test_head_tracker.py:
- ma trận quay đã biết -> yaw / pitch / roll đúng (±1°), đúng dấu;
- tròng mắt lệch trái / phải / trên / dưới -> gaze đúng chiều; nhìn thẳng -> ≈ 0;
- đường kính tròng mắt giảm một nửa -> khoảng cách gấp đôi;
- bàn tay giả: chụm / mở -> pinch bật / tắt đúng (hysteresis), không nhấp nháy quanh ngưỡng;
- cử chỉ chỉ xuất hiện sau 150 ms giữ liên tục;
- gói JSON mới vẫn có đủ trường cũ, kiểu đúng.
"""

from __future__ import annotations

import math
import sys
import unittest
from unittest import mock
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
import mediapipe_tinh as mt  # noqa: E402
import chon_camera as cc  # noqa: E402


def tao_ma_tran_quay_gia(
    yaw_deg: float = 0.0,
    pitch_deg: float = 0.0,
    roll_deg: float = 0.0,
    dist_z_cm: float = 60.0,
) -> np.ndarray:
    """Tạo ma trận 4x4 từ góc Euler và khoảng cách Z."""
    y = math.radians(yaw_deg)
    p = math.radians(pitch_deg)
    r = math.radians(roll_deg)

    # Ry: quay quanh trục Y (yaw)
    ry = np.array([
        [math.cos(y), 0, math.sin(y)],
        [0, 1, 0],
        [-math.sin(y), 0, math.cos(y)],
    ])
    # Rx: quay quanh trục X (pitch)
    rx = np.array([
        [1, 0, 0],
        [0, math.cos(p), -math.sin(p)],
        [0, math.sin(p), math.cos(p)],
    ])
    # Rz: quay quanh trục Z (roll)
    rz = np.array([
        [math.cos(r), -math.sin(r), 0],
        [math.sin(r), math.cos(r), 0],
        [0, 0, 1],
    ])
    # Tương tự RQDecomp3x3: R = Rz @ Ry @ Rx hoặc tích
    rot = rz @ ry @ rx
    mat = np.eye(4, dtype=np.float64)
    mat[:3, :3] = rot
    mat[2, 3] = -abs(dist_z_cm)
    return mat


def tao_diem_mat_gia(
    gaze_huong: str = "thang",  # "thang", "phai", "trai", "tren", "duoi"
    duong_kinh_ty_le: float = 1.0,
) -> np.ndarray:
    """Tạo danh sách 478 điểm mặt giả với các chỉ số mắt và tròng mắt chuẩn."""
    # 478 điểm (x, y, z)
    pts = np.zeros((478, 3), dtype=np.float64)

    # Mắt phải người dùng (bên trái ảnh)
    pts[mt.MAT_PHAI_NGOAI] = [0.40, 0.45, 0.0]  # x_ngoai = 0.40
    pts[mt.MAT_PHAI_TRONG] = [0.48, 0.45, 0.0]  # x_trong = 0.48 -> mid_x = 0.44, w_half = 0.04
    pts[mt.MAT_PHAI_TREN] = [0.44, 0.42, 0.0]   # y_tren = 0.42
    pts[mt.MAT_PHAI_DUOI] = [0.44, 0.48, 0.0]   # y_duoi = 0.48 -> mid_y = 0.45, h_half = 0.03

    # Mắt trái người dùng (bên phải ảnh)
    pts[mt.MAT_TRAI_TRONG] = [0.52, 0.45, 0.0]  # x_trong = 0.52
    pts[mt.MAT_TRAI_NGOAI] = [0.60, 0.45, 0.0]  # x_ngoai = 0.60 -> mid_x = 0.56, w_half = 0.04
    pts[mt.MAT_TRAI_TREN] = [0.56, 0.42, 0.0]
    pts[mt.MAT_TRAI_DUOI] = [0.56, 0.48, 0.0]

    # Tâm tròng mắt
    x_phai, y_phai = 0.44, 0.45
    x_trai, y_trai = 0.56, 0.45

    offset_x = 0.02
    offset_y = 0.015
    if gaze_huong == "phai":
        # Nhìn sang phải: tròng mắt dịch về phía phải người dùng = bên trái ảnh (x giảm)
        x_phai -= offset_x
        x_trai -= offset_x
    elif gaze_huong == "trai":
        # Nhìn sang trái: tròng mắt dịch sang phải ảnh (x tăng)
        x_phai += offset_x
        x_trai += offset_x
    elif gaze_huong == "tren":
        # Nhìn lên: y giảm
        y_phai -= offset_y
        y_trai -= offset_y
    elif gaze_huong == "duoi":
        # Nhìn xuống: y tăng
        y_phai += offset_y
        y_trai += offset_y

    pts[mt.TRONG_PHAI_TAM] = [x_phai, y_phai, 0.0]
    pts[mt.TRONG_TRAI_TAM] = [x_trai, y_trai, 0.0]

    # Vành tròng mắt
    r = 0.010 * duong_kinh_ty_le
    pts[469] = [x_phai - r, y_phai, 0.0]
    pts[470] = [x_phai, y_phai - r, 0.0]
    pts[471] = [x_phai + r, y_phai, 0.0]
    pts[472] = [x_phai, y_phai + r, 0.0]

    pts[474] = [x_trai - r, y_trai, 0.0]
    pts[475] = [x_trai, y_trai - r, 0.0]
    pts[476] = [x_trai + r, y_trai, 0.0]
    pts[477] = [x_trai, y_trai + r, 0.0]

    return pts


def tao_diem_tay_gia(pinch_distance: float = 0.10, hand_size: float = 0.30) -> np.ndarray:
    """Tạo bàn tay giả 21 điểm (cổ tay 0, đầu ngón cái 4, đầu ngón trỏ 8, gốc ngón giữa 9)."""
    pts = np.zeros((21, 3), dtype=np.float64)
    # Cổ tay 0 tại (0.5, 0.8)
    pts[0] = [0.50, 0.80, 0.0]
    # Gốc ngón giữa 9 tại (0.5, 0.8 - hand_size)
    pts[9] = [0.50, 0.80 - hand_size, 0.0]
    # Đầu ngón cái 4 và đầu ngón trỏ 8 cách nhau pinch_distance
    pts[4] = [0.50 - pinch_distance / 2.0, 0.40, 0.0]
    pts[8] = [0.50 + pinch_distance / 2.0, 0.40, 0.0]
    return pts


class MediaPipeTinhTest(unittest.TestCase):
    def test_ma_tran_quay_da_biet_dung_goc_va_dung_dau(self):
        """Ma trận quay đã biết -> yaw / pitch / roll đúng (±1°), đúng dấu."""
        # 1. Nhìn thẳng
        m0 = tao_ma_tran_quay_gia(0, 0, 0, 60.0)
        yaw, pitch, roll, dist_z = mt.tinh_tu_the_dau(m0)
        self.assertAlmostEqual(yaw, 0.0, delta=1.0)
        self.assertAlmostEqual(pitch, 0.0, delta=1.0)
        self.assertAlmostEqual(roll, 0.0, delta=1.0)
        self.assertAlmostEqual(dist_z, 60.0, delta=0.5)

        # 2. Quay phải / trái (yaw)
        for y_deg in [-30.0, -15.0, 15.0, 30.0]:
            m = tao_ma_tran_quay_gia(yaw_deg=y_deg)
            y_calc, _, _, _ = mt.tinh_tu_the_dau(m)
            self.assertAlmostEqual(y_calc, y_deg, delta=1.0, msg=f"yaw {y_deg}°")
            self.assertEqual(math.copysign(1, y_calc), math.copysign(1, y_deg))

        # 3. Cúi / ngẩng (pitch)
        for p_deg in [-20.0, 20.0]:
            m = tao_ma_tran_quay_gia(pitch_deg=p_deg)
            _, p_calc, _, _ = mt.tinh_tu_the_dau(m)
            self.assertAlmostEqual(p_calc, p_deg, delta=1.0, msg=f"pitch {p_deg}°")

        # 4. Nghiêng (roll)
        for r_deg in [-15.0, 15.0]:
            m = tao_ma_tran_quay_gia(roll_deg=r_deg)
            _, _, r_calc, _ = mt.tinh_tu_the_dau(m)
            self.assertAlmostEqual(r_calc, r_deg, delta=1.0, msg=f"roll {r_deg}°")

    def test_trong_mat_lech_trai_phai_tren_duoi_dung_chieu(self):
        """Tròng mắt lệch trái/phải/trên/dưới -> gaze đúng chiều; nhìn thẳng -> ≈ 0."""
        # Nhìn thẳng
        lm_thang = tao_diem_mat_gia("thang")
        gx0, gy0 = mt.tinh_gaze(lm_thang, yaw_deg=0.0, pitch_deg=0.0)
        self.assertAlmostEqual(gx0, 0.0, delta=0.05)
        self.assertAlmostEqual(gy0, 0.0, delta=0.05)

        # Nhìn phải -> gaze_x dương (> 0)
        lm_phai = tao_diem_mat_gia("phai")
        gx_phai, _ = mt.tinh_gaze(lm_phai, yaw_deg=0.0, pitch_deg=0.0)
        self.assertGreater(gx_phai, 0.3)

        # Nhìn trái -> gaze_x âm (< 0)
        lm_trai = tao_diem_mat_gia("trai")
        gx_trai, _ = mt.tinh_gaze(lm_trai, yaw_deg=0.0, pitch_deg=0.0)
        self.assertLess(gx_trai, -0.3)

        # Nhìn lên -> gaze_y dương (> 0)
        lm_tren = tao_diem_mat_gia("tren")
        _, gy_tren = mt.tinh_gaze(lm_tren, yaw_deg=0.0, pitch_deg=0.0)
        self.assertGreater(gy_tren, 0.3)

        # Nhìn xuống -> gaze_y âm (< 0)
        lm_duoi = tao_diem_mat_gia("duoi")
        _, gy_duoi = mt.tinh_gaze(lm_duoi, yaw_deg=0.0, pitch_deg=0.0)
        self.assertLess(gy_duoi, -0.3)

    def test_duong_kinh_trong_mat_giam_nua_khoang_cach_gap_doi(self):
        """Đường kính tròng mắt giảm một nửa -> khoảng cách gấp đôi."""
        lm1 = tao_diem_mat_gia("thang", duong_kinh_ty_le=1.0)
        lm2 = tao_diem_mat_gia("thang", duong_kinh_ty_le=0.5)

        d1, _ = mt.tinh_khoang_cach_iris(lm1, hfov_deg=60.0)
        d2, _ = mt.tinh_khoang_cach_iris(lm2, hfov_deg=60.0)

        self.assertAlmostEqual(d2 / d1, 2.0, delta=0.01)

    def test_ban_tay_chum_mo_hysteresis_chong_nhap_nhay(self):
        """Bàn tay giả: chụm / mở -> pinch bật / tắt đúng (hysteresis), không nhấp nháy quanh ngưỡng."""
        # Cỡ bàn tay = 0.30
        # Ngưỡng vào chụm < 0.25 (d < 0.075), ngưỡng ra > 0.35 (d > 0.105)
        co = 0.30

        # Ban đầu mở hoàn toàn: ratio = 0.15 / 0.30 = 0.50 (> 0.35)
        lm_open = tao_diem_tay_gia(pinch_distance=0.15, hand_size=co)
        _, _, _, pinch, r = mt.tinh_tay(lm_open, prev_pinch=False)
        self.assertEqual(pinch, 0)
        self.assertGreater(r, 0.35)

        # Di chuyển vào vùng hysteresis: ratio = 0.09 / 0.30 = 0.30 (giữa 0.25 và 0.35)
        # Đang mở -> vẫn phải giữ là mở (0)
        lm_mid = tao_diem_tay_gia(pinch_distance=0.09, hand_size=co)
        _, _, _, pinch, r = mt.tinh_tay(lm_mid, prev_pinch=False)
        self.assertEqual(pinch, 0)

        # Chụm thật sự: ratio = 0.06 / 0.30 = 0.20 (< 0.25) -> pinch = 1
        lm_close = tao_diem_tay_gia(pinch_distance=0.06, hand_size=co)
        _, _, _, pinch, r = mt.tinh_tay(lm_close, prev_pinch=False)
        self.assertEqual(pinch, 1)

        # Nới tay ra vùng hysteresis: ratio = 0.30
        # Đang chụm -> vẫn phải giữ là chụm (1)
        _, _, _, pinch, r = mt.tinh_tay(lm_mid, prev_pinch=True)
        self.assertEqual(pinch, 1)

        # Thả hẳn: ratio = 0.40 (> 0.35) -> pinch = 0
        lm_far = tao_diem_tay_gia(pinch_distance=0.12, hand_size=co)
        _, _, _, pinch, r = mt.tinh_tay(lm_far, prev_pinch=True)
        self.assertEqual(pinch, 0)

    def test_cu_chi_xuat_hien_sau_150ms_giu_lien_tuc(self):
        """Cử chỉ chỉ xuất hiện sau 150 ms giữ liên tục."""
        loc = mt.BoLocCuChi(nguong_tin=0.6, thoi_gian_giu_s=0.150)
        t = 1000.0

        # Khung 1 tại t: Open_Palm tin cậy 0.90 -> chưa đủ 150 ms -> kết quả rỗng
        res1 = loc.cap_nhat("phai", "Open_Palm", 0.90, t)
        self.assertEqual(res1, "")

        # Khung 2 tại t + 0.100s (100 ms) -> vẫn chưa đủ 150 ms -> rỗng
        res2 = loc.cap_nhat("phai", "Open_Palm", 0.90, t + 0.100)
        self.assertEqual(res2, "")

        # Khung 3 tại t + 0.160s (160 ms > 150 ms) -> ĐỦ ĐIỀU KIỆN -> Open_Palm
        res3 = loc.cap_nhat("phai", "Open_Palm", 0.90, t + 0.160)
        self.assertEqual(res3, "Open_Palm")

        # Khung 4: bị đổi sang cử chỉ khác trong tích tắc (nhấp nháy) -> không nhận cử chỉ mới ngay
        res4 = loc.cap_nhat("phai", "Victory", 0.85, t + 0.180)
        self.assertNotEqual(res4, "Victory")

        # Khung 5: tin cậy tụt dưới 0.6 -> hủy bỏ ngay
        res5 = loc.cap_nhat("phai", "Victory", 0.40, t + 0.200)
        self.assertEqual(res5, "")

    def test_goi_json_moi_du_truong_cu_va_dung_kieu(self):
        """Gói JSON mới vẫn có đủ trường cũ (ok, yaw, pitch, e, t) và các trường mở rộng."""
        hands = [
            {
                "ben": "phai",
                "ok": 1,
                "x": 0.62,
                "y": 0.48,
                "co": 0.18,
                "pinch": 1,
                "cu_chi": "Open_Palm",
                "tin": 0.91,
            }
        ]
        pkt = mt.tao_goi_udp(
            ok=1,
            yaw=12.34,
            pitch=-4.01,
            roll=1.52,
            e=0.21,
            dist_cm=58.2,
            gaze_x=-0.12,
            gaze_y=0.05,
            blink_l=0,
            blink_r=0,
            fps=29.8,
            hands=hands,
            t=1730000000.123,
            nguon="mp",
        )

        # Kiểm tra trường cũ
        self.assertIn("ok", pkt)
        self.assertIn("yaw", pkt)
        self.assertIn("pitch", pkt)
        self.assertIn("e", pkt)
        self.assertIn("t", pkt)

        self.assertIsInstance(pkt["ok"], int)
        self.assertIsInstance(pkt["yaw"], float)
        self.assertIsInstance(pkt["pitch"], float)
        self.assertIsInstance(pkt["e"], float)
        self.assertIsInstance(pkt["t"], float)

        # Kiểm tra trường mới
        self.assertEqual(pkt["nguon"], "mp")
        self.assertAlmostEqual(pkt["dist_cm"], 58.2)
        self.assertAlmostEqual(pkt["gaze_x"], -0.12)
        self.assertEqual(len(pkt["hands"]), 1)
        self.assertEqual(pkt["hands"][0]["cu_chi"], "Open_Palm")


class GiaLapAVDevice:
    def __init__(self, name: str, model_id: str, device_type: str, unique_id: str):
        self._name = name
        self._model_id = model_id
        self._device_type = device_type
        self._unique_id = unique_id

    def localizedName(self): return self._name
    def modelID(self): return self._model_id
    def deviceType(self): return self._device_type
    def uniqueID(self): return self._unique_id


class ChonCameraTest(unittest.TestCase):
    def test_chan_tu_khoa_dien_thoai(self):
        """Kiểm tra nhận diện đúng từ khóa camera điện thoại và ảo."""
        self.assertTrue(cc.la_camera_dien_thoai_hoac_ao("Camera idc🎶", "iPhone15,2", "AVCaptureDeviceTypeExternal"))
        self.assertTrue(cc.la_camera_dien_thoai_hoac_ao("iPad Camera", "iPad13,1", "AVCaptureDeviceTypeExternal"))
        self.assertTrue(cc.la_camera_dien_thoai_hoac_ao("DroidCam Source 3", "", ""))
        self.assertTrue(cc.la_camera_dien_thoai_hoac_ao("OBS Virtual Camera", "", ""))
        self.assertFalse(cc.la_camera_dien_thoai_hoac_ao("Camera FaceTime HD", "Camera FaceTime HD", "AVCaptureDeviceTypeBuiltInWideAngleCamera"))
        self.assertFalse(cc.la_camera_dien_thoai_hoac_ao("Logitech C920 HD Pro", "C920", "AVCaptureDeviceTypeExternal"))

    def test_gia_lap_cac_tinh_huong_thiet_bi(self):
        """Giả lập danh sách thiết bị:
        - webcam máy + iPhone (đủ thứ tự)
        - chỉ có iPhone
        - không có gì
        - webcam USB rời
        Luôn chọn đúng webcam máy hoặc không chọn gì; tuyệt đối không chọn iPhone.
        """
        dev_facetime = GiaLapAVDevice("Camera FaceTime HD", "FaceTime", "AVCaptureDeviceTypeBuiltInWideAngleCamera", "UID_FACETIME")
        dev_iphone = GiaLapAVDevice("Camera idc🎶", "iPhone15,2", "AVCaptureDeviceTypeExternal", "UID_IPHONE")
        dev_usb = GiaLapAVDevice("Logitech C920", "C920", "AVCaptureDeviceTypeExternal", "UID_USB")

        def _mock_env(cv_list, disc_list):
            mock_av = mock.MagicMock()
            mock_av.AVCaptureDeviceTypeBuiltInWideAngleCamera = "AVCaptureDeviceTypeBuiltInWideAngleCamera"
            mock_av.AVCaptureDeviceTypeExternal = "AVCaptureDeviceTypeExternal"
            mock_av.AVMediaTypeVideo = "video"
            mock_av.AVCaptureDevice.devicesWithMediaType_.return_value = cv_list
            mock_session = mock.MagicMock()
            mock_session.devices.return_value = disc_list
            mock_av.AVCaptureDeviceDiscoverySession.discoverySessionWithDeviceTypes_mediaType_position_.return_value = mock_session
            return mock_av

        # 1. Webcam máy trước, iPhone sau
        mock_av1 = _mock_env([dev_facetime, dev_iphone], [dev_facetime, dev_iphone])
        with mock.patch("chon_camera.sys.platform", "darwin"), \
             mock.patch.dict("sys.modules", {"AVFoundation": mock_av1}):

            # Tự chọn -> phải chọn FaceTime HD (index 0)
            idx, name, uid = cc.chon_webcam_may("may")
            self.assertEqual(idx, 0)
            self.assertEqual(name, "Camera FaceTime HD")
            self.assertEqual(uid, "UID_FACETIME")

            # Người dùng chọn index 1 (iPhone) -> bị từ chối
            idx1, name1, loi1 = cc.chon_webcam_may(1)
            self.assertIsNone(idx1)
            self.assertEqual(loi1, "da_chan_camera_dien_thoai")

        # 2. iPhone đứng trước (index 0), webcam máy đứng sau (index 1)
        mock_av2 = _mock_env([dev_iphone, dev_facetime], [dev_iphone, dev_facetime])
        with mock.patch("chon_camera.sys.platform", "darwin"), \
             mock.patch.dict("sys.modules", {"AVFoundation": mock_av2}):

            # Tự chọn -> vẫn phải tìm ra FaceTime HD tại index 1
            idx, name, uid = cc.chon_webcam_may("may")
            self.assertEqual(idx, 1)
            self.assertEqual(name, "Camera FaceTime HD")
            self.assertEqual(uid, "UID_FACETIME")

            # Chọn index 0 (iPhone) -> bị từ chối
            idx0, _, loi0 = cc.chon_webcam_may(0)
            self.assertIsNone(idx0)
            self.assertEqual(loi0, "da_chan_camera_dien_thoai")

        # 3. Chỉ có iPhone, không có webcam máy
        mock_av3 = _mock_env([dev_iphone], [dev_iphone])
        with mock.patch("chon_camera.sys.platform", "darwin"), \
             mock.patch.dict("sys.modules", {"AVFoundation": mock_av3}):

            # Tự chọn -> không mở camera nào
            idx, _, loi = cc.chon_webcam_may("may")
            self.assertIsNone(idx)
            self.assertEqual(loi, "khong_thay_webcam_may")

        # 4. Không có thiết bị nào
        mock_av4 = _mock_env([], [])
        with mock.patch("chon_camera.sys.platform", "darwin"), \
             mock.patch.dict("sys.modules", {"AVFoundation": mock_av4}):
            idx, _, loi = cc.chon_webcam_may("may")
            self.assertIsNone(idx)
            self.assertEqual(loi, "khong_thay_thiet_bi_video_nao")

        # 5. Webcam USB rời (không có webcam tích hợp, có USB + iPhone)
        mock_av5 = _mock_env([dev_iphone, dev_usb], [dev_iphone, dev_usb])
        with mock.patch("chon_camera.sys.platform", "darwin"), \
             mock.patch.dict("sys.modules", {"AVFoundation": mock_av5}):

            # Tự chọn -> chọn webcam USB rời tại index 1
            idx, name, uid = cc.chon_webcam_may("may")
            self.assertEqual(idx, 1)
            self.assertEqual(name, "Logitech C920")
            self.assertEqual(uid, "UID_USB")


if __name__ == "__main__":
    unittest.main()
