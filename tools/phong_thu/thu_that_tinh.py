"""Bài thử tích hợp với TAY CẦM THẬT nằm yên (không ai chạm): chạy đúng đường xử lý của phòng thử
(gói thô USB -> TrangThai -> ConTro (One Euro) -> CuChi -> luật tương tác) theo thời gian thực và đếm mọi thứ
lẽ ra phải đứng yên: sự kiện ma (chạm, cầm, nhấp, đổi chế độ), người tự đi / quay vì joystick, tia trôi và rung.
Đo thêm hiệu suất: số gói/giây, mất gói, tuổi dữ liệu, thời gian khứ hồi lệnh USB, thời gian xử lý mỗi khung.

    python tools/phong_thu/thu_that_tinh.py --com COM8 [--giay 180] [--ra thư_mục]

Mở cổng làm ESP32 khởi động lại và cân con quay 2 giây: tay cầm phải nằm yên từ trước khi chạy.
Ra: <thư mục>/that_tinh.csv (chỉ số), khung.csv (mỗi khung), su_kien.csv.
"""
import argparse
import csv
import math
import os
import sys
import time

os.environ.setdefault("SDL_VIDEODRIVER", "dummy")
DAY = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, DAY)

import numpy as np  # noqa: E402

import bai_thu  # noqa: E402
import chay  # noqa: E402
from nguon import NguonUSB  # noqa: E402
from xu_ly import joystick  # noqa: E402

