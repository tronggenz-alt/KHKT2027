using System.Collections.Generic;
using Khkt.Environment;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static Khkt.Demo.GlassKit;

namespace Khkt.Demo
{
    /// <summary>
    /// Màn hình giới thiệu và menu (Esc): chọn Thí nghiệm tự do, Chuẩn độ (tự chọn chất cần chuẩn, dung dịch chuẩn,
    /// chỉ thị, nồng độ) hoặc Khám phá phòng lab. Chuẩn độ là tự nguyện: chế độ tự do vẫn có sẵn bộ chuẩn độ.
    /// </summary>
    public class IntroScreen : MonoBehaviour
    {
        public ModernUI ui;
        public LabProcedure procedure;
        public DesktopViewer viewer;
        public bool showOnStart = true;

        public bool IsOpen { get; private set; }

        GameObject root, home, setup;
        Text continueText, setupNote;
        bool captured, started;
        string analyte = "HCl", titrant = "NaOH", indicator = "pp";
        float analyteC;
        readonly Dictionary<string, List<(Button b, string v)>> groups = new Dictionary<string, List<(Button, string)>>();

        void Start()
        {
            UiBridge.EnsureInteractive(ui.Canvas);
            var glass = Glass(ui.glassShader != null ? ui.glassShader : Shader.Find("Khkt/KinhMo"));
            var dimRt = Rect(ui.Canvas.transform, "Màn hình giới thiệu", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4000, 3000));
            var dim = dimRt.gameObject.AddComponent<Image>();
            dim.color = new Color(0.02f, 0.05f, 0.09f, 0.55f);
            root = dimRt.gameObject;

            // ----- Trang chính -----
            var hImg = Panel(dimRt, "Trang chính", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(1180, 700), glass, 1.1f);
            home = hImg.gameObject;
            var h = home.transform;
            Label(h, "Nhãn", "KHKT · TRƯỜNG THPT", 15, Accent, true, TextAnchor.UpperLeft, new Vector2(60, -48), new Vector2(600, 24));
            Label(h, "Tên", "Phòng thí nghiệm Hóa học ảo", 46, Ink, true, TextAnchor.UpperLeft, new Vector2(56, -76), new Vector2(1000, 64));
            Label(h, "Mô tả", "Thực hành an toàn với 720 hóa chất và 80 dụng cụ trong phòng lab 300 m². Mọi phản ứng được tính bằng bộ máy hóa học "
                                 + "(pH, kết tủa, khí, oxi hóa - khử, màu); robot AI chú thích hiện tượng và giúp xử lý sự cố.", 18, Dim, false, TextAnchor.UpperLeft,
                  new Vector2(60, -150), new Vector2(1060, 60));
            Mode(h, 0, "Thí nghiệm tự do", "Tự lấy hóa chất trong kho, tự trộn, đun, đo. Bộ chuẩn độ vẫn bày sẵn nếu muốn làm.", "Bắt đầu", StartFree, true);
            Mode(h, 1, "Chuẩn độ", "Tự chọn chất cần chuẩn, dung dịch chuẩn, chỉ thị, nồng độ ẩn hay biết trước. Có chấm kết quả.", "Chọn bài", ShowSetup, false);
            Mode(h, 2, "Khám phá phòng lab", "Đi một vòng: bàn thí nghiệm, khu thiết bị phân tích, tủ hút, phòng hóa chất 27 kệ.", "Đi tham quan", StartExplore, false);
            Label(h, "Phím", "<b>Rê chuột</b> nhìn   ·   <b>WASD</b> đi   ·   <b>Chuột trái</b> cầm, dùng   ·   <b>B</b> tra cứu hóa chất   ·   <b>T</b> trợ lý AI   ·   <b>1–9</b> các khu   ·   <b>H</b> trợ giúp   ·   <b>Esc</b> menu",
                  15, Dim, false, TextAnchor.MiddleCenter, new Vector2(0, -630), new Vector2(1180, 30));
            continueText = MakeButton(h, "Tiếp tục", "Tiếp tục", new Vector2(-190, -40), new Vector2(150, 40), Close, false, 15, new Vector2(1, 1)).GetComponentInChildren<Text>();
            continueText.transform.parent.gameObject.SetActive(false);

