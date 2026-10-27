using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Khkt.Demo;
using Khkt.Environment;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Khkt.Assembly
{
    /// <summary>
    /// Khu chế tạo dụng cụ: không gian lắp ráp ảo tách khỏi phòng lab (đặt xa tòa nhà, camera riêng). Mở từ bàn chế tạo trong
    /// ngăn kính: camera người xem tạm tắt (phòng lab thôi vẽ), học sinh lấy bộ phận ở bảng bên trái, rê tới gần khớp hợp thì
    /// hiện bóng mờ, bấm để lắp. Bấm vào bộ phận đã lắp để cầm ra (kéo theo nhánh con), Delete để bỏ, Ctrl+Z hoàn tác.
    /// Dùng lại nguyên phần lắp ráp C1 + C2: khớp nối, tự áp khít, hệ kín / hở, trọng tâm.
    ///   Chuột trái       : lấy / lắp / đặt
    ///   Giữ chuột phải   : xoay góc nhìn;   giữ chuột giữa: kéo góc nhìn
    ///   Lăn chuột        : phóng to / thu nhỏ (đang cầm: xoay bộ phận); R / E: xoay bộ phận
    ///   Delete           : bỏ bộ phận đang cầm;   Ctrl+Z: hoàn tác;   Esc: hủy cầm / thoát
    /// </summary>
    public class FabricationWorkspace : MonoBehaviour
    {
        [Header("Cảnh")]
        public Camera workCamera;
        [Tooltip("Camera người xem: tắt khi đang lắp để phòng lab thôi vẽ")]
        public Camera viewCamera;
        public DesktopViewer viewer;
        [Tooltip("Gốc chứa bộ phận đang lắp (và vòi nước giả của xưởng); mặt bàn ở y = tableTop (cục bộ)")]
        public Transform table;
        public float tableTop = 0.9f;
        public Vector2 tableHalf = new Vector2(1.15f, 0.55f);
        [Tooltip("Chỗ chụp ảnh nhỏ cho bảng bộ phận (ngoài khung nhìn)")]
        public Transform thumbStage;
        public Camera thumbCamera;
        public PartLibrary library;
        public Material ghostMaterial;
        public Shader outlineShader;
        public Shader glassShader;

        public static FabricationWorkspace Instance { get; private set; }
        /// <summary>Vật nằm trong không gian ảo (kể cả chỗ chụp ảnh nhỏ): bỏ qua kiểm tra đổ ngoài phòng.</summary>
        public static bool IsInside(Transform t) => Instance != null && t != null && t.IsChildOf(Instance.transform);

        public bool IsOpen { get; private set; }
        public ApparatusPart Held { get; private set; }
        public ApparatusPart Hover { get; private set; }
        public Port AttachMine { get; private set; }
        public Port AttachTarget { get; private set; }
        public string Message { get; private set; } = "";
        public bool MessageWarn { get; private set; }
        public float MessageTime { get; private set; } = -10f;
        public bool ThumbnailsReady { get; private set; }
        public int UndoCount => undo.Count;

        /// <summary>Có thay đổi trên bàn ảo (lắp, tháo, thêm, bỏ): bảng kiểm tra tính lại.</summary>
        public event Action Changed;
        /// <summary>Bấm "Gia công" (phần gia công ra bàn đích làm ở bước F3).</summary>
        public event Action FabricateRequested;

        /// <summary>Kiểm thử tự động: tia trỏ thay cho chuột.</summary>
        public Ray? OverrideRay { get; set; }

        FabricationUI ui;
        public FabricationUI Ui => ui;
        HoverOutline outline;
        bool captured, thumbsStarted;
        Transform targetDot;
        float heldYaw;
        float camYaw, camPitch = 32f, camDist = 1.9f;
        Vector3 pivot;
        int nextInst;
        readonly Dictionary<ApparatusPart, (Vector3 pos, Quaternion rot)> rests = new Dictionary<ApparatusPart, (Vector3, Quaternion)>();
        readonly Dictionary<ApparatusPart, int> inst = new Dictionary<ApparatusPart, int>();
        readonly List<List<Rec>> undo = new List<List<Rec>>();
        const int MaxUndo = 40;

        /// <summary>Một bộ phận trên bàn ảo: mẫu nào, ở đâu, nối vào đâu (để hoàn tác, gia công, lưu thiết kế).</summary>
        [Serializable]
        public struct Rec
        {
            public string lib, name, fixture;
            public int sub, inst, parent, myPort, targetPort;
            public Vector3 pos;
            public Quaternion rot;
        }

        void Awake()
        {
            Instance = this;
            Khkt.Rendering.ZoneRoot.Excluded.Add(transform);  // đồ trong xưởng ảo không phải đồ của phòng lab (robot, tra cứu, nguồn nhiệt bỏ qua)
            pivot = new Vector3(0, tableTop + 0.2f, 0);
            if (workCamera != null) workCamera.enabled = false;
            if (thumbCamera != null) thumbCamera.enabled = false;
            outline = gameObject.AddComponent<HoverOutline>();
            outline.outlineShader = outlineShader;
            outline.color = new Color(0.45f, 0.9f, 1f, 1f);
            // chấm sáng đánh dấu khớp đích khi đang nhắm lắp (thay vì viền sáng cả cây lắp ráp)
            var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(dot.GetComponent<Collider>());
            dot.name = "Dấu khớp đích";
            dot.transform.SetParent(transform, false);
            dot.transform.localScale = Vector3.one * 0.028f;
            var dr = dot.GetComponent<Renderer>();
            if (ghostMaterial != null) dr.sharedMaterial = ghostMaterial;
            dr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            dot.SetActive(false);
            targetDot = dot.transform;
            ApparatusPart.Changed += OnPartsChanged;
        }

        void OnDestroy() => ApparatusPart.Changed -= OnPartsChanged;

        void OnPartsChanged(ApparatusPart root)
        {
            if (root != null && IsInside(root.transform)) Changed?.Invoke();
        }

        // =====================================================================
        // Mở / đóng
        // =====================================================================

        public void Open()
        {
            if (IsOpen) return;
            if (ui == null)
            {
                ui = gameObject.AddComponent<FabricationUI>();
                ui.Init(this, glassShader);
            }
            IsOpen = true;
            UiBridge.SetCapture(ref captured, true);
            if (viewCamera != null) viewCamera.enabled = false;  // phòng lab thôi vẽ khi đang lắp
            workCamera.enabled = true;
            ui.SetVisible(true);
            if (!thumbsStarted) { thumbsStarted = true; StartCoroutine(MakeThumbnails()); }
            Say("Chọn bộ phận ở bảng bên trái, rê tới gần khớp hợp (hiện bóng mờ xanh) rồi bấm để lắp");
            Changed?.Invoke();
        }

        public void Close()
        {
            if (!IsOpen) return;
            if (Held != null) Drop();
            AssemblyGhost.Hide();
            outline.SetTarget(null);
            targetDot.gameObject.SetActive(false);
            Hover = null;
            IsOpen = false;
            ui.SetVisible(false);
            workCamera.enabled = false;
            if (viewCamera != null) viewCamera.enabled = true;
            UiBridge.SetCapture(ref captured, false);
        }

        public void RequestFabricate()
        {
            if (Busy) return;
            if (Parts().Count == 0) { Say("Bàn ảo còn trống: lắp bộ dụng cụ trước rồi mới gia công", true); return; }
            FabricateRequested?.Invoke();
        }

        // =====================================================================
        // Mỗi khung hình
        // =====================================================================

        void Update()
        {
            if (!IsOpen || Busy) return;
            var mouse = Mouse.current;
            var kb = Keyboard.current;
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            bool typing = UiBridge.Typing;

            if (mouse != null)
            {
                Vector2 d = mouse.delta.ReadValue();
                if (mouse.rightButton.isPressed)
                {
                    camYaw += d.x * 0.25f;
                    camPitch = Mathf.Clamp(camPitch - d.y * 0.25f, 5f, 85f);
                }
                if (mouse.middleButton.isPressed)
                {
                    var t = workCamera.transform;
                    Vector3 move = (-t.right * d.x - t.up * d.y) * camDist * 0.0015f;
                    pivot += Quaternion.Inverse(table.rotation) * move;
                    pivot = new Vector3(Mathf.Clamp(pivot.x, -1.4f, 1.4f), Mathf.Clamp(pivot.y, tableTop - 0.1f, tableTop + 0.9f), Mathf.Clamp(pivot.z, -0.8f, 0.8f));
                }
                float wheel = overUi ? 0f : mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f)
                {
                    if (Held != null) heldYaw += Mathf.Sign(wheel) * 15f;
                    else camDist = Mathf.Clamp(camDist * (wheel > 0 ? 0.9f : 1.1f), 0.45f, 4f);
                }
            }
            if (kb != null && !typing)
            {
                if (Held != null)
                {
                    if (kb.rKey.wasPressedThisFrame) heldYaw += 15f;
                    if (kb.eKey.wasPressedThisFrame) heldYaw -= 15f;
                    if (kb.deleteKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) DeleteHeld();
                }
                bool ctrl = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
                if (ctrl && kb.zKey.wasPressedThisFrame) Undo();
                if (kb.escapeKey.wasPressedThisFrame)
                {
                    if (Held != null) Undo();
                    else Close();
                    return;
                }
            }
            ApplyCamera();

            Ray ray = OverrideRay ?? (mouse != null ? workCamera.ScreenPointToRay(mouse.position.ReadValue()) : workCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f)));
            if (Held != null) FollowHeld(ray);
            else HoverAt(overUi ? (Ray?)null : ray);

            if (mouse != null && mouse.leftButton.wasPressedThisFrame && !overUi)
            {
                if (Held != null) Drop();
                else if (Hover != null) Pick(Hover);
            }

            if (Held != null && AttachTarget != null)
            {
                ApparatusPart.ComputePose(AttachMine, AttachTarget, out var gp, out var gr);
                AssemblyGhost.Show(Held, gp, gr, ghostMaterial);
            }
            else AssemblyGhost.Hide();
            outline.SetTarget(Held == null && Hover != null ? Hover.transform : null);
            bool aiming = Held != null && AttachTarget != null;
            if (targetDot.gameObject.activeSelf != aiming) targetDot.gameObject.SetActive(aiming);
            if (aiming) targetDot.position = AttachTarget.transform.position;
        }

        void ApplyCamera()
        {
            var rot = table.rotation * Quaternion.Euler(camPitch, camYaw, 0);
            workCamera.transform.SetPositionAndRotation(table.TransformPoint(pivot) - rot * Vector3.forward * camDist, rot);
        }

        /// <summary>Đưa góc nhìn về mặc định (nhìn từ trước bàn, thấy vòi nước ở vách sau).</summary>
        public void ResetView(float yaw = 0f, float pitch = 32f, float dist = 1.9f, Vector3? localPivot = null)
        {
            camYaw = yaw;
            camPitch = pitch;
            camDist = dist;
            pivot = localPivot ?? new Vector3(0, tableTop + 0.2f, 0);
            if (workCamera != null && table != null) ApplyCamera();
        }

        void HoverAt(Ray? ray)
        {
            Hover = null;
            if (!ray.HasValue || !Physics.Raycast(ray.Value, out var hit, 12f, ~0, QueryTriggerInteraction.Ignore)) return;
            var part = hit.collider.GetComponentInParent<ApparatusPart>();
            if (part != null && !part.fixture && inst.ContainsKey(part)) Hover = part;
        }

        void FollowHeld(Ray ray)
        {
            AttachMine = AttachTarget = null;
            if (ApparatusPart.FindAttach(Held, ray, 0.05f, 6f, out var m, out var t) && t.Part != null && t.Part.transform.IsChildOf(table))
            {
                AttachMine = m;
                AttachTarget = t;
            }
            if (SurfacePoint(ray, out var p)) PoseHeld(p);
        }

        /// <summary>Điểm đặt trên mặt đỡ (mặt bàn ảo, đế giá...) dưới tia trỏ, giới hạn trong mặt bàn.</summary>
        bool SurfacePoint(Ray ray, out Vector3 point)
        {
            point = default;
            float best = float.PositiveInfinity;
            foreach (var h in Physics.RaycastAll(ray, 12f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!h.collider.transform.IsChildOf(transform) || h.normal.y < 0.6f || h.distance >= best) continue;
                best = h.distance;
                point = h.point;
            }
            if (float.IsInfinity(best))
            {
                var plane = new Plane(table.up, table.TransformPoint(new Vector3(0, tableTop, 0)));
                if (!plane.Raycast(ray, out float enter)) return false;
                point = ray.GetPoint(enter);
            }
            var local = table.InverseTransformPoint(point);
            local.x = Mathf.Clamp(local.x, -tableHalf.x, tableHalf.x);
            local.z = Mathf.Clamp(local.z, -tableHalf.y, tableHalf.y);
            local.y = Mathf.Max(local.y, tableTop);
            point = table.TransformPoint(local);
            return true;
        }

        void PoseHeld(Vector3 p)
        {
            var rest = rests.TryGetValue(Held, out var r) ? r : (Vector3.zero, Quaternion.identity);
            var yaw = Quaternion.Euler(0, heldYaw, 0);
            Held.transform.SetPositionAndRotation(p + table.rotation * (yaw * rest.Item1), table.rotation * yaw * rest.Item2);
        }

        // =====================================================================
        // Thao tác (giao diện và kiểm thử tự động gọi)
        // =====================================================================

        /// <summary>Lấy một bộ phận mới từ kho (bảng bên trái): bộ phận đi theo chuột.</summary>
        public ApparatusPart SpawnHeld(string libraryId)
        {
            var e = library != null ? library.Get(libraryId) : null;
            if (e == null) return null;
            if (Held != null) Drop();
            PushUndo();
            var spawned = Spawn(e, table.TransformPoint(new Vector3(0, tableTop, -0.2f)), table.eulerAngles.y);
            if (spawned.Count == 0) return null;
            Held = spawned[0];
            heldYaw = 0;
            Held.Grab?.SetHeld(true);
            Say($"Đang cầm {Lower(Held.partName)}: rê tới khớp hợp để lắp, hoặc bấm lên mặt bàn để đặt");
            Changed?.Invoke();
            return Held;
        }

        List<ApparatusPart> Spawn(PartLibrary.Entry e, Vector3 pos, float yaw, int forceInst = 0)
        {
            var spawned = library.Spawn(e, table, pos, yaw);
            int id = forceInst > 0 ? forceInst : ++nextInst;
            nextInst = Mathf.Max(nextInst, id);
            int same = inst.Keys.Count(p => p != null && p.libraryId == e.id && p.librarySub == 0);
            var list = new List<ApparatusPart>();
            foreach (var (part, rp, rr) in spawned)
            {
                rests[part] = (rp, rr);
                inst[part] = id;
                if (same > 0 && forceInst == 0) part.partName = $"{part.partName} #{same + 1}";
                list.Add(part);
            }
            return list;
        }

        /// <summary>Cầm một bộ phận đang trên bàn ảo (đang lắp thì tự tháo, kéo theo nhánh con).</summary>
        public void Pick(ApparatusPart part)
        {
            if (part == null || part.fixture || !inst.ContainsKey(part)) return;
            if (Held != null) Drop();
            PushUndo();
            Held = part;
            var rest = rests.TryGetValue(part, out var r) ? r : (Vector3.zero, Quaternion.identity);
            heldYaw = (Quaternion.Inverse(table.rotation) * part.transform.rotation * Quaternion.Inverse(rest.Item2)).eulerAngles.y;
            if (part.Grab != null) part.Grab.SetHeld(true);
            else part.Detach();
            Say($"Đang cầm {Lower(part.partName)}" + (part.Subtree().Count > 1 ? $" (kèm {part.Subtree().Count - 1} bộ phận lắp vào nó)" : ""));
            Changed?.Invoke();
        }

        /// <summary>Thả bộ phận đang cầm: lắp vào khớp đang nhắm (có bóng mờ), không thì đặt xuống chỗ đang trỏ.</summary>
        public void Drop()
        {
            if (Held == null) return;
            var part = Held;
            Held = null;
            part.Grab?.SetHeld(false);
            if (AttachTarget != null && AttachMine != null && AttachMine.Part == part)
            {
                var t = AttachTarget;
                part.AttachTo(AttachMine, t);
                Say($"Đã lắp {Lower(part.partName)} vào {Lower(t.Part.partName)} ({AttachMine.Describe()} ↔ {t.Describe()})");
            }
            else Say($"Đã đặt {Lower(part.partName)} lên bàn");
            AttachMine = AttachTarget = null;
            ApparatusPart.NotifyChanged(part.Root);
            Changed?.Invoke();
        }

        /// <summary>Kiểm thử tự động: lắp bộ phận đang cầm bằng khớp mine vào khớp target (như khi bấm lúc có bóng mờ).</summary>
        public bool AttachHeld(Port mine, Port target)
        {
            if (Held == null || mine == null || target == null || mine.Part != Held || !Port.Compatible(mine, target) || !mine.Free || !target.Free) return false;
            AttachMine = mine;
            AttachTarget = target;
            Drop();
            return mine.Connected == target;
        }

        /// <summary>Kiểm thử tự động: đặt bộ phận đang cầm xuống bàn tại điểm cục bộ (x, z) của bàn, xoay yaw độ.</summary>
        public void PlaceHeld(float x, float z, float yaw = 0f)
        {
            if (Held == null) return;
            heldYaw = yaw;
            PoseHeld(table.TransformPoint(new Vector3(x, tableTop, z)));
            AttachMine = AttachTarget = null;
            Drop();
        }

        /// <summary>Bỏ bộ phận đang cầm (cả nhánh lắp vào nó; ống cao su bỏ cả hai đầu).</summary>
        public void DeleteHeld()
        {
            if (Held == null) return;
            var part = Held;
            Held = null;
            AttachMine = AttachTarget = null;
            string name = part.partName;
            Kill(part);
            Say($"Đã bỏ {Lower(name)}");
            Changed?.Invoke();
        }

        /// <summary>Xóa hết bàn ảo.</summary>
        public void ClearAll()
        {
            if (Held != null) { Held.Grab?.SetHeld(false); Held = null; }
            if (inst.Count == 0) return;
            PushUndo();
            KillAll();
            Say("Đã dọn sạch bàn");
            Changed?.Invoke();
        }

        void Kill(ApparatusPart part)
        {
            var victims = part.Subtree();
            // đầu kia của ống cao su (đang nằm ngoài nhánh này) cũng bỏ
            var extra = victims.Where(p => p.linked != null && !victims.Contains(p.linked)).Select(p => p.linked).Distinct().ToList();
            if (part.ParentJoint != null) part.Detach();
            foreach (var p in extra)
            {
                if (p.ParentJoint != null) p.Detach();
                Forget(p);
                Destroy(p.gameObject);
            }
            foreach (var p in victims) Forget(p);
            Destroy(part.gameObject);
        }

        void Forget(ApparatusPart p)
        {
            inst.Remove(p);
            rests.Remove(p);
            if (p != null) p.gameObject.SetActive(false);
        }

        void KillAll()
        {
            // vòi của xưởng đang nối với bộ phận sắp hủy: tháo ra ngay (hủy vật chỉ xảy ra cuối khung hình)
            foreach (var f in table.GetComponentsInChildren<ApparatusPart>(true))
                if (f.fixture)
                    foreach (var port in f.ports)
                        if (port != null && port.Connected != null && inst.ContainsKey(port.Connected.Part)) port.Connected = null;
            foreach (var p in inst.Keys.ToList())
                if (p != null)
                {
                    p.gameObject.SetActive(false);
                    Destroy(p.gameObject);
                }
            inst.Clear();
            rests.Clear();
            ResetFixtures();
        }

        void ResetFixtures()
        {
            foreach (var f in table.GetComponentsInChildren<ApparatusPart>(true))
                if (f.fixture)
                    foreach (var port in f.ports)
                        if (port != null && port.Connected == null) port.Connected = null;  // bỏ tham chiếu tới bộ phận đã hủy
        }

        /// <summary>Các bộ phận đang có trên bàn ảo (không kể vòi của xưởng).</summary>
        public List<ApparatusPart> Parts() => inst.Keys.Where(p => p != null && p.gameObject.activeInHierarchy).ToList();

        // =====================================================================
        // Hoàn tác: chụp lại cả bàn, dựng lại từ kho
        // =====================================================================

        void PushUndo()
        {
            undo.Add(Capture());
            if (undo.Count > MaxUndo) undo.RemoveAt(0);
        }

        public void Undo()
        {
            if (undo.Count == 0) { Say("Không còn gì để hoàn tác", true); return; }
            var snap = undo[undo.Count - 1];
            undo.RemoveAt(undo.Count - 1);
            Rebuild(snap);
            Say("Đã hoàn tác");
        }

        /// <summary>Ghi lại toàn bộ bàn ảo: mẫu, tư thế (so với bàn), khớp nối.</summary>
        public List<Rec> Capture() => CaptureParts(Parts(), table, p => inst[p], n => n);

        /// <summary>Ghi các bộ phận theo hệ tọa độ frame (bàn ảo, hoặc mốc của bộ đã gia công); fixtureName đổi tên vòi khi ghi.</summary>
        static List<Rec> CaptureParts(List<ApparatusPart> parts, Transform frame, Func<ApparatusPart, int> instOf, Func<string, string> fixtureName)
        {
            var index = new Dictionary<ApparatusPart, int>();
            for (int i = 0; i < parts.Count; i++) index[parts[i]] = i;
            var recs = new List<Rec>();
            foreach (var p in parts)
            {
                var r = new Rec
                {
                    lib = p.libraryId, sub = p.librarySub, inst = instOf(p), name = p.partName,
                    pos = frame.InverseTransformPoint(p.transform.position),
                    rot = Quaternion.Inverse(frame.rotation) * p.transform.rotation,
                    parent = -1, myPort = -1, targetPort = -1,
                };
                if (p.ParentJoint != null && p.ParentJoint.Connected != null)
                {
                    var tp = p.ParentJoint.Connected;
                    if (tp.Part.fixture)
                    {
                        r.fixture = fixtureName(tp.Part.name);
                        r.myPort = Array.IndexOf(p.ports, p.ParentJoint);
                        r.targetPort = Array.IndexOf(tp.Part.ports, tp);
                    }
                    else if (index.TryGetValue(tp.Part, out int pi))
                    {
                        r.parent = pi;
                        r.myPort = Array.IndexOf(p.ports, p.ParentJoint);
                        r.targetPort = Array.IndexOf(tp.Part.ports, tp);
                    }
                }
                recs.Add(r);
            }
            return recs;
        }

        // =====================================================================
        // Gia công ra bàn đích (F3) và lấy bộ về sửa
        // =====================================================================

        /// <summary>Một bộ dụng cụ đã gia công: mốc (hệ tọa độ tương ứng bàn ảo) đặt ở bàn đích.</summary>
        public class FabricatedSet
        {
            public int id;
            public string name, where;
            public Transform anchor;
        }

        public readonly List<FabricatedSet> Sets = new List<FabricatedSet>();
        [Tooltip("Đèn trạng thái của máy gia công trong ngăn kính (nháy khi đang gia công)")]
        public Renderer machineLight;
        public bool Busy { get; private set; }
        public float Progress { get; private set; }
        public FabricatedSet LastSet { get; private set; }
        public event Action<FabricatedSet> FabricationDone;
        int nextSet;
        MaterialPropertyBlock mpb;

        static bool TapLinked(ApparatusPart p) =>
            p.ParentJoint != null && p.ParentJoint.Connected != null && p.ParentJoint.Connected.Part != null && p.ParentJoint.Connected.Part.fixture;

        /// <summary>Hộp bao của bộ trên bàn ảo (cục bộ của bàn), không kể thân ống cao su và các đầu ống đang nối vòi xưởng.</summary>
        public bool DesignBounds(out Bounds b)
        {
            b = default;
            bool has = false;
            foreach (var p in Parts())
            {
                if (TapLinked(p)) continue;
                foreach (var r in p.GetComponentsInChildren<Renderer>())
                {
                    if (!r.enabled || r is ParticleSystemRenderer || r.GetComponentInParent<ApparatusPart>() != p) continue;
                    if (r.transform.parent != null && r.transform.parent.GetComponent<RubberHose>() != null) continue;
                    var wb = r.bounds;
                    for (int i = 0; i < 8; i++)
                    {
                        var c = table.InverseTransformPoint(new Vector3((i & 1) == 0 ? wb.min.x : wb.max.x, (i & 2) == 0 ? wb.min.y : wb.max.y, (i & 4) == 0 ? wb.min.z : wb.max.z));
                        if (!has) { b = new Bounds(c, Vector3.zero); has = true; } else b.Encapsulate(c);
                    }
                }
            }
            return has;
        }

        /// <summary>
        /// Chỗ trống cho bộ (hộp bao design) trên bàn đích: thử từ mép trước vào trong, từ giữa ra hai bên, mỗi bước 5 cm;
        /// trả về mốc (vị trí, hướng) ứng với gốc bàn ảo. Mặt trước bộ (phía camera xưởng) quay về phía người đứng.
        /// </summary>
        public bool FindSpot(FabricationDestination d, Bounds design, out Vector3 origin, out Quaternion rot)
        {
            origin = default;
            Vector3 fw = d.FrontWorld;
            if (fw.sqrMagnitude < 0.5f) fw = Vector3.back;
            rot = Quaternion.LookRotation(-fw, Vector3.up);
            Vector3 fl = d.front.normalized, sl = Vector3.Cross(Vector3.up, fl);
            bool frontIsX = Mathf.Abs(fl.x) > 0.5f;
            float depthAvail = frontIsX ? d.size.x : d.size.y, sideAvail = frontIsX ? d.size.y : d.size.x;
            float w = design.size.x, dd = design.size.z, h = Mathf.Max(0.05f, design.max.y - tableTop);
            if (w > sideAvail + 1e-3f || dd > depthAvail + 1e-3f) return false;
            float maxT = (depthAvail - dd) / 2, maxS = (sideAvail - w) / 2;
            var sides = new List<float> { 0f };
            for (float s = 0.05f; s <= maxS + 1e-4f; s += 0.05f) { sides.Add(s); sides.Add(-s); }
            for (float t = maxT; t >= -maxT - 1e-4f; t -= 0.05f)
                foreach (float s in sides)
                {
                    Vector3 worldC = d.transform.TransformPoint(fl * t + sl * s);
                    var half = new Vector3(w / 2 - 0.004f, h / 2, dd / 2 - 0.004f);
                    if (Blocked(worldC + Vector3.up * (h / 2 + 0.012f), half, rot)) continue;
                    var c = design.center;
                    origin = worldC - rot * new Vector3(c.x, tableTop, c.z);
                    return true;
                }
            return false;
        }

        bool Blocked(Vector3 center, Vector3 half, Quaternion rot)
        {
            foreach (var c in Physics.OverlapBox(center, half, rot, ~0, QueryTriggerInteraction.Ignore))
                if (!c.transform.IsChildOf(transform) && c.GetComponentInParent<CharacterController>() == null) return true;
            return false;
        }

        /// <summary>Bàn đích này còn đủ chỗ cho bộ đang lắp không.</summary>
        public bool HasRoom(FabricationDestination d) => DesignBounds(out var b) && FindSpot(d, b, out _, out _);

        /// <summary>Gia công: máy chạy ~1,6 s rồi bộ dụng cụ xuất hiện ở bàn đích, bàn ảo được dọn.</summary>
        public void Fabricate(FabricationDestination d)
        {
            if (!Busy && d != null) StartCoroutine(FabricateRoutine(d));
        }

        IEnumerator FabricateRoutine(FabricationDestination d)
        {
            if (Held != null) Drop();
            var parts = Parts();
            if (parts.Count == 0) { Say("Bàn ảo còn trống: lắp bộ dụng cụ trước rồi mới gia công", true); yield break; }
            if (!DesignBounds(out var b) || !FindSpot(d, b, out var origin, out var rot))
            {
                Say($"{d.label} không còn đủ chỗ cho bộ này: chọn bàn khác", true);
                yield break;
            }
            Busy = true;
            Say($"Máy đang gia công, sẽ đặt ra {Lower(d.label)}…");
            float t0 = Time.time;
            const float dur = 1.6f;
            while (Time.time - t0 < dur)
            {
                Progress = (Time.time - t0) / dur;
                SetMachineLight(0.5f + 0.5f * Mathf.Sin((Time.time - t0) * 18f));
                yield return null;
            }
            Progress = 1f;
            SetMachineLight(-1f);

            // đầu ống cao su đang nối vòi xưởng: tháo ra, ra bàn đích nối lại vào vòi thật cùng loại (nếu có)
            var taps = new List<(ApparatusPart part, int port, string fixture)>();
            foreach (var p in parts)
                if (TapLinked(p))
                {
                    taps.Add((p, Array.IndexOf(p.ports, p.ParentJoint), p.ParentJoint.Connected.Part.name));
                    p.Detach();
                }
            int id = ++nextSet;
            var zone = Khkt.Rendering.ZoneRoot.Of(Khkt.Rendering.ZoneMap.At(origin));
            var anchor = new GameObject($"Bộ dụng cụ gia công {id}").transform;
            anchor.SetParent(zone != null ? zone.transform : null, false);
            anchor.SetPositionAndRotation(origin, rot);
            var roots = parts.Where(p => p.ParentJoint == null).ToList();
            foreach (var r in roots)
            {
                Vector3 lp = table.InverseTransformPoint(r.transform.position);
                Quaternion lr = Quaternion.Inverse(table.rotation) * r.transform.rotation;
                r.transform.SetParent(anchor, true);
                r.transform.SetPositionAndRotation(anchor.TransformPoint(lp), anchor.rotation * lr);
            }
            int linkedTaps = 0, loose = 0;
            foreach (var (p, port, fx) in taps)
            {
                string want = fx.Replace(" (xưởng)", "");
                ApparatusPart best = null;
                float bestD = 2.5f;
                foreach (var f in Khkt.Rendering.ZoneRoot.FindAll<ApparatusPart>())
                {
                    if (!f.fixture || f.name != want || f.ports.Length == 0 || !f.ports[0].Free) continue;
                    float dist = Vector3.Distance(f.transform.position, anchor.position);
                    if (dist < bestD) { bestD = dist; best = f; }
                }
                if (best != null && port >= 0)
                {
                    p.AttachTo(p.ports[port], best.ports[0]);
                    linkedTaps++;
                }
                else
                {
                    LayOnSurface(p);
                    loose++;
                }
            }
            foreach (var p in parts)
            {
                p.fabricatedSet = id;
                if (p.Grab != null) p.Grab.SetHome(p.transform.position, p.transform.rotation);
            }
            var main = roots.OrderByDescending(r => r.Subtree().Count).First();
            var vessel = main.Subtree().FirstOrDefault(x => x.Container != null && x.Container.reactive);
            var set = new FabricatedSet
            {
                id = id,
                name = $"Bộ dụng cụ {id} ({(vessel != null ? vessel.partName : main.partName)}, {parts.Count} bộ phận)",
                where = d.label,
                anchor = anchor,
            };
            Sets.Add(set);
            LastSet = set;
            inst.Clear();
            rests.Clear();
            undo.Clear();
            ResetFixtures();
            Busy = false;
            Progress = 0f;
            foreach (var r in roots) ApparatusPart.NotifyChanged(r);
            Changed?.Invoke();
            string extra = linkedTaps > 0 ? $"; đã nối {linkedTaps} ống cao su vào vòi của bàn" : "";
            if (loose > 0) extra += $"; {loose} đầu ống cao su để trống trên bàn (nối vào vòi khi cần)";
            Close();
            LabMessages.Say($"Đã gia công {Lower(set.name)} tại {Lower(d.label)}{extra}", LabMessages.Tone.Good);
            FabricationDone?.Invoke(set);
        }

        /// <summary>Đặt nằm trên mặt đỡ ngay bên dưới (đầu ống cao su không có vòi để nối).</summary>
        static void LayOnSurface(ApparatusPart p)
        {
            var pos = p.transform.position;
            float y = pos.y;
            foreach (var h in Physics.RaycastAll(pos + Vector3.up * 0.05f, Vector3.down, 1.5f, ~0, QueryTriggerInteraction.Ignore))
                if (h.collider.GetComponentInParent<ApparatusPart>() != p) { y = h.point.y; break; }
            Vector3 flat = p.linked != null ? Vector3.ProjectOnPlane(pos - p.linked.transform.position, Vector3.up) : Vector3.forward;
            if (flat.sqrMagnitude < 1e-4f) flat = Vector3.forward;
            p.transform.SetPositionAndRotation(new Vector3(pos.x, y + 0.006f, pos.z), Quaternion.LookRotation(Vector3.up, -flat.normalized));
        }

        void SetMachineLight(float k)
        {
            if (machineLight == null) return;
            mpb ??= new MaterialPropertyBlock();
            machineLight.GetPropertyBlock(mpb);
            Color c = k < 0 ? new Color(0.25f, 0.95f, 0.45f) : Color.Lerp(new Color(0.2f, 0.6f, 1f), new Color(1f, 0.75f, 0.2f), k);
            mpb.SetColor("_BaseColor", c);
            mpb.SetColor("_EmissionColor", c * 2.2f);
            machineLight.SetPropertyBlock(mpb);
        }

        /// <summary>Các bộ đã gia công còn trong phòng (bỏ bộ đã bị dọn hết).</summary>
        public List<FabricatedSet> LiveSets()
        {
            var alive = new HashSet<int>(Khkt.Rendering.ZoneRoot.FindAll<ApparatusPart>().Select(p => p.fabricatedSet));
            Sets.RemoveAll(s => !alive.Contains(s.id) || s.anchor == null);
            return Sets.ToList();
        }

        /// <summary>Lấy bộ đã gia công về bàn ảo để sửa. Trả về lời báo lỗi (null: thành công).</summary>
        public string Recall(FabricatedSet s)
        {
            if (Busy) return "Máy đang gia công, chờ một chút";
            if (Parts().Count > 0) return "Bàn ảo đang có bộ phận: gia công hoặc Xóa hết trước khi lấy bộ về sửa";
            var parts = Khkt.Rendering.ZoneRoot.FindAll<ApparatusPart>().Where(p => p.fabricatedSet == s.id).ToList();
            if (parts.Count == 0 || s.anchor == null) { Sets.Remove(s); return "Bộ này không còn trong phòng"; }
            var wet = parts.FirstOrDefault(p => p.Container != null && p.Container.VolumeMl > 0.5f);
            if (wet != null) return $"{wet.partName} còn {wet.Container.VolumeMl:0} ml dung dịch: đổ hết rồi mới mang về sửa";
            if (parts.Any(p => p.Grab != null && p.Grab.IsHeld)) return "Đặt bộ dụng cụ xuống rồi mới mang về sửa";
            var ids = new Dictionary<ApparatusPart, int>();
            int k = 0;
            foreach (var p in parts)
                if (!ids.ContainsKey(p))
                {
                    ids[p] = ++k;
                    if (p.linked != null) ids[p.linked] = k;
                }
            var recs = CaptureParts(parts, s.anchor, p => ids.TryGetValue(p, out int v) ? v : ++k, WorkshopFixtureName);
            for (int i = 0; i < recs.Count; i++)
            {
                var r = recs[i];
                if (r.parent >= 0 || !string.IsNullOrEmpty(r.fixture)) continue;
                // bộ phận rời bị mang đi chỗ khác (bàn khác, sàn): đưa về trong mặt bàn ảo
                r.pos.x = Mathf.Clamp(r.pos.x, -tableHalf.x, tableHalf.x);
                r.pos.z = Mathf.Clamp(r.pos.z, -tableHalf.y, tableHalf.y);
                if (Mathf.Abs(r.pos.y - tableTop) > 0.05f && parts[i].ParentJoint == null) r.pos.y = tableTop;
                recs[i] = r;
            }
            foreach (var p in parts) if (TapLinked(p)) p.Detach();
            foreach (var p in parts)
                if (p != null && p.ParentJoint == null)
                {
                    p.gameObject.SetActive(false);
                    Destroy(p.gameObject);
                }
            Destroy(s.anchor.gameObject);
            Sets.Remove(s);
            undo.Clear();
            Rebuild(recs);
            Say($"Đã lấy {Lower(s.name)} về bàn ảo để sửa");
            return null;
        }

        /// <summary>"Vòi cấp nước 1" (bàn thật) → "Vòi cấp nước (xưởng) 1" (bàn ảo).</summary>
        static string WorkshopFixtureName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Contains("(xưởng)")) return name;
            int sp = name.LastIndexOf(' ');
            return sp > 0 && int.TryParse(name.Substring(sp + 1), out _) ? $"{name.Substring(0, sp)} (xưởng){name.Substring(sp)}" : name + " (xưởng)";
        }

        /// <summary>Dựng lại bàn ảo từ bản ghi (hoàn tác; sau này: mở thiết kế đã lưu, lấy bộ về sửa).</summary>
        public void Rebuild(List<Rec> recs)
        {
            if (Held != null) { Held.Grab?.SetHeld(false); Held = null; }
            AttachMine = AttachTarget = null;
            KillAll();
            var made = new Dictionary<int, List<ApparatusPart>>();
            foreach (var r in recs)
            {
                if (made.ContainsKey(r.inst)) continue;
                var e = library.Get(r.lib);
                made[r.inst] = e != null ? Spawn(e, table.TransformPoint(new Vector3(0, tableTop, 0)), table.eulerAngles.y, r.inst) : new List<ApparatusPart>();
            }
            var parts = new ApparatusPart[recs.Count];
            for (int i = 0; i < recs.Count; i++)
            {
                var r = recs[i];
                var list = made[r.inst];
                var p = list.FirstOrDefault(x => x.librarySub == r.sub);
                if (p == null) continue;
                parts[i] = p;
                p.partName = r.name;
                p.transform.SetPositionAndRotation(table.TransformPoint(r.pos), table.rotation * r.rot);
            }
            int Depth(int i) { int d = 0; for (int k = recs[i].parent; k >= 0 && d < 64; k = recs[k].parent) d++; return d; }
            foreach (int i in Enumerable.Range(0, recs.Count).Where(i => recs[i].myPort >= 0).OrderBy(Depth))
            {
                var r = recs[i];
                var p = parts[i];
                if (p == null) continue;
                Port target = null;
                if (r.parent >= 0 && parts[r.parent] != null) target = parts[r.parent].ports[r.targetPort];
                else if (!string.IsNullOrEmpty(r.fixture))
                {
                    var f = table.GetComponentsInChildren<ApparatusPart>(true).FirstOrDefault(x => x.fixture && x.name == r.fixture);
                    if (f != null) target = f.ports[r.targetPort];
                }
                if (target != null) p.AttachTo(p.ports[r.myPort], target);
            }
            Changed?.Invoke();
        }

        // =====================================================================
        // Bảng kiểm tra
        // =====================================================================

        /// <summary>Báo cáo bộ lắp trên bàn ảo (chữ có định dạng cho bảng bên phải): số bộ phận, hệ kín / hở, đổ, khớp quan trọng còn trống.</summary>
        public string Report()
        {
            var parts = Parts();
            if (parts.Count == 0) return "<color=#9fb4c8>Bàn còn trống. Chọn bộ phận ở bảng bên trái để bắt đầu.</color>";
            var roots = new List<ApparatusPart>();
            foreach (var p in parts)
            {
                if (p == Held || p.InSubtreeOf(Held)) continue;
                var r = p.Root;
                if (!roots.Contains(r)) roots.Add(r);
            }
            var sb = new StringBuilder();
            sb.Append($"Trên bàn: <b>{parts.Count}</b> bộ phận, <b>{roots.Count(r => !r.fixture)}</b> cụm\n");
            foreach (var root in roots)
            {
                var sub = root.Subtree().Where(x => !x.fixture).ToList();
                if (sub.Count == 0) continue;
                bool single = sub.Count == 1 && sub[0].Container == null;
                if (single) continue;
                string title = root.fixture ? $"Nối vào {Lower(root.partName)}" : root.partName;
                sb.Append($"\n<b>{title}</b> · {sub.Count} bộ phận\n");
                var vessel = sub.FirstOrDefault(x => x.Container != null && x.Container.reactive);
                if (vessel != null)
                {
                    var open = vessel.Openings();
                    if (open.Count == 0) sb.Append($"  <color=#ff8a80>● {vessel.partName}: HỆ KÍN — không được đun nóng</color>\n");
                    else sb.Append($"  <color=#7ef0a0>● {vessel.partName}: hệ hở</color> <color=#9fb4c8>(thông ra ở {Lower(open[0].label)})</color>\n");
                }
                if (!root.fixture)
                {
                    if (!AssemblyManager.Footprint(sub, out var rect, out _))
                    {
                        if (sub.Count > 1) sb.Append("  <color=#ffc96b>▲ Không có đế chạm mặt bàn</color>\n");
                    }
                    else
                    {
                        var com = AssemblyManager.CenterOfMass(sub, out float mass);
                        var q = new Vector2(com.x, com.z);
                        float out_ = Mathf.Max(rect.xMin - q.x, q.x - rect.xMax, rect.yMin - q.y, q.y - rect.yMax, 0f);
                        if (out_ > 0.005f) sb.Append($"  <color=#ff8a80>▲ Trọng tâm lệch {out_ * 100:0} cm ra ngoài đế: ra bàn thật sẽ đổ</color>\n");
                        else if (sub.Count > 2) sb.Append($"  <color=#7ef0a0>● Đứng vững</color> <color=#9fb4c8>({mass:0} g)</color>\n");
                    }
                }
                foreach (var w in Warnings(sub)) sb.Append($"  <color=#ffc96b>▲ {w}</color>\n");
            }
            return sb.ToString();
        }

        static IEnumerable<string> Warnings(List<ApparatusPart> sub)
        {
            foreach (var p in sub)
            {
                var water = p.ports.Where(x => x != null && x.type == PortType.VoiNhanh && x.channel == 1).ToList();
                if (water.Count > 0 && water.Any(x => x.Free))
                    yield return $"{p.partName}: {string.Join(", ", water.Where(x => x.Free).Select(x => Lower(x.label)))} chưa nối ống cao su";
                if (p.ports.Any(x => x != null && x.type == PortType.KhopNhamCai && x.size < 20 && x.Free) && p.ports.Length >= 3)
                    yield return $"{p.partName}: chưa cắm nhiệt kế";
                if (p.linked != null && p.librarySub == 0)
                {
                    bool a = p.ports.Any(x => x != null && x.Connected != null), b = p.linked.ports.Any(x => x != null && x.Connected != null);
                    if (a != b) yield return $"{p.partName.Replace(" (đầu A)", "")}: một đầu còn trống (ra bàn đích nối vào vòi, hoặc để dẫn khí ra)";
                }
            }
        }

        // =====================================================================
        // Ảnh nhỏ cho bảng bộ phận (chụp một lần khi mở lần đầu)
        // =====================================================================

        IEnumerator MakeThumbnails()
        {
            if (library == null || thumbCamera == null || thumbStage == null) yield break;
            foreach (var e in library.entries)
            {
                if (e.thumbnail != null) continue;
                var spawned = library.Spawn(e, thumbStage, thumbStage.position, thumbStage.eulerAngles.y + 20f);
                yield return null;  // ống cao su dựng thân ống ở Start
                bool has = false;
                var b = new Bounds();
                foreach (var (p, _, _) in spawned)
                    foreach (var r in p.GetComponentsInChildren<Renderer>())
                    {
                        if (r is ParticleSystemRenderer || !r.enabled || r.GetComponent<TextMesh>() != null) continue;
                        if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
                    }
                if (has)
                {
                    var rt = new RenderTexture(160, 160, 16, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "Ảnh nhỏ " + e.id };
                    rt.Create();
                    float radius = Mathf.Max(0.03f, b.extents.magnitude);
                    var rot = Quaternion.Euler(24f, thumbStage.eulerAngles.y - 30f, 0);
                    float dist = radius / Mathf.Sin(thumbCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.02f;
                    thumbCamera.transform.SetPositionAndRotation(b.center - rot * Vector3.forward * dist, rot);
                    thumbCamera.nearClipPlane = Mathf.Max(0.01f, dist - radius * 1.5f);
                    thumbCamera.farClipPlane = dist + radius * 2f;
                    thumbCamera.targetTexture = rt;
                    thumbCamera.Render();
                    thumbCamera.targetTexture = null;
                    e.thumbnail = rt;
                    if (ui != null) ui.ThumbnailReady(e);
                }
                foreach (var (p, _, _) in spawned)
                    if (p != null)
                    {
                        p.gameObject.SetActive(false);
                        Destroy(p.gameObject);
                    }
            }
            ThumbnailsReady = true;
        }

        // =====================================================================

        public void Say(string text, bool warn = false)
        {
            Message = text;
            MessageWarn = warn;
            MessageTime = Time.time;
        }

        static string Lower(string s) => string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s.Substring(1);
    }
}
