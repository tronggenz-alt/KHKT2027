using System;
using UnityEngine;
using UnityEngine.UI;

namespace Khkt.Demo
{
    /// <summary>
    /// Bộ dựng giao diện VERA Instrument (dựng lại 2026-11-13, chủ đề khoa học và kỹ thuật):
    ///   • Hình khối: tấm vát góc 45° như mặt máy đo, viền mảnh, bốn ke góc và một vạch nhấn ở mép trên.
    ///   • Nền: kính tối màu xanh mực có lưới bản vẽ kỹ thuật (shader Khkt/KinhMo), chữ sáng, một màu nhấn lam ngọc
    ///     cho thao tác và màu hổ phách cho số đo, cảnh báo.
    ///   • Mảnh ghép dùng chung: Header (mã bảng + tiêu đề + đường kẻ), Rule, MakeSlider, MakeChip, Readout, Backdrop.
    ///   • Phông Be Vietnam Pro nhúng trong Resources/GiaoDien/Phong/ (đủ dấu tiếng Việt, chỉ số trên/dưới).
    ///   • Mọi nút tạo qua MakeButton đều có NutHieuUng, bỏ navigation để không dính tiêu điểm trên cảm ứng.
    ///   • Cờ ReducedTransparency: nền đặc thay kính mờ (chống lóa).
    /// Tên hàm và tham số giữ như bộ GlassKit cũ nên mọi màn hình đang dùng đều đổi theo.
    /// </summary>
    public static class GlassKit
    {
        public static readonly Color Ink = new Color(0.93f, 0.96f, 1f, 0.98f);
        public static readonly Color Dim = new Color(0.66f, 0.76f, 0.88f, 0.86f);
        public static readonly Color Muted = new Color(0.47f, 0.57f, 0.70f, 0.75f);
        /// <summary>Màu nhấn chính (lam ngọc): nút chính, mục đang chọn, con trỏ, đường nhấn.</summary>
        public static readonly Color Accent = new Color(0.20f, 0.84f, 1f, 1f);
        /// <summary>Màu nhấn phụ (hổ phách): số đo, đơn vị, mã bảng, chú thích kỹ thuật.</summary>
        public static readonly Color Accent2 = new Color(1f, 0.72f, 0.30f, 1f);
        public static readonly Color Good = new Color(0.30f, 0.89f, 0.60f, 1f);
        public static readonly Color Warn = new Color(1f, 0.74f, 0.32f, 1f);
        public static readonly Color Bad = new Color(1f, 0.38f, 0.40f, 1f);
        /// <summary>Nền đặc của bảng khi tắt kính mờ, và nền các ô bên trong bảng.</summary>
        public static readonly Color Surface = new Color(0.035f, 0.065f, 0.115f, 0.95f);
        public static readonly Color Well = new Color(0.02f, 0.04f, 0.08f, 0.55f);
        public static readonly Color Line = new Color(0.45f, 0.78f, 1f, 0.22f);
        /// <summary>Màu chữ trên nền màu nhấn.</summary>
        public static readonly Color OnAccent = new Color(0.02f, 0.07f, 0.12f, 1f);

