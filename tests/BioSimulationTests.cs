// Kiểm thử bộ máy mô phỏng sinh học (C# thuần, không cần Unity).
// Chạy:  bash tools/chay_kiem_thu_hoa.sh tests/BioSimulationTests.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Khkt.Simulation.Bio;

static class BioSimulationTests
{
    static int failed, passed;

    static void Check(bool ok, string name, string detail = "")
    {
        if (ok) { passed++; Console.WriteLine("  ok   " + name); }
        else { failed++; Console.WriteLine("  SAI  " + name + "  " + detail); }
    }

    static void Near(double v, double want, double tol, string name) =>
        Check(Math.Abs(v - want) <= tol, name, $"được {v:G5}, cần {want:G5} ± {tol}");

    static int Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("=== KIỂM THỬ BỘ MÁY MÔ PHỎNG SINH HỌC (BioSimulationTests) ===");

        // 1. QUANG HỌC KÍNH HIỂN VI (Optics)
        Console.WriteLine("\n[1] Quang học kính hiển vi:");
        var (m4, na4, col4, oil4) = Optics.GetObjectiveProps(ObjectiveType.Scan4x);
        Check(m4 == 4.0 && na4 == 0.10 && col4 == "Đỏ" && !oil4, "Vật kính 4x: NA 0.10, vòng đỏ");

        var (m100, na100, col100, oil100) = Optics.GetObjectiveProps(ObjectiveType.Oil100x);
        Check(m100 == 100.0 && na100 == 1.25 && col100 == "Trắng" && oil100, "Vật kính 100x dầu: NA 1.25, vòng trắng");

        Near(Optics.TotalMagnification(10.0, 40.0), 400.0, 0.01, "Độ phóng đại 10x * 40x = 400x");
        Near(Optics.FieldOfViewMm(20.0, 10.0), 2.0, 0.01, "Thị trường FOV với FN20 và vật kính 10x = 2.0 mm");
        Near(Optics.FieldOfViewMm(20.0, 40.0), 0.5, 0.01, "Thị trường FOV với FN20 và vật kính 40x = 0.5 mm (500 µm)");

        // Độ phân giải Rayleigh: R = 0.61 * 0.55 / NA
        Near(Optics.ResolutionUm(0.25), 1.342, 0.01, "Độ phân giải vật kính 10x (NA 0.25) ≈ 1.34 µm");
        Near(Optics.ResolutionUm(1.25), 0.268, 0.01, "Độ phân giải vật kính 100x dầu (NA 1.25) ≈ 0.27 µm");

        // 2. THẨM THẤU & THẾ NƯỚC (Osmosis)
        Console.WriteLine("\n[2] Thẩm thấu & thế nước tế bào:");
        // Dung dịch sucrose 0.1 M ở 20°C: Psi_s = -1 * 0.1 * 0.008314 * 293.15 = -0.2437 MPa
        double psiS_01M = Osmosis.SolutePotentialMpa(1.0, 0.1, 20.0);
        Near(psiS_01M, -0.2437, 0.01, "Thế thẩm thấu sucrose 0.1 M ở 20°C: -0.244 MPa");

        // Tế bào thực vật no nước (Psi_w = 0 khi Psi_p = -Psi_s)
        Near(Osmosis.WaterPotentialMpa(-0.75, 0.75), 0.0, 0.001, "Tế bào no nước cân bằng: Psi_w = 0");

        // Co nguyên sinh: dung dịch ưu trương 0.6 M sucrose (Psi_w ≈ -1.46 MPa so với bên trong -0.75 MPa)
        double volFraction = Osmosis.ProtoplastVolumeFraction(-0.75, -1.46);
        Check(volFraction < 0.7 && volFraction >= 0.45, "Chất nguyên sinh co lại khi ngâm ưu trương", $"Tỷ lệ V/V0: {volFraction:F3}");

        Near(Osmosis.PlasmolysisPercentage(0.1), 0.0, 0.1, "0.1 M sucrose: 0% co nguyên sinh");
        Check(Osmosis.PlasmolysisPercentage(0.35) >= 45.0 && Osmosis.PlasmolysisPercentage(0.35) <= 55.0,
              "0.35 M sucrose: ~50% tế bào bắt đầu co nguyên sinh (incipient plasmolysis)");
        Near(Osmosis.PlasmolysisPercentage(0.8), 100.0, 0.5, "0.8 M sucrose: 100% co nguyên sinh");

        // 3. SINH TRƯỞNG QUẦN THỂ VI KHUẨN (ColonyGrowth)
        Console.WriteLine("\n[3] Sinh trưởng vi sinh vật (E. coli K-12):");
        Near(ColonyGrowth.GrowthRatePerHour(20.0), 2.079, 0.01, "Tốc độ sinh trưởng riêng E. coli: r ≈ 2.08 / giờ");

        double nStart = ColonyGrowth.PopulationAtTime(0.2); // Trong pha lag (0.5 giờ)
        Near(nStart, ColonyGrowth.DefaultInitialN0, 0.1, "Trong pha lag (0.2h): số lượng giữ nguyên N0");

        double nMid = ColonyGrowth.PopulationAtTime(4.0); // Sau 4 giờ
        Check(nMid > 1e6 && nMid < ColonyGrowth.DefaultCarryingCapacityK, "Sau 4 giờ: quần thể tăng trưởng logarit vượt 10^6");

        double nLate = ColonyGrowth.PopulationAtTime(24.0); // Sau 24 giờ
        Near(nLate, ColonyGrowth.DefaultCarryingCapacityK, 1e7, "Sau 24 giờ: quần thể đạt sức chứa môi trường K = 10^9");

        Near(ColonyGrowth.OpticalDensity600(8e8), 1.0, 0.01, "Nồng độ 8*10^8 tế bào/mL -> OD600 = 1.0");

