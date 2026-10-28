"""AI Riêng – giải chuỗi 3 tầng (tiết kiệm token, càng dùng càng rẻ):

1. THƯ VIỆN LỜI GIẢI: đề đã có lời giải đúng (bài mẫu đã duyệt / lời giải AI hoặc trích từ tài liệu đã qua RDKit) → trả ngay, 0 token.
2. KIẾN THỨC RIÊNG + MÔ HÌNH RẺ: tìm sửa sai / quy tắc / bài mẫu / tài liệu liên quan → Gemini Flash-Lite (mặc định) → RDKit kiểm tra.
3. MÔ HÌNH MẠNH: chỉ khi RDKit báo sai (Gemini Pro, hoặc Claude nếu người dùng có quyền) – cùng kho kiến thức, sửa thêm 1 vòng.
Lời giải AI qua RDKit → tự lưu vào thư viện (chờ duyệt) → lần sau đề đó không tốn token.
Kết quả vẫn đi qua RDKit, vẽ cấu tạo, ảnh đáp án, ChemDraw như luồng giải thường (pipeline._xuat_tep).
"""
from __future__ import annotations

import dataclasses
import logging
import re
import time
from concurrent.futures import TimeoutError as FutureTimeout
from dataclasses import dataclass, field
from typing import Callable

from rdkit import Chem

from .. import chi_phi_ai, db, giai_nhanh, parser, tai_khoan
from ..ai.providers.gemini import client as gemini_client
from ..config import GEMINI_MODEL_DU_PHONG, settings
from ..llm import KeHoachAI, LoiAI, PhienHoiThoai, dung_ke_hoach, ke_hoach_hien_tai, khoa_hien_tai, ten_ai
from ..schemas import LoiGiaiGon
from . import kho, song_song, tim

log = logging.getLogger(__name__)

TEN_TANG = {"thu_vien": "Thư viện lời giải – không gọi AI", "kien_thuc": "Kiến thức riêng + mô hình rẻ",
            "manh": "Kiến thức riêng + mô hình mạnh", "chua_day": "Chưa được dạy"}
SYSTEM_DOC_DE = """Chép lại đề chuỗi phản ứng trong ảnh thành MỘT dòng chữ: các chất nối bằng " → ", điều kiện / tác nhân trên mũi tên ghi
trong ngoặc ngay sau mũi tên, vd "C2H4 →(H2O, H+) C2H5OH → CH3CHO". Công thức viết số thường (CH3COOH). Chỉ trả về dòng đó."""


@dataclass
class KetQuaAIRieng:
    tang: str
    gon: LoiGiaiGon | None
    mo_hinh: str = ""
    muc: list[tim.Muc] = field(default_factory=list)
    loi: list[str] = field(default_factory=list)
    canh_bao: list[str] = field(default_factory=list)
    theo_buoc: dict = field(default_factory=dict)
    bai_mau_id: int | None = None
    da_luu_id: int | None = None       # lời giải vừa tự lưu vào thư viện (chờ duyệt)
    de: str = ""                       # đề dùng để tra (đề chữ, hoặc đề AI đọc từ ảnh)
    chua_duoc_day: list[str] = field(default_factory=list)
    doi_chieu: dict = field(default_factory=dict)  # làn Gemini chạy song song: đồng thuận / khác nhau / trọng tài, thời gian từng làn

    @property
    def da_dung(self) -> list[str]:
        if self.tang == "thu_vien":
            return [m.nhan for m in self.muc]
        return tim.nhan_da_dung([b.model_dump() for b in self.gon.buoc]) if self.gon else []

    def tom_tat(self, admin: bool) -> dict:
        da_dung = self.da_dung
        return {"tang": self.tang, "ten_tang": TEN_TANG[self.tang], "mo_hinh": self.mo_hinh, "che_do": kho.cai_dat()["che_do"],
                "nguon": [{**m.cong_khai(admin), "da_dung": m.nhan in da_dung} for m in self.muc],
                "chua_duoc_day": self.chua_duoc_day, "mau_thuan": list(self.gon.mau_thuan) if self.gon else [],
                "bai_mau_id": self.bai_mau_id if admin else None, "da_luu_id": self.da_luu_id if admin else None, "de": self.de,
                "doi_chieu": self.doi_chieu if admin else {k: self.doi_chieu.get(k) for k in ("ket_luan", "do_tin_cay", "khac")}}