DT = 1 / 60
CHO_ON_DINH = 4.0  # s bỏ qua đầu buổi (khởi động lại, cân con quay)


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--com", required=True)
    p.add_argument("--giay", type=float, default=180)
    p.add_argument("--ra")
    a = p.parse_args()
    sys.stdout.reconfigure(encoding="utf-8")
    ra = a.ra or os.path.join(chay.GOC, "du_lieu", "phong_thu", time.strftime("that_tinh_%Y%m%d_%H%M%S"))
    os.makedirs(ra, exist_ok=True)

    ng = NguonUSB(a.com)
    loi_nhan = []  # (thời điểm, dòng chữ) để đo khứ hồi lệnh
    bao_goc = ng._bao

    def bao(chu):
        loi_nhan.append((time.perf_counter(), chu))
        bao_goc(chu)
    ng._bao = bao

    app = chay.UngDung(ng, ra, hien=False)
    t_bd = time.perf_counter()
    while time.perf_counter() - t_bd < CHO_ON_DINH:
        time.sleep(0.05)
    # cảnh bài 6 (đi lại): joystick lệch thì người tự đi / quay, thấy ngay
    app.bat_dau_bai(bai_thu.BaiDi)
    app.bai.gioi_han_s = 1e9
    vi_tri0 = app.canh.cam_vi_tri.copy()
    yaw_cam0 = app.canh.cam_yaw

    khung = []
    khu_hoi = []
    lenh_cho = None
    lan_lenh = 2.0
    t0 = time.perf_counter()
    k = 0
    while True:
        bay_gio = time.perf_counter() - t0
        if bay_gio >= a.giay:
            break
        # khứ hồi: gửi "p", chờ dòng "#INFO" quay lại (gồm USB 2 chiều + hàng lệnh của firmware)
        if lenh_cho is None and bay_gio > lan_lenh:
            n_truoc = len(loi_nhan)
            lenh_cho = (time.perf_counter(), n_truoc)
            ng.gui("p\n")
        if lenh_cho is not None:
            for t_nhan, chu in loi_nhan[lenh_cho[1]:]:
                if "INFO" in chu:
                    khu_hoi.append((t_nhan - lenh_cho[0]) * 1000)
                    lenh_cho = None
                    lan_lenh = bay_gio + 10
                    break
            else:
                if time.perf_counter() - lenh_cho[0] > 2:
                    khu_hoi.append(float("nan"))
                    lenh_cho = None
                    lan_lenh = bay_gio + 10
        t_xl = time.perf_counter()
        app.buoc(DT)
        xu_ly_ms = (time.perf_counter() - t_xl) * 1000
        s = app.s
        jx, jy = joystick(*s.joy)
        khung.append((round(bay_gio, 3), round(xu_ly_ms, 3), round(min(ng.tre_ms(), 9999), 2), app.ct.ra[0], app.ct.ra[1],
                      app.ct.roll, s.joy[0], s.joy[1], jx, jy, s.muc_a, s.muc_b, int(s.cham_a), int(s.cham_b),
                      int(s.nhan_can), int(s.yen), round(s.nhiet, 2), app.canh.tuong_tac.che_do,
                      float(np.linalg.norm(app.canh.cam_vi_tri - vi_tri0)), app.canh.cam_yaw - yaw_cam0))
        k += 1
        cho = t0 + k * DT - time.perf_counter()
        if cho > 0:
            time.sleep(cho)
    ng.gui("r0\n")
    ng.dung()

    K = np.array([r[:17] for r in khung], float)
    tg, xl, tuoi, ry, rp = K[:, 0], K[:, 1], K[:, 2], K[:, 3], K[:, 4]
    jxr, jyr, jxo, jyo = K[:, 6], K[:, 7], K[:, 8], K[:, 9]
    n1 = np.searchsorted(tg, 1.0)
    # rung tia: độ lệch chuẩn trong từng cửa sổ 1 s, lấy trung vị (giống bài 1 "rung tia khi cầm yên")
    w = int(1 / DT)
    cs = [np.sqrt(ry[i:i + w].var() + rp[i:i + w].var()) for i in range(n1, len(ry) - w, w)]
    ev = [e for e in app.su_kien if e[1] != "bat_dau_bai"]
    loai = {}
    for e in ev:
        loai[e[1]] = loai.get(e[1], 0) + 1
    tk = np.array([r[1:4] for r in app.thong_ke if r[0] > 2], float) if app.thong_ke else np.zeros((1, 3))
    kh = np.array([x for x in khu_hoi if not math.isnan(x)]) if khu_hoi else np.array([])
    dong = [
        ("Thời gian đo", round(tg[-1], 1), "s", "tay cầm nằm yên, không ai chạm", None),
        ("Số gói nhận / giây", round(float(np.median(tk[:, 0])), 1), "gói/s", "4 mẫu 200 Hz mỗi gói ⇒ 50", tk[:, 0].min() > 45),
        ("Mất gói", round(float(tk[:, 1].mean()), 3), "%", "< 1% (PDF tr.8)", tk[:, 1].mean() < 1),
        ("Tuổi dữ liệu lúc dùng: trung vị", round(float(np.median(tuoi[n1:])), 1), "ms", "", None),
        ("Tuổi dữ liệu lúc dùng: P95", round(float(np.percentile(tuoi[n1:], 95)), 1), "ms", "< 30 ms", np.percentile(tuoi[n1:], 95) < 30),
        ("Khứ hồi lệnh USB (gửi p → nhận #INFO): trung vị", round(float(np.median(kh)), 1) if len(kh) else "-", "ms",
         f"{len(kh)}/{len(khu_hoi)} lần có trả lời", len(kh) == len(khu_hoi) and len(kh) > 0),
        ("Xử lý mỗi khung (luật + vẽ cảnh): trung vị", round(float(np.median(xl)), 2), "ms", "< 16,7 ms (60 khung/s)", np.median(xl) < 16.7),
        ("Xử lý mỗi khung: P99", round(float(np.percentile(xl, 99)), 2), "ms", "< 16,7 ms", np.percentile(xl, 99) < 16.7),
        ("Tia trôi yaw (cuối − đầu)", round(float(ry[-1] - ry[n1]), 3), "°", "< 1° / 10 phút", abs(ry[-1] - ry[n1]) < 1),
        ("Tia trôi pitch (cuối − đầu)", round(float(rp[-1] - rp[n1]), 3), "°", "", None),
        ("Rung tia khi nằm yên (trung vị cửa sổ 1 s)", round(float(np.median(cs)), 4), "°", "< 0,15° (PDF tr.8)", np.median(cs) < 0.15),
        ("Joystick thô X: trung bình ± độ lệch chuẩn", f"{jxr.mean():+.3f} ± {jxr.std():.3f}", "−1..1", "≈ 0", abs(jxr.mean()) < 0.08 and jxr.std() < 0.02),
        ("Joystick thô Y: trung bình ± độ lệch chuẩn", f"{jyr.mean():+.3f} ± {jyr.std():.3f}", "−1..1", "≈ 0", abs(jyr.mean()) < 0.08 and jyr.std() < 0.02),
        ("Khung joystick vượt vùng chết 8% (ma)", round(float(np.mean((jxo != 0) | (jyo != 0)) * 100), 1), "% khung", "0%", not np.any((jxo != 0) | (jyo != 0))),
        ("Người tự đi xa chỗ đứng", round(khung[-1][18], 3), "m", "0 m", khung[-1][18] < 0.01),
        ("Người tự quay", round(khung[-1][19], 1), "°", "0°", abs(khung[-1][19]) < 1),
        ("Mức cảm ứng A / B lớn nhất (0–255)", f"{int(K[:, 10].max())} / {int(K[:, 11].max())}", "", "< 38 (ngưỡng lơ lửng)", K[:, 10].max() < 38 and K[:, 11].max() < 38),
        ("Sự kiện ma (chạm, nhấp, cầm, đổi chế độ...)", len(ev), "lần", "0", len(ev) == 0),
        ("Tỉ lệ khung mạch báo 'nằm yên'", round(float(K[n1:, 15].mean() * 100), 1), "%", "≈ 100%", K[n1:, 15].mean() > 0.95),
    ]
    for ten, sl in sorted(loai.items()):
        dong.append((f"  trong đó: {ten}", sl, "lần", "0", False))
    with open(os.path.join(ra, "that_tinh.csv"), "w", newline="", encoding="utf-8-sig") as f:
        wr = csv.writer(f)
        wr.writerow(["Chỉ số", "Giá trị", "Đơn vị", "Tiêu chí", "Đạt"])
        for d in dong:
            wr.writerow(list(d[:4]) + ["" if d[4] is None else ("Đạt" if d[4] else "CHƯA")])
    with open(os.path.join(ra, "khung.csv"), "w", newline="", encoding="utf-8-sig") as f:
        wr = csv.writer(f)
        wr.writerow(["t (s)", "xử lý (ms)", "tuổi dữ liệu (ms)", "tia yaw", "tia pitch", "roll", "joy x thô", "joy y thô",
                     "joy x sau vùng chết", "joy y sau vùng chết", "mức A", "mức B", "chạm A", "chạm B", "nhấn cần",
                     "nằm yên", "nhiệt (°C)", "chế độ", "đi xa (m)", "quay (°)"])
        wr.writerows(khung)
    with open(os.path.join(ra, "su_kien.csv"), "w", newline="", encoding="utf-8-sig") as f:
        wr = csv.writer(f)
        wr.writerow(["t (s)", "sự kiện", "chi tiết"])
        wr.writerows(app.su_kien)
    for d in dong:
        print(f"{d[0]:52s} {str(d[1]):>18s} {d[2]:8s} {d[3]:28s} {'' if d[4] is None else ('Đạt' if d[4] else 'CHƯA')}")
    print(f"\nĐã lưu {ra}")


if __name__ == "__main__":
    main()
