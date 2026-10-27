// Firmware tay cầm tối giản - Phòng thí nghiệm ảo KHKT
// Phần cứng: ESP32-CAM (AI-Thinker) + 1 mạch joystick 2 trục X-Y có nút nhấn. Không cần linh kiện nào khác.
// Arduino IDE: board "AI Thinker ESP32-CAM", gói "esp32 by Espressif Systems" 3.x. Chỉ dùng thư viện có sẵn.
// Đấu nối: docs/09-tay-cam-esp32-cam.md. Gói tin: docs/02-giao-thuc-tay-cam.md (giống hệt tay cầm Arduino Mega).
//
// Mỗi 10 ms (100 Hz): đọc 2 trục và nút của joystick, gửi một gói 29 byte qua cổng USB (Serial)
// và Bluetooth nếu bật trong cau_hinh.h. Game tự dò cổng COM nào đang gửi gói hợp lệ.
//   - Joystick ghi vào trục "cần đi" (j2x, j2y), nút ghi vào bit 4 (nhấn cần đi).
//   - Bit 6 báo "tay cầm 1 cần": game dùng kiểu điều khiển 2 chế độ ĐI / TAY.
//   - Bit 7 báo "không có MPU6050": game bỏ qua hướng tay cầm.
// Nhận lệnh chữ như bản Mega: I (thông tin), J (căn tâm joystick); H, R, P, L, C được nhận nhưng bỏ qua
// vì tay cầm không có motor, đèn hay cảm biến nghiêng.

#include "cau_hinh.h"
#if DUNG_BLUETOOTH
#include <BluetoothSerial.h>
BluetoothSerial SerialBT;
#endif

// ----- Chân ESP32-CAM -----
const int PIN_TRUC_X = 13;     // VRx của joystick (ADC2 kênh 4)
const int PIN_TRUC_Y = 14;     // VRy của joystick (ADC2 kênh 6)
const int PIN_NUT = 15;        // SW của joystick, nhấn là nối GND (dùng điện trở kéo lên bên trong)
const int PIN_LED_BOARD = 33;  // LED đỏ nhỏ mặt sau board, sáng ở mức THẤP
const int PIN_DEN_FLASH = 4;   // đèn flash trắng rất sáng trên board: luôn giữ tắt
// Không dùng: IO0 (chọn chế độ nạp), IO2 và IO12 (chân cấu hình lúc khởi động), IO16 (PSRAM), IO1/IO3 (cổng USB).

const uint8_t NUT_NHAN_CAN = 1 << 4;  // trùng bit "nhấn joystick 2" của giao thức
const uint8_t CO_MOT_CAN = 1 << 6;    // tay cầm chỉ có 1 cần
const uint8_t CO_KHONG_IMU = 1 << 7;  // không có MPU6050
const uint32_t HET_HAN_PC_MS = 2000;
const char *PHIEN_BAN = "KHKT-TAYCAM-1CAN v2";

uint8_t seq = 0;
int tamX = 2048, tamY = 2048;  // vị trí nghỉ của cần (đo lúc khởi động)
uint32_t lanCuoiNghePcMs = 0;
bool daNghePc = false;
char lenh[32];
uint8_t doDaiLenh = 0;

// ======================================================================
// Đầu ra: USB và (nếu bật) Bluetooth
// ======================================================================

void ghiRa(const uint8_t *data, size_t len) {
  Serial.write(data, len);
#if DUNG_BLUETOOTH
  if (SerialBT.hasClient()) SerialBT.write(data, len);
#endif
}

// Dòng chữ '#...': game và tools/tay_cam_monitor.py hiện ra dưới dạng thông báo. Chỉ dùng ký tự ASCII.
void guiChu(const String &s) {
  String dong = "#" + s + "\n";
  ghiRa((const uint8_t *)dong.c_str(), dong.length());
}

// ======================================================================
// Joystick
// ======================================================================

int docTrungBinh(int pin) {
  int tong = 0;
  for (int i = 0; i < 4; i++) tong += analogRead(pin);
  return tong / 4;
}

// Lấy vị trí nghỉ làm tâm (mạch joystick rẻ thường lệch tâm vài phần trăm). Không chạm cần lúc này.
void canTam() {
  long sx = 0, sy = 0;
  for (int i = 0; i < 32; i++) {
    sx += docTrungBinh(PIN_TRUC_X);
    sy += docTrungBinh(PIN_TRUC_Y);
    delay(2);
  }
  tamX = sx / 32;
  tamY = sy / 32;
}

// 0..4095 (tâm = vị trí nghỉ) sang 0..1023 tâm 512, cùng thang với analogRead của Mega
uint16_t doiThang(int v, int tam, bool dao) {
  long ra;
  if (v >= tam) ra = 512 + (long)(v - tam) * 511 / (4095 - tam > 1 ? 4095 - tam : 1);
  else ra = 512 - (long)(tam - v) * 512 / (tam > 1 ? tam : 1);
  if (ra < 0) ra = 0;
  if (ra > 1023) ra = 1023;
  return dao ? 1023 - ra : ra;
}

