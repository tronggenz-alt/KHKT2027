using System.Collections.Generic;
using System.Text;
using Khkt.Interaction;
using Khkt.Rendering;
using UnityEngine;

namespace Khkt.Assembly
{
    /// <summary>
    /// Một bộ phận lắp ráp (giá sắt, kẹp, bình cầu, đầu nối, ống sinh hàn, nút, ống dẫn, phễu...). Các bộ phận nối với nhau
    /// thành cây: bộ phận được lắp vào trở thành con (Transform) của bộ phận đích, nên cầm giá sắt thì cả bộ đi theo; cầm một
    /// bộ phận đang lắp thì nó tự tháo ra, kéo theo các bộ phận lắp vào nó. Khớp nào nối với khớp nào được ghi ở Port.Connected,
    /// dùng cho mạng dòng chảy (giai đoạn C3), kiểm tra hệ kín / hở và nhận diện bộ lắp.
    /// </summary>
    public class ApparatusPart : MonoBehaviour
    {
        public string partName = "Bộ phận";
        public Port[] ports;
        [Tooltip("Đế đặt lên mặt bàn (giá sắt, bình đáy bằng...): kích thước đế theo x, z cục bộ (m), tâm ở gốc. 0: không có đế")]
        public Vector2 baseSize;
        [Tooltip("Ống cao su: hai đầu là hai bộ phận riêng, thông nhau qua thân ống")]
        public ApparatusPart linked;
        [Tooltip("Gắn cố định vào bàn (vòi cấp nước, ống thoát): không cầm được")]
        public bool fixture;
        [Tooltip("Đầu nhỏ giọt: sau khi lắp bằng khớp nhám, bộ phận tự xoay quanh trục khớp cho trục này chĩa xuống nhất có thể")]
        public Transform dripAxis;
        [Tooltip("Mã loại trong kho bộ phận mẫu của khu chế tạo (rỗng: bộ phận dựng sẵn ngoài phòng)")]
        public string libraryId;
        [Tooltip("Thứ tự trong một mẫu nhiều bộ phận (ống cao su: 0 đầu A, 1 đầu B)")]
        public int librarySub;

        /// <summary>Thuộc bộ dụng cụ đã gia công số mấy (0: không). Ngoài phòng, cầm bộ phận của bộ đã gia công là cầm cả bộ.</summary>
        [System.NonSerialized] public int fabricatedSet;

        public static readonly List<ApparatusPart> All = new List<ApparatusPart>();

        /// <summary>
        /// Ngoài phòng lab: vật thực sự được cầm khi trỏ vào g. Bộ phận của bộ đã gia công thì cầm cả bộ (gốc cây lắp ráp), trừ
        /// các thao tác lẻ: đầu ống cao su (nối / tháo vòi), nút cao su không cắm gì, vật đặt rời (cốc, bình hứng, đèn cồn).
        /// Muốn sửa cả bộ thì mang về khu chế tạo.
        /// </summary>
        public static Grabbable WorldGrab(Grabbable g)
        {
            var p = g != null ? g.GetComponent<ApparatusPart>() : null;
            if (p == null || p.fabricatedSet == 0 || WorldEditable(p)) return g;
            var r = p;
            for (int guard = 0; r.ParentPart != null && !r.ParentPart.fixture && guard < 64; guard++) r = r.ParentPart;
            return r.Grab != null ? r.Grab : g;
        }

        public static bool WorldEditable(ApparatusPart p) =>
            p.ParentJoint == null || p.linked != null || (p.libraryId != null && p.libraryId.StartsWith("nut") && p.Subtree().Count == 1);

        /// <summary>Lắp sẵn khi dựng cảnh (chế độ soạn thảo): đặt tư thế, nối khớp, làm con của bộ phận đích. Không phát sự kiện.</summary>
        public static void PreAttach(Port mine, Port target)
        {
            var me = mine.Part;
            ComputePose(mine, target, out var pos, out var rot);
            me.transform.SetPositionAndRotation(pos, rot);
            mine.Connected = target;
            target.Connected = mine;
            me.ParentJoint = mine;
            me.transform.SetParent(target.Part.transform, true);
        }
        /// <summary>Có thay đổi trong một bộ lắp (lắp, tháo, đổ). Tham số: gốc của bộ.</summary>
        public static event System.Action<ApparatusPart> Changed;

        public Grabbable Grab { get; private set; }
        public ApparatusPart ParentPart => ParentJoint != null && ParentJoint.Connected != null ? ParentJoint.Connected.Part : null;
        [SerializeField, HideInInspector] Port parentJoint;
        /// <summary>Khớp của bộ phận này đang nối vào bộ phận cha (null: là gốc của bộ).</summary>
        public Port ParentJoint { get => parentJoint; private set => parentJoint = value; }
        public LabContainer Container { get; private set; }

