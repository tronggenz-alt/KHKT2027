"""Phòng Lab Sinh học 3D: route /sinh-hoc (cần đăng nhập như mọi trang app) + file HTML độc lập có đủ thành phần bắt buộc."""
import re
from pathlib import Path

TEP = Path(__file__).resolve().parents[1] / "frontend" / "biology-lab-3d.html"


def test_route_can_dang_nhap(client, dang_nhap):
    r = client.get("/sinh-hoc", follow_redirects=False)
    assert r.status_code == 303 and r.headers["location"].startswith("/dang-nhap?tiep=%2Fsinh-hoc")
    dang_nhap(client)
    r = client.get("/sinh-hoc")
    assert r.status_code == 200 and "BIO-LAB 3D" in r.text and 'type="importmap"' in r.text


def test_file_doc_lap_du_thanh_phan():
    s = TEP.read_text(encoding="utf-8")
    # thư viện CDN có phiên bản cố định (chạy ngay khi mở file, không cần build)
    for url in ("three@0.160.0/build/three.module.js", "gsap@3.12.5", "mathjax@3.2.2"):
        assert url in s
    for addon in ("OrbitControls.js", "GLTFLoader.js", "CSS2DRenderer.js"):
        assert addon in s
    # quản lý tài nguyên WebGL + API nhúng
    assert "function pauseRendering()" in s and "function resumeRendering()" in s and "window.BioLab" in s
    # 5 hệ cơ quan + 6 cảnh hiển vi + 3 chế độ phòng
    for he in ("tuan_hoan", "ho_hap", "than_kinh", "bai_tiet", "tieu_hoa"):
        assert re.search(rf"\b{he}: \{{", s)
    for vm in ("pho_nang", "nephron", "synapse", "sarcomere", "te_bao_vien", "tieu_thuy_gan"):
        assert f"viMo.{vm} = " in s
    assert all(f'value="{m}"' in s for m in ("lab", "studio", "xray"))
    # AI của web: gọi máy chủ /api/sinh-hoc/hoi (CSRF), KHÔNG còn gọi thẳng Gemini / OpenAI bằng key trong trình duyệt;
    # câu trả lời được làm sạch + hành động được kiểm tra trước khi dùng; key cũ trong localStorage bị xoá
    assert "/api/sinh-hoc/hoi" in s and "X-CSRF-Token" in s and "lamSach(d.answer_html" in s and "hanhDongHopLe(d.action" in s
    assert "generativelanguage.googleapis.com" not in s and "api.openai.com" not in s and "x-goog-api-key" not in s
    assert 'luuXoa("bio-lab-key-gemini")' in s and 'luuXoa("bio-lab-key-openai")' in s
    assert "TODO" not in s and "placeholder:" not in s.lower()


def test_lop_ngoai_giai_phau_va_kham_pha():
    s = TEP.read_text(encoding="utf-8")
    # lớp cơ: MeshPhysicalMaterial + vân thớ (normal map) + xuyên sáng nhẹ; ngực – bụng bán trong suốt; thanh độ hiện 0→1
    assert "transmission: 0.15" in s and "thickness: 0.5" in s and "clearcoat: 0.3" in s and "uDoMoNguc" in s
    assert 'id="tt-lop-co"' in s and "vo.lopCo = Number(e.target.value) / 100" in s
    # phòng giải phẫu xám than + đèn studio + ACES / sRGB + bóng mềm
    assert "giai_phau: 0x2f2f2f" in s and "ACESFilmicToneMapping" in s and "SRGBColorSpace" in s and "PCFSoftShadowMap" in s
    # màu nội tạng / xương / mạch theo đặc tả
    for mau in ("0xe8a3a8", "0x8b3a2e", "0xd97b6c", "0xede3cf", "0xc0262e", "0x3b5ba8", "0xe6d27a"):
        assert mau in s, mau
    # khám phá: tìm cấu trúc, 7 cơ quan xem riêng (điểm nóng + thẻ), vùng / hệ / lớp, góc nhìn, tách, nhãn thông minh, hướng dẫn
    for cq in ("tim", "nao", "phoi", "gan", "than", "da_day", "ruot"):
        assert re.search(rf"\b{cq}: \{{ ten: ", s), cq
    for ham in ("function moCoQuan(", "function dongCoQuan(", "function taoDiemNong(", "function chonCauTruc(", "function capNhatDemCauTruc(",
                "function capNhatNhanThongMinh(", "function chonGocNhin(", "function datHeSoTach(", "function veLopV1(", "function denBuocHD("):
        assert ham in s, ham
    for el in ('id="o-tim"', 'id="cq-nhanh"', 'id="ds-vung"', 'id="mau-he"', 'id="ds-lop-v1"', 'id="tt-tach"', 'id="dem-cau-truc"',
               'id="nhan-thong-minh"', 'id="the-co-quan"', 'id="nut-huong-dan"'):
        assert el in s, el
    # AI có thể mở riêng một cơ quan
    assert "co_quan: typeof a.co_quan === \"string\" && THONG_TIN_CQ[a.co_quan]" in s


def _doc_bp3d():
    import gzip
    import json
    import struct
    du_lieu = gzip.decompress((TEP.parent / "sinh-hoc" / "bp3d.bin.gz").read_bytes())
    assert du_lieu[:4] == b"BP3D"
    phien_ban, n = struct.unpack("<II", du_lieu[4:12])
    return phien_ban, json.loads(du_lieu[12:12 + n]), len(du_lieu) - 12 - n


