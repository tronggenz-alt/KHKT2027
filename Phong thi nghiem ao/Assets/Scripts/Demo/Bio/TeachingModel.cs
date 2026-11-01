// Tách từ StorageTools.cs: Unity chỉ lưu được thành phần khi tên lớp trùng tên file.
using System;
using UnityEngine;
using Khkt.Interaction;
using Khkt.Interaction.Bio;
using Khkt.Demo.Instruments;

namespace Khkt.Demo.Bio
{
    /// <summary>
    /// 5.3.3. Mô hình dạy học trực quan (ADN xoắn kép Watson–Crick, Tế bào nhân thực, Bộ xương thu nhỏ) — mã: mo_hinh_day_hoc
    /// </summary>
    [DisallowMultipleComponent]
    public class TeachingModel : MonoBehaviour, IInteractable, IScrollAdjustable
    {
        public static readonly string[] BasePairDetails =
        {
            "Cặp base A – T (Adenine liên kết với Thymine bằng 2 liên kết hiđrô)",
            "Cặp base G – C (Guanine liên kết với Cytosine bằng 3 liên kết hiđrô — bền nhiệt hơn)",
            "Khung đường Deoxyribose – Photphat (mạch полиnucleotit chạy ngược chiều 5'→3' và 3'→5')",
            "Chu kỳ xoắn phải B-DNA: 10 cặp base / vòng xoắn, dài 3,4 nm (34 Å), đường kính 2,0 nm"
        };
        public int index = 0;
        public float yawAngle = 0f;

        public string UseHint => $"Mô hình cấu trúc ADN xoắn kép & Tế bào [{BasePairDetails[index]}] · Bấm: xem chi tiết cặp base · Con lăn: xoay mô hình";

        public void Use(GameObject user)
        {
            string info = BasePairDetails[index];
            index = (index + 1) % BasePairDetails.Length;
            BioFeedback.PlayClick();
            BioFeedback.LogAction("mo_hinh_day_hoc", "chon_cap_base_adn", $"Mô hình ADN: {info}.");
        }

        public void OnScroll(float delta)
        {
            if (Mathf.Abs(delta) < 0.001f) return;
            yawAngle = (yawAngle + Mathf.Sign(delta) * 18f) % 360f;
            transform.localRotation = Quaternion.Euler(0f, yawAngle, 0f);
        }
    }
}
