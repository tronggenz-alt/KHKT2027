"""Chỉ số khéo tay từ file ghi chuyển động (CSV 100 Hz của Unity hoặc tools/tay_cam_monitor.py).

Cột bắt buộc: t_ms, gx, gy, gz (°/s). Cột tùy chọn của bài chuẩn độ: v_buret_ml
(thể tích NaOH đã chảy), opening (độ mở khóa buret 0..1).
"""

from __future__ import annotations

import csv
import math
from pathlib import Path

import numpy as np

TREMOR_BAND_HZ = (4.0, 12.0)  # run sinh lý thường ở 8-12 Hz; lấy rộng xuống 4 Hz cho run do căng thẳng


def load_csv(path: str | Path) -> dict[str, np.ndarray]:
    with open(path, newline="", encoding="utf-8") as f:
        rows = list(csv.DictReader(f))
    if not rows:
        raise ValueError(f"{path}: không có dữ liệu")
    out: dict[str, np.ndarray] = {}
    for k in rows[0]:
        try:
            out[k] = np.array([float(r[k]) for r in rows])
        except ValueError:
            pass  # cột chữ (ví dụ tên sự kiện) bỏ qua
    return out


def band_rms(x: np.ndarray, fs: float, band: tuple[float, float]) -> float:
    """RMS của tín hiệu sau khi chỉ giữ dải tần band (lọc bằng FFT)."""
    x = np.asarray(x, dtype=float) - np.mean(x)
    spec = np.fft.rfft(x)
    freqs = np.fft.rfftfreq(len(x), 1.0 / fs)
    spec[(freqs < band[0]) | (freqs > band[1])] = 0
    y = np.fft.irfft(spec, n=len(x))
    return float(np.sqrt(np.mean(y**2)))


