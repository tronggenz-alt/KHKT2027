using Khkt.Simulation.Bio;
using UnityEngine;

namespace Khkt.Interaction.Bio
{
    /// <summary>
    /// Ống nhỏ giọt: hút dung dịch từ bình nào thì mang đúng dung dịch đó, mỗi lần bấm nhỏ một giọt 0,05 mL vào bình hoặc lam kính.
    /// Còn dịch cũ thì phải nhỏ hết hoặc xả ở bồn rửa mới hút chất khác, tránh nhiễm chéo.
    /// </summary>
    [DisallowMultipleComponent]
    public class BioDropper : MonoBehaviour
    {
        public const double CapacityMl = 1.0, DropMl = 0.05;
        public readonly BioMix load = new BioMix();
        BioTool tool;

        public bool IsEmpty => load.IsEmpty;
        public float SlideMolarity => BioSpecimenMap.Molarity(load);

        void Awake()
        {
            tool = GetComponent<BioTool>();
            Sync();
        }

        public void Load(BioMix m)
        {
            load.Merge(m);
            Sync();
        }

        public BioMix TakeDrop()
        {
            var d = load.Take(DropMl);
            Sync();
            return d;
        }

        public void Clear()
        {
            load.Clear();
            Sync();
        }

        /// <summary>Thuộc tính BioTool để lam kính nhận được: chất chính trong ống.</summary>
        void Sync()
        {
            if (tool == null) tool = GetComponent<BioTool>();
            if (tool == null) return;
            tool.kind = BioToolKind.StainDropper;
            string main = null;
            double most = 0;
            foreach (var kv in load.amounts)
            {
                // chất nhuộm dù ít vẫn quyết định màu giọt
                double w = kv.Value * (BioMatterDb.Get(kv.Key)?.tint > 5 ? 100 : 1);
                if (w > most) { most = w; main = kv.Key; }
            }
            tool.reagentOrSampleId = main != null ? BioSpecimenMap.SlideReagent(main) : "";
            tool.reagentDisplayName = main != null ? BioMatterDb.Name(main) : "";
            tool.carriedVolumeUl = (float)(load.Total * 1000);
        }
    }
}
