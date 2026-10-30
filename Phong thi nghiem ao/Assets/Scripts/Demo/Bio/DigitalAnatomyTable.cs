using Khkt.Demo;
using UnityEngine;

namespace Khkt.Demo.Bio
{
    public class DigitalAnatomyTable : MonoBehaviour, IDemoUsable
    {
        [Header("Các lớp giải phẫu BodyParts3D")]
        public GameObject layerSkeleton;
        public GameObject layerMuscles;
        public GameObject layerOrgans;
        public GameObject layerSkin;

        [Header("Hoạt cảnh tim & phổi")]
        public Transform heartTransform;
        public Transform lungsTransform;

        [Header("Hiển thị thông tin")]
        public TextMesh layerText;

        public enum AnatomyLayerMode
        {
            TatCa,
            Xuong,
            Co,
            NoiTang,
            Da
        }

        public AnatomyLayerMode currentMode = AnatomyLayerMode.TatCa;

        const float HeartRateBpm = 72f;
        const float BreathRateBpm = 16f;

        void Start()
        {
            UpdateLayers();
        }

        void Update()
        {
            // Nhịp tim 72 bpm = 1.2 Hz
            if (heartTransform != null)
            {
                float heartBeat = Mathf.Sin(Time.time * (HeartRateBpm / 60f) * Mathf.PI * 2f);
                float heartScale = 1f + 0.08f * Mathf.Max(0f, heartBeat);
                heartTransform.localScale = new Vector3(heartScale, heartScale, heartScale);
            }

            // Nhịp thở 16 bpm = 0.267 Hz
            if (lungsTransform != null)
            {
                float breath = Mathf.Sin(Time.time * (BreathRateBpm / 60f) * Mathf.PI * 2f);
                float lungExpansion = 1f + 0.05f * breath;
                lungsTransform.localScale = new Vector3(lungExpansion, 1f, lungExpansion);
            }
        }

        public string UseHint => $"Bàn giải phẫu 3D (Đang xem: {GetLayerName(currentMode)}). Bấm để đổi lớp.";

        public void Use()
        {
            // Chuyển lớp giải phẫu tuần hoàn
            currentMode = (AnatomyLayerMode)(((int)currentMode + 1) % 5);
            UpdateLayers();
        }

        void UpdateLayers()
        {
            if (layerSkeleton != null) layerSkeleton.SetActive(currentMode == AnatomyLayerMode.TatCa || currentMode == AnatomyLayerMode.Xuong);
            if (layerMuscles != null) layerMuscles.SetActive(currentMode == AnatomyLayerMode.TatCa || currentMode == AnatomyLayerMode.Co);
            if (layerOrgans != null) layerOrgans.SetActive(currentMode == AnatomyLayerMode.TatCa || currentMode == AnatomyLayerMode.NoiTang);
            if (layerSkin != null) layerSkin.SetActive(currentMode == AnatomyLayerMode.TatCa || currentMode == AnatomyLayerMode.Da);

            if (layerText != null)
            {
                layerText.text = $"Lớp: {GetLayerName(currentMode)}\n[CC BY 4.0 DBCLS BodyParts3D]";
            }
        }

        string GetLayerName(AnatomyLayerMode mode)
        {
            switch (mode)
            {
                case AnatomyLayerMode.TatCa: return "Toàn bộ";
                case AnatomyLayerMode.Xuong: return "Bộ xương";
                case AnatomyLayerMode.Co: return "Hệ cơ";
                case AnatomyLayerMode.NoiTang: return "Nội tạng";
                case AnatomyLayerMode.Da: return "Lớp da";
                default: return "Toàn bộ";
            }
        }
    }
}
