// chan_doan_i2c.ino - chẩn đoán lỗi "I2C không phản hồi" của BMI160 trên ESP32 DevKit (SDA 21, SCL 22).
// In ra Serial 115200, mỗi dòng bắt đầu "CD," để công cụ trên máy đọc tự động.
// 1) mức nghỉ của SDA/SCL (có điện trở kéo lên không, có bị chập không)
// 2) quét địa chỉ ở 100 kHz và 400 kHz
// 3) mã chip: thanh ghi 0x00 (BMI160 = 0xD1), 0x75 (MPU6050/6500/9250, ICM-20xxx), 0x0F (LSM6/LIS)
// 4) độ tin cậy: đọc CHIP_ID 500 lần ở mỗi tốc độ, đếm lỗi theo mã lỗi của Wire
// 5) nếu là BMI160: khởi động gia tốc + con quay, đọc 20 mẫu xem có số thay đổi không
#include <Wire.h>

const int SDA_PIN = 21, SCL_PIN = 22;
uint8_t addr = 0;

bool rd(uint8_t a, uint8_t reg, uint8_t *buf, uint8_t len, uint8_t *err) {
  Wire.beginTransmission(a);
  Wire.write(reg);
  uint8_t e = Wire.endTransmission(false);
  if (err) *err = e;
  if (e != 0) return false;
  if (Wire.requestFrom((int)a, (int)len) != len) { if (err) *err = 9; return false; }
  for (uint8_t i = 0; i < len; i++) buf[i] = Wire.read();
  return true;
}

void wr(uint8_t a, uint8_t reg, uint8_t v) {
  Wire.beginTransmission(a);
  Wire.write(reg);
  Wire.write(v);
  Wire.endTransmission();
}

void lineLevels() {
  // tắt Wire, đọc mức nghỉ khi không có kéo lên bên trong: phải là 1 nếu module có điện trở kéo lên
  Wire.end();
  pinMode(SDA_PIN, INPUT);
  pinMode(SCL_PIN, INPUT);
  delay(5);
  int sda = digitalRead(SDA_PIN), scl = digitalRead(SCL_PIN);
  int sdaA = analogReadMilliVolts(SDA_PIN), sclA = analogReadMilliVolts(SCL_PIN);
  Serial.printf("CD,MUC_NGHI,SDA=%d(%dmV),SCL=%d(%dmV)\n", sda, sdaA, scl, sclA);
}

int scan(uint32_t hz) {
  Wire.begin(SDA_PIN, SCL_PIN, hz);
  Wire.setTimeOut(20);
  int n = 0;
  String found = "";
  for (uint8_t a = 1; a < 127; a++) {
    Wire.beginTransmission(a);
    if (Wire.endTransmission() == 0) {
      n++;
      found += String(" 0x") + String(a, HEX);
      if (a == 0x68 || a == 0x69) addr = a;
    }
  }
  Serial.printf("CD,QUET,%luHz,%d,%s\n", (unsigned long)hz, n, found.c_str());
  return n;
}

void ids() {
  uint8_t v, e;
  const uint8_t regs[3] = {0x00, 0x75, 0x0F};
  for (int i = 0; i < 3; i++) {
    bool ok = rd(addr, regs[i], &v, 1, &e);
    Serial.printf("CD,ID,0x%02X,reg0x%02X,%s,0x%02X,err%d\n", addr, regs[i], ok ? "ok" : "loi", ok ? v : 0, e);
  }
}

void reliability(uint32_t hz) {
  Wire.end();
  Wire.begin(SDA_PIN, SCL_PIN, hz);
  Wire.setTimeOut(20);
  int fail = 0, wrong = 0, errs[10] = {0};
  uint8_t first = 0;
  for (int i = 0; i < 500; i++) {
    uint8_t v = 0, e = 0;
    if (!rd(addr, 0x00, &v, 1, &e)) { fail++; errs[e < 10 ? e : 9]++; }
    else { if (i == 0) first = v; if (v != first) wrong++; }
    delayMicroseconds(200);
  }
  Serial.printf("CD,TIN_CAY,%luHz,loi=%d/500,sai_gia_tri=%d,e1=%d,e2=%d,e3=%d,e4=%d,e5=%d,e9=%d\n",
                (unsigned long)hz, fail, wrong, errs[1], errs[2], errs[3], errs[4], errs[5], errs[9]);
}

void bmiData() {
  uint8_t id = 0, e;
  if (!rd(addr, 0x00, &id, 1, &e) || id != 0xD1) { Serial.printf("CD,BMI,khong_phai_BMI160,id=0x%02X\n", id); return; }
  wr(addr, 0x7E, 0xB6); delay(120);
  wr(addr, 0x7E, 0x11); delay(60);
  wr(addr, 0x7E, 0x15); delay(100);
  uint8_t pmu = 0, err = 0;
  rd(addr, 0x03, &pmu, 1, &e);
  rd(addr, 0x02, &err, 1, &e);
  Serial.printf("CD,BMI,PMU=0x%02X,ERR_REG=0x%02X\n", pmu, err);
  int16_t prev[6] = {0}; int changes = 0;
  for (int i = 0; i < 20; i++) {
    uint8_t b[12];
    if (!rd(addr, 0x0C, b, 12, &e)) { Serial.printf("CD,MAU,%d,loi,err%d\n", i, e); delay(10); continue; }
    int16_t v[6];
    for (int k = 0; k < 6; k++) v[k] = (int16_t)(b[2 * k + 1] << 8 | b[2 * k]);
    for (int k = 0; k < 6; k++) if (v[k] != prev[k]) { changes++; break; }
    memcpy(prev, v, sizeof(v));
    if (i % 5 == 0) Serial.printf("CD,MAU,%d,g=%d,%d,%d,a=%d,%d,%d\n", i, v[0], v[1], v[2], v[3], v[4], v[5]);
    delay(10);
  }
  Serial.printf("CD,BMI,so_mau_doi=%d/20\n", changes);
}

void setup() {
  Serial.begin(115200);
  delay(500);
  Serial.println("CD,BAT_DAU");
  lineLevels();
  int n = scan(100000);
  if (n == 0 || addr == 0) {
    Serial.println("CD,KET_LUAN,khong_co_thiet_bi_o_0x68_0x69");
  } else {
    ids();
    reliability(100000);
    reliability(400000);
    Wire.end();
    Wire.begin(SDA_PIN, SCL_PIN, 100000);
    bmiData();
  }
  scan(400000);
  Serial.println("CD,XONG");
}

void loop() {
  delay(3000);
  // lặp lại phép thử tin cậy để thấy lỗi chập chờn (rung dây, tiếp xúc kém)
  if (addr) { reliability(400000); reliability(100000); }
}
