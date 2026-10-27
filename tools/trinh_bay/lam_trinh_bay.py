"""Bản trình bày ý tưởng, tiến độ, lộ trình dự án VERA Lab (16 trang 16:9) -> HTML -> PDF.

Chạy:  python tools/trinh_bay/lam_trinh_bay.py <thư mục dựng>
  - <thư mục dựng>/img/*.jpg|png phải có sẵn (ảnh chụp đã thu nhỏ, ảnh 3D tay cầm)
  - ra <thư mục dựng>/trinh_bay.html; in PDF bằng Edge:
    msedge --headless=new --print-to-pdf=docs/trinh-bay/VERA_Lab_trinh_bay.pdf --no-pdf-header-footer trinh_bay.html
Số liệu lấy từ docs/05–15 của dự án.
"""
import os
import sys

TEN = "VERA Lab"
TEN_DAY_DU = "Hệ thống trải nghiệm phòng thí nghiệm thực tế ảo Hóa – Sinh tích hợp AI đám mây / cục bộ trong dạy học"
NHOM = "Nguyễn Ngọc Bảo Trọng · Trần Đức Trọng"
TONG = 16

CSS = """
@page { size: 1280px 720px; margin: 0 }
:root{ --nen:#f5f7f6; --giay:#ffffff; --muc:#10242b; --phu:#5a6d72; --vien:#d5dfdd;
  --hoa:#0e7c86; --vr:#11a8c8; --cam:#d98b12; --sinh:#3a9a4a; --ly:#6b5bd6; --do:#c2452b;
  --toi:#0d1b21; --f-tieu:'Barlow Condensed','Arial Narrow',sans-serif; --f-than:'Be Vietnam Pro','Segoe UI',sans-serif; --f-so:'JetBrains Mono',ui-monospace,monospace }
*{box-sizing:border-box}
html,body{margin:0;background:#888}
body{font:400 15px/1.5 var(--f-than);color:var(--muc);-webkit-print-color-adjust:exact;print-color-adjust:exact}
.trang{width:1280px;height:720px;position:relative;overflow:hidden;background:var(--nen);page-break-after:always;break-after:page;margin:0 auto}
.trang:last-child{page-break-after:auto;break-after:auto}
.dau{position:absolute;left:56px;top:34px;right:56px;display:flex;align-items:baseline;gap:18px}
.so{font:700 13px var(--f-so);color:var(--hoa);border:1.5px solid var(--hoa);padding:2px 7px}
.eyebrow{font:600 12px var(--f-tieu);letter-spacing:.16em;text-transform:uppercase;color:var(--phu)}
h1{font:700 40px/1.05 var(--f-tieu);letter-spacing:.01em;text-transform:uppercase;margin:0}
.tieu{position:absolute;left:56px;top:70px;right:56px}
.tieu h1{margin-bottom:4px}
.tieu p{margin:0;color:var(--phu);font-size:15.5px;max-width:1120px}
.than{position:absolute;left:56px;right:56px;top:166px;bottom:56px}
.chan{position:absolute;left:56px;right:56px;bottom:20px;display:flex;justify-content:space-between;font:500 11.5px var(--f-than);color:var(--phu);border-top:1px solid var(--vien);padding-top:8px}
.chan b{color:var(--muc);font-weight:600}
.luoi{display:grid;gap:18px;height:100%}
.luoi>*{min-height:0;min-width:0}
.the{background:var(--giay);border:1px solid var(--vien);padding:14px 16px;min-width:0}
.the h3{font:700 17px/1.2 var(--f-tieu);letter-spacing:.04em;text-transform:uppercase;margin:0 0 8px}
.the p{margin:0 0 6px}
ul.ds{margin:0;padding-left:18px}
ul.ds li{margin:0 0 5px}
ul.ds li::marker{color:var(--hoa)}
.anh{width:100%;height:100%;object-fit:cover;display:block;border:1px solid var(--vien)}
.cap{font:500 11.5px var(--f-than);color:var(--phu);margin-top:4px}
.khung-anh{display:flex;flex-direction:column;min-height:0;overflow:hidden}
.khung-anh .anh{flex:1;min-height:0}
.so-lon{font:700 46px/1 var(--f-tieu);color:var(--hoa)}
.so-lon small{font:600 14px var(--f-than);color:var(--phu);display:block;margin-top:4px;letter-spacing:0}
.chip{display:inline-block;font:600 11px var(--f-than);padding:1px 9px;border-radius:99px;white-space:nowrap}
.c-xong{background:#dff1e6;color:#1f6b3a}.c-dang{background:#fdf0d9;color:#8a5a06}.c-ke{background:#e4ecf7;color:#2b4f86}.c-ng{background:#efe9fb;color:#5a3fb0}
table.bang{border-collapse:collapse;width:100%;font-size:13.5px}
.bang td,.bang th{border-bottom:1px solid var(--vien);padding:5px 8px 5px 0;text-align:left;vertical-align:top}
.bang th{font:600 11px var(--f-tieu);letter-spacing:.12em;text-transform:uppercase;color:var(--phu);border-bottom:1.5px solid var(--muc)}
.mono{font-family:var(--f-so);font-size:12.5px}
.nhan{font:600 11px var(--f-tieu);letter-spacing:.14em;text-transform:uppercase;color:var(--phu)}
.vach{width:44px;height:4px;background:var(--hoa);margin:6px 0 10px}
"""


def chan(n):
    return (f'<div class="chan"><span><b>{TEN}</b> · {NHOM} · Dự án Khoa học Kỹ thuật</span>'
            f'<span class="mono">{n:02d} / {TONG}</span></div>')


def trang(n, eyebrow, tieu_de, mo_ta, than, nen=""):
    return (f'<section class="trang" {nen}><div class="dau"><span class="so">{n:02d}</span><span class="eyebrow">{eyebrow}</span></div>'
            f'<div class="tieu"><h1>{tieu_de}</h1><p>{mo_ta}</p></div><div class="than">{than}</div>{chan(n)}</section>')


def anh(ten, cap=None, style=""):
    c = f'<div class="cap">{cap}</div>' if cap else ""
    return f'<div class="khung-anh" style="{style}"><img class="anh" src="img/{ten}" alt="{cap or ten}">{c}</div>'


def chip(trang_thai):
    lop = {"Xong": "c-xong", "Chạy được": "c-xong", "Đang làm": "c-dang", "Thiết kế xong": "c-dang",
           "Kế hoạch": "c-ke", "Nghiên cứu": "c-ng", "Định hướng": "c-ng"}[trang_thai]
    return f'<span class="chip {lop}">{trang_thai}</span>'


# ---------------- các trang ----------------

def t01():
    return f"""<section class="trang" style="background:var(--toi)">
<img src="img/bia.jpg" alt="Toàn cảnh phòng thí nghiệm ảo" style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover;opacity:.9">
<div style="position:absolute;inset:0;background:linear-gradient(90deg,rgba(9,20,25,.96) 0%,rgba(9,20,25,.86) 44%,rgba(9,20,25,.15) 78%)"></div>
<div style="position:absolute;left:72px;top:74px;width:700px;color:#e8f2f3">
  <div style="font:600 13px var(--f-tieu);letter-spacing:.22em;text-transform:uppercase;color:#7fd6e6">Dự án Khoa học Kỹ thuật · Bản trình bày ý tưởng và tiến độ</div>
  <div style="font:700 112px/0.9 var(--f-tieu);letter-spacing:.01em;margin:22px 0 8px">VERA <span style="color:#3fd0ea">LAB</span></div>
  <div style="font:500 17px var(--f-tieu);letter-spacing:.12em;text-transform:uppercase;color:#9fc3c9">Virtual Experiment &amp; Reality-based AI Lab</div>
  <div style="width:64px;height:4px;background:#3fd0ea;margin:26px 0"></div>
  <div style="font:600 25px/1.3 var(--f-than);max-width:640px">{TEN_DAY_DU}</div>
  <div style="margin-top:56px;font:500 13px var(--f-than);color:#9fc3c9;letter-spacing:.06em;text-transform:uppercase">Nhóm thực hiện</div>
  <div style="font:600 22px var(--f-than);margin-top:6px">Nguyễn Ngọc Bảo Trọng</div>
  <div style="font:600 22px var(--f-than)">Trần Đức Trọng</div>
</div>
<div style="position:absolute;right:56px;bottom:24px;font:500 12px var(--f-than);color:#c9dde0">Ảnh: phòng thí nghiệm Hóa học trong ứng dụng (Unity 6), chụp từ bản chạy thật</div>
</section>"""