def test_bo_xuong_co_that_bodyparts3d():
    # file dữ liệu: đủ nhóm xương / cơ / mạch chi / da, kích thước dữ liệu khớp với khai báo, có ghi nguồn CC BY 4.0
    phien_ban, dau, con_lai = _doc_bp3d()
    assert phien_ban == 1 and "CC Attribution 4.0" in dau["nguon"]
    nhom = {g["bp"]: g for g in dau["nhom"]}
    for bp in ("hop_so", "cot_song", "xuong_suon", "xuong_chau", "xuong_dui", "xuong_canh_tay", "xuong_ban_tay", "xuong_ban_chan", "rang",
               "co_delta", "co_nhi_dau", "co_tu_dau_dui", "co_mong_lon", "co_thang", "co_thang_bung", "co_lung_rong", "co_cheo_bung",
               "gan_achilles", "dong_mach_ngoai_vi", "tinh_mach_ngoai_vi", "da"):
        assert bp in nhom, bp
    assert {g["lop"] for g in dau["nhom"]} == {"xuong", "co", "gan", "mach_do", "mach_xanh", "da"}
    can = sum(g["nv"] * 6 + (g["nv"] * 4 if g["uv"] else 0) + g["ni"] * (4 if g["i32"] else 2) for g in dau["nhom"])
    assert can == con_lai
    assert sum(g["ni"] for g in dau["nhom"]) // 3 < 600_000            # đủ nhẹ cho laptop thường
    # đã đặt khớp với nội tạng của trang: đỉnh đầu ~3,5, bàn chân dưới sàn cũ, khớp chi hai bên đối xứng
    k = dau["khop"]
    assert 3.4 < k["dinh_dau"] < 3.7 and -0.8 < k["day_chan"] < -0.4
    for ten in ("vai", "khuyu", "co_tay", "hong", "goi", "co_chan"):
        trai, phai = k["trai"][ten], k["phai"][ten]
        assert trai[0] > 0 > phai[0] and abs(trai[0] + phai[0]) < 0.02 and abs(trai[1] - phai[1]) < 0.02, ten
    assert k["trai"]["vai"][1] > k["trai"]["khuyu"][1] > k["trai"]["co_tay"][1] and k["trai"]["hong"][1] > k["trai"]["goi"][1] > k["trai"]["co_chan"][1]


def test_trang_tai_bp3d_va_du_phong():
    s = TEP.read_text(encoding="utf-8")
    # tải trước khi dựng cảnh, lỗi → mô hình dựng bằng code; mặc định chỉ hiện xương + nội tạng (da, cơ bật ở tab Các lớp)
    assert "/static/sinh-hoc/bp3d.bin.gz" in s and "DecompressionStream" in s and "const BP3D = await taiBP3D(" in s
    assert 'if (BP3D) { dungTuBP3D("xuong", "xuong"); return; }' in s and 'dungTuBP3D("co", "he_co")' in s and 'dungTuBP3D("da", "da")' in s
    assert "LOP_AN_MAC_DINH = BP3D ? { da: true, co: true } : {}" in s
    for bp in ("xuong_dui", "xuong_canh_tay", "xuong_cang_tay", "xuong_cang_chan", "xuong_vai", "co_khep_dui", "co_mong_nho", "da"):
        assert re.search(rf"\b{bp}: \{{ vi: ", s), bp
    # ghi công theo giấy phép CC BY 4.0
    assert "BodyParts3D © DBCLS · CC BY 4.0" in s and "bodyparts3d/lic.html" in s


def test_cong_cu_dung_bp3d():
    from tools import dung_bp3d as d
    import numpy as np
    # chia ngân sách: không vượt số tam giác gốc, phần dư rót cho phần tử khác
    assert d.chia_ngan_sach([(100, 1.0, 1.0), (10_000, 1.0, 1.0)], 5_000) == [100, 4900]
    # giảm đa giác giữ hai mặt của một tấm mỏng (pháp tuyến ngược chiều không bị gộp)
    luoi = np.stack(np.meshgrid(np.linspace(0, 1, 30), np.linspace(0, 1, 30)), -1).reshape(-1, 2)
    mat = [[i * 30 + j, i * 30 + j + 1, (i + 1) * 30 + j + 1] for i in range(29) for j in range(29)]
    mat += [[i * 30 + j, (i + 1) * 30 + j + 1, (i + 1) * 30 + j] for i in range(29) for j in range(29)]
    f = np.array(mat)
    v = np.r_[np.c_[luoi, np.zeros(900)], np.c_[luoi, np.full(900, 0.001)]]
    f = np.r_[f, f[:, ::-1] + 900]
    v2, f2 = d.giam(v, f, 400)
    assert 0 < len(f2) <= 440 and len(np.unique(np.round(v2[:, 2], 4))) == 2


def test_dieu_khien_tay_tich_hop():
    s = TEP.read_text(encoding="utf-8")
    assert 'id="btn-dieu-khien-tay"' in s
    assert 'import("/static/dieu-khien-tay/' in s
    assert "dieuKhienTay: {" in s
    assert "get gocCamera()" in s
    assert "tay: boDieuKhienTay" in s
    head_part = s.split("<body")[0]
    assert "tasks-vision" not in head_part
    assert "dieu-khien-tay" not in head_part



def test_ban_v2_da_bo_route_cu_chuyen_ve_sinh_hoc(client):
    """Bản v2 đã xoá (26/10/2026): file không còn, /sinh-hoc-v2 (menu app cũ, dấu trang) chuyển về /sinh-hoc."""
    assert not (TEP.parent / "biology-lab-3d-v2.html").exists()
    r = client.get("/sinh-hoc-v2", follow_redirects=False)
    assert r.status_code == 307 and r.headers["location"] == "/sinh-hoc"
