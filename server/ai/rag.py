"""RAG: tra kho kiến thức Hóa học, Sinh học THPT trước khi AI trả lời.

Nguồn:
    data/kien_thuc/**/*.md     lý thuyết, chuyên đề, bảng tra, thực hành (mỗi mục `##` là một đoạn)
    data/chem, data/reactions  720 hóa chất, phản ứng, thiết bị, nhiệt độ sôi
    data/may                   hồ sơ máy phân tích
    data/sinh                  bài thí nghiệm, dụng cụ, chủng vi sinh, môi trường, tiêu bản, sinh phẩm

Tìm kiếm lai: BM25 trên từ đã bỏ dấu (gõ không dấu vẫn tìm được) + vector bge-m3 qua Ollama, gộp bằng
Reciprocal Rank Fusion. Không có Ollama hoặc chưa dựng vector thì chỉ dùng BM25.

Dựng kho có vector:  .venv/bin/python tools/xay_kho_rag.py   (ghi data/rag.db)
Không có data/rag.db: máy chủ tự cắt đoạn từ các file lúc khởi động, chỉ tìm bằng từ khóa.

Biến môi trường:
    RAG_TAT=1             tắt RAG
    RAG_DB                đường dẫn kho (mặc định data/rag.db)
    RAG_EMBED_URL         Ollama chạy bge-m3 (mặc định http://localhost:11434)
    RAG_EMBED_MODEL       mặc định bge-m3
    RAG_SO_DOAN           số đoạn đưa cho AI (mặc định 5)
    RAG_MAX_KY_TU         tổng ký tự tài liệu tối đa (mặc định 9000, vừa ngữ cảnh 8192 token của mô hình 32B)
"""

from __future__ import annotations

import hashlib
import json
import math
import os
import re
import sqlite3
import time
import unicodedata
from collections import Counter
from dataclasses import dataclass, field
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
KIEN_THUC = ROOT / "data" / "kien_thuc"
DB_MAC_DINH = ROOT / "data" / "rag.db"

DOAN_TOI_DA = 3200   # ký tự; đoạn dài hơn được tách theo đoạn văn
DOAN_TOI_THIEU = 80  # ký tự; ngắn hơn thì bỏ (tiêu đề trơn)


# ---------------------------------------------------------------------------
# Chuẩn hóa chữ
# ---------------------------------------------------------------------------

_SUB = str.maketrans("₀₁₂₃₄₅₆₇₈₉⁰¹²³⁴⁵⁶⁷⁸⁹⁺⁻", "01234567890123456789+-")


def fold(s: str) -> str:
    """Chữ thường, bỏ dấu tiếng Việt, chỉ số dưới thành số thường."""
    s = (s or "").translate(_SUB).replace("đ", "d").replace("Đ", "D")
    s = "".join(c for c in unicodedata.normalize("NFD", s) if unicodedata.category(c) != "Mn")
    return s.lower()


_STOP = set("""
la va cua cac cho voi nhung mot co khong duoc nay do thi trong khi de ve tu ra vao tren duoi nhu the nao gi sao
bao nhieu hay hoac cung da se dang rat nhieu it hon nhat theo cach ban minh em toi oi a nhe ha vay ma neu
""".split())


def tokens(s: str) -> list[str]:
    """Từ đơn (bỏ dấu) và cặp từ liền nhau: tiếng Việt nhiều từ hai âm tiết ("te bao", "oxi hoa")."""
    words = re.findall(r"[a-z0-9]+", fold(s))
    uni = [w for w in words if w not in _STOP and (len(w) > 1 or w.isdigit())]
    bi = [f"{a}_{b}" for a, b in zip(words, words[1:]) if not (a in _STOP and b in _STOP)]
    return uni + bi


# ---------------------------------------------------------------------------
# Đoạn tài liệu
# ---------------------------------------------------------------------------

@dataclass
class Doan:
    nguon: str           # đường dẫn tương đối từ data/, ví dụ kien_thuc/hoa/lop10/04-....md
    tieu_de: str         # "Tên file › Tên mục"
    noi_dung: str
    mon: str = ""        # hoa | sinh | ""
    lop: int = 0
    loai: str = ""       # ly_thuyet | chuyen_de | tra_cuu | thuc_hanh | du_lieu
    vec: list[float] | None = field(default=None, repr=False)

    @property
    def bam(self) -> str:
        return hashlib.sha1((self.tieu_de + "\n" + self.noi_dung).encode("utf-8")).hexdigest()

    def van_ban(self) -> str:
        """Chữ đem đi nhúng vector và đánh chỉ mục."""
        return f"{self.tieu_de}\n{self.noi_dung}"


