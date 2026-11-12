using System.Collections.Generic;
using Khkt.Environment.Tang2;
using UnityEngine;
using UnityEngine.UI;
using static Khkt.Demo.GlassKit;

namespace Khkt.Demo
{
    /// <summary>
    /// Trung tâm thông báo dùng chung cho cả app (GlassKit Pro): mọi thông báo sử dụng dụng cụ của phòng Hóa (LabMessages),
    /// phòng Sinh (BioFeedback), giáo viên, tay cầm đều hiện ở đây, cùng một kiểu thẻ kính tối bo góc.
    ///
    ///   • Thẻ xếp chồng ở giữa phía trên, tối đa 3 thẻ; thẻ mới trượt vào trên cùng, thẻ cũ dịch xuống.
    ///   • Mỗi thẻ có huy hiệu màu theo loại (thông tin, đúng, cảnh báo, lỗi), dòng chính, dòng giải thích và vạch thời gian.
    ///   • Thông báo lặp lại không chồng thêm thẻ mà cộng số đếm (×2, ×3) và gia hạn thẻ đang hiện.
    ///   • Vòng tiến trình giữ-để-thao-tác (hơ que cấy, hạ lamen...) là vòng tròn thật quanh tâm ngắm.
    ///
    /// Canvas riêng (Screen Space - Overlay): thẻ chuyển động không làm dựng lại lưới của giao diện chính, và khi không có
    /// thông báo nào thì cả canvas lẫn Update đều tắt hẳn. Thay cho ba hộp IMGUI cũ (cỡ chữ theo điểm ảnh nên trên iPad
    /// bé xíu, mỗi khung hình OnGUI chạy nhiều lượt và sinh rác bộ nhớ).
    /// </summary>
    public class LabToasts : MonoBehaviour
    {
        public enum Tone { Info, Good, Warn, Error }

        /// <summary>Khoảng cách từ mép trên màn hình tới thẻ đầu tiên (giao diện chính đặt lại khi thanh bước hiện).</summary>
        public static float TopOffset = 86f;

        const int MaxVisible = 3;
        const float CardWidth = 560f;
        const float Gap = 10f;
        const float PadX = 16f, PadY = 13f, BadgeSize = 36f;
        const float EnterS = 0.20f, LeaveS = 0.16f;

        static LabToasts instance;
        static bool progressWanted;

        class Card
        {
            public RectTransform rt;
            public CanvasGroup group;
            public Image border, badge, bar;
            public Text glyph, title, body, sub, count;
            public string bodyText;
            public Tone tone;
            public float age, total, y, height, leaveT, barFull, lastHit;
            public int repeats;
            public bool leaving, placed;
        }

        Canvas canvas;
        RectTransform root;
        readonly List<Card> active = new List<Card>();   // mới nhất đứng đầu
        readonly Stack<Card> pool = new Stack<Card>();

        // Vòng tiến trình
        RectTransform ringRoot;
        CanvasGroup ringGroup;
        Image ringFill;
        Text ringPct, ringLabel;
        RectTransform ringPill;
        float ring01, ringAlpha;
        int ringShownPct = -1;
        string ringLabelText = "";
        static Sprite ringSprite;

        // =====================================================================
        // Gọi từ bên ngoài
        // =====================================================================

        /// <summary>Hiện một thông báo. title: dòng nhãn nhỏ phía trên (bỏ trống thì không có); sub: dòng giải thích phía dưới.</summary>
        public static void Push(string body, Tone tone = Tone.Info, float seconds = 4f, string title = null, string sub = null)
        {
            if (string.IsNullOrEmpty(body) || !Application.isPlaying) return;
            Ensure().Add(body, tone, Mathf.Max(1.5f, seconds), title, sub);
        }

