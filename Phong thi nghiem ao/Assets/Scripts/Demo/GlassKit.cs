using System;
using UnityEngine;
using UnityEngine.UI;

namespace Khkt.Demo
{
    /// <summary>
    /// Bộ dựng giao diện VERA Instrument (dựng lại 2026-11-13, chủ đề khoa học và kỹ thuật):
    ///   • Hình khối: bo góc liên tục (squircle) ở cả ba chủ đề, viền mảnh.
    ///   • Nền: kính lỏng (shader Khkt/KinhMo), chủ đề Xanh mực có lưới bản vẽ kỹ thuật, chữ sáng, một màu nhấn lam ngọc
    ///     cho thao tác và màu hổ phách cho số đo, cảnh báo.
    ///   • Mảnh ghép dùng chung: Header (mã bảng + tiêu đề + đường kẻ), Rule, MakeSlider, MakeChip, Readout, Backdrop.
    ///   • Phông Be Vietnam Pro nhúng trong Resources/GiaoDien/Phong/ (đủ dấu tiếng Việt, chỉ số trên/dưới).
    ///   • Mọi nút tạo qua MakeButton đều có NutHieuUng, bỏ navigation để không dính tiêu điểm trên cảm ứng.
    ///   • Cờ ReducedTransparency: nền đặc thay kính mờ (chống lóa).
    ///   • 27/11/2026: bảng là kính lỏng (Liquid Glass) — shader Khkt/KinhMo khúc xạ mép, tán sắc, viền phản chiếu theo đèn
    ///     (KinhLongAnhSang); mọi hình bo góc liên tục; nút, chip, công tắc, thanh trượt có chuyển động lò xo kiểu Apple
    ///     (NutHieuUng, KinhLongGiotChon, KinhLongCongTac, KinhLongNumTruot, KinhLongNgungTu).
    /// Tên hàm và tham số giữ như bộ GlassKit cũ nên mọi màn hình đang dùng đều đổi theo.
    /// </summary>
    public static class GlassKit
    {
        static Color H(int rgb, float a = 1f) =>
            new Color(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f, a);

        /// <summary>Nền trang tổng thể (PageBg).</summary>
        public static Color PageBg => UiTheme.Pick(H(0xf5f5f7), H(0x000000), H(0x0a1322));
        public static Color Ink => UiTheme.Pick(H(0x1d1d1f), H(0xf5f5f7), H(0xeaf1ff));
        public static Color Dim => UiTheme.Pick(H(0x5c5d66), H(0xa1a1a8), H(0x93a7c4));
        public static Color Muted => UiTheme.Pick(H(0x767782), H(0x7c7c84), H(0x7891b2));
        /// <summary>Màu nhấn chính: nút chính, mục đang chọn, con trỏ, đường nhấn.</summary>
        public static Color Accent => UiTheme.Pick(H(0x0b63d1), H(0x4da3ff), H(0x33d6ff));
        /// <summary>Màu nền tô đậm dùng cho nút chính và thanh gạt bật.</summary>
        public static Color AccentFill => UiTheme.Pick(H(0x0b63d1), H(0x0a84ff, 0.90f), H(0x22c3ec, 0.96f));
        /// <summary>Nền nhấn dịu cho chip đang chọn và thẻ nổi bật.</summary>
        public static Color AccentSoft => UiTheme.Pick(H(0xe6f0fc), H(0x4da3ff, 0.18f), H(0x33d6ff, 0.16f));
        /// <summary>Màu nhấn phụ (hổ phách): số đo, đơn vị, mã bảng, chú thích kỹ thuật.</summary>
        public static Color Accent2 => UiTheme.Pick(H(0x9a5600), H(0xffb84d), H(0xffb84d));
        static Color SoftOnSurface(int rgb, float weight = 0.22f, float surfaceAlpha = 0.96f)
        {
            Color b = SurfaceA(surfaceAlpha);
            Color fg = H(rgb);
            return new Color(
                Mathf.Lerp(b.r, fg.r, weight),
                Mathf.Lerp(b.g, fg.g, weight),
                Mathf.Lerp(b.b, fg.b, weight),
                surfaceAlpha);
        }

