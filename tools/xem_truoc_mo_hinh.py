import http.server
import os
import shutil
import socketserver
import threading
import time
from pathlib import Path
from playwright.sync_api import sync_playwright

GOC = Path(__file__).resolve().parents[1]
OUTPUT_DIR = GOC / "outputs" / "nhan-vat"
OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

# Copy GLB to outputs/nhan-vat/researcher.glb for web serving
glb_src = Path(os.path.expanduser("~/Downloads/MoHinh3D/researcher-60k.glb"))
glb_dst = OUTPUT_DIR / "researcher.glb"
shutil.copyfile(glb_src, glb_dst)

html_content = """<!DOCTYPE html>
<html>
<head>
  <meta charset="utf-8">
  <title>Xem Trước Mô Hình Nhân Vật</title>
  <style>
    body { margin: 0; background: #0f172a; overflow: hidden; }
    canvas { width: 100vw; height: 100vh; display: block; }
  </style>
</head>
<body>
  <script type="importmap">
  {
    "imports": {
      "three": "/frontend/vendor/three/three.module.js",
      "three/addons/": "/frontend/vendor/three/addons/"
    }
  }
  </script>
  <script type="module">
    import * as THREE from 'three';
    import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';

    const scene = new THREE.Scene();
    scene.background = new THREE.Color(0x1e293b);

    const camera = new THREE.PerspectiveCamera(45, window.innerWidth / window.innerHeight, 0.1, 100);
    const renderer = new THREE.WebGLRenderer({ antialias: true, preserveDrawingBuffer: true });
    renderer.setSize(window.innerWidth, window.innerHeight);
    renderer.setPixelRatio(window.devicePixelRatio);
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.2;
    renderer.outputColorSpace = THREE.SRGBColorSpace;
    document.body.appendChild(renderer.domElement);

    // Lights
    const ambientLight = new THREE.AmbientLight(0xffffff, 1.2);
    scene.add(ambientLight);

    const dirLight1 = new THREE.DirectionalLight(0xffffff, 2.0);
    dirLight1.position.set(2, 4, 3);
    scene.add(dirLight1);

    const dirLight2 = new THREE.DirectionalLight(0x94a3b8, 1.2);
    dirLight2.position.set(-2, 2, -2);
    scene.add(dirLight2);

    // Grid floor
    const grid = new THREE.GridHelper(10, 20, 0x38bdf8, 0x334155);
    scene.add(grid);

    window.__loaded = false;
    let model = null;

    const loader = new GLTFLoader();
    loader.load('/outputs/nhan-vat/researcher.glb', (gltf) => {
      model = gltf.scene;
      scene.add(model);
      window.__loaded = true;
    }, undefined, (err) => {
      console.error('Error loading GLTF:', err);
      window.__loadError = err.message;
    });

    window.datCamera = (camX, camY, camZ, lookX, lookY, lookZ) => {
      camera.position.set(camX, camY, camZ);
      camera.lookAt(lookX, lookY, lookZ);
      renderer.render(scene, camera);
    };

    function animate() {
      requestAnimationFrame(animate);
      if (model && !window.__manualCamera) {
        renderer.render(scene, camera);
      }
    }
    animate();
  </script>
</body>
</html>
"""

(OUTPUT_DIR / "xem_truoc.html").write_text(html_content, encoding="utf-8")


class CustomHandler(http.server.SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=str(GOC), **kwargs)

    def log_message(self, format, *args):
        pass  # Suppress logs


def run():
    PORT = 8991
    server = socketserver.TCPServer(("127.0.0.1", PORT), CustomHandler)
    server_thread = threading.Thread(target=server.serve_forever, daemon=True)
    server_thread.start()
    print(f"HTTP server started at http://127.0.0.1:{PORT}")

    with sync_playwright() as p:
        browser = p.chromium.launch(channel="chrome", headless=True)
        page = browser.new_page(viewport={"width": 1024, "height": 1024})
        page.goto(f"http://127.0.0.1:{PORT}/outputs/nhan-vat/xem_truoc.html")
        page.wait_for_function("window.__loaded === true || window.__loadError", timeout=15000)

        err = page.evaluate("window.__loadError")
        if err:
            print("Lỗi load model:", err)
            server.shutdown()
            return

        time.sleep(1.0)
        page.evaluate("window.__manualCamera = true;")

        # 1. Toàn thân chính diện (nhìn từ phía trước Z > 0, ngang ngực)
        page.evaluate("window.datCamera(0, 0.9, 2.8, 0, 0.85, 0)")
        time.sleep(0.5)
        path1 = OUTPUT_DIR / "01_toan_than_chinh_dien.png"
        page.screenshot(path=str(path1))
        print(f"Đã lưu: {path1}")

        # 2. Toàn thân góc nghiêng 3/4
        page.evaluate("window.datCamera(1.5, 1.0, 2.2, 0, 0.85, 0)")
        time.sleep(0.5)
        path2 = OUTPUT_DIR / "02_toan_than_goc_nghieng.png"
        page.screenshot(path=str(path2))
        print(f"Đã lưu: {path2}")

        # 3. Cận cảnh khuôn mặt và thân trên
        page.evaluate("window.datCamera(0, 1.55, 0.9, 0, 1.55, 0)")
        time.sleep(0.5)
        path3 = OUTPUT_DIR / "03_can_canh_khuon_mat.png"
        page.screenshot(path=str(path3))
        print(f"Đã lưu: {path3}")

        browser.close()

    server.shutdown()
    print("Hoàn thành chụp ảnh xem trước mô hình.")


if __name__ == "__main__":
    run()
