using System;
using System.Collections.Generic;
using System.Linq;

namespace Khkt.Simulation.Chem
{
    /// <summary>
    /// Mô hình GC-MS (data/may/tinh_chat/gc_ms.json):
    ///  • thời gian lưu theo chương trình nhiệt: ln k tuyến tính theo chỉ số lưu RI (đúng định nghĩa Kovats),
    ///    ln k = (RI/100)·(546/T − 0,690) + 100/T − 7,49 (mô phỏng, ΔH của một nhóm CH₂ ≈ 4,5 kJ/mol);
    ///    tích phân đường đi của chất trong cột khi nhiệt độ lò tăng;
    ///  • độ rộng pic theo số đĩa lý thuyết, quá tải cột khi lượng chất vào cột quá lớn;
    ///  • phổ khối EI của NIST, nền nước / không khí khi quét từ m/z thấp, tra thư viện theo độ khớp (0 – 999).
    /// </summary>
    public sealed class GcLibrary
    {
        public sealed class Entry
        {
            public string Id, Name, Formula, Cas, Source;
            public double M, RI, RF = 1;
            public readonly List<string> Ids = new List<string>();
            public readonly List<(int mz, double i)> Spectrum = new List<(int, double)>();
            public int BasePeak => Spectrum.Count == 0 ? 0 : Spectrum.OrderByDescending(s => s.i).First().mz;
        }

        public sealed class Method
        {
            public double T0 = 40, Hold = 2, Rate = 10, Tmax = 250, Flow = 1.0, Split = 50, SolventDelay = 1.0, ScanLo = 29, ScanHi = 300, RunMin = 16;
        }

        public struct Peak
        {
            public Entry E;
            public double Tr, Sigma, AmountNg, Area, Height, KElute;
            public bool Overloaded, Saturated;
        }

        public readonly List<Entry> Entries = new List<Entry>();
        public double TmMin = 1.37, Plates = 100000;
        public readonly List<(int mz, double i)> Water = new List<(int, double)>(), Air = new List<(int, double)>();

        /// <summary>Lượng chất tối đa cột 0,25 µm chịu được trước khi pic bị doãng (ng), mô phỏng.</summary>
        public const double CapacityNg = 500;
        /// <summary>Chiều cao pic tối đa trước khi detector bão hòa (đơn vị đếm), mô phỏng.</summary>
        public const double SaturationCounts = 2.0e7;
        /// <summary>Diện tích pic (số đếm·phút) trên 1 ng chất (hệ số đáp ứng 1), mô phỏng.</summary>
        public const double CountsPerNg = 600;

        public Entry Get(string speciesId) => Entries.FirstOrDefault(e => e.Ids.Contains(speciesId));
        public Entry ById(string id) => Entries.FirstOrDefault(e => e.Id == id);

        public static GcLibrary Load(string json)
        {
            var lib = new GcLibrary();
            var root = MiniJson.Obj(MiniJson.Parse(json));
            var col = MiniJson.Obj(root.TryGetValue("cot", out var c) ? c : null);
            lib.TmMin = MiniJson.N(col, "t_m_phut", 1.37);
            lib.Plates = MiniJson.N(col, "so_dia_ly_thuyet", 100000);
            foreach (var o in MiniJson.A(root, "chat"))
            {
                var d = MiniJson.Obj(o);
                var e = new Entry
                {
                    Id = MiniJson.S(d, "id"), Name = MiniJson.S(d, "ten"), Formula = MiniJson.S(d, "cong_thuc"), Cas = MiniJson.S(d, "cas"),
                    Source = MiniJson.S(d, "nguon"), M = MiniJson.N(d, "M"), RI = MiniJson.N(d, "ri"), RF = MiniJson.N(d, "rf", 1),
                };
                foreach (var s in MiniJson.A(d, "ids")) e.Ids.Add(s as string);
                foreach (var p in MiniJson.A(d, "pho")) { var a = MiniJson.Arr(p); e.Spectrum.Add(((int)(double)a[0], (double)a[1])); }
                lib.Entries.Add(e);
            }
            var nen = MiniJson.Obj(root.TryGetValue("nen", out var n) ? n : null);
            foreach (var p in MiniJson.A(nen, "nuoc")) { var a = MiniJson.Arr(p); lib.Water.Add(((int)(double)a[0], (double)a[1])); }
            foreach (var p in MiniJson.A(nen, "khong_khi")) { var a = MiniJson.Arr(p); lib.Air.Add(((int)(double)a[0], (double)a[1])); }
            return lib;
        }

