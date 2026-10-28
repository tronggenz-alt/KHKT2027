/**
 * frontend/hoa-hoc/phong-thi-nghiem/lab-unity-app.js
 * Tích hợp phòng thí nghiệm Unity WebGL trực tiếp vào ứng dụng máy tính CHEMISTRY.
 * Cầu nối hai chiều: window.PTNLoiTrang (lõi mô phỏng mo-phong.js) <-> Unity WebGL (CauNoiTrang.jslib).
 */

const M = window.PTNMoPhong || (typeof self !== "undefined" ? self.PTNMoPhong : null);
const KHOA_UU_TIEN = "ptn-che-do-uu-tien";

function taoPhanTu(tag, thuocTinh = {}, ...con) {
  const el = document.createElement(tag);
  for (const [k, v] of Object.entries(thuocTinh)) {
    if (v === null || v === undefined || v === false) continue;
    if (k === "class") el.className = v;
    else if (k === "text") el.textContent = v;
    else if (k === "on") {
      for (const [sk, fn] of Object.entries(v)) el.addEventListener(sk, fn);
    } else if (k === "style") {
      Object.assign(el.style, v);
    } else if (k in el && typeof v !== "string") {
      el[k] = v;
    } else {
      el.setAttribute(k, v === true ? "" : String(v));
    }
  }
  for (const c of con.flat()) {
    if (c !== null && c !== undefined && c !== false) {
      el.append(c instanceof Node ? c : String(c));
    }
  }
  return el;
}

let unityInstance = null;
let daSanSang = false;
let tramHienTai = null;
let dangChoChay = false;
let idRaf = null;
let mocThoiGianTruoc = 0;
let cacMoPhong = {};
let dsFps = [];
let lanToastFpsCuoi = 0;
let hopThoaiGiaoDien = null;
let hamChuyenThreeJs = null;
let duLieuCacTram = {};
let bangIonToanCuc = null;

function tinhFpsTrungBinh() {
  if (!dsFps.length) return 60.0;
  const tong = dsFps.reduce((a, b) => a + b, 0);
  return Math.round((tong / dsFps.length) * 10) / 10;
}

function ghiNhanFps(fps) {
  if (typeof fps !== "number" || isNaN(fps) || fps <= 0) return;
  dsFps.push(fps);
  if (dsFps.length > 10) dsFps.shift(); // Giữ tối đa 10 mẫu gần nhất (~20s)

  const tb = tinhFpsTrungBinh();
  const bayGio = Date.now();
  if (dsFps.length >= 5 && tb < 25 && bayGio - lanToastFpsCuoi > 60000) {
    lanToastFpsCuoi = bayGio;
    hienThongBao("Máy đang chậm – thử bấm ⚡ Mở cửa sổ mượt.", "canh-bao");
  }
}

function hienThongBao(chu, loai = "ok") {
  const hop = document.getElementById("toasts");
  if (!hop) return;
  const t = taoPhanTu("div", { class: `toast ${loai}`, role: "status", text: chu });
  hop.append(t);
  while (hop.childElementCount > 2) hop.firstElementChild.remove();
  setTimeout(() => t.remove(), 4200);
}

function chayVongLapMoPhong(moc) {
  if (!mocThoiGianTruoc) mocThoiGianTruoc = moc;
  const dt = Math.min(0.1, (moc - mocThoiGianTruoc) / 1000);
  mocThoiGianTruoc = moc;

  if (dangChoChay && tramHienTai && cacMoPhong[tramHienTai]) {
    try {
      cacMoPhong[tramHienTai].capNhat(dt);
    } catch (e) {
      console.error("[LabUnityApp] Lỗi cập nhật mô phỏng:", e);
    }
  }

  idRaf = requestAnimationFrame(chayVongLapMoPhong);
}

function khoiTaoMoPhong(duLieu, bangIon, cheDo) {
  duLieuCacTram = duLieu || {};
  bangIonToanCuc = bangIon || null;
  cacMoPhong = {};

  const moPhong = window.PTNMoPhong || M;
  if (!moPhong) {
    console.warn("[LabUnityApp] Không tìm thấy window.PTNMoPhong");
    return;
  }

  for (const [id, tn] of Object.entries(duLieuCacTram)) {
    const mp = moPhong.tao(tn, { bangIon: bangIonToanCuc, cheDo: cheDo || "huong_dan" });
    mp.on("trang_thai", (s) => {
      if (unityInstance && daSanSang) {
        try {
          unityInstance.SendMessage("CauNoiTrang", "NhanTrangThai", JSON.stringify(s));
        } catch {}
      }
    });
    mp.on("so_tay", (st) => {
      if (unityInstance && daSanSang) {
        try {
          unityInstance.SendMessage("CauNoiTrang", "NhanSoTay", JSON.stringify(st));
        } catch {}
      }
    });
    mp.on("hieu_ung", (e) => {
      if (unityInstance && daSanSang) {
        try {
          unityInstance.SendMessage("CauNoiTrang", "NhanHieuUng", JSON.stringify(e));
        } catch {}
      }
    });
    cacMoPhong[id] = mp;
  }
}

