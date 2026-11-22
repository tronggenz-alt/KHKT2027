using System;
using System.Collections.Generic;
using System.Globalization;
using Khkt.Environment;
using Khkt.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static Khkt.Demo.GlassKit;

namespace Khkt.Demo
{
    /// <summary>Một thông số chỉnh được trên bảng điều khiển máy: thanh trượt (min..max, bước step) hoặc chọn một trong vài mức (options).</summary>
    public class MachineParam
    {
        public string label, unit = "", format = "0";
        public float min, max = 1f, step = 1f;
        public Func<float> get;
        public Action<float> set;
        /// <summary>Dòng giải thích dưới thanh trượt, tính theo giá trị đang đặt (ví dụ quy đổi rpm sang ×g, cảnh báo quá nhiệt).</summary>
        public Func<float, string> note;
        /// <summary>Có thì thông số là lựa chọn: giá trị là chỉ số mức đang chọn.</summary>
        public string[] options;

        /// <summary>Gợi ý đơn vị phụ song hành hai chiều (ví dụ rpm &lt;-&gt; ×g cho máy ly tâm).</summary>
        public string altUnit, altFormat = "0";
        public float altMin, altMax, altStep;
        public Func<float, float> toAlt;
        public Func<float, float> fromAlt;

        public static MachineParam Slider(string label, string unit, float min, float max, float step, Func<float> get, Action<float> set,
                                          string format = "0", Func<float, string> note = null) =>
            new MachineParam { label = label, unit = unit, min = min, max = max, step = step, get = get, set = set, format = format, note = note };

        public static MachineParam Choice(string label, string[] options, Func<int> get, Action<int> set, Func<float, string> note = null) =>
            new MachineParam { label = label, options = options, get = () => get(), set = v => set(Mathf.RoundToInt(v)), note = note };
    }

    /// <summary>Một chương trình (bộ thông số cài đặt sẵn hoặc người dùng lưu mới) cho bảng máy.</summary>
    public class MachinePreset
    {
        public string name;
        public float[] values;
        public bool isCustom;

        public MachinePreset(string name, params float[] values)
        {
            this.name = name;
            this.values = values;
        }
    }

    /// <summary>Mô tả bảng điều khiển của một máy: thông số, nút chạy, trạng thái, kết quả.</summary>
    public class MachineSpec
    {
        /// <summary>Mã ngắn in ở đầu bảng, ví dụ "MÁY · PCR".</summary>
        public string code = "MÁY";
        public string title = "", subtitle = "";
        /// <summary>Khóa riêng để lưu các chương trình tùy chỉnh vào PlayerPrefs.</summary>
        public string presetKey;
        public readonly List<MachineParam> parameters = new List<MachineParam>();
        public readonly List<MachinePreset> presets = new List<MachinePreset>();
        /// <summary>Chữ trên nút chạy (đổi theo trạng thái, ví dụ "Bật nguồn" / "Tắt nguồn").</summary>
        public Func<string> runLabel = () => "Chạy máy";
        /// <summary>Bấm chạy: trả về dòng kết quả (rỗng nếu kết quả tới sau, lấy qua result).</summary>
        public Func<string> run;
        /// <summary>Dòng trạng thái cập nhật liên tục.</summary>
        public Func<string> status;
        /// <summary>Máy đang chạy (khóa thanh trượt và nút chạy).</summary>
        public Func<bool> busy;
        /// <summary>Tiến độ 0..1 khi đang chạy.</summary>
        public Func<float> progress;
        /// <summary>Kết quả mới nhất (máy chạy theo thời gian thì điền sau khi xong).</summary>
        public Func<string> result;
        /// <summary>Ảnh đồ thị / phổ của máy (nếu có), hiện ở cột trái dưới hình thiết bị.</summary>
        public Func<Texture> chart;
        /// <summary>Hình mô phỏng 2D riêng của máy (ví dụ sơ đồ rotor ly tâm). Nếu null sẽ chụp xem trước 3D qua ToolPreview.</summary>
        public Func<Texture> visual;
        /// <summary>Nút phụ mở màn hình vận hành chi tiết của máy (nếu có).</summary>
        public string advancedLabel;
        public Action advanced;
    }

    /// <summary>Máy có bảng điều khiển: bấm vào máy thì HandController mở MachinePanel thay vì chạy ngay.</summary>
    public interface IMachinePanel
    {
        MachineSpec PanelSpec();
    }

    /// <summary>
    /// Bảng điều khiển máy (75% màn hình 1660×940 trên canvas 1920×1080):
    ///   • Đầu bảng (62 px): biểu tượng + tên máy, chip chế độ (Hướng dẫn / Thực hành), thẻ trạng thái, nút Đóng.
    ///   • Cột trái (46%): hình thiết bị / sơ đồ rotor (visual hoặc ToolPreview 3D), thẻ trạng thái nhanh và đồ thị (nếu có).
    ///   • Cột phải (54%): thanh trượt / chip thông số, 3 thẻ số đo trực tiếp, thanh tiến độ + ô kết quả, dải gợi ý an toàn.
    ///   • Chân bảng (68 px): nút phụ / Hỏi trợ lý (trái) · Đặt lại / Chạy máy (phải).
    /// </summary>
    public class MachinePanel : MonoBehaviour
    {
        public static MachinePanel Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance.open;
        /// <summary>Bài kiểm thử tự động gọi thẳng Use() của máy: tắt bảng để không chặn kịch bản.</summary>
        public static bool Enabled = string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("KHKT_TEST"));
        public static bool ForceEnableForTest;

        const float PanelW = 1660f, PanelH = 940f;
        const float HeaderH = 62f, FooterH = 68f;
        const float BodyH = PanelH - HeaderH - FooterH;
        const float LeftColW = 764f;   // 46%
        const float RightColX = LeftColW + 1f;
        const float RightColW = PanelW - RightColX; // 54%
        const float Pad = 28f;

        static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

        bool open, captured;
        bool guidedMode = true;
        UiTheme.Mode builtTheme;
        int builtFontScale;
        Canvas canvas;
        GameObject root;
        RectTransform animFrame, panel, content;
        CanvasGroup group;
        MachineSpec spec;
        UnityEngine.Object owner;

        // Đầu bảng
        Button guidedChip, practiceChip;
        Image headerStatusBg, headerStatusBorder;
        Text headerStatusText;

        // Cột trái
        RawImage visualImage, chartImage;
        Text visualFallback, leftInfoText;
        Image leftBannerBg, leftBannerBorder;
        Text leftBannerText;

        // Cột phải
        RectTransform presetStrip;
        Text presetHintText;
        Button savePresetBtn;
        Text readoutVal1, readoutVal2, readoutVal3;
        Text readoutLbl2, readoutLbl3;
        Text statusText, resultText, runText;
        Button runButton, resetButton, lidQuickButton;
        Text lidQuickText;
        Image progressFill;
        Image rightBannerBg, rightBannerBorder;
        Text rightBannerText;

        const int MaxCustomPresets = 2;
        float nextPoll;
        string runResult = "";
        int activePresetIndex = -1;
        readonly List<Row> rows = new List<Row>();
        readonly List<float> initialValues = new List<float>();
        readonly List<MachinePreset> activePresets = new List<MachinePreset>();
        readonly List<Button> presetChips = new List<Button>();

        [Serializable]
        class SavedPresetEntry
        {
            public string name;
            public float[] values;
        }

        [Serializable]
        class SavedPresetStore
        {
            public List<SavedPresetEntry> items = new List<SavedPresetEntry>();
            public int nextSlot;
        }

        class Row
        {
            public MachineParam p;
            public Slider slider;
            public Text value, note;
            public Button[] chips;
            public Button unitBtn;
            public Text unitBtnText;
            public bool altActive;
        }

