/**
 * chon-camera.js - Chọn camera Mac (FaceTime HD / Webcam tích hợp), bỏ qua camera iPhone
 * Hỗ trợ cả Node.js (test), ES module và trình duyệt (window.DieuKhienTayChonCamera)
 */

function factory() {
  'use strict';

  var BO_QUA_REGEX = /iphone|ipad|continuity|liên tục|desk view|camera bàn/i;

  function chonCamera(dsThietBi) {
    if (!Array.isArray(dsThietBi) || dsThietBi.length === 0) return null;

    // 1. Lọc bỏ các camera nhận dạng được là iPhone / iPad / Continuity
    var hopLe = [];
    for (var i = 0; i < dsThietBi.length; i++) {
      var tb = dsThietBi[i];
      if (!tb) continue;
      var nhan = (tb.label || '').trim();
      if (BO_QUA_REGEX.test(nhan)) continue;
      hopLe.push(tb);
    }

    if (hopLe.length === 0) return null;

    // 2. Ưu tiên camera FaceTime HD
    for (var f = 0; f < hopLe.length; f++) {
      if (/facetime/i.test(hopLe[f].label || '')) {
        return hopLe[f].deviceId || null;
      }
    }

    // 3. Ưu tiên camera Built-in / Tích hợp
    for (var b = 0; b < hopLe.length; b++) {
      if (/built-in|tích hợp/i.test(hopLe[b].label || '')) {
        return hopLe[b].deviceId || null;
      }
    }

    // 4. Lấy thiết bị hợp lệ đầu tiên
    return hopLe[0].deviceId || null;
  }

  return {
    chonCamera: chonCamera,
    BO_QUA_REGEX: BO_QUA_REGEX
  };
}

const _chonCam = factory();
if (typeof self !== 'undefined') {
  self.DieuKhienTayChonCamera = _chonCam;
}
if (typeof module !== 'undefined' && module.exports) {
  module.exports = _chonCam;
}

export default _chonCam;
export const chonCamera = _chonCam.chonCamera;
export const BO_QUA_REGEX = _chonCam.BO_QUA_REGEX;
