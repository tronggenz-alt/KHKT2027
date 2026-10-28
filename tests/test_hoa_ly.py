"""Kiểm thử mục Hoá lý (/hoa-hoc/hoa-ly) và mô phỏng 2D Động cơ nhiệt 4 kỳ (/hoa-hoc/hoa-ly/dong-co-nhiet).
Bao gồm: bảo vệ route đăng nhập, băm ?v= file tĩnh, kiểm tra danh-muc.json, liên kết trang chủ,
chế độ app ngoại tuyến (vendor offline), cấu trúc nhien-lieu.json và cau-hoi.json."""
import json
import re
import shutil
import subprocess
from pathlib import Path

import pytest

from backend.config import settings

GOC = Path(__file__).resolve().parents[1]
FRONTEND = GOC / "frontend"
THU_MUC_HOA_LY = FRONTEND / "hoa-hoc" / "hoa-ly"
THU_MUC_DONG_CO = THU_MUC_HOA_LY / "dong-co-nhiet"


def test_danh_muc_hop_le_va_ten_tam():
    tep_json = THU_MUC_HOA_LY / "danh-muc.json"
    assert tep_json.is_file(), "Thiếu file danh-muc.json"
    data = json.loads(tep_json.read_text(encoding="utf-8"))

    assert "mo_phong" in data and len(data["mo_phong"]) >= 1
    ids = [mp["id"] for mp in data["mo_phong"]]
    assert len(ids) == len(set(ids)), "id mô phỏng trong danh-muc.json không được trùng"

    # Kiểm tra mục có sẵn
    muc_co = [mp for mp in data["mo_phong"] if mp.get("trang_thai") == "co"]
    assert len(muc_co) >= 1, "Phải có ít nhất 1 mô phỏng trạng thái 'co'"
    for mp in muc_co:
        assert mp["duong_dan"].startswith("/hoa-hoc/hoa-ly/"), f"Đường dẫn không hợp lệ: {mp['duong_dan']}"

    # Quy tắc quan trọng: Tên tạm KHÔNG xuất hiện trong trang.html, hoa-ly.html, index.html
    muc_tam = [mp for mp in data["mo_phong"] if mp.get("ten_tam")]
    for mp in muc_tam:
        ten_tam = mp["ten"]
        for ten_tep, p in (
            ("hoa-ly.html", FRONTEND / "hoa-ly.html"),
            ("trang.html", THU_MUC_DONG_CO / "trang.html"),
            ("index.html", FRONTEND / "index.html"),
        ):
            noi_dung = p.read_text(encoding="utf-8")
            assert ten_tam not in noi_dung, (
                f"Tên tạm '{ten_tam}' không được xuất hiện trực tiếp trong {ten_tep} "
                "(tên phải được tải động từ danh-muc.json)"
            )


def test_trang_chu_co_loi_vao_hoa_ly():
    s = (FRONTEND / "index.html").read_text(encoding="utf-8")
    assert 'href="/hoa-hoc/hoa-ly"' in s
    # Có ở menu
    assert re.search(r'href="/hoa-hoc/hoa-ly"[^>]*class="[^"]*nav-link-awe', s), "Thiếu link Hoá lý trong nav-awesomic"
    # Có ở banner
    assert re.search(r'href="/hoa-hoc/hoa-ly"[^>]*class="[^"]*the-lab-ngang', s), "Thiếu banner Hoá lý trên trang chủ"


def test_routes_can_dang_nhap_va_bam_tinh(client, dang_nhap):
    for duong_dan in ("/hoa-hoc/hoa-ly", "/hoa-hoc/hoa-ly/dong-co-nhiet"):
        # Chưa đăng nhập -> 303 Redirect
        r = client.get(duong_dan, follow_redirects=False)
        assert r.status_code == 303
        assert r.headers["location"].startswith("/dang-nhap?tiep="), f"{duong_dan} chưa chặn đăng nhập"

    # Đăng nhập
    dang_nhap(client, "hoc-sinh-hl", "hl.hs@vi-du.vn")

    for duong_dan in ("/hoa-hoc/hoa-ly", "/hoa-hoc/hoa-ly/dong-co-nhiet"):
        r = client.get(duong_dan)
        assert r.status_code == 200, f"Truy cập {duong_dan} thất bại: {r.status_code}"

        # Mọi /static/...js|css phải có ?v=<hash>
        static_refs = re.findall(r'"/static/([^"]+\.(?:js|css))(?:\?v=([0-9a-f]{10}))?"', r.text)
        assert len(static_refs) > 0, f"{duong_dan} không có script/css static nào"
        for tep, v in static_refs:
            assert v is not None, f"Tệp static {tep} trong {duong_dan} chưa được gắn mã băm ?v="


