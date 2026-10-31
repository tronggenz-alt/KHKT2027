#!/usr/bin/env python3
"""Thu thập dữ liệu ảnh cử chỉ tay cho MediaPipe Model Maker phòng thí nghiệm ảo.

Cách dùng:
    .venv/bin/python tools/thu_cu_chi.py [--cam may|so] [--crop]

Phím điều khiển:
    1: Cầm ống nghiệm (cam_ong_nghiem)
    2: Rót chất lỏng (rot_chat_long)
    3: Vặn núm / khóa buret (van_khoa_buret)
    4: Giơ 2 ngón chọn (gio_hai_ngon)
    0: Không cử chỉ / nền (none)
    c: Bật / tắt chế độ chụp liên tục khi giữ phím
    q hoặc Esc: Thoát
"""

import argparse
import os
import sys
import time
from pathlib import Path

import cv2

THU_MUC_GOC = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(THU_MUC_GOC / "tools"))
import chon_camera as cc

THU_MUC_DU_LIEU = THU_MUC_GOC / "data" / "du_lieu_cu_chi"

NHAN_CU_CHI = {
    ord("1"): "cam_ong_nghiem",
    ord("2"): "rot_chat_long",
    ord("3"): "van_khoa_buret",
    ord("4"): "gio_hai_ngon",
    ord("0"): "none",
}


def tao_thu_muc():
    for ten in NHAN_CU_CHI.values():
        (THU_MUC_DU_LIEU / ten).mkdir(parents=True, exist_ok=True)


def dem_anh_hien_co() -> dict[str, int]:
    dem = {}
    for ten in NHAN_CU_CHI.values():
        p = THU_MUC_DU_LIEU / ten
        if p.exists():
            dem[ten] = len(list(p.glob("*.jpg")))
        else:
            dem[ten] = 0
    return dem


def main():
    parser = argparse.ArgumentParser(description="Thu thập cử chỉ tay cho MediaPipe Model Maker")
    parser.add_argument("--cam", default="may", help="Chỉ định webcam ('may' hoặc chỉ số)")
    parser.add_argument("--lien-tuc", action="store_true", help="Chụp liên tục mỗi 100ms")
    args = parser.parse_args()

    tao_thu_muc()
    dem_anh = dem_anh_hien_co()

    idx_cam, ten_cam, _ = cc.chon_webcam_may(args.cam)
    if idx_cam is None:
        print(f"Lỗi: Không tìm thấy camera phù hợp: {ten_cam}")
        return 1

    print(f"Mở camera: {ten_cam} (index {idx_cam})")
    cap = cv2.VideoCapture(idx_cam)
    if not cap.isOpened():
        print(f"Lỗi: Không thể mở camera index {idx_cam}")
        return 1

    cap.set(cv2.CAP_PROP_FRAME_WIDTH, 640)
    cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 480)

    print("=== BẮT ĐẦU THU DỮ LIỆU CỬ CHỈ PHÒNG LAB ===")
    print("  1: cam_ong_nghiem  |  2: rot_chat_long")
    print("  3: van_khoa_buret  |  4: gio_hai_ngon")
    print("  0: none            |  q: Thoát")

    lien_tuc = args.lien_tuc
    nhan_hien_tai = None
    lan_chup_cuoi = 0.0

    while True:
        ret, frame = cap.read()
        if not ret:
            time.sleep(0.01)
            continue

        h, w = frame.shape[:2]
        now = time.time()

        key = cv2.waitKey(1) & 0xFF
        if key in (27, ord("q")):
            break
        elif key == ord("c"):
            lien_tuc = not lien_tuc
        elif key in NHAN_CU_CHI:
            nhan_hien_tai = NHAN_CU_CHI[key]
            # Chụp 1 ảnh ngay khi bấm
            fn = THU_MUC_DU_LIEU / nhan_hien_tai / f"{int(now*1000)}.jpg"
            cv2.imwrite(str(fn), frame)
            dem_anh[nhan_hien_tai] += 1
            lan_chup_cuoi = now

        # Nếu đang ở chế độ chụp liên tục
        if lien_tuc and nhan_hien_tai and (now - lan_chup_cuoi >= 0.15):
            fn = THU_MUC_DU_LIEU / nhan_hien_tai / f"{int(now*1000)}.jpg"
            cv2.imwrite(str(fn), frame)
            dem_anh[nhan_hien_tai] += 1
            lan_chup_cuoi = now

        # Hiển thị thông tin lên khung hình
        cv2.putText(
            frame,
            f"Thu cu chi: [1..4, 0] | Che do: {'Lien tuc' if lien_tuc else 'Tung anh'}",
            (10, 25),
            cv2.FONT_HERSHEY_SIMPLEX,
            0.6,
            (0, 255, 0),
            2,
        )

        y = 50
        for ten, count in dem_anh.items():
            color = (0, 255, 255) if ten == nhan_hien_tai else (200, 200, 200)
            cv2.putText(
                frame,
                f"  {ten}: {count} anh",
                (10, y),
                cv2.FONT_HERSHEY_SIMPLEX,
                0.5,
                color,
                1,
            )
            y += 20

        cv2.imshow("Thu cu chi phong lab (MediaPipe)", frame)

    cap.release()
    cv2.destroyAllWindows()
    print("\nTổng kết dữ liệu đã thu thập:")
    for ten, count in dem_anh.items():
        print(f"  - {ten}: {count} ảnh")
    return 0


if __name__ == "__main__":
    sys.exit(main())
