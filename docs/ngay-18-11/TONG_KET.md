# Tổng kết ngày 18/11/2026

Nhánh tích hợp `n18/tich-hop` = `main` + `n18/antigravity` (G1–G4) + `n18/co-che` (C1–C4) + các sửa của C5.
**Chưa merge vào `main`, chưa push, chưa đưa lên web.** App Mac: `~/AppBuild/VERA_mac/VERA Lab.app` (247 MB, ngoài kho git).

## 1. Chín việc trong ngày

| Mã | Việc | Kết quả | Số đo chính | Ảnh / báo cáo |
|---|---|---|---|---|
| N1 | Mã hóa dữ liệu 3D cơ chế / RAG AI tại máy | **Xong** | 8 gói `.verapack` (AES-256-CBC + HMAC-SHA256, 2,3–4,3 KB/gói). Dữ liệu gốc chỉ ở `~/Developer/VERA_du_lieu_rieng` (chưa từng lên GitHub). RAG 33 đoạn có vector. Mô hình 3B tại máy: 9,1 s/câu, 4/5 đúng + 1/5 đúng một phần | bao-cao-C1, C2 |
| N2 | Bài demo (cơ sở bài) | **Xong** | 8 bài qua `co_che.kiem_tra` ngay lần đầu, dựng xong trong ~1 s. App thật liệt kê đủ 8 bài từ gói mã hóa | `anh/c5/app_that_co_che_3d.png` |
| N3 | Thông số phần cứng cho 3D cơ chế và giải chuỗi | **Xong** | App: 5 mức đồ họa (Tự động/Thấp/Vừa/Cao/Siêu nét). Máy chủ `/co-che/may` chọn mô hình theo RAM (M2 8 GB → `vera-hoa-3b`, mức thấp). Bảng giải chuỗi trong app | bao-cao-G1, C2, C3 |
| N4 | Giải phẫu người trong app | **Xong** (Mac/Windows) | Mở lần đầu 0,19 s; 5 lần mở/đóng không rò (−1 MB); 8 hệ cơ quan, 3 mặt cắt, 15 cơ quan; G4 40/40 | `anh/g4/`, bao-cao-G4 |
| N5 | Thực vật 3D dạy học thời gian thực | **Xong** | 3 lát cắt, 7 thanh trượt, mô phỏng C# 12/12 test; G3 10/10 (sau sửa C5) | `anh/g3/`, `anh/c5/sau_c5_g3_*` |
| N6 | Bàn tay người và mô hình người 3D | **Xong** | Ngón 3 đốt, găng nitrile, IK không xuyên vật (sai lệch ≤ 0,51 mm); người 3 mức LOD trong phòng học chung; G2 16/16 | `anh/g2/`, bao-cao-G2 |
| N7 | Tư duy vẽ hình | **Xong** | Bộ dựng 3D viết lại tổng quát: trước chạy 3/8 bài, nay 8/8; có nhánh sai + màn chắn | bao-cao-C1 |
| N8 | Giao diện ngoài app và độ sắc nét | **Xong** | Hết xám đục: nền thẻ trong app thật đo được **255/255/255** (trước 208). 5 thẻ mô-đun có ảnh thật, cả 5 mở được. UI 40/40. Tên cửa sổ "VERA Lab", biểu tượng mới | `anh/c5/app_that_bang_5_the.png`, `app_that_man_chinh.png` |
| N9 | Hết lag khi xuất app | **Xong trên Mac** | App thật, mức Tự động (= Cao), màn 3840×2160: **p50 16,67 ms, p95 17,60 ms, 1,33 khung/phút > 50 ms, GC 2,12 KB/khung** (G1 TRƯỚC: p95 33,3 ms, 2,7–3,3 khung/phút, 23 KB/khung) | bảng mục 2 |

## 2. Đo khung hình trên app Mac đã xuất (DoKhungHinh, 90 giây, mức Tự động)

| Lần đo | Màn hình | FPS TB | p50 | p95 | p99 | Max | > 33 ms | > 50 ms (khung/phút) | GC |
|---|---|---|---|---|---|---|---|---|---|
| G1 TRƯỚC (lần 2) | 1600×900 | 51,3 | 16,67 | 33,30 | 33,34 | 1533 | 57 | 4 (2,67) | 23,35 KB/khung |
| G1 SAU Tự động (lần 2) | 3840×2486 | 59,7 | 16,67 | 17,17 | 17,66 | 100 | 11 | 2 (1,33) | 2,21 KB/khung |
| **C5 lần 1 (mở đầu sau khi xuất)** | 3840×2160 | 55,7 | 16,67 | 25,14 | 41,68 | 187 | 134 | 22 (14,67) | 2,16 KB/khung |
| **C5 lần 2** | 3840×2160 | **58,7** | **16,67** | **17,60** | 33,24 | 117 | 38 | **2 (1,33)** | **2,12 KB/khung** |

