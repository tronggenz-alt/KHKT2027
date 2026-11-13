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
    /// 5.1.16 & 5.4.6. Kiểm tra cân tải đối xứng cho Máy ly tâm (kết hợp Centrifuge tầng 1) — mã: may_ly_tam_de_ban, may_ly_tam_mini
    /// Công thức: RCF = 1,118 × 10⁻⁵ × r(cm) × rpm²
    /// </summary>
    [DisallowMultipleComponent]
    public class CentrifugeLoad : MonoBehaviour, IInteractable, IScrollAdjustable, IMachinePanel
    {
        public string centrifugeId = "may_ly_tam_mini";
        public int rotorSlots = 12;
        public float rotorRadiusCm = 6.5f;
        public int rpm = 6000;
        public bool lidClosed = true;
        public readonly float[] slotMassGrams = new float[24];

        public float CalculateRcf() => (float)Centrifugation.Rcf(rotorRadiusCm, rpm);

        public string UseHint => $"Máy ly tâm ({centrifugeId}) [{rpm} rpm ≈ {BioFeedback.FormatVi(CalculateRcf(), "0")} ×g · Nắp: {(lidClosed ? "ĐÓNG" : "MỞ")}] · Bấm: kiểm tra cân tải đối xứng & quay ly tâm";

        public void SetBalancedPair(int slotA, float massGrams)
        {
            Array.Clear(slotMassGrams, 0, slotMassGrams.Length);
            int n = Mathf.Clamp(rotorSlots, 2, 24);
            int slotB = (slotA + n / 2) % n;
            slotMassGrams[slotA % n] = massGrams;
            slotMassGrams[slotB] = massGrams;
        }

        public void SetUnbalancedSingle(int slotA, float massGrams)
        {
            Array.Clear(slotMassGrams, 0, slotMassGrams.Length);
            slotMassGrams[slotA % Mathf.Clamp(rotorSlots, 2, 24)] = massGrams;
        }

        public bool IsBalanced(out float imbalanceGrams)
        {
            int n = Mathf.Clamp(rotorSlots, 2, 24);
            var loadList = new List<(int slot, double grams)>();
            float fx = 0f, fy = 0f;
            for (int i = 0; i < n; i++)
            {
                if (slotMassGrams[i] > 0f)
                    loadList.Add((i + 1, slotMassGrams[i]));
                float angle = (2f * Mathf.PI * i) / n;
                fx += slotMassGrams[i] * Mathf.Cos(angle);
                fy += slotMassGrams[i] * Mathf.Sin(angle);
            }
            imbalanceGrams = Mathf.Sqrt(fx * fx + fy * fy);
            return Centrifugation.IsBalanced(loadList, n, out _);
        }

        public bool RunSpin(BioSample sample = null)
        {
            if (!lidClosed)
            {
                BioFeedback.RecordMistake(centrifugeId, "chay_ly_tam_chua_dong_nap",
                    "Bấm chạy máy ly tâm khi chưa đóng nắp an toàn!",
                    "Khóa liên động nắp máy ly tâm ngăn không cho rotor quay khi nắp mở.");
                return false;
            }
            if (!IsBalanced(out float diffG))
            {
                BioFeedback.RecordMistake(centrifugeId, "ly_tam_lech_tai",
                    $"Mất cân bằng tải rotor (lệch {BioFeedback.FormatVi(diffG, "0.00")} g > 0,10 g)! Máy rung mạnh và tự ngắt khẩn cấp!",
                    "Phải luôn đặt các ống Eppendorf có khối lượng tương đương (chênh ≤ 0,1 g) vào các vị trí đối xứng qua tâm rotor (thêm 1 ống đối trọng chứa nước nếu số mẫu lẻ).");
                return false;
            }
            BioFeedback.PlaySuccess();
            BioFeedback.LogAction(centrifugeId, "ly_tam_thanh_cong",
                $"Đã ly tâm cân bằng tại {rpm} rpm ({BioFeedback.FormatVi(CalculateRcf(), "0")} ×g): cặn lắng xuống đáy, dịch nổi tách pha rõ, giọt bám thành đã gom hết xuống đáy.");
            return true;
        }

        [NonSerialized] public float panelMinutes = 5f;
        [NonSerialized] public int panelLoad = 0;

        public MachineSpec PanelSpec()
        {
            var s = new MachineSpec { code = "MÁY · LY TÂM", title = centrifugeId == "may_ly_tam_mini" ? "Máy ly tâm mini" : "Máy ly tâm để bàn", subtitle = $"Rotor {rotorSlots} vị trí, bán kính {BioFeedback.FormatVi(rotorRadiusCm, "0.0")} cm · RCF = 1,118 × 10⁻⁵ × r × rpm²" };
            s.parameters.Add(MachineParam.Slider("Tốc độ quay", "rpm", 1000f, 14000f, 500f, () => rpm, v => rpm = Mathf.RoundToInt(v), "0",
                v => $"Lực ly tâm tương đối: {BioFeedback.FormatVi((float)Centrifugation.Rcf(rotorRadiusCm, v), "0")} ×g" + (v < 3000f ? " · gom giọt bám thành ống" : v < 9000f ? " · lắng tế bào vi khuẩn" : " · tủa ADN, tách pha")));
            s.parameters.Add(MachineParam.Slider("Thời gian", "phút", 1f, 30f, 1f, () => panelMinutes, v => panelMinutes = v, "0"));
            s.parameters.Add(MachineParam.Choice("Cách đặt ống", new[] { "Đối xứng 2 ống", "Chỉ 1 ống" }, () => panelLoad, i => panelLoad = i,
                i => i < 0.5f ? "Hai ống cùng khối lượng đặt đối xứng qua tâm rotor" : "Một ống không có đối trọng: rotor mất cân bằng"));
            s.parameters.Add(MachineParam.Choice("Nắp máy", new[] { "Mở", "Đóng" }, () => lidClosed ? 1 : 0, i => lidClosed = i == 1));
            s.runLabel = () => "Quay ly tâm";
            s.status = () => $"Đặt: {rpm} rpm ≈ {BioFeedback.FormatVi(CalculateRcf(), "0")} ×g\nNắp: {(lidClosed ? "ĐÓNG" : "MỞ")}";
            s.run = () =>
            {
                if (panelLoad == 0) SetBalancedPair(0, 1.5f); else SetUnbalancedSingle(0, 1.5f);
                if (!lidClosed) { RunSpin(); return "Máy không quay: nắp an toàn chưa đóng (khóa liên động)."; }
                if (!IsBalanced(out float diff)) { RunSpin(); return $"Máy rung mạnh và tự ngắt: rotor lệch tải {BioFeedback.FormatVi(diff, "0.00")} g.\nĐặt thêm một ống đối trọng chứa nước cùng khối lượng ở vị trí đối diện."; }
                RunSpin();
                return $"Đã ly tâm {BioFeedback.FormatVi(panelMinutes, "0")} phút ở {rpm} rpm ({BioFeedback.FormatVi(CalculateRcf(), "0")} ×g).\nCặn lắng gọn dưới đáy ống, dịch nổi trong.";
            };
            return s;
        }

        public void Use(GameObject user)
        {
            SetBalancedPair(0, 1.5f);
            RunSpin();
        }

        public void OnScroll(float delta)
        {
            if (Mathf.Abs(delta) < 0.001f) return;
            rpm = Mathf.Clamp(rpm + (delta > 0 ? 1000 : -1000), 1000, 14000);
        }
    }
}
