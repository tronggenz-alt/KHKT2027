// Phòng thí nghiệm Hoá ảo – NÂNG CẤP PHÒNG THỰC TẾ (bản Web 3D). phong-mo.js gọi nangCapPhong(this) sau khi dựng xong phòng:
//  • đổi vật liệu phẳng thành vật liệu có texture thật (vat-lieu-thuc.js): sàn vinyl có ron, mặt bàn epoxy có hạt, gỗ sồi, tường sơn, trần ô vuông, inox chải
//  • thêm chi tiết như phòng thí nghiệm ngoài đời: khe cánh tủ + tay nắm + chân đế, vòi cổ ngỗng liền mạch + núm nước + lòng chậu, ổ cắm + van gas,
//    lọ thuốc thử xếp trên giá (InstancedMesh, dùng chung hình lọ với dụng cụ), khung cửa sổ nhôm + bệ cửa, đồng hồ, khung cửa chính + biển lối ra,
//    khung đèn trần, đầu phun chữa cháy, cửa gió.
// Mọi con số kích thước là mét; bản Unity (ToaNha/ChiTietThuc.cs của Gemini) chép theo các hằng số trong file này.
// Không sửa dữ liệu hay luật chơi; chỉ thêm mesh / gán texture, nên bỏ nangCapPhong() là về đúng bản cũ.
import * as THREE from "three";
import * as VL from "ptn/vat-lieu-thuc";

const HH = window.PTNHinhHoc;

