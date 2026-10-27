using System.Collections.Generic;
using Khkt.Interaction;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using static Khkt.Demo.GlassKit;

namespace Khkt.Demo
{
    /// <summary>
    /// Giao diện chế độ máy tính theo phong cách kính mờ, ít chữ:
    /// thanh các bước (trên), thông báo ngắn, card dụng cụ nổi cạnh vật đang trỏ, thanh gợi ý thao tác
    /// dạng phím (dưới), kính lúp buret, số liệu, kết quả và đồ thị pH.
    /// </summary>
    public class ModernUI : MonoBehaviour
    {
        public Camera viewCamera;
        public Shader glassShader;
        public HandController hand;
        public LabProcedure procedure;
        public MeniscusReading reading;
        public LoupeCamera loupe;

        public Canvas Canvas { get; private set; }
        public bool StepperVisible => false;

        RectTransform root;
        Material glassMat, chipMat;

        // Thanh bước
        readonly List<(Image dot, Text num, Text label)> steps = new List<(Image, Text, Text)>();
        // Thông báo
        CanvasGroup toastGroup;
        RectTransform toastRect;
        Text toastText;
        Image toastDot;
        // Thanh gợi ý
        RectTransform hintBar;
        readonly List<(RectTransform chip, Text key, Text label)> hintItems = new List<(RectTransform, Text, Text)>();
        // Card dụng cụ
        RectTransform card;
        CanvasGroup cardGroup;
        Text cardTitle, cardStatus, cardBody;
        Transform cardTarget;
        Bounds cardBounds;
        // Kính lúp, số liệu, kết quả, đồ thị, trợ giúp
        GameObject loupePanel, dataPanel, resultPanel, graphPanel, helpPanel, stepBar;
        Image crosshair;
        RawImage loupeImage, graphImage;
        Text resultTitle, eyeText, flowText, dataLeft, dataRight, resultBig, resultSub, resultBadge, resultNotes;
        Image resultBadgeBg, flowBar, swirlBar;
        Texture2D graphTex;
        int drawnCurve = -1;
        bool drawnResult;

        const int GraphW = 460, GraphH = 170;

        void Awake()
        {
            if (glassShader == null) glassShader = Shader.Find("Khkt/KinhMo");
            glassMat = Glass(glassShader);
            chipMat = Glass(glassShader, light: true);

            // Camera cần ảnh cảnh (Opaque Texture) để làm mờ phía sau card
            if (viewCamera != null)
                viewCamera.GetUniversalAdditionalCameraData().requiresColorOption = CameraOverrideOption.On;

            var cgo = new GameObject("Giao diện", typeof(Canvas), typeof(CanvasScaler));
            cgo.transform.SetParent(transform, false);
            Canvas = cgo.GetComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceCamera;
            Canvas.worldCamera = viewCamera;
            Canvas.planeDistance = 0.03f;  // sát mặt cắt gần của camera để tay ảo không che giao diện
            var scaler = cgo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            root = (RectTransform)cgo.transform;

            BuildStepper();
            BuildToast();
            BuildHintBar();
            BuildCard();
            BuildLoupe();
            BuildData();
            BuildResult();
            BuildGraph();
            BuildHelp();
            var ch = Rect(root, "Tâm ngắm", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(8, 8));
            crosshair = ch.gameObject.AddComponent<Image>();
            crosshair.sprite = SolidCircle;
            crosshair.color = new Color(1, 1, 1, 0.85f);
            crosshair.raycastTarget = false;
        }

        // =====================================================================
        // Dựng
        // =====================================================================

        void BuildStepper()
        {
            const float itemW = 150f;
            int n = 7;
            stepBar = Panel(root, "Thanh bước", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -18),
                            new Vector2(itemW * n + 24, 52), glassMat, 1.4f).gameObject;
            var bar = stepBar.GetComponent<Image>();
            for (int i = 0; i < n; i++)
            {
                float x = 12 + i * itemW;
                var dotRt = Rect(bar.transform, "Chấm", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(x + 8, 0), new Vector2(28, 28));
                var dot = dotRt.gameObject.AddComponent<Image>();
                dot.sprite = SolidCircle;
                dot.raycastTarget = false;
                var num = Label(dotRt, "Số", (i + 1).ToString(), 15, Ink, true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(28, 28));
                var lab = Label(bar.transform, "Tên", "", 16, Dim, false, TextAnchor.MiddleLeft,
                                new Vector2(x + 42, 13), new Vector2(itemW - 40, 26), new Vector2(0, 0.5f));
                steps.Add((dot, num, lab));
            }
        }

