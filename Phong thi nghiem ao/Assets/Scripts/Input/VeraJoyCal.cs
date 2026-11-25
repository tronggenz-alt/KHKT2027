using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Khkt.Input
{
    /// <summary>
    /// Căn joystick tay cầm VERA ở phía game (trang "Tay cầm" ở sảnh, 6 bước như tools/canh_joystick.py).
    ///
    /// Hai cách tính cần gạt:
    ///  • Theo điện áp thật (mV, gói thô USB có): tâm và biên cố định, đo một lần lúc căn. Không phụ thuộc tâm firmware học
    ///    lại mỗi lần mở cổng — tâm đó có thể sai rất xa (đo 25/11/2026: firmware học tâm X = 237, Y = 2350 mV trong khi tâm
    ///    thật ~1650 mV, làm nhân vật tự xoay và tự đi khi không ai chạm). Chưa căn thì dùng số đo thật của tay cầm 24/11.
    ///  • Theo số firmware gửi (-1..1, Wi-Fi không có mV): như trước, bù hướng và biên sai lưu trong flash.
    ///
    /// Kết quả: trục nào là tiến / phải, dấu, độ khuếch đại riêng từng nửa trục (và tâm khi theo mV). Lưu PlayerPrefs
    /// "vera_joy_cal". Áp dụng trong ControllerPackets.FromVera (có thể chạy trên luồng đọc cổng), nên chỉ đọc biến tĩnh,
    /// PlayerPrefs đọc sẵn trên luồng chính lúc khởi động.
    /// </summary>
    public static class VeraJoyCal
    {
        public const string Key = "vera_joy_cal";
        /// <summary>Đẩy hết cỡ mà số firmware chưa lệch chừng này khỏi tâm thì coi như trục không đổi (dây lỏng, chưa đẩy).</summary>
        public const float MinDeflection = 0.2f;
        /// <summary>Như MinDeflection nhưng theo điện áp (mV).</summary>
        public const float MinDeflectionMv = 300f;

        public struct Cal
        {
            public bool Valid;
            /// <summary>true: tính từ điện áp thật (mV), CenterMv và các Gain là mV.</summary>
            public bool UseMv;
            public Vector2 CenterMv;
            public int AxisFwd, AxisRight;     // 0 = x, 1 = y (của số firmware hoặc của chân mạch khi theo mV)
            public float SignFwd, SignRight;   // +1 / -1
            public float GainFwd, GainBack, GainRight, GainLeft;  // độ lệch lúc đẩy hết cỡ mỗi hướng (> 0)

            public override string ToString()
            {
                var c = CultureInfo.InvariantCulture;
                string core = string.Join(",", AxisFwd, AxisRight, SignFwd.ToString("R", c), SignRight.ToString("R", c),
                                          GainFwd.ToString("R", c), GainBack.ToString("R", c), GainRight.ToString("R", c), GainLeft.ToString("R", c));
                return UseMv ? "mv," + core + "," + CenterMv.x.ToString("R", c) + "," + CenterMv.y.ToString("R", c) : core;
            }

            public static bool TryParse(string s, out Cal cal)
            {
                cal = default;
                var p = (s ?? "").Split(',');
                bool mv = p.Length == 11 && p[0] == "mv";
                if (p.Length != 8 && !mv) return false;
                int o = mv ? 1 : 0;
                var c = CultureInfo.InvariantCulture;
                var f = new float[p.Length - o];
                for (int i = 0; i < f.Length; i++)
                    if (!float.TryParse(p[i + o], NumberStyles.Float, c, out f[i])) return false;
                cal = new Cal
                {
                    UseMv = mv,
                    AxisFwd = (int)f[0], AxisRight = (int)f[1], SignFwd = Mathf.Sign(f[2]), SignRight = Mathf.Sign(f[3]),
                    GainFwd = f[4], GainBack = f[5], GainRight = f[6], GainLeft = f[7],
                    CenterMv = mv ? new Vector2(f[8], f[9]) : Vector2.zero,
                };
                float min = mv ? MinDeflectionMv : MinDeflection;
                cal.Valid = cal.AxisFwd != cal.AxisRight && (cal.AxisFwd | cal.AxisRight) <= 1 && cal.AxisFwd >= 0 && cal.AxisRight >= 0 &&
                            cal.GainFwd >= min && cal.GainBack >= min && cal.GainRight >= min && cal.GainLeft >= min;
                return cal.Valid;
            }
        }

        /// <summary>
        /// Mặc định khi chưa căn mà có mV: tay cầm VERA thật đo 24/11/2026 (tools/canh_joystick.py) — tiến = chân Y tăng,
        /// phải = chân X GIẢM; tâm ~1650 mV (đo được 1608–1741), đẩy hết cỡ ra 142 / 3150 mV.
        /// </summary>
        public static readonly Cal DefaultMv = new Cal
        {
            Valid = true, UseMv = true, CenterMv = new Vector2(1650f, 1650f),
            AxisFwd = 1, SignFwd = 1f, AxisRight = 0, SignRight = -1f,
            GainFwd = 1400f, GainBack = 1400f, GainRight = 1400f, GainLeft = 1400f,
        };

        static Cal current;
        /// <summary>Phần căn đang lưu (Valid = false: chưa căn).</summary>
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

        /// <summary>Phần căn sẽ dùng cho một mẫu: đã lưu và hợp với mẫu, hoặc mặc định theo mV, hoặc không căn.</summary>
        public static Cal Effective(bool hasMv)
        {
            // có mV (USB) thì luôn tính theo mV: phần căn mV đã lưu, chưa có thì số đo thật mặc định — không dùng phần căn cũ theo
            // số firmware vì số đó trừ tâm firmware học lúc mở cổng (có thể sai rất xa)
            if (hasMv) return current.Valid && current.UseMv ? current : DefaultMv;
            return current.Valid && !current.UseMv ? current : default;
        }

        /// <summary>Số firmware (và mV nếu có) -> (x phải, y tiến) trong -1..1.</summary>
        public static Vector2 Apply(Vector2 raw, Vector2 mv, bool hasMv) => Apply(Effective(hasMv), raw, mv, hasMv);

        /// <summary>Chỉ số firmware (Wi-Fi, hoặc phần căn cũ).</summary>
        public static Vector2 Apply(Vector2 raw) => Apply(Effective(false), raw, default, false);

        public static Vector2 Apply(in Cal c, Vector2 raw) => Apply(c, raw, default, false);

        public static Vector2 Apply(in Cal c, Vector2 raw, Vector2 mv, bool hasMv)
        {
            if (!c.Valid || (c.UseMv && !hasMv)) return new Vector2(Mathf.Clamp(raw.x, -1, 1), Mathf.Clamp(raw.y, -1, 1));
            Vector2 v = c.UseMv ? mv - c.CenterMv : raw;
            float y = v[c.AxisFwd] * c.SignFwd;
            float x = v[c.AxisRight] * c.SignRight;
            y = y >= 0 ? y / c.GainFwd : y / c.GainBack;
            x = x >= 0 ? x / c.GainRight : x / c.GainLeft;
            return new Vector2(Mathf.Clamp(x, -1, 1), Mathf.Clamp(y, -1, 1));
        }

        /// <summary>
        /// Tính phần căn từ trung bình ở 5 tư thế (giữa, tiến, lùi, trái, phải). mv = true: các số là điện áp chân mạch (mV),
        /// false: số firmware -1..1. errors: lỗi làm phần căn không dùng được; notes: nhắc nhở. Trả về true khi không có lỗi.
        /// </summary>
        public static bool Compute(Vector2 center, Vector2 fwd, Vector2 back, Vector2 left, Vector2 right, out Cal cal,
                                   List<string> errors, List<string> notes, bool mv = false)
        {
            cal = default;
            float min = mv ? MinDeflectionMv : MinDeflection;
            string u = mv ? " mV" : "";
            Vector2 df = fwd - center, db = back - center, dl = left - center, dr = right - center;
            int af = Mathf.Abs(df.x) >= Mathf.Abs(df.y) ? 0 : 1;
            int ar = Mathf.Abs(dr.x) >= Mathf.Abs(dr.y) ? 0 : 1;
            int n0 = errors.Count;
            if (af == ar) errors.Add("Tiến và phải cùng nằm trên một trục: có thể đẩy sai hướng, hoặc một trục không đổi (dây lỏng)");
            float sf = Mathf.Sign(df[af]), sr = Mathf.Sign(dr[ar]);
            foreach (var (name, d, axis, sign) in new[] { ("Tiến", df, af, sf), ("Lùi", db, af, -sf), ("Phải", dr, ar, sr), ("Trái", dl, ar, -sr) })
            {
                float main = d[axis] * sign;
                if (Mathf.Abs(d[axis]) < min)
                    errors.Add($"{name}: cần gần như không đổi ({Mathf.Abs(d[axis]):0.##}{u}), đẩy hết cỡ hoặc kiểm tra dây joystick");
                else if (main < 0)
                    errors.Add($"{name}: đi ngược chiều mong đợi, làm lại bước này");
                float other = Mathf.Abs(d[1 - axis]);
                if (other > 0.5f * Mathf.Abs(d[axis]) && Mathf.Abs(d[axis]) >= min)
                    notes.Add($"{name}: đẩy hơi chéo (trục kia lệch {other:0.##}{u})");
            }
            if (errors.Count > n0) return false;
            cal = new Cal
            {
                Valid = true, UseMv = mv, CenterMv = mv ? center : Vector2.zero,
                AxisFwd = af, AxisRight = ar, SignFwd = sf, SignRight = sr,
                GainFwd = df[af] * sf, GainBack = -db[af] * sf, GainRight = dr[ar] * sr, GainLeft = -dl[ar] * sr,
            };
            if (mv)
            {
                for (int i = 0; i < 2; i++)
                    if (center[i] < 900f || center[i] > 2400f)
                        notes.Add($"Tâm chân {(i == 0 ? "X" : "Y")} = {center[i]:0} mV, lệch xa 1650: kiểm tra dây VR{(i == 0 ? "x" : "y")} / GND / 3V3");
            }
            else if (center.magnitude > 0.15f) notes.Add($"Tâm cần lệch {center.magnitude:0.00}: lúc bật tay cầm nên buông cần");
            return true;
        }
    }
}
