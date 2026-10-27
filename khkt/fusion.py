"""Bộ lọc Mahony: gộp con quay (nhanh, trôi dần) với gia tốc (chậm, biết hướng trọng lực).

Hệ trục tay cầm thuận tay phải: X tới trước, Y sang trái, Z lên trên.
Kết quả là quaternion (w, x, y, z) quay từ hệ tay cầm sang hệ thế giới (Z lên trên).
Góc nghiêng (pitch, roll) bám theo trọng lực nên không trôi; góc yaw không có
mốc tuyệt đối (MPU6050 không có la bàn) nên trôi dần và cần nút "căn lại".
Unity dùng bản C# tương đương: Phong thi nghiem ao/Assets/Scripts/Input/MahonyFilter.cs.
"""

from __future__ import annotations

import math


class Mahony:
    def __init__(self, kp: float = 1.0, ki: float = 0.02) -> None:
        self.kp = kp
        self.ki = ki
        self.q = [1.0, 0.0, 0.0, 0.0]
        self._ix = self._iy = self._iz = 0.0

    def update(self, gyro_dps: tuple[float, float, float], accel: tuple[float, float, float], dt: float) -> None:
        gx, gy, gz = (math.radians(v) for v in gyro_dps)
        ax, ay, az = accel
        qw, qx, qy, qz = self.q
        n = math.sqrt(ax * ax + ay * ay + az * az)
        # Chỉ tin gia tốc kế khi gần 1 g (đang cầm yên hoặc chuyển động chậm)
        if 0.7 < n < 1.3:
            ax, ay, az = ax / n, ay / n, az / n
            # hướng trọng lực "lên trên" theo ước lượng hiện tại, biểu diễn trong hệ tay cầm
            vx = 2 * (qx * qz - qw * qy)
            vy = 2 * (qw * qx + qy * qz)
            vz = qw * qw - qx * qx - qy * qy + qz * qz
            ex = ay * vz - az * vy
            ey = az * vx - ax * vz
            ez = ax * vy - ay * vx
            if self.ki > 0:
                self._ix += self.ki * ex * dt
                self._iy += self.ki * ey * dt
                self._iz += self.ki * ez * dt
            gx += self.kp * ex + self._ix
            gy += self.kp * ey + self._iy
            gz += self.kp * ez + self._iz
        h = 0.5 * dt
        qw, qx, qy, qz = (
            qw + (-qx * gx - qy * gy - qz * gz) * h,
            qx + (qw * gx + qy * gz - qz * gy) * h,
            qy + (qw * gy - qx * gz + qz * gx) * h,
            qz + (qw * gz + qx * gy - qy * gx) * h,
        )
        m = math.sqrt(qw * qw + qx * qx + qy * qy + qz * qz)
        self.q = [qw / m, qx / m, qy / m, qz / m]

    def reset_from_accel(self, accel: tuple[float, float, float]) -> None:
        """Đặt ngay pitch/roll theo trọng lực (yaw = 0) để khỏi chờ bộ lọc hội tụ."""
        ax, ay, az = accel
        roll = math.atan2(ay, az)
        pitch = math.atan2(-ax, math.hypot(ay, az))
        cr, sr = math.cos(roll / 2), math.sin(roll / 2)
        cp, sp = math.cos(pitch / 2), math.sin(pitch / 2)
        self.q = [cr * cp, sr * cp, cr * sp, -sr * sp]
        self._ix = self._iy = self._iz = 0.0

    def euler_deg(self) -> tuple[float, float, float]:
        """(roll, pitch, yaw) theo độ. Pitch dương = chúc đầu tay cầm xuống."""
        w, x, y, z = self.q
        roll = math.atan2(2 * (w * x + y * z), 1 - 2 * (x * x + y * y))
        pitch = math.asin(max(-1.0, min(1.0, 2 * (w * y - z * x))))
        yaw = math.atan2(2 * (w * z + x * y), 1 - 2 * (y * y + z * z))
        return math.degrees(roll), math.degrees(pitch), math.degrees(yaw)

    def tilt_deg(self) -> float:
        """Góc giữa trục Z của tay cầm và phương thẳng đứng (0 = cầm thẳng)."""
        w, x, y, z = self.q
        up_z = w * w - x * x - y * y + z * z
        return math.degrees(math.acos(max(-1.0, min(1.0, up_z))))
