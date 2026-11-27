using System;
using UnityEngine;

namespace Khkt.Input
{
    /// <summary>
    /// Năm miếng chạm của tay cầm VERA (firmware 3.3: TIẾN, LÙI, TRÁI, PHẢI, OK) → thao tác trong lab, theo TỔ HỢP người dùng tự đặt
    /// ở Cài đặt → Tay cầm. Firmware chỉ gửi miếng nào đang chạm; mọi xử lý nằm ở đây nên đổi tổ hợp không cần nạp lại mạch.
    ///  • Mỗi thao tác là một tập miếng phải chạm CÙNG LÚC. Tổ hợp nhiều miếng được xét trước: miếng đã thuộc một tổ hợp đang
    ///    chạm thì không còn tính cho thao tác ít miếng hơn (bóp TIẾN + LÙI là "Dùng", không phải vừa tiến vừa lùi).
    ///  • Chống giật: tổ hợp phải chạm đủ 90 ms mới bật; nhả lệch một tổ hợp thì miếng còn lại bị bỏ qua 150 ms.
    ///  • Đi / quay ra trục Joy2 (làm mượt 8 đơn vị/giây); Cầm → cờ chạm B; Dùng → cờ chạm A; Nói → cờ Talk; Căn lại (giữ 1 s) → cờ
    ///    Recentered. Các phần còn lại của game (PadInput, HandController, VoiceChat, VeraPointer) không cần biết có 5 miếng.
    /// An toàn khi gọi từ luồng đọc cổng COM / UDP: cấu hình là mảng bất biến thay nguyên khối từ luồng chính.
    /// </summary>
    public static class VeraPadMap
    {
        public const int PadCount = 5;
        public const int TIEN = 1, LUI = 2, TRAI = 4, PHAI = 8, OK = 16;
        public static readonly string[] PadNames = { "TIẾN", "LÙI", "TRÁI", "PHẢI", "OK" };
        public static readonly string[] PadPins = { "GPIO27", "GPIO13", "GPIO32", "GPIO33", "GPIO4" };

        public enum Act { Tien, Lui, Trai, Phai, Cam, Dung, Noi, CanLai }
        public const int ActCount = 8;
        public static readonly string[] ActNames =
        {
            "Đi tới", "Đi lùi", "Quay trái", "Quay phải", "Cầm / đặt (giữ để mang)", "Dùng / rót (giữ)",
            "Nói với trợ lý (giữ 3 giây)", "Căn lại hướng tia (giữ 1 giây)",
        };
        public static readonly int[] Defaults = { TIEN, LUI, TRAI, PHAI, OK, TIEN | LUI, TRAI | PHAI, TIEN | LUI | TRAI | PHAI };

        public const string PrefKey = "vera_to_hop_5_mieng";
        public const uint SettleMs = 90, BlackoutMs = 150, RecenterHoldMs = 1000;
        public const float SlewPerS = 8f;

        static int[] combos = (int[])Defaults.Clone();
        static int[] order = BuildOrder(combos);
        public static event Action Changed;

        public static int Get(Act a) => combos[(int)a];
        public static int[] Snapshot() => (int[])combos.Clone();

        static int Bits(int m) { int n = 0; for (; m != 0; m &= m - 1) n++; return n; }

        static int[] BuildOrder(int[] c)
        {
            var idx = new int[ActCount];
            for (int i = 0; i < ActCount; i++) idx[i] = i;
            // nhiều miếng trước; bằng nhau thì theo thứ tự danh sách
            Array.Sort(idx, (x, y) => { int d = Bits(c[y]) - Bits(c[x]); return d != 0 ? d : x.CompareTo(y); });
            return idx;
        }

        static void Apply(int[] c)
        {
            var o = BuildOrder(c);
            lock (gate) { combos = c; order = o; }
            Changed?.Invoke();
        }

        /// <summary>Đọc tổ hợp đã lưu (luồng chính, lúc mở game và sau khi đổi).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Load()
        {
            var c = (int[])Defaults.Clone();
            var s = PlayerPrefs.GetString(PrefKey, "");
            var parts = s.Split(',');
            if (parts.Length == ActCount)
                for (int i = 0; i < ActCount; i++)
                    if (int.TryParse(parts[i], out int v) && v > 0 && v < 32) c[i] = v;
            Apply(c);
        }

        static void Save(int[] c)
        {
            PlayerPrefs.SetString(PrefKey, string.Join(",", c));
            PlayerPrefs.Save();
        }

        /// <summary>Bật / tắt một miếng trong tổ hợp của thao tác (không cho rỗng). Lưu ngay.</summary>
        public static void Toggle(Act a, int pad)
        {
            var c = Snapshot();
            int m = c[(int)a] ^ pad;
            if (m == 0) return;
            c[(int)a] = m;
            Save(c);
            Apply(c);
        }

        public static void ResetDefaults()
        {
            var c = (int[])Defaults.Clone();
            Save(c);
            Apply(c);
        }

