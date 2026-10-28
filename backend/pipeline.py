"""Điều phối: Phân tích (+ phân loại độ khó) → Tra cứu → Giải → Kiểm tra → Vẽ → Xong. Chạy trong thread nền, sự kiện đẩy ra SSE.

Chọn mô hình theo độ khó (giảm chi phí): đọc đề bằng Flash-Lite (ảnh: Flash), phân loại Dễ / Trung bình / HSG bằng Flash-Lite,
tra cứu bằng Flash-Lite (đề Dễ) hoặc Flash, giải bằng Flash-Lite / Flash / Pro (HSG + quyền Claude → Claude). Lời giải sai (RDKit)
hoặc JSON hỏng 2 lần → giải lại bằng mô hình cao hơn một bậc (chỉ lên Claude khi có quyền)."""
from __future__ import annotations

import json
import logging
import re
import threading
import time
import unicodedata
import uuid
from dataclasses import dataclass, field
from pathlib import Path
from typing import Callable

from . import chi_phi_ai, do_kho, giai_nhanh, literature, parser, research, solver
from .chemdraw import tao_cdxml
from .config import BASE_DIR, OUTPUT_DIR, settings
from .image_export import tao_anh_dap_an, tao_zip
from .llm import TEN_BAC, KeHoachAI, LoiAI, dung_ke_hoach, ke_hoach_hien_tai, ten_ai
from .renderer import luu_anh_phan_tu, xuat_mol, xuat_sdf
from .schemas import BuocTrongDe, ChatTrongDe, LoiGiai, LoiGiaiGon, PhanTichDe
from .validator import danh_sach_loi, kiem_tra_loi_giai

log = logging.getLogger(__name__)

GIAI_DOAN = ["phan_tich", "tra_cuu", "giai", "kiem_tra", "ve", "xong"]
SO_VONG_SUA = 2
CHAN_TRANG_AI = "Lời giải do AI tạo – vui lòng kiểm tra lại"
CHAN_TRANG_DEMO = "Lời giải mẫu soạn sẵn (chế độ demo, không gọi AI) – vui lòng kiểm tra lại"
DEMO_DIR = BASE_DIR / "backend" / "demo_data"

_gioi_han = threading.Semaphore(2)  # tối đa 2 bài chạy song song (mỗi bài có thể mở Chrome)


@dataclass
class Job:
    id: str
    thu_muc: Path
    su_kien: list[dict] = field(default_factory=list)
    xong: bool = False
    loi: str | None = None
    loai_loi: str = ""             # LoiAI.loai của lỗi (để quyết định có trả lại lượt dùng thử không)
    ket_qua: dict | None = None
    user_id: int | None = None     # chủ bài giải (chỉ người này xem được tiến trình/kết quả/file)
    muc_do: str = ""               # de | trung_binh | hsg (sau bước phân loại)
    ly_do_muc_do: str = ""
    muc_tm: str = ""               # mức độ thông minh đã dùng: nhanh | can_bang | ky | toi_da
    tuy_chon: dict = field(default_factory=dict)  # AI Riêng: bo_qua_thu_vien, san_tap (Sân tập của admin)
    ket_qua_loi: dict | None = None  # thông tin kèm khi lỗi (vd AI Riêng chưa được dạy → danh sách phần còn thiếu)

    def emit(self, giai_doan: str, thong_diep: str, trang_thai: str = "dang_chay", url: str | None = None, **them):
        ev = {"giai_doan": giai_doan, "trang_thai": trang_thai, "thong_diep": thong_diep, "t": round(time.time(), 2), **them}
        if url:
            ev["url"] = url
        self.su_kien.append(ev)
        log.info("[%s] %s/%s: %s", self.id, giai_doan, trang_thai, thong_diep)


JOBS: dict[str, Job] = {}


def tao_job(user_id: int | None = None) -> Job:
    jid = time.strftime("%Y%m%d-%H%M%S-") + uuid.uuid4().hex[:12]
    thu_muc = OUTPUT_DIR / jid
    thu_muc.mkdir(parents=True, exist_ok=True)
    job = Job(jid, thu_muc, user_id=user_id)
    JOBS[jid] = job
    return job


# ---------------------------------------------------------------- chế độ demo (không gọi AI)
def _chuan_chuoi(text: str) -> list[str]:
    """Dãy chất + tác nhân trên từng mũi tên, bỏ khoảng trắng và kiểu mũi tên (để so với chuỗi mẫu)."""
    chat = [re.sub(r"\s+", "", x).upper() for x in parser.tach_chuoi(text)]
    return chat + ["→" + re.sub(r"\s+", "", x).upper() for x in parser.tach_tac_nhan(text)]


