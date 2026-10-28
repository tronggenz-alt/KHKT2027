using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Khkt.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using static Khkt.Demo.GlassKit;

namespace Khkt.Demo.Instruments
{
    /// <summary>
    /// Phần riêng của từng máy trên màn hình vận hành: nút điều khiển, trạng thái máy, sơ đồ nguyên lý, đồ thị, kết quả.
    /// </summary>
    public abstract class InstrumentProgram
    {
        public InstrumentScreen S;
        public InstrumentDoc Doc;
        public LabContainer Sample;
        public abstract string Code { get; }
        /// <summary>Các bước trong quy trình không bắt buộc phải làm trước bước sau (chế độ Hướng dẫn không chặn).</summary>
        public virtual ICollection<string> Optional => Array.Empty<string>();
        public abstract void Build();
        public virtual void Tick(float dt) { }
        public abstract void DrawSchematic(Painter p, float time);
        public abstract void DrawPlot(Painter p);
        /// <summary>Gọi mỗi 0,2 s: cập nhật chữ trên nút, kết quả.</summary>
        public virtual void Refresh() { }
        public virtual void OnClose() { }
        /// <summary>Tên mẫu ở ổ máy (ống nghiệm / cuvet đặt trên nóc máy).</summary>
        public string SampleName => Sample != null ? (string.IsNullOrEmpty(Sample.label) ? Sample.name : Sample.label) : "";
    }

    /// <summary>
    /// Màn hình vận hành máy phân tích (toàn màn hình, giống phần mềm điều khiển máy thật). Khi mở, camera phòng lab tạm tắt.
    ///  • Thanh trên: tên máy, máy mẫu lấy thông số, chế độ Hướng dẫn / Thực hành, Nguyên lý, Thoát (Esc).
    ///  • Trái: các bước quy trình (đã xong / đang làm / chưa), thẻ "Bước tiếp theo" và lý do, dòng cảnh báo.
    ///  • Giữa: sơ đồ nguyên lý bên trong máy (vẽ 2D có hoạt cảnh) và đồ thị (phổ, đường chuẩn, tín hiệu).
    ///  • Phải: bảng điều khiển (nút như máy thật) và kết quả, phân tích chuyên sâu.
    /// Chế độ Hướng dẫn chặn thao tác bỏ bước; chế độ Thực hành cho làm tự do, làm sai thì ra kết quả sai như máy thật.
    /// </summary>
    public class InstrumentScreen : MonoBehaviour
    {
        public static InstrumentScreen Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance.open;

        public bool Guided = true;
        public InstrumentProgram Program { get; private set; }
        public Camera ScreenCamera { get; private set; }
        public AnalyzerInstrument Host { get; private set; }
        public string LastMessage { get; private set; } = "";
        public readonly HashSet<string> DoneSteps = new HashSet<string>();

        const int UiLayer = 5;
        const int SchemW = 980, SchemH = 350, PlotW = 920, PlotH = 470;
        bool open, captured, built;
        Camera viewCam;
        GameObject root;
        Text title, subtitle, modeText, message, guideTitle, guideBody, results, plotTitle, xLabel, yLabel, xMin, xMax, yMin, yMax, popupLeft, popupRight;
        Image messageDot;
        readonly List<Text> stepRows = new List<Text>();
        RectTransform controls, schemRect;
        GameObject popup;
        Painter schem, plot;
        readonly Dictionary<string, Text> schemLabels = new Dictionary<string, Text>();
        readonly List<GameObject> controlItems = new List<GameObject>();
        float refreshT, drawT;

        static readonly Color Bg = new Color(0.03f, 0.05f, 0.09f, 1f);
        static readonly Color PanelColor = new Color(0.07f, 0.11f, 0.17f, 0.96f);

        void Awake() => Instance = this;

        // =====================================================================
        // Mở / đóng
        // =====================================================================

        public static InstrumentScreen Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("Màn hình vận hành máy phân tích");
            go.transform.position = new Vector3(0, -500, 0);  // xa phòng lab: camera riêng chỉ thấy giao diện
            return go.AddComponent<InstrumentScreen>();
        }

        public static void Open(AnalyzerInstrument host, InstrumentProgram program, LabContainer sample)
        {
            var s = Ensure();
            s.Show(host, program, sample);
        }

