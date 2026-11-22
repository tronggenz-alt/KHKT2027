using System.Collections.Generic;
using Khkt.Input;
using Khkt.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static Khkt.Demo.GlassKit;

namespace Khkt.Demo
{
    /// <summary>
    /// Điều khiển phòng lab bằng thao tác bàn tay (nhãn thao tác do tools/tay_tracker.py gửi kèm 21 điểm):
    ///  • Nắm dọc: cầm vật cao (buret, bình khí, ống đong, bộ lắp ráp).
    ///  • Nắm ngang: cầm lọ, cốc, bình.
    ///  • Chụm ngón cái – ngón trỏ: cầm dụng cụ nhỏ (pipet, ống nhỏ giọt, giấy thử...); trong bảng giao diện là bấm chuột.
    ///  • Giơ ngón út: bật / dùng máy; đang cầm dụng cụ thì là dùng (rót, nhỏ giọt, hút).
    ///  • Ngón trỏ: con trỏ (gốc ngón trỏ, không rung khi chụm).
    ///  • Xòe tay: đặt vật xuống / hủy menu.
    /// Gần chỗ trỏ có nhiều vật cùng loại thì hiện menu: di tay để chọn, chụm ngón trỏ – cái để lấy.
    /// </summary>
    [DefaultExecutionOrder(-50)]   // trước HandController: đặt tia trỏ và nút giữ trước khi nó đọc
    public class GestureControl : MonoBehaviour
    {
        public enum Cat { Small, Bottle, Tall }

        public static GestureControl Instance { get; private set; }
        /// <summary>Đang điều khiển bằng thao tác tay (có tracker Python gửi điểm ảnh, đang thấy tay).</summary>
        public static bool Owning => Instance != null && Instance.owning;

        public Camera viewCamera;
        public HandController hand;
        public float pickRadius = 0.28f;
        const int MaxItems = 40, Rows = 8;

        bool owning;
        public int MenuCount => menu.Count;
        /// <summary>Việc ngón út vừa kích hoạt (để kiểm thử và hiển thị).</summary>
        public string LastAction { get; private set; } = "";
        public string Gesture => gesture;
        ArmTracking.Side side;
        string gesture = "", prevGesture = "";
        float gestureSince, lostAt = -10f;
        Vector2 pointer, pointerVel;
        bool pointerReady;
        int pulse;
        bool uiMouseDown;

        // menu chọn: vật cùng loại trên bàn, hoặc bàn để đi tới
        class Entry
        {
            public Grabbable g;
            public Khkt.Environment.LabStation st;
            public IDemoUsable use;
            public bool travel;
            public string name, sub;
        }
        readonly List<Entry> menu = new List<Entry>();
        /// <summary>Khu bàn đang đứng (null: đi lại tự do, tương tác như cũ quanh chỗ trỏ).</summary>
        public BenchZone Zone { get; private set; }
        public bool MenuIsTravel => menu.Count > 0 && menu[0].st != null;
        int menuSel;
        float menuOpenedAt;
        bool menuArmed;

        readonly Dictionary<Grabbable, Cat> catCache = new Dictionary<Grabbable, Cat>();
        readonly Collider[] overlap = new Collider[96];

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            if (hand == null) hand = HandController.Active != null ? HandController.Active : FindAnyObjectByType<HandController>();
            BuildUi();
        }

        // =====================================================================
        // Mỗi khung
        // =====================================================================

        void Update()
        {
            var at = ArmTracking.Instance;
            var s = PickSide(at);
            float now = Time.unscaledTime;
            bool want = s != null && hand != null && viewCamera != null && (HandTrackerProcess.Instance == null || HandTrackerProcess.Instance.Enabled);
            if (!want)
            {
                if (owning && now - lostAt > 0.6f) Release(false);
                if (owning && lostAt < 0) lostAt = now;
                if (!owning) { HideUi(); return; }
                // mất tay chốc lát: giữ nguyên (vật đang cầm không rơi), chờ thấy lại
                if (now - lostAt <= 0.6f) { UpdateUi(); return; }
                return;
            }
            lostAt = -1f;
            if (!owning)
            {
                owning = true;
                pointerReady = false;
                if (!shownTip)
                {
                    shownTip = true;
                    LabMessages.Say("Điều khiển bằng tay: nắm tay để mở danh sách, ngón trỏ trượt chọn, chụm 2 ngón để lấy (bảng cử chỉ ở góc trái)", LabMessages.Tone.Info);
                }
            }
            side = s;

            // ----- thao tác -----
            string g = s.gesture ?? "";
            bool wasClosed = Closed(gesture);
            if (g != gesture) { prevGesture = gesture; gesture = g; gestureSince = now; fired = false; }
            float held = now - gestureSince;
            // cử chỉ phải giữ ổn định một chút mới làm việc, để tư thế thoáng qua lúc đổi tay không kích hoạt nhầm
            bool entered = !fired && held >= StableSec;
            if (entered) fired = true;

            // ----- con trỏ: đầu ngón trỏ. Lúc chụm / nắm / giơ ngón út, đầu ngón trỏ gập lại nên chốt độ lệch
            // đầu ngón - gốc ngón của 0,15 giây trước rồi bám theo gốc ngón: con trỏ đứng đúng chỗ vừa trỏ, không giật
            Vector2 tip = s.image[8], knuckle = s.image[5];
            tipHistory.Enqueue((now, tip - knuckle));
            while (tipHistory.Count > 0 && now - tipHistory.Peek().t > 0.4f) tipHistory.Dequeue();
            bool closed = Closed(gesture);
            if (closed && !wasClosed)
            {
                lockOffset = Vector2.zero;
                foreach (var h in tipHistory) if (now - h.t >= 0.15f) lockOffset = h.off;   // mẫu mới nhất cách đây ≥ 0,15 s
                if (lockOffset == Vector2.zero && tipHistory.Count > 0) lockOffset = tipHistory.Peek().off;
            }
            Vector2 m = closed ? knuckle + lockOffset : tip;
            var target = new Vector2(Mathf.InverseLerp(0.12f, 0.88f, m.x) * viewCamera.pixelWidth,
                                     (1f - Mathf.InverseLerp(0.08f, 0.80f, m.y)) * viewCamera.pixelHeight);
            if (!pointerReady) { pointer = rawPrev = pointerSmooth = target; pointerReady = true; pointerVel = Vector2.zero; }
            else
            {
                // 1€ trên điểm ảnh màn hình: đứng yên lọc mạnh (1 Hz), đi nhanh bám sát; vùng chết mềm 2,5 px bỏ rung nhỏ
                float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-3f);
                pointerVel = Vector2.Lerp(pointerVel, (target - rawPrev) / dt, Alpha(1f, dt));
                rawPrev = target;
                float cutoff = 1.0f + 0.006f * pointerVel.magnitude;
                pointerSmooth = Vector2.Lerp(pointerSmooth == Vector2.zero ? target : pointerSmooth, target, Alpha(cutoff, dt));
                Vector2 d = pointerSmooth - pointer;
                float dm = d.magnitude;
                if (dm > 2.5f) pointer += d * ((dm - 2.5f) / dm);
            }
            Ray ray = viewCamera.ScreenPointToRay(pointer);
            Zone = BenchZones.Current(viewCamera.transform.position);

