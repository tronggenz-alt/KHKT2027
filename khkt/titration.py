"""Mô hình chuẩn độ axit (trong bình) bằng NaOH (trên buret).

Tính pH bằng phương trình bảo toàn điện tích, nên một công thức dùng được cả
trước, tại và sau điểm tương đương, cho axit mạnh lẫn axit yếu:

    [H+] + [Na+] = [OH-] + [A-]
    [OH-] = Kw / [H+]
    [A-]  = C_A                     (axit mạnh, phân li hoàn toàn)
    [A-]  = C_A * Ka / (Ka + [H+])  (axit yếu)

Với axit mạnh trước điểm tương đương, kết quả trùng công thức trong bản thiết kế:
[H+] = (CaVa - CbVb) / (Va + Vb).
"""

from __future__ import annotations

import math

KW = 1.0e-14

# Khoảng đổi màu chất chỉ thị (pH): (pH bắt đầu, pH kết thúc, màu axit, màu bazơ)
INDICATORS = {
    "phenolphtalein": (8.2, 10.0, (1.0, 1.0, 1.0, 0.0), (0.93, 0.25, 0.6, 0.8)),
    "metyl_da_cam": (3.1, 4.4, (0.85, 0.15, 0.1, 0.8), (0.95, 0.75, 0.1, 0.8)),
}


def ph(ca: float, va_ml: float, cb: float, vb_ml: float, ka: float | None = None) -> float:
    """pH của bình sau khi thêm vb_ml NaOH nồng độ cb vào va_ml axit nồng độ ca.

    ka=None nghĩa là axit mạnh (HCl). Axit axetic: ka = 1.75e-5.
    """
    v_l = (va_ml + vb_ml) / 1000.0
    if v_l <= 0:
        raise ValueError("thể tích phải dương")
    c_acid = ca * va_ml / 1000.0 / v_l
    c_na = cb * vb_ml / 1000.0 / v_l

    def f(h: float) -> float:
        a_minus = c_acid if ka is None else c_acid * ka / (ka + h)
        return h + c_na - KW / h - a_minus

    # f tăng đơn điệu theo h, nên chia đôi trên thang log là chắc chắn hội tụ
    lo, hi = -15.0, 1.0
    for _ in range(80):
        mid = 0.5 * (lo + hi)
        if f(10.0**mid) > 0:
            hi = mid
        else:
            lo = mid
    return -0.5 * (lo + hi)


def equivalence_volume_ml(ca: float, va_ml: float, cb: float) -> float:
    return ca * va_ml / cb


def indicator_fraction(name: str, p_h: float) -> float:
    """0 = màu dạng axit, 1 = màu dạng bazơ, nội suy tuyến tính trong khoảng đổi màu."""
    lo, hi, _, _ = INDICATORS[name]
    return max(0.0, min(1.0, (p_h - lo) / (hi - lo)))


def curve(ca: float, va_ml: float, cb: float, vb_max_ml: float, step_ml: float = 0.1,
          ka: float | None = None) -> list[tuple[float, float]]:
    """Đường cong chuẩn độ lý thuyết [(V_NaOH, pH)] để đặt cạnh số liệu đo thật."""
    n = int(round(vb_max_ml / step_ml))
    return [(i * step_ml, ph(ca, va_ml, cb, i * step_ml, ka)) for i in range(n + 1)]


def endpoint_error_percent(v_read_ml: float, ca: float, va_ml: float, cb: float) -> float:
    """Sai số tương đối (%) của thể tích học sinh đọc so với điểm tương đương."""
    ve = equivalence_volume_ml(ca, va_ml, cb)
    return 100.0 * (v_read_ml - ve) / ve


def parallax_error_ml(eye_above_m: float, eye_distance_m: float, wall_radius_m: float = 0.007,
                      ml_per_m: float = 95.0) -> float:
    """Sai số thị sai khi đọc buret.

    Mắt cao hơn mặt khum eye_above_m, cách trục buret eye_distance_m. Tia nhìn tới
    đáy mặt khum cắt thành ống (nơi in vạch) cao hơn đáy mặt khum một đoạn
    eye_above_m * r / D. Buret ghi số tăng dần từ trên xuống, nên nhìn từ trên
    sẽ đọc NHỎ hơn giá trị đúng (kết quả âm). Buret 50 ml: khoảng 0,95 ml/cm.
    """
    if eye_distance_m <= wall_radius_m:
        raise ValueError("mắt phải ở ngoài buret")
    dh = eye_above_m * wall_radius_m / eye_distance_m
    return -dh * ml_per_m
