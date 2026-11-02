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
    /// Giao diện cảm ứng cho iPad (Touch HUD):
    ///   • Cần di chuyển nổi bên trái dưới (hiện đúng chỗ đặt ngón).
    ///   • Nửa phải trống: vuốt xoay góc nhìn.
    ///   • Nút tay chính lớn bên phải dưới, chữ đổi theo HandController.Current (Cầm, Đặt, Dùng, Rót, Hút, Xả...).
    ///   • Các nút phụ xếp quanh nút chính: Trả về (Q), Lắc bình (F), Chậm / Nhanh (Shift), Ngồi / Đứng (C),
    ///     Tầm mắt (Z/X), Trạm (1-9, 0), Vặn / đóng khóa buret, Xoay vật lắp ráp (R/E), Ghi số (Enter).
    ///   • Menu nhanh góc trên: Đồ thị (G), Lần mới (R), Đổi chủ đề (F7), Trợ giúp (H), Độ nhạy vuốt.
    ///   • Tự động ẩn khi có bảng UI mở (UiCapture > 0), tự co theo Screen.safeArea.
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
            lastScreenSize = new Vector2(Screen.width, Screen.height);
            if (safeAreaRect == null) return;

            Rect safe = Screen.safeArea;
            Vector2 min = safe.position;
            Vector2 max = safe.position + safe.size;
            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;

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

            var knobImg = Panel(joyBase, "JoystickKnob", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60, 60), glassLightMat);
            joyKnob = knobImg.rectTransform;
            knobImg.color = new Color(Accent.r, Accent.g, Accent.b, 0.7f);
        }

        void BuildMainActionButton()
        {
            // Nút tay chính hình tròn lớn ở góc phải dưới
            var rt = Rect(safeAreaRect, "MainActionButton", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 40), new Vector2(104, 104));
            mainActionBg = rt.gameObject.AddComponent<Image>();
            mainActionBg.sprite = SolidCircle;
            mainActionBg.color = new Color(Accent.r, Accent.g, Accent.b, 0.35f);

            var border = Rect(rt, "Border", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(104, 104)).gameObject.AddComponent<Image>();
            border.sprite = Circle;
            border.material = glassLightMat;
            border.raycastTarget = false;

            mainActionText = Label(rt, "Text", "TAY", 20, Ink, true, TextAnchor.MiddleCenter, new Vector2(0, 6), new Vector2(96, 30));
            mainActionSub = Label(rt, "Sub", "Chạm / Giữ", 11, Dim, false, TextAnchor.MiddleCenter, new Vector2(0, -18), new Vector2(96, 20));

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
        }

        void BuildSurroundingButtons()
        {
            // Nút Trả về (Q) - phía trên nút tay
            returnBtn = MakeHudButton("Nút Trả về", "Trả về (Q)", new Vector2(1, 0), new Vector2(-40, 160), new Vector2(104, 48), () =>
            {
                PadInput.TouchBackPressed = true;
            });

            // Nút Lắc bình (F) - giữ để lắc
            var swirlRt = Rect(safeAreaRect, "Nút Lắc", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-160, 40), new Vector2(88, 52));
            var swirlImg = swirlRt.gameObject.AddComponent<Image>();
            swirlImg.sprite = Rounded;
            swirlImg.material = glassLightMat;
            Label(swirlRt, "Text", "Lắc (F)", 15, Ink, true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(88, 52));
            var swirlHold = swirlRt.gameObject.AddComponent<TouchHoldTrigger>();
            swirlHold.OnDown = () => PadInput.TouchSwirl = true;
            swirlHold.OnUp = () => PadInput.TouchSwirl = false;
            swirlBtn = swirlRt.gameObject;

            // Nút Chậm / Nhanh (Shift) - bật/tắt
            var shiftRt = Rect(safeAreaRect, "Nút Shift", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-160, 102), new Vector2(88, 52));
            shiftBg = shiftRt.gameObject.AddComponent<Image>();
            shiftBg.sprite = Rounded;
            shiftBg.material = glassLightMat;
            shiftText = Label(shiftRt, "Text", "Chậm", 14, Dim, false, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(88, 52));
            var shiftTap = shiftRt.gameObject.AddComponent<TouchTapTrigger>();
            shiftTap.OnClick = () =>
            {
                shiftActive = !shiftActive;
                PadInput.TouchShift = shiftActive;
                shiftBg.color = shiftActive ? new Color(Accent.r, Accent.g, Accent.b, 0.6f) : Color.white;
                shiftText.color = shiftActive ? Ink : Dim;
                shiftText.font = shiftActive ? Semibold : Regular;
            };
            shiftBtn = shiftRt.gameObject;

            // Nút Ngồi / Đứng (C)
            sitBtn = MakeHudButton("Nút Ngồi", "Ngồi (C)", new Vector2(1, 0), new Vector2(-160, 164), new Vector2(88, 48), () =>
            {
                PadInput.TouchSitPressed = true;
            });

            // Nút Tầm mắt: Nâng (X) / Hạ (Z)
            var eyeUpRt = Rect(safeAreaRect, "Nút Tầm mắt lên", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-260, 102), new Vector2(72, 52));
            var eyeUpImg = eyeUpRt.gameObject.AddComponent<Image>();
            eyeUpImg.sprite = Rounded;
            eyeUpImg.material = glassLightMat;
            Label(eyeUpRt, "Text", "Mắt ▲", 14, Ink, true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(72, 52));
            var eyeUpHold = eyeUpRt.gameObject.AddComponent<TouchHoldTrigger>();
            eyeUpHold.OnDown = () => PadInput.EyeAdjustAxis = 1f;
            eyeUpHold.OnUp = () => PadInput.EyeAdjustAxis = 0f;
            eyeUpBtn = eyeUpRt.gameObject;

            var eyeDownRt = Rect(safeAreaRect, "Nút Tầm mắt xuống", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-260, 40), new Vector2(72, 52));
            var eyeDownImg = eyeDownRt.gameObject.AddComponent<Image>();
            eyeDownImg.sprite = Rounded;
            eyeDownImg.material = glassLightMat;
            Label(eyeDownRt, "Text", "Mắt ▼", 14, Ink, true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(72, 52));
            var eyeDownHold = eyeDownRt.gameObject.AddComponent<TouchHoldTrigger>();
            eyeDownHold.OnDown = () => PadInput.EyeAdjustAxis = -1f;
            eyeDownHold.OnUp = () => PadInput.EyeAdjustAxis = 0f;
            eyeDownBtn = eyeDownRt.gameObject;

            // Bảng điều khiển buret (Mở / Đóng / Khóa hẳn)
            var buretBox = Panel(safeAreaRect, "BuretPanel", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 224), new Vector2(300, 56), glassMat);
            buretPanel = buretBox.gameObject;
            var bp = buretBox.transform;
            MakeHoldTextButton(bp, "Mở khóa", "Mở +", new Vector2(8, 6), new Vector2(88, 44), 1f);
            MakeHoldTextButton(bp, "Đóng bớt", "Đóng -", new Vector2(104, 6), new Vector2(88, 44), -1f);
            MakeTapTextButton(bp, "Khóa hẳn", "Khóa (Space)", new Vector2(200, 6), new Vector2(92, 44), () =>
            {
                PadInput.CloseStopcockPressed = true;
            });
            buretPanel.SetActive(false);

            // Bảng xoay bộ phận lắp ráp (Xoay -15° / Xoay +15°)
            var fabBox = Panel(safeAreaRect, "FabPanel", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 224), new Vector2(210, 56), glassMat);
            fabPanel = fabBox.gameObject;
            var fp = fabBox.transform;
            MakeTapTextButton(fp, "Xoay trái", "⟲ -15°", new Vector2(8, 6), new Vector2(92, 44), () =>
            {
                PadInput.HeldYawStep = -1f;
            });
            MakeTapTextButton(fp, "Xoay phải", "⟳ +15°", new Vector2(108, 6), new Vector2(92, 44), () =>
            {
                PadInput.HeldYawStep = 1f;
            });
            fabPanel.SetActive(false);

            // Nút Ghi số đọc (Enter)
            recordBtn = MakeHudButton("Nút Ghi số", "Ghi số (Enter)", new Vector2(1, 0), new Vector2(-40, 224), new Vector2(130, 48), () =>
            {
                PadInput.TouchRecordPressed = true;
            });
            recordBtn.SetActive(false);
        }

        void BuildTopBar()
        {
            // Nút Menu góc trên phải
            var menuBtn = MakeHudButton("Nút Menu", "Menu ☰", new Vector2(1, 1), new Vector2(-40, -40), new Vector2(96, 44), () =>
            {
                menuPanel.SetActive(!menuPanel.activeSelf);
            });

            // Nút Trợ giúp (H)
            helpBtn = MakeHudButton("Nút Trợ giúp", "Trợ giúp (H)", new Vector2(1, 1), new Vector2(-146, -40), new Vector2(110, 44), () =>
            {
                PadInput.TouchHelpPressed = true;
            });

            // Cụm đổi trạm: ◀ Trạm (danh sách) ▶
            var stBox = Panel(safeAreaRect, "StationBar", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(260, 48), glassMat);
            var sp = stBox.transform;
            MakeTapTextButton(sp, "Trạm trước", "◀", new Vector2(6, 4), new Vector2(46, 40), () =>
            {
                PadInput.TouchStationStep = -1;
            });
            MakeTapTextButton(sp, "Chọn trạm", "Đổi khu thí nghiệm", new Vector2(58, 4), new Vector2(144, 40), () =>
            {
                stationPanel.SetActive(!stationPanel.activeSelf);
            });
            MakeTapTextButton(sp, "Trạm sau", "▶", new Vector2(208, 4), new Vector2(46, 40), () =>
            {
                PadInput.TouchStationStep = 1;
            });
        }

        void BuildMenuPanel()
        {
            var menuBox = Panel(safeAreaRect, "MenuPanel", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -96), new Vector2(280, 260), glassMat, 1.2f);
            menuPanel = menuBox.gameObject;
            var mp = menuBox.transform;

            Label(mp, "Title", "TÙY CHỌN NHANH", 16, Accent, true, TextAnchor.UpperLeft, new Vector2(16, -14), new Vector2(240, 22));

            MakeTapTextButton(mp, "Đồ thị", "Đồ thị pH (G)", new Vector2(16, -44), new Vector2(248, 40), () =>
            {
                PadInput.ToggleGraphPressed = true;
                menuPanel.SetActive(false);
            });

            MakeTapTextButton(mp, "Lần mới", "Làm lượt chuẩn độ mới (R)", new Vector2(16, -90), new Vector2(248, 40), () =>
            {
                PadInput.NewRunPressed = true;
                menuPanel.SetActive(false);
            });

            MakeTapTextButton(mp, "Đổi chủ đề", "Đổi chủ đề phòng lab (F7)", new Vector2(16, -136), new Vector2(248, 40), () =>
            {
                PadInput.NextThemePressed = true;
            });

            sensText = Label(mp, "SensLabel", "Độ nhạy vuốt nhìn: 0.18", 13, Dim, false, TextAnchor.UpperLeft, new Vector2(16, -186), new Vector2(248, 18));
            var sRt = Rect(mp, "Slider", new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -210), new Vector2(248, 24));
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
            var box = Panel(safeAreaRect, "StationPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520, 360), glassMat, 1.2f);
            stationPanel = box.gameObject;
            var bp = box.transform;

            Label(bp, "Title", "CHỌN KHU THÍ NGHIỆM", 18, Accent, true, TextAnchor.UpperLeft, new Vector2(24, -18), new Vector2(300, 26));
            MakeTapTextButton(bp, "Đóng", "✕", new Vector2(464, -14), new Vector2(36, 34), () => stationPanel.SetActive(false));

            string[] stNames = {
                "1. Bàn chuẩn độ 1", "2. Bàn chuẩn độ 2", "3. Bàn chuẩn độ 3", "4. Bàn chuẩn độ 4",
                "5. Bàn chuẩn độ 5", "6. Bàn chuẩn độ 6", "7. Tủ hút hóa chất", "8. Phòng hóa chất",
                "9. Máy phân tích", "0. Bàn giáo viên & Khu chế tạo", "Tầng 2: Kính hiển vi", "Tầng 2: Tủ cấy & Tủ ấm"
            };

            for (int i = 0; i < stNames.Length; i++)
            {
                int idx = i;
                float x = 24 + (i % 2) * 240;
                float y = -58 - (i / 2) * 46;
                MakeTapTextButton(bp, "Trạm_" + i, stNames[i], new Vector2(x, y), new Vector2(230, 40), () =>
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
        }
    }

    /// <summary>
    /// Kích hoạt sự kiện giữ và thả nút cảm ứng (cho nút tay chính, nút lắc, nút vặn buret).
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