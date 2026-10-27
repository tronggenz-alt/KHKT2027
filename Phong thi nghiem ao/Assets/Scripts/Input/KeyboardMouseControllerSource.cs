using UnityEngine;
using UnityEngine.InputSystem;

namespace Khkt.Input
{
    /// <summary>
    /// Giả lập tay cầm bằng chuột-bàn phím: để lập trình khi chưa có phần cứng, và cho
    /// "chế độ laptop" (nhóm đối chứng trong thực nghiệm, người vào phòng không có kính).
    ///   Giữ chuột phải + rê chuột : xoay tay cầm (yaw/pitch)
    ///   Lăn chuột hoặc Q/E        : nghiêng tay cầm (rót, vặn khóa buret)
    ///   Chuột trái hoặc Space     : cò
    ///   WASD                      : joystick di chuyển     Mũi tên: joystick chỉnh tinh
    ///   R: căn lại    T: giữ để hỏi AI
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class KeyboardMouseControllerSource : ControllerSource
    {
        public float mouseSensitivity = 0.15f;
        public float rollDegPerScroll = 0.05f;
        public float rollKeySpeed = 60f;

        float yaw, pitch, roll;
        Quaternion prevRotation = Quaternion.identity;
        uint timeMs;
        byte seq;

        public override bool IsConnected => true;

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null || mouse == null) return;

            if (mouse.rightButton.isPressed)
            {
                Vector2 d = mouse.delta.ReadValue() * mouseSensitivity;
                yaw += d.x;
                pitch = Mathf.Clamp(pitch - d.y, -85, 85);
            }
            roll += mouse.scroll.ReadValue().y * rollDegPerScroll;
            if (kb.qKey.isPressed) roll -= rollKeySpeed * Time.deltaTime;
            if (kb.eKey.isPressed) roll += rollKeySpeed * Time.deltaTime;
            roll = Mathf.Clamp(roll, -170, 170);

            var rot = Quaternion.Euler(pitch, yaw, roll);
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            // vận tốc góc trong hệ thân (Unity) rồi đổi sang hệ tay cầm, để SwirlDetector và file ghi vẫn có dữ liệu
            (Quaternion.Inverse(prevRotation) * rot).ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180) angle -= 360;
            Vector3 omegaUnity = float.IsFinite(axis.x) ? axis * (angle / dt) : Vector3.zero;
            prevRotation = rot;
            Vector3 upInBody = Quaternion.Inverse(rot) * Vector3.up;

            var buttons = ControllerButtons.None;
            if (mouse.leftButton.isPressed || kb.spaceKey.isPressed) buttons |= ControllerButtons.Trigger;
            if (kb.rKey.isPressed) buttons |= ControllerButtons.Recenter;
            if (kb.tKey.isPressed) buttons |= ControllerButtons.AskAI;

            timeMs += (uint)(dt * 1000);
            Publish(new ControllerState
            {
                TimeMs = timeMs,
                Seq = seq++,
                AccelG = Frames.FromUnity(upInBody),
                GyroDps = Frames.AngularVelocityFromUnity(omegaUnity),
                OrientationRH = Frames.FromUnity(rot),
                Joy2 = new Vector2(Key(kb.dKey) - Key(kb.aKey), Key(kb.wKey) - Key(kb.sKey)),
                Joy1 = new Vector2(Key(kb.rightArrowKey) - Key(kb.leftArrowKey), Key(kb.upArrowKey) - Key(kb.downArrowKey)),
                Buttons = buttons,
            });
        }

        static float Key(UnityEngine.InputSystem.Controls.KeyControl k) => k.isPressed ? 1f : 0f;

        public override void SetVibration(int level)
        {
            if (level > 0) Debug.Log($"[Giả lập] rung {level}");
        }

        public override void Pulse(int level, int ms) => Debug.Log($"[Giả lập] xung rung {level} trong {ms} ms");
        public override void SetLeds(bool green, bool red) { }
        public override void ResetOrientation() => roll = 0;
    }
}
