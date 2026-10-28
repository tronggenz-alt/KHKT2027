"""Tìm kiến thức liên quan cho một đề (AI Riêng): kết hợp tìm theo nghĩa (vector) + theo chữ (FTS5, công thức khớp chính xác),
trộn thứ hạng bằng RRF, nhân hệ số theo loại (sửa sai > quy tắc theo độ ưu tiên > bài mẫu > tài liệu) → khối kiến thức đánh nhãn
[S1] [Q1] [M1] [T1] để ghép vào prompt; mô hình ghi nhãn đã dùng ở can_cu từng bước.
"""
from __future__ import annotations

import json
import logging
import re
import unicodedata
from dataclasses import dataclass, field

from .. import db
from ..llm import LoiAI
from . import kho, nhung

log = logging.getLogger(__name__)

TIEN_TO = {"sua_sai": "S", "kien_thuc": "Q", "bai_mau": "M", "doan": "T"}
TEN_LOAI = {"sua_sai": "Sửa sai", "kien_thuc": "Quy tắc", "bai_mau": "Bài mẫu", "doan": "Tài liệu"}
HE_SO = {"sua_sai": 1.6, "kien_thuc": 1.15, "bai_mau": 1.1, "doan": 0.9}
TOI_DA_LOAI = {"sua_sai": 6, "kien_thuc": 12, "bai_mau": 3, "doan": 6}
DO_DAI = {"sua_sai": 1500, "kien_thuc": 1500, "bai_mau": 2500, "doan": 2800}
_TU_BO = {"va", "voi", "cua", "cho", "cac", "mot", "la", "co", "khong", "duoc", "trong", "tren", "thanh", "tu", "den", "theo", "bang",
          "nhung", "nay", "do", "khi", "thi", "hay", "hoac", "tac", "dung", "phan", "ung", "chat", "san", "pham", "dieu", "kien", "xt"}
RRF_K = 60


@dataclass
class Muc:
    loai: str
    id: int
    diem: float
    tieu_de: str = ""
    noi_dung: str = ""
    trang: int | None = None
    chu_de: str = ""
    nhan: str = ""
    them: dict = field(default_factory=dict)

    def cong_khai(self, admin: bool = True) -> dict:
        """Cho giao diện: người dùng thường chỉ thấy loại + tiêu đề (không id, không nội dung)."""
        d = {"nhan": self.nhan, "loai": self.loai, "ten_loai": TEN_LOAI[self.loai], "tieu_de": self.tieu_de, "trang": self.trang}
        if admin:
            d.update(id=self.id, diem=round(self.diem, 4), chu_de=self.chu_de)
        return d


def _bo_dau(t: str) -> str:
    return "".join(c for c in unicodedata.normalize("NFD", t) if not unicodedata.combining(c))


def _cau_fts(de: str) -> str:
    tu = []
    for t in re.findall(r"[0-9A-Za-zÀ-ỹ]+", kho.chu_tim(de)):
        thuong = t.lower()
        if len(t) < 2 or _bo_dau(thuong) in _TU_BO or thuong in tu:
            continue
        tu.append(thuong)
    return " OR ".join(f'"{t}"' for t in tu[:40])


def _theo_chu(de: str, k: int = 60) -> list[tuple[str, int]]:
    cau = _cau_fts(de)
    if not cau:
        return []
    try:
        with db.ket_noi() as con:
            return [(r[0], int(r[1])) for r in con.execute(
                "SELECT loai, ref_id FROM ar_fts WHERE ar_fts MATCH ? ORDER BY bm25(ar_fts) LIMIT ?", (cau, k))]
    except db.sqlite3.OperationalError as e:
        log.warning("FTS lỗi (%s) – bỏ qua tìm theo chữ", e)
        return []


def _theo_nghia(de: str, nguong: float, k: int = 60) -> list[tuple[str, int]]:
    if not nhung.co_the():
        return []
    try:
        qv = nhung.nhung([de], truy_van=True)[0]
    except LoiAI as e:
        log.info("Không nhúng được câu hỏi (%s) – chỉ tìm theo chữ", e)
        return []
    return [(loai, mid) for loai, mid, _ in nhung.tim(qv, k, nguong)]


