/**
 * desktop/main.js
 * Tiến trình chính (Main Process) của ứng dụng máy tính CHEMISTRY.
 * Quản lý vòng đời ứng dụng, khởi động & dọn dẹp máy chủ backend Python,
 * hệ thống menu native, chuyển đổi chế độ Trên máy / Trực tuyến, và gọi app Unity.
 */
const { app, BrowserWindow, Menu, shell, dialog, ipcMain, safeStorage, Notification, protocol, systemPreferences } = require("electron");
const path = require("path");
const fs = require("fs");
const { spawn } = require("child_process");
const http = require("http");
const capNhat = require("./cap_nhat");
const labWebGL = require("./lab_webgl");
const { choPhepCamera } = require("./quyen_camera");

// Đăng ký scheme chemlab trước khi app ready (cho phép nạp tài nguyên Unity WebGL)
protocol.registerSchemesAsPrivileged([
  {
    scheme: "chemlab",
    privileges: {
      standard: true,
      secure: true,
      supportFetchAPI: true,
      corsEnabled: true,
      stream: true
    }
  }
]);

// 1. Đảm bảo Single Instance Lock (chỉ mở một bản duy nhất)
const gotTheLock = app.requestSingleInstanceLock();
if (!gotTheLock) {
  app.quit();
  process.exit(0);
}

let mainWindow = null;
let splashWindow = null;
let serverProcess = null;
let serverPort = null;
let currentMode = "cuc_bo"; // "cuc_bo" (Trên máy) hoặc "truc_tuyen" (Trực tuyến)
const REMOTE_WEB_URL = "https://giai-chuoi-huu-co.onrender.com"; // Địa chỉ web trực tuyến mặc định
const URL_PHIEN_BAN = `${REMOTE_WEB_URL}/api/app/phien-ban`;       // bản phát hành mới nhất (backend/cap_nhat_app.py)
const CHU_KY_KIEM_TRA_CAP_NHAT_MS = 6 * 60 * 60 * 1000;            // tự kiểm tra tối đa 6 giờ / lần

// Thư mục dữ liệu người dùng
const USER_DATA_DIR = path.join(app.getPath("appData"), "CHEMISTRY");
const CONFIG_FILE = path.join(USER_DATA_DIR, "app_config.json");
const KEYCHAIN_FILE = path.join(USER_DATA_DIR, "secure_keys.json");

// Đọc cấu hình đã lưu
function docCauHinh() {
  try {
    if (fs.existsSync(CONFIG_FILE)) {
      return JSON.parse(fs.readFileSync(CONFIG_FILE, "utf8"));
    }
  } catch (e) {
    console.error("Lỗi đọc file cấu hình:", e);
  }
  return { mode: "cuc_bo", windowBounds: { width: 1440, height: 900 } };
}

function ghiCauHinh(data) {
  try {
    fs.mkdirSync(USER_DATA_DIR, { recursive: true });
    const current = docCauHinh();
    const updated = { ...current, ...data };
    fs.writeFileSync(CONFIG_FILE, JSON.stringify(updated, null, 2), "utf8");
  } catch (e) {
    console.error("Lỗi ghi file cấu hình:", e);
  }
}

// 2. Quản lý khoá AI an toàn qua Keychain (safeStorage)
function luuKhoaAnToan(loai, khoa) {
  try {
    fs.mkdirSync(USER_DATA_DIR, { recursive: true });
    let existing = {};
    if (fs.existsSync(KEYCHAIN_FILE)) {
      existing = JSON.parse(fs.readFileSync(KEYCHAIN_FILE, "utf8"));
    }
    if (safeStorage.isEncryptionAvailable() && khoa) {
      const encrypted = safeStorage.encryptString(khoa).toString("base64");
      existing[loai] = encrypted;
    } else {
      existing[loai] = khoa;
    }
    fs.writeFileSync(KEYCHAIN_FILE, JSON.stringify(existing, null, 2), "utf8");
    return true;
  } catch (e) {
    console.error("Lỗi lưu khoá an toàn:", e);
    return false;
  }
}

function layKhoaAnToan(loai) {
  try {
    if (!fs.existsSync(KEYCHAIN_FILE)) return "";
    const existing = JSON.parse(fs.readFileSync(KEYCHAIN_FILE, "utf8"));
    const val = existing[loai];
    if (!val) return "";
    if (safeStorage.isEncryptionAvailable()) {
      const buffer = Buffer.from(val, "base64");
      return safeStorage.decryptString(buffer);
    }
    return val;
  } catch (e) {
    console.error("Lỗi đọc khoá an toàn:", e);
    return "";
  }
}

