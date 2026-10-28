/**
 * cau-hoi.js - Thành phần hiển thị ngân hàng câu hỏi 3 dạng THPT
 * Dạng I (4 lựa chọn) · Dạng II (Đúng/Sai 4 ý) · Dạng III (Trả lời ngắn)
 * UMD: Chạy được trong Node.js (test) và trình duyệt (window.HLCauHoi)
 */
(function (root, factory) {
  if (typeof define === 'function' && define.amd) {
    define([], factory);
  } else if (typeof module === 'object' && module.exports) {
    module.exports = factory();
  } else {
    root.HLCauHoi = factory();
  }
})(typeof self !== 'undefined' ? self : this, function () {
  'use strict';

  function TrinhHienThiCauHoi(container, duLieuCauHoi, options) {
    this.container = container;
    this.duLieu = duLieuCauHoi || [];
    this.options = options || {};
    this.onNapThongSo = this.options.onNapThongSo || null;
    this.trangThaiTraLoi = {}; // lưu câu trả lời của người dùng

    if (this.container && typeof document !== 'undefined') {
      this.render();
    }
  }

  TrinhHienThiCauHoi.prototype.render = function () {
    if (!this.container) return;
    this.container.innerHTML = '';

    if (!this.duLieu || this.duLieu.length === 0) {
      this.container.innerHTML = '<div class="text-sm text-zinc-500 py-4 text-center">Chưa có câu hỏi nào.</div>';
      return;
    }

    var self = this;
    for (var i = 0; i < this.duLieu.length; i++) {
      var ch = this.duLieu[i];
      var card = document.createElement('div');
      card.className = 'hl-card p-4 rounded-xl border border-zinc-200 dark:border-zinc-800 bg-white dark:bg-zinc-900/50 mb-4 shadow-sm';
      card.setAttribute('data-cau-hoi-id', ch.id || (i + 1));

      var header = document.createElement('div');
      header.className = 'flex items-center justify-between mb-2';

      var badge = document.createElement('span');
      badge.className = 'text-xs font-semibold px-2 py-0.5 rounded ' + (
        ch.dang === 'dang_1' ? 'bg-blue-100 text-blue-700 dark:bg-blue-950 dark:text-blue-300' :
        ch.dang === 'dang_2' ? 'bg-amber-100 text-amber-700 dark:bg-amber-950 dark:text-amber-300' :
        'bg-emerald-100 text-emerald-700 dark:bg-emerald-950 dark:text-emerald-300'
      );
      badge.textContent = ch.dang === 'dang_1' ? 'Dạng I: 4 Lựa chọn' :
                          ch.dang === 'dang_2' ? 'Dạng II: Đúng / Sai' :
                          'Dạng III: Trả lời ngắn';

      var idLabel = document.createElement('span');
      idLabel.className = 'text-xs text-zinc-400 font-mono';
      idLabel.textContent = 'Câu ' + (i + 1);

      header.appendChild(badge);
      header.appendChild(idLabel);
      card.appendChild(header);

      var deBai = document.createElement('div');
      deBai.className = 'text-sm font-medium text-zinc-800 dark:text-zinc-200 mb-3 leading-relaxed';
      deBai.innerHTML = ch.de_bai;
      card.appendChild(deBai);

      // Render theo dạng
      if (ch.dang === 'dang_1') {
        this._renderDang1(card, ch, i);
      } else if (ch.dang === 'dang_2') {
        this._renderDang2(card, ch, i);
      } else if (ch.dang === 'dang_3') {
        this._renderDang3(card, ch, i);
      }

      this.container.appendChild(card);
    }
  };

  TrinhHienThiCauHoi.prototype._renderDang1 = function (card, ch, idx) {
    var self = this;
    var form = document.createElement('div');
    form.className = 'space-y-2 mb-3';

    var letters = ['A', 'B', 'C', 'D'];
    ch.lua_chon.forEach(function (opt, oIdx) {
      var btn = document.createElement('button');
      btn.className = 'w-full text-left text-xs p-2.5 rounded-lg border border-zinc-200 dark:border-zinc-800 hover:border-sky-500 dark:hover:border-sky-400 transition-colors flex items-center gap-2';
      btn.innerHTML = '<strong class="text-zinc-400 font-mono w-4">' + letters[oIdx] + '.</strong> <span>' + opt + '</span>';

      btn.onclick = function () {
        var isCorrect = letters[oIdx] === ch.dap_an;
        btn.classList.remove('border-zinc-200', 'dark:border-zinc-800');
        if (isCorrect) {
          btn.classList.add('border-emerald-500', 'bg-emerald-50', 'dark:bg-emerald-950/30', 'text-emerald-700', 'dark:text-emerald-300');
        } else {
          btn.classList.add('border-red-500', 'bg-red-50', 'dark:bg-red-950/30', 'text-red-700', 'dark:text-red-300');
        }
        self._hienGiaiThich(card, ch);
      };
      form.appendChild(btn);
    });
    card.appendChild(form);
  };

  TrinhHienThiCauHoi.prototype._renderDang2 = function (card, ch, idx) {
    var self = this;
    var table = document.createElement('div');
    table.className = 'space-y-2 mb-3';

    var yDungSai = ch.y_dung_sai || [];
    var traLoi = {};

    yDungSai.forEach(function (y, yIdx) {
      var row = document.createElement('div');
      row.className = 'flex items-start justify-between gap-3 p-2 rounded-lg bg-zinc-50 dark:bg-zinc-800/40 text-xs';

      var noiDung = document.createElement('div');
      noiDung.className = 'flex-1 text-zinc-700 dark:text-zinc-300';
      noiDung.innerHTML = '<strong>' + y.ky_hieu + ')</strong> ' + y.noi_dung;

      var btnGroup = document.createElement('div');
      btnGroup.className = 'flex items-center gap-1 shrink-0';

      var btnD = document.createElement('button');
      btnD.className = 'px-2 py-1 rounded text-xs border border-zinc-300 dark:border-zinc-700 hover:bg-zinc-200 dark:hover:bg-zinc-700';
      btnD.textContent = 'Đ';

      var btnS = document.createElement('button');
      btnS.className = 'px-2 py-1 rounded text-xs border border-zinc-300 dark:border-zinc-700 hover:bg-zinc-200 dark:hover:bg-zinc-700';
      btnS.textContent = 'S';

      btnD.onclick = function () {
        traLoi[y.ky_hieu] = true;
        btnD.className = 'px-2 py-1 rounded text-xs bg-sky-600 text-white font-bold';
        btnS.className = 'px-2 py-1 rounded text-xs border border-zinc-300 dark:border-zinc-700';
        kiemTra();
      };

      btnS.onclick = function () {
        traLoi[y.ky_hieu] = false;
        btnS.className = 'px-2 py-1 rounded text-xs bg-sky-600 text-white font-bold';
        btnD.className = 'px-2 py-1 rounded text-xs border border-zinc-300 dark:border-zinc-700';
        kiemTra();
      };

      btnGroup.appendChild(btnD);
      btnGroup.appendChild(btnS);
      row.appendChild(noiDung);
      row.appendChild(btnGroup);
      table.appendChild(row);
    });

    function kiemTra() {
      if (Object.keys(traLoi).length === yDungSai.length) {
        self._hienGiaiThich(card, ch);
      }
    }

    card.appendChild(table);
  };

  TrinhHienThiCauHoi.prototype._renderDang3 = function (card, ch, idx) {
    var self = this;
    var box = document.createElement('div');
    box.className = 'flex items-center gap-2 mb-3';

    var input = document.createElement('input');
    input.type = 'text';
    input.placeholder = 'Nhập đáp án số...';
    input.className = 'flex-1 text-xs px-3 py-2 rounded-lg border border-zinc-300 dark:border-zinc-700 bg-white dark:bg-zinc-800 text-zinc-900 dark:text-zinc-100 font-mono';

    var btnSubmit = document.createElement('button');
    btnSubmit.className = 'px-3 py-2 rounded-lg text-xs font-semibold bg-sky-600 hover:bg-sky-500 text-white transition-colors';
    btnSubmit.textContent = 'Kiểm tra';

    btnSubmit.onclick = function () {
      var val = parseFloat(input.value.replace(',', '.'));
      var target = parseFloat(String(ch.dap_an).replace(',', '.'));
      var tol = ch.sai_so !== undefined ? ch.sai_so : 0.05;
      var isCorrect = Math.abs(val - target) <= Math.max(tol, Math.abs(target) * 0.02);

      if (isCorrect) {
        input.className += ' border-emerald-500 bg-emerald-50 dark:bg-emerald-950/30';
      } else {
        input.className += ' border-red-500 bg-red-50 dark:bg-red-950/30';
      }
      self._hienGiaiThich(card, ch);
    };

    box.appendChild(input);
    box.appendChild(btnSubmit);
    card.appendChild(box);
  };

  TrinhHienThiCauHoi.prototype._hienGiaiThich = function (card, ch) {
    var existing = card.querySelector('.hl-giai-thich');
    if (existing) return;

    var gt = document.createElement('div');
    gt.className = 'hl-giai-thich text-xs mt-3 p-3 rounded-lg bg-zinc-100 dark:bg-zinc-800/80 border border-zinc-200 dark:border-zinc-700/80 text-zinc-700 dark:text-zinc-300 leading-relaxed';
    gt.innerHTML = '<div class="font-semibold text-zinc-900 dark:text-zinc-100 mb-1">💡 Lời giải chi tiết:</div>' + (ch.giai_thich || 'Đáp án: ' + ch.dap_an);

    if (ch.thong_so && this.onNapThongSo) {
      var self = this;
      var btnNap = document.createElement('button');
      btnNap.className = 'mt-2 text-xs font-semibold text-sky-600 dark:text-sky-400 hover:underline flex items-center gap-1';
      btnNap.innerHTML = '⚙ Nạp thông số vào mô phỏng để quan sát';
      btnNap.onclick = function () {
        self.onNapThongSo(ch.thong_so);
      };
      gt.appendChild(btnNap);
    }

    card.appendChild(gt);
  };

  return {
    TrinhHienThiCauHoi: TrinhHienThiCauHoi
  };
});
