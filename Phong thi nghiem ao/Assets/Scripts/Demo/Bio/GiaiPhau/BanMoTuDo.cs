using System;
using System.Collections.Generic;
using System.Text;
using Khkt.Demo.Bio.ThucVat;
using Khkt.Environment;
using Khkt.Simulation.Bio.GiaiPhau;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Khkt.Demo.Bio.GiaiPhau
{
    /// <summary>Mở / đóng bàn mổ tự do (ếch, người) ngay trong app.</summary>
    public static class BanMoTuDoMoDun
    {
        static BanMoTuDo hienTai;
        static Action khiDong;

        public static bool DangMo => hienTai != null;
        public static BanMoTuDo HienTai => hienTai;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void DatLai() { hienTai = null; khiDong = null; }

        /// <summary>Mở bàn mổ ếch (24/11: chỉ làm bài mổ ếch, không mổ người).</summary>
        public static void Mo(LoaiMau loai = LoaiMau.Ech, Action onClosed = null)
        {
            loai = LoaiMau.Ech;
            if (GiaiPhauMoDunImpl.DangMo)
            {
                // Chuyển từ khung xem giải phẫu sang bàn mổ: giữ callback đóng của khung xem cho bàn mổ
                GiaiPhauMoDunImpl.Dong();
            }
            if (hienTai != null)
            {
                if (onClosed != null) khiDong += onClosed;
                hienTai.DoiMau(loai);
                return;
            }
            khiDong = onClosed;
            var go = new GameObject("BanMoTuDo_Host");
            UnityEngine.Object.DontDestroyOnLoad(go);
            hienTai = go.AddComponent<BanMoTuDo>();
            hienTai.KhoiTao(loai);
        }

        public static void Dong()
        {
            if (hienTai == null) return;
            var h = hienTai; hienTai = null;
            var cb = khiDong; khiDong = null;
            h.DonDep();
            UnityEngine.Object.Destroy(h.gameObject);
            cb?.Invoke();
        }
    }

    public sealed class BanMoTuDo : MonoBehaviour
    {
        struct MucDungCu { public DungCuMo dc; public string ten, moTa; public bool chiEch, chiNguoi; }

        static readonly MucDungCu[] DungCu =
        {
            new MucDungCu { dc = DungCuMo.KimHuyTuy, ten = "Kim hủy tủy", moTa = "Bấm vào đầu ếch: lần 1 hủy não qua lỗ chẩm, lần 2 hủy tủy sống.", chiEch = true },
            new MucDungCu { dc = DungCuMo.KimGhim, ten = "Kim ghim", moTa = "Ghim 4 chi ếch lên khay sáp; ghim vạt da, cơ đã lật để vạt không rơi về." },
            new MucDungCu { dc = DungCuMo.DaoMo, ten = "Dao mổ", moTa = "Giữ chuột trái và kéo để rạch. Mỗi đường chỉ cắt lớp trên cùng nơi lưỡi dao chạm. Không cắt được xương." },
            new MucDungCu { dc = DungCuMo.Keo, ten = "Kéo mổ", moTa = "Luồn mũi kéo từ mép vết rạch rồi kéo để cắt. Cắt được màng, cơ, xương mỏng của ếch, mạch và cuống cơ quan." },
            new MucDungCu { dc = DungCuMo.CuaXuong, ten = "Cưa xương ức", moTa = "Kéo dọc xương ức để cưa (mở ngực người).", chiNguoi = true },
            new MucDungCu { dc = DungCuMo.KepPanh, ten = "Kẹp panh", moTa = "Lật vạt đã rạch rời các phía, gắp mảnh đã rời, vén cơ quan sang bên. Bấm vạt đang lật để đặt lại." },
            new MucDungCu { dc = DungCuMo.Banh, ten = "Banh", moTa = "Giữ mở mép vết mổ hoặc tách hai nửa xương ức đã cưa." },
            new MucDungCu { dc = DungCuMo.KepCamMau, ten = "Kẹp cầm máu", moTa = "Kẹp mạch đang chảy, hoặc kẹp cuống cơ quan trước khi cắt." },
            new MucDungCu { dc = DungCuMo.BongGac, ten = "Bông gạc", moTa = "Thấm máu, dịch đọng; ép lên chỗ rỉ máu nhỏ cho mau đông." },
            new MucDungCu { dc = DungCuMo.OngTiem, ten = "Ống tiêm", moTa = "Hút dịch trong cơ quan hoặc máu trong mạch, buồng tim để xem tính chất." },
            new MucDungCu { dc = DungCuMo.OngNhoGiot, ten = "Nhỏ thuốc", moTa = "Chọn thuốc bên dưới rồi bấm lên mẫu (người: tiêm tĩnh mạch qua đường truyền)." },
            new MucDungCu { dc = DungCuMo.DienCuc, ten = "Điện cực", moTa = "Kích thích dây thần kinh, cơ, tim. Kích thích tim lúc tâm trương gây ngoại tâm thu." },
            new MucDungCu { dc = DungCuMo.OngThoi, ten = "Ống thổi", moTa = "Đặt vào miệng ếch (bấm vào đầu) để thổi phồng phổi qua khe thanh quản.", chiEch = true },
        };

        static readonly (ThuocMo t, string ten)[] DanhSachThuoc =
        {
            (ThuocMo.Ringer, "Ringer"), (ThuocMo.RingerLanh, "Ringer lạnh"), (ThuocMo.Adrenalin, "Adrenalin"),
            (ThuocMo.Acetylcholin, "Acetylcholin"), (ThuocMo.Atropin, "Atropin"), (ThuocMo.HCl, "HCl 0,3 %")
        };

        public MauMo Mau { get; private set; }
        public BanMoSanKhau SanKhau { get; private set; }
        public Camera Cam { get; private set; }
        public CameraQuyDao QuyDao { get; private set; }
        public Canvas UiCanvas { get; private set; }
        public DungCuMo DungCuDangCam { get; private set; } = DungCuMo.KimHuyTuy;
        public ThuocMo ThuocDangChon { get; private set; } = ThuocMo.Ringer;
        public string ThongBaoCuoi { get; private set; } = "";
        public float TocDo { get; set; } = 1f;

        Camera camCu; bool camCuBat;
        CursorLockMode khoaCu; bool hienCu;
        GameObject eventSystemTam;

        // UI
        readonly Dictionary<DungCuMo, Button> nutDungCu = new Dictionary<DungCuMo, Button>();
        readonly Dictionary<ThuocMo, Button> nutThuoc = new Dictionary<ThuocMo, Button>();
        Text txtMoTaDungCu, txtSinhHieu, txtDoiTuong, txtNhatKy, txtThongBao, txtTieuDeMau;
        RectTransform hangThuoc;
        Image nenThongBao;
        RawImage kyDo;
        Texture2D kyDoTex;
        Color32[] kyDoPx;
        int kyDoX;
        float kyDoTich, demUi;
        const int KyW = 380, KyH = 96;

        // Thao tác chuột
        bool dangRach;
        float doSauRach;
        LineRenderer vetDao;
        readonly List<Vector2> duongRach = new List<Vector2>();
        Vector2 chuotXuong;

        public void KhoiTao(LoaiMau loai)
        {
            DesktopViewer.UiCapture++;
            camCu = Camera.main;
            if (camCu != null) { camCuBat = camCu.enabled; camCu.enabled = false; }
            khoaCu = Cursor.lockState; hienCu = Cursor.visible;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                eventSystemTam = new GameObject("BanMo_EventSystem");
                eventSystemTam.AddComponent<EventSystem>();
                eventSystemTam.AddComponent<InputSystemUIInputModule>();
            }

            var camGo = new GameObject("CameraBanMo") { layer = BanMoSanKhau.Layer };
            camGo.transform.SetParent(transform, false);
            Cam = camGo.AddComponent<Camera>();
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.cullingMask = 1 << BanMoSanKhau.Layer;
            Cam.nearClipPlane = 0.01f; Cam.farClipPlane = 20f; Cam.depth = 66f; Cam.fieldOfView = 38f;
            QuyDao = camGo.AddComponent<CameraQuyDao>();
            QuyDao.TargetCamera = Cam;
            QuyDao.ChiXoayChuotPhai = true;
            QuyDao.MinPitch = 10f; QuyDao.MaxPitch = 89f;
            QuyDao.OnEscapePressed += BanMoTuDoMoDun.Dong;

            var sk = new GameObject("SanKhauBanMo") { layer = BanMoSanKhau.Layer };
            sk.transform.SetParent(transform, false);
            SanKhau = sk.AddComponent<BanMoSanKhau>();

            UiTheme.Changed += KhiDoiChuDe;
            DoiMau(loai);
        }

        public void DonDep()
        {
            UiTheme.Changed -= KhiDoiChuDe;
            DesktopViewer.UiCapture = Mathf.Max(0, DesktopViewer.UiCapture - 1);
            if (camCu != null) camCu.enabled = camCuBat;
            Cursor.lockState = khoaCu; Cursor.visible = hienCu;
            if (eventSystemTam != null) Destroy(eventSystemTam);
            if (UiCanvas != null) Destroy(UiCanvas.gameObject);
            if (kyDoTex != null) Destroy(kyDoTex);
        }

        public void DoiMau(LoaiMau loai)
        {
            Mau = new MauMo(loai);
            SanKhau.Dung(Mau);
            DungCuDangCam = loai == LoaiMau.Ech ? DungCuMo.KimHuyTuy : DungCuMo.DaoMo;
            if (loai == LoaiMau.Nguoi && ThuocDangChon == ThuocMo.HCl) ThuocDangChon = ThuocMo.Ringer;
            ThongBaoCuoi = loai == LoaiMau.Ech
                ? "Ếch còn sống. Hủy não, tủy bằng kim mũi mác, ghim 4 chi lên khay sáp, rồi tự chọn đường rạch."
                : "Bệnh nhân đã gây mê, thở máy. Tự chọn đường rạch; theo dõi sinh hiệu bên phải.";
            DatGocNhin(true);
            DungUi();
        }

        public void DatGocNhin(bool tucThi)
        {
            if (QuyDao == null || SanKhau == null) return;
            // Người học đứng phía chân mẫu nhìn về phía đầu (đầu ở trên màn hình), cửa sổ mổ nằm giữa hai bảng
            float dist = Mau.loai == LoaiMau.Ech ? 0.95f : 0.92f;
            QuyDao.MinDistance = dist * 0.2f; QuyDao.MaxDistance = dist * 2.4f;
            float cao = SanKhau.MatDa(0.5f, 0.5f) * 0.6f;
            QuyDao.SetTarget(BanMoSanKhau.Goc + new Vector3(0f, cao, Mau.loai == LoaiMau.Ech ? -0.01f : 0.02f), 0f, 66f, dist, tucThi);
        }

        void KhiDoiChuDe() => DungUi();

        // ======================================================================= vòng lặp

        void Update()
        {
            if (Mau == null) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f) * TocDo;
            // chia bước nhỏ để dẫn truyền tim chính xác
            int n = Mathf.Max(1, Mathf.CeilToInt(dt / 0.02f));
            for (int k = 0; k < n; k++) Mau.Buoc(dt / n);
            SanKhau.CapNhat(dt);
            XuLyChuot();
            VeKyDo(dt);
            demUi -= Time.unscaledDeltaTime;
            if (demUi <= 0f) { demUi = 0.12f; CapNhatChu(); }
        }

        /// <summary>Chạy mô hình nhanh (dùng cho kiểm thử và chụp ảnh): tiến thời gian mà không cần chờ khung hình.</summary>
        public void TuaNhanh(float giay)
        {
            int n = Mathf.CeilToInt(giay / 0.02f);
            for (int k = 0; k < n; k++) { Mau.Buoc(0.02f); if (k % 3 == 0) SanKhau.CapNhat(0.06f); }
            SanKhau.CapNhat(0.02f);
            CapNhatChu();
        }

        // ======================================================================= thao tác

        public void ChonDungCu(DungCuMo dc)
        {
            DungCuDangCam = dc;
            foreach (var kv in nutDungCu) GlassKit.SetChip(kv.Value, kv.Key == dc);
            if (hangThuoc != null) hangThuoc.gameObject.SetActive(dc == DungCuMo.OngNhoGiot);
            foreach (var m in DungCu) if (m.dc == dc && txtMoTaDungCu != null) txtMoTaDungCu.text = m.moTa;
        }

        public void ChonThuoc(ThuocMo t)
        {
            ThuocDangChon = t;
            foreach (var kv in nutThuoc) GlassKit.SetChip(kv.Value, kv.Key == t);
        }

        public void BaoKetQua(KetQua kq)
        {
            if (kq == null || string.IsNullOrEmpty(kq.thongBao)) return;
            ThongBaoCuoi = kq.thongBao;
            if (txtThongBao != null)
            {
                txtThongBao.text = kq.thongBao;
                txtThongBao.color = kq.ok ? GlassKit.Ink : GlassKit.Warn;
            }
            if (nenThongBao != null) nenThongBao.color = kq.ok ? GlassKit.SurfaceA(0.95f) : GlassKit.WarnSoft;
            try
            {
                Khkt.Interaction.Bio.BioFeedback.LogAction("ban_mo_tu_do", DungCuDangCam.ToString(), kq.thongBao);
            }
            catch { /* nhật ký chung không bắt buộc */ }
            CapNhatChu();
        }

        bool TrenUi() => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        void XuLyChuot()
        {
            var mouse = Mouse.current;
            if (mouse == null || Cam == null) return;
            Vector2 pos = mouse.position.ReadValue();
            var ray = Cam.ScreenPointToRay(pos);

            // Rê chuột: hiện thông tin vật dưới con trỏ
            if (!dangRach && !TrenUi() && Physics.Raycast(ray, out var hov, 20f, 1 << BanMoSanKhau.Layer))
                HienThongTin(hov);

            if (mouse.leftButton.wasPressedThisFrame && !TrenUi())
            {
                chuotXuong = pos;
                if (LaDungCuCat(DungCuDangCam) && Physics.Raycast(ray, out var hit, 20f, 1 << BanMoSanKhau.Layer))
                {
                    var nh = hit.collider.GetComponent<BanMoNhan>();
                    if (nh != null && nh.loai == BanMoNhan.Loai.Lop)
                    {
                        var uv = SanKhau.UV(hit.point);
                        var l = Mau.LopTrenCung(uv.x, uv.y);
                        if (l != null)
                        {
                            dangRach = true; doSauRach = l.doSau;
                            duongRach.Clear(); duongRach.Add(uv);
                            return;
                        }
                    }
                }
                BamTai(ray);
            }
            if (dangRach)
            {
                if (mouse.leftButton.isPressed)
                {
                    if (SanKhau.GiaoMatLop(ray, doSauRach, out var uv) && (uv - duongRach[duongRach.Count - 1]).sqrMagnitude > 0.00006f)
                        duongRach.Add(uv);
                    VeVetDao();
                }
                if (mouse.leftButton.wasReleasedThisFrame)
                {
                    dangRach = false;
                    if ((pos - chuotXuong).magnitude < 6f || duongRach.Count < 2) BamTai(ray);
                    else BaoKetQua(Mau.Rach(DungCuDangCam, duongRach));
                    duongRach.Clear();
                    if (vetDao != null) vetDao.positionCount = 0;
                }
            }
        }

        /// <summary>Vệt mảnh đỏ sẫm bám theo mặt mô khi đang kéo dao, để thấy đường sẽ rạch.</summary>
        void VeVetDao()
        {
            if (vetDao == null)
            {
                var go = new GameObject("VetDao") { layer = BanMoSanKhau.Layer };
                go.transform.SetParent(transform, false);
                vetDao = go.AddComponent<LineRenderer>();
                vetDao.sharedMaterial = BanMoVatLieu.Tao(new Color(0.45f, 0.02f, 0.05f), BanMoVatLieu.Van.Tron, 0.8f);
                vetDao.widthMultiplier = 0.0016f;
                vetDao.numCapVertices = 3;
                vetDao.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            vetDao.positionCount = duongRach.Count;
            for (int k = 0; k < duongRach.Count; k++)
                vetDao.SetPosition(k, SanKhau.DiemTheGioi(new Vector3(duongRach[k].x, duongRach[k].y, doSauRach)) + Vector3.up * 0.0008f);
        }

        static bool LaDungCuCat(DungCuMo d) => d == DungCuMo.DaoMo || d == DungCuMo.Keo || d == DungCuMo.CuaXuong;

        /// <summary>Rạch theo đường (u, v) bằng dụng cụ đang cầm (gọi từ chuột hoặc kiểm thử).</summary>
        public KetQua RachTheoDuong(IList<Vector2> duong)
        {
            var kq = Mau.Rach(DungCuDangCam, duong);
            BaoKetQua(kq);
            return kq;
        }

        /// <summary>Bấm dụng cụ đang cầm vào chỗ tia chạm trên mẫu.</summary>
        public KetQua BamTai(Ray ray)
        {
            if (!Physics.Raycast(ray, out var hit, 20f, 1 << BanMoSanKhau.Layer)) return null;
            var kq = XuLyCham(hit);
            BaoKetQua(kq);
            return kq;
        }

        KetQua XuLyCham(RaycastHit hit)
        {
            var nh = hit.collider.GetComponent<BanMoNhan>();
            if (nh == null) return null;
            var dc = DungCuDangCam;
            switch (nh.loai)
            {
                case BanMoNhan.Loai.Dau:
                    if (dc == DungCuMo.KimHuyTuy) return Mau.HuyTuy();
                    if (dc == DungCuMo.OngThoi) return Mau.ThoiQuaThanhQuan();
                    return new KetQua(false, "Đầu ếch: dùng kim hủy tủy (qua lỗ chẩm) hoặc ống thổi (qua miệng).");
                case BanMoNhan.Loai.Chi:
                    if (dc == DungCuMo.KimGhim) return Mau.GhimChi(nh.chi);
                    if (dc == DungCuMo.OngNhoGiot && ThuocDangChon == ThuocMo.HCl) return Mau.NhoAcidChan(nh.chi);
                    if (dc == DungCuMo.OngNhoGiot) return new KetQua(false, "Muốn thử phản xạ tủy thì chọn HCl 0,3 % rồi nhỏ lên da chân.");
                    if (dc == DungCuMo.DienCuc) return Mau.KichThichChi(nh.chi);
                    return new KetQua(false, "Bài mổ này mở vùng bụng, ngực; ở chân chỉ ghim, nhỏ acid thử phản xạ hoặc kích thích cơ.");
                case BanMoNhan.Loai.Khay:
                    return new KetQua(false, Mau.loai == LoaiMau.Ech ? "Khay sáp: ghim chi ếch vào đây." : "Bàn mổ phủ khăn vô khuẩn.");
                case BanMoNhan.Loai.CoQuan:
                    {
                        if (dc == DungCuMo.KimGhim || dc == DungCuMo.KimHuyTuy || dc == DungCuMo.CuaXuong)
                            return new KetQua(false, "Dụng cụ này không dùng trên cơ quan.");
                        if (dc == DungCuMo.OngNhoGiot && ThuocDangChon == ThuocMo.HCl)
                            return new KetQua(false, "HCl 0,3 % chỉ dùng thử phản xạ trên da chân ếch.");
                        return Mau.DungTrenCoQuan(dc, Mau.CQ(nh.id), ThuocDangChon);
                    }
                case BanMoNhan.Loai.Mach:
                    if (dc == DungCuMo.OngNhoGiot) return Mau.NhoThuoc(ThuocDangChon);
                    return Mau.DungTrenMach(dc, Mau.Mach(nh.id));
                case BanMoNhan.Loai.ThanKinh:
                    {
                        var tk = Mau.TK(nh.id);
                        return Mau.DungTrenThanKinh(dc, tk, SanKhau.ThamSoThanKinh(tk, hit.point));
                    }
                case BanMoNhan.Loai.Lop:
                    {
                        if (!SanKhau.OTuTamGiac(nh.id, hit.triangleIndex, out var lop, out int o) || o < 0) return null;
                        var uv = SanKhau.UV(hit.point);
                        if (lop.o[o] == TrangThaiO.Lat)
                        {
                            if (dc == DungCuMo.KimGhim) return Mau.GhimVatTheoO(lop, o);
                            if (dc == DungCuMo.Banh) return Mau.BanhVatTheoO(lop, o);
                            if (dc == DungCuMo.KepPanh) return Mau.DatLaiVatTheoO(lop, o);
                            return new KetQua(false, $"Đây là vạt {lop.ten.ToLowerInvariant()} đang lật.");
                        }
                        switch (dc)
                        {
                            case DungCuMo.KepPanh: return Mau.LatVat(uv.x, uv.y, false);
                            case DungCuMo.Banh: return Mau.LatVat(uv.x, uv.y, true);
                            case DungCuMo.DaoMo:
                            case DungCuMo.Keo:
                            case DungCuMo.CuaXuong:
                                return new KetQua(false, "Giữ chuột trái và kéo thành đường để rạch.");
                            case DungCuMo.KimGhim:
                                return new KetQua(false, Mau.loai == LoaiMau.Ech ? "Ghim vào bàn chân ếch để cố định, hoặc vào vạt đã lật." : "Ghim vạt đã lật.");
                            case DungCuMo.BongGac: return Mau.ThamMau();
                            case DungCuMo.OngNhoGiot:
                                if (ThuocDangChon == ThuocMo.HCl) return new KetQua(false, "HCl 0,3 % dùng nhỏ lên da chân ếch để thử phản xạ.");
                                return Mau.NhoThuoc(ThuocDangChon);
                            case DungCuMo.DienCuc:
                                return lop.loai == LoaiLop.Co
                                    ? new KetQua(true, "Kích thích cơ thành bụng: bó cơ dưới điện cực co giật nhẹ (cơ vân).")
                                    : new KetQua(false, $"{lop.ten} không co khi kích thích.");
                            case DungCuMo.OngTiem: return new KetQua(false, $"{lop.ten} không chứa dịch để hút.");
                            case DungCuMo.KimHuyTuy: return new KetQua(false, "Kim hủy tủy đưa qua lỗ chẩm ở đầu ếch.");
                            case DungCuMo.OngThoi: return new KetQua(false, "Ống thổi đặt vào miệng ếch (bấm vào đầu).");
                            default: return new KetQua(false, "Dụng cụ này không dùng trên " + lop.ten.ToLowerInvariant() + ".");
                        }
                    }
            }
            return null;
        }

        void HienThongTin(RaycastHit h)
        {
            if (txtDoiTuong == null) return;
            var nh = h.collider.GetComponent<BanMoNhan>();
            if (nh == null) return;
            var sb = new StringBuilder();
            switch (nh.loai)
            {
                case BanMoNhan.Loai.CoQuan:
                    {
                        var cq = Mau.CQ(nh.id);
                        if (cq == null) return;
                        sb.Append($"<b>{cq.ten}</b>  <color={GlassKit.HexDim}>· {cq.he}</color>\n{cq.moTa}");
                        var d = DichCoThe.Lay(cq.dich);
                        if (d != null && d.id != "khi") sb.Append($"\n<color={GlassKit.HexDim}>Chứa: {d.ten.ToLowerInvariant()}</color>");
                        var ly = Mau.LyDoChuaLo(cq);
                        if (ly != null) sb.Append($"\n<color={GlassKit.HexWarn}>{ly}</color>");
                        break;
                    }
                case BanMoNhan.Loai.Mach:
                    {
                        var m = Mau.Mach(nh.id);
                        if (m == null) return;
                        sb.Append($"<b>{m.ten}</b>\n{(m.dongMach ? "Động mạch: máu đỏ tươi, áp lực cao, đứt thì phụt theo nhịp tim." : "Tĩnh mạch: máu đỏ sẫm, áp lực thấp, đứt thì trào đều.")}");
                        sb.Append($"\nTrạng thái: {(m.kep ? "đã kẹp" : m.dut ? (m.dongCuc ? "đã tự cầm" : "đang chảy máu") : "nguyên vẹn")}");
                        break;
                    }
                case BanMoNhan.Loai.ThanKinh:
                    {
                        var tk = Mau.TK(nh.id);
                        if (tk == null) return;
                        sb.Append($"<b>{tk.ten}</b>\nDây thần kinh màu trắng ngà. {(tk.LienToanBo ? "Còn liền." : $"Đã cắt {tk.diemCat.Count} chỗ.")}");
                        break;
                    }
                case BanMoNhan.Loai.Lop:
                    {
                        var l = Mau.Lop(nh.id);
                        if (l == null) return;
                        sb.Append($"<b>{l.ten}</b>\nĐã mở {Mathf.RoundToInt(l.TiLeMo() * 100f)} % · {l.SoCanhCat()} đoạn rạch.");
                        break;
                    }
                case BanMoNhan.Loai.Chi:
                    sb.Append($"<b>Chi {(nh.chi < 2 ? "trước" : "sau")} {(nh.chi % 2 == 0 ? "phải" : "trái")}</b>\n{(Mau.chiGhim[Mathf.Clamp(nh.chi, 0, 3)] ? "Đã ghim." : "Chưa ghim.")}");
                    break;
                case BanMoNhan.Loai.Dau:
                    sb.Append($"<b>Đầu ếch</b>\nNão: {(Mau.huyNao ? "đã hủy" : "còn")} · Tủy sống: {(Mau.huyTuy ? "đã hủy" : "còn")}");
                    break;
                default: return;
            }
            txtDoiTuong.text = sb.ToString();
        }

        // ======================================================================= giao diện

        Color NenSanKhau => UiTheme.Pick(new Color(0.86f, 0.88f, 0.91f), new Color(0.055f, 0.06f, 0.07f), new Color(0.04f, 0.075f, 0.13f));

        void DungUi()
        {
            if (UiCanvas != null) Destroy(UiCanvas.gameObject);
            nutDungCu.Clear(); nutThuoc.Clear();
            if (Cam != null) Cam.backgroundColor = NenSanKhau;

            var cv = new GameObject("BanMo_UI") { layer = 5 };
            cv.transform.SetParent(transform, false);
            UiCanvas = cv.AddComponent<Canvas>();
            UiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            UiCanvas.sortingOrder = 170;
            var sc = cv.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920f, 1080f);
            sc.matchWidthOrHeight = 0.5f;
            cv.AddComponent<GraphicRaycaster>();
            var r = cv.transform;
            bool ech = Mau.loai == LoaiMau.Ech;

            // Đầu trang
            var dau = TamNen(r, "DauTrang", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(1880f, 66f));
            GlassKit.Label(dau, "TieuDe", "MỔ ẾCH TỰ DO · VERA LAB", 20, GlassKit.Ink, true, TextAnchor.MiddleLeft, new Vector2(20f, -6f), new Vector2(420f, 30f));
            txtTieuDeMau = GlassKit.Label(dau, "MauMo", Mau.ten, 13, GlassKit.Dim, false, TextAnchor.MiddleLeft, new Vector2(20f, -36f), new Vector2(760f, 24f));
            GlassKit.MakeButton(dau, "DatLai", "Ếch mới", new Vector2(1200f, -11f), new Vector2(130f, 44f), () => DoiMau(LoaiMau.Ech), false, 15);
            GlassKit.MakeButton(dau, "GiaiPhau3D", "Mô hình giải phẫu 3D", new Vector2(1340f, -11f), new Vector2(230f, 44f), () =>
            {
                BanMoTuDoMoDun.Dong();
                GiaiPhauMoDunImpl.Mo();
            }, false, 15);
            GlassKit.MakeButton(dau, "Dong", "Đóng (Esc)", new Vector2(1720f, -11f), new Vector2(140f, 44f), BanMoTuDoMoDun.Dong, true, 15);

            // Trái: dụng cụ
            var trai = TamNen(r, "DungCu", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, -18f), new Vector2(360f, 880f));
            GlassKit.Label(trai, "TieuDeDC", "DỤNG CỤ", 14, GlassKit.Accent, true, TextAnchor.MiddleLeft, new Vector2(18f, -14f), new Vector2(320f, 22f));
            float y = -44f; int cot = 0;
            foreach (var m in DungCu)
            {
                if (m.chiEch && !ech || m.chiNguoi && ech) continue;
                var mm = m;
                var b = GlassKit.MakeChip(trai, "DC_" + m.dc, m.ten, new Vector2(16f + cot * 166f, y), new Vector2(160f, 44f), () => ChonDungCu(mm.dc), m.dc == DungCuDangCam, 14);
                nutDungCu[m.dc] = b;
                cot++;
                if (cot == 2) { cot = 0; y -= 52f; }
            }
            if (cot != 0) y -= 52f;
            hangThuoc = GlassKit.Rect(trai, "HangThuoc", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, y - 4f), new Vector2(360f, 150f));
            GlassKit.Label(hangThuoc, "TieuDeThuoc", ech ? "THUỐC NHỎ" : "THUỐC TIÊM TĨNH MẠCH", 13, GlassKit.Accent, true, TextAnchor.MiddleLeft, new Vector2(18f, 0f), new Vector2(320f, 20f));
            int dem = 0;
            foreach (var (t, ten) in DanhSachThuoc)
            {
                if (!ech && (t == ThuocMo.HCl || t == ThuocMo.RingerLanh)) continue;
                var tt = t;
                string nhan = !ech && t == ThuocMo.Ringer ? "Ringer lactat" : ten;
                nutThuoc[t] = GlassKit.MakeChip(hangThuoc, "Thuoc_" + t, nhan, new Vector2(16f + (dem % 2) * 166f, -26f - (dem / 2) * 46f), new Vector2(160f, 40f), () => ChonThuoc(tt), t == ThuocDangChon, 13);
                dem++;
            }
            txtMoTaDungCu = GlassKit.Label(trai, "MoTaDC", "", 14, GlassKit.Dim, false, TextAnchor.UpperLeft, new Vector2(18f, -700f), new Vector2(326f, 160f));
            ChonDungCu(DungCuDangCam);

            // Phải: sinh hiệu, ký đồ, đối tượng, nhật ký
            var phai = TamNen(r, "SinhHieu", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, -18f), new Vector2(430f, 880f));
            GlassKit.Label(phai, "TieuDeSH", ech ? "THEO DÕI MẪU" : "MÀN HÌNH SINH HIỆU", 14, GlassKit.Accent, true, TextAnchor.MiddleLeft, new Vector2(18f, -14f), new Vector2(380f, 22f));
            txtSinhHieu = GlassKit.Label(phai, "ChiSo", "", 15, GlassKit.Ink, false, TextAnchor.UpperLeft, new Vector2(18f, -42f), new Vector2(396f, 190f));
            GlassKit.Label(phai, "TieuDeKy", ech ? "Ký đồ co bóp tim (nhĩ nhỏ, thất lớn)" : "Co bóp tâm thất", 13, GlassKit.Dim, true, TextAnchor.MiddleLeft, new Vector2(18f, -236f), new Vector2(396f, 20f));
            var khung = GlassKit.Fill(phai, "NenKyDo", new Vector2(18f, -260f), new Vector2(394f, 100f), GlassKit.Well);
            kyDo = new GameObject("KyDo", typeof(RectTransform)).AddComponent<RawImage>();
            var krt = kyDo.rectTransform; krt.SetParent(khung.transform, false);
            krt.anchorMin = Vector2.zero; krt.anchorMax = Vector2.one; krt.offsetMin = new Vector2(7f, 2f); krt.offsetMax = new Vector2(-7f, -2f);
            kyDo.raycastTarget = false;
            TaoKyDo();
            GlassKit.Label(phai, "TieuDeDT", "ĐANG TRỎ", 13, GlassKit.Accent, true, TextAnchor.MiddleLeft, new Vector2(18f, -372f), new Vector2(396f, 20f));
            txtDoiTuong = GlassKit.Label(phai, "DoiTuong", "Rê chuột lên mẫu để xem tên và trạng thái.", 14, GlassKit.Ink, false, TextAnchor.UpperLeft, new Vector2(18f, -396f), new Vector2(396f, 190f));
            GlassKit.Label(phai, "TieuDeNK", "NHẬT KÝ", 13, GlassKit.Accent, true, TextAnchor.MiddleLeft, new Vector2(18f, -596f), new Vector2(396f, 20f));
            txtNhatKy = GlassKit.Label(phai, "NhatKy", "", 12, GlassKit.Dim, false, TextAnchor.UpperLeft, new Vector2(18f, -620f), new Vector2(396f, 250f));

            // Dưới: kết quả thao tác gần nhất + hướng dẫn chuột
            var duoi = TamNen(r, "ThongBao", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(1060f, 92f));
            nenThongBao = duoi.GetComponent<Image>();
            txtThongBao = GlassKit.Label(duoi, "Chu", ThongBaoCuoi, 15, GlassKit.Ink, false, TextAnchor.MiddleLeft, new Vector2(18f, -6f), new Vector2(1024f, 58f));
            GlassKit.Label(duoi, "HuongDan", "Chuột trái: dùng dụng cụ (kéo để rạch) · Chuột phải kéo: xoay · Cuộn: phóng to", 12, GlassKit.Muted, false, TextAnchor.MiddleLeft, new Vector2(18f, -64f), new Vector2(1024f, 22f));

            CapNhatChu();
        }

        static RectTransform TamNen(Transform cha, string ten, Vector2 neo, Vector2 truc, Vector2 viTri, Vector2 co)
        {
            var img = GlassKit.Panel(cha, ten, neo, truc, viTri, co, null, 2f);
            img.material = null;
            img.sprite = GlassKit.Solid;
            img.color = GlassKit.SurfaceA(0.94f);
            img.raycastTarget = true;
            return img.rectTransform;
        }

        void TaoKyDo()
        {
            if (kyDoTex == null)
            {
                kyDoTex = new Texture2D(KyW, KyH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "KyDoTim" };
                kyDoPx = new Color32[KyW * KyH];
            }
            Color32 nen = GlassKit.Well;
            for (int k = 0; k < kyDoPx.Length; k++) kyDoPx[k] = nen;
            kyDoTex.SetPixels32(kyDoPx); kyDoTex.Apply();
            kyDo.texture = kyDoTex;
            kyDoX = 0;
        }

        void VeKyDo(float dt)
        {
            if (kyDoTex == null || kyDo == null) return;
            kyDoTich += dt * 70f; // 70 điểm ảnh mỗi giây, cửa sổ ≈ 5,4 s
            int buoc = (int)kyDoTich;
            if (buoc <= 0) return;
            kyDoTich -= buoc;
            Color32 nen = GlassKit.Well, net = GlassKit.Accent, nhi = GlassKit.Dim;
            var s = Mau.sinhLy;
            int yThat = Mathf.Clamp(14 + Mathf.RoundToInt(s.thatCo * 66f), 1, KyH - 2);
            int yNhi = Mathf.Clamp(14 + Mathf.RoundToInt(s.nhiCo * 16f), 1, KyH - 2);
            for (int b = 0; b < buoc; b++)
            {
                int x = kyDoX;
                for (int k = 0; k < 6; k++) { int cx = (x + k) % KyW; for (int yy = 0; yy < KyH; yy++) kyDoPx[cx + yy * KyW] = nen; }
                for (int yy = -1; yy <= 1; yy++) { kyDoPx[x + Mathf.Clamp(yThat + yy, 0, KyH - 1) * KyW] = net; }
                kyDoPx[x + yNhi * KyW] = nhi;
                kyDoX = (kyDoX + 1) % KyW;
            }
            kyDoTex.SetPixels32(kyDoPx); kyDoTex.Apply(false);
        }

        void CapNhatChu()
        {
            if (Mau == null || txtSinhHieu == null) return;
            var s = Mau.sinhLy;
            bool ech = Mau.loai == LoaiMau.Ech;
            var sb = new StringBuilder();
            string tot = GlassKit.HexGood, canh = GlassKit.HexWarn, xau = GlassKit.HexBad;
            int hr = Mathf.RoundToInt(s.nhipDo);
            string mauHr = ech ? (hr < 15 ? xau : GlassKit.HexInk) : (hr < 50 || hr > 120 ? xau : hr > 100 ? canh : tot);
            sb.Append($"Nhịp tim: <b><color={mauHr}>{(s.ngungTim ? "NGỪNG" : hr + " lần/phút")}</color></b>");
            if (s.soNgoaiTamThu > 0) sb.Append($"  <color={GlassKit.HexDim}>· ngoại tâm thu {s.soNgoaiTamThu}</color>");
            sb.Append('\n');
            if (!ech)
            {
                float map = s.huyetAp;
                string mauAp = map < 55 ? xau : map < 70 ? canh : tot;
                sb.Append($"Huyết áp trung bình: <b><color={mauAp}>{Mathf.RoundToInt(map)} mmHg</color></b>\n");
            }
            else sb.Append($"Lực co tâm thất: <b>{Mathf.RoundToInt(s.lucCo * 100f)} %</b>\n");
            float mat = s.TiLeMatMau * 100f;
            sb.Append($"Máu mất: <b><color={(mat > 30 ? xau : mat > 15 ? canh : GlassKit.HexInk)}>{mat:0} %</color></b> ({Mau.MatMauMl:0.##} mL)");
            if (Mau.mauDong > 0.0001f) sb.Append($" · đọng {Mau.mauDong:0.##} mL");
            sb.Append('\n');
            if (ech)
            {
                sb.Append($"Hô hấp: <b>{(s.coTheTho ? "thở bằng thềm miệng" : s.phoi > 0.05f ? "phổi đang phồng do thổi" : "ngừng (đã hủy não)")}</b>\n");
                sb.Append($"Nhiệt độ mô: <b>{s.nhietDo:0} °C</b> · Độ ẩm mô hở: <b><color={(s.doAm < 0.35f ? canh : GlassKit.HexInk)}>{Mathf.RoundToInt(s.doAm * 100f)} %</color></b>\n");
                sb.Append($"Não: {(Mau.huyNao ? "đã hủy" : "còn")} · Tủy: {(Mau.huyTuy ? "đã hủy" : "còn")} · Ghim {Mau.soChiGhim}/4\n");
            }
            else
            {
                sb.Append($"Thở máy: <b>14 lần/phút</b>{(s.phoiRo > 0.1f ? $" <color={canh}>· rò khí</color>" : "")}\n");
                sb.Append($"Thân nhiệt: <b>{s.nhietDo:0.0} °C</b>\n");
            }
            sb.Append($"Nhu động ruột: <b>{s.tanSoNhuDong:0.#} lần/phút</b>");
            if (s.adr > 0.05f || s.ach > 0.05f || s.atropin > 0.05f)
                sb.Append($"\n<color={GlassKit.HexDim}>Thuốc: {(s.adr > 0.05f ? "adrenalin " : "")}{(s.ach > 0.05f ? "acetylcholin " : "")}{(s.atropin > 0.05f ? "atropin" : "")}</color>");
            txtSinhHieu.text = sb.ToString();

            if (txtNhatKy != null)
            {
                var nk = new StringBuilder();
                int tu = Mathf.Max(0, Mau.nhatKy.Count - 7);
                for (int k = Mau.nhatKy.Count - 1; k >= tu; k--) nk.Append("• ").Append(Mau.nhatKy[k]).Append('\n');
                txtNhatKy.text = nk.ToString();
            }
            if (txtThongBao != null && txtThongBao.text != ThongBaoCuoi) txtThongBao.text = ThongBaoCuoi;
        }
    }
}