def doc_de_trong_anh(anh_b64: tuple[str, str]) -> str:
    try:
        ph = PhienHoiThoai(SYSTEM_DOC_DE, effort="low", max_tokens=1500, bac="lite").gui(text="Đề trong ảnh:", anh=anh_b64)
        return giai_nhanh.chuan_de(ph.text.strip().splitlines()[0] if ph.text.strip() else "")
    except LoiAI as e:
        log.info("Không đọc được đề trong ảnh để tra thư viện: %s", e)
        return ""


def _cfg_tang2(cd: dict, quyen_claude) -> tuple[giai_nhanh.CauHinhMuc, str]:
    m = cd["mo_hinh_t2"]
    if m == "claude" and quyen_claude is not None:
        return giai_nhanh.cau_hinh_muc("ky", True), ""
    if m == "flash":
        return giai_nhanh.cau_hinh_muc("can_bang", False), ""
    return giai_nhanh.cau_hinh_muc("nhanh", False), "openai" if m == "openai" else ""


def giai(de: str, anh_b64: tuple[str, str] | None = None, *, bao: Callable[[str], None] | None = None,
         khi_moi: Callable[[list[dict], list[dict]], None] | None = None, su_kien: Callable[..., None] | None = None,
         quyen_claude=None, bo_qua_thu_vien: bool = False, ghi_thu_vien: bool = True,
         hoi_notebooklm: bool = False, notebook_id: str = "", account_id: int | None = None) -> KetQuaAIRieng:
    """Giải MỘT đề bằng AI Riêng (không vẽ / xuất file). su_kien(giai_doan, thong_diep, trang_thai) – tiến trình cho giao diện."""
    cd = kho.cai_dat()
    su = su_kien or (lambda *_a, **_k: None)
    kh = ke_hoach_hien_tai()
    de = giai_nhanh.chuan_de(de or "")
    tra = de
    if not tra and anh_b64:
        su("phan_tich", "AI rẻ đọc đề trong ảnh để tra thư viện và kho kiến thức…")
        tra = doc_de_trong_anh(anh_b64)
        if tra:
            su("phan_tich", f"Đề đọc được: {tra[:200]}")

    # ---- tầng 1: thư viện lời giải
    if tra and not bo_qua_thu_vien:
        bm = kho.tim_khop(tra, cd["dung_cho_duyet"])
        if bm is not None:
            gon = LoiGiaiGon.model_validate(bm["loi_giai"])
            loi, canh_bao, theo_buoc = giai_nhanh.kiem_tra(gon, tra)
            if not loi or bm["trang_thai"] == "duyet":  # bài đã duyệt: tin giáo viên, RDKit chỉ ghi cảnh báo
                kho.tang_lan_dung(bm["id"])
                nguon = {"duyet": "bài mẫu đã duyệt", "cho_duyet": "lời giải đã qua RDKit, chờ duyệt"}[bm["trang_thai"]]
                su("tra_cuu", f"Đề này có trong thư viện lời giải ({nguon}) – dùng ngay, không gọi AI (0 token)", "xong")
                muc = [tim.Muc("bai_mau", bm["id"], 1.0, bm["de"][:140], trang=bm["trang"], chu_de=bm["chu_de"], nhan="M1")]
                return KetQuaAIRieng("thu_vien", gon, "AI Riêng · thư viện lời giải", muc, loi, canh_bao, theo_buoc, bai_mau_id=bm["id"], de=tra)

    # ---- tầng 2: kho kiến thức + mô hình rẻ
    muc = tim.tim_kien_thuc(tra) if tra else []

    # Hỏi thêm NotebookLM nếu người dùng bật tùy chọn
    if hoi_notebooklm and notebook_id and tra:
        try:
            from ..services import notebooklm as nlm_service
            su("tra_cuu", f"Đang tra cứu thêm từ Google NotebookLM ({notebook_id[:8]}...)…", "bat_dau")
            cau_hoi = f"Tìm các phản ứng hoá học, chất phản ứng và điều kiện liên quan đến chuỗi phản ứng sau: {tra}"
            kq_nlm = nlm_service.hoi_them_notebooklm(account_id, notebook_id, cau_hoi)
            if kq_nlm:
                muc.append(tim.Muc("doan", 999999, 1.2, f"NotebookLM ({notebook_id[:8]})", kq_nlm, nhan=f"T{len(muc)+1}"))
                su("tra_cuu", "Đã nhận kết quả tra cứu từ NotebookLM để hỗ trợ giải", "xong")
        except Exception as e:
            log.warning("Lỗi khi hỏi thêm NotebookLM: %s", e)
            su("tra_cuu", f"Không thể hỏi thêm NotebookLM: {e}", "canh_bao")

    dem: dict[str, int] = {}
    for m in muc:
        dem[m.loai] = dem.get(m.loai, 0) + 1
    su("tra_cuu", "Kho kiến thức riêng: " + (", ".join(f"{n} {tim.TEN_LOAI[l].lower()}" for l, n in dem.items()) if muc
                                            else "chưa có mục nào liên quan"), "xong" if muc else "canh_bao")
    if cd["che_do"] == "nghiem_ngat" and not muc:
        return KetQuaAIRieng("chua_day", None, de=tra, chua_duoc_day=["Kho chưa có kiến thức nào liên quan tới đề này"])
    khoi = tim.khoi_prompt(muc, cd["che_do"])
    system = cd["system_prompt"]
    cfg, uu_tien = _cfg_tang2(cd, quyen_claude)

    # ---- làn Gemini đối chiếu: chạy SONG SONG (cùng kho kiến thức) – xong trước thì dùng ngay khi AI Riêng sai / không chắc
    tuong_lai, cfg_b, bo_qua_b = None, None, ""
    if cd["song_song_gemini"] and gemini_client.san_sang(khoa_hien_tai()):
        cfg_b = _cfg_gemini(cfg, uu_tien)
        if cfg.bac == "lite" and not uu_tien and not _gemini_manh_con_han_muc():
            cfg_b, bo_qua_b = None, "Gemini Flash / Pro đã hết hạn mức hôm nay – bỏ làn đối chiếu (chỉ còn Flash-Lite, trùng làn chính)"
            su("giai", bo_qua_b, "canh_bao")
    if cfg_b is not None:
        kh_b = (dataclasses.replace(kh, buoc="ai_rieng_doi_chieu", bac=cfg_b.bac, uu_tien_ai="gemini", claude=None, thong_bao=[])
                if kh is not None else KeHoachAI(loai="giai", buoc="ai_rieng_doi_chieu", bac=cfg_b.bac, uu_tien_ai="gemini"))
        bao_b = (lambda m: bao(f"Gemini đối chiếu: {m}")) if bao else None

        def lan_gemini():
            with dung_ke_hoach(kh_b):
                return _giai_lan("gemini", de, anh_b64, cfg_b, khoi, system, bao_b, None, tra)

        tuong_lai = song_song.gui_nen(lan_gemini)
    if kh is not None:
        kh.buoc, kh.bac, kh.uu_tien_ai = "ai_rieng", cfg.bac, uu_tien
    try:
        ten_a = ten_ai(cfg.bac)
        su("giai", f"Đang giải bằng {ten_a} + kiến thức riêng" + (f" ‖ song song {TEN_GEMINI[cfg_b.bac]} đối chiếu…" if cfg_b else "…"),
           "bat_dau")
        lan_a = _giai_lan("ai_rieng", de, anh_b64, cfg, khoi, system, bao, khi_moi, tra)
    finally:
        if kh is not None:
            kh.uu_tien_ai = ""
    if lan_a.ngoai_le is not None and (tuong_lai is None and (lan_a.ngoai_le.loai != "dinh_dang" or not cd["cho_phep_t3"])):
        raise lan_a.ngoai_le

    # ---- chờ Gemini đến đâu: AI Riêng chắc chắn + chế độ "khi cần" → không chờ (Gemini đối chiếu ở nền, lệch thì đánh dấu nghi vấn)
    a_sach = lan_a.gon is not None and not lan_a.loi
    tu_tin = a_sach and _co_can_cu(lan_a.gon)
    lan_b, de_nen = None, False
    if tuong_lai is not None:
        if tu_tin and cd["cho_gemini"] == "khi_can":
            lan_b = _lay(tuong_lai, 0)
            de_nen = lan_b is None
        else:
            if not a_sach:
                su("kiem_tra", f"{'RDKit báo ' + str(len(lan_a.loi)) + ' lỗi' if lan_a.gon else 'AI Riêng lỗi'} → dùng làn Gemini đang chạy song "
                   "song", "canh_bao")
            lan_b = _lay(tuong_lai, CHO_GEMINI_THEM if a_sach else settings.ai_timeout_dai_giay)
            if lan_b is None:
                su("kiem_tra", "Gemini đối chiếu chưa xong kịp – dùng kết quả AI Riêng", "canh_bao")
    doi_chieu = _doi_chieu_moi(lan_a, lan_b, cfg_b, de_nen)
    if bo_qua_b:
        doi_chieu["bo_qua"] = bo_qua_b

    # ---- chọn lời giải
    tang, chon = "kien_thuc", None
    ung_vien = [x for x in (lan_a, lan_b) if x is not None and x.gon is not None]
    sach = [x for x in ung_vien if not x.loi]
    khac = doi_chieu.get("khac") or []
    if len(sach) == 2 and not khac:
        chon = lan_a
        doi_chieu.update(ket_luan="dong_thuan", do_tin_cay="cao")
    elif len(sach) == 2:
        doi_chieu.update(ket_luan="khac_nhau", do_tin_cay="trung_binh")
        chon = max(sach, key=lambda x: (_diem_can_cu(x.gon), x.ten == "gemini"))  # nhiều căn cứ trong kho hơn; hoà → Gemini (mạnh hơn)
    elif len(sach) == 1:
        chon = sach[0]
        if lan_b is not None:
            doi_chieu.update(ket_luan="chi_mot_ben", do_tin_cay="trung_binh")
    lan_t3 = None
    if cd["cho_phep_t3"] and (chon is None or doi_chieu.get("ket_luan") == "khac_nhau"):
        cfg3 = giai_nhanh.cau_hinh_muc("ky", quyen_claude is not None)
        if kh is not None:
            kh.buoc, kh.bac, kh.nang_cap = "ai_rieng_manh", cfg3.bac, True
        if chon is None:
            loi_dau = (min(ung_vien, key=lambda x: len(x.loi)).loi[:1] if ung_vien else []) or ["không có lời giải hợp lệ"]
            su("kiem_tra", f"Chưa có lời giải qua RDKit ({loi_dau[0][:140]}) → tầng 3: {ten_ai(cfg3.bac)} giải lại với cùng kiến thức",
               "canh_bao")
            them = ""
        else:
            su("kiem_tra", "AI Riêng và Gemini khác nhau ở " + ", ".join(k["ki_hieu"] for k in khac[:4])
               + f" → tầng 3: {ten_ai(cfg3.bac)} làm trọng tài", "canh_bao")
            them = "\n\nĐỐI CHIẾU – hai lời giải trước KHÁC NHAU ở: " + "; ".join(
                f"{k['ki_hieu']}: một bên {k['ai_rieng']}, bên kia {k['gemini']}" for k in khac[:6]) + \
                ". Xét kỹ từng bước (ưu tiên kho kiến thức, quy tắc Markovnikov / Zaitsev, điều kiện phản ứng) rồi trả lời đúng."
        lan_t3 = _giai_lan("manh", de, anh_b64, cfg3, khoi + them, system, bao, khi_moi, tra)
        if lan_t3.gon is not None and lan_t3.loi and lan_t3.phien is not None:
            try:
                gon_s, mh_s = giai_nhanh.sua(lan_t3.phien, lan_t3.gon, lan_t3.loi, cfg3, bao=bao)
                loi_s, cb_s, tb_s = giai_nhanh.kiem_tra(gon_s, tra)
                if len(loi_s) < len(lan_t3.loi):
                    lan_t3.gon, lan_t3.mo_hinh, lan_t3.loi, lan_t3.canh_bao, lan_t3.theo_buoc = gon_s, mh_s, loi_s, cb_s, tb_s
            except LoiAI as e:
                su("kiem_tra", f"Không sửa được ({e})", "canh_bao")
        doi_chieu["lan"].append(lan_t3.mo_ta())
        if lan_t3.gon is not None and (chon is None or not lan_t3.loi):
            if chon is not None and khac:  # trọng tài: trùng bên nào thì tin cậy cao
                _, khac_a = so_sanh(lan_t3.gon, lan_a.gon)
                _, khac_b = so_sanh(lan_t3.gon, lan_b.gon)
                doi_chieu.update(ket_luan="trong_tai", do_tin_cay="cao" if not khac_a or not khac_b else "trung_binh",
                                 trong_tai_theo="ai_rieng" if not khac_a else "gemini" if not khac_b else "")
            if chon is None or len(lan_t3.loi) <= len(chon.loi):
                chon, tang = lan_t3, "manh"
        elif lan_t3.ngoai_le is not None:
            su("kiem_tra", f"Mô hình mạnh lỗi ({lan_t3.ngoai_le}) – giữ lời giải tốt nhất đã có", "canh_bao")
    if chon is None:  # không có tầng 3 / tầng 3 cũng hỏng → lời giải ít lỗi RDKit nhất
        if not ung_vien:
            raise (lan_a.ngoai_le or (lan_b.ngoai_le if lan_b else None) or LoiAI("AI Riêng không tạo được lời giải.", "dinh_dang"))
        chon = min(ung_vien, key=lambda x: len(x.loi))
    doi_chieu["chon"] = chon.ten
    gon, mo_hinh, loi, canh_bao, theo_buoc = chon.gon, chon.mo_hinh, list(chon.loi), list(chon.canh_bao), chon.theo_buoc
    if doi_chieu.get("ket_luan") == "khac_nhau":
        canh_bao.insert(0, "AI Riêng và Gemini khác nhau ở " + "; ".join(f"{k['ki_hieu']} ({k['ai_rieng']} / {k['gemini']})" for k in khac[:4])
                        + f" – đã chọn lời giải của {TEN_LAN[chon.ten]}, nên kiểm tra lại")

    chua = list(gon.chua_duoc_day) if cd["che_do"] == "nghiem_ngat" else []
    if chua:
        return KetQuaAIRieng("chua_day", gon, mo_hinh, muc, loi, canh_bao, theo_buoc, de=tra, chua_duoc_day=chua, doi_chieu=doi_chieu)
    da_luu = None
    chac = doi_chieu.get("ket_luan") != "khac_nhau"  # hai AI bất đồng chưa phân xử → không đưa vào thư viện
    if not loi and chac and ghi_thu_vien and cd["luu_tu_dong"] and tra and not kho.co_trong_thu_vien(tra):
        try:
            da_luu = kho.tao_bai_mau(tra, _can_cu_thanh_ten(gon, muc), trang_thai="cho_duyet", nguon="ai", rdkit_ok=True, mo_hinh=mo_hinh)
        except ValueError:
            da_luu = None
    if de_nen:  # Gemini đối chiếu xong sau → so với lời giải đã trả; lệch thì đánh dấu nghi vấn (không dùng ở tầng 1 khi chưa duyệt)
        song_song.gui_nen(_doi_chieu_nen, tuong_lai, gon, da_luu, tra)  # việc nền riêng (được theo dõi), xếp SAU làn nó chờ → không kẹt
    return KetQuaAIRieng(tang, gon, mo_hinh, muc, loi, canh_bao, theo_buoc, da_luu_id=da_luu, de=tra, doi_chieu=doi_chieu)


