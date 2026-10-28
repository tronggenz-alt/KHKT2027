"""Bước 6 – Ảnh đáp án PNG (rộng 1920px, nền trắng) và gói ZIP kết quả."""
from __future__ import annotations

import datetime as dt
import re
import zipfile
from functools import lru_cache
from pathlib import Path

from fontTools.ttLib import TTFont
from PIL import Image, ImageDraw, ImageFont

from .config import FONT_BOLD, FONT_FALLBACK, FONT_ITALIC, FONT_REGULAR
from .renderer import chuan_bi_mol, doan_so_do, ve_mol

RONG = 1920
LE = 80
NOI_DUNG = RONG - 2 * LE

# Bảng màu (in trên nền trắng, gợi lại tông xanh lơ – tím của app)
MAU_CHU = (15, 23, 42)
MAU_PHU = (90, 102, 125)
MAU_NHAN = (26, 107, 69)    # xanh lá – số bước, kí hiệu, vạch tiêu đề mục (giống giao diện web)
MAU_LAM = (43, 92, 138)     # xanh lam – điều kiện trên mũi tên
MAU_LAM_SANG = (43, 111, 150)
MAU_DO = (200, 30, 45)
MAU_NEN_NHAT = (244, 246, 251)
MAU_VIEN = (222, 227, 238)

CAN_KIEM_TRA = "cần kiểm tra lại"
BOND_PX_BANG = 36  # độ dài liên kết của hình trong bảng chất ẩn (sơ đồ dùng renderer.BOND_PX)


# ---------------------------------------------------------------- font + chữ có chỉ số dưới
def _dau_tien(ds: list[str]) -> str:
    for f in ds:
        if Path(f).exists():
            return f
    raise FileNotFoundError("Không tìm thấy font Arial/Helvetica trên máy.")


@lru_cache(maxsize=None)
def _font(kieu: str, co: int) -> ImageFont.FreeTypeFont:
    ds = {"thuong": FONT_REGULAR, "dam": FONT_BOLD, "nghieng": FONT_ITALIC, "du_phong": FONT_FALLBACK + FONT_REGULAR}[kieu]
    return ImageFont.truetype(_dau_tien(ds), co)


@lru_cache(maxsize=None)
def _bang_ky_tu(path: str) -> frozenset:
    try:
        return frozenset(TTFont(path, fontNumber=0).getBestCmap())
    except Exception:  # noqa: BLE001
        return frozenset()


def _co_glyph(kieu: str, ch: str) -> bool:
    ds = {"thuong": FONT_REGULAR, "dam": FONT_BOLD, "nghieng": FONT_ITALIC}.get(kieu, FONT_REGULAR)
    return ord(ch) in _bang_ky_tu(_dau_tien(ds))


_SO_DUOI = str.maketrans("₀₁₂₃₄₅₆₇₈₉", "0123456789")
_DIEN_TICH = re.compile(r"(?<=[A-Za-z)\]])(\d*[+-])(?=$|[\s,.;)])")


_GACH_NOI = re.compile(r"(?<=[A-Za-z0-9)\]])-(?=[A-Z(\[])")


def dep_cong_thuc(text: str) -> str:
    """Gạch nối giữa các nhóm trong công thức thu gọn: "CH3-CHBr-CH3" → "CH3–CHBr–CH3" (không đụng điện tích OH-, tên propan-2-ol)."""
    return _GACH_NOI.sub("–", text or "")


