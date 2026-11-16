using System.Collections.Generic;
using Khkt.Environment;
using Khkt.Input;
using Khkt.Interaction;
using Khkt.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static Khkt.Demo.GlassKit;

namespace Khkt.Demo
{
    /// <summary>
    /// Tra cứu hóa chất (phím B hoặc máy tra cứu ở cửa phòng hóa chất): gõ tên (có dấu hay không dấu đều được),
    /// công thức, nhóm hoặc số thứ tự; chọn một chất để xem thông tin và vị trí (kệ · tầng · ô).
    /// "Chỉ vị trí": viền sáng lọ trên kệ kèm cột sáng; "Đi tới": dịch chuyển tới trước kệ;
    /// "Robot mang ra bàn": trợ lý bay tới lấy lọ đặt lên bàn đang làm.
    /// </summary>
    public class ChemSearchUI : MonoBehaviour
    {
        public ModernUI ui;
        public DesktopViewer viewer;
        public Material beamMaterial;
        public Shader outlineShader;

        public bool IsOpen { get; private set; }
        public static ChemSearchUI Instance { get; private set; }
        public ChemicalBottle Marked { get; private set; }

        GameObject panel;
        Image panelImg;
        Text titleLabel;
        bool glassOk;
        InputField input;
        Button closeBtn, markBtn, gotoBtn, bringBtn;
        readonly List<(Button b, Text name, Text info)> rows = new List<(Button, Text, Text)>();
        Text detailTitle, detailInfo, detailLocation, countText;
        GameObject detail;
        List<ChemEntry> results = new List<ChemEntry>();
        ChemEntry selected;
        int selectedIndex = -1;
        string lastQuery = null;
        bool captured;
        HoverOutline marker;
        Transform beam;
        Dictionary<int, ChemicalBottle> bottles;
        float markUntil;

        const int Rows = 9;

        void Awake() => Instance = this;

