// -----------------------------------------------------------------------------
// MicrobioTools.cs — Toàn bộ dụng cụ và thiết bị Phòng vi sinh, Buồng đệm & Pass-box
// (Mục 5.2 PROMPT_DUNG_CU_GIAO_DIEN.md & Mục 2, 4 HOP_DONG_GIAO_DIEN_BACKEND.md).
// -----------------------------------------------------------------------------
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
    /// 5.2.2. Đèn cồn thủy tinh (tạo vùng vô trùng hình nón bán kính ~ 10–15 cm) — mã: den_con
    /// </summary>
    [DisallowMultipleComponent]
    public class SpiritLamp : MonoBehaviour, IInteractable
    {
        public bool capRemoved = false;
        public bool isLit = false;
        public float sterileConeRadiusM = 0.15f;
        public Transform capVisual;
        public Transform flameVisual;

        public string UseHint => isLit
            ? "Đèn cồn thủy tinh [ĐANG CHÁY — tạo vùng đối lưu vô trùng bán kính 15 cm] · Bấm: đậy nắp chụp tắt lửa (TUYỆT ĐỐI KHÔNG THỔI)"
            : "Đèn cồn thủy tinh · Bấm để mở nắp và châm lửa tạo vùng thao tác vô trùng";

        public void BlowOutMistake()
        {
            if (!isLit) return;
            BioFeedback.RecordMistake("den_con", "thoi_tat_den_con_bang_mieng",
                "Dùng miệng thổi tắt ngọn lửa đèn cồn!",
                "Thổi bằng miệng dễ làm ngọn lửa tạt ngược vào cổ bình cồn gây cháy nổ và làm phát tán vi sinh vật từ hơi thở lên bàn cấy. Phải luôn dùng nắp chụp đậy kín để cắt nguồn oxy.");
        }

        public void SetLit(bool lit)
        {
            isLit = lit;
            capRemoved = lit;
            BioTransfer.NearSpiritFlame = lit;
            if (capVisual != null) capVisual.gameObject.SetActive(!lit);
            if (flameVisual != null) flameVisual.gameObject.SetActive(lit);
            BioFeedback.PlayClick();
            BioFeedback.LogAction("den_con", lit ? "cham_den_con" : "day_nap_tat_den_con",
                lit ? "Đã mở nắp và châm đèn cồn: tạo vùng vô trùng hình nón ~ 15 cm quanh ngọn lửa."
                    : "Đã đậy nắp chụp tắt đèn cồn an toàn.");
        }

        public void Use(GameObject user)
        {
            SetLit(!isLit);
        }
    }

    /// <summary>
    /// 5.2.3. Que cấy vòng (Ø 3 mm), que cấy thẳng, que cấy nhựa dùng 1 lần — mã: que_cay_vong, que_cay_thang, que_cay_nhua
    /// </summary>
    [DisallowMultipleComponent]
    public class InoculatingLoop : MonoBehaviour, IInteractable
    {
        public string loopToolId = "que_cay_vong";
        public bool isDisposablePlastic = false;
        public bool isSterile = false;
        public float wireTempC = 25.0f; // >= 750 °C khi đốt đỏ rực; phải để nguội < 42 °C trước khi chạm vi khuẩn
        public string carriedOrganismId = "";
        public double carriedCfuPerMl = 0.0;
        public BioTool bioTool;

        void Awake()
        {
            bioTool = GetComponent<BioTool>();
            if (bioTool == null) bioTool = gameObject.AddComponent<BioTool>();
            bioTool.toolId = loopToolId;
            bioTool.toolDisplayName = isDisposablePlastic ? "Que cấy nhựa vô trùng dùng 1 lần" : "Que cấy vòng kim loại Ø 3 mm";
            bioTool.kind = BioToolKind.InoculationLoop;
            if (isDisposablePlastic)
            {
                isSterile = true;
                bioTool.isSterile = true;
            }
        }

        public string UseHint
        {
            get
            {
                if (isDisposablePlastic)
                    return $"Que cấy nhựa dùng 1 lần [Vô trùng sẵn · Mẫu: {(string.IsNullOrEmpty(carriedOrganismId) ? "Chưa lấy" : carriedOrganismId)}] · Dùng xong bỏ thùng nguy hại";
                string state = !isSterile ? "CHƯA KHỬ TRÙNG" : (wireTempC > 45f ? $"ĐANG NÓNG ({BioFeedback.FormatVi(wireTempC, "0")} °C — chờ nguội!)" : "Đã vô trùng & nguội");
                return $"Que cấy ({loopToolId}) [{state} · Mẫu: {(string.IsNullOrEmpty(carriedOrganismId) ? "Trống" : carriedOrganismId)}] · Bấm: hơ đỏ trên đèn cồn & để nguội 8 s";
            }
        }

        public void FlameSterilize(bool waitCoolDown = true)
        {
            if (isDisposablePlastic)
            {
                BioFeedback.RecordMistake("que_cay_nhua", "dot_que_cay_nhua_tren_den_con",
                    "Đưa que cấy nhựa dùng 1 lần vào ngọn lửa đèn cồn làm nhựa chảy sun và bốc khói!",
                    "Que cấy nhựa đã được tiệt trùng sẵn bằng tia gamma trong túi kín; chỉ dùng 1 lần rồi bỏ vào thùng rác nguy hại, tuyệt đối không đốt.");
                return;
            }
            isSterile = true;
            wireTempC = waitCoolDown ? 32.0f : 650.0f;
            carriedOrganismId = "";
            carriedCfuPerMl = 0.0;
            if (bioTool != null)
            {
                bioTool.isSterile = true;
                bioTool.temperatureC = wireTempC;
            }
            BioFeedback.PlaySuccess();
            BioFeedback.LogAction(loopToolId, "khu_trung_que_cay",
                waitCoolDown
                    ? $"Đã nung đỏ đầu {loopToolId} trên ngọn lửa đèn cồn (≥ 1,5 s) và chờ nguội 8 s xuống {BioFeedback.FormatVi(wireTempC, "0")} °C (sterile = true)."
                    : $"Đã nung đỏ đầu {loopToolId} lên {BioFeedback.FormatVi(wireTempC, "0")} °C (CHƯA ĐỂ NGUỘI!).");
        }

        public bool PickInoculumFromCulture(string organismId, double cfuPerMl)
        {
            if (!isSterile)
            {
                BioFeedback.RecordMistake(loopToolId, "lay_giong_bang_que_chua_dot",
                    "Dùng que cấy chưa khử trùng nhúng vào ống giống vi sinh vật!",
                    "Làm nhiễm bẩn ống giống gốc. Phải nung đỏ dây que cấy và để nguội trước khi lấy mẫu.");
                return false;
            }
            if (wireTempC > 45.0f)
            {
                carriedOrganismId = organismId;
                carriedCfuPerMl = 0.0; // Vi khuẩn chết sạch do sốc nhiệt!
                BioFeedback.RecordMistake(loopToolId, "cham_que_cay_nong_giet_vi_khuan",
                    $"Chạm que cấy còn nóng ({BioFeedback.FormatVi(wireTempC, "0")} °C) vào giống '{organismId}' phát tiếng xèo — làm chết toàn bộ vi khuẩn!",
                    "Sau khi nung đỏ trên đèn cồn, phải đợi 5–10 giây cho dây hợp kim nguội hẳn mới được lấy sinh khối vi khuẩn.");
                return false;
            }
            carriedOrganismId = organismId;
            carriedCfuPerMl = cfuPerMl;
            if (bioTool != null)
            {
                bioTool.reagentOrSampleId = organismId;
                bioTool.reagentDisplayName = organismId;
            }
            BioFeedback.PlayClick();
            BioFeedback.LogAction(loopToolId, "lay_mau_vi_khuan",
                $"Vòng que cấy (~10 µL) đã lấy mẫu chủng '{organismId}' (mật độ {cfuPerMl:E2} CFU/mL).");
            return true;
        }

        public void Use(GameObject user)
        {
            FlameSterilize(true);
            if (string.IsNullOrEmpty(carriedOrganismId))
            {
                PickInoculumFromCulture("e_coli_k12", 1.0e8);
            }
        }
    }

    /// <summary>
    /// 5.2.3. Que trang thủy tinh hình chữ L (Cell Spreader) — mã: que_trang
    /// </summary>
    [DisallowMultipleComponent]
    public class CellSpreader : MonoBehaviour, IInteractable
    {
        public bool sterilizedWithEthanolFlame = false;
        public bool cooledDown = true;

        public string UseHint => sterilizedWithEthanolFlame
            ? "Que trang thủy tinh chữ L [Đã nhúng cồn 70 %, hơ lửa xanh 1 s và để nguội] · Bấm đĩa Petri để dàn đều 0,1 mL dịch khuẩn"
            : "Que trang thủy tinh chữ L · Bấm để nhúng cồn, hơ lửa khử trùng và để nguội trước khi trang đĩa";

        public void SterilizeAndSpread(PetriDish targetDish)
        {
            sterilizedWithEthanolFlame = true;
            cooledDown = true;
            if (targetDish != null)
            {
                targetDish.SpreadSurfaceUniformly();
            }
            BioFeedback.PlaySuccess();
            BioFeedback.LogAction("que_trang", "trang_deu_dia_thach",
                "Đã khử trùng que trang chữ L bằng cồn + lửa và xoay đĩa dàn đều 0,1 mL dịch pha loãng khắp mặt thạch.");
        }

        public void Use(GameObject user)
        {
            SterilizeAndSpread(null);
        }
    }

    /// <summary>
    /// 5.2.5. Đĩa Petri Ø 90 mm (nắp nhấc nghiêng 30° kiểu "vỏ sò", ria 4 vùng hoặc cấy trang) — mã: dia_petri
    /// Gọi trực tiếp StreakPlate.Colonies, Dilution.SampleColonies và ContaminationRules.Roll của Backend.
    /// </summary>
    [DisallowMultipleComponent]
    public class PetriDish : MonoBehaviour, IInteractable
    {
        public string mediaId = "thach_lb"; // thach_dinh_duong | thach_lb | thach_sabouraud
        public string labelText = "E. coli K-12 · 10⁻⁵";
        public bool lidClamshell30Deg = true; // Mở kiểu vỏ sò 30° chuẩn; nếu mở toang 100% lâu ngoài vùng vô trùng -> nhiễm
        public float openExposureSeconds = 2.0f;
        public bool isInverted = true;        // Úp ngược khi đặt tủ ấm
        public bool hasCondensationSpread = false;

        public double platedVolumeMl = 0.1;
        public double dilutionFactor = 1e-5;
        public bool isSpreadPlate = false;
        public int countedColonies = 0;

        public readonly List<StreakStroke> streakStrokes = new List<StreakStroke>();
        public readonly List<ColonySpot> grownColonies = new List<ColonySpot>();
        public BioSample sample;

        void Awake()
        {
            sample = GetComponent<BioSample>();
            if (sample == null) sample = gameObject.AddComponent<BioSample>();
            sample.sterile = true;
        }

        public string UseHint => $"Đĩa Petri Ø 90 mm ({mediaId}) [{labelText} · {(isInverted ? "Úp ngược" : "Đặt ngửa")} · Khuẩn lạc: {(grownColonies.Count > 0 ? grownColonies.Count : countedColonies)}{(sample != null && sample.isContaminated ? " · TẠP NHIỄM!" : "")}] · Bấm: ria 4 vùng / lật úp đĩa";

        /// <summary>
        /// Thực hiện cấy ria 4 vùng (Streak Plate) có kiểm tra đốt que cấy giữa các vùng và tạp nhiễm.
        /// </summary>
        public void PerformFourZoneStreak(InoculatingLoop loop, bool flameBetweenZones = true, float lidOpenSec = 3.0f)
        {
            if (sample == null) return;
            openExposureSeconds = lidOpenSec;
            bool loopSterile = loop == null || loop.isSterile;
            string org = (loop != null && !string.IsNullOrEmpty(loop.carriedOrganismId)) ? loop.carriedOrganismId : "e_coli_k12";
            double inoc = (loop != null) ? loop.carriedCfuPerMl : 1e8;

            sample.organismId = org;
            sample.sampleId = org;
            sample.sampleName = $"Đĩa ria 4 vùng ({org})";
            sample.cfuPerMl = inoc;
            sample.streakZonesCompleted = 4;

            if (!flameBetweenZones)
            {
                BioFeedback.RecordMistake("dia_petri", "ria_4_vung_quen_dot_que_giua_cac_vung",
                    "Ria liên tiếp 4 vùng mà KHÔNG đốt khử trùng que cấy giữa các vùng!",
                    "Mật độ vi khuẩn không được pha loãng bậc thang nên vùng 3 và vùng 4 vẫn mọc dày đặc, không tách được khuẩn lạc đơn.");
            }

            streakStrokes.Clear();
            for (int z = 1; z <= 4; z++)
            {
                bool sterilizedBefore = (z == 1) ? loopSterile : flameBetweenZones;
                for (int s = 0; s < 3; s++)
                {
                    streakStrokes.Add(new StreakStroke
                    {
                        zone = z,
                        x0 = z * 15f + s * 3f,
                        y0 = 10f + s * 8f,
                        x1 = z * 15f + s * 3f + 18f,
                        y1 = 35f + s * 8f,
                        loopSterilizedBefore = sterilizedBefore
                    });
                }
            }

            int seed = labelText.GetHashCode() ^ (int)(lidOpenSec * 100);
            BioTransfer.CheckContamination(loopSterile, lidOpenSec, seed, sample);
            sample.SyncToData();

            BioFeedback.PlaySuccess();
            BioFeedback.LogAction("dia_petri", "cay_ria_4_vung",
                $"Đã cấy ria 4 vùng chủng '{org}' lên đĩa {mediaId} (đốt que giữa các vùng: {(flameBetweenZones ? "CÓ" : "KHÔNG")}).");
        }

        /// <summary>
        /// Nhỏ dịch pha loãng (vd 0,1 mL ở 10^-5) để cấy trang đếm khuẩn lạc.
        /// </summary>
        public void InoculateSpreadPlate(string organismId, double tubeCfuPerMl, double volMl, double dilution, float lidOpenSec = 3.0f)
        {
            if (sample == null) return;
            isSpreadPlate = true;
            platedVolumeMl = volMl;
            dilutionFactor = dilution;
            openExposureSeconds = lidOpenSec;

            sample.organismId = organismId;
            sample.sampleId = organismId;
            sample.sampleName = $"Đĩa cấy trang {organismId} ({dilution:E0})";
            sample.cfuPerMl = tubeCfuPerMl;
            sample.volumeMl = (float)volMl;

            int seed = Math.Abs(organismId.GetHashCode() ^ (int)(dilution * 1e9) ^ (int)(lidOpenSec * 10));
            BioTransfer.CheckContamination(true, lidOpenSec, seed, sample);
            sample.SyncToData();
        }

        public void SpreadSurfaceUniformly()
        {
            isSpreadPlate = true;
        }

        /// <summary>
        /// Ủ đĩa trong tủ ấm và sinh khuẩn lạc bằng StreakPlate.Colonies / Dilution.SampleColonies của Backend.
        /// </summary>
        public void Incubate(float hours, float tempC)
        {
            if (sample == null) return;
            sample.incubationHours += hours;
            int seed = Math.Abs((sample.organismId ?? "ecoli").GetHashCode() ^ 1337);

            if (streakStrokes.Count > 0)
            {
                StreakPlate.Colonies(streakStrokes, sample.cfuPerMl, sample.organismId, sample.incubationHours, tempC, seed, grownColonies);
                countedColonies = grownColonies.Count;
            }
            else if (isSpreadPlate && sample.cfuPerMl > 0.0)
            {
                double expected = sample.cfuPerMl * platedVolumeMl;
                countedColonies = Dilution.SampleColonies(expected, seed);
                grownColonies.Clear();
                double dia = ColonyGrowth.ColonyDiameterMm(sample.incubationHours, 2.0);
                var rng = new System.Random(seed);
                int visualCap = Mathf.Min(countedColonies, 320);
                for (int i = 0; i < visualCap; i++)
                {
                    grownColonies.Add(new ColonySpot
                    {
                        x = (float)(rng.NextDouble() * 70.0 + 10.0),
                        y = (float)(rng.NextDouble() * 70.0 + 10.0),
                        diameterMm = (float)dia,
                        organismId = sample.organismId
                    });
                }
            }
            sample.colonyCount = countedColonies;
        }

        public void Use(GameObject user)
        {
            if (streakStrokes.Count == 0 && !isSpreadPlate)
            {
                PerformFourZoneStreak(null, true, 2.0f);
            }
            else
            {
                isInverted = !isInverted;
                BioFeedback.PlayClick();
                BioFeedback.LogAction("dia_petri", "lat_dia_petri", $"Đã lật đĩa Petri: {(isInverted ? "Úp ngược (đúng kỹ thuật ủ)" : "Đặt ngửa")}.");
            }
        }
    }

    /// <summary>
    /// 5.2.5. Ống canh thang & Ống thạch nghiêng giữ giống — mã: ong_canh_thang, ong_thach_nghieng
    /// </summary>
    [DisallowMultipleComponent]
    public class BrothTube : MonoBehaviour, IInteractable
    {
        public string tubeKindId = "ong_canh_thang";
        public string organismId = "e_coli_k12";
        public double initialCellsPerMl = 1.0e6;
        public double incubatedHours = 0.0;
        public bool capPlacedOnBenchMistake = false;
        public bool mouthFlamed = true;
        public BioSample sample;

        void Awake()
        {
            sample = GetComponent<BioSample>();
            if (sample == null) sample = gameObject.AddComponent<BioSample>();
            UpdateGrowthState();
        }

        public double CurrentCellsPerMl
        {
            get
            {
                var strain = BioCatalogLoader.GetStrain(organismId);
                double lagH = strain != null ? strain.lag_gio : 1.0;
                double kMax = strain != null && strain.k_toi_da_cfu_ml > 0 ? strain.k_toi_da_cfu_ml : 1.0e9;
                return ColonyGrowth.PopulationAtTime(incubatedHours, initialCellsPerMl, kMax, lagH);
            }
        }

        public double CurrentOd600 => ColonyGrowth.OpticalDensity600(CurrentCellsPerMl);

        public string UseHint => $"Ống nuôi cấy ({tubeKindId}: {organismId}) [Ủ {BioFeedback.FormatVi((float)incubatedHours, "0.0")} h · Mật độ: {CurrentCellsPerMl:E2} CFU/mL · OD600 = {BioFeedback.FormatVi((float)CurrentOd600, "0.00")}] · Bấm: hơ miệng ống & ủ thêm 2 giờ";

        public void SetCapOnBenchMistake()
        {
            capPlacedOnBenchMistake = true;
            if (sample != null)
            {
                sample.isContaminated = true;
                sample.contaminationReason = "Đặt nắp ống giống xuống mặt bàn";
            }
            BioFeedback.RecordMistake(tubeKindId, "dat_nap_ong_nghiem_xuong_ban",
                "Tháo nắp ống canh khuẩn rồi đặt ngửa/úp xuống mặt bàn!",
                "Theo kỹ thuật vô trùng chuẩn, phải kẹp giữ nắp ống bằng ngón út của bàn tay cầm que cấy và hơ miệng ống qua ngọn lửa đèn cồn trước & sau khi lấy mẫu.");
        }

        public void IncubateHours(double hours)
        {
            incubatedHours += hours;
            UpdateGrowthState();
            BioFeedback.PlayClick();
            BioFeedback.LogAction(tubeKindId, "u_ong_canh_khuan",
                $"Ống {tubeKindId} ({organismId}) sau {BioFeedback.FormatVi((float)incubatedHours, "0.0")} giờ: mật độ = {CurrentCellsPerMl:E2} tế bào/mL, độ đục OD600 = {BioFeedback.FormatVi((float)CurrentOd600, "0.00")}.");
        }

        void UpdateGrowthState()
        {
            if (sample == null) return;
            sample.organismId = organismId;
            sample.sampleId = organismId;
            sample.sampleName = $"Canh khuẩn {organismId}";
            sample.cfuPerMl = CurrentCellsPerMl;
            sample.volumeMl = 5.0f;
            sample.SyncToData();
        }

        public void Use(GameObject user)
        {
            IncubateHours(2.0);
        }
    }

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

    /// <summary>
    /// 5.2.11. Đèn UV diệt khuẩn trần 254 nm (có khóa liên động: tự tắt khi mở cửa / có người trong phòng) — mã: den_uv_tran
    /// </summary>
    [DisallowMultipleComponent]
    public class UvCeilingLamp : MonoBehaviour, IInteractable
    {
        public bool isOn = false;
        public bool personInsideRoom = false;
        public int timerMinutes = 15;
        public Light uvLightVisual;

        public string UseHint => isOn
            ? $"Đèn UV diệt khuẩn trần 254 nm [ĐANG BẬT HẸN GIỜ {timerMinutes} PHÚT — Khóa liên động cửa đang kích hoạt] · Bấm: tắt đèn UV"
            : "Công tắc đèn UV diệt khuẩn trần (254 nm, ngoài cửa) · Chỉ bật được khi không có người trong phòng";

        public bool TryTurnOn(bool someoneInside)
        {
            personInsideRoom = someoneInside;
            if (someoneInside)
            {
                isOn = false;
                if (uvLightVisual != null) uvLightVisual.enabled = false;
                BioFeedback.RecordMistake("den_uv_tran", "bat_uv_tran_khi_co_nguoi",
                    "Bật đèn UV diệt khuẩn trần (254 nm) khi đang có người bên trong phòng vi sinh!",
                    "Khóa liên động an toàn đã ngắt đèn UV! Tia UV-C bước sóng 254 nm phá hủy ADN biểu mô gây viêm giác mạc cấp và bỏng da.");
                return false;
            }
            isOn = true;
            if (uvLightVisual != null) uvLightVisual.enabled = true;
            BioFeedback.PlayClick();
            BioFeedback.LogAction("den_uv_tran", "bat_den_uv_tran", $"Đã bật đèn UV diệt khuẩn trần 254 nm hẹn giờ {timerMinutes} phút (phòng không có người).");
            return true;
        }

        public void OnDoorOpenedSafetyCutoff()
        {
            if (!isOn) return;
            isOn = false;
            if (uvLightVisual != null) uvLightVisual.enabled = false;
            BioFeedback.ShowToast("Cửa phòng mở -> Khóa liên động tự động ngắt đèn UV trần 254 nm!");
            BioFeedback.LogAction("den_uv_tran", "interlock_ngat_uv_khi_mo_cua", "Khóa liên động tự động tắt đèn UV trần ngay khi mở cửa buồng đệm.");
        }

        public void Use(GameObject user)
        {
            if (isOn)
            {
                isOn = false;
                if (uvLightVisual != null) uvLightVisual.enabled = false;
                BioFeedback.PlayClick();
            }
            else
            {
                TryTurnOn(false);
            }
        }
    }

    /// <summary>
    /// 5.2.12. Buồng đệm (Airlock) 2 cửa khóa liên động + kiểm tra đồ bảo hộ PPE — mã: buong_dem
    /// </summary>
    [DisallowMultipleComponent]
    public class AirlockInterlock : MonoBehaviour, IInteractable
    {
        public AutoDoor outerDoor;
        public AutoDoor innerDoor;
        public bool outerOpen = false;
        public bool innerOpen = false;

        void Start()
        {
            if (outerDoor != null && innerDoor != null)
            {
                outerDoor.interlockWith = innerDoor;
                innerDoor.interlockWith = outerDoor;
            }
        }

        public string UseHint => $"Buồng đệm vô trùng (Airlock) [Cửa ngoài: {(outerOpen ? "MỞ" : "ĐÓNG")} · Cửa trong: {(innerOpen ? "MỞ" : "ĐÓNG")} · PPE: {(BioTransfer.PpeEquipped ? "Đã mặc đủ" : "Chưa mặc!")}] · Không bao giờ mở 2 cửa cùng lúc";

        public bool TryOpenInnerDoor()
        {
            if (outerOpen || (outerDoor != null && outerDoor.IsCurrentlyOpen))
            {
                BioFeedback.RecordMistake("buong_dem", "mo_2_cua_buong_dem_cung_luc",
                    "Cố mở cửa trong buồng đệm khi cửa ngoài hành lang chưa đóng kín!",
                    "Khóa liên động chéo (Interlock) đã chặn cửa! Nếu hai cửa mở cùng lúc, luồng khí hành lang sẽ tràn thẳng vào phòng sạch vi sinh.");
                return false;
            }
            if (!BioTransfer.PpeEquipped)
            {
                BioFeedback.ShowToast("Nhắc nhở buồng đệm: Hãy mặc áo blouse, đeo găng tay và kính bảo hộ trước khi vào phòng vi sinh!", true);
            }
            innerOpen = true;
            outerOpen = false;
            BioFeedback.PlayClick();
            BioFeedback.LogAction("buong_dem", "qua_buong_dem_dung_chuan", "Đã đóng kín cửa ngoài trước khi mở cửa trong vào phòng vi sinh (đúng khóa liên động Airlock).");
            return true;
        }

        public void Use(GameObject user)
        {
            outerOpen = false;
            TryOpenInnerDoor();
        }
    }

    /// <summary>
    /// 5.2.12. Hộp chuyển mẫu xuyên tường (Pass-box) 2 cửa khóa liên động + đèn UV 5 phút — mã: pass_box
    /// </summary>
    [DisallowMultipleComponent]
    public class PassBoxController : MonoBehaviour, IInteractable
    {
        public bool generalSideDoorOpen = false;
        public bool microbioSideDoorOpen = false;
        public bool uvSterilized5Min = false;
        public string itemInside = "Giá đĩa Petri & Môi trường LB";

        public string UseHint => $"Pass-box xuyên tường [Cửa phòng chung: {(generalSideDoorOpen ? "MỞ" : "ĐÓNG")} · Cửa vi sinh: {(microbioSideDoorOpen ? "MỞ" : "ĐÓNG")} · UV 5 phút: {(uvSterilized5Min ? "ĐÃ KHỬ TRÙNG" : "Chưa")}] · Bấm: chuyển mẫu qua Pass-box";

        public bool TryOpenBothDoorsMistake()
        {
            generalSideDoorOpen = true;
            BioFeedback.RecordMistake("pass_box", "mo_2_cua_pass_box_cung_luc",
                "Cố mở cửa Pass-box phía phòng vi sinh khi cửa phía phòng chung đang mở!",
                "Chốt khóa liên động cơ–điện từ của Pass-box khóa chặt cửa đối diện để ngăn thông khí trực tiếp giữa hai phòng.");
            return false;
        }

        public void RunTransferCycle()
        {
            generalSideDoorOpen = false;
            microbioSideDoorOpen = false;
            uvSterilized5Min = true;
            microbioSideDoorOpen = true;
            BioFeedback.PlaySuccess();
            BioFeedback.LogAction("pass_box", "chuyen_do_qua_pass_box",
                $"Đã đặt '{itemInside}' vào Pass-box, đóng kín hai cửa, chiếu UV 5 phút rồi mở cửa phía phòng vi sinh lấy ra.");
        }

        public void Use(GameObject user)
        {
            RunTransferCycle();
        }
    }

    /// <summary>
    /// 5.2.13. Bộ 6 ống nghiệm pha loãng bậc 10 (10⁻¹ .. 10⁻⁶, mỗi ống 9 mL NaCl 0,9 %) — mã: bo_ong_pha_loang
    /// Gọi trực tiếp Dilution.CarryOverMl và BioSampleData.MixInto của Backend.
    /// </summary>
    [DisallowMultipleComponent]
    public class DilutionTubeSet : MonoBehaviour, IInteractable
    {
        public readonly double[] tubeCfuPerMl = new double[6];
        public bool changedTipEveryStep = true;
        public bool vortexedEveryStep = true;

        public string UseHint => $"Dãy 6 ống pha loãng bậc 10 (10⁻¹ .. 10⁻⁶ · 9 mL NaCl 0,9 %) [Ống 10⁻⁵ = {tubeCfuPerMl[4]:E2} CFU/mL · Thay côn mỗi bước: {(changedTipEveryStep ? "CÓ" : "KHÔNG!")}] · Bấm: pha loãng 10⁻¹ → 10⁻⁶";

        /// <summary>
        /// Pha loãng nối tiếp 1 mL vào 9 mL qua 6 ống. Nếu quên thay đầu côn P1000, lượng bám dư CarryOverMl(1000) làm tăng mật độ các ống sau!
        /// </summary>
        public void PerformSerialDilution(double stockCfuPerMl, bool changeTips, bool vortexEach)
        {
            changedTipEveryStep = changeTips;
            vortexedEveryStep = vortexEach;

            double carryMl = changeTips ? 0.0 : Dilution.CarryOverMl(1000.0);
            double currentConc = stockCfuPerMl;

            for (int i = 0; i < 6; i++)
            {
                // Chuyển 1,0 mL từ bậc trước + lượng bám dư từ mẫu gốc nếu không thay đầu côn
                double transferredCells = currentConc * 1.0 + (changeTips ? 0.0 : stockCfuPerMl * carryMl);
                double nextConc = transferredCells / (9.0 + 1.0 + carryMl);
                if (!vortexEach)
                {
                    // Không vortex làm phân bố không đều (lệch tất định theo bậc)
                    nextConc *= (i % 2 == 0) ? 1.35 : 0.68;
                }
                tubeCfuPerMl[i] = nextConc;
                currentConc = nextConc;
            }

            if (!changeTips)
            {
                BioFeedback.RecordMistake("bo_ong_pha_loang", "pha_loang_khong_thay_dau_con",
                    $"Pha loãng bậc 10 từ 10⁻¹ đến 10⁻⁶ mà dùng duy nhất 1 đầu côn P1000 (dư bám {BioFeedback.FormatVi((float)(carryMl * 1000.0), "0.0")} µL)!",
                    $"Vi khuẩn đậm đặc từ ống gốc bám trên thành trong đầu côn làm ống 10⁻⁵ bị vọt lên {tubeCfuPerMl[4]:E2} CFU/mL thay vì {(stockCfuPerMl * 1e-5):E2} CFU/mL. Bắt buộc thay đầu côn vô trùng mới sau mỗi bậc pha loãng.");
            }
            else if (!vortexEach)
            {
                BioFeedback.RecordMistake("bo_ong_pha_loang", "pha_loang_quen_lac_vortex",
                    "Rút 1 mL chuyển sang ống tiếp theo mà chưa lắc đều ống trên máy Vortex!",
                    "Huyền phù vi khuẩn chưa đồng nhất làm kết quả đếm CFU/mL bị sai lệch lớn.");
            }
            else
            {
                BioFeedback.PlaySuccess();
                BioFeedback.LogAction("bo_ong_pha_loang", "pha_loang_bac_10_chuan",
                    $"Đã pha loãng chuẩn 6 bậc (10⁻¹ .. 10⁻⁶) có thay đầu côn và lắc Vortex mỗi ống: ống 10⁻⁵ đạt {tubeCfuPerMl[4]:E2} CFU/mL.");
            }
        }

        public void Use(GameObject user)
        {
            PerformSerialDilution(1.25e8, true, true);
        }
    }

    /// <summary>
    /// 5.2.14. Túi hấp hủy sinh học chịu nhiệt (Autoclave Bag) — mã: tui_hap_huy
    /// </summary>
    [DisallowMultipleComponent]
    public class AutoclaveBag : MonoBehaviour, IInteractable
    {
        public int contaminatedPlatesCount = 2;
        public bool indicatorTapeBlackStripes = false; // Đổi sang sọc đen sau khi hấp 121 °C

        public string UseHint => indicatorTapeBlackStripes
            ? $"Túi hấp hủy PP chịu nhiệt 135 °C [Đã hấp tiệt trùng 121 °C — băng chỉ thị đã hiện sọc đen ({contaminatedPlatesCount} đĩa đã bất hoạt)]"
            : $"Túi hấp hủy sinh học màu cam [Đang chứa {contaminatedPlatesCount} đĩa thạch đã nuôi cấy] · Phải đưa vào nồi hấp 121 °C trước khi thải bỏ";

        public void MarkAutoclaved()
        {
            indicatorTapeBlackStripes = true;
            BioFeedback.PlaySuccess();
            BioFeedback.LogAction("tui_hap_huy", "hap_huy_rac_vi_sinh",
                "Túi hấp hủy đã qua chu trình nồi hấp 121 °C – 20 phút: băng chỉ thị nhiệt chuyển sọc đen, toàn bộ vi sinh vật đã bị tiêu diệt.");
        }

        public void Use(GameObject user)
        {
            MarkAutoclaved();
        }
    }
}

