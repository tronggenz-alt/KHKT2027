"""Vẽ các bản vẽ 2D của tay cầm VR (bản V1) ra SVG.

Chạy:  python tools/ve_tay_cam/ve_2d.py
Ra:    docs/anh-tay-cam/TC-01..TC-05 *.svg  (file đứng riêng, màu giấy sáng)
       và trả về chuỗi SVG nhúng cho trang HTML (màu theo biến CSS của trang).

Mọi kích thước tính bằng mm; mỗi hình chiếu có hàm đổi mm -> px riêng.
Hệ trục tay cầm theo docs/02: X dọc ống ra đầu, Y sang trái, Z lên trên.
Gốc x = 0 ở mép sau của ống PVC (trong nắp bịt sau).
"""
import math
import os

GOC = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
THU_MUC_RA = os.path.join(GOC, "docs", "anh-tay-cam")

# ---------------- Kích thước V1 (mm) ----------------
ONG_L, R_NGOAI, R_TRONG = 180, 17, 15          # ống PVC phi 34 dày 2
NAP_R = 20                                       # nắp bịt Ø40
NAP_SAU = (-5, 20)
NAP_TRUOC = (160, 185)
MICA = (81, 157)                                 # tấm mica trên cửa sổ, rộng 30
CUA_SO = (84, 156)                               # cửa sổ cắt trên ống, rộng 26
J1_X, J2_X = 100, 138                            # tâm hai joystick
NUT_X = 72                                       # 2 nút Căn lại / Hỏi AI, y = ±6
CO_X = 128                                       # công tắc hành trình (cò)
MPU = (95, 116)
MOTOR = (24, 49.5)
MACH = (58, 92)                                  # phíp đấu dây
TUOT = (-17, -5)                                 # ốc siết cáp PG9

KHOI_LUONG = [  # (tên, g, x tâm) – dùng tính trọng tâm
    ("Ống PVC 180 mm", 50.6, 90), ("Nắp bịt sau", 12, 7.5), ("Nắp bịt trước", 12, 172.5),
    ("Ốc siết cáp + đoạn cáp", 8, -8), ("Tấm mica", 5.4, 119), ("Joystick 1", 10, J1_X),
    ("Joystick 2", 10, J2_X), ("2 nút + mạch nhỏ", 2, NUT_X), ("Công tắc KW11", 2, CO_X),
    ("MPU6050", 2.5, 105.5), ("Mô-tơ 130 + ốc lệch tâm", 20, 38), ("Phíp đấu dây + điện trở", 5, 75),
    ("Ốc vít M2", 4, 119), ("Dây nối bên trong", 6, 90), ("2 LED", 1, 185),
]
TONG_G = sum(m for _, m, _ in KHOI_LUONG)
TRONG_TAM_X = sum(m * x for _, m, x in KHOI_LUONG) / TONG_G

STYLE = """
.paper{fill:var(--sheet,#fbfcfa);stroke:var(--rule,#c8d2d0);stroke-width:1}
.ln{fill:none;stroke:var(--ink,#18272c);stroke-width:1.4;stroke-linejoin:round}
.thin{fill:none;stroke:var(--ink,#18272c);stroke-width:.7}
.hid{fill:none;stroke:var(--ink-2,#5d6e72);stroke-width:.8;stroke-dasharray:4 3}
.ctr{fill:none;stroke:var(--ink-2,#5d6e72);stroke-width:.6;stroke-dasharray:14 3 2 3}
.dim{fill:none;stroke:var(--dim,#0a6b8a);stroke-width:.75}
.dimt{fill:var(--dim,#0a6b8a);font:500 11.5px 'JetBrains Mono',ui-monospace,monospace}
.arr{fill:var(--dim,#0a6b8a)}
.t{fill:var(--ink,#18272c);font:500 12.5px 'Be Vietnam Pro','Segoe UI',sans-serif}
.tb{fill:var(--ink,#18272c);font:700 12.5px 'Be Vietnam Pro','Segoe UI',sans-serif}
.ts{fill:var(--ink-2,#5d6e72);font:400 11px 'Be Vietnam Pro','Segoe UI',sans-serif}
.tw{fill:var(--warn,#b4471a);font:600 11.5px 'Be Vietnam Pro','Segoe UI',sans-serif}
.h{fill:var(--ink,#18272c);font:700 17px 'Barlow Condensed','Arial Narrow',sans-serif;letter-spacing:.06em}
.hs{fill:var(--ink-2,#5d6e72);font:600 12px 'Barlow Condensed','Arial Narrow',sans-serif;letter-spacing:.08em}
.pvc{fill:var(--pvc,#dde5e3);stroke:var(--ink,#18272c);stroke-width:1.4}
.pvc2{fill:var(--pvc2,#c9d4d1);stroke:var(--ink,#18272c);stroke-width:1.4}
.mica{fill:var(--mica,rgba(110,180,210,.35));stroke:var(--ink,#18272c);stroke-width:1.1}
.cut{fill:url(#gach);stroke:var(--ink,#18272c);stroke-width:1.2}
.gach{stroke:var(--ink,#18272c);stroke-width:.7}
.pcbk{fill:#2a3036;stroke:var(--ink,#18272c);stroke-width:1}
.pcbb{fill:#2563a6;stroke:var(--ink,#18272c);stroke-width:1}
.pcbt{fill:#0f7c86;stroke:var(--ink,#18272c);stroke-width:1}
.pcbr{fill:#c0392b;stroke:var(--ink,#18272c);stroke-width:1}
.phip{fill:#c9a066;stroke:var(--ink,#18272c);stroke-width:1}
.kim{fill:#aab3b8;stroke:var(--ink,#18272c);stroke-width:1}
.dong{fill:#c99a3b;stroke:var(--ink,#18272c);stroke-width:1}
.nhua{fill:#4a5257;stroke:var(--ink,#18272c);stroke-width:1}
.ledx{fill:#22b573;stroke:var(--ink,#18272c);stroke-width:.8}
.ledd{fill:#e0362c;stroke:var(--ink,#18272c);stroke-width:.8}
.pin{fill:#3b4a52;stroke:var(--ink,#18272c);stroke-width:1}
.hand{fill:var(--hand,#e9b98f);stroke:var(--hand-ln,#9c5f37);stroke-width:1.3}
.handln{fill:none;stroke:var(--hand-ln,#9c5f37);stroke-width:1.1}
.fig{fill:none;stroke:var(--fig,#a9bcc0);stroke-linecap:round;stroke-linejoin:round}
.figf{fill:var(--fig,#a9bcc0)}
.zone{fill:var(--ok,#2f7d4f);fill-opacity:.16;stroke:var(--ok,#2f7d4f);stroke-width:1;stroke-dasharray:5 3}
.zonet{fill:var(--ok,#2f7d4f);font:600 11.5px 'Be Vietnam Pro','Segoe UI',sans-serif}
.box{fill:none;stroke:var(--ink-2,#5d6e72);stroke-width:1;stroke-dasharray:7 4}
.mod{fill:var(--sheet,#fbfcfa);stroke:var(--ink,#18272c);stroke-width:1.4}
.modh{fill:var(--pvc,#dde5e3)}
.dot{fill:var(--ink,#18272c)}
.ball{fill:var(--sheet,#fbfcfa);stroke:var(--ink,#18272c);stroke-width:1.1}
.ballt{fill:var(--ink,#18272c);font:700 11px 'JetBrains Mono',ui-monospace,monospace}
.warnbox{fill:var(--warn,#b4471a);fill-opacity:.1;stroke:var(--warn,#b4471a);stroke-width:1}
.cable{fill:none;stroke:var(--gnd,#2b3135);stroke-linecap:round}
.wire{fill:none;stroke-width:2.2;stroke-linecap:round;stroke-linejoin:round}
.gnd{stroke:var(--gnd,#2b3135)}
"""

FONT_IMPORT = ("@import url('https://fonts.googleapis.com/css2?family=Barlow+Condensed:wght@600;700"
               "&family=Be+Vietnam+Pro:wght@400;500;600;700&family=JetBrains+Mono:wght@500;700&display=swap');")

DAY = {  # màu dây theo lõi cáp
    "5V": "#e0362c", "GND": None, "SDA": "#d9a400", "SCL": "#2e9e44",
    "J1X": "#f08c00", "J1Y": "#8d5a2b", "J1SW": "#8e44ad",
    "J2X": "#f5a742", "J2Y": "#b98552", "J2SW": "#b07cc6",
    "CANLAI": "#e84393", "HOIAI": "#2d7be0", "CO": "#7f8c8d",
    "LEDX": "#16a596", "LEDD": "#a51d2d", "M+": "#8a969b", "M-": "#5b676c",
}


def fmt(v):
    return f"{v:.1f}".rstrip("0").rstrip(".")


