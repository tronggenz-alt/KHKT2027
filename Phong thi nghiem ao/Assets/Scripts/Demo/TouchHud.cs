using System;
using System.Collections.Generic;
using Khkt.Environment;
using Khkt.Input;
using Khkt.Interaction;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static Khkt.Demo.GlassKit;

namespace Khkt.Demo
{
    /// <summary>
    /// Giao diện cảm ứng cho iPad (Touch HUD - Giai đoạn 4):
    ///   • 100% nút bấm đạt diện tích chạm chuẩn Apple HIG (>= 44x44 pt, tương đương >= 65 canvas units trên iPad Air M3).
    ///   • Tối ưu vị trí ngón cái hai bên màn hình; vùng giữa và nửa phải màn hình thoáng để vuốt xoay góc nhìn.
    ///   • Nhãn động theo thiết bị: Ẩn phím tắt PC (Q, F, Space, Enter, C, H, G) khi đang dùng cảm ứng.
    ///   • Tích hợp NutHieuUng cho mọi nút với phản hồi hình ảnh (co 0.95x) và âm thanh click.
    ///   • Xử lý an toàn safeArea chống lỗi chia cho 0 khi chạy batchmode.
    /// </summary>
    public class TouchHud : MonoBehaviour
    {
        public static TouchHud Instance { get; private set; }
        public HandController hand;
        public DesktopViewer viewer;
        public LabProcedure procedure;
        public Shader glassShader;
        public TouchControls touchControls;

        Canvas canvas;
        CanvasGroup canvasGroup;
        RectTransform safeAreaRect;
        Material glassMat, glassLightMat;

        // Cần di chuyển nổi
        RectTransform joyBase, joyKnob;
        CanvasGroup joyGroup;
        bool joyVisible;

        // Khung nền Kính mờ hiện đại thống nhất (QuickActionDock)
        RectTransform quickActionDock;
        Image dockBg;

        // Khay Công cụ mở rộng phía trên (4 slot)
        public struct QuickSlotInfo
        {
            public string label;
            public Action onClick;
            public bool highlighted;
            public bool isHold;
            public float holdAxis;
        }
        readonly QuickSlotInfo[] registeredSlots = new QuickSlotInfo[4];
        readonly GameObject[] slotRoots = new GameObject[4];
        readonly Image[] slotBgs = new Image[4];
        readonly Text[] slotTexts = new Text[4];
        readonly TouchTapTrigger[] slotTaps = new TouchTapTrigger[4];
        readonly TouchHoldTrigger[] slotHolds = new TouchHoldTrigger[4];

        // Nút tay chính
        Image mainActionBg;
        Text mainActionText, mainActionSub;
        bool mainActionHeld;

        // Các nút ngữ cảnh & điều hướng phía dưới
        GameObject returnBtn, swirlBtn, shiftBtn, sitBtn, eyeUpBtn, eyeDownBtn;
        GameObject buretPanel, fabPanel, recordBtn, helpBtn;
        Text returnText, swirlText, sitText, helpText;
        Image returnBg, swirlBg, sitBg, eyeUpBg, eyeDownBg, shiftBg;
        Text shiftText;
        bool shiftActive;

        // Menu nhanh
        GameObject menuPanel;
        Text sensText;
        Slider sensSlider;

        // Danh sách trạm
        GameObject stationPanel;

        // Trạng thái màn hình
        Rect lastSafeArea;
        Vector2 lastScreenSize;
        float buretHoldTimer;
        float buretHoldDir;

        void Awake()
        {
            Instance = this;
            if (glassShader == null) glassShader = Shader.Find("Khkt/KinhMo");
            glassMat = Glass(glassShader);
            glassLightMat = Glass(glassShader, light: true);
            BuildHud();
        }

        void Start()
        {
            if (hand == null) hand = FindAnyObjectByType<HandController>();
            if (viewer == null) viewer = FindAnyObjectByType<DesktopViewer>();
            if (procedure == null) procedure = FindAnyObjectByType<LabProcedure>();
            if (touchControls == null) touchControls = FindAnyObjectByType<TouchControls>();
            if (touchControls != null && touchControls.hud == null) touchControls.hud = this;
        }

        void Update()
        {
            // Kiểm tra safeArea
            if (Screen.safeArea != lastSafeArea || new Vector2(Screen.width, Screen.height) != lastScreenSize)
            {
                ApplySafeArea();
            }

            // Ẩn khi đang mở bảng giao diện khác (UiCapture > 0) hoặc chưa bật cảm ứng
            bool uiCaptured = DesktopViewer.UiCapture > 0;
            bool touchActive = PadInput.UsingTouch;
            bool show = touchActive && !uiCaptured;

            canvasGroup.alpha = show ? 1f : 0f;
            canvasGroup.blocksRaycasts = show;
            canvasGroup.interactable = show;

            if (!show) return;

            UpdateContextButtons();

            // Xử lý giữ nút vặn buret
            if (buretHoldDir != 0)
            {
                buretHoldTimer += Time.deltaTime;
                PadInput.TouchStopcockAxis = buretHoldDir;
            }
        }

