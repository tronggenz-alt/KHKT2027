// Tách từ GeneralBioTools.cs: Unity chỉ lưu được thành phần khi tên lớp trùng tên file.
using System;
using System.Collections.Generic;
using UnityEngine;
using Khkt.Interaction;
using Khkt.Interaction.Bio;
using Khkt.Demo.Instruments;
using Khkt.Simulation.Bio;

namespace Khkt.Demo.Bio
{
    /// <summary>
    /// 5.1.11. Phễu lọc + giấy lọc gấp — mã: pheu_loc
    /// </summary>
    [DisallowMultipleComponent]
    public class FilterFunnel : MonoBehaviour, IInteractable
    {
        public bool hasFilterPaper = true;
        public bool isFiltered = false;
        public float filtrateVolumeMl = 0f;
        public BioSample filtrateSample;

        void Awake()
        {
            filtrateSample = GetComponent<BioSample>();
            if (filtrateSample == null) filtrateSample = gameObject.AddComponent<BioSample>();
        }

        public string UseHint => isFiltered
            ? $"Phễu lọc + giấy lọc [Đã thu {BioFeedback.FormatVi(filtrateVolumeMl, "0.0")} mL dịch lọc trong suốt, bã giữ trên giấy]"
            : "Phễu lọc + giấy lọc · Bấm để rót dịch nghiền từ cối sứ qua phễu lọc thu dịch trong vào ống nghiệm";

        public void Use(GameObject user)
        {
            if (!hasFilterPaper)
            {
                BioFeedback.RecordMistake("pheu_loc", "loc_quyen_giay_loc",
                    "Rót dịch nghiền qua phễu khi chưa đặt giấy lọc gấp!",
                    "Toàn bộ bã mô thực vật/động vật rơi thẳng xuống ống nghiệm làm đục mẫu, không thể kết tủa ADN sạch.");
                return;
            }
            // Trước đây bấm phễu là có 8,5 mL dịch lọc chứa ADN: nay phải có dịch nghiền thật trong cối ở gần.
            var coi = BioGanNhat.Tim<MortarPestle>(transform.position, 0.8f);
            if (coi == null || !coi.isHomogenized)
            {
                BioFeedback.ShowToast(coi == null ? "Không có cối nghiền ở gần: chưa có gì để lọc." : $"Mẫu trong cối mới nghiền {coi.grindStrokes}/{MortarPestle.RequiredStrokes} vòng, chưa thành dịch nghiền.", true);
                return;
            }
            isFiltered = true;
            filtrateVolumeMl = coi.hasExtractionBuffer ? 8.5f : 3f;
            if (filtrateSample != null)
            {
                filtrateSample.sampleId = "dich_loc_adn";
                filtrateSample.sampleName = coi.hasExtractionBuffer ? "Dịch lọc tế bào chứa ADN" : "Dịch lọc (tế bào chưa bị phá màng)";
                filtrateSample.volumeMl = filtrateVolumeMl;
                filtrateSample.dna.Clear();
                // Không có đệm (xà phòng phá màng, muối) thì ADN còn kẹt trong nhân, dịch lọc gần như không có ADN tự do
                if (coi.hasExtractionBuffer) filtrateSample.dna.Add(new BioDnaBandInfo { bp = 10000, ng = 95.0, smear = false });
                filtrateSample.SyncToData();
            }
            BioFeedback.PlayBubblePop();
            BioFeedback.LogAction("pheu_loc", "loc_dich_nghien",
                $"Đã lọc dịch nghiền qua giấy lọc: thu được {BioFeedback.FormatVi(filtrateVolumeMl, "0.0")} mL dịch lọc trong suốt vào ống nghiệm.");
        }
    }
}
