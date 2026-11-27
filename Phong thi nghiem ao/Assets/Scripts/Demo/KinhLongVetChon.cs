using UnityEngine;
using UnityEngine.UI;
using Khkt.Environment.Tang2;

namespace Khkt.Demo
{
    /// <summary>
    /// Vệt chọn kính lỏng của nhóm chọn / thanh tab (như tab bar iOS 26): khi đổi mục, vệt trượt bằng lò xo,
    /// kéo dài theo hướng chạy rồi thắt về như giọt nước; lúc đang bay vệt hóa kính trong và sáng hơn.
    /// </summary>
    [DisallowMultipleComponent]
    public class KinhLongVetChon : MonoBehaviour
    {
        RectTransform rt;
        Image anh;
        LoXo2 vt, co;
        bool coViTri;

        public static KinhLongVetChon Attach(RectTransform glider)
        {
            var c = glider.GetComponent<KinhLongVetChon>() ?? glider.gameObject.AddComponent<KinhLongVetChon>();
            c.rt = glider;
            return c;
        }

        /// <summary>Đặt đích; snap = nhảy thẳng (lần đầu hoặc đang ẩn).</summary>
        public void Toi(Vector2 pos, Vector2 size, bool snap)
        {
            if (rt == null) rt = (RectTransform)transform;
            if (snap || !coViTri || ReducedMotion.IsEnabled)
            {
                vt.Snap(pos); co.Snap(size); coViTri = true;
                rt.anchoredPosition = pos; rt.sizeDelta = size;
                return;
            }
            vt.Target = pos; co.Target = size;
            enabled = true;
        }

        void LateUpdate()
        {
            if (rt == null || !coViTri) return;
            float dt = Time.unscaledDeltaTime;
            Vector2 p = vt.Step(dt, KinhLong.TruotResponse, KinhLong.TruotDamping);
            Vector2 s = co.Step(dt, KinhLong.TruotResponse, KinhLong.TruotDamping);
            Vector2 v = vt.Velocity;
            float sp = v.magnitude;
            float st = Mathf.Clamp(sp / 2000f, 0f, 0.35f);
            Vector2 d = sp > 1f ? v / sp : Vector2.right;
            Vector2 sz = new Vector2(
                s.x * (1f + st * Mathf.Abs(d.x)) * (1f - st * 0.3f * Mathf.Abs(d.y)),
                s.y * (1f + st * Mathf.Abs(d.y)) * (1f - st * 0.3f * Mathf.Abs(d.x)));
            // giữ tâm khi kéo dài (pivot ở góc trên trái)
            rt.anchoredPosition = p - new Vector2((sz.x - s.x) * 0.5f, -(sz.y - s.y) * 0.5f);
            rt.sizeDelta = sz;

            float bay = Mathf.Clamp01(sp / 700f);
            if (anh == null && bay > 0.02f)
            {
                var go = new GameObject("Ánh kính", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(rt, false);
                var a = (RectTransform)go.transform;
                a.anchorMin = Vector2.zero; a.anchorMax = Vector2.one;
                a.offsetMin = a.offsetMax = Vector2.zero;
                anh = go.GetComponent<Image>();
                anh.sprite = KinhLong.NumKinh;
                anh.type = Image.Type.Sliced;
                anh.raycastTarget = false;
            }
            if (anh != null)
            {
                anh.pixelsPerUnitMultiplier = KinhLong.NangMultiplier(sz.y) * 0.8f;
                anh.color = new Color(1f, 1f, 1f, 0.15f + 0.6f * bay);
                anh.enabled = bay > 0.01f;
            }
            if (vt.Resting(0.05f) && co.Resting(0.05f))
            {
                rt.anchoredPosition = vt.Target; rt.sizeDelta = co.Target;
                if (anh != null) anh.enabled = false;
                enabled = false;
            }
        }
    }
}
