// ai-rieng.js – Giao diện quản trị AI Riêng (Gia sư Hoá Hữu Cơ)
"use strict";

const $ = (s) => document.querySelector(s);
const $$ = (s) => document.querySelectorAll(s);

function esc(s) {
  return String(s ?? "").replace(/[&<>"']/g, (c) => ({
    "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;"
  }[c]));
}

function toast(noiDung, loai = "tin", thoiGian = 5000) {
  const el = document.createElement("div");
  el.className = `toast ${loai}`;
  el.innerHTML = noiDung;
  const c = $("#toasts");
  if (c) {
    c.appendChild(el);
    setTimeout(() => el.remove(), thoiGian);
  }
}

function renderChuDe(chuDe) {
  if (!chuDe || !String(chuDe).trim()) return '<span class="text-muc-2">—</span>';
  const parts = String(chuDe).split(/[,/]+/).map((s) => s.trim()).filter(Boolean);
  if (parts.length > 1) {
    return `<div class="flex flex-wrap gap-1">${parts.map((p) => `<span class="tag-chu-de">${esc(p)}</span>`).join("")}</div>`;
  }
  return `<span class="tag-chu-de">${esc(chuDe.trim())}</span>`;
}

// ------------------------------------------------------------ Khởi tạo & Chuyển tab
let thongKeKho = {};
let caiDatKho = {};
let sanTapAiRiengJob = null;
let sanTapAiThuongJob = null;

document.addEventListener("DOMContentLoaded", async () => {
  try {
    await TaiKhoan.tai();
  } catch (e) {
    console.warn("Không tải được tài khoản:", e);
  }

  // Khởi tạo nút chế độ sáng/tối
  const nutCheDo = $("#nut-che-do");
  if (nutCheDo) {
    nutCheDo.addEventListener("click", () => {
      const hienTai = document.documentElement.dataset.theme;
      const tiep = hienTai === "toi" ? "sang" : "toi";
      document.documentElement.dataset.theme = tiep;
      try { localStorage.setItem("giai-chuoi-giao-dien", tiep); } catch (_) {}
    });
  }

  // Khởi tạo tab
  $$(".tab-btn").forEach((btn) => {
    btn.addEventListener("click", () => {
      const tab = btn.dataset.tab;
      chonTab(tab);
    });
  });

  // Tải dữ liệu ban đầu
  await taiTongQuan();
  await taiCaiDat();

  // Bắt các sự kiện form
  khoiTaoCaiDat();
  khoiTaoTaiLieu();
  khoiTaoKienThuc();
  khoiTaoBaiMau();
  khoiTaoSuaSai();
  khoiTaoSanTap();
  khoiTaoBoDe();
  khoiTaoSaoLuu();
});

function chonTab(tabId) {
  $$(".tab-btn").forEach((b) => {
    b.classList.toggle("active", b.dataset.tab === tabId);
    b.classList.toggle("bg-nhan", b.dataset.tab === tabId);
    b.classList.toggle("text-white", b.dataset.tab === tabId);
  });
  $$(".tab-noi-dung").forEach((sec) => {
    sec.classList.toggle("hidden", sec.id !== `tab-${tabId}`);
  });

  // Tải dữ liệu theo tab khi mở
  if (tabId === "tai-lieu") taiDsTaiLieu();
  else if (tabId === "kien-thuc") taiDsKienThuc();
  else if (tabId === "bai-mau") taiDsBaiMau();
  else if (tabId === "sua-sai") { taiDsBaoSai(); taiDsSuaSai(); }
  else if (tabId === "bo-de") taiDsBoDe();
}

// ------------------------------------------------------------ 01. TỔNG QUAN & CÀI ĐẶT
async function taiTongQuan() {
  try {
    const r = await TaiKhoan.goi("/admin/api/ai-rieng/tong-quan");
    if (!r.ok) return;
    const d = await r.json();
    thongKeKho = d.thong_ke || {};
    caiDatKho = d.cai_dat || {};

    // Cập nhật số
    const setSo = (k, v) => {
      const el = $(`[data-so="${k}"]`);
      if (el) el.textContent = v;
    };
    setSo("tai_lieu", `${thongKeKho.tai_lieu_da_nap || 0} / ${thongKeKho.tai_lieu_tong || 0}`);
    setSo("doan", thongKeKho.doan || 0);
    setSo("kien_thuc", thongKeKho.kien_thuc || 0);
    setSo("bai_mau", `${thongKeKho.bai_mau_duyet || 0} (${thongKeKho.bai_mau_cho || 0} chờ)`);
    setSo("sua_sai", `${thongKeKho.sua_sai || 0} (${thongKeKho.bao_sai_cho || 0} báo sai)`);

    // Điểm kiểm tra
    const lanKt = (d.lan_kiem_tra || [])[0];
    if (lanKt && lanKt.xong && lanKt.so_cau > 0) {
      const pt = Math.round((lanKt.so_dung / lanKt.so_cau) * 100);
      setSo("diem_kiem_tra", `${pt}% (${lanKt.so_dung}/${lanKt.so_cau})`);
    } else {
      setSo("diem_kiem_tra", "Chưa có");
    }

    // Badge báo sai
    const bSai = $("#so-bao-sai-badge");
    if (bSai) {
      if (thongKeKho.bao_sai_cho > 0) {
        bSai.textContent = thongKeKho.bao_sai_cho;
        bSai.classList.remove("hidden");
      } else {
        bSai.classList.add("hidden");
      }
    }

    // Embed model & nhúng
    const txtEmbed = $("#txt-embed-model");
    if (txtEmbed) txtEmbed.textContent = d.embed_model || "gemini-embedding-2";

    const txtNhung = $("#txt-nhung-trang-thai");
    if (txtNhung) {
      const nh = d.nhung || {};
      if (nh.cho_nhung > 0) {
        txtNhung.innerHTML = `<span class="text-ho-phach font-medium">Đang xử lý: còn ${nh.cho_nhung} mục chờ vector hoá</span>`;
      } else {
        txtNhung.innerHTML = `<span class="text-nhan font-medium">Đầy đủ vector nhúng</span> (đã nhúng ${nh.tong_vec || 0} mục)`;
      }
    }

    // Đổ danh sách chủ đề vào select
    const chuDeList = d.chu_de || [];
    const selKt = $("#sel-chu-de-kt");
    if (selKt && chuDeList.length) {
      const cur = selKt.value;
      selKt.innerHTML = '<option value="">Mọi chủ đề</option>' +
        chuDeList.map((c) => `<option value="${esc(c)}">${esc(c)}</option>`).join("");
      selKt.value = cur;
    }
  } catch (e) {
    console.error("Lỗi tải tổng quan:", e);
  }
}

async function taiCaiDat() {
  try {
    const r = await TaiKhoan.goi("/admin/api/ai-rieng/cai-dat");
    if (!r.ok) return;
    const cd = await r.json();
    caiDatKho = cd;

    const setVal = (id, val, isChk = false) => {
      const el = $(id);
      if (!el) return;
      if (isChk) el.checked = !!val;
      else el.value = val ?? "";
    };

    setVal("#cd-bat", cd.bat, true);
    setVal("#cd-mo-nguoi-dung", cd.mo_cho_nguoi_dung, true);
    setVal("#cd-tu-luu", cd.tu_luu_thu_vien, true);
    setVal("#cd-nghiem-ngat", cd.che_do_nghiem_ngat || "tat");
    setVal("#cd-mo-hinh", cd.mo_hinh || "");
    setVal("#cd-k-doan", cd.k_doan || 6);
    setVal("#cd-k-kien-thuc", cd.k_kien_thuc || 8);
    setVal("#cd-system-prompt", cd.system_prompt || cd._mac_dinh_system || "");
  } catch (e) {
    console.error("Lỗi tải cài đặt:", e);
  }
}

