// Tách từ StorageTools.cs: Unity chỉ lưu được thành phần khi tên lớp trùng tên file.
using System;
using UnityEngine;
using Khkt.Interaction;
using Khkt.Interaction.Bio;
using Khkt.Demo.Instruments;

namespace Khkt.Demo.Bio
{
    /// <summary>
    /// 5.3.3. Kệ inox trưng bày mẫu ngâm bảo quản — mã: ke_mau
    /// </summary>
    [DisallowMultipleComponent]
    public class SpecimenShelf : MonoBehaviour, IInteractable
    {
        public static readonly string[] Specimens =
        {
            "Mẫu ngâm Cá xương (Lớp Osteichthyes — hệ tuần hoàn đơn)",
            "Mẫu ngâm Ếch đồng (Lớp Amphibia — tim 3 ngăn, hô hấp da & phổi)",
            "Mẫu ngâm Thằn lằn bóng (Lớp Reptilia — vảy sừng chống mất nước)",
            "Mẫu ngâm Giun đất & Tôm sông (Ngành Annelida & Arthropoda)",
            "Mẫu tiêu bản lá & quả các họ Thực vật hạt kín (Magnoliophyta)"
        };
        public int currentIndex = 0;

        public string UseHint => $"Kệ mẫu ngâm bảo quản [Đang xem lọ {currentIndex + 1}/{Specimens.Length}: {Specimens[currentIndex]}] · Bấm: xoay xem & đọc thẻ phân loại";

        public void Use(GameObject user)
        {
            string item = Specimens[currentIndex];
            currentIndex = (currentIndex + 1) % Specimens.Length;
            BioFeedback.PlayClick();
            BioFeedback.LogAction("ke_mau", "xem_mau_ngam", $"Quan sát mẫu ngâm kho sinh phẩm: {item}.");
        }
    }
}