class Svg:
    def __init__(self, w, h, ma):
        self.w, self.h, self.ma, self.el = w, h, ma, []

    def add(self, s):
        self.el.append(s)

    def line(self, x1, y1, x2, y2, cls="ln", extra=""):
        self.add(f'<line x1="{fmt(x1)}" y1="{fmt(y1)}" x2="{fmt(x2)}" y2="{fmt(y2)}" class="{cls}" {extra}/>')

    def rect(self, x, y, w, h, cls="ln", rx=0, extra=""):
        if w < 0:
            x, w = x + w, -w
        if h < 0:
            y, h = y + h, -h
        r = f' rx="{fmt(rx)}"' if rx else ""
        self.add(f'<rect x="{fmt(x)}" y="{fmt(y)}" width="{fmt(w)}" height="{fmt(h)}"{r} class="{cls}" {extra}/>')

    def circle(self, cx, cy, r, cls="ln", extra=""):
        self.add(f'<circle cx="{fmt(cx)}" cy="{fmt(cy)}" r="{fmt(r)}" class="{cls}" {extra}/>')

    def path(self, d, cls="ln", extra=""):
        self.add(f'<path d="{d}" class="{cls}" {extra}/>')

    def poly(self, pts, cls="ln", closed=False, extra=""):
        d = "M" + " L".join(f"{fmt(x)},{fmt(y)}" for x, y in pts) + (" Z" if closed else "")
        self.path(d, cls, extra)

    def text(self, x, y, s, cls="t", anchor="start", rot=None, extra=""):
        tr = f' transform="rotate({rot} {fmt(x)} {fmt(y)})"' if rot is not None else ""
        self.add(f'<text x="{fmt(x)}" y="{fmt(y)}" class="{cls}" text-anchor="{anchor}"{tr} {extra}>{s}</text>')

    def ar(self):
        return f'marker-start="url(#m{self.ma})" marker-end="url(#m{self.ma})"'

    def dimh(self, x1, x2, y, label, ref1=None, ref2=None, text_dy=-4):
        """Kích thước ngang: đường gióng từ ref (y vật) tới y, đường kích thước có mũi tên."""
        for x, r in ((x1, ref1), (x2, ref2)):
            if r is not None:
                d = 1 if y > r else -1
                self.line(x, r + 2 * d, x, y + 4 * d, "dim")
        self.line(x1, y, x2, y, "dim", self.ar())
        self.text((x1 + x2) / 2, y + text_dy, label, "dimt", "middle")

    def dimv(self, y1, y2, x, label, ref1=None, ref2=None, side=-1):
        for y, r in ((y1, ref1), (y2, ref2)):
            if r is not None:
                d = 1 if x > r else -1
                self.line(r + 2 * d, y, x + 4 * d, y, "dim")
        self.line(x, y1, x, y2, "dim", self.ar())
        tx = x + (-5 if side < 0 else 13)
        self.text(tx, (y1 + y2) / 2, label, "dimt", "middle", rot=-90)

    def leader(self, x, y, tx, ty, label, cls="t", anchor=None, sub=None):
        self.circle(x, y, 1.8, "dot")
        self.line(x, y, tx, ty, "thin")
        if anchor is None:
            anchor = "start" if tx >= x else "end"
        dx = 4 if anchor == "start" else -4
        self.text(tx + dx, ty + 4, label, cls, anchor)
        if sub:
            self.text(tx + dx, ty + 18, sub, "ts", anchor)

    def ball(self, x, y, n, tx, ty):
        self.circle(x, y, 1.8, "dot")
        self.line(x, y, tx, ty, "thin")
        self.circle(tx, ty, 10, "ball")
        self.text(tx, ty + 4, str(n), "ballt", "middle")

    def defs(self):
        return (f'<defs><marker id="m{self.ma}" viewBox="0 0 10 10" refX="10" refY="5" markerWidth="9" markerHeight="9" '
                f'orient="auto-start-reverse" markerUnits="userSpaceOnUse"><path d="M0,1.5 L10,5 L0,8.5 z" class="arr"/></marker>'
                f'<pattern id="gach" width="6" height="6" patternUnits="userSpaceOnUse" patternTransform="rotate(45)">'
                f'<line x1="0" y1="0" x2="0" y2="6" class="gach"/></pattern></defs>')

    def svg(self, standalone):
        css = (FONT_IMPORT.replace("&", "&amp;") if standalone else "") + STYLE
        head = (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {self.w} {self.h}" '
                + (f'width="{self.w}" height="{self.h}"' if standalone else 'class="sheet" role="img"') + ">")
        body = "\n".join(self.el)
        if not standalone:
            # pattern/marker id phải riêng cho từng bản vẽ khi nhúng chung một trang
            body = body.replace("url(#gach)", f"url(#gach{self.ma})")
            css = css.replace("url(#gach)", f"url(#gach{self.ma})")
            defs = self.defs().replace('id="gach"', f'id="gach{self.ma}"')
        else:
            defs = self.defs()
        return f"{head}<style>{css}</style>{defs}\n{body}\n</svg>"


def khung(s, so, ten, phu, x=None, y=None):
    """Viền bản vẽ + khung tên kiểu TCVN (góc dưới phải)."""
    s.rect(6, 6, s.w - 12, s.h - 12, "paper", rx=4)
    s.rect(14, 14, s.w - 28, s.h - 28, "thin")
    w, h = 540, 74
    x = s.w - 14 - w if x is None else x
    y = s.h - 14 - h if y is None else y
    s.rect(x, y, w, h, "ln")
    s.line(x, y + 30, x + 410, y + 30, "thin")
    s.line(x + 410, y + 30, x + w, y + 30, "thin")
    s.line(x + 410, y, x + 410, y + h, "thin")
    s.line(x + 410, y + 52, x + w, y + 52, "thin")
    s.text(x + 10, y + 21, ten, "h")
    s.text(x + 10, y + 47, phu, "ts")
    s.text(x + 10, y + 65, "Đơn vị: mm · Dự án KHKT Phòng thí nghiệm ảo · 27/10/2026", "ts")
    s.text(x + 420, y + 13, "BẢN VẼ SỐ", "hs")
    s.text(x + 420, y + 27, so, "tb")
    s.text(x + 420, y + 45, "Phiên bản V1", "ts")
    s.text(x + 420, y + 67, "Chiếu góc thứ nhất", "ts")


def catmull(pts, closed=True):
    """Đường cong trơn qua các điểm (Catmull-Rom -> Bézier bậc 3)."""
    n = len(pts)
    p = pts
    d = f"M{fmt(p[0][0])},{fmt(p[0][1])}"
    rng = range(n) if closed else range(n - 1)
    for i in rng:
        p0 = p[(i - 1) % n] if closed or i > 0 else p[i]
        p1 = p[i]
        p2 = p[(i + 1) % n]
        p3 = p[(i + 2) % n] if closed or i + 2 < n else p2
        c1 = (p1[0] + (p2[0] - p0[0]) / 6, p1[1] + (p2[1] - p0[1]) / 6)
        c2 = (p2[0] - (p3[0] - p1[0]) / 6, p2[1] - (p3[1] - p1[1]) / 6)
        d += f" C{fmt(c1[0])},{fmt(c1[1])} {fmt(c2[0])},{fmt(c2[1])} {fmt(p2[0])},{fmt(p2[1])}"
    return d + (" Z" if closed else "")


# ---------------- Hình chiếu dùng chung ----------------

def ve_canh_ben(s, X, Y, k, an=True, day_cap=True):
    """Hình chiếu đứng (nhìn từ phía phải, −Y): đầu tay cầm ở bên phải."""
    # ống: viền trên hạ xuống mặt mica trong vùng cửa sổ
    s.path(f"M{fmt(X(NAP_SAU[1]))},{fmt(Y(R_NGOAI))} L{fmt(X(CUA_SO[0]))},{fmt(Y(R_NGOAI))} "
           f"L{fmt(X(CUA_SO[0]))},{fmt(Y(11))} L{fmt(X(CUA_SO[1]))},{fmt(Y(11))} L{fmt(X(CUA_SO[1]))},{fmt(Y(R_NGOAI))} "
           f"L{fmt(X(NAP_TRUOC[0]))},{fmt(Y(R_NGOAI))} L{fmt(X(NAP_TRUOC[0]))},{fmt(Y(-R_NGOAI))} "
           f"L{fmt(X(NAP_SAU[1]))},{fmt(Y(-R_NGOAI))} Z", "pvc")
    s.rect(X(MICA[0]), Y(13), (MICA[1] - MICA[0]) * k, 2 * k, "mica")
    for a, b in (NAP_SAU, NAP_TRUOC):
        s.rect(X(a), Y(NAP_R), (b - a) * k, 2 * NAP_R * k, "pvc2", rx=2.5 * k)
    # lỗ xỏ dây đeo ở nắp sau
    s.circle(X(8), Y(-13), 2 * k, "ln")
    # ốc siết cáp PG9 + cáp
    s.rect(X(-11), Y(9.5), 6 * k, 19 * k, "nhua")
    s.path(f"M{fmt(X(-11))},{fmt(Y(7.5))} Q{fmt(X(-18))},{fmt(Y(7.5))} {fmt(X(-17))},{fmt(Y(0))} "
           f"Q{fmt(X(-18))},{fmt(Y(-7.5))} {fmt(X(-11))},{fmt(Y(-7.5))} Z", "nhua")
    if day_cap:
        s.rect(X(-40), Y(3.5), 23 * k, 7 * k, "pin")
        s.path(f"M{fmt(X(-40) - 3)},{fmt(Y(5))} l4,{fmt(3.5*k)} l-4,{fmt(3.5*k)} l4,{fmt(3.5*k)}", "thin")
    # nút bấm (2 nút trùng nhau khi nhìn ngang)
    s.rect(X(NUT_X - 4.5), Y(19.5), 9 * k, 2.5 * k, "nhua", rx=1)
    # joystick: vòm, cần, mũ
    for jx in (J1_X, J2_X):
        s.path(f"M{fmt(X(jx-8))},{fmt(Y(13))} Q{fmt(X(jx-8))},{fmt(Y(19.5))} {fmt(X(jx))},{fmt(Y(19.5))} "
               f"Q{fmt(X(jx+8))},{fmt(Y(19.5))} {fmt(X(jx+8))},{fmt(Y(13))} Z", "nhua")
        s.rect(X(jx - 2), Y(28), 4 * k, 9 * k, "kim")
        s.rect(X(jx - 10), Y(32), 20 * k, 4 * k, "pcbk", rx=1.5 * k)
    # ốc M2 trên mica
    for sx in (85, 153):
        s.rect(X(sx - 2), Y(14.3), 4 * k, 1.3 * k, "kim")
    # cò: cần công tắc KW11 nhô dưới ống
    s.path(f"M{fmt(X(118))},{fmt(Y(-17))} L{fmt(X(137))},{fmt(Y(-21.5))} Q{fmt(X(141))},{fmt(Y(-23))} "
           f"{fmt(X(139))},{fmt(Y(-19.5))} L{fmt(X(121))},{fmt(Y(-16.6))}", "kim")
    # LED ở mặt nắp trước
    s.path(f"M{fmt(X(185))},{fmt(Y(10.5))} h{fmt(2*k)} a{fmt(2.5*k)},{fmt(2.5*k)} 0 0 1 0,{fmt(5*k)} h{fmt(-2*k)} Z", "ledx")
    s.path(f"M{fmt(X(185))},{fmt(Y(-5.5))} h{fmt(2*k)} a{fmt(2.5*k)},{fmt(2.5*k)} 0 0 1 0,{fmt(5*k)} h{fmt(-2*k)} Z", "ledd")
    if an:  # nét khuất: linh kiện bên trong
        s.rect(X(MOTOR[0]), Y(7.5), (MOTOR[1] - MOTOR[0]) * k, 15 * k, "hid")
        s.circle(X(53), Y(1.5), 4.5 * k, "hid")
        s.rect(X(MACH[0]), Y(-4.4), (MACH[1] - MACH[0]) * k, 1.6 * k, "hid")
        s.rect(X(MPU[0]), Y(-8.4), (MPU[1] - MPU[0]) * k, 1.6 * k, "hid")
        for jx in (J1_X, J2_X):
            s.rect(X(jx - 17), Y(1.6), 34 * k, 1.6 * k, "hid")
            s.rect(X(jx - 8), Y(11), 16 * k, 9.4 * k, "hid")
        s.rect(X(118), Y(-5), 20 * k, 10 * k, "hid")
        s.line(X(-2), Y(R_TRONG), X(NAP_SAU[1]), Y(R_TRONG), "hid")
        s.line(X(-2), Y(-R_TRONG), X(NAP_TRUOC[1] - 3), Y(-R_TRONG), "hid")
    s.line(X(-45), Y(0), X(192), Y(0), "ctr")