function caiDatCauNoiLoiTrang() {
  window.PTNLoiTrang = {
    tao(id, cheDo) {
      if (!cacMoPhong[id] && duLieuCacTram[id]) {
        const moPhong = window.PTNMoPhong || M;
        if (moPhong) {
          cacMoPhong[id] = moPhong.tao(duLieuCacTram[id], { bangIon: bangIonToanCuc, cheDo: cheDo || "huong_dan" });
        }
      }
    },

    thaoTac(id, lenh, thamSoJson) {
      const mp = cacMoPhong[id];
      if (!mp) return JSON.stringify({ ok: false, loi: "Không tìm thấy trạm " + id });
      let thamSo = {};
      try {
        if (typeof thamSoJson === "string" && thamSoJson.trim()) thamSo = JSON.parse(thamSoJson);
        else if (typeof thamSoJson === "object" && thamSoJson) thamSo = thamSoJson;
      } catch (e) {
        console.warn("[LabUnityApp] Lỗi giải mã tham số thao tác:", e);
      }
      const kq = mp.thaoTac(lenh, thamSo);
      const tt = mp.trangThai();
      if (unityInstance && daSanSang) {
        try {
          unityInstance.SendMessage("CauNoiTrang", "NhanTrangThai", JSON.stringify(tt));
        } catch {}
      }
      return JSON.stringify(kq || { ok: true });
    },

    trangThai(id) {
      const mp = cacMoPhong[id];
      if (!mp) return "{}";
      return JSON.stringify(mp.trangThai() || {});
    },

    soTay(id) {
      const mp = cacMoPhong[id];
      if (!mp) return "[]";
      return JSON.stringify(mp.soTay() || []);
    },

    choChay(choChay) {
      dangChoChay = !!choChay;
    },

    nhanSuKien(ten, jsonStr) {
      let dl = {};
      try {
        if (typeof jsonStr === "string" && jsonStr.trim()) dl = JSON.parse(jsonStr);
      } catch {}

      if (ten === "san_sang") {
        daSanSang = true;
        const manTai = document.getElementById("ptn-dang-tai");
        if (manTai) manTai.classList.add("an");

        const theme = document.documentElement.dataset.theme || "sang";
        try {
          unityInstance?.SendMessage("CauNoiTrang", "DoiGiaoDien", theme);
        } catch {}

        const thamUrl = new URLSearchParams(window.location.search).get("tn");
        const tnMo = thamUrl || tramHienTai;
        if (tnMo) {
          try {
            unityInstance?.SendMessage("CauNoiTrang", "MoThiNghiem", tnMo);
          } catch {}
        }
      } else if (ten === "tram") {
        tramHienTai = dl.id || null;
        dangChoChay = !!dl.tai_ban;
        if (window.history && window.history.replaceState) {
          const duongDanMoi = tramHienTai ? `?tn=${encodeURIComponent(tramHienTai)}` : window.location.pathname;
          window.history.replaceState(null, "", duongDanMoi);
        }
      } else if (ten === "fps") {
        ghiNhanFps(dl.fps);
      } else if (ten === "mo_cua_so_muot") {
        chuyenSangCuaSoRieng();
      } else if (ten === "thoat") {
        LabUnityApp.dong().then(() => {
          window.location.href = "/";
        });
      } else if (ten === "loi") {
        console.error("[LabUnityApp] Lỗi từ Unity:", dl.thong_diep || dl);
      }
    }
  };
}

