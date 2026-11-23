// ============================================================================
// Luồng nhận diện tay riêng (Web Worker, kiểu module).
// Tài liệu MediaPipe: detect() / detectForVideo() chạy đồng bộ và CHẶN luồng giao diện, nên chạy trong web worker.
// Trước đây mỗi lần nhận diện (~20 ms) chen vào giữa các khung vẽ mô hình 3D → trang giật, nhận diện chỉ ~20 lần/giây.
// Luồng chính gửi sang ImageBitmap (đã cắt 4:3 + thu về 640 px); ở đây lật gương rồi nhận diện, trả điểm tay về.
// ============================================================================
import { FilesetResolver, HandLandmarker } from '../vendor/mediapipe/tasks-vision-1.1.0/vision_bundle.mjs';

let may = null;
let canvasSang = null, ctxSang = null;    // 16×12 điểm ảnh để đo độ sáng
let demKhung = 0, doSang = -1;
let msCuoi = 0;                           // bảo đảm timestamp MediaPipe VIDEO luôn tăng nghiêm ngặt (kể cả khi tắt rồi bật lại)

async function khoiTao({ duongWasm, duongMoHinh, tuyChon, thuTu = ['GPU', 'CPU'] }) {
  // useModule = true: trong worker kiểu module không có importScripts, phải nạp bản wasm "module"
  const fileset = await FilesetResolver.forVisionTasks(duongWasm, true);
  // Thứ tự do luồng chính gửi sang (mặc định GPU rồi CPU).
  let loiDau = null;
  for (const delegate of thuTu) {
    try {
      may = await HandLandmarker.createFromOptions(fileset, {
        baseOptions: { modelAssetPath: duongMoHinh, delegate },
        runningMode: 'VIDEO',
        ...tuyChon
      });
      // Khởi nóng shader GPU ngay lúc tạo để khung tay thật đầu tiên không bị khựng biên dịch shader
      try { msCuoi = 1; may.detectForVideo(new OffscreenCanvas(16, 16), msCuoi); } catch { /* bỏ qua nếu trình duyệt không hỗ trợ */ }
      return { uyQuyen: delegate, loiDau: loiDau && String(loiDau.message || loiDau) };
    } catch (e) {
      if (delegate === thuTu[thuTu.length - 1]) throw e;
      loiDau = e;
    }
  }
}

function doDoSang(anh) {
  if (!canvasSang) {
    canvasSang = new OffscreenCanvas(16, 12);
    ctxSang = canvasSang.getContext('2d', { willReadFrequently: true });
  }
  ctxSang.drawImage(anh, 0, 0, 16, 12);
  const d = ctxSang.getImageData(0, 0, 16, 12).data;
  let tong = 0;
  for (let i = 0; i < d.length; i += 4) tong += 0.299 * d[i] + 0.587 * d[i + 1] + 0.114 * d[i + 2];
  return Math.round(tong / (d.length / 4));
}

// Không copy qua OffscreenCanvas để lật ảnh nữa: đưa thẳng ImageBitmap vào MediaPipe (0-copy) rồi lật 21 điểm (x → 1 - x, đổi Left ↔ Right)
function nhanDien(anh, ms) {
  const ts = Math.max(ms, msCuoi + 1);
  msCuoi = ts;
  const r = may.detectForVideo(anh, ts);
  const landmarks = (r.landmarks || []).map((hand) =>
    hand.map((p) => ({ x: 1.0 - p.x, y: p.y, z: p.z }))
  );
  const worldLandmarks = (r.worldLandmarks || []).map((hand) =>
    hand.map((p) => ({ x: -p.x, y: p.y, z: p.z }))
  );
  const handedness = (r.handedness || []).map((h) =>
    h.map((c) => ({
      categoryName: c.categoryName === 'Left' ? 'Right' : c.categoryName === 'Right' ? 'Left' : c.categoryName,
      score: c.score
    }))
  );
  return { landmarks, worldLandmarks, handedness };
}

self.onmessage = async ({ data: m }) => {
  if (m.loai === 'khoi_tao') {
    try {
      self.postMessage({ loai: 'san_sang', ...(await khoiTao(m)) });
    } catch (e) {
      self.postMessage({ loai: 'loi', thongBao: String((e && e.message) || e) });
    }
  } else if (m.loai === 'khung') {
    const t0 = performance.now();
    let kq = null, loi = null;
    try {
      kq = nhanDien(m.anh, m.ms);
      if (demKhung++ % 30 === 0) doSang = doDoSang(m.anh);
    } catch (e) {
      loi = String((e && e.message) || e);
    }
    m.anh.close();
    self.postMessage({ loai: 'ket_qua', kq, loi, doSang, msNhanDien: performance.now() - t0, meta: m.meta });
  } else if (m.loai === 'dong') {
    try { may && may.close(); } catch (e) { /* đã đóng */ }
    may = null;
  }
};
