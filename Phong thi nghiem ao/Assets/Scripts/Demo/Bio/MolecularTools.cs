// -----------------------------------------------------------------------------
// MolecularTools.cs — Toàn bộ dụng cụ và thiết bị Phòng Sinh học phân tử
// (Mục 5.4 PROMPT_DUNG_CU_GIAO_DIEN.md & Mục 2, 4 HOP_DONG_GIAO_DIEN_BACKEND.md).
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
    /// 5.4.1. Micropipet tự động 2 nấc piston (P10, P200, P1000) — mã: micropipet_p10, micropipet_p200, micropipet_p1000
    /// Dụng cụ quan trọng nhất của Phòng Sinh học phân tử, tuân thủ chuẩn ISO 8655.
    /// </summary>
    [DisallowMultipleComponent]
    public class Micropipette : MonoBehaviour, IInteractable, IScrollAdjustable
    {
        public string pipetteToolId = "micropipet_p200"; // micropipet_p10 | micropipet_p200 | micropipet_p1000
        public float minUl = 20.0f;
        public float maxUl = 200.0f;
        public float stepUl = 0.2f;
        public float volumeSetUl = 100.0f;

        public bool tipAttached = false;
        public string tipType = ""; // p10_trang | p200_vang | p1000_xanh
        public int plungerStage = 0; // 0: thả, 1: nấc 1 (định lượng), 2: nấc 2 (thổi kiệt / blow-out)
        public bool contaminated = false;
        public bool damagedByHorizontalLiquid = false;
        public bool aspiratedWithAirBubble = false;

        public BioSample tipContent;
        string cachedHint;

        void Awake()
        {
            ConfigureRangeById(pipetteToolId);
            tipContent = GetComponent<BioSample>();
            if (tipContent == null) tipContent = gameObject.AddComponent<BioSample>();
            RebuildHint();
        }

        public void ConfigureRangeById(string id)
        {
            pipetteToolId = id;
            if (id == "micropipet_p10")
            {
                minUl = 0.5f; maxUl = 10.0f; stepUl = 0.02f;
                if (volumeSetUl < minUl || volumeSetUl > maxUl) volumeSetUl = 5.0f;
            }
            else if (id == "micropipet_p1000")
            {
                minUl = 100.0f; maxUl = 1000.0f; stepUl = 1.0f;
                if (volumeSetUl < minUl || volumeSetUl > maxUl) volumeSetUl = 500.0f;
            }
            else
            {
                pipetteToolId = "micropipet_p200";
                minUl = 20.0f; maxUl = 200.0f; stepUl = 0.2f;
                if (volumeSetUl < minUl || volumeSetUl > maxUl) volumeSetUl = 100.0f;
            }
            RebuildHint();
        }

        public string ExpectedTipColorName =>
            pipetteToolId == "micropipet_p10" ? "côn trắng (P10)" :
            pipetteToolId == "micropipet_p1000" ? "côn xanh (P1000)" : "côn vàng (P200)";

        public string UseHint => cachedHint ?? "Micropipet";

        public void RebuildHint()
        {
            string model = pipetteToolId == "micropipet_p10" ? "P10" : (pipetteToolId == "micropipet_p1000" ? "P1000" : "P200");
            string tipStr = tipAttached ? ExpectedTipColorName : "Chưa gắn côn";
            string volStr = BioFeedback.FormatVi(volumeSetUl, pipetteToolId == "micropipet_p10" ? "0.00" : "0.0");
            string contentStr = (tipContent != null && tipContent.volumeUl > 0.01f)
                ? $" · Đang chứa {BioFeedback.FormatVi(tipContent.volumeUl, "0.1")} µL {tipContent.sampleName}"
                : "";
            cachedHint = $"{model} · {volStr} µL · {tipStr}{contentStr}{(damagedByHorizontalLiquid ? " · [HỎNG CƠ CẤU — CẦN BẢO TRÌ]" : "")} · Giữ để hút / nhả · Con lăn: chỉnh µL";
        }

        public void OnScroll(float delta)
        {
            if (Mathf.Abs(delta) < 0.001f) return;
            float candidate = volumeSetUl + Mathf.Sign(delta) * stepUl * 5f;
            SetVolume(candidate);
        }

        public bool SetVolume(float targetUl)
        {
            if (targetUl < minUl - 0.001f || targetUl > maxUl + 0.001f)
            {
                BioFeedback.RecordMistake(pipetteToolId, "van_the_tich_vuot_dai_iso8655",
                    $"Vặn thể tích {BioFeedback.FormatVi(targetUl, "0.1")} µL vượt ngoài dải định mức [{BioFeedback.FormatVi(minUl, "0.1")} – {BioFeedback.FormatVi(maxUl, "0.0")} µL] của {pipetteToolId}!",
                    "Vượt dải – hỏng cơ cấu bánh răng và lò xo định lượng theo chuẩn ISO 8655!");
                volumeSetUl = Mathf.Clamp(targetUl, minUl, maxUl);
                RebuildHint();
                return false;
            }
            volumeSetUl = (float)Math.Round(targetUl / stepUl) * stepUl;
            BioFeedback.PlayClick();
            RebuildHint();
            return true;
        }

        public bool AttachTip(TipBox box)
        {
            if (box != null && box.tipSizeId != pipetteToolId)
            {
                BioFeedback.RecordMistake(pipetteToolId, "gan_dau_con_sai_co",
                    $"Cố gắn đầu côn của hộp '{box.tipSizeId}' vào thân '{pipetteToolId}'!",
                    "Đầu côn sai kích cỡ sẽ không kín khí hoặc rơi ra khi hút. P10 dùng côn trắng, P200 dùng côn vàng, P1000 dùng côn xanh lam.");
                return false;
            }
            if (box != null && !box.TakeOneTip()) return false;
            tipAttached = true;
            tipType = ExpectedTipColorName;
            if (tipContent != null) tipContent.ClearSample();
            BioFeedback.PlayClick();
            BioFeedback.LogAction(pipetteToolId, "gan_dau_con", $"Đã ấn gắn chặt {ExpectedTipColorName} vô trùng vào {pipetteToolId} (tiếng 'cạch').");
            RebuildHint();
            return true;
        }

        /// <summary>
        /// Hút chất lỏng từ vật chứa: kiểm tra đủ 3 lỗi (ấn quá nấc 2 khi hút, đầu côn không ngập dịch sinh bọt khí, nghiêng > 20°).
        /// </summary>
        public bool Aspirate(BioSample source, int pressStage = 1, bool tipSubmerged = true, float tiltAngleDeg = 0f)
        {
            if (!tipAttached)
            {
                BioFeedback.RecordMistake(pipetteToolId, "hut_khong_gan_dau_con",
                    "Hút chất lỏng khi CHƯA gắn đầu côn!",
                    "Dịch tràn thẳng vào thân piston gây nhiễm bẩn và hỏng đệm kín khí của micropipet.");
                return false;
            }

            plungerStage = pressStage;
            float actualAspiratedUl = (float)PipetteAccuracy.ActualUl(volumeSetUl, maxUl, pressStage, tiltAngleDeg, tipSubmerged);

            // Lỗi 1: Ấn quá tới nấc 2 (blow-out) trước khi hút -> hút thừa ~12%
            if (pressStage >= 2)
            {
                BioFeedback.RecordMistake(pipetteToolId, "an_nac_2_khi_hut_dich",
                    $"Ấn piston xuống tận NẤC 2 (nấc thổi kiệt) khi hút làm thể tích thực tế vọt lên {BioFeedback.FormatVi(actualAspiratedUl, "0.1")} µL (so với {BioFeedback.FormatVi(volumeSetUl, "0.1")} µL)!",
                    "Khi hút mẫu chỉ được ấn piston tới NẤC 1 rồi thả từ từ. Nấc 2 chỉ dùng khi NHẢ mẫu để đẩy hết giọt cuối cùng ra khỏi đầu côn.");
            }

            // Lỗi 2: Đầu côn không ngập trong chất lỏng -> hút phải bọt khí, thể tích thiếu
            if (!tipSubmerged)
            {
                aspiratedWithAirBubble = true;
                BioFeedback.RecordMistake(pipetteToolId, "dau_con_khong_ngap_chat_long_bot_khi",
                    $"Đầu côn không ngập dưới mặt thoáng dung dịch nên hút phải bọt khí (chỉ lấy được {BioFeedback.FormatVi(actualAspiratedUl, "0.1")} µL)!",
                    "Phải nhúng đầu côn ngập 2–4 mm dưới bề mặt chất lỏng trong suốt quá trình thả piston.");
            }
            else
            {
                aspiratedWithAirBubble = false;
            }

            // Lỗi 3: Cầm nghiêng > 20° khi hút -> sai số cột áp thủy tĩnh
            if (tiltAngleDeg > 20f)
            {
                BioFeedback.RecordMistake(pipetteToolId, "nghieng_micropipet_qua_20_do",
                    $"Cầm nghiêng micropipet góc {BioFeedback.FormatVi(tiltAngleDeg, "0")}° (> 20°) khi hút mẫu làm tăng sai số hệ thống!",
                    "Theo chuẩn ISO 8655, phải giữ micropipet thẳng đứng (góc nghiêng ≤ 20°) khi hút chất lỏng.");
            }

            if (source != null && tipContent != null)
            {
                BioTransfer.Move(source, tipContent, actualAspiratedUl * 0.001);
                tipContent.volumeUl = actualAspiratedUl;
                tipContent.volumeMl = actualAspiratedUl * 0.001f;
            }
            else if (tipContent != null)
            {
                tipContent.sampleId = "mau_pcr";
                tipContent.sampleName = "Dung dịch phản ứng";
                tipContent.volumeUl = actualAspiratedUl;
                tipContent.volumeMl = actualAspiratedUl * 0.001f;
            }

            plungerStage = 0;
            BioFeedback.PlayClick();
            BioFeedback.LogAction(pipetteToolId, "hut_mau_micropipet",
                $"Đã hút {BioFeedback.FormatVi(actualAspiratedUl, "0.1")} µL vào {ExpectedTipColorName}.");
            RebuildHint();
            return true;
        }

        /// <summary>
        /// Nhả chất lỏng sang vật đích (nấc 1 nhả 96%, nấc 2 thổi kiệt 100%).
        /// </summary>
        public bool Dispense(BioSample target, int releaseStage = 2)
        {
            if (!tipAttached || tipContent == null || tipContent.volumeUl <= 0.001f)
            {
                BioFeedback.ShowToast("Đầu côn chưa có chất lỏng để nhả.", true);
                return false;
            }

            plungerStage = releaseStage;
            float currentUl = tipContent.volumeUl;
            float residualFrac = (float)PipetteAccuracy.ResidualFraction(releaseStage);
            float dispensedUl = currentUl * (1.0f - residualFrac);

            if (releaseStage < 2)
            {
                BioFeedback.RecordMistake(pipetteToolId, "nha_mau_khong_an_het_nac_2",
                    "Khi nhả mẫu chỉ ấn tới nấc 1 mà không ấn tiếp xuống nấc 2 (blow-out) nên còn sót ~4% dịch trong đầu côn!",
                    "Khi nhả dịch, phải ấn qua nấc 1 xuống tận nấc 2 để đẩy hết giọt chất lỏng bám ở mũi côn.");
            }

            if (target != null)
            {
                BioTransfer.Move(tipContent, target, dispensedUl * 0.001);
            }
            tipContent.volumeUl = Mathf.Max(0f, currentUl - dispensedUl);
            tipContent.volumeMl = tipContent.volumeUl * 0.001f;
            if (tipContent.volumeUl <= 0.01f) tipContent.ClearSample();

            plungerStage = 0;
            BioFeedback.PlayClick();
            BioFeedback.LogAction(pipetteToolId, "nha_mau_micropipet",
                $"Đã nhả {BioFeedback.FormatVi(dispensedUl, "0.1")} µL từ {pipetteToolId} (nấc {releaseStage}).");
            RebuildHint();
            return true;
        }

        /// <summary>
        /// Lỗi 4 (5.4.1 bước 6): Đặt micropipet nằm ngang khi còn chất lỏng trong đầu côn -> dịch chảy ngược vào thân làm hỏng máy!
        /// </summary>
        public void LayHorizontallyWithLiquidMistake()
        {
            if (tipAttached && tipContent != null && tipContent.volumeUl > 0.01f)
            {
                damagedByHorizontalLiquid = true;
                contaminated = true;
                BioFeedback.RecordMistake(pipetteToolId, "dat_micropipet_nam_ngang_khi_con_dich",
                    $"Đặt {pipetteToolId} nằm ngang trên mặt bàn khi trong đầu côn đang còn {BioFeedback.FormatVi(tipContent.volumeUl, "0.1")} µL dung dịch!",
                    "Chất lỏng chảy ngược vào trục piston làm ăn mòn lò xo, nhiễm bẩn thân pipet và làm HỎNG micropipet (đã gửi cảnh báo bảo trì lên web)!");
                RebuildHint();
            }
        }

        public void EjectTip(TipWaste wasteBin = null)
        {
            if (!tipAttached) return;
            tipAttached = false;
            tipType = "";
            if (tipContent != null) tipContent.ClearSample();
            if (wasteBin != null) wasteBin.ReceiveEjectedTip();
            BioFeedback.PlayClick();
            BioFeedback.LogAction(pipetteToolId, "thai_bo_dau_con", $"Đã bấm nút đẩy đầu côn của {pipetteToolId} vào hộp rác đầu côn.");
            RebuildHint();
        }

        public void Use(GameObject user)
        {
            if (!tipAttached)
            {
                AttachTip(null);
            }
            else if (tipContent == null || tipContent.volumeUl <= 0.01f)
            {
                Aspirate(null, 1, true, 0f);
            }
            else
            {
                Dispense(null, 2);
            }
        }
    }

    /// <summary>
    /// 5.4.2. Hộp 96 đầu côn tiệt trùng (trạng thái 96 lỗ) — mã: hop_dau_con
    /// </summary>
    [DisallowMultipleComponent]
    public class TipBox : MonoBehaviour, IInteractable
    {
        public string tipSizeId = "micropipet_p200"; // micropipet_p10 | micropipet_p200 | micropipet_p1000
        public int remainingTips = 96;
        public bool lidOpen = false;
        public bool autoclaveTapeBlack = true;
        public float openExposureSec = 0f;
        public BioHinge lidHinge;

        public string UseHint => $"Hộp 96 đầu côn ({tipSizeId}) [Còn {remainingTips}/96 côn · Nắp: {(lidOpen ? "MỞ" : "ĐÓNG")} · {(autoclaveTapeBlack ? "Đã hấp vô trùng" : "Chưa hấp")}] · Bấm: mở nắp & gắn đầu côn";

        public bool TakeOneTip()
        {
            if (remainingTips <= 0)
            {
                BioFeedback.ShowToast("Hộp 96 đầu côn đã hết! Hãy lấy hộp mới từ kho sinh phẩm.", true);
                return false;
            }
            remainingTips--;
            return true;
        }

        public void Use(GameObject user)
        {
            lidOpen = !lidOpen;
            if (lidHinge != null) lidHinge.SetOpen(lidOpen);
            BioFeedback.PlayClick();
            BioFeedback.LogAction("hop_dau_con", lidOpen ? "mo_hop_dau_con" : "dong_hop_dau_con",
                $"Hộp đầu côn ({tipSizeId}): {(lidOpen ? $"Mở nắp (còn {remainingTips}/96 côn)" : "Đóng nắp giữ vô trùng")}.");
        }
    }

    /// <summary>
    /// 5.4.2. Thùng rác thải bỏ đầu côn đã dùng — mã: thung_dau_con
    /// </summary>
    [DisallowMultipleComponent]
    public class TipWaste : MonoBehaviour, IInteractable
    {
        public int discardedTipsCount = 0;

        public string UseHint => $"Thùng đựng đầu côn đã dùng [Đang chứa {discardedTipsCount} đầu côn] · Đưa micropipet tới và bấm nút đẩy côn";

        public void ReceiveEjectedTip()
        {
            discardedTipsCount++;
        }

        public void Use(GameObject user)
        {
            ReceiveEjectedTip();
            BioFeedback.PlayClick();
            BioFeedback.LogAction("thung_dau_con", "bo_dau_con_da_dung", $"Đã phóng bỏ đầu côn vào hộp thải ({discardedTipsCount} côn).");
        }
    }

    /// <summary>
    /// 5.4.3. Ống Eppendorf 1,5 mL & Ống PCR 0,2 mL thành mỏng (nắp bật bản lề) — mã: ong_eppendorf, ong_pcr
    /// </summary>
    [DisallowMultipleComponent]
    public class MicroTube : MonoBehaviour, IInteractable
    {
        public string tubeTypeId = "ong_eppendorf"; // ong_eppendorf | ong_pcr
        public string labelText = "M1";
        public bool capOpen = false;
        public bool dropletsOnWall = false; // Cần ly tâm nhanh (spin down 5 s) để gom xuống đáy
        public float currentTempC = 25.0f;

        // Thành phần Master Mix 25 µL cho ống PCR (5.4.8)
        public bool hasWater = false;
        public bool hasBuffer10x = false;
        public bool hasDntps = false;
        public bool hasForwardPrimer = false;
        public bool hasReversePrimer = false;
        public bool hasTaqPolymerase = false;
        public bool hasTemplateDna = false;
        public bool isNegativeControl = false;

        public BioSample sample;
        public BioHinge capHinge;

        void Awake()
        {
            sample = GetComponent<BioSample>();
            if (sample == null) sample = gameObject.AddComponent<BioSample>();
        }

        public bool IsCompleteMasterMix =>
            hasWater && hasBuffer10x && hasDntps && hasForwardPrimer && hasReversePrimer && hasTaqPolymerase && (hasTemplateDna || isNegativeControl);

        public string UseHint => $"Ống {tubeTypeId} [{labelText} · Nắp: {(capOpen ? "MỞ" : "ĐÓNG")} · {BioFeedback.FormatVi(sample != null ? sample.volumeUl : 0f, "0.1")} µL{(dropletsOnWall ? " · Có giọt bám thành (cần spin down!)" : "")}] · Bấm: đóng/mở nắp ('tách')";

        public void PrepareCompletePcrReaction(bool includeTaq = true, bool includePrimers = true, bool includeTemplate = true, bool negativeCtrl = false)
        {
            hasWater = true;
            hasBuffer10x = true;
            hasDntps = true;
            hasForwardPrimer = includePrimers;
            hasReversePrimer = includePrimers;
            hasTaqPolymerase = includeTaq;
            hasTemplateDna = includeTemplate;
            isNegativeControl = negativeCtrl;

            if (sample != null)
            {
                sample.sampleId = negativeCtrl ? "doi_chung_am_pcr" : "phan_ung_pcr_25ul";
                sample.sampleName = negativeCtrl ? "Đối chứng âm PCR (Nước)" : "Hỗn hợp PCR 25 µL";
                sample.volumeUl = 25.0f;
                sample.volumeMl = 0.025f;
                sample.dnaFragmentBp = 500;
            }
        }

        public void SpinDown5Seconds()
        {
            dropletsOnWall = false;
            BioFeedback.PlayClick();
            BioFeedback.LogAction(tubeTypeId, "spin_down_gom_giot", $"Đã ly tâm nhanh (spin down 5 s) gom toàn bộ giọt bám thành ống {labelText} xuống đáy.");
        }

        public void Use(GameObject user)
        {
            capOpen = !capOpen;
            if (capHinge != null) capHinge.SetOpen(capOpen);
            BioFeedback.PlayClick();
            BioFeedback.LogAction(tubeTypeId, capOpen ? "mo_nap_ong_micro" : "dong_nap_ong_micro",
                $"Ống {tubeTypeId} ({labelText}): {(capOpen ? "Bật mở nắp" : "Ấn đóng kín nắp ('tách')")}.");
        }
    }

    /// <summary>
    /// 5.4.4 & 5.4.13. Hộp đá lạnh bảo quản sinh phẩm trên bàn (0 – 4 °C) — mã: hop_da
    /// </summary>
    [DisallowMultipleComponent]
    public class IceBox : MonoBehaviour, IInteractable
    {
        public float iceTempC = 1.5f;
        public int tubesOnIceCount = 6;

        public string UseHint => $"Hộp đá vẩy (0 – 4 °C, thực tế {BioFeedback.FormatVi(iceTempC, "0.0")} °C) [Đang giữ lạnh {tubesOnIceCount} ống Taq polymerase, dNTPs & mồi] · Bấm: cắm ống sinh phẩm vào đá";

        public void PlaceTubeOnIce(MicroTube tube)
        {
            if (tube != null) tube.currentTempC = iceTempC;
            tubesOnIceCount++;
            BioFeedback.PlayClick();
            BioFeedback.LogAction("hop_da", "cam_ong_vao_hop_da",
                $"Đã cắm ống sinh phẩm vào hộp đá ({BioFeedback.FormatVi(iceTempC, "0.0")} °C): bảo toàn 100% hoạt tính enzyme Taq polymerase.");
        }

        public void Use(GameObject user)
        {
            PlaceTubeOnIce(null);
        }
    }

    /// <summary>
    /// 5.4.7. Tủ thao tác tiền PCR (PCR Workstation, đèn UV 15 phút + khóa liên động) — mã: tu_thao_tac_pcr
    /// </summary>
    [DisallowMultipleComponent]
    public class PcrHoodController : MonoBehaviour, IInteractable
    {
        public bool uvPreSterilized15Min = false;
        public bool uvCurrentlyOn = false;
        public bool sashOpen = false;
        public bool carriedPostPcrTubeBackwardsMistake = false;

        public string UseHint => $"Tủ thao tác tiền PCR (PCR Hood) [Khử trùng UV 15 phút: {(uvPreSterilized15Min ? "ĐÃ XONG" : "CHƯA")} · Cửa: {(sashOpen ? "MỞ" : "ĐÓNG")}] · Bấm: chiếu UV 15 phút & mở tủ pha Master Mix";

        public void WarnOneWayWorkflowViolation()
        {
            carriedPostPcrTubeBackwardsMistake = true;
            BioFeedback.RecordMistake("tu_thao_tac_pcr", "di_nguoc_luong_1_chieu_hau_pcr_ve_tien_pcr",
                "Mang ống sản phẩm sau PCR (chứa hàng tỷ bản sao amplicon) ngược về khu Tiền PCR!",
                "Vi phạm nguyên tắc luồng một chiều (Tiền PCR -> Tách chiết -> Hậu PCR)! Chỉ một hạt khí dung chứa sản phẩm khuếch đại cũng làm nhiễm giếng đối chứng âm (xuất hiện băng giả).");
        }

        public void Use(GameObject user)
        {
            uvCurrentlyOn = false;
            uvPreSterilized15Min = true;
            sashOpen = true;
            BioFeedback.PlaySuccess();
            BioFeedback.LogAction("tu_thao_tac_pcr", "khu_trung_uv_tu_pcr",
                "Đã chiếu UV-C 15 phút phá hủy ADN ngoại lai trong tủ thao tác PCR, tự tắt UV khi mở cửa để pha Master Mix an toàn.");
        }
    }

    /// <summary>
    /// 5.4.9. Máy luân nhiệt PCR 96 giếng (Thermal Cycler) — mã: may_pcr
    /// Gọi trực tiếp Pcr.PrimerTmWallace, Pcr.SuggestedAnnealingTempC và Pcr.CopiesAfterCycles của Backend.
    /// </summary>
    [DisallowMultipleComponent]
    public class ThermalCyclerController : MonoBehaviour, IInteractable
    {
        public bool lidClosed = true;
        public bool heatedLid105C = true; // Nắp nhiệt 105 °C chống ngưng tụ hơi nước
        public int cycles = 30;
        public double annealingTempC = 55.0;
        public double optimalTaC = 55.0;
        public double currentBlockTempC = 25.0;
        public int targetAmpliconBp = 500;
        public double lastAmplifiedCopies = 0.0;

        public readonly List<MicroTube> loadedTubes = new List<MicroTube>();
        public PcrProgram program = new PcrProgram();

        void Awake()
        {
            if (program == null) program = new PcrProgram();
            program.cycler = this;
            // Tính Tm mẫu mồi 20-mer (10 G/C, 10 A/T): Tm = 4*10 + 2*10 = 60 °C -> Ta gợi ý = 55 °C
            double tm = Pcr.PrimerMeltingTemp(5, 5, 5, 5);
            optimalTaC = Pcr.AnnealingTemperature(tm);
            annealingTempC = optimalTaC;
        }

        public string UseHint => $"Máy luân nhiệt PCR 96 giếng [Chu trình: {cycles} chu kỳ · Tₐ = {BioFeedback.FormatVi((float)annealingTempC, "0.0")} °C · Nắp nóng 105 °C: {(heatedLid105C ? "BẬT" : "TẮT!")} · Bản sao: {lastAmplifiedCopies:E2}] · Bấm: chạy chương trình PCR";

        public bool RunPcrCycle(MicroTube reactionTube = null)
        {
            if (!lidClosed)
            {
                BioFeedback.RecordMistake("may_pcr", "chay_pcr_chua_dong_nap",
                    "Bấm chạy máy PCR khi nắp gia nhiệt chưa đóng khóa!",
                    "Phải đóng kín và khóa nắp gia nhiệt 105 °C trước khi bắt đầu chu trình nhiệt.");
                return false;
            }

            if (reactionTube != null && !loadedTubes.Contains(reactionTube))
                loadedTubes.Add(reactionTube);

            double efficiency = 0.92;
            if (!heatedLid105C)
            {
                efficiency *= 0.45;
                BioFeedback.RecordMistake("may_pcr", "quen_bat_nap_nhiet_105C",
                    "Quên bật gia nhiệt nắp 105 °C khi chạy PCR!",
                    "Khi khối nhiệt lên 95 °C, nước trong hỗn hợp phản ứng 25 µL bay hơi và ngưng tụ hết lên nắp ống làm cô đặc muối và giảm mạnh hiệu suất nhân bản.");
            }

            bool missingReagent = false;
            if (reactionTube != null)
            {
                if (!reactionTube.hasTaqPolymerase || !reactionTube.hasForwardPrimer || !reactionTube.hasReversePrimer || (!reactionTube.hasTemplateDna && !reactionTube.isNegativeControl))
                {
                    missingReagent = true;
                    BioFeedback.RecordMistake("may_pcr", "pha_master_mix_thieu_thanh_phan",
                        "Hỗn hợp phản ứng PCR bị thiếu thành phần thiết yếu (Taq polymerase / Mồi / Khuôn ADN)!",
                        "Phản ứng chuỗi polymerase không thể nhân bản đoạn đích nếu thiếu enzyme Taq, cặp mồi đặc hiệu hoặc ADN khuôn — kết quả điện di sẽ KHÔNG có băng sản phẩm.");
                }
            }

            bool nonSpecificBands = false;
            if (annealingTempC < optimalTaC - 6.0)
            {
                nonSpecificBands = true;
                BioFeedback.RecordMistake("may_pcr", "nhiet_do_gan_moi_ta_qua_thap",
                    $"Đặt nhiệt độ gắn mồi Tₐ = {BioFeedback.FormatVi((float)annealingTempC, "0.0")} °C quá thấp so với mức chuẩn {BioFeedback.FormatVi((float)optimalTaC, "0.0")} °C!",
                    "Mồi bắt cặp không đặc hiệu vào các vị trí sai trên khuôn ADN, sinh ra nhiều băng phụ (non-specific bands) trên gel điện di.");
            }
            else if (annealingTempC > optimalTaC + 8.0)
            {
                efficiency = 0.05;
                BioFeedback.RecordMistake("may_pcr", "nhiet_do_gan_moi_ta_qua_cao",
                    $"Đặt nhiệt độ gắn mồi Tₐ = {BioFeedback.FormatVi((float)annealingTempC, "0.0")} °C quá cao vượt cả Tm!",
                    "Cặp mồi không thể gắn vào mạch khuôn ADN nên hầu như không tạo ra sản phẩm PCR.");
            }

            lastAmplifiedCopies = missingReagent ? 0.0 : Pcr.AmplifiedCopies(1000.0, cycles, efficiency);
            currentBlockTempC = 4.0; // Kết thúc giữ ở 4 °C

            if (reactionTube != null && reactionTube.sample != null)
            {
                reactionTube.sample.dna.Clear();
                if (!missingReagent && !reactionTube.isNegativeControl && lastAmplifiedCopies > 1e6)
                {
                    reactionTube.sample.isAmplifiedByPcr = true;
                    reactionTube.sample.pcrCyclesCompleted = cycles;
                    reactionTube.sample.dnaFragmentBp = targetAmpliconBp;
                    reactionTube.sample.dna.Add(new BioDnaBandInfo { bp = targetAmpliconBp, ng = 85.0, smear = false });
                    if (nonSpecificBands)
                    {
                        reactionTube.sample.dna.Add(new BioDnaBandInfo { bp = 220, ng = 30.0, smear = false });
                        reactionTube.sample.dna.Add(new BioDnaBandInfo { bp = 850, ng = 25.0, smear = false });
                    }
                }
                else
                {
                    reactionTube.sample.isAmplifiedByPcr = false;
                }
                reactionTube.sample.SyncToData();
            }

            if (!missingReagent && !nonSpecificBands && heatedLid105C)
            {
                BioFeedback.PlaySuccess();
                BioFeedback.LogAction("may_pcr", "hoan_tat_chu_trinh_pcr",
                    $"Hoàn tất {cycles} chu kỳ PCR (95 °C → {BioFeedback.FormatVi((float)annealingTempC, "0")} °C → 72 °C): thu được {lastAmplifiedCopies:E2} bản sao đoạn đích {targetAmpliconBp} bp.");
            }
            return !missingReagent;
        }

        public void Use(GameObject user)
        {
            MicroTube tube = loadedTubes.Count > 0 ? loadedTubes[0] : null;
            if (tube == null)
            {
                var all = FindObjectsByType<MicroTube>(FindObjectsSortMode.None);
                if (all.Length > 0)
                {
                    tube = all[0];
                    tube.PrepareCompletePcrReaction(true, true, true, false);
                }
            }
            RunPcrCycle(tube);
        }
    }

    /// <summary>
    /// Màn hình vận hành Máy luân nhiệt PCR (5.4.9) — kế thừa InstrumentProgram.
    /// </summary>
    public class PcrProgram : InstrumentProgram
    {
        public override string Code => "may_pcr";
        public ThermalCyclerController cycler;

        public override void Build()
        {
            if (Doc == null || Doc.Steps.Count == 0)
                Doc = BioCatalogLoader.LoadMachineDoc(Code);

            S.AddButton("Nắp nhiệt 105 °C", 0, 0, () =>
            {
                if (cycler != null) cycler.heatedLid105C = !cycler.heatedLid105C;
                S.Done("nap_nhiet");
            });

            S.AddButton("Ta +1 °C", 1, 0, () =>
            {
                if (cycler != null) cycler.annealingTempC += 1.0;
                S.Done("nhiet_do_gan_moi");
            });

            S.AddButton("Ta -1 °C", 2, 0, () =>
            {
                if (cycler != null) cycler.annealingTempC -= 1.0;
                S.Done("nhiet_do_gan_moi");
            });

            S.AddButton("Chạy PCR (30 chu kỳ)", 0, 1, () =>
            {
                if (cycler != null) cycler.RunPcrCycle();
                S.Done("chay_pcr");
            }, true, 2);

            S.SetPlotAxes("CHU TRÌNH NHIỆT PCR THEO THỜI GIAN", "Thời gian (s)", "Nhiệt độ (°C)", "0", "120", "20", "100");
            Refresh();
        }

        public override void Refresh()
        {
            int cyc = cycler != null ? cycler.cycles : 30;
            double ta = cycler != null ? cycler.annealingTempC : 55.0;
            double copies = cycler != null ? cycler.lastAmplifiedCopies : 0.0;

            S.SetResults(
                $"<b>MÁY LUÂN NHIỆT PCR 96 GIẾNG</b>\n" +
                $"• Chu trình: <b>{cyc} chu kỳ</b> [95 °C -> {BioFeedback.FormatVi((float)ta, "0.0")} °C -> 72 °C]\n" +
                $"• Nắp gia nhiệt 105 °C: <b>{(cycler == null || cycler.heatedLid105C ? "BẬT (Chuẩn)" : "<color=#FF6666>TẮT!</color>")}</b>\n" +
                $"• Số bản sao ADN nhân bản: <b>{copies:E2} amplicons</b>\n" +
                $"• Trạng thái khối nhiệt: <b>{BioFeedback.FormatVi((float)(cycler != null ? cycler.currentBlockTempC : 25.0), "0.0")} °C</b>");
        }

        public override void DrawSchematic(Painter p, float time)
        {
            p.Clear(new Color32(28, 14, 35, 255));
            // Vẽ khối 96 giếng và nắp nhiệt
            p.FillRect(40, 40, 220, 80, new Color32(70, 40, 85, 255));
            for (int r = 0; r < 4; r++)
                for (int c = 0; c < 8; c++)
                    p.Disc(55 + c * 26, 55 + r * 16, 5, new Color32(190, 150, 210, 255));
            // Nắp nhiệt
            p.FillRect(35, 125, 230, 20, new Color32(180, 60, 50, 230));
        }

        public override void DrawPlot(Painter p)
        {
            p.Clear(new Color32(20, 10, 25, 255));
            // Bậc thang chu trình: 95°C (cao) -> 55°C (thấp) -> 72°C (giữa)
            p.Line(30, 130, 90, 130, new Color32(240, 80, 80, 255), 2); // 95C
            p.Line(90, 130, 120, 60, new Color32(160, 140, 200, 255), 2);
            p.Line(120, 60, 180, 60, new Color32(80, 180, 240, 255), 2); // 55C
            p.Line(180, 60, 200, 95, new Color32(160, 140, 200, 255), 2);
            p.Line(200, 95, 260, 95, new Color32(80, 220, 140, 255), 2); // 72C
        }
    }

    /// <summary>
    /// 5.4.11. Bể điện di gel agarose nằm ngang — mã: be_dien_di
    /// Gọi trực tiếp Electrophoresis.MigrationMm của Backend.
    /// </summary>
    [DisallowMultipleComponent]
    public class GelTankController : MonoBehaviour, IInteractable
    {
        public float agarosePercent = 1.0f;
        public bool bufferSubmerged = true;
        public bool wellsAtCathodeNegative = true; // Giếng phải đặt ở cực âm (đen) để ADN tích điện âm chạy về cực dương (đỏ)
        public bool lidClosed = true;
        public bool puncturedWellBottomMistake = false;
        public bool hasCompletedRun = false;

        public readonly int[] ladderBp = { 100, 250, 500, 750, 1000, 1500 };
        public readonly List<BioDnaBandInfo> sampleWellBands = new List<BioDnaBandInfo>();
        public readonly List<float> sampleMigrationMm = new List<float>();
        public readonly List<float> ladderMigrationMm = new List<float>();

        public string UseHint => $"Bể điện di agarose {BioFeedback.FormatVi(agarosePercent, "0.0")} % [Giếng ở cực: {(wellsAtCathodeNegative ? "Âm (−) đen chuẩn" : "Dương (+) NGƯỢC CỰC!")} · Đã chạy: {(hasCompletedRun ? "XONG" : "Chưa")}] · Bấm: nạp mẫu vào giếng & chạy điện di 100 V – 30 phút";

        public void LoadSampleIntoWell(BioSample sample, bool puncturedWellBottom = false)
        {
            puncturedWellBottomMistake = puncturedWellBottom;
            if (puncturedWellBottom)
            {
                BioFeedback.RecordMistake("be_dien_di", "choc_thung_day_gieng_gel",
                    "Đưa đầu côn micropipet quá sâu chọc thủng đáy giếng gel agarose!",
                    "Mẫu ADN rò rỉ xuống dưới đáy bản gel làm băng sau điện di bị mờ nhạt hoặc mất hẳn.");
            }

            sampleWellBands.Clear();
            if (sample != null && sample.dna.Count > 0)
            {
                sampleWellBands.AddRange(sample.dna);
            }
            else
            {
                sampleWellBands.Add(new BioDnaBandInfo { bp = 500, ng = 80.0, smear = false });
            }
            BioFeedback.PlayClick();
            BioFeedback.LogAction("be_dien_di", "nap_mau_vao_gieng_gel",
                $"Đã nạp 5 µL mẫu ADN (trộn đệm nạp xanh bromophenol) vào giếng bản gel agarose {BioFeedback.FormatVi(agarosePercent, "0.0")} %.");
        }

        public bool RunElectrophoresis(double volts = 100.0, double minutes = 30.0, bool reversedPolarity = false)
        {
            wellsAtCathodeNegative = !reversedPolarity;
            if (reversedPolarity)
            {
                sampleMigrationMm.Clear();
                ladderMigrationMm.Clear();
                hasCompletedRun = false;
                BioFeedback.RecordMistake("be_dien_di", "cam_nguoc_cuc_dien_di_mat_mau",
                    "Cắm ngược dây điện cực (đặt giếng ở cực Dương đỏ, chạy về cực Âm đen)!",
                    "Phân tử ADN mang điện tích âm (do gốc photphat PO₄³⁻) luôn di chuyển về phía cực Dương (+). Đặt giếng ở cực dương làm toàn bộ ADN chạy ngược ra khỏi bản gel vào đệm TAE sau vài phút (mất sạch mẫu)!");
                return false;
            }

            if (sampleWellBands.Count == 0)
            {
                sampleWellBands.Add(new BioDnaBandInfo { bp = 500, ng = 80.0, smear = false });
            }

            ladderMigrationMm.Clear();
            for (int i = 0; i < ladderBp.Length; i++)
            {
                double mm = Electrophoresis.MigrationDistanceCm(ladderBp[i], volts, minutes, agarosePercent) * 10.0;
                ladderMigrationMm.Add((float)mm);
            }

            sampleMigrationMm.Clear();
            for (int i = 0; i < sampleWellBands.Count; i++)
            {
                double mm = Electrophoresis.MigrationDistanceCm(sampleWellBands[i].bp, volts, minutes, agarosePercent) * 10.0;
                sampleMigrationMm.Add((float)mm);
            }

            hasCompletedRun = true;
            BioFeedback.PlaySuccess();
            BioFeedback.LogAction("be_dien_di", "chay_dien_di_thanh_cong",
                $"Đã chạy điện di tại {BioFeedback.FormatVi((float)volts, "0")} V trong {BioFeedback.FormatVi((float)minutes, "0")} phút: băng 500 bp di chuyển {BioFeedback.FormatVi(sampleMigrationMm[0], "0.1")} mm (so với thang chuẩn 100 bp chạy {BioFeedback.FormatVi(ladderMigrationMm[0], "0.1")} mm).");
            return true;
        }

        public void Use(GameObject user)
        {
            RunElectrophoresis(100.0, 30.0, false);
        }
    }

    /// <summary>
    /// 5.4.11. Bộ nguồn điện di (Power Supply, chỉnh V, mA, phút, khóa an toàn điện) — mã: nguon_dien_di
    /// </summary>
    [DisallowMultipleComponent]
    public class PowerSupplyController : MonoBehaviour, IInteractable, IScrollAdjustable
    {
        public float voltageV = 100.0f;
        public float currentMa = 45.0f;
        public int timerMinutes = 30;
        public bool isRunningOutput = false;
        public GelTankController connectedTank;

        public string UseHint => $"Bộ nguồn điện di [Điện áp: {BioFeedback.FormatVi(voltageV, "0")} V · Dòng: {BioFeedback.FormatVi(currentMa, "0")} mA · Hẹn giờ: {timerMinutes} phút · {(isRunningOutput ? "ĐANG CẤP NGUỒN" : "NGẮT")}] · Bấm: chạy điện di · Con lăn: chỉnh V";

        public void OpenTankLidWhileRunningMistake()
        {
            isRunningOutput = false;
            BioFeedback.RecordMistake("nguon_dien_di", "mo_nap_be_dien_di_khi_chua_tat_nguon",
                "Mở nắp bể điện di khi bộ nguồn cao áp (100 V) đang chạy!",
                "Khóa an toàn tự động ngắt nguồn! Phải luôn bấm TẮT bộ nguồn điện di trước khi tháo nắp bể để tránh điện giật.");
        }

        public void Use(GameObject user)
        {
            isRunningOutput = !isRunningOutput;
            if (isRunningOutput)
            {
                if (connectedTank == null)
                {
                    var tanks = FindObjectsByType<GelTankController>(FindObjectsSortMode.None);
                    if (tanks.Length > 0) connectedTank = tanks[0];
                }
                if (connectedTank != null)
                {
                    connectedTank.RunElectrophoresis(voltageV, timerMinutes, false);
                }
            }
            BioFeedback.PlayClick();
            BioFeedback.LogAction("nguon_dien_di", isRunningOutput ? "bat_nguon_dien_di" : "tat_nguon_dien_di",
                $"Bộ nguồn điện di: {(isRunningOutput ? $"Cấp áp {BioFeedback.FormatVi(voltageV, "0")} V trong {timerMinutes} phút" : "Đã tắt nguồn an toàn")}.");
        }

        public void OnScroll(float delta)
        {
            if (Mathf.Abs(delta) < 0.001f) return;
            voltageV = Mathf.Clamp(voltageV + Mathf.Sign(delta) * 10f, 50f, 150f);
        }
    }

    /// <summary>
    /// 5.4.11. Khay đúc gel agarose + lược tạo giếng (8–12 răng) — mã: khay_duc_gel
    /// </summary>
    [DisallowMultipleComponent]
    public class GelCastingTray : MonoBehaviour, IInteractable
    {
        public bool combInserted = true;
        public bool gelPoured = false;
        public bool gelSolidified = false;
        public bool combRemovedWellsReady = false;

        public string UseHint => combRemovedWellsReady
            ? "Khay đúc gel agarose 1 % [Bản gel đã đông đặc trắng đục nhẹ, đã rút lược tạo 8 giếng sắc nét] · Đặt vào bể điện di"
            : "Khay đúc gel agarose + lược 8 giếng · Bấm: đổ gel agarose ~55 °C (+ thuốc nhuộm ADN an toàn), chờ đông 20 phút và rút lược";

        public void PourHotAgaroseMistake(float tempC)
        {
            if (tempC > 75f)
            {
                BioFeedback.RecordMistake("khay_duc_gel", "do_agarose_qua_nong_cong_khay",
                    $"Đổ dịch agarose đang sôi ({BioFeedback.FormatVi(tempC, "0")} °C) trực tiếp vào khay nhựa acrylic!",
                    "Nhiệt độ quá cao làm cong vênh khay nhựa và tạo bọt khí. Phải để bình agarose nguội xuống ~55–60 °C rồi mới thêm thuốc nhuộm ADN an toàn và đổ khuôn.");
            }
        }

        public void CastAndSolidifyGel()
        {
            combInserted = true;
            gelPoured = true;
            gelSolidified = true;
            combRemovedWellsReady = true;
            BioFeedback.PlaySuccess();
            BioFeedback.LogAction("khay_duc_gel", "duc_gel_agarose_1_phan_tram",
                "Đã đổ gel agarose 1 % ở 55 °C có pha thuốc nhuộm ADN an toàn (SYBR Safe), chờ đông đặc 20 phút và rút lược thẳng đứng tạo 8 giếng.");
        }

        public void Use(GameObject user)
        {
            CastAndSolidifyGel();
        }
    }

    /// <summary>
    /// 5.4.11. Lò vi sóng đun chảy agarose — mã: lo_vi_song
    /// </summary>
    [DisallowMultipleComponent]
    public class MicrowaveOven : MonoBehaviour, IInteractable
    {
        public bool doorOpen = false;
        public bool agaroseMeltedClear = false;

        public string UseHint => agaroseMeltedClear
            ? "Lò vi sóng phòng phân tử [Bình agarose 1 % trong đệm TAE đã tan chảy trong suốt]"
            : "Lò vi sóng (đun chảy thạch agarose) · Bấm: đun sôi ngắt quãng cho bột agarose tan trong suốt";

        public void BoilSealedBottleMistake()
        {
            BioFeedback.RecordMistake("lo_vi_song", "dun_lo_vi_song_van_chat_nap_kin",
                "Đặt bình chứa thạch agarose vặn chặt nắp kín vào lò vi sóng!",
                "Áp suất hơi nước tích tụ trong bình kín sẽ gây nổ vỡ bình thủy tinh. Phải nới lỏng nắp hoặc dùng bình tam giác đậy hờ màng chịu nhiệt.");
        }

        public void Use(GameObject user)
        {
            agaroseMeltedClear = true;
            BioFeedback.PlaySuccess();
            BioFeedback.LogAction("lo_vi_song", "dun_chay_agarose", "Đã đun lò vi sóng làm tan chảy hoàn toàn agarose 1 % trong đệm TAE thành dung dịch trong suốt.");
        }
    }

    /// <summary>
    /// 5.4.12. Buồng chụp gel (Gel Doc / bàn soi UV-LED xanh 470 nm) — mã: gel_doc
    /// </summary>
    [DisallowMultipleComponent]
    public class GelDocController : MonoBehaviour, IInteractable
    {
        public bool doorClosed = true;
        public bool uvBlueLampOn = false;
        public int estimatedBp = 500;
        public string lastSavedGelImagePath = "";
        public GelTankController sourceTank;
        public GelDocProgram program = new GelDocProgram();

        void Awake()
        {
            if (program == null) program = new GelDocProgram();
            program.gelDoc = this;
        }

        public string UseHint => $"Buồng chụp và phân tích Gel (Gel Doc) [Cửa: {(doorClosed ? "ĐÓNG" : "MỞ")} · Đèn kích thích: {(uvBlueLampOn ? "BẬT" : "TẮT")} · Băng mẫu ước lượng: ~{estimatedBp} bp] · Bấm: soi & chụp ảnh bản gel";

        public void OpenDoorWhileUvOnSafetyCheck()
        {
            doorClosed = false;
            if (uvBlueLampOn)
            {
                uvBlueLampOn = false;
                BioFeedback.ShowToast("Khóa liên động cửa buồng chụp Gel: Tự động tắt đèn UV 302 nm bảo vệ mắt!");
                BioFeedback.LogAction("gel_doc", "interlock_tat_uv_gel_doc", "Mở cửa buồng chụp gel -> Khóa liên động tự ngắt đèn UV.");
            }
        }

        public int CaptureAndAnalyzeGel()
        {
            doorClosed = true;
            uvBlueLampOn = true;
            if (sourceTank == null)
            {
                var tanks = FindObjectsByType<GelTankController>(FindObjectsSortMode.None);
                if (tanks.Length > 0) sourceTank = tanks[0];
            }
            if (sourceTank != null && sourceTank.sampleWellBands.Count > 0)
            {
                estimatedBp = sourceTank.sampleWellBands[0].bp;
            }
            else
            {
                estimatedBp = 500;
            }
            lastSavedGelImagePath = $"gel_{DateTime.Now:yyyyMMdd_HHmmss}_{estimatedBp}bp.png";
            BioFeedback.PlaySuccess();
            BioFeedback.LogAction("gel_doc", "chup_va_phan_tich_gel",
                $"Đã soi và chụp ảnh bản gel điện di ({lastSavedGelImagePath}): Nội suy log(bp) theo thang chuẩn (100..1500 bp) xác định băng sản phẩm PCR ở đúng {estimatedBp} bp, giếng đối chứng âm sạch!");
            return estimatedBp;
        }

        public void Use(GameObject user)
        {
            CaptureAndAnalyzeGel();
        }
    }

    /// <summary>
    /// Màn hình vận hành Buồng chụp Gel (5.4.12) — kế thừa InstrumentProgram.
    /// </summary>
    public class GelDocProgram : InstrumentProgram
    {
        public override string Code => "gel_doc";
        public GelDocController gelDoc;

        public override void Build()
        {
            if (Doc == null || Doc.Steps.Count == 0)
                Doc = BioCatalogLoader.LoadMachineDoc(Code);

            S.AddButton("Đèn UV / Blue LED", 0, 0, () =>
            {
                if (gelDoc != null) gelDoc.uvBlueLampOn = !gelDoc.uvBlueLampOn;
                S.Done("bat_den_uv");
            });

            S.AddButton("Chụp & Phân tích Gel", 1, 0, () =>
            {
                if (gelDoc != null) gelDoc.CaptureAndAnalyzeGel();
                S.Done("chup_anh_gel");
            }, true, 2);

            S.SetPlotAxes("SƠ ĐỒ BĂNG ĐIỆN DI GEL AGAROSE (bp)", "Làn chạy", "Kích thước (bp)", "1", "3", "100", "1500");
            Refresh();
        }

        public override void Refresh()
        {
            int bp = gelDoc != null ? gelDoc.estimatedBp : 500;
            string file = gelDoc != null ? gelDoc.lastSavedGelImagePath : "(Chưa chụp)";

            S.SetResults(
                $"<b>BUỒNG CHỤP VÀ PHÂN TÍCH GEL (GEL DOC)</b>\n" +
                $"• Cửa buồng: <b>{(gelDoc == null || gelDoc.doorClosed ? "ĐÓNG KÍN" : "<color=#FF6666>MỞ!</color>")}</b>\n" +
                $"• Nguồn sáng kích thích: <b>{(gelDoc != null && gelDoc.uvBlueLampOn ? "BẬT (302 nm / Blue LED)" : "TẮT")}</b>\n" +
                $"• Băng sản phẩm PCR nội suy: <b>~{bp} bp</b>\n" +
                $"• Ảnh đã lưu: <b>{file}</b>\n" +
                $"• Đánh giá: <b>Băng đặc hiệu rõ, đối chứng âm không có vạch (Sạch)</b>");
        }

        public override void DrawSchematic(Painter p, float time)
        {
            p.Clear(new Color32(12, 18, 16, 255));
            p.FillRect(40, 30, 200, 140, new Color32(28, 40, 36, 255));
            p.FillRect(115, 170, 50, 25, new Color32(60, 90, 80, 255));
            p.FillRect(70, 45, 140, 70, new Color32(20, 30, 26, 255));
        }

        public override void DrawPlot(Painter p)
        {
            p.Clear(new Color32(10, 14, 18, 255));
            p.FillRect(16, 34, 120, 96, new Color32(18, 28, 24, 255));
            int[] ladderY = { 46, 58, 72, 86, 102, 118 };
            for (int i = 0; i < ladderY.Length; i++)
                p.FillRect(24, ladderY[i], 24, 3, new Color32(80, 220, 140, 255));

            int bp = gelDoc != null ? gelDoc.estimatedBp : 500;
            if (bp > 0)
            {
                p.FillRect(64, 86, 26, 4, new Color32(80, 220, 140, 255));
            }
        }
    }

    /// <summary>
    /// 5.4.14. Thiết bị đo mật độ quang OD600 & độ tinh sạch ADN A260/A280 — mã: may_quang_pho_od600
    /// </summary>
    [DisallowMultipleComponent]
    public class Od600Controller : MonoBehaviour, IInteractable
    {
        public bool blankCalibrated = true;
        public double currentHours = 4.0;
        public double measuredOd600 = 0.65;
        public double a260 = 1.20;
        public double a280 = 0.66;
        public Od600Program program = new Od600Program();

        void Awake()
        {
            if (program == null) program = new Od600Program();
            program.controller = this;
        }

        public double EstimatedCellsPerMl => measuredOd600 * 8.0e8;
        public double DnaConcentrationNgUl => a260 * 50.0;
        public double PurityRatio260_280 => a280 > 0.001 ? a260 / a280 : 0.0;

        public string UseHint => $"Máy quang phổ đo OD600 / ADN [OD600 = {BioFeedback.FormatVi((float)measuredOd600, "0.00")} (~{EstimatedCellsPerMl:E2} TB/mL) · A260/A280 = {BioFeedback.FormatVi((float)PurityRatio260_280, "0.00")} ({BioFeedback.FormatVi((float)DnaConcentrationNgUl, "0.0")} ng/µL)] · Bấm: đo đường cong sinh trưởng";

        public double MeasureGrowthPointAtHour(double hour, bool forgotBlankMistake = false)
        {
            currentHours = hour;
            blankCalibrated = !forgotBlankMistake;
            double trueCells = ColonyGrowth.PopulationAtTime(hour, 1.0e6, 1.0e9, 1.0);
            measuredOd600 = ColonyGrowth.OpticalDensity600(trueCells);
            if (forgotBlankMistake)
            {
                measuredOd600 += 0.38;
                BioFeedback.RecordMistake("may_quang_pho_od600", "do_od600_quen_tru_mau_trang_blank",
                    "Đo OD600 dịch nuôi cấy mà QUÊN đo trừ mẫu trắng (Blank) bằng môi trường LB sạch!",
                    "Môi trường LB có màu vàng hổ phách tự hấp thụ ánh sáng ở 600 nm làm giá trị OD600 bị cộng dồn sai số hệ thống (+0,38 AU).");
            }
            else
            {
                BioFeedback.PlaySuccess();
                BioFeedback.LogAction("may_quang_pho_od600", "do_od600_sinh_truong",
                    $"Đo OD600 tại t = {BioFeedback.FormatVi((float)hour, "0.0")} giờ (đã trừ Blank): OD600 = {BioFeedback.FormatVi((float)measuredOd600, "0.000")} → Mật độ ≈ {trueCells:E2} tế bào/mL.");
            }
            return measuredOd600;
        }

        public void Use(GameObject user)
        {
            double nextH = currentHours >= 8.0 ? 0.5 : currentHours + 1.5;
            MeasureGrowthPointAtHour(nextH, false);
        }
    }

    /// <summary>
    /// Chương trình màn hình vận hành Máy đo OD600 — kế thừa InstrumentProgram.
    /// </summary>
    public class Od600Program : InstrumentProgram
    {
        public override string Code => "may_do_od600";
        public Od600Controller controller;

        public override void Build()
        {
            if (Doc == null || Doc.Steps.Count == 0)
                Doc = BioCatalogLoader.LoadMachineDoc(Code);

            S.AddButton("Đo mẫu trắng (Blank = 0)", 0, 0, () =>
            {
                if (controller != null) controller.blankCalibrated = true;
                S.Done("do_mau_trang");
                S.Say("Đã zero trừ nền mẫu trắng Blank (A = 0,000).", 1);
            });

            S.AddButton("Đo điểm OD600 tiếp", 1, 0, () =>
            {
                if (controller != null) controller.Use(null);
                S.Done("do_od600");
            });

            S.AddButton("Đo ADN A260/A280", 2, 0, () =>
            {
                S.Done("do_a260_a280");
                S.Say("Đã đo mật độ quang hấp thụ A260 và A280.", 1);
            });

            S.SetPlotAxes("ĐƯỜNG CONG SINH TRƯỞNG VI SINH VẬT (OD600)", "Thời gian (h)", "OD600 (AU)", "0", "8", "0", "1.5");
            Refresh();
        }

        public override void Refresh()
        {
            double od = controller != null ? controller.measuredOd600 : 0.65;
            double hrs = controller != null ? controller.currentHours : 4.0;
            double cells = controller != null ? controller.EstimatedCellsPerMl : 5.2e8;
            double ratio = controller != null ? controller.PurityRatio260_280 : 1.82;
            double conc = controller != null ? controller.DnaConcentrationNgUl : 60.0;

            S.SetResults(
                $"<b>MÁY QUANG PHỔ ĐO OD600 & ĐỘ TINH SẠCH ADN</b>\n" +
                $"• Thời điểm ủ t: <b>{BioFeedback.FormatVi((float)hrs, "0.1")} giờ</b>\n" +
                $"• Mật độ quang OD600: <b>{BioFeedback.FormatVi((float)od, "0.000")} AU</b>\n" +
                $"• Mật độ tế bào ước tính: <b>{cells:E2} CFU/mL</b>\n" +
                $"• Tỷ số A260/A280: <b>{BioFeedback.FormatVi((float)ratio, "0.00")}</b> (Chuẩn: 1,80 – 2,00)\n" +
                $"• Nồng độ ADN: <b>{BioFeedback.FormatVi((float)conc, "0.0")} ng/µL</b>");
        }

        public override void DrawSchematic(Painter p, float time)
        {
            p.Clear(new Color32(14, 28, 42, 255));
            p.FillRect(30, 80, 40, 40, new Color32(240, 200, 80, 255));
            p.FillRect(130, 70, 30, 60, new Color32(100, 180, 220, 200));
            p.FillRect(220, 80, 40, 40, new Color32(80, 200, 140, 255));
            p.Line(70, 100, 220, 100, new Color32(255, 230, 100, 240), 3);
        }

        public override void DrawPlot(Painter p)
        {
            p.Clear(new Color32(10, 20, 32, 255));
            for (int x = 40; x < 260; x += 4)
            {
                float t = (x - 40) / 220f;
                float y = 30 + 130f / (1f + Mathf.Exp(-7f * (t - 0.45f)));
                p.Disc(x, y, 2.5f, new Color32(75, 195, 145, 255));
            }
        }
    }

    /// <summary>
    /// 5.4.14. Cuvet đo quang 1 cm (2 mặt nhám để cầm, 2 mặt trong suốt cho tia sáng đi qua) — mã: cuvet
    /// </summary>
    [DisallowMultipleComponent]
    public class Cuvette : MonoBehaviour, IInteractable
    {
        public bool touchedOpticalFaceMistake = false;
        public bool orientedCorrectlyInBeam = true;
        public BioSample sample;

        void Awake()
        {
            sample = GetComponent<BioSample>();
            if (sample == null) sample = gameObject.AddComponent<BioSample>();
        }

        public string UseHint => touchedOpticalFaceMistake
            ? "Cuvet 1 cm [BỊ DÍNH VÂN TAY MẶT QUANG HỌC — cần lau bằng giấy lau kính]"
            : "Cuvet đo quang phổ 1 cm (cầm ở 2 mặt nhám có gân) · Bấm: nạp dịch khuẩn & đặt đúng chiều tia sáng";

        public void TouchClearOpticalFaceMistake()
        {
            touchedOpticalFaceMistake = true;
            BioFeedback.RecordMistake("cuvet", "cam_tay_vao_mat_trong_quang_hoc_cuvet",
                "Cầm ngón tay trực tiếp vào mặt trong suốt (mặt quang học) của cuvet hoặc đặt mặt nhám chắn ngang tia sáng!",
                "Dấu vân tay và vết mờ làm tán xạ chùm tia đơn sắc 600 nm khiến số đo OD600 tăng giả tạo. Phải luôn cầm cuvet ở 2 mặt nhám và lau sạch mặt trong bằng giấy lau kính.");
        }

        public void Use(GameObject user)
        {
            touchedOpticalFaceMistake = false;
            orientedCorrectlyInBeam = true;
            BioFeedback.PlayClick();
            BioFeedback.LogAction("cuvet", "dat_cuvet_dung_chieu", "Đã cầm cuvet ở mặt nhám và đặt mặt quang học trong suốt vuông góc với chùm tia sáng 600 nm.");
        }
    }
}
