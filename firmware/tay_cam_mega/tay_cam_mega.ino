// Firmware tay cầm tự chế - Phòng thí nghiệm ảo KHKT
// Board: Arduino Mega 2560. Không cần thư viện ngoài (chỉ Wire).
// Giao thức: docs/02-giao-thuc-tay-cam.md
//
// Mỗi 10 ms (100 Hz): đọc MPU6050 (gia tốc + con quay), 2 joystick, 5 nút,
// gửi một gói nhị phân 29 byte. Nhận lệnh chữ từ PC để rung và bật đèn.

#include <Wire.h>

// ----- Chân nối (khớp với bảng trong bản thiết kế) -----
const uint8_t PIN_J1X = A0, PIN_J1Y = A1, PIN_J1SW = 22;  // joystick 1: chỉnh tinh
const uint8_t PIN_J2X = A2, PIN_J2Y = A3, PIN_J2SW = 26;  // joystick 2: di chuyển
const uint8_t PIN_CO = 23, PIN_CAN_LAI = 24, PIN_HOI_AI = 25;
const uint8_t PIN_LED_XANH = 30, PIN_LED_DO = 31;
const uint8_t PIN_ENA = 5, PIN_IN1 = 6, PIN_IN2 = 7;      // L298N, tháo jumper ENA

// ----- Hệ trục -----
// Đổi chip sang hệ trục tay cầm: X dọc ống ra phía trước, Y sang trái, Z lên trên.
// Mặc định: chip gắn mặt hướng lên, trục X in trên chip chỉ ra đầu tay cầm.
// Nếu gắn khác, đổi các macro này, ví dụ chip quay 90 độ: X = (y), Y = (-x).
#define TRUC_X(x, y, z) (x)
#define TRUC_Y(x, y, z) (y)
#define TRUC_Z(x, y, z) (z)

const uint8_t MPU_ADDR = 0x68;
const uint32_t CHU_KY_US = 10000;        // 100 Hz
const uint32_t HET_HAN_PC_MS = 2000;     // quá thời gian này không nghe PC thì tắt rung/đèn
const char *PHIEN_BAN = "KHKT-TAYCAM v1";

// ----- Trạng thái -----
uint8_t seq = 0;
int32_t biasGx = 0, biasGy = 0, biasGz = 0;
bool loiImu = false;

uint8_t mucRung = 0;            // mức rung liên tục (lệnh R)
uint8_t mucXung = 0;            // mức xung (lệnh P)
uint32_t hetXungMs = 0;
bool ledXanh = false, ledDo = false;
uint32_t lanCuoiNghePcMs = 0;
bool daNghePc = false;

char dongLenh[24];
uint8_t doDaiLenh = 0;

// ----- MPU6050 -----
void ghiThanhGhi(uint8_t reg, uint8_t val) {
  Wire.beginTransmission(MPU_ADDR);
  Wire.write(reg);
  Wire.write(val);
  if (Wire.endTransmission() != 0) loiImu = true;
}

void khoiDongMpu() {
  loiImu = false;
  ghiThanhGhi(0x6B, 0x01);  // PWR_MGMT_1: thức dậy, xung nhịp theo con quay X
  delay(50);
  ghiThanhGhi(0x1A, 0x03);  // CONFIG: lọc thông thấp ~44 Hz (vẫn giữ dải run tay 4-12 Hz)
  ghiThanhGhi(0x19, 0x09);  // SMPLRT_DIV: 1 kHz / (1 + 9) = 100 Hz
  ghiThanhGhi(0x1B, 0x08);  // GYRO_CONFIG: ±500 °/s
  ghiThanhGhi(0x1C, 0x00);  // ACCEL_CONFIG: ±2 g
}

// Đọc 14 byte từ 0x3B: ax ay az nhiệt gx gy gz (big-endian). Trả về false nếu lỗi.
bool docMpu(int16_t &ax, int16_t &ay, int16_t &az, int16_t &gx, int16_t &gy, int16_t &gz) {
  Wire.beginTransmission(MPU_ADDR);
  Wire.write(0x3B);
  if (Wire.endTransmission(false) != 0) return false;
  if (Wire.requestFrom(MPU_ADDR, (uint8_t)14) != 14) return false;
  uint8_t b[14];
  for (uint8_t i = 0; i < 14; i++) b[i] = Wire.read();
  int16_t rax = (b[0] << 8) | b[1], ray = (b[2] << 8) | b[3], raz = (b[4] << 8) | b[5];
  int16_t rgx = (b[8] << 8) | b[9], rgy = (b[10] << 8) | b[11], rgz = (b[12] << 8) | b[13];
  ax = TRUC_X(rax, ray, raz); ay = TRUC_Y(rax, ray, raz); az = TRUC_Z(rax, ray, raz);
  gx = TRUC_X(rgx, rgy, rgz); gy = TRUC_Y(rgx, rgy, rgz); gz = TRUC_Z(rgx, rgy, rgz);
  return true;
}

