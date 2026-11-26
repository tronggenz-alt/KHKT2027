/*
 * tay_cam_vera.ino - firmware tay cầm VERA: ESP32 DevKit V1 + joystick KY-023 + BMI160 + 2 nút cảm ứng điện dung.
 * Thiết kế: docs/tay-cam-tich-hop/Tay_cam_ESP32_tich_hop.pdf. Hướng dẫn nạp: docs/28-firmware-tay-cam-vera.md.
 *
 * Arduino IDE: board "ESP32 Dev Module", gói "esp32 by Espressif Systems" 3.x. Chỉ dùng thư viện có sẵn trong gói.
 * Sửa cau_hinh.h (tên, mật khẩu Wi-Fi) trước khi nạp.
 *
 * Chia việc 2 nhân:
 *   nhân 1 - tác vụ cảm biến 200 Hz theo ngắt INT1: IMU + hợp nhất hướng; xen kẽ joystick, cảm ứng (mỗi thứ 100 Hz);
 *   nhân 0 - tác vụ mạng 100 Hz: gói UDP tới cổng 4210, nghe lệnh ở cổng 4211;
 *   loop() - lệnh qua USB, đèn LED, in số liệu ra USB.
 *
 * Lệnh (qua USB hoặc UDP; lệnh 1 chữ không cần Enter):
 *   z  đặt yaw về 0 (hoặc giữ cả 2 nút cảm ứng 1 giây)     c / C  cân con quay (để yên 1 giây)
 *   J  lấy tâm joystick ngay (buông cần)                    j      hiệu chuẩn biên joystick (xoay cần sát mép 8 giây)
 *   a  hiệu chuẩn nút A (đặt ngón tay giữ 3 giây)            b      hiệu chuẩn nút B
 *   g  hiệu chuẩn tỉ lệ con quay (xoay 5 vòng trên bàn)      k      hiệu chuẩn đầy đủ: j, a, b
 *   n  đo hệ số nhiệt con quay (để yên trên bàn 3-15 phút lúc mạch nóng lên; gửi n lần nữa để dừng sớm)
 *   t50,35,15  ngưỡng chạm, nhả, lơ lửng (%)                 s0/s1/s2 kiểu in ra USB
 *   r1 / r2 / r0  phát dữ liệu thô 200 Hz qua Wi-Fi / USB / tắt (để ghi bằng tools/ghi_tay_cam.py)
 *   h  hiệu chỉnh hướng joystick: đẩy cần LÊN (về phía mũi) giữ, buông, đẩy sang PHẢI giữ (gắn cần xoay hướng nào cũng được)
 *   p / I  in thông tin      x  xoá hiệu chuẩn đã lưu
 * Giữ nhấn cần joystick lúc cắm điện: chạy hiệu chuẩn đầy đủ (k) ngay sau khi khởi động (khi có nối CHAN_JOY_NUT).
 *
 * v3.1 (26/11/2026): 4 miếng chạm hướng GPIO32 tiến, 33 lùi, 4 trái, 15 phải (cau_hinh.h) thay cần gạt khi joystick
 * hỏng; chạm B chuyển sang GPIO27; trục joystick chỉ dùng khi đã thấy cần nằm gần tâm (kẹt 0 V / 3,3 V thì bỏ).
 * Game không cần sửa: miếng chạm gửi ra như joystick (cả điện áp giả 1650 ± 1500 mV).
 */
#include "cau_hinh.h"
#include "chung.h"
#include "imu.h"
#include "huong.h"
#include "joystick.h"
#include "cam_ung.h"
#include "mang.h"

TaskHandle_t tacVuCamBienHandle = nullptr;
volatile bool coYeuCauHieuChuanDayDu = false;
volatile bool dangDungNgat = false;  // đang lấy mẫu theo ngắt INT1 (false: đồng hồ 200 Hz)

void IRAM_ATTR ngatInt1() {
  BaseType_t w = pdFALSE;
  if (tacVuCamBienHandle) vTaskNotifyGiveFromISR(tacVuCamBienHandle, &w);
  portYIELD_FROM_ISR(w);
}

// ======================================================================
// Hiệu chuẩn (chạy xen trong tác vụ cảm biến, không chặn việc đọc)
// ======================================================================

CheDo cheDo = CD_CAN_YEN;
uint32_t batDauCheDoMs = 0;
bool canYenDayDu = true;   // lúc bật: cân con quay + mức nền cảm ứng + tâm joystick
int lanThuYen = 0;
int buocHJ = 0;            // hiệu chỉnh hướng joystick: 1 đẩy lên, 2 buông, 3 đẩy phải
int trucLen = -1, dauLen = 0, demGiu = 0;
int buocK = 0;             // hiệu chuẩn đầy đủ: 1 joystick, 2 nút A, 3 nút B
// gom số liệu
double gomG[3], gomG2[3], gomA[3], gomChamA, gomChamB, gomJoy[2], gomCham;
uint32_t soMau, soMauJoy, soMauCham;
float joyMin[2], joyMax[2];
float gocXoay = 0;
bool daXoay = false;
// đo hệ số nhiệt (lệnh n): hồi quy tuyến tính độ lệch theo nhiệt độ
double hqT, hqTT, hqB[3], hqTB[3];
uint32_t hqN = 0, lanGomNhietMs = 0, lanBaoNhietMs = 0;
float nhietMin = 1e9, nhietMax = -1e9;
uint32_t lanLuuNhietMs = 0;

void ketThucDoNhiet();

