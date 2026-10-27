using Khkt.Simulation;
using UnityEngine;

namespace Khkt.Interaction
{
    /// <summary>
    /// Lọ nhỏ giọt: mỗi lần bấm nhỏ một giọt (0,05 ml) vào vật chứa. Có source thì giọt lấy từ dung dịch
    /// trong lọ; có catalogStt thì giọt là hóa chất đó trong danh mục (đổi chỉ thị cho bài chuẩn độ);
    /// không có gì thì là phenolphtalein.
    /// </summary>
    public class Dropper : MonoBehaviour
    {
        public string reagent = "phenolphtalein";
        public Transform tip;
        public LabContainer source;
        public int catalogStt;

        public int DropsGiven { get; private set; }

        public void DropInto(LabContainer target)
        {
            if (target == null) return;
            if (source != null)
            {
                if (source.VolumeMl < Solution.DropMl) return;
                target.Add(source.Take(Solution.DropMl));
            }
            else if (catalogStt > 0 && ChemicalCatalog.Instance != null && ChemicalCatalog.Instance.Get(catalogStt) != null)
                target.Add(ChemicalCatalog.Instance.Make(ChemicalCatalog.Instance.Get(catalogStt), Solution.DropMl));
            else target.Add(Solution.Phenolphthalein(1));
            DropsGiven++;
        }
    }
}
