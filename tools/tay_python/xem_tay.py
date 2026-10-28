"""Xem tay qua camera: vẽ 21 điểm khớp của bàn tay và báo khi chụm ngón cái + ngón trỏ ("chạm").

Chỉ dùng camera của máy Mac (ưu tiên camera FaceTime HD), KHÔNG BAO GIỜ mở camera iPhone/iPad
nối qua Camera Liên tục (Continuity Camera), kể cả khi điện thoại đang ở gần.

Chạy (trong terminal đã có quyền camera):
    source ~/hand3d-env/bin/activate && python ~/Downloads/hand3d/xem_tay.py
Phím trong cửa sổ:  q / Esc = thoát · n = bật/tắt số thứ tự điểm
"""
import json
import math
import subprocess
import sys
import time
from pathlib import Path

import cv2
import mediapipe as mp
from mediapipe.tasks.python import BaseOptions, vision

# Mô hình: tìm cạnh file này trước, không có thì lấy ở ~/Downloads
MO_HINH = next((p for p in (Path(__file__).with_name("hand_landmarker.task"),
                            Path.home() / "Downloads" / "hand_landmarker.task") if p.exists()), None)

# Các đoạn nối 21 điểm thành khung bàn tay (0 = cổ tay, 4 = đầu ngón cái, 8 = đầu ngón trỏ, ...)
NOI = [(0, 1), (1, 2), (2, 3), (3, 4), (0, 5), (5, 6), (6, 7), (7, 8), (5, 9), (9, 10), (10, 11), (11, 12),
       (9, 13), (13, 14), (14, 15), (15, 16), (13, 17), (0, 17), (17, 18), (18, 19), (19, 20)]
DAU_NGON = (4, 8, 12, 16, 20)

CHAM_BAT, CHAM_NHA = 0.32, 0.45  # tỉ lệ chụm < 0,32 là chạm, > 0,45 là nhả (có khoảng trễ để không nhấp nháy)
# 26/10/2026: 0,25 / 0,40 → 0,32 / 0,45 – người dùng thật chụm đo được ~0,20, sát ngưỡng cũ nên "chụm khó ăn"
RONG_NHAN_DIEN = 640             # thu nhỏ hình trước khi nhận diện: nhanh hơn nhiều, độ chính xác như nhau
TEN_TAY = {"Left": "Tay trai", "Right": "Tay phai"}
DIEN_THOAI = ("iphone", "ipad")  # mã máy / tên camera chứa các chữ này là camera điện thoại → bỏ qua

XANH, CAM, TRANG, DEN = (90, 220, 120), (0, 160, 255), (255, 255, 255), (20, 20, 20)


def tao_may_nhan_dien():
    if MO_HINH is None:
        sys.exit("Không thấy hand_landmarker.task (cạnh file này hoặc trong ~/Downloads). Tải lại theo hướng dẫn.")
    return vision.HandLandmarker.create_from_options(vision.HandLandmarkerOptions(
        base_options=BaseOptions(model_asset_path=str(MO_HINH)),
        running_mode=vision.RunningMode.VIDEO,
        num_hands=2,
        min_hand_detection_confidence=0.6,
        min_hand_presence_confidence=0.6,
        min_tracking_confidence=0.6,
    ))


def mo_camera(so):
    cam = cv2.VideoCapture(so)
    cam.set(cv2.CAP_PROP_FRAME_WIDTH, 1280)
    cam.set(cv2.CAP_PROP_FRAME_HEIGHT, 720)
    for _ in range(20):  # camera cần vài khung để khởi động
        ok, _ = cam.read()
        if ok:
            return cam
        time.sleep(0.1)
    cam.release()
    return None


def danh_sach_camera():
    """Camera theo đúng số thứ tự OpenCV dùng trên macOS: OpenCV xếp camera theo Unique ID, nên iPhone
    có thể chiếm số 0 khi ở gần. Trả về [(số, tên, là_điện_thoại)], hoặc None nếu không đọc được."""
    try:
        ra = subprocess.run(["system_profiler", "SPCameraDataType", "-json"],
                            capture_output=True, text=True, timeout=15).stdout
        ds = sorted(json.loads(ra)["SPCameraDataType"], key=lambda c: c.get("spcamera_unique-id", ""))
    except (OSError, ValueError, KeyError, subprocess.SubprocessError):
        return None
    return [(i, c.get("_name", "?"),
             any(t in (c.get("spcamera_model-id", "") + " " + c.get("_name", "")).lower() for t in DIEN_THOAI))
            for i, c in enumerate(ds)]


