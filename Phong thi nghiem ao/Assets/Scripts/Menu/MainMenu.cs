using System;
using System.Collections;
using System.Collections.Generic;
using Khkt.Demo;
using Khkt.Menu.Dashboard;
using Khkt.Network;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static Khkt.Menu.Dashboard.MenuKit;

namespace Khkt.Menu
{
    /// <summary>
    /// Màn hình chính, cảnh đầu tiên khi mở ứng dụng: đăng nhập, tạo tài khoản bằng mã lớp hoặc dùng thử,
    /// rồi vào trang chính (DashboardShell), từ đó mới tải phòng lab. Lớp này giữ việc điều hướng và các
    /// màn hình ở bảng bên trái; trang chính nằm trong thư mục Dashboard.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        public enum Screen
        {
            Login,
            Register,
            ChangePassword,
            Settings,
            Home,
            Room,
            Loading
        }

        public Camera menuCamera;
        public Texture2D background;
        public Texture2D logo;
        public Texture2D schoolLogo;

        const float PanelW = 640, ContentX = 80, ContentW = 480, FieldH = 52;

        public Screen Current { get; private set; }
        public float LoadProgress { get; private set; }

        readonly Dictionary<Screen, GameObject> screens = new Dictionary<Screen, GameObject>();
        readonly Dictionary<Screen, List<InputField>> fields = new Dictionary<Screen, List<InputField>>();
        readonly Dictionary<Screen, Action> submit = new Dictionary<Screen, Action>();
        readonly Dictionary<Screen, Action> back = new Dictionary<Screen, Action>();
        readonly Dictionary<Screen, Text> status = new Dictionary<Screen, Text>();

        RawImage bgImage;
        InputField loginUser, loginPass, regClass, regName, regUser, regPass, regPass2, pwOld, pwNew, pwNew2, serverField;
        Text loadText;
        RectTransform uiRoot, loginRoot, panelRoot;
        DashboardShell dashboard;
        bool panelVisible;
        Image loadBar;
        InputField roomJoinField;
        GameObject hostRelayButton, hostLanButton;
        bool busy;

