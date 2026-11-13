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
                texPanel = Rounded(new Color(0.03f, 0.06f, 0.11f, 0.93f), new Color(0.45f, 0.78f, 1f, 0.40f));
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
            texIdle ??= Rounded(new Color(0.06f, 0.11f, 0.19f, 0.92f), new Color(0.45f, 0.78f, 1f, 0.38f));
            texHover ??= Rounded(new Color(0.11f, 0.17f, 0.28f, 0.94f), new Color(GlassKit.Accent.r, GlassKit.Accent.g, GlassKit.Accent.b, 0.70f));
            texDown ??= Rounded(new Color(GlassKit.Accent.r * 0.55f, GlassKit.Accent.g * 0.55f, GlassKit.Accent.b * 0.6f, 0.96f), GlassKit.Accent);
            texOn ??= Rounded(new Color(GlassKit.Accent.r, GlassKit.Accent.g, GlassKit.Accent.b, 0.95f), new Color(1f, 1f, 1f, 0.55f));
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
            st.active.textColor = Color.white;
            return st;
        }

        /// <summary>Ô vát góc 32 × 32 có viền 1 px, dùng làm nền 9 mảnh cho GUIStyle.</summary>
        static Texture2D Rounded(Color fill, Color border)
        {
            const int size = 32;
            const float radius = 10f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var px = new Color[size * size];
            float half = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // tấm vát góc 45° thay cho bo tròn
                    float ax = Mathf.Abs(x + 0.5f - half), ay = Mathf.Abs(y + 0.5f - half);
                    float outside = Mathf.Max(Mathf.Max(ax, ay) - half, (ax + ay - (2f * half - radius * 0.8f)) * 0.70710678f) + 0.75f;
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
