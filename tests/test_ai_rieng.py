"""AI Riêng – Gia sư Hoá Hữu Cơ: kho tri thức (tài liệu → đoạn, quy tắc, bài mẫu, sửa sai), tìm lai (vector giả + FTS5), giải 3 tầng
(thư viện 0 token → kiến thức + mô hình rẻ → mô hình mạnh khi RDKit báo sai), chế độ nghiêm ngặt, báo sai → hàng chờ, Sân tập,
Bộ đề kiểm tra (chấm RDKit), sao lưu JSON, quyền admin. AI giả ở tầng gọi Gemini; embedding giả (băm từ) – không gọi mạng."""
import io
import json
import re
import time
import zipfile
from types import SimpleNamespace

import numpy as np
import pytest

from backend import db, giai_nhanh, llm, pipeline
from backend.ai.providers.gemini import chat as gemini_chat
from backend.ai_rieng import cham, giai as ar_giai, kho, nhung, sao_luu, tach, tim, xu_ly
from backend.bao_mat import gioi_han_tan_suat
from backend.config import settings
from test_chon_mo_hinh import _cho_xong, _dang_nhap_email

DE = "CH3COOH → CH3COOC2H5 → C2H5OH"
LG = {"chuoi": [{"ki_hieu": "CH3COOH", "ctct": "CH3COOH", "ten": "acetic acid", "smiles": "CC(=O)O"},
                {"ki_hieu": "CH3COOC2H5", "ctct": "CH3COOC2H5", "ten": "ethyl acetate", "smiles": "CCOC(C)=O"},
                {"ki_hieu": "C2H5OH", "ctct": "CH3CH2OH", "ten": "ethanol", "smiles": "CCO"}],
      "buoc": [{"tu": "CH3COOH", "den": "CH3COOC2H5", "phan_ung": "ester hoá", "dieu_kien": "C2H5OH, H2SO4 đặc, t°"},
               {"tu": "CH3COOC2H5", "den": "C2H5OH", "phan_ung": "thuỷ phân ester", "dieu_kien": "NaOH, t°"}],
      "luu_y": [], "dap_an": "CH3COOC2H5 (ethyl acetate) → C2H5OH"}
DE2 = "C2H4 → C2H5OH → CH3CHO"
LG2 = {"chuoi": [{"ki_hieu": "C2H4", "ctct": "CH2=CH2", "ten": "ethylene", "smiles": "C=C"},
                 {"ki_hieu": "C2H5OH", "ctct": "CH3CH2OH", "ten": "ethanol", "smiles": "CCO"},
                 {"ki_hieu": "CH3CHO", "ctct": "CH3CHO", "ten": "acetaldehyde", "smiles": "CC=O"}],
       "buoc": [{"tu": "C2H4", "den": "C2H5OH", "phan_ung": "cộng nước", "dieu_kien": "H2O, H+, t°", "can_cu": "[Q1]"},
                {"tu": "C2H5OH", "den": "CH3CHO", "phan_ung": "oxi hoá bằng CuO", "dieu_kien": "CuO, t°", "can_cu": "[T1]"}],
       "luu_y": [], "dap_an": "CH3CHO", "chua_duoc_day": [], "mau_thuan": []}


def nhung_gia(ds, truy_van=False):
    """Embedding giả: túi từ băm vào 768 chiều (đoạn chung từ → cosine dương)."""
    m = np.zeros((len(ds), settings.embed_dim), dtype=np.float32)
    for i, t in enumerate(ds):
        for tu in re.findall(r"\w+", kho.chu_tim(t).lower()):
            m[i, hash(tu) % settings.embed_dim] += 1
    n = np.linalg.norm(m, axis=1, keepdims=True)
    return m / np.where(n == 0, 1, n)


class GeminiGia:
    """Trả lời mọi lượt gọi Gemini theo system prompt; ghi lại system + chữ người dùng (để kiểm tra khối kiến thức trong prompt)."""

    def __init__(self, monkeypatch):
        self.goi: list[dict] = []
        self.tra_loi = json.dumps(LG2, ensure_ascii=False)
        self.lan_sai = 0  # số lần đầu trả SMILES hỏng (RDKit bắt → tầng 3)
        self.theo_mo_hinh: dict[str, str] = {}  # mô hình → câu trả lời riêng (làn song song khác làn chính)
        self.cham: dict[str, float] = {}         # mô hình → trả lời chậm N giây (làn đối chiếu xong sau)
        monkeypatch.setattr(gemini_chat, "_goi_gemini", self._gemini)

    def _gemini(self, **kw):
        chu = " ".join(getattr(p, "text", "") or "" for c in kw["contents"] for p in (getattr(c, "parts", None) or []))
        self.goi.append({"model": kw["uu_tien"], "system": kw["system"], "chu": chu})
        time.sleep(self.cham.get(kw["uu_tien"], 0))
        if kw["system"].startswith(giai_nhanh.SYSTEM_GIAI_GON):
            d = json.loads(self.theo_mo_hinh.get(kw["uu_tien"], self.tra_loi))
            if self.lan_sai > 0:
                self.lan_sai -= 1
                d["chuoi"][1]["smiles"] = "C(C"
            text = json.dumps(d, ensure_ascii=False)
        elif kw["system"] == cham.SYSTEM_CHAM:
            text = json.dumps({"ket_qua": [{"ki_hieu": k, "dung": True} for k in re.findall(r"^(\S+): ĐÁP ÁN", chu, re.M)]})
        elif kw["system"] == xu_ly.SYSTEM_TRICH:
            text = json.dumps({"bai": [{"de": DE, "trang": 1, "chuoi": LG["chuoi"], "buoc": LG["buoc"], "dap_an": LG["dap_an"]}]})
        else:
            text = "chữ đọc được từ ảnh"
        part = SimpleNamespace(text=text, thought=False, function_call=None)
        resp = SimpleNamespace(candidates=[SimpleNamespace(content=SimpleNamespace(parts=[part]), finish_reason="STOP")],
                               usage_metadata=SimpleNamespace(prompt_token_count=800, candidates_token_count=200, thoughts_token_count=0))
        return resp, kw["uu_tien"], kw["contents"]