def tach_cong_thuc(text: str) -> list[tuple[str, str]]:
    """Chia chuỗi thành các đoạn ('n' thường | 'sub' chỉ số dưới | 'sup' chỉ số trên).

    Chữ số đứng ngay sau chữ cái/ngoặc đóng là chỉ số dưới (CH3COOH → CH₃COOH); hệ số đầu chất (2CH4) giữ nguyên;
    'n' sau ngoặc đóng ((C6H10O5)n) cũng là chỉ số dưới.
    """
    t = text.translate(_SO_DUOI)
    out: list[tuple[str, str]] = []
    # điện tích ion: Mn2+, Fe3+, H+, OH- (dấu đứng cuối từ) → chỉ số trên.
    # Ion nhiều nguyên tố: chữ số ngay trước dấu là chỉ số dưới (NH4+ → NH₄⁺), trừ khi có ≥ 2 chữ số (SO42- → SO₄²⁻)
    dien_tich = {}
    for m in _DIEN_TICH.finditer(t):
        bat_dau, so = m.start(1), m.group(1)[:-1]
        tu = re.search(r"[A-Za-z0-9()\[\]]*$", t[: m.start(1)]).group()
        if so and sum(ch.isupper() for ch in tu) > 1:
            bat_dau += len(so) - (1 if len(so) >= 2 else 0)
        dien_tich[bat_dau] = m.end(1)
    i = 0
    while i < len(t):
        ch = t[i]
        prev = t[i - 1] if i else ""
        if i in dien_tich:
            out.append((t[i : dien_tich[i]], "sup"))
            i = dien_tich[i]
            continue
        if ch.isdigit() and prev and (prev.isalpha() and prev.isascii() or prev in ")]"):
            j = i
            while j < len(t) and t[j].isdigit() and (j == i or j not in dien_tich):
                j += 1
            out.append((t[i:j], "sub"))
            i = j
            continue
        if ch == "n" and prev == ")" and (i + 1 == len(t) or not t[i + 1].isalpha()):
            out.append(("n", "sub"))
            i += 1
            continue
        if out and out[-1][1] == "n":
            out[-1] = (out[-1][0] + ch, "n")
        else:
            out.append((ch, "n"))
        i += 1
    return out


class But:
    """Vẽ chữ giàu định dạng (chỉ số dưới, font dự phòng) lên ImageDraw."""

    def __init__(self, draw: ImageDraw.ImageDraw):
        self.d = draw

    @staticmethod
    def _phan(text: str, kieu: str):
        """Chia theo khả năng hiển thị của font chính; ký tự thiếu dùng Arial Unicode."""
        cur, cur_k = "", None
        for ch in text:
            k = kieu if _co_glyph(kieu, ch) else "du_phong"
            if k != cur_k and cur:
                yield cur, cur_k
                cur = ""
            cur, cur_k = cur + ch, k
        if cur:
            yield cur, cur_k

    def do_rong(self, text: str, co: int, kieu: str = "thuong", cong_thuc: bool = True) -> float:
        w = 0.0
        for doan, loai in (tach_cong_thuc(text) if cong_thuc else [(text, "n")]):
            c = int(co * 0.66) if loai in ("sub", "sup") else co
            for p, k in self._phan(doan, kieu):
                w += _font(k, c).getlength(p)
        return w

    def viet(self, xy, text: str, co: int, mau=MAU_CHU, kieu: str = "thuong", cong_thuc: bool = True) -> float:
        """Viết tại (x, đường chân chữ y). Trả về x sau khi viết."""
        x, y = xy
        for doan, loai in (tach_cong_thuc(text) if cong_thuc else [(text, "n")]):
            c = int(co * 0.66) if loai in ("sub", "sup") else co
            dy = int(co * 0.28) if loai == "sub" else (-int(co * 0.42) if loai == "sup" else 0)
            for p, k in self._phan(doan, kieu):
                f = _font(k, c)
                self.d.text((x, y + dy), p, font=f, fill=mau, anchor="ls")
                x += f.getlength(p)
        return x

    def gach_dong(self, text: str, co: int, rong_toi_da: float, kieu: str = "thuong", cong_thuc: bool = True) -> list[str]:
        dong: list[str] = []
        for doan in text.split("\n"):
            cur = ""
            for w in doan.split(" "):
                thu = f"{cur} {w}" if cur else w
                if cur and self.do_rong(thu, co, kieu, cong_thuc) > rong_toi_da:
                    dong.append(cur)
                    cur = w
                else:
                    cur = thu
            dong.append(cur)
        return dong


