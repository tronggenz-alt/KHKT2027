using System;
using System.Collections.Generic;
using Khkt.Environment.Tang2;
using UnityEngine;
using UnityEngine.UI;

namespace Khkt.Menu.Dashboard
{
    /// <summary>
    /// Bộ chuyển động của màn hình chính. Một đối tượng chạy tất cả, theo thời gian thật nên không phụ thuộc
    /// Time.timeScale. Mỗi cặp (đối tượng, kênh) chỉ có một chuyển động: bấm liên tục thì cái sau thay cái trước,
    /// không giật. Bật "Giảm chuyển động" thì nhảy thẳng tới trạng thái cuối.
    /// </summary>
    public class MenuAnim : MonoBehaviour
    {
        public enum Ease { OutCubic, OutBack, InOutCubic, Linear, OutQuint, Away }

        // Khung hình bị khựng không được làm chuyển động nhảy cóc: giới hạn bước thời gian
        const float MaxStep = 1f / 20f;

        sealed class Tween
        {
            public UnityEngine.Object owner;
            public string channel;
            public float delay, duration, time;
            public Ease ease;
            public Action<float> apply;
            public Action done;
        }

        static MenuAnim instance;
        readonly List<Tween> tweens = new List<Tween>();
        // Vị trí đúng của đối tượng đang chạy dở: chuyển động mới không được lấy vị trí giữa chừng làm đích
        readonly Dictionary<RectTransform, Vector2> homes = new Dictionary<RectTransform, Vector2>();

        /// <summary>Còn chuyển động đang chạy không. Bài thử chờ hết rồi mới chụp ảnh.</summary>
        public static bool Busy => instance != null && instance.tweens.Count > 0;

        static MenuAnim Runner
        {
            get
            {
                if (instance == null) instance = new GameObject("Chuyển động màn hình chính").AddComponent<MenuAnim>();
                return instance;
            }
        }

        public static float Evaluate(Ease ease, float t)
        {
            t = Mathf.Clamp01(t);
            switch (ease)
            {
                case Ease.OutCubic:
                    return 1f - Mathf.Pow(1f - t, 3f);
                case Ease.OutBack:
                    const float c = 1.4f;
                    float u = t - 1f;
                    return 1f + (c + 1f) * u * u * u + c * u * u;
                case Ease.InOutCubic:
                    return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
                case Ease.OutQuint:
                    // gần cubic-bezier(.22,1,.36,1) của CHEMISTRY (--vao): vào nhanh, đậu êm
                    float q = 1f - t;
                    return 1f - q * q * q * q * q;
                case Ease.Away:
                    // gần cubic-bezier(.33,.17,.67,.5): rời đi tăng tốc dần
                    return t * t;
                default:
                    return t;
            }
        }

        public static void To(UnityEngine.Object owner, string channel, float duration, Action<float> apply, float delay = 0f, Ease ease = Ease.OutCubic, Action done = null)
        {
            Stop(owner, channel);
            if (ReducedMotion.IsEnabled || duration <= 0f)
            {
                apply(1f);
                done?.Invoke();
                return;
            }
            apply(0f);
            Runner.tweens.Add(new Tween { owner = owner, channel = channel, delay = delay, duration = duration, ease = ease, apply = apply, done = done });
        }