@pytest.fixture
def ar(monkeypatch, cau_hinh, tmp_path):
    for ten, gt in (("gemini_model_lite", "lite-test"), ("gemini_model", "flash-test"), ("gemini_model_hsg", "pro-test"),
                    ("model", "claude-test"), ("free_trial_limit", 30)):
        monkeypatch.setattr(settings, ten, gt)
    monkeypatch.setattr(llm, "_claude_tam_dung_den", 0.0)
    monkeypatch.setattr(pipeline, "OUTPUT_DIR", tmp_path / "out")
    monkeypatch.setattr(nhung, "nhung", nhung_gia)
    monkeypatch.setattr(nhung, "_bo_nho", {"phien_ban": None, "ma_tran": None, "nhan": []})
    monkeypatch.setattr(xu_ly, "_nhung_loi", {"luc": 0.0, "ly_do": ""})
    gioi_han_tan_suat.xoa()
    return GeminiGia(monkeypatch)


@pytest.fixture
def ad(monkeypatch, client):
    return _dang_nhap_email(monkeypatch, client, "admin@example.com", admin=True)


def _docx(*doan: str, bang: list[list[str]] | None = None) -> bytes:
    w = 'xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"'
    than = "".join(f"<w:p><w:r><w:t>{d}</w:t></w:r></w:p>" for d in doan)
    if bang:
        than += "<w:tbl>" + "".join("<w:tr>" + "".join(f"<w:tc><w:p><w:r><w:t>{o}</w:t></w:r></w:p></w:tc>" for o in h) + "</w:tr>"
                                    for h in bang) + "</w:tbl>"
    buf = io.BytesIO()
    with zipfile.ZipFile(buf, "w") as z:
        z.writestr("word/document.xml", f'<?xml version="1.0" encoding="UTF-8"?><w:document {w}><w:body>{than}</w:body></w:document>')
    return buf.getvalue()


# ================================================================ chuẩn hoá, tách đoạn, đọc file
def test_khoa_de_va_tach_doan():
    assert kho.khoa_de("CH3COOH -> CH3COOC2H5 →  C2H5OH.") == kho.khoa_de(DE) == kho.khoa_de("CH₃COOH ⟶ CH₃COOC₂H₅ → C₂H₅OH")
    assert kho.khoa_de("Etilen → Ancol etylic") == kho.khoa_de("etilen → ancol Etylic")
    assert kho.khoa_de("CO → X") != kho.khoa_de("Co → X")
    chu = "\n\n".join(f"Đoạn {i}. " + "Ancol etylic tác dụng với Na giải phóng H2. " * 12 for i in range(12))
    ds = tach.tach_doan([(1, chu[:3000]), (2, chu[3000:])])
    assert len(ds) >= 3 and all(len(d) <= 650 * 4 + 200 for _, d in ds) and ds[0][0] == 1 and ds[-1][0] == 2
    assert ds[1][1][:60] in ds[0][1]  # chồng lấp
    assert tach.tach_doan([(None, "ngắn")]) == [(None, "ngắn")]
    text = tach.doc_docx(_docx("Anđehit tráng bạc", bang=[["Chất", "Phản ứng"], ["HCHO", "AgNO3/NH3"]]))
    assert "Anđehit tráng bạc" in text and "HCHO | AgNO3/NH3" in text
    assert tach.loai_tu_ten("a.pdf", b"%PDF-1.4") == "pdf" and tach.loai_tu_ten("b.docx", b"PK..") == "docx"
    with pytest.raises(tach.LoiTaiLieu):
        tach.loai_tu_ten("c.exe", b"MZ")
    with pytest.raises(tach.LoiTaiLieu):
        tach.tai_link("http://127.0.0.1:8000/admin")  # chặn địa chỉ nội bộ (SSRF)
    with pytest.raises(tach.LoiTaiLieu):
        tach.tai_link("file:///etc/passwd")


# ================================================================ quyền
def test_chi_admin(ar, client, monkeypatch):
    hs = _dang_nhap_email(monkeypatch, client, "hs@example.com")
    assert hs.get("/admin/api/ai-rieng/tong-quan").status_code == 403
    assert hs.post("/admin/api/ai-rieng/kien-thuc", json={"tieu_de": "x", "noi_dung": "y"}, headers=hs.h).status_code == 403
    assert hs.get("/admin/ai-rieng").status_code == 403
    toi = hs.get("/api/toi").json()
    assert toi["ai_rieng"] == {"co": False, "mo_cho_nguoi_dung": False}
    kho.dat_cai_dat({"mo_cho_nguoi_dung": True})
    assert hs.get("/api/toi").json()["ai_rieng"]["co"] is True


