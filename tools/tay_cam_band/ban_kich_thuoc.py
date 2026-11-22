"""File kích thước VERA Band: hình chiếu 1:1 từng phần, bảng kích thước, đối chiếu với cỡ tay người Việt trưởng thành.

Chạy sau mo_hinh_band.py:  python tools/tay_cam_band/ban_kich_thuoc.py
Ra: docs/tay-cam-band/VERA_Band_kich_thuoc.html và .pdf (in bằng Edge chạy ẩn)
In PDF đúng 100% thì hình 1:1 bằng kích thước thật, có thước 50 mm để kiểm.
"""
import html
import math
import os
import subprocess
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mo_hinh_band as mh  # noqa: E402

EDGE = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
R_CANH = [[1, 0, 0], [0, 0, 1], [0, -1, 0]]     # nhìn từ sườn phải (phía mu bàn tay): x sang phải, z lên
R_TRUOC = [[0, 1, 0], [0, 0, 1], [1, 0, 0]]     # nhìn từ trước: y sang phải, z lên
R_TREN = [[1, 0, 0], [0, 1, 0], [0, 0, 1]]      # nhìn từ trên: x sang phải, y lên
R_MAT_NHIN = mh.R_MAT.T                          # nhìn thẳng vào mặt joystick

# số đo bàn tay người Việt trưởng thành, mm: (trung bình, độ lệch chuẩn)
NAM = {"dai": (177, 12), "rong": (79.2, 6.9)}            # Imrhan, Nguyen & Nguyen (1993), nam
NU = {"dai": (165, 7.48), "rong_gan": (75, 3.99), "day": (36.2, 2.44),
      "R3": (16.54, 1.26), "R4": (15.5, 1.19), "R5": (13.8, 1.16)}   # Bùi Văn Huấn, Đỗ Thị Hoa Ngà (2020), 270 nữ sinh viên
TI_LE_NAM_NU = NAM["rong"][0] / NU["rong_gan"][0]       # ước tính cỡ tay nam từ tay nữ khi thiếu số đo nam
Z = 1.645                                               # P5, P95 theo phân bố chuẩn


def so(v, nd=1):
    """Số kiểu Việt Nam: dấu phẩy thập phân, bỏ số 0 thừa."""
    t = f"{v:.{nd}f}".rstrip("0").rstrip(".") if nd else f"{v:.0f}"
    return t.replace(".", ",")


def p5p95(mt):
    m, s = mt
    return m - Z * s, m + Z * s


# ---------------- đo trên mô hình ----------------

def do_mo_hinh():
    ct = {ma: m for ma, _, _, m, _, _, _ in mh.chi_tiet_in()}
    z_giua = (mh.Z_DAY + mh.Z_CUA[0]) / 2
    cs = mh.khoi().slice(z_giua)
    vien = max(cs.to_polygons(), key=len)
    p = np.asarray(vien)
    chu_vi = float(np.sum(np.linalg.norm(np.roll(p, -1, axis=0) - p, axis=1)))
    b = cs.bounds()
    return ct, {"z_giua": z_giua, "chu_vi": chu_vi, "sau": b[2] - b[0], "rong": b[3] - b[1]}


def do_dai_quai():
    """Độ dài đường giữa của quai trên mô hình (bàn tay mô phỏng dày bằng tay nữ trung bình)."""
    p = np.array(mh.duong_quai())
    return float(np.sum(np.linalg.norm(p[1:] - p[:-1], axis=1))), mh.quai()


# ---------------- vẽ ----------------

def chieu(m, R):
    cs = m.transform(mh.bien_doi(np.array(R, float), [0, 0, 0])).project().simplify(0.02)
    return cs.to_polygons(), cs.bounds()