        // =====================================================================
        // Thời gian lưu
        // =====================================================================

        public static double LnK(double ri, double tempC)
        {
            double T = tempC + 273.15;
            return ri / 100.0 * (546.0 / T - 0.690) + 100.0 / T - 7.49;
        }

        public static double OvenTemp(Method m, double tMin) => tMin < m.Hold ? m.T0 : Math.Min(m.Tmax, m.T0 + m.Rate * (tMin - m.Hold));

        /// <summary>Thời gian chết t_M (phút) theo lưu lượng khí mang (tỉ lệ nghịch, gần đúng).</summary>
        public double DeadTime(Method m) => TmMin * 1.0 / Math.Max(0.2, m.Flow);

        /// <summary>Thời gian lưu (phút) và k lúc chất ra khỏi cột; quá maxMin mà chưa ra thì trả NaN.</summary>
        public (double tr, double kElute, double tElute) Retention(double ri, Method m, double maxMin = 60)
        {
            double tm = DeadTime(m), z = 0, t = 0, k = 0;
            const double dt = 0.002;
            while (z < 1 && t < maxMin)
            {
                double T = OvenTemp(m, t);
                k = Math.Exp(LnK(ri, T));
                z += dt / tm / (1 + k);
                t += dt;
            }
            return z < 1 ? (double.NaN, k, double.NaN) : (t, k, OvenTemp(m, t));
        }

        /// <summary>Chỉ số lưu tính ngược từ thời gian lưu theo dãy n-ankan C5 – C20 chạy cùng chương trình (van den Dool – Kratz).</summary>
        public double RetentionIndex(double tr, Method m)
        {
            double prevT = Retention(500, m).tr;
            if (tr <= prevT)
            {
                // ra trước n-C5 (lò còn đẳng nhiệt): ngoại suy theo ln k của C5, C6 (định nghĩa Kovats đẳng nhiệt)
                double tm = DeadTime(m), t6 = Retention(600, m).tr;
                double lx = Math.Log(Math.Max(1e-6, (tr - tm) / tm)), l5 = Math.Log((prevT - tm) / tm), l6 = Math.Log((t6 - tm) / tm);
                return 500 + 100 * (lx - l5) / (l6 - l5);
            }
            for (int n = 6; n <= 25; n++)
            {
                double tn = Retention(n * 100, m).tr;
                if (double.IsNaN(tn)) break;
                if (tr <= tn) return 100 * (n - 1) + 100 * (tr - prevT) / (tn - prevT);
                prevT = tn;
            }
            return double.NaN;
        }

        // =====================================================================
        // Sắc ký đồ
        // =====================================================================

        /// <summary>
        /// Các pic của mẫu: lượng chất vào cột (ng) = c·M·(1 µL)/(chia dòng)/(pha loãng). Chất không có trong thư viện mà dễ bay hơi
        /// ghi vào unknown; chất không bay hơi (đường, muối) ghi vào nonVolatile (ở lại buồng bơm mẫu).
        /// </summary>
        public List<Peak> Peaks(Mix mix, double volumeMl, Method m, double dilution, List<string> unknown = null, List<string> nonVolatile = null)
        {
            var res = new List<Peak>();
            double V = Math.Max(1e-6, volumeMl / 1000.0);
            if (mix == null) return res;
            for (int i = 0; i < mix.Count; i++)
            {
                var d = mix.SpeciesAt(i);
                double c = mix.MolAt(i) / V / Math.Max(1, dilution);
                if (c < 1e-9) continue;
                var e = Get(d.Id);
                if (e == null)
                {
                    if (d.ChemStt > 0 && !d.IsSolidPhase && c > 1e-5)
                    {
                        if (d.HasTag("cacbohidrat") || (d.HasTag("poliol") && !d.HasTag("ancol")) || d.MolarMass > 300) nonVolatile?.Add(d.Name);
                        else unknown?.Add(d.Name);
                    }
                    continue;
                }
                double ng = c * e.M * 1000.0 / Math.Max(1, m.Split);  // c (mol/L) × M (g/mol) × 1 µL = c·M µg → ng, chia dòng
                var (tr, ke, _) = Retention(e.RI, m);
                if (double.IsNaN(tr)) continue;
                int k = res.FindIndex(p => p.E == e);
                if (k >= 0) { var p = res[k]; p.AmountNg += ng; res[k] = p; continue; }
                res.Add(new Peak { E = e, Tr = tr, KElute = ke, AmountNg = ng });
            }
            for (int i = 0; i < res.Count; i++)
            {
                var p = res[i];
                double tm = DeadTime(m);
                double sigma = tm * (1 + p.KElute) / Math.Sqrt(Plates);
                sigma = Math.Sqrt(sigma * sigma + 0.008 * 0.008);  // băng bơm mẫu ~0,5 s
                if (p.AmountNg > CapacityNg) { sigma *= Math.Sqrt(p.AmountNg / CapacityNg); p.Overloaded = true; }
                p.Sigma = sigma;
                p.Area = p.AmountNg * p.E.RF * CountsPerNg;                 // đếm·phút·… (diện tích tương đối)
                p.Height = p.Area / (sigma * Math.Sqrt(2 * Math.PI));
                if (p.Height > SaturationCounts) p.Saturated = true;
                res[i] = p;
            }
            return res.OrderBy(p => p.Tr).ToList();
        }

