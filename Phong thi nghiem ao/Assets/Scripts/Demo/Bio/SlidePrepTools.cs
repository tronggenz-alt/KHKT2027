// -----------------------------------------------------------------------------
// SlidePrepTools.cs — Bộ dụng cụ làm tiêu bản hiển vi, hộp tiêu bản cố định
// và bảng điều khiển lớp học (Mục 5.1.3 – 5.1.6, 5.1.12 – 5.1.14 PROMPT_DUNG_CU_GIAO_DIEN.md).
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine;
using Khkt.Interaction;
using Khkt.Interaction.Bio;
using Khkt.Demo.Instruments;

namespace Khkt.Demo.Bio
{
    /// <summary>
    /// 5.1.3. Lam kính hiển vi (75 × 25 × 1 mm) — mã: lam_kinh
    /// </summary>
    [DisallowMultipleComponent]
    public class GlassSlide : MonoBehaviour, IInteractable, IBioTarget
    {
        public string labelText = "Chưa ghi nhãn";
        public bool hasWaterDrop = false;
        public int dropCount = 0;
        public string reagentInDrop = "nuoc_cat";
        public float saltMolarity = 0f; // M NaCl / sucrose cho bài co nguyên sinh (Osmosis)
        public bool isCracked = false;
        public float plasmolysisFraction = 0f;

        public BioSample sample;
        public Transform dropVisual;
        public Transform coverslipVisual;
        string cachedHint;

        void Awake()
        {
            sample = GetComponent<BioSample>();
            if (sample == null) sample = gameObject.AddComponent<BioSample>();
            RebuildHint();
        }

        public string UseHint => cachedHint ?? "Lam kính hiển vi";
        public string SlideTitle => labelText;

        public void RebuildHint()
        {
            if (isCracked)
            {
                cachedHint = $"Lam kính [VỠ — bỏ vào thùng vật sắc nhọn] ({labelText})";
                return;
            }
            string st = sample != null && !sample.IsEmpty ? sample.DescribeSummary() : (hasWaterDrop ? $"Đã nhỏ {dropCount} giọt ({reagentInDrop})" : "Trống");
            cachedHint = $"Lam kính [{labelText}] · {st} · Bấm: đổi nhãn / đặt lên kính hiển vi";
        }

        public void Use(GameObject user)
        {
            if (isCracked)
            {
                BioFeedback.ShowToast("Lam kính đã nứt vỡ! Hãy bỏ vào thùng vật sắc nhọn màu vàng.", true);
                return;
            }
            string[] presets = { "Biểu bì vảy hành", "Khí khổng thài lài tía", "Co nguyên sinh NaCl", "Nấm men S. cerevisiae", "Vi khuẩn nhuộm Gram", "Tế bào niêm mạc miệng" };
            int idx = Array.IndexOf(presets, labelText);
            labelText = presets[(idx + 1) % presets.Length];
            BioFeedback.PlayClick();
            BioFeedback.LogAction("lam_kinh", "dan_nhan_lam", $"Ghi nhãn đầu nhám lam kính: '{labelText}'");
            RebuildHint();
        }

        public void CrackSlide()
        {
            isCracked = true;
            BioFeedback.PlayCrack();
            BioFeedback.RecordMistake("lam_kinh", "ha_vat_kinh_manh_vo_lam",
                "Hạ vật kính 40× hoặc 100× quá nhanh làm va đập vỡ nứt lam kính và lamen!",
                "Khi dùng vật kính lớn (40×, 100×), khoảng cách làm việc rất ngắn (< 0,5 mm). Phải nhìn ngang mắt từ ngoài để hạ sát lam kính trước, sau đó ghé mắt nhìn qua thị kính và CHỈ DÙNG ỐC VI CẤP nâng dần lên để lấy nét.");
            RebuildHint();
        }

