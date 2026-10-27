"""Ghép bản vẽ 2D và số liệu thành một trang HTML (dùng cho Artifact và để mở offline).

Chạy:  python tools/ve_tay_cam/trang_html.py <đường dẫn file .html ra>
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ve_2d  # noqa: E402

BAN_TAY = {"HL": (177, 12), "HB": (79.2, 6.9), "HD": (28.2, 3.0)}

LINH_KIEN = [  # tên, kích thước, khối lượng, nguồn
    ("Joystick KY-023", "bo 34 × 26; cần cao 32 kể cả mũ", "≈ 10 g", "datasheet Joy-IT KY-023 (bo mạch); chiều cao đo theo mẫu phổ biến"),
    ("MPU6050 GY-521", "21 × 16 × 3", "≈ 2,5 g", "module phổ biến, cần đo lại"),
    ("Arduino Mega 2560", "101,6 × 53,3", "37 g", "kích thước chuẩn của Arduino"),
    ("ESP32-CAM AI Thinker", "40,5 × 27 × 4,5 (chưa tính camera)", "10 g", "thông số Ai-Thinker"),
    ("Mạch L298N", "43 × 43 × 27 (kể cả tản nhiệt)", "≈ 30 g", "module phổ biến, cần đo lại"),
    ("Công tắc hành trình KW11", "20 × 10 × 6,4, cần dài ≈ 22", "2 g", "loại phổ biến, cần đo lại"),
    ("Mô-tơ 130 + ốc lệch tâm", "thân dài 25, mặt cắt 20 × 15", "≈ 20 g", "loại phổ biến, cần đo lại"),
    ("Ống PVC phi 34 Bình Minh", "Ø 33,5 thực đo, dày 2,05", "0,28 g/mm", "phiếu thử nghiệm Nhựa Bình Minh (TCVN 6145:2007)"),
    ("Sạc dự phòng 10 000 mAh", "≈ 140 × 68 × 16", "≈ 210 g", "tùy mẫu, đo cái nhóm có"),
]

GIA = [  # tên, số lượng, đơn giá (đồng), nguồn ("hshop" hoặc "ước tính" hoặc "đã có")
    ("Module joystick X-Y kèm nút", 2, 20000, "hshop"),
    ("Cảm biến MPU6050 GY-521", 1, 85000, "hshop"),
    ("Mạch điều khiển động cơ L298", 1, 45000, "hshop"),
    ("Mô-tơ 130 + ốc M3 lệch tâm", 1, 10000, "ước tính"),
    ("Công tắc hành trình KW11 có cần", 1, 5000, "ước tính"),
    ("Nút nhấn 6×6 + mũ, LED 5 mm, điện trở", 1, 7000, "ước tính"),
    ("Ống PVC Ø34 (đoạn 20 cm) + 2 nắp bịt", 1, 20000, "ước tính"),
    ("Mica 2 mm + 4 trụ đồng, ốc M2", 1, 10000, "ước tính"),
    ("Phíp đục lỗ", 1, 5000, "ước tính"),
    ("Cáp 20 lõi chống nhiễu 1,5 m", 1, 35000, "ước tính"),
    ("Ốc siết cáp PG9", 2, 3000, "ước tính"),
    ("Hộp nhựa 158×90×60", 1, 35000, "ước tính"),
    ("Dây đeo cổ tay, băng dán gai, kẹp thắt lưng", 1, 15000, "ước tính"),
    ("Arduino Mega 2560", 1, 0, "đã có"),
    ("ESP32-CAM", 1, 0, "đã có"),
    ("Sạc dự phòng 5 V", 1, 0, "đã có"),
]

DAU_NOI = [  # lõi, tín hiệu, đầu tay cầm, đầu hộp, ghi chú
    ("1", "5V", "thanh 5V → VCC GY-521, +5V hai KY-023", "Mega 5V", "nguồn cho cảm biến và joystick"),
    ("2", "GND", "thanh GND (nút, LED, joystick)", "Mega GND", ""),
    ("3 + 4", "SDA + GND", "GY-521 SDA", "D20 SDA", "lõi 3 xoắn với lõi 4 (GND); I2C hạ 100 kHz"),
    ("5 + 6", "SCL + GND", "GY-521 SCL", "D21 SCL", "lõi 5 xoắn với lõi 6 (GND)"),
    ("7", "J1X", "KY-023 J1 VRx", "A0", "joystick 1: chỉnh tinh"),
    ("8", "J1Y", "KY-023 J1 VRy", "A1", ""),
    ("9", "J1SW", "KY-023 J1 SW", "D22", "INPUT_PULLUP, nhấn = GND"),
    ("10", "J2X", "KY-023 J2 VRx", "A2", "joystick 2: di chuyển"),
    ("11", "J2Y", "KY-023 J2 VRy", "A3", ""),
    ("12", "J2SW", "KY-023 J2 SW", "D26", "INPUT_PULLUP"),
    ("13", "CĂN LẠI", "nút 6×6 → GND", "D24", "INPUT_PULLUP"),
    ("14", "HỎI AI", "nút 6×6 → GND", "D25", "INPUT_PULLUP"),
    ("15", "CÒ", "KW11 chân NO; COM → GND", "D23", "INPUT_PULLUP"),
    ("16", "LED XANH", "220 Ω → LED xanh → GND", "D30", "≈ 12 mA"),
    ("17", "LED ĐỎ", "220 Ω → LED đỏ → GND", "D31", "≈ 14 mA"),
    ("18", "M+", "mô-tơ +", "L298N OUT1", "ENA = D5 (PWM), IN1 = D6, IN2 = D7"),
    ("19", "M−", "mô-tơ −", "L298N OUT2", ""),
    ("20", "–", "để trống", "–", "dự phòng"),
]
TRONG_HOP = [
    ("Mega D18 (TX1)", "1 kΩ → ESP32-CAM IO13; IO13 → 2 kΩ → GND", "chia 5 V xuống 3,3 V; 115 200 baud"),
    ("ESP32-CAM IO14", "Mega D19 (RX1)", "3,3 V vẫn đủ mức cao cho Mega (≥ 3,0 V)"),
    ("Mega D5, D6, D7", "L298N ENA, IN1, IN2", "tháo jumper ENA"),
    ("Sạc USB 1", "Mega USB-B", "nguồn cho Mega và phía tay cầm"),
    ("Sạc USB 2 (cáp cắt đầu)", "L298N +12V (Vs), +5V logic; ESP32-CAM 5V", "tháo jumper 5V-EN của L298N"),
    ("GND", "Mega, L298N, ESP32-CAM, sạc", "bắt buộc nối chung"),
]

PHAT_HIEN = [
    ("Cáp không đủ lõi", "Cần 19 lõi, mà cáp LAN trong docs/03 chỉ có 8.",
     "Dùng cáp 20 lõi, hoặc 2 cáp LAN kèm gộp nút bằng thang điện trở. Bước 5 sẽ so với phương án bỏ cáp."),
    ("Trùng chân D22", "firmware tay_cam_mega dùng D22 cho nút joystick 1, còn sketch Wi-Fi dùng D22 làm chân phát.",
     "Dời đường Wi-Fi sang TX1 (D18)."),
    ("9 600 baud không đủ", "Gói 29 byte × 100 lần/giây cần ít nhất 29 000 bit/s.",
     "Chạy TX1 ở 115 200 baud, có chia áp 1 kΩ / 2 kΩ."),
    ("Nguồn qua Mega quá tải", "Mô-tơ lúc khởi động và Wi-Fi lúc phát có thể vượt 500 mA của cổng USB Mega.",
     "Kéo đường 5V riêng từ sạc cho L298N và ESP32-CAM."),
    ("Ngón cái phải với xa", "Hai cần và hai nút trải dài 90 mm. Hai nút nằm quá gần gốc ngón cái, còn cần 2 (dùng nhiều nhất) ở sát mép vùng với.",
     "Cần kiểm chứng bằng mô hình giấy, rồi sắp xếp lại ở bước 5."),
    ("Đèn LED không có người thấy", "Người đeo kính VR không nhìn thấy đèn ở đầu tay cầm.",
     "Đèn chỉ có ích cho người quan sát. Có thể bỏ, để dành chân cho việc khác."),
]

NGUON = [
    ("Số đo bàn tay người Việt (Imrhan, Nguyen & Nguyen 1993), trích lại trong Zheng và cs., AHFE 2018, bảng 4",
     "https://openaccess-api.cms-conferences.org/articles/download/978-1-4951-2105-0_32"),
    ("Kong & Lowe 2005, đường kính tay nắm tối ưu = 19,7% chiều dài bàn tay (bản ghi NIOSH)",
     "https://stacks.cdc.gov/view/cdc/197408"),
    ("Phiếu thử nghiệm ống PVC-U Bình Minh DN34 × 2,0 mm PN12",
     "https://binhminhplastic.com.vn/Data/Sites/1/Product/8889/34x2,0mm%20PN12.pdf"),
    ("Datasheet joystick KY-023 (Joy-IT)",
     "https://asset.conrad.com/media10/add/160267/c1/-/en/001707629DS01/datasheet-1707629-joy-it-ky023jm-1-pcs-compatible-with-development-kits-raspberry-pi-pcduino-banana-pi-arduino.pdf"),
    ("Hshop: module joystick X-Y", "https://hshop.vn/mach-joystick"),
    ("Hshop: MPU6050 GY-521", "https://hshop.vn/cam-bien-6-dof-bac-tu-do-gy-521-mpu6050"),
    ("Hshop: mạch L298", "https://hshop.vn/mach-dieu-khien-dong-co-dc-l298"),
    ("Hshop: ESP32-CAM (để so giá)", "https://hshop.vn/kit-rf-thu-phat-wifi-ble-esp32-cam"),
]


def dong(v):
    return f"{v:,.0f}".replace(",", ".") + " đ"


def bang(head, rows, cls=""):
    th = "".join(f"<th>{h}</th>" for h in head)
    tr = "".join("<tr>" + "".join(f"<td>{c}</td>" for c in r) + "</tr>" for r in rows)
    return f'<div class="tbl {cls}"><table><thead><tr>{th}</tr></thead><tbody>{tr}</tbody></table></div>'


def trang():
    hl, sd = BAN_TAY["HL"]
    p5, p95 = hl - 1.645 * sd, hl + 1.645 * sd
    hb95 = BAN_TAY["HB"][0] + 1.645 * BAN_TAY["HB"][1]
    tay_rows = [
        ("Chiều dài bàn tay", f"{hl} ± {sd}", f"{p5:.0f} – {p95:.0f}"),
        ("Chiều rộng bàn tay", "79,2 ± 6,9", f"{79.2 - 1.645*6.9:.0f} – {hb95:.0f}"),
        ("Độ dày bàn tay", "28,2 ± 3,0", f"{28.2-1.645*3:.0f} – {28.2+1.645*3:.0f}"),
    ]
    duong_kinh = [(f"P5 · tay {p5:.0f} mm", 0.197 * p5), (f"P50 · tay {hl} mm", 0.197 * hl),
                  (f"P95 · tay {p95:.0f} mm", 0.197 * p95)]
    # thước đường kính: 25–45 mm
    def pos(v):
        return (v - 25) / 20 * 100
    bars = "".join(
        f'<div class="tick" style="left:{pos(v):.1f}%"><span class="tv">{v:.1f}</span><span class="tl">{lab}</span></div>'
        for lab, v in duong_kinh)
    thuoc = (f'<div class="ruler" role="img" aria-label="Đường kính tay nắm tối ưu từ {duong_kinh[0][1]:.0f} đến {duong_kinh[2][1]:.0f} mm, ống PVC 34 mm">'
             f'<div class="band" style="left:{pos(duong_kinh[0][1]):.1f}%;width:{pos(duong_kinh[2][1])-pos(duong_kinh[0][1]):.1f}%"></div>'
             f'<div class="pipe" style="left:{pos(33.5):.1f}%"><span>ống PVC Ø33,5</span></div>{bars}'
             + "".join(f'<div class="mm" style="left:{pos(v):.1f}%">{v}</div>' for v in range(25, 46, 5)) + "</div>")

    tong = sum(sl * dg for _, sl, dg, _ in GIA)
    gia_rows = [(t, str(sl), dong(dg) if dg else "–", dong(sl * dg) if dg else "–",
                 f'<span class="pill {"p-hs" if n == "hshop" else ("p-co" if n == "đã có" else "p-ut")}">{n}</span>')
                for t, sl, dg, n in GIA]
    gia_rows.append(("<b>Tổng phải mua thêm</b>", "", "", f"<b>{dong(tong)}</b>", ""))

    sheets = []
    for ten, s in ve_2d.tat_ca():
        so, mo_ta = ten.split(" ", 1)
        sheets.append(f'<figure class="sheet-wrap" id="{so.lower()}"><figcaption><span class="so">{so}</span>{mo_ta}'
                      f'<span class="png">PNG: docs/anh-tay-cam/{so}.png</span></figcaption>'
                      f'<div class="scroll">{s.svg(standalone=False)}</div></figure>')

    ph = "".join(f'<li><b>{a}.</b> {b} <span class="fix">→ {c}</span></li>' for a, b, c in PHAT_HIEN)
    src = "".join(f'<li><a href="{u}" target="_blank" rel="noopener">{t}</a></li>' for t, u in NGUON)

    return f"""<title>Bản vẽ tay cầm VR</title>
