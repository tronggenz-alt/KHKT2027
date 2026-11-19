
#include <Wire.h>

// ===== PIN CONFIGURATION =====
#define SDA_PIN 25
#define SCL_PIN 26
#define BMI_ADDR 0x69

// ===== BMI160 REGISTERS =====
#define REG_CHIP_ID   0x00
#define REG_GYR_DATA  0x0C
#define REG_ACC_CONF  0x40
#define REG_ACC_RANGE 0x41
#define REG_GYR_CONF  0x42
#define REG_GYR_RANGE 0x43
#define REG_CMD       0x7E

// ===== I2C WRITE =====
bool writeReg(uint8_t reg, uint8_t value) {
  Wire.beginTransmission(BMI_ADDR);
  Wire.write(reg);
  Wire.write(value);
  return Wire.endTransmission() == 0;
}

// ===== I2C READ =====
bool readRegs(uint8_t reg, uint8_t *data, uint8_t len) {
  Wire.beginTransmission(BMI_ADDR);
  Wire.write(reg);

  if (Wire.endTransmission(false) != 0)
    return false;

  uint8_t count = Wire.requestFrom(BMI_ADDR, len);

  if (count != len)
    return false;

  for (int i = 0; i < len; i++) {
    data[i] = Wire.read();
  }

  return true;
}

// ===== CONVERT RAW DATA =====
int16_t toInt16(uint8_t lsb, uint8_t msb) {
  return (int16_t)((uint16_t)msb << 8 | lsb);
}

void setup() {
  Serial.begin(115200);
  delay(1000);

  Wire.begin(SDA_PIN, SCL_PIN);
  Wire.setClock(400000);

  Serial.println("\n===== BMI160 INITIALIZATION =====");

  // Check CHIP ID
  uint8_t chipID;

  if (!readRegs(REG_CHIP_ID, &chipID, 1) || chipID != 0xD1) {
    Serial.println("BMI160 NOT FOUND!");
    while (1) delay(1000);
  }

  Serial.println("BMI160 DETECTED!");

  // Enable accelerometer normal mode
  writeReg(REG_CMD, 0x11);
  delay(50);

  // Enable gyroscope normal mode
  writeReg(REG_CMD, 0x15);
  delay(80);

  // Accelerometer: 100Hz, normal bandwidth
  writeReg(REG_ACC_CONF, 0x28);

  // Accelerometer range: +/- 2g
  writeReg(REG_ACC_RANGE, 0x03);

  // Gyroscope: 100Hz
  writeReg(REG_GYR_CONF, 0x28);

  // Gyroscope range: +/- 2000 degrees/s
  writeReg(REG_GYR_RANGE, 0x00);

  delay(50);

  Serial.println("BMI160 READY!");
  Serial.println("Sampling rate: 100Hz");
  Serial.println("Format: AX,AY,AZ,GX,GY,GZ");
  Serial.println("--------------------------------");
}

void loop() {
  uint8_t data[12];

  // Read 12 bytes from gyro + accelerometer registers
  if (readRegs(REG_GYR_DATA, data, 12)) {

    // Gyroscope raw values
    int16_t gxRaw = toInt16(data[0], data[1]);
    int16_t gyRaw = toInt16(data[2], data[3]);
    int16_t gzRaw = toInt16(data[4], data[5]);

    // Accelerometer raw values
    int16_t axRaw = toInt16(data[6], data[7]);
    int16_t ayRaw = toInt16(data[8], data[9]);
    int16_t azRaw = toInt16(data[10], data[11]);

    // Convert to physical units
    float ax = axRaw / 16384.0;
    float ay = ayRaw / 16384.0;
    float az = azRaw / 16384.0;

    float gx = gxRaw / 16.4;
    float gy = gyRaw / 16.4;
    float gz = gzRaw / 16.4;

    // CSV output for Unity
    Serial.print("DATA,");

    Serial.print(ax, 3); Serial.print(",");
    Serial.print(ay, 3); Serial.print(",");
    Serial.print(az, 3); Serial.print(",");

    Serial.print(gx, 2); Serial.print(",");
    Serial.print(gy, 2); Serial.print(",");
    Serial.println(gz, 2);

  } else {
    Serial.println("READ_ERROR");
  }

  delay(10);
}