def t02():
    than = f"""<div class="luoi" style="grid-template-columns:1.05fr 1fr">
<div class="the" style="border-top:4px solid var(--vr)">
  <div class="nhan">Tên đề xuất</div>
  <div style="font:700 44px/1 var(--f-tieu);margin:6px 0 2px">VERA Lab</div>
  <div class="mono" style="color:var(--phu)">Virtual Experiment &amp; Reality-based AI Lab</div>
  <ul class="ds" style="margin-top:12px">
    <li><b>VER</b> đọc lên gợi <b>VR</b> (thực tế ảo); <b>A</b> là <b>AI</b>.</li>
    <li><i>Vera</i> theo gốc Latin <i>vērus</i> nghĩa là "chân thật": thí nghiệm ảo nhưng thao tác và hiện tượng đúng như thật.</li>
    <li>Ngắn, dễ nhớ, đọc được bằng cả tiếng Việt lẫn tiếng Anh.</li>
    <li>Tên dự phòng: <b>LabVerse</b>, <b>BioChem VR</b>.</li>
  </ul>
  <div class="nhan" style="margin-top:14px">Mô tả</div>
  <p style="margin-top:4px"><b>VERA Lab</b> là hệ thống phòng thí nghiệm thực tế ảo cho môn Hóa học và Sinh học bậc THPT. Học sinh đeo kính VR và cầm tay cầm tự chế để thao tác như ở phòng thí nghiệm thật:</p>
  <ul class="ds">
    <li>lấy hóa chất đúng lượng, lắp dụng cụ, chuẩn độ;</li>
    <li>quan sát phản ứng do bộ máy hóa học tính toán.</li>
  </ul>
  <p>Giáo viên giao bài, chia nhóm và theo dõi kỹ năng qua web quản lý. Trợ lý AI chạy trên đám mây hoặc ngay trên máy của trường, giải thích hiện tượng và nhắc an toàn. Nhiều học sinh cùng vào một phòng lab qua máy chủ.</p>
</div>
<div class="luoi" style="grid-template-rows:1fr 1fr;gap:14px">
  <div class="the" style="border-top:4px solid var(--do)">
    <h3>Vấn đề</h3>
    <ul class="ds">
      <li>Nhiều trường thiếu phòng thí nghiệm, hóa chất, dụng cụ; học sinh chủ yếu xem thầy cô làm mẫu.</li>
      <li>Thí nghiệm độc, dễ cháy nổ (Cl₂, H₂S, H₂SO₄ đặc…) thường bị bỏ qua vì nguy hiểm.</li>
      <li>Hóa chất tốn kém, có hạn dùng; một lỗi thao tác là phải làm lại từ đầu.</li>
      <li>Giáo viên khó theo dõi cùng lúc thao tác của cả lớp.</li>
    </ul>
  </div>
  <div class="the" style="border-top:4px solid var(--sinh)">
    <h3>Mục tiêu</h3>
    <ul class="ds">
      <li>Thao tác <b>bằng tay như thật</b>: nghiêng để rót, vặn cổ tay mở khóa buret, lắc tròn bình nón.</li>
      <li>Làm được cả thí nghiệm nguy hiểm, an toàn tuyệt đối, không tốn hóa chất.</li>
      <li>Đo được kỹ năng: độ run tay, tốc độ nhỏ giọt cuối, số giọt vượt.</li>
      <li>Cả lớp cùng vào lab; AI hỗ trợ từng em.</li>
    </ul>
  </div>
</div></div>"""
    return trang(2, "Tên dự án và mô tả", "Tên dự án và bài toán", TEN_DAY_DU, than)


def t03():
    svg = """<svg viewBox="0 0 1172 470" width="1164" height="467" style="display:block">
<defs><marker id="mui" viewBox="0 0 10 10" refX="10" refY="5" markerWidth="8" markerHeight="8" orient="auto-start-reverse"><path d="M0,1 L10,5 L0,9 z" fill="#5a6d72"/></marker></defs>
<style>.k{fill:#fff;stroke:#10242b;stroke-width:1.4}.kt{font:700 17px 'Barlow Condensed',sans-serif;letter-spacing:.04em;fill:#10242b}.kn{font:500 12.5px 'Be Vietnam Pro',sans-serif;fill:#3e5257}.d{stroke:#5a6d72;stroke-width:1.6;fill:none}.dl{font:500 11.5px 'Be Vietnam Pro',sans-serif;fill:#5a6d72}</style>
<rect class="k" x="0" y="20" width="250" height="420" rx="6" style="stroke:#11a8c8;stroke-width:2"/>
<text class="kt" x="16" y="48">HỌC SINH</text>
<text class="kn" x="16" y="78">Kính VR (điện thoại / kính rời)</text>
<rect x="16" y="96" width="218" height="142" rx="4" fill="#e8f7fa"/>
<text class="kt" x="28" y="120" style="font-size:15px">TAY CẦM HALO</text>
<text class="kn" x="28" y="142">ESP32 38 chân · Wi-Fi UDP</text><text class="kn" x="28" y="162">BMI160: vặn, gập, lắc cổ tay</text>
<text class="kn" x="28" y="182">Joystick: đi, quay</text><text class="kn" x="28" y="202">Cò Hall analog 0–100%</text><text class="kn" x="28" y="222">Vòng halo cho OpenCV</text>
<rect x="16" y="252" width="218" height="74" rx="4" fill="#e8f7fa"/>
<text class="kt" x="28" y="276" style="font-size:15px">WEBCAM + OPENCV</text>
<text class="kn" x="28" y="298">đo xa gần, vị trí tay</text><text class="kn" x="28" y="316">Z = f · D / d (D = 70 mm)</text>
<text class="kn" x="16" y="356">Dự phòng: chuột – bàn phím</text><text class="kn" x="16" y="376">(nhóm đối chứng, máy không có kính)</text>
<rect class="k" x="330" y="20" width="440" height="270" rx="6" style="stroke:#0e7c86;stroke-width:2"/>
<text class="kt" x="348" y="48">ỨNG DỤNG UNITY 6 · WINDOWS / macOS</text>
<rect x="348" y="64" width="128" height="62" rx="4" fill="#e3f2f3"/><text class="kt" x="358" y="88" style="font-size:15px">LAB HÓA</text><text class="kn" x="358" y="108">≈ 70% · đang dùng</text>
<rect x="486" y="64" width="128" height="62" rx="4" fill="#e6f3e8"/><text class="kt" x="496" y="88" style="font-size:15px">LAB SINH</text><text class="kn" x="496" y="108">tầng 2 · kế hoạch</text>
<rect x="624" y="64" width="128" height="62" rx="4" fill="#efeafb"/><text class="kt" x="634" y="88" style="font-size:15px">LAB LÝ</text><text class="kn" x="634" y="108">định hướng</text>
<text class="kn" x="348" y="152">• Bộ máy phản ứng hóa học (pH, Ksp, phức, oxi hóa – khử, nhiệt)</text>
<text class="kn" x="348" y="174">• Mạng dòng chảy 10 Hz: chưng cất, thu khí, lọc, chiết</text>
<text class="kn" x="348" y="196">• Khu chế tạo dụng cụ, lắp ráp khớp nối chuẩn</text>
<text class="kn" x="348" y="218">• Robot trợ lý, 6 máy phân tích chạy trên mẫu</text>
<text class="kn" x="348" y="240">• Ghi chuyển động 100 Hz → chỉ số khéo tay</text>
<text class="kn" x="348" y="262">• Tải / gỡ từng khu, giữ hiện trạng; 60–70 FPS laptop</text>
<rect class="k" x="330" y="320" width="440" height="120" rx="6"/>
<text class="kt" x="348" y="348">MÁY CHỦ PHÒNG LAB (FastAPI · SQLite)</text>
<text class="kn" x="348" y="374">• Web quản lý: kho 720 hóa chất, thiết bị, nhật ký sự cố</text>
<text class="kn" x="348" y="396">• LMS giáo viên – học sinh (kế hoạch)</text>
<text class="kn" x="348" y="418">• Đồng bộ nhiều người trong lab (nghiên cứu)</text>
<rect class="k" x="860" y="20" width="308" height="200" rx="6" style="stroke:#6b5bd6;stroke-width:2"/>
<text class="kt" x="878" y="48">TRỢ LÝ AI</text>
<text class="kn" x="878" y="76">Đám mây (đang dùng): Groq, DeepSeek,</text><text class="kn" x="878" y="94">Gemini xoay vòng khóa; dự phòng Claude</text>
<text class="kn" x="878" y="124">Cục bộ (kế hoạch): Ollama / llama.cpp,</text><text class="kn" x="878" y="142">mô hình 7–8B lượng tử 4 bit trên máy trường</text>
<text class="kn" x="878" y="172">Chú thích hiện tượng, hỏi đáp,</text><text class="kn" x="878" y="190">lập kế hoạch cho robot (máy chủ kiểm duyệt)</text>
<rect class="k" x="860" y="260" width="308" height="180" rx="6" style="stroke:#d98b12;stroke-width:2"/>
<text class="kt" x="878" y="288">GIÁO VIÊN</text>
<text class="kn" x="878" y="316">Trình duyệt web: giao bài, chia nhóm,</text><text class="kn" x="878" y="334">thảo luận, học liệu, quy tắc</text>
<text class="kn" x="878" y="362">Xem nhật ký thao tác, lỗi an toàn,</text><text class="kn" x="878" y="380">bảng kỹ năng từng học sinh</text>
<text class="kn" x="878" y="410">Bóng ma giáo viên: phát lại thao tác mẫu</text>
<path class="d" d="M250,170 L330,170" marker-end="url(#mui)"/><text class="dl" x="256" y="160">UDP 4210</text>
<path class="d" d="M250,290 L330,240" marker-end="url(#mui)"/><text class="dl" x="262" y="300">vị trí tay</text>
<path class="d" d="M550,290 L550,320" marker-start="url(#mui)" marker-end="url(#mui)"/><text class="dl" x="558" y="310">HTTP / mạng LAN</text>
<path class="d" d="M770,120 L860,120" marker-start="url(#mui)" marker-end="url(#mui)"/>
<path class="d" d="M770,380 L860,350" marker-start="url(#mui)" marker-end="url(#mui)"/>
<path class="d" d="M770,360 L860,180" marker-start="url(#mui)" marker-end="url(#mui)"/>
</svg>"""
    than = f'<div class="the" style="padding:18px 0 0 0;background:transparent;border:0">{svg}</div>'
    return trang(3, "Tổng quan", "Kiến trúc hệ thống",
                 "Một ứng dụng Unity chạy trên cả Windows và macOS; tay cầm tự chế gửi dữ liệu qua Wi-Fi; máy chủ của phòng lab nối web quản lý, AI và nhiều người chơi.", than)


