using System.Collections.Generic;
using System.Text;
using Khkt.Environment;
using Khkt.Interaction;
using Khkt.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static Khkt.Demo.GlassKit;

namespace Khkt.Demo
{
    /// <summary>
    /// Khung trò chuyện với robot trợ lý (phím T). Có máy chủ AI: gửi câu hỏi kèm ngữ cảnh thí nghiệm hiện tại
    /// (đang cầm gì, các bình gần đó chứa gì, pH, nhiệt độ, hiện tượng vừa xảy ra, bước chuẩn độ) và nhận lời
    /// đáp kèm hành động cho robot. Không có máy chủ: robot vẫn hiểu các lệnh thường dùng (tìm, mang, dọn, bật
    /// tủ hút, tắt lửa, rửa mắt, giải thích hiện tượng).
    /// </summary>
    public class RobotChatUI : MonoBehaviour
    {
        public ModernUI ui;
        public HandController hand;
        public LabProcedure procedure;
        public Transform viewer;

        public bool IsOpen { get; private set; }

        GameObject panel;
        InputField input;
        Text log, status;
        CanvasGroup bubbleGroup;
        Text bubbleText;
        RectTransform bubble;
        float bubbleTime = -100;
        readonly List<string> lines = new List<string>();
        readonly List<string> history = new List<string>();
        bool captured, waiting;

        void Start()
        {
            UiBridge.EnsureInteractive(ui.Canvas);
            var glass = Glass(ui.glassShader != null ? ui.glassShader : Shader.Find("Khkt/KinhMo"));
            var img = Panel(ui.Canvas.transform, "Trợ lý AI", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-20, 24), new Vector2(520, 560), glass, 1.3f);
            img.raycastTarget = true;
            panel = img.gameObject;
            var p = panel.transform;
            Label(p, "Tiêu đề", "Robot trợ lý", 22, Ink, true, TextAnchor.UpperLeft, new Vector2(24, -18), new Vector2(300, 30));
            status = Label(p, "Trạng thái", "", 13, Dim, false, TextAnchor.UpperLeft, new Vector2(26, -50), new Vector2(470, 20));
            MakeButton(p, "Đóng", "Đóng  T", new Vector2(-110, -18), new Vector2(90, 32), Close, false, 14, new Vector2(1, 1));
            log = Label(p, "Hội thoại", "", 15, Ink, false, TextAnchor.LowerLeft, new Vector2(24, -78), new Vector2(472, 330));
            log.verticalOverflow = VerticalWrapMode.Truncate;
            string[] quick = { "Hiện tượng vừa rồi là gì?", "Làm mẫu chuẩn độ", "Thí nghiệm mẫu", "Đo pH bình này", "Lấy NaOH ra bàn", "Dọn bàn giúp mình" };
            for (int i = 0; i < quick.Length; i++)
            {
                string q = quick[i];
                MakeButton(p, "Gợi ý", q, new Vector2(24 + (i % 3) * 160, -418 - (i / 3) * 38), new Vector2(152, 32), () => Send(q), false, 13);
            }
            input = MakeInput(p, "Ô nhập", "Hỏi hoặc ra lệnh cho robot...", new Vector2(24, -500), new Vector2(380, 42), 16);
            MakeButton(p, "Gửi", "Gửi", new Vector2(414, -500), new Vector2(82, 42), () => Send(input.text), true);
            panel.SetActive(false);

            // Bong bóng lời robot khi khung trò chuyện đang đóng
            var bimg = Panel(ui.Canvas.transform, "Lời robot", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-20, 100), new Vector2(460, 70), glass, 1.6f);
            bubble = bimg.rectTransform;
            bubbleGroup = bimg.gameObject.AddComponent<CanvasGroup>();
            bubbleGroup.alpha = 0;
            Label(bubble, "Robot", "Robot", 13, Accent, true, TextAnchor.UpperLeft, new Vector2(18, -10), new Vector2(200, 18));
            bubbleText = Label(bubble, "Chữ", "", 15, Ink, false, TextAnchor.UpperLeft, new Vector2(18, -30), new Vector2(424, 60));