        // 4. KHUẾCH ĐẠI DNA (PCR)
        Console.WriteLine("\n[4] Phản ứng chuỗi Polymerase (PCR):");
        // 1000 bản sao ban đầu, hiệu suất 100%, 10 chu kỳ -> 1000 * 2^10 = 1,024,000 bản sao
        Near(Pcr.AmplifiedCopies(1000.0, 10, 1.0), 1024000.0, 100.0, "PCR 10 chu kỳ hiệu suất 100%: 1.024*10^6 bản sao");

        // Sau 35 chu kỳ: đạt hiệu ứng bão hòa (plateau)
        double n35 = Pcr.AmplifiedCopies(100.0, 35, 0.95);
        Check(n35 >= 1e11 && n35 <= Pcr.PlateauLimitCopies, "Sau 35 chu kỳ: đạt trần bão hòa plateau ~10^12 copies");

        // Nhiệt độ nóng chảy mồi Tm: mồi 10-mer (3A, 2T, 3G, 2C) -> 2*(5) + 4*(5) = 30°C
        Near(Pcr.PrimerMeltingTemp(3, 2, 3, 2), 30.0, 0.1, "Tm mồi ngắn (Wallace rule): 30°C");
        Near(Pcr.AnnealingTemperature(60.0), 55.0, 0.1, "Nhiệt độ gắn mồi Ta = Tm - 5 = 55°C");

        // 5. ĐIỆN DI GEL AGAROSE (Electrophoresis)
        Console.WriteLine("\n[5] Điện di gel agarose:");
        double d100 = Electrophoresis.MigrationDistanceCm(100, 100.0, 60.0);
        double d1000 = Electrophoresis.MigrationDistanceCm(1000, 100.0, 60.0);
        double d10000 = Electrophoresis.MigrationDistanceCm(10000, 100.0, 60.0);
        Check(d100 > d1000 && d1000 > d10000, "Đoạn ngắn (100 bp) chạy xa hơn đoạn dài (1000 bp, 10000 bp)",
              $"d100={d100:F2} cm, d1000={d1000:F2} cm, d10000={d10000:F2} cm");

        // 6. QUANG HỢP ELODEA (Photosynthesis)
        Console.WriteLine("\n[6] Quang hợp thải O2 (Rong đuôi chó):");
        double iClose = Photosynthesis.LightIntensityWattsPerM2(60.0, 0.1);
        double iFar = Photosynthesis.LightIntensityWattsPerM2(60.0, 0.2);
        Near(iClose / iFar, 4.0, 0.05, "Cường độ sáng tỉ lệ nghịch bình phương khoảng cách: d gấp đôi -> I giảm 4 lần");

        Near(Photosynthesis.BubbleRatePerMinute(1.0), 0.0, 0.01, "Cường độ sáng dưới điểm bù (1.0 W/m2 <= 2.0 W/m2) -> 0 bọt/phút");
        double bpmHigh = Photosynthesis.BubbleRatePerMinute(100.0);
        Check(bpmHigh > 30.0 && bpmHigh < 45.0, "Cường độ sáng cao (100 W/m2) -> tốc độ bọt O2 đạt ~35-40 bọt/phút", $"{bpmHigh:F1} bọt/phút");

        // 7. LÊN MEN (Fermentation)
        Console.WriteLine("\n[7] Lên men rượu & lên men lactic:");
        // 18.016 g glucose (0.1 mol) lên men rượu hoàn toàn: sinh 0.2 mol CO2
        double molesCo2 = Fermentation.MolesCo2FromGlucose(18.016, 1.0);
        Near(molesCo2, 0.2, 0.001, "0.1 mol glucose -> 0.2 mol CO2");

        // Thể tích bóng bay ở 30°C: V = 0.2 * 0.0821 * 303.15 = 4.978 L
        double balloonVol = Fermentation.BalloonVolumeLiters(molesCo2, 30.0);
        Near(balloonVol, 4.978, 0.02, "Thể tích CO2 làm phồng bóng bay ở 30°C: ~4.98 L");

        double diameterCm = Fermentation.BalloonDiameterCm(balloonVol);
        Check(diameterCm > 20.0 && diameterCm < 24.0, "Đường kính bóng bay phồng: ~21.2 cm", $"{diameterCm:F1} cm");

        // 8. MẪU SINH HỌC & TRỘN MẪU (BioSampleData - B4)
        Console.WriteLine("\n[8] Trộn mẫu sinh học (BioSampleData):");
        var dstSample = new BioSampleData { organismId = "", cfuPerMl = 0, sterile = true };
        var srcSample = new BioSampleData { organismId = "e_coli_k12", cfuPerMl = 1e6, sterile = false };
        srcSample.dna.Add(new DnaBand { bp = 500, ng = 40.0, smear = false });
        BioSampleData.MixInto(dstSample, 9.0, srcSample, 1.0);
        Near(dstSample.cfuPerMl, 1e5, 1.0, "Pha loãng 1 mL mẫu 10^6 CFU/mL vào 9 mL đệm -> 10^5 CFU/mL");
        Check(dstSample.organismId == "e_coli_k12" && !dstSample.sterile, "Chủng sau pha loãng là e_coli_k12 và không còn sterile");

        var otherStrain = new BioSampleData { organismId = "b_subtilis", cfuPerMl = 2e3, sterile = false };
        otherStrain.dna.Add(new DnaBand { bp = 500, ng = 10.0, smear = true });
        otherStrain.dna.Add(new DnaBand { bp = 1000, ng = 25.0, smear = false });
        BioSampleData.MixInto(dstSample, 10.0, otherStrain, 1.0);
        Check(dstSample.organismId == "e_coli_k12" && dstSample.contaminants.Count == 1 && dstSample.contaminants[0].organismId == "b_subtilis",
              "Trộn chủng ít tế bào hơn (b_subtilis) vào chủng chính -> chuyển vào contaminants");
        var band500 = dstSample.dna.First(b => b.bp == 500);
        Check(Math.Abs(band500.ng - 50.0) < 0.01 && band500.smear && dstSample.dna.Count == 2,
              "Gộp băng ADN cùng kích thước 500 bp: cộng khối lượng ng (40+10=50) và gộp cờ smear");