class Hinh:
    """Một hình chiếu vẽ theo mm, tỉ lệ `k` (1 là 1:1)."""

    def __init__(self, m, R, k=1.0, ten=""):
        self.polys, b = chieu(m, R)
        self.b = b
        self.k = k
        self.ten = ten
        self.them = []

    def ngang(self, u0, u1, v, nhan):
        self.them.append(("h", u0, u1, v, nhan))
        return self

    def doc(self, v0, v1, u, nhan):
        self.them.append(("v", v0, v1, u, nhan))
        return self

    def svg(self):
        k = self.k
        u0, v0, u1, v1 = self.b
        so_doc = 1 + sum(1 for t in self.them if t[0] == "v")
        so_ngang = 1 + sum(1 for t in self.them if t[0] == "h")
        trai, phai, tren, duoi = 2, 4 + 6 * so_doc, 7, 3 + 6 * so_ngang
        w = (u1 - u0) * k + trai + phai
        h = (v1 - v0) * k + tren + duoi
        X = lambda u: trai + (u - u0) * k
        Y = lambda v: tren + (v1 - v) * k
        d = " ".join("M" + " L".join(f"{X(x):.2f},{Y(y):.2f}" for x, y in p) + "Z" for p in self.polys)
        out = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{w:.1f}mm" height="{h:.1f}mm" viewBox="0 0 {w:.2f} {h:.2f}">',
               f'<text x="{trai}" y="4.5" class="tv">{html.escape(self.ten)}{"" if k == 1 else f" (tỉ lệ {so(k)}:1)"}</text>',
               f'<path d="{d}" class="ve"/>']
        # kích thước bao ngoài
        dong_ngang = [("h", u0, u1, None, so(u1 - u0))] + [t for t in self.them if t[0] == "h"]
        dong_doc = [("v", v0, v1, None, so(v1 - v0))] + [t for t in self.them if t[0] == "v"]
        for i, (_, a, b2, _v, nhan) in enumerate(dong_ngang):
            y = Y(v0) + 4 + 6 * i
            out.append(self._duong(X(a), y, X(b2), y, nhan, ngang=True, goc=Y(v0)))
        for i, (_, a, b2, _u, nhan) in enumerate(dong_doc):
            x = X(u1) + 4 + 6 * i
            out.append(self._duong(x, Y(a), x, Y(b2), nhan, ngang=False, goc=X(u1)))
        out.append("</svg>")
        return "".join(out)

    @staticmethod
    def _duong(x0, y0, x1, y1, nhan, ngang, goc):
        s = [f'<line x1="{x0:.2f}" y1="{y0:.2f}" x2="{x1:.2f}" y2="{y1:.2f}" class="kt"/>']
        if ngang:
            s += [f'<line x1="{x:.2f}" y1="{goc:.2f}" x2="{x:.2f}" y2="{y0 + 1.5:.2f}" class="gd"/>' for x in (x0, x1)]
            s += [f'<path d="M{x - 0.8:.2f},{y0 + 0.8:.2f} L{x + 0.8:.2f},{y0 - 0.8:.2f}" class="kt"/>' for x in (x0, x1)]
            s.append(f'<text x="{(x0 + x1) / 2:.2f}" y="{y0 - 1:.2f}" class="so" text-anchor="middle">{nhan}</text>')
        else:
            s += [f'<line x1="{goc:.2f}" y1="{y:.2f}" x2="{x0 + 1.5:.2f}" y2="{y:.2f}" class="gd"/>' for y in (y0, y1)]
            s += [f'<path d="M{x0 - 0.8:.2f},{y + 0.8:.2f} L{x0 + 0.8:.2f},{y - 0.8:.2f}" class="kt"/>' for y in (y0, y1)]
            ym = (y0 + y1) / 2
            s.append(f'<text x="{x0 - 1:.2f}" y="{ym:.2f}" class="so" text-anchor="middle" transform="rotate(-90 {x0 - 1:.2f} {ym:.2f})">{nhan}</text>')
        return "".join(s)


def bang(head, rows, cls=""):
    th = "".join(f"<th>{h}</th>" for h in head)
    tr = "".join("<tr>" + "".join(f"<td>{c}</td>" for c in r) + "</tr>" for r in rows)
    return f'<table class="{cls}"><thead><tr>{th}</tr></thead><tbody>{tr}</tbody></table>'


def trang(tieu_de, *noi_dung):
    return f'<section><h2>{tieu_de}</h2>{"".join(noi_dung)}</section>'


def hang(*svgs):
    return '<div class="hang">' + "".join(svgs) + "</div>"


# ---------------- nội dung ----------------