def ve_tren(s, X, Yy, k, an=True):
    """Hình chiếu bằng (nhìn từ trên xuống): Y+ (bên trái tay cầm) hướng lên giấy."""
    s.rect(X(NAP_SAU[1]), Yy(R_NGOAI), (NAP_TRUOC[0] - NAP_SAU[1]) * k, 2 * R_NGOAI * k, "pvc")
    for a, b in (NAP_SAU, NAP_TRUOC):
        s.rect(X(a), Yy(NAP_R), (b - a) * k, 2 * NAP_R * k, "pvc2", rx=2.5 * k)
    s.rect(X(MICA[0]), Yy(15), (MICA[1] - MICA[0]) * k, 30 * k, "mica", rx=1.5 * k)
    for sx in (85, 153):
        for sy in (11, -11):
            s.circle(X(sx), Yy(sy), 1.6 * k, "kim")
    for jx in (J1_X, J2_X):
        if an:
            s.rect(X(jx - 17), Yy(13), 34 * k, 26 * k, "hid")
        s.circle(X(jx), Yy(0), 8 * k, "nhua")
        s.circle(X(jx), Yy(0), 10 * k, "pcbk")
        s.circle(X(jx), Yy(0), 6.5 * k, "thin")
    for sy in (6, -6):
        s.circle(X(NUT_X), Yy(sy), 4.5 * k, "nhua")
    s.rect(X(-11), Yy(9.5), 6 * k, 19 * k, "nhua")
    s.path(f"M{fmt(X(-11))},{fmt(Yy(7.5))} Q{fmt(X(-18))},{fmt(Yy(7.5))} {fmt(X(-17))},{fmt(Yy(0))} "
           f"Q{fmt(X(-18))},{fmt(Yy(-7.5))} {fmt(X(-11))},{fmt(Yy(-7.5))} Z", "nhua")
    s.rect(X(-40), Yy(3.5), 23 * k, 7 * k, "pin")
    s.rect(X(185), Yy(2.5), 2 * k, 5 * k, "ledx")
    if an:
        s.rect(X(118), Yy(3.2), 20 * k, 6.4 * k, "hid")
        s.rect(X(MPU[0]), Yy(8), (MPU[1] - MPU[0]) * k, 16 * k, "hid")
        s.rect(X(MOTOR[0]), Yy(10), (MOTOR[1] - MOTOR[0]) * k, 20 * k, "hid")
    s.line(X(-45), Yy(0), X(192), Yy(0), "ctr")


# ---------------- TC-01: ba hình chiếu + mặt cắt ----------------

def tc01():
    s = Svg(1180, 830, 1)
    khung(s, "TC-01", "TAY CẦM VR V1 · BA HÌNH CHIẾU", "Ống PVC Ø34×2 Bình Minh, nắp bịt Ø34, tấm mica 2 mm")
    k = 2.6
    ox, oy = 125, 225
    X = lambda x: ox + x * k
    Y = lambda z: oy - z * k
    s.text(40, 48, "HÌNH CHIẾU ĐỨNG", "h")
    s.text(40, 64, "nhìn từ phía phải (−Y), đầu tay cầm ở bên phải", "ts")
    ve_canh_ben(s, X, Y, k)
    # kích thước trên: tính từ mép sau ống (x = 0)
    s.line(X(0), Y(R_NGOAI) - 2, X(0), 80, "dim")
    s.dimh(X(0), X(NUT_X), 118, "72", None, Y(19.5))
    s.dimh(X(0), X(J1_X), 100, "100", None, Y(32))
    s.dimh(X(0), X(J2_X), 82, "138", None, Y(32))
    # kích thước dưới
    s.dimh(X(0), X(CO_X), 300, "128 (tâm cò)", Y(-R_NGOAI), Y(-21))
    s.dimh(X(0), X(ONG_L), 322, "180 (ống)", Y(-R_NGOAI), Y(-R_NGOAI))
    s.dimh(X(NAP_SAU[0]), X(NAP_TRUOC[1]), 344, "190 (thân)", Y(-NAP_R), Y(-NAP_R))
    s.dimh(X(-17), X(-5), 272, "12", Y(-9.5), Y(-NAP_R))
    # chiều cao
    s.dimv(Y(R_NGOAI), Y(32), X(152), "15", side=1)
    s.line(X(148), Y(32), X(156), Y(32), "dim")
    s.line(X(150), Y(R_NGOAI), X(156), Y(R_NGOAI), "dim")
    s.dimv(Y(-R_NGOAI), Y(-23), X(146), "6", side=1)
    s.dimv(Y(NAP_R), Y(-NAP_R), X(185) + 30, "Ø40", X(185), X(185), side=1)
    s.leader(X(40), Y(R_NGOAI), X(40) + 30, Y(R_NGOAI) - 46, "Ø34×2 (ống PVC)")
    s.leader(X(8), Y(-13), X(8) - 4, Y(-13) + 60, "lỗ Ø4 xỏ dây đeo", anchor="end")
    s.leader(X(-14), Y(9.5), X(-14) + 8, Y(9.5) - 56, "ốc siết cáp PG9")
    s.leader(X(129), Y(-19), X(129) + 60, Y(-19) + 30, "cò (cần KW11)")

    # hình chiếu bằng
    oy2 = 520
    Yy = lambda y: oy2 - y * k
    s.text(40, 410, "HÌNH CHIẾU BẰNG", "h")
    s.text(40, 426, "nhìn từ trên xuống, nét đứt là linh kiện khuất", "ts")
    ve_tren(s, X, Yy, k)
    s.dimh(X(J1_X), X(J2_X), 600, "38", Yy(-10), Yy(-10))
    s.dimh(X(MICA[0]), X(MICA[1]), 624, "76 (tấm mica)", Yy(-15), Yy(-15))
    s.dimv(Yy(6), Yy(-6), X(NUT_X) - 36, "12", X(NUT_X) - 12, X(NUT_X) - 12)
    s.dimv(Yy(15), Yy(-15), X(185) + 30, "30", X(MICA[1]), X(MICA[1]), side=1)
    s.leader(X(NUT_X), Yy(6), X(NUT_X) - 50, Yy(6) - 46, "Căn lại", anchor="end")
    s.leader(X(NUT_X), Yy(-6), X(NUT_X) - 50, Yy(-6) + 50, "Hỏi AI", anchor="end")
    s.leader(X(J1_X), Yy(7), X(J1_X) + 20, Yy(7) - 74, "Joystick 1 · chỉnh tinh")
    s.leader(X(J2_X), Yy(7), X(J2_X) + 6, Yy(7) - 48, "Joystick 2 · di chuyển")
    s.leader(X(85), Yy(11), X(85) - 4, Yy(11) - 50, "4 ốc M2", anchor="end")

    # hình chiếu cạnh: nhìn từ đuôi (đặt bên phải hình chiếu đứng theo chiếu góc thứ nhất)
    cx, cy = 735, oy
    s.text(660, 48, "HÌNH CHIẾU CẠNH", "h")
    s.text(660, 64, "nhìn từ đuôi (−X)", "ts")
    U = lambda y: cx - y * k     # nhìn từ đuôi: Y+ (trái tay cầm) ở bên phải giấy? đặt −y sang phải
    for jy in (0,):
        s.path(f"M{fmt(U(-8))},{fmt(Y(13))} Q{fmt(U(-8))},{fmt(Y(19.5))} {fmt(U(0))},{fmt(Y(19.5))} "
               f"Q{fmt(U(8))},{fmt(Y(19.5))} {fmt(U(8))},{fmt(Y(13))} Z", "nhua")
        s.rect(U(2), Y(28), 4 * k, 9 * k, "kim")
        s.rect(U(10), Y(32), 20 * k, 4 * k, "pcbk", rx=1.5 * k)
    s.rect(U(2), Y(-17), 4 * k, 6 * k, "kim")
    s.circle(cx, cy, NAP_R * k, "pvc2")
    s.circle(cx, cy, (NAP_R - 3) * k, "thin")
    hexa = [(cx + 11 * k * math.cos(math.radians(a)), cy + 11 * k * math.sin(math.radians(a))) for a in range(0, 360, 60)]
    s.poly(hexa, "nhua", True)
    s.circle(cx, cy, 7.5 * k, "nhua")
    s.circle(cx, cy, 3.5 * k, "pin")
    s.line(cx - 26 * k, cy, cx + 26 * k, cy, "ctr")
    s.line(cx, cy - 35 * k, cx, cy + 26 * k, "ctr")
    s.dimv(Y(NAP_R), Y(-NAP_R), cx + 30 * k, "Ø40", cx, cx, side=1)
    s.dimh(cx - 9.5 * k, cx + 9.5 * k, cy + NAP_R * k + 26, "19 (lục giác PG9)", cy + 9.5 * k, cy + 9.5 * k, text_dy=16)
    s.leader(cx - 2.5 * k, cy + 2.5 * k, cx - 30, cy + 75, "cáp 20 lõi Ø7", anchor="end")

    # mặt cắt A-A tại x = 134 (qua joystick 2 và cò)
    k2 = 4.4
    mx, my = 955, 520
    P = lambda y: mx + y * k2
    Q = lambda z: my - z * k2
    s.text(880, 330, "MẶT CẮT A–A", "h")
    s.text(880, 346, "tại x = 134, qua joystick 2 và cò", "ts")
    s.line(X(134), Y(36), X(134), Y(40), "ln")
    s.line(X(134), Y(-25), X(134), Y(-29), "ln")
    s.text(X(134) + 4, Y(38), "A", "tb")
    s.text(X(134) + 4, Y(-29) + 6, "A", "tb")
    zo, zi = math.sqrt(R_NGOAI**2 - 13**2), math.sqrt(R_TRONG**2 - 13**2)
    s.path(f"M{fmt(P(13))},{fmt(Q(zo))} A{fmt(R_NGOAI*k2)},{fmt(R_NGOAI*k2)} 0 1 1 {fmt(P(-13))},{fmt(Q(zo))} "
           f"L{fmt(P(-13))},{fmt(Q(zi))} A{fmt(R_TRONG*k2)},{fmt(R_TRONG*k2)} 0 1 0 {fmt(P(13))},{fmt(Q(zi))} Z", "cut")
    s.rect(P(-4), Q(-14.6), 8 * k2, 3 * k2, "paper", extra='style="stroke:none"')   # khe cò
    s.line(P(-4), Q(-14.8), P(-4), Q(-17.1), "ln")
    s.line(P(4), Q(-14.8), P(4), Q(-17.1), "ln")
    s.rect(P(-15), Q(13), 30 * k2, 2 * k2, "mica")
    s.rect(P(-13), Q(1.6), 26 * k2, 1.6 * k2, "pcbk")
    s.rect(P(-8), Q(13), 16 * k2, 11.4 * k2, "nhua")
    s.rect(P(8), Q(11), 5 * k2, 9 * k2, "kim")
    s.path(f"M{fmt(P(-8))},{fmt(Q(13))} Q{fmt(P(-8))},{fmt(Q(19.5))} {fmt(P(0))},{fmt(Q(19.5))} "
           f"Q{fmt(P(8))},{fmt(Q(19.5))} {fmt(P(8))},{fmt(Q(13))} Z", "nhua")
    s.rect(P(-2), Q(28), 4 * k2, 8.5 * k2, "kim")
    s.rect(P(-10), Q(32), 20 * k2, 4 * k2, "pcbk", rx=1.5 * k2)
    s.rect(P(-3.2), Q(-5), 6.4 * k2, 10 * k2, "nhua")
    s.rect(P(-2), Q(-21), 4 * k2, 1.6 * k2, "kim")
    for i, (wy, wz) in enumerate(((-9, -8), (-6.5, -10), (-10.5, -10.6), (-8, -12.4))):
        s.circle(P(wy), Q(wz), 1.1 * k2, "pin")
    s.line(P(-22), Q(0), P(22), Q(0), "ctr")
    s.line(P(0), Q(36), P(0), Q(-25), "ctr")
    s.dimh(P(-R_NGOAI), P(R_NGOAI), Q(-R_NGOAI) + 44, "Ø34", Q(-6), Q(-6), text_dy=16)
    s.dimh(P(-13), P(13), Q(32) - 16, "26 (cửa sổ)", Q(11.5), Q(11.5))
    s.dimv(Q(R_NGOAI), Q(32), P(-R_NGOAI) - 22, "15", P(-10), None)
    s.line(P(-R_NGOAI) - 26, Q(R_NGOAI), P(-13), Q(R_NGOAI), "dim")
    s.leader(P(14), Q(12), P(14) + 34, Q(12) - 22, "mica 2")
    s.leader(P(10), Q(6), P(10) + 46, Q(6) + 4, "chiết áp")
    s.leader(P(16), Q(-4), P(16) + 38, Q(-4) + 34, "thành ống 2")
    s.leader(P(11), Q(1), P(11) + 52, Q(1) + 22, "mạch KY-023")
    s.leader(P(2), Q(-8), P(2) + 70, Q(-8) + 44, "KW11 20×10×6,4")
    s.leader(P(-9), Q(-10), P(-9) - 40, Q(-10) + 44, "bó dây", anchor="end")

    # ghi chú
    gx, gy = 40, 676
    s.text(gx, gy, "GHI CHÚ", "hs")
    notes = [
        "1. Ống PVC phi 34 thực đo Ø33,5, dày 2,05 (phiếu thử Bình Minh); hình vẽ dùng số tròn Ø34×2.",
        "2. Cửa sổ 72×26 cắt trên đỉnh ống bằng cưa lọng; tấm mica che cửa sổ, bắt 4 ốc M2 vào thành ống.",
        "3. Vùng nắm Ø34 dài 140 (giữa hai nắp): đủ cho bàn tay rộng tới 90 mm (P95 nam Việt Nam).",
        f"4. Khối lượng ước tính {TONG_G:.0f} g (chưa tính cáp); trọng tâm cách mép sau ống {TRONG_TAM_X:.0f} mm, nằm trong lòng bàn tay.",
    ]
    for i, n in enumerate(notes):
        s.text(gx, gy + 20 + i * 18, n, "ts")
    return s