        void BuildToast()
        {
            var img = Panel(root, "Thông báo", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -82), new Vector2(500, 44), glassMat, 2f);
            toastRect = img.rectTransform;
            toastGroup = img.gameObject.AddComponent<CanvasGroup>();
            var dotRt = Rect(toastRect, "Chấm", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(10, 10));
            toastDot = dotRt.gameObject.AddComponent<Image>();
            toastDot.sprite = SolidCircle;
            toastText = Label(toastRect, "Chữ", "", 17, Ink, false, TextAnchor.MiddleLeft, new Vector2(38, 14), new Vector2(900, 28), new Vector2(0, 0.5f));
            toastText.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        void BuildHintBar()
        {
            var img = Panel(root, "Gợi ý thao tác", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(400, 54), glassMat, 1.6f);
            hintBar = img.rectTransform;
            for (int i = 0; i < 5; i++)  // tay cầm 1 cần ở chế độ ĐI cần 5 gợi ý
            {
                var chip = Rect(hintBar, "Phím", new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(60, 32));
                var chipImg = chip.gameObject.AddComponent<Image>();
                chipImg.sprite = Solid;
                chipImg.type = Image.Type.Sliced;
                chipImg.pixelsPerUnitMultiplier = 3f;
                chipImg.color = new Color(Accent.r, Accent.g, Accent.b, 0.92f);
                chipImg.raycastTarget = false;
                var key = Label(chip, "Chữ phím", "", 15, new Color(0.03f, 0.07f, 0.12f), true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(60, 32));
                key.horizontalOverflow = HorizontalWrapMode.Overflow;
                var lab = Label(hintBar, "Việc", "", 17, Ink, false, TextAnchor.MiddleLeft, Vector2.zero, new Vector2(300, 30), new Vector2(0, 0.5f));
                lab.horizontalOverflow = HorizontalWrapMode.Overflow;
                hintItems.Add((chip, key, lab));
            }
        }