function khoiTaoCaiDat() {
  const form = $("#form-cai-dat");
  if (form) {
    form.addEventListener("submit", async (ev) => {
      ev.preventDefault();
      const body = {
        bat: $("#cd-bat").checked,
        mo_cho_nguoi_dung: $("#cd-mo-nguoi-dung").checked,
        tu_luu_thu_vien: $("#cd-tu-luu").checked,
        che_do_nghiem_ngat: $("#cd-nghiem-ngat").value,
        mo_hinh: $("#cd-mo-hinh").value,
        k_doan: parseInt($("#cd-k-doan").value, 10) || 6,
        k_kien_thuc: parseInt($("#cd-k-kien-thuc").value, 10) || 8,
      };
      try {
        const r = await TaiKhoan.goi("/admin/api/ai-rieng/cai-dat", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify(body),
        });
        if (r.ok) {
          toast("Đã lưu cấu hình AI Riêng thành công!", "xanh");
          taiTongQuan();
        } else {
          const e = await r.json();
          toast(e.loi || "Không lưu được cài đặt", "do");
        }
      } catch (err) {
        toast("Lỗi kết nối: " + err.message, "do");
      }
    });
  }

  // Lưu System Prompt
  const nutLuuPrompt = $("#nut-luu-prompt");
  if (nutLuuPrompt) {
    nutLuuPrompt.addEventListener("click", async () => {
      const prompt = $("#cd-system-prompt").value.trim();
      try {
        const r = await TaiKhoan.goi("/admin/api/ai-rieng/cai-dat", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ system_prompt: prompt }),
        });
        if (r.ok) {
          toast("Đã lưu System Prompt của AI Riêng!", "xanh");
        } else {
          const e = await r.json();
          toast(e.loi || "Lỗi lưu Prompt", "do");
        }
      } catch (err) {
        toast("Lỗi kết nối: " + err.message, "do");
      }
    });
  }

  // Khôi phục prompt mặc định
  const nutKhoiPhuc = $("#nut-khoi-phuc-prompt");
  if (nutKhoiPhuc) {
    nutKhoiPhuc.addEventListener("click", () => {
      if (caiDatKho._mac_dinh_system) {
        $("#cd-system-prompt").value = caiDatKho._mac_dinh_system;
        toast("Đã nạp prompt mặc định (hãy bấm 'Lưu Prompt' để ghi nhận)", "vang");
      }
    });
  }
}