        // 9. PHA LOÃNG BẬC 10 & ĐẾM KHUẨN LẠC (Dilution - B4)
        Console.WriteLine("\n[9] Pha loãng & đếm khuẩn lạc (Dilution):");
        Near(Dilution.CfuPerMl(125, 0.1, 1e-5), 1.25e8, 1e3, "125 khuẩn lạc từ 0,1 mL ở bậc 10^-5 -> 1,25*10^8 CFU/mL");
        Check(Dilution.IsCountable(30) && Dilution.IsCountable(150) && Dilution.IsCountable(300) && !Dilution.IsCountable(29) && !Dilution.IsCountable(301),
              "Đĩa đếm chuẩn ISO 4833: chỉ nhận 30..300 khuẩn lạc");
        Check(Dilution.CarryOverMl(1000.0) > Dilution.CarryOverMl(200.0) && Dilution.CarryOverMl(200.0) > 0.0,
              "Dư bám đầu côn (CarryOverMl) tăng theo cỡ đầu côn (P1000 > P200 > 0)");
        int colSeed1 = Dilution.SampleColonies(85.0, 42);
        int colSeed2 = Dilution.SampleColonies(85.0, 42);
        Check(colSeed1 == colSeed2 && colSeed1 > 40 && colSeed1 < 130 && Dilution.SampleColonies(0.0, 42) == 0,
              "SampleColonies Poisson tất định theo seed và trả 0 khi kỳ vọng = 0", $"kết quả={colSeed1}");

        // 10. CẤY RIA 4 VÙNG (StreakPlate - B4)
        Console.WriteLine("\n[10] Cấy ria 4 vùng (StreakPlate):");
        var strokesGood = new List<StreakStroke>
        {
            new StreakStroke { zone = 1, x0 = -30, y0 = 20, x1 = 30, y1 = 20, loopSterilizedBefore = true },
            new StreakStroke { zone = 2, x0 = 30, y0 = 20, x1 = 30, y1 = -20, loopSterilizedBefore = true },
            new StreakStroke { zone = 3, x0 = 30, y0 = -20, x1 = -30, y1 = -20, loopSterilizedBefore = true },
            new StreakStroke { zone = 4, x0 = -30, y0 = -20, x1 = 0, y1 = 0, loopSterilizedBefore = true },
        };
        var spotsGood = new List<ColonySpot>();
        StreakPlate.Colonies(strokesGood, 1e8, "e_coli_k12", 24.0, 37.0, 101, spotsGood);

        var strokesNoBurn = new List<StreakStroke>
        {
            new StreakStroke { zone = 1, x0 = -30, y0 = 20, x1 = 30, y1 = 20, loopSterilizedBefore = true },
            new StreakStroke { zone = 2, x0 = 30, y0 = 20, x1 = 30, y1 = -20, loopSterilizedBefore = false },
            new StreakStroke { zone = 3, x0 = 30, y0 = -20, x1 = -30, y1 = -20, loopSterilizedBefore = false },
            new StreakStroke { zone = 4, x0 = -30, y0 = -20, x1 = 0, y1 = 0, loopSterilizedBefore = false },
        };
        var spotsNoBurn = new List<ColonySpot>();
        StreakPlate.Colonies(strokesNoBurn, 1e8, "e_coli_k12", 24.0, 37.0, 101, spotsNoBurn);
        Check(spotsGood.Count > 10 && spotsNoBurn.Count > spotsGood.Count * 1.5,
              "Đốt que giữa các vùng giúp giảm mật độ tách khuẩn lạc đơn; quên đốt làm mọc dày khắp 4 vùng",
              $"đúng={spotsGood.Count}, quên đốt={spotsNoBurn.Count}");

        var spotsTooHot = new List<ColonySpot>();
        StreakPlate.Colonies(strokesGood, 1e8, "e_coli_k12", 24.0, 55.0, 101, spotsTooHot);
        var spotsHotLoop = new List<ColonySpot>();
        StreakPlate.Colonies(strokesGood, 0.0, "e_coli_k12", 24.0, 37.0, 101, spotsHotLoop);
        Check(spotsTooHot.Count == 0 && spotsHotLoop.Count == 0, "Ủ > 50 °C hoặc dí que cấy còn nóng đỏ (inoculum=0) -> không mọc khuẩn lạc");

        var spotsCold = new List<ColonySpot>();
        StreakPlate.Colonies(strokesGood, 1e8, "e_coli_k12", 24.0, 22.0, 101, spotsCold);
        Check(spotsCold.Count > 0 && spotsCold[0].diameterMm < spotsGood[0].diameterMm * 0.6f,
              "Ủ ở nhiệt độ thấp (22 °C so với tối ưu 37 °C) -> đường kính khuẩn lạc nhỏ hơn rõ rệt");

        // 11. QUY TẮC TẠP NHIỄM VÔ TRÙNG (ContaminationRules - B4)
        Console.WriteLine("\n[11] Quy tắc tạp nhiễm (ContaminationRules):");
        var ctxGoodBsc = new AsepticContext
        {
            insideBsc = true, bscRunning = true, bscPurgeMinutes = 10, sashHeightCm = 18,
            grilleBlocked = false, surfaceClean = true, handsWashed = true, ppeOn = true,
            nearFlame = false, toolSterile = true, openSeconds = 15
        };
        var ctxBad = new AsepticContext
        {
            insideBsc = false, bscRunning = false, bscPurgeMinutes = 0, sashHeightCm = 40,
            grilleBlocked = true, surfaceClean = false, handsWashed = false, ppeOn = false,
            nearFlame = false, toolSterile = false, openSeconds = 60
        };
        var ctxFlame = new AsepticContext
        {
            insideBsc = false, surfaceClean = true, handsWashed = true, ppeOn = true,
            nearFlame = true, toolSterile = true, openSeconds = 15
        };
        var ctxNoFlame = ctxFlame;
        ctxNoFlame.nearFlame = false;