async function chuyenSangCuaSoRieng() {
  const tramMucTieu = tramHienTai || "chuan-do";
  try {
    if (unityInstance) {
      await unityInstance.Quit();
      unityInstance = null;
    }
  } catch (e) {
    console.warn("[LabUnityApp] Lỗi khi Quit Unity trước khi mở app:", e);
  }

  const khung = document.getElementById("ptn-khung");
  if (khung) {
    const theCuaSo = taoPhanTu("div", { class: "ptn-app-the-cua-so-rieng" },
      taoPhanTu("div", { class: "ptn-app-the-cua-so-rieng-tieu-de", text: "Phòng lab đang mở ở cửa sổ riêng (mượt hơn)" }),
      taoPhanTu("div", { class: "ptn-app-the-cua-so-rieng-mo-ta", text: "Ứng dụng máy tính độc lập đang chạy với hiệu năng và đồ hoạ tối đa." }),
      taoPhanTu("button", {
        type: "button",
        class: "btn btn-primary",
        text: "Chạy lại trong app",
        on: {
          click: () => {
            theCuaSo.remove();
            LabUnityApp.mo({
              duLieu: duLieuCacTram,
              bangIon: bangIonToanCuc,
              id: tramMucTieu,
              onChuyenThreeJs: hamChuyenThreeJs
            });
          }
        }
      })
    );
    khung.append(theCuaSo);
  }

  if (window.chemistryApp && typeof window.chemistryApp.moAppLab === "function") {
    try {
      await window.chemistryApp.moAppLab(tramMucTieu);
    } catch (err) {
      console.error("[LabUnityApp] Không thể mở ứng dụng native:", err);
      hienThongBao("Không mở được app ngoài. Đang mở lại trong app...", "canh-bao");
      const card = khung?.querySelector(".ptn-app-the-cua-so-rieng");
      card?.remove();
      LabUnityApp.mo({
        duLieu: duLieuCacTram,
        bangIon: bangIonToanCuc,
        id: tramMucTieu,
        onChuyenThreeJs: hamChuyenThreeJs
      });
    }
  }
}

function taiScriptUnity(url) {
  return new Promise((resolve, reject) => {
    const tonTai = document.querySelector(`script[src="${url}"]`);
    if (tonTai) return resolve();
    const s = document.createElement("script");
    s.src = url;
    s.onload = () => resolve();
    s.onerror = (e) => reject(new Error(`Không thể nạp ${url}`));
    document.head.appendChild(s);
  });
}