        void Show(AnalyzerInstrument host, InstrumentProgram program, LabContainer sample)
        {
            if (!built) Build();
            if (open) Close();
            Host = host;
            Program = program;
            program.S = this;
            program.Sample = sample;
            program.Doc = InstrumentData.Instance != null ? InstrumentData.Instance.Doc(program.Code) : new InstrumentDoc { Name = program.Code };
            DoneSteps.Clear();
            ClearControls();
            foreach (var l in schemLabels.Values) l.gameObject.SetActive(false);
            title.text = program.Doc.Name.ToUpperInvariant();
            subtitle.text = "Thông số theo máy mẫu: " + program.Doc.Model;
            popup.SetActive(false);
            program.Build();
            BuildSteps();
            open = true;
            root.SetActive(true);
            ScreenCamera.enabled = true;
            var viewer = FindAnyObjectByType<Khkt.Environment.DesktopViewer>();
            viewCam = viewer != null && viewer.cameraTransform != null ? viewer.cameraTransform.GetComponent<Camera>() : Camera.main;
            if (viewCam != null && viewCam != ScreenCamera) viewCam.enabled = false;  // phòng lab thôi vẽ khi đang vận hành máy
            UiBridge.SetCapture(ref captured, true);
            Say(Guided ? "Chế độ Hướng dẫn: làm theo các bước bên trái; máy chặn thao tác bỏ bước." : "Chế độ Thực hành: tự làm như máy thật; làm sai thì kết quả sai và máy báo lỗi.", 0);
            Refresh();
        }

        public void Close()
        {
            if (!open) return;
            Program?.OnClose();
            open = false;
            root.SetActive(false);
            ScreenCamera.enabled = false;
            if (viewCam != null) viewCam.enabled = true;
            UiBridge.SetCapture(ref captured, false);
        }