// 3. Khởi động máy chủ Python backend
function khoiDongMayChuPython() {
  return new Promise((resolve, reject) => {
    fs.mkdirSync(USER_DATA_DIR, { recursive: true });

    let pythonExe = "";
    let args = [];
    const isPackaged = app.isPackaged;

    if (isPackaged) {
      // Bản đóng gói PyInstaller trong Resources
      const bundledExe = path.join(
        process.resourcesPath,
        "may_chu",
        "may_chu_chemistry",
        process.platform === "win32" ? "may_chu_chemistry.exe" : "may_chu_chemistry"
      );
      if (fs.existsSync(bundledExe)) {
        pythonExe = bundledExe;
        args = ["--storage-dir", USER_DATA_DIR];
      }
    }

    if (!pythonExe) {
      // Môi trường phát triển cục bộ
      const rootDir = path.join(__dirname, "..");
      const venvPython = path.join(rootDir, ".venv", "bin", "python3");
      pythonExe = fs.existsSync(venvPython) ? venvPython : "python3";
      args = [path.join(__dirname, "may_chu", "khoi_dong.py"), "--storage-dir", USER_DATA_DIR];
    }

    // Truyền khoá AI đã lưu (nếu có) vào biến môi trường của tiến trình máy chủ
    const env = {
      ...process.env,
      CHE_DO_APP: "1",
      CHE_DO_CUC_BO: "1",
      COOKIE_SECURE: "0",
      STORAGE_DIR: USER_DATA_DIR,
    };

    const geminiKey = layKhoaAnToan("gemini");
    const claudeKey = layKhoaAnToan("claude");
    const openaiKey = layKhoaAnToan("openai");
    if (geminiKey) env.GEMINI_API_KEY = geminiKey;
    if (claudeKey) env.ANTHROPIC_API_KEY = claudeKey;
    if (openaiKey) env.OPENAI_API_KEY = openaiKey;

    const logsDir = path.join(USER_DATA_DIR, "logs");
    fs.mkdirSync(logsDir, { recursive: true });
    const logStream = fs.createWriteStream(path.join(logsDir, "may_chu.log"), { flags: "a" });
    logStream.write(`\n--- Khởi động máy chủ: ${new Date().toISOString()} ---\n`);

    console.log(`[Main] Đang khởi chạy máy chủ: ${pythonExe} ${args.join(" ")}`);
    serverProcess = spawn(pythonExe, args, { env });

    serverProcess.stdout.on("data", (data) => {
      logStream.write(data);
      const text = data.toString();
      console.log(`[Python stdout] ${text.trim()}`);
      const match = text.match(/CONG=(\d+)/);
      if (match && !serverPort) {
        serverPort = parseInt(match[1], 10);
        console.log(`[Main] Máy chủ đã sẵn sàng tại cổng: ${serverPort}`);
        doiMayChuSanSang(serverPort)
          .then(() => resolve(serverPort))
          .catch(reject);
      }
    });

    serverProcess.stderr.on("data", (data) => {
      logStream.write(data);
      console.error(`[Python stderr] ${data.toString().trim()}`);
    });

    serverProcess.on("close", (code) => {
      console.log(`[Main] Tiến trình máy chủ đã dừng với mã: ${code}`);
      serverProcess = null;
    });

    serverProcess.on("error", (err) => {
      console.error("[Main] Lỗi khởi chạy máy chủ:", err);
      reject(err);
    });

    // Timeout 30s phòng trường hợp máy chủ bị treo
    setTimeout(() => {
      if (!serverPort) {
        reject(new Error("Quá thời gian chờ máy chủ khởi động (30s)"));
      }
    }, 30000);
  });
}

// Kiểm tra sức khoẻ máy chủ qua /api/suc-khoe
function doiMayChuSanSang(port, maxAttempts = 30) {
  return new Promise((resolve, reject) => {
    let attempts = 0;
    const check = () => {
      attempts++;
      const req = http.get(`http://127.0.0.1:${port}/api/suc-khoe`, (res) => {
        if (res.statusCode === 200) {
          resolve();
        } else if (attempts < maxAttempts) {
          setTimeout(check, 500);
        } else {
          reject(new Error("Máy chủ không phản hồi mã 200"));
        }
      });
      req.on("error", () => {
        if (attempts < maxAttempts) {
          setTimeout(check, 500);
        } else {
          reject(new Error("Không thể kết nối tới máy chủ"));
        }
      });
      req.end();
    };
    check();
  });
}

