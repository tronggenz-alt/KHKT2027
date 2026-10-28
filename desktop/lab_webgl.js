"use strict";

const fs = require("fs");
const path = require("path");

/**
 * Xác định thư mục chứa bản build WebGL của phòng lab
 * Thứ tự ưu tiên:
 * 1. process.resourcesPath/lab-webgl (khi chạy app đã đóng gói)
 * 2. ../outputs/app-lab/webgl (khi chạy ở chế độ dev từ thư mục desktop)
 */
function thuMucLabWebGL(options = {}) {
  const resourcesPath = options.resourcesPath || (typeof process !== "undefined" ? process.resourcesPath : null);
  const dirname = options.dirname || __dirname;

  if (resourcesPath) {
    const duongDanResources = path.join(resourcesPath, "lab-webgl");
    if (fs.existsSync(duongDanResources)) {
      return duongDanResources;
    }
  }

  // Chế độ phát triển: tìm trong outputs/app-lab/webgl
  const thuMucDev1 = path.resolve(dirname, "../outputs/app-lab/webgl");
  if (fs.existsSync(thuMucDev1)) {
    return thuMucDev1;
  }

  const thuMucDev2 = path.resolve(dirname, "../../outputs/app-lab/webgl");
  if (fs.existsSync(thuMucDev2)) {
    return thuMucDev2;
  }

  return thuMucDev1;
}

/**
 * Giải đường dẫn an toàn, chống path traversal (.. hoặc absolute path)
 * Trả về đường dẫn tuyệt đối tới file nếu hợp lệ và tồn tại, ngược lại trả về null.
 */
function giaiDuongDan(goc, urlPath) {
  if (!goc || typeof urlPath !== "string") return null;

  try {
    // Bỏ query string hoặc hash nếu có
    const urlSach = urlPath.split("?")[0].split("#")[0];
    const duongDanGiaiMa = decodeURIComponent(urlSach);

    // Chuẩn hoá đường dẫn tương đối, bỏ slash ở đầu
    const duongDanTuongDoi = duongDanGiaiMa.replace(/^[/\\]+/, "");
    const thuMucGocChuan = path.resolve(goc);
    const duongDanDich = path.resolve(thuMucGocChuan, duongDanTuongDoi);

    // Chặn path traversal: đường dẫn đích phải nằm trong thư mục gốc
    if (!duongDanDich.startsWith(thuMucGocChuan)) {
      return null;
    }

    // 1. Kiểm tra file tại vị trí trực tiếp
    if (fs.existsSync(duongDanDich) && fs.statSync(duongDanDich).isFile()) {
      return duongDanDich;
    }

    // 2. Dự phòng: Unity WebGL thường đặt các tệp trong thư mục con Build/
    const duongDanBuild = path.resolve(thuMucGocChuan, "Build", duongDanTuongDoi);
    if (duongDanBuild.startsWith(thuMucGocChuan) && fs.existsSync(duongDanBuild) && fs.statSync(duongDanBuild).isFile()) {
      return duongDanBuild;
    }

    return null;
  } catch (err) {
    return null;
  }
}

/**
 * Trả về Content-Type MIME tương ứng với phần mở rộng của tệp
 */
function kieuMime(tenTep) {
  if (!tenTep || typeof tenTep !== "string") return "application/octet-stream";

  const tenThuong = tenTep.toLowerCase();
  if (tenThuong.endsWith(".wasm")) return "application/wasm";
  if (tenThuong.endsWith(".js")) return "text/javascript";
  if (tenThuong.endsWith(".data")) return "application/octet-stream";
  if (tenThuong.endsWith(".json")) return "application/json";
  if (tenThuong.endsWith(".html") || tenThuong.endsWith(".htm")) return "text/html";
  if (tenThuong.endsWith(".css")) return "text/css";
  if (tenThuong.endsWith(".png")) return "image/png";
  if (tenThuong.endsWith(".jpg") || tenThuong.endsWith(".jpeg")) return "image/jpeg";
  if (tenThuong.endsWith(".svg")) return "image/svg+xml";
  if (tenThuong.endsWith(".ico")) return "image/x-icon";

  return "application/octet-stream";
}

/**
 * Kiểm tra tính đầy đủ của bản build WebGL (phải có đủ 4 file: loader, data, framework, wasm)
 */
function thongTinBuild(goc) {
  const ketQuaMacDinh = {
    coSan: false,
    tep: { loader: null, data: null, framework: null, wasm: null },
    kichThuocMB: 0
  };

  if (!goc || !fs.existsSync(goc)) {
    return ketQuaMacDinh;
  }

  const cacTepMucTieu = {
    loader: ["lab.loader.js", "Build/lab.loader.js"],
    data: ["lab.data", "Build/lab.data"],
    framework: ["lab.framework.js", "Build/lab.framework.js"],
    wasm: ["lab.wasm", "Build/lab.wasm"]
  };

  const tepPhatHien = {};
  let tongBytes = 0;

  for (const [khoa, danhSachTen] of Object.entries(cacTepMucTieu)) {
    let timThay = null;
    for (const ten of danhSachTen) {
      const duongDan = path.join(goc, ten);
      if (fs.existsSync(duongDan)) {
        try {
          const st = fs.statSync(duongDan);
          if (st.isFile()) {
            timThay = "/" + ten.replace(/\\/g, "/");
            tongBytes += st.size;
            break;
          }
        } catch {}
      }
    }
    tepPhatHien[khoa] = timThay;
  }

  // Tùy chọn tìm file bất kỳ khớp đuôi nếu tên không phải "lab.*"
  if (!tepPhatHien.loader || !tepPhatHien.data || !tepPhatHien.framework || !tepPhatHien.wasm) {
    function quetThuMuc(dir, prefix = "") {
      try {
        const danhSach = fs.readdirSync(dir);
        for (const item of danhSach) {
          const full = path.join(dir, item);
          const st = fs.statSync(full);
          if (st.isDirectory() && (item === "Build" || item === "build")) {
            quetThuMuc(full, prefix ? `${prefix}/${item}` : item);
          } else if (st.isFile()) {
            const rel = prefix ? `/${prefix}/${item}` : `/${item}`;
            if (!tepPhatHien.loader && item.endsWith(".loader.js")) { tepPhatHien.loader = rel; tongBytes += st.size; }
            else if (!tepPhatHien.framework && item.endsWith(".framework.js")) { tepPhatHien.framework = rel; tongBytes += st.size; }
            else if (!tepPhatHien.wasm && item.endsWith(".wasm")) { tepPhatHien.wasm = rel; tongBytes += st.size; }
            else if (!tepPhatHien.data && item.endsWith(".data")) { tepPhatHien.data = rel; tongBytes += st.size; }
          }
        }
      } catch {}
    }
    quetThuMuc(goc);
  }

  const coSan = Boolean(tepPhatHien.loader && tepPhatHien.data && tepPhatHien.framework && tepPhatHien.wasm);
  const kichThuocMB = Math.round((tongBytes / (1024 * 1024)) * 10) / 10;

  return {
    coSan,
    tep: tepPhatHien,
    kichThuocMB
  };
}

module.exports = {
  thuMucLabWebGL,
  giaiDuongDan,
  kieuMime,
  thongTinBuild
};
