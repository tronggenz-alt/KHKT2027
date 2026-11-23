import { FilesetResolver, HandLandmarker } from '../vendor/mediapipe/tasks-vision-1.1.0/vision_bundle.mjs';
import cuChiModule from './cu-chi.js';
import chonCamModule from './chon-camera.js';
import { RONG_NHAN_DIEN, tinhVungCat, doiVeKhungDay, kichThuocTay, ThongKeTay, goiYNhanDien } from './khung-hinh.js';

const { docTay, raCuaSo, HANG_SO } = cuChiModule;
const { chonCamera } = chonCamModule;

// Ngưỡng tối ưu cho nhận diện tay mượt và bám chắc khi chụm ngón / lia tay nhanh / ánh sáng trong phòng:
// - minHandDetectionConfidence 0.42: phát hiện bàn tay sớm hơn mà không bắt nhầm nền
// - minHandPresenceConfidence 0.35 & minTrackingConfidence 0.32: bám khung liên tục khi ngón cái/trỏ che nhau lúc chụm
const TUY_CHON = { numHands: 2, minHandDetectionConfidence: 0.42, minHandPresenceConfidence: 0.35, minTrackingConfidence: 0.32 };

// Bộ ổn định khung tay trước khi đưa vào docTay (chỉ dùng cho camera trực tiếp):
// 1. Giữ nguyên nhãn Left/Right theo vị trí lòng bàn tay gần nhất → hết lỗi lật tay giữa chừng làm đứt kéo
// 2. Lọc mượt 21 điểm + toạ độ 3D thích ứng theo tốc độ (chậm → lọc rung mạnh, nhanh → bắt kịp tức thì)
// 3. Bù 1–2 khung bị rớt thoáng qua (< 95 ms) khi vẩy tay nhanh để xoay/zoom không bị khựng
export function taoBoOnDinhTay() {
  const BAN_KINH_KHOP = 0.22;
  const BU_ROT_S = 0.095;
  let truoc = []; // [{ ben, tam: [x, y], v: [vx, vy], diem, theGioi, score, t }]

  const tamBanTay = (d) => [(d[0].x + d[9].x) * 0.5, (d[0].y + d[9].y) * 0.5];
  const saoDiem = (ds) => ds.map((p) => ({ x: p.x, y: p.y, z: p.z || 0 }));
  const locMang = (cu, moi, a) => moi.map((p, i) => {
    const q = cu[i] || p;
    return { x: q.x + (p.x - q.x) * a, y: q.y + (p.y - q.y) * a, z: (q.z || 0) + ((p.z || 0) - (q.z || 0)) * a };
  });

  function datLai() { truoc = []; }

  function xuLy(mp, t) {
    const hl = (mp && mp.hand_landmarks) || [];
    const hwl = (mp && mp.hand_world_landmarks) || [];
    const hd = (mp && mp.handedness) || [];
    const daDungCu = new Set();
    const moi = [];

    for (let i = 0; i < hl.length; i++) {
      const diem = hl[i];
      if (!diem || diem.length < 21) continue;
      const tam = tamBanTay(diem);
      let idxTot = -1, kcTot = BAN_KINH_KHOP;
      for (let j = 0; j < truoc.length; j++) {
        if (daDungCu.has(j)) continue;
        const dtCu = Math.max(0, t - truoc[j].t);
        if (dtCu > 0.35) continue;
        const px = truoc[j].tam[0] + truoc[j].v[0] * Math.min(dtCu, 0.08);
        const py = truoc[j].tam[1] + truoc[j].v[1] * Math.min(dtCu, 0.08);
        const kc = Math.hypot(tam[0] - px, tam[1] - py);
        if (kc < kcTot) { kcTot = kc; idxTot = j; }
      }

      const hItem = (hd[i] && hd[i][0]) || {};
      const tenGoc = hItem.categoryName || hItem.category_name || ('Tay' + i);
      const theGioiMoi = (hwl[i] && hwl[i].length >= 21) ? saoDiem(hwl[i]) : null;

      if (idxTot >= 0) {
        daDungCu.add(idxTot);
        const cu = truoc[idxTot];
        const dt = Math.max(0.008, t - cu.t);
        const buoc = Math.hypot(tam[0] - cu.tam[0], tam[1] - cu.tam[1]);
        // Thích ứng tốc độ: giữ yên/kéo chậm → alpha ~0.44 chống rung ngón; kéo nhanh → alpha ~0.92 không trễ
        const tyLe = Math.min(1, Math.max(0, (buoc - 0.005) / 0.032));
        const a2d = 0.44 + tyLe * 0.48;
        const a3d = 0.42 + tyLe * 0.46;
        const diemLoc = locMang(cu.diem, diem, a2d);
        const tgLoc = (theGioiMoi && cu.theGioi) ? locMang(cu.theGioi, theGioiMoi, a3d) : theGioiMoi;
        const tamLoc = tamBanTay(diemLoc);
        const vx = Math.max(-2.5, Math.min(2.5, (tamLoc[0] - cu.tam[0]) / dt));
        const vy = Math.max(-2.5, Math.min(2.5, (tamLoc[1] - cu.tam[1]) / dt));
        moi.push({
          ben: cu.ben,
          tam: tamLoc,
          v: [cu.v[0] * 0.35 + vx * 0.65, cu.v[1] * 0.35 + vy * 0.65],
          diem: diemLoc,
          theGioi: tgLoc,
          score: hItem.score,
          t,
          bu: false
        });
      } else {
        // Tránh trùng tên bên với tay đang tồn tại
        const tenTrung = moi.some((m) => m.ben === tenGoc);
        const ben = tenTrung ? (tenGoc === 'Left' ? 'Right' : 'Left') : tenGoc;
        moi.push({
          ben,
          tam,
          v: [0, 0],
          diem: saoDiem(diem),
          theGioi: theGioiMoi,
          score: hItem.score,
          t,
          bu: false
        });
      }
    }

    // Bù thoáng qua (<= 95 ms) nếu tay bị rớt 1–2 khung lúc lia nhanh / nghiêng bàn tay
    for (let j = 0; j < truoc.length; j++) {
      if (daDungCu.has(j) || moi.length >= 2) continue;
      const cu = truoc[j];
      const dtMat = t - cu.t;
      if (dtMat > 0 && dtMat <= BU_ROT_S && !moi.some((m) => m.ben === cu.ben)) {
        const dx = cu.v[0] * dtMat * 0.45;
        const dy = cu.v[1] * dtMat * 0.45;
        const diemBu = cu.diem.map((p) => ({
          x: Math.min(1, Math.max(0, p.x + dx)),
          y: Math.min(1, Math.max(0, p.y + dy)),
          z: p.z || 0
        }));
        moi.push({
          ben: cu.ben,
          tam: tamBanTay(diemBu),
          v: [cu.v[0] * 0.6, cu.v[1] * 0.6],
          diem: diemBu,
          theGioi: cu.theGioi ? saoDiem(cu.theGioi) : null,
          score: cu.score,
          t: cu.t,
          bu: true
        });
      }
    }

    truoc = moi;
    const ra = { hand_landmarks: [], hand_world_landmarks: [], handedness: [] };
    for (const h of moi) {
      ra.hand_landmarks.push(h.diem);
      if (h.theGioi) ra.hand_world_landmarks.push(h.theGioi);
      ra.handedness.push([{ categoryName: h.ben, category_name: h.ben, score: h.score }]);
    }
    if (ra.hand_world_landmarks.length !== ra.hand_landmarks.length) ra.hand_world_landmarks = [];
    return ra;
  }

  return { xuLy, datLai };
}

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
  const href = new URL('./dieu-khien-tay.css' + new URL(import.meta.url).search, import.meta.url).href;
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
  let dangDungVongRender = false; // true khi vòng render 60–120 Hz đang gọi capNhatKhung(dt)
  const boOnDinh = taoBoOnDinhTay();

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
      tam: el.querySelector('.dkt-ct-tam'),
      hien: false,
      x: 0,
      y: 0,
      tx: 0,
      ty: 0
    };
  }

  function datViTriConTro(pt, x, y) {
    pt.x = x;
    pt.y = y;
    pt.el.style.transform = `translate3d(${x.toFixed(1)}px, ${y.toFixed(1)}px, 0) translate(-50%, -50%)`;
  }

  function capNhatConTro(pt, x, y, cham, tl, cheDo) {
    if (x === null) {
      pt.hien = false;
      pt.el.style.display = 'none';
      return;
    }
    const moiHien = !pt.hien;
    pt.hien = true;
    pt.tx = x;
    pt.ty = y;
    pt.el.style.display = 'block';
    if (moiHien || !dangDungVongRender) {
      datViTriConTro(pt, x, y);
    }

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
        width: { ideal: 960 },
        height: { ideal: 540 },
        frameRate: { ideal: 60 }
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
        { resizeWidth: RONG_NHAN_DIEN, resizeHeight: caoNhanDien, resizeQuality: 'medium' })
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
    const mpDaCat = kq ? doiVeKhungDay(kq, cat, vw, vh) : null;
    const mpOnDinh = boOnDinh.xuLy(mpDaCat, tGiay);
    const dsTay = docTay(mpOnDinh, tiLe, tGiay, cacTay);
    soTayHienTai = dsTay.length;
    dsTayCuoi = dsTay;
    thongKe.ghiNhanDien(tGiay, dsTay.length);

    // Cập nhật bộ điều khiển cử chỉ (nếu vòng render 60–120 Hz đang tự tích phân quán tính mượt thì tránh cộng dồn dt lần 2)
    const dtDieuKhien = (dangDungVongRender && dieuKhien.cheDo === 'quan_tinh') ? 0 : dt;
    dieuKhien.capNhat(tGiay, dtDieuKhien, dsTay);

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
      boOnDinh.datLai();
      for (const k of Object.keys(cacTay)) delete cacTay[k];
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
    conTro1.hien = false;
    conTro2.hien = false;
    conTro1.el.style.display = 'none';
    conTro2.el.style.display = 'none';
    duongZoomSvg.style.display = 'none';
    vongTienDoWrap.classList.remove('hien');
    bangCam.style.display = 'none';
    hopGoiY.style.display = 'none';
    boOnDinh.datLai();
    for (const k of Object.keys(cacTay)) delete cacTay[k];
    dsTayCuoi = [];
    anLoi();
  }

  // Gọi mỗi khung hình render 3D (60–120 Hz): nội suy con trỏ tay bằng GPU translate3d và lướt quán tính cực mượt
  function capNhatKhung(dt) {
    dangDungVongRender = true;
    if (!dangChay) return;
    if (typeof dt !== 'number' || dt <= 0 || isNaN(dt)) dt = 0.016;
    const dtKep = Math.min(0.05, dt);

    // Nội suy mượt con trỏ tay ở 60–120 FPS (không nhảy cục theo nhịp camera 25–30 Hz)
    const k = 1 - Math.exp(-dtKep / 0.030);
    for (const pt of [conTro1, conTro2]) {
      if (!pt.hien) continue;
      const nx = pt.x + (pt.tx - pt.x) * k;
      const ny = pt.y + (pt.ty - pt.y) * k;
      datViTriConTro(pt, nx, ny);
    }
    if (duongZoomSvg.style.display !== 'none' && conTro1.hien && conTro2.hien) {
      duongZoomLine.setAttribute('x1', conTro1.x.toFixed(1));
      duongZoomLine.setAttribute('y1', conTro1.y.toFixed(1));
      duongZoomLine.setAttribute('x2', conTro2.x.toFixed(1));
      duongZoomLine.setAttribute('y2', conTro2.y.toFixed(1));
    }

    // Lướt quán tính ở đúng tần số quét màn hình (60–120 Hz) thay vì giật theo nhịp nhận diện camera
    if (dieuKhien.cheDo === 'quan_tinh') {
      dieuKhien.yaw += dieuKhien.wYaw * dtKep;
      const pMoi = dieuKhien.pitch + dieuKhien.wPitch * dtKep;
      dieuKhien.pitch = Math.max(-HANG_SO.PITCH_MAX, Math.min(HANG_SO.PITCH_MAX, pMoi));
      if (dieuKhien.pitch !== pMoi) dieuKhien.wPitch = 0.0;
      dieuKhien.wYaw *= Math.exp(-dtKep / HANG_SO.QT_TAT);
      dieuKhien.wPitch *= Math.exp(-dtKep / (HANG_SO.QT_TAT / 2));
      if (Math.hypot(dieuKhien.wYaw, dieuKhien.wPitch) < 0.05) {
        dieuKhien.wYaw = 0.0;
        dieuKhien.wPitch = 0.0;
        dieuKhien.cheDo = 'cho';
        if (dieuKhien._dangHoatDong) {
          dieuKhien._dangHoatDong = false;
          dieuKhien._phat?.('ket_thuc');
        }
        capNhatChip(dieuKhien.cheDo, dieuKhien.veDau);
      }
    }
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
    capNhatKhung,
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
