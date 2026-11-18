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
    ///   • Trên Unity Editor / macOS Standalone: mở biology-lab-3d.html trên trình duyệt mặc định
    ///     và hiển thị nút GUI overlay '← Trở về Phòng Lab 3D' để kiểm thử thuận tiện.
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

            string htmlRelPath = "biology_lab/biology-lab-3d.html";
            string fullPath = Path.Combine(Application.streamingAssetsPath, htmlRelPath);

            Debug.Log($"[BioWebViewBridge] Opening Biology Lab 3D at: {fullPath}");

#if UNITY_IOS && !UNITY_EDITOR
            _OpenBioWebView(fullPath);
#else
            // G4: Mac, Windows và Unity Editor mở mô-đun Giải phẫu người 3D trực tiếp trong ứng dụng, không mở trình duyệt ngoài.
            Khkt.Demo.Bio.GiaiPhau.GiaiPhauMoDunImpl.Mo(() =>
            {
                if (s_instance != null) s_instance.OnWebViewClosed("in_app_closed");
            });
#endif
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
            if (Khkt.Demo.Bio.GiaiPhau.GiaiPhauMoDunImpl.DangMo)
            {
                Khkt.Demo.Bio.GiaiPhau.GiaiPhauMoDunImpl.Dong();
            }
            else
            {
                s_instance.showEditorOverlay = false;
                s_instance.OnWebViewClosed(string.Empty);
            }
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

            GUI.Label(new Rect(x + 10, y + 8, bannerWidth - 20, 22), "🧪 Phòng Sinh học 3D đang mở trên trình duyệt", labelStyle);

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
                s_instance = null;
            }
        }
    }
}
