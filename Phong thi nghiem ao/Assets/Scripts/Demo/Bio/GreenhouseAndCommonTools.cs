// -----------------------------------------------------------------------------
// GreenhouseAndCommonTools.cs — Thiết bị Nhà kính thực vật (5.5), Hành lang tầng 2
// mới 3,6 m (5.6) và Dụng cụ dùng chung toàn tầng 2 (5.7).
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine;
using Khkt.Interaction;
using Khkt.Interaction.Bio;
using Khkt.Simulation.Bio;

namespace Khkt.Demo.Bio
{
    /// <summary>
    /// 5.5.1. Trạm quang hợp rong đuôi chó (Elodea canadensis) — mã: tram_quang_hop
    /// Gọi trực tiếp Photosynthesis.LightIntensityAt, Photosynthesis.RelativeRate và Photosynthesis.BubblesPerMinute.
    /// </summary>
    [DisallowMultipleComponent]
    public class PhotosynthesisRig : MonoBehaviour, IInteractable, IScrollAdjustable
    {
        public bool lampOn = true;
        public double lampDistanceCm = 15.0; // Thanh trượt 10..50 cm
        public string lightColorFilter = "trang"; // trang | do | xanh_lam | xanh_luc (xanh lục ít bọt nhất!)
        public bool addedNaHco3 = true;           // Cung cấp CO2 hòa tan
        public double waterTempC = 26.0;
        public double collectedO2Ml = 0.0;

        public double ColorFilterMultiplier =>
            lightColorFilter == "xanh_luc" ? 0.15 :
            (lightColorFilter == "do" ? 0.92 :
            (lightColorFilter == "xanh_lam" ? 0.85 : 1.0));

        public double EffectiveIntensity => lampOn
            ? Photosynthesis.LightIntensityWattsPerM2(40.0 * ColorFilterMultiplier, lampDistanceCm / 100.0) * (addedNaHco3 ? 1.0 : 0.12)
            : 0.0;
        public double EffectiveLux => EffectiveIntensity;

        public double CurrentBubblesPerMin => Photosynthesis.BubbleRatePerMinute(EffectiveIntensity);

        public string UseHint => $"Trạm quang hợp Rong đuôi chó [Đèn: {(lampOn ? $"{BioFeedback.FormatVi((float)lampDistanceCm, "0")} cm · Lọc: {lightColorFilter}" : "TẮT")} · NaHCO₃: {(addedNaHco3 ? "Có (đủ CO₂)" : "Thiếu CO₂!")} → {BioFeedback.FormatVi((float)CurrentBubblesPerMin, "0.1")} bọt O₂/phút · Đã thu {BioFeedback.FormatVi((float)collectedO2Ml, "0.1")} mL O₂] · Bấm: đếm bọt & thu khí · Con lăn: kéo đèn 10–50 cm";

        public void SetConfiguration(double distCm, string colorFilter, bool hasBicarbonate, double tempC = 26.0)
        {
            lampOn = true;
            lampDistanceCm = Math.Clamp(distCm, 10.0, 50.0);
            lightColorFilter = colorFilter;
            addedNaHco3 = hasBicarbonate;
            waterTempC = tempC;

            if (!hasBicarbonate)
            {
                BioFeedback.RecordMistake("tram_quang_hop", "quang_hop_nuoc_cat_thieu_nahco3",
                    "Dùng nước cất đun sôi để nguội mà quên bổ sung NaHCO₃ 0,2 % (nguồn cung cấp CO₂)!",
                    "Khi thiếu CO₂ cho chu trình Calvin, pha sáng cũng bị ức chế nên cành rong đuôi chó hầu như ngừng nhả bọt khí O₂.");
            }
            else if (colorFilter == "xanh_luc")
            {
                BioFeedback.LogAction("tram_quang_hop", "kinh_loc_xanh_luc",
                    $"Kính lọc ánh sáng XANH LỤC (520–560 nm): diệp lục phản xạ ánh sáng xanh lục nên tốc độ nhả bọt giảm mạnh chỉ còn {BioFeedback.FormatVi((float)CurrentBubblesPerMin, "0.1")} bọt/phút.");
            }
        }