void batDauCheDo(CheDo c) {
  cheDo = c;
  batDauCheDoMs = millis();
  for (int i = 0; i < 3; i++) gomG[i] = gomG2[i] = gomA[i] = 0;
  gomChamA = gomChamB = gomJoy[0] = gomJoy[1] = gomCham = 0;
  soMau = soMauJoy = soMauCham = 0;
  joyMin[0] = joyMin[1] = 1e9;
  joyMax[0] = joyMax[1] = -1e9;
  gocXoay = 0;
  daXoay = false;
  hqT = hqTT = 0;
  for (int i = 0; i < 3; i++) hqB[i] = hqTB[i] = 0;
  hqN = 0;
  nhietMin = 1e9;
  nhietMax = -1e9;
  lanGomNhietMs = lanBaoNhietMs = millis();
  switch (c) {
    case CD_CAN_YEN: bao("CAL de yen tay cam %d giay, khong cham nut, khong cham can", THOI_GIAN_CAN_MS / 1000); break;
    case CD_CAN_NHIET:
      bao("NHIET de tay cam nam yen tren ban 3-15 phut trong luc mach nong len");
      bao("NHIET tot nhat: lam ngay sau khi bat lai mot tay cam da tat lau. Gui n lan nua de dung som");
      break;
    case CD_CAN_JOY: bao("JOY xoay can sat mep 2 vong trong 8 giay"); break;
    case CD_CAN_HUONG_JOY:
      buocHJ = 1;
      trucLen = -1;
      demGiu = 0;
      bao("HUONG_JOY buoc 1/2: day can LEN (ve phia mui tay cam) het co va giu");
      break;
    case CD_CAN_A: bao("CHAM dat ngon tay len nut A va giu yen 3 giay"); break;
    case CD_CAN_B: bao("CHAM dat ngon tay len nut B va giu yen 3 giay"); break;
    case CD_CAN_XOAY:
      bao("XOAY dat tay cam nam ngang tren ban, danh dau huong. Xoay 5 vong cung chieu (cham, deu)");
      bao("XOAY roi dat dung huong cu va de yen. Toi da 90 giay");
      break;
    default: break;
  }
}

void tiepTucChuoiK() {
  if (buocK == 0) return;
  buocK++;
  if (buocK == 2) batDauCheDo(CD_CAN_A);
  else if (buocK == 3) batDauCheDo(CD_CAN_B);
  else {
    buocK = 0;
    bao("CAL hieu chuan day du xong, da luu");
  }
}

void ketThucCheDo() {
  cheDo = CD_CHAY;
  tiepTucChuoiK();
}