def tim_bai_demo(chuoi: str) -> dict | None:
    """Tìm lời giải mẫu có cùng dãy chất với chuỗi người dùng nhập."""
    can = _chuan_chuoi(chuoi.split(":")[-1])
    for f in sorted(DEMO_DIR.glob("*.json")):
        data = json.loads(f.read_text(encoding="utf-8"))
        mau = _chuan_chuoi(data["de_bai"].split(":")[-1])
        if can and can == mau:
            return data
    return None


def _de_tu_bai_demo(data: dict) -> PhanTichDe:
    sol = data["loi_giai"]
    an = {c["ki_hieu"] for c in sol.get("chat_an", [])}
    ctpt = data.get("ctpt_de_cho", {})            # {"C": "C3H6O"} – CTPT đề cho kèm chất ẩn
    tac_nhan = data.get("dieu_kien_de_cho", {})   # {"1": "HBr"} – tác nhân đề ghi trên mũi tên
    return PhanTichDe(
        de_bai=data["de_bai"],
        # tên trong lời giải mẫu là tên IUPAC tiếng Anh; tên có dấu (vd "tinh bột") thì không tra PubChem
        cac_chat=[ChatTrongDe(ki_hieu=n["ki_hieu"], la_chat_an=n["ki_hieu"] in an, ten_goi_y=n["ten"],
                              ten_tieng_anh=n["ten"] if n["ten"].isascii() else "", ctpt_de_cho=ctpt.get(n["ki_hieu"], ""))
                  for n in sol["so_do"]],
        cac_buoc=[BuocTrongDe(so=b["so"], chat_dau=b["chat_dau"], chat_sau=b["chat_sau"],
                              dieu_kien_de_cho=tac_nhan.get(str(b["so"]), "")) for b in sol["cac_buoc"]],
    )


# ---------------------------------------------------------------- tiện ích xuất file
def _ten_tep(text: str, i: int) -> str:
    t = unicodedata.normalize("NFD", text).encode("ascii", "ignore").decode()
    t = re.sub(r"[^A-Za-z0-9]+", "_", t).strip("_")[:40]
    return f"{i:02d}_{t or 'chat'}"


def _xuat_tep(job: Job, sol: LoiGiai, de_bai: str, mo_hinh: str, demo: bool, canh_bao: list[str]) -> dict:
    d = job.thu_muc
    (d / "chat").mkdir(exist_ok=True)
    (d / "mol").mkdir(exist_ok=True)

    # Ảnh từng chất (cho giao diện) + .mol
    anh: dict[str, str] = {}
    chat_mol: list[dict] = []
    cong_thuc = {n.smiles: n.cong_thuc for n in sol.so_do}
    tat_ca = [(n.ki_hieu, n.ten, n.smiles) for n in sol.so_do] + [(c.ki_hieu, c.ten, c.smiles) for c in sol.chat_an]
    for b in sol.cac_buoc:
        tat_ca += [("", x.ten, x.smiles) for x in b.chat_tham_gia + b.san_pham]
    for ki_hieu, ten, smiles in tat_ca:
        if not smiles or smiles in anh:
            continue
        i = len(anh) + 1
        ten_tep = _ten_tep(ki_hieu or ten, i)
        if luu_anh_phan_tu(smiles, d / "chat" / f"{ten_tep}.png", cong_thuc=cong_thuc.get(smiles, "")):
            anh[smiles] = f"/api/tep/{job.id}/chat/{ten_tep}.png"
            xuat_mol(smiles, ten or ki_hieu, d / "mol" / f"{ten_tep}.mol")
            chat_mol.append({"ki_hieu": ki_hieu, "ten": ten, "smiles": smiles, "cong_thuc": cong_thuc.get(smiles, "")})
    xuat_sdf(chat_mol, d / "tat_ca_chat.sdf")
    job.emit("ve", f"Đã vẽ {len(anh)} chất, xuất .mol/.sdf")

    co_cdxml = tao_cdxml(sol, d / "so_do.cdxml")
    if co_cdxml:
        job.emit("ve", "Đã tạo sơ đồ ChemDraw (.cdxml)")
    else:
        job.emit("ve", "Không tạo được .cdxml – dùng file .sdf gộp để mở bằng ChemDraw", "canh_bao")

    png = tao_anh_dap_an(sol, de_bai, d / "dap_an.png", CHAN_TRANG_DEMO if demo else CHAN_TRANG_AI, "" if demo else mo_hinh)
    job.emit("ve", "Đã tạo ảnh đáp án PNG")

    loi_giai_json = sol.model_dump()
    (d / "loi_giai.json").write_text(
        json.dumps({"de_bai": de_bai, "mo_hinh": mo_hinh, "demo": demo, "canh_bao": canh_bao, "loi_giai": loi_giai_json},
                   ensure_ascii=False, indent=1),
        encoding="utf-8",
    )
    files = [png, d / "loi_giai.json", d / "tat_ca_chat.sdf", *sorted((d / "mol").glob("*.mol"))]
    if co_cdxml:
        files.append(d / "so_do.cdxml")
    tao_zip(d / "ket_qua.zip", files)

    return {
        "job_id": job.id,
        "de_bai": de_bai,
        "mo_hinh": mo_hinh,
        "demo": demo,
        "canh_bao": canh_bao,
        "loi_giai": loi_giai_json,
        "anh": anh,
        "tep": {
            "png": f"/api/tep/{job.id}/dap_an.png",
            "tai_png": f"/api/tai/{job.id}/png",
            "tai_zip": f"/api/tai/{job.id}/zip",
            "cdxml": f"/api/tep/{job.id}/so_do.cdxml" if co_cdxml else None,
        },
    }