def test_che_do_app_ngoai_tuyen(client, dang_nhap, monkeypatch):
    """Khi settings.che_do_app=True: HTML dùng vendor offline, không còn CDN Tailwind và không có URL ngoài (trừ Google Fonts)."""
    monkeypatch.setattr(settings, "che_do_app", True)
    dang_nhap(client, "hoc-sinh-app", "app.hs@vi-du.vn")

    cac_duong_dan = (
        "/hoa-hoc/hoa-ly",
        "/hoa-hoc/hoa-ly/dong-co-nhiet",
        "/hoa-hoc/hoa-ly/khi-ly-tuong",
        "/hoa-hoc/hoa-ly/can-bang-le-chatelier",
        "/hoa-hoc/hoa-ly/toc-do-phan-ung",
        "/hoa-hoc/hoa-ly/cong-thuc",
    )
    for duong_dan in cac_duong_dan:
        r = client.get(duong_dan)
        assert r.status_code == 200, f"{duong_dan} trả về {r.status_code}"
        html = r.text

        # Phải dùng vendor offline cho Tailwind
        assert "cdn.tailwindcss.com" not in html, f"{duong_dan} vẫn còn cdn.tailwindcss.com khi ở chế độ app"
        assert "/static/vendor/tailwind.3.4.16.js" in html, f"{duong_dan} thiếu vendor offline tailwind.3.4.16.js"

        # MathJax offline
        assert "cdn.jsdelivr.net" not in html, f"{duong_dan} còn chứa CDN jsdelivr khi ở chế độ app"
        assert "cdnjs.cloudflare.com" not in html, f"{duong_dan} còn chứa CDN cdnjs khi ở chế độ app"

        # Không có URL bên ngoài nào khác ngoài Google Fonts (loại trừ xmlns w3.org trong SVG)
        cac_url_ngoai = re.findall(r'https?://(?:(?!fonts\.(?:googleapis|gstatic)\.com|www\.w3\.org)[a-zA-Z0-9.-]+)', html)
        assert not cac_url_ngoai, f"{duong_dan} còn chứa URL ngoài ở chế độ app: {cac_url_ngoai}"

    # Kiểm tra font MathJax offline
    thu_muc_font = FRONTEND / "vendor" / "mathjax-fonts" / "woff-v2"
    assert thu_muc_font.is_dir(), "Thiếu thư mục font MathJax woff-v2"
    cac_font = list(thu_muc_font.glob("*.woff"))
    assert len(cac_font) >= 20, f"Cần ít nhất 20 file font woff-v2, hiện có {len(cac_font)}"


def test_nhien_lieu_hop_le():
    tep_nl = THU_MUC_DONG_CO / "nhien-lieu.json"
    assert tep_nl.is_file(), "Thiếu nhien-lieu.json"
    data = json.loads(tep_nl.read_text(encoding="utf-8"))

    assert "san_pham" in data and "danh_sach" in data
    assert len(data["danh_sach"]) >= 4

    ids = [nl["id"] for nl in data["danh_sach"]]
    assert set(ids) >= {"isooctane", "ethanol", "methane", "hydrogen"}

    for nl in data["danh_sach"]:
        for truong in ("id", "ten", "cong_thuc", "the", "M", "delta_f_H", "nguon"):
            assert truong in nl, f"Nhiên liệu {nl.get('id')} thiếu trường {truong}"
        assert isinstance(nl["M"], (int, float)) and nl["M"] > 0
        assert isinstance(nl["delta_f_H"], (int, float))


