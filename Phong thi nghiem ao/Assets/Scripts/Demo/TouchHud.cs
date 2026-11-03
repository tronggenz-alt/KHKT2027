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

        // Nút tay chính
        Image mainActionBg;
        Text mainActionText, mainActionSub;
        bool mainActionHeld;

        // Các nút ngữ cảnh
        GameObject returnBtn, swirlBtn, shiftBtn, sitBtn, eyeUpBtn, eyeDownBtn;
        GameObject buretPanel, fabPanel, recordBtn, helpBtn;
        Text returnText, swirlText, sitText, helpText;
        Image shiftBg;
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
            BuildMainActionButton();
            BuildSurroundingButtons();
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

        void BuildMainActionButton()
        {
            // Nút tay chính hình tròn lớn ở góc phải dưới (kích thước 110x110)
            var rt = Rect(safeAreaRect, "MainActionButton", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 40), new Vector2(110, 110));
            mainActionBg = rt.gameObject.AddComponent<Image>();
            mainActionBg.sprite = SolidCircle;
            mainActionBg.color = new Color(Accent.r, Accent.g, Accent.b, 0.35f);

            var border = Rect(rt, "Border", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110, 110)).gameObject.AddComponent<Image>();
            border.sprite = Circle;
            border.material = glassLightMat;
            border.raycastTarget = false;

            mainActionText = Label(rt, "Text", "TAY", 20, Ink, true, TextAnchor.MiddleCenter, new Vector2(0, 6), new Vector2(100, 30));
            mainActionSub = Label(rt, "Sub", "Chạm / Giữ", 11, Dim, false, TextAnchor.MiddleCenter, new Vector2(0, -18), new Vector2(100, 20));

            var hold = rt.gameObject.AddComponent<TouchHoldTrigger>();
            hold.OnDown = () =>
            {
                mainActionHeld = true;
                PadInput.TouchUse = true;
                mainActionBg.color = new Color(Accent.r, Accent.g, Accent.b, 0.75f);
            };
            hold.OnUp = () =>
            {
                mainActionHeld = false;
                PadInput.TouchUse = false;
                mainActionBg.color = new Color(Accent.r, Accent.g, Accent.b, 0.35f);
            };
            NutHieuUng.Attach(rt.gameObject, isHold: true, onRelease: hold.OnUp);
        }

        void BuildSurroundingButtons()
        {
            // Nút Trả về (Q) - phía trên nút tay (kích thước 104x65 units >= 44 pt)
            string retStr = PadInput.UsingTouch ? "Trả về" : "Trả về (Q)";
            returnBtn = MakeHudButton("Nút Trả về", retStr, new Vector2(1, 0), new Vector2(-40, 168), new Vector2(110, 65), () =>
            {
                PadInput.TouchBackPressed = true;
            });
            returnText = returnBtn.GetComponentInChildren<Text>();

            // Nút Lắc bình (F) - giữ để lắc (kích thước 96x65)
            var swirlRt = Rect(safeAreaRect, "Nút Lắc", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-168, 40), new Vector2(96, 65));
            var swirlImg = swirlRt.gameObject.AddComponent<Image>();
            swirlImg.sprite = Rounded;
            swirlImg.material = glassLightMat;
            string swStr = PadInput.UsingTouch ? "Lắc bình" : "Lắc (F)";
            swirlText = Label(swirlRt, "Text", swStr, 15, Ink, true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(96, 65));
            var swirlHold = swirlRt.gameObject.AddComponent<TouchHoldTrigger>();
            swirlHold.OnDown = () => PadInput.TouchSwirl = true;
            swirlHold.OnUp = () => PadInput.TouchSwirl = false;
            NutHieuUng.Attach(swirlRt.gameObject, isHold: true, onRelease: swirlHold.OnUp);
            swirlBtn = swirlRt.gameObject;

            // Nút Chậm / Nhanh (Shift) - bật/tắt (kích thước 96x65)
            var shiftRt = Rect(safeAreaRect, "Nút Shift", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-168, 114), new Vector2(96, 65));
            shiftBg = shiftRt.gameObject.AddComponent<Image>();
            shiftBg.sprite = Rounded;
            shiftBg.material = glassLightMat;
            shiftText = Label(shiftRt, "Text", "Chậm", 14, Dim, false, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(96, 65));
            var shiftTap = shiftRt.gameObject.AddComponent<TouchTapTrigger>();
            shiftTap.OnClick = () =>
            {
                shiftActive = !shiftActive;
                PadInput.TouchShift = shiftActive;
                shiftBg.color = shiftActive ? new Color(Accent.r, Accent.g, Accent.b, 0.6f) : Color.white;
                shiftText.color = shiftActive ? Ink : Dim;
                shiftText.font = shiftActive ? Semibold : Regular;
            };
            NutHieuUng.Attach(shiftRt.gameObject);
            shiftBtn = shiftRt.gameObject;

            // Nút Ngồi / Đứng (C) (kích thước 96x65)
            string sitStr = PadInput.UsingTouch ? "Ngồi" : "Ngồi (C)";
            sitBtn = MakeHudButton("Nút Ngồi", sitStr, new Vector2(1, 0), new Vector2(-168, 188), new Vector2(96, 65), () =>
            {
                PadInput.TouchSitPressed = true;
            });
            sitText = sitBtn.GetComponentInChildren<Text>();

            // Nút Tầm mắt: Nâng / Hạ (kích thước 80x65)
            var eyeUpRt = Rect(safeAreaRect, "Nút Tầm mắt lên", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-274, 114), new Vector2(80, 65));
            var eyeUpImg = eyeUpRt.gameObject.AddComponent<Image>();
            eyeUpImg.sprite = Rounded;
            eyeUpImg.material = glassLightMat;
            Label(eyeUpRt, "Text", "Mắt ▲", 14, Ink, true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(80, 65));
            var eyeUpHold = eyeUpRt.gameObject.AddComponent<TouchHoldTrigger>();
            eyeUpHold.OnDown = () => PadInput.EyeAdjustAxis = 1f;
            eyeUpHold.OnUp = () => PadInput.EyeAdjustAxis = 0f;
            NutHieuUng.Attach(eyeUpRt.gameObject, isHold: true, onRelease: eyeUpHold.OnUp);
            eyeUpBtn = eyeUpRt.gameObject;

            var eyeDownRt = Rect(safeAreaRect, "Nút Tầm mắt xuống", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-274, 40), new Vector2(80, 65));
            var eyeDownImg = eyeDownRt.gameObject.AddComponent<Image>();
            eyeDownImg.sprite = Rounded;
            eyeDownImg.material = glassLightMat;
            Label(eyeDownRt, "Text", "Mắt ▼", 14, Ink, true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(80, 65));
            var eyeDownHold = eyeDownRt.gameObject.AddComponent<TouchHoldTrigger>();
            eyeDownHold.OnDown = () => PadInput.EyeAdjustAxis = -1f;
            eyeDownHold.OnUp = () => PadInput.EyeAdjustAxis = 0f;
            NutHieuUng.Attach(eyeDownRt.gameObject, isHold: true, onRelease: eyeDownHold.OnUp);
            eyeDownBtn = eyeDownRt.gameObject;

            // Bảng điều khiển buret (Mở / Đóng / Khóa hẳn - nút cao 65)
            var buretBox = Panel(safeAreaRect, "BuretPanel", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 248), new Vector2(340, 80), glassMat);
            buretPanel = buretBox.gameObject;
            var bp = buretBox.transform;
            MakeHoldTextButton(bp, "Mở khóa", "Mở +", new Vector2(10, 8), new Vector2(100, 65), 1f);
            MakeHoldTextButton(bp, "Đóng bớt", "Đóng -", new Vector2(120, 8), new Vector2(100, 65), -1f);
            string closeBuretStr = PadInput.UsingTouch ? "Khóa buret" : "Khóa (Space)";
            MakeTapTextButton(bp, "Khóa hẳn", closeBuretStr, new Vector2(230, 8), new Vector2(100, 65), () =>
            {
                PadInput.CloseStopcockPressed = true;
            });
            buretPanel.SetActive(false);

            // Bảng xoay bộ phận lắp ráp (Xoay -15° / Xoay +15° - nút cao 65)
            var fabBox = Panel(safeAreaRect, "FabPanel", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 248), new Vector2(236, 80), glassMat);
            fabPanel = fabBox.gameObject;
            var fp = fabBox.transform;
            MakeTapTextButton(fp, "Xoay trái", "⟲ -15°", new Vector2(10, 8), new Vector2(104, 65), () =>
            {
                PadInput.HeldYawStep = -1f;
            });
            MakeTapTextButton(fp, "Xoay phải", "⟳ +15°", new Vector2(122, 8), new Vector2(104, 65), () =>
            {
                PadInput.HeldYawStep = 1f;
            });
            fabPanel.SetActive(false);

            // Nút Ghi số đọc (kích thước 140x65)
            string recStr = PadInput.UsingTouch ? "Ghi số đọc" : "Ghi số (Enter)";
            recordBtn = MakeHudButton("Nút Ghi số", recStr, new Vector2(1, 0), new Vector2(-40, 248), new Vector2(140, 65), () =>
            {
                PadInput.TouchRecordPressed = true;
            });
            recordBtn.SetActive(false);
        }

        void BuildTopBar()
        {
            // Nút Menu góc trên phải (kích thước 110x65)
            var menuBtn = MakeHudButton("Nút Menu", "Menu ☰", new Vector2(1, 1), new Vector2(-40, -40), new Vector2(110, 65), () =>
            {
                menuPanel.SetActive(!menuPanel.activeSelf);
            });

            // Nút Trợ giúp (kích thước 110x65)
            string helpStr = PadInput.UsingTouch ? "Trợ giúp ?" : "Trợ giúp (H)";
            helpBtn = MakeHudButton("Nút Trợ giúp", helpStr, new Vector2(1, 1), new Vector2(-160, -40), new Vector2(110, 65), () =>
            {
                PadInput.TouchHelpPressed = true;
            });
            helpText = helpBtn.GetComponentInChildren<Text>();

            // Cụm đổi trạm: ◀ Đổi khu thí nghiệm ▶ (nút cao 65)
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
            if (sitText != null) sitText.text = touch ? "Ngồi" : "Ngồi (C)";
            if (helpText != null) helpText.text = touch ? "Trợ giúp ?" : "Trợ giúp (H)";

            // Nút Trả về (Q): chỉ hiện khi đang cầm vật
            returnBtn.SetActive(hand.Held != null);

            // Nút Lắc (F): hiện khi có thể lắc
            bool canSwirl = (hand.Held != null && hand.Held.GetComponent<LabContainer>() != null) ||
                            hand.Current == HandController.Act.Swirl;
            swirlBtn.SetActive(canSwirl);

            // Buret: hiện panel vặn / khóa khi trỏ vào khóa buret hoặc quy trình có buret
            bool nearBuret = hand.Current == HandController.Act.Stopcock ||
                             (procedure != null && procedure.buret != null && hand.Held == null);
            buretPanel.SetActive(nearBuret);

            // Lắp ráp: hiện panel xoay khi đang cầm bộ phận lắp ráp
            bool holdingPart = hand.HeldPart != null;
            fabPanel.SetActive(holdingPart && !nearBuret);

            // Ghi số: hiện khi quy trình đang chạy và có thể ghi số đọc
            bool canRecord = procedure != null && procedure.Active && !nearBuret && !holdingPart;
            recordBtn.SetActive(canRecord);
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

        GameObject MakeHudButton(string name, string text, Vector2 anchor, Vector2 pos, Vector2 size, Action onClick)
        {
            var rt = Rect(safeAreaRect, name, anchor, anchor, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Rounded;
            img.material = glassLightMat;
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
