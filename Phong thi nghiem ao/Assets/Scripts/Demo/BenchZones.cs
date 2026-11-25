using System.Collections.Generic;
using Khkt.Environment;
using Khkt.Interaction;
using UnityEngine;

namespace Khkt.Demo
{
    /// <summary>
    /// Khu tương tác theo bàn: mỗi trạm (LabStation) gắn với mặt bàn trước mặt nó. Khi đứng ở một trạm,
    /// điều khiển bằng tay chỉ lấy, đặt, bật máy trong khu của bàn đó (mặt bàn + kệ phía trên), nên không
    /// với nhầm sang bàn bên cạnh, đặt xuống luôn rơi đúng mặt bàn đang làm. Khu tính lúc chạy, không cần dựng lại cảnh.
    /// </summary>
    public class BenchZone
    {
        public LabStation station;
        public Collider surface;
        public Bounds box;           // vùng tương tác: mặt bàn nới rộng, cao tới kệ
        public float top;            // cao độ mặt bàn
        public Bounds plate;         // mặt bàn (để đặt vật)
        public string Name => station != null && !string.IsNullOrEmpty(station.displayName) ? station.displayName : "Bàn";

        public bool Contains(Vector3 p) => box.Contains(p);

        /// <summary>Chỗ trống trên mặt bàn gần điểm p nhất (lùi khỏi mép, tránh vật đang đứng).</summary>
        public Vector3 FreeSpot(Vector3 p, Grabbable ignore, float radius = 0.05f)
        {
            Vector3 c = new Vector3(Mathf.Clamp(p.x, plate.min.x + 0.08f, plate.max.x - 0.08f), top,
                                    Mathf.Clamp(p.z, plate.min.z + 0.08f, plate.max.z - 0.08f));
            for (int ring = 0; ring < 6; ring++)
            {
                int n = ring == 0 ? 1 : ring * 8;
                for (int k = 0; k < n; k++)
                {
                    float a = k * Mathf.PI * 2f / n;
                    Vector3 q = c + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * ring * radius * 1.6f;
                    if (!plate.Contains(new Vector3(q.x, plate.center.y, q.z))) continue;
                    if (!Occupied(q, radius, ignore)) return q;
                }
            }
            return c;
        }

        static readonly Collider[] buf = new Collider[32];

        static bool Occupied(Vector3 q, float r, Grabbable ignore)
        {
            int n = Physics.OverlapCapsuleNonAlloc(q + Vector3.up * (r + 0.01f), q + Vector3.up * 0.15f, r, buf, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var g = buf[i].GetComponentInParent<Grabbable>();
                if (g != null && g != ignore) return true;
            }
            return false;
        }
    }

    public static class BenchZones
    {
        static readonly List<BenchZone> zones = new List<BenchZone>();
        static float builtAt = -100f;

        public static IReadOnlyList<BenchZone> All { get { Refresh(); return zones; } }

        /// <summary>Khu của trạm người xem đang đứng (cách trạm dưới 1,2 m), null nếu đang đi lại tự do.</summary>
        public static BenchZone Current(Vector3 viewer)
        {
            Refresh();
            BenchZone best = null;
            float bd = 1.2f;
            foreach (var z in zones)
            {
                if (z.station == null) continue;
                Vector3 d = z.station.transform.position - viewer;
                d.y = 0;
                if (d.magnitude < bd) { bd = d.magnitude; best = z; }
            }
            return best;
        }

        static void Refresh()
        {
            // khu phòng có thể nạp / gỡ: dựng lại mỗi 4 giây hoặc khi mặt bàn biến mất
            bool stale = Time.unscaledTime - builtAt > 4f;
            foreach (var z in zones) if (z.surface == null || !z.surface.gameObject.activeInHierarchy) stale = true;
            if (!stale) return;
            builtAt = Time.unscaledTime;
            zones.Clear();
            var surfaces = new List<Collider>();
            foreach (var s in Object.FindObjectsByType<PlaceSurface>(FindObjectsInactive.Exclude))
            {
                var c = s.GetComponent<Collider>();
                if (c != null && c.enabled) surfaces.Add(c);
            }
            foreach (var st in Object.FindObjectsByType<LabStation>(FindObjectsInactive.Exclude))
            {
                Vector3 eye = st.transform.position + Vector3.up * (st.seated ? 1.25f : 1.6f);
                Vector3 fwd = st.transform.forward;
                Collider best = null;
                float bd = 1.6f;
                foreach (var c in surfaces)
                {
                    Vector3 p = c.ClosestPoint(eye);
                    Vector3 d = p - st.transform.position;
                    float up = d.y;
                    d.y = 0;
                    if (up < 0.5f || up > 1.3f) continue;                       // mặt bàn, không phải sàn hay nóc tủ
                    if (d.magnitude > 0.05f && Vector3.Dot(d.normalized, fwd) < 0.3f) continue;  // phải ở trước mặt
                    if (d.magnitude < bd) { bd = d.magnitude; best = c; }
                }
                if (best == null) continue;
                var b = best.bounds;
                var box = new Bounds(b.center, b.size);
                box.SetMinMax(new Vector3(b.min.x - 0.12f, b.max.y - 0.03f, b.min.z - 0.12f), new Vector3(b.max.x + 0.12f, b.max.y + 0.95f, b.max.z + 0.12f));
                zones.Add(new BenchZone { station = st, surface = best, box = box, top = b.max.y, plate = b });
            }
            zones.Sort((a, c) => a.station.order.CompareTo(c.station.order));
        }
    }
}
