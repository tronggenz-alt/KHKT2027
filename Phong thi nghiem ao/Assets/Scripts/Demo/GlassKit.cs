using UnityEngine;
using UnityEngine.UI;

namespace Khkt.Demo
{
    /// <summary>
    /// Bộ dựng giao diện kiểu kính mờ: sprite bo góc sinh bằng code, vật liệu shader Khkt/KinhMo,
    /// phông Segoe UI của Windows (không chép file phông vào project), chữ, chip phím.
    /// </summary>
    public static class GlassKit
    {
        public static readonly Color Ink = new Color(0.96f, 0.97f, 1f, 0.97f);
        public static readonly Color Dim = new Color(0.78f, 0.84f, 0.93f, 0.75f);
        public static readonly Color Accent = new Color(0.38f, 0.86f, 1f, 1f);
        public static readonly Color Good = new Color(0.45f, 0.95f, 0.62f, 1f);
        public static readonly Color Warn = new Color(1f, 0.78f, 0.35f, 1f);
        public static readonly Color Bad = new Color(1f, 0.45f, 0.45f, 1f);

        static Sprite rounded, circle, solid, solidCircle;
        static Font regular, semibold;
        static Material glass, glassLight;

        public static Sprite Rounded => rounded != null ? rounded : rounded = MakeRounded(96, 30, 1.6f);
        public static Sprite Circle => circle != null ? circle : circle = MakeRounded(64, 32, 1.6f);
        /// <summary>Bản màu trắng đặc (dùng với vật liệu UI thường để tô màu nền nút, thanh, chấm).</summary>
        public static Sprite Solid => solid != null ? solid : solid = MakeRounded(96, 30, 1.6f, true);
        public static Sprite SolidCircle => solidCircle != null ? solidCircle : solidCircle = MakeRounded(64, 32, 1.6f, true);

        public static Font Regular => regular != null ? regular : regular = LoadFont(false);
        public static Font Semibold => semibold != null ? semibold : semibold = LoadFont(true);

        static Font LoadFont(bool bold)
        {
            string[] installed = Font.GetOSInstalledFontNames();
            string[] wish = bold ? new[] { "Segoe UI Semibold", "Segoe UI Bold", "Segoe UI" } : new[] { "Segoe UI", "Arial" };
            foreach (var w in wish)
                if (System.Array.IndexOf(installed, w) >= 0)
                    return Font.CreateDynamicFontFromOSFont(w, 32);
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        /// <summary>Vật liệu kính mờ tối (mặc định) hoặc sáng hơn (chip, nút).</summary>
        public static Material Glass(Shader shader, bool light = false)
        {
            if (!light && glass != null) return glass;
            if (light && glassLight != null) return glassLight;
            var m = new Material(shader) { hideFlags = HideFlags.DontSave };
            m.SetColor("_Tint", light ? new Color(0.75f, 0.85f, 1f, 0.22f) : new Color(0.06f, 0.1f, 0.18f, 0.5f));
            m.SetFloat("_Dark", light ? 0.2f : 0.3f);
            m.SetColor("_BorderColor", new Color(1f, 1f, 1f, light ? 0.5f : 0.35f));
            m.SetFloat("_Blur", light ? 6f : 16f);
            if (light) glassLight = m; else glass = m;
            return m;
        }

        /// <summary>Hình chữ nhật bo góc: kênh A là phần trong, kênh R là đường viền mảnh.</summary>
        static Sprite MakeRounded(int size, float radius, float border, bool white = false)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var px = new Color[size * size];
            float half = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // khoảng cách có dấu tới cạnh hình chữ nhật bo góc
                    float qx = Mathf.Abs(x + 0.5f - half) - (half - radius);
                    float qy = Mathf.Abs(y + 0.5f - half) - (half - radius);
                    float outside = new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0) - radius;
                    float a = Mathf.Clamp01(0.5f - outside);
                    float edge = Mathf.Clamp01(1f - Mathf.Abs(outside + border * 0.5f) / (border * 0.5f + 0.5f));
                    px[y * size + x] = white ? new Color(1, 1, 1, a) : new Color(edge, 0, 0, a);
                }
            tex.SetPixels(px);
            tex.Apply();
            float b = Mathf.Min(radius + 2, half - 1);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        public static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image Panel(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size,
                                  Material mat, float cornerScale = 1f)
        {
            var rt = Rect(parent, name, anchor, pivot, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Rounded;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = cornerScale;
            img.material = mat;
            img.raycastTarget = false;
            return img;
        }

        public static Text Label(Transform parent, string name, string text, int size, Color color, bool bold,
                                 TextAnchor align, Vector2 pos, Vector2 box, Vector2? anchor = null)
        {
            var a = anchor ?? new Vector2(0, 1);
            var rt = Rect(parent, name, a, new Vector2(0, 1), pos, box);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = bold ? Semibold : Regular;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.text = text;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.lineSpacing = 1.05f;
            return t;
        }

        /// <summary>Nút bấm bo góc (nền màu, chữ giữa). primary: nền màu nhấn.</summary>
        public static Button MakeButton(Transform parent, string name, string text, Vector2 pos, Vector2 size, System.Action onClick,
                                        bool primary = false, int fontSize = 16, Vector2? anchor = null)
        {
            var a = anchor ?? new Vector2(0, 1);
            var rt = Rect(parent, name, a, new Vector2(0, 1), pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Solid;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 2.2f;
            img.color = primary ? new Color(Accent.r, Accent.g, Accent.b, 0.95f) : new Color(1, 1, 1, 0.13f);
            var b = rt.gameObject.AddComponent<Button>();
            var colors = b.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.colorMultiplier = 1.4f;
            b.colors = colors;
            b.targetGraphic = img;
            if (onClick != null) b.onClick.AddListener(() => onClick());
            var t = Label(rt, "Chữ", text, fontSize, primary ? new Color(0.03f, 0.07f, 0.12f) : Ink, primary, TextAnchor.MiddleCenter, Vector2.zero, size);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            return b;
        }

        /// <summary>Ô nhập chữ trên nền kính sáng.</summary>
        public static InputField MakeInput(Transform parent, string name, string placeholder, Vector2 pos, Vector2 size, int fontSize = 17)
        {
            var rt = Rect(parent, name, new Vector2(0, 1), new Vector2(0, 1), pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Solid;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 2.2f;
            img.color = new Color(1, 1, 1, 0.12f);
            var text = Label(rt, "Chữ", "", fontSize, Ink, false, TextAnchor.MiddleLeft, new Vector2(14, 0), new Vector2(size.x - 28, size.y));
            text.supportRichText = false;
            var ph = Label(rt, "Gợi ý", placeholder, fontSize, new Color(Dim.r, Dim.g, Dim.b, 0.6f), false, TextAnchor.MiddleLeft, new Vector2(14, 0), new Vector2(size.x - 28, size.y));
            var f = rt.gameObject.AddComponent<InputField>();
            f.textComponent = text;
            f.placeholder = ph;
            f.targetGraphic = img;
            f.caretColor = Accent;
            f.selectionColor = new Color(Accent.r, Accent.g, Accent.b, 0.35f);
            f.customCaretColor = true;
            return f;
        }

        /// <summary>Chiều rộng dòng chữ (để co giãn chip, thanh gợi ý).</summary>
        public static float Width(Text t, string s)
        {
            var settings = t.GetGenerationSettings(new Vector2(4000, 200));
            return t.cachedTextGeneratorForLayout.GetPreferredWidth(s, settings) / t.pixelsPerUnit;
        }
    }
}
