using System.Collections;
using System.Collections.Generic;
using Khkt.Demo;
using UnityEngine;

namespace Khkt.Assembly
{
    /// <summary>
    /// Luật vật lý giản lược cho bộ lắp: kiểm tra trọng tâm. Bộ lắp đứng trên mặt bàn nhờ các đế (giá sắt, bình đáy bằng...);
    /// trọng tâm (khối lượng dụng cụ + dung dịch bên trong) mà lệch ra ngoài vùng đế thì cả bộ đổ, dung dịch tràn ra. Ví dụ
    /// kẹp bình cầu đầy nước ở đầu tay kẹp vươn quá xa, hoặc ống sinh hàn dài chìa ra mà không có giá đỡ thứ hai.
    /// </summary>
    public partial class AssemblyManager : MonoBehaviour
    {
        public static AssemblyManager Instance { get; private set; }
        [Tooltip("Lề an toàn quanh vùng đế (m)")]
        public float margin = 0.005f;
        [Tooltip("Ghi nhật ký mỗi lần kiểm tra (gỡ lỗi)")]
        public bool verbose;

        readonly HashSet<ApparatusPart> toppled = new HashSet<ApparatusPart>();
        readonly HashSet<ApparatusPart> dirty = new HashSet<ApparatusPart>();
        float next;

        /// <summary>Lần đổ gần nhất (để kiểm thử / hiển thị).</summary>
        public static string LastTopple { get; private set; }

        void Awake() => Instance = this;
        void OnEnable() => ApparatusPart.Changed += OnChanged;
        void OnDisable() => ApparatusPart.Changed -= OnChanged;

        void OnChanged(ApparatusPart root)
        {
            if (root == null) return;
            toppled.Remove(root);
            dirty.Add(root);
        }

        void Update()
        {
            if (dirty.Count > 0)
            {
                foreach (var r in dirty) if (r != null) Check(r.Root);
                dirty.Clear();
            }
            if (Time.time < next) return;
            next = Time.time + 0.5f;
            for (int i = ApparatusPart.All.Count - 1; i >= 0; i--)
            {
                var p = ApparatusPart.All[i];
                if (p != null && p.ParentJoint == null) Check(p);
            }
        }

        /// <summary>Trọng tâm của cả bộ (tính cả dung dịch: 1 ml ≈ 1 g).</summary>
        public static Vector3 CenterOfMass(List<ApparatusPart> parts, out float massG)
        {
            Vector3 sum = Vector3.zero;
            massG = 0;
            foreach (var p in parts)
            {
                float m = (p.Grab != null ? p.Grab.massG : 200f) + (p.Container != null ? p.Container.VolumeMl : 0f);
                sum += OwnCenter(p) * m;
                massG += m;
            }
            return massG > 0 ? sum / massG : parts[0].transform.position;
        }

        /// <summary>Tâm khối bao của riêng bộ phận (không tính các bộ phận lắp vào nó).</summary>
        static Vector3 OwnCenter(ApparatusPart p)
        {
            bool has = false;
            var b = new Bounds();
            foreach (var r in p.GetComponentsInChildren<Renderer>())
            {
                if (r.GetComponentInParent<ApparatusPart>() != p || r is ParticleSystemRenderer) continue;
                // thân ống cao su võng tới tận vòi nước ở xa: nhẹ, mềm, không đỡ được bộ lắp, chỉ tính đầu nối
                if (r.transform.parent != null && r.transform.parent.GetComponent<RubberHose>() != null && r.name.StartsWith("Thân ống")) continue;
                if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
            }
            return has ? b.center : p.transform.position;
        }