        public static Color Good => UiTheme.Pick(H(0x0b7a5b), H(0x4de399), H(0x4de399));
        public static Color GoodSoft => UiTheme.Pick(H(0xe3f4ee), SoftOnSurface(0x4de399, 0.20f), SoftOnSurface(0x4de399, 0.20f));
        public static Color Warn => UiTheme.Pick(H(0x9a5600), H(0xffb84d), H(0xffb84d));
        public static Color WarnSoft => UiTheme.Pick(H(0xfdf1dc), SoftOnSurface(0xffb84d, 0.20f), SoftOnSurface(0xffb84d, 0.20f));
        public static Color Bad => UiTheme.Pick(H(0xb3261e), H(0xff6b6b), H(0xff6b6b));
        public static Color BadSoft => UiTheme.Pick(H(0xfbe9e7), SoftOnSurface(0xff6b6b, 0.22f), SoftOnSurface(0xff6b6b, 0.22f));
        /// <summary>Nền đặc của bảng khi tắt kính mờ, và nền các ô bên trong bảng.</summary>
        public static Color Surface => SurfaceA(0.96f);
        public static Color Well => UiTheme.Pick(H(0xf1f2f5), H(0x232326), H(0x0a1322));
        public static Color Line => UiTheme.Pick(new Color(0f, 0f, 0f, 0.13f), new Color(1f, 1f, 1f, 0.16f), H(0x73c7ff, 0.26f));
        /// <summary>Màu chữ trên nền màu nhấn.</summary>
        public static Color OnAccent => UiTheme.Pick(H(0xffffff), H(0x00172e), H(0x05121f));
        /// <summary>Nền nút phụ có viền.</summary>
        public static Color ButtonFill => UiTheme.Pick(H(0xffffff), H(0x232326, 0.92f), H(0x1a2f4d, 0.82f));

        public static Color Wash(float a) => UiTheme.Pick(new Color(0f, 0f, 0f, a), new Color(1f, 1f, 1f, a), new Color(0.45f, 0.78f, 1f, a));
        public static Color SurfaceA(float a) => UiTheme.Pick(H(0xffffff, a), H(0x151517, a), H(0x111d31, a));
        public static Color Scrim(float a) => UiTheme.Pick(new Color(0.04f, 0.05f, 0.08f, a), new Color(0f, 0f, 0f, a), new Color(0.01f, 0.025f, 0.05f, a));

        public static string Hex(Color c) => $"#{ColorUtility.ToHtmlStringRGB(c)}";
        public static string HexGood => Hex(Good);
        public static string HexWarn => Hex(Warn);
        public static string HexBad => Hex(Bad);
        public static string HexAccent => Hex(Accent);
        public static string HexDim => Hex(Dim);
        public static string HexInk => Hex(Ink);

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

        public const string LiquidGlassPrefKey = "KHKT_KINH_LONG";
        /// <summary>
        /// Giao diện Kính lỏng (Liquid Glass, mặc định) hay Cổ điển (kính mờ phẳng, thẻ đặc như trước 27/11/2026).
        /// Đổi xong cần dựng lại màn (MainMenu tải lại cảnh); vật liệu đang có cập nhật ngay.
        /// </summary>
        public static bool LiquidGlass
        {
            get => PlayerPrefs.GetInt(LiquidGlassPrefKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(LiquidGlassPrefKey, value ? 1 : 0);
                PlayerPrefs.Save();
                if (glass != null) Configure(glass, false);
                if (glassLight != null) Configure(glassLight, true);
            }
        }

        /// <summary>Vật liệu kính lỏng cho mọi màn (kể cả ngoài lab, khúc xạ hình nền KinhLongNen). Null nếu thiếu shader.</summary>
        public static Material KinhLongMat(bool light = false)
        {
            var sh = Shader.Find("Khkt/KinhMo");
            return sh != null ? Glass(sh, light) : null;
        }

        public const string IncreasedContrastPrefKey = "KHKT_TANG_TUONG_PHAN";
        /// <summary>
        /// Tăng tương phản (như cài đặt Increase Contrast của Apple cho Liquid Glass): kính đặc hơn, viền rõ,
        /// bớt độ rực và phản chiếu. Đổi là cập nhật ngay vật liệu đang dùng, không cần dựng lại giao diện.
        /// </summary>
        public static bool IncreasedContrast
        {
            get => PlayerPrefs.GetInt(IncreasedContrastPrefKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(IncreasedContrastPrefKey, value ? 1 : 0);
                PlayerPrefs.Save();
                if (glass != null) Configure(glass, false);
                if (glassLight != null) Configure(glassLight, true);
            }
        }

        static Sprite rounded, circle, solid, solidCircle, plain, border;
        static Font regular, semibold;
        static Material glass, glassLight;

        static GlassKit()
        {
            UiTheme.Changed += InvalidateThemeCache;
        }

        static void InvalidateThemeCache()
        {
            DestroyObj(ref glass);
            DestroyObj(ref glassLight);
            DestroySprite(ref rounded);
            DestroySprite(ref solid);
            DestroySprite(ref border);
        }

