"""Kiểm thử tích hợp phòng lab Unity WebGL vào ứng dụng CHEMISTRY (GĐ nhúng lab).
- Kiểm tra trang phong-thi-nghiem.html chứa importmap cho ptn/lab-unity-app và ptn/chon-che-do.
- Kiểm tra file lab-unity-app.js kiểm tra chemistryApp.labWebGL, xử lý Quit trước moAppLab và có fallback về Three.js.
- Kiểm tra desktop/main.js đăng ký scheme chemlab và giao thức xử lý tài nguyên WebGL.
"""
from pathlib import Path

GOC = Path(__file__).resolve().parent.parent


def test_trang_lab_co_importmap_va_script_chon_che_do():
    html_file = GOC / "frontend" / "phong-thi-nghiem.html"
    assert html_file.exists(), "phong-thi-nghiem.html phải tồn tại"
    noi_dung = html_file.read_text(encoding="utf-8")

    assert "chon-che-do-lab.js" in noi_dung, "Phải nạp chon-che-do-lab.js"
    assert '"ptn/chon-che-do"' in noi_dung, "importmap phải có ptn/chon-che-do"
    assert '"ptn/lab-unity-app"' in noi_dung, "importmap phải có ptn/lab-unity-app"
    assert 'id="ptn-nut-ban-day-du"' in noi_dung, "Phải có nút Bản 3D đầy đủ để chuyển lại Unity"


def test_lab_unity_app_ma_nguon():
    js_file = GOC / "frontend" / "hoa-hoc" / "phong-thi-nghiem" / "lab-unity-app.js"
    assert js_file.exists(), "lab-unity-app.js phải tồn tại"
    code = js_file.read_text(encoding="utf-8")

    # Kiểm tra chỉ chạy khi có chemistryApp.labWebGL
    assert "chemistryApp.labWebGL" in code, "Phải kiểm tra window.chemistryApp.labWebGL"

    # Kiểm tra Quit trước khi mở app ngoài moAppLab
    assert "moAppLab" in code, "Phải có gọi moAppLab"
    assert "Quit()" in code, "Phải gọi Quit() để giải phóng bộ nhớ"

    # Fallback khi lỗi
    assert "onChuyenThreeJs" in code, "Phải có callback fallback về Three.js"

    # Hook kiểm thử
    assert "PhongLab.app" in code, "Phải gắn hook window.PhongLab.app"


def test_electron_main_dang_ky_chemlab_va_ipc():
    main_js = GOC / "desktop" / "main.js"
    assert main_js.exists(), "desktop/main.js phải tồn tại"
    code = main_js.read_text(encoding="utf-8")

    assert "chemlab" in code, "desktop/main.js phải đăng ký scheme chemlab"
    assert "registerSchemesAsPrivileged" in code, "Phải đặc quyền hoá scheme chemlab"
    assert 'ipcMain.handle("lab:webgl"' in code, "Phải đăng ký IPC handler lab:webgl"
