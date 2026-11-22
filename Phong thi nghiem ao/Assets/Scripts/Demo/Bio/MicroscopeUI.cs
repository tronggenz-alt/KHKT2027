using System;
using System.Collections.Generic;
using Khkt.Input;
using Khkt.Interaction.Bio;
using Khkt.Simulation.Bio;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static Khkt.Demo.GlassKit;

namespace Khkt.Demo.Bio
{
    /// <summary>Một mục quan sát hiển vi (ảnh chụp + thông số tiêu bản) được lưu vào báo cáo thực hành.</summary>
    [Serializable]
    public class MicroscopeReportEntry
    {
        public string id;
        public string slideCode;
        public string specimen;
        public string magnification;
        public string objective;
        public string focusQuality;
        public int sharpPercent;
        public string stain;
        public string notes;
        public string fileName;
        public string timestamp;
        public double measuredCellUm;
        [NonSerialized] public Texture2D preview;
    }

    /// <summary>
    /// Kho báo cáo hiển vi cấp phiên làm việc + tóm tắt JSON lưu PlayerPrefs để xuất hoặc xem lại.
    /// </summary>
    public static class MicroscopeReportStore
    {
        public const string PrefsKey = "KHKT_MICROSCOPE_REPORT_V1";
        const int MaxEntries = 24;

        [Serializable]
        class Payload
        {
            public List<MicroscopeReportEntry> items = new List<MicroscopeReportEntry>();
        }

        static readonly List<MicroscopeReportEntry> entries = new List<MicroscopeReportEntry>();
        public static IReadOnlyList<MicroscopeReportEntry> Entries => entries;
        public static int Count => entries.Count;
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            entries.Clear();
            Changed = null;
        }