- Lần 1 chậm ở chặng 4–6 (thang bộ, tầng 2, kính hiển vi). Đây là lần mở đầu tiên ngay sau khi xuất app: bộ đệm shader Metal và đĩa còn nguội, máy còn dùng 2,5/4 GB swap sau 6 lần chạy Unity. Lần 2 cùng bản app, cùng máy, khớp số G1.
- **Việc mai:** làm nóng shader ở màn chờ cho các khu tầng 2 (GraphicsStateCollection), để lần mở đầu tiên cũng mượt. Hiện chỉ lần đầu bị giật.

## 3. Kiểm tra cuối ngày (trên `n18/tich-hop`)

| Bộ kiểm thử | Kết quả |
|---|---|
| Python `unittest discover -s tests` | 419 test. 77 FAIL + 100 ERROR, tất cả trong 5 tệp máy chủ hỏng sẵn (FastAPI 0.162), **giống mốc gốc**. 38 test mới đều đạt |
| Unity biên dịch | 0 `error CS` |
| KhktWave6Verify | 67/67 (một lần chạy giữa chừng hỏng 2 mục 7.2 pha loãng, chạy lại đạt 67/67: lỗi chập chờn, mai xem) |
| KhktN18Verify | G2 16/16, G3 10/10 (lần chạy riêng sau sửa) / 26/26 (lần chạy chung), G4 40/40 |
| KhktCoCheVerify | 46/46 (sau khi tạo gói thử `mau_kiem_thu.bin`) |
| KhktUiVerify | 40/40. Mới: bấm mở và đóng cả 3 mô-đun từ thẻ |
| Quét Windows | Không có đường dẫn cứng `/Users`, `/tmp`, `~`; không có API riêng Mac ngoài `#if` |
| Quét lộ dữ liệu | `test_khong_lo_du_lieu` 4/4. `git diff main` không có `.key/.env/.db/.verapack/khung_mau`. `khoa.bytes` và `StreamingAssets/CoChe/` bị `.gitignore` |
| App thật | Mở cơ chế (phím M) → 8 bài giải từ gói, hình 3D hiện; "Máy chủ: đã kết nối http://127.0.0.1:8010" |

## 4. C5 đã sửa

- **Gốc lỗi giao diện xám:** `HardwareProfile.ApplyCameras` bỏ qua camera chỉ vẽ lớp UI và camera có `CameraKhongHauKy` (tệp mới). Màn chính và Cơ chế gắn dấu này một lần, thay cho bản vá tắt hậu kỳ mỗi khung. Camera 3D của Thực vật và Giải phẫu vẫn có hậu kỳ; chữ của hai mô-đun nằm trên canvas Overlay nên không bị ảnh hưởng.
- Thẻ "Thực vật 3D" và "Giải phẫu" tìm được lớp cầu nối và mở được thật. Ảnh thu nhỏ cắt từ ảnh nghiệm thu G3/G4.
- `ThucVatMoDun`: chữ số liệu cập nhật ~5 lần/giây thay vì mỗi khung (trước đó nối chuỗi dài mỗi khung).
- `CoCheMoDun.Update`: bỏ `new Vector3[4]` mỗi khung.
- Rò bộ nhớ: `HumanoidAvatar` không giải phóng 8 vật liệu khi người khác rời phòng học chung; `TrinhPhat3D` không giải phóng 2 vật liệu sao chép. Đã thêm giải phóng.

## 5. Còn vướng / ghi lại

