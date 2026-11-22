"""Trang xem 3D tay cầm VERA Band 2 (three.js) để duyệt hình dáng, kèm so sánh với Band và kết quả khử nhiễu cảm biến.

Chạy sau mo_hinh_band2.py:  python tools/tay_cam_band2/trang.py <thư mục xem có canh.json>
Trang cần thêm 2 ảnh cạnh nó: so_sanh.png (Band và Band 2), troi_yaw.png (docs/tay-cam-tich-hop/mo_phong_troi_yaw.png)
Ra: <thư mục xem>/vera_band.html + mo_hinh.json (mở qua máy chủ http vì trang tải file bên cạnh)
"""
import base64
import json
import os
import sys


def bang(head, rows):
    th = "".join(f"<th>{h}</th>" for h in head)
    tr = "".join("<tr>" + "".join(f"<td>{c}</td>" for c in r) + "</tr>" for r in rows)
    return f'<div class="tbl"><table><thead><tr>{th}</tr></thead><tbody>{tr}</tbody></table></div>'


def main():
    thu_muc = sys.argv[1]
    with open(os.path.join(thu_muc, "canh.json"), encoding="utf-8") as fh:
        canh = json.load(fh)
    in_rows = canh["goi_in"]
    kt = canh["kich_thuoc"]
    bang_in = bang(["File STL", "Chi tiết trong file", "Màu đề xuất", "Khổ trên bàn in (mm)", "Nhựa"],
                   [(f"<code>{c['file']}.stl</code>", c["ten"], c["mau"], " × ".join(f"{v:g}" for v in c["kich_thuoc"]), f"{c['gam_pla']:g} g") for c in in_rows])
    goi = {}
    for ten in [c["ma"] for c in canh["chi_tiet"]] + ["ban_tay"]:
        with open(os.path.join(thu_muc, ten + ".stl"), "rb") as fh:
            goi[ten] = base64.b64encode(fh.read()).decode("ascii")
    with open(os.path.join(thu_muc, "mo_hinh.json"), "w", encoding="ascii") as fh:
        json.dump(goi, fh)
    canh_js = json.dumps(canh, ensure_ascii=False)
    bang_doi = bang(["Hạng mục", "VERA Band", "VERA Band 2", "Vì sao"], [
        ("Vùng nắm cho 3 ngón dưới", "47 mm", "<b>53 mm</b>", "3 ngón nữ P95 cần 51,8 mm, nam P95 ≈ 54,7 mm"),
        ("Khe khuyên quai ↔ gốc ngón cái (tay mô phỏng)", "0,42 mm", "<b>4,68 mm</b>", "khuyên dời ra trước 6 mm, chân bo tròn"),
        ("Đế phình (chỗ ngón út)", "cao 17 mm", "<b>cao 12 mm</b>", "tay to không còn tì ngón út lên đế"),
        ("Sườn lòng bàn tay", "trụ elip đều", "<b>bướu đỡ nhô 2,4 mm</b>", "lấp chỗ trũng giữa lòng bàn tay"),
        ("Sườn phía các ngón", "nhẵn", "<b>vân lục giác nổi 0,45 mm</b>", "chống trượt khi tay ra mồ hôi, gợi logo"),
        ("Pin 9V ↔ vách trong", "1,02 mm", "0,96 mm", "vẫn đủ chỗ, pin hạ thấp 6 mm"),
        ("Khối lượng ước tính", "141 g (51 g nhựa)", f"{canh['tong_g']} g ({canh['nhua_g']} g nhựa)", "chưa cân thật"),
        ("Kích thước cả bộ", "78 × 78 × 122 mm", " × ".join(str(v) for v in kt) + " mm", "")])

    def hang_cb(r):
        return [r[0], r[1], f'<span class="tt {r[3]}">{r[2]}</span>']
    bang_that = bang(["Hạng mục", "Kết quả trên tay cầm thật (COM8, 11/11/2026)", "Đánh giá"], [hang_cb(r) for r in [
        ("Trôi yaw khi nằm yên 10 phút", "0,07° (lần 1); 0,008° từ phút 2 đến 10 (lần 2)", "Đạt, dưới 1°", "ok"),
        ("Nhiễu con quay (ARW)", "0,36–0,45 °/√h ≈ 0,007 °/s/√Hz, tốt hơn datasheet 0,008", "Đạt", "ok"),
        ("Lấy mẫu IMU", "200,0 Hz, dao động chu kỳ 0,077 ms; đang chạy bằng đồng hồ vì dây INT1 chưa có xung", "Đạt, cần kiểm dây INT1", "ok"),
        ("Kết nối USB", "50 gói/s, mất 0 trên 360 000 mẫu", "Đạt", "ok"),
        ("2 nút cảm ứng lúc không chạm", "nhiễu 0,08% mức nền, 0 lần chạm ma", "Đạt", "ok"),
        ("Độ trễ", "tuổi dữ liệu 12,6 ms (P95 21,9 ms), khứ hồi lệnh USB 16,5 ms", "Đạt", "ok"),
        ("7 bài thao tác, cảm biến thật, joystick đã sửa dây", "23/23 chỉ số; sai yaw TB 0,68°, lớn nhất 3,2°", "Đạt", "ok"),
        ("Joystick Y", "dây VRy → GPIO35 chập chờn, nhân vật tự đi 8 m trong 3 phút", "Chưa đạt, lỗi đấu nối", "loi"),
        ("Cảm ứng khi chạm, Wi-Fi", "chưa đo", "Chưa đo", "cho")]])
    bang_mp = bang(["Mã", "Cách lọc", "Có đặt xuống bàn", "Cầm liên tục"], [
        ("M0", "Madgwick, không bù sai số tĩnh", "85,2°", "88,8°"),
        ("M1", "Madgwick + bù sai số tĩnh lúc bật", "39,9°", "46,4°"),
        ("R2", "Mahony Ki = 0,02 + bù sai số tĩnh", "34,3° (−14%)", "36,2° (−22%)"),
        ("M2", "Firmware cũ: M1 + học lại khi nằm yên", "7,9°", "45,4°"),
        ("<b>F1</b>", "<b>Firmware mới: Madgwick + học lại + bù nhiệt + vùng chết + hiệu chỉnh tỉ lệ</b>", "<b>1,7° (−96%)</b>", "<b>4,5° (−90%)</b>")])
    phan_cam_bien = f"""<div class="card"><h3>Kết quả khử nhiễu, chống trôi cảm biến BMI160</h3>
    <p style="margin:0 0 10px">BMI160 không có la bàn, nên sai số con quay dồn thành trôi hướng (yaw). Firmware mới dùng 5 bước: bù sai số tĩnh lúc bật, học lại độ lệch mỗi khi nằm yên quá 0,5 giây, bù nhiệt độ, vùng chết mềm dưới 1 °/s, hiệu chỉnh tỉ lệ con quay trục Z.</p>
    {bang_that}
    <h3 style="margin-top:18px">Mô phỏng: sai lệch yaw trung bình sau 10 phút, 24 lần mỗi kịch bản</h3>
    {bang_mp}
    <ul class="notes" style="margin-top:12px">
      <li>Phần trăm so với M1. So với firmware cũ M2, firmware mới giảm 78% khi có đặt xuống bàn và 90% khi cầm liên tục.</li>
      <li>Mô phỏng dùng nhiễu đo từ mạch thật, còn cử động tay là kịch bản dựng sẵn. Trôi khi cầm tay thật chưa đo.</li>
      <li>Mahony tự học độ lệch (Ki) và ZARU đều học nhầm cử động tay thành độ lệch, nên đã bỏ.</li>
    </ul>
    <img src="troi_yaw.png" alt="Sai lệch yaw theo thời gian của các cách lọc" style="margin-top:14px;width:100%;max-width:1000px;background:#fff">
  </div>"""
    bang_cong = bang(["Cổng", "Vị trí", "Chân ESP32", "Dùng được cho"],
                     [("P1", "Mặt tối, phía trái, ngón cái", "GPIO13", "Nút bấm, cảm ứng chạm, cảm biến tín hiệu số"),
                      ("P2", "Mặt tối, phía phải, ngón cái", "GPIO14", "Nút bấm, cảm ứng chạm, cảm biến tín hiệu số"),
                      ("P3", "Mũi đầu, phía trái", "GPIO4", "Nút bấm, cảm ứng chạm, cảm biến tín hiệu số"),
                      ("P4", "Mũi đầu, phía phải", "GPIO36", "Cảm biến analog; làm nút thì thêm điện trở kéo lên 10k")])

    html = f"""<meta charset="utf-8">
<title>VERA Band 2</title>
<meta name="claude:viewport-fit-sides" content="cover">
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Barlow+Condensed:wght@500;600;700&family=Be+Vietnam+Pro:wght@400;500;600;700&family=JetBrains+Mono:wght@500;700&display=swap">
<style>
/* bố cục: sân khấu tối toàn bề ngang như ảnh ra mắt sản phẩm; dưới là thông số trên nền sáng */
:root{{
  --bg:#e8edec; --sheet:#fbfcfa; --ink:#15232a; --ink-2:#5b6c71; --rule:#c6d1cf; --warn:#b4471a;
  --halo:#3fd4ee; --ok:#1f7a55; --link:#0b7f99; --stage-1:#070d10; --stage-2:#14242b; --stage-ink:#e6f1f3; --stage-ink-2:#8fa8ae;
  --f-disp:'Barlow Condensed','Arial Narrow',sans-serif; --f-body:'Be Vietnam Pro','Segoe UI',sans-serif; --f-mono:'JetBrains Mono',ui-monospace,monospace;
}}
@media (prefers-color-scheme: dark){{ :root:not([data-theme="light"]){{ --bg:#0d1517; --sheet:#132023; --ink:#dbe6e4; --ink-2:#93a5a8; --rule:#2a3b3e; --warn:#f08a5d; --ok:#5fd39a; --link:#5fd0e8; color-scheme:dark }} }}
:root[data-theme="dark"]{{ --bg:#0d1517; --sheet:#132023; --ink:#dbe6e4; --ink-2:#93a5a8; --rule:#2a3b3e; --warn:#f08a5d; --ok:#5fd39a; --link:#5fd0e8; color-scheme:dark }}
body{{background:var(--bg);color:var(--ink);font:400 15px/1.6 var(--f-body)}}
.hero{{background:radial-gradient(90% 80% at 42% 45%,var(--stage-2),var(--stage-1));color:var(--stage-ink)}}
.hero-in{{max-width:1320px;margin:0 auto;padding-block:24px 0;padding-left:max(16px,env(safe-area-inset-left,0px));padding-right:max(16px,env(safe-area-inset-right,0px));display:grid;grid-template-columns:minmax(0,1fr) 280px;gap:0 24px}}
.hero h1{{font:600 clamp(40px,7vw,76px)/.95 var(--f-disp);letter-spacing:.02em;margin:4px 0 10px;text-transform:uppercase}}
.hero h1 b{{color:var(--halo);font-weight:600}}
.lab{{font:600 11px var(--f-disp);letter-spacing:.14em;text-transform:uppercase;color:var(--stage-ink-2)}}
.hero p{{color:var(--stage-ink-2);max-width:60ch;margin:0}}
.stage{{position:relative;grid-column:1/2;height:min(68vh,640px);min-height:420px}}
.stage canvas{{display:block;width:100%;height:100%;cursor:grab}}
.loading{{position:absolute;left:0;bottom:16px;font:500 13px var(--f-body);color:var(--stage-ink-2)}}
.panel{{grid-column:2/3;grid-row:1/3;border-left:1px solid rgba(143,168,174,.25);padding:18px 0 22px 22px;display:grid;gap:16px;align-content:start}}
.panel label{{display:flex;gap:10px;align-items:center;font:500 14px var(--f-body);cursor:pointer;color:var(--stage-ink)}}
.panel input[type=range]{{width:100%;accent-color:var(--halo)}}
.panel input[type=checkbox]{{accent-color:var(--halo);width:16px;height:16px}}
.nut{{display:grid;grid-template-columns:1fr 1fr;gap:6px}}
.nut button{{font:600 13px var(--f-body);color:var(--stage-ink);background:rgba(143,168,174,.14);border:1px solid rgba(143,168,174,.35);padding:6px 8px;cursor:pointer}}
.nut button:hover,.nut button:focus-visible{{background:rgba(143,168,174,.3);outline:none}}
.hint{{font-size:12.5px;color:var(--stage-ink-2);margin:0}}
.specs{{grid-column:1/2;display:grid;grid-template-columns:repeat(auto-fit,minmax(130px,1fr));border-top:1px solid rgba(143,168,174,.25)}}
.specs div{{padding:12px 14px 16px 0}}
.specs b{{display:block;font:600 26px/1.1 var(--f-disp);color:var(--stage-ink);font-variant-numeric:tabular-nums}}
.specs span{{font-size:12.5px;color:var(--stage-ink-2)}}
.wrap{{max-width:1320px;margin:0 auto;padding-block:36px 64px;padding-left:max(16px,env(safe-area-inset-left,0px));padding-right:max(16px,env(safe-area-inset-right,0px));display:grid;gap:20px}}
.cols{{display:grid;grid-template-columns:repeat(auto-fit,minmax(min(100%,400px),1fr));gap:20px}}
.card{{background:var(--sheet);border:1px solid var(--rule);padding:16px 18px;min-width:0}}
.card h3{{font:600 15px var(--f-body);margin:0 0 8px}}
.tbl{{overflow-x:auto}}
table{{border-collapse:collapse;width:100%;font-size:13.5px}}
th{{text-align:left;font:600 11px var(--f-disp);letter-spacing:.1em;text-transform:uppercase;color:var(--ink-2);border-bottom:1.5px solid var(--ink);padding:6px 10px 6px 0}}
td{{border-bottom:1px solid var(--rule);padding:7px 10px 7px 0;vertical-align:top;font-variant-numeric:tabular-nums}}
code{{font:500 12.5px var(--f-mono)}}
.notes{{margin:0;padding-left:20px;display:grid;gap:6px}}
.warn{{border-left:3px solid var(--warn)}}
.tt{{font-weight:600}} .tt.ok{{color:var(--ok)}} .tt.loi{{color:var(--warn)}} .tt.cho{{color:var(--ink-2)}}
a{{color:var(--link)}}
@media (max-width:860px){{ .hero-in{{grid-template-columns:1fr}} .stage{{order:1;height:60vh}} .specs{{order:2}} .panel{{order:3;grid-column:1;grid-row:auto;border-left:0;border-top:1px solid rgba(143,168,174,.25);padding:16px 0}} }}
</style>

<div class="hero"><div class="hero-in">
  <div>
    <span class="lab">VERA LAB · Le Quy Don</span>
    <h1>VERA <b>Band 2</b></h1>
    <p>Bản nháp cải tiến từ VERA Band, chờ duyệt dáng. Cán dài hơn cho đủ 3 ngón tay to, bướu đỡ lòng bàn tay, vân lục giác chống trượt phía các ngón, đế thấp lại, khuyên quai bo tròn và lùi xa gốc ngón cái. Giữ nguyên linh kiện, pin vuông 9V và cách đeo.</p>
  </div>
  <aside class="panel">
    <div class="nut"><button id="vTruoc">Chéo trước</button><button id="vSau">Chéo sau</button><button id="vThang">Mặt bên</button><button id="vDay">Dưới đáy</button></div>
    <div><span class="lab">Tách rời</span><input type="range" id="tach" min="0" max="1" step="0.01" value="0" aria-label="Tách rời các chi tiết"></div>
    <div><span class="lab">Bóp cò</span><input type="range" id="bop" min="0" max="1" step="0.01" value="0" aria-label="Bóp cò"></div>
    <div style="display:grid;gap:7px">
      <label><input type="checkbox" id="quay" checked>Tự xoay</label>
      <label><input type="checkbox" id="trongSuot">Nhìn xuyên vỏ</label>
      <label><input type="checkbox" id="hienTay">Đặt trong tay</label>
    </div>
    <p class="hint">Kéo để xoay, cuộn để phóng to. Bàn tay chỉ là hình mô phỏng để so cỡ.</p>
  </aside>
  <div class="stage" id="stage"><div class="loading" id="tai">Đang tải mô hình 3D…</div></div>
  <div class="specs">
    <div><b>{kt[0]} × {kt[1]} × {kt[2]}</b><span>mm, kể cả khuyên quai và cò</span></div>
    <div><b>≈ {canh['tong_g']} g</b><span>có pin, chưa cân thật (mục tiêu dưới 500 g)</span></div>
    <div><b>{canh['nhua_g']} g</b><span>nhựa, gói in {len(in_rows)} file STL</span></div>
    <div><b>{canh['rong_quai']:g} mm</b><span>quai băng gai dính, chỉnh được độ chặt</span></div>
  </div>
</div></div>

<div class="wrap">
  <div class="card"><h3>Band 2 khác Band ở đâu</h3>{bang_doi}
    <ul class="notes" style="margin-top:12px">
      <li>Mọi số trong bảng đo trên mô hình bằng <code>kiem_va_cham.py</code> và <code>do_khoang_ho.py</code>. Bàn tay mô phỏng chỉ gần đúng, chưa thử trên tay thật.</li>
      <li>Kiểm va chạm: không có va chạm ngoài ý muốn; cò vẫn bóp đủ 13°, khe quai ở đế và ở tấm mu tay vẫn thông.</li>
    </ul>
    <img src="so_sanh.png" alt="Band (trên) và Band 2 (dưới) nhìn cùng 3 góc" style="margin-top:14px;width:100%;max-width:1100px;background:#fff">
  </div>
  <div class="card"><h3>Căn cứ thiết kế</h3><ul class="notes">
    <li>Tay cầm theo hình bàn tay được chấm thoải mái hơn hẳn tay cầm trụ tròn; tay cầm trụ dồn áp lực không đều lên lòng bàn tay. Vì vậy thêm bướu đỡ lòng tay thay vì giữ cán trụ elip đều. Nguồn: <a href="https://scinapse.io/papers/2090163216">so sánh tay cầm theo giải phẫu và trụ tròn</a>, <a href="https://yadda.icm.edu.pl/baztech/element/bwmeta1.element.baztech-2bd6e585-8ea3-4109-ba1b-bd963572f7d9/c/Kong.pdf">Kong và cs. (tay cầm móc thịt: hai nón cụt Ø37 được ưa nhất)</a>.</li>
    <li>Rãnh khía ngón chỉ vừa một cỡ tay, nên dùng vân nổi thấp 0,45 mm thay cho rãnh ngón. Một nghiên cứu ngoài đồng còn thấy dáng hai nón cụt làm lòng tay khó chịu, nên bướu chỉ nhô 2,4 mm.</li>
    <li>Đường kính cán vẫn theo Kong &amp; Lowe (2005): tối ưu khoảng 19,7% chiều dài bàn tay, <a href="https://stacks.cdc.gov/view/cdc/197408">stacks.cdc.gov/view/cdc/197408</a>. Pin vuông 9V buộc cán không nhỏ hơn được: thân hiện ôm góc pin chỉ dư khoảng 1 mm.</li>
    <li>Quai: tay cầm Valve Index cũng giữ tay bằng quai sau đốt ngón, chỉnh được độ chặt, mở hết các ngón tay cầm vẫn nằm yên. <a href="https://help.steampowered.com/en/faqs/view/699A-ECD2-F839-760C">Hướng dẫn đeo của Steam</a>.</li>
    <li>Cổ tay thẳng cho lực nắm lớn nhất. Band 2 chưa đổi góc cán, vì hướng chỉ trong game lấy từ con quay đã căn lại (F12), không từ góc cán. <a href="https://stacks.cdc.gov/view/cdc/226351/cdc_226351_DS1.pdf">NIOSH</a>.</li>
    <li>Số đo tay người Việt dùng lại từ bản Band: Bùi Văn Huấn, Đỗ Thị Hoa Ngà (2020), <a href="https://storage.googleapis.com/jst-journals/articles/140/005-19024.pdf">Tạp chí KH&amp;CN 140</a>; Imrhan, Nguyen &amp; Nguyen (1993).</li>
  </ul></div>
  {phan_cam_bien}
  <div class="cols">
    <div class="card"><h3>Cách đeo</h3><ul class="notes">
      <li>Gỡ đầu dưới quai, luồn tay phải vào giữa tay cầm và quai, cầm như bình thường.</li>
      <li>Kéo đầu quai đi qua khe ở đế, gập ngược lên rồi dán gai cho vừa tay. Mở các ngón ra, tay cầm vẫn nằm trong lòng bàn tay.</li>
      <li>Tấm mu tay nằm trên quai, ngay mu bàn tay. Mặt trong nên dán xốp mỏng cho êm.</li>
      <li>Quai chạy dọc mu bàn tay nên các ngón vẫn co duỗi tự do; ngón cái lên joystick, ngón trỏ trên cò.</li>
    </ul></div>
    <div class="card"><h3>Bố trí và nét riêng</h3><ul class="notes">
      <li>Không còn vòng: theo dõi tay dùng MediaPipe. Logo và chữ VERA LAB chuyển sang tấm mu tay.</li>
      <li>Đế cán phình nhẹ về phía mu bàn tay để chứa khe quai và đỡ mép bàn tay.</li>
      <li>Pin vuông 9V dựng đứng trong cán, ESP32 nằm ngang trong đầu. Thay pin từ đáy không cần tua vít.</li>
      <li>Giữ chén lục giác và 4 cổng mở rộng GPIO của bản trước.</li>
    </ul></div>
    <div class="card warn"><h3>Chưa chắc, cần thử</h3><ul class="notes">
      <li>Kẹp pin giả định cao 8,5 mm. Pin và kẹp phải đo thật trước khi in vì cán ôm rất sát.</li>
      <li>Đầu to hơn cán để đặt được ESP32 dài 55 mm, nên phần đuôi đầu nhô ra sau khoảng 12 mm trên khe ngón cái. Band 2 chưa sửa chỗ này. Cần in thử xem có cấn không.</li>
      <li>Vân lục giác nổi 0,45 mm in trên vách đứng: cần in thử xem bề mặt có đẹp không. Nếu xấu thì bỏ vân, chỉ mất phần chống trượt.</li>
      <li>Mạch thật đang dùng 2 lá đồng cảm ứng ở GPIO13, 14 (firmware G1), còn mô hình vẫn có cò Hall và 2 nút 6 × 6 ở P1, P2. Cần chốt: giữ cò hay đổi thành vùng chạm.</li>
      <li>Chia áp đo pin phải đổi sang 330k/82k. Chỉnh MP1584 ra đúng 3,3 V trước khi nối mạch.</li>
      <li>Nếu cắm cáp mà chưa tắt nguồn, 2 nguồn 3,3 V sẽ chạy song song với nhau.</li>
      <li>Tấm mu tay bằng PLA cứng, cần dán xốp mặt trong. Nếu xưởng có nhựa dẻo TPU thì in tấm này bằng TPU sẽ êm hơn.</li>
      <li>Độ chặt và độ êm của quai chưa thử trên tay thật.</li>
      <li>Ray nắp pin, móc mặt tối và độ nặng tay của cò chỉ mới đúng trên mô hình. Kích thước ESP32, KY-023 vẫn là số giả định.</li>
      <li>MediaPipe khi tay đang đeo tay cầm chưa thử với webcam thật.</li>
    </ul></div>
  </div>
  <div class="card"><h3>4 cổng mở rộng</h3>{bang_cong}<ul class="notes" style="margin-top:12px">
    <li>Mỗi cổng là một lỗ Ø7 mm, phía sau có ô giữ nút bấm 6 × 6 và nắp nút in 3D. Muốn gắn cảm biến thì bỏ nắp, luồn dây qua lỗ.</li>
    <li>Mọi chân chỉ chịu tối đa 3,3 V. Cảm biến ra 5 V phải qua chia áp.</li>
    <li>Chỉ P4 đọc được analog khi bật Wi-Fi, vì GPIO13, 14 và 4 thuộc ADC2.</li>
  </ul></div>
  <div class="card"><h3>Gói STL gửi in</h3>{bang_in}</div>
</div>

<script type="importmap">{{"imports":{{"three":"https://cdn.jsdelivr.net/npm/three@0.160.0/build/three.module.js","three/addons/":"https://cdn.jsdelivr.net/npm/three@0.160.0/examples/jsm/"}}}}</script>
<script type="module">
import * as THREE from 'three';
import {{ OrbitControls }} from 'three/addons/controls/OrbitControls.js';
import {{ STLLoader }} from 'three/addons/loaders/STLLoader.js';
import {{ RoomEnvironment }} from 'three/addons/environments/RoomEnvironment.js';

const CANH = {canh_js};
const CHOT = [25, 0, 57], GOC_BOP = 13;
const stage = document.getElementById('stage');
const renderer = new THREE.WebGLRenderer({{ antialias: true, alpha: true, preserveDrawingBuffer: true }});
renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
renderer.toneMapping = THREE.ACESFilmicToneMapping;
renderer.outputColorSpace = THREE.SRGBColorSpace;
stage.prepend(renderer.domElement);
const scene = new THREE.Scene();
scene.environment = new THREE.PMREMGenerator(renderer).fromScene(new RoomEnvironment(renderer), 0.04).texture;
// mô hình: z lên, x trước; three.js: y lên
const goc = new THREE.Group(); goc.rotation.x = -Math.PI / 2; scene.add(goc);
const TAM = new THREE.Vector3(-2, 40, 8);
const camera = new THREE.PerspectiveCamera(26, 1, 1, 4000);
const VIEW = {{ truoc: [300, 150, 260], sau: [-300, 200, 230], thang: [80, 70, 420], day: [200, -250, 250] }};
camera.position.set(...VIEW.sau);
const controls = new OrbitControls(camera, renderer.domElement);
controls.target.copy(TAM); controls.enableDamping = true; controls.autoRotateSpeed = 1.3;
controls.autoRotate = !matchMedia('(prefers-reduced-motion: reduce)').matches;
const cbQuay = document.getElementById('quay'); cbQuay.checked = controls.autoRotate;
const d1 = new THREE.DirectionalLight(0xffffff, 1.5); d1.position.set(200, 320, 220); scene.add(d1);
const d2 = new THREE.DirectionalLight(0x6fdcf0, 1.3); d2.position.set(-260, 60, -220); scene.add(d2);
// quầng sáng mờ dưới đế
const cv = document.createElement('canvas'); cv.width = cv.height = 256;
const cx2 = cv.getContext('2d'); const gr = cx2.createRadialGradient(128, 128, 18, 128, 128, 128);
gr.addColorStop(0, 'rgba(0,180,224,.34)'); gr.addColorStop(1, 'rgba(0,180,224,0)'); cx2.fillStyle = gr; cx2.fillRect(0, 0, 256, 256);
const san = new THREE.Mesh(new THREE.PlaneGeometry(240, 240), new THREE.MeshBasicMaterial({{ map: new THREE.CanvasTexture(cv), transparent: true, depthWrite: false }}));
san.rotation.x = -Math.PI / 2; san.position.set(2, -1, 0); scene.add(san);

const loader = new STLLoader();
const VL = {{
  than_phai: {{ color: '#eef2f4', roughness: 0.38, clearcoat: 0.7, clearcoatRoughness: 0.35 }},
  than_trai: {{ color: '#eef2f4', roughness: 0.38, clearcoat: 0.7, clearcoatRoughness: 0.35 }},
  quai: {{ color: '#2b3136', roughness: 0.95 }},
  chu: {{ color: '#04141a', roughness: 0.6 }},
  vit: {{ color: '#aab4ba', roughness: 0.3, metalness: 0.9 }},
  nap: {{ color: '#101418', roughness: 0.22, metalness: 0.2, clearcoat: 1, clearcoatRoughness: 0.12 }},
  nap_pin: {{ color: '#101418', roughness: 0.3, metalness: 0.2, clearcoat: 0.6 }},
  co: {{ color: '#c7ced2', roughness: 0.22, metalness: 0.85 }},
  nut_phu: {{ color: '#c7ced2', roughness: 0.22, metalness: 0.85 }},
  ky023: {{ color: '#15191d', roughness: 0.45, metalness: 0.2 }},
  tam_mu_tay: {{ color: '#0aa9c4', roughness: 0.35, emissive: '#00b4e0', emissiveIntensity: 0.35, clearcoat: 0.6 }},
  chen: {{ color: '#02242e', roughness: 0.3, emissive: '#00b4e0', emissiveIntensity: 1.25 }},
  pin9v: {{ color: '#c9862b', roughness: 0.4, metalness: 0.35 }},
}};
let goi = null;
async function tai(ten) {{
  if (!goi) {{
    const r = await fetch('mo_hinh.json');
    if (!r.ok) throw new Error('mo_hinh.json');
    goi = await r.json();
  }}
  const nhi = Uint8Array.from(atob(goi[ten]), c => c.charCodeAt(0));
  const g = loader.parse(nhi.buffer);
  g.computeVertexNormals();
  return g;
}}
const vat = [];
let tay = null, truc = new THREE.Group();
truc.position.set(...CHOT); goc.add(truc);
async function dung() {{
  for (const c of CANH.chi_tiet) {{
    const g = await tai(c.ma);
    const mat = new THREE.MeshPhysicalMaterial({{ ...(VL[c.ma] || {{ color: c.mau, roughness: 0.5, metalness: 0.2 }}), transparent: true, opacity: 1 }});
    const mesh = new THREE.Mesh(g, mat);
    const quayTheoCo = c.ma === 'co' || c.ma === 'nam_cham';
    if (quayTheoCo) {{ mesh.position.set(-CHOT[0], -CHOT[1], -CHOT[2]); truc.add(mesh); }} else goc.add(mesh);
    vat.push({{ ma: c.ma, mesh, tach: new THREE.Vector3(...c.tach), lk: !!c.linh_kien, co: quayTheoCo }});
  }}
  tay = new THREE.Mesh(await tai('ban_tay'), new THREE.MeshStandardMaterial({{ color: '#d9a07a', roughness: 0.8, transparent: true, opacity: 0.5, depthWrite: false }}));
  goc.add(tay);
  document.getElementById('tai').remove();
  capNhat();
}}
function capNhat() {{
  const t = +document.getElementById('tach').value;
  const ts = document.getElementById('trongSuot').checked;
  truc.rotation.y = THREE.MathUtils.degToRad(GOC_BOP) * +document.getElementById('bop').value;
  truc.position.set(CHOT[0] + 52 * t, CHOT[1], CHOT[2] - 8 * t);
  camera.zoom = 1 - 0.42 * t; camera.updateProjectionMatrix();
  for (const v of vat) {{
    if (!v.co) v.mesh.position.copy(v.tach).multiplyScalar(t);
    v.mesh.visible = v.lk && v.ma !== 'ky023' && v.ma !== 'quai' ? (ts || t > 0.02) : true;
    const vo = ['than_phai', 'than_trai', 'nap', 'co', 'nap_pin', 'nut_phu'].includes(v.ma);
    const mo = vo && ts ? 0.22 : 1;
    v.mesh.material.opacity = mo; v.mesh.material.depthWrite = mo === 1;
  }}
  if (tay) tay.visible = document.getElementById('hienTay').checked && t < 0.05;
}}
['tach', 'bop', 'trongSuot', 'hienTay'].forEach(id => document.getElementById(id).addEventListener('input', capNhat));
cbQuay.addEventListener('input', e => controls.autoRotate = e.target.checked);
function dungQuay() {{ controls.autoRotate = false; cbQuay.checked = false; }}
controls.addEventListener('start', dungQuay);
for (const [id, k] of [['vTruoc', 'truoc'], ['vSau', 'sau'], ['vThang', 'thang'], ['vDay', 'day']])
  document.getElementById(id).addEventListener('click', () => {{ dungQuay(); camera.position.set(...VIEW[k]); controls.target.copy(TAM); }});
function coKhung() {{ const w = stage.clientWidth, h = stage.clientHeight; renderer.setSize(w, h, false); camera.aspect = w / h; camera.updateProjectionMatrix(); }}
new ResizeObserver(coKhung).observe(stage); coKhung();
function ve() {{ controls.update(); renderer.render(scene, camera); requestAnimationFrame(ve); }}
dung().catch(e => {{ document.getElementById('tai').textContent = 'Không tải được mô hình 3D: ' + e.message; }});
ve();
</script>
"""
    ra = os.path.join(thu_muc, "vera_band_2.html")
    with open(ra, "w", encoding="utf-8") as fh:
        fh.write(html)
    print(ra, f"nhựa {canh['nhua_g']} g, tổng {canh['tong_g']} g, {kt} mm")


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    main()