        public double RecordOneMinuteCount()
        {
            double bpm = CurrentBubblesPerMin;
            collectedO2Ml += bpm * 0.04;
            BioFeedback.PlayBubblePop();
            BioFeedback.LogAction("tram_quang_hop", "dem_bot_quang_hop",
                $"Ở khoảng cách đèn d = {BioFeedback.FormatVi((float)lampDistanceCm, "0")} cm (I = {BioFeedback.FormatVi((float)EffectiveLux, "0")} lux, lọc {lightColorFilter}): đếm được {BioFeedback.FormatVi((float)bpm, "0.1")} bọt O₂/phút (đã thu {BioFeedback.FormatVi((float)collectedO2Ml, "0.1")} mL O₂).");
            return bpm;
        }

        public void Use(GameObject user)
        {
            RecordOneMinuteCount();
        }

        public void OnScroll(float delta)
        {
            if (Mathf.Abs(delta) < 0.001f) return;
            lampDistanceCm = Math.Clamp(lampDistanceCm + Math.Sign(delta) * 5.0, 10.0, 50.0);
            RecordOneMinuteCount();
        }
    }

    /// <summary>
    /// 5.5.1 & Bài 4 Catalase. Que đóm thử khí Oxy (O₂) — mã: que_dom
    /// Gọi trực tiếp Catalase.ActivityFactor và kiểm chứng khí O₂ từ quang hợp / phân hủy H₂O₂.
    /// </summary>
    [DisallowMultipleComponent]
    public class GlowingSplint : MonoBehaviour, IInteractable
    {
        public bool hasGlowingEmber = true;
        public bool burstIntoFlame = false;

        public string UseHint => burstIntoFlame
            ? "Que đóm thử khí [BÙNG CHÁY SÁNG RỰC — Xác nhận khí sinh ra là Oxy (O₂)!]"
            : "Que đóm còn tàn đỏ · Đưa vào miệng ống nghiệm thu khí quang hợp hoặc ống nghiệm Catalase + H₂O₂ để thử O₂";

        /// <summary>
        /// Thử phản ứng Catalase (khoai tây sống/luộc hoặc gan gà + H₂O₂ 3%) và kiểm tra bằng que đóm tàn đỏ.
        /// </summary>
        public bool TestCatalaseTube(string sampleTitle, double tempC, bool isBoiledSample)
        {
            hasGlowingEmber = true;
            double act = Catalase.ActivityFactor(tempC, isBoiledSample);
            if (isBoiledSample || act <= 0.01)
            {
                burstIntoFlame = false;
                BioFeedback.RecordMistake("que_dom", "thu_catalase_mau_da_luoc_chin_bien_tinh",
                    $"Nhỏ H₂O₂ 3 % vào '{sampleTitle}' (đã luộc chín ở 100 °C): enzyme catalase đã biến tính nhiệt (hoạt tính = {BioFeedback.FormatVi((float)act, "0.00")}) nên KHÔNG sủi bọt O₂, que đóm không bùng cháy!",
                    "Catalase có bản chất là protein, bị phá vỡ cấu trúc không gian bậc 3–4 khi đun sôi (> 65 °C).");
                return false;
            }

            burstIntoFlame = true;
            BioFeedback.PlaySuccess();
            BioFeedback.LogAction("que_dom", "que_dom_bung_chay_o2",
                $"Phản ứng Catalase ('{sampleTitle}' ở {BioFeedback.FormatVi((float)tempC, "0")} °C, hoạt tính = {BioFeedback.FormatVi((float)act, "0.00")}): 2 H₂O₂ → 2 H₂O + O₂↑ sủi bọt trắng xóa, đưa que đóm tàn đỏ vào BÙNG CHÁY SÁNG RỰC!");
            return true;
        }

        public void Use(GameObject user)
        {
            TestCatalaseTube("Gan gà / Khoai tây tươi sống", 37.0, false);
        }
    }

    /// <summary>
    /// 5.5.2. Trạm đo thoát hơi nước bằng áp kế bọt khí (Potometer) — mã: tram_potometer
    /// Gọi trực tiếp Transpiration.BubbleShiftMmPerMin của Backend.
    /// </summary>
    [DisallowMultipleComponent]
    public class PotometerRig : MonoBehaviour, IInteractable
    {
        public double wind01 = 0.0;
        public double light01 = 0.7;
        public double humidity01 = 0.45;
        public bool stomataBlockedWithVaseline = false;
        public bool stemCutInAirEmbolismMistake = false;
        public double bubblePositionMm = 10.0;

