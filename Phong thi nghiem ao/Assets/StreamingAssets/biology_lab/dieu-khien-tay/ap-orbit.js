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

  // Bộ rải đều khung + lò xo giảm chấn tới hạn C¹ (sieu_muot): vận tốc góc liên tục tuyệt đối ở 60–120 Hz,
  // không khựng khi khung camera về trễ 10–15 ms và chuyển mượt liền mạch từ kéo sang lướt quán tính
  let conYaw = 0.0;
  let conPitch = 0.0;
  let conLogZoom = 0.0;
  let tCon = 0.0;
  let chuKyCam = 0.034;
  let tTuLanMoi = 0.0;
  let vYaw = 0.0;
  let vPitch = 0.0;
  let vLogZoom = 0.0;
  let hangChoLogZoom = 0.0;

  let yawMoc = dieuKhien.yaw;
  let pitchMoc = dieuKhien.pitch;
  let zoomMoc = dieuKhien.zoom;

  function buocLoXo(rem, vel, smoothTime, dt) {
    if (Math.abs(rem) < 1e-7 && Math.abs(vel) < 1e-6) return [0.0, 0.0, 0.0];
    const omega = 2.0 / Math.max(0.008, smoothTime);
    const x = omega * dt;
    const exp = 1.0 / (1.0 + x + 0.48 * x * x + 0.235 * x * x * x);
    const change = -rem;
    const temp = (vel + omega * change) * dt;
    const velMoi = (vel - omega * temp) * exp;
    const changeMoi = (change + temp) * exp;
    const buoc = changeMoi - change;
    return [buoc, rem - buoc, velMoi];
  }

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
    conYaw = conPitch = conLogZoom = 0.0;
    hangChoYaw = hangChoPitch = hangChoLogZoom = 0.0;
    hangChoZoomRatio = 1.0;
    vYaw = vPitch = vLogZoom = 0.0;
    tCon = 0.0;
    if (dieuKhien.cheDo === 'quan_tinh') {
      dieuKhien.cheDo = 'cho';
    }
  }
  controls.addEventListener('start', onControlsStart);

  function dongBo() {
    hangChoYaw = 0.0;
    hangChoPitch = 0.0;
    hangChoLogZoom = 0.0;
    hangChoZoomRatio = 1.0;
    conYaw = 0.0;
    conPitch = 0.0;
    conLogZoom = 0.0;
    vYaw = 0.0;
    vPitch = 0.0;
    vLogZoom = 0.0;
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
    const dLogZoom = Math.abs(dZoomRatio - 1.0) > 1e-7 ? Math.log(dZoomRatio) : 0.0;
    const coMoi = Math.abs(dYaw) > 1e-7 || Math.abs(dPitch) > 1e-7 || Math.abs(dLogZoom) > 1e-7;

    yawMoc = dieuKhien.yaw;
    pitchMoc = dieuKhien.pitch;
    zoomMoc = dieuKhien.zoom;

    let apYaw = 0.0, apPitch = 0.0, apZoom = 1.0;

    if (sieuMuot) {
      if (dieuKhien.cheDo === 'quan_tinh') {
        // Quán tính đã tích phân đều ở 60–120 Hz → cộng thẳng vào lò xo C¹ mà không giật cục
        if (tCon > 1e-4) {
          const phan = Math.min(1.0, dt / tCon);
          hangChoYaw += conYaw * phan; conYaw -= conYaw * phan;
          hangChoPitch += conPitch * phan; conPitch -= conPitch * phan;
          hangChoLogZoom += conLogZoom * phan; conLogZoom -= conLogZoom * phan;
          tCon = Math.max(0.0, tCon - dt);
        } else if (Math.abs(conYaw) > 1e-7 || Math.abs(conPitch) > 1e-7 || Math.abs(conLogZoom) > 1e-7) {
          hangChoYaw += conYaw; hangChoPitch += conPitch; hangChoLogZoom += conLogZoom;
          conYaw = conPitch = conLogZoom = 0.0;
        }
        hangChoYaw += dYaw;
        hangChoPitch += dPitch;
        hangChoLogZoom += dLogZoom;
      } else {
        tTuLanMoi += dt;
        if (coMoi) {
          if (tTuLanMoi >= 0.014 && tTuLanMoi <= 0.090) {
            const doMoi = Math.min(0.060, Math.max(0.016, tTuLanMoi));
            // Tăng nhanh khi camera chậm lại, hạ chậm khi camera nhanh lên → đệm rải không bao giờ cạn giữa khung
            const heSo = doMoi > chuKyCam ? 0.45 : 0.16;
            chuKyCam = chuKyCam * (1 - heSo) + doMoi * heSo;
          }
          tTuLanMoi = 0.0;
          hangChoYaw += conYaw;
          hangChoPitch += conPitch;
          hangChoLogZoom += conLogZoom;
          conYaw = dYaw;
          conPitch = dPitch;
          conLogZoom = dLogZoom;
          tCon = chuKyCam * 1.12;
        }
        if (tCon > 1e-4) {
          const phan = Math.min(1.0, dt / tCon);
          const rYaw = conYaw * phan, rPitch = conPitch * phan, rLogZ = conLogZoom * phan;
          conYaw -= rYaw; conPitch -= rPitch; conLogZoom -= rLogZ;
          tCon = Math.max(0.0, tCon - dt);
          hangChoYaw += rYaw;
          hangChoPitch += rPitch;
          hangChoLogZoom += rLogZ;
        } else if (Math.abs(conYaw) > 1e-7 || Math.abs(conPitch) > 1e-7 || Math.abs(conLogZoom) > 1e-7) {
          hangChoYaw += conYaw; hangChoPitch += conPitch; hangChoLogZoom += conLogZoom;
          conYaw = conPitch = conLogZoom = 0.0;
        }
      }

      const dangNghi = Math.abs(hangChoYaw) < 1e-6 && Math.abs(hangChoPitch) < 1e-6 && Math.abs(hangChoLogZoom) < 1e-6
        && Math.abs(vYaw) < 1e-5 && Math.abs(vPitch) < 1e-5 && Math.abs(vLogZoom) < 1e-5;
      if (dangNghi) {
        hangChoYaw = hangChoPitch = hangChoLogZoom = 0.0;
        vYaw = vPitch = vLogZoom = 0.0;
        return;
      }

      const smoothT = Math.max(0.024, doMuot);
      [apYaw, hangChoYaw, vYaw] = buocLoXo(hangChoYaw, vYaw, smoothT, dt);
      [apPitch, hangChoPitch, vPitch] = buocLoXo(hangChoPitch, vPitch, smoothT, dt);
      let apLogZ = 0.0;
      [apLogZ, hangChoLogZoom, vLogZoom] = buocLoXo(hangChoLogZoom, vLogZoom, smoothT * 1.1, dt);
      apZoom = Math.exp(apLogZ);
    } else {
      hangChoYaw += dYaw;
      hangChoPitch += dPitch;
      hangChoZoomRatio *= dZoomRatio;

      if (Math.abs(hangChoYaw) < 1e-6 && Math.abs(hangChoPitch) < 1e-6 && Math.abs(hangChoZoomRatio - 1.0) < 1e-6) {
        hangChoYaw = 0.0;
        hangChoPitch = 0.0;
        hangChoZoomRatio = 1.0;
        return;
      }

      // Áp dần một phần của hàng chờ theo hàm mượt k = 1 - exp(-dt / doMuot)
      const k = doMuot > 0 ? (1.0 - Math.exp(-dt / doMuot)) : 1.0;
      const heSoK = Math.min(1.0, Math.max(0.0, k));

      apYaw = hangChoYaw * heSoK;
      hangChoYaw -= apYaw;

      apPitch = hangChoPitch * heSoK;
      hangChoPitch -= apPitch;

      apZoom = Math.pow(hangChoZoomRatio, heSoK);
      hangChoZoomRatio /= apZoom;
      if (Math.abs(hangChoZoomRatio - 1.0) < 1e-7) hangChoZoomRatio = 1.0;
    }

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