# ---------------------------------------------------------------- các thành phần
def _mui_ten(d: ImageDraw.ImageDraw, x1: float, x2: float, y: float, mau=MAU_CHU, day: int = 3, thuan_nghich: bool = False):
    dau = 16
    if not thuan_nghich:
        d.line([(x1, y), (x2 - 4, y)], fill=mau, width=day)
        d.polygon([(x2, y), (x2 - dau, y - dau * 0.45), (x2 - dau, y + dau * 0.45)], fill=mau)
        return
    k = 6  # hai nửa mũi tên ⇌
    d.line([(x1, y - k), (x2 - 2, y - k)], fill=mau, width=day - 1)
    d.polygon([(x2, y - k), (x2 - dau, y - k - dau * 0.45), (x2 - dau, y - k)], fill=mau)
    d.line([(x1 + 2, y + k), (x2, y + k)], fill=mau, width=day - 1)
    d.polygon([(x1, y + k), (x1 + dau, y + k + dau * 0.45), (x1 + dau, y + k)], fill=mau)


def _tieu_de_muc(d, but: But, y: int, text: str) -> int:
    d.rounded_rectangle([LE, y + 6, LE + 8, y + 40], radius=4, fill=MAU_NHAN)
    but.viet((LE + 22, y + 36), text, 32, MAU_CHU, "dam", cong_thuc=False)
    return y + 62


def _nhan_nut(nut) -> tuple[str, str]:
    """(dòng công thức, dòng tên) dưới mỗi chất trong sơ đồ."""
    ct = dep_cong_thuc((nut.cong_thuc or nut.ki_hieu or "").strip())
    kh = (nut.ki_hieu or "").strip()
    la_an = bool(kh) and len(kh) <= 3 and not any(ch.isdigit() for ch in kh) and kh != ct
    dong1 = f"{kh} = {ct}" if la_an and ct else (ct or kh)
    return dong1, (nut.ten or "").strip()


def _hinh_chat(smiles: str, cong_thuc: str, da_ve: dict, mau=None):
    """(ảnh, mol) của một chất; vẽ theo hướng chất mẫu (chất đứng trước trong chuỗi), lưu lại để bảng chất ẩn dùng đúng hình đó."""
    if smiles in da_ve:
        return da_ve[smiles]
    mol = chuan_bi_mol(smiles, cong_thuc=cong_thuc, mau=mau) if (smiles or "").strip() else None
    if mol is not None and mol.GetNumAtoms() == 0:  # SMILES rỗng / chất giữ chỗ – không vẽ hình, chỉ ghi kí hiệu
        mol = None
    kq = (ve_mol(mol), mol) if mol is not None else (None, None)
    da_ve[smiles] = kq
    return kq