def doc_front_matter(text: str) -> tuple[dict, str]:
    """Đọc phần đầu YAML đơn giản (khóa: giá trị, danh sách [a, b] hoặc gạch đầu dòng)."""
    if not text.startswith("---"):
        return {}, text
    end = text.find("\n---", 3)
    if end < 0:
        return {}, text
    meta: dict = {}
    key = None
    for line in text[3:end].splitlines():
        line = line.split(" #")[0].rstrip() if not line.lstrip().startswith("- ") else line.rstrip()
        if not line.strip():
            continue
        m = re.match(r"^([A-Za-z_]+):\s*(.*)$", line)
        if m:
            key, val = m.group(1), m.group(2).strip()
            if val.startswith("[") and val.endswith("]"):
                meta[key] = [v.strip().strip("\"'") for v in val[1:-1].split(",") if v.strip()]
            elif val == "":
                meta[key] = []
            else:
                meta[key] = val.strip("\"'")
        elif key and line.lstrip().startswith("- "):
            if not isinstance(meta.get(key), list):
                meta[key] = []
            meta[key].append(line.lstrip()[2:].strip().strip("\"'"))
    return meta, text[end + 4:].lstrip("\n")


def _tach_dai(tieu_de: str, body: str) -> list[tuple[str, str]]:
    """Đoạn quá dài: tách theo đoạn văn, mỗi phần giữ nguyên tiêu đề."""
    if len(body) <= DOAN_TOI_DA:
        return [(tieu_de, body)]
    parts, cur = [], ""
    for para in re.split(r"\n\s*\n", body):
        if cur and len(cur) + len(para) > DOAN_TOI_DA * 0.75:
            parts.append(cur)
            cur = ""
        cur = (cur + "\n\n" + para).strip()
    if cur:
        parts.append(cur)
    if len(parts) == 1:
        return [(tieu_de, body)]
    return [(f"{tieu_de} (phần {i}/{len(parts)})", p) for i, p in enumerate(parts, 1)]


def cat_markdown(path: Path, goc: Path) -> list[Doan]:
    text = path.read_text(encoding="utf-8")
    meta, body = doc_front_matter(text)
    try:
        lop = int(str(meta.get("lop", 0)).strip() or 0)
    except ValueError:
        lop = 0
    mon, loai = str(meta.get("mon", "")), str(meta.get("loai", ""))
    nguon = path.relative_to(goc).as_posix()
    tieu_de_tep = meta.get("chu_de") or path.stem
    tu_khoa = meta.get("tu_khoa") or []
    m = re.search(r"^# (.+)$", body, flags=re.M)
    if m:
        tieu_de_tep = m.group(1).strip()
    lop_txt = f" – lớp {lop}" if lop else ""
    mon_txt = {"hoa": "Hóa học", "sinh": "Sinh học"}.get(mon, "")
    nhan = f"{mon_txt}{lop_txt}: {tieu_de_tep}" if mon_txt else tieu_de_tep

    out: list[Doan] = []
    sections: list[tuple[str, list[str]]] = [("", [])]
    in_code = False
    for line in body.splitlines():
        if line.strip().startswith("```"):
            in_code = not in_code
        if not in_code and line.startswith("## "):
            sections.append((line[3:].strip(), []))
        elif not in_code and line.startswith("# ") and not sections[-1][1] and not sections[-1][0]:
            continue  # tiêu đề file đã nằm trong nhãn
        else:
            sections[-1][1].append(line)
    for i, (head, lines) in enumerate(sections):
        content = "\n".join(lines).strip()
        if i == 0 and tu_khoa:
            content = (content + "\n\nTừ khóa: " + ", ".join(tu_khoa)).strip()
        if len(content) < DOAN_TOI_THIEU:
            continue
        td = f"{nhan} › {head}" if head else f"{nhan} › Giới thiệu"
        for t, c in _tach_dai(td, content):
            out.append(Doan(nguon=nguon, tieu_de=t, noi_dung=c, mon=mon, lop=lop, loai=loai))
    return out


