using System;
using System.Collections.Generic;
using System.Globalization;
using Khkt.Demo;
using UnityEngine;
using UnityEngine.UI;

namespace Khkt.Menu.Dashboard
{
    /// <summary>
    /// Khung trang chính sau khi đăng nhập: thanh bên trái có vệt chọn trượt, vùng nội dung bên phải đổi trang.
    /// Giao diện dựng lúc chạy như các bảng khác. MainMenu vẫn giữ việc điều hướng giữa các màn hình.
    /// </summary>
    public class DashboardShell : MonoBehaviour
    {
        public const float SidebarW = 272f;
        const float NavTop = -128f, NavStep = 56f, NavH = 50f;
        const float PagePad = 56f, PageGap = 20f;

        struct AssignmentSpec
        {
            public string title, subject, duration, goal;
            public bool isBio;
            public int station;
        }

        struct SkillSpec
        {
            public string title, subject, checklist;
            public bool isBio;
            public int station;
        }

        static readonly AssignmentSpec[] Assignments =
        {
            new AssignmentSpec
            {
                title = "Chuẩn độ axit HCl bằng NaOH 0,1 M",
                subject = "Hóa học",
                duration = "~15 phút",
                goal = "Đọc mặt khum buret, chuẩn độ tới hồng nhạt bền 30 giây và tính nồng độ HCl.",
                isBio = false,
                station = 1
            },
            new AssignmentSpec
            {
                title = "Chuẩn độ FeSO₄ bằng KMnO₄",
                subject = "Hóa học",
                duration = "~15 phút",
                goal = "Chuẩn độ oxi hóa - khử tự chỉ thị KMnO₄ trong môi trường axit sunfuric.",
                isBio = false,
                station = 1
            },
            new AssignmentSpec
            {
                title = "Đo phổ hấp thụ UV-Vis mẫu CuSO₄",
                subject = "Hóa học",
                duration = "~12 phút",
                goal = "Đo mẫu trắng, quét phổ tìm bước sóng cực đại λmax và dựng đường chuẩn.",
                isBio = false,
                station = 7
            },
            new AssignmentSpec
            {
                title = "Kính hiển vi: Co nguyên sinh biểu bì hành tím",
                subject = "Sinh học",
                duration = "~10 phút",
                goal = "Quan sát tiêu bản ở vật kính 10×/40× và theo dõi tế bào trong dung dịch ưu trương.",
                isBio = true,
                station = 21
            },
            new AssignmentSpec
            {
                title = "Tủ ấm & nồi hấp tiệt trùng 121 °C",
                subject = "Sinh học",
                duration = "~12 phút",
                goal = "Xả sạch không khí, giữ nhiệt độ 121 °C ở 1 bar và đọc ống chỉ thị sinh học.",
                isBio = true,
                station = 24
            },
            new AssignmentSpec
            {
                title = "PCR & điện di gel agarose tách đoạn ADN",
                subject = "Sinh học",
                duration = "~15 phút",
                goal = "Thiết lập chu trình nhiệt PCR và đối chiếu băng ADN trên bản điện di.",
                isBio = true,
                station = 28
            }
        };

        static readonly SkillSpec[] CoreSkills =
        {
            new SkillSpec
            {
                title = "Kỹ năng đọc vạch buret & chuẩn độ",
                subject = "Hóa học",
                isBio = false,
                station = 1,
                checklist = "• Tráng buret bằng dung dịch chuẩn, đuổi sạch bọt khí ở đầu khóa.\n" +
                            "• Đặt mắt ngang đáy mặt khum khi đọc V đầu và V cuối để tránh thị sai.\n" +
                            "• Lắc đều bình nón, dừng nhỏ giọt khi chỉ thị đổi màu bền 30 giây."
            },
            new SkillSpec
            {
                title = "Pha loãng & an toàn hóa chất",
                subject = "Hóa học",
                isBio = false,
                station = 2,
                checklist = "• Đọc kỹ nhãn hóa chất, nồng độ và cảnh báo nguy hiểm trước khi rót.\n" +
                            "• Không rót vượt quá 2/3 thể tích ống nghiệm hoặc cốc thủy tinh.\n" +
                            "• Dùng pipet đúng vạch, đậy nắp chai và rửa sạch dụng cụ sau thao tác."
            },
            new SkillSpec
            {
                title = "Vận hành máy phân tích & kính hiển vi",
                subject = "Sinh học",
                isBio = true,
                station = 21,
                checklist = "• Luôn đo mẫu trắng (blank) và chọn đúng bước sóng λmax trên máy phổ.\n" +
                            "• Lấy nét kính hiển vi từ vật kính nhỏ (4×, 10×) rồi mới sang 40×.\n" +
                            "• Tuân thủ khóa liên động, xả áp và kiểm tra nhiệt trước khi mở thiết bị."
            }
        };