def t04():
    hang = [
        ("Phòng lab Hóa 3D: 300 m², kho hóa chất, phòng máy, phòng robot", "Xong"),
        ("Bộ máy phản ứng hóa học (C# thuần, có kiểm thử riêng)", "Xong"),
        ("720 hóa chất, 27 kệ theo nhóm bảo quản, tra cứu", "Xong"),
        ("Chuẩn độ tự làm từ đầu, sai số thị sai, đồ thị pH", "Xong"),
        ("Khu chế tạo dụng cụ, lắp ráp khoảng 40 bộ phận", "Xong"),
        ("Mạng dòng chảy: chưng cất, hồi lưu, thu khí, lọc, chiết", "Xong"),
        ("6 máy phân tích + robot trợ lý theo kế hoạch AI", "Xong"),
        ("Web quản lý kho, thiết bị, nhật ký + mạng lưới khóa AI", "Xong"),
        ("Tay cầm Wi-Fi bản thử: joystick → ESP32 → game", "Chạy được"),
        ("Tay cầm Halo: 3D, 7 file in, 5 bản vẽ, kiểm va chạm", "Thiết kế xong"),
        ("Lab Sinh học (tầng 2)", "Kế hoạch"),
        ("Web LMS cho giáo viên, học sinh", "Kế hoạch"),
        ("Nhiều người cùng thao tác trong lab", "Nghiên cứu"),
        ("AI chạy cục bộ trên máy của trường", "Kế hoạch"),
        ("Lab Vật lý", "Định hướng"),
    ]
    rows = "".join(f"<tr><td>{a}</td><td>{chip(b)}</td></tr>" for a, b in hang)
    than = f"""<div class="luoi" style="grid-template-columns:330px 1fr">
<div class="luoi" style="grid-template-rows:auto auto 1fr;gap:14px">
  <div class="the" style="border-top:4px solid var(--hoa)"><div class="so-lon">≈ 70%<small>phần Hóa học (nhóm tự đánh giá)</small></div>
    <div style="height:10px;background:#e3ebea;margin-top:12px"><div style="width:70%;height:100%;background:var(--hoa)"></div></div></div>
  <div class="the">
    <div class="luoi" style="grid-template-columns:1fr 1fr;gap:10px">
      <div><div class="so-lon" style="font-size:34px">118</div><div class="cap">file mã C# (≈ 24 500 dòng)</div></div>
      <div><div class="so-lon" style="font-size:34px">720</div><div class="cap">hóa chất có vị trí kệ</div></div>
      <div><div class="so-lon" style="font-size:34px">6</div><div class="cap">máy phân tích chạy trên mẫu</div></div>
      <div><div class="so-lon" style="font-size:34px">60–70</div><div class="cap">FPS trên laptop Iris Xe</div></div>
    </div></div>
  <div class="the"><h3>Kiểm thử</h3><ul class="ds" style="font-size:13.5px">
    <li>Bài tự chạy trong Unity: chuẩn độ, lắp ráp (9 bộ mẫu), chế tạo, tải / gỡ khu.</li>
    <li>21 bài kiểm thử Python cho giao thức, bộ lọc, mô hình rót, chuẩn độ.</li>
    <li>Đo FPS bằng bản .exe, nhiều lượt.</li></ul></div>
</div>
<div class="the" style="padding:8px 16px"><table class="bang" style="font-size:12.5px"><thead><tr><th>Hạng mục</th><th>Trạng thái</th></tr></thead><tbody>{rows}</tbody></table></div>
</div>"""
    return trang(4, "Tiến độ hiện tại", "Đã làm được gì", "Phần Hóa học đã chạy trọn vẹn bằng chuột – bàn phím; phần cứng tay cầm đã thiết kế xong; Sinh, LMS, nhiều người, AI cục bộ là các bước tiếp theo.", than)


def t05():
    than = f"""<div class="luoi" style="grid-template-columns:1.25fr 1fr 1fr;grid-template-rows:1fr 1fr">
  {anh('ban_hs.jpg', 'Khu bàn học sinh: 9 bàn, trần gỗ treo, ghế nhiều màu', 'grid-row:1/3')}
  {anh('kho.jpg', 'Phòng hóa chất: 27 kệ / tủ, mỗi lọ đúng ô')}
  {anh('phong_may.jpg', 'Phòng máy phân tích hiện đại')}
  {anh('nha_hoa_hoc.jpg', 'Tường nhà hóa học (ảnh công cộng)')}
  <div class="the"><h3>Không gian</h3><ul class="ds" style="font-size:13.5px">
    <li>Lab 20 × 15 m, phòng hóa chất 7 × 15 m, dãy phòng máy và robot.</li>
    <li>Phong cách lab trường quốc tế; mỗi khu một màu; biển song ngữ.</li>
    <li>Cửa tự động: đi qua cửa thì tải khu kế tiếp, gỡ khu phía sau mà <b>vẫn giữ hiện trạng</b> (ống nghiệm, hóa chất còn nguyên).</li>
    <li>Khu an toàn: vòi rửa mắt, vòi sen, bình chữa cháy, tủ bảo hộ.</li></ul></div>
</div>"""
    return trang(5, "Tính năng Hóa học · 1", "Phòng thí nghiệm Hóa 3D", "Toàn bộ cảnh dựng bằng code (menu KHKT → Dựng phòng thí nghiệm), nên sửa bố cục, thêm khu đều dựng lại tự động.", than)