# ---------------------------------------------------------------------------
# Dữ liệu có cấu trúc của dự án
# ---------------------------------------------------------------------------

_BO_KHOA = {"mau", "x", "y", "tp", "cac_C", "tp_theo_lit", "phien_ban", "the", "do_trong", "ma_vi_tri", "stt", "ten_khong_dau",
            "kieu_tu", "so_tang", "so_o", "mau_rgb", "icon", "prefab", "vi_tri_3d"}
_NGUY_HIEM = {"an_mon": "ăn mòn", "doc": "độc", "oxi_hoa": "chất oxi hóa", "de_chay": "dễ cháy", "no": "dễ nổ",
              "phan_ung_nuoc": "phản ứng mạnh với nước", "kich_ung": "kích ứng", "moi_truong": "nguy hại môi trường"}
_TRANG_THAI = {"dd": "dung dịch", "ran": "chất rắn", "long": "chất lỏng", "khi": "chất khí", "ket_tua": "kết tủa điều chế sẵn",
               "giay": "giấy thử"}
_DANG_CHUA = {"binh_khi": "bình khí", "chai_nau": "chai thủy tinh nâu", "lo_rong": "lọ miệng rộng", "chai": "chai",
              "hop_giay": "hộp giấy", "lo_nho_giot": "lọ nhỏ giọt"}


def _gia_tri(v) -> str:
    if isinstance(v, dict):
        return "; ".join(f"{k}: {_gia_tri(x)}" for k, x in v.items() if k not in _BO_KHOA and x not in ("", None, [], {}))
    if isinstance(v, list):
        if v and all(isinstance(x, dict) for x in v):
            return " | ".join(_gia_tri(x) for x in v)
        return ", ".join(str(x) for x in v)
    if isinstance(v, float) and v.is_integer():
        return str(int(v))
    return str(v)


def _ve_muc(d: dict) -> str:
    return "\n".join(f"{k.replace('_', ' ')}: {_gia_tri(v)}" for k, v in d.items()
                     if k not in _BO_KHOA and v not in ("", None, [], {}, 0, 0.0))


def _hoa_chat(c: dict, nhom: dict) -> str:
    vt = c.get("vi_tri") or {}
    ct = f" – công thức {c['cong_thuc']}" if c.get("cong_thuc") else ""
    dong = [f"{c['ten']}{ct}; {c.get('phan_loai', '')}; nhóm kệ {nhom.get(c.get('nhom'), c.get('nhom', ''))}."]
    tt = _TRANG_THAI.get(c.get("trang_thai", ""), c.get("trang_thai", ""))
    nd = f", nồng độ {c['nong_do']}" if c.get("nong_do") else ""
    chua = _DANG_CHUA.get(c.get("dang_chua", ""), str(c.get("dang_chua", "")).replace("_", " "))
    dong.append(f"Dạng trong kho: {tt}{nd}, đựng trong {chua}.")
    so = []
    if c.get("M"):
        so.append(f"khối lượng mol M = {c['M']} g/mol")
    if c.get("d"):
        so.append(f"khối lượng riêng d = {c['d']} g/mL")
    if c.get("C"):
        so.append(f"nồng độ mol {c['C']} M")
    if c.get("ka"):
        so.append(f"Ka = {c['ka']}")
    if c.get("kb"):
        so.append(f"Kb = {c['kb']}")
    if so:
        dong.append("Số liệu: " + ", ".join(so) + ".")
    if c.get("nguy_hiem"):
        dong.append("Nguy hiểm: " + ", ".join(_NGUY_HIEM.get(h, h.replace("_", " ")) for h in c["nguy_hiem"]) + ".")
    if vt:
        dong.append(f"Vị trí trong phòng hóa chất: kệ {vt.get('ke')}, tầng {vt.get('tang')}, ô {vt.get('o')} (mã {c.get('ma_vi_tri', '')}).")
    if c.get("ghi_chu"):
        dong.append("Ghi chú: " + c["ghi_chu"])
    return "\n".join(dong)


