// Chuyển bài giảng thành ảnh JPEG 1600×900 ngay trên trình duyệt của giáo viên, trước khi tải lên.
// Bản chạy trên Cloudflare (gói miễn phí) không đủ CPU để đọc PDF, đổi cỡ ảnh ở máy chủ; bản FastAPI vẫn nhận được các ảnh này.
// Mỗi trang thu vừa khung 16:9 trên nền trắng, giữ tỉ lệ, như _khop_kho() trong server/lab/trinh_chieu.py.
(() => {
  const RONG = 1600, CAO = 900, TOI_DA_TRANG = 200;
  const PDFJS = "https://cdnjs.cloudflare.com/ajax/libs/pdf.js/3.11.174/";
  let napPdf = null;

  function taiPdfJs() {
    napPdf = napPdf || new Promise((xong, loi) => {
      const s = document.createElement("script");
      s.src = PDFJS + "pdf.min.js";
      s.onload = () => {
        window.pdfjsLib.GlobalWorkerOptions.workerSrc = PDFJS + "pdf.worker.min.js";
        xong(window.pdfjsLib);
      };
      s.onerror = () => loi(new Error("Không tải được bộ đọc PDF, kiểm tra kết nối mạng"));
      document.head.appendChild(s);
    });
    return napPdf;
  }

  function khung() {
    const c = document.createElement("canvas");
    c.width = RONG;
    c.height = CAO;
    const g = c.getContext("2d");
    g.fillStyle = "#fff";
    g.fillRect(0, 0, RONG, CAO);
    return [c, g];
  }

  // Vẽ nguồn (ảnh hoặc canvas trang PDF) vừa khung, phóng to ảnh nhỏ: màn hình trong phòng ảo rất lớn
  function vuaKhung(nguon, w, h) {
    const [c, g] = khung();
    const ti_le = Math.min(RONG / w, CAO / h);
    const rw = Math.round(w * ti_le), rh = Math.round(h * ti_le);
    g.drawImage(nguon, Math.floor((RONG - rw) / 2), Math.floor((CAO - rh) / 2), rw, rh);
    return new Promise((xong) => c.toBlob(xong, "image/jpeg", 0.85));
  }

  async function trangPdf(tep, them) {
    const pdfjs = await taiPdfJs();
    let pdf;
    try {
      pdf = await pdfjs.getDocument({ data: await tep.arrayBuffer() }).promise;
    } catch {
      throw new Error(`Không đọc được PDF ${tep.name} (hỏng hoặc có mật khẩu)`);
    }
    if (pdf.numPages > TOI_DA_TRANG) throw new Error(`Mỗi bài giảng tối đa ${TOI_DA_TRANG} trang`);
    for (let so = 1; so <= pdf.numPages; so++) {
      const trang = await pdf.getPage(so);
      const goc = trang.getViewport({ scale: 1 });
      const vp = trang.getViewport({ scale: Math.min(RONG / goc.width, CAO / goc.height) });
      const c = document.createElement("canvas");
      c.width = Math.ceil(vp.width);
      c.height = Math.ceil(vp.height);
      const g = c.getContext("2d");
      g.fillStyle = "#fff";
      g.fillRect(0, 0, c.width, c.height);
      // intent "print": vẽ liền một lượt, không chờ requestAnimationFrame, nên giáo viên chuyển sang tab khác vẫn chạy tiếp
      await trang.render({ canvasContext: g, viewport: vp, intent: "print" }).promise;
      await them(await vuaKhung(c, c.width, c.height));
    }
  }

  async function trangAnh(tep, them) {
    let anh;
    try {
      anh = await createImageBitmap(tep);
    } catch {
      throw new Error(`${tep.name} không phải PDF hoặc ảnh dùng được`);
    }
    await them(await vuaKhung(anh, anh.width, anh.height));
  }

  // Biểu mẫu có ten và tep (nhiều tệp) → FormData gồm ten và các trang 001.jpg, 002.jpg... (máy chủ xếp theo tên tệp)
  window.ChuyenTrang = {
    async formData(form, baoTienDo) {
      const ra = new FormData();
      ra.append("ten", form.elements.ten.value);
      const tep = [...form.elements.tep.files].sort((a, b) => (a.name < b.name ? -1 : a.name > b.name ? 1 : 0));
      let dem = 0;
      const them = async (blob) => {
        if (++dem > TOI_DA_TRANG) throw new Error(`Mỗi bài giảng tối đa ${TOI_DA_TRANG} trang`);
        ra.append("tep", blob, String(dem).padStart(3, "0") + ".jpg");
        if (baoTienDo) baoTienDo(dem);
      };
      for (const t of tep) {
        const dau = new Uint8Array(await t.slice(0, 5).arrayBuffer());
        if (String.fromCharCode(...dau) === "%PDF-") await trangPdf(t, them);
        else await trangAnh(t, them);
      }
      if (!dem) throw new Error("Tệp không có trang nào");
      return ra;
    },
  };
})();
