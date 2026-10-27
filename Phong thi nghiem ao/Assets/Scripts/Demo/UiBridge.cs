using Khkt.Environment;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Khkt.Demo
{
    /// <summary>
    /// Làm cho giao diện kính mờ bấm được: thêm EventSystem (Input System) và GraphicRaycaster cho canvas,
    /// và giữ con trỏ thả ra khi có bảng đang mở (DesktopViewer.UiCapture).
    /// </summary>
    public static class UiBridge
    {
        public static void EnsureInteractive(Canvas canvas)
        {
            if (Object.FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("Hệ thống sự kiện giao diện", typeof(EventSystem), typeof(InputSystemUIInputModule));
                Object.DontDestroyOnLoad(es);
            }
            if (canvas != null && canvas.GetComponent<GraphicRaycaster>() == null) canvas.gameObject.AddComponent<GraphicRaycaster>();
        }

        /// <summary>Mở/đóng một bảng cần con trỏ: tăng/giảm UiCapture đúng một lần mỗi trạng thái.</summary>
        public static void SetCapture(ref bool captured, bool want)
        {
            if (captured == want) return;
            captured = want;
            if (!want) LastCloseFrame = Time.frameCount;
            DesktopViewer.UiCapture = Mathf.Max(0, DesktopViewer.UiCapture + (want ? 1 : -1));
        }

        /// <summary>Khung hình vừa đóng một bảng (để phím Esc không vừa đóng bảng vừa mở menu).</summary>
        public static int LastCloseFrame { get; private set; } = -1;

        public static bool Typing
        {
            get
            {
                var es = EventSystem.current;
                if (es == null || es.currentSelectedGameObject == null) return false;
                var f = es.currentSelectedGameObject.GetComponent<InputField>();
                return f != null && f.isFocused;
            }
        }
    }
}
