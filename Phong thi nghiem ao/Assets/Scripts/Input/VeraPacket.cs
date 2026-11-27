using System;
using UnityEngine;

namespace Khkt.Input
{
    /// <summary>Bit trạng thái tay cầm VERA (firmware/tay_cam_vera, chung.h).</summary>
    [Flags]
    public enum VeraFlags : byte
    {
        None = 0,
        TouchA = 0x01,      // ngón cái chạm nút cảm ứng A
        TouchB = 0x02,      // ngón trỏ chạm nút cảm ứng B
        StickPress = 0x04,  // nhấn cần joystick
        Still = 0x08,       // tay cầm đang nằm yên (firmware đang học lại độ lệch con quay)
        Recentered = 0x10,  // vừa giữ A + B 1 giây: firmware đặt yaw về 0
        Calibrating = 0x20,
        Talk = 0x40,        // app (VeraPadMap): tổ hợp "Nói với trợ lý" đang giữ (firmware không gửi bit này)
        ImuError = 0x80,
    }

    /// <summary>
    /// Một mẫu tay cầm VERA. Hai loại gói:
    ///   A5 5C, 37 byte (gói v3, Wi-Fi 100 Hz): hướng tính sẵn trên mạch, con quay đã trừ độ lệch, gia tốc, joystick, cảm ứng;
    ///   A5 5D, 105 byte (gói thô, USB 50 gói/s sau lệnh "r2"): hướng trên mạch + 4 mẫu IMU 200 Hz chưa trừ độ lệch.
    /// Bố cục: firmware/tay_cam_vera/mang.h (dongGoiV3), tay_cam_vera.ino (themMauTho), tools/ghi_tay_cam.py.
    /// </summary>
    public struct VeraSample
    {
        public byte Kind;              // 0x5C hoặc 0x5D
        public uint TimeMs;
        public ushort Seq;             // 5C: số gói 8 bit; 5D: chỉ số mẫu 16 bit
        /// <summary>Hướng tay cầm -> mặt đất, hệ thuận tay phải (X tới, Y trái, Z lên), lưu thành phần (x, y, z, w).</summary>
        public Quaternion OrientationRH;
        public Vector3 GyroDps;
        public Vector3 AccelG;
        /// <summary>Joystick đã hiệu chuẩn trên mạch, -1..1 (chưa có vùng chết).</summary>
        public Vector2 Joy;
        /// <summary>Điện áp thật 2 chân joystick (mV, chưa trừ tâm, chưa đổi hướng): chỉ gói thô USB (5D) có; HasJoyMv cho biết.
        /// Game tính cần gạt từ số này với tâm cố định (VeraJoyCal) vì tâm firmware học lúc mở cổng có thể sai.</summary>
        public Vector2 JoyMv;
        public bool HasJoyMv;
        /// <summary>Firmware 3.3 (gói thô, byte 2 = 2): 5 miếng chạm thô, bit 0..4 = TIẾN, LÙI, TRÁI, PHẢI, OK.</summary>
        public bool HasPads;
        public int PadMask;
        public byte TouchA, TouchB;    // mức 0..255 (lơ lửng ~38, chạm 128)
        public VeraFlags Flags;
        public float TempC;

        public const int LengthV3 = 37, LengthRaw = 105;
        const float Q = 16384f, Gyr = 32.8f, Acc = 4096f, J = 32767f;

        static short I16(byte[] b, int i) => (short)(b[i] | (b[i + 1] << 8));
        static ushort U16(byte[] b, int i) => (ushort)(b[i] | (b[i + 1] << 8));
        static uint U32(byte[] b, int i) => (uint)(b[i] | (b[i + 1] << 8) | (b[i + 2] << 16) | (b[i + 3] << 24));

        static Quaternion Quat(byte[] b, int i) =>
            new Quaternion(I16(b, i + 2) / Q, I16(b, i + 4) / Q, I16(b, i + 6) / Q, I16(b, i) / Q);  // gói ghi w, x, y, z

        /// <summary>b[o..o+37) là một gói v3 đã kiểm CRC.</summary>
        public static VeraSample DecodeV3(byte[] b, int o)
        {
            return new VeraSample
            {
                Kind = 0x5C,
                Seq = b[o + 3],
                TimeMs = U32(b, o + 4),
                OrientationRH = Quat(b, o + 8),
                GyroDps = new Vector3(I16(b, o + 16), I16(b, o + 18), I16(b, o + 20)) / Gyr,
                AccelG = new Vector3(I16(b, o + 22), I16(b, o + 24), I16(b, o + 26)) / Acc,
                Joy = new Vector2(I16(b, o + 28) / J, I16(b, o + 30) / J),
                TouchA = b[o + 32],
                TouchB = b[o + 33],
                Flags = (VeraFlags)b[o + 34],
                TempC = b[o + 35] - 40f,
            };
        }

        /// <summary>b[o..o+105) là một gói thô đã kiểm CRC. Con quay: trung bình 4 mẫu (chưa trừ độ lệch), gia tốc: mẫu cuối.</summary>
        public static VeraSample DecodeRaw(byte[] b, int o)
        {
            int n = Mathf.Clamp(b[o + 3], 1, 4);
            Vector3 g = Vector3.zero;
            for (int j = 0; j < n; j++)
            {
                int m = o + 48 + 14 * j;
                g += new Vector3(I16(b, m), I16(b, m + 2), I16(b, m + 4)) / Gyr;
            }
            int last = o + 48 + 14 * (n - 1);
            return new VeraSample
            {
                Kind = 0x5D,
                Seq = U16(b, o + 4),
                TimeMs = U32(b, o + 6),
                TempC = I16(b, o + 10) / 100f,
                Flags = (VeraFlags)b[o + 12],
                OrientationRH = Quat(b, o + 14),
                Joy = new Vector2(I16(b, o + 42) / J, I16(b, o + 44) / J),
                JoyMv = new Vector2(I16(b, o + 38), I16(b, o + 40)),
                HasJoyMv = true,
                HasPads = b[o + 2] >= 2,
                PadMask = b[o + 2] >= 2 ? b[o + 22] & 0x1F : 0,
                TouchA = b[o + 46],
                TouchB = b[o + 47],
                GyroDps = g / n,
                AccelG = new Vector3(I16(b, last + 6), I16(b, last + 8), I16(b, last + 10)) / Acc,
            };
        }
    }
}
