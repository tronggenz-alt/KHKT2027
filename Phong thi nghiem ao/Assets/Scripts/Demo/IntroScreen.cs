using System.Collections;
using System.Collections.Generic;
using Khkt.Environment;
using Khkt.Environment.Tang2;
using Khkt.Menu;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static Khkt.Demo.GlassKit;

namespace Khkt.Demo
{
    /// <summary>
    /// Màn hình giới thiệu, chọn chế độ và cài đặt hệ thống VERA Lab HD (Giai đoạn 5):
    ///   • Cập nhật thương hiệu toàn diện: VERA Lab Hóa - Sinh 2 tầng, 720 hóa chất, 108 dụng cụ, 14 máy.
    ///   • Hướng dẫn cảm ứng chuyên biệt cho iPad (loại bỏ phím chuột WASD, Esc khi chạy touch).
    ///   • Bổ sung tùy chọn Cài đặt: Âm thanh giao diện, Giảm chuyển động (Reduced Motion), Giảm độ trong suốt kính.
    ///   • 100% nút bấm đạt chuẩn Apple Touch Target (>= 44x44 pt).
    ///   • Tích hợp vào thẳng phòng lab từ Menu chính (GameSession.EnterLabPending).
    /// </summary>
    public class IntroScreen : MonoBehaviour
    {
        public ModernUI ui;
        public LabProcedure procedure;
        public DesktopViewer viewer;
        public bool showOnStart = true;

        public bool IsOpen { get; private set; }

        GameObject root, home, setup, settings;
        Text continueText, setupNote, aiSaveStatus, guideText;
        Text soundStatusText, motionStatusText, transStatusText;
        InputField aiUrlInput;
        bool captured, started;
        string analyte = "HCl", titrant = "NaOH", indicator = "pp";
        float analyteC;
        readonly Dictionary<string, List<(Button b, string v)>> groups = new Dictionary<string, List<(Button, string)>>();

        void Start()
        {
            UiBridge.EnsureInteractive(ui.Canvas);
            var glass = Glass(ui.glassShader != null ? ui.glassShader : Shader.Find("Khkt/KinhMo"));
            var dimRt = Rect(ui.Canvas.transform, "Bảng tạm dừng", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4000, 3000));
            var dim = dimRt.gameObject.AddComponent<Image>();
            dim.color = new Color(0.02f, 0.05f, 0.09f, 0.55f);
            root = dimRt.gameObject;

            // ----- Trang chính -----
            var hImg = Panel(dimRt, "Trang chính", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(1200, 720), glass, 1.1f);
            home = hImg.gameObject;
            var h = home.transform;
            Label(h, "Nhãn", "KHKT · DỰ ÁN NGHIÊN CỨU KHOA HỌC KỸ THUẬT QUỐC GIA", 14, Accent, true, TextAnchor.UpperLeft, new Vector2(60, -42), new Vector2(700, 24));
            Label(h, "Tên", "VERA Lab — Phòng Thí nghiệm Hóa học & Sinh học Ảo", 38, Ink, true, TextAnchor.UpperLeft, new Vector2(56, -68), new Vector2(1080, 54));
            Label(h, "Mô tả", "Thực hành khoa học an toàn với 720 hóa chất, 108 dụng cụ sinh học và 14 máy phân tích trong tòa nhà 2 tầng 600 m². Mọi phản ứng hóa - sinh được mô phỏng toán lý chính xác; robot AI hỗ trợ thời gian thực.",
                  16, Dim, false, TextAnchor.UpperLeft, new Vector2(60, -128), new Vector2(1080, 50));

            Mode(h, 0, "Thí nghiệm tự do", "Tự lấy hóa chất trong kho, pha chế, đun, đo. Bộ chuẩn độ sẵn sàng.", "Bắt đầu", StartFree, true);
            Mode(h, 1, "Chuẩn độ", "Tự chọn mẫu, dung dịch chuẩn, chỉ thị, nồng độ ẩn hay biết trước.", "Chọn bài", ShowSetup, false);
            Mode(h, 2, "Khám phá phòng lab", "Tham quan 13 khu: phòng thí nghiệm, tủ hút, kho hóa chất, sinh học tầng 2.", "Tham quan", StartExplore, false);

            guideText = Label(h, "Phím", GetControlGuidanceText(), 15, Dim, false, TextAnchor.MiddleCenter, new Vector2(0, -640), new Vector2(1180, 32));

            MakeButton(h, "Cài đặt", "Cài đặt", new Vector2(-360, -36), new Vector2(160, 54), ShowSettings, false, 15, new Vector2(1, 1));
            continueText = MakeButton(h, "Tiếp tục", "Tiếp tục", new Vector2(-180, -36), new Vector2(160, 54), Close, false, 15, new Vector2(1, 1)).GetComponentInChildren<Text>();
            continueText.transform.parent.gameObject.SetActive(false);