// ======================================================================
// Gói tin 29 byte
// ======================================================================

uint8_t crc8(const uint8_t *data, uint8_t len) {
  uint8_t crc = 0;
  for (uint8_t i = 0; i < len; i++) {
    crc ^= data[i];
    for (uint8_t b = 0; b < 8; b++) crc = (crc & 0x80) ? (crc << 1) ^ 0x07 : (crc << 1);
  }
  return crc;
}

void ghiU16(uint8_t *p, uint16_t v) {
  p[0] = v & 0xFF;
  p[1] = v >> 8;
}

void guiGoi() {
  uint32_t t = millis();
  uint16_t x = doiThang(docTrungBinh(PIN_TRUC_X), tamX, DAO_TRUC_X);
  uint16_t y = doiThang(docTrungBinh(PIN_TRUC_Y), tamY, DAO_TRUC_Y);
#if DOI_X_Y
  uint16_t tam = x;
  x = y;
  y = tam;
#endif
  uint8_t nut = CO_MOT_CAN | CO_KHONG_IMU;
  if (digitalRead(PIN_NUT) == LOW) nut |= NUT_NHAN_CAN;

  uint8_t g[29] = {0};
  g[0] = 0xA5;
  g[1] = 0x5A;
  g[2] = seq++;
  g[3] = t & 0xFF; g[4] = (t >> 8) & 0xFF; g[5] = (t >> 16) & 0xFF; g[6] = (t >> 24) & 0xFF;
  // byte 7..18: gia tốc và con quay = 0 (không có MPU6050)
  ghiU16(g + 19, 512);  // j1x: không có cần thứ hai, để ở tâm
  ghiU16(g + 21, 512);  // j1y
  ghiU16(g + 23, x);    // j2x
  ghiU16(g + 25, y);    // j2y
  g[27] = nut;
  g[28] = crc8(g + 2, 26);
  ghiRa(g, 29);
}

// ======================================================================
// Lệnh từ máy tính
// ======================================================================

void xuLyLenh(const char *s) {
  lanCuoiNghePcMs = millis();
  daNghePc = true;
  switch (s[0]) {
    case 'J':
      canTam();
      guiChu("JOY tam=" + String(tamX) + "," + String(tamY));
      break;
    case 'I':
      guiChu(String("INFO ") + PHIEN_BAN + " tam=" + tamX + "," + tamY +
             (DUNG_BLUETOOTH ? " bluetooth=" TEN_BLUETOOTH : " bluetooth=tat"));
      break;
    default:  // H, R, P, L, C: không có phần cứng tương ứng
      break;
  }
}

void docTuCong(Stream &cong) {
  while (cong.available()) {
    char c = cong.read();
    if (c == '\n' || c == '\r') {
      if (doDaiLenh > 0) {
        lenh[doDaiLenh] = '\0';
        xuLyLenh(lenh);
        doDaiLenh = 0;
      }
    } else if (doDaiLenh < sizeof(lenh) - 1) {
      lenh[doDaiLenh++] = c;
    }
  }
}

// LED đỏ trên board: nháy chậm = chưa nối game, sáng liên tục = game đang đọc tay cầm
void capNhatLed() {
  bool coGame = daNghePc && millis() - lanCuoiNghePcMs < HET_HAN_PC_MS;
  bool sang = coGame || (millis() / 500) % 2;
  digitalWrite(PIN_LED_BOARD, sang ? LOW : HIGH);
}

// ======================================================================

void setup() {
  pinMode(PIN_DEN_FLASH, OUTPUT);
  digitalWrite(PIN_DEN_FLASH, LOW);
  pinMode(PIN_LED_BOARD, OUTPUT);
  digitalWrite(PIN_LED_BOARD, HIGH);
  pinMode(PIN_NUT, INPUT_PULLUP);
  analogReadResolution(12);
  analogSetAttenuation(ADC_11db);  // đo được tới khoảng 3,1 V (joystick cấp 3,3 V)

  Serial.begin(TOC_DO_BAUD);
#if DUNG_BLUETOOTH
  SerialBT.begin(TEN_BLUETOOTH);
#endif
  canTam();
  guiChu(String(PHIEN_BAN) + " tam=" + tamX + "," + tamY);
}

void loop() {
  static uint32_t tiepTheo = micros();
  const uint32_t CHU_KY_US = 1000000UL / TAN_SO_GOI_HZ;
  docTuCong(Serial);
#if DUNG_BLUETOOTH
  docTuCong(SerialBT);
#endif
  if ((int32_t)(micros() - tiepTheo) >= 0) {
    tiepTheo += CHU_KY_US;
    if ((int32_t)(micros() - tiepTheo) > (int32_t)CHU_KY_US) tiepTheo = micros() + CHU_KY_US;  // trễ nhiều: bắt nhịp lại
    guiGoi();
    capNhatLed();
  }
}
