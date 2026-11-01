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
    /// 5.5.4. Giàn thủy canh dòng chảy màng dinh dưỡng NFT 3 tầng — mã: gian_thuy_canh_nft
    /// </summary>
    [DisallowMultipleComponent]
    public class NftRackController : MonoBehaviour, IInteractable
    {
        public bool pumpRunning = true;
        public bool growLedOn = true;
        public float ecMsCm = 1.8f; // Chuẩn 1,5 – 2,2 mS/cm
        public float phValue = 6.0f; // Chuẩn 5,8 – 6,5
        public bool plantsWilted = false;

        public string UseHint => $"Giàn thủy canh NFT 3 tầng [Bơm: {(pumpRunning ? "CHẠY" : "TẮT!")} · Đèn LED R/B: {(growLedOn ? "BẬT" : "TẮT")} · EC = {BioFeedback.FormatVi(ecMsCm, "0.00")} mS/cm · pH = {BioFeedback.FormatVi(phValue, "0.00")}{(plantsWilted ? " · CÂY HÉO!" : " · Cây xanh tốt")}] · Bấm: kiểm tra & châm dinh dưỡng A+B";

        public void SetToxicHighEcMistake(float highEc = 5.2f)
        {
            ecMsCm = highEc;
            plantsWilted = true;
            BioFeedback.RecordMistake("gian_thuy_canh_nft", "pha_dinh_duong_ec_qua_cao_chay_re",
                $"Pha dung dịch thủy canh quá đặc (EC = {BioFeedback.FormatVi(ecMsCm, "0.1")} mS/cm > 3,5 mS/cm) làm cây héo rũ!",
                "Nồng độ muối khoáng ưu trương làm thế nước bên ngoài thấp hơn trong tế bào lông hút, khiến rễ bị rút nước ngược (co nguyên sinh / cháy rễ).");
        }

        public void Use(GameObject user)
        {
            pumpRunning = true;
            growLedOn = true;
            ecMsCm = 1.85f;
            phValue = 6.05f;
            plantsWilted = false;
            BioFeedback.PlaySuccess();
            BioFeedback.LogAction("gian_thuy_canh_nft", "van_hanh_thuy_canh_nft",
                $"Giàn thủy canh NFT hoạt động tối ưu: Bơm màng dinh dưỡng bật, EC = {BioFeedback.FormatVi(ecMsCm, "0.00")} mS/cm, pH = {BioFeedback.FormatVi(phValue, "0.00")}.");
        }
    }
}