            // ----- Cài đặt hệ thống (iPad & AI) -----
            var setImg = Panel(dimRt, "Cài đặt", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800, 560), glass, 1.1f);
            settings = setImg.gameObject;
            var st = settings.transform;
            Label(st, "Tiêu đề", "Cài đặt & Tùy chọn iPad", 26, Ink, true, TextAnchor.UpperLeft, new Vector2(40, -32), new Vector2(700, 36));

            // Tùy chọn 1: Âm thanh giao diện
            Label(st, "Nhãn Âm thanh", "Âm thanh phản hồi nút bấm:", 16, Dim, true, TextAnchor.UpperLeft, new Vector2(40, -84), new Vector2(440, 26));
            soundStatusText = Label(st, "Trạng thái âm thanh", NutHieuUng.SoundEnabled ? "Đang bật" : "Đã tắt", 15, NutHieuUng.SoundEnabled ? Good : Dim, false, TextAnchor.MiddleLeft, new Vector2(500, -84), new Vector2(120, 26));
            MakeButton(st, "ToggleSound", "Bật / Tắt", new Vector2(-40, -74), new Vector2(140, 48), ToggleSound, false, 15, new Vector2(1, 1));

            // Tùy chọn 2: Giảm chuyển động
            Label(st, "Nhãn Chuyển động", "Giảm chuyển động (Reduced Motion):", 16, Dim, true, TextAnchor.UpperLeft, new Vector2(40, -144), new Vector2(440, 26));
            motionStatusText = Label(st, "Trạng thái motion", ReducedMotion.IsEnabled ? "Đang bật" : "Đã tắt", 15, ReducedMotion.IsEnabled ? Good : Dim, false, TextAnchor.MiddleLeft, new Vector2(500, -144), new Vector2(120, 26));
            MakeButton(st, "ToggleMotion", "Bật / Tắt", new Vector2(-40, -134), new Vector2(140, 48), ToggleMotion, false, 15, new Vector2(1, 1));

            // Tùy chọn 3: Giảm trong suốt
            Label(st, "Nhãn Kính mờ", "Giảm trong suốt (nền đặc chống lóa):", 16, Dim, true, TextAnchor.UpperLeft, new Vector2(40, -204), new Vector2(440, 26));
            transStatusText = Label(st, "Trạng thái kính", GlassKit.ReducedTransparency ? "Đang bật" : "Đã tắt", 15, GlassKit.ReducedTransparency ? Good : Dim, false, TextAnchor.MiddleLeft, new Vector2(500, -204), new Vector2(120, 26));
            MakeButton(st, "ToggleTrans", "Bật / Tắt", new Vector2(-40, -194), new Vector2(140, 48), ToggleTransparency, false, 15, new Vector2(1, 1));

            // Tùy chọn 4: Địa chỉ máy chủ AI
            Label(st, "Nhãn AI", "Địa chỉ máy chủ AI (trợ lý & robot):", 16, Dim, true, TextAnchor.UpperLeft, new Vector2(40, -264), new Vector2(700, 24));
            aiUrlInput = MakeInput(st, "Ô nhập AI URL", "http://127.0.0.1:8000", new Vector2(40, -298), new Vector2(540, 48));
            MakeButton(st, "Lưu AI", "Lưu", new Vector2(600, -298), new Vector2(140, 48), SaveSettings, true, 16);
            aiSaveStatus = Label(st, "Trạng thái AI", "", 14, Good, false, TextAnchor.UpperLeft, new Vector2(40, -354), new Vector2(700, 24));

            MakeButton(st, "Đóng cài đặt", "Quay lại", new Vector2(-40, -480), new Vector2(160, 54), CloseSettings, false, 16, new Vector2(1, 1));
            settings.SetActive(false);

