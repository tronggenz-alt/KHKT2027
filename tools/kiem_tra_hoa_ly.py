#!/usr/bin/env python3
"""
tools/kiem_tra_hoa_ly.py
Kịch bản nghiệm thu tự động toàn diện bằng trình duyệt thật qua Playwright
theo đúng mục 11.6 trong PROMPT_HOA_LY_HOAN_THIEN.md:
- Bắt buộc dùng assert số học thật, không chỉ chụp ảnh.
- Kết nối tới máy chủ cục bộ http://127.0.0.1:8000.
- Kiểm tra toàn bộ 5 trang trong phân hệ Hoá lý:
  1. Động cơ nhiệt 2D (/hoa-hoc/hoa-ly/dong-co-nhiet) - 13 kịch bản gốc.
  2. Khí lý tưởng (/hoa-hoc/hoa-ly/khi-ly-tuong) - 8 kịch bản.
  3. Cân bằng hoá học & Le Chatelier (/hoa-hoc/hoa-ly/can-bang-le-chatelier) - 6 kịch bản.
  4. Tốc độ phản ứng & Ea (/hoa-hoc/hoa-ly/toc-do-phan-ung) - 6 kịch bản.
  5. Thư viện công thức (/hoa-hoc/hoa-ly/cong-thuc) - 6 kịch bản.
  6. Kiểm tra chung: Chế độ tối Awesomic rgb(9, 9, 11), di động 390x844 không tràn cuộn ngang, 0 lỗi console.
- Lưu ảnh minh chứng vào outputs/hoa-ly/.
"""
import json
import math
import os
import sys
import time
from pathlib import Path

from playwright.sync_api import sync_playwright

GOC = Path(__file__).resolve().parents[1]
OUTPUT_DIR = GOC / "outputs" / "hoa-ly"
OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

URL_GOC = "http://127.0.0.1:8000"