# ---------------- TC-02: bố trí bên trong ----------------

def tc02():
    s = Svg(1180, 780, 2)
    khung(s, "TC-02", "TAY CẦM VR V1 · BỐ TRÍ BÊN TRONG", "Cắt dọc qua trục ống, bỏ nửa phía trước người xem")
    k = 4.0
    ox, oy = 215, 300
    X = lambda x: ox + x * k
    Y = lambda z: oy - z * k
    s.text(40, 48, "CẮT DỌC TAY CẦM", "h")
    s.text(40, 64, "nhìn từ phía phải (−Y); số trong vòng tròn ứng với bảng bên dưới", "ts")
    # vùng tay ôm
    s.rect(X(NAP_SAU[1]), Y(-27), (NAP_TRUOC[0] - NAP_SAU[1]) * k, 4 * k, "zone")
    s.text(X(90), Y(-27) + 36, "vùng bàn tay ôm: 140 mm (cần ≥ 91 mm cho tay to P95)", "zonet", "middle")
    # thành ống: trên (trừ cửa sổ), dưới (trừ khe cò)
    for a, b in ((NAP_SAU[1] - 18, CUA_SO[0]), (CUA_SO[1], NAP_TRUOC[0] + 18)):
        s.rect(X(max(a, 0)), Y(R_NGOAI), (min(b, ONG_L) - max(a, 0)) * k, 2 * k, "cut")
    for a, b in ((0, 117), (139, ONG_L)):
        s.rect(X(a), Y(-R_TRONG), (b - a) * k, 2 * k, "cut")
    # nắp bịt (cắt): hình chữ U
    s.path(f"M{fmt(X(20))},{fmt(Y(20))} L{fmt(X(-5))},{fmt(Y(20))} L{fmt(X(-5))},{fmt(Y(-20))} L{fmt(X(20))},{fmt(Y(-20))} "
           f"L{fmt(X(20))},{fmt(Y(-17))} L{fmt(X(-2))},{fmt(Y(-17))} L{fmt(X(-2))},{fmt(Y(-5))} L{fmt(X(-5))},{fmt(Y(-5))} "
           f"L{fmt(X(-5))},{fmt(Y(5))} L{fmt(X(-2))},{fmt(Y(5))} L{fmt(X(-2))},{fmt(Y(17))} L{fmt(X(20))},{fmt(Y(17))} Z", "cut")
    s.path(f"M{fmt(X(160))},{fmt(Y(20))} L{fmt(X(185))},{fmt(Y(20))} L{fmt(X(185))},{fmt(Y(-20))} L{fmt(X(160))},{fmt(Y(-20))} "
           f"L{fmt(X(160))},{fmt(Y(-17))} L{fmt(X(182))},{fmt(Y(-17))} L{fmt(X(182))},{fmt(Y(17))} L{fmt(X(160))},{fmt(Y(17))} Z", "cut")
    s.rect(X(182), Y(10.5), 3 * k, 5 * k, "paper", extra='style="stroke:none"')
    s.rect(X(182), Y(-5.5), 3 * k, 5 * k, "paper", extra='style="stroke:none"')
    # LED
    s.path(f"M{fmt(X(181))},{fmt(Y(10.5))} h{fmt(6*k)} a{fmt(2.5*k)},{fmt(2.5*k)} 0 0 1 0,{fmt(5*k)} h{fmt(-6*k)} Z", "ledx")
    s.path(f"M{fmt(X(181))},{fmt(Y(-5.5))} h{fmt(6*k)} a{fmt(2.5*k)},{fmt(2.5*k)} 0 0 1 0,{fmt(5*k)} h{fmt(-6*k)} Z", "ledd")
    # ốc siết cáp + cáp
    s.rect(X(-11), Y(9.5), 6 * k, 19 * k, "nhua")
    s.path(f"M{fmt(X(-11))},{fmt(Y(7.5))} Q{fmt(X(-18))},{fmt(Y(7.5))} {fmt(X(-17))},{fmt(Y(0))} "
           f"Q{fmt(X(-18))},{fmt(Y(-7.5))} {fmt(X(-11))},{fmt(Y(-7.5))} Z", "nhua")
    s.rect(X(-40), Y(3.5), 23 * k, 7 * k, "pin")
    s.rect(X(-5), Y(3.5), 10 * k, 7 * k, "pin")
    # bó dây trong ống
    s.path(f"M{fmt(X(5))},{fmt(Y(-1))} C{fmt(X(14))},{fmt(Y(-11))} {fmt(X(40))},{fmt(Y(-12))} {fmt(X(MACH[0]))},{fmt(Y(-7))}",
           "cable", 'stroke-width="9" stroke-opacity=".55"')
    for col, pts in (
        ("#e0362c", [(MACH[0] + 6, -4.4), (75, 8), (NUT_X, 10)]),
        ("#d9a400", [(MACH[1] - 4, -5), (95, -8)]),
        ("#f08c00", [(MACH[1] - 8, -4.4), (88, 0)]),
        ("#f5a742", [(MACH[1] - 2, -4.4), (118, 0.5)]),
        ("#8a969b", [(MACH[0] + 2, -4.4), (50, -3), (MOTOR[1] - 4, -6)]),
        ("#7f8c8d", [(MACH[1] - 1, -6), (110, -12), (120, -9)]),
        ("#16a596", [(MACH[1], -5), (150, -10), (181, 13)]),
    ):
        d = "M" + " L".join(f"{fmt(X(x))},{fmt(Y(z))}" for x, z in pts)
        s.path(d, "wire", f'stroke="{col}" stroke-width="1.6"')
    # mô-tơ 130 + ốc lệch tâm, bọc mút
    s.rect(X(MOTOR[0] - 2), Y(9.5), (MOTOR[1] - MOTOR[0] + 4) * k, 19 * k, "zone", rx=6, extra='style="fill-opacity:.08;stroke-dasharray:2 2"')
    s.rect(X(MOTOR[0]), Y(7.5), (MOTOR[1] - MOTOR[0]) * k, 15 * k, "kim", rx=3)
    s.rect(X(MOTOR[1]), Y(0.7), 6 * k, 1.4 * k, "kim")
    s.path(f"M{fmt(X(51))},{fmt(Y(1.8))} A{fmt(5*k)},{fmt(5*k)} 0 1 1 {fmt(X(55))},{fmt(Y(1.8))} Z", "dong")
    # phíp đấu dây + 2 điện trở
    s.rect(X(MACH[0]), Y(-4.4), (MACH[1] - MACH[0]) * k, 1.6 * k, "phip")
    for rx_ in (64, 72):
        s.rect(X(rx_), Y(-1.6), 6 * k, 2.4 * k, "dong", rx=3)
    # nút bấm
    s.rect(X(NUT_X - 8), Y(8.4), 16 * k, 1.6 * k, "phip")
    s.rect(X(NUT_X - 3), Y(13.5), 6 * k, 3.5 * k, "nhua")
    s.rect(X(NUT_X - 4.5), Y(19.5), 9 * k, 6 * k, "nhua", rx=3)
    # MPU6050 + trục
    s.rect(X(MPU[0]), Y(-8.4), (MPU[1] - MPU[0]) * k, 1.6 * k, "pcbb")
    s.rect(X(104), Y(-6.6), 4 * k, 1.8 * k, "nhua")
    mx, mz = X(106), Y(-10.5)
    mx, mz = X(98), Y(-12.5)
    s.line(mx, mz, mx + 34, mz, "ln", 'style="stroke:#e0362c"')
    s.path(f"M{fmt(mx+34)},{fmt(mz-4)} l9,4 l-9,4 Z", "ledd")
    s.text(mx + 46, mz + 4, "X", "tb")
    s.line(mx, mz, mx, mz + 30, "ln", 'style="stroke:#2d7be0"')
    s.path(f"M{fmt(mx-4)},{fmt(mz+30)} l4,9 l4,-9 Z", "ln", 'style="fill:#2d7be0;stroke:#2d7be0"')
    s.text(mx + 6, mz + 40, "−Z", "tb")
    s.text(mx - 6, mz + 4, "⊗Y", "tb", "end")
    # joystick
    for jx in (J1_X, J2_X):
        s.rect(X(jx - 17), Y(1.6), 34 * k, 1.6 * k, "pcbk")
        for px_ in range(-12, 13, 6):
            s.rect(X(jx + px_ - 0.6), Y(0), 1.2 * k, 3 * k, "kim")
        s.rect(X(jx - 8), Y(13), 16 * k, 11.4 * k, "nhua")
        s.rect(X(jx + 8), Y(11), 5 * k, 9 * k, "kim")
        s.path(f"M{fmt(X(jx-8))},{fmt(Y(13))} Q{fmt(X(jx-8))},{fmt(Y(19.5))} {fmt(X(jx))},{fmt(Y(19.5))} "
               f"Q{fmt(X(jx+8))},{fmt(Y(19.5))} {fmt(X(jx+8))},{fmt(Y(13))} Z", "nhua")
        s.rect(X(jx - 2), Y(28), 4 * k, 8.5 * k, "kim")
        s.rect(X(jx - 10), Y(32), 20 * k, 4 * k, "pcbk", rx=1.5 * k)
        s.rect(X(jx - 12), Y(13), 3 * k, 13 * k, "kim")      # trụ đồng M2 treo mạch dưới mica
    s.rect(X(MICA[0]), Y(13), (MICA[1] - MICA[0]) * k, 2 * k, "mica")
    # công tắc KW11
    s.rect(X(118), Y(-5), 20 * k, 10 * k, "nhua")
    s.rect(X(121), Y(-15), 2 * k, 1 * k, "kim")
    s.path(f"M{fmt(X(118))},{fmt(Y(-15.2))} L{fmt(X(137))},{fmt(Y(-21.5))} Q{fmt(X(141))},{fmt(Y(-23))} "
           f"{fmt(X(139))},{fmt(Y(-19.5))} L{fmt(X(121))},{fmt(Y(-14.8))} Z", "kim")
    # trọng tâm
    tx_ = X(TRONG_TAM_X)
    s.circle(tx_, oy, 9, "ball")
    s.path(f"M{fmt(tx_)},{fmt(oy-9)} A9,9 0 0 1 {fmt(tx_+9)},{fmt(oy)} L{fmt(tx_)},{fmt(oy)} Z", "dot")
    s.path(f"M{fmt(tx_)},{fmt(oy+9)} A9,9 0 0 1 {fmt(tx_-9)},{fmt(oy)} L{fmt(tx_)},{fmt(oy)} Z", "dot")
    s.line(X(-45), oy, X(192), oy, "ctr")
    # số chú thích
    top, bot = 112, 486
    balls = [
        (1, X(30), Y(R_NGOAI - 1), X(30), top), (2, X(4), Y(19), X(4), top), (3, X(-14), Y(8), X(-26), top),
        (7, X(NUT_X), Y(19.5), X(NUT_X), top), (9, X(J1_X), Y(32), X(J1_X), top), (11, X(MICA[1] - 3), Y(12), X(165), top),
        (10, X(J2_X), Y(32), X(J2_X), top), (13, X(184), Y(13), X(184), top),
        (4, X(-30), Y(-3.5), X(-30), bot), (5, X(36), Y(-7.5), X(36), bot), (6, X(80), Y(-4.4), X(80), bot),
        (14, tx_, oy + 9, X(96), bot), (8, X(110), Y(-8.4), X(112), bot), (12, X(132), Y(-20), X(132), bot),
    ]
    for n, x, y, tx, ty in balls:
        s.ball(x, y, n, tx, ty)
    s.dimh(X(0), X(TRONG_TAM_X), 386, f"{TRONG_TAM_X:.0f}", Y(-R_TRONG), oy + 9)

    # bảng chú thích
    rows = [
        ("1", "Ống PVC Ø34×2, dài 180", "50,6 g"), ("2", "Nắp bịt sau Ø34, lỗ Ø4 xỏ dây đeo", "12 g"),
        ("3", "Ốc siết cáp PG9 (cáp Ø4–8)", "–"), ("4", "Cáp 20 lõi chống nhiễu, 1,2 m", "–"),
        ("5", "Mô-tơ 130 + ốc M3 lệch tâm, bọc mút", "20 g"), ("6", "Phíp đấu dây 34×24 + 2 điện trở 220 Ω", "5 g"),
        ("7", "Nút Căn lại + Hỏi AI (6×6, mũ Ø9)", "2 g"), ("8", "MPU6050 GY-521, trục trùng trục tay cầm", "2,5 g"),
        ("9", "Joystick 1 KY-023 (chỉnh tinh)", "10 g"), ("10", "Joystick 2 KY-023 (di chuyển)", "10 g"),
        ("11", "Tấm mica 76×30×2 + 4 trụ đồng M2", "9 g"), ("12", "Công tắc hành trình KW11 có cần (cò)", "2 g"),
        ("13", "Nắp bịt trước + LED xanh, đỏ 5 mm", "13 g"), ("14", f"Trọng tâm, cách mép sau ống {TRONG_TAM_X:.0f} mm", f"Σ {TONG_G:.0f} g"),
    ]
    bx, by = 40, 516
    s.text(bx, by, "BẢNG CHÚ THÍCH", "hs")
    for i, (n, ten, m) in enumerate(rows):
        col, r = divmod(i, 7)
        x0, y0 = bx + col * 380, by + 22 + r * 22
        s.circle(x0 + 10, y0 - 4, 9, "ball")
        s.text(x0 + 10, y0, n, "ballt", "middle")
        s.text(x0 + 26, y0, ten, "t")
        s.text(x0 + 340, y0, m, "dimt", "end")
    return s