        public void AddDrop(string reagentId, string reagentName, float molarity = 0f)
        {
            hasWaterDrop = true;
            dropCount++;
            reagentInDrop = reagentId;
            saltMolarity = molarity;
            if (sample != null)
            {
                sample.volumeUl += 50f;
                if (reagentId.Contains("methylen") || reagentId.Contains("lugol"))
                {
                    sample.stainId = reagentId;
                    sample.stainName = reagentName;
                    sample.hasExcessStain = dropCount > 2;
                }
                if (molarity > 0f)
                {
                    double psiPi = Khkt.Simulation.Bio.Osmosis.SolutePotentialMpa(1.0, molarity, 25.0);
                    plasmolysisFraction = (float)(Khkt.Simulation.Bio.Osmosis.PlasmolysisPercentage(molarity) / 100.0);
                    BioFeedback.LogAction("lam_kinh", "co_nguyen_sinh",
                        $"Nhỏ dung dịch {BioFeedback.FormatVi(molarity, "0.00")} M (Ψπ = {BioFeedback.FormatVi((float)psiPi, "0.00")} MPa) → tỷ lệ co nguyên sinh = {BioFeedback.FormatVi(plasmolysisFraction * 100f, "0")}%");
                }
                else if (reagentId == "nuoc_cat" && plasmolysisFraction > 0f)
                {
                    plasmolysisFraction = 0f;
                    BioFeedback.LogAction("lam_kinh", "phan_co_nguyen_sinh", "Nhỏ nước cất kéo qua lamen → tế bào hút nước trương trở lại (phản co nguyên sinh)!");
                }
            }
            if (dropCount > 3 && sample != null && sample.hasCoverslip)
            {
                BioFeedback.RecordMistake("lam_kinh", "qua_nhieu_nuoc_trinh_lamen",
                    "Nhỏ quá nhiều nước (> 3 giọt) làm lamen nổi trôi, ảnh hiển vi bị trôi!",
                    "Dùng giấy thấm đặt sát mép lamen để rút bớt dịch thừa.");
            }
            if (dropVisual != null) dropVisual.gameObject.SetActive(true);
            RebuildHint();
        }

        public string TargetName => $"Lam kính [{labelText}]";

        public bool CanApplyTool(BioTool tool, out string promptOrReason)
        {
            if (!CanAcceptTool(tool, out promptOrReason)) return false;
            promptOrReason = GetTargetActionHint(tool);
            return true;
        }

        public float GetRequiredHoldSeconds(BioTool tool) => 0f;

        public void OnToolApplyStep(BioTool tool, float dt, float progress01) => OnToolInteractTick(tool, progress01);

        public void OnToolApplyComplete(BioTool tool) => OnToolInteractComplete(tool);

        public bool CanAcceptTool(BioTool tool, out string rejectReason)
        {
            rejectReason = "";
            if (isCracked)
            {
                rejectReason = "Lam kính đã vỡ";
                return false;
            }
            return true;
        }

        public string GetTargetActionHint(BioTool tool)
        {
            if (tool == null) return UseHint;
            return $"Dùng {tool.toolDisplayName} lên Lam kính [{labelText}]";
        }

        public void OnToolInteractStart(BioTool tool) { }
        public void OnToolInteractTick(BioTool tool, float progress01) { }

        public void OnToolInteractComplete(BioTool tool)
        {
            if (tool == null || sample == null) return;
            switch (tool.kind)
            {
                case BioToolKind.StainDropper:
                    AddDrop(tool.reagentOrSampleId, tool.reagentDisplayName);
                    break;
                case BioToolKind.Forceps:
                    if (!hasWaterDrop)
                    {
                        BioFeedback.RecordMistake("lam_kinh", "dat_mau_khong_co_giot_nuoc",
                            "Đặt mẫu biểu bì lên lam kính khô khi chưa nhỏ giọt nước/dung dịch!",
                            "Mẫu biểu bì khô sẽ nhăn, co rút và chứa đầy bọt khí. Phải nhỏ 1 giọt nước cất giữa lam kính trước.");
                    }
                    BioTransfer.PlaceSpecimenOnSlide(tool, sample,
                        string.IsNullOrEmpty(tool.reagentOrSampleId) ? "bieu_bi_vay_hanh" : tool.reagentOrSampleId,
                        string.IsNullOrEmpty(tool.reagentDisplayName) ? "Biểu bì vảy hành" : tool.reagentDisplayName);
                    break;
                case BioToolKind.DissectingNeedle:
                    BioTransfer.SpreadSpecimenWithNeedle(sample);
                    break;
                case BioToolKind.Coverslip:
                    BioTransfer.LowerCoverslip(sample, 45f);
                    if (coverslipVisual != null) coverslipVisual.gameObject.SetActive(true);
                    break;
                case BioToolKind.BlottingPaper:
                    BioTransfer.BlotSlideExcess(sample);
                    if (reagentInDrop == "nuoc_cat") plasmolysisFraction = 0f;
                    break;
                case BioToolKind.ImmersionOilBottle:
                    sample.hasImmersionOil = true;
                    BioFeedback.PlayBubblePop();
                    BioFeedback.LogAction("lo_dau_soi", "nho_dau_soi_lam", "Đã nhỏ 1 giọt dầu soi (n = 1,515) lên lamen để soi vật kính 100×.");
                    break;
            }
            RebuildHint();
        }