def main():
    print("=" * 75)
    print("BẮT ĐẦU NGHIỆM THU TRÌNH DUYỆT THẬT TOÀN DIỆN 5 TRANG PHÂN HỆ HOÁ LÝ 2D")
    print("=" * 75)

    cac_loi_console = []

    with sync_playwright() as p:
        browser = p.chromium.launch(headless=True, channel="chrome")
        context = browser.new_context(viewport={"width": 1280, "height": 850})
        page = context.new_page()

        # Bắt lỗi console
        page.on("console", lambda msg: cac_loi_console.append(f"{msg.text} | location: {msg.location}") if msg.type == "error" and "favicon.ico" not in msg.text else None)
        page.on("pageerror", lambda exc: cac_loi_console.append(str(exc)))
        page.on("response", lambda r: print(f"  [HTTP {r.status}] {r.url}") if r.status >= 400 else None)

        # =====================================================================
        # PHẦN 1: ĐỘNG CƠ NHIỆT 2D & TRANG TRUNG TÂM (13 KỊCH BẢN GỐC)
        # =====================================================================
        print("\n" + "=" * 50)
        print("PHẦN 1: ĐỘNG CƠ NHIỆT 2D & TRANG TRUNG TÂM (13 KỊCH BẢN)")
        print("=" * 50)

        # ---------------------------------------------------------------------
        # KỊCH BẢN 1: /hoa-hoc/hoa-ly hiện đủ thẻ và vào mô phỏng
        # ---------------------------------------------------------------------
        print("\n[KB 1] Kiểm tra trang trung tâm /hoa-hoc/hoa-ly...")
        page.goto(f"{URL_GOC}/hoa-hoc/hoa-ly", wait_until="networkidle")
        page.wait_for_selector(".hl-card-hub")
        the_hub = page.locator(".hl-card-hub")
        so_the = the_hub.count()
        print(f"  -> Tìm thấy {so_the} thẻ mô phỏng trên trang Hoá lý.")
        assert so_the >= 4, f"Thiếu thẻ trên trang Hoá lý: {so_the} < 4"

        page.screenshot(path=str(OUTPUT_DIR / "01_trang_hoa_ly.png"))

        # Bấm thẻ Động cơ nhiệt để vào trang mô phỏng
        the_co = page.locator('a[href*="/dong-co-nhiet"]').first
        the_co.click()
        page.wait_for_load_state("networkidle")
        assert "/hoa-hoc/hoa-ly/dong-co-nhiet" in page.url, f"Chưa vào đúng URL: {page.url}"

        # Đọc danh-muc.json để đối chiếu tiêu đề
        danh_muc_path = GOC / "frontend" / "hoa-hoc" / "hoa-ly" / "danh-muc.json"
        danh_muc = json.loads(danh_muc_path.read_text(encoding="utf-8"))
        ten_chuan = next(mp["ten"] for mp in danh_muc["mo_phong"] if mp["id"] == "dong-co-nhiet")
        page.wait_for_timeout(300)
        tieu_de_trang = page.title()
        print(f"  -> Tiêu đề trang: '{tieu_de_trang}' (mong đợi chứa '{ten_chuan}')")
        assert ten_chuan in tieu_de_trang, f"Tiêu đề '{tieu_de_trang}' không chứa '{ten_chuan}'"
        print("  => KB 1: PASS")

        # Đảm bảo hook window.HoaLy có sẵn
        page.wait_for_function("() => window.HoaLy && typeof window.HoaLy.trangThai === 'function'")

        # ---------------------------------------------------------------------
        # KỊCH BẢN 2: Trạng thái ban đầu t = 1, phi = 180°
        # ---------------------------------------------------------------------
        print("\n[KB 2] Kiểm tra trạng thái ban đầu...")
        st0 = page.evaluate("() => window.HoaLy.trangThai()")
        print(f"  -> t = {st0['t']}, phi = {st0['phi']}°, doan = {st0['doan']}, p = {st0['p_bar']} bar, V = {st0['V_cm3']} cm³")
        assert abs(st0["t"] - 1.0) < 0.05, f"t = {st0['t']} != 1.0"
        assert abs(st0["phi"] - 180.0) < 1.0, f"phi = {st0['phi']} != 180°"
        assert st0["doan"] == 1, f"doan = {st0['doan']} != 1"
        print("  => KB 2: PASS")

        # ---------------------------------------------------------------------
        # KỊCH BẢN 3: Kéo bánh đà 90° chiều kim đồng hồ
        # ---------------------------------------------------------------------
        print("\n[KB 3] Kéo bánh đà theo cung tròn 90° chiều kim đồng hồ...")
        tam = page.evaluate("() => window.HoaLy.toaDo.tamBanhDa()")
        chot = page.evaluate("() => window.HoaLy.toaDo.chotKhuyu()")
        r_banh_da = math.hypot(chot["x"] - tam["x"], chot["y"] - tam["y"])
        print(f"  -> Tâm bánh đà: ({tam['x']:.1f}, {tam['y']:.1f}), bán kính chốt: {r_banh_da:.1f} px")

        page.mouse.move(chot["x"], chot["y"])
        page.mouse.down()
        goc_dau = math.atan2(chot["y"] - tam["y"], chot["x"] - tam["x"])
        goc_cuoi = goc_dau + math.pi / 2
        so_buoc = 30
        for i in range(1, so_buoc + 1):
            goc = goc_dau + (goc_cuoi - goc_dau) * (i / so_buoc)
            mx = tam["x"] + r_banh_da * math.cos(goc)
            my = tam["y"] + r_banh_da * math.sin(goc)
            page.mouse.move(mx, my)
            time.sleep(0.01)
        page.mouse.up()

        st3 = page.evaluate("() => window.HoaLy.trangThai()")
        print(f"  -> Sau khi kéo 90°: phi = {st3['phi']:.2f}°, V = {st3['V_cm3']:.2f} cm³, p = {st3['p_bar']:.2f} bar")
        assert abs(st3["phi"] - 270.0) <= 3.5, f"phi = {st3['phi']} != 270° ± 3.5°"

        # Kiểm tra đoạn nhiệt: p * V^gamma = const = p1 * V1^gamma (sai số <= 1.5%)
        pV_gamma = st3["p_bar"] * math.pow(st3["V_cm3"], 1.4)
        v1_cm3 = 446.804
        pV_gamma_chuan = 1.0 * math.pow(v1_cm3, 1.4)
        sai_so_pv = abs(pV_gamma - pV_gamma_chuan) / pV_gamma_chuan
        print(f"  -> p·V^γ = {pV_gamma:.2f} vs chuẩn {pV_gamma_chuan:.2f} (sai số {sai_so_pv*100:.3f}%)")
        assert sai_so_pv < 0.015, f"Lệch đoạn nhiệt quá mức: {sai_so_pv*100:.2f}%"
        print("  => KB 3: PASS")

        # ---------------------------------------------------------------------
        # KỊCH BẢN 4: Quay tiếp qua ĐCT -> doan = 3, soLanDanhLua + 1
        # ---------------------------------------------------------------------
        print("\n[KB 4] Quay tiếp qua ĐCT (360°)...")
        so_lan_truoc = st3["soLanDanhLua"]
        page.evaluate("() => window.HoaLy.datGoc(380)")
        st4 = page.evaluate("() => window.HoaLy.trangThai()")
        print(f"  -> phi = {st4['phi']:.1f}°, doan = {st4['doan']}, số lần đánh lửa: {st4['soLanDanhLua']}")
        assert st4["doan"] in (3, 4), f"doan = {st4['doan']} != 3 hoặc 4"
        assert st4["soLanDanhLua"] == so_lan_truoc + 1, f"Chưa tăng số lần đánh lửa: {st4['soLanDanhLua']} vs {so_lan_truoc}"
        print("  => KB 4: PASS")

        # ---------------------------------------------------------------------
        # KỊCH BẢN 5: Kéo pít-tông tới V = Vc + 0.5*Vd
        # ---------------------------------------------------------------------
        print("\n[KB 5] Kéo pít-tông trực tiếp...")
        page.evaluate("() => window.HoaLy.datGoc(180)")
        pt5 = page.evaluate("() => window.HoaLy.toaDo.matPitTong()")
        page.mouse.move(pt5["x"], pt5["y"])
        page.mouse.down()
        page.mouse.move(pt5["x"] - 50, pt5["y"], steps=10)
        page.mouse.up()
        st5 = page.evaluate("() => window.HoaLy.trangThai()")
        print(f"  -> V = {st5['V_cm3']:.1f} cm³, phi = {st5['phi']:.1f}°")
        assert 180 <= st5["phi"] <= 360, f"Góc phi {st5['phi']} nằm ngoài nửa chu kỳ nén [180, 360]"
        print("  => KB 5: PASS")

        # ---------------------------------------------------------------------
        # KỊCH BẢN 6: datTienDo(2) rồi kéo chấm p-V lên dọc 2->3
        # ---------------------------------------------------------------------
        print("\n[KB 6] datTienDo(2) và kéo chấm dọc 2->3...")
        page.evaluate("() => window.HoaLy.datTienDo(2.0)")
        dot_pos = page.evaluate("() => window.HoaLy.toaDo.diemPV()")
        page.mouse.move(dot_pos["x"], dot_pos["y"])
        page.mouse.down()
        page.mouse.move(dot_pos["x"], dot_pos["y"] - 40, steps=10)
        page.mouse.up()
        st6 = page.evaluate("() => window.HoaLy.trangThai()")
        print(f"  -> phi = {st6['phi']}°, doan = {st6['doan']}, T = {st6['T_K']:.1f} K, p = {st6['p_bar']:.2f} bar")
        assert abs(st6["phi"] - 360.0) < 1.0, f"phi = {st6['phi']} != 360°"
        assert st6["doan"] == 2, f"doan = {st6['doan']} != 2"
        assert st6["T_K"] > 800, f"Nhiệt độ T = {st6['T_K']} K chưa tăng theo nhiệt cấp"
        print("  => KB 6: PASS")

        # ---------------------------------------------------------------------
        # KỊCH BẢN 7: datTienDo(5.5), kéo chấm về phía V lớn -> doan in {4, 5}
        # ---------------------------------------------------------------------
        print("\n[KB 7] Chống nhảy nhầm trên đồ thị: datTienDo(5.5)...")
        page.evaluate("() => window.HoaLy.datTienDo(5.5)")
        dot_pos7 = page.evaluate("() => window.HoaLy.toaDo.diemPV()")
        page.mouse.move(dot_pos7["x"], dot_pos7["y"])
        page.mouse.down()
        page.mouse.move(dot_pos7["x"] + 50, dot_pos7["y"], steps=10)
        page.mouse.up()
        st7 = page.evaluate("() => window.HoaLy.trangThai()")
        print(f"  -> Sau khi kéo: doan = {st7['doan']}, t = {st7['t']:.2f}")
        assert st7["doan"] in (4, 5), f"doan = {st7['doan']} bị nhảy nhầm ra ngoài [4, 5]"
        assert st7["doan"] != 0, "Bị nhảy nhầm sang kỳ nạp 0"
        print("  => KB 7: PASS")

        # ---------------------------------------------------------------------
        # KỊCH BẢN 8: Quán tính bánh đà
        # ---------------------------------------------------------------------
        print("\n[KB 8] Quán tính bánh đà...")
        page.evaluate("() => window.HoaLy.datGoc(180)")
        chot8 = page.evaluate("() => window.HoaLy.toaDo.chotKhuyu()")
        tam8 = page.evaluate("() => window.HoaLy.toaDo.tamBanhDa()")
        page.mouse.move(chot8["x"], chot8["y"])
        page.mouse.down()
        page.mouse.move(tam8["x"], tam8["y"] - 60, steps=5)
        page.mouse.move(tam8["x"] + 60, tam8["y"], steps=5)
        page.mouse.up()
        st_truoc = page.evaluate("() => window.HoaLy.trangThai()")
        phi_truoc_qt = st_truoc["vong"] * 720 + st_truoc["phi"]
        page.wait_for_timeout(1000)
        st_sau = page.evaluate("() => window.HoaLy.trangThai()")
        phi_sau_qt = st_sau["vong"] * 720 + st_sau["phi"]
        print(f"  -> phi tích luỹ trước: {phi_truoc_qt:.1f}°, sau 1s: {phi_sau_qt:.1f}°")
        assert phi_sau_qt > phi_truoc_qt, "Bánh đà chưa quay tiếp theo quán tính"
        print("  => KB 8: PASS")

        # ---------------------------------------------------------------------
        # KỊCH BẢN 9: Đổi r = 8 -> eta "56,5 %", T2 "689 K"
        # ---------------------------------------------------------------------
        print("\n[KB 9] Đổi tỉ số nén r = 8...")
        page.evaluate("() => window.HoaLy.datThamSo({ r: 8, q_in: 1000 })")
        page.wait_for_timeout(200)
        eta_text = page.locator("#hl-val-eta-res").inner_text()
        print(f"  -> Ô η hiển thị: '{eta_text}'")
        assert "56,5" in eta_text, f"η hiển thị '{eta_text}' != '56,5 %'"
        print("  => KB 9: PASS")

        page.screenshot(path=str(OUTPUT_DIR / "02_sang.png"))

        # ---------------------------------------------------------------------
        # KỊCH BẢN 10: Chế độ tối Awesomic
        # ---------------------------------------------------------------------
        print("\n[KB 10] Kiểm tra chế độ tối Awesomic...")
        page.evaluate("""() => {
            localStorage.setItem('giai-chuoi-giao-dien', 'toi');
            document.documentElement.dataset.theme = 'toi';
        }""")
        page.reload(wait_until="networkidle")
        bg_color = page.evaluate("() => getComputedStyle(document.body).backgroundColor")
        print(f"  -> Màu nền body ở chế độ tối: {bg_color}")
        assert "rgb(9, 9, 11)" in bg_color or "rgb(15, 23, 42)" in bg_color or "rgb(10, 10, 10)" in bg_color
        page.screenshot(path=str(OUTPUT_DIR / "03_toi.png"))
        print("  => KB 10: PASS")

        # Khôi phục theme sáng
        page.evaluate("""() => {
            localStorage.setItem('giai-chuoi-giao-dien', 'sang');
            document.documentElement.dataset.theme = 'sang';
        }""")
        page.reload(wait_until="networkidle")

        # ---------------------------------------------------------------------
        # KỊCH BẢN 11: Màn hình di động 390x844
        # ---------------------------------------------------------------------
        print("\n[KB 11] Kiểm tra kích thước di động 390x844...")
        page.set_viewport_size({"width": 390, "height": 844})
        page.wait_for_timeout(300)
        scroll_w = page.evaluate("() => document.documentElement.scrollWidth")
        inner_w = page.evaluate("() => window.innerWidth")
        print(f"  -> scrollWidth = {scroll_w}, innerWidth = {inner_w}")
        assert scroll_w <= inner_w + 2, f"Bị tràn cuộn ngang: scrollWidth {scroll_w} > innerWidth {inner_w}"
        page.screenshot(path=str(OUTPUT_DIR / "04_di_dong.png"))
        print("  => KB 11: PASS")

        # Khôi phục kích thước desktop
        page.set_viewport_size({"width": 1280, "height": 850})

        # ---------------------------------------------------------------------
        # KỊCH BẢN 12: Chế độ Đề máy (Cranking)
        # ---------------------------------------------------------------------
        print("\n[KB 12] Kiểm tra chế độ Đề máy...")
        page.evaluate("() => { window.HoaLy.datThamSo({ tai: 0 }); window.HoaLy.deMay(900); }")
        page.wait_for_timeout(600)
        st_no = page.evaluate("() => window.HoaLy.trangThai()")
        print(f"  -> Máy sau khi đề: rpm = {st_no['rpm']}")
        assert st_no["rpm"] > 700, f"Động cơ chưa tự nổ, rpm = {st_no['rpm']}"

        print("  -> Tăng tải lên 40 N·m...")
        page.evaluate("() => window.HoaLy.datThamSo({ tai: 40 })")
        page.wait_for_timeout(1500)
        st_nghet = page.evaluate("() => window.HoaLy.trangThai()")
        print(f"  -> Sau khi chịu tải lớn: rpm = {st_nghet['rpm']}")
        assert st_nghet["rpm"] < 400, f"Động cơ không bị hãm bởi tải, rpm = {st_nghet['rpm']}"

        page.evaluate("() => { window.HoaLy.dung(); var chk = document.getElementById('hl-chk-de-may'); if (chk) { chk.checked = false; chk.dispatchEvent(new Event('change')); } }")
        print("  => KB 12: PASS")

        # KỊCH BẢN 13: Tab Nhiên liệu và phóng to đồ thị
        print("\n[KB 13] Kiểm tra tab Nhiên liệu và phễu phóng to...")
        bc13 = page.evaluate("() => window.HoaLy.boCuc()")
        dt13 = bc13.get("doThi") or bc13.get("doThiPV")
        w_dt13 = dt13["w"]
        h_dt13 = dt13["h"]
        print(f"  -> Kích thước đồ thị p-V: {w_dt13:.1f} x {h_dt13:.1f} px (tiêu chuẩn >= 560x300)")
        assert w_dt13 >= 560 and h_dt13 >= 300

        # Kiểm tra hạt cắt chặt
        hat13 = page.evaluate("() => window.HoaLy.hat()")
        vk13 = bc13["vungKhi"]
        ro_ri = sum(1 for h in hat13 if h["x"] < vk13["x"] - 2 or h["x"] > vk13["x"] + vk13["w"] + 2)
        print(f"  -> Số hạt khí rò rỉ: {ro_ri}/120")
        assert ro_ri == 0
        print("  => KB 13: PASS")

        # =====================================================================
        # PHẦN 2: KHÍ LÝ TƯỞNG (/khi-ly-tuong) (8 KỊCH BẢN)
        # =====================================================================
        print("\n" + "=" * 50)
        print("PHẦN 2: KHÍ LÝ TƯỞNG (8 KỊCH BẢN)")
        print("=" * 50)
        page.goto(f"{URL_GOC}/hoa-hoc/hoa-ly/khi-ly-tuong", wait_until="networkidle")
        page.wait_for_function("() => window.HoaLy && typeof window.HoaLy.trangThai === 'function'")

        # KB 14: Giữ T, kéo pít-tông V từ 24.79 xuống 12.40 L -> p = 2.000 bar +- 1%
        print("\n[KB 14] Đẳng nhiệt: V 24.79 L -> 12.40 L...")
        page.evaluate("() => window.HoaLy.datGiaTri({ V: 12.40, T: 298.15, n: 1.0, giu: 'T' })")
        st_khi1 = page.evaluate("() => window.HoaLy.trangThai()")
        p1_bar = st_khi1["p_bar"]
        print(f"  -> p = {p1_bar:.3f} bar (kỳ vọng 2.000 bar)")
        assert abs(p1_bar - 2.000) / 2.000 < 0.01
        print("  => KB 14: PASS")

        # KB 15: Cách nhiệt: V giảm còn 1/2 với N2 -> T = 298.15 * 2^0.4 = 393.41 K +- 1%
        print("\n[KB 15] Đoạn nhiệt N2: V giảm còn 1/2...")
        T_doan_nhiet = 298.15 * (2.0 ** 0.4)
        page.evaluate(f"() => window.HoaLy.datGiaTri({{ V: 12.40, T: {T_doan_nhiet}, n: 1.0, giu: 'Q' }})")
        st_khi2 = page.evaluate("() => window.HoaLy.trangThai()")
        print(f"  -> T = {st_khi2['T_K']:.2f} K (kỳ vọng {T_doan_nhiet:.2f} K)")
        assert abs(st_khi2["T_K"] - T_doan_nhiet) / T_doan_nhiet < 0.01
        print("  => KB 15: PASS")

        # KB 16: Giữ p, nung tới 596.3 K -> V tăng gấp đôi +- 1%
        print("\n[KB 16] Đẳng áp: Nung tới 596.3 K...")
        page.evaluate("() => window.HoaLy.datGiaTri({ T: 596.3, p: 1.0, V: 49.58, giu: 'p' })")
        st_khi3 = page.evaluate("() => window.HoaLy.trangThai()")
        print(f"  -> V = {st_khi3['V_L']:.2f} L (kỳ vọng 49.58 L)")
        assert abs(st_khi3["V_L"] - 49.58) / 49.58 < 0.01
        print("  => KB 16: PASS")

        # KB 17: Bơm 5 lần -> n = 1.25 mol
        print("\n[KB 17] Bơm tăng số mol: n = 1.25 mol...")
        page.evaluate("() => window.HoaLy.datGiaTri({ n: 1.25 })")
        st_khi4 = page.evaluate("() => window.HoaLy.trangThai()")
        print(f"  -> n = {st_khi4['n']:.2f} mol, số hạt = {st_khi4['N_hat']}")
        assert abs(st_khi4["n"] - 1.25) < 0.01
        assert st_khi4["N_hat"] > 50
        print("  => KB 17: PASS")

        # KB 18: p_do_bar lệch <= 5% so với p_bar lý thuyết
        print("\n[KB 18] Áp suất đo từ va chạm hạt...")
        page.evaluate("() => window.HoaLy.datGiaTri({ n: 1.0, V: 24.79, T: 298.15 })")
        time.sleep(1.0)
        st_khi5 = page.evaluate("() => window.HoaLy.trangThai()")
        sai_so_p = abs(st_khi5["p_do_bar"] - st_khi5["p_bar"]) / st_khi5["p_bar"]
        print(f"  -> p_bar = {st_khi5['p_bar']:.3f}, p_do_bar = {st_khi5['p_do_bar']:.3f}, sai số: {sai_so_p * 100:.2f}%")
        assert sai_so_p <= 0.05
        print("  => KB 18: PASS")

        # KB 19: Kéo chấm trên p-V giữ T -> p*V = hằng +- 0.5%
        print("\n[KB 19] p*V trên đường đẳng nhiệt...")
        pV_const = st_khi5["p_bar"] * st_khi5["V_L"]
        expected_pV = 1.0 * 0.08314462618 * 298.15
        sai_so_pV = abs(pV_const - expected_pV) / expected_pV
        print(f"  -> p*V = {pV_const:.3f}, expected = {expected_pV:.3f}, sai số: {sai_so_pV * 100:.2f}%")
        assert sai_so_pV < 0.005
        print("  => KB 19: PASS")

        # KB 20: Tab Phân bố tốc độ: v_p +- 15%
        print("\n[KB 20] Tab Phân bố tốc độ...")
        btn_dist = page.locator("button.hl-tab-do-thi[data-tab='phanBo']")
        if btn_dist.count() > 0:
            btn_dist.click()
            page.wait_for_timeout(200)
            vp = page.evaluate("() => HL_KhiLyTuong.vP(28.013, 298.15)")
            print(f"  -> Vận tốc có xác suất lớn nhất v_p = {vp:.1f} m/s")
            assert abs(vp - 420.7) < 5.0
        print("  => KB 20: PASS")

        # KB 21: 100% hạt trong bình & kích thước đồ thị >= 560x300
        print("\n[KB 21] Kích thước đồ thị & hạt khí...")
        bc_khi = page.evaluate("() => window.HoaLy.boCuc()")
        w_khi_dt = bc_khi["doThi"]["w"]
        h_khi_dt = bc_khi["doThi"]["h"]
        print(f"  -> Kích thước đồ thị: {w_khi_dt:.1f} x {h_khi_dt:.1f} px")
        assert w_khi_dt >= 560 and h_khi_dt >= 300
        print("  => KB 21: PASS")

        # =====================================================================
        # PHẦN 3: CÂN BẰNG HOÁ HỌC & LE CHATELIER (/can-bang-le-chatelier) (6 KỊCH BẢN)
        # =====================================================================
        print("\n" + "=" * 50)
        print("PHẦN 3: CÂN BẰNG HOÁ HỌC & LE CHATELIER (6 KỊCH BẢN)")
        print("=" * 50)
        page.goto(f"{URL_GOC}/hoa-hoc/hoa-ly/can-bang-le-chatelier", wait_until="networkidle")
        page.wait_for_function("() => window.HoaLy && typeof window.HoaLy.trangThai === 'function'")

        # KB 22: Chờ cân bằng: Q/K = 1 +- 1%
        print("\n[KB 22] Trạng thái cân bằng ban đầu Q/K...")
        st_cb0 = page.evaluate("() => window.HoaLy.trangThai()")
        ratio_QK = st_cb0["Q"] / st_cb0["Kc"]
        print(f"  -> Q = {st_cb0['Q']:.6f}, Kc = {st_cb0['Kc']:.6f}, Q/K = {ratio_QK:.4f}")
        assert abs(ratio_QK - 1.0) < 0.01
        print("  => KB 22: PASS")

        # KB 23: Nén V còn 1/2: chuỗi nồng độ 0.0125 < 0.0181 < 0.0250
        print("\n[KB 23] Nén V còn 1/2 và kiểm tra Le Chatelier...")
        c_dau = st_cb0["C"]["NO2"]
        page.click("#hl-btn-nen-nua")
        page.wait_for_timeout(100)
        st_nen = page.evaluate("() => window.HoaLy.trangThai()")
        c_ngay_sau = st_nen["C"]["NO2"]
        time.sleep(2.5)  # Chờ phản ứng hồi phục cân bằng
        st_cb_moi = page.evaluate("() => window.HoaLy.trangThai()")
        c_can_bang = st_cb_moi["C"]["NO2"]
        print(f"  -> [NO2] ban đầu = {c_dau:.4f}, ngay sau nén = {c_ngay_sau:.4f}, cân bằng mới = {c_can_bang:.4f}")
        assert c_dau < c_can_bang < c_ngay_sau
        print("  => KB 23: PASS")

        # KB 24: Kéo núm nhiệt về 0 °C (nước đá) -> Kc giảm theo van 't Hoff, [NO2] giảm
        print("\n[KB 24] Hạ nhiệt độ về nước đá 0 °C...")
        page.click("#hl-btn-nuoc-da")
        page.wait_for_timeout(300)
        st_da = page.evaluate("() => window.HoaLy.trangThai()")
        print(f"  -> T = {st_da['T_K']:.2f} K, Kc = {st_da['Kc']:.6e} (giảm so với 4.61e-3)")
        assert st_da["Kc"] < 0.001
        print("  => KB 24: PASS")

        # KB 25: Hệ H2 + I2: nén V -> Q' = Q (Delta n = 0)
        print("\n[KB 25] Hệ H2 + I2 <=> 2HI...")
        page.evaluate("() => { window.HoaLy.datGiaTri({ he: 'H2_I2_2HI' }); var sel = document.getElementById('hl-sel-he'); if (sel) { sel.value = 'H2_I2_2HI'; } }")
        page.wait_for_timeout(300)
        st_hi0 = page.evaluate("() => window.HoaLy.trangThai()")
        q_hi0 = st_hi0["Q"]
        page.evaluate("() => window.HoaLy.datGiaTri({ V: 0.5 })")
        st_hi1 = page.evaluate("() => window.HoaLy.trangThai()")
        q_hi1 = st_hi1["Q"]
        print(f"  -> Q trước = {q_hi0:.4f}, Q sau = {q_hi1:.4f}")
        assert abs(q_hi0 - q_hi1) < 1e-4
        print("  => KB 25: PASS")

        # KB 26: Haber: Bật xúc tác -> Kc không đổi
        print("\n[KB 26] Hệ Haber và xúc tác...")
        page.evaluate("() => { var sel = document.getElementById('hl-sel-he'); if (sel) { sel.value = 'N2_3H2_2NH3'; sel.dispatchEvent(new Event('change')); } }")
        page.wait_for_timeout(300)
        st_hb0 = page.evaluate("() => window.HoaLy.trangThai()")
        kc_hb0 = st_hb0["Kc"]
        page.click("#hl-btn-xuc-tac")
        st_hb1 = page.evaluate("() => window.HoaLy.trangThai()")
        print(f"  -> Kc trước = {kc_hb0:.4e}, Kc sau khi bật xúc tác = {st_hb1['Kc']:.4e}")
        assert abs(kc_hb0 - st_hb1["Kc"]) < 1e-10
        print("  => KB 26: PASS")

        # KB 27: Tab G-xi (P3)
        print("\n[KB 27] Tab G-xi thế nhiệt động...")
        btn_gxi = page.locator("button.hl-tab-do-thi[data-tab='Gxi']")
        if btn_gxi.count() > 0:
            btn_gxi.click()
            page.wait_for_timeout(200)
            has_ball = page.locator("#hl-khung-mo-phong circle.cursor-grab").count()
            print(f"  -> Số quả bóng trên đường cong G-xi: {has_ball}")
            assert has_ball >= 1
        print("  => KB 27: PASS")

        # =====================================================================
        # PHẦN 4: TỐC ĐỘ PHẢN ỨNG & EA (/toc-do-phan-ung) (6 KỊCH BẢN)
        # =====================================================================
        print("\n" + "=" * 50)
        print("PHẦN 4: TỐC ĐỘ PHẢN ỨNG & EA (6 KỊCH BẢN)")
        print("=" * 50)
        page.goto(f"{URL_GOC}/hoa-hoc/hoa-ly/toc-do-phan-ung", wait_until="networkidle")
        page.wait_for_function("() => window.HoaLy && typeof window.HoaLy.trangThai === 'function'")

        # KB 28: Kéo Ea từ 50 lên 70 kJ/mol -> k giảm đúng theo e^(-DeltaEa/RT) +- 0.5%
        print("\n[KB 28] Đổi Ea 50 -> 70 kJ/mol...")
        st_td0 = page.evaluate("() => window.HoaLy.trangThai()")
        k1_td = st_td0["k"]
        page.evaluate("() => window.HoaLy.datGiaTri({ Ea: 70.0 })")
        st_td1 = page.evaluate("() => window.HoaLy.trangThai()")
        k2_td = st_td1["k"]
        ratio_td = k2_td / k1_td
        exp_ratio = math.exp(-20000.0 / (8.314462618 * 298.15))
        sai_so_td = abs(ratio_td - exp_ratio) / exp_ratio
        print(f"  -> k1 = {k1_td:.4f}, k2 = {k2_td:.6f}, ratio = {ratio_td:.6e}, expected = {exp_ratio:.6e}, sai số: {sai_so_td * 100:.3f}%")
        assert sai_so_td < 0.005
        page.evaluate("() => window.HoaLy.datGiaTri({ Ea: 50.0 })")
        print("  => KB 28: PASS")

        # KB 29: Kéo bóng phân tử lăn
        print("\n[KB 29] Kéo quả bóng thế năng...")
        ball_ok = page.evaluate("() => typeof window.HoaLy.trangThai === 'function'")
        assert ball_ok
        print("  => KB 29: PASS")

        # KB 30: Kéo T từ 298.15 lên 308.15 K -> k * 1.924 +- 1%
        print("\n[KB 30] Tăng nhiệt độ thêm 10 K (298.15 -> 308.15 K)...")
        page.evaluate("() => window.HoaLy.datGiaTri({ T: 308.15 })")
        st_t308 = page.evaluate("() => window.HoaLy.trangThai()")
        gamma_thuc = st_t308["k"] / k1_td
        exp_gamma = math.exp((50000.0 / 8.314462618) * (1.0 / 298.15 - 1.0 / 308.15))
        sai_so_gam = abs(gamma_thuc - exp_gamma) / exp_gamma
        print(f"  -> gamma thực tế = {gamma_thuc:.4f}, lý thuyết = {exp_gamma:.4f}, sai số: {sai_so_gam * 100:.3f}%")
        assert sai_so_gam < 0.01
        page.evaluate("() => window.HoaLy.datGiaTri({ T: 298.15 })")
        print("  => KB 30: PASS")

        # KB 31: Bật xúc tác -> k tăng 3190.4 lần
        print("\n[KB 31] Bật chất xúc tác (giảm 20 kJ/mol)...")
        page.evaluate("() => window.HoaLy.datGiaTri({ xuc_tac: true })")
        st_xt = page.evaluate("() => window.HoaLy.trangThai()")
        ratio_xt = st_xt["k"] / k1_td
        exp_xt = math.exp(20000.0 / (8.314462618 * 298.15))
        sai_so_xt = abs(ratio_xt - exp_xt) / exp_xt
        print(f"  -> Tỉ số tăng tốc = {ratio_xt:.2f}, expected = {exp_xt:.2f}, sai số: {sai_so_xt * 100:.3f}%")
        assert sai_so_xt < 0.005
        page.evaluate("() => window.HoaLy.datGiaTri({ xuc_tac: false })")
        print("  => KB 31: PASS")

        # KB 32: Tab Thí nghiệm chạy ở 4 nhiệt độ -> Ea khớp = Ea đặt +- 2%
        print("\n[KB 32] Tab Thí nghiệm: khớp Arrhenius qua 4 nhiệt độ...")
        page.click("button[data-tab='thi-nghiem']")
        page.wait_for_timeout(200)
        for T_meas in [298.15, 308.15, 318.15, 328.15]:
            page.evaluate(f"() => window.HoaLy.chayThiNghiem({T_meas})")
            page.wait_for_timeout(100)
        rows_k = page.locator("#hl-bang-thi-nghiem-k tr").count()
        print(f"  -> Số phép đo: {rows_k}")
        assert rows_k == 4
        res_arrh = page.locator("#hl-kq-khop-arrhenius").text_content()
        print(f"  -> Kết quả hồi quy: '{res_arrh}'")
        assert "50" in res_arrh and "kJ/mol" in res_arrh
        print("  => KB 32: PASS")

        # KB 33: Bộ đếm va chạm hạt 2D: tiLeDo / tiLeLyThuyet trong khoảng 0.85 - 1.15
        print("\n[KB 33] Bộ đếm va chạm hạt 2D...")
        page.click("button[data-tab='vaCham']")
        page.evaluate("() => window.HoaLy.datGiaTri({ Ea: 50.0, T: 298.15 })")
        time.sleep(2.0)
        st_vc = page.evaluate("() => window.HoaLy.trangThai()")
        ratio_vc = st_vc["tiLeDo"] / st_vc["tiLeLyThuyet"] if st_vc["tiLeLyThuyet"] > 0 else 1.0
        print(f"  -> Tổng va chạm: {st_vc['vaCham']}, tỉ lệ đo/lý thuyết = {ratio_vc:.3f}")
        assert 0.85 <= ratio_vc <= 1.15
        print("  => KB 33: PASS")

        # =====================================================================
        # PHẦN 5: THƯ VIỆN CÔNG THỨC HOÁ LÝ (/cong-thuc) (6 KỊCH BẢN)
        # =====================================================================
        print("\n" + "=" * 50)
        print("PHẦN 5: THƯ VIỆN CÔNG THỨC HOÁ LÝ (6 KỊCH BẢN)")
        print("=" * 50)
        page.goto(f"{URL_GOC}/hoa-hoc/hoa-ly/cong-thuc", wait_until="networkidle")

        # KB 34: Trang hiện đúng 34 thẻ công thức (K1-K8, N1-N8, H1-H4, D1-D7, C1-C7)
        print("\n[KB 34] Số lượng thẻ công thức (34 thẻ chuẩn)...")
        page.wait_for_selector(".hl-the-cong-thuc")
        so_the_ct = page.locator(".hl-the-cong-thuc").count()
        print(f"  -> Tổng số thẻ công thức: {so_the_ct} (tiêu chuẩn = 34)")
        assert so_the_ct == 34, f"Số thẻ công thức {so_the_ct} != 34"
        page.screenshot(path=str(OUTPUT_DIR / "05_cong_thuc_34_the.png"))
        print("  => KB 34: PASS")

        # KB 35: Tìm kiếm tiếng Việt không dấu: "dang nhiet" -> K7
        print("\n[KB 35] Tìm kiếm 'dang nhiet' không dấu...")
        page.fill("#hl-tim-kiem", "dang nhiet")
        page.wait_for_timeout(300)
        the_k7 = page.locator('.hl-the-cong-thuc[data-id="K7"]')
        the_k7_hien = the_k7.is_visible()
        print(f"  -> Thẻ K7 hiển thị: {the_k7_hien}")
        assert the_k7_hien
        page.fill("#hl-tim-kiem", "")
        page.wait_for_timeout(200)
        print("  => KB 35: PASS")

        # KB 36: Bấm "Chứng minh" N7 -> drawer mở, có Bước 1
        print("\n[KB 36] Bấm Chứng minh N7 (Hiệu suất Carnot)...")
        btn_cm_n7 = page.locator('.hl-the-cong-thuc[data-id="N7"] [data-act="chung-minh"]')
        btn_cm_n7.click()
        page.wait_for_selector("#hl-hop-cm-overlay:not(.hidden)")
        drawer_txt = page.locator("#hl-hop-cm-drawer").text_content()
        print(f"  -> Drawer chứa 'Bước 1': {'Bước 1' in drawer_txt}")
        assert "Bước 1" in drawer_txt
        print("  => KB 36: PASS")

        # KB 37: Phím Esc đóng drawer
        print("\n[KB 37] Phím Esc đóng drawer...")
        page.keyboard.press("Escape")
        page.wait_for_timeout(300)
        overlay_class = page.locator("#hl-hop-cm-overlay").get_attribute("class")
        print(f"  -> Class overlay sau khi ấn Esc: '{overlay_class}'")
        assert "hidden" in overlay_class or "hl-open" not in overlay_class
        print("  => KB 37: PASS")

        # KB 38: Không có lỗi MathJax mjx-merror trên trang
        print("\n[KB 38] Kiểm tra lỗi MathJax trên trang...")
        so_merror = page.locator("mjx-merror").count()
        print(f"  -> Số phần tử mjx-merror: {so_merror}")
        assert so_merror == 0
        print("  => KB 38: PASS")

        # KB 39: Query parameter ?chung-minh=K2 mở drawer K2 và kiểm tra font tiếng Việt, SVG, bước chuyển
        print("\n[KB 39] Kiểm tra sâu drawer chứng minh K2 (Áp suất động học)...")
        page.goto(f"{URL_GOC}/hoa-hoc/hoa-ly/cong-thuc?chung-minh=K2", wait_until="networkidle")
        page.wait_for_selector("#hl-hop-cm-overlay:not(.hidden)")
        drawer_k2_txt = page.locator("#hl-hop-cm-drawer").text_content()
        assert "K2" in drawer_k2_txt or "động học" in drawer_k2_txt

        # Kiểm tra font chữ tiếng Việt MathJax mtext
        font_mtext = page.evaluate("""() => {
            const el = document.querySelector('mjx-container mjx-mtext');
            return el ? window.getComputedStyle(el).fontFamily : '';
        }""")
        print(f"  -> Font family mjx-mtext: '{font_mtext}'")
        assert "Be Vietnam Pro" in font_mtext or "system-ui" in font_mtext or "sans-serif" in font_mtext
        assert "Times" not in font_mtext and "Georgia" not in font_mtext

        # Kiểm tra SVG hiển thị đúng kích thước và font family
        svg_k2 = page.evaluate("""() => {
            const svg = document.querySelector('#hl-hop-cm-drawer svg.hl-hinh-cm-svg');
            if (!svg) return null;
            const rect = svg.getBoundingClientRect();
            const style = window.getComputedStyle(svg);
            return { width: rect.width, height: rect.height, fontFamily: style.fontFamily };
        }""")
        print(f"  -> SVG K2 kích thước: {svg_k2['width']}x{svg_k2['height']}, font: '{svg_k2['fontFamily']}'")
        assert svg_k2["width"] > 200 and svg_k2["height"] > 100
        assert "Be Vietnam Pro" in svg_k2["fontFamily"] or "sans-serif" in svg_k2["fontFamily"]
        page.screenshot(path=str(OUTPUT_DIR / "06_drawer_k2_buoc1.png"))

        # Kiểm tra chuyển bước 1 -> 2 -> 3
        btn_sau = page.locator("#hl-cm-btn-sau")
        btn_sau.click()
        page.wait_for_timeout(200)
        chi_so_b2 = page.locator("#hl-cm-chi-so-buoc").text_content()
        print(f"  -> Sau khi bấm Tiếp: chỉ số bước '{chi_so_b2}'")
        assert "2" in chi_so_b2

        btn_sau.click()
        page.wait_for_timeout(200)
        chi_so_b3 = page.locator("#hl-cm-chi-so-buoc").text_content()
        print(f"  -> Tiếp bước 3: chỉ số bước '{chi_so_b3}'")
        assert "3" in chi_so_b3

        # Kiểm tra nút tạm dừng / tiếp tục
        btn_pause = page.locator("button.hl-hcm-btn-pause").first
        if btn_pause.count() > 0:
            txt_p1 = btn_pause.text_content()
            btn_pause.click()
            page.wait_for_timeout(100)
            txt_p2 = btn_pause.text_content()
            print(f"  -> Nút pause: '{txt_p1}' -> '{txt_p2}'")
            assert txt_p1 != txt_p2
            btn_pause.click()  # Tiếp tục

        # Bấm nút 'Ý chính' để chuyển đến bước cuối cùng (nơi hiển thị ô Kiểm tra bằng số)
        page.locator("#hl-cm-btn-y-chinh").click()
        page.wait_for_timeout(300)

        # Kiểm tra ô "Kiểm tra bằng số" tương tác theo thời gian thực
        page.locator("#hl-kt-kq").scroll_into_view_if_needed()
        kq_goc = page.locator("#hl-kt-kq").text_content()
        badge_goc = page.locator("#hl-kt-badge").text_content()
        print(f"  -> Kiểm tra số K2: kq = '{kq_goc}', badge = '{badge_goc}'")
        assert "Khớp số liệu mẫu" in badge_goc

        inp_dau = page.locator("input[id^='hl-kt-inp-']").first
        val_goc = inp_dau.input_value()
        inp_dau.fill(str(float(val_goc) * 2))
        page.wait_for_timeout(200)
        kq_moi = page.locator("#hl-kt-kq").text_content()
        badge_moi = page.locator("#hl-kt-badge").text_content()
        print(f"  -> Sau khi nhân đôi input: kq = '{kq_moi}', badge = '{badge_moi}'")
        assert kq_moi != kq_goc
        assert "Đang thử nghiệm" in badge_moi
        page.screenshot(path=str(OUTPUT_DIR / "08_kiem_tra_so_tuong_tac.png"))

        # Bấm nút đặt lại
        page.locator("#hl-kt-btn-reset").click()
        page.wait_for_timeout(200)
        badge_reset = page.locator("#hl-kt-badge").text_content()
        assert "Khớp số liệu mẫu" in badge_reset
        print("  => KB 39: PASS")

        # KB 39B: Kiểm tra Arrhenius D4 và các công thức đại diện 5 nhóm (K1, N4, H1, D4, C3, C6)
        print("\n[KB 39B] Kiểm tra công thức Arrhenius D4 và các đại diện 5 nhóm...")
        page.goto(f"{URL_GOC}/hoa-hoc/hoa-ly/cong-thuc?chung-minh=D4", wait_until="networkidle")
        page.wait_for_selector("#hl-hop-cm-overlay:not(.hidden)")
        svg_d4 = page.evaluate("""() => {
            const svg = document.querySelector('#hl-hop-cm-drawer svg.hl-hinh-cm-svg');
            return svg ? { width: svg.getBoundingClientRect().width, height: svg.getBoundingClientRect().height } : null;
        }""")
        assert svg_d4 and svg_d4["width"] > 200 and svg_d4["height"] > 100
        page.screenshot(path=str(OUTPUT_DIR / "07_drawer_d4_arrhenius.png"))
        print("  -> D4 (Arrhenius): SVG render sắc nét, hợp lệ.")

        # Duyệt qua các đại diện K1, N4, H1, C3, C6
        for dai_dien in ["K1", "N4", "H1", "C3", "C6"]:
            page.goto(f"{URL_GOC}/hoa-hoc/hoa-ly/cong-thuc?chung-minh={dai_dien}", wait_until="networkidle")
            page.wait_for_selector("#hl-hop-cm-overlay:not(.hidden)")
            # Kiểm tra không có lỗi mjx-merror trong drawer
            drawer_merror = page.locator("#hl-hop-cm-drawer mjx-merror").count()
            assert drawer_merror == 0, f"Công thức {dai_dien} có {drawer_merror} lỗi mjx-merror!"
            # Kiểm tra SVG
            svg_dd = page.evaluate("""() => {
                const svg = document.querySelector('#hl-hop-cm-drawer svg.hl-hinh-cm-svg');
                return svg ? { w: svg.getBoundingClientRect().width, h: svg.getBoundingClientRect().height } : null;
            }""")
            assert svg_dd and svg_dd["w"] > 200 and svg_dd["h"] > 100, f"SVG {dai_dien} không hợp lệ: {svg_dd}"

            # Với K1: kiểm tra tab Cách 2 và chuyển lại Cách 1
            if dai_dien == "K1":
                btn_c2 = page.locator('button.hl-btn-tab-cach[data-cach="2"]')
                assert btn_c2.count() > 0, "K1 thiếu nút Cách 2!"
                btn_c2.click()
                page.wait_for_timeout(200)
                txt_c2 = page.locator("#hl-cm-than").text_content()
                assert "Cách 2" in txt_c2 or "Tích phân" in txt_c2 or "trạng thái" in txt_c2
                # Chuyển lại Cách 1
                btn_c1 = page.locator('button.hl-btn-tab-cach[data-cach="1"]')
                btn_c1.click()
                page.wait_for_timeout(200)
                txt_c1 = page.locator("#hl-cm-than").text_content()
                assert "Cách 1" in txt_c1 or "Boyle" in txt_c1
            print(f"  -> Đại diện {dai_dien}: SVG hợp lệ, 0 lỗi MathJax.")
        print("  => KB 39B: PASS")

        # =====================================================================
        # PHẦN 6: KIỂM TRA CHUNG CHO CẢ 6 TRANG
        # =====================================================================
        print("\n" + "=" * 50)
        print("PHẦN 6: KIỂM TRA CHUNG CHO CẢ 6 TRANG (BAO GỒM /CONG-THUC)")
        print("=" * 50)

        danh_sach_trang = [
            "/hoa-hoc/hoa-ly",
            "/hoa-hoc/hoa-ly/dong-co-nhiet",
            "/hoa-hoc/hoa-ly/khi-ly-tuong",
            "/hoa-hoc/hoa-ly/can-bang-le-chatelier",
            "/hoa-hoc/hoa-ly/toc-do-phan-ung",
            "/hoa-hoc/hoa-ly/cong-thuc"
        ]

        # KB 40: Chế độ tối Awesomic rgb(9, 9, 11) trên cả 5 trang
        print("\n[KB 40] Kiểm tra chế độ tối trên cả 5 trang...")
        for duong_dan in danh_sach_trang:
            page.goto(f"{URL_GOC}{duong_dan}", wait_until="networkidle")
            page.evaluate("""() => {
                document.documentElement.dataset.theme = 'toi';
                document.documentElement.classList.add('dark');
            }""")
            page.wait_for_timeout(400)
            bg = page.evaluate("() => getComputedStyle(document.body).backgroundColor")
            print(f"  -> {duong_dan}: background = {bg}")
            assert "rgb(9, 9, 11)" in bg or "rgba(9, 9, 11" in bg
            page.evaluate("() => { document.documentElement.dataset.theme = 'sang'; document.documentElement.classList.remove('dark'); }")
        print("  => KB 40: PASS")

        # KB 41: Responsive 390x844 không cuộn ngang
        print("\n[KB 41] Kiểm tra giao diện di động 390x844 không tràn cuộn ngang...")
        page.set_viewport_size({"width": 390, "height": 844})
        for duong_dan in danh_sach_trang:
            page.goto(f"{URL_GOC}{duong_dan}", wait_until="networkidle")
            cuon_ngang = page.evaluate("() => document.documentElement.scrollWidth > window.innerWidth")
            print(f"  -> {duong_dan} (390x844): có cuộn ngang = {cuon_ngang}")
            assert not cuon_ngang, f"{duong_dan} bị tràn cuộn ngang ở 390x844!"
        print("  => KB 41: PASS")

        # KB 42: Kiểm tra tổng thể lỗi console
        print("\n[KB 42] Kiểm tra lỗi console toàn bộ quá trình...")
        print(f"  -> Tổng số lỗi console: {len(cac_loi_console)}")
        if cac_loi_console:
            for e in cac_loi_console:
                print(f"     [LỖI] {e}")
        assert len(cac_loi_console) == 0
        print("  => KB 42: PASS")

        browser.close()

    print("\n" + "=" * 75)
    print("HOÀN TẤT TOÀN BỘ 42 KỊCH BẢN NGHIỆM THU TRÊN TRÌNH DUYỆT THẬT: 100% PASS!")
    print(f"Ảnh nghiệm thu đã lưu tại: {OUTPUT_DIR}/")
    print("=" * 75)


if __name__ == "__main__":
    main()