        void Awake()
        {
            Grab = GetComponent<Grabbable>();
            Container = GetComponent<LabContainer>();
            if (ports == null) ports = new Port[0];
            foreach (var p in ports) if (p != null) p.Part = this;
            if (Grab != null) Grab.HeldChanged += OnHeldChanged;
        }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() => All.Remove(this);

        void OnHeldChanged(bool held)
        {
            if (held && ParentJoint != null) Detach();
        }

        // ------------------------------------------------------------------
        // Cây lắp ráp
        // ------------------------------------------------------------------

        public ApparatusPart Root
        {
            get
            {
                var p = this;
                for (int guard = 0; p.ParentPart != null && guard < 64; guard++) p = p.ParentPart;
                return p;
            }
        }

        /// <summary>Bộ phận này và mọi bộ phận lắp (trực tiếp hay gián tiếp) vào nó.</summary>
        public List<ApparatusPart> Subtree()
        {
            var list = new List<ApparatusPart>();
            var stack = new Stack<ApparatusPart>();
            stack.Push(this);
            while (stack.Count > 0)
            {
                var p = stack.Pop();
                list.Add(p);
                foreach (var port in p.ports)
                    if (port != null && port.Connected != null && port.Connected.Part.ParentJoint == port.Connected) stack.Push(port.Connected.Part);
            }
            return list;
        }

        /// <summary>Có khớp nào trong nhóm loại trừ này đang được dùng không.</summary>
        public bool GroupBusy(int group)
        {
            foreach (var p in ports) if (p != null && p.exclusiveGroup == group && p.Connected != null) return true;
            return false;
        }

        public bool InSubtreeOf(ApparatusPart ancestor)
        {
            for (var p = this; p != null; p = p.ParentPart) if (p == ancestor) return true;
            return false;
        }

        /// <summary>Tư thế (vị trí, hướng) của gốc bộ phận chứa khớp mine để khớp đó áp đúng vào khớp target.</summary>
        public static void ComputePose(Port mine, Port target, out Vector3 pos, out Quaternion rot)
        {
            Transform part = mine.Part.transform;
            Quaternion local = Quaternion.Inverse(part.rotation) * mine.transform.rotation;
            Vector3 fwd = -target.transform.forward;
            Vector3 up = Port.FixedRoll(mine.type) || Port.FixedRoll(target.type)
                ? target.transform.up
                : Vector3.ProjectOnPlane(mine.transform.up, fwd);
            if (up.sqrMagnitude < 1e-4f) up = Vector3.ProjectOnPlane(target.transform.up, fwd);
            if (up.sqrMagnitude < 1e-4f) up = Vector3.ProjectOnPlane(Vector3.up, fwd);
            if (up.sqrMagnitude < 1e-4f) up = Vector3.ProjectOnPlane(Vector3.forward, fwd);
            rot = Quaternion.LookRotation(fwd, up.normalized) * Quaternion.Inverse(local);
            Vector3 offset = Vector3.Scale(part.InverseTransformPoint(mine.transform.position), part.lossyScale);
            pos = target.transform.position - rot * offset;
        }

        /// <summary>Lắp bộ phận này vào khớp target bằng khớp mine của nó (đặt đúng tư thế, trở thành con của bộ phận đích).</summary>
        public void AttachTo(Port mine, Port target)
        {
            if (mine == null || target == null || mine.Part != this || !mine.Free || !target.Free) return;
            if (ParentJoint != null) Detach();
            ComputePose(mine, target, out var pos, out var rot);
            transform.SetPositionAndRotation(pos, rot);
            if (dripAxis != null && !Port.FixedRoll(mine.type))
            {
                // xoay quanh trục khớp cho đầu nhỏ giọt chĩa xuống
                Vector3 axis = mine.transform.forward;
                Vector3 a = Vector3.ProjectOnPlane(dripAxis.forward, axis), b = Vector3.ProjectOnPlane(Vector3.down, axis);
                if (a.sqrMagnitude > 1e-6f && b.sqrMagnitude > 1e-6f)
                    transform.RotateAround(mine.transform.position, axis, Vector3.SignedAngle(a, b, axis));
            }
            mine.Connected = target;
            target.Connected = mine;
            ParentJoint = mine;
            transform.SetParent(target.Part.transform, true);
            Changed?.Invoke(Root);
        }

        /// <summary>Tháo khỏi bộ phận cha (mang theo các bộ phận lắp vào nó).</summary>
        public void Detach()
        {
            if (ParentJoint == null) return;
            var oldRoot = Root;
            var other = ParentJoint.Connected;
            if (other != null) other.Connected = null;
            ParentJoint.Connected = null;
            ParentJoint = null;
            var zone = ZoneRoot.Of(ZoneMap.At(transform.position));
            transform.SetParent(zone != null ? zone.transform : oldRoot.transform.parent, true);
            Changed?.Invoke(oldRoot);
            Changed?.Invoke(this);
        }