def _phan_ung(r: dict) -> str:
    hien = "; ".join(" ".join(str(h.get(k, "")) for k in ("loai", "ten_mau", "ten") if h.get(k)).replace("_", " ")
                     for h in r.get("hien_tuong", []))
    dong = [f"Phương trình: {r.get('phuong_trinh', '')}", f"Hiện tượng: {hien}" if hien else "",
            f"Ý nghĩa định tính: {r.get('dinh_tinh', '')}" if r.get("dinh_tinh") else "",
            r.get("the_thong_tin", ""), ("An toàn: " + " ".join(r.get("an_toan", []))) if r.get("an_toan") else ""]
    return "\n".join(x for x in dong if x)


def _may(d: dict) -> list[tuple[str, str]]:
    """Một máy phân tích thành vài đoạn: nguyên lý, bộ phận và thông số, quy trình, an toàn và thuật ngữ, ôn tập."""
    ten = d.get("ten", d.get("ma", ""))
    nhom = [("Nguyên lý và công thức", ["nguyen_ly", "cong_thuc"]), ("Bộ phận và thông số", ["bo_phan", "thong_so", "may_mau"]),
            ("Quy trình vận hành", ["quy_trinh"]), ("An toàn và thuật ngữ", ["an_toan", "thuat_ngu"]),
            ("Câu hỏi ôn tập", ["cau_hoi_on_tap"])]
    out = []
    for nhan, keys in nhom:
        phan = []
        for k in keys:
            v = d.get(k)
            if not v:
                continue
            if k == "quy_trinh":
                phan.append("\n".join(f"{i}. {_gia_tri(s)}" for i, s in enumerate(v, 1)))
            else:
                phan.append(f"{k.replace('_', ' ')}: {_gia_tri(v)}")
        if phan:
            out.append((f"Máy {ten} › {nhan}", "\n".join(phan)))
    return out


def cat_du_lieu(data_dir: Path) -> list[Doan]:
    """Đoạn từ JSON của dự án (đường dẫn nguon tương đối từ data/)."""
    out: list[Doan] = []

    def them(nguon, tieu_de, noi_dung, mon, loai="du_lieu"):
        for t, c in _tach_dai(tieu_de, noi_dung.strip()):
            if len(c) >= 30:
                out.append(Doan(nguon=nguon, tieu_de=t, noi_dung=c, mon=mon, loai=loai))

    def doc(p: Path):
        try:
            return json.loads(p.read_text(encoding="utf-8"))
        except (OSError, ValueError):
            return None

    p = data_dir / "chem" / "hoa_chat.json"
    d = doc(p)
    if d:
        nhom = {n["ma"]: n["ten"] for n in d.get("nhom", [])}
        for c in d.get("hoa_chat", []):
            ct = f" ({c['cong_thuc']})" if c.get("cong_thuc") else ""
            them("chem/hoa_chat.json", f"Hóa chất trong kho VERA Lab › {c['ten']}{ct}", _hoa_chat(c, nhom), "hoa")
    d = doc(data_dir / "reactions" / "phan_ung.json")
    if d:
        for r in d.get("phan_ung", []):
            them("reactions/phan_ung.json", f"Phản ứng trong phòng thí nghiệm ảo › {r.get('phuong_trinh', r.get('id'))}", _phan_ung(r), "hoa")
    d = doc(data_dir / "chem" / "thiet_bi.json")
    if d:
        for t in d.get("thiet_bi", []):
            them("chem/thiet_bi.json", f"Dụng cụ, thiết bị phòng Hóa › {t.get('ten')}", _ve_muc(t), "hoa")
    d = doc(data_dir / "chem" / "nhiet_do_soi.json")
    if d:
        chat = d.get("chat", [])
        for i in range(0, len(chat), 12):
            nhom12 = chat[i:i + 12]
            dong = [f"{c['ten']}: nhiệt độ sôi {c.get('ts')} °C" + (f", nhiệt hóa hơi {c['dh']} kJ/mol" if c.get("dh") else "") for c in nhom12]
            them("chem/nhiet_do_soi.json", f"Nhiệt độ sôi các chất › {nhom12[0]['ten']} … {nhom12[-1]['ten']}", "\n".join(dong), "hoa")
    for p in sorted((data_dir / "may").glob("*.json")):
        d = doc(p)
        if isinstance(d, dict) and "quy_trinh" in d:
            mon = "sinh" if any(k in p.stem for k in ("dien_di", "gel", "pcr", "li_tam", "kinh_hien_vi", "vi_sinh")) else "hoa"
            for t, c in _may(d):
                them(f"may/{p.name}", t, c, mon)
    for p in sorted((data_dir / "may" / "tinh_chat").glob("*.json")):
        d = doc(p)
        if not isinstance(d, dict):
            continue
        for k, v in d.items():
            if isinstance(v, list) and v and isinstance(v[0], dict):
                for i in range(0, len(v), 6):
                    them(f"may/tinh_chat/{p.name}", f"Tính chất đo bằng máy ({p.stem.replace('_', ' ')}) › {k.replace('_', ' ')} {i + 1}–{i + len(v[i:i + 6])}",
                         "\n".join(_ve_muc(x) for x in v[i:i + 6]), "hoa")
    nhan_sinh = {"bai_thi_nghiem": "Bài thí nghiệm Sinh học", "dung_cu": "Dụng cụ phòng Sinh", "chung_vi_sinh": "Chủng vi sinh vật (BSL-1)",
                 "moi_truong": "Môi trường nuôi cấy", "tieu_ban": "Tiêu bản hiển vi", "sinh_pham": "Sinh phẩm trong kho"}
    for p in sorted((data_dir / "sinh").glob("*.json")):
        d = doc(p)
        if not isinstance(d, dict):
            continue
        for k, v in d.items():
            if isinstance(v, list) and v and isinstance(v[0], dict):
                for x in v:
                    ten = x.get("ten") or x.get("ma") or x.get("id")
                    them(f"sinh/{p.name}", f"{nhan_sinh.get(k, k)} › {ten}", _ve_muc(x), "sinh")
    return out