        void BuildCard()
        {
            var img = Panel(root, "Card dụng cụ", new Vector2(0, 0), new Vector2(0, 0.5f), Vector2.zero, new Vector2(340, 130), glassMat, 1.3f);
            card = img.rectTransform;
            cardGroup = img.gameObject.AddComponent<CanvasGroup>();
            cardGroup.alpha = 0;
            var accent = Rect(card, "Vạch nhấn", new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -20), new Vector2(4, 24));
            var ai = accent.gameObject.AddComponent<Image>();
            ai.sprite = Solid;
            ai.type = Image.Type.Sliced;
            ai.pixelsPerUnitMultiplier = 20f;
            ai.color = Accent;
            cardTitle = Label(card, "Tên", "", 21, Ink, true, TextAnchor.UpperLeft, new Vector2(32, -16), new Vector2(296, 30));
            cardStatus = Label(card, "Trạng thái", "", 15, Accent, false, TextAnchor.UpperLeft, new Vector2(32, -46), new Vector2(296, 22));
            cardBody = Label(card, "Mô tả", "", 15, Dim, false, TextAnchor.UpperLeft, new Vector2(32, -72), new Vector2(296, 50));
        }

        void BuildLoupe()
        {
            var img = Panel(root, "Kính lúp", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-20, 30), new Vector2(300, 420), glassMat, 1.3f);
            loupePanel = img.gameObject;
            Label(img.transform, "Tiêu đề", "Kính lúp · vạch buret", 16, Dim, false, TextAnchor.UpperLeft, new Vector2(20, -14), new Vector2(260, 22));
            var maskRt = Rect(img.transform, "Khung ảnh", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -44), new Vector2(260, 260));
            var maskImg = maskRt.gameObject.AddComponent<Image>();
            maskImg.sprite = Solid;
            maskImg.type = Image.Type.Sliced;
            maskImg.pixelsPerUnitMultiplier = 1.3f;
            maskRt.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var imRt = Rect(maskRt, "Ảnh", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260, 260));
            loupeImage = imRt.gameObject.AddComponent<RawImage>();
            loupeImage.raycastTarget = false;
            eyeText = Label(img.transform, "Mắt", "", 16, Ink, true, TextAnchor.UpperLeft, new Vector2(20, -314), new Vector2(260, 24));
            Label(img.transform, "Khóa", "Khóa", 15, Dim, false, TextAnchor.MiddleLeft, new Vector2(20, -350), new Vector2(60, 22));
            flowBar = Bar(img.transform, new Vector2(70, -361), out _);
            flowText = Label(img.transform, "Dòng", "", 14, Dim, false, TextAnchor.MiddleRight, new Vector2(170, -350), new Vector2(110, 22));
            Label(img.transform, "Lắc", "Lắc", 15, Dim, false, TextAnchor.MiddleLeft, new Vector2(20, -384), new Vector2(60, 22));
            swirlBar = Bar(img.transform, new Vector2(70, -395), out _);
        }

        Image Bar(Transform parent, Vector2 pos, out Image bg)
        {
            var bgRt = Rect(parent, "Nền thanh", new Vector2(0, 1), new Vector2(0, 0.5f), pos, new Vector2(110, 6));
            bg = bgRt.gameObject.AddComponent<Image>();
            bg.sprite = Solid;
            bg.type = Image.Type.Sliced;
            bg.pixelsPerUnitMultiplier = 10f;
            bg.color = new Color(1, 1, 1, 0.15f);
            var fillRt = Rect(bgRt, "Mức", new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(0, 6));
            var fill = fillRt.gameObject.AddComponent<Image>();
            fill.sprite = Solid;
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = 10f;
            fill.color = Accent;
            return fill;
        }

        void BuildData()
        {
            var img = Panel(root, "Số liệu", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-20, 24), new Vector2(300, 150), glassMat, 1.3f);
            dataPanel = img.gameObject;
            Label(img.transform, "Tiêu đề", "Số liệu", 16, Dim, false, TextAnchor.UpperLeft, new Vector2(20, -14), new Vector2(200, 22));
            dataLeft = Label(img.transform, "Nhãn", "V đầu\nV cuối\nV NaOH\nC HCl", 16, Dim, false, TextAnchor.UpperLeft, new Vector2(20, -42), new Vector2(120, 100));
            dataRight = Label(img.transform, "Giá trị", "", 16, Ink, true, TextAnchor.UpperRight, new Vector2(140, -42), new Vector2(140, 100));
            dataLeft.lineSpacing = dataRight.lineSpacing = 1.15f;
        }

        void BuildResult()
        {
            var img = Panel(root, "Kết quả", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(520, 470), glassMat, 1.2f);
            resultPanel = img.gameObject;
            resultTitle = Label(img.transform, "Tiêu đề", "Kết quả", 16, Dim, false, TextAnchor.UpperLeft, new Vector2(28, -20), new Vector2(300, 24));
            resultBig = Label(img.transform, "Số lớn", "", 44, Ink, true, TextAnchor.UpperLeft, new Vector2(26, -44), new Vector2(300, 56));
            resultSub = Label(img.transform, "Thật", "", 16, Dim, false, TextAnchor.UpperLeft, new Vector2(28, -102), new Vector2(300, 22));
            var badge = Rect(img.transform, "Nhãn sai số", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-28, -56), new Vector2(150, 36));
            resultBadgeBg = badge.gameObject.AddComponent<Image>();
            resultBadgeBg.sprite = Solid;
            resultBadgeBg.type = Image.Type.Sliced;
            resultBadgeBg.pixelsPerUnitMultiplier = 2f;
            resultBadge = Label(badge, "Chữ", "", 17, new Color(0.05f, 0.08f, 0.12f), true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(150, 36));
            var gRt = Rect(img.transform, "Đồ thị", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -136), new Vector2(GraphW, GraphH));
            var gImg = gRt.gameObject.AddComponent<RawImage>();
            resultNotes = Label(img.transform, "Ghi chú", "", 15, Dim, false, TextAnchor.UpperLeft, new Vector2(28, -318), new Vector2(464, 120));
            Label(img.transform, "Làm lại", "R  làm lượt mới", 14, Dim, false, TextAnchor.LowerRight, new Vector2(300, -440), new Vector2(194, 22));
            graphTex = new Texture2D(GraphW, GraphH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            gImg.texture = graphTex;
            resultPanel.SetActive(false);
        }

        void BuildGraph()
        {
            var img = Panel(root, "Đồ thị pH", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, -90), new Vector2(GraphW + 40, GraphH + 60), glassMat, 1.3f);
            graphPanel = img.gameObject;
            Label(img.transform, "Tiêu đề", "pH theo NaOH đã nhỏ (ml)", 15, Dim, false, TextAnchor.UpperLeft, new Vector2(20, -12), new Vector2(400, 22));
            var gRt = Rect(img.transform, "Ảnh", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(GraphW, GraphH));
            graphImage = gRt.gameObject.AddComponent<RawImage>();
            graphImage.texture = graphTex;
            graphPanel.SetActive(false);
        }

        void BuildHelp()
        {
            var chip = Panel(root, "Nút trợ giúp", new Vector2(0, 0), new Vector2(0, 0), new Vector2(20, 24), new Vector2(150, 40), glassMat, 2f);
            Label(chip.transform, "Chữ", "<b>H</b>   Trợ giúp", 16, Dim, false, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(150, 40));
            var img = Panel(root, "Trợ giúp", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(20, 0), new Vector2(820, 650), glassMat, 1.3f);
            helpPanel = img.gameObject;
            Label(img.transform, "Tiêu đề", "Điều khiển", 20, Ink, true, TextAnchor.UpperLeft, new Vector2(24, -18), new Vector2(360, 30));
            var rows = new (string k, string v)[]
            {
                ("Rê chuột", "Nhìn quanh"), ("W A S D", "Đi lại"), ("C", "Ngồi / đứng"), ("Z  X", "Hạ / nâng tầm mắt"),
                ("← →  PgUp PgDn", "Xoay, ngẩng bằng phím"), ("1 – 9   0", "Đến các khu"), ("Chuột trái", "Cầm, đặt, dùng"), ("Giữ chuột trái", "Rót, hút, xả, lắc"),
                ("Q", "Trả dụng cụ về chỗ"), ("Lăn chuột", "Vặn khóa buret"), ("↑ ↓   Space", "Mở, đóng, khóa hẳn"),
                ("F", "Lắc bình nón"), ("Enter", "Ghi số đọc buret"), ("G", "Đồ thị pH"), ("R", "Làm lượt chuẩn độ mới"),
                ("Tab   Alt", "Nhìn theo chuột · thả con trỏ"), ("B", "Tra cứu hóa chất"), ("T", "Trợ lý AI"), ("Esc", "Menu, chọn bài"),
                ("F8   F9", "Điều khiển bằng đầu qua webcam · căn lại"),
            };
            for (int i = 0; i < rows.Length; i++)
            {
                float y = -62 - i * 28.5f;
                var c = Panel(img.transform, "Phím", new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, y), new Vector2(150, 24), chipMat, 4f);
                Label(c.transform, "Chữ", rows[i].k, 14, Ink, true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(150, 24));
                Label(img.transform, "Việc", rows[i].v, 15, Dim, false, TextAnchor.MiddleLeft, new Vector2(190, y), new Vector2(210, 24));
            }

            // Cột phải: tay cầm ESP32-CAM 1 cần (docs/09-tay-cam-esp32-cam.md) và tay cầm chơi game Xbox / PlayStation
            Label(img.transform, "Tiêu đề tay cầm", "Tay cầm joystick  ·  Xbox", 20, Ink, true, TextAnchor.UpperLeft, new Vector2(424, -18), new Vector2(370, 30));
            var padRows = new (string k, string v)[]
            {
                ("ĐI: cần ↑ ↓", "Tiến, lùi (đẩy mạnh: nhanh)"), ("ĐI: cần ← →", "Quay mặt trái, phải"),
                ("ĐI: cần chéo", "Vừa đi vừa rẽ"), ("ĐI: ↑ hết cỡ 1 s", "Chạy nhanh"),
                ("ĐI: giữ nút + ← →", "Bước ngang"), ("ĐI: giữ nút + ↑ ↓", "Ngẩng, cúi nhìn"),
                ("ĐI: nhấn 1 lần", "Sang chế độ TAY"), ("ĐI: nhấn 2 lần", "Ngồi / đứng"), ("ĐI: giữ nút 1 s", "Sang khu kế tiếp"),
                ("TAY: cần", "Ngắm · trên khóa buret: vặn"), ("TAY: nhấn / giữ", "Cầm, đặt, dùng / rót, hút"),
                ("TAY: nhấn chỗ trống", "Về chế độ ĐI"), ("Xbox LS RS A B", "Đi · nhìn · cầm · trả"),
            };
            for (int i = 0; i < padRows.Length; i++)
            {
                float y = -62 - i * 28.5f;
                var c = Panel(img.transform, "Nút", new Vector2(0, 1), new Vector2(0, 1), new Vector2(424, y), new Vector2(150, 24), chipMat, 4f);
                Label(c.transform, "Chữ", padRows[i].k, 14, Ink, true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(150, 24));
                Label(img.transform, "Việc", padRows[i].v, 15, Dim, false, TextAnchor.MiddleLeft, new Vector2(590, y), new Vector2(220, 24));
            }
            padStatus = Label(img.transform, "Trạng thái tay cầm", "", 14, Dim, false, TextAnchor.UpperLeft,
                              new Vector2(424, -62 - padRows.Length * 28.5f - 8), new Vector2(380, 120));
            helpPanel.SetActive(false);
        }

        Text padStatus;

        /// <summary>Trạng thái các tay cầm trong bảng trợ giúp (cổng COM, Wi-Fi, tay cầm chơi game): để biết vì sao chưa nối được.</summary>
        void UpdatePadStatus()
        {
            if (padStatus == null || !helpPanel.activeSelf) return;
            var lines = new System.Collections.Generic.List<string>();
            foreach (var src in Khkt.Input.PadInput.Sources)
                if (src != null && src.isActiveAndEnabled) lines.Add(src.StatusText);
            if (Khkt.Input.PadInput.ControllerConnected && Khkt.Input.PadInput.SingleStick)
                lines.Add($"Tay cầm 1 cần, chế độ {(Khkt.Input.PadInput.Mode == Khkt.Input.PadInput.StickMode.Walk ? "ĐI" : "TAY")}");
            if (UnityEngine.InputSystem.Gamepad.current != null) lines.Add($"Tay cầm chơi game: {UnityEngine.InputSystem.Gamepad.current.displayName}");
            padStatus.text = lines.Count > 0 ? string.Join("\n", lines) : "Chưa bật nguồn tay cầm nào";
        }

        public void ToggleHelp() => helpPanel.SetActive(!helpPanel.activeSelf);
        public void ToggleGraph() => graphPanel.SetActive(!graphPanel.activeSelf);

        // =====================================================================
        // Cập nhật
        // =====================================================================

        void LateUpdate()
        {
            crosshair.enabled = (Khkt.Environment.DesktopViewer.MouseLook || Khkt.Input.PadInput.AimFromCenter) &&
                                Khkt.Environment.DesktopViewer.UiCapture == 0;
            UpdatePadStatus();
            crosshair.color = hand != null && hand.Current != HandController.Act.None ? Accent : new Color(1, 1, 1, 0.85f);
            UpdateToast();
            UpdateHints();
            UpdateCard();
            if (procedure == null) return;
            if (stepBar.activeSelf != procedure.Active) stepBar.SetActive(procedure.Active);
            if (!procedure.Active)
            {
                if (loupePanel.activeSelf) loupePanel.SetActive(false);
                if (dataPanel.activeSelf) dataPanel.SetActive(false);
                if (resultPanel.activeSelf) resultPanel.SetActive(false);
                if (graphPanel.activeSelf) graphPanel.SetActive(false);
                return;
            }
            UpdateStepper();

            bool near = reading != null && reading.eye != null && Vector3.Distance(reading.eye.position, reading.MeniscusPosition) < 2.5f;
            UpdateLoupe(near);
            if (dataPanel.activeSelf != near) dataPanel.SetActive(near);
            UpdateData();
            bool showResult = near && procedure.LastResult.HasValue;
            if (resultPanel.activeSelf != showResult) resultPanel.SetActive(showResult);
            if (showResult) UpdateResult(procedure.LastResult.Value);
            if (!near && graphPanel.activeSelf) graphPanel.SetActive(false);
            if ((graphPanel.activeSelf || showResult) && (drawnCurve != procedure.CurveVersion || drawnResult != procedure.LastResult.HasValue))
                DrawGraph();
        }

        void UpdateStepper()
        {
            for (int i = 0; i < steps.Count; i++)
            {
                bool done = procedure.Done[i];
                bool current = (int)procedure.Current == i;
                var (dot, num, lab) = steps[i];
                lab.text = procedure.StepNames[i];
                dot.color = done ? new Color(Good.r, Good.g, Good.b, 0.9f) : current ? Accent : new Color(1, 1, 1, 0.14f);
                num.text = done ? "✓" : (i + 1).ToString();
                num.color = done || current ? new Color(0.04f, 0.07f, 0.12f) : Dim;
                lab.color = current ? Ink : done ? new Color(Good.r, Good.g, Good.b, 0.85f) : Dim;
                lab.font = current ? Semibold : Regular;
            }
        }

        void UpdateToast()
        {
            float age = Time.time - LabMessages.MessageTime;
            float a = Mathf.Clamp01(Mathf.Min(age * 6f, 1.6f - age / 4f));
            toastGroup.alpha = a;
            if (a <= 0) return;
            toastText.text = LabMessages.Message;
            float w = Width(toastText, LabMessages.Message);
            toastRect.sizeDelta = new Vector2(w + 58, 44);
            toastDot.color = LabMessages.MessageTone == LabMessages.Tone.Warn ? Warn
                           : LabMessages.MessageTone == LabMessages.Tone.Good ? Good : Accent;
        }

        void UpdateHints()
        {
            int n = hand != null ? Mathf.Min(hand.Hints.Count, hintItems.Count) : 0;
            hintBar.gameObject.SetActive(n > 0);
            if (n == 0) return;
            float x = 14;
            for (int i = 0; i < hintItems.Count; i++)
            {
                var (chip, key, label) = hintItems[i];
                bool on = i < n;
                chip.gameObject.SetActive(on);
                label.gameObject.SetActive(on);
                if (!on) continue;
                key.text = hand.Hints[i].key;
                label.text = hand.Hints[i].text;
                float kw = Width(key, key.text) + 24;
                chip.sizeDelta = new Vector2(kw, 32);
                key.rectTransform.sizeDelta = new Vector2(kw, 32);
                chip.anchoredPosition = new Vector2(x, 0);
                x += kw + 10;
                float lw = Width(label, label.text);
                label.rectTransform.anchoredPosition = new Vector2(x, 15);
                label.rectTransform.sizeDelta = new Vector2(lw + 4, 30);
                x += lw + 26;
            }
            hintBar.sizeDelta = new Vector2(x - 12, 54);
            hintBar.GetComponent<Image>().color = hand.Acting ? new Color(0.8f, 0.95f, 1f, 1f) : Color.white;
        }

        void UpdateCard()
        {
            // Tay rỗng: card của vật đang trỏ. Đang cầm pipet: card của pipet (mực so với vạch).
            // Đang cầm đồ khác và trỏ vào vật chứa: card của vật chứa đó (đang chứa gì, bao nhiêu).
            Transform target = null;
            if (hand != null)
            {
                if (hand.Held == null) target = hand.HoverRoot;
                else if (hand.Held.GetComponent<Pipette>() != null) target = hand.Held.transform;
                else if (hand.TargetContainer != null) target = hand.TargetContainer.transform;
            }
            var info = target != null ? target.GetComponentInParent<EquipmentInfo>() : null;
            if (target != null && info == null) info = target.GetComponentInChildren<EquipmentInfo>();
            var chem = target != null ? target.GetComponentInParent<ChemicalBottle>() : null;
            bool show = target != null && (info != null || (chem != null && chem.Entry != null)) && !resultPanel.activeSelf;
            cardGroup.alpha = Mathf.MoveTowards(cardGroup.alpha, show ? 1f : 0f, Time.unscaledDeltaTime * 8f);
            if (!show) { cardTarget = null; return; }

            if (target != cardTarget)
            {
                cardTarget = target;
                var rs = target.GetComponentsInChildren<Renderer>();
                cardBounds = rs.Length > 0 ? rs[0].bounds : new Bounds(target.position, Vector3.one * 0.1f);
                foreach (var r in rs) if (!(r is ParticleSystemRenderer)) cardBounds.Encapsulate(r.bounds);
            }
            if (chem != null && chem.Entry != null)
            {
                var e = chem.Entry;
                cardTitle.text = e.ten + (string.IsNullOrEmpty(e.cong_thuc) ? "" : "  " + e.Formula);
                string hz = Khkt.Simulation.ChemicalCatalog.HazardText(e);
                cardStatus.text = Khkt.Simulation.ChemicalCatalog.StateText(e) + " · " + Khkt.Simulation.ChemicalCatalog.ConcText(e) + (hz.Length > 0 ? "  <color=#FFC759>⚠ " + hz + "</color>" : "");
                cardBody.text = Khkt.Simulation.ChemicalCatalog.Instance != null ? Khkt.Simulation.ChemicalCatalog.Instance.LocationText(e) : e.ma_vi_tri;
            }
            else
            {
                cardTitle.text = info.title;
                cardStatus.text = Status(target);
                cardBody.text = FirstSentence(info.description);
            }
            cardStatus.gameObject.SetActive(cardStatus.text.Length > 0);
            // Xếp dòng theo chiều cao thật của chữ (tên dài xuống 2 dòng không đè lên dòng dưới)
            float y = -16;
            float titleH = Mathf.Max(28, cardTitle.preferredHeight);
            cardTitle.rectTransform.sizeDelta = new Vector2(296, titleH);
            y -= titleH + 2;
            if (cardStatus.text.Length > 0)
            {
                cardStatus.rectTransform.anchoredPosition = new Vector2(32, y);
                float sh = Mathf.Max(20, cardStatus.preferredHeight);
                cardStatus.rectTransform.sizeDelta = new Vector2(296, sh);
                y -= sh + 4;
            }
            cardBody.rectTransform.anchoredPosition = new Vector2(32, y);
            float bodyH = cardBody.preferredHeight;
            card.sizeDelta = new Vector2(340, -y + bodyH + 18);

            // Đặt card bên phải vật, ngang phần trên của vật
            Vector3 anchorWorld = cardBounds.center + Vector3.up * cardBounds.extents.y * 0.6f + viewCamera.transform.right * cardBounds.extents.magnitude * 0.6f;
            Vector3 sp = viewCamera.WorldToScreenPoint(anchorWorld);
            if (sp.z < 0) { cardGroup.alpha = 0; return; }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, sp, viewCamera, out Vector2 local);
            Vector2 half = root.rect.size / 2;
            Vector2 pos = local + half + new Vector2(28, 0);
            pos.x = Mathf.Clamp(pos.x, 20, root.rect.width - card.sizeDelta.x - 20);
            pos.y = Mathf.Clamp(pos.y, card.sizeDelta.y / 2 + 90, root.rect.height - card.sizeDelta.y / 2 - 90);
            card.anchoredPosition = Vector2.Lerp(card.anchoredPosition, pos, cardGroup.alpha < 0.05f ? 1f : 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
        }

        static string FirstSentence(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            int i = s.IndexOf(". ");
            return i > 0 ? s.Substring(0, i + 1) : s;
        }

        string Status(Transform t)
        {
            var p = t.GetComponentInParent<Pipette>();
            if (p != null)
            {
                if (p.IsEmpty) return "Trống · vạch định mức 25,00 ml";
                float mm = p.OffsetFromMarkMm;
                string where = Mathf.Abs(mm) < 0.6f ? "đúng vạch" : mm > 0 ? $"trên vạch {LabProcedure.F(mm, "0")} mm" : $"dưới vạch {LabProcedure.F(-mm, "0")} mm";
                return $"{LabProcedure.F(p.VolumeMl, "0.0")} ml · {where}";
            }
            var b = t.GetComponentInParent<BuretStopcock>();
            if (b != null)
            {
                if (!b.mounted) return "Chưa lắp lên giá";
                if (!b.HasLiquid) return "Đã lắp · trống";
                return b.Opening > 0.001f ? $"Khóa mở {b.Opening * 100f:0} %" : "Khóa đóng";
            }
            var mp = t.GetComponentInParent<MeterProbe>();
            if (mp != null) return string.IsNullOrEmpty(mp.LastReading) ? "Nhúng đầu đo vào dung dịch" : mp.LastReading;
            var tp = t.GetComponentInParent<TestPaper>();
            if (tp != null) return string.IsNullOrEmpty(tp.LastResult) ? "Giấy mới" : tp.LastResult;
            var bs = t.GetComponentInParent<BalanceScale>();
            if (bs != null) return LabProcedure.F(bs.Grams, "0.00") + " g trên đĩa";
            var c = t.GetComponentInParent<LabContainer>();
            if (c != null) return c.Total.Describe() + (c.TemperatureC > 32f ? $" · {c.TemperatureC:0} °C" : "");
            var tg = t.GetComponentInParent<ToggleEffect>();
            if (tg != null) return tg.isOn ? "Đang bật" : "Đang tắt";
            return "";
        }

        void UpdateLoupe(bool near)
        {
            var b = procedure.buret;
            bool on = near && loupe != null && loupe.Active && b.mounted && b.HasLiquid;
            if (loupePanel.activeSelf != on) loupePanel.SetActive(on);
            if (!on) return;
            loupeImage.texture = loupe.Texture;
            float cm = reading.EyeAboveMeniscusM * 100f;
            eyeText.text = Mathf.Abs(cm) < 1f ? "<color=#73F29E>●</color>  Mắt ngang vạch"
                         : cm > 0 ? $"<color=#FFC759>●</color>  Mắt cao {LabProcedure.F(cm, "0")} cm · nhấn Z"
                         : $"<color=#FFC759>●</color>  Mắt thấp {LabProcedure.F(-cm, "0")} cm · nhấn X";
            SetBar(flowBar, b.Opening);
            flowText.text = b.Opening <= 0.001f ? "đóng" : b.IsStream ? "chảy dòng" : $"{LabProcedure.F(b.FlowMlPerS / b.dropMl, "0.0")} giọt/s";
            SetBar(swirlBar, procedure.swirl != null ? procedure.swirl.Intensity : 0f);
        }

        static void SetBar(Image fill, float v)
        {
            var rt = fill.rectTransform;
            rt.sizeDelta = new Vector2(110f * Mathf.Clamp01(v), rt.sizeDelta.y);
            fill.enabled = v > 0.005f;
        }

        void UpdateData()
        {
            string Ml(float? v) => v.HasValue ? LabProcedure.F(v.Value, "0.00") + " ml" : "—";
            var r = procedure.LastResult;
            dataLeft.text = $"V đầu\nV cuối\nV {procedure.Cfg.titrantShort}\nC {procedure.Cfg.analyteShort}";
            dataRight.text = $"{Ml(procedure.InitialRead)}\n{Ml(procedure.FinalRead)}\n" +
                             (r.HasValue ? Ml(r.Value.volumeRead) : "—") + "\n" +
                             (r.HasValue ? LabProcedure.F(r.Value.concentrationRead, "0.0000") + " M" : "—");
        }

        void UpdateResult(LabProcedure.Result r)
        {
            resultTitle.text = $"Kết quả · C({procedure.Cfg.analyteShort})";
            resultBig.text = LabProcedure.F(r.concentrationRead, "0.0000") + " M";
            resultSub.text = $"Thật: {LabProcedure.F(r.concentrationTrue, "0.0000")} M   ·   V NaOH {LabProcedure.F(r.volumeRead, "0.00")} ml";
            float e = Mathf.Abs(r.errorPercent);
            resultBadge.text = $"Sai số {LabProcedure.F(r.errorPercent, "+0.0;-0.0;0.0")} %";
            resultBadgeBg.color = e <= 1f ? Good : e <= 3f ? Warn : Bad;
            var notes = new List<string>();
            if (r.noIndicator) notes.Add($"Quên {procedure.Cfg.indicatorName}: không thấy được điểm cuối");
            if (r.contaminated) notes.Add($"{procedure.Cfg.titrantShort} rơi vào bình nón trước khi đọc V đầu");
            if (Mathf.Abs(r.acidActualMl - 25f) >= 0.05f) notes.Add($"Pipet lấy {LabProcedure.F(r.acidActualMl, "0.00")} ml thay vì 25,00 ml");
            if (r.overshootDrops >= 1.5f) notes.Add($"Nhỏ quá điểm tương đương khoảng {LabProcedure.F(r.overshootDrops, "0")} giọt");
            if (Mathf.Abs(r.parallaxInitial) >= 0.05f || Mathf.Abs(r.parallaxFinal) >= 0.05f)
                notes.Add($"Đọc lệch mắt: V đầu {LabProcedure.F(r.parallaxInitial, "+0.00;-0.00;0")} · V cuối {LabProcedure.F(r.parallaxFinal, "+0.00;-0.00;0")} ml");
            if (r.notMixedWell) notes.Add("Lắc bình chưa đều khi nhỏ");
            if (notes.Count == 0) notes.Add("Thao tác chuẩn: đúng vạch, đủ chỉ thị, dừng đúng giọt");
            resultNotes.text = "•  " + string.Join("\n•  ", notes.GetRange(0, Mathf.Min(4, notes.Count)));
        }

        void DrawGraph()
        {
            drawnCurve = procedure.CurveVersion;
            drawnResult = procedure.LastResult.HasValue;
            var px = new Color32[GraphW * GraphH];
            var bg = new Color32(10, 16, 28, 150);
            var grid = new Color32(255, 255, 255, 28);
            for (int i = 0; i < px.Length; i++) px[i] = bg;
            float maxV = 50f;
            foreach (var p in procedure.Curve) maxV = Mathf.Max(maxV, p.x + 2);
            for (int v = 0; v <= maxV; v += 10) VLine(px, X(v, maxV), grid, false);
            HLine(px, Y(7), grid);
            if (procedure.LastResult.HasValue)
            {
                VLine(px, X(procedure.EquivalenceMl, maxV), new Color32(255, 120, 120, 220), true);
                VLine(px, X(procedure.LastResult.Value.volumeRead, maxV), new Color32(255, 210, 100, 220), true);
            }
            var line = new Color32(110, 220, 255, 255);
            var c = procedure.Curve;
            for (int i = 1; i < c.Count; i++)
                Segment(px, X(c[i - 1].x, maxV), Y(c[i - 1].y), X(c[i].x, maxV), Y(c[i].y), line);
            graphTex.SetPixels32(px);
            graphTex.Apply();
        }

        static int X(float v, float maxV) => Mathf.Clamp(Mathf.RoundToInt(v / maxV * (GraphW - 1)), 0, GraphW - 1);
        static int Y(float ph) => Mathf.Clamp(Mathf.RoundToInt(ph / 14f * (GraphH - 1)), 0, GraphH - 1);

        static void VLine(Color32[] px, int x, Color32 c, bool dashed)
        {
            for (int y = 0; y < GraphH; y++)
                if (!dashed || (y / 5) % 2 == 0) px[y * GraphW + x] = c;
        }

        static void HLine(Color32[] px, int y, Color32 c)
        {
            for (int x = 0; x < GraphW; x++) px[y * GraphW + x] = c;
        }

        static void Segment(Color32[] px, int x0, int y0, int x1, int y1, Color32 c)
        {
            int n = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0), 1);
            for (int i = 0; i <= n; i++)
            {
                int x = x0 + (x1 - x0) * i / n, y = y0 + (y1 - y0) * i / n;
                for (int d = -1; d <= 1; d++)
                    if (y + d >= 0 && y + d < GraphH) px[(y + d) * GraphW + x] = c;
            }
        }
    }
}