// 4. Màn hình chờ (Splash Screen)
function taoSplashWindow() {
  splashWindow = new BrowserWindow({
    width: 420,
    height: 320,
    transparent: false,
    frame: false,
    alwaysOnTop: true,
    resizable: false,
    center: true,
    backgroundColor: "#0d1b2a",
    webPreferences: {
      contextIsolation: true,
      nodeIntegration: false,
    },
  });

  const splashHtml = `
    <!DOCTYPE html>
    <html>
    <head>
      <meta charset="utf-8">
      <style>
        body {
          margin: 0; padding: 0; display: flex; flex-direction: column;
          align-items: center; justify-content: center; height: 100vh;
          background: linear-gradient(135deg, #0b192c 0%, #1e3e62 100%);
          color: #eef4fa; font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
          user-select: none;
        }
        .logo { font-size: 52px; margin-bottom: 12px; }
        .title { font-size: 24px; font-weight: 700; letter-spacing: 2px; color: #38bdf8; margin-bottom: 6px; }
        .sub { font-size: 13px; color: #94a3b8; margin-bottom: 24px; }
        .spinner {
          width: 32px; height: 32px; border: 3px solid rgba(56, 189, 248, 0.2);
          border-top-color: #38bdf8; border-radius: 50%;
          animation: spin 0.8s linear infinite;
        }
        @keyframes spin { to { transform: rotate(360deg); } }
      </style>
    </head>
    <body>
      <div class="logo">🧪</div>
      <div class="title">CHEMISTRY</div>
      <div class="sub">Đang khởi động hệ thống thí nghiệm 3D...</div>
      <div class="spinner"></div>
    </body>
    </html>
  `;
  splashWindow.loadURL(`data:text/html;charset=utf-8,${encodeURIComponent(splashHtml)}`);
}

// 5. Cửa sổ ứng dụng chính
function taoCuaSoChinh() {
  const config = docCauHinh();
  const bounds = config.windowBounds || { width: 1440, height: 900 };

  mainWindow = new BrowserWindow({
    width: bounds.width,
    height: bounds.height,
    x: bounds.x,
    y: bounds.y,
    minWidth: 1024,
    minHeight: 700,
    show: false,
    backgroundColor: "#0b192c",
    title: "CHEMISTRY - Giải Chuỗi & Phòng Lab 3D",
    webPreferences: {
      preload: path.join(__dirname, "preload.js"),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true,
    },
  });

  // Ghi nhớ vị trí và kích thước cửa sổ
  mainWindow.on("resize", () => {
    ghiCauHinh({ windowBounds: mainWindow.getBounds() });
  });
  mainWindow.on("move", () => {
    ghiCauHinh({ windowBounds: mainWindow.getBounds() });
  });

  // Chặn điều hướng ngoài ứng dụng (mở bằng trình duyệt ngoài)
  mainWindow.webContents.setWindowOpenHandler(({ url }) => {
    if (url.startsWith("http://127.0.0.1") || url.startsWith(REMOTE_WEB_URL)) {
      return { action: "allow" };
    }
    shell.openExternal(url);
    return { action: "deny" };
  });

  mainWindow.webContents.on("will-navigate", (event, url) => {
    if (!url.startsWith("http://127.0.0.1") && !url.startsWith(REMOTE_WEB_URL)) {
      event.preventDefault();
      shell.openExternal(url);
    }
  });

  // Hộp thoại lưu tệp của hệ điều hành khi tải về (ZIP kết quả, PNG, CDXML, .mol)
  mainWindow.webContents.session.on("will-download", (event, item, webContents) => {
    item.setSaveDialogOptions({
      title: `Lưu ${item.getFilename()}`,
      defaultPath: path.join(app.getPath("downloads"), item.getFilename()),
    });
  });

  // Quản lý cấp quyền (chỉ cấp camera cho trang Sinh học và static demo)
  mainWindow.webContents.session.setPermissionRequestHandler(async (webContents, permission, callback, details) => {
    if (permission === "media") {
      const urlYeuCau = (details && details.requestingUrl) || webContents.getURL() || "";
      const gocMayChu = currentMode === "truc_tuyen" ? REMOTE_WEB_URL : `http://127.0.0.1:${serverPort}`;
      const kieuMedia = (details && details.mediaTypes) || [];
      const duocPhep = choPhepCamera({ urlYeuCau, gocMayChu, loaiQuyen: permission, kieuMedia });
      if (!duocPhep) {
        return callback(false);
      }
      // Trên macOS, xin quyền camera cấp hệ thống nếu cần
      if (process.platform === "darwin" && systemPreferences && systemPreferences.askForMediaAccess) {
        try {
          const granted = await systemPreferences.askForMediaAccess("camera");
          return callback(granted);
        } catch (e) {
          console.error("[Main] Lỗi askForMediaAccess camera:", e);
          return callback(false);
        }
      }
      return callback(true);
    }
    // Mọi loại quyền khác giữ nguyên hành vi cho phép hiện tại
    callback(true);
  });

  // Tải trang theo chế độ
  taiTrangTheoCheDo();

  // Đóng cửa sổ
  mainWindow.on("closed", () => {
    mainWindow = null;
  });
}