        double pGood = ContaminationRules.Probability(in ctxGoodBsc);
        double pBad = ContaminationRules.Probability(in ctxBad);
        double pFlame = ContaminationRules.Probability(in ctxFlame);
        double pNoFlame = ContaminationRules.Probability(in ctxNoFlame);
        Check(pGood < 0.01 && pBad > 0.85, "Trong tủ BSC chuẩn xác suất nhiễm < 1%; thao tác bẩn + không tiệt trùng > 85%", $"pGood={pGood:F4}, pBad={pBad:F4}");
        Check(pFlame < pNoFlame * 0.3, "Thao tác gần ngọn lửa đèn cồn giảm mạnh xác suất nhiễm so với xa đèn cồn", $"gần lửa={pFlame:F4}, xa lửa={pNoFlame:F4}");
        bool rolledBad = ContaminationRules.Roll(in ctxBad, 7, out var contam);
        Check(rolledBad && (contam.organismId == "tap_bacillus_khong_khi" || contam.organismId == "tap_nam_moc") && contam.colonies >= 1,
              "Roll tạp nhiễm trả về chủng tạp nhiễm BSL-1 hợp lệ và số khuẩn lạc >= 1");

        // 12. NHUỘM GRAM (GramStain - B4)
        Console.WriteLine("\n[12] Quy trình nhuộm Gram (GramStain):");
        Check(GramStain.Result(true, 60, 60, 15, 45, true, 0.4) == GramResult.Tim, "Vi khuẩn Gram dương nhuộm đúng chuẩn -> Tím");
        Check(GramStain.Result(false, 60, 60, 15, 45, true, 0.4) == GramResult.Hong, "Vi khuẩn Gram âm nhuộm đúng chuẩn -> Hồng");
        Check(GramStain.Result(true, 60, 60, 45, 45, true, 0.4) == GramResult.Hong, "Tẩy cồn quá lâu (45s) làm Gram dương mất màu tím thành Hồng");
        Check(GramStain.Result(false, 60, 60, 2, 45, true, 0.4) == GramResult.Tim, "Tẩy cồn quá ngắn (2s) làm Gram âm giữ màu Tím giả");
        Check(GramStain.Result(true, 60, 60, 15, 45, true, 0.9) == GramResult.LoangLo, "Vết bôi quá dày (0,9) -> Loang lổ");
        Check(GramStain.Result(true, 60, 60, 15, 45, false, 0.4) == GramResult.KhongCoTeBao, "Quên cố định nhiệt -> Không có tế bào (rửa trôi)");

        // 13. ĐỘ CHÍNH XÁC MICROPIPET (PipetteAccuracy - B5)
        Console.WriteLine("\n[13] Độ chính xác Micropipet (PipetteAccuracy):");
        double ulNormal = PipetteAccuracy.ActualUl(100.0, 200.0, 1, 0.0, true);
        Near(ulNormal, 100.56, 1.0, "Hút đúng nấc 1, thẳng đứng, đầu côn ngập: ~100 µL (sai số ISO 8655 < 1%)");
        double ulStage2 = PipetteAccuracy.ActualUl(100.0, 200.0, 2, 0.0, true);
        Check(ulStage2 >= 105.0 && ulStage2 <= 120.0, "Bấm nhầm xuống nấc 2 khi hút: hút dư trong khoảng 1,05 - 1,20x", $"ulStage2={ulStage2:F1}");
        double ulAir = PipetteAccuracy.ActualUl(100.0, 200.0, 1, 0.0, false);
        double ulTilt = PipetteAccuracy.ActualUl(100.0, 200.0, 1, 45.0, true);
        Check(ulAir < 30.0 && Math.Abs(ulTilt - 100.0) > Math.Abs(ulNormal - 100.0),
              "Đầu côn không ngập hút thiếu nặng; nghiêng 45° (> 20°) làm tăng sai số");
        Check(PipetteAccuracy.ResidualFraction(1) >= 0.02 && PipetteAccuracy.ResidualFraction(1) <= 0.05 && PipetteAccuracy.ResidualFraction(2) == 0.0,
              "Nhả ở nấc 1 còn sót 2-5%; nhả hết nấc 2 (blow-out) còn sót ≈ 0");

        // 14. LY TÂM & CÂN BẰNG ROTOR (Centrifugation - B5)
        Console.WriteLine("\n[14] Máy ly tâm & cân bằng đối xứng (Centrifugation):");
        // r = 7.5 cm, 10000 rpm -> RCF = 1.118e-5 * 7.5 * 10^8 = 8385 xg
        Near(Centrifugation.Rcf(7.5, 10000.0), 8385.0, 1.0, "RCF ở r=7,5 cm, 10000 rpm = 8385 ×g");
        Near(Centrifugation.Rpm(7.5, 8385.0), 10000.0, 1.0, "Rpm ngược từ RCF=8385 ×g, r=7,5 cm = 10000 rpm");

        bool balOk = Centrifugation.IsBalanced(new List<(int, double)> { (3, 1.5), (9, 1.5) }, 12, out string reasonOk);
        bool balMissing = Centrifugation.IsBalanced(new List<(int, double)> { (3, 1.5) }, 12, out string reasonMissing);
        bool balMassDiff = Centrifugation.IsBalanced(new List<(int, double)> { (3, 1.5), (9, 2.2) }, 12, out string reasonMass);
        Check(balOk, "2 ống đồng khối lượng ở lỗ 3 và lỗ 9 trên rotor 12 lỗ -> cân bằng", reasonOk);
        Check(!balMissing && reasonMissing.Contains("lỗ 3") && reasonMissing.Contains("lỗ 9"),
              "1 ống lẻ ở lỗ 3 báo rõ thiếu ống đối diện ở lỗ 9", reasonMissing);
        Check(!balMassDiff && reasonMass.Contains("chênh lệch"), "2 ống đối diện nhưng lệch khối lượng (1,5g vs 2,2g) -> báo mất cân bằng", reasonMass);

