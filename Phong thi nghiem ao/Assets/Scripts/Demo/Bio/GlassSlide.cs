// Tách từ SlidePrepTools.cs: Unity chỉ lưu được thành phần khi tên lớp trùng tên file.
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
                    var dropper = tool.GetComponent<BioDropper>();
                    if (dropper != null)
                    {
                        if (dropper.IsEmpty) { BioFeedback.ShowToast("Ống nhỏ giọt đang trống: hút dung dịch từ chai trước", true); break; }
                        AddDrop(tool.reagentOrSampleId, tool.reagentDisplayName, dropper.SlideMolarity);
                        dropper.TakeDrop();
                    }
                    else AddDrop(tool.reagentOrSampleId, tool.reagentDisplayName);
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
}
