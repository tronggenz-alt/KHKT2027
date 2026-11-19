using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Khkt.Demo.Bio.Portal
{
    /// <summary>
    /// BioWebViewBridge:
    ///   Cầu nối hiển thị Phòng Thí nghiệm Sinh học 3D (biology-lab-3d.html):
    ///   • Trên iOS: mở WKWebView toàn màn hình thông qua plugin native BioWebViewPlugin.mm.
    ///     Hỗ trợ WebGL, nạp file:// nội bộ từ StreamingAssets/biology_lab/biology-lab-3d.html,
    ///     bắt sự kiện nút đóng (veralab://close hoặc message 'unity') và nút thoát nổi '← Về Lab'.
    ///   • Trên macOS (bản app): WKWebView phủ cửa sổ app qua Plugins/macOS/BioWebViewMac.bundle, nút '← Về Lab'
    ///     (19/11, thay mô-đun G4 viết lại bằng C#; mã G4 vẫn giữ nhưng không dùng).
    ///   • Trên Windows / Unity Editor: mở trang qua máy chủ nội bộ http://127.0.0.1 (MayChuWebNoiBo) bằng trình duyệt
    ///     mặc định và hiện nút '← Trở về Phòng Lab 3D'. Editor batchmode (kiểm thử) chỉ đánh dấu mở, không mở trình duyệt.
    ///   • Nhận callback OnWebViewClosed từ native hoặc nút GUI để phục hồi trạng thái Lab 3D.
    /// </summary>
    public class BioWebViewBridge : MonoBehaviour
    {
        private static BioWebViewBridge s_instance;
        private static Action s_onClosedCallback;

        [Header("Trạng thái")]
        [SerializeField] private bool _isOpen = false;
        private bool showEditorOverlay = false;

        public static bool IsOpen => s_instance != null && s_instance._isOpen;

        public static BioWebViewBridge Instance => EnsureInstance();

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void _OpenBioWebView(string urlOrRelativePath);

        [DllImport("__Internal")]
        private static extern void _CloseBioWebView();
#endif
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        [DllImport("BioWebViewMac")] private static extern int _BioMac_Open(string path);
        [DllImport("BioWebViewMac")] private static extern void _BioMac_Close();
        [DllImport("BioWebViewMac")] private static extern int _BioMac_ConsumeClosed();
        private bool nativeMac;
#endif
        private bool captured;
        private CursorLockMode prevLock;
        private bool prevVisible;

        /// <summary>Cách trang đang được hiện: "ios", "mac", "trinh_duyet", "khong_giao_dien" (batchmode) — cho kiểm thử.</summary>
        public static string CachHien { get; private set; } = "";
        public const string HtmlRelPath = "biology_lab/biology-lab-3d.html";

        private void Awake()
        {
            if (s_instance == null)
            {
                s_instance = this;
                gameObject.name = "BioWebViewBridge";
                DontDestroyOnLoad(gameObject);
            }
            else if (s_instance != this)
            {
                Destroy(gameObject);
            }
        }

        private static BioWebViewBridge EnsureInstance()
        {
            if (s_instance != null) return s_instance;

#if UNITY_2023_1_OR_NEWER || UNITY_6000_0_OR_NEWER
            s_instance = FindAnyObjectByType<BioWebViewBridge>();
#else
            s_instance = FindObjectOfType<BioWebViewBridge>();
#endif
            if (s_instance == null)
            {
                var go = new GameObject("BioWebViewBridge");
                s_instance = go.AddComponent<BioWebViewBridge>();
                DontDestroyOnLoad(go);
            }
            return s_instance;
        }

        /// <summary>
        /// Mở Phòng Thí nghiệm Sinh học 3D trong WebView (iOS) hoặc trình duyệt (Editor/macOS).
        /// </summary>
        /// <param name="onClosed">Callback được kích hoạt khi người dùng đóng phòng thí nghiệm sinh học.</param>
        public static void Open(Action onClosed = null)
        {
            var bridge = EnsureInstance();
            s_onClosedCallback = onClosed;
            bridge._isOpen = true;

            string fullPath = Path.Combine(Application.streamingAssetsPath, HtmlRelPath);
            Debug.Log($"[BioWebViewBridge] Opening Biology Lab 3D at: {fullPath}");
            bridge.GiuGiaoDien(true);

#if UNITY_IOS && !UNITY_EDITOR
            CachHien = "ios";
            _OpenBioWebView(fullPath);
#else
            bool xong = false;
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
            try { bridge.nativeMac = _BioMac_Open(fullPath) == 1; }
            catch (Exception e) { Debug.LogWarning("[BioWebViewBridge] BioWebViewMac lỗi: " + e.Message); bridge.nativeMac = false; }
            if (bridge.nativeMac) { CachHien = "mac"; xong = true; }
#endif
            if (!xong)
            {
                if (Application.isBatchMode)
                {
                    CachHien = "khong_giao_dien";
                }
                else
                {
                    string url = MayChuWebNoiBo.Bat(Path.GetDirectoryName(fullPath))
                        ? MayChuWebNoiBo.Url(Path.GetFileName(fullPath))
                        : new Uri(fullPath).AbsoluteUri;
                    Application.OpenURL(url);
                    CachHien = "trinh_duyet";
                    bridge.showEditorOverlay = true;
                }
            }
#endif
        }

        /// <summary>Khi trang mở: thả con trỏ và chặn điều khiển lab (UiCapture); khi đóng: trả lại như cũ.</summary>
        void GiuGiaoDien(bool giu)
        {
            if (giu == captured) return;
            captured = giu;
            if (giu)
            {
                prevLock = Cursor.lockState; prevVisible = Cursor.visible;
                Khkt.Environment.DesktopViewer.UiCapture++;
                if (!Application.isBatchMode) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            }
            else
            {
                Khkt.Environment.DesktopViewer.UiCapture = Mathf.Max(0, Khkt.Environment.DesktopViewer.UiCapture - 1);
                if (!Application.isBatchMode) { Cursor.lockState = prevLock; Cursor.visible = prevVisible; }
            }
        }

        /// <summary>
        /// Đóng WebView (iOS) hoặc đóng mô-đun Giải phẫu 3D trong ứng dụng (Mac/Windows/Editor), phục hồi lab.
        /// </summary>
        public static void Close()
        {
            if (s_instance == null || !s_instance._isOpen) return;

#if UNITY_IOS && !UNITY_EDITOR
            _CloseBioWebView();
#else
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
            if (s_instance.nativeMac)
            {
                s_instance.nativeMac = false;
                _BioMac_Close();
                _BioMac_ConsumeClosed();
            }
#endif
            s_instance.showEditorOverlay = false;
            s_instance.OnWebViewClosed(string.Empty);
#endif
        }

        /// <summary>
        /// Được gọi từ Native iOS (UnitySendMessage) hoặc GUI overlay khi WebView đóng lại.
        /// </summary>
        /// <param name="message">Nội dung thông điệp (tùy chọn)</param>
        public void OnWebViewClosed(string message = "")
        {
            if (!_isOpen && !showEditorOverlay) return;

            Debug.Log($"[BioWebViewBridge] OnWebViewClosed received (message: '{message}'). Restoring main lab.");

            _isOpen = false;
            showEditorOverlay = false;
            CachHien = "";
            GiuGiaoDien(false);

            var callback = s_onClosedCallback;
            s_onClosedCallback = null;

            try
            {
                callback?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private void Update()
        {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
            if (nativeMac && _BioMac_ConsumeClosed() == 1)
            {
                nativeMac = false;
                OnWebViewClosed("mac_closed");
                return;
            }
#endif
            if (!showEditorOverlay) return;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                Close();
            }
#else
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
            }
#endif
        }

        private void OnGUI()
        {
            if (!showEditorOverlay) return;

            int prevDepth = GUI.depth;
            GUI.depth = -1000;

            float bannerWidth = 440f;
            float bannerHeight = 88f;
            float x = (Screen.width - bannerWidth) * 0.5f;
            float y = 24f;

            // Nền hộp thông báo
            var boxStyle = new GUIStyle(GUI.skin.box);
            GUI.Box(new Rect(x, y, bannerWidth, bannerHeight), GUIContent.none, boxStyle);

            // Tiêu đề trạng thái
            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };
            labelStyle.normal.textColor = new Color(0.13f, 0.77f, 0.93f); // Cyan #22D3EE

            GUI.Label(new Rect(x + 10, y + 8, bannerWidth - 20, 22), "Phòng Sinh học 3D đang mở trên trình duyệt", labelStyle);

            // Nút bấm quay về
            var btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };

            if (GUI.Button(new Rect(x + 40, y + 36, bannerWidth - 80, 38), "← Trở về Phòng Lab 3D", btnStyle))
            {
                Close();
            }

            GUI.depth = prevDepth;
        }

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                GiuGiaoDien(false);
                s_instance = null;
            }
        }
    }
}