function taiTrangTheoCheDo() {
  if (!mainWindow) return;
  const config = docCauHinh();
  currentMode = config.mode || "cuc_bo";

  if (currentMode === "truc_tuyen") {
    console.log(`[Main] Đang tải chế độ Trực tuyến: ${REMOTE_WEB_URL}`);
    mainWindow.loadURL(REMOTE_WEB_URL);
  } else {
    const localUrl = `http://127.0.0.1:${serverPort}/`;
    console.log(`[Main] Đang tải chế độ Trên máy: ${localUrl}`);
    mainWindow.loadURL(localUrl);
  }

  mainWindow.once("ready-to-show", () => {
    if (splashWindow && !splashWindow.isDestroyed()) {
      splashWindow.close();
      splashWindow = null;
    }
    mainWindow.show();
    mainWindow.focus();
  });
}

// 6. Điều hướng nhanh trong app
function dieuHuong(duongDan) {
  if (!mainWindow) return;
  if (currentMode === "cuc_bo") {
    mainWindow.loadURL(`http://127.0.0.1:${serverPort}${duongDan}`);
  } else {
    mainWindow.loadURL(`${REMOTE_WEB_URL}${duongDan}`);
  }
}

// 7. Mở app Unity "Phòng Lab Hoá 3D" đi kèm
function moAppLabUnity(thiNghiemId = "") {
  return new Promise((resolve, reject) => {
    let appPath = "";
    if (process.platform === "darwin") {
      const danhSachMac = [
        path.join(process.resourcesPath, "PhongLabHoa3D.app"),
        path.join(__dirname, "..", "outputs", "app-lab", "PhongLabHoa3D.app"),
        path.join(process.env.HOME || "", "UnityProjects", "PhongLabHoa3D", "Builds", "macOS", "PhongLabHoa3D.app"),
      ];

      for (const p of danhSachMac) {
        if (fs.existsSync(p)) {
          appPath = p;
          break;
        }
      }

      if (!appPath) {
        const err = "Không tìm thấy file ứng dụng Phòng Lab Hoá 3D (PhongLabHoa3D.app).";
        dialog.showErrorBox("Thiếu tài nguyên", err);
        return reject(new Error(err));
      }

      console.log(`[Main] Mở ứng dụng Unity Lab (macOS): ${appPath} (tn=${thiNghiemId})`);
      const args = ["-a", appPath];
      if (thiNghiemId) {
        args.push("--args", "-tn", thiNghiemId);
      }
      const proc = spawn("open", args, { detached: true });
      proc.on("error", (e) => {
        console.error("[Main] Lỗi mở app Lab Unity:", e);
        dialog.showErrorBox("Lỗi mở ứng dụng", `Không thể mở Phòng Lab Hoá 3D: ${e.message}`);
        reject(e);
      });
      proc.on("close", (code) => {
        if (code === 0) resolve(true);
        else {
          console.warn(`[Main] open thoát với mã: ${code}`);
          resolve(false);
        }
      });
    } else {
      const danhSachWin = [
        path.join(process.resourcesPath, "PhongLabHoa3D", "PhongLabHoa3D.exe"),
        path.join(process.resourcesPath, "PhongLabHoa3D.exe"),
        path.join(__dirname, "..", "outputs", "app-lab", "PhongLabHoa3D.exe"),
      ];

      for (const p of danhSachWin) {
        if (fs.existsSync(p)) {
          appPath = p;
          break;
        }
      }

      if (!appPath) {
        const err = "Không tìm thấy file ứng dụng Phòng Lab Hoá 3D (PhongLabHoa3D.exe).";
        dialog.showErrorBox("Thiếu tài nguyên", err);
        return reject(new Error(err));
      }

      console.log(`[Main] Mở ứng dụng Unity Lab (Windows): ${appPath} (tn=${thiNghiemId})`);
      const args = thiNghiemId ? ["-tn", thiNghiemId] : [];
      const proc = spawn(appPath, args, { detached: true, stdio: "ignore" });
      proc.unref();
      resolve(true);
    }
  });
}

