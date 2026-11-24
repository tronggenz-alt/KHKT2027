using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Khkt.Input
{
    /// <summary>
    /// Mô hình 3D thật của tay cầm VERA Band 2 (bản in ×1.3), xuất bằng tools/tay_cam_band2/xuat_unity.py vào
    /// Assets/Lab/MoHinh/Resources/TayCam/VeraBand2.bytes. Hệ trục giống tay cầm sau Frames.ToUnity: mũi +z, cán dựng +y,
    /// phải +x; gốc ở tâm hộp bao. Cần joystick do code vẽ và nghiêng theo Joy2; mặt tối (chạm A) và cò (chạm B) sáng lên
    /// khi chạm. Không có tệp thì dựng hình khối đơn giản cùng cỡ để trang vẫn chạy.
    /// </summary>
    public sealed class VeraModel
    {
        public Transform Root { get; private set; }
        public bool FromFile { get; private set; }
        public int PartCount { get; private set; }
        public Bounds Bounds { get; private set; }

        Transform knob;
        Vector3 knobAxis = Vector3.up, knobFwd = Vector3.forward, knobRight = Vector3.right;
        readonly List<(Material m, Color baseColor)> touchA = new List<(Material, Color)>(), touchB = new List<(Material, Color)>();
        float glowA, glowB;
        static readonly Color GlowA = new Color(1f, 0.62f, 0.15f), GlowB = new Color(0.25f, 0.85f, 0.5f);

        sealed class PartData
        {
            public string name;
            public Color32 color;
            public int role;
            public Mesh mesh;
        }

        static List<PartData> cache;
        static Vector3[] cacheMeta;

        /// <summary>Đọc tệp mô hình (một lần cho cả app). Trả về false nếu thiếu hoặc hỏng.</summary>
        static bool LoadParts()
        {
            if (cache != null) return cache.Count > 0;
            cache = new List<PartData>();
            var ta = Resources.Load<TextAsset>("TayCam/VeraBand2");
            if (ta == null) return false;
            try
            {
                using var r = new BinaryReader(new MemoryStream(ta.bytes));
                if (Encoding.ASCII.GetString(r.ReadBytes(4)) != "VTC1") return false;
                int n = r.ReadInt32();
                cacheMeta = new Vector3[4];
                for (int i = 0; i < 4; i++) cacheMeta[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                float scale = r.ReadSingle();
                for (int k = 0; k < n; k++)
                {
                    var pd = new PartData();
                    int len = r.ReadByte();
                    pd.name = Encoding.UTF8.GetString(r.ReadBytes(len));
                    var c = r.ReadBytes(4);
                    pd.color = new Color32(c[0], c[1], c[2], c[3]);
                    pd.role = r.ReadByte();
                    int nv = r.ReadInt32(), ni = r.ReadInt32();
                    var pos = new Vector3[nv];
                    var nor = new Vector3[nv];
                    for (int i = 0; i < nv; i++)
                    {
                        pos[i] = new Vector3(r.ReadInt16(), r.ReadInt16(), r.ReadInt16()) * scale;
                        nor[i] = new Vector3((sbyte)r.ReadByte(), (sbyte)r.ReadByte(), (sbyte)r.ReadByte()) / 127f;
                    }
                    var idx = new int[ni];
                    if (nv < 65536) for (int i = 0; i < ni; i++) idx[i] = r.ReadUInt16();
                    else for (int i = 0; i < ni; i++) idx[i] = (int)r.ReadUInt32();
                    var m = new Mesh { name = pd.name, hideFlags = HideFlags.DontSave };
                    if (nv >= 65536) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                    m.vertices = pos;
                    m.normals = nor;
                    m.triangles = idx;
                    m.RecalculateBounds();
                    pd.mesh = m;
                    cache.Add(pd);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[VeraModel] tệp mô hình hỏng: " + e.Message);
                cache.Clear();
            }
            return cache.Count > 0;
        }

        /// <summary>Dựng mô hình dưới parent. baseShader: shader có màu (URP Lit / Standard).</summary>
        public static VeraModel Build(Transform parent, Shader baseShader)
        {
            var vm = new VeraModel();
            var root = new GameObject("Mô hình tay cầm VERA Band 2").transform;
            root.SetParent(parent, false);
            vm.Root = root;
            Material Mat(Color c, float smooth)
            {
                var m = new Material(baseShader) { hideFlags = HideFlags.DontSave };
                m.color = c;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
                if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
                return m;
            }
            var bounds = new Bounds();
            bool first = true;
            if (LoadParts())
            {
                vm.FromFile = true;
                foreach (var pd in cache)
                {
                    var go = new GameObject(pd.name);
                    go.transform.SetParent(root, false);
                    go.AddComponent<MeshFilter>().sharedMesh = pd.mesh;
                    Color col = pd.color;
                    var mat = Mat(col, col.grayscale < 0.2f ? 0.55f : 0.35f);
                    go.AddComponent<MeshRenderer>().sharedMaterial = mat;
                    if (pd.role == 1) vm.touchA.Add((mat, col));
                    if (pd.role == 2) vm.touchB.Add((mat, col));
                    if (first) { bounds = pd.mesh.bounds; first = false; } else bounds.Encapsulate(pd.mesh.bounds);
                }
                vm.PartCount = cache.Count;
                vm.knobAxis = cacheMeta[1].normalized;
                vm.knobFwd = cacheMeta[2].normalized;
                vm.knobRight = cacheMeta[3].normalized;
                vm.BuildKnob(root, cacheMeta[0], Mat(new Color(0.12f, 0.13f, 0.15f), 0.3f));
            }
            else
            {
                // dự phòng: khối cỡ tay cầm (thân 50 × 40 mm, cao 160 mm, đầu to hơn)
                var body = Mat(new Color(0.93f, 0.95f, 0.96f), 0.35f);
                var face = Mat(new Color(0.08f, 0.1f, 0.11f), 0.55f);
                Prim(PrimitiveType.Cube, root, new Vector3(0, -0.02f, 0), new Vector3(0.04f, 0.12f, 0.05f), body);
                var top = Prim(PrimitiveType.Cube, root, new Vector3(0, 0.06f, 0.005f), new Vector3(0.06f, 0.05f, 0.09f), face);
                vm.touchA.Add((face, face.color));
                bounds = new Bounds(new Vector3(0, 0.0f, 0), new Vector3(0.06f, 0.165f, 0.1f));
                vm.knobAxis = Vector3.up;
                vm.BuildKnob(root, new Vector3(0, 0.07f, -0.01f), Mat(new Color(0.12f, 0.13f, 0.15f), 0.3f));
                vm.PartCount = 2;
                _ = top;
            }
            vm.Bounds = bounds;
            return vm;
        }

        static GameObject Prim(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(t);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        void BuildKnob(Transform root, Vector3 pivot, Material m)
        {
            knob = new GameObject("Cần joystick").transform;
            knob.SetParent(root, false);
            knob.localPosition = pivot;
            knob.localRotation = Quaternion.FromToRotation(Vector3.up, knobAxis);
            // thân cần Ø5 dài 17 mm từ khớp xoay, nắp Ø20 cao 6 mm (KY-023)
            var stem = Prim(PrimitiveType.Cylinder, knob, new Vector3(0, 0.0085f, 0), new Vector3(0.005f, 0.0085f, 0.005f), m);
            var cap = Prim(PrimitiveType.Cylinder, knob, new Vector3(0, 0.019f, 0), new Vector3(0.02f, 0.003f, 0.02f), m);
            stem.name = "Thân cần";
            cap.name = "Nắp cần";
        }

        /// <summary>Cập nhật cần gạt (nghiêng tối đa 25°) và đèn chạm theo trạng thái tay cầm.</summary>
        public void Animate(in ControllerState s, float dt)
        {
            if (knob != null)
            {
                Vector2 j = Vector2.ClampMagnitude(s.Joy2, 1f);
                // đẩy tiến: đầu cần nghiêng về phía mũi (knobFwd); đẩy phải: nghiêng sang phải
                Vector3 tiltAxis = Vector3.Cross(knobAxis, knobFwd * j.y + knobRight * j.x);
                float ang = j.magnitude * 25f;
                var baseRot = Quaternion.FromToRotation(Vector3.up, knobAxis);
                knob.localRotation = tiltAxis.sqrMagnitude > 1e-6f ? Quaternion.AngleAxis(ang, tiltAxis.normalized) * baseRot : baseRot;
                bool press = s.Has(VeraFlags.StickPress) || s.Pressed(ControllerButtons.Joy2Press);
                knob.localScale = press ? new Vector3(1, 0.85f, 1) : Vector3.one;
            }
            float k = 1f - Mathf.Exp(-dt * 18f);
            glowA = Mathf.Lerp(glowA, s.Has(VeraFlags.TouchA) ? 1f : s.TouchA / 255f * 0.6f, k);
            glowB = Mathf.Lerp(glowB, s.Has(VeraFlags.TouchB) || s.Pressed(ControllerButtons.Trigger) ? 1f : s.TouchB / 255f * 0.6f, k);
            foreach (var (m, c) in touchA) SetColor(m, Color.Lerp(c, GlowA, glowA));
            foreach (var (m, c) in touchB) SetColor(m, Color.Lerp(c, GlowB, glowB));
        }

        static void SetColor(Material m, Color c)
        {
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        }
    }
}
