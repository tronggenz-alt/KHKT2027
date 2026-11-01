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
    /// 5.1.16 & 5.4.5. Máy lắc ống nghiệm Vortex — mã: may_vortex
    /// </summary>
    [DisallowMultipleComponent]
    public class VortexMixer : MonoBehaviour, IInteractable, IScrollAdjustable
    {
        public int speedLevel = 5; // 0..10
        public bool continuousMode = false;
        public bool isVibrating = false;

        public string UseHint => $"Máy lắc Vortex [Mức tốc độ: {speedLevel}/10 · Chế độ: {(continuousMode ? "Liên tục" : "Chạm (Touch)")}] · Bấm: trộn đều ống mẫu · Con lăn: đổi tốc độ";

        public void VortexSample(BioSample sample, float durationSeconds)
        {
            isVibrating = true;
            if (sample != null && sample.dna.Count > 0 && speedLevel >= 9 && durationSeconds > 5f)
            {
                for (int i = 0; i < sample.dna.Count; i++)
                {
                    var b = sample.dna[i];
                    if (b.bp >= 5000)
                    {
                        b.smear = true;
                        sample.dna[i] = b;
                    }
                }
                sample.SyncToData();
                BioFeedback.RecordMistake("may_vortex", "vortex_adn_bo_gen_qua_manh",
                    $"Lắc Vortex ADN bộ gen ở tốc độ tối đa ({speedLevel}/10) quá lâu làm đứt gãy cơ học sợi ADN!",
                    "ADN bộ gen kích thước lớn rất dễ bị lực cắt thủy động làm đứt gãy tạo vệt kéo dài (smear) khi chạy điện di.");
                return;
            }
            BioFeedback.PlayClick();
            BioFeedback.LogAction("may_vortex", "lac_tron_ong_nghiem",
                $"Đã lắc trộn đồng nhất dung dịch trên máy Vortex ở mức {speedLevel}/10.");
        }

        public void Use(GameObject user)
        {
            VortexSample(null, 2.0f);
        }

        public void OnScroll(float delta)
        {
            if (Mathf.Abs(delta) < 0.001f) return;
            speedLevel = Mathf.Clamp(speedLevel + (delta > 0 ? 1 : -1), 1, 10);
        }
    }
}
