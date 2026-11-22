using System.Collections;
using System.IO;
using Khkt.Demo.Instruments;
using Khkt.Environment;
using Khkt.Interaction;
using Khkt.Interaction.Bio;
using Khkt.Simulation.Bio;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Khkt.Demo.Bio
{
    /// <summary>
    /// Kính hiển vi quang học hai mắt (29 máy trên Tầng 2) và Kính hiển vi kỹ thuật số giáo viên (5.1.1 & 5.1.2).
    /// Hỗ trợ 4 vật kính (4×, 10×, 40×, 100× dầu), ốc sơ cấp/vi cấp, bàn sa trượt X/Y, tụ quang, màn chắn sáng,
    /// ghé mắt qua thị kính (CloseUpView), thước trắc vi thị kính, vẽ ảnh tiêu bản thủ tục và kiểm tra lỗi vỡ lamen / thiếu dầu soi.
    /// </summary>
    public class MicroscopeController : MonoBehaviour, IDemoUsable, IBioTarget
    {
        public Transform nosepiece;
        public Transform stage;
        public Transform stageTransform;
        public PlaceSocket stageSocket;
        public Transform xyKnob;
        public Transform condenser;
        public Transform slideClipTransform;
        public Transform eyepieceTarget;
        public int currentObjectiveIndex;  // 0 = 4×, 1 = 10×, 2 = 40×, 3 = 100× dầu
        public float eyepieceMag = 10f;
        public float fieldNumberMm = 18f;

        [Header("Cấu hình Kính hiển vi giáo viên (5.1.2)")]
        public bool isTeacherCamera = false;
        public bool cameraStreaming = true;
        public Renderer teacherScreenRenderer;

        [Header("Trạng thái Quang học & Cơ học")]
        public float stageHeightMm = 12.42f;
        public float optimalStageHeightMm = 12.50f;
        public float fineUm = 0f;
        public float stageX = 0f;
        public float stageY = 0f;
        public bool lampOn = true;
        public float brightness = 0.85f;
        public float condenserHeight = 0.85f;
        public float irisOpen = 0.75f;
        public bool oilApplied = false;
        public bool objective40xOilDirty = false;
        public bool showMicrometerReticle = true;
        public bool showAnnotations = true;

        [Header("Hóa chất & Thí nghiệm trên tiêu bản")]
        public string appliedStain = ""; // "lugol", "methylene_blue", "gram"
        public float plasmolysisFactor = 0f; // 0..1
        public bool plasmolysisActive = false;
        public bool stomaOpen = true;

        [Header("Tiêu bản đang đặt trên bàn kính")]
        public GlassSlide mountedSlide;
        public BioSample slideSample;
        public string activeSlideCode = "bieu_bi_hanh";

        [Header("Núm điều khiển 3D")]
        public BioKnob coarseKnob;
        public BioKnob fineKnob;
        public BioKnob stageXKnob;
        public BioKnob stageYKnob;
        public BioKnob brightnessKnob;
        public BioKnob irisKnob;
        public BioButton lampButton;

        static readonly ObjectiveType[] Objectives =
        {
            ObjectiveType.Scan4x, ObjectiveType.Low10x, ObjectiveType.High40x, ObjectiveType.Oil100x
        };

        bool turning;
        bool inEyepieceView;
        Texture2D viewportTex;
        static Color32[] sharedPixelBuf;
        const int TexSize = 1024;
        const int GammaSteps = 1024;
        static float[] gammaLut;
        static float gammaLutFor = float.NaN;
        int viewportVersion, streamedVersion = -1;
        bool texDirty = true;
        float lastRedrawTime;
        float lastStreamTime;

        public string TargetName => isTeacherCamera ? "Kính hiển vi kỹ thuật số giáo viên" : "Kính hiển vi quang học hai mắt";

        public string UseHint
        {
            get
            {
                var (mag, na, color, isOil) = Optics.GetObjectiveProps(Objectives[currentObjectiveIndex]);
                double total = Optics.TotalMagnification(eyepieceMag, mag);
                double fov = Optics.FieldOfViewMm(fieldNumberMm, mag);
                string slideInfo = slideSample != null && !slideSample.IsEmpty ? slideSample.sampleName : activeSlideCode;
                return $"Kính hiển vi: vật kính {BioFeedback.FormatVi(mag, "0")}× (vòng {color}, NA {BioFeedback.FormatVi(na, "0.00")}{(isOil ? ", dầu soi" : "")}), phóng đại {BioFeedback.FormatVi(total, "0")}×, thị trường {BioFeedback.FormatVi(fov, "0.##")} mm · Mẫu: {slideInfo}. Bấm để mở màn hình soi kính.";
            }
        }

        public double CurrentMagnification => Optics.TotalMagnification(eyepieceMag, Optics.GetObjectiveProps(Objectives[currentObjectiveIndex]).mag);
        public double CurrentFovMm => Optics.FieldOfViewMm(fieldNumberMm, Optics.GetObjectiveProps(Objectives[currentObjectiveIndex]).mag);
        public float FocusOffsetMm { get; private set; }

        void Awake()
        {
            if (stageTransform == null && stage != null) stageTransform = stage;
            if (stage == null && stageTransform != null) stage = stageTransform;
            if (stageSocket == null) stageSocket = GetComponentInChildren<PlaceSocket>(true);
            if (stageSocket != null)
            {
                stageSocket.Changed += OnStageSocketChanged;
                if (stageSocket.Occupant != null)
                {
                    var gs = stageSocket.Occupant.GetComponent<GlassSlide>();
                    if (gs != null) mountedSlide = gs;
                }
            }
            if (slideSample == null)
            {
                slideSample = GetComponent<BioSample>();
                if (slideSample == null) slideSample = gameObject.AddComponent<BioSample>();
                slideSample.sampleId = activeSlideCode;
                slideSample.sampleName = "Tiêu bản biểu bì vảy hành";
                slideSample.specimenId = activeSlideCode;
                slideSample.hasCoverslip = true;
                slideSample.isSpreadFlat = true;
            }
            RecalculateFocusOffset();
        }

        void Start()
        {
            if (isTeacherCamera)
                RenderViewportIfNeeded(true);
        }

        void OnStageSocketChanged(Grabbable occupant)
        {
            if (occupant != null)
            {
                var gs = occupant.GetComponent<GlassSlide>();
                if (gs != null) MountSlide(gs);
            }
            else
            {
                mountedSlide = null;
                texDirty = true;
                RenderViewportIfNeeded(true);
            }
        }

        public static readonly (string id, string title)[] StandardSlides =
        {
            ("bieu_bi_hanh", "1. Biểu bì vảy hành tím (Allium cepa)"),
            ("co_nguyen_sinh", "2. Co & Phản co nguyên sinh (NaCl 5%)"),
            ("nguyen_phan_re_hanh", "3. Nguyên phân chóp rễ hành (Allium cepa)"),
            ("khi_khong_la_thao", "4. Khí khổng lá thài lài tía (Tradescantia)"),
            ("vi_khuan_gram", "5. Vi khuẩn nhuộm Gram (+/-)"),
            ("nam_men", "6. Nấm men Saccharomyces cerevisiae nảy chồi"),
            ("tb_niem_mac_mieng", "7. Tế bào niêm mạc miệng người"),
            ("tb_co_van", "8. Mô cơ vân (vân ngang sáng – tối)"),
            ("tb_than_kinh", "9. Nơron thần kinh đa cực"),
            ("tieu_ban_mau", "10. Tiêu bản máu người (hồng cầu & bạch cầu)"),
            ("ecoli_gfp", "11. Khuẩn lạc E. coli pUC19-GFP phát huỳnh quang")
        };

        /// <summary>Bấm vào kính: mở màn hình soi (MicroscopeUI) với đủ các việc chính của một buổi soi kính.</summary>
        public void Use() => MicroscopeUI.Open(this);

        /// <summary>Màn hình soi báo đang mở / đã đóng: lúc mở thì vẽ lại thị trường ngay.</summary>
        public void SetViewing(bool viewing)
        {
            inEyepieceView = viewing;
            if (viewing)
            {
                texDirty = true;
                RenderViewportIfNeeded(true);
            }
        }

        /// <summary>Vẽ lại thị trường nếu có thay đổi (force: vẽ ngay, bỏ qua giãn cách 0,1 giây).</summary>
        public void Refresh(bool force)
        {
            if (force) texDirty = true;
            RenderViewportIfNeeded(force);
        }

        public void SetBrightness(float value)
        {
            brightness = Mathf.Clamp01(value);
            texDirty = true;
        }

        /// <summary>Đặt thuốc nhuộm đang dùng: "" (không nhuộm), "lugol", "methylene_blue" hoặc "gram".</summary>
        public void SetStain(string stainId)
        {
            stainId ??= "";
            if (appliedStain == stainId) return;
            appliedStain = stainId;
            BioFeedback.PlaySuccess();
            string msg = stainId == "lugol" ? "Đã nhỏ Lugol (I-KI): nhân và hạt tinh bột bắt màu nâu đậm."
                       : stainId == "methylene_blue" ? "Đã nhuộm xanh methylen: nhân tế bào bắt màu xanh lam đậm."
                       : stainId == "gram" ? "Đã nhuộm Gram: vi khuẩn Gram dương màu tím, Gram âm màu hồng đỏ."
                       : "Đã rửa sạch thuốc nhuộm.";
            BioFeedback.ShowToast(msg, false);
            BioFeedback.LogAction("kinh_hien_vi", stainId.Length > 0 ? "nhuom_" + stainId : "rua_thuoc_nhuom", msg);
            texDirty = true;
            RenderViewportIfNeeded(true);
        }

        public void OpenTeacherCameraProgram()
        {
            var prog = new MicroscopeProgram(this);
            InstrumentScreen.Open(null, prog, null);
        }

        public void SetObjective(int index)
        {
            index = Mathf.Clamp(index, 0, Objectives.Length - 1);
            if (index == currentObjectiveIndex) return;
            int prev = currentObjectiveIndex;
            currentObjectiveIndex = index;
            CheckObjectiveSafety(prev, currentObjectiveIndex);
            BioFeedback.PlayClick();
            texDirty = true;
            RenderViewportIfNeeded(true);
            if (gameObject.activeInHierarchy)
                StartCoroutine(Turn());
            else if (nosepiece != null)
                nosepiece.localRotation = Quaternion.Euler(0, currentObjectiveIndex * 90f, 0);
        }

        // Tên hàm cũ (bảng IMGUI "ghé mắt thị kính") giữ lại cho mã gọi sẵn: nay đều mở / đóng màn hình soi mới.
        public void ToggleEyepieceView()
        {
            if (inEyepieceView) ExitEyepieceView(); else EnterEyepieceView();
        }

        public void EnterEyepieceView() => MicroscopeUI.Open(this);

        public void ExitEyepieceView()
        {
            if (MicroscopeUI.IsOpen) MicroscopeUI.Instance.Close();
            inEyepieceView = false;
        }

        public void SetFocus(float offsetMm)
        {
            FocusOffsetMm = offsetMm;
            stageHeightMm = optimalStageHeightMm - offsetMm;
            fineUm = 0f;
            texDirty = true;
            RenderViewportIfNeeded(false);
        }

        void RecalculateFocusOffset()
        {
            FocusOffsetMm = (optimalStageHeightMm - stageHeightMm) - (fineUm * 0.001f);
            texDirty = true;
        }

        public void AdjustCoarseFocus(float deltaMm)
        {
            if (currentObjectiveIndex >= 2)
            {
                BioFeedback.ShowToast("Ở vật kính lớn (40× / 100×) chỉ được dùng núm chỉnh tinh (ốc vi cấp)!", true);
                stageHeightMm = Mathf.Clamp(stageHeightMm + deltaMm, 0f, 25f);
                if (stageHeightMm > optimalStageHeightMm + 0.15f)
                {
                    if (slideSample != null) slideSample.isCoverslipCracked = true;
                    if (mountedSlide != null) mountedSlide.CrackSlide();
                    BioFeedback.PlayCrack();
                    BioFeedback.RecordMistake("kinh_hien_vi", "oc_so_cap_lam_vo_lamen",
                        "Vặn ốc sơ cấp ở vật kính 40×/100× làm vật kính đâm vỡ lamen và tiêu bản!",
                        "Ở vật kính 40× (khoảng cách làm việc chỉ ~0,5 mm) và 100× (~0,15 mm), một bước ốc sơ cấp nâng bàn kính quá nhanh sẽ đâm thẳng đầu thấu kính vào lamen gây vỡ kính và xước vật kính.");
                }
            }
            else
            {
                stageHeightMm = Mathf.Clamp(stageHeightMm + deltaMm, 0f, 25f);
                BioFeedback.PlayClick();
            }
            if (stageTransform != null)
            {
                Vector3 lp = stageTransform.localPosition;
                lp.y = 0.135f + (stageHeightMm - 12.5f) * 0.001f;
                stageTransform.localPosition = lp;
            }
            RecalculateFocusOffset();
            RenderViewportIfNeeded(false);
        }

        public void AdjustFineFocus(float deltaUm)
        {
            fineUm = Mathf.Clamp(fineUm + deltaUm, -500f, 500f);
            BioFeedback.PlayClick();
            RecalculateFocusOffset();
            RenderViewportIfNeeded(false);
        }

        public void AdjustStageXY(float dxMm, float dyMm)
        {
            stageX = Mathf.Clamp(stageX + dxMm, -15f, 15f);
            stageY = Mathf.Clamp(stageY + dyMm, -15f, 15f);
            texDirty = true;
            RenderViewportIfNeeded(false);
        }

        public void MountSlide(GlassSlide slide)
        {
            if (slide == null) return;
            mountedSlide = slide;
            if (slide.sample != null)
            {
                slideSample = slide.sample;
                activeSlideCode = !string.IsNullOrEmpty(slideSample.specimenId) ? slideSample.specimenId : slideSample.sampleId;
            }
            oilApplied = slideSample != null && slideSample.hasImmersionOil;
            texDirty = true;
            RenderViewportIfNeeded(true);
            BioFeedback.PlayClick();
            BioFeedback.LogAction("kinh_hien_vi", "dat_tieu_ban", $"Đã kẹp tiêu bản '{slide.SlideTitle}' lên bàn sa trượt kính hiển vi.");
        }

        public void LoadSlideById(string slideId)
        {
            if (string.IsNullOrEmpty(slideId)) return;
            activeSlideCode = slideId;
            texDirty = true;
            RenderViewportIfNeeded(true);
        }

        public void ApplyImmersionOil()
        {
            oilApplied = true;
            if (slideSample != null) slideSample.hasImmersionOil = true;
            texDirty = true;
            RenderViewportIfNeeded(true);
            BioFeedback.PlayBubblePop();
            BioFeedback.LogAction("lo_dau_soi", "nho_dau_soi_100x",
                "Đã nhỏ 1 giọt dầu soi (n = 1,515) lên lamen để hội tụ tia sáng cho vật kính 100× (NA 1,25).");
        }

        public void CleanOilWithLensTissue()
        {
            oilApplied = false;
            objective40xOilDirty = false;
            if (slideSample != null) slideSample.hasImmersionOil = false;
            texDirty = true;
            RenderViewportIfNeeded(true);
            BioFeedback.PlaySuccess();
            BioFeedback.LogAction("giay_lau_kinh", "lau_dau_vat_kinh",
                "Đã dùng giấy lau kính chuyên dụng thấm sạch dầu soi trên vật kính và mặt tiêu bản.");
        }

        public void CleanObjectiveWithLensTissue() => CleanOilWithLensTissue();

        void CheckObjectiveSafety(int prev, int next)
        {
            if (prev == 3 && next == 2 && oilApplied)
            {
                objective40xOilDirty = true;
                BioFeedback.RecordMistake("kinh_hien_vi", "vat_kinh_40x_dinh_dau",
                    "Quay từ vật kính 100× có dầu về vật kính 40× khi chưa lau sạch dầu soi!",
                    "Vật kính 40× là vật kính khô, nếu quệt vào giọt dầu soi trên lamen sẽ làm lem thấu kính khiến ảnh mờ đục. Cần dùng giấy lau kính lau sạch.");
            }
            if (next == 3 && !oilApplied)
            {
                BioFeedback.RecordMistake("kinh_hien_vi", "vat_kinh_100x_thieu_dau",
                    "Chuyển sang vật kính 100× (vòng trắng) nhưng CHƯA nhỏ dầu soi immersion!",
                    "Khi không có dầu soi (n = 1,515) nối giữa lamen và vật kính 100×, tia sáng bị phản xạ toàn phần ở khe không khí (n = 1,0) làm ảnh tối và mờ nhòe.");
            }
            if (next >= 2 && slideSample != null && !slideSample.hasCoverslip)
            {
                BioFeedback.RecordMistake("kinh_hien_vi", "soi_40x_khong_day_lamen",
                    "Chuyển sang vật kính 40× khi tiêu bản chưa đậy lamen!",
                    "Khoảng cách làm việc của vật kính 40× rất ngắn, không đậy lamen sẽ làm đầu vật kính chạm trực tiếp vào giọt nước/thuốc nhuộm.");
            }
        }

        public void CycleObjective()
        {
            if (turning) return;
            int prev = currentObjectiveIndex;
            currentObjectiveIndex = (currentObjectiveIndex + 1) % Objectives.Length;
            CheckObjectiveSafety(prev, currentObjectiveIndex);

            BioFeedback.PlayClick();
            texDirty = true;
            RenderViewportIfNeeded(true);
            if (gameObject.activeInHierarchy)
                StartCoroutine(Turn());
            else if (nosepiece != null)
                nosepiece.localRotation = Quaternion.Euler(0, currentObjectiveIndex * 90f, 0);
        }

        IEnumerator Turn()
        {
            if (nosepiece == null) yield break;
            turning = true;
            var from = nosepiece.localRotation;
            var to = Quaternion.Euler(0, currentObjectiveIndex * 90f, 0);
            for (float t = 0; t < 0.25f; t += Time.deltaTime)
            {
                nosepiece.localRotation = Quaternion.Slerp(from, to, Easing.Standard(t / 0.25f));
                yield return null;
            }
            nosepiece.localRotation = to;
            turning = false;
        }

        public bool CanApplyTool(BioTool tool, out string promptOrReason)
        {
            if (tool == null)
            {
                promptOrReason = UseHint;
                return true;
            }
            if (tool.kind == BioToolKind.ImmersionOil || tool.toolId == "lo_dau_soi")
            {
                promptOrReason = "Nhỏ 1 giọt dầu soi kính (n = 1,515) lên tiêu bản trên bàn kính";
                return true;
            }
            if (tool.toolId.Contains("nacl") || tool.toolId.Contains("uu_truong"))
            {
                promptOrReason = "Nhỏ dung dịch ưu trương NaCl 5% (Thí nghiệm Co nguyên sinh)";
                return true;
            }
            if (tool.toolId.Contains("nuoc_cat") || tool.toolId.Contains("nhuoc_truong") || tool.toolId.Contains("water"))
            {
                promptOrReason = "Nhỏ nước cất nhược trương (Thí nghiệm Phản co nguyên sinh)";
                return true;
            }
            if (tool.toolId.Contains("lugol") || tool.toolId.Contains("iod"))
            {
                promptOrReason = "Nhỏ thuốc thử Lugol (I-KI) nhuộm nhân và tinh bột";
                return true;
            }
            if (tool.toolId.Contains("methylen") || tool.toolId.Contains("xanh"))
            {
                promptOrReason = "Nhỏ thuốc nhuộm Xanh Methylene nhuộm nhân tế bào";
                return true;
            }
            if (tool.toolId.Contains("gram"))
            {
                promptOrReason = "Nhuộm phân biệt vi khuẩn Gram(+)/Gram(-)";
                return true;
            }
            if (tool.toolId == "giay_lau_kinh" || tool.kind == BioToolKind.BlottingPaper)
            {
                promptOrReason = "Lau sạch dầu soi trên vật kính bằng giấy lau kính";
                return true;
            }
            var gs = tool.GetComponent<GlassSlide>();
            if (gs != null || tool.kind == BioToolKind.SlideSample || tool.toolId == "lam_kinh")
            {
                promptOrReason = "Đặt tiêu bản lam kính lên bàn sa trượt";
                return true;
            }
            promptOrReason = UseHint;
            return false;
        }

        public float GetRequiredHoldSeconds(BioTool tool) => 0f;
        public void OnToolApplyStep(BioTool tool, float dt, float progress01) { }

        public void OnToolApplyComplete(BioTool tool)
        {
            if (tool == null) { Use(); return; }
            if (tool.kind == BioToolKind.ImmersionOil || tool.toolId == "lo_dau_soi")
            {
                ApplyImmersionOil();
                return;
            }
            if (tool.toolId.Contains("nacl") || tool.toolId.Contains("uu_truong"))
            {
                ApplyReagent("nacl");
                return;
            }
            if (tool.toolId.Contains("nuoc_cat") || tool.toolId.Contains("nhuoc_truong") || tool.toolId.Contains("water"))
            {
                ApplyReagent("nuoc_cat");
                return;
            }
            if (tool.toolId.Contains("lugol") || tool.toolId.Contains("iod"))
            {
                ApplyReagent("lugol");
                return;
            }
            if (tool.toolId.Contains("methylen") || tool.toolId.Contains("xanh"))
            {
                ApplyReagent("methylene_blue");
                return;
            }
            if (tool.toolId.Contains("gram"))
            {
                ApplyReagent("gram");
                return;
            }
            if (tool.toolId == "giay_lau_kinh")
            {
                CleanOilWithLensTissue();
                return;
            }
            var gs = tool.GetComponent<GlassSlide>();
            if (gs != null)
            {
                MountSlide(gs);
                return;
            }
            if (tool.kind == BioToolKind.SlideSample || tool.toolId == "lam_kinh")
            {
                var bs = tool.GetComponent<BioSample>();
                if (bs != null)
                {
                    slideSample = bs;
                    activeSlideCode = !string.IsNullOrEmpty(bs.specimenId) ? bs.specimenId : bs.sampleId;
                    texDirty = true;
                    RenderViewportIfNeeded(true);
                    BioFeedback.PlaySuccess();
                }
            }
        }

        public void ApplyReagent(string reagentId)
        {
            if (reagentId.Contains("nacl") || reagentId.Contains("uu_truong"))
            {
                plasmolysisActive = true;
                BioFeedback.PlayBubblePop();
                BioFeedback.ShowToast("Đã nhỏ NaCl 5 %: Nước rút khỏi không bào, tế bào bắt đầu co nguyên sinh!", false);
                BioFeedback.LogAction("kinh_hien_vi", "nho_nacl_5_phan_tram", "Nhỏ dung dịch ưu trương NaCl 5% lên tiêu bản để quan sát hiện tượng co nguyên sinh.");
            }
            else if (reagentId.Contains("nuoc_cat") || reagentId.Contains("nhuoc_truong") || reagentId.Contains("water"))
            {
                plasmolysisActive = false;
                BioFeedback.PlayBubblePop();
                BioFeedback.ShowToast("Đã nhỏ Nước cất: Nước thẩm thấu vào không bào, tế bào phản co nguyên sinh căng phồng trở lại!", false);
                BioFeedback.LogAction("kinh_hien_vi", "nho_nuoc_cat", "Nhỏ nước cất nhược trương lên tiêu bản để quan sát hiện tượng phản co nguyên sinh.");
            }
            else if (reagentId.Contains("lugol") || reagentId.Contains("iod"))
            {
                appliedStain = appliedStain == "lugol" ? "" : "lugol";
                BioFeedback.PlaySuccess();
                BioFeedback.ShowToast(appliedStain == "lugol" ? "Đã nhỏ Lugol (I-KI): Nhân và hạt tinh bột bắt màu nâu đậm rõ nét!" : "Đã rửa sạch thuốc thử Lugol.", false);
                BioFeedback.LogAction("kinh_hien_vi", "nhuom_lugol", "Nhuộm tiêu bản bằng thuốc thử Lugol.");
            }
            else if (reagentId.Contains("methylen") || reagentId.Contains("xanh"))
            {
                appliedStain = appliedStain == "methylene_blue" ? "" : "methylene_blue";
                BioFeedback.PlaySuccess();
                BioFeedback.ShowToast(appliedStain == "methylene_blue" ? "Đã nhuộm Xanh Methylene: Nhân tế bào bắt màu xanh lam coban đậm!" : "Đã rửa sạch phẩm nhuộm Xanh Methylene.", false);
                BioFeedback.LogAction("kinh_hien_vi", "nhuom_methylene_blue", "Nhuộm tiêu bản bằng Xanh Methylene.");
            }
            else if (reagentId.Contains("gram"))
            {
                appliedStain = appliedStain == "gram" ? "" : "gram";
                BioFeedback.PlaySuccess();
                BioFeedback.ShowToast(appliedStain == "gram" ? "Đã thực hiện quy trình nhuộm Gram: Vi khuẩn Gram(+) màu tím, Gram(-) màu hồng đỏ!" : "Đã rửa sạch thuốc nhuộm Gram.", false);
                BioFeedback.LogAction("kinh_hien_vi", "nhuom_gram", "Nhuộm phân biệt vi khuẩn Gram.");
            }
            texDirty = true;
            RenderViewportIfNeeded(true);
        }

        /// <summary>Đưa tiêu bản về trạng thái chưa nhỏ NaCl (bắt đầu lượt thực hành mới), không báo, không ghi nhật ký.</summary>
        public void ResetPlasmolysis()
        {
            if (!plasmolysisActive && plasmolysisFactor <= 0f) return;
            plasmolysisActive = false;
            plasmolysisFactor = 0f;
            texDirty = true;
        }

        public void StreamToVideoWall()
        {
            RenderViewportIfNeeded(true);
            var screen = LabScreen.Find("t2_tuong_video_2");
            if (screen != null && viewportTex != null)
            {
                streamedVersion = viewportVersion;
                screen.ShowImage(viewportTex, StreamCaption);
                BioFeedback.PlaySuccess();
                BioFeedback.ShowToast("Đã phát trực tiếp hình ảnh kính hiển vi lên Màn hình Tường video Tầng 2!", false);
                BioFeedback.LogAction("kinh_hien_vi_camera", "chieu_tuong_video", "Truyền trực tiếp hình ảnh hiển vi lên màn hình tường video 75 inch.");
            }
            else
            {
                BioFeedback.ShowToast("Đã đồng bộ tín hiệu HDMI sang màn hình lớp học.", false);
            }
        }

        public double MicronsPerDivision => Objectives[currentObjectiveIndex] switch
        {
            ObjectiveType.Scan4x => 25.0,
            ObjectiveType.Low10x => 10.0,
            ObjectiveType.High40x => 2.5,
            ObjectiveType.Oil100x => 1.0,
            _ => 10.0
        };

        public string CaptureMicrographPng()
        {
            RenderViewportIfNeeded(true);
            if (viewportTex == null) return "";
            try
            {
                string dir = Path.Combine(Application.persistentDataPath, "anh_hien_vi");
                Directory.CreateDirectory(dir);
                string fileName = $"hien_vi_{activeSlideCode}_{(int)CurrentMagnification}x.png";
                string fullPath = Path.Combine(dir, fileName);
                File.WriteAllBytes(fullPath, viewportTex.EncodeToPNG());
                BioFeedback.PlaySuccess();
                BioFeedback.LogAction("kinh_hien_vi_camera", "chup_anh_hien_vi", $"Đã lưu ảnh chụp hiển vi HD: {fileName}");
                StreamToVideoWall();
                return fileName;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[MicroscopeController] Lỗi lưu ảnh: {e.Message}");
                return "";
            }
        }

        public Texture2D GetViewportTexture()
        {
            RenderViewportIfNeeded(viewportTex == null);
            return viewportTex;
        }

        /// <summary>Tăng 1 sau mỗi lần thị trường được vẽ lại: nơi dùng ảnh so số này để biết có gì mới.</summary>
        public int ViewportVersion => viewportVersion;

        /// <summary>
        /// Chép thị trường đang soi (thu nhỏ, lấy trung bình 2×2) vào ô vuông size × size của một ảnh khác, góc dưới trái
        /// tại (x0, y0). Phần nền đen ngoài vành thị kính được bỏ qua. Màn hình phần mềm camera dùng hàm này để hiện
        /// ĐÚNG ảnh của kính (nhuộm, co nguyên sinh, đèn, dầu soi, nét) thay vì tự vẽ một bản riêng thiếu các hiệu ứng đó.
        /// </summary>
        public bool BlitViewport(Color32[] dst, int dstW, int dstH, int x0, int y0, int size)
        {
            RenderViewportIfNeeded(viewportTex == null);
            if (viewportTex == null || dst == null || size <= 0) return false;
            var src = viewportTex.GetPixelData<Color32>(0);
            if (src.Length < TexSize * TexSize) return false;
            float step = TexSize / (float)size;
            for (int y = 0; y < size; y++)
            {
                int ty = y0 + y;
                if ((uint)ty >= (uint)dstH) continue;
                int sy = Mathf.Min(TexSize - 2, (int)(y * step));
                int row0 = sy * TexSize, row1 = row0 + TexSize;
                for (int x = 0; x < size; x++)
                {
                    int tx = x0 + x;
                    if ((uint)tx >= (uint)dstW) continue;
                    int sx = Mathf.Min(TexSize - 2, (int)(x * step));
                    Color32 a = src[row0 + sx], b = src[row0 + sx + 1], c = src[row1 + sx], d = src[row1 + sx + 1];
                    if (a.r == 8 && a.g == 10 && a.b == 14 && d.r == 8 && d.g == 10 && d.b == 14) continue;  // nền ngoài vành
                    dst[ty * dstW + tx] = new Color32((byte)((a.r + b.r + c.r + d.r) >> 2), (byte)((a.g + b.g + c.g + d.g) >> 2), (byte)((a.b + b.b + c.b + d.b) >> 2), 255);
                }
            }
            return true;
        }

        public double ComputeCurrentBlurFactor()
        {
            var (mag, na, _, isOil) = Optics.GetObjectiveProps(Objectives[currentObjectiveIndex]);
            double totalMag = Optics.TotalMagnification(eyepieceMag, mag);
            double dofUm = Optics.DepthOfFieldUm(totalMag, na, isOil && oilApplied);
            double focalOffsetUm = FocusOffsetMm * 1000.0;
            double blur = Optics.BlurFactor(focalOffsetUm, dofUm);
            if (isOil && !oilApplied) blur += 2.5;
            if (currentObjectiveIndex == 2 && objective40xOilDirty) blur += 2.0;
            return blur;
        }

        public double ComputeMeasuredCellSizeUm()
        {
            var slideRec = BioCatalogLoader.GetSlide(activeSlideCode);
            if (slideRec != null)
                return 0.5 * (slideRec.minUm + slideRec.maxUm);
            return 220.0; // kích thước tế bào biểu bì hành trung bình ~220 µm
        }

        void RenderViewportIfNeeded(bool force)
        {
            if (!force && !texDirty) return;
            // giờ thực: lúc trò chơi tạm dừng (timeScale = 0) vẫn phải vẽ lại khi người dùng chỉnh kính
            if (!force && Time.unscaledTime - lastRedrawTime < 0.1f) return;
            lastRedrawTime = Time.unscaledTime;
            texDirty = false;

            if (viewportTex == null)
            {
                viewportTex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.DontSave,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
            }
            // Một bộ đệm dùng chung cho mọi kính (chỉ vẽ trên luồng chính, lần lượt từng kính):
            // trước đây mỗi kính đã soi giữ riêng 4 MB, 29 kính là 116 MB bộ nhớ chỉ để làm nháp.
            sharedPixelBuf ??= new Color32[TexSize * TexSize];
            Color32[] buf = sharedPixelBuf;

            var (mag, _, _, isOil) = Optics.GetObjectiveProps(Objectives[currentObjectiveIndex]);
            float blur = Mathf.Clamp((float)ComputeCurrentBlurFactor(), 0f, 4f);
            float lightGain = lampOn ? brightness * Mathf.Lerp(0.4f, 1.25f, irisOpen) * Mathf.Lerp(0.5f, 1.15f, condenserHeight) : 0.04f;

            // Vật kính 100x dầu soi: khi thiếu dầu (n = 1.0 không khí so với n = 1.515 thủy tinh) tia sáng bị tán xạ mạnh
            bool dryOil = isOil && !oilApplied;
            if (dryOil) lightGain *= 0.25f;

            // Mọi phép so chuỗi làm MỘT lần ở đây. Bản cũ so tên tiêu bản khoảng 20 lần cho từng điểm ảnh
            // (680 nghìn điểm ảnh mỗi lần vẽ), nên mỗi lần chỉnh nét hay kéo bàn sa trượt là khựng hình.
            string code = activeSlideCode ?? "bieu_bi_hanh";
            string sampleStain = slideSample != null && slideSample.stainId != null ? slideSample.stainId : "";
            bool stainedBlue = sampleStain.Contains("methylen") || code.Contains("xanh") || code.Contains("niem_mac") || appliedStain == "methylene_blue";
            bool stainedLugol = sampleStain.Contains("lugol") || appliedStain == "lugol";
            bool ecoli = code.Contains("ecoli") || code.Contains("e_coli");
            bool gramPosOnly = code.Contains("sua_chua") || code.Contains("subtilis");
            // Thuốc nhuộm Gram chỉ có nghĩa trên tiêu bản vi khuẩn: nhỏ lên biểu bì hành thì hành vẫn là hành
            // (bản cũ biến mọi tiêu bản thành ảnh vi khuẩn).
            bool bacterialSlide = ecoli || gramPosOnly || code.Contains("gram") || code.Contains("vi_khuan");
            bool isGram = code.Contains("gram") || (appliedStain == "gram" && bacterialSlide);
            // Tiêu bản Gram hỗn hợp (không nêu rõ loài) có cả hai loại: tím (+) xen hồng đỏ (-).
            bool stainedGramPos = gramPosOnly || (isGram && !ecoli);
            bool stainedGramNeg = ecoli || (isGram && !gramPosOnly);
            bool isStomata = code.Contains("thai_lai") || code.Contains("khi_khong");
            bool isSarcomere = code.Contains("co_van") || code.Contains("sarcomere");
            bool isMouth = code.Contains("niem_mac") || code.Contains("mieng");
            bool isYeast = code.Contains("nam_men") || code.Contains("yeast");
            bool isGfp = code.Contains("gfp");
            SpecimenKind kind = ClassifySpecimen(code, isStomata, stainedGramPos, stainedGramNeg, isSarcomere, isMouth, isYeast);
            bool hasBubbles = slideSample != null && slideSample.hasAirBubbles;
            bool isCracked = slideSample != null && slideSample.isCoverslipCracked;
            bool reticle = showMicrometerReticle;
            bool stomaOpenNow = stomaOpen;

            float plasmo = 0f;
            if (mountedSlide != null)
            {
                plasmo = mountedSlide.plasmolysisFraction;
                if (plasmo <= 0.01f && mountedSlide.naclPercent > 0.9f)
                    plasmo = (float)Osmosis.PlasmolysisFraction(Osmosis.NaClPercentToMolar(mountedSlide.naclPercent));
            }
            else if (slideSample != null && slideSample.concentrationPct > 1.2f)
            {
                plasmo = (float)Osmosis.PlasmolysisFraction(Osmosis.NaClPercentToMolar(slideSample.concentrationPct));
            }
            else if (code.Contains("co_nguyen_sinh"))
            {
                plasmo = (float)Osmosis.PlasmolysisFraction(Osmosis.NaClPercentToMolar(5.0));
            }
            float effectivePlasmo = Mathf.Max(plasmolysisFactor, plasmo);

            float scale = (float)mag / 10f;
            float cx = TexSize * 0.5f, cy = TexSize * 0.5f, rMax = TexSize * 0.465f;
            float rOuter = rMax + 8f;
            float rOuter2 = rOuter * rOuter;
            float invRMax = 1.0f / rMax;
            float invScale = 1.0f / Mathf.Max(0.35f, scale);
            float uOffset = stageX * 0.035f;
            float vOffset = stageY * 0.035f;

            Color32 vignetteColor = new Color32(8, 10, 14, 255);
            Color vignetteColorF = vignetteColor;
            float chromaticFringe = Mathf.Clamp(FocusOffsetMm * 60f, -0.15f, 0.15f) * 0.12f;
            float reticleHalfW = rMax * 0.72f;
            float reticleTotalW = reticleHalfW * 2f;
            bool blurred = blur > 0.08f;
            float blurMix = Mathf.Clamp01(blur * 0.35f);
            Color blurAvg = isGfp ? new Color(0.04f, 0.14f, 0.06f)
                          : (stainedBlue ? new Color(0.72f, 0.82f, 0.94f) : (stainedLugol ? new Color(0.92f, 0.88f, 0.76f) : new Color(0.88f, 0.84f, 0.90f)));
            Color dryOilHaze = new Color(0.28f, 0.28f, 0.32f);
            Color bezelBase = new Color(0.09f, 0.11f, 0.15f, 1f), bezelShine = new Color(0.26f, 0.30f, 0.38f);

            // Tụ quang & màn chắn sáng (Iris Diaphragm): độ mở đổi độ tương phản. Tính sẵn bảng tra thay cho
            // ba phép lũy thừa trên mỗi điểm ảnh (ảnh ra 8 bit mỗi kênh nên 1025 nấc là thừa đủ).
            float irisContrast = Mathf.Lerp(1.35f, 0.85f, irisOpen);
            float[] gamma = gammaLut ??= new float[GammaSteps + 1];
            if (gammaLutFor != irisContrast)
            {
                for (int i = 0; i <= GammaSteps; i++) gamma[i] = Mathf.Pow(i / (float)GammaSteps, irisContrast);
                gammaLutFor = irisContrast;
            }

            void DrawRow(int y)
            {
                float dy = y - cy;
                float dy2 = dy * dy;
                int rowOffset = y * TexSize;

                if (dy2 >= rOuter2)
                {
                    for (int x = 0; x < TexSize; x++)
                        buf[rowOffset + x] = vignetteColor;
                    return;
                }

                float halfW = Mathf.Sqrt(rOuter2 - dy2);
                int x0 = Mathf.Max(0, Mathf.CeilToInt(cx - halfW));
                int x1 = Mathf.Min(TexSize - 1, Mathf.FloorToInt(cx + halfW));

                for (int x = 0; x < x0; x++)
                    buf[rowOffset + x] = vignetteColor;

                float v = (dy * invRMax) * invScale + vOffset;
                bool onAxis = Mathf.Abs(dy) < 1.4f;

                for (int x = x0; x <= x1; x++)
                {
                    float dx = x - cx;
                    float dist = Mathf.Sqrt(dx * dx + dy2);
                    // Khử răng cưa 2 điểm ảnh ở mép vòng tròn thị kính và vành kim loại
                    float innerMask = Mathf.Clamp01((rMax - dist) * 0.5f);
                    float bezelMask = Mathf.Clamp01((rOuter - dist) * 0.5f) * (1f - innerMask);

                    if (innerMask <= 0f && bezelMask <= 0f)
                    {
                        buf[rowOffset + x] = vignetteColor;
                        continue;
                    }

                    // Màu vành kim loại chỉ cần ở dải mép (vài điểm ảnh), không tính cho cả thị trường
                    Color bezelCol = bezelBase;
                    if (innerMask < 1f)
                    {
                        float hl = Mathf.Clamp01(1f - Mathf.Abs(dy - dx * 0.45f) * invRMax) * 0.14f;
                        bezelCol = Color.Lerp(bezelBase, bezelShine, hl);
                        if (innerMask <= 0f)
                        {
                            buf[rowOffset + x] = Color.Lerp(vignetteColorF, bezelCol, bezelMask);
                            continue;
                        }
                    }

                    float u = (dx * invRMax) * invScale + uOffset;
                    Color c = SampleSpecimenColor(kind, u, v, stainedBlue, stainedLugol, stainedGramPos, stainedGramNeg, effectivePlasmo, stomaOpenNow);

                    // Tán xạ & mờ khi dùng 100x mà thiếu dầu soi
                    if (dryOil) c = Color.Lerp(c, dryOilHaze, 0.52f);

                    // Độ mờ tiêu cự & viền sắc sai quang học
                    if (blurred)
                    {
                        c = Color.Lerp(c, blurAvg, blurMix);
                        c.r = Mathf.Clamp01(c.r + chromaticFringe);
                        c.b = Mathf.Clamp01(c.b - chromaticFringe);
                    }

                    c.r = gamma[(int)(Mathf.Clamp01(c.r) * GammaSteps + 0.5f)];
                    c.g = gamma[(int)(Mathf.Clamp01(c.g) * GammaSteps + 0.5f)];
                    c.b = gamma[(int)(Mathf.Clamp01(c.b) * GammaSteps + 0.5f)];

                    // Bọt khí dưới lamen
                    if (hasBubbles)
                    {
                        float bx = dx - 24f, by = dy - 18f;
                        float br2 = bx * bx + by * by;
                        if (br2 > 22f * 22f && br2 < 28f * 28f) c = new Color(0.04f, 0.04f, 0.06f);
                        else if (br2 <= 22f * 22f) c = new Color(0.96f, 0.98f, 1.0f);
                    }

                    // Vết nứt lamen do vặn ốc sơ cấp ở 40×/100×
                    if (isCracked && Mathf.Abs(dx + dy * 0.65f) < 2.0f)
                        c = new Color(0.03f, 0.03f, 0.03f);

                    // Thước trắc vi thị kính (0..100 vạch ngang)
                    if (reticle)
                    {
                        float rx = dx + reticleHalfW;
                        if (rx >= 0f && rx <= reticleTotalW)
                        {
                            if (onAxis)
                            {
                                c = Color.Lerp(c, Color.black, 0.88f);   // trục ngang tâm
                            }
                            else if (dy > 0f && dy < 22f)
                            {
                                // Vạch đứng chia độ: vạch 10 dài 22 px, vạch 5 dài 14 px, vạch 1 dài 8 px
                                float div100 = (rx / reticleTotalW) * 100f;
                                float nearestDiv = Mathf.Round(div100);
                                if (Mathf.Abs(div100 - nearestDiv) < 0.11f)
                                {
                                    int divInt = (int)nearestDiv;
                                    float tickLen = (divInt % 10 == 0) ? 22f : ((divInt % 5 == 0) ? 14f : 8f);
                                    if (dy < tickLen) c = Color.Lerp(c, Color.black, 0.88f);
                                }
                            }
                        }
                    }

                    // Tối dần tự nhiên quanh mép thị trường
                    float rr = dist * invRMax;
                    rr *= rr;
                    float gain = Mathf.Clamp01(1f - rr * rr * 0.35f) * lightGain;
                    c.r *= gain; c.g *= gain; c.b *= gain;

                    // Hòa mượt với vành kim loại đen
                    if (innerMask < 1f)
                    {
                        c = Color.Lerp(bezelCol, c, innerMask);
                        if (bezelMask > 0f) c = Color.Lerp(vignetteColorF, c, innerMask + bezelMask);
                    }

                    c.a = 1f;
                    buf[rowOffset + x] = c;
                }

                for (int x = x1 + 1; x < TexSize; x++)
                    buf[rowOffset + x] = vignetteColor;
            }

            // Mỗi hàng điểm ảnh độc lập với nhau nên chia cho các nhân CPU. Bản WebGL không có luồng phụ: vẽ tuần tự.
#if UNITY_WEBGL && !UNITY_EDITOR
            for (int y = 0; y < TexSize; y++) DrawRow(y);
#else
            System.Threading.Tasks.Parallel.For(0, TexSize, DrawRow);
#endif

            viewportTex.SetPixels32(buf);
            viewportTex.Apply(false);
            viewportVersion++;
        }

        internal enum SpecimenKind { OnionEpidermis, PlantHistology, Gfp, Bacteria, Sarcomere, MouthEpithelium, Yeast, Neuron, Blood, Mitosis, Stomata }

        /// <summary>Tên tiêu bản thuộc loại ảnh nào. Thứ tự xét giữ nguyên như chuỗi điều kiện cũ.</summary>
        internal static SpecimenKind ClassifySpecimen(string code, bool isStomata, bool gramPos, bool gramNeg, bool isSarcomere, bool isMouth, bool isYeast)
        {
            code ??= "bieu_bi_hanh";
            if (code.Contains("mo_hoc") || code.Contains("than_re") || code.Contains("carmin") || code.Contains("thuc_vat")) return SpecimenKind.PlantHistology;
            if (code.Contains("gfp")) return SpecimenKind.Gfp;
            if (gramPos || gramNeg || code.Contains("gram") || code.Contains("sua_chua") || code.Contains("subtilis") || code.Contains("ecoli")) return SpecimenKind.Bacteria;
            if (isSarcomere || code.Contains("co_van")) return SpecimenKind.Sarcomere;
            if (isMouth || code.Contains("niem_mac")) return SpecimenKind.MouthEpithelium;
            if (isYeast || code.Contains("nam_men")) return SpecimenKind.Yeast;
            if (code.Contains("than_kinh")) return SpecimenKind.Neuron;
            if (code.Contains("tieu_ban_mau") || code.Contains("hong_cau")) return SpecimenKind.Blood;
            if (code.Contains("nguyen_phan") || code.Contains("chop_re")) return SpecimenKind.Mitosis;
            if (isStomata || code.Contains("thai_lai") || code.Contains("khi_khong")) return SpecimenKind.Stomata;
            return SpecimenKind.OnionEpidermis;
        }

        public static Color SampleSpecimenColor(string code, float u, float v, bool stainedBlue, bool isStomata, bool gramPos, bool gramNeg, float plasmo = 0f)
        {
            return SampleSpecimenColor(code, u, v, stainedBlue, false, isStomata, gramPos, gramNeg, false, false, false, plasmo, true);
        }

        public static Color SampleSpecimenColor(string code, float u, float v, bool stainedBlue, bool stainedLugol, bool isStomata, bool gramPos, bool gramNeg, bool isSarcomere, bool isMouth, bool isYeast, float plasmolysis, bool stomaOpen)
        {
            var kind = ClassifySpecimen(code, isStomata, gramPos, gramNeg, isSarcomere, isMouth, isYeast);
            return SampleSpecimenColor(kind, u, v, stainedBlue, stainedLugol, gramPos, gramNeg, plasmolysis, stomaOpen);
        }

        /// <summary>
        /// Màu một điểm trên tiêu bản. Hàm thuần (không đụng trạng thái, không so chuỗi) nên gọi được từ nhiều luồng
        /// và đủ nhẹ để gọi cho từng điểm ảnh: loại tiêu bản đã được ClassifySpecimen phân loại sẵn một lần.
        /// </summary>
        internal static Color SampleSpecimenColor(SpecimenKind kind, float u, float v, bool stainedBlue, bool stainedLugol, bool gramPos, bool gramNeg, float plasmolysis, bool stomaOpen)
        {

            // 0. Tiêu bản Mô học rễ / thân thực vật nhuộm kép (Carmin - Lục Iod: Libe hồng đỏ, Gỗ xanh lam)
            if (kind == SpecimenKind.PlantHistology)
            {
                float rad = Mathf.Sqrt(u * u + v * v);
                float bundleDist = Mathf.Abs(rad - 0.28f);
                if (bundleDist < 0.12f)
                {
                    if (rad < 0.28f)
                    {
                        float xu = Mathf.Repeat(u * 16f, 1f) - 0.5f;
                        float xv = Mathf.Repeat(v * 16f, 1f) - 0.5f;
                        float xr = Mathf.Sqrt(xu * xu + xv * xv);
                        if (xr < 0.38f)
                        {
                            if (xr > 0.24f) return new Color(0.12f, 0.42f, 0.68f); // Vách hóa gỗ xanh lam
                            return new Color(0.96f, 0.98f, 1.0f);                  // Lòng mạch rỗng sáng
                        }
                        return new Color(0.20f, 0.55f, 0.72f);
                    }
                    else
                    {
                        float lu = Mathf.Repeat(u * 28f, 1f) - 0.5f;
                        float lv = Mathf.Repeat(v * 28f, 1f) - 0.5f;
                        float lr = Mathf.Sqrt(lu * lu + lv * lv);
                        if (lr > 0.35f) return new Color(0.72f, 0.18f, 0.32f);
                        return new Color(0.88f, 0.32f, 0.48f); // Libe đỏ Carmin
                    }
                }
                float pu = Mathf.Repeat(u * 10f, 1f) - 0.5f;
                float pv = Mathf.Repeat(v * 10f, 1f) - 0.5f;
                float pr = Mathf.Sqrt(pu * pu + pv * pv);
                if (pr > 0.44f) return new Color(0.68f, 0.52f, 0.60f);
                return new Color(0.94f, 0.90f, 0.92f);
            }

            // 11. Khuẩn lạc / tế bào E. coli mang pUC19-GFP phát huỳnh quang xanh lục (509 nm)
            if (kind == SpecimenKind.Gfp)
            {
                float gx = Mathf.Repeat(u * 12f, 1f) - 0.5f;
                float gy = Mathf.Repeat(v * 12f, 1f) - 0.5f;
                float r2 = (gx * gx) / 0.045f + (gy * gy) / 0.012f;
                if (r2 < 1f) return Color.Lerp(new Color(0.18f, 1.0f, 0.42f), new Color(0.06f, 0.48f, 0.18f), r2);
                return new Color(0.03f, 0.07f, 0.05f);
            }

            // 1. Tiêu bản Vi khuẩn Gram hoặc Sữa chua (Lactobacillus, E. coli, Bacillus subtilis)
            if (kind == SpecimenKind.Bacteria)
            {
                float gU = u * 24f;
                float gV = v * 24f;
                int bx = Mathf.FloorToInt(gU);
                int by = Mathf.FloorToInt(gV);
                float rx = gU - bx - 0.5f;
                float ry = gV - by - 0.5f;

                float hash = Mathf.Sin(bx * 12.9898f + by * 78.233f) * 43758.5453f;
                hash = hash - Mathf.Floor(hash);
                if (hash > 0.42f)
                {
                    float rotAng = hash * 6.28318f;
                    float cosA = Mathf.Cos(rotAng), sinA = Mathf.Sin(rotAng);
                    float lx = rx * cosA - ry * sinA;
                    float ly = rx * sinA + ry * cosA;
                    bool isRod = Mathf.Abs(lx) < 0.34f && Mathf.Abs(ly) < 0.12f;
                    bool isCoccus = (rx * rx + ry * ry) < 0.08f;
                    if (isRod || isCoccus)
                    {
                        // Gram(+) bắt màu Crystal Violet tím đậm, Gram(-) bắt màu Safranin hồng đỏ
                        bool usePos = gramPos && (!gramNeg || ((bx + by) % 2 == 0));
                        return usePos ? new Color(0.28f, 0.10f, 0.62f) : new Color(0.88f, 0.22f, 0.42f);
                    }
                }
                return new Color(0.96f, 0.96f, 0.97f);
            }

            // 2. Tiêu bản Tế bào cơ vân (Sarcomere: vân sáng I, vân tối A, vạch Z)
            if (kind == SpecimenKind.Sarcomere)
            {
                float fiberV = Mathf.Repeat(v * 4.5f, 1f);
                if (fiberV < 0.06f || fiberV > 0.94f) return new Color(0.92f, 0.88f, 0.90f); // Màng sợi cơ (sarcolemma)

                float myoX = u * 28f;
                float fx = Mathf.Repeat(myoX, 1f);
                bool isZDisc = fx < 0.035f || fx > 0.965f;
                bool isABand = fx >= 0.25f && fx <= 0.75f;
                bool isHZone = fx >= 0.45f && fx <= 0.55f;
                if (isZDisc) return new Color(0.12f, 0.08f, 0.16f); // Vạch Z đậm
                if (isHZone) return new Color(0.52f, 0.30f, 0.45f); // Vùng H sáng hơn
                if (isABand) return new Color(0.40f, 0.20f, 0.35f); // Đĩa A (myosin)
                return new Color(0.82f, 0.65f, 0.78f);             // Đĩa I (actin)
            }

            // 3. Tiêu bản Tế bào niêm mạc miệng người
            if (kind == SpecimenKind.MouthEpithelium)
            {
                float mU = u * 5f, mV = v * 5f;
                int mx = Mathf.FloorToInt(mU), my = Mathf.FloorToInt(mV);
                float mfu = mU - mx - 0.5f, mfv = mV - my - 0.5f;
                float rCell = mfu * mfu * 2.2f + mfv * mfv * 3.1f + Mathf.Sin(mfu * 10f) * 0.02f;
                if (rCell < 0.24f)
                {
                    // Nhân tế bào hình cầu ở trung tâm
                    if (mfu * mfu + mfv * mfv < 0.015f)
                        return stainedBlue ? new Color(0.06f, 0.16f, 0.60f) : new Color(0.48f, 0.20f, 0.40f);
                    // Tế bào chất dẹt, màng nhăn nheo
                    return stainedBlue ? new Color(0.68f, 0.82f, 0.95f) : new Color(0.88f, 0.78f, 0.85f);
                }
                return new Color(0.97f, 0.97f, 0.98f);
            }

            // 4. Tiêu bản Nấm men bánh mì nảy chồi (Yeast - Saccharomyces cerevisiae)
            if (kind == SpecimenKind.Yeast)
            {
                float yU = u * 12f, yV = v * 12f;
                int yx = Mathf.FloorToInt(yU), yy = Mathf.FloorToInt(yV);
                float yfu = yU - yx - 0.5f, yfv = yV - yy - 0.5f;
                float rYeast = yfu * yfu + yfv * yfv;
                if (rYeast < 0.12f)
                {
                    if (rYeast > 0.09f) return new Color(0.25f, 0.22f, 0.20f); // Thành tế bào
                    return new Color(0.85f, 0.82f, 0.72f);                    // Tế bào chất nấm men
                }
                // Chồi con (budding)
                float bu = yfu - 0.32f, bv = yfv - 0.28f;
                float rBud = bu * bu + bv * bv;
                if (rBud < 0.04f)
                {
                    if (rBud > 0.026f) return new Color(0.25f, 0.22f, 0.20f);
                    return new Color(0.88f, 0.85f, 0.75f);
                }
                return new Color(0.96f, 0.96f, 0.97f);
            }

            // 5. Nơron thần kinh đa cực
            if (kind == SpecimenKind.Neuron)
            {
                float gx = Mathf.Repeat(u * 3.5f, 1f) - 0.5f;
                float gy = Mathf.Repeat(v * 3.5f, 1f) - 0.5f;
                float r = Mathf.Sqrt(gx * gx + gy * gy);
                float ang = Mathf.Atan2(gy, gx);
                float star = 0.14f + 0.06f * Mathf.Cos(ang * 5f);
                if (r < 0.045f) return new Color(0.18f, 0.12f, 0.38f); // nhân nơron
                if (r < star || (Mathf.Abs(gy) < 0.018f && gx > 0f)) return new Color(0.45f, 0.28f, 0.68f); // thân sao + sợi trục
                return new Color(0.93f, 0.91f, 0.96f);
            }

            // 6. Tiêu bản máu người (hồng cầu lõm hai mặt & bạch cầu đa nhân trung tính / lympho)
            if (kind == SpecimenKind.Blood)
            {
                int idx = Mathf.Abs(Mathf.FloorToInt(u * 10f) * 7 + Mathf.FloorToInt(v * 10f) * 13);
                float gx = Mathf.Repeat(u * 10f, 1f) - 0.5f;
                float gy = Mathf.Repeat(v * 10f, 1f) - 0.5f;
                float r = Mathf.Sqrt(gx * gx + gy * gy);
                if (idx % 11 == 0)
                {
                    // Bạch cầu trung tính (Neutrophil): nhân 3-4 múi nối nhau + hạt tế bào chất tím nhạt
                    float nu1 = Mathf.Sqrt((gx - 0.05f) * (gx - 0.05f) + (gy - 0.04f) * (gy - 0.04f));
                    float nu2 = Mathf.Sqrt((gx + 0.06f) * (gx + 0.06f) + (gy - 0.03f) * (gy - 0.03f));
                    float nu3 = Mathf.Sqrt(gx * gx + (gy + 0.07f) * (gy + 0.07f));
                    if (nu1 < 0.055f || nu2 < 0.052f || nu3 < 0.048f)
                        return new Color(0.35f, 0.12f, 0.52f);
                    if (r < 0.28f)
                        return new Color(0.82f, 0.78f, 0.90f);
                }
                else if (idx % 17 == 0)
                {
                    // Tế bào Lympho: nhân to tròn đậm màu chiếm gần hết tế bào
                    if (r < 0.18f) return new Color(0.24f, 0.10f, 0.48f);
                    if (r < 0.24f) return new Color(0.70f, 0.82f, 0.95f);
                }
                else if (r < 0.22f)
                {
                    // Hồng cầu hình đĩa lõm hai mặt: viền dày đỏ cam, tâm mỏng sáng màu
                    return r < 0.09f ? new Color(0.94f, 0.58f, 0.60f) : new Color(0.84f, 0.22f, 0.26f);
                }
                return new Color(0.96f, 0.95f, 0.95f);
            }

            // 7. Nguyên phân chóp rễ hành (Allium cepa)
            if (kind == SpecimenKind.Mitosis)
            {
                float cU = u * 7f, cV = v * 7f;
                float fU = Mathf.Repeat(cU, 1f), fV = Mathf.Repeat(cV, 1f);
                if (fU < 0.06f || fU > 0.94f || fV < 0.06f || fV > 0.94f)
                    return new Color(0.48f, 0.28f, 0.36f);
                int phase = Mathf.Abs(Mathf.FloorToInt(cU) * 3 + Mathf.FloorToInt(cV) * 5) % 4;
                float du = fU - 0.5f, dv = fV - 0.5f;
                if (phase == 1 && Mathf.Abs(dv) < 0.07f && Mathf.Abs(du) < 0.28f)
                    return new Color(0.28f, 0.06f, 0.18f); // Kỳ giữa: NST xếp 1 hàng mặt phẳng xích đạo
                if (phase == 2 && (Mathf.Abs(dv - 0.16f) < 0.06f || Mathf.Abs(dv + 0.16f) < 0.06f) && Mathf.Abs(du) < 0.24f)
                    return new Color(0.28f, 0.06f, 0.18f); // Kỳ sau: NST phân li về 2 cực
                if (du * du + dv * dv < 0.05f)
                    return new Color(0.42f, 0.12f, 0.26f); // Kỳ đầu / nhân
                return new Color(0.93f, 0.84f, 0.88f);
            }

            // 8. Tiêu bản Khí khổng thài lài tía (Tradescantia zebrina stomata)
            if (kind == SpecimenKind.Stomata)
            {
                float sCellU = u * 3.8f;
                int sRow = Mathf.FloorToInt(sCellU);
                float sCellV = v * 7.6f + (sRow & 1) * 0.5f;
                int sCol = Mathf.FloorToInt(sCellV);
                float sfu = sCellU - sRow;
                float sfv = sCellV - sCol;

                bool isStomaSite = ((sRow + sCol * 2) % 4 == 0);
                if (isStomaSite)
                {
                    float gu = sfu - 0.5f;
                    float gv = (sfv - 0.5f) * 1.8f;
                    float rGuard = gu * gu * 2.8f + gv * gv;

                    if (rGuard < 0.26f)
                    {
                        float poreWidth = stomaOpen ? 0.035f : 0.008f;
                        if (Mathf.Abs(gu) < poreWidth && Mathf.Abs(gv) < 0.28f)
                        {
                            return new Color(0.04f, 0.06f, 0.08f); // Lỗ khí (khổng)
                        }

                        // Lục lạp (Chloroplasts) xanh lục bảo vệ trong tế bào hình hạt đậu
                        float chl = Mathf.Sin(gu * 30f) * Mathf.Cos(gv * 25f);
                        if (chl > 0.45f) return new Color(0.12f, 0.52f, 0.18f);
                        return new Color(0.22f, 0.68f, 0.28f);
                    }
                }

                // Tế bào kèm biểu bì (vách tế bào uốn lượn hình sin đặc trưng thài lài hoặc nhạt)
                float dU = Mathf.Min(sfu, 1f - sfu);
                float dV = Mathf.Min(sfv, 1f - sfv);
                if (dU < 0.04f || dV < 0.03f) return new Color(0.32f, 0.22f, 0.30f); // Thành tế bào
                return new Color(0.86f, 0.72f, 0.84f);                               // Anthocyanin thài lài tía
            }

            // 9. Tiêu bản Biểu bì vảy hành tím (Allium cepa) — Thí nghiệm Co & Phản co nguyên sinh
            float cellU = u * 4.2f;
            int row = Mathf.FloorToInt(cellU);
            float cellV = v * 12.5f + (row & 1) * 0.5f;
            int col = Mathf.FloorToInt(cellV);
            float fu = cellU - row;
            float fv = cellV - col;

            float distU = Mathf.Min(fu, 1f - fu);
            float distV = Mathf.Min(fv, 1f - fv);

            // Thành tế bào cellulose đôi (Cell wall + Middle lamella)
            bool isMiddleLamella = distU < 0.012f || distV < 0.008f;
            bool isCellWall = distU < 0.042f || distV < 0.024f;

            if (isCellWall)
            {
                if (stainedBlue) return isMiddleLamella ? new Color(0.12f, 0.22f, 0.45f) : new Color(0.20f, 0.36f, 0.62f);
                if (stainedLugol) return isMiddleLamella ? new Color(0.28f, 0.18f, 0.08f) : new Color(0.42f, 0.28f, 0.14f);
                return isMiddleLamella ? new Color(0.22f, 0.15f, 0.20f) : new Color(0.38f, 0.26f, 0.35f);
            }

            // Co nguyên sinh: màng sinh chất và không bào co lại tách khỏi thành tế bào cellulose cứng
            float shrinkU = plasmolysis * 0.18f;
            float shrinkV = plasmolysis * 0.12f;
            bool inProtoplast = (distU > (0.042f + shrinkU)) && (distV > (0.024f + shrinkV));

            // Vùng gian bào giữa màng sinh chất co và thành tế bào: dung dịch ưu trương NaCl 5% lọt vào
            if (!inProtoplast)
            {
                return new Color(0.94f, 0.93f, 0.88f);
            }

            // Nhân tế bào hình cầu nằm ép sát màng sinh chất
            float nu = fu - (0.28f + shrinkU * 0.5f);
            float nv = fv - 0.44f;
            float nDist2 = nu * nu * 3.5f + nv * nv * 8.0f;
            if (nDist2 < 0.038f)
            {
                // Nhân con (nucleolus) sẫm màu ở trung tâm nhân
                if (nDist2 < 0.006f)
                {
                    if (stainedBlue) return new Color(0.02f, 0.08f, 0.40f);
                    if (stainedLugol) return new Color(0.22f, 0.08f, 0.02f);
                    return new Color(0.35f, 0.10f, 0.26f);
                }
                // Nhân bắt màu đậm với phẩm nhuộm
                if (stainedBlue) return new Color(0.06f, 0.18f, 0.68f);
                if (stainedLugol) return new Color(0.40f, 0.20f, 0.06f);
                return new Color(0.58f, 0.22f, 0.48f);
            }

            // Không bào trung tâm chứa dịch anthocyanin tím đậm (hoặc màu thuốc nhuộm)
            if (stainedBlue) return new Color(0.72f, 0.85f, 0.96f);
            if (stainedLugol) return new Color(0.92f, 0.85f, 0.62f);

            float pVar = Mathf.Sin(row * 1.6f + col * 2.2f) * 0.06f;
            return new Color(0.86f + pVar, 0.36f + pVar * 0.5f, 0.74f + pVar, 1f);
        }

        void Update()
        {
            // Cập nhật hoạt ảnh co và phản co nguyên sinh theo thời gian thực
            if (plasmolysisActive)
            {
                if (plasmolysisFactor < 1.0f)
                {
                    plasmolysisFactor = Mathf.MoveTowards(plasmolysisFactor, 1.0f, Time.deltaTime * 0.4f);
                    texDirty = true;
                }
            }
            else if (plasmolysisFactor > 0f)
            {
                plasmolysisFactor = Mathf.MoveTowards(plasmolysisFactor, 0f, Time.deltaTime * 0.5f);
                texDirty = true;
            }

            // Kính giáo viên: thị trường đổi (đã vẽ lại) thì đưa lên tường video. Ảnh trên tường dùng chung texture
            // với thị kính nên tự cập nhật; chỉ cần báo màn hình một lần cho mỗi lần đổi. Bản cũ cứ 2 giây lại thêm
            // một trang mới vào tường video (danh sách trang phình mãi, người xem bấm sang trang khác là bị kéo về)
            // và ghi đè thẳng lên mặt màn hình nên bài giáo viên trình chiếu không hiện được.
            if (isTeacherCamera && cameraStreaming && texDirty) RenderViewportIfNeeded(false);
            if (isTeacherCamera && cameraStreaming && streamedVersion != viewportVersion && Time.unscaledTime - lastStreamTime > 0.5f)
            {
                var screen = LabScreen.Find("t2_tuong_video_2");
                if (screen != null && viewportTex != null)
                {
                    lastStreamTime = Time.unscaledTime;
                    streamedVersion = viewportVersion;
                    screen.ShowImage(viewportTex, StreamCaption);
                }
            }
        }

        string StreamCaption => $"CAMERA KÍNH HIỂN VI · {(activeSlideCode ?? "").ToUpper()} ({BioFeedback.FormatVi(CurrentMagnification, "0")}×)";

        void OnDestroy()
        {
            if (stageSocket != null) stageSocket.Changed -= OnStageSocketChanged;
            if (viewportTex != null)
            {
                foreach (var screen in LabScreen.All) screen.RemoveImage(viewportTex);
                Destroy(viewportTex);
            }
        }
    }
}
