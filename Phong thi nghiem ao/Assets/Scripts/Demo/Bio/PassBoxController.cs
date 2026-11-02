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
    /// 5.2.12. Hộp chuyển mẫu xuyên tường (Pass-box) 2 cửa khóa liên động + đèn UV 5 phút — mã: pass_box
    /// </summary>
    [DisallowMultipleComponent]
    public class PassBoxController : MonoBehaviour, IInteractable
    {
        public bool generalSideDoorOpen = false;
        public bool microbioSideDoorOpen = false;
        public bool uvSterilized5Min = false;
        public string itemInside = "Giá đĩa Petri & Môi trường LB";

        public string UseHint => $"Pass-box xuyên tường [Cửa phòng chung: {(generalSideDoorOpen ? "MỞ" : "ĐÓNG")} · Cửa vi sinh: {(microbioSideDoorOpen ? "MỞ" : "ĐÓNG")} · UV 5 phút: {(uvSterilized5Min ? "ĐÃ KHỬ TRÙNG" : "Chưa")}] · Bấm: chuyển mẫu qua Pass-box";

        public bool TryOpenBothDoorsMistake()
        {
            generalSideDoorOpen = true;
            BioFeedback.RecordMistake("pass_box", "mo_2_cua_pass_box_cung_luc",
                "Cố mở cửa Pass-box phía phòng vi sinh khi cửa phía phòng chung đang mở!",
                "Chốt khóa liên động cơ–điện từ của Pass-box khóa chặt cửa đối diện để ngăn thông khí trực tiếp giữa hai phòng.");
            return false;
        }

        public void RunTransferCycle()
        {
            generalSideDoorOpen = false;
            microbioSideDoorOpen = false;
            uvSterilized5Min = true;
            microbioSideDoorOpen = true;
            BioFeedback.PlaySuccess();
            BioFeedback.LogAction("pass_box", "chuyen_do_qua_pass_box",
                $"Đã đặt '{itemInside}' vào Pass-box, đóng kín hai cửa, chiếu UV 5 phút rồi mở cửa phía phòng vi sinh lấy ra.");
        }

        public void Use(GameObject user)
        {
            RunTransferCycle();
        }
    }
}