            if (LabRobot.Instance != null) LabRobot.Instance.Spoke += OnRobotSpoke;
            Add("Robot", "Chào bạn! Mình làm mẫu chuẩn độ, 10 thí nghiệm mẫu, đo pH, đun, lắc, ly tâm, phân tích mẫu, mang hóa chất, dọn bàn, xử lý sự cố. Nhấn T để trò chuyện.");
        }

        void OnDestroy()
        {
            if (LabRobot.Instance != null) LabRobot.Instance.Spoke -= OnRobotSpoke;
        }

        void OnRobotSpoke(string s)
        {
            Add("Robot", s);
            bubbleText.text = s;
            bubble.sizeDelta = new Vector2(460, 44 + Mathf.Max(20, bubbleText.preferredHeight));
            bubbleTime = Time.time;
        }

        void Add(string who, string text)
        {
            lines.Add(who == "Bạn" ? $"<color=#9FE7FF><b>Bạn:</b></color> {text}" : $"<color=#8CF2B4><b>{who}:</b></color> {text}");
            if (lines.Count > 14) lines.RemoveAt(0);
            log.text = string.Join("\n", lines);
            history.Add($"{who}: {text}");
            if (history.Count > 10) history.RemoveAt(0);
        }

        public void Open()
        {
            IsOpen = true;
            panel.SetActive(true);
            UiBridge.SetCapture(ref captured, true);
            input.ActivateInputField();
        }

        public void Close()
        {
            IsOpen = false;
            panel.SetActive(false);
            UiBridge.SetCapture(ref captured, false);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.tKey.wasPressedThisFrame && !UiBridge.Typing)
                {
                    if (IsOpen) Close();
                    else if (DesktopViewer.UiCapture == 0) Open();
                }
                if (IsOpen && kb.escapeKey.wasPressedThisFrame) Close();
                if (IsOpen && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) && input.text.Trim().Length > 0)
                {
                    Send(input.text);
                    input.ActivateInputField();
                }
            }
            var ai = AiClient.Instance;
            var robot = LabRobot.Instance;
            status.text = (ai != null && ai.Online ? $"<color=#73F29E>●</color> AI đám mây: {ai.Provider} {ai.Model}" : "<color=#FFC759>●</color> Ngoại tuyến: hiểu lệnh cơ bản (chạy server/ai để có AI)")
                          + (robot != null ? "  ·  " + robot.StatusText : "") + (waiting ? "  ·  đang nghĩ..." : "");
            float age = Time.time - bubbleTime;
            bubbleGroup.alpha = IsOpen ? 0 : Mathf.Clamp01(Mathf.Min(age * 5f, (9f - age) / 1.5f));
        }

        public void Send(string text)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0) return;
            input.text = "";
            Add("Bạn", text);
            var ai = AiClient.Instance;
            if (ai != null && ai.Online)
            {
                waiting = true;
                // Ảnh chụp cảnh: AI không nhìn thấy 3D, chỉ biết danh sách vật có mã quanh người dùng
                var scene = RobotWorld.Capture(viewer, hand, procedure);
                ai.Plan(new AiClient.PlanRequest { cau_hoi = text, the_gioi = scene, lich_su = history.ToArray() }, r =>
                {
                    waiting = false;
                    if (!string.IsNullOrEmpty(r.tra_loi)) Add("Robot", r.tra_loi);
                    if (r.ke_hoach != null && r.ke_hoach.Length > 0 && LabRobot.Instance != null)
                        LabRobot.Instance.ExecutePlan(new List<RobotStep>(r.ke_hoach));
                }, err =>
                {
                    waiting = false;
                    Local(text);
                });
            }
            else Local(text);
        }

        /// <summary>Ngữ cảnh thí nghiệm gửi kèm câu hỏi (do mô phỏng tính, AI không tự đặt ra phản ứng).</summary>
        public string Context()
        {
            var sb = new StringBuilder();
            if (procedure != null)
                sb.AppendLine(procedure.Active ? $"Đang làm bài chuẩn độ {procedure.Cfg.analyteShort} bằng {procedure.Cfg.titrantShort}, bước: {procedure.Current}" : "Chế độ thí nghiệm tự do.");
            if (hand != null && hand.Held != null)
            {
                var cb = hand.Held.GetComponent<ChemicalBottle>();
                sb.AppendLine("Đang cầm: " + (cb != null && cb.Entry != null ? cb.Entry.ten : hand.Held.name));
            }
            if (hand != null && hand.TargetContainer != null) sb.AppendLine("Đang trỏ vào: " + Describe(hand.TargetContainer));
            var near = new List<LabContainer>();
            foreach (var c in Khkt.Rendering.ZoneRoot.FindAll<LabContainer>())
                if (c.reactive && c.VolumeMl > 0.05f && viewer != null && Vector3.Distance(c.transform.position, viewer.position) < 2.5f) near.Add(c);
            near.Sort((a, b) => Vector3.Distance(a.transform.position, viewer.position).CompareTo(Vector3.Distance(b.transform.position, viewer.position)));
            for (int i = 0; i < near.Count && i < 6; i++) sb.AppendLine("Bình gần: " + Describe(near[i]));
            var ann = ReactionAnnotator.Instance;
            if (ann != null && ann.History.Count > 0)
            {
                sb.AppendLine("Hiện tượng gần đây:");
                for (int i = Mathf.Max(0, ann.History.Count - 5); i < ann.History.Count; i++) sb.AppendLine("- " + ann.History[i]);
            }
            return sb.ToString();
        }

        static string Describe(LabContainer c) => $"{c.label}: {c.Total.Describe(6)}, pH {c.BulkPh:0.0}, {c.TemperatureC:0} °C" + (c.InFumeHood ? ", trong tủ hút" : "");

        // ------------------------------------------------------------------
        // Lệnh robot
        // ------------------------------------------------------------------

        void Execute(string action, int stt)
        {
            var robot = LabRobot.Instance;
            if (robot == null) return;
            switch (action)
            {
                case "tim_hoa_chat": robot.LocateChemical(stt); break;
                case "mang_hoa_chat": robot.BringChemical(stt); break;
                case "don_ban": robot.CleanBench(); break;
                case "bat_tu_hut": robot.Ventilate(); break;
                case "tat_lua": robot.Extinguish(); break;
                case "rua_mat": robot.GuideEyewash(); break;
                case "mo_tra_cuu": if (ChemSearchUI.Instance != null) { Close(); ChemSearchUI.Instance.Open(); } break;
            }
        }

        /// <summary>Hiểu lệnh tại chỗ khi không có AI.</summary>
        // Thí nghiệm mẫu nhận theo từ khóa (đã bỏ dấu), xét theo thứ tự
        static readonly (string key, string[] words)[] DemoWords =
        {
            ("glucozo_cu_oh2", new[] { "glucoz", "fehling", "cu(oh)2" }),
            ("ly_tam_ket_tua", new[] { "ly tam" }),
            ("phan_tich_nuoc", new[] { "aas", "phan tich nuoc", "kim loai nang", "nuoc thai", "nuoc gieng" }),
            ("do_ph_dung_dich", new[] { "so sanh ph", "ph cua axit", "ph axit bazo" }),
            ("nhan_biet_fe3", new[] { "fe3", "fe 3", "sat iii", "sat (iii)", "kscn", "thioxianat" }),
            ("nhan_biet_cu2", new[] { "cu2", "cu 2", "ion dong", "nhan biet dong", "dong(ii)", "dong ii" }),
            ("nhan_biet_cl", new[] { "clorua", "cl-", "ion cl", "agcl", "agno3" }),
            ("nhan_biet_so4", new[] { "sunfat", "so4", "bacl2", "baso4" }),
            ("chi_thi_axit_bazo", new[] { "phenolphtalein", "trung hoa", "chi thi" }),
            ("kim_loai_axit", new[] { "kem", "zn ", "kim loai tac dung", "kim loai voi axit", "sui bot h2" }),
        };

        static string MatchDemo(string q)
        {
            foreach (var (key, words) in DemoWords)
                foreach (var w in words) if (q.Contains(w)) return key;
            return null;
        }

        /// <summary>Bình người dùng đang trỏ, đang cầm, hoặc bình có dung dịch gần nhất trước mặt.</summary>
        LabContainer FocusContainer()
        {
            if (hand != null && hand.TargetContainer != null) return hand.TargetContainer;
            if (hand != null && hand.Held != null && hand.Held.GetComponent<LabContainer>() != null) return hand.Held.GetComponent<LabContainer>();
            LabContainer best = null;
            float bd = 1.8f;
            foreach (var c in Khkt.Rendering.ZoneRoot.FindAll<LabContainer>())
            {
                if (!c.reactive || c.VolumeMl < 0.3f || c.GetComponent<ChemicalBottle>() != null || viewer == null) continue;
                float d = Vector3.Distance(c.transform.position, viewer.position);
                if (d < bd) { bd = d; best = c; }
            }
            return best;
        }

        /// <summary>Lệnh một kỹ năng trên bình đang chọn (đo pH, đun, lắc...). Trả false nếu không có bình nào.</summary>
        bool OnFocus(string skill, string doing, float amount = 0)
        {
            var c = FocusContainer();
            var robot = LabRobot.Instance;
            if (c == null || robot == null) { Add("Robot", "Bạn trỏ chuột vào bình cần " + doing + " rồi gõ lại nhé."); return false; }
            RobotWorld.Ids["binh_dang_chon"] = c;
            Add("Robot", $"Mình {doing} {c.label}.");
            robot.ExecutePlan(new List<RobotStep> { new RobotStep { ky_nang = skill, doi_tuong = "binh_dang_chon", so_luong = amount } });
            return true;
        }

        void Local(string text)
        {
            string q = ChemicalCatalog.Fold(text);
            var robot = LabRobot.Instance;
            var cat = ChemicalCatalog.Instance;

            bool doIt = Has(q, "lam", "minh hoa", "thuc hien", "demo", "cho xem", "giup", "thu ");
            if (Has(q, "chuan do") && doIt && !Has(q, "huong dan", "cach "))
            {
                Add("Robot", "Mình làm mẫu bài chuẩn độ ở bàn 1 (bài đang chọn trong menu Esc). Bạn quan sát từng bước nhé.");
                robot?.DemoTitration();
                return;
            }
            if (Has(q, "thi nghiem mau", "lam duoc gi", "danh sach thi nghiem", "lam duoc nhung"))
            {
                var sb = new StringBuilder("Thí nghiệm mẫu mình làm được: ");
                for (int i = 0; i < RobotDemos.All.Length; i++) sb.Append(i == 0 ? "" : "; ").Append(RobotDemos.All[i].title);
                Add("Robot", sb.Append(". Gõ tên thí nghiệm, ví dụ \"làm nhận biết Fe3+\".").ToString());
                return;
            }
            string demo = MatchDemo(q);
            if (demo != null && (doIt || Has(q, "nhan biet", "thi nghiem")))
            {
                Add("Robot", $"Mình làm thí nghiệm mẫu: {RobotDemos.Title(demo)}.");
                robot?.RunDemo(demo);
                return;
            }
            if (Has(q, "do ph")) { OnFocus("do_ph", "đo pH"); return; }
            if (Has(q, "do nhiet do", "bao nhieu do")) { OnFocus("do_nhiet_do", "đo nhiệt độ"); return; }
            if (Has(q, "do do dan", "dan dien")) { OnFocus("do_do_dan", "đo độ dẫn điện"); return; }
            if (Has(q, "quy tim", "giay quy")) { OnFocus("thu_quy", "thử giấy quỳ cho"); return; }
            if (Has(q, "giay ph", "giay chi thi")) { OnFocus("thu_giay_ph", "thử giấy pH cho"); return; }
            if (Has(q, "dun ", "dun nong", "dun soi", "ho nong")) { OnFocus("dun_nong", "đun nóng", Has(q, "soi") ? 100 : 70); return; }
            if (Has(q, "lac ", "lac deu", "lac binh")) { OnFocus("lac", "lắc", 5); return; }
            if (Has(q, "khuay")) { OnFocus("khuay", "khuấy từ", 15); return; }
            if (Has(q, "ly tam")) { OnFocus("ly_tam", "ly tâm"); return; }
            if (Has(q, "can ", "can khoi luong", "nang bao nhieu")) { OnFocus("can", "cân"); return; }
            if (Has(q, "do bo", "do di", "bo di")) { OnFocus("do_bo", "đổ bỏ dung dịch trong"); return; }
            if (Has(q, "quan sat", "co gi", "trong binh", "chua gi")) { OnFocus("quan_sat", "xem trong"); return; }
            if (Has(q, "dua vao tu hut", "cho vao tu hut")) { OnFocus("dua_vao_tu_hut", "đưa vào tủ hút"); return; }
            bool bring = Has(q, "mang", "lay", "dua", "dem");
            bool find = Has(q, "tim", "o dau", "cho nao", "vi tri", "nam o");
            if ((bring || find) && cat != null)
            {
                string key = q;
                foreach (var w in new[] { "giup minh", "giup", "cho minh", "ra ban", "ra day", "mang", "lay", "dua", "dem", "tim", "o dau", "cho nao", "vi tri", "nam o", "hoa chat", "lo", "chai", "?", "." })
                    key = key.Replace(w, " ");
                var found = cat.Search(key.Trim(), 1);
                if (found.Count > 0)
                {
                    Add("Robot", $"{found[0].ten}: {cat.LocationText(found[0])}.");
                    if (bring) robot?.BringChemical(found[0].stt); else robot?.LocateChemical(found[0].stt);
                    return;
                }
                Add("Robot", "Mình chưa nhận ra tên hóa chất. Bạn thử gõ công thức (ví dụ CuSO4) hoặc nhấn B để tra cứu.");
                return;
            }
            if (Has(q, "don", "lau", "ve sinh")) { robot?.CleanBench(); return; }
            if (Has(q, "tu hut", "thong gio", "quat hut", "khi doc")) { robot?.Ventilate(); return; }
            if (Has(q, "tat lua", "tat den", "chay", "dap lua")) { robot?.Extinguish(); return; }
            if (Has(q, "rua mat", "bat vao mat", "vao mat")) { robot?.GuideEyewash(); return; }
            if (Has(q, "hien tuong", "giai thich", "tai sao", "vi sao", "chuyen gi", "la gi"))
            {
                var ann = ReactionAnnotator.Instance;
                if (ann != null && ann.Last != null)
                {
                    var e = ann.Last;
                    Add("Robot", $"{e.Title}. {(e.Equation != null ? "Phương trình: " + e.Equation + ". " : "")}{e.Detail}");
                }
                else Add("Robot", "Chưa có hiện tượng nào. Hãy trộn thử hai dung dịch, ví dụ AgNO₃ với NaCl.");
                return;
            }
            if (Has(q, "chuan do"))
            {
                Add("Robot", "Chuẩn độ: lắp buret lên kẹp, rót dung dịch chuẩn quá vạch 0 rồi xả về vạch, nhấn Enter ghi V đầu. Dùng pipet lấy 25 ml mẫu vào bình nón, nhỏ 2 giọt chỉ thị, đặt dưới buret. Lăn chuột mở khóa, giữ chuột trên bình để lắc; gần điểm cuối nhỏ từng giọt. Màu bền 30 giây thì khóa, nhấn Enter ghi V cuối. Chọn bài khác trong menu (Esc).");
                return;
            }
            if (Has(q, "tra cuu", "tim kiem")) { Execute("mo_tra_cuu", 0); return; }
            Add("Robot", "Mình làm được: \"làm mẫu chuẩn độ\", \"thí nghiệm mẫu\", \"làm nhận biết Cu2+\", \"đo pH bình này\", \"đun nóng\", \"ly tâm\", \"lấy CuSO4 ra bàn\", \"KMnO4 ở đâu\", \"dọn bàn\", \"bật tủ hút\". Chạy máy chủ AI để ra lệnh tự do.");
        }

        static bool Has(string q, params string[] keys)
        {
            foreach (var k in keys) if (q.Contains(k)) return true;
            return false;
        }
    }
}