# ================================================================ làn giải + đối chiếu
TEN_GEMINI = {"lite": "Gemini Flash-Lite", "flash": "Gemini Flash", "pro": "Gemini Pro"}
TEN_LAN = {"ai_rieng": "AI Riêng", "gemini": "Gemini đối chiếu", "manh": "mô hình mạnh (tầng 3)"}
CHO_GEMINI_THEM = 45  # giây tối đa chờ thêm Gemini đối chiếu sau khi AI Riêng đã có lời giải sạch (chế độ "luôn chờ")


@dataclass
class _Lan:
    ten: str
    gon: LoiGiaiGon | None = None
    phien: PhienHoiThoai | None = None
    mo_hinh: str = ""
    loi: list[str] = field(default_factory=list)
    canh_bao: list[str] = field(default_factory=list)
    theo_buoc: dict = field(default_factory=dict)
    giay: float = 0.0
    ngoai_le: LoiAI | None = None

    def mo_ta(self) -> dict:
        return {"ten": self.ten, "ten_hien_thi": TEN_LAN[self.ten], "mo_hinh": self.mo_hinh, "giay": self.giay,
                "trang_thai": "loi" if self.ngoai_le is not None else ("rdkit_loi" if self.loi else "ok"),
                "rdkit_loi": len(self.loi), "loi": str(self.ngoai_le)[:200] if self.ngoai_le else ""}


