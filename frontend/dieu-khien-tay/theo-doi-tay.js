import { FilesetResolver, HandLandmarker } from '../vendor/mediapipe/tasks-vision-1.1.0/vision_bundle.mjs';
import cuChiModule from './cu-chi.js';
import chonCamModule from './chon-camera.js';
import { RONG_NHAN_DIEN, tinhVungCat, doiVeKhungDay, kichThuocTay, ThongKeTay, goiYNhanDien } from './khung-hinh.js';

const { docTay, raCuaSo, HANG_SO } = cuChiModule;
const { chonCamera } = chonCamModule;

// Ngưỡng tin cậy để mặc định của MediaPipe (0,5): 0,6 làm rớt tay khi hơi tối / tay nghiêng / chuyển động nhanh.
// numHands 2 vì cần hai tay để phóng to; khi chỉ thấy 1 tay MediaPipe phải dò lòng bàn tay mỗi khung
// nên việc nhận diện chạy ở luồng riêng (nhan-dien-worker.js) để không chặn việc vẽ 3D.
const TUY_CHON = { numHands: 2, minHandDetectionConfidence: 0.5, minHandPresenceConfidence: 0.5, minTrackingConfidence: 0.5 };

// Công tắc ẩn để so cách chạy nhận diện trên máy thật: localStorage 'dkt-nhan-dien' =
// '' (mặc định: luồng riêng, GPU trước) | 'rieng-cpu' (luồng riêng, CPU trước) | 'chinh' (luồng chính, GPU trước – như bản đầu).
// Đo trên M2, khởi động nguội: luồng riêng + GPU giữ trang 120 hình/giây (17 ms/lần nhận diện); luồng chính tụt ~107
// trong 5 giây đầu (10 ms/lần); luồng riêng + CPU 26 ms/lần.
function cheDoNhanDien() {
  try { return localStorage.getItem('dkt-nhan-dien') || ''; } catch (e) { return ''; }
}

// Các đoạn nối 21 điểm thành khung bàn tay
const NOI = [
  [0, 1], [1, 2], [2, 3], [3, 4],
  [0, 5], [5, 6], [6, 7], [7, 8],
  [5, 9], [9, 10], [10, 11], [11, 12],
  [9, 13], [13, 14], [14, 15], [15, 16],
  [13, 17], [0, 17], [17, 18], [18, 19], [19, 20]
];
const DAU_NGON = [4, 8, 12, 16, 20];

// Tải CSS tự động nếu chưa có
function damBaoCSS() {
  if (typeof document === 'undefined') return;
  const href = new URL('./dieu-khien-tay.css', import.meta.url).href;
  if (!document.querySelector(`link[href="${href}"]`)) {
    const link = document.createElement('link');
    link.rel = 'stylesheet';
    link.href = href;
    document.head.appendChild(link);
  }
}

function doiKhungSangMediaPipe(dsTay) {
  const hand_landmarks = [];
  const hand_world_landmarks = [];
  const handedness = [];

  for (const h of dsTay) {
    hand_landmarks.push(h.diem.map(p => ({
      x: Array.isArray(p) ? p[0] : p.x,
      y: Array.isArray(p) ? p[1] : p.y,
      z: Array.isArray(p) ? p[2] : (p.z || 0)
    })));
    if (h.theGioi) {
      hand_world_landmarks.push(h.theGioi.map(p => ({
        x: Array.isArray(p) ? p[0] : p.x,
        y: Array.isArray(p) ? p[1] : p.y,
        z: Array.isArray(p) ? p[2] : (p.z || 0)
      })));
    }
    const tenBen = h.ben || 'Right';
    handedness.push([{ categoryName: tenBen, category_name: tenBen }]);
  }

  return { hand_landmarks, hand_world_landmarks, handedness };
}