        void Awake()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            BuildUi();
        }

        IEnumerator Start()
        {
            if (GameSession.HasIdentity)
            {
                Show(GameSession.User != null && GameSession.User.phai_doi_mat_khau ? Screen.ChangePassword : Screen.Home);
                yield break;
            }
            Show(Screen.Login);
            yield return null;
            EnsureDashboard();
            busy = true;
            bool restored = false;
            yield return AccountClient.Restore(r => restored = r.ok);
            busy = false;
            if (restored) AfterSignIn();
        }

        void Update()
        {
            if (panelVisible) FitBackground();
            var kb = Keyboard.current;
            if (kb == null || busy) return;
            if (kb.tabKey.wasPressedThisFrame) FocusNextField();
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
            {
                if (submit.TryGetValue(Current, out var a)) a();
            }
            if (kb.escapeKey.wasPressedThisFrame)
            {
                if (back.TryGetValue(Current, out var a)) a();
            }
        }

        // =====================================================================
        // Điều hướng
        // =====================================================================

        public void Show(Screen s)
        {
            Current = s;
            foreach (var kv in screens) kv.Value.SetActive(kv.Key == s);
            if (status.TryGetValue(s, out var t)) t.text = "";
            if (s == Screen.Home)
            {
                ShowDashboard();
                return;
            }
            ShowPanel();
            MenuAnim.Enter((RectTransform)screens[s].transform, 0f, 14f, 0.28f);
            if (s == Screen.Room) RefreshRoom();
            if (s == Screen.Settings) serverField.text = GameSession.ServerUrl;
            if (fields.TryGetValue(s, out var list) && list.Count > 0 && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(list[0].gameObject);
        }

        /// <summary>Trang chính phủ lên, bảng đăng nhập mờ đi rồi tắt hẳn để không tốn công vẽ.</summary>
        void ShowDashboard()
        {
            EnsureDashboard();
            dashboard.Show();
            if (!panelVisible) return;
            panelVisible = false;
            var g = MenuAnim.Group(loginRoot);
            g.blocksRaycasts = false;
            MenuAnim.Fade(g, g.alpha, 0f, 0.26f, 0f, () => loginRoot.gameObject.SetActive(false));
        }

        /// <summary>
        /// Dựng sẵn trang chính và bảng chữ trong lúc người dùng còn gõ ở màn đăng nhập, để lúc bấm Đăng nhập
        /// chuyển động bắt đầu ngay, không khựng vì dựng giao diện.
        /// </summary>
        void EnsureDashboard()
        {
            if (dashboard == null)
            {
                dashboard = DashboardShell.Create(uiRoot, logo);
                dashboard.Host = this;
                dashboard.EnterLab = EnterLab;
                dashboard.OpenRoom = () => { if (!busy) Show(Screen.Room); };
                dashboard.OpenSettings = () => { if (!busy) Show(Screen.Settings); };
                dashboard.SignOut = DoSignOut;
                dashboard.SessionExpired = message =>
                {
                    AccountClient.ExpireSession();
                    StudentData.Clear();
                    Show(Screen.Login);
                    Say(Screen.Login, message);
                };
            }
            dashboard.Prepare();
        }

        void ShowPanel()
        {
            dashboard?.Hide();
            if (panelVisible) return;
            panelVisible = true;
            loginRoot.gameObject.SetActive(true);
            var g = MenuAnim.Group(loginRoot);
            g.blocksRaycasts = true;
            MenuAnim.Fade(g, g.alpha, 1f, 0.32f);
            MenuAnim.Slide(panelRoot, new Vector2(-48, 0), Vector2.zero, 0.42f);
        }

        void FocusNextField()
        {
            if (!fields.TryGetValue(Current, out var list) || list.Count == 0 || EventSystem.current == null) return;
            var sel = EventSystem.current.currentSelectedGameObject;
            int i = sel != null ? list.FindIndex(f => f.gameObject == sel) : -1;
            var next = list[(i + 1) % list.Count];
            EventSystem.current.SetSelectedGameObject(next.gameObject);
            next.ActivateInputField();
        }

        void Say(Screen s, string message, bool error = true)
        {
            if (s == Screen.Home)
            {
                dashboard.Overview.Say(message, error);
                return;
            }
            status[s].color = error ? Error : Bio;
            status[s].text = message;
            if (error) MenuAnim.Shake(status[s].rectTransform);
        }

        void AfterSignIn()
        {
            Show(GameSession.User != null && GameSession.User.phai_doi_mat_khau ? Screen.ChangePassword : Screen.Home);
        }

        // =====================================================================
        // Hành động
        // =====================================================================

        void DoLogin()
        {
            if (busy) return;
            string user = loginUser.text.Trim();
            string pass = loginPass.text;
            if (user.Length == 0 || pass.Length == 0)
            {
                Say(Screen.Login, "Nhập tên đăng nhập và mật khẩu.");
                return;
            }
            StartCoroutine(Run(Screen.Login, "Đang đăng nhập…", AccountClient.Login(user, pass, r =>
            {
                busy = false;
                if (r.ok)
                {
                    loginPass.text = "";
                    AfterSignIn();
                }
                else Say(Screen.Login, r.message);
            })));
        }

        void DoRegister()
        {
            if (busy) return;
            string code = regClass.text.Trim(), name = regName.text.Trim(), user = regUser.text.Trim();
            if (code.Length == 0 || name.Length == 0 || user.Length == 0 || regPass.text.Length == 0)
            {
                Say(Screen.Register, "Điền đủ mã lớp, họ tên, tên đăng nhập và mật khẩu.");
                return;
            }
            if (regPass.text != regPass2.text)
            {
                Say(Screen.Register, "Hai lần nhập mật khẩu chưa khớp.");
                return;
            }
            StartCoroutine(Run(Screen.Register, "Đang tạo tài khoản…", AccountClient.Register(code, name, user, regPass.text, r =>
            {
                busy = false;
                if (r.ok)
                {
                    regPass.text = regPass2.text = "";
                    AfterSignIn();
                }
                else Say(Screen.Register, r.message);
            })));
        }

        void DoChangePassword()
        {
            if (busy) return;
            if (pwOld.text.Length == 0 || pwNew.text.Length == 0)
            {
                Say(Screen.ChangePassword, "Nhập mật khẩu hiện tại và mật khẩu mới.");
                return;
            }
            if (pwNew.text != pwNew2.text)
            {
                Say(Screen.ChangePassword, "Hai lần nhập mật khẩu mới chưa khớp.");
                return;
            }
            StartCoroutine(Run(Screen.ChangePassword, "Đang đổi mật khẩu…", AccountClient.ChangePassword(pwOld.text, pwNew.text, r =>
            {
                busy = false;
                if (r.ok)
                {
                    pwOld.text = pwNew.text = pwNew2.text = "";
                    Show(Screen.Home);
                }
                else if (r.expired)
                {
                    AccountClient.ExpireSession();
                    pwOld.text = pwNew.text = pwNew2.text = "";
                    Show(Screen.Login);
                    Say(Screen.Login, r.message);
                }
                else Say(Screen.ChangePassword, r.message);
            })));
        }

        IEnumerator Run(Screen s, string message, IEnumerator work)
        {
            busy = true;
            status[s].color = InkDim;
            status[s].text = message;
            yield return work;
            busy = false;
        }

        void DoGuest()
        {
            if (busy) return;
            GameSession.GuestActive = true;
            Show(Screen.Home);
        }

        void DoSignOut()
        {
            if (busy) return;
            StartCoroutine(SignOutFlow());
        }

        IEnumerator SignOutFlow()
        {
            busy = true;
            yield return AccountClient.Logout();
            StudentData.Clear();
            busy = false;
            Show(Screen.Login);
        }

        bool ValidServerUrl(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps);
        }

        void DoSaveServer()
        {
            string url = serverField.text.Trim();
            if (!ValidServerUrl(url))
            {
                Say(Screen.Settings, "Địa chỉ phải bắt đầu bằng http:// hoặc https://");
                return;
            }
            GameSession.ServerUrl = url;
            Say(Screen.Settings, "Đã lưu địa chỉ máy chủ.", false);
        }

        void DoCheckServer()
        {
            if (busy) return;
            string url = serverField.text.Trim();
            if (!ValidServerUrl(url))
            {
                Say(Screen.Settings, "Địa chỉ phải bắt đầu bằng http:// hoặc https://");
                return;
            }
            StartCoroutine(Run(Screen.Settings, "Đang kiểm tra…", AccountClient.CheckServer(url, ok =>
            {
                busy = false;
                if (ok) Say(Screen.Settings, "Kết nối được máy chủ.", false);
                else Say(Screen.Settings, "Không kết nối được máy chủ này.");
            })));
        }

        void EnterLab()
        {
            if (busy) return;
            GameSession.EnterLabPending = true;
            StartCoroutine(LoadLab());
        }

        IEnumerator LoadLab()
        {
            busy = true;
            Show(Screen.Loading);
            yield return null;
            var op = SceneManager.LoadSceneAsync(GameSession.LabScene);
            if (op == null)
            {
                busy = false;
                GameSession.EnterLabPending = false;
                Show(Screen.Home);
                Say(Screen.Home, "Không tìm thấy cảnh phòng thí nghiệm trong bản dựng này.");
                yield break;
            }
            float shown = 0f;
            while (!op.isDone)
            {
                LoadProgress = Mathf.Clamp01(op.progress / 0.9f);
                // tiến độ của Unity đi từng nấc: thanh chạy đuổi theo cho liền mạch
                shown = Mathf.MoveTowards(shown, LoadProgress, Time.unscaledDeltaTime * 1.6f);
                loadBar.rectTransform.anchorMax = new Vector2(shown, 1);
                loadText.text = $"{shown * 100f:0} %";
                yield return null;
            }
        }

        void FitBackground()
        {
            if (bgImage == null || background == null) return;
            var r = bgImage.rectTransform.rect;
            if (r.width < 1f || r.height < 1f) return;
            float want = r.width / r.height;
            float have = (float)background.width / background.height;
            // phủ kín khung, cắt phần thừa ở hai bên hoặc trên dưới
            bgImage.uvRect = want < have
                ? new UnityEngine.Rect((1f - want / have) * 0.5f, 0, want / have, 1)
                : new UnityEngine.Rect(0, (1f - have / want) * 0.5f, 1, have / want);
        }

        // =====================================================================
        // Dựng giao diện
        // =====================================================================

        void BuildUi()
        {
            var cgo = new GameObject("Giao diện màn hình chính", typeof(Canvas), typeof(CanvasScaler));
            cgo.transform.SetParent(transform, false);
            var canvas = cgo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = menuCamera;
            canvas.planeDistance = 1f;
            var scaler = cgo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            UiBridge.EnsureInteractive(canvas);
            uiRoot = (RectTransform)cgo.transform;
            // Bảng đăng nhập và ảnh nền chung một gốc để cùng mờ đi khi sang trang chính
            loginRoot = Full(uiRoot, "Đăng nhập và ảnh nền");
            var root = loginRoot;

            var bgRt = Stretch(root, "Ảnh nền", new Vector2(0, 0), new Vector2(1, 1), new Vector2(PanelW, 0), Vector2.zero);
            bgImage = bgRt.gameObject.AddComponent<RawImage>();
            bgImage.texture = background;
            bgImage.color = background != null ? Color.white : PaperAlt;
            bgImage.raycastTarget = false;

            var caption = Fill(bgRt, "Chú thích", new Vector2(0, 0), new Vector2(0, 0), new Vector2(36, 36), new Vector2(620, 64), new Color(0.04f, 0.05f, 0.06f, 0.8f), RadiusCard);
            GlassKit.Label(caption.transform, "Chữ", "Tự tay làm thí nghiệm Hóa học, Sinh học trong phòng lab ảo", 20, Color.white, false,
                           TextAnchor.MiddleLeft, new Vector2(22, 0), new Vector2(580, 64));

            var panel = Stretch(root, "Bảng bên trái", new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(PanelW, 0));
            panelRoot = panel;
            var bg = panel.gameObject.AddComponent<Image>();
            bg.color = Paper;
            bg.raycastTarget = true;

            BuildHeader(panel);
            BuildFooter(panel);
            BuildLogin(panel);
            BuildRegister(panel);
            BuildChangePassword(panel);
            BuildSettings(panel);
            BuildRoom(panel);
            BuildLoading(panel);

            // Từ lab về thì đi thẳng vào trang chính: bảng đăng nhập chỉ bật khi thật sự cần
            MenuAnim.Group(loginRoot).alpha = 0f;
            loginRoot.gameObject.SetActive(false);
        }

        void BuildHeader(RectTransform p)
        {
            if (logo != null)
            {
                var l = GlassKit.Rect(p, "Logo", new Vector2(0, 1), new Vector2(0, 1), new Vector2(ContentX, -60), new Vector2(60, 60));
                var img = l.gameObject.AddComponent<RawImage>();
                img.texture = logo;
                img.raycastTarget = false;
            }
            GlassKit.Label(p, "Tên", "VERA Lab", 32, Ink, true, TextAnchor.UpperLeft, new Vector2(ContentX + 74, -56), new Vector2(380, 42));
            GlassKit.Label(p, "Tên đầy đủ", "Virtual Experiment & Reality-based AI Lab", 16, InkDim, false, TextAnchor.UpperLeft, new Vector2(ContentX + 74, -98), new Vector2(420, 24));
        }

        void BuildFooter(RectTransform p)
        {
            // ghim đáy bảng: chiều cao canvas đổi theo tỉ lệ màn hình
            var foot = GlassKit.Rect(p, "Chân trang", new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(PanelW, 110));
            if (schoolLogo != null)
            {
                float w = 52f * schoolLogo.width / Mathf.Max(1, schoolLogo.height);
                var l = GlassKit.Rect(foot, "Logo trường", new Vector2(0, 0), new Vector2(0, 0), new Vector2(ContentX, 36), new Vector2(w, 52));
                var img = l.gameObject.AddComponent<RawImage>();
                img.texture = schoolLogo;
                img.raycastTarget = false;
            }
            MakeButton(foot, "Nút mở Cài đặt", "Cài đặt", new Vector2(PanelW - 240, -26), new Vector2(84, 36), () => { if (!busy) Show(Screen.Settings); }, 2);
            MakeButton(foot, "Nút Thoát", "Thoát", new Vector2(PanelW - 150, -26), new Vector2(70, 36), Application.Quit, 2);
        }

        RectTransform NewScreen(RectTransform p, Screen s, string name, string title, string subtitle)
        {
            var rt = GlassKit.Rect(p, name, new Vector2(0, 1), new Vector2(0, 1), new Vector2(ContentX, -170), new Vector2(ContentW, 820));
            screens[s] = rt.gameObject;
            fields[s] = new List<InputField>();
            GlassKit.Label(rt, "Tiêu đề", title, 36, Ink, true, TextAnchor.UpperLeft, Vector2.zero, new Vector2(ContentW, 48));
            if (subtitle != null) GlassKit.Label(rt, "Mô tả", subtitle, 17, InkDim, false, TextAnchor.UpperLeft, new Vector2(0, -52), new Vector2(ContentW, 48));
            rt.gameObject.SetActive(false);
            return rt;
        }

        void BuildLogin(RectTransform p)
        {
            var s = NewScreen(p, Screen.Login, "Đăng nhập", "Đăng nhập", "Dùng tài khoản VERA Lab của bạn.");
            loginUser = MakeField(s, Screen.Login, "Ô đăng nhập: tên", "Tên đăng nhập", -96, false);
            loginPass = MakeField(s, Screen.Login, "Ô đăng nhập: mật khẩu", "Mật khẩu", -182, true);
            status[Screen.Login] = MakeStatus(s, "Thông báo đăng nhập", -268);
            MakeButton(s, "Nút Đăng nhập", "Đăng nhập", new Vector2(0, -320), new Vector2(ContentW, 54), DoLogin, 0);
            MakeButton(s, "Nút chuyển Tạo tài khoản", "Tạo tài khoản bằng mã lớp", new Vector2(0, -386), new Vector2(ContentW, 54), () => { if (!busy) Show(Screen.Register); }, 1);
            MakeButton(s, "Nút Dùng thử", "Dùng thử không cần tài khoản", new Vector2(0, -452), new Vector2(ContentW, 54), DoGuest, 1);
            submit[Screen.Login] = DoLogin;
        }

        void BuildRegister(RectTransform p)
        {
            var s = NewScreen(p, Screen.Register, "Tạo tài khoản", "Tạo tài khoản", null);
            regClass = MakeField(s, Screen.Register, "Ô đăng ký: mã lớp", "Mã lớp (giáo viên cung cấp)", -60, false);
            regName = MakeField(s, Screen.Register, "Ô đăng ký: họ tên", "Họ và tên", -144, false);
            regUser = MakeField(s, Screen.Register, "Ô đăng ký: tên đăng nhập", "Tên đăng nhập", -228, false);
            regPass = MakeField(s, Screen.Register, "Ô đăng ký: mật khẩu", "Mật khẩu", -312, true);
            regPass2 = MakeField(s, Screen.Register, "Ô đăng ký: nhập lại", "Nhập lại mật khẩu", -396, true);
            status[Screen.Register] = MakeStatus(s, "Thông báo đăng ký", -482);
            MakeButton(s, "Nút Tạo tài khoản", "Tạo tài khoản", new Vector2(0, -534), new Vector2(ContentW, 54), DoRegister, 0);
            MakeButton(s, "Nút quay lại từ đăng ký", "Quay lại", new Vector2(0, -600), new Vector2(ContentW, 54), () => { if (!busy) Show(Screen.Login); }, 1);
            submit[Screen.Register] = DoRegister;
            back[Screen.Register] = () => Show(Screen.Login);
        }

        void BuildChangePassword(RectTransform p)
        {
            var s = NewScreen(p, Screen.ChangePassword, "Đổi mật khẩu", "Đổi mật khẩu", "Tài khoản này cần đặt mật khẩu mới trước khi dùng.");
            pwOld = MakeField(s, Screen.ChangePassword, "Ô đổi: mật khẩu hiện tại", "Mật khẩu hiện tại", -96, true);
            pwNew = MakeField(s, Screen.ChangePassword, "Ô đổi: mật khẩu mới", "Mật khẩu mới", -182, true);
            pwNew2 = MakeField(s, Screen.ChangePassword, "Ô đổi: nhập lại", "Nhập lại mật khẩu mới", -268, true);
            status[Screen.ChangePassword] = MakeStatus(s, "Thông báo đổi mật khẩu", -354);
            MakeButton(s, "Nút Đổi mật khẩu", "Đổi mật khẩu", new Vector2(0, -406), new Vector2(ContentW, 54), DoChangePassword, 0);
            MakeButton(s, "Nút đăng xuất khi đổi", "Đăng xuất", new Vector2(0, -472), new Vector2(ContentW, 54), DoSignOut, 1);
            submit[Screen.ChangePassword] = DoChangePassword;
        }

        void BuildSettings(RectTransform p)
        {
            var s = NewScreen(p, Screen.Settings, "Cài đặt", "Cài đặt", "Địa chỉ máy chủ tài khoản. Giáo viên hoặc nhà trường sẽ cung cấp địa chỉ này.");
            serverField = MakeField(s, Screen.Settings, "Ô địa chỉ máy chủ", "Địa chỉ máy chủ", -110, false);
            status[Screen.Settings] = MakeStatus(s, "Thông báo cài đặt", -196);
            MakeButton(s, "Nút Lưu địa chỉ", "Lưu", new Vector2(0, -248), new Vector2(ContentW, 54), DoSaveServer, 0);
            MakeButton(s, "Nút Kiểm tra kết nối", "Kiểm tra kết nối", new Vector2(0, -314), new Vector2(ContentW, 54), DoCheckServer, 1);
            MakeButton(s, "Nút Mặc định", "Về địa chỉ mặc định", new Vector2(0, -380), new Vector2(ContentW, 54), () => serverField.text = GameSession.DefaultServerUrl, 1);
            System.Action leave = () => { if (!busy) Show(GameSession.HasIdentity ? Screen.Home : Screen.Login); };
            MakeButton(s, "Nút quay lại từ cài đặt", "Quay lại", new Vector2(0, -446), new Vector2(ContentW, 54), leave, 1);
            submit[Screen.Settings] = DoSaveServer;
            back[Screen.Settings] = leave;
        }

        void BuildRoom(RectTransform p)
        {
            var s = NewScreen(p, Screen.Room, "Phòng học chung", "Phòng học chung", "Cùng vào một phòng lab với cả lớp.");
            roomJoinField = MakeField(s, Screen.Room, "Ô mã phòng", "Mã phòng hoặc địa chỉ IP", -96, false);
            MakeButton(s, "Nút Vào phòng", "Vào phòng", new Vector2(0, -182), new Vector2(ContentW, 54), DoJoinRoom, 0);
            status[Screen.Room] = MakeStatus(s, "Thông báo phòng", -246);
            hostRelayButton = MakeButton(s, "Nút Mở phòng Internet", "Mở phòng qua Internet", new Vector2(0, -300), new Vector2(ContentW, 54), () => DoHostRoom(true), 1).gameObject;
            hostLanButton = MakeButton(s, "Nút Mở phòng LAN", "Mở phòng trong mạng LAN", new Vector2(0, -366), new Vector2(ContentW, 54), () => DoHostRoom(false), 1).gameObject;
            System.Action leave = () => { if (!busy) Show(Screen.Home); };
            MakeButton(s, "Nút quay lại từ phòng", "Quay lại", new Vector2(0, -432), new Vector2(ContentW, 54), leave, 1);
            submit[Screen.Room] = DoJoinRoom;
            back[Screen.Room] = leave;
        }

        void RefreshRoom()
        {
            // tài khoản học sinh chỉ vào phòng, giáo viên và chế độ dùng thử mở được phòng
            bool canHost = GameSession.User == null || GameSession.User.vai_tro != "hoc_sinh";
            hostRelayButton.SetActive(canHost);
            hostLanButton.SetActive(canHost);
        }

        void DoHostRoom(bool relay)
        {
            if (busy) return;
            StartCoroutine(HostRoom(relay));
        }

        IEnumerator HostRoom(bool relay)
        {
            busy = true;
            status[Screen.Room].color = InkDim;
            status[Screen.Room].text = "Đang mở phòng…";
            var room = RoomConnection.Instance;
            bool ok;
            if (relay)
            {
                var task = room.HostRelay();
                while (!task.IsCompleted) yield return null;
                ok = task.Result;
            }
            else ok = room.HostLan();
            busy = false;
            if (ok) EnterLab();
            else Say(Screen.Room, room.Error);
        }

        void DoJoinRoom()
        {
            if (busy) return;
            string code = roomJoinField.text.Trim();
            if (code.Length == 0)
            {
                Say(Screen.Room, "Nhập mã phòng hoặc địa chỉ IP của máy mở phòng.");
                return;
            }
            StartCoroutine(JoinRoom(code));
        }

        IEnumerator JoinRoom(string code)
        {
            busy = true;
            status[Screen.Room].color = InkDim;
            status[Screen.Room].text = "Đang vào phòng…";
            var room = RoomConnection.Instance;
            var task = room.Join(code);
            while (!task.IsCompleted) yield return null;
            busy = false;
            if (task.Result) EnterLab();
            else Say(Screen.Room, room.Error);
        }

        void BuildLoading(RectTransform p)
        {
            var s = NewScreen(p, Screen.Loading, "Đang tải", "Đang mở phòng thí nghiệm", "Lần đầu có thể mất khoảng một phút trên máy yếu.");
            var track = Fill(s, "Thanh nền", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -120), new Vector2(ContentW, 12), PaperHover, 10f);
            var fill = Stretch(track.rectTransform, "Thanh tiến độ", new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, Vector2.zero);
            loadBar = fill.gameObject.AddComponent<Image>();
            loadBar.sprite = GlassKit.Solid;
            loadBar.type = Image.Type.Sliced;
            loadBar.pixelsPerUnitMultiplier = 10f;
            loadBar.color = Accent;
            loadBar.raycastTarget = false;
            loadText = GlassKit.Label(s, "Phần trăm", "0 %", 17, InkDim, false, TextAnchor.UpperLeft, new Vector2(0, -144), new Vector2(ContentW, 28));
        }

        InputField MakeField(Transform parent, Screen s, string name, string label, float y, bool password)
        {
            GlassKit.Label(parent, name + " (nhãn)", label, 16, Ink, true, TextAnchor.UpperLeft, new Vector2(0, y), new Vector2(ContentW, 24));
            var size = new Vector2(ContentW, FieldH);
            var outer = Fill(parent, name, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, y - 26), size, Line, RadiusButton);
            var inner = Fill(outer.transform, "Nền", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(3, 3), Paper, RadiusButton);
            inner.raycastTarget = true;
            var text = GlassKit.Label(outer.transform, "Chữ", "", 18, Ink, false, TextAnchor.MiddleLeft, new Vector2(14, 0), new Vector2(ContentW - 28, FieldH));
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            var f = outer.gameObject.AddComponent<InputField>();
            f.textComponent = text;
            f.targetGraphic = inner;
            f.caretColor = Accent;
            f.customCaretColor = true;
            f.selectionColor = new Color(Accent.r, Accent.g, Accent.b, 0.25f);
            var colors = f.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = PaperAlt;
            colors.selectedColor = AccentSoft;
            f.colors = colors;
            f.lineType = InputField.LineType.SingleLine;
            if (password)
            {
                f.contentType = InputField.ContentType.Password;
                f.asteriskChar = '•';
            }
            fields[s].Add(f);
            return f;
        }

        static Text MakeStatus(Transform parent, string name, float y)
        {
            return GlassKit.Label(parent, name, "", 16, Error, false, TextAnchor.UpperLeft, new Vector2(0, y), new Vector2(ContentW, 46));
        }
    }
}
