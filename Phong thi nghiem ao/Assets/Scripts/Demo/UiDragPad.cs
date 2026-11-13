using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Khkt.Demo
{
    /// <summary>Vùng kéo và cuộn trên giao diện: báo độ dời (theo đơn vị canvas) và số nấc cuộn cho màn hình chứa nó.</summary>
    public class UiDragPad : MonoBehaviour, IDragHandler, IScrollHandler, IPointerDownHandler
    {
        public Action<Vector2> onDrag;
        public Action<float> onScroll;
        public Action onDown;

        float scale = 1f;

        void OnEnable()
        {
            var c = GetComponentInParent<Canvas>();
            scale = c != null ? Mathf.Max(0.01f, c.rootCanvas.scaleFactor) : 1f;
        }

        public void OnPointerDown(PointerEventData e) => onDown?.Invoke();

        public void OnDrag(PointerEventData e) => onDrag?.Invoke(e.delta / scale);

        public void OnScroll(PointerEventData e)
        {
            if (Mathf.Abs(e.scrollDelta.y) > 0.01f) onScroll?.Invoke(Mathf.Sign(e.scrollDelta.y));
        }
    }
}