def _chi_tiet(ung_vien: list[tuple[str, int]]) -> dict[tuple[str, int], Muc]:
    """Nạp nội dung các ứng viên CÒN BẬT (tài liệu tắt / bài mẫu bị đánh dấu sai → bỏ)."""
    theo_loai: dict[str, list[int]] = {}
    for loai, mid in ung_vien:
        theo_loai.setdefault(loai, []).append(mid)
    out: dict[tuple[str, int], Muc] = {}
    with db.ket_noi() as con:
        for loai, ids in theo_loai.items():
            dau = ",".join("?" * len(ids))
            if loai == "doan":
                rows = con.execute(f"SELECT d.id, d.noi_dung, d.trang, t.ten, t.chu_de FROM ar_doan d JOIN ar_tai_lieu t ON t.id = d.tai_lieu_id "
                                   f"WHERE d.id IN ({dau}) AND t.bat = 1 AND t.trang_thai = 'da_nap'", ids)
                for r in rows:
                    out[(loai, r["id"])] = Muc(loai, r["id"], 0, r["ten"], r["noi_dung"], r["trang"], r["chu_de"])
            elif loai == "kien_thuc":
                for r in con.execute(f"SELECT * FROM ar_kien_thuc WHERE id IN ({dau}) AND bat = 1", ids):
                    out[(loai, r["id"])] = Muc(loai, r["id"], 0, r["tieu_de"], r["noi_dung"], None, r["chu_de"], them={"uu_tien": r["uu_tien"]})
            elif loai == "bai_mau":
                for r in con.execute(f"SELECT * FROM ar_bai_mau WHERE id IN ({dau}) AND bat = 1 AND trang_thai != 'sai'", ids):
                    out[(loai, r["id"])] = Muc(loai, r["id"], 0, r["de"][:140], kho.tom_tat_loi_giai(json.loads(r["loi_giai"])), r["trang"],
                                               r["chu_de"], them={"de": r["de"], "trang_thai": r["trang_thai"]})
            elif loai == "sua_sai":
                for r in con.execute(f"SELECT * FROM ar_sua_sai WHERE id IN ({dau}) AND bat = 1", ids):
                    out[(loai, r["id"])] = Muc(loai, r["id"], 0, r["de"][:140], r["dung"], None, r["chu_de"],
                                               them={"de": r["de"], "sai": r["sai"], "ly_do": r["ly_do"]})
    return out


def tim_kien_thuc(de: str, k: int | None = None) -> list[Muc]:
    """Top-k mục liên quan nhất (đã đánh nhãn S/Q/M/T theo thứ tự ưu tiên). Sửa sai của ĐÚNG đề này luôn có mặt."""
    cd = kho.cai_dat()
    k = int(k or cd["top_k"])
    if not (de or "").strip():
        return []
    diem: dict[tuple[str, int], float] = {}
    for ds in (_theo_nghia(de, float(cd["nguong_vector"])), _theo_chu(de)):
        for hang, khoa in enumerate(ds):
            diem[khoa] = diem.get(khoa, 0.0) + 1.0 / (RRF_K + hang + 1)
    khoa_de = kho.khoa_de(de)
    with db.ket_noi() as con:  # sửa sai / bài mẫu của đúng đề này: luôn lấy, điểm cao nhất
        for r in con.execute("SELECT id FROM ar_sua_sai WHERE de_khoa = ? AND bat = 1", (khoa_de,)):
            diem[("sua_sai", r[0])] = diem.get(("sua_sai", r[0]), 0) + 1.0
        for r in con.execute("SELECT id FROM ar_bai_mau WHERE de_khoa = ? AND bat = 1 AND trang_thai = 'duyet'", (khoa_de,)):
            diem[("bai_mau", r[0])] = diem.get(("bai_mau", r[0]), 0) + 0.5
    if not diem:
        return []
    chi_tiet = _chi_tiet(list(diem))
    ung_vien = []
    for khoa, d in diem.items():
        m = chi_tiet.get(khoa)
        if m is None:
            continue
        he_so = HE_SO[m.loai] * (1 + 0.08 * (m.them.get("uu_tien", 3) - 3) if m.loai == "kien_thuc" else 1)
        m.diem = d * he_so
        ung_vien.append(m)
    ung_vien.sort(key=lambda m: -m.diem)
    chon: list[Muc] = []
    dem: dict[str, int] = {}
    for m in ung_vien:
        if len(chon) >= k and not (m.loai == "sua_sai" and m.diem >= 1.0):
            continue
        if dem.get(m.loai, 0) >= {**TOI_DA_LOAI, "doan": int(cd["k_doan"]), "kien_thuc": int(cd["k_kien_thuc"])}[m.loai]:
            continue
        dem[m.loai] = dem.get(m.loai, 0) + 1
        chon.append(m)
    thu_tu = list(TIEN_TO)
    chon.sort(key=lambda m: (thu_tu.index(m.loai), -m.diem))
    so: dict[str, int] = {}
    for m in chon:
        so[m.loai] = so.get(m.loai, 0) + 1
        m.nhan = f"{TIEN_TO[m.loai]}{so[m.loai]}"
    return chon


