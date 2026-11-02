using UnityEngine;

namespace Khkt.Interaction.Bio
{
    /// <summary>
    /// Bộ điều phối chuyển mẫu sinh học (6 trường hợp cây quyết định) và kiểm tra hậu quả thao tác sai khoa học.
    /// </summary>
    public static class BioTransfer
    {
        // Cờ trạng thái vô trùng toàn cục (5.1.20, 5.1.21, 5.2.1, 5.2.2)
        public static bool HandsWashed = false;
        public static bool PpeEquipped = false;
        public static bool SurfaceClean = false;
        public static bool InsideBsc = false;
        public static bool BscRunning = false;
        public static double BscPurgeMinutes = 0.0;
        public static double BscSashHeightCm = 20.0;
        public static bool BscGrilleBlocked = false;
        public static bool NearSpiritFlame = false;

        public static Khkt.Simulation.Bio.AsepticContext BuildAsepticContext(bool toolSterile, double openSeconds)
        {
            return new Khkt.Simulation.Bio.AsepticContext
            {
                insideBsc = InsideBsc,
                bscRunning = BscRunning,
                bscPurgeMinutes = BscPurgeMinutes,
                sashHeightCm = BscSashHeightCm,
                grilleBlocked = BscGrilleBlocked,
                surfaceClean = SurfaceClean,
                handsWashed = HandsWashed,
                ppeOn = PpeEquipped,
                nearFlame = NearSpiritFlame,
                toolSterile = toolSterile,
                openSeconds = openSeconds
            };
        }

        public static bool CheckContamination(bool toolSterile, double openSeconds, int seed, BioSample target)
        {
            var ctx = BuildAsepticContext(toolSterile, openSeconds);
            if (Khkt.Simulation.Bio.ContaminationRules.Roll(in ctx, seed, out var contam))
            {
                if (target != null)
                {
                    target.isContaminated = true;
                    target.sterile = false;
                    target.contaminationReason = $"Tạp nhiễm {contam.organismId} ({contam.colonies} khuẩn lạc)";
                    target.contaminants.Add(new BioContaminantInfo { organismId = contam.organismId, colonies = contam.colonies });
                    target.SyncToData();
                }
                return true;
            }
            return false;
        }
        /// <summary>
        /// Trường hợp 1: Tách / gắp mẫu mô (biểu bì hành, lá rong mái chèo) đặt lên lam kính.
        /// </summary>
        public static bool PlaceSpecimenOnSlide(BioTool forcepsOrScalpel, BioSample slide, string specimenId, string specimenName)
        {
            if (slide == null) return false;
            if (slide.hasCoverslip)
            {
                BioFeedback.RecordMistake("lam_kinh", "dat_mau_khi_da_day_lamen",
                    "Không thể đặt mẫu lên trên lamen đã đậy!",
                    "Phải đặt mẫu vào giọt nước/thuốc nhuộm trên mặt lam kính trước, dàn phẳng rồi mới hạ lamen góc 45°.");
                return false;
            }

            slide.sampleId = specimenId;
            slide.sampleName = specimenName;
            slide.isSpreadFlat = false;
            slide.SetLiquidColor(new Color(0.84f, 0.92f, 0.86f, 0.5f));
            BioFeedback.PlaySuccess();
            BioFeedback.LogAction("lam_kinh", "dat_mau_tieu_ban", $"Đã đặt mẫu '{specimenName}' lên lam kính. Hãy dùng kim mũi mác dàn phẳng mẫu.");
            return true;
        }

        /// <summary>
        /// Dùng kim mũi mác dàn phẳng biểu bì hành / mẫu mô trên lam kính.
        /// </summary>
        public static bool SpreadSpecimenWithNeedle(BioSample slide)
        {
            if (slide == null || string.IsNullOrEmpty(slide.sampleId))
            {
                BioFeedback.ShowToast("Lam kính chưa có mẫu vật để dàn phẳng.", true);
                return false;
            }
            slide.isSpreadFlat = true;
            BioFeedback.PlayClick();
            BioFeedback.LogAction("kim_mui_mac", "dan_phang_mau", $"Đã dàn phẳng mẫu '{slide.sampleName}' bằng kim mũi mác, không còn nếp gấp tế bào.");
            return true;
        }