            // ----- Bảng chọn bài chuẩn độ -----
            var sImg = Panel(dimRt, "Chọn bài", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100, 680), glass, 1.1f);
            setup = sImg.gameObject;
            var s = setup.transform;
            Label(s, "Tiêu đề", "Chọn bài chuẩn độ", 28, Ink, true, TextAnchor.UpperLeft, new Vector2(40, -32), new Vector2(500, 36));
            Label(s, "Gợi ý", "Dung dịch chuẩn và chỉ thị sẽ tự động đổi theo chất cần chuẩn, hoặc tự chỉnh theo ý muốn.", 15, Dim, false,
                  TextAnchor.UpperLeft, new Vector2(40, -70), new Vector2(960, 24));

            Row(s, "Chất cần chuẩn", -106, "analyte", new[] {
                ("HCl", "Axit clohiđric (mạnh)"),
                ("CH3COOH", "Axit axetic (yếu)"),
                ("H2SO4", "Axit sunfuric (2 nấc)"),
                ("H2C2O4", "Axit oxalic (2 nấc)"),
                ("NaOH", "Natri hiđroxit (mạnh)"),
                ("NH3", "Amoniac (yếu)"),
                ("FeSO4", "Sắt(II) sunfat (oxi hóa - khử)")
            }, a => { analyte = a; AutoPick(); });

            Row(s, "Dung dịch chuẩn (buret)", -216, "titrant", new[] {
                ("NaOH", "NaOH 0,1 M"),
                ("HCl", "HCl 0,1 M"),
                ("KMnO4", "KMnO₄ 0,02 M (tự chỉ thị)")
            }, t => { titrant = t; Refresh(); });

            Row(s, "Chỉ thị màu", -326, "indicator", new[] {
                ("pp", "Phenolphtalein (pH 8,2 – 10,0)"),
                ("mo", "Metyl da cam (pH 3,1 – 4,4)")
            }, i => { indicator = i; Refresh(); });

            Row(s, "Nồng độ mẫu", -436, "conc", new[] {
                ("0.1", "0,100 M (biết trước)"),
                ("0.05", "0,050 M (biết trước)"),
                ("0.2", "0,200 M (biết trước)"),
                ("0", "Ẩn — học sinh tự xác định")
            }, c => { analyteC = float.Parse(c, System.Globalization.CultureInfo.InvariantCulture); Refresh(); });

            setupNote = Label(s, "Ghi chú bài", "", 15, Accent, false, TextAnchor.UpperLeft, new Vector2(40, -538), new Vector2(1000, 50));
            MakeButton(s, "Quay lại", "Quay lại", new Vector2(40, -600), new Vector2(160, 54), ShowHome, false, 16);
            MakeButton(s, "Bắt đầu bài", "Vào thí nghiệm", new Vector2(-40, -600), new Vector2(200, 54), StartTitration, true, 16, new Vector2(1, 1));
            setup.SetActive(false);

            Refresh();
            if (showOnStart) Open();
            else root.SetActive(false);
        }

        string GetControlGuidanceText()
        {
            if (Khkt.Input.PadInput.UsingTouch)
            {
                return "Cảm ứng iPad: Vuốt nửa trái để di chuyển  ·  Vuốt nửa phải xoay góc nhìn  ·  Chạm nút tròn TAY để thao tác  ·  Menu ☰ ở góc trên";
            }
            return "<b>Rê chuột</b> nhìn   ·   <b>WASD</b> đi   ·   <b>Chuột trái</b> cầm, dùng   ·   <b>B</b> tra cứu hóa chất   ·   <b>T</b> trợ lý AI   ·   <b>1–9</b> các khu   ·   <b>H</b> trợ giúp   ·   <b>Esc</b> menu";
        }

        void ToggleSound()
        {
            NutHieuUng.SoundEnabled = !NutHieuUng.SoundEnabled;
            if (soundStatusText != null)
            {
                soundStatusText.text = NutHieuUng.SoundEnabled ? "Đang bật" : "Đã tắt";
                soundStatusText.color = NutHieuUng.SoundEnabled ? Good : Dim;
            }
        }

        void ToggleMotion()
        {
            ReducedMotion.IsEnabled = !ReducedMotion.IsEnabled;
            if (motionStatusText != null)
            {
                motionStatusText.text = ReducedMotion.IsEnabled ? "Đang bật" : "Đã tắt";
                motionStatusText.color = ReducedMotion.IsEnabled ? Good : Dim;
            }
        }

        void ToggleTransparency()
        {
            GlassKit.ReducedTransparency = !GlassKit.ReducedTransparency;
            if (transStatusText != null)
            {
                transStatusText.text = GlassKit.ReducedTransparency ? "Đang bật" : "Đã tắt";
                transStatusText.color = GlassKit.ReducedTransparency ? Good : Dim;
            }
        }

        void Mode(Transform p, int col, string title, string desc, string action, System.Action onClick, bool primary)
        {
            float x = 60 + col * 366;
            var box = Panel(p, "Cột " + col, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -210), new Vector2(346, 390),
                            Glass(ui.glassShader != null ? ui.glassShader : Shader.Find("Khkt/KinhMo"), light: true), 1.3f);
            var b = box.transform;
            Label(b, "Số", "0" + (col + 1), 14, Accent, true, TextAnchor.UpperLeft, new Vector2(24, -22), new Vector2(60, 20));
            Label(b, "Tên", title, 22, Ink, true, TextAnchor.UpperLeft, new Vector2(24, -48), new Vector2(296, 30));
            Label(b, "Mô tả", desc, 15, Dim, false, TextAnchor.UpperLeft, new Vector2(24, -88), new Vector2(296, 200));
            MakeButton(b, "Nút", action, new Vector2(24, -316), new Vector2(298, 54), onClick, primary, 16);
        }

        void Row(Transform p, string label, float y, string key, (string val, string text)[] items, System.Action<string> onPick)
        {
            Label(p, "Nhãn " + key, label, 16, Dim, true, TextAnchor.UpperLeft, new Vector2(40, y), new Vector2(900, 22));
            float x = 40;
            var list = new List<(Button, string)>();
            foreach (var (val, text) in items)
            {
                string v = val;
                float w = Mathf.Max(Width(Label(p, "Tạm", text, 14, Ink, false, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero), text) + 32, 100);
                Destroy(p.Find("Tạm").gameObject);
                var b = MakeButton(p, "Nút " + val, text, new Vector2(x, y - 28), new Vector2(w, 48), () => onPick(v), false, 14);
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
            root.SetActive(false);
            bool fromMenu = GameSession.EnterLabPending;
            GameSession.EnterLabPending = false;
            if (fromMenu)
            {
                StartCoroutine(EnterLab());
            }
            else if (showOnStart && !Application.isBatchMode)
            {
                Open();
            }
        }

        IEnumerator EnterLab()
        {
            yield return null;
            procedure.Stop();
            viewer.GoTo(2);
            string msg = Khkt.Input.PadInput.UsingTouch
                ? "Thí nghiệm tự do: chạm Menu hoặc các nút cảm ứng để thao tác"
                : "Thí nghiệm tự do: nhấn B để tìm hóa chất, T để hỏi robot, H để xem trợ giúp";
            LabMessages.Say(msg, LabMessages.Tone.Info);
        }

        public void Open()
        {
            IsOpen = true;
            root.SetActive(true);
            root.transform.SetAsLastSibling();
            home.SetActive(true);
            setup.SetActive(false);
            if (settings != null) settings.SetActive(false);
            if (guideText != null) guideText.text = GetControlGuidanceText();
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

        void ShowSettings()
        {
            home.SetActive(false);
            setup.SetActive(false);
            settings.SetActive(true);
            if (aiUrlInput != null)
                aiUrlInput.text = PlayerPrefs.GetString(AiClient.ServerUrlPrefKey, AiClient.Instance != null ? AiClient.Instance.serverUrl : "http://127.0.0.1:8000");
            if (aiSaveStatus != null) aiSaveStatus.text = "";
            if (soundStatusText != null)
            {
                soundStatusText.text = NutHieuUng.SoundEnabled ? "Đang bật" : "Đã tắt";
                soundStatusText.color = NutHieuUng.SoundEnabled ? Good : Dim;
            }
            if (motionStatusText != null)
            {
                motionStatusText.text = ReducedMotion.IsEnabled ? "Đang bật" : "Đã tắt";
                motionStatusText.color = ReducedMotion.IsEnabled ? Good : Dim;
            }
            if (transStatusText != null)
            {
                transStatusText.text = GlassKit.ReducedTransparency ? "Đang bật" : "Đã tắt";
                transStatusText.color = GlassKit.ReducedTransparency ? Good : Dim;
            }
        }

        void CloseSettings()
        {
            settings.SetActive(false);
            home.SetActive(true);
        }

        void SaveSettings()
        {
            if (aiUrlInput == null) return;
            string url = aiUrlInput.text.Trim();
            if (string.IsNullOrEmpty(url)) url = "http://127.0.0.1:8000";
            if (AiClient.Instance != null) AiClient.Instance.SetServerUrl(url);
            else
            {
                PlayerPrefs.SetString(AiClient.ServerUrlPrefKey, url);
                PlayerPrefs.Save();
            }
            if (aiSaveStatus != null) aiSaveStatus.text = "✓ Đã lưu máy chủ: " + url;
        }

        void ShowHome()
        {
            setup.SetActive(false);
            home.SetActive(true);
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
            string msg = Khkt.Input.PadInput.UsingTouch
                ? "Thí nghiệm tự do: chạm Menu hoặc các nút cảm ứng để thao tác"
                : "Thí nghiệm tự do: nhấn B để tìm hóa chất, T để hỏi robot";
            LabMessages.Say(msg, LabMessages.Tone.Good);
        }

        void StartExplore()
        {
            procedure.Stop();
            Close();
            viewer.GoTo(9);
            string msg = Khkt.Input.PadInput.UsingTouch
                ? "Chạm thanh trạm trên cùng để chuyển khu thí nghiệm nhanh"
                : "Nhấn 1–9 để tới từng khu, WASD để đi bộ";
            LabMessages.Say(msg, LabMessages.Tone.Info);
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
            if (IsOpen) { Close(); return; }
            if (DesktopViewer.UiCapture == 0 && UiBridge.LastCloseFrame != Time.frameCount) Open();
        }
    }
}