        void Update()
        {
            if (!open) return;
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && !UiBridge.Typing)
            {
                if (popup.activeSelf) popup.SetActive(false);
                else { Close(); return; }
            }
            Program.Tick(Time.deltaTime);
            drawT += Time.deltaTime;
            if (drawT >= 1f / 30f)  // vẽ sơ đồ, đồ thị 30 lần/giây
            {
                drawT = 0;
                Program.DrawSchematic(schem, Time.time);
                schem.Apply();
                Program.DrawPlot(plot);
                plot.Apply();
            }
            refreshT += Time.deltaTime;
            if (refreshT >= 0.2f) { refreshT = 0; Refresh(); }
        }

        public void Refresh()
        {
            if (Program == null) return;
            Program.Refresh();
            var doc = Program.Doc;
            int cur = CurrentStep;
            for (int i = 0; i < stepRows.Count; i++)
            {
                if (i >= doc.Steps.Count) { stepRows[i].gameObject.SetActive(false); continue; }
                stepRows[i].gameObject.SetActive(true);
                var st = doc.Steps[i];
                bool done = DoneSteps.Contains(st.Code);
                string mark = done ? "<color=#73F29E>✓</color>" : i == cur ? "<color=#61DBFF>▶</color>" : "<color=#8899AA>•</color>";
                stepRows[i].text = $"{mark}  {(i == cur ? "<b>" + st.Name + "</b>" : st.Name)}";
                stepRows[i].color = done ? Dim : Ink;
            }
            if (Guided && cur >= 0 && cur < doc.Steps.Count)
            {
                var st = doc.Steps[cur];
                guideTitle.text = "Bước tiếp theo: " + st.Name;
                guideBody.text = st.Action + "\n<color=#9FB3C8>Vì sao: " + st.Why + "</color>" + (string.IsNullOrEmpty(st.Mistake) ? "" : "\n<color=#FFC966>Lỗi hay gặp: " + st.Mistake + "</color>");
            }
            else if (Guided)
            {
                guideTitle.text = "Đã xong quy trình";
                guideBody.text = "Xem kết quả và phân tích chuyên sâu ở cột bên phải. Bấm Nguyên lý để ôn lại.";
            }
            else
            {
                guideTitle.text = "Chế độ Thực hành";
                guideBody.text = "Tự làm theo quy trình như máy thật. Máy không chặn thao tác; làm sai bước nào thì kết quả sai đúng như lỗi đó gây ra.";
            }
            modeText.text = Guided ? "Chế độ: Hướng dẫn" : "Chế độ: Thực hành";
        }

        // =====================================================================
        // Quy trình
        // =====================================================================

        public int CurrentStep
        {
            get
            {
                var steps = Program?.Doc?.Steps;
                if (steps == null) return -1;
                for (int i = 0; i < steps.Count; i++)
                {
                    if (DoneSteps.Contains(steps[i].Code)) continue;
                    // bước không bắt buộc đã bị vượt qua (bước sau đã xong) thì không coi là bước đang làm
                    if (Program.Optional.Contains(steps[i].Code) && steps.Skip(i + 1).Any(s => DoneSteps.Contains(s.Code))) continue;
                    return i;
                }
                return steps.Count;
            }
        }

        public void Done(string code)
        {
            if (DoneSteps.Add(code)) Refresh();
        }

        /// <summary>
        /// Chế độ Hướng dẫn: mọi bước bắt buộc trước bước code phải xong, nếu không thì báo và trả false.
        /// Chế độ Thực hành: luôn cho làm (hậu quả do máy tự tính).
        /// </summary>
        public bool Require(string code)
        {
            if (!Guided) return true;
            var steps = Program.Doc.Steps;
            int idx = steps.FindIndex(s => s.Code == code);
            for (int i = 0; i < idx; i++)
            {
                var st = steps[i];
                if (DoneSteps.Contains(st.Code) || Program.Optional.Contains(st.Code)) continue;
                Say($"Chưa xong bước \"{st.Name}\": {st.Action}", 2);
                return false;
            }
            return true;
        }

        /// <summary>Thông báo trên màn hình máy. tone: 0 thông tin, 1 tốt, 2 cảnh báo, 3 nguy hiểm / lỗi.</summary>
        public void Say(string msg, int tone)
        {
            LastMessage = msg;
            message.text = msg;
            var c = tone == 1 ? Good : tone == 2 ? Warn : tone == 3 ? Bad : Accent;
            messageDot.color = c;
            message.color = tone >= 2 ? c : Ink;
            Debug.Log("[KHKT-MAY] " + msg);
        }

        public void SetResults(string rich) => results.text = rich;

        public void SetPlotAxes(string t, string xl, string yl, string x0, string x1, string y0, string y1)
        {
            plotTitle.text = t;
            xLabel.text = xl;
            yLabel.text = yl;
            xMin.text = x0;
            xMax.text = x1;
            yMin.text = y0;
            yMax.text = y1;
        }

        /// <summary>Nhãn chữ đặt trên sơ đồ tại điểm (x, y) của ảnh sơ đồ (gốc dưới trái).</summary>
        public void SchemLabel(string key, string text, float x, float y, int size = 13, Color? color = null, TextAnchor align = TextAnchor.MiddleCenter)
        {
            if (!schemLabels.TryGetValue(key, out var t))
            {
                var rt = Rect(schemRect, "Nhãn " + key, new Vector2(0, 0), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(170, 40));
                t = rt.gameObject.AddComponent<Text>();
                t.font = Regular;
                t.raycastTarget = false;
                t.supportRichText = true;
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                t.verticalOverflow = VerticalWrapMode.Overflow;
                rt.gameObject.layer = UiLayer;
                schemLabels[key] = t;
            }
            t.gameObject.SetActive(true);
            t.text = text;
            t.fontSize = size;
            t.color = color ?? Ink;
            t.alignment = align;
            ((RectTransform)t.transform).anchoredPosition = new Vector2(x, y);
        }

        // =====================================================================
        // Bảng điều khiển (nút do từng máy thêm)
        // =====================================================================

        const float CellW = 136, CellH = 40, Gap = 8;

        public void ClearControls()
        {
            foreach (var g in controlItems) if (g != null) Destroy(g);
            controlItems.Clear();
        }

        /// <summary>Nút ở ô (cột 0..2, hàng 0..), span: số cột chiếm.</summary>
        public Button AddButton(string text, int col, int row, Action onClick, bool primary = false, int span = 1, int fontSize = 14)
        {
            var b = MakeButton(controls, "Nút " + text, text, new Vector2(col * (CellW + Gap), -row * (CellH + Gap)),
                               new Vector2(CellW * span + Gap * (span - 1), CellH), onClick, primary, fontSize);
            foreach (var tr in b.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = UiLayer;
            controlItems.Add(b.gameObject);
            return b;
        }

        public Text AddText(string text, int row, int col = 0, int span = 3, int fontSize = 14, Color? color = null)
        {
            var t = Label(controls, "Chữ", text, fontSize, color ?? Dim, false, TextAnchor.MiddleLeft,
                          new Vector2(col * (CellW + Gap) + 2, -row * (CellH + Gap)), new Vector2(CellW * span + Gap * (span - 1), CellH));
            t.gameObject.layer = UiLayer;
            controlItems.Add(t.gameObject);
            return t;
        }

        public static void SetButton(Button b, string text, bool active, bool highlight = false)
        {
            if (b == null) return;
            var t = b.GetComponentInChildren<Text>();
            if (t != null && text != null) t.text = text;
            b.interactable = active;
            var img = b.GetComponent<Image>();
            if (img != null) img.color = highlight ? new Color(Accent.r, Accent.g, Accent.b, 0.55f) : new Color(1, 1, 1, active ? 0.13f : 0.05f);
        }

        // =====================================================================
        // Dựng giao diện
        // =====================================================================

        void Build()
        {
            built = true;
            ScreenCamera = new GameObject("Camera màn hình máy").AddComponent<Camera>();
            ScreenCamera.transform.SetParent(transform, false);
            ScreenCamera.clearFlags = CameraClearFlags.SolidColor;
            ScreenCamera.backgroundColor = Bg;
            ScreenCamera.cullingMask = 1 << UiLayer;
            ScreenCamera.orthographic = true;
            ScreenCamera.nearClipPlane = 0.1f;
            ScreenCamera.farClipPlane = 3f;
            ScreenCamera.depth = 50;
            var cd = ScreenCamera.GetUniversalAdditionalCameraData();
            cd.renderType = CameraRenderType.Base;
            cd.renderPostProcessing = false;
            ScreenCamera.enabled = false;

            root = new GameObject("Giao diện màn hình máy", typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = ScreenCamera;
            canvas.planeDistance = 1f;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            UiBridge.EnsureInteractive(canvas);
            var r = root.transform;
            var bg = Rect(r, "Nền", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4000, 3000)).gameObject.AddComponent<Image>();
            bg.color = Bg;

            // thanh trên
            var top = Box(r, "Thanh trên", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(1888, 64));
            title = Label(top, "Tên máy", "", 24, Accent, true, TextAnchor.UpperLeft, new Vector2(22, -6), new Vector2(1000, 32));
            subtitle = Label(top, "Máy mẫu", "", 14, Dim, false, TextAnchor.UpperLeft, new Vector2(22, -38), new Vector2(1300, 22));
            Corner(MakeButton(top, "Thoát", "Thoát  Esc", new Vector2(-14, -12), new Vector2(124, 40), Close, false, 15, new Vector2(1, 1)));
            Corner(MakeButton(top, "Nguyên lý", "Nguyên lý · bộ phận", new Vector2(-150, -12), new Vector2(196, 40), ShowPrinciple, false, 15, new Vector2(1, 1)));
            var mode = MakeButton(top, "Chế độ", "", new Vector2(-358, -12), new Vector2(220, 40), () => { Guided = !Guided; Say(Guided ? "Đã chuyển sang chế độ Hướng dẫn" : "Đã chuyển sang chế độ Thực hành", 0); Refresh(); }, true, 15, new Vector2(1, 1));
            Corner(mode);
            modeText = mode.GetComponentInChildren<Text>();

            // trái: quy trình
            var left = Box(r, "Quy trình", new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -88), new Vector2(400, 976));
            Label(left, "Tiêu đề", "Quy trình vận hành", 20, Ink, true, TextAnchor.UpperLeft, new Vector2(18, -14), new Vector2(360, 28));
            for (int i = 0; i < 10; i++)
                stepRows.Add(Label(left, "Bước", "", 16, Ink, false, TextAnchor.MiddleLeft, new Vector2(18, -52 - i * 42), new Vector2(370, 40)));
            var guide = Panel(left, "Thẻ bước", new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -490), new Vector2(376, 290), null, 1.4f);
            guide.color = new Color(1, 1, 1, 0.06f);
            guideTitle = Label(guide.transform, "Tiêu đề", "", 16, Accent, true, TextAnchor.UpperLeft, new Vector2(14, -12), new Vector2(350, 26));
            guideBody = Label(guide.transform, "Nội dung", "", 14, Ink, false, TextAnchor.UpperLeft, new Vector2(14, -44), new Vector2(350, 240));
            guideBody.verticalOverflow = VerticalWrapMode.Truncate;
            var msg = Panel(left, "Thông báo", new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -792), new Vector2(376, 172), null, 1.4f);
            msg.color = new Color(1, 1, 1, 0.06f);
            var dot = Rect(msg.transform, "Chấm", new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(20, -22), new Vector2(10, 10));
            messageDot = dot.gameObject.AddComponent<Image>();
            messageDot.sprite = SolidCircle;
            message = Label(msg.transform, "Chữ", "", 14, Ink, false, TextAnchor.UpperLeft, new Vector2(34, -12), new Vector2(330, 152));
            message.verticalOverflow = VerticalWrapMode.Truncate;

            // giữa: sơ đồ nguyên lý
            var sp = Box(r, "Sơ đồ", new Vector2(0, 1), new Vector2(0, 1), new Vector2(432, -88), new Vector2(1000, 400));
            Label(sp, "Tiêu đề", "Bên trong máy · sơ đồ nguyên lý", 18, Ink, true, TextAnchor.UpperLeft, new Vector2(16, -10), new Vector2(700, 26));
            schemRect = Rect(sp, "Ảnh sơ đồ", new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -42), new Vector2(SchemW, SchemH));
            schem = new Painter(SchemW, SchemH);
            var si = schemRect.gameObject.AddComponent<RawImage>();
            si.texture = schem.Tex;
            si.raycastTarget = false;

            // giữa: đồ thị
            var pp = Box(r, "Đồ thị", new Vector2(0, 1), new Vector2(0, 1), new Vector2(432, -500), new Vector2(1000, 564));
            plotTitle = Label(pp, "Tiêu đề", "", 18, Ink, true, TextAnchor.UpperLeft, new Vector2(16, -10), new Vector2(960, 26));
            var pr = Rect(pp, "Ảnh đồ thị", new Vector2(0, 1), new Vector2(0, 1), new Vector2(64, -44), new Vector2(PlotW, PlotH));
            plot = new Painter(PlotW, PlotH);
            var pi = pr.gameObject.AddComponent<RawImage>();
            pi.texture = plot.Tex;
            pi.raycastTarget = false;
            yLabel = Label(pp, "Trục y", "", 14, Dim, true, TextAnchor.UpperLeft, new Vector2(70, -48), new Vector2(400, 20));
            yMax = Label(pp, "y max", "", 13, Dim, false, TextAnchor.UpperRight, new Vector2(4, -44), new Vector2(56, 18));
            yMin = Label(pp, "y min", "", 13, Dim, false, TextAnchor.LowerRight, new Vector2(4, -496), new Vector2(56, 18));
            xMin = Label(pp, "x min", "", 13, Dim, false, TextAnchor.UpperLeft, new Vector2(64, -518), new Vector2(200, 18));
            xMax = Label(pp, "x max", "", 13, Dim, false, TextAnchor.UpperRight, new Vector2(784, -518), new Vector2(200, 18));
            xLabel = Label(pp, "Trục x", "", 14, Dim, true, TextAnchor.UpperCenter, new Vector2(64, -536), new Vector2(920, 20));

            // phải: điều khiển + kết quả
            var right = Box(r, "Điều khiển", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -88), new Vector2(456, 976));
            Label(right, "Tiêu đề", "Điều khiển máy", 20, Ink, true, TextAnchor.UpperLeft, new Vector2(18, -14), new Vector2(400, 28));
            controls = Rect(right, "Nút", new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -50), new Vector2(424, 430));
            Label(right, "Tiêu đề kết quả", "Kết quả · phân tích chuyên sâu", 18, Accent, true, TextAnchor.UpperLeft, new Vector2(18, -490), new Vector2(420, 26));
            results = Label(right, "Kết quả", "", 14, Ink, false, TextAnchor.UpperLeft, new Vector2(18, -522), new Vector2(424, 444));
            results.verticalOverflow = VerticalWrapMode.Truncate;

            // bảng nguyên lý
            // lớp nền phủ kín phía sau bảng nguyên lý (chữ của màn hình bên dưới không lộ qua)
            var backdrop = Rect(r, "Nền bảng nguyên lý", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4000, 3000));
            var bimg = backdrop.gameObject.AddComponent<Image>();
            bimg.color = new Color(Bg.r, Bg.g, Bg.b, 0.94f);
            var pop = Box(backdrop, "Nguyên lý", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(1440, 880));
            pop.GetComponent<Image>().color = new Color(0.07f, 0.11f, 0.17f, 1f);
            popup = backdrop.gameObject;
            Corner(MakeButton(pop, "Đóng", "Đóng", new Vector2(-18, -16), new Vector2(110, 38), () => popup.SetActive(false), false, 15, new Vector2(1, 1)));
            popupLeft = Label(pop, "Cột trái", "", 15, Ink, false, TextAnchor.UpperLeft, new Vector2(28, -24), new Vector2(680, 830));
            popupRight = Label(pop, "Cột phải", "", 14, Ink, false, TextAnchor.UpperLeft, new Vector2(732, -64), new Vector2(680, 800));
            popupLeft.verticalOverflow = popupRight.verticalOverflow = VerticalWrapMode.Truncate;
            popup.SetActive(false);

            foreach (var tr in root.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = UiLayer;
            root.SetActive(false);
        }

        static void Corner(Button b) => ((RectTransform)b.transform).pivot = new Vector2(1, 1);

        RectTransform Box(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var rt = Rect(parent, name, anchor, pivot, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Solid;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1.6f;
            img.color = PanelColor;
            return rt;
        }

        void BuildSteps() => Refresh();

        public void ShowPrinciple()
        {
            var d = Program.Doc;
            var a = new StringBuilder();
            a.Append("<size=22><b>").Append(d.Name).Append("</b></size>\n<color=#9FB3C8>Máy mẫu: ").Append(d.Model).Append("</color>\n\n<b>Nguyên lý</b>\n");
            foreach (var s in d.Principle) a.Append("• ").Append(s).Append('\n');
            if (d.Formulas.Count > 0) a.Append("\n<b>Công thức</b>\n").Append(string.Join("     ", d.Formulas)).Append('\n');
            a.Append("\n<b>An toàn</b>\n");
            foreach (var s in d.Safety) a.Append("• ").Append(s).Append('\n');
            a.Append("\n<b>Câu hỏi ôn tập</b>\n");
            for (int i = 0; i < d.Questions.Count; i++) a.Append(i + 1).Append(". ").Append(d.Questions[i]).Append('\n');
            popupLeft.text = a.ToString();
            var b = new StringBuilder();
            b.Append("<b>Các bộ phận bên trong</b>\n");
            foreach (var p in d.Parts) b.Append("<color=#61DBFF>").Append(p.name).Append("</color>: ").Append(p.role).Append('\n');
            b.Append("\n<b>Thông số (theo datasheet / tài liệu)</b>\n");
            foreach (var s in d.Specs) b.Append("• ").Append(s.name).Append(": ").Append(s.value).Append(" <color=#8899AA>(").Append(s.kind).Append(")</color>\n");
            b.Append("\n<b>Thuật ngữ</b>\n");
            foreach (var t in d.Terms) b.Append("<color=#61DBFF>").Append(t.term).Append("</color>: ").Append(t.meaning).Append('\n');
            b.Append("\n<color=#8899AA>Nguồn thông số ghi đầy đủ trong data/may/").Append(d.Code).Append(".json và docs/17, docs/18.</color>");
            popupRight.text = b.ToString();
            popup.SetActive(true);
        }

        // =====================================================================
        // Vẽ đồ thị dùng chung
        // =====================================================================

        public static readonly Color PlotBg = new Color(0.04f, 0.07f, 0.12f, 1f), Grid = new Color(1, 1, 1, 0.08f), AxisC = new Color(1, 1, 1, 0.35f);

        /// <summary>Nền đồ thị có lưới 5×4.</summary>
        public static void PlotFrame(Painter p)
        {
            p.Clear(PlotBg);
            for (int i = 1; i < 5; i++) p.FillRect(i * p.W / 5f, 0, 1, p.H, Grid);
            for (int i = 1; i < 4; i++) p.FillRect(0, i * p.H / 4f, p.W, 1, Grid);
            p.FillRect(0, 0, p.W, 2, AxisC);
            p.FillRect(0, 0, 2, p.H, AxisC);
        }

        public static Vector2 Map(Painter p, double x, double y, double x0, double x1, double y0, double y1) =>
            new Vector2((float)((x - x0) / (x1 - x0) * (p.W - 6) + 3), (float)((y - y0) / (y1 - y0) * (p.H - 8) + 3));
    }
}