// 8. Thiết lập Menu hệ thống
// 6b. Cập nhật app: hỏi máy chủ bản mới → tải → kiểm tra SHA-256 → mở bộ cài (cap_nhat.js). Không tự ghi đè app (app chưa ký Apple Developer).
let dangCapNhat = false;
async function kiemTraCapNhat({ imLang = false } = {}) {
  if (dangCapNhat) return;
  dangCapNhat = true;
  const cuaSo = mainWindow && !mainWindow.isDestroyed() ? mainWindow : null;
  const hop = (tuyChon) => (cuaSo ? dialog.showMessageBox(cuaSo, tuyChon) : dialog.showMessageBox(tuyChon));
  try {
    const m = await capNhat.layManifest(URL_PHIEN_BAN);
    ghiCauHinh({ cap_nhat_lan_cuoi: Date.now() });
    const ban = capNhat.banMoi(m, app.getVersion());
    if (!ban) {
      if (!imLang) await hop({ type: "info", message: "Bạn đang dùng bản mới nhất", detail: `CHEMISTRY ${app.getVersion()}`, buttons: ["Đóng"] });
      return;
    }
    if (imLang && !ban.bat_buoc && docCauHinh().bo_qua_phien_ban === ban.phien_ban) return;
    const mb = (ban.tai.kich_thuoc / 1048576).toFixed(0);
    const ghiChu = ban.ghi_chu.length ? "\n\nCó gì mới:\n• " + ban.ghi_chu.join("\n• ") : "";
    const nut = ban.bat_buoc ? ["Tải về và cài", "Thoát ứng dụng"] : ["Tải về và cài", "Để sau", "Bỏ qua bản này"];
    const { response } = await hop({
      type: "info", buttons: nut, defaultId: 0, cancelId: ban.bat_buoc ? 1 : 1,
      message: `Có bản CHEMISTRY mới: ${ban.phien_ban}`,
      detail: `Bạn đang dùng ${app.getVersion()}. Dung lượng khoảng ${mb} MB.${ghiChu}` +
        (ban.bat_buoc ? "\n\nBản này BẮT BUỘC cập nhật để tiếp tục dùng." : ""),
    });
    if (response === 1 && ban.bat_buoc) { app.quit(); return; }
    if (response === 2) { ghiCauHinh({ bo_qua_phien_ban: ban.phien_ban }); return; }
    if (response !== 0) return;

    const dich = path.join(app.getPath("downloads"), path.basename(new URL(ban.tai.url).pathname) || `CHEMISTRY-${ban.phien_ban}`);
    let moc = -1;
    if (cuaSo) cuaSo.setProgressBar(0);
    try {
      await capNhat.taiBoCai(ban.tai, dich, {
        onTienDo: (t) => {
          if (cuaSo && !cuaSo.isDestroyed()) cuaSo.setProgressBar(Math.min(t, 1));
          const phanTram = Math.floor(t * 10) * 10;
          if (phanTram !== moc) { moc = phanTram; console.log(`[CapNhat] Đã tải ${phanTram}%`); }
        },
      });
    } finally {
      if (cuaSo && !cuaSo.isDestroyed()) cuaSo.setProgressBar(-1);
    }
    const huongDan = process.platform === "darwin"
      ? "Mở file .dmg rồi kéo CHEMISTRY vào thư mục Applications (chọn Thay thế). Thoát CHEMISTRY trước khi thay."
      : "Chạy bộ cài; CHEMISTRY sẽ được cập nhật tại chỗ.";
    const kq = await hop({
      type: "info", buttons: ["Mở bộ cài", "Hiện trong thư mục", "Để sau"], defaultId: 0, cancelId: 2,
      message: "Đã tải xong và kiểm tra hợp lệ (SHA-256)",
      detail: `${dich}\n\n${huongDan}`,
    });
    if (kq.response === 0) shell.openPath(dich);
    else if (kq.response === 1) shell.showItemInFolder(dich);
  } catch (err) {
    console.error("[CapNhat] Lỗi:", err.message);
    if (!imLang) await hop({ type: "warning", message: "Không kiểm tra / tải được bản cập nhật", detail: `${err.message}\n\nKiểm tra kết nối mạng rồi thử lại (Trợ giúp ▸ Kiểm tra cập nhật).`, buttons: ["Đóng"] });
  } finally {
    dangCapNhat = false;
  }
}

