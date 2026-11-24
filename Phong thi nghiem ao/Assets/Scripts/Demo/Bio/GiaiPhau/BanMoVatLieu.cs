using System.Collections.Generic;
using UnityEngine;

namespace Khkt.Demo.Bio.GiaiPhau
{
    /// <summary>
    /// Vật liệu mô sống cho bàn mổ: URP Lit (mẫu trong Resources để shader và biến thể bản đồ pháp tuyến có trong bản dựng),
    /// vân bề mặt sinh theo thủ tục cho từng loại mô (da, cơ vân, gan, phổi, mỡ, ruột, màng), bóng ướt.
    /// </summary>
    public static class BanMoVatLieu
    {
        public enum Van { Tron = 0, DaEch, DaNguoi, Co, Gan, Phoi, Mo, Ruot, Mang, Tim, Xuong, Than, Khay, Vai, CoBung, SacTo }

        static readonly Dictionary<Van, Texture2D> mau = new Dictionary<Van, Texture2D>();
        static readonly Dictionary<Van, Texture2D> phap = new Dictionary<Van, Texture2D>();
        static Material mauLit;
        const int N = 256;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void DatLai() { mau.Clear(); phap.Clear(); mauLit = null; }

        static Material Mau()
        {
            if (mauLit != null) return mauLit;
            mauLit = Resources.Load<Material>("BanMoMoUot");
            if (mauLit == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit");
                if (sh != null) { mauLit = new Material(sh); mauLit.EnableKeyword("_NORMALMAP"); }
            }
            return mauLit;
        }

        /// <summary>Tạo vật liệu mô: màu nền, loại vân, độ bóng (mô ướt 0,6 – 0,85), độ lặp vân.</summary>
        public static Material Tao(Color c, Van van, float bong, float lap = 2f, float noi = 1f)
        {
            var goc = Mau();
            Material m;
            if (goc == null)
            {
                m = new Material(GiaiPhauSanKhau3D.TimShaderMatCat());
                m.SetColor("_BaseColor", c);
                m.SetFloat("_Smoothness", bong);
                return m;
            }
            m = new Material(goc);
            m.SetColor("_BaseColor", c);
            // mặt mô ướt bóng nhưng không như nhựa: hạ nhẹ độ bóng truyền vào
            m.SetFloat("_Smoothness", Mathf.Clamp01(bong * 0.82f));
            m.SetFloat("_Metallic", 0f);
            if (van != Van.Tron)
            {
                var a = Anh(van, out var n);
                m.SetTexture("_BaseMap", a);
                m.SetTextureScale("_BaseMap", Vector2.one * lap);
                m.SetTexture("_BumpMap", n);
                m.SetFloat("_BumpScale", noi * 0.55f);
                m.EnableKeyword("_NORMALMAP");
            }
            else
            {
                m.SetTexture("_BaseMap", Texture2D.whiteTexture);
                m.SetTexture("_BumpMap", null);
                m.DisableKeyword("_NORMALMAP");
            }
            // Không dùng chế độ trong suốt: biến thể shader trong suốt có thể bị loại khỏi bản dựng (mẫu trong Resources là mờ đục)
            if (false)
            {
                // màng mỏng hơi trong
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = 3000;
            }
            return m;
        }

        // ------------------------------------------------------------------ nhiễu