_DONG_NGHIA_DK = {"ánhsáng": "as", "ánhsángkhuếchtán": "as", "đunnóng": "t°", "nhiệtđộ": "t°"}


def _khoa_dk(x: str) -> str:
    x = re.sub(r"\s+", "", x).lower()
    x = re.sub(r"^t[o0º˚]$", "t°", x)
    return _DONG_NGHIA_DK.get(x, x)


def giu_tac_nhan_de_cho(sol: LoiGiai, de: PhanTichDe) -> None:
    """Nhãn trên mũi tên = tác nhân/điều kiện đề cho (giữ nguyên) + điều kiện AI bổ sung mà đề chưa ghi.

    AI hay bỏ tác nhân khỏi dieu_kien vì đã có trong phương trình (vd đề →[HBr] nhưng AI ghi "nhiệt độ phòng").
    """
    de_cho = {b.so: (b.dieu_kien_de_cho or "").strip() for b in de.cac_buoc}
    for b in sol.cac_buoc:
        goc = de_cho.get(b.so, "")
        if not goc:
            continue
        da_co = {_khoa_dk(x) for x in re.split(r"[,;]", goc)}
        da_co.add(_khoa_dk(goc))
        them = [x.strip() for x in re.split(r"[,;]", b.dieu_kien or "")
                if x.strip() and _khoa_dk(x) not in da_co and _khoa_dk(x) not in _khoa_dk(goc)]
        b.dieu_kien = ", ".join([goc] + them)


def _gop_nguon(sol: LoiGiai, nguon_that: list[str]) -> None:
    """Nguồn = các URL thực sự đã đọc + nguồn AI ghi (bỏ trùng; chỉ giữ URL thuộc các trang được phép)."""
    out: list[str] = []
    for n in list(nguon_that) + list(sol.nguon_tham_khao):
        n = (n or "").strip()
        if not n or n in out:
            continue
        if n.startswith("http") and not any(h in n for h in research.ALLOWED_HOSTS):
            continue
        out.append(n)
    sol.nguon_tham_khao = out


# ---------------------------------------------------------------- luồng chính
def chay(job: Job, chuoi: str, anh: bytes | None, hien_chrome: bool, demo: bool = False, hsg: bool = False,
         muc_do_toi_thieu: str = "", ke_hoach: KeHoachAI | None = None,
         truoc_khi_xong: Callable[[Job, KeHoachAI], None] | None = None, muc_thong_minh: str = "", bat_claude: bool = False,
         tra_cuu_tai_lieu: str = "", pdf: tuple[bytes, str] | None = None, ai: str = "") -> None:
    """ai: "ai_rieng" = AI Riêng (thư viện → kiến thức riêng + mô hình rẻ → mô hình mạnh; backend/ai_rieng/giai.py);
    "gemini" | "openai" = đưa AI đó lên đầu chuỗi cho bài này; "" = như cũ (thứ tự AI của admin, Claude theo quyền).
    muc_do_toi_thieu: người dùng nâng mức thủ công ("trung_binh" | "hsg"; hsg=True như "hsg").
    ke_hoach: kế hoạch AI (người dùng, quyền Claude) – main.py tạo; truoc_khi_xong: chạy trước khi báo xong (vd trừ lượt mã Claude).
    muc_thong_minh: thanh trượt Nhanh / Cân bằng / Kỹ / Tối đa ("" = tự động theo độ khó); bat_claude: người dùng bật "Dùng Claude".
    tra_cuu_tai_lieu: "1" bật / "0" tắt / "" tự động (bật ở Kỹ / Tối đa hoặc khi đề có DOI); pdf: (PDF người dùng tải lên, DOI) – chỉ lượt này.
    Chế độ AI: giai_nhanh (MỘT lần gọi, kết quả dạng chuỗi); chế độ demo: luồng cũ với lời giải mẫu (không gọi AI)."""
    kh = ke_hoach or ke_hoach_hien_tai() or KeHoachAI(loai="giai", user_id=job.user_id)
    kh.loai, kh.job_id = kh.loai if kh.loai != "khac" else "giai", kh.job_id or job.id
    toi_thieu = "hsg" if hsg else (muc_do_toi_thieu if muc_do_toi_thieu in do_kho.MUC_DO else "")
    with _gioi_han, dung_ke_hoach(kh):
        try:
            if demo:
                _chay(job, kh, chuoi, anh, hien_chrome, demo, toi_thieu)
            elif ai == "ai_rieng":
                from .ai_rieng import giai as ai_rieng

                ai_rieng.chay(job, kh, chuoi, anh)
            else:
                if ai in ("gemini", "openai"):
                    kh.uu_tien_ai = ai
                _chay_nhanh(job, kh, chuoi, anh, toi_thieu, muc_thong_minh, bat_claude, tra_cuu_tai_lieu, pdf)
        except LoiAI as e:
            job.loi = str(e)
            job.loai_loi = e.loai
            job.emit("loi", str(e), "loi", loai_loi=e.loai, **(job.ket_qua_loi or {}))
        except Exception as e:  # noqa: BLE001
            log.exception("Lỗi không mong muốn ở job %s", job.id)
            job.loi = f"Lỗi không mong muốn: {e.__class__.__name__}: {e}"
            job.emit("loi", job.loi, "loi")
        finally:
            if truoc_khi_xong is not None:
                try:
                    truoc_khi_xong(job, kh)
                except Exception:  # noqa: BLE001
                    log.exception("Lỗi khi kết thúc job %s", job.id)
            job.xong = True


