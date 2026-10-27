// ARDUINO MEGA 2560 - Doc joystick va gui cho ESP32 KHONG CAN dien tro / cau chia ap.
// Y tuong "open-drain": Mega KHONG BAO GIO xuat muc 5V ra day.
//   - bit 0: keo chan xuong GND (pinMode OUTPUT, muc LOW da dat san)
//   - bit 1: tha noi (pinMode INPUT) -> ESP32 tu keo len 3.3V bang pull-up noi
// Toc do 9600 baud, dinh dang moi dong: X,Y,B\n
//
// Day noi:  Mega D22 -> ESP32 IO13   |  Mega GND -> ESP32 GND
//
// Dung pinMode (khong dung thanh ghi) vi da test chay duoc tren board nay.
// Thoi gian moi bit tinh theo micros() tuyet doi nen khong bi tich luy sai lech.

const int PIN_X  = A0;
const int PIN_Y  = A1;
const int PIN_SW = 2;
const int PIN_TX = 22;                    // chan gui sang ESP32 (D22..D31 deu duoc)
const unsigned long BIT_US = 104;         // 1/9600 giay

inline void setLine(bool high) {
  if (high) pinMode(PIN_TX, INPUT);       // tha noi -> ESP32 keo len 3.3V
  else      pinMode(PIN_TX, OUTPUT);      // keo xuong GND (PORT dang = 0)
}

void sendByte(uint8_t c) {
  // khung: start(0) + 8 bit data (LSB truoc) + stop(1)
  uint16_t frame = ((uint16_t)c << 1) | (1u << 9);
  unsigned long t0 = micros();
  for (uint8_t i = 0; i < 10; i++) {
    while ((long)(micros() - (t0 + i * BIT_US)) < 0) { }   // cho den dung moc thoi gian cua bit i
    setLine(frame & 1);
    frame >>= 1;
  }
  while ((long)(micros() - (t0 + 10 * BIT_US)) < 0) { }    // het bit stop
}

void sendString(const char* s) {
  while (*s) sendByte((uint8_t)*s++);
}

void setup() {
  Serial.begin(115200);                   // debug qua USB
  pinMode(PIN_SW, INPUT_PULLUP);
  pinMode(PIN_TX, INPUT);                 // tha noi truoc
  digitalWrite(PIN_TX, LOW);              // dat PORT=0 de khi sang OUTPUT se la LOW, khong bao gio HIGH
}

void loop() {
  int x = analogRead(PIN_X);
  int y = analogRead(PIN_Y);
  int b = (digitalRead(PIN_SW) == LOW) ? 1 : 0;

  char buf[24];
  snprintf(buf, sizeof(buf), "%d,%d,%d\n", x, y, b);
  sendString(buf);

  Serial.print("X="); Serial.print(x);
  Serial.print(" Y="); Serial.print(y);
  Serial.print(" B="); Serial.println(b);

  delay(30);                              // ~25 Hz (moi dong mat ~10 ms o 9600 baud)
}
