using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Khkt.Interaction;
using Khkt.Simulation;
using Khkt.Simulation.Chem;
using UnityEngine;

namespace Khkt.Demo
{
    /// <summary>
    /// Máy phân tích hiện đại: đặt ống mẫu vào ổ, bấm máy để chạy. Kết quả tính từ đúng thành phần của mẫu
    /// (bộ máy hóa học), vẽ lên màn hình của máy:
    ///   UV-Vis  phổ hấp thụ 380-780 nm, λmax       FTIR  nhóm chức (O-H, C=O, N-H, C=C...)
    ///   AAS     nồng độ kim loại (mg/L)              GC-MS sắc ký đồ chất hữu cơ dễ bay hơi + m/z
    ///   HPLC    sắc ký đồ chất hữu cơ tan, axit hữu cơ  Chuẩn độ điện thế tự động: đường cong pH, nồng độ
    /// </summary>
    public class AnalyzerInstrument : MonoBehaviour, IDemoUsable
    {
        public enum Kind { UvVis, Ftir, Aas, GcMs, Hplc, AutoTitrator }

        public Kind kind;
        public PlaceSocket slot;
        public Renderer screen;
        public TextMesh header, resultText;
        public float runSeconds = 3f;

        public string LastReport { get; private set; } = "";
        public bool Running { get; private set; }
        public string UseHint => Running ? "Đang phân tích..." : "Bấm chuột trái: chạy phân tích mẫu trong ổ";

        Texture2D tex;
        const int W = 384, Hh = 192;
        static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

        public static string KindName(Kind k)
        {
            switch (k)
            {
                case Kind.UvVis: return "Quang phổ UV-Vis";
                case Kind.Ftir: return "Quang phổ hồng ngoại FTIR";
                case Kind.Aas: return "Quang phổ hấp thụ nguyên tử AAS";
                case Kind.GcMs: return "Sắc ký khí - khối phổ GC-MS";
                case Kind.Hplc: return "Sắc ký lỏng HPLC";
                default: return "Chuẩn độ điện thế tự động";
            }
        }

