"""Kiểm thử phần tính tư thế đầu của tools/head_tracker.py bằng khuôn mặt 3D giả lập chiếu qua webcam (không cần webcam)."""

import math
import os
import sys
import unittest
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
import head_tracker as ht  # noqa: E402

# Hệ camera OpenCV: x sang phải ảnh, y xuống, z ra xa camera (cm). Người nhìn thẳng vào webcam:
# mắt phải của họ ở bên trái ảnh. Mũi nhô về phía camera NHO_MUI × khoảng cách hai mắt.
MAT = 6.3
MAT_PHAI = np.array([-MAT / 2, 0.0, 0.0])
MAT_TRAI = np.array([MAT / 2, 0.0, 0.0])
MUI = np.array([0.0, 3.2, -ht.NHO_MUI * MAT])
MIENG_PHAI = np.array([-2.5, 6.0, -0.8])
MIENG_TRAI = np.array([2.5, 6.0, -0.8])
TAM_DAU = np.array([0.0, 2.0, 9.0])  # đầu quay quanh điểm sau mặt chừng 9 cm
F = 500.0
W = 640.0


def mat_gia(yaw_deg=0.0, pitch_deg=0.0, roll_deg=0.0, xa_cm=60.0):
    """5 điểm ảnh khi người dùng quay đầu yaw (dương = sang phải của họ), cúi pitch (dương = cúi), nghiêng roll."""
    y, p, r = (math.radians(v) for v in (yaw_deg, pitch_deg, roll_deg))
    ry = np.array([[math.cos(y), 0, math.sin(y)], [0, 1, 0], [-math.sin(y), 0, math.cos(y)]])
    rx = np.array([[1, 0, 0], [0, math.cos(p), -math.sin(p)], [0, math.sin(p), math.cos(p)]])
    rz = np.array([[math.cos(r), -math.sin(r), 0], [math.sin(r), math.cos(r), 0], [0, 0, 1]])
    rot = rz @ ry @ rx
    out = []
    for q in (MAT_PHAI, MAT_TRAI, MUI, MIENG_PHAI, MIENG_TRAI):
        v = rot @ (q - TAM_DAU) + TAM_DAU + np.array([0, 0, xa_cm])
        out.append([W / 2 + F * v[0] / v[2], 240 + F * v[1] / v[2]])
    return np.array(out)


class TuTheDauTest(unittest.TestCase):
    def test_nhin_thang(self):
        yaw, pitch, e = ht.tu_the(mat_gia(), W)
        self.assertAlmostEqual(yaw, 0.0, delta=0.5)
        self.assertGreater(e, 0)

    def test_quay_trai_phai_dung_dau_dung_do_lon(self):
        # góc đo được lệch dần theo phối cảnh (mắt gần webcam trông to hơn): dung sai 2° + 20%; dùng để lái nên cần đúng
        # chiều và tăng dần đều, không cần đúng tuyệt đối
        truoc = -1e9
        for goc in (-40, -25, -10, 0, 10, 25, 40):
            yaw, _, _ = ht.tu_the(mat_gia(yaw_deg=goc), W)
            self.assertAlmostEqual(yaw, goc, delta=2 + 0.2 * abs(goc), msg=f"quay {goc}°")
            if goc:
                self.assertEqual(math.copysign(1, yaw), math.copysign(1, goc))
            self.assertGreater(yaw, truoc)
            truoc = yaw

    def test_nghieng_dau_khong_lam_sai_goc_quay(self):
        yaw, _, _ = ht.tu_the(mat_gia(yaw_deg=20, roll_deg=15), W)
        self.assertAlmostEqual(yaw, 20, delta=2 + 0.2 * 20)
        yaw0, _, _ = ht.tu_the(mat_gia(roll_deg=-20), W)
        self.assertAlmostEqual(yaw0, 0, delta=1.5)

    def test_ghe_gan_thi_e_tang_quay_dau_thi_e_gan_nhu_khong_doi(self):
        _, _, e60 = ht.tu_the(mat_gia(xa_cm=60), W)
        _, _, e40 = ht.tu_the(mat_gia(xa_cm=40), W)
        _, _, e60q = ht.tu_the(mat_gia(yaw_deg=30, xa_cm=60), W)
        self.assertAlmostEqual(e40 / e60, 1.5, delta=0.1)
        self.assertAlmostEqual(e60q / e60, 1.0, delta=0.08)

    def test_cui_xuong_thi_pitch_tang(self):
        _, p0, _ = ht.tu_the(mat_gia(), W)
        _, p_cui, _ = ht.tu_the(mat_gia(pitch_deg=20), W)
        _, p_ngang, _ = ht.tu_the(mat_gia(pitch_deg=-20), W)
        self.assertGreater(p_cui, p0)
        self.assertLess(p_ngang, p0)

    def test_kiem_tien_trinh_cha(self):
        self.assertTrue(ht.con_song(os.getpid()))
        self.assertFalse(ht.con_song(0x7FFFFFF0))
        self.assertTrue(ht.con_song(0))  # không truyền pid: coi như còn

    def test_co_mo_hinh(self):
        self.assertTrue(ht.MO_HINH.exists(), ht.MO_HINH)


if __name__ == "__main__":
    unittest.main()