        void Start()
        {
            SearchKiosk.OpenRequested += Open;
            UiTheme.Changed += OnThemeChanged;
            var canvas = ui.Canvas;
            UiBridge.EnsureInteractive(canvas);
            var shader = Shader.Find("Khkt/KinhMo") ?? (ui != null ? ui.glassShader : null);
            glassOk = shader != null && canvas != null && canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceCamera;
            var glass = glassOk ? Glass(shader) : null;
            panelImg = Panel(canvas.transform, "Tra cứu hóa chất", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(980, 640), glass, 1.2f);
            if (!glassOk && !ReducedTransparency)
            {
                panelImg.sprite = Solid;
                panelImg.pixelsPerUnitMultiplier = UiTheme.IsVera ? 1.2f : 2.8f;
                panelImg.color = Surface;
            }
            panelImg.raycastTarget = true;
            panel = panelImg.gameObject;
            var p = panel.transform;
            titleLabel = Label(p, "Tiêu đề", "Tra cứu hóa chất", 24, Ink, true, TextAnchor.UpperLeft, new Vector2(28, -22), new Vector2(500, 34));
            countText = Label(p, "Số kết quả", "", 15, Dim, false, TextAnchor.UpperLeft, new Vector2(30, -58), new Vector2(500, 22));
            string closeLabel = PadInput.UsingTouch ? "Đóng ✕" : "Đóng  Esc";
            closeBtn = MakeButton(p, "Đóng", closeLabel, new Vector2(-130, -20), new Vector2(120, 50), Close, false, 15, new Vector2(1, 1));
            input = MakeInput(p, "Ô tìm kiếm", "Tên, công thức, nhóm... (ví dụ: dong sunfat, KMnO4, axit)", new Vector2(28, -88), new Vector2(520, 48));
            for (int i = 0; i < Rows; i++)
            {
                int k = i;
                var b = MakeButton(p, "Kết quả", "", new Vector2(28, -146 - i * 54), new Vector2(520, 50), () => Select(k), false, 15);
                var t = b.GetComponentInChildren<Text>();
                t.alignment = TextAnchor.UpperLeft;
                t.rectTransform.anchoredPosition = new Vector2(14, -6);
                t.rectTransform.sizeDelta = new Vector2(490, 22);
                t.font = Semibold;
                var info = Label(b.transform, "Vị trí", "", 13, Dim, false, TextAnchor.UpperLeft, new Vector2(14, -27), new Vector2(490, 20));
                rows.Add((b, t, info));
            }
            detail = Rect(p, "Chi tiết", new Vector2(0, 1), new Vector2(0, 1), new Vector2(574, -88), new Vector2(380, 520)).gameObject;
            var d = detail.transform;
            detailTitle = Label(d, "Tên chất", "", 22, Ink, true, TextAnchor.UpperLeft, Vector2.zero, new Vector2(370, 60));
            detailInfo = Label(d, "Thông tin", "", 15, Dim, false, TextAnchor.UpperLeft, new Vector2(0, -70), new Vector2(370, 170));
            detailLocation = Label(d, "Vị trí chi tiết", "", 17, Accent, true, TextAnchor.UpperLeft, new Vector2(0, -250), new Vector2(370, 60));
            markBtn = MakeButton(d, "Chỉ vị trí", "Chỉ vị trí trên kệ", new Vector2(0, -330), new Vector2(370, 50), () => Mark(selected, true), true);
            gotoBtn = MakeButton(d, "Đi tới kệ", "Đi tới kệ", new Vector2(0, -388), new Vector2(180, 50), () => GoTo(selected));
            bringBtn = MakeButton(d, "Robot mang ra bàn", "Robot mang ra bàn", new Vector2(190, -388), new Vector2(180, 50), () => Bring(selected));
            detail.SetActive(false);
            panel.SetActive(false);

            marker = gameObject.AddComponent<HoverOutline>();
            marker.outlineShader = outlineShader != null ? outlineShader : Shader.Find("Khkt/VienSang");
            marker.color = new Color(1f, 0.72f, 0.2f, 1f);
            marker.widthPx = 6f;
            var bm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bm.name = "Cột sáng vị trí";
            Destroy(bm.GetComponent<Collider>());
            if (beamMaterial != null) bm.GetComponent<Renderer>().sharedMaterial = beamMaterial;
            bm.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            beam = bm.transform;
            beam.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            SearchKiosk.OpenRequested -= Open;
            UiTheme.Changed -= OnThemeChanged;
        }

        void OnThemeChanged()
        {
            if (panelImg != null)
            {
                var shader = Shader.Find("Khkt/KinhMo") ?? (ui != null ? ui.glassShader : null);
                var canvas = ui != null ? ui.Canvas : null;
                glassOk = shader != null && canvas != null && canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceCamera;
                if (glassOk && !ReducedTransparency)
                {
                    panelImg.sprite = Rounded;
                    panelImg.material = Glass(shader);
                    panelImg.pixelsPerUnitMultiplier = 1.2f;
                    panelImg.color = Color.white;
                }
                else
                {
                    panelImg.material = null;
                    panelImg.sprite = Solid;
                    panelImg.pixelsPerUnitMultiplier = UiTheme.IsVera ? 1.2f : 2.8f;
                    panelImg.color = Surface;
                }
            }
            if (titleLabel != null) titleLabel.color = Ink;
            if (countText != null) countText.color = Dim;
            if (detailTitle != null) detailTitle.color = Ink;
            if (detailInfo != null) detailInfo.color = Dim;
            if (detailLocation != null) detailLocation.color = Accent;
            RefreshInputTheme(input);
            RefreshButtonTheme(closeBtn, false);
            RefreshButtonTheme(markBtn, true);
            RefreshButtonTheme(gotoBtn, false);
            RefreshButtonTheme(bringBtn, false);
            for (int k = 0; k < rows.Count; k++)
            {
                SetChip(rows[k].b, k == selectedIndex);
                RefreshButtonTheme(rows[k].b, false, keepChipStyle: true);
                rows[k].name.font = Semibold;
                rows[k].info.color = Dim;
            }
            if (selectedIndex >= 0 && selectedIndex < results.Count) Select(selectedIndex);
        }