def cat_tat_ca(kien_thuc: Path = KIEN_THUC, data_dir: Path | None = None) -> list[Doan]:
    data_dir = data_dir or kien_thuc.parent
    out: list[Doan] = []
    if kien_thuc.exists():
        for p in sorted(kien_thuc.rglob("*.md")):
            if p.name.lower() == "readme.md" or "_kiem_tra" in p.parts:
                continue
            out.extend(cat_markdown(p, data_dir))
    out.extend(cat_du_lieu(data_dir))
    return out


# ---------------------------------------------------------------------------
# Kho và tìm kiếm
# ---------------------------------------------------------------------------

class Kho:
    K1, B = 1.4, 0.72

    def __init__(self, doan: list[Doan], mo_hinh_vec: str = "", nguon: str = "tep"):
        self.doan = doan
        self.mo_hinh_vec = mo_hinh_vec
        self.nguon = nguon  # "tep" (cắt lúc chạy) | "db"
        # từ trong tiêu đề tính gấp 3 (kiểu BM25F): tiêu đề nói đoạn này về cái gì
        self._tf = [Counter(tokens(d.noi_dung)) + Counter({t: 3 * n for t, n in Counter(tokens(d.tieu_de)).items()}) for d in doan]
        self._len = [sum(c.values()) for c in self._tf]
        self._avg = (sum(self._len) / len(self._len)) if self._len else 1.0
        self._post: dict[str, list[tuple[int, int]]] = {}
        for i, c in enumerate(self._tf):
            for t, f in c.items():
                self._post.setdefault(t, []).append((i, f))
        n = len(doan)
        self._idf = {t: math.log(1 + (n - len(p) + 0.5) / (len(p) + 0.5)) for t, p in self._post.items()}
        self._mat = None
        if doan and all(d.vec for d in doan):
            import numpy as np
            m = np.asarray([d.vec for d in doan], dtype="float32")
            m /= np.linalg.norm(m, axis=1, keepdims=True) + 1e-9
            self._mat = m

    @property
    def co_vector(self) -> bool:
        return self._mat is not None

    def bm25(self, cau: str, k: int = 30, mon: str = "") -> list[tuple[int, float]]:
        acc: dict[int, float] = {}
        for t in set(tokens(cau)):
            idf = self._idf.get(t)
            if idf is None:
                continue
            for i, f in self._post[t]:
                if mon and self.doan[i].mon not in (mon, ""):
                    continue
                norm = self.K1 * (1 - self.B + self.B * self._len[i] / self._avg)
                acc[i] = acc.get(i, 0.0) + idf * f * (self.K1 + 1) / (f + norm)
        return sorted(acc.items(), key=lambda x: -x[1])[:k]

    def vector(self, qvec: list[float], k: int = 30, mon: str = "") -> list[tuple[int, float]]:
        if self._mat is None or not qvec:
            return []
        import numpy as np
        q = np.asarray(qvec, dtype="float32")
        q /= np.linalg.norm(q) + 1e-9
        sims = self._mat @ q
        order = np.argsort(-sims)
        out = []
        for i in order:
            if mon and self.doan[int(i)].mon not in (mon, ""):
                continue
            out.append((int(i), float(sims[i])))
            if len(out) >= k:
                break
        return out

    def tim(self, cau: str, k: int = 5, qvec: list[float] | None = None, mon: str = "") -> list[Doan]:
        """Gộp hai danh sách bằng Reciprocal Rank Fusion; mỗi file tối đa 2 đoạn để đa dạng nguồn."""
        lists = [self.bm25(cau, 40, mon)]
        if qvec is not None and self._mat is not None:
            lists.append(self.vector(qvec, 40, mon))
        fused: dict[int, float] = {}
        for lst in lists:
            for rank, (i, _) in enumerate(lst):
                fused[i] = fused.get(i, 0.0) + 1.0 / (60 + rank)
        order = sorted(fused, key=lambda i: -fused[i])
        out, per_file = [], Counter()
        for i in order:
            d = self.doan[i]
            if per_file[d.nguon] >= 2:
                continue
            per_file[d.nguon] += 1
            out.append(d)
            if len(out) >= k:
                break
        return out


