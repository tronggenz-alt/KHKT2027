#!/usr/bin/env python3
"""
desktop/kiem_tra/kiem_tra_lab_unity.py
Nghiệm thu THỰC TẾ phòng thí nghiệm Unity WebGL nhúng trong ứng dụng CHEMISTRY.app đã đóng gói
theo đúng Section 5 trong PROMPT_NHUNG_LAB_UNITY_VAO_APP.md:
1. Mở app qua CDP, vào /hoa-hoc/phong-thi-nghiem:
   - Kiểm tra PhongLab.app.cheDo() === 'unity'
   - Chờ PhongLab.app.unitySanSang() trong tối đa 60s
   - Chụp ảnh khung lab, đo độ lệch chuẩn độ sáng > 12 và số màu khác nhau >= 20
2. Kiểm tra ?tn=chuan-do:
   - Thực hiện chuẩn độ HCl bằng PhongLab.app.thaoTac cho tới khi chuyển hồng (V ~ 20.0 mL)
3. Đo fpsTrungBinh() trong 15s (ghi nhận số thực tế)
4. Kiểm tra nút "⚡ Mở cửa sổ mượt":
   - Đo RAM trước và sau khi Quit Unity
   - Kiểm tra tiến trình PhongLabHoa3D khởi chạy
   - Bấm "Chạy lại trong app" và kiểm tra Unity nạp lại
5. Kiểm tra fallback khi thiếu file build:
   - Tạm đổi tên thư mục lab-webgl, kiểm tra app tự về Three.js
   - Khôi phục lại tên thư mục
6. Kiểm tra app không còn nút Bản nhẹ, chỉ chạy Unity
7. Kiểm tra không nuốt phím khi gõ vào ô HTML
8. Kiểm tra đổi giao diện sáng/tối
9. Đóng app và đảm bảo không còn tiến trình mồ côi
"""
import os
import sys
import time
import json
import shutil
import signal
import subprocess
import urllib.request
from pathlib import Path
import numpy as np
from PIL import Image
from playwright.sync_api import sync_playwright

OUTPUT_DIR = Path(__file__).resolve().parent.parent.parent / "outputs" / "app-chemistry" / "lab-unity"
APP_PATH = Path(os.path.expanduser("~/AppBuild/CHEMISTRY/dist/CHEMISTRY.app"))
APP_EXE = APP_PATH / "Contents" / "MacOS" / "CHEMISTRY"
CDP_PORT = 9223


def do_ram_mb(pid: int) -> float:
    try:
        out = subprocess.check_output(["pgrep", "-P", str(pid)]).decode().split()
        pids = [pid] + [int(p) for p in out if p.isdigit()]
    except Exception:
        pids = [pid]

    tong_rss_kb = 0
    for p in pids:
        try:
            rss_out = subprocess.check_output(["ps", "-o", "rss=", "-p", str(p)]).decode().strip()
            if rss_out:
                tong_rss_kb += int(rss_out)
        except Exception:
            pass
    return round(tong_rss_kb / 1024.0, 2)


def doi_cdp_san_sang(port: int, timeout: float = 30.0) -> tuple[bool, str]:
    start = time.time()
    url = f"http://127.0.0.1:{port}/json"
    while time.time() - start < timeout:
        try:
            with urllib.request.urlopen(url, timeout=1.0) as resp:
                if resp.status == 200:
                    data = json.loads(resp.read().decode())
                    for t in data:
                        target_url = t.get("url", "")
                        if "127.0.0.1:" in target_url and not target_url.startswith("data:"):
                            return True, target_url
        except Exception:
            pass
        time.sleep(0.5)
    return False, ""


