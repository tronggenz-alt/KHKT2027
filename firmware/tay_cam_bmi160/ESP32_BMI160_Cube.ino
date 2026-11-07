/*
 * ESP32_BMI160_Cube.ino - gửi hướng tay cầm (quaternion) cho Unity / tools/xem_bmi160.py
 *
 * Board: ESP32 Dev Module (DevKit V1), Serial 115200
 * Dây:   BMI160 VCC->3V3, GND->GND, SDA->GPIO21, SCL->GPIO22
 *
 * Chống trôi góc khi để yên:
 *  - Cân con quay lúc khởi động (để yên 1 giây).
 *  - Nằm yên (số đo con quay gần như không dao động) quá 0,5 giây: sửa độ lệch và ngừng cộng dồn con quay, góc đứng hẳn.
 *  - Khi chuyển động thì dùng độ lệch đã sửa gần nhất.
 *  BMI160 không có la bàn: roll, pitch không trôi; yaw chỉ còn trôi khi xoay liên tục lâu.
 *
 * Chống treo / đọc ra 0: timeout I2C, gỡ kẹt bus (9 xung SCL), cấu hình lại cảm biến,
 * kiểm tra chế độ nguồn mỗi giây, hồi phục thất bại 15 lần thì khởi động lại ESP32.
 *
 * Lệnh nhận qua Serial: 'z' đặt hướng về 0, 'c' cân lại con quay (để yên).
 * Dữ liệu gửi ra: Q,w,x,y,z,soLanHoiPhuc
 */
#include <Wire.h>
#include <math.h>

#define PIN_SDA 21
#define PIN_SCL 22
#define SAMPLE_HZ 100
#define OUT_HZ 50
#define BAUD 115200

#define REG_CHIP_ID  0x00
#define REG_PMU_STAT 0x03
#define REG_DATA     0x0C
#define REG_ACC_CONF 0x40
#define REG_ACC_RNG  0x41
#define REG_GYR_CONF 0x42
#define REG_GYR_RNG  0x43
#define REG_CMD      0x7E

const float ACC_LSB = 8192.0f;  // ±4 g
const float GYR_LSB = 65.6f;    // ±500 độ/s
const float DEG2RAD = 0.017453292f;

// Chống trôi
const float STILL_NOISE_DPS = 0.6f;  // độ dao động con quay dưới mức này là đang yên
const float STILL_ACC_G = 0.06f;     // |a| lệch 1 g dưới mức này là không bị lắc
const float BIAS_RATE = 0.02f;       // sửa độ lệch khi yên, hội tụ khoảng 0,5 giây
float mx = 0, my = 0, mz = 0, vx = 1, vy = 1, vz = 1;   // trung bình, phương sai trượt của con quay

uint8_t addr = 0x69;
float gbx = 0, gby = 0, gbz = 0;       // độ lệch con quay (độ/s)
float q0 = 1, q1 = 0, q2 = 0, q3 = 0;
float beta = 0.08f;
uint32_t recoverCount = 0, consecutiveFail = 0, sameCount = 0, stillCount = 0;
uint8_t lastRaw[12] = {0};

// ---------------- I2C ----------------
bool readRegs(uint8_t reg, uint8_t *buf, uint8_t len) {
  Wire.beginTransmission(addr);
  Wire.write(reg);
  if (Wire.endTransmission(false) != 0) return false;
  if (Wire.requestFrom((int)addr, (int)len) != len) return false;
  for (uint8_t i = 0; i < len; i++) buf[i] = Wire.read();
  return true;
}

bool writeReg(uint8_t reg, uint8_t v) {
  Wire.beginTransmission(addr);
  Wire.write(reg);
  Wire.write(v);
  return Wire.endTransmission() == 0;
}

void busClear() {
  pinMode(PIN_SDA, INPUT_PULLUP);
  pinMode(PIN_SCL, OUTPUT);
  for (int i = 0; i < 9; i++) {
    digitalWrite(PIN_SCL, LOW);  delayMicroseconds(5);
    digitalWrite(PIN_SCL, HIGH); delayMicroseconds(5);
  }
  pinMode(PIN_SDA, OUTPUT);
  digitalWrite(PIN_SDA, LOW);  delayMicroseconds(5);
  digitalWrite(PIN_SCL, HIGH); delayMicroseconds(5);
  digitalWrite(PIN_SDA, HIGH); delayMicroseconds(5);
}

void startBus() {
  busClear();
  Wire.begin(PIN_SDA, PIN_SCL);
  Wire.setClock(400000);
  Wire.setTimeOut(20);
}

