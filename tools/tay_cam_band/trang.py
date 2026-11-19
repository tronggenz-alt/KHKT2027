"""Trang xem 3D tay cầm VERA Band (three.js) để duyệt hình dáng.

Chạy sau mo_hinh_band.py:  python tools/tay_cam_band/trang.py <thư mục xem có canh.json>
Ra: <thư mục xem>/vera_band.html + mo_hinh.json (mở qua máy chủ http vì trang tải file bên cạnh)
"""
import base64
import json
import os
import sys


def bang(head, rows):
    th = "".join(f"<th>{h}</th>" for h in head)
    tr = "".join("<tr>" + "".join(f"<td>{c}</td>" for c in r) + "</tr>" for r in rows)
    return f'<div class="tbl"><table><thead><tr>{th}</tr></thead><tbody>{tr}</tbody></table></div>'


def main():
    thu_muc = sys.argv[1]
    with open(os.path.join(thu_muc, "canh.json"), encoding="utf-8") as fh:
        canh = json.load(fh)
    in_rows = canh["goi_in"]
    kt = canh["kich_thuoc"]
    bang_in = bang(["File STL", "Chi tiết trong file", "Màu đề xuất", "Khổ trên bàn in (mm)", "Nhựa"],
                   [(f"<code>{c['file']}.stl</code>", c["ten"], c["mau"], " × ".join(f"{v:g}" for v in c["kich_thuoc"]), f"{c['gam_pla']:g} g") for c in in_rows])
    goi = {}
    for ten in [c["ma"] for c in canh["chi_tiet"]] + ["ban_tay"]:
        with open(os.path.join(thu_muc, ten + ".stl"), "rb") as fh:
            goi[ten] = base64.b64encode(fh.read()).decode("ascii")
    with open(os.path.join(thu_muc, "mo_hinh.json"), "w", encoding="ascii") as fh:
        json.dump(goi, fh)
    canh_js = json.dumps(canh, ensure_ascii=False)
    bang_cong = bang(["Cổng", "Vị trí", "Chân ESP32", "Dùng được cho"],
                     [("P1", "Mặt tối, phía trái, ngón cái", "GPIO13", "Nút bấm, cảm ứng chạm, cảm biến tín hiệu số"),
                      ("P2", "Mặt tối, phía phải, ngón cái", "GPIO14", "Nút bấm, cảm ứng chạm, cảm biến tín hiệu số"),
                      ("P3", "Mũi đầu, phía trái", "GPIO4", "Nút bấm, cảm ứng chạm, cảm biến tín hiệu số"),
                      ("P4", "Mũi đầu, phía phải", "GPIO36", "Cảm biến analog; làm nút thì thêm điện trở kéo lên 10k")])

    html = f"""<meta charset="utf-8">
<title>VERA Band</title>
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
.stage{{position:relative;grid-column:1/2;height:min(68vh,640px);min-height:420px}}
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
.wrap{{max-width:1320px;margin:0 auto;padding-block:36px 64px;padding-left:max(16px,env(safe-area-inset-left,0px));padding-right:max(16px,env(safe-area-inset-right,0px));display:grid;gap:20px}}
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
@media (max-width:860px){{ .hero-in{{grid-template-columns:1fr}} .stage{{order:1;height:60vh}} .specs{{order:2}} .panel{{order:3;grid-column:1;grid-row:auto;border-left:0;border-top:1px solid rgba(143,168,174,.25);padding:16px 0}} }}
</style>

<div class="hero"><div class="hero-in">
  <div>
    <span class="lab">VERA LAB · Le Quy Don</span>
    <h1>VERA <b>Band</b></h1>
    <p>Đeo vào tay bằng quai qua mu bàn tay: thả lỏng các ngón, tay cầm vẫn nằm trong lòng bàn tay. Tấm mu tay khắc logo VERA LAB, mặt tối bầu dục với chén lục giác, pin vuông 9V trong cán.</p>
  </div>
  <aside class="panel">
    <div class="nut"><button id="vTruoc">Chéo trước</button><button id="vSau">Chéo sau</button><button id="vThang">Mặt bên</button><button id="vDay">Dưới đáy</button></div>
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
    <div><b>{kt[0]} × {kt[1]} × {kt[2]}</b><span>mm, kể cả khuyên quai và cò</span></div>
    <div><b>≈ {canh['tong_g']} g</b><span>có pin, chưa cân thật (mục tiêu dưới 500 g)</span></div>
    <div><b>{canh['nhua_g']} g</b><span>nhựa, gói in {len(in_rows)} file STL</span></div>
    <div><b>{canh['rong_quai']:g} mm</b><span>quai băng gai dính, chỉnh được độ chặt</span></div>
  </div>
</div></div>

<div class="wrap">
  <div class="cols">
    <div class="card"><h3>Cách đeo</h3><ul class="notes">
      <li>Gỡ đầu dưới quai, luồn tay phải vào giữa tay cầm và quai, cầm như bình thường.</li>
      <li>Kéo đầu quai đi qua khe ở đế, gập ngược lên rồi dán gai cho vừa tay. Mở các ngón ra, tay cầm vẫn nằm trong lòng bàn tay.</li>
      <li>Tấm mu tay nằm trên quai, ngay mu bàn tay. Mặt trong nên dán xốp mỏng cho êm.</li>
      <li>Quai chạy dọc mu bàn tay nên các ngón vẫn co duỗi tự do; ngón cái lên joystick, ngón trỏ trên cò.</li>
    </ul></div>
    <div class="card"><h3>Bố trí và nét riêng</h3><ul class="notes">
      <li>Không còn vòng: theo dõi tay dùng MediaPipe. Logo và chữ VERA LAB chuyển sang tấm mu tay.</li>
      <li>Đế cán phình nhẹ về phía mu bàn tay để chứa khe quai và đỡ mép bàn tay.</li>
      <li>Pin vuông 9V dựng đứng trong cán, ESP32 nằm ngang trong đầu. Thay pin từ đáy không cần tua vít.</li>
      <li>Giữ chén lục giác và 4 cổng mở rộng GPIO của bản trước.</li>
    </ul></div>
    <div class="card warn"><h3>Chưa chắc, cần thử</h3><ul class="notes">
      <li>Kẹp pin giả định cao 8,5 mm. Pin và kẹp phải đo thật trước khi in vì cán ôm rất sát.</li>
      <li>Đầu to hơn cán để đặt được ESP32 dài 55 mm, nên phần đuôi đầu nhô ra sau khoảng 12 mm trên khe ngón cái. Cần in thử xem có cấn không.</li>
      <li>Chia áp đo pin phải đổi sang 330k/82k. Chỉnh MP1584 ra đúng 3,3 V trước khi nối mạch.</li>
      <li>Nếu cắm cáp mà chưa tắt nguồn, 2 nguồn 3,3 V sẽ chạy song song với nhau.</li>
      <li>Tấm mu tay bằng PLA cứng, cần dán xốp mặt trong. Nếu xưởng có nhựa dẻo TPU thì in tấm này bằng TPU sẽ êm hơn.</li>
      <li>Độ chặt và độ êm của quai, chỗ khuyên trên so với khe ngón cái, chưa thử trên tay thật.</li>
      <li>Ray nắp pin, móc mặt tối và độ nặng tay của cò chỉ mới đúng trên mô hình. Kích thước ESP32, KY-023 vẫn là số giả định.</li>
      <li>MediaPipe khi tay đang đeo tay cầm chưa thử với webcam thật.</li>
    </ul></div>
  </div>
  <div class="card"><h3>4 cổng mở rộng</h3>{bang_cong}<ul class="notes" style="margin-top:12px">
    <li>Mỗi cổng là một lỗ Ø7 mm, phía sau có ô giữ nút bấm 6 × 6 và nắp nút in 3D. Muốn gắn cảm biến thì bỏ nắp, luồn dây qua lỗ.</li>
    <li>Mọi chân chỉ chịu tối đa 3,3 V. Cảm biến ra 5 V phải qua chia áp.</li>
    <li>Chỉ P4 đọc được analog khi bật Wi-Fi, vì GPIO13, 14 và 4 thuộc ADC2.</li>
  </ul></div>
  <div class="card"><h3>Gói STL gửi in</h3>{bang_in}</div>
</div>

<script type="importmap">{{"imports":{{"three":"https://cdn.jsdelivr.net/npm/three@0.160.0/build/three.module.js","three/addons/":"https://cdn.jsdelivr.net/npm/three@0.160.0/examples/jsm/"}}}}</script>
<script type="module">
import * as THREE from 'three';
import {{ OrbitControls }} from 'three/addons/controls/OrbitControls.js';
import {{ STLLoader }} from 'three/addons/loaders/STLLoader.js';
import {{ RoomEnvironment }} from 'three/addons/environments/RoomEnvironment.js';

const CANH = {canh_js};
const CHOT = [25, 0, 57], GOC_BOP = 13;
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
const TAM = new THREE.Vector3(-2, 40, 8);
const camera = new THREE.PerspectiveCamera(26, 1, 1, 4000);
const VIEW = {{ truoc: [300, 150, 260], sau: [-300, 200, 230], thang: [80, 70, 420], day: [200, -250, 250] }};
camera.position.set(...VIEW.sau);
const controls = new OrbitControls(camera, renderer.domElement);
controls.target.copy(TAM); controls.enableDamping = true; controls.autoRotateSpeed = 1.3;
controls.autoRotate = !matchMedia('(prefers-reduced-motion: reduce)').matches;
const cbQuay = document.getElementById('quay'); cbQuay.checked = controls.autoRotate;
const d1 = new THREE.DirectionalLight(0xffffff, 1.5); d1.position.set(200, 320, 220); scene.add(d1);
const d2 = new THREE.DirectionalLight(0x6fdcf0, 1.3); d2.position.set(-260, 60, -220); scene.add(d2);
// quầng sáng mờ dưới đế
const cv = document.createElement('canvas'); cv.width = cv.height = 256;
const cx2 = cv.getContext('2d'); const gr = cx2.createRadialGradient(128, 128, 18, 128, 128, 128);
gr.addColorStop(0, 'rgba(0,180,224,.34)'); gr.addColorStop(1, 'rgba(0,180,224,0)'); cx2.fillStyle = gr; cx2.fillRect(0, 0, 256, 256);
const san = new THREE.Mesh(new THREE.PlaneGeometry(240, 240), new THREE.MeshBasicMaterial({{ map: new THREE.CanvasTexture(cv), transparent: true, depthWrite: false }}));
san.rotation.x = -Math.PI / 2; san.position.set(2, -1, 0); scene.add(san);

const loader = new STLLoader();
const VL = {{
  than_phai: {{ color: '#eef2f4', roughness: 0.38, clearcoat: 0.7, clearcoatRoughness: 0.35 }},
  than_trai: {{ color: '#eef2f4', roughness: 0.38, clearcoat: 0.7, clearcoatRoughness: 0.35 }},
  quai: {{ color: '#2b3136', roughness: 0.95 }},
  chu: {{ color: '#04141a', roughness: 0.6 }},
  vit: {{ color: '#aab4ba', roughness: 0.3, metalness: 0.9 }},
  nap: {{ color: '#101418', roughness: 0.22, metalness: 0.2, clearcoat: 1, clearcoatRoughness: 0.12 }},
  nap_pin: {{ color: '#101418', roughness: 0.3, metalness: 0.2, clearcoat: 0.6 }},
  co: {{ color: '#c7ced2', roughness: 0.22, metalness: 0.85 }},
  nut_phu: {{ color: '#c7ced2', roughness: 0.22, metalness: 0.85 }},
  ky023: {{ color: '#15191d', roughness: 0.45, metalness: 0.2 }},
  tam_mu_tay: {{ color: '#0aa9c4', roughness: 0.35, emissive: '#00b4e0', emissiveIntensity: 0.35, clearcoat: 0.6 }},
  chen: {{ color: '#02242e', roughness: 0.3, emissive: '#00b4e0', emissiveIntensity: 1.25 }},
  pin9v: {{ color: '#c9862b', roughness: 0.4, metalness: 0.35 }},
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
  truc.position.set(CHOT[0] + 52 * t, CHOT[1], CHOT[2] - 8 * t);
  camera.zoom = 1 - 0.42 * t; camera.updateProjectionMatrix();
  for (const v of vat) {{
    if (!v.co) v.mesh.position.copy(v.tach).multiplyScalar(t);
    v.mesh.visible = v.lk && v.ma !== 'ky023' && v.ma !== 'quai' ? (ts || t > 0.02) : true;
    const vo = ['than_phai', 'than_trai', 'nap', 'co', 'nap_pin', 'nut_phu'].includes(v.ma);
    const mo = vo && ts ? 0.22 : 1;
    v.mesh.material.opacity = mo; v.mesh.material.depthWrite = mo === 1;
  }}
  if (tay) tay.visible = document.getElementById('hienTay').checked && t < 0.05;
}}
['tach', 'bop', 'trongSuot', 'hienTay'].forEach(id => document.getElementById(id).addEventListener('input', capNhat));
cbQuay.addEventListener('input', e => controls.autoRotate = e.target.checked);
function dungQuay() {{ controls.autoRotate = false; cbQuay.checked = false; }}
controls.addEventListener('start', dungQuay);
for (const [id, k] of [['vTruoc', 'truoc'], ['vSau', 'sau'], ['vThang', 'thang'], ['vDay', 'day']])
  document.getElementById(id).addEventListener('click', () => {{ dungQuay(); camera.position.set(...VIEW[k]); controls.target.copy(TAM); }});
function coKhung() {{ const w = stage.clientWidth, h = stage.clientHeight; renderer.setSize(w, h, false); camera.aspect = w / h; camera.updateProjectionMatrix(); }}
new ResizeObserver(coKhung).observe(stage); coKhung();
function ve() {{ controls.update(); renderer.render(scene, camera); requestAnimationFrame(ve); }}
dung().catch(e => {{ document.getElementById('tai').textContent = 'Không tải được mô hình 3D: ' + e.message; }});
ve();
</script>
"""
    ra = os.path.join(thu_muc, "vera_band.html")
    with open(ra, "w", encoding="utf-8") as fh:
        fh.write(html)
    print(ra, f"nhựa {canh['nhua_g']} g, tổng {canh['tong_g']} g, {kt} mm")


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    main()
