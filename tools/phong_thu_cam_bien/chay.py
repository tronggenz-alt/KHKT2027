"""Mở phòng thử cảm biến VERA: chạy máy chủ web cục bộ (Web Serial cần http://127.0.0.1) rồi mở Edge/Chrome.

Chạy:  python tools/phong_thu_cam_bien/chay.py        (hoặc bấm đúp chay.bat)
Thêm  --khong-mo  để chỉ chạy máy chủ.
"""
import functools
import http.server
import os
import shutil
import subprocess
import sys
import threading
import webbrowser

THU_MUC = os.path.dirname(os.path.abspath(__file__))
CONG = 8765


class XuLy(http.server.SimpleHTTPRequestHandler):
    extensions_map = {**http.server.SimpleHTTPRequestHandler.extensions_map, ".js": "text/javascript", ".bin": "application/octet-stream"}

    def log_message(self, *a):
        pass


def mo_trinh_duyet(url):
    """Ưu tiên Edge hoặc Chrome vì Firefox chưa có Web Serial."""
    ung_vien = [r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe", r"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
                r"C:\Program Files\Google\Chrome\Application\chrome.exe", os.path.expandvars(r"%LOCALAPPDATA%\Google\Chrome\Application\chrome.exe"),
                shutil.which("msedge") or "", shutil.which("chrome") or ""]
    for p in ung_vien:
        if p and os.path.exists(p):
            subprocess.Popen([p, "--new-window", url])
            return
    webbrowser.open(url)


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    may = http.server.ThreadingHTTPServer(("127.0.0.1", CONG), functools.partial(XuLy, directory=THU_MUC))
    url = f"http://127.0.0.1:{CONG}/"
    print(f"Phòng thử cảm biến VERA: {url}\nĐóng cửa sổ này để tắt.")
    if "--khong-mo" not in sys.argv:
        threading.Timer(0.6, mo_trinh_duyet, args=(url,)).start()
    try:
        may.serve_forever()
    except KeyboardInterrupt:
        pass


if __name__ == "__main__":
    main()
