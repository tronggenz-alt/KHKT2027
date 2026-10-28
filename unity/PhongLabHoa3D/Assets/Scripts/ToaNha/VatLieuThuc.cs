/// Vật liệu THỰC cho phòng thí nghiệm (Unity) – texture PBR sinh bằng code, KHÔNG dùng ảnh ngoài (bản build nhỏ).
// Mỗi bộ gồm ba bản đồ: màu (albedo) · pháp tuyến (từ bản đồ độ cao: khe ron lõm, mép gạch vát, vân gỗ, hạt epoxy) · độ bóng (men gạch bóng, ron nhám).
// Nhiễu tuần hoàn → lặp liền mạch. Tính bằng Parallel.For ở luồng nền (không chặn lúc mở app); xong mới tạo Texture2D trên luồng chính.
// Vật liệu PBR lấy từ asset Resources/PTN/PBR.mat (đã bật _NORMALMAP + _METALLICSPECGLOSSMAP) để biến thể shader CHẮC CHẮN có trong bản build.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace PhongThiNghiem.ToaNha
{
    public static class VatLieuThuc
    {
        // ------------------------------------------------------------ nhiễu giá trị tuần hoàn (thuần toán, an toàn cho luồng nền)
        static float Bam(int x, int y, int s)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + s * 1013904223);
                h = (h ^ (h >> 13)) * 1274126177u;
                return (h ^ (h >> 16)) / 4294967295f;
            }
        }

        static float Lam(float t) => t * t * (3f - 2f * t);
        static int Mod(int a, int m) => ((a % m) + m) % m;

        static float Nhieu(float x, float y, int s, int px, int py)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float u = Lam(x - xi), v = Lam(y - yi);
            float a = Bam(Mod(xi, px), Mod(yi, py), s), b = Bam(Mod(xi + 1, px), Mod(yi, py), s);
            float c = Bam(Mod(xi, px), Mod(yi + 1, py), s), d = Bam(Mod(xi + 1, px), Mod(yi + 1, py), s);
            return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
        }

        static float Fbm(float u, float v, int s, int tf = 4, int tang = 4)
        {
            float t = 0f, bien = 0.5f, tong = 0f;
            int f = tf;
            for (int i = 0; i < tang; i++)
            {
                t += bien * Nhieu(u * f, v * f, s + i * 17, f, f);
                tong += bien;
                bien *= 0.5f;
                f *= 2;
            }
            return t / tong;
        }

        static float NhieuDan(float u, float v, int s, int fu, int fv) => Nhieu(u * fu, v * fv, s, fu, fv);
        static Color32 Mau(float r, float g, float b) => new Color32((byte)Mathf.Clamp(r, 0f, 255f), (byte)Mathf.Clamp(g, 0f, 255f), (byte)Mathf.Clamp(b, 0f, 255f), 255);

        // ------------------------------------------------------------ bộ texture PBR
        public struct Diem { public Color32 mau; public float cao; public float bong; }

        /// <summary>Ba mảng điểm ảnh (màu / pháp tuyến / độ bóng) – tạo được ở luồng nền, đổi sang Texture2D ở luồng chính.</summary>
        public class BoTex
        {
            public string ten;
            public int kt;
            public Color32[] mau, phapTuyen, bong;

            public Texture2D TaoMau() => Tao(mau, false, "_mau");
            public Texture2D TaoPhapTuyen() => Tao(phapTuyen, true, "_pt");
            public Texture2D TaoBong() => Tao(bong, true, "_bong");

            Texture2D Tao(Color32[] px, bool tuyenTinh, string hauTo)
            {
                var t = new Texture2D(kt, kt, TextureFormat.RGBA32, true, tuyenTinh) { name = ten + hauTo, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
                t.SetPixels32(px);
                t.Apply(true, false);
                return t;
            }
        }

        public delegate Diem HamDiem(float u, float v, int x, int y);

        /// <summary>Tính cả ba bản đồ. `heSoCao` = độ dốc pháp tuyến (lớn = khe / vát sâu hơn).</summary>
        public static BoTex Dung(string ten, int kt, HamDiem f, float heSoCao = 1.2f)
        {
            var kq = new BoTex { ten = ten, kt = kt, mau = new Color32[kt * kt], phapTuyen = new Color32[kt * kt], bong = new Color32[kt * kt] };
            var cao = new float[kt * kt];

#if UNITY_WEBGL && !UNITY_EDITOR
            for (int y = 0; y < kt; y++)
            {
                for (int x = 0; x < kt; x++)
                {
                    var d = f(x / (float)kt, y / (float)kt, x, y);
                    int i = y * kt + x;
                    kq.mau[i] = d.mau;
                    cao[i] = d.cao;
                    byte g = (byte)Mathf.Clamp(d.bong * 255f, 0f, 255f);
                    kq.bong[i] = new Color32(0, 0, 255, g);
                }
            }
            for (int y = 0; y < kt; y++)
            {
                int y0 = Mod(y - 1, kt), y1 = Mod(y + 1, kt);
                for (int x = 0; x < kt; x++)
                {
                    int x0 = Mod(x - 1, kt), x1 = Mod(x + 1, kt);
                    float dx = (cao[y * kt + x1] - cao[y * kt + x0]) * heSoCao, dy = (cao[y1 * kt + x] - cao[y0 * kt + x]) * heSoCao;
                    float l = Mathf.Sqrt(dx * dx + dy * dy + 1f);
                    kq.phapTuyen[y * kt + x] = new Color32((byte)((-dx / l * 0.5f + 0.5f) * 255f), (byte)((-dy / l * 0.5f + 0.5f) * 255f), (byte)((1f / l * 0.5f + 0.5f) * 255f), 255);
                }
            }
#else
            Parallel.For(0, kt, y =>
            {
                for (int x = 0; x < kt; x++)
                {
                    var d = f(x / (float)kt, y / (float)kt, x, y);
                    int i = y * kt + x;
                    kq.mau[i] = d.mau;
                    cao[i] = d.cao;
                    byte g = (byte)Mathf.Clamp(d.bong * 255f, 0f, 255f);
                    kq.bong[i] = new Color32(0, 0, 255, g);       // R = kim loại 0, A = độ bóng (URP: _MetallicGlossMap)
                }
            });
            Parallel.For(0, kt, y =>
            {
                int y0 = Mod(y - 1, kt), y1 = Mod(y + 1, kt);
                for (int x = 0; x < kt; x++)
                {
                    int x0 = Mod(x - 1, kt), x1 = Mod(x + 1, kt);
                    float dx = (cao[y * kt + x1] - cao[y * kt + x0]) * heSoCao, dy = (cao[y1 * kt + x] - cao[y0 * kt + x]) * heSoCao;
                    float l = Mathf.Sqrt(dx * dx + dy * dy + 1f);
                    // chuẩn Unity: pháp tuyến tiếp tuyến lưu (x, y) ở R, G; A = 255 (UnpackNormal đọc được cả dạng RGB lẫn AG)
                    kq.phapTuyen[y * kt + x] = new Color32((byte)((-dx / l * 0.5f + 0.5f) * 255f), (byte)((-dy / l * 0.5f + 0.5f) * 255f), (byte)((1f / l * 0.5f + 0.5f) * 255f), 255);
                }
            });
#endif
            return kq;
        }

        /// <summary>Sàn gạch men xám 3×3 viên (mỗi viên 0,6 m khi lặp 1 lần / 1,8 m): ron lõm, mép vát, men bóng, ron nhám + bẩn nhẹ gần ron, vệt xước.</summary>
        public static BoTex SanGach(int kt = 1024)
        {
            const int ON = 3;
            float tpx = kt / (float)ON;
            return Dung("SanGach", kt, (u, v, x, y) =>
            {
                float tx = u * ON, ty = v * ON;
                int ti = Mathf.FloorToInt(tx), tj = Mathf.FloorToInt(ty);
                float fx = tx - ti, fy = ty - tj;
                float dBien = Mathf.Min(Mathf.Min(fx, 1f - fx), Mathf.Min(fy, 1f - fy)) * tpx;   // khoảng cách tới mép viên (điểm ảnh)
                const float RON = 3.2f, VAT = 8f;
                float cao = dBien < RON ? 0f : Mathf.SmoothStep(0f, 1f, (dBien - RON) / VAT);
                float tone = 1f + (Bam(ti, tj, 7) - 0.5f) * 0.06f;
                float xuoc = Fbm(u, v, 5, 3, 3);
                float hat = Fbm(u, v, 31, 120, 2);
                float be = Mathf.Clamp01(1f - (dBien - RON) / 26f) * 0.1f;                       // bẩn đọng gần ron
                if (dBien < RON + 1f)
                {
                    float g = 0.62f + 0.08f * Fbm(u, v, 9, 90, 2);
                    return new Diem { mau = Mau(205f * g, 207f * g, 210f * g), cao = 0f, bong = 0.12f };
                }
                float k = tone * (1f - be) * (0.97f + 0.05f * xuoc) * (hat > 0.64f ? 1.03f : hat < 0.34f ? 0.965f : 1f);
                // men gạch: mịn, hơi trầm → độ bóng cao, giảm ở vệt xước / mòn
                float bong = 0.62f * Mathf.Clamp01(0.74f - (xuoc - 0.5f) * 0.45f - be * 1.2f);   // men bóng vừa: bóng quá thì phản chiếu bầu trời làm cháy trắng nền
                return new Diem { mau = Mau(128f * k, 134f * k, 142f * k), cao = cao + (hat - 0.5f) * 0.02f, bong = bong };
            }, 1.4f);
        }

        /// <summary>Gạch ốp tường men trắng 60 × 30 cm xếp so le (bản lặp 1,2 m × 1,2 m = 2 cột × 4 hàng): ron sáng, mép vát, men rất bóng.</summary>
        public static BoTex GachOpTuong(int kt = 1024)
        {
            const float COT = 2f, HANG = 4f;
            float ppmX = kt / 1.2f;
            return Dung("GachOpTuong", kt, (u, v, x, y) =>
            {
                int hang = Mathf.FloorToInt(v * HANG);
                float cx = u * COT + (hang % 2) * 0.5f;
                int cot = Mathf.FloorToInt(cx);
                float fx = cx - cot, fy = v * HANG - hang;
                float dx = Mathf.Min(fx, 1f - fx) * (kt / COT), dy = Mathf.Min(fy, 1f - fy) * (kt / HANG);
                float dBien = Mathf.Min(dx, dy);
                const float RON = 2.6f, VAT = 7f;
                if (dBien < RON) return new Diem { mau = Mau(196f, 198f, 200f), cao = 0f, bong = 0.15f };
                float cao = Mathf.SmoothStep(0f, 1f, (dBien - RON) / VAT);
                float tone = 1f + (Bam(cot, hang, 41) - 0.5f) * 0.035f;
                float men = 0.985f + 0.03f * Fbm(u, v, 43, 6, 3);
                float be = Mathf.Clamp01(1f - (dBien - RON) / 14f) * 0.05f;
                float k = tone * men * (1f - be);
                return new Diem { mau = Mau(238f * k, 240f * k, 241f * k), cao = cao, bong = 0.92f - be * 3f };
            }, 1.1f);
        }

        /// <summary>Mặt bàn epoxy xanh navy đậm: hạt sáng li ti (granite nhân tạo), phủ bóng.</summary>
        public static BoTex MatBanEpoxy(int kt = 512)
        {
            return Dung("MatBanEpoxy", kt, (u, v, x, y) =>
            {
                float k = 0.94f + 0.12f * Fbm(u, v, 41, 5, 3);
                float h = Bam(x, y, 77);
                float cao = 0.5f;
                float bong = 0.62f + 0.12f * Fbm(u, v, 45, 8, 2);
                if (h > 0.985f) { k *= 1.55f; cao = 0.7f; bong = 0.85f; }
                else if (h < 0.012f) { k *= 0.7f; cao = 0.35f; }
                return new Diem { mau = Mau(36f * k, 58f * k, 98f * k), cao = cao, bong = bong };
            }, 0.5f);
        }

        /// <summary>Gỗ sồi sáng: 3 tấm ghép, vân dọc (có độ nhám thớ trong pháp tuyến), mạch ghép tối.</summary>
        public static BoTex GoSoi(int kt = 512)
        {
            return Dung("GoSoi", kt, (u, v, x, y) =>
            {
                int tam = Mathf.FloorToInt(u * 3f);
                float dich = Bam(tam, 3, 13), uu = u * 3f - tam, vv = (v + dich) % 1f;
                float bien = Mathf.Min(uu, 1f - uu) * kt / 3f;
                float vong = Mathf.Sin((uu * 11f + Fbm(uu, vv, 51 + tam, 3, 3) * 5f) * Mathf.PI) * 0.5f + 0.5f;
                float tho = NhieuDan(uu, vv, 61 + tam, 70, 3);
                float k = (0.9f + 0.1f * vong) * (0.94f + 0.1f * tho) * (0.97f + 0.06f * Bam(tam, 5, 1));
                float cao = 0.5f + (tho - 0.5f) * 0.35f + (vong - 0.5f) * 0.12f;
                if (bien < 1.4f) { k *= 0.6f; cao = 0f; }
                return new Diem { mau = Mau(216f * k, 182f * k, 134f * k), cao = cao, bong = 0.34f + 0.1f * tho };
            }, 0.9f);
        }

        /// <summary>Tường sơn lăn: trắng ngà, vỏ cam rất nhẹ.</summary>
        public static BoTex TuongSon(int kt = 256)
        {
            return Dung("TuongSon", kt, (u, v, x, y) =>
            {
                float n = Fbm(u, v, 81, 60, 2);
                float k = 0.985f + 0.03f * n + 0.012f * NhieuDan(u, v, 83, 4, 90);
                return new Diem { mau = Mau(250f * k, 251f * k, 252f * k), cao = n, bong = 0.1f };
            }, 0.25f);
        }

        /// <summary>Trần thạch cao 3×3 tấm 0,6 m: thanh T xám, lỗ tiêu âm có độ sâu.</summary>
        public static BoTex TranTam(int kt = 512)
        {
            const int ON = 3;
            return Dung("TranTam", kt, (u, v, x, y) =>
            {
                float lt = (u * ON) % 1f, lg = (v * ON) % 1f;
                float bien = Mathf.Min(Mathf.Min(lt, 1f - lt), Mathf.Min(lg, 1f - lg)) * kt / ON;
                if (bien < 2.4f) return new Diem { mau = Mau(196f, 200f, 206f), cao = 1f, bong = 0.45f };
                float cx = (lt * 9f) % 1f - 0.5f, cy = (lg * 9f) % 1f - 0.5f;
                bool lo = cx * cx + cy * cy < 0.035f;
                float k = (lo ? 0.82f : 1f) * (0.97f + 0.04f * Fbm(u, v, 91, 30, 2));
                return new Diem { mau = Mau(247f * k, 248f * k, 250f * k), cao = lo ? 0f : 0.6f, bong = 0.08f };
            }, 0.8f);
        }

        // ------------------------------------------------------------ vật liệu
        /// <summary>Vật liệu URP Lit có texture màu (bản sao riêng, không đụng vật liệu dùng chung trong LabVatLieu).</summary>
        public static Material Vat(Texture2D map, float bong, float kimLoai = 0f, float lapX = 1f, float lapY = 1f)
        {
            var m = new Material(LabVatLieu.Dac(Color.white, bong)) { name = map != null ? map.name + "_vl" : "vl" };
            m.SetColor("_BaseColor", Color.white);
            if (map != null)
            {
                m.SetTexture("_BaseMap", map);
                m.SetTextureScale("_BaseMap", new Vector2(lapX, lapY));
            }
            m.SetFloat("_Metallic", kimLoai);
            m.SetFloat("_Smoothness", bong);
            return m;
        }

        /// <summary>Vật liệu PBR (màu + pháp tuyến + độ bóng) – gốc là Resources/PTN/PBR.mat. Texture gắn sau bằng GanTex khi tính xong ở luồng nền.</summary>
        public static Material VatPBR(string ten, float lapX = 1f, float lapY = 1f, float doDocPhapTuyen = 1f)
        {
            var goc = Resources.Load<Material>("PTN/PBR");
            var m = goc != null ? new Material(goc) : new Material(LabVatLieu.Dac(Color.white, 0.5f));
            m.name = ten + "_pbr";
            m.SetColor("_BaseColor", new Color(0.85f, 0.87f, 0.9f));   // trong lúc texture đang tính: màu xám sáng, không trắng chói
            m.SetFloat("_Smoothness", 1f);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_BumpScale", doDocPhapTuyen);
            m.SetTextureScale("_BaseMap", new Vector2(lapX, lapY));
            m.SetTextureScale("_BumpMap", new Vector2(lapX, lapY));
            m.SetTextureScale("_MetallicGlossMap", new Vector2(lapX, lapY));
            return m;
        }

        public static void GanTex(Material m, BoTex t)
        {
            m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_BaseMap", t.TaoMau());
            m.SetTexture("_BumpMap", t.TaoPhapTuyen());
            m.SetTexture("_MetallicGlossMap", t.TaoBong());
            m.EnableKeyword("_NORMALMAP");
            m.EnableKeyword("_METALLICSPECGLOSSMAP");
        }

        // ------------------------------------------------------------ tính nền
#if UNITY_WEBGL && !UNITY_EDITOR
        static readonly Queue<(Func<BoTex> tao, Action<BoTex> xong)> _hangDoiWeb = new Queue<(Func<BoTex>, Action<BoTex>)>();

        public static void TaoNen(Func<BoTex> tao, Action<BoTex> xong)
        {
            _hangDoiWeb.Enqueue((tao, xong));
        }

        public static bool DangCho => _hangDoiWeb.Count > 0;

        public static void Bom()
        {
            if (_hangDoiWeb.Count > 0)
            {
                var cap = _hangDoiWeb.Dequeue();
                try
                {
                    var res = cap.tao();
                    cap.xong(res);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[VatLieuThuc] Lỗi tính texture WebGL: " + ex.Message);
                }
            }
        }
#else
        static readonly List<(Task<BoTex> viec, Action<BoTex> xong)> _cho = new List<(Task<BoTex>, Action<BoTex>)>();

        /// <summary>Tính texture ở luồng nền; `xong` được gọi ở luồng chính (qua Bom) khi có kết quả.</summary>
        public static void TaoNen(Func<BoTex> tao, Action<BoTex> xong)
        {
            lock (_cho) _cho.Add((Task.Run(tao), xong));
        }

        public static bool DangCho { get { lock (_cho) return _cho.Count > 0; } }

        /// <summary>Gọi mỗi khung hình ở luồng chính: nhận các texture đã tính xong (tối đa 1 bộ / khung để không giật).</summary>
        public static void Bom()
        {
            (Task<BoTex> viec, Action<BoTex> xong) dau = default;
            lock (_cho)
            {
                for (int i = 0; i < _cho.Count; i++)
                    if (_cho[i].viec.IsCompleted) { dau = _cho[i]; _cho.RemoveAt(i); break; }
            }
            if (dau.viec == null) return;
            if (dau.viec.IsFaulted) { Debug.LogWarning("[VatLieuThuc] Lỗi tính texture: " + dau.viec.Exception?.GetBaseException().Message); return; }
            dau.xong(dau.viec.Result);
        }
#endif

        /// <summary>Vật liệu tự phát sáng (tấm đèn LED, màn hình). Gốc là asset Resources/PTN/PhatSangDac.mat (đã bật _EMISSION).</summary>
        public static Material PhatSangDac(Color mau, float cuong = 1.4f)
        {
            var goc = Resources.Load<Material>("PTN/PhatSangDac");
            var m = goc != null ? new Material(goc) : new Material(LabVatLieu.Dac(mau, 0.3f));
            m.name = "phatsang_vl";
            m.SetColor("_BaseColor", mau);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", mau * cuong);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            return m;
        }
    }
}
