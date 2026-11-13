using System;
using System.Collections.Generic;
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

        public static MachineParam Slider(string label, string unit, float min, float max, float step, Func<float> get, Action<float> set,
                                          string format = "0", Func<float, string> note = null) =>
            new MachineParam { label = label, unit = unit, min = min, max = max, step = step, get = get, set = set, format = format, note = note };

        public static MachineParam Choice(string label, string[] options, Func<int> get, Action<int> set, Func<float, string> note = null) =>
            new MachineParam { label = label, options = options, get = () => get(), set = v => set(Mathf.RoundToInt(v)), note = note };
    }

    /// <summary>Mô tả bảng điều khiển của một máy: thông số, nút chạy, trạng thái, kết quả.</summary>
    public class MachineSpec
    {
        /// <summary>Mã ngắn in ở đầu bảng, ví dụ "MÁY · PCR".</summary>
        public string code = "MÁY";
        public string title = "", subtitle = "";
        public readonly List<MachineParam> parameters = new List<MachineParam>();
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
        /// <summary>Ảnh đồ thị / phổ của máy (nếu có), hiện trong ô kết quả.</summary>
        public Func<Texture> chart;
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
    /// Bảng điều khiển máy: bấm vào một máy thì hiện bảng lớn ở giữa màn hình (khoảng 60 % bề ngang, không che kín cảnh),
    /// kéo thanh trượt chỉnh thông số rồi bấm "Chạy máy". Cột phải hiện trạng thái, tiến độ, kết quả và đồ thị của máy.
    /// Bấm ra ngoài bảng, nút Đóng hoặc Esc để đóng.
    /// </summary>
    public class MachinePanel : MonoBehaviour
    {
        public static MachinePanel Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance.open;
        /// <summary>Bài kiểm thử tự động gọi thẳng Use() của máy: tắt bảng để không chặn kịch bản.</summary>
        public static bool Enabled = string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("KHKT_TEST"));

        const float PanelW = 1180f, PanelH = 720f;
        const float LeftW = 640f;

        bool open, captured;
        Canvas canvas;
        GameObject root;
        RectTransform panel, content;
        CanvasGroup group;
        MachineSpec spec;
        UnityEngine.Object owner;
        Text subtitle, statusText, resultText, runText;
        Button runButton;
        Image progressFill;
        RawImage chartImage;
        float nextPoll;
        string runResult = "";
        readonly List<Row> rows = new List<Row>();

        class Row
        {
            public MachineParam p;
            public Slider slider;
            public Text value, note;
            public Button[] chips;
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

        void OnDestroy()
        {
            if (captured) UiBridge.SetCapture(ref captured, false);
            if (Instance == this) Instance = null;
        }

        void Build()
        {
            canvas = HostCanvas(transform, 45);
            root = new GameObject("Bảng máy", typeof(RectTransform));
            var rrt = (RectTransform)root.transform;
            rrt.SetParent(canvas.transform, false);
            rrt.anchorMin = Vector2.zero;
            rrt.anchorMax = Vector2.one;
            rrt.offsetMin = rrt.offsetMax = Vector2.zero;
            group = root.AddComponent<CanvasGroup>();

            Backdrop(root.transform, "Nền mờ", Close);
            var img = Window(root.transform, "Khung bảng", new Vector2(PanelW, PanelH), new Vector2(0, 8));
            panel = (RectTransform)img.transform;
            content = Rect(panel, "Nội dung", new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(PanelW, PanelH));
            root.SetActive(false);
        }

        void Show(MachineSpec s, UnityEngine.Object machine)
        {
            if (root == null) Build();
            spec = s;
            owner = machine;
            runResult = "";
            for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
            rows.Clear();

            var c = content;
            subtitle = Header(c, s.code, s.title, s.subtitle, PanelW - 230f);
            string closeLabel = PadInput.UsingTouch ? "Đóng ✕" : "Đóng  Esc";
            MakeButton(c, "Đóng", closeLabel, new Vector2(-150, -24), new Vector2(124, 52), Close, false, 15, new Vector2(1, 1));

            // ----- Cột trái: thông số -----
            Label(c, "Nhãn thông số", "THÔNG SỐ CÀI ĐẶT", 13, Muted, true, TextAnchor.UpperLeft, new Vector2(30, -122), new Vector2(300, 18));
            float y = -148f;
            float rowH = s.parameters.Count > 4 ? 88f : 100f;
            foreach (var p in s.parameters)
            {
                rows.Add(p.options != null ? BuildChoice(c, p, y) : BuildSlider(c, p, y));
                y -= rowH;
            }
            if (s.parameters.Count == 0)
                Label(c, "Không thông số", "Máy này không có thông số cần chỉnh: bấm Chạy máy.", 16, Dim, false, TextAnchor.UpperLeft, new Vector2(30, y), new Vector2(LeftW, 50));

            // ----- Cột phải: trạng thái, tiến độ, kết quả -----
            float rx = 30f + LeftW + 30f, rw = PanelW - rx - 30f;
            Fill(c, "Vạch chia cột", new Vector2(rx - 15f, -122f), new Vector2(1.5f, PanelH - 122f - 104f), Line);
            Label(c, "Nhãn trạng thái", "TRẠNG THÁI MÁY", 13, Muted, true, TextAnchor.UpperLeft, new Vector2(rx, -122), new Vector2(rw, 18));
            var statusBg = Fill(c, "Ô trạng thái", new Vector2(rx, -146), new Vector2(rw, 112), Well, null, true);
            statusText = Label(statusBg.transform, "Trạng thái", "", 15, Ink, false, TextAnchor.UpperLeft, new Vector2(14, -10), new Vector2(rw - 28, 92));
            statusText.verticalOverflow = VerticalWrapMode.Truncate;
            Fill(c, "Rãnh tiến độ", new Vector2(rx, -266), new Vector2(rw, 6), new Color(0.45f, 0.70f, 1f, 0.18f));
            progressFill = Fill(c, "Tiến độ", new Vector2(rx, -266), new Vector2(rw, 6), Accent);
            progressFill.type = Image.Type.Filled;
            progressFill.fillMethod = Image.FillMethod.Horizontal;
            progressFill.fillAmount = 0f;

            Label(c, "Nhãn kết quả", "KẾT QUẢ", 13, Muted, true, TextAnchor.UpperLeft, new Vector2(rx, -288), new Vector2(rw, 18));
            float resTop = -312f, resH = PanelH - 312f - 104f;
            var resBg = Fill(c, "Ô kết quả", new Vector2(rx, resTop), new Vector2(rw, resH), Well, null, true);
            float textTop = -10f;
            chartImage = null;
            if (s.chart != null)
            {
                float ch = Mathf.Min(150f, resH * 0.52f);
                var crt = Rect(resBg.transform, "Đồ thị", new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -10), new Vector2(rw - 24, ch));
                chartImage = crt.gameObject.AddComponent<RawImage>();
                chartImage.raycastTarget = false;
                chartImage.color = new Color(1, 1, 1, 0.96f);
                textTop = -18f - ch;
            }
            resultText = Label(resBg.transform, "Kết quả", "Chưa chạy. Chỉnh thông số rồi bấm nút chạy.", 15, Dim, false, TextAnchor.UpperLeft,
                               new Vector2(14, textTop), new Vector2(rw - 28, resH + textTop - 10f));
            resultText.verticalOverflow = VerticalWrapMode.Truncate;

            // ----- Hàng nút dưới cùng -----
            Rule(c, new Vector2(30, -PanelH + 96), PanelW - 60);
            runButton = MakeButton(c, "Chạy máy", s.runLabel(), new Vector2(-330, -PanelH + 80), new Vector2(300, 62), Run, true, 20, new Vector2(1, 1));
            runText = runButton.GetComponentInChildren<Text>();
            if (s.advanced != null)
                MakeButton(c, "Vận hành chi tiết", s.advancedLabel ?? "Màn hình vận hành chi tiết", new Vector2(30, -PanelH + 80), new Vector2(330, 62),
                           () => { var a = spec?.advanced; Close(); a?.Invoke(); }, false, 16);
            string hint = PadInput.UsingTouch ? "Kéo thanh trượt để chỉnh · chạm ra ngoài bảng để đóng" : "Kéo thanh trượt để chỉnh · Enter: chạy · Esc: đóng";
            Label(c, "Gợi ý", hint, 14, Muted, false, TextAnchor.MiddleLeft, new Vector2(s.advanced != null ? 380 : 30, -PanelH + 62), new Vector2(430, 26));

            open = true;
            root.SetActive(true);
            root.transform.SetAsLastSibling();
            UiBridge.SetCapture(ref captured, true);
            group.alpha = 0f;
            UiMotion.Popup(this, panel, group, 0.18f);
            nextPoll = 0f;
            Poll();
        }

        Row BuildSlider(Transform c, MachineParam p, float y)
        {
            var row = new Row { p = p };
            Label(c, "Tên thông số", p.label, 17, Ink, true, TextAnchor.UpperLeft, new Vector2(30, y), new Vector2(LeftW - 190, 24));
            row.value = Label(c, "Giá trị", "", 20, Accent2, true, TextAnchor.UpperRight, new Vector2(30 + LeftW - 190, y + 2), new Vector2(190, 26));
            float v0 = Mathf.Clamp(p.get(), p.min, p.max);
            row.slider = MakeSlider(c, "Thanh " + p.label, new Vector2(24, y - 26), new Vector2(LeftW + 12, 44), p.min, p.max, v0, v => OnSlider(row, v));
            row.note = Label(c, "Ghi chú", "", 13, Dim, false, TextAnchor.UpperLeft, new Vector2(30, y - 68), new Vector2(LeftW, 18));
            row.note.horizontalOverflow = HorizontalWrapMode.Overflow;
            RefreshRow(row, v0);
            return row;
        }

        Row BuildChoice(Transform c, MachineParam p, float y)
        {
            var row = new Row { p = p, chips = new Button[p.options.Length] };
            Label(c, "Tên thông số", p.label, 17, Ink, true, TextAnchor.UpperLeft, new Vector2(30, y), new Vector2(LeftW, 24));
            float w = Mathf.Min(200f, (LeftW - (p.options.Length - 1) * 10f) / p.options.Length);
            int cur = Mathf.RoundToInt(p.get());
            for (int i = 0; i < p.options.Length; i++)
            {
                int k = i;
                row.chips[i] = MakeChip(c, "Mức " + p.options[i], p.options[i], new Vector2(30 + i * (w + 10f), y - 28), new Vector2(w, 46), () => OnChoice(row, k), i == cur);
            }
            row.note = Label(c, "Ghi chú", "", 13, Dim, false, TextAnchor.UpperLeft, new Vector2(30, y - 78), new Vector2(LeftW, 18));
            row.note.horizontalOverflow = HorizontalWrapMode.Overflow;
            row.note.text = p.note != null ? p.note(cur) : "";
            return row;
        }

        void OnSlider(Row row, float raw)
        {
            if (spec != null && spec.busy != null && spec.busy()) { row.slider.SetValueWithoutNotify(Mathf.Clamp(row.p.get(), row.p.min, row.p.max)); return; }
            var p = row.p;
            float v = p.step > 0f ? p.min + Mathf.Round((raw - p.min) / p.step) * p.step : raw;
            v = Mathf.Clamp(v, p.min, p.max);
            p.set(v);
            RefreshRow(row, v);
        }

        void OnChoice(Row row, int index)
        {
            if (spec != null && spec.busy != null && spec.busy()) return;
            row.p.set(index);
            for (int i = 0; i < row.chips.Length; i++) SetChip(row.chips[i], i == index);
            row.note.text = row.p.note != null ? row.p.note(index) : "";
        }

        static readonly System.Globalization.CultureInfo Vi = System.Globalization.CultureInfo.GetCultureInfo("vi-VN");

        void RefreshRow(Row row, float v)
        {
            var p = row.p;
            row.value.text = v.ToString(p.format, Vi) + (string.IsNullOrEmpty(p.unit) ? "" : " " + p.unit);
            row.note.text = p.note != null ? p.note(v) : $"Khoảng chỉnh: {p.min.ToString(p.format, Vi)} – {p.max.ToString(p.format, Vi)} {p.unit}";
        }

        void Run()
        {
            if (spec == null || spec.run == null) return;
            if (spec.busy != null && spec.busy()) return;
            string r = null;
            try { r = spec.run(); }
            catch (Exception e) { Debug.LogException(e); r = "Máy báo lỗi: " + e.Message; }
            if (!string.IsNullOrEmpty(r)) runResult = r;
            nextPoll = 0f;
            Poll();
        }

        public void Close()
        {
            if (!open) return;
            open = false;
            spec = null;
            owner = null;
            if (root != null) root.SetActive(false);
            UiBridge.SetCapture(ref captured, false);
        }

        void Update()
        {
            if (!open) return;
            // máy bị gỡ cùng khu (đi xa, đổi tầng): đóng bảng
            if (ReferenceEquals(owner, null) == false && owner == null) { Close(); return; }
            var kb = Keyboard.current;
            if (kb != null && !UiBridge.Typing)
            {
                if (kb.escapeKey.wasPressedThisFrame) { Close(); return; }
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) Run();
            }
            if (Time.unscaledTime >= nextPoll) Poll();
        }

        void Poll()
        {
            nextPoll = Time.unscaledTime + 0.15f;
            if (spec == null) return;
            bool busy = spec.busy != null && spec.busy();
            if (statusText != null && spec.status != null) statusText.text = spec.status();
            if (progressFill != null)
                progressFill.fillAmount = spec.progress != null ? Mathf.Clamp01(spec.progress()) : (busy ? Mathf.PingPong(Time.unscaledTime * 0.6f, 1f) : 0f);
            if (runText != null) runText.text = busy ? "Đang chạy…" : spec.runLabel();
            if (runButton != null) runButton.interactable = !busy;
            foreach (var row in rows)
                if (row.slider != null) row.slider.interactable = !busy;

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
            }
        }
    }
}
