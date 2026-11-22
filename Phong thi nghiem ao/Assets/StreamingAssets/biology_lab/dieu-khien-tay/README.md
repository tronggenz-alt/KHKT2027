# Phân Hệ Điều Khiển Cử Chỉ Tay 3D (Phòng Lab Sinh Học)

Bộ điều khiển mô hình 3D người giải phẫu bằng cử chỉ tay trực tiếp trong trình duyệt, chuyển 1:1 từ bản mẫu Python (`tools/tay_python/`) sang JavaScript:
- **Xoay mô hình:** Chụm ngón cái + ngón trỏ rồi kéo ngang hoặc dọc.
- **Lướt quán tính:** Kéo nhanh rồi thả ra, mô hình tiếp tục quay chậm dần. Chụm lại để dừng ngay.
- **Phóng to / Thu nhỏ:** Chụm cả hai tay rồi kéo giãn hoặc kéo lại gần.
- **Về góc ban đầu:** Xoè cả hai bàn tay giữ 1 giây (hoặc phím `r`).
- **Chọn cơ quan:** Chạm nhẹ (chụm rồi nhả trong 0,3 giây không kéo) để chọn cơ quan dưới con trỏ tương đương bấm chuột.

## Cấu trúc tệp
- `cu-chi.js`: Bản chuyển 1:1 của các lớp & hàm từ Python (`HANG_SO`, `LocOneEuro`, `Tay`, `tiLeChum`, `laXoe`, `docTay`, `DieuKhien`). Hỗ trợ sự kiện chạm nhẹ `cham_chon`.
- `ap-orbit.js`: Áp góc yaw, pitch, zoom lên camera `OrbitControls` của Three.js, mượt hóa qua hàm `1 - exp(-dt / doMuot)`.
- `chon-camera.js`: Tự động nhận diện và chọn camera của máy Mac (FaceTime HD / Webcam tích hợp), bỏ qua camera iPhone (Continuity Camera).
- `theo-doi-tay.js`: Điều phối MediaPipe Tasks Vision 1.1.0 offline, luồng video camera, vẽ khung xương tay HUD và con trỏ.
- `dieu-khien-tay.css`: Toàn bộ kiểu dáng giao diện bảng camera và con trỏ, chuẩn Awesomic, tiền tố `.dkt-`.
- `demo.html`: Trang demo độc lập dựng người nộm 3D procedural với đầy đủ khối cơ quan và bệ xoay.
- `du-lieu-thu/`: 9 tệp dữ liệu phát lại tách từ dữ liệu vàng để kiểm thử tự động.

## Kiểm thử
- Test dữ liệu vàng (khớp từng khung sai số <= 1e-6):
  `node --test tests/js/dieu_khien_tay_vang.test.mjs`
- Test tính năng mở rộng & chạm nhẹ:
  `node --test tests/js/dieu_khien_tay_mo_rong.test.mjs`
- Test áp OrbitControls:
  `node --test tests/js/dieu_khien_tay_ap_orbit.test.mjs`
- Test chọn camera Mac:
  `node --test tests/js/dieu_khien_tay_chon_camera.test.mjs`
- Test phân hệ Python:
  `.venv/bin/python -m pytest tests/test_dieu_khien_tay.py -v`
- Nghiệm thu trình duyệt Playwright K1..K10:
  `.venv/bin/python tools/kiem_tra_dieu_khien_tay.py`
