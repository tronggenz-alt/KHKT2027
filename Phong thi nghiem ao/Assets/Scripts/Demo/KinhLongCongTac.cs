using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Khkt.Environment.Tang2;

namespace Khkt.Demo
{
    /// <summary>
    /// Công tắc kiểu Liquid Glass (Apple: "the knob transforms into Liquid Glass during interaction"):
    ///   • Chạm vào: núm phình thành viên kính trong suốt (rộng ~1,55×), lòng núm thấy được rãnh phía dưới.
    ///   • Gạt: núm trượt sang bên kia bằng lò xo có nảy, kéo dài theo vận tốc rồi co về hình tròn.
    ///   • Nhả: kính "đặc" lại thành núm trắng.
    ///   • Giảm chuyển động: nhảy thẳng tới vị trí mới, không phình, không nảy.
    /// GlassKit.BuildToggle đặt vị trí núm; thành phần này chỉ làm phần chuyển động ở giữa.
    /// </summary>
    [DisallowMultipleComponent]
    public class KinhLongCongTac : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public Toggle toggle;
        public RectTransform knob;
        public Image knobImage;
        public float travel = 12f;
        public Vector2 knobSize = new Vector2(22f, 22f);

        Image glass;
        LoXo x;
        LoXo kinh = new LoXo(0f);   // 0 = núm đặc, 1 = viên kính
        bool down;
        bool init;
        bool daNghi;

        public static KinhLongCongTac Attach(Toggle t, RectTransform knob, Image knobImage, float travel)
        {
            var c = t.gameObject.GetComponent<KinhLongCongTac>();
            if (c == null) c = t.gameObject.AddComponent<KinhLongCongTac>();
            c.toggle = t;
            c.knob = knob;
            c.knobImage = knobImage;
            c.travel = travel;
            c.knobSize = knob.sizeDelta;
            c.Snap();
            return c;
        }

        void EnsureGlass()
        {
            if (glass != null || knob == null) return;
            var go = new GameObject("Kính núm", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(knob, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            glass = go.GetComponent<Image>();
            glass.sprite = KinhLong.NumKinh;
            glass.type = Image.Type.Sliced;
            glass.raycastTarget = false;
            glass.color = Color.white;
            glass.canvasRenderer.SetAlpha(0f);
        }

        float Target => toggle != null && toggle.isOn ? travel : -travel;

        void Snap()
        {
            x.Snap(Target);
            kinh.Snap(0f);
            init = true;
            daNghi = false;
            Apply();
        }

        void OnEnable() { if (init) Snap(); }

        public void OnPointerDown(PointerEventData e)
        {
            if (toggle != null && !toggle.IsInteractable()) return;
            down = true;
        }

        public void OnPointerUp(PointerEventData e) => down = false;
        public void OnPointerExit(PointerEventData e) => down = false;

        void LateUpdate()
        {
            if (knob == null || toggle == null) return;
            if (!init) Snap();
            float dt = Time.unscaledDeltaTime;
            x.Target = Target;
            if (ReducedMotion.IsEnabled)
            {
                x.Snap(x.Target);
                kinh.Snap(0f);
                Apply();
                return;
            }
            bool moving = Mathf.Abs(x.Target - x.Value) > 0.4f || Mathf.Abs(x.Velocity) > 6f;
            kinh.Target = (down || moving) ? 1f : 0f;
            bool resting = x.Resting(0.01f) && kinh.Target <= 0f && kinh.Value < 0.002f;
            if (resting && daNghi) return;
            x.Step(dt, 0.36f, 0.66f);
            kinh.Step(dt, kinh.Target > kinh.Value ? 0.16f : 0.32f, 0.85f);
            if (resting) { x.Snap(x.Target); kinh.Snap(0f); }
            Apply();
            daNghi = resting;
        }

        void Apply()
        {
            if (knob == null) return;
            float g = Mathf.Clamp01(kinh.Value);
            float stretch = Mathf.Clamp(Mathf.Abs(x.Velocity) / 600f, 0f, 0.35f);
            float w = knobSize.x * (1f + 0.55f * g + stretch);
            float h = knobSize.y * (1f + 0.12f * g - stretch * 0.25f);
            knob.sizeDelta = new Vector2(w, h);
            // giữ núm trong rãnh khi phình
            float lim = travel + (knobSize.x - w) * 0.5f + 2f * g;
            knob.anchoredPosition = new Vector2(Mathf.Clamp(x.Value, -Mathf.Max(lim, 0f), Mathf.Max(lim, 0f)), 0f);
            float m = KinhLong.NangMultiplier(h);
            if (knobImage != null)
            {
                knobImage.sprite = KinhLong.VienNang;
                knobImage.type = Image.Type.Sliced;
                knobImage.preserveAspect = false;
                knobImage.pixelsPerUnitMultiplier = m;
                knobImage.canvasRenderer.SetAlpha(1f - 0.82f * g);
            }
            if (g > 0.01f) EnsureGlass();
            if (glass != null)
            {
                glass.type = Image.Type.Sliced;
                glass.pixelsPerUnitMultiplier = m;
                glass.canvasRenderer.SetAlpha(g);
            }
        }
    }
}
