// ESP32 (ESP-32S / WROOM-32) + Joystick, gui du lieu qua WiFi (UDP). KHONG dung USB de truyen du lieu.
// Board trong Arduino IDE: "ESP32 Dev Module"
//
// Day noi:  Joystick VCC -> 3V3 | GND -> GND | VRx -> IO34 | VRy -> IO35 | SW -> IO27

#include <WiFi.h>
#include <WiFiUdp.h>

// ====== SUA CHO PHU HOP ======
const char* WIFI_SSID = "TEN_WIFI";
const char* WIFI_PASS = "MAT_KHAU";
const uint16_t UDP_PORT = 4210;
IPAddress TARGET_IP(255, 255, 255, 255);   // broadcast; neu mang chan thi doi thanh IP may tinh
const bool DEBUG_SERIAL = false;           // false: khong in gi ra USB khi chay that. Bat true chi luc nap/test.
// =============================

const int PIN_X  = 34;   // ADC1 (chi vao)
const int PIN_Y  = 35;   // ADC1 (chi vao)
const int PIN_SW = 27;   // nut nhan, dung INPUT_PULLUP
const int PIN_LED = 2;   // LED tren board (neu co): nhay khi gui duoc, sang lien tuc khi co WiFi

WiFiUDP udp;
uint32_t lastSend = 0, lastLed = 0;
bool ledState = false;

void connectWiFi() {
  WiFi.mode(WIFI_STA);
  WiFi.begin(WIFI_SSID, WIFI_PASS);
  uint32_t t0 = millis();
  while (WiFi.status() != WL_CONNECTED) {
    // nhay LED nhanh = dang tim WiFi
    digitalWrite(PIN_LED, (millis() / 150) % 2);
    delay(50);
    if (millis() - t0 > 20000) {
      WiFi.disconnect();
      WiFi.begin(WIFI_SSID, WIFI_PASS);
      t0 = millis();
    }
  }
  digitalWrite(PIN_LED, HIGH);
  if (DEBUG_SERIAL) {
    Serial.print("IP: "); Serial.println(WiFi.localIP());
  }
}

void setup() {
  pinMode(PIN_LED, OUTPUT);
  pinMode(PIN_SW, INPUT_PULLUP);
  analogReadResolution(12);                 // 0..4095
  analogSetAttenuation(ADC_11db);           // do duoc 0..3.3V

  if (DEBUG_SERIAL) {
    Serial.begin(115200);
  }
  connectWiFi();
  udp.begin(UDP_PORT);
}

void loop() {
  if (WiFi.status() != WL_CONNECTED) {
    connectWiFi();
  }

  if (millis() - lastSend >= 20) {          // ~50 Hz
    lastSend = millis();
    int x = analogRead(PIN_X);
    int y = analogRead(PIN_Y);
    int b = (digitalRead(PIN_SW) == LOW) ? 1 : 0;

    char buf[32];
    snprintf(buf, sizeof(buf), "%d,%d,%d", x, y, b);
    udp.beginPacket(TARGET_IP, UDP_PORT);
    udp.print(buf);
    udp.endPacket();

    if (DEBUG_SERIAL) Serial.println(buf);
  }

  // Nhay LED cham ~2 lan/giay de biet dang gui du lieu
  if (millis() - lastLed > 250) {
    lastLed = millis();
    ledState = !ledState;
    digitalWrite(PIN_LED, ledState);
  }
}
