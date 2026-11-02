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
    /// 5.5.3. Hộp thí nghiệm tính hướng sáng dương (Phototropism Box) — mã: hop_huong_sang
    /// Gọi trực tiếp Phototropism.BendAngleDeg và Phototropism.EtiolationFactor của Backend.
    /// </summary>
    [DisallowMultipleComponent]
    public class PhototropismBox : MonoBehaviour, IInteractable
    {
        public double lightAzimuthDeg = 45.0; // Cửa sổ sáng bên phải (+45°), bên trái (-45°), trên (0°)
        public bool fullyDark = false;
        public double elapsedHours = 24.0;
        public Transform seedlingStemVisual;

        public double CurrentBendDeg => Phototropism.BendAngleDeg(elapsedHours, lightAzimuthDeg, fullyDark);
        public double CurrentEtiolation => Phototropism.EtiolationFactor(elapsedHours, fullyDark);

        public string UseHint => $"Hộp hướng sáng thực vật [Cửa sáng: {(fullyDark ? "ĐÓNG KÍN HOÀN TOÀN (Tối)" : $"Lệch {BioFeedback.FormatVi((float)lightAzimuthDeg, "0")}°")} · {BioFeedback.FormatVi((float)elapsedHours, "0")} giờ → Góc uốn thân mầm = {BioFeedback.FormatVi((float)CurrentBendDeg, "0.1")}° · Vươn dài = ×{BioFeedback.FormatVi((float)CurrentEtiolation, "0.00")}] · Bấm: tua 24 giờ / đổi cửa sáng";

        public void RunExperiment(double hours, double windowAngleDeg, bool darkBoxMistake = false)
        {
            elapsedHours = hours;
            lightAzimuthDeg = windowAngleDeg;
            fullyDark = darkBoxMistake;

            if (seedlingStemVisual != null)
            {
                seedlingStemVisual.localRotation = Quaternion.Euler(0f, 0f, -(float)CurrentBendDeg);
                seedlingStemVisual.localScale = new Vector3(1f, (float)CurrentEtiolation, 1f);
            }

            if (darkBoxMistake)
            {
                BioFeedback.RecordMistake("hop_huong_sang", "dong_kin_moi_cua_so_cay_moc_vong_vang_ua",
                    $"Đóng kín toàn bộ cửa sổ ánh sáng suốt {BioFeedback.FormatVi((float)hours, "0")} giờ!",
                    $"Auxin phân bố đều nên thân mầm không uốn cong (0°), cây bị hiện tượng mọc vống vàng úa (etiolation ×{BioFeedback.FormatVi((float)CurrentEtiolation, "0.00")}): thân vươn dài yếu ớt, lá màu vàng nhạt do không tổng hợp được diệp lục.");
            }
            else
            {
                BioFeedback.PlaySuccess();
                BioFeedback.LogAction("hop_huong_sang", "uon_cong_huong_sang_duong",
                    $"Sau {BioFeedback.FormatVi((float)hours, "0")} giờ chiếu sáng từ cửa sổ góc {BioFeedback.FormatVi((float)windowAngleDeg, "0")}°: auxin tích tụ phía khuất sáng kích thích tế bào dãn dài nhanh hơn, làm ngọn mầm đậu uốn cong {BioFeedback.FormatVi((float)CurrentBendDeg, "0.1")}° về phía nguồn sáng!");
            }
        }

        public void Use(GameObject user)
        {
            RunExperiment(elapsedHours + 12.0, 55.0, false);
        }
    }
}
