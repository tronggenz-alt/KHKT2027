using System;
using System.Collections.Generic;
using UnityEngine;

namespace Khkt.Demo.Bio.GiaiPhau
{
    /// <summary>Dựng lưới hữu cơ cho bàn mổ: khối biến dạng liền mịn có UV, ống quét liên tục (ruột, mạch, thần kinh).</summary>
    public static class BanMoLuoi
    {
        /// <summary>Khối từ mặt cầu đơn vị: hinh(hướng đơn vị) trả vị trí (trong hộp ~[-0,5; 0,5]³). UV bọc quanh để vân lặp đều.</summary>
        public static Mesh Khoi(string ten, int seg, int ring, Func<Vector3, Vector3> hinh)
        {
            var v = new List<Vector3>((seg + 1) * (ring + 1));
            var uv = new List<Vector2>(v.Capacity);
            var t = new List<int>(seg * ring * 6);
            for (int r = 0; r <= ring; r++)
            {
                float th = Mathf.PI * r / ring;
                for (int s = 0; s <= seg; s++)
                {
                    float ph = 2f * Mathf.PI * s / seg;
                    var d = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                    v.Add(hinh != null ? hinh(d) : d * 0.5f);
                    uv.Add(new Vector2(s / (float)seg * 2f, r / (float)ring));
                }
            }
            for (int r = 0; r < ring; r++)
                for (int s = 0; s < seg; s++)
                {
                    int a = r * (seg + 1) + s, b = a + seg + 1;
                    t.Add(a); t.Add(a + 1); t.Add(b);
                    t.Add(b); t.Add(a + 1); t.Add(b + 1);
                }
            var m = new Mesh { name = ten };
            if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            HanMuiKhau(m, seg, ring);
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }

        /// <summary>Pháp tuyến liền ở đường nối kinh tuyến 0/360° (cùng vị trí, khác UV).</summary>
        static void HanMuiKhau(Mesh m, int seg, int ring)
        {
            m.RecalculateNormals();
            var n = m.normals;
            for (int r = 0; r <= ring; r++)
            {
                int a = r * (seg + 1), b = a + seg;
                var tb = (n[a] + n[b]).normalized;
                n[a] = n[b] = tb;
            }
            // hai cực: trung bình mọi đỉnh trùng
            for (int cuc = 0; cuc < 2; cuc++)
            {
                int r = cuc == 0 ? 0 : ring;
                var tong = Vector3.zero;
                for (int s = 0; s <= seg; s++) tong += n[r * (seg + 1) + s];
                tong.Normalize();
                for (int s = 0; s <= seg; s++) n[r * (seg + 1) + s] = tong;
            }
            m.normals = n;
        }

        public static float Nhieu3(Vector3 p, float tanSo, int hat)
        {
            // nhiễu trơn đơn giản (tổng sin lệch pha) đủ để mặt cơ quan không tròn trịa như quả bóng
            float a = Mathf.Sin(p.x * tanSo * 1.7f + hat) * Mathf.Sin(p.y * tanSo * 2.3f + hat * 0.37f) * Mathf.Sin(p.z * tanSo * 1.9f + hat * 0.71f);
            float b = Mathf.Sin(p.x * tanSo * 3.9f + hat * 1.3f + p.z * 2f) * Mathf.Sin(p.y * tanSo * 4.1f - hat) * 0.5f;
            return a + b;
        }

        /// <summary>Đường cong mượt Catmull-Rom qua các điểm.</summary>
        public static List<Vector3> Muot(IList<Vector3> p, int moiDoan)
        {
            var kq = new List<Vector3>();
            if (p.Count == 1) { kq.Add(p[0]); return kq; }
            for (int k = 0; k < p.Count - 1; k++)
            {
                Vector3 p0 = p[Mathf.Max(0, k - 1)], p1 = p[k], p2 = p[k + 1], p3 = p[Mathf.Min(p.Count - 1, k + 2)];
                for (int i = 0; i < moiDoan; i++)
                {
                    float f = i / (float)moiDoan;
                    kq.Add(0.5f * (2f * p1 + (-p0 + p2) * f + (2f * p0 - 5f * p1 + 4f * p2 - p3) * f * f + (-p0 + 3f * p1 - 3f * p2 + p3) * f * f * f));
                }
            }
            kq.Add(p[p.Count - 1]);
            return kq;
        }
    }