// ------------------------------------------------------------ 02. TÀI LIỆU & ĐOẠN TRÍCH
function khoiTaoTaiLieu() {
  // Chuyển cách nạp
  $$('input[name="cach-nap"]').forEach((r) => {
    r.addEventListener("change", () => {
      const v = r.value;
      $("#form-nap-thu-muc")?.classList.toggle("hidden", v !== "thu-muc");
      $("#form-nap-tep")?.classList.toggle("hidden", v !== "tep");
      $("#form-nap-link")?.classList.toggle("hidden", v !== "link");
      $("#form-nap-van-ban")?.classList.toggle("hidden", v !== "van-ban");
    });
  });

  // Chuyển kiểu chọn thư mục (folder từ máy vs đường dẫn local)
  $$('input[name="kieu-thu-muc"]').forEach((r) => {
    r.addEventListener("change", () => {
      const k = r.value;
      $("#khoi-chon-folder")?.classList.toggle("hidden", k !== "chon-folder");
      $("#khoi-duong-dan-folder")?.classList.toggle("hidden", k !== "duong-dan");
    });
  });

  const DUOI_HOP_LE = new Set(["pdf", "docx", "txt", "md", "csv", "html", "htm", "png", "jpg", "jpeg", "webp", "heic", "heif"]);

  // Click vùng chọn thư mục
  $("#vung-chon-thu-muc")?.addEventListener("click", () => {
    $("#inp-thu-muc-tai-lieu")?.click();
  });

  let danhSachTepFolder = [];

  // Lắng nghe chọn thư mục từ trình duyệt
  $("#inp-thu-muc-tai-lieu")?.addEventListener("change", () => {
    const files = Array.from($("#inp-thu-muc-tai-lieu").files || []);
    if (!files.length) {
      danhSachTepFolder = [];
      $("#hop-tom-tat-folder")?.classList.add("hidden");
      return;
    }

    // Lọc các file hợp lệ
    const hopLe = [];
    let boQua = 0;
    let tongDungLuong = 0;
    let tenThuMucGoc = "";

    files.forEach((f) => {
      const ten = f.name;
      if (ten.startsWith(".") || ten.startsWith("~$") || ten === "Thumbs.db" || ten === "desktop.ini") {
        boQua++;
        return;
      }
      const ext = ten.includes(".") ? ten.split(".").pop().toLowerCase() : "";
      if (DUOI_HOP_LE.has(ext)) {
        hopLe.push(f);
        tongDungLuong += f.size;
        if (!tenThuMucGoc && f.webkitRelativePath) {
          tenThuMucGoc = f.webkitRelativePath.split("/")[0];
        }
      } else {
        boQua++;
      }
    });

    danhSachTepFolder = hopLe;

    // Tự động điền chủ đề theo tên folder nếu chưa nhập
    if (tenThuMucGoc && !$("#inp-chu-de-thu-muc").value.trim()) {
      $("#inp-chu-de-thu-muc").value = tenThuMucGoc.replace(/[_-]+/g, " ");
    }

    $("#hop-tom-tat-folder")?.classList.remove("hidden");
    const elTen = $("#txt-ten-folder-goc");
    if (elTen) elTen.textContent = `📁 ${tenThuMucGoc || "Thư mục đã chọn"}`;
    const mb = (tongDungLuong / (1024 * 1024)).toFixed(1);
    const elSo = $("#txt-so-file-folder");
    if (elSo) elSo.textContent = `${hopLe.length} file hợp lệ (${mb} MB)`;
    const elChiTiet = $("#txt-chi-tiet-folder");
    if (elChiTiet) elChiTiet.textContent = `Đã tìm thấy ${files.length} file (bỏ qua ${boQua} file không thuộc định dạng tài liệu hỗ trợ)`;
  });

  // Xem trước thư mục từ đường dẫn máy chủ
  $("#nut-xem-truoc-thu-muc")?.addEventListener("click", async () => {
    const dd = $("#inp-duong-dan-local").value.trim();
    if (!dd) return toast("Vui lòng nhập đường dẫn thư mục trên máy", "vang");

    const nut = $("#nut-xem-truoc-thu-muc");
    nut.disabled = true;
    nut.textContent = "Đang quét...";
    try {
      const r = await TaiKhoan.goi("/admin/api/ai-rieng/tai-lieu/xem-truoc-thu-muc", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ duong_dan: dd, de_quy: true }),
      });
      const d = await r.json();
      if (r.ok) {
        $("#hop-xem-truoc-local")?.classList.remove("hidden");
        const elTen = $("#txt-local-ten-folder");
        if (elTen) elTen.textContent = `📁 ${d.ten_thu_muc}`;
        const elSo = $("#txt-local-so-file");
        if (elSo) elSo.textContent = `${d.tong_hop_le} file hợp lệ`;
        const elChiTiet = $("#txt-local-chi-tiet");
        if (elChiTiet) elChiTiet.textContent = `Tìm thấy ${d.tong_hop_le} tài liệu (PDF, Word, TXT, Ảnh...) và bỏ qua ${d.tong_bo_qua} file rác/không hỗ trợ.`;
        if (d.ten_thu_muc && !$("#inp-chu-de-thu-muc").value.trim()) {
          $("#inp-chu-de-thu-muc").value = d.ten_thu_muc.replace(/[_-]+/g, " ");
        }
        toast(`Thư mục hợp lệ: tìm thấy ${d.tong_hop_le} file!`, "xanh");
      } else {
        toast(d.loi || "Không đọc được thư mục", "do");
      }
    } catch (err) {
      toast("Lỗi kết nối: " + err.message, "do");
    } finally {
      nut.disabled = false;
      nut.textContent = "Quét thử";
    }
  });

  // Xử lý submit form nạp cả thư mục
  $("#form-nap-thu-muc")?.addEventListener("submit", async (ev) => {
    ev.preventDefault();
    const kieu = $('input[name="kieu-thu-muc"]:checked')?.value || "chon-folder";
    const chuDeChung = $("#inp-chu-de-thu-muc").value.trim();
    const coLoiGiai = $("#chk-co-loi-giai-thu-muc")?.checked || false;
    const kemThuMucCon = $("#chk-kem-thu-muc-con")?.checked || false;
    const nutNop = $("#nut-nop-thu-muc");

    if (kieu === "duong-dan") {
      const dd = $("#inp-duong-dan-local").value.trim();
      if (!dd) return toast("Vui lòng nhập đường dẫn thư mục", "vang");

      nutNop.disabled = true;
      nutNop.innerHTML = `<span class="animate-spin inline-block mr-1">↻</span> Đang quét và nạp thư mục...`;
      toast("Đang đọc và nạp toàn bộ thư mục máy chủ...", "tin");

      try {
        const r = await TaiKhoan.goi("/admin/api/ai-rieng/tai-lieu/quet-thu-muc", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            duong_dan: dd,
            chu_de: chuDeChung,
            co_loi_giai: coLoiGiai,
            de_quy: true,
            kem_thu_muc_con: kemThuMucCon,
          }),
        });
        const d = await r.json();
        if (r.ok) {
          toast(`Đã nạp thành công ${d.tong_hop_le} tài liệu từ thư mục! AI đang tách đoạn...`, "xanh");
          taiDsTaiLieu();
          taiTongQuan();
          $("#inp-duong-dan-local").value = "";
          $("#hop-xem-truoc-local")?.classList.add("hidden");
        } else {
          toast(d.loi || "Lỗi nạp thư mục", "do");
        }
      } catch (err) {
        toast("Lỗi kết nối: " + err.message, "do");
      } finally {
        nutNop.disabled = false;
        nutNop.innerHTML = `<span>Nạp toàn bộ thư mục & Tách đoạn</span>`;
      }
      return;
    }

    // Trường hợp chọn folder qua trình duyệt
    if (!danhSachTepFolder.length) {
      return toast("Vui lòng chọn thư mục chứa các file tài liệu hợp lệ trước khi nạp.", "vang");
    }

    const tongSo = danhSachTepFolder.length;
    nutNop.disabled = true;
    $("#hop-tien-trinh-thu-muc")?.classList.remove("hidden");

    let tongThanhCong = 0;
    const BATCH_SIZE = 30; // Nạp 30 file mỗi đợt để tránh nghẽn mạng

    for (let i = 0; i < tongSo; i += BATCH_SIZE) {
      const batch = danhSachTepFolder.slice(i, i + BATCH_SIZE);
      const fd = new FormData();
      const relPaths = [];

      batch.forEach((f) => {
        fd.append("tep", f);
        relPaths.push(f.webkitRelativePath || f.name);
      });

      fd.append("chu_de", chuDeChung);
      fd.append("co_loi_giai", coLoiGiai);
      fd.append("duong_dan_tuong_doi", JSON.stringify(relPaths));

      const phanTram = Math.round((i / tongSo) * 100);
      const elTrangThai = $("#txt-trang-thai-tien-trinh");
      if (elTrangThai) elTrangThai.textContent = `Đang nạp file ${i + 1} - ${Math.min(i + BATCH_SIZE, tongSo)} / ${tongSo}...`;
      const elPhanTram = $("#txt-phan-tram-tien-trinh");
      if (elPhanTram) elPhanTram.textContent = `${phanTram}%`;
      const elThanh = $("#thanh-tien-trinh-thu-muc");
      if (elThanh) elThanh.style.width = `${phanTram}%`;

      try {
        const r = await TaiKhoan.goi("/admin/api/ai-rieng/tai-lieu", { method: "POST", body: fd });
        const d = await r.json();
        if (r.ok && d.da_them) {
          tongThanhCong += d.da_them.length;
        }
      } catch (err) {
        console.error("Lỗi đợt nạp:", err);
      }
    }

    const elTrangThai = $("#txt-trang-thai-tien-trinh");
    if (elTrangThai) elTrangThai.textContent = `Hoàn tất nạp ${tongThanhCong}/${tongSo} tài liệu!`;
    const elPhanTram = $("#txt-phan-tram-tien-trinh");
    if (elPhanTram) elPhanTram.textContent = "100%";
    const elThanh = $("#thanh-tien-trinh-thu-muc");
    if (elThanh) elThanh.style.width = "100%";

    toast(`Đã nạp thành công ${tongThanhCong} tài liệu từ thư mục! AI đang tiến hành đọc và tách đoạn.`, "xanh");
    setTimeout(() => {
      $("#hop-tien-trinh-thu-muc")?.classList.add("hidden");
      if (elThanh) elThanh.style.width = "0%";
      const inp = $("#inp-thu-muc-tai-lieu");
      if (inp) inp.value = "";
      $("#hop-tom-tat-folder")?.classList.add("hidden");
      danhSachTepFolder = [];
      nutNop.disabled = false;
      nutNop.innerHTML = `<span>Nạp toàn bộ thư mục & Tách đoạn</span>`;
      taiDsTaiLieu();
      taiTongQuan();
    }, 1200);
  });

  // Nạp tệp
  $("#form-nap-tep").addEventListener("submit", async (ev) => {
    ev.preventDefault();
    const files = $("#inp-tep-tai-lieu").files;
    if (!files || !files.length) return toast("Vui lòng chọn ít nhất một file.", "vang");

    const fd = new FormData();
    for (let i = 0; i < files.length; i++) fd.append("tep", files[i]);
    fd.append("chu_de", $("#inp-chu-de-tep").value.trim());
    fd.append("co_loi_giai", $("#chk-co-loi-giai-tep").checked);

    toast(`Đang tải lên ${files.length} file và tách đoạn...`, "tin");
    try {
      const r = await TaiKhoan.goi("/admin/api/ai-rieng/tai-lieu", { method: "POST", body: fd });
      const d = await r.json();
      if (r.ok) {
        toast(`Đã nạp ${d.da_them.length} tài liệu thành công!`, "xanh");
        $("#inp-tep-tai-lieu").value = "";
        taiDsTaiLieu();
        taiTongQuan();
      } else {
        toast(d.loi || "Lỗi tải lên tài liệu", "do");
      }
    } catch (err) {
      toast("Lỗi kết nối: " + err.message, "do");
    }
  });

  // Nạp link
  $("#form-nap-link").addEventListener("submit", async (ev) => {
    ev.preventDefault();
    const body = {
      url: $("#inp-url-link").value.trim(),
      ten: $("#inp-ten-link").value.trim(),
      chu_de: $("#inp-chu-de-link").value.trim(),
      co_loi_giai: false,
    };
    try {
      const r = await TaiKhoan.goi("/admin/api/ai-rieng/tai-lieu/link", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      });
      if (r.ok) {
        toast("Đã thêm link tài liệu, hệ thống đang cào nội dung!", "xanh");
        $("#inp-url-link").value = "";
        taiDsTaiLieu();
      } else {
        const e = await r.json();
        toast(e.loi || "Không nạp được link", "do");
      }
    } catch (err) {
      toast("Lỗi kết nối: " + err.message, "do");
    }
  });

  // Nạp văn bản
  $("#form-nap-van-ban").addEventListener("submit", async (ev) => {
    ev.preventDefault();
    const body = {
      ten: $("#inp-ten-vb").value.trim(),
      noi_dung: $("#inp-nd-vb").value.trim(),
      chu_de: $("#inp-chu-de-vb").value.trim(),
      co_loi_giai: false,
    };
    try {
      const r = await TaiKhoan.goi("/admin/api/ai-rieng/tai-lieu/van-ban", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      });
      if (r.ok) {
        toast("Đã lưu văn bản và tách đoạn thành công!", "xanh");
        $("#inp-nd-vb").value = "";
        taiDsTaiLieu();
      } else {
        const e = await r.json();
        toast(e.loi || "Lỗi nạp văn bản", "do");
      }
    } catch (err) {
      toast("Lỗi kết nối: " + err.message, "do");
    }
  });

  $("#nut-lam-moi-tl").addEventListener("click", taiDsTaiLieu);
}

async function taiDsTaiLieu() {
  const tbody = $("#ds-tai-lieu-body");
  if (!tbody) return;
  try {
    const r = await TaiKhoan.goi("/admin/api/ai-rieng/tai-lieu");
    if (!r.ok) return;
    const d = await r.json();
    const list = d.tai_lieu || [];
    $("#dem-tai-lieu").textContent = list.length;

    if (!list.length) {
      tbody.innerHTML = '<tr><td colspan="6" class="text-center py-6 text-muc-2">Chưa có tài liệu nào trong kho. Hãy nạp file ở cột bên trái!</td></tr>';
      return;
    }

    tbody.innerHTML = list.map((tl) => {
      let ttBadge = '<span class="chip chip-xanh">Đã nạp</span>';
      if (tl.trang_thai === "dang_tach") ttBadge = '<span class="chip chip-vang">Đang tách đoạn</span>';
      else if (tl.trang_thai === "cho") ttBadge = '<span class="chip">Chờ xử lý</span>';
      else if (tl.trang_thai === "loi") ttBadge = `<span class="chip chip-do" title="${esc(tl.loi)}">Lỗi</span>`;

      return `
        <tr class="border-b border-chi">
          <td class="py-2.5 font-medium text-muc">${esc(tl.ten)}</td>
          <td><span class="nhan-mono">${esc(tl.loai)}</span></td>
          <td>${renderChuDe(tl.chu_de)}</td>
          <td class="font-mono">${tl.so_doan}</td>
          <td>${ttBadge}</td>
          <td class="text-right whitespace-nowrap space-x-1">
            <button type="button" class="btn btn-sm py-0.5 px-2 text-xs" onclick="xemDoanTaiLieu(${tl.id}, '${esc(tl.ten)}')">Xem đoạn</button>
            <button type="button" class="btn btn-sm py-0.5 px-2 text-xs" onclick="xuLyLaiTaiLieu(${tl.id})">Tách lại</button>
            <button type="button" class="btn btn-sm btn-do py-0.5 px-2 text-xs" onclick="xoaTaiLieu(${tl.id})">Xoá</button>
          </td>
        </tr>
      `;
    }).join("");
  } catch (e) {
    console.error("Lỗi tải danh sách tài liệu:", e);
  }
}