def t06():
    than = f"""<div class="luoi" style="grid-template-columns:1fr 1fr 1.05fr">
  <div class="luoi" style="grid-template-rows:1fr 1fr;gap:14px">{anh('tra_cuu.jpg', 'Máy tra cứu: tìm theo tên có dấu / không dấu, công thức')}{anh('vi_tri_ke.jpg', 'Chỉ vị trí lọ trên kệ (ví dụ K06-6-07)')}</div>
  <div class="the"><h3>Danh mục hóa chất</h3><ul class="ds">
    <li><b>720 hóa chất</b>, xếp vào <b>27 kệ / tủ</b> theo nhóm bảo quản: axit, bazơ, chất oxi hóa, muối theo gốc axit, chất dễ cháy, bình khí…</li>
    <li>Mỗi chất có: công thức, khối lượng mol, các ion khi tan, nồng độ gợi ý, nhãn tính chất và nhãn nguy hiểm.</li>
    <li>80 loại thiết bị: cân phân tích 0,0001 g, máy đo pH, độ dẫn, quang phổ, ly tâm…</li>
    <li>Web quản lý: nhập / xuất kho có nhật ký, cảnh báo chất sắp hết, hạn dùng.</li></ul></div>
  <div class="the" style="border-top:4px solid var(--cam)"><h3>Lượng hóa chất như thật</h3><ul class="ds">
    <li>Mỗi dụng cụ giữ <b>số mol</b> từng chất, nước, chất chỉ thị. Rót, trộn theo thứ tự nào thì kết quả cũng đúng logic.</li>
    <li><b>Rót theo góc nghiêng</b> theo mô hình đập tràn: cốc càng vơi càng phải nghiêng nhiều, nghiêng quá tay thì chảy ào.</li>
    <li><b>Nhỏ giọt</b> 0,05 ml/giọt, có sai số ngẫu nhiên; <b>pipet</b> hút và chỉnh đúng vạch.</li>
    <li><b>Cân</b>: đặt cốc, trừ bì, xúc chất rắn tới đủ gam.</li>
    <li>Rót ra ngoài bình thì tính là <b>đổ ra bàn</b> và ghi vào nhật ký an toàn.</li></ul></div>
</div>"""
    return trang(6, "Tính năng Hóa học · 2", "Hóa chất và lượng hóa chất", "Hóa chất lấy từ danh mục CSV của nhóm, được script chuyển thành dữ liệu cho Unity và web: một nguồn, hai nơi dùng.", than)


def t07():
    than = f"""<div class="luoi" style="grid-template-columns:1.05fr 1fr">
  <div class="the"><h3>Bộ máy phản ứng (C#, chạy độc lập với Unity, có kiểm thử)</h3>
  <table class="bang" style="font-size:13px"><tbody>
    <tr><td><b>pH</b></td><td>Tính từ bảo toàn điện tích; axit, bazơ yếu (cacbonat, photphat, amoni, axetat…)</td></tr>
    <tr><td><b>Kết tủa</b></td><td>Theo Ksp: hiđroxit lưỡng tính tan lại; 55 cặp ion (AgCl, BaSO₄, CaCO₃, PbI₂…); chuẩn độ Mohr ra đúng thứ tự</td></tr>
    <tr><td><b>Phức</b></td><td>[Cu(NH₃)₄]²⁺, [Ag(NH₃)₂]⁺, [Fe(SCN)]²⁺, đồng – poliol (Fehling, Benedict)</td></tr>
    <tr><td><b>Khí</b></td><td>CO₂, SO₂, H₂S, NH₃, Cl₂, NO… thoát khi vượt độ tan</td></tr>
    <tr><td><b>Oxi hóa – khử</b></td><td>KMnO₄ (3 môi trường), Cr(VI), halogen, Fe³⁺, H₂O₂, HNO₃…</td></tr>
    <tr><td><b>Theo thời gian</b></td><td>Kim loại tan trong axit, kiềm, nước; tráng bạc; xà phòng hóa; Fe(OH)₂ hóa nâu</td></tr>
    <tr><td><b>Màu, nhiệt</b></td><td>Màu theo định luật Beer, chất chỉ thị, độ đục, tách lớp; nhiệt trung hòa, hòa tan; pha loãng H₂SO₄ sai cách là sự cố</td></tr>
  </tbody></table>
  <p style="margin-top:8px;font-size:13.5px">Mỗi hiện tượng sinh một <b>sự kiện</b> (tên, phương trình, giải thích, mức nguy hiểm). Unity hiện thẻ chú thích ngay; AI chỉ diễn giải thêm.</p></div>
  <div class="luoi" style="grid-template-rows:1fr 1fr;gap:14px">{anh('dinh_tinh.jpg', 'Bàn định tính: nhận biết ion bằng thuốc thử')}{anh('khi_doc.jpg', 'Sự cố: khí độc sinh ra ngoài tủ hút được cảnh báo')}</div>
</div>"""
    return trang(7, "Tính năng Hóa học · 3", "Phản ứng tính bằng bộ máy hóa học", "Hiện tượng không làm sẵn bằng hoạt hình: được tính từ thành phần thật trong dụng cụ, nên làm sai sẽ ra đúng hậu quả của cái sai.", than)


def t08():
    buoc = ["Lắp buret lên kẹp", "Nạp NaOH 0,1 M quá vạch 0", "Xả về vạch 0, đọc V đầu (hạ mắt ngang mặt khum)",
            "Hút 10 ml HCl bằng pipet, chỉnh vạch", "Nhỏ phenolphtalein", "Chuẩn độ: mở khóa, lắc tròn, nhỏ từng giọt",
            "Dừng ở hồng nhạt bền, đọc V cuối, tính nồng độ"]
    ol = "".join(f"<li>{b}</li>" for b in buoc)
    than = f"""<div class="luoi" style="grid-template-rows:250px 1fr">
  <div class="luoi" style="grid-template-columns:repeat(4,1fr);gap:12px">
    {anh('cd_doc.jpg', 'Đọc V đầu qua kính lúp, có thị sai')}{anh('cd_chuan.jpg', 'Mở khóa, lắc bình nón')}{anh('cd_hong.jpg', 'Điểm cuối: hồng nhạt bền')}{anh('cd_kq.jpg', 'Kết quả và nhận xét thao tác')}
  </div>
  <div class="luoi" style="grid-template-columns:1fr 1fr 1fr">
    <div class="the"><h3>7 bước tự làm từ đầu</h3><ol style="margin:0;padding-left:20px;font-size:13.5px">{ol}</ol></div>
    <div class="the" style="border-top:4px solid var(--cam)"><h3>Giống thật ở chỗ nào</h3><ul class="ds" style="font-size:13.5px">
      <li>HCl có <b>nồng độ ngẫu nhiên 0,08–0,12 M</b> mỗi lượt: không thể học thuộc đáp án.</li>
      <li>Giọt rơi vào vùng "chưa trộn": <b>vệt hồng tan dần khi lắc</b>; không lắc thì dễ dừng sai.</li>
      <li>Khóa buret <b>giữ nguyên độ mở</b> khi buông: quên khóa là lỗi được ghi lại.</li>
      <li>Đồ thị pH theo thể tích, xem bất cứ lúc nào.</li></ul></div>
    <div class="the"><h3>Với tay cầm VR</h3><ul class="ds" style="font-size:13.5px">
      <li><b>Vặn cổ tay</b> để mở khóa (đo bằng BMI160); cò Hall để mở rất tinh.</li>
      <li><b>Lắc tròn</b> tay cầm để trộn bình nón.</li>
      <li>Mỗi giọt rung một nhịp: học sinh "đếm giọt bằng tay".</li>
      <li>Chuẩn độ điện thế tự động ở phòng máy: giấm ăn → V<sub>tđ</sub> 99,59 ml, 0,830 N.</li></ul></div>
  </div>
</div>"""
    return trang(8, "Tính năng Hóa học · 4", "Chuẩn độ axit – bazơ", "Bài chuẩn độ HCl bằng NaOH làm trọn vẹn: mọi thao tác chạy trên một mô hình chất lỏng chung.", than)