// Gọi mỗi mẫu IMU. coJoy, coCham: lượt này có đọc joystick, cảm ứng không.
void xuLyHieuChuan(const float a[3], const float g[3], bool imuOk, bool coJoy, bool coCham, float nhiet, float dt) {
  uint32_t troi = millis() - batDauCheDoMs;
  switch (cheDo) {
    case CD_CAN_YEN: {
      if (imuOk) {
        for (int i = 0; i < 3; i++) {
          gomG[i] += g[i];
          gomG2[i] += (double)g[i] * g[i];
          gomA[i] += a[i];
        }
        soMau++;
      }
      if (coCham) {
        gomChamA += cham::A.tho;
        gomChamB += cham::B.tho;
        soMauCham++;
      }
      if (coJoy) {
        gomJoy[0] += joy::mv[0];
        gomJoy[1] += joy::mv[1];
        soMauJoy++;
      }
      if (troi < THOI_GIAN_CAN_MS) return;
      bool rung = false;
      float tbG[3], tbA[3];
      const uint32_t duMau = THOI_GIAN_CAN_MS / 10;  // ít nhất một nửa số mẫu
      if (soMau > duMau) {
        for (int i = 0; i < 3; i++) {
          tbG[i] = gomG[i] / soMau;
          tbA[i] = gomA[i] / soMau;
          float ps = gomG2[i] / soMau - (double)tbG[i] * tbG[i];
          if (ps > 1.0f) rung = true;  // độ lệch chuẩn > 1 °/s: tay cầm bị động
        }
      }
      if (soMau > duMau && rung && ++lanThuYen < 5) {
        bao("CAL tay cam bi rung, lam lai (%d/5)", lanThuYen);
        batDauCheDo(CD_CAN_YEN);
        return;
      }
      if (soMau > duMau && !rung) {
        huong::datLech(tbG, nhiet);
        huong::datTuGiaToc(tbA);
        for (int i = 0; i < 3; i++) cd.lechQuay[i] = tbG[i];
        cd.coLechQuay = 1;
        luuCaiDat();
        bao("CAL bias=%.3f,%.3f,%.3f do/s, nhiet %.1f C", tbG[0], tbG[1], tbG[2], nhiet);
      } else if (cd.coLechQuay) {
        huong::datLech(cd.lechQuay, nhiet);
        bao("CAL khong can duoc (rung hoac loi IMU), dung do lech da luu");
      }
      if (canYenDayDu) {
        if (soMauCham > 20) {
          cham::A.nen = gomChamA / soMauCham;
          cham::B.nen = gomChamB / soMauCham;
        }
        // chỉ nhận tâm nằm trong khoảng hợp lý: 25/11/2026 dây hỏng làm mạch học tâm 147/142 mV và 3150/3150 mV
        for (int i = 0; i < 2 && soMauJoy > 20; i++) {
          float tb = gomJoy[i] / soMauJoy;
          if (joy::ganTam(tb)) joy::tam[i] = tb;
          else bao("CAL tam joystick truc %c = %.0f mV bat thuong (day hong?), giu %.0f", i ? 'Y' : 'X', tb, joy::tam[i]);
        }
        bao("CAL nen cham A=%.0f B=%.0f, tam joystick %.0f,%.0f mV", cham::A.nen, cham::B.nen, joy::tam[0], joy::tam[1]);
      }
      lanThuYen = 0;
      canYenDayDu = false;
      cheDo = CD_CHAY;
      if (coYeuCauHieuChuanDayDu) {
        coYeuCauHieuChuanDayDu = false;
        buocK = 1;
        batDauCheDo(CD_CAN_JOY);
      }
      return;
    }

    case CD_CAN_HUONG_JOY: {
      if (!coJoy) return;
      if (troi > 30000) {
        bao("HUONG_JOY het gio, giu nguyen huong cu");
        ketThucCheDo();
        return;
      }
      // joy::loc: số đo đã trừ tâm, chuẩn hoá -1..1 theo trục của mạch joystick, CHƯA đổi hướng
      int truc = fabsf(joy::loc[0]) >= fabsf(joy::loc[1]) ? 0 : 1;
      float lech = joy::loc[truc];
      bool dayManh = fabsf(lech) > 0.6f && fabsf(joy::loc[1 - truc]) < 0.45f;
      if (buocHJ == 2) {  // chờ buông cần về giữa rồi mới sang bước đẩy phải
        if (fabsf(joy::loc[0]) < 0.2f && fabsf(joy::loc[1]) < 0.2f) {
          buocHJ = 3;
          demGiu = 0;
          bao("HUONG_JOY buoc 2/2: day can sang PHAI het co va giu");
        }
        return;
      }
      if (!dayManh) { demGiu = 0; return; }
      if (buocHJ == 3 && truc == trucLen) { demGiu = 0; return; }  // vẫn đang đẩy trục "lên": chờ đẩy sang phải
      if (++demGiu < 40) return;  // giữ 0,4 giây
      int dau = lech > 0 ? 1 : -1;
      if (buocHJ == 1) {
        trucLen = truc;
        dauLen = dau;
        buocHJ = 2;
        bao("HUONG_JOY len = truc %c %s. Buong can ve giua", truc == 0 ? 'X' : 'Y', dau > 0 ? "duong" : "am");
        return;
      }
      // y của game = trục "lên", x = trục "phải"
      joy::huong.doiXY = truc == 0 ? 0 : 1;           // x lấy từ trục 1 của mạch thì phải hoán đổi
      joy::huong.daoX = dau < 0 ? 1 : 0;
      joy::huong.daoY = dauLen < 0 ? 1 : 0;
      joy::luuHuong();
      bao("HUONG_JOY xong, da luu: doi_xy=%d dao_x=%d dao_y=%d", joy::huong.doiXY, joy::huong.daoX, joy::huong.daoY);
      buocHJ = 0;
      ketThucCheDo();
      return;
    }

    case CD_CAN_JOY: {
      if (coJoy) {
        for (int i = 0; i < 2; i++) {
          joyMin[i] = min(joyMin[i], joy::mv[i]);
          joyMax[i] = max(joyMax[i], joy::mv[i]);
        }
      }
      if (troi < 8000) return;
      float am[2], duong[2];
      bool ok = true;
      for (int i = 0; i < 2; i++) {
        am[i] = joy::tam[i] - joyMin[i] - 30;  // chừa 30 mV để gạt hết chắc chắn đạt 1,0
        duong[i] = joyMax[i] - joy::tam[i] - 30;
        if (am[i] < 600 || duong[i] < 600) ok = false;
      }
      if (ok) {
        for (int i = 0; i < 2; i++) {
          cd.joyAm[i] = am[i];
          cd.joyDuong[i] = duong[i];
        }
        luuCaiDat();
        bao("JOY bien X -%.0f/+%.0f, Y -%.0f/+%.0f mV, da luu", am[0], duong[0], am[1], duong[1]);
      } else {
        bao("JOY chua xoay du sat mep (X %.0f..%.0f, Y %.0f..%.0f mV), giu bien cu", joyMin[0], joyMax[0], joyMin[1], joyMax[1]);
      }
      ketThucCheDo();
      return;
    }

    case CD_CAN_A:
    case CD_CAN_B: {
      cham::Nut &n = (cheDo == CD_CAN_A) ? cham::A : cham::B;
      if (coCham && troi > 700) {
        gomCham += n.tho;
        soMauCham++;
      }
      if (troi < 3000) return;
      char ten = (cheDo == CD_CAN_A) ? 'A' : 'B';
      float tb = soMauCham ? gomCham / soMauCham : n.nen;
      float tut = n.nen - tb;
      if (soMauCham > 50 && fabsf(tut) > max(0.02f * n.nen, 3.0f)) {
        int8_t chieu = tut > 0 ? 1 : -1;
        float tyLe = fabsf(tut) / n.nen;
        if (cheDo == CD_CAN_A) { cd.tyLeA = tyLe; cd.chieuA = chieu; }
        else { cd.tyLeB = tyLe; cd.chieuB = chieu; }
        luuCaiDat();
        bao("CHAM %c nen=%.0f cham=%.0f (%s %.1f%%), da luu", ten, n.nen, tb, chieu > 0 ? "giam" : "tang", tyLe * 100);
      } else {
        bao("CHAM %c khong thay ngon tay (nen=%.0f, do=%.0f). Kiem tra day, la dong", ten, n.nen, tb);
      }
      ketThucCheDo();
      return;
    }

    case CD_CAN_XOAY: {
      // Cộng góc quay quanh trục đứng. Kết thúc khi đã quay > 300° rồi nằm yên.
      if (imuOk) {
        float wz = huong::tocDoDung(g);  // °/s quanh trục đứng, đã trừ độ lệch, chưa nhân tỉ lệ
        gocXoay += wz * dt;
        if (fabsf(gocXoay) > 300) daXoay = true;
      }
      if (daXoay && huong::yen && troi > 3000) {
        float k = roundf(fabsf(gocXoay) / 360.0f);
        float tiLe = k >= 1 ? 360.0f * k / fabsf(gocXoay) : 0;
        if (k >= 1 && fabsf(tiLe - 1.0f) < 0.05f) {
          cd.tiLeZ = tiLe;
          luuCaiDat();
          bao("XOAY do %.1f do = %.0f vong, ti le con quay Z = %.4f, da luu", gocXoay, k, tiLe);
        } else {
          bao("XOAY do %.1f do, khong khop so vong tron (lech > 5%%), giu ti le cu", gocXoay);
        }
        ketThucCheDo();
      } else if (troi > 90000) {
        bao("XOAY het gio, giu ti le cu");
        ketThucCheDo();
      }
      return;
    }

    case CD_CAN_NHIET: {
      // Nằm yên thì huong::lech bám theo độ lệch thật ở nhiệt độ hiện tại: mỗi giây lấy một điểm (nhiệt, độ lệch).
      if (huong::yen && millis() - lanGomNhietMs >= 1000) {
        lanGomNhietMs = millis();
        hqT += nhiet;
        hqTT += (double)nhiet * nhiet;
        for (int i = 0; i < 3; i++) {
          hqB[i] += huong::lech[i];
          hqTB[i] += (double)nhiet * huong::lech[i];
        }
        hqN++;
        nhietMin = min(nhietMin, nhiet);
        nhietMax = max(nhietMax, nhiet);
      }
      if (millis() - lanBaoNhietMs >= 15000) {
        lanBaoNhietMs = millis();
        bao("NHIET %.1f C, da tang %.1f C, %lu diem%s", nhiet, hqN ? nhietMax - nhietMin : 0.0f, (unsigned long)hqN,
            huong::yen ? "" : " (tay cam dang bi dong: dat yen lai)");
      }
      bool du = troi >= 180000 && nhietMax - nhietMin >= 3.0f;
      if (du || troi >= 900000) ketThucDoNhiet();
      return;
    }

    default:
      return;
  }
}

