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

// ---- Trục có đáng tin không (26/11/2026) ----
// Đo thật: mất nguồn module thì cả 2 trục kẹt 142 mV (0 V), mất GND thì kẹt 3150 mV, dây VRx hở thì chân trôi 150-300 mV.
// Kẹt ở mép trông y như gạt hết cỡ, nên chỉ phân biệt được bằng lịch sử: trục chỉ được tin sau khi đã thấy cần nằm
// gần tâm (lò xo luôn kéo cần về giữa) 0,5 giây; mất tin khi điện áp nhảy kiểu dây hở, cả 2 trục cùng vọt > 1 V trong
// một lượt đọc (mất nguồn), hoặc 20 giây liền không về gần tâm. Trục không tin: ra 0, báo game điện áp tâm 1650 mV.
const float TAM_THAP = 900, TAM_CAO = 2400, MV_TAM_GIA = 1650;
bool tot[2] = {false, false};
uint16_t demTam[2] = {0, 0};
uint32_t demXa[2] = {0, 0};
float nhieu[2] = {0, 0};
float mvRa[2] = {MV_TAM_GIA, MV_TAM_GIA};  // điện áp báo game (mV thật khi trục tốt)
uint8_t doiTot = 0;                        // bit i: trục i vừa đổi trạng thái (tác vụ cảm biến in thông báo rồi xoá)

bool ganTam(float v) { return v > TAM_THAP && v < TAM_CAO; }

void khoiTao() {
  analogReadResolution(12);
  analogSetPinAttenuation(CHAN_JOY_X, ADC_11db);  // đo được tới khoảng 3,1 V
  analogSetPinAttenuation(CHAN_JOY_Y, ADC_11db);
  if (CHAN_JOY_NUT >= 0) pinMode(CHAN_JOY_NUT, INPUT_PULLUP);
}

bool dangNhanLucBat() { return CHAN_JOY_NUT >= 0 && digitalRead(CHAN_JOY_NUT) == LOW; }

void xetTot() {
  float d[2] = {fabsf(mv[0] - mvCu[0]), fabsf(mv[1] - mvCu[1])};
  // mất nguồn / GND: cả 2 trục cùng vọt từ gần tâm ra sát mép trong 10 ms (ngón tay gạt ra góc mất 30-60 ms;
  // cần bật về tâm khi buông thì nhanh nhưng là đi VỀ tâm, không tính)
  bool matNguon = true;
  for (int i = 0; i < 2; i++)
    matNguon = matNguon && d[i] > 1000 && fabsf(mvCu[i] - MV_TAM_GIA) < 500 && (mv[i] < 300 || mv[i] > 3000);
  for (int i = 0; i < 2; i++) {
    nhieu[i] = nhieu[i] * 0.98f + (d[i] > 150 ? 0.02f : 0);  // tỉ lệ lượt nhảy lớn trong khoảng 0,5 s gần nhất
    bool gan = ganTam(mv[i]) && d[i] < 30;
    demTam[i] = gan ? min(demTam[i] + 1, 1000) : 0;
    demXa[i] = ganTam(mv[i]) ? 0 : demXa[i] + 1;
    bool cu = tot[i];
    if (tot[i] && (matNguon || nhieu[i] > 0.3f || demXa[i] > 2000)) tot[i] = false;
    else if (!tot[i] && demTam[i] >= 50 && nhieu[i] < 0.1f) tot[i] = true;
    if (tot[i] != cu) doiTot |= 1 << i;
    mvRa[i] = tot[i] ? mv[i] : MV_TAM_GIA;
  }
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
  xetTot();
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

  bool bamNay = CHAN_JOY_NUT >= 0 && digitalRead(CHAN_JOY_NUT) == LOW;  // khử dội: 2 mẫu liên tiếp (20 ms)
  if (bamNay == nut) demNut = 0;
  else if (++demNut >= 2) {
    nut = bamNay;
    demNut = 0;
  }
}

void layRa(float ra[2]) {
  float x = tot[0] ? loc[0] : 0, y = tot[1] ? loc[1] : 0;  // trục không tin: 0 (trước khi đổi / đảo trục)
  if (huong.doiXY) { float t = x; x = y; y = t; }
  if (huong.daoX) x = -x;
  if (huong.daoY) y = -y;
  ra[0] = x;
  ra[1] = y;
}

}  // namespace joy
