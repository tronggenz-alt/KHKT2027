using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Khkt.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Khkt.Demo.Bio.GiaiPhau
{
    [Serializable]
    public class Bp3dHeaderJson
    {
        public int phien_ban;
        public float[] nho;
        public float[] lon;
        public Bp3dNhomJson[] nhom;
    }

    [Serializable]
    public class Bp3dNhomJson
    {
        public string bp;
        public string lop;
        public int nv;
        public int ni;
        public bool uv;
        public bool i32;
        public float[] neo;
    }

    [Serializable]
    public class DanhMucRootJson
    {
        public int phien_ban;
        public DanhMucItemJson[] co_quan;
    }

    [Serializable]
    public class DanhMucItemJson
    {
        public string id;
        public string ten;
        public string icon;
        public string he;
        public string tep;
        public string[] bo_phan;
    }

    [Serializable]
    public class TracNghiemJson
    {
        public string cau;
        public string[] lua_chon;
        public string dap_an;
        public string giai_thich;
    }

    [Serializable]
    public class CoQuanJson
    {
        public string id;
        public string ten;
        public string chuong_trinh;
        public string[] vi_tri;
        public string[] cau_tao;
        public string[] chuc_nang;
        public string[] co_che;
        public string[] lien_he;
        public string[] diem_thi;
        public TracNghiemJson[] trac_nghiem;
    }

    public class HoSoCoQuan
    {
        public string id;
        public string ten;
        public string he;
        public string[] boPhan;
        public string chuongTrinh;
        public string[] viTri;
        public string[] cauTao;
        public string[] chucNang;
        public string[] coChe;
        public string[] lienHe;
        public string[] diemThi;
        public TracNghiemJson[] tracNghiem;
    }

    /// <summary>
    /// Đọc dữ liệu BodyParts3D (tận dụng trước lưới BP3D2 có sẵn trong cảnh; đọc bổ sung/dự phòng từ bp3d.bin.gz)
    /// và nạp 15 hồ sơ kiến thức cơ quan tiếng Việt từ StreamingAssets/biology_lab/sinh-hoc/kien-thuc/.
    /// </summary>
    public static class Bp3dReader
    {
        public const float Bp3dMinY = -0.61820f;
        public const float Bp3dMaxY = 3.55789f;
        public const float TargetHeightM = 1.70f;
        public const float FloorOffsetM = 0.02f;

        public static float BodyScale => TargetHeightM / (Bp3dMaxY - Bp3dMinY);
        public static Vector3 BodyLocalOffset => new Vector3(0f, FloorOffsetM - Bp3dMinY * BodyScale, 0f);

        static byte[] cachedDecompressed;
        static Bp3dHeaderJson cachedHeader;
        static int cachedDataStart;
        static int[] cachedGroupOffsets;

        static readonly Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>(16);
        static List<HoSoCoQuan> cachedOrgans;
        static Dictionary<string, HoSoCoQuan> organById;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            meshCache.Clear();
            cachedOrgans = null;
            organById = null;
        }

        public static string BioRootDir =>
            Path.Combine(Application.streamingAssetsPath, "biology_lab", "sinh-hoc");

        public static string Bp3dPath => Path.Combine(BioRootDir, "bp3d.bin.gz");

        public static Mesh LayLuoiLop(string lop, MucDoHoa muc)
        {
            if (string.IsNullOrEmpty(lop)) return null;
            bool simplified = muc == MucDoHoa.Thap;
            string key = simplified ? (lop + "_thap") : (lop + "_full");
            if (meshCache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            // 1) Ưu tiên tái sử dụng 3 lưới BP3D2_xuong / BP3D2_co / BP3D2_da đã có sẵn trên Bàn giải phẫu số
            Mesh baseMesh = TryReuseSceneMesh(lop);

            // 2) Nếu chưa có trong cảnh hoặc là lớp mach_do / mach_xanh / gan thì giải mã từ bp3d.bin.gz
            if (baseMesh == null)
            {
                baseMesh = DecodeLayerFromBinary(lop);
            }

            if (baseMesh == null) return null;

            if (simplified && baseMesh.vertexCount > 4500)
            {
                var decimated = SimplifyMeshSpatial(baseMesh, lop == "xuong" ? 0.022f : 0.028f);
                decimated.name = $"GP_{lop}_Thap";
                meshCache[key] = decimated;
                return decimated;
            }

            meshCache[key] = baseMesh;
            return baseMesh;
        }

        /// <summary>Lưới giải mã thẳng từ bp3d.bin.gz (luôn đọc được đỉnh, kể cả trong bản dựng), dùng cho bàn mổ người.</summary>
        public static Mesh LayLuoiDocDuoc(string lop)
        {
            string key = lop + "_doc";
            if (meshCache.TryGetValue(key, out var m) && m != null) return m;
            m = DecodeLayerFromBinary(lop);
            if (m != null) meshCache[key] = m;
            return m;
        }

        static Mesh TryReuseSceneMesh(string lop)
        {
            if (lop != "xuong" && lop != "co" && lop != "da") return null;
            var table = UnityEngine.Object.FindAnyObjectByType<DigitalAnatomyTable>(FindObjectsInactive.Include);
            if (table == null) return null;

            GameObject go = lop switch
            {
                "xuong" => table.layerSkeleton,
                "co" => table.layerMuscles,
                "da" => table.layerSkin,
                _ => null
            };
            if (go == null) return null;
            var mf = go.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null && mf.sharedMesh.vertexCount > 100)
                return mf.sharedMesh;
            return null;
        }

        static bool EnsureHeaderLoaded()
        {
            if (cachedHeader != null && cachedDecompressed != null) return true;
            if (!File.Exists(Bp3dPath))
            {
                Debug.LogWarning($"[GiaiPhau] Không tìm thấy {Bp3dPath}");
                return false;
            }

            using (var fs = File.OpenRead(Bp3dPath))
            using (var gz = new GZipStream(fs, CompressionMode.Decompress))
            using (var ms = new MemoryStream(4 * 1024 * 1024))
            {
                gz.CopyTo(ms);
                cachedDecompressed = ms.ToArray();
            }

            if (cachedDecompressed.Length < 12 ||
                cachedDecompressed[0] != (byte)'B' || cachedDecompressed[1] != (byte)'P' ||
                cachedDecompressed[2] != (byte)'3' || cachedDecompressed[3] != (byte)'D')
            {
                Debug.LogError("[GiaiPhau] Định dạng bp3d.bin.gz không hợp lệ.");
                cachedDecompressed = null;
                return false;
            }

            int jlen = BitConverter.ToInt32(cachedDecompressed, 8);
            string hdrJson = Encoding.UTF8.GetString(cachedDecompressed, 12, jlen);
            cachedHeader = JsonUtility.FromJson<Bp3dHeaderJson>(hdrJson);
            cachedDataStart = 12 + jlen;

            int n = cachedHeader?.nhom?.Length ?? 0;
            cachedGroupOffsets = new int[n];
            int offset = cachedDataStart;
            for (int i = 0; i < n; i++)
            {
                cachedGroupOffsets[i] = offset;
                var g = cachedHeader.nhom[i];
                int vertBytes = g.nv * 6;
                int uvBytes = g.uv ? g.nv * 4 : 0;
                int idxBytes = g.ni * (g.i32 ? 4 : 2);
                offset += vertBytes + uvBytes + idxBytes;
            }
            return n > 0;
        }

        static Mesh DecodeLayerFromBinary(string targetLop)
        {
            if (!EnsureHeaderLoaded()) return null;

            float[] nho = cachedHeader.nho;
            float[] lon = cachedHeader.lon;
            float stepX = (lon[0] - nho[0]) / 65535f;
            float stepY = (lon[1] - nho[1]) / 65535f;
            float stepZ = (lon[2] - nho[2]) / 65535f;

            var verts = new List<Vector3>(32768);
            var indices = new List<int>(65536);

            for (int gIdx = 0; gIdx < cachedHeader.nhom.Length; gIdx++)
            {
                var g = cachedHeader.nhom[gIdx];
                bool match = g.lop == targetLop || (targetLop == "co" && g.lop == "gan");
                if (!match) continue;

                int pos = cachedGroupOffsets[gIdx];
                int baseVertex = verts.Count;

                for (int v = 0; v < g.nv; v++)
                {
                    ushort qx = BitConverter.ToUInt16(cachedDecompressed, pos);
                    ushort qy = BitConverter.ToUInt16(cachedDecompressed, pos + 2);
                    ushort qz = BitConverter.ToUInt16(cachedDecompressed, pos + 4);
                    pos += 6;

                    float wx = -(nho[0] + qx * stepX);
                    float wy = nho[1] + qy * stepY;
                    float wz = nho[2] + qz * stepZ;
                    verts.Add(new Vector3(wx, wy, wz));
                }

                if (g.uv) pos += g.nv * 4;

                int prev = 0;
                int idxStep = g.i32 ? 4 : 2;
                int[] rawIdx = new int[g.ni];
                for (int k = 0; k < g.ni; k++)
                {
                    uint zz = g.i32
                        ? BitConverter.ToUInt32(cachedDecompressed, pos)
                        : BitConverter.ToUInt16(cachedDecompressed, pos);
                    pos += idxStep;
                    int delta = (int)(zz >> 1) ^ -((int)(zz & 1));
                    prev += delta;
                    rawIdx[k] = prev;
                }

                // Đảo mặt tam giác cho hệ tọa độ tay trái Unity khi lật trục X
                for (int k = 0; k + 2 < g.ni; k += 3)
                {
                    indices.Add(baseVertex + rawIdx[k]);
                    indices.Add(baseVertex + rawIdx[k + 2]);
                    indices.Add(baseVertex + rawIdx[k + 1]);
                }
            }

            if (verts.Count == 0 || indices.Count == 0) return null;

            var mesh = new Mesh
            {
                name = $"BP3D_Runtime_{targetLop}",
                indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            mesh.SetVertices(verts);
            mesh.SetIndices(indices, MeshTopology.Triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static Mesh SimplifyMeshSpatial(Mesh src, float cellSize)
        {
            var srcVerts = src.vertices;
            var srcTris = src.triangles;
            float invCell = 1f / Mathf.Max(0.005f, cellSize);

            var cellToNewIdx = new Dictionary<long, int>(srcVerts.Length / 2);
            var sumPos = new List<Vector3>(srcVerts.Length / 2);
            var sumCnt = new List<int>(srcVerts.Length / 2);
            var remap = new int[srcVerts.Length];

            for (int i = 0; i < srcVerts.Length; i++)
            {
                Vector3 p = srcVerts[i];
                long ix = Mathf.RoundToInt(p.x * invCell) + 4096L;
                long iy = Mathf.RoundToInt(p.y * invCell) + 4096L;
                long iz = Mathf.RoundToInt(p.z * invCell) + 4096L;
                long key = (ix & 0xFFFFFL) | ((iy & 0xFFFFFL) << 20) | ((iz & 0xFFFFFL) << 40);

                if (!cellToNewIdx.TryGetValue(key, out int newIdx))
                {
                    newIdx = sumPos.Count;
                    cellToNewIdx[key] = newIdx;
                    sumPos.Add(p);
                    sumCnt.Add(1);
                }
                else
                {
                    sumPos[newIdx] += p;
                    sumCnt[newIdx] += 1;
                }
                remap[i] = newIdx;
            }

            for (int i = 0; i < sumPos.Count; i++)
                sumPos[i] /= Mathf.Max(1, sumCnt[i]);

            var outTris = new List<int>(srcTris.Length / 2);
            for (int i = 0; i + 2 < srcTris.Length; i += 3)
            {
                int a = remap[srcTris[i]];
                int b = remap[srcTris[i + 1]];
                int c = remap[srcTris[i + 2]];
                if (a == b || b == c || a == c) continue;
                outTris.Add(a);
                outTris.Add(b);
                outTris.Add(c);
            }

            var mesh = new Mesh
            {
                indexFormat = sumPos.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            mesh.SetVertices(sumPos);
            mesh.SetTriangles(outTris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static List<HoSoCoQuan> TaiDanhMucCoQuan()
        {
            if (cachedOrgans != null && cachedOrgans.Count > 0) return cachedOrgans;

            cachedOrgans = new List<HoSoCoQuan>(15);
            organById = new Dictionary<string, HoSoCoQuan>(16);

            string kienThucDir = Path.Combine(BioRootDir, "kien-thuc");
            string danhMucPath = Path.Combine(kienThucDir, "danh-muc.json");
            if (!File.Exists(danhMucPath))
            {
                Debug.LogWarning($"[GiaiPhau] Không tìm thấy {danhMucPath}");
                return cachedOrgans;
            }

            var dm = JsonUtility.FromJson<DanhMucRootJson>(File.ReadAllText(danhMucPath, Encoding.UTF8));
            if (dm?.co_quan == null) return cachedOrgans;

            foreach (var item in dm.co_quan)
            {
                if (string.IsNullOrEmpty(item.id)) continue;
                var hoSo = new HoSoCoQuan
                {
                    id = item.id,
                    ten = item.ten,
                    he = item.he,
                    boPhan = item.bo_phan ?? Array.Empty<string>()
                };

                string detailPath = Path.Combine(kienThucDir, item.tep ?? (item.id + ".json"));
                if (File.Exists(detailPath))
                {
                    var detail = JsonUtility.FromJson<CoQuanJson>(File.ReadAllText(detailPath, Encoding.UTF8));
                    if (detail != null)
                    {
                        if (!string.IsNullOrEmpty(detail.ten)) hoSo.ten = detail.ten;
                        hoSo.chuongTrinh = detail.chuong_trinh ?? "";
                        hoSo.viTri = detail.vi_tri ?? Array.Empty<string>();
                        hoSo.cauTao = detail.cau_tao ?? Array.Empty<string>();
                        hoSo.chucNang = detail.chuc_nang ?? Array.Empty<string>();
                        hoSo.coChe = detail.co_che ?? Array.Empty<string>();
                        hoSo.lienHe = detail.lien_he ?? Array.Empty<string>();
                        hoSo.diemThi = detail.diem_thi ?? Array.Empty<string>();
                        hoSo.tracNghiem = detail.trac_nghiem ?? Array.Empty<TracNghiemJson>();
                    }
                }

                cachedOrgans.Add(hoSo);
                organById[hoSo.id] = hoSo;
            }

            return cachedOrgans;
        }

        public static HoSoCoQuan TimCoQuan(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            TaiDanhMucCoQuan();
            return organById.TryGetValue(id, out var hs) ? hs : null;
        }
    }
}
