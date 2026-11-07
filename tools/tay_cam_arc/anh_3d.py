"""Chụp ảnh 3D VERA Arc cho bản trình bày, nền trong suốt, bằng Edge chạy ẩn.

Chạy sau xuat_web.py:  python tools/tay_cam_arc/anh_3d.py <thư mục ra> [thư mục tạm]
Ra: <thư mục ra>/tc3d_a.png (nguyên khối, góc chéo), tc3d_tach.png (tách rời các chi tiết)
"""
import base64
import json
import os
import subprocess
import sys
import tempfile

GOC = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
EDGE = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
# ten: (rộng, cao, tách rời 0..1, vị trí máy ảnh, điểm nhìn, độ phóng)
ANH = {"tc3d_a": (1260, 1470, 0, [-300, 230, 210], [3, 58, 0], 1.0),
       "tc3d_tach": (1500, 1000, 1, [380, 170, 300], [3, 50, 8], 0.86)}

TRANG = """<!doctype html><meta charset="utf-8"><style>html,body{margin:0;background:transparent;overflow:hidden}canvas{display:block}</style>
<script type="importmap">{"imports":{"three":"https://cdn.jsdelivr.net/npm/three@0.170.0/build/three.module.js","three/addons/":"https://cdn.jsdelivr.net/npm/three@0.170.0/examples/jsm/"}}</script>
<script type="module">
import * as THREE from 'three';
import { RoomEnvironment } from 'three/addons/environments/RoomEnvironment.js';
const TT = __TT__, W = __W__, H = __H__, TACH = __TACH__, MAY = __MAY__, NHIN = __NHIN__, ZOOM = __ZOOM__;
const buf = Uint8Array.from(atob('__BIN__'), c => c.charCodeAt(0)).buffer;
const r = new THREE.WebGLRenderer({ antialias: true, alpha: true, preserveDrawingBuffer: true });
r.setPixelRatio(1); r.setSize(W, H); r.setClearColor(0, 0);
r.toneMapping = THREE.ACESFilmicToneMapping; r.toneMappingExposure = .95;
document.body.append(r.domElement);
const scene = new THREE.Scene();
scene.environment = new THREE.PMREMGenerator(r).fromScene(new RoomEnvironment(), .04).texture;
scene.environmentIntensity = .8;
const d1 = new THREE.DirectionalLight(0xffffff, 1.8); d1.position.set(200, 320, 220); scene.add(d1);
const d2 = new THREE.DirectionalLight(0xffffff, .7); d2.position.set(-260, 60, -220); scene.add(d2);
const VL = {
  than_phai: { color: '#eef2f4', roughness: .38, clearcoat: .7, clearcoatRoughness: .35 },
  than_trai: { color: '#eef2f4', roughness: .38, clearcoat: .7, clearcoatRoughness: .35 },
  thanh: { color: '#15191d', roughness: .32, metalness: .25, clearcoat: .5 },
  chu: { color: '#04141a', roughness: .6 }, vit: { color: '#aab4ba', roughness: .3, metalness: .9 },
  nap: { color: '#101418', roughness: .22, metalness: .2, clearcoat: 1, clearcoatRoughness: .12 },
  nap_pin: { color: '#101418', roughness: .3, metalness: .2, clearcoat: .6 },
  co: { color: '#c7ced2', roughness: .22, metalness: .85 }, nut_phu: { color: '#c7ced2', roughness: .22, metalness: .85 },
  ky023: { color: '#15191d', roughness: .45, metalness: .2 }, pin9v: { color: '#c9862b', roughness: .4, metalness: .35 },
  chen: { color: '#18c8e6', roughness: .38 }, vong: { color: '#18c8e6', roughness: .38 }
};
const goc = new THREE.Group(); goc.rotation.x = -Math.PI / 2; scene.add(goc);
for (const p of TT) {
  const g = new THREE.BufferGeometry();
  const ib = new THREE.InterleavedBuffer(new Int16Array(buf, p.v[0], p.v[1] * 5), 5);
  const nb = new THREE.InterleavedBuffer(new Int8Array(buf, p.v[0], p.v[1] * 10), 10);
  g.setAttribute('position', new THREE.InterleavedBufferAttribute(ib, 3, 0, false));
  g.setAttribute('normal', new THREE.InterleavedBufferAttribute(nb, 3, 6, true));
  const Kieu = p.i[2] === 4 ? Uint32Array : Uint16Array;
  g.setIndex(new THREE.BufferAttribute(new Kieu(buf, p.i[0], p.i[1]), 1));
  const m = new THREE.Mesh(g, new THREE.MeshPhysicalMaterial(VL[p.ma] || { color: p.mau, roughness: .5, metalness: .25 }));
  m.scale.setScalar(1 / 256);
  m.position.set(...p.tach).multiplyScalar(TACH);
  goc.add(m);
}
const cam = new THREE.PerspectiveCamera(26, W / H, 1, 4000);
cam.position.set(...MAY); cam.lookAt(...NHIN); cam.zoom = ZOOM; cam.updateProjectionMatrix();
r.render(scene, cam);
</script>"""


def main():
    ra = sys.argv[1]
    tam = sys.argv[2] if len(sys.argv) > 2 else tempfile.mkdtemp()
    os.makedirs(ra, exist_ok=True)
    mo_hinh = os.path.join(GOC, "trang_gioi_thieu", "mo_hinh")
    with open(os.path.join(mo_hinh, "arc.json"), encoding="utf-8") as fh:
        tt = fh.read()
    with open(os.path.join(mo_hinh, "arc.bin"), "rb") as fh:
        nhi = base64.b64encode(fh.read()).decode("ascii")
    for ten, (w, h, tach, may, nhin, zoom) in ANH.items():
        html = os.path.join(tam, ten + ".html")
        noi_dung = TRANG
        for a, b in (("__TT__", tt), ("__W__", w), ("__H__", h), ("__TACH__", tach), ("__MAY__", json.dumps(may)), ("__NHIN__", json.dumps(nhin)), ("__ZOOM__", zoom), ("__BIN__", nhi)):
            noi_dung = noi_dung.replace(a, str(b))
        with open(html, "w", encoding="utf-8") as fh:
            fh.write(noi_dung)
        png = os.path.abspath(os.path.join(ra, ten + ".png"))
        subprocess.run([EDGE, "--headless=new", "--disable-gpu-sandbox", "--use-angle=swiftshader", "--enable-unsafe-swiftshader", "--ignore-gpu-blocklist", "--hide-scrollbars",
                        f"--user-data-dir={os.path.join(tam, 'edge')}", "--default-background-color=00000000", f"--window-size={w},{h}", "--virtual-time-budget=30000",
                        f"--screenshot={png}", "file:///" + html.replace("\\", "/")], check=True, timeout=240)
        print(png, os.path.getsize(png))


if __name__ == "__main__":
    main()
