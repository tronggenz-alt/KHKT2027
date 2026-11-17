/**
 * ap-orbit.js - Áp góc xoay & zoom của bộ điều khiển tay lên camera OrbitControls
 * ES Module: Nhận THREE, camera, controls, dieuKhien qua tham số
 */

export function taoBoApOrbit({ THREE, camera, controls, dieuKhien, doMuot = 0.02 }) { // 26/10: 0.03 → 0.02 s cho đỡ trễ
  if (!THREE || !camera || !controls || !dieuKhien) {
    throw new Error('taoBoApOrbit cần đủ { THREE, camera, controls, dieuKhien }');
  }

  const spherical = new THREE.Spherical();
  const offset = new THREE.Vector3();

  let hangChoYaw = 0.0;
  let hangChoPitch = 0.0;
  let hangChoZoomRatio = 1.0;

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
    if (dieuKhien.cheDo === 'quan_tinh') {
      dieuKhien.cheDo = 'cho';
    }
  }
  controls.addEventListener('start', onControlsStart);

  function dongBo() {
    hangChoYaw = 0.0;
    hangChoPitch = 0.0;
    hangChoZoomRatio = 1.0;

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

    hangChoYaw += dYaw;
    hangChoPitch += dPitch;
    hangChoZoomRatio *= dZoomRatio;

    yawMoc = dieuKhien.yaw;
    pitchMoc = dieuKhien.pitch;
    zoomMoc = dieuKhien.zoom;

    // Áp dần một phần của hàng chờ theo hàm mượt k = 1 - exp(-dt / doMuot)
    const k = doMuot > 0 ? (1.0 - Math.exp(-dt / doMuot)) : 1.0;
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