        /// <summary>
        /// Trường hợp 2: Nhỏ thuốc nhuộm lên tiêu bản trên lam kính.
        /// </summary>
        public static bool ApplyStainToSlide(BioSample slide, string stainId, string stainName, Color stainColor)
        {
            if (slide == null || string.IsNullOrEmpty(slide.sampleId))
            {
                BioFeedback.RecordMistake("lam_kinh", "nhuom_khi_chua_co_mau",
                    "Chưa có mẫu vật trên lam kính mà đã nhỏ thuốc nhuộm.",
                    "Cần đặt mẫu lên lam kính trước hoặc nhỏ thuốc nhuộm ở mép lamen để dẫn truyền mao dẫn.");
                return false;
            }

            slide.stainId = stainId;
            slide.stainName = stainName;
            slide.hasExcessStain = true;
            slide.isBlotted = false;
            slide.SetLiquidColor(stainColor);
            BioFeedback.PlayBubblePop();
            BioFeedback.LogAction(stainId, "nho_thuoc_nhuom", $"Đã nhỏ {stainName} lên mẫu '{slide.sampleName}'. Dùng giấy thấm hút dịch thừa ở mép lamen.");
            return true;
        }

        /// <summary>
        /// Trường hợp 3: Hạ lamen đậy tiêu bản (góc 45° chuẩn vs thả rơi 90° sinh bọt khí).
        /// </summary>
        public static bool LowerCoverslip(BioSample slide, float dropAngleDeg)
        {
            if (slide == null || string.IsNullOrEmpty(slide.sampleId))
            {
                BioFeedback.ShowToast("Chưa có mẫu trên lam kính để đậy lamen.", true);
                return false;
            }

            if (!slide.isSpreadFlat && slide.sampleId.Contains("hanh"))
            {
                BioFeedback.RecordMistake("lamen", "chua_dan_phang_bieu_bi",
                    "Đậy lamen khi biểu bì hành còn cuộn gấp!",
                    "Các lớp tế bào chồng lên nhau làm ảnh hiển vi bị dày và mờ, khó quan sát vách tế bào và nhân.");
            }

            slide.hasCoverslip = true;
            slide.coverslipDropAngleDeg = dropAngleDeg;
            slide.isCoverslipCracked = false;

            if (dropAngleDeg > 65f)
            {
                slide.hasAirBubbles = true;
                BioFeedback.RecordMistake("lamen", "ha_lamen_roi_thang",
                    $"Thả lamen góc {BioFeedback.FormatVi(dropAngleDeg, "0")}° quá dốc làm kẹt nhiều bọt khí!",
                    "Khi thả lamen thẳng góc 90°, không khí không kịp thoát ra mép tạo thành các vòng tròn đen dày che khuất tế bào. Phải tựa mép lamen nghiêng ~45° rồi hạ từ từ bằng kim mũi mác.");
            }
            else
            {
                slide.hasAirBubbles = false;
                BioFeedback.PlaySuccess();
                BioFeedback.LogAction("lamen", "day_lamen_45_do",
                    $"Đã hạ lamen góc {BioFeedback.FormatVi(dropAngleDeg, "0")}° chuẩn xác: tiêu bản phẳng, không có bọt khí.");
            }
            return true;
        }