export const LabUnityApp = {
  async mo({ duLieu, bangIon, id, cheDo = "huong_dan", onChuyenThreeJs } = {}) {
    if (onChuyenThreeJs) hamChuyenThreeJs = onChuyenThreeJs;
    const callbackChuyen = onChuyenThreeJs || hamChuyenThreeJs;
    tramHienTai = id || new URLSearchParams(window.location.search).get("tn") || null;
    daSanSang = false;
    dangChoChay = true;
    dsFps = [];

    // Chỉ chạy trong app desktop
    if (!window.chemistryApp || typeof window.chemistryApp.labWebGL !== "function") {
      console.warn("[LabUnityApp] Chỉ hỗ trợ bên trong ứng dụng máy tính CHEMISTRY.");
      if (typeof callbackChuyen === "function") callbackChuyen();
      return;
    }

    const khung = document.getElementById("ptn-khung");
    if (!khung) throw new Error("Không tìm thấy #ptn-khung");

    // Dọn dẹp DOM cũ trong khung
    khung.innerHTML = "";
    khung.classList.add("ptn-app-che-do-unity");

    // Ẩn các thanh điều khiển ngoài của Web 3D
    const dsCotBen = ["#ptn-cot-trai", "#ptn-cot-phai", "#ptn-ngan-keo-trai", "#ptn-bang-dieu-khien"];
    dsCotBen.forEach((sel) => {
      const el = document.querySelector(sel);
      if (el) el.hidden = true;
    });

    // Tạo thanh điều khiển trên đầu khung Unity
    const thanhDieuKhien = taoPhanTu("div", { class: "ptn-app-thanh-dieu-khien" },
      taoPhanTu("button", {
        type: "button",
        id: "ptn-nut-cua-so-muot",
        class: "btn btn-sm btn-primary",
        text: "⚡ Mở cửa sổ mượt",
        on: { click: () => chuyenSangCuaSoRieng() }
      })
    );
    khung.append(thanhDieuKhien);

    // Tạo canvas
    const canvas = taoPhanTu("canvas", {
      id: "unity-canvas",
      tabIndex: -1,
      class: "ptn-app-canvas"
    });
    khung.append(canvas);

    // Khởi tạo máy trạng thái & cầu nối
    khoiTaoMoPhong(duLieu, bangIon, cheDo);
    caiDatCauNoiLoiTrang();

    // Thiết lập hook cho kiểm tra tự động
    window.PhongLab = window.PhongLab || {};
    window.PhongLab.app = {
      cheDo: () => "unity",
      unitySanSang: () => daSanSang,
      tram: () => tramHienTai,
      fpsTrungBinh: () => tinhFpsTrungBinh(),
      thaoTac: (tramId, lenh, json) => {
        const mp = cacMoPhong[tramId];
        if (!mp) return { ok: false, loi: "Không tìm thấy trạm " + tramId };
        const thamSo = typeof json === "string" ? JSON.parse(json) : (json || {});
        const res = mp.thaoTac(lenh, thamSo);
        if (unityInstance && daSanSang) {
          try {
            unityInstance.SendMessage("CauNoiTrang", "NhanTrangThai", JSON.stringify(mp.trangThai()));
          } catch {}
        }
        return res;
      },
      trangThai: (tramId) => cacMoPhong[tramId]?.trangThai() || {}
    };

    // Bắt đầu vòng lặp mô phỏng
    mocThoiGianTruoc = 0;
    if (idRaf) cancelAnimationFrame(idRaf);
    idRaf = requestAnimationFrame(chayVongLapMoPhong);

    // Hiển thị màn hình tải
    const manTai = document.getElementById("ptn-dang-tai");
    const chuTai = document.getElementById("ptn-dang-tai-chu");
    const thanhTai = manTai?.querySelector(".ptn-thanh-tai");
    if (manTai) manTai.classList.remove("an");
    if (chuTai) chuTai.textContent = "Đang tải bản Unity 3D…";
    if (thanhTai) thanhTai.hidden = false;

    // Nạp loader và khởi tạo Unity
    try {
      await taiScriptUnity("chemlab://lab/lab.loader.js");
      if (typeof createUnityInstance !== "function") {
        throw new Error("createUnityInstance không khả dụng sau khi nạp loader.");
      }

      const cauHinhUnity = {
        dataUrl: "chemlab://lab/lab.data",
        frameworkUrl: "chemlab://lab/lab.framework.js",
        codeUrl: "chemlab://lab/lab.wasm",
        productName: "PhongLabHoa3D",
        devicePixelRatio: Math.min(window.devicePixelRatio || 1, 1.5)
      };

      unityInstance = await createUnityInstance(canvas, cauHinhUnity, (tienTrinh) => {
        if (thanhTai?.firstElementChild) {
          thanhTai.firstElementChild.style.width = `${Math.round(tienTrinh * 100)}%`;
        }
      });

      if (!dangChoChay) {
        if (unityInstance) {
          try { await unityInstance.Quit(); } catch {}
          unityInstance = null;
        }
        return;
      }
    } catch (e) {
      console.error("[LabUnityApp] Lỗi khi nạp bản Unity:", e);
      hienThongBao("Không tải được bản Unity 3D – chuyển sang bản Web nhẹ.", "canh-bao");
      await LabUnityApp.dong();
      if (typeof callbackChuyen === "function") callbackChuyen();
    }
  },

  async dong() {
    daSanSang = false;
    dangChoChay = false;
    if (window.PhongLab && window.PhongLab.app) {
      window.PhongLab.app.cheDo = () => "nhe";
      window.PhongLab.app.unitySanSang = () => false;
    }
    if (idRaf) {
      cancelAnimationFrame(idRaf);
      idRaf = null;
    }

    if (unityInstance) {
      try {
        await unityInstance.Quit();
      } catch (e) {
        console.warn("[LabUnityApp] Lỗi khi Quit Unity:", e);
      }
      unityInstance = null;
    }

    const khung = document.getElementById("ptn-khung");
    if (khung) {
      khung.classList.remove("ptn-app-che-do-unity");
      khung.innerHTML = "";
    }

    // Phục hồi lại các cột bên nếu cần
    const dsCotBen = ["#ptn-cot-trai", "#ptn-cot-phai", "#ptn-ngan-keo-trai", "#ptn-bang-dieu-khien"];
    dsCotBen.forEach((sel) => {
      const el = document.querySelector(sel);
      if (el) el.hidden = false;
    });

    const manTai = document.getElementById("ptn-dang-tai");
    if (manTai) manTai.classList.add("an");
  }
};

// Dọn dẹp khi đổi trang hoặc đóng cửa sổ
window.addEventListener("pagehide", () => {
  if (unityInstance) {
    try {
      unityInstance.Quit();
    } catch {}
  }
});

window.addEventListener("beforeunload", () => {
  if (unityInstance) {
    try {
      unityInstance.Quit();
    } catch {}
  }
});

// Xuất mặc định và UMD
export default LabUnityApp;
if (typeof window !== "undefined") {
  window.LabUnityApp = LabUnityApp;
}
