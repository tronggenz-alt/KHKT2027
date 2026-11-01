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
    /// 5.1.20 & 5.6. Quầy rửa tay cảm ứng (xà phòng -> vòi nước >= 20 s -> giấy lau -> bật cờ tayDaRua) — mã: quay_rua_tay
    /// </summary>
    [DisallowMultipleComponent]
    public class HandWashStation : MonoBehaviour, IInteractable
    {
        public bool soapApplied = false;
        public float washSeconds = 0f;
        public bool handsDried = false;

        public string UseHint => BioTransfer.HandsWashed
            ? "Quầy rửa tay cảm ứng [Đã rửa tay đúng quy trình 6 bước ≥ 20 s (tayDaRua = true)]"
            : "Quầy rửa tay cảm ứng (xà phòng + vòi cảm ứng + giấy lau) · Bấm để rửa tay khử khuẩn 20 s trước khi thao tác vô trùng";

        public void CompleteWashRoutine(float seconds = 22f)
        {
            soapApplied = true;
            washSeconds = seconds;
            if (seconds < 20f)
            {
                BioTransfer.HandsWashed = false;
                BioFeedback.RecordMistake("quay_rua_tay", "rua_tay_chua_du_20_giay",
                    $"Rửa tay quá nhanh ({BioFeedback.FormatVi(seconds, "0")} s < 20 s) chưa sạch vi sinh vật trên da!",
                    "Theo quy trình WHO/CDC, phải chà xà phòng dưới vòi nước chảy tối thiểu 20 giây rồi lau khô bằng khăn giấy sạch.");
                return;
            }
            handsDried = true;
            BioTransfer.HandsWashed = true;
            BioFeedback.PlaySuccess();
            BioFeedback.LogAction("quay_rua_tay", "rua_tay_chuan_20s",
                "Đã rửa tay bằng xà phòng dưới vòi cảm ứng đủ 20 s và lau khô (tayDaRua = true).");
        }

        public void Use(GameObject user)
        {
            CompleteWashRoutine(22f);
        }
    }
}