        /// <summary>Vòng tiến trình giữ-để-thao-tác: gọi mỗi khung hình trong lúc đang giữ.</summary>
        public static void Progress(string label, float progress01)
        {
            if (!Application.isPlaying) return;
            var t = Ensure();
            progressWanted = true;
            t.ring01 = Mathf.Clamp01(progress01);
            label ??= "";
            if (!ReferenceEquals(label, t.ringLabelText) && label != t.ringLabelText)
            {
                t.ringLabelText = label;
                t.ringLabelDirty = true;
            }
            t.Wake();
        }

        /// <summary>Ẩn vòng tiến trình. Rẻ: bàn tay gọi mỗi khung hình khi không giữ.</summary>
        public static void HideProgress() => progressWanted = false;

        /// <summary>Đang có thẻ thông báo nào hiện không (kiểm thử dùng).</summary>
        public static int VisibleCount => instance != null ? instance.active.Count : 0;

        // =====================================================================
        // Dựng
        // =====================================================================

        static LabToasts Ensure()
        {
            if (instance != null) return instance;
            var go = new GameObject("Trung tâm thông báo", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            if (Application.isPlaying) DontDestroyOnLoad(go);
            instance = go.AddComponent<LabToasts>();
            return instance;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
            progressWanted = false;
            TopOffset = 86f;
        }

        void Awake()
        {
            canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 700;  // trên giao diện chính và HUD cảm ứng, dưới màn mờ tải khu (900)
            var scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            root = (RectTransform)transform;
            Canvas.ForceUpdateCanvases();
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        void Wake()
        {
            if (!enabled) enabled = true;
            if (!canvas.enabled) canvas.enabled = true;
        }

        static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        static Image Fill(Transform parent, string name, Sprite sprite, Color color, float cornerScale)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = cornerScale;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        Card BuildCard()
        {
            var c = new Card();
            c.rt = Rect(root, "Thẻ thông báo", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(CardWidth, 64));
            c.group = c.rt.gameObject.AddComponent<CanvasGroup>();
            c.group.blocksRaycasts = false;
            c.group.interactable = false;

            // Viền sáng theo màu loại thông báo, bên trong là nền kính tối đặc (đọc rõ trên mọi cảnh phòng lab)
            c.border = Fill(c.rt, "Viền", Solid, Color.white, 1.9f);
            Stretch(c.border.rectTransform, 0f);
            var bg = Fill(c.rt, "Nền", Solid, new Color(0.045f, 0.075f, 0.13f, 0.95f), 1.9f);
            Stretch(bg.rectTransform, 1.5f);

            var badgeRt = Rect(c.rt, "Huy hiệu", new Vector2(0, 1), new Vector2(0, 1), new Vector2(PadX, -PadY), new Vector2(BadgeSize, BadgeSize));
            c.badge = badgeRt.gameObject.AddComponent<Image>();
            c.badge.sprite = SolidCircle;
            c.badge.raycastTarget = false;
            c.glyph = Label(badgeRt, "Ký hiệu", "i", 20, new Color(0.03f, 0.07f, 0.12f), true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(BadgeSize, BadgeSize));

            float tx = PadX + BadgeSize + 14f;
            c.title = Label(c.rt, "Nhãn", "", 13, Accent, true, TextAnchor.UpperLeft, new Vector2(tx, -PadY), new Vector2(360, 18));
            c.title.horizontalOverflow = HorizontalWrapMode.Overflow;
            c.body = Label(c.rt, "Nội dung", "", 17, Ink, false, TextAnchor.UpperLeft, new Vector2(tx, -PadY), new Vector2(400, 24));
            c.sub = Label(c.rt, "Giải thích", "", 14, Dim, false, TextAnchor.UpperLeft, new Vector2(tx, -PadY), new Vector2(400, 20));

            var countRt = Rect(c.rt, "Số lần", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12, -10), new Vector2(40, 22));
            var countBg = countRt.gameObject.AddComponent<Image>();
            countBg.sprite = Solid;
            countBg.type = Image.Type.Sliced;
            countBg.pixelsPerUnitMultiplier = 4f;
            countBg.color = new Color(1, 1, 1, 0.14f);
            countBg.raycastTarget = false;
            c.count = Label(countRt, "Chữ", "", 13, Ink, true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(40, 22));

            var barRt = Rect(c.rt, "Vạch thời gian", new Vector2(0, 0), new Vector2(0, 0), new Vector2(PadX + 2, 6), new Vector2(100, 2.5f));
            c.bar = barRt.gameObject.AddComponent<Image>();
            c.bar.sprite = Solid;
            c.bar.type = Image.Type.Sliced;
            c.bar.pixelsPerUnitMultiplier = 24f;
            c.bar.raycastTarget = false;
            return c;
        }

        static Color ToneColor(Tone t) => t == Tone.Good ? Good : t == Tone.Warn ? Warn : t == Tone.Error ? Bad : Accent;
        static string ToneGlyph(Tone t) => t == Tone.Good ? "✓" : t == Tone.Warn ? "!" : t == Tone.Error ? "✕" : "i";

        // =====================================================================
        // Thêm thẻ
        // =====================================================================

        void Add(string body, Tone tone, float seconds, string title, string sub)
        {
            Wake();

            // Lặp lại thông báo đang hiện: gia hạn và cộng số đếm, không chồng thêm thẻ
            for (int i = 0; i < active.Count; i++)
            {
                var a = active[i];
                if (a.leaving || a.bodyText != body) continue;
                a.total = Mathf.Max(a.total, a.age + seconds * 0.6f);
                // Cảnh báo đang kéo dài (xả khí độc, pipet đầy...) được gọi lại đều đặn: chỉ giữ thẻ, không đếm.
                // Cách lần trước đủ lâu mới là một lần mắc lại thật.
                bool again = a.age - a.lastHit > 2.5f;
                a.lastHit = a.age;
                if (again)
                {
                    a.repeats++;
                    a.count.text = "×" + a.repeats;
                    a.count.transform.parent.gameObject.SetActive(true);
                }
                if (tone > a.tone) Paint(a, tone);  // lần sau nghiêm trọng hơn: nâng màu
                return;
            }

            // Quá số thẻ cho phép: thẻ cũ nhất lui ra
            int shown = 0;
            for (int i = 0; i < active.Count; i++) if (!active[i].leaving) shown++;
            for (int i = active.Count - 1; i >= 0 && shown >= MaxVisible; i--)
                if (!active[i].leaving) { active[i].leaving = true; shown--; }

            var c = pool.Count > 0 ? pool.Pop() : BuildCard();
            float width = Mathf.Min(CardWidth, Mathf.Max(280f, root.rect.width - 32f));
            float tx = PadX + BadgeSize + 14f;
            float tw = width - tx - PadX - 4f;

            c.bodyText = body;
            c.repeats = 1;
            c.count.transform.parent.gameObject.SetActive(false);
            Paint(c, tone);

            float y = PadY;
            bool hasTitle = !string.IsNullOrEmpty(title);
            c.title.gameObject.SetActive(hasTitle);
            if (hasTitle)
            {
                c.title.text = title.ToUpperInvariant();
                c.title.rectTransform.anchoredPosition = new Vector2(tx, -y);
                y += 20f;
            }
            c.body.text = body;
            c.body.rectTransform.anchoredPosition = new Vector2(tx, -y);
            c.body.rectTransform.sizeDelta = new Vector2(tw, 24f);
            float bodyH = Mathf.Max(24f, c.body.preferredHeight);
            c.body.rectTransform.sizeDelta = new Vector2(tw, bodyH);
            y += bodyH;
            bool hasSub = !string.IsNullOrEmpty(sub);
            c.sub.gameObject.SetActive(hasSub);
            if (hasSub)
            {
                y += 4f;
                c.sub.text = sub;
                c.sub.rectTransform.anchoredPosition = new Vector2(tx, -y);
                c.sub.rectTransform.sizeDelta = new Vector2(tw, 20f);
                float subH = Mathf.Max(20f, c.sub.preferredHeight);
                c.sub.rectTransform.sizeDelta = new Vector2(tw, subH);
                y += subH;
            }
            c.height = Mathf.Max(BadgeSize + PadY * 2f, y + PadY + 3f);
            c.rt.sizeDelta = new Vector2(width, c.height);
            c.barFull = width - (PadX + 2f) * 2f;

            c.age = 0f;
            c.lastHit = 0f;
            c.total = seconds;
            c.leaveT = 0f;
            c.leaving = false;
            c.placed = false;
            c.group.alpha = 0f;
            c.rt.gameObject.SetActive(true);
            c.rt.SetAsLastSibling();
            active.Insert(0, c);
        }

        static void Paint(Card c, Tone tone)
        {
            c.tone = tone;
            Color col = ToneColor(tone);
            c.border.color = new Color(col.r, col.g, col.b, tone == Tone.Info ? 0.38f : 0.62f);
            c.badge.color = col;
            c.glyph.text = ToneGlyph(tone);
            c.title.color = col;
            c.bar.color = new Color(col.r, col.g, col.b, 0.75f);
        }

        // =====================================================================
        // Chuyển động
        // =====================================================================

        bool ringLabelDirty = true;

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            bool reduced = ReducedMotion.IsEnabled;
            float top = TopOffset + SafeTop();
            float follow = 1f - Mathf.Exp(-16f * dt);

            float cursor = 0f;
            for (int i = 0; i < active.Count; i++)
            {
                var c = active[i];
                c.age += dt;
                if (!c.leaving && c.age >= c.total) c.leaving = true;
                if (c.leaving)
                {
                    c.leaveT += dt / LeaveS;
                    if (c.leaveT >= 1f)
                    {
                        c.rt.gameObject.SetActive(false);
                        pool.Push(c);
                        active.RemoveAt(i--);
                        continue;
                    }
                }
                float enter = Mathf.Clamp01(c.age / (reduced ? 0.06f : EnterS));
                float ease = 1f - (1f - enter) * (1f - enter) * (1f - enter);
                float stay = 1f - Mathf.Clamp01(c.leaveT);

                if (!c.placed) { c.y = cursor; c.placed = true; }
                else c.y = Mathf.Lerp(c.y, cursor, follow);
                cursor += (c.height + Gap) * stay;  // thẻ đang lui nhường chỗ dần: các thẻ dưới trượt lên êm

                float slide = reduced ? 0f : (1f - ease) * 14f;
                c.rt.anchoredPosition = new Vector2(0f, -(top + c.y) + slide);
                float s = reduced ? 1f : Mathf.Lerp(0.96f, 1f, ease);
                c.rt.localScale = new Vector3(s, s, 1f);
                c.group.alpha = ease * stay;
                c.bar.rectTransform.sizeDelta = new Vector2(c.barFull * Mathf.Clamp01(1f - c.age / c.total), 2.5f);
            }

            UpdateRing(dt);

            if (active.Count == 0 && ringAlpha <= 0f && !progressWanted)
            {
                // Không còn gì để vẽ: tắt hẳn cho tới thông báo sau
                canvas.enabled = false;
                enabled = false;
            }
        }

