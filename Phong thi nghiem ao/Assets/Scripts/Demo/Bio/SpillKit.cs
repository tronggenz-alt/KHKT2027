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
    /// 5.1.21. Bộ xử lý tràn đổ sinh học (Spill Kit) — mã: bo_xu_ly_tran
    /// Trình tự 4 bước chuẩn: 1. Phủ khăn thấm -> 2. Đổ NaOCl 0,5-1% từ ngoài vào trong -> 3. Chờ 15 phút -> 4. Gom bỏ thùng nguy hại.
    /// </summary>
    [DisallowMultipleComponent]
    public class SpillKit : MonoBehaviour, IInteractable
    {
        public int currentStep = 0; // 0..4
        public bool activeSpillResolved => currentStep >= 4;

        static readonly string[] StepLabels =
        {
            "Bước 1/4: Phủ khăn giấy thấm lên toàn bộ vùng tràn đổ",
            "Bước 2/4: Đổ dung dịch NaOCl 0,5–1 % theo vòng xoắn từ NGOÀI vào TRONG",
            "Bước 3/4: Chờ 15–20 phút để NaOCl bất hoạt hoàn toàn vi sinh vật",
            "Bước 4/4: Dùng kẹp gom khăn bỏ vào túi nguy hại sinh học và ghi biên bản sự cố"
        };

        public string UseHint => activeSpillResolved
            ? "Bộ xử lý tràn đổ sinh học (Spill Kit) [Sự cố tràn đổ đã xử lý hoàn tất đúng 4 bước chuẩn] · Bấm để đặt lại"
            : $"Bộ xử lý tràn đổ sinh học (Spill Kit) [{StepLabels[currentStep]}] · Bấm để thực hiện";

        public void PerformWrongOrderMistake()
        {
            BioFeedback.RecordMistake("bo_xu_ly_tran", "xit_tay_truc_tiep_giua_vet_tran",
                "Xịt thẳng dung dịch sát khuẩn vào giữa vệt tràn khi chưa phủ khăn thấm!",
                "Tia dung dịch làm bắn tung tóe giọt bắn chứa vi sinh vật (aerosol) ra xung quanh. Phải phủ khăn thấm trước rồi đổ NaOCl 0,5–1 % từ ngoài vào trong.");
        }

        public void Use(GameObject user)
        {
            if (currentStep >= 4)
            {
                currentStep = 0;
                return;
            }
            string done = StepLabels[currentStep];
            currentStep++;
            BioFeedback.PlaySuccess();
            BioFeedback.LogAction("bo_xu_ly_tran", $"xu_ly_tran_buoc_{currentStep}",
                $"Xử lý tràn đổ sinh học: {done}. {(currentStep >= 4 ? "Vệt tràn đã được khử trùng sạch và cập nhật trạng thái 'Đã xử lý' lên web!" : "")}");
        }
    }
}
