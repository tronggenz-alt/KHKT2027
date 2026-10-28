/**
 * desktop/quyen_camera.js
 * Hàm kiểm tra cấp quyền camera cho app máy tính (CommonJS thuần).
 * Chỉ cấp quyền khi đủ cả 4 điều kiện:
 * 1. Loại quyền là "media"
 * 2. Chỉ có kiểu "video" (không có audio/micro)
 * 3. Cùng origin máy chủ cục bộ của app
 * 4. Đường dẫn bắt đầu bằng /sinh-hoc hoặc /static/dieu-khien-tay/
 */

function choPhepCamera({ urlYeuCau, gocMayChu, loaiQuyen, kieuMedia }) {
  if (loaiQuyen !== 'media') return false;

  // Chỉ cho phép video, không cho phép audio
  if (!Array.isArray(kieuMedia) || kieuMedia.length === 0) return false;
  const chiCoVideo = kieuMedia.every(k => k === 'video');
  if (!chiCoVideo) return false;

  try {
    const reqUrl = new URL(urlYeuCau);
    const serverUrl = new URL(gocMayChu);

    // Cùng origin máy chủ cục bộ
    if (reqUrl.origin !== serverUrl.origin) return false;

    // Đường dẫn bắt đầu bằng /sinh-hoc hoặc /static/dieu-khien-tay/
    const p = reqUrl.pathname;
    if (p.startsWith('/sinh-hoc') || p.startsWith('/static/dieu-khien-tay/')) {
      return true;
    }
    return false;
  } catch (e) {
    return false;
  }
}

module.exports = {
  choPhepCamera
};