        public double CurrentSpeedMmPerMin => stemCutInAirEmbolismMistake
            ? 0.0
            : Transpiration.BubbleShiftMmPerMin(wind01, light01, humidity01, stomataBlockedWithVaseline);

        public string UseHint => $"Áp kế bọt khí Potometer [Gió: {BioFeedback.FormatVi((float)(wind01 * 100), "0")}% · Sáng: {BioFeedback.FormatVi((float)(light01 * 100), "0")}% · Ẩm: {BioFeedback.FormatVi((float)(humidity01 * 100), "0")}% · Khí khổng: {(stomataBlockedWithVaseline ? "Bịt Vaseline" : "Thông thoáng")} → Tốc độ bọt = {BioFeedback.FormatVi((float)CurrentSpeedMmPerMin, "0.2")} mm/phút] · Bấm: đổi điều kiện gió/sáng/ẩm";

        public void SetCutInAirEmbolismMistake()
        {
            stemCutInAirEmbolismMistake = true;
            BioFeedback.RecordMistake("tram_potometer", "cat_canh_cay_ngoai_khong_khi_tac_mach_go",
                "Cắt gốc cành cây ngoài không khí rồi mới cắm vào ống Potometer!",
                "Bọt khí lọt vào mạch gỗ (xylem embolism) làm đứt đoạn cột nước liên tục nên bọt khí chỉ thị đứng yên không dịch chuyển. Phải cắt vát gốc cành ngập hoàn toàn dưới mặt nước.");
        }

        public double MeasureCondition(string modeName, double w01, double l01, double h01, bool vaseline)
        {
            stemCutInAirEmbolismMistake = false;
            wind01 = w01;
            light01 = l01;
            humidity01 = h01;
            stomataBlockedWithVaseline = vaseline;
            double speed = CurrentSpeedMmPerMin;
            bubblePositionMm += speed * 5.0;
            BioFeedback.PlayClick();
            BioFeedback.LogAction("tram_potometer", "do_thoat_hoi_nuoc",
                $"Potometer [{modeName}]: tốc độ dịch chuyển bọt khí = {BioFeedback.FormatVi((float)speed, "0.2")} mm/phút (vạch hiện tại: {BioFeedback.FormatVi((float)bubblePositionMm, "0.1")} mm).");
            return speed;
        }

        public void Use(GameObject user)
        {
            if (wind01 < 0.5)
                MeasureCondition("Bật quạt gió + Đèn sáng", 0.85, 0.9, 0.35, false);
            else if (!stomataBlockedWithVaseline)
                MeasureCondition("Bôi Vaseline mặt dưới lá (bịt khí khổng)", 0.2, 0.7, 0.45, true);
            else
                MeasureCondition("Điều kiện đối chứng chuẩn", 0.0, 0.7, 0.45, false);
        }
    }

    /// <summary>
    /// 5.5.3. Hộp thí nghiệm tính hướng sáng dương (Phototropism Box) — mã: hop_huong_sang
    /// Gọi trực tiếp Phototropism.BendAngleDeg và Phototropism.EtiolationFactor của Backend.
    /// </summary>
    [DisallowMultipleComponent]
    public class PhototropismBox : MonoBehaviour, IInteractable
    {
        public double lightAzimuthDeg = 45.0; // Cửa sổ sáng bên phải (+45°), bên trái (-45°), trên (0°)
        public bool fullyDark = false;
        public double elapsedHours = 24.0;
        public Transform seedlingStemVisual;

        public double CurrentBendDeg => Phototropism.BendAngleDeg(elapsedHours, lightAzimuthDeg, fullyDark);
        public double CurrentEtiolation => Phototropism.EtiolationFactor(elapsedHours, fullyDark);

        public string UseHint => $"Hộp hướng sáng thực vật [Cửa sáng: {(fullyDark ? "ĐÓNG KÍN HOÀN TOÀN (Tối)" : $"Lệch {BioFeedback.FormatVi((float)lightAzimuthDeg, "0")}°")} · {BioFeedback.FormatVi((float)elapsedHours, "0")} giờ → Góc uốn thân mầm = {BioFeedback.FormatVi((float)CurrentBendDeg, "0.1")}° · Vươn dài = ×{BioFeedback.FormatVi((float)CurrentEtiolation, "0.00")}] · Bấm: tua 24 giờ / đổi cửa sáng";