        /// <summary>
        /// Dùng giấy thấm hút thuốc nhuộm thừa ở mép lamen.
        /// </summary>
        public static bool BlotSlideExcess(BioSample slide)
        {
            if (slide == null || !slide.hasCoverslip)
            {
                BioFeedback.ShowToast("Hãy đậy lamen trước khi dùng giấy thấm hút dịch thừa ở mép.", true);
                return false;
            }
            slide.hasExcessStain = false;
            slide.isBlotted = true;
            BioFeedback.PlayClick();
            BioFeedback.LogAction("giay_tham_sinh_hoc", "tham_dich_thua",
                "Đã thấm gọn thuốc nhuộm thừa ở mép lamen, đồng thời kéo thuốc nhuộm thấm đều qua mô theo lực mao dẫn.");
            return true;
        }

        /// <summary>
        /// Trường hợp 4: Lấy giống vi khuẩn bằng que cấy và ria cấy trên đĩa Petri thạch dinh dưỡng.
        /// </summary>
        public static bool PickUpInoculum(BioTool loopTool, string strainId, string strainName, bool nearAlcoholFlame)
        {
            if (loopTool == null || loopTool.kind != BioToolKind.InoculationLoop) return false;

            if (!loopTool.isSterile)
            {
                BioFeedback.RecordMistake("que_cay_vi_sinh", "que_cay_chua_khu_trung",
                    "Nhúng que cấy chưa hơ đỏ khử trùng vào ống giống gốc!",
                    "Vi khuẩn và nấm mốc ngoại lai trên que cấy sẽ làm hỏng toàn bộ ống giống gốc. Phải hơ nóng đỏ đầu que cấy trên ngọn lửa đèn cồn đến 800 °C trước.");
                return false;
            }

            if (loopTool.IsTooHotForSample)
            {
                BioFeedback.RecordMistake("que_cay_vi_sinh", "que_cay_qua_nong_giet_khuan",
                    $"Đầu que cấy đang ở {BioFeedback.FormatVi(loopTool.temperatureC, "0")} °C (> 45 °C) làm chết vi khuẩn giống!",
                    "Nhiệt độ cao làm biến tính tức thời protein và màng tế bào vi khuẩn. Sau khi hơ đỏ, phải chờ que cấy nguội xuống dưới 40 °C cạnh đèn cồn hoặc chạm nhẹ mép thạch trống trước khi lấy giống.");
                loopTool.reagentOrSampleId = "";
                loopTool.reagentDisplayName = "Vi khuẩn đã chết nhiệt";
                return false;
            }

            if (!nearAlcoholFlame)
            {
                BioFeedback.RecordMistake("den_con_sinh_hoc", "lay_giong_xa_vung_vo_trung",
                    "Mở ống giống ngoài vùng đối lưu vô trùng của đèn cồn!",
                    "Ngọn lửa đèn cồn tạo luồng khí nóng bốc lên đẩy bụi và bào tử vi sinh vật trong bán kính ~15–20 cm. Thao tác xa đèn cồn dễ bị tạp nhiễm từ không khí.");
            }

            loopTool.reagentOrSampleId = strainId;
            loopTool.reagentDisplayName = strainName;
            BioFeedback.PlaySuccess();
            BioFeedback.LogAction("que_cay_vi_sinh", "lay_giong_vi_khuan", $"Đã lấy sinh khối '{strainName}' vào vòng que cấy vô trùng.");
            return true;
        }

