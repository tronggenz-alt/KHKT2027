using UnityEngine;

namespace Khkt.Demo
{
    /// <summary>
    /// Da VERA Instrument (tấm vát góc, xanh mực, viền lam ngọc) cho các màn hình vẽ bằng IMGUI (góc nhìn cận cảnh, thông báo):
    ///   • Begin() phóng cả giao diện theo màn hình. IMGUI tính bằng điểm ảnh thật nên trên iPad 2360 × 1640 nút 36 px
    ///     chỉ cao khoảng 14 pt, khó bấm trúng; sau khi phóng nút cao khoảng 39 pt.
    ///   • Button / ButtonOn / Panel: nền bo góc xanh đen, viền sáng mảnh, nút đang chọn màu ngọc lam, phông Be Vietnam Pro,
    ///     cùng tông với phần còn lại của app thay cho da xám mặc định của Unity.
    /// </summary>
    public static class GuiKit
    {
        static GUIStyle button, buttonOn, panel, caption;
        static Texture2D texIdle, texHover, texDown, texOn, texPanel;

        static GuiKit()
        {
            UiTheme.Changed += InvalidateThemeCache;
        }

        static void InvalidateThemeCache()
        {
            DestroyTex(ref texIdle);
            DestroyTex(ref texHover);
            DestroyTex(ref texDown);
            DestroyTex(ref texOn);
            DestroyTex(ref texPanel);
            button = buttonOn = panel = caption = null;
        }

        static void DestroyTex(ref Texture2D tex)
        {
            if (tex == null) return;
            if (Application.isPlaying) Object.Destroy(tex);
            else Object.DestroyImmediate(tex);
            tex = null;
        }

        /// <summary>Hệ số phóng: màn cảm ứng quy về bề ngang 1100 đơn vị (nút to vừa ngón tay), máy tính 1600.</summary>
        public static float Scale
        {
            get
            {
                bool touch = Application.isMobilePlatform || Khkt.Input.PadInput.UsingTouch;
                float s = Mathf.Min(Screen.width / (touch ? 1100f : 1600f), Screen.height / (touch ? 700f : 900f));
                return Mathf.Max(1f, s);
            }
        }

        /// <summary>Gọi đầu OnGUI: đặt ma trận phóng, trả về kích thước màn hình tính theo đơn vị đã phóng.</summary>
        public static Vector2 Begin()
        {
            float s = Scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            return new Vector2(Screen.width / s, Screen.height / s);
        }

        public static GUIStyle Button => button ??= MakeButton(false);
        /// <summary>Nút đang được chọn (núm đang vặn, chế độ đang bật).</summary>
        public static GUIStyle ButtonOn => buttonOn ??= MakeButton(true);

        public static GUIStyle Panel
        {
            get
            {
                if (panel != null) return panel;
                Color border = UiTheme.IsVera ? new Color(0.45f, 0.78f, 1f, 0.40f) : GlassKit.Line;
                texPanel = Rounded(GlassKit.SurfaceA(0.95f), border);
                panel = new GUIStyle
                {
                    font = GlassKit.Regular,
                    fontSize = 13,
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true,
                    richText = true,
                    border = new RectOffset(12, 12, 12, 12),
                    padding = new RectOffset(14, 14, 8, 8),
                };
                panel.normal.background = texPanel;
                panel.normal.textColor = GlassKit.Ink;
                return panel;
            }
        }

        /// <summary>Chữ nhãn nhỏ không nền (chú thích trên ảnh).</summary>
        public static GUIStyle Caption
        {
            get
            {
                if (caption != null) return caption;
                caption = new GUIStyle { font = GlassKit.Semibold, fontSize = 12, alignment = TextAnchor.MiddleLeft };
                caption.normal.textColor = GlassKit.Warn;
                return caption;
            }
        }

        static GUIStyle MakeButton(bool on)
        {
            Color borderIdle = UiTheme.IsVera ? new Color(0.45f, 0.78f, 1f, 0.38f) : GlassKit.Line;
            Color hoverFill = UiTheme.Pick(
                GlassKit.Well,
                new Color(0.20f, 0.20f, 0.23f, 0.96f),
                new Color(0.11f, 0.17f, 0.28f, 0.94f));
            Color downFill = UiTheme.IsLight
                ? Color.Lerp(GlassKit.Well, GlassKit.Accent, 0.22f)
                : new Color(GlassKit.Accent.r * 0.55f, GlassKit.Accent.g * 0.55f, GlassKit.Accent.b * 0.6f, 0.96f);
            Color onFill = UiTheme.IsVera ? GlassKit.AccentFill : GlassKit.Accent;
            Color onBorder = UiTheme.IsLight ? GlassKit.Accent : new Color(1f, 1f, 1f, 0.45f);

            texIdle ??= Rounded(GlassKit.ButtonFill, borderIdle);
            texHover ??= Rounded(hoverFill, new Color(GlassKit.Accent.r, GlassKit.Accent.g, GlassKit.Accent.b, UiTheme.IsLight ? 0.50f : 0.70f));
            texDown ??= Rounded(downFill, GlassKit.Accent);
            texOn ??= Rounded(onFill, onBorder);
            var st = new GUIStyle
            {
                font = GlassKit.Semibold,
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                border = new RectOffset(12, 12, 12, 12),
                padding = new RectOffset(8, 8, 4, 4),
                clipping = TextClipping.Clip,
            };
            Color ink = on ? GlassKit.OnAccent : GlassKit.Ink;
            st.normal.background = on ? texOn : texIdle;
            st.hover.background = on ? texOn : texHover;
            st.active.background = texDown;
            st.normal.textColor = st.hover.textColor = ink;
            st.active.textColor = UiTheme.IsLight ? GlassKit.Accent : Color.white;
            return st;
        }

        /// <summary>Ô 32 × 32 (vát góc ở Xanh mực, bo tròn ở Trắng/Đen) có viền 1 px, dùng làm nền 9 mảnh cho GUIStyle.</summary>
        static Texture2D Rounded(Color fill, Color border)
        {
            const int size = 32;
            const float radius = 10f;
            bool chamfer = UiTheme.IsVera;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var px = new Color[size * size];
            float half = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float ax = Mathf.Abs(x + 0.5f - half), ay = Mathf.Abs(y + 0.5f - half);
                    float outside;
                    if (chamfer)
                    {
                        outside = Mathf.Max(Mathf.Max(ax, ay) - half, (ax + ay - (2f * half - radius * 0.8f)) * 0.70710678f) + 0.75f;
                    }
                    else
                    {
                        float qx = ax - (half - 9f), qy = ay - (half - 9f);
                        outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - 9f;
                    }
                    float a = Mathf.Clamp01(0.5f - outside);
                    float edge = Mathf.Clamp01(1f - Mathf.Abs(outside + 0.75f) / 1.25f);
                    Color c = Color.Lerp(fill, new Color(border.r, border.g, border.b, Mathf.Max(fill.a, border.a)), edge * border.a);
                    c.a *= a;
                    px[y * size + x] = c;
                }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }
    }
}
