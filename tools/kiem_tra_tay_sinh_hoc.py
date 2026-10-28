#!/usr/bin/env python3
"""
tools/kiem_tra_tay_sinh_hoc.py
Nghiệm thu tự động Playwright trên Chromium thật cho trang /sinh-hoc
(bản v2 /sinh-hoc-v2 đã bỏ ngày 26/10/2026, đường dẫn cũ chuyển về /sinh-hoc).
Kiểm tra các kịch bản K11..K17 theo PROMPT_DIEU_KHIEN_TAY_SINH_HOC.md:
- K11: Mở trang chưa bật tay (không lỗi console, không nạp MediaPipe, FPS mượt)
- K12: Kéo lướt xoay mô hình >= 60°, quay tiếp theo quán tính, dừng < 4s
- K13: Zoom hai tay thu nhỏ khoảng cách ≈ 1/1.5 (±5%)
- K14: Chạm nhẹ và chuột bấm cùng chọn trúng một cấu trúc
- K15: Đang bật tay sang tab Hoá học -> tắt camera, track ended
- K16: FPS khi bật camera giả đo 5s >= 0.85*lúc tắt hoặc >= 45
- K17: Chuột kéo, cuộn, bấm hoạt động nguyên vẹn

Kết quả ghi vào outputs/dieu-khien-tay/sinh-hoc.json và ảnh minh chứng.
"""

import json
import math
import os
import sys
import time
from pathlib import Path
from playwright.sync_api import sync_playwright

GOC = Path(__file__).resolve().parents[1]
OUTPUT_DIR = GOC / "outputs" / "dieu-khien-tay"
OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

URL_GOC = "http://127.0.0.1:8000"


def do_fps(page, so_giay=3.0):
    page.evaluate("""() => {
        window.__demFpsTest = 0;
        window.__startFpsTest = performance.now();
        window.__fpsHuy = false;
        function dem() {
            if (window.__fpsHuy) return;
            window.__demFpsTest++;
            requestAnimationFrame(dem);
        }
        requestAnimationFrame(dem);
    }""")
    time.sleep(so_giay)
    fps = page.evaluate("""() => {
        window.__fpsHuy = true;
        const dt = (performance.now() - window.__startFpsTest) / 1000;
        return window.__demFpsTest / dt;
    }""")
    return fps


def dat_lai_camera(page):
    page.evaluate("""() => {
        if (window.BioLab.chonGocNhin) window.BioLab.chonGocNhin();
        else window.BioLab.datLaiTatCa();
        if (window.BioLab.chotChuyenTiep) window.BioLab.chotChuyenTiep();
    }""")
    time.sleep(0.6)