def _giai_lan(ten: str, de: str, anh_b64, cfg, khoi: str, system: str, bao, khi_moi, tra: str) -> _Lan:
    t0 = time.time()
    lan = _Lan(ten)
    try:
        lan.gon, lan.phien, lan.mo_hinh = giai_nhanh.giai(de, anh_b64, cfg, bao=bao, khi_moi=khi_moi, kho_rieng=khoi, system_them=system)
        lan.loi, lan.canh_bao, lan.theo_buoc = giai_nhanh.kiem_tra(lan.gon, tra)
    except LoiAI as e:
        lan.ngoai_le = e
    lan.giay = round(time.time() - t0, 1)
    return lan


def _gemini_manh_con_han_muc() -> bool:
    """Còn ít nhất một mô hình Gemini MẠNH HƠN Flash-Lite chưa bị đánh dấu hết hạn mức (key của yêu cầu này) – không thì làn đối chiếu
    chỉ là Flash-Lite lặp lại làn chính: tốn token mà không thêm ý kiến nào."""
    from ..ai.providers.gemini import chat as gemini_chat

    khoa = khoa_hien_tai()
    het = gemini_chat._het_han_muc_ca_nhan.get(khoa.ma, {}) if khoa.nguon == "ca_nhan" else gemini_chat._gemini_het_han_muc
    bay_gio = time.time()
    manh = [m for m in dict.fromkeys([settings.gemini_model, settings.gemini_model_hsg, *GEMINI_MODEL_DU_PHONG]) if m and "lite" not in m]
    return any(het.get(m, 0) <= bay_gio for m in manh)