# ================================================================ tài liệu → đoạn → tìm
def test_nap_tai_lieu_tim_kien_thuc(ar, ad):
    r = ad.post("/admin/api/ai-rieng/tai-lieu/van-ban", headers=ad.h,
                json={"ten": "SGK Hoá 11 – Anđehit", "noi_dung": "Oxi hoá ancol etylic bằng CuO đun nóng thu được anđehit axetic CH3CHO. " * 3,
                      "chu_de": "anđehit"})
    assert r.status_code == 200, r.text
    tid = r.json()["id"]
    ad.post("/admin/api/ai-rieng/tai-lieu", headers=ad.h, data={"chu_de": "ancol"},
            files=[("tep", ("ancol.docx", _docx("Ancol etylic C2H5OH tác dụng Na giải phóng H2."), "application/vnd.openxmlformats"))])
    ad.post("/admin/api/ai-rieng/tai-lieu", headers=ad.h, files=[("tep", ("virus.exe", b"MZ....", "application/octet-stream"))])
    xu_ly.xu_ly_het()
    ds = {t["ten"]: t for t in ad.get("/admin/api/ai-rieng/tai-lieu").json()["tai_lieu"]}
    assert ds["SGK Hoá 11 – Anđehit"]["trang_thai"] == "da_nap" and ds["ancol"]["trang_thai"] == "da_nap" and "virus" not in ds
    doan = ad.get(f"/admin/api/ai-rieng/tai-lieu/{tid}/doan").json()["doan"]
    assert doan and all(d["co_vec"] for d in doan)
    kid = ad.post("/admin/api/ai-rieng/kien-thuc", headers=ad.h, json={"tieu_de": "Cộng nước vào anken", "noi_dung":
                  "C2H4 + H2O (H+, t°) → C2H5OH; anken bất đối xứng theo Markovnikov.", "chu_de": "anken", "uu_tien": 5}).json()["id"]
    xu_ly.xu_ly_het()
    muc = tim.tim_kien_thuc(DE2)
    loai = [m.loai for m in muc]
    assert "kien_thuc" in loai and "doan" in loai and [m.nhan for m in muc][0] == "Q1"
    khoi = tim.khoi_prompt(muc, "bo_sung")
    assert "[Q1] QUY TẮC «Cộng nước vào anken»" in khoi and "kiến thức chung" in khoi
    # tắt tài liệu → không còn được tìm thấy; FTS vẫn chạy khi không có embedding
    ad.post(f"/admin/api/ai-rieng/tai-lieu/{tid}", headers=ad.h, json={"bat": False})
    assert not any(m.loai == "doan" and "Anđehit" in m.tieu_de for m in tim.tim_kien_thuc(DE2))
    assert ad.delete(f"/admin/api/ai-rieng/kien-thuc/{kid}", headers=ad.h).json()["ok"]


def test_trich_bai_co_loi_giai_tu_tai_lieu(ar, ad):
    ad.post("/admin/api/ai-rieng/tai-lieu/van-ban", headers=ad.h, json={
        "ten": "Đề + lời giải", "co_loi_giai": True,
        "noi_dung": f"Bài 1: {DE}. Lời giải: CH3COOH + C2H5OH ⇌ CH3COOC2H5 + H2O; CH3COOC2H5 + NaOH → CH3COONa + C2H5OH."})
    xu_ly.xu_ly_het()
    tl = ad.get("/admin/api/ai-rieng/tai-lieu").json()["tai_lieu"][0]
    assert tl["so_bai_trich"] == 1
    bm = ad.get("/admin/api/ai-rieng/bai-mau?trang_thai=cho_duyet").json()["bai_mau"]
    assert bm[0]["nguon"] == "tai_lieu" and bm[0]["rdkit_ok"] == 1 and bm[0]["de_khoa"] == kho.khoa_de(DE)


# ================================================================ giải 3 tầng
def test_tang_1_thu_vien_khong_goi_ai(ar):
    bid = kho.tao_bai_mau(DE, LG, "este", "duyet", "tay", rdkit_ok=True)
    with llm.dung_khoa(llm.KHOA_CHU_APP):
        kq = ar_giai.giai("CH3COOH -> CH3COOC2H5 -> C2H5OH")
    assert kq.tang == "thu_vien" and kq.bai_mau_id == bid and not ar.goi
    assert kho.lay_bai_mau(bid)["so_lan_dung"] == 1


def test_tang_2_kien_thuc_va_tu_luu_thu_vien(ar):
    kho.tao_kien_thuc("Cộng nước vào anken", "C2H4 + H2O (H+, t°) → C2H5OH", "anken", 5)
    with llm.dung_khoa(llm.KHOA_CHU_APP), llm.dung_ke_hoach(llm.KeHoachAI(loai="giai")):
        kq = ar_giai.giai(DE2)
    assert kq.tang == "kien_thuc" and len(ar.goi) == 1
    g = ar.goi[0]
    assert g["model"] == "lite-test" and kho.SYSTEM_MAC_DINH in g["system"] and "KHO KIẾN THỨC RIÊNG" in g["chu"] and "[Q1]" in g["chu"]
    assert kq.da_dung == ["Q1", "T1"] and kq.da_luu_id
    assert kho.lay_bai_mau(kq.da_luu_id)["trang_thai"] == "cho_duyet"
    # lần 2: cùng đề → thư viện, không gọi AI
    with llm.dung_khoa(llm.KHOA_CHU_APP):
        assert ar_giai.giai(DE2).tang == "thu_vien" and len(ar.goi) == 1
    # admin tắt "dùng lời giải chờ duyệt" → lại gọi AI
    kho.dat_cai_dat({"dung_cho_duyet": False})
    with llm.dung_khoa(llm.KHOA_CHU_APP):
        assert ar_giai.giai(DE2).tang == "kien_thuc" and len(ar.goi) == 2