def _ve_so_do(img: Image.Image, d, but: But, y0: int, sol, da_ve: dict | None = None) -> int:
    """Vẽ sơ đồ chuỗi; trả về y kết thúc."""
    CO_DK, CO_SO, CO_CT, CO_TEN = 22, 24, 26, 21
    y = y0
    da_ve = {} if da_ve is None else da_ve
    for doan in doan_so_do(sol):
        mau = None  # chất vừa vẽ ngay trước – chất sau lật theo khung chung với nó
        muc: list[dict] = []
        for loai, obj in [("nut", doan["dau"])] + [x for b, s in doan["buoc"] for x in (("ten", b), ("nut", s))]:
            if loai == "nut":
                hinh, mol = _hinh_chat(obj.smiles, obj.cong_thuc, da_ve, mau)
                mau = mol if mol is not None else mau
                d1, d2 = _nhan_nut(obj)
                rong = max(hinh.width if hinh else 0, but.do_rong(d1, CO_CT, "dam") + 16, but.do_rong(d2, CO_TEN, "nghieng", False) + 16, 90)
                muc.append({"loai": "nut", "hinh": hinh, "d1": d1, "d2": d2, "rong": rong, "cao": hinh.height if hinh else 40})
            else:
                dk = (obj.dieu_kien or "").strip()
                if not (obj.phuong_trinh or "").strip() and (obj.loai_phan_ung or "").strip():  # dạng chuỗi: tên phản ứng trên mũi tên
                    dk = f"{obj.loai_phan_ung.strip()}; {dk}" if dk else obj.loai_phan_ung.strip()
                tu = [w for w in dk.split()]
                dai_tu = max((but.do_rong(w, CO_DK) for w in tu), default=0)
                rong = min(max(170, dai_tu + 40, min(but.do_rong(dk, CO_DK) + 40, 300)), 320)
                dong = but.gach_dong(dk, CO_DK, rong - 24) if dk else []
                muc.append({"loai": "ten", "buoc": obj, "rong": rong, "dong": dong})

        # chất quá rộng thì thu nhỏ để một hàng luôn chứa được "mũi tên + chất"
        rong_toi_da = NOI_DUNG - 2 * (170 + 24)
        for m in muc:
            if m["loai"] == "nut" and m["hinh"] is not None and m["hinh"].width > rong_toi_da:
                k = rong_toi_da / m["hinh"].width
                m["hinh"] = m["hinh"].resize((int(m["hinh"].width * k), int(m["hinh"].height * k)), Image.LANCZOS)
                m["rong"], m["cao"] = max(m["hinh"].width, m["rong"] * k), m["hinh"].height

        # bố cục "rắn lượn" (như sơ đồ tổng hợp trong bài báo): hàng đầu trái → phải; hết chỗ thì mũi tên DỌC đi xuống
        # dưới chất cuối hàng, hàng sau đi phải → trái, cứ thế đổi chiều. Mỗi hàng: chất – mũi tên – chất ...
        GAP = 24
        trai, phai = LE, LE + NOI_DUNG
        hang: list[dict] = []          # {"chieu": ±1, "muc": [(mục, x trái)]}
        doc: list[dict] = []           # mục "ten" thành mũi tên dọc nối hàng r với hàng r+1
        chieu = 1
        h = {"chieu": 1, "muc": [(muc[0], float(trai))]}
        for j in range(1, len(muc) - 1, 2):
            ten, nut = muc[j], muc[j + 1]
            m_cuoi, x_cuoi = h["muc"][-1]
            if chieu == 1:
                x_ten = x_cuoi + m_cuoi["rong"] + GAP
                x_nut = x_ten + ten["rong"] + GAP
                vua = x_nut + nut["rong"] <= phai
            else:
                x_ten = x_cuoi - GAP - ten["rong"]
                x_nut = x_ten - GAP - nut["rong"]
                vua = x_nut >= trai
            if vua:
                h["muc"] += [(ten, x_ten), (nut, x_nut)]
                continue
            hang.append(h)
            doc.append(ten)
            chieu = -chieu
            tam = x_cuoi + m_cuoi["rong"] / 2  # chất đầu hàng mới nằm ngay dưới chất cuối hàng trên
            h = {"chieu": chieu, "muc": [(nut, min(max(tam - nut["rong"] / 2, trai), phai - nut["rong"]))]}
        hang.append(h)

        def day_nhan(m, yc):  # đáy nhãn dưới hình của một chất
            return yc + m["cao"] / 2 + 34 + (30 if m["d2"] else 0) + 12

        day_hang_truoc = None
        y_mui_ten_doc = None
        for r, h in enumerate(hang):
            nuts = [m for m, _ in h["muc"] if m["loai"] == "nut"]
            tens = [m for m, _ in h["muc"] if m["loai"] == "ten"]
            cao_hinh = max([m["cao"] for m in nuts] or [60])
            so_dong_dk = max([len(m["dong"]) for m in tens] or [0])
            cao_chu_tren = (34 + so_dong_dk * 28) if tens else 0  # "(n)" + các dòng điều kiện
            tren = max(cao_hinh / 2, cao_chu_tren + 14)
            if r == 0:
                yc = y + tren
            else:
                ten = doc[r - 1]
                dong = [f"({ten['buoc'].so})"] + ten["dong"]
                dai = max(130, 28 * len(dong) + 40)
                nut0, x0 = h["muc"][0]
                yc = max(y_mui_ten_doc + dai + 12 + nut0["cao"] / 2, day_hang_truoc + 24 + tren)
                y_b = yc - nut0["cao"] / 2 - 12
                xm = x0 + nut0["rong"] / 2
                mau = MAU_CHU if not ten["buoc"].can_kiem_tra else MAU_DO
                d.line([(xm, y_mui_ten_doc), (xm, y_b - 4)], fill=mau, width=3)
                d.polygon([(xm, y_b), (xm - 7, y_b - 16), (xm + 7, y_b - 16)], fill=mau)
                # chữ cạnh mũi tên dọc: bên phải nếu đủ chỗ, không thì bên trái
                rong_chu = max([but.do_rong(dong[0], CO_SO, "dam", False)] + [but.do_rong(x, CO_DK) for x in dong[1:]])
                ben_phai = xm + 18 + rong_chu <= RONG - 20
                yy = (y_mui_ten_doc + y_b) / 2 - 28 * (len(dong) - 1) / 2 + 8
                for k, dg in enumerate(dong):
                    dam = k == 0
                    w = but.do_rong(dg, CO_SO, "dam", False) if dam else but.do_rong(dg, CO_DK)
                    xx = xm + 18 if ben_phai else xm - 18 - w
                    if dam:
                        but.viet((xx, yy), dg, CO_SO, MAU_NHAN, "dam", False)
                    else:
                        but.viet((xx, yy), dg, CO_DK, MAU_LAM)
                    yy += 28
            for m, x in h["muc"]:
                if m["loai"] == "nut":
                    if m["hinh"] is not None:
                        img.paste(m["hinh"], (int(x + (m["rong"] - m["hinh"].width) / 2), int(yc - m["hinh"].height / 2)))
                    ychu = yc + m["cao"] / 2 + 34  # nhãn ngay dưới hình của chính chất đó
                    w1 = but.do_rong(m["d1"], CO_CT, "dam")
                    but.viet((x + (m["rong"] - w1) / 2, ychu), m["d1"], CO_CT, MAU_CHU, "dam")
                    if m["d2"]:
                        w2 = but.do_rong(m["d2"], CO_TEN, "nghieng", False)
                        but.viet((x + (m["rong"] - w2) / 2, ychu + 30), m["d2"], CO_TEN, MAU_PHU, "nghieng", False)
                else:
                    b = m["buoc"]
                    mau = MAU_CHU if not b.can_kiem_tra else MAU_DO
                    if h["chieu"] == 1:
                        _mui_ten(d, x + 4, x + m["rong"] - 4, yc, mau)
                    else:  # hàng phải → trái: mũi tên chỉ sang trái
                        x1, x2 = x + m["rong"] - 4, x + 4
                        d.line([(x1, yc), (x2 + 4, yc)], fill=mau, width=3)
                        d.polygon([(x2, yc), (x2 + 16, yc - 7.2), (x2 + 16, yc + 7.2)], fill=mau)
                    yy = yc - 12
                    for dong in reversed(m["dong"]):
                        wd = but.do_rong(dong, CO_DK)
                        but.viet((x + (m["rong"] - wd) / 2, yy), dong, CO_DK, MAU_LAM)
                        yy -= 28
                    so = f"({b.so})"
                    ws = but.do_rong(so, CO_SO, "dam", False)
                    but.viet((x + (m["rong"] - ws) / 2, yy - 2), so, CO_SO, MAU_NHAN, "dam", False)
            m_cuoi = h["muc"][-1][0]
            y_mui_ten_doc = day_nhan(m_cuoi, yc) + 6
            day_hang_truoc = max(day_nhan(m, yc) for m in nuts)
            y = int(day_hang_truoc + 26)
        y += 10
    return y


