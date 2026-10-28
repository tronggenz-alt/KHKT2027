"""Mục 05 "Cơ chế phản ứng" v2 (mức HSG quốc gia): bộ đẩy electron (2e/1e/pericyclic),
kiểm tra tự động bằng RDKit, vẽ SVG (mũi tên đầy đủ, nửa đầu, TS ‡, thuận nghịch ⇌, nhãn trung gian),
xuất CDXML cho ChemDraw, và API /api/co-che. Không gọi mạng/AI."""
from __future__ import annotations

import json
import xml.etree.ElementTree as ET
from types import SimpleNamespace

import pytest
from rdkit import Chem

from backend import co_che
from backend.co_che import (
    CoChe,
    LoiCoChe,
    MuiTen,
    ap_mui_ten,
    cac_manh_smiles,
    dung_tu_mui_ten,
    kiem_tra,
    tao_co_che,
    tu_json,
    tu_smiles,
)
from backend.ve_co_che import _cung_tron, ve, ve_svg, xuat_cdxml

SN2_JSON = {
    "ten": "SN2",
    "loai": "SN2",
    "nguon": "ai",
    "buoc": [{
        "chat": ["[OH-:1]", "[CH3:2][CH2:3][Br:4]"],
        "mui_ten": [
            {"tu": {"loai": "cap_e", "nguyen_tu": 1}, "den": {"loai": "nguyen_tu", "nguyen_tu": 3}, "kieu": "day_du"},
            {"tu": {"loai": "lien_ket", "nguyen_tu": [3, 4]}, "den": {"loai": "nguyen_tu", "nguyen_tu": 4}, "kieu": "day_du"},
        ],
        "trang_thai_chuyen_tiep": True,
        "buoc_cham": True,
        "thuan_nghich": False,
        "nhan_trung_gian": "trạng thái chuyển tiếp SN2",
        "ghi_chu": "OH- tấn công từ phía sau",
    }],
    "san_pham": ["[CH3:2][CH2:3][OH:1]", "[Br-:4]"],
}

GOC_TU_DO_JSON = {
    "ten": "Tạo gốc brom",
    "loai": "GOC_TU_DO",
    "nguon": "ai",
    "buoc": [{
        "chat": ["[Br:1][Br:2]"],
        "mui_ten": [
            {"tu": {"loai": "lien_ket", "nguyen_tu": [1, 2]}, "den": {"loai": "nguyen_tu", "nguyen_tu": 1}, "kieu": "nua_dau"},
            {"tu": {"loai": "lien_ket", "nguyen_tu": [1, 2]}, "den": {"loai": "nguyen_tu", "nguyen_tu": 2}, "kieu": "nua_dau"},
        ],
        "trang_thai_chuyen_tiep": False,
        "buoc_cham": False,
        "thuan_nghich": True,
        "ghi_chu": "Phân cắt đồng ly dưới ánh sáng (hν)",
    }],
    "san_pham": ["[Br:1]", "[Br:2]"],
}


def _sp(cc: CoChe) -> set[str]:
    return set(cac_manh_smiles(tu_smiles(cc.san_pham), giu_map=False))


@pytest.fixture(autouse=True)
def khong_ai(monkeypatch):
    monkeypatch.setattr(co_che, "ai_san_sang", lambda: False)


# ================================================================ MuiTen & kiểu mũi tên
def test_mui_ten_kieu_day_du_va_nua_dau():
    mt_2e = MuiTen.tu_dict({"tu": {"loai": "cap_e", "nguyen_tu": 1}, "den": {"loai": "nguyen_tu", "nguyen_tu": 2}, "kieu": "day_du"})
    assert mt_2e.so_e == 2 and mt_2e.kieu == "day_du"

    mt_1e = MuiTen.tu_dict({"tu": {"loai": "lien_ket", "nguyen_tu": [1, 2]}, "den": {"loai": "nguyen_tu", "nguyen_tu": 1}, "kieu": "nua_dau"})
    assert mt_1e.so_e == 1 and mt_1e.kieu == "nua_dau"