def t09():
    than = f"""<div class="luoi" style="grid-template-columns:1fr 1fr 1fr;grid-template-rows:1fr 1fr">
  {anh('ct_ngan.jpg', 'Ngăn kính "Khu chế tạo dụng cụ" trong phòng lab')}
  {anh('ct_lap.jpg', 'Giao diện lắp ráp 3D riêng: lắp bộ chưng cất')}
  <div class="the" style="border-top:4px solid var(--hoa)"><h3>Bàn gia công, chế tạo</h3><ul class="ds" style="font-size:13.5px">
    <li>Khoảng <b>40 bộ phận</b>: giá sắt, kẹp, bình cầu, ống sinh hàn, nút cao su, ống dẫn, ống nghiệm…</li>
    <li><b>Khớp nối chuẩn</b>: đúng loại, đúng cỡ mới lắp được, tự áp khít.</li>
    <li>Kiểm tra <b>hệ kín / hở</b> (hệ kín không được đun), trọng tâm, nguy cơ đổ.</li>
    <li>Bấm <b>Gia công</b>: bộ dụng cụ ra đúng bàn thí nghiệm đã chọn. Lắp trong xưởng ảo nhanh hơn 26% FPS.</li></ul></div>
  {anh('dc_chung_cat.jpg', 'Chưng cất đang chạy: hơi, giọt ngưng, nhiệt kế')}
  {anh('dc_co2.jpg', 'Sục CO₂ vào nước vôi trong')}
  <div class="the" style="border-top:4px solid var(--vr)"><h3>Mạng dòng chảy (10 lần/giây)</h3><ul class="ds" style="font-size:13.5px">
    <li><b>Chưng cất</b> theo nhiệt độ sôi của hỗn hợp (định luật Raoult, ≈ 110 chất lỏng).</li>
    <li>Đun <b>hồi lưu</b> (este hóa), <b>điều chế – rửa – thu khí</b> qua chậu nước.</li>
    <li><b>Áp suất hệ kín</b>, cảnh báo lắp ngược bình rửa khí.</li>
    <li><b>Lọc, lọc hút, chiết, nhiệt phân, cô cạn</b>. 9 loại thí nghiệm mẫu đều qua kiểm thử tự động.</li></ul></div>
</div>"""
    return trang(9, "Tính năng Hóa học · 5", "Khu chế tạo và lắp ráp tự do", "Học sinh tự lắp bộ dụng cụ như ngoài đời, và bộ đã gia công chạy được như thật: chất lỏng chảy, khí đi theo ống, hơi ngưng tụ.", than)


def t10():
    may = [("UV-Vis", "phổ 380–780 nm, λmax"), ("FTIR", "nhóm chức O-H, C=O, N-H…"), ("AAS", "nước thải mạ: Cu 254 · Zn 196 · Ni 117 mg/L"),
           ("GC-MS", "rượu: etanol 7,3 phút, m/z 46"), ("HPLC", "nước cam: xitrat, glucozơ, saccarozơ"), ("Chuẩn độ tự động", "đường cong pH, V tương đương")]
    rows = "".join(f"<tr><td><b>{a}</b></td><td>{b}</td></tr>" for a, b in may)
    than = f"""<div class="luoi" style="grid-template-columns:1fr 1fr 1fr">
  <div class="luoi" style="grid-template-rows:1fr auto;gap:12px">{anh('aas.jpg', 'Máy AAS đo kim loại trong mẫu nước')}
    <div class="the" style="padding:10px 14px"><h3>6 máy chạy thật trên mẫu</h3><table class="bang" style="font-size:12.5px"><tbody>{rows}</tbody></table></div></div>
  <div class="luoi" style="grid-template-rows:1fr auto;gap:12px">{anh('robot_cd.jpg', 'Robot trợ lý làm mẫu chuẩn độ')}
    <div class="the" style="padding:10px 14px"><h3>Robot trợ lý theo kế hoạch AI</h3><ul class="ds" style="font-size:13px">
      <li>Nhận lệnh tiếng Việt tự nhiên, AI lập kế hoạch từ ≈ 30 kỹ năng: lấy hóa chất đúng kệ, rót, nhỏ giọt, cân, đun, đo pH, phân tích…</li>
      <li>Máy chủ kiểm duyệt, loại bước nguy hiểm (khí độc thì đưa vào tủ hút trước).</li></ul></div></div>
  <div class="luoi" style="grid-template-rows:1fr auto;gap:12px">{anh('robot_nguoi.jpg', 'Phòng tự động hóa và robot')}
    <div class="the" style="padding:10px 14px"><h3>AI và web quản lý</h3><ul class="ds" style="font-size:13px">
      <li><b>Mạng lưới khóa AI</b>: nhiều khóa Groq, DeepSeek, Gemini dùng chung, tự xoay vòng, nghỉ khi quá hạn mức; dự phòng Claude.</li>
      <li>Hết khóa vẫn chạy: chú thích soạn sẵn, robot hiểu lệnh cơ bản.</li>
      <li>Web: tổng quan, kho, thiết bị, nhật ký, thử AI.</li></ul></div></div>
</div>"""
    return trang(10, "Tính năng Hóa học · 6", "Phòng máy, robot và trợ lý AI", "Thiết bị hiện đại mà trường phổ thông khó có, ở đây học sinh được đặt mẫu và đọc kết quả tính từ đúng thành phần mẫu.", than)


def bieu_do_fps():
    du = [("Bàn chuẩn độ", 32, 55, 53, 69), ("Bàn định tính", 48, 60, 60, 79), ("Bàn đun nóng", 35, 41, 49, 51),
          ("Phòng hóa chất", 26, 32, 47, 55), ("Toàn cảnh", 33, 36, 50, 57), ("Phòng máy", 58, 66, 63, 95), ("Cả lượt", 41, 48, 60, 70)]
    W, H, x0, y0, bw = 560, 300, 120, 20, 230
    kx = lambda v: x0 + v / 100 * (W - x0 - 20)
    s = [f'<svg viewBox="0 0 {W} {H}" width="{W}" height="{H}" style="display:block">',
         "<style>.l{font:500 12px 'Be Vietnam Pro',sans-serif;fill:#3e5257}.n{font:500 11px 'JetBrains Mono',monospace;fill:#5a6d72}</style>"]
    for v in range(0, 101, 20):
        s.append(f'<line x1="{kx(v)}" y1="{y0}" x2="{kx(v)}" y2="{H - 30}" stroke="#d5dfdd"/><text class="n" x="{kx(v)}" y="{H - 14}" text-anchor="middle">{v}</text>')
    s.append(f'<line x1="{kx(60)}" y1="{y0}" x2="{kx(60)}" y2="{H - 30}" stroke="#3a9a4a" stroke-dasharray="4 3"/><text class="n" x="{kx(60) + 4}" y="{y0 + 10}" style="fill:#3a9a4a">60 FPS</text>')
    for i, (ten, a, b, c, d) in enumerate(du):
        y = y0 + 12 + i * 36
        s.append(f'<text class="l" x="{x0 - 8}" y="{y + 12}" text-anchor="end" style="{"font-weight:700" if ten == "Cả lượt" else ""}">{ten}</text>')
        s.append(f'<rect x="{kx(a)}" y="{y}" width="{kx(b) - kx(a)}" height="9" fill="#c9d4d1"/>')
        s.append(f'<rect x="{kx(c)}" y="{y + 12}" width="{kx(d) - kx(c)}" height="9" fill="#0e7c86"/>')
    s.append("</svg>")
    return "".join(s)


