// thu_hall.ino - thử riêng cảm biến Hall SS49E của cò tay cầm VERA, không cần firmware chính.
//
// Đấu nối (nhìn mặt có chữ của SS49E, chân chĩa xuống, từ trái sang):
//   chân 1 VCC -> 3V3 của ESP32   (KHÔNG cấp 5 V: chân OUT sẽ vượt 3,3 V của ADC)
//   chân 2 GND -> GND
//   chân 3 OUT -> GPIO32
//
// Nạp: Arduino IDE, board "ESP32 Dev Module", mở Serial Monitor 115200 baud.
// Lệnh gõ trong Serial Monitor (rồi Enter):
//   z  lấy lại mốc 0 (bỏ nam châm ra xa rồi gõ)
//   r  xóa min/max
//   p  bật/tắt kiểu in cho Serial Plotter (vẽ đồ thị)
//   c  đo hai đầu hành trình cò: nhả cò rồi bóp hết, làm theo hướng dẫn trên màn hình
// LED xanh trên board (GPIO2) sáng khi có nam châm lại gần (lệch quá NGUONG_LED mV).
//
// Nạp sketch này sẽ thay firmware tay cầm. Thử xong nhớ nạp lại firmware/tay_cam_vera.

#define CHAN_HALL   32     // ADC1, đọc được cả khi bật Wi-Fi
#define CHAN_LED    2
#define SO_MAU      32     // trung bình mỗi lần đọc để bớt nhiễu ADC
#define NGUONG_LED  80     // mV lệch khỏi mốc 0 thì bật LED

float moc0 = 0;            // mV khi không có nam châm
int mn = 99999, mx = -99999;
bool kieuPlotter = false;

int docMv() {
  long tong = 0;
  for (int i = 0; i < SO_MAU; i++) tong += analogReadMilliVolts(CHAN_HALL);
  return tong / SO_MAU;
}

float trungBinh(int ms) {
  long tong = 0; int n = 0;
  unsigned long t0 = millis();
  while (millis() - t0 < (unsigned long)ms) { tong += docMv(); n++; delay(5); }
  return n ? (float)tong / n : 0;
}

void layMoc0() {
  Serial.println("Lay moc 0: de nam cham xa cam bien, giu yen 1 giay...");
  moc0 = trungBinh(1000);
  mn = 99999; mx = -99999;
  Serial.printf("Moc 0 = %.0f mV\n", moc0);
}

// Đánh giá dây nối qua điện áp lúc không có nam châm (SS49E ra khoảng nửa VCC khi không có từ trường).
void kiemDayNoi(float mv) {
  Serial.println("---------------- KIEM TRA DAY NOI ----------------");
  if (mv < 150) {
    Serial.println("LOI: OUT gan 0 V. Kiem tra: cam bien co duoc cap 3V3 khong, dao chan VCC/GND, day OUT dut, sai chan GPIO32.");
  } else if (mv > 3000) {
    Serial.println("LOI: OUT gan 3,3 V. Kiem tra: chan OUT cham VCC, hoac nam cham dang dat sat cam bien.");
  } else if (mv < 1300 || mv > 2000) {
    Serial.printf("CANH BAO: %.0f mV, lech xa nua VCC (~1650 mV). Co the nam cham dang o gan, hoac nguon khong phai 3V3.\n", mv);
  } else {
    Serial.printf("OK: %.0f mV, gan nua VCC. Cam bien dang song. Bay gio dua nam cham lai gan.\n", mv);
  }
  Serial.println("---------------------------------------------------");
}

void doHanhTrinhCo() {
  Serial.println("\nDO HANH TRINH CO. Buoc 1: NHA co hoan toan, giu yen. Bat dau sau 2 giay...");
  delay(2000);
  float nha = trungBinh(1500);
  Serial.printf("  Nha co: %.0f mV\n", nha);
  Serial.println("Buoc 2: BOP co HET MUC va giu. Bat dau sau 3 giay...");
  delay(3000);
  float bop = trungBinh(1500);
  Serial.printf("  Bop het: %.0f mV\n", bop);
  float d = fabs(bop - nha);
  Serial.printf("  => Hanh trinh tin hieu: %.0f mV  ", d);
  if (d >= 300) Serial.println("(TOT: du rong de doc co muot)");
  else if (d >= 100) Serial.println("(DUNG DUOC, nhung nen dat nam cham gan Hall hon hoac lat mat nam cham)");
  else Serial.println("(QUA NHO: kiem tra nam cham co dung cuc, co dung vi tri doi dien Hall khong)");
  Serial.printf("  Ghi lai hai so nay de dien vao firmware: HALL_NHA = %.0f, HALL_BOP = %.0f\n\n", nha, bop);
}

void setup() {
  Serial.begin(115200);
  delay(500);
  pinMode(CHAN_LED, OUTPUT);
  analogReadResolution(12);
  analogSetPinAttenuation(CHAN_HALL, ADC_11db);   // đo được tới khoảng 3,1 V
  Serial.println("\n=== THU CAM BIEN HALL SS49E (GPIO32) ===");
  layMoc0();
  kiemDayNoi(moc0);
  Serial.println("Lenh: z = lay lai moc 0, r = xoa min/max, p = kieu Serial Plotter, c = do hanh trinh co");
}

void loop() {
  while (Serial.available()) {
    char c = Serial.read();
    if (c == 'z') { layMoc0(); kiemDayNoi(moc0); }
    else if (c == 'r') { mn = 99999; mx = -99999; Serial.println("Da xoa min/max"); }
    else if (c == 'p') { kieuPlotter = !kieuPlotter; }
    else if (c == 'c') doHanhTrinhCo();
  }

  int mv = docMv();
  int lech = mv - (int)moc0;
  if (mv < mn) mn = mv;
  if (mv > mx) mx = mv;
  digitalWrite(CHAN_LED, abs(lech) > NGUONG_LED ? HIGH : LOW);

  if (kieuPlotter) {
    Serial.printf("hall_mV:%d\tlech:%d\tmoc0:%.0f\n", mv, lech, moc0);
  } else {
    // thanh ngang: giữa là mốc 0, lệch sang phải là cực này, sang trái là cực kia
    char thanh[42];
    int vt = constrain(20 + lech / 40, 0, 40);
    for (int i = 0; i < 41; i++) thanh[i] = (i == 20) ? '|' : (i == vt ? '#' : '.');
    thanh[41] = 0;
    const char *cuc = lech > NGUONG_LED ? "cuc A" : (lech < -NGUONG_LED ? "cuc B" : "khong co tu truong");
    Serial.printf("%4d mV  lech %+5d  [%s]  min %4d max %4d  %s\n", mv, lech, thanh, mn, mx, cuc);
  }
  delay(50);   // 20 dòng mỗi giây
}