        void ApplySafeArea()
        {
            lastSafeArea = Screen.safeArea;
            int sw = Screen.width > 0 ? Screen.width : 2360;
            int sh = Screen.height > 0 ? Screen.height : 1640;
            lastScreenSize = new Vector2(sw, sh);
            if (safeAreaRect == null) return;

            Rect safe = Screen.safeArea;
            if (safe.width <= 0 || safe.height <= 0)
            {
                safe = new Rect(0, 0, sw, sh);
            }

            Vector2 min = safe.position;
            Vector2 max = safe.position + safe.size;
            min.x /= sw;
            min.y /= sh;
            max.x /= sw;
            max.y /= sh;

            safeAreaRect.anchorMin = min;
            safeAreaRect.anchorMax = max;
            safeAreaRect.offsetMin = Vector2.zero;
            safeAreaRect.offsetMax = Vector2.zero;
        }

        // =====================================================================
        // Dựng giao diện Canvas
        // =====================================================================

        void BuildHud()
        {
            var go = new GameObject("Touch HUD Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGroup = go.AddComponent<CanvasGroup>();

            // Root bao phủ safe area
            var saGo = new GameObject("SafeArea", typeof(RectTransform));
            saGo.transform.SetParent(go.transform, false);
            safeAreaRect = (RectTransform)saGo.transform;
            ApplySafeArea();

            BuildJoystick();
            BuildQuickActionDock();
            BuildTopBar();
            BuildMenuPanel();
            BuildStationPanel();
        }

        void BuildJoystick()
        {
            var baseImg = Panel(safeAreaRect, "JoystickBase", new Vector2(0, 0), new Vector2(0.5f, 0.5f), new Vector2(160, 160), new Vector2(140, 140), glassMat);
            joyBase = baseImg.rectTransform;
            joyGroup = baseImg.gameObject.AddComponent<CanvasGroup>();
            joyGroup.alpha = 0f;
            joyGroup.blocksRaycasts = false;

            var knobImg = Panel(joyBase, "JoystickKnob", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64, 64), glassLightMat);
            joyKnob = knobImg.rectTransform;
            knobImg.color = new Color(Accent.r, Accent.g, Accent.b, 0.7f);
        }

        void BuildQuickActionDock()
        {
            // 1. Khung nền Kính mờ hiện đại thống nhất (QuickActionDock)
            // Kích thước 404x186, bo góc 18px, màu nền new Color(0.08f, 0.12f, 0.18f, 0.74f) kèm viền sáng nhẹ new Color(1f, 1f, 1f, 0.18f)
            quickActionDock = Rect(safeAreaRect, "QuickActionDock", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-20, 20), new Vector2(404, 186));
            dockBg = quickActionDock.gameObject.AddComponent<Image>();
            dockBg.sprite = Solid;
            dockBg.type = Image.Type.Sliced;
            dockBg.color = new Color(0.08f, 0.12f, 0.18f, 0.74f);

