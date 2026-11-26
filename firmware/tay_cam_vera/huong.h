// huong.h - hợp nhất con quay và gia tốc kế thành hướng tay cầm (Madgwick 6 trục) và chống trôi yaw.
//
// So sánh thuật toán bằng mô phỏng 3D (tools/mo_phong_troi_yaw.py, docs/28): Madgwick và Mahony (Ki = 0) cho kết quả
// như nhau; Mahony có Ki (tự ước lượng độ lệch từ gia tốc kế) học nhầm gia tốc của tay nên ghép với các bước dưới
// thì kém hơn; ZARU (học độ lệch lúc cầm tay gần yên) học nhầm lắc lư của tay nên đã bỏ. Mặc định: Madgwick.
//
// BMI160 không có la bàn: roll, pitch được gia tốc kế giữ đúng, còn yaw chỉ là tích phân vận tốc góc quanh trục
// đứng, nên mọi sai số của con quay dồn thành trôi yaw. Các nguồn trôi và cách khử (docs/28, tools/mo_phong_troi_yaw.py):
//  1. Độ lệch (bias) lúc bật          -> cân 1 giây lúc bật (lệnh c).
//  2. Độ lệch đổi khi nằm yên trên bàn -> tự học lại khi yên > 0,5 giây, ngừng cộng dồn nên góc đứng hẳn.
//  3. Độ lệch đổi theo nhiệt độ        -> đo hệ số °/s mỗi °C bằng lệnh n (để yên trên bàn lúc mạch nóng lên), lưu NVS;
//                                        tinh chỉnh thêm giữa các lần nằm yên; lúc chuyển động cộng bù. Bước có lợi nhất.
//  5. Độ lệch dư rất nhỏ lúc gần yên   -> vùng chết mềm cho vận tốc quanh trục đứng (< 0,5 °/s bỏ, 0,5..1 °/s giảm dần).
//  6. Sai tỉ lệ con quay khi xoay nhiều -> lệnh g: xoay đủ 5 vòng trên bàn, so với 1800° để ra tỉ lệ đúng.
//  7. dt không đều                     -> lấy mẫu theo ngắt INT1, dt đo bằng micros().
//  Còn lại: giữ cả 2 nút cảm ứng 1 giây đặt yaw về 0; game dùng vận tốc góc cho tia trỏ.
#pragma once
#include <math.h>
#include "chung.h"

