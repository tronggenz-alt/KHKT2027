using System.Collections.Generic;
using Khkt.Demo;
using Khkt.Input;
using UnityEngine;
using UnityEngine.UI;

namespace Khkt.Menu.Dashboard
{
    /// <summary>
    /// Trang "Tay cầm" ở sảnh chính: thử tay cầm VERA trước khi vào lab.
    ///  • Kết nối: dùng ControllerLink chung của sảnh (USB tự dò cổng, Wi-Fi cổng 4210), số gói/giây, nhiệt độ, thông báo firmware.
    ///  • Hướng: mô hình thật VERA Band 2 ×1.3 (VeraModel) quay theo tay cầm đã bù căn trục BMI160, yaw / pitch / roll so với
    ///    lúc bấm "Đặt hướng về 0"; cần gạt nghiêng theo joystick, mặt tối / cò sáng khi chạm A / B.
    ///  • Căn joystick (6 bước) và căn trục BMI160 (2 bước): ControllerCalPanel.
    ///  • Joystick, nhấn cần, 2 nút cảm ứng A, B; số đo con quay, gia tốc.
    ///  • Thao tác thử: danh sách tự đánh dấu khi làm đúng (đẩy cần 4 hướng, chạm A, B, nghiêng, xoay...).
    /// Nguồn tay cầm do ControllerLink giữ trong suốt thời gian ở sảnh và trả lại khi vào lab.
    /// </summary>
    public class PageController : MonoBehaviour
    {
        public const string IconController = "";

        DashboardShell shell;
        RectTransform page;

        // nguồn: kết nối chung của sảnh
        ControllerLink Link => shell != null ? shell.Link : null;
        ControllerSource active;
        ControllerCalPanel cal;
        Text mountText;

        // giao diện
        Image statusDot;
        Text statusText, usbLine, wifiLine, rateLine, fwLine;
        RawImage view3d;
        Text angles;
        RectTransform joyDot;
        Text joyText, pressText;
        Image barA, barB, pressDot;
        Text touchAText, touchBText;
        Text rawText, doneCount;
        readonly List<(string name, Image icon, Text label, System.Func<bool> test)> checks = new List<(string, Image, Text, System.Func<bool>)>();
        readonly HashSet<int> done = new HashSet<int>();
        Image[] checkBg;

        // số đo hiện tại (đã tính cho danh sách thử)
        float yaw, pitch, roll;
        ControllerState s;

        const float JoyR = 74f, BarW = 230f;

        public void Build(DashboardShell owner, RectTransform root)
        {
            shell = owner;
            page = root;
            GlassKit.Label(root, "Tiêu đề", "Thử tay cầm", 36, MenuKit.Ink, true, TextAnchor.UpperLeft, new Vector2(56, -48), new Vector2(900, 50));
            GlassKit.Label(root, "Mô tả", "Cắm tay cầm VERA qua cáp USB hoặc bật Wi-Fi, rồi làm lần lượt các thao tác bên dưới.",
                           18, MenuKit.InkDim, false, TextAnchor.UpperLeft, new Vector2(56, -100), new Vector2(1200, 30));
            var grid = MenuKit.Stretch(root, "Lưới", Vector2.zero, Vector2.one, new Vector2(56, 32), new Vector2(-56, -146));

            BuildConnection(MenuKit.Body(MenuKit.Card(grid, "Thẻ kết nối", 0f, 0.31f, 0, 360, 24, false)));
            BuildOrientation(MenuKit.Body(MenuKit.Card(grid, "Thẻ hướng", 0.31f, 0.65f, 0, 360, 24, false)));
            BuildStick(MenuKit.Body(MenuKit.Card(grid, "Thẻ joystick", 0.65f, 1f, 0, 360, 24, false)));
            BuildChecklist(MenuKit.Body(MenuKit.Card(grid, "Thẻ thao tác", 0f, 0.65f, -384, 300, 24, false)));
            BuildRaw(MenuKit.Body(MenuKit.Card(grid, "Thẻ số đo", 0.65f, 1f, -384, 300, 24, false)));
            cal = ControllerCalPanel.Create(root, () => Link);
        }

        /// <summary>Bảng căn (cho kiểm thử).</summary>
        public ControllerCalPanel Cal => cal;

        static Text H(Transform p, string t) =>
            GlassKit.Label(p, "Tiêu đề thẻ", t, 19, MenuKit.Ink, true, TextAnchor.UpperLeft, new Vector2(22, -18), new Vector2(420, 28));

