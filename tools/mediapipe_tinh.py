"""Các hàm tính toán thuần cho MediaPipe (không phụ thuộc webcam).

Bao gồm:
1. Hướng đầu: từ facial_transformation_matrixes (ma trận 4x4) -> yaw, pitch, roll, dist_z_cm.
2. Tròng mắt: gaze_x, gaze_y, chớp mắt (hysteresis), khoảng cách d = f * 11.7mm / diameter.
3. Tay: tâm bàn tay (x, y), cỡ bàn tay, pinch (hysteresis vào < 0.25, ra > 0.35),
   và bộ lọc cử chỉ (tin cậy >= 0.6, giữ liên tục >= 150 ms).
4. Đóng gói JSON UDP tương thích ngược với head_tracker.py.
"""

from __future__ import annotations

import math
from typing import Any

import cv2
import numpy as np

# Đường kính tròng mắt người trung bình ~11.7 mm = 1.17 cm
DUONG_KINH_TRONG_MAT_CM = 1.17

# Chỉ số điểm mốc MediaPipe FaceLandmarker
# Mắt phải (bên trái ảnh): ngoài 33, trong 133, trên 159, dưới 145, tâm tròng 468, vành 469-472
# Mắt trái (bên phải ảnh): trong 362, ngoài 263, trên 386, dưới 374, tâm tròng 473, vành 474-477
MAT_PHAI_NGOAI = 33
MAT_PHAI_TRONG = 133
MAT_PHAI_TREN = 159
MAT_PHAI_DUOI = 145
TRONG_PHAI_TAM = 468
TRONG_PHAI_VANH = (469, 470, 471, 472)

MAT_TRAI_TRONG = 362
MAT_TRAI_NGOAI = 263
MAT_TRAI_TREN = 386
MAT_TRAI_DUOI = 374
TRONG_TRAI_TAM = 473
TRONG_TRAI_VANH = (474, 475, 476, 477)

# Cử chỉ được hỗ trợ
CU_CHI_HOP_LE = {
    "Closed_Fist",
    "Open_Palm",
    "Pointing_Up",
    "Thumb_Up",
    "Thumb_Down",
    "Victory",
    "ILoveYou",
}


def tinh_tu_the_dau(mat_4x4: np.ndarray) -> tuple[float, float, float, float]:
    """Từ ma trận tư thế 4x4 -> (yaw, pitch, roll, dist_z_cm).

    Quy ước dấu khớp với head_tracker.py:
    - yaw (độ): dương = quay đầu sang PHẢI của người dùng.
    - pitch (độ): dương = cúi xuống, âm = ngẩng lên.
    - roll (độ): dương = nghiêng đầu sang phải.
    - dist_z_cm (cm): khoảng cách dọc trục z của camera.
    """
    mat = np.asarray(mat_4x4, dtype=np.float64)
    if mat.shape != (4, 4):
        return 0.0, 0.0, 0.0, 0.0

    rot = mat[:3, :3]
    angles = cv2.RQDecomp3x3(rot)[0]
    pitch = float(angles[0])
    yaw = float(angles[1])
    roll = float(angles[2])
    dist_z_cm = float(abs(mat[2, 3]))

    return yaw, pitch, roll, dist_z_cm


def tinh_khoang_cach_iris(
    landmarks: list[Any] | np.ndarray,
    hfov_deg: float = 60.0,
) -> tuple[float, float]:
    """Tính khoảng cách đầu (cm) theo đường kính tròng mắt và e (tỉ lệ hai mắt).

    Trả về: (dist_iris_cm, e_raw).
    """
    def _pt(idx: int) -> tuple[float, float]:
        p = landmarks[idx]
        if hasattr(p, "x"):
            return float(p.x), float(p.y)
        return float(p[0]), float(p[1])

    # Đường kính vành tròng mắt phải: 469-471 (ngang), 470-472 (dọc)
    p469, p470, p471, p472 = _pt(469), _pt(470), _pt(471), _pt(472)
    d_phai_ngang = math.hypot(p471[0] - p469[0], p471[1] - p469[1])
    d_phai_doc = math.hypot(p472[0] - p470[0], p472[1] - p470[1])
    d_phai = (d_phai_ngang + d_phai_doc) / 2.0

    # Đường kính vành tròng mắt trái: 474-476 (ngang), 475-477 (dọc)
    p474, p475, p476, p477 = _pt(474), _pt(475), _pt(476), _pt(477)
    d_trai_ngang = math.hypot(p476[0] - p474[0], p476[1] - p474[1])
    d_trai_doc = math.hypot(p477[0] - p475[0], p477[1] - p475[1])
    d_trai = (d_trai_ngang + d_trai_doc) / 2.0

    d_iris_norm = max(1e-6, (d_phai + d_trai) / 2.0)

    # Tiêu cự chuẩn hóa theo góc nhìn ngang hfov
    tan_half = math.tan(math.radians(hfov_deg / 2.0))
    f_norm = 1.0 / (2.0 * max(1e-6, tan_half))

    dist_cm = (f_norm * DUONG_KINH_TRONG_MAT_CM) / d_iris_norm

    # Khoảng cách hai mắt (e_raw)
    p_phai_tam, p_trai_tam = _pt(TRONG_PHAI_TAM), _pt(TRONG_TRAI_TAM)
    e_raw = math.hypot(p_trai_tam[0] - p_phai_tam[0], p_trai_tam[1] - p_phai_tam[1])

    return dist_cm, e_raw