def _can_cu_thanh_ten(gon: LoiGiaiGon, muc: list[tim.Muc]) -> dict:
    """Lưu vào thư viện: nhãn căn cứ chỉ có nghĩa trong lần giải này ([Q1] …) → đổi thành tên mục (lần sau lấy từ thư viện vẫn đọc được)."""
    ten = {m.nhan: (m.tieu_de or "")[:60] for m in muc}
    d = gon.model_dump()
    for b in d["buoc"]:
        if b.get("can_cu"):
            b["can_cu"] = re.sub(r"\[([^\]]{1,60})\]", lambda x: "; ".join(f"«{ten[n]}»" if n in ten else n
                                                                       for n in re.findall(r"\b[SQMT]\d{1,3}\b", x.group(1)))
                                 or x.group(0), b["can_cu"])
    return d


def _lay(tuong_lai, cho: float) -> _Lan | None:
    try:
        return tuong_lai.result(timeout=cho)
    except FutureTimeout:
        return None
    except Exception as e:  # noqa: BLE001
        log.warning("Làn Gemini đối chiếu lỗi: %s", e)
        return None


def _cfg_gemini(cfg_a: giai_nhanh.CauHinhMuc, uu_tien_a: str) -> giai_nhanh.CauHinhMuc:
    """Làn đối chiếu = Gemini MẠNH HƠN làn chính một bậc (Flash-Lite → Flash; Flash → Pro – gói miễn phí tự lùi về Flash khác)."""
    if cfg_a.bac == "flash" and not uu_tien_a:
        return giai_nhanh.cau_hinh_muc("ky", False)
    return giai_nhanh.cau_hinh_muc("can_bang", False)


