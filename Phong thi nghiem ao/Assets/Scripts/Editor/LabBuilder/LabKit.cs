using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Khkt.EditorTools
{
    /// <summary>
    /// Bộ đồ nghề dựng cảnh bằng khối cơ bản: hộp, trụ, cầu, khối tròn xoay (lathe), tấm dán, chữ,
    /// vật liệu URP và ảnh vân sinh bằng code. Vật liệu, lưới và ảnh được lưu thành asset trong
    /// Assets/Lab để cảnh lưu xong vẫn giữ được; dựng lại lần nữa thì ghi đè lên chính các asset đó.
    /// </summary>
    internal static class LabKit
    {
        public const string Root = "Assets/Lab";

        static Shader lit;
        static Font font;

        /// <summary>Vật liệu chữ dùng chung (shader Khkt/ChuTrongCanh: chữ bị tường che như vật thật).</summary>
        public static Material TextMaterial { get; private set; }
        public static Font LabelFont => font;
        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();

        public static void Begin()
        {
            materials.Clear();
            meshes.Clear();
            lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) throw new InvalidOperationException("Không tìm thấy shader URP Lit: project phải dùng URP.");
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Folder("Assets", "Lab");
            Folder(Root, "Materials");
            Folder(Root, "Meshes");
            Folder(Root, "Textures");

            var textShader = AssetDatabase.LoadAssetAtPath<Shader>($"{Root}/Shaders/ChuTrongCanh.shader");
            if (textShader == null)
            {
                Debug.LogWarning("[KHKT] Thiếu Assets/Lab/Shaders/ChuTrongCanh.shader: chữ sẽ hiện xuyên tường.");
                TextMaterial = font.material;
            }
            else
            {
                string path = $"{Root}/Materials/ChuTrongCanh.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                {
                    m = new Material(textShader);
                    AssetDatabase.CreateAsset(m, path);
                }
                m.shader = textShader;
                m.renderQueue = -1;  // theo shader (Transparent-100): vẽ chữ thành một nhóm, không xen giữa các lọ thủy tinh
                m.mainTexture = font.material.mainTexture;
                TextMaterial = m;
            }
        }

        static void Folder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) AssetDatabase.CreateFolder(parent, name);
        }

        // ---------- Vật liệu ----------

        public static Material Mat(string name, Color color, float smoothness = 0.2f, float metallic = 0f,
                                   Texture2D texture = null, Vector2? tiling = null, Color? emission = null,
                                   bool transparent = false)
        {
            if (materials.TryGetValue(name, out var cached)) return cached;
            string path = $"{Root}/Materials/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = m == null;
            if (isNew) m = new Material(lit);
            m.shader = lit;
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            m.SetTexture("_BaseMap", texture);
            m.SetTextureScale("_BaseMap", tiling ?? Vector2.one);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            else
            {
                m.DisableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.black);
            }
            if (transparent) MakeTransparent(m);
            else MakeOpaque(m);
            if (isNew) AssetDatabase.CreateAsset(m, path);
            else EditorUtility.SetDirty(m);
            materials[name] = m;
            return m;
        }

        static void MakeTransparent(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", (float)CullMode.Off);  // kính nhìn được cả hai mặt
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetShaderPassEnabled("DepthOnly", false);
            m.SetShaderPassEnabled("ShadowCaster", false);
        }

        static void MakeOpaque(Material m)
        {
            m.SetFloat("_Surface", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.One);
            m.SetFloat("_DstBlend", (float)BlendMode.Zero);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.Zero);
            m.SetFloat("_ZWrite", 1f);
            m.SetFloat("_Cull", (float)CullMode.Back);
            m.SetOverrideTag("RenderType", "Opaque");
            m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = -1;
            m.SetShaderPassEnabled("DepthOnly", true);
            m.SetShaderPassEnabled("ShadowCaster", true);
        }

        // ---------- Ảnh vân ----------

        /// <summary>Sinh ảnh từ hàm điểm ảnh, lưu PNG vào Assets/Lab/Textures và trả về asset đã nhập.</summary>
        public static Texture2D Tex(string name, int w, int h, Func<int, int, Color> pixel, bool repeat = true)
        {
            string path = $"{Root}/Textures/{name}.png";
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = pixel(x, y);
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 4;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ---------- Khối ----------

        public static Transform Group(string name, Transform parent, Vector3 position = default, float yaw = 0f)
        {
            var g = new GameObject(name).transform;
            g.SetParent(parent, false);
            g.localPosition = position;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            return g;
        }

        public static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material m,
                                     bool collider = true, Vector3? euler = null)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Place(g, parent, name, center, size, m, euler);
            if (!collider) Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }

        /// <summary>Trụ đứng: tâm, bán kính, chiều cao. euler để xoay trục (mặc định trục dọc).</summary>
        public static GameObject Cyl(Transform parent, string name, Vector3 center, float radius, float height,
                                     Material m, bool collider = false, Vector3? euler = null)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Place(g, parent, name, center, new Vector3(radius * 2, height / 2, radius * 2), m, euler);
            if (!collider) Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }

        public static GameObject Ball(Transform parent, string name, Vector3 center, Vector3 diameter, Material m,
                                      bool collider = false)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Place(g, parent, name, center, diameter, m, null);
            if (!collider) Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }

        /// <summary>Tấm phẳng (tranh, biển) mặt nhìn về hướng facing.</summary>
        public static GameObject Panel(Transform parent, string name, Vector3 center, Vector3 facing, float w, float h, Material m)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = center;
            g.transform.localRotation = Quaternion.LookRotation(-facing, Vector3.up);
            g.transform.localScale = new Vector3(w, h, 1);
            g.GetComponent<Renderer>().sharedMaterial = m;
            Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }

        static void Place(GameObject g, Transform parent, string name, Vector3 center, Vector3 scale, Material m, Vector3? euler)
        {
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = center;
            if (euler.HasValue) g.transform.localEulerAngles = euler.Value;
            g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = m;
        }

        /// <summary>Dòng chữ (TextMesh) đặt trên mặt nhìn về hướng facing, cao letterHeight mét mỗi dòng.</summary>
        public static GameObject Label(Transform parent, string text, Vector3 position, Vector3 facing, float letterHeight, Color color)
        {
            var g = new GameObject("Chữ: " + text.Replace('\n', ' '));
            g.transform.SetParent(parent, false);
            g.transform.localPosition = position;
            g.transform.localRotation = Quaternion.LookRotation(-facing, Vector3.up);
            var tm = g.AddComponent<TextMesh>();
            tm.font = font;
            tm.fontSize = 64;
            tm.characterSize = letterHeight * 10f / tm.fontSize;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            tm.text = text;
            g.GetComponent<MeshRenderer>().sharedMaterial = TextMaterial;
            return g;
        }

        // ---------- Khối tròn xoay ----------

        /// <summary>
        /// Lưới tròn xoay từ đường biên (bán kính, độ cao) đi từ dưới lên. Bắt đầu và kết thúc ở bán kính 0
        /// để kín đáy/nắp. Lặp lại một điểm để tạo cạnh sắc (pháp tuyến không bị làm mượt qua đó).
        /// </summary>
        public static Mesh Lathe(string name, int segments, params Vector2[] profile)
        {
            if (meshes.TryGetValue(name, out var cached)) return cached;
            if (profile.Length < 2) throw new ArgumentException($"Lathe {name}: cần ít nhất 2 điểm biên");
            string path = $"{Root}/Meshes/{name}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh { name = name };
            else mesh.Clear();

            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            int n = profile.Length, row = segments + 1;
            for (int i = 0; i < n; i++)
                for (int s = 0; s <= segments; s++)
                {
                    float a = 2 * Mathf.PI * s / segments;
                    v.Add(new Vector3(Mathf.Cos(a) * profile[i].x, profile[i].y, Mathf.Sin(a) * profile[i].x));
                    uv.Add(new Vector2((float)s / segments, (float)i / (n - 1)));
                }
            for (int i = 0; i < n - 1; i++)
                for (int s = 0; s < segments; s++)
                {
                    int a = i * row + s, b = a + row;
                    t.Add(a); t.Add(b); t.Add(a + 1);
                    t.Add(a + 1); t.Add(b); t.Add(b + 1);
                }
            mesh.SetVertices(v);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            if (isNew) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            meshes[name] = mesh;
            return mesh;
        }

        public static GameObject MeshObj(Transform parent, string name, Mesh mesh, Vector3 position, Material m,
                                         bool collider = false, Vector3? euler = null, float scale = 1f)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = position;
            if (euler.HasValue) g.transform.localEulerAngles = euler.Value;
            g.transform.localScale = Vector3.one * scale;
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            g.AddComponent<MeshRenderer>().sharedMaterial = m;
            if (collider)
            {
                var c = g.AddComponent<BoxCollider>();
                c.center = mesh.bounds.center;
                c.size = mesh.bounds.size;
            }
            return g;
        }

        // ---------- Tiện ích ----------

        public static void NoShadow(GameObject g)
        {
            foreach (var r in g.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>Đánh dấu tĩnh để Unity gộp lô vẽ (static batching), trừ các nhánh có tên trong skip.</summary>
        public static void MarkStatic(Transform t, Func<Transform, bool> skip)
        {
            if (skip(t)) return;
            GameObjectUtility.SetStaticEditorFlags(t.gameObject,
                StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic
                | StaticEditorFlags.ReflectionProbeStatic);  // đầu dò phản chiếu nướng sẵn chỉ chụp vật tĩnh
            foreach (Transform c in t) MarkStatic(c, skip);
        }
    }
}