        public static MachinePanel Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("Bảng điều khiển máy");
            Instance = go.AddComponent<MachinePanel>();
            return Instance;
        }

        /// <summary>Mở bảng cho một máy. owner là thành phần của máy: máy bị hủy (khu bị gỡ) thì bảng tự đóng.</summary>
        public static void Open(IMachinePanel machine)
        {
            if (machine == null) return;
            var s = machine.PanelSpec();
            if (s == null) return;
            Ensure().Show(s, machine as UnityEngine.Object);
        }

        /// <summary>Mở bảng phục vụ kiểm thử tự động ngay cả khi đặt biến môi trường KHKT_TEST.</summary>
        public static void OpenForTest(IMachinePanel machine)
        {
            ForceEnableForTest = true;
            Open(machine);
        }

        void Awake()
        {
            UiTheme.Changed += OnThemeChanged;
        }

        void OnDestroy()
        {
            UiTheme.Changed -= OnThemeChanged;
            if (open) ToolPreview.Release();
            if (captured) UiBridge.SetCapture(ref captured, false);
            if (Instance == this) Instance = null;
        }

        void OnThemeChanged()
        {
            if (open && spec != null)
            {
                string savedRunResult = runResult;
                DestroyRoot();
                Show(spec, owner, preserveResult: true, savedResult: savedRunResult);
            }
            else if (root != null)
            {
                DestroyRoot();
            }
        }

        void DestroyRoot()
        {
            if (root != null)
            {
                Destroy(root);
                root = null;
            }
            animFrame = null;
            panel = null;
            content = null;
            group = null;
        }

        void Build()
        {
            builtTheme = UiTheme.Current;
            builtFontScale = UiTheme.FontScaleMode;
            canvas = HostCanvas(transform, 45);
            root = new GameObject("Bảng máy", typeof(RectTransform));
            var rrt = (RectTransform)root.transform;
            rrt.SetParent(canvas.transform, false);
            rrt.anchorMin = Vector2.zero;
            rrt.anchorMax = Vector2.one;
            rrt.offsetMin = rrt.offsetMax = Vector2.zero;
            group = root.AddComponent<CanvasGroup>();

            Backdrop(root.transform, "Nền mờ", Close);
            animFrame = Rect(root.transform, "Khung hiệu ứng", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PanelW, PanelH));
            var img = Window(animFrame, "Khung bảng", new Vector2(PanelW, PanelH), Vector2.zero);
            panel = (RectTransform)img.transform;
            ApplyWindowScale();
            content = Rect(panel, "Nội dung", new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(PanelW, PanelH));
            root.SetActive(false);
        }

        void ApplyWindowScale()
        {
            if (panel == null) return;
            var target = UiTheme.PanelSize(new Vector2(PanelW, PanelH));
            float scale = Mathf.Min(target.x / PanelW, target.y / PanelH);
            panel.localScale = new Vector3(scale, scale, 1f);
        }

        void Show(MachineSpec s, UnityEngine.Object machine, bool preserveResult = false, string savedResult = "")
        {
            if (root != null && (builtTheme != UiTheme.Current || builtFontScale != UiTheme.FontScaleMode)) DestroyRoot();
            if (root == null) Build();
            ApplyWindowScale();

            spec = s;
            owner = machine;
            runResult = preserveResult ? savedResult : "";
            for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
            rows.Clear();
            initialValues.Clear();
            activePresets.Clear();
            presetChips.Clear();
            activePresetIndex = -1;

            foreach (var p in s.parameters)
                initialValues.Add(p.get != null && IsFinite(p.get()) ? p.get() : p.min);

            LoadPresetsForSpec(s);

            var c = content;
            BuildHeader(c, s);
            BuildBody(c, s);
            BuildFooter(c, s);

            open = true;
            root.SetActive(true);
            root.transform.SetAsLastSibling();
            UiBridge.SetCapture(ref captured, true);
            group.alpha = 0f;
            UiMotion.Popup(this, animFrame != null ? animFrame : panel, group, 0.18f);

            RefreshVisualImmediate();
            nextPoll = 0f;
            Poll();
        }

        void BuildHeader(Transform c, MachineSpec s)
        {
            // Biểu tượng máy vẽ hình học (không phụ thuộc ký tự Unicode ngoài bảng mã BeVietnamPro)
            var iconBg = Fill(c, "Biểu tượng máy", new Vector2(24f, -13f), new Vector2(36f, 36f), AccentSoft, null, true);
            var ring = Rect(iconBg.transform, "Vòng ngoài", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20f, 20f)).gameObject.AddComponent<Image>();
            ring.sprite = Circle;
            ring.color = Accent;
            ring.raycastTarget = false;
            var core = Rect(iconBg.transform, "Lõi", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(8f, 8f)).gameObject.AddComponent<Image>();
            core.sprite = SolidCircle;
            core.color = Accent;
            core.raycastTarget = false;

            string codeText = string.IsNullOrEmpty(s.code) ? "MÁY THÍ NGHIỆM" : s.code;
            Label(c, "Mã bảng", codeText, 11, Accent2, true, TextAnchor.UpperLeft, new Vector2(72f, -8f), new Vector2(980f, 16f));
            var titleLbl = Label(c, "Tiêu đề", s.title, 20, Ink, true, TextAnchor.UpperLeft, new Vector2(72f, -24f), new Vector2(980f, 34f));
            titleLbl.verticalOverflow = VerticalWrapMode.Truncate;

            // Cụm bên phải đầu bảng: chip chế độ + thẻ trạng thái + nút Đóng
            string closeLabel = PadInput.UsingTouch ? "Đóng ×" : "Đóng  Esc";
            MakeButton(c, "Đóng", closeLabel, new Vector2(-148f, -9f), new Vector2(124f, 44f), Close, false, 15, new Vector2(1, 1));

            headerStatusBg = Fill(c, "Thẻ trạng thái đầu", new Vector2(-300f, -11f), new Vector2(140f, 40f), GoodSoft, new Vector2(1, 1), true);
            headerStatusBorder = headerStatusBg.transform.Find("Viền")?.GetComponent<Image>();
            headerStatusText = Label(headerStatusBg.transform, "Chữ trạng thái đầu", "Sẵn sàng", 13, Good, true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(140f, 40f));

            practiceChip = MakeChip(c, "Chế độ Thực hành", "Thực hành", new Vector2(-430f, -9f), new Vector2(116f, 44f), () => SetMode(false), !guidedMode, 14);
            SetAnchorTopRight(practiceChip.transform);
            guidedChip = MakeChip(c, "Chế độ Hướng dẫn", "Hướng dẫn", new Vector2(-554f, -9f), new Vector2(116f, 44f), () => SetMode(true), guidedMode, 14);
            SetAnchorTopRight(guidedChip.transform);

            Fill(c, "Vạch đầu", new Vector2(0f, -HeaderH), new Vector2(PanelW, 1f), Line);
        }

        static void SetAnchorTopRight(Transform t)
        {
            var rt = (RectTransform)t;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        }

        void SetMode(bool guided)
        {
            guidedMode = guided;
            SetChip(guidedChip, guidedMode);
            SetChip(practiceChip, !guidedMode);
            foreach (var row in rows)
            {
                if (row.note != null) row.note.gameObject.SetActive(guidedMode || !string.IsNullOrEmpty(row.note.text));
            }
            Poll();
        }

        void BuildBody(Transform c, MachineSpec s)
        {
            // ----- Cột trái (46%) có RectMask2D chống tràn -----
            var leftRt = Rect(c, "Cột trái", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -HeaderH), new Vector2(LeftColW, BodyH));
            leftRt.gameObject.AddComponent<RectMask2D>();

            const float visSize = 520f;
            float visX = (LeftColW - visSize) * 0.5f;
            var visOuter = Fill(leftRt, "Khung viền máy", new Vector2(visX, -24f), new Vector2(visSize, visSize), Line, null, true);
            var visWell = Fill(visOuter.transform, "Khung hình máy", new Vector2(1f, -1f), new Vector2(visSize - 2f, visSize - 2f), Well, null, true);

            var vrt = Rect(visWell.transform, "Ảnh mô phỏng", new Vector2(0, 1), new Vector2(0, 1), new Vector2(8f, -8f), new Vector2(visSize - 18f, visSize - 18f));
            visualImage = vrt.gameObject.AddComponent<RawImage>();
            visualImage.color = Color.white;
            visualImage.raycastTarget = false;
            var visFitter = vrt.gameObject.AddComponent<AspectRatioFitter>();
            visFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            visFitter.aspectRatio = 1f;

            visualFallback = Label(visWell.transform, "Chữ dự phòng hình", string.IsNullOrEmpty(s.title) ? s.code : s.title, 16, Dim, true, TextAnchor.MiddleCenter,
                                   new Vector2(24f, -24f), new Vector2(visSize - 50f, visSize - 50f));

            float innerLeftW = LeftColW - Pad * 2f;
            leftBannerBg = Banner(leftRt, "Dải trạng thái trái", "Sẵn sàng vận hành", Good, GoodSoft, new Vector2(Pad, -562f), new Vector2(innerLeftW, 44f));
            leftBannerText = leftBannerBg.transform.Find("Nội dung")?.GetComponent<Text>();
            leftBannerBorder = leftBannerBg.transform.Find("Viền")?.GetComponent<Image>();

            // Ô dưới cùng cột trái: đồ thị (nếu spec.chart != null) hoặc tóm tắt thiết bị
            var subWell = Fill(leftRt, "Khung dưới trái", new Vector2(Pad, -620f), new Vector2(innerLeftW, 166f), Well, null, true);
            chartImage = null;
            leftInfoText = null;
            if (s.chart != null)
            {
                Label(subWell.transform, "Nhãn đồ thị", "ĐỒ THỊ / PHỔ ĐO", 11, Muted, true, TextAnchor.UpperLeft, new Vector2(14f, -10f), new Vector2(innerLeftW - 28f, 16f));
                var crt = Rect(subWell.transform, "Đồ thị", new Vector2(0, 1), new Vector2(0, 1), new Vector2(14f, -28f), new Vector2(innerLeftW - 28f, 128f));
                chartImage = crt.gameObject.AddComponent<RawImage>();
                chartImage.raycastTarget = false;
                chartImage.color = Color.white;
                var fitter = crt.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fitter.aspectRatio = 2f;
            }
            else
            {
                Label(subWell.transform, "Nhãn mô tả", "MÔ TẢ THIẾT BỊ", 11, Muted, true, TextAnchor.UpperLeft, new Vector2(14f, -10f), new Vector2(innerLeftW - 28f, 16f));
                string desc = string.IsNullOrEmpty(s.subtitle) ? "Chỉnh thông số ở cột bên phải rồi bấm nút chạy máy." : s.subtitle;
                leftInfoText = Label(subWell.transform, "Nội dung mô tả", desc, 15, Dim, false, TextAnchor.UpperLeft, new Vector2(14f, -32f), new Vector2(innerLeftW - 28f, 122f));
                leftInfoText.verticalOverflow = VerticalWrapMode.Truncate;
            }

            // Vạch chia cột dọc 1 px
            Fill(c, "Vạch chia cột", new Vector2(LeftColW, -HeaderH), new Vector2(1f, BodyH), Line);

            // ----- Cột phải (54%) có RectMask2D chống tràn -----
            var rightRt = Rect(c, "Cột phải", new Vector2(0, 1), new Vector2(0, 1), new Vector2(RightColX, -HeaderH), new Vector2(RightColW, BodyH));
            rightRt.gameObject.AddComponent<RectMask2D>();
            float innerW = RightColW - Pad * 2f;

            // Dải Chương trình (preset có sẵn + nút Lưu mới vào PlayerPrefs)
            Label(rightRt, "Nhãn chương trình", "CHƯƠNG TRÌNH", 12, Muted, true, TextAnchor.UpperLeft, new Vector2(Pad, -14f), new Vector2(200f, 18f));
            presetHintText = Label(rightRt, "Gợi ý chương trình", "Chọn nhanh hoặc lưu thông số hiện tại", 12, Dim, false, TextAnchor.UpperRight, new Vector2(Pad + 200f, -14f), new Vector2(innerW - 200f, 18f));
            presetHintText.verticalOverflow = VerticalWrapMode.Truncate;

            const float saveBtnW = 122f;
            presetStrip = Rect(rightRt, "Dải chương trình", new Vector2(0, 1), new Vector2(0, 1), new Vector2(Pad, -36f), new Vector2(innerW - saveBtnW - 10f, 44f));
            savePresetBtn = MakeButton(rightRt, "Lưu mới", "+ Lưu mới", new Vector2(Pad + innerW - saveBtnW, -36f), new Vector2(saveBtnW, 44f), SaveCustomPreset, false, 14);
            if (s.parameters.Count == 0) savePresetBtn.interactable = false;
            RebuildPresetStrip();

            Label(rightRt, "Nhãn thông số", "THÔNG SỐ CÀI ĐẶT", 12, Muted, true, TextAnchor.UpperLeft, new Vector2(Pad, -92f), new Vector2(innerW, 18f));
            float y = -114f;
            int pCount = s.parameters.Count;
            float rowH = pCount <= 2 ? 120f : pCount == 3 ? 108f : pCount == 4 ? 92f : Mathf.Max(78f, 368f / pCount);
            foreach (var p in s.parameters)
            {
                rows.Add(p.options != null ? BuildChoice(rightRt, p, y, innerW) : BuildSlider(rightRt, p, y, innerW));
                y -= rowH;
            }
            if (pCount == 0)
            {
                var emptyBox = Fill(rightRt, "Không thông số", new Vector2(Pad, y), new Vector2(innerW, 64f), Well, null, true);
                Label(emptyBox.transform, "Chữ", "Máy này không có thông số cần chỉnh: kiểm tra trạng thái và bấm Chạy máy.", 15, Dim, false, TextAnchor.MiddleLeft,
                      new Vector2(16f, 0f), new Vector2(innerW - 32f, 64f));
            }

            // Đường phân cách trước khu vực số đo
            Fill(rightRt, "Vạch số đo", new Vector2(Pad, -492f), new Vector2(innerW, 1f), Line);
            Label(rightRt, "Nhãn trạng thái", "TRẠNG THÁI & TIẾN ĐỘ", 12, Muted, true, TextAnchor.UpperLeft, new Vector2(Pad, -500f), new Vector2(innerW, 18f));

            // Hàng 3 thẻ số đo GlassKit.Readout
            float cw = (innerW - 24f) / 3f;
            readoutVal1 = Readout(rightRt, "Thẻ trạng thái", "TRẠNG THÁI", "Sẵn sàng", new Vector2(Pad, -520f), new Vector2(cw, 64f), Good);
            readoutVal2 = Readout(rightRt, "Thẻ chỉ số", "CHỈ SỐ", "—", new Vector2(Pad + cw + 12f, -520f), new Vector2(cw, 64f), Accent2);
            readoutVal3 = Readout(rightRt, "Thẻ tiến độ", "TIẾN ĐỘ", "0 %", new Vector2(Pad + (cw + 12f) * 2f, -520f), new Vector2(cw, 64f), Accent);
            ClampReadout(readoutVal1);
            ClampReadout(readoutVal2);
            ClampReadout(readoutVal3);
            readoutLbl2 = readoutVal2.transform.parent.Find("Nhãn")?.GetComponent<Text>();
            readoutLbl3 = readoutVal3.transform.parent.Find("Nhãn")?.GetComponent<Text>();

            // Thanh tiến độ 6 px
            Fill(rightRt, "Rãnh tiến độ", new Vector2(Pad, -592f), new Vector2(innerW, 6f), Wash(0.16f));
            progressFill = Fill(rightRt, "Tiến độ", new Vector2(Pad, -592f), new Vector2(innerW, 6f), Accent);
            progressFill.type = Image.Type.Filled;
            progressFill.fillMethod = Image.FillMethod.Horizontal;
            progressFill.fillAmount = 0f;

            // Ô trạng thái chi tiết + kết quả
            var resBg = Fill(rightRt, "Ô kết quả", new Vector2(Pad, -606f), new Vector2(innerW, 134f), Well, null, true);
            statusText = Label(resBg.transform, "Trạng thái", "", 14, Ink, false, TextAnchor.UpperLeft, new Vector2(14f, -8f), new Vector2(innerW - 28f, 48f));
            statusText.verticalOverflow = VerticalWrapMode.Truncate;

            Fill(resBg.transform, "Vạch ô kết quả", new Vector2(14f, -58f), new Vector2(innerW - 28f, 1f), Line);
            Label(resBg.transform, "Nhãn kết quả", "KẾT QUẢ", 11, Muted, true, TextAnchor.UpperLeft, new Vector2(14f, -62f), new Vector2(160f, 18f));
            resultText = Label(resBg.transform, "Kết quả", "Chưa chạy. Chỉnh thông số rồi bấm nút chạy.", 14, Dim, false, TextAnchor.UpperLeft,
                               new Vector2(14f, -79f), new Vector2(innerW - 28f, 52f));
            resultText.verticalOverflow = VerticalWrapMode.Truncate;

            // Dải gợi ý / cảnh báo an toàn 1 dòng
            rightBannerBg = Banner(rightRt, "Dải gợi ý", "Chỉnh thông số rồi bấm chạy máy.", Warn, WarnSoft, new Vector2(Pad, -748f), new Vector2(innerW, 46f));
            rightBannerText = rightBannerBg.transform.Find("Nội dung")?.GetComponent<Text>();
            rightBannerBorder = rightBannerBg.transform.Find("Viền")?.GetComponent<Image>();
        }

        static void ClampReadout(Text t)
        {
            if (t == null) return;
            t.fontSize = UiTheme.FontScaleMode > 0
                ? Mathf.Clamp(Mathf.RoundToInt(17f * UiTheme.FontScale), 17, 22)
                : 17;
            var sd = t.rectTransform.sizeDelta;
            if (sd.y < 28f) t.rectTransform.sizeDelta = new Vector2(sd.x, 28f);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
        }

        void BuildFooter(Transform c, MachineSpec s)
        {
            float fy = -PanelH + FooterH; // -872
            Fill(c, "Vạch đáy", new Vector2(0f, fy), new Vector2(PanelW, 1f), Line);
            float btnY = fy - (FooterH - 46f) * 0.5f; // -883

            float leftX = 24f;
            lidQuickButton = null;
            lidQuickText = null;

            if (s.advanced != null)
            {
                MakeButton(c, "Vận hành chi tiết", s.advancedLabel ?? "Màn hình vận hành chi tiết",
                           new Vector2(leftX, btnY), new Vector2(260f, 46f),
                           () => { var a = spec?.advanced; Close(); a?.Invoke(); }, false, 15);
                leftX += 272f;
            }
            else
            {
                Row lidRow = FindLidRow();
                if (lidRow != null)
                {
                    lidQuickButton = MakeButton(c, "Thao tác nắp", "Đóng / Mở nắp", new Vector2(leftX, btnY), new Vector2(168f, 46f),
                                                ToggleLidQuick, false, 15);
                    lidQuickText = lidQuickButton.GetComponentInChildren<Text>();
                    leftX += 180f;
                }
            }

            MakeButton(c, "Hỏi trợ lý", "Hỏi trợ lý", new Vector2(leftX, btnY), new Vector2(156f, 46f), AskAssistant, false, 15);

            // Bên phải: [Đặt lại] + MỘT nút chính [Chạy máy]
            runButton = MakeButton(c, "Chạy máy", s.runLabel(), new Vector2(-264f, btnY), new Vector2(240f, 46f), Run, true, 17, new Vector2(1, 1));
            runText = runButton.GetComponentInChildren<Text>();
            resetButton = MakeButton(c, "Đặt lại", "Đặt lại", new Vector2(-416f, btnY), new Vector2(140f, 46f), ResetParams, false, 15, new Vector2(1, 1));
        }

        Row FindLidRow()
        {
            foreach (var r in rows)
            {
                if (r.p.options != null && r.p.label != null && r.p.label.IndexOf("Nắp", StringComparison.OrdinalIgnoreCase) >= 0)
                    return r;
            }
            return null;
        }

        void ToggleLidQuick()
        {
            if (spec != null && spec.busy != null && spec.busy()) return;
            var r = FindLidRow();
            if (r == null || r.p.options == null || r.p.options.Length < 2) return;
            int cur = Mathf.RoundToInt(r.p.get());
            int next = (cur + 1) % r.p.options.Length;
            OnChoice(r, next);
        }

        void ResetParams()
        {
            if (spec == null) return;
            if (spec.busy != null && spec.busy()) return;
            for (int i = 0; i < rows.Count && i < initialValues.Count; i++)
            {
                var row = rows[i];
                float v = initialValues[i];
                row.p.set?.Invoke(v);
                if (row.slider != null)
                {
                    float actual = row.p.get != null && IsFinite(row.p.get()) ? Mathf.Clamp(row.p.get(), row.p.min, row.p.max) : Mathf.Clamp(v, row.p.min, row.p.max);
                    row.slider.SetValueWithoutNotify(RowSliderDomainValue(row, actual));
                    RefreshRow(row, actual);
                }
                else if (row.chips != null)
                {
                    int idx = Mathf.RoundToInt(v);
                    for (int k = 0; k < row.chips.Length; k++) SetChip(row.chips[k], k == idx);
                    if (row.note != null) row.note.text = row.p.note != null ? row.p.note(idx) : "";
                }
            }
            activePresetIndex = -1;
            RefreshPresetChips();
            if (presetHintText != null) presetHintText.text = "Đã đặt lại thông số mặc định";
            runResult = "";
            if (resultText != null)
            {
                resultText.text = "Đã đặt lại thông số mặc định. Bấm nút chạy để vận hành.";
                resultText.color = Dim;
            }
            RefreshVisualImmediate();
            nextPoll = 0f;
            Poll();
        }

        void AskAssistant()
        {
            string title = spec != null && !string.IsNullOrEmpty(spec.title) ? spec.title : "Máy thí nghiệm";
            bool busy = spec != null && spec.busy != null && spec.busy();
            string stateLabel = busy ? "Đang chạy" : (headerStatusText != null && !string.IsNullOrEmpty(headerStatusText.text) ? headerStatusText.text : "Sẵn sàng");

            var paramParts = new List<string>(4);
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                if (r.p.get == null) continue;
                if (r.p.options != null && r.p.options.Length > 0)
                {
                    int idx = Mathf.Clamp(Mathf.RoundToInt(r.p.get()), 0, r.p.options.Length - 1);
                    paramParts.Add($"{r.p.label}: {r.p.options[idx]}");
                }
                else
                {
                    string valText = r.value != null && !string.IsNullOrEmpty(r.value.text) ? r.value.text : FormatParam(r.p, r.p.get());
                    paramParts.Add($"{r.p.label}: {valText}");
                }
            }
            string paramSummary = string.Join(", ", paramParts);
            string st = spec != null && spec.status != null ? spec.status().Replace('\n', ' ').Trim() : "";

            string topic = string.IsNullOrEmpty(paramSummary)
                ? $"{title} ({stateLabel})"
                : $"{title} ({stateLabel} · {paramSummary})";
            string question = string.IsNullOrEmpty(paramSummary)
                ? $"Hướng dẫn vận hành và lưu ý an toàn cho {title}" + (string.IsNullOrEmpty(st) ? "." : $" (trạng thái: {st}).")
                : $"Hướng dẫn sử dụng và ý nghĩa thông số hiện tại ({paramSummary}) cho {title}" + (string.IsNullOrEmpty(st) ? "." : $" (trạng thái: {st}).");

            float beforeMsgTime = LabMessages.MessageTime;
            if (!RobotChatUI.AskAbout(topic, question))
            {
                if (Mathf.Approximately(LabMessages.MessageTime, beforeMsgTime) && Time.time - LabMessages.MessageTime >= 2f)
                    LabMessages.Say("Trợ lý robot hiện chưa sẵn sàng hoặc đang tạm khóa.", LabMessages.Tone.Warn);
            }
        }

        Row BuildSlider(Transform parent, MachineParam p, float y, float innerW)
        {
            var row = new Row { p = p };
            bool hasAlt = p.toAlt != null && p.fromAlt != null && !string.IsNullOrEmpty(p.altUnit);
            float valW = hasAlt ? 380f : 240f;
            Label(parent, "Tên thông số", p.label, 16, Ink, true, TextAnchor.UpperLeft, new Vector2(Pad, y), new Vector2(innerW - valW - 8f, 26f));
            row.value = Label(parent, "Giá trị", "", 17, Accent2, true, TextAnchor.UpperRight, new Vector2(Pad + innerW - valW, y), new Vector2(valW, 28f));
            row.value.horizontalOverflow = HorizontalWrapMode.Wrap;
            row.value.verticalOverflow = VerticalWrapMode.Truncate;

            float v0 = p.get != null && IsFinite(p.get()) ? Mathf.Clamp(p.get(), p.min, p.max) : p.min;
            float unitBtnW = hasAlt ? 116f : 0f;
            float sliderW = hasAlt ? (innerW - unitBtnW - 10f) : innerW;
            row.slider = MakeSlider(parent, "Thanh " + p.label, new Vector2(Pad - 6f, y - 24f), new Vector2(sliderW + 12f, 44f), p.min, p.max, v0, v => OnSlider(row, v));
            if (hasAlt)
            {
                row.unitBtn = MakeChip(parent, "Đơn vị " + p.label, "Đặt " + p.altUnit,
                    new Vector2(Pad + innerW - unitBtnW, y - 24f), new Vector2(unitBtnW, 44f),
                    () => ToggleRowUnit(row), false, 14);
                row.unitBtnText = row.unitBtn.GetComponentInChildren<Text>();
            }
            row.note = Label(parent, "Ghi chú", "", 13, Dim, false, TextAnchor.UpperLeft, new Vector2(Pad, y - 68f), new Vector2(innerW, 22f));
            row.note.horizontalOverflow = HorizontalWrapMode.Wrap;
            row.note.verticalOverflow = VerticalWrapMode.Truncate;
            RefreshRow(row, v0);
            return row;
        }

        Row BuildChoice(Transform parent, MachineParam p, float y, float innerW)
        {
            var row = new Row { p = p, chips = new Button[p.options.Length] };
            Label(parent, "Tên thông số", p.label, 16, Ink, true, TextAnchor.UpperLeft, new Vector2(Pad, y), new Vector2(innerW, 26f));
            float w = Mathf.Min(260f, (innerW - (p.options.Length - 1) * 10f) / p.options.Length);
            int cur = Mathf.RoundToInt(p.get());
            for (int i = 0; i < p.options.Length; i++)
            {
                int k = i;
                row.chips[i] = MakeChip(parent, "Mức " + p.options[i], p.options[i], new Vector2(Pad + i * (w + 10f), y - 24f), new Vector2(w, 44f), () => OnChoice(row, k), i == cur, 14);
            }
            row.note = Label(parent, "Ghi chú", "", 13, Dim, false, TextAnchor.UpperLeft, new Vector2(Pad, y - 70f), new Vector2(innerW, 22f));
            row.note.horizontalOverflow = HorizontalWrapMode.Wrap;
            row.note.verticalOverflow = VerticalWrapMode.Truncate;
            row.note.text = p.note != null ? p.note(cur) : "";
            return row;
        }

        void ToggleRowUnit(Row row)
        {
            if (row == null || row.p.toAlt == null || row.p.fromAlt == null || row.slider == null) return;
            if (spec != null && spec.busy != null && spec.busy()) return;

            var p = row.p;
            float curPrimary = p.get != null && IsFinite(p.get()) ? Mathf.Clamp(p.get(), p.min, p.max) : p.min;
            row.altActive = !row.altActive;
            SetChip(row.unitBtn, row.altActive);
            if (row.unitBtnText != null)
                row.unitBtnText.text = row.altActive ? "Đặt " + p.unit : "Đặt " + p.altUnit;

            if (row.altActive)
            {
                float lo = Mathf.Min(p.altMin, p.altMax);
                float hi = Mathf.Max(p.altMin, p.altMax);
                row.slider.minValue = lo;
                row.slider.maxValue = hi;
                float altVal = p.toAlt(curPrimary);
                row.slider.SetValueWithoutNotify(IsFinite(altVal) ? Mathf.Clamp(altVal, lo, hi) : lo);
            }
            else
            {
                row.slider.minValue = p.min;
                row.slider.maxValue = p.max;
                row.slider.SetValueWithoutNotify(curPrimary);
            }
            RefreshRow(row, curPrimary);
        }

        void OnSlider(Row row, float raw)
        {
            var p = row.p;
            if (spec != null && spec.busy != null && spec.busy())
            {
                float cur = p.get != null && IsFinite(p.get()) ? Mathf.Clamp(p.get(), p.min, p.max) : p.min;
                row.slider.SetValueWithoutNotify(RowSliderDomainValue(row, cur));
                return;
            }
            if (!IsFinite(raw)) return;

            if (row.altActive && p.fromAlt != null)
            {
                float lo = Mathf.Min(p.altMin, p.altMax);
                float hi = Mathf.Max(p.altMin, p.altMax);
                float altV = p.altStep > 0f ? lo + Mathf.Round((raw - lo) / p.altStep) * p.altStep : raw;
                altV = Mathf.Clamp(altV, lo, hi);
                float prim = p.fromAlt(altV);
                if (IsFinite(prim))
                    p.set(Mathf.Clamp(prim, p.min, p.max));
            }
            else
            {
                float v = p.step > 0f ? p.min + Mathf.Round((raw - p.min) / p.step) * p.step : raw;
                v = Mathf.Clamp(v, p.min, p.max);
                if (IsFinite(v)) p.set(v);
            }

            float actual = p.get != null && IsFinite(p.get()) ? Mathf.Clamp(p.get(), p.min, p.max) : p.min;
            activePresetIndex = -1;
            RefreshPresetChips();
            RefreshRow(row, actual);
            nextPoll = 0f;
            Poll();
        }

        void OnChoice(Row row, int index)
        {
            if (spec != null && spec.busy != null && spec.busy()) return;
            row.p.set(index);
            for (int i = 0; i < row.chips.Length; i++) SetChip(row.chips[i], i == index);
            if (row.note != null) row.note.text = row.p.note != null ? row.p.note(index) : "";
            activePresetIndex = -1;
            RefreshPresetChips();
            RefreshVisualImmediate();
            nextPoll = 0f;
            Poll();
        }

        static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

        static float RowSliderDomainValue(Row row, float primaryVal)
        {
            if (row.altActive && row.p.toAlt != null)
            {
                float altVal = row.p.toAlt(primaryVal);
                float lo = Mathf.Min(row.p.altMin, row.p.altMax);
                float hi = Mathf.Max(row.p.altMin, row.p.altMax);
                return IsFinite(altVal) ? Mathf.Clamp(altVal, lo, hi) : lo;
            }
            return Mathf.Clamp(primaryVal, row.p.min, row.p.max);
        }

        void RefreshRow(Row row, float primaryVal)
        {
            var p = row.p;
            float safePrim = IsFinite(primaryVal) ? Mathf.Clamp(primaryVal, p.min, p.max) : p.min;
            if (p.toAlt != null && !string.IsNullOrEmpty(p.altUnit))
            {
                float altVal = p.toAlt(safePrim);
                string altStr = (IsFinite(altVal) ? altVal : 0f).ToString(p.altFormat, Vi) + " " + p.altUnit;
                row.value.text = row.altActive
                    ? $"{altStr} · ≈ {FormatParam(p, safePrim)}"
                    : $"{FormatParam(p, safePrim)} · ≈ {altStr}";
            }
            else
            {
                row.value.text = FormatParam(p, safePrim);
            }
            row.note.text = p.note != null ? p.note(safePrim) : $"Khoảng chỉnh: {p.min.ToString(p.format, Vi)} – {p.max.ToString(p.format, Vi)} {p.unit}";
        }

        static string FormatParam(MachineParam p, float v) =>
            (IsFinite(v) ? v : 0f).ToString(p.format, Vi) + (string.IsNullOrEmpty(p.unit) ? "" : " " + p.unit);

        // ------------------------------------------------------------------
        // Chương trình cài sẵn & lưu mới vào PlayerPrefs
        // ------------------------------------------------------------------

        void LoadPresetsForSpec(MachineSpec s)
        {
            activePresets.Clear();
            if (s.presets.Count > 0)
            {
                foreach (var pr in s.presets)
                    if (pr != null && !string.IsNullOrEmpty(pr.name)) activePresets.Add(pr);
            }
            else
            {
                AddBuiltInPresetsIfNeeded(s);
            }

            if (s.parameters.Count > 0)
            {
                var store = ReadSavedPresetStore(s);
                foreach (var item in store.items)
                {
                    if (item == null || item.values == null || item.values.Length != s.parameters.Count) continue;
                    bool valid = true;
                    for (int i = 0; i < item.values.Length; i++)
                        if (!IsFinite(item.values[i])) { valid = false; break; }
                    if (!valid) continue;
                    activePresets.Add(new MachinePreset(item.name, item.values) { isCustom = true });
                }
            }
        }

        void AddBuiltInPresetsIfNeeded(MachineSpec s)
        {
            if (s.parameters.Count == 0) return;
            string code = (s.code ?? "").ToUpperInvariant();
            if (code.Contains("NỒI HẤP") && s.parameters.Count >= 3)
            {
                activePresets.Add(new MachinePreset("Chuẩn 121 °C", 121f, 15f, 1f));
                activePresets.Add(new MachinePreset("Nhanh 134 °C", 134f, 5f, 1f));
            }
            else if (code.Contains("TỦ ẤM") && s.parameters.Count >= 2)
            {
                activePresets.Add(new MachinePreset("E. coli 37 °C", 37f, 24f));
                activePresets.Add(new MachinePreset("Nấm men 30 °C", 30f, 48f));
            }
            else if (code.Contains("BỂ ỔN NHIỆT") && s.parameters.Count >= 2)
            {
                activePresets.Add(new MachinePreset("Ủ enzym 37 °C", 37f, 20f));
                activePresets.Add(new MachinePreset("Biến tính 65 °C", 65f, 15f));
            }
            else if (code.Contains("KHỐI GIA NHIỆT") && s.parameters.Count >= 2)
            {
                activePresets.Add(new MachinePreset("Ủ 37 °C", 37f, 15f));
                activePresets.Add(new MachinePreset("Bất hoạt 95 °C", 95f, 5f));
            }
            else if (code.Contains("PCR") && s.parameters.Count >= 3)
            {
                activePresets.Add(new MachinePreset("Chuẩn 55 °C", 55f, 30f, 1f));
                activePresets.Add(new MachinePreset("Tăng nhạy 50 °C", 50f, 35f, 1f));
            }
            else if (code.Contains("VORTEX") && s.parameters.Count >= 3)
            {
                activePresets.Add(new MachinePreset("Trộn nhẹ", 4f, 5f, 0f));
                activePresets.Add(new MachinePreset("Đánh tan", 9f, 10f, 1f));
            }
            else if (code.Contains("NGUỒN ĐIỆN DI") && s.parameters.Count >= 2)
            {
                activePresets.Add(new MachinePreset("Chạy 100 V", 100f, 30f));
                activePresets.Add(new MachinePreset("Chậm 70 V", 70f, 45f));
            }
            else if (code.Contains("LÒ VI SÓNG") && s.parameters.Count >= 1)
            {
                activePresets.Add(new MachinePreset("Gel 1,0 %", 1.0f));
                activePresets.Add(new MachinePreset("Gel 2,0 %", 2.0f));
            }
            else
            {
                activePresets.Add(new MachinePreset("Mặc định", initialValues.ToArray()));
            }
        }

        void RebuildPresetStrip()
        {
            if (presetStrip == null) return;
            for (int i = presetStrip.childCount - 1; i >= 0; i--)
                Destroy(presetStrip.GetChild(i).gameObject);
            presetChips.Clear();

            int count = activePresets.Count;
            if (count == 0) return;

            float stripW = presetStrip.sizeDelta.x;
            const float gap = 8f;
            float chipW = Mathf.Min(166f, (stripW - gap * Mathf.Max(0, count - 1)) / count);
            for (int i = 0; i < count; i++)
            {
                int idx = i;
                var pr = activePresets[i];
                var chip = MakeChip(presetStrip, "CT " + pr.name, pr.name,
                    new Vector2(i * (chipW + gap), 0f), new Vector2(chipW, 44f),
                    () => ApplyPreset(idx), i == activePresetIndex, 13);
                var lbl = chip.GetComponentInChildren<Text>();
                if (lbl != null)
                {
                    lbl.horizontalOverflow = HorizontalWrapMode.Wrap;
                    lbl.verticalOverflow = VerticalWrapMode.Truncate;
                }
                presetChips.Add(chip);
            }
        }

        void RefreshPresetChips()
        {
            for (int i = 0; i < presetChips.Count; i++)
                SetChip(presetChips[i], i == activePresetIndex);
        }

        void ApplyPreset(int index)
        {
            if (spec == null || index < 0 || index >= activePresets.Count) return;
            if (spec.busy != null && spec.busy()) return;

            var pr = activePresets[index];
            for (int i = 0; i < rows.Count && pr.values != null && i < pr.values.Length; i++)
            {
                var row = rows[i];
                float val = pr.values[i];
                if (!IsFinite(val)) continue;

                if (row.slider != null)
                {
                    float clamped = Mathf.Clamp(val, row.p.min, row.p.max);
                    row.p.set?.Invoke(clamped);
                    float actual = row.p.get != null && IsFinite(row.p.get())
                        ? Mathf.Clamp(row.p.get(), row.p.min, row.p.max)
                        : clamped;
                    row.slider.SetValueWithoutNotify(RowSliderDomainValue(row, actual));
                    RefreshRow(row, actual);
                }
                else if (row.chips != null)
                {
                    int choiceIdx = Mathf.Clamp(Mathf.RoundToInt(val), 0, row.chips.Length - 1);
                    row.p.set?.Invoke(choiceIdx);
                    for (int k = 0; k < row.chips.Length; k++) SetChip(row.chips[k], k == choiceIdx);
                    if (row.note != null) row.note.text = row.p.note != null ? row.p.note(choiceIdx) : "";
                }
            }

            activePresetIndex = index;
            RefreshPresetChips();
            if (presetHintText != null) presetHintText.text = "Đang dùng: " + pr.name;
            RefreshVisualImmediate();
            nextPoll = 0f;
            Poll();
        }

        void SaveCustomPreset()
        {
            if (spec == null || spec.parameters.Count == 0) return;
            if (spec.busy != null && spec.busy()) return;

            var vals = new float[spec.parameters.Count];
            for (int i = 0; i < spec.parameters.Count; i++)
            {
                var p = spec.parameters[i];
                float v = p.get != null && IsFinite(p.get()) ? p.get() : p.min;
                vals[i] = p.options != null ? Mathf.Round(v) : Mathf.Clamp(v, p.min, p.max);
            }

            var store = ReadSavedPresetStore(spec);
            int savedCustomSlot;
            string savedName;
            if (store.items.Count < MaxCustomPresets)
            {
                savedCustomSlot = store.items.Count;
                savedName = "Tùy chỉnh " + (savedCustomSlot + 1);
                store.items.Add(new SavedPresetEntry { name = savedName, values = vals });
            }
            else
            {
                savedCustomSlot = store.nextSlot % MaxCustomPresets;
                savedName = "Tùy chỉnh " + (savedCustomSlot + 1);
                store.items[savedCustomSlot] = new SavedPresetEntry { name = savedName, values = vals };
                store.nextSlot = (savedCustomSlot + 1) % MaxCustomPresets;
            }

            WriteSavedPresetStore(spec, store);
            LoadPresetsForSpec(spec);

            int builtInCount = activePresets.Count - store.items.Count;
            activePresetIndex = Mathf.Clamp(builtInCount + savedCustomSlot, 0, activePresets.Count - 1);
            RebuildPresetStrip();
            RefreshPresetChips();

            if (presetHintText != null) presetHintText.text = "Đã lưu: " + savedName;
            LabMessages.Say($"Đã lưu chương trình \"{savedName}\" cho {spec.title}.", LabMessages.Tone.Good);
        }

        SavedPresetStore ReadSavedPresetStore(MachineSpec s)
        {
            string raw = PlayerPrefs.GetString(GetPresetPrefsKey(s), "");
            if (!string.IsNullOrEmpty(raw))
            {
                try
                {
                    var parsed = JsonUtility.FromJson<SavedPresetStore>(raw);
                    if (parsed != null && parsed.items != null) return parsed;
                }
                catch { }
            }
            return new SavedPresetStore();
        }

        static void WriteSavedPresetStore(MachineSpec s, SavedPresetStore store)
        {
            try
            {
                PlayerPrefs.SetString(GetPresetPrefsKey(s), JsonUtility.ToJson(store));
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MachinePanel] Không lưu được chương trình vào PlayerPrefs: " + e.Message);
            }
        }

        static string GetPresetPrefsKey(MachineSpec s)
        {
            if (s != null && !string.IsNullOrEmpty(s.presetKey))
                return "KHKT_PRESETS_" + s.presetKey;
            string raw = s == null ? "machine" : $"{s.code}|{s.title}";
            uint hash = 2166136261u;
            for (int i = 0; i < raw.Length; i++)
            {
                hash ^= raw[i];
                hash *= 16777619u;
            }
            return $"KHKT_PRESETS_{hash:X8}";
        }

        void Run()
        {
            if (spec == null || spec.run == null) return;
            if (spec.busy != null && spec.busy()) return;
            string r = null;
            try { r = spec.run(); }
            catch (Exception e) { Debug.LogException(e); r = "Máy báo lỗi: " + e.Message; }
            if (!string.IsNullOrEmpty(r)) runResult = r;
            RefreshVisualImmediate();
            nextPoll = 0f;
            Poll();
        }

        public void Close()
        {
            if (!open) return;
            open = false;
            spec = null;
            owner = null;
            ToolPreview.Release();
            if (root != null) root.SetActive(false);
            UiBridge.SetCapture(ref captured, false);
        }

        static bool IsChatBlockingKeys() =>
            (RobotChatUI.Instance != null && RobotChatUI.Instance.IsOpen) || UiBridge.LastCloseFrame == Time.frameCount;

        void Update()
        {
            if (!open) return;
            // máy bị gỡ cùng khu (đi xa, đổi tầng): đóng bảng
            if (ReferenceEquals(owner, null) == false && owner == null) { Close(); return; }
            var kb = Keyboard.current;
            if (kb != null && !UiBridge.Typing && !IsChatBlockingKeys())
            {
                if (kb.escapeKey.wasPressedThisFrame) { Close(); return; }
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) Run();
            }
            if (spec != null && spec.visual == null && owner is Component comp && comp != null && ToolPreview.ShouldRefresh(0.2f))
                ApplyVisualTexture(ToolPreview.Render(comp.gameObject));

            if (Time.unscaledTime >= nextPoll) Poll();
        }

        void RefreshVisualImmediate()
        {
            if (spec == null || visualImage == null) return;
            Texture tex;
            if (spec.visual != null)
            {
                tex = spec.visual();
            }
            else
            {
                var comp = owner as Component;
                tex = ToolPreview.Render(comp != null ? comp.gameObject : null);
            }
            ApplyVisualTexture(tex);
        }

        void ApplyVisualTexture(Texture tex)
        {
            if (visualImage == null) return;
            visualImage.texture = tex;
            visualImage.enabled = tex != null;
            if (visualFallback != null) visualFallback.gameObject.SetActive(tex == null);
            var fitter = visualImage.GetComponent<AspectRatioFitter>();
            if (fitter != null && tex != null && tex.height > 0)
                fitter.aspectRatio = (float)tex.width / tex.height;
        }

        void Poll()
        {
            nextPoll = Time.unscaledTime + 0.15f;
            if (spec == null) return;

            bool busy = spec.busy != null && spec.busy();
            float prog = spec.progress != null ? Mathf.Clamp01(spec.progress()) : (busy ? Mathf.PingPong(Time.unscaledTime * 0.6f, 1f) : 0f);
            string st = spec.status != null ? spec.status() : "";

            if (spec.visual != null) ApplyVisualTexture(spec.visual());

            if (statusText != null) statusText.text = st;
            if (progressFill != null) progressFill.fillAmount = prog;
            if (runText != null) runText.text = busy ? "Đang chạy…" : spec.runLabel();
            if (runButton != null) runButton.interactable = !busy;
            if (resetButton != null) resetButton.interactable = !busy;
            if (lidQuickButton != null)
            {
                lidQuickButton.interactable = !busy;
                var lr = FindLidRow();
                if (lidQuickText != null && lr != null && lr.p.options != null)
                {
                    int cur = Mathf.RoundToInt(lr.p.get());
                    lidQuickText.text = cur == 0 ? "Đóng nắp" : "Mở nắp";
                }
            }
            if (savePresetBtn != null) savePresetBtn.interactable = !busy && spec.parameters.Count > 0;
            foreach (var ch in presetChips)
                if (ch != null) ch.interactable = !busy;
            foreach (var row in rows)
            {
                if (row.slider != null) row.slider.interactable = !busy;
                if (row.unitBtn != null) row.unitBtn.interactable = !busy;
                if (row.chips != null)
                    foreach (var ch in row.chips)
                        if (ch != null) ch.interactable = !busy;
            }

            string res = spec.result != null ? spec.result() : null;
            if (string.IsNullOrEmpty(res)) res = runResult;
            if (resultText != null && !string.IsNullOrEmpty(res))
            {
                resultText.text = res;
                resultText.color = Ink;
            }

            if (chartImage != null)
            {
                var t = spec.chart != null ? spec.chart() : null;
                chartImage.texture = t;
                chartImage.enabled = t != null;
                var fitter = chartImage.GetComponent<AspectRatioFitter>();
                if (fitter != null && t != null && t.height > 0)
                    fitter.aspectRatio = (float)t.width / t.height;
            }

            UpdateStatusIndicators(busy, prog, st, res);
        }

        void UpdateStatusIndicators(bool busy, float prog, string st, string res)
        {
            string lowerSt = (st ?? "").ToLowerInvariant();
            string lowerRes = (res ?? "").ToLowerInvariant();

            bool hasError = lowerSt.Contains("lệch tải") || lowerSt.Contains("dưới vạch") || lowerRes.Contains("không chạy") || lowerRes.Contains("tự ngắt") || lowerRes.Contains("lỗi");
            bool hasWarn = !hasError && (lowerSt.Contains("nắp: mở") || lowerSt.Contains("chưa có") || lowerSt.Contains("trống") || lowerSt.Contains("chưa nối") || lowerSt.Contains("chưa đun") || lowerSt.Contains("chưa xong") || lowerRes.Contains("chưa đạt"));

            // 1. Thẻ trạng thái trên đầu bảng
            if (headerStatusBg != null && headerStatusText != null)
            {
                Color fg = busy ? Accent : hasError ? Bad : hasWarn ? Warn : Good;
                Color bg = busy ? AccentSoft : hasError ? BadSoft : hasWarn ? WarnSoft : GoodSoft;
                headerStatusBg.color = bg;
                if (headerStatusBorder != null) headerStatusBorder.color = new Color(fg.r, fg.g, fg.b, 0.36f);
                headerStatusText.color = fg;
                headerStatusText.text = busy ? "Đang chạy" : hasError ? "Cảnh báo" : hasWarn ? "Chờ chuẩn bị" : "Sẵn sàng";
            }

            // 2. Dải trạng thái ở cột trái (dưới hình)
            if (leftBannerBg != null && leftBannerText != null)
            {
                Color fg, bg;
                string msg;
                if (busy)
                {
                    fg = Accent; bg = AccentSoft;
                    msg = $"Đang vận hành {spec.title} · {Mathf.RoundToInt(prog * 100f)} %";
                }
                else if (lowerSt.Contains("lệch tải"))
                {
                    fg = Bad; bg = BadSoft;
                    msg = "Mất cân bằng tải rotor — cần đặt ống đối xứng qua tâm.";
                }
                else if (lowerSt.Contains("nắp: mở"))
                {
                    fg = Warn; bg = WarnSoft;
                    msg = "Nắp an toàn đang mở — đóng nắp trước khi quay.";
                }
                else if (lowerSt.Contains("cân bằng"))
                {
                    fg = Good; bg = GoodSoft;
                    msg = "Rotor cân bằng đối xứng · Nắp đã đóng · Sẵn sàng quay.";
                }
                else if (hasError || hasWarn)
                {
                    fg = hasError ? Bad : Warn;
                    bg = hasError ? BadSoft : WarnSoft;
                    msg = FirstLine(st, "Kiểm tra mẫu và điều kiện an toàn trước khi chạy.");
                }
                else
                {
                    fg = Good; bg = GoodSoft;
                    msg = "Thiết bị ổn định · Sẵn sàng chạy theo thông số đã đặt.";
                }
                ApplyBannerStyle(leftBannerBg, leftBannerText, leftBannerBorder, msg, fg, bg);
            }

            // 3. 3 thẻ Readout ở cột phải
            if (readoutVal1 != null)
            {
                readoutVal1.text = busy ? "Đang chạy" : hasError ? "Cần sửa" : hasWarn ? "Chưa đủ" : (prog >= 0.999f ? "Hoàn tất" : "Sẵn sàng");
                readoutVal1.color = busy ? Accent : hasError ? Bad : hasWarn ? Warn : Good;
            }
            if (readoutVal2 != null)
            {
                string metric = ExtractPrimaryMetric(st);
                if (string.IsNullOrEmpty(metric) && spec.parameters.Count > 0 && spec.parameters[0].get != null)
                {
                    var p0 = spec.parameters[0];
                    if (readoutLbl2 != null) readoutLbl2.text = p0.label.ToUpperInvariant();
                    metric = p0.options != null ? p0.options[Mathf.Clamp(Mathf.RoundToInt(p0.get()), 0, p0.options.Length - 1)] : FormatParam(p0, p0.get());
                }
                else if (readoutLbl2 != null)
                {
                    readoutLbl2.text = lowerSt.Contains("×g") ? "LỰC LY TÂM" : "CHỈ SỐ CHÍNH";
                }
                readoutVal2.text = string.IsNullOrEmpty(metric) ? "—" : metric;
                readoutVal2.color = Accent2;
            }
            if (readoutVal3 != null)
            {
                string aux = ExtractAuxMetric(st, busy, prog);
                if (readoutLbl3 != null) readoutLbl3.text = (busy || spec.progress != null) ? "TIẾN ĐỘ" : (lowerSt.Contains("nắp:") ? "KHÓA NẮP" : "THÔNG TIN");
                readoutVal3.text = aux;
                readoutVal3.color = busy ? Accent : hasError ? Bad : Accent;
            }

            // 4. Dải gợi ý 1 dòng ở chân cột phải
            if (rightBannerBg != null && rightBannerText != null)
            {
                Color fg, bg;
                string hint;
                if (busy)
                {
                    fg = Accent; bg = AccentSoft;
                    hint = "Đang chạy chu trình — các thông số được khóa an toàn cho tới khi hoàn tất.";
                }
                else if (hasError)
                {
                    fg = Bad; bg = BadSoft;
                    hint = FirstLine(!string.IsNullOrEmpty(res) ? res : st, "Thông số hoặc tải hiện tại chưa an toàn.");
                }
                else if (hasWarn)
                {
                    fg = Warn; bg = WarnSoft;
                    hint = FirstLine(st, "Chuẩn bị đủ điều kiện vận hành rồi bấm nút chạy.");
                }
                else if (!guidedMode)
                {
                    fg = Good; bg = GoodSoft;
                    hint = "Chế độ Thực hành: tự do điều chỉnh thông số và kiểm chứng kết quả thực tế.";
                }
                else
                {
                    string paramTip = FindActiveNote();
                    fg = Good; bg = GoodSoft;
                    hint = string.IsNullOrEmpty(paramTip)
                        ? "Chỉnh thông số bên trên rồi bấm nút chạy ở góc dưới phải."
                        : paramTip;
                }
                ApplyBannerStyle(rightBannerBg, rightBannerText, rightBannerBorder, hint, fg, bg);
            }
        }

        string FindActiveNote()
        {
            foreach (var r in rows)
            {
                if (r.note != null && !string.IsNullOrEmpty(r.note.text))
                    return r.note.text;
            }
            return "";
        }

        static string FirstLine(string text, string fallback)
        {
            if (string.IsNullOrWhiteSpace(text)) return fallback;
            int nl = text.IndexOf('\n');
            string s = (nl >= 0 ? text.Substring(0, nl) : text).Trim();
            return s.Length > 0 ? s : fallback;
        }

        string ExtractPrimaryMetric(string st)
        {
            if (string.IsNullOrEmpty(st)) return "";
            if (st.Contains("≈"))
            {
                int i = st.IndexOf("≈", StringComparison.Ordinal) + 1;
                int end = st.IndexOf('\n', i);
                int paren = st.IndexOf('(', i);
                if (paren >= 0 && (end < 0 || paren < end)) end = paren;
                string sub = (end >= 0 ? st.Substring(i, end - i) : st.Substring(i)).Trim();
                if (sub.Length > 0 && sub.Length <= 20) return sub;
            }
            string[] lines = st.Split('\n');
            foreach (var raw in lines)
            {
                string line = raw.Trim();
                int colon = line.IndexOf(':');
                if (colon > 0 && colon < line.Length - 1)
                {
                    string val = line.Substring(colon + 1).Trim();
                    int dot = val.IndexOf('·');
                    if (dot > 0) val = val.Substring(0, dot).Trim();
                    if (val.Length > 0 && val.Length <= 22) return val;
                }
            }
            return "";
        }

        string ExtractAuxMetric(string st, bool busy, float prog)
        {
            if (busy || spec.progress != null)
                return $"{Mathf.RoundToInt(prog * 100f)} %";
            if (!string.IsNullOrEmpty(st))
            {
                if (st.Contains("Nắp: ĐÓNG")) return "Nắp: ĐÓNG";
                if (st.Contains("Nắp: MỞ")) return "Nắp: MỞ";
                if (st.Contains("ĐÃ KHÓA")) return "Nắp: ĐÃ KHÓA";
            }
            if (spec.parameters.Count > 1 && spec.parameters[1].get != null)
            {
                var p1 = spec.parameters[1];
                return p1.options != null
                    ? p1.options[Mathf.Clamp(Mathf.RoundToInt(p1.get()), 0, p1.options.Length - 1)]
                    : FormatParam(p1, p1.get());
            }
            return string.IsNullOrEmpty(runResult) ? "Chờ chạy" : "Đã chạy";
        }

        static void ApplyBannerStyle(Image bgImg, Text txt, Image borderImg, string msg, Color fg, Color bg)
        {
            if (bgImg != null) bgImg.color = bg;
            if (borderImg != null) borderImg.color = new Color(fg.r, fg.g, fg.b, 0.30f);
            if (txt != null)
            {
                txt.color = fg;
                txt.text = msg;
            }
        }
    }
}