# ---------------------------------------------------------------------------
# Lưu và nạp kho (SQLite)
# ---------------------------------------------------------------------------

def luu(doan: list[Doan], path: Path, mo_hinh_vec: str) -> None:
    import numpy as np
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_suffix(".tmp")
    tmp.unlink(missing_ok=True)
    con = sqlite3.connect(tmp)
    con.executescript("""
        CREATE TABLE meta (khoa TEXT PRIMARY KEY, gia_tri TEXT);
        CREATE TABLE doan (id INTEGER PRIMARY KEY, nguon TEXT, tieu_de TEXT, noi_dung TEXT, mon TEXT, lop INTEGER,
                           loai TEXT, bam TEXT, vec BLOB);
    """)
    con.executemany("INSERT INTO meta VALUES (?, ?)", [("mo_hinh_vec", mo_hinh_vec), ("ngay_dung", time.strftime("%Y-%m-%d %H:%M:%S")),
                                                        ("so_doan", str(len(doan)))])
    con.executemany("INSERT INTO doan (nguon, tieu_de, noi_dung, mon, lop, loai, bam, vec) VALUES (?, ?, ?, ?, ?, ?, ?, ?)",
                    [(d.nguon, d.tieu_de, d.noi_dung, d.mon, d.lop, d.loai, d.bam,
                      np.asarray(d.vec, dtype="float32").tobytes() if d.vec else None) for d in doan])
    con.commit()
    con.close()
    os.replace(tmp, path)


def nap(path: Path) -> Kho | None:
    if not path.exists():
        return None
    import numpy as np
    con = sqlite3.connect(f"file:{path}?mode=ro", uri=True)
    try:
        meta = dict(con.execute("SELECT khoa, gia_tri FROM meta"))
        rows = con.execute("SELECT nguon, tieu_de, noi_dung, mon, lop, loai, vec FROM doan ORDER BY id").fetchall()
    except sqlite3.Error:
        return None
    finally:
        con.close()
    doan = [Doan(nguon=r[0], tieu_de=r[1], noi_dung=r[2], mon=r[3] or "", lop=r[4] or 0, loai=r[5] or "",
                 vec=np.frombuffer(r[6], dtype="float32").tolist() if r[6] else None) for r in rows]
    return Kho(doan, meta.get("mo_hinh_vec", ""), nguon="db")


def vector_cu(path: Path, mo_hinh_vec: str) -> dict[str, list[float]]:
    """Vector đã tính lần trước (theo mã băm đoạn) để dựng lại không phải nhúng lại đoạn không đổi."""
    if not path.exists():
        return {}
    import numpy as np
    con = sqlite3.connect(f"file:{path}?mode=ro", uri=True)
    try:
        meta = dict(con.execute("SELECT khoa, gia_tri FROM meta"))
        if meta.get("mo_hinh_vec") != mo_hinh_vec:
            return {}
        return {b: np.frombuffer(v, dtype="float32").tolist() for b, v in con.execute("SELECT bam, vec FROM doan WHERE vec IS NOT NULL")}
    except sqlite3.Error:
        return {}
    finally:
        con.close()


