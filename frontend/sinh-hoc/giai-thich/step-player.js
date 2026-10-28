// step-player.js – StepPlayer: bộ điều khiển trạng thái các bước (idle | playing | paused | finished).
// Chạy hoàn toàn theo delta time của vòng render chung (update(dt)) – không setTimeout, không tween riêng.
// Mỗi bước có 2 pha: "ve" (mũi tên mọc dần trong duration) → "doc" (chỉ khi Tự động chạy: dừng cho học sinh đọc rồi sang bước sau).
// Giao diện / 3D chỉ lắng nghe sự kiện: stepStart, stepEnd, finish, state.
import { CAU_HINH } from "./config.js";

export class StepPlayer {
  constructor({ giamChuyenDong = () => false } = {}) {
    this.giamChuyenDong = giamChuyenDong;
    this.state = "idle";
    this.process = null;
    this.index = -1;
    this.pha = "ve";
    this.t = 0;             // giây đã chạy trong pha hiện tại (đã nhân tốc độ)
    this.speed = 1;
    this.autoPlay = false;
    this._nghe = {};
  }

  // ---------- sự kiện ----------
  on(ten, fn) { (this._nghe[ten] ||= []).push(fn); return () => this.off(ten, fn); }
  off(ten, fn) { this._nghe[ten] = (this._nghe[ten] || []).filter((f) => f !== fn); }
  _phat(ten, ...a) { for (const f of this._nghe[ten] || []) f(...a); }
  _datState(s) { if (this.state !== s) { this.state = s; this._phat("state", s); } }

  // ---------- thông tin ----------
  get steps() { return this.process?.steps || []; }
  get step() { return this.steps[this.index] || null; }
  get laCuoi() { return this.index === this.steps.length - 1; }
  /** Thời gian vẽ mũi tên của bước hiện tại (giây, chưa chia tốc độ). Giảm chuyển động → gần như tức thì. */
  get thoiGianVe() { return this.giamChuyenDong() ? 0.001 : (this.step?.duration || CAU_HINH.thoiGian.veMacDinh); }
  /** Thời gian dừng đọc khi Tự động chạy: theo độ dài chữ, có trần. */
  get thoiGianDoc() {
    const s = this.step;
    if (!s) return 0;
    const tg = CAU_HINH.thoiGian;
    const soKyTu = (s.what || "").length + (s.why || "").length + (s.ghi_nho || "").length;
    return Math.min(tg.docToiDa, tg.docCoBan + soKyTu * tg.giayMoiKyTu);
  }
  /** Tiến độ vẽ mũi tên 0..1 */
  get progress() { return this.pha === "ve" ? Math.min(1, this.t / this.thoiGianVe) : 1; }
  /** Tiến độ thời gian đọc 0..1 (thanh đếm ngược của Tự động chạy) */
  get tienDoDoc() { return this.pha === "doc" ? Math.min(1, this.t / this.thoiGianDoc) : 0; }

  // ---------- điều khiển ----------
  load(process) {
    this.process = process;
    this.index = -1;
    this.pha = "ve";
    this.t = 0;
    this._datState("idle");
    this._phat("load", process);
  }
  unload() {
    this.process = null;
    this.index = -1;
    this.pha = "ve";
    this.t = 0;
    this._datState("idle");
  }
  play() {
    if (!this.steps.length) return;
    if (this.state === "finished" || this.index < 0) { this.goTo(0); return; }
    if (this.state === "paused" && this.pha === "xong") { this.next(); return; } // đã vẽ xong bước, đang chờ bấm → sang bước sau
    this._datState("playing");
  }
  pause() { if (this.state === "playing") this._datState("paused"); }
  toggle() { if (this.state === "playing") this.pause(); else this.play(); }
  next() {
    if (!this.steps.length) return;
    if (this.index < this.steps.length - 1) this.goTo(this.index + 1);
    else this._ketThuc();
  }
  prev() { if (this.index > 0) this.goTo(this.index - 1); }
  /** Nhảy tới bước i: huỷ ngay hoạt ảnh của bước đang chạy (chỉ là đặt lại bộ đếm), bắt đầu vẽ bước i. Bấm liên tục cũng không chồng hoạt ảnh. */
  goTo(i) {
    if (!this.steps.length) return;
    const moi = Math.max(0, Math.min(this.steps.length - 1, i | 0));
    const cu = this.index;
    this.index = moi;
    this.pha = "ve";
    this.t = 0;
    this._datState("playing");
    this._phat("stepStart", moi, this.steps[moi], cu);
  }
  setSpeed(x) { this.speed = Math.max(0.1, Number(x) || 1); this._phat("speed", this.speed); }
  setAutoPlay(bat) {
    this.autoPlay = !!bat;
    this._phat("autoPlay", this.autoPlay);
    if (this.autoPlay && this.state === "paused" && this.pha === "xong") { this.pha = "doc"; this.t = 0; this._datState("playing"); }
  }

  /** Gọi mỗi khung hình từ vòng render chung. */
  update(dt) {
    if (this.state !== "playing" || !this.step) return;
    this.t += dt * this.speed;
    if (this.pha === "ve" && this.t >= this.thoiGianVe) {
      this._phat("stepEnd", this.index, this.step);
      if (this.autoPlay) { this.pha = "doc"; this.t = 0; }
      else if (this.laCuoi) this._ketThuc();
      else { this.pha = "xong"; this._datState("paused"); }
    } else if (this.pha === "doc" && this.t >= this.thoiGianDoc) {
      this.next();
    }
  }

  _ketThuc() {
    this.pha = "xong";
    this.t = 0;
    this._datState("finished");
    this._phat("finish", this.process);
  }
}
