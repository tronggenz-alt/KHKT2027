"""Trang tổng hợp tay cầm VR V2: khung xem 3D (three.js), bản vẽ 2D, cò Hall, đấu nối, chi tiết in.

Chạy sau mo_hinh.py và ve_2d_v2.py:  python tools/tay_cam_v2/trang.py <thư mục xem có canh.json>
Ra: <thư mục xem>/tay_cam_v2.html (mở qua máy chủ http vì trang tải các file STL bên cạnh)
"""
import json
import os
import sys

DIR = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, DIR)
import ve_2d_v2  # noqa: E402

DAU_NOI = [  # linh kiện, chân linh kiện, chân ESP32, ghi chú
    ("Joystick KY-023", "+5V", "3V3", "cấp 3,3 V để tín hiệu không vượt mức chịu của ESP32"),
    ("", "GND", "GND", ""),
    ("", "VRx", "GPIO34", "ADC1, chỉ vào; đọc được khi bật Wi-Fi"),
    ("", "VRy", "GPIO35", "ADC1, chỉ vào"),
    ("", "SW", "GPIO27", "INPUT_PULLUP, nhấn cần = 0"),
    ("Hall SS49E", "VCC (chân 1)", "3V3", "SS49E chạy từ 2,7 V"),
    ("", "GND (chân 2)", "GND", ""),
    ("", "OUT (chân 3)", "GPIO32", "ADC1; nên thêm tụ 100 nF từ OUT xuống GND để bớt nhiễu"),
    ("BMI160", "VIN", "3V3", ""),
    ("", "GND", "GND", ""),
    ("", "SDA", "GPIO21", "I2C, địa chỉ 0x68 (SAO nối GND) hoặc 0x69"),
    ("", "SCL", "GPIO22", ""),
    ("Pin (tính sau)", "qua chia áp 100k/100k", "GPIO33", "đo điện áp pin để báo sắp hết"),
]


def bang(head, rows):
    th = "".join(f"<th>{h}</th>" for h in head)
    tr = "".join("<tr>" + "".join(f"<td>{c}</td>" for c in r) + "</tr>" for r in rows)
    return f'<div class="tbl"><table><thead><tr>{th}</tr></thead><tbody>{tr}</tbody></table></div>'


