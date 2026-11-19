// joystick.h - đọc KY-023 bằng ADC1 (GPIO34, 35): trung bình 16 mẫu, đổi ra mV theo hiệu chuẩn eFuse,
// trừ tâm (học lúc bật và tự học lại khi buông cần), chuẩn hoá theo biên đã học, lọc nhẹ.
// Vùng chết và đường cong để Unity làm (chỉnh trong cài đặt game, không cần nạp lại).
#pragma once
#include "chung.h"

namespace joy {

float tam[2] = {1650, 1650};  // mV lúc cần nghỉ
float loc[2] = {0, 0};        // -1..1 sau lọc
float mv[2] = {1650, 1650};
float mvCu[2] = {1650, 1650};
uint32_t demNghi = 0;

// Hướng gắn cần gạt (lệnh h học được, lưu flash khoá "joyh"); mặc định lấy từ cau_hinh.h
struct Huong {
  uint8_t doiXY = JOY_DOI_XY, daoX = JOY_DAO_X, daoY = JOY_DAO_Y;
};
Huong huong;

void napHuong() {  // gọi sau napCaiDat() (boNho đã mở)
  Huong t;
  if (boNho.getBytesLength("joyh") == sizeof(Huong)) {
    boNho.getBytes("joyh", &t, sizeof t);
    huong = t;
  }
}

void luuHuong() { boNho.putBytes("joyh", &huong, sizeof huong); }

void xoaHuong() {
  huong = Huong();
  boNho.remove("joyh");
}
bool nut = false;
uint8_t demNut = 0;

void khoiTao() {
  analogReadResolution(12);
  analogSetPinAttenuation(CHAN_JOY_X, ADC_11db);  // đo được tới khoảng 3,1 V
  analogSetPinAttenuation(CHAN_JOY_Y, ADC_11db);
  pinMode(CHAN_JOY_NUT, INPUT_PULLUP);
}

float docMv(int chan) {
  uint32_t s = 0;
  for (int i = 0; i < 16; i++) s += analogReadMilliVolts(chan);
  return s / 16.0f;
}

void docThuan(float ra[2]) {
  ra[0] = docMv(CHAN_JOY_X);
  ra[1] = docMv(CHAN_JOY_Y);
}

// Gọi 100 lần/giây
void capNhat() {
  docThuan(mv);
  for (int i = 0; i < 2; i++) {
    float d = mv[i] - tam[i];
    // đẩy quá biên đã học thì nới biên ra (chưa lưu; lưu bằng lệnh j)
    if (d > cd.joyDuong[i]) cd.joyDuong[i] = d;
    if (-d > cd.joyAm[i]) cd.joyAm[i] = -d;
    float n = d >= 0 ? d / cd.joyDuong[i] : d / cd.joyAm[i];
    n = constrain(n, -1.0f, 1.0f);
    loc[i] += 0.5f * (n - loc[i]);  // EMA α = 0,5: trễ thêm dưới 5 ms
  }
  // Cần nằm gần tâm và không nhúc nhích 1 giây: kéo tâm theo từ từ (tâm mạch rẻ hay xê dịch theo nhiệt).
  // Chỉ xét trong 5% quanh tâm, nhỏ hơn vùng chết 8% của game nên người dùng không thấy gì.
  bool nghi = fabsf(loc[0]) < 0.05f && fabsf(loc[1]) < 0.05f && fabsf(mv[0] - mvCu[0]) < 15 && fabsf(mv[1] - mvCu[1]) < 15;
  demNghi = nghi ? demNghi + 1 : 0;
  if (demNghi > 100) {
    tam[0] += 0.02f * (mv[0] - tam[0]);
    tam[1] += 0.02f * (mv[1] - tam[1]);
  }
  mvCu[0] = mv[0];
  mvCu[1] = mv[1];

  bool bamNay = digitalRead(CHAN_JOY_NUT) == LOW;  // khử dội: 2 mẫu liên tiếp (20 ms)
  if (bamNay == nut) demNut = 0;
  else if (++demNut >= 2) {
    nut = bamNay;
    demNut = 0;
  }
}

void layRa(float ra[2]) {
  float x = loc[0], y = loc[1];
  if (huong.doiXY) { float t = x; x = y; y = t; }
  if (huong.daoX) x = -x;
  if (huong.daoY) y = -y;
  ra[0] = x;
  ra[1] = y;
}

}  // namespace joy
