// Phòng thí nghiệm Hoá ảo – CẦU NỐI BẢN UNITY 3D (Unity WebGL). Cùng giao diện với ve-web.js:
// mo(id, tn, s) · capNhat(s) · hieuUng(e) · doiGiaoDien() · datChatLuong(q) · dieuKhienCamera(lenh) · hien(co) · huy().
// Unity KHÔNG tự tính hoá học: nhận trạng thái từ lõi mo-phong.js rồi chỉ vẽ; thao tác trong Unity gửi ngược về lõi.
//   JS → Unity: unityInstance.SendMessage("LabBridge", "NhanLenh", '{"lenh":"…","du_lieu":"<chuỗi JSON>"}')
//     lenh: mo_thi_nghiem {id, ten, trang_thai: "<JSON>"} · trang_thai "<JSON trạng thái>" · hieu_ung {loai,…} · doi_giao_dien {nen, nhan, the, chu, toi}
//           · chat_luong {q} · camera {lenh}
//   Unity → JS: Assets/Plugins/WebGL/LabBridge.jslib gọi window.PhongLabUnity.nhan('{"su_kien":"san_sang|thao_tac|chon|loi", …}')
import { bienMauWeb } from "ptn/ve-web";

const taiScript = (src) => new Promise((ok, loi) => {
  const s = document.createElement("script");
  s.src = src;
  s.async = true;
  s.onload = () => ok();
  s.onerror = () => loi(new Error(`Không tải được ${src}`));
  document.head.append(s);
});

export class PhongLabUnity {
  constructor(khung, { tep, onThaoTac = () => {}, onChon = () => {}, onTienTrinh = () => {} } = {}) {
    if (!tep?.loader) throw new Error("Chưa có bản build Unity");
    Object.assign(this, { khung, tep, onThaoTac, onChon, onTienTrinh });
    this.inst = null;
    this._choSanSang = null;
  }

  async tai() {
    const c = (this.canvas = document.createElement("canvas"));
    c.id = "ptn-unity-canvas";
    c.tabIndex = -1;
    c.setAttribute("aria-label", "Khung thí nghiệm bản Unity 3D");
    Object.assign(c.style, { position: "absolute", inset: "0", width: "100%", height: "100%", display: "block" });
    this.khung.append(c);
    const sanSang = new Promise((ok, loi) => {
      this._choSanSang = { ok, loi };
      setTimeout(() => loi(new Error("Unity không phản hồi (quá 45 giây)")), 45000);
    });
    window.PhongLabUnity = { nhan: (json) => this._nhan(json) };
    await taiScript(this.tep.loader);
    if (typeof window.createUnityInstance !== "function") throw new Error("Loader Unity không hợp lệ");
    this.inst = await window.createUnityInstance(c, {
      dataUrl: this.tep.data, frameworkUrl: this.tep.framework, codeUrl: this.tep.wasm,
      companyName: "CHEMISTRY", productName: "PhongThiNghiemHoaHoc", productVersion: "1.0",
      devicePixelRatio: Math.min(window.devicePixelRatio || 1, 1.5),
      matchWebGLToCanvasSize: true,
    }, (p) => this.onTienTrinh(p));
    await sanSang;
    this.doiGiaoDien();
  }

  _gui(lenh, duLieu) {
    if (!this.inst) return;
    const du = typeof duLieu === "string" ? duLieu : JSON.stringify(duLieu ?? {});
    try { this.inst.SendMessage("LabBridge", "NhanLenh", JSON.stringify({ lenh, du_lieu: du })); } catch (e) { console.error("[Unity]", e); }
  }
  _nhan(json) {
    let e;
    try { e = JSON.parse(json); } catch { return; }
    if (e.su_kien === "san_sang") this._choSanSang?.ok();
    else if (e.su_kien === "thao_tac") {
      let ts = {};
      try { ts = e.tham_so ? JSON.parse(e.tham_so) : {}; } catch { /* bỏ qua */ }
      this.onThaoTac(e.lenh, ts);
    } else if (e.su_kien === "chon") this.onChon({ loai: e.loai, so: Number(e.so) });
    else if (e.su_kien === "loi") console.error("[Unity]", e.thong_bao);
  }

  mo(id, tn, s) { this._gui("mo_thi_nghiem", { id, ten: tn.ten, trang_thai: JSON.stringify(s) }); }
  capNhat(s) { this._gui("trang_thai", JSON.stringify(s)); }
  hieuUng(e) { this._gui("hieu_ung", e); }
  doiGiaoDien() { this._gui("doi_giao_dien", bienMauWeb()); }
  datChatLuong(q) { this._gui("chat_luong", { q }); }
  dieuKhienCamera(lenh) { this._gui("camera", { lenh }); }
  hien(co) { if (this.canvas) this.canvas.style.display = co ? "block" : "none"; }
  async huy() {
    try { await this.inst?.Quit(); } catch { /* bỏ qua */ }
    this.canvas?.remove();
    this.inst = null;
  }
}