def _tom_tat_buoc(chuoi: list[dict], buoc: list[dict]) -> str:
    """"A1 → A2 (cộng HBr)" cho dòng tiến trình khi một bước vừa viết xong (streaming)."""
    if not buoc:
        c = chuoi[-1] if chuoi else {}
        return f"Đã có chất {c.get('ki_hieu', '')}: {c.get('ctct') or c.get('ten') or ''}".strip(": ")
    b = buoc[-1]
    return f"Bước {len(buoc)}: {b.get('tu', '')} → {b.get('den', '')}" + (f" ({b.get('phan_ung')})" if b.get("phan_ung") else "")


def _tra_cuu_tai_lieu(job: Job, kh: KeHoachAI, de: str, anh_b64, yeu_cau: str, muc: str, pdf: tuple[bytes, str] | None) -> dict | None:
    """Bước 2 "Tra cứu": tài liệu khoa học hợp pháp (literature.py) nếu bật – "1" bật, "0" tắt, "" tự động: bật ở mức Kỹ / Tối đa, hoặc
    khi đề (chữ hay ảnh) có DOI, hoặc khi người dùng tải PDF lên. Tắt → không gọi API tài liệu nào."""
    if yeu_cau == "0" and pdf is None:
        return None
    tu_khoa = None
    bat = (pdf is not None or yeu_cau == "1" or bool(literature.tim_doi(de))
           or (yeu_cau == "" and settings.tra_cuu_tu_dong and muc in ("ky", "toi_da")))
    if not bat and anh_b64:  # đề ảnh, mức thấp: AI rẻ đọc xem trong ảnh có DOI không (có → tự bật)
        kh.buoc = "tra_cuu"
        tu_khoa = literature.rut_tu_khoa(de, anh_b64)
        bat = bool(tu_khoa["doi"])
    if not bat:
        return None
    kh.buoc = "tra_cuu"
    job.emit("tra_cuu", "Đang tìm bài báo khoa học (Crossref, OpenAlex, Semantic Scholar, Europe PMC, arXiv)…", "bat_dau")
    if tu_khoa is None:
        tu_khoa = literature.rut_tu_khoa(de, anh_b64)
    kq = literature.tra_cuu(tu_khoa, bao=lambda m: job.emit("tra_cuu", m), pdf=pdf)
    if not kq["bai"]:
        job.emit("tra_cuu", f"Không tìm được bài báo phù hợp ({kq['thoi_gian']:g} s) – giải bằng kiến thức của mô hình", "canh_bao")
        return {**kq, "khoi_prompt": ""}
    so_tv = sum(b["toan_van"] for b in kq["bai"])
    job.emit("tra_cuu", f"Đã đọc {len(kq['bai'])} bài ({so_tv} toàn văn, {len(kq['bai']) - so_tv} tóm tắt) – {kq['so_tu']} từ trích dẫn, "
             f"{kq['thoi_gian']:g} s" + (" (từ bộ nhớ đệm)" if kq["tu_cache"] else ""), "xong", tai_lieu=kq["bai"])
    return kq