def tinh_gaze(
    landmarks: list[Any] | np.ndarray,
    yaw_deg: float = 0.0,
    pitch_deg: float = 0.0,
) -> tuple[float, float]:
    """Tính hướng nhìn gaze_x, gaze_y trong khoảng [-1, 1], đã bù hướng đầu.

    - gaze_x: dương = nhìn sang phải, âm = nhìn sang trái.
    - gaze_y: dương = nhìn lên, âm = nhìn xuống.
    """
    def _pt(idx: int) -> tuple[float, float]:
        p = landmarks[idx]
        if hasattr(p, "x"):
            return float(p.x), float(p.y)
        return float(p[0]), float(p[1])

    # Mắt phải người dùng (bên trái ảnh)
    p_ngoai_r, p_trong_r = _pt(MAT_PHAI_NGOAI), _pt(MAT_PHAI_TRONG)
    p_tren_r, p_duoi_r = _pt(MAT_PHAI_TREN), _pt(MAT_PHAI_DUOI)
    p_iris_r = _pt(TRONG_PHAI_TAM)

    mid_x_r = (p_ngoai_r[0] + p_trong_r[0]) / 2.0
    w_half_r = max(1e-6, abs(p_trong_r[0] - p_ngoai_r[0]) / 2.0)
    mid_y_r = (p_tren_r[1] + p_duoi_r[1]) / 2.0
    h_half_r = max(1e-6, abs(p_duoi_r[1] - p_tren_r[1]) / 2.0)

    # Trong ảnh chưa lật: mắt phải di chuyển sang trái ảnh (x giảm) khi người nhìn sang phải
    raw_gx_r = -(p_iris_r[0] - mid_x_r) / w_half_r
    raw_gy_r = -(p_iris_r[1] - mid_y_r) / h_half_r

    # Mắt trái người dùng (bên phải ảnh)
    p_trong_l, p_ngoai_l = _pt(MAT_TRAI_TRONG), _pt(MAT_TRAI_NGOAI)
    p_tren_l, p_duoi_l = _pt(MAT_TRAI_TREN), _pt(MAT_TRAI_DUOI)
    p_iris_l = _pt(TRONG_TRAI_TAM)

    mid_x_l = (p_trong_l[0] + p_ngoai_l[0]) / 2.0
    w_half_l = max(1e-6, abs(p_ngoai_l[0] - p_trong_l[0]) / 2.0)
    mid_y_l = (p_tren_l[1] + p_duoi_l[1]) / 2.0
    h_half_l = max(1e-6, abs(p_duoi_l[1] - p_tren_l[1]) / 2.0)

    raw_gx_l = -(p_iris_l[0] - mid_x_l) / w_half_l
    raw_gy_l = -(p_iris_l[1] - mid_y_l) / h_half_l

    raw_gx = (raw_gx_r + raw_gx_l) / 2.0
    raw_gy = (raw_gy_r + raw_gy_l) / 2.0

    # Bù hướng đầu: khi đầu quay sang phải (yaw > 0), nếu mắt vẫn nhìn màn hình
    # thì tròng mắt trong hốc mắt phải lệch sang trái (raw_gx âm).
    # Cộng yaw để hướng nhìn so với màn hình trở về 0.
    gaze_x = float(np.clip(raw_gx + yaw_deg / 30.0, -1.0, 1.0))
    gaze_y = float(np.clip(raw_gy - pitch_deg / 25.0, -1.0, 1.0))

    return gaze_x, gaze_y


