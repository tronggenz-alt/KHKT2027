"""Hàm chọn webcam của máy tính và chặn camera điện thoại (Continuity, DroidCam, etc.).

Dùng chung cho cả tools/mediapipe_tracker.py và tools/head_tracker.py.
Quy tắc:
- Không bao giờ mở camera điện thoại, kể cả khi webcam máy hỏng hay bận.
- macOS: dùng AVFoundation (pyobjc) quét thiết bị, ưu tiên BuiltInWideAngleCamera.
  Loại bỏ AVCaptureDeviceTypeContinuityCamera và các camera có model/tên iPhone/iPad/Continuity.
- Windows: dùng DirectShow (pygrabber nếu có), loại bỏ tên chứa iPhone, Android, Phone,
  DroidCam, Iriun, Camo, EpocCam, OBS Virtual; ưu tiên camera tích hợp (Integrated, HD Webcam).
- Không tìm thấy webcam máy -> trả về None, không mở camera nào.
"""

from __future__ import annotations

import os
import sys
from typing import Any, Optional

TU_KHOA_CAM = [
    "iphone",
    "ipad",
    "ipod",
    "android",
    "phone",
    "điện thoại",
    "dien thoai",
    "liên kết",
    "lien ket",
    "continuity",
    "desk view",
    "droidcam",
    "iriun",
    "camo",
    "epoccam",
    "obs virtual",
]

TU_KHOA_UU_TIEN = [
    "integrated",
    "hd webcam",
    "facetime",
    "built-in",
    "internal",
]


def la_camera_dien_thoai_hoac_ao(ten: str, model_id: str = "", loai: str = "") -> bool:
    chuoi = f"{ten} {model_id} {loai}".lower()
    return any(tu in chuoi for tu in TU_KHOA_CAM)


def chon_webcam_may(cam_arg: str | int = "may") -> tuple[Optional[int], str, str]:
    """Chọn webcam máy tính hợp lệ.

    Trả về: (chi_so_opencv, ten_camera, unique_id_hoac_loi).
    Nếu không tìm thấy hoặc người dùng chọn nhầm camera điện thoại -> chi_so_opencv = None.
    """
    arg_str = str(cam_arg).strip().lower()
    la_tu_chon = arg_str in ("may", "auto", "default", "", "none")

    if sys.platform == "darwin":
        try:
            import AVFoundation
        except ImportError:
            # Fallback nếu thiếu pyobjc
            if not la_tu_chon:
                try:
                    return int(cam_arg), f"Camera {cam_arg}", "khong_ro"
                except ValueError:
                    return None, "", "chi_so_khong_hop_le"
            return 0, "Default Camera", "fallback"

        cv_devices = list(AVFoundation.AVCaptureDevice.devicesWithMediaType_(AVFoundation.AVMediaTypeVideo))
        if not cv_devices:
            return None, "", "khong_thay_thiet_bi_video_nao"

        # Quét phân loại chi tiết qua discovery session
        device_types = [
            AVFoundation.AVCaptureDeviceTypeBuiltInWideAngleCamera,
            AVFoundation.AVCaptureDeviceTypeExternal,
        ]
        if hasattr(AVFoundation, "AVCaptureDeviceTypeContinuityCamera"):
            device_types.append(AVFoundation.AVCaptureDeviceTypeContinuityCamera)

        session = AVFoundation.AVCaptureDeviceDiscoverySession.discoverySessionWithDeviceTypes_mediaType_position_(
            device_types,
            AVFoundation.AVMediaTypeVideo,
            AVFoundation.AVCaptureDevicePositionUnspecified,
        )
        disc_devices = list(session.devices())

        def _kiem_dien_thoai(d: Any) -> bool:
            name = str(d.localizedName())
            model = str(getattr(d, "modelID", lambda: "")())
            dtype = str(d.deviceType())
            return la_camera_dien_thoai_hoac_ao(name, model, dtype)

        # 1. Nếu người dùng chỉ định số cụ thể
        if not la_tu_chon:
            try:
                idx = int(cam_arg)
            except ValueError:
                return None, "", f"Tham so --cam không hợp lệ: {cam_arg}"

            if idx < 0 or idx >= len(cv_devices):
                return None, "", f"Chỉ số camera {idx} không tồn tại (có {len(cv_devices)} thiết bị)"

            target_dev = cv_devices[idx]
            if _kiem_dien_thoai(target_dev):
                return None, str(target_dev.localizedName()), "da_chan_camera_dien_thoai"
            return idx, str(target_dev.localizedName()), str(target_dev.uniqueID())

        # 2. Tự động chọn webcam máy:
        # Ưu tiên 1: BuiltInWideAngleCamera
        builtin = [
            d for d in disc_devices
            if d.deviceType() == AVFoundation.AVCaptureDeviceTypeBuiltInWideAngleCamera
            and not _kiem_dien_thoai(d)
        ]
        if builtin:
            chosen = builtin[0]
        else:
            # Ưu tiên 2: External webcam chuẩn (không phải điện thoại / continuity)
            externals = [
                d for d in disc_devices
                if d.deviceType() == AVFoundation.AVCaptureDeviceTypeExternal
                and not _kiem_dien_thoai(d)
            ]
            if externals:
                chosen = externals[0]
            else:
                return None, "", "khong_thay_webcam_may"

        # Đổi uniqueID sang chỉ số OpenCV
        chosen_uid = str(chosen.uniqueID())
        for idx, dev in enumerate(cv_devices):
            if str(dev.uniqueID()) == chosen_uid:
                return idx, str(chosen.localizedName()), chosen_uid

        return None, "", "khong_khop_chi_so_opencv"

    if os.name == "nt":
        # Windows DirectShow
        devices_names: list[str] = []
        try:
            from pygrabber.dshow_graph import FilterGraph
            graph = FilterGraph()
            devices_names = graph.get_input_devices()
        except Exception:
            pass

        if not devices_names:
            # Fallback nếu không có pygrabber
            if not la_tu_chon:
                try:
                    return int(cam_arg), f"Camera {cam_arg}", "dshow"
                except ValueError:
                    return None, "", "chi_so_khong_hop_le"
            return 0, "Default Windows Camera", "dshow_fallback"

        if not la_tu_chon:
            try:
                idx = int(cam_arg)
            except ValueError:
                return None, "", f"Tham số --cam không hợp lệ: {cam_arg}"

            if idx < 0 or idx >= len(devices_names):
                return None, "", f"Chỉ số {idx} không tồn tại (có {len(devices_names)} thiết bị)"

            ten = devices_names[idx]
            if la_camera_dien_thoai_hoac_ao(ten):
                return None, ten, "da_chan_camera_dien_thoai"
            return idx, ten, "windows_dshow"

        # Tự động chọn trên Windows
        # Ưu tiên camera tích hợp
        for idx, ten in enumerate(devices_names):
            if not la_camera_dien_thoai_hoac_ao(ten) and any(u in ten.lower() for u in TU_KHOA_UU_TIEN):
                return idx, ten, "windows_integrated"

        # Nếu không có tên tích hợp rõ ràng, lấy camera đầu tiên không phải điện thoại
        for idx, ten in enumerate(devices_names):
            if not la_camera_dien_thoai_hoac_ao(ten):
                return idx, ten, "windows_external"

        return None, "", "khong_thay_webcam_may"

    # Linux / hệ điều hành khác
    if not la_tu_chon:
        try:
            return int(cam_arg), f"Camera {cam_arg}", "linux"
        except ValueError:
            return None, "", "chi_so_khong_hop_le"
    return 0, "Default Linux Camera", "linux"
