using System;
using System.Collections.Generic;
using System.Globalization;
using Khkt.Demo.Instruments;
using Khkt.Interaction;
using Khkt.Simulation;
using Khkt.Simulation.Chem;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Khkt.Demo
{
    /// <summary>
    /// Bảng chi tiết nổi 75% màn hình (1660×940 trên tham chiếu 1920×1080) cho Buret, Chai hóa chất và Dụng cụ thủy tinh.
    /// Tự động thích ứng theo 3 chủ đề UiTheme thông qua GlassKit.
    /// </summary>
    public class ToolPanel : MonoBehaviour
    {
        public static ToolPanel Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance.open;
        public static bool Enabled = string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("KHKT_TEST"));
        public static bool ForceEnableForTest;

        static bool CanOpen => Enabled || ForceEnableForTest;
        static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

        const float WinW = 1660f;
        const float WinH = 940f;
        const float PadX = 24f;
        const float InnerW = WinW - PadX * 2f; // 1612
        const float HeaderH = 64f;
        const float BodyH = 742f;
        const float FooterH = 68f;

        enum Mode { None, Buret, Bottle, Container, Object }

        Mode mode;
        bool open;
        bool captured;
        GameObject targetRoot;
        BuretStopcock curBuret;
        ChemicalBottle curBottle;
        LabContainer curContainer;

        GameObject backdropGo;
        GameObject windowGo;
        RectTransform leftCol;
        RectTransform rightCol;

        // Xem trước 3D + sơ đồ Painter dự phòng
        RawImage previewImg;
        GameObject previewFallbackGo;
        Texture2D bottleLabelTex;
        GameObject buretExtraCache;

        // Điều khiển riêng Buret
        RawImage meniscusImg;
        Painter meniscusPainter;
        float lastMeniscusReading = float.NaN;
        Text buretReadingVal;
        Text buretFlowVal;
        Text buretContentsVal;
        Slider buretSlider;
        Text buretSliderVal;
        RawImage curveImg;
        Text curveSummaryText;
        Text curveEmptyHint;
        int lastCurveVersion = -1;
        bool lastCurveResult;
        bool bubblePurged;
        Image[] checkBadges;
        Text[] checkLabels;
        Text buretBannerText;

        // Điều khiển riêng Chai hóa chất & Bình chứa
        Text bottlePhVal;
        Text bottleRemainVal;
        Text bottleLabelRemainText;
        readonly List<LabContainer> receivers = new List<LabContainer>(4);
        readonly List<Button> receiverChips = new List<Button>(4);
        int selectedReceiverIdx;
        float transferAmountMl = 25f;
        int transferScoops = 1;
        Button secondaryActionBtn;
        Text secondaryActionText;
        readonly List<PredRow> predRows = new List<PredRow>(3);
        Text predHeaderSub;
        Slider bottleTransferSlider;
        float dilutionStockC1;
        readonly List<float> dilutionC2Values = new List<float>(3);
        readonly List<float> dilutionV2Values = new List<float>(3);
        readonly List<Button> dilutionC2Chips = new List<Button>(3);
        readonly List<Button> dilutionV2Chips = new List<Button>(3);
        int selectedDilutionC2Idx;
        int selectedDilutionV2Idx = 1;
        Text dilutionFormulaText;

        // Dụng cụ chung (LabContainer)
        Text contVolVal;
        Text contPhVal;
        Text contTempVal;
        Text contCompVal;
        Text contDetailText;

        /// <summary>Mức phân loại an toàn cho từng phép trộn dự đoán.</summary>
        public enum MixSafetyTone { Good, Warn, Bad }

        /// <summary>Kết quả mô phỏng trộn trên bản sao độc lập qua ChemEngine.</summary>
        public struct MixPrediction
        {
            public MixSafetyTone Tone;
            public string Badge;
            public string Summary;
            public double PhBefore;
            public double PhAfter;
            public double DeltaTempC;
            public List<ChemEvent> Events;
        }

        struct PredRow
        {
            public Text title;
            public Text detail;
            public Image badgeBg;
            public Text badgeText;
        }

        // ------------------------------------------------------------------
        // Điểm vào công khai & test hooks
        // ------------------------------------------------------------------

        public static bool OpenBuret(BuretStopcock buret)
        {
            if (!CanOpen || buret == null) return false;
            EnsureInstance().ShowBuret(buret);
            return true;
        }

        public static bool OpenBottle(ChemicalBottle bottle)
        {
            if (!CanOpen || bottle == null) return false;
            EnsureInstance().ShowBottle(bottle);
            return true;
        }

        public static bool OpenContainer(LabContainer container)
        {
            if (!CanOpen || container == null) return false;
            EnsureInstance().ShowContainer(container);
            return true;
        }

        public static bool OpenObject(GameObject target)
        {
            if (!CanOpen || target == null) return false;
            var inst = EnsureInstance();
            var buret = target.GetComponentInParent<BuretStopcock>();
            if (buret != null) { inst.ShowBuret(buret); return true; }
            var bottle = target.GetComponentInParent<ChemicalBottle>();
            if (bottle != null) { inst.ShowBottle(bottle); return true; }
            var cont = target.GetComponentInParent<LabContainer>();
            if (cont != null) { inst.ShowContainer(cont); return true; }
            inst.ShowObject(target);
            return true;
        }

        public static void Close()
        {
            if (Instance != null) Instance.CloseInternal();
        }

        static ToolPanel EnsureInstance()
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<ToolPanel>();
            if (existing != null) { Instance = existing; return Instance; }
            var go = new GameObject("ToolPanel");
            Instance = go.AddComponent<ToolPanel>();
            return Instance;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            ForceEnableForTest = false;
            Enabled = string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("KHKT_TEST"));
        }

        // ------------------------------------------------------------------
        // Vòng đời Unity
        // ------------------------------------------------------------------

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void OnEnable() => UiTheme.Changed += OnThemeChanged;
        void OnDisable() => UiTheme.Changed -= OnThemeChanged;

        void OnDestroy()
        {
            UiTheme.Changed -= OnThemeChanged;
            ReleaseResources();
            UiBridge.SetCapture(ref captured, false);
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (!open) return;

            if (targetRoot == null)
            {
                CloseInternal();
                return;
            }

            var kb = Keyboard.current;
            var chat = RobotChatUI.Instance;
            bool chatOpen = chat != null && chat.IsOpen;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && !chatOpen && UiBridge.LastCloseFrame != Time.frameCount)
            {
                CloseInternal();
                return;
            }

            if (previewImg != null && ToolPreview.ShouldRefresh(0.2f))
                RefreshPreview();

            if (mode == Mode.Buret && curBuret != null)
                UpdateBuretLive();
            else if (mode == Mode.Bottle && curBottle != null)
                UpdateBottleLive();
            else if (mode == Mode.Container && curContainer != null)
                UpdateContainerLive();
        }

        void OnThemeChanged()
        {
            if (!open || targetRoot == null) return;
            switch (mode)
            {
                case Mode.Buret:
                    if (curBuret != null) ShowBuret(curBuret);
                    break;
                case Mode.Bottle:
                    if (curBottle != null) ShowBottle(curBottle);
                    break;
                case Mode.Container:
                    if (curContainer != null) ShowContainer(curContainer);
                    break;
                case Mode.Object:
                    ShowObject(targetRoot);
                    break;
            }
            ApplyWindowScale();
        }

        void ApplyWindowScale()
        {
            if (windowGo == null) return;
            var target = UiTheme.PanelSize(new Vector2(WinW, WinH));
            float scale = Mathf.Min(target.x / WinW, target.y / WinH);
            windowGo.transform.localScale = new Vector3(scale, scale, 1f);
        }

        // ------------------------------------------------------------------
        // Mở & đóng bảng
        // ------------------------------------------------------------------

        void ShowBuret(BuretStopcock buret)
        {
            mode = Mode.Buret;
            curBuret = buret;
            curBottle = null;
            curContainer = buret.container;
            targetRoot = buret.gameObject;
            buretExtraCache = FindBuretExtra(buret);
            lastMeniscusReading = float.NaN;
            lastCurveVersion = -1;

            float vol = buret.lastMarkMl > 0.1f ? buret.lastMarkMl : 50f;
            var proc = FindAnyObjectByType<LabProcedure>();
            string stepBadge = "Chuẩn độ";
            if (proc != null)
            {
                int total = proc.StepNames != null && proc.StepNames.Length > 0 ? proc.StepNames.Length : 7;
                int stepIdx = Mathf.Clamp((int)proc.Current + 1, 1, total);
                stepBadge = proc.Current == LabProcedure.Step.Xong
                    ? $"Hoàn thành {total}/{total}"
                    : $"Bước {stepIdx}/{total}";
            }

            BuildShell(
                "BR",
                $"Buret {vol.ToString("0", Vi)} mL",
                stepBadge,
                GlassKit.AccentSoft,
                GlassKit.Accent,
                0.40f,
                "Trả về chỗ cũ",
                () => ReturnTargetHome(targetRoot),
                $"Buret {vol:0} mL đang đọc {buret.ReadingMl:0.00} mL, đang chứa {DescribeContainerShort(buret.container)}. Cần lưu ý gì khi đọc mặt khum và điều khiển khóa?",
                "Ghi số đọc",
                RecordBuretReading,
                GetPickButtonLabel(targetRoot),
                () => PickTarget(targetRoot));

            BuildBuretBody(buret, proc);
        }

        void ShowBottle(ChemicalBottle bottle)
        {
            if (curBottle != bottle)
            {
                selectedDilutionC2Idx = 0;
                selectedDilutionV2Idx = 1;
            }
            mode = Mode.Bottle;
            curBottle = bottle;
            curBuret = null;
            curContainer = bottle.container;
            targetRoot = bottle.gameObject;

            var e = bottle.Entry ?? (ChemicalCatalog.Instance != null ? ChemicalCatalog.Instance.Get(bottle.stt) : null);
            string title = bottle.DisplayName;
            if (e != null)
            {
                string f = e.Formula;
                title = !string.IsNullOrEmpty(f) && !string.Equals(e.ten, f, StringComparison.OrdinalIgnoreCase)
                    ? $"{e.ten} · {f}"
                    : e.ten;
            }

            string hazard = e != null ? ChemicalCatalog.HazardText(e) : "";
            string badge = !string.IsNullOrEmpty(hazard) ? "⚠ " + hazard : (e != null ? ChemicalCatalog.StateText(e) : "Hóa chất");
            Color badgeBg = !string.IsNullOrEmpty(hazard) ? GlassKit.BadSoft : GlassKit.AccentSoft;
            Color badgeFg = !string.IsNullOrEmpty(hazard) ? GlassKit.Bad : GlassKit.Accent;

            FindNearbyReceivers(bottle.transform.position, bottle.container);
            if (!bottle.IsSolid && bottle.container != null)
                transferAmountMl = Mathf.Clamp(transferAmountMl, 5f, 100f);

            string secondaryLabel = FormatBottleActionLabel(bottle);
            string qtyInfo = FormatBottleRemaining(bottle);
            string askPrompt = $"Hóa chất {title} (còn {qtyInfo}{(string.IsNullOrEmpty(hazard) ? "" : ", cảnh báo: " + hazard)}). Cho biết tính chất chính và lưu ý an toàn khi thao tác.";

            BuildShell(
                "HC",
                title,
                badge,
                badgeBg,
                badgeFg,
                0.42f,
                "Trả về kệ",
                () => ReturnTargetHome(targetRoot),
                askPrompt,
                secondaryLabel,
                ExecuteBottleTransfer,
                GetPickButtonLabel(targetRoot),
                () => PickTarget(targetRoot));

            BuildBottleBody(bottle, e, hazard);
        }

        void ShowContainer(LabContainer container)
        {
            mode = Mode.Container;
            curContainer = container;
            curBuret = null;
            curBottle = null;
            targetRoot = container.gameObject;

            var info = container.GetComponentInParent<EquipmentInfo>();
            string title = !string.IsNullOrEmpty(container.label) ? container.label : (info != null && !string.IsNullOrEmpty(info.title) ? info.title : container.gameObject.name);
            bool hasStuff = !container.Total.IsEmpty;
            string badge = hasStuff ? "Đang chứa chất" : "Trống";
            Color badgeBg = hasStuff ? GlassKit.AccentSoft : GlassKit.Well;
            Color badgeFg = hasStuff ? GlassKit.Accent : GlassKit.Dim;

            FindNearbyReceivers(container.transform.position, container);
            float maxPour = Mathf.Max(5f, Mathf.Min(100f, container.VolumeMl));
            transferAmountMl = Mathf.Clamp(25f, 5f, maxPour);

            bool canTransfer = hasStuff && receivers.Count > 0;
            string secondaryLabel = canTransfer ? $"Rót {transferAmountMl:0} mL" : null;
            string askPrompt = $"Dụng cụ {title}: {container.Total.Describe(4)}, pH {(container.VolumeMl > 0.1f ? container.BulkPh.ToString("0.00") : "—")}, {container.TemperatureC:0.0} °C. Cần chú ý thao tác gì?";

            BuildShell(
                "DC",
                title,
                badge,
                badgeBg,
                badgeFg,
                0.42f,
                "Trả về chỗ cũ",
                () => ReturnTargetHome(targetRoot),
                askPrompt,
                secondaryLabel,
                canTransfer ? (Action)ExecuteContainerTransfer : null,
                GetPickButtonLabel(targetRoot),
                () => PickTarget(targetRoot));

            BuildContainerBody(container, info);
        }

        void ShowObject(GameObject target)
        {
            mode = Mode.Object;
            targetRoot = target;
            curBuret = null;
            curBottle = null;
            curContainer = null;

            var info = target.GetComponentInParent<EquipmentInfo>();
            string title = info != null && !string.IsNullOrEmpty(info.title) ? info.title : target.name;
            var usable = target.GetComponentInParent<IDemoUsable>();
            var grab = target.GetComponentInParent<Grabbable>();
            string badge = usable != null ? "Tương tác" : (grab != null ? "Dụng cụ" : "Thiết bị");

            string secLabel = usable != null ? "Kích hoạt" : null;
            Action secAct = usable != null ? () => { CloseInternal(); usable.Use(); } : (Action)null;
            string primLabel = grab != null ? GetPickButtonLabel(target) : "Đóng";
            Action primAct = grab != null ? () => PickTarget(target) : (Action)CloseInternal;

            string askPrompt = $"Dụng cụ {title}" + (info != null && !string.IsNullOrEmpty(info.usage) ? $" ({info.usage})" : "") + ". Hướng dẫn cách sử dụng đúng trong phòng thí nghiệm.";

            BuildShell(
                "DC",
                title,
                badge,
                GlassKit.AccentSoft,
                GlassKit.Accent,
                0.42f,
                "Trả về chỗ cũ",
                () => ReturnTargetHome(targetRoot),
                askPrompt,
                secLabel,
                secAct,
                primLabel,
                primAct);

            BuildGenericObjectBody(target, info, usable, grab);
        }

        void ClearVisuals()
        {
            ReleaseResources();
            if (backdropGo != null) Destroy(backdropGo);
            if (windowGo != null) Destroy(windowGo);
            backdropGo = null;
            windowGo = null;
            leftCol = null;
            rightCol = null;

            previewImg = null;
            previewFallbackGo = null;
            meniscusImg = null;
            buretReadingVal = null;
            buretFlowVal = null;
            buretContentsVal = null;
            buretSlider = null;
            buretSliderVal = null;
            curveImg = null;
            curveSummaryText = null;
            curveEmptyHint = null;
            checkBadges = null;
            checkLabels = null;
            buretBannerText = null;

            bottlePhVal = null;
            bottleRemainVal = null;
            bottleLabelRemainText = null;
            receiverChips.Clear();
            secondaryActionBtn = null;
            secondaryActionText = null;
            predRows.Clear();
            predHeaderSub = null;
            bottleTransferSlider = null;
            dilutionStockC1 = 0f;
            dilutionC2Values.Clear();
            dilutionV2Values.Clear();
            dilutionC2Chips.Clear();
            dilutionV2Chips.Clear();
            dilutionFormulaText = null;

            contVolVal = null;
            contPhVal = null;
            contTempVal = null;
            contCompVal = null;
            contDetailText = null;
        }

        void ReleaseResources()
        {
            ToolPreview.Release();
            if (meniscusPainter != null)
            {
                if (meniscusPainter.Tex != null) Destroy(meniscusPainter.Tex);
                meniscusPainter = null;
            }
            if (bottleLabelTex != null)
            {
                Destroy(bottleLabelTex);
                bottleLabelTex = null;
            }
        }

        void CloseInternal()
        {
            if (!open && windowGo == null && backdropGo == null) return;
            open = false;
            mode = Mode.None;
            targetRoot = null;
            curBuret = null;
            curBottle = null;
            curContainer = null;
            buretExtraCache = null;
            selectedDilutionC2Idx = 0;
            selectedDilutionV2Idx = 1;

            ClearVisuals();
            UiBridge.SetCapture(ref captured, false);
        }

        // ------------------------------------------------------------------
        // Khung bảng chung (64 px Đầu · 742 px Thân · 68 px Chân)
        // ------------------------------------------------------------------

        void BuildShell(
            string iconCode,
            string title,
            string badgeText,
            Color badgeBg,
            Color badgeFg,
            float leftRatio,
            string returnLabel,
            Action onReturn,
            string askQuestion,
            string secondaryLabel,
            Action onSecondary,
            string primaryLabel,
            Action onPrimary)
        {
            ClearVisuals();

            var canvas = GlassKit.HostCanvas(transform, 45);
            var backdrop = GlassKit.Backdrop(canvas.transform, "Lớp mờ bảng dụng cụ", CloseInternal, 0.48f);
            backdropGo = backdrop.gameObject;

            var winImg = GlassKit.Window(canvas.transform, "Bảng chi tiết dụng cụ", new Vector2(WinW, WinH), Vector2.zero);
            windowGo = winImg.gameObject;
            var win = winImg.rectTransform;
            ApplyWindowScale();
            var winCg = windowGo.GetComponent<CanvasGroup>();
            if (winCg == null) winCg = windowGo.AddComponent<CanvasGroup>();
            winCg.alpha = 0f;
            UiMotion.Popup(this, win, winCg, 0.22f);

            var tl = new Vector2(0f, 1f);

            // 1. Đầu bảng (64 px)
            var header = GlassKit.Rect(win, "Đầu bảng", tl, tl, new Vector2(PadX, -14f), new Vector2(InnerW, HeaderH));
            var iconBox = GlassKit.Fill(header, "Biểu tượng", new Vector2(0f, -12f), new Vector2(40f, 40f), GlassKit.AccentSoft, null, true);
            GlassKit.Label(iconBox.transform, "Mã", iconCode, 15, GlassKit.Accent, true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(40f, 40f));

            const float closeW = 104f;
            float closeX = InnerW - closeW;
            GlassKit.MakeButton(header, "Đóng", "Đóng", new Vector2(closeX, -10f), new Vector2(closeW, 44f), CloseInternal, false, 16);

            float titleMaxW = closeX - 66f;
            if (!string.IsNullOrEmpty(badgeText))
            {
                var probe = GlassKit.Label(header, "Đo nhãn", badgeText, 14, badgeFg, true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(400f, 44f));
                float bw = Mathf.Clamp(GlassKit.Width(probe, badgeText) + 34f, 108f, 340f);
                Destroy(probe.gameObject);
                float bx = closeX - 14f - bw;
                var chipImg = GlassKit.Fill(header, "Huy hiệu", new Vector2(bx, -10f), new Vector2(bw, 44f), badgeBg, null, true);
                AttachSoftBorder(chipImg.rectTransform, new Color(badgeFg.r, badgeFg.g, badgeFg.b, 0.35f));
                GlassKit.Label(chipImg.transform, "Chữ", badgeText, 14, badgeFg, true, TextAnchor.MiddleCenter, new Vector2(10f, 0f), new Vector2(bw - 20f, 44f));
                titleMaxW = bx - 70f;
            }

            var titleLbl = GlassKit.Label(header, "Tiêu đề", title, 23, GlassKit.Ink, true, TextAnchor.MiddleLeft, new Vector2(54f, -10f), new Vector2(Mathf.Max(220f, titleMaxW), 44f));
            titleLbl.horizontalOverflow = HorizontalWrapMode.Wrap;

            GlassKit.Fill(win, "Đường kẻ đầu", new Vector2(PadX, -84f), new Vector2(InnerW, 1f), GlassKit.Line);
            GlassKit.Fill(win, "Đoạn nhấn đầu", new Vector2(PadX, -83f), new Vector2(64f, 3f), GlassKit.Accent);

            // 2. Thân bảng (742 px) với hai cột bị cắt biên bằng RectMask2D + vạch chia 1 px
            var body = GlassKit.Rect(win, "Thân bảng", tl, tl, new Vector2(PadX, -96f), new Vector2(InnerW, BodyH));
            float splitX = Mathf.Round(InnerW * leftRatio);
            float leftW = splitX - 18f;
            float rightX = splitX + 19f;
            float rightW = InnerW - rightX;

            leftCol = GlassKit.Rect(body, "Cột trái", tl, tl, Vector2.zero, new Vector2(leftW, BodyH));
            leftCol.gameObject.AddComponent<RectMask2D>();

            GlassKit.Fill(body, "Vạch chia cột", new Vector2(splitX, 0f), new Vector2(1f, BodyH), GlassKit.Line);

            rightCol = GlassKit.Rect(body, "Cột phải", tl, tl, new Vector2(rightX, 0f), new Vector2(rightW, BodyH));
            rightCol.gameObject.AddComponent<RectMask2D>();

            // 3. Chân bảng (68 px)
            GlassKit.Fill(win, "Đường kẻ chân", new Vector2(PadX, -850f), new Vector2(InnerW, 1f), GlassKit.Line);
            var footer = GlassKit.Rect(win, "Chân bảng", tl, tl, new Vector2(PadX, -858f), new Vector2(InnerW, FooterH));

            float btnY = -11f;
            const float btnH = 46f;
            var grab = targetRoot != null ? targetRoot.GetComponentInParent<Grabbable>() : null;
            float leftBtnX = 0f;
            if (grab != null)
            {
                GlassKit.MakeButton(footer, "Trả về", returnLabel, new Vector2(leftBtnX, btnY), new Vector2(196f, btnH), () => onReturn?.Invoke(), false, 16);
                leftBtnX += 208f;
            }
            GlassKit.MakeButton(footer, "Hỏi trợ lý", "Hỏi trợ lý", new Vector2(leftBtnX, btnY), new Vector2(174f, btnH), () => AskAssistant(askQuestion), false, 16);

            const float primW = 188f;
            float primX = InnerW - primW;
            GlassKit.MakeButton(footer, "Chính", primaryLabel, new Vector2(primX, btnY), new Vector2(primW, btnH), () => onPrimary?.Invoke(), true, 16);

            if (!string.IsNullOrEmpty(secondaryLabel) && onSecondary != null)
            {
                const float secW = 204f;
                float secX = primX - 12f - secW;
                secondaryActionBtn = GlassKit.MakeButton(footer, "Phụ", secondaryLabel, new Vector2(secX, btnY), new Vector2(secW, btnH), () => onSecondary(), false, 16);
                var ch = secondaryActionBtn.transform.Find("Chữ");
                secondaryActionText = ch != null ? ch.GetComponent<Text>() : null;
            }

            UiBridge.SetCapture(ref captured, true);
            open = true;
            var chatTr = canvas != null ? canvas.transform.Find("Trợ lý AI") : null;
            if (chatTr != null && chatTr.gameObject.activeSelf) chatTr.SetAsLastSibling();
        }

        // ------------------------------------------------------------------
        // Biến thể A: Buret (40% trái / 60% phải)
        // ------------------------------------------------------------------

        void BuildBuretBody(BuretStopcock buret, LabProcedure proc)
        {
            float lw = leftCol.sizeDelta.x;
            float topH = 330f;
            float botH = BodyH - topH - 14f;

            // Hộp trên trái: Soi mặt khum
            var topBox = MakeWellCard(leftCol, "Khung soi mặt khum", new Vector2(0f, 0f), new Vector2(lw, topH));
            var menRt = GlassKit.Rect(topBox, "Ảnh mặt khum", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(lw - 16f, topH - 16f));
            meniscusImg = menRt.gameObject.AddComponent<RawImage>();
            meniscusImg.raycastTarget = false;
            var menTag = GlassKit.Fill(topBox, "Nhãn soi", new Vector2(12f, -12f), new Vector2(138f, 30f), GlassKit.SurfaceA(0.88f), null, true);
            AttachSoftBorder(menTag.rectTransform, GlassKit.Line);
            GlassKit.Label(menTag.transform, "Chữ", "Soi mặt khum", 13, GlassKit.Ink, true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(138f, 30f));

            // Hộp dưới trái: ToolPreview 3D toàn bộ buret + bình nhận
            var botBox = MakeWellCard(leftCol, "Khung xem trước", new Vector2(0f, -(topH + 14f)), new Vector2(lw, botH));
            float sq = Mathf.Min(lw - 16f, botH - 16f);
            BuildPreviewBox(botBox, new Vector2(sq, sq), "Buret & bình chuẩn độ");

            // Cột phải (60%): chia nhóm đều đặn trong 742 px
            float rw = rightCol.sizeDelta.x;
            float colW3 = Mathf.Floor((rw - 28f) / 3f);

            // 1. Hàng 3 Readout (68 px)
            buretReadingVal = GlassKit.Readout(rightCol, "Số đọc", "Số đọc", FormatMl(buret.ReadingMl), new Vector2(0f, 0f), new Vector2(colW3, 68f));
            buretFlowVal = GlassKit.Readout(rightCol, "Tốc độ", "Tốc độ", FormatFlow(buret), new Vector2(colW3 + 14f, 0f), new Vector2(colW3, 68f));
            buretContentsVal = GlassKit.Readout(rightCol, "Đang chứa", "Đang chứa", DescribeContainerShort(buret.container), new Vector2((colW3 + 14f) * 2f, 0f), new Vector2(rw - (colW3 + 14f) * 2f, 68f), GlassKit.Ink);

            // 2. Thanh trượt Độ mở khóa (68 px -> vùng trượt 46 px >= 44 pt)
            buretSlider = GlassKit.MakeSlider(
                rightCol,
                "Độ mở khóa",
                "Độ mở khóa",
                new Vector2(0f, -82f),
                new Vector2(rw, 68f),
                0f,
                100f,
                buret.Opening * 100f,
                v =>
                {
                    if (curBuret == null) return;
                    if (!curBuret.mounted && v > 0.1f)
                    {
                        buretSlider.SetValueWithoutNotify(0f);
                        if (buretSliderVal != null) buretSliderVal.text = "0%";
                        LabToasts.Push("Lắp buret lên giá kẹp trước khi mở khóa.", LabToasts.Tone.Warn, 2.6f);
                        return;
                    }
                    float want = Mathf.Clamp01(v / 100f);
                    curBuret.AdjustOpening(want - curBuret.Opening);
                    if (buretSliderVal != null) buretSliderVal.text = $"{want * 100f:0}%";
                },
                v => $"{v:0}%",
                false);
            var valTr = buretSlider.transform.parent.Find("Giá trị");
            buretSliderVal = valTr != null ? valTr.GetComponent<Text>() : null;

            // 3. Lưới nút thao tác 2×3 (cao 46 px >= 44 pt)
            float gx1 = colW3 + 14f;
            float gx2 = (colW3 + 14f) * 2f;
            float w3Right = rw - gx2;
            const float bh = 46f;
            GlassKit.MakeButton(rightCol, "Nhỏ 1 giọt", "Nhỏ 1 giọt", new Vector2(0f, -164f), new Vector2(colW3, bh), () => DispenseFromBuret(buret.dropMl, "1 giọt"), false, 15);
            GlassKit.MakeButton(rightCol, "Nhỏ 0,5 mL", "Nhỏ 0,5 mL", new Vector2(gx1, -164f), new Vector2(colW3, bh), () => DispenseFromBuret(0.5f, "0,5 mL"), false, 15);
            GlassKit.MakeButton(rightCol, "Đóng khóa", "Đóng khóa", new Vector2(gx2, -164f), new Vector2(w3Right, bh), CloseBuretStopcock, false, 15);

            GlassKit.MakeButton(rightCol, "Rót thêm", "Rót thêm", new Vector2(0f, -220f), new Vector2(colW3, bh), TopUpBuret, false, 15);
            GlassKit.MakeButton(rightCol, "Đuổi bọt khí", "Đuổi bọt khí", new Vector2(gx1, -220f), new Vector2(colW3, bh), PurgeBuretBubble, false, 15);
            GlassKit.MakeButton(rightCol, "Chỉnh về 0", "Chỉnh về 0", new Vector2(gx2, -220f), new Vector2(w3Right, bh), ZeroBuretMeniscus, false, 15);

            // 4. Đồ thị đường chuẩn độ từ ModernUI (330 px)
            var graphCard = MakeWellCard(rightCol, "Thẻ đường chuẩn độ", new Vector2(0f, -280f), new Vector2(rw, 330f));
            GlassKit.Label(graphCard, "Tiêu đề đồ thị", "Đường chuẩn độ · pH theo thể tích", 14, GlassKit.Ink, true, TextAnchor.MiddleLeft, new Vector2(14f, -6f), new Vector2(460f, 26f));
            curveSummaryText = GlassKit.Label(graphCard, "Tóm tắt đồ thị", "", 13, GlassKit.Accent2, true, TextAnchor.MiddleRight, new Vector2(rw - 360f, -6f), new Vector2(346f, 26f));

            var curveRt = GlassKit.Rect(graphCard, "Ảnh đường chuẩn độ", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -36f), new Vector2(rw - 24f, 282f));
            curveImg = curveRt.gameObject.AddComponent<RawImage>();
            curveImg.raycastTarget = false;

            curveEmptyHint = GlassKit.Label(curveRt, "Chưa có dữ liệu", "Chưa có điểm pH — nhỏ dung dịch chuẩn để vẽ đường chuẩn độ", 14, GlassKit.Dim, false, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(rw - 24f, 282f));

            // 5. Hàng 3 trạng thái kiểm tra (44 px)
            checkBadges = new Image[3];
            checkLabels = new Text[3];
            float cw = colW3;
            for (int i = 0; i < 3; i++)
            {
                float cx = i * (cw + 14f);
                float cWidth = i == 2 ? (rw - cx) : cw;
                var pill = GlassKit.Fill(rightCol, $"Kiểm tra {i}", new Vector2(cx, -624f), new Vector2(cWidth, 44f), GlassKit.GoodSoft, null, true);
                AttachSoftBorder(pill.rectTransform, GlassKit.Line);
                checkBadges[i] = pill;
                checkLabels[i] = GlassKit.Label(pill.transform, "Chữ", "", 14, GlassKit.Good, true, TextAnchor.MiddleCenter, new Vector2(8f, 0f), new Vector2(cWidth - 16f, 44f));
            }

            // 6. Dải nhắc nhở (52 px)
            string hint = ResolveBuretHint(proc, buret);
            var banner = GlassKit.Banner(rightCol, "Nhắc chuẩn độ", hint, GlassKit.Warn, GlassKit.WarnSoft, new Vector2(0f, -682f), new Vector2(rw, 52f));
            var bodyTr = banner.transform.Find("Nội dung");
            buretBannerText = bodyTr != null ? bodyTr.GetComponent<Text>() : null;

            RefreshPreview();
            UpdateBuretLive();
        }

        void UpdateBuretLive()
        {
            if (curBuret == null) return;
            if (buretReadingVal != null) buretReadingVal.text = FormatMl(curBuret.ReadingMl);
            if (buretFlowVal != null) buretFlowVal.text = FormatFlow(curBuret);
            if (buretContentsVal != null) buretContentsVal.text = DescribeContainerShort(curBuret.container);

            if (buretSlider != null)
            {
                float pct = curBuret.Opening * 100f;
                if (Mathf.Abs(buretSlider.value - pct) > 0.25f)
                    buretSlider.SetValueWithoutNotify(pct);
                if (buretSliderVal != null)
                    buretSliderVal.text = $"{pct:0}%";
            }

            // Soi mặt khum: ưu tiên LoupeCamera trực tiếp nếu đang hoạt động
            if (meniscusImg != null)
            {
                var loupe = FindAnyObjectByType<LoupeCamera>();
                if (loupe != null && loupe.Active && loupe.Texture != null)
                {
                    if (meniscusImg.texture != loupe.Texture) meniscusImg.texture = loupe.Texture;
                    FitRawImage(meniscusImg, loupe.Texture);
                }
                else if (float.IsNaN(lastMeniscusReading) || Mathf.Abs(curBuret.ReadingMl - lastMeniscusReading) > 0.01f)
                {
                    lastMeniscusReading = curBuret.ReadingMl;
                    var tex = DrawMeniscusSchematic(curBuret);
                    meniscusImg.texture = tex;
                    FitRawImage(meniscusImg, tex);
                }
            }

            var proc = FindAnyObjectByType<LabProcedure>();
            if (curveImg != null)
            {
                var modernUi = FindAnyObjectByType<ModernUI>();
                if (modernUi != null)
                {
                    bool hasRes = proc != null && proc.LastResult.HasValue;
                    int ver = proc != null ? proc.CurveVersion : 0;
                    if (curveImg.texture == null || ver != lastCurveVersion || hasRes != lastCurveResult)
                    {
                        lastCurveVersion = ver;
                        lastCurveResult = hasRes;
                        int tw = Mathf.Max(320, Mathf.RoundToInt(curveImg.rectTransform.sizeDelta.x));
                        int th = Mathf.Max(140, Mathf.RoundToInt(curveImg.rectTransform.sizeDelta.y));
                        curveImg.texture = modernUi.RenderTitrationCurveTexture(tw, th);
                    }
                }
                curveImg.enabled = curveImg.texture != null;

                bool emptyCurve = proc == null || proc.Curve.Count == 0;
                if (curveEmptyHint != null) curveEmptyHint.gameObject.SetActive(emptyCurve);
                if (curveSummaryText != null)
                {
                    if (proc != null && proc.flask != null)
                        curveSummaryText.text = $"V = {proc.NaohIntoFlaskMl.ToString("0.00", Vi)} mL  ·  pH {proc.flask.BulkPh.ToString("0.00", Vi)}";
                    else
                        curveSummaryText.text = $"Đã nhỏ {curBuret.DeliveredMl.ToString("0.00", Vi)} mL";
                }
            }

            UpdateBuretCheckStrip(proc);

            if (buretBannerText != null)
                buretBannerText.text = ResolveBuretHint(proc, curBuret);
        }

        void UpdateBuretCheckStrip(LabProcedure proc)
        {
            if (checkBadges == null || checkLabels == null) return;
            bool rinsed = curBuret != null && (curBuret.HasLiquid || bubblePurged);
            SetCheckItem(0, rinsed ? "✓ Đã tráng" : "⚠ Chưa nạp dịch", rinsed);
            bool noBubble = bubblePurged || (curBuret != null && curBuret.HasLiquid && curBuret.ReadingMl >= 0f);
            SetCheckItem(1, noBubble ? "✓ Không bọt" : "⚠ Kiểm tra bọt khí", noBubble);

            bool eyeLevel = false;
            if (proc != null && proc.reading != null && proc.reading.eye != null && proc.reading.zeroMark != null)
                eyeLevel = Mathf.Abs(proc.reading.EyeAboveMeniscusM) < 0.015f;
            SetCheckItem(2, eyeLevel ? "✓ Mắt nhìn ngang vạch" : "⚠ Mắt nhìn ngang vạch", eyeLevel);
        }

        void SetCheckItem(int idx, string text, bool ok)
        {
            if (idx < 0 || idx >= checkBadges.Length) return;
            if (checkBadges[idx] != null) checkBadges[idx].color = ok ? GlassKit.GoodSoft : GlassKit.WarnSoft;
            if (checkLabels[idx] != null)
            {
                checkLabels[idx].text = text;
                checkLabels[idx].color = ok ? GlassKit.Good : GlassKit.Warn;
            }
        }

        string ResolveBuretHint(LabProcedure proc, BuretStopcock buret)
        {
            if (proc != null && proc.Active && proc.flask != null)
            {
                float rem = proc.EquivalenceMl - proc.NaohIntoFlaskMl;
                if (proc.EquivalenceMl > 0.5f && rem > -0.5f && rem < 2.5f)
                    return "Gần điểm tương đương, chuyển sang nhỏ từng giọt và lắc đều bình nón.";
                if (!string.IsNullOrEmpty(proc.Message))
                    return proc.Message;
            }
            if (buret != null && buret.ReadingMl < -0.02f)
                return "Mặt khum đang cao hơn vạch 0,00 mL — bấm 'Chỉnh về 0' trước khi bắt đầu.";
            return "Gần điểm tương đương, nhỏ từng giọt để nhận đúng thời điểm đổi màu chỉ thị.";
        }

        void DispenseFromBuret(float amountMl, string label)
        {
            if (curBuret == null || curBuret.container == null) return;
            if (!curBuret.mounted)
            {
                LabToasts.Push("Buret chưa lắp trên giá: lắp buret trước khi mở khóa.", LabToasts.Tone.Warn, 2.6f);
                return;
            }
            if (!curBuret.HasLiquid)
            {
                LabToasts.Push("Buret đã cạn dung dịch: bấm 'Rót thêm' để nạp lại.", LabToasts.Tone.Warn, 2.6f);
                return;
            }
            float takeMl = Mathf.Min(amountMl, curBuret.container.VolumeMl);
            var portion = curBuret.container.Take(takeMl);
            var rx = curBuret.Receiver();
            if (rx != null)
            {
                rx.Add(portion);
                LabToasts.Push($"Đã nhỏ {label} ({portion.volumeMl.ToString("0.00", Vi)} mL) vào {rx.label}.", LabToasts.Tone.Good, 2.0f);
            }
            else
            {
                LabToasts.Push($"Đã xả {label} ({portion.volumeMl.ToString("0.00", Vi)} mL) — chưa có bình nhận bên dưới đầu buret.", LabToasts.Tone.Warn, 2.5f);
            }
            RefreshPreview();
            UpdateBuretLive();
        }

        void CloseBuretStopcock()
        {
            if (curBuret == null) return;
            curBuret.Close();
            if (buretSlider != null) buretSlider.SetValueWithoutNotify(0f);
            if (buretSliderVal != null) buretSliderVal.text = "0%";
            UpdateBuretLive();
            LabToasts.Push("Đã khóa buret.", LabToasts.Tone.Info, 1.8f);
        }

        void TopUpBuret()
        {
            if (curBuret == null || curBuret.container == null) return;
            float targetVol = curBuret.zeroVolumeMl + 1.5f;
            float need = targetVol - curBuret.container.VolumeMl;
            if (need <= 0.4f)
            {
                LabToasts.Push("Buret đã đầy trên vạch 0.", LabToasts.Tone.Info, 2.0f);
                return;
            }

            var proc = FindAnyObjectByType<LabProcedure>();
            if (proc != null && proc.naohStock != null && proc.naohStock != curBuret.container && proc.naohStock.VolumeMl > 1f)
            {
                var taken = proc.naohStock.Take(Mathf.Min(need, proc.naohStock.VolumeMl));
                curBuret.container.Add(taken);
                LabToasts.Push($"Đã rót thêm {taken.volumeMl.ToString("0.0", Vi)} mL từ {proc.naohStock.label}.", LabToasts.Tone.Good, 2.2f);
            }
            else
            {
                var cat = ChemicalCatalog.Instance;
                var entry = cat != null ? cat.Get(124) : null;
                var sol = entry != null ? cat.Make(entry, need, 0.1f) : Solution.NaOH(need, 0.1f);
                curBuret.container.Add(sol);
                LabToasts.Push($"Đã nạp thêm {need.ToString("0.0", Vi)} mL dung dịch chuẩn vào buret.", LabToasts.Tone.Good, 2.2f);
            }
            RefreshPreview();
            UpdateBuretLive();
        }

        void PurgeBuretBubble()
        {
            if (curBuret == null || curBuret.container == null) return;
            if (!curBuret.HasLiquid)
            {
                LabToasts.Push("Chưa có dung dịch trong buret để đuổi bọt khí.", LabToasts.Tone.Warn, 2.4f);
                return;
            }
            float purgeMl = Mathf.Min(0.6f, Mathf.Max(0.1f, curBuret.container.VolumeMl - 0.5f));
            var portion = curBuret.container.Take(purgeMl);
            var proc = FindAnyObjectByType<LabProcedure>();
            if (proc != null && proc.wasteBeaker != null) proc.wasteBeaker.Add(portion);
            bubblePurged = true;
            RefreshPreview();
            UpdateBuretLive();
            LabToasts.Push("Đã đuổi bọt khí ở đầu vuốt buret.", LabToasts.Tone.Good, 2.4f);
        }

        void ZeroBuretMeniscus()
        {
            if (curBuret == null || curBuret.container == null) return;
            var proc = FindAnyObjectByType<LabProcedure>();
            float diff = curBuret.container.VolumeMl - curBuret.zeroVolumeMl;
            if (diff > 0.02f)
            {
                var drained = curBuret.container.Take(diff);
                if (proc != null && proc.wasteBeaker != null) proc.wasteBeaker.Add(drained);
            }
            else if (diff < -0.05f)
            {
                float need = -diff;
                if (proc != null && proc.naohStock != null && proc.naohStock.VolumeMl > need)
                    curBuret.container.Add(proc.naohStock.Take(need));
                else
                {
                    var cat = ChemicalCatalog.Instance;
                    var entry = cat != null ? cat.Get(124) : null;
                    curBuret.container.Add(entry != null ? cat.Make(entry, need, 0.1f) : Solution.NaOH(need, 0.1f));
                }
            }
            curBuret.ResetCounters();
            RefreshPreview();
            UpdateBuretLive();
            LabToasts.Push("Đã chỉnh đáy mặt khum về đúng vạch 0,00 mL.", LabToasts.Tone.Good, 2.2f);
        }

        void RecordBuretReading()
        {
            if (curBuret == null) return;
            var proc = FindAnyObjectByType<LabProcedure>();
            if (proc != null && proc.Active)
            {
                bool wasOpen = curBuret.Opening > 0.001f;
                if (wasOpen) CloseBuretStopcock();
                proc.RecordReading();
            }
            else
            {
                LabToasts.Push($"Số đọc buret hiện tại: {curBuret.ReadingMl.ToString("0.00", Vi)} mL.", LabToasts.Tone.Good, 2.6f, "Ghi số đọc");
            }
            UpdateBuretLive();
        }

        // ------------------------------------------------------------------
        // Biến thể B: Chai hóa chất (42% trái / 58% phải)
        // ------------------------------------------------------------------

        void BuildBottleBody(ChemicalBottle bottle, ChemEntry entry, string hazard)
        {
            float lw = leftCol.sizeDelta.x;
            float rw = rightCol.sizeDelta.x;

            // Cột trái: ảnh 3D lớn + nhãn hóa chất chi tiết ở cạnh dưới
            var leftCard = MakeWellCard(leftCol, "Khung xem trước chai", Vector2.zero, new Vector2(lw, BodyH));
            float sq = Mathf.Min(lw - 24f, BodyH - 190f);
            BuildPreviewBox(leftCard, new Vector2(sq, sq), $"{bottle.DisplayName}\n{entry?.Formula ?? ""}", topOffsetY: -14f);
            BuildBottleLabelCard(leftCard, bottle, entry, hazard, lw);

            // Cột phải: 2x2 Readout -> Rót vào -> Lượng rót slider -> Pha loãng -> Dự đoán -> Cảnh báo đỏ
            float colW2 = (rw - 14f) * 0.5f;
            float rx2 = colW2 + 14f;

            string conc = FormatConcentration(entry);
            string phState = FormatBottlePhState(bottle, entry);
            string remain = FormatBottleRemaining(bottle);
            string shelf = FormatBottleShelf(entry);

            GlassKit.Readout(rightCol, "Nồng độ", "Nồng độ", conc, new Vector2(0f, 0f), new Vector2(colW2, 56f));
            bottlePhVal = GlassKit.Readout(rightCol, "pH / Trạng thái", "pH / Trạng thái", phState, new Vector2(rx2, 0f), new Vector2(colW2, 56f), GlassKit.Ink);
            bottleRemainVal = GlassKit.Readout(rightCol, "Còn lại", "Còn lại", remain, new Vector2(0f, -62f), new Vector2(colW2, 56f));
            GlassKit.Readout(rightCol, "Kệ", "Kệ", shelf, new Vector2(rx2, -62f), new Vector2(colW2, 56f), GlassKit.Ink);

            // Rót vào (chọn 1 trong nhóm)
            string pourSectionTitle = bottle.IsSolid ? "Xúc vào" : (bottle.IsGas ? "Sục khí vào" : "Rót vào");
            BuildReceiverSelector(rightCol, pourSectionTitle, -126f, rw, OnReceiverSelectionChanged);

            // Lượng rót / xúc slider + vạch chia (cách rõ bên dưới núm tròn >= 14 px)
            BuildTransferSlider(rightCol, bottle.IsSolid, -202f, rw, OnBottleAmountChanged);

            // Pha loãng C1·V1 = C2·V2 (chỉ bật tính toán khi là dung dịch có C1 > 0)
            dilutionStockC1 = GetStockMolarity(bottle, entry);
            float predY;
            float predH;
            if (dilutionStockC1 > 0.0001f)
            {
                BuildDilutionCard(rightCol, dilutionStockC1, -294f, rw, 136f);
                predY = -438f;
                predH = 240f;
            }
            else
            {
                BuildDilutionUnavailableCard(rightCol, bottle, entry, -294f, rw, 48f);
                predY = -350f;
                predH = 326f;
            }

            // Dự đoán khi trộn (tối đa 3 dòng)
            BuildPredictionCard(rightCol, predY, rw, predH);

            // Dải cảnh báo đỏ
            string note = !string.IsNullOrEmpty(entry?.ghi_chu)
                ? entry.ghi_chu
                : (!string.IsNullOrEmpty(hazard)
                    ? $"Cảnh báo {hazard.ToLowerInvariant()}: đeo kính, găng tay và đọc kỹ nhãn trước khi rót."
                    : "Đeo kính và găng tay. Đọc kỹ nhãn trước khi rót.");
            GlassKit.Banner(rightCol, "Cảnh báo hóa chất", note, GlassKit.Bad, GlassKit.BadSoft, new Vector2(0f, -684f), new Vector2(rw, 56f));

            RefreshPreview();
            RefreshMixPredictions();
        }

        /// <summary>Lấy nồng độ mol gốc C₁ (M) của chai hóa chất; trả về 0 với chất rắn, khí, giấy thử hoặc không rõ C₁.</summary>
        public static float GetStockMolarity(ChemicalBottle bottle, ChemEntry entry)
        {
            if (bottle == null || bottle.IsSolid || bottle.IsGas || bottle.IsPaper) return 0f;
            if (entry != null)
            {
                if (entry.IsSolid || entry.IsGas || entry.IsPaper) return 0f;
                if (entry.trang_thai == "dd" && !entry.tp_theo_lit && entry.C > 0.0001f)
                    return entry.C;
                return 0f;
            }

            if (bottle.container != null && bottle.container.VolumeMl > 0.1f)
            {
                double volL = bottle.container.VolumeMl / 1000.0;
                double maxC = 0.0;
                var mix = bottle.container.Total.Mix;
                for (int i = 0; i < mix.Count; i++)
                {
                    var d = mix.SpeciesAt(i);
                    if (d.Kind == SpeciesKind.Ion || d.Kind == SpeciesKind.Weak || d.Kind == SpeciesKind.Complex)
                        maxC = Math.Max(maxC, mix.MolAt(i) / volL);
                }
                return maxC > 0.001 ? (float)maxC : 0f;
            }
            return 0f;
        }

        /// <summary>Tạo danh sách nồng độ đích C₂ nhỏ hơn C₁ (thêm C₁/2 khi C₁ ≤ 0,1 M).</summary>
        public static void BuildDilutionTargetConcentrations(float c1, List<float> into)
        {
            into.Clear();
            if (float.IsNaN(c1) || float.IsInfinity(c1) || c1 <= 0.0001f) return;

            float[] std = { 0.1f, 0.2f, 0.5f };
            for (int i = 0; i < std.Length; i++)
                if (std[i] < c1 - 0.0005f) into.Add(std[i]);

            if (c1 <= 0.1005f)
            {
                float quarter = Mathf.Round(c1 * 2500f) / 10000f;
                float half = Mathf.Round(c1 * 5000f) / 10000f;
                if (quarter > 0.0001f && !into.Contains(quarter)) into.Add(quarter);
                if (half > 0.0001f && !into.Contains(half)) into.Add(half);
            }
            else if (into.Count == 1)
            {
                float half = Mathf.Round(c1 * 5000f) / 10000f;
                if (half > 0.0001f && half < c1 - 0.0001f && !into.Contains(half))
                    into.Insert(0, half);
            }
        }

        /// <summary>Tính thể tích gốc V₁ và nước cất V_nước theo công thức pha loãng C₁·V₁ = C₂·V₂.</summary>
        public static bool TryComputeDilution(float c1, float c2, float v2Ml, out float v1Ml, out float vWaterMl)
        {
            v1Ml = 0f;
            vWaterMl = 0f;
            if (float.IsNaN(c1) || float.IsInfinity(c1) || c1 <= 0.0001f) return false;
            if (float.IsNaN(c2) || float.IsInfinity(c2) || c2 <= 0f || float.IsNaN(v2Ml) || float.IsInfinity(v2Ml) || v2Ml <= 0f) return false;
            float rawV1 = (c2 * v2Ml) / c1;
            if (float.IsNaN(rawV1) || float.IsInfinity(rawV1)) return false;
            v1Ml = Mathf.Clamp(rawV1, 0f, v2Ml);
            vWaterMl = Mathf.Max(0f, v2Ml - v1Ml);
            return true;
        }

        void BuildDilutionCard(Transform parent, float stockC1, float y, float width, float height)
        {
            var card = MakeWellCard(parent, "Pha loãng", new Vector2(0f, y), new Vector2(width, height));
            GlassKit.Label(card, "Tiêu đề", "Pha loãng (C₁·V₁ = C₂·V₂)", 14, GlassKit.Ink, true, TextAnchor.MiddleLeft, new Vector2(14f, -8f), new Vector2(280f, 22f));
            GlassKit.Label(card, "Gốc C1", $"Gốc C₁ = {FormatConcentrationM(stockC1)}", 13, GlassKit.Dim, false, TextAnchor.MiddleRight, new Vector2(300f, -8f), new Vector2(width - 314f, 22f));

            BuildDilutionTargetConcentrations(stockC1, dilutionC2Values);
            dilutionV2Values.Clear();
            dilutionV2Values.Add(50f);
            dilutionV2Values.Add(100f);
            dilutionV2Values.Add(250f);

            if (dilutionC2Values.Count == 0)
            {
                GlassKit.Label(card, "Ghi chú", "Nồng độ gốc quá nhỏ để pha loãng tiếp.", 13, GlassKit.Dim, false, TextAnchor.MiddleLeft, new Vector2(14f, -44f), new Vector2(width - 28f, 44f));
                return;
            }

            selectedDilutionC2Idx = Mathf.Clamp(selectedDilutionC2Idx, 0, dilutionC2Values.Count - 1);
            selectedDilutionV2Idx = Mathf.Clamp(selectedDilutionV2Idx, 0, dilutionV2Values.Count - 1);
            dilutionC2Chips.Clear();
            dilutionV2Chips.Clear();

            const float c2ChipW = 100f;
            const float chipW = 104f;
            const float chipH = 44f;
            const float chipGap = 6f;

            GlassKit.Label(card, "Nhãn C2", "C₂:", 13, GlassKit.Dim, true, TextAnchor.MiddleLeft, new Vector2(14f, -34f), new Vector2(34f, chipH));
            for (int i = 0; i < dilutionC2Values.Count; i++)
            {
                int idx = i;
                string cLabel = FormatConcentrationM(dilutionC2Values[i]);
                var chip = GlassKit.MakeChip(card, $"Nồng độ đích {i}", cLabel, new Vector2(50f + i * (c2ChipW + chipGap), -34f), new Vector2(c2ChipW, chipH), () =>
                {
                    selectedDilutionC2Idx = idx;
                    for (int k = 0; k < dilutionC2Chips.Count; k++)
                        GlassKit.SetChip(dilutionC2Chips[k], k == selectedDilutionC2Idx);
                    UpdateDilutionUi();
                }, i == selectedDilutionC2Idx, 14);
                dilutionC2Chips.Add(chip);
            }

            GlassKit.Fill(card, "Vạch pha loãng", new Vector2(378f, -39f), new Vector2(1f, 34f), GlassKit.Line);
            GlassKit.Label(card, "Nhãn V2", "V₂:", 13, GlassKit.Dim, true, TextAnchor.MiddleLeft, new Vector2(392f, -34f), new Vector2(34f, chipH));
            for (int i = 0; i < dilutionV2Values.Count; i++)
            {
                int idx = i;
                string vLabel = $"{dilutionV2Values[i]:0} mL";
                var chip = GlassKit.MakeChip(card, $"Thể tích đích {i}", vLabel, new Vector2(428f + i * (chipW + chipGap), -34f), new Vector2(chipW, chipH), () =>
                {
                    selectedDilutionV2Idx = idx;
                    for (int k = 0; k < dilutionV2Chips.Count; k++)
                        GlassKit.SetChip(dilutionV2Chips[k], k == selectedDilutionV2Idx);
                    UpdateDilutionUi();
                }, i == selectedDilutionV2Idx, 14);
                dilutionV2Chips.Add(chip);
            }

            const float applyW = 168f;
            float formulaW = Mathf.Max(220f, width - 28f - applyW - 14f);
            dilutionFormulaText = GlassKit.Label(card, "Công thức V1", "", 13, GlassKit.Ink, true, TextAnchor.MiddleLeft, new Vector2(14f, -84f), new Vector2(formulaW, 44f));
            dilutionFormulaText.horizontalOverflow = HorizontalWrapMode.Wrap;
            GlassKit.MakeButton(card, "Áp dụng V1", "Áp dụng V₁", new Vector2(width - 14f - applyW, -84f), new Vector2(applyW, 44f), ApplyDilutionV1, false, 14);

            UpdateDilutionUi();
        }

        void BuildDilutionUnavailableCard(Transform parent, ChemicalBottle bottle, ChemEntry entry, float y, float width, float height)
        {
            var card = MakeWellCard(parent, "Pha loãng", new Vector2(0f, y), new Vector2(width, height));
            string reason = (entry != null && entry.IsSolid) || (bottle != null && bottle.IsSolid)
                ? "Chất rắn không áp dụng công thức thể tích dung dịch gốc."
                : ((entry != null && entry.IsGas) || (bottle != null && bottle.IsGas)
                    ? "Chất khí không áp dụng công thức pha loãng."
                    : ((entry != null && entry.IsPaper) || (bottle != null && bottle.IsPaper)
                        ? "Giấy thử pH không pha loãng."
                        : "Không có nồng độ mol (M) xác định để tính C₁·V₁ = C₂·V₂."));
            GlassKit.Label(card, "Tiêu đề", "Pha loãng (C₁·V₁ = C₂·V₂):", 13, GlassKit.Ink, true, TextAnchor.MiddleLeft, new Vector2(14f, 0f), new Vector2(256f, height));
            GlassKit.Label(card, "Ghi chú", reason, 13, GlassKit.Dim, false, TextAnchor.MiddleLeft, new Vector2(272f, 0f), new Vector2(width - 286f, height));
        }

        void UpdateDilutionUi()
        {
            if (dilutionFormulaText == null || dilutionC2Values.Count == 0 || dilutionV2Values.Count == 0) return;
            int ci = Mathf.Clamp(selectedDilutionC2Idx, 0, dilutionC2Values.Count - 1);
            int vi = Mathf.Clamp(selectedDilutionV2Idx, 0, dilutionV2Values.Count - 1);
            float c2 = dilutionC2Values[ci];
            float v2 = dilutionV2Values[vi];
            if (!TryComputeDilution(dilutionStockC1, c2, v2, out float v1, out float vWater))
            {
                dilutionFormulaText.text = "Không tính được thể tích pha loãng hợp lệ.";
                return;
            }
            dilutionFormulaText.text = $"Lấy V₁ = {FormatDilutionVol(v1)} gốc + V_nước = {FormatDilutionVol(vWater)} nước cất → {v2:0} mL dung dịch {FormatConcentrationM(c2)}";
        }

        void ApplyDilutionV1()
        {
            if (dilutionC2Values.Count == 0 || dilutionV2Values.Count == 0) return;
            int ci = Mathf.Clamp(selectedDilutionC2Idx, 0, dilutionC2Values.Count - 1);
            int vi = Mathf.Clamp(selectedDilutionV2Idx, 0, dilutionV2Values.Count - 1);
            if (!TryComputeDilution(dilutionStockC1, dilutionC2Values[ci], dilutionV2Values[vi], out float v1, out _))
                return;

            float minMl = bottleTransferSlider != null ? bottleTransferSlider.minValue : 5f;
            float maxMl = bottleTransferSlider != null ? bottleTransferSlider.maxValue : 100f;
            float clampedV1 = Mathf.Clamp(Mathf.Round(v1), minMl, maxMl);

            if (bottleTransferSlider != null)
            {
                bool changed = !Mathf.Approximately(bottleTransferSlider.value, clampedV1);
                bottleTransferSlider.value = clampedV1;
                if (!changed)
                {
                    transferAmountMl = clampedV1;
                    OnBottleAmountChanged();
                }
            }
            else
            {
                transferAmountMl = clampedV1;
                OnBottleAmountChanged();
            }

            if (v1 < minMl - 0.5f)
                LabToasts.Push($"V₁ = {FormatDilutionVol(v1)} (đặt thanh rót tối thiểu {minMl:0} mL).", LabToasts.Tone.Info, 2.5f);
            else if (v1 > maxMl + 0.5f)
                LabToasts.Push($"V₁ = {FormatDilutionVol(v1)} (giới hạn thanh rót tối đa {maxMl:0} mL).", LabToasts.Tone.Info, 2.5f);
            else
                LabToasts.Push($"Đã áp dụng V₁ = {clampedV1:0} mL vào Lượng rót.", LabToasts.Tone.Good, 2.2f);
        }

        static string FormatConcentrationM(float c) => $"{c.ToString("0.####", Vi)} M";

        static string FormatDilutionVol(float ml) => ml < 10f ? $"{ml.ToString("0.##", Vi)} mL" : $"{ml.ToString("0.#", Vi)} mL";

        void UpdateBottleLive()
        {
            if (curBottle == null) return;
            var e = curBottle.Entry;
            if (bottleRemainVal != null) bottleRemainVal.text = FormatBottleRemaining(curBottle);
            if (bottlePhVal != null) bottlePhVal.text = FormatBottlePhState(curBottle, e);
            if (bottleLabelRemainText != null)
                bottleLabelRemainText.text = $"Còn lại: {FormatBottleRemaining(curBottle)}  ·  Vị trí: {FormatBottleShelf(e)}";
        }

        void OnReceiverSelectionChanged()
        {
            RefreshMixPredictions();
            if (secondaryActionText != null && curBottle != null)
                secondaryActionText.text = FormatBottleActionLabel(curBottle);
        }

        void OnBottleAmountChanged()
        {
            RefreshMixPredictions();
            if (secondaryActionText != null && curBottle != null)
                secondaryActionText.text = FormatBottleActionLabel(curBottle);
        }

        void ExecuteBottleTransfer()
        {
            if (curBottle == null) return;
            var rx = SelectedReceiver;
            if (rx == null)
            {
                LabToasts.Push("Chưa chọn vật chứa nhận ở gần để chuyển hóa chất.", LabToasts.Tone.Warn, 2.5f);
                return;
            }

            if (curBottle.IsPaper)
            {
                LabToasts.Push($"Giấy thử {curBottle.DisplayName}: độ pH của {rx.label} đang là {rx.BulkPh.ToString("0.00", Vi)}.", LabToasts.Tone.Info, 2.8f);
                return;
            }

            if (curBottle.IsGas)
            {
                float mol = curBottle.BubbleInto(rx, 5f);
                if (mol <= 0f) LabToasts.Push("Bình khí đã hết hoặc vật chứa nhận chưa có nước.", LabToasts.Tone.Warn, 2.5f);
                else LabToasts.Push($"Đã sục {mol.ToString("0.000", Vi)} mol {curBottle.DisplayName} vào {rx.label}.", LabToasts.Tone.Good, 2.4f);
                UpdateBottleLive();
                RefreshMixPredictions();
                return;
            }

            if (curBottle.IsSolid)
            {
                int done = 0;
                for (int i = 0; i < transferScoops; i++)
                    if (curBottle.ScoopInto(rx)) done++;
                if (done <= 0) LabToasts.Push("Lọ chất rắn đã cạn.", LabToasts.Tone.Warn, 2.4f);
                else LabToasts.Push($"Đã xúc {done} thìa {curBottle.DisplayName} vào {rx.label}.", LabToasts.Tone.Good, 2.4f);
                UpdateBottleLive();
                RefreshMixPredictions();
                return;
            }

            if (curBottle.container == null || curBottle.container.VolumeMl <= 0.1f)
            {
                LabToasts.Push("Chai hóa chất đã hết dung dịch.", LabToasts.Tone.Warn, 2.4f);
                return;
            }

            float avail = curBottle.container.VolumeMl;
            float room = Mathf.Max(0f, rx.FreeMl);
            if (room < 0.5f)
            {
                LabToasts.Push($"{rx.label} đã đầy ({rx.VolumeMl.ToString("0", Vi)}/{rx.capacityMl.ToString("0", Vi)} mL).", LabToasts.Tone.Warn, 2.6f);
                return;
            }

            float ml = Mathf.Min(transferAmountMl, avail, room);
            var portion = curBottle.container.Take(ml);
            rx.Add(portion);
            LabToasts.Push($"Đã rót {portion.volumeMl.ToString("0.#", Vi)} mL {curBottle.DisplayName} vào {rx.label}.", LabToasts.Tone.Good, 2.4f);
            RefreshPreview();
            UpdateBottleLive();
            RefreshMixPredictions();
        }

        // ------------------------------------------------------------------
        // Biến thể C: Dụng cụ thủy tinh & vật thể chung (42% / 58%)
        // ------------------------------------------------------------------

        void BuildContainerBody(LabContainer cont, EquipmentInfo info)
        {
            float lw = leftCol.sizeDelta.x;
            float rw = rightCol.sizeDelta.x;

            var leftCard = MakeWellCard(leftCol, "Khung xem trước dụng cụ", Vector2.zero, new Vector2(lw, BodyH));
            float sq = Mathf.Min(lw - 24f, BodyH - 36f);
            BuildPreviewBox(leftCard, new Vector2(sq, sq), !string.IsNullOrEmpty(cont.label) ? cont.label : cont.gameObject.name);

            float colW2 = (rw - 14f) * 0.5f;
            float rx2 = colW2 + 14f;

            contVolVal = GlassKit.Readout(rightCol, "Thể tích", "Thể tích", $"{cont.VolumeMl.ToString("0.#", Vi)} / {cont.capacityMl.ToString("0", Vi)} mL", new Vector2(0f, 0f), new Vector2(colW2, 68f));
            contPhVal = GlassKit.Readout(rightCol, "pH", "pH", cont.VolumeMl > 0.1f ? cont.BulkPh.ToString("0.00", Vi) : "—", new Vector2(rx2, 0f), new Vector2(colW2, 68f));
            contTempVal = GlassKit.Readout(rightCol, "Nhiệt độ", "Nhiệt độ", $"{cont.TemperatureC.ToString("0.0", Vi)} °C", new Vector2(0f, -80f), new Vector2(colW2, 68f));
            contCompVal = GlassKit.Readout(rightCol, "Thành phần", "Thành phần", DescribeContainerShort(cont), new Vector2(rx2, -80f), new Vector2(colW2, 68f), GlassKit.Ink);

            // Hộp mô tả chi tiết thành phần hiện có
            var detailBox = MakeWellCard(rightCol, "Chi tiết thành phần", new Vector2(0f, -162f), new Vector2(rw, 112f));
            GlassKit.Label(detailBox, "Nhãn", "Thành phần hiện có", 13, GlassKit.Dim, true, TextAnchor.UpperLeft, new Vector2(14f, -10f), new Vector2(rw - 28f, 20f));
            contDetailText = GlassKit.Label(detailBox, "Mô tả", cont.Total.Describe(6), 16, GlassKit.Ink, false, TextAnchor.UpperLeft, new Vector2(14f, -34f), new Vector2(rw - 28f, 68f));

            if (!cont.Total.IsEmpty && receivers.Count > 0)
            {
                BuildReceiverSelector(rightCol, "Rót sang", -290f, rw, () =>
                {
                    if (secondaryActionText != null) secondaryActionText.text = $"Rót {transferAmountMl.ToString("0.#", Vi)} mL";
                });

                float minPour = cont.VolumeMl < 5f ? 0.5f : 5f;
                float maxPour = Mathf.Clamp(cont.VolumeMl, minPour, 100f);
                transferAmountMl = Mathf.Clamp(transferAmountMl, minPour, maxPour);
                if (secondaryActionText != null) secondaryActionText.text = $"Rót {transferAmountMl.ToString("0.#", Vi)} mL";
                GlassKit.MakeSlider(
                    rightCol,
                    "Lượng rót",
                    "Lượng rót",
                    new Vector2(0f, -378f),
                    new Vector2(rw, 68f),
                    minPour,
                    maxPour,
                    transferAmountMl,
                    v =>
                    {
                        transferAmountMl = v;
                        if (secondaryActionText != null) secondaryActionText.text = $"Rót {transferAmountMl.ToString("0.#", Vi)} mL";
                    },
                    v => $"{v.ToString("0.#", Vi)} mL",
                    true);
            }
            else
            {
                string emptyMsg = cont.Total.IsEmpty
                    ? "Vật chứa hiện đang trống. Cầm chai hóa chất hoặc pipet để nạp dung dịch."
                    : "Chưa tìm thấy bình chứa khác ở gần để rót sang.";
                GlassKit.Banner(rightCol, "Thông tin", emptyMsg, GlassKit.Accent, GlassKit.AccentSoft, new Vector2(0f, -290f), new Vector2(rw, 56f));
            }

            string usageNote = info != null && !string.IsNullOrEmpty(info.usage)
                ? info.usage
                : "Rót chậm theo thành bình và lắc đều sau khi pha trộn.";
            GlassKit.Banner(rightCol, "Ghi chú dụng cụ", usageNote, GlassKit.Warn, GlassKit.WarnSoft, new Vector2(0f, -670f), new Vector2(rw, 56f));

            RefreshPreview();
        }

        void UpdateContainerLive()
        {
            if (curContainer == null) return;
            if (contVolVal != null) contVolVal.text = $"{curContainer.VolumeMl.ToString("0.#", Vi)} / {curContainer.capacityMl.ToString("0", Vi)} mL";
            if (contPhVal != null) contPhVal.text = curContainer.VolumeMl > 0.1f ? curContainer.BulkPh.ToString("0.00", Vi) : "—";
            if (contTempVal != null) contTempVal.text = $"{curContainer.TemperatureC.ToString("0.0", Vi)} °C";
            if (contCompVal != null) contCompVal.text = DescribeContainerShort(curContainer);
            if (contDetailText != null) contDetailText.text = curContainer.Total.Describe(6);
        }

        void ExecuteContainerTransfer()
        {
            if (curContainer == null) return;
            var rx = SelectedReceiver;
            if (rx == null)
            {
                LabToasts.Push("Chưa chọn vật chứa nhận.", LabToasts.Tone.Warn, 2.4f);
                return;
            }
            if (curContainer.VolumeMl <= 0.05f)
            {
                LabToasts.Push("Bình nguồn đang trống.", LabToasts.Tone.Warn, 2.4f);
                return;
            }
            float room = Mathf.Max(0f, rx.FreeMl);
            if (room < 0.5f)
            {
                LabToasts.Push($"{rx.label} đã đầy.", LabToasts.Tone.Warn, 2.4f);
                return;
            }
            float ml = Mathf.Min(transferAmountMl, curContainer.VolumeMl, room);
            var part = curContainer.Take(ml);
            rx.Add(part);
            LabToasts.Push($"Đã rót {part.volumeMl.ToString("0.#", Vi)} mL sang {rx.label}.", LabToasts.Tone.Good, 2.4f);
            RefreshPreview();
            UpdateContainerLive();
        }

        void BuildGenericObjectBody(GameObject target, EquipmentInfo info, IDemoUsable usable, Grabbable grab)
        {
            float lw = leftCol.sizeDelta.x;
            float rw = rightCol.sizeDelta.x;

            var leftCard = MakeWellCard(leftCol, "Khung xem trước dụng cụ", Vector2.zero, new Vector2(lw, BodyH));
            float sq = Mathf.Min(lw - 24f, BodyH - 36f);
            BuildPreviewBox(leftCard, new Vector2(sq, sq), info != null && !string.IsNullOrEmpty(info.title) ? info.title : target.name);

            float colW2 = (rw - 14f) * 0.5f;
            float rx2 = colW2 + 14f;
            string kindStr = usable != null ? "Dụng cụ tương tác" : (grab != null ? "Dụng cụ cầm tay" : "Thiết bị thí nghiệm");
            var hand = FindAnyObjectByType<HandController>();
            string stateStr = hand != null && hand.Held != null && hand.Held.gameObject == target ? "Đang cầm" : "Sẵn sàng";
            GlassKit.Readout(rightCol, "Loại", "Phân loại", kindStr, new Vector2(0f, 0f), new Vector2(colW2, 68f), GlassKit.Ink);
            GlassKit.Readout(rightCol, "Trạng thái", "Trạng thái", stateStr, new Vector2(rx2, 0f), new Vector2(colW2, 68f));

            var usageCard = MakeWellCard(rightCol, "Công dụng", new Vector2(0f, -84f), new Vector2(rw, 160f));
            GlassKit.Label(usageCard, "Nhãn", "Chức năng & thao tác", 13, GlassKit.Dim, true, TextAnchor.UpperLeft, new Vector2(14f, -10f), new Vector2(rw - 28f, 20f));
            string usage = info != null && !string.IsNullOrEmpty(info.usage)
                ? info.usage
                : (info != null && !string.IsNullOrEmpty(info.description)
                    ? info.description
                    : "Dụng cụ phòng thí nghiệm tiêu chuẩn. Bấm 'Cầm lên' để thao tác trực tiếp trên bàn.");
            GlassKit.Label(usageCard, "Nội dung", usage, 16, GlassKit.Ink, false, TextAnchor.UpperLeft, new Vector2(14f, -38f), new Vector2(rw - 28f, 110f));

            GlassKit.Banner(rightCol, "Lưu ý", "Kiểm tra sạch sẽ và nguyên vẹn của dụng cụ trước khi sử dụng.", GlassKit.Accent, GlassKit.AccentSoft, new Vector2(0f, -670f), new Vector2(rw, 56f));
            RefreshPreview();
        }

        // ------------------------------------------------------------------
        // Khối dùng chung cho Chai hóa chất & Bình chứa
        // ------------------------------------------------------------------

        void BuildReceiverSelector(Transform parent, string title, float y, float width, Action onSelectChanged)
        {
            GlassKit.Label(parent, "Tiêu đề nhận", title, 14, GlassKit.Ink, true, TextAnchor.MiddleLeft, new Vector2(0f, y), new Vector2(width, 22f));
            receiverChips.Clear();

            if (receivers.Count == 0)
            {
                var box = MakeWellCard(parent, "Không có bình nhận", new Vector2(0f, y - 26f), new Vector2(width, 44f));
                GlassKit.Label(box, "Thông báo", "Chưa có bình chứa trống hoặc vật nhận nào gần đây.", 14, GlassKit.Dim, false, TextAnchor.MiddleLeft, new Vector2(12f, 0f), new Vector2(width - 24f, 44f));
                return;
            }

            float gap = 10f;
            int count = receivers.Count;
            float cw = Mathf.Floor((width - gap * (count - 1)) / count);
            for (int i = 0; i < count; i++)
            {
                int idx = i;
                var c = receivers[i];
                string cname = !string.IsNullOrEmpty(c.label) ? c.label : c.gameObject.name;
                var chip = GlassKit.MakeChip(parent, $"Nhận {i}", cname, new Vector2(i * (cw + gap), y - 26f), new Vector2(cw, 44f), () =>
                {
                    selectedReceiverIdx = idx;
                    for (int k = 0; k < receiverChips.Count; k++)
                        GlassKit.SetChip(receiverChips[k], k == selectedReceiverIdx);
                    onSelectChanged?.Invoke();
                }, i == selectedReceiverIdx, 14);
                receiverChips.Add(chip);
            }
        }

        void BuildTransferSlider(Transform parent, bool isSolid, float y, float width, Action onChanged)
        {
            if (isSolid)
            {
                float scoopG = curBottle != null && curBottle.scoopGrams > 0.01f ? curBottle.scoopGrams : 0.3f;
                bottleTransferSlider = GlassKit.MakeSlider(
                    parent,
                    "Lượng xúc",
                    "Lượng xúc",
                    new Vector2(0f, y),
                    new Vector2(width, 62f),
                    1f,
                    10f,
                    transferScoops,
                    v =>
                    {
                        transferScoops = Mathf.Clamp(Mathf.RoundToInt(v), 1, 10);
                        onChanged?.Invoke();
                    },
                    v => $"{Mathf.RoundToInt(v)} thìa ({(Mathf.RoundToInt(v) * scoopG).ToString("0.#", Vi)} g)",
                    true);
                DrawSliderTicks(parent, y - 68f, width, new[] { 1f, 3f, 5f, 8f, 10f }, new[] { "1 thìa", "3 thìa", "5 thìa", "8 thìa", "10 thìa" }, 1f, 10f);
            }
            else
            {
                bottleTransferSlider = GlassKit.MakeSlider(
                    parent,
                    "Lượng rót",
                    "Lượng rót",
                    new Vector2(0f, y),
                    new Vector2(width, 62f),
                    5f,
                    100f,
                    transferAmountMl,
                    v =>
                    {
                        transferAmountMl = Mathf.Clamp(Mathf.Round(v), 5f, 100f);
                        onChanged?.Invoke();
                    },
                    v => $"{v:0} mL",
                    true);
                DrawSliderTicks(parent, y - 68f, width, new[] { 5f, 25f, 50f, 75f, 100f }, new[] { "5", "25", "50", "75", "100 mL" }, 5f, 100f);
            }
        }

        void DrawSliderTicks(Transform parent, float y, float width, float[] values, string[] labels, float min, float max)
        {
            const float trackPad = 13f;
            float usableW = Mathf.Max(40f, width - trackPad * 2f);
            for (int i = 0; i < values.Length; i++)
            {
                float t = Mathf.Clamp01((values[i] - min) / Mathf.Max(0.001f, max - min));
                float cx = trackPad + t * usableW;
                if (i == 0)
                {
                    bool compact = labels[i].Length <= 3;
                    float x0 = compact ? 0f : trackPad;
                    float w0 = compact ? 26f : 72f;
                    TextAnchor a0 = compact ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
                    GlassKit.Label(parent, $"Vạch {i}", labels[i], 12, GlassKit.Muted, false, a0, new Vector2(x0, y), new Vector2(w0, 20f));
                }
                else if (i == values.Length - 1)
                {
                    const float wLast = 84f;
                    GlassKit.Label(parent, $"Vạch {i}", labels[i], 12, GlassKit.Muted, false, TextAnchor.MiddleRight, new Vector2(width - trackPad - wLast, y), new Vector2(wLast, 20f));
                }
                else
                {
                    const float wMid = 64f;
                    float xm = Mathf.Clamp(cx - wMid * 0.5f, 0f, width - wMid);
                    GlassKit.Label(parent, $"Vạch {i}", labels[i], 12, GlassKit.Muted, false, TextAnchor.MiddleCenter, new Vector2(xm, y), new Vector2(wMid, 20f));
                }
            }
        }

        void BuildPredictionCard(Transform parent, float y, float width, float height)
        {
            var card = MakeWellCard(parent, "Dự đoán khi trộn", new Vector2(0f, y), new Vector2(width, height));
            GlassKit.Label(card, "Tiêu đề", "Dự đoán khi trộn", 14, GlassKit.Ink, true, TextAnchor.MiddleLeft, new Vector2(14f, -6f), new Vector2(260f, 22f));
            predHeaderSub = GlassKit.Label(card, "Mục tiêu", "", 13, GlassKit.Dim, false, TextAnchor.MiddleRight, new Vector2(280f, -6f), new Vector2(width - 294f, 22f));

            predRows.Clear();
            float availRowsH = Mathf.Max(150f, height - 42f);
            float rowW = width - 28f;
            float provisional = Mathf.Floor((availRowsH - 12f) / 3f);
            bool compact = provisional < 68f;
            float rowGap = compact ? 6f : 8f;
            float rowH = Mathf.Clamp(Mathf.Floor((availRowsH - rowGap * 2f) / 3f), 56f, 78f);
            float rowY = compact ? -34f : -40f;
            float titleY = compact ? -5f : -10f;
            float titleH = compact ? 22f : 24f;
            float detailY = compact ? -30f : -38f;
            float detailH = Mathf.Max(26f, rowH - (-detailY) - 4f);
            float pillY = -Mathf.Round((rowH - 34f) * 0.5f);
            int detailFont = compact ? 13 : 14;
            const float pillW = 136f;

            for (int i = 0; i < 3; i++)
            {
                var rowBg = GlassKit.Fill(card, $"Dòng dự đoán {i}", new Vector2(14f, rowY), new Vector2(rowW, rowH), GlassKit.SurfaceA(0.75f), null, true);
                AttachSoftBorder(rowBg.rectTransform, GlassKit.Line);
                var titleLbl = GlassKit.Label(rowBg.transform, "Tên", "", 15, GlassKit.Ink, true, TextAnchor.UpperLeft, new Vector2(14f, titleY), new Vector2(rowW - pillW - 40f, titleH));
                var detailLbl = GlassKit.Label(rowBg.transform, "Chi tiết", "", detailFont, GlassKit.Dim, false, TextAnchor.UpperLeft, new Vector2(14f, detailY), new Vector2(rowW - pillW - 40f, detailH));
                var pillImg = GlassKit.Fill(rowBg.transform, "Trạng thái", new Vector2(rowW - pillW - 14f, pillY), new Vector2(pillW, 34f), GlassKit.GoodSoft, null, true);
                var pillTxt = GlassKit.Label(pillImg.transform, "Chữ", "", 13, GlassKit.Good, true, TextAnchor.MiddleCenter, new Vector2(6f, 0f), new Vector2(pillW - 12f, 34f));
                predRows.Add(new PredRow { title = titleLbl, detail = detailLbl, badgeBg = pillImg, badgeText = pillTxt });
                rowY -= rowH + rowGap;
            }
        }

        void RefreshMixPredictions()
        {
            if (curBottle == null || predRows.Count == 0) return;
            var e = curBottle.Entry ?? (ChemicalCatalog.Instance != null ? ChemicalCatalog.Instance.Get(curBottle.stt) : null);
            var rx = SelectedReceiver;
            if (predHeaderSub != null)
                predHeaderSub.text = rx != null
                    ? $"Đang chọn: {(!string.IsNullOrEmpty(rx.label) ? rx.label : rx.name)}"
                    : "Mô phỏng nhanh với chất thử thường gặp";

            var candidates = new List<(string name, Solution baseSol, float tempC, bool inFumeHood, float boilCapC)>(3);
            if (rx != null)
                candidates.Add((!string.IsNullOrEmpty(rx.label) ? rx.label : rx.name, rx.Total, rx.TemperatureC, rx.InFumeHood, rx.BoilCapC));

            for (int i = 0; i < receivers.Count && candidates.Count < 3; i++)
            {
                var c = receivers[i];
                if (c == rx) continue;
                candidates.Add((!string.IsNullOrEmpty(c.label) ? c.label : c.name, c.Total, c.TemperatureC, c.InFumeHood, c.BoilCapC));
            }

            if (candidates.Count < 3) candidates.Add(("Nước cất (100 mL)", Solution.Water(100f), 25f, false, 100f));
            if (candidates.Count < 3) candidates.Add(("NaOH 0,1 M (50 mL)", Solution.NaOH(50f, 0.1f), 25f, false, 100f));
            if (candidates.Count < 3) candidates.Add(("HCl 0,1 M (50 mL)", Solution.HCl(50f, 0.1f), 25f, false, 100f));

            for (int i = 0; i < predRows.Count; i++)
            {
                var row = predRows[i];
                if (i >= candidates.Count)
                {
                    row.title.text = "—";
                    row.detail.text = "";
                    row.badgeText.text = "";
                    continue;
                }

                var cand = candidates[i];
                EvaluateMixPrediction(curBottle, e, cand.baseSol, cand.tempC, cand.inFumeHood, cand.boilCapC, out string summary, out string pill, out Color pillBg, out Color pillFg);
                row.title.text = cand.name;
                row.detail.text = summary;
                row.badgeText.text = pill;
                row.badgeText.color = pillFg;
                row.badgeBg.color = pillBg;
            }
        }

        void EvaluateMixPrediction(
            ChemicalBottle bottle,
            ChemEntry entry,
            Solution receiverTotal,
            float tempC,
            bool inFumeHood,
            float boilCapC,
            out string summary,
            out string badge,
            out Color badgeBg,
            out Color badgeFg)
        {
            var pred = PredictMix(bottle, entry, receiverTotal, tempC, transferAmountMl, transferScoops, inFumeHood, boilCapC);
            summary = pred.Summary;
            badge = pred.Badge;
            switch (pred.Tone)
            {
                case MixSafetyTone.Bad:
                    badgeBg = GlassKit.BadSoft;
                    badgeFg = GlassKit.Bad;
                    break;
                case MixSafetyTone.Warn:
                    badgeBg = GlassKit.WarnSoft;
                    badgeFg = GlassKit.Warn;
                    break;
                default:
                    badgeBg = GlassKit.GoodSoft;
                    badgeFg = GlassKit.Good;
                    break;
            }
        }

        /// <summary>Tạo bản sao Solution và mảng Mix tách biệt để dự đoán không bao giờ làm thay đổi bình thật.</summary>
        public static Solution CloneSolution(Solution src)
        {
            return new Solution
            {
                volumeMl = src.volumeMl,
                indicatorDrops = src.indicatorDrops,
                mix = src.Mix.ToBuilder().ToMix(),
            };
        }

        /// <summary>Mô phỏng trộn lượng hóa chất dự kiến vào bản sao bình nhận qua ChemEngine.Equilibrate.</summary>
        public static MixPrediction PredictMix(
            ChemicalBottle bottle,
            ChemEntry entry,
            Solution receiverSol,
            float tempC,
            float amountMl,
            int scoops,
            bool inFumeHood = false,
            float boilCapC = 100f)
        {
            if (entry == null && bottle != null)
                entry = bottle.Entry ?? (ChemicalCatalog.Instance != null ? ChemicalCatalog.Instance.Get(bottle.stt) : null);

            var receiverClone = CloneSolution(receiverSol);
            if (entry != null && entry.IsPaper)
            {
                double rPh = receiverClone.volumeMl > 0.002f ? ChemEngine.Ph(receiverClone.Mix, receiverClone.volumeMl / 1000.0) : 7.0;
                return new MixPrediction
                {
                    Tone = MixSafetyTone.Good,
                    Badge = "Chỉ thị",
                    Summary = receiverClone.volumeMl < 0.1f
                        ? "Cần nhúng vào dung dịch có nước để thử pH."
                        : $"Giấy chỉ thị thử ở pH {rPh.ToString("0.0", Vi)} · An toàn khi nhúng thử.",
                    PhBefore = rPh,
                    PhAfter = rPh,
                    Events = new List<ChemEvent>(0),
                };
            }

            var sampleClone = ClonePlannedBottlePortion(bottle, entry, amountMl, scoops, out string emptyBottleReason);
            if (!string.IsNullOrEmpty(emptyBottleReason))
            {
                double rPh = receiverClone.volumeMl > 0.002f ? ChemEngine.Ph(receiverClone.Mix, receiverClone.volumeMl / 1000.0) : 7.0;
                return new MixPrediction
                {
                    Tone = MixSafetyTone.Warn,
                    Badge = "Đã cạn",
                    Summary = emptyBottleReason,
                    PhBefore = rPh,
                    PhAfter = rPh,
                    Events = new List<ChemEvent>(0),
                };
            }

            if (sampleClone.IsEmpty && receiverClone.IsEmpty)
            {
                return new MixPrediction
                {
                    Tone = MixSafetyTone.Good,
                    Badge = "An toàn",
                    Summary = entry == null ? "Bình và chai đều đang trống." : $"Chuẩn bị thêm {entry.ten} vào bình trống.",
                    PhBefore = 7.0,
                    PhAfter = 7.0,
                    Events = new List<ChemEvent>(0),
                };
            }

            var combined = receiverClone + sampleClone;
            if (combined.volumeMl < 0.1f)
            {
                bool hasHazard = entry != null && !string.IsNullOrEmpty(ChemicalCatalog.HazardText(entry));
                return new MixPrediction
                {
                    Tone = hasHazard ? MixSafetyTone.Warn : MixSafetyTone.Good,
                    Badge = hasHazard ? "Thận trọng" : "An toàn",
                    Summary = (entry != null && entry.IsSolid) || (bottle != null && bottle.IsSolid)
                        ? "Cần thêm nước hoặc dung dịch để hòa tan."
                        : "Chưa đủ thể tích lỏng để cân bằng phản ứng.",
                    PhBefore = 7.0,
                    PhAfter = 7.0,
                    Events = new List<ChemEvent>(0),
                };
            }

            SimulateIsolatedMix(
                receiverClone,
                sampleClone,
                combined,
                tempC,
                inFumeHood,
                out double phBefore,
                out double phAfter,
                out double heatJ,
                out double neutralJ,
                out var events);

            double deltaT = combined.volumeMl >= 0.05f ? heatJ / (combined.volumeMl * 4.18) : 0.0;
            return ClassifyMixPrediction(
                entry,
                receiverClone.IsEmpty,
                receiverClone.volumeMl > 0.002f,
                sampleClone.volumeMl > 0.002f,
                tempC,
                boilCapC,
                phBefore,
                phAfter,
                heatJ,
                neutralJ,
                deltaT,
                inFumeHood,
                events);
        }

        static Solution ClonePlannedBottlePortion(
            ChemicalBottle bottle,
            ChemEntry entry,
            float amountMl,
            int scoops,
            out string emptyReason)
        {
            emptyReason = null;
            var cat = ChemicalCatalog.Instance;
            bool isSolid = (entry != null && entry.IsSolid) || (bottle != null && bottle.IsSolid);
            if (isSolid)
            {
                float scoopG = bottle != null && bottle.scoopGrams > 0.01f ? bottle.scoopGrams : 0.3f;
                float grams = Mathf.Max(0.3f, Mathf.Clamp(scoops, 1, 10) * scoopG);
                if (bottle != null && bottle.container != null && !bottle.container.contents.Mix.IsEmpty && bottle.GramsLeft > 0.01f)
                    return CloneSolution(bottle.container.contents.Scale(Mathf.Clamp01(grams / bottle.GramsLeft)));
                if (bottle != null && bottle.Entry != null && bottle.GramsLeft <= 0.01f)
                {
                    emptyReason = "Lọ chất rắn đã cạn.";
                    return default;
                }
                return cat != null && entry != null ? CloneSolution(cat.Make(entry, grams)) : default;
            }

            bool isGas = (entry != null && entry.IsGas) || (bottle != null && bottle.IsGas);
            if (isGas)
            {
                float mol = bottle != null ? Mathf.Clamp(bottle.gasRateMolPerS * 5f, 0.0005f, 0.05f) : 0.002f;
                if (bottle != null && bottle.Entry != null)
                {
                    if (bottle.GasLeftMol <= 0f)
                    {
                        emptyReason = "Bình khí đã hết.";
                        return default;
                    }
                    mol = Mathf.Min(mol, bottle.GasLeftMol);
                }
                return cat != null && entry != null && mol > 0f ? CloneSolution(cat.Make(entry, mol)) : default;
            }

            float ml = Mathf.Clamp(amountMl, 5f, 100f);
            if (bottle != null && bottle.container != null)
            {
                if (bottle.container.VolumeMl > 0.1f)
                    return CloneSolution(bottle.container.Total.Portion(Mathf.Min(ml, bottle.container.VolumeMl)));
                if (bottle.Entry != null)
                {
                    emptyReason = "Chai hóa chất đã cạn dung dịch.";
                    return default;
                }
            }
            return cat != null && entry != null ? CloneSolution(cat.Make(entry, ml)) : default;
        }

        static void SimulateIsolatedMix(
            Solution receiverClone,
            Solution sampleClone,
            Solution combined,
            float tempC,
            bool inFumeHood,
            out double phBefore,
            out double phAfter,
            out double heatJ,
            out double neutralJ,
            out List<ChemEvent> events)
        {
            double volL = combined.volumeMl / 1000.0;
            bool hasLiquidA = receiverClone.volumeMl > 0.002f;
            bool hasLiquidB = sampleClone.volumeMl > 0.002f;
            phBefore = hasLiquidA ? ChemEngine.Ph(receiverClone.Mix, receiverClone.volumeMl / 1000.0) : 7.0;

            double hA = 0, ohA = 0, hB = 0, ohB = 0;
            if (hasLiquidA) ChemEngine.FreeIons(receiverClone.Mix, receiverClone.volumeMl / 1000.0, out hA, out ohA);
            if (hasLiquidB) ChemEngine.FreeIons(sampleClone.Mix, sampleClone.volumeMl / 1000.0, out hB, out ohB);

            var mb = combined.Mix.ToBuilder();
            events = new List<ChemEvent>(4);
            var env = new ChemEnv { TempC = tempC, InFumeHood = inFumeHood, Stir = 1.0 };
            var eqRes = ChemEngine.Equilibrate(mb, volL, env, events);
            heatJ = eqRes.HeatJ;
            phAfter = eqRes.Ph;

            neutralJ = 0.0;
            if (hasLiquidA && hasLiquidB)
            {
                var afterEqMix = mb.ToMix();
                ChemEngine.FreeIons(afterEqMix, volL, out double hM, out double ohM);
                neutralJ = ChemEngine.NeutralizationJ(hA, ohA, hB, ohB, hM, ohM);
                heatJ += neutralJ;
            }

            if (ChemEngine.NeedsStep(mb.ToMix(), tempC))
            {
                float pp = combined.indicatorDrops;
                var stepEvents = new List<ChemEvent>(4);
                var stepEnv = new ChemEnv
                {
                    TempC = tempC + heatJ / (Math.Max(1f, combined.volumeMl) * 4.18),
                    InFumeHood = inFumeHood,
                    Stir = 1.0,
                };
                var stepRes = ChemEngine.Step(mb, volL, 10.0, stepEnv, stepEvents, ref pp);
                heatJ += stepRes.HeatJ;
                if (stepRes.Changed && stepRes.H > 0) phAfter = stepRes.Ph;
                for (int i = 0; i < stepEvents.Count; i++)
                {
                    if (!ContainsEventKey(events, stepEvents[i]))
                        events.Add(stepEvents[i]);
                }
            }

            heatJ += AddSulfuricDilutionEffects(receiverClone, sampleClone, mb, volL, hasLiquidB, events);
        }

        static double AddSulfuricDilutionEffects(
            Solution receiverClone,
            Solution sampleClone,
            MixBuilder mb,
            double volL,
            bool hasLiquidB,
            List<ChemEvent> events)
        {
            double heat = 0.0;
            double sulfuricAfter = mb.Get("SO4") / volL;
            double sulfuricA = receiverClone.volumeMl > 0.01f ? receiverClone.Mix.Get("SO4") / (receiverClone.volumeMl / 1000.0) : 0.0;
            if (sulfuricA > 6.0 && sulfuricAfter < sulfuricA * 0.97)
            {
                heat += 60000.0 * receiverClone.Mix.Get("SO4") * (1.0 - sulfuricAfter / sulfuricA);
                if (hasLiquidB && sampleClone.Mix.Get("SO4") / (sampleClone.volumeMl / 1000.0) < 3.0)
                {
                    var boilEv = new ChemEvent
                    {
                        Kind = EventKind.Hazard,
                        Severity = Severity.Danger,
                        Hazard = "ban_be",
                        Key = "nguyhiem:nuocvaoaxit",
                        Title = "Nước sôi bắn ra! Không được đổ nước vào axit đặc",
                        Detail = "Pha loãng H₂SO₄ đặc: rót từ từ axit vào nước và khuấy đều, không làm ngược lại.",
                    };
                    if (!ContainsEventKey(events, boilEv)) events.Add(boilEv);
                }
            }

            double sulfuricB = sampleClone.volumeMl > 0.01f ? sampleClone.Mix.Get("SO4") / (sampleClone.volumeMl / 1000.0) : 0.0;
            if (sulfuricB > 6.0 && sulfuricAfter < sulfuricB * 0.97)
                heat += 60000.0 * sampleClone.Mix.Get("SO4") * (1.0 - sulfuricAfter / sulfuricB);

            return heat;
        }

        static MixPrediction ClassifyMixPrediction(
            ChemEntry entry,
            bool receiverWasEmpty,
            bool hasLiquidA,
            bool hasLiquidB,
            float tempC,
            float boilCapC,
            double phBefore,
            double phAfter,
            double heatJ,
            double neutralJ,
            double deltaT,
            bool inFumeHood,
            List<ChemEvent> events)
        {
            bool hasDanger = false, hasBoilOver = false, hasToxicGasOrHood = false, hasHeatWarn = false, hasOtherWarn = false;
            bool hasPrecipitate = false, hasDissolve = false, hasColor = false, hasGas = false, hasReaction = false;
            string gasId = null;

            for (int i = 0; i < events.Count; i++)
            {
                var ev = events[i];
                if (ev.Severity == Severity.Danger || ev.Hazard == "chay_no" || ev.GasId == "Cl2" || ev.GasId == "HCN")
                    hasDanger = true;
                if (ev.Hazard == "ban_be")
                    hasBoilOver = true;
                if (ev.Hazard == "khi_doc" || ev.GasId == "SO2" || ev.GasId == "H2S" || ev.GasId == "NO" ||
                    ev.GasId == "NO2" || ev.GasId == "NH3" || ev.GasId == "H2" || ev.GasId == "CH4" || ev.GasId == "C2H2")
                    hasToxicGasOrHood = true;
                if (ev.Kind == EventKind.Heat || ev.Hazard == "bong")
                    hasHeatWarn = true;
                if (ev.Severity == Severity.Warning || ev.Kind == EventKind.Hazard)
                    hasOtherWarn = true;
                if (ev.Kind == EventKind.Precipitate) hasPrecipitate = true;
                if (ev.Kind == EventKind.Dissolve) hasDissolve = true;
                if (ev.Kind == EventKind.Color) hasColor = true;
                if (ev.Kind == EventKind.Reaction) hasReaction = true;
                if (ev.Kind == EventKind.Gas)
                {
                    hasGas = true;
                    if (string.IsNullOrEmpty(gasId)) gasId = ev.GasId;
                }
            }

            double capTemp = boilCapC > 1f ? boilCapC : 100f;
            double rawTemp = tempC + deltaT;
            bool violentExotherm = (deltaT >= 5.0 && rawTemp >= capTemp - 2.0) || deltaT >= 35.0;
            if (!violentExotherm && deltaT >= 10.0) hasHeatWarn = true;

            bool phNeutralShift = (phBefore < 5.5 && phAfter >= phBefore + 1.0 && phAfter >= 5.5) ||
                                  (phBefore > 8.5 && phAfter <= phBefore - 1.0 && phAfter <= 8.5);
            bool isNeutralization = hasLiquidA && hasLiquidB && (neutralJ >= 10.0 || phNeutralShift);
            bool bigPhShift = hasLiquidA && Math.Abs(phAfter - phBefore) >= 1.5;

            MixSafetyTone tone;
            string badge;
            if (hasDanger || hasBoilOver || violentExotherm)
            {
                tone = MixSafetyTone.Bad;
                badge = "Nguy hiểm";
            }
            else if (hasToxicGasOrHood)
            {
                tone = MixSafetyTone.Warn;
                badge = "Cần tủ hút";
            }
            else if (hasHeatWarn || (entry != null && entry.HasHazard("phan_ung_nuoc") && Math.Abs(heatJ) > 80.0))
            {
                tone = MixSafetyTone.Warn;
                badge = "Tỏa nhiệt";
            }
            else if (hasOtherWarn)
            {
                tone = MixSafetyTone.Warn;
                badge = "Thận trọng";
            }
            else
            {
                tone = MixSafetyTone.Good;
                if (hasPrecipitate) badge = "Tạo kết tủa";
                else if (hasDissolve) badge = "Tan kết tủa";
                else if (isNeutralization) badge = "Trung hòa";
                else if (hasColor) badge = "Đổi màu";
                else if (hasReaction) badge = "Phản ứng";
                else if (hasGas) badge = gasId == "CO2" ? "Sủi khí CO₂" : (gasId == "O2" ? "Sủi khí O₂" : "Sinh khí");
                else badge = "An toàn";
            }

            var primary = SelectPrimaryEvent(events);
            string baseText;
            if (primary != null && !string.IsNullOrEmpty(primary.Title))
                baseText = primary.Title;
            else if (hasBoilOver || violentExotherm)
                baseText = "Tỏa nhiệt rất mạnh, có nguy cơ sôi bắn";
            else if (isNeutralization)
                baseText = deltaT >= 10.0 ? "Trung hòa tỏa nhiệt" : "Phản ứng trung hòa";
            else if (deltaT >= 10.0 || (entry != null && entry.HasHazard("phan_ung_nuoc") && Math.Abs(heatJ) > 80.0))
                baseText = "Pha trộn tỏa nhiệt";
            else if (bigPhShift)
                baseText = "Dung dịch đổi pH đáng kể";
            else
                baseText = receiverWasEmpty ? "Dung dịch sau rót ổn định" : "Không tạo kết tủa hay khí";

            string phStr = hasLiquidA && Math.Abs(phAfter - phBefore) >= 0.2
                ? $"pH {phBefore.ToString("0.0", Vi)} → {phAfter.ToString("0.0", Vi)}"
                : $"pH {phAfter.ToString("0.00", Vi)}";
            string dtStr = Math.Abs(deltaT) >= 2.0
                ? $" · ΔT {(deltaT >= 0 ? "+" : "")}{deltaT.ToString("0.#", Vi)} °C"
                : "";
            string hoodStr = inFumeHood && tone == MixSafetyTone.Warn && hasToxicGasOrHood ? " (tủ hút)" : "";

            return new MixPrediction
            {
                Tone = tone,
                Badge = badge,
                Summary = $"{baseText}{hoodStr} · {phStr}{dtStr}",
                PhBefore = phBefore,
                PhAfter = phAfter,
                DeltaTempC = deltaT,
                Events = events,
            };
        }

        static bool ContainsEventKey(List<ChemEvent> list, ChemEvent candidate)
        {
            string key = candidate.Key ?? candidate.Title;
            for (int i = 0; i < list.Count; i++)
                if ((list[i].Key ?? list[i].Title) == key) return true;
            return false;
        }

        static ChemEvent SelectPrimaryEvent(List<ChemEvent> events)
        {
            ChemEvent best = null;
            int bestScore = -1;
            for (int i = 0; i < events.Count; i++)
            {
                int score = ScoreEvent(events[i]);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = events[i];
                }
            }
            return best;
        }

        static int ScoreEvent(ChemEvent e)
        {
            if (e == null) return -1;
            int score = (int)e.Severity * 100;
            if (e.Hazard == "ban_be" || e.Hazard == "chay_no") score += 80;
            else if (e.Hazard == "khi_doc") score += 50;
            else if (!string.IsNullOrEmpty(e.Hazard)) score += 25;

            if (e.Kind == EventKind.Hazard) score += 40;
            else if (e.Kind == EventKind.Gas) score += 30;
            else if (e.Kind == EventKind.Heat) score += 25;
            else if (e.Kind == EventKind.Precipitate) score += 20;
            else if (e.Kind == EventKind.Dissolve) score += 16;
            else if (e.Kind == EventKind.Reaction) score += 14;
            else if (e.Kind == EventKind.Color) score += 10;
            return score;
        }

        // ------------------------------------------------------------------
        // Xem trước 3D & vẽ sơ đồ Painter
        // ------------------------------------------------------------------

        void BuildPreviewBox(Transform parent, Vector2 size, string fallbackCaption, float topOffsetY = 0f)
        {
            var anchor = topOffsetY == 0f ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 1f);
            var pivot = topOffsetY == 0f ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 1f);
            var rt = GlassKit.Rect(parent, "Ảnh 3D", anchor, pivot, new Vector2(0f, topOffsetY), size);
            previewImg = rt.gameObject.AddComponent<RawImage>();
            previewImg.raycastTarget = false;

            var fb = GlassKit.Rect(parent, "Dự phòng", anchor, pivot, new Vector2(0f, topOffsetY), size);
            previewFallbackGo = fb.gameObject;
            GlassKit.Label(fb, "Chữ", fallbackCaption, 16, GlassKit.Dim, true, TextAnchor.MiddleCenter, Vector2.zero, size);
            previewFallbackGo.SetActive(false);
        }

        void RefreshPreview()
        {
            if (previewImg == null || targetRoot == null) return;
            var extra = mode == Mode.Buret && curBuret != null ? buretExtraCache : null;
            var tex = ToolPreview.Render(targetRoot, -28f, 24f, extra);
            if (tex != null)
            {
                previewImg.texture = tex;
                previewImg.enabled = true;
                if (previewFallbackGo != null) previewFallbackGo.SetActive(false);
            }
            else
            {
                previewImg.enabled = false;
                if (previewFallbackGo != null) previewFallbackGo.SetActive(true);
            }
        }

        Texture2D DrawMeniscusSchematic(BuretStopcock buret)
        {
            const int W = 420;
            const int H = 220;
            if (meniscusPainter == null) meniscusPainter = new Painter(W, H);
            var p = meniscusPainter;

            Color bg = GlassKit.Well;
            p.Clear(bg);

            float reading = buret != null ? buret.ReadingMl : 0f;
            float centerX = W * 0.5f;
            float tubeHalfW = 54f;
            float pxPerMl = 46f;
            float midY = H * 0.5f;

            // Cột dung dịch bên dưới mặt khum (số đọc lớn hơn nằm phía dưới thang chia)
            Color liq = new Color(GlassKit.Accent.r, GlassKit.Accent.g, GlassKit.Accent.b, 0.24f);
            p.FillRect(centerX - tubeHalfW + 3f, 8f, tubeHalfW * 2f - 6f, midY - 6f, liq);

            // Đường cong mặt khum lõm chạm đúng y = midY tại tâm
            float arcW = tubeHalfW - 4f;
            for (int i = 0; i < 24; i++)
            {
                float t0 = -1f + 2f * i / 24f;
                float t1 = -1f + 2f * (i + 1) / 24f;
                float x0 = centerX + t0 * arcW;
                float x1 = centerX + t1 * arcW;
                float y0 = midY + t0 * t0 * 14f;
                float y1 = midY + t1 * t1 * 14f;
                p.Line(x0, y0, x1, y1, GlassKit.Accent, 2.5f);
            }

            // Thành thủy tinh trái/phải
            Color wallCol = GlassKit.Dim;
            p.FillRect(centerX - tubeHalfW, 8f, 3f, H - 16f, wallCol);
            p.FillRect(centerX + tubeHalfW - 3f, 8f, 3f, H - 16f, wallCol);

            // Vạch chia mỗi 0.1 mL quanh reading hiện tại
            float minMl = reading - (H * 0.5f - 16f) / pxPerMl;
            float maxMl = reading + (H * 0.5f - 16f) / pxPerMl;
            int firstTenth = Mathf.CeilToInt(minMl * 10f);
            int lastTenth = Mathf.FloorToInt(maxMl * 10f);
            for (int t = firstTenth; t <= lastTenth; t++)
            {
                float ml = t * 0.1f;
                float yy = midY - (ml - reading) * pxPerMl;
                if (yy < 12f || yy > H - 12f) continue;
                bool major = t % 10 == 0;
                bool half = !major && t % 5 == 0;
                float tickW = major ? 34f : (half ? 22f : 12f);
                Color tc = major ? GlassKit.Ink : GlassKit.Dim;
                p.Line(centerX - tubeHalfW + 6f, yy, centerX - tubeHalfW + 6f + tickW, yy, tc, major ? 2f : 1.2f);
            }

            // Đường ngắm ngang tiếp tuyến đáy mặt khum (hổ phách)
            p.Dashed(18f, midY, W - 18f, midY, GlassKit.Accent2, 1.8f, 7f, 5f);
            p.Frame(2f, 2f, W - 4f, H - 4f, GlassKit.Line, 1.5f);
            p.Apply();
            return p.Tex;
        }

        void BuildBottleLabelCard(Transform parent, ChemicalBottle bottle, ChemEntry entry, string hazard, float colW)
        {
            float cardW = colW - 32f;
            const float cardH = 150f;
            var cardImg = GlassKit.Fill(parent, "Nhãn chai", new Vector2(16f, -(BodyH - cardH - 16f)), new Vector2(cardW, cardH), GlassKit.SurfaceA(0.95f), null, true);
            AttachSoftBorder(cardImg.rectTransform, GlassKit.Line);
            var card = cardImg.transform;

            Color chemCol = GlassKit.Accent;
            if (entry != null && !string.IsNullOrEmpty(entry.mau))
            {
                var rc = Khkt.Simulation.Chem.Rgb.Hex(entry.mau);
                chemCol = new Color(rc.r, rc.g, rc.b, 0.92f);
            }
            var stripe = GlassKit.Fill(card, "Vạch màu", new Vector2(14f, -16f), new Vector2(8f, cardH - 32f), chemCol);

            var badgeTex = DrawBottleLabelSchematic(bottle, entry, hazard);
            var badgeRt = GlassKit.Rect(card, "Biểu tượng an toàn", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(cardW - 92f, -37f), new Vector2(76f, 76f));
            var badgeImg = badgeRt.gameObject.AddComponent<RawImage>();
            badgeImg.texture = badgeTex;
            badgeImg.raycastTarget = false;

            float textX = 34f;
            float textW = cardW - textX - 104f;
            string formula = entry != null ? entry.Formula : "";
            string headline = !string.IsNullOrEmpty(formula) && !string.Equals(formula, bottle.DisplayName, StringComparison.OrdinalIgnoreCase)
                ? $"{formula}  ·  {bottle.DisplayName}"
                : bottle.DisplayName;
            var titleLbl = GlassKit.Label(card, "Tên chất", headline, 19, GlassKit.Ink, true, TextAnchor.MiddleLeft, new Vector2(textX, -14f), new Vector2(textW, 28f));
            titleLbl.horizontalOverflow = HorizontalWrapMode.Wrap;
            titleLbl.verticalOverflow = VerticalWrapMode.Truncate;

            string concStr = FormatConcentration(entry);
            string stateStr = entry != null ? ChemicalCatalog.StateText(entry) : "Hóa chất";
            var concLbl = GlassKit.Label(card, "Nồng độ", $"Nồng độ: {concStr}  ·  {stateStr}", 14, GlassKit.Dim, false, TextAnchor.MiddleLeft, new Vector2(textX, -48f), new Vector2(textW, 24f));
            concLbl.horizontalOverflow = HorizontalWrapMode.Wrap;
            concLbl.verticalOverflow = VerticalWrapMode.Truncate;

            bottleLabelRemainText = GlassKit.Label(card, "Dung lượng", $"Còn lại: {FormatBottleRemaining(bottle)}  ·  Vị trí: {FormatBottleShelf(entry)}", 14, GlassKit.Accent, true, TextAnchor.MiddleLeft, new Vector2(textX, -78f), new Vector2(textW, 24f));
            bottleLabelRemainText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bottleLabelRemainText.verticalOverflow = VerticalWrapMode.Truncate;

            bool hasHaz = !string.IsNullOrEmpty(hazard);
            string safetyText = hasHaz ? $"⚠ Cảnh báo: {hazard}" : "An toàn phòng thí nghiệm · Đậy nắp sau khi dùng";
            var safeLbl = GlassKit.Label(card, "An toàn", safetyText, 13, hasHaz ? GlassKit.Bad : GlassKit.Good, true, TextAnchor.MiddleLeft, new Vector2(textX, -108f), new Vector2(textW, 26f));
            safeLbl.horizontalOverflow = HorizontalWrapMode.Wrap;
            safeLbl.verticalOverflow = VerticalWrapMode.Truncate;
        }

        Texture2D DrawBottleLabelSchematic(ChemicalBottle bottle, ChemEntry entry, string hazard)
        {
            const int S = 112;
            if (bottleLabelTex != null) Destroy(bottleLabelTex);
            var p = new Painter(S, S);
            bottleLabelTex = p.Tex;

            p.Clear(new Color(0f, 0f, 0f, 0f));

            bool hasHaz = !string.IsNullOrEmpty(hazard);
            Color hazCol = hasHaz ? GlassKit.Bad : GlassKit.Good;
            Color badgeFill = hasHaz ? GlassKit.BadSoft : GlassKit.GoodSoft;
            float cx = S * 0.5f;
            float cy = S * 0.5f;

            p.Disc(cx, cy, 46f, badgeFill);
            p.Ring(cx, cy, 46f, new Color(hazCol.r, hazCol.g, hazCol.b, 0.45f), 2.5f);

            if (hasHaz)
            {
                p.Line(cx, cy + 28f, cx - 26f, cy - 20f, hazCol, 3.5f);
                p.Line(cx - 26f, cy - 20f, cx + 26f, cy - 20f, hazCol, 3.5f);
                p.Line(cx + 26f, cy - 20f, cx, cy + 28f, hazCol, 3.5f);
                p.FillRect(cx - 2.5f, cy - 3f, 5f, 17f, hazCol);
                p.Disc(cx, cy - 12f, 3f, hazCol);
            }
            else
            {
                p.Line(cx - 14f, cy - 1f, cx - 3f, cy - 12f, hazCol, 4f);
                p.Line(cx - 3f, cy - 12f, cx + 17f, cy + 13f, hazCol, 4f);
            }

            p.Apply();
            return bottleLabelTex;
        }

        // ------------------------------------------------------------------
        // Tiện ích tìm dụng cụ, cầm lên & trả về chỗ cũ
        // ------------------------------------------------------------------

        LabContainer SelectedReceiver =>
            selectedReceiverIdx >= 0 && selectedReceiverIdx < receivers.Count ? receivers[selectedReceiverIdx] : null;

        void FindNearbyReceivers(Vector3 origin, LabContainer exclude)
        {
            receivers.Clear();
            selectedReceiverIdx = 0;

            var all = FindObjectsByType<LabContainer>();
            var scored = new List<(LabContainer c, float dist)>();
            for (int i = 0; i < all.Length; i++)
            {
                var c = all[i];
                if (c == null || c == exclude || !c.gameObject.activeInHierarchy) continue;
                // Bỏ qua chai hóa chất gốc khác, ưu tiên bình tam giác, cốc, ống đong, buret
                if (c.GetComponentInParent<ChemicalBottle>() != null) continue;
                float d = Vector3.Distance(origin, c.transform.position);
                if (d > 6f) continue;
                scored.Add((c, d));
            }
            scored.Sort((a, b) => a.dist.CompareTo(b.dist));
            for (int i = 0; i < scored.Count && receivers.Count < 4; i++)
                receivers.Add(scored[i].c);
        }

        GameObject FindBuretExtra(BuretStopcock buret)
        {
            if (buret == null) return null;
            var rx = buret.Receiver();
            if (rx != null) return rx.gameObject;

            Vector3 tipPos = buret.tip != null ? buret.tip.position : buret.transform.position;
            var all = FindObjectsByType<LabContainer>();
            LabContainer best = null;
            float bestD = 0.65f;
            for (int i = 0; i < all.Length; i++)
            {
                var c = all[i];
                if (c == null || c == buret.container || !c.gameObject.activeInHierarchy) continue;
                float d = Vector3.Distance(tipPos, c.transform.position);
                if (d < bestD) { bestD = d; best = c; }
            }
            return best != null ? best.gameObject : null;
        }

        static Grabbable ResolveGrabbable(GameObject target)
        {
            if (target == null) return null;
            var raw = target.GetComponentInParent<Grabbable>();
            var g = Khkt.Assembly.ApparatusPart.WorldGrab(raw);
            return g != null ? g : raw;
        }

        string GetPickButtonLabel(GameObject target)
        {
            var hand = FindAnyObjectByType<HandController>();
            if (hand != null && hand.Held != null)
            {
                var g = ResolveGrabbable(target);
                if (hand.Held.gameObject == target || (g != null && hand.Held == g))
                    return "Đang cầm";
            }
            return "Cầm lên";
        }

        void PickTarget(GameObject target)
        {
            CloseInternal();
            var hand = FindAnyObjectByType<HandController>();
            if (hand != null && target != null) hand.PickFromPanel(target);
        }

        void ReturnTargetHome(GameObject target)
        {
            if (target == null) return;
            var hand = FindAnyObjectByType<HandController>();
            if (hand != null)
            {
                if (hand.ReturnToHomeFromPanel(target))
                {
                    CloseInternal();
                    LabToasts.Push("Đã trả dụng cụ về đúng vị trí ban đầu.", LabToasts.Tone.Info, 2.0f);
                }
            }
            else
            {
                CloseInternal();
            }
        }

        void AskAssistant(string fallbackQuestion)
        {
            BuildAssistantContext(fallbackQuestion, out string topic, out string prompt);
            if (!RobotChatUI.AskAbout(topic, prompt) && !ClassRules.ChatLocked)
                LabMessages.Say("Trợ lý robot chưa sẵn sàng trong cảnh hiện tại.", LabMessages.Tone.Warn);
        }

        void BuildAssistantContext(string fallbackQuestion, out string topic, out string prompt)
        {
            topic = "Dụng cụ thí nghiệm";
            prompt = fallbackQuestion;
            switch (mode)
            {
                case Mode.Buret when curBuret != null:
                {
                    float vol = curBuret.lastMarkMl > 0.1f ? curBuret.lastMarkMl : 50f;
                    string contents = DescribeContainerShort(curBuret.container);
                    var proc = FindAnyObjectByType<LabProcedure>();
                    string stepNote = "";
                    if (proc != null && proc.Active && proc.StepNames != null && proc.StepNames.Length > 0)
                    {
                        int stepIdx = Mathf.Clamp((int)proc.Current + 1, 1, proc.StepNames.Length);
                        stepNote = proc.Current == LabProcedure.Step.Xong
                            ? $", đã hoàn thành {proc.StepNames.Length}/{proc.StepNames.Length} bước"
                            : $", bước {stepIdx}/{proc.StepNames.Length}: {proc.StepNames[stepIdx - 1]}";
                    }
                    topic = $"Buret {vol.ToString("0", Vi)} mL ({contents})";
                    prompt = $"Buret {vol.ToString("0", Vi)} mL đang đọc {curBuret.ReadingMl.ToString("0.00", Vi)} mL, tốc độ nhỏ {curBuret.FlowMlPerS.ToString("0.00", Vi)} mL/s, chứa {contents}{stepNote}. Cần lưu ý gì khi đọc mặt khum và điều khiển khóa?";
                    break;
                }
                case Mode.Bottle when curBottle != null:
                {
                    var e = curBottle.Entry ?? (ChemicalCatalog.Instance != null ? ChemicalCatalog.Instance.Get(curBottle.stt) : null);
                    string formula = e != null ? e.Formula : "";
                    topic = !string.IsNullOrEmpty(formula) && !string.Equals(curBottle.DisplayName, formula, StringComparison.OrdinalIgnoreCase)
                        ? $"{curBottle.DisplayName} ({formula})"
                        : curBottle.DisplayName;
                    string remain = FormatBottleRemaining(curBottle);
                    string hazard = e != null ? ChemicalCatalog.HazardText(e) : "";
                    var rx = SelectedReceiver;
                    string concNote = e != null && e.M > 0f ? $", nồng độ {e.M:0.##} M" : "";
                    string hazardNote = !string.IsNullOrEmpty(hazard) ? $", cảnh báo: {hazard}" : "";
                    string transferNote = rx != null ? $", dự kiến rót {transferAmountMl.ToString("0", Vi)} mL sang {rx.label}" : "";
                    prompt = $"Lọ hóa chất {topic}, còn {remain}{concNote}{hazardNote}{transferNote}. Cho biết tính chất chính và lưu ý an toàn khi thao tác.";
                    break;
                }
                case Mode.Container when curContainer != null:
                {
                    var info = curContainer.GetComponentInParent<EquipmentInfo>();
                    string title = !string.IsNullOrEmpty(curContainer.label) ? curContainer.label : (info != null && !string.IsNullOrEmpty(info.title) ? info.title : curContainer.gameObject.name);
                    string phStr = curContainer.VolumeMl > 0.1f ? curContainer.BulkPh.ToString("0.00", Vi) : "—";
                    var rx = SelectedReceiver;
                    string transferNote = rx != null && !curContainer.Total.IsEmpty ? $", dự kiến rót {transferAmountMl.ToString("0", Vi)} mL vào {rx.label}" : "";
                    topic = title;
                    prompt = $"Dụng cụ {title} (V = {curContainer.VolumeMl.ToString("0.0", Vi)} mL, thành phần: {DescribeContainerShort(curContainer)}, pH {phStr}, {curContainer.TemperatureC.ToString("0.0", Vi)} °C{transferNote}). Cần chú ý thao tác và an toàn gì?";
                    break;
                }
                default:
                {
                    if (targetRoot != null)
                    {
                        var info = targetRoot.GetComponentInParent<EquipmentInfo>();
                        topic = info != null && !string.IsNullOrEmpty(info.title) ? info.title : targetRoot.name;
                    }
                    if (string.IsNullOrWhiteSpace(prompt))
                        prompt = $"Hướng dẫn cách sử dụng đúng và lưu ý an toàn cho {topic}.";
                    break;
                }
            }
        }

        // ------------------------------------------------------------------
        // Tạo mảnh giao diện nhỏ & định dạng chuỗi
        // ------------------------------------------------------------------

        static RectTransform MakeWellCard(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            var img = GlassKit.Fill(parent, name, pos, size, GlassKit.Well, null, true);
            AttachSoftBorder(img.rectTransform, GlassKit.Line);
            return img.rectTransform;
        }

        static void AttachSoftBorder(RectTransform rt, Color col)
        {
            var go = new GameObject("Viền", typeof(RectTransform));
            var brt = (RectTransform)go.transform;
            brt.SetParent(rt, false);
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = brt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.sprite = GlassKit.Border;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = UiTheme.IsVera ? 2.2f : 3f;
            img.color = col;
            img.raycastTarget = false;
        }

        static void FitRawImage(RawImage img, Texture tex)
        {
            if (img == null || tex == null || tex.height <= 0) return;
            var parentRt = img.transform.parent as RectTransform;
            if (parentRt == null) return;
            float availW = Mathf.Max(40f, parentRt.sizeDelta.x - 16f);
            float availH = Mathf.Max(40f, parentRt.sizeDelta.y - 16f);
            float texRatio = (float)tex.width / tex.height;
            float boxRatio = availW / availH;
            if (texRatio > boxRatio)
                img.rectTransform.sizeDelta = new Vector2(availW, availW / texRatio);
            else
                img.rectTransform.sizeDelta = new Vector2(availH * texRatio, availH);
        }

        static string DescribeContainerShort(LabContainer c)
        {
            if (c == null || c.Total.IsEmpty) return "Trống";
            var parts = ChemColor.Components(c.Total.Mix, c.Total.volumeMl, 2);
            if (parts.Count == 0)
            {
                if (c.Total.indicatorDrops >= 0.3f) return "Phenolphtalein";
                return c.VolumeMl > 0.1f ? "Nước cất" : "Trống";
            }
            return string.Join(", ", parts);
        }

        static string FormatMl(float ml) => $"{ml.ToString("0.00", Vi)} mL";

        static string FormatFlow(BuretStopcock b)
        {
            if (b == null) return "0,00 mL/s";
            float flow = b.FlowMlPerS;
            if (flow < 0.005f) return "0,00 mL/s";
            int drops = Mathf.Max(1, Mathf.RoundToInt(flow / Mathf.Max(0.01f, b.dropMl)));
            return $"{flow.ToString("0.00", Vi)} mL/s ({drops} giọt/s)";
        }

        static string FormatConcentration(ChemEntry e)
        {
            if (e == null) return "—";
            if (!string.IsNullOrEmpty(e.nong_do)) return e.nong_do;
            string cText = ChemicalCatalog.ConcText(e);
            if (!string.IsNullOrEmpty(cText)) return cText;
            if (e.M > 0) return $"{e.M.ToString("0.##", Vi)} M";
            return ChemicalCatalog.StateText(e);
        }

        static string FormatBottlePhState(ChemicalBottle b, ChemEntry e)
        {
            if (b != null && b.container != null && b.container.VolumeMl > 0.1f && (e == null || e.IsLiquidLike))
                return $"pH {b.container.BulkPh.ToString("0.00", Vi)} · {(e != null ? ChemicalCatalog.StateText(e) : "Dung dịch")}";
            return e != null ? ChemicalCatalog.StateText(e) : "—";
        }

        static string FormatBottleRemaining(ChemicalBottle b)
        {
            if (b == null) return "—";
            if (b.IsSolid) return $"{b.GramsLeft.ToString("0.#", Vi)} g";
            if (b.IsGas) return $"{b.GasLeftMol.ToString("0.###", Vi)} mol";
            if (b.IsPaper) return "Sẵn sàng";
            if (b.container != null) return $"{b.container.VolumeMl.ToString("0.#", Vi)} mL";
            return "—";
        }

        static string FormatBottleShelf(ChemEntry e)
        {
            if (e == null) return "Bàn";
            if (!string.IsNullOrEmpty(e.ma_vi_tri)) return e.ma_vi_tri;
            if (e.vi_tri != null) return $"{e.vi_tri.ke}-{e.vi_tri.tang}";
            return "Bàn";
        }

        string FormatBottleActionLabel(ChemicalBottle b)
        {
            if (b == null) return "Rót";
            if (b.IsSolid) return $"Xúc {transferScoops} thìa";
            if (b.IsGas) return "Sục khí";
            if (b.IsPaper) return "Nhúng thử";
            return $"Rót {transferAmountMl:0} mL";
        }
    }
}