class BoLocChopMat:
    """Xác định chớp mắt có trễ (hysteresis) để chống nhấp nháy."""

    def __init__(self, nguong_dong: float = 0.45, nguong_mo: float = 0.25):
        self.nguong_dong = nguong_dong
        self.nguong_mo = nguong_mo
        self.blink_l = 0
        self.blink_r = 0

    def cap_nhat(self, score_l: float, score_r: float) -> tuple[int, int]:
        if self.blink_l == 0:
            if score_l >= self.nguong_dong:
                self.blink_l = 1
        else:
            if score_l <= self.nguong_mo:
                self.blink_l = 0

        if self.blink_r == 0:
            if score_r >= self.nguong_dong:
                self.blink_r = 1
        else:
            if score_r <= self.nguong_mo:
                self.blink_r = 0

        return self.blink_l, self.blink_r


def tinh_blendshapes(
    categories: list[Any],
    yaw_deg: float = 0.0,
    pitch_deg: float = 0.0,
) -> tuple[float, float, int, int]:
    """Tính gaze và chớp mắt từ danh sách FaceBlendshapes categories.

    Trả về: (gaze_x, gaze_y, blink_l, blink_r).
    """
    scores: dict[str, float] = {}
    for c in categories:
        name = getattr(c, "category_name", None) or getattr(c, "name", "")
        val = getattr(c, "score", 0.0)
        if name:
            scores[name] = float(val)

    blink_l = 1 if scores.get("eyeBlinkLeft", 0.0) >= 0.45 else 0
    blink_r = 1 if scores.get("eyeBlinkRight", 0.0) >= 0.45 else 0

    look_r_x = scores.get("eyeLookOutRight", 0.0) - scores.get("eyeLookInRight", 0.0)
    look_l_x = scores.get("eyeLookInLeft", 0.0) - scores.get("eyeLookOutLeft", 0.0)
    raw_gx = (look_r_x + look_l_x) / 2.0

    look_r_y = scores.get("eyeLookUpRight", 0.0) - scores.get("eyeLookDownRight", 0.0)
    look_l_y = scores.get("eyeLookUpLeft", 0.0) - scores.get("eyeLookDownLeft", 0.0)
    raw_gy = (look_r_y + look_l_y) / 2.0

    gaze_x = float(np.clip(raw_gx + yaw_deg / 30.0, -1.0, 1.0))
    gaze_y = float(np.clip(raw_gy - pitch_deg / 25.0, -1.0, 1.0))

    return gaze_x, gaze_y, blink_l, blink_r


class BoLocChopMatHaiLan:
    """Phát hiện chớp hai lần nhanh để xác nhận (double-blink confirmation).

    Khoảng cách giữa 2 lần chớp mắt: từ 0.15 s đến 0.50 s.
    """

    def __init__(self, min_interval: float = 0.15, max_interval: float = 0.50):
        self.min_interval = min_interval
        self.max_interval = max_interval
        self.last_blink_time = -1.0
        self.prev_blinking = False

    def cap_nhat(self, blink_l: int, blink_r: int, now: float) -> bool:
        """Cập nhật trạng thái chớp mắt. Trả về True trong đúng 1 khung khi phát hiện chớp đôi."""
        is_blinking = (blink_l == 1 and blink_r == 1)
        confirmed_this_frame = False

        # Bắt sườn xuống: vừa mở mắt trở lại sau khi chớp
        if self.prev_blinking and not is_blinking:
            if self.last_blink_time > 0:
                delta = now - self.last_blink_time
                if self.min_interval <= delta <= self.max_interval:
                    confirmed_this_frame = True
                    self.last_blink_time = -1.0
                else:
                    self.last_blink_time = now
            else:
                self.last_blink_time = now

        if self.last_blink_time > 0 and (now - self.last_blink_time) > self.max_interval:
            self.last_blink_time = -1.0

        self.prev_blinking = is_blinking
        return confirmed_this_frame