window.xemDoanTaiLieu = async (tid, ten) => {
  const modal = $("#modal-doan");
  const tieuDe = $("#modal-doan-tieu-de");
  const noiDung = $("#modal-doan-noi-dung");
  tieuDe.textContent = `Đoạn trích: ${ten}`;
  noiDung.innerHTML = '<p class="text-muc-2">Đang tải đoạn trích…</p>';
  modal.showModal();

  try {
    const r = await TaiKhoan.goi(`/admin/api/ai-rieng/tai-lieu/${tid}/doan`);
    const d = await r.json();
    const doan = d.doan || [];
    if (!doan.length) {
      noiDung.innerHTML = '<p class="text-muc-2 py-4">Tài liệu chưa có đoạn trích nào (hoặc đang xử lý tách đoạn).</p>';
      return;
    }
    noiDung.innerHTML = doan.map((dn, idx) => `
      <div class="p-3 rounded-lg border border-chi bg-giay-lom space-y-2">
        <div class="flex items-center justify-between text-[11px] text-muc-2">
          <span>Đoạn #${idx + 1} ${dn.trang ? `(Trang ${dn.trang})` : ""}</span>
          <button type="button" class="text-gach hover:underline" onclick="xoaDoan(${dn.id}, ${tid}, '${esc(ten)}')">Xoá đoạn</button>
        </div>
        <textarea class="o-nhap w-full text-xs font-mono" rows="3" id="dn-nd-${dn.id}">${esc(dn.noi_dung)}</textarea>
        <div class="flex justify-end">
          <button type="button" class="btn btn-sm py-0.5 px-2 text-[11px]" onclick="luuDoan(${dn.id})">Lưu sửa</button>
        </div>
      </div>
    `).join("");
  } catch (err) {
    noiDung.innerHTML = `<p class="text-gach">Lỗi: ${esc(err.message)}</p>`;
  }
};

window.luuDoan = async (did) => {
  const nd = $(`#dn-nd-${did}`).value.trim();
  try {
    const r = await TaiKhoan.goi(`/admin/api/ai-rieng/doan/${did}`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ noi_dung: nd }),
    });
    if (r.ok) toast("Đã lưu sửa đoạn kiến thức!", "xanh");
    else toast("Không lưu được đoạn", "do");
  } catch (e) {
    toast("Lỗi kết nối", "do");
  }
};

window.xoaDoan = async (did, tid, ten) => {
  if (!confirm("Bạn có chắc muốn xoá đoạn này?")) return;
  try {
    const r = await TaiKhoan.goi(`/admin/api/ai-rieng/doan/${did}`, { method: "DELETE" });
    if (r.ok) {
      toast("Đã xoá đoạn!", "xanh");
      xemDoanTaiLieu(tid, ten);
      taiDsTaiLieu();
    }
  } catch (e) {
    toast("Lỗi xoá đoạn", "do");
  }
};

window.xuLyLaiTaiLieu = async (tid) => {
  try {
    const r = await TaiKhoan.goi(`/admin/api/ai-rieng/tai-lieu/${tid}/xu-ly-lai`, { method: "POST" });
    if (r.ok) {
      toast("Đã đưa tài liệu vào hàng đợi tách đoạn lại!", "xanh");
      taiDsTaiLieu();
    } else {
      const e = await r.json();
      toast(e.loi || "Lỗi tách lại", "do");
    }
  } catch (e) {
    toast("Lỗi kết nối", "do");
  }
};

window.xoaTaiLieu = async (tid) => {
  if (!confirm("Xoá tài liệu này và toàn bộ các đoạn kiến thức trích xuất liên quan?")) return;
  try {
    const r = await TaiKhoan.goi(`/admin/api/ai-rieng/tai-lieu/${tid}`, { method: "DELETE" });
    if (r.ok) {
      toast("Đã xoá tài liệu thành công!", "xanh");
      taiDsTaiLieu();
      taiTongQuan();
    }
  } catch (e) {
    toast("Lỗi xoá tài liệu", "do");
  }
};

// ------------------------------------------------------------ 03. KIẾN THỨC & QUY TẮC
function khoiTaoKienThuc() {
  $("#inp-tim-kt").addEventListener("input", debounce(taiDsKienThuc, 300));
  $("#sel-chu-de-kt").addEventListener("change", taiDsKienThuc);

  $("#nut-them-kt-moi").addEventListener("click", () => {
    $("#modal-kt-tieu-de").textContent = "Thêm quy tắc mới";
    $("#inp-kt-id").value = "";
    $("#inp-kt-tieu-de").value = "";
    $("#inp-kt-noi-dung").value = "";
    $("#inp-kt-chu-de").value = "";
    $("#inp-kt-uu-tien").value = "3";
    $("#modal-kien-thuc").showModal();
  });

  $("#form-modal-kt").addEventListener("submit", async (ev) => {
    ev.preventDefault();
    const id = $("#inp-kt-id").value;
    const body = {
      tieu_de: $("#inp-kt-tieu-de").value.trim(),
      noi_dung: $("#inp-kt-noi-dung").value.trim(),
      chu_de: $("#inp-kt-chu-de").value.trim(),
      uu_tien: parseInt($("#inp-kt-uu-tien").value, 10) || 3,
      bat: true,
    };
    try {
      const url = id ? `/admin/api/ai-rieng/kien-thuc/${id}` : "/admin/api/ai-rieng/kien-thuc";
      const r = await TaiKhoan.goi(url, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      });
      if (r.ok) {
        toast("Đã lưu quy tắc kiến thức thành công!", "xanh");
        $("#modal-kien-thuc").close();
        taiDsKienThuc();
        taiTongQuan();
      } else {
        const e = await r.json();
        toast(e.loi || "Không lưu được quy tắc", "do");
      }
    } catch (e) {
      toast("Lỗi kết nối", "do");
    }
  });

  // Nhập từ file
  $("#nut-nhap-kt-file").addEventListener("click", () => $("#inp-file-kt").click());
  $("#inp-file-kt").addEventListener("change", async (ev) => {
    const f = ev.target.files[0];
    if (!f) return;
    const fd = new FormData();
    fd.append("tep", f);
    toast("Đang nhập quy tắc từ file...", "tin");
    try {
      const r = await TaiKhoan.goi("/admin/api/ai-rieng/kien-thuc/nhap", { method: "POST", body: fd });
      const d = await r.json();
      if (r.ok) {
        toast(`Đã nhập thành công ${d.da_nhap} quy tắc!`, "xanh");
        taiDsKienThuc();
        taiTongQuan();
      } else {
        toast(d.loi || "Lỗi nhập file", "do");
      }
    } catch (e) {
      toast("Lỗi kết nối", "do");
    }
    ev.target.value = "";
  });
}

