// Hình học dựng bằng code (procedural): mặt tròn xoay (lathe) cho cốc, bình tam giác, ống nghiệm; ống uốn (tube) cho dây dẫn, cầu muối;
// khối chất lỏng có mặt khum. Không tải model ngoài → bản build nhỏ.
using System.Collections.Generic;
using UnityEngine;

namespace PhongThiNghiem
{
    public static class LabHinh
    {
        public static Mesh Lathe(IList<Vector2> p, int phan = 40)
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            for (int i = 0; i < p.Count; i++)
                for (int j = 0; j <= phan; j++)
                {
                    float a = j * Mathf.PI * 2f / phan;
                    v.Add(new Vector3(p[i].x * Mathf.Cos(a), p[i].y, p[i].x * Mathf.Sin(a)));
                }
            int hang = phan + 1;
            for (int i = 0; i < p.Count - 1; i++)
                for (int j = 0; j < phan; j++)
                {
                    int a = i * hang + j, b = a + hang;
                    t.Add(a); t.Add(b); t.Add(a + 1);
                    t.Add(a + 1); t.Add(b); t.Add(b + 1);
                }
            var m = new Mesh { name = "lathe" };
            m.SetVertices(v);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        public static Vector3 CatmullRom(Vector3[] p, float u)
        {
            int n = p.Length - 1;
            if (n < 1) return p[0];
            u = Mathf.Clamp01(u) * n;
            int s = Mathf.Min(n - 1, Mathf.FloorToInt(u));
            float t = u - s;
            Vector3 p0 = p[Mathf.Max(s - 1, 0)], p1 = p[s], p2 = p[s + 1], p3 = p[Mathf.Min(s + 2, n)];
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        public static Mesh Ong(Vector3[] diem, float r, int doan = 48, int vong = 10)
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            for (int i = 0; i <= doan; i++)
            {
                float u = (float)i / doan;
                Vector3 c = CatmullRom(diem, u);
                Vector3 tiep = (CatmullRom(diem, Mathf.Min(1f, u + 0.01f)) - CatmullRom(diem, Mathf.Max(0f, u - 0.01f))).normalized;
                Vector3 phap = Vector3.Cross(tiep, Vector3.up);
                if (phap.sqrMagnitude < 1e-6f) phap = Vector3.Cross(tiep, Vector3.right);
                phap.Normalize();
                Vector3 phu = Vector3.Cross(tiep, phap);
                for (int j = 0; j <= vong; j++)
                {
                    float a = j * Mathf.PI * 2f / vong;
                    v.Add(c + r * (Mathf.Cos(a) * phap + Mathf.Sin(a) * phu));
                }
            }
            int hang = vong + 1;
            for (int i = 0; i < doan; i++)
                for (int j = 0; j < vong; j++)
                {
                    int a = i * hang + j, b = a + hang;
                    t.Add(a); t.Add(a + 1); t.Add(b);
                    t.Add(a + 1); t.Add(b + 1); t.Add(b);
                }
            var m = new Mesh { name = "ong" };
            m.SetVertices(v);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        // ---------- profile dụng cụ (đơn vị mét – cùng kích thước bản Web 3D)
        public static Vector2[] Coc(float r, float h) =>
            new[] { new Vector2(0f, 0.0015f), new Vector2(r * 0.94f, 0f), new Vector2(r, 0.012f), new Vector2(r, h), new Vector2(r * 1.07f, h + 0.004f) };

        public static Vector2[] BinhTamGiac(float r, float h, float rc, float hc) =>
            new[] { new Vector2(0f, 0.0015f), new Vector2(r * 0.95f, 0f), new Vector2(r, 0.014f), new Vector2(rc * 1.02f, hc), new Vector2(rc, h), new Vector2(rc * 1.18f, h + 0.005f) };

        public static Vector2[] OngNghiem(float r, float h)
        {
            var p = new List<Vector2>();
            for (int i = 0; i <= 10; i++)
            {
                float a = i / 10f * Mathf.PI / 2f;
                p.Add(new Vector2(r * Mathf.Sin(a) + 1e-4f, r - r * Mathf.Cos(a)));
            }
            p.Add(new Vector2(r, h));
            p.Add(new Vector2(r * 1.14f, h + 0.003f));
            return p.ToArray();
        }

        public static float BanKinhTai(Vector2[] p, float y, float day = 0.0025f)
        {
            for (int i = 1; i < p.Length; i++)
            {
                Vector2 a = p[i - 1], b = p[i];
                if (y >= a.y && y <= b.y)
                {
                    float t = Mathf.Approximately(b.y, a.y) ? 0f : (y - a.y) / (b.y - a.y);
                    return Mathf.Max(0.0005f, a.x + (b.x - a.x) * t - day);
                }
            }
            return Mathf.Max(0.0005f, p[p.Length - 1].x - day);
        }

        /// <summary>Khối chất lỏng trong bình (lathe kín), cao h, mặt khum lõm.</summary>
        public static Mesh ChatLong(Vector2[] p, float h, float day = 0.0025f)
        {
            h = Mathf.Max(0.006f, h);
            const float y0 = 0.003f;
            var pts = new List<Vector2> { new Vector2(0f, y0) };
            for (int i = 0; i <= 14; i++)
            {
                float y = y0 + (h - y0) * i / 14f;
                pts.Add(new Vector2(BanKinhTai(p, y, day), y));
            }
            float rw = BanKinhTai(p, h, day), m = Mathf.Min(0.004f, rw * 0.18f);
            pts[pts.Count - 1] = new Vector2(rw, h + m);
            for (int i = 1; i <= 8; i++)
            {
                float x = rw * (1f - i / 8f);
                pts.Add(new Vector2(Mathf.Max(0f, x), h + m * Mathf.Pow(x / rw, 4f)));
            }
            return Lathe(pts, 32);
        }

        public static float TheTichDenH(Vector2[] p, float h, float day = 0.0025f)
        {
            float v = 0f;
            for (float y = 0.003f; y < h; y += 0.0005f)
            {
                float r = BanKinhTai(p, y, day);
                v += Mathf.PI * r * r * 0.0005f;
            }
            return v * 1e6f;
        }

        public static float HDeCoTheTich(Vector2[] p, float mL, float day, float hMax)
        {
            float a = 0.003f, b = hMax;
            for (int i = 0; i < 30; i++)
            {
                float g = (a + b) / 2f;
                if (TheTichDenH(p, g, day) < mL) a = g; else b = g;
            }
            return (a + b) / 2f;
        }
    }
}