        public void RunExperiment(double hours, double windowAngleDeg, bool darkBoxMistake = false)
        {
            elapsedHours = hours;
            lightAzimuthDeg = windowAngleDeg;
            fullyDark = darkBoxMistake;

            if (seedlingStemVisual != null)
            {
                seedlingStemVisual.localRotation = Quaternion.Euler(0f, 0f, -(float)CurrentBendDeg);
                seedlingStemVisual.localScale = new Vector3(1f, (float)CurrentEtiolation, 1f);
            }

            if (darkBoxMistake)
            {
                BioFeedback.RecordMistake("hop_huong_sang", "dong_kin_moi_cua_so_cay_moc_vong_vang_ua",
                    $"Đóng kín toàn bộ cửa sổ ánh sáng suốt {BioFeedback.FormatVi((float)hours, "0")} giờ!",
                    $"Auxin phân bố đều nên thân mầm không uốn cong (0°), cây bị hiện tượng mọc vống vàng úa (etiolation ×{BioFeedback.FormatVi((float)CurrentEtiolation, "0.00")}): thân vươn dài yếu ớt, lá màu vàng nhạt do không tổng hợp được diệp lục.");
            }
            else
            {
                BioFeedback.PlaySuccess();
                BioFeedback.LogAction("hop_huong_sang", "uon_cong_huong_sang_duong",
                    $"Sau {BioFeedback.FormatVi((float)hours, "0")} giờ chiếu sáng từ cửa sổ góc {BioFeedback.FormatVi((float)windowAngleDeg, "0")}°: auxin tích tụ phía khuất sáng kích thích tế bào dãn dài nhanh hơn, làm ngọn mầm đậu uốn cong {BioFeedback.FormatVi((float)CurrentBendDeg, "0.1")}° về phía nguồn sáng!");
            }
        }

        public void Use(GameObject user)
        {
            RunExperiment(elapsedHours + 12.0, 55.0, false);
        }
    }

    /// <summary>
    /// 5.5.4. Giàn thủy canh dòng chảy màng dinh dưỡng NFT 3 tầng — mã: gian_thuy_canh_nft
    /// </summary>
    [DisallowMultipleComponent]
    public class NftRackController : MonoBehaviour, IInteractable
    {
        public bool pumpRunning = true;
        public bool growLedOn = true;
        public float ecMsCm = 1.8f; // Chuẩn 1,5 – 2,2 mS/cm
        public float phValue = 6.0f; // Chuẩn 5,8 – 6,5
        public bool plantsWilted = false;

        public string UseHint => $"Giàn thủy canh NFT 3 tầng [Bơm: {(pumpRunning ? "CHẠY" : "TẮT!")} · Đèn LED R/B: {(growLedOn ? "BẬT" : "TẮT")} · EC = {BioFeedback.FormatVi(ecMsCm, "0.00")} mS/cm · pH = {BioFeedback.FormatVi(phValue, "0.00")}{(plantsWilted ? " · CÂY HÉO!" : " · Cây xanh tốt")}] · Bấm: kiểm tra & châm dinh dưỡng A+B";

        public void SetToxicHighEcMistake(float highEc = 5.2f)
        {
            ecMsCm = highEc;
            plantsWilted = true;
            BioFeedback.RecordMistake("gian_thuy_canh_nft", "pha_dinh_duong_ec_qua_cao_chay_re",
                $"Pha dung dịch thủy canh quá đặc (EC = {BioFeedback.FormatVi(ecMsCm, "0.1")} mS/cm > 3,5 mS/cm) làm cây héo rũ!",
                "Nồng độ muối khoáng ưu trương làm thế nước bên ngoài thấp hơn trong tế bào lông hút, khiến rễ bị rút nước ngược (co nguyên sinh / cháy rễ).");
        }

        public void Use(GameObject user)
        {
            pumpRunning = true;
            growLedOn = true;
            ecMsCm = 1.85f;
            phValue = 6.05f;
            plantsWilted = false;
            BioFeedback.PlaySuccess();
            BioFeedback.LogAction("gian_thuy_canh_nft", "van_hanh_thuy_canh_nft",
                $"Giàn thủy canh NFT hoạt động tối ưu: Bơm màng dinh dưỡng bật, EC = {BioFeedback.FormatVi(ecMsCm, "0.00")} mS/cm, pH = {BioFeedback.FormatVi(phValue, "0.00")}.");
        }
    }