        public static string Describe(int mask)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < PadCount; i++)
                if ((mask & (1 << i)) != 0) sb.Append(sb.Length > 0 ? " + " : "").Append(PadNames[i]);
            return sb.Length > 0 ? sb.ToString() : "(chưa chọn)";
        }

        /// <summary>Thao tác khác có cùng tổ hợp (thao tác đứng trước trong danh sách thắng), null nếu không trùng.</summary>
        public static string Conflict(Act a)
        {
            var c = combos;
            for (int i = 0; i < ActCount; i++)
                if (i != (int)a && c[i] == c[(int)a]) return ActNames[i];
            return null;
        }

        // ------------------------------------------------------------------
        // Giải tổ hợp
        // ------------------------------------------------------------------

        public struct Result
        {
            public Vector2 Move;      // x: quay phải +, y: đi tới +
            public bool Grab, Use, Talk, Recenter;
            public int ActiveMask;    // bit i: thao tác i đang bật
        }

        static readonly object gate = new object();
        static readonly uint[] pressAt = new uint[PadCount];
        static readonly uint[] blackoutUntil = new uint[PadCount];
        static int prevPressed, prevClaimedMulti;
        static uint lastMs, recenterSince;
        static bool haveLast, recenterFired;
        static Vector2 move;

        public static void ResetState()
        {
            lock (gate)
            {
                prevPressed = prevClaimedMulti = 0;
                haveLast = false;
                move = Vector2.zero;
                recenterSince = 0;
                recenterFired = false;
                for (int i = 0; i < PadCount; i++) pressAt[i] = blackoutUntil[i] = 0;
            }
        }

        static bool After(uint now, uint t) => (int)(now - t) >= 0;

        /// <summary>pressed: bit i = miếng i đang chạm (TIẾN, LÙI, TRÁI, PHẢI, OK); ms: đồng hồ của tay cầm.</summary>
        public static Result Step(int pressed, uint ms)
        {
            lock (gate)
            {
                float dt = haveLast ? Mathf.Clamp((ms - lastMs) / 1000f, 0f, 0.1f) : 0f;
                lastMs = ms;
                haveLast = true;
                for (int i = 0; i < PadCount; i++)
                {
                    int bit = 1 << i;
                    if ((pressed & bit) != 0 && (prevPressed & bit) == 0) pressAt[i] = ms;
                }

                var c = combos;
                var o = order;
                int claimed = 0, claimedMulti = 0, active = 0;
                foreach (int a in o)
                {
                    int m = c[a];
                    if ((m & pressed) != m || (m & claimed) != 0) continue;
                    claimed |= m;
                    bool multi = (m & (m - 1)) != 0;
                    if (multi) claimedMulti |= m;
                    // đủ 90 ms tính từ miếng chạm sau cùng của tổ hợp; tổ hợp một miếng còn phải qua thời gian bỏ qua
                    uint last = 0;
                    bool any = false, blocked = false;
                    for (int i = 0; i < PadCount; i++)
                        if ((m & (1 << i)) != 0)
                        {
                            if (!any || (int)(pressAt[i] - last) > 0) last = pressAt[i];
                            any = true;
                            if (!multi && !After(ms, blackoutUntil[i])) blocked = true;
                        }
                    if (!blocked && After(ms, last + SettleMs)) active |= 1 << a;
                }
                // nhả lệch một tổ hợp nhiều miếng: miếng còn chạm bị bỏ qua một lúc (không "lùi một cái" khi buông cầm)
                int released = prevClaimedMulti & ~claimedMulti;
                for (int i = 0; i < PadCount; i++)
                    if ((released & (1 << i)) != 0 && (pressed & (1 << i)) != 0) blackoutUntil[i] = ms + BlackoutMs;
                prevClaimedMulti = claimedMulti;
                prevPressed = pressed;

                bool On(Act x) => (active & (1 << (int)x)) != 0;
                float ty = (On(Act.Tien) ? 1f : 0f) - (On(Act.Lui) ? 1f : 0f);
                float tx = (On(Act.Phai) ? 1f : 0f) - (On(Act.Trai) ? 1f : 0f);
                if (tx != 0 && ty != 0) { tx *= 0.7071f; ty *= 0.7071f; }
                float step = SlewPerS * dt;
                move.x += Mathf.Clamp(tx - move.x, -step, step);
                move.y += Mathf.Clamp(ty - move.y, -step, step);

                bool rec = false;
                if (On(Act.CanLai))
                {
                    if (recenterSince == 0) recenterSince = ms == 0 ? 1 : ms;
                    // một nhịp duy nhất: VeraPointer căn lại ở sườn lên; giữ cờ lâu thì tia đứng yên (bỏ qua chuyển động) suốt lúc giữ
                    if (!recenterFired && After(ms, recenterSince + RecenterHoldMs)) { recenterFired = true; rec = true; }
                }
                else { recenterSince = 0; recenterFired = false; }

                return new Result
                {
                    Move = move,
                    Grab = On(Act.Cam),
                    Use = On(Act.Dung),
                    Talk = On(Act.Noi),
                    Recenter = rec,
                    ActiveMask = active,
                };
            }
        }
    }
}