export async function taoDieuKhienTay({
  khung3D,
  vungBang = khung3D,
  dieuKhien,
  nguon = 'camera',
  tanSoToiDa = 30
}) {
  damBaoCSS();

  let stream = null;
  let videoEl = null;
  let procCanvas = null;
  let procCtx = null;
  let handLandmarker = null;
  let animFrameId = null;
  let rvfcId = null;
  let dangChay = false;
  let tBatDau = 0;
  let tTruoc = 0;            // giây kể từ lúc bật, của khung vừa nhận diện
  let msTruoc = -1;
  let msNhanDien = 0;
  let soTayHienTai = 0;
  let cameraTen = '';
  let thongBaoLoi = null;
  let dangTai = false;
  let uyQuyen = false;
  let nguonHienTai = nguon;
  let worker = null;
  let luong = '';            // 'worker' (luồng riêng) hoặc 'chinh' (luồng chính, khi trình duyệt không chạy được worker)
  let dangCho = false;       // đang chờ kết quả một khung → khung camera mới tới thì bỏ qua, không xếp hàng (đỡ trễ)
  let doSang = -1;
  let dsTayCuoi = [];
  let goiYHienTai = null;
  let thongKe = new ThongKeTay();

  const cacTay = {};

  // --------------------------------------------------------------------------
  // Dựng DOM giao diện (chỉ tạo 1 lần)
  // --------------------------------------------------------------------------
  const lopConTro = document.createElement('div');
  lopConTro.className = 'dkt-lop-con-tro';
  khung3D.appendChild(lopConTro);

  const duongZoomSvg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
  duongZoomSvg.setAttribute('class', 'dkt-duong-zoom');
  duongZoomSvg.style.display = 'none';
  const duongZoomLine = document.createElementNS('http://www.w3.org/2000/svg', 'line');
  duongZoomSvg.appendChild(duongZoomLine);
  lopConTro.appendChild(duongZoomSvg);

  const conTro1 = taoPhanTuConTro();
  const conTro2 = taoPhanTuConTro();
  lopConTro.appendChild(conTro1.el);
  lopConTro.appendChild(conTro2.el);

  // Vòng tròn tiến độ khi xoè 2 tay
  const vongTienDoWrap = document.createElement('div');
  vongTienDoWrap.className = 'dkt-vong-tien-do-wrap';
  vongTienDoWrap.innerHTML = `
    <svg class="dkt-vong-tien-do-svg" viewBox="0 0 48 48">
      <circle cx="24" cy="24" r="20" stroke="rgba(255,255,255,0.2)" stroke-width="4" fill="none"/>
      <circle class="dkt-vong-tien-do-cung" cx="24" cy="24" r="20" stroke="#22c55e" stroke-width="4" fill="none"
              stroke-dasharray="125.66" stroke-dashoffset="125.66" stroke-linecap="round"/>
    </svg>
  `;
  khung3D.appendChild(vongTienDoWrap);
  const cungTienDo = vongTienDoWrap.querySelector('.dkt-vong-tien-do-cung');

  // Bảng camera góc dưới
  const bangCam = document.createElement('div');
  bangCam.className = 'dkt-bang-cam';
  bangCam.innerHTML = `
    <div class="dkt-dau-bang">
      <span class="dkt-tieu-de">
        <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <path d="M18 11V6a2 2 0 0 0-2-2v0a2 2 0 0 0-2 2v0"/>
          <path d="M14 10V4a2 2 0 0 0-2-2v0a2 2 0 0 0-2 2v6"/>
          <path d="M10 10.5V6a2 2 0 0 0-2-2v0a2 2 0 0 0-2 2v8"/>
          <path d="M18 8a2 2 0 1 1 4 0v6a8 8 0 0 1-8 8h-2c-2.8 0-4.5-.86-5.99-2.34l-3.6-3.6a2 2 0 0 1 2.83-2.82L7 15"/>
        </svg>
        Điều khiển bằng tay
      </span>
      <div class="dkt-nut-nhom">
        <button class="dkt-nut-icon dkt-nut-thu-gon" title="Thu gọn/Mở rộng" aria-label="Thu gọn">
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <polyline points="6 9 12 15 18 9"></polyline>
          </svg>
        </button>
        <button class="dkt-nut-icon dkt-nut-dong" title="Tắt điều khiển tay" aria-label="Đóng">
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <line x1="18" y1="6" x2="6" y2="18"></line>
            <line x1="6" y1="6" x2="18" y2="18"></line>
          </svg>
        </button>
      </div>
    </div>
    <div class="dkt-khung-hinh">
      <canvas class="dkt-canvas-tay"></canvas>
    </div>
    <div class="dkt-chip-container">
      <span class="dkt-chip dkt-chip-cho">Chờ</span>
      <span class="dkt-thong-so-hud"></span>
    </div>
    <div class="dkt-thong-so-chi-tiet"></div>
    <div class="dkt-goi-y" style="display:none"></div>
    <div class="dkt-thong-bao-loi" style="display:none"></div>
    <div class="dkt-huong-dan-wrap">
      <ul class="dkt-huong-dan-list">
        <li class="dkt-huong-dan-item"><strong>Chụm + kéo:</strong> Xoay mô hình (ngang / dọc)</li>
        <li class="dkt-huong-dan-item"><strong>Kéo nhanh rồi thả:</strong> Lướt tiếp, chụm lại để dừng</li>
        <li class="dkt-huong-dan-item"><strong>Hai tay chụm kéo:</strong> Phóng to / thu nhỏ</li>
        <li class="dkt-huong-dan-item"><strong>Xoè 2 tay 1 giây:</strong> Về góc nhìn ban đầu</li>
        <li class="dkt-huong-dan-item"><strong>Chạm nhẹ:</strong> Chọn cơ quan</li>
      </ul>
      <div class="dkt-dong-quyen-rieng-tu">
        🔒 Hình camera chỉ xử lý trên máy này, không gửi đi, không lưu.
      </div>
    </div>
  `;
  vungBang.appendChild(bangCam);

  const canvasBang = bangCam.querySelector('.dkt-canvas-tay');
  const ctxBang = canvasBang.getContext('2d');
  const chipTrangThai = bangCam.querySelector('.dkt-chip');
  const hudText = bangCam.querySelector('.dkt-thong-so-hud');
  const hudChiTiet = bangCam.querySelector('.dkt-thong-so-chi-tiet');
  const hopGoiY = bangCam.querySelector('.dkt-goi-y');
  const hopThongBaoLoi = bangCam.querySelector('.dkt-thong-bao-loi');
  const nutThuGon = bangCam.querySelector('.dkt-nut-thu-gon');
  const nutDong = bangCam.querySelector('.dkt-nut-dong');

  nutThuGon.addEventListener('click', () => {
    bangCam.classList.toggle('thu-gon');
  });

  nutDong.addEventListener('click', () => {
    tat();
  });

  function taoPhanTuConTro() {
    const el = document.createElement('div');
    el.className = 'dkt-con-tro';
    el.style.display = 'none';
    el.innerHTML = `
      <svg viewBox="0 0 44 44">
        <circle class="dkt-ct-ngoai" cx="22" cy="22" r="18" fill="none" stroke="rgba(255,255,255,0.85)" stroke-width="2"/>
        <circle class="dkt-ct-cung" cx="22" cy="22" r="18" fill="none" stroke="#22c55e" stroke-width="3"
                stroke-dasharray="113.1" stroke-dashoffset="113.1" stroke-linecap="round" transform="rotate(-90 22 22)"/>
        <circle class="dkt-ct-tam" cx="22" cy="22" r="8" fill="#f97316" style="display:none"/>
      </svg>
    `;
    return {
      el,
      ngoai: el.querySelector('.dkt-ct-ngoai'),
      cung: el.querySelector('.dkt-ct-cung'),
      tam: el.querySelector('.dkt-ct-tam')
    };
  }

  function capNhatConTro(pt, x, y, cham, tl, cheDo) {
    if (x === null) {
      pt.el.style.display = 'none';
      return;
    }
    pt.el.style.display = 'block';
    pt.el.style.left = x + 'px';
    pt.el.style.top = y + 'px';

    const laZoom = (cheDo === 'zoom');
    const mauChinh = laZoom ? '#3b82f6' : '#f97316';

    if (cham) {
      pt.ngoai.setAttribute('stroke', mauChinh);
      pt.ngoai.setAttribute('stroke-width', '2.5');
      pt.cung.style.display = 'none';
      pt.tam.style.display = 'block';
      pt.tam.setAttribute('fill', mauChinh);
    } else {
      pt.ngoai.setAttribute('stroke', 'rgba(255,255,255,0.85)');
      pt.ngoai.setAttribute('stroke-width', '1.5');
      pt.cung.style.display = 'block';
      pt.cung.setAttribute('stroke', '#22c55e');
      pt.tam.style.display = 'none';

      // Tính tỉ lệ đầy
      const doChum = Math.min(1.0, Math.max(0.0, (0.7 - tl) / (0.7 - HANG_SO.CHAM_BAT)));
      const chuVi = 2 * Math.PI * 18; // 113.097
      pt.cung.setAttribute('stroke-dashoffset', String(chuVi * (1.0 - doChum)));
    }
  }

  function hienLoi(cauBao) {
    thongBaoLoi = cauBao;
    hopThongBaoLoi.textContent = cauBao;
    hopThongBaoLoi.style.display = 'block';
    chipTrangThai.className = 'dkt-chip dkt-chip-loi';
    chipTrangThai.textContent = 'Lỗi';
  }

  function anLoi() {
    thongBaoLoi = null;
    hopThongBaoLoi.style.display = 'none';
  }

  function capNhatChip(cheDo, veDau) {
    if (thongBaoLoi) return;
    if (dangTai) {
      chipTrangThai.className = 'dkt-chip dkt-chip-cho';
      chipTrangThai.textContent = 'Đang khởi động…';
      return;
    }

    if (veDau) {
      chipTrangThai.className = 'dkt-chip dkt-chip-hoat-dong';
      chipTrangThai.textContent = 'Về góc đầu';
    } else if (cheDo === 'keo') {
      chipTrangThai.className = 'dkt-chip dkt-chip-hoat-dong';
      chipTrangThai.textContent = 'Đang xoay';
    } else if (cheDo === 'quan_tinh') {
      chipTrangThai.className = 'dkt-chip dkt-chip-luot';
      chipTrangThai.textContent = 'Đang lướt';
    } else if (cheDo === 'zoom') {
      chipTrangThai.className = 'dkt-chip dkt-chip-zoom';
      chipTrangThai.textContent = 'Phóng to / thu nhỏ';
    } else {
      chipTrangThai.className = 'dkt-chip dkt-chip-cho';
      chipTrangThai.textContent = 'Chờ';
    }
  }

  // --------------------------------------------------------------------------
  // Khởi tạo MediaPipe Landmarker
  // --------------------------------------------------------------------------
  const duongWasm = new URL('../vendor/mediapipe/tasks-vision-1.1.0/wasm', import.meta.url).href;
  const duongMoHinh = new URL('../vendor/mediapipe/hand_landmarker.task', import.meta.url).href;

  // Ưu tiên luồng riêng (Web Worker); trình duyệt nào không chạy được thì nhận diện ngay trên luồng chính như trước.
  async function khoiTaoNhanDien() {
    if (worker || handLandmarker) return;
    const cheDo = cheDoNhanDien();
    if (cheDo !== 'chinh') {
      try {
        await khoiTaoWorker(cheDo);
        return;
      } catch (eWorker) {
        console.warn('Không chạy được luồng nhận diện riêng, nhận diện trên luồng chính:', eWorker);
      }
    }

    const fileset = await FilesetResolver.forVisionTasks(duongWasm);
    for (const delegate of ['GPU', 'CPU']) {
      try {
        handLandmarker = await HandLandmarker.createFromOptions(fileset, {
          baseOptions: { modelAssetPath: duongMoHinh, delegate },
          runningMode: 'VIDEO',
          ...TUY_CHON
        });
        uyQuyenMay = delegate;
        break;
      } catch (e) {
        if (delegate === 'CPU') throw e;
        console.warn('MediaPipe GPU không khả dụng, chuyển sang CPU delegate:', e);
      }
    }
    luong = 'chinh';
  }

  async function khoiTaoWorker(cheDo) {
    const w = new Worker(new URL('./nhan-dien-worker.js' + new URL(import.meta.url).search, import.meta.url), { type: 'module' });
    const ketQua = await new Promise((xong, hong) => {
      const hetGio = setTimeout(() => hong(new Error('Luồng nhận diện khởi động quá lâu')), 30000);
      w.onmessage = ({ data }) => {
        if (data.loai === 'san_sang') { clearTimeout(hetGio); xong(data); }
        else if (data.loai === 'loi') { clearTimeout(hetGio); hong(new Error(data.thongBao)); }
      };
      w.onerror = (e) => { clearTimeout(hetGio); hong(new Error(e.message || 'Lỗi luồng nhận diện')); };
      w.postMessage({ loai: 'khoi_tao', duongWasm, duongMoHinh, tuyChon: TUY_CHON,
                      thuTu: cheDo === 'rieng-cpu' ? ['CPU', 'GPU'] : ['GPU', 'CPU'] });
    }).catch((e) => { w.terminate(); throw e; });
    if (ketQua.loiDau) console.warn('Luồng nhận diện: cách chạy đầu tiên lỗi, đã chuyển cách khác:', ketQua.loiDau);
    w.onmessage = ({ data }) => { if (data.loai === 'ket_qua') nhanKetQua(data); };
    w.onerror = (e) => console.warn('Lỗi luồng nhận diện:', e.message);
    worker = w;
    luong = 'worker';
    uyQuyenMay = ketQua.uyQuyen;
  }
  let uyQuyenMay = '';

  // --------------------------------------------------------------------------
  // Mở camera (FaceTime HD / Mac, bỏ iPhone)
  // --------------------------------------------------------------------------
  async function moCamera() {
    if (location.protocol === 'file:') {
      throw new Error('file:');
    }
    if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
      throw new Error('NotFoundError');
    }

    let dsThietBi = await navigator.mediaDevices.enumerateDevices();
    let videoInputs = dsThietBi.filter(d => d.kind === 'videoinput');

    // Nếu nhãn rỗng do chưa xin quyền, gọi getUserMedia 1 lần để xin quyền
    const coNhan = videoInputs.some(d => d.label && d.label.length > 0);
    if (!coNhan) {
      try {
        const streamTam = await navigator.mediaDevices.getUserMedia({ video: true, audio: false });
        streamTam.getTracks().forEach(tr => tr.stop());
        dsThietBi = await navigator.mediaDevices.enumerateDevices();
        videoInputs = dsThietBi.filter(d => d.kind === 'videoinput');
      } catch (ePerm) {
        throw ePerm;
      }
    }

    const deviceId = chonCamera(videoInputs);
    if (!deviceId) {
      throw new Error('ChiCoCameraDienThoai');
    }

    const matched = videoInputs.find(d => d.deviceId === deviceId);
    cameraTen = matched ? (matched.label || 'Camera Mac') : 'Camera Mac';

    stream = await navigator.mediaDevices.getUserMedia({
      video: {
        deviceId: { exact: deviceId },
        width: { ideal: 1280 },
        height: { ideal: 720 },
        frameRate: { ideal: 30 }
      },
      audio: false
    });

    uyQuyen = true;
    return stream;
  }

  // --------------------------------------------------------------------------
  // Vòng lặp nhận diện
  // --------------------------------------------------------------------------
  function veKhungXuong(ctx, diem, cham, w, h) {
    const px = diem.map(d => ({
      x: (Array.isArray(d) ? d[0] : d.x) * w,
      y: (Array.isArray(d) ? d[1] : d.y) * h
    }));

    const mau = cham ? '#f97316' : '#22c55e';
    ctx.lineWidth = 2;
    ctx.strokeStyle = mau;

    for (const [a, b] of NOI) {
      if (px[a] && px[b]) {
        ctx.beginPath();
        ctx.moveTo(px[a].x, px[a].y);
        ctx.lineTo(px[b].x, px[b].y);
        ctx.stroke();
      }
    }

    for (let i = 0; i < px.length; i++) {
      const p = px[i];
      if (!p) continue;
      const laDau = DAU_NGON.includes(i);
      ctx.beginPath();
      ctx.arc(p.x, p.y, laDau ? 4.5 : 2.5, 0, 2 * Math.PI);
      ctx.fillStyle = laDau ? mau : '#ffffff';
      ctx.fill();
    }
  }

  // Bảng camera nhỏ: vẽ mỗi khung camera (không chờ nhận diện) + khung xương của lần nhận diện gần nhất.
  function vePiP() {
    const vw = videoEl.videoWidth || 640, vh = videoEl.videoHeight || 480;
    const bw = 320, bh = Math.round(320 * vh / vw);
    if (canvasBang.width !== bw || canvasBang.height !== bh) {
      canvasBang.width = bw;
      canvasBang.height = bh;
    }
    ctxBang.setTransform(-1, 0, 0, 1, bw, 0);
    ctxBang.drawImage(videoEl, 0, 0, bw, bh);
    ctxBang.setTransform(1, 0, 0, 1, 0, 0);
    for (const h of dsTayCuoi) veKhungXuong(ctxBang, h.diem, h.cham, bw, bh);
  }

  // Mỗi khung camera: vẽ bảng camera, rồi gửi khung đi nhận diện nếu lần trước đã xong (không xếp hàng → không trễ dồn).
  function xuLyKhungHinh() {
    if (!dangChay) return;
    tiepTucVongLap();
    if (!videoEl || videoEl.readyState < 2) return;
    const bay = performance.now();
    thongKe.ghiKhungCamera(bay / 1000);
    vePiP();
    if (dangCho || document.hidden) return;
    guiKhung(bay);
  }

  function guiKhung(tChup) {
    const vw = videoEl.videoWidth || 640, vh = videoEl.videoHeight || 480;
    const cat = tinhVungCat(vw, vh);
    const caoNhanDien = Math.round(RONG_NHAN_DIEN * cat.sh / cat.sw);
    const ms = Math.max(Math.round(tChup - tBatDau), msTruoc + 1);
    msTruoc = ms;
    const meta = { tChup, cat, vw, vh };
    dangCho = true;

    if (luong === 'worker') {
      createImageBitmap(videoEl, cat.sx, cat.sy, cat.sw, cat.sh,
        { resizeWidth: RONG_NHAN_DIEN, resizeHeight: caoNhanDien, resizeQuality: 'low' })
        .then((anh) => {
          if (!dangChay || !worker) { anh.close(); dangCho = false; return; }
          worker.postMessage({ loai: 'khung', anh, ms, meta }, [anh]);
        })
        .catch((e) => { dangCho = false; console.warn('Không chụp được khung camera:', e); });
      return;
    }

    // Dự phòng trên luồng chính: cắt 4:3 + lật gương lên canvas rồi nhận diện ngay
    if (procCanvas.width !== RONG_NHAN_DIEN || procCanvas.height !== caoNhanDien) {
      procCanvas.width = RONG_NHAN_DIEN;
      procCanvas.height = caoNhanDien;
    }
    const t0 = performance.now();
    procCtx.setTransform(-1, 0, 0, 1, RONG_NHAN_DIEN, 0);
    procCtx.drawImage(videoEl, cat.sx, cat.sy, cat.sw, cat.sh, 0, 0, RONG_NHAN_DIEN, caoNhanDien);
    procCtx.setTransform(1, 0, 0, 1, 0, 0);
    let kq = null, loi = null;
    try {
      const r = handLandmarker.detectForVideo(procCanvas, ms);
      kq = { landmarks: r.landmarks, worldLandmarks: r.worldLandmarks, handedness: r.handedness };
    } catch (e) {
      loi = String((e && e.message) || e);
    }
    nhanKetQua({ kq, loi, doSang: -1, msNhanDien: performance.now() - t0, meta });
  }

  function nhanKetQua({ kq, loi, doSang: sang, msNhanDien: msND, meta }) {
    dangCho = false;
    if (!dangChay) return;
    if (loi) console.warn('Lỗi nhận diện:', loi);
    if (sang >= 0) doSang = sang;
    msNhanDien = msND;

    const { tChup, cat, vw, vh } = meta;
    const tGiay = (tChup - tBatDau) / 1000;
    const dt = Math.min(0.1, Math.max(0.001, tGiay - tTruoc));
    tTruoc = tGiay;
    const tiLe = vw / vh;
    const dsTay = kq ? docTay(doiVeKhungDay(kq, cat, vw, vh), tiLe, tGiay, cacTay) : [];
    soTayHienTai = dsTay.length;
    dsTayCuoi = dsTay;
    thongKe.ghiNhanDien(tGiay, dsTay.length);

    // Cập nhật bộ điều khiển cử chỉ
    dieuKhien.capNhat(tGiay, dt, dsTay);

    // Cập nhật con trỏ trên khung 3D
    const rect = khung3D.getBoundingClientRect();
    const krong = rect.width;
    const kcao = rect.height;

    const conTroDiem = [];
    for (let i = 0; i < 2; i++) {
      const pt = (i === 0) ? conTro1 : conTro2;
      if (i < dsTay.length) {
        const h = dsTay[i];
        const [cx, cy] = raCuaSo(h.conTro, tiLe, krong, kcao);
        conTroDiem.push([cx, cy]);
        capNhatConTro(pt, cx, cy, h.cham, h.tl, dieuKhien.cheDo);
      } else {
        capNhatConTro(pt, null, null, false, 1.0, 'cho');
      }
    }

    // Đường nối khi zoom hai tay
    if (dieuKhien.cheDo === 'zoom' && conTroDiem.length === 2 && dsTay[0].cham && dsTay[1].cham) {
      duongZoomSvg.style.display = 'block';
      duongZoomLine.setAttribute('x1', conTroDiem[0][0]);
      duongZoomLine.setAttribute('y1', conTroDiem[0][1]);
      duongZoomLine.setAttribute('x2', conTroDiem[1][0]);
      duongZoomLine.setAttribute('y2', conTroDiem[1][1]);
    } else {
      duongZoomSvg.style.display = 'none';
    }

    // Vòng tròn tiến độ khi xoè 2 tay
    if (dieuKhien.xoeTu !== null && !dieuKhien.daDatLai) {
      vongTienDoWrap.classList.add('hien');
      const tlXoe = Math.min(1.0, Math.max(0.0, (tGiay - dieuKhien.xoeTu) / HANG_SO.XOE_GIU_S));
      const chuVi = 2 * Math.PI * 20; // 125.66
      cungTienDo.setAttribute('stroke-dashoffset', String(chuVi * (1.0 - tlXoe)));
    } else {
      vongTienDoWrap.classList.remove('hien');
    }

    capNhatChip(dieuKhien.cheDo, dieuKhien.veDau);

    // Số đo để người dùng đọc khi thử thật + gợi ý khi khó nhận ra tay
    const kichThuocNhoNhat = dsTay.length ? Math.min(...dsTay.map((h) => kichThuocTay(h.diem, tiLe))) : null;
    goiYHienTai = goiYNhanDien({ doSang, tanSoCamera: thongKe.tanSoCamera, kichThuocNhoNhat,
                                 giayKhongThayTay: thongKe.giayKhongThayTay(tGiay) });
    hopGoiY.textContent = goiYHienTai || '';
    hopGoiY.style.display = goiYHienTai ? 'block' : 'none';
    hudText.textContent = `${Math.round(thongKe.tanSoNhanDien)} lần/giây | ${Math.round(msNhanDien)} ms | ${soTayHienTai} tay`;
    hudChiTiet.textContent = `Thấy tay ${Math.round(thongKe.tiLeThayTay * 100)}% · sáng ${doSang < 0 ? '–' : doSang}`
      + ` · camera ${Math.round(thongKe.tanSoCamera)} hình/giây`
      + (dsTay.length ? ` · chụm ${dsTay[0].tl.toFixed(2)}` : '')
      + ` · ${luong === 'worker' ? 'luồng riêng' : 'luồng chính'} ${uyQuyenMay}`;
  }

  function tiepTucVongLap() {
    if (!dangChay) return;
    if (videoEl && 'requestVideoFrameCallback' in videoEl) {
      rvfcId = videoEl.requestVideoFrameCallback(xuLyKhungHinh);
    } else {
      animFrameId = requestAnimationFrame(xuLyKhungHinh);
    }
  }

  // --------------------------------------------------------------------------
  // Chế độ phát lại dữ liệu (du-lieu-thu/*.json)
  // --------------------------------------------------------------------------
  let playbackTimer = null;
  let playbackData = null;
  let playbackIndex = 0;

  async function khoiDongPhatLai(urlJson) {
    dangTai = true;
    anLoi();
    capNhatChip('cho', false);

    const res = await fetch(urlJson);
    if (!res.ok) throw new Error('Không tải được tệp phát lại: ' + res.status);
    const data = await res.json();
    playbackData = Array.isArray(data) ? data : (data.vao || []);
    playbackIndex = 0;

    dangTai = false;
    dangChay = true;
    tBatDau = performance.now();
    tTruoc = tBatDau;

    function chayKhungPhatLai() {
      if (!dangChay) return;
      if (playbackIndex >= playbackData.length) {
        // Đã hoàn tất kịch bản phát lại
        return;
      }
      const vao = playbackData[playbackIndex++];
      const mpData = doiKhungSangMediaPipe(vao.tay);
      const dsTay = docTay(mpData, vao.tiLe, vao.t, cacTay);
      dieuKhien.capNhat(vao.t, vao.dt, dsTay);

      soTayHienTai = dsTay.length;

      // Cập nhật con trỏ
      const rect = khung3D.getBoundingClientRect();
      const krong = rect.width;
      const kcao = rect.height;
      const conTroDiem = [];

      for (let i = 0; i < 2; i++) {
        const pt = (i === 0) ? conTro1 : conTro2;
        if (i < dsTay.length) {
          const h = dsTay[i];
          const [cx, cy] = raCuaSo(h.conTro, vao.tiLe, krong, kcao);
          conTroDiem.push([cx, cy]);
          capNhatConTro(pt, cx, cy, h.cham, h.tl, dieuKhien.cheDo);
        } else {
          capNhatConTro(pt, null, null, false, 1.0, 'cho');
        }
      }

      if (dieuKhien.cheDo === 'zoom' && conTroDiem.length === 2 && dsTay[0].cham && dsTay[1].cham) {
        duongZoomSvg.style.display = 'block';
        duongZoomLine.setAttribute('x1', conTroDiem[0][0]);
        duongZoomLine.setAttribute('y1', conTroDiem[0][1]);
        duongZoomLine.setAttribute('x2', conTroDiem[1][0]);
        duongZoomLine.setAttribute('y2', conTroDiem[1][1]);
      } else {
        duongZoomSvg.style.display = 'none';
      }

      capNhatChip(dieuKhien.cheDo, dieuKhien.veDau);
      hudText.textContent = `Phát lại | ${soTayHienTai} tay`;

      playbackTimer = setTimeout(chayKhungPhatLai, Math.round(vao.dt * 1000));
    }

    chayKhungPhatLai();
  }

  // --------------------------------------------------------------------------
  // Bật / Tắt / Huỷ
  // --------------------------------------------------------------------------
  async function bat(nguonMoi) {
    if (nguonMoi) nguonHienTai = nguonMoi;
    anLoi();
    dangTai = true;
    capNhatChip('cho', false);
    bangCam.style.display = 'block';

    try {
      if (typeof nguonHienTai === 'object' && nguonHienTai.phatLai) {
        await khoiDongPhatLai(nguonHienTai.phatLai);
        return;
      }

      // Khởi động MediaPipe (luồng riêng nếu được)
      await khoiTaoNhanDien();

      // Mở Camera
      await moCamera();

      if (!videoEl) {
        videoEl = document.createElement('video');
        videoEl.setAttribute('playsinline', '');
        videoEl.muted = true;
        videoEl.autoplay = true;
        videoEl.style.display = 'none';
        document.body.appendChild(videoEl);
      }
      videoEl.srcObject = stream;
      await videoEl.play();

      if (!procCanvas) {
        procCanvas = document.createElement('canvas');
        procCtx = procCanvas.getContext('2d');
      }

      dangChay = true;
      dangTai = false;
      dangCho = false;
      tBatDau = performance.now();
      tTruoc = 0;
      msTruoc = -1;
      thongKe = new ThongKeTay();
      dsTayCuoi = [];
      doSang = -1;

      tiepTucVongLap();
    } catch (err) {
      dangTai = false;
      dangChay = false;
      const ten = err.name || err.message || '';
      if (ten === 'NotAllowedError' || ten === 'PermissionDeniedError') {
        hienLoi('Vào Cài đặt hệ thống → Quyền riêng tư & Bảo mật → Camera để cấp quyền cho trình duyệt.');
      } else if (ten === 'NotFoundError' || ten === 'DevicesNotFoundError') {
        hienLoi('Không tìm thấy camera.');
      } else if (ten === 'NotReadableError' || ten === 'TrackStartError') {
        hienLoi('Camera đang bị ứng dụng khác sử dụng.');
      } else if (ten === 'ChiCoCameraDienThoai') {
        hienLoi('Chỉ thấy camera điện thoại. Bộ điều khiển tay chỉ dùng camera của máy tính.');
      } else if (ten === 'file:') {
        hienLoi('Vui lòng mở trang qua máy chủ web (http://127.0.0.1:8000), không mở trực tiếp từ file://.');
      } else {
        hienLoi('Không tải được mô hình nhận diện tay hoặc không mở được camera.');
      }
      console.error('Lỗi bật điều khiển tay:', err);
    }
  }

  function tat() {
    dangChay = false;
    dangTai = false;
    if (playbackTimer) {
      clearTimeout(playbackTimer);
      playbackTimer = null;
    }
    if (rvfcId && videoEl && videoEl.cancelVideoFrameCallback) {
      videoEl.cancelVideoFrameCallback(rvfcId);
      rvfcId = null;
    }
    if (animFrameId) {
      cancelAnimationFrame(animFrameId);
      animFrameId = null;
    }
    if (stream) {
      stream.getTracks().forEach(tr => tr.stop());
      stream = null;
    }
    if (videoEl) {
      videoEl.srcObject = null;
    }

    // Ẩn con trỏ và bảng
    conTro1.el.style.display = 'none';
    conTro2.el.style.display = 'none';
    duongZoomSvg.style.display = 'none';
    vongTienDoWrap.classList.remove('hien');
    bangCam.style.display = 'none';
    hopGoiY.style.display = 'none';
    dsTayCuoi = [];
    anLoi();
  }

  function huy() {
    tat();
    if (videoEl && videoEl.parentNode) {
      videoEl.parentNode.removeChild(videoEl);
      videoEl = null;
    }
    if (lopConTro && lopConTro.parentNode) {
      lopConTro.parentNode.removeChild(lopConTro);
    }
    if (vongTienDoWrap && vongTienDoWrap.parentNode) {
      vongTienDoWrap.parentNode.removeChild(vongTienDoWrap);
    }
    if (bangCam && bangCam.parentNode) {
      bangCam.parentNode.removeChild(bangCam);
    }
    if (handLandmarker) {
      try { handLandmarker.close(); } catch (e) {}
      handLandmarker = null;
    }
    if (worker) {
      worker.postMessage({ loai: 'dong' });
      worker.terminate();
      worker = null;
    }
  }

  window.addEventListener('pagehide', () => tat());

  return {
    bat,
    tat,
    huy,
    get trangThai() {
      return {
        bat: dangChay,
        dangTai: dangTai,
        loi: thongBaoLoi,
        uyQuyen: uyQuyen,
        tanSoNhanDien: Math.round(thongKe.tanSoNhanDien),
        msNhanDien: Math.round(msNhanDien),
        tanSoCamera: Math.round(thongKe.tanSoCamera),
        tiLeThayTay: Math.round(thongKe.tiLeThayTay * 100) / 100,
        doSang,
        luong,
        uyQuyenMay,
        goiY: goiYHienTai,
        soTay: soTayHienTai,
        cheDo: dieuKhien.cheDo,
        camera: cameraTen,
        nguon: nguonHienTai
      };
    }
  };
}