        // 15. MỞ RỘNG PCR (Pcr.Run - B5)
        Console.WriteLine("\n[15] Chạy chương trình PCR tổng hợp (Pcr.Run):");
        var pcrStd = new PcrSetup
        {
            hasTemplate = true, hasPrimers = true, hasTaq = true, hasDntp = true, hasBuffer = true,
            lidHeated = true, productContamination = false,
            annealC = 55.0, primerTm = 60.0, enzymeWarmMinutes = 0.0, templateCopies = 1e4,
            cycles = 30, targetBp = 500
        };
        var bandsStd = new List<DnaBand>();
        Pcr.Run(in pcrStd, 1, bandsStd);
        Check(bandsStd.Count == 1 && bandsStd[0].bp == 500 && bandsStd[0].ng > 50.0,
              "PCR đủ thành phần, Ta chuẩn (Tm - 5): ra đúng 1 băng 500 bp sáng rõ");

        var pcrNoTaq = pcrStd;
        pcrNoTaq.hasTaq = false;
        var bandsNoTaq = new List<DnaBand>();
        Pcr.Run(in pcrNoTaq, 1, bandsNoTaq);
        Check(bandsNoTaq.Count == 0, "Thiếu enzyme Taq -> không có băng ADN nào");

        var pcrLowTa = pcrStd;
        pcrLowTa.annealC = 45.0; // < Tm - 10 (50)
        var bandsLowTa = new List<DnaBand>();
        Pcr.Run(in pcrLowTa, 1, bandsLowTa);
        Check(bandsLowTa.Count >= 2, "Nhiệt độ gắn mồi quá thấp (Ta < Tm - 10) -> xuất hiện thêm băng phụ không đặc hiệu");

        var pcrHighTa = pcrStd;
        pcrHighTa.annealC = 61.5; // > Tm
        var bandsHighTa = new List<DnaBand>();
        Pcr.Run(in pcrHighTa, 1, bandsHighTa);
        Check(bandsHighTa.Count == 1 && bandsHighTa[0].ng < bandsStd[0].ng * 0.3,
              "Nhiệt độ gắn mồi cao hơn Tm (61,5 °C > 60 °C) -> băng mờ rõ rệt");

        var pcrContamNeg = pcrStd;
        pcrContamNeg.hasTemplate = false;
        pcrContamNeg.productContamination = true;
        var bandsContam = new List<DnaBand>();
        Pcr.Run(in pcrContamNeg, 1, bandsContam);
        Check(bandsContam.Count == 1 && bandsContam[0].bp == 500, "Đối chứng âm bị nhiễm chéo sản phẩm (productContamination) -> vẫn lên băng 500 bp");

        // 16. MỞ RỘNG ĐIỆN DI (Electrophoresis.BandPositionCm & LeftGel - B5)
        Console.WriteLine("\n[16] Vị trí băng & tràn gel điện di (Electrophoresis):");
        double pos200 = Electrophoresis.BandPositionCm(200, 1.0, 100.0, 40.0, false);
        double pos1500 = Electrophoresis.BandPositionCm(1500, 1.0, 100.0, 40.0, false);
        double pos200Dense = Electrophoresis.BandPositionCm(200, 2.0, 100.0, 40.0, false);
        Check(pos200 > pos1500 && pos200 > pos200Dense && pos200 > 0,
              "BandPositionCm: đoạn ngắn (200 bp) chạy nhanh hơn 1500 bp và gel 1% chạy nhanh hơn gel 2%");

        double posRev = Electrophoresis.BandPositionCm(500, 1.0, 100.0, 30.0, true);
        Check(posRev < 0.0, "Cắm ngược cực điện (reversedPolarity = true) -> vị trí băng âm (chạy ngược khỏi giếng)");

        bool leftShort = Electrophoresis.LeftGel(100, 1.0, 100.0, 30.0, 8.0);
        bool leftLong = Electrophoresis.LeftGel(100, 1.0, 120.0, 120.0, 8.0);
        Check(!leftShort && leftLong, "Chạy quá lâu ở điện áp cao (120V, 120 phút) làm băng 100 bp tràn ra khỏi bản gel 8 cm (LeftGel = true)");

        // 17. THOÁT HƠI NƯỚC QUA LÁ (Transpiration - B6)
        Console.WriteLine("\n[17] Thoát hơi nước bằng áp kế bọt khí (Transpiration):");
        double trBase = Transpiration.BubbleShiftMmPerMin(0.2, 0.5, 0.5, false);
        double trWindLight = Transpiration.BubbleShiftMmPerMin(0.9, 0.9, 0.3, false);
        double trHumid = Transpiration.BubbleShiftMmPerMin(0.2, 0.5, 0.95, false);
        double trBlocked = Transpiration.BubbleShiftMmPerMin(0.9, 0.9, 0.3, true);
        Check(trWindLight > trBase * 1.8, "Gió mạnh + ánh sáng cao làm bọt khí potometer dịch chuyển nhanh hơn rõ rệt");
        Check(trHumid < trBase * 0.4, "Độ ẩm không khí bão hòa (95%) làm giảm mạnh tốc độ thoát hơi nước");
        Check(trBlocked < trBase * 0.2, "Bôi vaseline bịt kín khí khổng (stomataBlocked) làm tốc độ thoát hơi nước gần bằng 0");

        // 18. HƯỚNG SÁNG & MỌC VỐNG TRONG TỐI (Phototropism - B6)
        Console.WriteLine("\n[18] Hướng sáng & vươn dài trong tối (Phototropism):");
        double bend48 = Phototropism.BendAngleDeg(48.0, 45.0, false);
        double bend12 = Phototropism.BendAngleDeg(12.0, 45.0, false);
        double bendDark = Phototropism.BendAngleDeg(48.0, 45.0, true);
        Check(bend48 > bend12 && bend48 > 35.0 && bend48 <= 45.0, "Chiếu sáng lệch 45°: góc uốn cong hướng sáng tăng theo thời gian tiến về 45°");
        Near(bendDark, 0.0, 1e-5, "Đặt trong tối hoàn toàn (fullyDark = true): cây mọc thẳng không uốn cong (0°)");
        double etioLight = Phototropism.EtiolationFactor(48.0, false);
        double etioDark = Phototropism.EtiolationFactor(48.0, true);
        Check(Math.Abs(etioLight - 1.0) < 1e-6 && etioDark > 1.7, "Hệ số mọc vống (EtiolationFactor): = 1 ngoài sáng, > 1,7 sau 48h trong tối");