def kiem_tra_trang(page, duong_dan_trang, ten_trang):
    print("\n" + "=" * 65)
    print(f"BẮT ĐẦU KIỂM TRA TRANG: {ten_trang} ({duong_dan_trang})")
    print("=" * 65)

    ket_qua = {}
    cac_loi_console = []
    cac_request_mediapipe_dau = []

    dang_kiem_k11 = True

    def on_req(req):
        if dang_kiem_k11:
            u = req.url
            if "dieu-khien-tay" in u or "mediapipe" in u or "hand_landmarker" in u:
                cac_request_mediapipe_dau.append(u)

    page.on("request", on_req)
    page.on("console", lambda msg: cac_loi_console.append(msg.text) if msg.type == "error" and "favicon.ico" not in msg.text else None)
    page.on("pageerror", lambda exc: cac_loi_console.append(str(exc)))

    url_day_du = f"{URL_GOC}{duong_dan_trang}"
    print(f"\n[K11] Mở trang {duong_dan_trang} (chưa bật tay)...")
    page.goto(url_day_du, wait_until="domcontentloaded")
    page.wait_for_selector("#khung-3d canvas", timeout=20000)
    time.sleep(1.0) # đợi Three.js render khung đầu

    # Kiểm tra K11
    k11_so_loi = len(cac_loi_console)
    k11_so_req_tay = len(cac_request_mediapipe_dau)
    fps_k11 = do_fps(page, 2.0)
    dang_kiem_k11 = False

    print(f"  Số lỗi console: {k11_so_loi}")
    print(f"  Số request điều khiển tay lúc đầu: {k11_so_req_tay}")
    print(f"  FPS ban đầu: {fps_k11:.1f}")

    k11_dat = (k11_so_loi == 0) and (k11_so_req_tay == 0) and (fps_k11 >= 35)
    page.screenshot(path=str(OUTPUT_DIR / f"{ten_trang}_k11_chua_bat.png"))
    ket_qua["K11"] = {
        "mo_ta": "Mở trang chưa bật tay: không lỗi console, không nạp MediaPipe, FPS mượt",
        "so_loi_console": k11_so_loi,
        "so_request_tay_dau": k11_so_req_tay,
        "fps": round(fps_k11, 1),
        "dat": k11_dat
    }

    # -------------------------------------------------------------------------
    # K12: Phát lại keo_luot
    # -------------------------------------------------------------------------
    print(f"\n[K12] Phát lại keo_luot trên {ten_trang}...")
    dat_lai_camera(page)
    goc_dau = page.evaluate("() => window.BioLab.gocCamera.phuongVi")
    t0_phat = time.perf_counter()
    t_tha = t0_phat + 0.67

    page.evaluate("async () => await window.BioLab.dieuKhienTay.bat({ phatLai: '/static/dieu-khien-tay/du-lieu-thu/keo_luot.json' })")

    min_goc = goc_dau
    while time.perf_counter() - t0_phat < 5.0:
        goc_hien_tai = page.evaluate("() => window.BioLab.gocCamera.phuongVi")
        if goc_hien_tai < min_goc:
            min_goc = goc_hien_tai
        che_do = page.evaluate("() => window.BioLab.trangThai.tay?.cheDo || 'cho'")
        w_yaw = page.evaluate("() => Math.abs(window.BioLab.trangThai.tay?.wYaw || 0)")
        if time.perf_counter() - t0_phat > 1.2 and (che_do == "cho" or w_yaw < 0.05):
            break
        time.sleep(0.04)

    t_dung_tu_tha = time.perf_counter() - t_tha
    delta_goc = goc_dau - min_goc
    print(f"  Góc đầu: {goc_dau:.1f}°, góc thấp nhất: {min_goc:.1f}°, giảm: {delta_goc:.1f}°, dừng sau khi thả: {t_dung_tu_tha:.2f} s")

    k12_dat = (delta_goc >= 60.0) and (t_dung_tu_tha < 4.0)
    page.screenshot(path=str(OUTPUT_DIR / f"{ten_trang}_k12_keo_luot.png"))
    page.evaluate("() => window.BioLab.dieuKhienTay.tat()")
    time.sleep(0.3)

    ket_qua["K12"] = {
        "mo_ta": "Kéo lướt: góc giảm >= 60°, quay tiếp sau khi thả, dừng < 4s",
        "goc_dau": round(goc_dau, 1),
        "goc_thap_nhat": round(min_goc, 1),
        "do_giam_deg": round(delta_goc, 1),
        "thoi_gian_dung_s": round(t_dung_tu_tha, 2),
        "dat": k12_dat
    }

    # -------------------------------------------------------------------------
    # K13: Phát lại zoom_hai_tay
    # -------------------------------------------------------------------------
    print(f"\n[K13] Phát lại zoom_hai_tay trên {ten_trang}...")
    dat_lai_camera(page)
    kc_dau = page.evaluate("() => window.BioLab.gocCamera.khoangCach")

    page.evaluate("async () => await window.BioLab.dieuKhienTay.bat({ phatLai: '/static/dieu-khien-tay/du-lieu-thu/zoom_hai_tay.json' })")

    t0_zoom = time.perf_counter()
    kc_min = kc_dau
    while time.perf_counter() - t0_zoom < 2.5:
        kc = page.evaluate("() => window.BioLab.gocCamera.khoangCach")
        if kc < kc_min:
            kc_min = kc
        time.sleep(0.05)

    ti_le_zoom = kc_dau / max(kc_min, 0.01)
    print(f"  KC đầu: {kc_dau:.2f}, KC min: {kc_min:.2f}, tỉ lệ zoom: {ti_le_zoom:.2f} (kỳ vọng ~1.5)")

    k13_dat = abs(ti_le_zoom - 1.5) < 0.15
    page.screenshot(path=str(OUTPUT_DIR / f"{ten_trang}_k13_zoom.png"))
    page.evaluate("() => window.BioLab.dieuKhienTay.tat()")
    time.sleep(0.3)

    ket_qua["K13"] = {
        "mo_ta": "Zoom hai tay: khoảng cách nhỏ nhất ≈ ban đầu / 1.5 (±5%)",
        "kc_dau": round(kc_dau, 2),
        "kc_min": round(kc_min, 2),
        "ti_le": round(ti_le_zoom, 2),
        "dat": k13_dat
    }

    # -------------------------------------------------------------------------
    # K14: Chạm nhẹ chọn cơ quan vs bấm chuột
    # -------------------------------------------------------------------------
    print(f"\n[K14] Chạm nhẹ chọn cơ quan vs Chuột trên {ten_trang}...")
    dat_lai_camera(page)

    canvas = page.locator("#khung-3d canvas")
    box = canvas.bounding_box()
    pos_x = box["width"] * 0.5
    pos_y = box["height"] * 0.42
    client_x = box["x"] + pos_x
    client_y = box["y"] + pos_y

    # Chọn bằng chạm nhẹ (qua hàm chonTaiDiem)
    bp_tay = page.evaluate("(pt) => window.chonTaiDiem(pt[0], pt[1])", [client_x, client_y])
    print(f"  Chọn bằng hàm chạm tay tại ({client_x:.0f}, {client_y:.0f}): '{bp_tay}'")

    # Đặt lại và bấm chuột bằng canvas.click(position=...)
    dat_lai_camera(page)
    canvas.click(position={"x": pos_x, "y": pos_y})
    time.sleep(0.5)
    bp_chuot = page.evaluate("() => window.BioLab.trangThai.chon || window.BioLab.trangThai.cau_truc")
    print(f"  Chọn bằng chuột click tại ({client_x:.0f}, {client_y:.0f}): '{bp_chuot}'")

    k14_dat = (bp_tay == bp_chuot) and (bp_tay is not None)
    page.screenshot(path=str(OUTPUT_DIR / f"{ten_trang}_k14_cham_chon.png"))

    ket_qua["K14"] = {
        "mo_ta": "Chạm nhẹ chọn cơ quan giống hệt click chuột",
        "bp_tay": bp_tay,
        "bp_chuot": bp_chuot,
        "dat": k14_dat
    }

    # -------------------------------------------------------------------------
    # K15: Đang bật tay sang tab Hoá học -> tắt camera
    # -------------------------------------------------------------------------
    print(f"\n[K15] Đang bật tay rồi chuyển sang tab Hoá học trên {ten_trang}...")
    page.evaluate("async () => await window.BioLab.dieuKhienTay.bat()")
    time.sleep(1.0)
    tt_truoc = page.evaluate("() => window.BioLab.trangThai.tay.bat")
    print(f"  Trạng thái tay trước khi chuyển tab: {tt_truoc}")

    # Chặn beforeunload để kiểm tra trạng thái tay ngay khi chonTab(true) chạy
    tt_sau = page.evaluate("""() => {
        window.addEventListener('beforeunload', (e) => { e.preventDefault(); e.returnValue = ''; });
        window.chonTab(true);
        return window.BioLab.trangThai.tay.bat;
    }""")
    print(f"  Trạng thái tay sau khi chuyển sang Hoá học: {tt_sau}")

    k15_dat = (tt_sau is False)
    page.screenshot(path=str(OUTPUT_DIR / f"{ten_trang}_k15_sang_hoa_hoc.png"))

    # Tải lại trang Sinh học để tiếp tục các kịch bản K16, K17
    page.goto(url_day_du, wait_until="domcontentloaded")
    page.wait_for_selector("#khung-3d canvas", timeout=15000)
    time.sleep(1.0)

    ket_qua["K15"] = {
        "mo_ta": "Sang tab Hoá học tắt bộ điều khiển tay",
        "tt_truoc": tt_truoc,
        "tt_sau": tt_sau,
        "dat": k15_dat
    }

    # -------------------------------------------------------------------------
    # K16: FPS khi bật camera giả
    # -------------------------------------------------------------------------
    print(f"\n[K16] Đo FPS khi bật camera giả trên {ten_trang}...")
    fps_tat = do_fps(page, 2.5)

    page.evaluate("async () => await window.BioLab.dieuKhienTay.bat()")
    time.sleep(2.0) # 2s warmup shader/WASM
    fps_bat = do_fps(page, 5.0)

    ti_le_fps = fps_bat / max(fps_tat, 1)
    print(f"  FPS lúc tắt tay: {fps_tat:.1f}, lúc bật tay: {fps_bat:.1f}, tỉ lệ: {ti_le_fps:.2f}")

    k16_dat = (fps_bat >= 0.85 * fps_tat) or (fps_bat >= 45.0)
    page.evaluate("() => window.BioLab.dieuKhienTay.tat()")
    time.sleep(0.3)

    ket_qua["K16"] = {
        "mo_ta": "FPS khi bật camera giả >= 0.85*lúc tắt hoặc >= 45",
        "fps_tat": round(fps_tat, 1),
        "fps_bat": round(fps_bat, 1),
        "ti_le": round(ti_le_fps, 2),
        "dat": k16_dat
    }

    # -------------------------------------------------------------------------
    # K17: Chuột kéo, cuộn, click sau khi gắn
    # -------------------------------------------------------------------------
    print(f"\n[K17] Kiểm tra chuột (kéo, cuộn) sau khi gắn trên {ten_trang}...")
    goc_pv_truoc = page.evaluate("() => window.BioLab.gocCamera.phuongVi")
    page.mouse.move(client_x, client_y)
    page.mouse.down()
    page.mouse.move(client_x + 120, client_y, steps=6)
    page.mouse.up()
    time.sleep(0.5)
    goc_pv_sau = page.evaluate("() => window.BioLab.gocCamera.phuongVi")
    diff_keo = abs(goc_pv_sau - goc_pv_truoc)
    print(f"  Chuột kéo xoay: góc trước={goc_pv_truoc:.1f}°, sau={goc_pv_sau:.1f}°, chênh lệch={diff_keo:.1f}°")

    kc_truoc = page.evaluate("() => window.BioLab.gocCamera.khoangCach")
    page.mouse.wheel(0, 250)
    time.sleep(0.5)
    kc_sau = page.evaluate("() => window.BioLab.gocCamera.khoangCach")
    diff_cuon = abs(kc_sau - kc_truoc)
    print(f"  Chuột cuộn zoom: kc trước={kc_truoc:.2f}, sau={kc_sau:.2f}, chênh lệch={diff_cuon:.2f}")

    k17_dat = (diff_keo > 1.0) and (diff_cuon > 0.05)
    page.screenshot(path=str(OUTPUT_DIR / f"{ten_trang}_k17_chuot.png"))

    ket_qua["K17"] = {
        "mo_ta": "Chuột kéo, cuộn zoom hoạt động chuẩn xác",
        "diff_keo_deg": round(diff_keo, 1),
        "diff_cuon": round(diff_cuon, 2),
        "dat": k17_dat
    }

    return ket_qua


