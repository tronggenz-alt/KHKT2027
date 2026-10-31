using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Khkt.EditorTools
{
    public static class Bp3dImporter
    {
        [Serializable]
        private class Bp3dHeader
        {
            public int phien_ban;
            public float k;
            public float[] nho;
            public float[] lon;
            public List<Bp3dGroup> nhom;
            public string nguon;
            public string lien_ket;
        }

        [Serializable]
        private class Bp3dGroup
        {
            public string bp;
            public string lop;
            public int nv;
            public int ni;
            public bool uv;
            public bool i32;
            public float u_max;
        }

        public static Dictionary<string, Mesh> ImportBodyParts3D()
        {
            var meshes = new Dictionary<string, Mesh>();
            string outDir = "Assets/Lab/Tang2/GiaiPhau";
            if (!AssetDatabase.IsValidFolder("Assets/Lab/Tang2"))
                AssetDatabase.CreateFolder("Assets/Lab", "Tang2");
            if (!AssetDatabase.IsValidFolder(outDir))
                AssetDatabase.CreateFolder("Assets/Lab/Tang2", "GiaiPhau");

            // đã nhập ở lần dựng trước thì dùng lại (giải mã ~250 nghìn đỉnh mất vài giây)
            var cachedLayers = new Dictionary<string, Mesh>();
            foreach (var l in new[] { "xuong", "co", "da" })
            {
                var m = AssetDatabase.LoadAssetAtPath<Mesh>($"{outDir}/BP3D2_{l}.asset");
                if (m != null) cachedLayers[l] = m;
            }
            if (cachedLayers.Count == 3) return cachedLayers;

            string binGzPath = Path.Combine(Directory.GetCurrentDirectory(), "phong_sinh", "tu_app_cu", "bp3d.bin.gz");
            if (!File.Exists(binGzPath))
            {
                // Thử đường dẫn từ thư mục cha
                binGzPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "phong_sinh", "tu_app_cu", "bp3d.bin.gz");
            }

            if (!File.Exists(binGzPath))
            {
                Debug.LogWarning($"[Bp3dImporter] Không tìm thấy '{binGzPath}'. Tạo lưới giải phẫu thay thế.");
                return CreateFallbackMeshes(outDir);
            }

            try
            {
                byte[] rawData;
                using (var fs = File.OpenRead(binGzPath))
                using (var gz = new GZipStream(fs, CompressionMode.Decompress))
                using (var ms = new MemoryStream())
                {
                    gz.CopyTo(ms);
                    rawData = ms.ToArray();
                }

                if (rawData.Length < 12 || Encoding.ASCII.GetString(rawData, 0, 4) != "BP3D")
                {
                    Debug.LogWarning("[Bp3dImporter] Tiêu đề BP3D không hợp lệ. Dùng lưới thay thế.");
                    return CreateFallbackMeshes(outDir);
                }

                int version = BitConverter.ToInt32(rawData, 4);
                int jsonLen = BitConverter.ToInt32(rawData, 8);
                string jsonStr = Encoding.UTF8.GetString(rawData, 12, jsonLen);
                var header = JsonUtility.FromJson<Bp3dHeader>(jsonStr);

                int offset = 12 + jsonLen;
                Vector3 minBox = new Vector3(header.nho[0], header.nho[1], header.nho[2]);
                Vector3 maxBox = new Vector3(header.lon[0], header.lon[1], header.lon[2]);
                Vector3 boxSize = maxBox - minBox;

                // Gom đỉnh và tam giác theo 4 lớp: xuong, co, noi_tang, da
                var layerVertices = new Dictionary<string, List<Vector3>> { { "xuong", new List<Vector3>() }, { "co", new List<Vector3>() }, { "noi_tang", new List<Vector3>() }, { "da", new List<Vector3>() } };
                var layerIndices = new Dictionary<string, List<int>> { { "xuong", new List<int>() }, { "co", new List<int>() }, { "noi_tang", new List<int>() }, { "da", new List<int>() } };

                foreach (var g in header.nhom)
                {
                    string lop = g.lop;
                    if (!layerVertices.ContainsKey(lop)) lop = "xuong";

                    int baseVertex = layerVertices[lop].Count;

                    // 1. Đọc đỉnh Uint16
                    for (int i = 0; i < g.nv; i++)
                    {
                        ushort qx = BitConverter.ToUInt16(rawData, offset); offset += 2;
                        ushort qy = BitConverter.ToUInt16(rawData, offset); offset += 2;
                        ushort qz = BitConverter.ToUInt16(rawData, offset); offset += 2;

                        // Chuyển sang tọa độ thế giới của mô hình (đổi x sang Unity: x_unity = -x_web)
                        float vx = -(minBox.x + (qx / 65535f) * boxSize.x);
                        float vy = minBox.y + (qy / 65535f) * boxSize.y;
                        float vz = minBox.z + (qz / 65535f) * boxSize.z;
                        layerVertices[lop].Add(new Vector3(vx, vy, vz));
                    }

                    // 2. Bỏ qua UV nếu có
                    if (g.uv)
                    {
                        offset += g.nv * 4; // 2 * uint16
                    }

                    // 3. Đọc chỉ số tam giác (mã hóa hiệu zigzag)
                    int prevIdx = 0;
                    for (int i = 0; i < g.ni; i++)
                    {
                        long zz;
                        if (g.i32)
                        {
                            zz = BitConverter.ToUInt32(rawData, offset); offset += 4;
                        }
                        else
                        {
                            zz = BitConverter.ToUInt16(rawData, offset); offset += 2;
                        }

                        // Zigzag decode
                        long diff = (zz >> 1) ^ -(zz & 1);
                        int idx = (int)(prevIdx + diff);
                        prevIdx = idx;

                        layerIndices[lop].Add(baseVertex + idx);
                    }
                }

                // Lật x (hệ tay phải của trang web → hệ tay trái của Unity) làm ngược chiều tam giác: đảo lại để mặt quay ra ngoài.
                // Bản trước bỏ qua bước này nên mô hình bị lộn trái (thấy mặt trong, pháp tuyến hướng vào trong, tối và nhấp nháy).
                foreach (var idx in layerIndices.Values)
                    for (int i = 0; i + 2 < idx.Count; i += 3)
                    {
                        int t = idx[i + 1];
                        idx[i + 1] = idx[i + 2];
                        idx[i + 2] = t;
                    }

                // Tạo mesh cho từng lớp
                foreach (var kvp in layerVertices)
                {
                    string lop = kvp.Key;
                    if (kvp.Value.Count == 0 || layerIndices[lop].Count == 0) continue;

                    var mesh = new Mesh { name = $"BP3D_{lop}" };
                    if (kvp.Value.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

                    mesh.SetVertices(kvp.Value);
                    mesh.SetTriangles(layerIndices[lop], 0);
                    mesh.RecalculateNormals();
                    mesh.RecalculateBounds();

                    string assetPath = $"{outDir}/BP3D2_{lop}.asset";
                    AssetDatabase.CreateAsset(mesh, assetPath);
                    meshes[lop] = mesh;
                }

                AssetDatabase.SaveAssets();
                Debug.Log($"[Bp3dImporter] Đã nhập thành công BodyParts3D: {meshes.Count} lớp.");
                return meshes;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Bp3dImporter] Lỗi giải mã BP3D: {ex.Message}. Tạo lưới thay thế.");
                return CreateFallbackMeshes(outDir);
            }
        }

        static Dictionary<string, Mesh> CreateFallbackMeshes(string outDir)
        {
            var meshes = new Dictionary<string, Mesh>();
            string[] layers = { "xuong", "co", "noi_tang", "da" };
            foreach (var l in layers)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                var mf = go.GetComponent<MeshFilter>();
                var mesh = UnityEngine.Object.Instantiate(mf.sharedMesh);
                mesh.name = $"BP3D_{l}";
                UnityEngine.Object.DestroyImmediate(go);

                string assetPath = $"{outDir}/BP3D_{l}.asset";
                AssetDatabase.CreateAsset(mesh, assetPath);
                meshes[l] = mesh;
            }
            AssetDatabase.SaveAssets();
            return meshes;
        }
    }
}