            bool uiOpen = Khkt.Environment.DesktopViewer.UiCapture > 0;
            hand.OverrideAimPoint = uiOpen ? (Vector3?)null : ray.GetPoint(1.2f);
            bool hold = false;

            if (uiOpen)
            {
                CloseMenu();
                // ngón trỏ là chuột: con trỏ hệ thống theo tay, chụm là giữ chuột trái (kéo thả được)
                var mouse = Mouse.current;
                if (mouse != null && (mouse.position.ReadValue() - pointer).sqrMagnitude > 0.25f) mouse.WarpCursorPosition(pointer);
                bool down = gesture == "chum";
                if (down != uiMouseDown) { uiMouseDown = down; OsMouse.Button(down); }
            }
            else
            {
                if (uiMouseDown) { uiMouseDown = false; OsMouse.Button(false); }
                if (menu.Count > 0) UpdateMenu(entered, held, now);
                else if (hand.Held == null)
                {
                    // nắm ngang: danh sách dụng cụ, nắm dọc: danh sách hóa chất (trượt ra ở cạnh màn hình)
                    if (entered && gesture == "nam_ngang") OpenList(ListKind.Tool, ray);
                    else if (entered && gesture == "nam_doc") OpenList(ListKind.Chemical, ray);
                    else if (entered && gesture == "ut") Activate(ray);
                    else if (entered && gesture == "chum") Flash("Nắm tay để mở danh sách: nắm ngang là dụng cụ, nắm dọc là hóa chất");
                }
                else
                {
                    // đang cầm: xòe tay là đặt xuống, giơ ngón út là dùng (rót, nhỏ giọt, hút...)
                    if (gesture == "mo" && held > 0.25f && !releasedThisOpen) { releasedThisOpen = true; Release(true); }
                    var act = hand.Current;
                    if (gesture == "ut" && act != HandController.Act.Place && act != HandController.Act.PlaceSurface) hold = true;
                }
                if (gesture != "mo") releasedThisOpen = false;
            }
            // đang cầm bằng tay thật: vật nằm trong tầm với của cánh tay ảo, bàn tay ảo ôm đúng vật
            if (baseMaxDist < 0) baseMaxDist = hand.maxHandDistance;
            hand.maxHandDistance = hand.Held != null ? Mathf.Min(baseMaxDist, 0.6f) : baseMaxDist;
            if (pulse > 0) { hold = true; pulse--; }
            hand.OverrideHold = hold;
            UpdateUi();
        }

        bool releasedThisOpen, shownTip, fired;
        const float StableSec = 0.12f;
        readonly Queue<(float t, Vector2 off)> tipHistory = new Queue<(float, Vector2)>();
        Vector2 lockOffset, rawPrev, pointerSmooth;
        static bool Closed(string g) => g == "chum" || g == "nam_doc" || g == "nam_ngang" || g == "ut";
        float baseMaxDist = -1f;

        static float Alpha(float cutoff, float dt) => 1f / (1f + 1f / (2f * Mathf.PI * cutoff * dt));

        ArmTracking.Side PickSide(ArmTracking a)
        {
            if (a == null) return null;
            bool r = a.Right.handSeen && a.Right.hasImage, l = a.Left.handSeen && a.Left.hasImage;
            // giữ tay đang điều khiển nếu còn thấy, để con trỏ không nhảy giữa hai tay
            if (side == a.Right && r) return a.Right;
            if (side == a.Left && l) return a.Left;
            return r ? a.Right : l ? a.Left : null;
        }

        void OnDisable() => Release(false);

        /// <summary>Thôi điều khiển (mất tay): trả chuột, tia trỏ, đóng menu.</summary>
        void Release(bool _)
        {
            if (_ ) { PutDown(); return; }
            owning = false;
            CloseMenu();
            if (uiMouseDown) { uiMouseDown = false; OsMouse.Button(false); }
            if (hand != null)
            {
                hand.OverrideAimPoint = null;
                hand.OverrideHold = false;
                if (baseMaxDist > 0) hand.maxHandDistance = baseMaxDist;
            }
            HideUi();
        }

        // =====================================================================
        // Cầm theo loại vật
        // =====================================================================

        public Cat Category(Grabbable g)
        {
            if (catCache.TryGetValue(g, out var c)) return c;
            c = Classify(g);
            catCache[g] = c;
            return c;
        }

        static Cat Classify(Grabbable g)
        {
            if (g.GetComponent<Pipette>() || g.GetComponent<Dropper>() || g.GetComponent<TestPaper>() || g.GetComponent<MeterProbe>()) return Cat.Small;
            switch (g.kind)
            {
                case "pipet": case "lo_nho_giot": case "giay": case "dau_do": case "cuvet": case "dung_cu_sinh": case "ong_nghiem": case "dung_cu":
                    return Cat.Small;
                case "buret": case "binh_khi": case "ong_dong":
                    return Cat.Tall;
                case "coc": case "chai": case "lo_rong": case "binh_non": case "binh_cau": case "binh_tia":
                    return Cat.Bottle;
            }
            var part = g.GetComponent<Khkt.Assembly.ApparatusPart>();
            if (part != null && part.fabricatedSet != 0) return Cat.Tall;
            if (g.GetComponent<ChemicalBottle>()) return Cat.Bottle;
            var b = Bounds(g);
            if (b.size.y > 0.2f) return Cat.Tall;
            if (Mathf.Max(b.size.x, b.size.y, b.size.z) < 0.1f) return Cat.Small;
            return g.GetComponent<LabContainer>() ? Cat.Bottle : Cat.Tall;
        }

        static Bounds Bounds(Grabbable g)
        {
            var rs = g.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(g.transform.position, Vector3.one * 0.05f);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        static string CatName(Cat c) => c == Cat.Small ? "dụng cụ nhỏ" : c == Cat.Bottle ? "lọ, cốc, bình" : "vật cao";
        static string CatGesture(Cat c) => c == Cat.Small ? "chụm ngón cái – ngón trỏ" : c == Cat.Bottle ? "nắm ngang" : "nắm dọc";

        public static string NameOf(Grabbable g)
        {
            var cb = g.GetComponent<ChemicalBottle>();
            if (cb != null) return cb.DisplayName;
            var info = g.GetComponentInParent<EquipmentInfo>();
            if (info != null && !string.IsNullOrEmpty(info.title)) return info.title;
            var lc = g.GetComponent<LabContainer>();
            if (lc != null && !string.IsNullOrEmpty(lc.label)) return lc.label;
            return g.name;
        }

        /// <summary>Vật cầm được gần tia trỏ, xếp theo khoảng cách tới tia. directHit: vật tia chạm thẳng.</summary>
        List<(Grabbable g, float d)> Nearby(Ray ray, out Grabbable directHit)
        {
            directHit = null;
            Vector3 center = ray.GetPoint(1.4f);
            float reach = hand.reach;
            if (Physics.Raycast(ray, out var hit, reach, ~0, QueryTriggerInteraction.Ignore))
            {
                center = hit.point;
                directHit = Khkt.Assembly.ApparatusPart.WorldGrab(hit.collider.GetComponentInParent<Grabbable>());
                if (directHit != null && Zone != null && !Zone.Contains(Bounds(directHit).center)) directHit = null;
            }
            int n = Physics.OverlapSphereNonAlloc(center, pickRadius, overlap, ~0, QueryTriggerInteraction.Collide);
            var seen = new HashSet<Grabbable>();
            var list = new List<(Grabbable, float)>();
            for (int i = 0; i < n; i++)
            {
                var g = Khkt.Assembly.ApparatusPart.WorldGrab(overlap[i].GetComponentInParent<Grabbable>());
                if (g == null || g.IsHeld || !g.isActiveAndEnabled || !seen.Add(g)) continue;
                Vector3 p = Bounds(g).center;
                if ((p - ray.origin).magnitude > reach) continue;
                if (Zone != null && !Zone.Contains(p)) continue;   // chỉ vật trên bàn đang làm
                float d = Vector3.Cross(ray.direction, p - ray.origin).magnitude;   // khoảng cách tới tia
                if (g == directHit) d = 0f;
                list.Add((g, d));
            }
            list.Sort((a, b) => a.Item2.CompareTo(b.Item2));
            return list;
        }

        void TryGrab(Cat cat, Ray ray)
        {
            // đứng ở bàn: xét mọi vật trên cả bàn; đi lại tự do: chỉ quanh chỗ trỏ
            var all = Zone != null ? InZone(ray) : Nearby(ray, out _);
            Grabbable direct = null;
            if (Physics.Raycast(ray, out var dh, hand.reach, ~0, QueryTriggerInteraction.Ignore))
            {
                direct = Khkt.Assembly.ApparatusPart.WorldGrab(dh.collider.GetComponentInParent<Grabbable>());
                if (direct != null && (direct.IsHeld || (Zone != null && !Zone.Contains(Bounds(direct).center)))) direct = null;
                if (direct != null) for (int i = 0; i < all.Count; i++) if (all[i].g == direct) all[i] = (direct, 0f);
                all.Sort((a, b) => a.d.CompareTo(b.d));
            }
            var match = all.FindAll(x => Category(x.g) == cat);
            if (match.Count == 0)
            {
                // cử chỉ quyết định loại vật: không có vật đúng loại thì không cầm, chỉ nhắc cử chỉ đúng của vật đang trỏ
                var hint = direct ?? (all.Count > 0 ? all[0].g : null);
                string where = Zone != null ? "trên " + Zone.Name : "gần tay";
                if (hint != null) Flash($"Không có {CatName(cat)} {where}. {NameOf(hint)}: {CatGesture(Category(hint))}");
                else Flash($"Không có {CatName(cat)} {where}");
                return;
            }
            // trỏ thẳng vào một vật và vật kế bên ở xa tia: cầm ngay; còn lại thì cho chọn
            bool clear = match.Count == 1 || (match[0].d < 0.02f && match[1].d > 0.08f) || match[1].d > match[0].d * 3f + 0.06f;
            if (clear) { PickNow(match[0].g); return; }
            OpenMenu(match.ConvertAll(x => Of(x.g)));
        }

        void UseNow(IDemoUsable u)
        {
            u.Use();
            LastAction = ((Component)u).name;
            Flash(string.IsNullOrEmpty(u.UseHint) ? $"Đã bật {UsableName(u)}" : u.UseHint);
        }

        static string UsableName(IDemoUsable u)
        {
            var c = (Component)u;
            var info = c.GetComponentInParent<EquipmentInfo>();
            return info != null && !string.IsNullOrEmpty(info.title) ? info.title : c.name;
        }

        public enum ListKind { Tool, Chemical }
        /// <summary>Danh sách đang mở là dụng cụ hay hóa chất (null: menu máy / đi bàn).</summary>
        public ListKind? MenuKind { get; private set; }
        public int MenuSel => menuSel;
        public string MenuItemName(int i) => i >= 0 && i < menu.Count ? menu[i].name : "";
        public Grabbable MenuItem(int i) => i >= 0 && i < menu.Count ? menu[i].g : null;

        /// <summary>Hóa chất: lọ hóa chất của kho, chai / lọ / bình khí; còn lại (cốc, bình nón, pipet, buret...) là dụng cụ.</summary>
        public static bool IsChemical(Grabbable g)
        {
            if (g.GetComponent<ChemicalBottle>() != null) return true;
            switch (g.kind) { case "chai": case "lo_nho_giot": case "lo_rong": case "binh_khi": return true; }
            return false;
        }

        void OpenList(ListKind kind, Ray ray)
        {
            var all = Zone != null ? InZone(ray) : Around(ray, 0.8f);
            var items = all.FindAll(x => IsChemical(x.g) == (kind == ListKind.Chemical));
            string what = kind == ListKind.Chemical ? "hóa chất" : "dụng cụ";
            string where = Zone != null ? "trên " + Zone.Name : "quanh đây";
            if (items.Count == 0) { Flash($"Không có {what} {where}"); return; }
            // xếp từ trái sang phải như trên bàn (số trên vật đi lần lượt), chọn sẵn vật đang trỏ gần nhất
            var nearest = items[0].g;
            items.Sort((a, b) => viewCamera.WorldToScreenPoint(Bounds(a.g).center).x.CompareTo(viewCamera.WorldToScreenPoint(Bounds(b.g).center).x));
            OpenMenu(items.ConvertAll(x => Of(x.g)), items.FindIndex(x => x.g == nearest));
            MenuKind = kind;
            menuTitleText = (kind == ListKind.Chemical ? "Hóa chất " : "Dụng cụ ") + where;
        }

        /// <summary>Vật cầm được trong bán kính r quanh chỗ trỏ (khi đi lại tự do, không đứng ở bàn nào).</summary>
        List<(Grabbable g, float d)> Around(Ray ray, float r)
        {
            Vector3 c = Physics.Raycast(ray, out var hit, hand.reach, ~0, QueryTriggerInteraction.Ignore) ? hit.point : ray.GetPoint(1.4f);
            int n = Physics.OverlapSphereNonAlloc(c, r, overlap, ~0, QueryTriggerInteraction.Collide);
            var seen = new HashSet<Grabbable>();
            var list = new List<(Grabbable, float)>();
            for (int i = 0; i < n; i++)
            {
                var g = Khkt.Assembly.ApparatusPart.WorldGrab(overlap[i].GetComponentInParent<Grabbable>());
                if (g == null || g.IsHeld || !g.isActiveAndEnabled || !seen.Add(g)) continue;
                list.Add((g, Vector3.Cross(ray.direction, Bounds(g).center - ray.origin).magnitude));
            }
            list.Sort((a, b) => a.Item2.CompareTo(b.Item2));
            return list;
        }

        static string SubOf(Grabbable g)
        {
            var cb = g.GetComponent<ChemicalBottle>();
            if (cb != null && cb.Entry != null)
            {
                var e = cb.Entry;
                string f = string.IsNullOrEmpty(e.cong_thuc) ? "" : e.Formula;
                string c = !string.IsNullOrEmpty(e.nong_do) ? e.nong_do : e.trang_thai;
                return string.IsNullOrEmpty(f) ? c : string.IsNullOrEmpty(c) ? f : f + " · " + c;
            }
            var lc = g.GetComponent<LabContainer>();
            if (lc != null && lc.VolumeMl > 0.1f) return $"đang chứa {lc.VolumeMl:0.#} ml";
            return lc != null ? "trống" : "";
        }

        Entry Of(Grabbable g) => new Entry { g = g, name = NameOf(g), sub = SubOf(g) };

        void PickNow(Grabbable g)
        {
            hand.Pick(g);
            if (hand.Held == g) Flash($"Đã cầm {NameOf(g)}");
        }

        string menuTitleText = "";

        void OpenMenu(List<Entry> items, int sel = 0)
        {
            menu.Clear();
            for (int i = 0; i < items.Count && i < MaxItems; i++) menu.Add(items[i]);
            menuSel = Mathf.Clamp(sel, 0, menu.Count - 1);
            menuOpenedAt = Time.unscaledTime;
            menuArmed = gesture != "chum";   // mở bằng chụm thì phải nhả ra rồi chụm lại mới lấy
            menuAnchorY = pointer.y;
            menuAnchorSel = menuSel;
            scrollTop = Mathf.Clamp(menuSel - Rows / 2, 0, Mathf.Max(0, menu.Count - Rows));
            MenuKind = null;
            menuTitleText = "";
            drawerX = DrawerHidden;
        }

        /// <summary>Mọi vật cầm được trên bàn đang làm, xếp theo khoảng cách tới tia trỏ.</summary>
        List<(Grabbable g, float d)> InZone(Ray ray)
        {
            var list = new List<(Grabbable, float)>();
            if (Zone == null) return list;
            int n = Physics.OverlapBoxNonAlloc(Zone.box.center, Zone.box.extents, overlap, Quaternion.identity, ~0, QueryTriggerInteraction.Collide);
            var seen = new HashSet<Grabbable>();
            for (int i = 0; i < n; i++)
            {
                var g = Khkt.Assembly.ApparatusPart.WorldGrab(overlap[i].GetComponentInParent<Grabbable>());
                if (g == null || g.IsHeld || !g.isActiveAndEnabled || !seen.Add(g)) continue;
                Vector3 p = Bounds(g).center;
                if (!Zone.Contains(p)) continue;
                list.Add((g, Vector3.Cross(ray.direction, p - ray.origin).magnitude));
            }
            list.Sort((a, b) => a.Item2.CompareTo(b.Item2));
            return list;
        }

        float menuAnchorY;
        int menuAnchorSel;

        void UpdateMenu(bool entered, float held, float now)
        {
            int k = menu.Count;
            // chỉ ngón trỏ (hoặc xòe tay) mới dời dòng chọn; chụm / nắm / giơ ngón út thì đứng yên,
            // nên lúc ngón trỏ cong lại hay lúc chụm để lấy, dòng đang chọn không nhảy
            bool moving = gesture == "tro" || gesture == "mo" || gesture == "khac";
            if (moving && drawer != null && canvas != null)
            {
                // độ cao con trỏ so với các dòng trên màn hình: tay ở ngang dòng nào thì chọn dòng đó
                float sf = canvas.scaleFactor > 0 ? canvas.scaleFactor : 1f;
                var corners = new Vector3[4];
                drawer.GetWorldCorners(corners);
                float topPx = viewCamera.WorldToScreenPoint(corners[1]).y - 96f * sf;   // mép trên dòng đầu
                float rowPx = RowH * sf;
                float f = (topPx - pointer.y) / rowPx;          // 0 = mép trên dòng đầu đang hiện
                int vis = Mathf.Min(k, Rows);
                int cur = menuSel - scrollTop;
                if (f < 0f)
                {
                    // trên đầu danh sách: cuộn lên dần
                    if (scrollTop > 0 && now - lastScroll > 0.3f) { scrollTop--; lastScroll = now; }
                    menuSel = scrollTop;
                }
                else if (f >= vis)
                {
                    if (scrollTop + Rows < k && now - lastScroll > 0.3f) { scrollTop++; lastScroll = now; }
                    menuSel = Mathf.Min(k - 1, scrollTop + vis - 1);
                }
                else
                {
                    // trễ ở ranh giới: phải vượt mép dòng 25% mới sang dòng mới
                    int r = Mathf.FloorToInt(f);
                    if (r != cur && (f < cur - 0.25f || f > cur + 1.25f || cur < 0 || cur >= vis)) menuSel = Mathf.Clamp(scrollTop + r, 0, k - 1);
                }
                menuArmed = true;
            }
            if (entered && gesture == "chum" && menuArmed && now - menuOpenedAt > 0.3f)
            {
                var e = menu[menuSel];
                CloseMenu();
                if (e.st != null) GoToBench(e.st);
                else if (e.use != null) UseNow(e.use);
                else if (e.travel) OpenTravelMenu();
                else if (e.g != null && !e.g.IsHeld) PickNow(e.g);
                return;
            }
            if ((gesture == "mo" && held > 0.4f) || now - menuOpenedAt > 20f) CloseMenu();
        }

        float lastScroll;

        /// <summary>Độ cao (điểm ảnh màn hình) của tâm dòng ứng với vật thứ i, nếu dòng đó đang hiện; NaN nếu không.</summary>
        public float MenuRowScreenY(int i)
        {
            if (drawer == null || canvas == null || i < scrollTop || i >= scrollTop + Rows || i >= menu.Count) return float.NaN;
            float sf = canvas.scaleFactor > 0 ? canvas.scaleFactor : 1f;
            var corners = new Vector3[4];
            drawer.GetWorldCorners(corners);
            float topPx = viewCamera.WorldToScreenPoint(corners[1]).y - 96f * sf;
            return topPx - (i - scrollTop + 0.5f) * RowH * sf;
        }

        void CloseMenu()
        {
            menu.Clear();
            MenuKind = null;
        }

        void PutDown()
        {
            var held = hand.Held;
            if (held == null) return;
            Ray ray = viewCamera.ScreenPointToRay(pointer);
            bool hitIn = Physics.Raycast(ray, out var hit, hand.reach, ~0, QueryTriggerInteraction.Ignore) && (Zone == null || Zone.Contains(hit.point));
            switch (hand.Current)
            {
                case HandController.Act.Place:
                    if (hand.TargetSocket != null && (Zone == null || Zone.Contains(hand.TargetSocket.transform.position)))
                    {
                        hand.PlaceInSocket(hand.TargetSocket);
                        return;
                    }
                    break;
                case HandController.Act.PlaceSurface:
                case HandController.Act.Attach:
                    if (hitIn) { pulse = 1; return; }   // để HandController đặt đúng điểm tia chạm mặt bàn
                    break;
            }
            if (Zone != null)
            {
                // trỏ ra ngoài bàn: đặt vào chỗ trống trên bàn đang làm, gần chỗ trỏ nhất
                Vector3 aim = hitIn ? hit.point : ray.origin + ray.direction * Mathf.Max(0.3f, (Zone.top - ray.origin.y) / Mathf.Min(-0.05f, ray.direction.y));
                hand.PlaceOnSurface(Zone.FreeSpot(aim, held));
                if (hand.Held == null) { Flash($"Đã đặt lên {Zone.Name}"); return; }
            }
            hand.ReturnHome();
        }

        // ----- di chuyển giữa các bàn -----

        void OpenTravelMenu()
        {
            var items = new List<Entry>();
            var here = Zone != null ? Zone.station : null;
            foreach (var z in BenchZones.All)
                if (z.station != here) items.Add(new Entry { st = z.station, name = z.Name, sub = "đi tới" });
            if (items.Count == 0) { Flash("Không có bàn nào khác"); return; }
            // bàn gần nhất trước
            Vector3 me = viewCamera.transform.position;
            items.Sort((a, b) => (a.st.transform.position - me).sqrMagnitude.CompareTo((b.st.transform.position - me).sqrMagnitude));
            OpenMenu(items);
            menuTitleText = "Đi tới bàn";
        }

        void GoToBench(Khkt.Environment.LabStation st)
        {
            var v = FindAnyObjectByType<Khkt.Environment.DesktopViewer>();
            if (v == null) return;
            v.GoTo(st);
            pointerReady = false;
            Flash($"Đã tới {st.displayName}");
        }

        void Activate(Ray ray)
        {
            switch (hand.Current)
            {
                case HandController.Act.Use:
                case HandController.Act.BioButton:
                case HandController.Act.BioHinge:
                case HandController.Act.BioKnob:
                case HandController.Act.BioTarget:
                    pulse = 1;
                    LastAction = hand.Current.ToString();
                    Flash("Kích hoạt");
                    return;
            }
            // máy tia chạm thẳng, không thì máy gần chỗ trỏ nhất
            bool hasHit = Physics.Raycast(ray, out var hit, hand.reach, ~0, QueryTriggerInteraction.Collide);
            var direct = hasHit ? hit.collider.GetComponentInParent<IDemoUsable>() : null;
            if (direct != null && (Zone == null || Zone.box.Contains(hit.point)))
            {
                UseNow(direct);
                return;
            }
            // đứng ở bàn mà trỏ ra ngoài bàn (trần, sàn, bàn khác): đi tới bàn khác
            if (Zone != null && (!hasHit || !Zone.box.Contains(hit.point))) { OpenTravelMenu(); return; }
            // máy trên cả bàn (hoặc quanh chỗ trỏ khi đi lại tự do), xếp theo khoảng cách tới tia
            var found = new List<(IDemoUsable u, float d)>();
            var seenU = new HashSet<IDemoUsable>();
            int n = Zone != null
                ? Physics.OverlapBoxNonAlloc(Zone.box.center, Zone.box.extents, overlap, Quaternion.identity, ~0, QueryTriggerInteraction.Collide)
                : Physics.OverlapSphereNonAlloc(hasHit ? hit.point : ray.GetPoint(1.4f), 0.45f, overlap, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var u = overlap[i].GetComponentInParent<IDemoUsable>();
                if (u == null || !seenU.Add(u) || !((Component)u).gameObject.activeInHierarchy) continue;
                Vector3 c = overlap[i].bounds.center;
                if (Zone != null && !Zone.box.Contains(c)) continue;
                found.Add((u, Vector3.Cross(ray.direction, c - ray.origin).magnitude));
            }
            found.Sort((a, b) => a.d.CompareTo(b.d));
            if (found.Count == 1 || (found.Count > 1 && found[1].d > found[0].d * 3f + 0.06f)) { UseNow(found[0].u); return; }
            if (found.Count > 1)
            {
                var items = found.ConvertAll(x => new Entry { use = x.u, name = UsableName(x.u), sub = "bật / dùng" });
                if (Zone != null) items.Add(new Entry { st = null, name = "Bàn khác…", sub = "đi tới", use = null, g = null, travel = true });
                OpenMenu(items);
                menuTitleText = "Máy " + (Zone != null ? "trên " + Zone.Name : "quanh đây");
                return;
            }
            OpenTravelMenu();   // bàn không có máy: giơ ngón út là đi tới bàn khác
        }

        // =====================================================================
        // Giao diện
        // =====================================================================

        Canvas canvas;
        RectTransform cursor, chip, legend;
        Image ring, ringFill, dot, chipDot;
        Text chipText, flashText;
        readonly List<(RectTransform rt, Text t, Image bg)> tags = new List<(RectTransform, Text, Image)>();
        readonly List<(Image bg, Text t)> legendRows = new List<(Image, Text)>();
        float flashUntil;

        static readonly Color Panel1 = new Color(0.035f, 0.06f, 0.095f, 0.97f);
        static readonly Color Teal = new Color(0.22f, 0.84f, 0.77f, 1f);

        static readonly (string id, string name, string use)[] Gestures =
        {
            ("nam_ngang", "Nắm ngang", "danh sách dụng cụ"),
            ("nam_doc", "Nắm dọc", "danh sách hóa chất"),
            ("chum", "Chụm 2 ngón", "lấy vật đã chọn · bấm"),
            ("ut", "Giơ ngón út", "bật máy · đi bàn khác"),
            ("tro", "Ngón trỏ", "trượt chọn · con trỏ"),
            ("mo", "Xòe tay", "đặt xuống · đóng"),
        };

        static Color GestureColor(string g) => g switch
        {
            "nam_doc" => new Color(1f, 0.67f, 0.3f),
            "nam_ngang" => new Color(0.45f, 0.75f, 1f),
            "chum" => new Color(0.22f, 0.84f, 0.77f),
            "ut" => new Color(0.95f, 0.5f, 0.85f),
            "mo" => new Color(0.6f, 0.95f, 0.55f),
            _ => new Color(0.92f, 0.95f, 1f),
        };

        static string GestureName(string g)
        {
            foreach (var x in Gestures) if (x.id == g) return x.name;
            return "Tay";
        }

        void BuildUi()
        {
            var go = new GameObject("Giao diện thao tác tay", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            canvas = go.AddComponent<Canvas>();
            // cùng kiểu với ModernUI: vẽ bằng camera người xem, sát mặt cắt gần để tay ảo không che
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = viewCamera;
            canvas.planeDistance = 0.029f;
            canvas.sortingOrder = 600;
            var sc = go.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight = 0.5f;
            var root = go.transform;

            // con trỏ: vòng ngoài, vòng tiến độ, chấm giữa
            cursor = Rect(root, "Con trỏ tay", Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(54, 54));
            ring = Img(cursor, "Vòng", Circle, new Color(1, 1, 1, 0.9f), Vector2.zero, new Vector2(54, 54));
            ringFill = Img(cursor, "Tiến độ", SolidCircle, new Color(Teal.r, Teal.g, Teal.b, 0.35f), Vector2.zero, new Vector2(54, 54));
            ringFill.type = Image.Type.Filled;
            ringFill.fillMethod = Image.FillMethod.Radial360;
            ringFill.fillOrigin = (int)Image.Origin360.Top;
            ringFill.fillAmount = 0;
            dot = Img(cursor, "Chấm", SolidCircle, Color.white, Vector2.zero, new Vector2(10, 10));

            // thẻ trạng thái ở giữa mép dưới
            chip = Panel(root, "Thao tác hiện tại", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(380, 50), null).rectTransform;
            Solidify(chip.GetComponent<Image>());
            chipDot = Img(chip, "Màu", SolidCircle, Teal, new Vector2(-160, 0), new Vector2(16, 16));
            chipText = Label(chip, "Chữ", "", 19, Ink, true, TextAnchor.MiddleLeft, new Vector2(42, -2), new Vector2(330, 46));
            flashText = Label(root, "Thông báo", "", 20, Ink, true, TextAnchor.MiddleCenter, new Vector2(-300, 120), new Vector2(600, 40), new Vector2(0.5f, 0));
            var sh = flashText.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.6f);
            sh.effectDistance = new Vector2(0, -2);

            // bảng cử chỉ ở góc dưới trái
            legend = Panel(root, "Bảng cử chỉ", new Vector2(0, 0), new Vector2(0, 0), new Vector2(18, 18), new Vector2(300, 26 + Gestures.Length * 30), null).rectTransform;
            Solidify(legend.GetComponent<Image>());
            for (int i = 0; i < Gestures.Length; i++)
            {
                var rowBg = Panel(legend, "Hàng " + i, new Vector2(0, 1), new Vector2(0, 1), new Vector2(8, -13 - i * 30), new Vector2(284, 28), null).GetComponent<Image>();
                rowBg.color = new Color(1, 1, 1, 0);
                Img(rowBg.rectTransform, "Màu", SolidCircle, GestureColor(Gestures[i].id), new Vector2(-130, 0), new Vector2(10, 10));
                var t = Label(rowBg.rectTransform, "Chữ", $"<b>{Gestures[i].name}</b>  <color=#9FB3C8>{Gestures[i].use}</color>", 14, Ink, false,
                              TextAnchor.MiddleLeft, new Vector2(24, 0), new Vector2(256, 28));
                legendRows.Add((rowBg, t));
            }

            BuildDrawer(root);
            BuildPreview(root);
            HideUi();
        }

        // ----- khung xem webcam + điểm mốc (F7): thấy ngay máy đang nhận tay thế nào -----
        RectTransform preview;
        RawImage previewImg;
        Text previewText;
        readonly Image[] previewDots = new Image[42];
        bool previewOn = true;
        const float PvW = 288f, PvH = 216f;

        void BuildPreview(Transform root)
        {
            preview = Panel(root, "Khung xem webcam", new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -18), new Vector2(PvW + 16, PvH + 44), null).rectTransform;
            Solidify(preview.GetComponent<Image>());
            var rt = Rect(preview, "Ảnh", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(PvW, PvH));
            previewImg = rt.gameObject.AddComponent<RawImage>();
            previewImg.raycastTarget = false;
            previewImg.color = new Color(1, 1, 1, 0.95f);
            for (int i = 0; i < previewDots.Length; i++)
            {
                previewDots[i] = Img(rt, "Điểm " + i, SolidCircle, i < 21 ? Teal : new Color(1f, 0.67f, 0.3f), Vector2.zero, new Vector2(7, 7));
                previewDots[i].rectTransform.anchorMin = previewDots[i].rectTransform.anchorMax = Vector2.zero;
            }
            previewText = Label(preview, "Chữ", "", 14, Ink, false, TextAnchor.MiddleLeft, new Vector2(12, -PvH - 12), new Vector2(PvW, 26));
        }

        void UpdatePreview()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f7Key.wasPressedThisFrame) previewOn = !previewOn;
            preview.gameObject.SetActive(previewOn);
            if (!previewOn) return;
            Texture tex = null;
