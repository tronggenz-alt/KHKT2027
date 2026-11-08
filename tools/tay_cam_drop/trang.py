"""Trang xem 3D tay cầm VERA Drop (three.js) để duyệt hình dáng.

Chạy sau mo_hinh_drop.py:  python tools/tay_cam_drop/trang.py <thư mục xem có canh.json>
Ra: <thư mục xem>/vera_drop.html + mo_hinh.json (mở qua máy chủ http vì trang tải file bên cạnh)
"""
import base64
import json
import os
import sys

# khối lượng linh kiện (g), chưa cân thật
LINH_KIEN_G = {"ESP32": 10, "pin 1000 mAh": 20, "TP4056, công tắc": 4, "KY-023": 10, "BMI160, Hall, nam châm, lò xo, trục": 3, "dây nối, 7 vít M2": 6}
V2_G = 155
BAN_VE = [("TCD-01", "Hình chiếu, kích thước"), ("TCD-02", "Bố trí linh kiện"), ("TCD-03", "Hai nửa thân, vít, thanh móc"),
          ("TCD-04", "Cò Hall"), ("TCD-05", "Đấu nối"), ("TCD-06", "Cách cầm, dây đeo, OpenCV")]


def bang(head, rows):
    th = "".join(f"<th>{h}</th>" for h in head)
    tr = "".join("<tr>" + "".join(f"<td>{c}</td>" for c in r) + "</tr>" for r in rows)
    return f'<div class="tbl"><table><thead><tr>{th}</tr></thead><tbody>{tr}</tbody></table></div>'


