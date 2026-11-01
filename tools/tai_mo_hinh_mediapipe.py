"""Tải các mô hình MediaPipe chính thức của Google vào tools/mo_hinh/.

Bao gồm:
- face_landmarker.task (478 điểm mặt, 10 điểm tròng mắt, blendshapes, ma trận tư thế 4x4)
- gesture_recognizer.task (21 điểm mỗi bàn tay, tay trái/phải, 7 cử chỉ dựng sẵn)
- hand_landmarker.task (21 điểm bàn tay, dùng để đối chiếu tốc độ)

Nguồn chính thức: Google MediaPipe Models (Apache 2.0).
"""

from __future__ import annotations

import hashlib
import sys
import urllib.request
from pathlib import Path

THU_MUC_MO_HINH = Path(__file__).resolve().parent / "mo_hinh"

DANH_SACH_MO_HINH = {
    "face_landmarker.task": {
        "url": "https://storage.googleapis.com/mediapipe-models/face_landmarker/face_landmarker/float16/latest/face_landmarker.task",
        "sha256": "64184e229b263107bc2b804c6625db1341ff2bb731874b0bcc2fe6544e0bc9ff",
        "mo_ta": "Mô hình Face Landmarker (478 điểm mặt, tròng mắt, blendshapes, ma trận 4x4)",
    },
    "gesture_recognizer.task": {
        "url": "https://storage.googleapis.com/mediapipe-models/gesture_recognizer/gesture_recognizer/float16/latest/gesture_recognizer.task",
        "sha256": "4bbf16c61556637e192ff411a437e4da884325a7daee3bf45ad99596350d75a8",
        "mo_ta": "Mô hình Gesture Recognizer (21 điểm bàn tay + nhận dạng 7 cử chỉ)",
    },
    "hand_landmarker.task": {
        "url": "https://storage.googleapis.com/mediapipe-models/hand_landmarker/hand_landmarker/float16/latest/hand_landmarker.task",
        "sha256": "fbc2a30080c3c557093b5ddfc334698132eb341044ccee322ccf8bcf3607cde1",
        "mo_ta": "Mô hình Hand Landmarker (21 điểm bàn tay)",
    },
}


def tinh_sha256(duong_dan: Path) -> str:
    h = hashlib.sha256()
    with open(duong_dan, "rb") as f:
        while chunk := f.read(65536):
            h.update(chunk)
    return h.hexdigest()


def tai_tep(url: str, dich: Path) -> str:
    dich.parent.mkdir(parents=True, exist_ok=True)
    tam = dich.with_suffix(".tmp")
    print(f"Đang tải {dich.name} từ {url}...", flush=True)
    req = urllib.request.Request(url, headers={"User-Agent": "Antigravity-MediaPipe/1.0"})
    with urllib.request.urlopen(req) as resp, open(tam, "wb") as f:
        da_tai = 0
        tong = int(resp.headers.get("Content-Length", 0))
        while chunk := resp.read(65536):
            f.write(chunk)
            da_tai += len(chunk)
            if tong > 0:
                phan_tram = (da_tai / tong) * 100
                print(f"\r  {dich.name}: {da_tai:,} / {tong:,} byte ({phan_tram:.1f}%)", end="", flush=True)
    print()
    tam.replace(dich)
    sha = tinh_sha256(dich)
    print(f"  ✓ Đã lưu {dich.name}: {dich.stat().st_size:,} byte, SHA-256: {sha}")
    return sha


def kiem_tra_va_tai_tat_ca() -> dict[str, tuple[int, str]]:
    THU_MUC_MO_HINH.mkdir(parents=True, exist_ok=True)
    ket_qua = {}
    for ten, thong_tin in DANH_SACH_MO_HINH.items():
        dich = THU_MUC_MO_HINH / ten
        if dich.exists() and dich.stat().st_size > 0:
            sha = tinh_sha256(dich)
            print(f"Đã có {ten} ({dich.stat().st_size:,} byte, SHA-256: {sha[:16]}...)")
            ket_qua[ten] = (dich.stat().st_size, sha)
        else:
            sha = tai_tep(thong_tin["url"], dich)
            ket_qua[ten] = (dich.stat().st_size, sha)
    return ket_qua


if __name__ == "__main__":
    print("=== TẢI VÀ KIỂM TRA MÔ HÌNH MEDIAPIPE ===")
    kq = kiem_tra_va_tai_tat_ca()
    print("Hoàn tất tải toàn bộ mô hình.")