# ---------------- TC-03: hộp đeo thắt lưng ----------------

def tc03():
    s = Svg(1180, 560, 3)
    khung(s, "TC-03", "HỘP ĐEO THẮT LƯNG", "Hộp nhựa 158×90×60, kẹp thắt lưng phía sau")
    k = 2.7
    ox, oy = 60, 112
    U = lambda u: ox + u * k
    V = lambda v: oy + v * k
    s.text(40, 48, "NHÌN TỪ TRÊN (đã mở nắp)", "h")
    s.text(40, 64, "sạc dự phòng nằm dưới tấm đế mica, xem hình cắt bên phải", "ts")
    s.rect(U(0), V(0), 158 * k, 90 * k, "pvc2", rx=6 * k)
    s.rect(U(2.5), V(2.5), 153 * k, 85 * k, "paper", rx=4 * k)
    s.rect(U(6), V(31), 101.6 * k, 53.3 * k, "pcbt", rx=2)
    s.text(U(56), V(60), "ARDUINO MEGA 2560", "tb", "middle", extra='style="fill:#fff"')
    s.text(U(56), V(66) + 4, "101,6 × 53,3", "ts", "middle", extra='style="fill:#d8f3f5"')
    s.rect(U(6) - 7, V(37), 9, 12 * k / 1.6, "kim")          # cổng USB-B
    s.rect(U(112), V(6), 43 * k, 43 * k, "pcbr", rx=2)
    s.rect(U(120), V(10), 27 * k, 15 * k, "nhua")
    s.text(U(133.5), V(36), "L298N", "tb", "middle", extra='style="fill:#fff"')
    s.text(U(133.5), V(42), "43 × 43 × 27", "ts", "middle", extra='style="fill:#fde3df"')
    s.rect(U(112), V(55), 40.5 * k, 27 * k, "pcbk", rx=2)
    s.rect(U(141), V(59), 10 * k, 19 * k, "kim")
    s.text(U(125), V(70), "ESP32-CAM", "tb", "middle", extra='style="fill:#fff;font-size:10.5px"')
    s.text(U(125), V(76), "40,5 × 27", "ts", "middle", extra='style="fill:#d6dbe0"')
    s.rect(U(84), V(8), 22 * k, 16 * k, "phip")
    s.text(U(95), V(18), "chia áp", "ts", "middle", extra='style="fill:#3b2a10"')
    s.rect(U(-4), V(14), 6 * k, 12 * k, "nhua")
    s.rect(U(-14), V(17.5), 10 * k, 5 * k, "pin")
    s.leader(U(-1), V(14), U(-1) + 6, V(14) - 56, "PG9: cáp 20 lõi từ tay cầm")
    s.rect(U(156), V(64), 4 * k, 9 * k, "nhua")
    s.leader(U(158), V(73), U(158) + 14, V(73) + 40, "lỗ sạc pin dự phòng")
    s.rect(U(30), V(5), 40 * k, 20 * k, "box")
    s.text(U(50), V(16), "dây nối, đầu cắm", "ts", "middle")
    s.dimh(U(0), U(158), V(90) + 34, "158", V(90), V(90))
    s.dimv(V(0), V(90), U(0) - 34, "90", U(0), U(0))
    s.leader(U(130), V(86), U(130) - 20, V(90) + 66, "ăng-ten ESP32 sát vách nhựa, xa tấm kim loại", anchor="end")

    # hình cắt dọc
    k2 = 2.7
    sx, sy = 620, 150
    A = lambda u: sx + u * k2
    B = lambda w: sy + (60 - w) * k2
    s.text(620, 120, "CẮT DỌC (CHỒNG LỚP)", "h")
    s.rect(A(0), B(60), 158 * k2, 60 * k2, "cut", rx=3)
    s.rect(A(2.5), B(57.5), 153 * k2, 55 * k2, "paper")
    s.rect(A(8), B(19), 140 * k2, 16 * k2, "pcbk", rx=5)
    s.text(A(78), B(10), "sạc dự phòng 10 000 mAh · 140×68×16", "ts", "middle", extra='style="fill:#e6eaec"')
    s.rect(A(4), B(24), 150 * k2, 2 * k2, "mica")
    for u in (10, 70, 148):
        s.rect(A(u), B(27.5), 3 * k2, 3.5 * k2, "dong")
    s.rect(A(6), B(29.1), 101.6 * k2, 1.6 * k2, "pcbt")
    s.rect(A(20), B(40), 60 * k2, 10.9 * k2, "nhua")
    s.rect(A(112), B(54), 43 * k2, 24 * k2, "pcbr")
    s.rect(A(118), B(54), 30 * k2, 20 * k2, "nhua")
    s.rect(A(84), B(40), 22 * k2, 11 * k2, "pcbk")
    s.rect(A(0) + 0.01, B(60), 158 * k2, 2.5 * k2, "pvc")
    s.rect(A(45), B(0), 70 * k2, 7 * k2, "kim", rx=3)
    s.text(A(80), B(0) + 30, "kẹp thắt lưng (mặt sau)", "ts", "middle")
    s.dimv(B(60), B(0), A(158) + 30, "60", A(158), A(158), side=1)
    s.dimv(B(19), B(3), A(0) - 22, "16", A(8), A(8))
    s.dimv(B(54), B(30), A(0) - 22, "27", A(112), A(112))
    s.text(A(133), B(60) - 8, "L298N + tản nhiệt", "ts", "middle")
    s.text(A(50), B(40) - 6, "đầu cắm Mega", "ts", "middle")
    s.text(A(95), B(40) - 6, "ESP32-CAM", "ts", "middle")

    gx, gy = 640, 384
    s.text(gx, gy, "GHI CHÚ", "hs")
    for i, n in enumerate([
        "1. Khối lượng hộp ước tính ≈ 420 g (hộp 110, sạc 210, mạch 77, dây 20).",
        "2. Một cáp USB: sạc → cổng USB-B của Mega. Cáp thứ hai cắt đầu: đường 5V riêng",
        "    cho L298N và ESP32-CAM, để mô-tơ và Wi-Fi không đi qua cầu chì 500 mA của Mega.",
        "3. Sạc dự phòng tự tắt khi dòng tải dưới khoảng 50–100 mA: tải ở đây ≈ 250 mA, không bị.",
    ]):
        s.text(gx, gy + 20 + i * 18, n, "ts")
    return s