        float SafeTop()
        {
            float sf = canvas.scaleFactor;
            return sf > 0.01f ? Mathf.Max(0f, Screen.height - Screen.safeArea.yMax) / sf : 0f;
        }

        // =====================================================================
        // Vòng tiến trình
        // =====================================================================

        void BuildRing()
        {
            ringRoot = Rect(root, "Vòng tiến trình", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(96, 96));
            ringGroup = ringRoot.gameObject.AddComponent<CanvasGroup>();
            ringGroup.blocksRaycasts = false;
            ringGroup.interactable = false;
            ringGroup.alpha = 0f;

            var disc = Rect(ringRoot, "Đĩa nền", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(96, 96)).gameObject.AddComponent<Image>();
            disc.sprite = SolidCircle;
            disc.color = new Color(0.045f, 0.075f, 0.13f, 0.72f);
            disc.raycastTarget = false;

            var track = Rect(ringRoot, "Rãnh", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84, 84)).gameObject.AddComponent<Image>();
            track.sprite = RingSprite();
            track.color = new Color(1, 1, 1, 0.16f);
            track.raycastTarget = false;

            ringFill = Rect(ringRoot, "Mức", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84, 84)).gameObject.AddComponent<Image>();
            ringFill.sprite = RingSprite();
            ringFill.type = Image.Type.Filled;
            ringFill.fillMethod = Image.FillMethod.Radial360;
            ringFill.fillOrigin = (int)Image.Origin360.Top;
            ringFill.fillClockwise = true;
            ringFill.fillAmount = 0f;
            ringFill.color = Accent;
            ringFill.raycastTarget = false;

