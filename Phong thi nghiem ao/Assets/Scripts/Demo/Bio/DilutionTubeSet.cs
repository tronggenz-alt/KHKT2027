// Tách từ MicrobioTools.cs: Unity chỉ lưu được thành phần khi tên lớp trùng tên file.
using System;
using System.Collections.Generic;
using UnityEngine;
using Khkt.Interaction;
using Khkt.Interaction.Bio;
using Khkt.Rendering;
using Khkt.Simulation.Bio;

namespace Khkt.Demo.Bio
{
    /// <summary>
    /// 5.2.13. Bộ 6 ống nghiệm pha loãng bậc 10 (10⁻¹ .. 10⁻⁶, mỗi ống 9 mL NaCl 0,9 %) — mã: bo_ong_pha_loang
    /// Gọi trực tiếp Dilution.CarryOverMl và BioSampleData.MixInto của Backend.
    /// </summary>
    [DisallowMultipleComponent]
    public class DilutionTubeSet : MonoBehaviour, IInteractable
    {
        public readonly double[] tubeCfuPerMl = new double[6];
        public bool changedTipEveryStep = true;
        public bool vortexedEveryStep = true;

        public string UseHint => $"Dãy 6 ống pha loãng bậc 10 (10⁻¹ .. 10⁻⁶ · 9 mL NaCl 0,9 %) [Ống 10⁻⁵ = {tubeCfuPerMl[4]:E2} CFU/mL · Thay côn mỗi bước: {(changedTipEveryStep ? "CÓ" : "KHÔNG!")}] · Bấm: pha loãng 10⁻¹ → 10⁻⁶";

        /// <summary>
        /// Pha loãng nối tiếp 1 mL vào 9 mL qua 6 ống. Nếu quên thay đầu côn P1000, lượng bám dư CarryOverMl(1000) làm tăng mật độ các ống sau!
        /// </summary>
        public void PerformSerialDilution(double stockCfuPerMl, bool changeTips, bool vortexEach)
        {
            changedTipEveryStep = changeTips;
            vortexedEveryStep = vortexEach;

            double carryMl = changeTips ? 0.0 : Dilution.CarryOverMl(1000.0);
            double currentConc = stockCfuPerMl;

            for (int i = 0; i < 6; i++)
            {
                // Chuyển 1,0 mL từ bậc trước + lượng bám dư từ mẫu gốc nếu không thay đầu côn
                double transferredCells = currentConc * 1.0 + (changeTips ? 0.0 : stockCfuPerMl * carryMl);
                double nextConc = transferredCells / (9.0 + 1.0 + carryMl);
                if (!vortexEach)
                {
                    // Không vortex làm phân bố không đều (lệch tất định theo bậc)
                    nextConc *= (i % 2 == 0) ? 1.35 : 0.68;
                }
                tubeCfuPerMl[i] = nextConc;
                currentConc = nextConc;
            }

            if (!changeTips)
            {
                BioFeedback.RecordMistake("bo_ong_pha_loang", "pha_loang_khong_thay_dau_con",
                    $"Pha loãng bậc 10 từ 10⁻¹ đến 10⁻⁶ mà dùng duy nhất 1 đầu côn P1000 (dư bám {BioFeedback.FormatVi((float)(carryMl * 1000.0), "0.0")} µL)!",
                    $"Vi khuẩn đậm đặc từ ống gốc bám trên thành trong đầu côn làm ống 10⁻⁵ bị vọt lên {tubeCfuPerMl[4]:E2} CFU/mL thay vì {(stockCfuPerMl * 1e-5):E2} CFU/mL. Bắt buộc thay đầu côn vô trùng mới sau mỗi bậc pha loãng.");
            }
            else if (!vortexEach)
            {
                BioFeedback.RecordMistake("bo_ong_pha_loang", "pha_loang_quen_lac_vortex",
                    "Rút 1 mL chuyển sang ống tiếp theo mà chưa lắc đều ống trên máy Vortex!",
                    "Huyền phù vi khuẩn chưa đồng nhất làm kết quả đếm CFU/mL bị sai lệch lớn.");
            }
            else
            {
                BioFeedback.PlaySuccess();
                BioFeedback.LogAction("bo_ong_pha_loang", "pha_loang_bac_10_chuan",
                    $"Đã pha loãng chuẩn 6 bậc (10⁻¹ .. 10⁻⁶) có thay đầu côn và lắc Vortex mỗi ống: ống 10⁻⁵ đạt {tubeCfuPerMl[4]:E2} CFU/mL.");
            }
        }

        public void Use(GameObject user)
        {
            PerformSerialDilution(1.25e8, true, true);
        }
    }
}