    /// <summary>
    /// 5.5.5. Bình tưới cây vòi hoa sen — mã: binh_tuoi
    /// </summary>
    [DisallowMultipleComponent]
    public class WateringCan : MonoBehaviour, IInteractable
    {
        public float waterLiters = 3.5f;
        public float soilMoisturePct = 68.0f;

        public string UseHint => $"Bình tưới cây vòi sen ({BioFeedback.FormatVi(waterLiters, "0.1")} L) [Độ ẩm giá thể: {BioFeedback.FormatVi(soilMoisturePct, "0")}%] · Bấm: tưới ẩm đều mặt chậu";

        public void Use(GameObject user)
        {
            waterLiters = Mathf.Max(0f, waterLiters - 0.3f);
            soilMoisturePct = Mathf.Min(85f, soilMoisturePct + 12f);
            BioFeedback.PlayBubblePop();
            BioFeedback.LogAction("binh_tuoi", "tuoi_am_chau_cay",
                $"Đã dùng bình tưới vòi hoa sen tưới ẩm chậu cây: độ ẩm đất đạt {BioFeedback.FormatVi(soilMoisturePct, "0")}%.");
        }
    }

    /// <summary>
    /// 5.5.5. Trạm cảm biến vi khí hậu nhà kính (Nhiệt độ, Độ ẩm, Cường độ sáng Lux) — mã: cam_bien_vi_khi_hau
    /// </summary>
    [DisallowMultipleComponent]
    public class ClimateSensor : MonoBehaviour, IInteractable
    {
        public float airTempC = 26.4f;
        public float relativeHumidityPct = 72.0f;
        public float lightLux = 18500.0f;

        public string UseHint => $"Trạm cảm biến vi khí hậu nhà kính [T = {BioFeedback.FormatVi(airTempC, "0.1")} °C · RH = {BioFeedback.FormatVi(relativeHumidityPct, "0")}% · Ánh sáng = {BioFeedback.FormatVi(lightLux, "0")} lux] · Bấm: đọc & đồng bộ dữ liệu lên web";

        public void Use(GameObject user)
        {
            BioFeedback.PlayClick();
            BioFeedback.LogAction("cam_bien_vi_khi_hau", "doc_vi_khi_hau_nha_kinh",
                $"Cảm biến vi khí hậu nhà kính: Nhiệt độ = {BioFeedback.FormatVi(airTempC, "0.1")} °C, Độ ẩm = {BioFeedback.FormatVi(relativeHumidityPct, "0")}%, Cường độ sáng = {BioFeedback.FormatVi(lightLux, "0")} lux.");
        }
    }

    /// <summary>
    /// 5.5.5. Hệ thống phun sương làm mát nhà kính (≤ 150 hạt sương) — mã: he_phun_suong
    /// </summary>
    [DisallowMultipleComponent]
    public class MistSystem : MonoBehaviour, IInteractable
    {
        public bool isMisting = false;
        public int activeParticleBudget = 120; // <= 150 hạt theo mục 5.5.5

        public string UseHint => $"Hệ thống phun sương cao áp nhà kính [{(isMisting ? $"ĐANG PHUN ({activeParticleBudget} hạt sương)" : "CHỜ CHU KỲ 90 s")}] · Bấm: bật/tắt phun sương tức thì";

        public void Use(GameObject user)
        {
            isMisting = !isMisting;
            BioFeedback.PlayClick();
            BioFeedback.LogAction("he_phun_suong", isMisting ? "bat_phun_suong" : "tat_phun_suong",
                $"Hệ thống phun sương nhà kính: {(isMisting ? "Đã kích hoạt phun sương hạ nhiệt & giữ ẩm lá" : "Đã ngắt van phun sương")}.");
        }
    }

    /// <summary>
    /// 5.5.5. Bộ dụng cụ sang chậu trồng cây (3 bước: cho đất -> đặt cây -> tưới định gốc) — mã: bo_sang_chau
    /// </summary>
    [DisallowMultipleComponent]
    public class PottingKit : MonoBehaviour, IInteractable
    {
        public int step = 0; // 0..3
        static readonly string[] StepNames =
        {
            "Bước 1/3: Xúc giá thể đất tơi xốp + xơ dừa vào 2/3 chậu",
            "Bước 2/3: Đặt bầu cây con thẳng đứng giữa chậu và lấp đất vừa ngang cổ rễ",
            "Bước 3/3: Tưới nước nhẹ nhàng định gốc"
        };