def t11():
    than = f"""<div class="luoi" style="grid-template-columns:600px 1fr">
  <div class="the"><h3>Tốc độ khung hình đo thật (bản .exe, laptop Iris Xe, 1080p)</h3>
    <div class="cap" style="margin-bottom:6px"><span style="display:inline-block;width:12px;height:8px;background:#c9d4d1"></span> trước tối ưu &nbsp; <span style="display:inline-block;width:12px;height:8px;background:#0e7c86"></span> sau tối ưu (khoảng thấp – cao)</div>
    {bieu_do_fps()}
    <ul class="ds" style="font-size:13px;margin-top:6px"><li>Kỹ thuật giữ lại sau khi đo từng cái: occlusion culling + cổng cửa, cửa sổ ảo; tải / gỡ khu theo cửa.</li>
    <li>Cấu hình tối thiểu đã đo: Core i5 thế hệ 11 / Ryzen 5 5500U, card tích hợp, 8 GB RAM.</li></ul></div>
  <div class="luoi" style="grid-template-rows:auto 1fr;gap:14px">
    <div class="the" style="border-top:4px solid var(--vr)"><h3>Chạy trên cả Windows và macOS</h3><ul class="ds" style="font-size:13.5px">
      <li>Một project Unity 6 (6000.6.7f1), build ra <b>Windows x64</b> và <b>macOS (Apple Silicon + Intel)</b>.</li>
      <li>Chung kho mã Git; hai bạn dùng cùng phiên bản Unity, cùng gói.</li>
      <li>Bản build để trên <b>ổ cứng ngoài định dạng exFAT</b>: Mac và Windows đều đọc / ghi được.</li>
      <li>Chỗ khác nhau đã biết: tên cổng nối tiếp (COMx / /dev/cu.*), tường lửa Windows khi nhận UDP, macOS phải cho phép ứng dụng chưa ký lần đầu mở.</li>
      <li>Tay cầm dùng Wi-Fi UDP nên không phụ thuộc hệ điều hành.</li></ul></div>
    <div class="the"><h3>Thước đo cho báo cáo KHKT</h3><ul class="ds" style="font-size:13.5px">
      <li>Độ trễ hình khi quay đầu; độ trôi góc của cảm biến (°/phút); tỉ lệ mất gói.</li>
      <li>Độ chính xác rót 10 ml; số giọt vượt; độ run tay (4–12 Hz).</li>
      <li>Mức say (bảng hỏi SSQ), độ dễ dùng (thang SUS).</li>
      <li>Thực nghiệm 3 nhóm: kính VR / laptop / cách học thường.</li></ul></div>
  </div>
</div>"""
    return trang(11, "Số liệu và kỹ thuật", "Hiệu năng, đa nền tảng, đo đạc", "Mọi kỹ thuật tối ưu đều được đo trước và sau; kỹ thuật nào không nhanh hơn thì tắt.", than)


def t12():
    than = f"""<div class="luoi" style="grid-template-columns:420px 1fr 330px">
  <div class="the" style="padding:14px;background:radial-gradient(90% 80% at 50% 45%,#ffffff,#e8f0f1)"><img src="img/tc3d_a.png" alt="Tay cầm Halo, góc chéo phía trước" style="width:100%;height:100%;object-fit:contain;display:block"></div>
  <div class="luoi" style="grid-template-rows:1fr 1fr;gap:12px">
    <div class="the" style="padding:8px;background:#fff"><img src="img/tc3d_tach.png" alt="Tay cầm Halo tách rời các chi tiết" style="width:100%;height:100%;object-fit:contain;display:block"></div>
    {anh('tc2_03.jpg', 'Bản vẽ TC2-03: cò Hall và điện áp ra theo độ bóp')}
  </div>
  <div class="the" style="border-top:4px solid var(--vr)"><h3>Thông số</h3><ul class="ds" style="font-size:13px">
    <li>120 × 160 × 70 mm, ≈ 135 g kể cả mạch, chưa tính pin.</li>
    <li>Cán <b>37 × 33 mm</b> ngả 20°: theo số đo bàn tay người Việt (đường kính tối ưu = 19,7% chiều dài bàn tay).</li>
    <li><b>ESP32 38 chân</b> + <b>BMI160</b> trong cán; <b>joystick</b> trên mặt ngửa 15°.</li>
    <li><b>Cò analog</b>: nam châm trên cò, cảm biến Hall SS49E cố định; bóp cò thì khe hở 6,5 → 3,8 mm, ESP32 đọc ≈ 465 mức.</li>
    <li><b>Vòng halo Ø70</b> in nhựa huỳnh quang: OpenCV đo xa gần (Z = f·D/d), trục lớn elip không đổi khi nghiêng.</li>
    <li>7 chi tiết in 3D (≈ 75 g nhựa), 5 bản vẽ 2D chiếu từ mô hình.</li>
    <li>Kiểm va chạm tự động: linh kiện xếp chồng không lấn nhau, khe lắp 0,2–0,7 mm, cò không chạm vỏ suốt hành trình.</li></ul></div>
</div>"""
    return trang(12, "Phần cứng", "Bản thiết kế tay cầm VR Halo", "Tay cầm tự thiết kế, in 3D được; mô hình 3D, bản vẽ và file in được sinh từ cùng một script nên luôn khớp nhau.", than)


def mat_bang(tang):
    k = 15
    ox, oy = 30, 30
    X = lambda x: ox + x * k
    Y = lambda z: oy + (15 - z) * k
    s = [f'<svg viewBox="0 0 520 430" width="520" height="430" style="display:block">',
         "<style>.p{stroke:#10242b;stroke-width:1.6}.t{font:700 12.5px 'Barlow Condensed',sans-serif;letter-spacing:.03em;fill:#10242b}"
         ".s{font:500 10px 'Be Vietnam Pro',sans-serif;fill:#3e5257}.d{font:500 10px 'JetBrains Mono',monospace;fill:#0a6b8a}</style>"]

    def phong(x0, z0, x1, z1, mau, ten, phu=(), k_t=12.5):
        s.append(f'<rect class="p" x="{X(x0)}" y="{Y(z1)}" width="{(x1 - x0) * k}" height="{(z1 - z0) * k}" fill="{mau}"/>')
        cx = (X(x0) + X(x1)) / 2
        cy = (Y(z1) + Y(z0)) / 2 - 6 * len(phu)
        s.append(f'<text class="t" x="{cx}" y="{cy}" text-anchor="middle" style="font-size:{k_t}px">{ten}</text>')
        for i, p in enumerate(phu):
            s.append(f'<text class="s" x="{cx}" y="{cy + 14 + i * 12}" text-anchor="middle">{p}</text>')

    def o(x0, z0, w, h, mau="#ffffff"):
        s.append(f'<rect x="{X(x0)}" y="{Y(z0 + h)}" width="{w * k}" height="{h * k}" fill="{mau}" stroke="#5a6d72" stroke-width=".8"/>')

    if tang == 1:
        phong(0, 0, 20, 15, "#e3f2f3", "")
        for i in range(3):
            for j in range(3):
                o(4 + j * 4.2, 3.5 + i * 3.4, 3, 1.4)
        o(0.3, 5, 1.2, 5, "#cfe3e5")
        s.append(f'<text class="s" x="{X(0.9)}" y="{Y(10.6)}" text-anchor="middle">bảng</text>')
        o(17, 12, 2.8, 2.4, "#bfe6ee")
        s.append(f'<text class="s" x="{X(18.4)}" y="{Y(13.4)}" text-anchor="middle">chế tạo</text>')
        o(17.4, 5.5, 2.2, 5, "#f4e4c8")
        s.append(f'<text class="s" x="{X(18.5)}" y="{Y(8.2)}" text-anchor="middle">đun nóng</text>')
        for i in range(3):
            o(4 + i * 4.2, 14.2, 2.6, 0.7, "#d9dfe3")
        s.append(f'<text class="s" x="{X(9)}" y="{Y(13.7)}" text-anchor="middle">3 tủ hút</text>')
        o(14, 0.2, 4, 0.9, "#e6e1f4")
        s.append(f'<text class="s" x="{X(16)}" y="{Y(1.6)}" text-anchor="middle">thiết bị phân tích</text>')
        o(0.2, 0.2, 2.6, 2.2, "#f6d9d2")
        s.append(f'<text class="s" x="{X(1.5)}" y="{Y(1.0)}" text-anchor="middle">an toàn</text>')
        s.append(f'<text class="t" x="{X(8)}" y="{Y(2.4)}" text-anchor="middle" style="font-size:15px">PHÒNG THÍ NGHIỆM HÓA</text>')
        s.append(f'<text class="s" x="{X(8)}" y="{Y(2.4) + 13}" text-anchor="middle">300 m² · 9 bàn học sinh</text>')
        phong(20, 0, 27, 15, "#f8ecd5", "PHÒNG HÓA CHẤT", ("27 kệ / tủ", "720 hóa chất", "vách kính mờ"))
        phong(0, -9, 13.5, 0, "#eef4dc", "PHÒNG MÁY PHÂN TÍCH", ("UV-Vis · FTIR · AAS", "GC-MS · HPLC", "chuẩn độ tự động"))
        phong(13.5, -9, 27, 0, "#fbe3dc", "TỰ ĐỘNG HÓA & ROBOT", ("robot trợ lý, cobot", "góc kính VR, máy in 3D"))
    else:
        phong(0, 0, 20, 15, "#e4f2e6", "")
        for i in range(3):
            for j in range(3):
                o(4 + j * 4.2, 3.5 + i * 3.4, 3, 1.4)
                s.append(f'<circle cx="{X(5.5 + j * 4.2)}" cy="{Y(4.2 + i * 3.4)}" r="3" fill="#3a9a4a"/>')
        o(0.3, 5, 1.2, 5, "#d3e8d6")
        s.append(f'<text class="s" x="{X(0.9)}" y="{Y(10.6)}" text-anchor="middle">bảng</text>')
        o(17.2, 9, 2.6, 4.5, "#cfe7d3")
        s.append(f'<text class="s" x="{X(18.5)}" y="{Y(11.3)}" text-anchor="middle">giải phẫu</text>')
        o(17.2, 3, 2.6, 4.5, "#f1ead0")
        s.append(f'<text class="s" x="{X(18.5)}" y="{Y(5.3)}" text-anchor="middle">tủ ấm, hấp</text>')
        for i in range(2):
            o(5 + i * 5, 14.2, 3.2, 0.7, "#d9dfe3")
        s.append(f'<text class="s" x="{X(9.5)}" y="{Y(13.7)}" text-anchor="middle">tủ an toàn sinh học</text>')
        o(0.2, 0.2, 2.6, 2.2, "#f6d9d2")
        s.append(f'<text class="s" x="{X(1.5)}" y="{Y(1.0)}" text-anchor="middle">an toàn</text>')
        s.append(f'<text class="t" x="{X(9)}" y="{Y(2.4)}" text-anchor="middle" style="font-size:15px">PHÒNG THÍ NGHIỆM SINH HỌC</text>')
        s.append(f'<text class="s" x="{X(9)}" y="{Y(2.4) + 13}" text-anchor="middle">300 m² · 9 bàn kính hiển vi</text>')
        phong(20, 7.5, 27, 15, "#dcefe9", "PHÒNG VI SINH", ("vùng sạch", "nuôi cấy, Gram"))
        phong(20, 0, 27, 7.5, "#e3ecf7", "KHO SINH PHẨM", ("tủ lạnh 4 / −20 °C", "mẫu vật, mô hình"))
        phong(0, -9, 13.5, 0, "#efe9fb", "SINH HỌC PHÂN TỬ", ("tách chiết ADN", "PCR · điện di"))
        phong(13.5, -9, 27, 0, "#e2f3d8", "NHÀ KÍNH THỰC VẬT", ("quang hợp, thoát hơi nước", "trồng cây thí nghiệm"))
    # lõi thang và hành lang
    phong(27, 0, 30, 6, "#d9dfe3", "THANG", ("bộ + máy",), k_t=11)
    s.append(f'<rect class="p" x="{X(27)}" y="{Y(15)}" width="{3 * k}" height="{9 * k}" fill="#eef1f2"/>')
    s.append(f'<text class="s" x="{X(28.5)}" y="{Y(10.5)}" text-anchor="middle" transform="rotate(-90 {X(28.5)} {Y(10.5)})">hành lang</text>')
    s.append(f'<line x1="{X(0)}" y1="{Y(-9) + 14}" x2="{X(27)}" y2="{Y(-9) + 14}" stroke="#0a6b8a"/>')
    s.append(f'<text class="d" x="{X(13.5)}" y="{Y(-9) + 26}" text-anchor="middle">27 m</text>')
    s.append(f'<line x1="{X(0) - 14}" y1="{Y(15)}" x2="{X(0) - 14}" y2="{Y(-9)}" stroke="#0a6b8a"/>')
    s.append(f'<text class="d" x="{X(0) - 18}" y="{Y(3)}" text-anchor="middle" transform="rotate(-90 {X(0) - 18} {Y(3)})">24 m</text>')
    s.append("</svg>")
    return "".join(s)