        // 19. TÁCH CHIẾT ADN (DnaExtraction - B6)
        Console.WriteLine("\n[19] Tách chiết ADN thô (DnaExtraction):");
        double dnaOpt = DnaExtraction.YieldUg(10.0, "chuoi_chin", true, true, 55.0, -20.0, 2.0);
        double dnaNoDet = DnaExtraction.YieldUg(10.0, "chuoi_chin", true, false, 55.0, -20.0, 2.0);
        double dnaNoSalt = DnaExtraction.YieldUg(10.0, "chuoi_chin", false, true, 55.0, -20.0, 2.0);
        double dnaWarmEth = DnaExtraction.YieldUg(10.0, "chuoi_chin", true, true, 55.0, 25.0, 2.0);
        Check(dnaOpt > 1500.0, "Tách chiết ADN chuối chín đủ muối + chất tẩy + ủ 55°C + cồn lạnh -20°C đạt hiệu suất cao", $"{dnaOpt:F0} µg");
        Near(dnaNoDet, 0.0, 1e-6, "Không có chất tẩy rửa phá màng tế bào -> không thu được ADN (0 µg)");
        Check(dnaNoSalt < dnaOpt * 0.15 && dnaWarmEth < dnaOpt * 0.65,
              "Thiếu muối NaCl hoặc dùng cồn ấm 25°C làm lượng ADN kết tủa giảm mạnh");

        // 20. HOẠT TÍNH CATALASE (Catalase - B6)
        Console.WriteLine("\n[20] Hệ số hoạt tính enzyme Catalase (Catalase):");
        Near(Catalase.ActivityFactor(37.0, false), 1.0, 0.01, "Catalase ở nhiệt độ tối ưu 37 °C có hệ số hoạt tính = 1,0");
        Check(Catalase.ActivityFactor(4.0, false) < 0.15 && Catalase.ActivityFactor(4.0, false) > 0.01,
              "Catalase ngâm đá lạnh 4 °C giảm mạnh hoạt tính (< 0,15)");
        Check(Catalase.ActivityFactor(37.0, true) == 0.0 && Catalase.ActivityFactor(80.0, false) == 0.0,
              "Catalase đã luộc chín hoặc ở 80 °C bị biến tính hoàn toàn (hoạt tính = 0)");

        // 21. MỞ RỘNG QUANG HỌC & LÊN MEN (Optics.Brightness & Fermentation - B6)
        Console.WriteLine("\n[21] Độ sáng kính hiển vi & động học lên men theo thời gian:");
        double brFull = Optics.Brightness(1.0, 1.0, 1.0, false);
        double brDim = Optics.Brightness(0.5, 0.3, 0.5, false);
        double brNoOil = Optics.Brightness(1.0, 1.0, 1.0, true);
        Check(Math.Abs(brFull - 1.0) < 0.01 && brDim < brFull * 0.3 && brNoOil < brFull * 0.4,
              "Optics.Brightness: tối đa = 1; giảm khi khép chắn sáng/hạ tụ quang; giảm > 60% khi vật kính 100x thiếu dầu soi");
        Near(Optics.Brightness(0.0, 1.0, 1.0, false), 0.0, 1e-6, "Tắt đèn kính hiển vi (lamp=0) -> độ sáng = 0");

        double lac8h = Fermentation.LacticAcidGramsAt(8.0, 42.0, 200.0);
        double lacCold = Fermentation.LacticAcidGramsAt(8.0, 18.0, 200.0);
        Check(lac8h > 1.2 && lacCold < lac8h * 0.3, "Ủ sữa chua 8h ở 42 °C sinh nhiều axit lactic hơn hẳn ở 18 °C", $"42°C={lac8h:F2}g, 18°C={lacCold:F2}g");

        double co2Live = Fermentation.Co2MolesAt(4.0, 32.0, 18.016, true);
        double co2Dead = Fermentation.Co2MolesAt(4.0, 32.0, 18.016, false);
        double co2Boiled = Fermentation.Co2MolesAt(4.0, 60.0, 18.016, true);
        Check(co2Live > 0.1 && co2Dead == 0.0 && co2Boiled == 0.0,
              "Lên men rượu: nấm men sống ở 32 °C sinh CO2 mạnh; nấm men chết hoặc ở 60 °C không sinh CO2");

        // 22. GHÉP GEN – ADN TÁI TỔ HỢP (DnaCloning)
        Console.WriteLine("\n[22] Ghép gen tạo ADN tái tổ hợp (DnaCloning):");
        var both = new[] { "EcoRI", "BamHI" };
        var vecFrags = DnaCloning.Digest(DnaCloning.Pu19(), both);
        var vec = DnaCloning.Largest(vecFrags);
        Check(vecFrags.Count == 2 && vec.LengthBp == 2665 && vecFrags.Sum(f => f.LengthBp) == DnaCloning.VectorLengthBp && !vec.Circular,
              "Cắt pUC19 bằng EcoRI + BamHI: 2 đoạn thẳng, khung 2665 bp, tổng bằng 2686 bp", $"{vecFrags.Count} đoạn, khung {vec.LengthBp}");
        Check(vec.HasAmpR && vec.LeftEnd == "BamHI" && vec.RightEnd == "EcoRI", "Khung giữ gen kháng ampicillin, hai đầu dính khác nhau (BamHI / EcoRI)");
        var oneCut = DnaCloning.Digest(DnaCloning.Pu19(), new[] { "EcoRI" });
        Check(oneCut.Count == 1 && oneCut[0].LengthBp == 2686 && oneCut[0].LeftEnd == "EcoRI" && oneCut[0].RightEnd == "EcoRI",
              "Cắt pUC19 bằng một enzyme EcoRI: mở vòng thành một đoạn thẳng 2686 bp, hai đầu cùng loại");
        var insFrags = DnaCloning.Digest(DnaCloning.GfpPcrProduct(), both);
        var ins = DnaCloning.Largest(insFrags);
        Check(insFrags.Count == 3 && ins.LengthBp == 726 && ins.LeftEnd == "EcoRI" && ins.RightEnd == "BamHI" && ins.InsertName == "GFP",
              "Cắt sản phẩm PCR gen GFP 738 bp bằng EcoRI + BamHI: gen 726 bp có đầu dính EcoRI / BamHI");
        Check(DnaCloning.EndsCompatible("EcoRI", "EcoRI") && !DnaCloning.EndsCompatible("EcoRI", "BamHI") && !DnaCloning.EndsCompatible("", "EcoRI"),
              "Chỉ đầu dính cùng loại mới bắt cặp; đầu PCR chưa cắt không nối được");