        /// <summary>Dừng chuyển động của đối tượng; không đưa về trạng thái cuối.</summary>
        public static void Stop(UnityEngine.Object owner, string channel = null)
        {
            if (instance == null) return;
            var list = instance.tweens;
            for (int i = list.Count - 1; i >= 0; i--)
                if (ReferenceEquals(list[i].owner, owner) && (channel == null || list[i].channel == channel))
                    list.RemoveAt(i);
        }

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, MaxStep);
            for (int i = 0; i < tweens.Count; i++)
            {
                var tw = tweens[i];
                // đối tượng đã bị hủy (đổi cảnh, dựng lại danh sách) thì bỏ, không báo lỗi
                if (tw.owner == null)
                {
                    tweens.RemoveAt(i--);
                    continue;
                }
                if (tw.delay > 0f)
                {
                    tw.delay -= dt;
                    continue;
                }
                tw.time += dt;
                float t = tw.time / tw.duration;
                tw.apply(Evaluate(tw.ease, t));
                if (t < 1f) continue;
                // done có thể thêm hoặc bỏ chuyển động khác nên phải tìm lại vị trí
                int at = tweens.IndexOf(tw);
                if (at >= 0)
                {
                    tweens.RemoveAt(at);
                    if (at <= i) i--;
                }
                tw.done?.Invoke();
            }
        }

        // =====================================================================
        // Các chuyển động hay dùng
        // =====================================================================

        static Vector2 Home(RectTransform rt)
        {
            var map = Runner.homes;
            if (!map.TryGetValue(rt, out var home))
            {
                home = rt.anchoredPosition;
                map[rt] = home;
            }
            return home;
        }

        static void Settle(RectTransform rt)
        {
            if (instance != null) instance.homes.Remove(rt);
        }

        public static CanvasGroup Group(Component c)
        {
            var g = c.GetComponent<CanvasGroup>();
            return g != null ? g : c.gameObject.AddComponent<CanvasGroup>();
        }

        public static void Fade(CanvasGroup g, float from, float to, float duration, float delay = 0f, Action done = null)
        {
            g.alpha = from;
            To(g, "mờ", duration, k => g.alpha = Mathf.LerpUnclamped(from, to, k), delay, Ease.OutCubic, done);
        }

        /// <summary>Hiện dần từ dưới lên, dùng cho thẻ và màn hình mới. Lần lượt theo delay tạo cảm giác xếp lớp.</summary>
        public static void Enter(RectTransform rt, float delay = 0f, float rise = 16f, float duration = 0.45f)
        {
            var g = Group(rt);
            Vector2 end = Home(rt);
            Vector2 start = end - new Vector2(0, rise);
            g.alpha = 0f;
            rt.anchoredPosition = start;
            To(rt, "vào", duration, k =>
            {
                g.alpha = k;
                rt.anchoredPosition = Vector2.LerpUnclamped(start, end, k);
            }, delay, Ease.OutQuint, () => Settle(rt));
        }

        /// <summary>Hộp thoại hiện ra: phóng từ 0,96 lên 1 (đường cong soft) cùng mờ dần, như hienRa('dialog') của CHEMISTRY.</summary>
        public static void Pop(RectTransform rt, float from = 0.96f, float duration = 0.38f, float delay = 0f)
        {
            var g = Group(rt);
            g.alpha = 0f;
            rt.localScale = new Vector3(from, from, 1f);
            To(rt, "nở", duration, k =>
            {
                g.alpha = Mathf.Clamp01(k * 1.6f);
                float s = Mathf.LerpUnclamped(from, 1f, k);
                rt.localScale = new Vector3(s, s, 1f);
            }, delay, Ease.OutQuint);
        }

        /// <summary>Rời màn: mờ đi và chìm nhẹ xuống (đường cong away, ~200 ms), xong thì gọi done.</summary>
        public static void Leave(RectTransform rt, float duration = 0.2f, Action done = null)
        {
            var g = Group(rt);
            float a0 = g.alpha;
            To(rt, "nở", duration, k =>
            {
                g.alpha = Mathf.LerpUnclamped(a0, 0f, k);
                float s = Mathf.LerpUnclamped(1f, 0.985f, k);
                rt.localScale = new Vector3(s, s, 1f);
            }, 0f, Ease.Away, () =>
            {
                rt.localScale = Vector3.one;
                done?.Invoke();
            });
        }

        public static void Slide(RectTransform rt, Vector2 from, Vector2 to, float duration, float delay = 0f, Ease ease = Ease.OutCubic, Action done = null)
        {
            rt.anchoredPosition = from;
            To(rt, "trượt", duration, k => rt.anchoredPosition = Vector2.LerpUnclamped(from, to, k), delay, ease, done);
        }

        /// <summary>Số chạy dần tới giá trị cuối.</summary>
        public static void Count(Text t, float value, string format, float delay = 0f, float duration = 0.7f)
        {
            // chỉ tạo chuỗi mới khi con số hiện ra thật sự đổi (đỡ cấp phát mỗi khung)
            float step = format.Contains(".") ? 0.1f : 1f;
            int shown = int.MinValue;
            To(t, "số", duration, k =>
            {
                int q = Mathf.RoundToInt(value * k / step);
                if (q == shown) return;
                shown = q;
                t.text = (q * step).ToString(format, System.Globalization.CultureInfo.InvariantCulture);
            }, delay);
        }

        /// <summary>Lắc nhẹ dòng báo lỗi để mắt chú ý tới.</summary>
        public static void Shake(RectTransform rt)
        {
            Vector2 home = Home(rt);
            To(rt, "lắc", 0.32f, k =>
            {
                float d = (1f - k) * 7f * Mathf.Sin(k * Mathf.PI * 5f);
                rt.anchoredPosition = home + new Vector2(d, 0);
            }, 0f, Ease.Linear, () =>
            {
                rt.anchoredPosition = home;
                Settle(rt);
            });
        }

        public static void ColorTo(Graphic g, Color to, float duration = 0.16f)
        {
            Color from = g.color;
            To(g, "màu", duration, k => g.color = Color.LerpUnclamped(from, to, k));
        }

        public static void ScaleTo(Transform t, float to, float duration = 0.16f, Ease ease = Ease.OutCubic)
        {
            float from = t.localScale.x;
            To(t, "cỡ", duration, k =>
            {
                float s = Mathf.LerpUnclamped(from, to, k);
                t.localScale = new Vector3(s, s, 1f);
            }, 0f, ease);
        }
    }
}