        public void OnToolInteractCancel(BioTool tool, float progressAtCancel)
        {
            if (tool != null && tool.kind == BioToolKind.Coverslip && progressAtCancel < 0.7f)
            {
                // Buông nút sớm khi đang hạ lamen -> rơi thẳng tạo bọt khí (5.1.3 bước 4)
                BioTransfer.LowerCoverslip(sample, 85f);
                if (coverslipVisual != null) coverslipVisual.gameObject.SetActive(true);
                RebuildHint();
            }
        }
    }

    /// <summary>
    /// 5.1.3. Lamen (lá kính đậy 22 × 22 × 0,17 mm) — mã: lamen
    /// </summary>
    [DisallowMultipleComponent]
    public class CoverSlip : MonoBehaviour, IInteractable
    {
        public BioTool bioTool;
        public bool isBroken = false;

        void Awake()
        {
            bioTool = GetComponent<BioTool>();
            if (bioTool == null) bioTool = gameObject.AddComponent<BioTool>();
            bioTool.toolId = "lamen";
            bioTool.toolDisplayName = "Lamen (22 × 22 × 0,17 mm)";
            bioTool.kind = BioToolKind.Coverslip;
            bioTool.holdDurationSeconds = 0.6f; // Giữ 0,6 s để hạ nghiêng 45° -> 0° không bọt khí
        }

        public string UseHint => isBroken
            ? "Lamen bị vỡ — bỏ vào thùng vật sắc nhọn"
            : "Lamen (22 × 22 × 0,17 mm) · Đưa tới lam kính và GIỮ 0,6 s để hạ nghiêng 45° (bấm nhanh sẽ sinh bọt khí)";

        public void Use(GameObject user)
        {
            var slide = FindNearestSlide(transform.position, 0.35f);
            if (slide != null)
            {
                // Bấm nhanh (không giữ) -> thả rơi thẳng góc 85° tạo bọt khí (5.1.3 bước 4)
                BioTransfer.LowerCoverslip(slide.sample, 85f);
                slide.RebuildHint();
            }
            else
            {
                BioFeedback.ShowToast("Hãy đưa lamen tới gần lam kính có mẫu (giữ 0,6 s để hạ góc 45° không bọt khí).");
            }
        }

        public void ApplySmooth45DegreeDrop(GlassSlide slide)
        {
            if (slide == null) return;
            BioTransfer.LowerCoverslip(slide.sample, 45f);
            slide.RebuildHint();
        }

        public static GlassSlide FindNearestSlide(Vector3 pos, float maxDist)
        {
            var slides = FindObjectsByType<GlassSlide>(FindObjectsSortMode.None);
            GlassSlide best = null;
            float bestD = maxDist;
            for (int i = 0; i < slides.Length; i++)
            {
                float d = Vector3.Distance(pos, slides[i].transform.position);
                if (d < bestD) { bestD = d; best = slides[i]; }
            }
            return best;
        }
    }

    /// <summary>
    /// 5.1.4. Kim mũi mác — mã: kim_mui_mac
    /// </summary>
    [DisallowMultipleComponent]
    public class DissectingNeedle : MonoBehaviour, IInteractable
    {
        public BioTool bioTool;

        void Awake()
        {
            bioTool = GetComponent<BioTool>();
            if (bioTool == null) bioTool = gameObject.AddComponent<BioTool>();
            bioTool.toolId = "kim_mui_mac";
            bioTool.toolDisplayName = "Kim mũi mác";
            bioTool.kind = BioToolKind.DissectingNeedle;
            bioTool.holdDurationSeconds = 0.4f;
        }

