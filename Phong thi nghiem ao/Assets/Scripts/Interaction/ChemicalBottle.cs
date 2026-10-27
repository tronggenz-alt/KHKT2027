using Khkt.Simulation;
using UnityEngine;

namespace Khkt.Interaction
{
    /// <summary>
    /// Lọ / chai / bình khí của một hóa chất trong danh mục (theo số thứ tự). Lần đầu chạy tự nạp lượng
    /// gốc đúng thành phần. Cầm lên rồi trỏ vào vật chứa: dung dịch thì rót, chất rắn thì xúc từng thìa,
    /// lọ nhỏ giọt thì nhỏ giọt, bình khí thì sục khí, giấy thì nhúng thử.
    /// </summary>
    public class ChemicalBottle : MonoBehaviour
    {
        public int stt;
        public LabContainer container;
        [Tooltip("Lượng gốc: ml (lỏng), gam (rắn), mol (khí)")]
        public float stockAmount = 250f;
        [Tooltip("Một thìa xúc (gam)")]
        public float scoopGrams = 0.3f;
        [Tooltip("Tốc độ sục khí (mol/s)")]
        public float gasRateMolPerS = 4e-4f;
        [Tooltip("Lớp bột trong lọ chất rắn (tô màu theo danh mục, thấp dần khi xúc)")]
        public Renderer powder;

        public ChemEntry Entry { get; private set; }
        public float GramsLeft { get; private set; }
        public float GasLeftMol { get; private set; }

        public bool IsSolid => Entry != null && Entry.IsSolid;
        public bool IsGas => Entry != null && Entry.IsGas;
        public bool IsPaper => Entry != null && Entry.IsPaper;
        public string DisplayName => Entry != null ? Entry.ten : $"Hóa chất {stt}";

        void Start()
        {
            var cat = ChemicalCatalog.Instance;
            if (cat == null) return;
            Entry = cat.Get(stt);
            if (Entry == null) return;
            if (container != null)
            {
                container.label = Entry.ten;
                if (Entry.IsLiquidLike)
                {
                    container.isStock = true;
                    container.SetContents(cat.Make(Entry, stockAmount));
                }
                else if (Entry.IsSolid)
                {
                    GramsLeft = stockAmount;
                    container.SetContents(cat.Make(Entry, stockAmount));
                }
            }
            if (Entry.IsGas) GasLeftMol = stockAmount;
            if (powder != null)
            {
                powderScale = powder.transform.localScale;
                powderPos = powder.transform.localPosition;
                var c = Khkt.Simulation.Chem.Rgb.Hex(Entry.mau);
                powder.sharedMaterial = LabContainer.SharedTint(powder.sharedMaterial, new Color(c.r, c.g, c.b, 1));  // dùng chung theo màu: gộp lô được
            }
        }

        Vector3 powderScale, powderPos;

        void UpdatePowder()
        {
            if (powder == null || stockAmount <= 0) return;
            float f = Mathf.Clamp01(GramsLeft / stockAmount);
            powder.enabled = f > 0.005f;
            float h = powderScale.y * Mathf.Max(0.05f, f);
            powder.transform.localScale = new Vector3(powderScale.x, h, powderScale.z);
            powder.transform.localPosition = powderPos - Vector3.up * (powderScale.y - h);
        }

        /// <summary>Xúc một thìa chất rắn cho vào target.</summary>
        public bool ScoopInto(LabContainer target)
        {
            if (!IsSolid || container == null || target == null || GramsLeft <= 0.01f) return false;
            float g = Mathf.Min(scoopGrams, GramsLeft);
            var part = container.TakeSolidFraction(g / GramsLeft);
            GramsLeft -= g;
            target.Add(part);
            UpdatePowder();
            return true;
        }

        /// <summary>Sục khí vào target trong dt giây (target null: khí thoát ra phòng).</summary>
        public float BubbleInto(LabContainer target, float dt)
        {
            if (!IsGas || GasLeftMol <= 0) return 0;
            float mol = Mathf.Min(gasRateMolPerS * dt, GasLeftMol);
            GasLeftMol -= mol;
            if (target != null && target.VolumeMl > 0.5f)
                target.Add(ChemicalCatalog.Instance.Make(Entry, mol));
            return mol;
        }
    }
}
