// Test các module KHÔNG phụ thuộc 3D của chế độ "Giải thích từng bước": dữ liệu quy trình, kiểm tra / chuẩn hoá, StepPlayer.
// Chạy: node --test tests/js/   (pytest gọi qua tests/test_giai_thich_tung_buoc.py)
import test from "node:test";
import assert from "node:assert/strict";
import { QUY_TRINH } from "../../frontend/sinh-hoc/giai-thich/data/index.js";
import { chuanHoaQuyTrinh, doiTen } from "../../frontend/sinh-hoc/giai-thich/kiem-tra.js";
import { StepPlayer } from "../../frontend/sinh-hoc/giai-thich/step-player.js";

// id bộ phận có trên mô hình 3D (cả bản dựng bằng code lẫn BodyParts3D)
const CO = new Set(["tam_nhi_phai", "tam_nhi_trai", "tam_that_phai", "tam_that_trai", "dong_mach_chu", "dong_mach_phoi", "tinh_mach_phoi",
  "tinh_mach_chu", "phoi_phai", "phoi_trai", "dai_nao", "da_day", "than_trai", "than_phai", "mach_chau", "co_tu_dau_dui", "gan"]);
const coBoPhan = (id) => CO.has(id);
const chuan = (tho) => chuanHoaQuyTrinh(tho, { coBoPhan, danhMuc: QUY_TRINH });

