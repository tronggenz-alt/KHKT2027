using System.Collections.Generic;
using Khkt.Demo;
using UnityEngine;

namespace Khkt.Interaction
{
    /// <summary>
    /// Nguồn nhiệt (bếp điện, đèn cồn, đèn Bunsen, bếp bọc): vật chứa có đáy nằm trong vùng nóng thì được
    /// cấp nhiệt. Kiểm tra theo khoảng cách (không dùng va chạm) để cả vật đang cầm hơ trên lửa cũng nóng.
    /// Bật/tắt theo ToggleEffect cùng vật (nếu có).
    /// </summary>
    public class HeatSource : MonoBehaviour
    {
        public float powerW = 400f;
        [Tooltip("Tâm vùng nóng (gốc của vật chứa phải nằm trong hộp này)")]
        public Transform zone;
        public Vector3 zoneSize = new Vector3(0.2f, 0.08f, 0.2f);
        public ToggleEffect toggle;
        public bool alwaysOn;

        public bool On => alwaysOn || (toggle != null && toggle.isOn);

        static readonly List<LabContainer> all = new List<LabContainer>();
        static float lastScan = -10f;

        void Update()
        {
            if (!On) return;
            if (Time.time - lastScan > 2f)
            {
                lastScan = Time.time;
                all.Clear();
                foreach (var c in Khkt.Rendering.ZoneRoot.FindAll<LabContainer>()) if (c.reactive) all.Add(c);
            }
            Transform z = zone != null ? zone : transform;
            foreach (var c in all)
            {
                if (c == null) continue;
                Vector3 local = z.InverseTransformPoint(c.transform.position);
                if (Mathf.Abs(local.x) <= zoneSize.x / 2 && Mathf.Abs(local.y) <= zoneSize.y / 2 && Mathf.Abs(local.z) <= zoneSize.z / 2)
                    c.HeatInputW += powerW;
            }
        }

        void OnDrawGizmosSelected()
        {
            Transform z = zone != null ? zone : transform;
            Gizmos.matrix = z.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.6f);
            Gizmos.DrawWireCube(Vector3.zero, zoneSize);
        }
    }
}