def tim_camera(lan_thu=3):
    """Mở camera của Mac (FaceTime HD trước). Trả về (số, tên, camera) hoặc (None, None, None)."""
    ds = danh_sach_camera()
    if ds is None:
        print("Không đọc được danh sách camera của macOS, thử lần lượt camera số 0, 1, 2.")
        ds = [(i, f"camera {i}", False) for i in range(3)]
    for _, ten, la_dt in ds:
        if la_dt:
            print(f"Bỏ qua camera điện thoại: {ten}")
    cac_so = sorted((so for so, _, la_dt in ds if not la_dt), key=lambda so: "facetime" not in ds[so][1].lower())
    for so in cac_so:
        cam = mo_camera(so)
        if cam is None:
            continue
        # Điện thoại vừa kết nối / ngắt đúng lúc đang mở thì số thứ tự bị xô lệch → kiểm tra lại cho chắc
        sau = danh_sach_camera()
        if sau is not None and (so >= len(sau) or sau[so][2] or sau[so][1] != ds[so][1]):
            cam.release()
            return tim_camera(lan_thu - 1) if lan_thu > 1 else (None, None, None)
        return so, ds[so][1], cam
    return None, None, None


def ti_le_chum(diem, the_gioi, ti_le_khung):
    """Khoảng cách đầu ngón cái–ngón trỏ chia cho cỡ bàn tay (cổ tay → gốc ngón giữa).
    Dùng toạ độ 3D tính bằng mét nếu có, nên không phụ thuộc tay xa hay gần, nghiêng hay thẳng."""
    if the_gioi:
        a, b, c0, c9 = (the_gioi[i] for i in (4, 8, 0, 9))
        kc = lambda p, q: math.dist((p.x, p.y, p.z), (q.x, q.y, q.z))
    else:
        a, b, c0, c9 = (diem[i] for i in (4, 8, 0, 9))
        kc = lambda p, q: math.hypot((p.x - q.x) * ti_le_khung, p.y - q.y)
    co_tay = kc(c0, c9)
    return kc(a, b) / co_tay if co_tay > 1e-6 else 9.0


def chu(khung, noi_dung, x, y, mau=TRANG, co=0.6):
    (w, h), _ = cv2.getTextSize(noi_dung, cv2.FONT_HERSHEY_SIMPLEX, co, 2)
    cv2.rectangle(khung, (x - 4, y - h - 6), (x + w + 4, y + 6), DEN, -1)
    cv2.putText(khung, noi_dung, (x, y), cv2.FONT_HERSHEY_SIMPLEX, co, mau, 2, cv2.LINE_AA)


def ve_tay(khung, diem, mau, hien_so):
    h, w = khung.shape[:2]
    px = [(round(d.x * w), round(d.y * h)) for d in diem]
    for a, b in NOI:
        cv2.line(khung, px[a], px[b], mau, 2, cv2.LINE_AA)
    for i, p in enumerate(px):
        cv2.circle(khung, p, 7 if i in DAU_NGON else 4, mau if i in DAU_NGON else TRANG, -1, cv2.LINE_AA)
        if hien_so:
            cv2.putText(khung, str(i), (p[0] + 6, p[1] - 6), cv2.FONT_HERSHEY_SIMPLEX, 0.4, TRANG, 1, cv2.LINE_AA)
    return px


def nhan_dien(may, khung, ms):
    """Chạy MediaPipe trên một khung BGR (đã thu nhỏ cho nhanh). Trả về (kết quả, ms nhận diện)."""
    h, w = khung.shape[:2]
    nho = cv2.resize(khung, (RONG_NHAN_DIEN, round(h * RONG_NHAN_DIEN / w)))
    anh = mp.Image(image_format=mp.ImageFormat.SRGB, data=cv2.cvtColor(nho, cv2.COLOR_BGR2RGB))
    t0 = time.perf_counter()
    kq = may.detect_for_video(anh, ms)
    return kq, (time.perf_counter() - t0) * 1000


