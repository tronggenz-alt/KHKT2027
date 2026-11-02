// Tách từ MolecularTools.cs: Unity chỉ lưu được thành phần khi tên lớp trùng tên file.
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Khkt.Interaction;
using Khkt.Interaction.Bio;
using Khkt.Demo.Instruments;
using Khkt.Simulation.Bio;

namespace Khkt.Demo.Bio
{
    [DisallowMultipleComponent]
    public class MicroTube : MonoBehaviour, IBioTarget
    {
        public string tubeTypeId = "ong_pcr";  // ong_pcr | ong_eppendorf
        public string labelText = "1";
        public MolMix mix = new MolMix();
        public bool amplified;
        public int pcrCycles;

        public float MaxUl => tubeTypeId == "ong_pcr" ? 200f : 1500f;
        public string TargetName => $"Ống {labelText}";

        void Start() => Changed();

        public bool CanApplyTool(BioTool tool, out string prompt)
        {
            prompt = "";
            var p = MolUi.Pipette(tool);
            if (p == null) return false;
            if (!p.tipAttached) prompt = "Chưa gắn đầu côn";
            else if (!p.load.IsEmpty) prompt = $"Nhả {BioFeedback.FormatVi(p.load.TotalUl, "0.0")} µL vào ống {labelText}";
            else if (mix.IsEmpty) prompt = $"Ống {labelText} đang trống";
            else prompt = $"Hút {p.VolumeText} từ ống {labelText}";
            return true;
        }

        public float GetRequiredHoldSeconds(BioTool tool) => 0f;
        public void OnToolApplyStep(BioTool tool, float dt, float progress01) { }

        public void OnToolApplyComplete(BioTool tool)
        {
            var p = MolUi.Pipette(tool);
            if (p == null || !p.tipAttached) return;
            if (!p.load.IsEmpty)
            {
                if (mix.TotalUl + p.load.TotalUl > MaxUl) { MolUi.Say($"Ống {labelText} sẽ tràn", true); return; }
                p.DispenseTube(this);
            }
            else p.AspirateTube(this);
        }

        public void Changed()
        {
            string kind = tubeTypeId == "ong_pcr" ? "Ống PCR 0,2 mL" : "Ống eppendorf 1,5 mL";
            MolUi.Info(this, $"{kind} – nhãn \"{labelText}\": {mix.Describe()}{(amplified ? $" · đã chạy PCR {pcrCycles} chu kỳ" : "")}.");
        }

        /// <summary>Thiết lập PCR từ đúng thành phần trong ống (công thức chuẩn 20 µL: 10 Master Mix + 1 mồi + 1 khuôn + 8 nước).</summary>
        public PcrSetup BuildPcrSetup(double annealC, int cycles, bool lidHeated)
        {
            float total = Mathf.Max(0.01f, mix.TotalUl);
            float mmFrac = mix.Get(MolCodes.MasterMix) / total;     // 0,5 là đúng 1×
            float primerUl = mix.Get(MolCodes.Primers);
            float gfpUl = mix.Get(MolCodes.Template);
            var s = new PcrSetup
            {
                hasBuffer = mmFrac >= 0.3f,
                hasDntp = mmFrac >= 0.3f,
                hasTaq = mmFrac >= 0.3f,
                hasPrimers = primerUl / total >= 0.015f,
                hasTemplate = gfpUl > 0.0001f,          // ADN chuối không mang gen GFP: mồi không có chỗ bám
                templateCopies = gfpUl * 2.0e5,          // 1 ng/µL plasmid ~ 2·10⁵ bản sao trong vùng mồi bám hiệu quả
                annealC = annealC,
                primerTm = 60.0,
                cycles = cycles,
                targetBp = DnaCloning.PcrProductBp,
                lidHeated = lidHeated,
                enzymeWarmMinutes = 0,
                productContamination = false,
            };
            return s;
        }
    }
}
