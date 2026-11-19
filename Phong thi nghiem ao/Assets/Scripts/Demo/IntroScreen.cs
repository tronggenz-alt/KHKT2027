using System;
using System.Collections;
using System.Collections.Generic;
using Khkt.Environment;
using Khkt.Environment.Tang2;
using Khkt.Menu;
using Khkt.Rendering;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static Khkt.Demo.GlassKit;

namespace Khkt.Demo
{
    /// <summary>
    /// Màn hình giới thiệu, chọn chế độ và cài đặt hệ thống VERA Lab HD (Giai đoạn 5):
    ///   • Trang chính lưới 2×2 với 4 chế độ (Thí nghiệm tự do, Chuẩn độ, Khám phá phòng lab, Bài demo).
    ///   • Bảng Cài đặt hợp nhất 5 nhóm bên trái (Hiển thị, Âm thanh, Điều khiển, Trợ lý AI, Máy chủ).
    ///   • 100% nút bấm đạt chuẩn Apple Touch Target (>= 44x44 pt).
    ///   • Tích hợp vào thẳng phòng lab từ Menu chính (GameSession.EnterLabPending + KHKT_PENDING_STATION).
    /// </summary>
    public class IntroScreen : MonoBehaviour
    {
        public ModernUI ui;
        public LabProcedure procedure;
        public DesktopViewer viewer;
        public bool showOnStart = true;

        public bool IsOpen { get; private set; }

        const string PendingStationPrefKey = "KHKT_PENDING_STATION";
        const string TouchSensitivityPrefKey = "KHKT_TOUCH_SENSITIVITY";
        const float HomeW = 1240f, HomeH = 800f;
        const float SettingsW = 1020f, SettingsH = 700f;

        struct ModeCardUi
        {
            public Image border;
            public Image bg;
            public GameObject badge;
            public Action launch;
        }

        struct ThemeCardUi
        {
            public UiTheme.Mode mode;
            public Image border;
            public Text status;
        }

        GameObject root, home, setup, settings, themeConfirmDialog, continueBtnObj;
        Text setupNote, aiSaveStatus, serverSaveStatus, guideText, themeConfirmDetail, touchSensValueLabel;
        Text graphicsHardwareLabel, graphicsStatusLabel;
        Toggle soundToggle, motionToggle, transToggle;
        Button clickPickChip, clickPanelChip, benchmark30Btn;
        Slider touchSensSlider;
        InputField aiUrlInput, serverUrlInput;

        readonly ModeCardUi[] modeCards = new ModeCardUi[4];
        readonly Button[] settingsTabs = new Button[5];
        readonly GameObject[] settingsPages = new GameObject[5];
        readonly ThemeCardUi[] themeCards = new ThemeCardUi[3];
        readonly Button[] fontScaleChips = new Button[3];
        readonly Button[] panelSizeChips = new Button[3];
        readonly Button[] graphicsChips = new Button[5];
        string graphicsBenchmarkMsg = "";

        UiTheme.Mode pendingTheme;
        bool captured, started, glassOk;
        int selectedMode;
        int activeSettingsTab;
        string analyte = "HCl", titrant = "NaOH", indicator = "pp";
        float analyteC;
        readonly Dictionary<string, List<(Button b, string v)>> groups = new Dictionary<string, List<(Button, string)>>();

        void Start()
        {
            BuildUi();
            UiTheme.Changed += OnThemeChanged;
            HardwareProfile.Changed += OnHardwareProfileChanged;

            if (Application.isPlaying)
            {
                bool fromMenu = GameSession.EnterLabPending;
                GameSession.EnterLabPending = false;
                if (fromMenu)
                {
                    StartCoroutine(EnterLab());
                    return;
                }
            }

            if (showOnStart && !Application.isBatchMode)
                Open();
            else if (root != null)
                root.SetActive(false);
        }

        void OnDestroy()
        {
            UiTheme.Changed -= OnThemeChanged;
            HardwareProfile.Changed -= OnHardwareProfileChanged;
            if (captured) UiBridge.SetCapture(ref captured, false);
        }

        void OnHardwareProfileChanged() => RefreshGraphicsProfileUi();

        void OnThemeChanged()
        {
            if (this == null || ui == null || ui.Canvas == null) return;
            bool wasOpen = IsOpen;
            bool inSettings = settings != null && settings.activeSelf;
            bool inSetup = setup != null && setup.activeSelf;

            if (root != null)
            {
                if (Application.isPlaying) Destroy(root);
                else DestroyImmediate(root);
                root = null;
            }

            BuildUi();
            if (continueBtnObj != null) continueBtnObj.SetActive(started);
            if (wasOpen)
            {
                root.SetActive(true);
                root.transform.SetAsLastSibling();
                if (inSettings)
                {
                    home.SetActive(false);
                    setup.SetActive(false);
                    settings.SetActive(true);
                    SelectSettingsTab(activeSettingsTab);
                    RefreshSettingsFields();
                }
                else if (inSetup)
                {
                    home.SetActive(false);
                    setup.SetActive(true);
                    if (settings != null) settings.SetActive(false);
                }
            }
            else if (root != null)
            {
                root.SetActive(false);
            }
        }

        void BuildUi()
        {
            var canvas = ui != null && ui.Canvas != null ? ui.Canvas : HostCanvas(transform, 45);
            UiBridge.EnsureInteractive(canvas);
            var shader = ui != null && ui.glassShader != null ? ui.glassShader : Shader.Find("Khkt/KinhMo");
            glassOk = shader != null && canvas != null && canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceCamera;
            var glass = glassOk ? Glass(shader) : null;

            var dimRt = Rect(canvas.transform, "Bảng tạm dừng", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4000, 3000));
            var dim = dimRt.gameObject.AddComponent<Image>();
            dim.sprite = Plain;
            dim.color = Scrim(0.55f);
            root = dimRt.gameObject;

            BuildHome(dimRt, glass);
            BuildSettings(dimRt, glass);
            BuildSetup(dimRt, glass);
            Refresh();
        }

        // =====================================================================
        // Trang chính: lưới 2×2 bốn chế độ
        // =====================================================================

