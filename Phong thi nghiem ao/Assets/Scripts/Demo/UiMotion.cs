using System;
using System.Collections;
using UnityEngine;
using Khkt.Environment.Tang2;

namespace Khkt.Demo
{
    /// <summary>
    /// Bộ tiện ích chuyển động giao diện hiện đại (UiMotion):
    ///   • Hiệu ứng mở bảng Popup (Scale 0.95 -> 1.00 kèm Fade Alpha).
    ///   • Hiệu ứng đóng bảng Dismiss (Fade Alpha ra mượt mà).
    ///   • Hỗ trợ đầy đủ cờ ReducedMotion (tự động bỏ scale khi bật giảm chuyển động).
    /// </summary>
    public static class UiMotion
    {
        public static Coroutine FadeIn(MonoBehaviour runner, CanvasGroup cg, float duration = 0.18f, Action onComplete = null)
        {
            if (runner == null || cg == null) { onComplete?.Invoke(); return null; }
            return runner.StartCoroutine(CoFade(cg, cg.alpha, 1f, duration, onComplete));
        }

        public static Coroutine FadeOut(MonoBehaviour runner, CanvasGroup cg, float duration = 0.12f, Action onComplete = null)
        {
            if (runner == null || cg == null) { onComplete?.Invoke(); return null; }
            return runner.StartCoroutine(CoFade(cg, cg.alpha, 0f, duration, onComplete));
        }

        public static Coroutine Popup(MonoBehaviour runner, RectTransform rt, CanvasGroup cg = null, float duration = 0.20f, Action onComplete = null)
        {
            if (runner == null || rt == null) { onComplete?.Invoke(); return null; }
            return runner.StartCoroutine(CoPopup(rt, cg, duration, onComplete));
        }

        public static Coroutine Dismiss(MonoBehaviour runner, RectTransform rt, CanvasGroup cg = null, float duration = 0.14f, Action onComplete = null)
        {
            if (runner == null || rt == null) { onComplete?.Invoke(); return null; }
            return runner.StartCoroutine(CoDismiss(rt, cg, duration, onComplete));
        }

        static IEnumerator CoFade(CanvasGroup cg, float from, float to, float duration, Action onComplete)
        {
            float elapsed = 0f;
            float dur = ReducedMotion.IsEnabled ? 0.06f : duration;
            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                float ease = 1f - Mathf.Pow(1f - t, 3f);
                cg.alpha = Mathf.Lerp(from, to, ease);
                yield return null;
            }
            cg.alpha = to;
            onComplete?.Invoke();
        }

        static IEnumerator CoPopup(RectTransform rt, CanvasGroup cg, float duration, Action onComplete)
        {
            bool reduced = ReducedMotion.IsEnabled;
            float dur = reduced ? 0.08f : duration;
            Vector3 startScale = reduced ? Vector3.one : new Vector3(0.94f, 0.94f, 1f);
            Vector3 targetScale = Vector3.one;
            float startAlpha = cg != null ? cg.alpha : 1f;

            rt.localScale = startScale;
            float elapsed = 0f;

            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                // Ease out quint: 1 - (1 - t)^5
                float ease = 1f - Mathf.Pow(1f - t, 4f);

                if (!reduced) rt.localScale = Vector3.LerpUnclamped(startScale, targetScale, ease);
                if (cg != null) cg.alpha = Mathf.Lerp(startAlpha, 1f, ease);

                yield return null;
            }

            rt.localScale = targetScale;
            if (cg != null) cg.alpha = 1f;
            onComplete?.Invoke();
        }

        static IEnumerator CoDismiss(RectTransform rt, CanvasGroup cg, float duration, Action onComplete)
        {
            float dur = duration;
            float startAlpha = cg != null ? cg.alpha : 1f;
            float elapsed = 0f;

            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                float ease = t * t; // Ease in

                if (cg != null) cg.alpha = Mathf.Lerp(startAlpha, 0f, ease);
                yield return null;
            }

            if (cg != null) cg.alpha = 0f;
            onComplete?.Invoke();
        }
    }
}
