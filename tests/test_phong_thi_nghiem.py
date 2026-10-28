"""Phòng thí nghiệm Hoá học ảo (/hoa-hoc/phong-thi-nghiem): dữ liệu 5 thí nghiệm + bảng ion, trang cần đăng nhập,
công tắc admin bản Unity (app_settings "phong_thi_nghiem"), trạng thái bản build, hàm tính (chạy node --test tests/js/)."""
import json
import re
import shutil
import subprocess
from pathlib import Path

import pytest

from backend import db, nhat_ky, phong_thi_nghiem

GOC = Path(__file__).resolve().parents[1]
THU_MUC = GOC / "frontend" / "hoa-hoc" / "phong-thi-nghiem"
DS_TN = ["an-toan", "chuan-do", "nhan-biet-ion", "pin-galvani", "dien-phan"]
TRUONG = ("id", "ten", "lop", "muc_tieu", "dung_cu", "hoa_chat", "buoc", "an_toan", "cau_hoi")


def _doc(p: str):
    return json.loads((THU_MUC / p).read_text(encoding="utf-8"))


# ------------------------------------------------------------------ dữ liệu
@pytest.mark.parametrize("tn", DS_TN)
def test_du_lieu_thi_nghiem_du_truong_va_cau_hoi_3_dang(tn):
    d = _doc(f"thi-nghiem/{tn}.json")
    for t in TRUONG:
        assert d.get(t) not in (None, "", []), f"{tn}: thiếu {t}"
    assert d["id"] == tn
    for b in d["buoc"]:
        assert b["tieu_de"] and b["huong_dan"] and b["dieu_kien"]
    ch = d["cau_hoi"]
    assert 3 <= len(ch["bon_lua_chon"]) <= 5
    for c in ch["bon_lua_chon"]:
        assert c["cau"] and c["giai_thich"] and len(c["lua_chon"]) == 4 and len(set(c["lua_chon"])) == 4 and c["dap_an"] in "ABCD"
    assert 1 <= len(ch["dung_sai"]) <= 2
    for c in ch["dung_sai"]:
        assert c["cau"] and len(c["y"]) == 4 and all(isinstance(y["dung"], bool) and y["noi_dung"] for y in c["y"])
    assert 1 <= len(ch["tra_loi_ngan"]) <= 2
    for c in ch["tra_loi_ngan"]:
        assert c["cau"] and re.fullmatch(r"-?\d+(,\d+)?", c["dap_so"]), f"{tn}: đáp số phải là số (dấu phẩy thập phân)"


def test_bang_ion_du_ion_va_phuong_trinh():
    b = _doc("bang-ion.json")
    can = {"Cl-", "Br-", "I-", "SO4_2-", "CO3_2-", "Fe3+", "Fe2+", "Cu2+", "Al3+", "Mg2+", "NH4+"}
    assert can <= set(b["ion"])
    pu = {(p["ion"], p["thuoc_thu"]): p for p in b["phan_ung"]}
    for ion, tt, kt in (("Cl-", "AgNO3", "AgCl"), ("Br-", "AgNO3", "AgBr"), ("I-", "AgNO3", "AgI"), ("SO4_2-", "BaCl2", "BaSO₄"),
                        ("Fe3+", "NaOH", "Fe(OH)₃"), ("Fe2+", "NaOH", "Fe(OH)₂"), ("Cu2+", "NaOH", "Cu(OH)₂"), ("Al3+", "NaOH", "Al(OH)₃"), ("Mg2+", "NaOH", "Mg(OH)₂")):
        assert pu[(ion, tt)]["ket_tua"]["cong_thuc"] == kt
    assert pu[("CO3_2-", "HCl")]["khi"] == "CO₂"
    assert pu[("Fe3+", "KSCN")]["mau_dd"]["mo_ta"] == "đỏ máu"
    assert pu[("Fe2+", "NaOH")]["ngoai_khong_khi"]["ket_tua"]["mau"] == "nâu đỏ"
    assert pu[("Cu2+", "NH3")]["du"]["tan"] and "xanh lam thẫm" in pu[("Cu2+", "NH3")]["du"]["mau_dd"]["mo_ta"]
    assert pu[("Al3+", "NaOH")]["du"]["tan"] and not pu[("Al3+", "NH3")]["du"]["tan"]
    assert pu[("NH4+", "NaOH")]["can_dun"] and pu[("NH4+", "NaOH")]["khi"] == "NH₃"
    assert all(p["pt_ion"] for p in b["phan_ung"]), "mỗi dòng phải có phương trình ion thu gọn"
    assert set(b["ngon_lua"]) == {"Na", "K", "Li", "Ca", "Ba", "Cu"}


