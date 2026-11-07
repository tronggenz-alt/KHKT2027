using System;
using System.Collections.Generic;
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

        RectTransform root, sidebar, pill, content;
        Text userName, userLine, avatarLetter;
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
            var bg = gameObject.AddComponent<Image>();
            bg.color = MenuKit.PaperAlt;

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
            edge.gameObject.AddComponent<Image>().color = MenuKit.PaperHover;
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

            AddNav("tong_quan", MenuKit.IconHome, "Tổng quan");
            AddNav("diem_so", MenuKit.IconScore, "Điểm số");
            AddNav("bai_tap", MenuKit.IconTask, "Bài tập");
            AddNav("tro_ly", MenuKit.IconRobot, "Trợ lý AI");
            AddNav("huong_dan", MenuKit.IconBook, "Hướng dẫn");
            AddNav("cai_thien", MenuKit.IconGrowth, "Cải thiện");

            // Phần tài khoản ghim đáy thanh bên: chiều cao canvas đổi theo tỉ lệ màn hình
            var foot = GlassKit.Rect(sidebar, "Tài khoản", new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(SidebarW, 228));
            var line = GlassKit.Rect(foot, "Kẻ", new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, 0), new Vector2(SidebarW - 40, 1));
            line.gameObject.AddComponent<Image>().color = MenuKit.PaperHover;
            line.GetComponent<Image>().raycastTarget = false;

            var avatar = MenuKit.Fill(foot, "Ảnh đại diện", new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -18), new Vector2(46, 46), MenuKit.Accent, 1f);
            avatar.sprite = GlassKit.SolidCircle;
            avatar.type = Image.Type.Simple;
            avatarLetter = GlassKit.Label(avatar.transform, "Chữ cái", "", 20, Color.white, true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(46, 46));
            userName = GlassKit.Label(foot, "Tên người dùng", "", 16, MenuKit.Ink, true, TextAnchor.UpperLeft, new Vector2(80, -18), new Vector2(176, 24));
            FitOneLine(userName, 12);
            userLine = GlassKit.Label(foot, "Lớp người dùng", "", 14, MenuKit.InkDim, false, TextAnchor.UpperLeft, new Vector2(80, -42), new Vector2(176, 22));
            FitOneLine(userLine, 11);

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
            Placeholder("bai_tap", "Bài tập", "Bài giáo viên giao, kèm hạn nộp.");
            Placeholder("tro_ly", "Trợ lý AI", "Hỏi đáp về thí nghiệm đang làm.");
            Placeholder("huong_dan", "Hướng dẫn", "Tài liệu bài thí nghiệm, máy và dụng cụ.");
            Placeholder("cai_thien", "Cải thiện", "Điểm tiến bộ và các bài nên làm lại.");
        }

        void Placeholder(string key, string title, string note)
        {
            var rt = MenuKit.Full(content, "Trang: " + title);
            GlassKit.Label(rt, "Tiêu đề", title, 36, MenuKit.Ink, true, TextAnchor.UpperLeft, new Vector2(56, -48), new Vector2(900, 50));
            GlassKit.Label(rt, "Mô tả", note, 18, MenuKit.InkDim, false, TextAnchor.UpperLeft, new Vector2(56, -100), new Vector2(900, 30));
            var card = MenuKit.Card(rt, "Thẻ sắp có", 0f, 1f, -156, 150, 0f, false);
            card.offsetMin = new Vector2(56, card.offsetMin.y);
            card.offsetMax = new Vector2(-56, card.offsetMax.y);
            GlassKit.Label(MenuKit.Body(card), "Chữ", "Trang này đang được xây dựng.", 20, MenuKit.InkDim, false, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(0, 0));
            var label = MenuKit.Body(card).Find("Chữ").GetComponent<RectTransform>();
            label.anchorMin = Vector2.zero;
            label.anchorMax = Vector2.one;
            label.offsetMin = label.offsetMax = Vector2.zero;
            rt.gameObject.SetActive(false);
            pages[key] = rt;
        }

        // =====================================================================
        // Hiện, ẩn
        // =====================================================================

        /// <summary>Đặt mục thanh bên theo vai trò. Khách và giáo viên không có điểm cá nhân nên không có các mục đó.</summary>
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
                bool show = student || item.key == "tong_quan" || item.key == "huong_dan" || (u != null && item.key == "tro_ly");
                item.rt.gameObject.SetActive(show);
                if (!show) continue;
                item.order = order++;
                item.rt.anchoredPosition = new Vector2(16, y);
                y -= NavStep;
            }
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
            MenuAnim.Fade(g, 0f, 1f, 0.3f);
            MenuAnim.Slide(sidebar, new Vector2(-40, 0), Vector2.zero, 0.42f);

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
                if (on) MenuAnim.Slide(pill, pill.anchoredPosition, item.rt.anchoredPosition, 0.28f);
            }
            foreach (var kv in pages) kv.Value.gameObject.SetActive(kv.Key == key);

            if (key == "tong_quan") Overview.Open();
            else
            {
                MenuAnim.Enter(pages[key].Find("Tiêu đề") as RectTransform, 0f, 12f, 0.28f);
                MenuAnim.Enter(pages[key].Find("Mô tả") as RectTransform, 0.04f, 12f, 0.28f);
                MenuAnim.Enter(pages[key].Find("Thẻ sắp có") as RectTransform, 0.08f);
            }
        }
    }
}
