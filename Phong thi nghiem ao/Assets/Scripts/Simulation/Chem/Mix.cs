using System;
using System.Collections.Generic;

namespace Khkt.Simulation.Chem
{
    /// <summary>
    /// Thành phần một lượng chất: số mol từng loài (mảng thưa, không đổi sau khi tạo). Trộn, chia phần
    /// tạo ra Mix mới nên Solution (struct) sao chép thoải mái mà không dùng chung dữ liệu.
    /// pH tính một lần rồi nhớ lại theo thể tích (đổi thể tích thì tính lại).
    /// </summary>
    public sealed class Mix
    {
        public static readonly Mix Empty = new Mix(Array.Empty<int>(), Array.Empty<double>());

        readonly int[] idx;
        readonly double[] mol;

        internal double cachedPh = double.NaN;
        internal double cachedPhVolume = -1;

        Mix(int[] idx, double[] mol)
        {
            this.idx = idx;
            this.mol = mol;
        }

        public int Count => idx.Length;
        public bool IsEmpty => idx.Length == 0;
        public int IndexAt(int i) => idx[i];
        public double MolAt(int i) => mol[i];
        public SpeciesDef SpeciesAt(int i) => SpeciesRegistry.At(idx[i]);

        public double Get(int speciesIndex)
        {
            int k = Array.BinarySearch(idx, speciesIndex);
            return k >= 0 ? mol[k] : 0;
        }

        public double Get(string id)
        {
            int s = SpeciesRegistry.IndexOf(id);
            return s >= 0 ? Get(s) : 0;
        }

        public bool Has(string id, double min = 1e-12) => Get(id) > min;

        public static Mix Of(params (string id, double mol)[] parts)
        {
            var b = new MixBuilder();
            foreach (var (id, m) in parts) b.Add(id, m);
            return b.ToMix();
        }

        public Mix Scale(double f)
        {
            if (f == 1 || idx.Length == 0) return this;
            if (f <= 0) return Empty;
            var m = new double[mol.Length];
            for (int i = 0; i < m.Length; i++) m[i] = mol[i] * f;
            return new Mix(idx, m);
        }

        public static Mix operator +(Mix a, Mix b)
        {
            a = a ?? Empty;
            b = b ?? Empty;
            if (a.idx.Length == 0) return b;
            if (b.idx.Length == 0) return a;
            var ri = new List<int>(a.idx.Length + b.idx.Length);
            var rm = new List<double>(a.idx.Length + b.idx.Length);
            int i = 0, j = 0;
            while (i < a.idx.Length || j < b.idx.Length)
            {
                if (j >= b.idx.Length || (i < a.idx.Length && a.idx[i] < b.idx[j])) { ri.Add(a.idx[i]); rm.Add(a.mol[i]); i++; }
                else if (i >= a.idx.Length || b.idx[j] < a.idx[i]) { ri.Add(b.idx[j]); rm.Add(b.mol[j]); j++; }
                else { ri.Add(a.idx[i]); rm.Add(a.mol[i] + b.mol[j]); i++; j++; }
            }
            return new Mix(ri.ToArray(), rm.ToArray());
        }

        /// <summary>a - b, không âm (loài về 0 thì bỏ).</summary>
        public static Mix operator -(Mix a, Mix b)
        {
            a = a ?? Empty;
            b = b ?? Empty;
            if (b.idx.Length == 0) return a;
            var bl = new MixBuilder(a);
            for (int k = 0; k < b.idx.Length; k++) bl.Add(b.idx[k], -b.mol[k]);
            return bl.ToMix();
        }

        public MixBuilder ToBuilder() => new MixBuilder(this);

        internal static Mix FromSorted(int[] idx, double[] mol) => new Mix(idx, mol);

        public override string ToString()
        {
            var parts = new List<string>();
            for (int i = 0; i < idx.Length; i++) parts.Add($"{SpeciesRegistry.At(idx[i]).Id}:{mol[i]:G4}");
            return string.Join(" ", parts);
        }
    }

    /// <summary>Thành phần sửa được (dùng trong lúc tính phản ứng), xong thì ToMix().</summary>
    public sealed class MixBuilder
    {
        readonly Dictionary<int, double> m = new Dictionary<int, double>();
        const double Tiny = 1e-15;

        public MixBuilder() { }

        public MixBuilder(Mix src)
        {
            if (src == null) return;
            for (int i = 0; i < src.Count; i++) m[src.IndexAt(i)] = src.MolAt(i);
        }

        public IEnumerable<int> Indices => m.Keys;
        public int Count => m.Count;

        public double Get(int s) => s >= 0 && m.TryGetValue(s, out var v) ? v : 0;
        public double Get(string id) => Get(SpeciesRegistry.IndexOf(id));

        public void Set(int s, double v)
        {
            if (s < 0) return;
            if (v <= Tiny) m.Remove(s);
            else m[s] = v;
        }

        public void Set(string id, double v) => Set(Index(id), v);

        public void Add(int s, double dv)
        {
            if (s < 0 || dv == 0) return;
            Set(s, Get(s) + dv);
        }

        public void Add(string id, double dv) => Add(Index(id), dv);

        /// <summary>Lấy bớt tối đa v, trả về lượng thực lấy được.</summary>
        public double Take(int s, double v)
        {
            double have = Get(s);
            double t = Math.Min(have, Math.Max(0, v));
            Set(s, have - t);
            return t;
        }

        public double Take(string id, double v) => Take(Index(id), v);

        static int Index(string id)
        {
            int s = SpeciesRegistry.IndexOf(id);
            if (s < 0) throw new ArgumentException($"Chưa có loài '{id}' trong SpeciesRegistry");
            return s;
        }

        public List<int> Snapshot() => new List<int>(m.Keys);

        public Mix ToMix()
        {
            var keys = new List<int>();
            foreach (var kv in m) if (kv.Value > Tiny) keys.Add(kv.Key);
            keys.Sort();
            var idx = keys.ToArray();
            var mol = new double[idx.Length];
            for (int i = 0; i < idx.Length; i++) mol[i] = m[idx[i]];
            return idx.Length == 0 ? Mix.Empty : Mix.FromSorted(idx, mol);
        }
    }
}