function henGioKiemTraCapNhat() {
  if (!app.isPackaged) return;   // chạy dev (npm start) không làm phiền
  setTimeout(() => {
    if (Date.now() - (docCauHinh().cap_nhat_lan_cuoi || 0) >= CHU_KY_KIEM_TRA_CAP_NHAT_MS) kiemTraCapNhat({ imLang: true });
  }, 12000);
}

function taoMenuHeThong() {
  const isMac = process.platform === "darwin";
  const template = [
    ...(isMac
      ? [
          {
            label: "CHEMISTRY",
            submenu: [
              {
                label: "Giới thiệu CHEMISTRY",
                click: () => hienHopThoaiGioiThieu(),
              },
              { type: "separator" },
              {
                label: "Cài đặt ứng dụng...",
                accelerator: "CmdOrCtrl+,",
                click: () => dieuHuong("/cai-dat"),
              },
              {
                label: "Kiểm tra cập nhật...",
                click: () => kiemTraCapNhat(),
              },
              { type: "separator" },
              { role: "services" },
              { type: "separator" },
              { role: "hide" },
              { role: "hideOthers" },
              { role: "unhide" },
              { type: "separator" },
              { role: "quit", label: "Thoát CHEMISTRY" },
            ],
          },
        ]
      : []),
    {
      label: "Tệp",
      submenu: [
        {
          label: "Nhập dữ liệu từ dự án...",
          click: () => nhapDuLieuTuDuAn(),
        },
        {
          label: "Mở thư mục dữ liệu ứng dụng",
          click: () => shell.openPath(USER_DATA_DIR),
        },
        { type: "separator" },
        isMac ? { role: "close" } : { role: "quit" },
      ],
    },
    {
      label: "Đi tới",
      submenu: [
        { label: "Trang chủ (Giải chuỗi)", click: () => dieuHuong("/") },
        { label: "Tìm cấu tạo chất", click: () => dieuHuong("/?muc=tim-chat") },
        { label: "Mô hình 3D phân tử", click: () => dieuHuong("/?muc=3d") },
        { label: "Cơ chế phản ứng", click: () => dieuHuong("/?muc=co-che") },
        { type: "separator" },
        { label: "Phòng thí nghiệm Hoá ảo (Web 3D)", click: () => dieuHuong("/hoa-hoc/phong-thi-nghiem") },
        { label: "Bảng tuần hoàn 118 nguyên tố", click: () => dieuHuong("/hoa-hoc/phong-thi-nghiem") },
        { label: "Lab Sinh học 3D", click: () => dieuHuong("/sinh-hoc") },
        { type: "separator" },
        { label: "Mô phỏng Hoá lý 2D", click: () => dieuHuong("/hoa-hoc/hoa-ly") },
        { label: "  • Động cơ nhiệt 4 kỳ (Otto/Diesel)", click: () => dieuHuong("/hoa-hoc/hoa-ly/dong-co-nhiet") },
        { label: "  • Định luật Khí lý tưởng", click: () => dieuHuong("/hoa-hoc/hoa-ly/khi-ly-tuong") },
        { label: "  • Cân bằng & Le Chatelier", click: () => dieuHuong("/hoa-hoc/hoa-ly/can-bang-le-chatelier") },
        { label: "  • Tốc độ phản ứng & Năng lượng hoạt hoá", click: () => dieuHuong("/hoa-hoc/hoa-ly/toc-do-phan-ung") },
        { label: "Thư viện Công thức Hoá lý", click: () => dieuHuong("/hoa-hoc/hoa-ly/cong-thuc") },
        { type: "separator" },
        { label: "Cài đặt & Khoá AI", click: () => dieuHuong("/cai-dat") },
        { label: "Quản trị hệ thống (Admin)", click: () => dieuHuong("/admin") },
        { label: "Sơ đồ hệ thống AI (Mindmap)", click: () => dieuHuong("/admin/mindmap") },
      ],
    },
    {
      label: "Chế độ",
      submenu: [
        {
          label: "● Trên máy (Cục bộ không cần mạng)",
          type: "radio",
          checked: currentMode === "cuc_bo",
          click: () => chuyenCheDo("cuc_bo"),
        },
        {
          label: "○ Trực tuyến (Sử dụng máy chủ web)",
          type: "radio",
          checked: currentMode === "truc_tuyen",
          click: () => chuyenCheDo("truc_tuyen"),
        },
      ],
    },
    {
      label: "Phòng Lab 3D",
      submenu: [
        {
          label: "🚀 Mở app Unity Phòng Lab Hoá 3D",
          accelerator: "CmdOrCtrl+Shift+L",
          click: () => moAppLabUnity(),
        },
      ],
    },
    {
      label: "Cửa sổ",
      submenu: [
        { role: "reload", label: "Tải lại trang" },
        { role: "forceReload", label: "Tải lại bỏ cache" },
        { role: "toggleDevTools", label: "Công cụ lập trình (DevTools)" },
        { type: "separator" },
        { role: "resetZoom", label: "Cỡ chữ gốc" },
        { role: "zoomIn", label: "Phóng to" },
        { role: "zoomOut", label: "Thu nhỏ" },
        { type: "separator" },
        { role: "togglefullscreen", label: "Toàn màn hình" },
      ],
    },
    {
      label: "Trợ giúp",
      submenu: [
        { label: `CHEMISTRY ${app.getVersion()}`, enabled: false },
        { label: "Kiểm tra cập nhật...", click: () => kiemTraCapNhat() },
        { type: "separator" },
        {
          label: "Mở thư mục nhật ký (Logs)",
          click: () => shell.openPath(path.join(USER_DATA_DIR, "logs")),
        },
        {
          label: "Tài liệu hướng dẫn sử dụng",
          click: () => shell.openExternal("https://github.com/tyrt07112/giai-chuoi-huu-co"),
        },
        { type: "separator" },
        {
          label: "Báo cáo lỗi / Góp ý",
          click: () => shell.openExternal("https://github.com/tyrt07112/giai-chuoi-huu-co/issues"),
        },
      ],
    },
  ];

  const menu = Menu.buildFromTemplate(template);
  Menu.setApplicationMenu(menu);
}