# ---------------- TC-04: sơ đồ đấu nối ----------------

def tc04():
    s = Svg(1240, 900, 4)
    khung(s, "TC-04", "SƠ ĐỒ ĐẤU NỐI V1", "Chân theo firmware/tay_cam_mega · Wi-Fi theo docs/09")
    s.text(40, 48, "SƠ ĐỒ ĐẤU NỐI", "h")
    s.text(40, 64, "trái: trong tay cầm · giữa: cáp 20 lõi 1,2 m · phải: trong hộp thắt lưng", "ts")
    R5, RG = 34, 48           # hai thanh nguồn trong tay cầm
    CX1, CX2 = 360, 560       # cáp
    MX = 640                  # mép trái Mega
    y5, yg = 112, 128         # hàng 5V, GND đi qua cáp
    step, gap = 27, 16
    rows = {}
    y = 182
    for grp in (["SDA", "SCL"], ["J1X", "J1Y", "J1SW"], ["J2X", "J2Y", "J2SW"], ["CANLAI", "HOIAI", "CO"],
                ["LEDX", "LEDD"], ["M+", "M-"]):
        for n in grp:
            rows[n] = y
            y += step
        y += gap
    ybot = y
    loi = {"5V": 1, "GND": 2, "SDA": 3, "SCL": 5, "J1X": 7, "J1Y": 8, "J1SW": 9, "J2X": 10, "J2Y": 11, "J2SW": 12,
           "CANLAI": 13, "HOIAI": 14, "CO": 15, "LEDX": 16, "LEDD": 17, "M+": 18, "M-": 19}

    s.rect(22, 84, 315, ybot - 60, "box", rx=10)
    s.text(30, 100, "TRONG TAY CẦM", "hs")
    s.rect(610, 84, 610, ybot - 60, "box", rx=10)
    s.text(1210, 100, "TRONG HỘP THẮT LƯNG", "hs", "end")
    # cáp
    s.rect(CX1, 96, CX2 - CX1, ybot - 92, "pvc", rx=14, extra='style="fill-opacity:.55"')
    s.text((CX1 + CX2) / 2, ybot + 20, "Cáp 20 lõi chống nhiễu, 1,2 m", "tb", "middle")
    s.text((CX1 + CX2) / 2, ybot + 36, "19 lõi dùng, lõi 20 dự phòng", "ts", "middle")

    def wcol(n):
        return DAY[n] if DAY[n] else None

    def wire(pts, n, w=2.2):
        d = "M" + " L".join(f"{fmt(x)},{fmt(y)}" for x, y in pts)
        c = wcol(n)
        if c:
            s.path(d, "wire", f'stroke="{c}" stroke-width="{w}"')
        else:
            s.path(d, "wire gnd", f'stroke-width="{w}"')

    # thanh nguồn trong tay cầm
    wire([(R5, ybot - 30), (R5, y5), (MX, y5)], "5V", 2.6)
    wire([(RG, ybot - 30), (RG, yg), (MX, yg)], "GND", 2.6)
    s.text(R5 - 5, ybot - 22, "5V", "tb", "end")
    s.text(RG + 5, ybot - 22, "GND", "tb")
    for nm, yy in (("5V", y5), ("GND", yg)):
        s.text(CX1 + 8, yy - 4, f"lõi {loi[nm]}", "ts")
        s.text(CX2 - 6, yy - 4, nm, "tb", "end")

    def tap(x_rail, yy, n):
        wire([(x_rail, yy), (100, yy)], n)
        s.circle(x_rail, yy, 3, "dot")

    # hàng tín hiệu: từ tay cầm sang Mega/L298N
    mega_pin = {"SDA": "D20 SDA", "SCL": "D21 SCL", "J1X": "A0", "J1Y": "A1", "J1SW": "D22", "J2X": "A2",
                "J2Y": "A3", "J2SW": "D26", "CANLAI": "D24", "HOIAI": "D25", "CO": "D23", "LEDX": "D30",
                "LEDD": "D31"}
    for n, yy in rows.items():
        x_end = MX
        wire([(230, yy), (x_end, yy)], n)
        s.text(CX1 + 8, yy - 4, f"lõi {loi[n]}", "ts")
        lab = {"CANLAI": "CĂN LẠI", "HOIAI": "HỎI AI", "CO": "CÒ", "LEDX": "LED XANH", "LEDD": "LED ĐỎ"}.get(n, n)
        s.text(CX2 - 6, yy - 4, lab, "tb", "end")

    # MPU6050
    a, b = rows["SDA"], rows["SCL"]
    s.rect(100, a - 26, 130, b - a + 40, "mod", rx=4)
    s.rect(100, a - 26, 130, 18, "modh", rx=4)
    s.text(165, a - 13, "MPU6050 GY-521", "tb", "middle")
    s.text(224, a + 4, "SDA", "ts", "end")
    s.text(224, b + 4, "SCL", "ts", "end")
    s.text(106, a + 4, "VCC", "ts")
    s.text(106, b + 4, "GND", "ts")
    tap(R5, a, "5V")
    tap(RG, b, "GND")
    s.text(CX1 + 44, a - 4, "+ lõi 4 GND xoắn", "ts")
    s.text(CX1 + 44, b - 4, "+ lõi 6 GND xoắn", "ts")
    # 2 joystick
    for pre, nm in (("J1", "JOYSTICK 1 · chỉnh tinh"), ("J2", "JOYSTICK 2 · di chuyển")):
        y1, y3 = rows[pre + "X"], rows[pre + "SW"]
        s.rect(100, y1 - 26, 130, y3 - y1 + 38, "mod", rx=4)
        s.rect(100, y1 - 26, 130, 18, "modh", rx=4)
        s.text(165, y1 - 13, "KY-023 " + pre, "tb", "middle")
        for lab, yy in (("VRx", y1), ("VRy", rows[pre + "Y"]), ("SW", y3)):
            s.text(224, yy + 4, lab, "ts", "end")
        s.text(106, y1 + 4, "+5V", "ts")
        s.text(106, y3 + 4, "GND", "ts")
        tap(R5, y1, "5V")
        tap(RG, y3, "GND")
    # 3 nút: một đầu GND, một đầu về chân (INPUT_PULLUP)
    for n, lab in (("CANLAI", "nút 6×6"), ("HOIAI", "nút 6×6"), ("CO", "KW11: COM–NO")):
        yy = rows[n]
        wire([(RG, yy), (120, yy)], "GND")
        s.circle(RG, yy, 3, "dot")
        s.circle(124, yy, 3.2, "ln")
        s.circle(160, yy, 3.2, "ln")
        s.line(126, yy - 2, 160, yy - 13, "ln")
        s.text(170, yy - 6, lab, "ts")
        wire([(164, yy), (230, yy)], n)
    # LED + điện trở 220
    for n in ("LEDX", "LEDD"):
        yy = rows[n]
        wire([(RG, yy), (110, yy)], "GND")
        s.circle(RG, yy, 3, "dot")
        s.path(f"M{fmt(130)},{fmt(yy-8)} L{fmt(130)},{fmt(yy+8)} L{fmt(116)},{fmt(yy)} Z",
               "ledx" if n == "LEDX" else "ledd")
        s.line(116, yy - 8, 116, yy + 8, "ln")
        s.line(110, yy, 116, yy, "ln")
        s.line(130, yy, 150, yy, "ln")
        s.rect(150, yy - 5, 34, 10, "mod", rx=2)
        s.text(167, yy - 9, "220 Ω", "ts", "middle")
        s.line(184, yy, 230, yy, "ln")
    # mô-tơ
    ym1, ym2 = rows["M+"], rows["M-"]
    s.circle(165, (ym1 + ym2) / 2, 20, "mod")
    s.text(165, (ym1 + ym2) / 2 + 5, "M", "tb", "middle")
    s.text(165, ym2 + 34, "mô-tơ rung 130, 3 V", "ts", "middle")
    wire([(185, ym1), (230, ym1)], "M+")
    wire([(185, ym2), (230, ym2)], "M-")
    s.text(192, ym1 - 5, "+", "tb")
    s.text(192, ym2 + 14, "−", "tb")

    # Mega
    mtop, mbot = 92, rows["LEDD"] + 22
    s.rect(MX, mtop, 210, mbot - mtop, "mod", rx=5)
    s.rect(MX, mtop, 210, 22, "modh", rx=5)
    s.text(MX + 105, mtop + 16, "ARDUINO MEGA 2560", "tb", "middle")
    for nm, yy in (("5V", y5), ("GND", yg)):
        s.text(MX + 8, yy + 4, nm, "ts")
        s.circle(MX, yy, 3, "dot")
    for n, p in mega_pin.items():
        s.text(MX + 8, rows[n] + 4, p, "ts")
        s.circle(MX, rows[n], 3, "dot")
    s.text(MX + 105, mbot - 30, "nút: INPUT_PULLUP", "ts", "middle")
    s.text(MX + 105, mbot - 14, "I2C hạ 100 kHz cho cáp dài", "ts", "middle")
    # chân bên phải Mega
    rp = {"USB": 112, "GND": 180, "D18 TX1": 300, "D19 RX1": 330, "D5 PWM": rows["LEDX"] - 44,
          "D6": rows["LEDX"] - 22, "D7": rows["LEDX"]}
    for p, yy in rp.items():
        s.text(MX + 202, yy + 4, p, "ts", "end")
        s.circle(MX + 210, yy, 3, "dot")
    # L298N: OUT1/OUT2 bên trái, ENA/IN1/IN2 vào từ cạnh trên, nguồn bên phải
    lx, ltop = 900, rows["M+"] - 62
    lw, lh = 180, rows["M-"] - ltop + 24
    s.rect(lx, ltop, lw, lh, "mod", rx=5)
    s.rect(lx, ltop, lw, 22, "modh", rx=5)
    s.text(lx + lw / 2, ltop + 16, "L298N", "tb", "middle")
    s.text(lx + lw / 2, ltop + lh + 16, "tháo jumper ENA và 5V-EN", "ts", "middle")
    for p, xx, yy in (("ENA", 1040, rp["D5 PWM"]), ("IN1", 1010, rp["D6"]), ("IN2", 980, rp["D7"])):
        wire([(MX + 210, yy), (xx, yy), (xx, ltop)], "CO")
        s.circle(xx, ltop, 3, "dot")
        s.text(xx, ltop + 36, p, "ts", "middle")
    # mô-tơ đi dưới Mega vào OUT1/OUT2
    for n in ("M+", "M-"):
        wire([(CX2, rows[n]), (lx, rows[n])], n)
        s.circle(lx, rows[n], 3, "dot")
    s.text(lx + 8, rows["M+"] + 4, "OUT1", "ts")
    s.text(lx + 8, rows["M-"] + 4, "OUT2", "ts")
    lp = {"+12V (Vs)": ltop + 50, "+5V logic": ltop + 72, "GND": ltop + 94}
    for p, yy in lp.items():
        s.text(lx + lw - 8, yy + 4, p, "ts", "end")
        s.circle(lx + lw, yy, 3, "dot")
    # ESP32-CAM: tín hiệu bên trái, nguồn bên phải
    ex, ey = 960, 262
    s.rect(ex, ey, 180, 128, "mod", rx=5)
    s.rect(ex, ey, 180, 22, "modh", rx=5)
    s.text(ex + 90, ey + 16, "ESP32-CAM (AI Thinker)", "tb", "middle")
    yA, yB = rp["D18 TX1"], ey + 108
    for p, yy in (("IO13 (RX)", yA), ("IO14 (TX)", yB)):
        s.text(ex + 8, yy + 4, p, "ts")
        s.circle(ex, yy, 3, "dot")
    for p, yy in (("5V", ey + 50), ("GND", ey + 72)):
        s.text(ex + 172, yy + 4, p, "ts", "end")
        s.circle(ex + 180, yy, 3, "dot")
    s.text(ex + 172, ey + 98, "không dùng", "ts", "end")
    s.text(ex + 172, ey + 112, "IO0, 2, 4, 12, 16", "ts", "end")
    s.text(ex + 90, ey + 146, "Wi-Fi UDP 4210 → laptop", "tb", "middle")
    # chia áp D18 (5 V) -> IO13 (3,3 V)
    wire([(MX + 210, yA), (890, yA)], "J2SW")
    s.rect(890, yA - 6, 30, 12, "mod", rx=2)
    s.text(905, yA - 10, "1 kΩ", "ts", "middle")
    wire([(920, yA), (ex, yA)], "J2SW")
    s.circle(935, yA, 3, "dot")
    s.line(935, yA, 935, yA + 12, "ln")
    s.rect(929, yA + 12, 12, 26, "mod", rx=2)
    s.text(925, yA + 32, "2 kΩ", "ts", "end")
    s.line(935, yA + 38, 935, yA + 48, "ln")
    s.line(927, yA + 48, 943, yA + 48, "ln")
    s.line(930, yA + 52, 940, yA + 52, "ln")
    wire([(ex, yB), (880, yB), (880, rp["D19 RX1"]), (MX + 210, rp["D19 RX1"])], "J1SW")
    s.text(856, 319, "115 200 baud", "ts")
    # sạc dự phòng
    px_, py_ = 940, 110
    s.rect(px_, py_, 200, 90, "mod", rx=8)
    s.rect(px_, py_, 200, 22, "modh", rx=8)
    s.text(px_ + 100, py_ + 16, "SẠC DỰ PHÒNG 5 V · 2 A", "tb", "middle")
    s.text(px_ + 8, py_ + 44, "USB 1", "ts")
    s.text(px_ + 8, py_ + 74, "USB 2 (cáp cắt đầu)", "ts")
    s.circle(px_, py_ + 40, 3, "dot")
    s.circle(px_, py_ + 70, 3, "dot")
    wire([(px_, py_ + 40), (870, py_ + 40), (870, rp["USB"]), (MX + 210, rp["USB"])], "5V", 2.6)
    s.text(876, 130, "→ USB-B", "ts")
    # đường 5V riêng
    rail = 1180
    wire([(px_, py_ + 70), (920, py_ + 70), (920, 230), (rail, 230), (rail, lp["+12V (Vs)"]), (lx + lw, lp["+12V (Vs)"])], "5V", 2.6)
    wire([(rail, lp["+5V logic"]), (lx + lw, lp["+5V logic"])], "5V", 2.6)
    s.circle(rail, lp["+5V logic"], 3, "dot")
    wire([(rail, ey + 50), (ex + 180, ey + 50)], "5V", 2.6)
    s.circle(rail, ey + 50, 3, "dot")
    s.text(rail - 6, 246, "đường 5V riêng (không qua Mega)", "ts", "end")
    # GND chung
    gr = 1196
    wire([(gr, 206), (gr, lp["GND"]), (lx + lw, lp["GND"])], "GND", 2.4)
    wire([(gr, ey + 72), (ex + 180, ey + 72)], "GND", 2.4)
    s.circle(gr, ey + 72, 3, "dot")
    wire([(gr, 206), (px_ + 200, 206), (px_ + 200, py_ + 90)], "GND", 2.4)
    wire([(MX + 210, rp["GND"]), (930, rp["GND"]), (930, 206), (gr - 1, 206)], "GND", 2.4)
    s.text(gr - 6, 220, "GND chung", "ts", "end")

    # cảnh báo
    wy = ybot + 60
    s.rect(30, wy, 640, 104, "warnbox", rx=6)
    s.text(44, wy + 22, "PHÁT HIỆN KHI VẼ (đưa vào đánh giá bước 4)", "tw")
    for i, t in enumerate([
        "• Cần 19 lõi; cáp LAN trong docs/03 chỉ có 8 lõi → phải dùng cáp 20 lõi hoặc 2 cáp LAN + gộp nút.",
        "• D22 vừa là nút joystick 1 (tay_cam_mega) vừa là chân phát Wi-Fi (sketch) → dời Wi-Fi sang TX1 (D18).",
        "• 9 600 baud không đủ cho gói 29 byte × 100 lần/giây (cần ≥ 29 000 bit/s) → TX1 115 200 + chia áp.",
        "• Mô-tơ + Wi-Fi có lúc vượt 500 mA của cổng USB Mega → đường 5V riêng như hình.",
    ]):
        s.text(44, wy + 44 + i * 16, t, "ts")
    return s