        void BuildHome(RectTransform dimRt, Material glass)
        {
            var hImg = Panel(dimRt, "Trang chính", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(HomeW, HomeH), glass, 1.1f);
            ApplyPanelFallback(hImg, 1.1f, Surface);
            home = hImg.gameObject;
            var h = home.transform;

            Label(h, "Nhãn", "KHKT · DỰ ÁN NGHIÊN CỨU KHOA HỌC KỸ THUẬT QUỐC GIA", 13, Accent, true, TextAnchor.UpperLeft, new Vector2(52, -32), new Vector2(820, 22));
            Label(h, "Tên", "VERA Lab — Phòng Thí nghiệm Hóa học & Sinh học Ảo", 34, Ink, true, TextAnchor.UpperLeft, new Vector2(50, -56), new Vector2(1140, 48));
            Label(h, "Mô tả", "Thực hành khoa học an toàn với 720 hóa chất, 108 dụng cụ sinh học và 14 máy phân tích trong tòa nhà 2 tầng 600 m². Mọi phản ứng hóa - sinh được mô phỏng toán lý chính xác; robot AI hỗ trợ thời gian thực.",
                  15, Dim, false, TextAnchor.UpperLeft, new Vector2(52, -110), new Vector2(1136, 44));
            Rule(h, new Vector2(52, -166), 1136);

            BuildModeCard(h, 0, "01 · TỰ DO", "Thí nghiệm tự do",
                "Tự lấy hóa chất trong kho, pha chế, đun, đo pH và lắp ghép dụng cụ. Bộ chuẩn độ sẵn sàng trên bàn.",
                "Bắt đầu", StartFree);
            BuildModeCard(h, 1, "02 · CHUẨN ĐỘ", "Chuẩn độ",
                "Tự chọn chất cần chuẩn, dung dịch chuẩn trên buret, chỉ thị màu và nồng độ ẩn hay biết trước.",
                "Chọn bài", ShowSetup);
            BuildModeCard(h, 2, "03 · THAM QUAN", "Khám phá phòng lab",
                "Tham quan 13 khu thí nghiệm: tủ hút, kho hóa chất, máy phân tích tầng 1 và khu sinh học tầng 2.",
                "Tham quan", StartExplore);
            BuildModeCard(h, 3, "04 · MẪU 7 BÀI", "Bài demo",
                "7 bài ngắn Hóa – Sinh (chuẩn độ, UV-Vis, kính hiển vi, PCR, điện di, giải phẫu 3D) kèm robot làm mẫu.",
                "Mở danh sách", StartDemoShowcase);

            Rule(h, new Vector2(52, -668), 1136);
            guideText = Label(h, "Phím", GetControlGuidanceText(), 14, Dim, false, TextAnchor.MiddleLeft, new Vector2(52, -680), new Vector2(1136, 26));

            MakeButton(h, "Cài đặt", "Cài đặt", new Vector2(52, -720), new Vector2(164, 50), ShowSettings, false, 15);
            // 19/11: rời lab về sảnh đầu (bảng điều khiển hoặc màn chọn 2 luồng), luôn hỏi lại
            if (VeSanh.CoTheVe)
                MakeButton(h, "Nút Về sảnh", "← Về sảnh", new Vector2(232, -720), new Vector2(164, 50), MoHoiVeSanh, false, 15);
            continueBtnObj = MakeButton(h, "Tiếp tục", "Tiếp tục", new Vector2(762, -720), new Vector2(160, 50), Close, false, 15).gameObject;
            continueBtnObj.SetActive(false);
            MakeButton(h, "Vào phòng thí nghiệm", "Vào phòng thí nghiệm", new Vector2(936, -720), new Vector2(252, 50), LaunchSelectedMode, true, 16);

            SelectModeCard(selectedMode);
        }

        void BuildModeCard(Transform parent, int index, string code, string title, string desc, string ctaLabel, Action launchAction)
        {
            int col = index % 2;
            int row = index / 2;
            const float cardW = 556f, cardH = 224f;
            float x = 52f + col * (cardW + 24f);
            float y = -186f - row * (cardH + 18f);
            float cornerScale = UiTheme.IsVera ? 1.5f : 2.6f;

            var cardRt = Rect(parent, "Chế độ " + index, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, y), new Vector2(cardW, cardH));
            var borderImg = cardRt.gameObject.AddComponent<Image>();
            borderImg.sprite = Solid;
            borderImg.type = Image.Type.Sliced;
            borderImg.pixelsPerUnitMultiplier = cornerScale;
            borderImg.color = Line;

            var cardBtn = cardRt.gameObject.AddComponent<Button>();
            cardBtn.transition = Selectable.Transition.None;
            cardBtn.navigation = new Navigation { mode = Navigation.Mode.None };
            int idx = index;
            cardBtn.onClick.AddListener(() => SelectModeCard(idx));
            NutHieuUng.Attach(cardRt.gameObject);

            var bgRt = new GameObject("Nền", typeof(RectTransform)).GetComponent<RectTransform>();
            bgRt.SetParent(cardRt, false);
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = new Vector2(2f, 2f);
            bgRt.offsetMax = new Vector2(-2f, -2f);
            var bgImg = bgRt.gameObject.AddComponent<Image>();
            bgImg.sprite = Solid;
            bgImg.type = Image.Type.Sliced;
            bgImg.pixelsPerUnitMultiplier = cornerScale;
            bgImg.color = Well;
            bgImg.raycastTarget = false;

            Label(cardRt, "Mã", code, 13, Accent2, true, TextAnchor.UpperLeft, new Vector2(24, -20), new Vector2(360, 22));

            var badgeRt = Rect(cardRt, "Đang chọn", new Vector2(0, 1), new Vector2(0, 1), new Vector2(cardW - 132f, -16f), new Vector2(108f, 28f));
            var badgeBg = badgeRt.gameObject.AddComponent<Image>();
            badgeBg.sprite = Solid;
            badgeBg.type = Image.Type.Sliced;
            badgeBg.pixelsPerUnitMultiplier = UiTheme.IsVera ? 3f : 4f;
            badgeBg.color = Color.Lerp(Well, Accent, UiTheme.IsLight ? 0.16f : 0.24f);
            badgeBg.raycastTarget = false;
            Label(badgeRt, "Chữ", "Đang chọn", 12, Accent, true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(108f, 28f));

            Label(cardRt, "Tên", title, 22, Ink, true, TextAnchor.UpperLeft, new Vector2(24, -50), new Vector2(cardW - 48f, 32));
            Label(cardRt, "Mô tả", desc, 14, Dim, false, TextAnchor.UpperLeft, new Vector2(24, -88), new Vector2(cardW - 48f, 64));

            MakeButton(cardRt, "Nút", ctaLabel, new Vector2(24, -160), new Vector2(180, 46), () =>
            {
                SelectModeCard(idx);
                launchAction?.Invoke();
            }, false, 15);

