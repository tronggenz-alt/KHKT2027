using System;
using UnityEngine;

namespace Khkt.Interaction
{
    /// <summary>
    /// Chỗ đặt dụng cụ có định vị sẵn: kẹp buret trên giá sắt, ô dưới đầu buret (trên gạch men)...
    /// Khi tay đang cầm vật hợp lệ, vòng sáng hiện lên ở đây để học sinh biết đặt vào đâu.
    /// </summary>
    public class PlaceSocket : MonoBehaviour
    {
        public string label = "Chỗ đặt";
        public string[] accepts = { "binh_non" };
        [Tooltip("Vòng sáng báo chỗ đặt (bật khi đang cầm vật hợp lệ)")]
        public GameObject marker;
        [Tooltip("Vùng trỏ chuột (trigger)")]
        public Collider zone;
        [Tooltip("Kẹp buret: đặt buret vào đây là buret được lắp trên giá")]
        public bool mountsBuret;

        public Grabbable Occupant { get; private set; }
        public event Action<Grabbable> Changed;

        public bool Accepts(Grabbable g) => g != null && Occupant == null && Array.IndexOf(accepts, g.kind) >= 0;

        /// <summary>Đặt vật vào: gốc vật trùng gốc ổ, hướng theo ổ.</summary>
        public void Put(Grabbable g)
        {
            Occupant = g;
            g.transform.SetPositionAndRotation(transform.position, transform.rotation);
            g.SetSocket(this);
            var b = g.GetComponent<BuretStopcock>();
            if (b != null) b.mounted = mountsBuret;
            Changed?.Invoke(g);
        }

        public void Release(Grabbable g)
        {
            if (Occupant != g) return;
            Occupant = null;
            var b = g.GetComponent<BuretStopcock>();
            if (b != null) b.mounted = false;
            Changed?.Invoke(null);
        }

        /// <summary>Hiện vòng sáng; hovered thì sáng hơn.</summary>
        public void Highlight(bool show, bool hovered)
        {
            if (zone != null) zone.enabled = show;
            if (marker == null) return;
            if (marker.activeSelf != show) marker.SetActive(show);
            if (show) marker.transform.localScale = Vector3.one * (hovered ? 1.15f + 0.05f * Mathf.Sin(Time.time * 8f) : 1f);
        }
    }
}