        var ligOk = DnaCloning.Ligate(vec, ins, true, true, 3.0, 16.0, 60.0);
        Check(ligOk.Recombinant != null && ligOk.Recombinant.LengthBp == 3391 && ligOk.Recombinant.Circular && !ligOk.Recombinant.LacZIntact,
              "Nối khung + gen bằng ligaza: plasmid tái tổ hợp vòng 3391 bp, lacZα bị gen chèn làm hỏng");
        Check(ligOk.RecombinantShare > 0.9 && ligOk.SelfLigatedShare < 0.01,
              "Cắt hai enzyme khác nhau: khung không tự đóng vòng, > 90 % plasmid vòng mang gen", $"tái tổ hợp {ligOk.RecombinantShare:P1}");
        var noLigase = DnaCloning.Ligate(vec, ins, false, true, 3.0, 16.0, 60.0);
        Check(noLigase.RecombinantShare == 0 && noLigase.CircularYield < 0.05, "Quên ADN ligaza: không có plasmid tái tổ hợp, gần như không có vòng");
        var singleVec = DnaCloning.Largest(oneCut);
        var singleIns = DnaCloning.Largest(DnaCloning.Digest(DnaCloning.GfpPcrProduct(), new[] { "EcoRI" }));
        var ligSingle = DnaCloning.Ligate(singleVec, singleIns, true, true, 3.0, 16.0, 60.0);
        Check(ligSingle.Recombinant == null && ligSingle.SelfLigatedShare > 0.9,
              "Gen chỉ cắt một đầu (đầu kia là đầu PCR): không ghép được, khung một enzyme tự đóng vòng");
        var ligUncut = DnaCloning.Ligate(DnaCloning.Pu19(), ins, true, true, 3.0, 16.0, 60.0);
        Check(ligUncut.UncutShare == 1.0 && ligUncut.Recombinant == null, "Quên cắt plasmid: chỉ có plasmid ban đầu");

        var trOk = DnaCloning.Transform(ligOk, true, 45, true, true, 7);
        Check(trOk.WhiteColonies > 150 && trOk.BlueColonies < trOk.WhiteColonies / 10 && !trOk.Lawn,
              "Biến nạp đúng (đá lạnh, sốc nhiệt 45 s, LB + Amp + X-gal): nhiều khuẩn lạc trắng, rất ít xanh", $"{trOk.WhiteColonies} trắng / {trOk.BlueColonies} xanh");
        var trSame = DnaCloning.Transform(ligOk, true, 45, true, true, 7);
        Check(trSame.WhiteColonies == trOk.WhiteColonies && trSame.BlueColonies == trOk.BlueColonies, "Cùng hạt giống → cùng số khuẩn lạc (kết quả xác định)");
        var trSelf = DnaCloning.Transform(ligSingle, true, 45, true, true, 7);
        Check(trSelf.BlueColonies > trSelf.WhiteColonies * 10, "Plasmid tự đóng vòng: hầu hết khuẩn lạc xanh");
        Check(DnaCloning.Transform(ligOk, true, 45, false, true, 7).Lawn, "Đĩa thiếu ampicillin: vi khuẩn mọc kín đĩa");
        var trNoShock = DnaCloning.Transform(ligOk, true, 0, true, true, 7);
        Check(trNoShock.WhiteColonies < trOk.WhiteColonies * 0.15, "Bỏ sốc nhiệt 42 °C: số khuẩn lạc giảm mạnh");
        var trNoLig = DnaCloning.Transform(noLigase, true, 45, true, true, 7);
        Check(trNoLig.WhiteColonies == 0, "Không nối ligaza → không có khuẩn lạc trắng");

        var check = DnaCloning.Digest(ligOk.Recombinant, both);
        Check(check.Count == 2 && check.Any(f => f.LengthBp == 726) && check.Any(f => f.LengthBp == 2665),
              "Cắt kiểm tra plasmid tái tổ hợp bằng EcoRI + BamHI: 2 băng 2665 bp và 726 bp (đúng gen ghép)");
        var bands = new List<DnaBand>();
        DnaCloning.ToBands(new List<DnaMolecule> { DnaCloning.Pu19() }, 100, bands);
        Check(bands.Count == 1 && bands[0].bp < 2686, "Plasmid vòng siêu xoắn chưa cắt chạy nhanh hơn đoạn thẳng cùng kích thước");