# ---------------------------------------------------------------------------
# Nhúng vector qua Ollama
# ---------------------------------------------------------------------------

def embed_url() -> str:
    return os.environ.get("RAG_EMBED_URL", "http://localhost:11434").rstrip("/")


def embed_model() -> str:
    return os.environ.get("RAG_EMBED_MODEL", "bge-m3")


def nhung(texts: list[str], timeout: float = 120.0) -> list[list[float]]:
    import httpx
    r = httpx.post(f"{embed_url()}/api/embed", json={"model": embed_model(), "input": texts}, timeout=timeout)
    r.raise_for_status()
    return r.json()["embeddings"]


_loi_nhung_den = 0.0  # lần lỗi gần nhất: tạm nghỉ 60 s để không làm chậm mọi câu hỏi khi Ollama tắt


async def nhung_cau_hoi(cau: str) -> list[float] | None:
    global _loi_nhung_den
    if time.time() - _loi_nhung_den < 60:
        return None
    try:
        import httpx
        async with httpx.AsyncClient(timeout=float(os.environ.get("RAG_EMBED_TIMEOUT", "4"))) as client:
            r = await client.post(f"{embed_url()}/api/embed", json={"model": embed_model(), "input": [cau]})
            r.raise_for_status()
            return r.json()["embeddings"][0]
    except Exception:
        _loi_nhung_den = time.time()
        return None


# ---------------------------------------------------------------------------
# Dùng trong máy chủ
# ---------------------------------------------------------------------------

_kho: Kho | None = None


def db_path() -> Path:
    return Path(os.environ.get("RAG_DB", str(DB_MAC_DINH)))


def tat() -> bool:
    return os.environ.get("RAG_TAT", "").lower() in ("1", "true", "co")


def kho() -> Kho:
    global _kho
    if _kho is None:
        _kho = nap(db_path()) or Kho(cat_tat_ca())
    return _kho


def dat_kho(k: Kho | None) -> None:
    """Đổi kho (kiểm thử, hoặc nạp lại sau khi dựng)."""
    global _kho
    _kho = k


async def tim(cau: str, k: int | None = None, mon: str = "") -> list[Doan]:
    if tat() or not (cau or "").strip():
        return []
    kh = kho()
    if not kh.doan:
        return []
    qvec = await nhung_cau_hoi(cau) if kh.co_vector else None
    return kh.tim(cau, k or int(os.environ.get("RAG_SO_DOAN", "5")), qvec, mon)


def dinh_dang(doan: list[Doan], max_ky_tu: int | None = None) -> str:
    """Khối TÀI LIỆU đưa vào lời nhắn cho AI, cắt theo ngân sách ký tự."""
    if not doan:
        return ""
    max_ky_tu = max_ky_tu or int(os.environ.get("RAG_MAX_KY_TU", "9000"))
    parts, used = [], 0
    for i, d in enumerate(doan, 1):
        block = f"[{i}] {d.tieu_de}\n{d.noi_dung}"
        if used + len(block) > max_ky_tu:
            con_lai = max_ky_tu - used
            if con_lai < 400:
                break
            block = block[:con_lai].rsplit("\n", 1)[0] + "\n…"
        parts.append(block)
        used += len(block)
    return "\n\n".join(parts)


async def ngu_canh(cau: str, k: int | None = None, mon: str = "", max_ky_tu: int | None = None) -> str:
    return dinh_dang(await tim(cau, k, mon), max_ky_tu)


def trang_thai() -> dict:
    if tat():
        return {"bat": False}
    kh = kho()
    nguon_moi = 0.0
    if KIEN_THUC.exists():
        nguon_moi = max((p.stat().st_mtime for p in KIEN_THUC.rglob("*.md")), default=0.0)
    p = db_path()
    cu = kh.nguon == "db" and p.exists() and nguon_moi > p.stat().st_mtime
    return {"bat": True, "so_doan": len(kh.doan), "vector": kh.co_vector, "mo_hinh_vec": kh.mo_hinh_vec,
            "nguon": kh.nguon, "can_dung_lai": cu}
