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
    public class PcrHoodController : MonoBehaviour, IInteractable
    {
        public bool uvRunning;
        public bool sterilized;
        float uvT;
        const float UvRealSeconds = 6f;   // 15 phút chiếu UV tua nhanh còn 6 giây

        public string UseHint => uvRunning ? $"Đang chiếu UV khử ADN lạ ({Mathf.RoundToInt(uvT / UvRealSeconds * 15)}/15 phút)…"
            : sterilized ? "Tủ thao tác PCR đã chiếu UV 15 phút, sạch ADN lạ. Pha hỗn hợp PCR trong tủ." : "Bấm: chiếu UV 15 phút trước khi pha hỗn hợp PCR (phá ADN nhiễm trên bề mặt)";

        public void Use(GameObject user)
        {
            if (uvRunning) return;
            uvRunning = true;
            uvT = 0;
            BioFeedback.PlayButton();
            MolUi.Say("Đóng cửa tủ, bật đèn UV-C 15 phút. Không nhìn trực tiếp vào đèn UV.");
        }

        void Update()
        {
            if (!uvRunning) return;
            uvT += Time.deltaTime;
            if (uvT < UvRealSeconds) return;
            uvRunning = false;
            sterilized = true;
            BioFeedback.PlaySuccess();
            MolUi.Say("Đã chiếu UV xong: tắt UV, mở cửa tủ để pha hỗn hợp PCR");
        }
    }
}