        public string UseHint => "Kim mũi mác · Dàn phẳng biểu bì trên lam kính và đỡ lamen hạ góc 45°";

        public void Use(GameObject user)
        {
            var slide = CoverSlip.FindNearestSlide(transform.position, 0.4f);
            if (slide != null && slide.sample != null)
            {
                BioTransfer.SpreadSpecimenWithNeedle(slide.sample);
                slide.RebuildHint();
            }
            else
            {
                BioFeedback.ShowToast("Đưa kim mũi mác tới lam kính để dàn phẳng mẫu biểu bì.");
            }
        }
    }

    /// <summary>
    /// 5.1.4. Kẹp (panh) mũi nhọn 12 cm — mã: kep_panh
    /// </summary>
    [DisallowMultipleComponent]
    public class Forceps : MonoBehaviour, IInteractable
    {
        public BioTool bioTool;
        public string heldSpecimenId = "";
        public string heldSpecimenName = "";
        public bool peeledTooFast = false;

        void Awake()
        {
            bioTool = GetComponent<BioTool>();
            if (bioTool == null) bioTool = gameObject.AddComponent<BioTool>();
            bioTool.toolId = "kep_panh";
            bioTool.toolDisplayName = "Kẹp (panh) mũi nhọn";
            bioTool.kind = BioToolKind.Forceps;
            bioTool.holdDurationSeconds = 0.8f; // Giữ 0,8 s để bóc lớp biểu bì mỏng 1 lớp tế bào
        }

        public string UseHint => string.IsNullOrEmpty(heldSpecimenId)
            ? "Kẹp (panh) mũi nhọn · Giữ 0,8 s trên vảy hành/lá thài lài để bóc biểu bì mỏng"
            : $"Kẹp đang giữ: {heldSpecimenName}{(peeledTooFast ? " [Dày/rách do bóc vội]" : " [Mỏng 1 lớp chuẩn]")} · Bấm lam kính để đặt";

        public void PeelSpecimen(string specId, string specName, float holdSeconds)
        {
            heldSpecimenId = specId;
            heldSpecimenName = specName;
            peeledTooFast = holdSeconds < 0.75f;
            if (bioTool != null)
            {
                bioTool.reagentOrSampleId = specId;
                bioTool.reagentDisplayName = specName;
            }
            if (peeledTooFast)
            {
                BioFeedback.RecordMistake("kep_panh", "boc_bieu_bi_qua_nhanh",
                    $"Bóc biểu bì quá nhanh ({BioFeedback.FormatVi(holdSeconds, "0.0")} s < 0,8 s) làm biểu bì bị rách và lẫn nhiều lớp mô thịt!",
                    "Khi soi hiển vi, các lớp tế bào chồng lên nhau làm ảnh tối và không thấy rõ từng tế bào. Phải giữ nhẹ nhàng ≥ 0,8 s để tách đúng 1 lớp tế bào biểu bì.");
            }
            else
            {
                BioFeedback.PlaySuccess();
                BioFeedback.LogAction("kep_panh", "boc_bieu_bi_mong", $"Đã bóc lớp biểu bì mỏng '{specName}' (1 lớp tế bào).");
            }
        }

        public void Use(GameObject user)
        {
            var slide = CoverSlip.FindNearestSlide(transform.position, 0.45f);
            if (slide != null && !string.IsNullOrEmpty(heldSpecimenId))
            {
                slide.OnToolInteractComplete(bioTool);
                if (slide.sample != null) slide.sample.isSpreadFlat = !peeledTooFast;
                heldSpecimenId = "";
                heldSpecimenName = "";
            }
            else if (string.IsNullOrEmpty(heldSpecimenId))
            {
                PeelSpecimen("bieu_bi_vay_hanh", "Biểu bì vảy hành tím", 0.9f);
            }
        }
    }

    /// <summary>
    /// 5.1.4. Dao lam — mã: dao_lam
    /// </summary>
    [DisallowMultipleComponent]
    public class RazorBlade : MonoBehaviour, IInteractable
    {
        public BioTool bioTool;
        public bool scoredOnionGrid = false;
        public bool placedSafelyInTray = true;