        /// <summary>Vùng đế (theo x, z thế giới) của các đế đang chạm mặt bàn. false: không đế nào chạm bàn (đang cầm, nằm trong ổ...).</summary>
        public static bool Footprint(List<ApparatusPart> parts, out Rect rect, out float floorY)
        {
            rect = default;
            floorY = 0;
            bool any = false;
            var root = parts[0].Root;
            foreach (var p in parts)
            {
                if (p.baseSize.x <= 0 || p.baseSize.y <= 0) continue;
                if (!Grounded(p, root, out float y)) continue;
                for (int i = 0; i < 4; i++)
                {
                    var c = p.transform.TransformPoint(new Vector3((i & 1) == 0 ? -p.baseSize.x / 2 : p.baseSize.x / 2, 0, (i & 2) == 0 ? -p.baseSize.y / 2 : p.baseSize.y / 2));
                    var q = new Vector2(c.x, c.z);
                    if (!any) { rect = new Rect(q, Vector2.zero); any = true; floorY = y; }
                    else rect = Rect.MinMaxRect(Mathf.Min(rect.xMin, q.x), Mathf.Min(rect.yMin, q.y), Mathf.Max(rect.xMax, q.x), Mathf.Max(rect.yMax, q.y));
                }
            }
            return any;
        }

        static bool Grounded(ApparatusPart p, ApparatusPart root, out float y)
        {
            y = 0;
            var o = p.transform.position + Vector3.up * 0.03f;
            foreach (var h in Physics.RaycastAll(o, Vector3.down, 0.07f, ~0, QueryTriggerInteraction.Ignore))
            {
                var ap = h.collider.GetComponentInParent<ApparatusPart>();
                if (ap != null && ap.Root == root) continue;
                y = h.point.y;
                return true;
            }
            return false;
        }

        void Check(ApparatusPart root)
        {
            if (root == null || toppled.Contains(root) || (root.Grab != null && root.Grab.IsHeld) || !root.isActiveAndEnabled) return;
            if (FabricationWorkspace.IsInside(root.transform)) return;  // bàn ảo khu chế tạo: chỉ cảnh báo trên bảng kiểm tra, không cho đổ
            var parts = root.Subtree();
            if (parts.Count < 2) return;
            if (!Footprint(parts, out var rect, out float floorY)) return;
            var com = CenterOfMass(parts, out float mass);
            var q = new Vector2(com.x, com.z);
            if (Debug.isDebugBuild && verbose) Debug.Log($"[KHKT-LAP] Kiểm tra {root.partName}: {parts.Count} bộ phận, trọng tâm ({q.x:0.000}, {q.y:0.000}), đế {rect}");
            if (q.x >= rect.xMin - margin && q.x <= rect.xMax + margin && q.y >= rect.yMin - margin && q.y <= rect.yMax + margin) return;
            toppled.Add(root);
            StartCoroutine(Topple(root, parts, rect, com, floorY, mass));
        }

        IEnumerator Topple(ApparatusPart root, List<ApparatusPart> parts, Rect rect, Vector3 com, float floorY, float mass)
        {
            var q = new Vector2(com.x, com.z);
            var edge = new Vector2(Mathf.Clamp(q.x, rect.xMin, rect.xMax), Mathf.Clamp(q.y, rect.yMin, rect.yMax));
            var pivot = new Vector3(edge.x, floorY, edge.y);
            var outward = new Vector3(q.x - edge.x, 0, q.y - edge.y);
            if (outward.sqrMagnitude < 1e-6f) outward = Vector3.forward;
            var axis = Vector3.Cross(Vector3.up, outward.normalized);
            LastTopple = $"{root.partName} đổ ({mass:0} g, trọng tâm lệch {outward.magnitude * 100:0} cm khỏi đế)";
            LabMessages.Say("Bộ dụng cụ bị đổ! Trọng tâm lệch ra ngoài đế giá: kẹp bình sát cọc hơn, hạ thấp, hoặc thêm giá đỡ thứ hai",
                            LabMessages.Tone.Warn);
            float angle = 0, speed = 0;
            while (angle < 82f)
            {
                speed += 900f * Time.deltaTime;  // ngã nhanh dần như rơi
                float d = Mathf.Min(speed * Time.deltaTime, 82f - angle);
                root.transform.RotateAround(pivot, axis, d);
                angle += d;
                yield return null;
            }
            // ngã ra ngoài mép bàn thì rơi tiếp xuống chỗ đỡ bên dưới trọng tâm (mặt bàn khác hoặc sàn)
            yield return Drop(root, parts);
            float spilled = 0;
            foreach (var p in parts) if (p != null && p.Container != null) spilled += p.Container.SpillAll();
            if (spilled > 0.5f) LabMessages.Say($"Đổ ra bàn {spilled:0} ml dung dịch: lau sạch, cẩn thận mảnh thủy tinh", LabMessages.Tone.Warn);
        }
    }