def test_mui_ten_bat_loi_dau_cuoi():
    with pytest.raises(LoiCoChe, match="cần 1 nguyên tử"):
        MuiTen.tu_dict({"tu": {"loai": "cap_e", "nguyen_tu": [1, 2]}, "den": {"loai": "nguyen_tu", "nguyen_tu": 3}})
    with pytest.raises(LoiCoChe, match="chính nguyên tử"):
        MuiTen("cap_e", (1,), "nguyen_tu", (1,)).dong_e()


# ================================================================ bộ đẩy electron & ap_mui_ten
def test_dinh_dang_json_dac_ta_qua_kiem_tra():
    cc = tu_json(SN2_JSON)
    assert kiem_tra(cc) == [] and cc.to_json()["san_pham"] == ["CCO", "[Br-]"]


def test_ap_mui_ten_sn2():
    mol = tu_smiles(["[OH-:1]", "[CH3:2][CH2:3][Br:4]"])
    sau = ap_mui_ten(mol, [
        MuiTen("cap_e", (1,), "nguyen_tu", (3,)),
        MuiTen("lien_ket", (3, 4), "nguyen_tu", (4,)),
    ])
    assert set(cac_manh_smiles(sau, giu_map=False)) == {"CCO", "[Br-]"}


def test_ap_mui_ten_dong_ly_goc_tu_do():
    mol = tu_smiles(["[Br:1][Br:2]"])
    sau = ap_mui_ten(mol, [
        MuiTen("lien_ket", (1, 2), "nguyen_tu", (1,), "nua_dau"),
        MuiTen("lien_ket", (1, 2), "nguyen_tu", (2,), "nua_dau"),
    ])
    frags = cac_manh_smiles(sau, giu_map=False)
    assert len(frags) == 2 and all("Br" in s for s in frags)


def test_chuyen_h_rut_gon():
    mol = tu_smiles(["[CH3:1][C:2](=[OH+:3])[OH:5]"])
    sau = ap_mui_ten(mol, [], chuyen_h=[(3, 5)])
    bd = co_che.ban_do(sau)
    assert sau.GetAtomWithIdx(bd[3]).GetFormalCharge() == 0
    assert sau.GetAtomWithIdx(bd[5]).GetFormalCharge() == 1


@pytest.mark.parametrize("sua, loi", [
    (lambda d: d.update(san_pham=["[CH3:2][CH2:3][Br:4]"]), "sai nhóm nguyên tử"),
    (lambda d: d["buoc"][0].update(chat=["[OH-:1]", "C[CH2:3]Brr"]), "SMILES không hợp lệ"),
    (lambda d: d["buoc"][0]["mui_ten"][0]["den"].update(nguyen_tu=9), "map 9"),
    (lambda d: d["buoc"][0].update(chat=["[OH-:1]", "[CH3:1][CH2:3][Br:4]"]), "trùng"),
])
def test_kiem_tra_bat_loi(sua, loi):
    d = json.loads(json.dumps(SN2_JSON))
    sua(d)
    try:
        ds = kiem_tra(tu_json(d))
    except LoiCoChe as e:
        ds = [str(e)]
    assert any(loi in x for x in ds), ds


# ================================================================ dựng cơ chế từ mũi tên
def test_dung_tu_mui_ten():
    cc = dung_tu_mui_ten(
        "SN2", "SN2", ["[OH-:1]", "[CH3:2][CH2:3][Br:4]"],
        [{
            "mui_ten": [
                MuiTen("cap_e", (1,), "nguyen_tu", (3,)),
                MuiTen("lien_ket", (3, 4), "nguyen_tu", (4,)),
            ],
            "trang_thai_chuyen_tiep": True,
            "buoc_cham": True,
            "nhan_trung_gian": "TS",
            "ghi_chu": "OH- thế Br",
        }]
    )
    assert len(cc.buoc) == 1 and cc.buoc[0].buoc_cham is True
    assert set(cac_manh_smiles(tu_smiles(cc.san_pham), giu_map=False)) == {"CCO", "[Br-]"}
    assert kiem_tra(cc) == []


