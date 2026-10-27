using System;
using UnityEngine;

namespace Khkt.Input
{
    [Flags]
    public enum ControllerButtons : byte
    {
        None = 0,
        Trigger = 1 << 0,     // cò: cầm / thả
        Recenter = 1 << 1,    // căn lại hướng
        AskAI = 1 << 2,       // giữ để hỏi AI
        Joy1Press = 1 << 3,   // nhấn joystick chỉnh tinh
        Joy2Press = 1 << 4,   // nhấn joystick di chuyển
        SingleStick = 1 << 6, // tay cầm chỉ có 1 cần (ESP32-CAM + 1 mạch joystick): điều khiển 2 chế độ ĐI / TAY
        ImuError = 1 << 7,    // firmware báo lỗi đọc MPU6050 (hoặc không có MPU6050)
    }

    /// <summary>Một mẫu 100 Hz của tay cầm, đã đổi đơn vị.</summary>
    public struct ControllerState
    {
        public uint TimeMs;
        public byte Seq;
        /// <summary>Gia tốc (g) trong hệ tay cầm thuận tay phải: x tới trước, y sang trái, z lên trên.</summary>
        public Vector3 AccelG;
        /// <summary>Vận tốc góc (°/s), cùng hệ trục với AccelG.</summary>
        public Vector3 GyroDps;
        /// <summary>Hướng tay cầm trong hệ thuận tay phải (Z lên). Chỉ để ghi file; trong Unity dùng UnityRotation.</summary>
        public Quaternion OrientationRH;
        /// <summary>Joystick chỉnh tinh, -1..1.</summary>
        public Vector2 Joy1;
        /// <summary>Joystick di chuyển, -1..1.</summary>
        public Vector2 Joy2;
        public ControllerButtons Buttons;

        public bool Pressed(ControllerButtons b) => (Buttons & b) != 0;
        public Quaternion UnityRotation => Frames.ToUnity(OrientationRH);
        /// <summary>Góc giữa trục "lên" của tay cầm và phương thẳng đứng (độ).</summary>
        public float TiltDeg => Vector3.Angle(UnityRotation * Vector3.up, Vector3.up);
    }

    /// <summary>
    /// Đổi hệ trục tay cầm (thuận tay phải: X tới, Y trái, Z lên) sang Unity (thuận tay trái: x phải, y lên, z tới).
    /// Vector: (x, y, z)_RH -> (-y, z, x)_Unity. Quaternion và vận tốc góc là véc-tơ giả nên đổi thêm dấu.
    /// </summary>
    public static class Frames
    {
        public static Vector3 ToUnity(Vector3 v) => new Vector3(-v.y, v.z, v.x);
        public static Vector3 FromUnity(Vector3 v) => new Vector3(v.z, -v.x, v.y);
        public static Quaternion ToUnity(Quaternion q) => new Quaternion(q.y, -q.z, -q.x, q.w);
        public static Quaternion FromUnity(Quaternion q) => new Quaternion(-q.z, q.x, -q.y, q.w);
        public static Vector3 AngularVelocityFromUnity(Vector3 w) => new Vector3(-w.z, w.x, -w.y);
    }
}
