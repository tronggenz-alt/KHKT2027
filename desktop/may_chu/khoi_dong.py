#!/usr/bin/env python3
"""
desktop/may_chu/khoi_dong.py
Khởi chạy máy chủ backend Uvicorn tại cổng trống 127.0.0.1 cho ứng dụng desktop CHEMISTRY.
Báo cổng ra stdout: CONG=<cổng>
"""
import argparse
import os
import signal
import socket
import sys
import uvicorn
from pathlib import Path

# Đảm bảo đường dẫn gốc của dự án hoặc gói PyInstaller nằm trong sys.path
_HIEN_TAI = Path(__file__).resolve().parent
_GOC_DU_AN = _HIEN_TAI.parent.parent

if getattr(sys, 'frozen', False):
    # Đang chạy từ bản đóng gói PyInstaller (_MEIPASS)
    _ROOT_DIR = Path(sys._MEIPASS)
else:
    _ROOT_DIR = _GOC_DU_AN

if str(_ROOT_DIR) not in sys.path:
    sys.path.insert(0, str(_ROOT_DIR))


def tim_cong_trong(host: str = "127.0.0.1") -> int:
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        s.bind((host, 0))
        s.listen(1)
        return s.getsockname()[1]


def main():
    parser = argparse.ArgumentParser(description="Khởi động máy chủ CHEMISTRY cục bộ cho desktop")
    parser.add_argument("--storage-dir", type=str, default="", help="Thư mục lưu trữ dữ liệu người dùng")
    parser.add_argument("--port", type=int, default=0, help="Cổng cố định (0 = tự động tìm cổng trống)")
    args = parser.parse_args()

    # 1. Cấu hình biến môi trường cốt lõi của desktop app
    os.environ["CHE_DO_APP"] = "1"
    os.environ["CHE_DO_CUC_BO"] = "1"
    os.environ["COOKIE_SECURE"] = "0"

    # Thư mục lưu trữ
    if args.storage_dir:
        storage_path = Path(args.storage_dir).resolve()
    else:
        # Mặc định theo hệ điều hành
        if sys.platform == "darwin":
            storage_path = Path.home() / "Library" / "Application Support" / "CHEMISTRY"
        elif sys.platform == "win32":
            storage_path = Path(os.environ.get("APPDATA", Path.home())) / "CHEMISTRY"
        else:
            storage_path = Path.home() / ".local" / "share" / "CHEMISTRY"

    storage_path.mkdir(parents=True, exist_ok=True)
    (storage_path / "data").mkdir(parents=True, exist_ok=True)
    (storage_path / "outputs").mkdir(parents=True, exist_ok=True)
    (storage_path / "logs").mkdir(parents=True, exist_ok=True)

    os.environ["STORAGE_DIR"] = str(storage_path)

    # 2. Xác định cổng lắng nghe
    port = args.port if args.port > 0 else tim_cong_trong("127.0.0.1")

    # In ra stdout để Electron main.js đọc
    print(f"CONG={port}", flush=True)
    print(f"STORAGE_DIR={storage_path}", flush=True)
    print("STATUS=STARTING", flush=True)

    # 3. Xử lý tín hiệu tắt an toàn
    def xu_ly_tin_hieu(sig, frame):
        print(f"STATUS=SHUTTING_DOWN signal={sig}", flush=True)
        sys.exit(0)

    signal.signal(signal.SIGINT, xu_ly_tin_hieu)
    signal.signal(signal.SIGTERM, xu_ly_tin_hieu)

    # 4. Chạy Uvicorn
    # Import sau khi đã thiết lập biến môi trường
    from backend.main import app

    uvicorn.run(
        app,
        host="127.0.0.1",
        port=port,
        log_level="info",
        access_log=False
    )


if __name__ == "__main__":
    main()