    /// <summary>
    /// Ống quét theo đường cong với khung quay tối thiểu; bán kính từng vòng có thể đổi mỗi khung (sóng nhu động).
    /// Hai đầu thu nhỏ tròn lại nên không hở.
    /// </summary>
    public sealed class OngQuet
    {
        public readonly Mesh mesh;
        readonly Vector3[] tam, phap, phu;
        readonly float[] chieuDai;
        readonly int vong, canh;
        readonly Vector3[] dinh;
        public float TongDai { get; }
        public int SoVong => vong;

        public OngQuet(string ten, IList<Vector3> duong, int canh)
        {
            this.canh = canh;
            vong = duong.Count;
            tam = new Vector3[vong]; phap = new Vector3[vong]; phu = new Vector3[vong]; chieuDai = new float[vong];
            for (int i = 0; i < vong; i++) tam[i] = duong[i];
            // tiếp tuyến + khung quay tối thiểu
            var tt = new Vector3[vong];
            for (int i = 0; i < vong; i++)
            {
                var d = tam[Mathf.Min(vong - 1, i + 1)] - tam[Mathf.Max(0, i - 1)];
                tt[i] = d.sqrMagnitude > 1e-12f ? d.normalized : Vector3.forward;
            }
            var n0 = Vector3.Cross(tt[0], Mathf.Abs(tt[0].y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            phap[0] = n0; phu[0] = Vector3.Cross(tt[0], n0);
            for (int i = 1; i < vong; i++)
            {
                var q = Quaternion.FromToRotation(tt[i - 1], tt[i]);
                phap[i] = (q * phap[i - 1]).normalized;
                phu[i] = Vector3.Cross(tt[i], phap[i]).normalized;
                chieuDai[i] = chieuDai[i - 1] + Vector3.Distance(tam[i - 1], tam[i]);
            }
            TongDai = chieuDai[vong - 1];
            dinh = new Vector3[vong * (canh + 1)];
            var uv = new Vector2[dinh.Length];
            for (int i = 0; i < vong; i++)
                for (int k = 0; k <= canh; k++)
                    uv[i * (canh + 1) + k] = new Vector2(chieuDai[i] / Mathf.Max(1e-5f, TongDai), k / (float)canh);
            var tri = new List<int>((vong - 1) * canh * 6);
            for (int i = 0; i < vong - 1; i++)
                for (int k = 0; k < canh; k++)
                {
                    int a = i * (canh + 1) + k, b = a + canh + 1;
                    // chiều quấn để pháp tuyến hướng ra ngoài ống (trước đây bị lộn trong ra ngoài)
                    tri.Add(a); tri.Add(a + 1); tri.Add(b);
                    tri.Add(a + 1); tri.Add(b + 1); tri.Add(b);
                }
            mesh = new Mesh { name = ten };
            mesh.MarkDynamic();
            CapNhat(null);
            mesh.uv = uv;
            mesh.SetTriangles(tri, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
        }

        /// <summary>banKinh(t theo chiều dài 0..1) cho bán kính mỗi vòng; null lần đầu (chưa có hình) dùng 1.</summary>
        public void CapNhat(Func<float, float> banKinh, bool tinhPhapTuyen = false)
        {
            for (int i = 0; i < vong; i++)
            {
                float t = chieuDai[i] / Mathf.Max(1e-5f, TongDai);
                float r = banKinh != null ? banKinh(t) : 0.001f;
                // bo tròn hai đầu
                float dau = Mathf.Min(i, vong - 1 - i);
                if (dau < 2) r *= dau == 0 ? 0.25f : 0.8f;
                for (int k = 0; k <= canh; k++)
                {
                    float a = 2f * Mathf.PI * k / canh;
                    dinh[i * (canh + 1) + k] = tam[i] + (phap[i] * Mathf.Cos(a) + phu[i] * Mathf.Sin(a)) * r;
                }
            }
            mesh.vertices = dinh;
            if (tinhPhapTuyen) { mesh.RecalculateNormals(); mesh.RecalculateBounds(); }
        }

        public Vector3 DiemTai(float t)
        {
            float d = Mathf.Clamp01(t) * TongDai;
            for (int i = 1; i < vong; i++)
                if (chieuDai[i] >= d) return Vector3.Lerp(tam[i - 1], tam[i], Mathf.InverseLerp(chieuDai[i - 1], chieuDai[i], d));
            return tam[vong - 1];
        }
    }
}
