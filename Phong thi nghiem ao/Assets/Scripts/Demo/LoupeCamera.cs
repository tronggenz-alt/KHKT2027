using Khkt.Interaction;
using UnityEngine;

namespace Khkt.Demo
{
    /// <summary>
    /// Kính lúp nhìn vạch buret. Camera phụ đặt TRÊN ĐƯỜNG THẲNG từ mắt người dùng tới mặt khum,
    /// sát buret, nên hình phóng to có đúng sai số thị sai mà mắt đang gặp: mắt cao thì vạch
    /// "trôi" lên so với mặt khum. Học sinh phải hạ/nâng mắt cho ngang vạch, như ngoài đời.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class LoupeCamera : MonoBehaviour
    {
        public MeniscusReading reading;
        public Transform eye;
        [Tooltip("Khoảng cách từ camera kính lúp tới trục buret (m)")]
        public float distance = 0.09f;
        [Tooltip("Chỉ bật khi mắt cách buret gần hơn (m)")]
        public float activeWithinM = 1.8f;
        public int resolution = 384;

        public RenderTexture Texture { get; private set; }
        public bool Active => cam != null && cam.enabled;

        Camera cam;

        void Awake()
        {
            cam = GetComponent<Camera>();
            cam.fieldOfView = 14f;
            cam.nearClipPlane = 0.004f;
            cam.farClipPlane = 4f;
            cam.depth = -10;
            Texture = new RenderTexture(resolution, resolution, 24) { name = "KinhLup" };
            cam.targetTexture = Texture;
        }

        void OnDestroy()
        {
            if (Texture != null) Texture.Release();
        }

        void LateUpdate()
        {
            if (reading == null || eye == null) return;
            Vector3 m = reading.MeniscusPosition;
            Vector3 d = m - eye.position;
            cam.enabled = d.magnitude < activeWithinM;
            if (!cam.enabled) return;
            Vector3 dir = d.normalized;
            transform.SetPositionAndRotation(m - dir * distance, Quaternion.LookRotation(dir, Vector3.up));
        }
    }
}
