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
    /// <summary>
    /// Màn hình soi kính hiển vi (dựng lại 2026-11-13): một bảng duy nhất, chỉ giữ các việc chính của một buổi soi kính.
    ///   • Trái: chọn tiêu bản.
    ///   • Giữa: thị trường tròn (kéo để dịch tiêu bản, cuộn để lấy nét tinh), hàng nút vật kính 4× · 10× · 40× · 100×.
    ///   • Phải: thanh lấy nét + nút tự lấy nét, độ sáng đèn, thuốc nhuộm, co nguyên sinh, dầu soi, thước đo, chú thích,
    ///     số đo (phóng đại, thị trường, kích thước tế bào), chụp ảnh và chiếu lên tường video.
    /// Thay cho hai màn hình cũ: bảng IMGUI "ghé mắt thị kính" (chọn núm 1..5 rồi bấm tăng/giảm) và chương trình camera toàn màn hình.
    /// Mọi quy tắc an toàn của kính (100× thiếu dầu, 40× dính dầu, chưa đậy lamen) vẫn do MicroscopeController kiểm tra.
    /// </summary>
    public class MicroscopeUI : MonoBehaviour
    {
        public static MicroscopeUI Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance.open;

        const float PanelW = 1560f, PanelH = 880f;
        const float ViewSize = 620f;
        const float FocusRangeMm = 0.60f;
        static readonly string[] ObjectiveNames = { "4×", "10×", "40×", "100× dầu" };

        bool open, captured;
        MicroscopeController mic;
        GameObject root;
        RectTransform panel;
        CanvasGroup group;
        RawImage view;
        Text title, subtitle, focusState, magText, fovText, cellText, stageText, hintText;
        Slider focusSlider, lightSlider;
        Button[] objectiveChips, stainChips;
        Button plasmoChip, oilChip, rulerChip, noteChip;
        readonly List<(Button b, string id)> slideRows = new List<(Button, string)>();
        readonly List<Text> noteLabels = new List<Text>();
        readonly List<Text> rulerLabels = new List<Text>();
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

        void OnDestroy()
        {
            if (captured) UiBridge.SetCapture(ref captured, false);
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------
        // Dựng
        // ------------------------------------------------------------------

        void Build()
        {
            var canvas = HostCanvas(transform, 46);
            root = new GameObject("Kính hiển vi", typeof(RectTransform));
            var rrt = (RectTransform)root.transform;
            rrt.SetParent(canvas.transform, false);
            rrt.anchorMin = Vector2.zero;
            rrt.anchorMax = Vector2.one;
            rrt.offsetMin = rrt.offsetMax = Vector2.zero;
            group = root.AddComponent<CanvasGroup>();
            Backdrop(root.transform, "Nền mờ", Close, 0.62f);
            var img = Window(root.transform, "Khung kính hiển vi", new Vector2(PanelW, PanelH), new Vector2(0, 6));
            panel = (RectTransform)img.transform;
            var c = panel;

            subtitle = Header(c, "KÍNH HIỂN VI · QUANG HỌC HAI MẮT", "Kính hiển vi", "", PanelW - 240f);
            title = c.Find("Tiêu đề").GetComponent<Text>();
            MakeButton(c, "Đóng", PadInput.UsingTouch ? "Đóng ✕" : "Đóng  Esc", new Vector2(-150, -24), new Vector2(124, 52), Close, false, 15, new Vector2(1, 1));

            // ----- Trái: tiêu bản -----
            const float lx = 30f, lw = 330f;
            Label(c, "Nhãn tiêu bản", "TIÊU BẢN", 13, Muted, true, TextAnchor.UpperLeft, new Vector2(lx, -122), new Vector2(lw, 18));
            float y = -146f;
            foreach (var (id, name) in MicroscopeController.StandardSlides)
            {
                string slideId = id;
                // bỏ số thứ tự đầu dòng, giữ tên
                int dot = name.IndexOf(". ");
                string shortName = dot > 0 && dot < 4 ? name.Substring(dot + 2) : name;
                int paren = shortName.IndexOf(" (");
                if (paren > 0) shortName = shortName.Substring(0, paren);
                var b = MakeChip(c, "Tiêu bản " + id, shortName, new Vector2(lx, y), new Vector2(lw, 50), () => PickSlide(slideId), false, 15);
                var t = b.GetComponentInChildren<Text>();
                t.alignment = TextAnchor.MiddleLeft;
                t.rectTransform.anchoredPosition = new Vector2(14, 0);
                t.rectTransform.sizeDelta = new Vector2(lw - 24, 50);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                slideRows.Add((b, id));
                y -= 55f;
            }

            // ----- Giữa: thị trường -----
            float vx = lx + lw + 34f, vy = -122f;
            var well = Fill(c, "Nền thị trường", new Vector2(vx - 8, vy + 8), new Vector2(ViewSize + 16, ViewSize + 16), new Color(0.01f, 0.015f, 0.025f, 0.92f), null, true);
            var vrt = Rect(c, "Thị trường", new Vector2(0, 1), new Vector2(0, 1), new Vector2(vx, vy), new Vector2(ViewSize, ViewSize));
            view = vrt.gameObject.AddComponent<RawImage>();
            view.color = Color.white;
            var pad = vrt.gameObject.AddComponent<UiDragPad>();
            pad.onDrag = OnViewDrag;
            pad.onScroll = OnViewScroll;
            for (int i = 0; i < 6; i++)
            {
                var rl = Label(vrt, "Số thước", (i * 20).ToString(), 12, new Color(0.10f, 0.10f, 0.12f, 0.9f), true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(34, 16));
                // thước nằm ngang qua tâm, dài 2 × 0,72 × 0,465 bề ngang ảnh
                float half = ViewSize * 0.465f * 0.72f;
                rl.rectTransform.anchoredPosition = new Vector2(ViewSize * 0.5f - half + i / 5f * 2f * half - 17f, -ViewSize * 0.5f + 20f);
                rulerLabels.Add(rl);
            }
            Label(c, "Nhãn vật kính", "VẬT KÍNH", 13, Muted, true, TextAnchor.UpperLeft, new Vector2(vx, vy - ViewSize - 18), new Vector2(200, 18));
            objectiveChips = new Button[4];
            float ow = (ViewSize - 3 * 10f) / 4f;
            for (int i = 0; i < 4; i++)
            {
                int k = i;
                objectiveChips[i] = MakeChip(c, "Vật kính " + ObjectiveNames[i], ObjectiveNames[i], new Vector2(vx + i * (ow + 10f), vy - ViewSize - 40), new Vector2(ow, 54), () => PickObjective(k), false, 17);
            }

            // ----- Phải: điều khiển -----
            float rx = vx + ViewSize + 38f, rw = PanelW - rx - 30f;
            Label(c, "Nhãn lấy nét", "LẤY NÉT", 13, Muted, true, TextAnchor.UpperLeft, new Vector2(rx, -122), new Vector2(200, 18));
            focusState = Label(c, "Độ nét", "", 15, Accent2, true, TextAnchor.UpperRight, new Vector2(rx + rw - 240, -122), new Vector2(240, 20));
            focusSlider = MakeSlider(c, "Thanh lấy nét", new Vector2(rx - 6, -144), new Vector2(rw - 150, 46), -FocusRangeMm, FocusRangeMm, 0f, OnFocus);
            MakeButton(c, "Tự lấy nét", "Tự lấy nét", new Vector2(rx + rw - 140, -144), new Vector2(140, 46), AutoFocus, true, 15);

            Label(c, "Nhãn đèn", "ĐỘ SÁNG ĐÈN", 13, Muted, true, TextAnchor.UpperLeft, new Vector2(rx, -206), new Vector2(200, 18));
            lightSlider = MakeSlider(c, "Thanh độ sáng", new Vector2(rx - 6, -226), new Vector2(rw + 12, 46), 0.15f, 1f, 0.85f, OnLight);

            Label(c, "Nhãn nhuộm", "THUỐC NHUỘM", 13, Muted, true, TextAnchor.UpperLeft, new Vector2(rx, -290), new Vector2(200, 18));
            string[] stains = { "Không nhuộm", "Lugol", "Xanh methylen" };
            stainChips = new Button[3];
            float sw = (rw - 2 * 8f) / 3f;
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                stainChips[i] = MakeChip(c, "Nhuộm " + stains[i], stains[i], new Vector2(rx + i * (sw + 8f), -312), new Vector2(sw, 50), () => PickStain(k), false, 14);
            }

            Label(c, "Nhãn thao tác", "THAO TÁC TRÊN TIÊU BẢN", 13, Muted, true, TextAnchor.UpperLeft, new Vector2(rx, -380), new Vector2(300, 18));
            float hw = (rw - 8f) / 2f;
            plasmoChip = MakeChip(c, "Co nguyên sinh", "Nhỏ NaCl 5 %", new Vector2(rx, -402), new Vector2(hw, 50), TogglePlasmolysis, false, 14);
            oilChip = MakeChip(c, "Dầu soi", "Nhỏ dầu soi", new Vector2(rx + hw + 8f, -402), new Vector2(hw, 50), ToggleOil, false, 14);
            rulerChip = MakeChip(c, "Thước đo", "Thước đo", new Vector2(rx, -458), new Vector2(hw, 50), ToggleRuler, true, 14);
            noteChip = MakeChip(c, "Chú thích", "Chú thích", new Vector2(rx + hw + 8f, -458), new Vector2(hw, 50), ToggleNotes, true, 14);

            Label(c, "Nhãn số đo", "SỐ ĐO", 13, Muted, true, TextAnchor.UpperLeft, new Vector2(rx, -526), new Vector2(200, 18));
            magText = Readout(c, "Ô phóng đại", "PHÓNG ĐẠI", "", new Vector2(rx, -548), new Vector2(hw, 62));
            fovText = Readout(c, "Ô thị trường", "THỊ TRƯỜNG", "", new Vector2(rx + hw + 8f, -548), new Vector2(hw, 62));
            cellText = Readout(c, "Ô tế bào", "KÍCH THƯỚC TẾ BÀO", "", new Vector2(rx, -616), new Vector2(hw, 62));
            stageText = Readout(c, "Ô bàn kính", "1 VẠCH THƯỚC", "", new Vector2(rx + hw + 8f, -616), new Vector2(hw, 62));

            hintText = Label(c, "Lời nhắc", "", 14, Dim, false, TextAnchor.UpperLeft, new Vector2(rx, -690), new Vector2(rw, 64));

            Rule(c, new Vector2(30, -PanelH + 92), PanelW - 60);
            MakeButton(c, "Chụp ảnh", "Chụp ảnh", new Vector2(-250, -PanelH + 78), new Vector2(220, 58), Capture, true, 18, new Vector2(1, 1));
            MakeButton(c, "Chiếu tường video", "Chiếu lên tường video", new Vector2(-510, -PanelH + 78), new Vector2(250, 58), () => mic?.StreamToVideoWall(), false, 16, new Vector2(1, 1));
            string keys = PadInput.UsingTouch ? "Kéo trên thị trường: dịch tiêu bản · Thanh lấy nét: chỉnh rõ ảnh"
                                              : "Kéo trên thị trường: dịch tiêu bản · Cuộn chuột: lấy nét tinh · Phím 1–4: vật kính · Esc: đóng";
            Label(c, "Phím tắt", keys, 14, Muted, false, TextAnchor.MiddleLeft, new Vector2(30, -PanelH + 62), new Vector2(PanelW - 600, 26));
            root.SetActive(false);
        }

        // ------------------------------------------------------------------
        // Mở, đóng
        // ------------------------------------------------------------------

        void Show(MicroscopeController microscope)
        {
            if (root == null) Build();
            mic = microscope;
            open = true;
            root.SetActive(true);
            root.transform.SetAsLastSibling();
            UiBridge.SetCapture(ref captured, true);
            mic.SetViewing(true);
            focusSlider.SetValueWithoutNotify(Mathf.Clamp(mic.FocusOffsetMm, -FocusRangeMm, FocusRangeMm));
            lightSlider.SetValueWithoutNotify(mic.brightness);
            shownSlide = "";
            shownObjective = -1;
            view.texture = mic.GetViewportTexture();
            RefreshAll();
            group.alpha = 0f;
            UiMotion.Popup(this, panel, group, 0.18f);
            BioFeedback.LogAction(mic.isTeacherCamera ? "kinh_hien_vi_camera" : "kinh_hien_vi", "mo_man_hinh_soi",
                $"Mở màn hình soi kính ở độ phóng đại {BioFeedback.FormatVi(mic.CurrentMagnification, "0")}×.");
        }

        public void Close()
        {
            if (!open) return;
            open = false;
            if (mic != null) mic.SetViewing(false);
            mic = null;
            if (root != null) root.SetActive(false);
            UiBridge.SetCapture(ref captured, false);
        }

        // ------------------------------------------------------------------
        // Thao tác
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

        void Capture()
        {
            if (mic == null) return;
            string file = mic.CaptureMicrographPng();
            BioFeedback.ShowToast(string.IsNullOrEmpty(file) ? "Không lưu được ảnh hiển vi." : "Đã lưu ảnh hiển vi: " + file, string.IsNullOrEmpty(file));
        }

        void OnViewDrag(Vector2 delta)
        {
            if (mic == null) return;
            // kéo ảnh sang phải thì tiêu bản đi sang phải: 1 bề ngang thị trường ứng với khoảng 13 mm bàn sa trượt ở 10×
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
        // Cập nhật
        // ------------------------------------------------------------------

        void Update()
        {
            if (!open) return;
            if (mic == null) { Close(); return; }   // kính bị gỡ cùng khu
            var kb = Keyboard.current;
            if (kb != null && !UiBridge.Typing)
            {
                if (kb.escapeKey.wasPressedThisFrame) { Close(); return; }
                if (kb.digit1Key.wasPressedThisFrame) PickObjective(0);
                else if (kb.digit2Key.wasPressedThisFrame) PickObjective(1);
                else if (kb.digit3Key.wasPressedThisFrame) PickObjective(2);
                else if (kb.digit4Key.wasPressedThisFrame) PickObjective(3);
                float px = (kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.leftArrowKey.isPressed ? 1f : 0f);
                float py = (kb.upArrowKey.isPressed ? 1f : 0f) - (kb.downArrowKey.isPressed ? 1f : 0f);
                if (px != 0f || py != 0f) mic.AdjustStageXY(px * Time.unscaledDeltaTime * 3f, py * Time.unscaledDeltaTime * 3f);
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
            plasmoChip.GetComponentInChildren<Text>().text = mic.plasmolysisActive ? "Rửa bằng nước cất" : "Nhỏ NaCl 5 %";
            SetChip(oilChip, mic.oilApplied);
            oilChip.GetComponentInChildren<Text>().text = mic.oilApplied ? "Lau sạch dầu soi" : "Nhỏ dầu soi";
            SetChip(rulerChip, mic.showMicrometerReticle);
            SetChip(noteChip, mic.showAnnotations);
            foreach (var rl in rulerLabels) rl.gameObject.SetActive(mic.showMicrometerReticle);

            string slideTitle = mic.activeSlideCode;
            foreach (var (id, name) in MicroscopeController.StandardSlides)
                if (id == mic.activeSlideCode) { int dot = name.IndexOf(". "); slideTitle = dot > 0 && dot < 4 ? name.Substring(dot + 2) : name; }
            if (mic.mountedSlide != null && !string.IsNullOrEmpty(mic.mountedSlide.SlideTitle)) slideTitle = mic.mountedSlide.SlideTitle;
            title.text = slideTitle;

            BuildNotes();
            RefreshInfo();
        }

        void BuildNotes()
        {
            foreach (var t in noteLabels) if (t != null) Destroy(t.gameObject);
            noteLabels.Clear();
            if (!mic.showAnnotations) return;
            var rec = BioCatalogLoader.GetSlide(mic.activeSlideCode);
            if (rec == null || rec.chuThich == null) return;
            foreach (var pt in rec.chuThich)
            {
                var t = Label(view.transform, "Chú thích", "◂ " + pt.ten, 14, Accent2, true, TextAnchor.MiddleLeft,
                              new Vector2(pt.x * ViewSize, -(1f - pt.y) * ViewSize + 11f), new Vector2(280, 22));
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                var sh = t.gameObject.AddComponent<Shadow>();
                sh.effectColor = new Color(0, 0, 0, 0.85f);
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
            focusState.text = blur < 0.12 ? "Ảnh rõ nét" : blur < 0.6 ? "Gần nét" : "Ảnh mờ";
            focusState.color = blur < 0.12 ? Good : blur < 0.6 ? Accent2 : Bad;
            magText.text = BioFeedback.FormatVi(mic.CurrentMagnification, "0") + "×";
            fovText.text = BioFeedback.FormatVi(mic.CurrentFovMm, "0.##") + " mm";
            cellText.text = "≈ " + BioFeedback.FormatVi(mic.ComputeMeasuredCellSizeUm(), "0") + " µm";
            stageText.text = BioFeedback.FormatVi(mic.MicronsPerDivision, "0.#") + " µm";
            subtitle.text = $"Vật kính {ObjectiveNames[mic.currentObjectiveIndex]} · thị kính {BioFeedback.FormatVi(mic.eyepieceMag, "0")}× · bàn kính X {mic.stageX:+0.0;-0.0;0.0} mm, Y {mic.stageY:+0.0;-0.0;0.0} mm";

            string hint;
            if (oilMissing) hint = "Vật kính 100× cần dầu soi: bấm \"Nhỏ dầu soi\", nếu không ảnh sẽ tối và mờ.";
            else if (mic.currentObjectiveIndex == 2 && mic.objective40xOilDirty) hint = "Vật kính 40× dính dầu soi: bấm \"Lau sạch dầu soi\" để ảnh trong trở lại.";
            else if (blur >= 0.6) hint = "Ảnh đang mờ: kéo thanh lấy nét tới khi báo \"Ảnh rõ nét\", hoặc bấm \"Tự lấy nét\".";
            else if (mic.currentObjectiveIndex == 0) hint = "Bắt đầu ở 4× để tìm vùng mẫu, đưa vào giữa thị trường rồi tăng dần lên 10×, 40×.";
            else if (mic.plasmolysisActive) hint = "NaCl 5 % là dung dịch ưu trương: nước rút khỏi không bào, màng sinh chất tách khỏi thành tế bào.";
            else hint = "Kéo trên thị trường để dịch tiêu bản. Dùng thước đo: kích thước = số vạch × giá trị một vạch.";
            hintText.text = hint;
        }
    }
}