void ketThucDoNhiet() {
  float khoang = hqN ? nhietMax - nhietMin : 0;
  double mau = hqN * hqTT - hqT * hqT;
  if (hqN >= 60 && khoang >= 1.5f && mau > 1e-6) {
    float k[3];
    for (int i = 0; i < 3; i++) {
      k[i] = (hqN * hqTB[i] - hqT * hqB[i]) / mau;
      k[i] = constrain(k[i], -0.08f, 0.08f);
      huong::heSoNhiet[i] = k[i];
      cd.heSoNhiet[i] = k[i];
    }
    cd.coHeSoNhiet = 2;
    luuCaiDat();
    for (int i = 0; i < 3; i++) huong::lechMoc[i] = huong::lech[i];
    huong::nhietMoc = nhietMax;  // mốc: độ lệch vừa học ở nhiệt độ hiện tại
    huong::coMoc = true;
    bao("NHIET he so %.4f, %.4f, %.4f do/s moi C (khoang %.1f C, %lu diem), da luu", k[0], k[1], k[2], khoang, (unsigned long)hqN);
  } else {
    bao("NHIET chua du: nhiet do moi tang %.1f C (can >= 1.5), %lu diem yen. Giu he so cu", khoang, (unsigned long)hqN);
  }
  ketThucCheDo();
}

// ======================================================================
// Lệnh
// ======================================================================

uint32_t lanCanLaiMs = 0;

// ---------- gói dữ liệu thô (chung.h): 4 mẫu IMU + trạng thái cảm ứng, joystick, hướng firmware ----------
GoiTho goiTho;
uint8_t soMauTho = 0;
uint16_t chiSoMauTho = 0;

void ghiTho16(uint8_t *p, int32_t v) {
  if (v > 32767) v = 32767;
  if (v < -32768) v = -32768;
  p[0] = v & 0xFF;
  p[1] = (v >> 8) & 0xFF;
}

void ghiTho32(uint8_t *p, uint32_t v) {
  p[0] = v & 0xFF; p[1] = (v >> 8) & 0xFF; p[2] = (v >> 16) & 0xFF; p[3] = (v >> 24) & 0xFF;
}

// a, g: số đo đã đổi trục (g, °/s, CHƯA trừ độ lệch) để máy tính chạy lại các thuật toán trên đúng dữ liệu này
void themMauTho(const float a[3], const float g[3], uint32_t dtUs, const TrangThai &t) {
  uint8_t *b = goiTho.b;
  uint8_t *m = b + 48 + soMauTho * 14;
  for (int i = 0; i < 3; i++) ghiTho16(m + 2 * i, lroundf(g[i] * imu::GYR_LSB));
  for (int i = 0; i < 3; i++) ghiTho16(m + 6 + 2 * i, lroundf(a[i] * imu::ACC_LSB));
  ghiTho16(m + 12, (int32_t)min(dtUs, (uint32_t)65535) - 32768);
  if (++soMauTho < THO_SO_MAU) return;
  soMauTho = 0;
  // gói luôn có 4 ô mẫu: gửi 2 mẫu/gói thì ô 2, 3 chép lại ô 0, 1 (ô 3 là mẫu mới nhất, phòng thử đọc gia tốc ở đó);
  // bên nhận đọc đúng số mẫu ghi ở byte 3 nên không bị tính trùng
  for (int k = THO_SO_MAU; k < 4; k++) memcpy(b + 48 + k * 14, b + 48 + (k - THO_SO_MAU) * 14, 14);
  b[0] = 0xA5;
  b[1] = 0x5D;
  b[2] = 1;
  b[3] = THO_SO_MAU;
  b[4] = chiSoMauTho & 0xFF;
  b[5] = chiSoMauTho >> 8;
  chiSoMauTho += THO_SO_MAU;
  ghiTho32(b + 6, t.tMs);
  ghiTho16(b + 10, lroundf(t.nhietDo * 100));
  b[12] = t.bit;
  b[13] = (uint8_t)cheDo;
  for (int i = 0; i < 4; i++) ghiTho16(b + 14 + 2 * i, lroundf(t.q[i] * 16384.0f));
  ghiTho32(b + 22, t.thoA);
  ghiTho32(b + 26, t.thoB);
  ghiTho32(b + 30, (uint32_t)lroundf(t.nenA));
  ghiTho32(b + 34, (uint32_t)lroundf(t.nenB));
  ghiTho16(b + 38, lroundf(t.joyMv[0]));
  ghiTho16(b + 40, lroundf(t.joyMv[1]));
  ghiTho16(b + 42, lroundf(t.joy[0] * 32767));
  ghiTho16(b + 44, lroundf(t.joy[1] * 32767));
  b[46] = t.mucA;
  b[47] = t.mucB;
  b[104] = mang::crc8(b + 2, 102);
  xQueueSend(hangTho, &goiTho, 0);  // đầy thì bỏ (máy tính thấy chỉ số mẫu bị nhảy)
}