        public string UseHint => step >= 3
            ? "Bộ dụng cụ sang chậu trồng cây [Đã hoàn tất sang chậu 3 bước — cây con đứng vững]"
            : $"Bàn sang chậu thực vật [{StepNames[step]}] · Bấm để thực hiện";

        public void Use(GameObject user)
        {
            if (step >= 3) step = 0;
            string msg = StepNames[step];
            step++;
            BioFeedback.PlaySuccess();
            BioFeedback.LogAction("bo_sang_chau", $"sang_chau_buoc_{step}", $"Thao tác sang chậu: {msg}.");
        }
    }

    // =========================================================================
    // 5.6 & 5.7. HÀNH LANG TẦNG 2 (NỚI RỘNG 3,6 M) & DỤNG CỤ DÙNG CHUNG
    // =========================================================================

    /// <summary>
    /// 5.6. Màn hình bảng tin hành lang tầng 2 (Lịch phòng hôm nay, Cảnh báo tủ lạnh & sự cố) — mã: man_hinh_bang_tin
    /// </summary>
    [DisallowMultipleComponent]
    public class CorridorBoard : MonoBehaviour, IInteractable
    {
        public int pageIndex = 0;
        static readonly string[] PageTitles =
        {
            "TRANG 1/3: NỘI QUY AN TOÀN SINH HỌC CẤP 1 (BSL-1)",
            "TRANG 2/3: LỊCH ĐẶT PHÒNG & THIẾT BỊ HÔM NAY (Đồng bộ Web /sinh)",
            "TRANG 3/3: TRẠNG THÁI NHIỆT ĐỘ TỦ (4,0 °C / −20,0 °C) & SỰ CỐ AN TOÀN"
        };
        public TextMesh boardTextMesh;

        public string UseHint => $"Màn hình bảng tin hành lang tầng 2 [{PageTitles[pageIndex]}] · Bấm để lật trang";

        public void Use(GameObject user)
        {
            pageIndex = (pageIndex + 1) % PageTitles.Length;
            if (boardTextMesh != null) boardTextMesh.text = PageTitles[pageIndex];
            BioFeedback.PlayClick();
            BioFeedback.LogAction("man_hinh_bang_tin", "lat_trang_bang_tin", $"Bảng tin hành lang tầng 2: {PageTitles[pageIndex]}.");
        }
    }

    /// <summary>
    /// 5.6. Ki-ốt tra cứu dụng cụ sinh học & dẫn đường bằng vạch sáng trên sàn hành lang 3,6 m — mã: kiosk_tra_cuu
    /// </summary>
    [DisallowMultipleComponent]
    public class ToolKiosk : MonoBehaviour, IInteractable
    {
        public int currentToolIdx = 0;
        public bool floorGuideActive = false;
        public float guideTimer = 0f;
        public GameObject floorGuideLineVisual;

        public BioToolCatalogEntry SelectedTool
        {
            get
            {
                BioCatalogLoader.EnsureLoaded();
                if (BioCatalogLoader.ToolsList.Count == 0) return null;
                return BioCatalogLoader.ToolsList[currentToolIdx % BioCatalogLoader.ToolsList.Count];
            }
        }

        public string UseHint
        {
            get
            {
                var t = SelectedTool;
                string info = t != null ? $"{t.ten} ({t.ma} · Phòng: {t.phong})" : "108 dụng cụ sinh học";
                return $"Ki-ốt tra cứu dụng cụ [{info}{(floorGuideActive ? " · ĐANG BẬT VẠCH SÁNG DẪN ĐƯỜNG" : "")}] · Bấm: tra cứu & bật mũi tên dẫn đường trên sàn";
            }
        }

        public void ActivateFloorWayfinding(string toolId)
        {
            floorGuideActive = true;
            guideTimer = 0f;
            if (floorGuideLineVisual != null) floorGuideLineVisual.SetActive(true);
            enabled = true;
            var t = BioCatalogLoader.GetTool(toolId) ?? SelectedTool;
            BioFeedback.PlaySuccess();
            BioFeedback.LogAction("kiosk_tra_cuu", "dan_duong_dung_cu",
                $"Ki-ốt tra cứu: Hiển thị thông số '{t?.ten}' ({t?.ma}) và bật vạch sáng dẫn đường trên sàn hành lang 3,6 m tới khu '{t?.phong}' (tự tắt sau 20 s).");
        }