bool detectAddr() {
  const uint8_t cand[2] = {0x69, 0x68};
  for (int i = 0; i < 2; i++) {
    addr = cand[i];
    uint8_t id;
    if (readRegs(REG_CHIP_ID, &id, 1) && id == 0xD1) return true;
  }
  return false;
}

bool initBMI() {
  if (!detectAddr()) return false;
  writeReg(REG_CMD, 0xB6); delay(120);   // khởi động lại mềm
  uint8_t id;
  if (!readRegs(REG_CHIP_ID, &id, 1) || id != 0xD1) return false;
  writeReg(REG_CMD, 0x11); delay(60);    // gia tốc: chế độ thường
  writeReg(REG_CMD, 0x15); delay(100);   // con quay: chế độ thường
  writeReg(REG_ACC_CONF, 0x28);          // 100 Hz, lọc thường
  writeReg(REG_ACC_RNG, 0x05);           // ±4 g
  writeReg(REG_GYR_CONF, 0x28);          // 100 Hz
  writeReg(REG_GYR_RNG, 0x02);           // ±500 độ/s
  delay(20);
  uint8_t st;
  if (!readRegs(REG_PMU_STAT, &st, 1)) return false;
  return (st & 0x3C) == 0x14;            // gia tốc + con quay đang chạy
}

void recoverSensor() {
  recoverCount++;
  Wire.end();
  delay(5);
  startBus();
  bool ok = false;
  for (int i = 0; i < 3 && !ok; i++) ok = initBMI();
  if (!ok) {
    if (++consecutiveFail >= 15) { delay(50); ESP.restart(); }
  } else consecutiveFail = 0;
  sameCount = 0;
  stillCount = 0;
}

// ---------------- Đọc cảm biến ----------------
bool readIMU(float &ax, float &ay, float &az, float &gx, float &gy, float &gz) {
  uint8_t b[12];
  if (!readRegs(REG_DATA, b, 12)) return false;
  int16_t rgx = (int16_t)(b[1] << 8 | b[0]);
  int16_t rgy = (int16_t)(b[3] << 8 | b[2]);
  int16_t rgz = (int16_t)(b[5] << 8 | b[4]);
  int16_t rax = (int16_t)(b[7] << 8 | b[6]);
  int16_t ray = (int16_t)(b[9] << 8 | b[8]);
  int16_t raz = (int16_t)(b[11] << 8 | b[10]);

  if (rax == 0 && ray == 0 && raz == 0) return false;      // cảm biến vừa treo / ngủ
  if (rax == -1 && ray == -1 && raz == -1) return false;   // bus kẹt
  if (memcmp(b, lastRaw, 12) == 0) {                       // số không đổi quá 2 giây: treo
    if (++sameCount > SAMPLE_HZ * 2) return false;
  } else sameCount = 0;
  memcpy(lastRaw, b, 12);

  ax = rax / ACC_LSB; ay = ray / ACC_LSB; az = raz / ACC_LSB;
  gx = rgx / GYR_LSB; gy = rgy / GYR_LSB; gz = rgz / GYR_LSB;
  return true;
}

void calibrateGyro() {
  float sx = 0, sy = 0, sz = 0, ax, ay, az, gx, gy, gz;
  int n = 0;
  uint32_t t0 = millis();
  while (n < 200 && millis() - t0 < 4000) {
    if (readIMU(ax, ay, az, gx, gy, gz)) { sx += gx; sy += gy; sz += gz; n++; }
    delay(5);
  }
  if (n > 50) { gbx = sx / n; gby = sy / n; gbz = sz / n; }
  stillCount = 0;
}