function chuyenCheDo(mode) {
  if (currentMode === mode) return;
  currentMode = mode;
  ghiCauHinh({ mode });
  taoMenuHeThong();
  taiTrangTheoCheDo();
}

function hienHopThoaiGioiThieu() {
  dialog.showMessageBox(mainWindow, {
    type: "info",
    title: "Về CHEMISTRY",
    message: `CHEMISTRY - Giải Chuỗi & Phòng Lab 3D v${app.getVersion() || "1.1.0"}`,
    detail:
      "Ứng dụng máy tính toàn diện cho học tập và nghiên cứu Hoá học & Sinh học.\n\n" +
      "Tài nguyên & Ghi công bản quyền:\n" +
      "• Mô hình giải phẫu 3D: BodyParts3D (DBCLS, CC BY 4.0)\n" +
      "• Mô hình phòng thí nghiệm: 30 tác phẩm tuyển chọn Sketchfab (CC BY 4.0 / CC0)\n" +
      "• Dữ liệu nguyên tố: PubChem & IUPAC Standards\n" +
      "• Lõi hoá học tính toán: RDKit Open-Source Cheminformatics\n" +
      "• Phông chữ: Be Vietnam Pro & JetBrains Mono (SIL Open Font License)\n" +
      "• Động cơ 3D: Three.js & Unity Engine\n\n" +
      "Phát triển bởi đội ngũ Antigravity (2026).",
    buttons: ["Đóng"],
  });
}

function nhapDuLieuTuDuAn() {
  const rootDir = path.join(__dirname, "..");
  const srcDb = path.join(rootDir, "data", "app.db");

  if (!fs.existsSync(srcDb)) {
    dialog.showMessageBox(mainWindow, {
      type: "warning",
      title: "Không tìm thấy dữ liệu",
      message: "Không tìm thấy tệp data/app.db trong thư mục dự án để sao chép.",
    });
    return;
  }

  const res = dialog.showMessageBoxSync(mainWindow, {
    type: "question",
    buttons: ["Huỷ", "Sao chép ngay"],
    defaultId: 1,
    title: "Xác nhận nhập dữ liệu",
    message: "Bạn có chắc chắn muốn sao chép cơ sở dữ liệu data/app.db từ dự án vào ứng dụng desktop không?",
    detail: "Thao tác này sẽ chỉ sao chép (không di chuyển) và làm mới dữ liệu người dùng cục bộ.",
  });

  if (res === 1) {
    const destDir = path.join(USER_DATA_DIR, "data");
    fs.mkdirSync(destDir, { recursive: true });
    const destDb = path.join(destDir, "app.db");
    fs.copyFileSync(srcDb, destDb);
    dialog.showMessageBox(mainWindow, {
      type: "info",
      title: "Thành công",
      message: "Đã nhập dữ liệu từ dự án thành công. Hãy tải lại trang để cập nhật.",
    });
  }
}

