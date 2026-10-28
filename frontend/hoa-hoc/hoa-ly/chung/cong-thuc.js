/**
 * cong-thuc.js - Thành phần hiển thị thẻ công thức, ngăn kéo chứng minh từng bước (HLCongThuc).
 * Hỗ trợ MathJax 3.2.2 offline, phím tắt Esc / ← / →, bẫy focus, ghi nhớ tiến độ qua localStorage.
 */

(function (global) {
  'use strict';

  var CACHE_CONG_THUC = null;
  var DANG_MO = null; // Công thức đang mở trong drawer
  var NUT_KICH_HOAT_TRUOC = null; // Lưu element để trả focus khi đóng

  var HLCongThuc = {
    /**
     * Tải danh sách công thức từ cong-thuc.json
     */
    taiDuLieu: async function () {
      if (CACHE_CONG_THUC) return CACHE_CONG_THUC;
      try {
        var res = await fetch('/static/hoa-hoc/hoa-ly/cong-thuc.json', { cache: 'no-cache' });
        if (!res.ok) throw new Error('Không thể tải cong-thuc.json');
        CACHE_CONG_THUC = await res.json();
        return CACHE_CONG_THUC;
      } catch (err) {
        console.error('Lỗi tải công thức hoá lý:', err);
        return { phien_ban: 1, chu_de: [], cong_thuc: [] };
      }
    },

    /**
     * Cấu hình và gọi render MathJax an toàn cho một phần tử
     */
    renderMath: function (element) {
      if (!element) return;
      if (typeof window.MathJax !== 'undefined' && window.MathJax.typesetPromise) {
        window.MathJax.typesetPromise([element]).catch(function (err) {
          console.warn('Lỗi MathJax typeset:', err);
        });
      }
    },

    /**
     * Tạo phần tử thẻ công thức (Card)
     * @param {object} ct - Dữ liệu một công thức trong cong-thuc.json
     * @param {object} [tuyChon] - Tuỳ chọn { giaTriHienTai: string, moRong: boolean }
     */
    taoTheCongThuc: function (ct, tuyChon) {
      tuyChon = tuyChon || {};
      var card = document.createElement('article');
      card.className = 'hl-the-cong-thuc group relative p-5 rounded-2xl bg-white dark:bg-zinc-900 border border-zinc-200 dark:border-zinc-800 shadow-sm hover:shadow-md transition-all flex flex-col justify-between';
      card.id = 'ct-' + (ct.id || ct.ma);
      card.setAttribute('data-id', ct.ma || ct.id);
      card.setAttribute('data-slug', ct.id);
      card.setAttribute('data-ma', ct.ma);
      card.setAttribute('data-chu-de', ct.chu_de);
      card.setAttribute('data-muc-do', ct.muc_do);

      var mucDoNhan = {
        'sgk': { ten: 'SGK GDPT 2018', lop: 'bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 border-emerald-500/20' },
        'mo-rong': { ten: 'Mở rộng · HSG', lop: 'bg-amber-500/10 text-amber-600 dark:text-amber-400 border-amber-500/20' },
        'dai-hoc': { ten: 'Đại học', lop: 'bg-purple-500/10 text-purple-600 dark:text-purple-400 border-purple-500/20' }
      }[ct.muc_do] || { ten: 'SGK', lop: 'bg-zinc-500/10 text-zinc-600 dark:text-zinc-400 border-zinc-500/20' };

      // Ký hiệu & đơn vị
      var kyHieuHtml = (ct.ky_hieu || []).map(function (kh) {
        return '<li class="flex items-baseline gap-1 text-xs text-zinc-600 dark:text-zinc-400">' +
          '<span class="font-mono font-bold text-zinc-900 dark:text-zinc-100">' + kh.k + '</span>: ' +
          '<span>' + kh.y + (kh.dv ? ' (' + kh.dv + ')' : '') + '</span>' +
        '</li>';
      }).join('');

      var toanBadge = '';
      if (ct.chung_minh && (ct.chung_minh.can_toan === 'tich-phan' || ct.chung_minh.can_toan === 'dao-ham')) {
        toanBadge = '<span class="text-[11px] px-2 py-0.5 rounded-full border border-sky-500/30 bg-sky-500/10 text-sky-600 dark:text-sky-400 font-medium">Cần giải tích Toán 12</span>';
      }

      var linkMoPhong = '';
      if (ct.mo_phong && ct.mo_phong.length > 0) {
        var mp = ct.mo_phong[0];
        linkMoPhong = '/hoa-hoc/hoa-ly/' + mp.id + (mp.query ? '?' + mp.query : '');
      }

      var dongGiaTriHienTai = tuyChon.giaTriHienTai
        ? '<div class="hl-gt-hien-tai text-xs font-mono py-1.5 px-3 rounded-lg bg-sky-500/10 text-sky-700 dark:text-sky-300 border border-sky-500/20 mb-3">' +
            '<span>Đang chạy: </span><strong>' + tuyChon.giaTriHienTai + '</strong>' +
          '</div>'
        : '';

      card.innerHTML =
        '<div>' +
          '<div class="flex items-center justify-between gap-2 mb-2">' +
            '<span class="text-xs font-bold font-mono px-2 py-0.5 rounded bg-zinc-100 dark:bg-zinc-800 text-zinc-700 dark:text-zinc-300 border border-zinc-200 dark:border-zinc-700">' + (ct.ma || ct.id) + '</span>' +
            '<div class="flex items-center gap-1.5 flex-wrap justify-end">' +
              toanBadge +
              '<span class="text-[11px] px-2 py-0.5 rounded-full border ' + mucDoNhan.lop + ' font-medium">' + mucDoNhan.ten + '</span>' +
            '</div>' +
          '</div>' +
          '<h3 class="text-base font-bold text-zinc-900 dark:text-zinc-100 mb-2">' + ct.ten + '</h3>' +
          '<div class="hl-bieu-thuc-lon my-3 p-3 rounded-xl bg-zinc-50 dark:bg-zinc-950/60 border border-zinc-100 dark:border-zinc-800/80 text-center overflow-x-auto select-all">' +
            '<div class="text-lg font-bold text-zinc-900 dark:text-zinc-50">\\(' + ct.latex + '\\)</div>' +
          '</div>' +
          dongGiaTriHienTai +
          (ct.dieu_kien ? '<p class="text-xs text-zinc-500 dark:text-zinc-400 mb-2.5"><em>Điều kiện:</em> ' + ct.dieu_kien + '</p>' : '') +
          (kyHieuHtml ? '<ul class="space-y-1 mb-4 border-t border-zinc-100 dark:border-zinc-800/60 pt-2.5">' + kyHieuHtml + '</ul>' : '') +
        '</div>' +
        '<div class="pt-3 border-t border-zinc-100 dark:border-zinc-800 flex items-center justify-between gap-2 flex-wrap">' +
          '<button type="button" class="hl-btn-chung-minh px-3 py-1.5 rounded-lg text-xs font-semibold bg-sky-500 hover:bg-sky-400 text-white shadow-sm transition-colors flex items-center gap-1.5" data-act="chung-minh">' +
            '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="w-3.5 h-3.5"><path d="M12 20h9M16.5 3.5a2.12 2.12 0 0 1 3 3L7 19l-4 1 1-4Z"/></svg>' +
            '<span>Chứng minh</span>' +
          '</button>' +
          '<div class="flex items-center gap-1">' +
            (ct.vi_du_so ? '<button type="button" class="px-2.5 py-1.5 rounded-lg text-xs font-medium text-zinc-600 dark:text-zinc-400 hover:bg-zinc-100 dark:hover:bg-zinc-800 transition-colors" data-act="vi-du">Ví dụ số</button>' : '') +
            (linkMoPhong ? '<a href="' + linkMoPhong + '" class="px-2.5 py-1.5 rounded-lg text-xs font-medium text-sky-600 dark:text-sky-400 hover:underline flex items-center gap-1"><span>Thử</span><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="w-3 h-3"><path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6M15 3h6v6M10 14L21 3"/></svg></a>' : '') +
          '</div>' +
        '</div>';

      // Xử lý sự kiện bấm nút
      card.querySelector('[data-act="chung-minh"]').addEventListener('click', function (e) {
        NUT_KICH_HOAT_TRUOC = e.currentTarget;
        HLCongThuc.moHopChungMinh(ct);
      });

      var nutViDu = card.querySelector('[data-act="vi-du"]');
      if (nutViDu) {
        nutViDu.addEventListener('click', function (e) {
          NUT_KICH_HOAT_TRUOC = e.currentTarget;
          var buocCuoi = (ct.chung_minh && ct.chung_minh.buoc) ? ct.chung_minh.buoc.length : 1;
          HLCongThuc.moHopChungMinh(ct, { buocMacDinh: buocCuoi, moViDu: true });
        });
      }

      this.renderMath(card);
      return card;
    },

    /**
     * Mở ngăn kéo (Drawer/Modal) chứng minh từng bước cho công thức ct
     */
    moHopChungMinh: function (ct, tuyChon) {
      tuyChon = tuyChon || {};
      DANG_MO = ct;

      var overlay = document.getElementById('hl-hop-cm-overlay');
      if (!overlay) {
        overlay = this.taoKhungHopChungMinh();
      }

      var buocLuu = 1;
      try {
        var luu = localStorage.getItem('hl_cm_' + ct.id);
        if (luu) buocLuu = parseInt(luu, 10) || 1;
      } catch (e) {}

      var buocHienTai = tuyChon.buocMacDinh || buocLuu;

      overlay.classList.remove('hidden');
      var drawer = document.getElementById('hl-hop-cm-drawer');
      if (drawer) {
        drawer.style.transform = 'translateX(0)';
      }

      this.capNhatNoiDungChungMinh(ct, buocHienTai, tuyChon.hienTatCa);

      setTimeout(function () {
        overlay.classList.add('hl-open');
        var btnDong = document.getElementById('hl-cm-btn-dong');
        if (btnDong) btnDong.focus();
      }, 10);

      // Thêm query hash nếu đang ở trang /cong-thuc
      if (window.history && window.history.replaceState) {
        var url = new URL(window.location.href);
        url.searchParams.set('chung-minh', ct.ma || ct.id);
        window.history.replaceState({}, '', url.toString());
      }
    },

    /**
     * Đóng ngăn kéo chứng minh
     */
    dongHopChungMinh: function () {
      var overlay = document.getElementById('hl-hop-cm-overlay');
      if (!overlay) return;
      var drawer = document.getElementById('hl-hop-cm-drawer');
      if (drawer) {
        drawer.style.transform = 'translateX(100%)';
      }
      if (global.HLHinhChungMinh && global.HLHinhChungMinh.donDepTatCa) {
        global.HLHinhChungMinh.donDepTatCa();
      }
      overlay.classList.remove('hl-open');
      setTimeout(function () {
        overlay.classList.add('hidden');
        DANG_MO = null;
        if (NUT_KICH_HOAT_TRUOC && NUT_KICH_HOAT_TRUOC.focus) {
          NUT_KICH_HOAT_TRUOC.focus();
        }
      }, 200);

      if (window.history && window.history.replaceState) {
        var url = new URL(window.location.href);
        url.searchParams.delete('chung-minh');
        window.history.replaceState({}, '', url.toString());
      }
    },

    /**
     * Tạo phần tử DOM của drawer chứng minh một lần duy nhất
     */
    taoKhungHopChungMinh: function () {
      var overlay = document.createElement('div');
      overlay.id = 'hl-hop-cm-overlay';
      overlay.className = 'hl-hop-cm-overlay fixed inset-0 z-50 bg-black/60 backdrop-blur-sm hidden flex justify-end transition-opacity duration-200';
      overlay.setAttribute('role', 'dialog');
      overlay.setAttribute('aria-modal', 'true');
      overlay.setAttribute('aria-label', 'Chứng minh công thức');

      overlay.innerHTML =
        '<div id="hl-hop-cm-drawer" class="hl-hop-cm-drawer w-full sm:w-[560px] max-w-full h-full bg-white dark:bg-zinc-900 border-l border-zinc-200 dark:border-zinc-800 shadow-2xl flex flex-col transform translate-x-full transition-transform duration-250 ease-out">' +
          // Header
          '<div class="p-4 border-b border-zinc-200 dark:border-zinc-800 flex items-start justify-between gap-3 bg-zinc-50 dark:bg-zinc-950">' +
            '<div>' +
              '<div class="flex items-center gap-2 mb-1">' +
                '<span id="hl-cm-ma" class="text-xs font-mono font-bold px-2 py-0.5 rounded bg-sky-500/10 text-sky-600 dark:text-sky-400 border border-sky-500/20"></span>' +
                '<span id="hl-cm-loai" class="text-xs text-zinc-500 dark:text-zinc-400"></span>' +
              '</div>' +
              '<h2 id="hl-cm-tieu-de" class="text-base sm:text-lg font-bold text-zinc-900 dark:text-zinc-100"></h2>' +
            '</div>' +
            '<button type="button" id="hl-cm-btn-dong" class="p-2 rounded-xl text-zinc-400 hover:text-zinc-700 dark:hover:text-zinc-200 hover:bg-zinc-200/60 dark:hover:bg-zinc-800 transition-colors" aria-label="Đóng">' +
              '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="w-5 h-5"><path d="M18 6L6 18M6 6l12 12"/></svg>' +
            '</button>' +
          '</div>' +

          // Progress bar
          '<div class="w-full bg-zinc-100 dark:bg-zinc-800 h-1">' +
            '<div id="hl-cm-tien-do" class="bg-sky-500 h-1 transition-all duration-200" style="width: 0%"></div>' +
          '</div>' +

          // Body cuộn
          '<div id="hl-cm-than" class="flex-1 overflow-y-auto p-5 space-y-4">' +
            // Nội dung các bước sẽ chèn vào đây
          '</div>' +

          // Footer thanh điều hướng
          '<div class="p-4 border-t border-zinc-200 dark:border-zinc-800 bg-zinc-50 dark:bg-zinc-950 flex items-center justify-between gap-2 flex-wrap">' +
            '<div class="flex items-center gap-1.5">' +
              '<button type="button" id="hl-cm-btn-truoc" class="px-3 py-1.5 rounded-lg text-xs font-semibold border border-zinc-200 dark:border-zinc-700 hover:bg-zinc-100 dark:hover:bg-zinc-800 text-zinc-700 dark:text-zinc-200 transition-colors">‹ Trước</button>' +
              '<span id="hl-cm-chi-so-buoc" class="text-xs font-medium text-zinc-500 dark:text-zinc-400 px-1"></span>' +
              '<button type="button" id="hl-cm-btn-sau" class="px-3 py-1.5 rounded-lg text-xs font-semibold bg-sky-500 hover:bg-sky-400 text-white transition-colors">Tiếp ›</button>' +
            '</div>' +
            '<div class="flex items-center gap-1.5">' +
              '<button type="button" id="hl-cm-btn-tat-ca" class="px-2.5 py-1.5 rounded-lg text-xs font-medium text-zinc-600 dark:text-zinc-400 hover:bg-zinc-200/60 dark:hover:bg-zinc-800 transition-colors">Hiện tất cả</button>' +
              '<button type="button" id="hl-cm-btn-y-chinh" class="px-2.5 py-1.5 rounded-lg text-xs font-medium text-zinc-600 dark:text-zinc-400 hover:bg-zinc-200/60 dark:hover:bg-zinc-800 transition-colors">Ý chính</button>' +
            '</div>' +
          '</div>' +
        '</div>';

      document.body.appendChild(overlay);

      // Bắt sự kiện phím Esc và click nền để đóng
      overlay.addEventListener('click', function (e) {
        if (e.target === overlay) {
          HLCongThuc.dongHopChungMinh();
        }
      });

      document.getElementById('hl-cm-btn-dong').addEventListener('click', function () {
        HLCongThuc.dongHopChungMinh();
      });

      document.addEventListener('keydown', function (e) {
        if (!DANG_MO) return;
        if (e.key === 'Escape') {
          e.preventDefault();
          HLCongThuc.dongHopChungMinh();
        } else if (e.key === 'ArrowRight') {
          var btnSau = document.getElementById('hl-cm-btn-sau');
          if (btnSau && !btnSau.disabled) btnSau.click();
        } else if (e.key === 'ArrowLeft') {
          var btnTruoc = document.getElementById('hl-cm-btn-truoc');
          if (btnTruoc && !btnTruoc.disabled) btnTruoc.click();
        }
      });

      return overlay;
    },

    /**
     * Cập nhật nội dung hiển thị trong Drawer chứng minh
     */
    capNhatNoiDungChungMinh: function (ct, buocIndex, hienTatCa, cheDoCach) {
      cheDoCach = cheDoCach || 1;
      var cm = ct.chung_minh || {};
      var buocList = (cheDoCach === 2 && cm.cach_2) ? (cm.cach_2.buoc || []) : (cm.buoc || []);
      var tongBuoc = buocList.length;

      buocIndex = Math.max(1, Math.min(buocIndex || 1, tongBuoc));

      // Dọn dẹp các chuyển động rAF trước đó để tránh rò rỉ bộ nhớ
      if (global.HLHinhChungMinh && global.HLHinhChungMinh.donDepTatCa) {
        global.HLHinhChungMinh.donDepTatCa();
      }

      // Lưu lại vào localStorage
      try {
        localStorage.setItem('hl_cm_' + ct.id, buocIndex);
      } catch (e) {}

      document.getElementById('hl-cm-ma').textContent = ct.ma || ct.id;
      document.getElementById('hl-cm-tieu-de').textContent = ct.ten;

      var loaiText = {
        'chung-minh': 'Chứng minh toán học',
        'suy-luan-mo-hinh': 'Suy luận mô hình vật lý',
        'dinh-luat-thuc-nghiem': 'Định luật thực nghiệm / Tiên đề'
      }[cm.loai] || 'Chứng minh';
      document.getElementById('hl-cm-loai').textContent = loaiText + (cheDoCach === 2 ? ' (Cách 2)' : '');

      var progressBar = document.getElementById('hl-cm-tien-do');
      if (progressBar) {
        var pct = hienTatCa ? 100 : Math.round((buocIndex / tongBuoc) * 100);
        progressBar.style.width = pct + '%';
      }

      var chiSoEl = document.getElementById('hl-cm-chi-so-buoc');
      if (chiSoEl) {
        chiSoEl.textContent = hienTatCa ? 'Toàn bộ' : ('Bước ' + buocIndex + ' / ' + tongBuoc);
      }

      var btnTruoc = document.getElementById('hl-cm-btn-truoc');
      var btnSau = document.getElementById('hl-cm-btn-sau');
      if (btnTruoc) btnTruoc.disabled = hienTatCa || (buocIndex <= 1);
      if (btnSau) btnSau.disabled = hienTatCa || (buocIndex >= tongBuoc);

      var thanEl = document.getElementById('hl-cm-than');
      thanEl.innerHTML = '';

      // Thẻ mục tiêu "Ta cần chứng minh ..."
      var headerDich = document.createElement('div');
      headerDich.className = 'p-3.5 rounded-xl bg-zinc-50 dark:bg-zinc-950 border border-zinc-200 dark:border-zinc-800 text-center';
      var nhacText = cm.loai === 'dinh-luat-thuc-nghiem'
        ? 'Ý nghĩa & cách kiểm chứng bằng mô phỏng:'
        : 'Biểu thức cần chứng minh:';
      headerDich.innerHTML =
        '<p class="text-xs font-medium text-zinc-500 dark:text-zinc-400 mb-1">' + nhacText + '</p>' +
        '<div class="text-lg font-bold text-zinc-900 dark:text-zinc-100">\\(' + ct.latex + '\\)</div>';
      thanEl.appendChild(headerDich);

      var self = this;

      // Nếu có cách 2, hiển thị tab chuyển cách mượt mà
      if (cm.cach_2) {
        var tabCach = document.createElement('div');
        tabCach.className = 'flex border-b border-zinc-200 dark:border-zinc-800 text-xs font-semibold gap-4 my-2';
        var active1 = cheDoCach === 1 ? 'border-b-2 border-sky-500 text-sky-600 dark:text-sky-400 font-bold' : 'text-zinc-500 dark:text-zinc-400 hover:text-zinc-700 dark:hover:text-zinc-200';
        var active2 = cheDoCach === 2 ? 'border-b-2 border-sky-500 text-sky-600 dark:text-sky-400 font-bold' : 'text-zinc-500 dark:text-zinc-400 hover:text-zinc-700 dark:hover:text-zinc-200';

        tabCach.innerHTML =
          '<button type="button" class="hl-btn-tab-cach pb-1.5 ' + active1 + '" id="hl-btn-tab-cach-1" data-cach="1">Cách 1 (Chính)</button>' +
          '<button type="button" class="hl-btn-tab-cach pb-1.5 ' + active2 + '" id="hl-btn-tab-cach-2" data-cach="2">' + cm.cach_2.ten + '</button>';
        thanEl.appendChild(tabCach);

        var btnTab1 = tabCach.querySelector('#hl-btn-tab-cach-1');
        var btnTab2 = tabCach.querySelector('#hl-btn-tab-cach-2');
        if (btnTab1) {
          btnTab1.addEventListener('click', function () {
            self.capNhatNoiDungChungMinh(ct, 1, false, 1);
          });
        }
        if (btnTab2) {
          btnTab2.addEventListener('click', function () {
            self.capNhatNoiDungChungMinh(ct, 1, false, 2);
          });
        }
      }

      // Danh sách bước cần hiển thị
      var danhSachBuocHien = hienTatCa
        ? buocList.map(function (b, i) { return { b: b, idx: i + 1 }; })
        : [{ b: buocList[buocIndex - 1], idx: buocIndex }];

      danhSachBuocHien.forEach(function (item) {
        var b = item.b;
        if (!b) return;
        var stepCard = document.createElement('div');
        stepCard.className = 'p-4 rounded-xl bg-zinc-50 dark:bg-zinc-950 border border-zinc-200 dark:border-zinc-800 space-y-2.5 transition-all';

        stepCard.innerHTML =
          '<div class="flex items-center justify-between text-xs font-bold text-zinc-400">' +
            '<span>Bước ' + item.idx + ' / ' + tongBuoc + (cheDoCach === 2 ? ' (Cách 2)' : '') + '</span>' +
          '</div>' +
          '<p class="text-sm leading-relaxed text-zinc-700 dark:text-zinc-300">' + (b.loi || b.loai || '') + '</p>' +
          (b.latex ? '<div class="hl-latex-buoc text-center font-bold text-base py-2.5 px-3 rounded-lg bg-white dark:bg-zinc-900 border border-zinc-100 dark:border-zinc-850 overflow-x-auto text-zinc-900 dark:text-zinc-100">\\(' + b.latex + '\\)</div>' : '');

        // Chèn hình chuyển động 2D đồng bộ theo từng bước
        var hinhId = b.hinh || cm.hinh || ct.ma;
        if (hinhId && global.HLHinhChungMinh) {
          var hinhEl = global.HLHinhChungMinh.taoHinh(hinhId, { ct: ct, buocIndex: item.idx, tongBuoc: tongBuoc });
          stepCard.appendChild(hinhEl);
        }

        thanEl.appendChild(stepCard);
      });

      // Ý chính (nếu đang ở bước cuối hoặc xem tất cả)
      if ((buocIndex === tongBuoc || hienTatCa) && cm.y_chinh) {
        var yChinhBox = document.createElement('div');
        yChinhBox.className = 'p-3.5 rounded-xl bg-sky-500/10 border border-sky-500/20 text-xs text-sky-800 dark:text-sky-300 space-y-1';
        yChinhBox.innerHTML =
          '<div class="font-bold flex items-center gap-1.5"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="w-4 h-4"><circle cx="12" cy="12" r="10"/><path d="M12 16v-4M12 8h.01"/></svg><span>Ý chính cốt lõi:</span></div>' +
          '<p class="leading-relaxed">' + cm.y_chinh + '</p>';
        thanEl.appendChild(yChinhBox);
      }

      // Ô kiểm tra bằng số & Nút thử trong mô phỏng (ở bước cuối hoặc xem tất cả)
      if ((buocIndex === tongBuoc || hienTatCa) && ct.vi_du_so) {
        var viDuBox = this.taoOKiemTraSo(ct);
        thanEl.appendChild(viDuBox);
      }

      // Gắn sự kiện nút Trước / Sau / Tất cả / Ý chính
      btnTruoc.onclick = function () {
        self.capNhatNoiDungChungMinh(ct, buocIndex - 1, false, cheDoCach);
      };
      btnSau.onclick = function () {
        self.capNhatNoiDungChungMinh(ct, buocIndex + 1, false, cheDoCach);
      };
      document.getElementById('hl-cm-btn-tat-ca').onclick = function () {
        self.capNhatNoiDungChungMinh(ct, buocIndex, !hienTatCa, cheDoCach);
      };
      document.getElementById('hl-cm-btn-y-chinh').onclick = function () {
        self.capNhatNoiDungChungMinh(ct, tongBuoc, false, cheDoCach);
      };

      this.renderMath(thanEl);
    },

    /**
     * Tạo ô kiểm tra bằng số tương tác trực tiếp
     * Cho phép học sinh thay đổi số đầu vào và tính toán kết quả theo thời gian thực
     */
    taoOKiemTraSo: function (ct) {
      var vd = ct.vi_du_so;
      var box = document.createElement('div');
      box.className = 'p-4 rounded-xl bg-emerald-500/10 border border-emerald-500/20 text-xs space-y-3';

      var hamStr = vd.ham;
      var fn = null;
      try {
        var parts = hamStr.split('.');
        var cur = window;
        for (var i = 0; i < parts.length; i++) {
          if (cur) cur = cur[parts[i]];
        }
        if (typeof cur === 'function') fn = cur;
      } catch (err) {
        console.warn('Lỗi tìm hàm vi_du_so:', err);
      }

      var thamSoHienTai = Object.assign({}, vd.vao || {});

      var header = document.createElement('div');
      header.className = 'flex items-center justify-between font-bold text-emerald-800 dark:text-emerald-300';
      header.innerHTML =
        '<span class="flex items-center gap-1.5"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="w-4 h-4"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"/><polyline points="22 4 12 14.01 9 11.01"/></svg><span>Kiểm tra bằng số (Hàm: ' + hamStr + ')</span></span>' +
        '<span id="hl-kt-badge" class="font-mono text-[11px] px-2 py-0.5 rounded bg-emerald-500/20 text-emerald-700 dark:text-emerald-300">Khớp số liệu mẫu ✓</span>';
      box.appendChild(header);

      // Lưới các ô input số liệu đầu vào
      var gridInputs = document.createElement('div');
      gridInputs.className = 'grid grid-cols-2 sm:grid-cols-3 gap-2.5 p-3 rounded-xl bg-white/90 dark:bg-zinc-900/90 border border-zinc-200/80 dark:border-zinc-800';

      var keys = Object.keys(vd.vao || {});
      var inputEls = {};

      keys.forEach(function (k) {
        var val = vd.vao[k];
        var item = document.createElement('div');
        item.className = 'flex flex-col gap-1';
        item.innerHTML =
          '<label class="text-[11px] font-mono font-bold text-zinc-600 dark:text-zinc-400" for="hl-kt-inp-' + k + '">' + k + ':</label>' +
          '<input type="number" id="hl-kt-inp-' + k + '" data-param="' + k + '" step="any" class="hl-kt-inp w-full px-2 py-1 text-xs font-mono rounded-lg bg-zinc-50 dark:bg-zinc-800 border border-zinc-200 dark:border-zinc-700 text-zinc-900 dark:text-zinc-100 focus:outline-none focus:ring-1 focus:ring-emerald-500" value="' + val + '">';
        var inp = item.querySelector('input');
        inputEls[k] = inp;
        gridInputs.appendChild(item);
      });
      box.appendChild(gridInputs);

      // Hộp hiển thị kết quả tính toán tức thì
      var kqBox = document.createElement('div');
      kqBox.className = 'p-2.5 rounded-lg bg-white/80 dark:bg-zinc-900/80 font-mono text-zinc-800 dark:text-zinc-200 flex items-center justify-between gap-2 flex-wrap';
      kqBox.innerHTML =
        '<div>Kết quả tính: <strong id="hl-kt-kq" class="text-emerald-600 dark:text-emerald-400 text-sm">--</strong>' +
        ' <span class="text-zinc-500 text-xs">(Kỳ vọng mẫu: ' + vd.ra + ' ' + (vd.dv || '') + ')</span></div>' +
        '<button type="button" id="hl-kt-btn-reset" class="px-2 py-1 text-[11px] rounded bg-zinc-100 dark:bg-zinc-800 hover:bg-zinc-200 dark:hover:bg-zinc-700 text-zinc-600 dark:text-zinc-300 font-sans font-medium transition-colors">Đặt lại số mẫu</button>';
      box.appendChild(kqBox);

      function tinhLai() {
        keys.forEach(function (k) {
          var num = parseFloat(inputEls[k].value);
          thamSoHienTai[k] = isNaN(num) ? 0 : num;
        });

        var ketQua = null;
        if (typeof fn === 'function') {
          try {
            ketQua = fn(thamSoHienTai);
          } catch (e) {
            console.warn('Lỗi tính hàm vi_du_so:', e);
          }
        }

        var kqEl = box.querySelector('#hl-kt-kq');
        var badgeEl = box.querySelector('#hl-kt-badge');
        if (ketQua !== null && typeof ketQua === 'number') {
          var dinhDangText = (typeof global.HLDinhDang !== 'undefined' && global.HLDinhDang.so)
            ? global.HLDinhDang.so(ketQua, 3)
            : (Number.isInteger(ketQua) ? ketQua.toString() : ketQua.toFixed(2));
          kqEl.textContent = dinhDangText + ' ' + (vd.dv || '');

          var saiSo = Math.abs(ketQua - vd.ra);
          var laMau = keys.every(function (k) { return thamSoHienTai[k] === vd.vao[k]; });
          if (laMau && saiSo <= (vd.sai_so || 0.05)) {
            badgeEl.textContent = 'Khớp số liệu mẫu ✓';
            badgeEl.className = 'font-mono text-[11px] px-2 py-0.5 rounded bg-emerald-500/20 text-emerald-700 dark:text-emerald-300';
          } else {
            badgeEl.textContent = 'Đang thử nghiệm ⚡';
            badgeEl.className = 'font-mono text-[11px] px-2 py-0.5 rounded bg-sky-500/20 text-sky-700 dark:text-sky-300';
          }
        } else {
          kqEl.textContent = vd.ra + ' ' + (vd.dv || '');
        }
      }

      keys.forEach(function (k) {
        inputEls[k].addEventListener('input', tinhLai);
      });

      var btnReset = box.querySelector('#hl-kt-btn-reset');
      if (btnReset) {
        btnReset.addEventListener('click', function () {
          keys.forEach(function (k) {
            inputEls[k].value = vd.vao[k];
          });
          tinhLai();
        });
      }

      // Link mô phỏng nếu có
      if (ct.mo_phong && ct.mo_phong.length > 0) {
        var mp = ct.mo_phong[0];
        var linkMoPhong = '/hoa-hoc/hoa-ly/' + mp.id + (mp.query ? '?' + mp.query : '');
        var linkBox = document.createElement('div');
        linkBox.className = 'pt-1 flex justify-end';
        linkBox.innerHTML = '<a href="' + linkMoPhong + '" class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white font-semibold transition-colors shadow-sm"><span>Thử ngay trong mô phỏng</span><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="w-3.5 h-3.5"><path d="M5 12h14M12 5l7 7-7 7"/></svg></a>';
        box.appendChild(linkBox);
      }

      tinhLai();
      return box;
    }
  };

  if (typeof module !== 'undefined' && module.exports) {
    module.exports = HLCongThuc;
  }
  global.HLCongThuc = HLCongThuc;
})(typeof window !== 'undefined' ? window : global);
