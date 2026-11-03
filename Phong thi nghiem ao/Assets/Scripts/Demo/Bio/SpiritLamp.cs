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
    /// 5.2.2. Đèn cồn thủy tinh (tạo vùng vô trùng hình nón bán kính ~ 10–15 cm) — mã: den_con
    /// </summary>
    [DisallowMultipleComponent]
    public class SpiritLamp : MonoBehaviour, IInteractable, IBioTarget
    {
        public bool capRemoved = false;
        public bool isLit = false;
        public float sterileConeRadiusM = 0.15f;
        public Transform capVisual;
        public Transform flameVisual;

        public string UseHint => isLit
            ? "Đèn cồn thủy tinh [ĐANG CHÁY — tạo vùng đối lưu vô trùng bán kính 15 cm] · Bấm: đậy nắp chụp tắt lửa (TUYỆT ĐỐI KHÔNG THỔI)"
            : "Đèn cồn thủy tinh · Bấm để mở nắp và châm lửa tạo vùng thao tác vô trùng";

        public void BlowOutMistake()
        {
            if (!isLit) return;
            BioFeedback.RecordMistake("den_con", "thoi_tat_den_con_bang_mieng",
                "Dùng miệng thổi tắt ngọn lửa đèn cồn!",
                "Thổi bằng miệng dễ làm ngọn lửa tạt ngược vào cổ bình cồn gây cháy nổ và làm phát tán vi sinh vật từ hơi thở lên bàn cấy. Phải luôn dùng nắp chụp đậy kín để cắt nguồn oxy.");
        }

        public void SetLit(bool lit)
        {
            isLit = lit;
            capRemoved = lit;
            BioTransfer.NearSpiritFlame = lit;
            if (capVisual != null) capVisual.gameObject.SetActive(!lit);
            if (flameVisual != null) flameVisual.gameObject.SetActive(lit);
            BioFeedback.PlayClick();
            BioFeedback.LogAction("den_con", lit ? "cham_den_con" : "day_nap_tat_den_con",
                lit ? "Đã mở nắp và châm đèn cồn: tạo vùng vô trùng hình nón ~ 15 cm quanh ngọn lửa."
                    : "Đã đậy nắp chụp tắt đèn cồn an toàn.");
        }

        public void Use(GameObject user)
        {
            SetLit(!isLit);
        }

        // Cầm dụng cụ trỏ vào ngọn lửa: khử trùng que cấy, hơ nóng bình đang cầm
        public string TargetName => "Đèn cồn";

        public bool CanApplyTool(BioTool tool, out string prompt)
        {
            prompt = "";
            if (tool == null) return false;
            if (!isLit) { prompt = "Châm đèn cồn trước (tay không, bấm vào đèn)"; return true; }
            if (tool.GetComponent<InoculatingLoop>() != null) { prompt = "Đốt đỏ que cấy (giữ chuột)"; return true; }
            if (tool.GetComponent<BioVessel>() != null) { prompt = "Hơ nóng bình trên ngọn lửa (giữ chuột)"; return true; }
            return false;
        }

        public float GetRequiredHoldSeconds(BioTool tool) => tool != null && isLit ? 2f : 0f;

        public void OnToolApplyStep(BioTool tool, float dt, float progress01)
        {
            var v = tool != null && isLit ? tool.GetComponent<BioVessel>() : null;
            if (v != null) v.TemperatureC = Mathf.Min(100f, v.TemperatureC + 12f * dt * 25f / Mathf.Max(5f, (float)v.mix.Total + 5f));
        }

        public void OnToolApplyComplete(BioTool tool)
        {
            if (tool == null || !isLit) return;
            tool.GetComponent<InoculatingLoop>()?.FlameSterilize();
        }
    }
}