void inThongTin() {
  TrangThai t = docTrangThai();
  bao("INFO %s imu=%s addr=0x%02X lay_mau=%s nhiet=%.1fC hoiphuc=%lu", PHIEN_BAN, imu::loi ? "LOI" : "ok", imu::diaChi,
      dangDungNgat ? "ngat INT1" : "dong ho 200 Hz", t.nhietDo, (unsigned long)imu::soLanHoiPhuc);
  bao("YAW lech=%.3f,%.3f,%.3f do/s heso_nhiet=%.4f,%.4f,%.4f tile_z=%.4f nhiet:%s", huong::lech[0], huong::lech[1],
      huong::lech[2], huong::heSoNhiet[0], huong::heSoNhiet[1], huong::heSoNhiet[2], cd.tiLeZ,
      cd.coHeSoNhiet == 2 ? "do bang n" : (cd.coHeSoNhiet ? "tu hoc" : "chua co"));
  bao("CHAM A tho=%lu nen=%.0f tyle=%.3f chieu=%d | B tho=%lu nen=%.0f tyle=%.3f chieu=%d", (unsigned long)cham::A.tho,
      cham::A.nen, cd.tyLeA, cd.chieuA, (unsigned long)cham::B.tho, cham::B.nen, cd.tyLeB, cd.chieuB);
  bao("NGUONG bam=%.0f nha=%.0f lolung=%.0f (%%)", cd.bam * 100, cd.nha * 100, cd.loLung * 100);
  // tách 2 dòng: một dòng thông báo tối đa 99 ký tự, gộp lại thì mất phần hướng
  bao("JOY tam=%.0f,%.0f am=%.0f,%.0f duong=%.0f,%.0f mV", joy::tam[0], joy::tam[1], cd.joyAm[0], cd.joyAm[1],
      cd.joyDuong[0], cd.joyDuong[1]);
  bao("JOYH doi_xy=%d dao_x=%d dao_y=%d", joy::huong.doiXY, joy::huong.daoX, joy::huong.daoY);
  bao("JOYTOT X=%s (%.0f mV) Y=%s (%.0f mV)", joy::tot[0] ? "tot" : "bo", joy::mv[0], joy::tot[1] ? "tot" : "bo", joy::mv[1]);
  {
    const char ten[] = "TLtp";  // tiến, lùi, trái, phải
    char s[96];
    int n = snprintf(s, sizeof s, "HUONG_CHAM");
    for (int i = 0; i < cham::SO_HUONG && n < (int)sizeof s; i++) {
      if (cham::coHuong(i))
        n += snprintf(s + n, sizeof s - n, " %c=%lu/%.0f%s", ten[i], (unsigned long)cham::H[i].tho, cham::H[i].nen,
                      cham::H[i].bam ? "*" : "");
      else n += snprintf(s + n, sizeof s - n, " %c=khong", ten[i]);
    }
    bao("%s", s);
  }
  if (strcmp(WIFI_TEN, "TEN_WIFI") == 0) bao("WIFI chua dat ten trong cau_hinh.h: chi chay qua USB");
  else bao("WIFI %s %s may_tinh=%s", WIFI_TEN, wifiDaNoi ? WiFi.localIP().toString().c_str() : "chua noi",
           coMayTinh ? "co" : "chua");
}

void xuLyLenh(const char *s) {
  switch (s[0]) {
    case 'z':
      huong::datLaiYaw();
      lanCanLaiMs = millis();
      bao("YAW dat ve 0");
      break;
    case 'c':
    case 'C':
      canYenDayDu = false;
      batDauCheDo(CD_CAN_YEN);
      break;
    case 'J':
      joy::tam[0] = joy::mv[0];
      joy::tam[1] = joy::mv[1];
      bao("JOY tam=%.0f,%.0f mV", joy::tam[0], joy::tam[1]);
      break;
    case 'j': batDauCheDo(CD_CAN_JOY); break;
    case 'a': batDauCheDo(CD_CAN_A); break;
    case 'b': batDauCheDo(CD_CAN_B); break;
    case 'g': batDauCheDo(CD_CAN_XOAY); break;
    case 'n':
      if (cheDo == CD_CAN_NHIET) ketThucDoNhiet();
      else batDauCheDo(CD_CAN_NHIET);
      break;
    case 'k':
      buocK = 1;
      batDauCheDo(CD_CAN_JOY);
      break;
    case 't': {
      float b1, n1, l1;
      if (sscanf(s + 1, "%f,%f,%f", &b1, &n1, &l1) == 3 && l1 > 0 && l1 < n1 && n1 < b1 && b1 < 100) {
        cd.bam = b1 / 100;
        cd.nha = n1 / 100;
        cd.loLung = l1 / 100;
        luuCaiDat();
        bao("NGUONG bam=%.0f nha=%.0f lolung=%.0f, da luu", b1, n1, l1);
      } else bao("NGUONG sai cu phap, vi du: t50,35,15 (lolung < nha < bam < 100)");
      break;
    }
    case 's':
      if (s[1] >= '0' && s[1] <= '2') serialXuat = s[1] - '0';
      break;
    case 'r':
      if (s[1] >= '0' && s[1] <= '2') {
        guiTho = s[1] - '0';
        if (guiTho == 2) serialXuat = 0;  // USB chỉ còn gói nhị phân và dòng thông báo '#'
        soMauTho = 0;
        xQueueReset(hangTho);
        bao("THO phat du lieu tho: %s", guiTho == 1 ? "Wi-Fi" : guiTho == 2 ? "USB" : "tat");
      }
      break;
    case 'p':
    case 'I': inThongTin(); break;
    case 'h': batDauCheDo(CD_CAN_HUONG_JOY); break;
    case 'H': {  // ghi thẳng hướng joystick (tools/canh_joystick.py gửi), ví dụ H0,1,0 = doi_xy, dao_x, dao_y
      int d, x, y;
      if (sscanf(s + 1, "%d,%d,%d", &d, &x, &y) == 3 && (d | x | y) >= 0 && (d | x | y) <= 1) {
        joy::huong.doiXY = d;
        joy::huong.daoX = x;
        joy::huong.daoY = y;
        joy::luuHuong();
        bao("JOYH doi_xy=%d dao_x=%d dao_y=%d, da luu", d, x, y);
      } else bao("JOYH sai cu phap, vi du: H0,1,0 (doi_xy,dao_x,dao_y; moi so 0 hoac 1)");
      break;
    }
    case 'B': {  // ghi thẳng biên joystick theo trục mạch (mV tính từ tâm), ví dụ B1569,1462,1379,1486 = am X,Y, duong X,Y
      float a0, a1, d0, d1;
      if (sscanf(s + 1, "%f,%f,%f,%f", &a0, &a1, &d0, &d1) == 4 && min(min(a0, a1), min(d0, d1)) >= 600 &&
          max(max(a0, a1), max(d0, d1)) <= 3300) {
        cd.joyAm[0] = a0;
        cd.joyAm[1] = a1;
        cd.joyDuong[0] = d0;
        cd.joyDuong[1] = d1;
        luuCaiDat();
        bao("JOYB am=%.0f,%.0f duong=%.0f,%.0f mV, da luu", a0, a1, d0, d1);
      } else bao("JOYB sai, vi du: B1569,1462,1379,1486 (am X,Y, duong X,Y; 600..3300 mV)");
      break;
    }
    case 'x':
      cd = CaiDat();
      luuCaiDat();
      joy::xoaHuong();
      bao("Da xoa hieu chuan da luu, ve mac dinh");
      break;
    default: break;
  }
}

