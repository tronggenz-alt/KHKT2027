// Kiểm thử bộ máy mô phỏng sinh học (C# thuần, không cần Unity).
// Chạy:  bash tools/chay_kiem_thu_hoa.sh tests/BioSimulationTests.cs
using System;
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

        Console.WriteLine($"\nKết quả: {passed} đạt, {failed} lỗi.");
        return failed == 0 ? 0 : 1;
    }
}
