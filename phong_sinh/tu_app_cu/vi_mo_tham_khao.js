// =====================================================================================================================
// 16. CẤP ĐỘ HIỂN VI (Micro-Zoom) – 6 cảnh tế bào / hoá sinh
// =====================================================================================================================
const viMo = {};
let cheDoViMo = null;
function nhanViMo(nhom, chu, viTri) {
  const el = document.createElement("div");
  el.className = "nhan-vi-mo";
  el.textContent = chu;
  const o = new CSS2DObject(el);
  o.position.copy(viTri);
  o.visible = false;
  nhom.add(o);
  (nhom.userData.nhan = nhom.userData.nhan || []).push(o);
}
function matVM(mau, doMo = 1, them = {}) {
  const m = new THREE.MeshPhysicalMaterial({ color: mau, roughness: 0.4, metalness: 0.05, clearcoat: 0.4, transparent: doMo < 1, opacity: doMo, side: doMo < 1 ? THREE.DoubleSide : THREE.FrontSide, ...them });
  if (doMo < 1) m.depthWrite = false;
  return m;
}
function hatDiChuyen(nhom, soLuong, mau, kt) {
  const vt = new Float32Array(soLuong * 3);
  const g = new THREE.BufferGeometry();
  g.setAttribute("position", new THREE.BufferAttribute(vt, 3));
  const p = new THREE.Points(g, vatLieuHat(mau, kt, 1));
  p.frustumCulled = false;
  nhom.add(p);
  return { p, vt, n: soLuong };
}
// --- Phế nang + mao mạch phổi ---
viMo.pho_nang = (() => {
  const nhom = new THREE.Group();
  const r = ngauNhien(11);
  const tamPN = [V(0, 0, 0), V(0.95, 0.3, 0.1), V(-0.9, 0.35, -0.1), V(0.3, 0.95, -0.3), V(-0.25, -0.85, 0.2), V(0.6, -0.6, -0.5), V(-0.5, 0.2, 0.9)];
  tamPN.forEach((c) => { const m = new THREE.Mesh(new THREE.SphereGeometry(0.62, 40, 28), matVM(0xf9a8d4, 0.32)); m.position.copy(c); nhom.add(m); });
  const ongPQ = new THREE.Mesh(new THREE.CylinderGeometry(0.18, 0.22, 1.4, 24, 1, true), matVM(0xf1f5f9, 0.5));
  ongPQ.position.set(0, 1.65, 0.1);
  nhom.add(ongPQ);
  const mm = []; // mạng mao mạch: mỗi phế nang được 2 vòng mao mạch ôm sát bề mặt (đầu tiểu động mạch xanh → đầu tiểu tĩnh mạch đỏ)
  tamPN.forEach((tam, iPN) => {
    for (let vong = 0; vong < 2; vong++) {
      const truc = V(r() - 0.5, r() - 0.5, r() - 0.5).normalize();
      const u = V(0, 1, 0).cross(truc).normalize();
      if (u.lengthSq() < 1e-6) u.set(1, 0, 0);
      const w = truc.clone().cross(u).normalize();
      const pts = [];
      for (let k = 0; k <= 16; k++) {
        const a = (k / 16) * Math.PI * 1.75;
        const p = u.clone().multiplyScalar(Math.cos(a)).add(w.clone().multiplyScalar(Math.sin(a))).multiplyScalar(0.64 + 0.03 * Math.sin(a * 5 + iPN));
        p.add(truc.clone().multiplyScalar(0.12 * Math.sin(a * 2 + vong)));
        pts.push(p.add(tam));
      }
      const duong = new THREE.CatmullRomCurve3(pts);
      const geo = new THREE.TubeGeometry(duong, 60, 0.035, 8, false);
      const mau = [];
      const c = new THREE.Color();
      const pos = geo.attributes.position;
      for (let v = 0; v < pos.count; v++) { const t = Math.floor(v / 9) / 60; c.setRGB(0.23 + 0.7 * t, 0.36 - 0.2 * t, 0.95 - 0.7 * t); mau.push(c.r, c.g, c.b); }
      geo.setAttribute("color", new THREE.Float32BufferAttribute(mau, 3));
      nhom.add(new THREE.Mesh(geo, new THREE.MeshPhysicalMaterial({ vertexColors: true, roughness: 0.35, clearcoat: 0.5 })));
      mm.push(duong);
    }
  });
  const soHC = 90;
  const hc = new THREE.InstancedMesh(cauBienDoi(V(0.06, 0.022, 0.06), (v) => { v.y *= 0.6 + 0.6 * (v.x * v.x + v.z * v.z); }), new THREE.MeshPhysicalMaterial({ roughness: 0.3, clearcoat: 0.6 }), soHC);
  hc.instanceColor = new THREE.InstancedBufferAttribute(new Float32Array(soHC * 3), 3);
  nhom.add(hc);
  const trangHC = Array.from({ length: soHC }, (_, i) => ({ d: i % mm.length, u: r() }));
  const o2 = hatDiChuyen(nhom, 70, 0x38bdf8, 0.08);
  const co2 = hatDiChuyen(nhom, 50, 0xcbd5e1, 0.08);
  const tuyen = (iMM) => { const d = mm[iMM]; const tam = tamPN[Math.floor(iMM / 2)]; return { trong: tam.clone().add(V(r() - 0.5, r() - 0.5, r() - 0.5).multiplyScalar(0.55)), mm: d.getPointAt(r()) }; };
  const tuyenO2 = Array.from({ length: 70 }, () => { const t = tuyen(Math.floor(r() * mm.length)); return { a: t.trong, b: t.mm, u: r() }; });
  const tuyenCO2 = Array.from({ length: 50 }, () => { const t = tuyen(Math.floor(r() * mm.length)); return { a: t.mm, b: t.trong, u: r() }; });
  nhanViMo(nhom, "Phế nang (Alveolus) – Ø ~0,2 mm", V(0.3, 1.1, 0.5));
  nhanViMo(nhom, "Mao mạch phổi: máu xanh → đỏ", V(1.45, -0.55, 0.5));
  nhanViMo(nhom, "Hồng cầu: Hb + 4O₂ ⇌ Hb(O₂)₄", V(-1.2, 0.5, 0.6));
  nhanViMo(nhom, "O₂ khuếch tán vào máu (100 → 40 mmHg)", V(-0.4, -1.2, 0.8));
  nhanViMo(nhom, "CO₂ ra phế nang (46 → 40 mmHg)", V(0.9, 1.1, -0.6));
  nhanViMo(nhom, "Tiểu phế quản hô hấp", V(0, 2.3, 0.1));
  const ma = new THREE.Matrix4(), q = new THREE.Quaternion(), s = V(1, 1, 1), p = V(0, 0, 0), c = new THREE.Color();
  return {
    nhom, ten: "Phế nang – trao đổi khí", mo_ta: "Màng hô hấp dày ~0,5 µm: O₂ (xanh) khuếch tán từ phế nang vào mao mạch, gắn Hb trong hồng cầu (đổi xanh → đỏ); CO₂ (xám) đi ngược lại. Tốc độ theo nhịp thở và pO₂.",
    camera: { vi_tri: V(3.0, 1.5, 4.6), dich: V(0.05, 0.25, 0) },
    capNhat(dt) {
      const nhanh = 0.15 * (sinhLy.nhipTim / 70);
      trangHC.forEach((h, i) => {
        h.u = (h.u + dt * nhanh) % 1;
        mm[h.d].getPointAt(h.u, p);
        q.setFromAxisAngle(V(0, 1, 0), h.u * 12);
        ma.compose(p, q, s);
        hc.setMatrixAt(i, ma);
        const chuyen = kep((h.u - 0.25) / 0.5, 0, 1) * kep((sinhLy.po2 - 30) / 70, 0.2, 1);
        c.setRGB(0.25 + 0.7 * chuyen, 0.3 - 0.2 * chuyen, 0.9 - 0.75 * chuyen);
        hc.setColorAt(i, c);
      });
      hc.instanceMatrix.needsUpdate = true;
      hc.instanceColor.needsUpdate = true;
      const tocKhi = 0.25 * (sinhLy.nhipTho / 14);
      tuyenO2.forEach((t, i) => { t.u = (t.u + dt * tocKhi * (sinhLy.po2 / 97)) % 1; p.lerpVectors(t.a, t.b, t.u); o2.vt.set([p.x, p.y, p.z], i * 3); });
      tuyenCO2.forEach((t, i) => { t.u = (t.u + dt * tocKhi * (sinhLy.pco2 / 40)) % 1; p.lerpVectors(t.a, t.b, t.u); co2.vt.set([p.x, p.y, p.z], i * 3); });
      o2.p.geometry.attributes.position.needsUpdate = true;
      co2.p.geometry.attributes.position.needsUpdate = true;
    },
  };
})();
// --- Nephron ---
viMo.nephron = (() => {
  const nhom = new THREE.Group();
  const cau = new THREE.Mesh(new THREE.TubeGeometry(new THREE.CatmullRomCurve3(Array.from({ length: 40 }, (_, i) => {
    const t = (i / 40) * Math.PI * 2; return V(Math.sin(t * 3) * 0.32, Math.cos(t * 5) * 0.28, Math.sin(t * 7) * 0.3).add(V(-1.6, 1.0, 0));
  }), true), 200, 0.055, 8, true), matVM(0xdc2626));
  const bowman = new THREE.Mesh(new THREE.SphereGeometry(0.55, 40, 28, 0, Math.PI * 2, 0, Math.PI * 0.82), matVM(0xfde68a, 0.3));
  bowman.position.set(-1.6, 1.0, 0);
  bowman.rotation.z = -Math.PI / 2;
  const ongThan = new THREE.CatmullRomCurve3([V(-1.05, 1.0, 0), V(-0.6, 1.3, 0.2), V(-0.3, 0.8, -0.2), V(0.0, 1.25, 0.2), V(0.25, 0.85, -0.1), V(0.35, 0.3, 0),
    V(0.35, -1.4, 0.05), V(0.55, -1.6, 0.05), V(0.75, -1.4, 0.05), V(0.75, 0.4, 0), V(1.0, 0.8, 0.2), V(1.3, 0.5, -0.2), V(1.55, 0.9, 0.1), V(1.8, 1.0, 0)]);
  const mOng = new THREE.Mesh(new THREE.TubeGeometry(ongThan, 260, 0.075, 12, false), matVM(0xfef3c7, 0.55));
  const ongGop = new THREE.Mesh(new THREE.CylinderGeometry(0.1, 0.1, 3.4, 20, 1, true), matVM(0xa7f3d0, 0.5));
  ongGop.position.set(1.95, -0.6, 0);
  const mmQuanh = new THREE.Mesh(new THREE.TubeGeometry(new THREE.CatmullRomCurve3([V(-1.3, 0.55, 0.5), V(-0.4, 0.4, 0.55), V(0.2, 0.0, 0.5), V(0.5, -1.0, 0.45), V(1.0, -0.5, 0.5), V(1.6, 0.2, 0.5)]), 120, 0.05, 8, false), matVM(0x60a5fa));
  const tdmDen = new THREE.Mesh(new THREE.TubeGeometry(new THREE.LineCurve3(V(-2.9, 1.5, 0), V(-1.9, 1.15, 0)), 4, 0.07, 10, false), matVM(0xef4444));
  const tdmDi = new THREE.Mesh(new THREE.TubeGeometry(new THREE.CatmullRomCurve3([V(-1.9, 0.85, 0), V(-2.0, 0.6, 0.3), V(-1.3, 0.55, 0.5)]), 20, 0.05, 10, false), matVM(0xb91c1c));
  nhom.add(cau, bowman, mOng, ongGop, mmQuanh, tdmDen, tdmDi);
  const loc = hatDiChuyen(nhom, 90, 0xfde047, 0.07);
  const hapThu = hatDiChuyen(nhom, 50, 0x4ade80, 0.07);
  const nuoc = hatDiChuyen(nhom, 30, 0x38bdf8, 0.06);
  const r = ngauNhien(21);
  const trangLoc = Array.from({ length: 90 }, () => r());
  const trangHT = Array.from({ length: 50 }, () => ({ u: r() * 0.35, t: r() }));
  const trangN = Array.from({ length: 30 }, () => ({ u: 0.42 + r() * 0.12, t: r() }));
  const p = V(0, 0, 0), q = V(0, 0, 0);
  nhanViMo(nhom, "Cầu thận: lọc ~125 mL/phút", V(-1.6, 1.65, 0));
  nhanViMo(nhom, "Nang Bowman", V(-2.15, 0.55, 0.2));
  nhanViMo(nhom, "Ống lượn gần: tái hấp thu glucose, 65% Na⁺", V(-0.2, 1.55, 0.2));
  nhanViMo(nhom, "Quai Henle: hấp thu nước, muối", V(0.55, -1.85, 0));
  nhanViMo(nhom, "Ống lượn xa: aldosterone", V(1.3, 1.25, 0));
  nhanViMo(nhom, "Ống góp: ADH tăng hấp thu nước", V(2.35, -1.6, 0));
  nhanViMo(nhom, "Mao mạch quanh ống", V(0.9, -0.9, 0.7));
  return {
    nhom, ten: "Nephron – đơn vị lọc máu", mo_ta: "Dịch lọc (vàng) từ cầu thận đi qua ống lượn gần – quai Henle – ống lượn xa – ống góp; glucose và Na⁺ (xanh lá) được tái hấp thu về mao mạch, nước (xanh dương) rời nhánh xuống quai Henle.",
    camera: { vi_tri: V(0.2, 0.1, 7.4), dich: V(-0.2, -0.15, 0) },
    capNhat(dt) {
      const toc = 0.06 * (0.6 + sinhLy.nhipTim / 160);
      trangLoc.forEach((u, i) => { trangLoc[i] = (u + dt * toc) % 1; ongThan.getPointAt(trangLoc[i], p); loc.vt.set([p.x, p.y, p.z], i * 3); });
      trangHT.forEach((h, i) => { h.t += dt * 0.5; if (h.t > 1) { h.t = 0; h.u = r() * 0.35; } ongThan.getPointAt(h.u, p); q.set(p.x * 0.85, p.y - 0.35, 0.5); p.lerp(q, h.t); hapThu.vt.set([p.x, p.y, p.z], i * 3); });
      trangN.forEach((h, i) => { h.t += dt * 0.6; if (h.t > 1) { h.t = 0; h.u = 0.42 + r() * 0.12; } ongThan.getPointAt(h.u, p); p.x -= h.t * 0.6; nuoc.vt.set([p.x, p.y, p.z], i * 3); });
      [loc, hapThu, nuoc].forEach((h) => { h.p.geometry.attributes.position.needsUpdate = true; });
    },
  };
})();
// --- Synapse ---
viMo.synapse = (() => {
  const nhom = new THREE.Group();
  const nut = new THREE.Mesh(new THREE.SphereGeometry(1.1, 48, 32, 0, Math.PI * 2, 0, Math.PI * 0.55), matVM(0xfde68a, 0.35));
  nut.position.y = 0.35;
  nut.rotation.x = Math.PI;
  const soi = new THREE.Mesh(new THREE.CylinderGeometry(0.28, 0.4, 1.8, 24, 1, true), matVM(0xfde68a, 0.35));
  soi.position.y = 1.8;
  const mangSau = new THREE.Mesh(new THREE.BoxGeometry(3.2, 0.12, 2.0), matVM(0x93c5fd, 0.55));
  mangSau.position.y = -1.15;
  const thuThe = [];
  for (let i = 0; i < 9; i++) {
    const k = new THREE.Mesh(new THREE.CylinderGeometry(0.07, 0.07, 0.22, 12), matVM(0x60a5fa, 1, { emissive: 0x1d4ed8, emissiveIntensity: 0.2 }));
    k.position.set(-1.2 + (i % 5) * 0.6, -1.03, i < 5 ? -0.35 : 0.35);
    thuThe.push(k);
    nhom.add(k);
  }
  const kenhCa = [];
  for (let i = 0; i < 4; i++) { const k = new THREE.Mesh(new THREE.CylinderGeometry(0.06, 0.06, 0.2, 10), matVM(0x4ade80)); k.position.set(-0.9 + i * 0.6, -0.48, 0.55 - (i % 2) * 1.1); kenhCa.push(k); nhom.add(k); }
  nhom.add(nut, soi, mangSau);
  const r = ngauNhien(31);
  const tui = Array.from({ length: 16 }, () => {
    const m = new THREE.Mesh(new THREE.SphereGeometry(0.12, 18, 12), matVM(0xa78bfa, 0.85, { emissive: 0x5b21b6, emissiveIntensity: 0.25 }));
    const goc = V((r() - 0.5) * 1.5, 0.2 + r() * 0.5, (r() - 0.5) * 1.2);
    m.position.copy(goc);
    nhom.add(m);
    return { m, goc, t: r() * 3 };
  });
  const ach = hatDiChuyen(nhom, 120, 0xf0abfc, 0.06);
  const na = hatDiChuyen(nhom, 60, 0xfacc15, 0.07);
  const ca = hatDiChuyen(nhom, 30, 0x4ade80, 0.07);
  const trangACh = Array.from({ length: 120 }, () => ({ s: V(0, -0.5, 0), e: V(0, -1.05, 0), t: 2 }));
  const trangNa = Array.from({ length: 60 }, (_, i) => ({ k: i % 9, t: r() }));
  const trangCa = Array.from({ length: 30 }, (_, i) => ({ k: i % 4, t: r() }));
  let demPhat = 0;
  nhanViMo(nhom, "Chuỳ synapse (màng trước)", V(-0.3, 0.95, 0.9));
  nhanViMo(nhom, "Bóng chứa acetylcholine", V(-0.9, 0.55, 0.6));
  nhanViMo(nhom, "Ca²⁺ vào → xuất bào", V(1.2, -0.45, 0.6));
  nhanViMo(nhom, "Khe synapse ~20–40 nm", V(1.05, -0.85, 0.4));
  nhanViMo(nhom, "Thụ thể màng sau: kênh Na⁺ mở", V(-1.3, -1.35, 0.6));
  nhanViMo(nhom, "AChE phân giải ACh → choline + acetic acid", V(0.4, -1.55, 0.9));
  return {
    nhom, ten: "Synapse thần kinh – cơ", mo_ta: "Xung thần kinh tới chuỳ synapse → Ca²⁺ (xanh lá) đi vào → bóng (tím) giải phóng acetylcholine (hồng) vào khe → gắn thụ thể, mở kênh Na⁺ (vàng) → xuất hiện xung ở màng sau; AChE phân giải ACh. Tần số theo trạng thái giao cảm.",
    camera: { vi_tri: V(2.6, 0.4, 3.8), dich: V(0, -0.3, 0) },
    capNhat(dt, t) {
      demPhat += dt * (1.2 + 3.5 * sinhLy.giaoCam);
      if (demPhat > 1) {
        demPhat = 0;
        const tv = tui[Math.floor(Math.random() * tui.length)];
        tv.t = 0;
        let them = 0;
        for (const a of trangACh) if (a.t >= 1 && them < 14) { a.s.set(tv.goc.x, -0.5, tv.goc.z); a.e.set((Math.random() - 0.5) * 2.4, -1.05, (Math.random() - 0.5) * 1.2); a.t = 0; them++; }
      }
      tui.forEach((v) => { v.t += dt; const pha = kep(v.t / 0.8, 0, 1); v.m.position.set(v.goc.x, v.goc.y - pha * (v.goc.y + 0.45), v.goc.z); v.m.scale.setScalar(pha < 1 ? 1 : kep((v.t - 0.8) / 1.5, 0.05, 1)); });
      const p = V(0, 0, 0);
      trangACh.forEach((a, i) => { if (a.t < 1) a.t += dt * 0.9; p.lerpVectors(a.s, a.e, kep(a.t, 0, 1)); if (a.t >= 1) p.set(0, -50, 0); ach.vt.set([p.x, p.y, p.z], i * 3); });
      const kichHoat = trangACh.filter((a) => a.t < 1).length / 30;
      thuThe.forEach((k) => { k.material.emissiveIntensity = 0.2 + kep(kichHoat, 0, 1) * 1.2; });
      trangNa.forEach((n, i) => { n.t = (n.t + dt * (0.3 + kep(kichHoat, 0, 1.5))) % 1; const k = thuThe[n.k].position; na.vt.set([k.x + Math.sin(i) * 0.05, -0.75 - n.t * 0.9, k.z + Math.cos(i) * 0.05], i * 3); });
      trangCa.forEach((n, i) => { n.t = (n.t + dt * (0.35 + 0.6 * sinhLy.giaoCam)) % 1; const k = kenhCa[n.k].position; ca.vt.set([k.x, -0.85 + n.t * 0.9, k.z], i * 3); });
      [ach, na, ca].forEach((h) => { h.p.geometry.attributes.position.needsUpdate = true; });
      nut.material.emissive.setHex(0xffd166);
      nut.material.emissiveIntensity = 0.15 + 0.15 * Math.max(0, Math.sin(t * 6));
    },
  };
})();
// --- Sarcomere cơ tim ---
viMo.sarcomere = (() => {
  const nhom = new THREE.Group();
  const z1 = new THREE.Mesh(new THREE.BoxGeometry(0.08, 2.2, 1.6), matVM(0xfca5a5));
  const z2 = z1.clone();
  const actin = [], myosin = [];
  for (let i = 0; i < 6; i++) {
    const y = -0.8 + (i % 3) * 0.8, z = i < 3 ? -0.35 : 0.35;
    for (const s of [-1, 1]) {
      const a = new THREE.Mesh(new THREE.CylinderGeometry(0.035, 0.035, 1.5, 10), matVM(0xf87171));
      a.rotation.z = Math.PI / 2;
      a.userData = { s, y, z };
      actin.push(a);
      nhom.add(a);
    }
  }
  for (let i = 0; i < 4; i++) {
    const g = new THREE.Group();
    const than = new THREE.Mesh(new THREE.CylinderGeometry(0.07, 0.07, 1.9, 12), matVM(0x7c3aed));
    than.rotation.z = Math.PI / 2;
    g.add(than);
    for (let k = 0; k < 10; k++) {
      const dau = new THREE.Mesh(new THREE.ConeGeometry(0.05, 0.16, 8), matVM(0xc4b5fd));
      const x = -0.85 + k * 0.19;
      dau.position.set(x, 0.12 * (k % 2 ? 1 : -1), 0);
      dau.userData.x = x;
      g.add(dau);
    }
    g.position.set(0, -0.4 + (i % 2) * 0.8, i < 2 ? -0.35 : 0.35);
    myosin.push(g);
    nhom.add(g);
  }
  nhom.add(z1, z2);
  const ca = hatDiChuyen(nhom, 60, 0x4ade80, 0.07);
  const atp = hatDiChuyen(nhom, 30, 0xfacc15, 0.08);
  const r = ngauNhien(41);
  const trangCa = Array.from({ length: 60 }, () => V((r() - 0.5) * 3, (r() - 0.5) * 2, (r() - 0.5) * 1.4));
  const trangATP = Array.from({ length: 30 }, () => V((r() - 0.5) * 2, (r() - 0.5) * 1.8, (r() - 0.5) * 1.2));
  nhanViMo(nhom, "Đĩa Z", V(-1.6, 1.25, 0));
  nhanViMo(nhom, "Actin (sợi mảnh)", V(-0.9, -1.2, 0.5));
  nhanViMo(nhom, "Myosin (sợi dày) – đầu chèo", V(0.2, 0.75, 0.6));
  nhanViMo(nhom, "Ca²⁺ mở vị trí bám → co", V(1.3, 1.1, 0.5));
  nhanViMo(nhom, "ATP → ADP + Pᵢ (năng lượng)", V(1.2, -1.15, 0.4));
  return {
    nhom, ten: "Sarcomere cơ tim", mo_ta: "Mỗi nhịp tim: Ca²⁺ (xanh lá) tăng → đầu myosin bám và kéo actin về giữa, hai đĩa Z xích lại (~10–15%); ATP (vàng) cung cấp năng lượng. Đồng bộ với nhịp tim trên HUD.",
    camera: { vi_tri: V(0.4, 0.5, 4.4), dich: V(0, 0, 0) },
    capNhat(dt) {
      const p = sinhLy.phaTim % 1;
      const co = p > 0.13 && p < 0.45 ? Math.sin(Math.PI * (p - 0.13) / 0.32) : 0;
      const nua = 1.55 - 0.22 * co;
      z1.position.x = -nua; z2.position.x = nua;
      actin.forEach((a) => { a.position.set(a.userData.s * (nua - 0.75), a.userData.y, a.userData.z); });
      myosin.forEach((g) => g.children.forEach((d, k) => { if (k) d.rotation.z = Math.sin(thoiGian * 14 + k) * 0.5 * co + (d.userData.x > 0 ? -0.6 : 0.6); }));
      trangCa.forEach((v, i) => { const s = 0.2 + co; ca.vt.set([v.x * s, v.y * (0.4 + 0.6 * s), v.z], i * 3); });
      trangATP.forEach((v, i) => { v.x += dt * 0.4; if (v.x > 1) v.x = -1; atp.vt.set([v.x, v.y, v.z], i * 3); });
      ca.p.geometry.attributes.position.needsUpdate = true;
      atp.p.geometry.attributes.position.needsUpdate = true;
    },
  };
})();
// --- Tế bào viền dạ dày ---
viMo.te_bao_vien = (() => {
  const nhom = new THREE.Group();
  const teBao = new THREE.Mesh(cauBienDoi(V(1.4, 1.2, 1.1)), matVM(0xfda4af, 0.28));
  const long = new THREE.Mesh(new THREE.BoxGeometry(4.5, 0.08, 3), matVM(0xfef08a, 0.25));
  long.position.y = 1.5;
  const tieuQuan = new THREE.Mesh(new THREE.TubeGeometry(new THREE.CatmullRomCurve3([V(0, 1.45, 0), V(0.1, 0.8, 0.1), V(-0.2, 0.2, -0.1), V(0.3, -0.3, 0.1), V(0.6, 0.1, 0.4)]), 80, 0.16, 12, false), matVM(0xfef3c7, 0.55));
  nhom.add(teBao, long, tieuQuan);
  const duongTQ = tieuQuan.geometry.parameters.path;
  const bom = [];
  for (let i = 0; i < 8; i++) {
    const p = duongTQ.getPointAt((i + 0.5) / 8);
    const m = new THREE.Mesh(new THREE.CylinderGeometry(0.06, 0.06, 0.2, 10), matVM(0x22d3ee, 1, { emissive: 0x0891b2, emissiveIntensity: 0.4 }));
    m.position.copy(p).add(V(0.17, 0, 0));
    m.rotation.z = Math.PI / 2;
    bom.push(m);
    nhom.add(m);
  }
  const hH = hatDiChuyen(nhom, 80, 0xf43f5e, 0.07);
  const hK = hatDiChuyen(nhom, 40, 0xa78bfa, 0.07);
  const hCl = hatDiChuyen(nhom, 40, 0x4ade80, 0.07);
  const hHCO3 = hatDiChuyen(nhom, 40, 0x93c5fd, 0.07);
  const r = ngauNhien(51);
  const tH = Array.from({ length: 80 }, () => ({ b: Math.floor(r() * 8), t: r() }));
  const tK = Array.from({ length: 40 }, () => ({ b: Math.floor(r() * 8), t: r() }));
  const tCl = Array.from({ length: 40 }, () => ({ t: r(), x: (r() - 0.5) * 0.4, z: (r() - 0.5) * 0.4 }));
  const tB = Array.from({ length: 40 }, () => ({ t: r(), x: (r() - 0.5) * 2, z: (r() - 0.5) * 1.6 }));
  nhanViMo(nhom, "Lòng dạ dày: pH 1,5–3,5", V(-1.6, 1.75, 0.6));
  nhanViMo(nhom, "Bơm H⁺ (đổi lấy K⁺)", V(0.9, 0.4, 0.6));
  nhanViMo(nhom, "Ống tiểu quản tiết HCl", V(-0.6, 0.9, 0.4));
  nhanViMo(nhom, "CO₂ + H₂O → H⁺ + HCO₃⁻ (CA)", V(-1.1, -0.4, 0.8));
  nhanViMo(nhom, "HCO₃⁻ ra máu (triều kiềm)", V(0.2, -1.45, 0.6));
  return {
    nhom, ten: "Tế bào viền dạ dày", mo_ta: "Enzyme trong tế bào tạo H⁺ + HCO₃⁻ từ CO₂ và H₂O; bơm H⁺ (xanh ngọc) đẩy H⁺ (đỏ) vào ống tiểu quản – lấy K⁺ (tím) vào; Cl⁻ (xanh lá) đi theo tạo HCl; HCO₃⁻ ra máu. Thần kinh đối giao cảm làm tăng tiết.",
    camera: { vi_tri: V(2.4, 0.9, 3.6), dich: V(0, 0.2, 0) },
    capNhat(dt) {
      const toc = 0.35 * (1.2 - 0.6 * sinhLy.giaoCam);
      const p = V(0, 0, 0);
      tH.forEach((h, i) => {  // H⁺ từ bào tương → qua bơm vào ống tiểu quản → trôi lên lòng dạ dày
        h.t += dt * toc;
        if (h.t > 1) { h.t = 0; h.b = Math.floor(Math.random() * 8); }
        const b = bom[h.b].position, uBom = (h.b + 0.5) / 8;
        if (h.t < 0.5) p.set(b.x + 0.5 - h.t, b.y, b.z);
        else duongTQ.getPointAt(uBom * (1 - (h.t - 0.5) * 2), p);
        hH.vt.set([p.x, p.y, p.z], i * 3);
      });
      tK.forEach((h, i) => { h.t = (h.t + dt * toc) % 1; const b = bom[h.b].position; hK.vt.set([b.x + h.t * 0.6, b.y + 0.05, b.z], i * 3); });
      tCl.forEach((h, i) => { h.t = (h.t + dt * toc * 0.8) % 1; duongTQ.getPointAt(1 - h.t, p); hCl.vt.set([p.x + h.x, p.y, p.z + h.z], i * 3); });
      tB.forEach((h, i) => { h.t = (h.t + dt * toc * 0.6) % 1; hHCO3.vt.set([h.x, -0.2 - h.t * 1.3, h.z], i * 3); });
      [hH, hK, hCl, hHCO3].forEach((h) => { h.p.geometry.attributes.position.needsUpdate = true; });
      bom.forEach((b, i) => { b.material.emissiveIntensity = 0.3 + 0.4 * Math.max(0, Math.sin(thoiGian * 5 + i)); });
    },
  };
})();
// --- Tiểu thuỳ gan ---
viMo.tieu_thuy_gan = (() => {
  const nhom = new THREE.Group();
  const luc = new THREE.Mesh(new THREE.CylinderGeometry(1.6, 1.6, 0.5, 6, 1, false), matVM(0x9a3412, 0.28));
  const tmTrungTam = new THREE.Mesh(new THREE.CylinderGeometry(0.16, 0.16, 0.7, 24), matVM(0x3b82f6));
  nhom.add(luc, tmTrungTam);
  const goc = Array.from({ length: 6 }, (_, i) => V(Math.cos(i * Math.PI / 3) * 1.6, 0, Math.sin(i * Math.PI / 3) * 1.6));
  goc.forEach((g) => {
    [[0x7c3aed, V(0, 0, 0)], [0xef4444, V(0.1, 0, 0.1)], [0x22c55e, V(-0.1, 0, 0.08)]].forEach(([mau, lech]) => {
      const o = new THREE.Mesh(new THREE.CylinderGeometry(0.06, 0.06, 0.7, 12), matVM(mau));
      o.position.copy(g).add(lech);
      nhom.add(o);
    });
  });
  for (let i = 0; i < 12; i++) {
    const a = (i / 12) * Math.PI * 2 + 0.26;
    const tam = new THREE.Mesh(new THREE.BoxGeometry(1.2, 0.36, 0.07), matVM(0xc2410c, 0.6));
    tam.position.set(Math.cos(a) * 0.85, 0, Math.sin(a) * 0.85);
    tam.rotation.y = -a;
    nhom.add(tam);
  }
  const mau = hatDiChuyen(nhom, 120, 0xe11d48, 0.07);
  const mat = hatDiChuyen(nhom, 60, 0x4ade80, 0.06);
  const hatUrea = hatDiChuyen(nhom, 30, 0xfef08a, 0.06);
  const r = ngauNhien(61);
  const tM = Array.from({ length: 120 }, () => ({ g: Math.floor(r() * 6), t: r(), l: (r() - 0.5) * 0.3 }));
  const tMat = Array.from({ length: 60 }, () => ({ g: Math.floor(r() * 6), t: r(), l: (r() - 0.5) * 0.2 }));
  const tU = Array.from({ length: 30 }, () => ({ g: Math.floor(r() * 6), t: r() }));
  nhanViMo(nhom, "Tĩnh mạch trung tâm", V(0, 0.55, 0));
  nhanViMo(nhom, "Bộ ba cửa: TM cửa · ĐM gan · ống mật", V(1.6, 0.5, 0));
  nhanViMo(nhom, "Xoang gan: máu chảy vào trung tâm", V(-0.9, 0.45, 0.8));
  nhanViMo(nhom, "Mật chảy ngược ra ống mật", V(-1.1, 0.35, -1.0));
  nhanViMo(nhom, "Tế bào gan: 2NH₃ + CO₂ → urea", V(0.9, -0.45, -0.6));
  return {
    nhom, ten: "Tiểu thuỳ gan", mo_ta: "Máu (đỏ) từ bộ ba cửa ở 6 góc chảy qua xoang gan về tĩnh mạch trung tâm; tế bào gan chuyển NH₃ thành urea (vàng), dự trữ glycogen, oxi hoá ethanol; mật (xanh lá) chảy ngược ra ống mật.",
    camera: { vi_tri: V(0, 3.6, 3.2), dich: V(0, 0, 0) },
    capNhat(dt) {
      const p = V(0, 0, 0);
      tM.forEach((h, i) => { h.t = (h.t + dt * 0.22 * (sinhLy.nhipTim / 70)) % 1; p.lerpVectors(goc[h.g], V(0, 0, 0), h.t); p.y = h.l * 0.5; p.x += h.l * 0.3; mau.vt.set([p.x, p.y, p.z], i * 3); });
      tMat.forEach((h, i) => { h.t = (h.t + dt * 0.12) % 1; p.lerpVectors(V(0, 0, 0), goc[h.g], 0.15 + h.t * 0.85); p.y = 0.21; p.z += h.l; mat.vt.set([p.x, p.y, p.z], i * 3); });
      tU.forEach((h, i) => { h.t = (h.t + dt * 0.2) % 1; p.lerpVectors(goc[h.g], V(0, 0, 0), 0.3 + h.t * 0.7); p.y = -0.2; hatUrea.vt.set([p.x, p.y, p.z], i * 3); });
      [mau, mat, hatUrea].forEach((h) => { h.p.geometry.attributes.position.needsUpdate = true; });
    },
  };
})();
Object.values(viMo).forEach((c) => { c.nhom.visible = false; nhomViMo.add(c.nhom); });
const anhSangViMo = new THREE.PointLight(0xffffff, 18, 0, 2);
anhSangViMo.position.set(2, 3, 4);
nhomViMo.add(anhSangViMo);
const VI_MO_THEO_HE = { tuan_hoan: "sarcomere", ho_hap: "pho_nang", than_kinh: "synapse", bai_tiet: "nephron", tieu_hoa: "te_bao_vien" };
const VI_MO_THEO_BP = { gan: "tieu_thuy_gan", tui_mat: "tieu_thuy_gan" };
let cameraTruocViMo = null;
async function vaoViMo(ten) {
  const c = viMo[ten];
  if (!c) return;
  xoaNhan();
  xoaLoTrinh();
  if (!cheDoViMo) cameraTruocViMo = { vi_tri: camera.position.clone(), dich: controls.target.clone() };
  Object.entries(viMo).forEach(([k, v]) => { v.nhom.visible = k === ten; (v.nhom.userData.nhan || []).forEach((n) => { n.visible = k === ten; }); });
  cheDoViMo = ten;
  theGioi.visible = false;
  moiTruong.visible = false;
  datNen(MAU_NEN.vi_mo);
  scene.fog.density = 0.0;
  $("#vm-tieu-de").textContent = "🔬 " + c.ten;
  $("#vm-mo-ta").textContent = c.mo_ta;
  $("#hop-vi-mo").classList.remove("an");
  $("#nut-hien-vi").setAttribute("aria-pressed", "true");
  const goc = nhomViMo.position;
  camera.position.copy(c.camera.vi_tri.clone().add(goc).add(V(0, 0, 6)));
  controls.target.copy(c.camera.dich.clone().add(goc));
  bayToi(c.camera.dich.clone().add(goc), c.camera.vi_tri.clone().add(goc), 1.6);
}
function thoatViMo(bayVe = true) {
  if (!cheDoViMo) return;
  Object.values(viMo).forEach((v) => { v.nhom.visible = false; (v.nhom.userData.nhan || []).forEach((n) => { n.visible = false; }); });
  cheDoViMo = null;
  theGioi.visible = true;
  moiTruong.visible = true;
  datMoiTruong(xQuang ? "xray" : cheDoMoiTruong);
  $("#hop-vi-mo").classList.add("an");
  $("#nut-hien-vi").setAttribute("aria-pressed", "false");
  const ve = cameraTruocViMo || CAMERA_GOC;
  camera.position.copy(ve.vi_tri.clone().add(V(0, 0, 1.5)));
  controls.target.copy(ve.dich);
  if (bayVe) bayToi(ve.dich, ve.vi_tri, 1.2);
}
$("#nut-thoat-vi-mo").addEventListener("click", () => thoatViMo(true));
$("#nut-hien-vi").addEventListener("click", () => {
  if (cheDoViMo) { thoatViMo(true); return; }
  const bp = bpDangChon;
  if (!bp) { thongBao("Hãy chọn một cơ quan trên mô hình (hoặc trong danh sách hệ) trước khi soi hiển vi."); return; }
  const ten = VI_MO_THEO_BP[bp] || VI_MO_THEO_HE[tenBP(bp).he];
  if (!ten) { thongBao("Bộ phận này chưa có mô phỏng hiển vi – hãy chọn tim, phổi, não, thận, gan hoặc dạ dày."); return; }
  vaoViMo(ten);
});

