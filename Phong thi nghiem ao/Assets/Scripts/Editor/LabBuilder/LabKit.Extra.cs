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
            string key = $"RBox_{sx}_{sy}_{sz}_{sr}";

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

        static Mesh GenerateRoundBox(Vector3 size, float r)
        {
            var mesh = new Mesh();
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            Vector3 half = size * 0.5f;
            Vector3 inner = new Vector3(
                Mathf.Max(0f, half.x - r),
                Mathf.Max(0f, half.y - r),
                Mathf.Max(0f, half.z - r)
            );

            // Sinh 8 góc cầu (mỗi góc 2x2 phân đoạn)
            const int segs = 2;
            int cornerOffset = 0;
            for (int cx = -1; cx <= 1; cx += 2)
            for (int cy = -1; cy <= 1; cy += 2)
            for (int cz = -1; cz <= 1; cz += 2)
            {
                Vector3 cCenter = new Vector3(cx * inner.x, cy * inner.y, cz * inner.z);
                int baseIndex = vertices.Count;

                for (int lat = 0; lat <= segs; lat++)
                {
                    float theta = (lat / (float)segs) * (Mathf.PI * 0.5f);
                    float sinT = Mathf.Sin(theta);
                    float cosT = Mathf.Cos(theta);

                    for (int lon = 0; lon <= segs; lon++)
                    {
                        float phi = (lon / (float)segs) * (Mathf.PI * 0.5f);
                        Vector3 n = new Vector3(
                            cx * Mathf.Cos(phi) * sinT,
                            cy * cosT,
                            cz * Mathf.Sin(phi) * sinT
                        );
                        if (n.sqrMagnitude > 1e-5f) n.Normalize(); else n = new Vector3(cx, cy, cz).normalized;

                        Vector3 v = cCenter + n * r;
                        vertices.Add(v);
                        normals.Add(n);
                        uvs.Add(new Vector2(v.x + half.x, v.z + half.z));
                    }
                }

                int stride = segs + 1;
                for (int lat = 0; lat < segs; lat++)
                {
                    for (int lon = 0; lon < segs; lon++)
                    {
                        int i0 = baseIndex + lat * stride + lon;
                        int i1 = i0 + 1;
                        int i2 = baseIndex + (lat + 1) * stride + lon;
                        int i3 = i2 + 1;

                        bool flip = (cx * cy * cz) < 0;
                        if (flip)
                        {
                            triangles.Add(i0); triangles.Add(i2); triangles.Add(i1);
                            triangles.Add(i1); triangles.Add(i2); triangles.Add(i3);
                        }
                        else
                        {
                            triangles.Add(i0); triangles.Add(i1); triangles.Add(i2);
                            triangles.Add(i1); triangles.Add(i3); triangles.Add(i2);
                        }
                    }
                }
                cornerOffset++;
            }

            // Mặt phẳng chính 6 mặt (Top, Bottom, Front, Back, Left, Right)
            void AddQuad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 n)
            {
                int b = vertices.Count;
                vertices.Add(p0); vertices.Add(p1); vertices.Add(p2); vertices.Add(p3);
                normals.Add(n); normals.Add(n); normals.Add(n); normals.Add(n);
                uvs.Add(new Vector2(p0.x, p0.z));
                uvs.Add(new Vector2(p1.x, p1.z));
                uvs.Add(new Vector2(p2.x, p2.z));
                uvs.Add(new Vector2(p3.x, p3.z));

                triangles.Add(b); triangles.Add(b + 1); triangles.Add(b + 2);
                triangles.Add(b); triangles.Add(b + 2); triangles.Add(b + 3);
            }

            // Top (+Y)
            AddQuad(new Vector3(-inner.x, half.y, -inner.z),
                    new Vector3(inner.x, half.y, -inner.z),
                    new Vector3(inner.x, half.y, inner.z),
                    new Vector3(-inner.x, half.y, inner.z), Vector3.up);

            // Bottom (-Y)
            AddQuad(new Vector3(-inner.x, -half.y, inner.z),
                    new Vector3(inner.x, -half.y, inner.z),
                    new Vector3(inner.x, -half.y, -inner.z),
                    new Vector3(-inner.x, -half.y, -inner.z), Vector3.down);

            // Front (+Z)
            AddQuad(new Vector3(-inner.x, -inner.y, half.z),
                    new Vector3(inner.x, -inner.y, half.z),
                    new Vector3(inner.x, inner.y, half.z),
                    new Vector3(-inner.x, inner.y, half.z), Vector3.forward);

            // Back (-Z)
            AddQuad(new Vector3(inner.x, -inner.y, -half.z),
                    new Vector3(-inner.x, -inner.y, -half.z),
                    new Vector3(-inner.x, inner.y, -half.z),
                    new Vector3(inner.x, inner.y, -half.z), Vector3.back);

            // Right (+X)
            AddQuad(new Vector3(half.x, -inner.y, -inner.z),
                    new Vector3(half.x, -inner.y, inner.z),
                    new Vector3(half.x, inner.y, inner.z),
                    new Vector3(half.x, inner.y, -inner.z), Vector3.right);

            // Left (-X)
            AddQuad(new Vector3(-half.x, -inner.y, inner.z),
                    new Vector3(-half.x, -inner.y, -inner.z),
                    new Vector3(-half.x, inner.y, -inner.z),
                    new Vector3(-half.x, inner.y, inner.z), Vector3.left);

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
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
            for (int i = 0; i < segL; i++)
            {
                for (int j = 0; j < segW; j++)
                {
                    int v0 = i * rowStride + j;
                    int v1 = v0 + 1;
                    int v2 = (i + 1) * rowStride + j;
                    int v3 = v2 + 1;

                    // Mặt trên
                    tris.Add(v0); tris.Add(v2); tris.Add(v1);
                    tris.Add(v1); tris.Add(v2); tris.Add(v3);

                    // Mặt dưới (Double-sided)
                    tris.Add(v0); tris.Add(v1); tris.Add(v2);
                    tris.Add(v1); tris.Add(v3); tris.Add(v2);
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
