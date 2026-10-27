using System.Collections.Generic;
using System.Linq;
using Khkt.Demo;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using static Khkt.Demo.GlassKit;

namespace Khkt.Assembly
{
    /// <summary>
    /// Giao diện khu chế tạo (vẽ đè lên camera xưởng): bảng bộ phận theo nhóm có ảnh nhỏ (trái), bảng kiểm tra bộ lắp (phải),
    /// thẻ thông tin bộ phận đang trỏ, dòng thông báo (trên), nút Hoàn tác / Xóa hết / Gia công và gợi ý phím (dưới).
    /// </summary>
    public class FabricationUI : MonoBehaviour
    {
        FabricationWorkspace ws;
        Canvas canvas;
        public Canvas Canvas => canvas;
        public Camera UiCamera { get; private set; }
        const int UiLayer = 5;  // lớp "UI" mặc định của Unity
        GameObject rootGo;
        Text message, checkText, infoTitle, infoBody, groupTitle;
        Image messageDot;
        readonly List<Button> tabs = new List<Button>();
        readonly List<(Button b, RawImage img, Text name, Text loading)> cells = new List<(Button, RawImage, Text, Text)>();
        readonly List<PartLibrary.Entry> shown = new List<PartLibrary.Entry>();
        PartLibrary.Entry paletteHover;
        GameObject pickPanel, progressPanel;
        Text pickTitle, pickSub, progressText;
        Image progressFill;
        readonly List<(Button b, Text t)> choices = new List<(Button, Text)>();
        readonly List<System.Action> choiceActions = new List<System.Action>();
        int group;
        bool dirty = true;
        float nextRefresh;

        static readonly Color PanelColor = new Color(0.05f, 0.08f, 0.13f, 0.88f);
        static readonly Color CellColor = new Color(1f, 1f, 1f, 0.07f);
        const int Columns = 3, MaxCells = 12;

        public void Init(FabricationWorkspace w, Shader glassShader)
        {
            ws = w;
            ws.Changed += () => dirty = true;
            var cgo = new GameObject("Giao diện khu chế tạo", typeof(Canvas), typeof(CanvasScaler));
            cgo.transform.SetParent(transform, false);
            canvas = cgo.GetComponent<Canvas>();
            // vẽ bằng camera phủ (overlay, không hậu kỳ) chồng lên camera xưởng: chữ nét, màu bảng đúng thiết kế,
            // và chụp ảnh màn hình (kiểm thử) bằng camera xưởng là có cả giao diện
            UiCamera = new GameObject("Camera giao diện chế tạo").AddComponent<Camera>();
            UiCamera.transform.SetParent(ws.workCamera.transform, false);
            UiCamera.cullingMask = 1 << UiLayer;
            UiCamera.nearClipPlane = 0.1f;
            UiCamera.farClipPlane = 2f;
            var uiData = UiCamera.GetUniversalAdditionalCameraData();
            uiData.renderType = CameraRenderType.Overlay;
            uiData.renderPostProcessing = false;
            ws.workCamera.GetUniversalAdditionalCameraData().cameraStack.Add(UiCamera);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = UiCamera;
            canvas.planeDistance = 1f;
            var scaler = cgo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            UiBridge.EnsureInteractive(canvas);
            rootGo = cgo;
            var root = cgo.transform;

            // ---------- Thanh trên: tên khu, thông báo, thoát ----------
            var top = Box(root, "Thanh trên", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(1888, 58));
            Label(top, "Tiêu đề", "KHU CHẾ TẠO DỤNG CỤ", 22, Accent, true, TextAnchor.MiddleLeft, new Vector2(22, 0), new Vector2(340, 58));
            var dot = Rect(top, "Chấm", new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(392, 0), new Vector2(10, 10));
            messageDot = dot.gameObject.AddComponent<Image>();
            messageDot.sprite = SolidCircle;
            messageDot.raycastTarget = false;
            message = Label(top, "Thông báo", "", 17, Ink, false, TextAnchor.MiddleLeft, new Vector2(408, 0), new Vector2(1300, 58));
            MakeButton(top, "Thoát", "Thoát  Esc", new Vector2(-14, -11), new Vector2(128, 36), () => ws.Close(), false, 15, new Vector2(1, 1))
                .GetComponent<RectTransform>().pivot = new Vector2(1, 1);

            // ---------- Bảng bộ phận (trái) ----------
            var left = Box(root, "Bảng bộ phận", new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -82), new Vector2(376, 790));
            Label(left, "Tên bảng", "Bộ phận", 20, Ink, true, TextAnchor.UpperLeft, new Vector2(18, -14), new Vector2(200, 30));
            for (int i = 0; i < PartLibrary.Groups.Length; i++)
            {
                int k = i;
                var b = MakeButton(left, "Thẻ nhóm", PartLibrary.Groups[i], new Vector2(14 + (i % 3) * 118, -52 - (i / 3) * 42), new Vector2(112, 36), () => SetGroup(k), false, 14);
                tabs.Add(b);
            }
            groupTitle = Label(left, "Nhóm", "", 15, Dim, false, TextAnchor.UpperLeft, new Vector2(18, -146), new Vector2(340, 22));
            for (int i = 0; i < MaxCells; i++)
            {
                int k = i;
                var b = MakeButton(left, "Ô bộ phận", "", new Vector2(14 + (i % Columns) * 118, -174 - (i / Columns) * 152), new Vector2(112, 146), () => Take(k), false, 13);
                var t = b.GetComponentInChildren<Text>();
                t.alignment = TextAnchor.LowerCenter;
                t.rectTransform.sizeDelta = new Vector2(104, 140);
                t.rectTransform.anchoredPosition = new Vector2(4, -2);
                t.fontSize = 13;
                b.GetComponent<Image>().color = CellColor;
                var imgRt = Rect(b.transform, "Ảnh", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(100, 100));
                var img = imgRt.gameObject.AddComponent<RawImage>();
                img.raycastTarget = false;
                var loading = Label(imgRt, "Đang chụp", "…", 22, Dim, false, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(100, 100));
                var trigger = b.gameObject.AddComponent<EventTrigger>();
                var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                enter.callback.AddListener(_ => paletteHover = k < shown.Count ? shown[k] : null);
                var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                exit.callback.AddListener(_ => paletteHover = null);
                trigger.triggers.Add(enter);
                trigger.triggers.Add(exit);
                cells.Add((b, img, t, loading));
            }

