using UnityEngine;

namespace Khkt.Assembly
{
    /// <summary>
    /// Ống cao su mềm: thân ống uốn võng giữa đầu A (gốc bộ phận này) và đầu B (một bộ phận cầm được riêng). Mỗi đầu lắp được
    /// vào đầu ống thủy tinh hoặc vòi nhánh. Thân ống là chuỗi đoạn trụ ngắn theo đường cong võng xuống, dài tối đa length;
    /// kéo xa hơn thì đầu B (nếu đang để tự do) bị kéo theo.
    /// </summary>
    public class RubberHose : MonoBehaviour
    {
        public Transform endA;
        public ApparatusPart endB;
        public Transform endBPoint;
        public float length = 0.6f;
        public float radius = 0.005f;
        public Material material;
        public int segments = 16;

        Transform[] segs;
        Vector3 lastA, lastB;

        void Start()
        {
            segs = new Transform[segments];
            for (int i = 0; i < segments; i++)
            {
                var s = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(s.GetComponent<Collider>());
                s.name = "Thân ống cao su";
                s.transform.SetParent(transform, false);
                var r = s.GetComponent<Renderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                segs[i] = s.transform;
            }
            lastA = lastB = new Vector3(float.NaN, 0, 0);
        }

        void DragB(Vector3 a, Vector3 b)
        {
            var flat = Vector3.ProjectOnPlane(b - a, Vector3.up);
            flat = flat.sqrMagnitude > 1e-6f ? flat.normalized : Vector3.forward;
            float reach = length * 0.85f;
            Vector3 target = a + flat * reach;
            if (SurfaceBelow(target, a.y + 0.02f, out float y))
            {
                float dy = Mathf.Min(a.y - y, reach * 0.95f);  // mặt đỡ ở quá thấp: đầu ống treo lơ lửng
                float h = Mathf.Sqrt(reach * reach - dy * dy);
                target = new Vector3(a.x + flat.x * h, a.y - dy, a.z + flat.z * h);
                if (SurfaceBelow(target, a.y + 0.02f, out float y2) && y2 > target.y - 0.02f) target.y = y2;
            }
            // nằm ngang trên mặt đỡ, thân ống quay về phía đầu A
            endB.transform.rotation = Quaternion.LookRotation(Vector3.up, -flat);
            endB.transform.position += target + Vector3.up * radius * 1.1f - endBPoint.position;
            ApparatusPart.NotifyChanged(endB);
        }

        bool SurfaceBelow(Vector3 p, float maxY, out float y)
        {
            y = float.NegativeInfinity;
            foreach (var h in Physics.RaycastAll(new Vector3(p.x, maxY, p.z), Vector3.down, 5f, ~0, QueryTriggerInteraction.Ignore))
                if (h.collider.GetComponentInParent<ApparatusPart>() != endB && h.point.y > y) y = h.point.y;
            return !float.IsNegativeInfinity(y);
        }

        void LateUpdate()
        {
            if (endA == null || endB == null || endBPoint == null || segs == null) return;
            Vector3 a = endA.position, b = endBPoint.position;
            float dist = Vector3.Distance(a, b);
            // đầu B đang để tự do mà bị kéo quá chiều dài ống: kéo theo, rơi xuống nằm trên mặt đỡ bên dưới (ống hơi chùng)
            if (dist > length && endB.ParentJoint == null && (endB.Grab == null || !endB.Grab.IsHeld))
            {
                DragB(a, b);
                b = endBPoint.position;
                dist = Vector3.Distance(a, b);
            }
            if ((a - lastA).sqrMagnitude < 1e-8f && (b - lastB).sqrMagnitude < 1e-8f) return;
            lastA = a;
            lastB = b;
            // đường cong bậc hai võng xuống: phần ống thừa càng nhiều càng võng
            float sag = Mathf.Max(0.01f, (length - dist) * 0.55f);
            Vector3 dirA = endA.forward, dirB = endBPoint.forward;
            Vector3 c = (a + b) / 2 - Vector3.up * sag - (dirA + dirB) * 0.02f;  // thân ống rời đầu nối theo chiều ngược hướng khớp
            Vector3 prev = a;
            for (int i = 0; i < segments; i++)
            {
                float t = (i + 1f) / segments;
                Vector3 p = (1 - t) * (1 - t) * a + 2 * (1 - t) * t * c + t * t * b;
                Vector3 d = p - prev;
                var s = segs[i];
                s.position = (p + prev) / 2;
                s.up = d.sqrMagnitude > 1e-8f ? d.normalized : Vector3.up;
                s.localScale = new Vector3(radius * 2, d.magnitude / 2 + radius * 0.4f, radius * 2);
                prev = p;
            }
        }
    }
}