def test_cau_hoi_3_dang_va_tinh_bang():
    tep_ch = THU_MUC_DONG_CO / "cau-hoi.json"
    assert tep_ch.is_file(), "Thiếu cau-hoi.json"
    data = json.loads(tep_ch.read_text(encoding="utf-8"))

    ds = data.get("danh_sach", [])
    assert len(ds) == 9, "cau-hoi.json phải có đúng 9 câu hỏi"

    dang_counts = {"I": 0, "II": 0, "III": 0}
    for c in ds:
        dang = c.get("dang")
        assert dang in dang_counts, f"Dạng câu hỏi không hợp lệ: {dang}"
        dang_counts[dang] += 1
        assert c.get("cau_hoi") and c.get("giai_thich"), f"Câu {c.get('id')} thiếu nội dung"

        if dang == "I":
            assert len(c.get("lua_chon", [])) == 4
            assert c.get("dap_an") in "ABCD"
        elif dang == "II":
            assert len(c.get("y", [])) == 4
            for y in c["y"]:
                assert "noi_dung" in y and isinstance(y.get("dung"), bool)
        elif dang == "III":
            assert c.get("dap_so"), f"Câu {c.get('id')} thiếu đáp số"

    assert dang_counts == {"I": 3, "II": 3, "III": 3}, f"Mỗi dạng phải có đúng 3 câu, hiện tại: {dang_counts}"

    # Kiểm tra các câu có tinh_bang với node
    node_bin = shutil.which("node")
    if not node_bin:
        pytest.skip("Không có node trên hệ thống để kiểm tra tinh_bang")

    nhiet_dong_js = THU_MUC_DONG_CO / "nhiet-dong.js"
    if not nhiet_dong_js.is_file():
        pytest.skip("Chưa tạo nhiet-dong.js (sẽ kiểm tra ở GĐ 2)")

    for c in ds:
        tb = c.get("tinh_bang")
        if not tb:
            continue
        tham_so_json = json.dumps(tb["tham_so"])
        ham = tb["ham"]
        truong = tb["truong"]
        lam_tron = tb.get("lam_tron", 0)

        cmd = [
            node_bin,
            "-e",
            f"""
            const ND = require('{nhiet_dong_js.resolve()}');
            const res = ND.{ham}({tham_so_json});
            let val = res['{truong}'];
            if ({lam_tron} === 0) {{
                val = Math.round(val);
                console.log(val.toString());
            }} else {{
                val = val.toFixed({lam_tron}).replace('.', ',');
                console.log(val);
            }}
            """,
        ]
        res = subprocess.run(cmd, capture_output=True, text=True, check=True)
        ket_qua = res.stdout.strip()
        assert ket_qua == str(c["dap_so"]).strip(), (
            f"Câu {c['id']}: tinh_bang tính ra '{ket_qua}' nhưng đáp số là '{c['dap_so']}'"
        )