def test_tang_3_khi_rdkit_bao_sai(ar):
    ar.lan_sai = 1
    with llm.dung_khoa(llm.KHOA_CHU_APP), llm.dung_ke_hoach(llm.KeHoachAI(loai="giai")):
        kq = ar_giai.giai(DE2)
    assert kq.tang == "manh" and [g["model"] for g in ar.goi] == ["lite-test", "pro-test"] and not kq.loi
    kho.dat_cai_dat({"cho_phep_t3": False})
    ar.lan_sai = 1
    with llm.dung_khoa(llm.KHOA_CHU_APP):
        kq = ar_giai.giai("C2H4 → C2H5OH → CH3CHO → CH3COOH")
    assert kq.tang == "kien_thuc" and kq.loi and kq.da_luu_id is None  # lỗi RDKit → không lưu vào thư viện


def test_nghiem_ngat_chua_duoc_day(ar):
    kho.dat_cai_dat({"che_do": "nghiem_ngat"})
    with llm.dung_khoa(llm.KHOA_CHU_APP):
        kq = ar_giai.giai(DE2)
    assert kq.tang == "chua_day" and kq.gon is None and not ar.goi  # kho trống → không gọi AI
    kho.tao_kien_thuc("Cộng nước vào anken", "C2H4 + H2O → C2H5OH", "anken")
    ar.tra_loi = json.dumps({**LG2, "chua_duoc_day": ["Bước 2 C2H5OH → CH3CHO: chưa được dạy oxi hoá ancol"]}, ensure_ascii=False)
    with llm.dung_khoa(llm.KHOA_CHU_APP):
        kq = ar_giai.giai(DE2)
    assert kq.tang == "chua_day" and "chưa được dạy" in kq.chua_duoc_day[0] and kq.da_luu_id is None
    assert "CHẾ ĐỘ NGHIÊM NGẶT" in ar.goi[-1]["chu"]


# ================================================================ qua API: trang giải, báo sai, Sân tập
def test_api_giai_ai_rieng_bao_sai_va_san_tap(ar, ad, client, monkeypatch):
    hs = _dang_nhap_email(monkeypatch, client, "hs@example.com")
    r = hs.post("/api/giai", data={"chuoi": DE2, "ai": "ai_rieng"}, headers=hs.h)
    assert r.json()["ai"] == "" and r.status_code == 200  # admin chưa mở AI Riêng → giải như thường
    _cho_xong(r.json()["job_id"])
    kho.dat_cai_dat({"mo_cho_nguoi_dung": True})
    kho.tao_kien_thuc("Oxi hoá ancol bậc I", "C2H5OH + CuO (t°) → CH3CHO + Cu + H2O", "ancol", 4)
    gioi_han_tan_suat.xoa()
    r = hs.post("/api/giai", data={"chuoi": DE2, "ai": "ai_rieng"}, headers=hs.h)
    assert r.json()["ai"] == "ai_rieng"
    job = _cho_xong(r.json()["job_id"])
    kq = hs.get(f"/api/ket-qua/{job.id}").json()
    assert kq["ai_rieng"]["tang"] in ("kien_thuc", "thu_vien") and kq["tep"]["png"]
    nguon = kq["ai_rieng"]["nguon"]
    assert nguon and all("id" not in n and "noi_dung" not in n for n in nguon)  # người dùng thường: chỉ tiêu đề
    # lần 2 cùng đề → thư viện, không tính lượt dùng thử
    luot = db.lay_nguoi_dung(hs.uid).luot_da_dung
    gioi_han_tan_suat.xoa()
    job2 = _cho_xong(hs.post("/api/giai", data={"chuoi": DE2, "ai": "ai_rieng"}, headers=hs.h).json()["job_id"])
    assert job2.ket_qua["ai_rieng"]["tang"] == "thu_vien" and db.lay_nguoi_dung(hs.uid).luot_da_dung == luot
    # báo sai → hàng chờ (một lần / bài); người khác không báo được bài này
    assert hs.post("/api/bao-sai", json={"job_id": job2.id, "ghi_chu": "B phải là CH3CHO"}, headers=hs.h).json()["ok"]
    assert hs.post("/api/bao-sai", json={"job_id": job2.id}, headers=hs.h).status_code == 409
    assert ad.post("/api/bao-sai", json={"job_id": job2.id}, headers=ad.h).status_code == 404
    cho = ad.get("/admin/api/ai-rieng/bao-sai").json()["bao_sai"]
    assert len(cho) == 1 and cho[0]["ghi_chu"] == "B phải là CH3CHO"
    sid = ad.post(f"/admin/api/ai-rieng/bao-sai/{cho[0]['id']}/chuyen", headers=ad.h,
                  json={"dung": "CH3CHO (anđehit axetic)", "ly_do": "oxi hoá nhẹ"}).json()["sua_sai_id"]
    assert kho.tim_khop(DE2, True) is None  # lời giải cũ cùng đề không được dùng lại
    assert any(m.loai == "sua_sai" and m.id == sid for m in tim.tim_kien_thuc(DE2))
    # Sân tập (admin): so sánh AI Riêng / AI thường, "Đúng" → bài mẫu đã duyệt
    d = ad.post("/admin/api/ai-rieng/san-tap", headers=ad.h, json={"de": DE2, "so_sanh": True}).json()
    j1, j2 = _cho_xong(d["ai_rieng"]), _cho_xong(d["ai_thuong"])
    assert j1.ket_qua["ai_rieng"]["nguon"][0].get("id") and "ai_rieng" not in j2.ket_qua
    bid = ad.post("/admin/api/ai-rieng/san-tap/dung", headers=ad.h, json={"job_id": j1.id, "chu_de": "ancol"}).json()["id"]
    assert kho.lay_bai_mau(bid)["trang_thai"] == "duyet" and kho.tim_khop(DE2, False)["id"] == bid


