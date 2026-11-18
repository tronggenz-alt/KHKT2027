using System;
using Khkt.Demo;
using UnityEngine;
using UnityEngine.UI;

namespace Khkt.HoaLy
{
    /// <summary>
    /// Một mesh động cho cả hình vẽ của trang: hạt tròn, đoạn thẳng, hình chữ nhật, dải dưới đường cong.
    /// Mỗi khung: BatDau() → các lệnh vẽ → KetThuc(). Bộ đệm cấp sẵn, chỉ nới (gấp đôi) khi thiếu chỗ — không cấp phát mỗi khung.
    /// Tọa độ là tọa độ cục bộ của RectTransform (gốc ở pivot).
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class LuoiVe : MaskableGraphic
    {
        const byte Hat = 0, Doan = 1, Hop = 2, Dai = 3, TamGiac = 4;
        const int ToiDa = 15000;   // ≤ 60 000 đỉnh (chỉ số 16 bit)

        Vector4[] p = new Vector4[256];
        Vector2[] q = new Vector2[256];
        float[] w = new float[256];
        Color32[] c = new Color32[256];
        byte[] k = new byte[256];
        int n;

        static Texture2D tron;
        static readonly Vector4 UvDac = new Vector4(0.5f, 0.5f, 0, 0);

        public int SoHinh => n;

        /// <summary>Kết cấu hạt tròn trắng viền mềm 64×64 (tâm đặc dùng cho hình phẳng).</summary>
        public static Texture2D KetCauTron
        {
            get
            {
                if (tron != null) return tron;
                const int S = 64;
                tron = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Hạt tròn Hóa lý" };
                var px = new Color32[S * S];
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        float dx = x + 0.5f - S / 2f, dy = y + 0.5f - S / 2f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(S / 2f - 1f - d + 0.5f);
                        // vầng sáng nhẹ ở tâm để hạt có khối
                        byte v = (byte)(255 - Mathf.Clamp01(d / (S / 2f)) * 40);
                        px[y * S + x] = new Color32(v, v, v, (byte)(a * 255));
                    }
                tron.SetPixels32(px);
                tron.Apply(false, true);
                return tron;
            }
        }

        public override Texture mainTexture => KetCauTron;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        public void BatDau() => n = 0;

        public void KetThuc() => SetVerticesDirty();

        bool Them(byte kieu, Vector4 pp, float ww, Color32 cc, Vector2 qq = default)
        {
            if (n >= ToiDa) return false;
            if (n == p.Length)
            {
                int m = Math.Min(ToiDa, n * 2);
                Array.Resize(ref p, m); Array.Resize(ref q, m); Array.Resize(ref w, m); Array.Resize(ref c, m); Array.Resize(ref k, m);
            }
            p[n] = pp; q[n] = qq; w[n] = ww; c[n] = cc; k[n] = kieu;
            n++;
            return true;
        }

        public void VeHat(float x, float y, float banKinh, Color32 mau) => Them(Hat, new Vector4(x, y, 0, 0), banKinh, mau);

        public void VeDoan(float x0, float y0, float x1, float y1, float day, Color32 mau) => Them(Doan, new Vector4(x0, y0, x1, y1), day, mau);

        public void VeDoan(Vector2 a, Vector2 b, float day, Color32 mau) => Them(Doan, new Vector4(a.x, a.y, b.x, b.y), day, mau);

        public void VeHop(float x0, float y0, float x1, float y1, Color32 mau) => Them(Hop, new Vector4(x0, y0, x1, y1), 0, mau);

        public void VeHop(Rect r, Color32 mau) => VeHop(r.xMin, r.yMin, r.xMax, r.yMax, mau);

        /// <summary>Khung chữ nhật rỗng (4 cạnh dày day).</summary>
        public void VeKhung(Rect r, float day, Color32 mau)
        {
            VeHop(r.xMin, r.yMin, r.xMax, r.yMin + day, mau);
            VeHop(r.xMin, r.yMax - day, r.xMax, r.yMax, mau);
            VeHop(r.xMin, r.yMin + day, r.xMin + day, r.yMax - day, mau);
            VeHop(r.xMax - day, r.yMin + day, r.xMax, r.yMax - day, mau);
        }

        /// <summary>Dải từ đáy yDay lên tới đoạn (x0,y0)–(x1,y1): tô diện tích dưới đường cong.</summary>
        public void VeDai(float x0, float y0, float x1, float y1, float yDay, Color32 mau) => Them(Dai, new Vector4(x0, y0, x1, y1), yDay, mau);