        public static bool StreakPetriZone(BioTool loopTool, BioSample petri, bool nearAlcoholFlame)
        {
            if (petri == null || loopTool == null || loopTool.kind != BioToolKind.InoculationLoop) return false;

            if (!loopTool.isSterile)
            {
                petri.isContaminated = true;
                petri.contaminationReason = "Que cấy chưa khử trùng";
                BioFeedback.RecordMistake("dia_petri_thach_dinh_duong", "ria_cay_que_ban",
                    "Ria cấy bằng que cấy chưa khử trùng làm đĩa thạch bị tạp nhiễm!",
                    "Bào tử nấm mốc và tạp khuẩn trên que cấy sẽ mọc lấn át chủng cần phân lập.");
                return false;
            }

            if (string.IsNullOrEmpty(loopTool.reagentOrSampleId) && petri.streakZonesCompleted == 0)
            {
                BioFeedback.ShowToast("Que cấy chưa có giống vi sinh vật. Hãy lấy giống từ lọ khuẩn mẫu trước.", true);
                return false;
            }

            if (!nearAlcoholFlame)
            {
                petri.isContaminated = true;
                petri.contaminationReason = "Ria cấy xa ngọn lửa đèn cồn";
                BioFeedback.RecordMistake("dia_petri_thach_dinh_duong", "mo_dia_xa_den_con",
                    "Mở đĩa thạch ria cấy khi chưa bật đèn cồn hoặc để quá xa đèn cồn!",
                    "Bào tử nấm mốc trong không khí rơi xuống mặt thạch dinh dưỡng gây mọc khuẩn lạc lạ.");
            }

            petri.sampleId = string.IsNullOrEmpty(petri.sampleId) ? loopTool.reagentOrSampleId : petri.sampleId;
            petri.sampleName = string.IsNullOrEmpty(loopTool.reagentDisplayName) ? "Chủng vi sinh" : loopTool.reagentDisplayName;
            petri.streakZonesCompleted = Mathf.Clamp(petri.streakZonesCompleted + 1, 1, 4);

            BioFeedback.PlaySuccess();
            BioFeedback.LogAction("dia_petri_thach_dinh_duong", $"ria_vung_{petri.streakZonesCompleted}",
                $"Đã ria cấy vùng {petri.streakZonesCompleted}/4 trên đĩa thạch ({petri.sampleName}).");
            return true;
        }

        /// <summary>
        /// Trường hợp 5: Hút / Bơm mẫu bằng Micropipette có kiểm tra đầu côn, tầm thể tích và góc nghiêng.
        /// </summary>
        public static bool MicropipetteTransfer(
            string pipetteRangeName, float minUl, float maxUl, float setUl,
            bool hasTip, ref string tipPreviousSampleId, float tiltDeg,
            BioSample source, BioSample dest)
        {
            if (!hasTip)
            {
                BioFeedback.RecordMistake("micropipet_bo_3_cay", "hut_khong_dau_con",
                    "Hút dung dịch bằng Micropipette khi CHƯA gắn đầu côn (tip)!",
                    "Dung dịch sẽ tràn trực tiếp vào trục piston kim loại bên trong thân pipet, gây ăn mòn, sai lệch thể tích và nhiễm chéo toàn bộ các mẫu sau.");
                return false;
            }

            if (setUl < minUl - 0.01f || setUl > maxUl + 0.01f)
            {
                BioFeedback.RecordMistake("micropipet_bo_3_cay", "vuot_tam_the_tich",
                    $"Đặt thể tích {BioFeedback.FormatVi(setUl, "0.0")} µL nằm ngoài dải định mức của {pipetteRangeName} ({BioFeedback.FormatVi(minUl, "0")}–{BioFeedback.FormatVi(maxUl, "0")} µL)!",
                    "Vặn quá ngưỡng làm giãn lò xo định lượng và hỏng bộ đếm cơ học của micropipet.");
                return false;
            }

            if (tiltDeg > 45f)
            {
                BioFeedback.RecordMistake("micropipet_bo_3_cay", "nghieng_pipet_qua_45_do",
                    $"Cầm nghiêng Micropipette góc {BioFeedback.FormatVi(tiltDeg, "0")}° (> 45°) khi hút dịch!",
                    "Cầm nghiêng quá 45° làm thay đổi áp suất cột chất lỏng trong đầu côn (sai số thể tích) và dễ làm dịch chảy ngược vào thân pipet.");
            }

            if (source != null && !string.IsNullOrEmpty(tipPreviousSampleId) && tipPreviousSampleId != source.sampleId)
            {
                if (dest != null)
                {
                    dest.isContaminated = true;
                    dest.contaminationReason = $"Nhiễm chéo từ {tipPreviousSampleId}";
                }
                BioFeedback.RecordMistake("micropipet_bo_3_cay", "khong_thay_dau_con",
                    $"Dùng lại đầu côn cũ (đã hút '{tipPreviousSampleId}') để hút '{source.sampleName}'!",
                    "Trong Sinh học phân tử (PCR/điện di), chỉ 0,01 µL ADN bám lại trong đầu côn cũ cũng được nhân bản hàng tỷ lần gây dương tính giả. Phải phóng bỏ đầu côn cũ và gắn đầu côn mới cho mỗi mẫu.");
            }

            if (source != null)
            {
                tipPreviousSampleId = source.sampleId;
            }

            if (source != null && dest != null)
            {
                dest.sampleId = source.sampleId;
                dest.sampleName = source.sampleName;
                dest.volumeUl += setUl;
                dest.dnaFragmentBp = source.dnaFragmentBp;
                dest.isAmplifiedByPcr = source.isAmplifiedByPcr;
                dest.SetLiquidColor(source.liquidColor);
                BioFeedback.PlayClick();
                BioFeedback.LogAction("micropipet_bo_3_cay", "chuyen_mau_ul",
                    $"Đã chuyển {BioFeedback.FormatVi(setUl, "0.0")} µL '{source.sampleName}' bằng {pipetteRangeName}.");
            }
            return true;
        }