        static Text L(Transform p, string name, float y, int size = 15, bool bold = false, float w = 420) =>
            GlassKit.Label(p, name, "", size, bold ? MenuKit.Ink : MenuKit.InkDim, bold, TextAnchor.UpperLeft, new Vector2(22, y), new Vector2(w, size + 10));

        // ---------------------------------------------------------------- kết nối
        void BuildConnection(RectTransform b)
        {
            H(b, "Kết nối");
            statusDot = MenuKit.Fill(b, "Đèn", new Vector2(0, 1), new Vector2(0, 1), new Vector2(22, -62), new Vector2(18, 18), MenuKit.Line, 1f);
            statusDot.sprite = GlassKit.SolidCircle;
            statusDot.type = Image.Type.Simple;
            statusText = GlassKit.Label(b, "Trạng thái", "", 20, MenuKit.Ink, true, TextAnchor.UpperLeft, new Vector2(50, -58), new Vector2(380, 30));
            usbLine = L(b, "USB", -100);
            wifiLine = L(b, "Wi-Fi", -126);
            rateLine = L(b, "Gói", -152);
            fwLine = L(b, "Firmware", -178, 14);
            fwLine.horizontalOverflow = HorizontalWrapMode.Wrap;
            fwLine.rectTransform.sizeDelta = new Vector2(420, 44);

            Btn(b, "Nút căn joystick", "Căn joystick", new Vector2(22, -238), () => cal.Open(ControllerCalPanel.Kind.Joystick), 0);
            Btn(b, "Nút căn trục BMI160", "Căn trục BMI160", new Vector2(208, -238), () => cal.Open(ControllerCalPanel.Kind.Mount), 0);
            Btn(b, "Nút cân con quay", "Cân con quay", new Vector2(22, -292), () => Send("c"), 1);
            Btn(b, "Nút đặt hướng", "Đặt hướng về 0", new Vector2(208, -292), SetZero, 1);
        }

        static void Btn(Transform p, string name, string text, Vector2 pos, System.Action click, int style)
        {
            var btn = MenuKit.MakeButton(p, name, text, pos, new Vector2(176, 42), click, style);
            var t = btn.transform.Find("Chữ")?.GetComponent<Text>();
            if (t != null) t.fontSize = 15;
        }

        void Send(string cmd)
        {
            Link?.Send(cmd);
            if (cmd == "c") Flash("Để yên tay cầm 2 giây, không chạm nút");
        }

        string flash;
        float flashUntil;
        void Flash(string t) { flash = t; flashUntil = Time.unscaledTime + 8f; }

        void SetZero() => Link?.SetZero();

        // ---------------------------------------------------------------- hướng 3D
        void BuildOrientation(RectTransform b)
        {
            H(b, "Hướng tay cầm");
            var area = GlassKit.Rect(b, "Ảnh 3D", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -52), new Vector2(420, 250));
            view3d = area.gameObject.AddComponent<RawImage>();
            view3d.raycastTarget = false;
            angles = GlassKit.Label(b, "Góc", "", 16, MenuKit.Ink, true, TextAnchor.UpperCenter, new Vector2(0, -312), new Vector2(0, 26), new Vector2(0.5f, 1));
            angles.rectTransform.pivot = new Vector2(0.5f, 1);
            angles.rectTransform.sizeDelta = new Vector2(440, 26);
            mountText = GlassKit.Label(b, "Căn trục", "", 13, MenuKit.InkDim, false, TextAnchor.UpperRight, new Vector2(-22, -22), new Vector2(240, 22), new Vector2(1, 1));
            mountText.rectTransform.pivot = new Vector2(1, 1);
        }