        /// <summary>Tín hiệu TIC tại thời điểm t (phút), có cắt đỉnh khi detector bão hòa.</summary>
        public static double Tic(IList<Peak> peaks, double t)
        {
            double y = 0;
            foreach (var p in peaks)
            {
                double x = (t - p.Tr) / p.Sigma;
                if (Math.Abs(x) > 8) continue;
                double g = Math.Exp(-0.5 * x * x);
                if (p.Overloaded && x < 0) g = Math.Exp(-0.5 * x * x * 0.35);  // pic quá tải bị doãng phía trước (fronting)
                y += Math.Min(SaturationCounts, p.Height * g);
            }
            return y;
        }

        /// <summary>Diện tích pic máy đo được (detector bão hòa thì mất phần đỉnh).</summary>
        public static double MeasuredArea(Peak p)
        {
            if (!p.Saturated) return p.Area;
            // diện tích của Gauss bị cắt ở mức S: ∫min(H·g, S)
            double h = p.Height, s = SaturationCounts, sig = p.Sigma, area = 0;
            const int n = 400;
            for (int i = 0; i < n; i++)
            {
                double x = -6 + 12.0 * (i + 0.5) / n;
                area += Math.Min(s, h * Math.Exp(-0.5 * x * x)) * 12.0 / n * sig;
            }
            return area;
        }

        // =====================================================================
        // Khối phổ
        // =====================================================================

        /// <summary>Phổ khối đo được ở thời điểm t: tổng phổ các chất đang ra khỏi cột (theo chiều cao pic) + nền.</summary>
        public Dictionary<int, double> SpectrumAt(IList<Peak> peaks, double t, Method m, double airLeak = 0)
        {
            var s = new Dictionary<int, double>();
            void Add(IEnumerable<(int mz, double i)> sp, double scale)
            {
                foreach (var (mz, i) in sp)
                {
                    if (mz < m.ScanLo || mz > m.ScanHi) continue;
                    s[mz] = (s.TryGetValue(mz, out var v) ? v : 0) + i / 999.0 * scale;
                }
            }
            foreach (var p in peaks)
            {
                double x = (t - p.Tr) / p.Sigma;
                if (Math.Abs(x) > 6) continue;
                Add(p.E.Spectrum, Math.Min(SaturationCounts, p.Height * Math.Exp(-0.5 * x * x)));
            }
            Add(Water, 4e4);
            Add(Air, 2e4 + airLeak * 3e6);
            return s;
        }

        /// <summary>Độ khớp phổ kiểu NIST (0 – 999): tích vô hướng có trọng số m/z và căn cường độ.</summary>
        public static double Match(IDictionary<int, double> a, IEnumerable<(int mz, double i)> b, double scanLo)
        {
            var bd = b.Where(x => x.mz >= scanLo).ToDictionary(x => x.mz, x => x.i);
            double ab = 0, aa = 0, bb = 0;
            foreach (var mz in a.Keys.Union(bd.Keys))
            {
                double wa = Weight(mz, a.TryGetValue(mz, out var va) ? va : 0), wb = Weight(mz, bd.TryGetValue(mz, out var vb) ? vb : 0);
                ab += wa * wb; aa += wa * wa; bb += wb * wb;
            }
            return aa > 0 && bb > 0 ? 999 * ab * ab / (aa * bb) : 0;
        }

        static double Weight(int mz, double i) => i <= 0 ? 0 : Math.Pow(i, 0.6) * mz;

        /// <summary>Tra thư viện cho một phổ: các chất xếp theo độ khớp.</summary>
        public List<(Entry e, double match)> Search(IDictionary<int, double> spectrum, double scanLo) =>
            Entries.Select(e => (e, Match(spectrum, e.Spectrum, scanLo))).OrderByDescending(x => x.Item2).ToList();
    }
}
