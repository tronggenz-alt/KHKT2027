// highlight-manager.js – HighlightManager: làm sáng bộ phận của bước, làm mờ phần còn lại; NHỚ trạng thái hiển thị trước khi mở
// chế độ (độ mờ / độ sáng từng bộ phận, bộ phận đang chọn) để trả lại đúng khi đóng. Không tự đổi material:
// trang 3D đã có hệ trạng thái (trangThaiBP) và tự chuyển material mượt mỗi khung hình, ở đây chỉ đặt mục tiêu.
export class HighlightManager {
  constructor({ datMucTieu, layTrangThai }) {
    this.datMucTieu = datMucTieu;       // (ids[], côLập) → đặt mục tiêu sáng / mờ của trang
    this.layTrangThai = layTrangThai;   // () → { bp: { doMo, sang, … } } (object sống của trang)
    this._chup = null;
  }

  batDau() {
    if (this._chup) return;
    this._chup = {};
    for (const [bp, st] of Object.entries(this.layTrangThai())) this._chup[bp] = { doMo: st.doMo, sang: st.sang };
  }

  lamNoi(ids) { this.datMucTieu(ids, ids.length > 0); }

  ketThuc() {
    if (!this._chup) return;
    const ts = this.layTrangThai();
    for (const [bp, cu] of Object.entries(this._chup)) if (ts[bp]) Object.assign(ts[bp], cu);
    this._chup = null;
  }
}