def test_chua_duoc_day_qua_api(ar, ad, client, monkeypatch):
    kho.dat_cai_dat({"mo_cho_nguoi_dung": True, "che_do": "nghiem_ngat"})
    hs = _dang_nhap_email(monkeypatch, client, "hs@example.com")
    luot = db.lay_nguoi_dung(hs.uid).luot_da_dung
    job = _cho_xong(hs.post("/api/giai", data={"chuoi": DE2, "ai": "ai_rieng"}, headers=hs.h).json()["job_id"])
    r = hs.get(f"/api/ket-qua/{job.id}")
    assert r.status_code == 500 and r.json()["ma"] == "chua_day" and "chưa được dạy" in r.json()["loi"]
    assert db.lay_nguoi_dung(hs.uid).luot_da_dung == luot  # không tính lượt dùng thử
    kho.dat_cai_dat({"khi_chua_day": "ai_thuong"})
    gioi_han_tan_suat.xoa()
    job = _cho_xong(hs.post("/api/giai", data={"chuoi": DE2, "ai": "ai_rieng"}, headers=hs.h).json()["job_id"])
    assert job.ket_qua and "ai_rieng" not in job.ket_qua  # tự giải bằng AI thường


def test_chon_ai_dua_len_dau_chuoi(ar, monkeypatch):
    monkeypatch.setattr(settings, "openai_api_key", "sk-proj-test")
    monkeypatch.setattr(settings, "openai_model", "gpt-test")
    with llm.dung_khoa(llm.KHOA_CHU_APP), llm.dung_ke_hoach(llm.KeHoachAI(loai="giai", uu_tien_ai="openai")):
        p = llm.PhienHoiThoai("x")
        assert p.chuoi[0] == "openai" and llm.ten_ai("flash") == "ChatGPT"


# ================================================================ quy tắc hàng loạt, bộ đề, sao lưu
def test_nhap_quy_tac_csv_xlsx(ar, ad):
    csv_ = "Tiêu đề;Nội dung;Chủ đề;Độ ưu tiên\nMarkovnikov;H+ cộng vào C nhiều H hơn;anken;5\n;thiếu tiêu đề;;\n".encode()
    r = ad.post("/admin/api/ai-rieng/kien-thuc/nhap", headers=ad.h, files={"tep": ("qt.csv", csv_, "text/csv")}).json()
    assert r["da_nhap"] == 1 and "Dòng 3" in r["loi"][0]
    from openpyxl import Workbook

    wb = Workbook()
    wb.active.append(["tieu_de", "noi_dung", "chu_de", "uu_tien"])
    wb.active.append(["Zaitsev", "Tách H ở C bậc cao hơn", "ancol", 4])
    buf = io.BytesIO()
    wb.save(buf)
    assert ad.post("/admin/api/ai-rieng/kien-thuc/nhap", headers=ad.h, files={"tep": ("qt.xlsx", buf.getvalue(), "application/vnd.ms-excel")}
                   ).json()["da_nhap"] == 1
    ds = ad.get("/admin/api/ai-rieng/kien-thuc?chu_de=ancol").json()["kien_thuc"]
    assert [k["tieu_de"] for k in ds] == ["Zaitsev"]


def test_bo_de_kiem_tra(ar, ad):
    ad.post("/admin/api/ai-rieng/bo-de", headers=ad.h, json={"de": DE2, "chu_de": "ancol", "dap_an": [
        {"ki_hieu": "C2H5OH", "smiles": "OCC"}, {"ki_hieu": "CH3CHO", "chat": "CH3CHO"}]})
    ad.post("/admin/api/ai-rieng/bo-de", headers=ad.h, json={"de": "C2H4 → C2H5OH → X", "chu_de": "anđehit", "dap_an": [
        {"ki_hieu": "X", "chat": "CH3COOH"}]})
    assert ad.post("/admin/api/ai-rieng/bo-de", headers=ad.h, json={"de": "x", "dap_an": []}).status_code == 400
    lid = ad.post("/admin/api/ai-rieng/kiem-tra/chay", headers=ad.h).json()["id"]
    for _ in range(200):
        d = ad.get(f"/admin/api/ai-rieng/kiem-tra/{lid}").json()
        if d["trang_thai"] != "dang_chay":
            break
        time.sleep(0.02)
    assert d["trang_thai"] == "xong" and (d["so_cau"], d["so_dung"]) == (2, 1)
    sai = [x for x in d["chi_tiet"] if not x["dung"]][0]
    assert sai["chat"][0]["ket_qua"] == "thieu"  # AI không có chất X
    assert d["theo_chu_de"][0] == {"chu_de": "anđehit", "dung": 0, "tong": 1, "ti_le": 0}
    assert ad.get("/admin/api/ai-rieng/bo-de").json()["lan"][0]["so_dung"] == 1