def main():
    thu_muc = sys.argv[1]
    with open(os.path.join(thu_muc, "canh.json"), encoding="utf-8") as fh:
        canh = json.load(fh)
    in_rows = [c for c in canh["chi_tiet"] if not c.get("linh_kien")]
    tong_g = sum(c["gam_pla"] for c in in_rows)
    bang_in = bang(["File STL", "Chi tiết", "Kích thước in (mm)", "Nhựa PLA", "Hướng in"],
                   [(f"<code>{c['ma']}.stl</code>", c["ten"], " × ".join(f"{v:g}" for v in c["kich_thuoc"]),
                     f"{c['gam_pla']:g} g", c["cach_in"]) for c in in_rows]
                   + [("", "<b>Tổng</b>", "", f"<b>{tong_g:.0f} g</b>", "")])
    to = ve_2d_v2.tat_ca(standalone=False)
    sheets = "".join(f'<figure class="sheet-wrap" id="{so.lower()}"><figcaption><span class="so">{so}</span>{ten}'
                     f'<span class="png">PNG: docs/tay-cam-v2/{so}.png</span></figcaption><div class="scroll">{svg}</div></figure>'
                     for so, ten, svg in to)
    canh_js = json.dumps(canh, ensure_ascii=False)

    html = f"""<meta charset="utf-8">
<title>Tay cầm VR Halo</title>
<meta name="claude:viewport-fit-sides" content="cover">
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Barlow+Condensed:wght@500;600;700&family=Be+Vietnam+Pro:wght@400;500;600;700&family=JetBrains+Mono:wght@500;700&display=swap">
<style>
/* bố cục: sân khấu 3D tối toàn bề ngang ở đầu (giống phòng trưng bày), dưới là các bản vẽ trên nền giấy kỹ thuật */
:root{{
  --bg:#e8edec; --sheet:#fbfcfa; --ink:#15232a; --ink-2:#5b6c71; --rule:#c6d1cf;
  --dim:#0a6b8a; --warn:#b4471a; --ok:#2f7d4f; --pvc:#dde5e3; --pvc2:#c9d4d1; --gnd:#2b3135;
  --mica:rgba(110,180,210,.35); --hand:#e9b98f; --hand-ln:#9c5f37; --fig:#a9bcc0;
  --halo:#11b5d4; --stage-1:#0b1418; --stage-2:#16262d; --stage-ink:#e6f1f3; --stage-ink-2:#8fa8ae;
  --f-disp:'Barlow Condensed','Arial Narrow',sans-serif; --f-body:'Be Vietnam Pro','Segoe UI',sans-serif; --f-mono:'JetBrains Mono',ui-monospace,monospace;
}}
@media (prefers-color-scheme: dark){{ :root:not([data-theme="light"]){{
  --bg:#0d1517; --sheet:#132023; --ink:#dbe6e4; --ink-2:#93a5a8; --rule:#2a3b3e; --dim:#5cc3e0; --warn:#f08a5d;
  --ok:#6cc58f; --pvc:#2a3a3e; --pvc2:#36494d; --gnd:#c9d1d3; --mica:rgba(110,180,210,.25); --hand:#b9835a; --hand-ln:#e8b98f; --fig:#4b6166;
  --halo:#3fd4ee; color-scheme:dark }} }}
:root[data-theme="dark"]{{
  --bg:#0d1517; --sheet:#132023; --ink:#dbe6e4; --ink-2:#93a5a8; --rule:#2a3b3e; --dim:#5cc3e0; --warn:#f08a5d;
  --ok:#6cc58f; --pvc:#2a3a3e; --pvc2:#36494d; --gnd:#c9d1d3; --mica:rgba(110,180,210,.25); --hand:#b9835a; --hand-ln:#e8b98f; --fig:#4b6166;
  --halo:#3fd4ee; color-scheme:dark }}
body{{background:var(--bg);color:var(--ink);font:400 15px/1.6 var(--f-body)}}
.hero{{background:radial-gradient(120% 90% at 70% 40%,var(--stage-2),var(--stage-1));color:var(--stage-ink)}}
.hero-in{{max-width:1320px;margin:0 auto;padding-block:22px 0;padding-left:max(16px,env(safe-area-inset-left,0px));padding-right:max(16px,env(safe-area-inset-right,0px));display:grid;grid-template-columns:minmax(0,1fr) 290px;gap:0 24px}}
.hero h1{{font:600 clamp(36px,6vw,64px)/.95 var(--f-disp);letter-spacing:.02em;margin:6px 0 10px;text-transform:uppercase}}
.hero h1 b{{color:var(--halo);font-weight:600}}
.lab{{font:600 11px var(--f-disp);letter-spacing:.14em;text-transform:uppercase;color:var(--ink-2)}}
.hero .lab{{color:var(--stage-ink-2)}}
.hero p{{color:var(--stage-ink-2);max-width:62ch;margin:0}}
.stage{{position:relative;grid-column:1/2;height:min(68vh,620px);min-height:420px}}
.stage canvas{{display:block;width:100%;height:100%;cursor:grab}}
#nhan{{position:absolute;inset:0;pointer-events:none}}
.tag{{position:absolute;transform:translate(-50%,-100%);font:500 11.5px var(--f-body);color:var(--stage-ink);background:rgba(10,20,24,.72);border:1px solid rgba(143,168,174,.45);padding:1px 8px;white-space:nowrap}}
.loading{{position:absolute;left:0;bottom:16px;font:500 13px var(--f-body);color:var(--stage-ink-2)}}
.panel{{grid-column:2/3;grid-row:1/3;align-self:stretch;border-left:1px solid rgba(143,168,174,.25);padding:18px 0 22px 22px;display:grid;gap:18px;align-content:start}}
.panel label{{display:flex;gap:10px;align-items:center;font:500 14px var(--f-body);cursor:pointer;color:var(--stage-ink)}}
.panel input[type=range]{{width:100%;accent-color:var(--halo)}}
.panel input[type=checkbox]{{accent-color:var(--halo);width:16px;height:16px}}
.parts{{list-style:none;margin:0;padding:0;display:grid;gap:2px}}
.parts button{{all:unset;cursor:pointer;display:flex;gap:9px;align-items:center;width:100%;box-sizing:border-box;font:500 13px var(--f-body);color:var(--stage-ink);padding:4px 6px}}
.parts button:hover,.parts button:focus-visible,.parts button[aria-pressed=true]{{background:rgba(143,168,174,.16);outline:none}}
.sw{{width:11px;height:11px;border:1px solid rgba(255,255,255,.35);flex:none}}
.hint{{font-size:12.5px;color:var(--stage-ink-2);margin:0}}
.specs{{grid-column:1/2;display:grid;grid-template-columns:repeat(auto-fit,minmax(130px,1fr));border-top:1px solid rgba(143,168,174,.25)}}
.specs div{{padding:12px 14px 16px 0}}
.specs b{{display:block;font:600 26px/1.1 var(--f-disp);color:var(--stage-ink);font-variant-numeric:tabular-nums}}
.specs span{{font-size:12.5px;color:var(--stage-ink-2)}}
.wrap{{max-width:1320px;margin:0 auto;padding-block:40px 64px;padding-left:max(16px,env(safe-area-inset-left,0px));padding-right:max(16px,env(safe-area-inset-right,0px));display:grid;gap:40px}}
h2{{font:700 26px/1.1 var(--f-disp);letter-spacing:.05em;text-transform:uppercase;margin:0}}
section{{display:grid;gap:16px}}
.lead{{max-width:75ch;margin:0;color:var(--ink-2)}}
.cols{{display:grid;grid-template-columns:repeat(auto-fit,minmax(min(100%,420px),1fr));gap:20px}}
.card{{background:var(--sheet);border:1px solid var(--rule);padding:16px 18px;min-width:0}}
.card h3{{font:600 15px var(--f-body);margin:0 0 8px}}
.tbl{{overflow-x:auto}}
table{{border-collapse:collapse;width:100%;font-size:13.5px}}
th{{text-align:left;font:600 11px var(--f-disp);letter-spacing:.1em;text-transform:uppercase;color:var(--ink-2);border-bottom:1.5px solid var(--ink);padding:6px 10px 6px 0}}
td{{border-bottom:1px solid var(--rule);padding:7px 10px 7px 0;vertical-align:top;font-variant-numeric:tabular-nums}}
code{{font:500 12.5px var(--f-mono)}}
.sheet-wrap{{margin:0;display:grid;gap:8px}}
.sheet-wrap figcaption{{display:flex;flex-wrap:wrap;gap:6px 14px;align-items:baseline;font:600 15px var(--f-body)}}
.sheet-wrap .so{{font:700 13px var(--f-mono);color:var(--dim)}}
.sheet-wrap .png{{margin-left:auto;font:500 12px var(--f-mono);color:var(--ink-2)}}
.scroll{{overflow-x:auto;border:1px solid var(--rule);background:var(--sheet)}}
svg.sheet{{display:block;width:100%;min-width:880px;height:auto}}
.toc{{display:flex;flex-wrap:wrap;gap:8px}}
.toc a{{font:600 13px var(--f-body);color:var(--ink);text-decoration:none;border:1px solid var(--rule);background:var(--sheet);padding:5px 12px}}
.toc a:hover,.toc a:focus-visible{{border-color:var(--dim);color:var(--dim);outline:none}}
ul.notes{{margin:0;padding-left:20px;display:grid;gap:6px}}
.warn{{border-left:3px solid var(--warn)}}
@media (max-width:860px){{ .hero-in{{grid-template-columns:1fr}} .panel{{grid-column:1;grid-row:auto;border-left:0;border-top:1px solid rgba(143,168,174,.25);padding:16px 0}} .stage{{height:60vh}} }}
@media (prefers-reduced-motion: reduce){{ .stage canvas{{cursor:default}} }}
</style>

<div class="hero"><div class="hero-in">
  <div>
    <span class="lab">Dự án KHKT · Phòng thí nghiệm ảo · thiết kế 27/10/2026</span>
    <h1>Tay cầm VR <b>Halo</b></h1>
    <p>Một tay cầm gọn trong lòng bàn tay. Bên trong có ESP32 38 chân và BMI160, joystick đặt ở mặt nghiêng cho ngón cái. Cò analog đo bằng cảm biến Hall. Vòng halo phía trước để webcam (OpenCV) đo xa gần và vị trí tay.</p>
  </div>
  <aside class="panel">
    <div><span class="lab">Tách rời</span><input type="range" id="tach" min="0" max="1" step="0.01" value="0" aria-label="Tách rời các chi tiết"></div>
    <div style="display:grid;gap:8px">
      <label><input type="checkbox" id="quay" checked>Tự xoay</label>
      <label><input type="checkbox" id="hienLk" checked>Mạch bên trong</label>
      <label><input type="checkbox" id="trongSuot">Vỏ trong suốt</label>
      <label><input type="checkbox" id="hienNhan">Nhãn</label>
    </div>
    <div><span class="lab">Chi tiết</span><ul class="parts" id="ds"></ul></div>
    <p class="hint">Kéo để xoay, cuộn để phóng to. Bấm tên chi tiết để làm nổi nó lên.</p>
  </aside>
  <div class="stage" id="stage"><div id="nhan" hidden></div><div class="loading" id="tai">Đang tải mô hình 3D…</div></div>
  <div class="specs">
    <div><b>≈ {tong_g + 60:.0f} g</b><span>cả mạch, chưa tính pin</span></div>
    <div><b>37 × 33</b><span>mặt cắt cán (mm)</span></div>
    <div><b>Ø70</b><span>vòng halo cho OpenCV</span></div>
    <div><b>0–100%</b><span>cò Hall analog</span></div>
    <div><b>{len(in_rows)}</b><span>chi tiết in 3D</span></div>
  </div>
</div></div>

<div class="wrap">
<section>
  <h2>Bản vẽ 2D</h2>
  <p class="lead">Các bản vẽ được chiếu và cắt thẳng từ mô hình 3D, nên khớp với file STL. Chiếu theo phương pháp góc thứ nhất như sách Công nghệ, đơn vị mm.</p>
  <nav class="toc" aria-label="Các bản vẽ">{"".join(f'<a href="#{so.lower()}">{so} {ten}</a>' for so, ten, _ in to)}</nav>
  {sheets}
</section>

<section>
  <h2>Đấu nối ESP32</h2>
  <div class="card">{bang(["Linh kiện", "Chân", "ESP32", "Ghi chú"], DAU_NOI)}</div>
</section>

<section>
  <h2>Chi tiết in 3D</h2>
  <p class="lead">File STL nằm trong <code>docs/tay-cam-v2/in/</code> và đã xoay sẵn đúng hướng in. Nên in PETG hoặc PLA, lớp 0,2 mm, 3 lớp vách, độ đặc 25%. Vòng halo in bằng nhựa màu huỳnh quang để OpenCV dễ lọc màu.</p>
  <div class="card">{bang_in}</div>
  <div class="cols">
    <div class="card"><h3>Vật tư lắp ráp</h3><ul class="notes">
      <li>2 vít M2 × 10 tự ren để ghép hai nửa vỏ; 1 vít M3 × 16 làm chốt cò; 2 vít M3 × 12 bắt vòng halo.</li>
      <li>Nam châm neodymium Ø6 × 3 mm (dán keo vào hốc ở mặt sau cò) và lò xo nén Ø4 × 8 mm.</li>
      <li>Dây đeo cổ tay xỏ qua tai ở nắp đáy. Dây quàng tay 20 mm xỏ qua tai ở lưng đầu tay cầm.</li>
    </ul></div>
    <div class="card warn"><h3>Cần kiểm trước khi in</h3><ul class="notes">
      <li>ESP32 nằm dọc trong cán, giữa hai rãnh kẹp, nên phải <b>tháo chân header hoặc mua loại chưa hàn chân</b> rồi hàn dây thẳng. Đo mạch thật: ở đây giả định 55 × 28 mm.</li>
      <li>KY-023 phải bẻ thẳng chân rồi hàn dây, vì đầu tay cầm không đủ chỗ cắm dupont.</li>
      <li>Khoang pin dành cho LiPo 40 × 25 × 9 (≈ 1000 mAh) và mạch sạc TP4056 USB-C 25 × 18. Phần nguồn chốt sau.</li>
      <li>In thử riêng cò và một nửa vỏ trước, để thử khe hở nam châm và độ nặng tay của lò xo.</li>
    </ul></div>
  </div>
</section>
</div>

<script type="importmap">{{"imports":{{"three":"https://cdn.jsdelivr.net/npm/three@0.160.0/build/three.module.js","three/addons/":"https://cdn.jsdelivr.net/npm/three@0.160.0/examples/jsm/"}}}}</script>
<script type="module">
import * as THREE from 'three';
import {{ OrbitControls }} from 'three/addons/controls/OrbitControls.js';
import {{ STLLoader }} from 'three/addons/loaders/STLLoader.js';
import {{ RoomEnvironment }} from 'three/addons/environments/RoomEnvironment.js';

const CANH = {canh_js};
const stage = document.getElementById('stage');
const nhanBox = document.getElementById('nhan');
const renderer = new THREE.WebGLRenderer({{ antialias: true, alpha: true, preserveDrawingBuffer: true }});
renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
renderer.toneMapping = THREE.ACESFilmicToneMapping;
renderer.outputColorSpace = THREE.SRGBColorSpace;
stage.prepend(renderer.domElement);
const scene = new THREE.Scene();
const pmrem = new THREE.PMREMGenerator(renderer);
scene.environment = pmrem.fromScene(new RoomEnvironment(renderer), 0.04).texture;
// tay cầm dựng theo hệ X trước, Y trái, Z lên: đổi sang Y lên của three.js
const goc = new THREE.Group(); goc.rotation.x = -Math.PI / 2; scene.add(goc);
const camera = new THREE.PerspectiveCamera(30, 1, 1, 4000);
camera.position.set(320, 110, 260);
const controls = new OrbitControls(camera, renderer.domElement);
controls.target.set(0, -30, 0); controls.enableDamping = true; controls.autoRotateSpeed = 1.2;
controls.autoRotate = !matchMedia('(prefers-reduced-motion: reduce)').matches;
document.getElementById('quay').checked = controls.autoRotate;
const den = new THREE.DirectionalLight(0xffffff, 1.4); den.position.set(200, 300, 200); scene.add(den);
const vien = new THREE.DirectionalLight(0x6fdcf0, 1.2); vien.position.set(-250, 80, -200); scene.add(vien);
// bóng mờ dưới chân
const san = new THREE.Mesh(new THREE.CircleGeometry(95, 64), new THREE.MeshBasicMaterial({{ color: 0x000000, transparent: true, opacity: 0.18 }}));
san.rotation.x = -Math.PI / 2; san.position.y = -140; scene.add(san);

const loader = new STLLoader();
const vat = [];
const VL = {{
  vo_trai: {{ color: '#eef2f4', roughness: 0.42, metalness: 0.0, clearcoat: 0.6 }},
  vo_phai: {{ color: '#eef2f4', roughness: 0.42, metalness: 0.0, clearcoat: 0.6 }},
  co: {{ color: '#1b2329', roughness: 0.35, metalness: 0.3 }},
  nap_day: {{ color: '#1b2329', roughness: 0.35, metalness: 0.3 }},
  halo: {{ color: '#0b3d4a', roughness: 0.25, metalness: 0.1, emissive: '#18c8e6', emissiveIntensity: 1.6 }},
  dai_sang: {{ color: '#0b3d4a', roughness: 0.25, metalness: 0.1, emissive: '#18c8e6', emissiveIntensity: 1.6 }},
  tam_op: {{ color: '#1b2329', roughness: 0.3, metalness: 0.35 }},
}};
let goi = null;     // mo_hinh.json: mọi lưới STL nhị phân, mã base64
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
async function dung() {{
  const ds = document.getElementById('ds');
  for (const c of CANH.chi_tiet) {{
    const g = await tai(c.ma);
    const tham = VL[c.ma] || {{ color: c.mau, roughness: 0.5, metalness: 0.2 }};
    const mat = new THREE.MeshPhysicalMaterial({{ ...tham, transparent: true, opacity: 1 }});
    const mesh = new THREE.Mesh(g, mat);
    goc.add(mesh);
    g.computeBoundingBox();
    const tam = new THREE.Vector3(); g.boundingBox.getCenter(tam);
    const tag = document.createElement('div'); tag.className = 'tag'; tag.textContent = c.ten; nhanBox.appendChild(tag);
    const v = {{ ma: c.ma, mesh, tach: new THREE.Vector3(...c.tach), tam, top: g.boundingBox.max.z, tag, lk: !!c.linh_kien }};
    vat.push(v);
    const li = document.createElement('li'); const b = document.createElement('button');
    b.innerHTML = `<span class="sw" style="background:${{c.mau}}"></span>${{c.ten}}`;
    b.setAttribute('aria-pressed', 'false');
    b.addEventListener('click', () => chon(v, b));
    li.appendChild(b); ds.appendChild(li);
  }}
  document.getElementById('tai').remove();
  capNhat();
}}
let dangChon = null;
function chon(v, b) {{
  const tat = dangChon === v;
  document.querySelectorAll('.parts button').forEach(x => x.setAttribute('aria-pressed', 'false'));
  dangChon = tat ? null : v;
  if (!tat) b.setAttribute('aria-pressed', 'true');
  capNhat();
}}
function capNhat() {{
  const t = +document.getElementById('tach').value;
  const hienLk = document.getElementById('hienLk').checked;
  const ts = document.getElementById('trongSuot').checked;
  for (const v of vat) {{
    v.mesh.position.copy(v.tach).multiplyScalar(t);
    v.mesh.visible = v.lk ? hienLk : true;
    const vo = v.ma === 'vo_trai' || v.ma === 'vo_phai';
    const mo = dangChon && dangChon !== v ? 0.15 : (vo && ts ? 0.28 : 1);
    v.mesh.material.opacity = mo; v.mesh.material.depthWrite = mo === 1;
  }}
}}
['tach', 'hienLk', 'trongSuot'].forEach(id => document.getElementById(id).addEventListener('input', capNhat));
document.getElementById('hienNhan').addEventListener('input', e => nhanBox.hidden = !e.target.checked);
document.getElementById('quay').addEventListener('input', e => controls.autoRotate = e.target.checked);
controls.addEventListener('start', () => {{ controls.autoRotate = false; document.getElementById('quay').checked = false; }});

function coKhung() {{ const w = stage.clientWidth, h = stage.clientHeight; renderer.setSize(w, h, false); camera.aspect = w / h; camera.updateProjectionMatrix(); }}
new ResizeObserver(coKhung).observe(stage); coKhung();
const p = new THREE.Vector3();
function ve() {{
  controls.update();
  renderer.render(scene, camera);
  if (!nhanBox.hidden) {{
    const w = stage.clientWidth, h = stage.clientHeight;
    for (const v of vat) {{
      const an = !v.mesh.visible || (dangChon && dangChon !== v);
      v.tag.hidden = an; if (an) continue;
      p.set(v.tam.x, v.tam.y, v.top + 4).add(v.mesh.position); goc.localToWorld(p); p.project(camera);
      v.tag.style.left = ((p.x + 1) / 2 * w) + 'px'; v.tag.style.top = ((1 - p.y) / 2 * h - 6) + 'px';
    }}
  }}
  requestAnimationFrame(ve);
}}
dung().catch(e => {{ document.getElementById('tai').textContent = 'Không tải được mô hình 3D: ' + e.message; }});
ve();
</script>
"""
    import base64
    goi = {}
    for c in canh["chi_tiet"]:
        with open(os.path.join(thu_muc, c["ma"] + ".stl"), "rb") as fh:
            goi[c["ma"]] = base64.b64encode(fh.read()).decode("ascii")
    with open(os.path.join(thu_muc, "mo_hinh.json"), "w", encoding="ascii") as fh:
        json.dump(goi, fh)
    ra = os.path.join(thu_muc, "tay_cam_v2.html")
    with open(ra, "w", encoding="utf-8") as fh:
        fh.write(html)
    print(ra)


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    main()