        /// <summary>
        /// Trường hợp 6: Đánh giá màu sắc thuốc thử sinh hóa (Iốt Lugol, Benedict, Biuret) trên ống nghiệm enzyme.
        /// </summary>
        public static Color ComputeBiochemIndicatorColor(BioSample tube, string reagentId, out string reactionSummary)
        {
            reactionSummary = "Không đổi màu đặc trưng.";
            if (tube == null) return Color.clear;

            tube.indicatorId = reagentId;

            if (reagentId == "iot_lugol")
            {
                // Tinh bột còn lại: 1 - reactionProgress01 (nếu enzyme bị đun sôi isDenatured thì reactionProgress01 = 0 -> còn nguyên tinh bột)
                float remainingStarch = tube.hasSubstrate && tube.substrateId == "tinh_bot"
                    ? (tube.isDenatured ? 1f : Mathf.Clamp01(1f - tube.reactionProgress01))
                    : 0f;

                Color iodineYellow = new Color(0.86f, 0.68f, 0.18f, 0.72f);
                Color starchBlueBlack = new Color(0.12f, 0.16f, 0.52f, 0.88f);
                Color res = Color.Lerp(iodineYellow, starchBlueBlack, remainingStarch);
                tube.SetLiquidColor(res);

                if (remainingStarch > 0.7f)
                    reactionSummary = tube.isDenatured
                        ? "Màu xanh tím đậm: Amylase đã bị biến tính nhiệt nên tinh bột KHÔNG bị thủy phân."
                        : "Màu xanh tím đậm: Tinh bột chưa bị thủy phân hết (phức amylose–iốt).";
                else if (remainingStarch > 0.2f)
                    reactionSummary = "Màu tím đỏ nhạt (dextrin trung gian): Tinh bột mới thủy phân một phần.";
                else
                    reactionSummary = "Màu vàng nhạt của I₂: Amylase đã thủy phân hoàn toàn tinh bột thành đường maltose!";
                return res;
            }

            if (reagentId == "thuoc_thu_benedict")
            {
                float reducingSugar = (tube.hasSubstrate && tube.substrateId == "tinh_bot" && !tube.isDenatured)
                    ? Mathf.Clamp01(tube.reactionProgress01)
                    : 0f;
                Color benedictBlue = new Color(0.18f, 0.56f, 0.92f, 0.75f);
                Color brickRed = new Color(0.85f, 0.28f, 0.14f, 0.88f);
                Color res = Color.Lerp(benedictBlue, brickRed, reducingSugar);
                tube.SetLiquidColor(res);
                reactionSummary = reducingSugar > 0.5f
                    ? "Kết tủa đỏ gạch Cu₂O: Có đường khử (maltose/glucose) sinh ra từ thủy phân tinh bột!"
                    : "Giữ màu xanh lam Cu²⁺: Không có đường khử tự do.";
                return res;
            }

            if (reagentId == "thuoc_thu_biuret")
            {
                bool hasProtein = tube.hasEnzyme || tube.sampleId.Contains("protein") || tube.sampleId.Contains("albumin");
                Color biuretViolet = new Color(0.58f, 0.24f, 0.68f, 0.82f);
                Color paleBlue = new Color(0.45f, 0.72f, 0.92f, 0.60f);
                Color res = hasProtein ? biuretViolet : paleBlue;
                tube.SetLiquidColor(res);
                reactionSummary = hasProtein
                    ? "Màu tím hồng Biuret: Liên kết peptit (-CO-NH-) của bản chất protein enzyme tạo phức với Cu²⁺ trong môi trường kiềm."
                    : "Màu xanh nhạt: Không phát hiện liên kết peptit.";
                return res;
            }

            return tube.liquidColor;
        }

