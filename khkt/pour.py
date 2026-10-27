"""Mô hình rót theo góc nghiêng cho cốc hình trụ (bản C#: PourableContainer.cs).

Khi nghiêng cốc góc θ, mặt chất lỏng vẫn đi qua tâm ở độ cao H (H = V / diện tích đáy)
và dâng lên ở phía miệng cốc thêm r·tanθ. Chất lỏng bắt đầu tràn khi H + r·tanθ ≥ h,
tức là góc bắt đầu rót phụ thuộc lượng chất lỏng còn lại:

    tan θ_bắt_đầu = (h - H) / r

Phần dâng vượt miệng cốc e = H + r·tanθ - h chảy ra như qua đập tràn: Q = k·e^1.5.
Hệ quả giống thật: cốc càng vơi càng phải nghiêng nhiều, và tốc độ chảy tăng rất
nhanh khi nghiêng quá tay. Đây chính là thứ học sinh phải luyện.
"""

from __future__ import annotations

import math
from dataclasses import dataclass


@dataclass
class Cup:
    radius_m: float = 0.025   # cốc 100 ml: đường kính 5 cm
    height_m: float = 0.07
    volume_ml: float = 50.0
    weir_coef: float = 0.036  # k = C·b, C ≈ 1,8 m^0.5/s, miệng rót rộng b ≈ 2 cm
    max_excess_m: float = 0.03
    min_excess_m: float = 0.0005  # dưới 0,5 mm sức căng bề mặt giữ nước lại: ngừng chảy

    @property
    def area_m2(self) -> float:
        return math.pi * self.radius_m**2

    @property
    def level_m(self) -> float:
        return min(self.height_m, self.volume_ml * 1e-6 / self.area_m2)

    def start_angle_deg(self) -> float:
        return math.degrees(math.atan2(self.height_m - self.level_m, self.radius_m))

    def flow_ml_s(self, tilt_deg: float) -> float:
        if self.volume_ml <= 0:
            return 0.0
        t = math.radians(max(0.0, min(89.0, tilt_deg)))
        excess = self.level_m + self.radius_m * math.tan(t) - self.height_m
        if excess <= self.min_excess_m:
            return 0.0
        excess = min(excess, self.max_excess_m)
        return self.weir_coef * excess**1.5 * 1e6

    def step(self, tilt_deg: float, dt: float) -> float:
        """Rót trong dt giây, trả về số ml chảy ra."""
        out = min(self.volume_ml, self.flow_ml_s(tilt_deg) * dt)
        self.volume_ml -= out
        return out
