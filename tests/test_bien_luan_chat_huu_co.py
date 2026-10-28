"""Kiểm thử tích hợp Module Biện luận Chất hữu cơ 4 bước."""
from pathlib import Path
from backend.giai_nhanh import SYSTEM_GIAI_GON, YEU_CAU_HSG
from backend.ai_rieng.kho import SYSTEM_MAC_DINH


def test_rule_bien_luan_chat_huu_co_ton_tai():
    p = Path(".agents/rules/bien_luan_chat_huu_co.md")
    assert p.is_file(), "File rule .agents/rules/bien_luan_chat_huu_co.md phải tồn tại"
    noi_dung = p.read_text(encoding="utf-8")
    assert "MODULE: BIỆN LUẬN CHẤT HỮU CƠ" in noi_dung
    assert "1. $\\Delta$ & Nhóm chức" in noi_dung or "1. $\\Delta$" in noi_dung
    assert "2. Truy vết sơ đồ" in noi_dung
    assert "3. Tổng hợp CTCT" in noi_dung
    assert "4. Kiểm chứng (Bắt buộc)" in noi_dung
    assert "->" in noi_dung


def test_agents_md_co_chuan_bien_luan():
    p = Path("AGENTS.md")
    noi_dung = p.read_text(encoding="utf-8")
    assert "Chuẩn Biện Luận Hoá Hữu Cơ" in noi_dung
    assert ".agents/rules/bien_luan_chat_huu_co.md" in noi_dung


def test_prompt_giai_nhanh_tich_hop_module():
    assert "MODULE: BIỆN LUẬN CHẤT HỮU CƠ" in SYSTEM_GIAI_GON
    assert "Δ & Nhóm chức" in SYSTEM_GIAI_GON
    assert "Truy vết sơ đồ" in SYSTEM_GIAI_GON
    assert "Tổng hợp CTCT" in SYSTEM_GIAI_GON
    assert "Kiểm chứng (Bắt buộc)" in SYSTEM_GIAI_GON

    assert "MODULE: BIỆN LUẬN CHẤT HỮU CƠ" in YEU_CAU_HSG
    assert "Δ & Nhóm chức" in YEU_CAU_HSG
    assert "Truy vết sơ đồ" in YEU_CAU_HSG
    assert "Tổng hợp CTCT" in YEU_CAU_HSG
    assert "Kiểm chứng (Bắt buộc)" in YEU_CAU_HSG


def test_ai_rieng_kho_tich_hop_module():
    assert "MODULE: BIỆN LUẬN CHẤT HỮU CƠ" in SYSTEM_MAC_DINH
    assert "Δ & Nhóm chức" in SYSTEM_MAC_DINH
    assert "Truy vết sơ đồ" in SYSTEM_MAC_DINH
    assert "Tổng hợp CTCT" in SYSTEM_MAC_DINH
    assert "Kiểm chứng (Bắt buộc)" in SYSTEM_MAC_DINH