namespace huong {

const float DEG2RAD = 0.017453292f;
const float NGUONG_RUNG_DPS = 0.6f;  // độ dao động con quay dưới mức này là đang nằm yên
const float NGUONG_LAC_G = 0.06f;    // |a| lệch 1 g dưới mức này là không bị lắc
const float HE_SO_EMA = 0.025f;      // trung bình trượt khoảng 0,2 giây ở 200 Hz
const float TOC_DO_HOC_LECH = 0.01f; // hội tụ khoảng 0,5 giây
// vùng chết mềm quanh trục đứng
const float VUNG_CHET_DPS = 0.5f;

float q0 = 1, q1 = 0, q2 = 0, q3 = 0;
bool daDatHuong = false;             // đã đặt nghiêng theo trọng lực ít nhất một lần (IMU lỗi lúc bật thì chưa)
float lech[3] = {0, 0, 0};           // độ lệch con quay (°/s) tại nhiệt độ nhietMoc
float tb[3] = {0, 0, 0}, ps[3] = {1, 1, 1};
uint32_t demYen = 0;
bool yen = false;
uint32_t batDauYenMs = 0, batDauBetaMs = 0;
// bù nhiệt
float heSoNhiet[3] = {0, 0, 0};
float lechMoc[3] = {0, 0, 0};
float nhietMoc = 0;
bool coMoc = false;
bool canLuuNhiet = false;
float gyroRa[3] = {0, 0, 0};         // vận tốc góc đã trừ độ lệch, để gửi đi

// Véc-tơ "trục đứng của mặt đất" nhìn trong hệ tay cầm (hàng 3 của ma trận quay)
void trucDung(float ez[3]) {
  ez[0] = 2 * (q1 * q3 - q0 * q2);
  ez[1] = 2 * (q2 * q3 + q0 * q1);
  ez[2] = 1 - 2 * (q1 * q1 + q2 * q2);
}

// Vận tốc góc quanh trục đứng (°/s) của số đo thô g, đã trừ độ lệch, chưa nhân tỉ lệ (dùng cho lệnh g)
float tocDoDung(const float g[3]) {
  float ez[3];
  trucDung(ez);
  return (g[0] - lech[0]) * ez[0] + (g[1] - lech[1]) * ez[1] + (g[2] - lech[2]) * ez[2];
}

void datTuGiaToc(const float a[3]) {
  float phi = atan2f(a[1], a[2]);
  float theta = atan2f(-a[0], sqrtf(a[1] * a[1] + a[2] * a[2]));
  float cp = cosf(phi / 2), sp = sinf(phi / 2), ct = cosf(theta / 2), st = sinf(theta / 2);
  q0 = cp * ct;
  q1 = sp * ct;
  q2 = cp * st;
  q3 = -sp * st;
  batDauBetaMs = millis();
  daDatHuong = true;
}

float yawHienTai() { return atan2f(2 * (q0 * q3 + q1 * q2), 1 - 2 * (q2 * q2 + q3 * q3)); }

// q' = quay quanh trục đứng của mặt đất một góc goc (rad) ⊗ q
void quayQuanhTrucDung(float goc) {
  float c = cosf(goc / 2), s = sinf(goc / 2);
  float n0 = c * q0 - s * q3;
  float n1 = c * q1 - s * q2;
  float n2 = c * q2 + s * q1;
  float n3 = c * q3 + s * q0;
  float r = 1.0f / sqrtf(n0 * n0 + n1 * n1 + n2 * n2 + n3 * n3);
  q0 = n0 * r; q1 = n1 * r; q2 = n2 * r; q3 = n3 * r;
}

// Đặt yaw về 0, giữ nguyên roll, pitch
void datLaiYaw() { quayQuanhTrucDung(-yawHienTai()); }

// Đặt roll, pitch ngay theo trọng lực, giữ yaw đang có (gia tốc kế không biết yaw). Dùng khi IMU lỗi lúc bật rồi mới
// chạy, hoặc vừa mất mẫu một lúc: không đặt lại thì hướng trôi dần về đúng vài độ mỗi giây, cầm lên di chuyển thấy ngược.
void datNghiengGiuYaw(const float a[3]) {
  bool coYaw = daDatHuong;
  float yaw = yawHienTai();
  datTuGiaToc(a);
  if (coYaw) quayQuanhTrucDung(yaw);
}

// Mahony (Mahony 2008): sửa hướng theo tích có hướng giữa trọng lực đo và trọng lực ước lượng, Ki tích phân sai lệch
// đó thành ước lượng độ lệch con quay. Bật bằng BO_LOC_MAHONY trong cau_hinh.h để thử trên mạch thật.
float tichPhan[3] = {0, 0, 0};
void mahony(float gx, float gy, float gz, float ax, float ay, float az, float dt, bool hocTichPhan) {
  float an = sqrtf(ax * ax + ay * ay + az * az);
  if (an > 0.85f && an < 1.15f) {
    ax /= an; ay /= an; az /= an;
    float vx = 2 * (q1 * q3 - q0 * q2), vy = 2 * (q0 * q1 + q2 * q3), vz = q0 * q0 - q1 * q1 - q2 * q2 + q3 * q3;
    float ex = ay * vz - az * vy, ey = az * vx - ax * vz, ez = ax * vy - ay * vx;
    if (MAHONY_KI > 0 && hocTichPhan) {
      tichPhan[0] += MAHONY_KI * ex * dt; tichPhan[1] += MAHONY_KI * ey * dt; tichPhan[2] += MAHONY_KI * ez * dt;
    }
    gx += MAHONY_KP * ex; gy += MAHONY_KP * ey; gz += MAHONY_KP * ez;
  }
  if (hocTichPhan) { gx += tichPhan[0]; gy += tichPhan[1]; gz += tichPhan[2]; }
  float pa = q0, pb = q1, pc = q2;
  q0 += 0.5f * (-pb * gx - pc * gy - q3 * gz) * dt;
  q1 += 0.5f * (pa * gx + pc * gz - q3 * gy) * dt;
  q2 += 0.5f * (pa * gy - pb * gz + q3 * gx) * dt;
  q3 += 0.5f * (pa * gz + pb * gy - pc * gx) * dt;
  float r = 1.0f / sqrtf(q0 * q0 + q1 * q1 + q2 * q2 + q3 * q3);
  q0 *= r; q1 *= r; q2 *= r; q3 *= r;
}

void madgwick(float gx, float gy, float gz, float ax, float ay, float az, float dt, float beta) {
  float qDot1 = 0.5f * (-q1 * gx - q2 * gy - q3 * gz);
  float qDot2 = 0.5f * (q0 * gx + q2 * gz - q3 * gy);
  float qDot3 = 0.5f * (q0 * gy - q1 * gz + q3 * gx);
  float qDot4 = 0.5f * (q0 * gz + q1 * gy - q2 * gx);

  float an = sqrtf(ax * ax + ay * ay + az * az);
  if (an > 0.85f && an < 1.15f) {  // chỉ tin gia tốc kế khi không bị lắc
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

// Một lần nằm yên (> 1 giây) vừa kết thúc: so với lần trước để học độ lệch đổi theo nhiệt độ.
void ketThucYen(float nhiet) {
  if (coMoc && fabsf(nhiet - nhietMoc) >= 0.5f) {
    for (int i = 0; i < 3; i++) {
      float d = (lech[i] - lechMoc[i]) / (nhiet - nhietMoc);
      d = constrain(d, -0.05f, 0.05f);  // BMI160: tối đa khoảng ±0,05 °/s mỗi °C
      float w = cd.coHeSoNhiet == 2 ? 0.1f : 0.3f;  // đã đo bằng lệnh n: chỉ tinh chỉnh nhẹ
      heSoNhiet[i] = (1 - w) * heSoNhiet[i] + w * d;
    }
    canLuuNhiet = true;
  }
  for (int i = 0; i < 3; i++) lechMoc[i] = lech[i];
  nhietMoc = nhiet;
  coMoc = true;
}

// Gọi mỗi mẫu (200 Hz). g: °/s chưa trừ độ lệch.
void capNhat(const float a[3], const float g[3], float dt, float nhiet) {
  for (int i = 0; i < 3; i++) {
    tb[i] += HE_SO_EMA * (g[i] - tb[i]);
    float d = g[i] - tb[i];
    ps[i] += HE_SO_EMA * (d * d - ps[i]);
  }
  // Độ dao động của số đo thô: nằm yên thì số đo gần như không đổi dù độ lệch đang sai bao nhiêu.
  float aMag = sqrtf(a[0] * a[0] + a[1] * a[1] + a[2] * a[2]);
  bool yenLucNay = sqrtf(ps[0] + ps[1] + ps[2]) < NGUONG_RUNG_DPS && fabsf(aMag - 1.0f) < NGUONG_LAC_G;
  demYen = yenLucNay ? demYen + 1 : 0;
  bool yenMoi = demYen > imu::TAN_SO / 2;
  if (yenMoi && !yen) batDauYenMs = millis();
  if (!yenMoi && yen && millis() - batDauYenMs > 1000) ketThucYen(nhiet);
  yen = yenMoi;

  float c[3];
  for (int i = 0; i < 3; i++) {
    float lechDung = lech[i] + (coMoc ? heSoNhiet[i] * (nhiet - nhietMoc) : 0);
    c[i] = g[i] - lechDung;
  }
  c[2] *= cd.tiLeZ;

  if (yen) {
    // (2) nằm yên: học độ lệch, không cộng dồn con quay
    for (int i = 0; i < 3; i++) {
      lech[i] += TOC_DO_HOC_LECH * (tb[i] - lech[i]);
      c[i] = 0;
    }
  } else {
    // (5) vùng chết mềm cho vận tốc quanh trục đứng
    float ez[3];
    trucDung(ez);
    float wz = c[0] * ez[0] + c[1] * ez[1] + c[2] * ez[2];
    float aw = fabsf(wz);
    if (aw < 2 * VUNG_CHET_DPS) {
      float giu = aw <= VUNG_CHET_DPS ? 0 : (aw - VUNG_CHET_DPS) / VUNG_CHET_DPS;
      float bo = wz * (1 - giu);
      for (int i = 0; i < 3; i++) c[i] -= bo * ez[i];
    }
  }

  for (int i = 0; i < 3; i++) gyroRa[i] = c[i];
#if BO_LOC_MAHONY
  mahony(c[0] * DEG2RAD, c[1] * DEG2RAD, c[2] * DEG2RAD, a[0], a[1], a[2], dt, !yen);
#else
  float beta = (millis() - batDauBetaMs < 2000) ? 0.1f : 0.033f;
  madgwick(c[0] * DEG2RAD, c[1] * DEG2RAD, c[2] * DEG2RAD, a[0], a[1], a[2], dt, beta);
#endif
}

void datLech(const float b[3], float nhiet) {
  for (int i = 0; i < 3; i++) {
    lech[i] = b[i];
    tb[i] = b[i];
    lechMoc[i] = b[i];
  }
  nhietMoc = nhiet;
  coMoc = true;
}

}  // namespace huong