def _chay_nhanh(job: Job, kh: KeHoachAI, chuoi: str, anh: bytes | None, toi_thieu: str, yeu_cau_muc: str, bat_claude: bool,
                yeu_cau_tl: str = "", pdf: tuple[bytes, str] | None = None) -> None:
    """Giải chuỗi theo mức độ thông minh: (phân loại nếu tự động) → cache? → MỘT lần gọi AI (streaming) → RDKit → sửa theo mức → vẽ."""
    t0 = time.time()

    def bao(giai_doan):
        return lambda m: job.emit(giai_doan, m, "canh_bao")

    # 1. Phân tích: chuẩn hoá đề (cục bộ), phân loại độ khó bằng Flash-Lite nếu để Tự động (kết quả phân loại cũng lưu cache)
    kh.buoc = "phan_tich"
    job.emit("phan_tich", "Đang đọc đề...", "bat_dau")
    de = giai_nhanh.chuan_de(chuoi)
    anh_b64 = parser.chuan_bi_anh(anh, "JPEG") if anh else None
    muc_do = toi_thieu
    if not yeu_cau_muc:
        kh.buoc = "phan_loai"
        khoa_pl = giai_nhanh.khoa_cache(de, anh, "phan_loai", "")
        pl = giai_nhanh.doc_cache(khoa_pl)
        if pl is None:
            pl = do_kho.phan_loai(de or "(đề trong ảnh)", bao=bao("phan_tich"), anh=anh_b64)
            if not pl.get("loi"):
                giai_nhanh.ghi_cache(khoa_pl, "phan_loai", pl)
        muc_do = do_kho.cao_hon(pl["muc_do"], toi_thieu)
        job.ly_do_muc_do = pl["ly_do"]
        tay = f" – bạn chọn nâng lên {do_kho.TEN_MUC[muc_do]}" if muc_do != pl["muc_do"] else ""
        job.emit("phan_tich", f"Mức độ đề: {do_kho.TEN_MUC[pl['muc_do']]} ({pl['ly_do'].rstrip('.')}){tay}", "dang_chay",
                 muc_do=muc_do, muc_do_ai=pl["muc_do"])
    kh.muc_do = job.muc_do = muc_do
    muc, dung_claude, tb = giai_nhanh.chon_muc(yeu_cau_muc, muc_do, kh.claude, bat_claude)
    if tb:
        job.emit("phan_tich", tb, "canh_bao")
    cfg = giai_nhanh.cau_hinh_muc(muc, dung_claude)
    job.muc_tm = muc
    job.emit("phan_tich", f"Mức {giai_nhanh.TEN_MUC_TM[muc]}{' (tự động theo độ khó)' if not yeu_cau_muc else ''} → "
             f"{ten_ai(cfg.bac)}{' mạnh nhất' if cfg.claude_model else ''}", "xong", muc_tm=muc)

    # 2. Tra cứu tài liệu khoa học (nếu bật); 3–4. cache (cùng đề + mức + loại mô hình + có/không tài liệu, 7 ngày) hoặc MỘT lần gọi AI
    tl = _tra_cuu_tai_lieu(job, kh, de, anh_b64, yeu_cau_tl, muc, pdf)
    khoi_tl = (tl or {}).get("khoi_prompt", "")
    khoa = giai_nhanh.khoa_cache(de, anh, muc, ("claude" if dung_claude else "gemini") + ("|tl:" + "|".join(
        b.get("doi") or b["tieu_de"][:40] for b in tl["bai"]) if khoi_tl else ""))
    cu = giai_nhanh.doc_cache(khoa) if pdf is None else None  # PDF người dùng tải lên: luôn giải mới, không dùng / không lưu cache
    if cu is not None:
        gon, mo_hinh, canh_bao = LoiGiaiGon.model_validate(cu["gon"]), cu["mo_hinh"], cu.get("canh_bao", [])
        theo_buoc = {int(k): v for k, v in (cu.get("theo_buoc") or {}).items()}
        for gd in ("tra_cuu", "giai", "kiem_tra"):
            job.emit(gd, "Đề này vừa được giải ở cùng mức – dùng lại kết quả đã lưu (không gọi AI)", "xong")
    else:
        if tl is None:
            job.emit("tra_cuu", "Tra cứu gộp vào bước giải (mô hình tự dùng dữ kiện về các chất – nhanh, tiết kiệm token)", "xong")
        kh.buoc, kh.bac = "giai", cfg.bac
        job.emit("giai", f"Đang giải bằng {ten_ai(cfg.bac)}…", "bat_dau")
        khi_moi = lambda c, b: job.emit("giai", _tom_tat_buoc(c, b), "dang_chay", chuoi_tam=c, buoc_tam=b)  # noqa: E731
        try:
            gon, phien, mo_hinh = giai_nhanh.giai(de, anh_b64, cfg, muc_do, bao("giai"), khi_moi, tai_lieu=khoi_tl)
        except LoiAI as e:  # JSON hỏng / bị cắt 2 lần → giải lại ở mức cao hơn một bậc
            i = giai_nhanh.MUC_TM.index(cfg.muc)
            if e.loai != "dinh_dang" or i >= len(giai_nhanh.MUC_TM) - 2:
                raise
            cfg = giai_nhanh.cau_hinh_muc(giai_nhanh.MUC_TM[i + 1], dung_claude)
            job.emit("giai", f"Kết quả hỏng → giải lại ở mức {giai_nhanh.TEN_MUC_TM[cfg.muc]} ({ten_ai(cfg.bac)})", "canh_bao")
            kh.bac, kh.nang_cap = cfg.bac, True
            gon, phien, mo_hinh = giai_nhanh.giai(de, anh_b64, cfg, muc_do, bao("giai"), khi_moi, tai_lieu=khoi_tl)
        job.emit("giai", f"Đã có chuỗi {len(gon.buoc)} bước ({mo_hinh})", "xong", chuoi_tam=[c.model_dump() for c in gon.chuoi],
                 buoc_tam=[b.model_dump() for b in gon.buoc])

        job.emit("kiem_tra", "Kiểm tra bằng RDKit (SMILES, công thức, nối bước)...", "bat_dau")
        loi, canh_bao, theo_buoc = giai_nhanh.kiem_tra(gon, de)
        if cfg.kiem_tra == "bo":
            job.emit("kiem_tra", "Mức Nhanh: bỏ bước nhờ AI kiểm tra" + (f" – RDKit báo {len(loi)} chỗ cần xem lại" if loi else ""))
        else:
            so_vong = 1 if cfg.kiem_tra == "re" else 2
            vong = 0
            while (loi and vong < so_vong) or (cfg.kiem_tra == "ky" and vong == 0):
                vong += 1
                kh.buoc = "kiem_tra"
                ra_soat = not loi
                job.emit("kiem_tra", ("AI rà soát lại toàn bộ lời giải (mức Tối đa)…" if ra_soat else
                                      f"RDKit báo {len(loi)} lỗi: {'; '.join(loi[:2])}{'…' if len(loi) > 2 else ''} → nhờ AI sửa "
                                      f"({'mô hình rẻ' if cfg.kiem_tra == 're' else ten_ai(cfg.bac)})"), "canh_bao" if loi else "dang_chay")
                try:
                    gon_moi, mo_hinh_moi = giai_nhanh.sua(phien, gon, loi, cfg, ra_soat=ra_soat, bao=bao("kiem_tra"))
                except LoiAI as e:
                    job.emit("kiem_tra", f"Không sửa được ({e}) – giữ lời giải trước", "canh_bao")
                    break
                loi_moi, canh_bao_moi, theo_buoc_moi = giai_nhanh.kiem_tra(gon_moi, de)
                if len(loi_moi) <= len(loi):
                    gon, loi, canh_bao, theo_buoc = gon_moi, loi_moi, canh_bao_moi, theo_buoc_moi
                    mo_hinh = mo_hinh if cfg.kiem_tra == "re" else mo_hinh_moi
                if ra_soat:
                    break
        job.emit("kiem_tra", "Tất cả chất hợp lệ" if not loi else f"Còn {len(loi)} chỗ cần kiểm tra lại", "xong" if not loi else "canh_bao")
        canh_bao = loi + canh_bao
        if not loi and (kh.claude_thanh_cong or not dung_claude) and pdf is None:  # chỉ lưu lời giải đã qua kiểm tra, đúng loại mô hình
            giai_nhanh.ghi_cache(khoa, muc, {"gon": gon.model_dump(), "mo_hinh": mo_hinh, "canh_bao": canh_bao,
                                             "theo_buoc": {str(k): v for k, v in theo_buoc.items()}})

    # 5. Vẽ + xuất file (chất, ảnh đáp án dạng sơ đồ chuỗi, .cdxml, .sdf, ZIP)
    job.emit("ve", "Đang vẽ sơ đồ chuỗi và ảnh đáp án...", "bat_dau")
    sol = giai_nhanh.sang_loi_giai(gon, theo_buoc)
    if tl and tl["bai"]:  # nguồn tham khảo (chân ảnh đáp án) = link DOI các bài đã dùng
        sol.nguon_tham_khao = [b["url"] or b["tieu_de"] for b in tl["bai"]]
    job.ket_qua = _xuat_tep(job, sol, chuoi.strip() or "(Đề trong ảnh đã tải lên)", mo_hinh, False, canh_bao)
    giay = round(time.time() - t0, 1)
    job.ket_qua.update(gon=gon.model_dump(), muc_thong_minh=muc, ten_muc_thong_minh=giai_nhanh.TEN_MUC_TM[muc],
                       thoi_gian_giai=giay, tu_cache=cu is not None, muc_do=muc_do, ten_muc_do=do_kho.TEN_MUC.get(muc_do, ""),
                       ly_do_muc_do=job.ly_do_muc_do, nang_cap=kh.nang_cap, dung_claude=kh.claude_thanh_cong,
                       tokens=chi_phi_ai.tong_job(job.id), tai_lieu=(tl or {}).get("bai", []),
                       tra_cuu_tl={"bat": tl is not None, "so_bai": len((tl or {}).get("bai", [])) if tl is not None else -1,
                                   "token": (tl or {}).get("token_uoc_tinh", 0) if khoi_tl else 0, "thoi_gian": (tl or {}).get("thoi_gian", 0),
                                   "pdf_nguoi_dung": pdf is not None})
    job.emit("ve", "Đã xuất PNG, CDXML, SDF, JSON và ZIP", "xong")
    job.emit("xong", f"Hoàn tất sau {giay:g} giây" + (" (kết quả đã lưu)" if cu is not None else ""), "xong")


