// ============================================================================
// Luồng nhận diện tay riêng (Web Worker, kiểu module).
// Tài liệu MediaPipe: detect() / detectForVideo() chạy đồng bộ và CHẶN luồng giao diện, nên chạy trong web worker.
// Trước đây mỗi lần nhận diện (~20 ms) chen vào giữa các khung vẽ mô hình 3D → trang giật, nhận diện chỉ ~20 lần/giây.
// Luồng chính gửi sang ImageBitmap (đã cắt 4:3 + thu về 640 px); ở đây lật gương rồi nhận diện, trả điểm tay về.
// ============================================================================
import { FilesetResolver, HandLandmarker } from '../vendor/mediapipe/tasks-vision-1.1.0/vision_bundle.mjs';

let may = null;
let canvasLat = null, ctxLat = null;      // khung lật gương (giống cv2.flip(khung, 1) của bản Python)
let canvasSang = null, ctxSang = null;    // 16×12 điểm ảnh để đo độ sáng
let demKhung = 0, doSang = -1;

async function khoiTao({ duongWasm, duongMoHinh, tuyChon, thuTu = ['GPU', 'CPU'] }) {
  // useModule = true: trong worker kiểu module không có importScripts, phải nạp bản wasm "module"
  const fileset = await FilesetResolver.forVisionTasks(duongWasm, true);
  // Thứ tự do luồng chính gửi sang (mặc định GPU rồi CPU). Đo trên trang Sinh học (M2, camera giả 30 hình/giây,
  // khởi động nguội): luồng riêng + GPU giữ trang 120 hình/giây, nhận diện 17 ms/lần; CPU (WASM SIMD) 26 ms/lần.
  let loiDau = null;
  for (const delegate of thuTu) {
    try {
      may = await HandLandmarker.createFromOptions(fileset, {
        baseOptions: { modelAssetPath: duongMoHinh, delegate },
        runningMode: 'VIDEO',
        ...tuyChon
      });
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

function nhanDien(anh, ms) {
  if (!canvasLat || canvasLat.width !== anh.width || canvasLat.height !== anh.height) {
    canvasLat = new OffscreenCanvas(anh.width, anh.height);
    ctxLat = canvasLat.getContext('2d');
    ctxLat.setTransform(-1, 0, 0, 1, anh.width, 0);
  }
  ctxLat.drawImage(anh, 0, 0);
  const r = may.detectForVideo(canvasLat, ms);
  return {
    landmarks: r.landmarks,
    worldLandmarks: r.worldLandmarks,
    handedness: r.handedness.map((h) => h.map((c) => ({ categoryName: c.categoryName, score: c.score })))
  };
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
      if (demKhung++ % 10 === 0) doSang = doDoSang(m.anh);
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