        public void Use(GameObject user)
        {
            BioCatalogLoader.EnsureLoaded();
            if (BioCatalogLoader.ToolsList.Count > 0)
            {
                var t = SelectedTool;
                ActivateFloorWayfinding(t.ma);
                currentToolIdx = (currentToolIdx + 1) % BioCatalogLoader.ToolsList.Count;
            }
        }

        void Update()
        {
            if (!floorGuideActive) { enabled = false; return; }
            guideTimer += Time.deltaTime;
            if (guideTimer >= 20f)
            {
                floorGuideActive = false;
                if (floorGuideLineVisual != null) floorGuideLineVisual.SetActive(false);
                enabled = false;
            }
        }
    }

    /// <summary>
    /// 5.6. Tủ trưng bày mẫu vật hành lang (3 ngăn kính) — mã: tu_trung_bay
    /// </summary>
    [DisallowMultipleComponent]
    public class DisplayCabinet : MonoBehaviour, IInteractable
    {
        public bool glassDoorOpen = false;
        public int shelfIndex = 0;
        static readonly string[] ShelfTopics =
        {
            "Ngăn 1: Bộ sưu tập Tiến hóa & Hóa thạch Sinh giới",
            "Ngăn 2: Mô hình cấu trúc Siêu hiển vi Tế bào & Bào quan (Lục lạp, Ti thể)",
            "Ngăn 3: Mẫu tiêu bản Đa dạng Sinh học Việt Nam"
        };
        public BioHinge doorHinge;

        public string UseHint => $"Tủ kính trưng bày hành lang 3,6 m [{ShelfTopics[shelfIndex]} · Cửa kính: {(glassDoorOpen ? "MỞ" : "ĐÓNG")}] · Bấm: mở cửa kính & xem từng ngăn";

        public void Use(GameObject user)
        {
            glassDoorOpen = !glassDoorOpen;
            if (glassDoorOpen) shelfIndex = (shelfIndex + 1) % ShelfTopics.Length;
            if (doorHinge != null) doorHinge.SetOpen(glassDoorOpen);
            BioFeedback.PlayClick();
            BioFeedback.LogAction("tu_trung_bay", "xem_tu_trung_bay", $"Tủ trưng bày hành lang: {ShelfTopics[shelfIndex]}.");
        }
    }

    /// <summary>
    /// 5.6. Tủ đồ cá nhân học sinh (cất balô, lấy áo blouse & găng tay trước khi vào phòng phân tử) — mã: tu_do_ca_nhan
    /// </summary>
    [DisallowMultipleComponent]
    public class Locker : MonoBehaviour, IInteractable
    {
        public bool isOpen = false;
        public BioHinge doorHinge;

        public string UseHint => $"Tủ đồ cá nhân (Locker hành lang) [Cửa: {(isOpen ? "MỞ" : "ĐÓNG")} · Đồ bảo hộ PPE: {(BioTransfer.PpeEquipped ? "Đã mặc" : "Chưa mặc")}] · Bấm: cất đồ cá nhân & mặc áo blouse, găng tay";

        public void Use(GameObject user)
        {
            isOpen = !isOpen;
            BioTransfer.PpeEquipped = true;
            if (doorHinge != null) doorHinge.SetOpen(isOpen);
            BioFeedback.PlaySuccess();
            BioFeedback.LogAction("tu_do_ca_nhan", "cat_do_mac_ppe_tu_locker",
                "Đã mở tủ đồ cá nhân cất tư trang và mặc áo blouse trắng + găng tay nitrile (daMacBaoHo = true).");
        }
    }

    /// <summary>
    /// 5.7. Đồng hồ bấm giờ để bàn 3 kênh — mã: dong_ho_bam_gio
    /// </summary>
    [DisallowMultipleComponent]
    public class BenchTimer : MonoBehaviour, IInteractable
    {
        public bool isRunning = false;
        public float elapsedSeconds = 0f;

        public string UseHint => $"Đồng hồ bấm giờ để bàn [{(isRunning ? "ĐANG ĐẾM" : "DỪNG")}: {BioFeedback.FormatVi(elapsedSeconds, "0.0")} s] · Bấm: bắt đầu / dừng / đặt lại";

