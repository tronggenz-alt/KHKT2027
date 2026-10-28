/**
 * frontend/nhan-vat/tro-ly.js
 * Widget Trợ Lý Nhà Nghiên Cứu 3D góc màn hình cho web CHEMISTRY.
 * Sử dụng Three.js r160 vendor cục bộ, DRACOLoader nội bộ, và Animate UI cho bong bóng lời thoại.
 */

import * as THREE from '/frontend/vendor/three/three.module.js';
import { GLTFLoader } from '/frontend/vendor/three/addons/loaders/GLTFLoader.js';
import { DRACOLoader } from '/frontend/vendor/three/addons/loaders/DRACOLoader.js';
import { hienRa, anDi, giamChuyenDong, TOKENS } from '/frontend/chuyen-dong.js';

class TroLyNhaNghienCuu {
  constructor() {
    this.daKhoiTao = false;
    this.dangBat = false;
    this.container = null;
    this.canvas = null;
    this.bongBong = null;
    this.renderer = null;
    this.scene = null;
    this.camera = null;
    this.mixer = null;
    this.actions = {};
    this.currentAction = null;
    this.model = null;
    this.manifest = null;
    this.clock = new THREE.Clock();
    this.animationFrameId = null;
    this.timeoutBongBong = null;

    // Tự động kiểm tra cài đặt
    this.khoiTaoCaiDat();
  }

  khoiTaoCaiDat() {
    if (typeof window === 'undefined') return;

    // Mặc định: bật trên desktop (>= 1024px), tắt trên điện thoại
    const isDesktop = window.innerWidth >= 1024;
    const luu = localStorage.getItem('chemistry_tro_ly_ai');
    if (luu === null) {
      this.dangBat = isDesktop;
      localStorage.setItem('chemistry_tro_ly_ai', this.dangBat ? '1' : '0');
    } else {
      this.dangBat = luu === '1';
    }

    // Đăng ký API toàn cục
    window.TroLy = {
      trangThai: (ten) => this.chuyenTrangThai(ten),
      noi: (chu, ms = 4000) => this.noi(chu, ms),
      bat: () => this.bat(),
      tat: () => this.tat(),
      toggle: () => this.toggle(),
      dangBat: () => this.dangBat
    };

    if (this.dangBat) {
      this.khoiTaoDOM();
    }
  }

  async napManifest() {
    if (this.manifest) return this.manifest;
    try {
      const res = await fetch('/frontend/nhan-vat/nhan-vat-nha-nghien-cuu.json');
      if (res.ok) {
        this.manifest = await res.json();
      }
    } catch (e) {
      console.warn('[TroLy] Không đọc được manifest:', e);
    }
    return this.manifest;
  }

