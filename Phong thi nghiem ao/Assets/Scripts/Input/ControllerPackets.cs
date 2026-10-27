using UnityEngine;

namespace Khkt.Input
{
    /// <summary>Đổi gói thô (docs/02-giao-thuc-tay-cam.md) sang ControllerState. Dùng chung cho tay cầm dây (COM) và Wi-Fi (UDP).</summary>
    public static class ControllerPackets
    {
        public static ControllerState ToState(in RawPacket p, float deadzone, bool invertJoy1Y, bool invertJoy2Y)
        {
            return new ControllerState
            {
                TimeMs = p.TimeMs,
                Seq = p.Seq,
                AccelG = new Vector3(p.Ax, p.Ay, p.Az) / PacketParser.AccLsbPerG,
                GyroDps = new Vector3(p.Gx, p.Gy, p.Gz) / PacketParser.GyroLsbPerDps,
                Joy1 = new Vector2(Axis(p.J1x, deadzone), Axis(p.J1y, deadzone) * (invertJoy1Y ? -1 : 1)),
                Joy2 = new Vector2(Axis(p.J2x, deadzone), Axis(p.J2y, deadzone) * (invertJoy2Y ? -1 : 1)),
                Buttons = (ControllerButtons)p.Buttons,
            };
        }

        /// <summary>0..1023 (tâm 511,5) sang -1..1, bỏ vùng chết rồi kéo giãn lại để vẫn đạt được 1.0.</summary>
        public static float Axis(ushort raw, float deadzone)
        {
            float x = (raw - 511.5f) / 511.5f;
            if (Mathf.Abs(x) < deadzone) return 0;
            return Mathf.Clamp(Mathf.Sign(x) * (Mathf.Abs(x) - deadzone) / (1 - deadzone), -1, 1);
        }
    }
}
