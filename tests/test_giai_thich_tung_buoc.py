"""Chế độ "Giải thích từng bước có mũi tên" (frontend/sinh-hoc/giai-thich/): module tách riêng, chạy theo vòng render chung,
không setTimeout / PointLight, dữ liệu tách khỏi giao diện; test logic bằng Node (tests/js/giai_thich.test.mjs)."""
import re
import shutil
import subprocess
from pathlib import Path

import pytest

GOC = Path(__file__).resolve().parents[1]
THU_MUC = GOC / "frontend" / "sinh-hoc" / "giai-thich"
TRANG = GOC / "frontend" / "biology-lab-3d.html"


@pytest.mark.skipif(shutil.which("node") is None, reason="máy không có Node.js")
def test_logic_bang_node():
    kq = subprocess.run(["node", "--test", str(GOC / "tests" / "js" / "giai_thich.test.mjs")], capture_output=True, text=True, timeout=120)
    assert kq.returncode == 0, kq.stdout[-3000:] + kq.stderr[-2000:]


def test_module_tach_rieng_va_co_chu_thich():
    for ten in ("config.js", "step-player.js", "arrow-renderer.js", "camera-director.js", "highlight-manager.js", "explain-panel.js",
                "kiem-tra.js", "index.js", "data/index.js", "data/vong-tuan-hoan-nho.js", "data/vong-tuan-hoan-lon.js", "data/ten-bo-phan.js"):
        nd = (THU_MUC / ten).read_text(encoding="utf-8")
        assert nd.startswith("// "), f"{ten}: thiếu chú thích đầu module"
        ma = re.sub(r"//[^\n]*", "", nd)        # bỏ chú thích, chỉ xét code
        assert not re.search(r"\b(setTimeout|setInterval|requestAnimationFrame)\s*\(", ma), ten
        assert "PointLight" not in ma, ten
    mt = (THU_MUC / "arrow-renderer.js").read_text(encoding="utf-8")
    assert "clear()" in mt and ".dispose()" in mt and "this._vatLieu" in mt


def test_trang_dung_module_va_vong_render_chung():
    s = TRANG.read_text(encoding="utf-8")
    assert 'await import("/static/sinh-hoc/giai-thich/index.js' in s and "giaiThichTungBuoc?.capNhat(dt);" in s
    # dữ liệu / code cũ đã chuyển hết sang module (không còn dữ liệu cứng, chuỗi setTimeout, đèn nhấp nháy)
    for cu in ("MAU_TUAN_HOAN_NHO", "MAU_TUAN_HOAN_LON", "taoMuiTen3D", "timerTuChay", "chuanHoaTenBP", "khoiTaoKeoKhung"):
        assert cu not in s, cu
    for el in ('id="gt-vi-sao"', 'id="gt-ghi-nho"', 'id="gt-tu-chay"', 'id="gt-toc-do"', 'id="gt-dem-nguoc"', 'id="gt-tieu-de-buoc"'):
        assert el in s, el
    assert re.search(r'quy_trinh: "tuan_hoan_(nho|lon)"', s)
