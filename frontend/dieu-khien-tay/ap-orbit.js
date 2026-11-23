/**
 * ap-orbit.js - Áp góc xoay & zoom của bộ điều khiển tay lên camera OrbitControls
 * ES Module: Nhận THREE, camera, controls, dieuKhien qua tham số
 */

export function taoBoApOrbit({ THREE, camera, controls, dieuKhien, doMuot = 0.038, cheDoMuot = 'mac_dinh' }) {
  if (!THREE || !camera || !controls || !dieuKhien) {
    throw new Error('taoBoApOrbit cần đủ { THREE, camera, controls, dieuKhien }');
  }

  const spherical = new THREE.Spherical();
  const offset = new THREE.Vector3();
  const sieuMuot = cheDoMuot === 'sieu_muot';

  let hangChoYaw = 0.0;
  let hangChoPitch = 0.0;
  let hangChoZoomRatio = 1.0;

  // Bộ rải đều khung (sieu_muot): chia bước nhảy 25–30 Hz từ camera thành dòng vận tốc đều tăm tắp 60–120 Hz (triệt gợn răng cưa)
  let conYaw = 0.0;
  let conPitch = 0.0;
  let conLogZoom = 0.0;
  let tCon = 0.0;
  let chuKyCam = 0.033;
  let tTuLanMoi = 0.0;

  let yawMoc = dieuKhien.yaw;
  let pitchMoc = dieuKhien.pitch;
  let zoomMoc = dieuKhien.zoom;

  // Lắng nghe sự kiện từ bộ điều khiển tay để báo cho controls
  const cbGoc = dieuKhien.khiSuKien;
  dieuKhien.khiSuKien = function (sk) {
    if (cbGoc) {
      try { cbGoc(sk); } catch (e) { console.error(e); }
    }
    if (sk.kieu === 'bat_dau') {
      controls.dispatchEvent({ type: 'start', doTay: true });
    } else if (sk.kieu === 'ket_thuc') {
      controls.dispatchEvent({ type: 'end', doTay: true });
    }
  };

  // Chuột cắt ngang: người dùng kéo chuột thì dừng quán tính tay
  function onControlsStart(e) {
    if (e && e.doTay) return;
    dieuKhien.wYaw = 0.0;
    dieuKhien.wPitch = 0.0;
    conYaw = 0.0;
    conPitch = 0.0;
    conLogZoom = 0.0;
    tCon = 0.0;
    if (dieuKhien.cheDo === 'quan_tinh') {
      dieuKhien.cheDo = 'cho';
    }
  }
  controls.addEventListener('start', onControlsStart);

  function dongBo() {
    hangChoYaw = 0.0;
    hangChoPitch = 0.0;
    hangChoZoomRatio = 1.0;
    conYaw = 0.0;
    conPitch = 0.0;
    conLogZoom = 0.0;
    tCon = 0.0;
    tTuLanMoi = 0.0;

    yawMoc = dieuKhien.yaw;
    pitchMoc = dieuKhien.pitch;
    zoomMoc = dieuKhien.zoom;

    offset.copy(camera.position).sub(controls.target);
    spherical.setFromVector3(offset);
  }

  function capNhat(dt) {
    if (typeof dt !== 'number' || dt <= 0 || isNaN(dt)) dt = 0.016;

    // Cộng dồn Δ từ lần đo trước của dieuKhien vào hàng chờ
    const dYaw = dieuKhien.yaw - yawMoc;
    const dPitch = dieuKhien.pitch - pitchMoc;
    const dZoomRatio = (dieuKhien.zoom > 1e-4) ? (zoomMoc / dieuKhien.zoom) : 1.0;
    const coMoi = Math.abs(dYaw) > 1e-7 || Math.abs(dPitch) > 1e-7 || Math.abs(dZoomRatio - 1.0) > 1e-7;

    yawMoc = dieuKhien.yaw;
    pitchMoc = dieuKhien.pitch;
    zoomMoc = dieuKhien.zoom;

    if (sieuMuot && dieuKhien.cheDo !== 'quan_tinh') {
      tTuLanMoi += dt;
      if (coMoi) {
        // Tự đo chu kỳ thực của camera (kẹp 16–55 ms) để rải bước nhảy thật đều qua các khung 60–120 Hz
        if (tTuLanMoi >= 0.014 && tTuLanMoi <= 0.085) {
          chuKyCam = chuKyCam * 0.72 + Math.min(0.055, Math.max(0.016, tTuLanMoi)) * 0.28;
        }
        tTuLanMoi = 0.0;
        // Phần dư cũ chưa kịp rải đổ sang bộ lọc mượt ngay, phần mới rải đều theo chuKyCam
        hangChoYaw += conYaw;
        hangChoPitch += conPitch;
        if (Math.abs(conLogZoom) > 1e-7) hangChoZoomRatio *= Math.exp(conLogZoom);
        conYaw = dYaw;
        conPitch = dPitch;
        conLogZoom = Math.log(dZoomRatio);
        tCon = chuKyCam;
      }
      if (tCon > 1e-4) {
        const phan = Math.min(1.0, dt / tCon);
        const rYaw = conYaw * phan;
        const rPitch = conPitch * phan;
        const rLogZ = conLogZoom * phan;
        conYaw -= rYaw;
        conPitch -= rPitch;
        conLogZoom -= rLogZ;
        tCon = Math.max(0.0, tCon - dt);
        hangChoYaw += rYaw;
        hangChoPitch += rPitch;
        if (Math.abs(rLogZ) > 1e-7) hangChoZoomRatio *= Math.exp(rLogZ);
      } else if (Math.abs(conYaw) > 1e-7 || Math.abs(conPitch) > 1e-7 || Math.abs(conLogZoom) > 1e-7) {
        hangChoYaw += conYaw;
        hangChoPitch += conPitch;
        if (Math.abs(conLogZoom) > 1e-7) hangChoZoomRatio *= Math.exp(conLogZoom);
        conYaw = conPitch = conLogZoom = 0.0;
      }
    } else {
      if (Math.abs(conYaw) > 1e-7 || Math.abs(conPitch) > 1e-7 || Math.abs(conLogZoom) > 1e-7) {
        hangChoYaw += conYaw;
        hangChoPitch += conPitch;
        if (Math.abs(conLogZoom) > 1e-7) hangChoZoomRatio *= Math.exp(conLogZoom);
        conYaw = conPitch = conLogZoom = 0.0;
        tCon = 0.0;
      }
      hangChoYaw += dYaw;
      hangChoPitch += dPitch;
      hangChoZoomRatio *= dZoomRatio;
    }

    // Khi không có biến thiên nào đang chờ, không chạm vào camera để chuột / hoạt ảnh GSAP mượt hoàn toàn
    if (Math.abs(hangChoYaw) < 1e-6 && Math.abs(hangChoPitch) < 1e-6 && Math.abs(hangChoZoomRatio - 1.0) < 1e-6) {
      hangChoYaw = 0.0;
      hangChoPitch = 0.0;
      hangChoZoomRatio = 1.0;
      return;
    }

    // Áp dần một phần của hàng chờ theo hàm mượt k = 1 - exp(-dt / doMuot)
    const tau = sieuMuot ? Math.min(doMuot, 0.024) : doMuot;
    const k = tau > 0 ? (1.0 - Math.exp(-dt / tau)) : 1.0;
    const heSoK = Math.min(1.0, Math.max(0.0, k));

    const apYaw = hangChoYaw * heSoK;
    hangChoYaw -= apYaw;

    const apPitch = hangChoPitch * heSoK;
    hangChoPitch -= apPitch;

    const apZoom = Math.pow(hangChoZoomRatio, heSoK);
    hangChoZoomRatio /= apZoom;
    if (Math.abs(hangChoZoomRatio - 1.0) < 1e-7) hangChoZoomRatio = 1.0;

    // Lấy toạ độ cầu hiện tại của camera quanh target
    offset.copy(camera.position).sub(controls.target);
    spherical.setFromVector3(offset);

    // Đổi dấu:
    // θ_camera -= Δyaw (yaw tăng -> mô hình quay sang phải -> camera đi sang trái)
    // φ_camera -= Δpitch (pitch tăng -> nhìn từ trên xuống -> camera lên cao -> phi giảm)
    // radius *= zoomTrước / zoomSau (zoom tăng -> bán kính giảm -> lại gần)
    spherical.theta -= apYaw;
    spherical.phi -= apPitch;
    spherical.radius *= apZoom;

    // Kẹp góc cực theo controls
    const minPhi = controls.minPolarAngle !== undefined ? controls.minPolarAngle : 0.01;
    const maxPhi = controls.maxPolarAngle !== undefined ? controls.maxPolarAngle : (Math.PI - 0.01);
    spherical.phi = Math.max(minPhi, Math.min(maxPhi, spherical.phi));

    // Kẹp khoảng cách theo controls
    const minR = controls.minDistance !== undefined ? controls.minDistance : 0.1;
    const maxR = controls.maxDistance !== undefined ? controls.maxDistance : 1000.0;
    spherical.radius = Math.max(minR, Math.min(maxR, spherical.radius));

    // Cập nhật lại vị trí camera
    offset.setFromSpherical(spherical);
    camera.position.copy(controls.target).add(offset);
    camera.lookAt(controls.target);
  }

  function huy() {
    controls.removeEventListener('start', onControlsStart);
  }

  // Đồng bộ vị trí ban đầu
  dongBo();

  return {
    capNhat,
    dongBo,
    huy
  };
}
