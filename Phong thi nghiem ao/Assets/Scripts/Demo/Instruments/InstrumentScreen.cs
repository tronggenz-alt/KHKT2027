using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Khkt.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;
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
        public AnalyzerInstrument Host { get; private set; }
        public string LastMessage { get; private set; } = "";
        public readonly HashSet<string> DoneSteps = new HashSet<string>();

        enum LabelTone { None, Ink, Dim, Accent }

        struct BoxEntry
        {
            public Image Fill;
            public Image Border;
            public float RadiusScale;
            public bool InnerWell;
            public int FillMode; // 0 = PanelColor, 1 = Well, 2 = PopupSurface
        }

        struct LabelEntry
        {
            public Text Label;
            public int BaseSize;
            public LabelTone Tone;
        }

        struct ButtonEntry
        {
            public Button Btn;
            public bool Primary;
            public int BaseFont;
        }

        const int SchemW = 980, SchemH = 350, PlotW = 920, PlotH = 470;
        int uiLayer = 5;
        bool open, captured, built;
        int lastTone;
        UiTheme.Mode builtTheme;
        int builtFontMode;
        GameObject root;
        Image screenBackdropImg, winFrameImg, popupBackdropImg;
        Text title, subtitle, modeText, message, guideTitle, guideBody, results, plotTitle, xLabel, yLabel, xMin, xMax, yMin, yMax, popupLeft, popupRight;
        Image messageDot;
        readonly List<Text> stepRows = new List<Text>();
        RectTransform windowFrame, controls, schemRect;
        GameObject popup;
        Painter schem, plot;
        readonly Dictionary<string, Text> schemLabels = new Dictionary<string, Text>();
        readonly List<GameObject> controlItems = new List<GameObject>();
        readonly List<BoxEntry> trackedBoxes = new List<BoxEntry>();
        readonly List<LabelEntry> trackedLabels = new List<LabelEntry>();
        readonly List<ButtonEntry> staticButtons = new List<ButtonEntry>();
        readonly List<ButtonEntry> controlButtons = new List<ButtonEntry>();
        readonly List<(Text Label, int BaseSize, bool CustomColor)> controlTexts = new List<(Text, int, bool)>();
        float refreshT, drawT;

        static Color HexColor(int rgb, float a = 1f) => new Color(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f, a);
        static Color Bg => UiTheme.Pick(PageBg, PageBg, new Color(0.03f, 0.05f, 0.09f, 1f));
        static Color PanelColor => UiTheme.Pick(Surface, Surface, new Color(0.07f, 0.11f, 0.17f, 0.96f));
        static int ScaleFont(int size) =>
            (UiTheme.FontScaleMode > 0 && size >= 11 && size <= 28)
                ? Mathf.Clamp(Mathf.RoundToInt(size * UiTheme.FontScale), size, size + 5)
                : size;

        void Awake()
        {
            Instance = this;
            UiTheme.Changed += OnThemeChanged;
        }

        void OnDestroy()
        {
            UiTheme.Changed -= OnThemeChanged;
            if (captured) UiBridge.SetCapture(ref captured, false);
            DestroyUi();
            if (Instance == this) Instance = null;
        }

        void OnThemeChanged()
        {
            if (!built || root == null) return;
            if (!open)
            {
                DestroyUi();
                return;
            }
            ApplyWindowScale();
            ApplyLiveTheme();
        }

        void DestroyUi()
        {
            if (root != null) { Destroy(root); root = null; }
            DestroyPainter(ref schem);
            DestroyPainter(ref plot);
            stepRows.Clear();
            schemLabels.Clear();
            controlItems.Clear();
            trackedBoxes.Clear();
            trackedLabels.Clear();
            staticButtons.Clear();
            controlButtons.Clear();
            controlTexts.Clear();
            windowFrame = null;
            controls = null;
            schemRect = null;
            screenBackdropImg = null;
            winFrameImg = null;
            popupBackdropImg = null;
            popup = null;
            built = false;
        }

        static void DestroyPainter(ref Painter p)
        {
            if (p == null) return;
            if (p.Tex != null) Destroy(p.Tex);
            p = null;
        }

        void ApplyWindowScale()
        {
            if (windowFrame == null) return;
            float s = UiTheme.InstrumentWindowScale;
            windowFrame.localScale = new Vector3(s, s, 1f);
        }

        void ApplyLiveTheme()
        {
            builtTheme = UiTheme.Current;
            builtFontMode = UiTheme.FontScaleMode;
            if (screenBackdropImg != null) screenBackdropImg.color = Scrim(0.46f);
            if (popupBackdropImg != null) popupBackdropImg.color = Scrim(0.84f);
            if (winFrameImg != null)
            {
                var canvas = winFrameImg.canvas;
                var shader = Shader.Find("Khkt/KinhMo");
                bool glassOk = !ReducedTransparency && shader != null && canvas != null && canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceCamera;
                if (glassOk)
                {
                    winFrameImg.sprite = Rounded;
                    winFrameImg.type = Image.Type.Sliced;
                    winFrameImg.pixelsPerUnitMultiplier = 1.15f;
                    winFrameImg.material = Glass(shader);
                    winFrameImg.color = new Color(1f, 1f, 1f, 1f);
                }
                else
                {
                    winFrameImg.material = null;
                    winFrameImg.sprite = Solid;
                    winFrameImg.type = Image.Type.Sliced;
                    winFrameImg.pixelsPerUnitMultiplier = UiTheme.IsVera ? 1.15f : 2.8f;
                    winFrameImg.color = Surface;
                }
            }
            for (int i = 0; i < trackedBoxes.Count; i++)
            {
                var b = trackedBoxes[i];
                float ppu = b.InnerWell ? (UiTheme.IsVera ? 1.4f : 2.6f) : b.RadiusScale;
                if (b.Fill != null)
                {
                    b.Fill.sprite = Solid;
                    b.Fill.pixelsPerUnitMultiplier = ppu;
                    b.Fill.color = b.FillMode == 2
                        ? UiTheme.Pick(SurfaceA(1f), SurfaceA(1f), new Color(0.07f, 0.11f, 0.17f, 1f))
                        : b.FillMode == 1 ? Well : PanelColor;
                }
                if (b.Border != null)
                {
                    b.Border.sprite = Border;
                    b.Border.pixelsPerUnitMultiplier = ppu;
                    b.Border.color = Line;
                }
            }
            for (int i = 0; i < trackedLabels.Count; i++)
            {
                var l = trackedLabels[i];
                if (l.Label == null) continue;
                l.Label.fontSize = ScaleFont(l.BaseSize);
                if (l.Tone == LabelTone.Ink) l.Label.color = Ink;
                else if (l.Tone == LabelTone.Dim) l.Label.color = Dim;
                else if (l.Tone == LabelTone.Accent) l.Label.color = Accent;
            }
            for (int i = 0; i < staticButtons.Count; i++) RefreshButtonTheme(staticButtons[i]);
            for (int i = 0; i < controlButtons.Count; i++) RefreshButtonTheme(controlButtons[i]);
            for (int i = 0; i < controlTexts.Count; i++)
            {
                var ct = controlTexts[i];
                if (ct.Label == null) continue;
                ct.Label.fontSize = ScaleFont(ct.BaseSize);
                if (!ct.CustomColor) ct.Label.color = Dim;
            }
            ApplyMessageStyle(lastTone);
            if (Program != null)
            {
                Refresh();
                if (popup != null && popup.activeSelf) ShowPrinciple();
                if (schem != null) { Program.DrawSchematic(schem, Time.time); schem.Apply(); }
                if (plot != null) { Program.DrawPlot(plot); plot.Apply(); }
            }
        }

        static void RefreshButtonTheme(ButtonEntry entry)
        {
            var b = entry.Btn;
            if (b == null) return;
            float ppu = UiTheme.IsVera ? 2.2f : 3f;
            bool primary = entry.Primary;
            bool disabled = !b.interactable && !primary;
            var img = b.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = Solid;
                img.pixelsPerUnitMultiplier = ppu;
                img.color = primary ? (UiTheme.IsVera ? AccentFill : Accent) : (disabled ? Well : ButtonFill);
            }
            var border = b.transform.Find("Viền")?.GetComponent<Image>();
            if (border != null)
            {
                border.sprite = Border;
                border.pixelsPerUnitMultiplier = ppu;
                border.enabled = !primary || disabled;
                border.color = Line;
            }
            var colors = b.colors;
            colors.normalColor = UiTheme.IsLight ? new Color(1f, 1f, 1f, 1f) : new Color(0.92f, 0.92f, 0.92f, 1f);
            colors.highlightedColor = UiTheme.IsLight ? new Color(0.93f, 0.94f, 0.96f, 1f) : new Color(1f, 1f, 1f, 1f);
            colors.selectedColor = colors.normalColor;
            colors.pressedColor = UiTheme.IsLight ? new Color(0.83f, 0.86f, 0.90f, 1f) : new Color(0.70f, 0.70f, 0.70f, 1f);
            b.colors = colors;
            var t = b.GetComponentInChildren<Text>();
            if (t != null)
            {
                t.font = primary ? Semibold : Regular;
                t.fontSize = ScaleFont(entry.BaseFont);
                t.color = primary ? OnAccent : (disabled ? Muted : Ink);
            }
        }

        // =====================================================================
        // Mở / đóng
        // =====================================================================

        public static InstrumentScreen Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("Màn hình vận hành máy phân tích");
            return go.AddComponent<InstrumentScreen>();
        }

        public static void Open(AnalyzerInstrument host, InstrumentProgram program, LabContainer sample)
        {
            var s = Ensure();
            s.Show(host, program, sample);
        }

        void Show(AnalyzerInstrument host, InstrumentProgram program, LabContainer sample)
        {
            if (built && (builtTheme != UiTheme.Current || builtFontMode != UiTheme.FontScaleMode))
                DestroyUi();
            if (!built) Build();
            ApplyWindowScale();
            if (open) Close();
            Host = host;
            Program = program;
            program.S = this;
            program.Sample = sample;
            program.Doc = InstrumentData.Instance != null ? InstrumentData.Instance.Doc(program.Code) : new InstrumentDoc { Name = program.Code };
            // máy sinh học tầng 2: hồ sơ không nằm trong InstrumentData mà đọc từ data/may/<ma>.json
            if (program.Doc == null || program.Doc.Steps.Count == 0) program.Doc = Khkt.Demo.Bio.BioCatalogLoader.LoadMachineDoc(program.Code);
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
            root.transform.SetAsLastSibling();
            if (windowFrame != null) UiMotion.Popup(this, windowFrame, null, 0.22f);
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
            UiBridge.SetCapture(ref captured, false);
        }

        void Update()
        {
            if (!open) return;
            var kb = Keyboard.current;
            bool chatOpen = RobotChatUI.Instance != null && RobotChatUI.Instance.IsOpen;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && !UiBridge.Typing && !chatOpen && UiBridge.LastCloseFrame != Time.frameCount)
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
                string mark = done ? $"<color={HexGood}>✓</color>" : i == cur ? $"<color={HexAccent}>▶</color>" : $"<color={Hex(Muted)}>•</color>";
                stepRows[i].text = $"{mark}  {(i == cur ? "<b>" + st.Name + "</b>" : st.Name)}";
                stepRows[i].color = done ? Dim : Ink;
            }
            if (Guided && cur >= 0 && cur < doc.Steps.Count)
            {
                var st = doc.Steps[cur];
                guideTitle.text = "Bước tiếp theo: " + st.Name;
                guideBody.text = st.Action + $"\n<color={HexDim}>Vì sao: " + st.Why + "</color>" + (string.IsNullOrEmpty(st.Mistake) ? "" : $"\n<color={HexWarn}>Lỗi hay gặp: " + st.Mistake + "</color>");
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
            lastTone = tone;
            message.text = msg;
            ApplyMessageStyle(tone);
            Debug.Log("[KHKT-MAY] " + msg);
        }

        void ApplyMessageStyle(int tone)
        {
            if (message == null || messageDot == null) return;
            var c = tone == 1 ? Good : tone == 2 ? Warn : tone == 3 ? Bad : Accent;
            messageDot.color = c;
            message.color = tone >= 2 ? c : Ink;
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
        public void SchemLabel(string key, string text, float x, float y, int size = 15, Color? color = null, TextAnchor align = TextAnchor.MiddleCenter)
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
                rt.gameObject.layer = uiLayer;
                schemLabels[key] = t;
            }
            t.gameObject.SetActive(true);
            t.text = text;
            t.fontSize = ScaleFont(Mathf.Max(size, 14));
            t.color = color ?? Ink;
            t.alignment = align;
            ((RectTransform)t.transform).anchoredPosition = new Vector2(x, y);
        }

        // =====================================================================
        // Bảng điều khiển (nút do từng máy thêm)
        // =====================================================================

        const float CellW = 138, CellH = 45, Gap = 5;

        public void ClearControls()
        {
            foreach (var g in controlItems) if (g != null) Destroy(g);
            controlItems.Clear();
            controlButtons.Clear();
            controlTexts.Clear();
        }

        /// <summary>Nút ở ô (cột 0..2, hàng 0..), span: số cột chiếm.</summary>
        public Button AddButton(string text, int col, int row, Action onClick, bool primary = false, int span = 1, int fontSize = 14)
        {
            int fs = Mathf.Max(fontSize, 14);
            var b = MakeButton(controls, "Nút " + text, text, new Vector2(col * (CellW + Gap), -row * (CellH + Gap)),
                               new Vector2(CellW * span + Gap * (span - 1), Mathf.Max(CellH, 44f)), onClick, primary, fs);
            foreach (var tr in b.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = uiLayer;
            controlItems.Add(b.gameObject);
            controlButtons.Add(new ButtonEntry { Btn = b, Primary = primary, BaseFont = fs });
            return b;
        }

        public Text AddText(string text, int row, int col = 0, int span = 3, int fontSize = 14, Color? color = null)
        {
            int fs = Mathf.Max(fontSize, 14);
            var t = Label(controls, "Chữ", text, fs, color ?? Dim, false, TextAnchor.MiddleLeft,
                          new Vector2(col * (CellW + Gap) + 2, -row * (CellH + Gap)), new Vector2(CellW * span + Gap * (span - 1), 44f));
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.gameObject.layer = uiLayer;
            controlItems.Add(t.gameObject);
            controlTexts.Add((t, fs, color.HasValue));
            return t;
        }

        public static void SetButton(Button b, string text, bool active, bool highlight = false)
        {
            if (b == null) return;
            var t = b.GetComponentInChildren<Text>();
            if (t != null && text != null) t.text = text;
            b.interactable = active;
            var img = b.GetComponent<Image>();
            if (img != null) img.color = highlight ? AccentSoft : (active ? ButtonFill : Well);
            if (t != null) t.color = active ? (highlight ? Accent : Ink) : Muted;
            var border = b.transform.Find("Viền")?.GetComponent<Image>();
            if (border != null)
            {
                border.enabled = true;
                border.color = highlight ? Accent : Line;
            }
        }

        // =====================================================================
        // Dựng giao diện
        // =====================================================================

        void Build()
        {
            built = true;
            builtTheme = UiTheme.Current;
            builtFontMode = UiTheme.FontScaleMode;
            var host = HostCanvas(transform, 48);
            uiLayer = host.gameObject.layer;

            var rootRt = new GameObject("Giao diện màn hình máy", typeof(RectTransform)).GetComponent<RectTransform>();
            rootRt.SetParent(host.transform, false);
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = rootRt.offsetMax = Vector2.zero;
            root = rootRt.gameObject;

            screenBackdropImg = Backdrop(rootRt, "Nền màn hình máy", Close).GetComponent<Image>();
            var win = Window(rootRt, "Khung màn hình máy", new Vector2(1920, 1080), Vector2.zero);
            winFrameImg = win;
            var r = win.rectTransform;
            windowFrame = r;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            ApplyWindowScale();

            // thanh trên
            var top = Box(r, "Thanh trên", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(1888, 64));
            top.anchorMin = new Vector2(0, 1);
            top.anchorMax = new Vector2(1, 1);
            top.pivot = new Vector2(0.5f, 1);
            top.offsetMin = new Vector2(16, -76);
            top.offsetMax = new Vector2(-16, -12);

            title = TrackLabel(Label(top, "Tên máy", "", 24, Accent, true, TextAnchor.MiddleLeft, new Vector2(28, -20), new Vector2(1060, 36)), 24, LabelTone.Accent);
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            title.verticalOverflow = VerticalWrapMode.Truncate;
            subtitle = TrackLabel(Label(top, "Máy mẫu", "", 14, Dim, false, TextAnchor.MiddleLeft, new Vector2(28, -48), new Vector2(1060, 24)), 14, LabelTone.Dim);
            subtitle.horizontalOverflow = HorizontalWrapMode.Wrap;
            subtitle.verticalOverflow = VerticalWrapMode.Truncate;
            string exitText = Khkt.Input.PadInput.UsingTouch ? "Đóng ✕" : "Thoát  Esc";
            TrackBtn(Corner(MakeButton(top, "Thoát", exitText, new Vector2(-14, -8), new Vector2(124, Mathf.Max(48, 44)), Close, false, 15, new Vector2(1, 1))), false, 15);
            TrackBtn(Corner(MakeButton(top, "Nguyên lý", "Nguyên lý · bộ phận", new Vector2(-150, -8), new Vector2(196, Mathf.Max(48, 44)), ShowPrinciple, false, 15, new Vector2(1, 1))), false, 15);
            var mode = MakeButton(top, "Chế độ", "", new Vector2(-358, -8), new Vector2(220, Mathf.Max(48, 44)), () => { Guided = !Guided; Say(Guided ? "Đã chuyển sang chế độ Hướng dẫn" : "Đã chuyển sang chế độ Thực hành", 0); Refresh(); }, true, 15, new Vector2(1, 1));
            TrackBtn(Corner(mode), true, 15);
            modeText = mode.GetComponentInChildren<Text>();
            TrackBtn(Corner(MakeButton(top, "Hỏi trợ lý", "Hỏi trợ lý", new Vector2(-590, -8), new Vector2(160, Mathf.Max(48, 44)), AskAssistant, false, 15, new Vector2(1, 1))), false, 15);

            // trái: quy trình
            var left = Box(r, "Quy trình", new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -88), new Vector2(400, 976));
            TrackLabel(Label(left, "Tiêu đề", "Quy trình vận hành", 20, Ink, true, TextAnchor.UpperLeft, new Vector2(18, -14), new Vector2(360, 28)), 20, LabelTone.Ink);
            for (int i = 0; i < 10; i++)
                stepRows.Add(TrackLabel(Label(left, "Bước", "", 16, Ink, false, TextAnchor.MiddleLeft, new Vector2(18, -52 - i * 42), new Vector2(370, 40)), 16, LabelTone.None));
            float innerPpu = UiTheme.IsVera ? 1.4f : 2.6f;
            var guide = Box(left, "Thẻ bước", new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -490), new Vector2(376, 290), Well, innerPpu, true, 1);
            guideTitle = TrackLabel(Label(guide, "Tiêu đề", "", 16, Accent, true, TextAnchor.UpperLeft, new Vector2(14, -12), new Vector2(350, 26)), 16, LabelTone.Accent);
            guideBody = TrackLabel(Label(guide, "Nội dung", "", 14, Ink, false, TextAnchor.UpperLeft, new Vector2(14, -44), new Vector2(350, 240)), 14, LabelTone.Ink);
            guideBody.verticalOverflow = VerticalWrapMode.Truncate;
            var msg = Box(left, "Thông báo", new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -792), new Vector2(376, 172), Well, innerPpu, true, 1);
            var dot = Rect(msg, "Chấm", new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(20, -22), new Vector2(10, 10));
            messageDot = dot.gameObject.AddComponent<Image>();
            messageDot.sprite = SolidCircle;
            message = TrackLabel(Label(msg, "Chữ", "", 14, Ink, false, TextAnchor.UpperLeft, new Vector2(34, -12), new Vector2(330, 152)), 14, LabelTone.None);
            message.verticalOverflow = VerticalWrapMode.Truncate;

            // giữa: sơ đồ nguyên lý
            var sp = Box(r, "Sơ đồ", new Vector2(0, 1), new Vector2(0, 1), new Vector2(432, -88), new Vector2(1000, 400));
            TrackLabel(Label(sp, "Tiêu đề", "Bên trong máy · sơ đồ nguyên lý", 18, Ink, true, TextAnchor.UpperLeft, new Vector2(16, -10), new Vector2(700, 26)), 18, LabelTone.Ink);
            schemRect = Rect(sp, "Ảnh sơ đồ", new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -42), new Vector2(SchemW, SchemH));
            schem = new Painter(SchemW, SchemH);
            var si = schemRect.gameObject.AddComponent<RawImage>();
            si.texture = schem.Tex;
            si.raycastTarget = false;

            // giữa: đồ thị
            var pp = Box(r, "Đồ thị", new Vector2(0, 1), new Vector2(0, 1), new Vector2(432, -500), new Vector2(1000, 564));
            plotTitle = TrackLabel(Label(pp, "Tiêu đề", "", 18, Ink, true, TextAnchor.UpperLeft, new Vector2(16, -10), new Vector2(960, 26)), 18, LabelTone.Ink);
            var pr = Rect(pp, "Ảnh đồ thị", new Vector2(0, 1), new Vector2(0, 1), new Vector2(64, -44), new Vector2(PlotW, PlotH));
            plot = new Painter(PlotW, PlotH);
            var pi = pr.gameObject.AddComponent<RawImage>();
            pi.texture = plot.Tex;
            pi.raycastTarget = false;
            yLabel = TrackLabel(Label(pp, "Trục y", "", 14, Dim, true, TextAnchor.UpperLeft, new Vector2(70, -48), new Vector2(400, 20)), 14, LabelTone.Dim);
            yMax = TrackLabel(Label(pp, "y max", "", 14, Dim, false, TextAnchor.UpperRight, new Vector2(4, -44), new Vector2(56, 22)), 14, LabelTone.Dim);
            yMin = TrackLabel(Label(pp, "y min", "", 14, Dim, false, TextAnchor.LowerRight, new Vector2(4, -492), new Vector2(56, 22)), 14, LabelTone.Dim);
            xMin = TrackLabel(Label(pp, "x min", "", 14, Dim, false, TextAnchor.UpperLeft, new Vector2(64, -518), new Vector2(200, 22)), 14, LabelTone.Dim);
            xMax = TrackLabel(Label(pp, "x max", "", 14, Dim, false, TextAnchor.UpperRight, new Vector2(784, -518), new Vector2(200, 22)), 14, LabelTone.Dim);
            xLabel = TrackLabel(Label(pp, "Trục x", "", 14, Dim, true, TextAnchor.UpperCenter, new Vector2(64, -536), new Vector2(920, 20)), 14, LabelTone.Dim);

            // phải: điều khiển + kết quả
            var right = Box(r, "Điều khiển", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -88), new Vector2(456, 976));
            TrackLabel(Label(right, "Tiêu đề", "Điều khiển máy", 20, Ink, true, TextAnchor.UpperLeft, new Vector2(18, -14), new Vector2(400, 28)), 20, LabelTone.Ink);
            controls = Rect(right, "Nút", new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -48), new Vector2(424, 450));
            TrackLabel(Label(right, "Tiêu đề kết quả", "Kết quả · phân tích chuyên sâu", 18, Accent, true, TextAnchor.UpperLeft, new Vector2(18, -512), new Vector2(420, 26)), 18, LabelTone.Accent);
            results = TrackLabel(Label(right, "Kết quả", "", 14, Ink, false, TextAnchor.UpperLeft, new Vector2(18, -542), new Vector2(424, 422)), 14, LabelTone.Ink);
            results.verticalOverflow = VerticalWrapMode.Truncate;

            // bảng nguyên lý
            // lớp nền phủ kín phía sau bảng nguyên lý (chữ của màn hình bên dưới không lộ qua, chặn bấm xuyên qua khung)
            var backdrop = Rect(r, "Nền bảng nguyên lý", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4000, 3000));
            var bimg = backdrop.gameObject.AddComponent<Image>();
            popupBackdropImg = bimg;
            bimg.sprite = Plain;
            bimg.color = Scrim(0.84f);
            bimg.raycastTarget = true;
            var pop = Box(backdrop, "Nguyên lý", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(1440, 880),
                          UiTheme.Pick(SurfaceA(1f), SurfaceA(1f), new Color(0.07f, 0.11f, 0.17f, 1f)), null, false, 2);
            popup = backdrop.gameObject;
            TrackBtn(Corner(MakeButton(pop, "Đóng", "Đóng ✕", new Vector2(-18, -14), new Vector2(120, Mathf.Max(46, 44)), () => popup.SetActive(false), false, 15, new Vector2(1, 1))), false, 15);
            popupLeft = TrackLabel(Label(pop, "Cột trái", "", 15, Ink, false, TextAnchor.UpperLeft, new Vector2(28, -24), new Vector2(680, 830)), 15, LabelTone.Ink);
            popupRight = TrackLabel(Label(pop, "Cột phải", "", 14, Ink, false, TextAnchor.UpperLeft, new Vector2(732, -64), new Vector2(680, 800)), 14, LabelTone.Ink);
            popupLeft.verticalOverflow = popupRight.verticalOverflow = VerticalWrapMode.Truncate;
            popup.SetActive(false);

            foreach (var tr in root.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = uiLayer;
            root.SetActive(false);
        }

        static Button Corner(Button b)
        {
            ((RectTransform)b.transform).pivot = new Vector2(1, 1);
            return b;
        }

        Text TrackLabel(Text t, int baseSize, LabelTone tone)
        {
            trackedLabels.Add(new LabelEntry { Label = t, BaseSize = baseSize, Tone = tone });
            return t;
        }

        Button TrackBtn(Button b, bool primary, int baseFont)
        {
            staticButtons.Add(new ButtonEntry { Btn = b, Primary = primary, BaseFont = baseFont });
            return b;
        }

        RectTransform Box(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size,
                          Color? fill = null, float? radiusScale = null, bool innerWell = false, int fillMode = 0)
        {
            float ppu = radiusScale ?? 1.6f;
            var rt = Rect(parent, name, anchor, pivot, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Solid;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = ppu;
            img.color = fill ?? PanelColor;
            var borderGo = new GameObject("Viền", typeof(RectTransform));
            var brt = (RectTransform)borderGo.transform;
            brt.SetParent(rt, false);
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = brt.offsetMax = Vector2.zero;
            var bimg = borderGo.AddComponent<Image>();
            bimg.sprite = Border;
            bimg.type = Image.Type.Sliced;
            bimg.pixelsPerUnitMultiplier = ppu;
            bimg.color = Line;
            bimg.raycastTarget = false;
            trackedBoxes.Add(new BoxEntry
            {
                Fill = img,
                Border = bimg,
                RadiusScale = ppu,
                InnerWell = innerWell,
                FillMode = fillMode,
            });
            return rt;
        }

        void AskAssistant()
        {
            string name = Program?.Doc != null && !string.IsNullOrEmpty(Program.Doc.Name) ? Program.Doc.Name : "Máy phân tích";
            string model = Program?.Doc != null && !string.IsNullOrEmpty(Program.Doc.Model) ? Program.Doc.Model : "";
            string topic = !string.IsNullOrEmpty(model) ? $"{name} ({model})" : name;
            int cur = CurrentStep;
            string question = null;
            if (Program?.Doc?.Steps != null && cur >= 0 && cur < Program.Doc.Steps.Count)
            {
                var st = Program.Doc.Steps[cur];
                question = $"Tôi đang vận hành {topic}, bước hiện tại là \"{st.Name}\" ({st.Action}). Hãy hướng dẫn cách thực hiện, thông số cần chú ý và lưu ý an toàn.";
            }
            RobotChatUI.AskAbout(topic, question);
        }

        void BuildSteps() => Refresh();

        public void ShowPrinciple()
        {
            var d = Program.Doc;
            var a = new StringBuilder();
            a.Append("<size=22><b>").Append(d.Name).Append($"</b></size>\n<color={HexDim}>Máy mẫu: ").Append(d.Model).Append("</color>\n\n<b>Nguyên lý</b>\n");
            foreach (var s in d.Principle) a.Append("• ").Append(s).Append('\n');
            if (d.Formulas.Count > 0) a.Append("\n<b>Công thức</b>\n").Append(string.Join("     ", d.Formulas)).Append('\n');
            a.Append("\n<b>An toàn</b>\n");
            foreach (var s in d.Safety) a.Append("• ").Append(s).Append('\n');
            a.Append("\n<b>Câu hỏi ôn tập</b>\n");
            for (int i = 0; i < d.Questions.Count; i++) a.Append(i + 1).Append(". ").Append(d.Questions[i]).Append('\n');
            popupLeft.text = a.ToString();
            var b = new StringBuilder();
            b.Append("<b>Các bộ phận bên trong</b>\n");
            foreach (var p in d.Parts) b.Append($"<color={HexAccent}>").Append(p.name).Append("</color>: ").Append(p.role).Append('\n');
            b.Append("\n<b>Thông số (theo datasheet / tài liệu)</b>\n");
            foreach (var s in d.Specs) b.Append("• ").Append(s.name).Append(": ").Append(s.value).Append($" <color={Hex(Muted)}>(").Append(s.kind).Append(")</color>\n");
            b.Append("\n<b>Thuật ngữ</b>\n");
            foreach (var t in d.Terms) b.Append($"<color={HexAccent}>").Append(t.term).Append("</color>: ").Append(t.meaning).Append('\n');
            b.Append($"\n<color={Hex(Muted)}>Nguồn thông số ghi đầy đủ trong data/may/").Append(d.Code).Append(".json và docs/17, docs/18.</color>");
            popupRight.text = b.ToString();
            popup.SetActive(true);
            var popRt = popup.transform.Find("Nguyên lý") as RectTransform;
            if (popRt != null) UiMotion.Popup(this, popRt, null, 0.20f);
        }

        // =====================================================================
        // Vẽ đồ thị dùng chung
        // =====================================================================

        public static Color PlotBg => UiTheme.Pick(HexColor(0xf7f8fa), HexColor(0x101013), new Color(0.04f, 0.07f, 0.12f, 1f));
        public static Color Grid => UiTheme.Pick(new Color(0f, 0f, 0f, 0.10f), new Color(1f, 1f, 1f, 0.14f), HexColor(0x73c7ff, 0.14f));
        public static Color AxisC => UiTheme.Pick(HexColor(0x3a3c42), HexColor(0x8e8e96), new Color(1f, 1f, 1f, 0.35f));

        public static Color TraceCyan => UiTheme.Pick(HexColor(0x0066cc), HexColor(0x45d6ff), HexColor(0x43dcff));
        public static Color TraceGold => UiTheme.Pick(HexColor(0xb35900), HexColor(0xffc145), HexColor(0xffc759));
        public static Color TraceGreen => UiTheme.Pick(HexColor(0x0a7a56), HexColor(0x66e896), HexColor(0x73f29e));
        public static Color TraceCoral => UiTheme.Pick(HexColor(0xc62828), HexColor(0xff6b6b), HexColor(0xff7373));
        public static Color TraceViolet => UiTheme.Pick(HexColor(0x6a1b9a), HexColor(0xc494ff), HexColor(0xc9a6ff));
        public static Color TraceMagenta => UiTheme.Pick(HexColor(0xad1457), HexColor(0xff7eb6), HexColor(0xff8cc6));
        public static Color TraceDim => UiTheme.Pick(new Color(0f, 0f, 0f, 0.25f), new Color(1f, 1f, 1f, 0.25f), new Color(1f, 1f, 1f, 0.25f));
        public static Color TraceMarker => UiTheme.Pick(HexColor(0x1d1d1f), HexColor(0xf5f5f7), HexColor(0xf2f7ff));

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