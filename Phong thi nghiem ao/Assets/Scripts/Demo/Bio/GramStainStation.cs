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
    /// 5.2.8. Đảo nhuộm Gram 4 bước chuẩn ASM — mã: dao_nhuom_gram
    /// Gọi trực tiếp GramStain.Result của Backend.
    /// </summary>
    [DisallowMultipleComponent]
    public class GramStainStation : MonoBehaviour, IInteractable
    {
        public string currentOrganismId = "l_bulgaricus"; // Gram + (tím) hoặc e_coli_k12 Gram - (hồng)
        public bool isGramPositive = true;
        public bool heatFixed = true;
        public double smearThickness01 = 0.45;
        public double crystalVioletSec = 60.0;
        public double lugolIodineSec = 60.0;
        public double alcoholDecolorizeSec = 15.0; // Chuẩn 5..30 s; > 30 s -> mất màu tím giả!
        public double safraninSec = 45.0;

        public GramResult LastResult => GramStain.Result(
            isGramPositive, crystalVioletSec, lugolIodineSec, alcoholDecolorizeSec, safraninSec, heatFixed, smearThickness01);

        public string UseHint => $"Đảo nhuộm Gram 4 bước [Chủng: {currentOrganismId} (Gram {(isGramPositive ? "+" : "−")}) · Tẩy cồn: {BioFeedback.FormatVi((float)alcoholDecolorizeSec, "0")} s → Kết quả: {DescribeGramResult(LastResult)}] · Bấm: thực hiện quy trình nhuộm Gram";

        public static string DescribeGramResult(GramResult r)
        {
            switch (r)
            {
                case GramResult.Tim: return "Màu TÍM (Gram +)";
                case GramResult.Hong: return "Màu HỒNG ĐỎ (Gram −)";
                case GramResult.LoangLo: return "LOANG LỔ (vết bôi quá dày / thiếu bước)";
                case GramResult.KhongCoTeBao: return "KHÔNG CÓ TẾ BÀO (trôi mất do quên cố định nhiệt)";
                default: return r.ToString();
            }
        }

        public GramResult RunProtocol(
            string organismId, bool gramPos,
            bool fixedOverFlame, double thickness01,
            double cvS, double iodineS, double decolorS, double safS)
        {
            currentOrganismId = organismId;
            isGramPositive = gramPos;
            heatFixed = fixedOverFlame;
            smearThickness01 = thickness01;
            crystalVioletSec = cvS;
            lugolIodineSec = iodineS;
            alcoholDecolorizeSec = decolorS;
            safraninSec = safS;

            GramResult res = LastResult;

            if (!fixedOverFlame)
            {
                BioFeedback.RecordMistake("dao_nhuom_gram", "quen_co_dinh_nhiet_vet_boi",
                    "Quên hơ cố định vết bôi qua ngọn lửa đèn cồn trước khi nhuộm!",
                    "Vi khuẩn không bám chặt vào mặt lam kính nên bị tia nước rửa trôi sạch (KhongCoTeBao).");
            }
            else if (decolorS > GramStain.MaxDecolorizeSeconds && gramPos)
            {
                BioFeedback.RecordMistake("dao_nhuom_gram", "tay_con_qua_lau_am_tinh_gia",
                    $"Tẩy cồn 95 % quá lâu ({BioFeedback.FormatVi((float)decolorS, "0")} s > 30 s) làm vi khuẩn Gram dương ({organismId}) mất phức hợp Tím–Iốt và bắt màu Hồng (âm tính giả)!",
                    "Bước tẩy cồn chỉ được kéo dài 10–20 giây rồi phải rửa nước ngay lập tức.");
            }
            else if (decolorS < GramStain.MinDecolorizeSeconds && !gramPos)
            {
                BioFeedback.RecordMistake("dao_nhuom_gram", "tay_con_qua_ngan_duong_tinh_gia",
                    $"Tẩy cồn quá ngắn ({BioFeedback.FormatVi((float)decolorS, "0")} s < 5 s) làm vi khuẩn Gram âm ({organismId}) vẫn giữ màu Tím (dương tính giả)!",
                    "Cần tẩy cồn 10–20 giây để hòa tan lớp màng ngoài giàu lipid của vi khuẩn Gram âm.");
            }
            else
            {
                BioFeedback.PlaySuccess();
                BioFeedback.LogAction("dao_nhuom_gram", "hoan_tat_nhuom_gram",
                    $"Nhuộm Gram chuẩn chủng '{organismId}' (Gram {(gramPos ? "+" : "−")}): Kết quả = {DescribeGramResult(res)}.");
            }
            return res;
        }

        public void Use(GameObject user)
        {
            if (currentOrganismId == "l_bulgaricus")
                RunProtocol("e_coli_k12", false, true, 0.45, 60.0, 60.0, 15.0, 45.0);
            else
                RunProtocol("l_bulgaricus", true, true, 0.45, 60.0, 60.0, 15.0, 45.0);
        }
    }
}