def test_cham_chat():
    assert cham.so_chat({"ki_hieu": "A", "smiles": "CC=O"}, {"smiles": "O=CC"}) == ("dung", "SMILES (RDKit)")
    assert cham.so_chat({"ki_hieu": "A", "chat": "CH3CHO"}, {"ctct": "CH3COOH", "smiles": "CC(=O)O"})[0] == "sai"
    assert cham.so_chat({"ki_hieu": "A", "chat": "CH3-CHO"}, {"ctct": "CH3CHO", "smiles": "CC=O"})[0] == "dung"
    assert cham.so_chat({"ki_hieu": "A", "chat": "C2H5OH"}, {"ctct": "CH3OCH3", "smiles": "COC"})[0] == "chua_ro"  # đồng phân → AI so
    assert cham.dem_nguyen_to("(CH3)2CHOH") == {"C": 3, "H": 8, "O": 1} and cham.dem_nguyen_to("ancol etylic") is None


def test_sao_luu_va_phuc_hoi(ar, ad):
    kho.tao_kien_thuc("Markovnikov", "H+ vào C nhiều H", "anken", 5)
    kho.tao_bai_mau(DE, LG, "este", "duyet", rdkit_ok=True)
    kho.tao_sua_sai(DE2, "CH3CHO", "oxi hoá nhẹ", "AI ghi CH3COOH")
    kho.tao_cau_hoi(DE2, [{"ki_hieu": "CH3CHO", "chat": "CH3CHO"}])
    r = ad.get("/admin/api/ai-rieng/sao-luu")
    assert r.status_code == 200 and "attachment" in r.headers["content-disposition"]
    du_lieu = r.json()
    assert du_lieu["dinh_dang"] == "chemistry-ai-rieng" and '"vec"' not in json.dumps(du_lieu)
    gop = ad.post("/admin/api/ai-rieng/phuc-hoi", headers=ad.h, files={"tep": ("b.json", r.content, "application/json")}).json()
    assert gop["bo_qua"] == 4 and gop["kien_thuc"] == 0  # gộp: mục trùng bỏ qua
    thay = ad.post("/admin/api/ai-rieng/phuc-hoi", headers=ad.h, data={"thay_the": "true"},
                   files={"tep": ("b.json", r.content, "application/json")}).json()
    assert (thay["kien_thuc"], thay["bai_mau"], thay["sua_sai"], thay["bo_de"]) == (1, 1, 1, 1)
    assert kho.tim_khop(DE, False) is not None  # bài mẫu đã duyệt vẫn dùng được sau phục hồi
    assert ad.post("/admin/api/ai-rieng/phuc-hoi", headers=ad.h, files={"tep": ("x.json", b'{"a": 1}', "application/json")}).status_code == 400


def test_cai_dat_hop_le(ar, ad):
    assert ad.post("/admin/api/ai-rieng/cai-dat", headers=ad.h, json={"top_k": 99}).status_code == 400
    assert ad.post("/admin/api/ai-rieng/cai-dat", headers=ad.h, json={"che_do": "linh_tinh"}).status_code == 400
    d = ad.post("/admin/api/ai-rieng/cai-dat", headers=ad.h, json={"top_k": 5, "system_prompt": "  "}).json()
    assert d["top_k"] == 5 and d["system_prompt"] == kho.SYSTEM_MAC_DINH
    tq = ad.get("/admin/api/ai-rieng/tong-quan").json()
    assert "sk-" not in json.dumps(tq) and tq["thong_ke"]["so"]["kien_thuc"] == 0


# ================================================================ chạy SONG SONG với Gemini (đối chiếu)
DE3 = "C2H4 →(H2O, H+) X →(CuO, t°) Y"


def _lg2(**sua):
    """Lời giải của DE3 (chất ẩn X, Y – hai AI có thể cho Y khác nhau mà vẫn hợp lệ RDKit)."""
    d = json.loads(json.dumps(LG2))
    d["chuoi"][1]["ki_hieu"], d["chuoi"][2]["ki_hieu"] = "X", "Y"
    d["buoc"][0]["den"], d["buoc"][1]["tu"], d["buoc"][1]["den"] = "X", "X", "Y"
    for k, v in sua.items():
        if k == "c":  # Y khác (CH3COOH thay CH3CHO) – vẫn hợp lệ RDKit
            d["chuoi"][2].update(ctct="CH3COOH", ten="acetic acid", smiles="CC(=O)O")
        elif k == "hong":
            d["chuoi"][1]["smiles"] = "C(C"
        elif k == "chung":  # không dựa vào kho
            for b in d["buoc"]:
                b["can_cu"] = "kiến thức chung"
    return json.dumps(d, ensure_ascii=False)


def _giai_song_song(ar, **cai_dat):
    kho.dat_cai_dat({"song_song_gemini": True, **cai_dat})
    kho.tao_kien_thuc("Cộng nước vào anken", "C2H4 + H2O (H+, t°) → C2H5OH", "anken", 5)
    ar.tra_loi = _lg2()
    with llm.dung_khoa(llm.KHOA_CHU_APP), llm.dung_ke_hoach(llm.KeHoachAI(loai="giai")):
        return ar_giai.giai(DE3)