async function taiDsKienThuc() {
  const tbody = $("#ds-kien-thuc-body");
  if (!tbody) return;
  const q = $("#inp-tim-kt") ? $("#inp-tim-kt").value.trim() : "";
  const chuDe = $("#sel-chu-de-kt") ? $("#sel-chu-de-kt").value : "";

  try {
    const r = await TaiKhoan.goi(`/admin/api/ai-rieng/kien-thuc?q=${encodeURIComponent(q)}&chu_de=${encodeURIComponent(chuDe)}`);
    if (!r.ok) return;
    const d = await r.json();
    const list = d.kien_thuc || [];

    if (!list.length) {
      tbody.innerHTML = '<tr><td colspan="6" class="text-center py-6 text-muc-2">Không tìm thấy quy tắc nào. Hãy bấm "+ Thêm quy tắc" để dạy AI!</td></tr>';
      return;
    }

    tbody.innerHTML = list.map((kt) => `
      <tr class="border-b border-chi">
        <td class="font-mono text-center font-bold text-nhan">${kt.uu_tien}</td>
        <td class="font-medium text-muc">${esc(kt.tieu_de)}</td>
        <td class="text-muc-2 line-clamp-2 max-w-md font-mono">${esc(kt.noi_dung)}</td>
        <td>${renderChuDe(kt.chu_de)}</td>
        <td class="text-center"><span class="${kt.bat ? "text-nhan" : "text-gach"} text-sm">${kt.bat ? "●" : "○"}</span></td>
        <td class="text-right whitespace-nowrap space-x-1">
          <button type="button" class="btn btn-sm py-0.5 px-2 text-xs" onclick='suaKienThuc(${JSON.stringify(kt)})'>Sửa</button>
          <button type="button" class="btn btn-sm btn-do py-0.5 px-2 text-xs" onclick="xoaKienThuc(${kt.id})">Xoá</button>
        </td>
      </tr>
    `).join("");
  } catch (e) {
    console.error("Lỗi tải kiến thức:", e);
  }
}

window.suaKienThuc = (kt) => {
  $("#modal-kt-tieu-de").textContent = "Sửa quy tắc";
  $("#inp-kt-id").value = kt.id;
  $("#inp-kt-tieu-de").value = kt.tieu_de || "";
  $("#inp-kt-noi-dung").value = kt.noi_dung || "";
  $("#inp-kt-chu-de").value = kt.chu_de || "";
  $("#inp-kt-uu-tien").value = String(kt.uu_tien || 3);
  $("#modal-kien-thuc").showModal();
};

window.xoaKienThuc = async (kid) => {
  if (!confirm("Bạn có chắc muốn xoá quy tắc này khỏi kho kiến thức?")) return;
  try {
    const r = await TaiKhoan.goi(`/admin/api/ai-rieng/kien-thuc/${kid}`, { method: "DELETE" });
    if (r.ok) {
      toast("Đã xoá quy tắc!", "xanh");
      taiDsKienThuc();
      taiTongQuan();
    }
  } catch (e) {
    toast("Lỗi xoá quy tắc", "do");
  }
};

// ------------------------------------------------------------ 04. THƯ VIỆN LỜI GIẢI & BÀI MẪU
function khoiTaoBaiMau() {
  $("#inp-tim-bm").addEventListener("input", debounce(taiDsBaiMau, 300));
  $("#sel-trang-thai-bm").addEventListener("change", taiDsBaiMau);

  $("#nut-duyet-tat-ca-bm").addEventListener("click", async () => {
    try {
      const r = await TaiKhoan.goi("/admin/api/ai-rieng/bai-mau?trang_thai=cho_duyet");
      const d = await r.json();
      const ids = (d.bai_mau || []).map((b) => b.id);
      if (!ids.length) return toast("Không có bài mẫu nào đang chờ duyệt.", "vang");

      const rDuyet = await TaiKhoan.goi("/admin/api/ai-rieng/bai-mau/duyet", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ ids, trang_thai: "duyet" }),
      });
      if (rDuyet.ok) {
        toast(`Đã duyệt ${ids.length} bài mẫu vào thư viện!`, "xanh");
        taiDsBaiMau();
        taiTongQuan();
      }
    } catch (e) {
      toast("Lỗi duyệt bài", "do");
    }
  });

  $("#nut-them-bm-moi").addEventListener("click", () => {
    const de = prompt("Nhập đề bài chuỗi phản ứng (vd: C2H4 → C2H5OH → CH3CHO):");
    if (!de || !de.trim()) return;
    toast("Bạn có thể giải thử chuỗi này ở mục 'Sân tập' rồi bấm 'Đúng → Lưu thư viện' để tự động tạo cấu trúc chuẩn!", "tin", 8000);
    chonTab("san-tap");
    $("#inp-de-san-tap").value = de.trim();
  });
}

async function taiDsBaiMau() {
  const tbody = $("#ds-bai-mau-body");
  if (!tbody) return;
  const q = $("#inp-tim-bm") ? $("#inp-tim-bm").value.trim() : "";
  const tt = $("#sel-trang-thai-bm") ? $("#sel-trang-thai-bm").value : "";

  try {
    const r = await TaiKhoan.goi(`/admin/api/ai-rieng/bai-mau?q=${encodeURIComponent(q)}&trang_thai=${encodeURIComponent(tt)}`);
    if (!r.ok) return;
    const d = await r.json();
    const list = d.bai_mau || [];

    if (!list.length) {
      tbody.innerHTML = '<tr><td colspan="7" class="text-center py-6 text-muc-2">Không tìm thấy bài mẫu nào.</td></tr>';
      return;
    }

    tbody.innerHTML = list.map((bm) => {
      let ttChip = '<span class="chip chip-xanh">Đã duyệt</span>';
      if (bm.trang_thai === "cho_duyet") ttChip = '<span class="chip chip-vang">Chờ duyệt</span>';
      else if (bm.trang_thai === "sai") ttChip = '<span class="chip chip-do">Sai</span>';

      return `
        <tr class="border-b border-chi">
          <td class="font-mono text-muc font-medium">${esc(bm.de)}</td>
          <td class="text-muc-2 text-[11px] font-mono">${esc(bm.tom_tat || "Xem chi tiết")}</td>
          <td><span class="${bm.rdkit_ok ? "text-nhan" : "text-gach"}">${bm.rdkit_ok ? "Hợp lệ" : "Lỗi"}</span></td>
          <td><span class="nhan-mono">${esc(bm.nguon || "tay")}</span></td>
          <td class="font-mono">${bm.so_lan_dung || 0}</td>
          <td>${ttChip}</td>
          <td class="text-right whitespace-nowrap space-x-1">
            ${bm.trang_thai === "cho_duyet" ? `<button type="button" class="btn btn-sm btn-primary py-0.5 px-2 text-xs" onclick="duyetBaiMau(${bm.id})">Duyệt</button>` : ""}
            <button type="button" class="btn btn-sm btn-do py-0.5 px-2 text-xs" onclick="xoaBaiMau(${bm.id})">Xoá</button>
          </td>
        </tr>
      `;
    }).join("");
  } catch (e) {
    console.error("Lỗi tải bài mẫu:", e);
  }
}

window.duyetBaiMau = async (bid) => {
  try {
    const r = await TaiKhoan.goi("/admin/api/ai-rieng/bai-mau/duyet", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ ids: [bid], trang_thai: "duyet" }),
    });
    if (r.ok) {
      toast("Đã duyệt bài mẫu vào thư viện!", "xanh");
      taiDsBaiMau();
      taiTongQuan();
    }
  } catch (e) {
    toast("Lỗi kết nối", "do");
  }
};

window.xoaBaiMau = async (bid) => {
  if (!confirm("Xoá bài mẫu này khỏi thư viện?")) return;
  try {
    const r = await TaiKhoan.goi(`/admin/api/ai-rieng/bai-mau/${bid}`, { method: "DELETE" });
    if (r.ok) {
      toast("Đã xoá bài mẫu!", "xanh");
      taiDsBaiMau();
      taiTongQuan();
    }
  } catch (e) {
    toast("Lỗi kết nối", "do");
  }
};

// ------------------------------------------------------------ 05. SỬA SAI & HÀNG CHỜ BÁO SAI
function khoiTaoSuaSai() {
  $("#nut-lam-moi-bao-sai").addEventListener("click", taiDsBaoSai);

  $("#nut-them-sua-sai-moi").addEventListener("click", () => {
    $("#modal-ss-tieu-de").textContent = "Thêm mục dạy sửa sai";
    $("#inp-ss-id").value = "";
    $("#inp-ss-bao-sai-id").value = "";
    $("#inp-ss-de").value = "";
    $("#inp-ss-sai").value = "";
    $("#inp-ss-dung").value = "";
    $("#inp-ss-ly-do").value = "";
    $("#inp-ss-chu-de").value = "";
    $("#modal-sua-sai").showModal();
  });

  $("#form-modal-ss").addEventListener("submit", async (ev) => {
    ev.preventDefault();
    const id = $("#inp-ss-id").value;
    const baoSaiId = $("#inp-ss-bao-sai-id").value;

    const body = {
      de: $("#inp-ss-de").value.trim(),
      sai: $("#inp-ss-sai").value.trim(),
      dung: $("#inp-ss-dung").value.trim(),
      ly_do: $("#inp-ss-ly-do").value.trim(),
      chu_de: $("#inp-ss-chu-de").value.trim(),
      bat: true,
    };

    try {
      let r;
      if (baoSaiId) {
        // Chuyển từ báo sai
        r = await TaiKhoan.goi(`/admin/api/ai-rieng/bao-sai/${baoSaiId}/chuyen`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ dung: body.dung, ly_do: body.ly_do, chu_de: body.chu_de }),
        });
      } else if (id) {
        r = await TaiKhoan.goi(`/admin/api/ai-rieng/sua-sai/${id}`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify(body),
        });
      } else {
        r = await TaiKhoan.goi("/admin/api/ai-rieng/sua-sai", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify(body),
        });
      }

      if (r.ok) {
        toast("Đã lưu mục sửa sai cho AI!", "xanh");
        $("#modal-sua-sai").close();
        taiDsBaoSai();
        taiDsSuaSai();
        taiTongQuan();
      } else {
        const e = await r.json();
        toast(e.loi || "Không lưu được mục sửa sai", "do");
      }
    } catch (e) {
      toast("Lỗi kết nối", "do");
    }
  });
}

