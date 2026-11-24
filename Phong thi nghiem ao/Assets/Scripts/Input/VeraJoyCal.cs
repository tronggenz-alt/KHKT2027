using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Khkt.Input
{
    /// <summary>
    /// Căn joystick tay cầm VERA ở phía game (trang "Tay cầm" ở sảnh, 6 bước như tools/canh_joystick.py).
    /// Làm trên số joystick firmware gửi lên (-1..1, đã qua hướng / biên lưu trong flash tay cầm), nên chạy được với mọi bản
    /// firmware, cả USB lẫn Wi-Fi, và bù được hướng sai hay biên sai đang lưu trong mạch (biên quá rộng làm đẩy hết cần chỉ ra ~0,5).
    /// Kết quả: trục nào là tiến / phải, dấu, độ khuếch đại riêng từng nửa trục. Lưu PlayerPrefs "vera_joy_cal".
    /// Áp dụng trong ControllerPackets.FromVera (có thể chạy trên luồng đọc cổng), nên chỉ đọc biến tĩnh, PlayerPrefs đọc sẵn
    /// trên luồng chính lúc khởi động.
    /// Đổi hướng / biên trong flash tay cầm (lệnh h, j, H, B) thì phải căn lại.
    /// </summary>
    public static class VeraJoyCal
    {
        public const string Key = "vera_joy_cal";
        /// <summary>Đẩy hết cỡ mà số firmware chưa lệch chừng này khỏi tâm thì coi như trục không đổi (dây lỏng, chưa đẩy).</summary>
        public const float MinDeflection = 0.2f;

        public struct Cal
        {
            public bool Valid;
            public int AxisFwd, AxisRight;     // 0 = x, 1 = y của số firmware
            public float SignFwd, SignRight;   // +1 / -1
            public float GainFwd, GainBack, GainRight, GainLeft;  // độ lệch firmware lúc đẩy hết cỡ mỗi hướng (> 0)

            public override string ToString()
            {
                var c = CultureInfo.InvariantCulture;
                return string.Join(",", AxisFwd, AxisRight, SignFwd.ToString("R", c), SignRight.ToString("R", c),
                                   GainFwd.ToString("R", c), GainBack.ToString("R", c), GainRight.ToString("R", c), GainLeft.ToString("R", c));
            }

            public static bool TryParse(string s, out Cal cal)
            {
                cal = default;
                var p = (s ?? "").Split(',');
                if (p.Length != 8) return false;
                var c = CultureInfo.InvariantCulture;
                var f = new float[8];
                for (int i = 0; i < 8; i++)
                    if (!float.TryParse(p[i], NumberStyles.Float, c, out f[i])) return false;
                cal = new Cal
                {
                    AxisFwd = (int)f[0], AxisRight = (int)f[1], SignFwd = Mathf.Sign(f[2]), SignRight = Mathf.Sign(f[3]),
                    GainFwd = f[4], GainBack = f[5], GainRight = f[6], GainLeft = f[7],
                };
                cal.Valid = cal.AxisFwd != cal.AxisRight && (cal.AxisFwd | cal.AxisRight) <= 1 && cal.AxisFwd >= 0 && cal.AxisRight >= 0 &&
                            cal.GainFwd >= MinDeflection && cal.GainBack >= MinDeflection && cal.GainRight >= MinDeflection && cal.GainLeft >= MinDeflection;
                return cal.Valid;
            }
        }

        static Cal current;
        /// <summary>Phần căn đang dùng (Valid = false: dùng thẳng số firmware).</summary>
        public static Cal Current => current;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void LoadOnStart() => Load();

        public static void Load()
        {
            current = Cal.TryParse(PlayerPrefs.GetString(Key, ""), out var c) ? c : default;
        }

        public static void Save(Cal c)
        {
            current = c;
            PlayerPrefs.SetString(Key, c.ToString());
            PlayerPrefs.Save();
        }

        public static void Clear()
        {
            current = default;
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }

        /// <summary>Số firmware -> (x phải, y tiến) trong -1..1 theo phần căn đang dùng.</summary>
        public static Vector2 Apply(Vector2 raw) => Apply(current, raw);

        public static Vector2 Apply(in Cal c, Vector2 raw)
        {
            if (!c.Valid) return new Vector2(Mathf.Clamp(raw.x, -1, 1), Mathf.Clamp(raw.y, -1, 1));
            float y = raw[c.AxisFwd] * c.SignFwd;
            float x = raw[c.AxisRight] * c.SignRight;
            y = y >= 0 ? y / c.GainFwd : y / c.GainBack;
            x = x >= 0 ? x / c.GainRight : x / c.GainLeft;
            return new Vector2(Mathf.Clamp(x, -1, 1), Mathf.Clamp(y, -1, 1));
        }

        /// <summary>
        /// Tính phần căn từ số firmware trung bình ở 5 tư thế (giữa, tiến, lùi, trái, phải). errors: lỗi làm phần căn không dùng được;
        /// notes: nhắc nhở (đẩy chéo...). Trả về true khi không có lỗi.
        /// </summary>
        public static bool Compute(Vector2 center, Vector2 fwd, Vector2 back, Vector2 left, Vector2 right, out Cal cal,
                                   List<string> errors, List<string> notes)
        {
            cal = default;
            Vector2 df = fwd - center, db = back - center, dl = left - center, dr = right - center;
            int af = Mathf.Abs(df.x) >= Mathf.Abs(df.y) ? 0 : 1;
            int ar = Mathf.Abs(dr.x) >= Mathf.Abs(dr.y) ? 0 : 1;
            int n0 = errors.Count;
            if (af == ar) errors.Add("Tiến và phải cùng nằm trên một trục: có thể đẩy sai hướng, hoặc một trục không đổi (dây lỏng)");
            float sf = Mathf.Sign(df[af]), sr = Mathf.Sign(dr[ar]);
            foreach (var (name, d, axis, sign) in new[] { ("Tiến", df, af, sf), ("Lùi", db, af, -sf), ("Phải", dr, ar, sr), ("Trái", dl, ar, -sr) })
            {
                float main = d[axis] * sign;
                if (Mathf.Abs(d[axis]) < MinDeflection)
                    errors.Add($"{name}: cần gần như không đổi ({Mathf.Abs(d[axis]):0.00}), đẩy hết cỡ hoặc kiểm tra dây joystick");
                else if (main < 0)
                    errors.Add($"{name}: đi ngược chiều mong đợi, làm lại bước này");
                float other = Mathf.Abs(d[1 - axis]);
                if (other > 0.5f * Mathf.Abs(d[axis]) && Mathf.Abs(d[axis]) >= MinDeflection)
                    notes.Add($"{name}: đẩy hơi chéo (trục kia lệch {other:0.00})");
            }
            if (errors.Count > n0) return false;
            cal = new Cal
            {
                Valid = true, AxisFwd = af, AxisRight = ar, SignFwd = sf, SignRight = sr,
                GainFwd = df[af] * sf, GainBack = -db[af] * sf, GainRight = dr[ar] * sr, GainLeft = -dl[ar] * sr,
            };
            if (center.magnitude > 0.15f) notes.Add($"Tâm cần lệch {center.magnitude:0.00}: lúc bật tay cầm nên buông cần");
            return true;
        }
    }
}
