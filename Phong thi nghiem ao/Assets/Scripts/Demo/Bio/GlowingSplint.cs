// Tách từ GreenhouseAndCommonTools.cs: Unity chỉ lưu được thành phần khi tên lớp trùng tên file.
using System;
using System.Collections.Generic;
using UnityEngine;
using Khkt.Interaction;
using Khkt.Interaction.Bio;
using Khkt.Simulation.Bio;

namespace Khkt.Demo.Bio
{
    /// <summary>
    /// 5.5.1 & Bài 4 Catalase. Que đóm thử khí Oxy (O₂) — mã: que_dom
    /// Gọi trực tiếp Catalase.ActivityFactor và kiểm chứng khí O₂ từ quang hợp / phân hủy H₂O₂.
    /// </summary>
    [DisallowMultipleComponent]
    public class GlowingSplint : MonoBehaviour, IInteractable
    {
        public bool hasGlowingEmber = true;
        public bool burstIntoFlame = false;

        public string UseHint => burstIntoFlame
            ? "Que đóm thử khí [BÙNG CHÁY SÁNG RỰC — Xác nhận khí sinh ra là Oxy (O₂)!]"
            : "Que đóm còn tàn đỏ · Đưa vào miệng ống nghiệm thu khí quang hợp hoặc ống nghiệm Catalase + H₂O₂ để thử O₂";

        /// <summary>
        /// Thử phản ứng Catalase (khoai tây sống/luộc hoặc gan gà + H₂O₂ 3%) và kiểm tra bằng que đóm tàn đỏ.
        /// </summary>
        public bool TestCatalaseTube(string sampleTitle, double tempC, bool isBoiledSample)
        {
            hasGlowingEmber = true;
            double act = Catalase.ActivityFactor(tempC, isBoiledSample);
            if (isBoiledSample || act <= 0.01)
            {
                burstIntoFlame = false;
                BioFeedback.RecordMistake("que_dom", "thu_catalase_mau_da_luoc_chin_bien_tinh",
                    $"Nhỏ H₂O₂ 3 % vào '{sampleTitle}' (đã luộc chín ở 100 °C): enzyme catalase đã biến tính nhiệt (hoạt tính = {BioFeedback.FormatVi((float)act, "0.00")}) nên KHÔNG sủi bọt O₂, que đóm không bùng cháy!",
                    "Catalase có bản chất là protein, bị phá vỡ cấu trúc không gian bậc 3–4 khi đun sôi (> 65 °C).");
                return false;
            }

            burstIntoFlame = true;
            BioFeedback.PlaySuccess();
            BioFeedback.LogAction("que_dom", "que_dom_bung_chay_o2",
                $"Phản ứng Catalase ('{sampleTitle}' ở {BioFeedback.FormatVi((float)tempC, "0")} °C, hoạt tính = {BioFeedback.FormatVi((float)act, "0.00")}): 2 H₂O₂ → 2 H₂O + O₂↑ sủi bọt trắng xóa, đưa que đóm tàn đỏ vào BÙNG CHÁY SÁNG RỰC!");
            return true;
        }

        public void Use(GameObject user)
        {
            TestCatalaseTube("Gan gà / Khoai tây tươi sống", 37.0, false);
        }
    }
}
