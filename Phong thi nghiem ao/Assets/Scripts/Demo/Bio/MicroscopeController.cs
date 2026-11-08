using System.Collections;
using System.IO;
using Khkt.Demo.Instruments;
using Khkt.Environment;
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
        public Transform stageTransform;
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
        int selectedEyepieceKnob = 1; // 1: Thô, 2: Tinh, 3: X/Y, 4: Độ sáng, 5: Màn chắn
        Texture2D viewportTex;
        Color32[] pixelBuf;
        const int TexSize = 1024;
        bool texDirty = true;
        float lastRedrawTime;
        float lastStreamTime;
        GUIStyle hudStyle, labelStyle;

        public string TargetName => isTeacherCamera ? "Kính hiển vi kỹ thuật số giáo viên" : "Kính hiển vi quang học hai mắt";

        public string UseHint
        {
            get
            {
                var (mag, na, color, isOil) = Optics.GetObjectiveProps(Objectives[currentObjectiveIndex]);
                double total = Optics.TotalMagnification(eyepieceMag, mag);
                double fov = Optics.FieldOfViewMm(fieldNumberMm, mag);
                string slideInfo = slideSample != null && !slideSample.IsEmpty ? slideSample.sampleName : activeSlideCode;
                return $"Kính hiển vi: vật kính {BioFeedback.FormatVi(mag, "0")}× (vòng {color}, NA {BioFeedback.FormatVi(na, "0.00")}{(isOil ? ", dầu soi" : "")}), phóng đại {BioFeedback.FormatVi(total, "0")}×, thị trường {BioFeedback.FormatVi(fov, "0.##")} mm · Mẫu: {slideInfo}. Bấm đổi vật kính, giữ Shift+Bấm ghé mắt thị kính.";
            }
        }

        public double CurrentMagnification => Optics.TotalMagnification(eyepieceMag, Optics.GetObjectiveProps(Objectives[currentObjectiveIndex]).mag);
        public double CurrentFovMm => Optics.FieldOfViewMm(fieldNumberMm, Optics.GetObjectiveProps(Objectives[currentObjectiveIndex]).mag);
        public float FocusOffsetMm { get; private set; }

        void Awake()
        {
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

        public void Use()
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed))
            {
                ToggleEyepieceView();
                return;
            }
            if (isTeacherCamera && kb != null && kb.cKey.isPressed)
            {
                OpenTeacherCameraProgram();
                return;
            }
            CycleObjective();
        }

        public void OpenTeacherCameraProgram()
        {
            var prog = new MicroscopeCameraProgram(this);
            InstrumentScreen.Open(null, prog, null);
        }

        public void ToggleEyepieceView()
        {
            if (inEyepieceView)
            {
                ExitEyepieceView();
            }
            else
            {
                EnterEyepieceView();
            }
        }

        public void EnterEyepieceView()
        {
            if (eyepieceTarget == null)
            {
                var go = new GameObject("ViTriMatThiKinh");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0.18f, 0.42f, 0f);
                go.transform.localRotation = Quaternion.Euler(25f, -90f, 0f);
                eyepieceTarget = go.transform;
            }
            inEyepieceView = true;
            texDirty = true;
            RenderViewportIfNeeded(true);
            CloseUpView.Enter(eyepieceTarget, TargetName, 38f, () => { inEyepieceView = false; });
            BioFeedback.LogAction(isTeacherCamera ? "kinh_hien_vi_camera" : "kinh_hien_vi", "ghe_mat_thi_kinh",
                $"Ghé mắt quan sát thị kính ở độ phóng đại {BioFeedback.FormatVi(CurrentMagnification, "0")}×.");
        }

        public void ExitEyepieceView()
        {
            if (!inEyepieceView) return;
            inEyepieceView = false;
            if (CloseUpView.IsActive)
                CloseUpView.Exit();
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

        public void CycleObjective()
        {
            if (turning) return;
            int prev = currentObjectiveIndex;
            currentObjectiveIndex = (currentObjectiveIndex + 1) % Objectives.Length;

            if (prev == 3 && currentObjectiveIndex == 2 && oilApplied)
            {
                objective40xOilDirty = true;
                BioFeedback.RecordMistake("kinh_hien_vi", "vat_kinh_40x_dinh_dau",
                    "Quay từ vật kính 100× có dầu về vật kính 40× khi chưa lau sạch dầu soi!",
                    "Vật kính 40× là vật kính khô, nếu quệt vào giọt dầu soi trên lamen sẽ làm lem thấu kính khiến ảnh mờ đục. Cần dùng giấy lau kính lau sạch.");
            }
            if (currentObjectiveIndex == 3 && !oilApplied)
            {
                BioFeedback.RecordMistake("kinh_hien_vi", "vat_kinh_100x_thieu_dau",
                    "Chuyển sang vật kính 100× (vòng trắng) nhưng CHƯA nhỏ dầu soi immersion!",
                    "Khi không có dầu soi (n = 1,515) nối giữa lamen và vật kính 100×, tia sáng bị phản xạ toàn phần ở khe không khí (n = 1,0) làm ảnh tối và mờ nhòe.");
            }
            if (currentObjectiveIndex >= 2 && slideSample != null && !slideSample.hasCoverslip)
            {
                BioFeedback.RecordMistake("kinh_hien_vi", "soi_40x_khong_day_lamen",
                    "Chuyển sang vật kính 40× khi tiêu bản chưa đậy lamen!",
                    "Khoảng cách làm việc của vật kính 40× rất ngắn, không đậy lamen sẽ làm đầu vật kính chạm trực tiếp vào giọt nước/thuốc nhuộm.");
            }

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
                BioFeedback.ShowToast("Đã thực hiện quy trình nhuộm Gram: Vi khuẩn Gram(+) màu tím, Gram(-) màu hồng đỏ!", false);
                BioFeedback.LogAction("kinh_hien_vi", "nhuom_gram", "Nhuộm phân biệt vi khuẩn Gram.");
            }
            texDirty = true;
            RenderViewportIfNeeded(true);
        }

        public void StreamToVideoWall()
        {
            RenderViewportIfNeeded(true);
            var screen = LabScreen.Find("t2_tuong_video_2");
            if (screen != null && viewportTex != null)
            {
                screen.ShowImage(viewportTex, $"CAMERA KÍNH HIỂN VI · {activeSlideCode.ToUpper()} ({BioFeedback.FormatVi(CurrentMagnification, "0")}×)");
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
            if (!force && Time.time - lastRedrawTime < 0.1f) return;
            lastRedrawTime = Time.time;
            texDirty = false;

            if (viewportTex == null)
            {
                viewportTex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.DontSave,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                pixelBuf = new Color32[TexSize * TexSize];
            }

            var (mag, na, _, isOil) = Optics.GetObjectiveProps(Objectives[currentObjectiveIndex]);
            float blur = Mathf.Clamp((float)ComputeCurrentBlurFactor(), 0f, 4f);
            float lightGain = lampOn ? brightness * Mathf.Lerp(0.4f, 1.25f, irisOpen) * Mathf.Lerp(0.5f, 1.15f, condenserHeight) : 0.04f;
            
            // Vật kính 100x dầu soi: khi thiếu dầu (n = 1.0 không khí so với n = 1.515 thủy tinh) tia sáng bị tán xạ mạnh
            if (isOil && !oilApplied)
            {
                lightGain *= 0.25f;
            }

            bool stainedBlue = (slideSample != null && slideSample.stainId.Contains("methylen")) || activeSlideCode.Contains("xanh") || appliedStain == "methylene_blue";
            bool stainedLugol = (slideSample != null && slideSample.stainId.Contains("lugol")) || appliedStain == "lugol";
            bool isGram = activeSlideCode.Contains("gram") || appliedStain == "gram";
            bool stainedGramPos = activeSlideCode.Contains("sua_chua") || activeSlideCode.Contains("subtilis") || (isGram && !activeSlideCode.Contains("ecoli"));
            bool stainedGramNeg = activeSlideCode.Contains("ecoli") || activeSlideCode.Contains("e_coli");
            bool isStomata = activeSlideCode.Contains("thai_lai") || activeSlideCode.Contains("khi_khong");
            bool isSarcomere = activeSlideCode.Contains("co_van") || activeSlideCode.Contains("sarcomere");
            bool isMouth = activeSlideCode.Contains("niem_mac") || activeSlideCode.Contains("mieng");
            bool isYeast = activeSlideCode.Contains("nam_men") || activeSlideCode.Contains("yeast");
            bool hasBubbles = slideSample != null && slideSample.hasAirBubbles;
            bool isCracked = slideSample != null && slideSample.isCoverslipCracked;

            float scale = (float)mag / 10f;
            float cx = TexSize * 0.5f, cy = TexSize * 0.5f, rMax = TexSize * 0.47f;
            float rMax2 = rMax * rMax;
            float invRMax = 1.0f / rMax;
            float invScale = 1.0f / Mathf.Max(0.35f, scale);
            float uOffset = stageX * 0.035f;
            float vOffset = stageY * 0.035f;

            Color32 vignetteColor = new Color32(8, 10, 14, 255);
            float chromaticFringe = Mathf.Clamp(FocusOffsetMm * 60f, -0.15f, 0.15f);
            float irisContrast = Mathf.Lerp(1.35f, 0.85f, irisOpen);
            float reticleHalfW = rMax * 0.72f;
            float reticleTotalW = reticleHalfW * 2f;

            for (int y = 0; y < TexSize; y++)
            {
                float dy = y - cy;
                float dy2 = dy * dy;
                int rowOffset = y * TexSize;

                if (dy2 >= rMax2)
                {
                    for (int x = 0; x < TexSize; x++)
                        pixelBuf[rowOffset + x] = vignetteColor;
                    continue;
                }

                float halfW = Mathf.Sqrt(rMax2 - dy2);
                int x0 = Mathf.Max(0, Mathf.CeilToInt(cx - halfW));
                int x1 = Mathf.Min(TexSize - 1, Mathf.FloorToInt(cx + halfW));

                for (int x = 0; x < x0; x++)
                    pixelBuf[rowOffset + x] = vignetteColor;

                float v = (dy * invRMax) * invScale + vOffset;

                for (int x = x0; x <= x1; x++)
                {
                    float dx = x - cx;
                    float u = (dx * invRMax) * invScale + uOffset;

                    Color c = SampleSpecimenColor(u, v, stainedBlue, stainedLugol, isStomata, stainedGramPos, stainedGramNeg, isSarcomere, isMouth, isYeast, plasmolysisFactor, stomaOpen);

                    // Tán xạ & mờ khi dùng 100x mà thiếu dầu soi
                    if (isOil && !oilApplied)
                    {
                        c = Color.Lerp(c, new Color(0.28f, 0.28f, 0.32f), 0.52f);
                    }

                    // Độ mờ tiêu cự (Defocus point-spread blur) & viền sắc sai quang học
                    if (blur > 0.08f)
                    {
                        Color avg = stainedBlue ? new Color(0.72f, 0.82f, 0.94f) : (stainedLugol ? new Color(0.92f, 0.88f, 0.76f) : new Color(0.88f, 0.84f, 0.90f));
                        c = Color.Lerp(c, avg, Mathf.Clamp01(blur * 0.35f));
                        c.r = Mathf.Clamp01(c.r + chromaticFringe * 0.12f);
                        c.b = Mathf.Clamp01(c.b - chromaticFringe * 0.12f);
                    }

                    // Tụ quang & Màn chắn sáng (Iris Diaphragm): điều chỉnh độ mở thay đổi độ tương phản biên
                    if (irisContrast != 1.0f)
                    {
                        c.r = Mathf.Clamp01(Mathf.Pow(c.r, irisContrast));
                        c.g = Mathf.Clamp01(Mathf.Pow(c.g, irisContrast));
                        c.b = Mathf.Clamp01(Mathf.Pow(c.b, irisContrast));
                    }

                    // Bọt khí dưới lamen
                    if (hasBubbles)
                    {
                        float bx = dx - 24f, by = dy - 18f;
                        float br = Mathf.Sqrt(bx * bx + by * by);
                        if (br > 22f && br < 28f) c = new Color(0.04f, 0.04f, 0.06f);
                        else if (br <= 22f) c = new Color(0.96f, 0.98f, 1.0f);
                    }

                    // Vết nứt lamen do vặn ốc sơ cấp ở 40×/100×
                    if (isCracked && Mathf.Abs(dx + dy * 0.65f) < 2.0f)
                    {
                        c = new Color(0.03f, 0.03f, 0.03f);
                    }

                    // Thước trắc vi thị kính (Ocular micrometer reticle: 0..100 vạch ngang)
                    if (showMicrometerReticle)
                    {
                        float rx = dx + reticleHalfW;
                        if (rx >= 0f && rx <= reticleTotalW)
                        {
                            float div100 = (rx / reticleTotalW) * 100f;
                            float nearestDiv = Mathf.Round(div100);
                            float divDist = Mathf.Abs(div100 - nearestDiv);

                            // Trục ngang tâm
                            if (Mathf.Abs(dy) < 1.4f)
                            {
                                c = Color.Lerp(c, Color.black, 0.88f);
                            }
                            else
                            {
                                // Vạch đứng chia độ: 10 vạch dài 22px, 5 vạch dài 14px, 1 vạch dài 8px
                                int divInt = Mathf.RoundToInt(nearestDiv);
                                float tickLen = (divInt % 10 == 0) ? 22f : ((divInt % 5 == 0) ? 14f : 8f);
                                if (divDist < 0.11f && dy > 0f && dy < tickLen)
                                {
                                    c = Color.Lerp(c, Color.black, 0.88f);
                                }
                            }
                        }
                    }

                    c *= lightGain;
                    c.a = 1f;
                    pixelBuf[rowOffset + x] = c;
                }

                for (int x = x1 + 1; x < TexSize; x++)
                    pixelBuf[rowOffset + x] = vignetteColor;
            }

            viewportTex.SetPixels32(pixelBuf);
            viewportTex.Apply(false);

            if (isTeacherCamera && cameraStreaming && teacherScreenRenderer != null)
            {
                var mpb = new MaterialPropertyBlock();
                teacherScreenRenderer.GetPropertyBlock(mpb);
                mpb.SetTexture("_BaseMap", viewportTex);
                teacherScreenRenderer.SetPropertyBlock(mpb);
            }
        }

        static Color SampleSpecimenColor(float u, float v, bool stainedBlue, bool stainedLugol, bool isStomata, bool gramPos, bool gramNeg, bool isSarcomere, bool isMouth, bool isYeast, float plasmolysis, bool stomaOpen)
        {
            // 1. Tiêu bản Vi khuẩn Gram hoặc Sữa chua (Lactobacillus, E. coli, Bacillus subtilis)
            if (gramPos || gramNeg)
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
                        return gramPos ? new Color(0.28f, 0.10f, 0.62f) : new Color(0.88f, 0.22f, 0.42f);
                    }
                }
                return new Color(0.96f, 0.96f, 0.97f);
            }

            // 2. Tiêu bản Tế bào cơ vân (Sarcomere: vân sáng I, vân tối A, vạch Z)
            if (isSarcomere)
            {
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
            if (isMouth)
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
            if (isYeast)
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

            // 5. Tiêu bản Khí khổng thài lài tía (Tradescantia zebrina stomata)
            if (isStomata)
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
                    float su = sfu - 0.5f;
                    float sv = sfv - 0.5f;
                    float rOuter = su * su * 3.2f + sv * sv * 5.2f;
                    float rInner = su * su * 15.0f + sv * sv * 26.0f;

                    // Khe khí khổng mở hoặc đóng
                    float poreW = stomaOpen ? 0.014f : 0.003f;
                    bool isPore = Mathf.Abs(su) < poreW && Mathf.Abs(sv) < 0.14f;
                    if (isPore) return new Color(0.08f, 0.12f, 0.08f);

                    // Thành trong dày (mặt lõm của 2 tế bào hình hạt đậu)
                    bool isThickWall = rInner < 0.040f && !isPore;
                    if (isThickWall) return new Color(0.12f, 0.32f, 0.14f);

                    // Tế bào hạt đậu chứa hạt lục lạp xanh lục hình đĩa
                    if (rOuter < 0.22f)
                    {
                        float chkX = Mathf.Repeat(su * 24f, 1f) - 0.5f;
                        float chkY = Mathf.Repeat(sv * 24f, 1f) - 0.5f;
                        bool isChloroplast = (chkX * chkX + chkY * chkY) < 0.14f;
                        if (isChloroplast) return new Color(0.08f, 0.50f, 0.14f); // Lục lạp xanh lục
                        return new Color(0.44f, 0.80f, 0.48f);                   // Tế bào chất hạt đậu
                    }
                }

                // Tế bào biểu bì phụ trợ bao quanh (màu tím thài lài hoặc nhạt)
                float dU = Mathf.Min(sfu, 1f - sfu);
                float dV = Mathf.Min(sfv, 1f - sfv);
                if (dU < 0.04f || dV < 0.03f) return new Color(0.32f, 0.22f, 0.30f); // Thành tế bào
                return new Color(0.86f, 0.72f, 0.84f);                               // Anthocyanin thài lài tía
            }

            // 6. Tiêu bản Biểu bì vảy hành tím (Allium cepa) — Thí nghiệm Co & Phản co nguyên sinh
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

            // Tự động đồng bộ luồng video sang tường video khi đang streaming
            if (isTeacherCamera && cameraStreaming && Time.time - lastStreamTime > 2.0f)
            {
                lastStreamTime = Time.time;
                var screen = LabScreen.Find("t2_tuong_video_2");
                if (screen != null && viewportTex != null)
                {
                    screen.ShowImage(viewportTex, $"CAMERA KÍNH HIỂN VI · {activeSlideCode.ToUpper()} ({BioFeedback.FormatVi(CurrentMagnification, "0")}×)");
                }
            }

            if (!inEyepieceView) return;

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) selectedEyepieceKnob = 1;
                else if (kb.digit2Key.wasPressedThisFrame) selectedEyepieceKnob = 2;
                else if (kb.digit3Key.wasPressedThisFrame) selectedEyepieceKnob = 3;
                else if (kb.digit4Key.wasPressedThisFrame) selectedEyepieceKnob = 4;
                else if (kb.digit5Key.wasPressedThisFrame) selectedEyepieceKnob = 5;
                else if (kb.mKey.wasPressedThisFrame) { showMicrometerReticle = !showMicrometerReticle; texDirty = true; }
                else if (kb.nKey.wasPressedThisFrame) { showAnnotations = !showAnnotations; }
                else if (kb.pKey.wasPressedThisFrame) { ApplyReagent(plasmolysisActive ? "nuoc_cat" : "nacl"); }
                else if (kb.lKey.wasPressedThisFrame) { ApplyReagent("lugol"); }
                else if (kb.bKey.wasPressedThisFrame) { ApplyReagent("methylene_blue"); }
                else if (kb.oKey.wasPressedThisFrame) { if (oilApplied) CleanOilWithLensTissue(); else ApplyImmersionOil(); }
                else if (kb.cKey.wasPressedThisFrame) { CaptureMicrographPng(); }
                else if (kb.escapeKey.wasPressedThisFrame) { ToggleEyepieceView(); }
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.05f)
                {
                    int dir = scroll > 0f ? 1 : -1;
                    switch (selectedEyepieceKnob)
                    {
                        case 1: AdjustCoarseFocus(dir * 0.1f); break;
                        case 2: AdjustFineFocus(dir * 2f); break;
                        case 3: AdjustStageXY(dir * 0.5f, 0f); break;
                        case 4: brightness = Mathf.Clamp01(brightness + dir * 0.05f); texDirty = true; break;
                        case 5: irisOpen = Mathf.Clamp01(irisOpen + dir * 0.05f); texDirty = true; break;
                    }
                    RenderViewportIfNeeded(false);
                }
            }
        }

        void OnGUI()
        {
            if (!inEyepieceView) return;
            RenderViewportIfNeeded(false);
            if (viewportTex == null) return;

            float size = Mathf.Min(Screen.width, Screen.height) * 0.68f;
            Rect viewRect = new Rect((Screen.width - size) * 0.5f, (Screen.height - size) * 0.44f, size, size);
            GUI.DrawTexture(viewRect, viewportTex, ScaleMode.StretchToFill);

            hudStyle ??= new GUIStyle(GUI.skin.box)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.88f, 0.96f, 1f) }
            };
            labelStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.95f, 0.55f) }
            };

            // Hiển thị số đo trên thước trắc vi quang học
            if (showMicrometerReticle)
            {
                var reticleNumStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 10,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.12f, 0.12f, 0.12f) }
                };
                float cyPix = viewRect.y + viewRect.height * 0.5f;
                float rMaxPix = viewRect.width * 0.47f;
                float reticleHalfWPix = rMaxPix * 0.72f;
                float leftPix = viewRect.x + viewRect.width * 0.5f - reticleHalfWPix;
                float totalWPix = reticleHalfWPix * 2f;
                for (int dVal = 0; dVal <= 100; dVal += 20)
                {
                    float xPix = leftPix + (dVal / 100f) * totalWPix;
                    GUI.Label(new Rect(xPix - 15f, cyPix - 18f, 30f, 16f), dVal.ToString(), reticleNumStyle);
                }
            }

            if (showAnnotations)
            {
                var slideRec = BioCatalogLoader.GetSlide(activeSlideCode);
                if (slideRec != null)
                {
                    for (int i = 0; i < slideRec.chuThich.Count; i++)
                    {
                        var pt = slideRec.chuThich[i];
                        float px = viewRect.x + pt.x * viewRect.width;
                        float py = viewRect.y + (1f - pt.y) * viewRect.height;
                        GUI.Label(new Rect(px, py, 260f, 24f), "← " + pt.ten, labelStyle);
                    }
                }
            }

            string knobName = selectedEyepieceKnob switch
            {
                1 => "1: Ốc sơ cấp (0,1 mm)",
                2 => "2: Ốc vi cấp (2 µm)",
                3 => "3: Bàn sa trượt X/Y",
                4 => "4: Độ sáng đèn LED",
                _ => "5: Màn chắn sáng (Iris)"
            };
            double cellUm = ComputeMeasuredCellSizeUm();
            double umPerDiv = MicronsPerDivision;
            string keyGuide = "Phím 1..5: Đổi núm | Cuộn chuột: Vặn núm | P: Co/Phản co NaCl 5% | L: Lugol | B: Xanh | O: Dầu soi | C: Chụp HD & Chiếu video | Esc: Thoát";
            string footer = $"Vật kính: {BioFeedback.FormatVi(Optics.GetObjectiveProps(Objectives[currentObjectiveIndex]).mag, "0")}× (NA {BioFeedback.FormatVi(Optics.GetObjectiveProps(Objectives[currentObjectiveIndex]).na, "0.00")}) | Phóng đại: {BioFeedback.FormatVi(CurrentMagnification, "0")}× | Thị trường: {BioFeedback.FormatVi(CurrentFovMm, "0.##")} mm\n" +
                            $"Thước trắc vi: 1 vạch = {BioFeedback.FormatVi(umPerDiv, "0.#")} µm | Du xích sa trượt: X={stageX:+0.00;-0.00;0.00} mm, Y={stageY:+0.00;-0.00;0.00} mm | Tế bào: ~{BioFeedback.FormatVi(cellUm, "0")} µm\n" +
                            $"Đang chọn [{knobName}] · {keyGuide}";
            GUI.Box(new Rect((Screen.width - 860f) * 0.5f, Screen.height - 86f, 860f, 74f), footer, hudStyle);

            // Bảng nút thao tác nhanh (hỗ trợ cả chạm Touch và nhấp chuột Chuột)
            float btnW = 112f, btnH = 38f;
            float startX = (Screen.width - 7 * (btnW + 6f)) * 0.5f;
            float topY = 14f;

            string[] kNames = { "1. Ốc sơ", "2. Ốc vi", "3. Bàn X/Y", "4. Độ sáng", "5. Iris" };
            for (int ki = 0; ki < 5; ki++)
            {
                if (GUI.Button(new Rect(startX + ki * (btnW + 6f), topY, btnW, btnH), kNames[ki]))
                {
                    selectedEyepieceKnob = ki + 1;
                    BioFeedback.PlayClick();
                }
            }
            if (GUI.Button(new Rect(startX + 5 * (btnW + 6f), topY, btnW, btnH), plasmolysisActive ? "Phản co (Nước)" : "Co NS (NaCl)"))
            {
                ApplyReagent(plasmolysisActive ? "nuoc_cat" : "nacl");
            }
            if (GUI.Button(new Rect(startX + 6 * (btnW + 6f), topY, btnW, btnH), "Đổi vật kính"))
            {
                CycleObjective();
            }

            // Hàng nút thí nghiệm và điều chỉnh bên phải
            float rightX = Screen.width - 146f;
            if (GUI.Button(new Rect(rightX, 70f, 132f, 36f), showMicrometerReticle ? "Ẩn thước (M)" : "Hiện thước (M)"))
            {
                showMicrometerReticle = !showMicrometerReticle;
                texDirty = true;
                BioFeedback.PlayClick();
            }
            if (GUI.Button(new Rect(rightX, 114f, 132f, 36f), "Nhuộm Lugol (L)"))
            {
                ApplyReagent("lugol");
            }
            if (GUI.Button(new Rect(rightX, 158f, 132f, 36f), "Nhuộm Xanh (B)"))
            {
                ApplyReagent("methylene_blue");
            }
            if (GUI.Button(new Rect(rightX, 202f, 132f, 36f), oilApplied ? "Lau sạch dầu" : "Dầu soi 100×"))
            {
                if (oilApplied) CleanOilWithLensTissue(); else ApplyImmersionOil();
            }
            if (GUI.Button(new Rect(rightX, 246f, 132f, 36f), "Chụp ảnh HD (C)"))
            {
                CaptureMicrographPng();
            }
            if (GUI.Button(new Rect(rightX, 290f, 132f, 36f), "Chiếu Tường video"))
            {
                StreamToVideoWall();
            }

            // Nút vặn - và + lớn
            if (GUI.Button(new Rect(rightX, Screen.height * 0.5f + 10f, 132f, 44f), "▲ Tăng (+)") || (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame))
            {
                ApplyEyepieceAdjustment(1);
            }
            if (GUI.Button(new Rect(rightX, Screen.height * 0.5f + 62f, 132f, 44f), "▼ Giảm (-)"))
            {
                ApplyEyepieceAdjustment(-1);
            }

            // Nút thoát thị kính
            if (GUI.Button(new Rect(rightX, Screen.height - 64f, 132f, 44f), "✕ Thoát (Esc)"))
            {
                ToggleEyepieceView();
            }
        }

        void ApplyEyepieceAdjustment(int dir)
        {
            switch (selectedEyepieceKnob)
            {
                case 1: AdjustCoarseFocus(dir * 0.1f); break;
                case 2: AdjustFineFocus(dir * 2f); break;
                case 3: AdjustStageXY(dir * 0.5f, 0f); break;
                case 4: brightness = Mathf.Clamp01(brightness + dir * 0.05f); texDirty = true; break;
                case 5: irisOpen = Mathf.Clamp01(irisOpen + dir * 0.05f); texDirty = true; break;
            }
            RenderViewportIfNeeded(false);
            BioFeedback.PlayClick();
        }
    }
}