        static void DestroyObj<T>(ref T obj) where T : UnityEngine.Object
        {
            if (obj == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(obj);
            else UnityEngine.Object.DestroyImmediate(obj);
            obj = null;
        }

        static void DestroySprite(ref Sprite sp)
        {
            if (sp == null) return;
            var tex = sp.texture;
            DestroyObj(ref sp);
            DestroyObj(ref tex);
        }

        /// <summary>Hình cho shader kính lỏng (độ sâu + pháp tuyến mép).</summary>
        public static Sprite Rounded => rounded != null ? rounded : rounded = MakeLiquidShape(128, 30f, LiquidDepthPx, 44f);

        /// <summary>Độ sâu (pixel sprite) mà kênh R của Rounded mã hóa, tính từ mép vào lòng kính.</summary>
        public const float LiquidDepthPx = 40f;
        public static Sprite Circle => circle != null ? circle : circle = MakeRounded(64, 32, 1.6f);
        /// <summary>Hình đặc màu trắng (dùng với vật liệu UI thường để tô màu nền nút, thanh, ô).</summary>
        public static Sprite Solid => solid != null ? solid : solid = MakeShape(96, 30f, 1.5f, true, false);
        public static Sprite SolidCircle => solidCircle != null ? solidCircle : solidCircle = MakeRounded(64, 32, 1.6f, true);
        /// <summary>Đường viền 9-slice khớp với Solid.</summary>
        public static Sprite Border => border != null ? border : border = MakeShape(96, 30f, 1.5f, false, true);
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

        /// <summary>Vật liệu kính mờ tối (mặc định) hoặc sáng hơn (chip, nút), tự đổi theo UiTheme.</summary>
        public static Material Glass(Shader shader, bool light = false)
        {
            if (!light && glass != null) return glass;
            if (light && glassLight != null) return glassLight;
            var m = new Material(shader) { hideFlags = HideFlags.DontSave };
            Configure(m, light);
            if (light) glassLight = m; else glass = m;
            return m;
        }

        /// <summary>Đặt thông số kính lỏng theo chủ đề, biến thể (bảng / chip) và cờ Tăng tương phản.</summary>
        static void Configure(Material m, bool light)
        {
            // Kính lỏng: khúc xạ mép, tán sắc, viền phản chiếu theo hướng đèn (KinhLongAnhSang), lớp phủ tự thích ứng.
            // Bảng (light = false) đặc hơn để chữ dài đọc dễ; chip, nút (light = true) trong hơn, khúc xạ mạnh hơn.
            void Liquid(float refraction, float chroma, float specular)
            {
                if (m.HasProperty("_Refraction")) m.SetFloat("_Refraction", refraction);
                if (m.HasProperty("_Chroma")) m.SetFloat("_Chroma", chroma);
                if (m.HasProperty("_Specular")) m.SetFloat("_Specular", specular);
                if (m.HasProperty("_Adaptive")) m.SetFloat("_Adaptive", 1f);
                if (m.HasProperty("_VertTint")) m.SetFloat("_VertTint", 0.42f);   // kính màu chỉ phớt màu
                if (m.HasProperty("_Vibrancy")) m.SetFloat("_Vibrancy", UiTheme.IsLight ? 1.05f : 1.2f);
                if (m.HasProperty("_EdgeClear")) m.SetFloat("_EdgeClear", 0.08f);
                if (m.HasProperty("_Reflect")) m.SetFloat("_Reflect", 0.08f);
            }
            if (UiTheme.IsLight)
            {
                m.SetColor("_Tint", light ? new Color(1f, 1f, 1f, 0.20f) : new Color(1f, 1f, 1f, 0.26f));
                m.SetFloat("_Dark", 0f);
                m.SetColor("_BorderColor", new Color(1f, 1f, 1f, 0.45f));
                m.SetFloat("_Blur", light ? 10f : 18f);   // trắng mờ đục nhẹ (frosted)
                if (m.HasProperty("_AccentColor")) m.SetColor("_AccentColor", Color.clear);
                if (m.HasProperty("_Grid")) m.SetFloat("_Grid", 0f);
                Liquid(light ? 16f : 14f, 0.35f, 1.1f);
            }
            else if (UiTheme.IsDark)
            {
                m.SetColor("_Tint", light ? new Color(0.32f, 0.34f, 0.40f, 0.22f) : new Color(0.24f, 0.26f, 0.32f, 0.30f));
                m.SetFloat("_Dark", light ? 0.06f : 0.12f);
                m.SetColor("_BorderColor", new Color(1f, 1f, 1f, 0.22f));
                m.SetFloat("_Blur", light ? 6f : 12f);
                if (m.HasProperty("_AccentColor")) m.SetColor("_AccentColor", Color.clear);
                if (m.HasProperty("_Grid")) m.SetFloat("_Grid", 0f);
                Liquid(light ? 16f : 14f, 0.4f, 0.9f);
            }
            else
            {
                m.SetColor("_Tint", light ? new Color(0.34f, 0.54f, 0.80f, 0.16f) : new Color(0.14f, 0.22f, 0.36f, 0.30f));
                m.SetFloat("_Dark", light ? 0.06f : 0.14f);
                m.SetColor("_BorderColor", light ? new Color(0.62f, 0.86f, 1f, 0.5f) : new Color(0.45f, 0.78f, 1f, 0.42f));
                m.SetFloat("_Blur", light ? 6f : 12f);
                if (m.HasProperty("_AccentColor")) m.SetColor("_AccentColor", new Color(Accent.r, Accent.g, Accent.b, 0.9f));
                if (m.HasProperty("_Grid")) m.SetFloat("_Grid", light ? 0f : 0.8f);
                Liquid(light ? 16f : 14f, 0.4f, 0.9f);
            }
            if (!LiquidGlass)
            {
                // Cổ điển: kính mờ phẳng như bản trước — không khúc xạ, không tán sắc, phủ đặc
                var t0 = m.GetColor("_Tint");
                m.SetColor("_Tint", new Color(t0.r, t0.g, t0.b, Mathf.Min(t0.a + 0.26f, 0.9f)));
                if (m.HasProperty("_Refraction")) m.SetFloat("_Refraction", 0f);
                if (m.HasProperty("_Chroma")) m.SetFloat("_Chroma", 0f);
                if (m.HasProperty("_Specular")) m.SetFloat("_Specular", 0.12f);
                if (m.HasProperty("_Vibrancy")) m.SetFloat("_Vibrancy", 1f);
                if (m.HasProperty("_Reflect")) m.SetFloat("_Reflect", 0f);
                if (m.HasProperty("_EdgeClear")) m.SetFloat("_EdgeClear", 0f);
                if (m.HasProperty("_Adaptive")) m.SetFloat("_Adaptive", 0.5f);
                m.SetFloat("_Blur", light ? 8f : 16f);
            }
            if (IncreasedContrast)
            {
                var t = m.GetColor("_Tint");
                m.SetColor("_Tint", new Color(t.r, t.g, t.b, Mathf.Min(t.a + 0.26f, 0.95f)));
                var bc = m.GetColor("_BorderColor");
                m.SetColor("_BorderColor", UiTheme.IsLight ? new Color(0f, 0f, 0f, 0.55f) : new Color(bc.r, bc.g, bc.b, 0.95f));
                if (m.HasProperty("_Vibrancy")) m.SetFloat("_Vibrancy", 1.1f);
                if (m.HasProperty("_Reflect")) m.SetFloat("_Reflect", 0.12f);
                if (m.HasProperty("_EdgeClear")) m.SetFloat("_EdgeClear", 0.15f);
                if (m.HasProperty("_Chroma")) m.SetFloat("_Chroma", 0.4f);
            }
        }

        /// <summary>
        /// Sprite 9-slice cho kính lỏng: góc bo liên tục (siêu elip bậc 4, kiểu "squircle" của Apple).
        /// A = hình (khử răng cưa), R = độ sâu tính từ mép (0 ở mép → 1 khi sâu hơn depthPx),
        /// G, B = pháp tuyến mép hướng ra ngoài (0,5 + 0,5·n). Độ sâu và pháp tuyến đúng trong dải viền 9-slice.
        /// </summary>
        static Sprite MakeLiquidShape(int size, float radius, float depthPx, float slice)
        {
            // linear = true: kênh màu là DỮ LIỆU (độ sâu, pháp tuyến), không được đổi gamma sRGB khi shader đọc
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            var px = new Color[size * size];
            float half = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float sx = x + 0.5f - half, sy = y + 0.5f - half;
                    float outside = SquircleSdf(Mathf.Abs(sx), Mathf.Abs(sy), half, radius, out Vector2 g);
                    g.x *= Mathf.Sign(sx);
                    g.y *= Mathf.Sign(sy);
                    float a = Mathf.Clamp01(0.5f - outside);
                    float depth = Mathf.Clamp01(-outside / depthPx);
                    px[y * size + x] = new Color(depth, g.x * 0.5f + 0.5f, g.y * 0.5f + 0.5f, a);
                }
            tex.SetPixels(px);
            tex.Apply();
            float b = Mathf.Min(slice, half - 1);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        /// <summary>Khoảng cách có dấu tới hình chữ nhật góc siêu elip (bậc 4) và pháp tuyến (góc phần tư dương).</summary>
        static float SquircleSdf(float ax, float ay, float half, float radius, out Vector2 normal)
        {
            float qx = ax - (half - radius), qy = ay - (half - radius);
            if (qx > 0f && qy > 0f)
            {
                float x4 = qx * qx * qx * qx, y4 = qy * qy * qy * qy;
                float len4 = Mathf.Pow(x4 + y4, 0.25f);
                normal = new Vector2(qx * qx * qx, qy * qy * qy).normalized;
                return len4 - radius;
            }
            normal = qx > qy ? new Vector2(1f, 0f) : new Vector2(0f, 1f);
            return Mathf.Max(qx, qy) - radius;
        }

        static Sprite MakeRounded(int size, float radius, float border, bool white = false) =>
            MakeShape(size, radius, border, white, false);

        /// <summary>
        /// Sprite 9-slice bo góc liên tục (siêu elip bậc 4): white = nền đặc trắng, borderOnly = chỉ đường viền trắng.
        /// </summary>
        static Sprite MakeShape(int size, float corner, float borderWidth, bool white, bool borderOnly)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var px = new Color[size * size];
            float half = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float ax = Mathf.Abs(x + 0.5f - half), ay = Mathf.Abs(y + 0.5f - half);
                    float outside = SquircleSdf(ax, ay, half, corner, out _);
                    float a = Mathf.Clamp01(0.5f - outside);
                    float edge = Mathf.Clamp01(1f - Mathf.Abs(outside + borderWidth * 0.5f) / (borderWidth * 0.5f + 0.5f));
                    px[y * size + x] = borderOnly ? new Color(1f, 1f, 1f, edge * a)
                                     : white ? new Color(1f, 1f, 1f, a)
                                     : new Color(edge, 0f, 0f, a);
                }
            tex.SetPixels(px);
            tex.Apply();
            float b = Mathf.Min(corner + 2f, half - 1);
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
            if (mat != null && !ReducedTransparency && LiquidGlass) KinhLongNgungTu.Attach(img);

