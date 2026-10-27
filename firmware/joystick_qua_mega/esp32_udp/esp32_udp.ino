// ESP32 (ESP-32S / WROOM-32) - Nhan du lieu tu Mega (UART2) va gui len mang bang UDP broadcast
// Board trong Arduino IDE: "AI Thinker ESP32-CAM" (hoac "ESP32 Dev Module"), PSRAM: Disabled
// Dung cho module ESP32-CAM + ESP32-CAM-MB. Khong dung camera trong bai test nay.

#include <WiFi.h>
#include <WiFiUdp.h>
#include "driver/gpio.h"

// ====== SUA CHO PHU HOP ======
const char* WIFI_SSID = "TEN_WIFI";
const char* WIFI_PASS = "MAT_KHAU";
const uint16_t UDP_PORT = 4210;
// Mac dinh broadcast -> khong can biet IP may tinh. Neu mang chan broadcast, doi sang IP may tinh.
IPAddress TARGET_IP(255, 255, 255, 255);
// =============================

const int RX_PIN = 13;   // IO13 - noi THANG voi D22 cua Mega (open-drain, khong can dien tro)
const int TX_PIN = -1;   // khong dung (ESP32-CAM: IO16/17 bi PSRAM chiem)

WiFiUDP udp;
String line;
uint32_t pktCount = 0, lastStat = 0, lastRx = 0, lastHeartbeat = 0, rxBytes = 0;

void connectWiFi() {
  WiFi.mode(WIFI_STA);
  WiFi.begin(WIFI_SSID, WIFI_PASS);
  Serial.print("Dang ket noi WiFi");
  uint32_t t0 = millis();
  while (WiFi.status() != WL_CONNECTED) {
    delay(500);
    Serial.print(".");
    if (millis() - t0 > 20000) {   // 20s khong duoc thi thu lai
      Serial.println("\nKhong ket noi duoc, thu lai...");
      WiFi.disconnect();
      WiFi.begin(WIFI_SSID, WIFI_PASS);
      t0 = millis();
    }
  }
  Serial.println();
  Serial.print("DA KET NOI. IP ESP32: "); Serial.println(WiFi.localIP());
  Serial.print("RSSI: "); Serial.print(WiFi.RSSI()); Serial.println(" dBm");
}

void sendUdp(const String& s) {
  udp.beginPacket(TARGET_IP, UDP_PORT);
  udp.print(s);
  udp.endPacket();
  pktCount++;
}

void setup() {
  Serial.begin(115200);
  delay(500);
  Serial1.begin(9600, SERIAL_8N1, RX_PIN, TX_PIN);       // 9600 de khop code Mega open-drain
  gpio_set_pull_mode((gpio_num_t)RX_PIN, GPIO_PULLUP_ONLY); // keo len 3.3V noi bo (thay cho dien tro)
  connectWiFi();
  udp.begin(UDP_PORT);
}

void loop() {
  if (WiFi.status() != WL_CONNECTED) {
    Serial.println("Mat WiFi, ket noi lai...");
    connectWiFi();
  }

  // Doc tung dong "X,Y,B" tu Mega
  while (Serial1.available()) {
    char c = Serial1.read();
    rxBytes++;
    if (c == '\n') {
      line.trim();
      if (line.length() > 0) {
        sendUdp(line);          // gui nguyen "X,Y,B"
        lastRx = millis();
      }
      line = "";
    } else if (c != '\r') {
      line += c;
      if (line.length() > 40) line = "";  // chong rac
    }
  }

  // Neu 1 giay khong nhan duoc gi tu Mega -> gui heartbeat de biet ESP van song
  if (millis() - lastRx > 1000 && millis() - lastHeartbeat > 1000) {
    sendUdp("NO_MEGA_DATA");
    lastHeartbeat = millis();
  }

  // Thong ke moi giay
  if (millis() - lastStat > 1000) {
    Serial.printf("UDP gui: %lu goi/s | RSSI %d dBm | byte nhan tu Mega: %lu | Mega %s\n",
                  (unsigned long)pktCount, WiFi.RSSI(), (unsigned long)rxBytes,
                  (millis() - lastRx < 1000) ? "OK" : "KHONG CO DU LIEU");
    pktCount = 0;
    rxBytes = 0;
    lastStat = millis();
  }
}