    public partial class AssemblyManager
    {
        IEnumerator Drop(ApparatusPart root, List<ApparatusPart> parts)
        {
            bool has = false;
            var b = new Bounds();
            foreach (var p in parts)
                foreach (var r in p.GetComponentsInChildren<Renderer>())
                {
                    if (r.GetComponentInParent<ApparatusPart>() != p) continue;
                    if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
                }
            if (!has) yield break;
            var com = CenterOfMass(parts, out _);
            float below = float.NegativeInfinity;
            foreach (var h in Physics.RaycastAll(new Vector3(com.x, b.max.y + 0.05f, com.z), Vector3.down, 10f, ~0, QueryTriggerInteraction.Ignore))
            {
                var ap = h.collider.GetComponentInParent<ApparatusPart>();
                if (ap != null && ap.Root == root) continue;
                below = Mathf.Max(below, h.point.y > b.max.y ? float.NegativeInfinity : h.point.y);
            }
            if (float.IsNegativeInfinity(below)) yield break;
            float fall = b.min.y - below;
            float v = 0, done = 0;
            while (done < fall - 1e-4f)
            {
                v += 9.81f * Time.deltaTime;
                float d = Mathf.Min(v * Time.deltaTime, fall - done);
                root.transform.position += Vector3.down * d;
                done += d;
                yield return null;
            }
        }
    }

    /// <summary>Bóng mờ xem trước chỗ lắp: bản sao trong suốt của bộ phận đang cầm, đặt đúng tư thế sẽ lắp.</summary>
    public static class AssemblyGhost
    {
        static GameObject ghost;
        static ApparatusPart forPart;

        public static void Show(ApparatusPart part, Vector3 pos, Quaternion rot, Material mat)
        {
            if (part == null || mat == null) { Hide(); return; }
            if (forPart != part || ghost == null) Rebuild(part, mat);
            ghost.SetActive(true);
            ghost.transform.SetPositionAndRotation(pos, rot);
        }

        public static void Hide()
        {
            if (ghost != null && ghost.activeSelf) ghost.SetActive(false);
        }

        static void Rebuild(ApparatusPart part, Material mat)
        {
            if (ghost != null) Object.Destroy(ghost);
            forPart = part;
            ghost = new GameObject("Bóng mờ lắp ráp");
            var root = part.transform;
            foreach (var mf in part.GetComponentsInChildren<MeshFilter>())
            {
                var r = mf.GetComponent<MeshRenderer>();
                if (r == null || !r.enabled || mf.sharedMesh == null) continue;
                var g = new GameObject("Bóng", typeof(MeshFilter), typeof(MeshRenderer));
                g.transform.SetParent(ghost.transform, false);
                g.transform.localPosition = root.InverseTransformPoint(mf.transform.position);
                g.transform.localRotation = Quaternion.Inverse(root.rotation) * mf.transform.rotation;
                var s = mf.transform.lossyScale;
                var rs = root.lossyScale;
                g.transform.localScale = new Vector3(s.x / rs.x, s.y / rs.y, s.z / rs.z);
                g.GetComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var gr = g.GetComponent<MeshRenderer>();
                var mats = new Material[mf.sharedMesh.subMeshCount];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                gr.sharedMaterials = mats;
                gr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                gr.receiveShadows = false;
            }
        }
    }
}
