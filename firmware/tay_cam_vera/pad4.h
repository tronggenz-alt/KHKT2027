// pad4.h - 4 miếng chạm điều khiển tay cầm VERA (bản 3.2, 27/11/2026), thay cần gạt joystick đã hỏng.
//
//   Bốn miếng, mỗi miếng 1 ngón:  TIẾN (ngón trỏ, GPIO27)    LÙI (ngón cái, GPIO13)
//                                  TRÁI (ngón giữa, GPIO32)   PHẢI (ngón áp út, GPIO33)
//   Chạm MỘT miếng  = đi (tiến / lùi) hoặc quay (trái / phải). Chạm hai miếng khác cặp = đi chéo (vừa đi vừa quay).
//   Chạm HAI miếng ĐỐI NHAU (không thể là di chuyển) = nút chức năng, giống bóp tay:
//     TIẾN + LÙI  (trỏ + cái, "bóp")  = nút B của game: giữ = CẦM vật đang trỏ, buông = ĐẶT xuống
//     TRÁI + PHẢI (giữa + áp út)       = nút A của game: DÙNG (bấm thiết bị, nhỏ giọt, rót khi đang cầm);
//                                        GIỮ 3 GIÂY lúc tay rảnh = NÓI với trợ lý AI (xem VoiceChat trong game)
//     Cả bốn cùng lúc                  = cầm + dùng (rót, chế độ tinh)
//   Cặp đối nhau tự triệt tiêu chuyển động (tiến − lùi = 0), nên tổ hợp không làm nhân vật nhích. Cầm vật mà vẫn quay được:
//   TIẾN + LÙI + TRÁI = cầm và quay trái.
//
// Chống giật: (1) một miếng phải chạm đủ PAD_TRE_DI_CHUYEN_MS mới tính là đi: bóp hai miếng lệch nhau vài chục ms không làm
// nhân vật giật một nhịp; (2) bóp rồi nhả lệch nhau thì miếng còn lại bị bỏ qua PAD_GIAN_CACH_MS (không bị "lùi một cái").
#pragma once
#include <Arduino.h>
#include "cau_hinh.h"

namespace pad4 {

enum { TIEN, LUI, TRAI, PHAI, SO };

struct Ra {
  float x = 0, y = 0;     // -1..1 sau làm mượt: x dương = quay phải, y dương = tiến
  bool cam = false;       // bóp TIẾN + LÙI  → nút B của game
  bool dung = false;      // chạm TRÁI + PHẢI → nút A của game
  uint8_t dang = 0;       // bit 0..3: miếng nào đang chạm (TIẾN, LÙI, TRÁI, PHẢI)
};

struct Bo {
  bool in[SO] = {false, false, false, false};
  uint32_t tu[SO] = {0, 0, 0, 0};        // lúc bắt đầu chạm
  uint32_t boQua[SO] = {0, 0, 0, 0};     // bỏ qua việc đi tới lúc này (sau khi nhả một cặp)
  bool capTruoc[2] = {false, false};
  float x = 0, y = 0;
  Ra ra;

  // ms: millis(); chạm[i]: miếng i đang chạm (đã khử dội ở cam_ung.h); dt: giây từ lần gọi trước
  const Ra &capNhat(uint32_t ms, const bool cham[SO], float dt) {
    for (int i = 0; i < SO; i++) {
      if (cham[i] && !in[i]) tu[i] = ms;
      in[i] = cham[i];
    }
    const int cap[2][2] = {{TIEN, LUI}, {TRAI, PHAI}};
    bool capNay[2];
    for (int k = 0; k < 2; k++) {
      capNay[k] = in[cap[k][0]] && in[cap[k][1]];
      if (capTruoc[k] && !capNay[k])  // vừa nhả cặp: miếng còn lại (nếu còn chạm) không được hiểu là đi
        for (int j = 0; j < 2; j++)
          if (in[cap[k][j]]) boQua[cap[k][j]] = ms + PAD_GIAN_CACH_MS;
      capTruoc[k] = capNay[k];
    }
    bool di[SO];
    const int doi[SO] = {LUI, TIEN, PHAI, TRAI};   // miếng đối diện: cặp đối nhau cùng chạm là tổ hợp, không ai được đi
    for (int i = 0; i < SO; i++)
      di[i] = in[i] && !in[doi[i]] && (ms - tu[i]) >= PAD_TRE_DI_CHUYEN_MS && (int32_t)(ms - boQua[i]) >= 0;
    float my = (float)di[TIEN] - (float)di[LUI];
    float mx = (float)di[PHAI] - (float)di[TRAI];
    if (my != 0 && mx != 0) { my *= 0.7071f; mx *= 0.7071f; }
    float buoc = PAD_TOC_DO * dt;
    x += constrain(mx - x, -buoc, buoc);
    y += constrain(my - y, -buoc, buoc);
    ra.x = x;
    ra.y = y;
    ra.cam = capNay[0];
    ra.dung = capNay[1];
    ra.dang = (in[TIEN] ? 1 : 0) | (in[LUI] ? 2 : 0) | (in[TRAI] ? 4 : 0) | (in[PHAI] ? 8 : 0);
    return ra;
  }

  bool dangDung() const { return ra.dang != 0 || fabsf(x) > 0.001f || fabsf(y) > 0.001f; }
};

}  // namespace pad4