def xu_ly(may, khung, ms, dang_cham, hien_so):
    """Nhận diện tay trên một khung (đã lật gương) rồi vẽ lên chính khung đó. Trả về (số tay, ms nhận diện)."""
    h, w = khung.shape[:2]
    kq, ms_nhan_dien = nhan_dien(may, khung, ms)

    thay = set()
    for i, diem in enumerate(kq.hand_landmarks):
        ben = kq.handedness[i][0].category_name if kq.handedness and kq.handedness[i] else f"Tay{i}"
        thay.add(ben)
        the_gioi = kq.hand_world_landmarks[i] if kq.hand_world_landmarks else None
        tl = ti_le_chum(diem, the_gioi, w / h)

        cham = dang_cham.get(ben, False)
        if not cham and tl < CHAM_BAT:
            cham = True
            print(f"{TEN_TAY.get(ben, ben)}: CHẠM")
        elif cham and tl > CHAM_NHA:
            cham = False
            print(f"{TEN_TAY.get(ben, ben)}: nhả")
        dang_cham[ben] = cham

        px = ve_tay(khung, diem, CAM if cham else XANH, hien_so)

        # Vòng tròn ở điểm chụm: đầy dần khi chụm lại, thành chấm đặc khi "chạm"
        tam = ((px[4][0] + px[8][0]) // 2, (px[4][1] + px[8][1]) // 2)
        do_chum = min(1.0, max(0.0, (0.7 - tl) / (0.7 - CHAM_BAT)))
        if cham:
            cv2.circle(khung, tam, 14, CAM, -1, cv2.LINE_AA)
        else:
            cv2.circle(khung, tam, 18, TRANG, 1, cv2.LINE_AA)
            cv2.ellipse(khung, tam, (18, 18), -90, 0, 360 * do_chum, XANH, 3, cv2.LINE_AA)

        nhan = f"{TEN_TAY.get(ben, ben)}  chum {tl:.2f}" + ("  CHAM" if cham else "")
        chu(khung, nhan, px[0][0] - 60, min(h - 10, px[0][1] + 40), CAM if cham else TRANG)

    for ben in list(dang_cham):  # tay biến mất khỏi khung hình thì coi như nhả
        if ben not in thay and dang_cham[ben]:
            print(f"{TEN_TAY.get(ben, ben)}: nhả (mất tay)")
            dang_cham[ben] = False
    return len(kq.hand_landmarks), ms_nhan_dien


def main():
    may = tao_may_nhan_dien()
    so_cam, ten_cam, cam = tim_camera()
    if cam is None:
        sys.exit("Không mở được camera của Mac. Kiểm tra quyền camera của app terminal đang dùng "
                 "(Cài đặt hệ thống → Quyền riêng tư & Bảo mật → Camera), hoặc chạy trong app Terminal của Mac.")
    print(f"Đang dùng: {ten_cam} (số {so_cam}). Phím: q = thoát, n = số điểm.")
    ten_hud = ten_cam.encode("ascii", "ignore").decode().strip() or f"camera {so_cam}"  # chữ trên hình chỉ hiện được ASCII

    cua_so = "Xem tay (q: thoat)"
    cv2.namedWindow(cua_so, cv2.WINDOW_NORMAL)
    bat_dau, ms_truoc, fps, luc_truoc = time.perf_counter(), -1, 0.0, time.perf_counter()
    dang_cham, hien_so = {}, False
    try:
        while True:
            ok, khung = cam.read()
            if not ok:
                print("Mất hình từ camera, đang thử mở lại…")
                cam.release()
                so_cam, ten_cam, cam = tim_camera()
                if cam is None:
                    sys.exit("Không mở lại được camera của Mac.")
                continue

            khung = cv2.flip(khung, 1)  # lật gương: giơ tay phải thì hình hiện bên phải, như soi gương
            ms = max(int((time.perf_counter() - bat_dau) * 1000), ms_truoc + 1)  # mốc thời gian phải tăng dần
            ms_truoc = ms
            so_tay, ms_nd = xu_ly(may, khung, ms, dang_cham, hien_so)

            bay_gio = time.perf_counter()
            fps = 0.9 * fps + 0.1 / max(bay_gio - luc_truoc, 1e-6)
            luc_truoc = bay_gio
            chu(khung, f"FPS {fps:.0f} | nhan dien {ms_nd:.0f} ms | {so_tay} tay | {ten_hud}", 14, 30)
            chu(khung, "Chum ngon cai + ngon tro de CHAM   |   q: thoat  n: so diem", 14, 62, co=0.5)

            cv2.imshow(cua_so, khung)
            phim = cv2.waitKey(1) & 0xFF
            if phim in (ord("q"), 27) or cv2.getWindowProperty(cua_so, cv2.WND_PROP_VISIBLE) < 1:
                break
            if phim == ord("n"):
                hien_so = not hien_so
    finally:
        if cam is not None:
            cam.release()
        may.close()
        cv2.destroyAllWindows()


if __name__ == "__main__":
    main()