def test_song_song_dong_thuan(ar):
    kq = _giai_song_song(ar, cho_gemini="luon")
    assert sorted(g["model"] for g in ar.goi) == ["flash-test", "lite-test"]  # 2 làn cùng kho kiến thức
    assert all("KHO KIẾN THỨC RIÊNG" in g["chu"] for g in ar.goi)
    dc = kq.doi_chieu
    assert (dc["ket_luan"], dc["do_tin_cay"], dc["khop"], dc["chon"]) == ("dong_thuan", "cao", 3, "ai_rieng") and kq.da_luu_id
    assert [x["ten"] for x in dc["lan"]] == ["ai_rieng", "gemini"]


def test_song_song_ai_rieng_sai_dung_ngay_gemini(ar):
    ar.theo_mo_hinh["lite-test"] = _lg2(hong=True)
    kq = _giai_song_song(ar)
    assert [g["model"] for g in sorted(ar.goi, key=lambda g: g["model"])] == ["flash-test", "lite-test"]  # không cần tầng 3 tuần tự
    assert kq.tang == "kien_thuc" and not kq.loi and kq.doi_chieu["chon"] == "gemini" and kq.doi_chieu["ket_luan"] == "chi_mot_ben"


def test_song_song_khac_nhau_trong_tai(ar):
    from backend.ai_rieng import song_song

    ar.theo_mo_hinh["lite-test"] = _lg2(c=True, chung=True)  # AI Riêng không chắc (kiến thức chung) + khác Gemini ở CH3CHO
    kq = _giai_song_song(ar)
    assert sorted(g["model"] for g in ar.goi) == ["flash-test", "lite-test", "pro-test"]
    assert "ĐỐI CHIẾU – hai lời giải trước KHÁC NHAU ở: Y" in [g for g in ar.goi if g["model"] == "pro-test"][0]["chu"]
    dc = kq.doi_chieu
    assert (dc["ket_luan"], dc["trong_tai_theo"], dc["do_tin_cay"], dc["chon"]) == ("trong_tai", "gemini", "cao", "manh")
    assert kq.tang == "manh" and kq.gon.chuoi[2].smiles == "CC=O" and kq.da_luu_id  # đã phân xử → được lưu thư viện
    assert song_song.cho_nen_xong(5)
    # không cho tầng 3: hai AI bất đồng → chọn bên có nhiều căn cứ trong kho hơn, cảnh báo, KHÔNG lưu thư viện
    ar.goi.clear()
    kho.dat_cai_dat({"cho_phep_t3": False})
    with llm.dung_khoa(llm.KHOA_CHU_APP), llm.dung_ke_hoach(llm.KeHoachAI(loai="giai")):
        kq = ar_giai.giai(DE3, bo_qua_thu_vien=True)
    assert kq.doi_chieu["ket_luan"] == "khac_nhau" and kq.doi_chieu["chon"] == "gemini" and kq.da_luu_id is None
    assert "khác nhau ở Y" in kq.canh_bao[0]


def test_doi_chieu_nen_danh_dau_nghi_van(ar):
    from backend.ai_rieng import song_song

    ar.theo_mo_hinh["flash-test"] = _lg2(c=True)
    ar.cham["flash-test"] = 0.4  # Gemini xong SAU khi AI Riêng (chắc chắn) đã trả kết quả
    kq = _giai_song_song(ar)  # mặc định "khi_can": không chờ
    assert kq.doi_chieu["ket_luan"] == "doi_chieu_nen" and kq.tang == "kien_thuc" and kq.da_luu_id
    assert kho.tim_khop(DE3, True)["id"] == kq.da_luu_id
    assert song_song.cho_nen_xong(5)
    bm = kho.lay_bai_mau(kq.da_luu_id)
    assert "Gemini đối chiếu khác ở Y" in bm["nghi_van"] and kho.tim_khop(DE3, True) is None  # nghi vấn → không dùng ở tầng 1
    kho.duyet_bai_mau([kq.da_luu_id])  # admin duyệt → hết nghi vấn
    assert kho.lay_bai_mau(kq.da_luu_id)["nghi_van"] == "" and kho.tim_khop(DE3, False)["id"] == kq.da_luu_id


def test_ban_do_giu_ngu_canh_va_thu_tu():
    from backend.ai_rieng import song_song

    with llm.dung_khoa(llm.KHOA_CHU_APP), llm.dung_ke_hoach(llm.KeHoachAI(loai="giai", buoc="x")):
        kq = song_song.ban_do(lambda i: (i, llm.khoa_hien_tai().nguon, llm.ke_hoach_hien_tai().buoc) if i != 3 else 1 / 0, range(6), 3)
    assert kq[:3] == [(0, "chu_app", "x"), (1, "chu_app", "x"), (2, "chu_app", "x")] and isinstance(kq[3], ZeroDivisionError)


