using UnityEngine;

namespace Khkt.Demo
{
    /// <summary>
    /// Đèn soi cục bộ cho bàn thí nghiệm:
    /// Chiếu vùng sáng ấm (warm light spot) chân thực khi người dùng lại gần (< 2.5m),
    /// tự tắt khi đi xa để tối ưu tuyệt đối hiệu năng.
    /// </summary>
    public class TaskLampSpot : MonoBehaviour
    {
        public Light spotLight;
        public float maxDistance = 2.5f;
        Transform camTransform;
        float checkTimer;

        void Start()
        {
            if (spotLight == null) spotLight = GetComponentInChildren<Light>();
            FindCamera();
            if (spotLight != null) spotLight.enabled = false;
        }

        void FindCamera()
        {
            var cam = Camera.main;
            if (cam != null) camTransform = cam.transform;
            else
            {
                var v = FindAnyObjectByType<Khkt.Environment.DesktopViewer>();
                if (v != null && v.viewCamera != null) camTransform = v.viewCamera.transform;
            }
        }

        void Update()
        {
            checkTimer += Time.deltaTime;
            if (checkTimer < 0.1f) return;
            checkTimer = 0f;

            if (camTransform == null)
            {
                FindCamera();
                if (camTransform == null) return;
            }

            float dist = Vector3.Distance(camTransform.position, transform.position);
            bool active = dist < maxDistance;
            if (spotLight != null && spotLight.enabled != active)
            {
                spotLight.enabled = active;
            }
        }
    }
}