def t13():
    than = f"""<div class="luoi" style="grid-template-columns:1fr 1fr">
  <div class="the" style="padding:10px 14px"><div style="display:flex;justify-content:space-between;align-items:baseline"><h3 style="margin:0">Tầng 1 · Khu Hóa học</h3>{chip('Xong')}</div>{mat_bang(1)}</div>
  <div class="the" style="padding:10px 14px"><div style="display:flex;justify-content:space-between;align-items:baseline"><h3 style="margin:0">Tầng 2 · Khu Sinh học</h3>{chip('Kế hoạch')}</div>{mat_bang(2)}</div>
</div>"""
    return trang(13, "Quy hoạch", "Bản đồ quy hoạch dự kiến 2 tầng", "Tầng 2 dùng cùng khung nhà với tầng 1 (27 × 24 m), nối bằng lõi thang bộ và thang máy; mỗi tầng tải riêng như các khu hiện có.", than)


def t14():
    sinh = [("Quan sát tế bào biểu bì hành, khí khổng; co và phản co nguyên sinh", "kính hiển vi ảo, phóng to bằng tay cầm"),
            ("Enzyme: catalase (H₂O₂ + gan, khoai), amylase với tinh bột", "dùng lại bộ máy hóa (pH, nhiệt độ, tốc độ)"),
            ("Tách chiết sắc tố quang hợp, sắc ký giấy", "dùng lại mạng dòng chảy, màu theo Beer"),
            ("Quang hợp, thoát hơi nước ở nhà kính", "đo O₂, khối lượng theo thời gian"),
            ("Hô hấp, lên men: nấm men sinh CO₂", "sục khí vào nước vôi trong như Hóa"),
            ("Tách chiết ADN (chuối, gan gà), điện di", "phòng sinh học phân tử"),
            ("Nguyên phân ở rễ hành; lai Mendel mô phỏng", "đếm tế bào, sơ đồ lai tự sinh"),
            ("Kỹ thuật vô trùng, nuôi cấy, nhuộm Gram", "phòng vi sinh, lỗi nhiễm khuẩn được ghi lại")]
    rows = "".join(f"<tr><td>{a}</td><td style='color:#5a6d72'>{b}</td></tr>" for a, b in sinh)
    than = f"""<div class="luoi" style="grid-template-columns:1.6fr 1fr">
  <div class="the" style="border-top:4px solid var(--sinh)"><h3>Lab Sinh học: bài dự kiến theo chương trình THPT</h3>
    <table class="bang" style="font-size:13px"><thead><tr><th>Thí nghiệm</th><th>Cách làm trong VERA Lab</th></tr></thead><tbody>{rows}</tbody></table>
    <p style="font-size:13px;margin-top:8px">Khung lab (tải khu, thẻ chú thích, robot, AI, ghi thao tác) dùng chung với Hóa, nên phần Sinh chủ yếu là thêm dụng cụ, mẫu vật và mô hình sinh học.</p></div>
  <div class="luoi" style="grid-template-rows:auto 1fr;gap:14px">
    <div class="the" style="border-top:4px solid var(--ly)"><h3>Định hướng Vật lý</h3><ul class="ds" style="font-size:13.5px">
      <li>Con lắc đơn, lò xo: đo chu kỳ, gia tốc trọng trường.</li>
      <li>Quang học: thấu kính, khúc xạ, giao thoa.</li>
      <li>Mạch điện: lắp mạch theo khớp nối giống khu chế tạo.</li>
      <li>Tay cầm BMI160 làm cảm biến gia tốc thật cho bài chuyển động.</li></ul></div>
    <div class="the"><h3>Dùng lại được gì từ phần Hóa</h3><ul class="ds" style="font-size:13.5px">
      <li>Bộ máy phản ứng, mạng dòng chảy, hệ thống chất lỏng theo mol.</li>
      <li>Khu chế tạo, khớp nối chuẩn, kiểm thử tự động.</li>
      <li>Tay cầm, robot, AI, web, đo kỹ năng.</li></ul></div>
  </div>
</div>"""
    return trang(14, "Mở rộng", "Lab Sinh học và định hướng Vật lý", "Mở rộng từ phòng Hóa sang Hóa – Sinh, sau đó là Lý, trên cùng một nền tảng.", than)