        static float Hash(int x, int y, int s)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + s * 2246822519);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xffffff) / 16777215f;
            }
        }

        // nhiễu giá trị lặp chu kỳ p
        static float Nhieu(float x, float y, int p, int s)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            int X0 = ((x0 % p) + p) % p, Y0 = ((y0 % p) + p) % p, X1 = (X0 + 1) % p, Y1 = (Y0 + 1) % p;
            float a = Hash(X0, Y0, s), b = Hash(X1, Y0, s), c = Hash(X0, Y1, s), d = Hash(X1, Y1, s);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static float Fbm(float u, float v, int p, int oct, int s)
        {
            float t = 0, w = 0.5f, sum = 0;
            for (int o = 0; o < oct; o++)
            {
                int pp = p << o;
                t += w * Nhieu(u * pp, v * pp, pp, s + o * 17);
                sum += w; w *= 0.5f;
            }
            return t / sum;
        }

        // nhiễu tế bào (Worley) lặp chu kỳ p: khoảng cách tới hạt gần nhất, gần nhì
        static void TeBao(float u, float v, int p, int s, out float f1, out float f2)
        {
            float x = u * p, y = v * p;
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            f1 = f2 = 9f;
            for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                {
                    int cx = xi + i, cy = yi + j;
                    int hx = ((cx % p) + p) % p, hy = ((cy % p) + p) % p;
                    float px = cx + Hash(hx, hy, s), py = cy + Hash(hx, hy, s + 7);
                    float d = Mathf.Sqrt((px - x) * (px - x) + (py - y) * (py - y));
                    if (d < f1) { f2 = f1; f1 = d; } else if (d < f2) f2 = d;
                }
        }

        /// <summary>Độ cao vân (0..1) và độ sáng màu (0..1, nhân với màu nền) của loại mô tại (u, v) ∈ [0,1)².</summary>
        static void Mau1(Van van, float u, float v, out float cao, out float sang)
        {
            switch (van)
            {
                case Van.DaEch:
                    {
                        // bụng ếch: nền trắng ngà, đốm xám mờ, nốt sần nhỏ
                        float dom = Fbm(u, v, 4, 4, 3);
                        TeBao(u, v, 28, 11, out float f1, out _);
                        cao = 0.6f * (1f - Mathf.Clamp01(f1 * 1.6f)) + 0.4f * Fbm(u, v, 16, 2, 5);
                        sang = Mathf.Lerp(1f, 0.72f, Mathf.SmoothStep(0.52f, 0.68f, dom)) * (0.93f + 0.07f * cao);
                        break;
                    }
                case Van.DaNguoi:
                    {
                        float n = Fbm(u, v, 8, 4, 21);
                        TeBao(u, v, 48, 23, out float f1, out _);
                        cao = 0.35f * n + 0.65f * Mathf.Clamp01(f1 * 1.2f);
                        sang = 0.94f + 0.08f * n;
                        break;
                    }
                case Van.Co:
                    {
                        // bó sợi cơ vân chạy dọc v: sọc mảnh + bó lớn, có vách mô liên kết
                        float bo = Fbm(u * 0.5f, v * 0.06f, 8, 3, 31);
                        float soi = 0.5f + 0.5f * Mathf.Sin((u * 64f + bo * 6f) * Mathf.PI * 2f);
                        TeBao(u * 1f, v * 0.25f, 10, 37, out float f1, out float f2);
                        float vach = Mathf.Clamp01((f2 - f1) * 6f);
                        cao = 0.55f * soi + 0.45f * vach;
                        sang = Mathf.Lerp(0.78f, 1.05f, cao) * (0.9f + 0.15f * Fbm(u, v, 4, 3, 41));
                        break;
                    }
                case Van.CoBung:
                    {
                        // cơ thẳng bụng ếch: sợi dọc, các vách gân ngang chia khúc
                        Mau1(Van.Co, u, v, out cao, out sang);
                        float ngang = Mathf.Abs(Mathf.Repeat(v + 0.05f * Fbm(u, v, 4, 2, 151), 1f) - 0.5f);
                        float vach = Mathf.SmoothStep(0.44f, 0.49f, ngang);
                        cao = Mathf.Lerp(cao, 0.15f, vach);
                        sang = Mathf.Lerp(sang, 1.15f, vach * 0.8f);
                        break;
                    }
                case Van.SacTo:
                    {
                        // màng bụng thành lưng ếch: nhạt, có tế bào sắc tố đen hình sao lấm tấm
                        TeBao(u, v, 30, 161, out float f1, out _);
                        float dom = Mathf.SmoothStep(0.22f, 0.10f, f1) * Mathf.SmoothStep(0.45f, 0.65f, Fbm(u, v, 6, 3, 163));
                        cao = 0.4f * Fbm(u, v, 8, 3, 165);
                        sang = Mathf.Lerp(1f, 0.18f, dom);
                        break;
                    }
                case Van.Gan:
                    {
                        // tiểu thùy gan hình đa giác ~1 mm
                        TeBao(u, v, 22, 51, out float f1, out float f2);
                        float bien = Mathf.Clamp01((f2 - f1) * 5f);
                        cao = bien;
                        sang = Mathf.Lerp(0.78f, 1.04f, bien) * (0.9f + 0.12f * Fbm(u, v, 4, 3, 53));
                        break;
                    }
                case Van.Phoi:
                    {
                        // phế nang lấm tấm, tiểu thùy viền sẫm, vệt bụi than nhẹ
                        TeBao(u, v, 40, 61, out float f1, out _);
                        TeBao(u, v, 7, 63, out float g1, out float g2);
                        float thuy = Mathf.Clamp01((g2 - g1) * 4f);
                        cao = 0.6f * Mathf.Clamp01(f1 * 1.3f) + 0.4f * thuy;
                        sang = Mathf.Lerp(0.82f, 1.05f, thuy) * Mathf.Lerp(1f, 0.85f, Mathf.SmoothStep(0.62f, 0.8f, Fbm(u, v, 4, 3, 65)));
                        break;
                    }
                case Van.Mo:
                    {
                        // tiểu thùy mỡ vàng, vách mỏng có mạch nhỏ
                        TeBao(u, v, 14, 71, out float f1, out float f2);
                        float vach = Mathf.Clamp01((f2 - f1) * 7f);
                        cao = vach * (0.7f + 0.3f * (1f - f1));
                        sang = Mathf.Lerp(0.8f, 1.06f, vach);
                        break;
                    }
                case Van.Ruot:
                    {
                        // nếp vòng ngang + mạch thanh mạc mảnh
                        float vong = 0.5f + 0.5f * Mathf.Sin(u * 12f * Mathf.PI * 2f + Fbm(u, v, 4, 2, 81) * 3f);
                        float mach = Mathf.SmoothStep(0.47f, 0.5f, Fbm(u * 2f, v * 2f, 4, 4, 83)) * (1f - Mathf.SmoothStep(0.5f, 0.53f, Fbm(u * 2f, v * 2f, 4, 4, 83)));
                        cao = 0.35f * vong + 0.65f * Fbm(u, v, 16, 2, 85);
                        sang = (0.97f + 0.03f * vong) * (1f - 0.3f * mach);
                        break;
                    }
                case Van.Tim:
                    {
                        // thượng tâm mạc bóng, ít mỡ, sợi cơ chéo
                        float soi = 0.5f + 0.5f * Mathf.Sin(((u + v) * 40f + Fbm(u, v, 4, 2, 91) * 4f) * Mathf.PI * 2f);
                        float mo = Mathf.SmoothStep(0.6f, 0.75f, Fbm(u, v, 4, 4, 93));
                        cao = 0.4f * soi + 0.6f * Fbm(u, v, 8, 3, 95);
                        sang = Mathf.Lerp(0.9f, 1.05f, soi) + 0.25f * mo;
                        break;
                    }
                case Van.Than:
                    {
                        float n = Fbm(u, v, 8, 4, 101);
                        cao = n; sang = 0.9f + 0.15f * n;
                        break;
                    }
                case Van.Mang:
                    {
                        float mach = Fbm(u * 1.5f, v * 1.5f, 4, 5, 111);
                        float m = 1f - Mathf.Clamp01(Mathf.Abs(mach - 0.5f) * 30f);
                        cao = 0.3f * Fbm(u, v, 8, 3, 113) + 0.5f * m;
                        sang = 1f - 0.25f * m;
                        break;
                    }
                case Van.Xuong:
                    {
                        float n = Fbm(u, v, 8, 4, 121);
                        cao = n; sang = 0.9f + 0.12f * n;
                        break;
                    }
                case Van.Khay:
                    {
                        float n = Fbm(u, v, 4, 5, 131);
                        cao = 0.2f * n; sang = 0.85f + 0.2f * n;
                        break;
                    }
                case Van.Vai:
                    {
                        float det = 0.5f + 0.25f * Mathf.Sin(u * 256f * Mathf.PI) + 0.25f * Mathf.Sin(v * 256f * Mathf.PI);
                        cao = det; sang = 0.92f + 0.08f * det * Fbm(u, v, 8, 3, 141);
                        break;
                    }
                default:
                    cao = 0.5f; sang = 1f; break;
            }
        }

        public static Texture2D Anh(Van van, out Texture2D banDoPhap)
        {
            if (mau.TryGetValue(van, out var a) && a != null) { banDoPhap = phap[van]; return a; }
            var h = new float[N * N];
            var c = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    Mau1(van, x / (float)N, y / (float)N, out float cao, out float sang);
                    h[x + y * N] = cao;
                    byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(sang * 235f), 0, 255);
                    c[x + y * N] = new Color32(b, b, b, 255);
                }
            a = new Texture2D(N, N, TextureFormat.RGBA32, true) { name = "BanMo_" + van, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            a.SetPixels32(c); a.Apply(true, false);
            float k = van == Van.Co ? 2.2f : van == Van.Gan || van == Van.Mo ? 1.6f : 1.1f;
            var nc = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = h[((x + 1) % N) + y * N] - h[((x - 1 + N) % N) + y * N];
                    float dy = h[x + ((y + 1) % N) * N] - h[x + ((y - 1 + N) % N) * N];
                    var n = new Vector3(-dx * k, -dy * k, 1f).normalized;
                    nc[x + y * N] = new Color32((byte)((n.x * 0.5f + 0.5f) * 255), (byte)((n.y * 0.5f + 0.5f) * 255), (byte)((n.z * 0.5f + 0.5f) * 255), 255);
                }
            var nt = new Texture2D(N, N, TextureFormat.RGBA32, true, true) { name = "BanMo_N_" + van, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            nt.SetPixels32(nc); nt.Apply(true, false);
            mau[van] = a; phap[van] = nt;
            banDoPhap = nt;
            return a;
        }
    }
}
