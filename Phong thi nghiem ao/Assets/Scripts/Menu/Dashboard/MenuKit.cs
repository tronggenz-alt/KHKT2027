using System;
using System.Collections.Generic;
using Khkt.Demo;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Khkt.Menu.Dashboard
{
    /// <summary>Phản hồi khi rê chuột và khi bấm: phóng nhẹ, thu nhẹ, đậm bóng của thẻ.</summary>
    public class MenuHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public float hoverScale = 1f;
        public float pressScale = 1f;
        public Image shadow;
        public float shadowRest, shadowHover;
        public Image tint;
        public Color tintRest, tintHover;

        bool over;

        public void OnPointerEnter(PointerEventData e)
        {
            over = true;
            Apply(hoverScale);
        }

        public void OnPointerExit(PointerEventData e)
        {
            over = false;
            Apply(1f);
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (pressScale != 1f) MenuAnim.ScaleTo(transform, pressScale, 0.08f);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (pressScale != 1f) MenuAnim.ScaleTo(transform, over ? hoverScale : 1f, 0.22f, MenuAnim.Ease.OutBack);
        }

        void Apply(float scale)
        {
            if (hoverScale != 1f) MenuAnim.ScaleTo(transform, scale, 0.18f);
            if (tint != null) MenuAnim.ColorTo(tint, over ? tintHover : tintRest, 0.14f);
            if (shadow != null)
            {
                var c = shadow.color;
                c.a = over ? shadowHover : shadowRest;
                MenuAnim.ColorTo(shadow, c, 0.18f);
            }
        }

        void OnDisable()
        {
            // nút bị ẩn giữa lúc rê chuột thì lần hiện sau phải về đúng cỡ
            over = false;
            MenuAnim.Stop(transform, "cỡ");
            transform.localScale = Vector3.one;
        }
    }

    /// <summary>Màu sắc và mảnh giao diện chung của màn hình chính, theo trang giới thiệu (trang_gioi_thieu/kieu.css).</summary>
    public static class MenuKit
    {
        public static readonly Color Paper = Hex(0xffffff);
        public static readonly Color PaperAlt = Hex(0xf5f5f7);
        public static readonly Color PaperHover = Hex(0xececf0);
        public static readonly Color Ink = Hex(0x1d1d1f);
        public static readonly Color InkDim = Hex(0x5c5d66);
        public static readonly Color Line = Hex(0xd9d9de);
        public static readonly Color Accent = Hex(0x0b63d1);
        public static readonly Color AccentDeep = Hex(0x084fa8);
        public static readonly Color AccentSoft = Hex(0xe8f1fd);
        public static readonly Color Bio = Hex(0x0b7a5b);
        public static readonly Color BioSoft = Hex(0xe3f4ee);
        public static readonly Color Warn = Hex(0x9a5b00);
        public static readonly Color WarnSoft = Hex(0xfdf1dc);
        public static readonly Color Error = Hex(0xb3261e);

        // Hệ số thu bán kính của GlassKit.Solid (bán kính gốc 30 px)
        public const float RadiusButton = 3f;  // 10 px
        public const float RadiusCard = 1.875f;  // 16 px
        public const float RadiusPill = 1f;  // 30 px, đủ tròn với nút cao tới 60 px

        // Bảng mã phông ký hiệu Segoe MDL2 Assets có sẵn trên Windows 10 trở lên
        public const string IconHome = "\uE80F";
        public const string IconScore = "\uE9D9";
        public const string IconTask = "\uE9D5";
        public const string IconRobot = "\uE99A";
        public const string IconBook = "\uE8F1";
        public const string IconGrowth = "\uE8EF";
        public const string IconSettings = "\uE713";
        public const string IconSignOut = "\uF3B1";
        public const string IconDone = "\uE73E";
        public const string IconBeaker = "\uE950";
        public const string IconPeople = "\uE716";
        public const string IconRetry = "\uE72C";
        public const string IconQuit = "\uE8BB";

        static Font iconFont;
        static bool iconFontTried;
        static Sprite softShadow;

        public static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);

        public static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }

        /// <summary>Phông ký hiệu của Windows; không có (macOS) thì trả null và bỏ qua biểu tượng.</summary>
        public static Font IconFont
        {
            get
            {
                if (iconFontTried) return iconFont;
                iconFontTried = true;
                if (Array.IndexOf(Font.GetOSInstalledFontNames(), "Segoe MDL2 Assets") >= 0)
                    iconFont = Font.CreateDynamicFontFromOSFont("Segoe MDL2 Assets", 32);
                return iconFont;
            }
        }

        public static RectTransform Stretch(Transform parent, string name, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.pivot = new Vector2(0, 0);
            rt.offsetMin = offMin;
            rt.offsetMax = offMax;
            return rt;
        }

        /// <summary>Phủ kín đối tượng cha.</summary>
        public static RectTransform Full(Transform parent, string name) => Stretch(parent, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        public static Image Fill(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color color, float radius)
        {
            var rt = GlassKit.Rect(parent, name, anchor, pivot, pos, size);
            return Paint(rt, color, radius);
        }

        public static Image Paint(RectTransform rt, Color color, float radius)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = GlassKit.Solid;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = radius;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Biểu tượng trong ô tròn màu nhạt. Máy không có phông ký hiệu thì chỉ còn ô tròn.</summary>
        public static Image IconBadge(Transform parent, string name, string glyph, Vector2 pos, float size, Color soft, Color ink)
        {
            var bg = Fill(parent, name, new Vector2(0, 1), new Vector2(0, 1), pos, new Vector2(size, size), soft, 1f);
            bg.sprite = GlassKit.SolidCircle;
            bg.type = Image.Type.Simple;
            Icon(bg.transform, glyph, Mathf.RoundToInt(size * 0.46f), ink, Vector2.zero, new Vector2(size, size));
            return bg;
        }

        public static Text Icon(Transform parent, string glyph, int size, Color color, Vector2 pos, Vector2 box)
        {
            var t = GlassKit.Label(parent, "Biểu tượng", IconFont != null ? glyph : "", size, color, false, TextAnchor.MiddleCenter, pos, box);
            if (IconFont != null) t.font = IconFont;
            t.supportRichText = false;
            return t;
        }

        /// <summary>Bóng mờ dưới thẻ, vẽ một ảnh trải 9 ô. Trải từ tâm ra nên phải nằm sau thẻ trong cây đối tượng.</summary>
        public static Sprite SoftShadow
        {
            get
            {
                if (softShadow != null) return softShadow;
                const int size = 96;
                const float blur = 26f, radius = 16f;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
                var px = new Color[size * size];
                float half = size / 2f, inner = half - blur - radius;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float qx = Mathf.Abs(x + 0.5f - half) - inner;
                        float qy = Mathf.Abs(y + 0.5f - half) - inner;
                        float d = new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0) - radius;
                        float a = 1f - Mathf.Clamp01((d + 1f) / blur);
                        px[y * size + x] = new Color(1, 1, 1, a * a * (3f - 2f * a));
                    }
                tex.SetPixels(px);
                tex.Apply();
                float b = half - 2f;
                softShadow = Sprite.Create(tex, new UnityEngine.Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
                return softShadow;
            }
        }

        /// <summary>
        /// Thẻ trắng có bóng mờ, co giãn theo cha: chiều ngang là phần (x0, x1) của cha, cách đỉnh y và cao h
        /// (h &lt;= 0 thì kéo tới cách đáy -h). Trả về thân thẻ để đặt nội dung.
        /// </summary>
        public static RectTransform Card(Transform parent, string name, float x0, float x1, float y, float h, float gap, bool hover = true)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.pivot = new Vector2(0.5f, 1f);
            if (h > 0)
            {
                rt.anchorMin = new Vector2(x0, 1);
                rt.anchorMax = new Vector2(x1, 1);
                rt.anchoredPosition = new Vector2(0, y);
                rt.sizeDelta = new Vector2(-gap, h);
            }
            else
            {
                rt.anchorMin = new Vector2(x0, 0);
                rt.anchorMax = new Vector2(x1, 1);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.offsetMin = new Vector2(gap * 0.5f, -h);
                rt.offsetMax = new Vector2(-gap * 0.5f, y);
            }

            var shadow = Stretch(rt, "Bóng", Vector2.zero, Vector2.one, new Vector2(-26, -32), new Vector2(26, 20));
            var si = shadow.gameObject.AddComponent<Image>();
            si.sprite = SoftShadow;
            si.type = Image.Type.Sliced;
            si.color = new Color(0.05f, 0.1f, 0.2f, 0.06f);
            si.raycastTarget = false;

            var body = Stretch(rt, "Thân", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var bi = Paint(body, Paper, RadiusCard);
            bi.raycastTarget = hover;
            if (hover)
            {
                var h2 = rt.gameObject.AddComponent<MenuHover>();
                h2.hoverScale = 1.008f;
                h2.shadow = si;
                h2.shadowRest = 0.06f;
                h2.shadowHover = 0.13f;
            }
            return rt;
        }

        /// <summary>Nơi đặt nội dung bên trong thẻ do Card tạo.</summary>
        public static RectTransform Body(RectTransform card) => (RectTransform)card.Find("Thân");

        /// <summary>style: 0 nền màu nhấn, 1 nền sáng có viền, 2 chữ không nền, 3 nền trắng chữ màu nhấn (đặt trên thẻ màu).</summary>
        public static Button MakeButton(Transform parent, string name, string text, Vector2 pos, Vector2 size, Action onClick, int style, float radius = RadiusButton, Vector2? anchor = null)
        {
            var a = anchor ?? new Vector2(0, 1);
            var pivot = anchor.HasValue ? anchor.Value : new Vector2(0, 1);
            var outer = Fill(parent, name, a, pivot, pos, size, style == 0 ? Accent : style == 1 ? Line : style == 3 ? Paper : new Color(1, 1, 1, 0), radius);
            Image target = outer;
            if (style == 1)
            {
                target = Fill(outer.transform, "Nền", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(3, 3), Paper, radius);
            }
            target.raycastTarget = true;
            var b = outer.gameObject.AddComponent<Button>();
            var colors = b.colors;
            colors.normalColor = Color.white;
            colors.selectedColor = Color.white;
            colors.highlightedColor = style == 0 ? new Color(0.86f, 0.86f, 0.86f) : PaperAlt;
            colors.pressedColor = style == 0 ? new Color(0.76f, 0.76f, 0.76f) : PaperHover;
            colors.fadeDuration = 0.12f;
            b.colors = colors;
            b.targetGraphic = target;
            if (style == 2)
            {
                // Không có nền để tô: nền trong suốt, vẫn nhận chuột
                outer.color = new Color(1, 1, 1, 0.001f);
            }
            if (onClick != null) b.onClick.AddListener(() => onClick());
            var label = GlassKit.Label(outer.transform, "Chữ", text, style == 2 ? 16 : 18, style == 0 ? Color.white : style == 1 ? Ink : style == 3 ? Accent : InkDim, style != 2, TextAnchor.MiddleCenter, Vector2.zero, size);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            var hover = outer.gameObject.AddComponent<MenuHover>();
            hover.hoverScale = style == 2 ? 1f : 1.012f;
            hover.pressScale = 0.975f;
            if (style == 2)
            {
                hover.tint = outer;
                hover.tintRest = outer.color;
                hover.tintHover = new Color(0.05f, 0.1f, 0.2f, 0.045f);
            }
            return b;
        }

        /// <summary>
        /// Tạo sẵn các chữ tiếng Việt trong bảng phông động. Không làm thì lần đầu hiện chữ mới, Unity phải dựng lại
        /// bảng phông giữa lúc đang chuyển động, gây khựng.
        /// </summary>
        public static void PrewarmFonts(float canvasScale)
        {
            const string chars =
                "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,:;!?%/+-–·()…\"'" +
                "ÀÁẢÃẠĂẰẮẲẴẶÂẦẤẨẪẬĐÈÉẺẼẸÊỀẾỂỄỆÌÍỈĨỊÒÓỎÕỌÔỒỐỔỖỘƠỜỚỞỠỢÙÚỦŨỤƯỪỨỬỮỰỲÝỶỸỴ" +
                "àáảãạăằắẳẵặâầấẩẫậđèéẻẽẹêềếểễệìíỉĩịòóỏõọôồốổỗộơờớởỡợùúủũụưừứửữựỳýỷỹỵ";
            if (Mathf.Approximately(prewarmedScale, canvasScale)) return;
            prewarmedScale = canvasScale;
            foreach (var s in new[] { 14, 15, 16, 17, 18, 20 }) GlassKit.Regular.RequestCharactersInTexture(chars, Px(s, canvasScale));
            foreach (var s in new[] { 16, 17, 18, 20, 24, 26, 36, 40 }) GlassKit.Semibold.RequestCharactersInTexture(chars, Px(s, canvasScale));
            GlassKit.Semibold.RequestCharactersInTexture("0123456789.,+−–", Px(44, canvasScale));
        }

        static float prewarmedScale = -1f;

        static int Px(int size, float scale) => Mathf.Max(1, Mathf.RoundToInt(size * scale));

        /// <summary>Tên gọi thân mật: chữ cuối của họ tên Việt Nam.</summary>
        public static string GivenName(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return "";
            var parts = fullName.Trim().Split(' ');
            return parts[parts.Length - 1];
        }

        public static string Initial(string fullName)
        {
            string g = GivenName(fullName);
            return g.Length > 0 ? g.Substring(0, 1).ToUpperInvariant() : "?";
        }

        public static string ClassLine(AccountUser u)
        {
            string role = u.vai_tro == "giao_vien" ? "Giáo viên" : "Học sinh";
            if (u.lop == null || u.lop.Length == 0) return role;
            var names = new List<string>();
            foreach (var c in u.lop) names.Add(c.ten);
            return $"{role} · Lớp {string.Join(", ", names)}";
        }
    }
}
