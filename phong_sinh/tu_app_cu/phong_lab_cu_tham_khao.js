// =====================================================================================================================
// 3. PHÒNG LAB (môi trường)
// =====================================================================================================================
const lab = { tuong: null, luoi: null, hatNen: null, chumSang: null, vongs: [], san: null, de: null, dai: [] };
(function dungPhongLab() {
  const san = new THREE.Mesh(new THREE.CircleGeometry(10, 72), new THREE.MeshStandardMaterial({ color: 0x0a1222, roughness: 0.85, metalness: 0.35 }));
  san.rotation.x = -Math.PI / 2;
  lab.san = san;
  const luoi = new THREE.GridHelper(20, 40, 0x06b6d4, 0x0e3a4a);
  luoi.material.transparent = true;
  luoi.material.opacity = 0.28;
  luoi.position.y = 0.002;
  lab.luoi = luoi;
  // bệ hologram
  const de = new THREE.Mesh(new THREE.CylinderGeometry(1.05, 1.2, 0.14, 72), new THREE.MeshStandardMaterial({ color: 0x0c1a2e, roughness: 0.35, metalness: 0.7, emissive: 0x06b6d4, emissiveIntensity: 0.12 }));
  de.position.y = 0.07;
  lab.de = de;
  const matVong = (mau, doMo) => new THREE.MeshBasicMaterial({ color: mau, transparent: true, opacity: doMo, blending: THREE.AdditiveBlending, side: THREE.DoubleSide, depthWrite: false });
  const vong1 = new THREE.Mesh(new THREE.RingGeometry(0.98, 1.04, 128), matVong(0x22d3ee, 0.9));
  vong1.rotation.x = -Math.PI / 2;
  vong1.position.y = 0.145;
  const vong2 = new THREE.Mesh(new THREE.RingGeometry(0.62, 0.65, 6, 1), matVong(0x10b981, 0.7));
  vong2.rotation.x = -Math.PI / 2;
  vong2.position.y = 0.146;
  const vong3 = new THREE.Mesh(new THREE.TorusGeometry(1.12, 0.006, 8, 160), matVong(0x06b6d4, 0.65));
  vong3.rotation.x = Math.PI / 2;
  vong3.position.y = 0.6;
  const vong4 = new THREE.Mesh(new THREE.TorusGeometry(1.18, 0.004, 8, 160), matVong(0x10b981, 0.5));
  vong4.rotation.x = Math.PI / 2;
  vong4.position.y = 2.4;
  lab.vongs = [vong1, vong2, vong3, vong4];
  // chùm sáng hologram (gradient dọc)
  const chum = new THREE.Mesh(new THREE.CylinderGeometry(0.98, 1.08, 3.9, 72, 1, true), new THREE.ShaderMaterial({
    transparent: true, depthWrite: false, blending: THREE.AdditiveBlending, side: THREE.DoubleSide,
    uniforms: { uThoiGian: { value: 0 }, uMau: { value: new THREE.Color(0x06b6d4) } },
    vertexShader: "varying vec2 vUv; void main(){ vUv = uv; gl_Position = projectionMatrix * modelViewMatrix * vec4(position,1.0); }",
    fragmentShader: `uniform float uThoiGian; uniform vec3 uMau; varying vec2 vUv;
      void main(){ float a = pow(1.0 - vUv.y, 2.2) * 0.22; float vach = smoothstep(0.92, 1.0, sin((vUv.y * 40.0) - uThoiGian * 3.0)) * 0.06;
        gl_FragColor = vec4(uMau, a + vach * (1.0 - vUv.y)); }`,
  }));
  chum.position.y = 2.05;
  lab.chumSang = chum;
  // tường lục giác
  const c = document.createElement("canvas");
  c.width = 256; c.height = 222;
  const g = c.getContext("2d");
  g.fillStyle = "#050a14"; g.fillRect(0, 0, 256, 222);
  g.strokeStyle = "rgba(6,182,212,0.55)"; g.lineWidth = 2;
  const luc = (cx, cy, r) => { g.beginPath(); for (let i = 0; i < 6; i++) { const a = Math.PI / 3 * i; g.lineTo(cx + r * Math.cos(a), cy + r * Math.sin(a)); } g.closePath(); g.stroke(); };
  for (let hang = -1; hang < 4; hang++) for (let cot = -1; cot < 4; cot++) luc(cot * 96 + (hang % 2 ? 48 : 0), hang * 74, 42);
  const ktTuong = new THREE.CanvasTexture(c);
  ktTuong.wrapS = ktTuong.wrapT = THREE.RepeatWrapping;
  ktTuong.repeat.set(14, 3);
  ktTuong.colorSpace = THREE.SRGBColorSpace;
  const tuong = new THREE.Mesh(new THREE.CylinderGeometry(9, 9, 7, 64, 1, true), new THREE.MeshStandardMaterial({ map: ktTuong, emissiveMap: ktTuong, emissive: 0x0b6f84, emissiveIntensity: 0.55, side: THREE.BackSide, roughness: 0.9, metalness: 0.1 }));
  tuong.position.y = 3.5;
  lab.tuong = tuong;
  // dải đèn dọc + vòng trần
  const matDai = new THREE.MeshBasicMaterial({ color: 0x10b981, transparent: true, opacity: 0.7 });
  for (let i = 0; i < 12; i++) {
    const a = (i / 12) * Math.PI * 2;
    const dai = new THREE.Mesh(new THREE.BoxGeometry(0.05, 6, 0.05), matDai);
    dai.position.set(Math.cos(a) * 8.85, 3.2, Math.sin(a) * 8.85);
    lab.dai.push(dai);
  }
  const tran = new THREE.Mesh(new THREE.TorusGeometry(3.2, 0.03, 8, 120), matVong(0x06b6d4, 0.6));
  tran.rotation.x = Math.PI / 2;
  tran.position.y = 6.2;
  lab.dai.push(tran);
  // hạt sinh học lơ lửng
  const n = 520;
  const pos = new Float32Array(n * 3);
  const r = ngauNhien(7);
  for (let i = 0; i < n; i++) {
    const a = r() * Math.PI * 2, rr = 1.6 + r() * 6.5;
    pos.set([Math.cos(a) * rr, r() * 6.2, Math.sin(a) * rr], i * 3);
  }
  const hg = new THREE.BufferGeometry();
  hg.setAttribute("position", new THREE.BufferAttribute(pos, 3));
  const hatNen = new THREE.Points(hg, vatLieuHat(0x34d399, 0.05, 0.55));
  hatNen.userData.goc = pos.slice();
  lab.hatNen = hatNen;
  moiTruong.add(san, luoi, de, ...lab.vongs, chum, tuong, ...lab.dai, hatNen);
})();