        public Action EnterLab, OpenRoom, OpenSettings, SignOut;
        public Action<string> SessionExpired;
        /// <summary>Đối tượng luôn bật để chạy việc tải dữ liệu: ẩn trang lúc đang tải không được làm mất việc đó.</summary>
        public MonoBehaviour Host;

        public PageOverview Overview { get; private set; }
        public string CurrentPage { get; private set; } = "";

        sealed class NavItem
        {
            public string key;
            public int order;
            public RectTransform rt;
            public Text label, icon;
        }

        readonly List<NavItem> nav = new List<NavItem>();
        readonly Dictionary<string, RectTransform> pages = new Dictionary<string, RectTransform>();
        readonly Dictionary<string, Text> placeholderBodies = new Dictionary<string, Text>();
        readonly List<RectTransform> assignmentCards = new List<RectTransform>();
        readonly List<RectTransform> improvementCards = new List<RectTransform>();

        RectTransform root, sidebar, pill, content, improveKpiRow, improveBody;
        Text userName, userLine, avatarLetter, assignSubtitle, improveSubtitle;
        Canvas ownCanvas;
        bool visible;

        public static DashboardShell Create(RectTransform parent, Texture2D logo)
        {
            var rt = MenuKit.Full(parent, "Trang chính");
            var shell = rt.gameObject.AddComponent<DashboardShell>();
            shell.root = rt;
            shell.Build(logo);
            rt.gameObject.SetActive(false);
            return shell;
        }

        void Build(Texture2D logo)
        {
            // Canvas riêng: chuyển động trong trang chính không bắt dựng lại phần còn lại của giao diện
            ownCanvas = gameObject.AddComponent<Canvas>();
            gameObject.AddComponent<GraphicRaycaster>();
            MenuKit.Backdrop(root);                           // nền có chiều sâu, ảnh đục (chặn chạm xuống lớp dưới)

            content = MenuKit.Stretch(root, "Vùng nội dung", Vector2.zero, Vector2.one, new Vector2(SidebarW, 0), Vector2.zero);
            BuildSidebar(logo);
            BuildPages();
        }

        void BuildSidebar(Texture2D logo)
        {
            sidebar = MenuKit.Stretch(root, "Thanh bên", Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(SidebarW, 0));
            var bg = sidebar.gameObject.AddComponent<Image>();
            bg.color = MenuKit.Paper;
            var edge = MenuKit.Stretch(sidebar, "Viền", new Vector2(1, 0), Vector2.one, new Vector2(-1, 0), Vector2.zero);
            edge.gameObject.AddComponent<Image>().color = MenuKit.Hair;
            edge.GetComponent<Image>().raycastTarget = false;

            if (logo != null)
            {
                var l = GlassKit.Rect(sidebar, "Logo", new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -30), new Vector2(46, 46));
                var img = l.gameObject.AddComponent<RawImage>();
                img.texture = logo;
                img.raycastTarget = false;
            }
            GlassKit.Label(sidebar, "Tên", "VERA Lab", 24, MenuKit.Ink, true, TextAnchor.MiddleLeft, new Vector2(80, -30), new Vector2(180, 46));

            pill = MenuKit.Fill(sidebar, "Vệt chọn", new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, NavTop), new Vector2(SidebarW - 32, NavH), MenuKit.AccentSoft, MenuKit.RadiusButton).rectTransform;
            MenuKit.Ring(pill, "Viền", MenuKit.WithAlpha(MenuKit.Accent, 0.22f), MenuKit.RadiusButton);
            var vach = MenuKit.Fill(pill, "Vạch", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(-1, 0), new Vector2(4, 22), MenuKit.Accent, MenuKit.RadiusPill);
            vach.raycastTarget = false;

            AddNav("tong_quan", MenuKit.IconHome, "Tổng quan");
            AddNav("diem_so", MenuKit.IconScore, "Điểm số");
            AddNav("bai_tap", MenuKit.IconTask, "Bài tập");
            AddNav("tro_ly", MenuKit.IconRobot, "Trợ lý AI");
            AddNav("huong_dan", MenuKit.IconBook, "Hướng dẫn");
            AddNav("cai_thien", MenuKit.IconGrowth, "Cải thiện");