test("dữ liệu 2 vòng tuần hoàn hợp lệ, đủ phần giải thích bản chất", () => {
  for (const id of ["tuan_hoan_nho", "tuan_hoan_lon"]) {
    const kq = chuan(id);
    assert.ok(kq, id);
    assert.deepEqual(kq.canhBao, [], `${id}: mọi bộ phận phải có trên mô hình`);
    assert.equal(kq.quyTrinh.steps.length, QUY_TRINH[id].steps.length);
    for (const b of kq.quyTrinh.steps) {
      assert.ok(b.title && b.what.length > 30 && b.why.length > 60 && b.ghi_nho, `${id} bước ${b.id}: thiếu what / why / ghi_nho`);
      assert.match(b.color, /^#[0-9a-fA-F]{6}$/);
    }
  }
  // giải phẫu: máu KHÔNG đi từ phổi này sang phổi kia; vòng nhỏ bắt đầu ở TT phải, kết thúc ở TN trái; vòng lớn TT trái → TN phải
  const nho = chuan("tuan_hoan_nho").quyTrinh.steps, lon = chuan("tuan_hoan_lon").quyTrinh.steps;
  for (const b of nho) assert.ok(!(b.from.includes("phoi_phai") && b.to.length === 1 && b.to[0] === "phoi_trai"), "mũi tên phổi phải → phổi trái là sai");
  assert.deepEqual([nho[0].from, nho.at(-1).to], [["tam_that_phai"], ["tam_nhi_trai"]]);
  assert.deepEqual([lon[0].from, lon.at(-1).to], [["tam_that_trai"], ["tam_nhi_phai"]]);
  assert.ok(nho.some((b) => b.tai_cho) && lon.some((b) => b.tai_cho), "mỗi vòng có bước trao đổi tại chỗ");
});

test("chuẩn hoá dữ liệu AI: tên tiếng Anh, dạng cũ label/text, màu lạ, bộ phận không có", () => {
  assert.deepEqual(doiTen("Right Ventricle"), ["tam_that_phai"]);
  assert.deepEqual(doiTen("lungs"), ["phoi_phai", "phoi_trai"]);
  const kq = chuan({
    process: "Thử",
    steps: [
      { from: "right_ventricle", to: "pulmonary_artery", label: "TT phải → ĐM phổi", text: "Máu nghèo O2 lên phổi.", color: "red; background:url(x)" },
      { from: "khong_ton_tai", to: "aorta", label: "Sai" },
      { from: "lungs", to: "lungs", title: "Trao đổi khí", what: "O2 vào, CO2 ra", duration: 999 },
    ],
  });
  assert.equal(kq.quyTrinh.steps.length, 2);
  assert.equal(kq.canhBao.length, 1);
  const [a, b] = kq.quyTrinh.steps;
  assert.deepEqual([a.from, a.to, a.title, a.what, a.color], [["tam_that_phai"], ["dong_mach_phoi"], "TT phải → ĐM phổi", "Máu nghèo O2 lên phổi.", "#06b6d4"]);
  assert.ok(b.tai_cho && b.duration === 8 && b.id === 2);
  assert.equal(chuan({ steps: [{ from: "x", to: "y" }] }), null);
  assert.equal(chuan("khong_co_quy_trinh_nay"), null);
});

const chay = (p, giay, buoc = 1 / 60) => { for (let t = 0; t < giay; t += buoc) p.update(buoc); };

test("StepPlayer: bấm Tiếp 10 lần liên tục không chồng hoạt ảnh", () => {
  const p = new StepPlayer();
  const batDau = [];
  p.on("stepStart", (i) => batDau.push(i));
  p.load(chuan("tuan_hoan_lon").quyTrinh);
  p.goTo(0);
  for (let i = 0; i < 10; i++) p.next();
  assert.equal(p.index, 4);                // dừng ở bước cuối (5 bước)
  assert.equal(p.state, "finished");      // bấm tiếp ở bước cuối → kết thúc, không vượt
  assert.deepEqual(batDau, [0, 1, 2, 3, 4]);
  p.goTo(2);
  assert.equal(p.progress, 0);            // nhảy bước = đặt lại bộ đếm (huỷ ngay hoạt ảnh bước cũ)
});

test("StepPlayer: chế độ bấm tay – vẽ xong thì dừng chờ, Phát sang bước sau, hết bước thì finished", () => {
  const p = new StepPlayer();
  const ketThuc = [];
  p.on("finish", () => ketThuc.push(1));
  p.load(chuan("tuan_hoan_nho").quyTrinh);
  p.play();
  assert.deepEqual([p.index, p.state], [0, "playing"]);
  chay(p, 1.0);
  assert.ok(p.progress > 0.3 && p.progress < 0.6);
  p.pause();
  const truoc = p.progress;
  chay(p, 5);
  assert.equal(p.progress, truoc);        // tạm dừng thật (kể cả bước "tại chỗ")
  p.play();
  chay(p, 3);
  assert.deepEqual([p.index, p.state, p.progress], [0, "paused", 1]);   // vẽ xong → chờ người học
  p.play();
  assert.equal(p.index, 1);
  p.goTo(4);
  chay(p, 4);
  assert.deepEqual([p.state, ketThuc.length], ["finished", 1]);
  p.play();                                // ở trạng thái kết thúc: Phát = xem lại từ đầu
  assert.deepEqual([p.index, p.state], [0, "playing"]);
});

test("StepPlayer: Tự động chạy đi hết các bước, có thời gian đọc, tốc độ 2× nhanh gấp đôi", () => {
  const thoiGianChay = (tocDo) => {
    const p = new StepPlayer();
    let xong = 0, giay = 0;
    p.on("finish", () => { xong++; });
    p.load(chuan("tuan_hoan_nho").quyTrinh);
    p.setAutoPlay(true);
    p.setSpeed(tocDo);
    p.play();
    while (p.state !== "finished" && giay < 600) { p.update(0.05); giay += 0.05; }
    assert.equal(xong, 1);
    assert.equal(p.index, 4);
    return giay;
  };
  const t1 = thoiGianChay(1), t2 = thoiGianChay(2);
  assert.ok(t1 > 5 * 2.4, "phải có thời gian vẽ + đọc");
  assert.ok(Math.abs(t1 / t2 - 2) < 0.1, `2× phải nhanh gấp đôi (${t1} / ${t2})`);
});