def _cat(t: str, n: int) -> str:
    t = (t or "").strip()
    return t if len(t) <= n else t[:n].rsplit(" ", 1)[0] + " …"


def khoi_prompt(muc: list[Muc], che_do: str) -> str:
    """Khối KHO KIẾN THỨC RIÊNG + yêu cầu ghi căn cứ (đặt ở lượt người dùng, sau đề)."""
    dong = []
    for m in muc:
        if m.loai == "sua_sai":
            dong.append(f"[{m.nhan}] SỬA SAI (ưu tiên cao nhất) – Đề: {_cat(m.them['de'], 600)}\n"
                        + (f"  AI từng trả lời sai: {_cat(m.them['sai'], 500)}\n" if m.them.get("sai") else "")
                        + f"  ĐÁP ÁN ĐÚNG: {_cat(m.noi_dung, DO_DAI['sua_sai'])}"
                        + (f"\n  Lý do: {_cat(m.them['ly_do'], 600)}" if m.them.get("ly_do") else ""))
        elif m.loai == "kien_thuc":
            dong.append(f"[{m.nhan}] QUY TẮC «{m.tieu_de}» (ưu tiên {m.them.get('uu_tien', 3)}/5"
                        + (f"; {m.chu_de}" if m.chu_de else "") + f"): {_cat(m.noi_dung, DO_DAI['kien_thuc'])}")
        elif m.loai == "bai_mau":
            dong.append(f"[{m.nhan}] BÀI MẪU – Đề: {_cat(m.them['de'], 600)}\n{_cat(m.noi_dung, DO_DAI['bai_mau'])}")
        else:
            dong.append(f"[{m.nhan}] TÀI LIỆU «{m.tieu_de}»" + (f" tr. {m.trang}" if m.trang else "") + f": {_cat(m.noi_dung, DO_DAI['doan'])}")
    kho_chu = "\n\n".join(dong) if dong else "(Kho chưa có mục nào liên quan tới đề này.)"
    if che_do == "nghiem_ngat":
        luat = ("CHẾ ĐỘ NGHIÊM NGẶT – chỉ dùng kiến thức trong kho. Bước nào kho không có căn cứ: can_cu = \"chưa được dạy\" và ghi một dòng "
                "vào chua_duoc_day (vd \"Bước 2 C2H5OH → CH3CHO: chưa được dạy phản ứng oxi hoá ancol bằng CuO\"); vẫn điền chuỗi đầy đủ "
                "(app dùng để chỉ cho giáo viên phần cần dạy thêm, KHÔNG đưa cho học sinh).")
    else:
        luat = ("CHẾ ĐỘ CHO PHÉP BỔ SUNG – ưu tiên kho; bước kho không có thì dùng kiến thức chung, can_cu = \"kiến thức chung\". "
                "chua_duoc_day = [].")
    return ("KHO KIẾN THỨC RIÊNG (giáo viên đã dạy – ưu tiên hơn hiểu biết chung):\n" + kho_chu
            + "\n\nYÊU CẦU: mỗi bước ghi can_cu = nhãn đã dựa vào (vd \"[Q2]\", \"[M1], [T3]\"). Kho mâu thuẫn với hiểu biết chung → "
              "theo kho và ghi vào mau_thuan. " + luat)


def nhan_da_dung(buoc: list) -> list[str]:
    """Nhãn [Q2] / [M1, T3] mô hình ghi trong can_cu các bước (theo thứ tự xuất hiện)."""
    out: list[str] = []
    for b in buoc:
        cc = b.get("can_cu", "") if isinstance(b, dict) else getattr(b, "can_cu", "")
        for nhom in re.findall(r"\[([^\]]{1,60})\]", cc or ""):
            for n in re.findall(r"\b[SQMT]\d{1,3}\b", nhom):
                if n not in out:
                    out.append(n)
    return out