  khoiTaoDOM() {
    if (this.daKhoiTao || typeof document === 'undefined') return;
    this.daKhoiTao = true;

    // Container chính
    this.container = document.createElement('div');
    this.container.id = 'tro-ly-container';
    this.container.style.cssText = `
      position: fixed;
      bottom: 16px;
      right: 16px;
      width: 220px;
      height: 300px;
      z-index: 9999;
      pointer-events: none;
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: flex-end;
    `;

    // Bong bóng lời thoại (Animate UI)
    this.bongBong = document.createElement('div');
    this.bongBong.id = 'tro-ly-bong-bong';
    this.bongBong.style.cssText = `
      position: absolute;
      bottom: 270px;
      right: 10px;
      max-width: 200px;
      background: rgba(15, 23, 42, 0.92);
      backdrop-filter: blur(8px);
      -webkit-backdrop-filter: blur(8px);
      border: 1px solid rgba(56, 189, 248, 0.4);
      border-radius: 12px;
      padding: 10px 14px;
      color: #f8fafc;
      font-family: system-ui, -apple-system, sans-serif;
      font-size: 13px;
      line-height: 1.45;
      box-shadow: 0 10px 25px -5px rgba(0, 0, 0, 0.5), 0 0 15px rgba(56, 189, 248, 0.2);
      display: none;
      pointer-events: auto;
      transform-origin: bottom right;
    `;

    // Mũi nhọn tam giác chỉ vào nhân vật
    const tamGiac = document.createElement('div');
    tamGiac.style.cssText = `
      position: absolute;
      bottom: -6px;
      right: 24px;
      width: 0;
      height: 0;
      border-left: 6px solid transparent;
      border-right: 6px solid transparent;
      border-top: 6px solid rgba(15, 23, 42, 0.92);
    `;
    this.bongBong.appendChild(tamGiac);

    this.noiDungBongBong = document.createElement('div');
    this.bongBong.appendChild(this.noiDungBongBong);

    // Canvas 3D
    this.canvas = document.createElement('canvas');
    this.canvas.id = 'tro-ly-canvas';
    this.canvas.style.cssText = `
      width: 220px;
      height: 270px;
      pointer-events: none;
    `;

    // Nút thu gọn / đóng góc
    this.nutDong = document.createElement('button');
    this.nutDong.title = 'Ẩn trợ lý 3D';
    this.nutDong.innerHTML = '✕';
    this.nutDong.style.cssText = `
      position: absolute;
      top: 25px;
      right: 5px;
      width: 22px;
      height: 22px;
      border-radius: 50%;
      background: rgba(30, 41, 59, 0.8);
      border: 1px solid rgba(255, 255, 255, 0.2);
      color: #94a3b8;
      font-size: 11px;
      cursor: pointer;
      display: flex;
      align-items: center;
      justify-content: center;
      pointer-events: auto;
      transition: all 0.15s ease;
    `;
    this.nutDong.onclick = () => this.tat();

    this.container.appendChild(this.bongBong);
    this.container.appendChild(this.canvas);
    this.container.appendChild(this.nutDong);
    document.body.appendChild(this.container);

    this.khoiTaoThree();
  }

  async khoiTaoThree() {
    await this.napManifest();

    const w = 220;
    const h = 270;

    this.scene = new THREE.Scene();
    this.camera = new THREE.PerspectiveCamera(38, w / h, 0.1, 20);
    // Vị trí camera nhìn ngang ngực/đầu nhân vật (y ~ 1.25m đến 1.6m)
    this.camera.position.set(0, 1.45, 1.45);
    this.camera.lookAt(0, 1.38, 0);

    this.renderer = new THREE.WebGLRenderer({
      canvas: this.canvas,
      alpha: true,
      antialias: true,
      powerPreference: 'low-power'
    });
    this.renderer.setSize(w, h);
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    this.renderer.toneMapping = THREE.ACESFilmicToneMapping;
    this.renderer.toneMappingExposure = 1.15;

    // Ánh sáng chân dung mềm
    const ambient = new THREE.AmbientLight(0xffffff, 1.3);
    this.scene.add(ambient);

    const keyLight = new THREE.DirectionalLight(0xffffff, 2.0);
    keyLight.position.set(1.5, 2.5, 2);
    this.scene.add(keyLight);

    const rimLight = new THREE.DirectionalLight(0x38bdf8, 1.2);
    rimLight.position.set(-1.5, 2, -1.5);
    this.scene.add(rimLight);

    // Tải mô hình
    this.napMoHinh();

    // Vòng lặp render
    this.batDauVongLap();

    // Tự động dừng render khi tab ẩn
    document.addEventListener('visibilitychange', () => {
      if (document.hidden) {
        this.dungVongLap();
      } else if (this.dangBat) {
        this.batDauVongLap();
      }
    });
  }

  napMoHinh() {
    const dracoLoader = new DRACOLoader();
    dracoLoader.setDecoderPath('/frontend/vendor/three/draco/');

    const loader = new GLTFLoader();
    loader.setDRACOLoader(dracoLoader);

    loader.load(
      '/frontend/nhan-vat/researcher.glb',
      (gltf) => {
        this.model = gltf.scene;
        this.model.position.set(0, 0, 0);
        this.scene.add(this.model);

        // Khởi tạo animations nếu có
        if (gltf.animations && gltf.animations.length > 0) {
          this.mixer = new THREE.AnimationMixer(this.model);
          for (const clip of gltf.animations) {
            const action = this.mixer.clipAction(clip);
            const cfg = this.manifest?.animations?.[clip.name];
            if (cfg && !cfg.loop) {
              action.setLoop(THREE.LoopOnce, 1);
              action.clampWhenFinished = true;
            }
            this.actions[clip.name] = action;
          }
          this.choiClip('idle');
        }

        // Chào mừng khi vào trang
        this.chuyenTrangThai('pageEnter');
      },
      undefined,
      (err) => {
        console.warn('[TroLy] Dùng hình ảnh dự phòng do chưa nạp được GLB:', err);
      }
    );
  }

