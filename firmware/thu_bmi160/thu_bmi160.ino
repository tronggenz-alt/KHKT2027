// Thử BMI160 trên ESP32 DevKit V1: SDA = GPIO21, SCL = GPIO22, VCC 3V3, GND.
// Đọc thẳng thanh ghi (không cần thư viện), lọc Madgwick, gửi "q w x y z" 50 lần/giây qua USB 115200.
// Giữ cảm biến đứng yên 2 giây sau khi bật để đo độ lệch con quay.
#include <Wire.h>

const uint8_t ADDRS[] = {0x68, 0x69};
uint8_t addr = 0;
float bx = 0, by = 0, bz = 0;                  // độ lệch con quay (rad/s)
float q0 = 1, q1 = 0, q2 = 0, q3 = 0;
const float BETA = 0.08f;

void wr(uint8_t r, uint8_t v) { Wire.beginTransmission(addr); Wire.write(r); Wire.write(v); Wire.endTransmission(); }

bool rd(uint8_t r, uint8_t* b, uint8_t n) {
  Wire.beginTransmission(addr); Wire.write(r);
  if (Wire.endTransmission(false) != 0) return false;
  if (Wire.requestFrom(addr, n) != n) return false;
  for (uint8_t i = 0; i < n; i++) b[i] = Wire.read();
  return true;
}

// gyro ±2000 °/s (16.4 LSB/°/s), gia tốc ±2 g (16384 LSB/g)
bool readImu(float& gx, float& gy, float& gz, float& ax, float& ay, float& az) {
  uint8_t b[12];
  if (!rd(0x0C, b, 12)) return false;
  int16_t v[6];
  for (int i = 0; i < 6; i++) v[i] = (int16_t)(b[2 * i] | (b[2 * i + 1] << 8));
  const float g = 0.0174533f / 16.4f;
  gx = v[0] * g; gy = v[1] * g; gz = v[2] * g;
  ax = v[3] / 16384.0f; ay = v[4] / 16384.0f; az = v[5] / 16384.0f;
  return true;
}

void madgwick(float gx, float gy, float gz, float ax, float ay, float az, float dt) {
  float qd0 = 0.5f * (-q1 * gx - q2 * gy - q3 * gz);
  float qd1 = 0.5f * (q0 * gx + q2 * gz - q3 * gy);
  float qd2 = 0.5f * (q0 * gy - q1 * gz + q3 * gx);
  float qd3 = 0.5f * (q0 * gz + q1 * gy - q2 * gx);
  float n = sqrtf(ax * ax + ay * ay + az * az);
  if (n > 0.5f && n < 1.5f) {  // chỉ tin gia tốc kế khi gần 1 g (không đang lắc mạnh)
    ax /= n; ay /= n; az /= n;
    float f1 = 2 * (q1 * q3 - q0 * q2) - ax;
    float f2 = 2 * (q0 * q1 + q2 * q3) - ay;
    float f3 = 1 - 2 * (q1 * q1 + q2 * q2) - az;
    float s0 = -2 * q2 * f1 + 2 * q1 * f2;
    float s1 = 2 * q3 * f1 + 2 * q0 * f2 - 4 * q1 * f3;
    float s2 = -2 * q0 * f1 + 2 * q3 * f2 - 4 * q2 * f3;
    float s3 = 2 * q1 * f1 + 2 * q2 * f2;
    float sn = sqrtf(s0 * s0 + s1 * s1 + s2 * s2 + s3 * s3);
    if (sn > 0) { qd0 -= BETA * s0 / sn; qd1 -= BETA * s1 / sn; qd2 -= BETA * s2 / sn; qd3 -= BETA * s3 / sn; }
  }
  q0 += qd0 * dt; q1 += qd1 * dt; q2 += qd2 * dt; q3 += qd3 * dt;
  float qn = sqrtf(q0 * q0 + q1 * q1 + q2 * q2 + q3 * q3);
  q0 /= qn; q1 /= qn; q2 /= qn; q3 /= qn;
}

void setup() {
  Serial.begin(115200);
  Wire.begin(21, 22, 400000);
  delay(100);
  for (uint8_t a : ADDRS) {
    addr = a;
    uint8_t id = 0;
    if (rd(0x00, &id, 1) && id == 0xD1) break;
    addr = 0;
  }
  if (!addr) { while (true) { Serial.println("LOI khong thay BMI160 (kiem tra day SDA21 SCL22, 3V3, GND)"); delay(1000); } }
  wr(0x7E, 0xB6); delay(100);   // khởi động lại mềm
  wr(0x7E, 0x11); delay(10);    // gia tốc: chế độ thường
  wr(0x7E, 0x15); delay(80);    // con quay: chế độ thường
  wr(0x40, 0x28); wr(0x41, 0x03);  // gia tốc 100 Hz, ±2 g
  wr(0x42, 0x28); wr(0x43, 0x00);  // con quay 100 Hz, ±2000 °/s
  delay(50);
  Serial.printf("OK BMI160 o dia chi 0x%02X, giu yen 2 giay de can...\n", addr);
  float sx = 0, sy = 0, sz = 0, ax, ay, az, gx, gy, gz; int n = 0;
  for (uint32_t t = millis(); millis() - t < 2000; delay(10))
    if (readImu(gx, gy, gz, ax, ay, az)) { sx += gx; sy += gy; sz += gz; n++; }
  if (n) { bx = sx / n; by = sy / n; bz = sz / n; }
  Serial.printf("CAN xong, lech con quay (do/s): %.2f %.2f %.2f\n", bx * 57.3f, by * 57.3f, bz * 57.3f);
}

void loop() {
  static uint32_t last = micros(), lastOut = 0;
  float gx, gy, gz, ax, ay, az;
  if (!readImu(gx, gy, gz, ax, ay, az)) { Serial.println("LOI doc I2C"); delay(100); return; }
  uint32_t now = micros();
  float dt = (now - last) * 1e-6f; last = now;
  if (dt <= 0 || dt > 0.1f) dt = 0.01f;
  madgwick(gx - bx, gy - by, gz - bz, ax, ay, az, dt);
  if (millis() - lastOut >= 20) {
    lastOut = millis();
    Serial.printf("q %.4f %.4f %.4f %.4f\n", q0, q1, q2, q3);
  }
  delay(5);
}