async function taiDsBaoSai() {
  const tbody = $("#ds-bao-sai-body");
  if (!tbody) return;
  try {
    const r = await TaiKhoan.goi("/admin/api/ai-rieng/bao-sai?trang_thai=cho");
    if (!r.ok) return;
    const d = await r.json();
    const list = d.bao_sai || [];
    $("#dem-bao-sai").textContent = list.length;

    if (!list.length) {
      tbody.innerHTML = '<tr><td colspan="4" class="text-center py-4 text-muc-2">Không có báo cáo sai nào đang chờ duyệt.</td></tr>';
      return;
    }

    tbody.innerHTML = list.map((b) => `
      <tr class="border-b border-chi">
        <td class="font-mono text-muc font-medium">${esc(b.de)}</td>
        <td><span class="nhan-mono">${esc(b.mo_hinh || "AI")}</span></td>
        <td class="text-xs">
          ${b.ghi_chu ? `<p class="font-medium text-gach">Ghi chú: ${esc(b.ghi_chu)}</p>` : ""}
          <p class="text-muc-2 font-mono line-clamp-2 mt-0.5">${esc(b.loi_giai_chu || "")}</p>
        </td>
        <td class="text-right whitespace-nowrap space-x-1">
          <button type="button" class="btn btn-sm btn-primary py-0.5 px-2 text-xs" onclick='chuyenBaoSai(${JSON.stringify(b)})'>Dạy sửa</button>
          <button type="button" class="btn btn-sm py-0.5 px-2 text-xs" onclick="boQuaBaoSai(${b.id})">Bỏ qua</button>
        </td>
      </tr>
    `).join("");
  } catch (e) {
    console.error("Lỗi tải báo sai:", e);
  }
}

window.chuyenBaoSai = (b) => {
  $("#modal-ss-tieu-de").textContent = "Dạy sửa sai từ phản ánh của học sinh";
  $("#inp-ss-id").value = "";
  $("#inp-ss-bao-sai-id").value = b.id;
  $("#inp-ss-de").value = b.de || "";
  $("#inp-ss-sai").value = b.loi_giai_chu || b.ghi_chu || "";
  $("#inp-ss-dung").value = "";
  $("#inp-ss-ly-do").value = b.ghi_chu ? `Học sinh lưu ý: ${b.ghi_chu}` : "";
  $("#inp-ss-chu-de").value = "";
  $("#modal-sua-sai").showModal();
};

window.boQuaBaoSai = async (bid) => {
  if (!confirm("Bỏ qua báo sai này?")) return;
  try {
    const r = await TaiKhoan.goi(`/admin/api/ai-rieng/bao-sai/${bid}/bo-qua`, { method: "POST" });
    if (r.ok) {
      toast("Đã bỏ qua phản ánh", "tin");
      taiDsBaoSai();
      taiTongQuan();
    }
  } catch (e) {
    toast("Lỗi kết nối", "do");
  }
};

async function taiDsSuaSai() {
  const tbody = $("#ds-sua-sai-body");
  if (!tbody) return;
  try {
    const r = await TaiKhoan.goi("/admin/api/ai-rieng/sua-sai");
    if (!r.ok) return;
    const d = await r.json();
    const list = d.sua_sai || [];
    $("#dem-sua-sai-list").textContent = list.length;

    if (!list.length) {
      tbody.innerHTML = '<tr><td colspan="5" class="text-center py-4 text-muc-2">Chưa có mục sửa sai nào được dạy.</td></tr>';
      return;
    }

    tbody.innerHTML = list.map((ss) => `
      <tr class="border-b border-chi">
        <td class="font-mono text-muc font-medium">${esc(ss.de)}</td>
        <td class="text-gach font-mono text-[11px] line-clamp-2">${esc(ss.sai || "—")}</td>
        <td class="text-nhan font-mono text-[11px]">${esc(ss.dung)} ${ss.ly_do ? `<span class="text-muc-2">(${esc(ss.ly_do)})</span>` : ""}</td>
        <td><span class="${ss.bat ? "text-nhan" : "text-gach"}">${ss.bat ? "●" : "○"}</span></td>
        <td class="text-right whitespace-nowrap space-x-1">
          <button type="button" class="btn btn-sm btn-do py-0.5 px-2 text-xs" onclick="xoaSuaSai(${ss.id})">Xoá</button>
        </td>
      </tr>
    `).join("");
  } catch (e) {
    console.error("Lỗi tải mục sửa sai:", e);
  }
}

window.xoaSuaSai = async (sid) => {
  if (!confirm("Xoá mục sửa sai này?")) return;
  try {
    const r = await TaiKhoan.goi(`/admin/api/ai-rieng/sua-sai/${sid}`, { method: "DELETE" });
    if (r.ok) {
      toast("Đã xoá mục sửa sai!", "xanh");
      taiDsSuaSai();
      taiTongQuan();
    }
  } catch (e) {
    toast("Lỗi kết nối", "do");
  }
};

// ------------------------------------------------------------ 06. SÂN TẬP (THỬ NGHIỆM)
function khoiTaoSanTap() {
  const form = $("#form-san-tap");
  if (!form) return;

  form.addEventListener("submit", async (ev) => {
    ev.preventDefault();
    const de = $("#inp-de-san-tap").value.trim();
    if (!de) return;

    const boQuaTv = $("#chk-bo-qua-tv").checked;
    const soSanh = $("#chk-so-sanh").checked;
    const dungClaude = $("#chk-dung-claude-st").checked;

    $("#khung-kq-san-tap").classList.remove("hidden");
    $("#cot-ai-thuong").classList.toggle("hidden", !soSanh);

    // Reset trạng thái
    $("#st-ar-badge").textContent = "Đang kết nối…";
    $("#st-ar-tien-do").innerHTML = "";
    $("#st-ar-noi-dung").innerHTML = "";
    $("#st-ar-danh-gia").classList.add("hidden");

    if (soSanh) {
      $("#st-thuong-badge").textContent = "Đang kết nối…";
      $("#st-thuong-tien-do").innerHTML = "";
      $("#st-thuong-noi-dung").innerHTML = "";
    }

    $("#nut-chay-san-tap").disabled = true;

    try {
      const r = await TaiKhoan.goi("/admin/api/ai-rieng/san-tap", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ de, so_sanh: soSanh, bo_qua_thu_vien: boQuaTv, dung_claude: dungClaude }),
      });
      const d = await r.json();
      if (!r.ok) {
        toast(d.loi || "Không chạy được Sân tập", "do");
        $("#nut-chay-san-tap").disabled = false;
        return;
      }

      sanTapAiRiengJob = d.ai_rieng;
      sanTapAiThuongJob = d.ai_thuong;

      theoDoiSanTap(sanTapAiRiengJob, "ar", de);
      if (sanTapAiThuongJob) {
        theoDoiSanTap(sanTapAiThuongJob, "thuong", de);
      }
    } catch (e) {
      toast("Lỗi kết nối Sân tập: " + e.message, "do");
      $("#nut-chay-san-tap").disabled = false;
    }
  });

  // Nút Đúng → Lưu thư viện
  $("#nut-st-dung").addEventListener("click", async () => {
    if (!sanTapAiRiengJob) return;
    try {
      const r = await TaiKhoan.goi("/admin/api/ai-rieng/san-tap/dung", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ job_id: sanTapAiRiengJob, chu_de: "" }),
      });
      const d = await r.json();
      if (r.ok) {
        toast("Đã lưu lời giải này thành bài mẫu đã duyệt!", "xanh");
        $("#st-ar-danh-gia").classList.add("hidden");
        taiTongQuan();
      } else {
        toast(d.loi || "Lỗi lưu bài", "do");
      }
    } catch (e) {
      toast("Lỗi kết nối", "do");
    }
  });

  // Nút Sai → Dạy sửa
  $("#nut-st-sai").addEventListener("click", () => {
    if (!sanTapAiRiengJob) return;
    $("#modal-ss-tieu-de").textContent = "Dạy sửa sai từ Sân tập";
    $("#inp-ss-id").value = "";
    $("#inp-ss-bao-sai-id").value = "";
    $("#inp-ss-de").value = $("#inp-de-san-tap").value.trim();
    $("#inp-ss-sai").value = $("#st-ar-noi-dung").innerText.slice(0, 500);
    $("#inp-ss-dung").value = "";
    $("#inp-ss-ly-do").value = "";
    $("#inp-ss-chu-de").value = "";
    $("#modal-sua-sai").showModal();
  });
}

