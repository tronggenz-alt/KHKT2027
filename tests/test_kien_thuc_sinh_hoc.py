"""Dữ liệu kiến thức Phòng Lab Sinh học 3D (frontend/sinh-hoc/kien-thuc/): đủ khung 6 mục, 3–5 câu trắc nghiệm hợp lệ,
mã bộ phận khớp mô hình 3D (BO_PHAN trong biology-lab-3d.html), thuật ngữ thống nhất theo thuat-ngu.json."""
import json
import re
from pathlib import Path

import pytest

GOC = Path(__file__).resolve().parent.parent
THU_MUC = GOC / "frontend" / "sinh-hoc" / "kien-thuc"
TRANG = GOC / "frontend" / "biology-lab-3d.html"
MUC = ("vi_tri", "cau_tao", "chuc_nang", "co_che", "lien_he", "diem_thi")


def _doc(ten: str) -> dict:
    return json.loads((THU_MUC / ten).read_text(encoding="utf-8"))


DANH_MUC = _doc("danh-muc.json")["co_quan"]
THUAT_NGU = _doc("thuat-ngu.json")["thuat_ngu"]


def _bo_phan_tren_trang() -> set[str]:
    s = TRANG.read_text(encoding="utf-8")
    khoi = s[s.index("const BO_PHAN = {"):s.index("const CO_QUAN = {")]
    return set(re.findall(r"^  ([a-z0-9_]+): \{ vi:", khoi, re.M))


def _chu(gt) -> list[str]:
    if isinstance(gt, str):
        return [gt]
    if isinstance(gt, list):
        return [c for x in gt for c in _chu(x)]
    if isinstance(gt, dict):
        return [c for x in gt.values() for c in _chu(x)]
    return []


def test_danh_muc_khop_thu_muc_va_mo_hinh():
    ids = [c["id"] for c in DANH_MUC]
    assert len(ids) == len(set(ids)), "trùng id cơ quan"
    tep = {c["tep"] for c in DANH_MUC}
    co_that = {p.name for p in THU_MUC.glob("*.json")} - {"danh-muc.json", "thuat-ngu.json"}
    assert tep == co_that, f"file chưa có trong danh-muc.json hoặc danh mục trỏ tới file không tồn tại: {tep ^ co_that}"
    tren_trang = _bo_phan_tren_trang()
    assert len(tren_trang) > 50
    da_gan: dict[str, str] = {}
    for c in DANH_MUC:
        assert c["ten"] and c["icon"] and c["bo_phan"], c["id"]
        for bp in c["bo_phan"]:
            assert bp in tren_trang, f"{c['id']}: mã bộ phận '{bp}' không có trên mô hình 3D"
            assert bp not in da_gan, f"'{bp}' thuộc cả {da_gan[bp]} và {c['id']}"
            da_gan[bp] = c["id"]


@pytest.mark.parametrize("muc", DANH_MUC, ids=lambda m: m["id"])
def test_moi_co_quan_du_khung_va_trac_nghiem(muc):
    d = _doc(muc["tep"])
    assert d["id"] == muc["id"] and d["ten"]
    assert isinstance(d["can_duyet"], bool)
    for m in MUC:
        assert isinstance(d[m], list) and d[m], f"{muc['id']}: thiếu mục {m}"
        assert all(isinstance(x, str) and x.strip() for x in d[m]), f"{muc['id']}.{m} có dòng trống"
    tn = d["trac_nghiem"]
    assert 3 <= len(tn) <= 5, f"{muc['id']}: cần 3–5 câu trắc nghiệm"
    for i, c in enumerate(tn, 1):
        assert c["cau"].strip() and c["giai_thich"].strip(), f"{muc['id']} câu {i}"
        assert len(c["lua_chon"]) == 4 and len(set(c["lua_chon"])) == 4 and all(x.strip() for x in c["lua_chon"]), f"{muc['id']} câu {i}"
        assert c["dap_an"] in ("A", "B", "C", "D"), f"{muc['id']} câu {i}: đáp án phải là A/B/C/D"
    gy = d["goi_y_hoi"]
    assert 2 <= len(gy) <= 4 and all(x.strip().endswith("?") for x in gy), f"{muc['id']}: goi_y_hoi"
    assert isinstance(d.get("mo_rong", []), list)


def test_dap_an_khong_don_mot_chu():
    dem = {}
    for muc in DANH_MUC:
        for c in _doc(muc["tep"])["trac_nghiem"]:
            dem[c["dap_an"]] = dem.get(c["dap_an"], 0) + 1
    tong = sum(dem.values())
    assert set(dem) == {"A", "B", "C", "D"} and max(dem.values()) <= tong * 0.4, f"đáp án dồn về một chữ: {dem}"


def test_thuat_ngu_thong_nhat():
    loi = []
    for muc in DANH_MUC:
        for dong in _chu(_doc(muc["tep"])):
            for t in THUAT_NGU:
                for mau in t["tranh"]:
                    if re.search(mau, dong, re.I):
                        loi.append(f"{muc['tep']}: '{re.search(mau, dong, re.I).group(0)}' → dùng '{t['chuan']}'")
    assert not loi, "\n".join(loi)


# từ khoá tìm kiếm không dấu (tu: [...]) và tên gọi cũ (cu: "…") cố ý giữ cách viết học sinh hay gõ
BO_QUA = re.compile(r'\btu: \[|\bcu: "')
VUOT_CHUONG_TRINH = ("DHP", "RyR", "SERCA", "troponin", "kênh If", "aquaporin", "mOsm", "ATPase", "cAMP", "PKA", "Nernst",
                     "Henderson", "nicotinic", "CICR", "P450", "Krebs", "Cori", "β₁", "β₂", "M₂")


def test_thuat_ngu_trang_3d_va_knowledge():
    nguon = [(f"biology-lab-3d.html:{so}", d) for so, d in enumerate(TRANG.read_text(encoding="utf-8").splitlines(), 1)
             if not BO_QUA.search(d)]
    for tep in sorted((GOC / "knowledge").glob("*.md")):
        nguon += [(f"knowledge/{tep.name}:{so}", d) for so, d in enumerate(tep.read_text(encoding="utf-8").splitlines(), 1)]
    mau = [(re.compile(m, re.I), t["chuan"]) for t in THUAT_NGU for m in t["tranh"]]
    loi = [f"{noi}: '{m.group(0)}' → dùng '{chuan}'" for noi, d in nguon for r, chuan in mau if (m := r.search(d))]
    assert not loi, "\n".join(loi)


def test_kien_thuc_vuot_chuong_trinh_chi_o_tab_mo_rong():
    """Giải phẫu / Cơ chế / câu gợi ý / AI nội bộ ở mức SGK; hoá sinh mức đại học chỉ nằm trong tab Mở rộng (hoa_sinh)."""
    s = TRANG.read_text(encoding="utf-8")
    khoi = re.sub(r"hoa_sinh: `.*?`,", "", s[s.index("const CO_QUAN = {"):s.index("const MAU_NEN = {")], flags=re.S)
    loi = [f"'{tu}': {d.strip()[:100]}" for d in khoi.splitlines() if not BO_QUA.search(d) for tu in VUOT_CHUONG_TRINH if tu in d]
    assert not loi, "\n".join(loi)
    assert ">Mở rộng</button>" in s and "Hoá sinh</button>" not in s
    assert "[t.vi, t.cu, t.en, t.la]" in s, "ô tìm cấu trúc phải tìm cả tên gọi cũ (cu)"