<meta name="claude:viewport-fit-sides" content="cover">
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Barlow+Condensed:wght@600;700&family=Be+Vietnam+Pro:wght@400;500;600;700&family=JetBrains+Mono:wght@500;700&display=swap">
<style>
/* bố cục: một cột tài liệu kỹ thuật, bản vẽ chiếm trọn bề ngang, khung tên kiểu TCVN ở đầu trang */
:root{{
  --bg:#e9eeec; --sheet:#fbfcfa; --ink:#18272c; --ink-2:#5d6e72; --rule:#c8d2d0;
  --dim:#0a6b8a; --warn:#b4471a; --ok:#2f7d4f; --pvc:#dde5e3; --pvc2:#c9d4d1;
  --mica:rgba(110,180,210,.35); --hand:#e9b98f; --hand-ln:#9c5f37; --fig:#a9bcc0; --gnd:#2b3135;
  --f-disp:'Barlow Condensed','Arial Narrow',sans-serif; --f-body:'Be Vietnam Pro','Segoe UI',sans-serif;
  --f-mono:'JetBrains Mono',ui-monospace,monospace;
}}
@media (prefers-color-scheme: dark){{ :root:not([data-theme="light"]){{
  --bg:#0d1517; --sheet:#132023; --ink:#dbe6e4; --ink-2:#93a5a8; --rule:#2a3b3e;
  --dim:#5cc3e0; --warn:#f08a5d; --ok:#6cc58f; --pvc:#2a3a3e; --pvc2:#36494d;
  --mica:rgba(110,180,210,.25); --hand:#b9835a; --hand-ln:#e8b98f; --fig:#4b6166; --gnd:#c9d1d3; color-scheme:dark }} }}
:root[data-theme="dark"]{{
  --bg:#0d1517; --sheet:#132023; --ink:#dbe6e4; --ink-2:#93a5a8; --rule:#2a3b3e;
  --dim:#5cc3e0; --warn:#f08a5d; --ok:#6cc58f; --pvc:#2a3a3e; --pvc2:#36494d;
  --mica:rgba(110,180,210,.25); --hand:#b9835a; --hand-ln:#e8b98f; --fig:#4b6166; --gnd:#c9d1d3; color-scheme:dark }}
body{{background:var(--bg);color:var(--ink);font:400 15px/1.6 var(--f-body)}}
.wrap{{max-width:1280px;margin:0 auto;padding-block:28px 64px;padding-left:max(16px,env(safe-area-inset-left,0px));padding-right:max(16px,env(safe-area-inset-right,0px));display:grid;gap:40px}}
.titleblock{{display:grid;grid-template-columns:minmax(0,1fr) auto;border:1.5px solid var(--ink);background:var(--sheet)}}
.titleblock .main{{padding:20px 24px;min-width:0}}
.titleblock h1{{font:700 clamp(30px,5vw,48px)/1 var(--f-disp);letter-spacing:.04em;margin:0 0 10px;text-wrap:balance;text-transform:uppercase}}
.titleblock p{{margin:0;max-width:68ch;color:var(--ink-2)}}
.titleblock .side{{border-left:1.5px solid var(--ink);display:grid;grid-auto-rows:1fr;min-width:210px}}
.titleblock .side div{{padding:8px 16px;border-bottom:1px solid var(--rule);font:500 13px var(--f-body)}}
.titleblock .side div:last-child{{border-bottom:0}}
.lab{{display:block;font:600 11px var(--f-disp);letter-spacing:.12em;color:var(--ink-2);text-transform:uppercase}}
.st{{display:inline-flex;align-items:center;gap:6px}}
.st::before{{content:"";width:8px;height:8px;border-radius:50%;background:var(--ok)}}
.st.wait::before{{background:transparent;border:1.5px solid var(--ink-2)}}
h2{{font:700 26px/1.1 var(--f-disp);letter-spacing:.05em;text-transform:uppercase;margin:0;display:flex;gap:14px;align-items:baseline;text-wrap:balance}}
h2 .n{{font:700 13px var(--f-mono);color:var(--dim);border:1.5px solid var(--dim);padding:2px 7px}}
h3{{font:600 15px var(--f-body);margin:0 0 8px}}
section{{display:grid;gap:20px}}
.lead{{max-width:72ch;margin:0;color:var(--ink-2)}}
.cols{{display:grid;grid-template-columns:repeat(auto-fit,minmax(min(100%,460px),1fr));gap:24px}}
.card{{background:var(--sheet);border:1px solid var(--rule);padding:18px 20px;min-width:0}}
.tbl{{overflow-x:auto}}
table{{border-collapse:collapse;width:100%;font-size:13.5px}}
th{{text-align:left;font:600 11px var(--f-disp);letter-spacing:.1em;text-transform:uppercase;color:var(--ink-2);border-bottom:1.5px solid var(--ink);padding:6px 10px 6px 0}}
td{{border-bottom:1px solid var(--rule);padding:7px 10px 7px 0;vertical-align:top;font-variant-numeric:tabular-nums}}
td:first-child{{white-space:nowrap}}
.pill{{font:600 11px var(--f-body);padding:1px 8px;border-radius:99px;border:1px solid currentColor;white-space:nowrap}}
.p-hs{{color:var(--dim)}} .p-ut{{color:var(--ink-2)}} .p-co{{color:var(--ok)}}
.ruler{{position:relative;height:118px;margin:14px 6px 4px;border-bottom:1.5px solid var(--ink)}}
.ruler .band{{position:absolute;bottom:0;height:46px;background:var(--ok);opacity:.18}}
.ruler .pipe{{position:absolute;bottom:0;top:6px;border-left:2px dashed var(--warn)}}
.ruler .pipe span{{position:absolute;top:0;left:6px;font:600 12px var(--f-body);color:var(--warn);white-space:nowrap}}
.ruler .tick{{position:absolute;bottom:0;height:46px;border-left:1.5px solid var(--ok)}}
.ruler .tv{{position:absolute;top:-34px;left:-14px;font:700 12px var(--f-mono);color:var(--ok)}}
.ruler .tl{{position:absolute;top:-18px;left:-14px;font:500 10.5px var(--f-body);color:var(--ink-2);white-space:nowrap}}
.ruler .tick:last-child .tl, .ruler .tick:last-child .tv{{left:auto;right:4px}}
.ruler .mm{{position:absolute;bottom:-22px;transform:translateX(-50%);font:500 11px var(--f-mono);color:var(--ink-2)}}
.note{{font-size:13px;color:var(--ink-2);margin:8px 0 0}}
.sheet-wrap{{margin:0;display:grid;gap:8px}}
.sheet-wrap figcaption{{display:flex;flex-wrap:wrap;gap:6px 14px;align-items:baseline;font:600 15px var(--f-body)}}
.sheet-wrap .so{{font:700 13px var(--f-mono);color:var(--dim)}}
.sheet-wrap .png{{margin-left:auto;font:500 12px var(--f-mono);color:var(--ink-2)}}
.scroll{{overflow-x:auto;border:1px solid var(--rule);background:var(--sheet)}}
svg.sheet{{display:block;width:100%;min-width:880px;height:auto}}
.toc{{display:flex;flex-wrap:wrap;gap:8px}}
.toc a{{font:600 13px var(--f-body);color:var(--ink);text-decoration:none;border:1px solid var(--rule);background:var(--sheet);padding:5px 12px}}
.toc a:hover,.toc a:focus-visible{{border-color:var(--dim);color:var(--dim);outline:none}}
.finds{{margin:0;padding:0;list-style:none;display:grid;gap:10px}}
.finds li{{background:var(--sheet);border:1px solid var(--rule);border-left:3px solid var(--warn);padding:10px 14px}}
.finds .fix{{display:block;color:var(--ink-2);font-size:13.5px}}
.src{{margin:0;padding-left:20px;font-size:13.5px}}
.src a{{color:var(--dim)}}
@media (max-width:640px){{ .titleblock{{grid-template-columns:1fr}} .titleblock .side{{border-left:0;border-top:1.5px solid var(--ink)}} }}
</style>
<div class="wrap">
<header class="titleblock">
  <div class="main">
    <span class="lab">Dự án KHKT · Phòng thí nghiệm ảo nhiều người</span>
    <h1>Bản vẽ tay cầm VR</h1>
    <p>Bản V1: tay cầm làm từ ống PVC phi 34, nối cáp 20 lõi về hộp đeo thắt lưng (Arduino Mega, L298N, ESP32-CAM phát Wi-Fi).
    Trang này gồm số liệu đầu vào (bước 1) và năm bản vẽ 2D có chú thích, kích thước, cách đấu nối và cách đeo (bước 2).</p>
  </div>
  <div class="side">
    <div><span class="lab">Bước 1 · số liệu</span><span class="st">xong</span></div>
    <div><span class="lab">Bước 2 · bản vẽ 2D</span><span class="st">xong, chờ duyệt</span></div>
    <div><span class="lab">Bước 3–5 · 3D, đánh giá, V2</span><span class="st wait">chưa làm</span></div>
    <div><span class="lab">Ngày</span>27/10/2026</div>
  </div>
</header>

<section>
  <h2><span class="n">1</span>Số liệu đầu vào</h2>
  <p class="lead">Kích thước tay nắm lấy từ số đo bàn tay người Việt. Kích thước bên trong lấy từ linh kiện thật. Giá lấy ở Hshop ngày 27/10/2026, món nào không tìm thấy thì ghi là ước tính.</p>
  <div class="cols">
    <div class="card">
      <h3>Bàn tay nam người Việt (mm)</h3>
      {bang(["Số đo", "Trung bình ± độ lệch", "Khoảng P5 – P95"], tay_rows)}
      <h3 style="margin-top:22px">Đường kính tay nắm tối ưu = 19,7% chiều dài bàn tay</h3>
      {thuoc}
      <p class="note" style="margin-top:34px">Ống PVC phi 34 (thực đo Ø33,5) nằm giữa dải tối ưu {duong_kinh[0][1]:.0f}–{duong_kinh[2][1]:.0f} mm, nên giữ ống này.
      Vùng nắm cần dài ít nhất {hb95:.0f} mm (bằng chiều rộng tay to P95); bản V1 có 140 mm.
      Số đo bàn tay là của nam (khảo sát năm 1993), nên học sinh nữ và lớp 11 có thể nhỏ hơn. Nhóm nên tự đo tay 10 bạn để kiểm lại.</p>
    </div>
    <div class="card">
      <h3>Kích thước linh kiện dùng để vẽ</h3>
      {bang(["Linh kiện", "Kích thước (mm)", "Khối lượng", "Nguồn"], LINH_KIEN)}
    </div>
  </div>
  <div class="card">
    <h3>Dự trù chi phí bản V1</h3>
    {bang(["Món", "SL", "Đơn giá", "Thành tiền", "Nguồn giá"], gia_rows)}
    <p class="note">Mega, ESP32-CAM và sạc dự phòng nhóm đã có nên không tính. Nếu phải mua mới thì ở Hshop ESP32-CAM giá 180.000 đ, còn Vietduino Mega giá 385.000 đ.</p>
  </div>
</section>

<section>
  <h2><span class="n">2</span>Bản vẽ 2D</h2>
  <p class="lead">Bản vẽ theo phương pháp chiếu góc thứ nhất như sách Công nghệ: hình chiếu bằng đặt dưới hình chiếu đứng, hình chiếu cạnh đặt bên phải. Kích thước ghi bằng mm, tính từ mép sau của ống. Hệ trục giống docs/02: X dọc ống ra đầu, Y sang trái, Z lên trên.</p>
  <nav class="toc" aria-label="Các bản vẽ">
    <a href="#tc-01">TC-01 Ba hình chiếu</a><a href="#tc-02">TC-02 Bên trong</a><a href="#tc-03">TC-03 Hộp thắt lưng</a>
    <a href="#tc-04">TC-04 Đấu nối</a><a href="#tc-05">TC-05 Cách cầm, đeo</a><a href="#bang-dau-noi">Bảng đấu nối</a>
  </nav>
  {"".join(sheets)}
  <div class="cols" id="bang-dau-noi">
    <div class="card">
      <h3>Cáp 20 lõi: tay cầm ↔ hộp</h3>
      {bang(["Lõi", "Tín hiệu", "Đầu tay cầm", "Đầu hộp", "Ghi chú"], DAU_NOI)}
    </div>
    <div class="card">
      <h3>Nối bên trong hộp</h3>
      {bang(["Từ", "Tới", "Ghi chú"], TRONG_HOP)}
      <p class="note">Chân lấy theo <code>firmware/tay_cam_mega</code>. Đường Wi-Fi giữ cách của docs/09 (Mega → ESP32-CAM IO13 → UDP 4210), nhưng dời sang TX1 để khỏi trùng D22 và đủ tốc độ cho gói 29 byte.</p>
    </div>
  </div>
</section>

<section>
  <h2><span class="n">→4</span>Vấn đề phát hiện khi vẽ</h2>
  <p class="lead">Mình ghi lại ở đây, sẽ phân tích kỹ ở bước 4 (đánh giá khả thi) và xử lý ở bước 5 (bản cải tiến V2).</p>
  <ul class="finds">{ph}</ul>
</section>

<section>
  <h2>Nguồn</h2>
  <ul class="src">{src}</ul>
  <p class="note">Không tìm được khảo sát bàn tay đăng bằng tiếng Việt, nên phải dùng số liệu tiếng Anh. Bản vẽ được sinh từ <code>tools/ve_tay_cam/ve_2d.py</code>. Chạy lại script đó sau khi đo linh kiện thật bằng thước kẹp.</p>
</section>
</div>
"""


if __name__ == "__main__":
    ra = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ve_2d.THU_MUC_RA, "tay_cam_vr.html")
    with open(ra, "w", encoding="utf-8") as f:
        f.write(trang())
    print(ra)
