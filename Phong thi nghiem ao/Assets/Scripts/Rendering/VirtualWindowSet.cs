using UnityEngine;

namespace Khkt.Rendering
{
    /// <summary>
    /// Bật cửa sổ ảo khi người xem ở trong nhà: các tấm cửa sổ ảo (shader Khkt/CuaSoAo, cảnh ngoài chụp sẵn) hiện ra ngoài khung
    /// kính, còn hiên và sân thật (cây, cột cờ, chậu cây, đèn hiên) tắt hẳn khỏi khâu vẽ và đổ bóng. Ra gần cửa hiên (cửa kính
    /// trong nhìn thẳng ra ngoài, có thể bước ra) thì bật lại cảnh thật, tắt cửa sổ ảo.
    /// </summary>
    public class VirtualWindowSet : MonoBehaviour
    {
        public Transform viewer;
        [Tooltip("Nhóm các tấm cửa sổ ảo")]
        public GameObject windows;
        [Tooltip("Hiên, sân, cây ngoài trời: tắt khi đang dùng cửa sổ ảo")]
        public GameObject[] outdoorRoots;
        [Tooltip("Tâm cửa ra hiên")]
        public Vector3 balconyDoor;
        public float nearDoor = 4.5f;

        bool? inside;
        float next;

        /// <summary>Đang dùng cửa sổ ảo (cảnh ngoài thật đang tắt).</summary>
        public static bool Active { get; private set; }

        void Update()
        {
            if (viewer == null || Time.unscaledTime < next) return;
            next = Time.unscaledTime + 0.2f;
            Vector3 d = viewer.position - balconyDoor;
            bool now = ZoneMap.At(viewer.position) >= 0 && d.x * d.x + d.z * d.z > nearDoor * nearDoor;
            if (inside == now) return;
            inside = now;
            Active = now;
            if (windows != null) windows.SetActive(now);
            foreach (var r in outdoorRoots) if (r != null) r.SetActive(!now);
        }

        void OnDisable()
        {
            if (inside == true)
            {
                if (windows != null) windows.SetActive(false);
                foreach (var r in outdoorRoots) if (r != null) r.SetActive(true);
            }
            inside = null;
            Active = false;
        }
    }
}