        /// <summary>Tứ giác bất kỳ a-b-c-d (theo thứ tự vòng).</summary>
        public void VeTuGiac(Vector2 a, Vector2 b, Vector2 cc, Vector2 d, Color32 mau)
        {
            if (Them(TamGiac, new Vector4(a.x, a.y, b.x, b.y), 0, mau, cc)) { }
            Them(TamGiac, new Vector4(a.x, a.y, cc.x, cc.y), 0, mau, d);
        }

        /// <summary>Đường gấp khúc qua các điểm (đã đổi sang tọa độ cục bộ).</summary>
        public void VeDuong(Vector2[] diem, int so, float day, Color32 mau)
        {
            for (int i = 1; i < so; i++) VeDoan(diem[i - 1], diem[i], day, mau);
        }

        /// <summary>Đoạn nét đứt.</summary>
        public void VeNetDut(Vector2 a, Vector2 b, float day, float net, Color32 mau)
        {
            float L = Vector2.Distance(a, b);
            if (L < 1e-3f) return;
            Vector2 d = (b - a) / L;
            for (float s = 0; s < L; s += net * 2) VeDoan(a + d * s, a + d * Mathf.Min(L, s + net), day, mau);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            int v = 0;
            for (int i = 0; i < n; i++)
            {
                var pp = p[i];
                var cc = c[i];
                switch (k[i])
                {
                    case Hat:
                    {
                        float r = w[i];
                        vh.AddVert(new Vector3(pp.x - r, pp.y - r), cc, new Vector4(0, 0, 0, 0));
                        vh.AddVert(new Vector3(pp.x - r, pp.y + r), cc, new Vector4(0, 1, 0, 0));
                        vh.AddVert(new Vector3(pp.x + r, pp.y + r), cc, new Vector4(1, 1, 0, 0));
                        vh.AddVert(new Vector3(pp.x + r, pp.y - r), cc, new Vector4(1, 0, 0, 0));
                        break;
                    }
                    case Doan:
                    {
                        float dx = pp.z - pp.x, dy = pp.w - pp.y;
                        float L = Mathf.Sqrt(dx * dx + dy * dy);
                        if (L < 1e-4f) continue;
                        float h = w[i] * 0.5f;
                        float nx = -dy / L * h, ny = dx / L * h;
                        // kéo dài nửa bề dày ở hai đầu để nối liền các đoạn
                        float ex = dx / L * h * 0.5f, ey = dy / L * h * 0.5f;
                        vh.AddVert(new Vector3(pp.x - ex + nx, pp.y - ey + ny), cc, UvDac);
                        vh.AddVert(new Vector3(pp.z + ex + nx, pp.w + ey + ny), cc, UvDac);
                        vh.AddVert(new Vector3(pp.z + ex - nx, pp.w + ey - ny), cc, UvDac);
                        vh.AddVert(new Vector3(pp.x - ex - nx, pp.y - ey - ny), cc, UvDac);
                        break;
                    }
                    case Hop:
                        vh.AddVert(new Vector3(pp.x, pp.y), cc, UvDac);
                        vh.AddVert(new Vector3(pp.x, pp.w), cc, UvDac);
                        vh.AddVert(new Vector3(pp.z, pp.w), cc, UvDac);
                        vh.AddVert(new Vector3(pp.z, pp.y), cc, UvDac);
                        break;
                    case Dai:
                        vh.AddVert(new Vector3(pp.x, w[i]), cc, UvDac);
                        vh.AddVert(new Vector3(pp.x, pp.y), cc, UvDac);
                        vh.AddVert(new Vector3(pp.z, pp.w), cc, UvDac);
                        vh.AddVert(new Vector3(pp.z, w[i]), cc, UvDac);
                        break;
                    default:   // tam giác (đỉnh thứ 4 trùng đỉnh 3)
                        vh.AddVert(new Vector3(pp.x, pp.y), cc, UvDac);
                        vh.AddVert(new Vector3(pp.z, pp.w), cc, UvDac);
                        vh.AddVert(new Vector3(q[i].x, q[i].y), cc, UvDac);
                        vh.AddVert(new Vector3(q[i].x, q[i].y), cc, UvDac);
                        break;
                }
                vh.AddTriangle(v, v + 1, v + 2);
                vh.AddTriangle(v + 2, v + 3, v);
                v += 4;
            }
        }

        public static LuoiVe Tao(Transform cha, string ten)
        {
            var go = new GameObject(ten, typeof(RectTransform), typeof(CanvasRenderer));
            var rt = (RectTransform)go.transform;
            rt.SetParent(cha, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            var lv = go.AddComponent<LuoiVe>();
            lv.color = Color.white;
            return lv;
        }
    }
}