function theoDoiSanTap(jobId, loai, de) {
  const badge = $(`#st-${loai}-badge`);
  const tienDo = $(`#st-${loai}-tien-do`);
  const noiDung = $(`#st-${loai}-noi-dung`);

  const es = new EventSource(`/api/tien-trinh/${jobId}`);

  es.onmessage = (ev) => {
    try {
      const msg = JSON.parse(ev.data);
      if (msg.thong_diep) {
        tienDo.innerHTML += `<div>• ${esc(msg.thong_diep)}</div>`;
        badge.textContent = msg.thong_diep.slice(0, 24);
      }
    } catch (_) {}
  };

  es.addEventListener("ket_thuc", async () => {
    es.close();
    badge.textContent = "Hoàn thành";
    badge.className = "chip chip-xanh";
    $("#nut-chay-san-tap").disabled = false;

    try {
      const r = await TaiKhoan.goi(`/api/ket-qua/${jobId}`);
      if (!r.ok) {
        const err = await r.json();
        noiDung.innerHTML = `<p class="text-gach">${esc(err.detail?.loi || err.detail || "Lỗi giải bài")}</p>`;
        return;
      }
      const kq = await r.json();
      hienThiKetQuaSanTap(kq, loai);
    } catch (e) {
      noiDung.innerHTML = `<p class="text-gach">Lỗi nhận kết quả: ${esc(e.message)}</p>`;
    }
  });

  es.onerror = () => {
    es.close();
    badge.textContent = "Lỗi kết nối";
    badge.className = "chip chip-do";
    $("#nut-chay-san-tap").disabled = false;
  };
}

function hienThiKetQuaSanTap(kq, loai) {
  const c = $(`#st-${loai}-noi-dung`);
  const lg = kq.gon || kq.loi_giai || {};
  const buoc = lg.buoc || lg.cac_buoc || [];

  let html = `<div class="space-y-2">`;

  // Căn cứ AI Riêng
  if (kq.ai_rieng) {
    const ar = kq.ai_rieng;
    html += `
      <div class="p-2 rounded bg-giay border border-chi text-[11px] space-y-1">
        <div class="flex items-center justify-between font-semibold">
          <span class="text-nhan">Tầng giải: ${esc(ar.tang || "kiến thức")}</span>
          <span class="nhan-mono">${esc(kq.mo_hinh || "")}</span>
        </div>
        ${ar.so_muc_tim_thay ? `<p class="text-muc-2">Tìm thấy ${ar.so_muc_tim_thay} quy tắc/đoạn tri thức liên quan</p>` : ""}
      </div>
    `;
  }

  // Từng bước phản ứng
  html += `<div class="space-y-1.5 font-mono text-xs">`;
  buoc.forEach((b, idx) => {
    const canCu = b.can_cu ? `<span class="text-nhan font-bold">[${esc(b.can_cu)}]</span>` : "";
    html += `
      <div class="p-2 rounded border border-chi bg-giay">
        <div class="flex items-center justify-between">
          <span class="font-bold text-muc">Bước ${idx + 1}: ${esc(b.tu || b.chat_dau)} → ${esc(b.den || b.chat_sau)}</span>
          ${canCu}
        </div>
        <p class="text-muc text-[11px] mt-0.5">${esc(b.phuong_trinh || `${b.tu} + ... → ${b.den}`)}</p>
        <p class="text-muc-2 text-[10px]">ĐK: ${esc(b.dieu_kien || "—")}</p>
      </div>
    `;
  });
  html += `</div>`;

  // Cảnh báo bước chưa dạy
  if (lg.chua_duoc_day && lg.chua_duoc_day.length) {
    html += `
      <div class="p-2 rounded bg-giay-lom border border-gach text-[11px] text-gach">
        <p class="font-bold">Bước chưa được dạy trong kho:</p>
        <ul class="list-disc list-inside">${lg.chua_duoc_day.map((x) => `<li>${esc(x)}</li>`).join("")}</ul>
      </div>
    `;
  }

  html += `</div>`;
  c.innerHTML = html;

  if (loai === "ar") {
    $("#st-ar-danh-gia").classList.remove("hidden");
  }
}

// ------------------------------------------------------------ 07. BỘ ĐỀ KIỂM TRA
function khoiTaoBoDe() {
  $("#form-them-ch").addEventListener("submit", async (ev) => {
    ev.preventDefault();
    const de = $("#inp-de-ch").value.trim();
    const daRaw = $("#inp-da-ch").value.trim();
    const chuDe = $("#inp-cd-ch").value.trim();

    // Parse đáp án
    const dapAn = [];
    daRaw.split(/[;\n]/).forEach((p) => {
      const m = p.match(/^\s*([^:=]+)\s*[:=]\s*(.+)\s*$/);
      if (m) dapAn.push({ ki_hieu: m[1].trim(), chat: m[2].trim() });
    });

    if (!dapAn.length) return toast("Đáp án chưa đúng định dạng (vd: A: C2H5OH; B: CH3COOH)", "vang");

    try {
      const r = await TaiKhoan.goi("/admin/api/ai-rieng/bo-de", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ de, dap_an: dapAn, chu_de: chuDe, bat: true }),
      });
      if (r.ok) {
        toast("Đã thêm câu hỏi kiểm tra thành công!", "xanh");
        $("#inp-de-ch").value = "";
        $("#inp-da-ch").value = "";
        taiDsBoDe();
        taiTongQuan();
      } else {
        const e = await r.json();
        toast(e.loi || "Không thêm được câu hỏi", "do");
      }
    } catch (e) {
      toast("Lỗi kết nối", "do");
    }
  });

  // Nhập bộ đề từ file
  $("#nut-nhap-bo-de-file").addEventListener("click", () => $("#inp-file-bo-de").click());
  $("#inp-file-bo-de").addEventListener("change", async (ev) => {
    const f = ev.target.files[0];
    if (!f) return;
    const fd = new FormData();
    fd.append("tep", f);
    toast("Đang nhập câu hỏi kiểm tra từ file...", "tin");
    try {
      const r = await TaiKhoan.goi("/admin/api/ai-rieng/bo-de/nhap", { method: "POST", body: fd });
      const d = await r.json();
      if (r.ok) {
        toast(`Đã nhập ${d.da_nhap} câu hỏi thành công!`, "xanh");
        taiDsBoDe();
        taiTongQuan();
      } else {
        toast(d.loi || "Lỗi nhập file", "do");
      }
    } catch (e) {
      toast("Lỗi kết nối", "do");
    }
    ev.target.value = "";
  });

  // Chạy chấm bộ đề
  const chayKt = async () => {
    if (!confirm("Bắt đầu chạy chấm bộ đề kiểm tra cho AI Riêng? Quá trình sẽ chạy ngầm và chấm tự động bằng RDKit.")) return;
    toast("Đang khởi động đợt kiểm tra...", "tin");
    try {
      const r = await TaiKhoan.goi("/admin/api/ai-rieng/kiem-tra/chay", { method: "POST" });
      const d = await r.json();
      if (r.ok) {
        toast(`Đã bắt đầu kiểm tra đợt #${d.id}! Hệ thống đang chấm...`, "xanh");
        taiDsBoDe();
      } else {
        toast(d.loi || "Lỗi chạy kiểm tra", "do");
      }
    } catch (e) {
      toast("Lỗi kết nối", "do");
    }
  };

  $("#nut-chay-kiem-tra").addEventListener("click", chayKt);
  $("#nut-chay-kt-nhanh").addEventListener("click", () => {
    chonTab("bo-de");
    chayKt();
  });
}