            modeCards[index] = new ModeCardUi
            {
                border = borderImg,
                bg = bgImg,
                badge = badgeRt.gameObject,
                launch = launchAction
            };
        }

        void SelectModeCard(int index)
        {
            selectedMode = Mathf.Clamp(index, 0, modeCards.Length - 1);
            for (int i = 0; i < modeCards.Length; i++)
            {
                bool on = i == selectedMode;
                if (modeCards[i].border != null) modeCards[i].border.color = on ? Accent : Line;
                if (modeCards[i].bg != null)
                    modeCards[i].bg.color = on ? Color.Lerp(Well, Accent, UiTheme.IsLight ? 0.08f : 0.14f) : Well;
                if (modeCards[i].badge != null) modeCards[i].badge.SetActive(on);
            }
        }

        void LaunchSelectedMode()
        {
            int idx = Mathf.Clamp(selectedMode, 0, modeCards.Length - 1);
            modeCards[idx].launch?.Invoke();
        }

        // =====================================================================
        // Cài đặt hợp nhất 5 nhóm (bên trái: danh mục; bên phải: chi tiết)
        // =====================================================================

        void BuildSettings(RectTransform dimRt, Material glass)
        {
            var setImg = Panel(dimRt, "Cài đặt", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(SettingsW, SettingsH), glass, 1.1f);
            ApplyPanelFallback(setImg, 1.1f, Surface);
            settings = setImg.gameObject;
            var st = settings.transform;

            Label(st, "Tiêu đề", "Cài đặt & Tùy chọn", 26, Ink, true, TextAnchor.UpperLeft, new Vector2(36, -24), new Vector2(640, 34));
            Label(st, "Gợi ý", "Chỉnh giao diện, âm thanh, điều khiển cảm ứng và địa chỉ máy chủ.", 14, Dim, false, TextAnchor.UpperLeft, new Vector2(36, -60), new Vector2(760, 22));
            Rule(st, new Vector2(36, -92), SettingsW - 72f);

            // Thanh danh mục bên trái
            string[] tabLabels = { "Hiển thị", "Âm thanh", "Điều khiển", "Trợ lý AI", "Máy chủ" };
            for (int i = 0; i < tabLabels.Length; i++)
            {
                int tabIdx = i;
                var b = MakeChip(st, "Tab " + tabLabels[i], tabLabels[i], new Vector2(36, -114 - i * 56f), new Vector2(212, 48), () => SelectSettingsTab(tabIdx), i == activeSettingsTab, 15);
                var t = b.transform.Find("Chữ")?.GetComponent<Text>();
                if (t != null)
                {
                    t.alignment = TextAnchor.MiddleLeft;
                    t.rectTransform.anchoredPosition = new Vector2(16, 0);
                    t.rectTransform.sizeDelta = new Vector2(180, 48);
                }
                settingsTabs[i] = b;
            }

            MakeButton(st, "Đóng cài đặt", "Quay lại", new Vector2(36, -620), new Vector2(212, 48), CloseSettings, false, 15);
            Fill(st, "Vạch chia dọc", new Vector2(266, -112), new Vector2(1.5f, 556), Line);

            // Vùng nội dung bên phải
            const float paneW = 694f, paneH = 556f;
            var paneRt = Rect(st, "Nội dung cài đặt", new Vector2(0, 1), new Vector2(0, 1), new Vector2(290, -112), new Vector2(paneW, paneH));

            BuildSettingsDisplayTab(paneRt, paneW, paneH);
            BuildSettingsAudioTab(paneRt, paneW, paneH);
            BuildSettingsControlsTab(paneRt, paneW, paneH);
            BuildSettingsAiTab(paneRt, paneW, paneH);
            BuildSettingsServerTab(paneRt, paneW, paneH);

            SelectSettingsTab(activeSettingsTab);
            BuildThemeConfirmDialog(st);
            settings.SetActive(false);
        }

        RectTransform NewSettingsPage(Transform pane, int idx, string name, float w, float h)
        {
            var rt = Rect(pane, name, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(w, h));
            settingsPages[idx] = rt.gameObject;
            return rt;
        }

        void BuildSettingsDisplayTab(Transform pane, float w, float h)
        {
            var p = NewSettingsPage(pane, 0, "Tab Hiển thị", w, h);
            Label(p, "Mục Giao diện", "Giao diện phòng thí nghiệm", 16, Ink, true, TextAnchor.UpperLeft, new Vector2(0, 0), new Vector2(w, 22));

            BuildPreviewThemeCard(p, 0, UiTheme.Mode.XanhMuc, "Xanh mực VERA", "Tối xanh kỹ thuật",
                HexColor(0x0a1322), HexColor(0x111d31), HexColor(0xeaf1ff), HexColor(0x93a7c4), HexColor(0x33d6ff), HexColor(0xffb84d));
            BuildPreviewThemeCard(p, 1, UiTheme.Mode.Trang, "Trắng", "Sáng ban ngày",
                HexColor(0xf5f5f7), HexColor(0xffffff), HexColor(0x1d1d1f), HexColor(0x5c5d66), HexColor(0x0b63d1), HexColor(0x9a5600));
            BuildPreviewThemeCard(p, 2, UiTheme.Mode.Den, "Đen", "Tương phản cao",
                HexColor(0x000000), HexColor(0x151517), HexColor(0xf5f5f7), HexColor(0xa1a1a8), HexColor(0x4da3ff), HexColor(0xffb84d));

            Rule(p, new Vector2(0, -144), w);

            Label(p, "Nhãn Cỡ chữ", "Cỡ chữ:", 15, Ink, true, TextAnchor.MiddleLeft, new Vector2(0, -154), new Vector2(176, 44));
            string[] fontLabels = { "Chuẩn", "Lớn", "Rất lớn" };
            for (int i = 0; i < fontLabels.Length; i++)
            {
                int mode = i;
                fontScaleChips[i] = MakeChip(p, "CoChu_" + i, fontLabels[i], new Vector2(188f + i * 172f, -154), new Vector2(162, 44),
                    () => SelectFontScale(mode), UiTheme.FontScaleMode == mode, 14);
            }

            Label(p, "Nhãn Cỡ bảng", "Cỡ bảng dụng cụ:", 15, Ink, true, TextAnchor.MiddleLeft, new Vector2(0, -206), new Vector2(176, 44));
            string[] panelLabels = { "60%", "75%", "Kín màn hình" };
            for (int i = 0; i < panelLabels.Length; i++)
            {
                int mode = i;
                panelSizeChips[i] = MakeChip(p, "CoBang_" + i, panelLabels[i], new Vector2(188f + i * 172f, -206), new Vector2(162, 44),
                    () => SelectPanelSize(mode), UiTheme.PanelSizeMode == mode, 14);
            }

            Rule(p, new Vector2(0, -258), w);
            Label(p, "Mục Đồ họa", "Chất lượng đồ họa", 16, Ink, true, TextAnchor.UpperLeft, new Vector2(0, -268), new Vector2(w, 22));
            graphicsHardwareLabel = Label(p, "Thông tin phần cứng", HardwareProfile.MoTaMay(), 13, Dim, false, TextAnchor.UpperLeft, new Vector2(0, -292), new Vector2(w, 20));

            string[] gfxLabels = { "Tự động", "Thấp", "Vừa", "Cao", "Siêu nét" };
            const float chipW = 132f, chipGap = 8f;
            for (int i = 0; i < gfxLabels.Length; i++)
            {
                int idx = i;
                graphicsChips[i] = MakeChip(p, "DoHoa_" + i, gfxLabels[i], new Vector2(i * (chipW + chipGap), -318), new Vector2(chipW, 44),
                    () => SelectGraphicsLevel(idx), IsGraphicsChipSelected(idx), 14);
            }

            benchmark30Btn = MakeButton(p, "DoThu30s", "Đo thử 30 giây", new Vector2(0, -370), new Vector2(204, 44), Run30sBenchmarkFromSettings, false, 14);
            graphicsStatusLabel = Label(p, "Trạng thái đồ họa", "", 13, Accent, false, TextAnchor.MiddleLeft, new Vector2(216, -370), new Vector2(w - 216f, 44));

            Rule(p, new Vector2(0, -422), w);
            Label(p, "Mục Hiển thị", "Khả năng đọc & chuyển động", 15, Ink, true, TextAnchor.UpperLeft, new Vector2(0, -432), new Vector2(w, 20));
            transToggle = MakeToggle(p, "ToggleTrans", "Nền đặc chống lóa (tắt hiệu ứng kính mờ)", new Vector2(0, -456), new Vector2(w, 44),
                GlassKit.ReducedTransparency, v => GlassKit.ReducedTransparency = v);
            motionToggle = MakeToggle(p, "ToggleMotion", "Giảm chuyển động (Reduced Motion)", new Vector2(0, -504), new Vector2(w, 44),
                ReducedMotion.IsEnabled, v => ReducedMotion.IsEnabled = v);

            RefreshGraphicsProfileUi();
        }

        static Color HexColor(int rgb) =>
            new Color(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f, 1f);

        void BuildPreviewThemeCard(Transform parent, int index, UiTheme.Mode mode, string label, string subtitle,
            Color previewBg, Color previewSurface, Color previewInk, Color previewDim, Color previewAccent, Color previewAccent2)
        {
            const float cardW = 220f, cardH = 112f;
            float x = index * (cardW + 17f);
            float corner = UiTheme.IsVera ? 1.8f : 2.6f;

            var cardRt = Rect(parent, "Thẻ nền " + mode, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -26), new Vector2(cardW, cardH));
            var borderImg = cardRt.gameObject.AddComponent<Image>();
            borderImg.sprite = Solid;
            borderImg.type = Image.Type.Sliced;
            borderImg.pixelsPerUnitMultiplier = corner;
            borderImg.color = UiTheme.Current == mode ? Accent : Line;

            var btn = cardRt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            btn.onClick.AddListener(() => RequestThemeSwitch(mode));
            NutHieuUng.Attach(cardRt.gameObject);

            var innerRt = new GameObject("Lõi", typeof(RectTransform)).GetComponent<RectTransform>();
            innerRt.SetParent(cardRt, false);
            innerRt.anchorMin = Vector2.zero;
            innerRt.anchorMax = Vector2.one;
            innerRt.offsetMin = new Vector2(2.5f, 2.5f);
            innerRt.offsetMax = new Vector2(-2.5f, -2.5f);
            var innerImg = innerRt.gameObject.AddComponent<Image>();
            innerImg.sprite = Solid;
            innerImg.type = Image.Type.Sliced;
            innerImg.pixelsPerUnitMultiplier = corner;
            innerImg.color = previewBg;
            innerImg.raycastTarget = false;

            // Khung mẫu thu nhỏ bên trong thẻ xem trước
            var miniBox = Rect(innerRt, "Mẫu", new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -8), new Vector2(cardW - 25f, 44f));
            var miniBg = miniBox.gameObject.AddComponent<Image>();
            miniBg.sprite = Solid;
            miniBg.type = Image.Type.Sliced;
            miniBg.pixelsPerUnitMultiplier = mode == UiTheme.Mode.XanhMuc ? 2.4f : 3.2f;
            miniBg.color = previewSurface;
            miniBg.raycastTarget = false;

            Fill(miniBox, "Thanh nhấn", new Vector2(10, -8), new Vector2(58, 5), previewAccent);
            Fill(miniBox, "Dòng 1", new Vector2(10, -19), new Vector2(132, 4), new Color(previewInk.r, previewInk.g, previewInk.b, 0.72f));
            Fill(miniBox, "Dòng 2", new Vector2(10, -29), new Vector2(96, 4), new Color(previewDim.r, previewDim.g, previewDim.b, 0.65f));

            // 3 chấm bảng màu đặc trưng
            void Swatch(string n, float sx, Color c)
            {
                var sw = Rect(innerRt, n, new Vector2(0, 1), new Vector2(0, 1), new Vector2(sx, -57), new Vector2(24, 7));
                var im = sw.gameObject.AddComponent<Image>();
                im.sprite = Solid;
                im.type = Image.Type.Sliced;
                im.pixelsPerUnitMultiplier = 6f;
                im.color = c;
                im.raycastTarget = false;
            }
            Swatch("Màu 1", 10, previewAccent);
            Swatch("Màu 2", 38, previewAccent2);
            Swatch("Màu 3", 66, previewInk);

            Label(innerRt, "Tên", label, 14, previewInk, true, TextAnchor.UpperLeft, new Vector2(10, -68), new Vector2(cardW - 25f, 20));
            var statusLbl = Label(innerRt, "Trạng thái", UiTheme.Current == mode ? "Đang dùng" : subtitle, 12,
                UiTheme.Current == mode ? previewAccent : previewDim, UiTheme.Current == mode, TextAnchor.UpperLeft, new Vector2(10, -89), new Vector2(cardW - 25f, 18));

            themeCards[index] = new ThemeCardUi
            {
                mode = mode,
                border = borderImg,
                status = statusLbl
            };
        }

        void BuildSettingsAudioTab(Transform pane, float w, float h)
        {
            var p = NewSettingsPage(pane, 1, "Tab Âm thanh", w, h);
            Label(p, "Mục Âm thanh", "Âm thanh phản hồi", 18, Ink, true, TextAnchor.UpperLeft, new Vector2(0, -4), new Vector2(w, 26));
            Label(p, "Ghi chú Âm thanh", "Bật hoặc tắt tiếng phản hồi nhẹ khi chạm vào nút và công tắc giao diện.", 14, Dim, false, TextAnchor.UpperLeft, new Vector2(0, -32), new Vector2(w, 22));
            soundToggle = MakeToggle(p, "ToggleSound", "Âm thanh phản hồi nút bấm", new Vector2(0, -72), new Vector2(w, 46),
                NutHieuUng.SoundEnabled, v => NutHieuUng.SoundEnabled = v);
        }

        void BuildSettingsControlsTab(Transform pane, float w, float h)
        {
            var p = NewSettingsPage(pane, 2, "Tab Điều khiển", w, h);
            Label(p, "Mục Dụng cụ", "Thao tác với dụng cụ", 18, Ink, true, TextAnchor.UpperLeft, new Vector2(0, -4), new Vector2(w, 26));
            Label(p, "Ghi chú Dụng cụ", "Chọn hành động mặc định khi bấm chuột hoặc chạm vào dụng cụ và thiết bị trong phòng lab.", 14, Dim, false, TextAnchor.UpperLeft, new Vector2(0, -32), new Vector2(w, 22));

            Label(p, "Nhãn Bấm dụng cụ", "Bấm vào dụng cụ:", 15, Ink, true, TextAnchor.MiddleLeft, new Vector2(0, -72), new Vector2(180, 46));
            int clickMode = PlayerPrefs.GetInt(UiTheme.ClickModePrefKey, 0);
            clickPickChip = MakeChip(p, "ClickCamNgay", "Cầm ngay", new Vector2(196, -72), new Vector2(240, 46), () => SetClickMode(0), clickMode == 0, 15);
            clickPanelChip = MakeChip(p, "ClickMoBang", "Mở bảng", new Vector2(452, -72), new Vector2(242, 46), () => SetClickMode(1), clickMode == 1, 15);

            Rule(p, new Vector2(0, -140), w);
            Label(p, "Mục Cảm ứng", "Điều khiển cảm ứng (iPad)", 18, Ink, true, TextAnchor.UpperLeft, new Vector2(0, -158), new Vector2(w, 26));

            float initSens = Mathf.Clamp(PlayerPrefs.GetFloat(TouchSensitivityPrefKey, 0.18f), 0.05f, 0.50f);
            touchSensSlider = MakeSlider(p, "TouchSensitivity", "Độ nhạy vuốt nhìn (iPad)", new Vector2(0, -198), new Vector2(w, 64),
                0.05f, 0.50f, initSens, OnTouchSensitivityChanged, v => v.ToString("0.00"));
            if (touchSensSlider != null && touchSensSlider.transform.parent != null)
                touchSensValueLabel = touchSensSlider.transform.parent.Find("Giá trị")?.GetComponent<Text>();
            Label(p, "Ghi chú Độ nhạy", "Chỉnh tốc độ xoay góc nhìn khi vuốt nửa phải màn hình cảm ứng (0,05 – 0,50).", 13, Dim, false, TextAnchor.UpperLeft, new Vector2(0, -270), new Vector2(w, 20));
        }

        void BuildSettingsAiTab(Transform pane, float w, float h)
        {
            var p = NewSettingsPage(pane, 3, "Tab Trợ lý AI", w, h);
            Label(p, "Mục AI", "Máy chủ trợ lý AI", 18, Ink, true, TextAnchor.UpperLeft, new Vector2(0, -4), new Vector2(w, 26));
            Label(p, "Ghi chú AI", "Địa chỉ máy chủ backend phục vụ hỏi đáp hóa - sinh và điều khiển robot trợ lý.", 14, Dim, false, TextAnchor.UpperLeft, new Vector2(0, -32), new Vector2(w, 22));
            Label(p, "Nhãn AI", "Địa chỉ máy chủ AI (trợ lý & robot):", 14, Dim, true, TextAnchor.UpperLeft, new Vector2(0, -76), new Vector2(w, 22));

            aiUrlInput = MakeInput(p, "Ô nhập AI URL", "http://127.0.0.1:8000", new Vector2(0, -104), new Vector2(534, 48));
            MakeButton(p, "Lưu AI", "Lưu", new Vector2(550, -104), new Vector2(144, 48), SaveSettings, true, 16);
            aiSaveStatus = Label(p, "Trạng thái AI", "", 14, Good, false, TextAnchor.UpperLeft, new Vector2(0, -166), new Vector2(w, 26));
        }

        void BuildSettingsServerTab(Transform pane, float w, float h)
        {
            var p = NewSettingsPage(pane, 4, "Tab Máy chủ", w, h);
            Label(p, "Mục Máy chủ", "Máy chủ tài khoản & lớp học", 18, Ink, true, TextAnchor.UpperLeft, new Vector2(0, -4), new Vector2(w, 26));
            Label(p, "Ghi chú Máy chủ", "Địa chỉ máy chủ tài khoản, bài tập, điểm số và phòng học chung. Giáo viên sẽ cung cấp nếu đổi.", 14, Dim, false, TextAnchor.UpperLeft, new Vector2(0, -32), new Vector2(w, 22));
            Label(p, "Nhãn Máy chủ", "Địa chỉ máy chủ:", 14, Dim, true, TextAnchor.UpperLeft, new Vector2(0, -76), new Vector2(w, 22));

            serverUrlInput = MakeInput(p, "Ô nhập Server URL", GameSession.DefaultServerUrl, new Vector2(0, -104), new Vector2(534, 48));
            MakeButton(p, "Lưu máy chủ", "Lưu", new Vector2(550, -104), new Vector2(144, 48), SaveAccountServerSettings, true, 16);

            MakeButton(p, "Máy chủ trực tuyến", "Máy chủ trực tuyến", new Vector2(0, -166), new Vector2(339, 46), () => FillAccountServerUrl(GameSession.CloudServerUrl), false, 15);
            MakeButton(p, "Máy chủ trên máy này", "Máy chủ trên máy này", new Vector2(355, -166), new Vector2(339, 46), () => FillAccountServerUrl(GameSession.LocalServerUrl), false, 15);
            serverSaveStatus = Label(p, "Trạng thái máy chủ", "", 14, Good, false, TextAnchor.UpperLeft, new Vector2(0, -226), new Vector2(w, 26));
        }

        void SelectSettingsTab(int index)
        {
            activeSettingsTab = Mathf.Clamp(index, 0, settingsTabs.Length - 1);
            for (int i = 0; i < settingsTabs.Length; i++)
            {
                if (settingsTabs[i] != null) SetChip(settingsTabs[i], i == activeSettingsTab);
                if (settingsPages[i] != null) settingsPages[i].SetActive(i == activeSettingsTab);
            }
        }

        void ApplyPanelFallback(Image img, float scale, Color fallbackBg)
        {
            if (img == null || glassOk || ReducedTransparency) return;
            img.sprite = Solid;
            img.pixelsPerUnitMultiplier = UiTheme.IsVera ? scale : 2.8f;
            img.color = fallbackBg;
        }

        GameObject hoiVeSanh;
        Text hoiVeSanhNoiDung;

        public bool DangHoiVeSanh => hoiVeSanh != null && hoiVeSanh.activeSelf;

        /// <summary>Hộp hỏi "Về sảnh": Ở lại / Về màn chọn ban đầu / Về bảng điều khiển (hoặc Về sảnh nếu chưa đăng nhập).</summary>
        public void MoHoiVeSanh()
        {
            if (hoiVeSanh == null) BuildHoiVeSanh(home.transform.parent);
            hoiVeSanhNoiDung.text = VeSanh.DangLamDo()
                ? "Bài đang làm dở sẽ mất. Bạn vẫn muốn rời phòng lab?"
                : "Rời phòng lab và quay về sảnh đầu?";
            hoiVeSanh.SetActive(true);
            hoiVeSanh.transform.SetAsLastSibling();
        }

        public void DongHoiVeSanh() { if (hoiVeSanh != null) hoiVeSanh.SetActive(false); }

        void XacNhanVeSanh(bool veManChon)
        {
            DongHoiVeSanh();
            IsOpen = false;
            UiBridge.SetCapture(ref captured, false);
            VeSanh.Ve(veManChon);
        }

        void BuildHoiVeSanh(Transform parent)
        {
            var scrimRt = new GameObject("Hỏi về sảnh", typeof(RectTransform)).GetComponent<RectTransform>();
            scrimRt.SetParent(parent, false);
            scrimRt.anchorMin = Vector2.zero;
            scrimRt.anchorMax = Vector2.one;
            scrimRt.offsetMin = scrimRt.offsetMax = Vector2.zero;
            var scrimImg = scrimRt.gameObject.AddComponent<Image>();
            scrimImg.sprite = Plain;
            scrimImg.color = Scrim(0.62f);
            scrimImg.raycastTarget = true;

            float cornerScale = UiTheme.IsVera ? 1.2f : 2.6f;
            var borderRt = Rect(scrimRt, "Viền hộp thoại", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640, 240));
            var borderImg = borderRt.gameObject.AddComponent<Image>();
            borderImg.sprite = Solid;
            borderImg.type = Image.Type.Sliced;
            borderImg.pixelsPerUnitMultiplier = cornerScale;
            borderImg.color = Line;

            var cardRt = new GameObject("Khung hộp thoại", typeof(RectTransform)).GetComponent<RectTransform>();
            cardRt.SetParent(borderRt, false);
            cardRt.anchorMin = Vector2.zero;
            cardRt.anchorMax = Vector2.one;
            cardRt.offsetMin = new Vector2(1.5f, 1.5f);
            cardRt.offsetMax = new Vector2(-1.5f, -1.5f);
            var cardImg = cardRt.gameObject.AddComponent<Image>();
            cardImg.sprite = Solid;
            cardImg.type = Image.Type.Sliced;
            cardImg.pixelsPerUnitMultiplier = cornerScale;
            cardImg.color = Surface;

            Label(cardRt, "Tiêu đề", "Về sảnh", 20, Ink, true, TextAnchor.UpperLeft, new Vector2(28, -24), new Vector2(580, 28));
            hoiVeSanhNoiDung = Label(cardRt, "Nội dung", "", 16, Dim, false, TextAnchor.UpperLeft, new Vector2(28, -64), new Vector2(580, 56));

            MakeButton(cardRt, "Nút ở lại", "Ở lại", new Vector2(28, -166), new Vector2(120, 48), DongHoiVeSanh, false, 15);
            MakeButton(cardRt, "Nút về màn chọn", "Màn chọn ban đầu", new Vector2(160, -166), new Vector2(214, 48), () => XacNhanVeSanh(true), false, 15);
            MakeButton(cardRt, "Nút về bảng điều khiển", GameSession.HasIdentity ? "Bảng điều khiển" : "Về sảnh",
                new Vector2(386, -166), new Vector2(222, 48), () => XacNhanVeSanh(false), true, 15);

            hoiVeSanh = scrimRt.gameObject;
            hoiVeSanh.SetActive(false);
        }

        void BuildThemeConfirmDialog(Transform parent)
        {
            var scrimRt = new GameObject("Xác nhận đổi nền", typeof(RectTransform)).GetComponent<RectTransform>();
            scrimRt.SetParent(parent, false);
            scrimRt.anchorMin = Vector2.zero;
            scrimRt.anchorMax = Vector2.one;
            scrimRt.offsetMin = scrimRt.offsetMax = Vector2.zero;
            var scrimImg = scrimRt.gameObject.AddComponent<Image>();
            scrimImg.sprite = Plain;
            scrimImg.color = Scrim(0.62f);
            scrimImg.raycastTarget = true;

            float cornerScale = UiTheme.IsVera ? 1.2f : 2.6f;
            var borderRt = Rect(scrimRt, "Viền hộp thoại", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560, 240));
            var borderImg = borderRt.gameObject.AddComponent<Image>();
            borderImg.sprite = Solid;
            borderImg.type = Image.Type.Sliced;
            borderImg.pixelsPerUnitMultiplier = cornerScale;
            borderImg.color = Line;
            borderImg.raycastTarget = true;

            var cardRt = new GameObject("Khung hộp thoại", typeof(RectTransform)).GetComponent<RectTransform>();
            cardRt.SetParent(borderRt, false);
            cardRt.anchorMin = Vector2.zero;
            cardRt.anchorMax = Vector2.one;
            cardRt.offsetMin = new Vector2(1.5f, 1.5f);
            cardRt.offsetMax = new Vector2(-1.5f, -1.5f);
            var cardImg = cardRt.gameObject.AddComponent<Image>();
            cardImg.sprite = Solid;
            cardImg.type = Image.Type.Sliced;
            cardImg.pixelsPerUnitMultiplier = cornerScale;
            cardImg.color = Surface;
            cardImg.raycastTarget = true;

            Label(cardRt, "Tiêu đề", "Đổi giao diện", 20, Ink, true, TextAnchor.UpperLeft, new Vector2(28, -24), new Vector2(500, 28));
            themeConfirmDetail = Label(cardRt, "Nền mới", "", 14, Accent, true, TextAnchor.UpperLeft, new Vector2(28, -56), new Vector2(500, 22));
            Label(cardRt, "Nội dung", "Đổi nền cần mở lại phòng lab, bài đang làm sẽ mất", 16, Dim, false, TextAnchor.UpperLeft, new Vector2(28, -86), new Vector2(500, 56));

            MakeButton(cardRt, "Hủy", "Hủy", new Vector2(226, -166), new Vector2(132, 48), CancelThemeSwitch, false, 15);
            MakeButton(cardRt, "Đổi & mở lại", "Đổi & mở lại", new Vector2(370, -166), new Vector2(160, 48), ConfirmThemeSwitch, true, 15);

            themeConfirmDialog = scrimRt.gameObject;
            themeConfirmDialog.SetActive(false);
        }

        string GetControlGuidanceText()
        {
            if (Khkt.Input.PadInput.UsingTouch)
                return "Cảm ứng iPad: Vuốt nửa trái để di chuyển  ·  Vuốt nửa phải xoay góc nhìn  ·  Chạm nút TAY để thao tác  ·  Menu ☰ ở góc trên";
            return $"<color={HexInk}><b>Rê chuột</b></color> nhìn  ·  <color={HexInk}><b>WASD</b></color> đi  ·  <color={HexInk}><b>Chuột trái</b></color> cầm, dùng  ·  <color={HexAccent}><b>B</b></color> tra cứu hóa chất  ·  <color={HexAccent}><b>T</b></color> trợ lý AI  ·  <color={HexInk}><b>1–9</b></color> các khu  ·  <color={HexInk}><b>H</b></color> trợ giúp  ·  <color={HexInk}><b>Esc</b></color> menu";
        }

        void RequestThemeSwitch(UiTheme.Mode mode)
        {
            if (mode == UiTheme.Current) return;
            pendingTheme = mode;
            if (themeConfirmDetail != null)
                themeConfirmDetail.text = $"Chuyển sang: {UiTheme.ModeLabel(mode)}";
            if (themeConfirmDialog != null)
            {
                themeConfirmDialog.SetActive(true);
                themeConfirmDialog.transform.SetAsLastSibling();
            }
        }

        void CancelThemeSwitch()
        {
            if (themeConfirmDialog != null) themeConfirmDialog.SetActive(false);
            RefreshThemeCards();
        }

        void ConfirmThemeSwitch()
        {
            if (themeConfirmDialog != null) themeConfirmDialog.SetActive(false);
            UiTheme.Set(pendingTheme, true);
            UiBridge.SetCapture(ref captured, false);
            Time.timeScale = 1f;
            var activeScene = SceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(activeScene.name))
                SceneManager.LoadScene(activeScene.name);
        }

        void SetClickMode(int mode)
        {
            mode = mode == 1 ? 1 : 0;
            PlayerPrefs.SetInt(UiTheme.ClickModePrefKey, mode);
            PlayerPrefs.Save();
            RefreshClickModeChips();
        }

        void OnTouchSensitivityChanged(float value)
        {
            float clamped = Mathf.Clamp(value, 0.05f, 0.50f);
            PlayerPrefs.SetFloat(TouchSensitivityPrefKey, clamped);
            PlayerPrefs.Save();
            var tc = FindAnyObjectByType<Khkt.Input.TouchControls>();
            if (tc != null) tc.SetLookSensitivity(clamped);
        }

        void RefreshThemeCards()
        {
            var cur = UiTheme.Current;
            for (int i = 0; i < themeCards.Length; i++)
            {
                if (themeCards[i].border != null)
                    themeCards[i].border.color = themeCards[i].mode == cur ? Accent : Line;
                if (themeCards[i].status != null)
                {
                    bool on = themeCards[i].mode == cur;
                    if (on) themeCards[i].status.text = "Đang dùng";
                    themeCards[i].status.font = on ? Semibold : Regular;
                }
            }
        }

        void RefreshClickModeChips()
        {
            int clickMode = PlayerPrefs.GetInt(UiTheme.ClickModePrefKey, 0);
            SetChip(clickPickChip, clickMode == 0);
            SetChip(clickPanelChip, clickMode == 1);
        }

        void SelectFontScale(int mode)
        {
            UiTheme.SetFontScale(mode);
            RefreshDisplayScaleChips();
        }

        void SelectPanelSize(int mode)
        {
            UiTheme.SetPanelSize(mode);
            RefreshDisplayScaleChips();
        }

        void RefreshDisplayScaleChips()
        {
            int fontMode = UiTheme.FontScaleMode;
            for (int i = 0; i < fontScaleChips.Length; i++)
                if (fontScaleChips[i] != null) SetChip(fontScaleChips[i], i == fontMode);

            int panelMode = UiTheme.PanelSizeMode;
            for (int i = 0; i < panelSizeChips.Length; i++)
                if (panelSizeChips[i] != null) SetChip(panelSizeChips[i], i == panelMode);
        }

        static bool IsGraphicsChipSelected(int index)
        {
            if (index == 0) return HardwareProfile.TuDong;
            return !HardwareProfile.TuDong && (int)HardwareProfile.Muc == index - 1;
        }

        void SelectGraphicsLevel(int index)
        {
            graphicsBenchmarkMsg = "";
            if (index == 0) HardwareProfile.DatTuDong();
            else HardwareProfile.Dat((MucDoHoa)Mathf.Clamp(index - 1, 0, 3));
            RefreshGraphicsProfileUi();
        }

        void Run30sBenchmarkFromSettings()
        {
            if (DoKhungHinh.DangDo) return;
            graphicsBenchmarkMsg = "Đang đo 30 giây...";
            Close();
            DoKhungHinh.ChayThu30Giay((deXuat, kq) =>
            {
                graphicsBenchmarkMsg = $"Đo 30s: p95 = {kq.p95Ms:0.1} ms ({kq.fpsTrungBinh:0} FPS) → đề xuất {HardwareProfile.TenMuc(deXuat)}";
                Open();
                ShowSettings();
                SelectSettingsTab(0);
            });
        }

        void RefreshGraphicsProfileUi()
        {
            for (int i = 0; i < graphicsChips.Length; i++)
                if (graphicsChips[i] != null) SetChip(graphicsChips[i], IsGraphicsChipSelected(i));
            if (graphicsHardwareLabel != null)
                graphicsHardwareLabel.text = HardwareProfile.MoTaMay();
            if (graphicsStatusLabel != null)
            {
                graphicsStatusLabel.text = !string.IsNullOrEmpty(graphicsBenchmarkMsg)
                    ? graphicsBenchmarkMsg
                    : (HardwareProfile.TuDong
                        ? $"Đang dùng: Tự động ({HardwareProfile.TenMuc(HardwareProfile.Muc)})"
                        : $"Đang dùng: {HardwareProfile.TenMuc(HardwareProfile.Muc)}");
            }
        }

        // =====================================================================
        // Bảng chọn bài chuẩn độ
        // =====================================================================

        void BuildSetup(RectTransform dimRt, Material glass)
        {
            var sImg = Panel(dimRt, "Chọn bài", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100, 680), glass, 1.1f);
            ApplyPanelFallback(sImg, 1.1f, Surface);
            setup = sImg.gameObject;
            var s = setup.transform;
            Label(s, "Tiêu đề", "Chọn bài chuẩn độ", 28, Ink, true, TextAnchor.UpperLeft, new Vector2(40, -32), new Vector2(500, 36));
            Label(s, "Gợi ý", "Dung dịch chuẩn và chỉ thị sẽ tự động đổi theo chất cần chuẩn, hoặc tự chỉnh theo ý muốn.", 15, Dim, false,
                  TextAnchor.UpperLeft, new Vector2(40, -70), new Vector2(960, 24));

            Row(s, "Chất cần chuẩn", -106, "analyte", new[] {
                ("HCl", "Axit clohiđric (mạnh)"),
                ("CH3COOH", "Axit axetic (yếu)"),
                ("H2SO4", "Axit sunfuric (2 nấc)"),
                ("H2C2O4", "Axit oxalic (2 nấc)"),
                ("NaOH", "Natri hiđroxit (mạnh)"),
                ("NH3", "Amoniac (yếu)"),
                ("FeSO4", "Sắt(II) sunfat (oxi hóa - khử)")
            }, a => { analyte = a; AutoPick(); });

            Row(s, "Dung dịch chuẩn (buret)", -216, "titrant", new[] {
                ("NaOH", "NaOH 0,1 M"),
                ("HCl", "HCl 0,1 M"),
                ("KMnO4", "KMnO₄ 0,02 M (tự chỉ thị)")
            }, t => { titrant = t; Refresh(); });

            Row(s, "Chỉ thị màu", -326, "indicator", new[] {
                ("pp", "Phenolphtalein (pH 8,2 – 10,0)"),
                ("mo", "Metyl da cam (pH 3,1 – 4,4)")
            }, i => { indicator = i; Refresh(); });

            Row(s, "Nồng độ mẫu", -436, "conc", new[] {
                ("0.1", "0,100 M (biết trước)"),
                ("0.05", "0,050 M (biết trước)"),
                ("0.2", "0,200 M (biết trước)"),
                ("0", "Ẩn — học sinh tự xác định")
            }, c => { analyteC = float.Parse(c, System.Globalization.CultureInfo.InvariantCulture); Refresh(); });

            setupNote = Label(s, "Ghi chú bài", "", 15, Accent, false, TextAnchor.UpperLeft, new Vector2(40, -538), new Vector2(1000, 50));
            MakeButton(s, "Quay lại", "Quay lại", new Vector2(40, -600), new Vector2(160, 54), ShowHome, false, 16);
            MakeButton(s, "Bắt đầu bài", "Vào thí nghiệm", new Vector2(860, -600), new Vector2(200, 54), StartTitration, true, 16);
            setup.SetActive(false);
        }

        void Row(Transform p, string label, float y, string key, (string val, string text)[] items, Action<string> onPick)
        {
            Label(p, "Nhãn " + key, label, 16, Dim, true, TextAnchor.UpperLeft, new Vector2(40, y), new Vector2(900, 22));
            float x = 40;
            var list = new List<(Button, string)>();
            foreach (var (val, text) in items)
            {
                string v = val;
                var tmp = Label(p, "Tạm", text, 14, Ink, false, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero);
                float w = Mathf.Max(Width(tmp, text) + 32, 100);
                if (Application.isPlaying) Destroy(tmp.gameObject);
                else DestroyImmediate(tmp.gameObject);
                var b = MakeChip(p, "Nút " + val, text, new Vector2(x, y - 28), new Vector2(w, 48), () => onPick(v), false, 14);
                list.Add((b, v));
                x += w + 10;
            }
            groups[key] = list;
        }

        void AutoPick()
        {
            bool baseAnalyte = analyte == "NaOH" || analyte == "KOH" || analyte == "NH3";
            bool redoxAnalyte = analyte == "FeSO4";
            if (redoxAnalyte) titrant = "KMnO4";
            else if (baseAnalyte && titrant != "HCl") titrant = "HCl";
            else if (!baseAnalyte && titrant == "HCl") titrant = "NaOH";
            if (titrant == "KMnO4" && analyte != "FeSO4" && analyte != "H2C2O4") titrant = "NaOH";
            if (analyte == "NH3" && indicator == "pp") indicator = "mo";
            if (analyte == "CH3COOH" && indicator == "mo") indicator = "pp";
            Refresh();
        }

        void Refresh()
        {
            void Mark(string key, string value)
            {
                if (!groups.TryGetValue(key, out var list)) return;
                foreach (var (b, v) in list) SetChip(b, v == value);
            }
            Mark("analyte", analyte);
            Mark("titrant", titrant);
            Mark("indicator", titrant == "KMnO4" ? "-" : indicator);
            Mark("conc", analyteC <= 0 ? "0" : analyteC.ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture));
            string note = titrant == "KMnO4" ? "KMnO₄ tự làm chỉ thị: điểm cuối khi dung dịch hồng nhạt bền 30 giây (cần môi trường H₂SO₄)."
                        : analyte == "NH3" ? "Bazơ yếu: điểm tương đương có pH < 7, nên dùng metyl da cam."
                        : analyte == "CH3COOH" ? "Axit yếu: điểm tương đương có pH > 7, nên dùng phenolphtalein."
                        : analyte == "H2SO4" || analyte == "H2C2O4" ? "Axit hai nấc: 1 mol axit cần 2 mol NaOH."
                        : "";
            if (setupNote != null)
                setupNote.text = note + (analyteC <= 0 ? (note.Length > 0 ? "\n" : "") + "Nồng độ mẫu ẩn: tính ra rồi so với đáp án ở bảng kết quả." : "");
        }

        IEnumerator EnterLab()
        {
            yield return null;
            if (procedure != null) procedure.Stop();

            int pendingStation = PlayerPrefs.GetInt(PendingStationPrefKey, -1);
            int targetStation = 2;
            if (pendingStation >= 0)
            {
                PlayerPrefs.SetInt(PendingStationPrefKey, -1);
                PlayerPrefs.Save();
                targetStation = pendingStation;
            }
            if (viewer != null) viewer.GoTo(targetStation);

            string msg;
            if (pendingStation >= 20)
            {
                msg = Khkt.Input.PadInput.UsingTouch
                    ? "Phòng Sinh học tầng 2: chạm kính hiển vi, PCR hoặc thanh trạm trên cùng"
                    : "Phòng Sinh học tầng 2: bấm vào kính hiển vi, PCR, hoặc nhấn 1–9 để chuyển khu";
            }
            else if (pendingStation >= 0)
            {
                msg = Khkt.Input.PadInput.UsingTouch
                    ? "Phòng Hóa học tầng 1: chạm Menu hoặc các nút cảm ứng để thao tác"
                    : "Phòng Hóa học tầng 1: nhấn B để tìm hóa chất, T để hỏi robot, H để xem trợ giúp";
            }
            else
            {
                msg = Khkt.Input.PadInput.UsingTouch
                    ? "Thí nghiệm tự do: chạm Menu hoặc các nút cảm ứng để thao tác"
                    : "Thí nghiệm tự do: nhấn B để tìm hóa chất, T để hỏi robot, H để xem trợ giúp";
            }
            LabMessages.Say(msg, LabMessages.Tone.Info);
        }

        public void Open()
        {
            if (root == null) return;
            IsOpen = true;
            root.SetActive(true);
            root.transform.SetAsLastSibling();
            if (home != null) home.SetActive(true);
            if (setup != null) setup.SetActive(false);
            if (settings != null) settings.SetActive(false);
            if (themeConfirmDialog != null) themeConfirmDialog.SetActive(false);
            DongHoiVeSanh();
            if (guideText != null) guideText.text = GetControlGuidanceText();
            if (continueBtnObj != null) continueBtnObj.SetActive(started);
            SelectModeCard(selectedMode);
            UiBridge.SetCapture(ref captured, true);
        }

        public void Close()
        {
            IsOpen = false;
            if (root != null) root.SetActive(false);
            UiBridge.SetCapture(ref captured, false);
            started = true;
        }

        public void OpenDisplaySettings()
        {
            Open();
            ShowSettings();
            SelectSettingsTab(0);
        }

        void RefreshSettingsFields()
        {
            if (aiUrlInput != null)
                aiUrlInput.text = PlayerPrefs.GetString(AiClient.ServerUrlPrefKey, AiClient.Instance != null ? AiClient.Instance.serverUrl : "http://127.0.0.1:8000");
            if (aiSaveStatus != null) aiSaveStatus.text = "";
            if (serverUrlInput != null)
                serverUrlInput.text = GameSession.ServerUrl;
            if (serverSaveStatus != null) serverSaveStatus.text = "";
            if (soundToggle != null) soundToggle.SetIsOnWithoutNotify(NutHieuUng.SoundEnabled);
            if (motionToggle != null) motionToggle.SetIsOnWithoutNotify(ReducedMotion.IsEnabled);
            if (transToggle != null) transToggle.SetIsOnWithoutNotify(GlassKit.ReducedTransparency);
            if (touchSensSlider != null)
            {
                var tc = FindAnyObjectByType<Khkt.Input.TouchControls>();
                float sens = Mathf.Clamp(tc != null ? tc.lookSensitivity : PlayerPrefs.GetFloat(TouchSensitivityPrefKey, 0.18f), 0.05f, 0.50f);
                touchSensSlider.SetValueWithoutNotify(sens);
                if (touchSensValueLabel != null) touchSensValueLabel.text = sens.ToString("0.00");
            }
            RefreshThemeCards();
            RefreshClickModeChips();
            RefreshDisplayScaleChips();
            RefreshGraphicsProfileUi();
        }

        void ShowSettings()
        {
            home.SetActive(false);
            setup.SetActive(false);
            settings.SetActive(true);
            if (themeConfirmDialog != null) themeConfirmDialog.SetActive(false);
            SelectSettingsTab(activeSettingsTab);
            RefreshSettingsFields();
        }

        void CloseSettings()
        {
            if (themeConfirmDialog != null) themeConfirmDialog.SetActive(false);
            settings.SetActive(false);
            home.SetActive(true);
        }

        void SaveSettings()
        {
            if (aiUrlInput == null) return;
            string url = aiUrlInput.text.Trim();
            if (string.IsNullOrEmpty(url)) url = "http://127.0.0.1:8000";
            if (AiClient.Instance != null) AiClient.Instance.SetServerUrl(url);
            else
            {
                PlayerPrefs.SetString(AiClient.ServerUrlPrefKey, url);
                PlayerPrefs.Save();
            }
            if (aiSaveStatus != null)
            {
                aiSaveStatus.color = Good;
                aiSaveStatus.text = "✓ Đã lưu máy chủ AI: " + url;
            }
        }

        void FillAccountServerUrl(string url)
        {
            if (serverUrlInput != null) serverUrlInput.text = url;
            if (serverSaveStatus != null) serverSaveStatus.text = "";
        }

        void SaveAccountServerSettings()
        {
            if (serverUrlInput == null) return;
            string url = serverUrlInput.text.Trim().TrimEnd('/');
            bool valid = Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                         (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
            if (!valid)
            {
                if (serverSaveStatus != null)
                {
                    serverSaveStatus.color = Bad;
                    serverSaveStatus.text = "Địa chỉ phải bắt đầu bằng http:// hoặc https://";
                }
                return;
            }
            GameSession.ServerUrl = url;
            serverUrlInput.text = GameSession.ServerUrl;
            if (serverSaveStatus != null)
            {
                serverSaveStatus.color = Good;
                serverSaveStatus.text = "✓ Đã lưu máy chủ: " + GameSession.ServerUrl;
            }
        }

        void ShowHome()
        {
            setup.SetActive(false);
            home.SetActive(true);
        }

        void ShowSetup()
        {
            home.SetActive(false);
            setup.SetActive(true);
        }

        void StartFree()
        {
            if (procedure != null) procedure.Stop();
            Close();
            if (viewer != null) viewer.GoTo(2);
            string msg = Khkt.Input.PadInput.UsingTouch
                ? "Thí nghiệm tự do: chạm Menu hoặc các nút cảm ứng để thao tác"
                : "Thí nghiệm tự do: nhấn B để tìm hóa chất, T để hỏi robot";
            LabMessages.Say(msg, LabMessages.Tone.Good);
        }

        void StartExplore()
        {
            if (procedure != null) procedure.Stop();
            Close();
            if (viewer != null) viewer.GoTo(9);
            string msg = Khkt.Input.PadInput.UsingTouch
                ? "Chạm thanh trạm trên cùng để chuyển khu thí nghiệm nhanh"
                : "Nhấn 1–9 để tới từng khu, WASD để đi bộ";
            LabMessages.Say(msg, LabMessages.Tone.Info);
        }

        void StartDemoShowcase()
        {
            var showcase = DemoShowcase.Instance != null ? DemoShowcase.Instance : FindAnyObjectByType<DemoShowcase>(FindObjectsInactive.Include);
            Close();
            if (showcase != null)
                showcase.Open();
            else
                LabMessages.Say("Danh sách bài demo chưa sẵn sàng trong cảnh này", LabMessages.Tone.Warn);
        }

        void StartTitration()
        {
            if (procedure != null) procedure.Begin(LabProcedure.Config.Make(analyte, titrant, indicator, analyteC));
            Close();
            if (viewer != null) viewer.GoTo(1);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;
            if (IsOpen)
            {
                if (DangHoiVeSanh)
                {
                    DongHoiVeSanh();
                    return;
                }
                if (themeConfirmDialog != null && themeConfirmDialog.activeSelf)
                {
                    CancelThemeSwitch();
                    return;
                }
                if (settings != null && settings.activeSelf)
                {
                    CloseSettings();
                    return;
                }
                if (setup != null && setup.activeSelf)
                {
                    ShowHome();
                    return;
                }
                Close();
                return;
            }
            if (DesktopViewer.UiCapture == 0 && UiBridge.LastCloseFrame != Time.frameCount) Open();
        }
    }
}