def _chay(job: Job, kh: KeHoachAI, chuoi: str, anh: bytes | None, hien_chrome: bool, demo: bool, toi_thieu: str = "") -> None:
    """Luồng cũ nhiều bước (phân tích → tra cứu web → giải → RDKit → vẽ) – hiện chỉ dùng cho chế độ DEMO (lời giải mẫu, không gọi AI)."""
    def bao(giai_doan):
        return lambda m: job.emit(giai_doan, m, "canh_bao")

    # 1. Phân tích
    kh.buoc = "phan_tich"
    job.emit("phan_tich", "Đang phân tích đề...", "bat_dau")
    bai_demo = None
    if demo:
        bai_demo = tim_bai_demo(chuoi)
        if bai_demo is None:
            raise LoiAI("Chế độ demo chỉ dùng được với các chuỗi mẫu. Hãy bấm một chuỗi mẫu, hoặc tắt chế độ demo.")
        de = _de_tu_bai_demo(bai_demo)
    else:
        de = parser.phan_tich_de(chuoi, anh, bao=bao("phan_tich"))
    an = [c.ki_hieu + (f" ({c.ctpt_de_cho})" if c.ctpt_de_cho else "") for c in de.cac_chat if c.la_chat_an]
    tn = [f"({b.so}) {b.dieu_kien_de_cho}" for b in de.cac_buoc if b.dieu_kien_de_cho]
    job.emit("phan_tich", f"Đề có {len(de.cac_buoc)} bước, {len(de.cac_chat)} chất"
             + (f", chất ẩn: {', '.join(an)}" if an else "")
             + (f"; tác nhân đề cho: {'; '.join(tn)}" if tn else ""))

    # 1b. Phân loại độ khó bằng mô hình rẻ nhất (đề ảnh: sau khi đã đọc ảnh) → chọn mô hình giải
    if demo:
        muc = toi_thieu
    else:
        kh.buoc = "phan_loai"
        pl = do_kho.phan_loai(do_kho.mo_ta_de(de), bao=bao("phan_tich"))
        muc = do_kho.cao_hon(pl["muc_do"], toi_thieu)
        job.ly_do_muc_do = pl["ly_do"]
        tay = f" – bạn chọn nâng lên {do_kho.TEN_MUC[muc]}" if muc != pl["muc_do"] else ""
        job.emit("phan_tich", f"Mức độ đề: {do_kho.TEN_MUC[pl['muc_do']]} ({pl['ly_do'].rstrip('.')}){tay}", "dang_chay",
                 muc_do=muc, muc_do_ai=pl["muc_do"])
    kh.muc_do = job.muc_do = muc
    hsg = muc == "hsg"
    job.emit("phan_tich", "Đã phân tích đề" + (" – chế độ đề khó (HSG)" if hsg and not demo else ""), "xong")

    # 2. Tra cứu
    job.emit("tra_cuu", "Đang tra cứu PubChem và web...", "bat_dau")
    if demo:
        tc = research.KetQuaTraCuu()
        tc.pubchem = research.tra_cuu_truoc_pubchem(de, job.emit)
        tc.nguon = [r["url"] for r in tc.pubchem.values() if r.get("url")]
        web = research.TraCuuWeb(hien_chrome=hien_chrome, emit=job.emit)
        try:
            ten = next((c.ten_goi_y for c in de.cac_chat[1:] if not c.la_chat_an), de.cac_chat[0].ten_goi_y)
            r = web.web_research(f"{ten} điều chế", "vi.wikipedia")
            tc.trich_doan += r.get("ket_qua", [])
            tc.nguon += [t["url"] for t in r.get("ket_qua", [])]
        finally:
            web.dong()
    else:
        kh.buoc, kh.bac = "tra_cuu", ("lite" if muc == "de" else "flash")
        tc = research.tra_cuu(de, hien_chrome, job.emit, hsg=hsg)
    job.emit("tra_cuu", f"Đã tra {len(tc.pubchem)} chất trên PubChem, đọc {len(tc.trich_doan)} trang web", "xong")

    # 3. Giải
    job.emit("giai", "Đang giải chuỗi..." if not demo else "Chế độ demo: dùng lời giải mẫu soạn sẵn", "bat_dau")
    phien = None
    co_claude = kh.claude is not None
    if demo:
        sol = LoiGiai.model_validate(bai_demo["loi_giai"])
        mo_hinh = "chế độ demo"
    else:
        kh.buoc, kh.bac = "giai", do_kho.bac_giai(muc, co_claude, getattr(kh.claude, "moi_muc", False))
        job.emit("giai", f"Mức {do_kho.TEN_MUC[muc]} → giải bằng {TEN_BAC[kh.bac]}")
        while True:
            try:
                sol, phien, mo_hinh = solver.giai(de, tc, bao=bao("giai"), hsg=hsg)
                break
            except LoiAI as e:  # JSON hỏng 2 lần → giải lại bằng mô hình cao hơn một bậc
                tiep = do_kho.bac_tiep(kh.bac, co_claude) if e.loai == "dinh_dang" else None
                if tiep is None:
                    raise
                job.emit("giai", f"{TEN_BAC[kh.bac]} trả kết quả hỏng 2 lần → giải lại bằng {TEN_BAC[tiep]}", "canh_bao")
                kh.bac, kh.nang_cap = tiep, True
    giu_tac_nhan_de_cho(sol, de)
    job.emit("giai", f"Đã có lời giải {len(sol.cac_buoc)} bước ({mo_hinh})", "xong")

    # 4. Kiểm tra bằng RDKit, sửa tối đa SO_VONG_SUA vòng
    job.emit("kiem_tra", "Kiểm tra SMILES và cân bằng phương trình bằng RDKit...", "bat_dau")
    for vong in range(SO_VONG_SUA + 1):
        loi = danh_sach_loi(kiem_tra_loi_giai(sol, de))
        if not loi:
            job.emit("kiem_tra", "Tất cả phương trình hợp lệ và đã cân bằng", "dang_chay")
            break
        job.emit("kiem_tra", f"Phát hiện {len(loi)} lỗi: " + "; ".join(loi[:3]) + ("..." if len(loi) > 3 else ""), "canh_bao")
        if vong == SO_VONG_SUA or phien is None:
            break
        tiep = do_kho.bac_tiep(phien.bac, co_claude)
        if tiep is not None:  # lời giải sai → giải lại bằng mô hình cao hơn một bậc (kèm danh sách lỗi)
            job.emit("kiem_tra", f"Lời giải còn lỗi → giải lại bằng {TEN_BAC[tiep]} (vòng {vong + 1}/{SO_VONG_SUA})...")
            kh.buoc, kh.bac, kh.nang_cap = "giai", tiep, True
            try:
                sol, phien, mo_hinh = solver.giai(de, tc, bao=bao("giai"), hsg=hsg, loi_truoc=loi)
            except LoiAI as e:
                if e.loai != "dinh_dang":
                    raise
                job.emit("kiem_tra", f"{TEN_BAC[tiep]} trả kết quả hỏng – giữ lời giải trước", "canh_bao")
                break
        else:  # đã ở mô hình cao nhất được phép → nhờ chính mô hình đó sửa
            kh.buoc = "sua"
            job.emit("kiem_tra", f"Gửi lỗi cho AI sửa (vòng {vong + 1}/{SO_VONG_SUA})...")
            sol, mo_hinh = solver.sua_loi(phien, loi)
        giu_tac_nhan_de_cho(sol, de)
    canh_bao = danh_sach_loi(kiem_tra_loi_giai(sol, de))
    if canh_bao:
        job.emit("kiem_tra", f"Còn {len(canh_bao)} lỗi – các bước liên quan được ghi “cần kiểm tra lại”", "canh_bao")
    job.emit("kiem_tra", "Kiểm tra xong", "xong")
    _gop_nguon(sol, tc.nguon)

    # 5. Vẽ + xuất file
    job.emit("ve", "Đang vẽ công thức cấu tạo và ảnh đáp án...", "bat_dau")
    job.ket_qua = _xuat_tep(job, sol, de.de_bai or chuoi, mo_hinh, demo, canh_bao)
    job.ket_qua.update(muc_do=muc, ten_muc_do=do_kho.TEN_MUC.get(muc, ""), ly_do_muc_do=job.ly_do_muc_do,
                       nang_cap=kh.nang_cap, dung_claude=kh.claude_thanh_cong,
                       # giao diện hiển thị dạng chuỗi; lời giải mẫu đã có sẵn phương trình (không cần gọi AI)
                       gon=giai_nhanh.gon_tu_loi_giai(sol),
                       phuong_trinh=[{"buoc": b.so, "phuong_trinh": b.phuong_trinh, "ghi_chu": b.giai_thich} for b in sol.cac_buoc])
    job.emit("ve", "Đã xuất PNG, CDXML, SDF, JSON và ZIP", "xong")
    job.emit("xong", "Hoàn tất!", "xong")
