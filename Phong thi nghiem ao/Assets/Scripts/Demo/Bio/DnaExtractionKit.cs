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
    public class DnaExtractionKit : MonoBehaviour, IInteractable
    {
        public Transform outputRack;
        public int step;
        int made;

        static readonly string[] Steps =
        {
            "Nghiền 5 g chuối với 10 mL đệm ly giải (nước rửa bát phá màng tế bào, màng nhân + 1 thìa muối NaCl)",
            "Ủ dịch nghiền ở bể ổn nhiệt 60 °C trong 10 phút (enzyme phân hủy ADN bị bất hoạt)",
            "Lọc qua phễu giấy lọc lấy dịch trong vào ống nghiệm",
            "Rót nhẹ 2 thể tích cồn 90° lạnh theo thành ống: ADN kết tủa thành sợi trắng ở mặt phân cách, quấn bằng đũa thủy tinh",
        };

        public string UseHint => "Trạm thu ADN chuối cho phòng sinh học phân tử · Mang cốc đã tách ADN (sợi trắng trong lớp cồn lạnh) tới đây rồi bấm để hòa ADN vào ống Eppendorf";

        /// <summary>
        /// Trước đây bấm 4 lần là đi hết 4 bước in sẵn và luôn ra ống ADN. Nay trạm chỉ thu ADN có thật: cốc/ống ở gần (1,5 m)
        /// phải đã tách ra sợi ADN theo luật của lab Sinh tự do (đệm phá màng + muối + cồn lạnh, xem BioRules).
        /// </summary>
        public void Use(GameObject user)
        {
            var binh = BioGanNhat.Tim<BioVessel>(transform.position, 1.5f, b => !b.isStock && b.mix.dnaThreads >= 0.01);
            if (binh == null)
            {
                var conTho = BioGanNhat.Tim<BioVessel>(transform.position, 1.5f, b => !b.isStock && !b.mix.IsEmpty);
                MolUi.Say(conTho == null
                    ? "Chưa có cốc nào ở gần chứa ADN đã tách. Ở bàn lab Sinh: thái chuối vào cốc, thêm nước rửa chén và muối, ủ ấm, lọc, rót cồn lạnh theo thành cốc cho tới khi thấy sợi trắng, rồi mang cốc tới đây."
                    : $"{conTho.label} chưa có sợi ADN kết tủa (thiếu đệm phá màng, muối hoặc cồn lạnh), chưa thu được ADN.");
                BioFeedback.PlayWarning();
                return;
            }
            double ug = binh.mix.dnaThreads * 100.0;
            binh.mix.dnaThreads = 0;
            binh.RefreshVisual(true);
            made++;
            var tube = BioFloor2InteractiveTools.MakeTube(outputRack != null ? outputRack : transform, $"Ống ADN chuối {made}",
                new Vector3(0, 0, -0.03f + 0.02f * ((made - 1) % 4)), "ong_eppendorf", $"ADN chuối {made}", new Color(0.95f, 0.85f, 0.2f));
            tube.mix.Add(MolCodes.BananaDna, 100f);
            tube.Changed();
            BioFeedback.PlaySuccess();
            MolUi.Say($"Quấn sợi ADN từ {binh.label} (~{ug:0} µg), hòa vào 100 µL nước: ống \"ADN chuối {made}\" ở giá cạnh trạm. ADN chuối không có gen GFP.");
            BioLabSync.Instance?.RecordExperimentResult("tach_chiet_adn", 10, "Phòng SHPT", $"Thu ~{ug:0} µg ADN thô từ {binh.label}");
        }
    }
}
