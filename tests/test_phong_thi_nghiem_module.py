"""Phòng thí nghiệm Hoá ảo: mọi module JS khai báo đủ trong trang (importmap + thẻ script) và file tồn tại.
Thiếu một dòng importmap / thẻ <script> là cả phòng 3D trắng trang (lỗi chỉ lộ ra trên trình duyệt) – test này bắt sớm."""
import json
import re
from pathlib import Path

GOC = Path(__file__).resolve().parent.parent / "frontend"
THU_MUC = GOC / "hoa-hoc" / "phong-thi-nghiem"
TRANG = (GOC / "phong-thi-nghiem.html").read_text(encoding="utf-8")


def _importmap() -> dict:
    m = re.search(r'<script type="importmap">\s*(\{.*?\})\s*</script>', TRANG, re.S)
    assert m, "thiếu importmap"
    return json.loads(m.group(1))["imports"]


def _tep_tu_url(url: str) -> Path:
    assert url.startswith("/static/"), url
    return GOC / url[len("/static/"):]


def test_importmap_tro_toi_tep_co_that():
    for ten, url in _importmap().items():
        if url.startswith("/static/"):
            assert _tep_tu_url(url).is_file(), f"importmap '{ten}' → {url} không tồn tại"


def test_the_script_tinh_tro_toi_tep_co_that():
    for url in re.findall(r'<script src="(/static/[^"?]+)', TRANG):
        assert _tep_tu_url(url).is_file(), f"<script src> {url} không tồn tại"


def test_moi_import_ptn_deu_co_trong_importmap():
    khai_bao = set(_importmap())
    for tep in THU_MUC.glob("*.js"):
        for ten in re.findall(r'from\s+"(ptn/[^"]+)"', tep.read_text(encoding="utf-8")):
            assert ten in khai_bao, f"{tep.name} import '{ten}' nhưng importmap của phong-thi-nghiem.html không có"


def test_module_umd_dung_chung_duoc_nap_truoc_module_dung_no():
    """ve-web.js dùng window.PTNHinhHoc (hinh-hoc.js, UMD) → thẻ <script> của nó phải có và đứng trước importmap/module."""
    assert "window.PTNHinhHoc" in (THU_MUC / "ve-web.js").read_text(encoding="utf-8")
    vt_script = TRANG.index("/static/hoa-hoc/phong-thi-nghiem/hinh-hoc.js")
    assert vt_script < TRANG.index('<script type="importmap">')
