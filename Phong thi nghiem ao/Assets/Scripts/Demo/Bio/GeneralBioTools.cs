// -----------------------------------------------------------------------------
// GeneralBioTools.cs — Các dụng cụ và thiết bị phòng thí nghiệm Sinh học chung
// (Mục 5.1.7 – 5.1.11, 5.1.15 – 5.1.21 PROMPT_DUNG_CU_GIAO_DIEN.md).
// -----------------------------------------------------------------------------
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
    /// 5.1.7. Đĩa đồng hồ thủy tinh lõm Ø 75 mm — mã: dia_dong_ho
    /// </summary>
    [DisallowMultipleComponent]
    public class WatchGlass : MonoBehaviour, IInteractable
    {
        public BioSample sample;
        public float soakMolarity = 0.5f; // Dung dịch muối ngâm biểu bì

        void Awake()
        {
            sample = GetComponent<BioSample>();
            if (sample == null) sample = gameObject.AddComponent<BioSample>();
        }

        public string UseHint => sample != null && !sample.IsEmpty
            ? $"Đĩa đồng hồ Ø 75 mm [Đang ngâm: {sample.sampleName} trong NaCl {BioFeedback.FormatVi(soakMolarity, "0.0")} M]"
            : "Đĩa đồng hồ thủy tinh Ø 75 mm · Bấm: cho biểu bì thài lài tía vào ngâm dung dịch muối";

        public void Use(GameObject user)
        {
            if (sample == null) return;
            sample.sampleId = "bieu_bi_thai_lai_tia";
            sample.sampleName = "Biểu bì lá thài lài tía";
            sample.volumeMl = 2.0f;
            BioFeedback.PlayClick();
            BioFeedback.LogAction("dia_dong_ho", "ngam_mau_dia_dong_ho",
                $"Đã ngâm mẫu '{sample.sampleName}' trong đĩa đồng hồ chứa dung dịch NaCl {BioFeedback.FormatVi(soakMolarity, "0.0")} M.");
        }
    }

    /// <summary>
    /// 5.1.7 & 5.4.4. Giá ống nghiệm / Giá ống Eppendorf / Giá ống PCR — mã: gia_ong_nghiem, gia_ong_eppendorf, gia_ong_pcr
    /// </summary>
    [DisallowMultipleComponent]
    public class TubeRackSockets : MonoBehaviour, IInteractable
    {
        public string rackToolId = "gia_ong_nghiem";
        public string acceptedTubeType = "ong_nghiem"; // ong_nghiem | ong_eppendorf | ong_pcr
        public int slotCount = 12;
        public int occupiedCount = 4;

        public string UseHint => $"Giá đỡ ống ({rackToolId}) [{occupiedCount}/{slotCount} lỗ đang cắm ống {acceptedTubeType}] · Đưa ống đúng cỡ tới gần (≤ 8 cm) để hít vào ổ";

        public bool TrySnapTube(string tubeType, out string rejectReason)
        {
            if (!string.IsNullOrEmpty(acceptedTubeType) && tubeType != acceptedTubeType)
            {
                rejectReason = $"Giá này chỉ nhận {acceptedTubeType} (không vừa {tubeType})!";
                BioFeedback.ShowToast(rejectReason, true);
                return false;
            }
            if (occupiedCount >= slotCount)
            {
                rejectReason = "Giá ống đã đầy!";
                return false;
            }
            occupiedCount++;
            rejectReason = "";
            BioFeedback.PlayClick();
            BioFeedback.LogAction(rackToolId, "cam_ong_vao_gia", $"Đã đặt {tubeType} vào lỗ số {occupiedCount}/{slotCount} trên {rackToolId}.");
            return true;
        }

        public void Use(GameObject user)
        {
            BioFeedback.PlayClick();
            BioFeedback.LogAction(rackToolId, "kiem_tra_gia_ong", $"Kiểm tra {rackToolId}: {occupiedCount}/{slotCount} ống đang đặt ngay ngắn.");
        }
    }

    /// <summary>
    /// 5.1.7. Đũa thủy tinh (khuấy dung dịch và quấn sợi ADN kết tủa) — mã: dua_thuy_tinh
    /// </summary>
    [DisallowMultipleComponent]
    public class GlassRod : MonoBehaviour, IInteractable
    {
        public bool hasSpoolDna = false;
        public float spooledDnaUg = 0f;

        public string UseHint => hasSpoolDna
            ? $"Đũa thủy tinh [Đã quấn kết tủa sợi ADN trắng đục: ~{BioFeedback.FormatVi(spooledDnaUg, "0.0")} µg]"
            : "Đũa thủy tinh · Bấm để khuấy nhẹ hoặc xoay tròn ở mặt phân cách nước – cồn lạnh để quấn sợi ADN";

        public void SpoolDna(bool ethanolCold, bool hasSalt, double sampleGrams = 10.0, string sampleKind = "chuoi")
        {
            double ethTempC = ethanolCold ? -20.0 : 25.0;
            spooledDnaUg = (float)DnaExtraction.YieldUg(sampleGrams, sampleKind, hasSalt, true, 60.0, ethTempC, 2.0);
            if (!ethanolCold || !hasSalt)
            {
                hasSpoolDna = spooledDnaUg > 15f;
                BioFeedback.RecordMistake("dua_thuy_tinh", "ket_tua_adn_con_khong_lanh",
                    !ethanolCold ? $"Dùng cồn ở nhiệt độ phòng (25 °C) nên hiệu suất thu hồi ADN chỉ đạt {BioFeedback.FormatVi(spooledDnaUg, "0.1")} µg!" : "Thiếu muối NaCl trung hòa điện tích âm của gốc photphat nên ADN hầu như không kết tủa!",
                    "Để tách chiết ADN thành sợi trắng nổi rõ ở ranh giới pha, phải có Na⁺ trung hòa khung đường–photphat và cồn 90–96 % thật lạnh (−20 °C).");
                return;
            }
            hasSpoolDna = true;
            BioFeedback.PlaySuccess();
            BioFeedback.LogAction("dua_thuy_tinh", "quan_soi_adn",
                $"Đã xoay đũa thủy tinh tại mặt phân cách cồn lạnh (−20 °C) – dịch lọc, thu được {BioFeedback.FormatVi(spooledDnaUg, "0.1")} µg búi sợi ADN trắng đục!");
        }

        public void Use(GameObject user)
        {
            SpoolDna(true, true);
        }
    }

    /// <summary>
    /// 5.1.8. Đèn bàn LED (tối đa 2 đèn bật cùng lúc trong phòng để tiết kiệm điện & giữ FPS) — mã: den_ban_led
    /// </summary>
    [DisallowMultipleComponent]
    public class DeskLamp : MonoBehaviour, IInteractable
    {
        private static readonly List<DeskLamp> ActiveLamps = new List<DeskLamp>();
        public const int MaxSimultaneousLamps = 2;

        public bool isOn = false;
        public float armAngleDeg = 35f;
        public Light pointLight;

        public string UseHint => $"Đèn bàn LED [{(isOn ? "ĐANG BẬT" : "TẮT")} · góc cần {BioFeedback.FormatVi(armAngleDeg, "0")}°] · Bấm: bật/tắt (tối đa 2 đèn cùng lúc)";

        public void Use(GameObject user)
        {
            SetLamp(!isOn);
        }

        public void SetLamp(bool on)
        {
            if (on && !isOn)
            {
                ActiveLamps.RemoveAll(l => l == null || !l.isOn);
                if (ActiveLamps.Count >= MaxSimultaneousLamps)
                {
                    var oldest = ActiveLamps[0];
                    ActiveLamps.RemoveAt(0);
                    if (oldest != null)
                    {
                        oldest.isOn = false;
                        if (oldest.pointLight != null) oldest.pointLight.enabled = false;
                        BioFeedback.ShowToast("Tắt mờ đèn bàn xa nhất để tiết kiệm điện (tối đa 2 đèn bàn bật cùng lúc).");
                    }
                }
                ActiveLamps.Add(this);
            }
            else if (!on)
            {
                ActiveLamps.Remove(this);
            }

            isOn = on;
            if (pointLight != null) pointLight.enabled = isOn;
            BioFeedback.PlayClick();
            BioFeedback.LogAction("den_ban_led", isOn ? "bat_den_ban" : "tat_den_ban",
                $"Đèn bàn LED: {(isOn ? "BẬT" : "TẮT")}.");
        }
    }

    /// <summary>
    /// 5.1.8. Vòi nước bàn thí nghiệm (tự ngắt sau 20 s) — mã: voi_nuoc_ban
    /// </summary>
    [DisallowMultipleComponent]
    public class BenchTap : MonoBehaviour, IInteractable
    {
        public bool isRunning = false;
        public float autoShutoffTimer = 0f;
        public Transform waterStreamVisual;

        public string UseHint => isRunning
            ? $"Vòi nước bàn [ĐANG CHẢY — tự khóa sau {BioFeedback.FormatVi(20f - autoShutoffTimer, "0")} s] · Bấm: khóa vòi / hứng nước vào cốc"
            : "Vòi nước bàn thí nghiệm · Bấm để mở vòi nước (tự ngắt sau 20 s)";

        public void Use(GameObject user)
        {
            isRunning = !isRunning;
            autoShutoffTimer = 0f;
            if (waterStreamVisual != null) waterStreamVisual.gameObject.SetActive(isRunning);
            enabled = isRunning;
            BioFeedback.PlayClick();
            BioFeedback.LogAction("voi_nuoc_ban", isRunning ? "mo_voi_nuoc" : "khoa_voi_nuoc",
                $"Vòi nước bàn: {(isRunning ? "Mở dòng chảy" : "Đã khóa")}.");
        }

        void Update()
        {
            if (!isRunning) { enabled = false; return; }
            autoShutoffTimer += Time.deltaTime;
            if (autoShutoffTimer >= 20f)
            {
                isRunning = false;
                if (waterStreamVisual != null) waterStreamVisual.gameObject.SetActive(false);
                enabled = false;
            }
        }
    }

    /// <summary>
    /// 5.1.9. Màn hình bàn học sinh (BioBenchScreenController) — mã: man_hinh_ban
    /// Đọc dữ liệu từ data/sinh/bai_thi_nghiem.json, lật bước và đánh dấu hoàn thành.
    /// </summary>
    [DisallowMultipleComponent]
    public class BioBenchScreenController : MonoBehaviour, IInteractable
    {
        public string currentExperimentId = "te_bao_hanh";
        public int currentStepIndex = 0;
        public int completedStepsMask = 0;
        public string lastMeasurementSummary = "Chưa có số đo";
        public TextMesh screenTextMesh;

        void Start()
        {
            SetExperiment(currentExperimentId);
        }

        public BioExperimentEntry CurrentExperiment => BioCatalogLoader.GetExperiment(currentExperimentId);

        public string UseHint
        {
            get
            {
                var exp = CurrentExperiment;
                if (exp == null) return $"Màn hình bàn ({currentExperimentId}) · Bấm để chuyển bước";
                int total = exp.cac_buoc != null ? exp.cac_buoc.Length : 1;
                string stepDesc = (exp.cac_buoc != null && exp.cac_buoc.Length > 0)
                    ? exp.cac_buoc[currentStepIndex % exp.cac_buoc.Length]
                    : exp.muc_tieu;
                return $"Màn hình bàn [{exp.ten} · Bước {currentStepIndex + 1}/{total}: {stepDesc}] · Bấm: sang bước tiếp";
            }
        }

        public void SetExperiment(string expId)
        {
            BioCatalogLoader.EnsureLoaded();
            currentExperimentId = expId;
            currentStepIndex = 0;
            completedStepsMask = 0;
            UpdateVisualText();
        }

        public void MarkCurrentStepComplete(string measurementNote = null)
        {
            completedStepsMask |= (1 << currentStepIndex);
            if (!string.IsNullOrEmpty(measurementNote)) lastMeasurementSummary = measurementNote;
            var exp = CurrentExperiment;
            int total = (exp != null && exp.cac_buoc != null && exp.cac_buoc.Length > 0) ? exp.cac_buoc.Length : 1;
            currentStepIndex = (currentStepIndex + 1) % total;
            UpdateVisualText();
        }

        public void Use(GameObject user)
        {
            MarkCurrentStepComplete();
            BioFeedback.PlayClick();
            var exp = CurrentExperiment;
            BioFeedback.LogAction("man_hinh_ban", "lat_buoc_thi_nghiem",
                $"Màn hình bàn ({currentExperimentId}): chuyển sang bước {currentStepIndex + 1}/{(exp?.cac_buoc?.Length ?? 1)}.");
        }

        void UpdateVisualText()
        {
            if (screenTextMesh == null) return;
            var exp = CurrentExperiment;
            if (exp == null)
            {
                screenTextMesh.text = $"BÀI: {currentExperimentId}\nBước {currentStepIndex + 1}";
                return;
            }
            int total = exp.cac_buoc != null ? exp.cac_buoc.Length : 1;
            string stepStr = (exp.cac_buoc != null && exp.cac_buoc.Length > 0)
                ? exp.cac_buoc[currentStepIndex % exp.cac_buoc.Length]
                : exp.muc_tieu;
            screenTextMesh.text = $"LỚP {exp.lop}: {exp.ten}\nBước {currentStepIndex + 1}/{total}: {stepStr}\nKết quả: {lastMeasurementSummary}";
        }
    }

    /// <summary>
    /// 5.1.11. Chày cối sứ Ø 9 cm — mã: coi_chay_su
    /// </summary>
    [DisallowMultipleComponent]
    public class MortarPestle : MonoBehaviour, IInteractable
    {
        public string rawSampleName = "Chuối chín / Gan gà";
        public int grindStrokes = 0;
        public const int RequiredStrokes = 5;
        public bool hasExtractionBuffer = false;
        public bool isHomogenized => grindStrokes >= RequiredStrokes;
        public BioSample homogenateSample;

        void Awake()
        {
            homogenateSample = GetComponent<BioSample>();
            if (homogenateSample == null) homogenateSample = gameObject.AddComponent<BioSample>();
        }

        public string UseHint => isHomogenized
            ? $"Cối chày sứ [Đã nghiền nhuyễn '{rawSampleName}' ({grindStrokes}/{RequiredStrokes} vòng){(hasExtractionBuffer ? " + đệm tách chiết" : "")}] · Rót sang phễu lọc"
            : $"Cối chày sứ Ø 9 cm [Đang nghiền '{rawSampleName}': {grindStrokes}/{RequiredStrokes} vòng] · Bấm/giữ để nghiền mịn phá vỡ thành tế bào";

        public void AddExtractionBuffer()
        {
            hasExtractionBuffer = true;
            if (homogenateSample != null)
            {
                homogenateSample.sampleId = "dich_nghien_adn";
                homogenateSample.sampleName = $"Dịch nghiền {rawSampleName} + đệm ly giải";
                homogenateSample.volumeMl = 15f;
                homogenateSample.dna.Clear();
                homogenateSample.dna.Add(new BioDnaBandInfo { bp = 10000, ng = 120.0, smear = false });
                homogenateSample.SyncToData();
            }
            BioFeedback.LogAction("coi_chay_su", "them_dem_tach_chiet", "Đã thêm dung dịch tách chiết (nước rửa chén + NaCl) vào cối nghiền.");
        }

        public void Use(GameObject user)
        {
            grindStrokes = Mathf.Min(RequiredStrokes, grindStrokes + 2);
            if (!hasExtractionBuffer && grindStrokes >= RequiredStrokes)
            {
                AddExtractionBuffer();
            }
            BioFeedback.PlayClick();
            BioFeedback.LogAction("coi_chay_su", "nghien_mau_coi_su",
                $"Nghiền mẫu '{rawSampleName}' trong cối sứ: {grindStrokes}/{RequiredStrokes} vòng ({(isHomogenized ? "Đã thành dịch nghiền mịn" : "Đang nghiền")}).");
        }
    }

    /// <summary>
    /// 5.1.11. Phễu lọc + giấy lọc gấp — mã: pheu_loc
    /// </summary>
    [DisallowMultipleComponent]
    public class FilterFunnel : MonoBehaviour, IInteractable
    {
        public bool hasFilterPaper = true;
        public bool isFiltered = false;
        public float filtrateVolumeMl = 0f;
        public BioSample filtrateSample;

        void Awake()
        {
            filtrateSample = GetComponent<BioSample>();
            if (filtrateSample == null) filtrateSample = gameObject.AddComponent<BioSample>();
        }

        public string UseHint => isFiltered
            ? $"Phễu lọc + giấy lọc [Đã thu {BioFeedback.FormatVi(filtrateVolumeMl, "0.0")} mL dịch lọc trong suốt, bã giữ trên giấy]"
            : "Phễu lọc + giấy lọc · Bấm để rót dịch nghiền từ cối sứ qua phễu lọc thu dịch trong vào ống nghiệm";

        public void Use(GameObject user)
        {
            if (!hasFilterPaper)
            {
                BioFeedback.RecordMistake("pheu_loc", "loc_quyen_giay_loc",
                    "Rót dịch nghiền qua phễu khi chưa đặt giấy lọc gấp!",
                    "Toàn bộ bã mô thực vật/động vật rơi thẳng xuống ống nghiệm làm đục mẫu, không thể kết tủa ADN sạch.");
                return;
            }
            isFiltered = true;
            filtrateVolumeMl = 8.5f;
            if (filtrateSample != null)
            {
                filtrateSample.sampleId = "dich_loc_adn";
                filtrateSample.sampleName = "Dịch lọc tế bào chứa ADN";
                filtrateSample.volumeMl = filtrateVolumeMl;
                filtrateSample.dna.Clear();
                filtrateSample.dna.Add(new BioDnaBandInfo { bp = 10000, ng = 95.0, smear = false });
                filtrateSample.SyncToData();
            }
            BioFeedback.PlayBubblePop();
            BioFeedback.LogAction("pheu_loc", "loc_dich_nghien",
                $"Đã lọc dịch nghiền qua giấy lọc: thu được {BioFeedback.FormatVi(filtrateVolumeMl, "0.0")} mL dịch lọc trong suốt vào ống nghiệm.");
        }
    }

    /// <summary>
    /// 5.1.15 & 5.2.1. Tủ an toàn sinh học cấp II loại A2 — mã: tu_an_toan_sinh_hoc
    /// </summary>
    [DisallowMultipleComponent]
    public class BiosafetyCabinetController : MonoBehaviour, IInteractable, IScrollAdjustable
    {
        public bool fanOn = false;
        public bool lightOn = true;
        public bool uvOn = false;
        public float sashHeightCm = 20.0f; // Chuẩn làm việc: 20 cm
        public float purgeMinutes = 0f;    // Cần chạy quạt >= 5 phút trước khi thao tác
        public bool grilleBlocked = false;
        public bool surfaceCleanedWithEthanol = false;
        public bool showAirflowParticles = false;

        public float downflowVelocityMs => fanOn ? 0.35f : 0f;
        public float inflowVelocityMs => fanOn ? 0.52f : 0f;
        public BscProgram program = new BscProgram();

        void Awake()
        {
            if (program == null) program = new BscProgram();
            program.cabinet = this;
        }

        public string UseHint
        {
            get
            {
                string sashState = sashHeightCm > 20.5f ? "CẢNH BÁO KÍNH CAO > 20 cm!" : (sashHeightCm <= 0.5f ? "Kính đóng kín" : "Đúng vạch 20 cm");
                return $"Tủ an toàn sinh học cấp II A2 [Quạt: {(fanOn ? $"BẬT ({BioFeedback.FormatVi(purgeMinutes, "0.0")} phút)" : "TẮT")} · Kính: {BioFeedback.FormatVi(sashHeightCm, "0")} cm ({sashState}) · UV: {(uvOn ? "BẬT" : "TẮT")}] · Bấm: bật quạt & chuẩn bị vô trùng · Con lăn: kéo kính";
            }
        }

        public void SetSashHeight(float cm)
        {
            sashHeightCm = Mathf.Clamp(cm, 0f, 45f);
            if (sashHeightCm > 0.5f && uvOn)
            {
                uvOn = false;
                BioFeedback.ShowToast("Khóa liên động: Mở kính cửa trước -> Đèn UV tự động tắt để bảo vệ mắt và da!");
                BioFeedback.LogAction("tu_an_toan_sinh_hoc", "khoa_lien_dong_uv", "Mở kính trước khi UV đang bật -> Hệ thống khóa liên động tự tắt đèn UV.");
            }
            if (sashHeightCm > 20.5f && fanOn)
            {
                BioFeedback.RecordMistake("tu_an_toan_sinh_hoc", "mo_kinh_bsc_qua_vach_20cm",
                    $"Kéo kính tủ BSC lên {BioFeedback.FormatVi(sashHeightCm, "0")} cm vượt quá vạch an toàn 20 cm!",
                    "Khi mở kính cao quá 20 cm, vận tốc màn khí hút vào lưới trước giảm dưới 0,5 m/s làm rò rỉ khí và tăng mạnh nguy cơ tạp nhiễm.");
            }
            SyncGlobalAsepticFlags();
        }

        public void ToggleUv(bool turnOn)
        {
            if (turnOn && sashHeightCm > 0.5f)
            {
                BioFeedback.RecordMistake("tu_an_toan_sinh_hoc", "bat_uv_khi_kinh_mo",
                    "Cố bật đèn UV diệt khuẩn khi kính trước chưa đóng kín!",
                    "Tia UV-C (254 nm) gây bỏng giác mạc và tổn thương da. Chỉ bật UV khi đã đóng kín kính chắn.");
                return;
            }
            uvOn = turnOn;
            BioFeedback.PlayClick();
            BioFeedback.LogAction("tu_an_toan_sinh_hoc", uvOn ? "bat_uv_bsc" : "tat_uv_bsc", $"Đèn UV tủ BSC: {(uvOn ? "BẬT khử trùng" : "TẮT")}.");
        }

        public void WipeSurfaceWithEthanol()
        {
            surfaceCleanedWithEthanol = true;
            BioTransfer.SurfaceClean = true;
            BioFeedback.PlaySuccess();
            BioFeedback.LogAction("tu_an_toan_sinh_hoc", "lau_con_70_mat_ban", "Đã xịt cồn 70 % và lau sạch mặt bàn inox trong tủ an toàn sinh học.");
            SyncGlobalAsepticFlags();
        }

        public void CleanSurfaceWithEthanol() => WipeSurfaceWithEthanol();

        public void FastForwardPurge(float minutes)
        {
            if (!fanOn) fanOn = true;
            purgeMinutes += minutes;
            SyncGlobalAsepticFlags();
            BioFeedback.LogAction("tu_an_toan_sinh_hoc", "tua_thoi_gian_quat_bsc",
                $"Đã chạy quạt HEPA tủ BSC được {BioFeedback.FormatVi(purgeMinutes, "0.0")} phút (Dòng xuống {BioFeedback.FormatVi(downflowVelocityMs, "0.00")} m/s, Dòng vào {BioFeedback.FormatVi(inflowVelocityMs, "0.00")} m/s).");
        }

        public void Use(GameObject user)
        {
            fanOn = true;
            uvOn = false;
            sashHeightCm = 20.0f;
            if (purgeMinutes < 5.0f) purgeMinutes = 5.0f;
            surfaceCleanedWithEthanol = true;
            grilleBlocked = false;
            SyncGlobalAsepticFlags();
            BioFeedback.PlaySuccess();
            BioFeedback.LogAction("tu_an_toan_sinh_hoc", "chuan_bi_bsc_chuan",
                "Tủ BSC cấp II A2 sẵn sàng: Quạt HEPA chạy đủ 5 phút (dòng xuống 0,35 m/s, dòng vào 0,52 m/s), kính ở vạch 20 cm, mặt bàn đã lau cồn 70 %.");
        }

        public void OnScroll(float delta)
        {
            if (Mathf.Abs(delta) < 0.001f) return;
            SetSashHeight(sashHeightCm + Mathf.Sign(delta) * 2.5f);
        }

        public void SyncGlobalAsepticFlags()
        {
            BioTransfer.InsideBsc = fanOn;
            BioTransfer.BscRunning = fanOn;
            BioTransfer.BscPurgeMinutes = purgeMinutes;
            BioTransfer.BscSashHeightCm = sashHeightCm;
            BioTransfer.BscGrilleBlocked = grilleBlocked;
            BioTransfer.SurfaceClean = surfaceCleanedWithEthanol;
        }
    }

    /// <summary>
    /// Màn hình vận hành Tủ an toàn sinh học cấp II A2 (5.2.1) — kế thừa InstrumentProgram.
    /// </summary>
    public class BscProgram : InstrumentProgram
    {
        public override string Code => "tu_an_toan_sinh_hoc";
        public BiosafetyCabinetController cabinet;

        public override void Build()
        {
            if (Doc == null || Doc.Steps.Count == 0)
                Doc = BioCatalogLoader.LoadMachineDoc(Code);

            S.AddButton("Quạt HEPA (Bật/Tắt)", 0, 0, () =>
            {
                if (cabinet != null)
                {
                    cabinet.fanOn = !cabinet.fanOn;
                    cabinet.SyncGlobalAsepticFlags();
                }
                S.Done("chay_quat");
            });

            S.AddButton("Kính trước: 20 cm", 1, 0, () =>
            {
                if (cabinet != null) cabinet.SetSashHeight(20f);
                S.Done("ha_kinh");
            });

            S.AddButton("Lau cồn 70%", 2, 0, () =>
            {
                if (cabinet != null) cabinet.CleanSurfaceWithEthanol();
                S.Done("lau_con");
            });

            S.AddButton("Tua làm sạch (+5 ph)", 0, 1, () =>
            {
                if (cabinet != null) cabinet.FastForwardPurge(5f);
                S.Done("thoi_gian_lam_sach");
            });

            S.SetPlotAxes("VẬN TỐC DÒNG KHÍ TỦ BSC A2 (m/s)", "Vị trí", "Vận tốc", "0", "1", "0", "1");
            Refresh();
        }

        public override void Refresh()
        {
            bool fan = cabinet != null && cabinet.fanOn;
            float sash = cabinet != null ? cabinet.sashHeightCm : 20f;
            float purge = cabinet != null ? cabinet.purgeMinutes : 0f;

            S.SetResults(
                $"<b>TỦ AN TOÀN SINH HỌC CẤP II LOẠI A2 (70/30)</b>\n" +
                $"• Quạt HEPA: <b>{(fan ? "ĐANG CHẠY" : "TẮT")}</b>\n" +
                $"• Dòng khí đi xuống (Downflow): <b>{BioFeedback.FormatVi(cabinet != null ? cabinet.downflowVelocityMs : 0f, "0.00")} m/s</b> (Chuẩn: 0,35 m/s)\n" +
                $"• Dòng khí bảo vệ (Inflow): <b>{BioFeedback.FormatVi(cabinet != null ? cabinet.inflowVelocityMs : 0f, "0.00")} m/s</b> (Chuẩn: 0,52 m/s)\n" +
                $"• Độ mở kính sash: <b>{BioFeedback.FormatVi(sash, "0.0")} cm</b> {(sash <= 20.5f ? "(Đúng chuẩn 20 cm)" : "<color=#FF6666>(CẢNH BÁO QUÁ CAO!)</color>")}\n" +
                $"• Thời gian chạy làm sạch: <b>{BioFeedback.FormatVi(purge, "0.0")} phút</b> {(purge >= 5f ? "(Đạt vô trùng)" : "<color=#FFC966>(Cần >= 5 phút)</color>")}");
        }

        public override void DrawSchematic(Painter p, float time)
        {
            p.Clear(new Color32(10, 24, 20, 255));
            p.FillRect(30, 20, 240, 160, new Color32(20, 60, 50, 255));
            p.FillRect(30, 80, 20, 100, new Color32(100, 200, 240, 160));
            p.FillRect(30, 20, 240, 12, new Color32(180, 190, 200, 255));
            p.Line(120, 160, 120, 40, new Color32(80, 220, 180, 240), 3);
            p.Line(180, 160, 180, 40, new Color32(80, 220, 180, 240), 3);
        }

        public override void DrawPlot(Painter p)
        {
            p.Clear(new Color32(8, 18, 16, 255));
            p.Line(40, 50, 240, 50, new Color32(80, 220, 180, 255), 2);
            p.Line(40, 90, 240, 90, new Color32(100, 180, 255, 255), 2);
        }
    }

    /// <summary>
    /// 5.1.16 & 5.2.4. Tủ ấm vi sinh (20..60 °C, có thanh tua thời gian 24h/48h) — mã: tu_am
    /// </summary>
    [DisallowMultipleComponent]
    public class IncubatorController : MonoBehaviour, IInteractable, IScrollAdjustable
    {
        public float targetTempC = 37.0f;
        public float currentTempC = 37.0f;
        public float incubatedHours = 0f;
        public bool doorOpen = false;
        public List<PetriDish> dishesInside = new List<PetriDish>();
        public IncubatorProgram program = new IncubatorProgram();

        void Awake()
        {
            if (program == null) program = new IncubatorProgram();
            program.incubator = this;
        }

        public string UseHint => $"Tủ ấm vi sinh [Đặt: {BioFeedback.FormatVi(targetTempC, "0.0")} °C · Thực: {BioFeedback.FormatVi(currentTempC, "0.0")} °C · Đã ủ: {BioFeedback.FormatVi(incubatedHours, "0.0")} giờ · {dishesInside.Count} đĩa] · Bấm: ủ tua 24 giờ · Con lăn: đổi nhiệt độ";

        public void PlaceDish(PetriDish dish)
        {
            if (dish == null) return;
            if (!dish.isInverted)
            {
                dish.hasCondensationSpread = true;
                BioFeedback.RecordMistake("tu_am", "dat_dia_petri_ngua_trong_tu_am",
                    "Đặt đĩa Petri NGỬA (nắp ở trên) vào tủ ấm!",
                    "Khi ủ ở 37 °C, hơi nước từ thạch bốc lên đọng trên nắp nhựa rồi nhỏ ngược xuống mặt thạch làm các khuẩn lạc bị loang lẫn vào nhau. Phải ÚP NGƯỢC đĩa Petri (đáy thạch ở trên, nắp ở dưới) khi ủ.");
            }
            if (!dishesInside.Contains(dish)) dishesInside.Add(dish);
            BioFeedback.PlayClick();
            BioFeedback.LogAction("tu_am", "dat_dia_vao_tu_am",
                $"Đã đặt đĩa Petri '{dish.labelText}' ({(dish.isInverted ? "úp ngược đúng chuẩn" : "đặt ngửa — sai quy trình!")}) vào tủ ấm {BioFeedback.FormatVi(targetTempC, "0.0")} °C.");
        }

        public void FastForwardHours(float hours)
        {
            currentTempC = targetTempC;
            incubatedHours += hours;
            for (int i = 0; i < dishesInside.Count; i++)
            {
                if (dishesInside[i] != null)
                {
                    dishesInside[i].Incubate(hours, currentTempC);
                }
            }
            BioFeedback.PlaySuccess();
            BioFeedback.LogAction("tu_am", "tua_thoi_gian_u",
                $"Đã ủ trong tủ ấm ở {BioFeedback.FormatVi(currentTempC, "0.0")} °C thêm {BioFeedback.FormatVi(hours, "0")} giờ (tổng {BioFeedback.FormatVi(incubatedHours, "0")} giờ).");
        }

        public void Use(GameObject user)
        {
            FastForwardHours(24f);
        }

        public void OnScroll(float delta)
        {
            if (Mathf.Abs(delta) < 0.001f) return;
            targetTempC = Mathf.Clamp(targetTempC + Mathf.Sign(delta) * 1.0f, 20f, 60f);
            currentTempC = targetTempC;
        }
    }

    /// <summary>
    /// Màn hình vận hành Tủ ấm vi sinh (5.2.4) — kế thừa InstrumentProgram.
    /// </summary>
    public class IncubatorProgram : InstrumentProgram
    {
        public override string Code => "tu_am_vi_sinh";
        public IncubatorController incubator;

        public override void Build()
        {
            if (Doc == null || Doc.Steps.Count == 0)
                Doc = BioCatalogLoader.LoadMachineDoc(Code);

            S.AddButton("Nhiệt độ +1 °C", 0, 0, () =>
            {
                if (incubator != null) incubator.OnScroll(1f);
                S.Done("cai_dat_nhiet_do");
            });

            S.AddButton("Nhiệt độ -1 °C", 1, 0, () =>
            {
                if (incubator != null) incubator.OnScroll(-1f);
                S.Done("cai_dat_nhiet_do");
            });

            S.AddButton("Tua ủ 24 giờ", 2, 0, () =>
            {
                if (incubator != null) incubator.FastForwardHours(24f);
                S.Done("u_mau");
            });

            S.AddButton("Tua ủ 48 giờ", 0, 1, () =>
            {
                if (incubator != null) incubator.FastForwardHours(48f);
                S.Done("u_mau");
            });

            S.SetPlotAxes("NHIỆT ĐỘ BUỒNG Ủ THEO THỜI GIAN", "Thời gian (h)", "Nhiệt độ (°C)", "0", "48", "20", "60");
            Refresh();
        }

        public override void Refresh()
        {
            float tSet = incubator != null ? incubator.targetTempC : 37f;
            float tCur = incubator != null ? incubator.currentTempC : 37f;
            float hrs = incubator != null ? incubator.incubatedHours : 0f;
            int count = incubator != null ? incubator.dishesInside.Count : 0;

            S.SetResults(
                $"<b>TỦ ẤM VI SINH (INCUBATOR)</b>\n" +
                $"• Nhiệt độ cài đặt: <b>{BioFeedback.FormatVi(tSet, "0.0")} °C</b>\n" +
                $"• Nhiệt độ thực tế: <b>{BioFeedback.FormatVi(tCur, "0.0")} °C</b> (Sai số ±0,5 °C)\n" +
                $"• Thời gian đã ủ: <b>{BioFeedback.FormatVi(hrs, "0.0")} giờ</b>\n" +
                $"• Số đĩa Petri trong tủ: <b>{count} đĩa</b>");
        }

        public override void DrawSchematic(Painter p, float time)
        {
            p.Clear(new Color32(24, 18, 12, 255));
            p.FillRect(40, 30, 220, 150, new Color32(60, 45, 30, 255));
            p.FillRect(50, 40, 200, 130, new Color32(35, 25, 18, 255));
            // Các giá inox
            p.FillRect(55, 75, 190, 4, new Color32(180, 170, 160, 255));
            p.FillRect(55, 120, 190, 4, new Color32(180, 170, 160, 255));
        }

        public override void DrawPlot(Painter p)
        {
            p.Clear(new Color32(20, 14, 10, 255));
            // Đường nhiệt độ ổn định ở 37 độ
            p.Line(40, 90, 240, 90, new Color32(245, 166, 35, 255), 2);
        }
    }

    /// <summary>
    /// 5.1.16 & 5.2.6. Nồi hấp tiệt trùng đứng (121 °C, 103 kPa dư, 15–20 phút, khóa liên động áp suất) — mã: noi_hap
    /// Dùng lại AutoclaveProgram / SteamTable tầng 1.
    /// </summary>
    [DisallowMultipleComponent]
    public class AutoclaveController : MonoBehaviour, IInteractable
    {
        public bool hasEnoughWater = true;
        public bool lidLocked = true;
        public bool exhaustValveClosed = true;
        public float tempC = 25.0f;
        public float gaugePressureKpa = 0.0f; // 103 kPa dư ở 121 °C
        public bool cycleCompletedSterile = false;

        public string UseHint => cycleCompletedSterile
            ? $"Nồi hấp tiệt trùng [Đã hoàn tất chu trình 121 °C · 103 kPa · Băng chỉ thị nhiệt đã chuyển sọc đen · Áp suất dư: {BioFeedback.FormatVi(gaugePressureKpa, "0")} kPa]"
            : $"Nồi hấp tiệt trùng đứng [T = {BioFeedback.FormatVi(tempC, "0.0")} °C · P dư = {BioFeedback.FormatVi(gaugePressureKpa, "0.0")} kPa] · Bấm: chạy chu trình tiệt trùng 121 °C – 20 phút";

        public bool TryOpenLidWhilePressurized()
        {
            if (gaugePressureKpa > 5.0f)
            {
                BioFeedback.RecordMistake("noi_hap", "mo_nap_noi_hap_khi_con_ap_suat",
                    $"Cố mở nắp nồi hấp khi áp suất dư còn {BioFeedback.FormatVi(gaugePressureKpa, "0.0")} kPa ({BioFeedback.FormatVi(tempC, "0.0")} °C)!",
                    "Khóa liên động an toàn đã chặn mở nắp! Nếu mở nắp khi còn áp suất hơi nước bão hòa > 100 °C, dịch môi trường sẽ sôi bùng trào nổ gây bỏng nặng. Phải chờ áp suất về 0 kPa rồi mới mở van xả và mở nắp.");
                return false;
            }
            lidLocked = false;
            return true;
        }

        public void RunSterilizationCycle()
        {
            if (!hasEnoughWater)
            {
                BioFeedback.RecordMistake("noi_hap", "chay_noi_hap_thieu_nuoc",
                    "Vận hành nồi hấp khi mực nước cất dưới vạch tối thiểu!",
                    "Thanh đốt điện trở sẽ bị cháy khô và không đủ hơi nước bão hòa đạt 103 kPa.");
                return;
            }
            lidLocked = true;
            exhaustValveClosed = true;
            tempC = 121.0f;
            gaugePressureKpa = 103.0f;
            cycleCompletedSterile = true;
            BioFeedback.PlaySuccess();
            BioFeedback.LogAction("noi_hap", "chay_chu_trinh_121C",
                "Nồi hấp đạt 121,0 °C – 103 kPa dư trong 20 phút -> Toàn bộ môi trường và túi hấp hủy đã tiệt trùng (sterile = true), sau đó hạ áp an toàn về 0 kPa.");
            gaugePressureKpa = 0.0f;
            tempC = 65.0f;
        }

        public void Use(GameObject user)
        {
            RunSterilizationCycle();
        }
    }

    /// <summary>
    /// 5.1.16 & 5.4.10. Bể ổn nhiệt (WaterBath, 25..95 °C) — mã: be_on_nhiet
    /// </summary>
    [DisallowMultipleComponent]
    public class WaterBathController : MonoBehaviour, IInteractable, IScrollAdjustable
    {
        public float targetTempC = 60.0f;
        public float currentTempC = 60.0f;
        public int tubesInFloatingRack = 0;

        public string UseHint => $"Bể ổn nhiệt (Water Bath) [Đặt: {BioFeedback.FormatVi(targetTempC, "0.0")} °C · Thực: {BioFeedback.FormatVi(currentTempC, "0.0")} °C{(currentTempC >= 60f ? " · Có bọt nhiệt nhỏ" : "")}] · Bấm: ủ mẫu ở {BioFeedback.FormatVi(targetTempC, "0")} °C · Con lăn: chỉnh nhiệt độ";

        public void Use(GameObject user)
        {
            currentTempC = targetTempC;
            tubesInFloatingRack++;
            BioFeedback.PlayClick();
            BioFeedback.LogAction("be_on_nhiet", "u_mau_be_on_nhiet",
                $"Đã ủ mẫu trong giá nổi bể ổn nhiệt ở {BioFeedback.FormatVi(currentTempC, "0.0")} °C.");
        }

        public void OnScroll(float delta)
        {
            if (Mathf.Abs(delta) < 0.001f) return;
            targetTempC = Mathf.Clamp(targetTempC + Mathf.Sign(delta) * 5.0f, 25f, 95f);
            currentTempC = targetTempC;
        }
    }

    /// <summary>
    /// 5.4.10. Khối gia nhiệt khô (Dry Block Heater, cho ống Eppendorf) — mã: khoi_gia_nhiet_kho
    /// </summary>
    [DisallowMultipleComponent]
    public class DryBlockHeater : MonoBehaviour, IInteractable, IScrollAdjustable
    {
        public float targetTempC = 65.0f;
        public float currentTempC = 65.0f;

        public string UseHint => $"Khối gia nhiệt khô (Dry Block) [Màn LED đỏ: {BioFeedback.FormatVi(currentTempC, "0.0")} °C] · Bấm: ủ ống Eppendorf 1,5 mL · Con lăn: chỉnh nhiệt độ";

        public void Use(GameObject user)
        {
            currentTempC = targetTempC;
            BioFeedback.PlayClick();
            BioFeedback.LogAction("khoi_gia_nhiet_kho", "gia_nhiet_eppendorf",
                $"Đã gia nhiệt ống Eppendorf trên khối nhiệt khô ở {BioFeedback.FormatVi(currentTempC, "0.0")} °C.");
        }

        public void OnScroll(float delta)
        {
            if (Mathf.Abs(delta) < 0.001f) return;
            targetTempC = Mathf.Clamp(targetTempC + Mathf.Sign(delta) * 5.0f, 30f, 100f);
            currentTempC = targetTempC;
        }
    }

    /// <summary>
    /// 5.1.16 & 5.4.5. Máy lắc ống nghiệm Vortex — mã: may_vortex
    /// </summary>
    [DisallowMultipleComponent]
    public class VortexMixer : MonoBehaviour, IInteractable, IScrollAdjustable
    {
        public int speedLevel = 5; // 0..10
        public bool continuousMode = false;
        public bool isVibrating = false;

        public string UseHint => $"Máy lắc Vortex [Mức tốc độ: {speedLevel}/10 · Chế độ: {(continuousMode ? "Liên tục" : "Chạm (Touch)")}] · Bấm: trộn đều ống mẫu · Con lăn: đổi tốc độ";

        public void VortexSample(BioSample sample, float durationSeconds)
        {
            isVibrating = true;
            if (sample != null && sample.dna.Count > 0 && speedLevel >= 9 && durationSeconds > 5f)
            {
                for (int i = 0; i < sample.dna.Count; i++)
                {
                    var b = sample.dna[i];
                    if (b.bp >= 5000)
                    {
                        b.smear = true;
                        sample.dna[i] = b;
                    }
                }
                sample.SyncToData();
                BioFeedback.RecordMistake("may_vortex", "vortex_adn_bo_gen_qua_manh",
                    $"Lắc Vortex ADN bộ gen ở tốc độ tối đa ({speedLevel}/10) quá lâu làm đứt gãy cơ học sợi ADN!",
                    "ADN bộ gen kích thước lớn rất dễ bị lực cắt thủy động làm đứt gãy tạo vệt kéo dài (smear) khi chạy điện di.");
                return;
            }
            BioFeedback.PlayClick();
            BioFeedback.LogAction("may_vortex", "lac_tron_ong_nghiem",
                $"Đã lắc trộn đồng nhất dung dịch trên máy Vortex ở mức {speedLevel}/10.");
        }

        public void Use(GameObject user)
        {
            VortexSample(null, 2.0f);
        }

        public void OnScroll(float delta)
        {
            if (Mathf.Abs(delta) < 0.001f) return;
            speedLevel = Mathf.Clamp(speedLevel + (delta > 0 ? 1 : -1), 1, 10);
        }
    }

    /// <summary>
    /// 5.1.16 & 5.4.6. Kiểm tra cân tải đối xứng cho Máy ly tâm (kết hợp Centrifuge tầng 1) — mã: may_ly_tam_de_ban, may_ly_tam_mini
    /// Công thức: RCF = 1,118 × 10⁻⁵ × r(cm) × rpm²
    /// </summary>
    [DisallowMultipleComponent]
    public class CentrifugeLoad : MonoBehaviour, IInteractable, IScrollAdjustable
    {
        public string centrifugeId = "may_ly_tam_mini";
        public int rotorSlots = 12;
        public float rotorRadiusCm = 6.5f;
        public int rpm = 6000;
        public bool lidClosed = true;
        public readonly float[] slotMassGrams = new float[24];

        public float CalculateRcf() => (float)Centrifugation.Rcf(rotorRadiusCm, rpm);

        public string UseHint => $"Máy ly tâm ({centrifugeId}) [{rpm} rpm ≈ {BioFeedback.FormatVi(CalculateRcf(), "0")} ×g · Nắp: {(lidClosed ? "ĐÓNG" : "MỞ")}] · Bấm: kiểm tra cân tải đối xứng & quay ly tâm";

        public void SetBalancedPair(int slotA, float massGrams)
        {
            Array.Clear(slotMassGrams, 0, slotMassGrams.Length);
            int n = Mathf.Clamp(rotorSlots, 2, 24);
            int slotB = (slotA + n / 2) % n;
            slotMassGrams[slotA % n] = massGrams;
            slotMassGrams[slotB] = massGrams;
        }

        public void SetUnbalancedSingle(int slotA, float massGrams)
        {
            Array.Clear(slotMassGrams, 0, slotMassGrams.Length);
            slotMassGrams[slotA % Mathf.Clamp(rotorSlots, 2, 24)] = massGrams;
        }

        public bool IsBalanced(out float imbalanceGrams)
        {
            int n = Mathf.Clamp(rotorSlots, 2, 24);
            var loadList = new List<(int slot, double grams)>();
            float fx = 0f, fy = 0f;
            for (int i = 0; i < n; i++)
            {
                if (slotMassGrams[i] > 0f)
                    loadList.Add((i + 1, slotMassGrams[i]));
                float angle = (2f * Mathf.PI * i) / n;
                fx += slotMassGrams[i] * Mathf.Cos(angle);
                fy += slotMassGrams[i] * Mathf.Sin(angle);
            }
            imbalanceGrams = Mathf.Sqrt(fx * fx + fy * fy);
            return Centrifugation.IsBalanced(loadList, n, out _);
        }

        public bool RunSpin(BioSample sample = null)
        {
            if (!lidClosed)
            {
                BioFeedback.RecordMistake(centrifugeId, "chay_ly_tam_chua_dong_nap",
                    "Bấm chạy máy ly tâm khi chưa đóng nắp an toàn!",
                    "Khóa liên động nắp máy ly tâm ngăn không cho rotor quay khi nắp mở.");
                return false;
            }
            if (!IsBalanced(out float diffG))
            {
                BioFeedback.RecordMistake(centrifugeId, "ly_tam_lech_tai",
                    $"Mất cân bằng tải rotor (lệch {BioFeedback.FormatVi(diffG, "0.00")} g > 0,10 g)! Máy rung mạnh và tự ngắt khẩn cấp!",
                    "Phải luôn đặt các ống Eppendorf có khối lượng tương đương (chênh ≤ 0,1 g) vào các vị trí đối xứng qua tâm rotor (thêm 1 ống đối trọng chứa nước nếu số mẫu lẻ).");
                return false;
            }
            BioFeedback.PlaySuccess();
            BioFeedback.LogAction(centrifugeId, "ly_tam_thanh_cong",
                $"Đã ly tâm cân bằng tại {rpm} rpm ({BioFeedback.FormatVi(CalculateRcf(), "0")} ×g): cặn lắng xuống đáy, dịch nổi tách pha rõ, giọt bám thành đã gom hết xuống đáy.");
            return true;
        }

        public void Use(GameObject user)
        {
            SetBalancedPair(0, 1.5f);
            RunSpin();
        }

        public void OnScroll(float delta)
        {
            if (Mathf.Abs(delta) < 0.001f) return;
            rpm = Mathf.Clamp(rpm + (delta > 0 ? 1000 : -1000), 1000, 14000);
        }
    }

    /// <summary>
    /// 5.1.17. Bình lên men rượu (bình tam giác 250 mL + bóng bay chụp miệng) — mã: binh_len_men
    /// Gọi trực tiếp Fermentation.Co2MolesAt và Fermentation.BalloonDiameterCm của Backend.
    /// </summary>
    [DisallowMultipleComponent]
    public class FermentationFlask : MonoBehaviour, IInteractable
    {
        public double glucoseGrams = 10.0;
        public double yeastGrams = 2.0;
        public double waterTempC = 32.0; // Nếu đun sôi >= 60 °C thì men chết!
        public double elapsedHours = 0.0;
        public bool balloonAttached = true;

        public double Co2Moles => Fermentation.Co2MolesAt(elapsedHours, waterTempC, glucoseGrams, waterTempC < 60.0 && yeastGrams > 0.0);
        public double Co2Liters => Fermentation.BalloonVolumeLiters(Co2Moles, waterTempC);
        public double BalloonDiameterCm => Fermentation.BalloonDiameterCm(Co2Liters);

        public Transform balloonVisual;

        public string UseHint => $"Bình lên men rượu 250 mL [Đường: {BioFeedback.FormatVi((float)glucoseGrams, "0.0")} g · Men: {BioFeedback.FormatVi((float)yeastGrams, "0.0")} g · T = {BioFeedback.FormatVi((float)waterTempC, "0")} °C · Đã ủ {BioFeedback.FormatVi((float)elapsedHours, "0.0")} h → Bóng bay Ø {BioFeedback.FormatVi((float)BalloonDiameterCm, "0.1")} cm] · Bấm: ủ tua 2 giờ";

        public void SetupCondition(double sugarG, double yeastG, double tempC)
        {
            glucoseGrams = sugarG;
            yeastGrams = yeastG;
            waterTempC = tempC;
            elapsedHours = 0.0;
            if (tempC >= 55.0)
            {
                BioFeedback.RecordMistake("binh_len_men", "pha_men_bang_nuoc_soi",
                    $"Pha nấm men S. cerevisiae bằng nước quá nóng ({BioFeedback.FormatVi((float)tempC, "0")} °C ≥ 55 °C) làm chết tế bào nấm men!",
                    "Enzyme zymase và màng tế bào nấm men bị biến tính nhiệt nên không sinh ra khí CO₂, bóng bay không phồng.");
            }
            UpdateBalloonScale();
        }

        public void AdvanceTimeHours(double hours)
        {
            elapsedHours += hours;
            UpdateBalloonScale();
            BioFeedback.PlayBubblePop();
            BioFeedback.LogAction("binh_len_men", "len_men_ruou_co2",
                $"Lên men rượu sau {BioFeedback.FormatVi((float)elapsedHours, "0.0")} giờ ở {BioFeedback.FormatVi((float)waterTempC, "0")} °C: n(CO₂) = {BioFeedback.FormatVi((float)Co2Moles, "0.000")} mol ({BioFeedback.FormatVi((float)Co2Liters, "0.00")} L) → Đường kính bóng bay = {BioFeedback.FormatVi((float)BalloonDiameterCm, "0.1")} cm.");
        }

        void UpdateBalloonScale()
        {
            if (balloonVisual == null) return;
            float dMeters = Mathf.Max(0.03f, (float)BalloonDiameterCm * 0.01f);
            balloonVisual.localScale = new Vector3(dMeters, dMeters * 1.15f, dMeters);
        }

        public void Use(GameObject user)
        {
            AdvanceTimeHours(2.0);
        }
    }

    /// <summary>
    /// 5.1.18. Bộ làm sữa chua (lên men lactic) — mã: bo_lam_sua_chua
    /// Gọi Fermentation.LacticAcidGramsAt của Backend.
    /// </summary>
    [DisallowMultipleComponent]
    public class YogurtJar : MonoBehaviour, IInteractable
    {
        public double lactoseGrams = 12.0; // trong 250 mL sữa
        public double tempC = 42.0;
        public double incubatedHours = 0.0;
        public bool hasStarterCulture = true;
        public bool lidClosed = true;

        public double LacticAcidGrams => hasStarterCulture ? Fermentation.LacticAcidGramsAt(incubatedHours, lactoseGrams, tempC) : 0.0;
        public float CurrentPh
        {
            get
            {
                double g = LacticAcidGrams;
                // Từ pH 6,60 của sữa tươi hạ dần về ~4,45 khi tích lũy axit lactic
                float drop = (float)Math.Min(2.15, (g / Math.Max(1.0, lactoseGrams)) * 2.5);
                return 6.60f - drop;
            }
        }
        public bool IsCoagulated => CurrentPh <= 4.75f;

        public string UseHint => $"Hũ làm sữa chua (L. bulgaricus) [Ủ {BioFeedback.FormatVi((float)tempC, "0")} °C · {BioFeedback.FormatVi((float)incubatedHours, "0.0")} giờ · Axit lactic: {BioFeedback.FormatVi((float)LacticAcidGrams, "0.00")} g · pH = {BioFeedback.FormatVi(CurrentPh, "0.00")} · {(IsCoagulated ? "Sữa đã đông đặc mịn" : "Đang lỏng")}] · Bấm: ủ thêm 3 giờ";

        public void IncubateHours(double hours, double temperatureC = 42.0)
        {
            tempC = temperatureC;
            if (tempC > 55.0)
            {
                BioFeedback.RecordMistake("bo_lam_sua_chua", "u_sua_chua_nuoc_soi",
                    $"Ủ sữa chua ở nhiệt độ quá cao ({BioFeedback.FormatVi((float)tempC, "0")} °C) làm chết vi khuẩn L. bulgaricus!",
                    "Nhiệt độ tối ưu cho vi khuẩn lactic sữa chua là 40 – 45 °C.");
            }
            incubatedHours += hours;
            BioFeedback.PlaySuccess();
            BioFeedback.LogAction("bo_lam_sua_chua", "u_len_men_lactic",
                $"Sau {BioFeedback.FormatVi((float)incubatedHours, "0.0")} giờ ủ ở {BioFeedback.FormatVi((float)tempC, "0")} °C: sinh ra {BioFeedback.FormatVi((float)LacticAcidGrams, "0.00")} g axit lactic, pH giảm còn {BioFeedback.FormatVi(CurrentPh, "0.00")} ({(IsCoagulated ? "casein kết tủa làm sữa đông đặc" : "chưa đông")}).");
        }

        public void Use(GameObject user)
        {
            IncubateHours(3.0, tempC);
        }
    }

    /// <summary>
    /// 5.1.19. Bộ xương người trên giá có bánh xe — mã: bo_xuong_gia
    /// </summary>
    [DisallowMultipleComponent]
    public class SkeletonStand : MonoBehaviour, IInteractable
    {
        public static readonly string[] BoneNames =
        {
            "Hộp sọ (Cranium)",
            "Cột sống (33–34 đốt sống)",
            "Lồng ngực (12 đôi xương sườn & xương ức)",
            "Xương đùi (Femur — xương dài nhất cơ thể)",
            "Khung chậu (Pelvis)"
        };
        public int index = 0;

        public string UseHint => $"Mô hình bộ xương người 170 cm [Đang trỏ: {BoneNames[index]}] · Bấm để xem giải phẫu từng phần xương";

        public void Use(GameObject user)
        {
            string bone = BoneNames[index];
            index = (index + 1) % BoneNames.Length;
            BioFeedback.PlayClick();
            BioFeedback.LogAction("bo_xuong_gia", "xem_ten_xuong", $"Tra cứu bộ xương: {bone}.");
        }
    }

    /// <summary>
    /// 5.1.20 & 5.6. Quầy rửa tay cảm ứng (xà phòng -> vòi nước >= 20 s -> giấy lau -> bật cờ tayDaRua) — mã: quay_rua_tay
    /// </summary>
    [DisallowMultipleComponent]
    public class HandWashStation : MonoBehaviour, IInteractable
    {
        public bool soapApplied = false;
        public float washSeconds = 0f;
        public bool handsDried = false;

        public string UseHint => BioTransfer.HandsWashed
            ? "Quầy rửa tay cảm ứng [Đã rửa tay đúng quy trình 6 bước ≥ 20 s (tayDaRua = true)]"
            : "Quầy rửa tay cảm ứng (xà phòng + vòi cảm ứng + giấy lau) · Bấm để rửa tay khử khuẩn 20 s trước khi thao tác vô trùng";

        public void CompleteWashRoutine(float seconds = 22f)
        {
            soapApplied = true;
            washSeconds = seconds;
            if (seconds < 20f)
            {
                BioTransfer.HandsWashed = false;
                BioFeedback.RecordMistake("quay_rua_tay", "rua_tay_chua_du_20_giay",
                    $"Rửa tay quá nhanh ({BioFeedback.FormatVi(seconds, "0")} s < 20 s) chưa sạch vi sinh vật trên da!",
                    "Theo quy trình WHO/CDC, phải chà xà phòng dưới vòi nước chảy tối thiểu 20 giây rồi lau khô bằng khăn giấy sạch.");
                return;
            }
            handsDried = true;
            BioTransfer.HandsWashed = true;
            BioFeedback.PlaySuccess();
            BioFeedback.LogAction("quay_rua_tay", "rua_tay_chuan_20s",
                "Đã rửa tay bằng xà phòng dưới vòi cảm ứng đủ 20 s và lau khô (tayDaRua = true).");
        }

        public void Use(GameObject user)
        {
            CompleteWashRoutine(22f);
        }
    }

    /// <summary>
    /// 5.1.20. Bồn rửa mắt khẩn cấp — mã: bon_rua_mat
    /// </summary>
    [DisallowMultipleComponent]
    public class EyeWashStation : MonoBehaviour, IInteractable
    {
        public bool isSpraying = false;

        public string UseHint => isSpraying
            ? "Bồn rửa mắt khẩn cấp [ĐANG PHUN 2 VÒI RỬA MẮT — đã ghi nhật ký sự cố] · Bấm để khóa"
            : "Bồn rửa mắt khẩn cấp · Kéo cần khi hóa chất/sinh phẩm bắn vào mắt (xối liên tục ≥ 15 phút)";

        public void Use(GameObject user)
        {
            isSpraying = !isSpraying;
            BioFeedback.PlayClick();
            BioFeedback.LogAction("bon_rua_mat", isSpraying ? "kich_hoat_rua_mat_khan_cap" : "dong_bon_rua_mat",
                isSpraying ? "Đã kéo cần kích hoạt vòi rửa mắt khẩn cấp hai tia và gửi thông báo sự cố lên web!" : "Đã đóng van bồn rửa mắt khẩn cấp.");
        }
    }

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

    /// <summary>
    /// 5.1.21. Thùng rác nguy hại sinh học (màu vàng/đỏ có biểu tượng biohazard, nắp đạp chân) — mã: thung_rac_nguy_hai
    /// </summary>
    [DisallowMultipleComponent]
    public class BiohazardBin : MonoBehaviour, IInteractable
    {
        public int disposedItemsCount = 0;
        public BioHinge lidHinge;

        public string UseHint => $"Thùng rác nguy hại sinh học [Đã thu gom {disposedItemsCount} vật tư nhiễm sinh học] · Bấm: đạp chân mở nắp & bỏ rác nguy hại";

        public void DisposeBiohazardItem(string itemName, bool isSharpObject = false)
        {
            if (isSharpObject)
            {
                BioFeedback.RecordMistake("thung_rac_nguy_hai", "bo_vat_sac_vao_tui_nhua_nguy_hai",
                    $"Bỏ vật sắc nhọn ({itemName}) vào túi nilon thùng rác nguy hại!",
                    "Kim tiêm, dao lam, lam kính vỡ sẽ đâm thủng túi nhựa gây thương tích. Phải bỏ vật sắc nhọn vào hộp cứng màu vàng (SharpsBin).");
                return;
            }
            disposedItemsCount++;
            if (lidHinge != null) lidHinge.SetOpen(true);
            BioFeedback.PlayClick();
            BioFeedback.LogAction("thung_rac_nguy_hai", "bo_rac_nguy_hai",
                $"Đã bỏ '{itemName}' vào thùng rác nguy hại sinh học (tổng: {disposedItemsCount} món).");
        }

        public void Use(GameObject user)
        {
            DisposeBiohazardItem("Đĩa Petri / Găng tay đã qua sử dụng", false);
        }
    }

    /// <summary>
    /// 5.1.21. Thùng đựng vật sắc nhọn (hộp cứng màu vàng) — mã: thung_vat_sac
    /// </summary>
    [DisallowMultipleComponent]
    public class SharpsBin : MonoBehaviour, IInteractable
    {
        public int sharpsCount = 0;

        public string UseHint => $"Hộp đựng vật sắc nhọn màu vàng [Đang chứa {sharpsCount} vật sắc (lam kính vỡ, dao lam)] · Bấm: bỏ vật sắc nhọn an toàn";

        public void DisposeSharp(string itemName)
        {
            sharpsCount++;
            BioFeedback.PlayClick();
            BioFeedback.LogAction("thung_vat_sac", "bo_vat_sac_nhon",
                $"Đã bỏ vật sắc nhọn '{itemName}' vào hộp kháng thủng màu vàng (tổng: {sharpsCount}).");
        }

        public void Use(GameObject user)
        {
            DisposeSharp("Lam kính / Lưỡi dao lam đã dùng");
        }
    }

    /// <summary>
    /// 5.1.21. Tủ sơ cứu y tế — mã: tu_so_cuu
    /// </summary>
    [DisallowMultipleComponent]
    public class FirstAidCabinet : MonoBehaviour, IInteractable
    {
        public bool isOpen = false;
        public BioHinge doorHinge;

        public string UseHint => isOpen
            ? "Tủ sơ cứu y tế [Gạc vô trùng, cồn povidone-iodine, băng cá nhân, nước muối sinh lý 0,9 %] · Bấm: đóng tủ"
            : "Tủ sơ cứu y tế · Bấm để mở tủ lấy băng gạc và dung dịch sát khuẩn vết thương";

        public void Use(GameObject user)
        {
            isOpen = !isOpen;
            if (doorHinge != null) doorHinge.SetOpen(isOpen);
            BioFeedback.PlayClick();
            BioFeedback.LogAction("tu_so_cuu", isOpen ? "mo_tu_so_cuu" : "dong_tu_so_cuu",
                $"Tủ sơ cứu y tế: {(isOpen ? "Đã mở kiểm tra vật tư sơ cứu" : "Đã đóng")}.");
        }
    }

    /// <summary>
    /// 5.1.21 & 5.7. Giá treo đồ bảo hộ cá nhân (Áo blouse trắng, găng tay nitrile, kính bảo hộ) — mã: do_bao_ho
    /// </summary>
    [DisallowMultipleComponent]
    public class PpeStation : MonoBehaviour, IInteractable
    {
        public bool wearingCoat = false;
        public bool wearingGloves = false;
        public bool wearingGoggles = false;

        public string UseHint => BioTransfer.PpeEquipped
            ? "Trạm đồ bảo hộ cá nhân (PPE) [Đang mặc đủ: Áo blouse + Găng tay nitrile + Kính bảo hộ (daMacBaoHo = true)] · Bấm: cởi đồ bảo hộ trước khi rời khu"
            : "Trạm đồ bảo hộ cá nhân (Áo blouse, Găng tay nitrile, Kính bảo hộ) · Bấm để mặc đầy đủ PPE trước khi vào thí nghiệm";

        public void EquipAll(bool equip)
        {
            wearingCoat = equip;
            wearingGloves = equip;
            wearingGoggles = equip;
            BioTransfer.PpeEquipped = equip;
            BioFeedback.PlaySuccess();
            BioFeedback.LogAction("do_bao_ho", equip ? "mac_do_bao_ho" : "coi_do_bao_ho",
                equip
                    ? "Đã mặc đầy đủ áo blouse, đeo găng tay nitrile và kính bảo hộ (daMacBaoHo = true)."
                    : "Đã tháo bỏ găng tay và treo áo blouse đúng quy định trước khi rời phòng.");
        }

        public void Use(GameObject user)
        {
            EquipAll(!BioTransfer.PpeEquipped);
        }
    }
}