// 9. Lắng nghe IPC từ preload.js
ipcMain.handle("app:lay-che-do", () => currentMode);
ipcMain.handle("app:chuyen-che-do", (event, mode) => chuyenCheDo(mode));
ipcMain.handle("lab:mo-app", (event, tn) => moAppLabUnity(tn));
ipcMain.handle("lab:webgl", () => {
  const thuMucGoc = labWebGL.thuMucLabWebGL({ resourcesPath: process.resourcesPath, dirname: __dirname });
  const info = labWebGL.thongTinBuild(thuMucGoc);
  return {
    ...info,
    goc: "chemlab://lab/"
  };
});
ipcMain.handle("keychain:luu-khoa", (event, { loai, khoa }) => luuKhoaAnToan(loai, khoa));
ipcMain.handle("keychain:lay-khoa", (event, { loai }) => layKhoaAnToan(loai));
ipcMain.handle("app:nhap-du-lieu", () => nhapDuLieuTuDuAn());
ipcMain.on("app:thong-bao", (event, { tieuDe, noiDung }) => {
  if (Notification.isSupported()) {
    new Notification({ title: tieuDe, body: noiDung }).show();
  }
});

// Xử lý deep link URL scheme chemistry://
app.setAsDefaultProtocolClient("chemistry");
app.on("open-url", (event, url) => {
  event.preventDefault();
  console.log(`[Main] Nhận deep link: ${url}`);
  if (mainWindow) {
    // chemistry://dang-nhap?ma=...
    if (url.includes("dang-nhap?ma=")) {
      mainWindow.webContents.send("app:deep-link-dang-nhap", url);
    }
  }
});

// 10. Khởi chạy ứng dụng
app.whenReady().then(async () => {
  // Đăng ký bộ xử lý giao thức chemlab:// phục vụ WebGL trực tiếp từ app
  protocol.handle("chemlab", async (request) => {
    try {
      const url = new URL(request.url);
      const thuMucGoc = labWebGL.thuMucLabWebGL({ resourcesPath: process.resourcesPath, dirname: __dirname });
      const duongDanTep = labWebGL.giaiDuongDan(thuMucGoc, url.pathname);

      if (!duongDanTep || !fs.existsSync(duongDanTep)) {
        return new Response("Not Found", { status: 404 });
      }

      const mime = labWebGL.kieuMime(duongDanTep);
      const fileBuffer = fs.readFileSync(duongDanTep);

      return new Response(fileBuffer, {
        status: 200,
        headers: {
          "Content-Type": mime,
          "Access-Control-Allow-Origin": "*",
          "Access-Control-Allow-Methods": "GET, HEAD, OPTIONS",
          "Access-Control-Allow-Headers": "*",
          "Cross-Origin-Resource-Policy": "cross-origin"
        }
      });
    } catch (err) {
      console.error("[chemlab protocol] Lỗi tải tài nguyên:", err);
      return new Response("Internal Server Error", { status: 500 });
    }
  });

  taoSplashWindow();
  taoMenuHeThong();

  try {
    await khoiDongMayChuPython();
    taoCuaSoChinh();
    henGioKiemTraCapNhat();
  } catch (err) {
    console.error("[Main] Lỗi khởi động ứng dụng:", err);
    if (splashWindow && !splashWindow.isDestroyed()) splashWindow.close();
    dialog.showErrorBox("Lỗi khởi động", `Không thể khởi động máy chủ CHEMISTRY:\n${err.message}`);
    app.quit();
  }

  app.on("activate", () => {
    if (BrowserWindow.getAllWindows().length === 0) {
      taoCuaSoChinh();
    }
  });
});

// Dọn dẹp tiến trình khi tắt
app.on("before-quit", () => {
  if (serverProcess) {
    console.log("[Main] Đang tắt tiến trình máy chủ Python con...");
    try {
      serverProcess.kill("SIGTERM");
    } catch (e) {
      // Bỏ qua nếu đã tắt
    }
    serverProcess = null;
  }
});

app.on("window-all-closed", () => {
  if (process.platform !== "darwin") {
    app.quit();
  }
});