def test_cong_thuc_json_hop_le_va_day_du():
    tep_json = THU_MUC_HOA_LY / "cong-thuc.json"
    assert tep_json.is_file(), "Thiếu file cong-thuc.json"
    data = json.loads(tep_json.read_text(encoding="utf-8"))

    assert "chu_de" in data and "cong_thuc" in data
    ds_ct = data["cong_thuc"]
    assert len(ds_ct) >= 30, f"Phải có ít nhất 30 công thức, hiện có: {len(ds_ct)}"

    # Kiểm tra id và ma duy nhất
    ids = [ct["id"] for ct in ds_ct]
    assert len(ids) == len(set(ids)), "id công thức không được trùng"
    mas = [ct.get("ma") for ct in ds_ct if "ma" in ct]
    assert len(mas) == len(set(mas)), "ma công thức không được trùng"

    # Kiểm tra đủ các mã bắt buộc theo mục 5.3: K1-K8, N1-N8, H1-H3, D1-D7, C1-C7
    ma_bat_buoc = (
        [f"K{i}" for i in range(1, 9)] +
        [f"N{i}" for i in range(1, 9)] +
        [f"H{i}" for i in range(1, 4)] +
        [f"D{i}" for i in range(1, 8)] +
        [f"C{i}" for i in range(1, 8)]
    )
    for ma in ma_bat_buoc:
        assert ma in mas, f"Thiếu mã công thức bắt buộc: {ma}"

    # Mỗi chủ đề có ít nhất 4 công thức
    cd_counts = {}
    for ct in ds_ct:
        cd = ct.get("chu_de")
        cd_counts[cd] = cd_counts.get(cd, 0) + 1
    for cd_obj in data["chu_de"]:
        cd_id = cd_obj["id"]
        assert cd_counts.get(cd_id, 0) >= 4, f"Chủ đề '{cd_id}' phải có ít nhất 4 công thức (hiện có {cd_counts.get(cd_id, 0)})"

    # Đọc danh mục mô phỏng hợp lệ
    tep_dm = THU_MUC_HOA_LY / "danh-muc.json"
    data_dm = json.loads(tep_dm.read_text(encoding="utf-8"))
    ids_mo_phong = {mp["id"] for mp in data_dm.get("mo_phong", [])}

    # Mỗi mô phỏng có >= 5 công thức trỏ tới nó
    mp_counts = {}
    for ct in ds_ct:
        for mp in ct.get("mo_phong", []):
            mp_id = mp["id"]
            assert mp_id in ids_mo_phong, f"Mô phỏng '{mp_id}' trong công thức {ct['id']} không có trong danh-muc.json"
            mp_counts[mp_id] = mp_counts.get(mp_id, 0) + 1

    for mp_id in ("khi-ly-tuong", "dong-co-nhiet", "can-bang-le-chatelier", "toc-do-phan-ung"):
        assert mp_counts.get(mp_id, 0) >= 5, f"Mô phỏng '{mp_id}' phải có ít nhất 5 công thức trỏ tới nó (hiện có {mp_counts.get(mp_id, 0)})"

    # Mỗi bước chứng minh >= 3 bước, trừ dinh-luat-thuc-nghiem
    for ct in ds_ct:
        cm = ct.get("chung_minh", {})
        loai = cm.get("loai")
        buoc = cm.get("buoc", [])
        if loai != "dinh-luat-thuc-nghiem":
            assert len(buoc) >= 3, f"Công thức {ct['id']} ({loai}) phải có ít nhất 3 bước chứng minh (hiện có {len(buoc)})"


def test_cong_thuc_vi_du_so_voi_node():
    node_bin = shutil.which("node")
    if not node_bin:
        pytest.skip("Không có node trên hệ thống để kiểm tra vi_du_so")

    tep_json = THU_MUC_HOA_LY / "cong-thuc.json"
    data = json.loads(tep_json.read_text(encoding="utf-8"))

    js_khi = THU_MUC_HOA_LY / "khi-ly-tuong" / "mo-hinh.js"
    js_toc_do = THU_MUC_HOA_LY / "toc-do-phan-ung" / "mo-hinh.js"
    js_can_bang = THU_MUC_HOA_LY / "can-bang-le-chatelier" / "mo-hinh.js"
    js_dong_co = THU_MUC_HOA_LY / "dong-co-nhiet" / "nhiet-dong.js"

    cmd = [
        node_bin,
        "-e",
        f"""
        global.HL_KhiLyTuong = require('{js_khi.resolve()}');
        global.HL_TocDo = require('{js_toc_do.resolve()}');
        global.HL_CanBang = require('{js_can_bang.resolve()}');
        global.HL_DongCoNhiet = require('{js_dong_co.resolve()}');

        const congThuc = {json.dumps(data['cong_thuc'])};
        let errors = [];

        for (const ct of congThuc) {{
            if (!ct.vi_du_so) continue;
            const vd = ct.vi_du_so;
            try {{
                const fn = eval(vd.ham);
                const res = fn(vd.vao);
                const diff = Math.abs(res - vd.ra);
                if (diff > (vd.sai_so || 0.05)) {{
                    errors.push(ct.ma + ': ' + vd.ham + ' ra ' + res + ' nhung ky vong ' + vd.ra + ' (diff: ' + diff + ')');
                }}
            }} catch (err) {{
                errors.push(ct.ma + ' loi: ' + err.message);
            }}
        }}

        if (errors.length > 0) {{
            console.error(errors.join('\\n'));
            process.exit(1);
        }}
        console.log('ALL_OK');
        """
    ]
    res = subprocess.run(cmd, capture_output=True, text=True)
    assert res.returncode == 0, f"Lỗi tính vi_du_so:\n{res.stderr}\n{res.stdout}"
    assert "ALL_OK" in res.stdout