            // ---------- Bảng kiểm tra (phải) ----------
            var right = Box(root, "Bảng kiểm tra", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -82), new Vector2(440, 520));
            Label(right, "Tên bảng", "Kiểm tra bộ lắp", 20, Ink, true, TextAnchor.UpperLeft, new Vector2(18, -14), new Vector2(300, 30));
            checkText = Label(right, "Nội dung", "", 15, Ink, false, TextAnchor.UpperLeft, new Vector2(18, -52), new Vector2(406, 460));
            checkText.verticalOverflow = VerticalWrapMode.Truncate;

            // ---------- Thẻ thông tin ----------
            var info = Box(root, "Thẻ thông tin", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -612), new Vector2(440, 260));
            infoTitle = Label(info, "Tên", "", 18, Accent, true, TextAnchor.UpperLeft, new Vector2(18, -14), new Vector2(406, 28));
            infoBody = Label(info, "Nội dung", "", 14, Ink, false, TextAnchor.UpperLeft, new Vector2(18, -46), new Vector2(406, 206));
            infoBody.verticalOverflow = VerticalWrapMode.Truncate;

            // ---------- Thanh dưới: nút + gợi ý ----------
            var bottom = Box(root, "Thanh dưới", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(1080, 104));
            Label(bottom, "Gợi ý phím",
                  "Chuột trái: lấy / lắp / đặt   ·   Giữ chuột phải: xoay nhìn   ·   Giữ chuột giữa: kéo   ·   Lăn chuột: phóng to (đang cầm: xoay)   ·   R E: xoay   ·   Delete: bỏ",
                  14, Dim, false, TextAnchor.MiddleCenter, new Vector2(0, -8), new Vector2(1080, 26));
            MakeButton(bottom, "Lấy bộ về sửa", "Lấy bộ về sửa", new Vector2(150, -44), new Vector2(180, 46), ShowRecall, false, 16);
            MakeButton(bottom, "Hoàn tác", "Hoàn tác  Ctrl+Z", new Vector2(342, -44), new Vector2(180, 46), () => ws.Undo(), false, 16);
            MakeButton(bottom, "Xóa hết", "Xóa hết", new Vector2(534, -44), new Vector2(140, 46), () => ws.ClearAll(), false, 16);
            MakeButton(bottom, "Gia công", "Gia công  ▶", new Vector2(686, -44), new Vector2(220, 46), () => ws.RequestFabricate(), true, 18);

            // ---------- Bảng chọn (bàn đích / bộ cần sửa) và tiến độ gia công ----------
            var pick = Box(root, "Bảng chọn", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(700, 640));
            pickPanel = pick.gameObject;
            pickTitle = Label(pick, "Tiêu đề", "", 22, Ink, true, TextAnchor.UpperLeft, new Vector2(26, -20), new Vector2(560, 32));
            pickSub = Label(pick, "Mô tả", "", 15, Dim, false, TextAnchor.UpperLeft, new Vector2(26, -58), new Vector2(650, 44));
            MakeButton(pick, "Hủy", "Hủy", new Vector2(-24, -18), new Vector2(100, 36), () => pickPanel.SetActive(false), false, 15, new Vector2(1, 1))
                .GetComponent<RectTransform>().pivot = new Vector2(1, 1);
            for (int i = 0; i < 18; i++)
            {
                int k = i;
                var b = MakeButton(pick, "Lựa chọn", "", new Vector2(26 + (i % 2) * 330, -112 - (i / 2) * 56), new Vector2(318, 48), () => Choose(k), false, 14);
                var tx = b.GetComponentInChildren<Text>();
                tx.alignment = TextAnchor.MiddleLeft;
                tx.rectTransform.anchoredPosition = new Vector2(14, 0);
                tx.rectTransform.sizeDelta = new Vector2(296, 48);
                choices.Add((b, tx));
            }
            pickPanel.SetActive(false);
            var prog = Box(root, "Tiến độ gia công", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(560, 120));
            progressPanel = prog.gameObject;
            progressText = Label(prog, "Chữ", "Máy đang gia công…", 20, Ink, true, TextAnchor.UpperCenter, new Vector2(0, -20), new Vector2(560, 32));
            var track = Rect(prog, "Rãnh", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 28), new Vector2(480, 14));
            var trackImg = track.gameObject.AddComponent<Image>();
            trackImg.sprite = Solid;
            trackImg.type = Image.Type.Sliced;
            trackImg.color = new Color(1, 1, 1, 0.12f);
            var fill = Rect(track, "Thanh", new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(0, 14));
            progressFill = fill.gameObject.AddComponent<Image>();
            progressFill.sprite = Solid;
            progressFill.type = Image.Type.Sliced;
            progressFill.color = Accent;
            progressPanel.SetActive(false);
            ws.FabricateRequested += ShowDestinations;

            SetGroup(0);
            foreach (var tr in rootGo.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = UiLayer;
            rootGo.SetActive(false);
        }

        RectTransform Box(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var rt = Rect(parent, name, anchor, pivot, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Solid;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1.6f;
            img.color = PanelColor;
            img.raycastTarget = true;  // chặn bấm xuyên xuống bàn ảo
            return rt;
        }

        public void SetVisible(bool v)
        {
            rootGo.SetActive(v);
            dirty = true;
        }

        void SetGroup(int g)
        {
            group = g;
            for (int i = 0; i < tabs.Count; i++)
                tabs[i].GetComponent<Image>().color = i == g ? new Color(Accent.r, Accent.g, Accent.b, 0.55f) : new Color(1, 1, 1, 0.13f);
            shown.Clear();
            if (ws.library != null) shown.AddRange(ws.library.InGroup(PartLibrary.Groups[g]).Take(MaxCells));
            groupTitle.text = $"{PartLibrary.Groups[g]} · {shown.Count} loại · bấm để lấy";
            for (int i = 0; i < cells.Count; i++)
            {
                var c = cells[i];
                bool on = i < shown.Count;
                c.b.gameObject.SetActive(on);
                if (!on) continue;
                c.name.text = shown[i].name;
                c.img.texture = shown[i].thumbnail;
                c.img.enabled = shown[i].thumbnail != null;
                c.loading.enabled = shown[i].thumbnail == null;
            }
        }

        public void ThumbnailReady(PartLibrary.Entry e)
        {
            int i = shown.IndexOf(e);
            if (i < 0) return;
            cells[i].img.texture = e.thumbnail;
            cells[i].img.enabled = true;
            cells[i].loading.enabled = false;
        }

        void Choose(int i)
        {
            if (i < choiceActions.Count) choiceActions[i]?.Invoke();
        }

        void FillChoices(List<(string text, bool enabled, System.Action act)> items)
        {
            choiceActions.Clear();
            for (int i = 0; i < choices.Count; i++)
            {
                var (b, tx) = choices[i];
                bool on = i < items.Count;
                b.gameObject.SetActive(on);
                if (!on) continue;
                tx.text = items[i].text;
                b.interactable = items[i].enabled;
                tx.color = items[i].enabled ? Ink : new Color(Dim.r, Dim.g, Dim.b, 0.45f);
                var act = items[i].act;
                choiceActions.Add(() => { pickPanel.SetActive(false); act(); });
            }
        }

        /// <summary>Bảng chọn bàn đích: bàn còn đủ chỗ cho bộ đang lắp thì bấm được, hết chỗ thì mờ.</summary>
        public void ShowDestinations()
        {
            ws.DesignBounds(out var b);
            var dests = FabricationDestination.All.Where(d => d != null && d.isActiveAndEnabled).OrderBy(d => d.order).ToList();
            var items = new List<(string, bool, System.Action)>();
            foreach (var d in dests)
            {
                bool room = ws.HasRoom(d);
                var dd = d;
                items.Add(($"{(d.isDefault ? "★ " : "")}{d.label}  ·  {(room ? "<color=#7ef0a0>đủ chỗ</color>" : "hết chỗ")}", room, () => ws.Fabricate(dd)));
            }
            pickTitle.text = "Gia công: chọn bàn đích";
            pickSub.text = $"Bộ đang lắp: {ws.Parts().Count} bộ phận, chiếm {b.size.x * 100:0} × {b.size.z * 100:0} cm, cao {(b.max.y - ws.tableTop) * 100:0} cm. "
                         + "Máy tìm chỗ trống trên bàn, mặt trước bộ quay về phía người làm thí nghiệm.";
            FillChoices(items);
            pickPanel.SetActive(true);
        }

        /// <summary>Bảng chọn bộ đã gia công để lấy về sửa.</summary>
        public void ShowRecall()
        {
            var sets = ws.LiveSets();
            var items = new List<(string, bool, System.Action)>();
            foreach (var s in sets)
            {
                var ss = s;
                items.Add(($"{s.name}\n<color=#9fb4c8>{s.where}</color>", true, () =>
                {
                    string err = ws.Recall(ss);
                    if (err != null) ws.Say(err, true);
                }));
            }
            pickTitle.text = "Lấy bộ về sửa";
            pickSub.text = sets.Count == 0 ? "Chưa có bộ dụng cụ nào đã gia công trong phòng."
                                           : "Chọn bộ cần sửa: bộ được đưa về bàn ảo đúng như lúc gia công (phải đổ hết dung dịch trước).";
            FillChoices(items);
            pickPanel.SetActive(true);
        }

        void Take(int i)
        {
            if (i < shown.Count) ws.SpawnHeld(shown[i].id);
        }

        void Update()
        {
            if (ws == null || !ws.IsOpen) return;
            if (progressPanel.activeSelf != ws.Busy) progressPanel.SetActive(ws.Busy);
            if (ws.Busy && pickPanel.activeSelf) pickPanel.SetActive(false);
            if (ws.Busy)
            {
                progressFill.rectTransform.sizeDelta = new Vector2(480 * ws.Progress, 14);
                progressText.text = $"Máy đang gia công… {ws.Progress * 100:0}%";
            }
            float age = Time.time - ws.MessageTime;
            message.text = ws.Message;
            message.color = new Color(Ink.r, Ink.g, Ink.b, age < 8f ? 1f : 0.55f);
            messageDot.color = ws.MessageWarn ? Warn : Good;

            if (dirty || Time.time > nextRefresh)
            {
                dirty = false;
                nextRefresh = Time.time + 0.5f;
                checkText.text = ws.Report();
            }

            // thẻ thông tin: ô bộ phận đang trỏ, không thì bộ phận đang cầm / đang trỏ trên bàn
            if (paletteHover != null)
            {
                var info = paletteHover.template != null ? paletteHover.template.GetComponentInChildren<EquipmentInfo>(true) : null;
                ShowInfo(paletteHover.name, info);
            }
            else
            {
                var p = ws.Held != null ? ws.Held : ws.Hover;
                if (p != null)
                {
                    string head = p.partName;
                    if (ws.Held != null && ws.AttachTarget != null) head += $"  →  {ws.AttachTarget.Part.partName}";
                    var info = p.GetComponent<EquipmentInfo>();
                    ShowInfo(head, info, ws.Held != null && ws.AttachTarget != null
                        ? $"Bấm để lắp: {ws.AttachMine.Describe()} ↔ {ws.AttachTarget.Describe()} ({ws.AttachTarget.label})" : null);
                }
                else ShowInfo("Trỏ vào một bộ phận", null, "Rê chuột lên ô ở bảng bên trái hoặc lên bộ phận trên bàn để xem công dụng và lưu ý khi lắp.");
            }
        }

        void ShowInfo(string title, EquipmentInfo info, string extra = null)
        {
            infoTitle.text = title;
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(extra)) parts.Add($"<color=#7ef0a0>{extra}</color>");
            if (info != null && !string.IsNullOrEmpty(info.description)) parts.Add(info.description);
            if (info != null && !string.IsNullOrEmpty(info.usage)) parts.Add($"<color=#ffc96b>Lưu ý:</color> {info.usage}");
            infoBody.text = string.Join("\n\n", parts);
        }
    }
}