def _cau_tao(gon: LoiGiaiGon) -> dict[str, tuple[str, str]]:
    """Kí hiệu → (SMILES chuẩn RDKit, bỏ lập thể; chữ hiển thị)."""
    out: dict[str, tuple[str, str]] = {}
    for c in gon.chuoi:
        m = Chem.MolFromSmiles(c.smiles or "") if c.smiles else None
        out[re.sub(r"\s+", "", c.ki_hieu).upper()] = (Chem.MolToSmiles(m, isomericSmiles=False) if m is not None else (c.smiles or ""),
                                                      c.ctct or c.smiles or c.ten)
    return out


def so_sanh(g1: LoiGiaiGon, g2: LoiGiaiGon) -> tuple[list[str], list[dict]]:
    """→ (kí hiệu cùng cấu tạo, [{ki_hieu, ai_rieng, gemini}] khác cấu tạo) trên các chất hai lời giải cùng có."""
    a, b = _cau_tao(g1), _cau_tao(g2)
    chung = [k for k in a if k in b]

    def chu(x: tuple[str, str], y: tuple[str, str]) -> str:  # cùng chữ CTCT mà khác cấu tạo → kèm SMILES cho thấy chỗ khác
        return x[1] if x[1] != y[1] else f"{x[1]} [{x[0]}]"

    return ([k for k in chung if a[k][0] == b[k][0]],
            [{"ki_hieu": k, "ai_rieng": chu(a[k], b[k]), "gemini": chu(b[k], a[k])} for k in chung if a[k][0] != b[k][0]])