            // ----- Chọn bài chuẩn độ -----
            var sImg = Panel(dimRt, "Chọn bài chuẩn độ", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980, 640), glass, 1.1f);
            setup = sImg.gameObject;
            var s = setup.transform;
            Label(s, "Tên", "Bài chuẩn độ của bạn", 32, Ink, true, TextAnchor.UpperLeft, new Vector2(48, -36), new Vector2(800, 44));
            Label(s, "Mô tả", "Chọn chất cần xác định nồng độ, dung dịch chuẩn trên buret và chất chỉ thị. Pipet lấy 25,00 ml mẫu.", 16, Dim, false, TextAnchor.UpperLeft,
                  new Vector2(50, -86), new Vector2(880, 24));
            Chips(s, "analyte", "Chất cần chuẩn (trong cốc)", -128, new[] { ("HCl", "HCl"), ("H₂SO₄", "H2SO4"), ("HNO₃", "HNO3"), ("CH₃COOH", "CH3COOH"), ("H₂C₂O₄", "H2C2O4"), ("NaOH", "NaOH"), ("KOH", "KOH"), ("NH₃", "NH3"), ("FeSO₄", "FeSO4") },
                  v => { analyte = v; AutoPick(); });
            Chips(s, "titrant", "Dung dịch chuẩn (trên buret)", -218, new[] { ("NaOH 0,1 M", "NaOH"), ("HCl 0,1 M", "HCl"), ("KMnO₄ 0,02 M", "KMnO4") }, v => { titrant = v; AutoPick(); });
            Chips(s, "indicator", "Chất chỉ thị", -308, new[] { ("Phenolphtalein", "pp"), ("Metyl da cam", "mo"), ("Bromothymol xanh", "btb") }, v => { indicator = v; Refresh(); });
            Chips(s, "conc", "Nồng độ mẫu", -398, new[] { ("Ẩn (ngẫu nhiên)", "0"), ("0,05 M", "0.05"), ("0,1 M", "0.1"), ("0,2 M", "0.2") },
                  v => { analyteC = float.Parse(v, System.Globalization.CultureInfo.InvariantCulture); Refresh(); });
            setupNote = Label(s, "Ghi chú", "", 15, Warn, false, TextAnchor.UpperLeft, new Vector2(50, -490), new Vector2(880, 44));
            MakeButton(s, "Bắt đầu", "Bắt đầu chuẩn độ", new Vector2(50, -560), new Vector2(260, 48), StartTitration, true, 17);
            MakeButton(s, "Quay lại", "Quay lại", new Vector2(326, -560), new Vector2(150, 48), () => { setup.SetActive(false); home.SetActive(true); }, false, 16);
            setup.SetActive(false);
            Refresh();

            if (showOnStart && !Application.isBatchMode) Open(); else root.SetActive(false);
        }

        void Mode(Transform p, int i, string title, string desc, string button, System.Action act, bool primary)
        {
            var glassLight = Glass(ui.glassShader != null ? ui.glassShader : Shader.Find("Khkt/KinhMo"), light: true);
            var card = Panel(p, "Thẻ " + title, new Vector2(0, 1), new Vector2(0, 1), new Vector2(60 + i * 360, -240), new Vector2(340, 340), glassLight, 1.4f);
            var bar = Rect(card.transform, "Vạch màu", new Vector2(0, 1), new Vector2(0, 1), new Vector2(26, -28), new Vector2(46, 6));
            var bi = bar.gameObject.AddComponent<Image>();
            bi.sprite = Solid;
            bi.type = Image.Type.Sliced;
            bi.pixelsPerUnitMultiplier = 10;
            bi.color = i == 0 ? Good : i == 1 ? Accent : Warn;
            Label(card.transform, "Tên chế độ", title, 26, Ink, true, TextAnchor.UpperLeft, new Vector2(26, -48), new Vector2(290, 70));
            Label(card.transform, "Mô tả chế độ", desc, 16, Dim, false, TextAnchor.UpperLeft, new Vector2(26, -128), new Vector2(290, 120));
            MakeButton(card.transform, "Nút " + title, button, new Vector2(26, -270), new Vector2(288, 46), act, primary, 17);
        }

        void Chips(Transform p, string key, string title, float y, (string label, string value)[] opts, System.Action<string> pick)
        {
            Label(p, "Nhóm " + key, title, 15, Dim, true, TextAnchor.UpperLeft, new Vector2(50, y), new Vector2(600, 22));
            var list = new List<(Button, string)>();
            float x = 50;
            foreach (var (label, value) in opts)
            {
                string v = value;
                float w = Mathf.Max(90, label.Length * 9.5f + 34);
                var b = MakeButton(p, "Lựa chọn", label, new Vector2(x, y - 28), new Vector2(w, 40), () => pick(v), false, 15);
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
                foreach (var (b, v) in list)
                    b.GetComponent<Image>().color = v == value ? new Color(Accent.r, Accent.g, Accent.b, 0.85f) : new Color(1, 1, 1, 0.13f);
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
            setupNote.text = note + (analyteC <= 0 ? (note.Length > 0 ? "\n" : "") + "Nồng độ mẫu ẩn: tính ra rồi so với đáp án ở bảng kết quả." : "");
        }

        public void Open()
        {
            IsOpen = true;
            root.SetActive(true);
            root.transform.SetAsLastSibling();
            home.SetActive(true);
            setup.SetActive(false);
            continueText.transform.parent.gameObject.SetActive(started);
            UiBridge.SetCapture(ref captured, true);
        }

        public void Close()
        {
            IsOpen = false;
            root.SetActive(false);
            UiBridge.SetCapture(ref captured, false);
            started = true;
        }

        void ShowSetup()
        {
            home.SetActive(false);
            setup.SetActive(true);
        }

        void StartFree()
        {
            procedure.Stop();
            Close();
            viewer.GoTo(2);
            LabMessages.Say("Thí nghiệm tự do: nhấn B để tìm hóa chất, T để hỏi robot", LabMessages.Tone.Good);
        }

        void StartExplore()
        {
            procedure.Stop();
            Close();
            viewer.GoTo(9);
            LabMessages.Say("Nhấn 1–9 để tới từng khu, WASD để đi bộ", LabMessages.Tone.Info);
        }

        void StartTitration()
        {
            procedure.Begin(LabProcedure.Config.Make(analyte, titrant, indicator, analyteC));
            Close();
            viewer.GoTo(1);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;
            if (IsOpen) { if (started) Close(); return; }
            if (DesktopViewer.UiCapture == 0 && UiBridge.LastCloseFrame != Time.frameCount) Open();
        }
    }
}
