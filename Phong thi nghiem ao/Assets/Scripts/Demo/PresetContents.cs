using System.Globalization;
using Khkt.Interaction;
using Khkt.Simulation;
using Khkt.Simulation.Chem;
using UnityEngine;

namespace Khkt.Demo
{
    /// <summary>
    /// Nạp sẵn dung dịch mẫu cho một vật chứa khi bắt đầu (mẫu phân tích: nước giếng, nước thải mạ điện...).
    /// parts: "mã loài:mol/l", mã "hc:stt" là chất hữu cơ trong danh mục (glucozơ hc:578...).
    /// </summary>
    public class PresetContents : MonoBehaviour
    {
        public float volumeMl = 10f;
        public string[] parts;

        void Start()
        {
            var c = GetComponent<LabContainer>();
            if (c == null) return;
            var b = new MixBuilder();
            double L = volumeMl / 1000.0;
            foreach (var p in parts ?? new string[0])
            {
                int k = p.LastIndexOf(':');
                if (k <= 0) continue;
                string id = p.Substring(0, k);
                if (!double.TryParse(p.Substring(k + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out double conc)) continue;
                if (id.StartsWith("hc:") && int.TryParse(id.Substring(3), out int stt) && ChemicalCatalog.Instance != null)
                {
                    var e = ChemicalCatalog.Instance.Get(stt);
                    var sp = e != null ? ChemicalCatalog.Instance.OrganicSpecies(e) : null;
                    if (sp != null) b.Add(sp.Index, conc * L);
                    continue;
                }
                if (SpeciesRegistry.Get(id) != null) b.Add(id, conc * L);
            }
            c.SetContents(Solution.Of(volumeMl, b.ToMix()));
        }
    }
}