def test_mathjax_offline_va_font_woff_v2(client, dang_nhap, monkeypatch):
    """Kiểm tra font MathJax offline và route /hoa-hoc/hoa-ly/cong-thuc."""
    dir_fonts = FRONTEND / "vendor" / "mathjax-fonts" / "woff-v2"
    assert dir_fonts.is_dir(), f"Thiếu thư mục font MathJax {dir_fonts}"
    woff_files = list(dir_fonts.glob("*.woff"))
    assert len(woff_files) >= 20, f"Phải có ít nhất 20 file font .woff của MathJax (hiện có {len(woff_files)})"

    phien_ban_json = FRONTEND / "vendor" / "PHIEN_BAN.json"
    assert phien_ban_json.is_file(), "Thiếu PHIEN_BAN.json"
    bang_pb = json.loads(phien_ban_json.read_text(encoding="utf-8"))
    assert "mathjax.3.2.2.js" in bang_pb
    font_keys = [k for k in bang_pb if "mathjax-fonts" in k]
    assert len(font_keys) >= 20

    # Chế độ app với route /hoa-hoc/hoa-ly/cong-thuc
    monkeypatch.setattr(settings, "che_do_app", True)
    dang_nhap(client, "hoc-sinh-cong-thuc", "ct.hs@vi-du.vn")

    r = client.get("/hoa-hoc/hoa-ly/cong-thuc")
    assert r.status_code == 200
    html = r.text
    assert "/static/vendor/mathjax.3.2.2.js" in html
    assert "chtml/fonts/woff-v2" in html or "mathjax-fonts/woff-v2" in html


def test_khi_ly_tuong_hop_le_va_cau_hoi(client, dang_nhap):
    thu_muc_khi = THU_MUC_HOA_LY / "khi-ly-tuong"
    assert (thu_muc_khi / "khi.json").is_file(), "Thiếu khi.json"
    assert (thu_muc_khi / "cau-hoi.json").is_file(), "Thiếu cau-hoi.json"
    assert (thu_muc_khi / "mo-hinh.js").is_file(), "Thiếu mo-hinh.js"
    assert (thu_muc_khi / "ve.js").is_file(), "Thiếu ve.js"
    assert (thu_muc_khi / "giao-dien.js").is_file(), "Thiếu giao-dien.js"
    assert (thu_muc_khi / "trang.html").is_file(), "Thiếu trang.html"

    # Kiểm tra khi.json
    khi_data = json.loads((thu_muc_khi / "khi.json").read_text(encoding="utf-8"))
    ds_khi = khi_data.get("danh_sach", [])
    assert len(ds_khi) >= 7
    ids_khi = {k["id"] for k in ds_khi}
    assert {"He", "Ne", "Ar", "H2", "N2", "O2", "CO2"}.issubset(ids_khi)
    for k in ds_khi:
        for f in ("id", "ten", "ct", "M", "gamma", "a", "b", "nguon"):
            assert f in k, f"Khí {k.get('id')} thiếu {f}"

    # Kiểm tra cau-hoi.json
    ch_data = json.loads((thu_muc_khi / "cau-hoi.json").read_text(encoding="utf-8"))
    ds_ch = ch_data.get("danh_sach", [])
    assert len(ds_ch) == 9
    counts = {"I": 0, "II": 0, "III": 0}
    for c in ds_ch:
        assert c.get("nguon"), f"Câu {c.get('id')} thiếu nguon"
        counts[c["dang"]] += 1
    assert counts == {"I": 3, "II": 3, "III": 3}

    # Đăng nhập và kiểm tra route /hoa-hoc/hoa-ly/khi-ly-tuong
    dang_nhap(client, "hoc-sinh-khi", "khi.hs@vi-du.vn")
    r = client.get("/hoa-hoc/hoa-ly/khi-ly-tuong")
    assert r.status_code == 200
    assert "Khí lý tưởng" in r.text


