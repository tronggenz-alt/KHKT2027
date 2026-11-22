// mang.h - tác vụ mạng (nhân 0): Wi-Fi không ngủ, gửi gói 100 Hz qua UDP, nghe lệnh, gửi thông báo.
#pragma once
#include <WiFi.h>
#include <WiFiUdp.h>
#include "chung.h"

namespace mang {

WiFiUDP udp;
IPAddress ipPc;
bool coIpPc = false;
uint32_t lanNgheCuoi = 0;
IPAddress ipCoDinh;
bool coIpCoDinh = false;
uint8_t seq = 0;

uint8_t crc8(const uint8_t *d, uint8_t n) {
  uint8_t c = 0;
  for (uint8_t i = 0; i < n; i++) {
    c ^= d[i];
    for (uint8_t b = 0; b < 8; b++) c = (c & 0x80) ? (c << 1) ^ 0x07 : (c << 1);
  }
  return c;
}

void ghi16(uint8_t *p, int32_t v) {
  if (v > 32767) v = 32767;
  if (v < -32767) v = -32767;
  p[0] = v & 0xFF;
  p[1] = (v >> 8) & 0xFF;
}

void ghiU16(uint8_t *p, uint16_t v) {
  p[0] = v & 0xFF;
  p[1] = v >> 8;
}

void ghi32(uint8_t *p, uint32_t v) {
  p[0] = v & 0xFF; p[1] = (v >> 8) & 0xFF; p[2] = (v >> 16) & 0xFF; p[3] = (v >> 24) & 0xFF;
}

// Gói v3, 37 byte (docs/tay-cam-tich-hop trang 7)
size_t dongGoiV3(const TrangThai &t, uint8_t *g) {
  g[0] = 0xA5;
  g[1] = 0x5C;
  g[2] = 3;
  g[3] = seq;
  ghi32(g + 4, t.tMs);
  for (int i = 0; i < 4; i++) ghi16(g + 8 + 2 * i, lroundf(t.q[i] * 16384.0f));
  for (int i = 0; i < 3; i++) ghi16(g + 16 + 2 * i, lroundf(t.gyro[i] * 32.8f));
  for (int i = 0; i < 3; i++) ghi16(g + 22 + 2 * i, lroundf(t.acc[i] * 4096.0f));
  ghi16(g + 28, lroundf(t.joy[0] * 32767.0f));
  ghi16(g + 30, lroundf(t.joy[1] * 32767.0f));
  g[32] = t.mucA;
  g[33] = t.mucB;
  g[34] = t.bit;
  g[35] = (uint8_t)constrain(lroundf(t.nhietDo + 40.0f), 0L, 255L);
  g[36] = crc8(g + 2, 34);
  return 37;
}

// Gói cũ 29 byte (docs/02) để game hiện tại dùng được ngay: game tự tính hướng từ gia tốc và con quay.
//   cò (cầm/thả) = chạm B, nhấn joystick 1 = chạm A, nhấn joystick 2 = nhấn cần, căn lại = vừa giữ A + B.
size_t dongGoiCu(const TrangThai &t, uint8_t *g) {
  g[0] = 0xA5;
  g[1] = 0x5A;
  g[2] = seq;
  ghi32(g + 3, t.tMs);
  for (int i = 0; i < 3; i++) ghi16(g + 7 + 2 * i, lroundf(t.acc[i] * 16384.0f));   // ±2 g
  for (int i = 0; i < 3; i++) ghi16(g + 13 + 2 * i, lroundf(t.gyro[i] * 65.5f));   // ±500 °/s
  ghiU16(g + 19, 512);
  ghiU16(g + 21, 512);
  ghiU16(g + 23, (uint16_t)constrain(lroundf(512 + t.joy[0] * 511), 0L, 1023L));
  ghiU16(g + 25, (uint16_t)constrain(lroundf(512 + t.joy[1] * 511), 0L, 1023L));
  uint8_t nut = 1 << 6;  // tay cầm 1 cần: game dùng 2 chế độ ĐI / TAY
  if (t.bit & BIT_CHAM_B) nut |= 1 << 0;
  if (t.bit & BIT_VUA_CAN_LAI) nut |= 1 << 1;
  if (t.bit & BIT_CHAM_A) nut |= 1 << 3;
  if (t.bit & BIT_NHAN_CAN) nut |= 1 << 4;
  if (t.bit & BIT_LOI_IMU) nut |= 1 << 7;
  g[27] = nut;
  g[28] = crc8(g + 2, 26);
  return 29;
}

void gui(const IPAddress &ip, const uint8_t *d, size_t n) {
  udp.beginPacket(ip, CONG_PC);
  udp.write(d, n);
  udp.endPacket();
}

void xuLyGoiDen() {
  int len;
  while ((len = udp.parsePacket()) > 0) {
    char buf[160];
    int n = udp.read(buf, sizeof buf - 1);
    if (n <= 0) continue;
    buf[n] = 0;
    ipPc = udp.remoteIP();
    coIpPc = true;
    lanNgheCuoi = millis();
    char *luu = nullptr;
    for (char *dong = strtok_r(buf, "\r\n", &luu); dong; dong = strtok_r(nullptr, "\r\n", &luu)) {
      char c = dong[0];
      if (c == 'H' || c == 'R' || c == 'P' || c == 'L') continue;  // nhịp tim; rung, đèn: tay cầm không có
      guiLenh(dong);
    }
  }
}

void tacVu(void *) {
  coIpCoDinh = strlen(IP_MAY_TINH) > 0 && ipCoDinh.fromString(IP_MAY_TINH);
  WiFi.mode(WIFI_STA);
  WiFi.setSleep(false);          // tắt tiết kiệm điện: không thì trễ, giật thất thường
  WiFi.setAutoReconnect(true);
  WiFi.begin(WIFI_TEN, WIFI_MAT_KHAU);
  uint32_t lanThuLai = millis();
  TickType_t nhip = xTaskGetTickCount();
  const TickType_t chuKy = pdMS_TO_TICKS(1000 / TAN_SO_GOI_HZ);

  for (;;) {
    vTaskDelayUntil(&nhip, chuKy);
    bool ok = WiFi.status() == WL_CONNECTED;
    if (ok && !wifiDaNoi) {
      WiFi.setSleep(false);
      WiFi.setTxPower(WIFI_POWER_19_5dBm);
      udp.begin(CONG_TAY_CAM);
      bao("WIFI da noi %s, IP tay cam %s, kenh %d, RSSI %d dBm", WIFI_TEN, WiFi.localIP().toString().c_str(),
          WiFi.channel(), WiFi.RSSI());
    } else if (!ok && wifiDaNoi) {
      udp.stop();
      coIpPc = false;
      Serial.println("#WIFI mat ket noi, dang noi lai");
    }
    wifiDaNoi = ok;
    if (!ok) {
      coMayTinh = false;
      Dong bo;
      while (xQueueReceive(hangBao, &bo, 0)) {}  // chưa có mạng: thông báo đã in ra USB rồi
      if (millis() - lanThuLai > 15000) {
        WiFi.disconnect();
        WiFi.begin(WIFI_TEN, WIFI_MAT_KHAU);
        lanThuLai = millis();
      }
      continue;
    }
    lanThuLai = millis();

    xuLyGoiDen();
    // 5 giây không nghe máy tính (game tắt, đổi máy): quên máy tính, quay lại phát quảng bá
    coMayTinh = coIpPc && millis() - lanNgheCuoi < 5000;
    IPAddress dich = coMayTinh ? ipPc : (coIpCoDinh ? ipCoDinh : WiFi.broadcastIP());

    TrangThai t = docTrangThai();
    uint8_t g[40];
    if (KIEU_GOI != 1) gui(dich, g, dongGoiV3(t, g));
    if (KIEU_GOI != 0) gui(dich, g, dongGoiCu(t, g));
    seq++;
    if (guiTho == 1) {
      GoiTho gt;
      while (xQueueReceive(hangTho, &gt, 0)) gui(dich, gt.b, THO_DO_DAI);
    }

    Dong d;
    for (int i = 0; i < 4 && xQueueReceive(hangBao, &d, 0); i++) {
      char b[104];
      int n = snprintf(b, sizeof b, "#%s\n", d.s);
      gui(dich, (const uint8_t *)b, n);
    }
  }
}

}  // namespace mang