        /// <summary>
        /// Chuyển thể tích chất lỏng sinh học ml từ src sang dst theo đúng tỷ lệ thể tích (Mục 4.2.6 PROMPT_DUNG_CU_GIAO_DIEN.md).
        /// Bên trong gọi trực tiếp BioSampleData.MixInto của Backend (mục 4 hợp đồng).
        /// </summary>
        public static void Move(BioSample src, BioSample dst, double ml)
        {
            if (src == null || dst == null || ml <= 0.0) return;

            double srcVol = System.Math.Max(0.0, src.volumeMl > 0f ? src.volumeMl : src.volumeUl * 0.001);
            double dstVol = System.Math.Max(0.0, dst.volumeMl > 0f ? dst.volumeMl : dst.volumeUl * 0.001);
            double moveMl = srcVol > 0.0 ? System.Math.Min(ml, srcVol) : ml;
            double totalDstVol = dstVol + moveMl;
            if (totalDstVol <= 1e-9) return;

            double fracSrc = moveMl / totalDstVol;

            if (string.IsNullOrEmpty(dst.sampleId)) dst.sampleId = src.sampleId;
            if (dst.sampleName == "Mẫu trống" && !string.IsNullOrEmpty(src.sampleName)) dst.sampleName = src.sampleName;
            dst.isContaminated = dst.isContaminated || src.isContaminated;
            if (dst.isContaminated && string.IsNullOrEmpty(dst.contaminationReason))
                dst.contaminationReason = src.contaminationReason;

            var srcData = src.SyncToData();
            var dstData = dst.SyncToData();

            // Bản sao phần chuyển đi với lượng ADN chia theo tỷ lệ thể tích rút ra
            var movedData = new Khkt.Simulation.Bio.BioSampleData();
            movedData.CopyFrom(srcData);
            double pullRatio = srcVol > 1e-9 ? System.Math.Min(1.0, moveMl / srcVol) : 1.0;
            for (int i = 0; i < movedData.dna.Count; i++)
            {
                var b = movedData.dna[i];
                movedData.dna[i] = new Khkt.Simulation.Bio.DnaBand { bp = b.bp, ng = b.ng * pullRatio, smear = b.smear };
            }

            Khkt.Simulation.Bio.BioSampleData.MixInto(dstData, dstVol, movedData, moveMl);
            dst.SyncFromData();

            src.volumeMl = Mathf.Max(0f, (float)(srcVol - moveMl));
            src.volumeUl = src.volumeMl * 1000f;
            dst.volumeMl = (float)totalDstVol;
            dst.volumeUl = dst.volumeMl * 1000f;
            dst.SetLiquidColor(Color.Lerp(dst.liquidColor, src.liquidColor, (float)fracSrc));
            src.UpdateVisualColor();
        }
    }
}
