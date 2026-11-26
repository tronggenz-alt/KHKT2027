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

        public static Coroutine Popup(MonoBehaviour runner, RectTransform rt, CanvasGroup cg = null, float duration = 0.22f, Action onComplete = null)
        {
            if (runner == null || rt == null || !runner.isActiveAndEnabled) { onComplete?.Invoke(); return null; }
            if (cg == null)
            {
                cg = rt.GetComponent<CanvasGroup>();
                if (cg == null) cg = rt.gameObject.AddComponent<CanvasGroup>();
            }
            return runner.StartCoroutine(CoPopup(rt, cg, duration, onComplete));
        }

        public static Coroutine Dismiss(MonoBehaviour runner, RectTransform rt, CanvasGroup cg = null, float duration = 0.15f, Action onComplete = null)
        {
            if (runner == null || rt == null || !runner.isActiveAndEnabled || Application.isBatchMode || ReducedMotion.IsEnabled)
            {
                if (rt != null) rt.localScale = Vector3.one;
                if (cg != null) cg.alpha = 0f;
                onComplete?.Invoke();
                return null;
            }
            if (cg == null)
            {
                cg = rt.GetComponent<CanvasGroup>();
                if (cg == null) cg = rt.gameObject.AddComponent<CanvasGroup>();
            }
            return runner.StartCoroutine(CoDismiss(rt, cg, duration, onComplete));
        }

        /// <summary>Mở một bảng GameObject bất kỳ với hiệu ứng phóng mở mượt (scale 0.93 -> 1.0 + alpha 0 -> 1).</summary>
        public static Coroutine OpenPanel(MonoBehaviour runner, GameObject panelGo, float duration = 0.22f, Action onComplete = null)
        {
            if (panelGo == null) { onComplete?.Invoke(); return null; }
            panelGo.SetActive(true);
            var rt = panelGo.GetComponent<RectTransform>();
            var cg = panelGo.GetComponent<CanvasGroup>();
            if (cg == null) cg = panelGo.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            if (runner == null || !runner.isActiveAndEnabled || rt == null)
            {
                cg.alpha = 1f;
                if (rt != null) rt.localScale = Vector3.one;
                onComplete?.Invoke();
                return null;
            }
            return runner.StartCoroutine(CoPopup(rt, cg, duration, onComplete));
        }

        /// <summary>Đóng một bảng GameObject với hiệu ứng thu gọn mượt (scale 1.0 -> 0.94 + alpha 1 -> 0) rồi SetActive(false).</summary>
        public static Coroutine ClosePanel(MonoBehaviour runner, GameObject panelGo, float duration = 0.15f, Action onClosed = null)
        {
            if (panelGo == null || !panelGo.activeSelf)
            {
                onClosed?.Invoke();
                return null;
            }
            var rt = panelGo.GetComponent<RectTransform>();
            var cg = panelGo.GetComponent<CanvasGroup>();
            if (runner == null || !runner.isActiveAndEnabled || rt == null || Application.isBatchMode || ReducedMotion.IsEnabled)
            {
                if (rt != null) rt.localScale = Vector3.one;
                if (cg != null) cg.alpha = 1f;
                panelGo.SetActive(false);
                onClosed?.Invoke();
                return null;
            }
            if (cg == null) cg = panelGo.AddComponent<CanvasGroup>();
            return runner.StartCoroutine(CoDismiss(rt, cg, duration, () =>
            {
                if (rt != null) rt.localScale = Vector3.one;
                if (cg != null) cg.alpha = 1f;
                if (panelGo != null) panelGo.SetActive(false);
                onClosed?.Invoke();
            }));
        }

        static IEnumerator CoFade(CanvasGroup cg, float from, float to, float duration, Action onComplete)
        {
            float elapsed = 0f;
            float dur = ReducedMotion.IsEnabled ? 0.06f : duration;
            while (elapsed < dur)
            {
                if (cg == null) yield break;
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                float ease = 1f - Mathf.Pow(1f - t, 3f);
                cg.alpha = Mathf.Lerp(from, to, ease);
                yield return null;
            }
            if (cg != null) cg.alpha = to;
            onComplete?.Invoke();
        }

        static IEnumerator CoPopup(RectTransform rt, CanvasGroup cg, float duration, Action onComplete)
        {
            bool reduced = ReducedMotion.IsEnabled;
            float dur = reduced ? 0.08f : duration;
            Vector3 startScale = reduced ? Vector3.one : new Vector3(0.93f, 0.93f, 1f);
            Vector3 targetScale = Vector3.one;
            if (cg != null && cg.alpha > 0.95f) cg.alpha = 0f;
            float startAlpha = cg != null ? cg.alpha : 0f;

            if (rt == null) yield break;
            rt.localScale = startScale;
            float elapsed = 0f;

            while (elapsed < dur)
            {
                if (rt == null) yield break;
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                // Đường cong OutBackSoft mượt mà: hơi nở nhẹ tinh tế rồi đậu êm
                const float cs = 0.68f;
                float u = t - 1f;
                float easeScale = 1f + (cs + 1f) * u * u * u + cs * u * u;
                float easeAlpha = 1f - Mathf.Pow(1f - t, 3f);

                if (!reduced) rt.localScale = Vector3.LerpUnclamped(startScale, targetScale, easeScale);
                if (cg != null) cg.alpha = Mathf.Lerp(startAlpha, 1f, Mathf.Clamp01(easeAlpha * 1.35f));

                yield return null;
            }

            if (rt != null) rt.localScale = targetScale;
            if (cg != null) cg.alpha = 1f;
            onComplete?.Invoke();
        }

        static IEnumerator CoDismiss(RectTransform rt, CanvasGroup cg, float duration, Action onComplete)
        {
            bool reduced = ReducedMotion.IsEnabled;
            float dur = Mathf.Max(0.05f, duration);
            float startAlpha = cg != null ? cg.alpha : 1f;
            Vector3 startScale = rt != null ? rt.localScale : Vector3.one;
            Vector3 endScale = reduced ? Vector3.one : new Vector3(0.94f, 0.94f, 1f);
            float elapsed = 0f;

            while (elapsed < dur)
            {
                if (rt == null && cg == null) yield break;
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                float ease = t * t; // Ease in

                if (rt != null && !reduced) rt.localScale = Vector3.LerpUnclamped(startScale, endScale, ease);
                if (cg != null) cg.alpha = Mathf.Lerp(startAlpha, 0f, ease);
                yield return null;
            }

            if (rt != null) rt.localScale = Vector3.one;
            if (cg != null) cg.alpha = 0f;
            onComplete?.Invoke();
        }
    }
}
