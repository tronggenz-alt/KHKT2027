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

        public string UseHint => step < Steps.Length ? $"Tách chiết ADN chuối – bước {step + 1}/4: {Steps[step]} · Bấm để làm" : "Đã tách xong ADN chuối · Bấm để làm mẻ mới";

        public void Use(GameObject user)
        {
            if (step >= Steps.Length) step = 0;
            MolUi.Say($"Bước {step + 1}: {Steps[step]}");
            BioFeedback.PlayClick();
            step++;
            if (step < Steps.Length) return;

            double ug = DnaExtraction.YieldUg(5.0, "chuoi", true, true, 60.0, -20.0, 2.0);
            made++;
            var tube = BioFloor2InteractiveTools.MakeTube(outputRack != null ? outputRack : transform, $"Ống ADN chuối {made}",
                new Vector3(0, 0, -0.03f + 0.02f * ((made - 1) % 4)), "ong_eppendorf", $"ADN chuối {made}", new Color(0.95f, 0.85f, 0.2f));
            tube.mix.Add(MolCodes.BananaDna, 100f);
            tube.Changed();
            BioFeedback.PlaySuccess();
            MolUi.Say($"Thu được ~{ug:0} µg ADN thô (sợi trắng). Hòa vào 100 µL nước: ống \"ADN chuối {made}\" ở giá cạnh bộ tách chiết. ADN chuối không có gen GFP: dùng làm khuôn PCR với mồi GFP sẽ không có băng.");
            BioLabSync.Instance?.RecordExperimentResult("tach_chiet_adn", 10, "Phòng SHPT", $"Tách được ~{ug:0} µg ADN thô từ 5 g chuối");
        }
    }
}