        // ---------------------------------------------------------------- joystick, nút
        void BuildStick(RectTransform b)
        {
            H(b, "Joystick và nút");
            var ring = MenuKit.Fill(b, "Vòng cần", new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(110, -150), new Vector2(JoyR * 2, JoyR * 2), MenuKit.PaperHover, 1f);
            ring.sprite = GlassKit.SolidCircle;
            ring.type = Image.Type.Simple;
            var cross1 = MenuKit.Fill(ring.transform, "Kẻ ngang", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(JoyR * 2 - 16, 1), MenuKit.Line, 0f);
            var cross2 = MenuKit.Fill(ring.transform, "Kẻ dọc", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1, JoyR * 2 - 16), MenuKit.Line, 0f);
            cross1.raycastTarget = cross2.raycastTarget = false;
            var dot = MenuKit.Fill(ring.transform, "Chấm cần", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 30), MenuKit.Accent, 1f);
            dot.sprite = GlassKit.SolidCircle;
            dot.type = Image.Type.Simple;
            joyDot = dot.rectTransform;
            joyText = GlassKit.Label(b, "Số cần", "", 14, MenuKit.InkDim, false, TextAnchor.UpperCenter, new Vector2(36, -236), new Vector2(150, 22));
            pressDot = MenuKit.Fill(b, "Đèn nhấn", new Vector2(0, 1), new Vector2(0, 1), new Vector2(58, -268), new Vector2(14, 14), MenuKit.Line, 1f);
            pressDot.sprite = GlassKit.SolidCircle;
            pressDot.type = Image.Type.Simple;
            pressText = GlassKit.Label(b, "Nhấn cần", "Nhấn cần", 14, MenuKit.InkDim, false, TextAnchor.UpperLeft, new Vector2(80, -264), new Vector2(120, 22));

            float x = 222;
            touchAText = GlassKit.Label(b, "Chữ A", "", 15, MenuKit.Ink, true, TextAnchor.UpperLeft, new Vector2(x, -70), new Vector2(BarW, 22));
            barA = Bar(b, "Thanh A", new Vector2(x, -98));
            touchBText = GlassKit.Label(b, "Chữ B", "", 15, MenuKit.Ink, true, TextAnchor.UpperLeft, new Vector2(x, -142), new Vector2(BarW, 22));
            barB = Bar(b, "Thanh B", new Vector2(x, -170));
            GlassKit.Label(b, "Ghi chú", "A: ngón cái · B: ngón trỏ (cầm vật). Vạch mảnh: ngưỡng bấm 50%.", 13, MenuKit.InkDim, false,
                           TextAnchor.UpperLeft, new Vector2(x, -206), new Vector2(BarW, 40));
        }

        static Image Bar(Transform p, string name, Vector2 pos)
        {
            var bg = MenuKit.Fill(p, name, new Vector2(0, 1), new Vector2(0, 1), pos, new Vector2(BarW, 16), MenuKit.PaperHover, 1f);
            var fill = MenuKit.Fill(bg.transform, "Mức", new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(0, 16), MenuKit.Accent, 1f);
            MenuKit.Fill(bg.transform, "Ngưỡng", new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(BarW * 0.5f, 0), new Vector2(2, 22), MenuKit.Ink, 0f);
            return fill;
        }

        // ---------------------------------------------------------------- danh sách thử
        void BuildChecklist(RectTransform b)
        {
            H(b, "Thao tác thử");
            doneCount = GlassKit.Label(b, "Đếm", "", 16, MenuKit.Accent, true, TextAnchor.UpperRight, new Vector2(-150, -20), new Vector2(200, 26), new Vector2(1, 1));
            doneCount.rectTransform.pivot = new Vector2(1, 1);
            var reset = MenuKit.MakeButton(b, "Nút làm lại", "Làm lại", new Vector2(-22, -14), new Vector2(110, 36), () => { done.Clear(); SetZero(); }, 1, MenuKit.RadiusButton, new Vector2(1, 1));
            reset.transform.Find("Chữ").GetComponent<Text>().fontSize = 14;

            Add("Đẩy cần lên (tiến)", () => s.Joy2.y > 0.7f);
            Add("Đẩy cần xuống (lùi)", () => s.Joy2.y < -0.7f);
            Add("Đẩy cần sang trái", () => s.Joy2.x < -0.7f);
            Add("Đẩy cần sang phải", () => s.Joy2.x > 0.7f);
            Add("Nhấn cần", () => s.Has(VeraFlags.StickPress) || s.Pressed(ControllerButtons.Joy2Press));
            Add("Chạm nút A (ngón cái)", () => s.Has(VeraFlags.TouchA));
            Add("Chạm nút B (ngón trỏ)", () => s.Has(VeraFlags.TouchB) || s.Pressed(ControllerButtons.Trigger));
            Add("Chúc mũi lên 30°", () => pitch > 30);
            Add("Chúc mũi xuống 30°", () => pitch < -30);
            Add("Nghiêng sang trái 30°", () => roll < -30);
            Add("Nghiêng sang phải 30°", () => roll > 30);
            Add("Xoay sang trái 45°", () => yaw < -45);
            Add("Xoay sang phải 45°", () => yaw > 45);

            checkBg = new Image[checks.Count];
            for (int i = 0; i < checks.Count; i++)
            {
                int col = i % 3, row = i / 3;
                var pos = new Vector2(22 + col * 236, -60 - row * 46);
                var bg = MenuKit.Fill(b, "Ô " + checks[i].name, new Vector2(0, 1), new Vector2(0, 1), pos, new Vector2(226, 38), MenuKit.PaperAlt, MenuKit.RadiusButton);
                checkBg[i] = bg;
                var icon = MenuKit.Icon(bg.transform, MenuKit.IconDone, 15, MenuKit.Line, new Vector2(6, 0), new Vector2(30, 38));
                var label = GlassKit.Label(bg.transform, "Chữ", checks[i].name, 15, MenuKit.InkDim, false, TextAnchor.MiddleLeft, new Vector2(36, 0), new Vector2(186, 38));
                var c = checks[i];
                checks[i] = (c.name, null, label, c.test);
                icons.Add(icon);
            }
        }

        readonly List<Text> icons = new List<Text>();

        void Add(string name, System.Func<bool> test) => checks.Add((name, null, null, test));

        // ---------------------------------------------------------------- số đo thô
        void BuildRaw(RectTransform b)
        {
            H(b, "Số đo");
            rawText = GlassKit.Label(b, "Số", "", 15, MenuKit.Ink, false, TextAnchor.UpperLeft, new Vector2(22, -58), new Vector2(440, 220));
            rawText.font = GlassKit.Regular;
            rawText.lineSpacing = 1.25f;
        }

        // ================================================================ mở, đóng
        public void Open()
        {
            MenuAnim.Enter(page.Find("Tiêu đề") as RectTransform, 0f, 12f, 0.28f);
            MenuAnim.Enter(page.Find("Mô tả") as RectTransform, 0.04f, 12f, 0.28f);
            MenuAnim.Enter(page.Find("Lưới") as RectTransform, 0.08f);
        }

        void OnEnable()
        {
            if (page == null) return;
            if (Link != null) Link.LargeViewWanted = true;
        }

        void OnDisable()
        {
            if (Link != null) Link.LargeViewWanted = false;
            cal?.Close();
            active = null;
        }

        // ================================================================ mỗi khung
        void Update()
        {
            if (page == null) return;
            float now = Time.unscaledTime;
            var link = Link;
            if (link == null) return;
            var usb = link.Usb;
            var wifi = link.Wifi;
            bool usbLive = link.UsbLive, wifiLive = link.WifiLive;
            active = link.Active;
            if (view3d.texture != link.LargeView) view3d.texture = link.LargeView;
            mountText.text = link.MountCalibrated ? "Đã căn trục BMI160" : "Chưa căn trục BMI160";
            mountText.color = link.MountCalibrated ? MenuKit.Bio : MenuKit.Warn;

            // ----- kết nối
            Color c = active != null ? (active.Latest.Has(VeraFlags.ImuError) ? MenuKit.Warn : MenuKit.Bio) : MenuKit.Error;
            statusDot.color = c;
            statusText.text = active == null ? "Chưa thấy tay cầm" : active.Latest.Has(VeraFlags.ImuError) ? "Đã nối · lỗi cảm biến hướng" : "Đã nối, đang nhận dữ liệu";
            statusText.color = c;
            usbLine.text = usb == null ? "USB: đang dùng ở nơi khác" : "USB: " + (usbLive ? $"đang nhận ({usb.ControllerName})" : usb.IsConnected ? "đã mở cổng, chờ dữ liệu" : "chưa thấy (cắm cáp, tắt Serial Monitor)");
            wifiLine.text = wifi == null ? "Wi-Fi: đang dùng ở nơi khác" : "Wi-Fi: " + (wifiLive ? $"đang nhận ({wifi.ControllerName})" : "chưa có gói (cổng 4210)");
            int rate = link.PacketsPerSecond;
            rateLine.text = active == null ? "" : $"{rate} gói/giây · nhiệt độ chip {active.Latest.TempC:0.0} °C";
            string fw = usbLive ? usb.LastFirmwareMessage : wifiLive ? wifi.LastFirmwareMessage : "";
            fwLine.text = now < flashUntil ? flash : string.IsNullOrEmpty(fw) ? "" : "Tay cầm: " + fw;
            fwLine.color = now < flashUntil ? MenuKit.Accent : MenuKit.InkDim;

            if (active == null)
            {
                angles.text = "Chưa có dữ liệu";
                joyText.text = "chờ tay cầm";
                touchAText.text = "Nút A: chờ tay cầm";
                touchBText.text = "Nút B: chờ tay cầm";
                rawText.text = "Chưa có dữ liệu. Cắm tay cầm qua USB (đóng Serial Monitor và phòng thử Python nếu đang mở),\n" +
                               "hoặc bật Wi-Fi của tay cầm cùng mạng với máy này.";
                return;
            }
            s = active.Latest;

            // ----- hướng (đã bù căn trục, so với lúc đặt về 0); mô hình 3D do ControllerLink quay
            var rel = link.Pose;
            Vector3 f = rel * Vector3.forward, r = rel * Vector3.right;
            pitch = Mathf.Asin(Mathf.Clamp(f.y, -1, 1)) * Mathf.Rad2Deg;
            yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
            roll = Mathf.Asin(Mathf.Clamp(-r.y, -1, 1)) * Mathf.Rad2Deg;
            angles.text = $"Xoay {yaw:+0;-0;0}°   ·   Chúc {pitch:+0;-0;0}°   ·   Nghiêng {roll:+0;-0;0}°";

            // ----- joystick, nút
            joyDot.anchoredPosition = Vector2.ClampMagnitude(s.Joy2, 1f) * (JoyR - 15);
            joyText.text = $"x {s.Joy2.x:+0.00;-0.00}  y {s.Joy2.y:+0.00;-0.00}";
            bool press = s.Has(VeraFlags.StickPress) || s.Pressed(ControllerButtons.Joy2Press);
            pressDot.color = press ? MenuKit.Accent : MenuKit.Line;
            pressText.color = press ? MenuKit.Accent : MenuKit.InkDim;
            Touch(barA, touchAText, "Nút A", s.TouchA, s.Has(VeraFlags.TouchA));
            Touch(barB, touchBText, "Nút B", s.TouchB, s.Has(VeraFlags.TouchB) || s.Pressed(ControllerButtons.Trigger));

            // ----- danh sách thử
            for (int i = 0; i < checks.Count; i++)
            {
                if (!done.Contains(i) && checks[i].test()) done.Add(i);
                bool ok = done.Contains(i);
                checkBg[i].color = ok ? MenuKit.BioSoft : MenuKit.PaperAlt;
                icons[i].color = ok ? MenuKit.Bio : MenuKit.Line;
                checks[i].label.color = ok ? MenuKit.Ink : MenuKit.InkDim;
            }
            doneCount.text = $"Đạt {done.Count}/{checks.Count}";

            // ----- số đo
            var g = s.GyroDps;
            var a = s.AccelG;
            var flags = new List<string>();
            if (s.Has(VeraFlags.Still)) flags.Add("đang nằm yên");
            if (s.Has(VeraFlags.Calibrating)) flags.Add("đang hiệu chuẩn");
            if (s.Has(VeraFlags.ImuError)) flags.Add("LỖI cảm biến hướng");
            rawText.text = $"Con quay  x {g.x,7:0.0}   y {g.y,7:0.0}   z {g.z,7:0.0}  °/s\n" +
                           $"Gia tốc   x {a.x,6:0.00}   y {a.y,6:0.00}   z {a.z,6:0.00}  g   (|a| = {a.magnitude:0.00})\n" +
                           $"Cảm ứng   A {s.TouchA,3}   B {s.TouchB,3}   (0–255)\n" +
                           $"Trạng thái: {(flags.Count > 0 ? string.Join(", ", flags) : "bình thường")}\n" +
                           $"Joystick firmware x {s.JoyRaw.x:+0.00;-0.00} y {s.JoyRaw.y:+0.00;-0.00} → đã căn x {s.Joy2.x:+0.00;-0.00} y {s.Joy2.y:+0.00;-0.00}" +
                           (VeraJoyCal.Current.Valid ? "" : " (chưa căn)");
        }

        static void Touch(Image bar, Text label, string name, byte level, bool on)
        {
            bar.rectTransform.sizeDelta = new Vector2(BarW * level / 255f, 16);
            bar.color = on ? MenuKit.Bio : MenuKit.Accent;
            label.text = $"{name}: {(on ? "đang chạm" : "không chạm")}  ({level * 100 / 255}%)";
            label.color = on ? MenuKit.Bio : MenuKit.Ink;
        }
    }
}
