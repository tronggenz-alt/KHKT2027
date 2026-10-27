using System.Collections.Generic;
using Khkt.Interaction;
using UnityEngine;
using UnityEngine.Rendering;

namespace Khkt.Rendering
{
    /// <summary>
    /// HLOD cho một cụm vật cầm được (kệ hóa chất ~30 lọ, tủ dụng cụ thủy tinh). Vật cầm được không gộp lô tĩnh được (phải
    /// di chuyển được) nên mỗi lọ là 5–8 lệnh vẽ riêng. Ở xa (quá switchDistance), cả cụm được thay bằng một lưới gộp theo
    /// vật liệu (mỗi vật liệu một lệnh vẽ); tới gần thì hiện lại từng lọ thật để cầm, đọc nhãn.
    ///
    /// Lọ nào bị cầm lên / dời chỗ thì bị loại khỏi lưới gộp (lưới được tạo lại, lọ đó luôn hiện riêng); trả về đúng chỗ cũ
    /// thì được gộp lại. Chữ nhãn không gộp (phông động); ở xa chữ nhỏ đã bị tắt theo lớp ChuNho.
    /// </summary>
    public class ClusterHlod : MonoBehaviour
    {
        [Tooltip("Xa hơn khoảng này (m, tính tới tâm cụm) thì dùng lưới gộp")]
        public float switchDistance = 5.5f;
        public float hysteresis = 0.5f;
        [Tooltip("Gốc cụm (kệ, tủ). Thành phần này nằm trên một vật con riêng để phần tĩnh của kệ vẫn tách được sang cảnh khu")]
        public Transform cluster;

        class Member
        {
            public Grabbable grab;
            public MeshRenderer[] renderers;
            public bool[] wasEnabled;   // trạng thái bật của renderer trước khi bị ẩn (chuyển sang lưới gộp)
            public bool[] included;     // renderer này có trong lưới gộp (chỉ ẩn những cái có trong lưới)
            public Vector3 pos;
            public Quaternion rot;
            public bool inMesh;
        }

        readonly List<Member> members = new List<Member>();
        Mesh mesh;
        MeshRenderer hlodRenderer;
        MeshFilter hlodFilter;
        bool far, built;
        float nextTick;
        Vector3 center;
        static Transform cam;

        /// <summary>Số cụm đang vẽ bằng lưới gộp (để đo, hiển thị).</summary>
        public static int FarCount { get; private set; }

        void OnEnable()
        {
            if (!built) Build();
            nextTick = 0;
        }

        void OnDisable()
        {
            if (far) { far = false; FarCount--; }
            SetFar(false, true);
        }

        void Build()
        {
            built = true;
            if (cluster == null) cluster = transform.parent;
            foreach (var g in cluster.GetComponentsInChildren<Grabbable>(true))
            {
                var up = g.transform.parent != null ? g.transform.parent.GetComponentInParent<Grabbable>(true) : null;
                if (up != null && up.transform.IsChildOf(cluster)) continue;
                var rs = new List<MeshRenderer>();
                foreach (var r in g.GetComponentsInChildren<MeshRenderer>(true))
                    if (r.GetComponent<TextMesh>() == null && r.GetComponent<MeshFilter>() != null) rs.Add(r);
                members.Add(new Member { grab = g, renderers = rs.ToArray(), wasEnabled = new bool[rs.Count], included = new bool[rs.Count],
                                         pos = g.transform.position, rot = g.transform.rotation, inMesh = true });
            }
            hlodFilter = gameObject.AddComponent<MeshFilter>();
            hlodRenderer = gameObject.AddComponent<MeshRenderer>();
            hlodRenderer.shadowCastingMode = ShadowCastingMode.Off;
            hlodRenderer.receiveShadows = true;
            hlodRenderer.enabled = false;
            Rebuild();
        }

