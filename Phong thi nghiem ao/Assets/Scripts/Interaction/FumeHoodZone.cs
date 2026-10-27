using System.Collections.Generic;
using UnityEngine;

namespace Khkt.Interaction
{
    /// <summary>
    /// Vùng làm việc trong tủ hút: vật chứa nằm trong hộp này được đánh dấu InFumeHood, nên khí độc
    /// sinh ra được hút đi (không thành sự cố). Quạt tủ hút tắt thì không có tác dụng.
    /// </summary>
    public class FumeHoodZone : MonoBehaviour
    {
        public Vector3 size = new Vector3(1.3f, 1.2f, 0.7f);
        public Demo.ToggleEffect fan;

        public bool Running => fan == null || fan.isOn;

        static readonly List<FumeHoodZone> zones = new List<FumeHoodZone>();
        static readonly List<LabContainer> containers = new List<LabContainer>();
        static float lastScan = -10f;

        void OnEnable() => zones.Add(this);
        void OnDisable() => zones.Remove(this);

        public bool Contains(Vector3 p)
        {
            Vector3 l = transform.InverseTransformPoint(p);
            return Mathf.Abs(l.x) <= size.x / 2 && Mathf.Abs(l.y) <= size.y / 2 && Mathf.Abs(l.z) <= size.z / 2;
        }

        /// <summary>Điểm này có nằm trong tủ hút đang chạy không.</summary>
        public static bool Covers(Vector3 p)
        {
            foreach (var z in zones) if (z.Running && z.Contains(p)) return true;
            return false;
        }

        public static FumeHoodZone Nearest(Vector3 p)
        {
            FumeHoodZone best = null;
            float bd = float.MaxValue;
            foreach (var z in zones)
            {
                float d = Vector3.Distance(z.transform.position, p);
                if (d < bd) { bd = d; best = z; }
            }
            return best;
        }

        void Update()
        {
            if (zones.Count == 0 || zones[0] != this) return;  // một tủ làm thay cho tất cả
            if (Time.time - lastScan > 1.5f)
            {
                lastScan = Time.time;
                containers.Clear();
                foreach (var c in Khkt.Rendering.ZoneRoot.FindAll<LabContainer>()) if (c.reactive) containers.Add(c);
            }
            foreach (var c in containers)
                if (c != null) c.InFumeHood = Covers(c.transform.position);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.3f, 1f, 0.6f, 0.5f);
            Gizmos.DrawWireCube(Vector3.zero, size);
        }
    }
}