def t15():
    than = f"""<div class="luoi" style="grid-template-columns:1fr 1fr 1fr">
  <div class="the" style="border-top:4px solid var(--cam)"><h3>Web LMS cho giáo viên, học sinh</h3><ul class="ds" style="font-size:14.5px">
    <li><b>Giao bài</b>: chọn thí nghiệm, hạn nộp, tiêu chí; chấm tự động từ nhật ký thao tác (số giọt vượt, lỗi an toàn, sai số).</li>
    <li><b>Thảo luận</b> theo lớp, nhóm, bài; đính kèm ảnh chụp trong lab.</li>
    <li><b>Chia nhóm</b> tự động hoặc tay; chia vai (giữ khóa buret – lắc bình).</li>
    <li><b>Minh họa</b>: video, mô hình 3D, phát lại thao tác mẫu của giáo viên.</li>
    <li><b>Học liệu</b>: SGK, phiếu học tập, tài liệu thiết bị.</li>
    <li><b>Giới thiệu</b> dự án; <b>quy tắc</b> phòng lab với bài kiểm tra an toàn trước khi vào.</li>
    <li>Tài khoản giáo viên / học sinh / quản trị; bảng kỹ năng từng em.</li></ul></div>
  <div class="the" style="border-top:4px solid var(--ly)"><h3>Máy chủ nhiều người cùng lab</h3><ul class="ds" style="font-size:14.5px">
    <li><b>Máy chủ quyết định</b> trạng thái hóa chất (server-authoritative), để mọi máy thấy cùng một kết quả.</li>
    <li>Đồng bộ <b>trạng thái theo mol</b> khoảng 10 lần/giây và các sự kiện phản ứng, không gửi từng giọt.</li>
    <li>Phương án đang so sánh: Unity Netcode for GameObjects chạy trên <b>một máy trong mạng LAN của trường</b> (không cần Internet); Photon Fusion (đám mây); FishNet / Mirror (tự host).</li>
    <li><b>Giữ quyền vật</b>: bạn nào đang cầm dụng cụ thì máy khác không lấy được, tránh hai người cùng rót một bình.</li>
    <li>Chia vai trong nhóm 2–4 bạn; giáo viên quan sát cả lớp.</li></ul></div>
  <div class="the" style="border-top:4px solid var(--vr)"><h3>AI chạy cục bộ</h3><ul class="ds" style="font-size:14.5px">
    <li><b>Ollama / llama.cpp</b> chạy mô hình mở 7–8B lượng tử 4 bit (ví dụ Qwen2.5, Llama 3.1) trên máy chủ trường: card 8–12 GB hoặc Mac Apple Silicon 16 GB.</li>
    <li>Tra cứu tài liệu (RAG) trên SGK, phiếu an toàn hóa chất của dự án.</li>
    <li>Nhận giọng nói tiếng Việt (Whisper) để hỏi AI bằng lời.</li>
    <li>Thứ tự dùng: cục bộ trước, đám mây khi cần; dữ liệu học sinh ở lại trong trường.</li>
    <li>Đo và so sánh độ trễ, độ đúng câu trả lời giữa AI cục bộ và AI đám mây trên cùng bộ câu hỏi.</li></ul></div>
</div>"""
    return trang(15, "Bước tiếp theo", "Web LMS, nhiều người, AI cục bộ", "Ba phần nền tảng giúp dùng VERA Lab cho cả lớp học thật.", than)


def t16():
    gd = [("1", "Hoàn thiện Hóa", "Các bài còn lại; thử với học sinh; gắn kính VR; chỉnh theo phản hồi"),
          ("2", "Tay cầm Halo", "In 3D, lắp mạch, firmware ESP32 (cò Hall, BMI160), OpenCV đo xa gần; đo độ trễ, độ chính xác"),
          ("3", "Web LMS", "Tài khoản, giao bài, chia nhóm, thảo luận, học liệu, quy tắc, chấm từ nhật ký"),
          ("4", "Lab Sinh học", "Dựng tầng 2; kính hiển vi ảo; 6–8 bài đầu tiên"),
          ("5", "Nhiều người + AI cục bộ", "Máy chủ LAN; đồng bộ trạng thái; mô hình AI chạy trên máy trường"),
          ("6", "Vật lý, thực nghiệm sư phạm", "Bài Lý đầu tiên; thực nghiệm 3 nhóm, SSQ, SUS; viết báo cáo")]
    o = "".join(f"""<div class="the" style="border-top:4px solid {['#0e7c86', '#11a8c8', '#d98b12', '#3a9a4a', '#6b5bd6', '#6b5bd6'][i]};padding:12px 14px">
      <div style="display:flex;gap:10px;align-items:baseline"><span class="so-lon" style="font-size:30px">{n}</span><b style="font:700 17px var(--f-tieu);text-transform:uppercase;letter-spacing:.04em">{t}</b></div>
      <p style="font-size:13px;margin:6px 0 0">{m}</p></div>""" for i, (n, t, m) in enumerate(gd))
    than = f"""<div class="luoi" style="grid-template-rows:auto 1fr;gap:16px">
  <div class="luoi" style="grid-template-columns:repeat(6,1fr);gap:12px">{o}</div>
  <div class="luoi" style="grid-template-columns:1.2fr 1fr">
    <div class="the"><h3>Đóng góp mới của dự án</h3><ul class="ds" style="font-size:13.5px">
      <li>Phản ứng tính bằng <b>bộ máy hóa học</b> từ thành phần thật, không phải hoạt hình làm sẵn.</li>
      <li><b>Lắp ráp và gia công dụng cụ</b> tự do, bộ lắp chạy được như thật.</li>
      <li><b>Tay cầm tự chế</b> tích hợp <b>cảm biến quán tính</b> (IMU), <b>cần điều khiển</b>, cò analog Hall và vòng halo đo vị trí bằng webcam. Mục tiêu là một <b>hệ thống tracking toàn diện</b> với <b>chi phí có lợi thế kinh tế cạnh tranh</b>, để <b>phổ cập rộng rãi</b>.</li>
      <li><b>AI lai</b> đám mây / cục bộ, có máy chủ kiểm duyệt an toàn.</li>
      <li>Đo kỹ năng thao tác bằng số liệu, so sánh có đối chứng.</li>
      <li><b>Nhiều người cùng tham gia một lúc</b> trong cùng phòng lab.</li>
      <li>Chạy được trên <b>phần cứng tầm trung</b>.</li>
      <li><b>Tích hợp liên môn</b> Hóa – Sinh – Lý trong <b>không gian rộng rãi</b>.</li></ul></div>
    <div class="the" style="background:var(--toi);color:#e8f2f3;border-color:var(--toi)">
      <div style="font:700 44px/1 var(--f-tieu);margin-top:8px">VERA <span style="color:#3fd0ea">LAB</span></div>
      <p style="color:#9fc3c9;margin-top:10px">{TEN_DAY_DU}</p>
      <div style="margin-top:22px;font:600 18px var(--f-than)">Nguyễn Ngọc Bảo Trọng</div>
      <div style="font:600 18px var(--f-than)">Trần Đức Trọng</div>
      <p style="color:#9fc3c9;margin-top:18px">Cảm ơn thầy cô đã lắng nghe. Nhóm rất mong nhận được góp ý.</p></div>
  </div>
</div>"""
    return trang(16, "Lộ trình", "Lộ trình kế tiếp", "Thứ tự ưu tiên các giai đoạn; mỗi giai đoạn kết thúc bằng một bản chạy được và số liệu đo.", than)


def main():
    out = sys.argv[1]
    html = ("<!doctype html><html lang='vi'><head><meta charset='utf-8'><title>VERA Lab – trình bày</title>"
            "<link rel='stylesheet' href='https://fonts.googleapis.com/css2?family=Barlow+Condensed:wght@500;600;700&family=Be+Vietnam+Pro:ital,wght@0,400;0,500;0,600;0,700;1,400&family=JetBrains+Mono:wght@500;700&display=swap'>"
            f"<style>{CSS}</style></head><body>"
            + "".join(f() for f in (t01, t02, t03, t04, t05, t06, t07, t08, t09, t10, t11, t12, t13, t14, t15, t16))
            + "</body></html>")
    p = os.path.join(out, "trinh_bay.html")
    with open(p, "w", encoding="utf-8") as fh:
        fh.write(html)
    print(p)


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    main()