// ---------------- Madgwick 6 trục ----------------
void madgwick(float gx, float gy, float gz, float ax, float ay, float az, float dt) {
  float qDot1 = 0.5f * (-q1 * gx - q2 * gy - q3 * gz);
  float qDot2 = 0.5f * ( q0 * gx + q2 * gz - q3 * gy);
  float qDot3 = 0.5f * ( q0 * gy - q1 * gz + q3 * gx);
  float qDot4 = 0.5f * ( q0 * gz + q1 * gy - q2 * gx);

  float an = sqrtf(ax * ax + ay * ay + az * az);
  if (an > 0.5f && an < 1.5f) {   // chỉ tin gia tốc kế khi không bị lắc mạnh
    ax /= an; ay /= an; az /= an;
    float _2q0 = 2 * q0, _2q1 = 2 * q1, _2q2 = 2 * q2, _2q3 = 2 * q3;
    float _4q0 = 4 * q0, _4q1 = 4 * q1, _4q2 = 4 * q2, _8q1 = 8 * q1, _8q2 = 8 * q2;
    float q0q0 = q0 * q0, q1q1 = q1 * q1, q2q2 = q2 * q2, q3q3 = q3 * q3;
    float s0 = _4q0 * q2q2 + _2q2 * ax + _4q0 * q1q1 - _2q1 * ay;
    float s1 = _4q1 * q3q3 - _2q3 * ax + 4 * q0q0 * q1 - _2q0 * ay - _4q1 + _8q1 * q1q1 + _8q1 * q2q2 + _4q1 * az;
    float s2 = 4 * q0q0 * q2 + _2q0 * ax + _4q2 * q3q3 - _2q3 * ay - _4q2 + _8q2 * q1q1 + _8q2 * q2q2 + _4q2 * az;
    float s3 = 4 * q1q1 * q3 - _2q1 * ax + 4 * q2q2 * q3 - _2q2 * ay;
    float sn = sqrtf(s0 * s0 + s1 * s1 + s2 * s2 + s3 * s3);
    if (sn > 0) {
      qDot1 -= beta * s0 / sn; qDot2 -= beta * s1 / sn; qDot3 -= beta * s2 / sn; qDot4 -= beta * s3 / sn;
    }
  }
  q0 += qDot1 * dt; q1 += qDot2 * dt; q2 += qDot3 * dt; q3 += qDot4 * dt;
  float r = 1.0f / sqrtf(q0 * q0 + q1 * q1 + q2 * q2 + q3 * q3);
  q0 *= r; q1 *= r; q2 *= r; q3 *= r;
}

// ---------------- setup / loop ----------------
void setup() {
  Serial.begin(BAUD);
  delay(300);
  startBus();
  while (!initBMI()) { recoverSensor(); delay(200); }
  Serial.println("# BMI160 OK, giu yen de can con quay...");
  calibrateGyro();
  Serial.printf("# San sang, lech con quay (do/s): %.3f %.3f %.3f\n", gbx, gby, gbz);
}

uint32_t lastUs = 0, lastOut = 0, lastPmuCheck = 0, lastOkMs = 0;

void loop() {
  while (Serial.available()) {
    char c = Serial.read();
    if (c == 'z') { q0 = 1; q1 = q2 = q3 = 0; }
    else if (c == 'c') calibrateGyro();
  }

  uint32_t now = micros();
  if (now - lastUs < 1000000UL / SAMPLE_HZ) return;
  float dt = (now - lastUs) * 1e-6f;
  lastUs = now;
  if (dt > 0.1f) dt = 1.0f / SAMPLE_HZ;

  float ax, ay, az, gx, gy, gz;
  if (readIMU(ax, ay, az, gx, gy, gz)) {
    lastOkMs = millis();
    // Độ dao động của số đo thô (trung bình trượt khoảng 0,2 giây): đứng yên thì số đo gần như không đổi,
    // dù độ lệch đang sai bao nhiêu. Không dựa vào độ lệch nên lần cân đầu sai vẫn tự sửa được.
    const float a = 0.05f;
    mx += a * (gx - mx); my += a * (gy - my); mz += a * (gz - mz);
    vx += a * ((gx - mx) * (gx - mx) - vx);
    vy += a * ((gy - my) * (gy - my) - vy);
    vz += a * ((gz - mz) * (gz - mz) - vz);
    float amag = sqrtf(ax * ax + ay * ay + az * az);
    bool still = sqrtf(vx + vy + vz) < STILL_NOISE_DPS && fabsf(amag - 1.0f) < STILL_ACC_G;
    stillCount = still ? stillCount + 1 : 0;
    float cx = gx - gbx, cy = gy - gby, cz = gz - gbz;
    if (stillCount > SAMPLE_HZ / 2) {
      // yên quá 0,5 giây: số đo trung bình chính là độ lệch; không cộng dồn con quay nên góc đứng hẳn,
      // gia tốc kế vẫn giữ roll, pitch đúng
      gbx += BIAS_RATE * (mx - gbx); gby += BIAS_RATE * (my - gby); gbz += BIAS_RATE * (mz - gbz);
      cx = cy = cz = 0;
    }
    madgwick(cx * DEG2RAD, cy * DEG2RAD, cz * DEG2RAD, ax, ay, az, dt);
  } else {
    recoverSensor();  // giữ nguyên hướng hiện tại, khối 3D không nhảy
  }

  if (millis() - lastPmuCheck > 1000) {
    lastPmuCheck = millis();
    uint8_t st;
    if (!readRegs(REG_PMU_STAT, &st, 1) || (st & 0x3C) != 0x14) recoverSensor();
  }
  if (millis() - lastOkMs > 3000) { lastOkMs = millis(); recoverSensor(); }

  if (millis() - lastOut >= 1000 / OUT_HZ) {
    lastOut = millis();
    Serial.printf("Q,%.4f,%.4f,%.4f,%.4f,%lu\n", q0, q1, q2, q3, recoverCount);
  }
}