#if KHKT_MEDIAPIPE_PLUGIN
            if (PluginHandTracker.Instance != null && PluginHandTracker.Instance.Running) tex = PluginHandTracker.Instance.Preview;
#endif
            previewImg.texture = tex;
            previewImg.uvRect = new Rect(0, 1, 1, -1);   // ảnh đưa vào MediaPipe lộn ngược theo chiều dọc
            previewImg.color = tex != null ? new Color(1, 1, 1, 0.95f) : new Color(0.1f, 0.13f, 0.18f, 1f);
            var at = ArmTracking.Instance;
            for (int k = 0; k < 2; k++)
            {
                var sd = at == null ? null : k == 0 ? at.Right : at.Left;
                bool on = sd != null && sd.handSeen && sd.hasImage;
                for (int i = 0; i < 21; i++)
                {
                    var d = previewDots[k * 21 + i];
                    d.gameObject.SetActive(on);
                    if (on) d.rectTransform.anchoredPosition = new Vector2(sd.image[i].x * PvW, (1f - sd.image[i].y) * PvH);
                }
            }
            string src = HandTrackerProcess.Instance == null ? "" : HandTrackerProcess.Instance.UsingPlugin ? "MediaPipe C#" : HandTrackerProcess.Instance.UsingFallback ? "dự phòng" : "Python";
            string pinch = side != null && side.hasImage ? $"chụm {PinchRatio(side.image):0.00}" : "";
            previewText.text = $"<b>{GestureName(gesture)}</b>  <color=#9FB3C8>{pinch} · {src} · F7 ẩn</color>";
        }

        static float PinchRatio(Vector2[] lm) { Vector2 a = new Vector2(4f / 3f, 1f); return Vector2.Scale(lm[4] - lm[8], a).magnitude / (Vector2.Scale(lm[0] - lm[9], a).magnitude + 1e-6f); }

        static void Solidify(Image img)
        {
            img.sprite = Solid;
            img.type = Image.Type.Sliced;
            img.color = Panel1;
        }

        static Image Img(Transform parent, string name, Sprite sprite, Color c, Vector2 pos, Vector2 size)
        {
            var rt = Rect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        void HideUi()
        {
            if (outline != null && outline.enabled) outline.enabled = false;
            if (canvas != null && canvas.gameObject.activeSelf) canvas.gameObject.SetActive(false);
        }

        void Flash(string text)
        {
            if (flashText == null) return;
            flashText.text = text;
            flashUntil = Time.unscaledTime + 1.6f;
        }

        void UpdateUi()
        {
            if (canvas == null) return;
            if (!canvas.gameObject.activeSelf) canvas.gameObject.SetActive(true);
            float scale = canvas.scaleFactor > 0 ? 1f / canvas.scaleFactor : 1f;
            float now = Time.unscaledTime;

            // con trỏ
            bool uiOpen = Khkt.Environment.DesktopViewer.UiCapture > 0;
            cursor.anchoredPosition = pointer * scale;
            Color gc = GestureColor(gesture);
            ring.color = new Color(gc.r, gc.g, gc.b, 0.95f);
            dot.color = gc;
            float target = gesture == "chum" ? 0.7f : gesture.StartsWith("nam") ? 0.8f : 1f;
            cursor.localScale = Vector3.one * Mathf.Lerp(cursor.localScale.x, target, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
            float fill = 0f;
            if (hand.Held != null && gesture == "mo") fill = Mathf.Clamp01((now - gestureSince) / 0.25f);
            else if (menu.Count > 0 && gesture == "mo") fill = Mathf.Clamp01((now - gestureSince) / 0.4f);
            ringFill.fillAmount = fill;
            ringFill.color = new Color(gc.r, gc.g, gc.b, 0.4f);

            // thẻ trạng thái
            chipDot.color = gc;
            string what;
            if (uiOpen) what = gesture == "chum" ? "Đang bấm" : "Ngón trỏ là chuột · chụm để bấm";
            else if (menu.Count > 0) what = "Ngón trỏ đưa lên / xuống ngang dòng cần chọn · chụm 2 ngón để lấy · xòe tay để đóng";
            else if (hand.Held != null) what = $"Đang cầm {NameOf(hand.Held)} · út: dùng · xòe: đặt";
            else what = gesture switch
            {
                "nam_doc" => "Nắm dọc · danh sách hóa chất",
                "nam_ngang" => "Nắm ngang · danh sách dụng cụ",
                "chum" => "Chụm 2 ngón · nắm tay trước để mở danh sách",
                "ut" => "Ngón út · bật máy",
                "tro" => "Ngón trỏ · trỏ vào vật",
                "mo" => "Xòe tay",
                _ => "Đưa tay vào khung webcam",
            };
            string bench = Zone != null ? $"<color=#38D6C4>{Zone.Name}</color>  " : "";
            chipText.text = $"{bench}{GestureName(gesture)}  <color=#9FB3C8>{what.Substring(what.IndexOf('·') >= 0 && !uiOpen && menu.Count == 0 && hand.Held == null ? what.IndexOf('·') + 2 : 0)}</color>";
            float w = Mathf.Clamp(Width(chipText, StripTags(chipText.text)) * 1.08f + 96, 260, 1100);
            chip.sizeDelta = new Vector2(w, 50);
            chipDot.rectTransform.anchoredPosition = new Vector2(-w / 2 + 24, 0);
            chipText.rectTransform.anchorMin = chipText.rectTransform.anchorMax = new Vector2(0, 0.5f);
            chipText.rectTransform.pivot = new Vector2(0, 0.5f);
            chipText.rectTransform.anchoredPosition = new Vector2(42, 0);
            chipText.rectTransform.sizeDelta = new Vector2(w - 50, 46);
            flashText.gameObject.SetActive(now < flashUntil);
            UpdatePreview();
            // thông báo nằm trên menu khi menu đang mở
            flashText.rectTransform.anchoredPosition = new Vector2(-300, menu.Count > 0 && !uiOpen ? 300 : 120);

            for (int i = 0; i < legendRows.Count; i++)
                legendRows[i].bg.color = Gestures[i].id == gesture ? new Color(1, 1, 1, 0.12f) : new Color(1, 1, 1, 0f);

            UpdateOutline(!uiOpen);
            UpdateDrawer(uiOpen, scale);
        }

        // ----- danh sách trượt bên phải -----
        RectTransform drawer;
        Text drawerTitle, drawerHint, drawerMore;
        Image drawerAccent;
        readonly List<(Image bg, Text num, Text name, Text sub)> rows = new List<(Image, Text, Text, Text)>();
        const float DrawerW = 420f, RowH = 58f, DrawerHidden = 460f;
        float drawerX = DrawerHidden;
        int scrollTop;

        void BuildDrawer(Transform root)
        {
            float h = 96 + Rows * RowH + 44;
            drawer = Panel(root, "Danh sách trượt", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(DrawerHidden, 0), new Vector2(DrawerW, h), null).rectTransform;
            Solidify(drawer.GetComponent<Image>());
            drawerAccent = Img(drawer, "Vạch màu", Solid, Teal, Vector2.zero, new Vector2(6, h - 36));
            drawerAccent.rectTransform.anchorMin = drawerAccent.rectTransform.anchorMax = new Vector2(0, 0.5f);
            drawerAccent.rectTransform.anchoredPosition = new Vector2(10, 0);
            drawerTitle = Label(drawer, "Tiêu đề", "", 20, Ink, true, TextAnchor.MiddleLeft, new Vector2(28, -14), new Vector2(DrawerW - 44, 34));
            drawerTitle.horizontalOverflow = HorizontalWrapMode.Overflow;
            drawerHint = Label(drawer, "Hướng dẫn", "", 13, Dim, false, TextAnchor.UpperLeft, new Vector2(28, -50), new Vector2(DrawerW - 44, 40));
            for (int i = 0; i < Rows; i++)
            {
                var bg = Panel(drawer, "Dòng " + i, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -96 - i * RowH), new Vector2(DrawerW - 36, RowH - 6), null).GetComponent<Image>();
                var num = Label(bg.rectTransform, "Số", "", 20, Teal, true, TextAnchor.MiddleCenter, new Vector2(4, -4), new Vector2(40, RowH - 14));
                var name = Label(bg.rectTransform, "Tên", "", 16, Ink, true, TextAnchor.UpperLeft, new Vector2(50, -6), new Vector2(DrawerW - 100, 24));
                var sub = Label(bg.rectTransform, "Phụ", "", 12, Dim, false, TextAnchor.UpperLeft, new Vector2(50, -30), new Vector2(DrawerW - 100, 20));
                name.horizontalOverflow = HorizontalWrapMode.Overflow;
                rows.Add((bg, num, name, sub));
            }
            drawerMore = Label(drawer, "Còn nữa", "", 13, Dim, false, TextAnchor.MiddleCenter, new Vector2(0, -96 - Rows * RowH), new Vector2(DrawerW, 30));
            for (int i = 0; i < 5; i++)
            {
                var tagBg = Img(root, "Nhãn vật " + i, SolidCircle, Teal, Vector2.zero, new Vector2(34, 34));
                tagBg.rectTransform.anchorMin = tagBg.rectTransform.anchorMax = Vector2.zero;
                var tt = Label(tagBg.rectTransform, "Số", "", 16, new Color(0.03f, 0.08f, 0.1f), true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(34, 34));
                tt.rectTransform.anchorMin = tt.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                tt.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                tags.Add((tagBg.rectTransform, tt, tagBg));
            }
        }

        void UpdateDrawer(bool uiOpen, float scale)
        {
            bool show = menu.Count > 0 && !uiOpen;
            // trượt ra / vào mượt
            float target = show ? -18f : DrawerHidden;
            drawerX = Mathf.Lerp(drawerX, target, 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime));
            drawer.anchoredPosition = new Vector2(drawerX, 0);
            drawer.gameObject.SetActive(show || drawerX < DrawerHidden - 2f);
            for (int i = 0; i < tags.Count; i++) tags[i].rt.gameObject.SetActive(false);
            if (!show) return;
            int k = menu.Count;
            // chiều cao co theo số dòng (đáy cố định, đầu bảng hạ xuống khi danh sách ngắn)
            int vis = Mathf.Min(k, Rows);
            float h = 96 + vis * RowH + (k > Rows ? 40 : 14);
            drawer.sizeDelta = new Vector2(DrawerW, h);
            drawerAccent.rectTransform.sizeDelta = new Vector2(6, h - 36);
            drawerMore.rectTransform.anchoredPosition = new Vector2(0, -96 - vis * RowH);
            Color accent = MenuKind == ListKind.Chemical ? GestureColor("nam_doc") : MenuKind == ListKind.Tool ? GestureColor("nam_ngang") : GestureColor("ut");
            drawerAccent.color = accent;
            drawerTitle.text = string.IsNullOrEmpty(menuTitleText) ? "Chọn" : menuTitleText;
            drawerHint.text = $"<color=#{ColorUtility.ToHtmlStringRGB(accent)}><b>{menuSel + 1}/{k}</b></color>   Giơ <b>ngón trỏ</b>, đưa tay lên / xuống ngang dòng cần chọn · <b>chụm 2 ngón</b> để " +
                              (MenuIsTravel ? "đi" : menu[0].use != null ? "bật" : "lấy") + " · <b>xòe tay</b> để đóng";
            // cuộn để dòng đang chọn luôn thấy được
            if (menuSel < scrollTop) scrollTop = menuSel;
            if (menuSel >= scrollTop + Rows) scrollTop = menuSel - Rows + 1;
            scrollTop = Mathf.Clamp(scrollTop, 0, Mathf.Max(0, k - Rows));
            for (int r = 0; r < Rows; r++)
            {
                int i = scrollTop + r;
                var row = rows[r];
                bool on = i < k;
                row.bg.gameObject.SetActive(on);
                if (!on) continue;
                bool sel = i == menuSel;
                row.bg.color = sel ? new Color(accent.r * 0.55f, accent.g * 0.55f, accent.b * 0.55f, 1f) : new Color(1, 1, 1, 0.06f);
                row.bg.sprite = sel ? Solid : Rounded;
                row.sub.color = sel ? new Color(0.92f, 0.96f, 1f, 0.9f) : Dim;
                row.num.text = (i + 1).ToString();
                row.num.color = sel ? Color.white : accent;
                row.name.text = menu[i].name;
                row.sub.text = menu[i].sub;
                row.bg.rectTransform.anchoredPosition = new Vector2(20, -96 - r * RowH);
                row.bg.rectTransform.localScale = new Vector3(sel ? 1.02f : 1f, sel ? 1.04f : 1f, 1f);
            }
            int below = k - (scrollTop + Rows), above = scrollTop;
            drawerMore.text = below > 0 || above > 0 ? $"{(above > 0 ? $"▲ {above} ở trên   " : "")}{(below > 0 ? $"▼ còn {below} ở dưới" : "")}" : "";

            // số trên vật thật: dòng đang chọn và hai dòng kề
            int t = 0;
            for (int i = Mathf.Max(0, menuSel - 2); i <= Mathf.Min(k - 1, menuSel + 2) && t < tags.Count; i++)
            {
                var g = menu[i].g;
                if (g == null) continue;
                var b = Bounds(g);
                Vector3 sp = viewCamera.WorldToScreenPoint(b.center + Vector3.up * (b.extents.y + 0.04f));
                if (sp.z <= 0) continue;
                var tag = tags[t++];
                bool sel = i == menuSel;
                tag.rt.gameObject.SetActive(true);
                tag.rt.anchoredPosition = new Vector2(sp.x, sp.y) * scale;
                tag.bg.color = sel ? Color.white : new Color(accent.r, accent.g, accent.b, 0.85f);
                tag.t.text = (i + 1).ToString();
                tag.rt.localScale = Vector3.one * (sel ? 1.3f : 0.85f);
            }
        }

        LineRenderer outline;
        BenchZone outlined;

        /// <summary>Viền sáng quanh mặt bàn đang làm: biết ngay khu nào tay đang tác động.</summary>
        void UpdateOutline(bool show)
        {
            if (outline == null)
            {
                var go = new GameObject("Viền khu bàn");
                go.transform.SetParent(transform, false);
                outline = go.AddComponent<LineRenderer>();
                outline.useWorldSpace = true;
                outline.loop = true;
                outline.positionCount = 4;
                outline.widthMultiplier = 0.006f;
                outline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                outline.receiveShadows = false;
                var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                var mat = new Material(sh) { hideFlags = HideFlags.DontSave };
                mat.SetColor("_BaseColor", new Color(0.22f, 0.84f, 0.77f, 1f));
                mat.color = new Color(0.22f, 0.84f, 0.77f, 1f);
                outline.sharedMaterial = mat;
            }
            bool on = show && Zone != null;
            if (outline.enabled != on) outline.enabled = on;
            if (!on || outlined == Zone) return;
            outlined = Zone;
            var b = Zone.plate;
            float y = Zone.top + 0.003f;
            outline.SetPosition(0, new Vector3(b.min.x + 0.01f, y, b.min.z + 0.01f));
            outline.SetPosition(1, new Vector3(b.max.x - 0.01f, y, b.min.z + 0.01f));
            outline.SetPosition(2, new Vector3(b.max.x - 0.01f, y, b.max.z - 0.01f));
            outline.SetPosition(3, new Vector3(b.min.x + 0.01f, y, b.max.z - 0.01f));
        }

        static string StripTags(string s) => System.Text.RegularExpressions.Regex.Replace(s, "<.*?>", "");

        // =====================================================================
        // Bấm chuột hệ thống (bảng giao diện uGUI lẫn IMGUI đều nhận)
        // =====================================================================

        static class OsMouse
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            [System.Runtime.InteropServices.DllImport("user32.dll")]
            static extern void mouse_event(uint flags, uint dx, uint dy, uint data, System.UIntPtr extra);
            public static void Button(bool down) => mouse_event(down ? 0x0002u : 0x0004u, 0, 0, 0, System.UIntPtr.Zero);
#else
            public static void Button(bool down)
            {
                var m = Mouse.current;
                if (m == null) return;
                using (UnityEngine.InputSystem.LowLevel.StateEvent.From(m, out var ptr))
                {
                    m.leftButton.WriteValueIntoEvent(down ? 1f : 0f, ptr);
                    UnityEngine.InputSystem.InputSystem.QueueEvent(ptr);
                }
            }
#endif
        }
    }
}
