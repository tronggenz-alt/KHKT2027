using System;
using Khkt.Demo;
using Khkt.Environment;
using Khkt.Rendering;
using Khkt.Simulation.Bio.ThucVat;
using UnityEngine;
using UnityEngine.EventSystems;
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
    /// Phần thực thi nội bộ của mô-đun 3D Thực vật (G3) đặt trong Khkt.Demo.Bio.ThucVat
    /// để tránh xung đột tên với MenuKit.Bio trong MainMenu.cs. Cầu nối Khkt.Bio.ThucVat.ThucVatMoDun nằm trong Bridge/.
    /// </summary>
    public static class ThucVatMoDunImpl
    {
        static ThucVatTrinhDieuKhien controller;

        public static bool DangMo => controller != null;
        public static ThucVatTrinhDieuKhien HienTai => controller;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => controller = null;

        public static void Mo()
        {
            if (controller != null) return;
            var host = new GameObject("ThucVatMoDun_Host");
            UnityEngine.Object.DontDestroyOnLoad(host);
            controller = host.AddComponent<ThucVatTrinhDieuKhien>();
            controller.Init();
        }

        public static void Dong()
        {
            if (controller == null) return;
            var go = controller.gameObject;
            controller.CleanupBeforeDestroy();
            controller = null;
            if (go != null) UnityEngine.Object.Destroy(go);
        }
    }

    /// <summary>
    /// Điều phối sân khấu 3D, camera quỹ đạo, giao diện thanh trượt thời gian thực, 3 lát cắt,
    /// chế độ Bài giảng (5 bước), Tự do và Câu hỏi (5 câu) cho mô-đun Thực vật.
    /// </summary>
    public sealed class ThucVatTrinhDieuKhien : MonoBehaviour
    {
        DieuKienMoiTruong dk = DieuKienMoiTruong.MacDinh;
        TrangThaiThucVat trangThai;
        CheDoThucVat cheDo = CheDoThucVat.TuDo;
        int buocBaiGiang;
        int cauHoiIdx;
        int cauHoiDung;
        int cauHoiDaTraLoi;
        float dayTimeScale = 1f;
        bool autoDayCycle;
        double gioTrongNgay = 10.5;

        public DieuKienMoiTruong DieuKien => dk;
        public TrangThaiThucVat TrangThai => trangThai;
        public CayHaiLaMam3D Cay3D { get; private set; }
        public CameraQuyDao OrbitCam { get; private set; }
        public Camera StageCamera { get; private set; }
        public Canvas UiCanvas { get; private set; }

        Camera prevMainCam;
        bool prevMainCamEnabled;
        CursorLockMode prevCursorLock;
        bool prevCursorVisible;
        GameObject tempEventSystem;

        Slider slLight, slCo2, slTemp, slRh, slSoil, slWind, slLampAngle;
        bool suppressSliderCallbacks;

        Button[] modeChips = new Button[3];
        Button[] viewChips = new Button[4];
        Button[] speedChips = new Button[3];
        Button btnAnno, btnCycle;
        Text txtMetrics, txtCardTitle, txtCardBody, txtLessonText, txtQuizStatus;
        RectTransform cardPanel, quizPanel, lessonBar;
        Text txtQuizQuestion, txtQuizFeedback;
        readonly Button[] quizOptBtns = new Button[4];
        readonly Text[] quizOptTexts = new Text[4];

        public void Init()
        {
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

            // Sân khấu 3D & camera riêng trên Layer 28
            var stageGo = new GameObject("SanKhauThucVat_3D");
            stageGo.transform.SetParent(transform, false);

            var camGo = new GameObject("CameraSanKhauThucVat") { layer = CayHaiLaMam3D.StageLayer };
            camGo.transform.SetParent(transform, false);
            StageCamera = camGo.AddComponent<Camera>();
            StageCamera.clearFlags = CameraClearFlags.SolidColor;
            StageCamera.backgroundColor = new Color(0.055f, 0.088f, 0.125f, 1f);
            StageCamera.cullingMask = 1 << CayHaiLaMam3D.StageLayer;
            StageCamera.nearClipPlane = 0.03f;
            StageCamera.farClipPlane = 45f;
            StageCamera.depth = 60f;

            OrbitCam = camGo.AddComponent<CameraQuyDao>();
            OrbitCam.TargetCamera = StageCamera;
            OrbitCam.PickLayerMask = 1 << CayHaiLaMam3D.StageLayer;
            OrbitCam.OnEscapePressed += ThucVatMoDunImpl.Dong;
            OrbitCam.OnPartClicked += OnPartClicked;
            OrbitCam.OnPartDoubleClicked += OnPartDoubleClicked;

            Cay3D = stageGo.AddComponent<CayHaiLaMam3D>();
            Cay3D.Initialize(StageCamera.transform);

            BuildUi();
            ChonCheDo(CheDoThucVat.BaiGiang);
            ChonKhungNhin(KhungNhinThucVat.TongThe, instantCamera: true);
            ChonBoPhan("than_cay");
            CapNhatMoPhong(0.016f);
        }

        public void CleanupBeforeDestroy()
        {
            if (prevMainCam != null) prevMainCam.enabled = prevMainCamEnabled;
            Cursor.lockState = prevCursorLock;
            Cursor.visible = prevCursorVisible;
            if (tempEventSystem != null) Destroy(tempEventSystem);
        }

        public void DatDieuKien(DieuKienMoiTruong moi)
        {
            dk = moi;
            SyncSlidersFromState();
            CapNhatMoPhong(0f);
        }

        public void SetAnhSang(float lux) { dk.AnhSangLux = Mathf.Clamp(lux, 0f, 60000f); SyncSlidersFromState(); CapNhatMoPhong(0f); }
        public void SetCo2(float ppm) { dk.Co2Ppm = Mathf.Clamp(ppm, 0f, 1200f); SyncSlidersFromState(); CapNhatMoPhong(0f); }
        public void SetNhietDo(float c) { dk.NhietDoC = Mathf.Clamp(c, 5f, 45f); SyncSlidersFromState(); CapNhatMoPhong(0f); }
        public void SetDoAmKhongKhi(float pct) { dk.DoAmKhongKhiPct = Mathf.Clamp(pct, 10f, 98f); SyncSlidersFromState(); CapNhatMoPhong(0f); }
        public void SetDoAmDat(float pct) { dk.DoAmDatPct = Mathf.Clamp(pct, 5f, 100f); SyncSlidersFromState(); CapNhatMoPhong(0f); }
        public void SetGio(float w) { dk.Gio01 = Mathf.Clamp01(w); SyncSlidersFromState(); CapNhatMoPhong(0f); }
        public void SetGocDen(float deg) { dk.GocDenHuongSangDo = Mathf.Clamp(deg, -75f, 75f); SyncSlidersFromState(); CapNhatMoPhong(0f); }
        public void SetSoGioHuongSang(float hrs) { dk.SoGioHuongSang = Mathf.Clamp(hrs, 0f, 72f); CapNhatMoPhong(0f); }

        public void ChonKhungNhin(KhungNhinThucVat view, bool instantCamera = false)
        {
            if (Cay3D == null) return;
            Cay3D.SetKhungNhin(view);
            Cay3D.GetDefaultCameraPose(view, out var pivot, out float yaw, out float pitch, out float dist);
            OrbitCam?.SetTarget(pivot, yaw, pitch, dist, instantCamera);
            for (int i = 0; i < viewChips.Length; i++)
                if (viewChips[i] != null) GlassKit.SetChip(viewChips[i], i == (int)view);
        }

        public void ChonCheDo(CheDoThucVat mode)
        {
            cheDo = mode;
            for (int i = 0; i < modeChips.Length; i++)
                if (modeChips[i] != null) GlassKit.SetChip(modeChips[i], i == (int)mode);

            if (lessonBar != null) lessonBar.gameObject.SetActive(mode == CheDoThucVat.BaiGiang);
            if (cardPanel != null) cardPanel.gameObject.SetActive(mode != CheDoThucVat.CauHoi);
            if (quizPanel != null) quizPanel.gameObject.SetActive(mode == CheDoThucVat.CauHoi);

            if (mode == CheDoThucVat.BaiGiang)
                ApDungBuocBaiGiang(buocBaiGiang);
            else if (mode == CheDoThucVat.CauHoi)
                HienCauHoi(cauHoiIdx);
        }

        public void ChonBoPhan(string partId)
        {
            var info = ThucVatMoPhong.TimBoPhan(partId);
            if (info == null) return;
            Cay3D?.SelectPart(partId);
            if (txtCardTitle != null) txtCardTitle.text = info.Ten;
            if (txtCardBody != null)
            {
                txtCardBody.text =
                    $"<color={GlassKit.HexAccent}><b>Cấu tạo:</b></color> {info.CauTao}\n\n" +
                    $"<color={GlassKit.HexGood}><b>Chức năng:</b></color> {info.ChucNang}\n\n" +
                    $"<color={GlassKit.HexWarn}><b>Câu hỏi nhanh:</b></color> {info.CauHoiNhanh}\n" +
                    $"<color={GlassKit.HexDim}><i>Trả lời: {info.DapAn}</i></color>\n\n" +
                    $"<color={GlassKit.HexDim}><size=12>Nguồn: {info.Nguon}</size></color>";
            }
        }

        void OnPartClicked(Collider col)
        {
            if (col == null) return;
            var tag = col.GetComponent<ThucVatPartTag>() ?? col.GetComponentInParent<ThucVatPartTag>();
            if (tag != null) ChonBoPhan(tag.PartId);
        }

        void OnPartDoubleClicked(Collider col)
        {
            if (col == null || Cay3D == null) return;
            var tag = col.GetComponent<ThucVatPartTag>() ?? col.GetComponentInParent<ThucVatPartTag>();
            if (tag == null) return;
            if (tag.PartId == "re_chop_va_long_hut") { ChonKhungNhin(KhungNhinThucVat.LatCatRe); ChonBoPhan("re_dai_caspari"); }
            else if (tag.PartId == "than_cay") { ChonKhungNhin(KhungNhinThucVat.LatCatThan); ChonBoPhan("than_tang_sinh_mach"); }
            else if (tag.PartId == "la_cay") { ChonKhungNhin(KhungNhinThucVat.LatCatLa); ChonBoPhan("la_khi_khong"); }
        }

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            if (autoDayCycle)
            {
                gioTrongNgay = (gioTrongNgay + dt * dayTimeScale / 3600.0) % 24.0;
                float sunRad = (float)((gioTrongNgay - 6.0) / 12.0 * Math.PI);
                float sunSin = Mathf.Max(0f, Mathf.Sin(sunRad));
                dk.AnhSangLux = sunSin > 0.02f ? 42000.0 * Math.Pow(sunSin, 1.2) : 0.0;
                dk.GocDenHuongSangDo = (gioTrongNgay >= 6.0 && gioTrongNgay <= 18.0) ? ((12.0 - gioTrongNgay) / 6.0) * 65.0 : 0.0;
                SyncSlidersFromState();
            }

            if (Math.Abs(dk.GocDenHuongSangDo) > 1.0 && dk.AnhSangLux >= 150.0 && dayTimeScale > 1f)
            {
                dk.SoGioHuongSang = Math.Min(72.0, dk.SoGioHuongSang + dt * (dayTimeScale / 180.0));
            }
            else if (dk.AnhSangLux < 150.0 && dayTimeScale > 1f)
            {
                dk.SoGioHuongSang = Math.Min(72.0, dk.SoGioHuongSang + dt * (dayTimeScale / 120.0));
            }

            CapNhatMoPhong(dt);
        }

        void CapNhatMoPhong(float dt)
        {
            trangThai = ThucVatMoPhong.TinhToan(dk);
            Cay3D?.ApplySimulation(dk, trangThai, dt, dayTimeScale);
            RefreshMetricsText();
        }

        void RefreshMetricsText()
        {
            if (txtMetrics == null) return;
            string cheDoNgay = trangThai.BanDem ? $"<color={GlassKit.HexWarn}>Ban đêm (Chỉ hô hấp)</color>"
                : (dk.AnhSangLux <= ThucVatMoPhong.DiemBuAnhSangLux ? $"<color={GlassKit.HexWarn}>Dưới điểm bù</color>" : $"<color={GlassKit.HexGood}>Ban ngày (Quang hợp)</color>");

            txtMetrics.text =
                $"<b>{cheDoNgay}</b>  |  Tua: ×{dayTimeScale:0}" + (autoDayCycle ? $"  ({Mathf.FloorToInt((float)gioTrongNgay):00}:{Mathf.FloorToInt((float)((gioTrongNgay % 1) * 60)):00})" : "") + "\n" +
                $"• Quang hợp thực: <b>{trangThai.CuongDoQuangHopRong:F1}</b> (thô {trangThai.CuongDoQuangHopTho:F1} | hô hấp {trangThai.CuongDoHoHap:F1})\n" +
                $"• Thoát hơi nước / mạch gỗ: <b>{trangThai.TocDoThoatHoiNuocMmPhut:F2} mm/phút</b>\n" +
                $"• Độ mở khí khổng: <b>{trangThai.DoMoKhiKhong01 * 100:F0}%</b> (ABA: {trangThai.NongDoAba01 * 100:F0}%)\n" +
                $"• Dòng đường mạch rây: <b>{trangThai.TocDoMachRay * 100:F0}%</b>\n" +
                $"• Hướng sáng uốn thân: <b>{trangThai.GocUonThanDo:F1}°</b> (Auxin tối {trangThai.TiLeAuxinPhiaToi * 100:F0}% / sáng {trangThai.TiLeAuxinPhiaSang * 100:F0}%)";
        }

        void SyncSlidersFromState()
        {
            suppressSliderCallbacks = true;
            slLight?.SetValueWithoutNotify((float)dk.AnhSangLux);
            slCo2?.SetValueWithoutNotify((float)dk.Co2Ppm);
            slTemp?.SetValueWithoutNotify((float)dk.NhietDoC);
            slRh?.SetValueWithoutNotify((float)dk.DoAmKhongKhiPct);
            slSoil?.SetValueWithoutNotify((float)dk.DoAmDatPct);
            slWind?.SetValueWithoutNotify((float)dk.Gio01 * 100f);
            slLampAngle?.SetValueWithoutNotify((float)dk.GocDenHuongSangDo);
            suppressSliderCallbacks = false;
        }

        void ApDungBuocBaiGiang(int idx)
        {
            var steps = ThucVatMoPhong.DanhSachBaiGiang;
            buocBaiGiang = (idx % steps.Length + steps.Length) % steps.Length;
            var s = steps[buocBaiGiang];
            if (txtLessonText != null)
                txtLessonText.text = $"<b>{s.TieuDe}</b>\n{s.NoiDung}";

            KhungNhinThucVat view = s.KhungNhin switch
            {
                "lat_cat_re" => KhungNhinThucVat.LatCatRe,
                "lat_cat_than" => KhungNhinThucVat.LatCatThan,
                "lat_cat_la" => KhungNhinThucVat.LatCatLa,
                _ => KhungNhinThucVat.TongThe
            };
            ChonKhungNhin(view);
            ChonBoPhan(s.BoPhanNoiBat);
        }

        void HienCauHoi(int idx)
        {
            var qs = ThucVatMoPhong.DanhSachCauHoi;
            cauHoiIdx = (idx % qs.Length + qs.Length) % qs.Length;
            var q = qs[cauHoiIdx];
            if (txtQuizQuestion != null) txtQuizQuestion.text = $"<b>Câu {cauHoiIdx + 1}/{qs.Length}:</b> {q.Cau}";
            if (txtQuizFeedback != null) txtQuizFeedback.text = "";
            for (int i = 0; i < 4; i++)
            {
                if (quizOptTexts[i] != null)
                    quizOptTexts[i].text = (char)('A' + i) + ". " + q.LuaChon[i];
                if (quizOptBtns[i] != null) GlassKit.SetChip(quizOptBtns[i], false);
            }
            if (txtQuizStatus != null)
                txtQuizStatus.text = $"Điểm hiện tại: <b>{cauHoiDung}/{cauHoiDaTraLoi}</b>";
        }

        void TraLoiCauHoi(int choice)
        {
            var q = ThucVatMoPhong.DanhSachCauHoi[cauHoiIdx];
            cauHoiDaTraLoi++;
            bool ok = choice == q.DapAnDung;
            if (ok) cauHoiDung++;
            for (int i = 0; i < 4; i++)
                if (quizOptBtns[i] != null) GlassKit.SetChip(quizOptBtns[i], i == q.DapAnDung);
            if (txtQuizFeedback != null)
            {
                string col = ok ? GlassKit.HexGood : GlassKit.HexBad;
                string head = ok ? "Chính xác!" : $"Chưa đúng — đáp án đúng là {(char)('A' + q.DapAnDung)}.";
                txtQuizFeedback.text = $"<color={col}><b>{head}</b></color>\n{q.GiaiThich}";
            }
            if (txtQuizStatus != null)
                txtQuizStatus.text = $"Điểm hiện tại: <b>{cauHoiDung}/{cauHoiDaTraLoi}</b>";
        }

        // ---------------------------------------------------------------------
        // Dựng giao diện Overlay toàn màn hình bằng GlassKit
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
            scaler.matchWidthOrHeight = 0f; // Giữ đủ chiều rộng 1920 trên cả màn 16:10 (Mac Retina) và 16:9
            cvGo.AddComponent<GraphicRaycaster>();

            Transform root = cvGo.transform;

            // 1. Thanh tiêu đề trên cùng
            var topBar = MakeSurfacePanel(root, "ThanhTren", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(1876f, 64f));
            GlassKit.Label(topBar, "TieuDe", "MÔ HÌNH 3D THỰC VẬT — CÂY ĐẬU HAI LÁ MẦM", 18, Color.white, true, TextAnchor.MiddleLeft,
                new Vector2(20f, -10f), new Vector2(460f, 44f));

            // 3 chế độ dạy học
            string[] modeNames = { "Bài giảng (5 bước)", "Tự do", "Câu hỏi (5 câu)" };
            for (int i = 0; i < 3; i++)
            {
                int capI = i;
                modeChips[i] = GlassKit.MakeChip(topBar, $"CheDo_{i}", modeNames[i], new Vector2(490f + i * 164f, -10f), new Vector2(156f, 44f),
                    () => ChonCheDo((CheDoThucVat)capI), i == 0, 14);
            }

            // 4 nút khung nhìn
            string[] viewNames = { "Tổng thể", "Lát cắt Rễ", "Lát cắt Thân", "Lát cắt Lá" };
            for (int i = 0; i < 4; i++)
            {
                int capI = i;
                viewChips[i] = GlassKit.MakeChip(topBar, $"KhungNhin_{i}", viewNames[i], new Vector2(1010f + i * 130f, -10f), new Vector2(124f, 44f),
                    () => ChonKhungNhin((KhungNhinThucVat)capI), i == 0, 14);
            }

            btnAnno = GlassKit.MakeChip(topBar, "NutChuThich", "Chú thích: BẬT", new Vector2(1545f, -10f), new Vector2(146f, 44f), () =>
            {
                bool next = Cay3D == null || !Cay3D.HienChuThich;
                Cay3D?.SetHienChuThich(next);
                var lbl = btnAnno.GetComponentInChildren<Text>();
                if (lbl != null) lbl.text = next ? "Chú thích: BẬT" : "Chú thích: TẮT";
                GlassKit.SetChip(btnAnno, next);
            }, true, 14);

            GlassKit.MakeButton(topBar, "NutDong", "Đóng (Esc)", new Vector2(1706f, -10f), new Vector2(150f, 44f), ThucVatMoDunImpl.Dong, true, 15);

            // 2. Bảng trái: 7 thanh trượt môi trường + tốc độ tua + thông số thời gian thực
            var leftPanel = MakeSurfacePanel(root, "BangMoiTruong", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f, -20f), new Vector2(420f, 820f));
            GlassKit.Label(leftPanel, "TieuDeTrai", "ĐIỀU KIỆN MÔI TRƯỜNG & SINH LÝ", 15, GlassKit.Accent, true, TextAnchor.MiddleLeft,
                new Vector2(18f, -12f), new Vector2(384f, 28f));

            float y = -46f;
            slLight = MakeDarkSlider(leftPanel, "SlLight", "Ánh sáng (lux)", y, 0f, 55000f, (float)dk.AnhSangLux,
                v => { if (!suppressSliderCallbacks) { dk.AnhSangLux = v; } }, v => $"{v:0} lux"); y -= 64f;
            slCo2 = MakeDarkSlider(leftPanel, "SlCo2", "Nồng độ CO₂ (ppm)", y, 0f, 1200f, (float)dk.Co2Ppm,
                v => { if (!suppressSliderCallbacks) { dk.Co2Ppm = v; } }, v => $"{v:0} ppm"); y -= 64f;
            slTemp = MakeDarkSlider(leftPanel, "SlTemp", "Nhiệt độ (°C)", y, 5f, 45f, (float)dk.NhietDoC,
                v => { if (!suppressSliderCallbacks) { dk.NhietDoC = v; } }, v => $"{v:0.0} °C"); y -= 64f;
            slRh = MakeDarkSlider(leftPanel, "SlRh", "Độ ẩm không khí (%)", y, 10f, 98f, (float)dk.DoAmKhongKhiPct,
                v => { if (!suppressSliderCallbacks) { dk.DoAmKhongKhiPct = v; } }, v => $"{v:0}%"); y -= 64f;
            slSoil = MakeDarkSlider(leftPanel, "SlSoil", "Độ ẩm đất (%)", y, 5f, 100f, (float)dk.DoAmDatPct,
                v => { if (!suppressSliderCallbacks) { dk.DoAmDatPct = v; } }, v => $"{v:0}%"); y -= 64f;
            slWind = MakeDarkSlider(leftPanel, "SlWind", "Tốc độ gió (%)", y, 0f, 100f, (float)dk.Gio01 * 100f,
                v => { if (!suppressSliderCallbacks) { dk.Gio01 = v / 100f; } }, v => $"{v:0}%"); y -= 64f;
            slLampAngle = MakeDarkSlider(leftPanel, "SlLamp", "Góc đèn hướng sáng (°)", y, -70f, 70f, (float)dk.GocDenHuongSangDo,
                v => { if (!suppressSliderCallbacks) { dk.GocDenHuongSangDo = v; } }, v => $"{v:+0;-0;0}°"); y -= 70f;

            // Tua nhanh ngày đêm ×1, ×60, ×600
            float[] spds = { 1f, 60f, 600f };
            for (int i = 0; i < 3; i++)
            {
                float sp = spds[i];
                int capI = i;
                speedChips[i] = GlassKit.MakeChip(leftPanel, $"Tua_{sp}", $"×{sp:0}", new Vector2(18f + i * 74f, y), new Vector2(68f, 44f), () =>
                {
                    dayTimeScale = sp;
                    for (int k = 0; k < 3; k++) GlassKit.SetChip(speedChips[k], k == capI);
                }, i == 0, 14);
            }
            btnCycle = GlassKit.MakeChip(leftPanel, "ChuKyNgayDem", "Ngày/đêm: TẮT", new Vector2(246f, y), new Vector2(156f, 44f), () =>
            {
                autoDayCycle = !autoDayCycle;
                var lbl = btnCycle.GetComponentInChildren<Text>();
                if (lbl != null) lbl.text = autoDayCycle ? "Ngày/đêm: BẬT" : "Ngày/đêm: TẮT";
                GlassKit.SetChip(btnCycle, autoDayCycle);
            }, false, 14);
            y -= 56f;

            txtMetrics = GlassKit.Label(leftPanel, "ChiSoSinhLy", "", 13, new Color(0.90f, 0.94f, 0.98f), false, TextAnchor.UpperLeft,
                new Vector2(18f, y), new Vector2(384f, 180f));

            // 3. Cột phải: Thẻ kiến thức & Bảng câu hỏi
            cardPanel = MakeSurfacePanel(root, "TheKienThuc", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-22f, -10f), new Vector2(400f, 520f));
            GlassKit.Label(cardPanel, "NhanThe", "THẺ KIẾN THỨC BỘ PHẬN", 14, GlassKit.Accent, true, TextAnchor.MiddleLeft,
                new Vector2(18f, -12f), new Vector2(364f, 26f));
            txtCardTitle = GlassKit.Label(cardPanel, "TenBoPhan", "", 18, Color.white, true, TextAnchor.UpperLeft,
                new Vector2(18f, -44f), new Vector2(364f, 48f));
            txtCardBody = GlassKit.Label(cardPanel, "NoiDungThe", "", 14, new Color(0.90f, 0.94f, 0.98f), false, TextAnchor.UpperLeft,
                new Vector2(18f, -96f), new Vector2(364f, 340f));

            GlassKit.MakeButton(cardPanel, "MoLatCatRe", "Lát cắt Rễ", new Vector2(18f, -456f), new Vector2(114f, 44f),
                () => { ChonKhungNhin(KhungNhinThucVat.LatCatRe); ChonBoPhan("re_dai_caspari"); }, false, 13);
            GlassKit.MakeButton(cardPanel, "MoLatCatThan", "Lát cắt Thân", new Vector2(142f, -456f), new Vector2(114f, 44f),
                () => { ChonKhungNhin(KhungNhinThucVat.LatCatThan); ChonBoPhan("than_tang_sinh_mach"); }, false, 13);
            GlassKit.MakeButton(cardPanel, "MoLatCatLa", "Lát cắt Lá", new Vector2(266f, -456f), new Vector2(114f, 44f),
                () => { ChonKhungNhin(KhungNhinThucVat.LatCatLa); ChonBoPhan("la_khi_khong"); }, false, 13);

            // Bảng Câu hỏi (5 câu)
            quizPanel = MakeSurfacePanel(root, "BangCauHoi", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-22f, -10f), new Vector2(420f, 600f));
            GlassKit.Label(quizPanel, "TieuDeQuiz", "KIỂM TRA NHANH SINH HỌC THỰC VẬT", 15, GlassKit.Accent, true, TextAnchor.MiddleLeft,
                new Vector2(18f, -12f), new Vector2(384f, 28f));
            txtQuizStatus = GlassKit.Label(quizPanel, "DiemQuiz", "Điểm hiện tại: 0/0", 14, GlassKit.Good, true, TextAnchor.MiddleLeft,
                new Vector2(18f, -42f), new Vector2(384f, 24f));
            txtQuizQuestion = GlassKit.Label(quizPanel, "NoiDungCauHoi", "", 15, Color.white, false, TextAnchor.UpperLeft,
                new Vector2(18f, -72f), new Vector2(384f, 96f));
            for (int i = 0; i < 4; i++)
            {
                int capI = i;
                quizOptBtns[i] = GlassKit.MakeChip(quizPanel, $"DapAn_{i}", "", new Vector2(18f, -178f - i * 66f), new Vector2(384f, 56f),
                    () => TraLoiCauHoi(capI), false, 13);
                quizOptTexts[i] = quizOptBtns[i].GetComponentInChildren<Text>();
                if (quizOptTexts[i] != null) quizOptTexts[i].alignment = TextAnchor.MiddleLeft;
            }
            txtQuizFeedback = GlassKit.Label(quizPanel, "GiaiThichQuiz", "", 13, new Color(0.92f, 0.94f, 0.98f), false, TextAnchor.UpperLeft,
                new Vector2(18f, -448f), new Vector2(384f, 88f));
            GlassKit.MakeButton(quizPanel, "CauTruoc", "Câu trước", new Vector2(18f, -542f), new Vector2(184f, 44f),
                () => HienCauHoi(cauHoiIdx - 1), false, 14);
            GlassKit.MakeButton(quizPanel, "CauSau", "Câu tiếp theo →", new Vector2(218f, -542f), new Vector2(184f, 44f),
                () => HienCauHoi(cauHoiIdx + 1), true, 14);

            // 4. Thanh dưới: Lời dẫn chế độ Bài giảng (5 bước)
            lessonBar = MakeSurfacePanel(root, "ThanhBaiGiang", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(20f, 18f), new Vector2(1010f, 108f));
            txtLessonText = GlassKit.Label(lessonBar, "LoiDanBaiGiang", "", 14, Color.white, false, TextAnchor.UpperLeft,
                new Vector2(18f, -12f), new Vector2(730f, 84f));
            GlassKit.MakeButton(lessonBar, "BuocTruoc", "← Bước trước", new Vector2(762f, -32f), new Vector2(114f, 44f),
                () => ApDungBuocBaiGiang(buocBaiGiang - 1), false, 14);
            GlassKit.MakeButton(lessonBar, "BuocSau", "Bước sau →", new Vector2(884f, -32f), new Vector2(110f, 44f),
                () => ApDungBuocBaiGiang(buocBaiGiang + 1), true, 14);

            SetLayerRecursive(cvGo.transform, 5);
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
            img.color = new Color(0.06f, 0.095f, 0.145f, 0.88f);
            return img.rectTransform;
        }

        static Slider MakeDarkSlider(Transform parent, string name, string title, float y, float min, float max, float val, Action<float> onChanged, Func<float, string> fmt)
        {
            var s = GlassKit.MakeSlider(parent, name, title, new Vector2(18f, y), new Vector2(384f, 58f), min, max, val, onChanged, fmt);
            var titleTxt = s.transform.parent.Find("Tiêu đề")?.GetComponent<Text>();
            if (titleTxt != null) titleTxt.color = new Color(0.92f, 0.95f, 0.99f);
            return s;
        }
    }
}