        void Awake()
        {
            bioTool = GetComponent<BioTool>();
            if (bioTool == null) bioTool = gameObject.AddComponent<BioTool>();
            bioTool.toolId = "dao_lam";
            bioTool.toolDisplayName = "Dao lam rạch mẫu";
            bioTool.kind = BioToolKind.Scalpel;
            bioTool.holdDurationSeconds = 0.5f;
        }

        public string UseHint => scoredOnionGrid
            ? "Dao lam (đã rạch ô vuông 1 cm trên vảy hành) · Đặt lại vào hộp trên khay an toàn"
            : "Dao lam · Bấm để rạch ô vuông 1 × 1 cm trên mặt vảy hành";

        public void Use(GameObject user)
        {
            scoredOnionGrid = true;
            BioFeedback.PlayClick();
            BioFeedback.LogAction("dao_lam", "rach_o_vuong_hanh", "Đã dùng dao lam rạch các ô vuông 1 × 1 cm trên mặt trong vảy hành tím.");
        }

        public void CheckSafePlacement(bool inTray)
        {
            placedSafelyInTray = inTray;
            if (!inTray)
            {
                BioFeedback.RecordMistake("dao_lam", "dat_dao_lam_ngoai_khay",
                    "Đặt dao lam trần ngoài khay dụng cụ!",
                    "Lưỡi dao lam rất sắc, phải đặt ngay ngắn trong hộp/khay quy định sau khi rạch mẫu hoặc bỏ vào thùng vật sắc nhọn màu vàng.");
            }
        }
    }

    /// <summary>
    /// 5.1.4. Kéo mổ nhỏ — mã: keo_nho
    /// </summary>
    [DisallowMultipleComponent]
    public class Scissors : MonoBehaviour, IInteractable
    {
        public BioTool bioTool;

        void Awake()
        {
            bioTool = GetComponent<BioTool>();
            if (bioTool == null) bioTool = gameObject.AddComponent<BioTool>();
            bioTool.toolId = "keo_nho";
            bioTool.toolDisplayName = "Kéo nhỏ";
            bioTool.kind = BioToolKind.Scalpel;
        }

        public string UseHint => "Kéo mổ nhỏ · Cắt lá rong đuôi chó / lá thài lài tía thành mẩu nhỏ";

        public void Use(GameObject user)
        {
            BioFeedback.PlayClick();
            BioFeedback.LogAction("keo_nho", "cat_mau_la", "Đã dùng kéo nhỏ cắt cành/lá mẫu thực vật.");
        }
    }

    /// <summary>
    /// 5.1.5. Lọ thuốc thử nhỏ giọt trên khay bàn (nước cất, xanh methylen 1%, Lugol, NaCl, H2O2 3%) — mã: lo_thuoc_thu_nho_giot
    /// </summary>
    [DisallowMultipleComponent]
    public class ReagentDropperBottle : MonoBehaviour, IInteractable
    {
        public string reagentId = "xanh_methylen";
        public string reagentName = "Xanh methylen 1 %";
        public float molarity = 0f;
        public float dropVolumeMl = 0.05f;
        public int dropsDispensed = 0;
        public BioTool bioTool;

        void Awake()
        {
            bioTool = GetComponent<BioTool>();
            if (bioTool == null) bioTool = gameObject.AddComponent<BioTool>();
            bioTool.toolId = "lo_thuoc_thu_nho_giot";
            bioTool.toolDisplayName = reagentName;
            bioTool.kind = BioToolKind.StainBottle;
            bioTool.reagentOrSampleId = reagentId;
            bioTool.reagentDisplayName = reagentName;
        }

        public string UseHint => $"Lọ nhỏ giọt {reagentName} (0,05 mL/giọt · đã nhỏ {dropsDispensed} giọt) · Bấm: nhỏ 1 giọt lên lam/ống gần nhất";

        public void Use(GameObject user)
        {
            dropsDispensed++;
            var slide = CoverSlip.FindNearestSlide(transform.position, 0.5f);
            if (slide != null)
            {
                slide.AddDrop(reagentId, reagentName, molarity);
            }
            BioFeedback.PlayBubblePop();
            BioFeedback.LogAction("lo_thuoc_thu_nho_giot", $"nho_giot_{reagentId}",
                $"Đã nhỏ 1 giọt ({BioFeedback.FormatVi(dropVolumeMl, "0.00")} mL) {reagentName} (tổng {dropsDispensed} giọt).");
        }
    }

