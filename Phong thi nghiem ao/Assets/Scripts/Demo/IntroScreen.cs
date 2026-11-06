using System.Collections;
using Khkt.Environment;
using Khkt.Menu;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static Khkt.Demo.GlassKit;

namespace Khkt.Demo
{
    /// <summary>
    /// Vào lab là dùng được mọi tính năng ngay, không phải chọn chế độ. Phím Esc mở bảng tạm dừng:
    /// tiếp tục hoặc về màn hình chính.
    /// </summary>
    public class IntroScreen : MonoBehaviour
    {
        public ModernUI ui;
        public LabProcedure procedure;
        public DesktopViewer viewer;
        public bool showOnStart = true;

        public bool IsOpen { get; private set; }

        GameObject root;
        bool captured;

        void Start()
        {
            UiBridge.EnsureInteractive(ui.Canvas);
            var glass = Glass(ui.glassShader != null ? ui.glassShader : Shader.Find("Khkt/KinhMo"));
            var dimRt = Rect(ui.Canvas.transform, "Bảng tạm dừng", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4000, 3000));
            var dim = dimRt.gameObject.AddComponent<Image>();
            dim.color = new Color(0.02f, 0.05f, 0.09f, 0.55f);
            root = dimRt.gameObject;

            var size = new Vector2(620, 400);
            var panel = Panel(dimRt, "Khung", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size, glass, 1.1f);
            var p = panel.transform;
            // nền tối thêm dưới lớp kính: chữ phải đọc được cả khi phía sau là tường sáng
            var plate = Panel(p, "Nền tối", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size, null, 1.1f);
            plate.color = new Color(0.03f, 0.05f, 0.09f, 0.72f);

            Label(p, "Tên", "VERA Lab", 40, Ink, true, TextAnchor.UpperLeft, new Vector2(48, -38), new Vector2(520, 56));
            Label(p, "Trạng thái", "Đang tạm dừng", 18, Dim, false, TextAnchor.UpperLeft, new Vector2(50, -96), new Vector2(520, 28));
            MakeButton(p, "Tiếp tục", "Tiếp tục", new Vector2(50, -142), new Vector2(520, 50), Close, true, 18);
            if (GameSession.MenuAvailable)
                MakeButton(p, "Về màn hình chính", "Về màn hình chính", new Vector2(50, -204), new Vector2(520, 50), GameSession.ReturnToMenu, false, 18);
            Label(p, "Phím", "<b>Rê chuột</b> nhìn  ·  <b>WASD</b> đi  ·  <b>Chuột trái</b> cầm, dùng  ·  <b>B</b> tra cứu hóa chất  ·  <b>T</b> trợ lý AI  ·  <b>1–9</b> các khu  ·  <b>H</b> trợ giúp  ·  <b>Esc</b> menu",
                  15, Dim, false, TextAnchor.UpperCenter, new Vector2(50, -282), new Vector2(520, 80));
            root.SetActive(false);

            bool fromMenu = GameSession.EnterLabPending;
            GameSession.EnterLabPending = false;
            if (fromMenu || (showOnStart && !Application.isBatchMode)) StartCoroutine(EnterLab());
        }

        /// <summary>Đợi các Start khác xong rồi mới dịch chuyển tới bàn thí nghiệm.</summary>
        IEnumerator EnterLab()
        {
            yield return null;
            procedure.Stop();
            viewer.GoTo(2);
            LabMessages.Say("Nhấn B để tìm hóa chất, T để hỏi robot, H để xem trợ giúp", LabMessages.Tone.Info);
        }

        public void Open()
        {
            IsOpen = true;
            root.SetActive(true);
            root.transform.SetAsLastSibling();
            UiBridge.SetCapture(ref captured, true);
        }

        public void Close()
        {
            IsOpen = false;
            root.SetActive(false);
            UiBridge.SetCapture(ref captured, false);
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