        void Start()
        {
            tex = new Texture2D(W, Hh, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            if (screen != null)
            {
                // vật liệu riêng cho từng máy: ảnh đồ thị làm ảnh phát sáng (màn hình tự sáng, không phụ thuộc đèn phòng)
                var m = new Material(screen.sharedMaterial) { name = "Màn hình " + KindName(kind), hideFlags = HideFlags.DontSave };
                m.SetColor("_BaseColor", new Color(0.05f, 0.05f, 0.05f));
                m.SetTexture("_BaseMap", tex);
                m.EnableKeyword("_EMISSION");
                m.SetTexture("_EmissionMap", tex);
                m.SetColor("_EmissionColor", Color.white * 1.15f);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                screen.sharedMaterial = m;
            }
            Clear(new Color32(8, 18, 32, 255));
            Apply();
            if (header != null) header.text = KindName(kind);
            if (resultText != null) resultText.text = "Đặt ống mẫu vào ổ rồi bấm máy";
        }

        LabContainer Sample => slot != null && slot.Occupant != null ? slot.Occupant.GetComponent<LabContainer>() : null;

        /// <summary>
        /// Người dùng bấm vào máy: mở màn hình vận hành riêng của máy (quy trình như máy thật, sơ đồ bên trong máy, phân tích chuyên sâu).
        /// </summary>
        public void Use()
        {
            Instruments.InstrumentProgram program = kind switch
            {
                Kind.UvVis => new Instruments.UvVisProgram(),
                Kind.Aas => new Instruments.AasProgram(),
                Kind.Ftir => new Instruments.FtirProgram(),
                Kind.GcMs => new Instruments.GcMsProgram(),
                Kind.Hplc => new Instruments.HplcProgram(),
                _ => new Instruments.TitratorProgram(),
            };
            Instruments.InstrumentScreen.Open(this, program, Sample);
        }

        /// <summary>Chạy nhanh (đặt mẫu, bấm là ra kết quả trên màn hình cạnh máy): robot trợ lý dùng, không mở màn hình vận hành.</summary>
        public void RunQuick()
        {
            if (Running) return;
            var s = Sample;
            if (s == null || s.VolumeMl < 0.3f)
            {
                LabMessages.Say("Chưa có mẫu: đặt ống nghiệm (hoặc cuvet) có dung dịch vào ổ của máy", LabMessages.Tone.Warn);
                return;
            }
            StartCoroutine(Run(s));
        }

        IEnumerator Run(LabContainer s)
        {
            Running = true;
            // giữ phòng máy không bị gỡ khi máy đang chạy dở (người xem có đi ra hành lang cũng chạy xong)
            Khkt.Rendering.ZoneStreamer.Instance?.Keep(this, runSeconds + 2f, transform.position);
            for (float t = 0; t < runSeconds; t += Time.deltaTime)
            {
                if (resultText != null) resultText.text = "Đang phân tích" + new string('.', 1 + (int)(t * 3) % 3);
                yield return null;
            }
            string report;
            switch (kind)
            {
                case Kind.UvVis: report = UvVis(s); break;
                case Kind.Ftir: report = Ftir(s); break;
                case Kind.Aas: report = Aas(s); break;
                case Kind.GcMs: report = Chromatogram(s, true); break;
                case Kind.Hplc: report = Chromatogram(s, false); break;
                default: report = Titrate(s); break;
            }
            Apply();
            LastReport = report;
            if (resultText != null) resultText.text = report;
            LabMessages.Say(KindName(kind) + ": " + report.Split('\n')[0], LabMessages.Tone.Good);
            if (AiClient.Instance != null) AiClient.Instance.Log("phan_tich", $"{KindName(kind)}: {s.label}", report);
            Running = false;
        }

        // ------------------------------------------------------------------
        // Các phép phân tích
        // ------------------------------------------------------------------

        string UvVis(LabContainer s)
        {
            var look = ChemColor.Compute(s.Total.Mix, s.VolumeMl, s.BulkPh, s.TemperatureC, 1f);
            float Ab(float t, float white) => -Mathf.Log10(Mathf.Clamp(t / white, 1e-3f, 1f));
            float ab = Ab(look.Liquid.b, 1f), ag = Ab(look.Liquid.g, 0.93f), ar = Ab(look.Liquid.r, 0.86f);
            if (s.PinkVisible > 0.05f) ag += s.PinkVisible * 1.2f;  // phenolphtalein hồng hấp thụ ~550 nm
            var pts = new List<Vector2>();
            float max = 0, lmax = 0;
            for (int i = 0; i <= 200; i++)
            {
                float l = 380 + i * 2;
                float a = ab * G(l, 450, 45) + ag * G(l, 545, 45) + ar * G(l, 650, 55) + look.Turbidity * 0.6f * (500f / l);
                pts.Add(new Vector2(l, a));
                if (a > max) { max = a; lmax = l; }
            }
            Clear(new Color32(8, 18, 32, 255));
            Axes();
            Plot(pts, 380, 780, 0, Mathf.Max(0.2f, max * 1.15f), new Color32(110, 220, 255, 255));
            if (max < 0.02f) return "Gần như không hấp thụ ánh sáng khả kiến (dung dịch không màu)";
            return $"λmax ≈ {lmax:0} nm · A = {max.ToString("0.00", Vi)}\nMàu quan sát là màu bù của vùng {Band(lmax)}";
        }

        static string Band(float l) => l < 480 ? "lam - tím" : l < 560 ? "lục" : l < 600 ? "vàng" : "đỏ - cam";

        string Ftir(LabContainer s)
        {
            var mix = s.Total.Mix;
            var bands = new List<(float wn, float depth, float width, string label)> { (3400, 0.55f, 180, "O-H (nước)"), (1640, 0.25f, 40, "") };
            var seen = new HashSet<string>();
            for (int i = 0; i < mix.Count; i++)
            {
                var d = mix.SpeciesAt(i);
                if (mix.MolAt(i) / (s.VolumeMl / 1000.0) < 1e-3) continue;
                void Add(string tag, float wn, float depth, float width, string label)
                {
                    if (seen.Add(tag)) bands.Add((wn, depth, width, label));
                }
                if (d.HasTag("ancol") || d.HasTag("phenol")) Add("oh", 3330, 0.35f, 120, "O-H ancol/phenol");
                if (d.HasTag("andehit")) { Add("cho", 1725, 0.6f, 25, "C=O anđehit"); Add("chh", 2720, 0.2f, 20, "C-H anđehit"); }
                if (d.HasTag("este")) { Add("coo", 1740, 0.65f, 25, "C=O este"); Add("co", 1200, 0.45f, 40, "C-O este"); }
                if (d.HasTag("amin") || d.HasTag("amino_axit") || d.HasTag("protein")) Add("nh", 3350, 0.35f, 60, "N-H");
                if (d.HasTag("khong_no")) Add("cc", 1645, 0.3f, 20, "C=C");
                if (d.HasTag("aren_ankyl") || d.HasTag("phenol") || d.HasTag("anilin")) Add("ar", 1600, 0.3f, 18, "vòng thơm");
                if (d.Id == "Ac" || d.Id == "Fo" || d.Id == "Ox" || d.Id == "Cit" || d.Id == "Bz" || d.Id == "Lac" || (d.ChemStt > 0 && d.Kind == SpeciesKind.Weak && d.Charge == 0))
                    Add("cooh", 1710, 0.6f, 30, "C=O axit cacboxylic");
                if (d.Id == "C2H5OH") Add("oh", 3330, 0.35f, 120, "O-H ancol");
                if (d.Id == "CO3") Add("co3", 1410, 0.4f, 60, "CO₃²⁻");
                if (d.Id == "SO4") Add("so4", 1100, 0.45f, 50, "SO₄²⁻");
                if (d.Id == "NO3-") Add("no3", 1350, 0.4f, 40, "NO₃⁻");
            }
            var pts = new List<Vector2>();
            for (int i = 0; i <= 300; i++)
            {
                float wn = 4000 - i * 11;
                float t = 1;
                foreach (var b in bands) t *= 1 - b.depth * G(wn, b.wn, b.width);
                pts.Add(new Vector2(4000 - wn, t));
            }
            Clear(new Color32(8, 18, 32, 255));
            Axes();
            Plot(pts, 0, 3300, 0, 1.05f, new Color32(255, 200, 110, 255));
            var sb = new StringBuilder();
            foreach (var b in bands) if (b.label.Length > 0 && b.label != "O-H (nước)") sb.Append(sb.Length > 0 ? " · " : "").Append($"{b.label} {b.wn:0}");
            return sb.Length > 0 ? "Nhóm chức (cm⁻¹): " + sb : "Chỉ thấy dải của nước (3400, 1640 cm⁻¹)";
        }

        static readonly Dictionary<string, (string el, float mass)> Metals = new Dictionary<string, (string, float)>
        {
            ["Na+"] = ("Na", 22.99f), ["K+"] = ("K", 39.1f), ["Li+"] = ("Li", 6.94f), ["Ca2+"] = ("Ca", 40.08f), ["Mg2+"] = ("Mg", 24.31f),
            ["Ba2+"] = ("Ba", 137.3f), ["Sr2+"] = ("Sr", 87.6f), ["Al3+"] = ("Al", 26.98f), ["Fe2+"] = ("Fe", 55.85f), ["Fe3+"] = ("Fe", 55.85f),
            ["Cu2+"] = ("Cu", 63.55f), ["CuNH3"] = ("Cu", 63.55f), ["CuPol"] = ("Cu", 63.55f), ["Zn2+"] = ("Zn", 65.38f), ["ZnNH3"] = ("Zn", 65.38f),
            ["Pb2+"] = ("Pb", 207.2f), ["Ag+"] = ("Ag", 107.9f), ["AgNH3"] = ("Ag", 107.9f), ["Hg2+"] = ("Hg", 200.6f), ["Ni2+"] = ("Ni", 58.69f),
            ["Co2+"] = ("Co", 58.93f), ["Mn2+"] = ("Mn", 54.94f), ["Cr3+"] = ("Cr", 52f), ["Cd2+"] = ("Cd", 112.4f), ["FeSCN"] = ("Fe", 55.85f),
        };

        string Aas(LabContainer s)
        {
            var mix = s.Total.Mix;
            double L = s.VolumeMl / 1000.0;
            var conc = new Dictionary<string, double>();
            for (int i = 0; i < mix.Count; i++)
            {
                var d = mix.SpeciesAt(i);
                if (!Metals.TryGetValue(d.Id, out var m)) continue;
                conc.TryGetValue(m.el, out double c);
                conc[m.el] = c + mix.MolAt(i) / L * m.mass * 1000;  // mg/L (chỉ phần tan, như mẫu đã lọc)
            }
            Clear(new Color32(8, 18, 32, 255));
            Axes();
            if (conc.Count == 0) return "Không phát hiện kim loại trong dung dịch";
            var list = new List<KeyValuePair<string, double>>(conc);
            list.Sort((a, b) => b.Value.CompareTo(a.Value));
            double max = list[0].Value;
            int n = Mathf.Min(list.Count, 8);
            var sb = new StringBuilder();
            for (int i = 0; i < n; i++)
            {
                int x0 = 30 + i * (W - 40) / n, x1 = x0 + (W - 40) / n - 8;
                int h = Mathf.Max(2, (int)((Hh - 30) * list[i].Value / max));
                FillRect(x0, 16, x1, 16 + h, new Color32(120, 230, 160, 255));
                sb.Append(i == 0 ? "" : " · ").Append($"{list[i].Key} {Fmt(list[i].Value)} mg/L");
            }
            return sb.ToString();
        }

        static string Fmt(double v) => v >= 100 ? v.ToString("0", Vi) : v >= 1 ? v.ToString("0.0", Vi) : v.ToString("0.00", Vi);

        string Chromatogram(LabContainer s, bool gas)
        {
            var mix = s.Total.Mix;
            double L = s.VolumeMl / 1000.0;
            var peaks = new List<(float rt, float h, string name, float mz)>();
            for (int i = 0; i < mix.Count; i++)
            {
                var d = mix.SpeciesAt(i);
                bool organic = d.ChemStt > 0 || d.Id == "C2H5OH" || d.Id == "Ac" || d.Id == "Fo" || d.Id == "Ox" || d.Id == "Cit" || d.Id == "Tart" || d.Id == "Bz" || d.Id == "Lac" || d.Id == "ure";
                if (!organic || d.Kind == SpeciesKind.Lump || d.Kind == SpeciesKind.Precipitate) continue;
                double c = mix.MolAt(i) / L;
                if (c < 1e-4) continue;
                float M = (float)(d.MolarMass > 0 ? d.MolarMass : 100);
                if (gas && (M > 320 || d.HasTag("cacbohidrat") || d.HasTag("protein") || d.HasTag("amino_axit") || d.Id == "Ox" || d.Id == "Cit" || d.Id == "Tart")) continue;  // không bay hơi được
                // HPLC cột pha đảo: axit hữu cơ (phân cực, mang điện) ra sớm, đường ra sau, chất kém tan ra muộn
                float rt = gas ? 1.2f + Mathf.Sqrt(M) * 0.9f + (d.HasTag("ancol") ? 0.8f : 0)
                               : 1f + (d.HasTag("khong_tan") ? 9f : 2f) + Mathf.Log(M) * 0.8f + (d.Kind == SpeciesKind.Weak ? -1.6f : 0) + (d.HasTag("cacbohidrat") ? 0.7f : 0);
                peaks.Add((rt, Mathf.Sqrt((float)c), d.Name, M));
            }
            Clear(new Color32(8, 18, 32, 255));
            Axes();
            if (peaks.Count == 0)
                return gas ? "Không có chất hữu cơ dễ bay hơi trong mẫu" : "Không có chất hữu cơ phân tích được";
            float maxH = 0;
            foreach (var p in peaks) maxH = Mathf.Max(maxH, p.h);
            var pts = new List<Vector2>();
            for (int i = 0; i <= 300; i++)
            {
                float t = i * 0.06f;
                float y = 0.01f;
                foreach (var p in peaks) y += p.h / maxH * G(t, p.rt, 0.12f);
                pts.Add(new Vector2(t, y));
            }
            Plot(pts, 0, 18, 0, 1.15f, gas ? new Color32(255, 150, 200, 255) : new Color32(180, 160, 255, 255));
            peaks.Sort((a, b) => a.rt.CompareTo(b.rt));
            var sb = new StringBuilder();
            foreach (var p in peaks)
                sb.Append(sb.Length > 0 ? "\n" : "").Append($"{p.rt.ToString("0.0", Vi)} phút · {p.name}" + (gas ? $" · m/z {p.mz:0}" : ""));
            return sb.ToString();
        }

        string Titrate(LabContainer s)
        {
            var mix = s.Total.Mix;
            double V0 = s.VolumeMl / 1000.0;
            double ph0 = ChemEngine.Ph(mix, V0);
            bool acid = ph0 < 7;
            string add = acid ? "Na+" : "Cl-";
            const double c = 0.1;  // NaOH hoặc HCl 0,1 M
            double PhAt(double v)
            {
                var b = mix.ToBuilder();
                b.Add(add, c * v);
                return -System.Math.Log10(ChemEngine.SolveH(b, V0 + v));
            }
            // Định liều động như máy thật: lượt thô 0,5 ml/lần tìm vùng bước nhảy, rồi lượt mịn 0,01 ml/lần quanh đó
            const double coarse = 0.5 / 1000, fine = 0.01 / 1000;
            var pts = new List<Vector2> { new Vector2(0, (float)ph0) };
            double best = 0, vc = -1, prev = ph0;
            for (int i = 1; i <= 400; i++)
            {
                double v = i * coarse, ph = PhAt(v);
                pts.Add(new Vector2((float)(v * 1000), (float)ph));
                if (System.Math.Abs(ph - prev) > best) { best = System.Math.Abs(ph - prev); vc = v - coarse / 2; }
                prev = ph;
                if (i > 4 && ((acid && ph > 11.8) || (!acid && ph < 2.2))) break;
            }
            double veq = vc, fineBest = 0;
            if (vc > 0)
            {
                double from = System.Math.Max(0, vc - 1.5 * coarse);
                prev = PhAt(from);
                for (double v = from + fine; v <= vc + 1.5 * coarse; v += fine)
                {
                    double ph = PhAt(v);
                    if (System.Math.Abs(ph - prev) > fineBest) { fineBest = System.Math.Abs(ph - prev); veq = v - fine / 2; }
                    prev = ph;
                }
            }
            Clear(new Color32(8, 18, 32, 255));
            Axes();
            float vmax = pts[pts.Count - 1].x;
            Plot(pts, 0, Mathf.Max(1, vmax), 0, 14, new Color32(110, 220, 255, 255));
            if (best < 0.5 || veq <= 0) return $"pH ban đầu {ph0.ToString("0.00", Vi)} · không thấy bước nhảy pH rõ";
            double n = c * veq;
            return $"pH đầu {ph0.ToString("0.00", Vi)} · V tương đương {(veq * 1000).ToString("0.00", Vi)} ml {(acid ? "NaOH" : "HCl")} 0,1 M\n"
                   + $"Lượng {(acid ? "axit" : "bazơ")} (đương lượng) {(n * 1000).ToString("0.000", Vi)} mmol · {(n / V0).ToString("0.0000", Vi)} N";
        }

        // ------------------------------------------------------------------
        // Vẽ
        // ------------------------------------------------------------------

        static float G(float x, float mu, float sigma) => Mathf.Exp(-0.5f * (x - mu) * (x - mu) / (sigma * sigma));

        Color32[] px;

        void Clear(Color32 c)
        {
            if (px == null) px = new Color32[W * Hh];
            for (int i = 0; i < px.Length; i++) px[i] = c;
        }

        void Axes()
        {
            var grid = new Color32(255, 255, 255, 40);
            for (int x = 20; x < W; x += 40) for (int y = 12; y < Hh; y++) px[y * W + x] = grid;
            for (int y = 12; y < Hh; y += 30) for (int x = 20; x < W; x++) px[y * W + x] = grid;
            for (int x = 20; x < W; x++) px[12 * W + x] = new Color32(200, 210, 220, 255);
            for (int y = 12; y < Hh; y++) px[y * W + 20] = new Color32(200, 210, 220, 255);
        }

        void Plot(List<Vector2> pts, float x0, float x1, float y0, float y1, Color32 c)
        {
            int PX(float x) => Mathf.Clamp(20 + (int)((x - x0) / (x1 - x0) * (W - 24)), 0, W - 1);
            int PY(float y) => Mathf.Clamp(12 + (int)((y - y0) / (y1 - y0) * (Hh - 16)), 0, Hh - 1);
            for (int i = 1; i < pts.Count; i++)
            {
                int ax = PX(pts[i - 1].x), ay = PY(pts[i - 1].y), bx = PX(pts[i].x), by = PY(pts[i].y);
                int n = Mathf.Max(Mathf.Abs(bx - ax), Mathf.Abs(by - ay), 1);
                for (int k = 0; k <= n; k++)
                {
                    int x = ax + (bx - ax) * k / n, y = ay + (by - ay) * k / n;
                    for (int d = -1; d <= 1; d++) if (y + d >= 0 && y + d < Hh) px[(y + d) * W + x] = c;
                }
            }
        }

        void FillRect(int x0, int y0, int x1, int y1, Color32 c)
        {
            for (int y = Mathf.Max(0, y0); y < Mathf.Min(Hh, y1); y++)
                for (int x = Mathf.Max(0, x0); x < Mathf.Min(W, x1); x++) px[y * W + x] = c;
        }

        void Apply()
        {
            tex.SetPixels32(px);
            tex.Apply();
        }
    }
}