# ---------------- TC-05: cách cầm, đeo ----------------

def tc05():
    s = Svg(1180, 900, 5)
    khung(s, "TC-05", "CÁCH CẦM VÀ ĐEO TAY CẦM V1", "Tay phải; người thuận tay trái lắp gương")
    # (a) bàn tay cầm, nhìn từ phía phải
    k = 2.1
    ox, oy = 395, 230
    X = lambda x: ox + x * k
    Y = lambda z: oy - z * k
    s.text(40, 48, "A · BÀN TAY CẦM TAY CẦM", "h")
    s.text(40, 64, "nhìn từ phía phải (thấy mu bàn tay phải)", "ts")
    ve_canh_ben(s, X, Y, k, an=False, day_cap=False)
    # cáp dọc cẳng tay
    s.path(f"M{fmt(X(-17))},{fmt(Y(0))} C{fmt(X(-45))},{fmt(Y(0))} {fmt(X(-60))},{fmt(Y(-22))} {fmt(X(-100))},{fmt(Y(-28))} "
           f"S{fmt(X(-160))},{fmt(Y(-50))} {fmt(X(-170))},{fmt(Y(-64))}", "cable", 'stroke-width="7"')
    hand = [(12, 23), (55, 31), (92, 29), (121, 16), (133, -2), (129, -23), (108, -37), (72, -41), (35, -38), (6, -30),
            (-40, -42), (-90, -58), (-150, -82), (-160, -40), (-110, -16), (-50, 6)]
    # ngón trỏ và ngón cái: nét dày bo tròn
    s.add('<g opacity=".85">')
    for pts, w in (([(X(114), Y(3)), (X(124), Y(-9)), (X(131), Y(-18))], 15 * k),
                   ([(X(56), Y(28)), (X(96), Y(37)), (X(133), Y(39))], 18 * k)):
        d = "M" + " L".join(f"{fmt(x)},{fmt(y)}" for x, y in pts)
        s.path(d, "", f'fill="none" stroke="var(--hand-ln,#9c5f37)" stroke-width="{fmt(w+2.6)}" stroke-linecap="round" stroke-linejoin="round"')
        s.path(d, "", f'fill="none" stroke="var(--hand,#e9b98f)" stroke-width="{fmt(w)}" stroke-linecap="round" stroke-linejoin="round"')
    s.add("</g>")
    # tô lại phần thân bàn tay (chuyển hệ mm sang px)
    s.add('<g opacity=".55">')
    s.path(catmull([(X(x), Y(z)) for x, z in hand]), "hand")
    s.add("</g>")
    for x1, z1, x2, z2 in ((100, 22, 108, -32), (82, 26, 84, -38), (62, 28, 60, -38)):
        s.path(f"M{fmt(X(x1))},{fmt(Y(z1))} Q{fmt(X((x1+x2)/2+8))},{fmt(Y((z1+z2)/2))} {fmt(X(x2))},{fmt(Y(z2))}", "handln")
    # dây đeo cổ tay
    s.path(f"M{fmt(X(8))},{fmt(Y(-13))} C{fmt(X(-4))},{fmt(Y(-20))} {fmt(X(-14))},{fmt(Y(-28))} {fmt(X(-22))},{fmt(Y(-36))}", "ln", 'style="stroke-width:2.4"')
    s.add(f'<ellipse cx="{fmt(X(-24))}" cy="{fmt(Y(-14))}" rx="{fmt(5*k)}" ry="{fmt(31*k)}" transform="rotate(-18 {fmt(X(-24))} {fmt(Y(-14))})" '
          f'class="ln" style="stroke-width:2.4;fill:none"/>')
    # băng dán trên cẳng tay
    s.add(f'<rect x="{fmt(X(-104))}" y="{fmt(Y(-12))}" width="{fmt(12*k)}" height="{fmt(64*k)}" rx="4" class="mica" '
          f'transform="rotate(-22 {fmt(X(-98))} {fmt(Y(-44))})"/>')
    s.leader(X(133), Y(46), X(160), Y(64), "Ngón cái: cần 2 (di chuyển)", sub="lùi về cần 1 và 2 nút phía sau")
    s.leader(X(131), Y(-20), X(160), Y(-48), "Ngón trỏ: bóp cò KW11", sub="hành trình ≈ 3 mm")
    s.leader(X(80), Y(-38), X(108), Y(-74), "Ngón giữa, áp út, út ôm thân Ø34")
    s.leader(X(-24), Y(-44), X(0), Y(-80), "Dây đeo cổ tay Ø3, chống rơi")
    s.leader(X(-98), Y(-30), X(-80), Y(-96), "Băng dán gai 25 mm giữ cáp", sub="cách cổ tay ≈ 10 cm")
    s.leader(X(-168), Y(-62), X(-170), Y(30), "cáp → khuỷu tay → hộp thắt lưng", anchor="start")

    # (b) người ngồi tại trạm
    s.text(40, 470, "B · NGỒI TẠI TRẠM THÍ NGHIỆM", "h")
    s.text(40, 486, "hộp đeo bên hông phải, cáp chùng ở khuỷu", "ts")
    g = 2.0   # px / cm
    bx, by = 80, 780
    P = lambda x: bx + x * g
    Q = lambda z: by - z * g
    s.rect(P(10), Q(46), 50 * g, 4 * g, "pvc2")
    s.line(P(14), Q(42), P(14), Q(0), "ln", 'style="stroke-width:3"')
    s.line(P(56), Q(42), P(56), Q(0), "ln", 'style="stroke-width:3"')
    s.line(P(12), Q(50), P(4), Q(95), "ln", 'style="stroke-width:4"')
    s.rect(P(115), Q(75), 120 * g, 4 * g, "pvc2")
    s.line(P(125), Q(71), P(125), Q(0), "ln", 'style="stroke-width:3"')
    s.line(P(225), Q(71), P(225), Q(0), "ln", 'style="stroke-width:3"')
    for a, b2, w in (((30, 52), (75, 52), 16), ((75, 52), (78, 4), 13), ((78, 4), (92, 2), 9),
                     ((28, 52), (38, 112), 34), ((40, 118), (48, 82), 11), ((48, 82), (98, 88), 10)):
        s.line(P(a[0]), Q(a[1]), P(b2[0]), Q(b2[1]), "fig", f'stroke-width="{fmt(w*g)}"')
    s.circle(P(44), Q(132), 11 * g, "figf")
    s.rect(P(50), Q(138), 14 * g, 9 * g, "pcbk", rx=4)
    s.line(P(42), Q(138), P(52), Q(136), "ln", 'style="stroke-width:2"')
    s.text(P(68), Q(134), "kính VR Box", "ts")
    cx0, cz0 = 98, 88
    s.add(f'<g transform="rotate(-6 {fmt(P(cx0))} {fmt(Q(cz0))})">')
    s.rect(P(cx0 - 4), Q(cz0 + 1.8), 19 * g, 3.6 * g, "pvc", rx=4)
    s.add("</g>")
    s.circle(P(cx0), Q(cz0), 5 * g, "hand")
    s.rect(P(25), Q(62), 9 * g, 14 * g, "pcbt", rx=3)
    s.text(P(29.5), Q(48) + 14, "hộp", "ts", "middle")
    s.path(f"M{fmt(P(cx0-4))},{fmt(Q(cz0))} C{fmt(P(80))},{fmt(Q(86))} {fmt(P(60))},{fmt(Q(84))} {fmt(P(48))},{fmt(Q(82))} "
           f"C{fmt(P(44))},{fmt(Q(70))} {fmt(P(52))},{fmt(Q(60))} {fmt(P(34))},{fmt(Q(56))}", "cable", 'stroke-width="3.5"')
    s.rect(P(70) - 3, Q(88) - 9, 6, 18, "mica")
    s.leader(P(72), Q(86), P(140), Q(126), "băng dán giữa cẳng tay")
    s.leader(P(46), Q(70), P(132), Q(55), "cáp chùng ≥ 15 cm ở khuỷu")
    s.leader(P(30), Q(56), P(132), Q(38), "hộp 158×90×60 kẹp thắt lưng")
    s.leader(P(102), Q(90), P(150), Q(108), "tay cầm")
    s.text(40, 812, "Tay ↔ hộp ≈ 65 cm khi ngồi; cáp 1,2 m đủ để đứng dậy và với tay.", "ts")

    # (c) vùng với của ngón cái
    s.text(620, 470, "C · VÙNG VỚI THOẢI MÁI CỦA NGÓN CÁI", "h")
    s.text(620, 486, "nhìn từ trên; ước lượng cho tay dài 177 mm, cần đo lại bằng mô hình giấy", "ts")
    k3 = 2.4
    ox3, oy3 = 650, 650
    X3 = lambda x: ox3 + x * k3
    Y3 = lambda y: oy3 - y * k3
    ve_tren(s, X3, Y3, k3, an=False)
    gx, gy = 55, -25      # gốc ngón cái (khớp bàn – cổ tay), phía phải tay cầm
    r1, r2 = 50, 80
    def arcpt(r, a):
        return X3(gx + r * math.cos(a)), Y3(gy + r * math.sin(a))
    a0, a1 = math.radians(-20), math.radians(75)
    p1, p2, p3, p4 = arcpt(r1, a0), arcpt(r1, a1), arcpt(r2, a1), arcpt(r2, a0)
    s.path(f"M{fmt(p1[0])},{fmt(p1[1])} A{fmt(r1*k3)},{fmt(r1*k3)} 0 0 0 {fmt(p2[0])},{fmt(p2[1])} "
           f"L{fmt(p3[0])},{fmt(p3[1])} A{fmt(r2*k3)},{fmt(r2*k3)} 0 0 1 {fmt(p4[0])},{fmt(p4[1])} Z", "zone")
    s.circle(X3(gx), Y3(gy), 5, "dot")
    s.text(X3(gx), Y3(gy) + 20, "gốc ngón cái", "ts", "middle")
    s.line(X3(gx), Y3(gy), *arcpt(r2, math.radians(-12)), "thin")
    s.text(X3(gx), Y3(gy) + 36, "vùng cách gốc 50–80 mm", "zonet", "middle")
    s.leader(X3(NUT_X), Y3(6), X3(NUT_X) - 10, Y3(6) - 70, "2 nút: quá gần, phải gập mạnh", "tw", anchor="end")
    s.leader(X3(J1_X), Y3(0), X3(J1_X) + 4, Y3(0) - 92, "cần 1: trong vùng")
    s.leader(X3(J2_X), Y3(-8), X3(J2_X) + 10, Y3(-8) + 92, "cần 2: sát mép ngoài", "tw", sub="(cần dùng nhiều nhất)")
    return s


def tat_ca():
    return [("TC-01 Ba hình chiếu", tc01()), ("TC-02 Bố trí bên trong", tc02()), ("TC-03 Hộp thắt lưng", tc03()),
            ("TC-04 Sơ đồ đấu nối", tc04()), ("TC-05 Cách cầm và đeo", tc05())]


if __name__ == "__main__":
    import sys
    sys.stdout.reconfigure(encoding="utf-8")
    os.makedirs(THU_MUC_RA, exist_ok=True)
    for ten, s in tat_ca():
        duong = os.path.join(THU_MUC_RA, ten.split(" ")[0] + ".svg")
        with open(duong, "w", encoding="utf-8") as f:
            f.write(s.svg(standalone=True))
        print(duong, f"{s.w}x{s.h}")
    print(f"Khối lượng tay cầm ≈ {TONG_G:.1f} g, trọng tâm x = {TRONG_TAM_X:.1f} mm")
