using Khkt.Simulation.Chem;
using UnityEngine;

namespace Khkt.Interaction
{
    /// <summary>
    /// Giấy thử: nhúng vào dung dịch thì đổi màu (giấy quỳ, giấy chỉ thị vạn năng, giấy coban clorua thử nước).
    /// Màu hiện trên đầu giấy (renderer tip).
    /// </summary>
    public class TestPaper : MonoBehaviour
    {
        public enum Kind { Litmus, Universal, CobaltChloride }

        public Kind kind = Kind.Universal;
        public Renderer tipRenderer;
        public Color dryColor = new Color(0.95f, 0.85f, 0.45f);

        public Color Current { get; private set; }
        public string LastResult { get; private set; } = "";

        static MaterialPropertyBlock mpb;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        void Start() => Paint(dryColor);

        public string Dip(LabContainer c)
        {
            if (c == null || c.VolumeMl < 0.2f) return LastResult;
            double ph = c.BulkPh;
            Rgb col;
            switch (kind)
            {
                case Kind.Litmus:
                    col = ChemColor.Litmus(ph);
                    LastResult = ph < 5 ? "Quỳ hóa đỏ: môi trường axit" : ph > 8 ? "Quỳ hóa xanh: môi trường bazơ" : "Quỳ không đổi màu: gần trung tính";
                    break;
                case Kind.CobaltChloride:
                    col = Rgb.Hex("#E890B0");
                    LastResult = "Giấy coban clorua chuyển hồng: có nước";
                    break;
                default:
                    col = ChemColor.Universal(ph);
                    LastResult = $"Giấy chỉ thị: pH khoảng {System.Math.Round(ph):0}";
                    break;
            }
            Paint(new Color(col.r, col.g, col.b));
            return LastResult;
        }

        void Paint(Color c)
        {
            Current = c;
            if (tipRenderer == null) return;
            if (mpb == null) mpb = new MaterialPropertyBlock();
            tipRenderer.GetPropertyBlock(mpb);
            mpb.SetColor(BaseColorId, c);
            tipRenderer.SetPropertyBlock(mpb);
        }

        /// <summary>Lấy giấy mới (giấy đã nhúng không dùng lại).</summary>
        public void Renew()
        {
            LastResult = "";
            Paint(dryColor);
        }
    }
}
