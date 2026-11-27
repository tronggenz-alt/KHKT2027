using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Khkt.Environment.Tang2;

namespace Khkt.Demo
{
    /// <summary>
    /// Thanh trượt kiểu Liquid Glass: khi chạm/kéo, núm phình thành viên kính trong (rộng ~1,7×, cao ~1,3×)
    /// để thấy rãnh và phần đã kéo bên dưới; kéo nhanh thì núm kéo dài theo hướng chạy; nhả thì đặc lại và nảy nhẹ.
    /// Giảm chuyển động: giữ núm nguyên hình.
    /// </summary>
    [DisallowMultipleComponent]
    public class KinhLongNumTruot : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public Slider slider;
        public RectTransform handle;
        public Image ring;
        public Image core;

        Image glass;
        LoXo kinh = new LoXo(0f);
        LoXo keo = new LoXo(0f);
        float lastX;
        bool down;
        bool daNghi;
        Vector2 baseSize;

        public static KinhLongNumTruot Attach(Slider s, RectTransform handle, Image ring, Image core)
        {
            var c = s.gameObject.GetComponent<KinhLongNumTruot>();
            if (c == null) c = s.gameObject.AddComponent<KinhLongNumTruot>();
            c.slider = s;
            c.handle = handle;
            c.ring = ring;
            c.core = core;
            c.baseSize = handle.sizeDelta;
            return c;
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (slider != null && !slider.IsInteractable()) return;
            down = true;
            daNghi = false;
        }

        public void OnPointerUp(PointerEventData e) { down = false; daNghi = false; }

        void OnDisable()
        {
            down = false;
            kinh.Snap(0f);
            keo.Snap(0f);
            Apply(0f, 0f);
        }

        void LateUpdate()
        {
            if (handle == null) return;
            if (ReducedMotion.IsEnabled) { if (!daNghi) { kinh.Snap(0f); Apply(0f, 0f); daNghi = true; } return; }
            float dt = Time.unscaledDeltaTime;
            float hx = handle.position.x;
            float v = dt > 0f ? (hx - lastX) / dt : 0f;
            lastX = hx;
            kinh.Target = down ? 1f : 0f;
            keo.Target = down ? Mathf.Clamp(Mathf.Abs(v) / 2400f, 0f, 0.3f) : 0f;
            bool resting = !down && kinh.Value < 0.002f && keo.Resting(0.002f);
            if (resting && daNghi) return;
            kinh.Step(dt, down ? 0.18f : KinhLong.NhaResponse, down ? 0.85f : KinhLong.NhaDamping);
            keo.Step(dt, 0.2f, 0.9f);
            if (resting) { kinh.Snap(0f); keo.Snap(0f); }
            Apply(kinh.Value, keo.Value);
            daNghi = resting;
        }

        void Apply(float g, float stretch)
        {
            if (handle == null) return;
            float gc = Mathf.Max(g, 0f);
            // vượt 1 một chút khi nảy lúc nhả: núm hơi to rồi về — nhìn như giọt nước
            float w = baseSize.x * (1f + 0.7f * gc + stretch);
            float h = baseSize.y * (1f + 0.3f * gc - stretch * 0.3f);
            handle.sizeDelta = new Vector2(w, h);
            float m = KinhLong.NangMultiplier(h);
            float solid = 1f - 0.85f * Mathf.Clamp01(g);
            if (ring != null)
            {
                ring.sprite = KinhLong.VienNang;
                ring.type = Image.Type.Sliced;
                ring.preserveAspect = false;
                ring.pixelsPerUnitMultiplier = m;
                ring.canvasRenderer.SetAlpha(solid);
            }
            if (core != null)
            {
                var crt = core.rectTransform;
                crt.sizeDelta = new Vector2(w - 8f, h - 8f);
                core.sprite = KinhLong.VienNang;
                core.type = Image.Type.Sliced;
                core.preserveAspect = false;
                core.pixelsPerUnitMultiplier = KinhLong.NangMultiplier(h - 8f);
                core.canvasRenderer.SetAlpha(solid);
            }
            if (g > 0.01f && glass == null)
            {
                var go = new GameObject("Kính núm", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(handle, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                glass = go.GetComponent<Image>();
                glass.sprite = KinhLong.NumKinh;
                glass.type = Image.Type.Sliced;
                glass.raycastTarget = false;
            }
            if (glass != null)
            {
                glass.pixelsPerUnitMultiplier = m;
                glass.canvasRenderer.SetAlpha(Mathf.Clamp01(g));
            }
        }
    }
}
