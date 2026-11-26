using System;
using System.Collections.Generic;
using Khkt.Demo;
using Khkt.Environment;
using Khkt.Menu.Dashboard;
using Khkt.Rendering;
using Khkt.Simulation.Bio.ThucVat;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Khkt.Demo.Bio.ThucVat
{
    public enum CheDoThucVat
    {
        BaiGiang = 0,
        TuDo = 1,
        CauHoi = 2
    }

    /// <summary>
    /// Mô-đun Mô hình 3D Thực vật (Cây hai lá mầm + 3 lát cắt vi thể Rễ/Thân/Lá + 6 Quá trình sinh lý 3D từng bước có mũi tên).
    /// Thiết kế đồng bộ chủ đề Tối giản - Chuyên nghiệp của Phòng Giải phẫu 3D:
    ///   • 6 Quá trình sinh lý 3D từng bước (Hút nước & khoáng, Quang hợp C3, Hô hấp tế bào, Thoát hơi nước,
    ///     Dòng mạch rây, Hướng sáng Auxin) kèm mũi tên 3D nhỏ gọn bám chính xác điểm đầu/điểm cuối & bảng điều khiển bước.
    ///   • Nhãn chú thích 3D kiểu viên thuốc tối bo tròn kèm điểm chấm màu viền trắng (◉ Tên bộ phận) bấm trực tiếp được.
    ///   • Thanh trượt mảnh tinh gọn (núm tròn trắng nhỏ 12px không che chữ), chẩn đoán yếu tố giới hạn & phương trình sinh lý trực tiếp.
    ///   • Hỗ trợ đầy đủ Chuột & MacBook Trackpad xoay 360° + Zoom In/Out mượt mà.
    /// </summary>
    public static class ThucVatMoDunImpl
    {
        static ThucVatStageController instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        public static bool DangMo => instance != null && instance.gameObject.activeInHierarchy;
        public static ThucVatStageController HienTai => instance;

        public static void Mo()
        {
            if (DangMo) return;
            if (instance == null)
            {
                var go = new GameObject("ThucVatMoDun_Stage");
                UnityEngine.Object.DontDestroyOnLoad(go);
                instance = go.AddComponent<ThucVatStageController>();
                instance.Init();
            }
            else
            {
                instance.gameObject.SetActive(true);
                instance.OnReopen();
            }
        }

        public static void Dong()
        {
            if (instance == null) return;
            instance.OnCloseClean();
            UnityEngine.Object.Destroy(instance.gameObject);
            instance = null;
        }
    }

    public class ThucVatStageController : MonoBehaviour
    {
        public Camera StageCamera { get; private set; }
        public CameraQuyDao OrbitCam { get; private set; }
        public CayHaiLaMam3D Cay3D { get; private set; }
        public ThucVatMuiTen3D MuiTen3D { get; private set; }
        public Canvas UiCanvas { get; private set; }

        public DieuKienMoiTruong DieuKien => dk;
        public TrangThaiThucVat TrangThai => trangThai;

        DieuKienMoiTruong dk = DieuKienMoiTruong.MacDinh;
        TrangThaiThucVat trangThai;
        CheDoThucVat cheDo = CheDoThucVat.BaiGiang;
        int buocBaiGiang;
        int cauHoiIdx;
        int cauHoiDung;
        int cauHoiDaTraLoi;
        int tabKienThuc;
        bool hienDapAnNhanh;
        int kichBanDangChon = -1;
        bool dangLayNetBoPhan;
        bool leftCard2ShowPresets;
        bool? particlesBeforeProcess;

        bool cheDoChiTren3D;
        int chi3DIdx;
        static readonly (string prompt, string partId, KhungNhinThucVat view)[] BaiTapChi3D =
        {
            ("Hãy bấm trực tiếp vào ĐAI CASPARI (vòng suberin chặn dòng gian bào) trên Lát cắt Rễ:", "re_dai_caspari", KhungNhinThucVat.LatCatRe),
            ("Hãy bấm trực tiếp vào MẠCH GỖ HÌNH SAO 4 CÁNH (dẫn nước & khoáng đi lên) ở tâm Lát cắt Rễ:", "re_mach_go", KhungNhinThucVat.LatCatRe),
            ("Hãy bấm trực tiếp vào TẦNG SINH MẠCH / TƯỢNG TẦNG (sinh trưởng thứ cấp) trên Lát cắt Thân:", "than_tang_sinh_mach", KhungNhinThucVat.LatCatThan),
            ("Hãy bấm trực tiếp vào BÓ MẠCH RÂY (nằm phía ngoài bó mạch) trên Lát cắt Thân:", "than_mach_ray", KhungNhinThucVat.LatCatThan),
            ("Hãy bấm trực tiếp vào TẾ BÀO MÔ GIẬU (chứa nhiều hạt lục lạp nhất) trên Lát cắt Lá:", "la_mo_giau_va_mo_xop", KhungNhinThucVat.LatCatLa),
            ("Hãy bấm trực tiếp vào KHÍ KHỔNG (2 tế bào hình hạt đậu ở biểu bì dưới) trên Lát cắt Lá:", "la_khi_khong", KhungNhinThucVat.LatCatLa)
        };

        float dayTimeScale = 1f;
        bool autoDayCycle;
        float simulatedHour = 10.5f;

        Camera prevMainCam;
        bool prevMainCamEnabled;
        CursorLockMode prevCursorLock;
        bool prevCursorVisible;
        GameObject tempEventSystem;

        // Thanh trượt thông số mảnh + danh sách đồng bộ chữ giá trị
        Slider slLight, slCo2, slTemp, slRh, slSoil, slWind, slLampAngle;
        readonly List<Action> syncSliderLabels = new List<Action>(8);
        bool suppressSliderCallbacks;

        // Nhãn chú thích 3D viên thuốc (◉ Tên bộ phận) đồng bộ Phòng Giải phẫu
        struct PinBadgeUi
        {
            public PinChuThich3D pin;
            public RectTransform root;
            public Image pillRing;
            public Image pillFace;
            public Text label;
        }
        RectTransform pinLayerRt;
        readonly List<PinBadgeUi> pinBadges = new List<PinBadgeUi>();

        // Nhãn nổi trên mũi tên của bước quá trình đang chạy (① Tiêu đề bước)
        RectTransform activeStepBadgeRt;
        Image activeStepNumBg, activeStepPillRing;
        Text activeStepNumTxt, activeStepTitleTxt;

        readonly Button[] modeChips = new Button[3];
        readonly Button[] viewChips = new Button[4];
        readonly Button[] speedChips = new Button[3];
        readonly Button[] presetChips = new Button[5];
        readonly Button[] processQuickLeftBtns = new Button[6];
        readonly Button[] processSwitchRightBtns = new Button[6];
        readonly Button[] processSpeedBtns = new Button[4];
        readonly List<Button> processStepDotBtns = new List<Button>(8);
        readonly Button[] cardTabChips = new Button[3];
        readonly Button[] stepDots = new Button[5];
        readonly Button[] angleChips = new Button[6];
        readonly List<(Button btn, string partId)> partJumpBtns = new List<(Button, string)>();

        Button btnOpenProcesses, btnTabCard2Proc, btnTabCard2Preset;
        Button btnAnno, btnExplode, btnXray, btnParticles, btnLamp, btnCycle, btnToggleAns, btnQuizPoint3D, btnAutoRotateTool, btnFocusPart, btnJumpHistology, btnToggleLeft, btnToggleRight;
        Button btnProcPlay, btnProcAutoPlay;
        RectTransform leftSidebar, rightSidebar, cardPanel, quizPanel, processPanel, lessonBar, partSelectorRow, floatingTools;
        RectTransform leftCard2ProcGroup, leftCard2PresetGroup, procDotsContainer;
        Image procFlowBadgeImg, procCountdownFill;
        Text txtHudBadge, txtMetrics, txtDiagnosis, txtPresetConclusion, txtCardGroupBadge, txtCardTitle, txtCardBody, txtLessonText, txtQuizStatus;
        Text txtQuizQuestion, txtQuizFeedback;
        Text txtProcHeader, txtProcCounter, txtProcFlowBadge, txtProcStepTitle, txtProcWhat, txtProcWhy, txtProcRemember, txtProcSummary;
        RectTransform procSummaryBox;
        Image barPhotoFill, barTranspFill, barStomaFill, barAbaFill;
        Text valPhotoTxt, valTranspTxt, valStomaTxt, valAbaTxt;
        readonly Button[] quizOptBtns = new Button[4];
        readonly Text[] quizOptTexts = new Text[4];
        bool leftCollapsed, rightCollapsed;

        // Bảng màu Dark Studio đồng bộ 100% với Phòng Giải phẫu (biology-lab-3d.html)
        static readonly Color StageBg = new Color(0.039f, 0.039f, 0.039f, 1.0f);        // #0A0A0A
        static readonly Color HeaderBg = new Color(0.043f, 0.045f, 0.050f, 0.98f);      // #0B0B0D
        static readonly Color SidebarBg = new Color(0.067f, 0.067f, 0.071f, 0.96f);     // #111112
        static readonly Color CardBg = new Color(0.086f, 0.086f, 0.092f, 0.98f);        // #161617
        static readonly Color CardInsetBg = new Color(0.060f, 0.065f, 0.078f, 0.98f);   // #0F1114
        static readonly Color PillBg = new Color(0.075f, 0.077f, 0.085f, 0.94f);        // #131416
        static readonly Color BtnNormal = new Color(0.094f, 0.096f, 0.104f, 0.98f);     // #18181A
        static readonly Color BtnSelectedDark = new Color(0.155f, 0.160f, 0.175f, 1.0f);// #27292D
        static readonly Color BtnPrimary = new Color(0.14f, 0.36f, 0.72f, 0.98f);       // #245CB8
        static readonly Color BtnActive = new Color(0.06f, 0.50f, 0.36f, 0.98f);        // #10805C
        static readonly Color BtnWarm = new Color(0.68f, 0.38f, 0.10f, 0.98f);          // #AD611A
        static readonly Color BorderLine = new Color(0.155f, 0.158f, 0.168f, 1.0f);     // #27282B
        static readonly Color BorderPill = new Color(0.24f, 0.25f, 0.28f, 0.95f);       // #3D3F47
        static readonly Color BorderBlue = new Color(0.231f, 0.510f, 0.965f, 0.95f);    // #3B82F6
        static readonly Color BorderGreen = new Color(0.063f, 0.725f, 0.506f, 0.95f);   // #10B981
        static readonly Color BorderAmber = new Color(0.96f, 0.62f, 0.08f, 0.95f);      // #F59E0B
        static readonly Color TextMain = new Color(0.93f, 0.93f, 0.94f, 1.0f);          // #EDEDED
        static readonly Color TextSub = new Color(0.63f, 0.64f, 0.66f, 1.0f);           // #A1A1A6
        static readonly Color TextMuted = new Color(0.45f, 0.46f, 0.49f, 1.0f);         // #73757D

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
                tempEventSystem = new GameObject("ThucVat_TempEventSystem");
                tempEventSystem.AddComponent<EventSystem>();
                tempEventSystem.AddComponent<InputSystemUIInputModule>();
            }

            var stageGo = new GameObject("SanKhauThucVat_3D");
            stageGo.transform.SetParent(transform, false);

            var camGo = new GameObject("CameraSanKhauThucVat") { layer = CayHaiLaMam3D.StageLayer };
            camGo.transform.SetParent(transform, false);
            StageCamera = camGo.AddComponent<Camera>();
            StageCamera.clearFlags = CameraClearFlags.SolidColor;
            StageCamera.backgroundColor = StageBg;
            StageCamera.cullingMask = 1 << CayHaiLaMam3D.StageLayer;
            StageCamera.nearClipPlane = 0.02f;
            StageCamera.farClipPlane = 45f;
            StageCamera.depth = 60f;
            StageCamera.fieldOfView = 38f;

            OrbitCam = camGo.AddComponent<CameraQuyDao>();
            OrbitCam.TargetCamera = StageCamera;
            OrbitCam.PickLayerMask = 1 << CayHaiLaMam3D.StageLayer;
            OrbitCam.OnEscapePressed += HandleEscapeKey;
            OrbitCam.OnPartClicked += OnPartClicked;
            OrbitCam.OnPartDoubleClicked += OnPartDoubleClicked;

            Cay3D = stageGo.AddComponent<CayHaiLaMam3D>();
            Cay3D.Initialize(StageCamera.transform);

            MuiTen3D = stageGo.AddComponent<ThucVatMuiTen3D>();
            MuiTen3D.Initialize(Cay3D, StageCamera.transform);
            MuiTen3D.OnStepStarted += OnProcessStepStarted;
            MuiTen3D.OnFinished += OnProcessFinished;
            MuiTen3D.OnStateChanged += RefreshProcessControlsUi;

            BuildUi();
            Build3DPinBadges();
            BuildActiveStep3DBadge();
            ChonKhungNhin(KhungNhinThucVat.TongThe, instantCamera: true);
            ChonCheDo(CheDoThucVat.BaiGiang);
            ChonBoPhan("than_cay");
            RecomputeAndApply(0f);
        }

        void HandleEscapeKey()
        {
            if (MuiTen3D != null && MuiTen3D.DangMo)
            {
                DongQuaTrinh();
                return;
            }
            ThucVatMoDunImpl.Dong();
        }

        public void OnReopen()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void OnCloseClean()
        {
            DesktopViewer.UiCapture = Mathf.Max(0, DesktopViewer.UiCapture - 1);
            if (tempEventSystem != null) Destroy(tempEventSystem);
            if (prevMainCam != null) prevMainCam.enabled = prevMainCamEnabled;
            Cursor.lockState = prevCursorLock;
            Cursor.visible = prevCursorVisible;
        }

        public void DatDieuKien(DieuKienMoiTruong newEnv)
        {
            dk = newEnv;
            SyncSlidersFromEnv();
            RecomputeAndApply(0f);
        }

        public void SetAnhSang(float lux) { dk.AnhSangLux = lux; SyncSlidersFromEnv(); RecomputeAndApply(0f); }
        public void SetCo2(float ppm) { dk.Co2Ppm = ppm; SyncSlidersFromEnv(); RecomputeAndApply(0f); }
        public void SetNhietDo(float c) { dk.NhietDoC = c; SyncSlidersFromEnv(); RecomputeAndApply(0f); }
        public void SetDoAmKhongKhi(float rh) { dk.DoAmKhongKhiPct = rh; SyncSlidersFromEnv(); RecomputeAndApply(0f); }
        public void SetDoAmDat(float soil) { dk.DoAmDatPct = soil; SyncSlidersFromEnv(); RecomputeAndApply(0f); }
        public void SetGio(float wind01) { dk.Gio01 = wind01; SyncSlidersFromEnv(); RecomputeAndApply(0f); }
        public void SetGocDen(float deg) { dk.GocDenHuongSangDo = deg; SyncSlidersFromEnv(); RecomputeAndApply(0f); }
        public void SetSoGioHuongSang(float hours) { dk.SoGioHuongSang = hours; RecomputeAndApply(0f); }

        static KhungNhinThucVat ParseView(string id) => id switch
        {
            "lat_cat_re" => KhungNhinThucVat.LatCatRe,
            "lat_cat_than" => KhungNhinThucVat.LatCatThan,
            "lat_cat_la" => KhungNhinThucVat.LatCatLa,
            _ => KhungNhinThucVat.TongThe
        };

        // ---------------------------------------------------------------------
        // HỆ THỐNG CHẠY 6 QUÁ TRÌNH SINH LÝ TỪNG BƯỚC CÓ MŨI TÊN 3D
        // ---------------------------------------------------------------------
        public void BatDauQuaTrinh(string ma, int buocBatDau = 0)
        {
            var qt = ThucVatQuaTrinhData.TimTheoMa(ma) ?? ThucVatQuaTrinhData.DanhSach[0];
            if (qt == null || MuiTen3D == null) return;

            if (!MuiTen3D.DangMo && Cay3D != null)
            {
                // Giống Phòng Giải phẫu: tạm tắt dòng hạt nền khi đang học theo mũi tên từng bước cho gọn mắt
                particlesBeforeProcess = Cay3D.HienHatDong;
                Cay3D.SetHienHatDong(false);
                UpdateParticlesButtonState();
            }

            if (rightCollapsed) ToggleRightSidebar();
            if (cardPanel != null) cardPanel.gameObject.SetActive(false);
            if (quizPanel != null) quizPanel.gameObject.SetActive(false);
            if (lessonBar != null) lessonBar.gameObject.SetActive(false);
            if (processPanel != null)
            {
                processPanel.gameObject.SetActive(true);
                UiMotion.Popup(this, processPanel, null, 0.16f);
            }

            RebuildProcessStepDots(qt);
            HighlightActiveProcessButtons(qt.Ma);
            if (btnOpenProcesses != null) SetAnatomyBtnState(btnOpenProcesses, true, activeFill: BtnPrimary, activeRing: BorderBlue);

            MuiTen3D.StartProcess(qt, buocBatDau);
        }

        public void DongQuaTrinh()
        {
            if (MuiTen3D == null) return;
            MuiTen3D.CloseProcess();

            if (particlesBeforeProcess.HasValue && Cay3D != null)
            {
                Cay3D.SetHienHatDong(particlesBeforeProcess.Value);
                particlesBeforeProcess = null;
                UpdateParticlesButtonState();
            }

            if (processPanel != null) processPanel.gameObject.SetActive(false);
            if (cardPanel != null) cardPanel.gameObject.SetActive(cheDo != CheDoThucVat.CauHoi);
            if (quizPanel != null) quizPanel.gameObject.SetActive(cheDo == CheDoThucVat.CauHoi);
            if (lessonBar != null) lessonBar.gameObject.SetActive(cheDo == CheDoThucVat.BaiGiang);
            if (activeStepBadgeRt != null) activeStepBadgeRt.gameObject.SetActive(false);

            HighlightActiveProcessButtons(null);
            if (btnOpenProcesses != null) SetAnatomyBtnState(btnOpenProcesses, false);
        }

        void OnProcessStepStarted(int idx, BuocQuaTrinhThucVat step)
        {
            var qt = MuiTen3D?.QuaTrinhHienTai;
            if (qt == null || step == null) return;

            // 1. Tự chuyển đúng khung nhìn giải phẫu + môi trường sinh lý + làm nổi bộ phận + căn camera tối ưu
            var targetView = ParseView(step.KhungNhin);
            if (Cay3D != null && Cay3D.KhungHienTai != targetView)
                ChonKhungNhin(targetView, instantCamera: false);

            if (step.CoDatMoiTruong)
            {
                autoDayCycle = false;
                DatDieuKien(step.MoiTruongGoiY);
            }

            if (!string.IsNullOrEmpty(step.BoPhanNoiBat))
                ChonBoPhan(step.BoPhanNoiBat);

            CanGocBuocHienTai();

            // 2. Cập nhật bảng Giải thích từng bước bên cột phải
            if (txtProcHeader != null) txtProcHeader.text = $"{qt.BieuTuong} {qt.Ten}";
            if (txtProcCounter != null) txtProcCounter.text = $"Bước {idx + 1}/{qt.CacBuoc.Length}";

            Color stepCol = ColorUtility.TryParseHtmlString(step.MauHex, out var c) ? c : BorderBlue;
            if (procFlowBadgeImg != null) procFlowBadgeImg.color = new Color(stepCol.r * 0.35f, stepCol.g * 0.35f, stepCol.b * 0.35f, 0.95f);
            if (txtProcFlowBadge != null)
            {
                txtProcFlowBadge.text = string.IsNullOrEmpty(step.NhanLuong) ? $"Bước {idx + 1}" : step.NhanLuong;
                txtProcFlowBadge.color = Color.Lerp(stepCol, Color.white, 0.35f);
            }

            if (txtProcStepTitle != null) txtProcStepTitle.text = $"{idx + 1}. {step.TieuDe}";
            if (txtProcWhat != null) txtProcWhat.text = step.DieuGiXayRa;
            if (txtProcWhy != null) txtProcWhy.text = $"<color=#60A5FA><b>VÌ SAO?</b></color>\n{step.ViSao}";
            if (txtProcRemember != null) txtProcRemember.text = $"<color=#34D399><b>● GHI NHỚ:</b></color> {step.GhiNho}";
            if (procSummaryBox != null) procSummaryBox.gameObject.SetActive(false);

            for (int k = 0; k < processStepDotBtns.Count; k++)
            {
                bool cur = k == idx;
                bool past = k < idx;
                SetAnatomyBtnState(processStepDotBtns[k], cur || past,
                    activeFill: cur ? BtnPrimary : new Color(0.11f, 0.16f, 0.24f, 0.95f),
                    activeRing: cur ? BorderBlue : BorderLine);
            }
        }

        void OnProcessFinished(QuaTrinhSinhLyThucVat qt)
        {
            if (procSummaryBox == null || txtProcSummary == null || qt == null) return;
            txtProcSummary.text = $"<color=#FBBF24><b>● TÓM TẮT QUÁ TRÌNH:</b></color>\n{qt.TomTat}";
            procSummaryBox.gameObject.SetActive(true);
        }

        void CanGocBuocHienTai()
        {
            if (MuiTen3D == null || !MuiTen3D.DangMo || Cay3D == null || OrbitCam == null) return;
            var step = MuiTen3D.QuaTrinhHienTai.CacBuoc[MuiTen3D.BuocHienTai];
            var view = ParseView(step.KhungNhin);
            Cay3D.GetDefaultCameraPose(view, out var defPivot, out float defYaw, out float defPitch, out float defDist);
            Vector3 pivot = defPivot + new Vector3(step.PivotOffX, step.PivotOffY, step.PivotOffZ);
            float yaw = step.CamDist > 0.05f ? step.CamYaw : defYaw;
            float pitch = step.CamDist > 0.05f ? step.CamPitch : defPitch;
            float dist = step.CamDist > 0.05f ? step.CamDist : defDist;
            OrbitCam.SetTarget(pivot, yaw, pitch, dist, instant: false);
        }

        void RefreshProcessControlsUi()
        {
            if (MuiTen3D == null) return;
            if (btnProcPlay != null)
            {
                var t = btnProcPlay.GetComponentInChildren<Text>();
                if (t != null)
                {
                    t.text = MuiTen3D.TrangThai == TrangThaiChayQuaTrinh.HoanThanh ? "↺ Lặp"
                           : MuiTen3D.TrangThai == TrangThaiChayQuaTrinh.DangChay ? "|| Dừng" : "▶ Phát";
                }
                SetAnatomyBtnState(btnProcPlay, MuiTen3D.TrangThai == TrangThaiChayQuaTrinh.DangChay, activeFill: BtnPrimary, activeRing: BorderBlue);
            }

            if (btnProcAutoPlay != null)
            {
                var t = btnProcAutoPlay.GetComponentInChildren<Text>();
                if (t != null) t.text = MuiTen3D.TuDongChay ? "Tự chạy: BẬT" : "Tự chạy: TẮT";
                SetAnatomyBtnState(btnProcAutoPlay, MuiTen3D.TuDongChay, activeFill: BtnSelectedDark, activeRing: BorderGreen);
            }

            float[] speeds = { 0.5f, 1f, 1.5f, 2f };
            for (int i = 0; i < processSpeedBtns.Length; i++)
            {
                if (processSpeedBtns[i] != null)
                    SetAnatomyBtnState(processSpeedBtns[i], Mathf.Abs(MuiTen3D.TocDo - speeds[i]) < 0.05f, activeFill: BtnSelectedDark, activeRing: BorderBlue);
            }
        }

        void HighlightActiveProcessButtons(string activeId)
        {
            var list = ThucVatQuaTrinhData.DanhSach;
            for (int i = 0; i < list.Length; i++)
            {
                bool on = !string.IsNullOrEmpty(activeId) && list[i].Ma == activeId;
                if (i < processQuickLeftBtns.Length && processQuickLeftBtns[i] != null)
                    SetAnatomyBtnState(processQuickLeftBtns[i], on, activeFill: BtnSelectedDark, activeRing: BorderBlue);
                if (i < processSwitchRightBtns.Length && processSwitchRightBtns[i] != null)
                    SetAnatomyBtnState(processSwitchRightBtns[i], on, activeFill: BtnSelectedDark, activeRing: BorderBlue);
            }
        }

        void RebuildProcessStepDots(QuaTrinhSinhLyThucVat qt)
        {
            if (procDotsContainer == null || qt == null) return;
            for (int i = procDotsContainer.childCount - 1; i >= 0; i--)
                Destroy(procDotsContainer.GetChild(i).gameObject);
            processStepDotBtns.Clear();

            int n = qt.CacBuoc.Length;
            float totalW = 280f;
            float gap = 5f;
            float btnW = (totalW - gap * (n - 1)) / Mathf.Max(1, n);
            for (int i = 0; i < n; i++)
            {
                int capI = i;
                var b = MakeAnatomyButton(procDotsContainer, $"DotBuoc_{i + 1}", $"{i + 1}",
                    new Vector2(i * (btnW + gap), 0f), new Vector2(btnW, 20f),
                    () => MuiTen3D?.GoToStep(capI), i == 0, 10, radius: 5f, activeFill: BtnPrimary, activeRing: BorderBlue);
                processStepDotBtns.Add(b);
            }
            SetLayerRecursive(procDotsContainer, 5);
        }

        void SetLeftCard2Mode(bool showPresets)
        {
            leftCard2ShowPresets = showPresets;
            if (leftCard2ProcGroup != null) leftCard2ProcGroup.gameObject.SetActive(!showPresets);
            if (leftCard2PresetGroup != null) leftCard2PresetGroup.gameObject.SetActive(showPresets);
            if (btnTabCard2Proc != null) SetAnatomyBtnState(btnTabCard2Proc, !showPresets, activeFill: BtnSelectedDark, activeRing: BorderBlue);
            if (btnTabCard2Preset != null) SetAnatomyBtnState(btnTabCard2Preset, showPresets, activeFill: BtnSelectedDark, activeRing: BorderGreen);
        }

        void UpdateParticlesButtonState()
        {
            if (btnParticles == null || Cay3D == null) return;
            bool on = Cay3D.HienHatDong;
            var lbl = btnParticles.GetComponentInChildren<Text>();
            if (lbl != null) lbl.text = on ? "Dòng hạt: BẬT" : "Dòng hạt: TẮT";
            SetAnatomyBtnState(btnParticles, on, activeFill: BtnSelectedDark, activeRing: BorderBlue);
        }

        public void ChonKhungNhin(KhungNhinThucVat view, bool instantCamera = false)
        {
            if (Cay3D == null) return;
            dangLayNetBoPhan = false;
            UpdateFocusPartButtonLabel();
            Cay3D.SetKhungNhin(view);
            Cay3D.GetDefaultCameraPose(view, out var pivot, out float yaw, out float pitch, out float dist);
            OrbitCam?.SetTarget(pivot, yaw, pitch, dist, instantCamera);
            for (int i = 0; i < viewChips.Length; i++)
                if (viewChips[i] != null) SetAnatomyBtnState(viewChips[i], i == (int)view, activeFill: BtnSelectedDark, activeRing: BorderBlue);

            RebuildPartSelectorRow(view);

            string targetGroup = view switch
            {
                KhungNhinThucVat.LatCatRe => "lat_cat_re",
                KhungNhinThucVat.LatCatThan => "lat_cat_than",
                KhungNhinThucVat.LatCatLa => "lat_cat_la",
                _ => "tong_the"
            };
            var curInfo = ThucVatMoPhong.TimBoPhan(Cay3D.BoPhanDangChon);
            if (curInfo == null || curInfo.Nhom != targetGroup)
            {
                string defaultPart = view switch
                {
                    KhungNhinThucVat.LatCatRe => "re_dai_caspari",
                    KhungNhinThucVat.LatCatThan => "than_tang_sinh_mach",
                    KhungNhinThucVat.LatCatLa => "la_khi_khong",
                    _ => "than_cay"
                };
                ChonBoPhan(defaultPart);
            }
            UpdateJumpHistologyButton();
            Update3DPinBadges();
        }

        public void ChonCheDo(CheDoThucVat mode)
        {
            if (MuiTen3D != null && MuiTen3D.DangMo) DongQuaTrinh();
            cheDo = mode;
            for (int i = 0; i < modeChips.Length; i++)
                if (modeChips[i] != null) SetAnatomyBtnState(modeChips[i], i == (int)mode, activeFill: BtnSelectedDark, activeRing: BorderGreen);

            if (lessonBar != null)
            {
                bool showLesson = mode == CheDoThucVat.BaiGiang;
                lessonBar.gameObject.SetActive(showLesson);
                if (showLesson) UiMotion.Popup(this, lessonBar, null, 0.18f);
            }
            if (cardPanel != null) cardPanel.gameObject.SetActive(mode != CheDoThucVat.CauHoi);
            if (quizPanel != null) quizPanel.gameObject.SetActive(mode == CheDoThucVat.CauHoi);

            if (mode == CheDoThucVat.BaiGiang)
                ApDungBuocBaiGiang(buocBaiGiang);
            else if (mode == CheDoThucVat.CauHoi)
            {
                cheDoChiTren3D = false;
                HienCauHoi(cauHoiIdx);
            }
        }

        public void ChonBoPhan(string partId)
        {
            var info = ThucVatMoPhong.TimBoPhan(partId);
            if (info == null) return;
            Cay3D?.SelectPart(partId);
            hienDapAnNhanh = false;

            for (int i = 0; i < partJumpBtns.Count; i++)
                SetAnatomyBtnState(partJumpBtns[i].btn, partJumpBtns[i].partId == partId, activeFill: BtnSelectedDark, activeRing: BorderBlue);

            for (int i = 0; i < pinBadges.Count; i++)
            {
                bool sel = pinBadges[i].pin.PartId == partId;
                if (pinBadges[i].pillRing != null)
                    pinBadges[i].pillRing.color = sel ? BorderBlue : BorderPill;
                if (pinBadges[i].pillFace != null)
                    pinBadges[i].pillFace.color = sel ? new Color(0.11f, 0.15f, 0.22f, 0.96f) : PillBg;
            }

            if (dangLayNetBoPhan && Cay3D != null && Cay3D.TryGetPartBounds(partId, out var b))
                OrbitCam?.FocusOn(b, 2.15f, instant: false);

            UpdateJumpHistologyButton();
            RefreshKnowledgeCard();
        }

        void NhaySangLatCatTuongUng()
        {
            if (Cay3D == null) return;
            if (Cay3D.KhungHienTai != KhungNhinThucVat.TongThe)
            {
                ChonKhungNhin(KhungNhinThucVat.TongThe);
                return;
            }
            KhungNhinThucVat target = Cay3D.BoPhanDangChon switch
            {
                "re_chop_va_long_hut" => KhungNhinThucVat.LatCatRe,
                "la_cay" => KhungNhinThucVat.LatCatLa,
                _ => KhungNhinThucVat.LatCatThan
            };
            ChonKhungNhin(target);
        }

        void UpdateJumpHistologyButton()
        {
            if (btnJumpHistology == null || Cay3D == null) return;
            var txt = btnJumpHistology.GetComponentInChildren<Text>();
            if (txt == null) return;

            if (Cay3D.KhungHienTai != KhungNhinThucVat.TongThe)
            {
                txt.text = "↩ Quay lại mô hình Toàn cây (đại thể)";
                SetAnatomyBtnState(btnJumpHistology, true, activeFill: BtnSelectedDark, activeRing: BorderBlue);
            }
            else
            {
                txt.text = Cay3D.BoPhanDangChon switch
                {
                    "re_chop_va_long_hut" => "▸ Phóng đại Lát cắt vi thể Rễ →",
                    "la_cay" => "▸ Phóng đại Lát cắt vi thể Lá →",
                    _ => "▸ Phóng đại Lát cắt vi thể Thân →"
                };
                SetAnatomyBtnState(btnJumpHistology, true, activeFill: new Color(0.08f, 0.28f, 0.22f, 0.98f), activeRing: BorderGreen);
            }
        }

        void BatTatLayNetBoPhan360()
        {
            if (Cay3D == null || OrbitCam == null) return;
            dangLayNetBoPhan = !dangLayNetBoPhan;
            if (dangLayNetBoPhan && Cay3D.TryGetPartBounds(Cay3D.BoPhanDangChon, out var b))
                OrbitCam.FocusOn(b, 2.0f, instant: false);
            else
            {
                Cay3D.GetDefaultCameraPose(Cay3D.KhungHienTai, out var pivot, out float yaw, out float pitch, out float dist);
                OrbitCam.SetTarget(pivot, yaw, pitch, dist, instant: false);
            }
            UpdateFocusPartButtonLabel();
        }

        void UpdateFocusPartButtonLabel()
        {
            if (btnFocusPart == null) return;
            var txt = btnFocusPart.GetComponentInChildren<Text>();
            if (txt != null)
                txt.text = dangLayNetBoPhan ? "↩ Về khung nhìn toàn cảnh" : "◎ Tập trung & Xoay 360° bộ phận này";
            SetAnatomyBtnState(btnFocusPart, dangLayNetBoPhan, activeFill: BtnWarm, activeRing: BorderBlue);
        }

        void BatTatTuXoay360()
        {
            if (OrbitCam == null) return;
            OrbitCam.AutoRotate = !OrbitCam.AutoRotate;
            bool on = OrbitCam.AutoRotate;
            if (btnAutoRotateTool != null)
                SetAnatomyBtnState(btnAutoRotateTool, on, activeFill: BtnSelectedDark, activeRing: BorderBlue);
            if (angleChips[5] != null)
                SetAnatomyBtnState(angleChips[5], on, activeFill: BtnSelectedDark, activeRing: BorderBlue);
        }

        public void ApDungKichBan(int idx)
        {
            if (MuiTen3D != null && MuiTen3D.DangMo) DongQuaTrinh();
            var list = ThucVatMoPhong.DanhSachKichBan;
            if (idx < 0 || idx >= list.Length) return;
            kichBanDangChon = idx;
            var kb = list[idx];
            for (int i = 0; i < presetChips.Length; i++)
                if (presetChips[i] != null) SetAnatomyBtnState(presetChips[i], i == idx, activeFill: BtnSelectedDark, activeRing: BorderBlue);

            ChonKhungNhin(ParseView(kb.KhungNhinGoiY));
            ChonBoPhan(kb.BoPhanGoiY);
            DatDieuKien(kb.DieuKien);
            if (txtPresetConclusion != null)
                txtPresetConclusion.text = $"<color=#60A5FA><b>{kb.Ten}:</b></color> {kb.KetLuanKhoaHoc}";
        }

        void ChonTabKienThuc(int tabIdx)
        {
            tabKienThuc = Mathf.Clamp(tabIdx, 0, 2);
            for (int i = 0; i < cardTabChips.Length; i++)
                if (cardTabChips[i] != null) SetAnatomyBtnState(cardTabChips[i], i == tabKienThuc, activeFill: BtnSelectedDark, activeRing: BorderBlue);
            RefreshKnowledgeCard();
        }

        void RefreshKnowledgeCard()
        {
            if (Cay3D == null) return;
            var info = ThucVatMoPhong.TimBoPhan(Cay3D.BoPhanDangChon);
            if (info == null) return;

            if (txtCardGroupBadge != null)
            {
                txtCardGroupBadge.text = info.Nhom switch
                {
                    "lat_cat_re" => "GIẢI PHẪU LÁT CẮT NGANG RỄ · SINH HỌC 11",
                    "lat_cat_than" => "GIẢI PHẪU LÁT CẮT NGANG THÂN · SINH HỌC 11",
                    "lat_cat_la" => "GIẢI PHẪU LÁT CẮT NGANG LÁ · SINH HỌC 11",
                    _ => "CƠ QUAN CÂY HAI LÁ MẦM (PHASEOLUS VULGARIS)"
                };
            }
            if (txtCardTitle != null) txtCardTitle.text = info.Ten;
            if (btnToggleAns != null) btnToggleAns.gameObject.SetActive(tabKienThuc == 2);

            if (txtCardBody == null) return;
            if (tabKienThuc == 0)
            {
                txtCardBody.text =
                    $"<color=#60A5FA><b>CẤU TẠO GIẢI PHẪU</b></color>\n{info.CauTao}\n\n" +
                    $"<color=#34D399><b>CHỨC NĂNG & CƠ CHẾ SINH LÝ</b></color>\n{info.ChucNang}\n\n" +
                    $"<color=#73757D><i>{info.Nguon}</i></color>";
            }
            else if (tabKienThuc == 1)
            {
                txtCardBody.text =
                    $"<color=#FBBF24><b>SO SÁNH HAI LÁ MẦM & MỘT LÁ MẦM</b></color>\n{info.SoSanhMotLaMam}\n\n" +
                    $"<color=#34D399><b>ỨNG DỤNG THỰC TIỄN NÔNG NGHIỆP</b></color>\n{info.UngDungThucTe}";
            }
            else
            {
                string ansBlock = hienDapAnNhanh
                    ? $"\n\n<color=#34D399><b>✓ ĐÁP ÁN CHUẨN SGK:</b></color>\n{info.DapAn}"
                    : "\n\n<color=#73757D><i>(Bấm nút bên dưới để mở gợi ý đáp án chuẩn SGK)</i></color>";
                txtCardBody.text =
                    $"<color=#60A5FA><b>CÂU HỎI THẢO LUẬN TRÊN LỚP</b></color>\n<b>{info.CauHoiNhanh}</b>" + ansBlock;
                var lbl = btnToggleAns != null ? btnToggleAns.GetComponentInChildren<Text>() : null;
                if (lbl != null) lbl.text = hienDapAnNhanh ? "Ẩn đáp án" : "Hiện đáp án chuẩn SGK";
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;

            // Phím tắt Space / Mũi tên khi đang mở Quá trình sinh lý 3D (đồng bộ Phòng Giải phẫu)
            if (MuiTen3D != null && MuiTen3D.DangMo)
            {
                var kb = Keyboard.current;
                if (kb != null)
                {
                    if (kb.spaceKey.wasPressedThisFrame) MuiTen3D.TogglePlayPause();
                    else if (kb.rightBracketKey.wasPressedThisFrame) MuiTen3D.NextStep();
                    else if (kb.leftBracketKey.wasPressedThisFrame) MuiTen3D.PrevStep();
                }

                if (procCountdownFill != null)
                {
                    bool showBar = MuiTen3D.DangChoDoc;
                    procCountdownFill.transform.parent.gameObject.SetActive(showBar);
                    if (showBar) SetBarFill(procCountdownFill, MuiTen3D.TienDoDoc);
                }
            }

            if (autoDayCycle)
            {
                simulatedHour = (simulatedHour + (dt * dayTimeScale) / 240f) % 24f;
                float sunFactor = Mathf.Clamp01(Mathf.Sin((simulatedHour - 6f) / 12f * Mathf.PI));
                dk.AnhSangLux = sunFactor * 42000.0;
                dk.NhietDoC = 20.0 + sunFactor * 10.0;
                SyncSlidersFromEnv();
            }

            if (Math.Abs(dk.GocDenHuongSangDo) > 1.0 && dk.AnhSangLux >= 150.0)
                dk.SoGioHuongSang = Math.Min(48.0, dk.SoGioHuongSang + (dt * dayTimeScale) / 60.0);

            RecomputeAndApply(dt);
        }

        void LateUpdate()
        {
            Update3DPinBadges();
            UpdateActiveStep3DBadge();
        }

        void RecomputeAndApply(float dt)
        {
            trangThai = ThucVatMoPhong.TinhToan(in dk);
            Cay3D?.ApplySimulation(in dk, in trangThai, dt, dayTimeScale);
            UpdateMetricsUi();
        }

        void UpdateMetricsUi()
        {
            float qh01 = Mathf.Clamp01((float)(trangThai.CuongDoQuangHopRong / 36.0));
            float th01 = Mathf.Clamp01((float)(trangThai.TocDoThoatHoiNuocMmPhut / 3.5));
            float kk01 = Mathf.Clamp01((float)trangThai.DoMoKhiKhong01);
            float aba01 = Mathf.Clamp01((float)trangThai.NongDoAba01);

            SetBarFill(barPhotoFill, qh01);
            SetBarFill(barTranspFill, th01);
            SetBarFill(barStomaFill, kk01);
            SetBarFill(barAbaFill, aba01);

            if (valPhotoTxt != null) valPhotoTxt.text = $"{trangThai.CuongDoQuangHopRong:+0.0;-0.0;0.0}";
            if (valTranspTxt != null) valTranspTxt.text = $"{trangThai.TocDoThoatHoiNuocMmPhut:0.00} mm/p";
            if (valStomaTxt != null) valStomaTxt.text = $"{trangThai.DoMoKhiKhong01 * 100.0:0}%";
            if (valAbaTxt != null) valAbaTxt.text = $"{trangThai.NongDoAba01 * 100.0:0}%";

            if (txtHudBadge != null)
            {
                string dot = trangThai.BanDem ? "<color=#F59E0B>● Đêm (Hô hấp)</color>" : "<color=#10B981>● Quang hợp C3</color>";
                txtHudBadge.text = $"{dot}   <b>{trangThai.CuongDoQuangHopRong:+0.0;-0.0;0.0}</b>   |   Khí khổng <b>{trangThai.DoMoKhiKhong01 * 100.0:0}%</b>";
            }

            if (txtMetrics != null)
            {
                string pha = trangThai.BanDem
                    ? "<color=#F59E0B><b>Đêm (Hô hấp)</b></color>"
                    : "<color=#10B981><b>Ngày (Quang hợp C3)</b></color>";
                txtMetrics.text =
                    $"{pha} · Tua <b>×{dayTimeScale:0}</b> · QH thô <b>{trangThai.CuongDoQuangHopTho:0.0}</b> | Hô hấp <b>{trangThai.CuongDoHoHap:0.0}</b>\n" +
                    $"Hướng sáng: <b>{trangThai.GocUonThanDo:0.0}°</b> (Auxin tối <b>{trangThai.TiLeAuxinPhiaToi * 100.0:0}%</b> / sáng <b>{trangThai.TiLeAuxinPhiaSang * 100.0:0}%</b>)";
            }

            if (txtDiagnosis != null)
            {
                string diag;
                string eq;
                if (trangThai.BanDem)
                {
                    diag = "<color=#F59E0B><b>⚠ Hô hấp tối:</b> Không có ánh sáng, khí khổng khép.</color>";
                    eq = "<color=#A1A1A6>C₆H₁₂O₆ + 6O₂ → 6CO₂ + 6H₂O + ATP</color>";
                }
                else if (trangThai.NongDoAba01 > 0.45)
                {
                    diag = "<color=#F87171><b>⚠ Giới hạn bởi Khô hạn:</b> ABA đóng khí khổng.</color>";
                    eq = "<color=#A1A1A6>ABA ↑ → thoát K⁺ → xẹp tế bào hạt đậu</color>";
                }
                else if (dk.AnhSangLux <= ThucVatMoPhong.DiemBuAnhSangLux)
                {
                    diag = "<color=#FBBF24><b>⚠ Dưới điểm bù ánh sáng:</b> QH ≤ Hô hấp.</color>";
                    eq = "<color=#A1A1A6>6CO₂ + 6H₂O ⇄ C₆H₁₂O₆ + 6O₂ (cân bằng)</color>";
                }
                else if (dk.Co2Ppm < 220.0)
                {
                    diag = "<color=#FBBF24><b>⚠ Giới hạn CO₂:</b> Thiếu cơ chất chu trình Calvin.</color>";
                    eq = "<color=#A1A1A6>6CO₂ + 6H₂O —(PAR)→ C₆H₁₂O₆ + 6O₂</color>";
                }
                else if (dk.NhietDoC > 38.0 || dk.NhietDoC < 14.0)
                {
                    diag = "<color=#F87171><b>⚠ Stress nhiệt độ:</b> Giảm hoạt tính enzym Rubisco.</color>";
                    eq = "<color=#A1A1A6>Nhiệt tối ưu C3: 24–30 °C</color>";
                }
                else
                {
                    diag = "<color=#34D399><b>✓ Sinh lý C3 tối ưu:</b> Quang hợp & dẫn mạch mạnh.</color>";
                    eq = "<color=#60A5FA>6CO₂ + 6H₂O —(PAR, Diệp lục)→ C₆H₁₂O₆ + 6O₂</color>";
                }
                txtDiagnosis.text = diag + "\n" + eq;
            }
        }

        static void SetBarFill(Image img, float t01)
        {
            if (img == null) return;
            var rt = img.rectTransform;
            rt.anchorMax = new Vector2(Mathf.Clamp(t01, 0.02f, 1f), 1f);
        }

        void SyncSlidersFromEnv()
        {
            suppressSliderCallbacks = true;
            if (slLight != null) slLight.SetValueWithoutNotify((float)dk.AnhSangLux);
            if (slCo2 != null) slCo2.SetValueWithoutNotify((float)dk.Co2Ppm);
            if (slTemp != null) slTemp.SetValueWithoutNotify((float)dk.NhietDoC);
            if (slRh != null) slRh.SetValueWithoutNotify((float)dk.DoAmKhongKhiPct);
            if (slSoil != null) slSoil.SetValueWithoutNotify((float)dk.DoAmDatPct);
            if (slWind != null) slWind.SetValueWithoutNotify((float)dk.Gio01 * 100f);
            if (slLampAngle != null) slLampAngle.SetValueWithoutNotify((float)dk.GocDenHuongSangDo);
            for (int i = 0; i < syncSliderLabels.Count; i++) syncSliderLabels[i]?.Invoke();
            suppressSliderCallbacks = false;
        }

        void OnPartClicked(Collider col)
        {
            if (col == null) return;
            var tag = col.GetComponent<ThucVatPartTag>() ?? col.GetComponentInParent<ThucVatPartTag>();
            if (tag == null || string.IsNullOrEmpty(tag.PartId)) return;

            if (cheDo == CheDoThucVat.CauHoi && cheDoChiTren3D)
            {
                ChamDiemChiTren3D(tag.PartId);
                return;
            }
            ChonBoPhan(tag.PartId);
        }

        void OnPartDoubleClicked(Collider col)
        {
            if (col == null) return;
            var tag = col.GetComponent<ThucVatPartTag>() ?? col.GetComponentInParent<ThucVatPartTag>();
            if (tag != null && !string.IsNullOrEmpty(tag.PartId))
            {
                dangLayNetBoPhan = true;
                ChonBoPhan(tag.PartId);
                UpdateFocusPartButtonLabel();
            }
        }

        void ApDungBuocBaiGiang(int idx)
        {
            var list = ThucVatMoPhong.DanhSachBaiGiang;
            buocBaiGiang = (idx % list.Length + list.Length) % list.Length;
            var b = list[buocBaiGiang];
            ChonKhungNhin(ParseView(b.KhungNhin));
            if (!string.IsNullOrEmpty(b.BoPhanNoiBat))
                ChonBoPhan(b.BoPhanNoiBat);
            if (txtLessonText != null)
                txtLessonText.text = $"<color=#60A5FA><b>{b.TieuDe}:</b></color> {b.NoiDung}";

            for (int i = 0; i < stepDots.Length; i++)
                if (stepDots[i] != null) SetAnatomyBtnState(stepDots[i], i == buocBaiGiang, activeFill: BtnSelectedDark, activeRing: BorderBlue);
        }

        void BatTatCheDoChi3D()
        {
            cheDoChiTren3D = !cheDoChiTren3D;
            SetAnatomyBtnState(btnQuizPoint3D, cheDoChiTren3D, activeFill: BtnWarm, activeRing: BorderBlue);
            var lbl = btnQuizPoint3D != null ? btnQuizPoint3D.GetComponentInChildren<Text>() : null;
            if (lbl != null)
                lbl.text = cheDoChiTren3D ? "Đang bật: Chỉ trên 3D (Về Trắc nghiệm)" : "Thực hành: Chỉ trực tiếp bộ phận trên 3D";

            for (int i = 0; i < 4; i++)
                if (quizOptBtns[i] != null) quizOptBtns[i].gameObject.SetActive(!cheDoChiTren3D);

            if (cheDoChiTren3D) HienBaiTapChi3D(chi3DIdx);
            else HienCauHoi(cauHoiIdx);
        }

        void HienBaiTapChi3D(int idx)
        {
            chi3DIdx = (idx % BaiTapChi3D.Length + BaiTapChi3D.Length) % BaiTapChi3D.Length;
            var task = BaiTapChi3D[chi3DIdx];
            ChonKhungNhin(task.view);
            if (txtQuizQuestion != null)
                txtQuizQuestion.text = $"<color=#FBBF24><b>[Chỉ điểm 3D {chi3DIdx + 1}/{BaiTapChi3D.Length}]</b></color>\n{task.prompt}";
            if (txtQuizFeedback != null)
                txtQuizFeedback.text = "<color=#A1A1A6><i>Gợi ý: Kéo chuột/Trackpad xoay 360° mô hình ở giữa màn hình và nhấp trực tiếp vào bộ phận được yêu cầu.</i></color>";
        }

        void ChamDiemChiTren3D(string clickedPartId)
        {
            var task = BaiTapChi3D[chi3DIdx];
            cauHoiDaTraLoi++;
            bool ok = string.Equals(clickedPartId, task.partId, StringComparison.OrdinalIgnoreCase);
            if (ok) cauHoiDung++;
            Cay3D?.SelectPart(clickedPartId);
            var clickedInfo = ThucVatMoPhong.TimBoPhan(clickedPartId);
            var targetInfo = ThucVatMoPhong.TimBoPhan(task.partId);

            if (txtQuizFeedback != null)
            {
                txtQuizFeedback.text = ok
                    ? $"<color=#34D399><b>✓ Chính xác! Bạn đã chỉ đúng: {targetInfo?.Ten}</b></color>\n{targetInfo?.ChucNang}"
                    : $"<color=#FBBF24><b>✗ Bạn vừa bấm vào: {clickedInfo?.Ten ?? clickedPartId}</b></color>\nHãy tìm: <b>{targetInfo?.Ten}</b> ({targetInfo?.CauTao})";
            }
            if (txtQuizStatus != null)
                txtQuizStatus.text = $"Điểm: <b>{cauHoiDung}/{cauHoiDaTraLoi}</b>";
        }

        void HienCauHoi(int idx)
        {
            if (cheDoChiTren3D) { HienBaiTapChi3D(idx); return; }
            var qs = ThucVatMoPhong.DanhSachCauHoi;
            cauHoiIdx = (idx % qs.Length + qs.Length) % qs.Length;
            var q = qs[cauHoiIdx];
            if (txtQuizQuestion != null) txtQuizQuestion.text = $"<b>Câu {cauHoiIdx + 1}/{qs.Length}:</b> {q.Cau}";
            if (txtQuizFeedback != null) txtQuizFeedback.text = "";
            for (int i = 0; i < 4; i++)
            {
                if (quizOptTexts[i] != null) quizOptTexts[i].text = (char)('A' + i) + ". " + q.LuaChon[i];
                if (quizOptBtns[i] != null) SetAnatomyBtnState(quizOptBtns[i], false);
            }
            if (txtQuizStatus != null) txtQuizStatus.text = $"Điểm: <b>{cauHoiDung}/{cauHoiDaTraLoi}</b>";
        }

        void TraLoiCauHoi(int choice)
        {
            var q = ThucVatMoPhong.DanhSachCauHoi[cauHoiIdx];
            cauHoiDaTraLoi++;
            bool ok = choice == q.DapAnDung;
            if (ok) cauHoiDung++;
            for (int i = 0; i < 4; i++)
            {
                if (quizOptBtns[i] == null) continue;
                if (i == q.DapAnDung) SetAnatomyBtnState(quizOptBtns[i], true, activeFill: BtnActive, activeRing: BorderGreen);
                else if (i == choice && !ok) SetAnatomyBtnState(quizOptBtns[i], true, activeFill: new Color(0.55f, 0.16f, 0.18f, 0.96f), activeRing: new Color(0.94f, 0.27f, 0.27f));
                else SetAnatomyBtnState(quizOptBtns[i], false);
            }
            if (txtQuizFeedback != null)
            {
                string col = ok ? "#34D399" : "#F87171";
                string head = ok ? "✓ Chính xác!" : $"✗ Chưa đúng — đáp án đúng là {(char)('A' + q.DapAnDung)}.";
                txtQuizFeedback.text = $"<color={col}><b>{head}</b></color>\n{q.GiaiThich}";
            }
            if (txtQuizStatus != null) txtQuizStatus.text = $"Điểm: <b>{cauHoiDung}/{cauHoiDaTraLoi}</b>";
        }

        void ToggleLeftSidebar()
        {
            leftCollapsed = !leftCollapsed;
            if (leftSidebar != null) leftSidebar.gameObject.SetActive(!leftCollapsed);
            if (btnToggleLeft != null)
            {
                var t = btnToggleLeft.GetComponentInChildren<Text>();
                if (t != null) t.text = leftCollapsed ? "» Mở bảng trái" : "«";
                var rt = (RectTransform)btnToggleLeft.transform;
                rt.anchoredPosition = leftCollapsed ? new Vector2(12f, -60f) : new Vector2(256f, -60f);
                rt.sizeDelta = leftCollapsed ? new Vector2(96f, 26f) : new Vector2(24f, 24f);
            }
        }

        void ToggleRightSidebar()
        {
            rightCollapsed = !rightCollapsed;
            if (rightSidebar != null) rightSidebar.gameObject.SetActive(!rightCollapsed);
            if (floatingTools != null)
                floatingTools.anchoredPosition = new Vector2(rightCollapsed ? -16f : -346f, 0f);
            if (btnToggleRight != null)
            {
                var t = btnToggleRight.GetComponentInChildren<Text>();
                if (t != null) t.text = rightCollapsed ? "Mở thẻ phải «" : "»";
                var rt = (RectTransform)btnToggleRight.transform;
                rt.anchoredPosition = rightCollapsed ? new Vector2(-12f, -60f) : new Vector2(-302f, -60f);
                rt.sizeDelta = rightCollapsed ? new Vector2(96f, 26f) : new Vector2(24f, 24f);
            }
        }

        // ---------------------------------------------------------------------
        // NHÃN CHÚ THÍCH 3D KIỂU PHÒNG GIẢI PHẪU (◉ Tên bộ phận) & NHÃN BƯỚC MŨI TÊN (① Bước)
        // ---------------------------------------------------------------------
        void Build3DPinBadges()
        {
            if (pinLayerRt == null || Cay3D == null) return;
            pinBadges.Clear();

            var pins = Cay3D.DanhSachPin;
            for (int i = 0; i < pins.Count; i++)
            {
                var pin = pins[i];
                var badgeGo = new GameObject($"Nhan3D_{i}_{pin.PartId}", typeof(RectTransform));
                badgeGo.layer = 5;
                var rt = badgeGo.GetComponent<RectTransform>();
                rt.SetParent(pinLayerRt, false);
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0f, 0.5f);

                float pillW = Mathf.Clamp(pin.Label.Length * 6.5f + 22f, 88f, 250f);
                rt.sizeDelta = new Vector2(pillW + 24f, 26f);

                var haloRt = GlassKit.Rect(rt, "Halo", new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18f, 18f));
                var haloImg = haloRt.gameObject.AddComponent<Image>();
                haloImg.sprite = GlassKit.SolidCircle;
                haloImg.color = new Color(0f, 0f, 0f, 0.50f);
                haloImg.raycastTarget = false;

                var whiteRingRt = GlassKit.Rect(rt, "VongTrang", new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(13f, 13f));
                var whiteRingImg = whiteRingRt.gameObject.AddComponent<Image>();
                whiteRingImg.sprite = GlassKit.SolidCircle;
                whiteRingImg.color = Color.white;
                whiteRingImg.raycastTarget = false;

                var dotRt = GlassKit.Rect(rt, "ChamMau", new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(8f, 8f));
                var dotImg = dotRt.gameObject.AddComponent<Image>();
                dotImg.sprite = GlassKit.SolidCircle;
                dotImg.color = pin.DotColor;
                dotImg.raycastTarget = false;

                var pillRt = GlassKit.Rect(rt, "VienThuoc", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(11f, 0f), new Vector2(pillW, 24f));

                var shRt = MenuKit.Stretch(pillRt, "Bóng", Vector2.zero, Vector2.one, new Vector2(-4f, -6f), new Vector2(4f, 2f));
                var shImg = shRt.gameObject.AddComponent<Image>();
                shImg.sprite = MenuKit.ShadowSprite(5f, 12f);
                shImg.type = Image.Type.Sliced;
                shImg.pixelsPerUnitMultiplier = 2f;
                shImg.color = new Color(0f, 0f, 0f, 0.62f);
                shImg.raycastTarget = false;

                var pillFace = pillRt.gameObject.AddComponent<Image>();
                pillFace.sprite = MenuKit.Round(12f);
                pillFace.type = Image.Type.Sliced;
                pillFace.pixelsPerUnitMultiplier = 2f;
                pillFace.color = PillBg;
                pillFace.raycastTarget = true;

                var pillRing = MenuKit.Ring(pillRt, "Viền", BorderPill, 12f);

                var lbl = GlassKit.Label(pillRt, "Chữ", pin.Label, 11, Color.white, true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(pillW, 24f));
                lbl.horizontalOverflow = HorizontalWrapMode.Overflow;

                float exactW = Mathf.Clamp(lbl.preferredWidth + 20f, 76f, 270f);
                pillRt.sizeDelta = new Vector2(exactW, 24f);
                lbl.rectTransform.sizeDelta = new Vector2(exactW, 24f);
                rt.sizeDelta = new Vector2(exactW + 14f, 26f);

                var btn = pillRt.gameObject.AddComponent<Button>();
                btn.navigation = new Navigation { mode = Navigation.Mode.None };
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = pillFace;
                string capPartId = pin.PartId;
                btn.onClick.AddListener(() =>
                {
                    if (cheDo == CheDoThucVat.CauHoi && cheDoChiTren3D) ChamDiemChiTren3D(capPartId);
                    else ChonBoPhan(capPartId);
                });

                pinBadges.Add(new PinBadgeUi { pin = pin, root = rt, pillRing = pillRing, pillFace = pillFace, label = lbl });
            }
            SetLayerRecursive(pinLayerRt, 5);
        }

        void BuildActiveStep3DBadge()
        {
            if (pinLayerRt == null) return;
            var go = new GameObject("NhanBuocMuiTen3D", typeof(RectTransform));
            go.layer = 5;
            activeStepBadgeRt = go.GetComponent<RectTransform>();
            activeStepBadgeRt.SetParent(pinLayerRt, false);
            activeStepBadgeRt.anchorMin = new Vector2(0.5f, 0.5f);
            activeStepBadgeRt.anchorMax = new Vector2(0.5f, 0.5f);
            activeStepBadgeRt.pivot = new Vector2(0.5f, 0.5f);
            activeStepBadgeRt.sizeDelta = new Vector2(240f, 26f);

            var shRt = MenuKit.Stretch(activeStepBadgeRt, "Bóng", Vector2.zero, Vector2.one, new Vector2(-4f, -6f), new Vector2(4f, 2f));
            var sh = shRt.gameObject.AddComponent<Image>();
            sh.sprite = MenuKit.ShadowSprite(6f, 13f);
            sh.type = Image.Type.Sliced;
            sh.pixelsPerUnitMultiplier = 2f;
            sh.color = new Color(0f, 0f, 0f, 0.72f);
            sh.raycastTarget = false;

            var face = go.AddComponent<Image>();
            face.sprite = MenuKit.Round(13f);
            face.type = Image.Type.Sliced;
            face.pixelsPerUnitMultiplier = 2f;
            face.color = new Color(0.06f, 0.08f, 0.12f, 0.96f);
            face.raycastTarget = false;

            activeStepPillRing = MenuKit.Ring(activeStepBadgeRt, "Viền", BorderBlue, 13f);

            var numRt = GlassKit.Rect(activeStepBadgeRt, "SoBuoc", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(18f, 18f));
            activeStepNumBg = numRt.gameObject.AddComponent<Image>();
            activeStepNumBg.sprite = GlassKit.SolidCircle;
            activeStepNumBg.color = BorderBlue;
            activeStepNumBg.raycastTarget = false;

            activeStepNumTxt = GlassKit.Label(numRt, "ChuSo", "1", 10, Color.white, true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(18f, 18f));
            activeStepTitleTxt = GlassKit.Label(activeStepBadgeRt, "TenBuoc", "", 11, Color.white, true, TextAnchor.MiddleLeft, new Vector2(27f, 0f), new Vector2(206f, 26f));
            activeStepTitleTxt.horizontalOverflow = HorizontalWrapMode.Overflow;

            activeStepBadgeRt.gameObject.SetActive(false);
            SetLayerRecursive(activeStepBadgeRt, 5);
        }

        void UpdateActiveStep3DBadge()
        {
            if (activeStepBadgeRt == null || MuiTen3D == null || StageCamera == null || pinLayerRt == null) return;
            if (!MuiTen3D.TryGetActiveStepBadge(out Vector3 worldPos, out int stepNum, out string title, out Color col))
            {
                if (activeStepBadgeRt.gameObject.activeSelf) activeStepBadgeRt.gameObject.SetActive(false);
                return;
            }

            Vector3 vp = StageCamera.WorldToViewportPoint(worldPos);
            if (vp.z <= 0.02f)
            {
                activeStepBadgeRt.gameObject.SetActive(false);
                return;
            }

            if (!activeStepBadgeRt.gameObject.activeSelf) activeStepBadgeRt.gameObject.SetActive(true);
            activeStepNumTxt.text = stepNum.ToString();
            activeStepNumBg.color = col;
            activeStepPillRing.color = col;
            activeStepTitleTxt.text = title;

            float w = Mathf.Clamp(activeStepTitleTxt.preferredWidth + 40f, 110f, 310f);
            activeStepBadgeRt.sizeDelta = new Vector2(w, 26f);
            activeStepTitleTxt.rectTransform.sizeDelta = new Vector2(w - 34f, 26f);

            Rect r = pinLayerRt.rect;
            float halfW = r.width * 0.5f;
            float halfH = r.height * 0.5f;
            float minX = -halfW + (leftCollapsed ? 24f : 316f) + w * 0.5f;
            float maxX =  halfW - (rightCollapsed ? 68f : 402f) - w * 0.5f;
            float cx = Mathf.Clamp((vp.x - 0.5f) * r.width, minX, Mathf.Max(minX, maxX));
            float cy = Mathf.Clamp((vp.y - 0.5f) * r.height, -halfH + 62f, halfH - 112f);
            activeStepBadgeRt.anchoredPosition = new Vector2(cx, cy);
        }

        readonly List<(int idx, Vector2 pos)> visiblePinsBuf = new List<(int idx, Vector2 pos)>(16);

        void Update3DPinBadges()
        {
            if (pinLayerRt == null || StageCamera == null || Cay3D == null) return;
            // Khi đang chạy Quá trình mũi tên 3D: ẩn tạm nhãn bộ phận tĩnh để tập trung hoàn toàn vào mũi tên & nhãn bước!
            bool showAll = Cay3D.HienChuThich && (MuiTen3D == null || !MuiTen3D.DangMo);
            KhungNhinThucVat curView = Cay3D.KhungHienTai;
            Rect canvasRect = pinLayerRt.rect;
            float halfW = canvasRect.width * 0.5f;
            float halfH = canvasRect.height * 0.5f;
            float minX = -halfW + (leftCollapsed ? 24f : 312f);
            float minY = -halfH + (cheDo == CheDoThucVat.BaiGiang ? 128f : 58f);
            float maxY =  halfH - 118f;

            visiblePinsBuf.Clear();
            for (int i = 0; i < pinBadges.Count; i++)
            {
                var b = pinBadges[i];
                bool active = showAll && b.pin.View == curView && b.pin.Anchor != null && b.pin.Anchor.gameObject.activeInHierarchy;
                if (!active)
                {
                    if (b.root.gameObject.activeSelf) b.root.gameObject.SetActive(false);
                    continue;
                }

                Vector3 worldPos = b.pin.Anchor.TransformPoint(b.pin.LocalOffset);
                Vector3 vp = StageCamera.WorldToViewportPoint(worldPos);
                if (vp.z <= 0.02f || vp.x < -0.05f || vp.x > 1.05f || vp.y < -0.05f || vp.y > 1.05f)
                {
                    if (b.root.gameObject.activeSelf) b.root.gameObject.SetActive(false);
                    continue;
                }

                if (!b.root.gameObject.activeSelf) b.root.gameObject.SetActive(true);
                float badgeW = b.root.sizeDelta.x;
                float safeRightEdge = halfW - (rightCollapsed ? 62f : 398f);
                float cx = Mathf.Clamp((vp.x - 0.5f) * canvasRect.width, minX, Mathf.Max(minX, safeRightEdge - badgeW));
                float cy = Mathf.Clamp((vp.y - 0.5f) * canvasRect.height, minY, maxY);
                visiblePinsBuf.Add((i, new Vector2(cx, cy)));
            }

            visiblePinsBuf.Sort((a, b) => b.pos.y.CompareTo(a.pos.y));
            for (int k = 1; k < visiblePinsBuf.Count; k++)
            {
                var prev = visiblePinsBuf[k - 1];
                var cur = visiblePinsBuf[k];
                if (Mathf.Abs(cur.pos.x - prev.pos.x) < 175f && (prev.pos.y - cur.pos.y) < 30f)
                {
                    cur.pos.y = Mathf.Max(minY, prev.pos.y - 30f);
                    visiblePinsBuf[k] = cur;
                }
            }

            for (int k = 0; k < visiblePinsBuf.Count; k++)
                pinBadges[visiblePinsBuf[k].idx].root.anchoredPosition = visiblePinsBuf[k].pos;
        }

        // ---------------------------------------------------------------------
        // DỰNG GIAO DIỆN THEO CHỦ ĐỀ PHÒNG GIẢI PHẪU
        // ---------------------------------------------------------------------
        void BuildUi()
        {
            var cvGo = new GameObject("GiaoDienThucVat");
            cvGo.transform.SetParent(transform, false);
            UiCanvas = cvGo.AddComponent<Canvas>();
            UiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            UiCanvas.sortingOrder = 180;
            var scaler = cvGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0f;
            cvGo.AddComponent<GraphicRaycaster>();

            Transform root = cvGo.transform;

            OrbitCam?.AttachViewportZone(UiCanvas);
            pinLayerRt = MenuKit.Full(root, "LopNhanChuThich3D");

            // =================================================================
            // 1. THANH HEADER TRÊN CÙNG (.thanh-tren cao 50px)
            // =================================================================
            var topBar = MakeAnatomyPanel(root, "ThanhTren", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(1920f, 50f), HeaderBg, radius: 0f, hasShadow: false);

            GlassKit.Label(topBar, "TieuDe", "Phòng Sinh học Thực vật 3D", 13, TextMain, true, TextAnchor.UpperLeft,
                new Vector2(18f, -7f), new Vector2(226f, 18f));
            GlassKit.Label(topBar, "PhuDe", "GIẢI PHẪU · SINH LÝ · CẤP ĐỘ TẾ BÀO", 9, TextMuted, true, TextAnchor.LowerLeft,
                new Vector2(18f, -26f), new Vector2(226f, 14f));

            var modeTrack = MakeAnatomyCard(topBar, "TrackCheDo", new Vector2(246f, -9f), new Vector2(254f, 32f), radius: 16f);
            string[] modeNames = { "Bài giảng", "Thí nghiệm", "Kiểm tra" };
            for (int i = 0; i < 3; i++)
            {
                int capI = i;
                modeChips[i] = MakeAnatomyButton(modeTrack, $"CheDo_{i}", modeNames[i], new Vector2(3f + i * 83f, -3f), new Vector2(80f, 26f),
                    () => ChonCheDo((CheDoThucVat)capI), i == 0, 11, radius: 13f, activeFill: BtnSelectedDark, activeRing: BorderGreen);
            }

            var viewTrack = MakeAnatomyCard(topBar, "TrackKhungNhin", new Vector2(510f, -9f), new Vector2(366f, 32f), radius: 16f);
            string[] viewNames = { "Tổng thể", "Lát cắt Rễ", "Lát cắt Thân", "Lát cắt Lá" };
            for (int i = 0; i < 4; i++)
            {
                int capI = i;
                viewChips[i] = MakeAnatomyButton(viewTrack, $"KhungNhin_{i}", viewNames[i], new Vector2(3f + i * 90f, -3f), new Vector2(88f, 26f),
                    () => { if (MuiTen3D != null && MuiTen3D.DangMo) DongQuaTrinh(); ChonKhungNhin((KhungNhinThucVat)capI); },
                    i == 0, 11, radius: 13f, activeFill: BtnSelectedDark, activeRing: BorderBlue);
            }

            // Nút mở nhanh "▸ Quá trình 3D" ngay thanh trên
            btnOpenProcesses = MakeAnatomyButton(topBar, "NutMoQuaTrinh3D", "▸ Quá trình 3D", new Vector2(888f, -10f), new Vector2(118f, 30f), () =>
            {
                if (MuiTen3D != null && MuiTen3D.DangMo) DongQuaTrinh();
                else BatDauQuaTrinh("hap_thu_nuoc", 0);
            }, false, 11, radius: 8f, activeFill: BtnPrimary, activeRing: BorderBlue);

            btnParticles = MakeAnatomyButton(topBar, "NutHatDong", "Dòng hạt: BẬT", new Vector2(1224f, -10f), new Vector2(96f, 30f), () =>
            {
                bool next = Cay3D == null || !Cay3D.HienHatDong;
                Cay3D?.SetHienHatDong(next);
                UpdateParticlesButtonState();
            }, true, 10, radius: 7f, activeFill: BtnSelectedDark, activeRing: BorderBlue);

            btnLamp = MakeAnatomyButton(topBar, "NutDen", "Đèn: BẬT", new Vector2(1326f, -10f), new Vector2(74f, 30f), () =>
            {
                bool next = Cay3D == null || !Cay3D.HienDenHuongSang;
                Cay3D?.SetHienDenHuongSang(next);
                var lbl = btnLamp.GetComponentInChildren<Text>();
                if (lbl != null) lbl.text = next ? "Đèn: BẬT" : "Đèn: TẮT";
                SetAnatomyBtnState(btnLamp, next, activeFill: BtnSelectedDark, activeRing: BorderBlue);
            }, true, 10, radius: 7f, activeFill: BtnSelectedDark, activeRing: BorderBlue);

            btnExplode = MakeAnatomyButton(topBar, "NutBocTach", "Bóc tách lớp", new Vector2(1406f, -10f), new Vector2(90f, 30f), () =>
            {
                bool next = Cay3D != null && !Cay3D.CheDoBocTach;
                Cay3D?.SetCheDoBocTach(next);
                SetAnatomyBtnState(btnExplode, next, activeFill: BtnSelectedDark, activeRing: BorderBlue);
            }, false, 11, radius: 7f);

            btnXray = MakeAnatomyButton(topBar, "NutSoiMach", "Soi mạch", new Vector2(1502f, -10f), new Vector2(76f, 30f), () =>
            {
                bool next = Cay3D != null && !Cay3D.CheDoXQuangMach;
                Cay3D?.SetCheDoXQuangMach(next);
                SetAnatomyBtnState(btnXray, next, activeFill: BtnSelectedDark, activeRing: BorderBlue);
            }, false, 11, radius: 7f);

            btnAnno = MakeAnatomyButton(topBar, "NutChuThich", "Nhãn: BẬT", new Vector2(1584f, -10f), new Vector2(82f, 30f), () =>
            {
                bool next = Cay3D == null || !Cay3D.HienChuThich;
                Cay3D?.SetHienChuThich(next);
                var lbl = btnAnno.GetComponentInChildren<Text>();
                if (lbl != null) lbl.text = next ? "Nhãn: BẬT" : "Nhãn: TẮT";
                SetAnatomyBtnState(btnAnno, next, activeFill: BtnSelectedDark, activeRing: BorderGreen);
                Update3DPinBadges();
            }, true, 11, radius: 7f, activeFill: BtnSelectedDark, activeRing: BorderGreen);

            MakeAnatomyButton(topBar, "NutDatLaiGoc", "↺ Đặt lại", new Vector2(1672f, -10f), new Vector2(76f, 30f),
                () => ChonKhungNhin(Cay3D != null ? Cay3D.KhungHienTai : KhungNhinThucVat.TongThe), false, 11, radius: 7f);

            MakeAnatomyButton(topBar, "NutDong", "← Về Lab (Esc)", new Vector2(1754f, -10f), new Vector2(148f, 30f),
                ThucVatMoDunImpl.Dong, true, 11, radius: 7f, activeFill: new Color(0.18f, 0.19f, 0.22f, 1f), activeRing: BorderLine);

            // =================================================================
            // 2. CỘT TRÁI (rộng 288px)
            // =================================================================
            leftSidebar = MakeAnatomyPanel(root, "BangMoiTruong", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(10f, -22f), new Vector2(288f, 996f), SidebarBg, radius: 12f, hasShadow: true);

            GlassKit.Label(leftSidebar, "DauCotTrai", "Điều khiển & Thực nghiệm", 12, TextMain, true, TextAnchor.MiddleLeft,
                new Vector2(14f, -8f), new Vector2(220f, 22f));

            btnToggleLeft = MakeAnatomyButton(root, "NutThuTrai", "«", new Vector2(256f, -60f), new Vector2(24f, 24f),
                ToggleLeftSidebar, false, 11, radius: 6f);

            // Thẻ 1: Chọn nhanh cấu trúc 3D
            var cardParts = MakeAnatomyCard(leftSidebar, "TheBoPhan", new Vector2(10f, -34f), new Vector2(268f, 102f), radius: 10f);
            GlassKit.Label(cardParts, "NhanChonNhanh", "CẤU TRÚC TRONG KHUNG NHÌN", 10, TextMuted, true, TextAnchor.MiddleLeft,
                new Vector2(10f, -5f), new Vector2(248f, 14f));
            partSelectorRow = GlassKit.Rect(cardParts, "HangNutBoPhan", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -22f), new Vector2(248f, 76f));

            // Thẻ 2: 6 QUÁ TRÌNH SINH LÝ 3D (Mũi tên từng bước) + Tab 5 Kịch bản mẫu
            var cardProcesses = MakeAnatomyCard(leftSidebar, "TheQuaTrinh", new Vector2(10f, -142f), new Vector2(268f, 92f), radius: 10f);
            btnTabCard2Proc = MakeAnatomyButton(cardProcesses, "Tab6QuaTrinh", "6 Quá trình 3D", new Vector2(8f, -6f), new Vector2(130f, 20f),
                () => SetLeftCard2Mode(false), true, 10, radius: 5f, activeFill: BtnSelectedDark, activeRing: BorderBlue);
            btnTabCard2Preset = MakeAnatomyButton(cardProcesses, "Tab5KichBan", "Kịch bản mẫu", new Vector2(142f, -6f), new Vector2(118f, 20f),
                () => SetLeftCard2Mode(true), false, 10, radius: 5f, activeFill: BtnSelectedDark, activeRing: BorderGreen);

            leftCard2ProcGroup = GlassKit.Rect(cardProcesses, "Nhom6QuaTrinh", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -31f), new Vector2(252f, 56f));
            var allProcs = ThucVatQuaTrinhData.DanhSach;
            string[] procShortLabels = { "1. Hút nước", "2. Quang hợp", "3. Hô hấp", "4. Thoát nước", "5. Mạch rây", "6. Hướng sáng" };
            for (int i = 0; i < 6 && i < allProcs.Length; i++)
            {
                string capMa = allProcs[i].Ma;
                int row = i / 3;
                int col = i % 3;
                processQuickLeftBtns[i] = MakeAnatomyButton(leftCard2ProcGroup, $"QT_{capMa}", procShortLabels[i],
                    new Vector2(col * 85f, -row * 27f), new Vector2(82f, 24f),
                    () => BatDauQuaTrinh(capMa, 0), false, 9, radius: 6f, activeFill: BtnSelectedDark, activeRing: BorderBlue);
            }

            leftCard2PresetGroup = GlassKit.Rect(cardProcesses, "Nhom5KichBan", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -31f), new Vector2(252f, 56f));
            string[] presetShort = { "1. Tối ưu C3", "2. Khô hạn", "3. Ban đêm", "4. Hướng sáng", "5. Nhà kính" };
            for (int i = 0; i < 5; i++)
            {
                int capI = i;
                int row = i / 3;
                int col = i % 3;
                presetChips[i] = MakeAnatomyButton(leftCard2PresetGroup, $"KichBan_{i}", presetShort[i], new Vector2(col * 85f, -row * 27f), new Vector2(82f, 24f),
                    () => ApDungKichBan(capI), false, 9, radius: 6f, activeFill: BtnSelectedDark, activeRing: BorderBlue);
            }
            MakeAnatomyButton(leftCard2PresetGroup, "NutMacDinh", "↺ Chuẩn", new Vector2(2 * 85f, -27f), new Vector2(82f, 24f), () =>
            {
                kichBanDangChon = -1;
                for (int i = 0; i < presetChips.Length; i++) SetAnatomyBtnState(presetChips[i], false);
                DatDieuKien(DieuKienMoiTruong.MacDinh);
                if (txtPresetConclusion != null)
                    txtPresetConclusion.text = "<color=#60A5FA><b>Trạng thái chuẩn:</b></color> Kéo thanh trượt hoặc chọn quá trình 3D để khảo sát.";
            }, false, 9, radius: 6f);
            leftCard2PresetGroup.gameObject.SetActive(false);

            // Thẻ 3: Thông số môi trường (thanh trượt mảnh, núm trắng 12px)
            var cardEnv = MakeAnatomyCard(leftSidebar, "TheMoiTruong", new Vector2(10f, -240f), new Vector2(268f, 344f), radius: 10f);
            GlassKit.Label(cardEnv, "TieuDeTrai", "THÔNG SỐ MÔI TRƯỜNG", 10, TextMuted, true, TextAnchor.MiddleLeft,
                new Vector2(10f, -7f), new Vector2(248f, 14f));

            syncSliderLabels.Clear();
            float y = -26f;
            const float stepY = 39f;
            slLight = MakeSlimAnatomySlider(cardEnv, "SlLight", "Ánh sáng PAR", y, 0f, 55000f, (float)dk.AnhSangLux,
                v => { if (!suppressSliderCallbacks) dk.AnhSangLux = v; }, v => $"{v:0} lx"); y -= stepY;
            slCo2 = MakeSlimAnatomySlider(cardEnv, "SlCo2", "Nồng độ CO₂", y, 0f, 1200f, (float)dk.Co2Ppm,
                v => { if (!suppressSliderCallbacks) dk.Co2Ppm = v; }, v => $"{v:0} ppm"); y -= stepY;
            slTemp = MakeSlimAnatomySlider(cardEnv, "SlTemp", "Nhiệt độ", y, 5f, 45f, (float)dk.NhietDoC,
                v => { if (!suppressSliderCallbacks) dk.NhietDoC = v; }, v => $"{v:0.0} °C"); y -= stepY;
            slRh = MakeSlimAnatomySlider(cardEnv, "SlRh", "Độ ẩm khí", y, 10f, 98f, (float)dk.DoAmKhongKhiPct,
                v => { if (!suppressSliderCallbacks) dk.DoAmKhongKhiPct = v; }, v => $"{v:0}%"); y -= stepY;
            slSoil = MakeSlimAnatomySlider(cardEnv, "SlSoil", "Độ ẩm đất", y, 5f, 100f, (float)dk.DoAmDatPct,
                v => { if (!suppressSliderCallbacks) dk.DoAmDatPct = v; }, v => $"{v:0}%"); y -= stepY;
            slWind = MakeSlimAnatomySlider(cardEnv, "SlWind", "Gió mặt lá", y, 0f, 100f, (float)dk.Gio01 * 100f,
                v => { if (!suppressSliderCallbacks) dk.Gio01 = v / 100f; }, v => $"{v:0}%"); y -= stepY;
            slLampAngle = MakeSlimAnatomySlider(cardEnv, "SlLamp", "Góc đèn chiếu", y, -70f, 70f, (float)dk.GocDenHuongSangDo,
                v => { if (!suppressSliderCallbacks) dk.GocDenHuongSangDo = v; }, v => $"{v:+0;-0;0}°"); y -= 44f;

            float[] spds = { 1f, 60f, 600f };
            for (int i = 0; i < 3; i++)
            {
                float sp = spds[i];
                int capI = i;
                speedChips[i] = MakeAnatomyButton(cardEnv, $"Tua_{sp}", $"×{sp:0}", new Vector2(10f + i * 48f, y), new Vector2(44f, 24f), () =>
                {
                    dayTimeScale = sp;
                    for (int k = 0; k < 3; k++) SetAnatomyBtnState(speedChips[k], k == capI, activeFill: BtnSelectedDark, activeRing: BorderBlue);
                }, i == 0, 10, radius: 6f, activeFill: BtnSelectedDark, activeRing: BorderBlue);
            }
            btnCycle = MakeAnatomyButton(cardEnv, "ChuKyNgayDem", "Ngày/Đêm: TẮT", new Vector2(156f, y), new Vector2(102f, 24f), () =>
            {
                autoDayCycle = !autoDayCycle;
                var lbl = btnCycle.GetComponentInChildren<Text>();
                if (lbl != null) lbl.text = autoDayCycle ? "Ngày/Đêm: BẬT" : "Ngày/Đêm: TẮT";
                SetAnatomyBtnState(btnCycle, autoDayCycle, activeFill: BtnSelectedDark, activeRing: BorderGreen);
            }, false, 10, radius: 6f);

            // Thẻ 4: Đáp ứng sinh lý & Chẩn đoán trực tiếp
            var meterWell = MakeAnatomyCard(leftSidebar, "KhungDongHoSinhLy", new Vector2(10f, -590f), new Vector2(268f, 202f), radius: 10f);
            GlassKit.Label(meterWell, "TieuDeMeter", "ĐÁP ỨNG SINH LÝ THỜI GIAN THỰC", 10, TextMuted, true, TextAnchor.MiddleLeft,
                new Vector2(10f, -6f), new Vector2(248f, 14f));

            BuildMeterBar(meterWell, "Quang hợp ròng", -24f, new Color(0.06f, 0.73f, 0.51f), out barPhotoFill, out valPhotoTxt);
            BuildMeterBar(meterWell, "Thoát hơi nước", -45f, new Color(0.23f, 0.51f, 0.96f), out barTranspFill, out valTranspTxt);
            BuildMeterBar(meterWell, "Độ mở khí khổng", -66f, new Color(0.52f, 0.85f, 0.26f), out barStomaFill, out valStomaTxt);
            BuildMeterBar(meterWell, "Tín hiệu hạn ABA", -87f, new Color(0.96f, 0.62f, 0.04f), out barAbaFill, out valAbaTxt);

            txtMetrics = GlassKit.Label(meterWell, "ChiSoSinhLy", "", 10, TextSub, false, TextAnchor.UpperLeft,
                new Vector2(10f, -108f), new Vector2(248f, 38f));

            var diagWell = MakeAnatomyCard(meterWell, "HopChanDoan", new Vector2(8f, -148f), new Vector2(252f, 46f), radius: 7f);
            txtDiagnosis = GlassKit.Label(diagWell, "ChuChanDoan", "", 9, TextMain, false, TextAnchor.MiddleLeft,
                new Vector2(8f, -3f), new Vector2(236f, 40f));

            // Thẻ 5: Kết luận & Chú giải
            var legendCard = MakeAnatomyCard(leftSidebar, "HopChuGiaiHat", new Vector2(10f, -798f), new Vector2(268f, 188f), radius: 10f);
            txtPresetConclusion = GlassKit.Label(legendCard, "KetLuanKichBan",
                "<color=#60A5FA><b>Mẹo dạy học:</b></color> Bấm vào 1 trong <b>6 Quá trình 3D</b> phía trên để xem mũi tên từng bước như Vòng lớn tim.",
                10, TextMain, false, TextAnchor.UpperLeft, new Vector2(10f, -8f), new Vector2(248f, 62f));

            GlassKit.Label(legendCard, "ChuGiaiHat",
                "<color=#73757D><b>CHÚ GIẢI DÒNG VẬT CHẤT 3D</b></color>\n" +
                "<color=#38BDF8>● Hạt lam:</color> Nước & khoáng (Mạch gỗ ↑)\n" +
                "<color=#FBBF24>● Hạt cam:</color> Đường saccarôzơ (Mạch rây ↓)\n" +
                "<color=#CBD5E1>● Hạt xám:</color> CO₂ vào   <color=#34D399>● Hạt lục:</color> O₂ ra\n" +
                "<color=#FDE047>● Hạt vàng:</color> Hormone Auxin (IAA)",
                10, TextSub, false, TextAnchor.UpperLeft, new Vector2(10f, -74f), new Vector2(248f, 106f));

            // =================================================================
            // 3. CỘT PHẢI (rộng 324px — 3A: Inspector | 3B: Quiz | 3C: Giải thích từng bước Mũi tên 3D)
            // =================================================================
            rightSidebar = MakeAnatomyPanel(root, "CotPhai", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-10f, -22f), new Vector2(324f, 996f), SidebarBg, radius: 12f, hasShadow: true);

            btnToggleRight = MakeAnatomyButton(root, "NutThuPhai", "»", new Vector2(-302f, -60f), new Vector2(24f, 24f),
                ToggleRightSidebar, false, 11, radius: 6f);

            // 3A. Thẻ chi tiết giải phẫu (Inspector)
            cardPanel = GlassKit.Rect(rightSidebar, "TheKienThuc", new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(324f, 996f));
            GlassKit.Label(cardPanel, "DauCotPhai", "Chi tiết Giải phẫu & Sinh lý", 12, TextMain, true, TextAnchor.MiddleLeft,
                new Vector2(14f, -10f), new Vector2(260f, 22f));

            var infoCard = MakeAnatomyCard(cardPanel, "HopNoiDungKienThuc", new Vector2(10f, -38f), new Vector2(304f, 946f), radius: 10f);
            txtCardGroupBadge = GlassKit.Label(infoCard, "NhanThe", "CƠ QUAN CÂY HAI LÁ MẦM", 9, new Color(0.38f, 0.65f, 0.98f), true, TextAnchor.MiddleLeft,
                new Vector2(12f, -10f), new Vector2(280f, 14f));
            txtCardTitle = GlassKit.Label(infoCard, "TenBoPhan", "", 15, Color.white, true, TextAnchor.UpperLeft,
                new Vector2(12f, -26f), new Vector2(280f, 40f));

            btnFocusPart = MakeAnatomyButton(infoCard, "NutLayNet360", "◎ Tập trung & Xoay 360° bộ phận này",
                new Vector2(12f, -70f), new Vector2(280f, 27f), BatTatLayNetBoPhan360, false, 11, radius: 7f, activeFill: BtnWarm, activeRing: BorderBlue);

            btnJumpHistology = MakeAnatomyButton(infoCard, "NutSoiViThe", "▸ Phóng đại Lát cắt vi thể Thân →",
                new Vector2(12f, -102f), new Vector2(280f, 27f), NhaySangLatCatTuongUng, true, 11, radius: 7f,
                activeFill: new Color(0.08f, 0.28f, 0.22f, 0.98f), activeRing: BorderGreen);

            var tabTrack = MakeAnatomyCard(infoCard, "TrackTabKienThuc", new Vector2(12f, -138f), new Vector2(280f, 30f), radius: 8f);
            string[] tabTitles = { "Cấu tạo & Cơ chế", "1–2 Lá mầm", "Vấn đáp SGK" };
            for (int i = 0; i < 3; i++)
            {
                int capI = i;
                cardTabChips[i] = MakeAnatomyButton(tabTrack, $"TabKienThuc_{i}", tabTitles[i], new Vector2(3f + i * 92f, -3f), new Vector2(90f, 24f),
                    () => ChonTabKienThuc(capI), i == 0, 10, radius: 6f, activeFill: BtnSelectedDark, activeRing: BorderBlue);
            }

            txtCardBody = GlassKit.Label(infoCard, "NoiDungThe", "", 12, TextMain, false, TextAnchor.UpperLeft,
                new Vector2(12f, -176f), new Vector2(280f, 712f));

            btnToggleAns = MakeAnatomyButton(infoCard, "NutHienDapAn", "Hiện đáp án chuẩn SGK", new Vector2(12f, -898f), new Vector2(280f, 32f), () =>
            {
                hienDapAnNhanh = !hienDapAnNhanh;
                RefreshKnowledgeCard();
            }, true, 11, radius: 7f, activeFill: BtnActive, activeRing: BorderGreen);
            btnToggleAns.gameObject.SetActive(false);

            // 3B. Bảng Kiểm tra & Thực hành 3D
            quizPanel = GlassKit.Rect(rightSidebar, "BangCauHoi", new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(324f, 996f));
            GlassKit.Label(quizPanel, "TieuDeQuiz", "Kiểm tra & Thực hành 3D", 12, TextMain, true, TextAnchor.MiddleLeft,
                new Vector2(14f, -10f), new Vector2(190f, 22f));
            txtQuizStatus = GlassKit.Label(quizPanel, "DiemQuiz", "Điểm: 0/0", 11, BorderGreen, true, TextAnchor.MiddleRight,
                new Vector2(200f, -10f), new Vector2(80f, 22f));

            var quizCard = MakeAnatomyCard(quizPanel, "TheQuizTrong", new Vector2(10f, -38f), new Vector2(304f, 946f), radius: 10f);
            btnQuizPoint3D = MakeAnatomyButton(quizCard, "NutCheDoChi3D", "Thực hành: Chỉ trực tiếp bộ phận trên 3D",
                new Vector2(12f, -12f), new Vector2(280f, 28f), BatTatCheDoChi3D, false, 11, radius: 7f, activeFill: BtnWarm, activeRing: BorderBlue);

            var qBox = MakeAnatomyCard(quizCard, "KhungCauHoi", new Vector2(12f, -48f), new Vector2(280f, 96f), radius: 8f);
            txtQuizQuestion = GlassKit.Label(qBox, "NoiDungCauHoi", "", 12, Color.white, false, TextAnchor.UpperLeft,
                new Vector2(10f, -8f), new Vector2(260f, 80f));

            for (int i = 0; i < 4; i++)
            {
                int capI = i;
                quizOptBtns[i] = MakeAnatomyButton(quizCard, $"DapAn_{i}", "", new Vector2(12f, -154f - i * 46f), new Vector2(280f, 40f),
                    () => TraLoiCauHoi(capI), false, 11, radius: 7f, activeFill: BtnActive, activeRing: BorderGreen);
                quizOptTexts[i] = quizOptBtns[i].GetComponentInChildren<Text>();
                if (quizOptTexts[i] != null)
                {
                    quizOptTexts[i].alignment = TextAnchor.MiddleLeft;
                    quizOptTexts[i].rectTransform.offsetMin = new Vector2(12f, 2f);
                    quizOptTexts[i].rectTransform.offsetMax = new Vector2(-12f, -2f);
                }
            }

            txtQuizFeedback = GlassKit.Label(quizCard, "GiaiThichQuiz", "", 11, TextMain, false, TextAnchor.UpperLeft,
                new Vector2(12f, -346f), new Vector2(280f, 240f));

            MakeAnatomyButton(quizCard, "CauTruoc", "← Câu trước", new Vector2(12f, -600f), new Vector2(136f, 32f),
                () => HienCauHoi((cheDoChiTren3D ? chi3DIdx : cauHoiIdx) - 1), false, 11, radius: 7f);
            MakeAnatomyButton(quizCard, "CauSau", "Câu tiếp →", new Vector2(156f, -600f), new Vector2(136f, 32f),
                () => HienCauHoi((cheDoChiTren3D ? chi3DIdx : cauHoiIdx) + 1), true, 11, radius: 7f, activeFill: BtnPrimary, activeRing: BorderBlue);

            // 3C. Bảng GIẢI THÍCH TỪNG BƯỚC (MŨI TÊN 3D) — Đồng bộ ExplainPanel của Phòng Giải phẫu (Vòng lớn tim)
            BuildProcessExplanationPanel();

            // =================================================================
            // 4. VÙNG GIỮA: HUD TRẠNG THÁI, THANH CÔNG CỤ 3D & GÓC NHÌN 360°
            // =================================================================
            var hudPill = MakeAnatomyPanel(root, "HudTrangThai", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-18f, -58f), new Vector2(360f, 28f), CardBg, radius: 14f, hasShadow: true);
            txtHudBadge = GlassKit.Label(hudPill, "ChuHud", "● Quang hợp C3   +26.1   |   Khí khổng 100%", 11, TextMain, false, TextAnchor.MiddleCenter,
                new Vector2(10f, -4f), new Vector2(340f, 20f));

            floatingTools = MakeAnatomyPanel(root, "ThanhCongCu3D", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-346f, 0f), new Vector2(38f, 174f), SidebarBg, radius: 10f, hasShadow: true);

            btnAutoRotateTool = MakeAnatomyButton(floatingTools, "ToolTuXoay", "⟳", new Vector2(4f, -4f), new Vector2(30f, 30f),
                BatTatTuXoay360, false, 14, radius: 6f);
            MakeAnatomyButton(floatingTools, "ToolLayNet", "◎", new Vector2(4f, -38f), new Vector2(30f, 30f),
                BatTatLayNetBoPhan360, false, 13, radius: 6f);
            MakeAnatomyButton(floatingTools, "ToolPhongTo", "＋", new Vector2(4f, -72f), new Vector2(30f, 30f),
                () => OrbitCam?.ZoomBy(0.82f), false, 13, radius: 6f);
            MakeAnatomyButton(floatingTools, "ToolThuNho", "－", new Vector2(4f, -106f), new Vector2(30f, 30f),
                () => OrbitCam?.ZoomBy(1.22f), false, 13, radius: 6f);
            MakeAnatomyButton(floatingTools, "ToolDatLai", "↺", new Vector2(4f, -140f), new Vector2(30f, 30f),
                () => ChonKhungNhin(Cay3D != null ? Cay3D.KhungHienTai : KhungNhinThucVat.TongThe), false, 13, radius: 6f);

            var angleBar = MakeAnatomyPanel(root, "BangGocNhin360", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-18f, 10f), new Vector2(494f, 34f), SidebarBg, radius: 10f, hasShadow: true);

            (string label, float yaw, float pitch)[] presets360 =
            {
                ("Mặt trước", 20f, 16f),
                ("Bên phải", 110f, 16f),
                ("Mặt sau", 200f, 16f),
                ("Bên trái", 290f, 16f),
                ("Từ trên", 20f, 75f)
            };
            for (int i = 0; i < presets360.Length; i++)
            {
                var pr = presets360[i];
                angleChips[i] = MakeAnatomyButton(angleBar, $"Goc360_{i}", pr.label, new Vector2(4f + i * 76f, -4f), new Vector2(72f, 26f),
                    () => OrbitCam?.SetAnglePreset(pr.yaw, pr.pitch), false, 10, radius: 6f);
            }
            angleChips[5] = MakeAnatomyButton(angleBar, "Goc360_TuXoay", "⟳ Tự xoay 360°", new Vector2(384f, -4f), new Vector2(106f, 26f),
                BatTatTuXoay360, false, 10, radius: 6f, activeFill: BtnSelectedDark, activeRing: BorderBlue);

            lessonBar = MakeAnatomyPanel(root, "ThanhBaiGiang", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-18f, 50f), new Vector2(650f, 64f), SidebarBg, radius: 10f, hasShadow: true);
            txtLessonText = GlassKit.Label(lessonBar, "LoiDanBaiGiang", "", 11, TextMain, false, TextAnchor.UpperLeft,
                new Vector2(12f, -6f), new Vector2(472f, 52f));

            for (int i = 0; i < 5; i++)
            {
                int capI = i;
                stepDots[i] = MakeAnatomyButton(lessonBar, $"BuocDot_{i + 1}", $"{i + 1}",
                    new Vector2(494f + i * 29f, -8f), new Vector2(25f, 20f),
                    () => ApDungBuocBaiGiang(capI), i == 0, 10, radius: 5f, activeFill: BtnSelectedDark, activeRing: BorderBlue);
            }

            MakeAnatomyButton(lessonBar, "BuocTruoc", "‹ Trước", new Vector2(494f, -33f), new Vector2(68f, 23f),
                () => ApDungBuocBaiGiang(buocBaiGiang - 1), false, 10, radius: 6f);
            MakeAnatomyButton(lessonBar, "BuocSau", "Tiếp ›", new Vector2(568f, -33f), new Vector2(68f, 23f),
                () => ApDungBuocBaiGiang(buocBaiGiang + 1), true, 10, radius: 6f, activeFill: BtnPrimary, activeRing: BorderBlue);

            SetLayerRecursive(cvGo.transform, 5);
            UiMotion.Popup(this, topBar, null, 0.18f);
            UiMotion.Popup(this, leftSidebar, null, 0.20f);
            UiMotion.Popup(this, rightSidebar, null, 0.20f);
        }

        void BuildProcessExplanationPanel()
        {
            processPanel = GlassKit.Rect(rightSidebar, "BangGiaiThichQuaTrinh", new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(324f, 996f));

            txtProcHeader = GlassKit.Label(processPanel, "TenQuaTrinh", "Quá trình sinh lý 3D", 12, Color.white, true, TextAnchor.MiddleLeft,
                new Vector2(14f, -10f), new Vector2(246f, 22f));
            MakeAnatomyButton(processPanel, "NutDongQuaTrinh", "✕", new Vector2(264f, -9f), new Vector2(24f, 24f),
                DongQuaTrinh, false, 11, radius: 6f);

            var card = MakeAnatomyCard(processPanel, "TheGiaiThichTrong", new Vector2(10f, -38f), new Vector2(304f, 946f), radius: 10f);

            // 1. Lưới chuyển nhanh 6 quá trình sinh lý
            GlassKit.Label(card, "NhanChonQT", "CHỌN QUÁ TRÌNH SINH LÝ (6 MÔ PHỎNG)", 9, TextMuted, true, TextAnchor.MiddleLeft,
                new Vector2(12f, -8f), new Vector2(280f, 13f));

            var allProcs = ThucVatQuaTrinhData.DanhSach;
            for (int i = 0; i < 6 && i < allProcs.Length; i++)
            {
                string capMa = allProcs[i].Ma;
                int row = i / 2;
                int col = i % 2;
                processSwitchRightBtns[i] = MakeAnatomyButton(card, $"ChonQT_{capMa}", $"{allProcs[i].BieuTuong} {allProcs[i].TenNgan}",
                    new Vector2(12f + col * 143f, -25f - row * 26f), new Vector2(137f, 23f),
                    () => BatDauQuaTrinh(capMa, 0), false, 10, radius: 6f, activeFill: BtnSelectedDark, activeRing: BorderBlue);
            }

            // 2. Nhãn luồng + Đếm bước (giống gt-nhan-luu & gt-dem-buoc của Phòng Giải phẫu)
            var flowPillRt = GlassKit.Rect(card, "NhanLuongPill", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -110f), new Vector2(174f, 20f));
            procFlowBadgeImg = flowPillRt.gameObject.AddComponent<Image>();
            procFlowBadgeImg.sprite = MenuKit.Round(10f);
            procFlowBadgeImg.type = Image.Type.Sliced;
            procFlowBadgeImg.pixelsPerUnitMultiplier = 2f;
            procFlowBadgeImg.color = new Color(0.10f, 0.25f, 0.42f, 0.95f);
            procFlowBadgeImg.raycastTarget = false;
            txtProcFlowBadge = GlassKit.Label(flowPillRt, "ChuNhanLuong", "Dòng mạch gỗ ↑", 10, Color.white, true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(174f, 20f));

            txtProcCounter = GlassKit.Label(card, "DemBuocQT", "Bước 1/6", 11, BorderBlue, true, TextAnchor.MiddleRight,
                new Vector2(196f, -110f), new Vector2(96f, 20f));

            // 3. Tiêu đề bước & Nội dung chi tiết (Điều gì xảy ra -> Vì sao? -> Ghi nhớ -> Tóm tắt)
            txtProcStepTitle = GlassKit.Label(card, "TieuDeBuocQT", "", 14, Color.white, true, TextAnchor.UpperLeft,
                new Vector2(12f, -138f), new Vector2(280f, 44f));

            txtProcWhat = GlassKit.Label(card, "DieuGiXayRa", "", 11, TextMain, false, TextAnchor.UpperLeft,
                new Vector2(12f, -186f), new Vector2(280f, 140f));

            var whyBox = MakeAnatomyCard(card, "HopViSao", new Vector2(12f, -332f), new Vector2(280f, 128f), radius: 8f);
            whyBox.GetComponent<Image>().color = CardInsetBg;
            txtProcWhy = GlassKit.Label(whyBox, "ChuViSao", "", 11, TextSub, false, TextAnchor.UpperLeft,
                new Vector2(10f, -8f), new Vector2(260f, 112f));

            var remBox = MakeAnatomyCard(card, "HopGhiNho", new Vector2(12f, -468f), new Vector2(280f, 94f), radius: 8f);
            remBox.GetComponent<Image>().color = new Color(0.06f, 0.12f, 0.10f, 0.96f);
            txtProcRemember = GlassKit.Label(remBox, "ChuGhiNho", "", 11, TextMain, false, TextAnchor.UpperLeft,
                new Vector2(10f, -8f), new Vector2(260f, 78f));

            procSummaryBox = MakeAnatomyCard(card, "HopTomTatQT", new Vector2(12f, -570f), new Vector2(280f, 126f), radius: 8f);
            procSummaryBox.GetComponent<Image>().color = new Color(0.14f, 0.11f, 0.06f, 0.96f);
            txtProcSummary = GlassKit.Label(procSummaryBox, "ChuTomTatQT", "", 10, TextMain, false, TextAnchor.UpperLeft,
                new Vector2(10f, -8f), new Vector2(260f, 110f));
            procSummaryBox.gameObject.SetActive(false);

            // 4. Thanh đếm thời gian đọc tự động (Auto-play countdown)
            var countTrack = GlassKit.Rect(card, "ThanhDemNguoc", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -772f), new Vector2(280f, 4f));
            var countBg = countTrack.gameObject.AddComponent<Image>();
            countBg.sprite = MenuKit.Round(2f);
            countBg.type = Image.Type.Sliced;
            countBg.color = new Color(0.16f, 0.17f, 0.20f, 1f);
            var fillRt = new GameObject("Fill", typeof(RectTransform)).GetComponent<RectTransform>();
            fillRt.SetParent(countTrack, false);
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = new Vector2(0.02f, 1f);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            procCountdownFill = fillRt.gameObject.AddComponent<Image>();
            procCountdownFill.sprite = MenuKit.Round(2f);
            procCountdownFill.type = Image.Type.Sliced;
            procCountdownFill.color = BorderGreen;
            countTrack.gameObject.SetActive(false);

            // 5. Hàng chấm bước bấm chọn nhanh (1..6)
            GlassKit.Label(card, "NhanTienDo", "TIẾN ĐỘ CÁC BƯỚC (BẤM ĐỂ CHỌN)", 9, TextMuted, true, TextAnchor.MiddleLeft,
                new Vector2(12f, -784f), new Vector2(280f, 14f));
            procDotsContainer = GlassKit.Rect(card, "HangChamBuoc", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -802f), new Vector2(280f, 22f));

            // 6. Cụm nút điều khiển phát (◎ | ◀ | || Dừng / ▶ Phát | ▶| | ↺)
            MakeAnatomyButton(card, "GT_CanGoc", "◎", new Vector2(12f, -834f), new Vector2(42f, 34f), CanGocBuocHienTai, false, 14, radius: 7f);
            MakeAnatomyButton(card, "GT_Truoc", "◀", new Vector2(60f, -834f), new Vector2(46f, 34f), () => MuiTen3D?.PrevStep(), false, 13, radius: 7f);
            btnProcPlay = MakeAnatomyButton(card, "GT_Phat", "|| Dừng", new Vector2(112f, -834f), new Vector2(80f, 34f), () => MuiTen3D?.TogglePlayPause(), true, 11, radius: 8f, activeFill: BtnPrimary, activeRing: BorderBlue);
            MakeAnatomyButton(card, "GT_Sau", "▶|", new Vector2(198f, -834f), new Vector2(46f, 34f), () => MuiTen3D?.NextStep(), false, 13, radius: 7f);
            MakeAnatomyButton(card, "GT_Lap", "↺", new Vector2(250f, -834f), new Vector2(42f, 34f), () => MuiTen3D?.GoToStep(0), false, 14, radius: 7f);

            // 7. Hàng tốc độ (0.5× | 1× | 1.5× | 2×) & Tự chạy
            float[] spdVals = { 0.5f, 1f, 1.5f, 2f };
            string[] spdLbls = { "0.5×", "1×", "1.5×", "2×" };
            for (int i = 0; i < 4; i++)
            {
                float capSpd = spdVals[i];
                processSpeedBtns[i] = MakeAnatomyButton(card, $"GT_Spd_{i}", spdLbls[i], new Vector2(12f + i * 44f, -878f), new Vector2(40f, 24f),
                    () => MuiTen3D?.SetSpeed(capSpd), i == 1, 10, radius: 6f, activeFill: BtnSelectedDark, activeRing: BorderBlue);
            }
            btnProcAutoPlay = MakeAnatomyButton(card, "GT_TuChay", "Tự chạy: BẬT", new Vector2(192f, -878f), new Vector2(100f, 24f),
                () => MuiTen3D?.SetAutoPlay(!(MuiTen3D?.TuDongChay ?? true)), true, 10, radius: 6f, activeFill: BtnSelectedDark, activeRing: BorderGreen);

            GlassKit.Label(card, "GoiYPhimTat", "Phím tắt: Space (Phát/Dừng) · [ / ] (Bước trước/sau) · Esc (Đóng)", 9, TextMuted, false, TextAnchor.MiddleCenter,
                new Vector2(12f, -912f), new Vector2(280f, 16f));

            processPanel.gameObject.SetActive(false);
        }

        void RebuildPartSelectorRow(KhungNhinThucVat view)
        {
            if (partSelectorRow == null) return;
            for (int i = partSelectorRow.childCount - 1; i >= 0; i--)
                Destroy(partSelectorRow.GetChild(i).gameObject);
            partJumpBtns.Clear();

            string targetGroup = view switch
            {
                KhungNhinThucVat.LatCatRe => "lat_cat_re",
                KhungNhinThucVat.LatCatThan => "lat_cat_than",
                KhungNhinThucVat.LatCatLa => "lat_cat_la",
                _ => "tong_the"
            };

            var list = new List<(string id, string shortName)>();
            foreach (var bp in ThucVatMoPhong.DanhMucBoPhan)
            {
                if (bp.Nhom != targetGroup) continue;
                string sn = bp.Ma switch
                {
                    "re_chop_va_long_hut" => "● Rễ cọc & Nốt sần",
                    "than_cay" => "● Thân & Mạch dẫn",
                    "la_cay" => "● Lá kép gân mạng",
                    "hoa_dau" => "● Hoa mẫu 5 & Quả",
                    "re_long_hut" => "1. Lông hút",
                    "re_vo_nhu_mo" => "2. Mô mềm vỏ",
                    "re_dai_caspari" => "3. Đai Caspari",
                    "re_mach_go" => "4. Mạch gỗ sao",
                    "re_mach_ray" => "5. Mạch rây",
                    "than_bieu_bi" => "1. Biểu bì & Vỏ",
                    "than_mach_ray" => "2. Mạch rây ngoài",
                    "than_tang_sinh_mach" => "3. Tầng sinh mạch",
                    "than_mach_go" => "4. Mạch gỗ trong",
                    "la_bieu_bi_cutin" => "1. Cutin & Biểu bì",
                    "la_mo_giau_va_mo_xop" => "2. Mô giậu & Xốp",
                    "la_gan_bo_mach" => "3. Bó mạch gân lá",
                    "la_khi_khong" => "4. Khí khổng",
                    _ => bp.Ten
                };
                list.Add((bp.Ma, sn));
            }

            for (int i = 0; i < list.Count; i++)
            {
                string pid = list[i].id;
                int row = i / 2;
                int col = i % 2;
                bool fullWidth = (list.Count == 5 && i == 4);
                float w = fullWidth ? 248f : 121f;
                float x = fullWidth ? 0f : col * 127f;
                float y = -row * 25f;
                bool isSel = Cay3D != null && Cay3D.BoPhanDangChon == pid;
                var b = MakeAnatomyButton(partSelectorRow, "ChonBP_" + pid, list[i].shortName,
                    new Vector2(x, y), new Vector2(w, 22f), () => ChonBoPhan(pid), isSel, 10, radius: 6f,
                    activeFill: BtnSelectedDark, activeRing: BorderBlue);
                partJumpBtns.Add((b, pid));
            }

            SetLayerRecursive(partSelectorRow, 5);
        }

        Slider MakeSlimAnatomySlider(Transform parent, string name, string title, float y, float min, float max, float val,
                                     Action<float> onChanged, Func<float, string> fmt)
        {
            const float w = 248f;
            var row = GlassKit.Rect(parent, name, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, y), new Vector2(w, 34f));

            GlassKit.Label(row, "Tiêu đề", title, 10, TextSub, false, TextAnchor.MiddleLeft, new Vector2(0f, 0f), new Vector2(160f, 14f));
            float v0 = Mathf.Clamp(val, min, max);
            var valLbl = GlassKit.Label(row, "Giá trị", fmt(v0), 10, TextMain, true, TextAnchor.MiddleRight, new Vector2(150f, 0f), new Vector2(98f, 14f));

            var slRt = GlassKit.Rect(row, "ThanhTrượt", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -15f), new Vector2(w, 18f));
            var hit = slRt.gameObject.AddComponent<Image>();
            hit.sprite = GlassKit.Plain;
            hit.color = new Color(1f, 1f, 1f, 0.001f);

            var trackRt = new GameObject("Rãnh", typeof(RectTransform)).GetComponent<RectTransform>();
            trackRt.SetParent(slRt, false);
            trackRt.anchorMin = new Vector2(0f, 0.5f);
            trackRt.anchorMax = new Vector2(1f, 0.5f);
            trackRt.offsetMin = new Vector2(6f, -2f);
            trackRt.offsetMax = new Vector2(-6f, 2f);
            var trackImg = trackRt.gameObject.AddComponent<Image>();
            trackImg.sprite = MenuKit.Round(2f);
            trackImg.type = Image.Type.Sliced;
            trackImg.pixelsPerUnitMultiplier = 2f;
            trackImg.color = new Color(0.16f, 0.17f, 0.19f, 1f);
            trackImg.raycastTarget = false;

            var fillArea = new GameObject("VùngFill", typeof(RectTransform)).GetComponent<RectTransform>();
            fillArea.SetParent(slRt, false);
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.offsetMin = new Vector2(6f, -2f);
            fillArea.offsetMax = new Vector2(-6f, 2f);
            var fillRt = new GameObject("Fill", typeof(RectTransform)).GetComponent<RectTransform>();
            fillRt.SetParent(fillArea, false);
            fillRt.sizeDelta = Vector2.zero;
            var fillImg = fillRt.gameObject.AddComponent<Image>();
            fillImg.sprite = MenuKit.Round(2f);
            fillImg.type = Image.Type.Sliced;
            fillImg.pixelsPerUnitMultiplier = 2f;
            fillImg.color = BorderBlue;
            fillImg.raycastTarget = false;

            var handleArea = new GameObject("VùngNúm", typeof(RectTransform)).GetComponent<RectTransform>();
            handleArea.SetParent(slRt, false);
            handleArea.anchorMin = new Vector2(0f, 0.5f);
            handleArea.anchorMax = new Vector2(1f, 0.5f);
            handleArea.sizeDelta = new Vector2(-12f, 0f);
            handleArea.anchoredPosition = Vector2.zero;

            var handleRt = new GameObject("Núm", typeof(RectTransform)).GetComponent<RectTransform>();
            handleRt.SetParent(handleArea, false);
            handleRt.anchorMin = new Vector2(0f, 0.5f);
            handleRt.anchorMax = new Vector2(0f, 0.5f);
            handleRt.anchoredPosition = Vector2.zero;
            handleRt.sizeDelta = new Vector2(12f, 12f);
            var handleImg = handleRt.gameObject.AddComponent<Image>();
            handleImg.sprite = GlassKit.SolidCircle;
            handleImg.preserveAspect = true;
            handleImg.color = BorderBlue;

            var innerRt = GlassKit.Rect(handleRt, "LõiTrắng", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(7.5f, 7.5f));
            var innerImg = innerRt.gameObject.AddComponent<Image>();
            innerImg.sprite = GlassKit.SolidCircle;
            innerImg.preserveAspect = true;
            innerImg.color = Color.white;
            innerImg.raycastTarget = false;

            var sl = slRt.gameObject.AddComponent<Slider>();
            sl.navigation = new Navigation { mode = Navigation.Mode.None };
            sl.fillRect = fillRt;
            sl.handleRect = handleRt;
            sl.targetGraphic = handleImg;
            sl.direction = Slider.Direction.LeftToRight;
            sl.minValue = min;
            sl.maxValue = max;
            sl.SetValueWithoutNotify(v0);
            sl.onValueChanged.AddListener(v =>
            {
                valLbl.text = fmt(v);
                onChanged?.Invoke(v);
            });
            syncSliderLabels.Add(() => { if (valLbl != null && sl != null) valLbl.text = fmt(sl.value); });
            return sl;
        }

        static void BuildMeterBar(Transform parent, string label, float y, Color barColor, out Image fillImg, out Text valTxt)
        {
            GlassKit.Label(parent, "Lbl_" + label, label, 10, TextSub, false, TextAnchor.MiddleLeft,
                new Vector2(10f, y), new Vector2(108f, 15f));
            valTxt = GlassKit.Label(parent, "Val_" + label, "0", 10, barColor, true, TextAnchor.MiddleRight,
                new Vector2(202f, y), new Vector2(56f, 15f));

            var trackRt = GlassKit.Rect(parent, "Track_" + label, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(118f, y - 4f), new Vector2(82f, 7f));
            var trackImg = trackRt.gameObject.AddComponent<Image>();
            trackImg.sprite = MenuKit.Round(3.5f);
            trackImg.type = Image.Type.Sliced;
            trackImg.pixelsPerUnitMultiplier = 2f;
            trackImg.color = new Color(0.04f, 0.04f, 0.05f, 0.95f);
            trackImg.raycastTarget = false;

            var fillRt = new GameObject("Fill", typeof(RectTransform)).GetComponent<RectTransform>();
            fillRt.SetParent(trackRt, false);
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = new Vector2(0.5f, 1f);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            fillImg = fillRt.gameObject.AddComponent<Image>();
            fillImg.sprite = MenuKit.Round(3.5f);
            fillImg.type = Image.Type.Sliced;
            fillImg.pixelsPerUnitMultiplier = 2f;
            fillImg.color = barColor;
            fillImg.raycastTarget = false;
        }

        static RectTransform MakeAnatomyPanel(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size,
                                              Color bgCol, float radius = 12f, bool hasShadow = true)
        {
            var rt = GlassKit.Rect(parent, name, anchor, pivot, pos, size);

            if (hasShadow && radius > 0f)
            {
                var shRt = MenuKit.Stretch(rt, "Bóng nổi", Vector2.zero, Vector2.one, new Vector2(-8f, -12f), new Vector2(8f, 6f));
                var shImg = shRt.gameObject.AddComponent<Image>();
                shImg.sprite = MenuKit.ShadowSprite(12f, radius);
                shImg.type = Image.Type.Sliced;
                shImg.pixelsPerUnitMultiplier = 2f;
                shImg.color = new Color(0f, 0f, 0f, 0.55f);
                shImg.raycastTarget = false;
            }

            var faceImg = rt.gameObject.AddComponent<Image>();
            if (radius > 0f)
            {
                faceImg.sprite = MenuKit.Round(radius);
                faceImg.type = Image.Type.Sliced;
                faceImg.pixelsPerUnitMultiplier = 2f;
            }
            faceImg.color = bgCol;
            faceImg.raycastTarget = true;

            if (radius > 0f) MenuKit.Ring(rt, "Viền", BorderLine, radius);
            return rt;
        }

        static RectTransform MakeAnatomyCard(Transform parent, string name, Vector2 pos, Vector2 size, float radius = 10f)
        {
            var rt = GlassKit.Rect(parent, name, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = MenuKit.Round(radius);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 2f;
            img.color = CardBg;
            img.raycastTarget = false;
            MenuKit.Ring(rt, "Viền", BorderLine, radius);
            return rt;
        }

        static Button MakeAnatomyButton(Transform parent, string name, string text, Vector2 pos, Vector2 size, Action onClick,
                                        bool active = false, int fontSize = 11, float radius = 7f, Color? activeFill = null, Color? activeRing = null)
        {
            var rt = GlassKit.Rect(parent, name, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, size);

            var shRt = MenuKit.Stretch(rt, "Bóng", Vector2.zero, Vector2.one, new Vector2(-2f, -4f), new Vector2(2f, 1f));
            var sh = shRt.gameObject.AddComponent<Image>();
            sh.sprite = MenuKit.ShadowSprite(3f, radius);
            sh.type = Image.Type.Sliced;
            sh.pixelsPerUnitMultiplier = 2f;
            sh.color = new Color(0f, 0f, 0f, active ? 0.45f : 0.25f);
            sh.raycastTarget = false;

            Color fillCol = active ? (activeFill ?? BtnSelectedDark) : BtnNormal;
            var face = rt.gameObject.AddComponent<Image>();
            face.sprite = MenuKit.Round(radius);
            face.type = Image.Type.Sliced;
            face.pixelsPerUnitMultiplier = 2f;
            face.color = fillCol;
            face.raycastTarget = true;

            MenuKit.Sheen(rt, radius, active ? 0.14f : 0.06f);
            Color rCol = active ? (activeRing ?? BorderBlue) : BorderLine;
            var ring = MenuKit.Ring(rt, "Viền", rCol, radius);

            var glowRt = MenuKit.Full(rt, "Sáng");
            var glow = glowRt.gameObject.AddComponent<Image>();
            glow.sprite = MenuKit.Round(radius);
            glow.type = Image.Type.Sliced;
            glow.pixelsPerUnitMultiplier = 2f;
            glow.color = new Color(1f, 1f, 1f, 0f);
            glow.raycastTarget = false;

            var lbl = GlassKit.Label(rt, "Chữ", text, fontSize, active ? Color.white : TextSub,
                active, TextAnchor.MiddleCenter, Vector2.zero, size);
            lbl.horizontalOverflow = HorizontalWrapMode.Wrap;

            var b = rt.gameObject.AddComponent<Button>();
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            b.transition = Selectable.Transition.None;
            b.targetGraphic = face;
            if (onClick != null) b.onClick.AddListener(() => onClick());

            var hv = rt.gameObject.AddComponent<MenuHover>();
            hv.hoverScale = 1.022f;
            hv.pressScale = 0.955f;
            hv.shadow = sh;
            hv.shadowRest = sh.color.a;
            hv.shadowHover = Mathf.Min(0.68f, sh.color.a + 0.18f);
            hv.ring = ring;
            hv.ringRest = ring.color;
            hv.ringHover = BorderBlue;
            hv.tint = glow;
            hv.tintRest = new Color(1f, 1f, 1f, 0f);
            hv.tintHover = new Color(1f, 1f, 1f, 0.06f);

            return b;
        }

        static void SetAnatomyBtnState(Button b, bool active, Color? activeFill = null, Color? activeRing = null)
        {
            if (b == null) return;
            var face = b.GetComponent<Image>();
            if (face != null) face.color = active ? (activeFill ?? BtnSelectedDark) : BtnNormal;

            var ring = b.transform.Find("Viền")?.GetComponent<Image>();
            Color ringCol = active ? (activeRing ?? BorderBlue) : BorderLine;
            if (ring != null) ring.color = ringCol;

            var sh = b.transform.Find("Bóng")?.GetComponent<Image>();
            float shAlpha = active ? 0.45f : 0.25f;
            if (sh != null) sh.color = new Color(0f, 0f, 0f, shAlpha);

            var hv = b.GetComponent<MenuHover>();
            if (hv != null)
            {
                hv.ringRest = ringCol;
                hv.shadowRest = shAlpha;
                hv.shadowHover = Mathf.Min(0.68f, shAlpha + 0.18f);
            }

            var txt = b.transform.Find("Chữ")?.GetComponent<Text>() ?? b.GetComponentInChildren<Text>();
            if (txt != null)
            {
                txt.color = active ? Color.white : TextSub;
                txt.font = active ? GlassKit.Semibold : GlassKit.Regular;
            }
        }

        static void SetLayerRecursive(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++)
                SetLayerRecursive(t.GetChild(i), layer);
        }
    }
}
