"""Chạy toàn bộ pipeline ở chế độ THẬT (không demo) với AI giả lập theo kịch bản – không gọi mạng.

Luồng mới (giai_nhanh): phân loại → MỘT lần gọi giải (streaming từng đoạn chữ → hiện dần từng chất/bước) → RDKit bắt lỗi →
mức Cân bằng nhờ mô hình rẻ sửa → vẽ/xuất file dạng sơ đồ chuỗi.
"""
import copy
import json
from pathlib import Path

import pytest

from backend import do_kho, giai_nhanh, llm, pipeline
from backend.llm import PhanHoi
from backend.schemas import LoiGiai

DEMO = Path(__file__).resolve().parent.parent / "backend" / "demo_data"
BAI = json.loads((DEMO / "metan_etyl_axetat.json").read_text(encoding="utf-8"))
CHUOI = "CH4 → C2H2 → CH3CHO → C2H5OH → CH3COOH → CH3COOC2H5"
GON = giai_nhanh.gon_tu_loi_giai(LoiGiai.model_validate(BAI["loi_giai"]))


@pytest.fixture
def ai_gia(monkeypatch, tmp_path):
    """AI giả: phân loại "trung_binh" (mức Cân bằng); lần giải đầu SMILES của C2H2 hỏng; bản sửa (phiên Flash-Lite mới) đúng."""
    nhat_ky = {"phien": [], "giai": 0, "sua_text": "", "sai_mai": False}

    def gui(self, text=None, anh=None, ket_qua_cong_cu=None, khi_co_chu=None):
        nhat_ky["phien"].append((self.system, self.bac, self.suy_nghi))
        if self.system == do_kho.SYSTEM_PHAN_LOAI:
            return PhanHoi('{"muc_do": "trung_binh", "ly_do": "chuỗi SGK"}', nha_cung_cap="gemini", model="lite-gia")
        if self.system == giai_nhanh.SYSTEM_GIAI_GON:
            nhat_ky["giai"] += 1
            gon = copy.deepcopy(GON)
            if nhat_ky["giai"] == 1 or nhat_ky["sai_mai"]:
                gon["chuoi"][1]["smiles"] = "C(#C"
                if nhat_ky["sai_mai"]:
                    gon["buoc"][1]["tu"] = "Z"   # bước nối sai kí hiệu → đánh dấu "cần kiểm tra" ở bước 2
            else:
                nhat_ky["sua_text"] = text
            chu = json.dumps(gon, ensure_ascii=False)
            if khi_co_chu:  # streaming: gửi từng đoạn 37 ký tự
                khi_co_chu(None)
                for i in range(0, len(chu), 37):
                    khi_co_chu(chu[i:i + 37])
            return PhanHoi(chu, [], "gemini", f"{self.bac}-gia")
        raise AssertionError(f"system prompt lạ: {self.system[:60]}")

    monkeypatch.setattr(llm.PhienHoiThoai, "gui", gui)
    monkeypatch.setattr(pipeline, "OUTPUT_DIR", tmp_path)
    return nhat_ky


def test_pipeline_that_voi_ai_gia(ai_gia):
    job = pipeline.tao_job()
    pipeline.chay(job, CHUOI, None, hien_chrome=False, demo=False)
    thong_diep = [e["thong_diep"] for e in job.su_kien]

    assert job.loi is None, job.loi
    assert job.su_kien[-1]["giai_doan"] == "xong" and job.su_kien[-1]["thong_diep"].startswith("Hoàn tất sau")
    # mức tự động: Trung bình → Cân bằng (Flash, suy nghĩ ít); MỘT lần gọi giải gộp tra cứu
    assert any("Mức Cân bằng (tự động theo độ khó) → Gemini Flash" in m for m in thong_diep) and job.muc_tm == "can_bang"
    phien_giai = [(b, n) for s, b, n in ai_gia["phien"] if s == giai_nhanh.SYSTEM_GIAI_GON]
    assert phien_giai == [("flash", 1024), ("lite", 0)]  # giải bằng Flash, sửa bằng mô hình rẻ (Flash-Lite)
    # streaming: từng chất / bước hiện dần trong tiến trình
    tam = [e for e in job.su_kien if e.get("buoc_tam") is not None and e["trang_thai"] == "dang_chay"]
    assert len(tam) >= 5 and [len(e["buoc_tam"]) for e in tam] == sorted(len(e["buoc_tam"]) for e in tam)
    assert any(m.startswith("Bước 1: CH4 → C2H2") for m in thong_diep)
    # RDKit bắt SMILES hỏng → mô hình rẻ sửa (gửi kèm JSON + lỗi)
    assert any("RDKit báo 1 lỗi" in m and "mô hình rẻ" in m for m in thong_diep)
    assert "SMILES không hợp lệ: C(#C" in ai_gia["sua_text"] and '"chuoi"' in ai_gia["sua_text"]
    kq = job.ket_qua
    assert kq["canh_bao"] == [] and kq["muc_thong_minh"] == "can_bang" and kq["thoi_gian_giai"] >= 0 and kq["tu_cache"] is False
    assert len(kq["gon"]["buoc"]) == 5 and kq["gon"]["luu_y"] is not None
    assert all(b["phuong_trinh"] == "" for b in kq["loi_giai"]["cac_buoc"])  # không sinh phương trình đầy đủ
    for ten in ("dap_an.png", "ket_qua.zip", "loi_giai.json", "so_do.cdxml", "tat_ca_chat.sdf"):
        assert (job.thu_muc / ten).exists(), ten


def test_ai_khong_sua_duoc_thi_danh_dau_can_kiem_tra(ai_gia):
    ai_gia["sai_mai"] = True
    job = pipeline.tao_job()
    pipeline.chay(job, CHUOI, None, hien_chrome=False, demo=False)
    assert job.loi is None
    kq = job.ket_qua
    assert any("C2H2" in x for x in kq["canh_bao"]) and kq["loi_giai"]["cac_buoc"][1]["can_kiem_tra"] is True
    assert sum(s == giai_nhanh.SYSTEM_GIAI_GON for s, _, _ in ai_gia["phien"]) == 2  # Cân bằng: sửa tối đa 1 vòng
    assert any("Còn" in e["thong_diep"] and "cần kiểm tra lại" in e["thong_diep"] for e in job.su_kien)