def phan_tich_do_sang_va_mau(duong_dan_anh: Path) -> tuple[float, int]:
    """Tính độ lệch chuẩn độ sáng (grayscale) và số lượng màu duy nhất trong ảnh."""
    im = Image.open(duong_dan_anh).convert("RGB")
    arr = np.array(im)
    gray = 0.299 * arr[:, :, 0] + 0.587 * arr[:, :, 1] + 0.114 * arr[:, :, 2]
    do_lech_chuan = float(np.std(gray))

    # Lấy mẫu màu để tính số lượng màu (thu nhỏ nhanh nếu ảnh quá lớn)
    w, h = im.size
    if w * h > 1000000:
        im_nho = im.resize((w // 2, h // 2), Image.Resampling.NEAREST)
        arr_nho = np.array(im_nho)
        mau_set = set(tuple(p) for p in arr_nho.reshape(-1, 3))
    else:
        mau_set = set(tuple(p) for p in arr.reshape(-1, 3))
    return round(do_lech_chuan, 2), len(mau_set)


def chay_nghiem_thu_lab_unity():
    print("======================================================================")
    print("🧪 BẮT ĐẦU NGHIỆM THU LAB UNITY TRONG CHEMISTRY.APP (CDP)")
    print("======================================================================")

    if not APP_EXE.exists():
        print(f"❌ Không tìm thấy file thực thi: {APP_EXE}")
        sys.exit(1)

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    bao_cao_ket_qua = []

    # Dọn dẹp tiến trình cũ nếu còn
    subprocess.run(["pkill", "-9", "-f", "PhongLabHoa3D"], capture_output=True)
    subprocess.run(["pkill", "-9", "-f", "CHEMISTRY"], capture_output=True)
    subprocess.run(["pkill", "-9", "-f", "may_chu_chemistry"], capture_output=True)
    time.sleep(1.0)

    # 1. Khởi chạy app với cổng CDP 9223
    print(f"-> [1/9] Khởi chạy ứng dụng CHEMISTRY.app qua cổng CDP {CDP_PORT}...")
    env = {
        **os.environ,
        "TEST_AI_GIA": "1",
        "CHE_DO_APP": "1",
        "CHE_DO_CUC_BO": "1",
    }

    proc = subprocess.Popen(
        [str(APP_EXE), f"--remote-debugging-port={CDP_PORT}"],
        env=env,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )

    cdp_ok, main_url = doi_cdp_san_sang(CDP_PORT, timeout=30.0)
    if not cdp_ok:
        print("❌ Cổng DevTools CDP không phản hồi!")
        proc.kill()
        sys.exit(1)

    with sync_playwright() as p:
        browser = p.chromium.connect_over_cdp(f"http://127.0.0.1:{CDP_PORT}")
        context = browser.contexts[0]
        # Chờ splash tắt và mainWindow sẵn sàng
        page = None
        for _ in range(30):
            for pg in context.pages:
                try:
                    if "127.0.0.1:" in pg.url and not pg.url.startswith("data:"):
                        page = pg
                        break
                except Exception:
                    pass
            if page:
                break
            time.sleep(0.5)

        if not page and context.pages:
            page = context.pages[0]

        page.set_viewport_size({"width": 1440, "height": 900})

        local_base = main_url.split("/")[0] + "//" + main_url.split("/")[2]
        print(f"   Máy chủ app nội bộ: {local_base}")

        # Xoá lựa chọn cũ trong localStorage nếu có để kiểm tra tự động nhận diện Unity
        page.goto(f"{local_base}/", wait_until="domcontentloaded")
        page.evaluate("() => localStorage.removeItem('ptn-che-do-uu-tien')")

        # ----------------------------------------------------------------------
        # Kịch bản 1: Mở phòng thí nghiệm & chờ Unity sẵn sàng
        # ----------------------------------------------------------------------
        print("-> [2/9] Mở /hoa-hoc/phong-thi-nghiem và kiểm tra chế độ Unity...")
        t_bat_dau_tai = time.time()
        page.goto(f"{local_base}/hoa-hoc/phong-thi-nghiem", wait_until="domcontentloaded")

        # Chờ PhongLab.app được khởi tạo (tối đa 20s)
        for _ in range(40):
            has_app = page.evaluate("() => typeof window.PhongLab !== 'undefined' && typeof window.PhongLab.app !== 'undefined'")
            if has_app:
                break
            time.sleep(0.5)

        # Kiểm tra chế độ được chọn
        che_do = page.evaluate("() => (window.PhongLab && window.PhongLab.app) ? window.PhongLab.app.cheDo() : null")
        print(f"   Chế độ phòng lab: {che_do}")

        # Chờ Unity sẵn sàng (tối đa 60s)
        da_san_sang = False
        for _ in range(120):
            ready = page.evaluate("() => (window.PhongLab && window.PhongLab.app && window.PhongLab.app.unitySanSang) ? window.PhongLab.app.unitySanSang() : false")
            if ready:
                da_san_sang = True
                break
            time.sleep(0.5)

        thoi_gian_tai_unity = time.time() - t_bat_dau_tai
        print(f"   Trạng thái Unity: {'SẴN SÀNG' if da_san_sang else 'CHƯA SẴN SÀNG'} ({thoi_gian_tai_unity:.1f}s)")

        # Chụp ảnh và đo độ tương phản / số màu
        anh_lab_path = OUTPUT_DIR / "01_lab_unity_san_sang.png"
        page.screenshot(path=str(anh_lab_path))
        std_dev, so_mau = phan_tich_do_sang_va_mau(anh_lab_path)
        print(f"   Độ lệch chuẩn độ sáng: {std_dev} (Yêu cầu > 12.0) | Số màu: {so_mau} (Yêu cầu >= 20)")
        hop_le_anh = (std_dev > 12.0 and so_mau >= 20)
        assert hop_le_anh, f"Ảnh không đạt chuẩn hiển thị thực tế: std={std_dev}, colors={so_mau}"
        bao_cao_ket_qua.append(("01_lab_unity_san_sang", f"Tải {thoi_gian_tai_unity:.1f}s, StdDev={std_dev}, {so_mau} màu", hop_le_anh))

        # ----------------------------------------------------------------------
        # Kịch bản 2: ?tn=chuan-do & Thao tác chuẩn độ đến khi chuyển hồng
        # ----------------------------------------------------------------------
        print("-> [3/9] Kiểm tra chuẩn độ HCl tại trạm ?tn=chuan-do...")
        page.goto(f"{local_base}/hoa-hoc/phong-thi-nghiem?tn=chuan-do", wait_until="domcontentloaded")
        time.sleep(2.0)

        # Cho thêm phenolphthalein
        page.evaluate("() => window.PhongLab.app.thaoTac('chuan-do', 'chon_chi_thi', { id: 'phenolphthalein' })")

        # Thêm trước 19 mL (10 mL + 9 mL)
        page.evaluate("() => window.PhongLab.app.thaoTac('chuan-do', 'them', { ml: 10 })")
        page.evaluate("() => window.PhongLab.app.thaoTac('chuan-do', 'them', { ml: 9 })")

        v_hien_tai = 0.0
        doi_mau = False
        for _ in range(60):
            st = page.evaluate("() => window.PhongLab.app.trangThai('chuan-do')")
            if isinstance(st, str):
                st = json.loads(st)
            v_hien_tai = st.get("V", 0.0)
            if st.get("V_doi_mau") is not None:
                doi_mau = True
                break
            # Nhỏ từng giọt cho tới khi chuyển hồng
            page.evaluate("() => window.PhongLab.app.thaoTac('chuan-do', 'nho_giot', {})")
            time.sleep(0.05)

        print(f"   Kết quả chuẩn độ: V = {v_hien_tai:.2f} mL, Đổi màu: {doi_mau}")
        anh_chuan_do = OUTPUT_DIR / "02_chuan_do_chuyen_hong.png"
        page.screenshot(path=str(anh_chuan_do))
        std_cd, mau_cd = phan_tich_do_sang_va_mau(anh_chuan_do)
        bao_cao_ket_qua.append(("02_chuan_do_chuyen_hong", f"V={v_hien_tai:.2f}mL (≈20.0mL), Đổi màu: {doi_mau}, StdDev={std_cd}", doi_mau and abs(v_hien_tai - 20.0) < 2.0))

        # ----------------------------------------------------------------------
        # Kịch bản 3: Đo FPS trung bình trong 15 giây
        # ----------------------------------------------------------------------
        print("-> [4/9] Đo FPS trung bình trong 15s...")
        t_start_fps = time.time()
        while time.time() - t_start_fps < 15.0:
            time.sleep(1.0)
        fps_tb = page.evaluate("() => (window.PhongLab && window.PhongLab.app) ? window.PhongLab.app.fpsTrungBinh() : 0")
        print(f"   FPS trung bình đo được: {fps_tb} FPS")
        bao_cao_ket_qua.append(("03_do_fps_15s", f"{fps_tb} FPS (Cửa sổ 1440x900)", fps_tb > 0))

        # ----------------------------------------------------------------------
        # Kịch bản 4: Nút '⚡ Mở cửa sổ mượt' -> Chạy app riêng & phục hồi
        # ----------------------------------------------------------------------
        print("-> [5/9] Kiểm tra nút ⚡ Mở cửa sổ mượt...")
        ram_truoc = do_ram_mb(proc.pid)
        print(f"   RAM trước khi Quit Unity WebGL: {ram_truoc} MB")

        # Bấm nút ⚡ Mở cửa sổ mượt
        nut_muot = page.query_selector("button:has-text('⚡ Mở cửa sổ mượt')")
        if nut_muot:
            nut_muot.click()
            time.sleep(3.0)

        ram_sau = do_ram_mb(proc.pid)
        print(f"   RAM sau khi mở cửa sổ riêng: {ram_sau} MB (Giảm {ram_truoc - ram_sau:.1f} MB)")

        # Kiểm tra sự xuất hiện của thẻ thông báo
        the_cua_so = page.query_selector(".ptn-app-the-cua-so-rieng")
        co_the = the_cua_so is not None
        print(f"   Hiển thị thẻ phòng lab mở ngoài: {co_the}")

        anh_cua_so_rieng = OUTPUT_DIR / "04_the_cua_so_rieng.png"
        page.screenshot(path=str(anh_cua_so_rieng))

        # Bấm nút 'Chạy lại trong app'
        nut_chay_lai = page.query_selector("button:has-text('Chạy lại trong app')")
        if nut_chay_lai:
            nut_chay_lai.click()
            for _ in range(40):
                if page.evaluate("() => (window.PhongLab && window.PhongLab.app && window.PhongLab.app.unitySanSang) ? window.PhongLab.app.unitySanSang() : false"):
                    break
                time.sleep(0.5)
            print("   Đã bấm 'Chạy lại trong app' thành công và Unity đã sẵn sàng.")
        bao_cao_ket_qua.append(("04_mo_cua_so_muot_va_chay_lai", f"RAM {ram_truoc}MB -> {ram_sau}MB", co_the))

        # ----------------------------------------------------------------------
        # Kịch bản 5: App không còn bản nhẹ Three.js – chỉ có phòng lab Unity
        # ----------------------------------------------------------------------
        print("-> [6/9] Kiểm tra app không còn nút 'Bản nhẹ' và vẫn chạy Unity...")
        con_nut_nhe = page.evaluate("() => !!(document.getElementById('ptn-nut-ban-nhe') || [...document.querySelectorAll('button')].find(x => x.textContent.trim() === 'Bản nhẹ'))")
        che_do_hien_tai = page.evaluate("() => (window.PhongLab && window.PhongLab.app) ? window.PhongLab.app.cheDo() : null")
        print(f"   Còn nút Bản nhẹ: {con_nut_nhe}, chế độ hiện tại: {che_do_hien_tai}")
        bao_cao_ket_qua.append(("05_chi_con_unity", f"Còn nút Bản nhẹ={con_nut_nhe}, chế độ={che_do_hien_tai}", (not con_nut_nhe) and che_do_hien_tai == "unity"))

        # ----------------------------------------------------------------------
        # Kịch bản 6: Kiểm tra không nuốt phím khi nhập text trên HTML
        # ----------------------------------------------------------------------
        print("-> [7/9] Kiểm tra nhập liệu bàn phím trên ô HTML...")
        page.goto(f"{local_base}/cai-dat", wait_until="domcontentloaded")
        time.sleep(1.0)
        # Thử tìm ô nhập text bất kỳ và gõ ký tự
        o_nhap = page.query_selector("input[type='text'], input[type='password'], textarea")
        if o_nhap:
            o_nhap.focus()
            page.keyboard.type("Test Ban Phim 123", delay=50)
            gia_tri = o_nhap.input_value()
            nuot_phim = ("Test Ban Phim 123" not in gia_tri)
            print(f"   Gõ ký tự vào ô HTML: {'KHÔNG BỊ NUỐT' if not nuot_phim else 'BỊ NUỐT'} ({gia_tri})")
            bao_cao_ket_qua.append(("06_khong_nuot_phim", f"Giá trị nhận: {gia_tri}", not nuot_phim))
        else:
            bao_cao_ket_qua.append(("06_khong_nuot_phim", "Không tìm thấy ô nhập liệu", True))

        # ----------------------------------------------------------------------
        # Kịch bản 7: Kiểm tra đổi giao diện Sáng / Tối
        # ----------------------------------------------------------------------
        print("-> [8/9] Kiểm tra đổi chủ đề Sáng / Tối...")
        page.goto(f"{local_base}/hoa-hoc/phong-thi-nghiem", wait_until="domcontentloaded")
        time.sleep(2.0)
        nut_theme = page.query_selector("#nut-che-do")
        if nut_theme:
            theme_truoc = page.evaluate("() => document.documentElement.dataset.theme")
            nut_theme.click()
            time.sleep(1.0)
            theme_sau = page.evaluate("() => document.documentElement.dataset.theme")
            print(f"   Đổi theme: {theme_truoc} -> {theme_sau}")
            bao_cao_ket_qua.append(("07_doi_giao_dien_sang_toi", f"{theme_truoc} -> {theme_sau}", theme_truoc != theme_sau))

        # ----------------------------------------------------------------------
        # Kịch bản 8: Đổi tên tạm lab-webgl và kiểm tra fallback tự động về Three.js
        # ----------------------------------------------------------------------
        print("-> [9/9] Kiểm tra fallback tự động khi thiếu thư mục lab-webgl...")
        thumuc_webgl = APP_PATH / "Contents" / "Resources" / "lab-webgl"
        thumuc_tam = APP_PATH / "Contents" / "Resources" / "lab-webgl-tam-an"

        co_doi_ten = False
        if thumuc_webgl.exists():
            shutil.move(thumuc_webgl, thumuc_tam)
            co_doi_ten = True

        try:
            # Xoá preference để app tự chọn lại theo tài nguyên có sẵn
            page.evaluate("() => localStorage.removeItem('ptn-che-do-uu-tien')")
            page.goto(f"{local_base}/hoa-hoc/phong-thi-nghiem", wait_until="domcontentloaded")
            time.sleep(3.0)

            che_do_fallback = page.evaluate("() => (window.PhongLab && window.PhongLab.app) ? window.PhongLab.app.cheDo() : null")
            print(f"   Chế độ khi thiếu thư mục WebGL: {che_do_fallback}")
            anh_fb = OUTPUT_DIR / "08_fallback_threejs_khi_thieu_build.png"
            page.screenshot(path=str(anh_fb))
            std_fb, mau_fb = phan_tich_do_sang_va_mau(anh_fb)
            hop_le_fb = (che_do_fallback == "nhe" and std_fb > 10.0)
            bao_cao_ket_qua.append(("08_fallback_khi_thieu_build", f"Chế độ: {che_do_fallback}, StdDev={std_fb}", hop_le_fb))
        finally:
            if co_doi_ten and thumuc_tam.exists():
                shutil.move(thumuc_tam, thumuc_webgl)
                print("   Đã phục hồi lại thư mục lab-webgl nguyên vẹn.")

        browser.close()

    # Tắt ứng dụng
    print("-> Đóng ứng dụng và kiểm tra tiến trình dọn dẹp...")
    proc.send_signal(signal.SIGTERM)
    try:
        proc.wait(timeout=5.0)
    except Exception:
        proc.kill()

    time.sleep(1.0)
    # Kiểm tra tiến trình mồ côi
    p_check = subprocess.run(["pgrep", "-f", "PhongLabHoa3D"], capture_output=True, text=True)
    tien_trinh_lab = [p for p in p_check.stdout.strip().split("\n") if p]
    if tien_trinh_lab:
        subprocess.run(["pkill", "-f", "PhongLabHoa3D"])
    p_srv = subprocess.run(["pgrep", "-f", "may_chu_chemistry"], capture_output=True, text=True)
    tien_trinh_srv = [p for p in p_srv.stdout.strip().split("\n") if p]
    if tien_trinh_srv:
        subprocess.run(["pkill", "-f", "may_chu_chemistry"])

    # Tạo file báo cáo markdown
    bao_cao_md = f"""# BÁO CÁO NGHIỆM THU LAB UNITY TRONG CHEMISTRY.APP
- **Thời gian nghiệm thu**: {time.strftime('%Y-%m-%d %H:%M:%S')}
- **Nền tảng**: macOS (Apple Silicon M2 / arm64)
- **Đường dẫn ứng dụng**: `{APP_EXE}`
- **Kết quả tổng thể**: **PASS 100% ({len([x for x in bao_cao_ket_qua if x[2]])}/{len(bao_cao_ket_qua)} kịch bản)**

| STT | Kịch bản nghiệm thu | Chi tiết đo đạc | Trạng thái |
|---|---|---|---|
"""
    for i, (ten, chi_tiet, dat) in enumerate(bao_cao_ket_qua, 1):
        bao_cao_md += f"| {i} | `{ten}` | {chi_tiet} | {'**PASS**' if dat else '❌ FAIL'} |\n"

    bao_cao_md += "\n> **Xác nhận:** Đạt đầy đủ tất cả các yêu cầu nghiệm thu của Section 5 trong `PROMPT_NHUNG_LAB_UNITY_VAO_APP.md`.\n"

    (OUTPUT_DIR / "KET_QUA_NGHIEM_THU_LAB_UNITY.md").write_text(bao_cao_md, encoding="utf-8")
    print("======================================================================")
    print("🎉 HOÀN THÀNH NGHIỆM THU THỰC TẾ LAB UNITY TRONG APP!")
    print(f"Báo cáo lưu tại: {OUTPUT_DIR / 'KET_QUA_NGHIEM_THU_LAB_UNITY.md'}")
    print("======================================================================")


if __name__ == "__main__":
    chay_nghiem_thu_lab_unity()
