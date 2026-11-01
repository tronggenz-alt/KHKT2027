// Tách từ StorageTools.cs: Unity chỉ lưu được thành phần khi tên lớp trùng tên file.
using System;
using UnityEngine;
using Khkt.Interaction;
using Khkt.Interaction.Bio;
using Khkt.Demo.Instruments;

namespace Khkt.Demo.Bio
{
    /// <summary>
    /// 5.3.1 & 5.3.2. Tủ mát bảo quản sinh phẩm 4 °C & Tủ đông sâu −20 °C — mã: tu_mat_4, tu_dong_am20
    /// </summary>
    [DisallowMultipleComponent]
    public class FridgeController : MonoBehaviour, IInteractable
    {
        public string fridgeId = "tu_mat_4"; // tu_mat_4 | tu_dong_am20
        public bool isDeepFreezer = false;
        public float setTempC = 4.0f;
        public float currentTempC = 4.0f;
        public bool doorOpen = false;
        public float doorOpenSeconds = 0f;
        public bool alarmTriggered = false;
        public bool taqLeftAtRoomTempMistake = false;
        public float taqActivityRetention = 1.0f; // 1.0 khi giữ trong hộp đá; giảm nếu để ngoài nhiệt độ phòng
        public BioHinge leftDoorHinge;
        public BioHinge rightDoorHinge;

        void Awake()
        {
            if (fridgeId == "tu_dong_am20" || isDeepFreezer)
            {
                isDeepFreezer = true;
                setTempC = -20.0f;
                currentTempC = -20.0f;
            }
        }

        public string UseHint => $"Tủ bảo quản ({fridgeId}) [Đặt: {BioFeedback.FormatVi(setTempC, "0.0")} °C · Thực: {BioFeedback.FormatVi(currentTempC, "0.0")} °C · Cửa: {(doorOpen ? $"ĐANG MỞ ({BioFeedback.FormatVi(doorOpenSeconds, "0")} s)" : "ĐÓNG KÍN")}{(alarmTriggered ? " · CÒI BÁO ĐỎ (> 60 s)!" : "")}] · Bấm: mở/đóng cửa & lấy sinh phẩm";

        public void SimulateDoorLeftOpen(float openSec)
        {
            doorOpen = true;
            doorOpenSeconds = openSec;
            currentTempC = isDeepFreezer
                ? Mathf.Min(-2.0f, setTempC + openSec * 0.12f)
                : Mathf.Min(18.0f, setTempC + openSec * 0.08f);

            if (openSec > 60.0f)
            {
                alarmTriggered = true;
                BioFeedback.RecordMistake(fridgeId, "mo_cua_tu_lanh_qua_60_giay",
                    $"Mở cửa {fridgeId} quá lâu ({BioFeedback.FormatVi(openSec, "0")} s > 60 s) làm nhiệt độ vọt lên {BioFeedback.FormatVi(currentTempC, "0.1")} °C!",
                    "Màn LED đỏ nhấp nháy và còi báo động kêu! Nhiệt độ tăng vượt ngưỡng an toàn làm giảm hoạt tính enzyme/kháng sinh và hỏng môi trường bảo quản.");
            }
        }

        public void TakeEnzymeOutOfFreezer(bool placedOnIceImmediately)
        {
            if (!placedOnIceImmediately)
            {
                taqLeftAtRoomTempMistake = true;
                taqActivityRetention = 0.25f;
                BioFeedback.RecordMistake("tu_dong_am20", "lay_enzyme_taq_khong_dat_hop_da",
                    "Lấy enzyme Taq polymerase từ tủ đông −20 °C ra bàn ở nhiệt độ phòng mà KHÔNG đặt vào hộp đá!",
                    "Enzyme và mồi PCR bị giảm hoạt tính nhanh ở nhiệt độ phòng làm hiệu suất nhân bản PCR tụt mạnh.");
            }
            else
            {
                taqLeftAtRoomTempMistake = false;
                taqActivityRetention = 1.0f;
                BioFeedback.PlaySuccess();
                BioFeedback.LogAction("tu_dong_am20", "lay_enzyme_dat_hop_da",
                    "Đã lấy bộ sinh phẩm PCR (Taq polymerase, dNTPs, mồi) từ tủ đông −20 °C đặt ngay vào hộp đá 0–4 °C.");
            }
        }

        public void Use(GameObject user)
        {
            doorOpen = !doorOpen;
            if (!doorOpen)
            {
                doorOpenSeconds = 0f;
                alarmTriggered = false;
                currentTempC = setTempC;
            }
            if (leftDoorHinge != null) leftDoorHinge.SetOpen(doorOpen);
            if (rightDoorHinge != null) rightDoorHinge.SetOpen(doorOpen);
            BioFeedback.PlayClick();
            BioFeedback.LogAction(fridgeId, doorOpen ? "mo_cua_tu" : "dong_cua_tu",
                $"{fridgeId}: {(doorOpen ? "Mở cửa lấy sinh phẩm" : $"Đóng kín cửa, nhiệt độ ổn định ở {BioFeedback.FormatVi(currentTempC, "0.0")} °C")}.");
        }
    }
}
