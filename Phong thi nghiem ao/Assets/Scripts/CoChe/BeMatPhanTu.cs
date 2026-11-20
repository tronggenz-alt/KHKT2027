using System.Collections.Generic;
using UnityEngine;

namespace Khkt.CoChe
{
    /// <summary>
    /// Bề mặt phân tử như 3Dmol (mục 04 CHEMISTRY): VDW (hợp các quả cầu van der Waals), SAS (bán kính + đầu dò 1,4 Å),
    /// SES (gần đúng: hợp mềm các quả cầu vdW, lấp kẽ hở giữa nguyên tử). Lưới dựng bằng Surface Nets trên trường khoảng cách;
    /// mỗi đỉnh mang màu nguyên tử gần nhất (màu đỉnh, vẽ bằng Particles/Lit trong suốt 0,76).
    /// </summary>
    public static class BeMatPhanTu
    {
        public enum Loai { Tat = 0, VDW = 1, SAS = 2, SES = 3 }

        public static Mesh Dung(IList<Vector3> tam, IList<float> banKinh, IList<string> nguyenTo, Loai loai, System.Func<string, Color> mauNguyenTo, float buoc = 0.3f)
        {
            int n = tam.Count;
            if (n == 0 || loai == Loai.Tat) return null;
            float them = loai == Loai.SAS ? 1.4f : 0f;
            var r = new float[n];
            for (int i = 0; i < n; i++) r[i] = banKinh[i] + them;

            Vector3 lo = Vector3.one * float.MaxValue, hi = Vector3.one * float.MinValue;
            for (int i = 0; i < n; i++) { lo = Vector3.Min(lo, tam[i] - Vector3.one * (r[i] + 0.8f)); hi = Vector3.Max(hi, tam[i] + Vector3.one * (r[i] + 0.8f)); }
            var size = hi - lo;
            float maxDim = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            if (maxDim / buoc > 110f) buoc = maxDim / 110f;                 // phân tử lớn: thưa lưới để giữ ≤ ~1,3 triệu điểm
            int nx = Mathf.CeilToInt(size.x / buoc) + 1, ny = Mathf.CeilToInt(size.y / buoc) + 1, nz = Mathf.CeilToInt(size.z / buoc) + 1;
            var f = new float[nx * ny * nz];
            int Id(int x, int y, int z) => (z * ny + y) * nx + x;

            const float kMem = 0.6f;                                          // độ mềm cho SES
            for (int z = 0; z < nz; z++)
                for (int y = 0; y < ny; y++)
                    for (int x = 0; x < nx; x++)
                    {
                        var p = lo + new Vector3(x, y, z) * buoc;
                        float d;
                        if (loai == Loai.SES)
                        {   // smooth-min dạng mũ: -k·ln Σ e^{-dᵢ/k}
                            float tong = 0f;
                            for (int i = 0; i < n; i++) { float di = (p - tam[i]).magnitude - r[i]; if (di < 3f) tong += Mathf.Exp(-di / kMem); }
                            d = tong > 0f ? -kMem * Mathf.Log(tong) : 3f;
                        }
                        else
                        {
                            d = float.MaxValue;
                            for (int i = 0; i < n; i++) { float di = (p - tam[i]).magnitude - r[i]; if (di < d) d = di; }
                        }
                        f[Id(x, y, z)] = d;
                    }

            // 1. một đỉnh cho mỗi ô có đổi dấu = trung bình các điểm cắt trên 12 cạnh
            var oDinh = new int[(nx - 1) * (ny - 1) * (nz - 1)];
            int IdO(int x, int y, int z) => (z * (ny - 1) + y) * (nx - 1) + x;
            var dinh = new List<Vector3>();
            int[,] canh = { { 0, 1 }, { 2, 3 }, { 4, 5 }, { 6, 7 }, { 0, 2 }, { 1, 3 }, { 4, 6 }, { 5, 7 }, { 0, 4 }, { 1, 5 }, { 2, 6 }, { 3, 7 } };
            var gv = new float[8]; var gp = new Vector3[8];
            for (int z = 0; z < nz - 1; z++)
                for (int y = 0; y < ny - 1; y++)
                    for (int x = 0; x < nx - 1; x++)
                    {
                        int am = 0;
                        for (int c = 0; c < 8; c++)
                        {
                            int dx = c & 1, dy = (c >> 1) & 1, dz = (c >> 2) & 1;
                            gv[c] = f[Id(x + dx, y + dy, z + dz)];
                            gp[c] = lo + new Vector3(x + dx, y + dy, z + dz) * buoc;
                            if (gv[c] < 0f) am++;
                        }
                        if (am == 0 || am == 8) { oDinh[IdO(x, y, z)] = -1; continue; }
                        Vector3 tb = Vector3.zero; int dem = 0;
                        for (int e = 0; e < 12; e++)
                        {
                            float a = gv[canh[e, 0]], b = gv[canh[e, 1]];
                            if ((a < 0f) == (b < 0f)) continue;
                            float t = a / (a - b);
                            tb += Vector3.Lerp(gp[canh[e, 0]], gp[canh[e, 1]], t); dem++;
                        }
                        oDinh[IdO(x, y, z)] = dinh.Count;
                        dinh.Add(tb / dem);
                    }

            // 2. mỗi cạnh lưới đổi dấu → một tứ giác nối 4 ô quanh cạnh, hướng ra ngoài (phía f > 0)
            var tamGiac = new List<int>();
            void Tu(int a, int b, int c, int d, Vector3 ngoai)
            {
                if (a < 0 || b < 0 || c < 0 || d < 0) return;
                var nn = Vector3.Cross(dinh[b] - dinh[a], dinh[c] - dinh[a]);
                if (Vector3.Dot(nn, ngoai) < 0f) { tamGiac.Add(a); tamGiac.Add(c); tamGiac.Add(b); tamGiac.Add(a); tamGiac.Add(d); tamGiac.Add(c); }
                else { tamGiac.Add(a); tamGiac.Add(b); tamGiac.Add(c); tamGiac.Add(a); tamGiac.Add(c); tamGiac.Add(d); }
            }
            for (int z = 0; z < nz; z++)
                for (int y = 0; y < ny; y++)
                    for (int x = 0; x < nx; x++)
                    {
                        float v0 = f[Id(x, y, z)];
                        if (x < nx - 1 && y > 0 && z > 0 && y < ny - 1 && z < nz - 1)
                        {
                            float v1 = f[Id(x + 1, y, z)];
                            if ((v0 < 0f) != (v1 < 0f))
                                Tu(oDinh[IdO(x, y - 1, z - 1)], oDinh[IdO(x, y, z - 1)], oDinh[IdO(x, y, z)], oDinh[IdO(x, y - 1, z)], v0 < 0f ? Vector3.right : Vector3.left);
                        }
                        if (y < ny - 1 && x > 0 && z > 0 && x < nx - 1 && z < nz - 1)
                        {
                            float v1 = f[Id(x, y + 1, z)];
                            if ((v0 < 0f) != (v1 < 0f))
                                Tu(oDinh[IdO(x - 1, y, z - 1)], oDinh[IdO(x, y, z - 1)], oDinh[IdO(x, y, z)], oDinh[IdO(x - 1, y, z)], v0 < 0f ? Vector3.up : Vector3.down);
                        }
                        if (z < nz - 1 && x > 0 && y > 0 && x < nx - 1 && y < ny - 1)
                        {
                            float v1 = f[Id(x, y, z + 1)];
                            if ((v0 < 0f) != (v1 < 0f))
                                Tu(oDinh[IdO(x - 1, y - 1, z)], oDinh[IdO(x, y - 1, z)], oDinh[IdO(x, y, z)], oDinh[IdO(x - 1, y, z)], v0 < 0f ? Vector3.forward : Vector3.back);
                        }
                    }
            if (tamGiac.Count == 0) return null;

            // 3. màu theo từng đỉnh = màu nguyên tử gần mặt nhất (nội suy mượt trên tam giác, như 3Dmol)
            var mau = new List<Color>(dinh.Count);
            for (int v = 0; v < dinh.Count; v++)
            {
                int gan = 0; float best = float.MaxValue;
                for (int i = 0; i < n; i++) { float d = (dinh[v] - tam[i]).magnitude - r[i]; if (d < best) { best = d; gan = i; } }
                var c = mauNguyenTo != null ? mauNguyenTo(nguyenTo[gan]) : Color.white; c.a = 0.76f;
                mau.Add(c);
            }
            var mesh = new Mesh { name = "Bề mặt " + loai, indexFormat = dinh.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            mesh.SetVertices(dinh);
            mesh.SetColors(mau);
            mesh.SetTriangles(tamGiac, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