_NHAN = re.compile(r"\[[^\]]*\b[SQMT]\d{1,3}\b")


def _co_can_cu(gon: LoiGiaiGon) -> bool:
    """Mọi bước đều dựa trên kho (can_cu có nhãn [Q2] / [M1] …) – không bước nào là "kiến thức chung"."""
    return bool(gon.buoc) and all(_NHAN.search(b.can_cu or "") for b in gon.buoc)


def _diem_can_cu(gon: LoiGiaiGon) -> int:
    return sum(bool(_NHAN.search(b.can_cu or "")) for b in gon.buoc)


def _doi_chieu_moi(lan_a: _Lan, lan_b: _Lan | None, cfg_b, de_nen: bool) -> dict:
    d: dict = {"song_song": cfg_b is not None, "lan": [lan_a.mo_ta()] + ([lan_b.mo_ta()] if lan_b is not None else []),
               "ket_luan": "khong_doi_chieu" if cfg_b is None else ("doi_chieu_nen" if de_nen else "chua_doi_chieu"),
               "do_tin_cay": "", "khac": [], "khop": 0}
    if de_nen:
        d["do_tin_cay"] = "cao"  # RDKit sạch + mọi bước có căn cứ trong kho
        d["lan"].append({"ten": "gemini", "ten_hien_thi": TEN_LAN["gemini"], "mo_hinh": TEN_GEMINI.get(cfg_b.bac, ""), "trang_thai": "nen"})
    if lan_b is not None and lan_a.gon is not None and lan_b.gon is not None:
        khop, khac = so_sanh(lan_a.gon, lan_b.gon)
        # "khác nhau" chỉ có nghĩa khi CẢ HAI lời giải qua RDKit; một bên còn lỗi → ghi lỗi đó (bên kia được chọn)
        d.update(khop=len(khop), khac=khac if not lan_a.loi and not lan_b.loi else [])
        loi_ben = [(x, x.loi[0]) for x in (lan_a, lan_b) if x.loi]
        if len(loi_ben) == 1:
            d["loai_ben"] = {"ten": loi_ben[0][0].ten, "ly_do": loi_ben[0][1][:240]}
    return d


def _doi_chieu_nen(tuong_lai, gon: LoiGiaiGon, bai_mau_id: int | None, de: str) -> None:
    try:
        lan_b = tuong_lai.result()
    except Exception:  # noqa: BLE001
        return
    if lan_b is None or lan_b.gon is None or lan_b.loi:
        return
    khop, khac = so_sanh(gon, lan_b.gon)
    if not khac:
        log.info("AI Riêng: Gemini đối chiếu (nền) đồng thuận %d chất – %s", len(khop), de[:80])
        return
    ly_do = "Gemini đối chiếu khác ở " + "; ".join(f"{k['ki_hieu']}: AI Riêng {k['ai_rieng']}, Gemini {k['gemini']}" for k in khac[:6])
    log.warning("AI Riêng: %s – %s", ly_do, de[:80])
    if bai_mau_id:
        kho.dat_nghi_van(bai_mau_id, ly_do)


def _la_admin(user_id: int | None) -> bool:
    u = db.lay_nguoi_dung(user_id) if user_id else None
    return bool(u and tai_khoan.la_chu_app(u))


