// chung.h - dữ liệu dùng chung giữa các tác vụ, hàng lệnh, thông báo, cài đặt lưu trong NVS.
#pragma once
#include <Arduino.h>
#include <Preferences.h>
#include <stdarg.h>
#include "cau_hinh.h"

#define PHIEN_BAN "VERA-TAYCAM v3.1"  // 3.1 (26/11/2026): 4 miếng chạm hướng, chạm B sang GPIO27, bỏ trục joystick hỏng

// Bit trạng thái, byte 34 của gói v3
#define BIT_CHAM_A       0x01
#define BIT_CHAM_B       0x02
#define BIT_NHAN_CAN     0x04
#define BIT_DANG_YEN     0x08
#define BIT_VUA_CAN_LAI  0x10
#define BIT_HIEU_CHUAN   0x20
#define BIT_LOI_IMU      0x80

// Việc tác vụ cảm biến đang làm
enum CheDo : uint8_t { CD_CHAY, CD_CAN_YEN, CD_CAN_JOY, CD_CAN_A, CD_CAN_B, CD_CAN_XOAY, CD_CAN_NHIET, CD_CAN_HUONG_JOY };

// Ảnh chụp trạng thái tay cầm. Tác vụ cảm biến ghi 200 lần/giây, tác vụ mạng và loop() đọc.
struct TrangThai {
  uint32_t tMs = 0;
  float q[4] = {1, 0, 0, 0};    // hướng (w, x, y, z), hệ tay cầm -> hệ mặt đất
  float gyro[3] = {0, 0, 0};    // °/s, đã trừ độ lệch
  float acc[3] = {0, 0, 1};     // g
  float joy[2] = {0, 0};        // -1..1, đã hiệu chuẩn
  uint8_t mucA = 0, mucB = 0;   // mức cảm ứng 0..255
  uint8_t bit = 0;
  float nhietDo = 23;           // °C, cảm biến nhiệt trong BMI160
  uint32_t soLanHoiPhuc = 0;
  // chẩn đoán (in ra Serial)
  uint32_t thoA = 0, thoB = 0;
  float nenA = 0, nenB = 0;
  float joyMv[2] = {0, 0};      // điện áp báo game: mV thật, hoặc 1650 khi trục không tin, hoặc giả khi chạm miếng hướng
  float joyMvTho[2] = {0, 0};   // mV đo thật ở chân (chỉ để in chẩn đoán)
  uint8_t huongCham = 0;        // bit 0..3: miếng tiến, lùi, trái, phải đang chạm
};

portMUX_TYPE khoaTrangThai = portMUX_INITIALIZER_UNLOCKED;
TrangThai trangThaiChung;

void ghiTrangThai(const TrangThai &t) {
  portENTER_CRITICAL(&khoaTrangThai);
  trangThaiChung = t;
  portEXIT_CRITICAL(&khoaTrangThai);
}

TrangThai docTrangThai() {
  TrangThai t;
  portENTER_CRITICAL(&khoaTrangThai);
  t = trangThaiChung;
  portEXIT_CRITICAL(&khoaTrangThai);
  return t;
}

// ---------- Dữ liệu thô để ghi và phân tích (tools/ghi_tay_cam.py), lệnh r1 (Wi-Fi) / r2 (USB) / r0 (tắt) ----------
// Gói 105 byte, mã đồng bộ A5 5D, mỗi gói 4 mẫu IMU liên tiếp (200 Hz -> 50 gói/giây).
#define THO_SO_MAU 2   // 2 mẫu/gói: 100 gói/s (trước 4 mẫu, 50 gói/s); gói vẫn 105 byte, 2 ô cuối chép lại 2 mẫu mới
#define THO_DO_DAI 105
struct GoiTho {
  uint8_t b[THO_DO_DAI];
};
QueueHandle_t hangTho = nullptr;
volatile uint8_t guiTho = 0;  // 0 tắt, 1 qua Wi-Fi, 2 qua USB

// ---------- Hàng lệnh (Serial, UDP -> tác vụ cảm biến) và thông báo (-> Serial, UDP) ----------
struct Dong {
  char s[100];
};
QueueHandle_t hangLenh = nullptr, hangBao = nullptr;
volatile bool coMayTinh = false;   // tác vụ mạng nghe thấy máy tính trong 5 giây gần nhất
volatile bool wifiDaNoi = false;
volatile uint8_t serialXuat = SERIAL_XUAT;

void guiLenh(const char *s) {
  if (!hangLenh) return;
  Dong d;
  strlcpy(d.s, s, sizeof d.s);
  xQueueSend(hangLenh, &d, 0);
}

// Thông báo dạng "#..." (chỉ ký tự ASCII). In ra USB và gửi về game qua Wi-Fi.
void bao(const char *fmt, ...) {
  Dong d;
  va_list ap;
  va_start(ap, fmt);
  vsnprintf(d.s, sizeof d.s, fmt, ap);
  va_end(ap);
  Serial.printf("#%s\n", d.s);
  if (hangBao) xQueueSend(hangBao, &d, 0);
}

// ---------- Cài đặt lưu trong bộ nhớ trong (NVS), giữ qua các lần tắt nguồn ----------
struct CaiDat {
  uint16_t phienBan = 3;
  float joyAm[2] = {1250, 1250};      // nửa biên joystick phía âm (mV tính từ tâm)
  float joyDuong[2] = {1250, 1250};   // nửa biên phía dương
  float tyLeA = CHAM_TY_LE_MAC_DINH;  // mức tụt khi chạm thật / mức nền
  float tyLeB = CHAM_TY_LE_MAC_DINH;
  int8_t chieuA = 1, chieuB = 1;      // +1: số đo giảm khi chạm (ESP32), -1: tăng
  float bam = CHAM_NGUONG_BAM, nha = CHAM_NGUONG_NHA, loLung = CHAM_NGUONG_LO_LUNG;
  float lechQuay[3] = {0, 0, 0};      // độ lệch con quay lần cân gần nhất (dùng khi lúc bật bị rung)
  uint8_t coLechQuay = 0;
  float heSoNhiet[3] = {0, 0, 0};     // độ lệch con quay đổi bao nhiêu °/s mỗi °C (tự học)
  uint8_t coHeSoNhiet = 0;           // 1: tự học, 2: đo bằng lệnh n
  float tiLeZ = 1.0f;                 // tỉ lệ con quay trục Z (lệnh g)
};
CaiDat cd;
Preferences boNho;

void napCaiDat() {
  boNho.begin("vera", false);
  CaiDat t;
  if (boNho.getBytesLength("cd") == sizeof(CaiDat)) {
    boNho.getBytes("cd", &t, sizeof t);
    if (t.phienBan == 3) cd = t;
  }
}

void luuCaiDat() { boNho.putBytes("cd", &cd, sizeof cd); }