def _ve_phuong_trinh(d, but: But, y: int, sol) -> int:
    CO = 30
    for b in sorted(sol.cac_buoc, key=lambda b: b.so):
        pt = dep_cong_thuc((b.phuong_trinh or "").strip())
        m = re.split(r"\s*(→|⇌|⟶|->|=>|⇄)\s*", pt, maxsplit=1)
        x = LE + 10
        so = f"({b.so})"
        x = but.viet((x, y), so, CO, MAU_NHAN, "dam", False) + 18
        if len(m) == 3:
            trai, mt, phai = m
            x = but.viet((x, y), trai, CO) + 16
            dk = (b.dieu_kien or "").strip()
            co_dk = 21
            dai = max(90, but.do_rong(dk, co_dk) + 30) if dk else 90
            _mui_ten(d, x, x + dai, y - 10, MAU_CHU, 3, thuan_nghich=mt in ("⇌", "⇄"))
            if dk:
                but.viet((x + (dai - but.do_rong(dk, co_dk)) / 2, y - 26), dk, co_dk, MAU_LAM)
            x = but.viet((x + dai + 16, y), phai, CO)
        else:
            x = but.viet((x, y), pt, CO)
        if b.loai_phan_ung:
            x = but.viet((x + 24, y), f"[{b.loai_phan_ung}]", 22, MAU_PHU, "nghieng", False)
        if b.can_kiem_tra:
            but.viet((x + 20, y), f"⚠ {CAN_KIEM_TRA}", 22, MAU_DO, "dam", False)
        y += 64
    return y