def test_ban_do_hop_le():
    b = _doc("ban-do.json")
    for truong in ("phong", "xuat_phat", "khu", "tram", "noi_that"):
        assert truong in b, f"Thiếu trường {truong} trong ban-do.json"
    assert len(b["khu"]) == 5
    assert len(b["tram"]) == 5
    huong_hop_le = {0, 90, 180, 270}
    # Kiểm tra huong của trạm và đồ nội thất
    for t in b["tram"]:
        assert t["huong"] in huong_hop_le, f"Trạm {t['tn']} huong không hợp lệ: {t['huong']}"
        assert (THU_MUC / f"thi-nghiem/{t['tn']}.json").is_file(), f"Không có file thi-nghiem/{t['tn']}.json"
    for f in b["noi_that"]:
        assert f["huong"] in huong_hop_le, f"Nội thất {f['id']} huong không hợp lệ: {f['huong']}"
    # Khu không chồng nhau
    for i, ki in enumerate(b["khu"]):
        for j in range(i + 1, len(b["khu"])):
            kj = b["khu"][j]
            gx0 = max(ki["hcn"][0], kj["hcn"][0])
            gx1 = min(ki["hcn"][2], kj["hcn"][2])
            gz0 = max(ki["hcn"][1], kj["hcn"][1])
            gz1 = min(ki["hcn"][3], kj["hcn"][3])
            assert not (gx1 - gx0 > 0.001 and gz1 - gz0 > 0.001), f"Khu {ki['id']} và {kj['id']} chồng nhau"



# ------------------------------------------------------------------ trang
def test_trang_can_dang_nhap(client, dang_nhap):
    r = client.get("/hoa-hoc/phong-thi-nghiem", follow_redirects=False)
    assert r.status_code == 303 and r.headers["location"].startswith("/dang-nhap?tiep=%2Fhoa-hoc%2Fphong-thi-nghiem")
    dang_nhap(client, "ptn-hs", "ptn.hs@vi-du.vn")
    r = client.get("/hoa-hoc/phong-thi-nghiem?tn=chuan-do")
    assert r.status_code == 200
    assert "three@0.160.0/build/three.module.js" in r.text and 'type="importmap"' in r.text
    assert '"ptn/phong-mo"' in r.text and '"ptn/nhan-vat"' in r.text and '"ptn/ban-do"' in r.text and '"ptn/dieu-khien"' in r.text
    assert 'id="ptn-ban-do-mini"' in r.text and 'id="ptn-joystick"' in r.text
    # JS/CSS cục bộ được gắn ?v= (kể cả trong importmap) để không dùng lại bản cũ đã lưu
    for f in (
        "hoa-hoc/phong-thi-nghiem/ve-web.js",
        "hoa-hoc/phong-thi-nghiem/giao-dien.js",
        "hoa-hoc/phong-thi-nghiem/mo-phong.js",
        "hoa-hoc/phong-thi-nghiem/phong-mo.js",
        "hoa-hoc/phong-thi-nghiem/nhan-vat.js",
        "hoa-hoc/phong-thi-nghiem/ban-do.js",
        "hoa-hoc/phong-thi-nghiem/dieu-khien.js",
        "style.css",
    ):
        assert re.search(rf'"/static/{re.escape(f)}\?v=[0-9a-f]{{10}}"', r.text), f



def test_trang_chu_co_4_loi_vao():
    s = (GOC / "frontend" / "index.html").read_text(encoding="utf-8")
    assert s.count('href="/hoa-hoc/phong-thi-nghiem"') == 4
    for lop in ("nav-link-awe", "nav-lab-pill-mobile", "the-lab-ngang", "nut-lien-ket-ngoai"):
        assert re.search(rf'href="/hoa-hoc/phong-thi-nghiem" class="[^"]*{lop}', s), lop


def test_giao_dien_khong_chen_html_tu_du_lieu():
    s = (THU_MUC / "giao-dien.js").read_text(encoding="utf-8")
    # innerHTML chỉ dùng cho hình GHS cố định trong code, không cho dữ liệu JSON
    gan = re.findall(r"\.innerHTML\s*=\s*([^;]+);", s)
    assert gan == ['HINH_GHS[ma] || ""'], gan


# ------------------------------------------------------------------ công tắc Unity
def _admin(client, dang_nhap):
    csrf = dang_nhap(client, "ptn-admin", "ptn.admin@vi-du.vn")
    db.dat_admin(db.tim_theo_email("ptn.admin@vi-du.vn").id, True)
    return {"X-CSRF-Token": csrf}


def test_cong_tac_mac_dinh_tat_va_quyen(client, dang_nhap):
    assert client.get("/admin/api/phong-thi-nghiem").status_code == 401
    assert client.get("/api/phong-thi-nghiem/cau-hinh").status_code == 401
    csrf = dang_nhap(client, "ptn-hs2", "ptn.hs2@vi-du.vn")
    assert client.get("/admin/api/phong-thi-nghiem").status_code == 403
    assert client.post("/admin/api/phong-thi-nghiem", headers={"X-CSRF-Token": csrf}, json={"unity_bat": True}).status_code == 403
    ch = client.get("/api/phong-thi-nghiem/cau-hinh").json()
    assert ch["unity_bat"] is False and ch["la_admin"] is False
    client.cookies.clear()
    h = _admin(client, dang_nhap)
    d = client.get("/admin/api/phong-thi-nghiem").json()
    assert d["unity_bat"] is False and "dung_luong_build" in d
    assert client.post("/admin/api/phong-thi-nghiem", json={"unity_bat": True}).status_code == 403   # thiếu CSRF


