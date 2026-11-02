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
    /// 5.2.10. Máy đếm khuẩn lạc (Colony Counter, bàn đèn nền + kính lúp + bút cảm biến) — mã: may_dem_khuan_lac
    /// Gọi trực tiếp Dilution.CfuPerMl và Dilution.IsCountable của Backend (chuẩn 30..300 khuẩn lạc).
    /// </summary>
    [DisallowMultipleComponent]
    public class ColonyCounterController : MonoBehaviour, IInteractable
    {
        public PetriDish currentDish;
        public int manualCount = 0;
        public int autoCount = 0;
        public double platedVolumeMl = 0.1;
        public double dilutionFactor = 1e-5;

        public double CalculatedCfuPerMl => Dilution.CfuPerMl(autoCount > 0 ? autoCount : manualCount, platedVolumeMl, dilutionFactor);
        public bool IsValidRange => Dilution.IsCountable(autoCount > 0 ? autoCount : manualCount);

        public string UseHint
        {
            get
            {
                int c = autoCount > 0 ? autoCount : manualCount;
                return $"Máy đếm khuẩn lạc [Số đếm: {c} khuẩn lạc ({(IsValidRange ? "Hợp lệ 30–300" : "Ngoài khoảng 30–300!")}) · Nồng độ gốc = {CalculatedCfuPerMl:E2} CFU/mL] · Bấm: đếm đĩa Petri & tính CFU/mL";
            }
        }

        public double EvaluateDish(PetriDish dish)
        {
            currentDish = dish;
            if (dish != null)
            {
                autoCount = dish.countedColonies > 0 ? dish.countedColonies : dish.grownColonies.Count;
                manualCount = autoCount;
                platedVolumeMl = dish.platedVolumeMl > 0 ? dish.platedVolumeMl : 0.1;
                dilutionFactor = dish.dilutionFactor > 0 ? dish.dilutionFactor : 1e-5;
            }
            else
            {
                autoCount = 128;
                manualCount = 128;
            }

            if (!Dilution.IsCountable(autoCount))
            {
                BioFeedback.RecordMistake("may_dem_khuan_lac", "dem_dia_ngoai_khoang_30_300",
                    $"Chọn đĩa có {autoCount} khuẩn lạc (nằm ngoài khoảng tiêu chuẩn 30 – 300 khuẩn lạc theo ISO 4833) để tính mật độ!",
                    autoCount < 30
                        ? "Đĩa dưới 30 khuẩn lạc có sai số thống kê ngẫu nhiên quá lớn."
                        : "Đĩa trên 300 khuẩn lạc (TNTC) có các khuẩn lạc mọc chồng lấp làm đếm thiếu.");
            }
            else
            {
                BioFeedback.PlaySuccess();
                BioFeedback.LogAction("may_dem_khuan_lac", "tinh_cfu_ml",
                    $"Đếm được {autoCount} khuẩn lạc (hợp lệ 30–300) ở độ pha loãng {dilutionFactor:E0}, thể tích cấy {BioFeedback.FormatVi((float)platedVolumeMl, "0.1")} mL → Mật độ mẫu gốc = {CalculatedCfuPerMl:E2} CFU/mL.");
            }
            return CalculatedCfuPerMl;
        }

        public void Use(GameObject user)
        {
            EvaluateDish(currentDish);
        }
    }
}
