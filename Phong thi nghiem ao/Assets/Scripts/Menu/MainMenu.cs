using System;
using System.Collections;
using System.Collections.Generic;
using Khkt.Demo;
using Khkt.Environment.Tang2;
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

        const float LeftW = 880f, ContentW = 520f, FieldH = 52f;
        const string TouchSensPrefKey = "KHKT_TOUCH_SENSITIVITY";

        public Screen Current { get; private set; }
        public float LoadProgress { get; private set; }

        readonly Dictionary<Screen, GameObject> screens = new Dictionary<Screen, GameObject>();
        readonly Dictionary<Screen, List<InputField>> fields = new Dictionary<Screen, List<InputField>>();
        readonly Dictionary<Screen, Action> submit = new Dictionary<Screen, Action>();
        readonly Dictionary<Screen, Action> back = new Dictionary<Screen, Action>();
        readonly Dictionary<Screen, Text> status = new Dictionary<Screen, Text>();

        RawImage bgImage;
        InputField loginUser, loginPass, regClass, regName, regUser, regPass, regPass2, pwOld, pwNew, pwNew2, serverField, aiUrlField;
        Text loadText, loginServerText, aiStatusText;
        RectTransform uiRoot, loginRoot, panelRoot;
        DashboardShell dashboard;
        ManChonLuong chonLuong;         // 18/11: màn đầu tiên, 2 nút lớn Giải bài tập / Phòng Lab
        ManGiaiBaiTap giaiBaiTap;       // phần CHEMISTRY đưa vào VERA: Chuỗi, 3D, Cơ chế
        bool giaiBaiTapTuBang;          // mở từ bảng điều khiển thì Quay lại về bảng, không về màn chọn
        bool panelVisible;
        Image loadBar;
        InputField roomJoinField;
        GameObject hostRelayButton, hostLanButton;
        Button[] settingsTabs;
        GameObject[] settingsPanes;
        int currentSettingsTab;
        Image[] themeCardBorders;
        Text[] themeCardLabels;
        Toggle transToggle, motionToggle, soundToggle;
        Slider touchSensSlider;
        Button clickImmediateChip, clickPanelChip;
        Button[] fontScaleChips, panelSizeChips;
        bool busy;

        void Awake()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            // C4 (18/11): nền là ảnh dựng thật của phòng lab (2x, làm mờ nhẹ, phủ màu thương hiệu) và logo vẽ lại nét cho Retina
            var nenLab = Resources.Load<Texture2D>("GiaoDien/NenLab");
            if (nenLab != null) background = nenLab;
            var logoNet = Resources.Load<Texture2D>("GiaoDien/LogoVera192");
            if (logoNet != null) logo = logoNet;
            BuildUi();
        }

        IEnumerator Start()
        {
            if (GameSession.HasIdentity)
            {
                // từ phòng lab quay về: vào thẳng bảng điều khiển như trước
                Show(GameSession.User != null && GameSession.User.phai_doi_mat_khau ? Screen.ChangePassword : Screen.Home);
                yield break;
            }
            chonLuong.Hien();
        }

        /// <summary>Màn chọn → "Phòng Lab": luồng cũ, đăng nhập (tự khôi phục phiên nếu còn) rồi vào bảng điều khiển.</summary>
        IEnumerator VaoPhongLab()
        {
            if (GameSession.HasIdentity)
            {
                AfterSignIn();
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

        bool daGanKhongHauKy;

        void Update()
        {
            GiuCameraGiaoDienSach();
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

        /// <summary>
        /// Camera màn chính chỉ vẽ giao diện: không được có hậu kỳ (tonemapping kéo trắng xuống ~81%, giao diện xám đục).
        /// Gắn CameraKhongHauKy một lần là HardwareProfile.ApplyCameras bỏ qua nó (sửa gốc ở C5, thay cho tắt lại mỗi khung).
        /// </summary>
        void GiuCameraGiaoDienSach()
        {
            if (daGanKhongHauKy || menuCamera == null) return;
            Khkt.Rendering.CameraKhongHauKy.Gan(menuCamera);
            daGanKhongHauKy = true;
        }

        // =====================================================================
        // Điều hướng
        // =====================================================================

        public void Show(Screen s)
        {
            Current = s;
            if (chonLuong != null && chonLuong.DangHien) chonLuong.An();        // vào màn nào của luồng Phòng Lab thì ẩn màn chọn
            if (giaiBaiTap != null && giaiBaiTap.DangHien && s != Screen.Home) giaiBaiTap.An();
            foreach (var kv in screens) kv.Value.SetActive(kv.Key == s);
            if (status.TryGetValue(s, out var t)) t.text = "";
            if (s == Screen.Home)
            {
                ShowDashboard();
                return;
            }
            ShowPanel();
            MenuAnim.Enter((RectTransform)screens[s].transform, 0f, 14f, 0.28f);
            if (s == Screen.Login && loginServerText != null) loginServerText.text = "Máy chủ: " + GameSession.ServerUrl;
            if (s == Screen.Room) RefreshRoom();
            if (s == Screen.Settings) RefreshSettings();
            if (fields.TryGetValue(s, out var list) && EventSystem.current != null)
            {
                var firstActive = list.Find(f => f != null && f.gameObject.activeInHierarchy);
                if (firstActive != null) EventSystem.current.SetSelectedGameObject(firstActive.gameObject);
            }
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
        }

        void FocusNextField()
        {
            if (!fields.TryGetValue(Current, out var raw) || EventSystem.current == null) return;
            var list = raw.FindAll(f => f != null && f.gameObject.activeInHierarchy);
            if (list.Count == 0) return;
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
            if (loginServerText != null) loginServerText.text = "Máy chủ: " + GameSession.ServerUrl;
            Say(Screen.Settings, "Đã lưu địa chỉ máy chủ.", false);
        }

        void DoSaveAiUrl()
        {
            if (aiUrlField == null) return;
            string url = aiUrlField.text.Trim();
            if (string.IsNullOrEmpty(url)) url = "http://127.0.0.1:8000";
            if (!ValidServerUrl(url))
            {
                if (aiStatusText != null)
                {
                    aiStatusText.color = Error;
                    aiStatusText.text = "Địa chỉ phải bắt đầu bằng http:// hoặc https://";
                }
                return;
            }
            PlayerPrefs.SetString(AiClient.ServerUrlPrefKey, url);
            PlayerPrefs.Save();
            if (aiStatusText != null)
            {
                aiStatusText.color = Bio;
                aiStatusText.text = "Đã lưu địa chỉ trợ lý AI.";
            }
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
            float shown = 0f, doiMeo = 0f;
            while (!op.isDone)
            {
                LoadProgress = Mathf.Clamp01(op.progress / 0.9f);
                // tiến độ của Unity đi từng nấc: thanh chạy đuổi theo cho liền mạch
                shown = Mathf.MoveTowards(shown, LoadProgress, Time.unscaledDeltaTime * 1.6f);
                loadBar.rectTransform.anchorMax = new Vector2(shown, 1);
                loadText.text = $"{shown * 100f:0} %";
                doiMeo += Time.unscaledDeltaTime;
                if (tipText != null && doiMeo >= 5f)                // đổi mẹo mỗi 5 giây
                {
                    doiMeo = 0f;
                    tipText.text = MeoAnToan[UnityEngine.Random.Range(0, MeoAnToan.Length)];
                }
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
            canvas.pixelPerfect = true;                       // viền 1 px thẳng hàng điểm ảnh (sắc trên Retina)
            var scaler = cgo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            UiBridge.EnsureInteractive(canvas);
            uiRoot = (RectTransform)cgo.transform;
            // Bảng đăng nhập gồm 2 cột: cột trái giới thiệu (880 px), cột phải biểu mẫu (1040 px)
            loginRoot = Full(uiRoot, "Đăng nhập và ảnh nền");
            var root = loginRoot;

            var leftCol = Stretch(root, "Cột trái", new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(LeftW, 0));
            var leftBase = leftCol.gameObject.AddComponent<Image>();
            leftBase.color = PaperAlt;
            leftBase.raycastTarget = true;
            var leftTint = Full(leftCol, "Nền nhấn dịu").gameObject.AddComponent<Image>();
            leftTint.color = AccentSoft;
            leftTint.raycastTarget = false;
            var colLine = Stretch(leftCol, "Kẻ ngăn cột", new Vector2(1, 0), Vector2.one, new Vector2(-1, 0), Vector2.zero).gameObject.AddComponent<Image>();
            colLine.color = Line;
            colLine.raycastTarget = false;

            BuildLeftShowcase(leftCol);

            var panel = Stretch(root, "Cột phải", new Vector2(0, 0), new Vector2(1, 1), new Vector2(LeftW, 0), Vector2.zero);
            panelRoot = panel;
            var bg = panel.gameObject.AddComponent<Image>();
            bg.color = Paper;
            bg.raycastTarget = true;

            var quitBtn = MakeButton(panel, "Nút Thoát", "Thoát", new Vector2(-36, -28), new Vector2(96, 44), Application.Quit, 2, RadiusButton, new Vector2(1, 1));
            ((RectTransform)quitBtn.transform).pivot = new Vector2(1, 1);

            BuildLogin(panel);
            BuildRegister(panel);
            BuildChangePassword(panel);
            BuildSettings(panel);
            BuildRoom(panel);
            BuildLoading(panel);

            // Từ lab về thì đi thẳng vào trang chính: bảng đăng nhập chỉ bật khi thật sự cần
            MenuAnim.Group(loginRoot).alpha = 0f;
            loginRoot.gameObject.SetActive(false);

            var veMan = MakeButton(panel, "Nút Về màn chọn", "← Màn chính", new Vector2(36, -28), new Vector2(160, 44), () => { if (!busy) VeManChon(); }, 2, RadiusButton, new Vector2(0, 1));
            ((RectTransform)veMan.transform).pivot = new Vector2(0, 1);

            giaiBaiTap = ManGiaiBaiTap.Tao(uiRoot, logo);
            giaiBaiTap.QuayLai = () =>
            {
                giaiBaiTap.An();
                if (!giaiBaiTapTuBang) chonLuong.Hien();
            };
            giaiBaiTap.MoCoChe = () => Khkt.CoChe.CoCheMoDun.Mo();
            giaiBaiTap.MoChuoi = () => Khkt.CoChe.CoCheMoDun.MoGiaiChuoi();
            giaiBaiTap.Mo3D = () => Khkt.CoChe.MoHinh3DMoDun.Mo();
            var caiDatGbt = CaiDatGiaiBaiTap.Tao(uiRoot);
            giaiBaiTap.MoCaiDat = caiDatGbt.Hien;
            giaiBaiTap.MoHoaLy = ma => giaiBaiTap.Bao("Hóa lý (" + ma.Replace('_', ' ') + "): đang chuyển 4 mô phỏng của CHEMISTRY sang Unity, sẽ có ở bản sau.", false);
            chonLuong = ManChonLuong.Tao(uiRoot, logo);
            chonLuong.ChonPhongLab = () => { chonLuong.An(); StartCoroutine(VaoPhongLab()); };
            chonLuong.ChonGiaiBaiTap = () => { chonLuong.An(); MoGiaiBaiTap(false); };
            chonLuong.Thoat = Application.Quit;
            chonLuong.gameObject.SetActive(false);
        }

        /// <summary>Mở màn Giải bài tập. tuBang = mở từ bảng điều khiển (Quay lại thì về bảng).</summary>
        public void MoGiaiBaiTap(bool tuBang)
        {
            giaiBaiTapTuBang = tuBang;
            giaiBaiTap.Hien();
        }

        /// <summary>Từ màn đăng nhập về màn chọn 2 luồng.</summary>
        void VeManChon()
        {
            if (panelVisible)
            {
                panelVisible = false;
                var g = MenuAnim.Group(loginRoot);
                g.blocksRaycasts = false;
                MenuAnim.Fade(g, g.alpha, 0f, 0.22f, 0f, () => loginRoot.gameObject.SetActive(false));
            }
            chonLuong.Hien();
        }

        void BuildLeftShowcase(RectTransform left)
        {
            const float PadX = 72f;
            float innerW = LeftW - PadX * 2f;

            if (logo != null)
            {
                var l = GlassKit.Rect(left, "Logo", new Vector2(0, 1), new Vector2(0, 1), new Vector2(PadX, -56), new Vector2(60, 60));
                var img = l.gameObject.AddComponent<RawImage>();
                img.texture = logo;
                img.raycastTarget = false;
            }
            GlassKit.Label(left, "Tên", "VERA Lab", 34, Ink, true, TextAnchor.UpperLeft, new Vector2(PadX + 74, -54), new Vector2(540, 42));
            GlassKit.Label(left, "Tên đầy đủ", "Virtual Experiment & Reality-based AI Lab", 16, InkDim, false, TextAnchor.UpperLeft, new Vector2(PadX + 74, -96), new Vector2(580, 24));

            // Khung minh họa bo góc chứa ảnh lab
            var artFrame = Fill(left, "Khung minh họa", new Vector2(0, 1), new Vector2(0, 1), new Vector2(PadX, -152), new Vector2(innerW, 540), Paper, RadiusCard);
            var mask = artFrame.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;
            var bgRt = Stretch(artFrame.transform, "Ảnh nền", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            bgImage = bgRt.gameObject.AddComponent<RawImage>();
            bgImage.texture = background;
            bgImage.color = background != null ? MenuKit.Hex(0xffffff) : PaperAlt;
            bgImage.raycastTarget = false;
            var borderRt = GlassKit.Rect(left, "Viền khung minh họa", new Vector2(0, 1), new Vector2(0, 1), new Vector2(PadX, -152), new Vector2(innerW, 540));
            var borderImg = borderRt.gameObject.AddComponent<Image>();
            borderImg.sprite = GlassKit.Border;
            borderImg.type = Image.Type.Sliced;
            borderImg.pixelsPerUnitMultiplier = RadiusCard;
            borderImg.color = Line;
            borderImg.raycastTarget = false;

            GlassKit.Label(left, "Giới thiệu", "Tự tay làm thí nghiệm Hóa học, Sinh học trong phòng lab ảo.", 20, Ink, false, TextAnchor.UpperLeft, new Vector2(PadX, -718), new Vector2(innerW, 64));

            if (schoolLogo != null)
            {
                float w = 52f * schoolLogo.width / Mathf.Max(1, schoolLogo.height);
                var l = GlassKit.Rect(left, "Logo trường", new Vector2(0, 0), new Vector2(0, 0), new Vector2(PadX, 44), new Vector2(w, 52));
                var img = l.gameObject.AddComponent<RawImage>();
                img.texture = schoolLogo;
                img.raycastTarget = false;
            }
        }

        RectTransform NewScreen(RectTransform p, Screen s, string name, string title, string subtitle, float w = ContentW, float h = 760f)
        {
            var rt = GlassKit.Rect(p, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, h));
            screens[s] = rt.gameObject;
            fields[s] = new List<InputField>();
            GlassKit.Label(rt, "Tiêu đề", title, 34, Ink, true, TextAnchor.UpperLeft, Vector2.zero, new Vector2(w, 46));
            if (subtitle != null) GlassKit.Label(rt, "Mô tả", subtitle, 17, InkDim, false, TextAnchor.UpperLeft, new Vector2(0, -50), new Vector2(w, 44));
            rt.gameObject.SetActive(false);
            return rt;
        }

        void BuildLogin(RectTransform p)
        {
            var s = NewScreen(p, Screen.Login, "Đăng nhập", "Đăng nhập", "Dùng tài khoản VERA Lab của bạn.", ContentW, 650f);
            loginUser = MakeField(s, Screen.Login, "Ô đăng nhập: tên", "Tên đăng nhập", -104, false);
            loginPass = MakeField(s, Screen.Login, "Ô đăng nhập: mật khẩu", "Mật khẩu", -192, true);
            status[Screen.Login] = MakeStatus(s, "Thông báo đăng nhập", -280);
            MakeButton(s, "Nút Đăng nhập", "Đăng nhập", new Vector2(0, -334), new Vector2(ContentW, 54), DoLogin, 0);
            MakeButton(s, "Nút chuyển Tạo tài khoản", "Đăng ký", new Vector2(0, -400), new Vector2(ContentW, 54), () => { if (!busy) Show(Screen.Register); }, 1);
            MakeButton(s, "Nút Dùng thử", "Dùng thử không cần tài khoản", new Vector2(0, -466), new Vector2(ContentW, 54), DoGuest, 1);

            var rule = GlassKit.Rect(s, "Kẻ máy chủ", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -550), new Vector2(ContentW, 1));
            var rImg = rule.gameObject.AddComponent<Image>();
            rImg.color = Line;
            rImg.raycastTarget = false;

            loginServerText = GlassKit.Label(s, "Trạng thái máy chủ", "Máy chủ: " + GameSession.ServerUrl, 15, InkDim, false, TextAnchor.MiddleLeft, new Vector2(0, -568), new Vector2(ContentW - 116, 44));
            loginServerText.verticalOverflow = VerticalWrapMode.Truncate;
            loginServerText.resizeTextForBestFit = true;
            loginServerText.resizeTextMinSize = 12;
            loginServerText.resizeTextMaxSize = 15;
            MakeButton(s, "Nút Đổi máy chủ", "Đổi", new Vector2(ContentW - 104, -568), new Vector2(104, 44), () =>
            {
                if (busy) return;
                SelectSettingsTab(4);
                Show(Screen.Settings);
            }, 1);

            submit[Screen.Login] = DoLogin;
        }

        void BuildRegister(RectTransform p)
        {
            var s = NewScreen(p, Screen.Register, "Tạo tài khoản", "Tạo tài khoản", null, ContentW, 700f);
            regClass = MakeField(s, Screen.Register, "Ô đăng ký: mã lớp", "Mã lớp (giáo viên cung cấp)", -64, false);
            regName = MakeField(s, Screen.Register, "Ô đăng ký: họ tên", "Họ và tên", -148, false);
            regUser = MakeField(s, Screen.Register, "Ô đăng ký: tên đăng nhập", "Tên đăng nhập", -232, false);
            regPass = MakeField(s, Screen.Register, "Ô đăng ký: mật khẩu", "Mật khẩu", -316, true);
            regPass2 = MakeField(s, Screen.Register, "Ô đăng ký: nhập lại", "Nhập lại mật khẩu", -400, true);
            status[Screen.Register] = MakeStatus(s, "Thông báo đăng ký", -486);
            MakeButton(s, "Nút Tạo tài khoản", "Tạo tài khoản", new Vector2(0, -540), new Vector2(ContentW, 54), DoRegister, 0);
            MakeButton(s, "Nút quay lại từ đăng ký", "Quay lại", new Vector2(0, -606), new Vector2(ContentW, 54), () => { if (!busy) Show(Screen.Login); }, 1);
            submit[Screen.Register] = DoRegister;
            back[Screen.Register] = () => Show(Screen.Login);
        }

        void BuildChangePassword(RectTransform p)
        {
            var s = NewScreen(p, Screen.ChangePassword, "Đổi mật khẩu", "Đổi mật khẩu", "Tài khoản này cần đặt mật khẩu mới trước khi dùng.", ContentW, 600f);
            pwOld = MakeField(s, Screen.ChangePassword, "Ô đổi: mật khẩu hiện tại", "Mật khẩu hiện tại", -104, true);
            pwNew = MakeField(s, Screen.ChangePassword, "Ô đổi: mật khẩu mới", "Mật khẩu mới", -192, true);
            pwNew2 = MakeField(s, Screen.ChangePassword, "Ô đổi: nhập lại", "Nhập lại mật khẩu mới", -280, true);
            status[Screen.ChangePassword] = MakeStatus(s, "Thông báo đổi mật khẩu", -368);
            MakeButton(s, "Nút Đổi mật khẩu", "Đổi mật khẩu", new Vector2(0, -422), new Vector2(ContentW, 54), DoChangePassword, 0);
            MakeButton(s, "Nút đăng xuất khi đổi", "Đăng xuất", new Vector2(0, -488), new Vector2(ContentW, 54), DoSignOut, 1);
            submit[Screen.ChangePassword] = DoChangePassword;
        }

        void BuildSettings(RectTransform p)
        {
            const float SetW = 920f, SetH = 740f, SideW = 210f, PaneX = 248f, PaneW = SetW - PaneX;
            var s = NewScreen(p, Screen.Settings, "Cài đặt", "Cài đặt", "Tùy chỉnh giao diện, âm thanh, điều khiển, trợ lý AI và máy chủ.", SetW, SetH);

            var topRule = GlassKit.Rect(s, "Kẻ đầu", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -98), new Vector2(SetW, 1));
            var trImg = topRule.gameObject.AddComponent<Image>();
            trImg.color = Line;
            trImg.raycastTarget = false;

            var sideRule = GlassKit.Rect(s, "Kẻ cột mục", new Vector2(0, 1), new Vector2(0, 1), new Vector2(SideW + 18, -114), new Vector2(1, 520));
            var srImg = sideRule.gameObject.AddComponent<Image>();
            srImg.color = Line;
            srImg.raycastTarget = false;

            string[] tabTitles = { "Hiển thị", "Âm thanh", "Điều khiển", "Trợ lý AI", "Máy chủ" };
            settingsTabs = new Button[tabTitles.Length];
            settingsPanes = new GameObject[tabTitles.Length];
            for (int i = 0; i < tabTitles.Length; i++)
            {
                int idx = i;
                settingsTabs[i] = GlassKit.MakeChip(s, "Mục " + tabTitles[i], tabTitles[i], new Vector2(0, -114 - i * 58), new Vector2(SideW, 50), () => SelectSettingsTab(idx), i == 0, 16);
                var tabLabel = settingsTabs[i].transform.Find("Chữ")?.GetComponent<Text>();
                if (tabLabel != null)
                {
                    tabLabel.alignment = TextAnchor.MiddleLeft;
                    tabLabel.rectTransform.anchoredPosition = new Vector2(14, 0);
                    tabLabel.rectTransform.sizeDelta = new Vector2(SideW - 28, 50);
                }

                var paneRt = GlassKit.Rect(s, "Nội dung " + tabTitles[i], new Vector2(0, 1), new Vector2(0, 1), new Vector2(PaneX, -114), new Vector2(PaneW, 530));
                settingsPanes[i] = paneRt.gameObject;
            }

            // Tab 0: Hiển thị (3 thẻ xem trước giao diện + Nền đặc + Giảm chuyển động + Cỡ chữ + Cỡ bảng dụng cụ)
            var t0 = (RectTransform)settingsPanes[0].transform;
            GlassKit.Label(t0, "Nhãn giao diện", "Giao diện", 17, Ink, true, TextAnchor.UpperLeft, Vector2.zero, new Vector2(PaneW, 26));
            var modes = new[] { UiTheme.Mode.XanhMuc, UiTheme.Mode.Trang, UiTheme.Mode.Den };
            themeCardBorders = new Image[modes.Length];
            themeCardLabels = new Text[modes.Length];
            float cardW = (PaneW - 24f) / 3f;
            for (int i = 0; i < modes.Length; i++)
            {
                var mode = modes[i];
                BuildThemePreviewCard(t0, i, mode, new Vector2(i * (cardW + 12f), -30), new Vector2(cardW, 148));
            }
            transToggle = GlassKit.MakeToggle(t0, "ToggleTrans", "Nền đặc chống lóa", new Vector2(0, -194), new Vector2(PaneW, 46), GlassKit.ReducedTransparency, v => GlassKit.ReducedTransparency = v);
            motionToggle = GlassKit.MakeToggle(t0, "ToggleMotion", "Giảm chuyển động", new Vector2(0, -248), new Vector2(PaneW, 46), ReducedMotion.IsEnabled, v => ReducedMotion.IsEnabled = v);
            fontScaleChips = BuildChipRow(t0, "CoChu", "Cỡ chữ", new[] { "Chuẩn", "Lớn", "Rất lớn" }, -312f, PaneW, UiTheme.FontScaleMode, SelectFontScale);
            panelSizeChips = BuildChipRow(t0, "CoBang", "Cỡ bảng dụng cụ", new[] { "60%", "75%", "Kín màn hình" }, -404f, PaneW, UiTheme.PanelSizeMode, SelectPanelSize);

            // Tab 1: Âm thanh
            var t1 = (RectTransform)settingsPanes[1].transform;
            GlassKit.Label(t1, "Nhãn âm thanh", "Âm thanh hệ thống", 17, Ink, true, TextAnchor.UpperLeft, Vector2.zero, new Vector2(PaneW, 26));
            soundToggle = GlassKit.MakeToggle(t1, "ToggleSound", "Âm thanh phản hồi nút bấm", new Vector2(0, -40), new Vector2(PaneW, 46), NutHieuUng.SoundEnabled, v => NutHieuUng.SoundEnabled = v);

            // Tab 2: Điều khiển
            var t2 = (RectTransform)settingsPanes[2].transform;
            GlassKit.Label(t2, "Nhãn bấm dụng cụ", "Bấm vào dụng cụ", 17, Ink, true, TextAnchor.UpperLeft, Vector2.zero, new Vector2(PaneW, 26));
            float half = (PaneW - 12f) / 2f;
            int clickMode = PlayerPrefs.GetInt(UiTheme.ClickModePrefKey, 0);
            clickImmediateChip = GlassKit.MakeChip(t2, "Nút Cầm ngay", "Cầm ngay", new Vector2(0, -36), new Vector2(half, 46), () => SelectClickMode(0), clickMode == 0, 15);
            clickPanelChip = GlassKit.MakeChip(t2, "Nút Mở bảng", "Mở bảng", new Vector2(half + 12f, -36), new Vector2(half, 46), () => SelectClickMode(1), clickMode == 1, 15);
            float savedSens = Mathf.Clamp(PlayerPrefs.GetFloat(TouchSensPrefKey, 0.18f), 0.05f, 0.50f);
            touchSensSlider = GlassKit.MakeSlider(t2, "Thanh độ nhạy", "Độ nhạy vuốt nhìn", new Vector2(0, -108), new Vector2(PaneW, 64), 0.05f, 0.50f, savedSens, OnTouchSensChanged, v => v.ToString("0.00"));

            // Tab 3: Trợ lý AI
            var t3 = (RectTransform)settingsPanes[3].transform;
            string savedAiUrl = PlayerPrefs.GetString(AiClient.ServerUrlPrefKey, "http://127.0.0.1:8000");
            aiUrlField = MakeField(t3, Screen.Settings, "Ô địa chỉ AI", "Địa chỉ máy chủ AI (trợ lý & robot)", 0, false, PaneW);
            aiUrlField.text = savedAiUrl;
            MakeButton(t3, "Nút Lưu AI", "Lưu", new Vector2(0, -98), new Vector2(200, 48), DoSaveAiUrl, 0);
            aiStatusText = GlassKit.Label(t3, "Trạng thái AI", "", 15, Bio, false, TextAnchor.UpperLeft, new Vector2(0, -160), new Vector2(PaneW, 38));

            // Tab 4: Máy chủ
            var t4 = (RectTransform)settingsPanes[4].transform;
            serverField = MakeField(t4, Screen.Settings, "Ô địa chỉ máy chủ", "Địa chỉ máy chủ tài khoản", 0, false, PaneW);
            status[Screen.Settings] = MakeStatus(t4, "Thông báo cài đặt", -86, PaneW);
            MakeButton(t4, "Nút Lưu địa chỉ", "Lưu", new Vector2(0, -138), new Vector2(half, 48), DoSaveServer, 0);
            MakeButton(t4, "Nút Kiểm tra kết nối", "Kiểm tra kết nối", new Vector2(half + 12f, -138), new Vector2(half, 48), DoCheckServer, 1);
            MakeButton(t4, "Nút Máy chủ trực tuyến", "Máy chủ trực tuyến", new Vector2(0, -198), new Vector2(half, 48), () => serverField.text = GameSession.CloudServerUrl, 1);
            MakeButton(t4, "Nút Mặc định", "Máy chủ trên máy này", new Vector2(half + 12f, -198), new Vector2(half, 48), () => serverField.text = GameSession.LocalServerUrl, 1);

            System.Action leave = () => { if (!busy) Show(GameSession.HasIdentity ? Screen.Home : Screen.Login); };
            MakeButton(s, "Nút quay lại từ cài đặt", "Quay lại", new Vector2(0, -SetH + 54), new Vector2(200, 52), leave, 1);
            submit[Screen.Settings] = () => { if (currentSettingsTab == 3) DoSaveAiUrl(); else if (currentSettingsTab == 4) DoSaveServer(); };
            back[Screen.Settings] = leave;

            SelectSettingsTab(0);
        }

        void BuildThemePreviewCard(Transform parent, int index, UiTheme.Mode mode, Vector2 pos, Vector2 size)
        {
            bool active = UiTheme.Current == mode;
            var border = Fill(parent, "Thẻ giao diện " + mode, new Vector2(0, 1), new Vector2(0, 1), pos, size, active ? Accent : Line, RadiusButton);
            var inner = Fill(border.transform, "Nền thẻ", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(4, 4), Paper, RadiusButton);
            inner.raycastTarget = true;

            var btn = border.gameObject.AddComponent<Button>();
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            btn.targetGraphic = inner;
            btn.onClick.AddListener(() => SelectTheme(mode));

            ThemeSwatchPalette(mode, out var swBg, out var swCard, out var swAccent, out var swInk);
            var swW = size.x - 24f;
            var swBox = Fill(inner.transform, "Xem trước", new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -10), new Vector2(swW, 86), swBg, RadiusButton);
            Fill(swBox.transform, "Thẻ mẫu", new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -10), new Vector2(swW - 20f, 44), swCard, RadiusButton);
            Fill(swBox.transform, "Nhấn mẫu", new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -60), new Vector2(56, 16), swAccent, RadiusButton);
            Fill(swBox.transform, "Chữ mẫu 1", new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -20), new Vector2(swW - 60f, 8), swInk, RadiusButton);
            Fill(swBox.transform, "Chữ mẫu 2", new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -34), new Vector2(swW - 92f, 6), WithAlpha(swInk, 0.5f), RadiusButton);

            string title = mode == UiTheme.Mode.XanhMuc ? "Xanh mực VERA" : UiTheme.ModeLabel(mode);
            var lbl = GlassKit.Label(inner.transform, "Nhãn", title, 15, active ? Accent : Ink, active, TextAnchor.MiddleCenter, new Vector2(8, -102), new Vector2(size.x - 20f, 38));
            themeCardBorders[index] = border;
            themeCardLabels[index] = lbl;
        }

        static void ThemeSwatchPalette(UiTheme.Mode mode, out Color bg, out Color card, out Color accent, out Color ink)
        {
            switch (mode)
            {
                case UiTheme.Mode.Trang:
                    bg = MenuKit.Hex(0xf5f5f7);
                    card = MenuKit.Hex(0xffffff);
                    accent = MenuKit.Hex(0x0b63d1);
                    ink = MenuKit.Hex(0x1d1d1f);
                    break;
                case UiTheme.Mode.Den:
                    bg = MenuKit.Hex(0x08080a);
                    card = MenuKit.Hex(0x1c1c1f);
                    accent = MenuKit.Hex(0x4da3ff);
                    ink = MenuKit.Hex(0xf5f5f7);
                    break;
                default:
                    bg = MenuKit.Hex(0x0a1322);
                    card = MenuKit.Hex(0x111d31);
                    accent = MenuKit.Hex(0x33d6ff);
                    ink = MenuKit.Hex(0xeaf1ff);
                    break;
            }
        }

        void SelectSettingsTab(int idx)
        {
            currentSettingsTab = Mathf.Clamp(idx, 0, settingsPanes.Length - 1);
            for (int i = 0; i < settingsPanes.Length; i++)
            {
                bool on = i == currentSettingsTab;
                if (settingsPanes[i] != null) settingsPanes[i].SetActive(on);
                if (settingsTabs[i] != null) GlassKit.SetChip(settingsTabs[i], on);
            }
        }

        void RefreshSettings()
        {
            serverField.text = GameSession.ServerUrl;
            if (aiUrlField != null)
                aiUrlField.text = PlayerPrefs.GetString(AiClient.ServerUrlPrefKey, "http://127.0.0.1:8000");
            if (aiStatusText != null) aiStatusText.text = "";
            if (transToggle != null) transToggle.SetIsOnWithoutNotify(GlassKit.ReducedTransparency);
            if (motionToggle != null) motionToggle.SetIsOnWithoutNotify(ReducedMotion.IsEnabled);
            if (soundToggle != null) soundToggle.SetIsOnWithoutNotify(NutHieuUng.SoundEnabled);
            if (touchSensSlider != null)
                touchSensSlider.SetValueWithoutNotify(Mathf.Clamp(PlayerPrefs.GetFloat(TouchSensPrefKey, 0.18f), 0.05f, 0.50f));

            var modes = new[] { UiTheme.Mode.XanhMuc, UiTheme.Mode.Trang, UiTheme.Mode.Den };
            if (themeCardBorders != null)
            {
                for (int i = 0; i < modes.Length && i < themeCardBorders.Length; i++)
                {
                    bool on = UiTheme.Current == modes[i];
                    if (themeCardBorders[i] != null) themeCardBorders[i].color = on ? Accent : Line;
                    if (themeCardLabels[i] != null)
                    {
                        themeCardLabels[i].color = on ? Accent : Ink;
                        themeCardLabels[i].font = on ? GlassKit.Semibold : GlassKit.Regular;
                    }
                }
            }
            int clickMode = PlayerPrefs.GetInt(UiTheme.ClickModePrefKey, 0);
            if (clickImmediateChip != null) GlassKit.SetChip(clickImmediateChip, clickMode == 0);
            if (clickPanelChip != null) GlassKit.SetChip(clickPanelChip, clickMode == 1);
            SetChipRow(fontScaleChips, UiTheme.FontScaleMode);
            SetChipRow(panelSizeChips, UiTheme.PanelSizeMode);
            SelectSettingsTab(currentSettingsTab);
        }

        void OnTouchSensChanged(float v)
        {
            PlayerPrefs.SetFloat(TouchSensPrefKey, Mathf.Clamp(v, 0.05f, 0.50f));
            PlayerPrefs.Save();
        }

        void SelectTheme(UiTheme.Mode mode)
        {
            if (busy || UiTheme.Current == mode) return;
            UiTheme.Set(mode);
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        void SelectClickMode(int mode)
        {
            PlayerPrefs.SetInt(UiTheme.ClickModePrefKey, mode);
            PlayerPrefs.Save();
            if (clickImmediateChip != null) GlassKit.SetChip(clickImmediateChip, mode == 0);
            if (clickPanelChip != null) GlassKit.SetChip(clickPanelChip, mode == 1);
        }

        Button[] BuildChipRow(RectTransform parent, string key, string label, string[] options, float topY, float rowWidth, int active, System.Action<int> onSelect)
        {
            GlassKit.Label(parent, "Label" + key, label, 17, Ink, true, TextAnchor.UpperLeft, new Vector2(0, topY), new Vector2(rowWidth, 26));
            var chips = new Button[options.Length];
            float gap = 12f;
            float w = (rowWidth - gap * (options.Length - 1)) / options.Length;
            for (int i = 0; i < options.Length; i++)
            {
                int idx = i;
                chips[i] = GlassKit.MakeChip(parent, "Chip" + key + i, options[i], new Vector2(i * (w + gap), topY - 30f), new Vector2(w, 46), () => onSelect(idx), active == i);
            }
            return chips;
        }

        void SelectFontScale(int mode)
        {
            UiTheme.SetFontScale(mode);
            SetChipRow(fontScaleChips, UiTheme.FontScaleMode);
        }

        void SelectPanelSize(int mode)
        {
            UiTheme.SetPanelSize(mode);
            SetChipRow(panelSizeChips, UiTheme.PanelSizeMode);
        }

        static void SetChipRow(Button[] chips, int active)
        {
            if (chips == null) return;
            for (int i = 0; i < chips.Length; i++)
                if (chips[i] != null) GlassKit.SetChip(chips[i], active == i);
        }

        void BuildRoom(RectTransform p)
        {
            var s = NewScreen(p, Screen.Room, "Phòng học chung", "Phòng học chung", "Cùng vào một phòng lab với cả lớp.", ContentW, 560f);
            roomJoinField = MakeField(s, Screen.Room, "Ô mã phòng", "Mã phòng hoặc địa chỉ IP", -104, false);
            MakeButton(s, "Nút Vào phòng", "Vào phòng", new Vector2(0, -190), new Vector2(ContentW, 54), DoJoinRoom, 0);
            status[Screen.Room] = MakeStatus(s, "Thông báo phòng", -254);
            hostRelayButton = MakeButton(s, "Nút Mở phòng Internet", "Mở phòng qua Internet", new Vector2(0, -308), new Vector2(ContentW, 54), () => DoHostRoom(true), 1).gameObject;
            hostLanButton = MakeButton(s, "Nút Mở phòng LAN", "Mở phòng trong mạng LAN", new Vector2(0, -374), new Vector2(ContentW, 54), () => DoHostRoom(false), 1).gameObject;
            System.Action leave = () => { if (!busy) Show(Screen.Home); };
            MakeButton(s, "Nút quay lại từ phòng", "Quay lại", new Vector2(0, -440), new Vector2(ContentW, 54), leave, 1);
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
            var s = NewScreen(p, Screen.Loading, "Đang tải", "Đang mở phòng thí nghiệm", "Lần đầu có thể mất khoảng một phút trên máy yếu.", ContentW, 340f);
            var track = Fill(s, "Thanh nền", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -120), new Vector2(ContentW, 12), PaperHover, 10f);
            var fill = Stretch(track.rectTransform, "Thanh tiến độ", new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, Vector2.zero);
            loadBar = fill.gameObject.AddComponent<Image>();
            loadBar.sprite = GlassKit.Solid;
            loadBar.type = Image.Type.Sliced;
            loadBar.pixelsPerUnitMultiplier = 10f;
            loadBar.color = Accent;
            loadBar.raycastTarget = false;
            loadText = GlassKit.Label(s, "Phần trăm", "0 %", 17, InkDim, false, TextAnchor.UpperLeft, new Vector2(0, -144), new Vector2(ContentW, 28));
            GlassKit.Label(s, "Tiêu đề mẹo", "MẸO AN TOÀN PHÒNG THÍ NGHIỆM", 13, Accent, true, TextAnchor.UpperLeft, new Vector2(0, -196), new Vector2(ContentW, 20));
            tipText = GlassKit.Label(s, "Mẹo an toàn", MeoAnToan[UnityEngine.Random.Range(0, MeoAnToan.Length)], 17, Ink, false, TextAnchor.UpperLeft, new Vector2(0, -222), new Vector2(ContentW, 80));
        }

        Text tipText;
        static readonly string[] MeoAnToan =
        {
            "Luôn đeo kính bảo hộ và áo choàng khi làm việc với axit, bazơ hoặc dung môi.",
            "Pha loãng axit sunfuric: rót từ từ AXIT vào NƯỚC, không làm ngược lại.",
            "Không ngửi trực tiếp miệng bình hóa chất; dùng tay phẩy nhẹ hơi về phía mũi.",
            "Đun ống nghiệm thì hướng miệng ống ra xa người khác và xa chính mình.",
            "Hóa chất dễ cháy (ethanol, acetone) phải để xa ngọn lửa đèn cồn.",
            "Làm việc với khí độc (Cl₂, NO₂, H₂S) trong tủ hút.",
            "Hóa chất bắn vào mắt: rửa ngay bằng vòi rửa mắt ít nhất 15 phút.",
            "Không trả hóa chất thừa vào lọ gốc; đổ vào bình thu hồi đúng loại.",
        };

        InputField MakeField(Transform parent, Screen s, string name, string label, float y, bool password, float width = ContentW)
        {
            GlassKit.Label(parent, name + " (nhãn)", label, 16, Ink, true, TextAnchor.UpperLeft, new Vector2(0, y), new Vector2(width, 24));
            var size = new Vector2(width, FieldH);
            var outer = Fill(parent, name, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, y - 26), size, Line, RadiusButton);
            var inner = Fill(outer.transform, "Nền", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(3, 3), MenuKit.Hex(0xffffff), RadiusButton);
            inner.raycastTarget = true;
            var text = GlassKit.Label(outer.transform, "Chữ", "", 18, Ink, false, TextAnchor.MiddleLeft, new Vector2(14, 0), new Vector2(width - 28, FieldH));
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            var f = outer.gameObject.AddComponent<InputField>();
            f.textComponent = text;
            f.targetGraphic = inner;
            f.caretColor = Accent;
            f.customCaretColor = true;
            f.selectionColor = WithAlpha(Accent, 0.25f);
            var colors = f.colors;
            colors.normalColor = Paper;
            colors.highlightedColor = PaperHover;
            colors.pressedColor = PaperHover;
            colors.selectedColor = Color.Lerp(Paper, Accent, 0.08f);
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

        static Text MakeStatus(Transform parent, string name, float y, float width = ContentW)
        {
            return GlassKit.Label(parent, name, "", 16, Error, false, TextAnchor.UpperLeft, new Vector2(0, y), new Vector2(width, 46));
        }
    }
}