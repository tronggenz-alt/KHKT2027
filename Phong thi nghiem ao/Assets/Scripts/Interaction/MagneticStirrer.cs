using System.Collections.Generic;
using Khkt.Demo;
using UnityEngine;

namespace Khkt.Interaction
{
    /// <summary>Máy khuấy từ: vật chứa đặt trên mặt máy được khuấy đều (con khuấy quay) khi máy bật.</summary>
    public class MagneticStirrer : MonoBehaviour
    {
        public Transform plate;
        public Vector3 zoneSize = new Vector3(0.18f, 0.08f, 0.18f);
        public ToggleEffect toggle;
        [Range(0, 1)] public float speed = 0.8f;
        public Transform stirBarVisual;

        readonly List<LabContainer> nearby = new List<LabContainer>();
        float lastScan = -10f;

        void Update()
        {
            if (toggle == null || !toggle.isOn) return;
            Transform z = plate != null ? plate : transform;
            if (Time.time - lastScan > 1f)
            {
                lastScan = Time.time;
                nearby.Clear();
                foreach (var c in Khkt.Rendering.ZoneRoot.FindAll<LabContainer>())
                    if (c.reactive && Vector3.Distance(c.transform.position, z.position) < 1.5f) nearby.Add(c);
            }
            foreach (var c in nearby)
            {
                if (c == null) continue;
                Vector3 local = z.InverseTransformPoint(c.transform.position);
                if (Mathf.Abs(local.x) <= zoneSize.x / 2 && Mathf.Abs(local.y) <= zoneSize.y / 2 && Mathf.Abs(local.z) <= zoneSize.z / 2)
                    c.ExternalStir = speed;
            }
            if (stirBarVisual != null) stirBarVisual.Rotate(0, 720f * speed * Time.deltaTime, 0, Space.Self);
        }
    }
}
