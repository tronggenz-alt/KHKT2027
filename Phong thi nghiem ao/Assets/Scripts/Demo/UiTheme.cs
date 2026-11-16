using System;
using UnityEngine;

namespace Khkt.Demo
{
    /// <summary>
    /// Quản lý chủ đề giao diện dùng chung cho toàn bộ ứng dụng (3 nền: Xanh mực VERA, Trắng, Đen).
    /// Lưu lựa chọn vào PlayerPrefs và phát sự kiện Changed khi thay đổi.
    /// </summary>
    public static class UiTheme
    {
        public enum Mode
        {
            XanhMuc = 0,
            Trang = 1,
            Den = 2
        }

        public const string PrefKey = "KHKT_NEN_GIAO_DIEN";
        /// <summary>0 = Cầm ngay, 1 = Mở bảng.</summary>
        public const string ClickModePrefKey = "KHKT_BAM_DUNG_CU";
        /// <summary>0 = Chuẩn (1.0), 1 = Lớn (1.12), 2 = Rất lớn (1.22).</summary>
        public const string FontScalePrefKey = "KHKT_CO_CHU";
        /// <summary>0 = 60%, 1 = 75% (mặc định), 2 = Kín màn hình.</summary>
        public const string PanelSizePrefKey = "KHKT_CO_BANG";

        public static event Action Changed;

        static Mode current;
        static int fontScaleMode;
        static int panelSizeMode = 1;
        static bool loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            loaded = false;
            current = Mode.XanhMuc;
            fontScaleMode = 0;
            panelSizeMode = 1;
        }

        static void EnsureLoaded()
        {
            if (loaded) return;
            current = Clamp(PlayerPrefs.GetInt(PrefKey, (int)Mode.XanhMuc));
            fontScaleMode = Mathf.Clamp(PlayerPrefs.GetInt(FontScalePrefKey, 0), 0, 2);
            panelSizeMode = Mathf.Clamp(PlayerPrefs.GetInt(PanelSizePrefKey, 1), 0, 2);
            loaded = true;
        }

        static Mode Clamp(int raw) => (Mode)Mathf.Clamp(raw, (int)Mode.XanhMuc, (int)Mode.Den);

        public static Mode Current
        {
            get
            {
                EnsureLoaded();
                return current;
            }
            set => Set(value, true);
        }

        public static void Set(Mode m, bool save = true)
        {
            EnsureLoaded();
            var next = Clamp((int)m);
            bool changed = next != current;
            current = next;
            if (save)
            {
                PlayerPrefs.SetInt(PrefKey, (int)current);
                PlayerPrefs.Save();
            }
            if (changed) Changed?.Invoke();
        }

        public static void ReloadFromPrefs()
        {
            var nextMode = Clamp(PlayerPrefs.GetInt(PrefKey, (int)Mode.XanhMuc));
            var nextFont = Mathf.Clamp(PlayerPrefs.GetInt(FontScalePrefKey, 0), 0, 2);
            var nextPanel = Mathf.Clamp(PlayerPrefs.GetInt(PanelSizePrefKey, 1), 0, 2);
            bool changed = !loaded || nextMode != current || nextFont != fontScaleMode || nextPanel != panelSizeMode;
            current = nextMode;
            fontScaleMode = nextFont;
            panelSizeMode = nextPanel;
            loaded = true;
            if (changed) Changed?.Invoke();
        }

        /// <summary>Cỡ chữ: 0 = Chuẩn (1.0f), 1 = Lớn (1.12f), 2 = Rất lớn (1.22f).</summary>
        public static int FontScaleMode
        {
            get
            {
                EnsureLoaded();
                return fontScaleMode;
            }
            set => SetFontScale(value);
        }

        public static float FontScale => FontScaleMode == 1 ? 1.12f : FontScaleMode == 2 ? 1.22f : 1.0f;

        public static void SetFontScale(int mode)
        {
            EnsureLoaded();
            int next = Mathf.Clamp(mode, 0, 2);
            bool changed = next != fontScaleMode;
            fontScaleMode = next;
            PlayerPrefs.SetInt(FontScalePrefKey, fontScaleMode);
            PlayerPrefs.Save();
            if (changed) Changed?.Invoke();
        }

        /// <summary>Kích thước bảng dụng cụ: 0 = 60%, 1 = 75% (mặc định), 2 = Kín màn hình.</summary>
        public static int PanelSizeMode
        {
            get
            {
                EnsureLoaded();
                return panelSizeMode;
            }
            set => SetPanelSize(value);
        }

        public static float PanelWidthFactor => PanelSizeMode == 0 ? 0.88f : PanelSizeMode == 2 ? 1.13f : 1.0f;

        public static Vector2 PanelSize(Vector2 base75Size)
        {
            int mode = PanelSizeMode;
            if (mode == 1) return base75Size;
            if (mode == 0) return new Vector2(Mathf.Round(base75Size.x * 0.88f), Mathf.Round(base75Size.y * 0.88f));
            return new Vector2(Mathf.Min(1896f, Mathf.Round(base75Size.x * 1.14f)), Mathf.Min(1056f, Mathf.Round(base75Size.y * 1.12f)));
        }

        public static float InstrumentWindowScale => PanelSizeMode == 0 ? 0.775f : PanelSizeMode == 2 ? 0.98f : 0.865f;

        public static void SetPanelSize(int mode)
        {
            EnsureLoaded();
            int next = Mathf.Clamp(mode, 0, 2);
            bool changed = next != panelSizeMode;
            panelSizeMode = next;
            PlayerPrefs.SetInt(PanelSizePrefKey, panelSizeMode);
            PlayerPrefs.Save();
            if (changed) Changed?.Invoke();
        }

        public static bool IsLight => Current == Mode.Trang;
        public static bool IsDark => Current == Mode.Den;
        public static bool IsVera => Current == Mode.XanhMuc;

        public static T Pick<T>(T trang, T den, T xanhMuc)
        {
            switch (Current)
            {
                case Mode.Trang: return trang;
                case Mode.Den: return den;
                default: return xanhMuc;
            }
        }

        public static string ModeLabel(Mode m)
        {
            switch (m)
            {
                case Mode.Trang: return "Trắng";
                case Mode.Den: return "Đen";
                default: return "Xanh mực";
            }
        }
    }
}