def main():
    sys.stdout.reconfigure(encoding="utf-8")
    ct, do = do_mo_hinh()
    than = ct["than_phai"] + ct["than_trai"]
    ca_bo = than + ct["nap"] + ct["chen"] + ct["co"] + ct["tam_mu_tay"] + ct["nap_pin"] + ct["nut_phu"]
    l_quai, quai = do_dai_quai()
    ca_bo_quai = ca_bo + quai

    # đối chiếu cỡ tay
    d_tuong = do["chu_vi"] / math.pi
    nu_dai, nam_dai = p5p95(NU["dai"]), (157.0, 197.0)
    tot = lambda L: 0.197 * L
    ba_ngon_nu = sum(NU[k][0] for k in ("R3", "R4", "R5"))
    ba_ngon_nu_95 = sum(NU[k][0] + Z * NU[k][1] for k in ("R3", "R4", "R5"))
    ba_ngon_nam, ba_ngon_nam_95 = ba_ngon_nu * TI_LE_NAM_NU, ba_ngon_nu_95 * TI_LE_NAM_NU
    vung_nam = mh.Z_CUA[0] - mh.Z_DAY
    day_nu = p5p95(NU["day"])
    day_nam_95 = (NU["day"][0] + Z * NU["day"][1]) * TI_LE_NAM_NU
    day_mo_hinh = NU["day"][0]
    quai_ngan = l_quai + 2 * (day_nu[0] - day_mo_hinh)
    quai_dai = l_quai + 2 * (day_nam_95 - day_mo_hinh)
    gap_gai, may_tren = 50, 25
    cat_quai = math.ceil((quai_dai + gap_gai + may_tren) / 10) * 10
    tam_cao = mh.TAM_Z[1] - mh.TAM_Z[0]
    rong_gan_nu5 = p5p95(NU["rong_gan"])[0]
    so_ngon_thua = lambda c: max(0.0, c - vung_nam)

    rows_tay = [
        ["Đường kính cán", f"Cán eo {so(do['sau'])} × {so(do['rong'])} mm, chu vi {so(do['chu_vi'])} mm, tương đương Ø{so(d_tuong)}",
         f"Tối ưu = 19,7% chiều dài bàn tay (Kong & Lowe 2005): Ø{so(tot(nu_dai[0]))} (nữ P5) · Ø{so(tot(NU['dai'][0]))} (nữ TB) · Ø{so(tot(NAM['dai'][0]))} (nam TB) · Ø{so(tot(nam_dai[1]))} (nam P95)",
         "Nằm giữa dải: hợp tay trung bình, tay nhỏ thấy hơi to"],
        ["Chiều dài vùng nắm cho 3 ngón giữa, áp út, út", f"{so(vung_nam)} mm (từ đáy cán tới mép dưới ô cò)",
         f"Tổng bề rộng 3 ngón: nữ TB {so(ba_ngon_nu)}, nữ P95 {so(ba_ngon_nu_95)}, nam TB ≈ {so(ba_ngon_nam)}, nam P95 ≈ {so(ba_ngon_nam_95)}",
         f"Vừa với nữ và nam trung bình. Tay nữ P95 thừa {so(so_ngon_thua(ba_ngon_nu_95))} mm, tay nam P95 thừa ≈ {so(so_ngon_thua(ba_ngon_nam_95))} mm: ngón út tì lên đế phình"],
        ["Quai qua mu bàn tay", f"Đường quai trên mô hình {so(l_quai, 0)} mm (tay dày {so(day_mo_hinh)} mm)",
         f"Độ dày bàn tay: nữ P5 {so(day_nu[0])}, nữ P95 {so(day_nu[1])}, nam P95 ≈ {so(day_nam_95)}",
         f"Quai cần dài {so(quai_ngan, 0)}–{so(quai_dai, 0)} mm. Cắt {cat_quai} mm = phần ôm tay + {gap_gai} mm gập dán + {may_tren} mm khâu ở khuyên trên"],
        ["Tấm mu tay", f"{so(mh.TAM_X[1] - mh.TAM_X[0])} × {so(tam_cao)} mm, cong R{so(mh.TAM_R)}",
         f"Bề rộng gan bàn tay nữ P5 {so(rong_gan_nu5)} mm, nam TB {so(NAM['rong'][0])} mm",
         f"Tấm chỉ phủ {so(100 * tam_cao / rong_gan_nu5, 0)}% bề rộng tay nhỏ nhất, không cấn mép bàn tay"],
        ["Khuyên trên ↔ ngón cái", "Khe 0,42 mm với ngón cái mô phỏng", "Mô hình bàn tay chỉ gần đúng",
         "Cần in thử: tay to có thể chạm khuyên ở gốc ngón cái"],
    ]

    tong = sum(c["gam_pla"] for c in mh_goi())
    rows_file = [[f"<b>{c['file']}.stl</b>", c["ten"], c["mau"], " × ".join(so(v) for v in c["kich_thuoc"]),
                  ", ".join(f"{mh_ten(x['ma'])}: {x['cach_in']}" for x in c["chi_tiet"])] for c in mh_goi()]

    # trang 1: tổng thể
    t1 = trang("VERA Band · tổng thể",
               '<p class="ghi">Mọi số đo tính bằng mm. In file này đúng 100% (không “vừa trang”) thì hình 1:1 bằng kích thước thật: đo thước 50 mm bên dưới để kiểm rồi có thể đặt tay lên hình để thử cỡ.</p>',
               '<svg width="60mm" height="8mm" viewBox="0 0 60 8"><rect x="0" y="2" width="50" height="2.5" class="thuoc"/><text x="52" y="5" class="so">50 mm</text></svg>',
               hang(Hinh(ca_bo, R_CANH, ten="Nhìn từ phía mu bàn tay (không vẽ quai)").doc(mh.Z_DAY, mh.Z_CUA[0], mh.TAM_X[0] - 2, so(vung_nam)).svg(),
                    Hinh(ca_bo, R_TRUOC, ten="Nhìn từ trước").svg()),
               bang(["File gửi in", "Chi tiết", "Màu nhựa", "Khổ trên bàn in", "Hướng in"], rows_file),
               f'<p class="ghi">Tổng nhựa ≈ {so(tong, 0)} g PLA (chưa tính vật đỡ). Gợi ý: PLA hoặc PETG, lớp 0,16 mm, 3 lớp vách, độ đặc 20%. Mặt ghép của 2 nửa thân phải phẳng. Tấm mu tay in dựng đứng, nên có brim.</p>')
    t2 = trang("Đối chiếu với cỡ tay người Việt trưởng thành",
               bang(["Hạng mục", "Tay cầm", "Số đo bàn tay", "Kết luận"], rows_tay, "rong"),
               '<p class="ghi">Nguồn: tay nam theo Imrhan, Nguyen &amp; Nguyen (1993), trích trong Zheng và cs., AHFE 2018; tay nữ theo Bùi Văn Huấn, Đỗ Thị Hoa Ngà, Tạp chí Khoa học và Công nghệ 140 (2020) 25–30, đo 270 nữ sinh viên Hưng Yên. '
               'P5, P95 tính bằng trung bình ∓ 1,645 độ lệch chuẩn. Chưa có số đo độ dày và bề rộng ngón tay của nam người Việt, nên số có dấu ≈ là ước tính từ tay nữ theo tỉ lệ bề rộng bàn tay nam/nữ.</p>',
               hang(Hinh(ca_bo, R_TREN, ten="Nhìn từ trên (không vẽ quai)").svg()))

    # trang thân
    k = mh
    rows_than = [
        ["Bề dày vỏ", so(k.VACH)],
        ["Lỗ lọt pin ở đáy cán", f"{so(2 * k.BAY_X)} × {so(2 * k.BAY_Y)} (pin vuông 9V 48,5 × 26,5 × 17,5)"],
        ["Ray nắp pin", "rộng 20 ở đáy → 22,6 ở trên, dày 2, khe hở 0,2"],
        ["Ô cửa cò", f"cao {so(k.Z_CUA[1] - k.Z_CUA[0])}, rộng {so(2 * (k.Y_CO + 0.5))}; lỗ trục cò Ø2,2"],
        ["Khuyên trên (quai)", f"lòng rộng {so(k.KHUYEN_X[1] - k.KHUYEN_X[0])}, thanh ngang cao {so(k.KHUYEN_Z[1] - k.KHUYEN_Z[0])}, khe ≥ 4,4"],
        ["Khe dưới (quai)", f"{so(k.KHE_X[1] - k.KHE_X[0])} × {so(k.KHE_Y[1] - k.KHE_Y[0])}, xuyên dọc đế phình"],
        ["7 lỗ vít ghép", "nửa phải: lỗ mồi Ø1,7; nửa trái: lỗ qua Ø2,2, hốc đầu vít Ø4,2"],
        ["2 lỗ vít giữ đáy trên", "lỗ qua Ø2,3, hốc đầu vít Ø4,2, ở lưng đầu"],
        ["Cổng P3, P4", "lỗ Ø7, ô giữ nút 6,3 × 6,3"],
        ["Cổng micro-USB", f"{so(13.0)} × {so(7.5)} ở lưng đầu"],
        ["Khe công tắc", "2,6 × 5,6 ở bụng đầu"],
    ]
    h_than = Hinh(than, R_CANH, ten="Thân (2 nửa ghép lại), nhìn từ phía mu bàn tay")
    h_than.doc(k.Z_DAY, k.Z_CUA[0], None, so(vung_nam)).doc(k.Z_CUA[0], k.Z_CUA[1], None, so(k.Z_CUA[1] - k.Z_CUA[0]))
    t3 = trang("Thân", hang(h_than.svg(), Hinh(than, R_TRUOC, ten="Nhìn từ trước").svg()),
               bang(["Chi tiết", "Kích thước"], rows_than))

    # đáy trên, đáy dưới
    rows_tren = [["Bề dày, độ lún", f"dày {so(k.DAY_MAT)}, lún {so(k.LUN_MAT)} so với vành thân"],
                 ["Lỗ chén lục giác", f"bán kính đỉnh {so(k.R_CHEN + 0.2)}"],
                 ["4 trụ đỡ KY-023", "Ø3,4, đặt cách nhau 30 × 22"],
                 ["Cổng P1, P2", f"lỗ Ø7 cách tâm cần {so(k.T_CONG + 5)} về phía trước, lệch ±{so(k.Y_CONG)}"],
                 ["2 lỗ vít ở tai sau", "lỗ mồi Ø1,9"]]
    t4 = trang("Đáy trên và đáy dưới",
               hang(Hinh(ct["nap"], R_MAT_NHIN, ten="Đáy trên, nhìn thẳng mặt joystick").svg(), Hinh(ct["nap"], R_CANH, ten="Đáy trên, nhìn bên").svg()),
               bang(["Chi tiết", "Kích thước"], rows_tren),
               hang(Hinh(ct["nap_pin"], R_TREN, 2.0, ten="Đáy dưới (nắp pin), nhìn từ dưới lên").svg(), Hinh(ct["nap_pin"], R_TRUOC, 2.0, ten="Đáy dưới, nhìn từ trước").svg()),
               '<p class="ghi">Đáy dưới: ray thang rộng 20 → 22,6, dày 2; 2 gai khóa Ø2 nhô 0,45; 3 rãnh bám ngón sâu 0,6.</p>')

    # chi tiết xanh, bạc
    rows_nho = [["Tấm mu tay", f"{so(k.TAM_X[1] - k.TAM_X[0])} × {so(tam_cao)}, dày {so(k.TAM_DAY)}, mặt trong cong R{so(k.TAM_R)}; 2 khe 23 × 3; logo cao {so(k.CAO_LOGO_TAM)}, chữ VERA LAB cỡ {so(k.CO_CHU_TAM)} khắc sâu {so(k.SAU_CHU)}"],
                ["Chén lục giác", f"bán kính đỉnh {so(k.R_CHEN)} ở miệng, 12,5 ở đáy, sâu 7,1"],
                ["Cò", f"trục Ø2,2; hốc nam châm Ø6,2 sâu 3,05; bóp hết {so(k.GOC_BOP, 0)}°"],
                ["Nắp nút phụ (4 cái)", "đầu Ø6,6 nhô 1,4; gờ giữ Ø8,6 dày 1,1"]]
    t5 = trang("Chi tiết xanh và chi tiết bạc",
               hang(Hinh(ct["tam_mu_tay"], R_CANH, ten="Tấm mu tay, mặt ngoài").svg(), Hinh(ct["tam_mu_tay"], R_TREN, ten="Tấm mu tay, nhìn từ trên").svg(),
                    Hinh(ct["chen"], R_MAT_NHIN, ten="Chén lục giác").svg()),
               hang(Hinh(ct["co"], R_CANH, 2.0, ten="Cò, nhìn bên").svg(), Hinh(ct["co"], R_TRUOC, 2.0, ten="Cò, nhìn trước").svg(),
                    Hinh(mh.nut_don(), R_CANH, 3.0, ten="Nắp nút phụ").svg()),
               bang(["Chi tiết", "Kích thước"], rows_nho),
               f'<p class="ghi">Mua thêm: quai băng gai dính 2 mặt rộng {so(k.RONG_QUAI, 0)} mm, cắt {cat_quai} mm; xốp mỏng 1–2 mm dán mặt trong tấm mu tay.</p>')

    css = """
@page{size:A4;margin:9mm 10mm}
body{font:10.5pt/1.45 'Segoe UI',Arial,sans-serif;color:#15232a;margin:0}
section{page-break-after:always}
section:last-child{page-break-after:auto}
h2{font:700 15pt/1.35 'Bahnschrift','Segoe UI',sans-serif;margin:0 0 3mm;padding-top:1.5mm;border-bottom:.4mm solid #0aa9c4;padding-bottom:1.5mm}
.hang{display:flex;flex-wrap:wrap;gap:2mm;align-items:flex-end;margin:1mm 0}
table{border-collapse:collapse;width:100%;font-size:9pt;margin:2mm 0}
th{text-align:left;border-bottom:.3mm solid #15232a;padding:1mm 2mm 1mm 0}
td{border-bottom:.15mm solid #c6d1cf;padding:1mm 2mm 1mm 0;vertical-align:top}
table.rong td:first-child{font-weight:600;width:28mm}
.ghi{font-size:9pt;color:#4a5b60;margin:1.5mm 0}
svg .ve{fill:#e3eaec;stroke:#15232a;stroke-width:.25;fill-rule:evenodd}
svg .kt{stroke:#b4471a;stroke-width:.18;fill:none}
svg .gd{stroke:#b4471a;stroke-width:.12}
svg .so{font:2.6px 'Segoe UI',Arial,sans-serif;fill:#b4471a}
svg .tv{font:600 3px 'Segoe UI',Arial,sans-serif;fill:#15232a}
svg .thuoc{fill:#15232a}
"""
    trang_html = f'<!doctype html><html lang="vi"><head><meta charset="utf-8"><title>VERA Band - kích thước</title><style>{css}</style></head><body>{t1}{t2}{t3}{t4}{t5}</body></html>'
    ra_html = os.path.join(mh.THU_MUC, "VERA_Band_kich_thuoc.html")
    with open(ra_html, "w", encoding="utf-8") as fh:
        fh.write(trang_html)
    ra_pdf = os.path.join(mh.THU_MUC, "VERA_Band_kich_thuoc.pdf")
    subprocess.run([EDGE, "--headless=new", "--disable-gpu", "--no-pdf-header-footer", f"--print-to-pdf={ra_pdf}", "file:///" + ra_html.replace("\\", "/")],
                   check=True, capture_output=True, timeout=120)
    print(ra_pdf, os.path.getsize(ra_pdf) // 1024, "KB")
    print(f"cán Ø tương đương {d_tuong:.1f}, vùng nắm {vung_nam:.0f}, quai {l_quai:.0f} → {quai_ngan:.0f}–{quai_dai:.0f}, cắt {cat_quai}")


def mh_goi():
    import json
    with open(os.path.join(mh.THU_MUC, "thong_so.json"), encoding="utf-8") as fh:
        return json.load(fh)


def mh_ten(ma):
    return {"than_phai": "nửa phải", "than_trai": "nửa trái", "nap": "đáy trên", "nap_pin": "đáy dưới", "tam_mu_tay": "tấm mu tay",
            "chen": "chén", "co": "cò", "nut_phu": "nắp nút"}.get(ma, ma)


if __name__ == "__main__":
    main()