    /// <summary>
    /// 5.1.6. Giấy thấm — mã: giay_tham
    /// </summary>
    [DisallowMultipleComponent]
    public class BlottingPaper : MonoBehaviour, IInteractable
    {
        public BioTool bioTool;
        public bool isUsedWet = false;

        void Awake()
        {
            bioTool = GetComponent<BioTool>();
            if (bioTool == null) bioTool = gameObject.AddComponent<BioTool>();
            bioTool.toolId = "giay_tham";
            bioTool.toolDisplayName = "Giấy thấm";
            bioTool.kind = BioToolKind.BlottingPaper;
        }

        public string UseHint => isUsedWet
            ? "Giấy thấm (đã thấm ướt dịch thừa) · Bỏ vào thùng rác"
            : "Giấy thấm · Đặt ở mép lamen để rút dịch thừa / kéo dung dịch muối hoặc nước cất qua tiêu bản";

        public void Use(GameObject user)
        {
            isUsedWet = true;
            var slide = CoverSlip.FindNearestSlide(transform.position, 0.5f);
            if (slide != null)
            {
                slide.OnToolInteractComplete(bioTool);
            }
            else
            {
                BioFeedback.PlayClick();
                BioFeedback.LogAction("giay_tham", "tham_dich_lamen", "Đã dùng giấy thấm kéo dung dịch qua mép lamen.");
            }
        }
    }

    /// <summary>
    /// 5.1.6. Giấy lau kính chuyên dụng — mã: giay_lau_kinh
    /// </summary>
    [DisallowMultipleComponent]
    public class LensTissue : MonoBehaviour, IInteractable
    {
        public BioTool bioTool;
        public bool isUsed = false;

        void Awake()
        {
            bioTool = GetComponent<BioTool>();
            if (bioTool == null) bioTool = gameObject.AddComponent<BioTool>();
            bioTool.toolId = "giay_lau_kinh";
            bioTool.toolDisplayName = "Giấy lau kính chuyên dụng";
            bioTool.kind = BioToolKind.LensPaper;
        }

        public string UseHint => "Giấy lau kính chuyên dụng · Dùng lau sạch dầu soi trên vật kính 100× và 40×";

        public void Use(GameObject user)
        {
            isUsed = true;
            var scopes = FindObjectsByType<MicroscopeController>(FindObjectsSortMode.None);
            for (int i = 0; i < scopes.Length; i++)
            {
                if (Vector3.Distance(transform.position, scopes[i].transform.position) < 1.2f)
                {
                    scopes[i].CleanObjectiveWithLensTissue();
                    return;
                }
            }
            BioFeedback.PlayClick();
            BioFeedback.LogAction("giay_lau_kinh", "lau_vat_kinh", "Đã dùng giấy lau kính chuyên dụng lau sạch vật kính hiển vi.");
        }
    }

    /// <summary>
    /// 5.1.12. Lọ dầu soi kính (dầu cedar / tổng hợp, n = 1,515) — mã: lo_dau_soi
    /// </summary>
    [DisallowMultipleComponent]
    public class ImmersionOil : MonoBehaviour, IInteractable
    {
        public const float RefractiveIndex = 1.515f;
        public BioTool bioTool;

        void Awake()
        {
            bioTool = GetComponent<BioTool>();
            if (bioTool == null) bioTool = gameObject.AddComponent<BioTool>();
            bioTool.toolId = "lo_dau_soi";
            bioTool.toolDisplayName = "Lọ dầu soi kính (n = 1,515)";
            bioTool.kind = BioToolKind.ImmersionOilBottle;
        }

        public string UseHint => "Lọ dầu soi kính (n = 1,515) · Bấm để nhỏ 1 giọt dầu lên tiêu bản trước khi xoay vật kính 100×";

        public void Use(GameObject user)
        {
            var scopes = FindObjectsByType<MicroscopeController>(FindObjectsSortMode.None);
            for (int i = 0; i < scopes.Length; i++)
            {
                if (Vector3.Distance(transform.position, scopes[i].transform.position) < 1.5f)
                {
                    scopes[i].ApplyImmersionOil();
                    return;
                }
            }
            var slide = CoverSlip.FindNearestSlide(transform.position, 0.6f);
            if (slide != null && slide.sample != null)
            {
                slide.sample.hasImmersionOil = true;
                slide.RebuildHint();
            }
            BioFeedback.PlayBubblePop();
            BioFeedback.LogAction("lo_dau_soi", "nho_dau_soi", "Đã nhỏ dầu soi chiết suất n = 1,515 lên lam kính.");
        }
    }