// Lấy trung bình con quay khi để yên. Nếu tay cầm bị lắc trong lúc đo thì đo lại (tối đa 3 lần).
void hieuChuanConQuay() {
  for (uint8_t lan = 0; lan < 3; lan++) {
    int32_t sx = 0, sy = 0, sz = 0;
    int16_t minZ = 32767, maxZ = -32768;
    uint16_t n = 0;
    for (uint16_t i = 0; i < 200; i++) {
      int16_t ax, ay, az, gx, gy, gz;
      if (docMpu(ax, ay, az, gx, gy, gz)) {
        sx += gx; sy += gy; sz += gz; n++;
        if (gz < minZ) minZ = gz;
        if (gz > maxZ) maxZ = gz;
      }
      digitalWrite(PIN_LED_XANH, (i / 10) % 2);  // nháy nhanh khi đang hiệu chuẩn
      delay(10);
    }
    if (n > 150 && (maxZ - minZ) < 200) {  // dao động < ~3 °/s: coi là đứng yên
      biasGx = sx / n; biasGy = sy / n; biasGz = sz / n;
      break;
    }
  }
  digitalWrite(PIN_LED_XANH, LOW);
  Serial.print('#'); Serial.print("CAL bias="); Serial.print(biasGx); Serial.print(',');
  Serial.print(biasGy); Serial.print(','); Serial.println(biasGz);
}

// ----- CRC-8, đa thức 0x07 -----
uint8_t crc8(const uint8_t *data, uint8_t len) {
  uint8_t crc = 0;
  for (uint8_t i = 0; i < len; i++) {
    crc ^= data[i];
    for (uint8_t b = 0; b < 8; b++) crc = (crc & 0x80) ? (crc << 1) ^ 0x07 : (crc << 1);
  }
  return crc;
}

void ghiU16(uint8_t *p, uint16_t v) { p[0] = v & 0xFF; p[1] = v >> 8; }
void ghiI16(uint8_t *p, int16_t v) { ghiU16(p, (uint16_t)v); }

int16_t truBias(int16_t v, int32_t bias) {
  int32_t r = (int32_t)v - bias;
  if (r > 32767) r = 32767;
  if (r < -32768) r = -32768;
  return (int16_t)r;
}

void guiGoi() {
  int16_t ax = 0, ay = 0, az = 0, gx = 0, gy = 0, gz = 0;
  uint32_t t = millis();
  bool ok = docMpu(ax, ay, az, gx, gy, gz);
  if (!ok) loiImu = true;

  uint8_t nut = 0;
  if (digitalRead(PIN_CO) == LOW) nut |= 1 << 0;
  if (digitalRead(PIN_CAN_LAI) == LOW) nut |= 1 << 1;
  if (digitalRead(PIN_HOI_AI) == LOW) nut |= 1 << 2;
  if (digitalRead(PIN_J1SW) == LOW) nut |= 1 << 3;
  if (digitalRead(PIN_J2SW) == LOW) nut |= 1 << 4;
  if (!ok) nut |= 1 << 7;

  uint8_t g[29];
  g[0] = 0xA5; g[1] = 0x5A;
  g[2] = seq++;
  g[3] = t & 0xFF; g[4] = (t >> 8) & 0xFF; g[5] = (t >> 16) & 0xFF; g[6] = (t >> 24) & 0xFF;
  ghiI16(g + 7, ax); ghiI16(g + 9, ay); ghiI16(g + 11, az);
  ghiI16(g + 13, truBias(gx, biasGx)); ghiI16(g + 15, truBias(gy, biasGy)); ghiI16(g + 17, truBias(gz, biasGz));
  ghiU16(g + 19, analogRead(PIN_J1X)); ghiU16(g + 21, analogRead(PIN_J1Y));
  ghiU16(g + 23, analogRead(PIN_J2X)); ghiU16(g + 25, analogRead(PIN_J2Y));
  g[27] = nut;
  g[28] = crc8(g + 2, 26);
  Serial.write(g, 29);
}

