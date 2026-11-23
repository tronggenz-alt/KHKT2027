using System.Collections.Generic;
using Khkt.Interaction;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using static Khkt.Demo.GlassKit;

namespace Khkt.Demo
{
    /// <summary>
    /// Giao diện chế độ máy tính theo phong cách kính mờ, ít chữ:
    /// thanh các bước (trên), thông báo ngắn, card dụng cụ nổi cạnh vật đang trỏ, thanh gợi ý thao tác
    /// dạng phím (dưới), kính lúp buret, số liệu, kết quả và đồ thị pH.
    /// </summary>
    public class ModernUI : MonoBehaviour
    {
        public static ModernUI Instance { get; private set; }
        public static bool IsSafetyModalOpen => safetyPanel != null && safetyPanel.activeSelf;

        public Camera viewCamera;
        public Shader glassShader;
        public HandController hand;
        public LabProcedure procedure;
        public MeniscusReading reading;
        public LoupeCamera loupe;

        public Canvas Canvas { get; private set; }
        public bool StepperVisible => false;

        const int UiLayer = 5;
        Camera uiCamera;
        RectTransform root;
        Material glassMat, chipMat;

        // Thanh bước
        readonly List<(Image dot, Text num, Text label)> steps = new List<(Image, Text, Text)>();
        // Thanh gợi ý (chỉ xếp lại khi nội dung gợi ý đổi)
        RectTransform hintBar;
        Image hintBarImage;
        readonly List<(RectTransform chip, Text key, Text label)> hintItems = new List<(RectTransform, Text, Text)>();
        readonly string[] hintKeys = new string[5], hintTexts = new string[5];
        int hintShown = -1;
        bool hintActing;
        // Card dụng cụ (nội dung làm mới 5 lần/giây, vị trí bám theo vật mỗi khung hình)
        RectTransform card;
        CanvasGroup cardGroup;
        Text cardKind, cardTitle, cardStatus, cardBody;
        Image cardAccent;
        Transform cardTarget;
        Bounds cardBounds;
        EquipmentInfo cardInfo;
        ChemicalBottle cardChem;
        bool cardIsTool;
        bool cardLastCanInspect;
        float cardNextRefresh;
        string cardTitleStr, cardStatusStr, cardBodyStr;
        // Số liệu chuẩn độ: chữ làm mới 5 – 10 lần/giây thay vì mỗi khung hình
        float nextDataAt, nextLoupeTextAt, nextResultAt;
        static readonly string[] StepNums = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "10" };
        // Kính lúp, số liệu, kết quả, đồ thị, trợ giúp
        GameObject loupePanel, dataPanel, resultPanel, graphPanel, helpPanel, helpBtn, stepBar;
        Image crosshair;
        RawImage loupeImage, graphImage;
        Text resultTitle, eyeText, flowText, dataLeft, dataRight, resultBig, resultSub, resultBadge, resultNotes;
        Image resultBadgeBg, flowBar, swirlBar;
        const int RubricRowCount = 4;
        readonly Image[] rubricCardBgs = new Image[RubricRowCount];
        readonly Image[] rubricCardBorders = new Image[RubricRowCount];
        readonly Image[] rubricStripes = new Image[RubricRowCount];
        readonly Image[] rubricPillBgs = new Image[RubricRowCount];
        readonly Text[] rubricTitles = new Text[RubricRowCount];
        readonly Text[] rubricDetails = new Text[RubricRowCount];
        readonly Text[] rubricScores = new Text[RubricRowCount];
        GameObject resultGraphBox, resultBioBox;
        Image resultGraphBoxBg, resultGraphBoxBorder, resultBioBoxBg, resultBioBoxBorder, resultScorePillBg, resultRule;
        Text resultBioHeadline, resultBioDetails, resultScorePill, resultRubricHeader;
        Button resultNewRunBtn;
        Image resultNewRunBg, resultNewRunBorder;
        Text resultNewRunTxt;
        bool resultShowingBio;
        Texture2D graphTex, panelGraphTex;
        int drawnCurve = -1, panelCurveVersion = -1;
        bool drawnResult, panelCurveResult;
        UiTheme.Mode panelCurveTheme;

        static GameObject safetyPanel;
        static RectTransform safetyRoot;
        static bool safetyCaptured;
        static string safetyTitleStr, safetyReasonStr;
        static System.Action safetyOnProceed, safetyOnCancel;

        enum TextRole { Ink, Dim, Accent, OnAccent }
        readonly List<(Image img, bool isChip, float scale)> themedPanels = new List<(Image, bool, float)>();
        readonly List<(Text txt, TextRole role)> themedTexts = new List<(Text, TextRole)>();
        readonly List<Image> themedBarBgs = new List<Image>();
        readonly List<Image> themedBarFills = new List<Image>();
        readonly List<Image> themedHintChipBgs = new List<Image>();
        readonly List<Image> themedSolidSprites = new List<Image>();
        Image loupeMaskImg;
        Image helpCloseBg, helpCloseBorder;
        Button cardInfoBtn;
        Image cardInfoBtnBg, cardInfoBtnBorder;
        Text cardInfoBtnTxt;