        public void Use(GameObject user)
        {
            if (!isRunning && elapsedSeconds == 0f)
            {
                isRunning = true;
                elapsedSeconds = 60.0f;
            }
            else if (isRunning)
            {
                isRunning = false;
            }
            else
            {
                elapsedSeconds = 0f;
            }
            BioFeedback.PlayClick();
            BioFeedback.LogAction("dong_ho_bam_gio", "bam_gio_thi_nghiem",
                $"Đồng hồ bấm giờ: {(isRunning ? "Đã bấm giờ 60,0 s" : $"Dừng ở {BioFeedback.FormatVi(elapsedSeconds, "0.0")} s")}.");
        }
    }

    /// <summary>
    /// 5.7. Bút lông dầu kháng cồn & nhãn dán — mã: but_ghi_nhan
    /// </summary>
    [DisallowMultipleComponent]
    public class LabelPen : MonoBehaviour, IInteractable
    {
        public string currentLabel = "E. coli K-12 · 10⁻⁵ · 31/10";
        public bool labeledOnLidMistake = false;

        public string UseHint => $"Bút lông dầu kháng cồn & Nhãn dán [Nhãn: '{currentLabel}'] · Ghi lên ĐÁY đĩa Petri / THÂN ống nghiệm (không ghi lên nắp rời!)";

        public void WriteLabel(bool writeOnRemovableLidMistake = false)
        {
            labeledOnLidMistake = writeOnRemovableLidMistake;
            if (writeOnRemovableLidMistake)
            {
                BioFeedback.RecordMistake("but_ghi_nhan", "ghi_nhan_len_nap_dia_petri",
                    "Ghi tên mẫu lên NẮP đĩa Petri thay vì ghi ở ĐÁY đĩa thạch!",
                    "Khi mở nắp hoặc nếu nắp bị rơi/tráo đổi giữa các đĩa, sẽ không thể biết đáy thạch chứa mẫu nào. Quy tắc bắt buộc: luôn ghi nhãn ở mép ĐÁY đĩa Petri và THÂN ống nghiệm.");
                return;
            }
            BioFeedback.PlayClick();
            BioFeedback.LogAction("but_ghi_nhan", "ghi_nhan_dung_quy_cach",
                $"Đã dùng bút lông kháng cồn ghi nhãn '{currentLabel}' vào đáy đĩa Petri / thân ống nghiệm.");
        }

        public void Use(GameObject user)
        {
            WriteLabel(false);
        }
    }

    /// <summary>
    /// 5.7. Nhiệt kế thủy ngân / rượu / điện tử (−10..110 °C) — mã: nhiet_ke
    /// </summary>
    [DisallowMultipleComponent]
    public class Thermometer : MonoBehaviour, IInteractable
    {
        public float currentReadingC = 25.0f;

        public string UseHint => $"Nhiệt kế phòng thí nghiệm (−10 .. 110 °C) [Số đọc hiện tại: {BioFeedback.FormatVi(currentReadingC, "0.1")} °C] · Bấm: nhúng bầu nhiệt kế đọc nhiệt độ dung dịch";

        public float MeasureTemperature(float containerTempC, bool touchedHotBottomMistake = false)
        {
            currentReadingC = touchedHotBottomMistake ? containerTempC + 14.0f : containerTempC;
            if (touchedHotBottomMistake)
            {
                BioFeedback.RecordMistake("nhiet_ke", "cham_bau_nhiet_ke_vao_day_coc_dang_dun",
                    "Để bầu nhiệt kế chạm sát đáy cốc thủy tinh đang đun nóng!",
                    "Đáy cốc tiếp xúc trực tiếp với mặt bếp có nhiệt độ cao hơn lòng chất lỏng làm số đọc bị vọt sai lệch.");
            }
            else
            {
                BioFeedback.PlayClick();
                BioFeedback.LogAction("nhiet_ke", "do_nhiet_do_dung_dich",
                    $"Nhúng bầu nhiệt kế giữa lòng chất lỏng: đọc được T = {BioFeedback.FormatVi(currentReadingC, "0.1")} °C.");
            }
            return currentReadingC;
        }

        public void Use(GameObject user)
        {
            MeasureTemperature(37.0f, false);
        }
    }
}