def test_quet_va_nap_ca_thu_muc(ar, ad, tmp_path):
    tm = tmp_path / "TaiLieuHuuCo"
    tm.mkdir()
    (tm / "Este").mkdir()

    # Tạo các file hợp lệ và không hợp lệ
    (tm / "bai1.txt").write_text("Ankan phản ứng thế clo khi chiếu sáng askt.")
    (tm / "Este" / "bai2.txt").write_text("Este no đơn chức mạch hở có công thức chung CnH2nO2 (n >= 2).")
    (tm / ".DS_Store").write_bytes(b"\x00\x01\x02")
    (tm / "virus.bin").write_bytes(b"INVALID BINARY DATA")

    # 1. Thử xem trước thư mục không tồn tại -> 400
    r_loi = ad.post("/admin/api/ai-rieng/tai-lieu/xem-truoc-thu-muc", headers=ad.h,
                    json={"duong_dan": str(tmp_path / "khong_co")})
    assert r_loi.status_code == 400

    # 2. Xem trước thư mục hợp lệ
    r_xt = ad.post("/admin/api/ai-rieng/tai-lieu/xem-truoc-thu-muc", headers=ad.h,
                   json={"duong_dan": str(tm), "de_quy": True})
    assert r_xt.status_code == 200, r_xt.text
    d_xt = r_xt.json()
    assert d_xt["tong_hop_le"] == 2
    assert d_xt["tong_bo_qua"] >= 1

    # 3. Quét nạp toàn bộ thư mục
    r_quet = ad.post("/admin/api/ai-rieng/tai-lieu/quet-thu-muc", headers=ad.h,
                     json={"duong_dan": str(tm), "chu_de": "Hóa 12", "kem_thu_muc_con": True})
    assert r_quet.status_code == 200, r_quet.text
    d_quet = r_quet.json()
    assert d_quet["tong_hop_le"] == 2
    assert len(d_quet["da_them"]) == 2

    xu_ly.xu_ly_het()
    ds = {t["ten"]: t for t in ad.get("/admin/api/ai-rieng/tai-lieu").json()["tai_lieu"]}
    assert "bai1" in ds
    assert "Este/bai2" in ds
    assert "Este" in ds["Este/bai2"]["chu_de"]


def test_nap_tep_kem_duong_dan_tuong_doi(ar, ad):
    rel = ["Folder1/Sub/chuyen_de.txt"]
    r = ad.post("/admin/api/ai-rieng/tai-lieu", headers=ad.h,
                data={"chu_de": "Hóa học", "duong_dan_tuong_doi": json.dumps(rel)},
                files=[("tep", ("chuyen_de.txt", b"Noi dung on thi hoa huu co dai cuong", "text/plain"))])
    assert r.status_code == 200, r.text
    d = r.json()
    assert len(d["da_them"]) == 1

    xu_ly.xu_ly_het()
    ds = {t["ten"]: t for t in ad.get("/admin/api/ai-rieng/tai-lieu").json()["tai_lieu"]}
    assert "Folder1/Sub/chuyen_de" in ds
    assert "Folder1/Sub" in ds["Folder1/Sub/chuyen_de"]["chu_de"]



def test_cai_dat_theo_ten_truong_cua_form(ar, ad):
    """Form Cài đặt ở /admin/ai-rieng gửi tên trường giao diện → backend đổi đúng khoá (trước đây bị bỏ qua âm thầm)."""
    d = ad.post("/admin/api/ai-rieng/cai-dat", headers=ad.h, json={
        "bat": True, "mo_cho_nguoi_dung": True, "tu_luu_thu_vien": False, "che_do_nghiem_ngat": "tu_choi", "mo_hinh": "gemini",
        "k_doan": 3, "k_kien_thuc": 5}).json()
    cd = kho.cai_dat()
    assert (cd["luu_tu_dong"], cd["che_do"], cd["khi_chua_day"], cd["mo_hinh_t2"], cd["k_doan"], cd["k_kien_thuc"], cd["top_k"]) == \
        (False, "nghiem_ngat", "bao", "flash", 3, 5, 12)
    g = ad.get("/admin/api/ai-rieng/cai-dat").json()  # form đọc lại đúng giá trị
    assert (g["tu_luu_thu_vien"], g["che_do_nghiem_ngat"], g["mo_hinh"], g["k_doan"]) == (False, "tu_choi", "gemini", 3) == \
        (d["tu_luu_thu_vien"], d["che_do_nghiem_ngat"], d["mo_hinh"], d["k_doan"])
    ad.post("/admin/api/ai-rieng/cai-dat", headers=ad.h, json={"che_do_nghiem_ngat": "canh_bao", "mo_hinh": ""})
    assert (kho.cai_dat()["che_do"], kho.cai_dat()["khi_chua_day"], kho.cai_dat()["mo_hinh_t2"]) == ("nghiem_ngat", "ai_thuong", "lite")
    ad.post("/admin/api/ai-rieng/cai-dat", headers=ad.h, json={"bat": False})
    assert ad.get("/api/toi").json()["ai_rieng"]["co"] is False  # tắt AI Riêng → trang giải không còn lựa chọn này


def test_bo_lan_doi_chieu_khi_gemini_manh_het_han_muc(ar, monkeypatch):
    from backend.ai.providers.gemini import chat as gc

    monkeypatch.setattr(gc, "_gemini_het_han_muc", {m: time.time() + 3600 for m in
                                                   ["flash-test", "pro-test", *ar_giai.GEMINI_MODEL_DU_PHONG] if "lite" not in m})
    kq = _giai_song_song(ar)
    assert [g["model"] for g in ar.goi] == ["lite-test"] and "hết hạn mức" in kq.doi_chieu["bo_qua"]  # không chạy Flash-Lite 2 lần


def test_luu_thu_vien_doi_nhan_can_cu_thanh_ten(ar):
    kho.tao_kien_thuc("Cộng nước vào anken", "C2H4 + H2O (H+, t°) → C2H5OH", "anken", 5)
    with llm.dung_khoa(llm.KHOA_CHU_APP), llm.dung_ke_hoach(llm.KeHoachAI(loai="giai")):
        kq = ar_giai.giai(DE2)
        kq2 = ar_giai.giai(DE2)
    assert kq.da_luu_id and kq2.tang == "thu_vien"
    assert kq2.gon.buoc[0].can_cu == "«Cộng nước vào anken»"  # [Q1] của lần giải trước → tên mục
