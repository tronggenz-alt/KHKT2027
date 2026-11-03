using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Khkt.Environment.Tang2;
using Khkt.Interaction.Bio;

namespace Khkt.Demo
{
    /// <summary>
    /// Bộ hiệu ứng nút bấm cảm ứng iPad chuẩn Apple HIG & Material Design 3 (Giai đoạn 3):
    ///   • Phản hồi hình ảnh: Lớp phủ HighlightOverlay (raycastTarget = false), co giãn nhẹ 0.95x bằng coroutine mượt mà.
    ///   • Phản hồi âm thanh: Click nhẹ từ BioFeedback (có thể tắt trong Cài đặt).
    ///   • Luật an toàn tuyệt đối: Nút giữ luôn nhả khi nhấc ngón, trượt ra ngoài (PointerExit),
    ///     khi SetActive(false), khi mất focus, hoặc khi mở bảng khác.
    ///   • Tương thích hoàn toàn với ReducedMotion (tắt scale, giữ chuyển đổi alpha).
    /// </summary>
    public class NutHieuUng : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerEnterHandler
    {
        public const string SoundPrefKey = "KHKT_AM_THANH_GIAO_DIEN";

        public static bool SoundEnabled
        {
            get => PlayerPrefs.GetInt(SoundPrefKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(SoundPrefKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        [Header("Cấu hình hiệu ứng")]
        public float pressedScale = 0.95f;
        public float animDuration = 0.08f;
        public bool isHoldButton = false;
        public Action onHoldReleaseAction;

        RectTransform rectTransform;
        Image highlightOverlay;
        Vector3 baseScale = Vector3.one;
        Coroutine currentAnim;
        bool isPointerDown = false;

        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            if (rectTransform != null) baseScale = rectTransform.localScale;
            EnsureHighlightOverlay();
        }

        void EnsureHighlightOverlay()
        {
            if (highlightOverlay != null) return;
            var t = transform.Find("HighlightOverlay");
            if (t != null)
            {
                highlightOverlay = t.GetComponent<Image>();
                return;
            }

            var go = new GameObject("HighlightOverlay", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            highlightOverlay = go.AddComponent<Image>();
            highlightOverlay.sprite = GlassKit.Solid;
            highlightOverlay.type = Image.Type.Sliced;
            highlightOverlay.color = new Color(1f, 1f, 1f, 0f);
            highlightOverlay.raycastTarget = false; // Tuyệt đối không cản trở raycast
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            isPointerDown = true;
            PlayPressAnimation(true);
            if (SoundEnabled)
            {
                if (isHoldButton) BioFeedback.PlayButton();
                else BioFeedback.PlayClick();
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (isPointerDown)
            {
                isPointerDown = false;
                PlayPressAnimation(false);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (isPointerDown)
            {
                isPointerDown = false;
                PlayPressAnimation(false);
                ReleaseHold();
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            // Tránh kích hoạt lại khi rê chuột nếu ngón tay đã nhấc
        }

        void OnDisable()
        {
            ResetImmediate();
            ReleaseHold();
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                ResetImmediate();
                ReleaseHold();
            }
        }

        void ReleaseHold()
        {
            if (isHoldButton && onHoldReleaseAction != null)
            {
                try { onHoldReleaseAction.Invoke(); }
                catch (Exception ex) { Debug.LogWarning($"[NutHieuUng] Lỗi nhả nút giữ: {ex.Message}"); }
            }
        }

        void ResetImmediate()
        {
            isPointerDown = false;
            if (currentAnim != null) { StopCoroutine(currentAnim); currentAnim = null; }
            if (rectTransform != null) rectTransform.localScale = baseScale;
            if (highlightOverlay != null)
            {
                var c = highlightOverlay.color;
                c.a = 0f;
                highlightOverlay.color = c;
            }
        }

        void PlayPressAnimation(bool down)
        {
            if (currentAnim != null) StopCoroutine(currentAnim);
            currentAnim = StartCoroutine(AnimateScaleAndAlpha(down));
        }

        IEnumerator AnimateScaleAndAlpha(bool down)
        {
            bool reduced = ReducedMotion.IsEnabled;
            float targetScaleFactor = (down && !reduced) ? pressedScale : 1.0f;
            Vector3 targetScale = baseScale * targetScaleFactor;
            float targetAlpha = down ? 0.18f : 0f;

            Vector3 startScale = rectTransform != null ? rectTransform.localScale : baseScale;
            float startAlpha = highlightOverlay != null ? highlightOverlay.color.a : 0f;

            float elapsed = 0f;
            float dur = animDuration > 0.01f ? animDuration : 0.08f;

            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                // Cubic Ease Out: f(t) = 1 - (1 - t)^3
                float ease = 1f - Mathf.Pow(1f - t, 3f);

                if (rectTransform != null && !reduced)
                    rectTransform.localScale = Vector3.LerpUnclamped(startScale, targetScale, ease);

                if (highlightOverlay != null)
                {
                    Color c = highlightOverlay.color;
                    c.a = Mathf.Lerp(startAlpha, targetAlpha, ease);
                    highlightOverlay.color = c;
                }

                yield return null;
            }

            if (rectTransform != null && !reduced) rectTransform.localScale = targetScale;
            if (highlightOverlay != null)
            {
                Color c = highlightOverlay.color;
                c.a = targetAlpha;
                highlightOverlay.color = c;
            }
            currentAnim = null;
        }

        /// <summary>
        /// Gắn nhanh hiệu ứng NutHieuUng vào bất kỳ GameObject nút bấm nào.
        /// </summary>
        public static NutHieuUng Attach(GameObject go, bool isHold = false, Action onRelease = null)
        {
            if (go == null) return null;
            var eff = go.GetComponent<NutHieuUng>();
            if (eff == null) eff = go.AddComponent<NutHieuUng>();
            eff.isHoldButton = isHold;
            if (onRelease != null) eff.onHoldReleaseAction = onRelease;
            return eff;
        }
    }
}