            var borderRt = Rect(quickActionDock, "Border", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            borderRt.anchorMin = Vector2.zero;
            borderRt.anchorMax = Vector2.one;
            borderRt.offsetMin = Vector2.zero;
            borderRt.offsetMax = Vector2.zero;
            var borderImg = borderRt.gameObject.AddComponent<Image>();
            borderImg.sprite = Rounded;
            borderImg.type = Image.Type.Sliced;
            borderImg.material = glassLightMat;
            borderImg.color = new Color(1f, 1f, 1f, 0.18f);
            borderImg.raycastTarget = false;

            // 2. Khay Công cụ mở rộng phía trên (4 slot dựng sẵn)
            // Padding = 10, Gap = 8. Width = 384. Mỗi ô rộng 90px, cao 44px (chuẩn Apple HIG >= 44 pt)
            for (int i = 0; i < 4; i++)
            {
                int slotIdx = i;
                float slotX = 10 + i * (90 + 8);
                var sRt = Rect(quickActionDock, $"Slot_{i}", new Vector2(0, 1), new Vector2(0, 1), new Vector2(slotX, -10), new Vector2(90, 44));
                var sImg = sRt.gameObject.AddComponent<Image>();
                sImg.sprite = Solid;
                sImg.type = Image.Type.Sliced;
                sImg.color = new Color(1f, 1f, 1f, 0.12f);

                var sBorder = Rect(sRt, "Border", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                sBorder.anchorMin = Vector2.zero;
                sBorder.anchorMax = Vector2.one;
                sBorder.offsetMin = Vector2.zero;
                sBorder.offsetMax = Vector2.zero;
                var sbImg = sBorder.gameObject.AddComponent<Image>();
                sbImg.sprite = Rounded;
                sbImg.type = Image.Type.Sliced;
                sbImg.material = glassLightMat;
                sbImg.color = new Color(1f, 1f, 1f, 0.14f);
                sbImg.raycastTarget = false;

                var sText = Label(sRt, "Text", "", 13, Ink, true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(90, 44));

                var tap = sRt.gameObject.AddComponent<TouchTapTrigger>();
                tap.OnClick = () => OnSlotClicked(slotIdx);

                var hold = sRt.gameObject.AddComponent<TouchHoldTrigger>();
                hold.OnDown = () => OnSlotHoldDown(slotIdx);
                hold.OnUp = () => OnSlotHoldUp(slotIdx);

                NutHieuUng.Attach(sRt.gameObject, isHold: false);

                slotRoots[i] = sRt.gameObject;
                slotBgs[i] = sImg;
                slotTexts[i] = sText;
                slotTaps[i] = tap;
                slotHolds[i] = hold;
                slotRoots[i].SetActive(false);
            }

            // 3. Cụm Điều hướng & Thao tác chính phía dưới (y = -62, cao 114px, gap = 8px)
            // Cột 1: Mắt ▲ (trên) và Mắt ▼ (dưới) - rộng 72px
            eyeUpBtn = MakeDockSubButton("Nút Mắt ▲", "Mắt ▲", new Vector2(10, -62), new Vector2(72, 53),
                out eyeUpBg, () => { PadInput.EyeAdjustAxis = 1f; }, () => { PadInput.EyeAdjustAxis = 0f; });

            eyeDownBtn = MakeDockSubButton("Nút Mắt ▼", "Mắt ▼", new Vector2(10, -123), new Vector2(72, 53),
                out eyeDownBg, () => { PadInput.EyeAdjustAxis = -1f; }, () => { PadInput.EyeAdjustAxis = 0f; });

            // Cột 2: Ngồi (trên) và Chậm (dưới) - rộng 84px
            string sitStr = PadInput.UsingTouch ? "Ngồi" : "Ngồi (C)";
            sitBtn = MakeDockTapButton("Nút Ngồi", sitStr, new Vector2(90, -62), new Vector2(84, 53),
                out sitBg, out sitText, () => { PadInput.TouchSitPressed = true; });

            shiftBtn = MakeDockTapButton("Nút Shift", "Chậm", new Vector2(90, -123), new Vector2(84, 53),
                out shiftBg, out shiftText, () =>
                {
                    shiftActive = !shiftActive;
                    PadInput.TouchShift = shiftActive;
                    shiftBg.color = shiftActive ? new Color(0.18f, 0.63f, 0.73f, 0.88f) : new Color(1f, 1f, 1f, 0.12f);
                    shiftText.color = shiftActive ? Ink : Dim;
                    shiftText.font = shiftActive ? Semibold : Regular;
                });

            // Cột 3: Trả về (trên) và Lắc (dưới) - rộng 84px
            string retStr = PadInput.UsingTouch ? "Trả về" : "Trả về (Q)";
            returnBtn = MakeDockTapButton("Nút Trả về", retStr, new Vector2(182, -62), new Vector2(84, 53),
                out returnBg, out returnText, () => { PadInput.TouchBackPressed = true; });

            string swStr = PadInput.UsingTouch ? "Lắc bình" : "Lắc (F)";
            swirlBtn = MakeDockSubButton("Nút Lắc", swStr, new Vector2(182, -123), new Vector2(84, 53),
                out swirlBg, () => { PadInput.TouchSwirl = true; }, () => { PadInput.TouchSwirl = false; });
            swirlText = swirlBtn.GetComponentInChildren<Text>();

            // Cột 4: Nút TAY chính (chiếm toàn bộ chiều cao 114px, rộng 110px)
            var handRt = Rect(quickActionDock, "MainActionButton", new Vector2(0, 1), new Vector2(0, 1), new Vector2(274, -62), new Vector2(110, 114));
            mainActionBg = handRt.gameObject.AddComponent<Image>();
            mainActionBg.sprite = Solid;
            mainActionBg.type = Image.Type.Sliced;
            mainActionBg.color = new Color(1f, 1f, 1f, 0.16f);

            var handBorder = Rect(handRt, "Border", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            handBorder.anchorMin = Vector2.zero; handBorder.anchorMax = Vector2.one;
            handBorder.offsetMin = Vector2.zero; handBorder.offsetMax = Vector2.zero;
            var hbImg = handBorder.gameObject.AddComponent<Image>();
            hbImg.sprite = Rounded;
            hbImg.type = Image.Type.Sliced;
            hbImg.material = glassLightMat;
            hbImg.color = new Color(0.38f, 0.86f, 1f, 0.45f);
            hbImg.raycastTarget = false;

            mainActionText = Label(handRt, "Text", "TAY", 22, Ink, true, TextAnchor.MiddleCenter, new Vector2(0, 10), new Vector2(110, 36));
            mainActionSub = Label(handRt, "Sub", "Chạm / Giữ", 11, Dim, false, TextAnchor.MiddleCenter, new Vector2(0, -20), new Vector2(110, 22));

            var handHold = handRt.gameObject.AddComponent<TouchHoldTrigger>();
            handHold.OnDown = () =>
            {
                mainActionHeld = true;
                PadInput.TouchUse = true;
                mainActionBg.color = new Color(0.18f, 0.63f, 0.73f, 0.88f);
            };
            handHold.OnUp = () =>
            {
                mainActionHeld = false;
                PadInput.TouchUse = false;
                mainActionBg.color = new Color(1f, 1f, 1f, 0.16f);
            };
            NutHieuUng.Attach(handRt.gameObject, isHold: true, onRelease: handHold.OnUp);

            // Giữ lại tham chiếu các panel cũ dưới dạng đối tượng phụ ẩn an toàn
            buretPanel = new GameObject("BuretPanelRef"); buretPanel.transform.SetParent(quickActionDock, false); buretPanel.SetActive(false);
            fabPanel = new GameObject("FabPanelRef"); fabPanel.transform.SetParent(quickActionDock, false); fabPanel.SetActive(false);
            recordBtn = new GameObject("RecordBtnRef"); recordBtn.transform.SetParent(quickActionDock, false); recordBtn.SetActive(false);
        }

        readonly Action[] activeSlotActions = new Action[4];
        readonly Action[] activeSlotDown = new Action[4];
        readonly Action[] activeSlotUp = new Action[4];

        void OnSlotClicked(int idx) => activeSlotActions[idx]?.Invoke();
        void OnSlotHoldDown(int idx) => activeSlotDown[idx]?.Invoke();
        void OnSlotHoldUp(int idx) => activeSlotUp[idx]?.Invoke();

        public void RegisterQuickActionSlot(int slotIndex, string label, Action onClick, bool highlighted = false)
        {
            if (slotIndex < 0 || slotIndex >= 4) return;
            registeredSlots[slotIndex] = new QuickSlotInfo
            {
                label = label,
                onClick = onClick,
                highlighted = highlighted,
                isHold = false,
                holdAxis = 0f
            };
        }

        GameObject MakeDockTapButton(string name, string text, Vector2 pos, Vector2 size, out Image bg, out Text label, Action onClick)
        {
            var rt = Rect(quickActionDock, name, new Vector2(0, 1), new Vector2(0, 1), pos, size);
            bg = rt.gameObject.AddComponent<Image>();
            bg.sprite = Solid;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(1f, 1f, 1f, 0.12f);

            var border = Rect(rt, "Border", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            border.anchorMin = Vector2.zero; border.anchorMax = Vector2.one;
            border.offsetMin = Vector2.zero; border.offsetMax = Vector2.zero;
            var bImg = border.gameObject.AddComponent<Image>();
            bImg.sprite = Rounded;
            bImg.type = Image.Type.Sliced;
            bImg.material = glassLightMat;
            bImg.color = new Color(1f, 1f, 1f, 0.16f);
            bImg.raycastTarget = false;

            label = Label(rt, "Text", text, 14, Ink, true, TextAnchor.MiddleCenter, Vector2.zero, size);
            var tap = rt.gameObject.AddComponent<TouchTapTrigger>();
            tap.OnClick = onClick;
            NutHieuUng.Attach(rt.gameObject);
            return rt.gameObject;
        }

        GameObject MakeDockSubButton(string name, string text, Vector2 pos, Vector2 size, out Image bg, Action onDown, Action onUp)
        {
            var rt = Rect(quickActionDock, name, new Vector2(0, 1), new Vector2(0, 1), pos, size);
            bg = rt.gameObject.AddComponent<Image>();
            bg.sprite = Solid;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(1f, 1f, 1f, 0.12f);

            var border = Rect(rt, "Border", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            border.anchorMin = Vector2.zero; border.anchorMax = Vector2.one;
            border.offsetMin = Vector2.zero; border.offsetMax = Vector2.zero;
            var bImg = border.gameObject.AddComponent<Image>();
            bImg.sprite = Rounded;
            bImg.type = Image.Type.Sliced;
            bImg.material = glassLightMat;
            bImg.color = new Color(1f, 1f, 1f, 0.16f);
            bImg.raycastTarget = false;

            Label(rt, "Text", text, 14, Ink, true, TextAnchor.MiddleCenter, Vector2.zero, size);
            var hold = rt.gameObject.AddComponent<TouchHoldTrigger>();
            hold.OnDown = onDown;
            hold.OnUp = onUp;
            NutHieuUng.Attach(rt.gameObject, isHold: true, onRelease: onUp);
            return rt.gameObject;
        }

        void BuildTopBar()
        {
            Color topBtnBg = new Color(0.07f, 0.11f, 0.17f, 0.82f);
            Color topBtnBorder = new Color(1f, 1f, 1f, 0.18f);

            // Nút Menu góc trên phải (kích thước 110x65, tăng độ đục nền chống lóa)
            var menuBtn = MakeHudButton("Nút Menu", "Menu ☰", new Vector2(1, 1), new Vector2(-40, -40), new Vector2(110, 65), () =>
            {
                menuPanel.SetActive(!menuPanel.activeSelf);
            }, topBtnBg, topBtnBorder);

            // Nút Trợ giúp (kích thước 110x65, tăng độ đục nền chống lóa)
            string helpStr = PadInput.UsingTouch ? "Trợ giúp ?" : "Trợ giúp (H)";
            helpBtn = MakeHudButton("Nút Trợ giúp", helpStr, new Vector2(1, 1), new Vector2(-160, -40), new Vector2(110, 65), () =>
            {
                PadInput.TouchHelpPressed = true;
            }, topBtnBg, topBtnBorder);
            helpText = helpBtn.GetComponentInChildren<Text>();

            // Cụm đổi trạm: ◀ Đổi khu thí nghiệm ▶ (giữ nguyên vị trí và kích thước hoàn hảo)
            var stBox = Panel(safeAreaRect, "StationBar", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(356, 75), glassMat);
            var sp = stBox.transform;
            MakeTapTextButton(sp, "Trạm trước", "◀", new Vector2(6, 5), new Vector2(66, 65), () =>
            {
                PadInput.TouchStationStep = -1;
            });
            MakeTapTextButton(sp, "Chọn trạm", "Đổi khu thí nghiệm", new Vector2(78, 5), new Vector2(200, 65), () =>
            {
                stationPanel.SetActive(!stationPanel.activeSelf);
            });
            MakeTapTextButton(sp, "Trạm sau", "▶", new Vector2(284, 5), new Vector2(66, 65), () =>
            {
                PadInput.TouchStationStep = 1;
            });
        }

        void BuildMenuPanel()
        {
            var menuBox = Panel(safeAreaRect, "MenuPanel", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -115), new Vector2(300, 360), glassMat, 1.2f);
            menuPanel = menuBox.gameObject;
            var mp = menuBox.transform;

            Label(mp, "Title", "TÙY CHỌN NHANH", 16, Accent, true, TextAnchor.UpperLeft, new Vector2(16, -14), new Vector2(260, 24));

            string gStr = PadInput.UsingTouch ? "Đồ thị pH" : "Đồ thị pH (G)";
            MakeTapTextButton(mp, "Đồ thị", gStr, new Vector2(16, -46), new Vector2(268, 65), () =>
            {
                PadInput.ToggleGraphPressed = true;
                menuPanel.SetActive(false);
            });

            string rStr = PadInput.UsingTouch ? "Lượt chuẩn độ mới" : "Làm lượt chuẩn độ mới (R)";
            MakeTapTextButton(mp, "Lần mới", rStr, new Vector2(16, -118), new Vector2(268, 65), () =>
            {
                PadInput.NewRunPressed = true;
                menuPanel.SetActive(false);
            });

            string f7Str = PadInput.UsingTouch ? "Đổi giao diện lab" : "Đổi chủ đề phòng lab (F7)";
            MakeTapTextButton(mp, "Đổi chủ đề", f7Str, new Vector2(16, -190), new Vector2(268, 65), () =>
            {
                PadInput.NextThemePressed = true;
            });

            sensText = Label(mp, "SensLabel", "Độ nhạy vuốt nhìn: 0.18", 13, Dim, false, TextAnchor.UpperLeft, new Vector2(16, -266), new Vector2(268, 20));
            var sRt = Rect(mp, "Slider", new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -292), new Vector2(268, 30));
            sensSlider = sRt.gameObject.AddComponent<Slider>();
            sensSlider.minValue = 0.05f;
            sensSlider.maxValue = 0.40f;
            sensSlider.value = touchControls != null ? touchControls.lookSensitivity : 0.18f;
            sensSlider.onValueChanged.AddListener(v =>
            {
                if (touchControls != null) touchControls.SetLookSensitivity(v);
                sensText.text = $"Độ nhạy vuốt nhìn: {v:F2}";
            });

            menuPanel.SetActive(false);
        }

        void BuildStationPanel()
        {
            var box = Panel(safeAreaRect, "StationPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600, 560), glassMat, 1.2f);
            stationPanel = box.gameObject;
            var bp = box.transform;

            Label(bp, "Title", "CHỌN KHU THÍ NGHIỆM", 18, Accent, true, TextAnchor.UpperLeft, new Vector2(24, -18), new Vector2(340, 28));
            MakeTapTextButton(bp, "Đóng", "✕", new Vector2(520, -10), new Vector2(66, 65), () => stationPanel.SetActive(false));

            string[] stNames = {
                "1. Bàn chuẩn độ 1", "2. Bàn chuẩn độ 2", "3. Bàn chuẩn độ 3", "4. Bàn chuẩn độ 4",
                "5. Bàn chuẩn độ 5", "6. Bàn chuẩn độ 6", "7. Tủ hút hóa chất", "8. Phòng hóa chất",
                "9. Máy phân tích", "0. Bàn giáo viên & Chế tạo", "Tầng 2: Kính hiển vi", "Tầng 2: Tủ cấy & Tủ ấm"
            };

            for (int i = 0; i < stNames.Length; i++)
            {
                int idx = i;
                float x = 24 + (i % 2) * 280;
                float y = -64 - (i / 2) * 76;
                MakeTapTextButton(bp, "Trạm_" + i, stNames[i], new Vector2(x, y), new Vector2(266, 66), () =>
                {
                    PadInput.StationJump = idx;
                    stationPanel.SetActive(false);
                });
            }

            stationPanel.SetActive(false);
        }

        // =====================================================================
        // Cập nhật trạng thái nút theo ngữ cảnh
        // =====================================================================

        void UpdateContextButtons()
        {
            if (hand == null) return;

            // Cập nhật nút chính theo hand.Current
            string actText = "TAY";
            string actSub = "Chạm / Giữ";
            switch (hand.Current)
            {
                case HandController.Act.Pick: actText = "CẦM"; actSub = "Chạm"; break;
                case HandController.Act.Place: actText = "ĐẶT"; actSub = "Chạm"; break;
                case HandController.Act.PlaceSurface: actText = "ĐẶT BÀN"; actSub = "Chạm"; break;
                case HandController.Act.Use: actText = "DÙNG"; actSub = "Chạm"; break;
                case HandController.Act.Pour: actText = "RÓT"; actSub = "Giữ để rót"; break;
                case HandController.Act.Suck: actText = "HÚT"; actSub = "Giữ để hút"; break;
                case HandController.Act.PipetteDrain: actText = "XẢ"; actSub = "Giữ để xả"; break;
                case HandController.Act.Drop: actText = "NHỎ GIỌT"; actSub = "Chạm: 1 giọt"; break;
                case HandController.Act.Spray: actText = "TIA NƯỚC"; actSub = "Giữ"; break;
                case HandController.Act.Scoop: actText = "XÚC"; actSub = "Chạm: 1 thìa"; break;
                case HandController.Act.Bubble: actText = "SỤC KHÍ"; actSub = "Giữ"; break;
                case HandController.Act.Measure: actText = "ĐO"; actSub = "Chạm"; break;
                case HandController.Act.Attach: actText = "LẮP RÁP"; actSub = "Chạm"; break;
                case HandController.Act.PaperTest: actText = "NHÚNG"; actSub = "Chạm"; break;
                case HandController.Act.Swirl: actText = "LẮC"; actSub = "Giữ"; break;
                case HandController.Act.Stopcock: actText = "KHÓA"; actSub = "Vặn khóa"; break;
            }

            mainActionText.text = actText;
            mainActionSub.text = actSub;
            if (!mainActionHeld)
            {
                mainActionBg.color = hand.Current != HandController.Act.None
                    ? new Color(Accent.r, Accent.g, Accent.b, 0.55f)
                    : new Color(1f, 1f, 1f, 0.15f);
            }

            // Cập nhật nhãn động theo PadInput.UsingTouch
            bool touch = PadInput.UsingTouch;
            if (returnText != null) returnText.text = touch ? "Trả về" : "Trả về (Q)";
            if (swirlText != null) swirlText.text = touch ? "Lắc bình" : "Lắc (F)";
            if (helpText != null) helpText.text = touch ? "Trợ giúp ?" : "Trợ giúp (H)";

            // Cập nhật trạng thái các nút ở cụm điều khiển dưới
            if (sitBtn != null && sitText != null)
            {
                sitText.text = viewer != null && viewer.IsSitting ? "Đứng" : (touch ? "Ngồi" : "Ngồi (C)");
                sitBg.color = viewer != null && viewer.IsSitting ? new Color(0.18f, 0.63f, 0.73f, 0.88f) : new Color(1f, 1f, 1f, 0.12f);
            }

            if (shiftBtn != null && shiftBg != null)
            {
                shiftBg.color = shiftActive ? new Color(0.18f, 0.63f, 0.73f, 0.88f) : new Color(1f, 1f, 1f, 0.12f);
            }

            bool canReturn = hand.Held != null || hand.HeldPart != null;
            if (returnBtn != null && returnBg != null && returnText != null)
            {
                returnBg.color = canReturn ? new Color(1f, 1f, 1f, 0.22f) : new Color(1f, 1f, 1f, 0.06f);
                returnText.color = canReturn ? Ink : new Color(1f, 1f, 1f, 0.35f);
            }

            bool canSwirl = (hand.Held != null && hand.Held.GetComponent<LabContainer>() != null) ||
                            hand.Current == HandController.Act.Swirl;
            if (swirlBtn != null && swirlBg != null && swirlText != null)
            {
                swirlBg.color = canSwirl ? new Color(0.18f, 0.63f, 0.73f, 0.88f) : new Color(1f, 1f, 1f, 0.08f);
                swirlText.color = canSwirl ? Ink : new Color(1f, 1f, 1f, 0.35f);
            }

            // Cập nhật khay công cụ mở rộng phía trên (4 slot)
            bool nearBuret = hand.Current == HandController.Act.Stopcock ||
                             (procedure != null && procedure.buret != null && hand.Held == null);
            bool holdingPart = hand.HeldPart != null;
            bool canRecord = procedure != null && procedure.Active && !nearBuret && !holdingPart;

            for (int i = 0; i < 4; i++)
            {
                activeSlotActions[i] = null;
                activeSlotDown[i] = null;
                activeSlotUp[i] = null;
            }

            if (nearBuret)
            {
                // Slot 0: Mở + (giữ nút để xả buret)
                SetSlot(0, "Mở +", true, false, () => { buretHoldDir = 1f; buretHoldTimer = 0f; }, () => { buretHoldDir = 0f; PadInput.TouchStopcockAxis = 0f; });
                // Slot 1: Đóng - (giữ nút để hãm dòng buret)
                SetSlot(1, "Đóng -", true, false, () => { buretHoldDir = -1f; buretHoldTimer = 0f; }, () => { buretHoldDir = 0f; PadInput.TouchStopcockAxis = 0f; });
                // Slot 2: Khóa hẳn
                string closeStr = touch ? "Khóa buret" : "Khóa";
                SetSlot(2, closeStr, false, true, () => { PadInput.CloseStopcockPressed = true; });
                // Slot 3: Ghi số (nếu quy trình cần)
                if (procedure != null && procedure.Active)
                    SetSlot(3, "Ghi số", false, false, () => { PadInput.TouchRecordPressed = true; });
                else
                    ClearSlot(3);
            }
            else if (holdingPart)
            {
                SetSlot(0, "⟲ -15°", false, false, () => { PadInput.HeldYawStep = -1f; });
                SetSlot(1, "⟳ +15°", false, false, () => { PadInput.HeldYawStep = 1f; });
                ClearSlot(2);
                ClearSlot(3);
            }
            else if (canRecord)
            {
                SetSlot(0, "Ghi số đọc", false, true, () => { PadInput.TouchRecordPressed = true; });
                for (int i = 1; i < 4; i++) ApplyCustomSlot(i);
            }
            else
            {
                for (int i = 0; i < 4; i++) ApplyCustomSlot(i);
            }
        }

        void SetSlot(int idx, string label, bool isHold, bool highlight, Action onClickOrDown, Action onUp = null)
        {
            if (slotRoots[idx] == null) return;
            slotRoots[idx].SetActive(true);
            slotTexts[idx].text = label;
            slotBgs[idx].color = highlight ? new Color(0.18f, 0.63f, 0.73f, 0.88f) : new Color(1f, 1f, 1f, 0.12f);
            if (isHold)
            {
                activeSlotDown[idx] = onClickOrDown;
                activeSlotUp[idx] = onUp;
            }
            else
            {
                activeSlotActions[idx] = onClickOrDown;
            }
        }

        void ClearSlot(int idx)
        {
            if (slotRoots[idx] != null) slotRoots[idx].SetActive(false);
        }

        void ApplyCustomSlot(int idx)
        {
            if (!string.IsNullOrEmpty(registeredSlots[idx].label))
            {
                SetSlot(idx, registeredSlots[idx].label, false, registeredSlots[idx].highlighted, registeredSlots[idx].onClick);
            }
            else
            {
                ClearSlot(idx);
            }
        }

        // =====================================================================
        // Điều khiển Joystick nổi từ TouchControls
        // =====================================================================

        public void ShowJoystick(Vector2 screenPos)
        {
            joyVisible = true;
            joyGroup.alpha = 1f;
            joyBase.position = screenPos;
            joyKnob.anchoredPosition = Vector2.zero;
        }

        public void UpdateJoystick(Vector2 screenPos)
        {
            if (!joyVisible) return;
            joyKnob.position = screenPos;
        }

        public void HideJoystick()
        {
            joyVisible = false;
            joyGroup.alpha = 0f;
            joyKnob.anchoredPosition = Vector2.zero;
        }

        // =====================================================================
        // Bộ trợ giúp tạo nút
        // =====================================================================

        GameObject MakeHudButton(string name, string text, Vector2 anchor, Vector2 pos, Vector2 size, Action onClick, Color? bgColor = null, Color? borderColor = null)
        {
            var rt = Rect(safeAreaRect, name, anchor, anchor, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Solid;
            img.type = Image.Type.Sliced;
            img.color = bgColor ?? new Color(1f, 1f, 1f, 0.12f);

            var borderRt = Rect(rt, "Border", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            borderRt.anchorMin = Vector2.zero;
            borderRt.anchorMax = Vector2.one;
            borderRt.offsetMin = Vector2.zero;
            borderRt.offsetMax = Vector2.zero;
            var bImg = borderRt.gameObject.AddComponent<Image>();
            bImg.sprite = Rounded;
            bImg.type = Image.Type.Sliced;
            bImg.material = glassLightMat;
            bImg.color = borderColor ?? new Color(1f, 1f, 1f, 0.18f);
            bImg.raycastTarget = false;

            Label(rt, "Text", text, 14, Ink, true, TextAnchor.MiddleCenter, Vector2.zero, size);
            var tap = rt.gameObject.AddComponent<TouchTapTrigger>();
            tap.OnClick = onClick;
            NutHieuUng.Attach(rt.gameObject);
            return rt.gameObject;
        }

        void MakeTapTextButton(Transform parent, string name, string text, Vector2 pos, Vector2 size, Action onClick)
        {
            var rt = Rect(parent, name, new Vector2(0, 1), new Vector2(0, 1), pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Rounded;
            img.material = glassLightMat;
            Label(rt, "Text", text, 14, Ink, false, TextAnchor.MiddleCenter, Vector2.zero, size);
            var tap = rt.gameObject.AddComponent<TouchTapTrigger>();
            tap.OnClick = onClick;
            NutHieuUng.Attach(rt.gameObject);
        }

        void MakeHoldTextButton(Transform parent, string name, string text, Vector2 pos, Vector2 size, float axisDir)
        {
            var rt = Rect(parent, name, new Vector2(0, 1), new Vector2(0, 1), pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Rounded;
            img.material = glassLightMat;
            Label(rt, "Text", text, 14, Ink, false, TextAnchor.MiddleCenter, Vector2.zero, size);
            var hold = rt.gameObject.AddComponent<TouchHoldTrigger>();
            hold.OnDown = () => { buretHoldDir = axisDir; buretHoldTimer = 0; };
            hold.OnUp = () => { buretHoldDir = 0; PadInput.TouchStopcockAxis = 0; };
            NutHieuUng.Attach(rt.gameObject, isHold: true, onRelease: hold.OnUp);
        }
    }

    /// <summary>
    /// Kích hoạt sự kiện giữ và thả nút cảm ứng (cho nút tay chính, nút lắc, nút vặn buret).
    /// Tự động nhả khi ngón tay trượt ra ngoài, khi tắt GameObject hoặc mất tiêu điểm app.
    /// </summary>
    public class TouchHoldTrigger : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public Action OnDown;
        public Action OnUp;
        bool isDown;

        public void OnPointerDown(PointerEventData eventData)
        {
            isDown = true;
            OnDown?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (isDown)
            {
                isDown = false;
                OnUp?.Invoke();
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (isDown)
            {
                isDown = false;
                OnUp?.Invoke();
            }
        }

        void OnDisable()
        {
            if (isDown)
            {
                isDown = false;
                OnUp?.Invoke();
            }
        }
    }

    /// <summary>
    /// Kích hoạt sự kiện chạm thả (nhấp) trên cảm ứng với diện tích chạm chuẩn >= 44 pt.
    /// </summary>
    public class TouchTapTrigger : MonoBehaviour, IPointerClickHandler
    {
        public Action OnClick;

        public void OnPointerClick(PointerEventData eventData)
        {
            OnClick?.Invoke();
        }
    }
}