def _ve_chat_an(img, d, but: But, y: int, sol, da_ve: dict | None = None) -> int:
    cot = [("Kí hiệu", 180), ("Công thức", 380), ("Tên", 520), ("Cấu tạo", NOI_DUNG - 1080)]
    x = LE
    d.rectangle([LE, y, LE + NOI_DUNG, y + 52], fill=MAU_NEN_NHAT)
    for ten, w in cot:
        but.viet((x + 18, y + 35), ten, 24, MAU_CHU, "dam", False)
        x += w
    y += 52
    for c in sol.chat_an:
        # bảng chỉ để tra cứu nhanh: vẽ lại cùng hình (cùng hướng) với sơ đồ nhưng nhỏ hơn
        _, mol = _hinh_chat(c.smiles, c.cong_thuc, {} if da_ve is None else da_ve)
        hinh = ve_mol(mol, BOND_PX_BANG) if mol is not None else None
        if hinh is not None and hinh.height > 130:
            hinh = hinh.resize((int(hinh.width * 130 / hinh.height), 130), Image.LANCZOS)
        cao = max(70, (hinh.height + 20) if hinh else 70)
        yc = y + cao / 2 + 10
        x = LE
        but.viet((x + 18, yc), c.ki_hieu, 28, MAU_NHAN, "dam")
        x += cot[0][1]
        dong_ct = but.gach_dong(dep_cong_thuc(c.cong_thuc), 26, cot[1][1] - 30)
        for dong_i, dong in enumerate(dong_ct):
            but.viet((x + 18, yc + dong_i * 32), dong, 26)
        if c.ctpt and c.ctpt != re.sub(r"[\s=≡–—\-]", "", c.cong_thuc or ""):
            but.viet((x + 18, yc + len(dong_ct) * 32 + 2), f"CTPT: {c.ctpt}", 21, MAU_PHU)
        x += cot[1][1]
        for dong_i, dong in enumerate(but.gach_dong(c.ten or "", 24, cot[2][1] - 30, cong_thuc=False)):
            but.viet((x + 18, yc + dong_i * 30), dong, 24, MAU_CHU, "thuong", False)
        x += cot[2][1]
        if hinh is not None:
            img.paste(hinh, (int(x + 18), int(y + 10)))
        y += cao
        d.line([(LE, y), (LE + NOI_DUNG, y)], fill=MAU_VIEN, width=2)
    return y + 20


