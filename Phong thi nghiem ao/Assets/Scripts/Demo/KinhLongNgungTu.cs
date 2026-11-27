using UnityEngine;
using UnityEngine.UI;
using Khkt.Environment.Tang2;

namespace Khkt.Demo
{
    /// <summary>
    /// "Ngưng tụ" (materialize) của Liquid Glass: mỗi lần tấm kính hiện ra, độ khúc xạ, vệt sáng và độ đục lớn dần
    /// trong ~0,38 s như giọt kính đọng lại (shader Khkt/KinhMo đọc mức ngưng tụ từ alpha màu đỉnh).
    /// Dùng CanvasRenderer.SetAlpha nên không đụng tới Image.color mà màn hình tự đặt.
    /// Giảm chuyển động: chỉ mờ dần nhanh (0,15 s).
    /// </summary>
    [DisallowMultipleComponent]
    public class KinhLongNgungTu : MonoBehaviour
    {
        Graphic g;
        float t = 1f;
        float dur;

        public static KinhLongNgungTu Attach(Graphic graphic)
        {
            if (graphic == null) return null;
            var c = graphic.GetComponent<KinhLongNgungTu>();
            if (c == null) c = graphic.gameObject.AddComponent<KinhLongNgungTu>();
            c.g = graphic;
            return c;
        }

        void OnEnable()
        {
            if (g == null) g = GetComponent<Graphic>();
            if (g == null) return;
            dur = ReducedMotion.IsEnabled ? 0.15f : KinhLong.NgungTuGiay;
            t = 0f;
            g.canvasRenderer.SetAlpha(0f);
        }

        void OnDisable()
        {
            if (g != null) g.canvasRenderer.SetAlpha(1f);
            t = 1f;
        }

        void LateUpdate()
        {
            if (g == null || t >= 1f) return;
            t = Mathf.Min(1f, t + Time.unscaledDeltaTime / Mathf.Max(dur, 0.01f));
            // đường cong ngưng tụ: bắt nhanh rồi lắng chậm (ease-out mũ 5) — giống "materialize" của visionOS / iOS 26
            float e = 1f - Mathf.Pow(1f - t, 5f);
            g.canvasRenderer.SetAlpha(e);
        }
    }
}