    /// <summary>
    /// 5.1.13. Hộp tiêu bản cố định (10 khe, nắp bản lề) — mã: hop_tieu_ban
    /// </summary>
    [DisallowMultipleComponent]
    public class SlideBox : MonoBehaviour, IInteractable
    {
        public static readonly string[] FixedSlides =
        {
            "co_dinh_phe_nang",
            "co_dinh_nephron",
            "co_dinh_synapse",
            "co_dinh_sarcomere",
            "co_dinh_da_day",
            "co_dinh_gan",
            "co_dinh_mau_nguoi",
            "co_dinh_nguyen_phan_hanh"
        };

        public bool isOpen = false;
        public int selectedIndex = 0;
        public BioHinge lidHinge;

        public string UseHint
        {
            get
            {
                string id = FixedSlides[selectedIndex % FixedSlides.Length];
                var spec = BioCatalogLoader.GetSlide(id);
                string title = spec != null ? spec.ten : id;
                return isOpen
                    ? $"Hộp tiêu bản cố định [Đang chọn khe {selectedIndex + 1}/8: {title}] · Bấm: lấy đặt lên kính hiển vi gần nhất / đổi khe"
                    : "Hộp tiêu bản cố định (8 tiêu bản H&E / nguyên phân) · Bấm để mở nắp hộp";
            }
        }

        public void Use(GameObject user)
        {
            if (!isOpen)
            {
                isOpen = true;
                if (lidHinge != null) lidHinge.SetOpen(true);
                BioFeedback.PlayClick();
                BioFeedback.LogAction("hop_tieu_ban", "mo_hop_tieu_ban", "Đã mở nắp hộp tiêu bản cố định (8 tiêu bản chuẩn).");
                return;
            }

            string slideId = FixedSlides[selectedIndex % FixedSlides.Length];
            var spec = BioCatalogLoader.GetSlide(slideId);
            string slideTitle = spec != null ? spec.ten : slideId;

            var scopes = FindObjectsByType<MicroscopeController>(FindObjectsSortMode.None);
            MicroscopeController nearest = null;
            float bestD = 3.5f;
            for (int i = 0; i < scopes.Length; i++)
            {
                float d = Vector3.Distance(transform.position, scopes[i].transform.position);
                if (d < bestD) { bestD = d; nearest = scopes[i]; }
            }
            if (nearest != null)
            {
                nearest.LoadSlideById(slideId);
            }

            BioFeedback.PlaySuccess();
            BioFeedback.LogAction("hop_tieu_ban", "lay_tieu_ban_co_dinh",
                $"Đã lấy tiêu bản cố định '{slideTitle}' ({slideId}) từ khe {selectedIndex + 1} đặt lên bàn kính hiển vi.");
            selectedIndex = (selectedIndex + 1) % FixedSlides.Length;
        }
    }

    /// <summary>
    /// 5.1.14. Bảng điều khiển lớp học trên bàn giáo viên — mã: bang_dieu_khien_lop
    /// </summary>
    [DisallowMultipleComponent]
    public class ClassroomConsole : MonoBehaviour, IInteractable
    {
        public int currentExperimentIndex = 0;
        public bool roomLightsOn = true;
        public ClassroomProgram program = new ClassroomProgram();

        void Awake()
        {
            if (program == null) program = new ClassroomProgram();
            program.console = this;
        }

        public string UseHint
        {
            get
            {
                var exp = GetCurrentExperiment();
                string title = exp != null ? $"{exp.ma} – {exp.ten}" : "te_bao_hanh";
                return $"Bảng điều khiển lớp học [Bài đang phát cho 9 bàn: {title}] · Bấm: chuyển bài thí nghiệm cho cả lớp";
            }
        }

        public BioExperimentEntry GetCurrentExperiment()
        {
            BioCatalogLoader.EnsureLoaded();
            if (BioCatalogLoader.Experiments.Count == 0) return null;
            return BioCatalogLoader.Experiments[currentExperimentIndex % BioCatalogLoader.Experiments.Count];
        }