# ================================================================ vẽ SVG & CDXML
def test_svg_sn2_co_mui_ten_cong_ts_va_nhan_trung_gian():
    cc = tu_json(SN2_JSON)
    hinh = ve(cc)
    root = ET.fromstring(hinh.svg)
    assert root.get("viewBox") and len(hinh.mui_ten_cong) == 2 and hinh.ngoac and "‡" in hinh.svg
    assert "stroke-dasharray" in hinh.svg
    assert "trạng thái chuyển tiếp SN2" in hinh.svg
    assert "(chậm)" in hinh.svg


def test_svg_goc_tu_do_mui_ten_nua_dau_va_thuan_nghich():
    cc = tu_json(GOC_TU_DO_JSON)
    hinh = ve(cc)
    assert len(hinh.mui_ten_cong) == 2
    assert any(m.get("kieu") == "nua_dau" for m in hinh.mui_ten_cong)
    assert any(p.get("thuan_nghich") is True for p in hinh.mui_ten_pu)
    root = ET.fromstring(hinh.svg)
    assert root.tag.endswith("svg")


def test_cdxml_hop_le_rdkit_doc_lai():
    cc = tu_json(SN2_JSON)
    s = xuat_cdxml(cc)
    assert "<CDXML" in s
    root = ET.fromstring(s.split("\n", 2)[2])
    assert len([a for a in root.iter("arrow") if a.get("AngularSize")]) == 2
    assert len([b for b in root.iter("b") if b.get("Display") == "Dash"]) == 2
    assert len([g for g in root.iter("graphic") if g.get("GraphicType") == "Bracket"]) == 1
    mols = [m for m in Chem.MolsFromCDXML(s) if m is not None]
    assert {Chem.MolToSmiles(m) for m in mols} >= {"[OH-]", "CCBr", "CCO", "[Br-]"}


def test_cdxml_mui_ten_nua_dau_chua_half_left():
    cc = tu_json(GOC_TU_DO_JSON)
    s = xuat_cdxml(cc)
    assert 'ArrowheadHead="HalfLeft"' in s


def test_dau_goc_cung_theo_chemdraw():
    _, _, goc = _cung_tron((0, 0), (50, -30), (100, 0))
    assert goc < 0
    _, _, goc = _cung_tron((0, 0), (50, 30), (100, 0))
    assert goc > 0


# ================================================================ API /api/co-che
def test_api_co_che(client, dang_nhap, monkeypatch):
    assert client.post("/api/co-che", json={"van_ban": "CCBr.[OH-]>>CCO.[Br-]"}).status_code == 403
    h = {"X-CSRF-Token": client.get("/api/toi").json()["csrf"]}
    assert client.post("/api/co-che", json={"van_ban": "CCBr.[OH-]"}, headers=h).status_code == 401

    h = {"X-CSRF-Token": dang_nhap(client)}

    monkeypatch.setattr(co_che, "ai_san_sang", lambda: True)

    class PhienGia:
        def gui(self, text, **kwargs):
            return SimpleNamespace(text=json.dumps(SN2_JSON))

    monkeypatch.setattr(co_che, "_phien_manh", lambda bao: PhienGia())

    r = client.post("/api/co-che", json={"van_ban": "CCBr + OH- -> CCO + Br-", "loai": "SN2"}, headers=h)
    assert r.status_code == 200
    d = r.json()
    assert d["nguon"] == "ai" and d["svg"].startswith("<svg") and "<CDXML" in d["cdxml"]
    assert len(d["co_che"]["buoc"]) == 1 and d["co_che"]["buoc"][0]["buoc_cham"] is True