- **Vật liệu `.mat` tự đổi sau mỗi lần chạy kiểm thử Unity** (27–40 tệp: mất `_EMISSION`, `_SrcBlend` 5→1). Không có mã chạy trong app nào tắt `_EMISSION`. Nghi do Unity/URP kiểm lại vật liệu khi nạp rồi ghi ra đĩa lúc lưu (`KhktUiVerify.DatTenVaBieuTuong` gọi `AssetDatabase.SaveAssets()`; nhưng `KhktCoCheVerify` không gọi mà vẫn đổi). Cả hai lần C5 đều lưu bản vá vào `~/Developer/Antigravity-luu-ban-cu/mat_va_anh_do_kiem_thu_C5_18-11.patch` rồi hoàn tác. **Mai:** tìm đúng chỗ (thử chạy một bộ kiểm thử rồi `git status`, lần lượt từng bộ).
- Bộ kiểm thử N18 chụp đè ảnh nghiệm thu `anh/g2..g4`. Đã hoàn tác; ảnh mới của C5 ở `anh/c5/`. Nên cho bộ chụp ghi vào thư mục riêng của lần chạy.
- Chụp app thật: phiên C5 không có công cụ bấm chuột (System Events không gửi được cú bấm vào Unity), chỉ gửi được phím. Đã chụp màn chính, bảng 5 thẻ và Cơ chế 3D (phím M). Thực vật 3D và Giải phẫu chưa chụp trong app thật (không có phím tắt), nhưng đã kiểm trong Editor (UI 40/40, G3, G4).
- Hai sửa rò bộ nhớ (`HumanoidAvatar`, `TrinhPhat3D`; đã kiểm bằng G2 16/16 và Wave6 67/67) làm **sau** khi xuất app, nên chưa có trong app ở `~/AppBuild/VERA_mac`. Xuất lại lần sau sẽ có.

## 6. Windows

Máy này chưa cài Windows Build Support (`PlaybackEngines` chỉ có Mac và iOS), nên chưa xuất bản Windows. Việc cần thử trên máy Windows:
1. Cài module Windows trong Unity Hub, chạy `Khkt.EditorTools.MacBuild.BuildWindows` → `~/AppBuild/VERA_win/VERA Lab.exe`.
2. Chạy `VERA Lab.exe -vera-do`, so p95 và số khung > 50 ms với bảng mục 2 (máy Windows yếu thì mức Tự động nên chọn Vừa/Thấp).
3. Mở Cơ chế 3D (M): gói `.verapack` giải bằng `Aes.Create()` của .NET, phải ra đủ 8 bài.
4. Máy chủ cơ chế: `python -m server.ai.co_che.may_chu` (cổng 8010). Trên Windows ollama dùng GPU NVIDIA/CPU, `/co-che/may` phải chọn mô hình hợp RAM.
5. Giải phẫu: trên Windows phải mở trong app (không mở trình duyệt). Thực vật 3D: shader `Khkt/Bio/LaCay*` chạy D3D11.
6. Chữ tiếng Việt và độ nét trên màn 125%/150% (DPI Windows), thẻ mô-đun không tràn chữ.

## 7. iPad (chưa làm hôm nay, chỉ ghi chỗ có thể vướng)

- `CameraQuyDao` (Thực vật, Giải phẫu) và `TrinhPhat3D` (Cơ chế) chỉ đọc chuột và bàn phím (`Mouse.current`, `Keyboard.current`). Cần thêm kéo một ngón để xoay, chụm hai ngón để phóng to, chạm để chọn. Phím Esc/M/←/→ cần nút trên màn.
- Giải phẫu trên iPad vẫn dùng WebView (`#if UNITY_IOS`), khác Mac/Windows. Nếu muốn trong app như Mac, cần kiểm bộ nhớ (lưới BodyParts3D) trên iPad.
- Gói `.verapack`: `Aes.Create()` chạy được với IL2CPP, nhưng cần kiểm `link.xml` giữ các lớp mã hóa khi strip.
- Máy chủ cơ chế: iPad gọi máy Mac trong mạng LAN (`VERA_CO_CHE_HOST=0.0.0.0`), phải nhập IP ở "Cài đặt máy chủ".
- Nút cao ≥ 44 pt đã kiểm cho bảng điều khiển; giao diện hai mô-đun mới (thanh trượt) chưa kiểm với ngón tay.

## 8. Việc mai làm tiếp

1. Tìm chỗ làm `.mat` tự đổi khi chạy kiểm thử (mục 5).
2. Làm nóng shader tầng 2 để lần mở đầu tiên không giật.
3. Xuất lại app Mac (có 2 sửa rò bộ nhớ), chụp Thực vật 3D và Giải phẫu trong app thật.
4. Cài Windows Build Support và thử 6 mục ở mục 6.
5. Gắn router cơ chế vào máy chủ chính (cổng 8000) sau khi xử lý middleware đăng nhập (bao-cao-C2).
6. Khi người dùng đồng ý: merge `n18/tich-hop` vào `main`.
