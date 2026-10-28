// kiem-tra.js – chuẩn hoá + kiểm tra một quy trình (dữ liệu có sẵn hoặc do AI gửi) trước khi chạy:
// đổi tên bộ phận về id thật, bỏ bước có bộ phận không tồn tại, lọc màu, cắt chữ quá dài, kẹp thời lượng. Không đụng tới 3D / giao diện.
import { CAU_HINH } from "./config.js";
import { NHOM_TEN, TEN_KHAC } from "./data/ten-bo-phan.js";

const MAU_HEX = /^#[0-9a-fA-F]{6}$/;

function chu(x, toiDa) {
  return String(x ?? "").replace(/[\u0000-\u001f\u007f]/g, " ").replace(/\s+/g, " ").trim().slice(0, toiDa);
}

/** Đổi một tên (id, tên tiếng Anh, tên nhóm) → danh sách id bộ phận ứng viên. */
export function doiTen(ten) {
  const s = String(ten ?? "").trim().toLowerCase().replace(/[\s-]+/g, "_");
  if (!s) return [];
  if (NHOM_TEN[s]) return [...NHOM_TEN[s]];
  return [TEN_KHAC[s] || s];
}

/** Một đầu mũi tên (chuỗi hoặc mảng) → mảng id bộ phận có thật, không trùng, tối đa N. */
export function danhSachBoPhan(dau, coBoPhan) {
  const tho = Array.isArray(dau) ? dau : [dau];
  const kq = [];
  for (const t of tho) for (const id of doiTen(t)) if (coBoPhan(id) && !kq.includes(id)) kq.push(id);
  return kq.slice(0, CAU_HINH.gioiHan.boPhanMoiDau);
}

/**
 * Chuẩn hoá quy trình. `tho` = object quy trình, hoặc id quy trình có sẵn (tra trong `danhMuc`).
 * Trả { quyTrinh, canhBao } hoặc null khi không còn bước nào hợp lệ.
 * Nhận cả dạng cũ của AI: label → title, text → what.
 */
export function chuanHoaQuyTrinh(tho, { coBoPhan, danhMuc = {} }) {
  const g = CAU_HINH.gioiHan, tg = CAU_HINH.thoiGian;
  if (typeof tho === "string") tho = danhMuc[tho];
  else if (tho && typeof tho === "object" && (!Array.isArray(tho.steps) || !tho.steps.length)) {
    const pid = tho.presetId || tho.process || tho.processId || tho.id;
    if (pid && danhMuc[pid]) tho = danhMuc[pid];
  }
  if (!tho || typeof tho !== "object" || !Array.isArray(tho.steps)) return null;
  const canhBao = [];
  const steps = [];
  for (const [i, b] of tho.steps.slice(0, g.soBuoc).entries()) {
    if (!b || typeof b !== "object") continue;
    const tu = danhSachBoPhan(b.from, coBoPhan);
    const den = danhSachBoPhan(b.to ?? b.from, coBoPhan);
    if (!tu.length || !den.length) {
      canhBao.push(`Bỏ bước ${i + 1}: không tìm thấy bộ phận "${chu([].concat(b.from, b.to).join(", "), 60)}" trên mô hình.`);
      continue;
    }
    const tai_cho = tu.length === den.length && tu.every((x) => den.includes(x));
    const so = Number(b.duration);
    steps.push({
      id: steps.length + 1,
      from: tu,
      to: den,
      tai_cho,
      color: MAU_HEX.test(String(b.color || "")) ? b.color : CAU_HINH.mau.macDinh,
      luong: chu(b.luong, g.luong),
      title: chu(b.title || b.label || `Bước ${steps.length + 1}`, g.tieuDe),
      what: chu(b.what || b.text, g.what),
      why: chu(b.why, g.why),
      ghi_nho: chu(b.ghi_nho || b.key_point, g.ghiNho),
      duration: Number.isFinite(so) && so > 0 ? Math.min(tg.veToiDa, Math.max(tg.veToiThieu, so)) : tg.veMacDinh,
      aiGenerated: !!b.aiGenerated,
      keyword: chu(b.keyword, 60),
      bloodState: chu(b.bloodState, 80),
      valves: chu(b.valves, 120),
      result: chu(b.result, 160),
    });
  }
  if (!steps.length) return null;
  return {
    quyTrinh: {
      id: chu(tho.id || (tho.aiGenerated ? "ai_anh" : "ai"), 40),
      process: chu(tho.process || tho.name || "Giải thích từng bước", g.tenQuyTrinh),
      bieu_tuong: chu(tho.bieu_tuong || (tho.aiGenerated ? "🤖" : "🧭"), 4),
      tom_tat: chu(tho.tom_tat, g.what),
      aiGenerated: !!tho.aiGenerated,
      steps,
    },
    canhBao,
  };
}