def test_admin_bat_thi_hoc_sinh_thay_va_luu_ben(client, dang_nhap):
    h = _admin(client, dang_nhap)
    r = client.post("/admin/api/phong-thi-nghiem", headers=h, json={"unity_bat": True})
    assert r.status_code == 200 and r.json()["unity_bat"] is True
    assert json.loads(db.cai_dat(phong_thi_nghiem.KHOA))["unity_bat"] is True          # lưu trong app.db → có trong bản sao lưu
    with db.ket_noi() as con:
        assert con.execute("SELECT COUNT(*) FROM activity_log WHERE event = 'doi_phong_thi_nghiem'").fetchone()[0] == 1   # có nhật ký
    assert "doi_phong_thi_nghiem" in nhat_ky.SU_KIEN
    client.cookies.clear()
    dang_nhap(client, "ptn-hs3", "ptn.hs3@vi-du.vn")
    assert client.get("/api/phong-thi-nghiem/cau-hinh").json()["unity_bat"] is True
    client.cookies.clear()
    h = _admin(client, dang_nhap)
    assert client.post("/admin/api/phong-thi-nghiem", headers=h, json={"unity_bat": False}).json()["unity_bat"] is False


def test_unity_san_sang_theo_thu_muc_build(client, dang_nhap, tmp_path, monkeypatch):
    monkeypatch.setattr(phong_thi_nghiem, "THU_MUC_BUILD", tmp_path / "Build")
    assert phong_thi_nghiem.unity_san_sang() is False and phong_thi_nghiem.dung_luong_build() == 0
    b = tmp_path / "Build"
    b.mkdir()
    (b / "unity-lab.loader.js").write_text("// loader")
    (b / "unity-lab.data.unityweb").write_bytes(b"x" * 1000)
    (b / "unity-lab.framework.js.unityweb").write_bytes(b"y" * 100)
    assert phong_thi_nghiem.unity_san_sang() is False, "thiếu wasm → chưa sẵn sàng"
    (b / "unity-lab.wasm.unityweb").write_bytes(b"z" * 10)
    assert phong_thi_nghiem.unity_san_sang() is True
    assert phong_thi_nghiem.dung_luong_build() == len("// loader") + 1110
    assert phong_thi_nghiem.tep_build()["wasm"] == "/static/unity-lab/Build/unity-lab.wasm.unityweb"
    # đang TẮT: học sinh thấy "sẵn sàng" nhưng không nhận đường dẫn build; admin thì có
    dang_nhap(client, "ptn-hs4", "ptn.hs4@vi-du.vn")
    ch = client.get("/api/phong-thi-nghiem/cau-hinh").json()
    assert ch["unity_san_sang"] is True and ch["unity_bat"] is False and "unity_tep" not in ch
    client.cookies.clear()
    h = _admin(client, dang_nhap)
    assert "unity_tep" in client.get("/api/phong-thi-nghiem/cau-hinh").json()
    assert client.get("/admin/api/phong-thi-nghiem").json()["unity_san_sang"] is True
    client.post("/admin/api/phong-thi-nghiem", headers=h, json={"unity_bat": True})
    client.cookies.clear()
    dang_nhap(client, "ptn-hs4", "ptn.hs4@vi-du.vn")
    assert "unity_tep" in client.get("/api/phong-thi-nghiem/cau-hinh").json()


def test_du_an_unity_co_ma_nguon_va_script_build():
    u = GOC / "unity" / "PhongThiNghiemHoaHoc"
    for f in ("README.md", "Packages/manifest.json", "ProjectSettings/ProjectVersion.txt", "Assets/Scripts/LabBridge.cs",
              "Assets/Plugins/WebGL/LabBridge.jslib", "Assets/Editor/BuildWebGL.cs"):
        assert (u / f).is_file(), f
    build = (u / "Assets/Editor/BuildWebGL.cs").read_text(encoding="utf-8")
    assert "public static void Build()" in build and "WebGLCompressionFormat.Gzip" in build and "decompressionFallback = true" in build
    assert "window.PhongLabUnity" in (u / "Assets/Plugins/WebGL/LabBridge.jslib").read_text(encoding="utf-8")
    assert '"NhanLenh"' in (THU_MUC / "cau-unity.js").read_text(encoding="utf-8")


# ------------------------------------------------------------------ hàm tính (UMD – chạy bằng Node)
def test_tinh_toan_bang_node():
    node = shutil.which("node")
    if not node:
        pytest.skip("Máy không có Node – bỏ qua test tính toán JS")
    r = subprocess.run([node, "--test", "tests/js/"], cwd=GOC, capture_output=True, text=True, timeout=300)
    assert r.returncode == 0, r.stdout[-3000:] + r.stderr[-2000:]
    assert re.search(r"# fail 0|ℹ fail 0", r.stdout)