// ----- Rung và đèn -----
void capNhatRungDen() {
  uint32_t now = millis();
  bool pcSong = daNghePc && (now - lanCuoiNghePcMs < HET_HAN_PC_MS);
  if (!pcSong) {
    // Mất PC: tắt hết để motor không rung mãi, nháy xanh chậm báo "chưa kết nối"
    mucRung = 0; mucXung = 0; ledDo = false;
    analogWrite(PIN_ENA, 0);
    digitalWrite(PIN_LED_DO, LOW);
    digitalWrite(PIN_LED_XANH, (now / 500) % 2);
    return;
  }
  uint8_t muc = mucRung;
  if (mucXung > 0) {
    if ((int32_t)(now - hetXungMs) < 0) muc = mucXung;
    else mucXung = 0;
  }
  analogWrite(PIN_ENA, muc);
  digitalWrite(PIN_LED_XANH, ledXanh);
  digitalWrite(PIN_LED_DO, ledDo);
}

void xuLyLenh(char *s) {
  switch (s[0]) {
    case 'R':
      mucRung = constrain(atoi(s + 1), 0, 255);
      break;
    case 'P': {
      char *phay = strchr(s, ',');
      if (!phay) break;
      mucXung = constrain(atoi(s + 1), 0, 255);
      hetXungMs = millis() + constrain(atol(phay + 1), 0, 2000);
      break;
    }
    case 'L': {
      char *phay = strchr(s, ',');
      if (!phay) break;
      ledXanh = atoi(s + 1) != 0;
      ledDo = atoi(phay + 1) != 0;
      break;
    }
    case 'C':
      analogWrite(PIN_ENA, 0);  // tắt rung để không làm nhiễu phép đo bias
      hieuChuanConQuay();
      break;
    case 'I':
      Serial.print('#'); Serial.print("INFO "); Serial.print(PHIEN_BAN);
      Serial.print(" imu="); Serial.print(loiImu ? "LOI" : "OK");
      Serial.print(" bias="); Serial.print(biasGx); Serial.print(',');
      Serial.print(biasGy); Serial.print(','); Serial.println(biasGz);
      break;
    case 'H':
    default:
      break;
  }
}

void docLenh() {
  while (Serial.available()) {
    char c = Serial.read();
    lanCuoiNghePcMs = millis();
    daNghePc = true;
    if (c == '\n' || c == '\r') {
      if (doDaiLenh > 0) {
        dongLenh[doDaiLenh] = '\0';
        xuLyLenh(dongLenh);
        doDaiLenh = 0;
      }
    } else if (doDaiLenh < sizeof(dongLenh) - 1) {
      dongLenh[doDaiLenh++] = c;
    } else {
      doDaiLenh = 0;  // dòng quá dài: bỏ
    }
  }
}

void setup() {
  Serial.begin(115200);
  pinMode(PIN_J1SW, INPUT_PULLUP); pinMode(PIN_J2SW, INPUT_PULLUP);
  pinMode(PIN_CO, INPUT_PULLUP); pinMode(PIN_CAN_LAI, INPUT_PULLUP); pinMode(PIN_HOI_AI, INPUT_PULLUP);
  pinMode(PIN_LED_XANH, OUTPUT); pinMode(PIN_LED_DO, OUTPUT);
  pinMode(PIN_ENA, OUTPUT); pinMode(PIN_IN1, OUTPUT); pinMode(PIN_IN2, OUTPUT);
  digitalWrite(PIN_IN1, HIGH); digitalWrite(PIN_IN2, LOW);  // motor chỉ quay một chiều
  analogWrite(PIN_ENA, 0);

  Wire.begin();
  Wire.setClock(100000);           // 100 kHz cho dây I2C dài ~1 m (400 kHz dễ lỗi)
  Wire.setWireTimeout(3000, true); // không treo cả chương trình khi dây I2C chập chờn
  khoiDongMpu();
  hieuChuanConQuay();

  Serial.print('#'); Serial.print(PHIEN_BAN); Serial.println(loiImu ? " imu=LOI" : " imu=OK");
}

void loop() {
  static uint32_t tiepTheo = micros();
  docLenh();
  if ((int32_t)(micros() - tiepTheo) >= 0) {
    tiepTheo += CHU_KY_US;
    if ((int32_t)(micros() - tiepTheo) > (int32_t)CHU_KY_US) tiepTheo = micros() + CHU_KY_US;  // bị trễ quá nhiều: bắt nhịp lại
    if (Wire.getWireTimeoutFlag()) {  // I2C từng bị treo: khởi động lại cảm biến
      Wire.clearWireTimeoutFlag();
      khoiDongMpu();
    }
    guiGoi();
    capNhatRungDen();
  }
}