        public void Use(GameObject user)
        {
            BioCatalogLoader.EnsureLoaded();
            if (BioCatalogLoader.Experiments.Count > 0)
            {
                currentExperimentIndex = (currentExperimentIndex + 1) % BioCatalogLoader.Experiments.Count;
            }
            var exp = GetCurrentExperiment();
            string code = exp != null ? exp.ma : "te_bao_hanh";
            string name = exp != null ? exp.ten : "Quan sát tế bào biểu bì vảy hành";

            // Đồng bộ mọi màn hình bàn học sinh (BioBenchScreenController)
            var screens = FindObjectsByType<BioBenchScreenController>(FindObjectsSortMode.None);
            for (int i = 0; i < screens.Length; i++)
            {
                screens[i].SetExperiment(code);
            }

            BioFeedback.PlaySuccess();
            BioFeedback.LogAction("bang_dieu_khien_lop", "chon_bai_thi_nghiem",
                $"Giáo viên phát bài thí nghiệm '{name}' ({code}) xuống toàn bộ {screens.Length} màn hình bàn học sinh.");
        }
    }

    /// <summary>
    /// Chương trình màn hình vận hành Bảng điều khiển lớp học (5.1.14) — kế thừa InstrumentProgram.
    /// </summary>
    public class ClassroomProgram : InstrumentProgram
    {
        public override string Code => "bang_dieu_khien_lop";
        public ClassroomConsole console;

        public override void Build()
        {
            if (Doc == null || Doc.Steps.Count == 0)
                Doc = BioCatalogLoader.LoadMachineDoc(Code);

            S.AddButton("Đổi bài tiếp theo", 0, 0, () =>
            {
                if (console != null) console.Use(null);
                S.Done("chon_bai");
            });

            S.AddButton("Đèn phòng (Bật/Giảm)", 1, 0, () =>
            {
                if (console != null) console.roomLightsOn = !console.roomLightsOn;
                S.Done("dieu_khien_den");
            });

            S.AddButton("Đồng bộ 9 bàn", 2, 0, () =>
            {
                if (console != null) console.Use(null);
                S.Say("Đã đồng bộ bài thí nghiệm tới 9/9 bàn học sinh.", 1);
            });

            S.SetPlotAxes("TIẾN ĐỘ THÍ NGHIỆM 9 BÀN HỌC SINH (%)", "Bàn", "Tiến độ (%)", "1", "9", "0", "100");
            Refresh();
        }

        public override void Refresh()
        {
            var exp = console != null ? console.GetCurrentExperiment() : null;
            string code = exp != null ? exp.ma : "te_bao_hanh";
            string title = exp != null ? exp.ten : "Quan sát tế bào biểu bì vảy hành";
            int lop = exp != null ? exp.lop : 10;

            S.SetResults(
                $"<b>BẢNG ĐIỀU KHIỂN LỚP HỌC (9 BÀN)</b>\n" +
                $"• Bài đang phát: <b>{code}</b> (Lớp {lop})\n" +
                $"• Tên bài: <b>{title}</b>\n" +
                $"• Đèn phòng: <b>{(console == null || console.roomLightsOn ? "BẬT (100%)" : "GIẢM SÁNG SOI KÍNH")}</b>\n" +
                $"• Trạng thái: <b>9/9 bàn học sinh đã đồng bộ</b>");
        }

        public override void DrawSchematic(Painter p, float time)
        {
            p.Clear(new Color32(18, 32, 48, 255));
            // Vẽ lưới 9 bàn học sinh
            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 3; c++)
                {
                    p.FillRect(40 + c * 80, 30 + r * 50, 60, 35, new Color32(35, 75, 110, 255));
                    p.FillRect(45 + c * 80, 35 + r * 50, 50, 8, new Color32(80, 210, 160, 255));
                }
            }
        }

        public override void DrawPlot(Painter p)
        {
            p.Clear(new Color32(12, 22, 34, 255));
            // Vẽ 9 cột tiến độ
            for (int i = 0; i < 9; i++)
            {
                int h = 40 + (i * 13) % 90;
                p.FillRect(40 + i * 26, 30, 18, h, new Color32(75, 195, 145, 230));
            }
        }
    }
}