def tinh_tay(
    landmarks: list[Any] | np.ndarray,
    prev_pinch: bool = False,
) -> tuple[float, float, float, int, float]:
    """Tính các đại lượng bàn tay:

    Trả về: (x, y, co_tay, pinch, pinch_ratio).
    - x, y: tâm lòng bàn tay (trung điểm cổ tay 0 và gốc ngón giữa 9).
    - co_tay: khoảng cách từ cổ tay 0 tới gốc ngón giữa 9 (chuẩn hóa theo bề rộng ảnh).
    - pinch: 1 (chụm) hoặc 0 (thả) có hysteresis: vào < 0.25, ra > 0.35.
    - pinch_ratio: tỉ lệ khoảng cách ngón cái (4) - ngón trỏ (8) trên cỡ bàn tay.
    """
    def _pt(idx: int) -> tuple[float, float]:
        p = landmarks[idx]
        if hasattr(p, "x"):
            return float(p.x), float(p.y)
        return float(p[0]), float(p[1])

    w0 = _pt(0)   # cổ tay
    m9 = _pt(9)   # gốc ngón giữa
    t4 = _pt(4)   # đầu ngón cái
    i8 = _pt(8)   # đầu ngón trỏ

    # Tâm bàn tay
    x = (w0[0] + m9[0]) / 2.0
    y = (w0[1] + m9[1]) / 2.0

    # Cỡ bàn tay
    co_tay = max(1e-6, math.hypot(m9[0] - w0[0], m9[1] - w0[1]))

    # Khoảng cách ngón cái - ngón trỏ
    d_pinch = math.hypot(t4[0] - i8[0], t4[1] - i8[1])
    pinch_ratio = d_pinch / co_tay

    # Hysteresis chụm ngón
    if not prev_pinch:
        pinch = 1 if pinch_ratio < 0.25 else 0
    else:
        pinch = 0 if pinch_ratio > 0.35 else 1

    return float(x), float(y), float(co_tay), pinch, float(pinch_ratio)


class BoLocCuChi:
    """Bộ lọc cử chỉ bàn tay: chỉ nhận khi điểm tin cậy >= 0.6 và giữ liên tục >= 150 ms."""

    def __init__(self, nguong_tin: float = 0.6, thoi_gian_giu_s: float = 0.150):
        self.nguong_tin = nguong_tin
        self.thoi_gian_giu_s = thoi_gian_giu_s
        self.ung_vien: dict[str, str] = {}  # ben -> tên cử chỉ ứng viên
        self.t_bat_dau: dict[str, float] = {}  # ben -> thời điểm bắt đầu thấy cử chỉ
        self.cu_chi_chot: dict[str, str] = {}  # ben -> cử chỉ đã chốt

    def cap_nhat(self, ben: str, ten_cu_chi: str, diem_tin: float, now: float) -> str:
        if ten_cu_chi not in CU_CHI_HOP_LE or diem_tin < self.nguong_tin:
            self.ung_vien[ben] = ""
            self.t_bat_dau[ben] = now
            self.cu_chi_chot[ben] = ""
            return ""

        cur = self.ung_vien.get(ben, "")
        if ten_cu_chi != cur:
            self.ung_vien[ben] = ten_cu_chi
            self.t_bat_dau[ben] = now
            # Nếu cử chỉ chốt trước đó khác, tạm thời không nhận cử chỉ mới cho đến khi đủ thời gian
            return self.cu_chi_chot.get(ben, "")

        # Giữ cùng cử chỉ
        dt = now - self.t_bat_dau.get(ben, now)
        if dt >= self.thoi_gian_giu_s:
            self.cu_chi_chot[ben] = ten_cu_chi

        return self.cu_chi_chot.get(ben, "")


def tao_goi_udp(
    ok: int,
    yaw: float = 0.0,
    pitch: float = 0.0,
    roll: float = 0.0,
    e: float = 0.0,
    dist_cm: float = 0.0,
    gaze_x: float = 0.0,
    gaze_y: float = 0.0,
    blink_l: int = 0,
    blink_r: int = 0,
    fps: float = 0.0,
    hands: list[dict[str, Any]] | None = None,
    t: float = 0.0,
    nguon: str = "mp",
    xac_nhan: int = 0,
) -> dict[str, Any]:
    """Tạo từ điển gói tin UDP chuẩn, tương thích cả trường cũ lẫn trường mới."""
    d = {
        "ok": int(ok),
        "yaw": round(float(yaw), 2),
        "pitch": round(float(pitch), 4),
        "e": round(float(e), 5),
        "t": round(float(t), 4),
        "nguon": str(nguon),
        "roll": round(float(roll), 2),
        "dist_cm": round(float(dist_cm), 2),
        "gaze_x": round(float(gaze_x), 3),
        "gaze_y": round(float(gaze_y), 3),
        "blink_l": int(blink_l),
        "blink_r": int(blink_r),
        "fps": round(float(fps), 1),
        "hands": hands if hands is not None else [],
    }
    if xac_nhan:
        d["xac_nhan"] = 1
    return d