def tremor_dps(gyro: np.ndarray, fs: float = 100.0, window_s: float = 2.0,
               band: tuple[float, float] = TREMOR_BAND_HZ) -> float:
    """Chỉ số run tay (°/s): trung vị qua các cửa sổ 2 s của RMS dải 4-12 Hz, gộp 3 trục.

    gyro có dạng (N, 3). Dùng trung vị để một cú va chạm không làm hỏng cả buổi.
    Lưu ý: khối lượng tay cầm làm thay đổi phổ run, nên chỉ so sánh các lần đo
    trên CÙNG một tay cầm.
    """
    gyro = np.asarray(gyro, dtype=float)
    n = int(window_s * fs)
    if len(gyro) < n:
        n = len(gyro)
    vals = []
    for start in range(0, len(gyro) - n + 1, n // 2 or 1):
        seg = gyro[start:start + n]
        vals.append(math.sqrt(sum(band_rms(seg[:, k], fs, band) ** 2 for k in range(3))))
    return float(np.median(vals))


def dtw_distance(a: np.ndarray, b: np.ndarray, window: int | None = None) -> float:
    """Khoảng cách DTW chuẩn hóa theo độ dài đường đi (cùng đơn vị với tín hiệu).

    window: dải Sakoe-Chiba (số mẫu) để giới hạn lệch thời gian và tăng tốc.
    """
    a = np.asarray(a, dtype=float)
    b = np.asarray(b, dtype=float)
    n, m = len(a), len(b)
    w = max(window if window is not None else max(n, m), abs(n - m))
    cost = np.full((n + 1, m + 1), np.inf)
    steps = np.zeros((n + 1, m + 1))
    cost[0, 0] = 0.0
    for i in range(1, n + 1):
        for j in range(max(1, i - w), min(m, i + w) + 1):
            d = abs(a[i - 1] - b[j - 1])
            choices = (cost[i - 1, j - 1], cost[i - 1, j], cost[i, j - 1])
            k = int(np.argmin(choices))
            prev = ((i - 1, j - 1), (i - 1, j), (i, j - 1))[k]
            cost[i, j] = d + choices[k]
            steps[i, j] = steps[prev] + 1
    return float(cost[n, m] / steps[n, m])


def resample_by(x: np.ndarray, y: np.ndarray, grid: np.ndarray) -> np.ndarray:
    """Biểu diễn y theo x (ví dụ độ mở khóa theo thể tích đã chảy) trên lưới grid.

    So học sinh với giáo viên theo THỂ TÍCH thay vì theo thời gian: hai người rót
    nhanh chậm khác nhau vẫn so được "ở 2 ml cuối, mở khóa bao nhiêu".
    """
    x = np.asarray(x, dtype=float)
    y = np.asarray(y, dtype=float)
    xm = np.maximum.accumulate(x)  # thể tích chỉ tăng; bỏ nhiễu đi lùi
    keep = np.concatenate(([True], np.diff(xm) > 0))
    return np.interp(grid, xm[keep], y[keep])


def rate_near_endpoint(t_s: np.ndarray, v_ml: np.ndarray, v_eq_ml: float, last_ml: float = 2.0) -> float:
    """Tốc độ rót trung bình (ml/s) trong last_ml ml cuối trước điểm tương đương."""
    t_s = np.asarray(t_s, dtype=float)
    v_ml = np.asarray(v_ml, dtype=float)
    lo = v_eq_ml - last_ml
    i0 = int(np.searchsorted(np.maximum.accumulate(v_ml), lo))
    i1 = int(np.searchsorted(np.maximum.accumulate(v_ml), v_eq_ml))
    i1 = min(i1, len(v_ml) - 1)
    if i0 >= i1:
        return float("nan")
    return float((v_ml[i1] - v_ml[i0]) / (t_s[i1] - t_s[i0]))


def slowdown_ratio(t_s: np.ndarray, v_ml: np.ndarray, v_eq_ml: float, last_ml: float = 2.0) -> float:
    """Tốc độ ở last_ml cuối chia tốc độ trước đó. < 1 nghĩa là có giảm tốc (tốt)."""
    t_s = np.asarray(t_s, dtype=float)
    v_ml = np.asarray(v_ml, dtype=float)
    near = rate_near_endpoint(t_s, v_ml, v_eq_ml, last_ml)
    vm = np.maximum.accumulate(v_ml)
    i0 = int(np.searchsorted(vm, 0.5))  # bỏ lúc mới bắt đầu mở khóa
    i1 = int(np.searchsorted(vm, v_eq_ml - last_ml))
    if i1 <= i0 or t_s[i1] == t_s[i0]:
        return float("nan")
    before = (v_ml[i1] - v_ml[i0]) / (t_s[i1] - t_s[i0])
    return float(near / before) if before > 0 else float("nan")


def overshoot_drops(v_final_ml: float, v_eq_ml: float, drop_ml: float = 0.05) -> float:
    """Số giọt vượt điểm tương đương (âm nghĩa là dừng sớm)."""
    return (v_final_ml - v_eq_ml) / drop_ml


def summarize(data: dict[str, np.ndarray], v_eq_ml: float | None = None,
              reference: dict[str, np.ndarray] | None = None) -> dict[str, float]:
    t_s = (data["t_ms"] - data["t_ms"][0]) / 1000.0
    fs = 1.0 / float(np.median(np.diff(t_s)))
    gyro = np.column_stack([data["gx"], data["gy"], data["gz"]])
    out = {"thoi_gian_s": float(t_s[-1]), "tan_so_mau_hz": fs, "run_tay_dps": tremor_dps(gyro, fs)}
    if v_eq_ml is not None and "v_buret_ml" in data:
        v = data["v_buret_ml"]
        out["toc_do_2ml_cuoi_ml_s"] = rate_near_endpoint(t_s, v, v_eq_ml)
        out["ti_le_giam_toc"] = slowdown_ratio(t_s, v, v_eq_ml)
        out["so_giot_vuot"] = overshoot_drops(float(v[-1]), v_eq_ml)
    if reference is not None and "opening" in data and "opening" in reference and "v_buret_ml" in data:
        top = min(float(data["v_buret_ml"].max()), float(reference["v_buret_ml"].max()))
        grid = np.linspace(0, top, 200)
        mine = resample_by(data["v_buret_ml"], data["opening"], grid)
        ref = resample_by(reference["v_buret_ml"], reference["opening"], grid)
        out["lech_so_voi_chuan"] = dtw_distance(mine, ref, window=20)
    return out