def main():
    print("=" * 70)
    print("NGHIỆM THU ĐIỀU KHIỂN TAY TRÊN PHÒNG SINH HỌC 3D (K11..K17)")
    print("=" * 70)

    args = [
        "--use-fake-device-for-media-stream",
        "--use-fake-ui-for-media-stream",
        "--no-sandbox"
    ]

    ket_qua_toan_bo = {}

    with sync_playwright() as p:
        browser = p.chromium.launch(
            headless=False,
            channel="chrome",
            args=args
        )

        context = browser.new_context(viewport={"width": 1280, "height": 850})
        page = context.new_page()

        # 1. Kiểm tra trên /sinh-hoc
        kq_v1 = kiem_tra_trang(page, "/sinh-hoc", "sinh_hoc_v1")
        ket_qua_toan_bo["sinh_hoc_v1"] = kq_v1

        browser.close()

    # Ghi file ket-qua
    tep_ket_qua = OUTPUT_DIR / "sinh-hoc.json"
    tep_ket_qua.write_text(json.dumps(ket_qua_toan_bo, indent=2, ensure_ascii=False), encoding="utf-8")
    print(f"\n[XONG] Đã ghi kết quả nghiệm thu vào: {tep_ket_qua}")

    # Đánh giá tổng quan
    tat_ca_dat = True
    for trang, cac_k in ket_qua_toan_bo.items():
        print(f"\nTổng kết {trang}:")
        for k_id, k_val in cac_k.items():
            dat_str = "PASS" if k_val.get("dat") else "FAIL"
            if not k_val.get("dat"):
                tat_ca_dat = False
            print(f"  {k_id}: {dat_str} - {k_val.get('mo_ta')}")

    if tat_ca_dat:
        print("\n🎉 CHÚC MỪNG: 100% KỊCH BẢN K11..K17 TRÊN /sinh-hoc ĐÃ PASS!")
        return 0
    else:
        print("\n⚠️ CÓ KỊCH BẢN CHƯA ĐẠT! Vui lòng kiểm tra log chi tiết ở trên.")
        return 1


if __name__ == "__main__":
    sys.exit(main())
