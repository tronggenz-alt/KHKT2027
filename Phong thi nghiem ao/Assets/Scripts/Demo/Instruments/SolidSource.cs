using System.Collections.Generic;
using System.Linq;
using Khkt.Interaction;
using Khkt.Simulation.Chem;

namespace Khkt.Demo.Instruments
{
    /// <summary>
    /// Nguồn mẫu rắn cho XRD, SEM, bom nhiệt lượng: lọc lấy kết tủa / cô cạn ống mẫu ở ổ máy, hoặc mẫu rắn chuẩn có sẵn trong hộp mẫu của máy.
    /// </summary>
    public sealed class SolidSource
    {
        public sealed class Option
        {
            public string Name;
            public int Mode;  // 0: lọc kết tủa của mẫu ở ổ, 1: cô cạn mẫu ở ổ, 2: mẫu chuẩn
            public List<SolidResidue.Phase> Fixed;
        }

        public readonly List<Option> Options = new List<Option>();
        public int Index;
        public double TakeMl = 10;
        public readonly List<string> Notes = new List<string>();

        public Option Current => Options.Count > 0 ? Options[Index % Options.Count] : null;
        public bool FromTube => Current != null && Current.Mode < 2;

        public static SolidResidue.Phase Pure(string id, string formula, string name, double mg, bool crystalline = true, bool organic = false) =>
            new SolidResidue.Phase { Id = id, Formula = formula, Name = name, Mg = mg, Crystalline = crystalline, Organic = organic };

        public SolidSource(bool tube, params (string name, SolidResidue.Phase[] phases)[] fixedSamples)
        {
            if (tube)
            {
                Options.Add(new Option { Name = "Mẫu ở ổ: lọc lấy kết tủa, sấy", Mode = 0 });
                Options.Add(new Option { Name = "Mẫu ở ổ: cô cạn, sấy khô", Mode = 1 });
            }
            foreach (var (n, ps) in fixedSamples) Options.Add(new Option { Name = n, Mode = 2, Fixed = ps.ToList() });
        }

        public void Next() => Index = (Index + 1) % System.Math.Max(1, Options.Count);
        public void Select(string namePrefix) { int i = Options.FindIndex(o => o.Name.StartsWith(namePrefix)); if (i >= 0) Index = i; }

        /// <summary>Chất rắn thu được (mg) từ lựa chọn hiện tại.</summary>
        public List<SolidResidue.Phase> Solids(LabContainer tube)
        {
            Notes.Clear();
            var o = Current;
            if (o == null) return new List<SolidResidue.Phase>();
            if (o.Mode == 2) return o.Fixed.Select(p => Pure(p.Id, p.Formula, p.Name, p.Mg, p.Crystalline, p.Organic)).ToList();
            if (tube == null) return new List<SolidResidue.Phase>();
            double take = System.Math.Min(TakeMl, tube.VolumeMl);
            return SolidResidue.From(tube.Total.Mix, tube.VolumeMl, take, o.Mode == 1, Notes);
        }

        public string Describe(LabContainer tube) => Current == null ? "-" : Current.Mode == 2 ? Current.Name : $"{Current.Name} ({TakeMl:0} mL {(tube != null ? tube.label : "—")})";
    }
}
