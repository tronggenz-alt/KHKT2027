using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Khkt.Input
{
    /// <summary>
    /// Gộp tay cầm tự chế (cổng COM: SerialControllerSource; Wi-Fi: UdpControllerSource) và tay cầm chơi game thường
    /// (Xbox, PlayStation... qua Input System) thành một bộ điều khiển chung cho chế độ máy tính (DesktopViewer, HandController).
    ///
    /// Tay cầm 1 cần (mạch joystick → Arduino Mega → ESP32 gửi "X,Y,B" qua Wi-Fi, docs/09) có 2 chế độ:
    ///   ĐI  : cần ↑ ↓ tiến / lùi (đẩy nhẹ đi chậm), ← → quay mặt trái / phải, đẩy chéo vừa đi vừa rẽ;
    ///         đẩy lên hết cỡ quá 1 s: chạy nhanh; giữ quay hết cỡ: quay nhanh dần.
    ///         Giữ nút + cần ← →: bước ngang; giữ nút + cần ↑ ↓: ngẩng / cúi nhìn.
    ///         Nhấn nút 1 lần: sang TAY; nhấn 2 lần: ngồi / đứng; giữ nút 1 s (không đẩy cần): sang khu kế tiếp.
    ///   TAY : cần để ngắm (xoay, ngẩng; trỏ vào khóa buret thì ↑ ↓ vặn khóa). Nút như chuột trái: nhấn cầm / đặt / dùng,
    ///         giữ để rót, hút, lắc. Nhấn khi tâm ngắm không trỏ vào gì: về ĐI.
    /// Tay cầm đủ nút (2 cần, 5 nút): xem docs/09-tay-cam-esp32-cam.md.
    /// Đọc lười, tính một lần mỗi khung hình, nên script nào gọi trước cũng được.
    /// </summary>
    public static class PadInput
    {
        public enum StickMode { Walk, Hand }

        const float LongPressS = 0.6f;

        /// <summary>Có tay cầm tự chế đang gửi gói hoặc có tay cầm chơi game cắm vào.</summary>
        public static bool Active { get { Tick(); return controllerOn || Gamepad.current != null; } }
        public static bool ControllerConnected { get { Tick(); return controllerOn; } }
        /// <summary>Tay cầm tự chế đang nối (hoặc nguồn đầu tiên nếu chưa nối cái nào), để hiện trạng thái.</summary>
        public static ControllerSource Controller { get { Tick(); return active; } }
        public static IReadOnlyList<ControllerSource> Sources => sources;
        /// <summary>Tay cầm đang nối chỉ có 1 cần: dùng 2 chế độ ĐI / TAY.</summary>
        public static bool SingleStick { get { Tick(); return singleStick; } }
        public static StickMode Mode { get { Tick(); return mode; } }
        /// <summary>Lần cuối người chơi dùng tay cầm (không phải chuột): trỏ bằng tâm màn hình, gợi ý ghi tên nút tay cầm.</summary>
        public static bool AimFromCenter { get { Tick(); return lastPadUse > 0 && lastPadUse >= lastMouseUse; } }
        /// <summary>Tay cầm dùng gần nhất là tay cầm chơi game (đổi tên nút trong gợi ý: A, B, RT...).</summary>
        public static bool UsingGamepad { get { Tick(); return usingGamepad; } }

        public static Vector2 Move { get { Tick(); return move; } }
        public static Vector2 Look { get { Tick(); return look; } }
        /// <summary>Đang giữ cò (như giữ chuột trái).</summary>
        public static bool Use { get { Tick(); return use; } }
        /// <summary>Đang giữ nút đi nhanh / thao tác chậm (như Shift).</summary>
        public static bool Shift { get { Tick(); return shift; } }
        /// <summary>Đang giữ nút lắc bình (như F).</summary>
        public static bool Swirl { get { Tick(); return swirl; } }
        public static bool BackPressed { get { Tick(); return back; } }
        public static bool RecordPressed { get { Tick(); return record; } }
        public static bool HelpPressed { get { Tick(); return help; } }
        /// <summary>+1 sang khu kế tiếp, -1 về khu trước, 0 đứng yên.</summary>
        public static int StationStep { get { Tick(); return station; } }
        /// <summary>Mở / đóng khóa buret bằng phím mũi tên trên tay cầm chơi game (như ↑ ↓).</summary>
        public static float StopcockAxis { get { Tick(); return stopcockAxis; } }
        /// <summary>Tay cầm 1 cần vừa nhấn nút 2 lần ở chế độ ĐI: ngồi / đứng (như phím C).</summary>
        public static bool SitPressed { get { Tick(); return sit; } }

        /// <summary>Thông báo ngắn cho người chơi (đổi chế độ ĐI / TAY), HandController hiện lên màn hình.</summary>
        public static event Action<string> Notice;

        /// <summary>HandController bật khi đang trỏ vào khóa buret: cần nhìn trục dọc dùng để vặn khóa, không ngẩng / cúi.</summary>
        public static bool LookYUsedByTool { get; set; }
        /// <summary>HandController bật khi tâm ngắm đang trỏ vào thứ thao tác được (tay cầm 1 cần: nhấn vào chỗ trống thì về ĐI).</summary>
        public static bool AimHasTarget { get; set; }

        /// <summary>Báo khi tay cầm tự chế nối / mất nối (true = nối, kèm tên cổng hoặc địa chỉ).</summary>
        public static event Action<bool, string> ControllerChanged;

        static readonly List<ControllerSource> sources = new List<ControllerSource>();
        static ControllerSource active;
        static int tickFrame = -1;
        static bool controllerOn, singleStick, usingGamepad, use, shift, swirl, back, record, help;
        static Vector2 move, look;
        static int station;
        static float stopcockAxis, lastPadUse, lastMouseUse;
        static float recenterSince = -1, askSince = -1;
        static bool recenterLong, askLong;
        static StickMode mode;
        static bool stickPrev, waitRelease;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            sources.Clear();
            active = null;
            tickFrame = -1;
            controllerOn = singleStick = usingGamepad = false;
            lastPadUse = lastMouseUse = 0;
            recenterSince = askSince = -1;
            mode = StickMode.Walk;
            stickPrev = waitRelease = false;
            pressStart = lastTap = runSince = turnSince = -1;
            chord = holdFired = sit = false;
            LookYUsedByTool = AimHasTarget = false;
            ControllerChanged = null;
            Notice = null;
        }

        public static void Register(ControllerSource s) { if (!sources.Contains(s)) sources.Add(s); }
        public static void Unregister(ControllerSource s) => sources.Remove(s);

        // ----- Rung, đèn (chỉ tay cầm tự chế; tay cầm chơi game không rung để khỏi bị kẹt rung khi thoát) -----

        public static void Pulse(int level, int ms) { if (ControllerConnected) active.Pulse(level, ms); }
        public static void Rumble(int level) { if (ControllerConnected) active.SetVibration(level); }
        public static void Leds(bool green, bool red) { if (ControllerConnected) active.SetLeds(green, red); }

        static void Tick()
        {
            if (tickFrame == Time.frameCount) return;
            tickFrame = Time.frameCount;
            float now = Time.unscaledTime;

            ControllerSource connected = null;
            foreach (var s in sources)
                if (s != null && s.isActiveAndEnabled && s.IsConnected) { connected = s; break; }
            active = connected ?? (sources.Count > 0 ? sources[0] : null);
            bool on = connected != null;
            if (on != controllerOn)
            {
                controllerOn = on;
                if (on)
                {
                    mode = StickMode.Walk;
                    lastPadUse = now;  // vừa nối tay cầm: hiện ngay gợi ý nút tay cầm, chưa cần đụng cần
                }
                ControllerChanged?.Invoke(on, active != null ? active.ControllerName : "");
            }

            move = look = Vector2.zero;
            use = shift = swirl = back = record = help = sit = false;
            station = 0;
            stopcockAxis = 0;
            singleStick = false;
            bool padTouched = false;

            if (controllerOn)
            {
                var s = active.Latest;
                bool Held(ControllerButtons b) => s.Pressed(b) || active.ButtonDown(b);
                singleStick = s.Pressed(ControllerButtons.SingleStick);
                if (singleStick) SingleStickTick(s.Joy2, Held(ControllerButtons.Joy2Press), now);
                else
                {
                    move = s.Joy2;
                    look = s.Joy1;
                    use = Held(ControllerButtons.Trigger);
                    swirl = Held(ControllerButtons.Joy1Press);
                    shift = Held(ControllerButtons.Joy2Press);
                    ShortLong(Held(ControllerButtons.Recenter), now, ref recenterSince, ref recenterLong, out back, out bool nextStation);
                    ShortLong(Held(ControllerButtons.AskAI), now, ref askSince, ref askLong, out record, out help);
                    if (nextStation) station = 1;
                }
                bool any = s.Joy1.sqrMagnitude > 0.01f || s.Joy2.sqrMagnitude > 0.01f ||
                           (s.Buttons & ~(ControllerButtons.ImuError | ControllerButtons.SingleStick)) != 0;
                if (any)
                {
                    padTouched = true;
                    usingGamepad = false;
                }
            }

            var gp = Gamepad.current;
            if (gp != null)
            {
                Vector2 m = gp.leftStick.ReadValue(), l = gp.rightStick.ReadValue();
                bool gUse = gp.rightTrigger.ReadValue() > 0.4f || gp.buttonSouth.isPressed;
                bool gShift = gp.leftTrigger.ReadValue() > 0.4f || gp.leftStickButton.isPressed;
                bool gSwirl = gp.buttonWest.isPressed || gp.rightStickButton.isPressed;
                float dpad = gp.dpad.ReadValue().y;
                bool gEdge = gp.buttonEast.wasPressedThisFrame || gp.buttonNorth.wasPressedThisFrame || gp.startButton.wasPressedThisFrame ||
                             gp.leftShoulder.wasPressedThisFrame || gp.rightShoulder.wasPressedThisFrame;
                if (m.sqrMagnitude > 0.04f || l.sqrMagnitude > 0.04f || gUse || gShift || gSwirl || Mathf.Abs(dpad) > 0.5f || gEdge)
                {
                    padTouched = true;
                    usingGamepad = true;
                }
                // Vùng chết 0,15 cho cần analog (tay cầm chơi game cũ hay lệch tâm)
                move += DeadZone(m);
                look += DeadZone(l);
                use |= gUse;
                shift |= gShift;
                swirl |= gSwirl;
                back |= gp.buttonEast.wasPressedThisFrame;
                record |= gp.buttonNorth.wasPressedThisFrame;
                help |= gp.startButton.wasPressedThisFrame;
                if (gp.rightShoulder.wasPressedThisFrame) station = 1;
                if (gp.leftShoulder.wasPressedThisFrame) station = -1;
                stopcockAxis = Mathf.Abs(dpad) > 0.5f ? Mathf.Sign(dpad) : 0;
            }
            move = Vector2.ClampMagnitude(move, 1f);
            look = Vector2.ClampMagnitude(look, 1f);

            if (padTouched) lastPadUse = now;
            var mouse = Mouse.current;
            if (mouse != null && (mouse.delta.ReadValue().sqrMagnitude > 4f || mouse.leftButton.isPressed || mouse.rightButton.isPressed))
                lastMouseUse = now;
        }

        // ----- Tay cầm 1 cần (mạch joystick 2 trục + nút nhấn cần) -----

        const float TapMaxS = 0.45f;       // nhấn rồi thả trong chừng này là "nhấn"
        const float DoubleTapS = 0.4f;     // hai lần nhấn cách nhau dưới chừng này là "nhấn 2 lần"
        const float HoldStationS = 1.0f;   // giữ nút, không đẩy cần: sang khu kế tiếp
        const float RunAfterS = 1.0f;      // đẩy lên hết cỡ chừng này thì chạy nhanh
        const float ChordStick = 0.3f;     // giữ nút mà đẩy cần quá mức này: bước ngang / ngẩng cúi
        static float pressStart = -1, lastTap = -1, runSince = -1, turnSince = -1;
        static bool chord, holdFired, sit;

        /// <summary>Tay cầm 1 cần: 2 chế độ ĐI / TAY (xem đầu file).</summary>
        static void SingleStickTick(Vector2 stick, bool button, float now)
        {
            bool pressed = button && !stickPrev, released = !button && stickPrev;
            stickPrev = button;
            if (waitRelease)
            {
                // vừa đổi chế độ bằng lần nhấn này: chờ thả nút, để lần nhấn đó không làm thêm việc gì
                if (!button) waitRelease = false;
                return;
            }

            if (mode == StickMode.Walk)
            {
                if (pressed)
                {
                    pressStart = now;
                    chord = holdFired = false;
                }
                if (button)
                {
                    if (stick.magnitude > ChordStick) chord = true;
                    if (chord)
                    {
                        move = new Vector2(stick.x, 0);         // giữ nút + ← →: bước ngang, không quay mặt
                        look = new Vector2(0, stick.y * 0.8f);  // giữ nút + ↑ ↓: ngẩng / cúi nhìn
                    }
                    else if (!holdFired && now - pressStart >= HoldStationS)
                    {
                        holdFired = true;
                        station = 1;
                    }
                    runSince = turnSince = -1;
                    return;
                }
                if (released && !chord && !holdFired && now - pressStart <= TapMaxS)
                {
                    if (lastTap >= 0 && now - lastTap <= DoubleTapS)
                    {
                        sit = true;  // nhấn 2 lần: ngồi / đứng
                        lastTap = -1;
                    }
                    else lastTap = now;
                }
                if (lastTap >= 0 && now - lastTap > DoubleTapS)
                {
                    lastTap = -1;  // chỉ nhấn 1 lần: sang TAY
                    SetMode(StickMode.Hand);
                    return;
                }

                // ↑ ↓ tiến lùi (đẩy nhẹ đi chậm), ← → quay mặt, chéo: vừa đi vừa rẽ
                move = new Vector2(0, stick.y);
                if (Mathf.Abs(stick.x) > 0.85f) { if (turnSince < 0) turnSince = now; }
                else turnSince = -1;
                float turn = turnSince >= 0 && now - turnSince > 0.7f ? 1f : 0.8f;  // giữ quay hết cỡ: quay nhanh dần
                look = new Vector2(stick.x * turn, 0);
                if (stick.y > 0.9f) { if (runSince < 0) runSince = now; }
                else runSince = -1;
                shift = runSince >= 0 && now - runSince >= RunAfterS;  // đẩy lên hết cỡ 1 giây: chạy nhanh
            }
            else
            {
                look = stick * 0.6f;  // ngắm chậm hơn lúc đi cho dễ trúng
                if (pressed && !AimHasTarget)
                {
                    SetMode(StickMode.Walk);
                    waitRelease = true;
                    return;
                }
                use = button;
            }
        }

        static void SetMode(StickMode m)
        {
            mode = m;
            pressStart = lastTap = runSince = turnSince = -1;
            Notice?.Invoke(m == StickMode.Hand
                ? "Chế độ TAY: cần để ngắm, nhấn cần để cầm / dùng, nhấn vào chỗ trống để đi tiếp"
                : "Chế độ ĐI: cần để đi và quay, nhấn cần để thao tác");
        }

        static Vector2 DeadZone(Vector2 v)
        {
            float m = v.magnitude;
            return m < 0.15f ? Vector2.zero : v / m * Mathf.Clamp01((m - 0.15f) / 0.85f);
        }

        /// <summary>Nhấn nhanh (thả trước LongPressS) hoặc giữ lâu: mỗi lần bấm chỉ ra một trong hai.</summary>
        static void ShortLong(bool held, float now, ref float since, ref bool longFired, out bool shortPress, out bool longPress)
        {
            shortPress = longPress = false;
            if (held)
            {
                if (since < 0)
                {
                    since = now;
                    longFired = false;
                }
                else if (!longFired && now - since >= LongPressS)
                {
                    longFired = true;
                    longPress = true;
                }
            }
            else if (since >= 0)
            {
                shortPress = !longFired;
                since = -1;
            }
        }
    }
}
