// cau_hinh.h - các thông số cần sửa trước khi nạp. Chỉ sửa tệp này, không cần sửa tệp khác.
#pragma once

// ===================== Wi-Fi =====================
// Wi-Fi 2,4 GHz (ESP32 không bắt được 5 GHz). Để nguyên "TEN_WIFI" thì tay cầm chỉ chạy qua cáp USB.
#define WIFI_TEN       "TEN_WIFI"
#define WIFI_MAT_KHAU  "MAT_KHAU"

// IP máy tính chạy game, ví dụ "192.168.1.23". Để "" thì tay cầm phát quảng bá cho tới khi game trả lời.
// Khi đi thi nên điền: gửi thẳng tới một máy ít trễ và ít mất gói hơn phát quảng bá.
#define IP_MAY_TINH    ""

#define CONG_PC        4210   // cổng máy tính nghe gói tay cầm (giống các bản trước)
#define CONG_TAY_CAM   4211   // cổng tay cầm nghe lệnh

// Kiểu gói gửi đi:
//   0 = chỉ gói v3 37 byte (đủ thông tin: hướng tính sẵn, mức cảm ứng);
//   1 = chỉ gói cũ 29 byte (game hiện tại đã đọc được);
//   2 = gửi cả hai (mặc định, để game hiện tại chạy ngay và công cụ xem đọc được v3).
#define KIEU_GOI       2
#define TAN_SO_GOI_HZ  100

// ===================== Cổng USB (Serial) =====================
#define TOC_DO_BAUD    921600   // 100 gói/s × 105 byte gần đầy 115200 baud: nâng lên 921600
// Mặc định khi bật: 0 = chỉ in thông báo; 1 = dòng "Q,w,x,y,z,n" 50 Hz cho tools/xem_bmi160.py;
// 2 = dòng số liệu dễ đọc 10 Hz (xem mức cảm ứng, joystick). Đổi lúc chạy bằng lệnh s0, s1, s2.
#define SERIAL_XUAT    2

// ===================== Chân (docs/tay-cam-tich-hop, trang 2) =====================
#define CHAN_JOY_X     34   // ADC1, đọc được khi bật Wi-Fi
#define CHAN_JOY_Y     35   // ADC1
#define CHAN_JOY_NUT   27   // nhấn cần, nối GND khi nhấn
#define CHAN_SDA       25   // 18/11/2026: BMI160 chuyển sang GPIO25 (trước là 21)
#define CHAN_SCL       26   // 18/11/2026: BMI160 chuyển sang GPIO26 (trước là 22)
#define CHAN_INT1      -1   // không nối dây INT1: lấy mẫu theo đồng hồ 200 Hz. Nối INT1 vào GPIO19 thì đổi lại 19
#define CHAN_CHAM_A    13   // T4, nút cảm ứng ngón cái
#define CHAN_CHAM_B    14   // T6, nút cảm ứng ngón trỏ (cầm vật)
#define CHAN_LED       2    // LED xanh có sẵn trên board

// ===================== Joystick =====================
#define JOY_DAO_X      0    // 1 = đảo chiều trục X
#define JOY_DAO_Y      0    // 1 = đảo chiều trục Y (đẩy lên mà nhân vật đi lùi)
#define JOY_DOI_XY     0    // 1 = hoán đổi X và Y (mạch joystick gắn xoay ngang)

// ===================== Trục BMI160 =====================
// Hệ trục tay cầm: X hướng ra mũi tay cầm, Y sang trái, Z lên trên (docs/02).
// Mỗi trục tay cầm lấy từ trục nào của chip (0 = x, 1 = y, 2 = z) và dấu (+1 hoặc -1).
// Kiểm tra: đặt tay cầm nằm ngang trên bàn thì gia tốc Z ≈ +1 g; chúc mũi xuống thì X tiến về -1 g.
#define IMU_X_TU   0
#define IMU_X_DAU  -1   // 18/11/2026: BMI160 gắn quay ngược 180° (lên/xuống bị ngược, trái/phải đúng)
#define IMU_Y_TU   1
#define IMU_Y_DAU  -1
#define IMU_Z_TU   2
#define IMU_Z_DAU  1

// ===================== Bộ lọc hướng =====================
// 0 = Madgwick (mặc định, tốt nhất trong mô phỏng); 1 = Mahony để thử so sánh trên mạch thật.
#define BO_LOC_MAHONY  0
#define MAHONY_KP      1.0f
#define MAHONY_KI      0.0f   // > 0: tự ước lượng độ lệch từ gia tốc kế (mô phỏng: có hại khi tay chuyển động nhiều)
// Thời gian cân con quay lúc bật và lệnh c (ms). 2 giây: độ lệch đo chính xác gấp 1,4 lần so với 1 giây.
#define THOI_GIAN_CAN_MS 2000

// ===================== Nút cảm ứng =====================
// Ngưỡng tính theo phần của "mức chạm thật" đã học bằng lệnh a, b (1.0 = chạm chắc).
#define CHAM_NGUONG_BAM      0.50f
#define CHAM_NGUONG_NHA      0.35f
#define CHAM_NGUONG_LO_LUNG  0.15f
// Chưa hiệu chuẩn thì coi chạm thật làm số đo tụt 10% so với mức nền. Nên chạy lệnh k một lần.
#define CHAM_TY_LE_MAC_DINH  0.10f
// Thời gian đo mỗi lần (ms): dài hơn thì ít nhiễu hơn nhưng chậm hơn. 2 ms cho 2 nút ≈ 4 ms một lượt.
#define CHAM_THOI_GIAN_DO_MS 2.0f
// Mức nền đi theo nhiệt độ, độ ẩm rất chậm (giây), chỉ khi không chạm.
#define CHAM_TAU_NEN_S       20.0f