def chay(job, kh, chuoi: str, anh: bytes | None) -> None:
    """Chế độ "AI Riêng" của trang giải (pipeline.chay(ai="ai_rieng")): giải 3 tầng → vẽ + xuất file như luồng thường."""
    from .. import pipeline

    t0 = time.time()
    tuy_chon = getattr(job, "tuy_chon", {}) or {}
    admin = _la_admin(job.user_id)
    anh_b64 = parser.chuan_bi_anh(anh, "JPEG") if anh else None
    job.emit("phan_tich", "AI Riêng: thư viện lời giải → kiến thức riêng + mô hình rẻ → mô hình mạnh khi RDKit báo sai", "bat_dau")

    def khi_moi(c, b):
        job.emit("giai", pipeline._tom_tat_buoc(c, b), "dang_chay", chuoi_tam=c, buoc_tam=b)

    kq = giai(chuoi, anh_b64, bao=lambda m: job.emit("giai", m, "canh_bao"), khi_moi=khi_moi, su_kien=job.emit, quyen_claude=kh.claude,
              bo_qua_thu_vien=bool(tuy_chon.get("bo_qua_thu_vien")),
              hoi_notebooklm=bool(tuy_chon.get("hoi_notebooklm")),
              notebook_id=str(tuy_chon.get("nlm_notebook_id") or ""),
              account_id=tuy_chon.get("nlm_account_id"))
    if kq.tang == "chua_day":
        tom = "; ".join(kq.chua_duoc_day[:3])
        if kho.cai_dat()["khi_chua_day"] == "ai_thuong" and not tuy_chon.get("san_tap"):
            kho.ghi_lan_giai("chua_day", job.user_id, job.id, so_muc=len(kq.muc))
            job.emit("giai", f"AI Riêng chưa được dạy ({tom}) → giải bằng AI thường", "canh_bao")
            pipeline._chay_nhanh(job, kh, chuoi, anh, "", "", kh.claude is not None)
            return
        if kq.gon is None or not admin:  # người dùng thường: không đưa "dự đoán" chưa được dạy
            kho.ghi_lan_giai("chua_day", job.user_id, job.id, so_muc=len(kq.muc), tokens=_tokens(job.id))
            job.ket_qua_loi = {"ai_rieng": kq.tom_tat(admin)}
            raise LoiAI(f"AI Riêng chưa được dạy phần này: {tom}. Bấm «Giải bằng AI thường», hoặc báo admin dạy thêm.", "chua_day")
        job.emit("kiem_tra", f"AI Riêng chưa được dạy: {tom} – lời giải dưới đây chỉ là DỰ ĐOÁN để giáo viên biết cần dạy gì", "canh_bao")

    if kq.tang != "thu_vien":
        job.emit("giai", f"Đã có chuỗi {len(kq.gon.buoc)} bước ({kq.mo_hinh})", "xong", chuoi_tam=[c.model_dump() for c in kq.gon.chuoi],
                 buoc_tam=[b.model_dump() for b in kq.gon.buoc])
    job.emit("kiem_tra", "Tất cả chất hợp lệ (RDKit)" if not kq.loi else f"Còn {len(kq.loi)} chỗ cần kiểm tra lại",
             "xong" if not kq.loi else "canh_bao")
    if kq.da_luu_id:
        job.emit("kiem_tra", "Đã lưu lời giải vào thư viện AI Riêng (chờ duyệt) – lần sau đề này không tốn token")
    job.emit("ve", "Đang vẽ sơ đồ chuỗi và ảnh đáp án...", "bat_dau")
    sol = giai_nhanh.sang_loi_giai(kq.gon, kq.theo_buoc)
    job.ket_qua = pipeline._xuat_tep(job, sol, chuoi.strip() or "(Đề trong ảnh đã tải lên)", kq.mo_hinh, False, kq.loi + kq.canh_bao)
    giay = round(time.time() - t0, 1)
    job.ket_qua.update(gon=kq.gon.model_dump(), muc_thong_minh="", ten_muc_thong_minh="AI Riêng", thoi_gian_giai=giay,
                       tu_cache=kq.tang == "thu_vien", muc_do="", ten_muc_do="", ly_do_muc_do="", nang_cap=kq.tang == "manh",
                       dung_claude=kh.claude_thanh_cong, tokens=chi_phi_ai.tong_job(job.id), tai_lieu=[],
                       tra_cuu_tl={"bat": False, "so_bai": -1, "token": 0, "thoi_gian": 0, "pdf_nguoi_dung": False},
                       ai_rieng=kq.tom_tat(admin))
    kho.ghi_lan_giai(kq.tang, job.user_id, job.id, kq.bai_mau_id, kq.mo_hinh, len(kq.muc), _tokens(job.id))
    job.emit("ve", "Đã xuất PNG, CDXML, SDF, JSON và ZIP", "xong")
    job.emit("xong", f"Hoàn tất sau {giay:g} giây" + (" – lấy từ thư viện, 0 token" if kq.tang == "thu_vien" else ""), "xong")


def _tokens(job_id: str) -> int:
    t = chi_phi_ai.tong_job(job_id)
    return t["vao"] + t["ra"]