// ======================================================================
// Tác vụ cảm biến (nhân 1)
// ======================================================================

void tacVuCamBien(void *) {
  bool dungNgat = CHAN_INT1 >= 0;
  if (dungNgat) {
    pinMode(CHAN_INT1, INPUT);
    attachInterrupt(digitalPinToInterrupt(CHAN_INT1), ngatInt1, RISING);
  }
  uint32_t soLanTre = 0, dem = 0, lanKiemPmu = 0, lanDocOk = millis(), lanThuImu = 0;
  uint32_t lanImuTot = 0;      // lần cuối đọc được mẫu IMU (0 = chưa có), để biết vừa mất mẫu bao lâu
  bool canDatNghieng = false;  // chờ tay cầm bớt lắc để đặt lại nghiêng theo trọng lực
  uint32_t truocUs = micros();
  float nhiet = 23;
  bool coNhiet = false;
  uint32_t batDauCaHai = 0;
  bool daCanLaiLanNay = false;
  TickType_t nhip = xTaskGetTickCount();
  batDauCheDo(CD_CAN_YEN);

  for (;;) {
    // 1. Chờ mẫu mới: theo ngắt INT1, mất ngắt thì dùng đồng hồ 200 Hz
    if (dungNgat && !imu::loi) {
      if (ulTaskNotifyTake(pdTRUE, pdMS_TO_TICKS(8)) == 0) {
        if (++soLanTre >= 50) {
          dungNgat = false;
          nhip = xTaskGetTickCount();
          bao("INT1 khong co tin hieu: chuyen sang dong ho 200 Hz (kiem tra day GPIO%d)", CHAN_INT1);
        }
      } else soLanTre = 0;
    } else {
      vTaskDelayUntil(&nhip, pdMS_TO_TICKS(5));
    }
    dangDungNgat = dungNgat && !imu::loi;
    uint32_t bayGio = micros();
    uint32_t dtUs = bayGio - truocUs;
    float dt = dtUs * 1e-6f;
    truocUs = bayGio;
    if (dt <= 0 || dt > 0.05f) dt = 1.0f / imu::TAN_SO;
    dem++;

    // 2. Lệnh
    Dong lenh;
    while (xQueueReceive(hangLenh, &lenh, 0)) xuLyLenh(lenh.s);

    // 3. IMU
    float a[3] = {0, 0, 1}, g[3] = {0, 0, 0};
    bool imuOk = false;
    if (!imu::loi) {
      imuOk = imu::doc(a, g);
      if (!imuOk) imuOk = imu::doc(a, g);  // lỗi I2C lẻ: đọc lại một lần trước khi khởi động lại cảm biến (0,3 giây)
      if (imuOk) lanDocOk = millis();
      else imu::hoiPhuc();  // giữ nguyên hướng hiện tại
    } else if (millis() - lanThuImu > 2000) {  // không có IMU: 2 giây thử lại một lần (trước là 5), joystick, cảm ứng vẫn chạy
      lanThuImu = millis();
      if (imu::hoiPhuc()) {
        bao("IMU BMI160 da chay lai (dia chi 0x%02X)", imu::diaChi);
        if (CHAN_INT1 >= 0) { dungNgat = true; soLanTre = 0; }
      }
    }
    if (!imu::loi && millis() - lanKiemPmu > 1000) {
      lanKiemPmu = millis();
      if (!imu::dangChay()) imu::hoiPhuc();
      float tm;
      if (imu::docNhietDo(tm)) {
        nhiet = coNhiet ? nhiet + 0.3f * (tm - nhiet) : tm;
        coNhiet = true;
      }
    }
    if (!imu::loi && millis() - lanDocOk > 3000) {
      lanDocOk = millis();
      imu::hoiPhuc();
    }

    // 3b. Đặt lại nghiêng theo trọng lực khi hướng chưa từng được đặt (IMU lỗi lúc bật nên bước cân yên bỏ qua) hoặc
    // vừa mất mẫu quá MAT_MAU_DAT_LAI_MS (con quay không ghi được chuyển động trong lúc đó). Không đặt lại thì Madgwick
    // kéo hướng về đúng chỉ vài độ mỗi giây: tay cầm lúc đứng yên trông đúng, cầm lên di chuyển lại thấy ngược.
    if (imuOk) {
      uint32_t ms = millis();
      if (!huong::daDatHuong || (lanImuTot != 0 && ms - lanImuTot > MAT_MAU_DAT_LAI_MS)) canDatNghieng = true;
      lanImuTot = ms;
      if (cheDo == CD_CAN_YEN) canDatNghieng = false;  // bước cân yên tự đặt hướng khi xong; chưa đặt được thì lượt sau bật lại
      float an = sqrtf(a[0] * a[0] + a[1] * a[1] + a[2] * a[2]);
      if (canDatNghieng && cheDo != CD_CAN_YEN && an > 0.85f && an < 1.15f) {  // không lắc mạnh: gia tốc ≈ trọng lực
        bool lanDau = !huong::daDatHuong;
        huong::datNghiengGiuYaw(a);
        canDatNghieng = false;
        bao(lanDau ? "HUONG dat theo trong luc (luc bat chua doc duoc IMU)"
                   : "HUONG dat lai nghieng theo trong luc sau khi mat mau IMU");
      }
    }

    // 4. Joystick và cảm ứng, xen kẽ (mỗi thứ 100 Hz)
    bool coJoy = (dem & 1) == 0, coCham = !coJoy;
    if (coJoy) joy::capNhat();
    if (coCham) {
      cham::capNhat(cham::A, cd.tyLeA, cd.chieuA, 2 * dt, cheDo == CD_CAN_A);
      cham::capNhat(cham::B, cd.tyLeB, cd.chieuB, 2 * dt, cheDo == CD_CAN_B);
      cham::capNhatHuong(dem >> 1, 4 * dt);
    }
    if (joy::doiTot) {
      for (int i = 0; i < 2; i++)
        if (joy::doiTot & (1 << i))
          bao(joy::tot[i] ? "JOY truc %c on dinh, dung lai" : "JOY truc %c mat tin hieu (%.0f mV): bo qua, dung mieng cham huong",
              i ? 'Y' : 'X', joy::mv[i]);
      joy::doiTot = 0;
    }

    // 5. Hướng
    if (imuOk && cheDo != CD_CAN_YEN) huong::capNhat(a, g, dt, nhiet);
    if (cheDo != CD_CHAY) xuLyHieuChuan(a, g, imuOk, coJoy, coCham, nhiet, dt);
    if (huong::canLuuNhiet && millis() - lanLuuNhietMs > 60000) {  // ghi NVS tối đa 1 lần/phút
      huong::canLuuNhiet = false;
      lanLuuNhietMs = millis();
      for (int i = 0; i < 3; i++) cd.heSoNhiet[i] = huong::heSoNhiet[i];
      if (cd.coHeSoNhiet != 2) cd.coHeSoNhiet = 1;
      luuCaiDat();
    }

    // 6. Giữ cả 2 nút 1 giây: đặt yaw về 0 (một lần mỗi lần giữ)
    if (cheDo == CD_CHAY && cham::A.bam && cham::B.bam) {
      if (batDauCaHai == 0) batDauCaHai = millis();
      if (!daCanLaiLanNay && millis() - batDauCaHai >= 1000) {
        huong::datLaiYaw();
        lanCanLaiMs = millis();
        daCanLaiLanNay = true;
      }
    } else {
      batDauCaHai = 0;
      daCanLaiLanNay = false;
    }

    // 7. Ghi trạng thái cho tác vụ mạng và loop()
    TrangThai t;
    t.tMs = millis();
    t.q[0] = huong::q0; t.q[1] = huong::q1; t.q[2] = huong::q2; t.q[3] = huong::q3;
    for (int i = 0; i < 3; i++) {
      t.gyro[i] = imuOk ? huong::gyroRa[i] : 0;
      t.acc[i] = a[i];
    }
    // Cần gạt: miếng chạm hướng được ưu tiên, không chạm thì dùng joystick thật (trục nào tin được).
    // Miếng chạm gửi cả điện áp giả theo đúng chiều joystick thật (tiến = Y tăng, phải = X giảm, tâm 1650 mV)
    // để game đang đọc mV (Unity VeraJoyCal) chạy được ngay, không cần sửa game.
    uint8_t hc = cham::huongDangCham();
    float muc[2] = {(float)((hc >> cham::H_PHAI & 1) - (hc >> cham::H_TRAI & 1)),
                    (float)((hc >> cham::H_TIEN & 1) - (hc >> cham::H_LUI & 1))};
    if (muc[0] != 0 && muc[1] != 0) { muc[0] *= 0.7071f; muc[1] *= 0.7071f; }
    static float joyCham[2] = {0, 0};
    float buoc = CHAM_HUONG_TOC_DO * dt;
    for (int i = 0; i < 2; i++) joyCham[i] += constrain(muc[i] - joyCham[i], -buoc, buoc);
    bool dungCham = hc || fabsf(joyCham[0]) > 0.001f || fabsf(joyCham[1]) > 0.001f;
    if (dungCham) {
      t.joy[0] = joyCham[0];
      t.joy[1] = joyCham[1];
      t.joyMv[0] = joy::MV_TAM_GIA - 1500 * joyCham[0];
      t.joyMv[1] = joy::MV_TAM_GIA + 1500 * joyCham[1];
    } else {
      joy::layRa(t.joy);
      t.joyMv[0] = joy::mvRa[0];
      t.joyMv[1] = joy::mvRa[1];
    }
    t.huongCham = hc;
    t.mucA = cham::muc8(cham::A);
    t.mucB = cham::muc8(cham::B);
    uint8_t bit = 0;
    if (cham::A.bam) bit |= BIT_CHAM_A;
    if (cham::B.bam) bit |= BIT_CHAM_B;
    if (joy::nut) bit |= BIT_NHAN_CAN;
    if (huong::yen && !dungCham) bit |= BIT_DANG_YEN;  // đang chạm miếng hướng = có người cầm (game không bỏ trục)
    if (lanCanLaiMs && millis() - lanCanLaiMs < 300) bit |= BIT_VUA_CAN_LAI;
    if (cheDo != CD_CHAY) bit |= BIT_HIEU_CHUAN;
    if (imu::loi) bit |= BIT_LOI_IMU;
    t.bit = bit;
    t.nhietDo = nhiet;
    t.soLanHoiPhuc = imu::soLanHoiPhuc;
    t.thoA = cham::A.tho; t.thoB = cham::B.tho;
    t.nenA = cham::A.nen; t.nenB = cham::B.nen;
    t.joyMvTho[0] = joy::mv[0]; t.joyMvTho[1] = joy::mv[1];
    ghiTrangThai(t);
    if (guiTho && imuOk) themMauTho(a, g, dtUs, t);
  }
}