        // BIOMIX: kết quả chỉ phụ thuộc thành phần và nhiệt độ, không phụ thuộc thứ tự
        Console.WriteLine("\n[BioMix] Thao tác tự do:");
        var ev = new List<string>();
        BioMix Run(BioMix mm, double tC, double secs, double scale = 60)
        {
            for (double s = 0; s < secs; s += 0.2) BioRules.Step(mm, tC, 0.2, scale, ev);
            return mm;
        }
        var ab = new BioMix(); ab.Add(BioMatterDb.Liver, 1); ab.Add(BioMatterDb.H2O2, 5); Run(ab, 25, 3);
        var ba = new BioMix(); ba.Add(BioMatterDb.H2O2, 5); ba.Add(BioMatterDb.Liver, 1); Run(ba, 25, 3);
        Check(ab.o2Ml > 0.5 && Math.Abs(ab.o2Ml - ba.o2Ml) < 1e-9, "Catalase: đảo thứ tự cho kết quả như nhau");
        var cooked = new BioMix(); cooked.Add(BioMatterDb.Liver, 1); Run(cooked, 95, 1); cooked.Add(BioMatterDb.H2O2, 5); Run(cooked, 25, 3);
        Check(cooked.catalaseDead && cooked.o2Ml == 0, "Catalase: mô đã đun sôi không sinh O2, nguội lại cũng không");
        var mergeA = new BioMix(); mergeA.Add(BioMatterDb.H2O2, 5);
        var mergeB = new BioMix(); mergeB.Merge(mergeA); mergeB.Add(BioMatterDb.Liver, 1); Run(mergeB, 25, 3);
        Check(mergeB.o2Ml > 0.5, "Thêm mô sống vào bình chỉ có H2O2 vẫn phản ứng");
        var starch = new BioMix(); starch.Add(BioMatterDb.Potato, 5); starch.Add(BioMatterDb.Lugol, 0.05); Run(starch, 25, 0.4);
        Check(starch.iodineStarch, "Lugol + khoai tây: xanh tím");
        Run(starch, 80, 0.4);
        Check(!starch.iodineStarch, "Đun nóng làm mất màu xanh tím");
        Run(starch, 25, 0.4);
        Check(starch.iodineStarch, "Nguội lại màu xanh tím hiện lại");
        var plain = new BioMix(); plain.Add(BioMatterDb.Water, 5); plain.Add(BioMatterDb.Lugol, 0.05); Run(plain, 25, 0.4);
        Check(!plain.iodineStarch, "Lugol + nước: không tím");
        var yeast = new BioMix(); yeast.Add(BioMatterDb.Water, 50); yeast.Add(BioMatterDb.Glucose, 5); yeast.Add(BioMatterDb.Yeast, 1); Run(yeast, 37, 4, 1800);
        var cold = new BioMix(); cold.Add(BioMatterDb.Water, 50); cold.Add(BioMatterDb.Glucose, 5); cold.Add(BioMatterDb.Yeast, 1); Run(cold, 4, 4, 1800);
        var dead = new BioMix(); dead.Add(BioMatterDb.Water, 50); dead.Add(BioMatterDb.Glucose, 5); dead.Add(BioMatterDb.Yeast, 1); Run(dead, 90, 0.4); Run(dead, 37, 4, 1800);
        Check(yeast.co2Ml > cold.co2Ml && dead.co2Ml == 0, "Lên men: 37 °C nhanh hơn 4 °C, men đun chết không sinh CO2", $"{yeast.co2Ml:0} / {cold.co2Ml:0} / {dead.co2Ml:0}");
        var yogurt = new BioMix(); yogurt.Add(BioMatterDb.Milk, 90); yogurt.Add(BioMatterDb.YogurtStarter, 10); Run(yogurt, 42, 15, 1800);
        var hotMilk = new BioMix(); hotMilk.Add(BioMatterDb.Milk, 90); hotMilk.Add(BioMatterDb.YogurtStarter, 10); Run(hotMilk, 70, 15, 1800);
        Check(yogurt.yogurt >= 1 && BioRules.Ph(yogurt) < 5 && hotMilk.yogurt < 0.1, "Sữa chua: 42 °C đông, pH giảm; 70 °C không đông");
        var dna = new BioMix(); dna.Add(BioMatterDb.Banana, 5); dna.ground = true; dna.Add(BioMatterDb.Water, 10); dna.Add(BioMatterDb.Soap, 1); dna.Add(BioMatterDb.Nacl10, 1); Run(dna, 25, 15); dna.Add(BioMatterDb.ColdEthanol, 10); Run(dna, 5, 0.4);
        var noSoap = new BioMix(); noSoap.Add(BioMatterDb.Banana, 5); noSoap.ground = true; noSoap.Add(BioMatterDb.Water, 10); noSoap.Add(BioMatterDb.ColdEthanol, 10); Run(noSoap, 5, 15);
        Check(dna.dnaThreads > 0.5 && noSoap.dnaThreads == 0, "Tách ADN: đủ xà phòng + muối mới có sợi ADN");
        var bact = new BioMix(); bact.Add(BioMatterDb.Broth, 10); bact.Add(BioMatterDb.EColi, 0.001); Run(bact, 37, 6, 1800);
        var bactCold = new BioMix(); bactCold.Add(BioMatterDb.Broth, 10); bactCold.Add(BioMatterDb.EColi, 0.001); Run(bactCold, 4, 6, 1800);
        Check(bact.od > 0.3 && bactCold.od < 0.1, "Vi khuẩn: 37 °C đục, 4 °C gần như không lớn", $"{bact.od:0.00} / {bactCold.od:0.00}");
        var agar = new BioMix(); agar.Add(BioMatterDb.Agarose, 1); agar.Add(BioMatterDb.Tae, 100); Run(agar, 25, 0.4);
        Check(!agar.agaroseMelted, "Agarose chưa đun thì chưa tan");
        Run(agar, 95, 0.4); Run(agar, 30, 0.4);
        Check(agar.gelSet, "Đun tan rồi nguội thành gel");
        var part = new BioMix(); part.Add(BioMatterDb.Water, 10); part.Add(BioMatterDb.Potato, 5);
        var liquidOnly = part.TakeLiquid(1);
        Check(liquidOnly.Of(BioMatterDb.Potato) == 0 && Math.Abs(liquidOnly.Total - 1) < 1e-9, "Ống nhỏ giọt không hút được mô");

        Console.WriteLine($"\nKết quả: {passed} đạt, {failed} lỗi.");
        return failed == 0 ? 0 : 1;
    }
}