def tao_anh_dap_an(sol, de_bai: str, out: Path, chan_trang: str = "Lời giải do AI tạo – vui lòng kiểm tra lại",
                   mo_hinh: str = "") -> Path:
    img = Image.new("RGB", (RONG, 12000), "white")
    d = ImageDraw.Draw(img)
    but = But(d)

    # Đầu trang: dải màu xanh lá → xanh lam
    for i in range(RONG):
        t = i / (RONG - 1)
        mau = tuple(int(MAU_NHAN[k] * (1 - t) + MAU_LAM_SANG[k] * t) for k in range(3))
        d.line([(i, 0), (i, 12)], fill=mau)
    y = 92
    but.viet((LE, y), "ĐÁP ÁN – CHUỖI PHẢN ỨNG HỮU CƠ", 50, MAU_CHU, "dam", False)
    ngay = dt.datetime.now().strftime("%d/%m/%Y")
    nhan = f"CHEMISTRY · {ngay}"
    but.viet((RONG - LE - but.do_rong(nhan, 22, "thuong", False), y - 4), nhan, 22, MAU_PHU, "thuong", False)
    y += 40

    co_loi = [b.so for b in sol.cac_buoc if b.can_kiem_tra]
    if co_loi:
        thong_bao = f"⚠ Bước {', '.join(map(str, co_loi))} chưa qua kiểm tra tự động (cân bằng/SMILES) – {CAN_KIEM_TRA}."
        d.rounded_rectangle([LE, y, LE + NOI_DUNG, y + 56], radius=10, fill=(254, 236, 238))
        but.viet((LE + 20, y + 38), thong_bao, 24, MAU_DO, "dam", False)
        y += 76

    # Đề bài
    y = _tieu_de_muc(d, but, y, "Đề bài")
    dong = but.gach_dong(de_bai.strip(), 28, NOI_DUNG - 60)
    cao = 30 + len(dong) * 42
    d.rounded_rectangle([LE, y, LE + NOI_DUNG, y + cao], radius=14, fill=MAU_NEN_NHAT)
    for i, dg in enumerate(dong):
        but.viet((LE + 30, y + 52 + i * 42), dg, 28)
    y += cao + 40

    # Sơ đồ chuỗi
    y = _tieu_de_muc(d, but, y, "Sơ đồ chuỗi phản ứng")
    da_ve: dict = {}  # smiles → (ảnh, mol): sơ đồ và bảng chất ẩn dùng chung một hình
    y = _ve_so_do(img, d, but, y + 10, sol, da_ve)

    # Phương trình (kết quả dạng chuỗi không có phương trình – chỉ sinh khi người dùng bấm "Xem phương trình chi tiết")
    if any((b.phuong_trinh or "").strip() for b in sol.cac_buoc):
        y = _tieu_de_muc(d, but, y + 10, "Phương trình phản ứng")
        y = _ve_phuong_trinh(d, but, y + 36, sol) + 10

    for tieu_de, dong_viet in (("Lưu ý", [f"• {x.strip()}" for x in getattr(sol, "luu_y", []) if x and x.strip()]),
                               ("Đáp án", [sol.dap_an.strip()] if getattr(sol, "dap_an", "").strip() else [])):
        if dong_viet:
            y = _tieu_de_muc(d, but, y + 10, tieu_de)
            for dong in dong_viet:
                for dg in but.gach_dong(dong, 26, NOI_DUNG - 20):
                    but.viet((LE + 10, y + 26), dg, 26, MAU_CHU)
                    y += 38
            y += 20

    if sol.chat_an:
        y = _tieu_de_muc(d, but, y + 10, "Các chất ẩn")
        y = _ve_chat_an(img, d, but, y, sol, da_ve)

    if (sol.ghi_chu or "").strip():
        y = _tieu_de_muc(d, but, y + 10, "Ghi chú")
        for dg in but.gach_dong(sol.ghi_chu.strip(), 26, NOI_DUNG - 20):
            but.viet((LE + 10, y + 26), dg, 26, MAU_CHU)
            y += 38
        y += 20

    # Chân trang
    y += 20
    d.line([(LE, y), (LE + NOI_DUNG, y)], fill=MAU_VIEN, width=2)
    y += 44
    nguon = [n.strip() for n in sol.nguon_tham_khao if n and n.strip()]
    if nguon:
        for dg in but.gach_dong("Nguồn tham khảo: " + "  ·  ".join(nguon), 21, NOI_DUNG, cong_thuc=False):
            but.viet((LE, y), dg, 21, MAU_PHU, "thuong", False)
            y += 32
    chu = chan_trang + (f"  ({mo_hinh})" if mo_hinh else "")
    but.viet((LE, y + 8), chu, 24, MAU_NHAN, "nghieng", False)
    y += 60

    img.crop((0, 0, RONG, y)).save(out, "PNG", optimize=True)
    return out


def tao_zip(out: Path, files: list[Path]) -> Path:
    with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
        for f in files:
            if f.exists():
                z.write(f, f.relative_to(out.parent) if f.is_relative_to(out.parent) else f.name)
    return out