// ======================================================================
// setup / loop
// ======================================================================

void setup() {
  Serial.begin(TOC_DO_BAUD);
  pinMode(CHAN_LED, OUTPUT);
  digitalWrite(CHAN_LED, HIGH);
  delay(300);
  hangLenh = xQueueCreate(8, sizeof(Dong));
  hangBao = xQueueCreate(16, sizeof(Dong));
  hangTho = xQueueCreate(12, sizeof(GoiTho));
  napCaiDat();
  joy::napHuong();
  if (cd.coHeSoNhiet) for (int i = 0; i < 3; i++) huong::heSoNhiet[i] = cd.heSoNhiet[i];
  bao("%s khoi dong", PHIEN_BAN);

  joy::khoiTao();
  delay(20);
  coYeuCauHieuChuanDayDu = joy::dangNhanLucBat();
  if (coYeuCauHieuChuanDayDu) bao("Dang giu nhan can: se hieu chuan day du sau khi khoi dong");
  cham::khoiTao();

  imu::batDauBus();
  for (int i = 0; i < 3 && imu::loi; i++) {
    if (imu::khoiTao()) imu::loi = false;
    else delay(100);
  }
  if (imu::loi) bao("IMU khong thay BMI160 (kiem tra SDA %d, SCL %d, 3V3). Joystick va cam ung van chay", CHAN_SDA, CHAN_SCL);
  else bao("IMU BMI160 o dia chi 0x%02X: 200 Hz, 1000 do/s, 8 g", imu::diaChi);
  delay(100);  // cảm ứng đo xong vài lượt

  xTaskCreatePinnedToCore(tacVuCamBien, "cam_bien", 8192, nullptr, 5, &tacVuCamBienHandle, 1);
  if (strcmp(WIFI_TEN, "TEN_WIFI") != 0) xTaskCreatePinnedToCore(mang::tacVu, "mang", 8192, nullptr, 3, nullptr, 0);
  else bao("WIFI chua dat ten trong cau_hinh.h: chi chay qua USB");
}

