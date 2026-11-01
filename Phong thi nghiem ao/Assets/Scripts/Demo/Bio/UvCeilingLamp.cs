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
    /// 5.2.11. Đèn UV diệt khuẩn trần 254 nm (có khóa liên động: tự tắt khi mở cửa / có người trong phòng) — mã: den_uv_tran
    /// </summary>
    [DisallowMultipleComponent]
    public class UvCeilingLamp : MonoBehaviour, IInteractable
    {
        public bool isOn = false;
        public bool personInsideRoom = false;
        public int timerMinutes = 15;
        public Light uvLightVisual;

        public string UseHint => isOn
            ? $"Đèn UV diệt khuẩn trần 254 nm [ĐANG BẬT HẸN GIỜ {timerMinutes} PHÚT — Khóa liên động cửa đang kích hoạt] · Bấm: tắt đèn UV"
            : "Công tắc đèn UV diệt khuẩn trần (254 nm, ngoài cửa) · Chỉ bật được khi không có người trong phòng";

        public bool TryTurnOn(bool someoneInside)
        {
            personInsideRoom = someoneInside;
            if (someoneInside)
            {
                isOn = false;
                if (uvLightVisual != null) uvLightVisual.enabled = false;
                BioFeedback.RecordMistake("den_uv_tran", "bat_uv_tran_khi_co_nguoi",
                    "Bật đèn UV diệt khuẩn trần (254 nm) khi đang có người bên trong phòng vi sinh!",
                    "Khóa liên động an toàn đã ngắt đèn UV! Tia UV-C bước sóng 254 nm phá hủy ADN biểu mô gây viêm giác mạc cấp và bỏng da.");
                return false;
            }
            isOn = true;
            if (uvLightVisual != null) uvLightVisual.enabled = true;
            BioFeedback.PlayClick();
            BioFeedback.LogAction("den_uv_tran", "bat_den_uv_tran", $"Đã bật đèn UV diệt khuẩn trần 254 nm hẹn giờ {timerMinutes} phút (phòng không có người).");
            return true;
        }

        public void OnDoorOpenedSafetyCutoff()
        {
            if (!isOn) return;
            isOn = false;
            if (uvLightVisual != null) uvLightVisual.enabled = false;
            BioFeedback.ShowToast("Cửa phòng mở -> Khóa liên động tự động ngắt đèn UV trần 254 nm!");
            BioFeedback.LogAction("den_uv_tran", "interlock_ngat_uv_khi_mo_cua", "Khóa liên động tự động tắt đèn UV trần ngay khi mở cửa buồng đệm.");
        }

        public void Use(GameObject user)
        {
            if (isOn)
            {
                isOn = false;
                if (uvLightVisual != null) uvLightVisual.enabled = false;
                BioFeedback.PlayClick();
            }
            else
            {
                TryTurnOn(false);
            }
        }
    }
}