def test_can_bang_files_va_cau_hoi(client, dang_nhap):
    thu_muc_cb = THU_MUC_HOA_LY / "can-bang-le-chatelier"
    assert (thu_muc_cb / "he-can-bang.json").is_file(), "Thiếu he-can-bang.json"
    assert (thu_muc_cb / "cau-hoi.json").is_file(), "Thiếu cau-hoi.json"
    assert (thu_muc_cb / "mo-hinh.js").is_file(), "Thiếu mo-hinh.js"
    assert (thu_muc_cb / "ve.js").is_file(), "Thiếu ve.js"
    assert (thu_muc_cb / "giao-dien.js").is_file(), "Thiếu giao-dien.js"
    assert (thu_muc_cb / "trang.html").is_file(), "Thiếu trang.html"

    # Kiểm tra he-can-bang.json
    data_he = json.loads((thu_muc_cb / "he-can-bang.json").read_text(encoding="utf-8"))
    ds_he = data_he.get("danh_sach", [])
    assert len(ds_he) >= 3
    ids_he = {h["id"] for h in ds_he}
    assert {"N2O4_2NO2", "H2_I2_2HI", "N2_3H2_2NH3"}.issubset(ids_he)
    for h in ds_he:
        for f in ("id", "ten", "phuong_trinh", "chat", "dH_kJ", "K_ref", "nguon"):
            assert f in h, f"Hệ {h.get('id')} thiếu {f}"

    # Kiểm tra cau-hoi.json
    ch_data = json.loads((thu_muc_cb / "cau-hoi.json").read_text(encoding="utf-8"))
    ds_ch = ch_data.get("danh_sach", [])
    assert len(ds_ch) == 9
    counts = {"I": 0, "II": 0, "III": 0}
    for c in ds_ch:
        assert c.get("nguon"), f"Câu {c.get('id')} thiếu nguon"
        counts[c["dang"]] += 1
    assert counts == {"I": 3, "II": 3, "III": 3}

    # Đăng nhập và kiểm tra route /hoa-hoc/hoa-ly/can-bang-le-chatelier
    dang_nhap(client, "hoc-sinh-cb", "cb.hs@vi-du.vn")
    r = client.get("/hoa-hoc/hoa-ly/can-bang-le-chatelier")
    assert r.status_code == 200
    assert "Cân bằng hoá học" in r.text


def test_toc_do_files_va_cau_hoi(client, dang_nhap):
    thu_muc_td = THU_MUC_HOA_LY / "toc-do-phan-ung"
    assert (thu_muc_td / "phan-ung.json").is_file(), "Thiếu phan-ung.json"
    assert (thu_muc_td / "cau-hoi.json").is_file(), "Thiếu cau-hoi.json"
    assert (thu_muc_td / "mo-hinh.js").is_file(), "Thiếu mo-hinh.js"
    assert (thu_muc_td / "ve.js").is_file(), "Thiếu ve.js"
    assert (thu_muc_td / "giao-dien.js").is_file(), "Thiếu giao-dien.js"
    assert (thu_muc_td / "trang.html").is_file(), "Thiếu trang.html"

    # Kiểm tra phan-ung.json
    data_pu = json.loads((thu_muc_td / "phan-ung.json").read_text(encoding="utf-8"))
    ds_pu = data_pu.get("danh_sach", [])
    assert len(ds_pu) >= 4
    ids_pu = {p["id"] for p in ds_pu}
    assert {"A_B_C_D", "2HI_H2_I2", "H2O2_H2O_O2", "Zn_HCl"}.issubset(ids_pu)
    for p in ds_pu:
        for f in ("id", "ten", "phuong_trinh", "Ea_kJ", "A", "dH_kJ", "bac", "nguon"):
            assert f in p, f"Phản ứng {p.get('id')} thiếu {f}"

    # Kiểm tra cau-hoi.json
    ch_data = json.loads((thu_muc_td / "cau-hoi.json").read_text(encoding="utf-8"))
    ds_ch = ch_data.get("danh_sach", [])
    assert len(ds_ch) == 9
    counts = {"I": 0, "II": 0, "III": 0}
    for c in ds_ch:
        assert c.get("nguon"), f"Câu {c.get('id')} thiếu nguon"
        counts[c["dang"]] += 1
    assert counts == {"I": 3, "II": 3, "III": 3}

    # Đăng nhập và kiểm tra route /hoa-hoc/hoa-ly/toc-do-phan-ung
    dang_nhap(client, "hoc-sinh-td", "td.hs@vi-du.vn")
    r = client.get("/hoa-hoc/hoa-ly/toc-do-phan-ung")
    assert r.status_code == 200
    assert "Tốc độ phản ứng" in r.text