void capNhatLed(const TrangThai &t) {
  uint32_t ms = millis();
  bool sang;
  if (t.bit & BIT_HIEU_CHUAN) sang = (ms / 60) % 2;                         // nháy nhanh: đang hiệu chuẩn
  else if (t.bit & BIT_LOI_IMU) { uint32_t p = ms % 1500; sang = p < 600 && (p / 100) % 2 == 0; }  // 3 chớp: lỗi IMU
  else if (strcmp(WIFI_TEN, "TEN_WIFI") == 0) sang = true;                 // chỉ chạy USB
  else if (!wifiDaNoi) sang = (ms / 500) % 2;                              // nháy chậm: đang nối Wi-Fi
  else if (!coMayTinh) sang = (ms % 1000) < 60;                            // chớp ngắn: chờ game
  else sang = true;                                                        // sáng đứng: game đang đọc
  digitalWrite(CHAN_LED, sang ? HIGH : LOW);
}

void docLenhUsb() {
  static char b[32];
  static uint8_t n = 0;
  while (Serial.available()) {
    char c = Serial.read();
    if (c == '\n' || c == '\r') {
      if (n) {
        b[n] = 0;
        guiLenh(b);
        n = 0;
      }
      continue;
    }
    if (n == 0 && strchr("zcCJjabgknpIxh", c)) {  // lệnh 1 chữ: chạy ngay, không cần Enter (tools/xem_bmi160.py gửi 'z', 'c')
      char s[2] = {c, 0};
      guiLenh(s);
      continue;
    }
    if (n < sizeof(b) - 1) b[n++] = c;
  }
}

void loop() {
  static uint32_t lanIn = 0;
  docLenhUsb();
  if (guiTho == 2) {
    GoiTho g;
    while (xQueueReceive(hangTho, &g, 0)) Serial.write(g.b, THO_DO_DAI);
  }
  TrangThai t = docTrangThai();
  capNhatLed(t);
  uint32_t ms = millis();
  if (serialXuat == 1 && ms - lanIn >= 20) {
    lanIn = ms;
    Serial.printf("Q,%.4f,%.4f,%.4f,%.4f,%lu\n", t.q[0], t.q[1], t.q[2], t.q[3], (unsigned long)t.soLanHoiPhuc);
  } else if (serialXuat == 2 && ms - lanIn >= 100) {
    lanIn = ms;
    float yaw = atan2f(2 * (t.q[0] * t.q[3] + t.q[1] * t.q[2]), 1 - 2 * (t.q[2] * t.q[2] + t.q[3] * t.q[3])) * 57.29578f;
    float pitch = asinf(constrain(2 * (t.q[0] * t.q[2] - t.q[3] * t.q[1]), -1.0f, 1.0f)) * 57.29578f;
    float roll = atan2f(2 * (t.q[0] * t.q[1] + t.q[2] * t.q[3]), 1 - 2 * (t.q[1] * t.q[1] + t.q[2] * t.q[2])) * 57.29578f;
    char h[5] = {t.huongCham & 1 ? 'T' : '.', t.huongCham & 2 ? 'L' : '.', t.huongCham & 4 ? 't' : '.',
                 t.huongCham & 8 ? 'p' : '.', 0};
    Serial.printf("J %+.2f %+.2f %c %s (%.0f,%.0f mV) | A %3u %c (%lu/%.0f) | B %3u %c (%lu/%.0f) | YPR %7.2f %6.2f %6.2f | %.1fC %s%s\n",
                  t.joy[0], t.joy[1], (t.bit & BIT_NHAN_CAN) ? 'N' : '-', h, t.joyMvTho[0], t.joyMvTho[1], t.mucA,
                  (t.bit & BIT_CHAM_A) ? '*' : ' ',
                  (unsigned long)t.thoA, t.nenA, t.mucB, (t.bit & BIT_CHAM_B) ? '*' : ' ', (unsigned long)t.thoB, t.nenB,
                  yaw, pitch, roll, t.nhietDo, (t.bit & BIT_DANG_YEN) ? "yen " : "", (t.bit & BIT_LOI_IMU) ? "LOI_IMU" : "");
  }
  delay(2);
}