        /// <summary>Gộp các lọ còn ở đúng chỗ thành một lưới, mỗi vật liệu một lưới con.</summary>
        void Rebuild()
        {
            var byMat = new Dictionary<Material, List<CombineInstance>>();
            var toLocal = transform.worldToLocalMatrix;
            var b = new Bounds();
            bool hasB = false;
            foreach (var m in members)
            {
                for (int i = 0; i < m.renderers.Length; i++)
                {
                    var r = m.renderers[i];
                    bool hidden = far && m.inMesh && m.included[i];  // đang bị chính HLOD ẩn: xét trạng thái trước khi ẩn
                    if (hidden) m.renderers[i].enabled = m.wasEnabled[i];
                    m.included[i] = false;
                    if (!m.inMesh || r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                    var src = r.GetComponent<MeshFilter>().sharedMesh;
                    if (src == null || !src.isReadable) continue;
                    m.included[i] = true;
                    var mats = r.sharedMaterials;
                    for (int s = 0; s < src.subMeshCount && s < mats.Length; s++)
                    {
                        if (mats[s] == null) continue;
                        if (!byMat.TryGetValue(mats[s], out var list)) byMat[mats[s]] = list = new List<CombineInstance>();
                        list.Add(new CombineInstance { mesh = src, subMeshIndex = s, transform = toLocal * r.localToWorldMatrix });
                    }
                    if (!hasB) { b = r.bounds; hasB = true; } else b.Encapsulate(r.bounds);
                }
            }
            if (mesh == null) mesh = new Mesh { name = "HLOD " + name, indexFormat = IndexFormat.UInt32 };
            var parts = new List<CombineInstance>();
            var materials = new List<Material>();
            foreach (var kv in byMat)
            {
                var part = new Mesh { indexFormat = IndexFormat.UInt32 };
                part.CombineMeshes(kv.Value.ToArray(), true, true);
                parts.Add(new CombineInstance { mesh = part, transform = Matrix4x4.identity });
                materials.Add(kv.Key);
            }
            mesh.Clear();
            mesh.CombineMeshes(parts.ToArray(), false, false);
            foreach (var p in parts) Destroy(p.mesh);
            hlodFilter.sharedMesh = mesh;
            hlodRenderer.sharedMaterials = materials.ToArray();
            center = hasB ? b.center : cluster.position;
            if (far) foreach (var m in members) if (m.inMesh) Show(m, false);  // ẩn lại theo danh sách gộp mới
        }

        void Update()
        {
            if (Time.unscaledTime < nextTick) return;
            nextTick = Time.unscaledTime + 0.2f;
            if (cam == null) { var c = Camera.main; if (c == null) return; cam = c.transform; }

            // lọ bị cầm / dời chỗ: loại khỏi lưới gộp; trả về đúng chỗ cũ: gộp lại
            bool dirty = false;
            foreach (var m in members)
            {
                if (m.grab == null) continue;
                bool home = !m.grab.IsHeld && (m.grab.transform.position - m.pos).sqrMagnitude < 1e-6f
                            && Quaternion.Angle(m.grab.transform.rotation, m.rot) < 0.5f && m.grab.transform.IsChildOf(cluster);
                if (m.inMesh && !home) { if (far) Show(m, true); m.inMesh = false; dirty = true; }
                else if (!m.inMesh && home) { m.inMesh = true; dirty = true; }
            }
            if (dirty) Rebuild();

            float d = Vector3.Distance(cam.position, center);
            if (!far && d > switchDistance + hysteresis) SetFar(true);
            else if (far && d < switchDistance - hysteresis) SetFar(false);
        }

        void SetFar(bool value, bool force = false)
        {
            if (value == far && !force) return;
            if (value != far) FarCount += value ? 1 : -1;
            far = value;
            foreach (var m in members) if (m.inMesh) Show(m, !value);
            if (hlodRenderer != null) hlodRenderer.enabled = value;
        }

        /// <summary>Hiện / ẩn các renderer riêng của một lọ, giữ nguyên renderer vốn đang tắt (lọ rỗng, bột đã hết...).</summary>
        static void Show(Member m, bool show)
        {
            for (int i = 0; i < m.renderers.Length; i++)
            {
                var r = m.renderers[i];
                if (r == null || !m.included[i]) continue;
                if (!show) { m.wasEnabled[i] = r.enabled; r.enabled = false; }
                else r.enabled = m.wasEnabled[i];
            }
        }
    }
}
