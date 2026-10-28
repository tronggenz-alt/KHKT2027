# Bản mẫu Python – điều khiển mô hình 3D bằng cử chỉ tay

Đây là **bản mẫu đã chạy thật** trên máy người dùng (MacBook M2, camera FaceTime HD, ngày 26/10/2026).
Phần điều khiển tay của phòng Sinh học (`frontend/dieu-khien-tay/`) là bản chuyển 1:1 sang JavaScript của các file này.

| File | Nội dung |
|---|---|
| `xem_tay.py` | Mở camera của Mac (bỏ qua camera iPhone nối qua Camera Liên tục), nhận diện 21 điểm khớp bằng MediaPipe, tính tỉ lệ chụm, vẽ khung xương. |
| `xoay_3d.py` | `LocOneEuro`, `Tay`, `doc_tay`, `DieuKhien`: chụm kéo để xoay, lướt có quán tính, chụm lại để dừng, hai tay chụm kéo giãn để phóng to, xoè hai tay 1 giây để về góc đầu. Có người nộm 3D vẽ bằng numpy + OpenCV để thử. |
| `tao_du_lieu_vang.py` | Cho 9 kịch bản tay giả chạy qua `doc_tay` + `DieuKhien`, ghi đầu vào và kết quả từng khung vào `tests/js/du_lieu/dieu_khien_tay_vang.json`. |

Chạy thử (cần môi trường riêng `~/hand3d-env` có mediapipe + opencv; **không** cài vào `.venv` của dự án):

```bash
source ~/hand3d-env/bin/activate && python tools/tay_python/xoay_3d.py
```

Sinh lại dữ liệu vàng (chỉ khi bản mẫu Python thay đổi và người dùng đồng ý):

```bash
~/hand3d-env/bin/python tools/tay_python/tao_du_lieu_vang.py
```

Không import các file này từ backend. Chúng không nằm trong `requirements.txt` và không chạy trên máy chủ Render.
