/**
 * frontend/chuyen-dong.js
 * Quản lý chuyển động giao diện (Animate UI) cho CHEMISTRY.
 * Sử dụng token Animate UI (springs, curves, scales, blur, shift) kết hợp thư viện Motion.
 * Tôn trọng hoàn toàn prefers-reduced-motion: không di chuyển vị trí, chỉ mờ/hiện nhanh.
 */

// Kiểm tra prefers-reduced-motion
export function giamChuyenDong() {
  if (typeof window === 'undefined' || !window.matchMedia) return false;
  return window.matchMedia('(prefers-reduced-motion: reduce)').matches;
}

// Token chuẩn Animate UI
export const TOKENS = {
  spring: {
    press: { stiffness: 550, damping: 30, restSpeed: 10 },
    pop: { stiffness: 400, damping: 29 },
    dialog: { stiffness: 150, damping: 25 },
    toast: { stiffness: 280, damping: 24 },
    selection: { stiffness: 300, damping: 30 },
    highlight: { stiffness: 500, damping: 40 },
    morph: { stiffness: 660, damping: 52 },
    slide: { stiffness: 300, damping: 35, restDelta: 0.01 }
  },
  ease: {
    out: [0.23, 1, 0.32, 1],
    soft: [0.215, 0.61, 0.355, 1],
    drawer: [0.34, 1.2, 0.64, 1],
    inOut: [0.77, 0, 0.175, 1],
    away: [0.33, 0.17, 0.67, 0.5],
    color: [0.25, 0.1, 0.25, 1]
  },
  transition: {
    menu: { duration: 0.22, ease: [0.215, 0.61, 0.355, 1] },
    fade: { duration: 0.20, ease: [0.23, 1, 0.32, 1] },
    overlay: { duration: 0.40, ease: [0.215, 0.61, 0.355, 1] },
    dismiss: { duration: 0.20, ease: [0.33, 0.17, 0.67, 0.5] },
    swap: { duration: 0.15, ease: [0.23, 1, 0.32, 1] },
    sheet: { duration: 0.35, ease: [0.215, 0.61, 0.355, 1] }
  },
  scale: {
    press: 0.97,
    choice: 0.985,
    popover: 0.8,
    dialog: 0.8,
    menu: 0.9,
    toast: 0.96,
    icon: 0.5
  },
  blur: {
    surface: '4px',
    panel: '5px',
    icon: '2px'
  }
};

/**
 * Hiệu ứng hiện ra (Surface / Dialog / Tooltip / Bong bóng)
 */
export function hienRa(el, options = {}) {
  if (!el) return Promise.resolve();
  const kieu = options.kieu || 'pop'; // 'pop', 'dialog', 'fade'
  const reduced = giamChuyenDong();

  if (reduced) {
    el.style.display = options.display || '';
    el.style.opacity = '1';
    el.style.transform = 'none';
    return Promise.resolve();
  }

  el.style.display = options.display || '';
  el.style.opacity = '0';

  if (kieu === 'dialog') {
    el.style.transform = `scale(${TOKENS.scale.dialog})`;
    el.style.transition = `transform ${TOKENS.transition.overlay.duration}s cubic-bezier(${TOKENS.ease.soft.join(',')}), opacity ${TOKENS.transition.fade.duration}s ease`;
  } else if (kieu === 'pop') {
    el.style.transform = `scale(${TOKENS.scale.popover})`;
    el.style.transition = `transform 0.25s cubic-bezier(0.34, 1.3, 0.64, 1), opacity ${TOKENS.transition.fade.duration}s ease`;
  } else {
    el.style.transition = `opacity ${TOKENS.transition.fade.duration}s ease`;
  }

  // Force reflow
  void el.offsetWidth;

  requestAnimationFrame(() => {
    el.style.opacity = '1';
    el.style.transform = 'scale(1)';
  });

  return new Promise((res) => setTimeout(res, 250));
}

/**
 * Hiệu ứng ẩn đi (Dismiss)
 */
export function anDi(el, options = {}) {
  if (!el) return Promise.resolve();
  const reduced = giamChuyenDong();

  if (reduced) {
    el.style.opacity = '0';
    el.style.display = 'none';
    return Promise.resolve();
  }

  el.style.transition = `transform ${TOKENS.transition.dismiss.duration}s cubic-bezier(${TOKENS.ease.away.join(',')}), opacity ${TOKENS.transition.dismiss.duration}s ease`;
  el.style.opacity = '0';
  el.style.transform = `scale(${TOKENS.scale.menu})`;

  return new Promise((res) => {
    setTimeout(() => {
      el.style.display = 'none';
      res();
    }, TOKENS.transition.dismiss.duration * 1000);
  });
}

/**
 * Phản hồi khi nhấn nút (Press feedback <= 120ms)
 */
export function nhanNut(el) {
  if (!el) return;
  if (giamChuyenDong()) return;

  el.style.transition = 'transform 0.08s ease';
  el.style.transform = `scale(${TOKENS.scale.press})`;

  setTimeout(() => {
    el.style.transform = 'scale(1)';
  }, 90);
}

/**
 * Trượt vào từ cạnh màn hình (Sheet / Panel / Drawer)
 */
export function truotVao(el, huong = 'phai', options = {}) {
  if (!el) return Promise.resolve();
  const reduced = giamChuyenDong();

  if (reduced) {
    el.style.display = options.display || '';
    el.style.opacity = '1';
    el.style.transform = 'none';
    return Promise.resolve();
  }

  el.style.display = options.display || '';
  el.style.opacity = '0';

  let startTransform = 'translateX(100%)';
  if (huong === 'trai') startTransform = 'translateX(-100%)';
  else if (huong === 'duoi') startTransform = 'translateY(100%)';
  else if (huong === 'tren') startTransform = 'translateY(-100%)';

  el.style.transform = startTransform;
  el.style.transition = `transform ${TOKENS.transition.sheet.duration}s cubic-bezier(${TOKENS.ease.soft.join(',')}), opacity ${TOKENS.transition.fade.duration}s ease`;

  void el.offsetWidth;

  requestAnimationFrame(() => {
    el.style.opacity = '1';
    el.style.transform = 'translate(0, 0)';
  });

  return new Promise((res) => setTimeout(res, TOKENS.transition.sheet.duration * 1000));
}

/**
 * Chỉ báo tab di chuyển trơn tru (Spring Selection / Highlight)
 */
export function chiBaoTab(chiBaoEl, targetTabEl) {
  if (!chiBaoEl || !targetTabEl) return;
  const rect = targetTabEl.getBoundingClientRect();
  const parentRect = targetTabEl.parentElement.getBoundingClientRect();

  const left = rect.left - parentRect.left;
  const width = rect.width;

  if (giamChuyenDong()) {
    chiBaoEl.style.transition = 'none';
    chiBaoEl.style.left = `${left}px`;
    chiBaoEl.style.width = `${width}px`;
    return;
  }

  chiBaoEl.style.transition = `left 0.22s cubic-bezier(${TOKENS.ease.soft.join(',')}), width 0.22s cubic-bezier(${TOKENS.ease.soft.join(',')})`;
  chiBaoEl.style.left = `${left}px`;
  chiBaoEl.style.width = `${width}px`;
}