        public const string ReducedTransparencyPrefKey = "KHKT_GIAM_TRONG_SUOT";
        public static bool ReducedTransparency
        {
            get => PlayerPrefs.GetInt(ReducedTransparencyPrefKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(ReducedTransparencyPrefKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        static Sprite rounded, circle, solid, solidCircle, plain;
        static Font regular, semibold;
        static Material glass, glassLight;

        /// <summary>Tấm vát góc cho shader kính mờ: A = hình, R = viền, G = ke góc, B = vạch nhấn mép trên.</summary>
        public static Sprite Rounded => rounded != null ? rounded : rounded = MakePlate(96, 20f, 1.5f, false);
        public static Sprite Circle => circle != null ? circle : circle = MakeRounded(64, 32, 1.6f);
        /// <summary>Tấm vát góc màu trắng (dùng với vật liệu UI thường để tô màu nền nút, thanh, ô).</summary>
        public static Sprite Solid => solid != null ? solid : solid = MakePlate(96, 20f, 1.5f, true);
        public static Sprite SolidCircle => solidCircle != null ? solidCircle : solidCircle = MakeRounded(64, 32, 1.6f, true);
        /// <summary>Ô trắng vuông (đường kẻ, thanh trượt, nền phẳng).</summary>
        public static Sprite Plain
        {
            get
            {
                if (plain != null) return plain;
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
                var px = new Color[16];
                for (int k = 0; k < 16; k++) px[k] = Color.white;
                tex.SetPixels(px);
                tex.Apply();
                return plain = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
            }
        }

        public static Font Regular => regular != null ? regular : regular = LoadFont(false);
        public static Font Semibold => semibold != null ? semibold : semibold = LoadFont(true);

        static Font LoadFont(bool bold)
        {
            // 1. Ưu tiên phông Be Vietnam Pro nhúng trong Resources
            string resName = bold ? "GiaoDien/Phong/BeVietnamPro-SemiBold" : "GiaoDien/Phong/BeVietnamPro-Regular";
            var f = Resources.Load<Font>(resName);
            if (f != null) return f;

            // 2. Tra cứu phông hệ thống (ưu tiên SF Pro trên iOS/macOS, Segoe UI trên Windows)
            string[] installed = Font.GetOSInstalledFontNames();
            string[] wish = bold
                ? new[] { "SF Pro Text", ".AppleSystemUIFont", "Helvetica Neue", "Segoe UI Semibold", "Segoe UI Bold", "Arial Bold", "Arial" }
                : new[] { "SF Pro Text", ".AppleSystemUIFont", "Helvetica Neue", "Segoe UI", "Arial" };

            foreach (var w in wish)
            {
                if (Array.IndexOf(installed, w) >= 0)
                {
                    try
                    {
                        var dynamicFont = Font.CreateDynamicFontFromOSFont(w, 32);
                        if (dynamicFont != null) return dynamicFont;
                    }
                    catch { }
                }
            }

            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        /// <summary>Vật liệu kính mờ tối (mặc định) hoặc sáng hơn (chip, nút).</summary>
        public static Material Glass(Shader shader, bool light = false)
        {
            if (!light && glass != null) return glass;
            if (light && glassLight != null) return glassLight;
            var m = new Material(shader) { hideFlags = HideFlags.DontSave };
            m.SetColor("_Tint", light ? new Color(0.30f, 0.52f, 0.78f, 0.26f) : new Color(0.03f, 0.065f, 0.12f, 0.74f));
            m.SetFloat("_Dark", light ? 0.25f : 0.5f);
            m.SetColor("_BorderColor", light ? new Color(0.62f, 0.86f, 1f, 0.5f) : new Color(0.45f, 0.78f, 1f, 0.42f));
            m.SetFloat("_Blur", light ? 6f : 14f);
            if (m.HasProperty("_AccentColor")) m.SetColor("_AccentColor", new Color(Accent.r, Accent.g, Accent.b, light ? 0f : 0.9f));
            if (m.HasProperty("_Grid")) m.SetFloat("_Grid", light ? 0f : 1f);
            if (light) glassLight = m; else glass = m;
            return m;
        }

        /// <summary>
        /// Tấm vát góc 45°. white = false: A hình, R viền, G ke góc (hai nét ngắn ôm mỗi góc, lùi vào trong), B vạch nhấn
        /// dọc mép trên. white = true: trắng, viền sáng hơn thân một chút để nút tô màu vẫn có nét viền.
        /// </summary>
        static Sprite MakePlate(int size, float chamfer, float border, bool white)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var px = new Color[size * size];
            float half = size / 2f;
            const float inv = 0.70710678f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float ax = Mathf.Abs(x + 0.5f - half), ay = Mathf.Abs(y + 0.5f - half);
                    // khoảng cách có dấu tới mép tấm: cạnh thẳng và cạnh vát
                    float dBox = Mathf.Max(ax, ay) - half;
                    float dCut = (ax + ay - (2f * half - chamfer)) * inv;
                    float outside = Mathf.Max(dBox, dCut) + 0.75f;
                    float a = Mathf.Clamp01(0.5f - outside);
                    float edge = Mathf.Clamp01(1f - Mathf.Abs(outside + border * 0.5f) / (border * 0.5f + 0.5f));
                    if (white)
                    {
                        float v = Mathf.Lerp(0.90f, 1f, edge);
                        px[y * size + x] = new Color(v, v, v, a);
                        continue;
                    }
                    // ke góc: dải mảnh cách mép 5 px, chỉ trong vùng góc
                    float inset = -outside - 5f;
                    float bracket = Mathf.Clamp01(1f - Mathf.Abs(inset) / 1.1f);
                    bool corner = ax > half - chamfer - 9f && ay > half - chamfer - 9f;
                    // vạch nhấn mép trên: nằm trong dải 9 ô kéo giãn theo chiều ngang
                    float topLine = (y > size - 4 && ax < half - chamfer - 2f) ? 1f : 0f;
                    px[y * size + x] = new Color(edge, corner ? bracket * a : 0f, topLine * a, a);
                }
            tex.SetPixels(px);
            tex.Apply();
            float b = Mathf.Min(chamfer + 12f, half - 1);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
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

            // Nếu người dùng bật ReducedTransparency thì dùng nền đặc mờ nhẹ để chống lóa
            if (ReducedTransparency)
            {
                img.material = null;
                img.sprite = Solid;
                img.color = Surface;
            }

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
            t.lineSpacing = 1.15f; // Giãn dòng 1.15x chống cấn dấu tiếng Việt
            return t;
        }

        /// <summary>Nút bấm bo góc (nền màu, chữ giữa). primary: nền màu nhấn. Tự động gắn NutHieuUng.</summary>
        public static Button MakeButton(Transform parent, string name, string text, Vector2 pos, Vector2 size, System.Action onClick,
                                        bool primary = false, int fontSize = 16, Vector2? anchor = null)
        {
            var a = anchor ?? new Vector2(0, 1);
            var rt = Rect(parent, name, a, new Vector2(0, 1), pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Solid;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 2.2f;
            img.color = primary ? new Color(Accent.r, Accent.g, Accent.b, 0.96f) : new Color(0.36f, 0.62f, 0.92f, 0.20f);
            var b = rt.gameObject.AddComponent<Button>();
            
            // Tắt navigation để tránh dính focus trên cảm ứng iPad
            b.navigation = new Navigation { mode = Navigation.Mode.None };

            var colors = b.colors;
            colors.normalColor = Color.white;
            colors.normalColor = new Color(0.86f, 0.86f, 0.86f, 1f);
            colors.highlightedColor = Color.white;
            colors.selectedColor = colors.normalColor;
            colors.pressedColor = new Color(0.66f, 0.66f, 0.66f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.45f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            b.colors = colors;
            b.targetGraphic = img;
            if (onClick != null) b.onClick.AddListener(() => onClick());

            var t = Label(rt, "Chữ", text, fontSize, primary ? OnAccent : Ink, primary, TextAnchor.MiddleCenter, Vector2.zero, size);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;

            // Gắn bộ hiệu ứng cảm ứng NutHieuUng
            NutHieuUng.Attach(rt.gameObject);

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
            img.color = new Color(0.02f, 0.05f, 0.10f, 0.70f);
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

        // ------------------------------------------------------------------
        // Mảnh ghép của bộ VERA Instrument
        // ------------------------------------------------------------------

        /// <summary>Ô màu phẳng (đường kẻ, nền ô, thanh).</summary>
        public static Image Fill(Transform parent, string name, Vector2 pos, Vector2 size, Color color, Vector2? anchor = null, bool plate = false)
        {
            var a = anchor ?? new Vector2(0, 1);
            var rt = Rect(parent, name, a, new Vector2(0, 1), pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = plate ? Solid : Plain;
            if (plate) { img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 2.4f; }
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Đường kẻ mảnh ngang.</summary>
        public static Image Rule(Transform parent, Vector2 pos, float width, Color? color = null) =>
            Fill(parent, "Đường kẻ", pos, new Vector2(width, 1.5f), color ?? Line);

        /// <summary>
        /// Đầu bảng kiểu máy đo: mã bảng màu hổ phách (ví dụ "MÁY · PCR-01"), tiêu đề lớn, dòng phụ, đường kẻ có đoạn nhấn.
        /// Trả về chữ dòng phụ để cập nhật trạng thái.
        /// </summary>
        public static Text Header(Transform panel, string code, string title, string subtitle, float width, float x = 30f, float y = -22f)
        {
            Label(panel, "Mã bảng", code, 13, Accent2, true, TextAnchor.UpperLeft, new Vector2(x, y), new Vector2(width, 18));
            Label(panel, "Tiêu đề", title, 25, Ink, true, TextAnchor.UpperLeft, new Vector2(x, y - 19), new Vector2(width, 34));
            var sub = Label(panel, "Dòng phụ", subtitle, 15, Dim, false, TextAnchor.UpperLeft, new Vector2(x, y - 54), new Vector2(width, 22));
            sub.horizontalOverflow = HorizontalWrapMode.Overflow;
            Rule(panel, new Vector2(x, y - 82), width);
            Fill(panel, "Đoạn nhấn", new Vector2(x, y - 81), new Vector2(64, 3), Accent);
            return sub;
        }

        /// <summary>Ô số đo: nhãn nhỏ phía trên, giá trị lớn màu hổ phách. Trả về chữ giá trị.</summary>
        public static Text Readout(Transform parent, string name, string label, string value, Vector2 pos, Vector2 size)
        {
            var bg = Fill(parent, name, pos, size, Well, null, true);
            Label(bg.transform, "Nhãn", label, 12, Muted, true, TextAnchor.UpperLeft, new Vector2(12, -7), new Vector2(size.x - 20, 16));
            var v = Label(bg.transform, "Giá trị", value, 19, Accent2, true, TextAnchor.LowerLeft, new Vector2(12, -size.y + 30), new Vector2(size.x - 20, 24));
            v.horizontalOverflow = HorizontalWrapMode.Overflow;
            return v;
        }

        /// <summary>Thanh trượt: rãnh mảnh, phần đã kéo màu nhấn, núm tròn. Chạm được bằng ngón tay (cao tối thiểu 44).</summary>
        public static Slider MakeSlider(Transform parent, string name, Vector2 pos, Vector2 size, float min, float max, float value,
                                        Action<float> onChange, bool wholeNumbers = false, Vector2? anchor = null)
        {
            var a = anchor ?? new Vector2(0, 1);
            var rt = Rect(parent, name, a, new Vector2(0, 1), pos, size);
            // vùng bắt chạm trong suốt phủ cả chiều cao
            var hit = rt.gameObject.AddComponent<Image>();
            hit.sprite = Plain;
            hit.color = new Color(1, 1, 1, 0.001f);

            RectTransform Bar(Transform p, string n, Color c, float h)
            {
                var r = new GameObject(n, typeof(RectTransform)).GetComponent<RectTransform>();
                r.SetParent(p, false);
                var im = r.gameObject.AddComponent<Image>();
                im.sprite = Plain;
                im.color = c;
                im.raycastTarget = false;
                r.anchorMin = new Vector2(0, 0.5f);
                r.anchorMax = new Vector2(1, 0.5f);
                r.sizeDelta = new Vector2(0, h);
                r.anchoredPosition = Vector2.zero;
                return r;
            }

            var track = Bar(rt, "Rãnh", new Color(0.45f, 0.70f, 1f, 0.20f), 5f);
            track.offsetMin = new Vector2(13, track.offsetMin.y);
            track.offsetMax = new Vector2(-13, track.offsetMax.y);

            var fillArea = new GameObject("Vùng đã kéo", typeof(RectTransform)).GetComponent<RectTransform>();
            fillArea.SetParent(rt, false);
            fillArea.anchorMin = new Vector2(0, 0.5f);
            fillArea.anchorMax = new Vector2(1, 0.5f);
            fillArea.offsetMin = new Vector2(13, -2.5f);
            fillArea.offsetMax = new Vector2(-13, 2.5f);
            var fill = new GameObject("Đã kéo", typeof(RectTransform)).GetComponent<RectTransform>();
            fill.SetParent(fillArea, false);
            var fi = fill.gameObject.AddComponent<Image>();
            fi.sprite = Plain;
            fi.color = Accent;
            fi.raycastTarget = false;
            fill.sizeDelta = Vector2.zero;

            var handleArea = new GameObject("Vùng núm", typeof(RectTransform)).GetComponent<RectTransform>();
            handleArea.SetParent(rt, false);
            handleArea.anchorMin = Vector2.zero;
            handleArea.anchorMax = Vector2.one;
            handleArea.offsetMin = new Vector2(13, 0);
            handleArea.offsetMax = new Vector2(-13, 0);
            var handle = new GameObject("Núm", typeof(RectTransform)).GetComponent<RectTransform>();
            handle.SetParent(handleArea, false);
            var hi = handle.gameObject.AddComponent<Image>();
            hi.sprite = SolidCircle;
            hi.color = Ink;
            handle.sizeDelta = new Vector2(26, 0);
            handle.anchorMin = new Vector2(0, 0.5f);
            handle.anchorMax = new Vector2(0, 0.5f);
            var hrt = handle;
            hrt.sizeDelta = new Vector2(26, 26);
            var dot = Rect(handle, "Tâm", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10, 10)).gameObject.AddComponent<Image>();
            dot.sprite = SolidCircle;
            dot.color = Accent;
            dot.raycastTarget = false;

            var sl = rt.gameObject.AddComponent<Slider>();
            sl.navigation = new Navigation { mode = Navigation.Mode.None };
            sl.fillRect = fill;
            sl.handleRect = handle;
            sl.targetGraphic = hi;
            sl.direction = Slider.Direction.LeftToRight;
            sl.minValue = min;
            sl.maxValue = max;
            sl.wholeNumbers = wholeNumbers;
            sl.SetValueWithoutNotify(Mathf.Clamp(value, min, max));
            if (onChange != null) sl.onValueChanged.AddListener(v => onChange(v));
            return sl;
        }

        /// <summary>Nút chọn dạng chip (bật/tắt, chọn một trong nhóm). Đổi vẻ ngoài bằng SetChip.</summary>
        public static Button MakeChip(Transform parent, string name, string text, Vector2 pos, Vector2 size, Action onClick, bool on = false, int fontSize = 15)
        {
            var b = MakeButton(parent, name, text, pos, size, onClick, false, fontSize);
            SetChip(b, on);
            return b;
        }

        public static void SetChip(Button b, bool on)
        {
            if (b == null) return;
            var img = (Image)b.targetGraphic;
            img.color = on ? new Color(Accent.r, Accent.g, Accent.b, 0.96f) : new Color(0.36f, 0.62f, 0.92f, 0.20f);
            var t = b.GetComponentInChildren<Text>();
            if (t != null)
            {
                t.color = on ? OnAccent : Ink;
                t.font = on ? Semibold : Regular;
            }
        }

        /// <summary>Lớp mờ tối phủ sau một bảng nổi (bảng không chiếm cả màn hình): chặn chạm xuống cảnh, bấm ra ngoài để đóng.</summary>
        public static Button Backdrop(Transform canvas, string name, Action onClick, float alpha = 0.46f)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(canvas, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Plain;
            img.color = new Color(0.01f, 0.025f, 0.05f, alpha);
            var b = rt.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            if (onClick != null) b.onClick.AddListener(() => onClick());
            return b;
        }

        /// <summary>
        /// Canvas để đặt bảng nổi: dùng canvas của HUD (Screen Space - Camera, có kính mờ) nếu cảnh có ModernUI,
        /// không thì tạo một canvas phủ riêng dưới owner.
        /// </summary>
        public static Canvas HostCanvas(Transform owner, int sortingOrder = 40)
        {
            var ui = UnityEngine.Object.FindAnyObjectByType<ModernUI>();
            if (ui != null && ui.Canvas != null)
            {
                UiBridge.EnsureInteractive(ui.Canvas);
                return ui.Canvas;
            }
            var go = new GameObject("Giao diện nổi", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(owner, false);
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            UiBridge.EnsureInteractive(c);
            return c;
        }

        /// <summary>Khung bảng nổi ở giữa màn hình: kính mờ có lưới nếu canvas hỗ trợ, không thì tấm đặc cùng màu.</summary>
        public static Image Window(Transform parent, string name, Vector2 size, Vector2 pos)
        {
            var canvas = parent.GetComponentInParent<Canvas>();
            var shader = Shader.Find("Khkt/KinhMo");
            bool glassOk = shader != null && canvas != null && canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceCamera;
            var mid = new Vector2(0.5f, 0.5f);
            var img = Panel(parent, name, mid, mid, pos, size, glassOk ? Glass(shader) : null, 1.15f);
            if (!glassOk)
            {
                img.sprite = Solid;
                img.color = Surface;
            }
            img.raycastTarget = true;  // chạm vào thân bảng không lọt xuống lớp nền mờ (không tự đóng)
            return img;
        }

        /// <summary>Chiều rộng dòng chữ (để co giãn chip, thanh gợi ý).</summary>
        public static float Width(Text t, string s)
        {
            var settings = t.GetGenerationSettings(new Vector2(4000, 200));
            return t.cachedTextGeneratorForLayout.GetPreferredWidth(s, settings) / t.pixelsPerUnit;
        }
    }
}