async function taiDsBoDe() {
  const tbody = $("#ds-bo-de-body");
  const tbLichSu = $("#ds-lich-su-kt-body");
  if (!tbody) return;

  try {
    const r = await TaiKhoan.goi("/admin/api/ai-rieng/bo-de");
    if (!r.ok) return;
    const d = await r.json();
    const boDe = d.bo_de || [];
    const lanKt = d.lan || [];

    $("#dem-bo-de").textContent = boDe.length;

    if (!boDe.length) {
      tbody.innerHTML = '<tr><td colspan="5" class="text-center py-4 text-muc-2">Chưa có câu hỏi nào trong bộ đề. Hãy thêm câu hỏi ở form bên trái!</td></tr>';
    } else {
      tbody.innerHTML = boDe.map((ch) => {
        const daStr = (ch.dap_an || []).map((a) => `${a.ki_hieu}: ${a.chat}`).join("; ");
        return `
          <tr class="border-b border-chi">
            <td class="font-mono text-muc font-medium">${esc(ch.de)}</td>
            <td class="font-mono text-nhan text-[11px]">${esc(daStr)}</td>
            <td>${ch.chu_de ? `<span class="chip">${esc(ch.chu_de)}</span>` : "—"}</td>
            <td><span class="${ch.bat ? "text-nhan" : "text-gach"}">${ch.bat ? "●" : "○"}</span></td>
            <td class="text-right">
              <button type="button" class="btn btn-sm btn-do py-0.5 px-2 text-xs" onclick="xoaCauHoi(${ch.id})">Xoá</button>
            </td>
          </tr>
        `;
      }).join("");
    }

    if (tbLichSu) {
      if (!lanKt.length) {
        tbLichSu.innerHTML = '<tr><td colspan="5" class="text-center py-4 text-muc-2">Chưa có lần kiểm tra nào. Bấm "Chạy chấm toàn bộ" để kiểm tra kiến thức AI!</td></tr>';
      } else {
        tbLichSu.innerHTML = lanKt.map((l) => {
          const pt = l.so_cau > 0 ? Math.round((l.so_dung / l.so_cau) * 100) : 0;
          let ttBadge = '<span class="chip chip-xanh">Đã xong</span>';
          if (l.trang_thai === "dang_chay") ttBadge = `<span class="chip chip-vang">Đang chấm (${l.da_cham}/${l.so_cau})</span>`;
          else if (l.trang_thai === "loi") ttBadge = '<span class="chip chip-do">Lỗi</span>';

          const tStr = new Date(l.bat_dau * 1000).toLocaleString("vi-VN");

          return `
            <tr class="border-b border-chi">
              <td>${tStr}</td>
              <td class="font-mono">${l.so_cau}</td>
              <td class="font-mono font-bold ${pt >= 80 ? "text-nhan" : (pt >= 50 ? "text-ho-phach" : "text-gach")}">${l.trang_thai === "xong" ? `${pt}% (${l.so_dung}/${l.so_cau})` : "—"}</td>
              <td>${ttBadge}</td>
              <td class="text-right">
                <button type="button" class="btn btn-sm py-0.5 px-2 text-xs" onclick="xemChiTietKiemTra(${l.id})">Chi tiết</button>
              </td>
            </tr>
          `;
        }).join("");
      }
    }
  } catch (e) {
    console.error("Lỗi tải bộ đề:", e);
  }
}

window.xoaCauHoi = async (cid) => {
  if (!confirm("Xoá câu hỏi này khỏi bộ đề?")) return;
  try {
    const r = await TaiKhoan.goi(`/admin/api/ai-rieng/bo-de/${cid}`, { method: "DELETE" });
    if (r.ok) {
      toast("Đã xoá câu hỏi!", "xanh");
      taiDsBoDe();
      taiTongQuan();
    }
  } catch (e) {
    toast("Lỗi kết nối", "do");
  }
};

window.xemChiTietKiemTra = async (lid) => {
  const modal = $("#modal-kt-chi-tiet");
  const body = $("#modal-kt-ct-body");
  modal.showModal();
  body.innerHTML = '<p class="text-muc-2">Đang tải chi tiết lần kiểm tra…</p>';

  try {
    const r = await TaiKhoan.goi(`/admin/api/ai-rieng/kiem-tra/${lid}`);
    const d = await r.json();
    const chiTiet = d.chi_tiet || [];

    let html = `
      <div class="p-3 rounded-lg bg-giay-lom border border-chi flex items-center justify-between">
        <div>
          <span class="font-semibold text-muc">Tổng kết: </span>
          <span class="font-bold text-nhan">${d.so_dung}/${d.so_cau} câu đúng</span>
        </div>
        <span class="nhan-mono">${d.trang_thai}</span>
      </div>
    `;

    if (d.theo_chu_de && Object.keys(d.theo_chu_de).length) {
      html += `<div class="grid grid-cols-2 sm:grid-cols-3 gap-2">`;
      for (const [cd, sc] of Object.entries(d.theo_chu_de)) {
        html += `
          <div class="p-2 rounded border border-chi bg-giay text-[11px]">
            <p class="font-medium text-muc">${esc(cd || "Chung")}</p>
            <p class="text-muc-2">${sc.dung}/${sc.tong} (${Math.round((sc.dung / sc.tong) * 100)}%)</p>
          </div>
        `;
      }
      html += `</div>`;
    }

    html += `<div class="space-y-2 pt-2">`;
    chiTiet.forEach((ct, i) => {
      const dung = ct.dung;
      html += `
        <div class="p-3 rounded-lg border ${dung ? "border-nhan bg-nhan/5" : "border-gach bg-gach/5"} space-y-1">
          <div class="flex items-center justify-between font-mono font-semibold">
            <span>Câu #${i + 1}: ${esc(ct.de)}</span>
            <span class="${dung ? "text-nhan" : "text-gach"}">${dung ? "✓ ĐÚNG" : "✗ SAI"}</span>
          </div>
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-2 text-[11px] font-mono mt-1">
            <div><span class="text-muc-2">Đáp án chuẩn:</span> ${esc(JSON.stringify(ct.chuan))}</div>
            <div><span class="text-muc-2">AI trả lời:</span> ${esc(JSON.stringify(ct.ai_tra_loi))}</div>
          </div>
          ${ct.loi ? `<p class="text-[11px] text-gach font-mono">Lỗi: ${esc(ct.loi)}</p>` : ""}
        </div>
      `;
    });
    html += `</div>`;

    body.innerHTML = html;
  } catch (err) {
    body.innerHTML = `<p class="text-gach">Lỗi: ${esc(err.message)}</p>`;
  }
};

// ------------------------------------------------------------ 08. SAO LƯU & PHỤC HỒI
function khoiTaoSaoLuu() {
  $("#nut-tai-sao-luu").addEventListener("click", () => {
    window.location.href = "/admin/api/ai-rieng/sao-luu";
  });

  const nutPhucHoi = $("#nut-mo-phuc-hoi");
  const inpPhucHoi = $("#inp-file-phuc-hoi");

  nutPhucHoi.addEventListener("click", () => inpPhucHoi.click());

  inpPhucHoi.addEventListener("change", async (ev) => {
    const f = ev.target.files[0];
    if (!f) return;
    if (!confirm(`Phục hồi kho tri thức từ file "${f.name}"? Dữ liệu mới sẽ được gộp vào kho hiện tại.`)) {
      ev.target.value = "";
      return;
    }

    const fd = new FormData();
    fd.append("tep", f);
    fd.append("thay_the", false);
    fd.append("nhap_cai_dat", true);

    toast("Đang nhập và phục hồi kho tri thức...", "tin");
    try {
      const r = await TaiKhoan.goi("/admin/api/ai-rieng/phuc-hoi", { method: "POST", body: fd });
      const d = await r.json();
      if (r.ok) {
        toast(`Phục hồi thành công! Đã gộp tài liệu, quy tắc và bài mẫu.`, "xanh");
        taiTongQuan();
        taiCaiDat();
      } else {
        toast(d.loi || "Lỗi phục hồi", "do");
      }
    } catch (e) {
      toast("Lỗi kết nối", "do");
    }
    ev.target.value = "";
  });
}

function debounce(fn, ms) {
  let timer;
  return function (...args) {
    clearTimeout(timer);
    timer = setTimeout(() => fn.apply(this, args), ms);
  };
}