            var pctRt = Rect(ringRoot, "Phần trăm", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84, 30));
            ringPct = pctRt.gameObject.AddComponent<Text>();
            ringPct.font = Semibold;
            ringPct.fontSize = 19;
            ringPct.alignment = TextAnchor.MiddleCenter;
            ringPct.color = Ink;
            ringPct.raycastTarget = false;

            ringPill = Rect(ringRoot, "Nhãn thao tác", new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0, -12), new Vector2(240, 36));
            var pillBg = ringPill.gameObject.AddComponent<Image>();
            pillBg.sprite = Solid;
            pillBg.type = Image.Type.Sliced;
            pillBg.pixelsPerUnitMultiplier = 2.6f;
            pillBg.color = new Color(0.045f, 0.075f, 0.13f, 0.9f);
            pillBg.raycastTarget = false;
            var labRt = Rect(ringPill, "Chữ", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240, 36));
            ringLabel = labRt.gameObject.AddComponent<Text>();
            ringLabel.font = Regular;
            ringLabel.fontSize = 16;
            ringLabel.alignment = TextAnchor.MiddleCenter;
            ringLabel.color = Ink;
            ringLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            ringLabel.raycastTarget = false;
        }

        void UpdateRing(float dt)
        {
            if (ringRoot == null)
            {
                if (!progressWanted) return;
                BuildRing();
            }
            ringAlpha = Mathf.MoveTowards(ringAlpha, progressWanted ? 1f : 0f, dt * (progressWanted ? 12f : 7f));
            bool show = ringAlpha > 0.001f;
            if (ringRoot.gameObject.activeSelf != show) ringRoot.gameObject.SetActive(show);
            if (!show) { ringShownPct = -1; return; }
            ringGroup.alpha = ringAlpha;

            ringFill.fillAmount = ring01;
            int pct = Mathf.RoundToInt(ring01 * 100f);
            if (pct != ringShownPct)
            {
                // chữ chỉ dựng lại khi con số đổi (tối đa 100 lần cho một lượt giữ)
                ringShownPct = pct;
                ringPct.text = pct >= 100 ? "✓" : pct + "%";
                ringFill.color = pct >= 100 ? Good : Accent;
            }
            if (ringLabelDirty)
            {
                ringLabelDirty = false;
                ringLabel.text = ringLabelText;
                bool has = ringLabelText.Length > 0;
                if (ringPill.gameObject.activeSelf != has) ringPill.gameObject.SetActive(has);
                if (has)
                {
                    float w = Mathf.Clamp(Width(ringLabel, ringLabelText) + 36f, 120f, 760f);
                    ringPill.sizeDelta = new Vector2(w, 36f);
                    ringLabel.rectTransform.sizeDelta = new Vector2(w, 36f);
                }
            }
        }

        /// <summary>Vành tròn trắng khử răng cưa (dùng cho Image kiểu Filled Radial360).</summary>
        static Sprite RingSprite()
        {
            if (ringSprite != null) return ringSprite;
            const int size = 128;
            const float outer = 62f, inner = 53f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var px = new Color32[size * size];
            float half = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - half, dy = y + 0.5f - half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(outer - d + 0.5f) * Mathf.Clamp01(d - inner + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply();
            ringSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            ringSprite.hideFlags = HideFlags.DontSave;
            return ringSprite;
        }
    }
}
