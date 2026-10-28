/**
 * frontend/hoa-hoc/phong-thi-nghiem/chon-che-do-lab.js
 * Hàm quyết định chạy bản Unity WebGL hay Three.js trong ứng dụng CHEMISTRY.
 * Trình duyệt web thường vẫn dùng Three.js; app desktop luôn dùng Unity (chỉ lùi khi thiếu bản build hoặc không có WebGL2).
 * Thiết kế thuần hàm (pure function) để test độc lập cả trong Node.js lẫn trình duyệt.
 * Hỗ trợ ES Module, CommonJS và Browser global (window.PTNChonCheDo).
 */

/**
 * Quyết định chế độ hiển thị phòng lab
 * @param {Object} thamSo
 * @param {boolean} thamSo.laApp - Có đang chạy trong ứng dụng desktop Electron không
 * @param {Object} thamSo.build - Thông tin build trả về từ desktop IPC labWebGL
 * @param {boolean} thamSo.webgl2 - Trình duyệt có hỗ trợ WebGL2 không
 * @param {string} thamSo.rendererGpu - Tên GPU / unmasked renderer
 * @param {number} [thamSo.soNhan] - Số nhân logic (navigator.hardwareConcurrency)
 * @param {number} [thamSo.ramGB] - Dung lượng RAM ước tính (navigator.deviceMemory)
 * @param {string} [thamSo.uuTien] - Lựa chọn lưu trong localStorage ("unity" | "nhe")
 * @returns {{ cheDo: "unity" | "nhe", lyDo: string }}
 */
export function chonCheDo(thamSo = {}) {
  const {
    laApp = false,
    build = null,
    coWebGL2 = false,
    webgl2 = null,
    gpu = "",
    rendererGpu = "",
    soNhan = null,
    ramGB = null,
    uuTien = null
  } = thamSo;

  const hoTroWebGL2 = (webgl2 !== null) ? webgl2 : coWebGL2;
  const tenGpu = (rendererGpu || gpu || "").toLowerCase();

  // 1. Nếu không phải ứng dụng desktop, hoặc app chưa có chức năng nhúng lab WebGL
  if (!laApp) {
    return {
      cheDo: "nhe",
      lyDo: "Chạy trên trình duyệt web thông thường, sử dụng bản nhẹ Three.js."
    };
  }

  // 2. Kiểm tra bản build có sẵn
  if (!build || !build.coSan) {
    return {
      cheDo: "nhe",
      lyDo: "Bản build Unity WebGL chưa sẵn sàng trong app."
    };
  }

  // 3. Kiểm tra WebGL2
  if (!hoTroWebGL2) {
    return {
      cheDo: "nhe",
      lyDo: "Môi trường không hỗ trợ WebGL2."
    };
  }

  // 4. Trong app desktop luôn dùng phòng lab Unity: không còn tự lùi về Three.js theo GPU / số nhân / RAM / lựa chọn cũ.
  return {
    cheDo: "unity",
    lyDo: "App CHEMISTRY luôn dùng phòng thí nghiệm Unity."
  };
}

export default {
  chonCheDo
};

if (typeof module === "object" && module.exports) {
  module.exports = { chonCheDo };
}

if (typeof self !== "undefined") {
  self.PTNChonCheDo = { chonCheDo };
  self.chonCheDo = chonCheDo;
}
