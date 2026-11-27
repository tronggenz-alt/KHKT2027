// cam_ung.h - 2 nút cảm ứng điện dung nối thẳng chân touch của ESP32 (lá đồng dán trong vỏ),
// thêm 4 miếng chạm hướng (tiến, lùi, trái, phải) từ 26/11/2026. Miếng chạm là vật dẫn bất kỳ: lá đồng, giấy bạc,
// đầu dây tuốt vỏ cuộn tròn, con vít.
//  - Trung vị 3 mẫu bỏ gai nhiễu (Wi-Fi phát, sụt áp).
//  - Mức = (mức nền - số đo) / (mức tụt khi chạm thật đã học). 0 = rảnh, 1 = chạm chắc.
//  - Ngưỡng có trễ: chạm khi mức ≥ 0,50, nhả khi < 0,35, mỗi lần đổi phải đúng 2 mẫu liên tiếp.
//  - Mức nền đi theo nhiệt, ẩm rất chậm, chỉ khi không chạm. Số đo vọt ngược chiều chạm (vừa đổi USB sang pin,
//    hoặc lúc bật đang đặt tay lên nút) quá 0,5 giây thì lấy luôn số đo đó làm mức nền.
#pragma once
#include "chung.h"

namespace cham {

struct Nut {
  uint8_t chan;
  float nen = 0;      // mức nền (không chạm)
  float muc = 0;      // mức hiện tại, 1.0 = chạm chắc
  bool bam = false;
  uint8_t demBam = 0, demNha = 0;
  uint16_t demNguoc = 0;
  uint32_t r[3] = {0, 0, 0};
  uint8_t ri = 0;
  uint32_t tho = 0;   // số đo sau trung vị
};

Nut A{CHAN_CHAM_A};
Nut B{CHAN_CHAM_B};

// Các miếng chạm ngoài A (LÙI), B (TIẾN): OK, dự phòng, TRÁI, PHẢI (cau_hinh.h). Chân -1 thành 255 = không nối, bỏ qua.
enum { H_OK, H_DU, H_TRAI, H_PHAI, SO_HUONG };
const uint8_t KHONG_NOI = 255;
Nut H[SO_HUONG] = {Nut{(uint8_t)CHAN_CHAM_OK}, Nut{(uint8_t)CHAN_CHAM_DU}, Nut{(uint8_t)CHAN_CHAM_TRAI},
                   Nut{(uint8_t)CHAN_CHAM_PHAI}};
bool coHuong(int i) { return H[i].chan != KHONG_NOI; }

void khoiTao() {
#if defined(ESP_IDF_VERSION) && ESP_IDF_VERSION >= ESP_IDF_VERSION_VAL(5, 5, 0)
  // lõi Arduino-ESP32 3.3 trở lên (driver cảm ứng mới)
  touchSetConfig(CHAM_THOI_GIAN_DO_MS, TOUCH_VOLT_LIM_L_0V5, TOUCH_VOLT_LIM_H_1V7);
#else
  touchSetCycles(0x2000, 0x1000);  // lõi cũ: đo lâu hơn mặc định cho ít nhiễu
#endif
  for (Nut *n : {&A, &B, &H[0], &H[1], &H[2], &H[3]}) {
    if (n->chan == KHONG_NOI) continue;
    uint32_t v = (uint32_t)touchRead(n->chan);
    n->r[0] = n->r[1] = n->r[2] = v;
    n->tho = v;
  }
}

uint32_t trungVi3(uint32_t a, uint32_t b, uint32_t c) {
  if (a > b) { uint32_t t = a; a = b; b = t; }
  if (b > c) b = c;
  return a > b ? a : b;
}

// Gọi 100 lần/giây
// dongBangNen: đang hiệu chuẩn nút này (lệnh a, b): giữ nguyên mức nền để đo đúng mức tụt khi chạm
void capNhat(Nut &n, float tyLe, int8_t chieu, float dt, bool dongBangNen = false) {
  n.r[n.ri] = (uint32_t)touchRead(n.chan);
  n.ri = (n.ri + 1) % 3;
  uint32_t m = trungVi3(n.r[0], n.r[1], n.r[2]);
  n.tho = m;
  if (n.nen <= 0) n.nen = m;
  float span = n.nen * tyLe;
  if (span < 1) span = 1;
  float muc = chieu * (n.nen - (float)m) / span;
  n.muc = muc;

  if (!n.bam) {
    if (muc >= cd.bam) {
      if (++n.demBam >= 2) { n.bam = true; n.demBam = 0; }
    } else n.demBam = 0;
  } else {
    if (muc < cd.nha) {
      if (++n.demNha >= 2) { n.bam = false; n.demNha = 0; }
    } else n.demNha = 0;
  }

  if (dongBangNen) return;
  if (!n.bam && muc < cd.loLung && muc > -0.3f) n.nen += ((float)m - n.nen) * (dt / CHAM_TAU_NEN_S);
  if (muc < -0.3f) {
    if (++n.demNguoc > 50) { n.nen = m; n.demNguoc = 0; }
  } else n.demNguoc = 0;
}

// Miếng OK, TRÁI, PHẢI (TIẾN, LÙI là hai nút A, B cũ). Đọc tất cả mỗi lần gọi.
void capNhatHuong(float dt) {
  for (int i = 0; i < SO_HUONG; i++)
    if (coHuong(i)) capNhat(H[i], CHAM_HUONG_TY_LE, 1, dt);
}

// bit 0..3 = tiến, lùi, trái, phải đang chạm
uint8_t huongDangCham() {
  uint8_t b = 0;
  for (int i = 0; i < SO_HUONG; i++)
    if (coHuong(i) && H[i].bam) b |= 1 << i;
  return b;
}

uint8_t muc8(const Nut &n) {
  float m = constrain(n.muc, 0.0f, 1.0f);
  return (uint8_t)(m * 255.0f + 0.5f);
}

}  // namespace cham