            // Nếu người dùng bật ReducedTransparency thì dùng nền đặc mờ nhẹ để chống lóa
            if (ReducedTransparency)
            {
                img.material = null;
                img.sprite = Solid;
                img.pixelsPerUnitMultiplier = cornerScale;  // Solid (góc 30) khớp góc kính (Rounded, góc 30)
                img.color = Surface;
            }

            return img;
        }

        static int ScaleFontSize(int size) =>
            (UiTheme.FontScaleMode > 0 && size >= 11 && size <= 28)
                ? Mathf.Clamp(Mathf.RoundToInt(size * UiTheme.FontScale), size, size + 5)
                : size;

        public static Text Label(Transform parent, string name, string text, int size, Color color, bool bold,
                                 TextAnchor align, Vector2 pos, Vector2 box, Vector2? anchor = null)
        {
            var a = anchor ?? new Vector2(0, 1);
            var rt = Rect(parent, name, a, new Vector2(0, 1), pos, box);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = bold ? Semibold : Regular;
            t.fontSize = ScaleFontSize(size);
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

        static Image AttachBorder(Transform parent, Color color, float pixelsPerUnitMultiplier, bool enabled = true)
        {
            var go = new GameObject("Viền", typeof(RectTransform));
            var brt = (RectTransform)go.transform;
            brt.SetParent(parent, false);
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = brt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.sprite = Border;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier;
            img.color = color;
            img.raycastTarget = false;
            img.enabled = enabled;
            return img;
        }

        /// <summary>Nút bấm bo góc (nền màu, chữ giữa). primary: nền màu nhấn. Tự động gắn NutHieuUng.</summary>
        public static Button MakeButton(Transform parent, string name, string text, Vector2 pos, Vector2 size, System.Action onClick,
                                        bool primary = false, int fontSize = 16, Vector2? anchor = null)
        {
            if (size.y <= 0f) size.y = 44f;
            else if (size.y > 40f && size.y < 44f && size.x > 48f) size.y = 44f;

            var a = anchor ?? new Vector2(0, 1);
            var rt = Rect(parent, name, a, new Vector2(0, 1), pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Solid;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = UiTheme.IsVera ? 2.2f : 3f;
            img.color = primary ? (UiTheme.IsVera ? AccentFill : Accent) : ButtonFill;
            AttachBorder(rt, Line, img.pixelsPerUnitMultiplier, !primary);

            var b = rt.gameObject.AddComponent<Button>();

            // Tắt navigation để tránh dính focus trên cảm ứng iPad
            b.navigation = new Navigation { mode = Navigation.Mode.None };

            var colors = b.colors;
            colors.normalColor = UiTheme.IsLight ? Color.white : new Color(0.92f, 0.92f, 0.92f, 1f);
            colors.highlightedColor = UiTheme.IsLight ? new Color(0.93f, 0.94f, 0.96f, 1f) : Color.white;
            colors.selectedColor = colors.normalColor;
            colors.pressedColor = UiTheme.IsLight ? new Color(0.83f, 0.86f, 0.90f, 1f) : new Color(0.70f, 0.70f, 0.70f, 1f);
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

        /// <summary>Ô nhập chữ trên nền theo chủ đề.</summary>
        public static InputField MakeInput(Transform parent, string name, string placeholder, Vector2 pos, Vector2 size, int fontSize = 17)
        {
            var rt = Rect(parent, name, new Vector2(0, 1), new Vector2(0, 1), pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Solid;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = UiTheme.IsVera ? 2.2f : 3f;
            img.color = Well;
            AttachBorder(rt, Line, img.pixelsPerUnitMultiplier, true);
            var text = Label(rt, "Chữ", "", fontSize, Ink, false, TextAnchor.MiddleLeft, new Vector2(14, 0), new Vector2(size.x - 28, size.y));
            text.supportRichText = false;
            var ph = Label(rt, "Gợi ý", placeholder, fontSize, Muted, false, TextAnchor.MiddleLeft, new Vector2(14, 0), new Vector2(size.x - 28, size.y));
            var f = rt.gameObject.AddComponent<InputField>();
            f.textComponent = text;
            f.placeholder = ph;
            f.targetGraphic = img;
            f.caretColor = Accent;
            f.selectionColor = new Color(Accent.r, Accent.g, Accent.b, 0.30f);
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
            if (plate) { img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = UiTheme.IsVera ? 2.4f : 3f; }
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
        public static Text Readout(Transform parent, string name, string label, string value, Vector2 pos, Vector2 size) =>
            Readout(parent, name, label, value, pos, size, Accent2);

        /// <summary>Ô số đo với màu giá trị tùy chỉnh. Trả về chữ giá trị.</summary>
        public static Text Readout(Transform parent, string name, string label, string value, Vector2 pos, Vector2 size, Color valueColor)
        {
            var bg = Fill(parent, name, pos, size, Well, null, true);
            Label(bg.transform, "Nhãn", label, 12, Dim, true, TextAnchor.UpperLeft, new Vector2(12, -7), new Vector2(size.x - 20, 16));
            var v = Label(bg.transform, "Giá trị", value, 19, valueColor, true, TextAnchor.LowerLeft, new Vector2(12, -size.y + 30), new Vector2(size.x - 20, 24));
            v.horizontalOverflow = HorizontalWrapMode.Overflow;
            return v;
        }

        /// <summary>Thanh trượt kèm tiêu đề và nhãn giá trị màu hổ phách (Accent2) đặt bên phải tiêu đề.</summary>
        public static Slider MakeSlider(Transform parent, string name, string title, Vector2 pos, Vector2 size, float min, float max, float value,
                                        Action<float> onChange, Func<float, string> formatValue = null, bool wholeNumbers = false, Vector2? anchor = null)
        {
            var a = anchor ?? new Vector2(0, 1);
            var h = Mathf.Max(size.y, 62f);
            var root = Rect(parent, name, a, new Vector2(0, 1), pos, new Vector2(size.x, h));
            Label(root, "Tiêu đề", title, 14, Ink, true, TextAnchor.MiddleLeft, Vector2.zero, new Vector2(size.x, 22f));
            Func<float, string> fmt = formatValue ?? (v => wholeNumbers ? Mathf.RoundToInt(v).ToString() : v.ToString("0.##"));
            float v0 = Mathf.Clamp(value, min, max);
            var valLabel = Label(root, "Giá trị", fmt(v0), 14, Accent2, true, TextAnchor.MiddleRight, Vector2.zero, new Vector2(size.x, 22f));
            return MakeSlider(root, "Thanh trượt", new Vector2(0f, -24f), new Vector2(size.x, Mathf.Max(34f, h - 24f)),
                min, max, v0, v =>
                {
                    valLabel.text = fmt(v);
                    onChange?.Invoke(v);
                }, wholeNumbers);
        }

        /// <summary>Thanh trượt: rãnh 6 px rõ nét ở mọi nền, phần đã kéo màu nhấn, núm tròn viền màu nhấn lõi Surface.</summary>
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

            Color trackColor = UiTheme.Pick(
                new Color(0f, 0f, 0f, 0.20f),
                new Color(1f, 1f, 1f, 0.24f),
                new Color(0.45f, 0.78f, 1f, 0.28f));
            var track = Bar(rt, "Rãnh", trackColor, 6f);
            track.offsetMin = new Vector2(13, track.offsetMin.y);
            track.offsetMax = new Vector2(-13, track.offsetMax.y);

            var fillArea = new GameObject("Vùng đã kéo", typeof(RectTransform)).GetComponent<RectTransform>();
            fillArea.SetParent(rt, false);
            fillArea.anchorMin = new Vector2(0, 0.5f);
            fillArea.anchorMax = new Vector2(1, 0.5f);
            fillArea.offsetMin = new Vector2(13, -3f);
            fillArea.offsetMax = new Vector2(-13, 3f);
            var fill = new GameObject("Đã kéo", typeof(RectTransform)).GetComponent<RectTransform>();
            fill.SetParent(fillArea, false);
            var fi = fill.gameObject.AddComponent<Image>();
            fi.sprite = Plain;
            fi.color = Accent;
            fi.raycastTarget = false;
            fill.sizeDelta = Vector2.zero;

            // Vùng núm có chiều cao = 0 đặt giữa theo chiều dọc để Slider.UpdateVisuals() không kéo dãn núm thành hình bầu dục
            var handleArea = new GameObject("Vùng núm", typeof(RectTransform)).GetComponent<RectTransform>();
            handleArea.SetParent(rt, false);
            handleArea.anchorMin = new Vector2(0f, 0.5f);
            handleArea.anchorMax = new Vector2(1f, 0.5f);
            handleArea.sizeDelta = new Vector2(-26f, 0f);
            handleArea.anchoredPosition = Vector2.zero;
            var handle = new GameObject("Núm", typeof(RectTransform)).GetComponent<RectTransform>();
            handle.SetParent(handleArea, false);
            var hi = handle.gameObject.AddComponent<Image>();
            hi.sprite = SolidCircle;
            hi.preserveAspect = true;
            hi.color = Accent;
            handle.anchorMin = new Vector2(0, 0.5f);
            handle.anchorMax = new Vector2(0, 0.5f);
            handle.anchoredPosition = Vector2.zero;
            handle.sizeDelta = new Vector2(26f, 26f);

            var inner = Rect(handle, "Lõi", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18f, 18f)).gameObject.AddComponent<Image>();
            inner.sprite = SolidCircle;
            inner.preserveAspect = true;
            inner.color = SurfaceA(1f);
            inner.raycastTarget = false;

            var sl = rt.gameObject.AddComponent<Slider>();
            sl.navigation = new Navigation { mode = Navigation.Mode.None };
            var sc = sl.colors;
            sc.normalColor = Color.white;
            sc.highlightedColor = new Color(0.95f, 0.95f, 0.95f, 1f);
            sc.selectedColor = Color.white;
            sc.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            sl.colors = sc;
            sl.fillRect = fill;
            sl.handleRect = handle;
            sl.targetGraphic = hi;
            sl.direction = Slider.Direction.LeftToRight;
            sl.minValue = min;
            sl.maxValue = max;
            sl.wholeNumbers = wholeNumbers;
            sl.SetValueWithoutNotify(Mathf.Clamp(value, min, max));
            if (onChange != null) sl.onValueChanged.AddListener(v => onChange(v));
            if (LiquidGlass) KinhLongNumTruot.Attach(sl, handle, hi, inner);
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
            if (img != null) img.color = on ? AccentSoft : ButtonFill;
            var borderImg = b.transform.Find("Viền")?.GetComponent<Image>();
            if (borderImg != null)
            {
                borderImg.enabled = true;
                borderImg.color = on
                    ? new Color(Accent.r, Accent.g, Accent.b, UiTheme.IsLight ? 0.50f : 0.65f)
                    : Line;
            }
            var ch = b.transform.Find("Chữ");
            var t = ch != null ? ch.GetComponent<Text>() : null;
            if (t == null) t = b.GetComponentInChildren<Text>();
            if (t != null)
            {
                t.color = on ? Accent : Ink;
                t.font = on ? Semibold : Regular;
            }
            // nền "đã chọn" dùng chung một giọt kính trượt giữa các chip cùng nhóm (KinhLongGiotChon)
            if (!ReducedTransparency && LiquidGlass) KinhLongGiotChon.Bao(b, on);
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
            img.color = Scrim(alpha);
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

        /// <summary>Khung bảng nổi ở giữa màn hình: kính mờ nếu canvas hỗ trợ, không thì tấm đặc cùng màu.</summary>
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
                img.pixelsPerUnitMultiplier = 1.15f;
                img.color = Surface;
            }
            img.raycastTarget = true;  // chạm vào thân bảng không lọt xuống lớp nền mờ (không tự đóng)
            return img;
        }

        /// <summary>Dải thông báo/cảnh báo một dòng có nền màu dịu, dùng trong layout dọc.</summary>
        public static Image Banner(Transform parent, string text, Color fg, Color bg)
        {
            var rt = new GameObject("Dải thông báo", typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, 40f);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 40f;
            le.preferredHeight = 40f;
            le.flexibleWidth = 1f;
            return BuildBanner(rt, text, fg, bg);
        }

        /// <summary>Dải thông báo/cảnh báo một dòng đặt theo tọa độ cố định.</summary>
        public static Image Banner(Transform parent, string name, string text, Color fg, Color bg, Vector2 pos, Vector2 size, Vector2? anchor = null)
        {
            var rt = Rect(parent, name, anchor ?? new Vector2(0, 1), new Vector2(0, 1), pos, size);
            return BuildBanner(rt, text, fg, bg);
        }

        static Image BuildBanner(RectTransform rt, string text, Color fg, Color bg)
        {
            Color fillColor = bg;
            if (bg.a < 0.9f)
            {
                Color baseSurface = SurfaceA(0.96f);
                float w = Mathf.Max(bg.a, 0.22f);
                fillColor = new Color(
                    Mathf.Lerp(baseSurface.r, bg.r, w),
                    Mathf.Lerp(baseSurface.g, bg.g, w),
                    Mathf.Lerp(baseSurface.b, bg.b, w),
                    0.96f);
            }
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Solid;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = UiTheme.IsVera ? 2.2f : 3f;
            img.color = fillColor;
            img.raycastTarget = false;
            AttachBorder(rt, new Color(fg.r, fg.g, fg.b, UiTheme.IsLight ? 0.30f : 0.44f), img.pixelsPerUnitMultiplier, true);

            var labelRt = new GameObject("Nội dung", typeof(RectTransform)).GetComponent<RectTransform>();
            labelRt.SetParent(rt, false);
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(14f, 0f);
            labelRt.offsetMax = new Vector2(-14f, 0f);
            var txt = labelRt.gameObject.AddComponent<Text>();
            txt.text = text;
            txt.font = Semibold;
            txt.fontSize = ScaleFontSize(15);
            txt.color = fg;
            txt.alignment = TextAnchor.MiddleLeft;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Truncate;
            txt.raycastTarget = false;
            return img;
        }

        /// <summary>Công tắc bật/tắt dạng viên nang (pill track + núm tròn), vùng chạm >= 44 pt.</summary>
        public static Toggle MakeToggle(Transform parent, string label, bool initial, Action<bool> onChanged)
        {
            var rt = new GameObject("Công tắc", typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, 46f);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 46f;
            le.preferredHeight = 46f;
            le.flexibleWidth = 1f;
            return BuildToggle(rt, label, initial, onChanged);
        }

        /// <summary>Công tắc bật/tắt dạng viên nang đặt theo tọa độ cố định.</summary>
        public static Toggle MakeToggle(Transform parent, string name, string label, Vector2 pos, Vector2 size, bool initial, Action<bool> onChanged, Vector2? anchor = null)
        {
            if (size.y < 44f) size.y = 44f;
            var rt = Rect(parent, name, anchor ?? new Vector2(0, 1), new Vector2(0, 1), pos, size);
            return BuildToggle(rt, label, initial, onChanged);
        }

        static Toggle BuildToggle(RectTransform rt, string label, bool initial, Action<bool> onChanged)
        {
            var hit = rt.gameObject.AddComponent<Image>();
            hit.sprite = Plain;
            hit.color = new Color(1f, 1f, 1f, 0.001f);

            var lblRt = new GameObject("Nhãn", typeof(RectTransform)).GetComponent<RectTransform>();
            lblRt.SetParent(rt, false);
            lblRt.anchorMin = Vector2.zero;
            lblRt.anchorMax = Vector2.one;
            lblRt.offsetMin = Vector2.zero;
            lblRt.offsetMax = new Vector2(-66f, 0f);
            var txt = lblRt.gameObject.AddComponent<Text>();
            txt.text = label;
            txt.font = Regular;
            txt.fontSize = ScaleFontSize(15);
            txt.color = Ink;
            txt.alignment = TextAnchor.MiddleLeft;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Truncate;
            txt.raycastTarget = false;

            var trackRt = Rect(rt, "Rãnh công tắc", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(54f, 30f));
            var trackImg = trackRt.gameObject.AddComponent<Image>();
            trackImg.sprite = Solid;
            trackImg.type = Image.Type.Sliced;
            trackImg.pixelsPerUnitMultiplier = 1f;
            trackImg.raycastTarget = false;
            var borderImg = AttachBorder(trackRt, Line, 1f, !initial);

            var knobRt = Rect(trackRt, "Núm", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22f, 22f));
            var knobImg = knobRt.gameObject.AddComponent<Image>();
            knobImg.sprite = SolidCircle;
            knobImg.raycastTarget = false;

            var tog = rt.gameObject.AddComponent<Toggle>();
            tog.navigation = new Navigation { mode = Navigation.Mode.None };
            tog.transition = Selectable.Transition.None;
            tog.targetGraphic = trackImg;
            tog.graphic = null;

            void Apply(bool on)
            {
                trackImg.color = on
                    ? (UiTheme.IsVera ? AccentFill : Accent)
                    : UiTheme.Pick(new Color(0f, 0f, 0f, 0.14f), new Color(1f, 1f, 1f, 0.20f), Well);
                borderImg.enabled = !on;
                borderImg.color = Line;
                knobRt.anchoredPosition = new Vector2(on ? 12f : -12f, 0f);
                knobImg.color = on ? (UiTheme.IsVera ? OnAccent : Color.white) : (UiTheme.IsLight ? Color.white : Ink);
            }

            tog.SetIsOnWithoutNotify(initial);
            Apply(initial);
            if (LiquidGlass) KinhLongCongTac.Attach(tog, knobRt, knobImg, 12f);
            tog.onValueChanged.AddListener(v =>
            {
                Apply(v);
                onChanged?.Invoke(v);
            });
            return tog;
        }

        /// <summary>Chiều rộng dòng chữ (để co giãn chip, thanh gợi ý).</summary>
        public static float Width(Text t, string s)
        {
            var settings = t.GetGenerationSettings(new Vector2(4000, 200));
            return t.cachedTextGeneratorForLayout.GetPreferredWidth(s, settings) / t.pixelsPerUnit;
        }
    }
}