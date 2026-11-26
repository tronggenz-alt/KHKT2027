using System;
using System.Collections.Generic;
using System.Text;
using Khkt.Demo;
using Khkt.Demo.Bio.ThucVat;
using Khkt.Environment;
using Khkt.Rendering;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Khkt.Demo.Bio.GiaiPhau
{
    public enum CheDoGiaiPhau
    {
        KhamPha = 0,
        CauHoiNhanh = 1
    }

    /// <summary>
    /// Điều phối thực thi nội bộ của mô-đun Giải phẫu người 3D (G4) trong namespace Khkt.Demo.Bio.GiaiPhau
    /// nhằm tránh xung đột MenuKit.Bio trong MainMenu.cs. Cầu nối hợp đồng Khkt.Bio.GiaiPhau.GiaiPhauMoDun nằm ở Bridge/.
    /// </summary>
    public static class GiaiPhauMoDunImpl
    {
        static GiaiPhauTrinhDieuKhien controller;
        static Action pendingOnClosed;

        public static bool DangMo => controller != null;
        public static GiaiPhauTrinhDieuKhien HienTai => controller;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            controller = null;
            pendingOnClosed = null;
        }

        public static void Mo() => Mo(null);

        public static void Mo(Action onClosed)
        {
            if (controller != null)
            {
                if (onClosed != null) pendingOnClosed += onClosed;
                return;
            }
            pendingOnClosed = onClosed;
            var host = new GameObject("GiaiPhauMoDun_Host");
            UnityEngine.Object.DontDestroyOnLoad(host);
            controller = host.AddComponent<GiaiPhauTrinhDieuKhien>();
            controller.Init();
        }

        public static void Dong()
        {
            if (controller == null) return;
            var go = controller.gameObject;
            var cb = pendingOnClosed;
            pendingOnClosed = null;
            controller.CleanupBeforeDestroy();
            controller = null;
            if (go != null) UnityEngine.Object.Destroy(go);
            cb?.Invoke();
        }
    }

    public sealed class GiaiPhauTrinhDieuKhien : MonoBehaviour
    {
        struct MucHeCoQuan
        {
            public string id;
            public string nhan;
        }

        struct CauHoiChiCoQuan
        {
            public string deBai;
            public string idDapAn;
            public string tenDapAn;
            public string goiYHe;
            public string giaiThich;
        }

        static readonly MucHeCoQuan[] DanhSachHe =
        {
            new MucHeCoQuan { id = "xuong", nhan = "Xương" },
            new MucHeCoQuan { id = "he_co", nhan = "Cơ" },
            new MucHeCoQuan { id = "tieu_hoa", nhan = "Tiêu hóa" },
            new MucHeCoQuan { id = "tuan_hoan", nhan = "Tuần hoàn" },
            new MucHeCoQuan { id = "ho_hap", nhan = "Hô hấp" },
            new MucHeCoQuan { id = "than_kinh", nhan = "Thần kinh" },
            new MucHeCoQuan { id = "bai_tiet", nhan = "Bài tiết" },
            new MucHeCoQuan { id = "noi_tiet", nhan = "Nội tiết" },
            new MucHeCoQuan { id = "da", nhan = "Lớp da" }
        };

        static readonly CauHoiChiCoQuan[] BoCauHoiNhanh =
        {
            new CauHoiChiCoQuan
            {
                deBai = "Câu 1/5: Bấm chọn trên mô hình 3D cơ quan BƠM MÁU gồm 4 ngăn (thành tâm thất trái dày nhất).",
                idDapAn = "tim",
                tenDapAn = "Tim",
                goiYHe = "tuan_hoan",
                giaiThich = "Tim co bóp tạo lực đẩy máu đi qua hai vòng tuần hoàn (tâm thất trái đẩy máu giàu O₂ vào động mạch chủ)."
            },
            new CauHoiChiCoQuan
            {
                deBai = "Câu 2/5: Bấm chọn cơ quan TRAO ĐỔI KHÍ O₂ và CO₂ qua hàng trăm triệu phế nang.",
                idDapAn = "phoi",
                tenDapAn = "Phổi và đường dẫn khí",
                goiYHe = "ho_hap",
                giaiThich = "Hai lá phổi nằm trong lồng ngực thực hiện thông khí nhờ cơ hoành và các cơ liên sườn."
            },
            new CauHoiChiCoQuan
            {
                deBai = "Câu 3/5: Bấm chọn cơ quan TRUNG ƯƠNG THẦN KINH điều khiển ý thức, trí nhớ và vận động.",
                idDapAn = "nao",
                tenDapAn = "Não bộ",
                goiYHe = "than_kinh",
                giaiThich = "Não bộ gồm đại não, tiểu não và thân não nằm được bảo vệ trong hộp sọ."
            },
            new CauHoiChiCoQuan
            {
                deBai = "Câu 4/5: Bấm chọn cơ quan hình chữ J tiết HCl và enzyme pepsin tiêu hóa sơ bộ protein.",
                idDapAn = "da_day",
                tenDapAn = "Thực quản và dạ dày",
                goiYHe = "tieu_hoa",
                giaiThich = "Dạ dày co bóp nhào trộn thức ăn (nhu động) cùng dịch vị chứa HCl (pH ~1,5–2) và pepsin."
            },
            new CauHoiChiCoQuan
            {
                deBai = "Câu 5/5: Bấm chọn cơ quan LỌC MÁU tạo nước tiểu và điều hòa áp suất thẩm thấu cơ thể.",
                idDapAn = "than",
                tenDapAn = "Thận và đường tiết niệu",
                goiYHe = "bai_tiet",
                giaiThich = "Hai quả thận lọc máu qua các đơn vị thận (nephron), dẫn nước tiểu theo niệu quản xuống bàng quang."
            }
        };

        public GiaiPhauSanKhau3D SanKhau { get; private set; }
        public CameraQuyDao OrbitCam { get; private set; }
        public Camera StageCamera { get; private set; }
        public Canvas UiCanvas { get; private set; }
        public CheDoGiaiPhau CheDo { get; private set; } = CheDoGiaiPhau.KhamPha;
        public List<HoSoCoQuan> DanhSachCoQuan { get; private set; }

        public int CauHoiHienTai { get; private set; }
        public int DiemCauHoiNhanh { get; private set; }
        public int SoCauDaTraLoi { get; private set; }
        public bool[] DaTraLoiCau { get; } = new bool[5];

        Camera prevMainCam;
        bool prevMainCamEnabled;
        CursorLockMode prevCursorLock;
        bool prevCursorVisible;
        GameObject tempEventSystem;

        readonly Dictionary<string, Button> sysButtons = new Dictionary<string, Button>(10);
        readonly Dictionary<string, Image> sysBtnImgs = new Dictionary<string, Image>(10);
        readonly Dictionary<string, Button> organButtons = new Dictionary<string, Button>(16);
        readonly Button[] cutButtons = new Button[4];
        readonly Button[] modeButtons = new Button[2];

        Slider slBocLop, slViTriCat, slNhipTim;
        bool suppressCallbacks;
        Button btnTachRa, btnDongMau, btnNhipTho;
        Text txtTachRaLabel, txtCardTitle, txtCardMeta, txtCardBody, txtStatusBar, txtQuizPrompt, txtQuizFeedback, txtQuizScore;
        RectTransform cardPanel, quizPanel;

        public void Init()
        {
            DesktopViewer.UiCapture++;
            prevMainCam = Camera.main;
            if (prevMainCam != null)
            {
                prevMainCamEnabled = prevMainCam.enabled;
                prevMainCam.enabled = false;
            }
            prevCursorLock = Cursor.lockState;
            prevCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                tempEventSystem = new GameObject("GiaiPhau_TempEventSystem");
                tempEventSystem.AddComponent<EventSystem>();
                tempEventSystem.AddComponent<InputSystemUIInputModule>();
            }

            DanhSachCoQuan = Bp3dReader.TaiDanhMucCoQuan();

            // Sân khấu giải phẫu 3D trên Layer 29
            var stageGo = new GameObject("SanKhauGiaiPhau_3D");
            stageGo.transform.SetParent(transform, false);

            var camGo = new GameObject("CameraSanKhauGiaiPhau") { layer = GiaiPhauSanKhau3D.AnatomyLayer };
            camGo.transform.SetParent(transform, false);
            StageCamera = camGo.AddComponent<Camera>();
            StageCamera.clearFlags = CameraClearFlags.SolidColor;
            StageCamera.backgroundColor = NenSanKhau;
            StageCamera.cullingMask = 1 << GiaiPhauSanKhau3D.AnatomyLayer;
            StageCamera.nearClipPlane = 0.025f;
            StageCamera.farClipPlane = 35f;
            StageCamera.depth = 65f;
            StageCamera.fieldOfView = 42f;

            OrbitCam = camGo.AddComponent<CameraQuyDao>();
            OrbitCam.TargetCamera = StageCamera;
            OrbitCam.PickLayerMask = 1 << GiaiPhauSanKhau3D.AnatomyLayer;
            OrbitCam.MinDistance = 0.22f;
            OrbitCam.MaxDistance = 4.6f;
            OrbitCam.OnEscapePressed += GiaiPhauMoDunImpl.Dong;
            OrbitCam.OnPartClicked += OnColliderClicked;
            OrbitCam.OnPartDoubleClicked += OnColliderDoubleClicked;

            SanKhau = stageGo.AddComponent<GiaiPhauSanKhau3D>();
            SanKhau.KhoiTao(HardwareProfile.Muc);

            BuildUi();
            UiTheme.Changed += KhiDoiChuDe;

            DatCheDo(CheDoGiaiPhau.KhamPha);
            ChonCoQuan("tim");
            DatGocNhinToanThan(instant: true);
            CapNhatUiTrangThai();
        }

        static Color NenSanKhau => UiTheme.Pick(new Color(0.86f, 0.88f, 0.91f), new Color(0.045f, 0.050f, 0.060f), new Color(0.045f, 0.070f, 0.105f));

        /// <summary>Đổi chủ đề Sáng/Tối khi đang mở: dựng lại giao diện bằng màu mới, giữ nguyên trạng thái mô hình.</summary>
        void KhiDoiChuDe()
        {
            if (StageCamera != null) StageCamera.backgroundColor = NenSanKhau;
            if (UiCanvas != null) Destroy(UiCanvas.gameObject);
            sysButtons.Clear(); sysBtnImgs.Clear(); organButtons.Clear();
            BuildUi();
            DatCheDo(CheDo);
            if (!string.IsNullOrEmpty(SanKhau?.CoQuanDangChon)) CapNhatTheKienThuc(SanKhau.CoQuanDangChon);
            CapNhatUiTrangThai();
        }

        public void CleanupBeforeDestroy()
        {
            UiTheme.Changed -= KhiDoiChuDe;
            DesktopViewer.UiCapture = Mathf.Max(0, DesktopViewer.UiCapture - 1);
            if (SanKhau != null) SanKhau.GiaiPhongTaiNguyen();
            if (prevMainCam != null) prevMainCam.enabled = prevMainCamEnabled;
            Cursor.lockState = prevCursorLock;
            Cursor.visible = prevCursorVisible;
            if (tempEventSystem != null) Destroy(tempEventSystem);
            if (UiCanvas != null) Destroy(UiCanvas.gameObject);
        }

        // ==================== TƯƠNG TÁC TỪ 3D & UI & KIỂM THỬ ====================

        void OnColliderClicked(Collider col)
        {
            if (col == null) return;
            var marker = col.GetComponent<CoQuan3DMarker>();
            if (marker == null || string.IsNullOrEmpty(marker.IdCoQuan)) return;
            XuLyChonCoQuan(marker.IdCoQuan, doubleClick: false);
        }

        void OnColliderDoubleClicked(Collider col)
        {
            if (col == null) return;
            var marker = col.GetComponent<CoQuan3DMarker>();
            if (marker == null || string.IsNullOrEmpty(marker.IdCoQuan)) return;
            XuLyChonCoQuan(marker.IdCoQuan, doubleClick: true);
        }

        public void XuLyChonCoQuan(string idCoQuan, bool doubleClick = false)
        {
            if (string.IsNullOrEmpty(idCoQuan)) return;

            if (CheDo == CheDoGiaiPhau.CauHoiNhanh)
            {
                TraLoiChiCoQuan(idCoQuan);
                return;
            }

            ChonCoQuan(idCoQuan);
            if (doubleClick)
                OrbitCam?.FocusOn(SanKhau.LayKhungCoQuanDangChon(), 2.25f, false);
        }

        public void ChonCoQuan(string idCoQuan)
        {
            if (SanKhau == null) return;
            SanKhau.ChonCoQuan(idCoQuan);
            if (SanKhau.DangTachRa)
                OrbitCam?.FocusOn(SanKhau.LayKhungCoQuanDangChon(), 2.35f, false);
            CapNhatTheKienThuc(idCoQuan);
            CapNhatUiTrangThai();
        }

        public void DatHeCoQuan(string heId, bool bat)
        {
            if (SanKhau == null) return;
            SanKhau.DatHe(heId, bat);
            CapNhatUiTrangThai();
        }

        public void BatTatHeCoQuan(string heId)
        {
            if (SanKhau == null) return;
            SanKhau.DatHe(heId, !SanKhau.LayTrangThaiHe(heId));
            CapNhatUiTrangThai();
        }

        public void DatBocLop(float v)
        {
            if (SanKhau == null) return;
            SanKhau.DatBocLop(v);
            suppressCallbacks = true;
            if (slBocLop != null) slBocLop.value = SanKhau.BocLop;
            suppressCallbacks = false;
            CapNhatUiTrangThai();
        }

        public void DatMatCat(CheDoMatCat cheDo, float viTri)
        {
            if (SanKhau == null) return;
            SanKhau.DatMatCat(cheDo, viTri);
            suppressCallbacks = true;
            if (slViTriCat != null) slViTriCat.value = SanKhau.ViTriCat;
            suppressCallbacks = false;
            CapNhatUiTrangThai();
        }

        public void DatTachRa(bool tach)
        {
            if (SanKhau == null) return;
            if (tach && string.IsNullOrEmpty(SanKhau.CoQuanDangChon))
                SanKhau.ChonCoQuan("tim");
            SanKhau.DatTachRa(tach);
            if (tach)
                OrbitCam?.SetTarget(SanKhau.LayKhungCoQuanDangChon().center, 166f, 10f, 0.72f, true);
            else
                DatGocNhinToanThan(instant: true);
            CapNhatUiTrangThai();
        }

        public void DatNhipTim(float bpm)
        {
            if (SanKhau == null) return;
            SanKhau.DatNhipTim(bpm);
            suppressCallbacks = true;
            if (slNhipTim != null) slNhipTim.value = SanKhau.NhipTimBpm;
            suppressCallbacks = false;
            CapNhatUiTrangThai();
        }

        public void DatGocNhinToanThan(bool instant = false)
        {
            if (SanKhau == null || OrbitCam == null) return;
            Vector3 pivot = GiaiPhauSanKhau3D.StageOrigin + new Vector3(0f, 0.90f, 0f);
            // Yaw 168°: camera đứng phía trước (+Z hơi lệch trái) nhìn thẳng vào mặt trước cơ thể
            OrbitCam.SetTarget(pivot, 168f, 8f, 2.35f, instant);
        }

        public void DatGocNhinNoiTang(bool instant = false)
        {
            if (SanKhau == null || OrbitCam == null) return;
            Vector3 chestCenter = GiaiPhauSanKhau3D.StageOrigin + new Vector3(0f, 1.15f, 0.02f);
            OrbitCam.SetTarget(chestCenter, 166f, 8f, 0.98f, instant);
        }

        public void DatCheDo(CheDoGiaiPhau cheDoMoi)
        {
            CheDo = cheDoMoi;
            if (cardPanel != null)
            {
                bool showCard = CheDo == CheDoGiaiPhau.KhamPha;
                cardPanel.gameObject.SetActive(showCard);
                if (showCard) UiMotion.Popup(this, cardPanel, null, 0.22f);
            }
            if (quizPanel != null)
            {
                bool showQuiz = CheDo == CheDoGiaiPhau.CauHoiNhanh;
                quizPanel.gameObject.SetActive(showQuiz);
                if (showQuiz) UiMotion.Popup(this, quizPanel, null, 0.22f);
            }

            if (CheDo == CheDoGiaiPhau.CauHoiNhanh)
            {
                if (SanKhau != null && SanKhau.DangTachRa) DatTachRa(false);
                if (SanKhau != null && SanKhau.BocLop < 0.72f) DatBocLop(0.75f);
                ChuyenCauHoiNhanh(CauHoiHienTai);
            }
            CapNhatUiTrangThai();
        }

        public void BatDauLaiCauHoiNhanh()
        {
            DiemCauHoiNhanh = 0;
            SoCauDaTraLoi = 0;
            for (int i = 0; i < DaTraLoiCau.Length; i++) DaTraLoiCau[i] = false;
            ChuyenCauHoiNhanh(0);
        }

        public void ChuyenCauHoiNhanh(int idx)
        {
            CauHoiHienTai = ((idx % BoCauHoiNhanh.Length) + BoCauHoiNhanh.Length) % BoCauHoiNhanh.Length;
            var q = BoCauHoiNhanh[CauHoiHienTai];
            if (SanKhau != null && !string.IsNullOrEmpty(q.goiYHe))
                SanKhau.DatHe(q.goiYHe, true);
            if (txtQuizPrompt != null) txtQuizPrompt.text = q.deBai;
            if (txtQuizFeedback != null) txtQuizFeedback.text = "Hãy bấm trực tiếp lên cơ quan tương ứng trong mô hình 3D (hoặc xoay/phóng to để quan sát rõ hơn).";
            if (txtQuizScore != null) txtQuizScore.text = $"Điểm hiện tại: {DiemCauHoiNhanh} / {BoCauHoiNhanh.Length} (Đã trả lời {SoCauDaTraLoi}/{BoCauHoiNhanh.Length})";
        }

        public bool TraLoiChiCoQuan(string idCoQuan)
        {
            var q = BoCauHoiNhanh[CauHoiHienTai];
            SanKhau?.ChonCoQuan(idCoQuan);
            bool dung = idCoQuan == q.idDapAn;
            if (!DaTraLoiCau[CauHoiHienTai])
            {
                DaTraLoiCau[CauHoiHienTai] = true;
                SoCauDaTraLoi++;
                if (dung) DiemCauHoiNhanh++;
            }

            if (txtQuizFeedback != null)
            {
                txtQuizFeedback.text = dung
                    ? $"✓ CHÍNH XÁC ({q.tenDapAn})!\n{q.giaiThich}"
                    : $"✗ Bạn vừa chọn '{TimTenCoQuan(idCoQuan)}'. Đáp án đúng là '{q.tenDapAn}'.\n{q.giaiThich}";
                txtQuizFeedback.color = dung ? GlassKit.Good : GlassKit.Warn;
            }
            if (txtQuizScore != null)
                txtQuizScore.text = $"Điểm hiện tại: {DiemCauHoiNhanh} / {BoCauHoiNhanh.Length} (Đã trả lời {SoCauDaTraLoi}/{BoCauHoiNhanh.Length})";
            return dung;
        }

        string TimTenCoQuan(string id)
        {
            var hs = Bp3dReader.TimCoQuan(id);
            return hs != null ? hs.ten : id;
        }

        void CapNhatTheKienThuc(string id)
        {
            var hs = Bp3dReader.TimCoQuan(id);
            if (hs == null) return;
            if (txtCardTitle != null) txtCardTitle.text = hs.ten;
            if (txtCardMeta != null) txtCardMeta.text = hs.chuongTrinh;

            var sb = new StringBuilder(1024);
            AppendSection(sb, "VỊ TRÍ", hs.viTri, 2);
            AppendSection(sb, "CẤU TẠO", hs.cauTao, 3);
            AppendSection(sb, "CHỨC NĂNG & CƠ CHẾ", hs.chucNang, 2);
            AppendSection(sb, "CƠ CHẾ SINH LÝ", hs.coChe, 2);
            AppendSection(sb, "LIÊN HỆ THỰC TẾ", hs.lienHe, 2);
            AppendSection(sb, "ĐIỂM NHẤN ÔN THI", hs.diemThi, 2);
            if (txtCardBody != null) txtCardBody.text = sb.ToString();
        }

        static void AppendSection(StringBuilder sb, string header, string[] lines, int maxLines)
        {
            if (lines == null || lines.Length == 0) return;
            sb.Append("<b><color=").Append(GlassKit.HexAccent).Append(">").Append(header).Append("</color></b>\n");
            int cnt = Mathf.Min(maxLines, lines.Length);
            for (int i = 0; i < cnt; i++)
                sb.Append("• ").Append(lines[i]).Append('\n');
            sb.Append('\n');
        }

        void CapNhatUiTrangThai()
        {
            if (SanKhau == null) return;

            foreach (var kv in sysButtons) GlassKit.SetChip(kv.Value, SanKhau.LayTrangThaiHe(kv.Key));
            for (int i = 0; i < cutButtons.Length; i++) GlassKit.SetChip(cutButtons[i], (int)SanKhau.CheDoCat == i);
            for (int i = 0; i < modeButtons.Length; i++) GlassKit.SetChip(modeButtons[i], (int)CheDo == i);
            foreach (var kv in organButtons) GlassKit.SetChip(kv.Value, SanKhau.CoQuanDangChon == kv.Key);
            if (txtTachRaLabel != null)
                txtTachRaLabel.text = SanKhau.DangTachRa ? "↩ Đưa về cơ thể" : "◎ Tách ra xoay riêng";
            GlassKit.SetChip(btnTachRa, SanKhau.DangTachRa);

            if (txtStatusBar != null)
            {
                string catLabel = SanKhau.CheDoCat switch
                {
                    CheDoMatCat.CatDoc => "Cắt dọc",
                    CheDoMatCat.CatNgang => "Cắt ngang",
                    CheDoMatCat.CatTran => "Cắt trán",
                    _ => "Tắt"
                };
                txtStatusBar.text =
                    $"Mức đồ họa: {HardwareProfile.Muc} · Bóc lớp: {Mathf.RoundToInt(SanKhau.BocLop * 100f)}% · Mặt cắt: {catLabel} · Nhịp tim: {Mathf.RoundToInt(SanKhau.NhipTimBpm)} lần/phút";
            }
        }

        // ==================== DỰNG GIAO DIỆN TIẾNG VIỆT ====================

        void BuildUi()
        {
            var cvGo = new GameObject("GiaiPhau_UI_Canvas") { layer = 5 };
            UiCanvas = cvGo.AddComponent<Canvas>();
            UiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            UiCanvas.sortingOrder = 160;
            var scaler = cvGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0f;
            cvGo.AddComponent<GraphicRaycaster>();

            var root = cvGo.transform;

            // 1. Thanh tiêu đề trên cùng
            var header = MakeSurfacePanel(root, "ThanhTieuDe", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(1880f, 68f));
            GlassKit.Label(header, "TieuDeChinh", "GIẢI PHẪU NGƯỜI 3D · VERA LAB", 20, GlassKit.Ink, true, TextAnchor.MiddleLeft,
                new Vector2(20f, -6f), new Vector2(540f, 34f));
            txtStatusBar = GlassKit.Label(header, "ThanhTrangThai", "", 13, GlassKit.Dim, false, TextAnchor.MiddleLeft,
                new Vector2(20f, -38f), new Vector2(680f, 24f));

            modeButtons[0] = MakeDarkButton(header, "CheDoKhamPha", "Khám phá 3D", new Vector2(760f, -12f), new Vector2(160f, 44f),
                () => DatCheDo(CheDoGiaiPhau.KhamPha), true, 14);
            modeButtons[1] = MakeDarkButton(header, "CheDoCauHoi", "Câu hỏi nhanh (5 câu)", new Vector2(930f, -12f), new Vector2(210f, 44f),
                () => DatCheDo(CheDoGiaiPhau.CauHoiNhanh), false, 14);

            MakeDarkButton(header, "GocToanThan", "Toàn thân", new Vector2(1150f, -12f), new Vector2(120f, 44f),
                () => { if (SanKhau.DangTachRa) DatTachRa(false); else DatGocNhinToanThan(false); }, false, 14);
            btnTachRa = MakeDarkButton(header, "NutTachRa", "◎ Tách ra xoay riêng", new Vector2(1280f, -12f), new Vector2(220f, 44f),
                () => DatTachRa(!SanKhau.DangTachRa), true, 14);
            // Sang bàn mổ tự do (ếch, người) ngay trong app
            MakeDarkButton(header, "NutBanMo", "Mổ ếch tự do", new Vector2(1510f, -12f), new Vector2(200f, 44f),
                () => BanMoTuDoMoDun.Mo(Khkt.Simulation.Bio.GiaiPhau.LoaiMau.Ech), false, 14);
            txtTachRaLabel = btnTachRa.GetComponentInChildren<Text>();

            MakeDarkButton(header, "DongMoDun", "✕ Đóng (Esc)", new Vector2(1720f, -12f), new Vector2(142f, 44f),
                GiaiPhauMoDunImpl.Dong, false, 14);

            // 2. Bảng điều khiển bên trái
            var left = MakeSurfacePanel(root, "BangDieuKhienTrai", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, -10f), new Vector2(430f, 920f));
            float ly = -14f;

            GlassKit.Label(left, "TieuDeHe", "8 HỆ CƠ QUAN & LỚP DA", 14, GlassKit.Accent, true, TextAnchor.MiddleLeft,
                new Vector2(16f, ly), new Vector2(390f, 24f));
            ly -= 28f;

            for (int i = 0; i < DanhSachHe.Length; i++)
            {
                var h = DanhSachHe[i];
                int col = i % 3;
                int row = i / 3;
                var btn = MakeDarkButton(left, "He_" + h.id, h.nhan,
                    new Vector2(16f + col * 134f, ly - row * 40f), new Vector2(128f, 34f),
                    () => BatTatHeCoQuan(h.id), true, 13);
                sysButtons[h.id] = btn;
                sysBtnImgs[h.id] = btn.GetComponent<Image>();
            }
            ly -= 128f;

            slBocLop = MakeDarkSlider(left, "TruotBocLop", "Bóc lớp (Da → Cơ → Xương & Nội tạng)", ly, 0f, 1f, SanKhau.BocLop,
                v => { if (!suppressCallbacks) DatBocLop(v); }, v => $"{Mathf.RoundToInt(v * 100f)}%");
            ly -= 64f;

            GlassKit.Label(left, "TieuDeMatCat", "MẶT PHẲNG CẮT GIẢI PHẪU", 14, GlassKit.Accent, true, TextAnchor.MiddleLeft,
                new Vector2(16f, ly), new Vector2(390f, 22f));
            ly -= 26f;

            string[] cutLabels = { "Tắt", "Cắt dọc (X)", "Cắt ngang (Y)", "Cắt trán (Z)" };
            for (int i = 0; i < 4; i++)
            {
                var mode = (CheDoMatCat)i;
                cutButtons[i] = MakeDarkButton(left, "Cat_" + i, cutLabels[i],
                    new Vector2(16f + i * 100f, ly), new Vector2(94f, 34f),
                    () => DatMatCat(mode, SanKhau.ViTriCat), false, 12);
            }
            ly -= 40f;

            slViTriCat = MakeDarkSlider(left, "TruotViTriCat", "Vị trí mặt phẳng cắt", ly, -1f, 1f, 0f,
                v => { if (!suppressCallbacks) DatMatCat(SanKhau.CheDoCat == CheDoMatCat.Tat ? CheDoMatCat.CatDoc : SanKhau.CheDoCat, v); },
                v => $"{v:+0.00;-0.00;0.00}");
            ly -= 64f;

            slNhipTim = MakeDarkSlider(left, "TruotNhipTim", "Nhịp tim (Tim đập & Dòng máu)", ly, 60f, 120f, 75f,
                v => { if (!suppressCallbacks) DatNhipTim(v); }, v => $"{Mathf.RoundToInt(v)} lần/phút");
            ly -= 64f;

            btnDongMau = MakeDarkButton(left, "NutDongMau", "Dòng máu đỏ/xanh: BẬT", new Vector2(16f, ly), new Vector2(194f, 34f),
                () =>
                {
                    SanKhau.DatHoatAnh(!SanKhau.BatDongMau, SanKhau.BatNhipTho);
                    btnDongMau.GetComponentInChildren<Text>().text = SanKhau.BatDongMau ? "Dòng máu đỏ/xanh: BẬT" : "Dòng máu: TẮT";
                }, true, 12);
            btnNhipTho = MakeDarkButton(left, "NutNhipTho", "Phổi thở & Nhu động: BẬT", new Vector2(218f, ly), new Vector2(194f, 34f),
                () =>
                {
                    SanKhau.DatHoatAnh(SanKhau.BatDongMau, !SanKhau.BatNhipTho);
                    btnNhipTho.GetComponentInChildren<Text>().text = SanKhau.BatNhipTho ? "Phổi thở & Nhu động: BẬT" : "Phổi thở: TẮT";
                }, true, 12);
            ly -= 44f;

            GlassKit.Label(left, "TieuDe15CoQuan", "CHỌN NHANH 15 CƠ QUAN (NHẤP TRÊN MÔ HÌNH HOẶC Ở ĐÂY)", 13, GlassKit.Accent, true, TextAnchor.MiddleLeft,
                new Vector2(16f, ly), new Vector2(398f, 22f));
            ly -= 26f;

            for (int i = 0; i < DanhSachCoQuan.Count; i++)
            {
                var cq = DanhSachCoQuan[i];
                int col = i % 3;
                int row = i / 3;
                string shortName = cq.ten switch
                {
                    "Phổi và đường dẫn khí" => "Phổi",
                    "Tuỷ sống và dây thần kinh" => "Tuỷ sống",
                    "Thận và đường tiết niệu" => "Thận",
                    "Thực quản và dạ dày" => "Dạ dày",
                    "Gan và túi mật" => "Gan & Mật",
                    "Hệ mạch máu" => "Mạch máu",
                    _ => cq.ten
                };
                var btn = MakeDarkButton(left, "CQ_" + cq.id, shortName,
                    new Vector2(16f + col * 134f, ly - row * 38f), new Vector2(128f, 32f),
                    () => XuLyChonCoQuan(cq.id, doubleClick: false), false, 12);
                organButtons[cq.id] = btn;
            }

            // 3. Cột phải A: Thẻ kiến thức cơ quan
            cardPanel = MakeSurfacePanel(root, "TheKienThuc", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-18f, -10f), new Vector2(450f, 920f));
            txtCardTitle = GlassKit.Label(cardPanel, "TenCoQuan", "Tim", 22, GlassKit.Ink, true, TextAnchor.MiddleLeft,
                new Vector2(20f, -16f), new Vector2(410f, 34f));
            txtCardMeta = GlassKit.Label(cardPanel, "ChuongTrinhCoQuan", "", 13, GlassKit.Accent, false, TextAnchor.UpperLeft,
                new Vector2(20f, -52f), new Vector2(410f, 38f));
            txtCardBody = GlassKit.Label(cardPanel, "NoiDungTheKienThuc", "", 13, GlassKit.Ink, false, TextAnchor.UpperLeft,
                new Vector2(20f, -96f), new Vector2(410f, 750f));
            txtCardBody.supportRichText = true;
            MakeDarkButton(cardPanel, "TachCoQuanTuThe", "◎ Tách cơ quan đang chọn ra xoay 360°", new Vector2(20f, -856f), new Vector2(410f, 44f),
                () => DatTachRa(!SanKhau.DangTachRa), true, 14);

            // 4. Cột phải B: Câu hỏi nhanh 5 câu (chỉ vào cơ quan trên mô hình 3D)
            quizPanel = MakeSurfacePanel(root, "BangCauHoiNhanh", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-18f, -10f), new Vector2(450f, 920f));
            GlassKit.Label(quizPanel, "TieuDeQuiz", "CÂU HỎI NHANH CHỈ CƠ QUAN (5 CÂU)", 17, GlassKit.Accent, true, TextAnchor.MiddleLeft,
                new Vector2(20f, -18f), new Vector2(410f, 30f));
            txtQuizScore = GlassKit.Label(quizPanel, "DiemQuiz", "Điểm hiện tại: 0 / 5", 15, GlassKit.Good, true, TextAnchor.MiddleLeft,
                new Vector2(20f, -52f), new Vector2(410f, 28f));
            txtQuizPrompt = GlassKit.Label(quizPanel, "DeBaiQuiz", "", 16, GlassKit.Ink, true, TextAnchor.UpperLeft,
                new Vector2(20f, -96f), new Vector2(410f, 140f));
            txtQuizFeedback = GlassKit.Label(quizPanel, "PhanHoiQuiz", "", 15, GlassKit.Ink, false, TextAnchor.UpperLeft,
                new Vector2(20f, -256f), new Vector2(410f, 220f));
            MakeDarkButton(quizPanel, "CauTruocBtn", "← Câu trước", new Vector2(20f, -510f), new Vector2(198f, 46f),
                () => ChuyenCauHoiNhanh(CauHoiHienTai - 1), false, 14);
            MakeDarkButton(quizPanel, "CauSauBtn", "Câu tiếp theo →", new Vector2(232f, -510f), new Vector2(198f, 46f),
                () => ChuyenCauHoiNhanh(CauHoiHienTai + 1), true, 14);
            MakeDarkButton(quizPanel, "LamLaiQuizBtn", "Làm lại từ đầu (5 câu)", new Vector2(20f, -572f), new Vector2(410f, 44f),
                BatDauLaiCauHoiNhanh, false, 14);

            // 5. Thanh bản quyền BodyParts3D dưới đáy màn hình (bắt buộc giữ nguyên ghi nhận nguồn)
            var footer = MakeSurfacePanel(root, "ChanTrangBanQuyen", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(960f, 40f));
            GlassKit.Label(footer, "ChuBanQuyenBP3D",
                "BodyParts3D © The Database Center for Life Science · CC BY-SA 2.1 JP / CC BY 4.0  |  Kéo xoay · Cuộn thu phóng · Bấm chọn cơ quan",
                13, GlassKit.Dim, false, TextAnchor.MiddleCenter,
                new Vector2(10f, -4f), new Vector2(940f, 32f));

            SetLayerRecursive(cvGo.transform, 5);
            UiMotion.Popup(this, header, null, 0.22f);
            UiMotion.Popup(this, left, null, 0.24f);
            UiMotion.Popup(this, footer, null, 0.22f);
        }

        static void SetLayerRecursive(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++)
                SetLayerRecursive(t.GetChild(i), layer);
        }

        static RectTransform MakeSurfacePanel(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var img = GlassKit.Panel(parent, name, anchor, pivot, pos, size, null, 2f);
            img.material = null;
            img.sprite = GlassKit.Solid;
            img.color = GlassKit.SurfaceA(0.94f);
            img.raycastTarget = true;
            return img.rectTransform;
        }

        static Button MakeDarkButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, Action onClick, bool primary, int fontSize)
        {
            var btn = GlassKit.MakeButton(parent, name, label, pos, size, onClick, primary, fontSize);
            var txt = btn.GetComponentInChildren<Text>();
            if (txt != null) txt.color = GlassKit.Ink;
            return btn;
        }

        static Slider MakeDarkSlider(Transform parent, string name, string title, float y, float min, float max, float val, Action<float> onChanged, Func<float, string> fmt)
        {
            return GlassKit.MakeSlider(parent, name, title, new Vector2(16f, y), new Vector2(398f, 56f), min, max, val, onChanged, fmt);
        }
    }
}
