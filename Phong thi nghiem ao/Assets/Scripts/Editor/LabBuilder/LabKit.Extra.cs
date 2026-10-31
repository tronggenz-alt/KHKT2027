using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Khkt.EditorTools
{
    /// <summary>
    /// Các tiện ích dựng hình nâng cao cho Tầng 2: hộp bo cạnh (RoundBox), normal map sinh bằng code (TexN),
    /// lưới lá cây cong procedural (LeafMesh).
    /// </summary>
    public static partial class LabKit
    {
        public const string RootT2 = "Assets/Lab/Tang2";
        static readonly Dictionary<string, Mesh> roundBoxCache = new Dictionary<string, Mesh>();

        public static void EnsureT2Folders()
        {
            if (!AssetDatabase.IsValidFolder(RootT2)) AssetDatabase.CreateFolder("Assets/Lab", "Tang2");
            if (!AssetDatabase.IsValidFolder($"{RootT2}/Materials")) AssetDatabase.CreateFolder(RootT2, "Materials");
            if (!AssetDatabase.IsValidFolder($"{RootT2}/Meshes")) AssetDatabase.CreateFolder(RootT2, "Meshes");
            if (!AssetDatabase.IsValidFolder($"{RootT2}/Textures")) AssetDatabase.CreateFolder(RootT2, "Textures");
            if (!AssetDatabase.IsValidFolder($"{RootT2}/GiaiPhau")) AssetDatabase.CreateFolder(RootT2, "GiaiPhau");
        }

        // ---------- Overloads accepting GameObject parent ----------
        public static GameObject Box(GameObject parent, string name, Vector3 center, Vector3 size, Material m,
                                     bool collider = true, Vector3? euler = null)
            => Box(parent.transform, name, center, size, m, collider, euler);

        public static GameObject Cyl(GameObject parent, string name, Vector3 center, float radius, float height,
                                     Material m, bool collider = false, Vector3? euler = null)
            => Cyl(parent.transform, name, center, radius, height, m, collider, euler);

        public static GameObject Ball(Transform parent, string name, Vector3 center, float radius, Material m,
                                      bool collider = false)
            => Ball(parent, name, center, Vector3.one * (radius * 2f), m, collider);

        public static GameObject Ball(GameObject parent, string name, Vector3 center, float radius, Material m,
                                      bool collider = false)
            => Ball(parent.transform, name, center, Vector3.one * (radius * 2f), m, collider);

        public static GameObject Ball(GameObject parent, string name, Vector3 center, Vector3 diameter, Material m,
                                      bool collider = false)
            => Ball(parent.transform, name, center, diameter, m, collider);

        public static GameObject Panel(GameObject parent, string name, Vector3 center, Vector3 facing, float w, float h, Material m)
            => Panel(parent.transform, name, center, facing, w, h, m);

        public static GameObject RoundBox(GameObject parent, string name, Vector3 center, Vector3 size, float radius, Material m,
                                          bool collider = true, Vector3? euler = null)
            => RoundBox(parent.transform, name, center, size, radius, m, collider, euler);

        // ---------- Overloads for Label ----------
        public static GameObject Label(Transform parent, string name, string text, Vector3 position, float letterHeight, Color color)
        {
            var g = Label(parent, text, position, Vector3.forward, letterHeight, color);
            g.name = name;
            return g;
        }

        public static GameObject Label(GameObject parent, string name, string text, Vector3 position, float letterHeight, Color color)
        {
            var g = Label(parent.transform, text, position, Vector3.forward, letterHeight, color);
            g.name = name;
            return g;
        }

        public static GameObject Label(GameObject parent, string text, Vector3 position, Vector3 facing, float letterHeight, Color color)
            => Label(parent.transform, text, position, facing, letterHeight, color);

        // =====================================================================
        // 1. Hộp bo cạnh (RoundBox / Bevel Box)
        // =====================================================================

        /// <summary>
        /// Tạo hộp bo cạnh bằng code. R bo góc 2-6 mm bắt sáng chân thực như đồ nội thất thật.
        /// </summary>
        public static GameObject RoundBox(Transform parent, string name, Vector3 center, Vector3 size, float radius, Material m,
                                          bool collider = true, Vector3? euler = null)
        {
            float r = Mathf.Min(radius, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.45f);
            if (r <= 0.001f)
            {
                return Box(parent, name, center, size, m, collider, euler);
            }

            int sx = Mathf.RoundToInt(size.x * 1000);
            int sy = Mathf.RoundToInt(size.y * 1000);
            int sz = Mathf.RoundToInt(size.z * 1000);
            int sr = Mathf.RoundToInt(r * 1000);
            string key = $"RB3_{sx}_{sy}_{sz}_{sr}";

            Mesh mesh = GetOrCreateRoundBoxMesh(key, size, r);
            var g = MeshObj(parent, name, mesh, center, m, collider: false, euler: euler);
            if (collider)
            {
                var col = g.AddComponent<BoxCollider>();
                col.center = Vector3.zero;
                col.size = size;
            }
            return g;
        }

        static Mesh GetOrCreateRoundBoxMesh(string key, Vector3 size, float r)
        {
            if (roundBoxCache.TryGetValue(key, out var cached) && cached != null) return cached;
            EnsureT2Folders();
            string path = $"{RootT2}/Meshes/{key}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null)
            {
                roundBoxCache[key] = mesh;
                return mesh;
            }

            mesh = GenerateRoundBox(size, r);
            mesh.name = key;
            AssetDatabase.CreateAsset(mesh, path);
            roundBoxCache[key] = mesh;
            return mesh;
        }

        /// <summary>
        /// Hộp bo cạnh kín: mỗi mặt là một lưới phẳng trên mặt hộp, đẩy các điểm ngoài phần lõi ra mặt cầu bán kính r quanh lõi.
        /// Các điểm trên cạnh chung của hai mặt trùng nhau nên không hở khe. Tam giác quay mặt ra ngoài theo quy ước của Unity
        /// (Cross(b - a, c - a) hướng ra ngoài). Bản trước để hở 12 dải cạnh và lật mặt trên: nhìn thấy xuyên qua, nhấp nháy.
        /// </summary>
        static Mesh GenerateRoundBox(Vector3 size, float r)
        {
            Vector3 half = size * 0.5f;
            Vector3 inner = half - Vector3.one * r;
            const int B = 3;  // số đoạn trên nửa cung bo của mỗi mặt (cả cung 90° là 2B đoạn)
            float[] Axis(float inn)
            {
                var l = new List<float>();
                for (int k = B; k >= 1; k--) l.Add(-inn - r * Mathf.Tan(k * Mathf.PI / 4f / B));
                l.Add(-inn);
                l.Add(inn);
                for (int k = 1; k <= B; k++) l.Add(inn + r * Mathf.Tan(k * Mathf.PI / 4f / B));
                return l.ToArray();
            }
            var axes = new[] { Axis(inner.x), Axis(inner.y), Axis(inner.z) };
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int a = 0; a < 3; a++)
                for (int s = -1; s <= 1; s += 2)
                {
                    // u × v = pháp tuyến mặt (trục a, dấu s)
                    int ua = s > 0 ? (a + 1) % 3 : (a + 2) % 3, va = s > 0 ? (a + 2) % 3 : (a + 1) % 3;
                    float[] lu = axes[ua], lv = axes[va];
                    int b0 = verts.Count;
                    for (int i = 0; i < lu.Length; i++)
                        for (int j = 0; j < lv.Length; j++)
                        {
                            var p = Vector3.zero;
                            p[a] = s * half[a];
                            p[ua] = lu[i];
                            p[va] = lv[j];
                            var c = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                            var n = (p - c).normalized;
                            verts.Add(c + n * r);
                            norms.Add(n);
                            uvs.Add(new Vector2(lu[i] + half[ua], lv[j] + half[va]));
                        }
                    int row = lv.Length;
                    for (int i = 0; i < lu.Length - 1; i++)
                        for (int j = 0; j < lv.Length - 1; j++)
                        {
                            int p00 = b0 + i * row + j, p10 = p00 + row, p01 = p00 + 1, p11 = p10 + 1;
                            tris.Add(p00); tris.Add(p10); tris.Add(p01);
                            tris.Add(p10); tris.Add(p11); tris.Add(p01);
                        }
                }
            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        /// <summary>
        /// Tấm tam giác dày (đầu hồi mái, vách dưới gầm cầu thang): tam giác trong mặt phẳng y–z gồm hai điểm đáy (zA, yA), (zB, yB)
        /// và đỉnh (zP, yP), dày thick theo trục x, tâm ở x = 0 của nhóm cha. Mặt nào cũng quay ra ngoài.
        /// </summary>
        public static GameObject Triangle(Transform parent, string name, string meshName, Vector2 a, Vector2 b, Vector2 peak, float thick, Material m)
        {
            float h = thick / 2;
            Vector3 P(Vector2 q, float x) => new Vector3(x, q.y, q.x);
            EnsureT2Folders();
            string path = $"{RootT2}/Meshes/{meshName}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh { name = meshName };
            else mesh.Clear();
            var v = new List<Vector3>();
            var t = new List<int>();
            void Tri(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 outward)
            {
                if (Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), outward) < 0) { var tmp = p1; p1 = p2; p2 = tmp; }
                int i = v.Count;
                v.Add(p0); v.Add(p1); v.Add(p2);
                t.Add(i); t.Add(i + 1); t.Add(i + 2);
            }
            Vector3 a0 = P(a, -h), b0 = P(b, -h), c0 = P(peak, -h), a1 = P(a, h), b1 = P(b, h), c1 = P(peak, h);
            Vector3 mid = (a0 + b0 + c0 + a1 + b1 + c1) / 6f;
            Tri(a0, b0, c0, Vector3.left);
            Tri(a1, b1, c1, Vector3.right);
            foreach (var (p, q) in new[] { (a0, b0), (b0, c0), (c0, a0) })
            {
                var p1 = p + Vector3.right * thick;
                var q1 = q + Vector3.right * thick;
                var outward = ((p + q) / 2 + Vector3.right * h) - mid;
                Tri(p, q, q1, outward);
                Tri(p, q1, p1, outward);
            }
            mesh.SetVertices(v);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            if (isNew) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            g.AddComponent<MeshRenderer>().sharedMaterial = m;
            g.AddComponent<MeshCollider>().sharedMesh = mesh;
            return g;
        }

        // =====================================================================
        // 2. Normal Map sinh bằng code (TexN)
        // =====================================================================

        /// <summary>
        /// Sinh Normal map từ hàm độ cao (0..1) bằng bộ lọc Sobel, lưu TextureImporter.textureType = NormalMap.
        /// </summary>
        public static Texture2D TexN(string name, int w, int h, Func<int, int, float> heightFunc, float strength = 1.0f)
        {
            EnsureT2Folders();
            string path = $"{RootT2}/Textures/{name}.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            float[,] hMap = new float[w, h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    hMap[x, y] = heightFunc(x, y);

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            var px = new Color[w * h];

            for (int y = 0; y < h; y++)
            {
                int y0 = (y - 1 + h) % h;
                int y1 = (y + 1) % h;
                for (int x = 0; x < w; x++)
                {
                    int x0 = (x - 1 + w) % w;
                    int x1 = (x + 1) % w;

                    // Sobel filter
                    float dx = (hMap[x1, y0] + 2f * hMap[x1, y] + hMap[x1, y1]) -
                               (hMap[x0, y0] + 2f * hMap[x0, y] + hMap[x0, y1]);
                    float dy = (hMap[x0, y1] + 2f * hMap[x, y1] + hMap[x1, y1]) -
                               (hMap[x0, y0] + 2f * hMap[x, y0] + hMap[x1, y0]);

                    Vector3 normal = new Vector3(-dx * strength, -dy * strength, 1.0f).normalized;
                    px[y * w + x] = new Color(
                        normal.x * 0.5f + 0.5f,
                        normal.y * 0.5f + 0.5f,
                        normal.z * 0.5f + 0.5f,
                        1f
                    );
                }
            }

            tex.SetPixels(px);
            tex.Apply(true);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.NormalMap;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 4;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // =====================================================================
        // 3. Lưới lá cây procedural (LeafMesh)
        // =====================================================================

        /// <summary>
        /// Sinh lưới lá cây uốn cong nhẹ từ gốc đến ngọn với gân giữa và mép cong.
        /// </summary>
        public static Mesh LeafMesh(string name, float length = 0.3f, float width = 0.12f, float curvature = 0.05f)
        {
            EnsureT2Folders();
            string path = $"{RootT2}/Meshes/{name}.asset";
            var cached = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (cached != null) return cached;

            var mesh = new Mesh { name = name };
            const int segL = 6;
            const int segW = 4;

            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var colors = new List<Color>();
            var tris = new List<int>();

            for (int i = 0; i <= segL; i++)
            {
                float tL = (float)i / segL;
                float z = tL * length;
                // Biên dạng chiếc lá: phồng ở giữa, thon nhọn ở đầu
                float wCurve = Mathf.Sin(tL * Mathf.PI);
                float curW = width * wCurve;
                float droop = curvature * tL * tL;

                for (int j = 0; j <= segW; j++)
                {
                    float tW = (float)j / segW - 0.5f; // -0.5..+0.5
                    float x = tW * curW;
                    float y = -droop + curvature * 0.3f * (1f - 4f * tW * tW);

                    verts.Add(new Vector3(x, y, z));
                    normals.Add(Vector3.up);
                    uvs.Add(new Vector2(tW + 0.5f, tL));
                    // Vertex color R lưu trọng số gió để shader LaCay đung đưa ngọn lá
                    colors.Add(new Color(tL * tL, 0f, 0f, 1f));
                }
            }

            int rowStride = segW + 1;
            int front = verts.Count;
            // mặt dưới: bộ đỉnh riêng (pháp tuyến ngược) để RecalculateNormals không triệt tiêu pháp tuyến hai mặt
            for (int k = 0; k < front; k++)
            {
                verts.Add(verts[k]);
                normals.Add(Vector3.down);
                uvs.Add(uvs[k]);
                colors.Add(colors[k]);
            }
            for (int i = 0; i < segL; i++)
            {
                for (int j = 0; j < segW; j++)
                {
                    int v0 = i * rowStride + j;
                    int v1 = v0 + 1;
                    int v2 = (i + 1) * rowStride + j;
                    int v3 = v2 + 1;
                    tris.Add(v0); tris.Add(v2); tris.Add(v1);
                    tris.Add(v1); tris.Add(v2); tris.Add(v3);
                    tris.Add(front + v0); tris.Add(front + v1); tris.Add(front + v2);
                    tris.Add(front + v1); tris.Add(front + v3); tris.Add(front + v2);
                }
            }

            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
    }
}
