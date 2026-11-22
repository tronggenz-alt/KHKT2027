// index.js – ghép các module thành chế độ "Giải thích từng bước có mũi tên" cho Phòng Lab Sinh học 3D.
// Trang 3D chỉ cần: taoGiaiThichTungBuoc(phụ_thuộc) rồi gọi .capNhat(dt) trong vòng render chung.
//   StepPlayer (trạng thái bước) → sự kiện → ArrowRenderer (mũi tên) · CameraDirector (camera) · HighlightManager (sáng / mờ) · ExplainPanel (khung chữ)
import { CAU_HINH } from "./config.js";
import { QUY_TRINH, QUY_TRINH_MAC_DINH } from "./data/index.js";
import { chuanHoaQuyTrinh } from "./kiem-tra.js";
import { StepPlayer } from "./step-player.js";
import { ArrowRenderer } from "./arrow-renderer.js";
import { CameraDirector } from "./camera-director.js";
import { HighlightManager } from "./highlight-manager.js";
import { ExplainPanel } from "./explain-panel.js";

export { QUY_TRINH, CAU_HINH };

/**
 * @param d phụ thuộc từ trang 3D:
 *   scene, camera, controls, khung3D,
 *   coBoPhan(id) → bool, viTri(id) → Vector3, hopBoPhan(ids) → Box3,
 *   datMucTieu(ids, coLap), layTrangThai() → trangThaiBP, hatMau: { lay(), dat(bool) },
 *   luuDoc(k, macDinh), luuGhi(k, v), thongBao(chu, loai), giamChuyenDong() → bool
 */
export function taoGiaiThichTungBuoc(d) {
  const player = new StepPlayer({ giamChuyenDong: d.giamChuyenDong });
  const muiTen = new ArrowRenderer({ scene: d.scene, camera: d.camera, viTri: (id) => d.viTri(id) });
  const cam = new CameraDirector({ camera: d.camera, controls: d.controls, khung3D: d.khung3D });
  const sang = new HighlightManager({ datMucTieu: d.datMucTieu, layTrangThai: d.layTrangThai });
  const bang = new ExplainPanel({ luuDoc: d.luuDoc, luuGhi: d.luuGhi });
  let dangMo = false, hatMauTruoc = null, giay = 0;

  const dieuKhien = () => ({ state: player.state, autoPlay: player.autoPlay, speed: player.speed });
  const capNhatBuCamera = () => cam.datVungChe(dangMo ? bang.vungChe(d.khung3D) : {});
  const dongKhungBuoc = (T) => {
    const s = player.step;
    if (s) cam.dongKhung(d.hopBoPhan([...new Set([...s.from, ...s.to])]), T);
  };

  player.on("stepStart", (i, s) => {
    bang.hienBuoc(i, s);
    sang.lamNoi([...new Set([...s.from, ...s.to])]);
    dongKhungBuoc(d.giamChuyenDong() ? 0 : CAU_HINH.thoiGian.camera / player.speed);
  });
  player.on("finish", (qt) => bang.hienTomTat(qt));
  for (const ten of ["state", "speed", "autoPlay"]) player.on(ten, () => bang.capNhatDieuKhien(dieuKhien()));

  function khoiDong(tho, batDau = 0) {
    const kq = chuanHoaQuyTrinh(tho ?? QUY_TRINH_MAC_DINH, { coBoPhan: d.coBoPhan, danhMuc: QUY_TRINH });
    if (!kq) {
      d.thongBao?.("Không dựng được quy trình: các bộ phận trong dữ liệu không có trên mô hình 3D.", "loi");
      return false;
    }
    for (const c of kq.canhBao) console.warn("[Giải thích từng bước]", c);
    if (!dangMo) {
      dangMo = true;
      sang.batDau();
      hatMauTruoc = d.hatMau.lay();
      d.hatMau.dat(false);      // học theo mũi tên: tắt dòng hạt chạy liên tục cho đỡ rối mắt (bật lại khi đóng)
    }
    muiTen.clear();
    bang.mo();
    bang.datDanhMuc(QUY_TRINH, kq.quyTrinh);
    bang.datQuyTrinh(kq.quyTrinh);
    player.load(kq.quyTrinh);
    capNhatBuCamera();
    player.goTo(batDau);
    bang.capNhatDieuKhien(dieuKhien());
    return true;
  }

  function dong() {
    if (!dangMo) return;
    dangMo = false;
    player.unload();
    muiTen.clear();
    cam.dung();
    bang.dong();
    capNhatBuCamera();
    sang.ketThuc();
    if (hatMauTruoc !== null) d.hatMau.dat(hatMauTruoc);
    hatMauTruoc = null;
  }

  bang.onAction((ten, gt) => {
    switch (ten) {
      case "dong": dong(); break;
      case "thuNho": bang.thuNho(gt === undefined ? !bang.laThuNho : gt); capNhatBuCamera(); break;
      case "moRong": bang.thuNho(false); capNhatBuCamera(); break;
      case "diChuyen": capNhatBuCamera(); break;
      case "canGoc": dongKhungBuoc(CAU_HINH.thoiGian.camera); break;
      case "truoc": player.prev(); break;
      case "sau": player.next(); break;
      case "phat": player.toggle(); break;
      case "lap": player.goTo(0); break;
      case "denBuoc": player.goTo(gt); break;
      case "tocDo": player.setSpeed(gt); break;
      case "tuChay": player.setAutoPlay(gt); d.luuGhi("bio-lab-gt-tu-chay", !!gt); break;
      case "chonQuyTrinh": if (QUY_TRINH[gt]) khoiDong(gt); break;
      default: break;
    }
  });
  player.setAutoPlay(!!d.luuDoc("bio-lab-gt-tu-chay", false));

  return {
    get dangMo() { return dangMo; },
    get dangChayCamera() { return cam.dangBay; },
    khoiDong,
    dong,
    /** Gọi mỗi khung hình từ vòng render chung của trang (dt = giây). */
    capNhat(dt) {
      if (!dangMo) return;
      giay += dt;
      player.update(dt);
      if (player.process) muiTen.dongBo(player.steps, player.index, player.progress, giay);
      cam.update(dt);
      bang.capNhatDemNguoc(player.autoPlay && player.pha === "doc" ? player.tienDoDoc : null);
    },
    buocTruoc: () => player.prev(),
    buocSau: () => player.next(),
    chuyenPhatDung: () => player.toggle(),
    xemLaiTuDau: () => player.goTo(0),
    datTocDo: (x) => player.setSpeed(x),
    datTuChay: (b) => player.setAutoPlay(b),
    capNhatBuCamera,
    dungCamera: () => cam.dung(),
    dongKhungBuoc,
    on: (evt, cb) => player.on(evt, cb),
    off: (evt, cb) => player.off(evt, cb),
    get player() { return player; },
    get bang() { return bang; },
    get sang() { return sang; },
    get trangThai() {
      return { mo: dangMo, quy_trinh: player.process?.id || null, buoc: player.index, tong: player.steps.length, state: player.state,
        tu_chay: player.autoPlay, toc_do: player.speed, doi_tuong_3d: muiTen.soDoiTuong };
    },
  };
}