        static void RefreshInputTheme(InputField f)
        {
            if (f == null) return;
            var img = f.GetComponent<Image>();
            if (img != null) { img.sprite = Solid; img.pixelsPerUnitMultiplier = UiTheme.IsVera ? 2.2f : 3f; img.color = Well; }
            if (f.textComponent != null) f.textComponent.color = Ink;
            if (f.placeholder is Text ph) ph.color = Muted;
            f.caretColor = Accent;
            f.selectionColor = new Color(Accent.r, Accent.g, Accent.b, 0.30f);
            var border = f.transform.Find("Viền")?.GetComponent<Image>();
            if (border != null && border.sprite == null) border.enabled = false;
        }

        static void RefreshButtonTheme(Button b, bool primary, bool keepChipStyle = false)
        {
            if (b == null) return;
            var img = b.targetGraphic as Image;
            if (img != null)
            {
                img.sprite = Solid;
                img.pixelsPerUnitMultiplier = UiTheme.IsVera ? 2.2f : 3f;
                if (!keepChipStyle) img.color = primary ? (UiTheme.IsVera ? AccentFill : Accent) : ButtonFill;
            }
            var border = b.transform.Find("Viền")?.GetComponent<Image>();
            if (border != null && border.sprite == null) border.enabled = false;
            if (!keepChipStyle)
            {
                var t = b.transform.Find("Chữ")?.GetComponent<Text>();
                if (t != null) t.color = primary ? OnAccent : Ink;
            }
        }

        Dictionary<int, ChemicalBottle> Bottles()
        {
            if (bottles != null) return bottles;
            bottles = new Dictionary<int, ChemicalBottle>();
            foreach (var b in Khkt.Rendering.ZoneRoot.FindAll<ChemicalBottle>())
            {
                // ưu tiên lọ trong phòng hóa chất (lọ ở bàn học sinh là bản sao)
                if (!bottles.ContainsKey(b.stt) || b.transform.position.x > 20f) bottles[b.stt] = b;
            }
            return bottles;
        }

        public ChemicalBottle FindBottle(int stt) => Bottles().TryGetValue(stt, out var b) ? b : null;

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            panel.SetActive(true);
            UiBridge.SetCapture(ref captured, true);
            input.text = "";
            lastQuery = null;
            input.ActivateInputField();
            Refresh();
        }