            // Phần tài khoản ghim đáy thanh bên: chiều cao canvas đổi theo tỉ lệ màn hình
            var foot = GlassKit.Rect(sidebar, "Tài khoản", new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(SidebarW, 228));
            var line = GlassKit.Rect(foot, "Kẻ", new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, 0), new Vector2(SidebarW - 40, 1));
            line.gameObject.AddComponent<Image>().color = MenuKit.Hair;
            line.GetComponent<Image>().raycastTarget = false;

            var avatar = MenuKit.Fill(foot, "Ảnh đại diện", new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -18), new Vector2(46, 46), MenuKit.Accent, 1f);
            avatar.sprite = GlassKit.SolidCircle;
            avatar.type = Image.Type.Simple;
            avatarLetter = GlassKit.Label(avatar.transform, "Chữ cái", "", 20, MenuKit.OnAccent, true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(46, 46));
            userName = GlassKit.Label(foot, "Tên người dùng", "", 16, MenuKit.Ink, true, TextAnchor.UpperLeft, new Vector2(80, -18), new Vector2(176, 24));
            FitOneLine(userName, 12);
            userLine = GlassKit.Label(foot, "Lớp người dùng", "", 14, MenuKit.InkDim, false, TextAnchor.UpperLeft, new Vector2(80, -42), new Vector2(176, 22));
            FitOneLine(userLine, 12);

            FootButton(foot, "Nút mở Cài đặt", MenuKit.IconSettings, "Cài đặt", -78, () => OpenSettings?.Invoke());
            FootButton(foot, "Nút Đăng xuất", MenuKit.IconSignOut, "Đăng xuất", -124, () => SignOut?.Invoke());
            FootButton(foot, "Nút Thoát", MenuKit.IconQuit, "Thoát ứng dụng", -170, Application.Quit);
        }

        /// <summary>Họ tên, danh sách lớp dài thì thu chữ lại cho vừa, không tràn khỏi thanh bên.</summary>
        static void FitOneLine(Text t, int minSize)
        {
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = minSize;
            t.resizeTextMaxSize = t.fontSize;
        }

        void FootButton(Transform parent, string name, string glyph, string text, float y, Action click)
        {
            var b = MenuKit.MakeButton(parent, name, "", new Vector2(16, y), new Vector2(SidebarW - 32, 44), click, 2);
            var label = b.transform.Find("Chữ").GetComponent<Text>();
            label.text = text;
            label.alignment = TextAnchor.MiddleLeft;
            label.rectTransform.anchoredPosition = new Vector2(48, 0);
            label.rectTransform.sizeDelta = new Vector2(SidebarW - 80, 44);
            MenuKit.Icon(b.transform, glyph, 16, MenuKit.InkDim, new Vector2(4, 0), new Vector2(40, 44));
        }

        void AddNav(string key, string glyph, string text)
        {
            var b = MenuKit.MakeButton(sidebar, "Mục: " + text, "", new Vector2(16, NavTop), new Vector2(SidebarW - 32, NavH), () => Go(key), 2);
            b.transition = Selectable.Transition.None;
            var hover = b.GetComponent<MenuHover>();
            hover.pressScale = 0.985f;
            var label = b.transform.Find("Chữ").GetComponent<Text>();
            label.text = text;
            label.alignment = TextAnchor.MiddleLeft;
            label.fontSize = 17;
            label.rectTransform.anchoredPosition = new Vector2(50, 0);
            label.rectTransform.sizeDelta = new Vector2(SidebarW - 90, NavH);
            var icon = MenuKit.Icon(b.transform, glyph, 18, MenuKit.InkDim, new Vector2(6, 0), new Vector2(40, NavH));
            nav.Add(new NavItem { key = key, rt = (RectTransform)b.transform, label = label, icon = icon });
        }

        void BuildPages()
        {
            var overview = MenuKit.Full(content, "Trang: Tổng quan");
            Overview = overview.gameObject.AddComponent<PageOverview>();
            Overview.Build(this);
            pages["tong_quan"] = overview;

            Placeholder("diem_so", "Điểm số", "Điểm và nhận xét của giáo viên cho từng bài.");
            BuildAssignmentsPage();
            Placeholder("tro_ly", "Trợ lý AI", "Hỏi đáp về thí nghiệm đang làm.");
            Placeholder("huong_dan", "Hướng dẫn", "Tài liệu bài thí nghiệm, máy và dụng cụ.");
            BuildImprovementPage();
        }

        RectTransform MakePageShell(string key, string title, string note, out Text subtitleLabel)
        {
            var rt = MenuKit.Full(content, "Trang: " + title);
            GlassKit.Label(rt, "Tiêu đề", title, 40, MenuKit.Ink, true, TextAnchor.UpperLeft, new Vector2(PagePad, -40), new Vector2(900, 48));
            subtitleLabel = GlassKit.Label(rt, "Mô tả", note, 18, MenuKit.InkDim, false, TextAnchor.UpperLeft, new Vector2(PagePad, -92), new Vector2(1100, 28));
            rt.gameObject.SetActive(false);
            pages[key] = rt;
            return rt;
        }

        static RectTransform MakePageRow(Transform parent, string name, float y, float h)
        {
            var row = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(parent, false);
            row.anchorMin = new Vector2(0, 1);
            row.anchorMax = new Vector2(1, 1);
            row.pivot = new Vector2(0.5f, 1);
            row.offsetMin = new Vector2(PagePad - PageGap * 0.5f, y - h);
            row.offsetMax = new Vector2(-PagePad + PageGap * 0.5f, y);
            return row;
        }

        static RectTransform MakeStretchTextBox(Text t, float left, float right, float top, float bottom)
        {
            var r = t.rectTransform;
            r.anchorMin = new Vector2(0, 1);
            r.anchorMax = new Vector2(1, 1);
            r.pivot = new Vector2(0, 1);
            r.offsetMin = new Vector2(left, bottom);
            r.offsetMax = new Vector2(-right, top);
            t.verticalOverflow = VerticalWrapMode.Truncate;
            return r;
        }

        static void MakeChipTag(Transform parent, string name, string text, Color bg, Color fg, Vector2 pos, float width = 104f)
        {
            var size = new Vector2(width, 30f);
            var pillImg = MenuKit.Fill(parent, name, new Vector2(0, 1), new Vector2(0, 1), pos, size, bg, MenuKit.RadiusPill);
            GlassKit.Label(pillImg.transform, "Chữ", text, 14, fg, true, TextAnchor.MiddleCenter, Vector2.zero, size);
        }

        void LaunchStation(int station, string wipTitle = null, string wipStep = null)
        {
            PlayerPrefs.SetInt("KHKT_PENDING_STATION", station);
            if (!string.IsNullOrEmpty(wipTitle))
            {
                PlayerPrefs.SetString("KHKT_WIP_TITLE", wipTitle);
                PlayerPrefs.SetString("KHKT_WIP_STEP", wipStep ?? "");
                PlayerPrefs.SetInt("KHKT_WIP_STATION", station);
            }
            PlayerPrefs.Save();
            EnterLab?.Invoke();
        }

        static int GuessStation(string title)
        {
            if (string.IsNullOrEmpty(title)) return 2;
            string s = title.ToLowerInvariant();
            if (s.Contains("pcr") || s.Contains("điện di") || s.Contains("adn") || s.Contains("gel")) return 28;
            if (s.Contains("tủ ấm") || s.Contains("nồi hấp") || s.Contains("tiệt trùng") || s.Contains("vi sinh")) return 24;
            if (s.Contains("kính hiển vi") || s.Contains("hành") || s.Contains("tế bào") || s.Contains("co nguyên sinh") || s.Contains("sinh học")) return 21;
            if (s.Contains("uv") || s.Contains("phổ") || s.Contains("ftir") || s.Contains("hplc") || s.Contains("aas") || s.Contains("sắc ký") || s.Contains("xrd") || s.Contains("nmr")) return 7;
            if (s.Contains("chuẩn độ") || s.Contains("buret") || s.Contains("hcl") || s.Contains("naoh") || s.Contains("feso") || s.Contains("kmno")) return 1;
            return 2;
        }

        static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var c = parent.GetChild(i);
                MenuAnim.Stop(c);
                c.SetParent(null);
                Destroy(c.gameObject);
            }
        }

        /// <summary>Dựng trang Bài tập với 6 bài thí nghiệm Hóa học và Sinh học vào thẳng đúng trạm.</summary>
        void BuildAssignmentsPage()
        {
            var rt = MakePageShell("bai_tap", "Bài tập", "Chọn một bài thí nghiệm để mở đúng trạm thực hành trong phòng lab.", out assignSubtitle);
            assignmentCards.Clear();

            const float cardH = 260f;
            for (int r = 0; r < 3; r++)
            {
                var row = MakePageRow(rt, "Hàng bài tập " + r, -138f - r * (cardH + PageGap), cardH);
                for (int c = 0; c < 2; c++)
                {
                    int idx = r * 2 + c;
                    if (idx >= Assignments.Length) break;
                    var a = Assignments[idx];
                    var card = MenuKit.Card(row, "Thẻ bài tập " + idx, c * 0.5f, (c + 1) * 0.5f, 0f, cardH, PageGap);
                    assignmentCards.Add(card);
                    var body = MenuKit.Body(card);

                    MakeChipTag(body, "Nhãn môn", a.subject, a.isBio ? MenuKit.BioSoft : MenuKit.AccentSoft, a.isBio ? MenuKit.Bio : MenuKit.Accent, new Vector2(24, -20));
                    GlassKit.Label(body, "Thời lượng", a.duration, 14, MenuKit.InkMute, false, TextAnchor.MiddleLeft, new Vector2(140, -20), new Vector2(200, 30));

                    var t = GlassKit.Label(body, "Tên bài", a.title, 20, MenuKit.Ink, true, TextAnchor.UpperLeft, Vector2.zero, Vector2.zero);
                    MakeStretchTextBox(t, 24, 24, -64, -118);

                    var goal = GlassKit.Label(body, "Mục tiêu", a.goal, 15, MenuKit.InkDim, false, TextAnchor.UpperLeft, Vector2.zero, Vector2.zero);
                    goal.lineSpacing = 1.22f;
                    MakeStretchTextBox(goal, 24, 24, -124, -182);

                    GlassKit.Label(body, "Trạm", $"Trạm {a.station}", 14, MenuKit.InkMute, false, TextAnchor.MiddleLeft, new Vector2(24, 57), new Vector2(180, 24), new Vector2(0, 0));
                    MenuKit.MakeButton(body, "Nút Làm bài", "Làm bài", new Vector2(-24, 22), new Vector2(150, 46), () => LaunchStation(a.station, a.title, a.goal), 0, MenuKit.RadiusButton, new Vector2(1, 0));
                }
            }
        }

        /// <summary>Dựng khung trang Cải thiện; nội dung được làm mới từ StudentData thật mỗi lần mở.</summary>
        void BuildImprovementPage()
        {
            var rt = MakePageShell("cai_thien", "Cải thiện", "", out improveSubtitle);
            improveKpiRow = MakePageRow(rt, "Hàng tóm tắt cải thiện", -138f, 140f);
            improveBody = new GameObject("Nội dung cải thiện", typeof(RectTransform)).GetComponent<RectTransform>();
            improveBody.SetParent(rt, false);
            improveBody.anchorMin = Vector2.zero;
            improveBody.anchorMax = Vector2.one;
            improveBody.offsetMin = new Vector2(PagePad - PageGap * 0.5f, 32f);
            improveBody.offsetMax = new Vector2(-PagePad + PageGap * 0.5f, -296f);
        }

        void RefreshAssignmentsPage()
        {
            if (assignSubtitle == null) return;
            var u = GameSession.User;
            assignSubtitle.text = u == null
                ? "Chế độ dùng thử: chọn bài để thực hành ngay trong phòng lab (kết quả chưa lưu lên lớp)."
                : u.vai_tro != "hoc_sinh"
                    ? "Xem trước danh sách bài thí nghiệm Hóa học và Sinh học trong VERA Lab."
                    : "Chọn một bài thí nghiệm để mở đúng trạm thực hành trong phòng lab.";
        }

        /// <summary>Cập nhật trang Cải thiện bằng số liệu StudentData thật hoặc 3 nhóm kỹ năng cốt lõi.</summary>
        void RefreshImprovementPage()
        {
            if (improveKpiRow == null || improveBody == null) return;
            ClearChildren(improveKpiRow);
            ClearChildren(improveBody);
            improvementCards.Clear();

            var u = GameSession.User;
            bool student = u != null && u.vai_tro == "hoc_sinh";
            var results = StudentData.Results;
            bool hasPersonalData = student && StudentData.HasData && results != null && results.Count > 0;

            if (hasPersonalData)
            {
                improveSubtitle.text = "Tổng hợp từ kết quả thật trên máy chủ và các nội dung nên luyện tập lại.";
                BuildPersonalImprovement(StudentData.Stats, results);
            }
            else
            {
                improveSubtitle.text = u == null
                    ? "Chế độ dùng thử chưa lưu điểm. Luyện 3 kỹ năng trọng tâm dưới đây để làm chủ phòng lab."
                    : !student
                        ? "Tài khoản giáo viên không có bảng điểm cá nhân."
                        : "Chưa có kết quả bài làm trên máy chủ. Bắt đầu với 3 kỹ năng trọng tâm dưới đây.";
                BuildCoreSkillsFallback(student);
            }
        }

        void BuildPersonalImprovement(StudentStats stats, List<StudentResult> results)
        {
            StudentResult lowestGraded = null;
            var graded = new List<StudentResult>();
            var ungraded = new List<StudentResult>();
            foreach (var r in results)
            {
                if (r.Graded)
                {
                    graded.Add(r);
                    if (lowestGraded == null || r.diem < lowestGraded.diem) lowestGraded = r;
                }
                else ungraded.Add(r);
            }
            graded.Sort((a, b) => a.diem.CompareTo(b.diem));

            string avgStr = stats.graded > 0 ? stats.average.ToString("0.0", CultureInfo.InvariantCulture) : "–";
            string avgNote = stats.graded > 0 ? $"Từ {stats.graded}/{stats.done} bài đã chấm" : $"{stats.done} bài đang chờ chấm";
            AddImproveKpi(0, "Điểm trung bình", avgStr, MenuKit.Ink, avgNote, MenuKit.IconScore, MenuKit.AccentSoft, MenuKit.Accent);

            string growthStr = stats.hasGrowth ? (stats.growth >= 0f ? "+" : "−") + Mathf.Abs(stats.growth).ToString("0.0", CultureInfo.InvariantCulture) : "–";
            Color growthColor = stats.hasGrowth ? (stats.growth >= 0f ? MenuKit.Bio : MenuKit.Error) : MenuKit.Ink;
            string growthNote = stats.hasGrowth
                ? "3 bài gần nhất so với 3 bài trước"
                : $"Cần thêm {StudentStats.GrowthWindow * 2 - stats.graded} bài đã chấm";
            AddImproveKpi(1, "Tiến bộ gần đây", growthStr, growthColor, growthNote, MenuKit.IconGrowth, MenuKit.BioSoft, MenuKit.Bio);

            string lowStr = lowestGraded != null ? lowestGraded.diem.ToString("0.#", CultureInfo.InvariantCulture) : "–";
            string lowNote = lowestGraded != null ? lowestGraded.ten_bai : "Chưa có điểm nào để xếp hạng";
            AddImproveKpi(2, "Điểm thấp nhất", lowStr, lowestGraded != null && lowestGraded.diem < 6.5f ? MenuKit.Warn : MenuKit.Ink, lowNote, MenuKit.IconTask, MenuKit.WarnSoft, MenuKit.Warn);

            var picks = new List<StudentResult>();
            foreach (var r in graded)
            {
                if (picks.Count >= 3) break;
                picks.Add(r);
            }
            foreach (var r in ungraded)
            {
                if (picks.Count >= 3) break;
                picks.Add(r);
            }

            const float cardH = 158f;
            for (int i = 0; i < picks.Count; i++)
            {
                var r = picks[i];
                var card = MenuKit.Card(improveBody, "Bài cần luyện " + i, 0f, 1f, -i * (cardH + 18f), cardH, PageGap);
                improvementCards.Add(card);
                var body = MenuKit.Body(card);

                bool isGraded = r.Graded;
                string badge = isGraded ? r.diem.ToString("0.#", CultureInfo.InvariantCulture) + " điểm" : "Chờ chấm";
                Color chipBg = !isGraded ? MenuKit.PaperAlt : r.diem >= 7f ? MenuKit.BioSoft : r.diem >= 5f ? MenuKit.WarnSoft : MenuKit.ErrorSoft;
                Color chipFg = !isGraded ? MenuKit.InkDim : r.diem >= 7f ? MenuKit.Bio : r.diem >= 5f ? MenuKit.Warn : MenuKit.Error;
                MakeChipTag(body, "Điểm", badge, chipBg, chipFg, new Vector2(24, -18), 114f);
                GlassKit.Label(body, "Ngày", r.DateText, 14, MenuKit.InkMute, false, TextAnchor.MiddleLeft, new Vector2(150, -18), new Vector2(220, 30));

                var t = GlassKit.Label(body, "Tên bài", r.ten_bai, 20, MenuKit.Ink, true, TextAnchor.UpperLeft, Vector2.zero, Vector2.zero);
                MakeStretchTextBox(t, 24, 232, -56, -92);

                string detail = !string.IsNullOrWhiteSpace(r.nhan_xet)
                    ? "Nhận xét: " + r.nhan_xet
                    : !isGraded
                        ? "Bài đã nộp và đang chờ giáo viên chấm điểm."
                        : r.diem < 6.5f
                            ? "Điểm còn thấp: nên thực hành lại để rà soát sai số và các bước chuẩn."
                            : "Ôn lại để giữ vững thao tác và nâng độ chính xác.";
                var d = GlassKit.Label(body, "Chi tiết", detail, 15, MenuKit.InkDim, false, TextAnchor.UpperLeft, Vector2.zero, Vector2.zero);
                d.lineSpacing = 1.2f;
                MakeStretchTextBox(d, 24, 232, -96, -146);

                int station = GuessStation(r.ten_bai);
                MenuKit.MakeButton(body, "Nút Luyện tập lại", "Luyện tập lại", new Vector2(-24, 0), new Vector2(184, 48), () => LaunchStation(station, r.ten_bai, detail), 0, MenuKit.RadiusButton, new Vector2(1, 0.5f));
            }
        }

        void AddImproveKpi(int index, string label, string value, Color valueColor, string note, string glyph, Color soft, Color iconInk)
        {
            var card = MenuKit.Card(improveKpiRow, "Chỉ số cải thiện " + index, index / 3f, (index + 1) / 3f, 0f, 140f, PageGap);
            improvementCards.Add(card);
            var body = MenuKit.Body(card);
            GlassKit.Label(body, "Nhãn", label, 17, MenuKit.InkDim, false, TextAnchor.UpperLeft, new Vector2(24, -16), new Vector2(260, 24));
            var badge = MenuKit.IconBadge(body, "Ô biểu tượng", glyph, Vector2.zero, 44, soft, iconInk).rectTransform;
            badge.anchorMin = badge.anchorMax = new Vector2(1, 1);
            badge.pivot = new Vector2(0, 1);
            badge.anchoredPosition = new Vector2(-68, -16);
            GlassKit.Label(body, "Giá trị", value, 38, valueColor, true, TextAnchor.UpperLeft, new Vector2(24, -40), new Vector2(260, 48));
            var n = GlassKit.Label(body, "Ghi chú", note, 15, MenuKit.InkDim, false, TextAnchor.UpperLeft, Vector2.zero, Vector2.zero);
            MakeStretchTextBox(n, 24, 24, -96, -126);
        }

        void BuildCoreSkillsFallback(bool student)
        {
            var banner = MenuKit.Card(improveKpiRow, "Thẻ chưa có dữ liệu", 0f, 1f, 0f, 140f, PageGap, false);
            improvementCards.Add(banner);
            var bb = MenuKit.Body(banner);
            MenuKit.IconBadge(bb, "Ô biểu tượng", MenuKit.IconGrowth, new Vector2(24, -22), 44, MenuKit.WarnSoft, MenuKit.Warn);
            GlassKit.Label(bb, "Tiêu đề", "Chưa có dữ liệu điểm cá nhân", 24, MenuKit.Ink, true, TextAnchor.UpperLeft, new Vector2(82, -20), new Vector2(640, 32));
            var bannerNote = GlassKit.Label(bb, "Nội dung",
                student
                    ? "Chưa có kết quả bài làm để phân tích điểm yếu. Sau khi hoàn thành bài thí nghiệm, các gợi ý cá nhân hóa sẽ hiện tại đây."
                    : "Không hiển thị điểm giả lập. Hãy đăng nhập bằng tài khoản học sinh để theo dõi tiến bộ từ bài làm thật.",
                16, MenuKit.InkDim, false, TextAnchor.UpperLeft, Vector2.zero, Vector2.zero);
            bannerNote.lineSpacing = 1.22f;
            MakeStretchTextBox(bannerNote, 82, 28, -58, -126);

            const float cardH = 174f;
            for (int i = 0; i < CoreSkills.Length; i++)
            {
                var sk = CoreSkills[i];
                var card = MenuKit.Card(improveBody, "Thẻ kỹ năng " + i, 0f, 1f, -i * (cardH + 18f), cardH, PageGap);
                improvementCards.Add(card);
                var body = MenuKit.Body(card);

                MakeChipTag(body, "Nhãn môn", sk.subject, sk.isBio ? MenuKit.BioSoft : MenuKit.AccentSoft, sk.isBio ? MenuKit.Bio : MenuKit.Accent, new Vector2(24, -18));
                var t = GlassKit.Label(body, "Tên kỹ năng", sk.title, 20, MenuKit.Ink, true, TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero);
                MakeStretchTextBox(t, 140, 232, -16, -50);

                var list = GlassKit.Label(body, "Danh sách kiểm tra", sk.checklist, 15, MenuKit.InkDim, false, TextAnchor.UpperLeft, Vector2.zero, Vector2.zero);
                list.lineSpacing = 1.24f;
                MakeStretchTextBox(list, 24, 232, -58, -164);

                MenuKit.MakeButton(body, "Nút Thực hành ngay", "Thực hành ngay", new Vector2(-24, 0), new Vector2(186, 48), () => LaunchStation(sk.station, sk.title, ""), 0, MenuKit.RadiusButton, new Vector2(1, 0.5f));
            }
        }

        void Placeholder(string key, string title, string note)
        {
            var rt = MenuKit.Full(content, "Trang: " + title);
            GlassKit.Label(rt, "Tiêu đề", title, 40, MenuKit.Ink, true, TextAnchor.UpperLeft, new Vector2(56, -48), new Vector2(900, 50));
            GlassKit.Label(rt, "Mô tả", note, 18, MenuKit.InkDim, false, TextAnchor.UpperLeft, new Vector2(56, -100), new Vector2(900, 30));
            var card = MenuKit.Card(rt, "Thẻ sắp có", 0f, 1f, -156, 150, 0f, false);
            card.offsetMin = new Vector2(56, card.offsetMin.y);
            card.offsetMax = new Vector2(-56, card.offsetMax.y);
            var body = MenuKit.Body(card);
            var msg = GlassKit.Label(body, "Chữ", "Trang này đang được xây dựng.", 20, MenuKit.InkDim, false, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero);
            var label = msg.rectTransform;
            label.anchorMin = Vector2.zero;
            label.anchorMax = Vector2.one;
            label.offsetMin = new Vector2(32, 0);
            label.offsetMax = new Vector2(-32, 0);
            placeholderBodies[key] = msg;
            rt.gameObject.SetActive(false);
            pages[key] = rt;
        }

        // =====================================================================
        // Hiện, ẩn
        // =====================================================================

        /// <summary>Hiển thị đủ 6 mục trên thanh bên và cập nhật lời giải thích cho chế độ dùng thử / giáo viên.</summary>
        void BindUser()
        {
            var u = GameSession.User;
            userName.text = GameSession.DisplayName;
            userLine.text = u != null ? MenuKit.ClassLine(u) : "Dùng thử";
            avatarLetter.text = MenuKit.Initial(GameSession.DisplayName);

            bool student = u != null && u.vai_tro == "hoc_sinh";
            float y = NavTop;
            int order = 0;
            foreach (var item in nav)
            {
                item.rt.gameObject.SetActive(true);
                item.order = order++;
                item.rt.anchoredPosition = new Vector2(16, y);
                y -= NavStep;
            }

            if (placeholderBodies.TryGetValue("diem_so", out var scoreBody))
            {
                scoreBody.text = student
                    ? "Trang bảng điểm chi tiết đang được hoàn thiện. Xem tóm tắt điểm và nhận xét mới ở mục Tổng quan."
                    : u == null
                        ? "Chế độ dùng thử không lưu điểm số. Hãy đăng nhập bằng tài khoản học sinh để xem điểm và nhận xét."
                        : "Tài khoản giáo viên không có bảng điểm cá nhân.";
            }
            RefreshAssignmentsPage();
            RefreshImprovementPage();
        }

        /// <summary>Tạo sẵn chữ tiếng Việt trong bảng phông. Gọi lúc rảnh; lần sau cùng tỉ lệ thì không làm gì.</summary>
        public void Prepare() => MenuKit.PrewarmFonts(ownCanvas.rootCanvas.scaleFactor);

        public void Show()
        {
            if (visible)
            {
                Overview.Refresh();
                return;
            }
            visible = true;
            root.gameObject.SetActive(true);
            MenuKit.PrewarmFonts(ownCanvas.rootCanvas.scaleFactor);
            BindUser();

            var g = MenuAnim.Group(root);
            g.blocksRaycasts = true;
            MenuAnim.Fade(g, 0f, 1f, 0.24f);
            MenuAnim.Pop(sidebar, 0.96f, 0.28f);

            CurrentPage = "";
            Go("tong_quan");
        }

        public void Hide(Action done = null)
        {
            if (!visible)
            {
                done?.Invoke();
                return;
            }
            visible = false;
            var g = MenuAnim.Group(root);
            g.blocksRaycasts = false;
            MenuAnim.Fade(g, g.alpha, 0f, 0.22f, 0f, () =>
            {
                root.gameObject.SetActive(false);
                done?.Invoke();
            });
        }

        /// <summary>Chuyển trang: vệt chọn trượt tới mục mới, trang mới hiện dần từ dưới lên.</summary>
        public void Go(string key)
        {
            if (key == CurrentPage || !pages.ContainsKey(key)) return;
            CurrentPage = key;
            foreach (var item in nav)
            {
                bool on = item.key == key;
                MenuAnim.ColorTo(item.label, on ? MenuKit.Accent : MenuKit.InkDim);
                item.label.font = on ? GlassKit.Semibold : GlassKit.Regular;
                if (item.icon != null) MenuAnim.ColorTo(item.icon, on ? MenuKit.Accent : MenuKit.InkDim);
                if (on)
                {
                    MenuAnim.Slide(pill, pill.anchoredPosition, item.rt.anchoredPosition, 0.28f, 0f, MenuAnim.Ease.OutQuint);
                    MenuAnim.PulseClick(pill, 0.96f, 1.02f, 0.20f);
                }
            }
            foreach (var kv in pages) kv.Value.gameObject.SetActive(kv.Key == key);

            if (key == "tong_quan") Overview.Open();
            else
            {
                if (pages[key].Find("Tiêu đề") is RectTransform t) MenuAnim.Enter(t, 0f, 12f, 0.28f);
                if (pages[key].Find("Mô tả") is RectTransform d) MenuAnim.Enter(d, 0.04f, 12f, 0.28f);
                if (key == "bai_tap")
                {
                    RefreshAssignmentsPage();
                    for (int i = 0; i < assignmentCards.Count; i++) MenuAnim.Enter(assignmentCards[i], 0.07f + i * 0.03f);
                }
                else if (key == "cai_thien")
                {
                    RefreshImprovementPage();
                    for (int i = 0; i < improvementCards.Count; i++) MenuAnim.Enter(improvementCards[i], 0.07f + i * 0.04f);
                }
                else if (pages[key].Find("Thẻ sắp có") is RectTransform card)
                {
                    MenuAnim.Enter(card, 0.08f);
                }
            }
        }
    }
}
