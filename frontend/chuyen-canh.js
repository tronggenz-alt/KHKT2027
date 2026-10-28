// Hiệu ứng chuyển cảnh "QUÉT" dùng chung (một component cho mọi thẻ) – công nghệ nhưng tiết chế:
//   tấm nền màu trang + lưới mờ + 4 góc ngắm + dòng chữ mono, một vạch sáng mảnh màu nhấn.
//   Mở:  nền NỞ RA từ thẻ vừa bấm phủ kín màn hình → đổi nội dung → nền trượt xuống, vạch sáng ở mép trên QUÉT lộ nội dung.
//   Đóng: nền trượt xuống phủ màn hình (vạch sáng ở mép dưới) → đổi nội dung → nền THU VỀ đúng thẻ cũ rồi mờ đi.
//   ChuyenCanh.chuyen(doiNoiDung, { tu: phầnTửNởRa, ve: () => phầnTửThuVề, nhan: "MỞ · Tên khối" }) → Promise<boolean>
//   (false nếu đang có một lượt chạy dở).
// MƯỢT: chỉ animate transform + opacity (Web Animations – chạy trên compositor), nên việc nặng ở luồng chính (dựng khối vừa mở)
// không làm khựng; đổi nội dung xảy ra lúc màn hình đang được phủ kín. Màu / tốc độ: biến --cn-* trong style.css.
// prefers-reduced-motion: không chạy hiệu ứng – nội dung chỉ mờ dần (.hien-dan).
const ChuyenCanh = (() => {
  let lop = null, nen = null, vachTren = null, vachDuoi = null, chu = null;
  let dangChay = false;
  const giamHieuUng = () => window.matchMedia("(prefers-reduced-motion: reduce)").matches;

  function tao() {
    lop = document.createElement("div");
    lop.className = "cn-lop";
    lop.setAttribute("aria-hidden", "true");
    lop.innerHTML = `<div class="cn-nen"><i class="cn-goc tl"></i><i class="cn-goc tr"></i><i class="cn-goc bl"></i><i class="cn-goc br"></i>
      <div class="cn-chu"><span></span><b class="cn-con-tro"></b></div><div class="cn-vach tren"></div><div class="cn-vach duoi"></div></div>`;
    document.body.append(lop);
    nen = lop.querySelector(".cn-nen");
    vachTren = lop.querySelector(".cn-vach.tren");
    vachDuoi = lop.querySelector(".cn-vach.duoi");
    chu = lop.querySelector(".cn-chu");
  }

  const bien = (ten, macDinh) => {
    const v = getComputedStyle(lop).getPropertyValue(ten).trim();
    return v.endsWith("ms") ? parseFloat(v) : v.endsWith("s") ? parseFloat(v) * 1000 : (v || macDinh);
  };
  // transform (origin 0 0) đưa tấm nền toàn màn hình trùng khít khung một phần tử
  function veThe(el) {
    const r = el && el.getBoundingClientRect();
    if (!r || !r.width) return `translate(0px, ${innerHeight / 2}px) scale(1, 0.004)`;
    return `translate(${r.left}px, ${r.top}px) scale(${r.width / innerWidth}, ${r.height / innerHeight})`;
  }
  const chay = (el, khung, thoiGian, them = {}) => {
    const a = el.animate(khung, { duration: thoiGian, easing: bien("--cn-easing", "cubic-bezier(.2,.8,.2,1)"), fill: "forwards", ...them });
    return Promise.race([a.finished.catch(() => {}), new Promise((r) => setTimeout(r, thoiGian + (them.delay || 0) + 400))]);
  };
  const hien = (el, co) => { el.getAnimations().forEach((a) => a.cancel()); el.style.opacity = co ? "1" : "0"; };

  async function chuyen(doiNoiDung, { tu = null, ve = null, nhan = "" } = {}) {
    if (dangChay) return false;
    dangChay = true;
    try {
      if (giamHieuUng()) { doiNoiDung(); return true; }
      if (!lop) tao();
      const tPhu = bien("--cn-phu", 420), tLo = bien("--cn-lo", 460), tDung = bien("--cn-dung", 90);
      chu.querySelector("span").textContent = nhan;
      lop.classList.add("dang-chay");
      [nen, chu, vachTren, vachDuoi].forEach((el) => el.getAnimations().forEach((a) => a.cancel()));
      // 1. PHỦ: mở → nền nở ra từ thẻ (chữ hiện ở cuối); đóng → nền trượt xuống, vạch sáng ở mép dưới
      if (tu) {
        hien(vachTren, false); hien(vachDuoi, false);
        await Promise.all([chay(nen, [{ transform: veThe(tu) }, { transform: "none" }], tPhu),
          chay(chu, [{ opacity: 0 }, { opacity: 0, offset: 0.6 }, { opacity: 1 }], tPhu)]);
      } else {
        hien(vachTren, false); hien(vachDuoi, true); hien(chu, true);
        await chay(nen, [{ transform: "translateY(-100%)" }, { transform: "none" }], tPhu);
      }
      // 2. đổi nội dung khi màn hình đã được phủ kín (dựng khối mới ngay, trong lúc còn che)
      doiNoiDung();
      void document.body.offsetHeight;
      await new Promise((r) => setTimeout(r, tDung));
      // 3. LỘ: đóng → thu về thẻ cũ rồi mờ đi; mở → nền trượt xuống, vạch sáng ở mép trên quét lộ nội dung
      const dich = typeof ve === "function" ? ve() : ve;
      if (dich) {
        hien(vachDuoi, false);
        await Promise.all([chay(nen, [{ transform: "none", opacity: 1 }, { transform: veThe(dich), opacity: 1, offset: 0.8 }, { transform: veThe(dich), opacity: 0 }], tLo),
          chay(chu, [{ opacity: 1 }, { opacity: 0 }], tLo * 0.3)]);
      } else {
        hien(vachDuoi, false); hien(vachTren, true);
        await chay(nen, [{ transform: "none" }, { transform: "translateY(100%)" }], tLo);
      }
      return true;
    } finally {
      if (lop) {
        lop.classList.remove("dang-chay");
        [nen, chu, vachTren, vachDuoi].forEach((el) => el.getAnimations().forEach((a) => a.cancel()));
      }
      dangChay = false;
    }
  }

  return { chuyen, get dangChay() { return dangChay; } };
})();