        /// <summary>Báo cho các hệ thống (mạng dòng chảy, kiểm tra đổ) rằng bộ lắp vừa đổi (đổ, đổi chất...).</summary>
        public static void NotifyChanged(ApparatusPart root) => Changed?.Invoke(root);

        // ------------------------------------------------------------------
        // Tìm chỗ lắp cho vật đang cầm
        // ------------------------------------------------------------------

        /// <summary>
        /// Tìm cặp khớp lắp được gần tia ngắm nhất: khớp trống của bộ phận đích nằm cách tia không quá maxPerp (m), trong tầm reach,
        /// và hợp loại / cỡ với một khớp trống của bộ phận đang cầm.
        /// </summary>
        public static bool FindAttach(ApparatusPart held, Ray ray, float maxPerp, float reach, out Port mine, out Port target)
        {
            mine = null;
            target = null;
            float best = maxPerp;  // điểm: khoảng cách tới tia (m) + phạt góc xoay nhỏ
            foreach (var part in All)
            {
                if (part == null || part == held || part.InSubtreeOf(held)) continue;
                if (part.Grab != null && part.Grab.IsHeld) continue;
                foreach (var tp in part.ports)
                {
                    if (tp == null || !tp.Free) continue;
                    Vector3 v = tp.transform.position - ray.origin;
                    float along = Vector3.Dot(v, ray.direction);
                    if (along < 0.05f || along > reach) continue;
                    float perp = (v - ray.direction * along).magnitude;
                    if (perp >= best + 0.04f) continue;
                    foreach (var hp in held.ports)
                    {
                        if (hp == null || !hp.Free || !Port.Compatible(hp, tp)) continue;
                        // cùng khoảng cách (nhiều hướng ở một điểm): chọn tư thế ít phải xoay so với hướng đang cầm
                        ComputePose(hp, tp, out _, out var rot);
                        float score = perp + 0.02f * Quaternion.Angle(rot, held.transform.rotation) / 180f;
                        if (score >= best) continue;
                        best = score;
                        mine = hp;
                        target = tp;
                    }
                }
            }
            return target != null;
        }

        // ------------------------------------------------------------------
        // Hệ kín / hở
        // ------------------------------------------------------------------

        /// <summary>
        /// Các lối thông ra không khí của khoang chất chứa trong bộ phận này (kênh 0): đi theo các khớp đã nối (cùng kênh dẫn chất)
        /// và thân ống cao su. Danh sách rỗng: hệ kín.
        /// </summary>
        public List<Port> Openings(int channel = 0)
        {
            var open = new List<Port>();
            var seen = new HashSet<(ApparatusPart, int)>();
            var queue = new Queue<(ApparatusPart, int)>();
            queue.Enqueue((this, channel));
            while (queue.Count > 0)
            {
                var (p, ch) = queue.Dequeue();
                if (!seen.Add((p, ch))) continue;
                foreach (var port in p.ports)
                {
                    if (port == null || port.channel != ch) continue;
                    if (port.IsOpening) open.Add(port);
                    else if (port.Connected != null && port.Connected.channel >= 0) queue.Enqueue((port.Connected.Part, port.Connected.channel));
                }
                if (p.linked != null && ch == 0) queue.Enqueue((p.linked, 0));
            }
            return open;
        }

        /// <summary>Mô tả bộ lắp chứa bộ phận này: số bộ phận, các khớp đã nối, hệ kín / hở của bình chứa chính.</summary>
        public string DescribeAssembly()
        {
            var root = Root;
            var parts = root.Subtree();
            var sb = new StringBuilder();
            sb.Append($"Bộ lắp {parts.Count} bộ phận");
            LabContainer vessel = null;
            foreach (var p in parts) if (p.Container != null && p.Container.reactive) { vessel = p.Container; break; }
            if (vessel != null)
            {
                var open = vessel.GetComponent<ApparatusPart>().Openings();
                if (open.Count == 0) sb.Append($". {vessel.label}: HỆ KÍN, không có lối thoát khí — không được đun nóng");
                else
                {
                    sb.Append($". {vessel.label}: hệ hở, thông ra ngoài ở ");
                    for (int i = 0; i < open.Count && i < 3; i++)
                        sb.Append(i > 0 ? ", " : "").Append(open[i].label).Append(" (").Append(open[i].Part.partName.ToLowerInvariant()).Append(')');
                }
            }
            return sb.ToString();
        }
    }
}
