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
        const int TexSize = 128;
        bool texDirty = true;
        float lastRedrawTime;
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

        public string CaptureMicrographPng()
        {
            RenderViewportIfNeeded(true);
            if (viewportTex == null) return "";
            try
            {
                string dir = Path.Combine(Application.persistentDataPath, "anh_hien_vi");
                Directory.CreateDirectory(dir);
                string fileName = $"hien_vi_{activeSlideCode}_{ (int)CurrentMagnification }x.png";
                string fullPath = Path.Combine(dir, fileName);
                File.WriteAllBytes(fullPath, viewportTex.EncodeToPNG());
                BioFeedback.PlaySuccess();
                BioFeedback.LogAction("kinh_hien_vi_camera", "chup_anh_hien_vi", $"Đã lưu ảnh chụp hiển vi: {fileName}");
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
            RenderViewportIfNeeded(false);
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
                    wrapMode = TextureWrapMode.Clamp
                };
                pixelBuf = new Color32[TexSize * TexSize];
            }

            var (mag, na, _, isOil) = Optics.GetObjectiveProps(Objectives[currentObjectiveIndex]);
            float blur = Mathf.Clamp((float)ComputeCurrentBlurFactor(), 0f, 4f);
            float lightGain = lampOn ? brightness * Mathf.Lerp(0.4f, 1.25f, irisOpen) * Mathf.Lerp(0.5f, 1.15f, condenserHeight) : 0.04f;
            if (isOil && !oilApplied) lightGain *= 0.38f;

            bool stainedBlue = slideSample != null && (slideSample.stainId.Contains("methylen") || activeSlideCode.Contains("xanh"));
            bool stainedGramPos = activeSlideCode.Contains("sua_chua") || activeSlideCode.Contains("subtilis");
            bool stainedGramNeg = activeSlideCode.Contains("ecoli") || activeSlideCode.Contains("e_coli");
            bool isStomata = activeSlideCode.Contains("thai_lai") || activeSlideCode.Contains("khi_khong");
            bool hasBubbles = slideSample != null && slideSample.hasAirBubbles;
            bool isCracked = slideSample != null && slideSample.isCoverslipCracked;

            float scale = (float)mag / 10f;
            float cx = TexSize * 0.5f, cy = TexSize * 0.5f, rMax = TexSize * 0.47f;

            for (int y = 0; y < TexSize; y++)
            {
                for (int x = 0; x < TexSize; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist > rMax)
                    {
                        pixelBuf[y * TexSize + x] = new Color32(10, 12, 16, 255);
                        continue;
                    }

                    float u = (dx / rMax) / Mathf.Max(0.4f, scale) + stageX * 0.04f;
                    float v = (dy / rMax) / Mathf.Max(0.4f, scale) + stageY * 0.04f;

                    Color c = SampleSpecimenColor(u, v, stainedBlue, isStomata, stainedGramPos, stainedGramNeg);
                    // Độ mờ lấy nét theo DOF
                    if (blur > 0.15f)
                    {
                        Color avg = stainedBlue ? new Color(0.72f, 0.82f, 0.94f) : new Color(0.88f, 0.84f, 0.90f);
                        c = Color.Lerp(c, avg, Mathf.Clamp01(blur * 0.32f));
                    }

                    // Bọt khí dưới lamen khi hạ lamen 90°
                    if (hasBubbles)
                    {
                        float bx = dx - 14f, by = dy - 10f;
                        float br = Mathf.Sqrt(bx * bx + by * by);
                        if (br > 11f && br < 15.5f) c = new Color(0.06f, 0.06f, 0.08f);
                        else if (br <= 11f) c = new Color(0.96f, 0.98f, 1.0f);
                    }

                    // Vết nứt lamen khi vặn ốc sơ cấp ở 40×/100×
                    if (isCracked && Mathf.Abs(dx + dy * 0.65f) < 1.3f)
                    {
                        c = new Color(0.05f, 0.05f, 0.05f);
                    }

                    // Thước trắc vi thị kính (vạch micromet ngang tâm)
                    if (showMicrometerReticle && Mathf.Abs(dy) < 1.1f && Mathf.Abs(dx) < rMax * 0.75f)
                    {
                        c = Color.Lerp(c, Color.black, 0.65f);
                    }

                    c *= lightGain;
                    c.a = 1f;
                    pixelBuf[y * TexSize + x] = c;
                }
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

        static Color SampleSpecimenColor(float u, float v, bool stainedBlue, bool isStomata, bool gramPos, bool gramNeg)
        {
            if (gramPos || gramNeg)
            {
                float gx = Mathf.Repeat(u * 14f, 1f) - 0.5f;
                float gy = Mathf.Repeat(v * 14f, 1f) - 0.5f;
                bool rod = Mathf.Abs(gx) < 0.22f && Mathf.Abs(gy) < 0.08f;
                if (rod) return gramPos ? new Color(0.36f, 0.14f, 0.68f) : new Color(0.88f, 0.28f, 0.45f);
                return new Color(0.95f, 0.95f, 0.96f);
            }

            // Lưới tế bào biểu bì lục giác kéo dài
            float cellU = u * 4.5f;
            float cellV = v * 9.0f + (Mathf.FloorToInt(cellU) % 2) * 0.5f;
            float fu = Mathf.Repeat(cellU, 1f);
            float fv = Mathf.Repeat(cellV, 1f);

            // Vách tế bào (Cell wall)
            if (fu < 0.07f || fu > 0.93f || fv < 0.08f || fv > 0.92f)
                return stainedBlue ? new Color(0.20f, 0.36f, 0.62f) : new Color(0.42f, 0.26f, 0.38f);

            // Khí khổng (2 tế bào hạt đậu màu xanh lục chứa lục lạp)
            if (isStomata && ((int)(Mathf.Floor(cellU) + Mathf.Floor(cellV)) % 3 == 0))
            {
                float su = fu - 0.5f, sv = fv - 0.5f;
                float sr = su * su * 2.2f + sv * sv * 3.5f;
                if (sr < 0.04f) return new Color(0.18f, 0.25f, 0.18f); // Khe khí khổng mở
                if (sr < 0.22f) return new Color(0.24f, 0.66f, 0.28f); // Tế bào hạt đậu
            }

            // Nhân tế bào (Nucleus)
            float nu = fu - 0.36f, nv = fv - 0.48f;
            if (nu * nu + nv * nv < 0.022f)
                return stainedBlue ? new Color(0.08f, 0.22f, 0.68f) : new Color(0.62f, 0.36f, 0.52f);

            // Không bào chứa anthocyanin hoặc tế bào chất
            return stainedBlue ? new Color(0.78f, 0.88f, 0.97f) : new Color(0.91f, 0.78f, 0.88f);
        }

        void Update()
        {
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
            Rect viewRect = new Rect((Screen.width - size) * 0.5f, (Screen.height - size) * 0.45f, size, size);
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
                3 => "3: Dịch mẫu X/Y",
                4 => "4: Độ sáng đèn",
                _ => "5: Màn chắn sáng (Iris)"
            };
            double cellUm = ComputeMeasuredCellSizeUm();
            string footer = $"Phóng đại: {BioFeedback.FormatVi(CurrentMagnification, "0")}× | Thị trường: {BioFeedback.FormatVi(CurrentFovMm, "0.##")} mm | Kích thước tế bào đo được: ~{BioFeedback.FormatVi(cellUm, "0")} µm\n" +
                            $"Đang chọn [{knobName}] (Phím 1..5 đổi núm, Con lăn vặn, M: Thước trắc vi, N: Chú thích, Esc: Thoát)";
            GUI.Box(new Rect((Screen.width - 760f) * 0.5f, Screen.height - 68f, 760f, 52f), footer, hudStyle);
        }
    }
}
