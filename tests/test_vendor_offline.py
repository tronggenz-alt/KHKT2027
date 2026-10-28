"""App máy tính chạy OFFLINE: mọi thư viện three.js (three/addons/...) mà trang 3D import phải có sẵn trong frontend/vendor/three/addons/.
Thiếu một file là trang đó 404 → kẹt ở màn hình chờ (đã từng: phòng thí nghiệm thiếu RoomEnvironment + RoundedBoxGeometry, Sinh học 3D thiếu GLTFLoader).
Thêm addon mới → thêm vào tools/tai_vendor.py rồi chạy lại."""
import re
from pathlib import Path

GOC = Path(__file__).resolve().parent.parent
FRONTEND = GOC / "frontend"
ADDONS = FRONTEND / "vendor" / "three" / "addons"


def _addon_dang_dung() -> dict[str, set[str]]:
    dung: dict[str, set[str]] = {}
    for tep in FRONTEND.rglob("*"):
        if tep.suffix not in (".html", ".js", ".mjs") or "vendor" in tep.parts:
            continue
        for m in re.findall(r"three/addons/([A-Za-z0-9_/\.]+\.js)", tep.read_text(encoding="utf-8", errors="ignore")):
            dung.setdefault(m, set()).add(tep.name)
    return dung


def test_moi_addon_three_deu_co_ban_vendor():
    thieu = {a: sorted(t) for a, t in _addon_dang_dung().items() if not (ADDONS / a).is_file()}
    assert not thieu, f"thiếu trong frontend/vendor/three/addons/ (app offline sẽ 404): {thieu}"


def test_addon_vendor_khong_phai_stub():
    for tep in ADDONS.rglob("*.js"):
        assert "Fallback stub" not in tep.read_text(encoding="utf-8"), f"{tep.name} chỉ là stub (tải thất bại lúc đóng gói?)"


def test_addon_import_tuong_doi_cung_co_san():
    """GLTFLoader import '../utils/BufferGeometryUtils.js' → file đó cũng phải có."""
    for tep in ADDONS.rglob("*.js"):
        for rel in re.findall(r"from\s+'(\.[^']+\.js)'", tep.read_text(encoding="utf-8")):
            assert (tep.parent / rel).resolve().is_file(), f"{tep.name} import {rel} nhưng không có trong vendor"
