using System;
using System.Collections.Generic;
using Khkt.Demo;
using Khkt.Environment.Tang2;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Khkt.Menu.Dashboard
{
    /// <summary>
    /// Phản hồi khi rê chuột và khi bấm, chạy bằng hai lò xo (rê, nhấn) theo thời gian thật, chia bước con cố định
    /// nên không phụ thuộc FPS và không cấp phát mỗi khung. Thẻ nhấc lên (lift), bóng đậm và rơi xa hơn, viền đổi màu,
    /// lớp sáng hiện dần, mũi tên nhích sang phải. Bật "Giảm chuyển động" thì nhảy thẳng tới trạng thái cuối.
    /// </summary>
    public class MenuHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public float hoverScale = 1f;
        public float pressScale = 1f;
        public Image shadow;
        public float shadowRest, shadowHover;
        public Image shadowNear;
        public float nearRest, nearHover;
        public Graphic tint;
        public Color tintRest, tintHover;
        public Graphic ring;
        public Color ringRest, ringHover;
        /// <summary>Số điểm nhấc lên khi rê (0 = không nhấc).</summary>
        public float lift;
        /// <summary>Các phần nhấc theo (thân, viền, chữ…); bóng ở yên và rơi xa hơn.</summary>
        public RectTransform[] liftTargets;
        public RectTransform nudge;
        public float nudgeX = 6f;

        // lò xo: rê (h) mềm, nhấn (p) cứng
        const float HoverK = 320f, HoverC = 32f, PressK = 600f, PressC = 34f;
        float h, hv, p, pv, hTarget, pTarget;
        bool active, homesTaken;
        Vector2[] liftHome;
        Vector2 shadowHome, nearHome, nudgeHome;

        public void OnPointerEnter(PointerEventData e)
        {
            hTarget = 1f;
            Wake();
        }

        public void OnPointerExit(PointerEventData e)
        {
            hTarget = 0f;
            pTarget = 0f;
            Wake();
        }

        public void OnPointerDown(PointerEventData e)
        {
            pTarget = 1f;
            Wake();
        }

        public void OnPointerUp(PointerEventData e)
        {
            pTarget = 0f;
            Wake();
        }

        void Wake()
        {
            if (!homesTaken) TakeHomes();
            active = true;
        }

        void TakeHomes()
        {
            homesTaken = true;
            if (liftTargets != null)
            {
                liftHome = new Vector2[liftTargets.Length];
                for (int i = 0; i < liftTargets.Length; i++) if (liftTargets[i] != null) liftHome[i] = liftTargets[i].anchoredPosition;
            }
            if (shadow != null) shadowHome = shadow.rectTransform.anchoredPosition;
            if (shadowNear != null) nearHome = shadowNear.rectTransform.anchoredPosition;
            if (nudge != null) nudgeHome = nudge.anchoredPosition;
        }

        static void Step(ref float x, ref float v, float target, float k, float c, float dt)
        {
            const float Sub = 1f / 240f;
            while (dt > 1e-6f)
            {
                float s = dt < Sub ? dt : Sub;
                v += (-k * (x - target) - c * v) * s;
                x += v * s;
                dt -= s;
            }
        }

        void Update()
        {
            if (!active) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 20f);
            if (ReducedMotion.IsEnabled)
            {
                h = hTarget; p = pTarget; hv = pv = 0f;
            }
            else
            {
                Step(ref h, ref hv, hTarget, HoverK, HoverC, dt);
                Step(ref p, ref pv, pTarget, PressK, PressC, dt);
            }
            bool settled = Mathf.Abs(h - hTarget) < 0.0015f && Mathf.Abs(hv) < 0.01f && Mathf.Abs(p - pTarget) < 0.0015f && Mathf.Abs(pv) < 0.01f;
            if (settled)
            {
                h = hTarget; p = pTarget; hv = pv = 0f;
                active = false;
            }
            Apply();
        }

        void Apply()
        {
            if (hoverScale != 1f || pressScale != 1f)
            {
                float s = 1f + (hoverScale - 1f) * h - (1f - pressScale) * p;
                transform.localScale = new Vector3(s, s, 1f);
            }
            float up = lift * (h - 0.6f * p);
            if (liftTargets != null && liftHome != null)
                for (int i = 0; i < liftTargets.Length; i++)
                    if (liftTargets[i] != null) liftTargets[i].anchoredPosition = liftHome[i] + new Vector2(0f, up);
            float hc = Mathf.Clamp01(h);
            if (shadow != null)
            {
                var c = shadow.color;
                c.a = Mathf.Lerp(shadowRest, shadowHover, hc);
                shadow.color = c;
                if (lift != 0f) shadow.rectTransform.anchoredPosition = shadowHome - new Vector2(0f, up * 0.8f);
            }
            if (shadowNear != null)
            {
                var c = shadowNear.color;
                c.a = Mathf.Lerp(nearRest, nearHover, hc);
                shadowNear.color = c;
            }
            if (ring != null) ring.color = Color.Lerp(ringRest, ringHover, hc);
            if (tint != null) tint.color = Color.Lerp(tintRest, tintHover, Mathf.Clamp01(hc + 0.7f * Mathf.Clamp01(p)));
            if (nudge != null) nudge.anchoredPosition = nudgeHome + new Vector2(nudgeX * h, 0f);
        }

        void OnDisable()
        {
            // bị ẩn giữa lúc rê chuột thì lần hiện sau phải về đúng trạng thái nghỉ
            h = hv = p = pv = hTarget = pTarget = 0f;
            active = false;
            if (homesTaken) Apply();
            else if (hoverScale != 1f || pressScale != 1f) transform.localScale = Vector3.one;
        }
    }

    /// <summary>Viền ô nhập: chuyển sang màu nhấn và hiện quầng mờ khi ô đang được gõ (theo dõi isFocused, không cấp phát).</summary>
    public class MenuFocusRing : MonoBehaviour
    {
        public InputField field;
        public Graphic ring, halo;
        public Color ringRest, ringFocus, haloFocus;
        float k = -1f, v, target;

        void Update()
        {
            if (field == null) return;
            target = field.isFocused ? 1f : 0f;
            if (k >= 0f && Mathf.Abs(k - target) < 0.002f && Mathf.Abs(v) < 0.01f) return;
            if (k < 0f || ReducedMotion.IsEnabled) { k = target; v = 0f; }
            else
            {
                float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 20f);
                const float Sub = 1f / 240f;
                while (dt > 1e-6f)
                {
                    float s = dt < Sub ? dt : Sub;
                    v += (-500f * (k - target) - 40f * v) * s;
                    k += v * s;
                    dt -= s;
                }
            }
            float c = Mathf.Clamp01(k);
            if (ring != null) ring.color = Color.Lerp(ringRest, ringFocus, c);
            if (halo != null)
            {
                var hc = haloFocus;
                hc.a *= c;
                halo.color = hc;
            }
        }
    }

    /// <summary>
    /// Tô dải màu thẳng theo góc kiểu CSS (0° = lên trên, 90° = sang phải, 145° = chéo xuống phải) bằng màu đỉnh lưới.
    /// Ảnh 9 ô vẫn giữ mép khử răng cưa của sprite (khác với Mask stencil). Màu cuối = màu Graphic × dải màu.
    /// </summary>
    public class MenuGradient : BaseMeshEffect
    {
        public Color from = Color.white, to = Color.white;
        public float angle = 180f;

        public void Set(Color a, Color b, float deg)
        {
            from = a; to = b; angle = deg;
            if (graphic != null) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;
            var r = graphic.rectTransform.rect;
            float rad = angle * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
            float half = Mathf.Abs(r.width * 0.5f * dir.x) + Mathf.Abs(r.height * 0.5f * dir.y);
            if (half < 0.001f) return;
            var c = r.center;
            var vert = new UIVertex();
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vert, i);
                float t = Vector2.Dot((Vector2)vert.position - c, dir) / (2f * half) + 0.5f;
                vert.color = (Color32)((Color)vert.color * Color.LerpUnclamped(from, to, Mathf.Clamp01(t)));
                vh.SetUIVertex(vert, i);
            }
        }
    }

    /// <summary>Lát vân chấm theo đúng số điểm của khung (cập nhật khi khung đổi cỡ, không chạy mỗi khung).</summary>
    public class MenuTile : UIBehaviour
    {
        public float cell = 24f;

        protected override void OnEnable()
        {
            base.OnEnable();
            Fit();
        }

        protected override void OnRectTransformDimensionsChange() => Fit();

        void Fit()
        {
            var raw = GetComponent<RawImage>();
            if (raw == null) return;
            var r = ((RectTransform)transform).rect;
            raw.uvRect = new UnityEngine.Rect(0, 0, Mathf.Max(1f, r.width / cell), Mathf.Max(1f, r.height / cell));
        }
    }

    /// <summary>Nhóm nút chọn một (thanh phân đoạn ngang hoặc danh sách dọc) có vệt chọn trượt theo lò xo.</summary>
    public class MenuTabs : MonoBehaviour
    {
        public Button[] buttons;
        public Text[] labels;
        public RectTransform glider;
        public int current = -1;
        public Action<int> onSelect;

        public void Select(int i, bool notify = false)
        {
            if (buttons == null || buttons.Length == 0) return;
            i = Mathf.Clamp(i, 0, buttons.Length - 1);
            bool first = current < 0;
            current = i;
            for (int k = 0; k < labels.Length; k++)
            {
                if (labels[k] == null) continue;
                labels[k].color = k == i ? MenuKit.Ink : MenuKit.InkDim;
                labels[k].font = k == i ? GlassKit.Semibold : GlassKit.Regular;
            }
            var dst = (RectTransform)buttons[i].transform;
            if (glider != null)
            {
                if (first || !isActiveAndEnabled) glider.anchoredPosition = dst.anchoredPosition;
                else MenuAnim.Slide(glider, glider.anchoredPosition, dst.anchoredPosition, 0.34f, 0f, MenuAnim.Ease.OutQuint);
                glider.sizeDelta = dst.sizeDelta;
            }
            if (notify) onSelect?.Invoke(i);
        }
    }

    /// <summary>Màu sắc và mảnh giao diện chung của màn hình chính, đọc động theo UiTheme (Trắng / Đen / Xanh mực).</summary>
    public static class MenuKit
    {
        // ------------------------------------------------------------------ màu (18/11: trung tính kiểu zinc, viền tóc)
        public static Color Paper => UiTheme.Pick(Hex(0xffffff), Hex(0x18181b), Hex(0x0e1b2e));        // thẻ
        public static Color PaperAlt => UiTheme.Pick(Hex(0xf4f4f5), Hex(0x09090b), Hex(0x07111f));     // nền trang
        public static Color PaperHover => UiTheme.Pick(Hex(0xf4f4f5), Hex(0x27272a), Hex(0x16263e));
        /// <summary>Nền ô nhập, rãnh, ô lõm.</summary>
        public static Color Well => UiTheme.Pick(Hex(0xfafafa), Hex(0x0f0f12), Hex(0x0a1526));
        public static Color Ink => UiTheme.Pick(Hex(0x09090b), Hex(0xfafafa), Hex(0xeaf1ff));
        public static Color InkDim => UiTheme.Pick(Hex(0x52525b), Hex(0xa1a1aa), Hex(0x93a7c4));
        public static Color InkMute => UiTheme.Pick(Hex(0x71717a), Hex(0x71717a), Hex(0x6d87a8));
        public static Color Line => GlassKit.Line;
        /// <summary>Viền tóc 1 px của thẻ, nút, ô nhập.</summary>
        public static Color Hair => UiTheme.Pick(new Color(0f, 0f, 0f, 0.09f), new Color(1f, 1f, 1f, 0.09f), new Color(0.55f, 0.78f, 1f, 0.16f));
        public static Color HairStrong => UiTheme.Pick(new Color(0f, 0f, 0f, 0.16f), new Color(1f, 1f, 1f, 0.16f), new Color(0.55f, 0.78f, 1f, 0.28f));
        public static Color Accent => GlassKit.Accent;
        public static Color Blue => Accent;
        public static Color OnAccent => GlassKit.OnAccent;
        public static Color AccentDeep => UiTheme.Pick(Hex(0x094fa8), Hex(0x2d86e6), Hex(0x16a8d4));
        public static Color AccentSoft => GlassKit.AccentSoft;
        public static Color BlueSoft => AccentSoft;
        public static Color Amber => GlassKit.Accent2;
        public static Color Bio => GlassKit.Good;
        public static Color BioSoft => GlassKit.GoodSoft;
        public static Color Warn => GlassKit.Warn;
        public static Color WarnSoft => GlassKit.WarnSoft;
        public static Color Error => GlassKit.Bad;
        public static Color ErrorSoft => GlassKit.BadSoft;
        /// <summary>Màu bóng: nền sáng hơi xanh than, nền tối gần đen.</summary>
        static Color ShadowInk => UiTheme.IsLight ? new Color(0.06f, 0.09f, 0.16f, 1f) : new Color(0f, 0.005f, 0.02f, 1f);

        // ------------------------------------------------------------------ bán kính (điểm giao diện, tham chiếu 1920×1080)
        public const float RadiusButton = 12f;
        public const float RadiusCard = 20f;
        public const float RadiusLarge = 28f;
        /// <summary>Viên nang: góc tự thu bằng nửa chiều cao (giá trị ≤ 1 cũng hiểu là viên nang).</summary>
        public const float RadiusPill = 1f;

        // Bảng mã phông ký hiệu Segoe MDL2 Assets có sẵn trên Windows 10 trở lên
        public const string IconHome = "";
        public const string IconScore = "";
        public const string IconTask = "";
        public const string IconRobot = "";
        public const string IconBook = "";
        public const string IconGrowth = "";
        public const string IconSettings = "";
        public const string IconSignOut = "";
        public const string IconDone = "";
        public const string IconBeaker = "";
        public const string IconPeople = "";
        public const string IconRetry = "";
        public const string IconQuit = "";

        static Font iconFont;
        static bool iconFontTried;

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

        // ------------------------------------------------------------------ hình (vẽ một lần, 2x cho Retina)
        // kind: 0 đặc, 1 viền tóc, 2 phần ngoài góc bo (che góc ảnh)
        static readonly Dictionary<int, Sprite> shapes = new Dictionary<int, Sprite>();
        static readonly Dictionary<int, Sprite> shadows = new Dictionary<int, Sprite>();
        const float ShapeScale = 2f;
        const int PillRadius = 32;

        static int RadiusKey(float radius) => radius <= 1f ? PillRadius : Mathf.Clamp(Mathf.RoundToInt(radius), 2, 48);

        static float SdRoundBox(float px, float py, float hx, float hy, float r)
        {
            float qx = Mathf.Abs(px) - (hx - r), qy = Mathf.Abs(py) - (hy - r);
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        static Sprite Shape(int kind, float radius)
        {
            int r = RadiusKey(radius);
            int key = kind * 1000 + r;
            if (shapes.TryGetValue(key, out var sp) && sp != null) return sp;
            float R = r * ShapeScale;
            int size = Mathf.CeilToInt(R * 2f) + 6;
            float half = size * 0.5f;
            const float Stroke = 1.25f * ShapeScale;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = SdRoundBox(x + 0.5f - half, y + 0.5f - half, half, half, R);
                    float inside = Mathf.Clamp01(0.5f - d);
                    float a = kind == 0 ? inside
                            : kind == 1 ? inside - Mathf.Clamp01(0.5f - (d + Stroke))
                            : 1f - inside;
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            float b = Mathf.Ceil(R) + 2f;
            sp = Sprite.Create(tex, new UnityEngine.Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
            sp.hideFlags = HideFlags.DontSave;
            shapes[key] = sp;
            return sp;
        }

        /// <summary>Hình bo góc đặc (trắng, tô màu bằng Image.color). Đặt pixelsPerUnitMultiplier = 2.</summary>
        public static Sprite Round(float radius) => Shape(0, radius);
        /// <summary>Viền tóc 1,25 điểm khớp bán kính của Round (thay cho chồng hai mảng tô hay Outline).</summary>
        public static Sprite RingSprite(float radius) => Shape(1, radius);
        /// <summary>Phần nằm ngoài góc bo, tô bằng màu thẻ để bo góc ảnh mà vẫn khử răng cưa (Mask stencil thì răng cưa).</summary>
        public static Sprite CornerCap(float radius) => Shape(2, radius);

        /// <summary>Bóng mờ 9 ô: blur là độ nhòe (điểm), radius là bán kính góc của khối đổ bóng.</summary>
        public static Sprite ShadowSprite(float blur, float radius)
        {
            int bl = Mathf.Clamp(Mathf.RoundToInt(blur), 2, 64), rr = RadiusKey(radius);
            int key = bl * 1000 + rr;
            if (shadows.TryGetValue(key, out var sp) && sp != null) return sp;
            int size = 2 * (bl + rr) + 8;
            float half = size * 0.5f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = SdRoundBox(x + 0.5f - half, y + 0.5f - half, half - bl, half - bl, rr);
                    float t = Mathf.Clamp01((d + bl * 0.35f) / (bl * 1.35f));
                    float s = 1f - t * t * (3f - 2f * t);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(s * s * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            float b = bl + rr + 3f;
            sp = Sprite.Create(tex, new UnityEngine.Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
            sp.hideFlags = HideFlags.DontSave;
            shadows[key] = sp;
            return sp;
        }

        /// <summary>Bóng cũ một lớp (giữ tên cho mã cũ).</summary>
        public static Sprite SoftShadow => ShadowSprite(26f, 16f);

        // ------------------------------------------------------------------ dựng khối
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

        /// <summary>Tô khối bo góc (radius: điểm; ≤ 1 là viên nang).</summary>
        public static Image Paint(RectTransform rt, Color color, float radius)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Round(radius);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = ShapeScale;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Viền tóc phủ kín cha (đặt sau nội dung để nằm trên cùng).</summary>
        public static Image Ring(Transform parent, string name, Color color, float radius)
        {
            var rt = Full(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = RingSprite(radius);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = ShapeScale;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Ánh sáng mép trên (nền tối): viền trắng mờ dần từ trên xuống, như mặt kính bắt sáng.</summary>
        public static Image Sheen(Transform parent, float radius, float alpha)
        {
            var img = Ring(parent, "Ánh mép", Color.white, radius);
            img.gameObject.AddComponent<MenuGradient>().Set(new Color(1, 1, 1, alpha), new Color(1, 1, 1, 0f), 180f);
            return img;
        }

        /// <summary>Bóng mờ phủ quanh khối cha, lệch xuống dy. Phải tạo trước thân để nằm dưới.</summary>
        public static Image Shadow(Transform parent, string name, float blur, float radius, float dy, float alpha, Color? ink = null)
        {
            var rt = Stretch(parent, name, Vector2.zero, Vector2.one, new Vector2(-blur, -blur + dy), new Vector2(blur, blur + dy));
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = ShadowSprite(blur, radius);
            img.type = Image.Type.Sliced;
            var c = ink ?? ShadowInk;
            c.a = alpha;
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        public static MenuGradient Gradient(Graphic g, Color from, Color to, float angle = 180f)
        {
            var gr = g.GetComponent<MenuGradient>() ?? g.gameObject.AddComponent<MenuGradient>();
            gr.Set(from, to, angle);
            return gr;
        }

        /// <summary>Biểu tượng trong ô tròn màu nhạt. Máy không có phông ký hiệu thì chỉ còn ô tròn.</summary>
        public static Image IconBadge(Transform parent, string name, string glyph, Vector2 pos, float size, Color soft, Color ink)
        {
            var bg = Fill(parent, name, new Vector2(0, 1), new Vector2(0, 1), pos, new Vector2(size, size), soft, RadiusButton);
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

        /// <summary>Nhãn nhỏ IN HOA đậm phía trên tiêu đề (eyebrow).</summary>
        public static Text Eyebrow(Transform parent, string name, string text, Vector2 pos, float width, Color color, Vector2? anchor = null)
        {
            var t = GlassKit.Label(parent, name, text.ToUpperInvariant(), 13, color, true, TextAnchor.UpperLeft, pos, new Vector2(width, 20), anchor);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }

        /// <summary>Nhãn dạng viên nang có viền tóc (tag). Trả về khối nhãn.</summary>
        public static RectTransform Tag(Transform parent, string name, string text, Vector2 pos, Color bg, Color ring, Color fg, Vector2? anchor = null)
        {
            var a = anchor ?? new Vector2(0, 1);
            var probe = GlassKit.Label(parent, "đo", text, 12, fg, true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(10, 10));
            float w = Mathf.Ceil(GlassKit.Width(probe, text)) + 24f;
            UnityEngine.Object.DestroyImmediate(probe.gameObject);
            var img = Fill(parent, name, a, a, pos, new Vector2(w, 26), bg, RadiusPill);
            if (ring.a > 0f) Ring(img.transform, "Viền", ring, RadiusPill);
            var t = GlassKit.Label(img.transform, "Chữ", text, 12, fg, true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(w, 26));
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return img.rectTransform;
        }

        /// <summary>Đổ bóng mềm cho chữ lớn (lệch xuống, mờ nhẹ), đọc rõ trên nền sáng lẫn tối.</summary>
        public static void TextShadow(Text t, float alpha, float dy)
        {
            var sh = t.gameObject.GetComponent<UnityEngine.UI.Shadow>() ?? t.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            sh.effectColor = new Color(0f, 0.03f, 0.10f, alpha);
            sh.effectDistance = new Vector2(0, -dy);
            sh.useGraphicAlpha = true;
        }

        // ------------------------------------------------------------------ nền có chiều sâu
        static Texture2D backdropTex, dotTex;
        static int backdropTheme = -1;

        /// <summary>
        /// Nền trang có chiều sâu: dải màu dọc rất nhẹ, hai quầng màu nhấn mờ ở góc, vân chấm mảnh. Ảnh đục (alpha 1)
        /// nên không thành lớp phủ bán trong suốt làm xám giao diện. Vẽ một lần cho mỗi nền giao diện.
        /// </summary>
        public static RawImage Backdrop(Transform parent, string name = "Nền chiều sâu", bool dots = true)
        {
            var rt = Full(parent, name);
            rt.SetAsFirstSibling();
            var raw = rt.gameObject.AddComponent<RawImage>();
            raw.texture = BackdropTexture();
            raw.color = Color.white;
            raw.raycastTarget = true;
            if (dots)
            {
                var drt = Full(rt, "Vân chấm");
                var d = drt.gameObject.AddComponent<RawImage>();
                d.texture = DotTexture();
                d.color = Color.white;
                d.raycastTarget = false;
                drt.gameObject.AddComponent<MenuTile>().cell = 28f;
            }
            return raw;
        }

        static Texture2D BackdropTexture()
        {
            int theme = (int)UiTheme.Current;
            if (backdropTex != null && backdropTheme == theme) return backdropTex;
            if (backdropTex != null) UnityEngine.Object.Destroy(backdropTex);
            backdropTheme = theme;
            const int W = 640, H = 360;
            Color top = UiTheme.Pick(Hex(0xf8f8f9), Hex(0x0c0c0f), Hex(0x0a1628));
            Color bottom = UiTheme.Pick(Hex(0xf1f1f3), Hex(0x070708), Hex(0x050c17));
            Color glow1 = Accent, glow2 = UiTheme.IsLight ? Hex(0x10b981) : Bio;
            float g1 = UiTheme.Pick(0.07f, 0.07f, 0.10f), g2 = UiTheme.Pick(0.04f, 0.035f, 0.05f);
            var px = new Color32[W * H];
            var rng = new System.Random(18112026);
            float aspect = (float)W / H;
            for (int y = 0; y < H; y++)
            {
                float v = (float)y / (H - 1);
                Color baseC = Color.Lerp(bottom, top, v);
                for (int x = 0; x < W; x++)
                {
                    float u = (float)x / (W - 1);
                    // quầng 1: góc trên trái, quầng 2: góc dưới phải (toạ độ chuẩn hoá theo chiều cao)
                    float dx1 = (u - 0.12f) * aspect, dy1 = v - 1.05f;
                    float dx2 = (u - 0.95f) * aspect, dy2 = v + 0.05f;
                    float k1 = Mathf.Exp(-(dx1 * dx1 + dy1 * dy1) / 0.32f);
                    float k2 = Mathf.Exp(-(dx2 * dx2 + dy2 * dy2) / 0.28f);
                    Color c = Color.Lerp(baseC, glow1, k1 * g1);
                    c = Color.Lerp(c, glow2, k2 * g2);
                    // nhiễu ±0,5 mức để dải màu không thành bậc thang
                    float n = ((float)rng.NextDouble() - 0.5f) / 255f;
                    px[y * W + x] = new Color(c.r + n, c.g + n, c.b + n, 1f);
                }
            }
            backdropTex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            backdropTex.SetPixels32(px);
            backdropTex.Apply(false, true);
            if (dotTex != null) { UnityEngine.Object.Destroy(dotTex); dotTex = null; }
            return backdropTex;
        }

        static Texture2D DotTexture()
        {
            if (dotTex != null) return dotTex;
            const int S = 56;     // ô 28 điểm vẽ ở 2x
            Color ink = UiTheme.Pick(new Color(0f, 0f, 0f, 0.075f), new Color(1f, 1f, 1f, 0.055f), new Color(0.6f, 0.82f, 1f, 0.08f));
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = x + 0.5f - S * 0.5f, dy = y + 0.5f - S * 0.5f;
                    float a = Mathf.Clamp01(1.6f - Mathf.Sqrt(dx * dx + dy * dy));
                    var c = ink;
                    c.a *= a;
                    px[y * S + x] = c;
                }
            dotTex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            dotTex.SetPixels32(px);
            dotTex.Apply(false, true);
            return dotTex;
        }

        // ------------------------------------------------------------------ thẻ
        /// <summary>
        /// Thẻ co giãn theo cha: chiều ngang là phần (x0, x1) của cha, cách đỉnh y và cao h (h &lt;= 0 thì kéo tới cách đáy -h).
        /// Gồm bóng rơi + bóng sát (hai lớp), thân, viền tóc và (nền tối) ánh mép trên. Trả về khối thẻ; nội dung đặt vào Body().
        /// </summary>
        public static RectTransform Card(Transform parent, string name, float x0, float x1, float y, float h, float gap, bool hover = true, float radius = RadiusCard)
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
            Dress(rt, hover, radius);
            return rt;
        }

        /// <summary>Thẻ bọc quanh một khối có sẵn (lề pad mỗi phía), nằm dưới nội dung của khối đó.</summary>
        public static RectTransform Surface(RectTransform around, string name, float pad, float radius = RadiusLarge)
        {
            var rt = Stretch(around, name, Vector2.zero, Vector2.one, new Vector2(-pad, -pad), new Vector2(pad, pad));
            rt.SetAsFirstSibling();
            Dress(rt, false, radius);
            Body(rt).GetComponent<Image>().raycastTarget = true;
            return rt;
        }

        static void Dress(RectTransform rt, bool hover, float radius)
        {
            bool light = UiTheme.IsLight;
            float dropRest = light ? 0.07f : 0.42f, dropHover = light ? 0.15f : 0.62f;
            float nearRest = light ? 0.05f : 0.30f, nearHover = light ? 0.07f : 0.36f;
            var drop = Shadow(rt, "Bóng", 30f, radius, -14f, dropRest);
            var near = Shadow(rt, "Bóng sát", 4f, radius, -1f, nearRest);
            var body = Stretch(rt, "Thân", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var bi = Paint(body, Paper, radius);
            bi.raycastTarget = hover;
            var ring = Ring(rt, "Viền", Hair, radius);
            Image sheen = light ? null : Sheen(rt, radius, UiTheme.IsVera ? 0.14f : 0.10f);
            var hv = rt.gameObject.AddComponent<MenuHover>();
            hv.shadow = drop;
            hv.shadowRest = dropRest;
            hv.shadowHover = hover ? dropHover : dropRest;
            hv.shadowNear = near;
            hv.nearRest = nearRest;
            hv.nearHover = hover ? nearHover : nearRest;
            hv.ring = ring;
            hv.ringRest = Hair;
            hv.ringHover = hover ? HairStrong : Hair;
            hv.liftTargets = sheen != null
                ? new[] { body, ring.rectTransform, sheen.rectTransform }
                : new[] { body, ring.rectTransform };
        }

        /// <summary>Nơi đặt nội dung bên trong thẻ do Card tạo.</summary>
        public static RectTransform Body(RectTransform card) => (RectTransform)card.Find("Thân");

        /// <summary>Viền tóc của thẻ (để đổi màu).</summary>
        public static Image CardRing(RectTransform card) => card.Find("Viền")?.GetComponent<Image>();

        /// <summary>
        /// Biến cả thẻ thành nút: rê chuột thì thẻ nhấc lên lift điểm, bóng đậm và rơi xa, viền chuyển màu nhấn
        /// (kiểu thẻ lớn CHEMISTRY: translateY(-5px) + bóng 0 20px 40px). Trả về nút gắn trên thân thẻ.
        /// </summary>
        public static Button MakeCardClickable(RectTransform card, Action onClick, float lift = 5f, Color? ringHover = null)
        {
            var body = Body(card);
            var img = body.GetComponent<Image>();
            img.raycastTarget = true;
            var b = body.gameObject.AddComponent<Button>();
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            b.transition = Selectable.Transition.None;
            b.targetGraphic = img;
            if (onClick != null) b.onClick.AddListener(() => onClick());
            var hv = card.GetComponent<MenuHover>();
            if (hv != null)
            {
                hv.lift = lift;
                hv.pressScale = 0.99f;
                hv.shadowHover = UiTheme.IsLight ? 0.18f : 0.66f;
                hv.ringHover = ringHover ?? WithAlpha(Accent, UiTheme.IsLight ? 0.55f : 0.65f);
            }
            return b;
        }

        /// <summary>
        /// Khung ảnh bo góc khử răng cưa: ảnh cắt theo tỉ lệ khung (cover), bốn góc phủ bằng màu nền (cap), viền tóc.
        /// Tên của RawImage là imageName (bài thử tìm theo tên).
        /// </summary>
        public static RawImage ImageFrame(Transform parent, string name, string imageName, Texture tex, Color capColor, float radius, Color fallback)
        {
            var frame = Full(parent, name);
            var raw = Full(frame, imageName).gameObject.AddComponent<RawImage>();
            raw.texture = tex;
            raw.color = tex != null ? Color.white : fallback;
            raw.raycastTarget = false;
            var fit = frame.gameObject.AddComponent<MenuCover>();
            fit.target = raw;
            var cap = Full(frame, "Góc bo").gameObject.AddComponent<Image>();
            cap.sprite = CornerCap(radius);
            cap.type = Image.Type.Sliced;
            cap.fillCenter = false;
            cap.pixelsPerUnitMultiplier = ShapeScale;
            cap.color = capColor;
            cap.raycastTarget = false;
            Ring(frame, "Viền ảnh", Hair, radius);
            return raw;
        }

        // ------------------------------------------------------------------ nút
        /// <summary>
        /// style: 0 nút chính (màu nhấn, dải sáng nhẹ, viền sáng mép trên), 1 nút phụ (nền thẻ + viền tóc),
        /// 2 chữ không nền (rê chuột hiện nền mờ), 3 nền trắng chữ màu nhấn (đặt trên thẻ màu).
        /// Cấu trúc: [tên] (Button) → Bóng, Mặt (đích bấm), Viền, Sáng (lớp rê/nhấn), Chữ.
        /// </summary>
        public static Button MakeButton(Transform parent, string name, string text, Vector2 pos, Vector2 size, Action onClick, int style, float radius = RadiusButton, Vector2? anchor = null)
        {
            var a = anchor ?? new Vector2(0, 1);
            var pivot = anchor.HasValue ? anchor.Value : new Vector2(0, 1);
            var rt = GlassKit.Rect(parent, name, a, pivot, pos, size);
            bool light = UiTheme.IsLight;

            Image shadow = null;
            if (style == 0) shadow = Shadow(rt, "Bóng", 10f, radius, -3f, light ? 0.30f : 0.40f, light ? AccentDeep : (Color?)null);
            else if (style == 1 || style == 3) shadow = Shadow(rt, "Bóng", 3f, radius, -1f, light ? 0.06f : 0.28f);

            var face = Paint(Full(rt, "Mặt"), style == 0 ? Accent : style == 1 ? Paper : style == 3 ? Color.white : new Color(1, 1, 1, 0.001f), radius);
            face.raycastTarget = true;
            if (style == 0) Gradient(face, new Color(1.08f, 1.08f, 1.08f, 1f), new Color(0.93f, 0.93f, 0.93f, 1f));

            Image ring = null;
            if (style == 1) ring = Ring(rt, "Viền", HairStrong, radius);
            else if (style == 0) Sheen(rt, radius, 0.32f);

            var glow = Paint(Full(rt, "Sáng"), new Color(1, 1, 1, 0f), radius);
            Color washRest, washHover;
            if (style == 0 || style == 3)
            {
                washRest = new Color(1, 1, 1, 0f);
                washHover = style == 0 ? new Color(1, 1, 1, 0.12f) : new Color(0, 0, 0, 0.05f);
            }
            else
            {
                var w = GlassKit.Wash(light ? 0.05f : 0.07f);
                washHover = w;
                w.a = 0f;
                washRest = w;
            }
            glow.color = washRest;

            var b = rt.gameObject.AddComponent<Button>();
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            b.transition = Selectable.Transition.None;
            b.targetGraphic = face;
            if (onClick != null) b.onClick.AddListener(() => onClick());

            var label = GlassKit.Label(rt, "Chữ", text, style == 2 ? 16 : 17, style == 0 ? OnAccent : style == 1 ? Ink : style == 3 ? Accent : InkDim,
                                       style != 2, TextAnchor.MiddleCenter, Vector2.zero, size);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;

            var hover = rt.gameObject.AddComponent<MenuHover>();
            hover.pressScale = 0.97f;
            hover.tint = glow;
            hover.tintRest = washRest;
            hover.tintHover = washHover;
            if (shadow != null)
            {
                hover.shadow = shadow;
                hover.shadowRest = shadow.color.a;
                hover.shadowHover = Mathf.Min(1f, shadow.color.a * 1.6f);
            }
            if (ring != null)
            {
                hover.ring = ring;
                hover.ringRest = HairStrong;
                hover.ringHover = light ? new Color(0, 0, 0, 0.30f) : new Color(1, 1, 1, 0.30f);
            }
            if (style != 2)
            {
                hover.lift = 1f;
                var targets = new List<RectTransform>();
                foreach (Transform c in rt) if (c.name != "Bóng") targets.Add((RectTransform)c);
                hover.liftTargets = targets.ToArray();
            }
            return b;
        }

        /// <summary>Mặt tô màu của nút do MakeButton tạo (để đổi màu nền).</summary>
        public static Image Face(Button b) => b != null ? b.transform.Find("Mặt")?.GetComponent<Image>() : null;

        // ------------------------------------------------------------------ ô nhập, công tắc, nhóm chọn
        /// <summary>Ô nhập: nền lõm, viền tóc; khi gõ thì viền màu nhấn và quầng 3 điểm (như :focus của CHEMISTRY).</summary>
        public static InputField TextField(Transform parent, string name, string placeholder, Vector2 pos, Vector2 size, int fontSize = 18, bool password = false)
        {
            var rt = GlassKit.Rect(parent, name, new Vector2(0, 1), new Vector2(0, 1), pos, size);
            var haloRt = Stretch(rt, "Quầng", Vector2.zero, Vector2.one, new Vector2(-3, -3), new Vector2(3, 3));
            var halo = Paint(haloRt, WithAlpha(Accent, 0f), RadiusButton + 3f);
            var face = Paint(Full(rt, "Nền"), Well, RadiusButton);
            face.raycastTarget = true;
            var ring = Ring(rt, "Viền", HairStrong, RadiusButton);
            var text = GlassKit.Label(rt, "Chữ", "", fontSize, Ink, false, TextAnchor.MiddleLeft, new Vector2(16, 0), new Vector2(size.x - 32, size.y));
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            Text ph = null;
            if (!string.IsNullOrEmpty(placeholder))
            {
                ph = GlassKit.Label(rt, "Gợi ý", placeholder, fontSize, InkMute, false, TextAnchor.MiddleLeft, new Vector2(16, 0), new Vector2(size.x - 32, size.y));
                ph.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
            var f = rt.gameObject.AddComponent<InputField>();
            f.textComponent = text;
            f.placeholder = ph;
            f.targetGraphic = face;
            f.transition = Selectable.Transition.None;
            f.navigation = new Navigation { mode = Navigation.Mode.None };
            f.caretColor = Accent;
            f.customCaretColor = true;
            f.caretWidth = 2;
            f.selectionColor = WithAlpha(Accent, 0.25f);
            f.lineType = InputField.LineType.SingleLine;
            if (password)
            {
                f.contentType = InputField.ContentType.Password;
                f.asteriskChar = '•';
            }
            var fr = rt.gameObject.AddComponent<MenuFocusRing>();
            fr.field = f;
            fr.ring = ring;
            fr.halo = halo;
            fr.ringRest = HairStrong;
            fr.ringFocus = WithAlpha(Accent, 0.9f);
            fr.haloFocus = WithAlpha(Accent, UiTheme.IsLight ? 0.16f : 0.24f);
            return f;
        }

        /// <summary>Nhóm nút chọn một: ngang là thanh phân đoạn (segmented), dọc là danh sách mục. Vệt chọn trượt theo lò xo.</summary>
        public static MenuTabs Tabs(Transform parent, string name, string[] buttonNames, string[] texts, Vector2 pos, Vector2 itemSize, bool vertical, float gap, int active, Action<int> onSelect)
        {
            int n = texts.Length;
            Vector2 total = vertical ? new Vector2(itemSize.x, n * itemSize.y + (n - 1) * gap) : new Vector2(n * itemSize.x + (n - 1) * gap, itemSize.y);
            var root = GlassKit.Rect(parent, name, new Vector2(0, 1), new Vector2(0, 1), pos, total);
            if (!vertical)
            {
                // khay lõm chứa cả thanh
                var trayRt = Stretch(root, "Khay", Vector2.zero, Vector2.one, new Vector2(-4, -4), new Vector2(4, 4));
                Paint(trayRt, Well, RadiusButton + 4f);
                Ring(trayRt, "Viền", Hair, RadiusButton + 4f);
            }
            var glider = GlassKit.Rect(root, "Vệt chọn", new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, itemSize);
            Shadow(glider, "Bóng", 4f, RadiusButton, -1f, UiTheme.IsLight ? 0.08f : 0.35f);
            Paint(Full(glider, "Mặt"), vertical ? AccentSoft : Paper, RadiusButton);
            Ring(glider, "Viền", vertical ? WithAlpha(Accent, 0.28f) : HairStrong, RadiusButton);

            var tabs = root.gameObject.AddComponent<MenuTabs>();
            tabs.glider = glider;
            tabs.buttons = new Button[n];
            tabs.labels = new Text[n];
            tabs.onSelect = onSelect;
            for (int i = 0; i < n; i++)
            {
                int idx = i;
                var p = vertical ? new Vector2(0, -i * (itemSize.y + gap)) : new Vector2(i * (itemSize.x + gap), 0);
                var b = MakeButton(root, buttonNames != null ? buttonNames[i] : "Mục " + texts[i], texts[i], p, itemSize, () => tabs.Select(idx, true), 2);
                var lbl = b.transform.Find("Chữ").GetComponent<Text>();
                if (vertical)
                {
                    lbl.alignment = TextAnchor.MiddleLeft;
                    lbl.rectTransform.anchoredPosition = new Vector2(16, 0);
                    lbl.rectTransform.sizeDelta = new Vector2(itemSize.x - 28, itemSize.y);
                }
                lbl.fontSize = 16;
                tabs.buttons[i] = b;
                tabs.labels[i] = lbl;
            }
            tabs.Select(active);
            return tabs;
        }

        /// <summary>Công tắc viên nang: núm tròn có bóng, trượt bằng lò xo; tự theo Toggle.isOn (kể cả SetIsOnWithoutNotify).</summary>
        public static Toggle Switch(Transform parent, string name, string label, Vector2 pos, Vector2 size, bool initial, Action<bool> onChanged)
        {
            if (size.y < 44f) size.y = 44f;
            var rt = GlassKit.Rect(parent, name, new Vector2(0, 1), new Vector2(0, 1), pos, size);
            var hit = rt.gameObject.AddComponent<Image>();
            hit.sprite = GlassKit.Plain;
            hit.color = new Color(1, 1, 1, 0.001f);
            var lbl = GlassKit.Label(rt, "Nhãn", label, 16, Ink, false, TextAnchor.MiddleLeft, Vector2.zero, new Vector2(size.x - 70, size.y));
            lbl.horizontalOverflow = HorizontalWrapMode.Wrap;
            var track = Fill(rt, "Rãnh công tắc", new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(52, 30), Well, RadiusPill);
            var trackRing = Ring(track.transform, "Viền", HairStrong, RadiusPill);
            var knob = GlassKit.Rect(track.transform, "Núm", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-11, 0), new Vector2(24, 24));
            Shadow(knob, "Bóng", 4f, RadiusPill, -1f, UiTheme.IsLight ? 0.18f : 0.45f);
            var knobFace = Paint(Full(knob, "Mặt"), Color.white, RadiusPill);
            var tog = rt.gameObject.AddComponent<Toggle>();
            tog.navigation = new Navigation { mode = Navigation.Mode.None };
            tog.transition = Selectable.Transition.None;
            tog.targetGraphic = hit;
            tog.SetIsOnWithoutNotify(initial);
            if (onChanged != null) tog.onValueChanged.AddListener(v => onChanged(v));
            var sw = rt.gameObject.AddComponent<MenuSwitch>();
            sw.toggle = tog;
            sw.track = track;
            sw.trackRing = trackRing;
            sw.knob = knob;
            sw.knobFace = knobFace;
            return tog;
        }

        /// <summary>
        /// Tạo sẵn các chữ tiếng Việt trong bảng phông động. Không làm thì lần đầu hiện chữ mới, Unity phải dựng lại
        /// bảng phông giữa lúc đang chuyển động, gây khựng.
        /// </summary>
        public static void PrewarmFonts(float canvasScale)
        {
            const string chars =
                "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,:;!?%/+-–·()…\"'→←" +
                "ÀÁẢÃẠĂẰẮẲẴẶÂẦẤẨẪẬĐÈÉẺẼẸÊỀẾỂỄỆÌÍỈĨỊÒÓỎÕỌÔỒỐỔỖỘƠỜỚỞỠỢÙÚỦŨỤƯỪỨỬỮỰỲÝỶỸỴ" +
                "àáảãạăằắẳẵặâầấẩẫậđèéẻẽẹêềếểễệìíỉĩịòóỏõọôồốổỗộơờớởỡợùúủũụưừứửữựỳýỷỹỵ";
            if (Mathf.Approximately(prewarmedScale, canvasScale)) return;
            prewarmedScale = canvasScale;
            foreach (var s in new[] { 13, 14, 15, 16, 17, 18, 20 }) GlassKit.Regular.RequestCharactersInTexture(chars, Px(s, canvasScale));
            foreach (var s in new[] { 13, 16, 17, 18, 20, 24, 28, 40 }) GlassKit.Semibold.RequestCharactersInTexture(chars, Px(s, canvasScale));
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

    /// <summary>Ảnh phủ kín khung (cắt phần thừa), tính lại khi khung đổi cỡ.</summary>
    public class MenuCover : UIBehaviour
    {
        public RawImage target;

        protected override void OnEnable()
        {
            base.OnEnable();
            Fit();
        }

        protected override void OnRectTransformDimensionsChange() => Fit();

        void Fit()
        {
            if (target == null || target.texture == null) return;
            var r = ((RectTransform)transform).rect;
            if (r.width < 1f || r.height < 1f) return;
            float want = r.width / r.height, have = (float)target.texture.width / target.texture.height;
            target.uvRect = want < have
                ? new UnityEngine.Rect((1f - want / have) * 0.5f, 0, want / have, 1)
                : new UnityEngine.Rect(0, (1f - have / want) * 0.5f, 1, have / want);
        }
    }

    /// <summary>Hình ảnh của công tắc: theo Toggle.isOn, núm trượt và rãnh đổi màu bằng lò xo (không cấp phát).</summary>
    public class MenuSwitch : MonoBehaviour
    {
        public Toggle toggle;
        public Image track, trackRing, knobFace;
        public RectTransform knob;
        float k = -1f, v;

        void Update()
        {
            if (toggle == null) return;
            float target = toggle.isOn ? 1f : 0f;
            if (k >= 0f && Mathf.Abs(k - target) < 0.002f && Mathf.Abs(v) < 0.01f) return;
            if (k < 0f || ReducedMotion.IsEnabled) { k = target; v = 0f; }
            else
            {
                float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 20f);
                const float Sub = 1f / 240f;
                while (dt > 1e-6f)
                {
                    float s = dt < Sub ? dt : Sub;
                    v += (-520f * (k - target) - 36f * v) * s;
                    k += v * s;
                    dt -= s;
                }
            }
            float c = Mathf.Clamp01(k);
            knob.anchoredPosition = new Vector2(Mathf.LerpUnclamped(-11f, 11f, k), 0f);
            track.color = Color.Lerp(MenuKit.Well, MenuKit.Accent, c);
            trackRing.color = Color.Lerp(MenuKit.HairStrong, MenuKit.WithAlpha(MenuKit.Accent, 0f), c);
            knobFace.color = Color.Lerp(UiTheme.IsLight ? Color.white : MenuKit.Hex(0xd4d4d8), Color.white, c);
        }
    }
}
