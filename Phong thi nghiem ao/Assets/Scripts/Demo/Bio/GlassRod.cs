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
    /// 5.1.7. Đũa thủy tinh (khuấy dung dịch và quấn sợi ADN kết tủa) — mã: dua_thuy_tinh
    /// </summary>
    [DisallowMultipleComponent]
    public class GlassRod : MonoBehaviour, IInteractable
    {
        public bool hasSpoolDna = false;
        public float spooledDnaUg = 0f;

        public string UseHint => hasSpoolDna
            ? $"Đũa thủy tinh [Đã quấn kết tủa sợi ADN trắng đục: ~{BioFeedback.FormatVi(spooledDnaUg, "0.0")} µg]"
            : "Đũa thủy tinh · Bấm để khuấy nhẹ hoặc xoay tròn ở mặt phân cách nước – cồn lạnh để quấn sợi ADN";

        public void SpoolDna(bool ethanolCold, bool hasSalt, double sampleGrams = 10.0, string sampleKind = "chuoi")
        {
            double ethTempC = ethanolCold ? -20.0 : 25.0;
            spooledDnaUg = (float)DnaExtraction.YieldUg(sampleGrams, sampleKind, hasSalt, true, 60.0, ethTempC, 2.0);
            if (!ethanolCold || !hasSalt)
            {
                hasSpoolDna = spooledDnaUg > 15f;
                BioFeedback.RecordMistake("dua_thuy_tinh", "ket_tua_adn_con_khong_lanh",
                    !ethanolCold ? $"Dùng cồn ở nhiệt độ phòng (25 °C) nên hiệu suất thu hồi ADN chỉ đạt {BioFeedback.FormatVi(spooledDnaUg, "0.1")} µg!" : "Thiếu muối NaCl trung hòa điện tích âm của gốc photphat nên ADN hầu như không kết tủa!",
                    "Để tách chiết ADN thành sợi trắng nổi rõ ở ranh giới pha, phải có Na⁺ trung hòa khung đường–photphat và cồn 90–96 % thật lạnh (−20 °C).");
                return;
            }
            hasSpoolDna = true;
            BioFeedback.PlaySuccess();
            BioFeedback.LogAction("dua_thuy_tinh", "quan_soi_adn",
                $"Đã xoay đũa thủy tinh tại mặt phân cách cồn lạnh (−20 °C) – dịch lọc, thu được {BioFeedback.FormatVi(spooledDnaUg, "0.1")} µg búi sợi ADN trắng đục!");
        }

        public void Use(GameObject user)
        {
            SpoolDna(true, true);
        }
    }
}