def main():
    thu_muc = sys.argv[1]
    with open(os.path.join(thu_muc, "canh.json"), encoding="utf-8") as fh:
        canh = json.load(fh)
    in_rows = [c for c in canh["chi_tiet"] if not c.get("linh_kien") and not c.get("chi_xem")]
    nhua = sum(c["gam_pla"] for c in in_rows)
    tong = nhua + sum(LINH_KIEN_G.values())
    bang_in = bang(["File STL", "Chi tiết", "Kích thước in (mm)", "Nhựa", "Hướng in"],
                   [(f"<code>{c['ma']}.stl</code>", c["ten"], " × ".join(f"{v:g}" for v in c["kich_thuoc"]), f"{c['gam_pla']:g} g", c["cach_in"]) for c in in_rows])
    goi = {}
    for ten in [c["ma"] for c in canh["chi_tiet"]] + ["ban_tay"]:
        with open(os.path.join(thu_muc, ten + ".stl"), "rb") as fh:
            goi[ten] = base64.b64encode(fh.read()).decode("ascii")
    with open(os.path.join(thu_muc, "mo_hinh.json"), "w", encoding="ascii") as fh:
        json.dump(goi, fh)
    canh_js = json.dumps(canh, ensure_ascii=False)
    ban_ve = "".join(f'<figure class="card"><img src="{ma}.png" alt="Bản vẽ {ma}: {ten}" loading="lazy"><figcaption>{ma} · {ten}</figcaption></figure>' for ma, ten in BAN_VE)

    html = f"""<meta charset="utf-8">
<title>VERA Drop</title>
<meta name="claude:viewport-fit-sides" content="cover">
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Barlow+Condensed:wght@500;600;700&family=Be+Vietnam+Pro:wght@400;500;600;700&family=JetBrains+Mono:wght@500;700&display=swap">
<style>
/* bố cục: sân khấu tối toàn bề ngang như ảnh ra mắt sản phẩm; dưới là thông số trên nền sáng */
:root{{
  --bg:#e8edec; --sheet:#fbfcfa; --ink:#15232a; --ink-2:#5b6c71; --rule:#c6d1cf; --warn:#b4471a;
  --halo:#3fd4ee; --stage-1:#070d10; --stage-2:#14242b; --stage-ink:#e6f1f3; --stage-ink-2:#8fa8ae;
  --f-disp:'Barlow Condensed','Arial Narrow',sans-serif; --f-body:'Be Vietnam Pro','Segoe UI',sans-serif; --f-mono:'JetBrains Mono',ui-monospace,monospace;
}}
@media (prefers-color-scheme: dark){{ :root:not([data-theme="light"]){{ --bg:#0d1517; --sheet:#132023; --ink:#dbe6e4; --ink-2:#93a5a8; --rule:#2a3b3e; --warn:#f08a5d; color-scheme:dark }} }}
:root[data-theme="dark"]{{ --bg:#0d1517; --sheet:#132023; --ink:#dbe6e4; --ink-2:#93a5a8; --rule:#2a3b3e; --warn:#f08a5d; color-scheme:dark }}
body{{background:var(--bg);color:var(--ink);font:400 15px/1.6 var(--f-body)}}
.hero{{background:radial-gradient(90% 80% at 42% 45%,var(--stage-2),var(--stage-1));color:var(--stage-ink)}}
.hero-in{{max-width:1320px;margin:0 auto;padding-block:24px 0;padding-left:max(16px,env(safe-area-inset-left,0px));padding-right:max(16px,env(safe-area-inset-right,0px));display:grid;grid-template-columns:minmax(0,1fr) 280px;gap:0 24px}}
.hero h1{{font:600 clamp(40px,7vw,76px)/.95 var(--f-disp);letter-spacing:.02em;margin:4px 0 10px;text-transform:uppercase}}
.hero h1 b{{color:var(--halo);font-weight:600}}
.lab{{font:600 11px var(--f-disp);letter-spacing:.14em;text-transform:uppercase;color:var(--stage-ink-2)}}
.hero p{{color:var(--stage-ink-2);max-width:60ch;margin:0}}
.stage{{position:relative;grid-column:1/2;height:min(66vh,620px);min-height:420px}}
.stage canvas{{display:block;width:100%;height:100%;cursor:grab}}
.loading{{position:absolute;left:0;bottom:16px;font:500 13px var(--f-body);color:var(--stage-ink-2)}}
.panel{{grid-column:2/3;grid-row:1/3;border-left:1px solid rgba(143,168,174,.25);padding:18px 0 22px 22px;display:grid;gap:16px;align-content:start}}
.panel label{{display:flex;gap:10px;align-items:center;font:500 14px var(--f-body);cursor:pointer;color:var(--stage-ink)}}
.panel input[type=range]{{width:100%;accent-color:var(--halo)}}
.panel input[type=checkbox]{{accent-color:var(--halo);width:16px;height:16px}}
.nut{{display:grid;grid-template-columns:1fr 1fr;gap:6px}}
.nut button{{font:600 13px var(--f-body);color:var(--stage-ink);background:rgba(143,168,174,.14);border:1px solid rgba(143,168,174,.35);padding:6px 8px;cursor:pointer}}
.nut button:hover,.nut button:focus-visible{{background:rgba(143,168,174,.3);outline:none}}
.hint{{font-size:12.5px;color:var(--stage-ink-2);margin:0}}
.specs{{grid-column:1/2;display:grid;grid-template-columns:repeat(auto-fit,minmax(130px,1fr));border-top:1px solid rgba(143,168,174,.25)}}
.specs div{{padding:12px 14px 16px 0}}
.specs b{{display:block;font:600 26px/1.1 var(--f-disp);color:var(--stage-ink);font-variant-numeric:tabular-nums}}
.specs span{{font-size:12.5px;color:var(--stage-ink-2)}}
.wrap{{max-width:1320px;margin:0 auto;padding-block:36px 64px;padding-left:max(16px,env(safe-area-inset-left,0px));padding-right:max(16px,env(safe-area-inset-right,0px));display:grid;gap:28px}}
h2{{font:700 26px/1.1 var(--f-disp);letter-spacing:.05em;text-transform:uppercase;margin:0}}
section{{display:grid;gap:14px}}
.cols{{display:grid;grid-template-columns:repeat(auto-fit,minmax(min(100%,400px),1fr));gap:20px}}
.card{{background:var(--sheet);border:1px solid var(--rule);padding:16px 18px;min-width:0}}
.card h3{{font:600 15px var(--f-body);margin:0 0 8px}}
.tbl{{overflow-x:auto}}
table{{border-collapse:collapse;width:100%;font-size:13.5px}}
th{{text-align:left;font:600 11px var(--f-disp);letter-spacing:.1em;text-transform:uppercase;color:var(--ink-2);border-bottom:1.5px solid var(--ink);padding:6px 10px 6px 0}}
td{{border-bottom:1px solid var(--rule);padding:7px 10px 7px 0;vertical-align:top;font-variant-numeric:tabular-nums}}
code{{font:500 12.5px var(--f-mono)}}
.notes{{margin:0;padding-left:20px;display:grid;gap:6px}}
.warn{{border-left:3px solid var(--warn)}}
figure.card{{margin:0;display:grid;gap:8px}}
figure.card img{{width:100%;height:auto;display:block}}
figcaption{{font:500 13px var(--f-body);color:var(--ink-2)}}
@media (max-width:860px){{ .hero-in{{grid-template-columns:1fr}} .stage{{order:1;height:60vh}} .specs{{order:2}} .panel{{order:3;grid-column:1;grid-row:auto;border-left:0;border-top:1px solid rgba(143,168,174,.25);padding:16px 0}} }}
</style>

<div class="hero"><div class="hero-in">
  <div>
    <span class="lab">VERA LAB · Le Quy Don</span>
    <h1>VERA <b>Drop</b></h1>
    <p>Tay cầm dáng giọt nước, thân gồm 2 nửa bắt vít để mở ra lắp mạch. Có thanh móc dây ở lưng đáy và chữ khắc ở sườn. Bên trong vẫn là ESP32, BMI160, joystick và cò Hall.</p>
  </div>
  <aside class="panel">
    <div class="nut"><button id="vTruoc">Chéo trước</button><button id="vSau">Chéo sau</button><button id="vTren">Từ trên</button><button id="vDay">Dưới đáy</button></div>
    <div><span class="lab">Tách rời</span><input type="range" id="tach" min="0" max="1" step="0.01" value="0" aria-label="Tách rời các chi tiết"></div>
    <div><span class="lab">Bóp cò</span><input type="range" id="bop" min="0" max="1" step="0.01" value="0" aria-label="Bóp cò"></div>
    <div style="display:grid;gap:7px">
      <label><input type="checkbox" id="quay" checked>Tự xoay</label>
      <label><input type="checkbox" id="trongSuot">Nhìn xuyên vỏ</label>
      <label><input type="checkbox" id="hienTay">Đặt trong tay</label>
    </div>
    <p class="hint">Kéo để xoay, cuộn để phóng to. Bàn tay chỉ là hình mô phỏng để so cỡ.</p>
  </aside>
  <div class="stage" id="stage"><div class="loading" id="tai">Đang tải mô hình 3D…</div></div>
  <div class="specs">
    <div><b>59 × 42 × 106</b><span>mm, kể cả cần joystick và thanh móc</span></div>
    <div><b>≈ {tong:.0f} g</b><span>có pin, chưa cân thật (bản cũ ≈ {V2_G} g)</span></div>
    <div><b>{len(in_rows)}</b><span>chi tiết in 3D</span></div>
    <div><b>2 cổng</b><span>ở đáy: sạc và nạp code</span></div>
  </div>
</div></div>

<div class="wrap">
<section>
  <div class="cols">
    <div class="card"><h3>Cách dùng</h3><ul class="notes">
      <li>Cầm như báng súng nhỏ: ngón cái trên joystick, ngón trỏ trên cò, 3 ngón ôm eo thân.</li>
      <li>Luồn dây đeo qua thanh móc ở lưng đáy rồi quàng vào cổ tay, buông tay cũng không rơi. Khe luồn rộng 11 mm, dày ≈ 2,8 mm.</li>
      <li>Cò đi được ≈ 6 mm. Nam châm cách Hall 6,5 mm lúc nhả, còn ≈ 3,6 mm lúc bóp hết.</li>
      <li>Cắm cáp ở đáy để sạc và nạp code, không phải mở vỏ.</li>
    </ul></div>
    <div class="card"><h3>Thứ tự lắp</h3><ol class="notes">
      <li>Mở 2 nửa thân. Đặt ESP32, BMI160 và Hall vào nửa trái; dán pin, đặt mạch sạc và công tắc vào nửa phải.</li>
      <li>Đặt cò vào giữa, xỏ trục Ø2 qua 2 tai, đặt lò xo và lồng thanh móc vào 2 tai. Nối dây.</li>
      <li>Khép 2 nửa, siết 4 vít M2×10 và vít M2×16 xuyên thanh móc, tất cả bắt từ nửa trái.</li>
      <li>Đặt vòng sáng lên miệng thân. Bắt KY-023 và chén lõm vào nắp, úp nắp xuống rồi siết 2 vít M2×6 ở lưng.</li>
    </ol></div>
    <div class="card warn"><h3>Chưa chắc, cần thử</h3><ul class="notes">
      <li>Mạch ESP32 giả định 55 × 28 mm, chân header cắt ngắn còn ≈ 2,5 mm. Đo mạch thật rồi chạy lại kiểm khít.</li>
      <li>KY-023 phải bẻ thẳng chân và hàn dây.</li>
      <li>Vòng trên hình đang vẽ phát sáng. Nhựa huỳnh quang chỉ nổi màu chứ không tự sáng; muốn sáng thật phải thêm dải LED.</li>
      <li>Chữ khắc sâu 0,6 mm, chữ hoa cao ≈ 3,2 mm. Trên hình đang tô đen để dễ đọc; in xong cần tô bút sơn thì chữ mới nổi.</li>
      <li>2 nửa thân in với mặt ghép úp xuống nên bên trong và dưới 2 tai móc cần vật đỡ. Mặt ghép phẳng, không có gờ khóa, nên có thể lệch nhẹ tới khi siết vít.</li>
      <li>Nguồn cần thêm mạch hạ áp 3,3 V từ 500 mA (ví dụ AP2112K) vì ESP32 bật Wi-Fi tốn dòng. Mạch này chưa có hốc riêng trong mô hình.</li>
      <li>Vòng bầu dục 55 × 41 mm nhỏ hơn halo tròn Ø70 của bản V2, nên OpenCV đo xa gần cần lấy hướng từ BMI160 để bù. Chưa thử với webcam.</li>
      <li>Độ mỏi tay chưa đo, cần in thử để cầm.</li>
    </ul></div>
  </div>
  <div class="card">{bang_in}</div>
</section>
<section>
  <h2>Bản vẽ 2D</h2>
  <div class="cols">{ban_ve}</div>
</section>
</div>

<script type="importmap">{{"imports":{{"three":"https://cdn.jsdelivr.net/npm/three@0.160.0/build/three.module.js","three/addons/":"https://cdn.jsdelivr.net/npm/three@0.160.0/examples/jsm/"}}}}</script>
<script type="module">
import * as THREE from 'three';
import {{ OrbitControls }} from 'three/addons/controls/OrbitControls.js';
import {{ STLLoader }} from 'three/addons/loaders/STLLoader.js';
import {{ RoomEnvironment }} from 'three/addons/environments/RoomEnvironment.js';

const CANH = {canh_js};
const CHOT = [18.8, 0, 72.5], GOC_BOP = 15.5;
const stage = document.getElementById('stage');
const renderer = new THREE.WebGLRenderer({{ antialias: true, alpha: true, preserveDrawingBuffer: true }});
renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
renderer.toneMapping = THREE.ACESFilmicToneMapping;
renderer.outputColorSpace = THREE.SRGBColorSpace;
stage.prepend(renderer.domElement);
const scene = new THREE.Scene();
scene.environment = new THREE.PMREMGenerator(renderer).fromScene(new RoomEnvironment(renderer), 0.04).texture;
// mô hình: z lên, x trước; three.js: y lên
const goc = new THREE.Group(); goc.rotation.x = -Math.PI / 2; scene.add(goc);
const TAM = new THREE.Vector3(0, 52, 0);
const camera = new THREE.PerspectiveCamera(26, 1, 1, 4000);
const VIEW = {{ truoc: [250, 130, 300], sau: [-250, 150, -260], tren: [-70, 400, 90], day: [150, -280, 170] }};
camera.position.set(...VIEW.truoc);
const controls = new OrbitControls(camera, renderer.domElement);
controls.target.copy(TAM); controls.enableDamping = true; controls.autoRotateSpeed = 1.3;
controls.autoRotate = !matchMedia('(prefers-reduced-motion: reduce)').matches;
const cbQuay = document.getElementById('quay'); cbQuay.checked = controls.autoRotate;
const d1 = new THREE.DirectionalLight(0xffffff, 1.5); d1.position.set(200, 320, 220); scene.add(d1);
const d2 = new THREE.DirectionalLight(0x6fdcf0, 1.3); d2.position.set(-260, 60, -220); scene.add(d2);
// quầng sáng mờ dưới đáy
const cv = document.createElement('canvas'); cv.width = cv.height = 256;
const cx2 = cv.getContext('2d'); const gr = cx2.createRadialGradient(128, 128, 18, 128, 128, 128);
gr.addColorStop(0, 'rgba(0,180,224,.34)'); gr.addColorStop(1, 'rgba(0,180,224,0)'); cx2.fillStyle = gr; cx2.fillRect(0, 0, 256, 256);
const san = new THREE.Mesh(new THREE.PlaneGeometry(190, 190), new THREE.MeshBasicMaterial({{ map: new THREE.CanvasTexture(cv), transparent: true, depthWrite: false }}));
san.rotation.x = -Math.PI / 2; san.position.y = -0.5; scene.add(san);

const loader = new STLLoader();
const VL = {{
  than_phai: {{ color: '#eef2f4', roughness: 0.38, clearcoat: 0.7, clearcoatRoughness: 0.35 }},
  than_trai: {{ color: '#eef2f4', roughness: 0.38, clearcoat: 0.7, clearcoatRoughness: 0.35 }},
  thanh: {{ color: '#15191d', roughness: 0.32, metalness: 0.25, clearcoat: 0.5 }},
  chu: {{ color: '#15191d', roughness: 0.6 }},
  vit: {{ color: '#aab4ba', roughness: 0.3, metalness: 0.9 }},
  nap: {{ color: '#101418', roughness: 0.22, metalness: 0.2, clearcoat: 1, clearcoatRoughness: 0.12 }},
  co: {{ color: '#15191d', roughness: 0.32, metalness: 0.25, clearcoat: 0.5 }},
  chen: {{ color: '#3a444b', roughness: 0.42, metalness: 0.65 }},
  ky023: {{ color: '#15191d', roughness: 0.45, metalness: 0.2 }},
  vong: {{ color: '#02242e', roughness: 0.3, emissive: '#00b4e0', emissiveIntensity: 1.25 }},
}};
let goi = null;
async function tai(ten) {{
  if (!goi) {{
    const r = await fetch('mo_hinh.json');
    if (!r.ok) throw new Error('mo_hinh.json');
    goi = await r.json();
  }}
  const nhi = Uint8Array.from(atob(goi[ten]), c => c.charCodeAt(0));
  const g = loader.parse(nhi.buffer);
  g.computeVertexNormals();
  return g;
}}
const vat = [];
let tay = null, truc = new THREE.Group();
truc.position.set(...CHOT); goc.add(truc);
async function dung() {{
  for (const c of CANH.chi_tiet) {{
    const g = await tai(c.ma);
    const mat = new THREE.MeshPhysicalMaterial({{ ...(VL[c.ma] || {{ color: c.mau, roughness: 0.5, metalness: 0.2 }}), transparent: true, opacity: 1 }});
    const mesh = new THREE.Mesh(g, mat);
    const quayTheoCo = c.ma === 'co' || c.ma === 'nam_cham';
    if (quayTheoCo) {{ mesh.position.set(-CHOT[0], -CHOT[1], -CHOT[2]); truc.add(mesh); }} else goc.add(mesh);
    vat.push({{ ma: c.ma, mesh, tach: new THREE.Vector3(...c.tach), lk: !!c.linh_kien, co: quayTheoCo }});
  }}
  tay = new THREE.Mesh(await tai('ban_tay'), new THREE.MeshStandardMaterial({{ color: '#d9a07a', roughness: 0.8, transparent: true, opacity: 0.5, depthWrite: false }}));
  goc.add(tay);
  document.getElementById('tai').remove();
  capNhat();
}}
function capNhat() {{
  const t = +document.getElementById('tach').value;
  const ts = document.getElementById('trongSuot').checked;
  truc.rotation.y = THREE.MathUtils.degToRad(GOC_BOP) * +document.getElementById('bop').value;
  truc.position.set(CHOT[0] + 46 * t, CHOT[1], CHOT[2] - 6 * t);
  camera.zoom = 1 - 0.32 * t; camera.updateProjectionMatrix();
  for (const v of vat) {{
    if (!v.co) v.mesh.position.copy(v.tach).multiplyScalar(t);
    v.mesh.visible = v.lk && v.ma !== 'ky023' ? (ts || t > 0.02) : true;
    const vo = ['than_phai', 'than_trai', 'nap', 'co', 'thanh'].includes(v.ma);
    const mo = vo && ts ? 0.22 : 1;
    v.mesh.material.opacity = mo; v.mesh.material.depthWrite = mo === 1;
  }}
  if (tay) tay.visible = document.getElementById('hienTay').checked && t < 0.05;
}}
['tach', 'bop', 'trongSuot', 'hienTay'].forEach(id => document.getElementById(id).addEventListener('input', capNhat));
cbQuay.addEventListener('input', e => controls.autoRotate = e.target.checked);
function dungQuay() {{ controls.autoRotate = false; cbQuay.checked = false; }}
controls.addEventListener('start', dungQuay);
for (const [id, k] of [['vTruoc', 'truoc'], ['vSau', 'sau'], ['vTren', 'tren'], ['vDay', 'day']])
  document.getElementById(id).addEventListener('click', () => {{ dungQuay(); camera.position.set(...VIEW[k]); controls.target.copy(TAM); }});
function coKhung() {{ const w = stage.clientWidth, h = stage.clientHeight; renderer.setSize(w, h, false); camera.aspect = w / h; camera.updateProjectionMatrix(); }}
new ResizeObserver(coKhung).observe(stage); coKhung();
function ve() {{ controls.update(); renderer.render(scene, camera); requestAnimationFrame(ve); }}
dung().catch(e => {{ document.getElementById('tai').textContent = 'Không tải được mô hình 3D: ' + e.message; }});
ve();
</script>
"""
    ra = os.path.join(thu_muc, "vera_drop.html")
    with open(ra, "w", encoding="utf-8") as fh:
        fh.write(html)
    print(ra, f"nhựa {nhua:.0f} g, tổng {tong:.0f} g")


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    main()
