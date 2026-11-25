// imu.h - đọc BMI160 qua I2C: 200 Hz, ±1000 °/s, ±8 g, ngắt "có dữ liệu mới" ra INT1.
// Chống treo (giữ từ firmware/tay_cam_bmi160): timeout I2C, gỡ kẹt bus bằng 9 xung SCL, khởi tạo lại chip.
#pragma once
#include <Wire.h>
#include "chung.h"

namespace imu {

const uint8_t R_CHIP_ID = 0x00, R_PMU = 0x03, R_DATA = 0x0C, R_TEMP = 0x20;
const uint8_t R_ACC_CONF = 0x40, R_ACC_RANGE = 0x41, R_GYR_CONF = 0x42, R_GYR_RANGE = 0x43;
const uint8_t R_INT_EN1 = 0x51, R_INT_OUT = 0x53, R_INT_LATCH = 0x54, R_INT_MAP1 = 0x56, R_CMD = 0x7E;
const float ACC_LSB = 4096.0f;   // ±8 g: lắc mạnh không bị bão hoà
const float GYR_LSB = 32.8f;     // ±1000 °/s: vẩy cổ tay nhanh không bị bão hoà
const uint32_t TAN_SO = 200;

uint8_t diaChi = 0x69;
uint32_t soLanHoiPhuc = 0;
uint32_t soLanGiong = 0;
uint8_t thoCu[12] = {0};
bool loi = true;

bool docThanhGhi(uint8_t reg, uint8_t *buf, uint8_t len) {
  Wire.beginTransmission(diaChi);
  Wire.write(reg);
  if (Wire.endTransmission(false) != 0) return false;
  if (Wire.requestFrom((int)diaChi, (int)len) != len) return false;
  for (uint8_t i = 0; i < len; i++) buf[i] = Wire.read();
  return true;
}

bool ghiThanhGhi(uint8_t reg, uint8_t v) {
  Wire.beginTransmission(diaChi);
  Wire.write(reg);
  Wire.write(v);
  return Wire.endTransmission() == 0;
}

// Chip treo giữa chừng có thể giữ SDA ở mức thấp: tạo 9 xung SCL rồi một điều kiện STOP để nhả bus.
void goKetBus() {
  pinMode(CHAN_SDA, INPUT_PULLUP);
  pinMode(CHAN_SCL, OUTPUT);
  for (int i = 0; i < 9; i++) {
    digitalWrite(CHAN_SCL, LOW);
    delayMicroseconds(5);
    digitalWrite(CHAN_SCL, HIGH);
    delayMicroseconds(5);
  }
  pinMode(CHAN_SDA, OUTPUT);
  digitalWrite(CHAN_SDA, LOW);
  delayMicroseconds(5);
  digitalWrite(CHAN_SCL, HIGH);
  delayMicroseconds(5);
  digitalWrite(CHAN_SDA, HIGH);
  delayMicroseconds(5);
}

void batDauBus() {
  goKetBus();
  Wire.begin(CHAN_SDA, CHAN_SCL);
  Wire.setClock(400000);
  Wire.setTimeOut(20);
}

bool timDiaChi() {
  const uint8_t ungVien[2] = {0x69, 0x68};
  for (int i = 0; i < 2; i++) {
    diaChi = ungVien[i];
    uint8_t id;
    if (docThanhGhi(R_CHIP_ID, &id, 1) && id == 0xD1) return true;
  }
  return false;
}

bool khoiTao() {
  if (!timDiaChi()) return false;
  ghiThanhGhi(R_CMD, 0xB6);  // khởi động lại mềm
  delay(120);
  uint8_t id;
  if (!docThanhGhi(R_CHIP_ID, &id, 1) || id != 0xD1) return false;
  ghiThanhGhi(R_CMD, 0x11);  // gia tốc kế: chế độ thường
  delay(60);
  ghiThanhGhi(R_CMD, 0x15);  // con quay: chế độ thường
  delay(100);
  ghiThanhGhi(R_ACC_CONF, 0x29);   // 200 Hz, lọc thông thấp thường
  ghiThanhGhi(R_ACC_RANGE, 0x08);  // ±8 g
  ghiThanhGhi(R_GYR_CONF, 0x29);   // 200 Hz, lọc thường
  ghiThanhGhi(R_GYR_RANGE, 0x01);  // ±1000 °/s
  ghiThanhGhi(R_INT_OUT, 0x0A);    // INT1: bật ngõ ra, mức cao khi có ngắt, đẩy-kéo
  ghiThanhGhi(R_INT_LATCH, 0x00);  // không chốt
  ghiThanhGhi(R_INT_MAP1, 0x80);   // ngắt "có dữ liệu mới" -> INT1
  ghiThanhGhi(R_INT_EN1, 0x10);    // bật ngắt "có dữ liệu mới"
  delay(20);
  uint8_t st, ra, rg;
  if (!docThanhGhi(R_PMU, &st, 1) || (st & 0x3C) != 0x14) return false;  // gia tốc + con quay đang chạy
  if (!docThanhGhi(R_ACC_RANGE, &ra, 1) || ra != 0x08) return false;
  if (!docThanhGhi(R_GYR_RANGE, &rg, 1) || rg != 0x01) return false;
  return true;
}

// Mất khoảng 0,3 giây. Trả về true nếu cảm biến chạy lại được.
bool hoiPhuc() {
  soLanHoiPhuc++;
  Wire.end();
  delay(5);
  batDauBus();
  bool ok = false;
  for (int i = 0; i < 2 && !ok; i++) ok = khoiTao();
  soLanGiong = 0;
  loi = !ok;
  return ok;
}

inline float layTruc(const float s[3], int tu, int dau) { return dau * s[tu]; }

// a: g, g: °/s (chưa trừ độ lệch), theo hệ trục tay cầm (cau_hinh.h)
bool doc(float a[3], float g[3]) {
  uint8_t b[12];
  if (!docThanhGhi(R_DATA, b, 12)) return false;
  int16_t rgx = (int16_t)(b[1] << 8 | b[0]);
  int16_t rgy = (int16_t)(b[3] << 8 | b[2]);
  int16_t rgz = (int16_t)(b[5] << 8 | b[4]);
  int16_t rax = (int16_t)(b[7] << 8 | b[6]);
  int16_t ray = (int16_t)(b[9] << 8 | b[8]);
  int16_t raz = (int16_t)(b[11] << 8 | b[10]);
  if (rax == 0 && ray == 0 && raz == 0) return false;     // cảm biến vừa treo hoặc ngủ
  if (rax == -1 && ray == -1 && raz == -1) return false;  // bus kẹt
  if (memcmp(b, thoCu, 12) == 0) {                        // số không đổi quá 2 giây: treo
    if (++soLanGiong > TAN_SO * 2) return false;
  } else soLanGiong = 0;
  memcpy(thoCu, b, 12);

  float sa[3] = {rax / ACC_LSB, ray / ACC_LSB, raz / ACC_LSB};
  float sg[3] = {rgx / GYR_LSB, rgy / GYR_LSB, rgz / GYR_LSB};
  a[0] = layTruc(sa, IMU_X_TU, IMU_X_DAU);
  a[1] = layTruc(sa, IMU_Y_TU, IMU_Y_DAU);
  a[2] = layTruc(sa, IMU_Z_TU, IMU_Z_DAU);
  g[0] = layTruc(sg, IMU_X_TU, IMU_X_DAU);
  g[1] = layTruc(sg, IMU_Y_TU, IMU_Y_DAU);
  g[2] = layTruc(sg, IMU_Z_TU, IMU_Z_DAU);
  return true;
}

bool docNhietDo(float &t) {
  uint8_t b[2];
  if (!docThanhGhi(R_TEMP, b, 2)) return false;
  int16_t r = (int16_t)(b[1] << 8 | b[0]);
  if (r == (int16_t)0x8000) return false;  // chưa có số đo
  t = 23.0f + r / 512.0f;
  return true;
}

bool dangChay() {
  uint8_t st;
  return docThanhGhi(R_PMU, &st, 1) && (st & 0x3C) == 0x14;
}

}  // namespace imu
