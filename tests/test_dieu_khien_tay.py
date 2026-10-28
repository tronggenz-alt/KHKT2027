"""tests/test_dieu_khien_tay.py
Kiểm tra tính toàn vẹn của phân hệ điều khiển cử chỉ tay:
1. Vendor ngoại tuyến đầy đủ, SHA256 khớp, mô hình 7 819 105 bytes.
2. Không chứa URL CDN ngoại tuyến (cdn., jsdelivr, unpkg, googleapis).
3. Cách ly: chỉ trang Sinh học dùng, các trang khác (index, hoa-hoc, hoa-ly, phong-thi-nghiem) không chứa dieu-khien-tay.
4. Demo trả về mã 200, file .wasm trả đúng MIME type application/wasm.
5. Dữ liệu vàng đầy đủ 9 kịch bản.
"""

import hashlib
import json
from pathlib import Path
import pytest
from fastapi.testclient import TestClient

from backend.main import app

GOC = Path(__file__).resolve().parents[1]
client = TestClient(app)


def test_01_vendor_ngoai_tuyen_va_sha256():
    """Vendor MediaPipe và mô hình tồn tại cục bộ, khớp SHA256 và kích thước."""
    p_phien_ban = GOC / "frontend" / "vendor" / "PHIEN_BAN.json"
    assert p_phien_ban.exists(), "Thiếu PHIEN_BAN.json"

    phien_ban = json.loads(p_phien_ban.read_text(encoding="utf-8"))

    cac_file_can = [
        "mediapipe/tasks-vision-1.1.0/vision_bundle.mjs",
        "mediapipe/tasks-vision-1.1.0/wasm/vision_wasm_internal.js",
        "mediapipe/tasks-vision-1.1.0/wasm/vision_wasm_internal.wasm",
        # bản "module": luồng nhận diện riêng (nhan-dien-worker.js, worker kiểu module) chỉ nạp được bản này
        "mediapipe/tasks-vision-1.1.0/wasm/vision_wasm_module_internal.js",
        "mediapipe/tasks-vision-1.1.0/wasm/vision_wasm_module_internal.wasm",
        "mediapipe/hand_landmarker.task"
    ]

    for rel in cac_file_can:
        assert rel in phien_ban, f"Thiếu đăng ký {rel} trong PHIEN_BAN.json"
        p = GOC / "frontend" / "vendor" / rel
        assert p.exists(), f"Không tìm thấy file {p}"

        du_lieu = p.read_bytes()
        sha = hashlib.sha256(du_lieu).hexdigest()
        assert sha == phien_ban[rel]["sha256"], f"Sai SHA256 ở {rel}"
        assert len(du_lieu) == phien_ban[rel]["kich_thuoc_bytes"], f"Sai kích thước ở {rel}"

    # Kiểm tra kích thước chính xác của hand_landmarker.task
    mo_hinh = GOC / "frontend" / "vendor" / "mediapipe" / "hand_landmarker.task"
    assert mo_hinh.stat().st_size == 7819105, "Mô hình hand_landmarker.task phải đúng 7 819 105 byte"


def test_02_khong_chua_cdn_ngoai():
    """Các module dieu-khien-tay không chứa liên kết CDN bên ngoài."""
    thu_muc = GOC / "frontend" / "dieu-khien-tay"
    assert thu_muc.exists()

    tu_khoa_cam = ["cdn.", "jsdelivr.net", "unpkg.com", "googleapis.com"]
    for f in thu_muc.glob("*.js"):
        noi_dung = f.read_text(encoding="utf-8").lower()
        for cdn in tu_khoa_cam:
            assert cdn not in noi_dung, f"{f.name} chứa domain CDN cấm: {cdn}"


def test_03_cach_ly_chi_cho_sinh_hoc():
    """Chỉ Sinh học mới được liên kết dieu-khien-tay. Các trang Hoá học/Trang chủ không được nạp."""
    cac_file_khong_duoc_chua = [
        GOC / "frontend" / "index.html",
        GOC / "frontend" / "app.js",
        GOC / "frontend" / "trang-chu.js",
        GOC / "frontend" / "phong-thi-nghiem.html",
        GOC / "frontend" / "hoa-hoc" / "hoa-ly.html"
    ]

    for p in cac_file_khong_duoc_chua:
        if p.exists():
            nd = p.read_text(encoding="utf-8").lower()
            assert "dieu-khien-tay" not in nd, f"{p.name} không được chứa dieu-khien-tay"


def test_04_demo_va_wasm_endpoint():
    """Demo HTML trả 200 OK và wasm trả application/wasm."""
    resp_demo = client.get("/static/dieu-khien-tay/demo.html")
    assert resp_demo.status_code == 200
    assert "text/html" in resp_demo.headers.get("content-type", "")

    resp_wasm = client.get("/static/vendor/mediapipe/tasks-vision-1.1.0/wasm/vision_wasm_internal.wasm")
    assert resp_wasm.status_code == 200
    assert resp_wasm.headers.get("content-type") == "application/wasm"


def test_05_du_lieu_vang_ton_tai_va_du_9_kich_ban():
    """Dữ liệu vàng có đủ 9 kịch bản và các tệp du-lieu-thu đã được sinh."""
    vang_path = GOC / "tests" / "js" / "du_lieu" / "dieu_khien_tay_vang.json"
    assert vang_path.exists()

    d = json.loads(vang_path.read_text(encoding="utf-8"))
    assert "kich_ban" in d
    assert len(d["kich_ban"]) == 9

    thu_muc_thu = GOC / "frontend" / "dieu-khien-tay" / "du-lieu-thu"
    assert thu_muc_thu.exists()
    for ten in d["kich_ban"]:
        tep = thu_muc_thu / f"{ten}.json"
        assert tep.exists(), f"Thiếu tệp du-lieu-thu/{ten}.json"


def test_06_nhan_dien_o_luong_rieng_cat_4_3():
    """Nhận diện chạy trong Web Worker (không chặn vẽ 3D), cắt khung về 4:3, ngưỡng tin cậy mặc định 0,5."""
    td = (GOC / "frontend" / "dieu-khien-tay" / "theo-doi-tay.js").read_text(encoding="utf-8")
    wk = (GOC / "frontend" / "dieu-khien-tay" / "nhan-dien-worker.js").read_text(encoding="utf-8")
    assert "new Worker(" in td and "type: 'module'" in td and "nhan-dien-worker.js" in td
    assert "tinhVungCat" in td and "doiVeKhungDay" in td
    assert "minHandDetectionConfidence: 0.5" in td and "0.6" not in td.split("const TUY_CHON")[1].split(";")[0]
    assert "forVisionTasks(duongWasm, true)" in wk and "detectForVideo" in wk
    r = client.get("/static/dieu-khien-tay/nhan-dien-worker.js")
    assert r.status_code == 200 and "javascript" in r.headers["content-type"]