  choiClip(ten, fade = 0.3) {
    if (!this.mixer) return;
    let next = this.actions[ten];
    // Lùi về idle nếu thiếu clip
    if (!next) {
      next = this.actions['idle'] || Object.values(this.actions)[0];
    }
    if (!next || next === this.currentAction) return;

    if (giamChuyenDong()) {
      if (this.currentAction) this.currentAction.stop();
      next.play();
      this.currentAction = next;
      return;
    }

    next.reset().fadeIn(fade).play();
    if (this.currentAction) {
      this.currentAction.fadeOut(fade);
    }
    this.currentAction = next;
  }

  async chuyenTrangThai(tenTrangThai) {
    if (!this.dangBat) return;
    const map = this.manifest?.stateMap;
    const target = map?.[tenTrangThai];

    if (!target) {
      this.choiClip('idle');
      return;
    }

    if (Array.isArray(target)) {
      if (giamChuyenDong()) {
        this.choiClip(target[target.length - 1] || 'idle');
        return;
      }
      for (const n of target) {
        this.choiClip(n);
        const a = this.actions[n];
        if (a && a.loop === THREE.LoopOnce) {
          await new Promise((res) => {
            const onFinish = (e) => {
              if (e.action === a) {
                this.mixer.removeEventListener('finished', onFinish);
                res();
              }
            };
            this.mixer.addEventListener('finished', onFinish);
          });
        }
      }
    } else {
      this.choiClip(target);
    }
  }

  noi(chu, ms = 4000) {
    if (!this.dangBat || !this.bongBong) return;

    if (this.timeoutBongBong) {
      clearTimeout(this.timeoutBongBong);
    }

    this.noiDungBongBong.innerText = chu;
    hienRa(this.bongBong, { kieu: 'pop', display: 'block' });

    this.timeoutBongBong = setTimeout(() => {
      anDi(this.bongBong);
      this.timeoutBongBong = null;
    }, ms);
  }

  batDauVongLap() {
    if (this.animationFrameId) return;
    const vongLap = () => {
      this.animationFrameId = requestAnimationFrame(vongLap);
      const dt = this.clock.getDelta();

      if (this.mixer) {
        this.mixer.update(dt);
      } else if (this.model) {
        // Nhịp thở procedural nhẹ nhàng khi chưa có xương rigged
        const t = this.clock.getElapsedTime();
        this.model.position.y = Math.sin(t * 1.5) * 0.005;
        this.model.rotation.y = Math.sin(t * 0.5) * 0.04;
      }

      if (this.renderer && this.scene && this.camera) {
        this.renderer.render(this.scene, this.camera);
      }
    };
    vongLap();
  }

  dungVongLap() {
    if (this.animationFrameId) {
      cancelAnimationFrame(this.animationFrameId);
      this.animationFrameId = null;
    }
  }

  bat() {
    this.dangBat = true;
    localStorage.setItem('chemistry_tro_ly_ai', '1');
    if (!this.daKhoiTao) {
      this.khoiTaoDOM();
    } else if (this.container) {
      this.container.style.display = 'flex';
      this.batDauVongLap();
    }
  }

  tat() {
    this.dangBat = false;
    localStorage.setItem('chemistry_tro_ly_ai', '0');
    if (this.container) {
      this.container.style.display = 'none';
      this.dungVongLap();
    }
  }

  toggle() {
    if (this.dangBat) this.tat();
    else this.bat();
  }
}

// Khởi tạo singleton khi DOM sẵn sàng
if (typeof window !== 'undefined') {
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => new TroLyNhaNghienCuu());
  } else {
    new TroLyNhaNghienCuu();
  }
}

export default TroLyNhaNghienCuu;
