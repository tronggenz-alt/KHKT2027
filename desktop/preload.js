/**
 * desktop/preload.js
 * Cầu nối an toàn giữa tiến trình giao diện (renderer) và tiến trình chính (main).
 * Tuân thủ nghiêm ngặt bảo mật: contextIsolation = true, sandbox = true, nodeIntegration = false.
 */
const { contextBridge, ipcRenderer } = require("electron");

contextBridge.exposeInMainWorld("chemistryApp", {
  // 1. Thông tin phiên bản & môi trường
  phienBan: () => "1.1.0",
  cheDo: () => ipcRenderer.invoke("app:lay-che-do"),
  laAppDesktop: true,

  // 2. Mở ứng dụng Unity Phòng Lab Hoá 3D đóng gói sẵn
  moAppLab: (thiNghiemId = "") => {
    return ipcRenderer.invoke("lab:mo-app", thiNghiemId);
  },

  // 2b. Thông tin bản build WebGL nhúng sẵn trong app
  labWebGL: () => {
    return ipcRenderer.invoke("lab:webgl");
  },

  // 3. Quản lý khoá AI an toàn (thông qua Keychain safeStorage ở tiến trình chính)
  luuKhoaAI: (loai, khoa) => {
    return ipcRenderer.invoke("keychain:luu-khoa", { loai, khoa });
  },
  layKhoaAI: (loai) => {
    return ipcRenderer.invoke("keychain:lay-khoa", { loai });
  },

  // 4. Tiện ích hệ thống
  thongBao: (tieuDe, noiDung) => {
    ipcRenderer.send("app:thong-bao", { tieuDe, noiDung });
  },
  nhapDuLieuTuDuAn: () => {
    return ipcRenderer.invoke("app:nhap-du-lieu");
  },
  chuyenCheDo: (cheDoMoi) => {
    return ipcRenderer.invoke("app:chuyen-che-do", cheDoMoi);
  }
});
