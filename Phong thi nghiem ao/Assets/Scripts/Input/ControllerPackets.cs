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

        /// <summary>
        /// Mẫu tay cầm VERA sang ControllerState. Nút cũ được điền để các phần chưa biết VERA vẫn chạy:
        /// cò = chạm B, nhấn joystick 1 = chạm A, nhấn joystick 2 = nhấn cần, căn lại = firmware vừa đặt yaw về 0.
        /// Joystick nằm ở Joy2 (cần di chuyển), chưa có vùng chết (PadInput xử lý).
        /// </summary>
        public static ControllerState FromVera(in VeraSample v)
        {
            var b = ControllerButtons.None;
            if ((v.Flags & VeraFlags.TouchB) != 0) b |= ControllerButtons.Trigger;
            if ((v.Flags & VeraFlags.TouchA) != 0) b |= ControllerButtons.Joy1Press;
            if ((v.Flags & VeraFlags.StickPress) != 0) b |= ControllerButtons.Joy2Press;
            if ((v.Flags & VeraFlags.Recentered) != 0) b |= ControllerButtons.Recenter;
            if ((v.Flags & VeraFlags.ImuError) != 0) b |= ControllerButtons.ImuError;
            return new ControllerState
            {
                TimeMs = v.TimeMs,
                Seq = (byte)v.Seq,
                AccelG = v.AccelG,
                GyroDps = v.GyroDps,
                OrientationRH = (v.Flags & VeraFlags.ImuError) != 0 ? Quaternion.identity : v.OrientationRH,
                // cần gạt: có điện áp thật (USB) thì tính từ mV với tâm cố định, không phụ thuộc tâm firmware học lúc mở cổng
                Joy2 = VeraJoyCal.Apply(v.Joy, v.JoyMv, v.HasJoyMv),
                JoyRaw = v.Joy,
                JoyMv = v.JoyMv,
                HasJoyMv = v.HasJoyMv,
                Buttons = b,
                Vera = true,
                TouchA = v.TouchA,
                TouchB = v.TouchB,
                Flags = v.Flags,
                TempC = v.TempC,
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