export function nangCapPhong(phong) {
  const { rong = 22, sau = 16, cao = 4.2 } = phong.banDo?.phong || {};
  const tg = (o) => phong._theo(o);
  const caoCL = phong.chatLuong === "cao";
  const nha = new THREE.Group();
  nha.name = "ChiTietThuc";
  phong.goc.add(nha);
  const hopMat = (mau, nham = 0.6, kl = 0.05, them = {}) => tg(new THREE.MeshStandardMaterial({ color: mau, roughness: nham, metalness: kl, ...them }));
  const hop = (w, h, d, vl, x, y, z, cha = nha, doDe = false) => {
    const m = new THREE.Mesh(tg(new THREE.BoxGeometry(w, h, d)), vl);
    m.position.set(x, y, z);
    m.castShadow = doDe && caoCL;
    m.receiveShadow = true;
    cha.add(m);
    return m;
  };
  const tru = (rt, rd, h, vl, x, y, z, cha = nha, seg = 20) => {
    const m = new THREE.Mesh(tg(new THREE.CylinderGeometry(rt, rd, h, seg)), vl);
    m.position.set(x, y, z);
    cha.add(m);
    return m;
  };
  const lap = (t, rx, ry) => { for (const k of ["map", "roughnessMap", "bumpMap"]) if (t[k]) t[k].repeat.set(rx, ry); };

  // ============================================================ 1. VẬT LIỆU CÓ TEXTURE
  const san = VL.sanVinyl();
  Object.assign(phong.vlSan, { map: san.map, roughnessMap: san.roughnessMap, bumpMap: san.bumpMap, bumpScale: 0.9, roughness: 1, metalness: 0.02 });
  for (const t of [san.map, san.roughnessMap, san.bumpMap]) { tg(t); t.repeat.set(rong / 1.8, sau / 1.8); }
  phong.vlSan.needsUpdate = true;

  const tran = VL.tranTam();
  phong.vlTran.map = tg(tran.map);
  tran.map.repeat.set(rong / 1.8, sau / 1.8);
  phong.vlTran.needsUpdate = true;

  const tuong = VL.tuongSon();
  phong.vlTuong.map = tg(tuong.map);
  tuong.map.repeat.set(rong / 4, 1.2);
  phong.vlTuong.needsUpdate = true;

  const epoxy = VL.matBanEpoxy();
  Object.assign(phong.vlMocBan, { map: tg(epoxy.map), roughnessMap: tg(epoxy.roughnessMap), color: new THREE.Color(0xffffff), roughness: 0.55, metalness: 0.04, envMapIntensity: 0.9 });
  phong.vlMocBan.needsUpdate = true;

  const so = VL.goSoi();
  Object.assign(phong.vlGoTu, { map: tg(so.map), roughnessMap: tg(so.roughnessMap), color: new THREE.Color(0xffffff), roughness: 1 });
  phong.vlGoTu.needsUpdate = true;

  const chai = VL.inoxChai();
  tg(chai.roughnessMap);
  for (const vl of [phong.vlInox, phong.vlChanThep]) { vl.roughnessMap = chai.roughnessMap; vl.needsUpdate = true; }

  // ============================================================ 2. CHI TIẾT MỖI BÀN
  const vlKhe = hopMat(0x0f172a, 0.9, 0);
  const vlDen = hopMat(0x1f2937, 0.7, 0.2);
  const vlTrang = hopMat(0xf1f5f9, 0.5, 0);
  const vlVang = hopMat(0xf5b301, 0.45, 0.2);
  const vlXanh = hopMat(0x2563eb, 0.4, 0.1);
  const vlDo = hopMat(0xdc2626, 0.4, 0.1);
  const vlLongChau = hopMat(0x1c242e, 0.35, 0.7);
  const vlLabel = tg(new THREE.MeshStandardMaterial({ color: 0xf8fafc, roughness: 0.8, side: THREE.DoubleSide }));

  // hình lọ thuốc thử dùng CHUNG số liệu với dụng cụ trên bàn (hinh-hoc.js)
  const hinhLo = (() => {
    const m = HH.latheMem(HH.DUNG_CU.loThuoc(0.026, 0.13, 0.011), { phan: 20 });
    const g = new THREE.BufferGeometry();
    g.setAttribute("position", new THREE.BufferAttribute(m.v, 3));
    g.setAttribute("normal", new THREE.BufferAttribute(m.n, 3));
    g.setIndex(new THREE.BufferAttribute(m.i, 1));
    return tg(g);
  })();
  const hinhNap = tg(new THREE.CylinderGeometry(0.0125, 0.0125, 0.022, 14));
  const hinhNhan = tg(new THREE.CylinderGeometry(0.0268, 0.0268, 0.05, 20, 1, true));
  const vlLo = tg(new THREE.MeshPhysicalMaterial({ color: 0xffffff, roughness: 0.14, clearcoat: 0.9, clearcoatRoughness: 0.08, envMapIntensity: 1.1 }));
  const vlNap = hopMat(0xffffff, 0.5, 0.05);
  const MAU_LO = [0x8a4b14, 0x7a3f10, 0x9a5a1c, 0x5aa078, 0x3b6fc4, 0x8a4b14, 0xb9c4d0];
  const MAU_NAP = [0x111827, 0xf8fafc, 0x2563eb, 0x111827, 0xdc2626];

  const dsBan = phong.goc.children.filter((c) => c.isGroup && c.children.some((m) => m.userData?.loai === "ban"));
  for (const ban of dsBan) {
    const top = ban.children.find((m) => m.userData?.loai === "ban");
    // RoundedBoxGeometry không cập nhật .parameters (luôn 1×1×1) → đo bằng hộp bao
    top.geometry.computeBoundingBox();
    const kt = top.geometry.boundingBox.getSize(new THREE.Vector3());
    const dai = kt.x, rg = kt.z;
    const zTruoc = (rg - 0.2) / 2 + 0.0012;           // mặt trước thân tủ
    const zSau = -rg / 2;

    // --- thân tủ: khe cánh + ngăn kéo + tay nắm + chân đế (plinth)
    for (const dau of [-1, 1]) {
      const xc = dau * (dai / 2 - 0.45);
      hop(0.004, 0.46, 0.002, vlKhe, xc, 0.34, zTruoc, ban);               // khe giữa hai cánh
      hop(0.64, 0.004, 0.002, vlKhe, xc, 0.615, zTruoc, ban);              // khe trên cánh / dưới ngăn kéo
      hop(0.64, 0.004, 0.002, vlKhe, xc, 0.13, zTruoc, ban);               // khe dưới
      hop(0.14, 0.012, 0.016, vlTrang, xc, 0.695, zTruoc + 0.01, ban);     // tay nắm ngăn kéo
      for (const s of [-1, 1]) hop(0.012, 0.16, 0.016, vlTrang, xc + s * 0.03, 0.4, zTruoc + 0.01, ban); // tay nắm cánh
      hop(0.65, 0.12, rg - 0.34, vlDen, xc, 0.06, 0, ban);                 // chân đế sẫm màu
    }
    hop(dai - 0.1, 0.035, 0.012, vlDen, 0, 0.822, rg / 2 - 0.02, ban);     // thanh viền thép dưới mép mặt bàn

    // --- chậu rửa: lòng chậu tối + cửa thoát nước + vòi cổ ngỗng liền mạch + hai núm nước
    const sx = -dai / 2 + 0.35, sz = -rg / 4;
    const cu = ban.children.find((m) => m.geometry?.type === "TorusGeometry");
    if (cu) cu.visible = false;                                           // vòi cũ (cung tròn gãy khúc)
    const loChau = new THREE.Mesh(tg(new THREE.PlaneGeometry(0.34, 0.34)), vlLongChau);
    loChau.rotation.x = -Math.PI / 2;
    loChau.position.set(sx, 0.9035, sz);
    ban.add(loChau);
    tru(0.024, 0.024, 0.0025, phong.vlInox, sx, 0.9045, sz, ban);        // cửa thoát nước
    const zv = sz - 0.235;
    tru(0.032, 0.036, 0.03, phong.vlInox, sx, 0.915, zv, ban);             // đế vòi
    const duong = new THREE.CatmullRomCurve3([[0, 0.93, 0], [0, 1.2, 0], [0, 1.31, 0.035], [0, 1.33, 0.13], [0, 1.27, 0.215], [0, 1.18, 0.235]].map(([x, y, z]) => new THREE.Vector3(sx + x, y, zv + z)));
    const co = new THREE.Mesh(tg(new THREE.TubeGeometry(duong, 56, 0.0115, 14, false)), phong.vlInox);
    co.castShadow = caoCL;
    ban.add(co);
    tru(0.0175, 0.014, 0.04, phong.vlInox, sx, 1.165, zv + 0.235, ban);    // đầu vòi
    for (const [dx, vl] of [[-0.085, vlXanh], [0.085, vlDo]]) {
      tru(0.013, 0.015, 0.05, phong.vlInox, sx + dx, 0.93, zv + 0.01, ban);
      const tay = tru(0.006, 0.006, 0.07, vl, sx + dx, 0.965, zv + 0.01, ban);
      tay.rotation.z = Math.PI / 2;
    }

    // --- ổ cắm điện + van gas trên gờ chắn nước phía sau
    for (const x of [-0.55, 0.45]) {
      hop(0.085, 0.058, 0.008, vlTrang, x, 0.94, zSau + 0.0235, ban);
      for (const dx of [-0.02, 0.02]) hop(0.016, 0.022, 0.003, vlKhe, x + dx, 0.945, zSau + 0.0285, ban);
    }
    for (const x of [-0.25, 0.1]) {
      tru(0.009, 0.009, 0.06, phong.vlChanThep, x, 0.93, zSau + 0.06, ban, 12);
      tru(0.017, 0.017, 0.016, vlVang, x, 0.968, zSau + 0.06, ban, 12);
    }

    // --- lọ thuốc thử xếp trên hai giá (InstancedMesh: 3 lượt vẽ cho cả hàng)
    const ZG = zSau + 0.1;
    for (const yGia of [1.18, 1.42]) {
      // chừa khoảng giữa giá trống (|x| < 0,3 m) để không che dụng cụ thí nghiệm đặt ở giữa bàn
      const xs = [-0.7, -0.6, -0.5, -0.4, -0.3, 0.3, 0.4, 0.5, 0.6, 0.7], N = xs.length;
      const lo = new THREE.InstancedMesh(hinhLo, vlLo, N), nap = new THREE.InstancedMesh(hinhNap, vlNap, N), nhan = new THREE.InstancedMesh(hinhNhan, vlLabel, N);
      const M = new THREE.Matrix4(), C = new THREE.Color();
      for (let i = 0; i < N; i++) {
        const r = (k) => { const h = Math.sin((i + 1) * 12.9898 + k * 78.233 + yGia * 43.1) * 43758.5453; return h - Math.floor(h); };
        const xl = xs[i] + (r(1) - 0.5) * 0.012, s = 0.82 + r(2) * 0.3, ang = r(3) * 6.28;
        M.compose(new THREE.Vector3(xl, yGia + 0.01, ZG + (r(4) - 0.5) * 0.02), new THREE.Quaternion().setFromAxisAngle(new THREE.Vector3(0, 1, 0), ang), new THREE.Vector3(1, s, 1));
        lo.setMatrixAt(i, M); lo.setColorAt(i, C.setHex(MAU_LO[Math.floor(r(5) * MAU_LO.length)]));
        M.compose(new THREE.Vector3(xl, yGia + 0.01 + 0.13 * s + 0.007, ZG), new THREE.Quaternion(), new THREE.Vector3(1, 1, 1));
        nap.setMatrixAt(i, M); nap.setColorAt(i, C.setHex(MAU_NAP[Math.floor(r(6) * MAU_NAP.length)]));
        M.compose(new THREE.Vector3(xl, yGia + 0.01 + 0.052 * s, ZG), new THREE.Quaternion().setFromAxisAngle(new THREE.Vector3(0, 1, 0), ang), new THREE.Vector3(1, s, 1));
        nhan.setMatrixAt(i, M);
      }
      for (const m of [lo, nap, nhan]) { m.instanceMatrix.needsUpdate = true; m.castShadow = false; m.receiveShadow = true; if (m.instanceColor) m.instanceColor.needsUpdate = true; ban.add(m); }
      if (!caoCL) { lo.visible = nap.visible = nhan.visible = false; }
      phong._doCao.push(lo, nap, nhan);
    }
  }

  // ============================================================ 3. TƯỜNG, CỬA SỔ, CỬA CHÍNH, ĐỒNG HỒ
  const vlNhom = hopMat(0xe5e7eb, 0.4, 0.6, { roughnessMap: chai.roughnessMap });
  const vlKhungCua = hopMat(0x374151, 0.5, 0.6, { roughnessMap: chai.roughnessMap });
  // khung cửa sổ Bắc: ô kính 2 m, thanh đứng + thanh ngang + bệ cửa
  const wr = rong - 2, wx0 = 1, yDuoi = 1.0, yTren = 3.6, zc = -0.04;
  for (let i = 0; i <= Math.round(wr / 2); i++) hop(0.06, yTren - yDuoi, 0.08, vlNhom, wx0 + (wr / Math.round(wr / 2)) * i, (yDuoi + yTren) / 2, zc);
  hop(wr + 0.06, 0.07, 0.08, vlNhom, rong / 2, yDuoi, zc);
  hop(wr + 0.06, 0.07, 0.08, vlNhom, rong / 2, yTren, zc);
  hop(wr + 0.06, 0.05, 0.05, vlNhom, rong / 2, 2.3, zc);
  hop(wr + 0.3, 0.045, 0.24, vlTrang, rong / 2, yDuoi - 0.02, zc + 0.09);                // bệ cửa sổ
  // khung cửa chính Nam (x = 11, rộng 2,4 m, cao 2,8 m)
  const cx = rong / 2;
  for (const s of [-1, 1]) hop(0.07, 2.8, 0.24, vlKhungCua, cx + s * 1.2, 1.4, sau - 0.02);
  hop(2.47, 0.07, 0.24, vlKhungCua, cx, 2.8, sau - 0.02);
  const bienRa = new THREE.Mesh(tg(new THREE.PlaneGeometry(0.62, 0.155)), tg(new THREE.MeshBasicMaterial({ map: tg(VL.bienPhatSang("LỐI RA")) })));
  bienRa.position.set(cx, 3.05, sau - 0.001);
  bienRa.rotation.y = Math.PI;
  nha.add(bienRa);
  // đồng hồ treo tường Tây
  const dh = new THREE.Mesh(tg(new THREE.CircleGeometry(0.22, 40)), tg(new THREE.MeshStandardMaterial({ map: tg(VL.matDongHo()), roughness: 0.4 })));
  dh.position.set(0.012, 3.1, sau / 2);
  dh.rotation.y = Math.PI / 2;
  nha.add(dh);
  const vien = new THREE.Mesh(tg(new THREE.TorusGeometry(0.225, 0.014, 10, 40)), vlDen);
  vien.position.copy(dh.position);
  vien.position.x += 0.006;
  vien.rotation.y = Math.PI / 2;
  nha.add(vien);

  // ============================================================ 4. TRẦN: KHUNG ĐÈN, ĐẦU PHUN, CỬA GIÓ
  for (const d of phong._denPanel) {
    hop(1.7, 0.03, 0.7, vlNhom, d.position.x, d.position.y + 0.012, d.position.z);
    d.scale.set(0.97, 1, 0.94);
  }
  const daoGio = (() => {
    const c = document.createElement("canvas");
    c.width = c.height = 128;
    const g = c.getContext("2d");
    g.fillStyle = "#e8ecf1"; g.fillRect(0, 0, 128, 128);
    g.fillStyle = "#6b7280";
    for (let i = 0; i < 9; i++) g.fillRect(14, 14 + i * 12, 100, 4);
    const t = new THREE.CanvasTexture(c);
    t.colorSpace = THREE.SRGBColorSpace;
    return tg(t);
  })();
  const vlGio = tg(new THREE.MeshStandardMaterial({ map: daoGio, roughness: 0.6 }));
  for (const [x, z] of [[6.5, 8], [15.5, 8]]) {
    const g = new THREE.Mesh(tg(new THREE.PlaneGeometry(0.6, 0.6)), vlGio);
    g.rotation.x = Math.PI / 2;
    g.position.set(x, cao - 0.004, z);
    nha.add(g);
  }
  const hinhPhun = tg(new THREE.CylinderGeometry(0.018, 0.018, 0.04, 10));
  const hinhDia = tg(new THREE.CylinderGeometry(0.045, 0.045, 0.004, 16));
  const vlPhun = hopMat(0xb91c1c, 0.4, 0.4);
  for (let x = 2.5; x < rong; x += 4.5) for (let z = 2.5; z < sau; z += 4.5) {
    const p = new THREE.Mesh(hinhPhun, vlPhun); p.position.set(x, cao - 0.03, z); nha.add(p);
    const d = new THREE.Mesh(hinhDia, vlTrang); d.position.set(x, cao - 0.004, z); nha.add(d);
  }

  return nha;
}