        public static bool ContainsId(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].id == id) return true;
            return false;
        }

        public static bool Add(MicroscopeReportEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.specimen)) return false;
            if (!string.IsNullOrEmpty(entry.id) && ContainsId(entry.id)) return false;
            if (string.IsNullOrEmpty(entry.id)) entry.id = Guid.NewGuid().ToString("N");
            entries.Add(entry);
            while (entries.Count > MaxEntries)
                entries.RemoveAt(0);
            SaveToPrefs();
            Changed?.Invoke();
            return true;
        }

        public static bool Remove(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].id == id)
                {
                    entries.RemoveAt(i);
                    SaveToPrefs();
                    Changed?.Invoke();
                    return true;
                }
            }
            return false;
        }

        public static void ClearPreviewRefs(Texture2D tex)
        {
            if (tex == null) return;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].preview == tex) entries[i].preview = null;
        }

        public static void Clear()
        {
            entries.Clear();
            SaveToPrefs();
            Changed?.Invoke();
        }

        public static string ExportSummaryText()
        {
            if (entries.Count == 0) return "Chưa có mục quan sát kính hiển vi nào trong báo cáo.";
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"BÁO CÁO QUAN SÁT KÍNH HIỂN VI ({entries.Count} mục)");
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                sb.AppendLine($"{i + 1}. {e.specimen} ({e.magnification}, {e.objective}) · Nhuộm: {e.stain} · {e.focusQuality} ({e.sharpPercent}%) · TB ≈ {BioFeedback.FormatVi(e.measuredCellUm, "0")} µm · Lúc {e.timestamp}");
                if (!string.IsNullOrEmpty(e.notes)) sb.AppendLine("   Ghi chú: " + e.notes);
            }
            return sb.ToString().TrimEnd();
        }

        static void SaveToPrefs()
        {
            try
            {
                var payload = new Payload { items = new List<MicroscopeReportEntry>(entries) };
                PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(payload));
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MicroscopeReportStore] Không lưu được báo cáo vào PlayerPrefs: " + e.Message);
            }
        }
    }

    /// <summary>
    /// Màn hình soi kính hiển vi (Spec 4.5): bố cục kính rộng 66% / 34%, thị trường tròn lớn trên nền đen quang học #000000,
    /// 4 nút nổi ở 4 góc thị trường (Kéo · Thước · Chú thích · Chụp) và cụm điều khiển cân đối bên phải.
    /// Mọi quy tắc quang học & an toàn (100× thiếu dầu, 40× dính dầu, co nguyên sinh, lamen) vẫn do MicroscopeController xử lý.
    /// </summary>
    public class MicroscopeUI : MonoBehaviour
    {
        public static MicroscopeUI Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance.open;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
        }

        const float PanelW = 1660f, PanelH = 940f;
        const float HeaderH = 62f, FooterH = 68f;
        const float BodyH = PanelH - HeaderH - FooterH;
        const float LeftW = 1096f;
        const float RightW = PanelW - LeftW - 1f;
        const float StageW = 1052f, StageH = 786f;
        const float ViewSize = 776f;
        const float FocusRangeMm = 0.60f;

        static readonly string[] ObjectiveLabels = { "4×", "10×", "40×", "100×" };
        static readonly string[] ObjectiveFullNames = { "4×", "10×", "40×", "100× dầu" };
        static readonly string[] StainLabels = { "Không", "Iốt", "Metylen" };

        static readonly Color CornerPillBg = new Color(0.08f, 0.09f, 0.12f, 0.82f);
        static readonly Color CornerPillActiveBg = new Color(0.11f, 0.14f, 0.20f, 0.92f);

        class CapturedShot
        {
            public string id;
            public string fileName;
            public string slideCode;
            public string slideTitle;
            public string objectiveLabel;
            public string magLabel;
            public string stainLabel;
            public string focusLabel;
            public int sharpPercent;
            public double measuredCellUm;
            public string notes;
            public string timestamp;
            public Texture2D preview;
            public bool addedToReport;
        }

        bool open, captured;
        bool panEnabled = true;
        UiTheme.Mode builtTheme;
        int builtFontScale;
        MicroscopeController mic;
        GameObject root;
        RectTransform animFrame, panel, galleryPanel;
        CanvasGroup group;
        RawImage view;

        Text headerTitle, headerMagText, headerStateText;
        Image headerMagBg, headerMagBorder, headerStateBg, headerStateBorder;
        Text focusState, focusValueText, lightValueText;
        Text fovText, cellText, stageText;
        Image hintBanner, hintBorder;
        Text hintText, galleryBtnText, addReportBtnText;

        Slider focusSlider, lightSlider;
        Button[] objectiveChips, stainChips;
        Button plasmoChip, oilChip;
        Button cornerPanBtn, cornerRulerBtn, cornerNoteBtn, cornerSnapBtn;

        GameObject galleryOverlay, galleryEmptyLeft, galleryEmptyRight, galleryFrameBox, galleryMetaBox;
        RectTransform galleryGridContent;
        RawImage galleryImage;
        Text galleryTitleText, galleryReportBadgeText, galleryOverlayTagText, galleryZoomBtnText;
        Text gallerySpecimenText, gallerySpecsText, galleryMeasureText, galleryNotesText;
        Text galleryReportStatusText, galleryAddReportBtnText;
        Image galleryReportBadgeBg, galleryReportBadgeBorder, galleryReportStatusBg, galleryReportStatusBorder;
        Button galleryPrevBtn, galleryNextBtn, galleryZoomBtn, galleryDeleteBtn, galleryAddReportBtn;
        int galleryIndex;
        bool galleryZoom2x;
        Vector2 galleryZoomCenter = new Vector2(0.5f, 0.5f);
        readonly List<Button> galleryCards = new List<Button>();

        readonly List<(Button b, string id)> slideRows = new List<(Button, string)>();
        readonly List<Text> noteLabels = new List<Text>();
        readonly List<Text> rulerLabels = new List<Text>();
        readonly List<CapturedShot> capturedShots = new List<CapturedShot>();

        string shownSlide = "";
        int shownObjective = -1;
        float nextInfo;

        public static MicroscopeUI Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("Màn hình kính hiển vi");
            Instance = go.AddComponent<MicroscopeUI>();
            return Instance;
        }

        public static void Open(MicroscopeController microscope)
        {
            if (microscope == null) return;
            Ensure().Show(microscope);
        }

        void Awake()
        {
            UiTheme.Changed += OnThemeChanged;
            MicroscopeReportStore.Changed += OnReportStoreChanged;
        }

        void OnDestroy()
        {
            UiTheme.Changed -= OnThemeChanged;
            MicroscopeReportStore.Changed -= OnReportStoreChanged;
            if (captured) UiBridge.SetCapture(ref captured, false);
            for (int i = 0; i < capturedShots.Count; i++)
            {
                if (capturedShots[i].preview != null)
                {
                    MicroscopeReportStore.ClearPreviewRefs(capturedShots[i].preview);
                    Destroy(capturedShots[i].preview);
                }
            }
            capturedShots.Clear();
            if (Instance == this) Instance = null;
        }

        void OnReportStoreChanged()
        {
            for (int i = 0; i < capturedShots.Count; i++)
                capturedShots[i].addedToReport = MicroscopeReportStore.ContainsId(capturedShots[i].id);
            UpdateReportButtonState();
            if (galleryOverlay != null && galleryOverlay.activeSelf) RenderGalleryItem();
        }

        void OnThemeChanged()
        {
            if (open && mic != null)
            {
                bool wasGallery = galleryOverlay != null && galleryOverlay.activeSelf;
                int savedGalleryIndex = galleryIndex;
                DestroyUi();
                Build();
                ApplyLiveStateToControls();
                root.SetActive(true);
                root.transform.SetAsLastSibling();
                RefreshAll();
                if (wasGallery)
                {
                    galleryIndex = savedGalleryIndex;
                    OpenGallery();
                }
            }
            else if (root != null)
            {
                DestroyUi();
            }
        }

        void DestroyUi()
        {
            slideRows.Clear();
            noteLabels.Clear();
            rulerLabels.Clear();
            galleryCards.Clear();
            galleryOverlay = null;
            galleryEmptyLeft = null;
            galleryEmptyRight = null;
            galleryFrameBox = null;
            galleryMetaBox = null;
            galleryGridContent = null;
            galleryImage = null;
            galleryTitleText = null;
            galleryReportBadgeText = null;
            galleryOverlayTagText = null;
            galleryZoomBtnText = null;
            gallerySpecimenText = null;
            gallerySpecsText = null;
            galleryMeasureText = null;
            galleryNotesText = null;
            galleryReportStatusText = null;
            galleryAddReportBtnText = null;
            galleryReportBadgeBg = null;
            galleryReportBadgeBorder = null;
            galleryReportStatusBg = null;
            galleryReportStatusBorder = null;
            galleryPrevBtn = null;
            galleryNextBtn = null;
            galleryZoomBtn = null;
            galleryDeleteBtn = null;
            galleryAddReportBtn = null;
            galleryBtnText = null;
            addReportBtnText = null;
            if (root != null) Destroy(root);
            root = null;
            animFrame = null;
            panel = null;
            galleryPanel = null;
            group = null;
        }

        // ------------------------------------------------------------------
        // Dựng giao diện
        // ------------------------------------------------------------------

        void Build()
        {
            builtTheme = UiTheme.Current;
            builtFontScale = UiTheme.FontScaleMode;
            var canvas = HostCanvas(transform, 46);
            root = new GameObject("Kính hiển vi", typeof(RectTransform));
            var rrt = (RectTransform)root.transform;
            rrt.SetParent(canvas.transform, false);
            rrt.anchorMin = Vector2.zero;
            rrt.anchorMax = Vector2.one;
            rrt.offsetMin = rrt.offsetMax = Vector2.zero;
            group = root.AddComponent<CanvasGroup>();
            Backdrop(root.transform, "Nền mờ", Close, 0.62f);

            animFrame = Rect(root.transform, "Khung hiệu ứng", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PanelW, PanelH));
            var winImg = Window(animFrame, "Khung kính hiển vi", new Vector2(PanelW, PanelH), Vector2.zero);
            panel = (RectTransform)winImg.transform;
            ApplyWindowScale();
            var c = panel;

            BuildHeader(c);
            BuildLeftOpticalStage(c);
            Fill(c, "Vạch chia cột", new Vector2(LeftW, -HeaderH), new Vector2(1f, BodyH), Line);
            BuildRightControls(c);
            BuildFooter(c);

            root.SetActive(false);
        }

        void BuildHeader(RectTransform c)
        {
            // Biểu tượng thấu kính hình học bên trái thanh đầu
            var iconWrap = Rect(c, "Biểu tượng kính", new Vector2(0, 1), new Vector2(0, 1), new Vector2(24f, -14f), new Vector2(34f, 34f));
            var iconBg = iconWrap.gameObject.AddComponent<Image>();
            iconBg.sprite = SolidCircle;
            iconBg.color = AccentSoft;
            iconBg.raycastTarget = false;

            var iconRingRt = Rect(iconWrap, "Vòng thấu kính", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20f, 20f));
            var iconRing = iconRingRt.gameObject.AddComponent<Image>();
            iconRing.sprite = Circle;
            iconRing.color = Accent;
            iconRing.raycastTarget = false;

            var iconDotRt = Rect(iconWrap, "Tâm thấu kính", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(7f, 7f));
            var iconDot = iconDotRt.gameObject.AddComponent<Image>();
            iconDot.sprite = SolidCircle;
            iconDot.color = Accent;
            iconDot.raycastTarget = false;

            headerTitle = Label(c, "Tiêu đề kính", "Kính hiển vi", 20, Ink, true, TextAnchor.MiddleLeft,
                new Vector2(68f, 0f), new Vector2(PanelW - 500f, HeaderH));
            headerTitle.horizontalOverflow = HorizontalWrapMode.Wrap;
            headerTitle.verticalOverflow = VerticalWrapMode.Truncate;

            // Thẻ trạng thái nét / an toàn dầu
            headerStateBg = MakeHeaderBadge(c, "Thẻ trạng thái", new Vector2(-416f, -12f), new Vector2(154f, 38f),
                out headerStateBorder, out headerStateText, Good, GoodSoft, 13);

            // Thẻ độ phóng đại (ví dụ: 400×)
            headerMagBg = MakeHeaderBadge(c, "Thẻ phóng đại", new Vector2(-252f, -12f), new Vector2(102f, 38f),
                out headerMagBorder, out headerMagText, Accent, AccentSoft, 16);

            MakeButton(c, "Đóng", PadInput.UsingTouch ? "Đóng ×" : "Đóng · Esc",
                new Vector2(-138f, -9f), new Vector2(114f, 44f), Close, false, 14, new Vector2(1, 1));

            Rule(c, new Vector2(0f, -HeaderH), PanelW);
        }

        static Image MakeHeaderBadge(RectTransform parent, string name, Vector2 pos, Vector2 size,
            out Image borderImg, out Text label, Color fg, Color bg, int fontSize)
        {
            var rt = Rect(parent, name, new Vector2(1, 1), new Vector2(0, 1), pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Solid;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = UiTheme.IsVera ? 2.2f : 3f;
            img.color = bg;
            img.raycastTarget = false;

            var brt = Rect(rt, "Viền", new Vector2(0, 0), Vector2.zero, Vector2.zero, Vector2.zero);
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = brt.offsetMax = Vector2.zero;
            borderImg = brt.gameObject.AddComponent<Image>();
            borderImg.sprite = Border;
            borderImg.type = Image.Type.Sliced;
            borderImg.pixelsPerUnitMultiplier = img.pixelsPerUnitMultiplier;
            borderImg.color = new Color(fg.r, fg.g, fg.b, 0.35f);
            borderImg.raycastTarget = false;

            label = Label(rt, "Chữ", "", fontSize, fg, true, TextAnchor.MiddleCenter, Vector2.zero, size);
            var lrt = label.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(8f, 0f);
            lrt.offsetMax = new Vector2(-8f, 0f);
            return img;
        }

        void BuildLeftOpticalStage(RectTransform c)
        {
            var leftCol = Rect(c, "Cột thị trường", new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(0f, -HeaderH), new Vector2(LeftW, BodyH));
            leftCol.gameObject.AddComponent<RectMask2D>();

            float stageX = (LeftW - StageW) * 0.5f;
            float stageY = (BodyH - StageH) * 0.5f;
            var stage = Rect(leftCol, "Sân khấu quang học", new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(stageX, -stageY), new Vector2(StageW, StageH));
            stage.gameObject.AddComponent<RectMask2D>();

            var stageBg = stage.gameObject.AddComponent<Image>();
            stageBg.color = Color.black; // #000000 cố định trên cả 3 giao diện
            stageBg.raycastTarget = true;

            var pad = stage.gameObject.AddComponent<UiDragPad>();
            pad.onDrag = OnViewDrag;
            pad.onScroll = OnViewScroll;

            // Khung thị trường tròn căn giữa tuyệt đối bên trong sân khấu đen
            var vrt = Rect(stage, "Thị trường", new Vector2(0.5f, 0.5f), new Vector2(0f, 1f),
                new Vector2(-ViewSize * 0.5f, ViewSize * 0.5f), new Vector2(ViewSize, ViewSize));
            view = vrt.gameObject.AddComponent<RawImage>();
            view.color = Color.white;
            view.raycastTarget = false;

            for (int i = 0; i < 6; i++)
            {
                var rl = Label(vrt, "Số thước", (i * 20).ToString(), 12, new Color(0.10f, 0.10f, 0.12f, 0.92f),
                    true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(36, 16));
                float half = ViewSize * 0.465f * 0.72f;
                rl.rectTransform.anchoredPosition = new Vector2(ViewSize * 0.5f - half + i / 5f * 2f * half - 18f, -ViewSize * 0.5f + 22f);
                var sh = rl.gameObject.AddComponent<Shadow>();
                sh.effectColor = new Color(1f, 1f, 1f, 0.55f);
                sh.effectDistance = new Vector2(0.8f, -0.8f);
                rulerLabels.Add(rl);
            }

            // 4 nút tối bo góc ở 4 góc nền đen sân khấu (cao 44 pt)
            const float edge = 14f, btnH = 44f;
            cornerPanBtn = MakeCornerPill(stage, "Nút góc kéo", "Kéo",
                new Vector2(edge, -edge), new Vector2(108f, btnH), TogglePanMode, new Vector2(0, 1));
            cornerRulerBtn = MakeCornerPill(stage, "Nút góc thước", "Thước",
                new Vector2(-edge - 116f, -edge), new Vector2(116f, btnH), ToggleRuler, new Vector2(1, 1));
            cornerNoteBtn = MakeCornerPill(stage, "Nút góc chú thích", "Chú thích",
                new Vector2(edge, edge + btnH), new Vector2(132f, btnH), ToggleNotes, new Vector2(0, 0));
            cornerSnapBtn = MakeCornerPill(stage, "Nút góc chụp", "Chụp",
                new Vector2(-edge - 108f, edge + btnH), new Vector2(108f, btnH), Capture, new Vector2(1, 0));
        }

        static Button MakeCornerPill(RectTransform parent, string name, string text, Vector2 pos, Vector2 size,
            System.Action onClick, Vector2 anchor)
        {
            var b = MakeButton(parent, name, text, pos, size, onClick, false, 14, anchor);
            var cols = b.colors;
            cols.normalColor = Color.white;
            cols.highlightedColor = new Color(1f, 1f, 1f, 0.96f);
            cols.selectedColor = Color.white;
            cols.pressedColor = new Color(0.78f, 0.80f, 0.85f, 1f);
            b.colors = cols;
            StyleCornerPill(b, false);
            return b;
        }

        static void StyleCornerPill(Button b, bool active)
        {
            if (b == null) return;
            if (b.TryGetComponent<Image>(out var img))
                img.color = active ? Color.Lerp(CornerPillActiveBg, Accent, 0.30f) : CornerPillBg;
            var borderTr = b.transform.Find("Viền");
            if (borderTr != null && borderTr.TryGetComponent<Image>(out var borderImg))
                borderImg.color = active ? Accent : new Color(1f, 1f, 1f, 0.24f);
            var lbl = b.GetComponentInChildren<Text>();
            if (lbl != null) lbl.color = Color.white;
        }

        void BuildRightControls(RectTransform c)
        {
            var rightCol = Rect(c, "Cột điều khiển", new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(LeftW + 1f, -HeaderH), new Vector2(RightW, BodyH));
            rightCol.gameObject.AddComponent<RectMask2D>();

            const float px = 20f;
            float rw = RightW - px * 2f;
            float y = -14f;

            // 1. Vật kính (4 chip)
            Label(rightCol, "Nhãn vật kính", "VẬT KÍNH", 12, Muted, true, TextAnchor.UpperLeft, new Vector2(px, y), new Vector2(160f, 18f));
            y -= 22f;
            objectiveChips = new Button[4];
            float ow = (rw - 3f * 8f) / 4f;
            for (int i = 0; i < 4; i++)
            {
                int k = i;
                objectiveChips[i] = MakeChip(rightCol, "Vật kính " + ObjectiveLabels[i], ObjectiveLabels[i],
                    new Vector2(px + i * (ow + 8f), y), new Vector2(ow, 44f), () => PickObjective(k), false, 15);
            }
            y -= 58f;

            // 2. Lấy nét (thanh trượt + đọc trị + Tự lấy nét)
            Label(rightCol, "Nhãn lấy nét", "LẤY NÉT", 12, Muted, true, TextAnchor.UpperLeft, new Vector2(px, y), new Vector2(120f, 18f));
            focusValueText = Label(rightCol, "Giá trị lấy nét", "0%", 14, Accent2, true, TextAnchor.UpperRight, new Vector2(px + 120f, y), new Vector2(150f, 18f));
            focusState = Label(rightCol, "Độ nét", "", 13, Accent2, true, TextAnchor.UpperRight, new Vector2(px + rw - 134f, y), new Vector2(134f, 18f));
            y -= 22f;
            float autoW = 132f;
            focusSlider = MakeSlider(rightCol, "Thanh lấy nét", new Vector2(px, y), new Vector2(rw - autoW - 10f, 44f),
                -FocusRangeMm, FocusRangeMm, 0f, OnFocus);
            var autoBtn = MakeButton(rightCol, "Tự lấy nét", "Tự lấy nét", new Vector2(px + rw - autoW, y), new Vector2(autoW, 44f),
                AutoFocus, false, 14);
            var autoTxt = autoBtn.GetComponentInChildren<Text>();
            if (autoTxt != null) autoTxt.color = Accent;
            y -= 58f;

            // 3. Độ sáng (0..100%)
            Label(rightCol, "Nhãn đèn", "ĐỘ SÁNG ĐÈN", 12, Muted, true, TextAnchor.UpperLeft, new Vector2(px, y), new Vector2(180f, 18f));
            lightValueText = Label(rightCol, "Giá trị sáng", "85%", 14, Accent2, true, TextAnchor.UpperRight, new Vector2(px + rw - 120f, y), new Vector2(120f, 18f));
            y -= 22f;
            lightSlider = MakeSlider(rightCol, "Thanh độ sáng", new Vector2(px, y), new Vector2(rw, 44f), 0f, 1f, 0.85f, OnLight);
            y -= 58f;

            // 4. Nhuộm (Không, Iốt, Metylen)
            Label(rightCol, "Nhãn nhuộm", "NHUỘM", 12, Muted, true, TextAnchor.UpperLeft, new Vector2(px, y), new Vector2(160f, 18f));
            y -= 22f;
            stainChips = new Button[3];
            float sw = (rw - 2f * 8f) / 3f;
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                stainChips[i] = MakeChip(rightCol, "Nhuộm " + StainLabels[i], StainLabels[i],
                    new Vector2(px + i * (sw + 8f), y), new Vector2(sw, 44f), () => PickStain(k), false, 14);
            }
            y -= 58f;

            // 5. Thí nghiệm (NaCl 5%, Dầu soi)
            Label(rightCol, "Nhãn thí nghiệm", "THÍ NGHIỆM", 12, Muted, true, TextAnchor.UpperLeft, new Vector2(px, y), new Vector2(200f, 18f));
            y -= 22f;
            float hw = (rw - 8f) * 0.5f;
            plasmoChip = MakeChip(rightCol, "NaCl 5%", "Nhỏ NaCl 5%", new Vector2(px, y), new Vector2(hw, 44f), TogglePlasmolysis, false, 14);
            oilChip = MakeChip(rightCol, "Dầu soi", "Nhỏ dầu soi", new Vector2(px + hw + 8f, y), new Vector2(hw, 44f), ToggleOil, false, 14);
            y -= 58f;

            // 6. Tiêu bản (lưới 3 cột gọn gàng)
            Label(rightCol, "Nhãn tiêu bản", "TIÊU BẢN", 12, Muted, true, TextAnchor.UpperLeft, new Vector2(px, y), new Vector2(180f, 18f));
            y -= 22f;
            const int cols = 3;
            const float chipGapX = 6f, chipGapY = 6f, slideChipH = 44f;
            float slideChipW = (rw - (cols - 1) * chipGapX) / cols;
            int idx = 0;
            foreach (var (id, fullName) in MicroscopeController.StandardSlides)
            {
                string slideId = id;
                int row = idx / cols;
                int col = idx % cols;
                float cx = px + col * (slideChipW + chipGapX);
                float cy = y - row * (slideChipH + chipGapY);
                string shortLabel = ShortSlideChipName(slideId, fullName);
                var b = MakeChip(rightCol, "Tiêu bản " + slideId, shortLabel,
                    new Vector2(cx, cy), new Vector2(slideChipW, slideChipH), () => PickSlide(slideId), false, 13);
                var t = b.GetComponentInChildren<Text>();
                if (t != null)
                {
                    t.horizontalOverflow = HorizontalWrapMode.Wrap;
                    t.verticalOverflow = VerticalWrapMode.Truncate;
                }
                slideRows.Add((b, slideId));
                idx++;
            }
            int rows = (idx + cols - 1) / cols;
            y -= rows * slideChipH + Mathf.Max(0, rows - 1) * chipGapY + 14f;

            // 7. Hàng 3 ô số đo (Thị trường, Kích thước tế bào, 1 vạch thước)
            Label(rightCol, "Nhãn số đo", "SỐ ĐO", 12, Muted, true, TextAnchor.UpperLeft, new Vector2(px, y), new Vector2(180f, 18f));
            y -= 22f;
            float cw = (rw - 2f * 8f) / 3f;
            fovText = Readout(rightCol, "Ô thị trường", "THỊ TRƯỜNG", "", new Vector2(px, y), new Vector2(cw, 62f));
            cellText = Readout(rightCol, "Ô tế bào", "TẾ BÀO", "", new Vector2(px + cw + 8f, y), new Vector2(cw, 62f));
            stageText = Readout(rightCol, "Ô thước", "1 VẠCH", "", new Vector2(px + 2f * (cw + 8f), y), new Vector2(cw, 62f));
            int readoutFs = UiTheme.FontScaleMode > 0 ? Mathf.Clamp(Mathf.RoundToInt(16f * UiTheme.FontScale), 16, 21) : 16;
            fovText.fontSize = cellText.fontSize = stageText.fontSize = readoutFs;
            foreach (var rtVal in new[] { fovText, cellText, stageText })
            {
                var sd = rtVal.rectTransform.sizeDelta;
                if (sd.y < 26f) rtVal.rectTransform.sizeDelta = new Vector2(sd.x, 26f);
                rtVal.horizontalOverflow = HorizontalWrapMode.Wrap;
                rtVal.verticalOverflow = VerticalWrapMode.Truncate;
            }
            y -= 74f;

            // 8. Dải nhắc 1-2 dòng via GlassKit.Banner
            float bannerH = Mathf.Max(48f, BodyH + y - 12f);
            hintBanner = Banner(rightCol, "Dải nhắc", "Đổi vật kính, lấy nét, nhuộm hoặc nhỏ NaCl để xem thử.",
                Warn, WarnSoft, new Vector2(px, y), new Vector2(rw, bannerH));
            hintBorder = hintBanner.transform.Find("Viền")?.GetComponent<Image>();
            hintText = hintBanner.transform.Find("Nội dung")?.GetComponent<Text>();
            if (hintText != null)
                hintText.fontSize = UiTheme.FontScaleMode > 0 ? Mathf.Clamp(Mathf.RoundToInt(13f * UiTheme.FontScale), 13, 18) : 13;
        }

        void BuildFooter(RectTransform c)
        {
            float fy = -PanelH + FooterH;
            Rule(c, new Vector2(0f, fy), PanelW);
            float by = fy - (FooterH - 46f) * 0.5f;

            // Trái: Chiếu tường video, Đo kích thước, Hỏi trợ lý
            MakeButton(c, "Chiếu tường video", "Chiếu lên tường video", new Vector2(24f, by), new Vector2(218f, 46f),
                StreamToWall, false, 14);
            MakeButton(c, "Đo kích thước", "Đo kích thước", new Vector2(250f, by), new Vector2(146f, 46f),
                MeasureCell, false, 14);
            MakeButton(c, "Hỏi trợ lý", "Hỏi trợ lý", new Vector2(404f, by), new Vector2(132f, 46f),
                AskAssistant, false, 14);

            if (!PadInput.UsingTouch)
            {
                Label(c, "Phím tắt", "Kéo: dịch · Cuộn: nét · 1–4: vật kính", 13, Muted, false,
                    TextAnchor.MiddleCenter, new Vector2(544f, fy - 18f), new Vector2(PanelW - 544f - 644f, 32f));
            }

            // Phải: Thêm vào báo cáo, Ảnh đã chụp (N), Chụp ảnh (1 nút chính duy nhất)
            var reportBtn = MakeButton(c, "Thêm vào báo cáo", "Thêm vào báo cáo", new Vector2(-634f, by), new Vector2(234f, 46f),
                AddLiveOrLatestToReport, false, 14, new Vector2(1, 1));
            addReportBtnText = reportBtn.GetComponentInChildren<Text>();

            var galBtn = MakeButton(c, "Ảnh đã chụp", "Ảnh đã chụp (0)", new Vector2(-388f, by), new Vector2(194f, 46f),
                OpenGallery, false, 14, new Vector2(1, 1));
            galleryBtnText = galBtn.GetComponentInChildren<Text>();

            MakeButton(c, "Chụp ảnh", "Chụp ảnh", new Vector2(-182f, by), new Vector2(158f, 46f),
                Capture, true, 16, new Vector2(1, 1));
        }

        // ------------------------------------------------------------------
        // Mở, đóng
        // ------------------------------------------------------------------

        void ApplyWindowScale()
        {
            if (panel != null)
            {
                var target = UiTheme.PanelSize(new Vector2(PanelW, PanelH));
                float scale = Mathf.Min(target.x / PanelW, target.y / PanelH);
                panel.localScale = new Vector3(scale, scale, 1f);
            }
            if (galleryPanel != null)
            {
                const float Gw = 1260f, Gh = 840f;
                var gTarget = UiTheme.PanelSize(new Vector2(Gw, Gh));
                float gScale = Mathf.Min(gTarget.x / Gw, gTarget.y / Gh);
                galleryPanel.localScale = new Vector3(gScale, gScale, 1f);
            }
        }

        void Show(MicroscopeController microscope)
        {
            if (root == null || builtTheme != UiTheme.Current || builtFontScale != UiTheme.FontScaleMode)
            {
                DestroyUi();
                Build();
            }
            ApplyWindowScale();
            mic = microscope;
            open = true;
            root.SetActive(true);
            root.transform.SetAsLastSibling();
            UiBridge.SetCapture(ref captured, true);
            mic.SetViewing(true);
            ApplyLiveStateToControls();
            group.alpha = 0f;
            UiMotion.Popup(this, animFrame != null ? animFrame : panel, group, 0.18f);
            BioFeedback.LogAction(mic.isTeacherCamera ? "kinh_hien_vi_camera" : "kinh_hien_vi", "mo_man_hinh_soi",
                $"Mở màn hình soi kính ở độ phóng đại {BioFeedback.FormatVi(mic.CurrentMagnification, "0")}×.");
        }

        void ApplyLiveStateToControls()
        {
            if (mic == null) return;
            focusSlider.SetValueWithoutNotify(Mathf.Clamp(mic.FocusOffsetMm, -FocusRangeMm, FocusRangeMm));
            lightSlider.SetValueWithoutNotify(Mathf.Clamp01(mic.brightness));
            shownSlide = "";
            shownObjective = -1;
            view.texture = mic.GetViewportTexture();
            UpdateGalleryButtonCount();
            UpdateReportButtonState();
        }

        public void Close()
        {
            if (!open) return;
            CloseGallery();
            open = false;
            if (mic != null) mic.SetViewing(false);
            mic = null;
            if (root != null) root.SetActive(false);
            UiBridge.SetCapture(ref captured, false);
        }

        // ------------------------------------------------------------------
        // Thao tác điều khiển & thí nghiệm
        // ------------------------------------------------------------------

        void PickSlide(string id)
        {
            if (mic == null) return;
            mic.LoadSlideById(id);
            BioFeedback.PlayClick();
            RefreshAll();
        }

        void PickObjective(int index)
        {
            if (mic == null) return;
            mic.SetObjective(index);
            RefreshAll();
        }

        void OnFocus(float offsetMm)
        {
            if (mic == null) return;
            mic.SetFocus(offsetMm);
            RefreshInfo();
        }

        void AutoFocus()
        {
            if (mic == null) return;
            mic.SetFocus(0f);
            focusSlider.SetValueWithoutNotify(0f);
            BioFeedback.PlaySuccess();
            RefreshInfo();
        }

        void OnLight(float v)
        {
            if (mic == null) return;
            mic.SetBrightness(v);
            if (lightValueText != null)
                lightValueText.text = Mathf.RoundToInt(Mathf.Clamp01(v) * 100f) + "%";
        }

        void PickStain(int index)
        {
            if (mic == null) return;
            mic.SetStain(index == 1 ? "lugol" : index == 2 ? "methylene_blue" : "");
            RefreshAll();
        }

        void TogglePlasmolysis()
        {
            if (mic == null) return;
            mic.ApplyReagent(mic.plasmolysisActive ? "nuoc_cat" : "nacl");
            RefreshAll();
        }

        void ToggleOil()
        {
            if (mic == null) return;
            if (mic.oilApplied) mic.CleanOilWithLensTissue(); else mic.ApplyImmersionOil();
            RefreshAll();
        }

        void TogglePanMode()
        {
            panEnabled = !panEnabled;
            BioFeedback.PlayClick();
            StyleCornerPill(cornerPanBtn, panEnabled);
            BioFeedback.ShowToast(panEnabled ? "Bật chế độ kéo: kéo trên thị trường để dịch tiêu bản." : "Đã khóa kéo bàn sa trượt.", false);
        }

        void ToggleRuler()
        {
            if (mic == null) return;
            mic.showMicrometerReticle = !mic.showMicrometerReticle;
            mic.Refresh(true);
            RefreshAll();
        }

        void ToggleNotes()
        {
            if (mic == null) return;
            mic.showAnnotations = !mic.showAnnotations;
            RefreshAll();
        }

        void StreamToWall()
        {
            if (mic == null) return;
            mic.StreamToVideoWall();
            BioFeedback.PlaySuccess();
            BioFeedback.ShowToast("Đã chiếu thị trường kính hiển vi lên tường video.", false);
        }

        void MeasureCell()
        {
            if (mic == null) return;
            if (!mic.showMicrometerReticle)
            {
                mic.showMicrometerReticle = true;
                mic.Refresh(true);
                RefreshAll();
            }
            string msg = $"Đo kích thước tế bào ≈ {BioFeedback.FormatVi(mic.ComputeMeasuredCellSizeUm(), "0")} µm · 1 vạch = {BioFeedback.FormatVi(mic.MicronsPerDivision, "0.#")} µm";
            BioFeedback.PlaySuccess();
            BioFeedback.ShowToast(msg, false);
        }

        void AskAssistant()
        {
            string slideTitle = ResolveSlideTitle();
            string magLabel = mic != null ? BioFeedback.FormatVi(mic.CurrentMagnification, "0") + "×" : "40×";
            string stainLabel = mic != null ? ResolveStainLabel(mic.appliedStain) : "Không nhuộm";
            int focusPercent = mic != null ? Mathf.RoundToInt(Mathf.Clamp01(1f - (float)mic.ComputeCurrentBlurFactor()) * 100f) : 0;
            string notes = mic != null ? BuildObservationNotes(mic) : "";

            string topic = $"Kính hiển vi · {slideTitle} ({magLabel}, nhuộm {stainLabel}, nét {focusPercent}%)";
            string prompt = $"Giải thích ngắn gọn các đặc điểm cần quan sát trên tiêu bản \"{slideTitle}\" ở {magLabel} (nhuộm {stainLabel}, độ nét {focusPercent}%)"
                + (string.IsNullOrEmpty(notes) ? " và lưu ý khi lấy nét." : $" · {notes}.");

            float beforeMsgTime = LabMessages.MessageTime;
            if (!RobotChatUI.AskAbout(topic, prompt))
            {
                if (Mathf.Approximately(LabMessages.MessageTime, beforeMsgTime) && Time.time - LabMessages.MessageTime >= 2f)
                    LabMessages.Say("Trợ lý robot hiện chưa sẵn sàng hoặc đang tạm khóa.", LabMessages.Tone.Warn);
            }
        }

        void Capture() => CaptureSnapshotInternal(true);

        CapturedShot CaptureSnapshotInternal(bool showCaptureToast)
        {
            if (mic == null) return null;
            mic.Refresh(true);
            Texture srcTex = mic.GetViewportTexture();
            string file = mic.CaptureMicrographPng();
            if (string.IsNullOrEmpty(file))
            {
                if (srcTex == null)
                {
                    LabMessages.Say("Không lưu được ảnh hiển vi.", LabMessages.Tone.Warn);
                    return null;
                }
                file = $"hien_vi_{mic.activeSlideCode}_{Mathf.RoundToInt((float)mic.CurrentMagnification)}x_{DateTime.Now:HHmmss}.png";
            }
            var shot = SaveSnapshotPreview(file, srcTex);
            if (showCaptureToast)
                BioFeedback.ShowToast("Đã lưu ảnh hiển vi: " + file, false);
            return shot;
        }

        CapturedShot SaveSnapshotPreview(string fileName, Texture srcTex)
        {
            Texture fallbackTex = mic != null ? mic.GetViewportTexture() : null;
            Texture2D preview = CopyTextureSafe(srcTex != null ? srcTex : fallbackTex);
            double blur = mic != null ? mic.ComputeCurrentBlurFactor() : 0.0;
            int sharpPct = Mathf.RoundToInt(Mathf.Clamp01(1f - (float)blur) * 100f);
            string focusDesc = blur < 0.12 ? "Ảnh rõ nét" : blur < 0.6 ? "Gần đạt nét" : "Ảnh mờ";
            string objName = mic != null ? ObjectiveFullNames[Mathf.Clamp(mic.currentObjectiveIndex, 0, ObjectiveFullNames.Length - 1)] : "4×";
            string magStr = mic != null ? BioFeedback.FormatVi(mic.CurrentMagnification, "0") + "×" : "40×";

            var shot = new CapturedShot
            {
                id = Guid.NewGuid().ToString("N"),
                fileName = fileName,
                slideCode = mic != null ? mic.activeSlideCode : "",
                slideTitle = ResolveSlideTitle(),
                objectiveLabel = objName,
                magLabel = magStr,
                stainLabel = ResolveStainLabel(mic != null ? mic.appliedStain : ""),
                focusLabel = focusDesc,
                sharpPercent = sharpPct,
                measuredCellUm = mic != null ? mic.ComputeMeasuredCellSizeUm() : 0.0,
                notes = BuildObservationNotes(mic),
                timestamp = DateTime.Now.ToString("HH:mm:ss"),
                preview = preview,
                addedToReport = false
            };

            capturedShots.Add(shot);
            while (capturedShots.Count > 12)
            {
                if (capturedShots[0].preview != null)
                {
                    MicroscopeReportStore.ClearPreviewRefs(capturedShots[0].preview);
                    Destroy(capturedShots[0].preview);
                }
                capturedShots.RemoveAt(0);
            }
            galleryIndex = Mathf.Max(0, capturedShots.Count - 1);
            UpdateGalleryButtonCount();
            UpdateReportButtonState();
            if (galleryOverlay != null && galleryOverlay.activeSelf)
            {
                RebuildGalleryCards();
                RenderGalleryItem();
            }
            return shot;
        }

        static Texture2D CopyTextureSafe(Texture src)
        {
            if (src == null || src.width <= 0 || src.height <= 0) return null;
            if (src is Texture2D t2d)
            {
                try
                {
                    var copy = new Texture2D(t2d.width, t2d.height, TextureFormat.RGBA32, false)
                    {
                        hideFlags = HideFlags.DontSave,
                        wrapMode = TextureWrapMode.Clamp,
                        filterMode = FilterMode.Bilinear
                    };
                    copy.SetPixels32(t2d.GetPixels32());
                    copy.Apply(false, false);
                    return copy;
                }
                catch { }
            }

            RenderTexture prev = RenderTexture.active;
            RenderTexture tmp = null;
            try
            {
                tmp = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(src, tmp);
                RenderTexture.active = tmp;
                var copy = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.DontSave,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                copy.ReadPixels(new Rect(0f, 0f, src.width, src.height), 0, 0);
                copy.Apply(false, false);
                return copy;
            }
            catch
            {
                return null;
            }
            finally
            {
                RenderTexture.active = prev;
                if (tmp != null) RenderTexture.ReleaseTemporary(tmp);
            }
        }

        static string ResolveStainLabel(string stainCode)
        {
            switch (stainCode)
            {
                case "lugol": return "Iốt (Lugol)";
                case "methylene_blue": return "Xanh Methylene";
                case "gram": return "Nhuộm Gram";
                default: return "Không nhuộm";
            }
        }

        static string BuildObservationNotes(MicroscopeController m)
        {
            if (m == null) return "";
            var parts = new List<string>(4);
            if (m.plasmolysisActive)
                parts.Add("Đang co nguyên sinh (NaCl 5%)");
            if (m.currentObjectiveIndex == 3)
                parts.Add(m.oilApplied ? "Đã nhỏ dầu soi 100×" : "Thiếu dầu soi ở vật kính 100×");
            else if (m.currentObjectiveIndex == 2 && m.objective40xOilDirty)
                parts.Add("Cần lau sạch dầu ở vật kính 40×");
            else if (m.oilApplied)
                parts.Add("Có dầu soi trên lam kính");
            parts.Add($"Đèn {Mathf.RoundToInt(Mathf.Clamp01(m.brightness) * 100f)}%");
            if (m.showMicrometerReticle)
                parts.Add($"Thước 1 vạch = {BioFeedback.FormatVi(m.MicronsPerDivision, "0.#")} µm");
            return string.Join(" · ", parts);
        }

        void AddLiveOrLatestToReport()
        {
            if (mic == null) return;
            CapturedShot target = null;
            if (capturedShots.Count > 0)
            {
                var latest = capturedShots[capturedShots.Count - 1];
                string curStain = ResolveStainLabel(mic.appliedStain);
                string curObj = ObjectiveFullNames[Mathf.Clamp(mic.currentObjectiveIndex, 0, ObjectiveFullNames.Length - 1)];
                string curNotes = BuildObservationNotes(mic);
                if (!latest.addedToReport && latest.slideCode == mic.activeSlideCode && latest.objectiveLabel == curObj
                    && latest.stainLabel == curStain && latest.notes == curNotes)
                {
                    target = latest;
                }
            }
            if (target == null)
                target = CaptureSnapshotInternal(false);
            AddShotToReport(target);
        }

        void AddSelectedGalleryShotToReport()
        {
            if (capturedShots.Count == 0 || galleryIndex < 0 || galleryIndex >= capturedShots.Count) return;
            AddShotToReport(capturedShots[galleryIndex]);
        }

        void AddShotToReport(CapturedShot shot)
        {
            if (shot == null) return;
            if (shot.addedToReport || MicroscopeReportStore.ContainsId(shot.id))
            {
                shot.addedToReport = true;
                LabMessages.Say($"Tiêu bản \"{shot.slideTitle}\" đã có trong báo cáo.", LabMessages.Tone.Info);
                UpdateReportButtonState();
                if (galleryOverlay != null && galleryOverlay.activeSelf)
                {
                    RebuildGalleryCards();
                    RenderGalleryItem();
                }
                return;
            }

            var entry = new MicroscopeReportEntry
            {
                id = shot.id,
                slideCode = shot.slideCode,
                specimen = shot.slideTitle,
                magnification = shot.magLabel,
                objective = shot.objectiveLabel,
                focusQuality = shot.focusLabel,
                sharpPercent = shot.sharpPercent,
                stain = shot.stainLabel,
                notes = shot.notes,
                fileName = shot.fileName,
                timestamp = shot.timestamp,
                measuredCellUm = shot.measuredCellUm,
                preview = shot.preview
            };

            if (MicroscopeReportStore.Add(entry))
            {
                shot.addedToReport = true;
                BioFeedback.PlaySuccess();
                LabMessages.Say($"Đã thêm \"{shot.slideTitle}\" ({shot.magLabel}) vào báo cáo ({MicroscopeReportStore.Count} mục).", LabMessages.Tone.Good);
            }
            UpdateReportButtonState();
            if (galleryOverlay != null && galleryOverlay.activeSelf)
            {
                RebuildGalleryCards();
                RenderGalleryItem();
            }
        }

        void UpdateGalleryButtonCount()
        {
            if (galleryBtnText != null)
                galleryBtnText.text = $"Ảnh đã chụp ({capturedShots.Count})";
        }

        void UpdateReportButtonState()
        {
            if (addReportBtnText != null)
                addReportBtnText.text = MicroscopeReportStore.Count > 0
                    ? $"Thêm vào báo cáo ({MicroscopeReportStore.Count})"
                    : "Thêm vào báo cáo";
        }

        void OpenGallery()
        {
            if (root == null) return;
            if (capturedShots.Count > 0)
                galleryIndex = Mathf.Clamp(galleryIndex, 0, capturedShots.Count - 1);
            else
                galleryIndex = 0;
            galleryZoom2x = false;
            galleryZoomCenter = new Vector2(0.5f, 0.5f);
            if (galleryOverlay == null) BuildGalleryOverlay();
            ApplyWindowScale();
            galleryOverlay.SetActive(true);
            galleryOverlay.transform.SetAsLastSibling();
            RebuildGalleryCards();
            RenderGalleryItem();
        }

        void CloseGallery()
        {
            if (galleryOverlay != null) galleryOverlay.SetActive(false);
        }

        void StepGallery(int delta)
        {
            if (capturedShots.Count == 0) return;
            galleryIndex = (galleryIndex + delta + capturedShots.Count) % capturedShots.Count;
            galleryZoom2x = false;
            galleryZoomCenter = new Vector2(0.5f, 0.5f);
            RebuildGalleryCards();
            RenderGalleryItem();
        }

        void SelectGalleryIndex(int index)
        {
            if (capturedShots.Count == 0) return;
            galleryIndex = Mathf.Clamp(index, 0, capturedShots.Count - 1);
            galleryZoom2x = false;
            galleryZoomCenter = new Vector2(0.5f, 0.5f);
            BioFeedback.PlayClick();
            RebuildGalleryCards();
            RenderGalleryItem();
        }

        void DeleteSelectedGalleryShot()
        {
            if (capturedShots.Count == 0 || galleryIndex < 0 || galleryIndex >= capturedShots.Count) return;
            var shot = capturedShots[galleryIndex];
            if (shot.preview != null)
            {
                MicroscopeReportStore.ClearPreviewRefs(shot.preview);
                Destroy(shot.preview);
            }
            capturedShots.RemoveAt(galleryIndex);
            if (capturedShots.Count == 0) galleryIndex = 0;
            else galleryIndex = Mathf.Clamp(galleryIndex, 0, capturedShots.Count - 1);
            galleryZoom2x = false;
            galleryZoomCenter = new Vector2(0.5f, 0.5f);
            UpdateGalleryButtonCount();
            RebuildGalleryCards();
            RenderGalleryItem();
            LabMessages.Say("Đã xóa ảnh chụp khỏi thư viện phiên.", LabMessages.Tone.Info);
        }

        void ToggleGalleryZoom()
        {
            if (capturedShots.Count == 0) return;
            galleryZoom2x = !galleryZoom2x;
            if (!galleryZoom2x) galleryZoomCenter = new Vector2(0.5f, 0.5f);
            BioFeedback.PlayClick();
            ApplyGalleryImageZoom();
        }

        void OnGalleryImageDrag(Vector2 delta)
        {
            if (!galleryZoom2x || capturedShots.Count == 0) return;
            const float sensitivity = 1f / 900f;
            galleryZoomCenter.x = Mathf.Clamp(galleryZoomCenter.x - delta.x * sensitivity, 0.25f, 0.75f);
            galleryZoomCenter.y = Mathf.Clamp(galleryZoomCenter.y - delta.y * sensitivity, 0.25f, 0.75f);
            ApplyGalleryImageZoom();
        }

        void ApplyGalleryImageZoom()
        {
            if (galleryImage != null)
            {
                galleryImage.uvRect = galleryZoom2x
                    ? new Rect(galleryZoomCenter.x - 0.25f, galleryZoomCenter.y - 0.25f, 0.5f, 0.5f)
                    : new Rect(0f, 0f, 1f, 1f);
            }
            if (galleryZoomBtnText != null)
                galleryZoomBtnText.text = galleryZoom2x ? "Thu nhỏ 1×" : "Phóng to 2×";
        }

        void BuildGalleryOverlay()
        {
            const float Gw = 1260f, Gh = 840f;
            const float GHead = 62f, GFoot = 68f, GBody = Gh - GHead - GFoot;
            const float GLeft = 456f, GRight = Gw - GLeft - 1f;

            var go = new GameObject("Xem ảnh đã chụp", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(root.transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            galleryOverlay = go;

            Backdrop(go.transform, "Nền mờ thư viện", CloseGallery, 0.72f);
            var win = Window(go.transform, "Khung thư viện ảnh", new Vector2(Gw, Gh), Vector2.zero);
            var wrt = (RectTransform)win.transform;
            galleryPanel = wrt;
            ApplyWindowScale();

            // Đầu thư viện (62 px)
            galleryTitleText = Label(wrt, "Tiêu đề thư viện", "Thư viện ảnh hiển vi", 20, Ink, true, TextAnchor.MiddleLeft,
                new Vector2(24f, 0f), new Vector2(680f, GHead));
            galleryTitleText.verticalOverflow = VerticalWrapMode.Truncate;

            galleryReportBadgeBg = MakeHeaderBadge(wrt, "Thẻ đếm báo cáo", new Vector2(-396f, -12f), new Vector2(242f, 38f),
                out galleryReportBadgeBorder, out galleryReportBadgeText, Accent, AccentSoft, 13);

            MakeButton(wrt, "Đóng thư viện trên", PadInput.UsingTouch ? "Đóng" : "Đóng · Esc", new Vector2(-142f, -9f), new Vector2(118f, 44f),
                CloseGallery, false, 14, new Vector2(1, 1));
            Rule(wrt, new Vector2(0f, -GHead), Gw);

            // Cột trái: danh sách ảnh thu nhỏ (cuộn được)
            var leftPane = Rect(wrt, "Cột danh sách ảnh", new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(0f, -GHead), new Vector2(GLeft, GBody));
            leftPane.gameObject.AddComponent<RectMask2D>();
            Label(leftPane, "Nhãn danh sách", "DANH SÁCH ẢNH ĐÃ CHỤP", 12, Muted, true, TextAnchor.UpperLeft,
                new Vector2(20f, -14f), new Vector2(220f, 18f));
            Label(leftPane, "Gợi ý chọn", "Bấm thẻ để xem", 12, Dim, false, TextAnchor.UpperRight,
                new Vector2(240f, -14f), new Vector2(196f, 18f));

            var scrollGo = new GameObject("Khung cuộn ảnh", typeof(RectTransform));
            var scrollRt = (RectTransform)scrollGo.transform;
            scrollRt.SetParent(leftPane, false);
            scrollRt.anchorMin = new Vector2(0, 1);
            scrollRt.anchorMax = new Vector2(0, 1);
            scrollRt.pivot = new Vector2(0, 1);
            scrollRt.anchoredPosition = new Vector2(16f, -40f);
            scrollRt.sizeDelta = new Vector2(424f, GBody - 52f);
            scrollGo.AddComponent<RectMask2D>();
            var hitImg = scrollGo.AddComponent<Image>();
            hitImg.sprite = Plain;
            hitImg.color = new Color(1f, 1f, 1f, 0.001f);
            var sr = scrollGo.AddComponent<ScrollRect>();
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 28f;
            sr.viewport = scrollRt;

            galleryGridContent = Rect(scrollRt, "Lưới thẻ ảnh", new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(424f, 200f));
            sr.content = galleryGridContent;

            var emptyLeftFill = Fill(leftPane, "Trống danh sách", new Vector2(20f, -44f), new Vector2(416f, 140f), Well, null, true);
            AddBorder((RectTransform)emptyLeftFill.transform, Line, UiTheme.IsVera ? 2.4f : 3f);
            Label(emptyLeftFill.transform, "Tiêu đề trống", "Chưa có ảnh nào được chụp", 16, Ink, true, TextAnchor.MiddleCenter,
                new Vector2(16f, -24f), new Vector2(384f, 30f));
            Label(emptyLeftFill.transform, "Mô tả trống", "Bấm \"Chụp\" hoặc \"Chụp ảnh\" khi đang soi kính để lưu tiêu bản.", 13, Dim, false, TextAnchor.UpperCenter,
                new Vector2(20f, -64f), new Vector2(376f, 52f));
            galleryEmptyLeft = emptyLeftFill.gameObject;

            // Vạch chia dọc giữa 2 cột
            Fill(wrt, "Vạch chia thư viện", new Vector2(GLeft, -GHead), new Vector2(1f, GBody), Line);

            // Cột phải: xem ảnh phóng to & thông số quan sát
            var rightPane = Rect(wrt, "Cột chi tiết ảnh", new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(GLeft + 1f, -GHead), new Vector2(GRight, GBody));
            rightPane.gameObject.AddComponent<RectMask2D>();

            var frame = Fill(rightPane, "Khung nền ảnh", new Vector2(24f, -16f), new Vector2(GRight - 48f, 466f), Color.black, null, true);
            galleryFrameBox = frame.gameObject;
            var frameRt = (RectTransform)frame.transform;
            AddBorder(frameRt, Line, UiTheme.IsVera ? 2.4f : 3f);
            frameRt.gameObject.AddComponent<RectMask2D>();
            var dragPad = frameRt.gameObject.AddComponent<UiDragPad>();
            dragPad.onDrag = OnGalleryImageDrag;

            var imgRt = Rect(frameRt, "Ảnh mẫu", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(448f, 448f));
            galleryImage = imgRt.gameObject.AddComponent<RawImage>();
            galleryImage.color = Color.white;
            galleryImage.raycastTarget = false;

            var tagFill = Fill(frameRt, "Nhãn trên ảnh", new Vector2(12f, -12f), new Vector2(268f, 34f), CornerPillBg, null, true);
            AddBorder((RectTransform)tagFill.transform, new Color(1f, 1f, 1f, 0.24f), UiTheme.IsVera ? 2.4f : 3f);
            galleryOverlayTagText = Label(tagFill.transform, "Chữ nhãn ảnh", "", 13, Color.white, true, TextAnchor.MiddleCenter,
                Vector2.zero, new Vector2(268f, 34f));

            // Bảng thông số quan sát dưới ảnh
            var metaWell = Fill(rightPane, "Thẻ thông số ảnh", new Vector2(24f, -496f), new Vector2(GRight - 48f, 198f), Well, null, true);
            galleryMetaBox = metaWell.gameObject;
            AddBorder((RectTransform)metaWell.transform, Line, UiTheme.IsVera ? 2.4f : 3f);
            gallerySpecimenText = Label(metaWell.transform, "Tiêu bản", "", 18, Ink, true, TextAnchor.MiddleLeft,
                new Vector2(16f, -12f), new Vector2(GRight - 290f, 28f));
            gallerySpecimenText.verticalOverflow = VerticalWrapMode.Truncate;

            var statusPill = Fill(metaWell.transform, "Trạng thái báo cáo", new Vector2(GRight - 48f - 224f, -12f), new Vector2(208f, 30f), GoodSoft, null, true);
            galleryReportStatusBg = statusPill;
            galleryReportStatusBorder = AddBorder((RectTransform)statusPill.transform, Good, UiTheme.IsVera ? 2.4f : 3f);
            galleryReportStatusText = Label(statusPill.transform, "Chữ trạng thái", "Chưa vào báo cáo", 12, Good, true, TextAnchor.MiddleCenter,
                Vector2.zero, new Vector2(208f, 30f));

            gallerySpecsText = Label(metaWell.transform, "Quang học", "", 14, Ink, false, TextAnchor.UpperLeft,
                new Vector2(16f, -50f), new Vector2(GRight - 80f, 24f));
            galleryMeasureText = Label(metaWell.transform, "Đo và tệp", "", 13, Dim, false, TextAnchor.UpperLeft,
                new Vector2(16f, -80f), new Vector2(GRight - 80f, 24f));
            galleryNotesText = Label(metaWell.transform, "Ghi chú", "", 13, Dim, false, TextAnchor.UpperLeft,
                new Vector2(16f, -110f), new Vector2(GRight - 80f, 74f));
            galleryNotesText.verticalOverflow = VerticalWrapMode.Truncate;

            var emptyRightFill = Fill(rightPane, "Trống xem ảnh", new Vector2(24f, -16f), new Vector2(GRight - 48f, GBody - 32f), Well, null, true);
            AddBorder((RectTransform)emptyRightFill.transform, Line, UiTheme.IsVera ? 2.4f : 3f);
            Label(emptyRightFill.transform, "Tiêu đề trống phải", "Thư viện ảnh phiên này đang trống", 20, Ink, true, TextAnchor.MiddleCenter,
                new Vector2(40f, -220f), new Vector2(GRight - 128f, 34f));
            Label(emptyRightFill.transform, "Gợi ý trống phải", "Chụp trực tiếp thị trường đang soi để xem phóng to 2× và đính kèm vào báo cáo.", 14, Dim, false, TextAnchor.UpperCenter,
                new Vector2(60f, -264f), new Vector2(GRight - 168f, 52f));
            MakeButton(emptyRightFill.transform, "Chụp ngay trong thư viện", "Chụp ảnh hiện tại",
                new Vector2((GRight - 48f - 220f) * 0.5f, -330f), new Vector2(220f, 46f),
                () => CaptureSnapshotInternal(true), true, 15);
            galleryEmptyRight = emptyRightFill.gameObject;

            // Chân thư viện (68 px)
            float gfy = -Gh + GFoot;
            float gby = gfy - (GFoot - 44f) * 0.5f;
            Rule(wrt, new Vector2(0f, gfy), Gw);
            galleryPrevBtn = MakeButton(wrt, "Ảnh trước", "< Trước", new Vector2(24f, gby), new Vector2(114f, 44f), () => StepGallery(-1), false, 14);
            galleryNextBtn = MakeButton(wrt, "Ảnh sau", "Sau >", new Vector2(148f, gby), new Vector2(114f, 44f), () => StepGallery(1), false, 14);
            galleryZoomBtn = MakeButton(wrt, "Phóng to ảnh", "Phóng to 2×", new Vector2(272f, gby), new Vector2(148f, 44f), ToggleGalleryZoom, false, 14);
            galleryZoomBtnText = galleryZoomBtn.GetComponentInChildren<Text>();
            galleryDeleteBtn = MakeButton(wrt, "Xóa ảnh", "Xóa ảnh", new Vector2(430f, gby), new Vector2(122f, 44f), DeleteSelectedGalleryShot, false, 14);
            var delTxt = galleryDeleteBtn.GetComponentInChildren<Text>();
            if (delTxt != null) delTxt.color = Bad;

            galleryAddReportBtn = MakeButton(wrt, "Lưu báo cáo từ thư viện", "Thêm vào báo cáo", new Vector2(-416f, gby), new Vector2(216f, 44f),
                AddSelectedGalleryShotToReport, false, 14, new Vector2(1, 1));
            galleryAddReportBtnText = galleryAddReportBtn.GetComponentInChildren<Text>();

            MakeButton(wrt, "Quay lại soi", "Quay lại soi", new Vector2(-188f, gby), new Vector2(164f, 44f),
                CloseGallery, true, 15, new Vector2(1, 1));
        }

        void RebuildGalleryCards()
        {
            if (galleryGridContent == null) return;
            for (int i = galleryGridContent.childCount - 1; i >= 0; i--)
                Destroy(galleryGridContent.GetChild(i).gameObject);
            galleryCards.Clear();

            bool hasAny = capturedShots.Count > 0;
            if (galleryEmptyLeft != null) galleryEmptyLeft.SetActive(!hasAny);
            if (galleryEmptyRight != null) galleryEmptyRight.SetActive(!hasAny);
            if (galleryFrameBox != null) galleryFrameBox.SetActive(hasAny);
            if (galleryMetaBox != null) galleryMetaBox.SetActive(hasAny);
            if (!hasAny)
            {
                galleryGridContent.sizeDelta = new Vector2(424f, 100f);
                return;
            }

            const int cols = 2;
            const float cardW = 198f, cardH = 198f, gap = 12f, startX = 4f, startY = -4f;
            for (int i = 0; i < capturedShots.Count; i++)
            {
                int idx = i;
                var shot = capturedShots[i];
                int col = i % cols;
                int row = i / cols;
                float cx = startX + col * (cardW + gap);
                float cy = startY - row * (cardH + gap);
                bool selected = i == galleryIndex;

                var cardRt = Rect(galleryGridContent, "Thẻ ảnh " + (i + 1), new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(cx, cy), new Vector2(cardW, cardH));
                var bgImg = cardRt.gameObject.AddComponent<Image>();
                bgImg.sprite = Solid;
                bgImg.type = Image.Type.Sliced;
                bgImg.pixelsPerUnitMultiplier = UiTheme.IsVera ? 2.2f : 3f;
                bgImg.color = selected ? AccentSoft : Well;
                AddBorder(cardRt, selected ? Accent : Line, bgImg.pixelsPerUnitMultiplier);

                var btn = cardRt.gameObject.AddComponent<Button>();
                btn.navigation = new Navigation { mode = Navigation.Mode.None };
                btn.onClick.AddListener(() => SelectGalleryIndex(idx));
                galleryCards.Add(btn);

                // Ảnh thu nhỏ nền đen quang học
                var thumbBg = Fill(cardRt, "Khung thu nhỏ", new Vector2(10f, -10f), new Vector2(cardW - 20f, 114f), Color.black);
                var thumbImgRt = Rect(thumbBg.transform, "Ảnh thu nhỏ", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(106f, 106f));
                var raw = thumbImgRt.gameObject.AddComponent<RawImage>();
                Texture fallbackThumb = mic != null ? mic.GetViewportTexture() : null;
                raw.texture = shot.preview != null ? shot.preview : fallbackThumb;
                raw.color = Color.white;
                raw.raycastTarget = false;

                var tTitle = Label(cardRt, "Tên tiêu bản", shot.slideTitle, 13, selected ? Accent : Ink, true, TextAnchor.UpperLeft,
                    new Vector2(10f, -128f), new Vector2(cardW - 20f, 22f));
                tTitle.verticalOverflow = VerticalWrapMode.Truncate;

                var tSub = Label(cardRt, "Độ phóng đại", $"{shot.magLabel} · {shot.stainLabel}", 12, Dim, false, TextAnchor.UpperLeft,
                    new Vector2(10f, -151f), new Vector2(cardW - 20f, 20f));
                tSub.verticalOverflow = VerticalWrapMode.Truncate;

                bool inRep = shot.addedToReport || MicroscopeReportStore.ContainsId(shot.id);
                var tTime = Label(cardRt, "Thời gian", inRep ? $"{shot.timestamp} · Báo cáo" : shot.timestamp, 11,
                    inRep ? Good : Muted, inRep, TextAnchor.UpperLeft, new Vector2(10f, -173f), new Vector2(cardW - 20f, 18f));
                tTime.verticalOverflow = VerticalWrapMode.Truncate;
            }

            int totalRows = (capturedShots.Count + cols - 1) / cols;
            float totalH = totalRows * cardH + Mathf.Max(0, totalRows - 1) * gap + 12f;
            galleryGridContent.sizeDelta = new Vector2(424f, totalH);
        }

        void RenderGalleryItem()
        {
            if (galleryOverlay == null) return;

            int repCount = MicroscopeReportStore.Count;
            if (galleryReportBadgeText != null)
                galleryReportBadgeText.text = repCount > 0 ? $"Báo cáo đã lưu: {repCount} mục" : "Báo cáo: 0 mục";
            ApplyBadgeColors(galleryReportBadgeBg, galleryReportBadgeBorder, galleryReportBadgeText,
                repCount > 0 ? Good : Dim, repCount > 0 ? GoodSoft : Well);

            bool hasShots = capturedShots.Count > 0;
            if (galleryPrevBtn != null) galleryPrevBtn.interactable = capturedShots.Count > 1;
            if (galleryNextBtn != null) galleryNextBtn.interactable = capturedShots.Count > 1;
            if (galleryZoomBtn != null) galleryZoomBtn.interactable = hasShots;
            if (galleryDeleteBtn != null) galleryDeleteBtn.interactable = hasShots;

            if (!hasShots)
            {
                if (galleryTitleText != null) galleryTitleText.text = "Thư viện ảnh hiển vi · 0 ảnh";
                if (galleryAddReportBtn != null) galleryAddReportBtn.interactable = false;
                if (galleryAddReportBtnText != null) galleryAddReportBtnText.text = "Thêm vào báo cáo";
                ApplyGalleryImageZoom();
                return;
            }

            galleryIndex = Mathf.Clamp(galleryIndex, 0, capturedShots.Count - 1);
            var shot = capturedShots[galleryIndex];
            bool inReport = shot.addedToReport || MicroscopeReportStore.ContainsId(shot.id);
            shot.addedToReport = inReport;

            if (galleryTitleText != null)
                galleryTitleText.text = $"Thư viện ảnh hiển vi · Ảnh {galleryIndex + 1}/{capturedShots.Count}";
            if (galleryOverlayTagText != null)
                galleryOverlayTagText.text = $"{shot.magLabel} · Nét {shot.sharpPercent}% · {shot.timestamp}";
            if (galleryImage != null)
            {
                Texture fallbackImg = mic != null ? mic.GetViewportTexture() : null;
                galleryImage.texture = shot.preview != null ? shot.preview : fallbackImg;
            }
            ApplyGalleryImageZoom();

            if (gallerySpecimenText != null)
                gallerySpecimenText.text = $"{shot.slideTitle} ({shot.timestamp})";
            if (gallerySpecsText != null)
                gallerySpecsText.text = $"Vật kính: {shot.objectiveLabel} ({shot.magLabel}) · Nhuộm: {shot.stainLabel} · {shot.focusLabel} ({shot.sharpPercent}%)";
            if (galleryMeasureText != null)
                galleryMeasureText.text = $"Kích thước tế bào: ≈ {BioFeedback.FormatVi(shot.measuredCellUm, "0")} µm · Tệp: {shot.fileName}";
            if (galleryNotesText != null)
                galleryNotesText.text = "Ghi chú quan sát: " + (string.IsNullOrEmpty(shot.notes) ? "Không có ghi chú thêm." : shot.notes);

            if (galleryReportStatusText != null)
                galleryReportStatusText.text = inReport ? "Đã thêm vào báo cáo" : "Chưa vào báo cáo";
            ApplyBadgeColors(galleryReportStatusBg, galleryReportStatusBorder, galleryReportStatusText,
                inReport ? Good : Dim, inReport ? GoodSoft : Well);

            if (galleryAddReportBtn != null) galleryAddReportBtn.interactable = !inReport;
            if (galleryAddReportBtnText != null)
                galleryAddReportBtnText.text = inReport ? "Đã thêm báo cáo" : "Thêm vào báo cáo";
        }

        void OnViewDrag(Vector2 delta)
        {
            if (mic == null || !panEnabled) return;
            float mmPerUnit = 13f / ViewSize * (10f / Mathf.Max(4f, (float)(mic.CurrentMagnification / mic.eyepieceMag)));
            mic.AdjustStageXY(-delta.x * mmPerUnit, -delta.y * mmPerUnit);
        }

        void OnViewScroll(float dir)
        {
            if (mic == null) return;
            float step = mic.currentObjectiveIndex >= 2 ? 0.002f : 0.01f;
            float v = Mathf.Clamp(mic.FocusOffsetMm + dir * step, -FocusRangeMm, FocusRangeMm);
            mic.SetFocus(v);
            focusSlider.SetValueWithoutNotify(v);
            RefreshInfo();
        }

        // ------------------------------------------------------------------
        // Cập nhật khung hình & đồng bộ số liệu
        // ------------------------------------------------------------------

        static bool IsChatBlockingKeys() =>
            (RobotChatUI.Instance != null && RobotChatUI.Instance.IsOpen) || UiBridge.LastCloseFrame == Time.frameCount;

        void Update()
        {
            if (!open) return;
            if (mic == null) { Close(); return; }
            var kb = Keyboard.current;
            if (kb != null && !UiBridge.Typing && !IsChatBlockingKeys())
            {
                if (kb.escapeKey.wasPressedThisFrame)
                {
                    if (galleryOverlay != null && galleryOverlay.activeSelf) { CloseGallery(); return; }
                    Close();
                    return;
                }
                if (galleryOverlay == null || !galleryOverlay.activeSelf)
                {
                    if (kb.digit1Key.wasPressedThisFrame) PickObjective(0);
                    else if (kb.digit2Key.wasPressedThisFrame) PickObjective(1);
                    else if (kb.digit3Key.wasPressedThisFrame) PickObjective(2);
                    else if (kb.digit4Key.wasPressedThisFrame) PickObjective(3);
                    float px = (kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.leftArrowKey.isPressed ? 1f : 0f);
                    float py = (kb.upArrowKey.isPressed ? 1f : 0f) - (kb.downArrowKey.isPressed ? 1f : 0f);
                    if (px != 0f || py != 0f) mic.AdjustStageXY(px * Time.unscaledDeltaTime * 3f, py * Time.unscaledDeltaTime * 3f);
                }
                else
                {
                    if (kb.leftArrowKey.wasPressedThisFrame) StepGallery(-1);
                    else if (kb.rightArrowKey.wasPressedThisFrame) StepGallery(1);
                }
            }
            mic.Refresh(false);
            var tex = mic.GetViewportTexture();
            if (view.texture != tex) view.texture = tex;
            if (Time.unscaledTime >= nextInfo) RefreshInfo();
            if (shownObjective != mic.currentObjectiveIndex || shownSlide != mic.activeSlideCode) RefreshAll();
        }

        void RefreshAll()
        {
            if (mic == null) return;
            shownObjective = mic.currentObjectiveIndex;
            shownSlide = mic.activeSlideCode;

            for (int i = 0; i < objectiveChips.Length; i++) SetChip(objectiveChips[i], i == mic.currentObjectiveIndex);
            foreach (var (b, id) in slideRows) SetChip(b, id == mic.activeSlideCode);

            int stain = mic.appliedStain == "lugol" ? 1 : mic.appliedStain == "methylene_blue" ? 2 : 0;
            for (int i = 0; i < stainChips.Length; i++) SetChip(stainChips[i], i == stain);

            SetChip(plasmoChip, mic.plasmolysisActive);
            var plasmoText = plasmoChip.GetComponentInChildren<Text>();
            if (plasmoText != null) plasmoText.text = mic.plasmolysisActive ? "Rửa nước cất" : "Nhỏ NaCl 5%";

            SetChip(oilChip, mic.oilApplied);
            var oilText = oilChip.GetComponentInChildren<Text>();
            if (oilText != null) oilText.text = mic.oilApplied ? "Lau sạch dầu" : "Nhỏ dầu soi";

            StyleCornerPill(cornerPanBtn, panEnabled);
            StyleCornerPill(cornerRulerBtn, mic.showMicrometerReticle);
            StyleCornerPill(cornerNoteBtn, mic.showAnnotations);
            StyleCornerPill(cornerSnapBtn, false);

            foreach (var rl in rulerLabels) rl.gameObject.SetActive(mic.showMicrometerReticle);

            if (headerTitle != null)
                headerTitle.text = "Kính hiển vi · " + ResolveSlideTitle();

            BuildNotes();
            RefreshInfo();
        }

        string ResolveSlideTitle()
        {
            if (mic == null) return "";
            if (mic.mountedSlide != null && !string.IsNullOrEmpty(mic.mountedSlide.SlideTitle))
                return mic.mountedSlide.SlideTitle;
            string slideTitle = mic.activeSlideCode;
            foreach (var (id, name) in MicroscopeController.StandardSlides)
            {
                if (id == mic.activeSlideCode)
                {
                    int dot = name.IndexOf(". ");
                    slideTitle = dot > 0 && dot < 4 ? name.Substring(dot + 2) : name;
                    break;
                }
            }
            return slideTitle;
        }

        static string ShortSlideChipName(string id, string fullName)
        {
            switch (id)
            {
                case "bieu_bi_hanh": return "Hành tím";
                case "co_nguyen_sinh": return "Co nguyên sinh";
                case "nguyen_phan_re_hanh": return "Rễ hành NP";
                case "khi_khong_la_thao": return "Khí khổng";
                case "vi_khuan_gram": return "Vi khuẩn Gram";
                case "nam_men": return "Nấm men";
                case "tb_niem_mac_mieng": return "Niêm mạc miệng";
                case "tb_co_van": return "Mô cơ vân";
                case "tb_than_kinh": return "Thần kinh";
                case "tieu_ban_mau": return "Máu người";
                case "ecoli_gfp": return "E. coli GFP";
                default:
                    int dot = fullName.IndexOf(". ");
                    string s = dot > 0 && dot < 4 ? fullName.Substring(dot + 2) : fullName;
                    int paren = s.IndexOf(" (");
                    return paren > 0 ? s.Substring(0, paren) : s;
            }
        }

        void BuildNotes()
        {
            foreach (var t in noteLabels) if (t != null) Destroy(t.gameObject);
            noteLabels.Clear();
            if (mic == null || !mic.showAnnotations) return;
            var rec = BioCatalogLoader.GetSlide(mic.activeSlideCode);
            if (rec == null || rec.chuThich == null) return;
            foreach (var pt in rec.chuThich)
            {
                var t = Label(view.transform, "Chú thích", "• " + pt.ten, 14, Accent2, true, TextAnchor.MiddleLeft,
                    new Vector2(pt.x * ViewSize, -(1f - pt.y) * ViewSize + 11f), new Vector2(280f, 22f));
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                var sh = t.gameObject.AddComponent<Shadow>();
                sh.effectColor = new Color(0f, 0f, 0f, 0.85f);
                sh.effectDistance = new Vector2(1.2f, -1.2f);
                noteLabels.Add(t);
            }
        }

        void RefreshInfo()
        {
            nextInfo = Time.unscaledTime + 0.2f;
            if (mic == null) return;

            double blur = mic.ComputeCurrentBlurFactor();
            bool oilMissing = mic.currentObjectiveIndex == 3 && !mic.oilApplied;
            bool oilDirty40 = mic.currentObjectiveIndex == 2 && mic.objective40xOilDirty;

            int sharpPct = Mathf.RoundToInt(Mathf.Clamp01(1f - (float)blur) * 100f);
            if (focusValueText != null) focusValueText.text = sharpPct + "%";
            if (lightValueText != null) lightValueText.text = Mathf.RoundToInt(Mathf.Clamp01(mic.brightness) * 100f) + "%";

            Color focusCol = blur < 0.12 ? Good : blur < 0.6 ? Accent2 : Bad;
            if (focusState != null)
            {
                focusState.text = blur < 0.12 ? "Ảnh rõ nét" : blur < 0.6 ? "Gần nét" : "Ảnh mờ";
                focusState.color = focusCol;
            }

            if (headerMagText != null)
                headerMagText.text = BioFeedback.FormatVi(mic.CurrentMagnification, "0") + "×";
            ApplyBadgeColors(headerMagBg, headerMagBorder, headerMagText, Accent, AccentSoft);

            string badgeMsg;
            Color badgeFg, badgeBg;
            if (oilMissing) { badgeMsg = "Thiếu dầu 100×"; badgeFg = Bad; badgeBg = BadSoft; }
            else if (oilDirty40) { badgeMsg = "40× dính dầu"; badgeFg = Bad; badgeBg = BadSoft; }
            else if (blur < 0.12) { badgeMsg = "Ảnh rõ nét"; badgeFg = Good; badgeBg = GoodSoft; }
            else if (blur < 0.6) { badgeMsg = "Gần đạt nét"; badgeFg = Warn; badgeBg = WarnSoft; }
            else { badgeMsg = "Đang mất nét"; badgeFg = Bad; badgeBg = BadSoft; }
            if (headerStateText != null) headerStateText.text = badgeMsg;
            ApplyBadgeColors(headerStateBg, headerStateBorder, headerStateText, badgeFg, badgeBg);

            if (fovText != null) fovText.text = BioFeedback.FormatVi(mic.CurrentFovMm, "0.##") + " mm";
            if (cellText != null) cellText.text = "≈ " + BioFeedback.FormatVi(mic.ComputeMeasuredCellSizeUm(), "0") + " µm";
            if (stageText != null) stageText.text = BioFeedback.FormatVi(mic.MicronsPerDivision, "0.#") + " µm";

            string hint;
            Color hintFg = Warn, hintBg = WarnSoft;
            if (oilMissing)
            {
                hint = "Vật kính 100× cần dầu soi: bấm \"Nhỏ dầu soi\", nếu không ảnh sẽ tối và mờ.";
                hintFg = Bad; hintBg = BadSoft;
            }
            else if (oilDirty40)
            {
                hint = "Vật kính 40× dính dầu soi: bấm \"Lau sạch dầu\" để ảnh trong trở lại.";
                hintFg = Bad; hintBg = BadSoft;
            }
            else if (blur >= 0.6)
            {
                hint = "Ảnh đang mờ: kéo thanh lấy nét tới khi báo \"Ảnh rõ nét\", hoặc bấm \"Tự lấy nét\".";
            }
            else if (mic.plasmolysisActive)
            {
                hint = "NaCl 5% ưu trương: nước rút khỏi không bào, màng sinh chất co khỏi thành tế bào.";
                hintFg = Good; hintBg = GoodSoft;
            }
            else if (mic.currentObjectiveIndex == 0)
            {
                hint = "Đang ở 4×: kéo đưa vùng mẫu vào giữa rồi tăng dần lên 10×, 40×; hoặc đổi nhuộm/NaCl.";
            }
            else
            {
                hint = "Đổi vật kính, lấy nét, nhuộm hoặc nhỏ NaCl để xem thử.";
            }

            if (hintBanner != null) hintBanner.color = hintBg;
            if (hintBorder != null) hintBorder.color = new Color(hintFg.r, hintFg.g, hintFg.b, 0.30f);
            if (hintText != null)
            {
                hintText.text = hint;
                hintText.color = hintFg;
            }
        }

        static Image AddBorder(RectTransform parent, Color color, float pixelsPerUnitMultiplier)
        {
            var go = new GameObject("Viền", typeof(RectTransform));
            var brt = (RectTransform)go.transform;
            brt.SetParent(parent, false);
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = brt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.sprite = Border;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static void ApplyBadgeColors(Image bg, Image border, Text txt, Color fg, Color bgCol)
        {
            if (bg != null) bg.color = bgCol;
            if (border != null) border.color = new Color(fg.r, fg.g, fg.b, 0.35f);
            if (txt != null) txt.color = fg;
        }
    }
}