        const int GraphW = 460, GraphH = 170;
        Color32[] graphPx;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            safetyPanel = null;
            safetyRoot = null;
            safetyCaptured = false;
            safetyTitleStr = null;
            safetyReasonStr = null;
            safetyOnProceed = null;
            safetyOnCancel = null;
        }

        void Awake()
        {
            Instance = this;
            if (glassShader == null) glassShader = Shader.Find("Khkt/KinhMo");
            glassMat = Glass(glassShader);
            chipMat = Glass(glassShader, light: true);

            // Camera chính cần ảnh cảnh (Opaque Texture) cho kính mờ và bỏ lớp UI/Preview;
            // camera Overlay riêng vẽ Canvas ở lớp 5 sau toàn bộ thủy tinh 3D (ThuyTinhHD Queue Transparent+10).
            if (viewCamera != null)
            {
                var baseData = viewCamera.GetUniversalAdditionalCameraData();
                if (baseData != null) baseData.requiresColorOption = CameraOverrideOption.On;
                viewCamera.cullingMask &= ~((1 << UiLayer) | (1 << 30));

                var existingCamTr = viewCamera.transform.Find("UI Overlay Camera");
                var uiCamGo = existingCamTr != null ? existingCamTr.gameObject : new GameObject("UI Overlay Camera");
                if (existingCamTr == null) uiCamGo.transform.SetParent(viewCamera.transform, false);
                uiCamGo.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                uiCamGo.transform.localScale = Vector3.one;

                uiCamera = uiCamGo.GetComponent<Camera>();
                if (uiCamera == null) uiCamera = uiCamGo.AddComponent<Camera>();
                uiCamera.enabled = viewCamera.enabled;
                uiCamera.clearFlags = CameraClearFlags.Nothing;
                uiCamera.cullingMask = 1 << UiLayer;
                uiCamera.fieldOfView = viewCamera.fieldOfView;
                uiCamera.rect = viewCamera.rect;
                uiCamera.nearClipPlane = 0.05f;
                uiCamera.farClipPlane = 5f;

                var uiData = uiCamera.GetUniversalAdditionalCameraData();
                if (uiData != null)
                {
                    uiData.renderType = CameraRenderType.Overlay;
                    uiData.renderShadows = false;
                    uiData.renderPostProcessing = false;
                    uiData.volumeLayerMask = 0;
                    uiData.antialiasing = AntialiasingMode.None;
                    uiData.requiresColorOption = CameraOverrideOption.Off;
                }
                if (baseData != null && !baseData.cameraStack.Contains(uiCamera))
                    baseData.cameraStack.Add(uiCamera);
            }

            var cgo = new GameObject("Giao diện", typeof(Canvas), typeof(CanvasScaler));
            cgo.layer = UiLayer;
            cgo.transform.SetParent(transform, false);
            Canvas = cgo.GetComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceCamera;
            Canvas.worldCamera = uiCamera != null ? uiCamera : viewCamera;
            Canvas.planeDistance = uiCamera != null ? 1f : 0.03f;
            if (Application.isPlaying) UiBridge.EnsureInteractive(Canvas);
            var scaler = cgo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            root = (RectTransform)cgo.transform;

            BuildStepper();
            BuildHintBar();
            BuildCard();
            BuildLoupe();
            BuildData();
            BuildResult();
            BuildGraph();
            BuildHelp();
            var ch = Rect(root, "Tâm ngắm", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(8, 8));
            crosshair = ch.gameObject.AddComponent<Image>();
            crosshair.sprite = SolidCircle;
            crosshair.color = new Color(1, 1, 1, 0.85f);
            crosshair.raycastTarget = false;

            SetLayerIfNeeded(cgo.transform, UiLayer);
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            UiTheme.Changed += ApplyTheme;
        }

        void OnDestroy()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            UiTheme.Changed -= ApplyTheme;
            if (IsSafetyModalOpen) CloseSafetyModal(invokeCancel: false);
            if (safetyCaptured) UiBridge.SetCapture(ref safetyCaptured, false);
            if (Instance == this) Instance = null;
            if (viewCamera != null && uiCamera != null)
            {
                var baseData = viewCamera.GetUniversalAdditionalCameraData();
                baseData?.cameraStack.Remove(uiCamera);
            }
            if (uiCamera != null)
            {
                if (Application.isPlaying) Destroy(uiCamera.gameObject);
                else DestroyImmediate(uiCamera.gameObject);
                uiCamera = null;
            }
            DestroyTex(ref graphTex);
            DestroyTex(ref panelGraphTex);
        }

        void OnBeginCameraRendering(ScriptableRenderContext ctx, Camera cam)
        {
            if ((cam == uiCamera || cam == viewCamera) && Canvas != null)
                SetLayerIfNeeded(Canvas.transform, UiLayer);
        }

        static void SetLayerIfNeeded(Transform t, int layer)
        {
            if (t.gameObject.layer != layer) t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++)
                SetLayerIfNeeded(t.GetChild(i), layer);
        }

        static void DestroyTex(ref Texture2D tex)
        {
            if (tex == null) return;
            if (Application.isPlaying) Destroy(tex);
            else DestroyImmediate(tex);
            tex = null;
        }

        Image RegPanel(Image img, bool isChip, float scale)
        {
            themedPanels.Add((img, isChip, scale));
            return img;
        }

        Text RegText(Text txt, TextRole role)
        {
            themedTexts.Add((txt, role));
            return txt;
        }

        void ApplyTheme()
        {
            if (glassShader == null) glassShader = Shader.Find("Khkt/KinhMo");
            glassMat = Glass(glassShader);
            chipMat = Glass(glassShader, light: true);

            for (int i = 0; i < themedPanels.Count; i++)
            {
                var (img, isChip, scale) = themedPanels[i];
                if (img == null) continue;
                if (ReducedTransparency)
                {
                    img.material = null;
                    img.sprite = Solid;
                    img.pixelsPerUnitMultiplier = UiTheme.IsVera ? scale : Mathf.Max(scale * 2.5f, 2.5f);
                    img.color = Surface;
                }
                else
                {
                    img.material = isChip ? chipMat : glassMat;
                    img.sprite = Rounded;
                    img.pixelsPerUnitMultiplier = scale;
                    img.color = Color.white;
                }
            }

            for (int i = 0; i < themedTexts.Count; i++)
            {
                var (txt, role) = themedTexts[i];
                if (txt == null) continue;
                txt.color = role switch
                {
                    TextRole.Ink => Ink,
                    TextRole.Dim => Dim,
                    TextRole.Accent => Accent,
                    _ => OnAccent,
                };
            }

            for (int i = 0; i < themedBarBgs.Count; i++)
            {
                if (themedBarBgs[i] == null) continue;
                themedBarBgs[i].sprite = Solid;
                themedBarBgs[i].color = Wash(0.16f);
            }
            for (int i = 0; i < themedBarFills.Count; i++)
            {
                if (themedBarFills[i] == null) continue;
                themedBarFills[i].sprite = Solid;
                themedBarFills[i].color = Accent;
            }
            for (int i = 0; i < themedHintChipBgs.Count; i++)
            {
                if (themedHintChipBgs[i] == null) continue;
                themedHintChipBgs[i].sprite = Solid;
                themedHintChipBgs[i].color = new Color(Accent.r, Accent.g, Accent.b, 0.92f);
            }
            for (int i = 0; i < themedSolidSprites.Count; i++)
            {
                if (themedSolidSprites[i] != null) themedSolidSprites[i].sprite = Solid;
            }
            if (helpCloseBg != null)
            {
                helpCloseBg.sprite = Solid;
                helpCloseBg.pixelsPerUnitMultiplier = UiTheme.IsVera ? 2.2f : 3f;
                helpCloseBg.color = ButtonFill;
            }
            if (helpCloseBorder != null)
            {
                helpCloseBorder.sprite = Border;
                helpCloseBorder.pixelsPerUnitMultiplier = UiTheme.IsVera ? 2.2f : 3f;
                helpCloseBorder.color = Line;
            }
            if (loupeMaskImg != null) loupeMaskImg.sprite = Solid;
            if (cardInfoBtnBg != null)
            {
                cardInfoBtnBg.sprite = Solid;
                cardInfoBtnBg.pixelsPerUnitMultiplier = UiTheme.IsVera ? 2.2f : 3f;
                cardInfoBtnBg.color = AccentSoft;
            }
            if (cardInfoBtnBorder != null)
            {
                cardInfoBtnBorder.sprite = Border;
                cardInfoBtnBorder.pixelsPerUnitMultiplier = UiTheme.IsVera ? 2.2f : 3f;
                cardInfoBtnBorder.color = Line;
            }
            if (cardInfoBtnTxt != null) cardInfoBtnTxt.color = Accent;
            if (resultRule != null) resultRule.color = Line;
            if (resultGraphBoxBg != null) { resultGraphBoxBg.sprite = Solid; resultGraphBoxBg.color = Well; }
            if (resultGraphBoxBorder != null) { resultGraphBoxBorder.sprite = Border; resultGraphBoxBorder.color = Line; }
            if (resultBioBoxBg != null) { resultBioBoxBg.sprite = Solid; resultBioBoxBg.color = Well; }
            if (resultBioBoxBorder != null) { resultBioBoxBorder.sprite = Border; resultBioBoxBorder.color = Line; }
            if (resultRubricHeader != null) resultRubricHeader.color = Accent2;
            for (int i = 0; i < RubricRowCount; i++)
            {
                if (rubricCardBgs[i] != null) { rubricCardBgs[i].sprite = Solid; rubricCardBgs[i].color = Well; }
                if (rubricCardBorders[i] != null) { rubricCardBorders[i].sprite = Border; rubricCardBorders[i].color = Line; }
            }
            if (resultNewRunBg != null)
            {
                resultNewRunBg.sprite = Solid;
                resultNewRunBg.pixelsPerUnitMultiplier = UiTheme.IsVera ? 2.2f : 3f;
                resultNewRunBg.color = ButtonFill;
            }
            if (resultNewRunBorder != null)
            {
                resultNewRunBorder.sprite = Border;
                resultNewRunBorder.pixelsPerUnitMultiplier = UiTheme.IsVera ? 2.2f : 3f;
                resultNewRunBorder.color = Line;
            }
            if (resultNewRunTxt != null) resultNewRunTxt.color = Ink;

            drawnCurve = -1;
            panelCurveVersion = -1;
            cardTitleStr = null;
            cardNextRefresh = 0f;
            nextDataAt = 0f;
            nextLoupeTextAt = 0f;
            nextResultAt = 0f;
            hintShown = -1;
        }

        // =====================================================================
        // Dựng
        // =====================================================================

        void BuildStepper()
        {
            const float itemW = 150f;
            int n = 7;
            // Đặt ở y = -105 để tuyệt đối không bao giờ nằm đè phía sau cụm "< | Đổi khu thí nghiệm | >" (y = -20)
            var bar = RegPanel(Panel(root, "Thanh bước", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -105),
                            new Vector2(itemW * n + 24, 52), glassMat, 1.4f), false, 1.4f);
            stepBar = bar.gameObject;
            stepBar.SetActive(false); // Mặc định ẩn hoàn toàn thanh 7 bước
            for (int i = 0; i < n; i++)
            {
                float x = 12 + i * itemW;
                var dotRt = Rect(bar.transform, "Chấm", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(x + 8, 0), new Vector2(28, 28));
                var dot = dotRt.gameObject.AddComponent<Image>();
                dot.sprite = SolidCircle;
                dot.raycastTarget = false;
                var num = Label(dotRt, "Số", (i + 1).ToString(), 15, Ink, true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(28, 28));
                var lab = Label(bar.transform, "Tên", "", 16, Dim, false, TextAnchor.MiddleLeft,
                                new Vector2(x + 42, 13), new Vector2(itemW - 40, 26), new Vector2(0, 0.5f));
                steps.Add((dot, num, lab));
            }
        }

        void BuildHintBar()
        {
            var img = RegPanel(Panel(root, "Gợi ý thao tác", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(400, 54), glassMat, 1.6f), false, 1.6f);
            hintBar = img.rectTransform;
            hintBarImage = img;
            for (int i = 0; i < 5; i++)  // tay cầm 1 cần ở chế độ ĐI cần 5 gợi ý
            {
                var chip = Rect(hintBar, "Phím", new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(60, 32));
                var chipImg = chip.gameObject.AddComponent<Image>();
                chipImg.sprite = Solid;
                chipImg.type = Image.Type.Sliced;
                chipImg.pixelsPerUnitMultiplier = 3f;
                chipImg.color = new Color(Accent.r, Accent.g, Accent.b, 0.92f);
                chipImg.raycastTarget = false;
                themedHintChipBgs.Add(chipImg);
                var key = RegText(Label(chip, "Chữ phím", "", 15, OnAccent, true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(60, 32)), TextRole.OnAccent);
                key.horizontalOverflow = HorizontalWrapMode.Overflow;
                var lab = RegText(Label(hintBar, "Việc", "", 17, Ink, false, TextAnchor.MiddleLeft, Vector2.zero, new Vector2(300, 30), new Vector2(0, 0.5f)), TextRole.Ink);
                lab.horizontalOverflow = HorizontalWrapMode.Overflow;
                hintItems.Add((chip, key, lab));
            }
        }

        void BuildCard()
        {
            var img = RegPanel(Panel(root, "Card dụng cụ", new Vector2(0, 0), new Vector2(0, 0.5f), Vector2.zero, new Vector2(340, 150), glassMat, 1.3f), false, 1.3f);
            card = img.rectTransform;
            cardGroup = img.gameObject.AddComponent<CanvasGroup>();
            cardGroup.alpha = 0;
            cardGroup.blocksRaycasts = false;
            cardGroup.interactable = false;
            // Vạch nhấn chạy dọc mép trái: xanh ngọc với dụng cụ thường, vàng cảnh báo với hóa chất nguy hiểm
            var accent = Rect(card, "Vạch nhấn", new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -16), new Vector2(4, 46));
            cardAccent = accent.gameObject.AddComponent<Image>();
            cardAccent.sprite = Solid;
            cardAccent.type = Image.Type.Sliced;
            cardAccent.pixelsPerUnitMultiplier = 20f;
            cardAccent.color = Accent;
            cardAccent.raycastTarget = false;
            themedSolidSprites.Add(cardAccent);
            cardKind = Label(card, "Loại", "", 12, Accent, true, TextAnchor.UpperLeft, new Vector2(32, -14), new Vector2(244, 16));
            cardKind.horizontalOverflow = HorizontalWrapMode.Overflow;
            cardTitle = RegText(Label(card, "Tên", "", 21, Ink, true, TextAnchor.UpperLeft, new Vector2(32, -32), new Vector2(244, 30)), TextRole.Ink);
            cardStatus = RegText(Label(card, "Trạng thái", "", 15, Accent, false, TextAnchor.UpperLeft, new Vector2(32, -62), new Vector2(296, 22)), TextRole.Accent);
            cardBody = RegText(Label(card, "Mô tả", "", 15, Dim, false, TextAnchor.UpperLeft, new Vector2(32, -88), new Vector2(296, 50)), TextRole.Dim);

            cardInfoBtn = MakeButton(card, "Mở chi tiết", "i", new Vector2(card.sizeDelta.x - 54f, -10f), new Vector2(44f, 44f), OnCardInspectClicked, false, 18);
            cardInfoBtnBg = (Image)cardInfoBtn.targetGraphic;
            if (cardInfoBtnBg != null) cardInfoBtnBg.color = AccentSoft;
            cardInfoBtnBorder = cardInfoBtn.transform.Find("Viền")?.GetComponent<Image>();
            cardInfoBtnTxt = cardInfoBtn.transform.Find("Chữ")?.GetComponent<Text>();
            if (cardInfoBtnTxt != null) { cardInfoBtnTxt.font = Semibold; cardInfoBtnTxt.color = Accent; }
            cardInfoBtn.gameObject.SetActive(false);
        }

        void OnCardInspectClicked()
        {
            if (cardTarget == null) return;
            ToolPanel.OpenObject(cardTarget.gameObject);
        }

        void BuildLoupe()
        {
            var img = RegPanel(Panel(root, "Kính lúp", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-20, 30), new Vector2(300, 420), glassMat, 1.3f), false, 1.3f);
            loupePanel = img.gameObject;
            RegText(Label(img.transform, "Tiêu đề", "Kính lúp · vạch buret", 16, Dim, false, TextAnchor.UpperLeft, new Vector2(20, -14), new Vector2(260, 22)), TextRole.Dim);
            var maskRt = Rect(img.transform, "Khung ảnh", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -44), new Vector2(260, 260));
            loupeMaskImg = maskRt.gameObject.AddComponent<Image>();
            loupeMaskImg.sprite = Solid;
            loupeMaskImg.type = Image.Type.Sliced;
            loupeMaskImg.pixelsPerUnitMultiplier = 1.3f;
            maskRt.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var imRt = Rect(maskRt, "Ảnh", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260, 260));
            loupeImage = imRt.gameObject.AddComponent<RawImage>();
            loupeImage.raycastTarget = false;
            eyeText = RegText(Label(img.transform, "Mắt", "", 16, Ink, true, TextAnchor.UpperLeft, new Vector2(20, -314), new Vector2(260, 24)), TextRole.Ink);
            RegText(Label(img.transform, "Khóa", "Khóa", 15, Dim, false, TextAnchor.MiddleLeft, new Vector2(20, -350), new Vector2(60, 22)), TextRole.Dim);
            flowBar = Bar(img.transform, new Vector2(70, -361), out _);
            flowText = RegText(Label(img.transform, "Dòng", "", 14, Dim, false, TextAnchor.MiddleRight, new Vector2(170, -350), new Vector2(110, 22)), TextRole.Dim);
            RegText(Label(img.transform, "Lắc", "Lắc", 15, Dim, false, TextAnchor.MiddleLeft, new Vector2(20, -384), new Vector2(60, 22)), TextRole.Dim);
            swirlBar = Bar(img.transform, new Vector2(70, -395), out _);
        }

        Image Bar(Transform parent, Vector2 pos, out Image bg)
        {
            var bgRt = Rect(parent, "Nền thanh", new Vector2(0, 1), new Vector2(0, 0.5f), pos, new Vector2(110, 6));
            bg = bgRt.gameObject.AddComponent<Image>();
            bg.sprite = Solid;
            bg.type = Image.Type.Sliced;
            bg.pixelsPerUnitMultiplier = 10f;
            bg.color = Wash(0.16f);
            themedBarBgs.Add(bg);
            var fillRt = Rect(bgRt, "Mức", new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(0, 6));
            var fill = fillRt.gameObject.AddComponent<Image>();
            fill.sprite = Solid;
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = 10f;
            fill.color = Accent;
            themedBarFills.Add(fill);
            return fill;
        }

        void BuildData()
        {
            var img = RegPanel(Panel(root, "Số liệu", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-20, 24), new Vector2(300, 150), glassMat, 1.3f), false, 1.3f);
            dataPanel = img.gameObject;
            RegText(Label(img.transform, "Tiêu đề", "Số liệu", 16, Dim, false, TextAnchor.UpperLeft, new Vector2(20, -14), new Vector2(200, 22)), TextRole.Dim);
            dataLeft = RegText(Label(img.transform, "Nhãn", "V đầu\nV cuối\nV NaOH\nC HCl", 16, Dim, false, TextAnchor.UpperLeft, new Vector2(20, -42), new Vector2(120, 100)), TextRole.Dim);
            dataRight = RegText(Label(img.transform, "Giá trị", "", 16, Ink, true, TextAnchor.UpperRight, new Vector2(140, -42), new Vector2(140, 100)), TextRole.Ink);
            dataLeft.lineSpacing = dataRight.lineSpacing = 1.15f;
        }

        static Image MakeResultBox(Transform parent, string name, Vector2 pos, Vector2 size, out Image border, Color? tint = null, Color? borderColor = null)
        {
            var rt = Rect(parent, name, new Vector2(0, 1), new Vector2(0, 1), pos, size);
            var bg = rt.gameObject.AddComponent<Image>();
            bg.sprite = Solid;
            bg.type = Image.Type.Sliced;
            bg.pixelsPerUnitMultiplier = UiTheme.IsVera ? 2.2f : 2.8f;
            bg.color = tint ?? Well;
            bg.raycastTarget = false;

            var brt = new GameObject("Viền", typeof(RectTransform)).GetComponent<RectTransform>();
            brt.SetParent(rt, false);
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = brt.offsetMax = Vector2.zero;
            border = brt.gameObject.AddComponent<Image>();
            border.sprite = Border;
            border.type = Image.Type.Sliced;
            border.pixelsPerUnitMultiplier = bg.pixelsPerUnitMultiplier;
            border.color = borderColor ?? Line;
            border.raycastTarget = false;
            return bg;
        }

        void BuildResult()
        {
            var img = RegPanel(Panel(root, "Kết quả", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 8), new Vector2(596, 610), glassMat, 1.2f), false, 1.2f);
            resultPanel = img.gameObject;
            resultTitle = RegText(Label(img.transform, "Tiêu đề", "Kết quả", 15, Dim, false, TextAnchor.UpperLeft, new Vector2(26, -18), new Vector2(360, 22)), TextRole.Dim);
            resultBig = RegText(Label(img.transform, "Số lớn", "", 38, Ink, true, TextAnchor.UpperLeft, new Vector2(26, -42), new Vector2(368, 48)), TextRole.Ink);
            resultSub = RegText(Label(img.transform, "Thật", "", 14, Dim, false, TextAnchor.UpperLeft, new Vector2(26, -92), new Vector2(372, 22)), TextRole.Dim);

            var badge = Rect(img.transform, "Nhãn sai số", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-26, -22), new Vector2(164, 34));
            resultBadgeBg = badge.gameObject.AddComponent<Image>();
            resultBadgeBg.sprite = Solid;
            resultBadgeBg.type = Image.Type.Sliced;
            resultBadgeBg.pixelsPerUnitMultiplier = 2f;
            themedSolidSprites.Add(resultBadgeBg);
            resultBadge = RegText(Label(badge, "Chữ", "", 15, OnAccent, true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(164, 34)), TextRole.OnAccent);

            var scorePill = Rect(img.transform, "Tổng điểm", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-26, -64), new Vector2(164, 34));
            resultScorePillBg = scorePill.gameObject.AddComponent<Image>();
            resultScorePillBg.sprite = Solid;
            resultScorePillBg.type = Image.Type.Sliced;
            resultScorePillBg.pixelsPerUnitMultiplier = 2f;
            themedSolidSprites.Add(resultScorePillBg);
            resultScorePill = Label(scorePill, "Chữ", "", 15, Good, true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(164, 34));

            resultRule = Fill(img.transform, "Đường kẻ kết quả", new Vector2(26, -122), new Vector2(544, 1.5f), Line);

            resultGraphBoxBg = MakeResultBox(img.transform, "Khung đồ thị", new Vector2(26, -132), new Vector2(544, 130), out resultGraphBoxBorder);
            resultGraphBox = resultGraphBoxBg.gameObject;
            var gRt = Rect(resultGraphBox.transform, "Đồ thị", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(524, 114));
            var gImg = gRt.gameObject.AddComponent<RawImage>();
            gImg.raycastTarget = false;
            graphTex = new Texture2D(GraphW, GraphH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            gImg.texture = graphTex;

            resultBioBoxBg = MakeResultBox(img.transform, "Khung tóm tắt Sinh", new Vector2(26, -132), new Vector2(544, 130), out resultBioBoxBorder);
            resultBioBox = resultBioBoxBg.gameObject;
            resultBioHeadline = RegText(Label(resultBioBox.transform, "Tóm tắt", "", 15, Ink, true, TextAnchor.UpperLeft, new Vector2(16, -14), new Vector2(512, 24)), TextRole.Ink);
            resultBioDetails = RegText(Label(resultBioBox.transform, "Chi tiết", "", 14, Dim, false, TextAnchor.UpperLeft, new Vector2(16, -44), new Vector2(512, 72)), TextRole.Dim);
            resultBioBox.SetActive(false);

            resultRubricHeader = Label(img.transform, "Tiêu đề tiêu chí", "BẢNG CHẤM ĐIỂM THEO TIÊU CHÍ", 12, Accent2, true, TextAnchor.UpperLeft, new Vector2(26, -272), new Vector2(360, 18));

            for (int i = 0; i < RubricRowCount; i++)
            {
                float y = -296f - i * 58f;
                var rowBg = MakeResultBox(img.transform, $"Tiêu chí {i + 1}", new Vector2(26, y), new Vector2(544, 52), out var rowBorder);
                rubricCardBgs[i] = rowBg;
                rubricCardBorders[i] = rowBorder;

                var stripe = Fill(rowBg.transform, "Vạch", Vector2.zero, new Vector2(4, 52), Accent);
                rubricStripes[i] = stripe;

                var titleTxt = RegText(Label(rowBg.transform, "Tên", "", 14, Ink, true, TextAnchor.UpperLeft, new Vector2(16, -6), new Vector2(404, 20)), TextRole.Ink);
                titleTxt.verticalOverflow = VerticalWrapMode.Truncate;
                rubricTitles[i] = titleTxt;

                var detTxt = RegText(Label(rowBg.transform, "Chi tiết", "", 12, Dim, false, TextAnchor.UpperLeft, new Vector2(16, -27), new Vector2(404, 18)), TextRole.Dim);
                detTxt.verticalOverflow = VerticalWrapMode.Truncate;
                rubricDetails[i] = detTxt;

                var pillRt = Rect(rowBg.transform, "Điểm", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-12, 0), new Vector2(100, 30));
                var pillImg = pillRt.gameObject.AddComponent<Image>();
                pillImg.sprite = Solid;
                pillImg.type = Image.Type.Sliced;
                pillImg.pixelsPerUnitMultiplier = 2.4f;
                pillImg.raycastTarget = false;
                themedSolidSprites.Add(pillImg);
                rubricPillBgs[i] = pillImg;
                rubricScores[i] = Label(pillRt, "Chữ", "", 13, Good, true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(100, 30));
            }

            resultNotes = RegText(Label(img.transform, "Ghi chú", "", 13, Dim, false, TextAnchor.UpperLeft, new Vector2(26, -534), new Vector2(376, 58)), TextRole.Dim);
            resultNotes.verticalOverflow = VerticalWrapMode.Truncate;
            string redoLabel = Khkt.Input.PadInput.UsingTouch ? "Lượt mới" : "Làm lượt mới (R)";
            resultNewRunBtn = MakeButton(img.transform, "Làm lại", redoLabel, new Vector2(416, -542), new Vector2(154, 46), OnResultNewRunClicked, false, 15);
            resultNewRunBg = resultNewRunBtn.targetGraphic as Image;
            resultNewRunBorder = resultNewRunBtn.transform.Find("Viền")?.GetComponent<Image>();
            resultNewRunTxt = resultNewRunBtn.transform.Find("Chữ")?.GetComponent<Text>();
            if (resultNewRunTxt != null) RegText(resultNewRunTxt, TextRole.Ink);
            resultPanel.SetActive(false);
        }

        void OnResultNewRunClicked()
        {
            if (resultShowingBio) Khkt.Demo.Bio.BioLabProcedure.Instance?.NewRun();
            else procedure?.NewRun();
        }

        void BuildGraph()
        {
            var img = RegPanel(Panel(root, "Đồ thị pH", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, -90), new Vector2(GraphW + 40, GraphH + 60), glassMat, 1.3f), false, 1.3f);
            graphPanel = img.gameObject;
            RegText(Label(img.transform, "Tiêu đề", "pH theo NaOH đã nhỏ (ml)", 15, Dim, false, TextAnchor.UpperLeft, new Vector2(20, -12), new Vector2(400, 22)), TextRole.Dim);
            var gRt = Rect(img.transform, "Ảnh", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(GraphW, GraphH));
            graphImage = gRt.gameObject.AddComponent<RawImage>();
            graphImage.texture = graphTex;
            graphPanel.SetActive(false);
        }

        void BuildHelp()
        {
            var chip = RegPanel(Panel(root, "Nút trợ giúp", new Vector2(0, 0), new Vector2(0, 0), new Vector2(20, 24), new Vector2(150, 44), glassMat, 2f), false, 2f);
            helpBtn = chip.gameObject;
            string chipHelpText = Khkt.Input.PadInput.UsingTouch ? "<b>?</b>   Trợ giúp" : "<b>H</b>   Trợ giúp";
            RegText(Label(chip.transform, "Chữ", chipHelpText, 16, Dim, false, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(150, 44)), TextRole.Dim);
            var img = RegPanel(Panel(root, "Trợ giúp", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(20, 0), new Vector2(820, 650), glassMat, 1.3f), false, 1.3f);
            helpPanel = img.gameObject;
            var closeHelpBtn = MakeButton(img.transform, "Đóng trợ giúp", "✕", new Vector2(740, -10), new Vector2(66, 65), () => helpPanel.SetActive(false));
            helpCloseBg = closeHelpBtn.targetGraphic as Image;
            helpCloseBorder = closeHelpBtn.transform.Find("Viền")?.GetComponent<Image>();
            var chTxt = closeHelpBtn.transform.Find("Chữ")?.GetComponent<Text>();
            if (chTxt != null) RegText(chTxt, TextRole.Ink);

            bool usingTouch = Khkt.Input.PadInput.UsingTouch;
            string titleLeft = usingTouch ? "Điều khiển cảm ứng iPad" : "Điều khiển bàn phím & chuột";
            RegText(Label(img.transform, "Tiêu đề", titleLeft, 20, Ink, true, TextAnchor.UpperLeft, new Vector2(24, -18), new Vector2(360, 30)), TextRole.Ink);
            var rows = usingTouch ? new (string k, string v)[]
            {
                ("Vuốt nửa trái", "Cần di chuyển nổi (đi lại)"),
                ("Vuốt nửa phải", "Xoay góc nhìn camera"),
                ("Nút TAY", "Chạm: cầm, đặt, bấm máy"),
                ("Giữ nút TAY", "Rót dung dịch, hút pipet, xả"),
                ("Nút Trả về", "Đặt dụng cụ đang cầm về chỗ"),
                ("Nút Lắc", "Giữ để lắc bình nón"),
                ("Nút Chậm", "Bật/tắt bước chậm để ngắm"),
                ("Ngồi / Đứng", "Hạ thấp góc nhìn quan sát"),
                ("Mắt ▲  ▼", "Nâng, hạ tầm mắt ngang vạch"),
                ("Mở +  Đóng -", "Vặn mở / đóng khóa buret"),
                ("Khóa buret", "Khóa chặt vòi buret"),
                ("Ghi số đọc", "Ghi thể tích buret vào bảng"),
                ("Menu ☰", "Đồ thị pH, lượt mới, độ nhạy"),
                ("Đổi trạm", "Chuyển nhanh tới 12 trạm lab"),
                ("Trợ lý AI", "Chạm biểu tượng Robot để hỏi"),
                ("Tra cứu", "Chạm máy tra cứu tìm hóa chất"),
            } : new (string k, string v)[]
            {
                ("Rê chuột", "Nhìn quanh"), ("W A S D", "Đi lại"), ("C", "Ngồi / đứng"), ("Z  X", "Hạ / nâng tầm mắt"),
                ("← →  PgUp PgDn", "Xoay, ngẩng bằng phím"), ("1 – 9   0", "Đến các khu"), ("Chuột trái", "Cầm, đặt, dùng"), ("Giữ chuột trái", "Rót, hút, xả, lắc"),
                ("Q", "Trả dụng cụ về chỗ"), ("Lăn chuột", "Vặn khóa buret"), ("↑ ↓   Space", "Mở, đóng, khóa hẳn"),
                ("F", "Lắc bình nón"), ("Enter", "Ghi số đọc buret"), ("G", "Đồ thị pH"), ("R", "Làm lượt chuẩn độ mới"),
                ("Tab   Alt", "Nhìn theo chuột · thả con trỏ"), ("B", "Tra cứu hóa chất"), ("T", "Trợ lý AI"), ("Esc", "Menu, chọn bài"),
#if !UNITY_IOS
                ("F8  F9  F12", "Đầu qua webcam · xem mô phỏng · căn lại"),
#endif
            };
            for (int i = 0; i < rows.Length; i++)
            {
                float y = -62 - i * 28.5f;
                var c = RegPanel(Panel(img.transform, "Phím", new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, y), new Vector2(150, 24), chipMat, 4f), true, 4f);
                RegText(Label(c.transform, "Chữ", rows[i].k, 14, Ink, true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(150, 24)), TextRole.Ink);
                RegText(Label(img.transform, "Việc", rows[i].v, 15, Dim, false, TextAnchor.MiddleLeft, new Vector2(190, y), new Vector2(210, 24)), TextRole.Dim);
            }

            // Cột phải: Thao tác lab trên iPad hoặc tay cầm joystick trên PC
            string titleRight = usingTouch ? "Thao tác phòng lab & thiết bị" : "Tay cầm joystick  ·  Xbox";
            RegText(Label(img.transform, "Tiêu đề tay cầm", titleRight, 20, Ink, true, TextAnchor.UpperLeft, new Vector2(424, -18), new Vector2(370, 30)), TextRole.Ink);
            var padRows = usingTouch ? new (string k, string v)[]
            {
                ("Kính hiển vi", "Chạm máy, chạm kính soi mẫu"),
                ("Máy phân tích", "Chạm màn hình 14 máy"),
                ("Tủ hút khí độc", "Đóng mở kính, kẹp giữ bình"),
                ("Kho hóa chất", "Tra cứu số kệ, hàng, ngăn"),
                ("Cân điện tử", "Đặt bình, trừ bì, cân mẫu"),
                ("Máy khuấy từ", "Bật núm nhiệt, núm khuấy"),
                ("Buret chuẩn độ", "Chỉnh giọt, đổi màu chỉ thị"),
                ("Khu sinh học", "Tầng 2: PCR, điện di, vi sinh"),
                ("Ghép gen", "Tách chiết DNA, biến nạp"),
                ("Nhà kính", "Nuôi cấy mô, quang hợp cây"),
                ("Cài đặt chung", "Bật tắt âm, giảm chuyển động"),
                ("Giao diện kính", "Tùy chọn giảm trong suốt"),
            } : new (string k, string v)[]
            {
                ("ĐI: cần ↑ ↓", "Tiến, lùi (đẩy mạnh: nhanh)"), ("ĐI: cần ← →", "Quay mặt trái, phải"),
                ("ĐI: cần chéo", "Vừa đi vừa rẽ"), ("ĐI: ↑ hết cỡ 1 s", "Chạy nhanh"),
                ("ĐI: giữ nút + ← →", "Bước ngang"), ("ĐI: giữ nút + ↑ ↓", "Ngẩng, cúi nhìn"),
                ("ĐI: nhấn 1 lần", "Sang chế độ TAY"), ("ĐI: nhấn 2 lần", "Ngồi / đứng"), ("ĐI: giữ nút 1 s", "Sang khu kế tiếp"),
                ("TAY: cần", "Ngắm · trên khóa buret: vặn"), ("TAY: nhấn / giữ", "Cầm, đặt, dùng / rót, hút"),
                ("TAY: nhấn chỗ trống", "Về chế độ ĐI"), ("Xbox LS RS A B", "Đi · nhìn · cầm · trả"),
            };
            for (int i = 0; i < padRows.Length; i++)
            {
                float y = -62 - i * 28.5f;
                var c = RegPanel(Panel(img.transform, "Nút", new Vector2(0, 1), new Vector2(0, 1), new Vector2(424, y), new Vector2(150, 24), chipMat, 4f), true, 4f);
                RegText(Label(c.transform, "Chữ", padRows[i].k, 14, Ink, true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(150, 24)), TextRole.Ink);
                RegText(Label(img.transform, "Việc", padRows[i].v, 15, Dim, false, TextAnchor.MiddleLeft, new Vector2(590, y), new Vector2(220, 24)), TextRole.Dim);
            }
            padStatus = RegText(Label(img.transform, "Trạng thái tay cầm", "", 14, Dim, false, TextAnchor.UpperLeft,
                              new Vector2(424, -62 - padRows.Length * 28.5f - 8), new Vector2(380, 120)), TextRole.Dim);
            helpPanel.SetActive(false);
        }

        Text padStatus;

        /// <summary>Trạng thái các tay cầm trong bảng trợ giúp (cổng COM, Wi-Fi, tay cầm chơi game): để biết vì sao chưa nối được.</summary>
        void UpdatePadStatus()
        {
            if (padStatus == null || !helpPanel.activeSelf) return;
            var lines = new System.Collections.Generic.List<string>();
            foreach (var src in Khkt.Input.PadInput.Sources)
                if (src != null && src.isActiveAndEnabled) lines.Add(src.StatusText);
            if (Khkt.Input.PadInput.ControllerConnected && Khkt.Input.PadInput.SingleStick)
                lines.Add($"Tay cầm 1 cần, chế độ {(Khkt.Input.PadInput.Mode == Khkt.Input.PadInput.StickMode.Walk ? "ĐI" : "TAY")}");
            if (UnityEngine.InputSystem.Gamepad.current != null) lines.Add($"Tay cầm chơi game: {UnityEngine.InputSystem.Gamepad.current.displayName}");
            padStatus.text = lines.Count > 0 ? string.Join("\n", lines) : "Chưa bật nguồn tay cầm nào";
        }

        public void ToggleHelp()
        {
            if (helpPanel == null) return;
            if (!helpPanel.activeSelf) UiMotion.OpenPanel(this, helpPanel, 0.20f);
            else UiMotion.ClosePanel(this, helpPanel, 0.14f);
        }

        public void ToggleGraph()
        {
            if (graphPanel == null) return;
            if (!graphPanel.activeSelf) UiMotion.OpenPanel(this, graphPanel, 0.20f);
            else UiMotion.ClosePanel(this, graphPanel, 0.14f);
        }

        // =====================================================================
        // Cập nhật
        // =====================================================================

        static bool IsModalOpen() =>
            ToolPanel.IsOpen || MachinePanel.IsOpen || IsSafetyModalOpen || Khkt.Environment.DesktopViewer.UiCapture > 0;

        void LateUpdate()
        {
            if (uiCamera != null && viewCamera != null)
            {
                if (uiCamera.enabled != viewCamera.enabled) uiCamera.enabled = viewCamera.enabled;
                uiCamera.fieldOfView = viewCamera.fieldOfView;
                uiCamera.rect = viewCamera.rect;
            }
            if (Canvas != null) SetLayerIfNeeded(Canvas.transform, UiLayer);

            bool modalOpen = IsModalOpen();
            if (helpBtn != null && helpBtn.activeSelf != !modalOpen) helpBtn.SetActive(!modalOpen);
            crosshair.enabled = !modalOpen &&
                                (Khkt.Environment.DesktopViewer.MouseLook || Khkt.Input.PadInput.AimFromCenter);
            UpdatePadStatus();
            crosshair.color = hand != null && hand.Current != HandController.Act.None ? Accent : new Color(1, 1, 1, 0.85f);
            UpdateHints();
            UpdateCard();
            if (modalOpen)
            {
                if (stepBar != null && stepBar.activeSelf) stepBar.SetActive(false);
                if (loupePanel != null && loupePanel.activeSelf) loupePanel.SetActive(false);
                if (dataPanel != null && dataPanel.activeSelf) dataPanel.SetActive(false);
                if (resultPanel != null && resultPanel.activeSelf) resultPanel.SetActive(false);
                if (graphPanel != null && graphPanel.activeSelf) graphPanel.SetActive(false);
                if (helpPanel != null && helpPanel.activeSelf) helpPanel.SetActive(false);
                UpdateToast();
                return;
            }
            UpdateToast();
            bool onFloor2 = viewCamera != null && viewCamera.transform.position.y > 3.0f;
            var bioProc = onFloor2 ? Khkt.Demo.Bio.BioLabProcedure.Instance : null;
            if (bioProc != null && bioProc.Active)
            {
                if (!stepBar.activeSelf) stepBar.SetActive(true);
                if (loupePanel.activeSelf) loupePanel.SetActive(false);
                if (dataPanel.activeSelf) dataPanel.SetActive(false);
                if (graphPanel.activeSelf) graphPanel.SetActive(false);
                UpdateBioStepper(bioProc);
                bool showBioResult = bioProc.LastResult.HasValue;
                if (resultPanel.activeSelf != showBioResult) { resultPanel.SetActive(showBioResult); nextResultAt = 0f; }
                if (showBioResult) UpdateBioResult(bioProc.LastResult.Value);
                return;
            }
            if (procedure == null)
            {
                if (stepBar != null && stepBar.activeSelf) stepBar.SetActive(false);
                return;
            }
            if (stepBar.activeSelf != procedure.Active) stepBar.SetActive(procedure.Active);
            if (!procedure.Active)
            {
                if (loupePanel.activeSelf) loupePanel.SetActive(false);
                if (dataPanel.activeSelf) dataPanel.SetActive(false);
                if (resultPanel.activeSelf) resultPanel.SetActive(false);
                if (graphPanel.activeSelf) graphPanel.SetActive(false);
                return;
            }
            UpdateStepper();

            bool near = reading != null && reading.eye != null && Vector3.Distance(reading.eye.position, reading.MeniscusPosition) < 2.5f;
            UpdateLoupe(near);
            if (dataPanel.activeSelf != near) { dataPanel.SetActive(near); nextDataAt = 0f; }
            UpdateData();
            bool showResult = near && procedure.LastResult.HasValue;
            if (resultPanel.activeSelf != showResult) { resultPanel.SetActive(showResult); nextResultAt = 0f; }
            if (showResult) UpdateResult(procedure.LastResult.Value);
            if (!near && graphPanel.activeSelf) graphPanel.SetActive(false);
            if ((graphPanel.activeSelf || showResult) && (drawnCurve != procedure.CurveVersion || drawnResult != procedure.LastResult.HasValue))
                DrawGraph();
        }

        void UpdateStepper()
        {
            for (int i = 0; i < steps.Count; i++)
            {
                bool done = procedure.Done[i];
                bool current = (int)procedure.Current == i;
                var (dot, num, lab) = steps[i];
                lab.text = procedure.StepNames[i];
                dot.color = done ? new Color(Good.r, Good.g, Good.b, 0.9f) : current ? Accent : Wash(0.14f);
                num.text = done ? "✓" : StepNums[i];
                num.color = done || current ? OnAccent : Dim;
                lab.color = current ? Ink : done ? new Color(Good.r, Good.g, Good.b, 0.85f) : Dim;
                lab.font = current ? Semibold : Regular;
            }
        }

        void UpdateBioStepper(Khkt.Demo.Bio.BioLabProcedure bioProc)
        {
            for (int i = 0; i < steps.Count && i < bioProc.Done.Length; i++)
            {
                bool done = bioProc.Done[i];
                bool current = (int)bioProc.Current == i;
                var (dot, num, lab) = steps[i];
                lab.text = bioProc.StepNames != null && i < bioProc.StepNames.Length ? bioProc.StepNames[i] : $"Bước {i + 1}";
                dot.color = done ? new Color(Good.r, Good.g, Good.b, 0.9f) : current ? Accent : Wash(0.14f);
                num.text = done ? "✓" : StepNums[i];
                num.color = done || current ? OnAccent : Dim;
                lab.color = current ? Ink : done ? new Color(Good.r, Good.g, Good.b, 0.85f) : Dim;
                lab.font = current ? Semibold : Regular;
            }
        }

        /// <summary>Thông báo do LabToasts vẽ (thẻ xếp chồng); ở đây chỉ báo cho nó biết phải né thanh bước.</summary>
        void UpdateToast()
        {
            LabToasts.TopOffset = stepBar != null && stepBar.activeSelf ? 170f : 86f;
        }

        void UpdateHints()
        {
            bool modalOpen = IsModalOpen();
            int n = !modalOpen && hand != null && !ClassRules.HideHelp ? Mathf.Min(hand.Hints.Count, hintItems.Count) : 0;
            if (hintBar.gameObject.activeSelf != (n > 0)) hintBar.gameObject.SetActive(n > 0);
            if (n == 0) { hintShown = 0; return; }

            // Đo bề rộng chữ và xếp lại là việc đắt (dựng lưới chữ, dựng lại canvas): chỉ làm khi gợi ý thật sự đổi
            bool changed = n != hintShown;
            for (int i = 0; i < n && !changed; i++)
                changed = hintKeys[i] != hand.Hints[i].key || hintTexts[i] != hand.Hints[i].text;
            if (changed)
            {
                hintShown = n;
                float x = 14;
                for (int i = 0; i < hintItems.Count; i++)
                {
                    var (chip, key, label) = hintItems[i];
                    bool on = i < n;
                    if (chip.gameObject.activeSelf != on) chip.gameObject.SetActive(on);
                    if (label.gameObject.activeSelf != on) label.gameObject.SetActive(on);
                    if (!on) continue;
                    hintKeys[i] = hand.Hints[i].key;
                    hintTexts[i] = hand.Hints[i].text;
                    key.text = hintKeys[i] ?? "";
                    label.text = hintTexts[i] ?? "";
                    float kw = Width(key, key.text) + 24;
                    chip.sizeDelta = new Vector2(kw, 32);
                    key.rectTransform.sizeDelta = new Vector2(kw, 32);
                    chip.anchoredPosition = new Vector2(x, 0);
                    x += kw + 10;
                    float lw = Width(label, label.text);
                    label.rectTransform.anchoredPosition = new Vector2(x, 15);
                    label.rectTransform.sizeDelta = new Vector2(lw + 4, 30);
                    x += lw + 26;
                }
                hintBar.sizeDelta = new Vector2(x - 12, 54);
            }
            if (changed || hand.Acting != hintActing)
            {
                hintActing = hand.Acting;
                hintBarImage.color = ReducedTransparency
                    ? (hintActing ? AccentSoft : Surface)
                    : (hintActing ? Color.Lerp(Color.white, Accent, 0.25f) : Color.white);
            }
        }

        static bool IsCardInspectable(Transform t)
        {
            if ((!ToolPanel.Enabled && !ToolPanel.ForceEnableForTest) || t == null) return false;
            if (t.GetComponentInParent<IMachinePanel>() != null) return false;
            return t.GetComponentInParent<BuretStopcock>() != null
                || t.GetComponentInParent<ChemicalBottle>() != null
                || t.GetComponentInParent<LabContainer>() != null
                || t.GetComponentInParent<Grabbable>() != null;
        }

        void UpdateCard()
        {
            // Tay rỗng: card của vật đang trỏ. Đang cầm pipet: card của pipet (mực so với vạch).
            // Đang cầm đồ khác và trỏ vào vật chứa: card của vật chứa đó (đang chứa gì, bao nhiêu).
            Transform target = null;
            if (hand != null)
            {
                if (hand.Held == null) target = hand.HoverRoot;
                else if (hand.Held.GetComponent<Pipette>() != null) target = hand.Held.transform;
                else if (hand.TargetContainer != null) target = hand.TargetContainer.transform;
            }

            bool newTarget = target != cardTarget;
            if (newTarget)
            {
                // Tra thành phần và khối bao chỉ một lần cho mỗi vật (trước đây tra lại mỗi khung hình)
                cardTarget = target;
                cardInfo = null;
                cardChem = null;
                if (target != null)
                {
                    cardInfo = target.GetComponentInParent<EquipmentInfo>();
                    if (cardInfo == null) cardInfo = target.GetComponentInChildren<EquipmentInfo>();
                    cardChem = target.GetComponentInParent<ChemicalBottle>();
                    cardIsTool = target.GetComponentInParent<Grabbable>() != null;
                    var rs = target.GetComponentsInChildren<Renderer>();
                    cardBounds = rs.Length > 0 ? rs[0].bounds : new Bounds(target.position, Vector3.one * 0.1f);
                    foreach (var r in rs) if (!(r is ParticleSystemRenderer)) cardBounds.Encapsulate(r.bounds);
                }
            }
            bool hasChem = cardChem != null && cardChem.Entry != null;
            bool modalOpen = IsModalOpen();
            bool show = !modalOpen && target != null && (cardInfo != null || hasChem) && !resultPanel.activeSelf;
            bool canInspect = show && IsCardInspectable(target);
            if (cardInfoBtn != null && cardInfoBtn.gameObject.activeSelf != canInspect)
                cardInfoBtn.gameObject.SetActive(canInspect);
            cardGroup.blocksRaycasts = canInspect;
            cardGroup.interactable = canInspect;
            if (modalOpen)
            {
                cardGroup.alpha = 0f;
                return;
            }
            cardGroup.alpha = Mathf.MoveTowards(cardGroup.alpha, show ? 1f : 0f, Time.unscaledDeltaTime * 8f);
            if (!show) return;

            // Chữ trên card: dựng khi đổi vật, sau đó 5 lần/giây (thể tích, nhiệt độ, số cân vẫn chạy theo kịp mắt nhìn)
            if (newTarget || Time.unscaledTime >= cardNextRefresh)
            {
                cardNextRefresh = Time.unscaledTime + 0.2f;
                RefreshCardText(target, hasChem, canInspect);
            }

            // Đặt card bên phải vật, ngang phần trên của vật
            Vector3 anchorWorld = cardBounds.center + Vector3.up * cardBounds.extents.y * 0.6f + viewCamera.transform.right * cardBounds.extents.magnitude * 0.6f;
            Vector3 sp = viewCamera.WorldToScreenPoint(anchorWorld);
            if (sp.z < 0) { cardGroup.alpha = 0; cardGroup.blocksRaycasts = false; return; }
            Camera canvasCam = Canvas != null && Canvas.worldCamera != null ? Canvas.worldCamera : viewCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, sp, canvasCam, out Vector2 local);
            Vector2 half = root.rect.size / 2;
            Vector2 pos = local + half + new Vector2(28, 0);
            pos.x = Mathf.Clamp(pos.x, 20, root.rect.width - card.sizeDelta.x - 20);
            pos.y = Mathf.Clamp(pos.y, card.sizeDelta.y / 2 + 90, root.rect.height - card.sizeDelta.y / 2 - 90);
            card.anchoredPosition = Vector2.Lerp(card.anchoredPosition, pos, cardGroup.alpha < 0.05f ? 1f : 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
            // Card nở nhẹ khi hiện ra (bỏ khi bật Giảm chuyển động)
            float pop = Khkt.Environment.Tang2.ReducedMotion.IsEnabled ? 1f : Mathf.Lerp(0.96f, 1f, cardGroup.alpha);
            card.localScale = new Vector3(pop, pop, 1f);
        }

        void RefreshCardText(Transform target, bool hasChem, bool canInspect)
        {
            string title, status, body, kind;
            bool hazard = false;
            if (hasChem)
            {
                var e = cardChem.Entry;
                title = e.ten + (string.IsNullOrEmpty(e.cong_thuc) ? "" : "  " + e.Formula);
                string hz = Khkt.Simulation.ChemicalCatalog.HazardText(e);
                hazard = hz.Length > 0;
                status = Khkt.Simulation.ChemicalCatalog.StateText(e) + " · " + Khkt.Simulation.ChemicalCatalog.ConcText(e) + (hazard ? $"  <color={HexWarn}>⚠ " + hz + "</color>" : "");
                body = Khkt.Simulation.ChemicalCatalog.Instance != null ? Khkt.Simulation.ChemicalCatalog.Instance.LocationText(e) : e.ma_vi_tri;
                kind = hazard ? "HÓA CHẤT · CẨN THẬN" : "HÓA CHẤT";
            }
            else
            {
                title = cardInfo.title;
                status = Status(target);
                body = FirstSentence(cardInfo.description);
                kind = cardIsTool ? "DỤNG CỤ" : "THIẾT BỊ";
            }
            if (canInspect)
            {
                string inspectHint = $"<color={HexAccent}>Giữ hoặc bấm (i) để mở bảng</color>";
                body = string.IsNullOrEmpty(body) ? inspectHint : $"{body}\n{inspectHint}";
            }
            if (title == cardTitleStr && status == cardStatusStr && body == cardBodyStr && canInspect == cardLastCanInspect) return;  // không đổi: khỏi xếp lại
            cardTitleStr = title;
            cardStatusStr = status;
            cardBodyStr = body;
            cardLastCanInspect = canInspect;

            Color tone = hazard ? Warn : Accent;
            cardAccent.color = tone;
            cardKind.color = new Color(tone.r, tone.g, tone.b, 0.92f);
            cardKind.text = kind;
            cardTitle.text = title ?? "";
            cardStatus.color = Accent;
            cardStatus.text = status ?? "";
            cardBody.text = body ?? "";
            bool hasStatus = cardStatus.text.Length > 0;
            if (cardStatus.gameObject.activeSelf != hasStatus) cardStatus.gameObject.SetActive(hasStatus);
            // Xếp dòng theo chiều cao thật của chữ (tên dài xuống 2 dòng không đè lên dòng dưới)
            float y = -32;
            float titleW = canInspect ? 244f : 296f;
            float titleH = Mathf.Max(28, cardTitle.preferredHeight);
            cardKind.rectTransform.sizeDelta = new Vector2(titleW, 16);
            cardTitle.rectTransform.sizeDelta = new Vector2(titleW, titleH);
            cardAccent.rectTransform.sizeDelta = new Vector2(4, 16 + titleH);
            y -= titleH + 2;
            if (hasStatus)
            {
                cardStatus.rectTransform.anchoredPosition = new Vector2(32, y);
                float sh = Mathf.Max(20, cardStatus.preferredHeight);
                cardStatus.rectTransform.sizeDelta = new Vector2(296, sh);
                y -= sh + 4;
            }
            cardBody.rectTransform.anchoredPosition = new Vector2(32, y);
            float bodyH = cardBody.text.Length > 0 ? cardBody.preferredHeight : 0f;
            cardBody.rectTransform.sizeDelta = new Vector2(296, Mathf.Max(20f, bodyH));
            card.sizeDelta = new Vector2(340, -y + bodyH + 18);
            if (cardInfoBtn != null)
                ((RectTransform)cardInfoBtn.transform).anchoredPosition = new Vector2(card.sizeDelta.x - 54f, -10f);
        }

        static string FirstSentence(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            int i = s.IndexOf(". ");
            return i > 0 ? s.Substring(0, i + 1) : s;
        }

        string Status(Transform t)
        {
            var p = t.GetComponentInParent<Pipette>();
            if (p != null)
            {
                if (p.IsEmpty) return "Trống · vạch định mức 25,00 ml";
                float mm = p.OffsetFromMarkMm;
                string where = Mathf.Abs(mm) < 0.6f ? "đúng vạch" : mm > 0 ? $"trên vạch {LabProcedure.F(mm, "0")} mm" : $"dưới vạch {LabProcedure.F(-mm, "0")} mm";
                return $"{LabProcedure.F(p.VolumeMl, "0.0")} ml · {where}";
            }
            var b = t.GetComponentInParent<BuretStopcock>();
            if (b != null)
            {
                if (!b.mounted) return "Chưa lắp lên giá";
                if (!b.HasLiquid) return "Đã lắp · trống";
                return b.Opening > 0.001f ? $"Khóa mở {b.Opening * 100f:0} %" : "Khóa đóng";
            }
            var mp = t.GetComponentInParent<MeterProbe>();
            if (mp != null) return string.IsNullOrEmpty(mp.LastReading) ? "Nhúng đầu đo vào dung dịch" : mp.LastReading;
            var tp = t.GetComponentInParent<TestPaper>();
            if (tp != null) return string.IsNullOrEmpty(tp.LastResult) ? "Giấy mới" : tp.LastResult;
            var bs = t.GetComponentInParent<BalanceScale>();
            if (bs != null) return LabProcedure.F(bs.Grams, "0.00") + " g trên đĩa";
            var c = t.GetComponentInParent<LabContainer>();
            if (c != null) return c.Total.Describe() + (c.TemperatureC > 32f ? $" · {c.TemperatureC:0} °C" : "");
            var tg = t.GetComponentInParent<ToggleEffect>();
            if (tg != null) return tg.isOn ? "Đang bật" : "Đang tắt";
            return "";
        }

        void UpdateLoupe(bool near)
        {
            var b = procedure.buret;
            bool on = near && loupe != null && loupe.Active && b.mounted && b.HasLiquid;
            if (loupePanel.activeSelf != on) loupePanel.SetActive(on);
            if (!on) return;
            loupeImage.texture = loupe.Texture;
            SetBar(flowBar, b.Opening);
            SetBar(swirlBar, procedure.swirl != null ? procedure.swirl.Intensity : 0f);
            if (Time.unscaledTime < nextLoupeTextAt) return;  // thanh mức chạy mỗi khung hình, chữ số 10 lần/giây là đủ
            nextLoupeTextAt = Time.unscaledTime + 0.1f;
            float cm = reading.EyeAboveMeniscusM * 100f;
            eyeText.text = Mathf.Abs(cm) < 1f ? $"<color={HexGood}>●</color>  Mắt ngang vạch"
                         : cm > 0 ? $"<color={HexWarn}>●</color>  Mắt cao {LabProcedure.F(cm, "0")} cm · nhấn Z"
                         : $"<color={HexWarn}>●</color>  Mắt thấp {LabProcedure.F(-cm, "0")} cm · nhấn X";
            flowText.text = b.Opening <= 0.001f ? "đóng" : b.IsStream ? "chảy dòng" : $"{LabProcedure.F(b.FlowMlPerS / b.dropMl, "0.0")} giọt/s";
        }

        static void SetBar(Image fill, float v)
        {
            var rt = fill.rectTransform;
            rt.sizeDelta = new Vector2(110f * Mathf.Clamp01(v), rt.sizeDelta.y);
            fill.enabled = v > 0.005f;
        }

        void UpdateData()
        {
            if (Time.unscaledTime < nextDataAt) return;
            nextDataAt = Time.unscaledTime + 0.2f;
            string Ml(float? v) => v.HasValue ? LabProcedure.F(v.Value, "0.00") + " ml" : "—";
            var r = procedure.LastResult;
            dataLeft.text = $"V đầu\nV cuối\nV {procedure.Cfg.titrantShort}\nC {procedure.Cfg.analyteShort}";
            dataRight.text = $"{Ml(procedure.InitialRead)}\n{Ml(procedure.FinalRead)}\n" +
                             (r.HasValue ? Ml(r.Value.volumeRead) : "—") + "\n" +
                             (r.HasValue ? LabProcedure.F(r.Value.concentrationRead, "0.0000") + " M" : "—");
        }

        void SetRubricRow(int idx, string title, string detail, int earned, int max)
        {
            if (idx < 0 || idx >= RubricRowCount || rubricTitles[idx] == null) return;
            float ratio = max > 0 ? (float)earned / max : 1f;
            Color fg = ratio >= 0.8f ? Good : ratio >= 0.5f ? Warn : Bad;
            Color bg = ratio >= 0.8f ? GoodSoft : ratio >= 0.5f ? WarnSoft : BadSoft;
            rubricTitles[idx].text = title;
            rubricDetails[idx].text = detail;
            rubricScores[idx].text = $"{earned} / {max} đ";
            rubricScores[idx].color = fg;
            if (rubricPillBgs[idx] != null) rubricPillBgs[idx].color = bg;
            if (rubricStripes[idx] != null) rubricStripes[idx].color = fg;
        }

        void UpdateResult(LabProcedure.Result r)
        {
            if (Time.unscaledTime < nextResultAt) return;
            nextResultAt = Time.unscaledTime + 0.5f;
            resultShowingBio = false;
            if (resultGraphBox != null && !resultGraphBox.activeSelf) resultGraphBox.SetActive(true);
            if (resultBioBox != null && resultBioBox.activeSelf) resultBioBox.SetActive(false);

            resultTitle.text = $"Kết quả · C({procedure.Cfg.analyteShort})";
            resultBig.text = LabProcedure.F(r.concentrationRead, "0.0000") + " M";
            resultSub.text = $"Thật: {LabProcedure.F(r.concentrationTrue, "0.0000")} M   ·   V {procedure.Cfg.titrantShort} {LabProcedure.F(r.volumeRead, "0.00")} ml";
            float e = Mathf.Abs(r.errorPercent);
            resultBadge.text = $"Sai số {LabProcedure.F(r.errorPercent, "+0.0;-0.0;0.0")}%";
            resultBadgeBg.color = e <= 1f ? Good : e <= 3f ? Warn : Bad;

            // 1. Chuẩn bị & nạp dung dịch chuẩn (25 đ)
            bool mountedAndFilled = procedure != null ? (procedure.Done[0] && procedure.Done[1]) : (r.volumeRead > 0f);
            int prepPts = mountedAndFilled ? 8 : ((procedure != null && (procedure.Done[0] || procedure.Done[1])) ? 4 : 0);
            float nominal = procedure != null ? procedure.nominalAcidMl : 25f;
            float pipErr = Mathf.Abs(r.acidActualMl - nominal);
            int pipPts = r.acidActualMl <= 0.05f ? 0 : (pipErr < 0.05f ? 10 : pipErr < 0.15f ? 7 : pipErr < 0.40f ? 4 : 1);
            int indPts = r.noIndicator ? 0 : 7;
            int c1 = prepPts + pipPts + indPts;
            string d1 = r.noIndicator
                ? $"Thiếu {procedure.Cfg.indicatorName} · Pipet {LabProcedure.F(r.acidActualMl, "0.00")}/{LabProcedure.F(nominal, "0")} ml"
                : $"Buret sẵn sàng · Pipet {LabProcedure.F(r.acidActualMl, "0.00")}/{LabProcedure.F(nominal, "0")} ml · Có chỉ thị";
            SetRubricRow(0, "1. Chuẩn bị & nạp dung dịch chuẩn", d1, c1, 25);

            // 2. Kỹ thuật đọc vạch & thao tác buret (25 đ)
            float pMax = Mathf.Max(Mathf.Abs(r.parallaxInitial), Mathf.Abs(r.parallaxFinal));
            int eyePts = pMax < 0.03f ? 20 : pMax < 0.08f ? 16 : pMax < 0.15f ? 11 : pMax < 0.30f ? 6 : 2;
            bool readBoth = procedure != null ? (procedure.Done[2] && procedure.Done[6]) : (r.volumeRead > 0f);
            int readPts = readBoth && r.volumeRead > 0f ? 5 : 0;
            int c2 = eyePts + readPts;
            string d2 = $"Thị sai V đầu {LabProcedure.F(r.parallaxInitial, "+0.00;-0.00;0.00")} · V cuối {LabProcedure.F(r.parallaxFinal, "+0.00;-0.00;0.00")} ml";
            SetRubricRow(1, "2. Kỹ thuật đọc vạch & đuổi bọt khí", d2, c2, 25);

            // 3. Độ chính xác điểm tương đương & nồng độ (35 đ)
            int concPts = e <= 0.5f ? 25 : e <= 1.0f ? 22 : e <= 2.0f ? 18 : e <= 3.5f ? 13 : e <= 6.0f ? 8 : 3;
            float overAbs = Mathf.Abs(r.overshootDrops);
            int dropPts = overAbs <= 1.0f ? 10 : overAbs <= 2.5f ? 7 : overAbs <= 5.0f ? 4 : 1;
            int c3 = concPts + dropPts;
            string d3 = $"Sai số nồng độ {LabProcedure.F(r.errorPercent, "+0.0;-0.0;0.0")}% · Lệch {LabProcedure.F(r.overshootDrops, "+0.0;-0.0;0.0")} giọt";
            SetRubricRow(2, "3. Độ chính xác điểm tương đương & C", d3, c3, 35);

            // 4. An toàn & kỷ luật thao tác (15 đ)
            int c4 = 15;
            var safeIssues = new List<string>();
            if (r.contaminated) { c4 -= 6; safeIssues.Add("nhiễm chéo trước V đầu"); }
            if (r.notMixedWell) { c4 -= 5; safeIssues.Add("lắc bình chưa đều"); }
            if (procedure != null && !procedure.Done[5]) { c4 -= 4; safeIssues.Add("chưa giữ màu bền 30 s"); }
            c4 = Mathf.Max(0, c4);
            string d4 = safeIssues.Count == 0 ? "Không nhiễm chéo · Lắc đều · Giữ màu 30 s" : string.Join(", ", safeIssues);
            SetRubricRow(3, "4. An toàn & quy trình thao tác", d4, c4, 15);

            int total = Mathf.Clamp(c1 + c2 + c3 + c4, 0, 100);
            resultScorePill.text = $"{total} / 100 điểm";
            resultScorePill.color = total >= 85 ? Good : total >= 65 ? Warn : Bad;
            resultScorePillBg.color = total >= 85 ? GoodSoft : total >= 65 ? WarnSoft : BadSoft;

            var notes = new List<string>();
            if (r.noIndicator) notes.Add($"Quên {procedure.Cfg.indicatorName}: không thấy được điểm cuối");
            if (r.contaminated) notes.Add($"{procedure.Cfg.titrantShort} rơi vào bình nón trước khi đọc V đầu");
            if (pipErr >= 0.05f) notes.Add($"Pipet lấy {LabProcedure.F(r.acidActualMl, "0.00")} ml thay vì {LabProcedure.F(nominal, "0")} ml");
            if (r.overshootDrops >= 1.5f) notes.Add($"Nhỏ quá điểm tương đương khoảng {LabProcedure.F(r.overshootDrops, "0")} giọt");
            if (Mathf.Abs(r.parallaxInitial) >= 0.05f || Mathf.Abs(r.parallaxFinal) >= 0.05f)
                notes.Add($"Đọc lệch mắt: V đầu {LabProcedure.F(r.parallaxInitial, "+0.00;-0.00;0")} · V cuối {LabProcedure.F(r.parallaxFinal, "+0.00;-0.00;0")} ml");
            if (r.notMixedWell) notes.Add("Lắc bình chưa đều khi nhỏ");
            if (notes.Count == 0) notes.Add("Thao tác chuẩn: đúng vạch, đủ chỉ thị, dừng đúng giọt");
            resultNotes.text = "•  " + string.Join("\n•  ", notes.GetRange(0, Mathf.Min(3, notes.Count)));
        }

        void UpdateBioResult(Khkt.Demo.Bio.BioLabProcedure.BioResult r)
        {
            if (Time.unscaledTime < nextResultAt) return;
            nextResultAt = Time.unscaledTime + 0.5f;
            resultShowingBio = true;
            if (resultGraphBox != null && resultGraphBox.activeSelf) resultGraphBox.SetActive(false);
            if (resultBioBox != null && !resultBioBox.activeSelf) resultBioBox.SetActive(true);

            resultTitle.text = r.scenario == Khkt.Demo.Bio.BioLabProcedure.Scenario.OnionPlasmolysis
                ? "Kết quả · Co & Phản co nguyên sinh"
                : "Kết quả · Ghép ADN tái tổ hợp pUC19-GFP";
            resultBig.text = r.headline;
            resultSub.text = $"Hoàn thành {r.completedSteps}/7 bước · Lỗi kỹ thuật: {r.mistakeCount}";
            resultBadge.text = r.badgeText;
            resultBadgeBg.color = r.score >= 90 ? Good : r.score >= 70 ? Warn : Bad;

            int score = Mathf.Clamp(r.score, 0, 100);
            resultScorePill.text = $"{score} / 100 điểm";
            resultScorePill.color = score >= 85 ? Good : score >= 65 ? Warn : Bad;
            resultScorePillBg.color = score >= 85 ? GoodSoft : score >= 65 ? WarnSoft : BadSoft;

            if (resultBioHeadline != null)
            {
                resultBioHeadline.text = r.scenario == Khkt.Demo.Bio.BioLabProcedure.Scenario.OnionPlasmolysis
                    ? "Chỉ số quan sát & Co nguyên sinh"
                    : "Chỉ số tạo dòng ADN tái tổ hợp";
            }
            if (resultBioDetails != null) resultBioDetails.text = r.subline;

            int totalPenalty = 100 - score;
            int missingPrep = Mathf.Clamp(7 - r.completedSteps, 0, 6);
            int stepPenalty = Mathf.Min(totalPenalty, missingPrep * 10);
            int techPenalty = Mathf.Max(0, totalPenalty - stepPenalty);

            int p1 = Mathf.Min(40, stepPenalty);
            int c1 = 40 - p1;
            int rem1 = stepPenalty - p1;

            int p2 = Mathf.Min(25, techPenalty + rem1);
            int c2 = 25 - p2;
            int rem2 = (techPenalty + rem1) - p2;

            int p3 = Mathf.Min(20, rem2);
            int c3 = 20 - p3;
            int rem3 = rem2 - p3;

            int c4 = Mathf.Max(0, 15 - rem3);

            SetRubricRow(0, "1. Hoàn thành các bước quy trình", $"Thực hiện {r.completedSteps}/7 bước chuẩn", c1, 40);
            SetRubricRow(1, "2. Kỹ thuật thao tác & thiết bị", r.mistakeCount == 0 ? "Không ghi nhận lỗi kỹ thuật" : $"Phát hiện {r.mistakeCount} lỗi thao tác", c2, 25);
            string d3 = r.scenario == Khkt.Demo.Bio.BioLabProcedure.Scenario.OnionPlasmolysis
                ? $"Phóng đại {LabProcedure.F(r.maxMagnification, "0")}× · Co cực đại {LabProcedure.F(r.maxPlasmolysisPercent, "0.0")}%"
                : $"Plasmid {r.recombinantBp} bp · {r.whiteColonies} khuẩn lạc trắng GFP+";
            SetRubricRow(2, "3. Kết quả quan sát & sản phẩm", d3, c3, 20);
            SetRubricRow(3, "4. An toàn & kỷ luật phòng Sinh", r.mistakeCount == 0 && missingPrep == 0 ? "Đảm bảo vô trùng và an toàn dụng cụ" : "Xem nhận xét để khắc phục lỗi quy trình", c4, 15);

            if (r.notes != null && r.notes.Count > 0)
                resultNotes.text = "•  " + string.Join("\n•  ", r.notes.GetRange(0, Mathf.Min(3, r.notes.Count)));
            else
                resultNotes.text = "•  Hoàn thành bài thực hành Sinh học.";
        }

        public Texture RenderTitrationCurveTexture(int width = 760, int height = 160)
        {
            int w = Mathf.Max(64, width);
            int h = Mathf.Max(48, height);
            if (panelGraphTex != null && (panelGraphTex.width != w || panelGraphTex.height != h))
                DestroyTex(ref panelGraphTex);

            if (panelGraphTex == null)
            {
                panelGraphTex = new Texture2D(w, h, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.DontSave,
                };
                panelCurveVersion = -1;
            }

            int ver = procedure != null ? procedure.CurveVersion : 0;
            bool hasRes = procedure != null && procedure.LastResult.HasValue;
            var theme = UiTheme.Current;
            if (ver != panelCurveVersion || hasRes != panelCurveResult || theme != panelCurveTheme)
            {
                panelCurveVersion = ver;
                panelCurveResult = hasRes;
                panelCurveTheme = theme;
                DrawTitrationCurve(panelGraphTex);
            }
            return panelGraphTex;
        }

        void DrawGraph()
        {
            drawnCurve = procedure != null ? procedure.CurveVersion : 0;
            drawnResult = procedure != null && procedure.LastResult.HasValue;
            DrawTitrationCurve(graphTex);
        }

        void DrawTitrationCurve(Texture2D tex)
        {
            if (tex == null) return;
            int w = tex.width;
            int h = tex.height;
            // đồ thị vẽ lại sau mỗi giọt: dùng lại bộ đệm, không cấp phát mới
            var px = graphPx != null && graphPx.Length == w * h ? graphPx : (graphPx = new Color32[w * h]);

            Color baseBg = UiTheme.IsVera ? Well : Surface;
            baseBg.a = 1f;
            Color32 bg = baseBg;
            Color32 grid = Color.Lerp(baseBg, Ink, 0.14f);
            for (int i = 0; i < px.Length; i++) px[i] = bg;

            float maxV = 50f;
            if (procedure != null)
                foreach (var p in procedure.Curve) maxV = Mathf.Max(maxV, p.x + 2);
            for (int v = 0; v <= maxV; v += 10) VLine(px, w, h, CoordX(v, maxV, w), grid, false);
            HLine(px, w, h, CoordY(7, h), grid);

            if (procedure != null)
            {
                if (procedure.LastResult.HasValue)
                {
                    Color32 eqCol = Bad;
                    Color32 measCol = Accent2;
                    VLine(px, w, h, CoordX(procedure.EquivalenceMl, maxV, w), eqCol, true);
                    VLine(px, w, h, CoordX(procedure.LastResult.Value.volumeRead, maxV, w), measCol, true);
                }
                Color32 line = Accent;
                var c = procedure.Curve;
                for (int i = 1; i < c.Count; i++)
                    Segment(px, w, h, CoordX(c[i - 1].x, maxV, w), CoordY(c[i - 1].y, h), CoordX(c[i].x, maxV, w), CoordY(c[i].y, h), line);
            }

            tex.SetPixels32(px);
            tex.Apply();
        }

        static int CoordX(float v, float maxV, int w) => Mathf.Clamp(Mathf.RoundToInt(v / maxV * (w - 1)), 0, w - 1);
        static int CoordY(float ph, int h) => Mathf.Clamp(Mathf.RoundToInt(ph / 14f * (h - 1)), 0, h - 1);

        static void VLine(Color32[] px, int w, int h, int x, Color32 c, bool dashed)
        {
            for (int y = 0; y < h; y++)
                if (!dashed || (y / 5) % 2 == 0) px[y * w + x] = c;
        }

        static void HLine(Color32[] px, int w, int h, int y, Color32 c)
        {
            for (int x = 0; x < w; x++) px[y * w + x] = c;
        }

        static void Segment(Color32[] px, int w, int h, int x0, int y0, int x1, int y1, Color32 c)
        {
            int n = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0), 1);
            for (int i = 0; i <= n; i++)
            {
                int x = x0 + (x1 - x0) * i / n, y = y0 + (y1 - y0) * i / n;
                for (int d = -1; d <= 1; d++)
                    if (y + d >= 0 && y + d < h) px[(y + d) * w + x] = c;
            }
        }

        // =====================================================================
        // Hộp thoại cảnh báo an toàn (chặn thao tác nguy hiểm)
        // =====================================================================

        /// <summary>
        /// Hiện hộp thoại cảnh báo an toàn thí nghiệm dạng modal nổi trên cùng.
        /// Người dùng phải chọn "Đổi cách làm" (an toàn, hoặc nhấn Esc) hoặc "Vẫn làm" để tiếp tục.
        /// </summary>
        public static void ShowSafetyWarning(string title, string reason, System.Action onProceed, System.Action onChangeApproach = null)
        {
            safetyTitleStr = string.IsNullOrEmpty(title) ? "Cảnh báo an toàn thí nghiệm" : title;
            safetyReasonStr = string.IsNullOrEmpty(reason) ? "Thao tác này tiềm ẩn nguy cơ mất an toàn trong phòng thí nghiệm." : reason;
            safetyOnProceed = onProceed;
            safetyOnCancel = onChangeApproach;

            if (!safetyCaptured) UiBridge.SetCapture(ref safetyCaptured, true);
            BuildSafetyModal();
        }

        static void BuildSafetyModal()
        {
            DestroySafetyPanelObject();

            var ownerTr = Instance != null ? Instance.transform : null;
            var hostCanvas = HostCanvas(ownerTr, 90);
            if (hostCanvas == null) return;

            var go = new GameObject("Hộp thoại an toàn", typeof(RectTransform));
            safetyRoot = go.GetComponent<RectTransform>();
            safetyRoot.SetParent(hostCanvas.transform, false);
            safetyRoot.anchorMin = Vector2.zero;
            safetyRoot.anchorMax = Vector2.one;
            safetyRoot.offsetMin = safetyRoot.offsetMax = Vector2.zero;
            safetyRoot.SetAsLastSibling();
            safetyPanel = go;
            go.AddComponent<SafetyModalTick>();

            Backdrop(safetyRoot, "Nền mờ cảnh báo", TriggerSafetyCancel, 0.56f);

            var win = Window(safetyRoot, "Khung cảnh báo an toàn", new Vector2(620, 368), Vector2.zero);
            var winTr = win.transform;

            // Viền cảnh báo đỏ & thanh nhấn đỉnh
            var borderRt = new GameObject("Viền cảnh báo", typeof(RectTransform)).GetComponent<RectTransform>();
            borderRt.SetParent(winTr, false);
            borderRt.anchorMin = Vector2.zero;
            borderRt.anchorMax = Vector2.one;
            borderRt.offsetMin = borderRt.offsetMax = Vector2.zero;
            var borderImg = borderRt.gameObject.AddComponent<Image>();
            borderImg.sprite = Border;
            borderImg.type = Image.Type.Sliced;
            borderImg.pixelsPerUnitMultiplier = 1.15f;
            borderImg.color = new Color(Bad.r, Bad.g, Bad.b, UiTheme.IsLight ? 0.58f : 0.76f);
            borderImg.raycastTarget = false;

            Fill(winTr, "Vạch cảnh báo", new Vector2(24, -10), new Vector2(572, 4), Bad);

            // Huy hiệu biểu tượng + chip tiêu đề
            var iconBgRt = Rect(winTr, "Biểu tượng", new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -26), new Vector2(44, 44));
            var iconBg = iconBgRt.gameObject.AddComponent<Image>();
            iconBg.sprite = Solid;
            iconBg.type = Image.Type.Sliced;
            iconBg.pixelsPerUnitMultiplier = 2.2f;
            iconBg.color = Bad;
            iconBg.raycastTarget = false;
            Label(iconBgRt, "Dấu chấm than", "!", 24, OnAccent, true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(44, 44));

            var chipRt = Rect(winTr, "Nhãn nguy hiểm", new Vector2(0, 1), new Vector2(0, 1), new Vector2(84, -26), new Vector2(182, 26));
            var chipImg = chipRt.gameObject.AddComponent<Image>();
            chipImg.sprite = Solid;
            chipImg.type = Image.Type.Sliced;
            chipImg.pixelsPerUnitMultiplier = 2.6f;
            chipImg.color = BadSoft;
            chipImg.raycastTarget = false;
            Label(chipRt, "Chữ", "CẢNH BÁO AN TOÀN", 12, Bad, true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(182, 26));

            var titleTxt = Label(winTr, "Tiêu đề", safetyTitleStr, 21, Ink, true, TextAnchor.UpperLeft, new Vector2(84, -56), new Vector2(508, 52));
            titleTxt.verticalOverflow = VerticalWrapMode.Truncate;

            var bodyBg = MakeResultBox(winTr, "Nội dung cảnh báo", new Vector2(28, -118), new Vector2(564, 152),
                out _, BadSoft, new Color(Bad.r, Bad.g, Bad.b, UiTheme.IsLight ? 0.28f : 0.42f));
            var reasonTxt = Label(bodyBg.transform, "Lý do", safetyReasonStr, 15, Ink, false, TextAnchor.UpperLeft, new Vector2(16, -14), new Vector2(532, 102));
            reasonTxt.verticalOverflow = VerticalWrapMode.Truncate;
            Label(bodyBg.transform, "Khuyến nghị", "Khuyến nghị: chọn \"Đổi cách làm\" (hoặc Esc) để chỉnh lại thao tác an toàn.",
                13, Warn, true, TextAnchor.LowerLeft, new Vector2(16, -120), new Vector2(532, 22));

            // Hàng nút đáy (>= 44 pt)
            var proceedBtn = MakeButton(winTr, "Nút vẫn làm", "Vẫn làm", new Vector2(28, -292), new Vector2(216, 48), TriggerSafetyProceed, false, 16);
            if (proceedBtn.targetGraphic is Image pBg) pBg.color = BadSoft;
            var pBorder = proceedBtn.transform.Find("Viền")?.GetComponent<Image>();
            if (pBorder != null) pBorder.color = new Color(Bad.r, Bad.g, Bad.b, 0.78f);
            var pTxt = proceedBtn.transform.Find("Chữ")?.GetComponent<Text>();
            if (pTxt != null) { pTxt.color = Bad; pTxt.font = Semibold; }

            MakeButton(winTr, "Nút đổi cách làm", "Đổi cách làm", new Vector2(260, -292), new Vector2(332, 48), TriggerSafetyCancel, true, 16);

            SetLayerIfNeeded(safetyPanel.transform, hostCanvas.gameObject.layer);
            if (Instance != null && win != null)
                UiMotion.Popup(Instance, win.rectTransform, null, 0.20f);
        }

        static void RebuildSafetyModal()
        {
            if (IsSafetyModalOpen) BuildSafetyModal();
        }

        static void TriggerSafetyProceed()
        {
            var cb = safetyOnProceed;
            CloseSafetyModal(invokeCancel: false);
            cb?.Invoke();
        }

        static void TriggerSafetyCancel()
        {
            var cb = safetyOnCancel;
            CloseSafetyModal(invokeCancel: false);
            cb?.Invoke();
        }

        static void CloseSafetyModal(bool invokeCancel)
        {
            var cancelCb = invokeCancel ? safetyOnCancel : null;
            safetyOnProceed = null;
            safetyOnCancel = null;
            if (safetyCaptured) UiBridge.SetCapture(ref safetyCaptured, false);
            DestroySafetyPanelObject();
            cancelCb?.Invoke();
        }

        static void DestroySafetyPanelObject()
        {
            if (safetyPanel == null) return;
            var old = safetyPanel;
            safetyPanel = null;
            safetyRoot = null;
            old.SetActive(false);
            if (Application.isPlaying) Destroy(old);
            else DestroyImmediate(old);
        }

        sealed class SafetyModalTick : MonoBehaviour
        {
            void OnEnable() => UiTheme.Changed += OnThemeChanged;
            void OnDisable() => UiTheme.Changed -= OnThemeChanged;
            void OnThemeChanged() => RebuildSafetyModal();

            void Update()
            {
                if (!IsSafetyModalOpen) return;
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null && kb.escapeKey.wasPressedThisFrame) TriggerSafetyCancel();
            }

            void OnDestroy()
            {
                if (safetyPanel == gameObject)
                {
                    safetyPanel = null;
                    safetyRoot = null;
                    safetyOnProceed = null;
                    safetyOnCancel = null;
                    if (safetyCaptured) UiBridge.SetCapture(ref safetyCaptured, false);
                }
            }
        }
    }
}