        public void Close()
        {
            IsOpen = false;
            panel.SetActive(false);
            UiBridge.SetCapture(ref captured, false);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (!IsOpen && kb.bKey.wasPressedThisFrame && DesktopViewer.UiCapture == 0) Open();
                else if (IsOpen && kb.escapeKey.wasPressedThisFrame) Close();
                else if (IsOpen && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) && results.Count > 0) { Select(0); Mark(selected, true); }
            }
            if (IsOpen && input.text != lastQuery) Refresh();

            // Cột sáng và viền chỉ chỗ lọ hóa chất
            if (Marked != null && Time.time < markUntil && !Marked.GetComponent<Grabbable>().IsHeld)
            {
                beam.gameObject.SetActive(true);
                float pulse = 1f + 0.15f * Mathf.Sin(Time.time * 6f);
                Vector3 top = Marked.transform.position;
                beam.position = top + Vector3.up * 0.65f;
                beam.localScale = new Vector3(0.06f * pulse, 0.6f, 0.06f * pulse);
                marker.SetTarget(Marked.transform);
            }
            else
            {
                beam.gameObject.SetActive(false);
                marker.SetTarget(null);
                Marked = null;
            }
        }

        void Refresh()
        {
            lastQuery = input.text;
            var cat = ChemicalCatalog.Instance;
            results = cat != null ? cat.Search(lastQuery, Rows) : new List<ChemEntry>();
            if (string.IsNullOrWhiteSpace(lastQuery) && cat != null)
            {
                results.Clear();
                foreach (var s in new[] { 98, 124, 253, 230, 338, 468, 578, 394 }) if (cat.Get(s) != null) results.Add(cat.Get(s));
                countText.text = $"{cat.Chemicals.Length} hóa chất · {cat.Shelves.Length} kệ · gợi ý hay dùng";
            }
            else countText.text = results.Count == 0 ? "Không tìm thấy, thử gõ không dấu hoặc công thức" : $"{results.Count} kết quả";
            for (int i = 0; i < rows.Count; i++)
            {
                bool on = i < results.Count;
                rows[i].b.gameObject.SetActive(on);
                if (!on) continue;
                var e = results[i];
                rows[i].name.text = e.ten + (string.IsNullOrEmpty(e.cong_thuc) ? "" : "   " + e.Formula);
                rows[i].info.text = $"{e.ma_vi_tri}  ·  {cat.Group(e.nhom)?.ten}";
            }
            if (results.Count > 0 && (selected == null || !results.Contains(selected))) Select(0);
            if (results.Count == 0) detail.SetActive(false);
        }

        void Select(int i)
        {
            if (i < 0 || i >= results.Count) return;
            selectedIndex = i;
            selected = results[i];
            var e = selected;
            var cat = ChemicalCatalog.Instance;
            detail.SetActive(true);
            detailTitle.text = e.ten + (string.IsNullOrEmpty(e.cong_thuc) ? "" : "\n" + e.Formula);
            string hz = ChemicalCatalog.HazardText(e);
            detailInfo.text = $"{e.phan_loai} · {ChemicalCatalog.StateText(e)}\nNồng độ: {ChemicalCatalog.ConcText(e)}" + (e.M > 0 ? $"\nM = {LabProcedure.F(e.M, "0.##")} g/mol" : "")
                              + (hz.Length > 0 ? $"\n<color={HexWarn}>⚠ {hz}</color>" : "") + $"\nNhóm bảo quản: {cat.Group(e.nhom)?.ten}";
            detailLocation.text = cat.LocationText(e);
            for (int k = 0; k < rows.Count; k++)
            {
                SetChip(rows[k].b, k == i);
                var border = rows[k].b.transform.Find("Viền")?.GetComponent<Image>();
                if (border != null && border.sprite == null) border.enabled = false;
                rows[k].name.font = Semibold;
                rows[k].info.color = Dim;
            }
        }

        /// <summary>Đánh dấu lọ trên kệ (viền cam + cột sáng) trong 40 giây.</summary>
        public void Mark(ChemEntry e, bool say)
        {
            if (e == null) return;
            var b = FindBottle(e.stt);
            if (b == null) return;
            Marked = b;
            markUntil = Time.time + 40f;
            if (say) LabMessages.Say($"{e.ten}: {ChemicalCatalog.Instance.LocationText(e)}", LabMessages.Tone.Good);
        }

        public void GoTo(ChemEntry e)
        {
            if (e == null) return;
            var b = FindBottle(e.stt);
            if (b == null) return;
            Mark(e, true);
            Close();
            // đứng trước mặt kệ (mặt trước kệ là -x cục bộ của kệ)
            Transform unit = b.transform.parent;
            Vector3 front = unit != null ? -unit.right : Vector3.left;
            viewer.GoNear(b.transform.position, front, 1.6f);
        }

        void Bring(ChemEntry e)
        {
            if (e == null) return;
            Mark(e, false);
            Close();
            var robot = LabRobot.Instance;
            if (robot != null) robot.BringChemical(e.stt);
            else LabMessages.Say("Chưa có robot trợ lý trong cảnh", LabMessages.Tone.Warn);
        }
    }
}
