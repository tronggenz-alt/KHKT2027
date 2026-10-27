using System.Collections.Generic;
using Khkt.Input;
using Khkt.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Khkt.Demo
{
    /// <summary>
    /// Bàn tay ảo điều khiển bằng chuột (chế độ máy tính, mô phỏng tay cầm VR).
    /// Tay rỗng: trỏ vào dụng cụ để viền sáng và xem card; bấm để cầm; lăn chuột trên khóa buret để mở/đóng.
    /// Tay đang cầm: trỏ vào chỗ đặt rồi bấm để đặt; trỏ vào vật chứa rồi giữ chuột để rót / hút / xả.
    /// Mọi thao tác chỉ làm được khi hợp lý (ví dụ phải lắp buret lên giá mới nạp dung dịch được).
    /// Tay cầm Wi-Fi / tay cầm chơi game (PadInput): cò thay chuột trái, trỏ bằng tâm màn hình, cần nhìn vặn khóa buret.
    /// </summary>
    public class HandController : MonoBehaviour
    {
        public enum Act { None, Pick, Swirl, Use, Stopcock, Place, PlaceSurface, Pour, Suck, PipetteDrain, Drop, Spray, Scoop, Bubble, Measure, PaperTest, Attach }

        public Camera viewCamera;
        public HandRig hand;
        public HoverOutline outline;
        public LabProcedure procedure;
        public ModernUI ui;
        public float reach = 3.2f;
        public float minHandDistance = 0.3f;
        public float maxHandDistance = 1.0f;
        public float pourRateMlPerS = 4f;
        public float scrollStep = 0.04f;
        public float keyRate = 0.25f;
        [Tooltip("Vật liệu bóng mờ xem trước chỗ lắp ráp (trong suốt)")]
        public Material ghostMaterial;

        public Grabbable Held { get; private set; }
        public Act Current { get; private set; }
        /// <summary>Đang thực hiện thao tác giữ (rót, hút, xả, lắc).</summary>
        public bool Acting { get; private set; }
        public Transform HoverRoot { get; private set; }
        public EquipmentInfo HoverInfo { get; private set; }
        public LabContainer TargetContainer { get; private set; }
        public PlaceSocket TargetSocket { get; private set; }
        /// <summary>Lắp ráp: khớp của vật đang cầm và khớp đích sẽ lắp vào (khi Current == Attach).</summary>
        public Khkt.Assembly.Port AttachMine { get; private set; }
        public Khkt.Assembly.Port AttachTarget { get; private set; }
        public readonly List<(string key, string text)> Hints = new List<(string, string)>();

        /// <summary>Kiểm thử tự động: ép trỏ vào điểm này (thế giới) thay vì đọc chuột.</summary>
        public Vector3? OverrideAimPoint { get; set; }
        /// <summary>Kiểm thử tự động: giả lập giữ chuột trái.</summary>
        public bool OverrideHold { get; set; }
        public bool OverrideShift { get; set; }

        PlaceSocket[] sockets;
        IDemoUsable usable;
        StopcockHandle stopcock;
        Grabbable hoverGrab;
        RaycastHit hit;
        bool hasHit;
        float pressTime = -1f, dropCooldown;
        bool wasDown;
        bool swirlStarted;
        Vector3 handPos;
        float tilt;
        float heldYaw;  // xoay bộ phận đang cầm (lăn chuột / R E) để chọn chiều khi lắp
        Transform pourStream;

        void Start()
        {
            sockets = Khkt.Rendering.ZoneRoot.FindAll<PlaceSocket>();
            handPos = viewCamera.transform.position + viewCamera.transform.forward * 0.5f;
            var s = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            s.name = "Dòng rót";
            Destroy(s.GetComponent<Collider>());
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(lit) { hideFlags = HideFlags.DontSave };
            m.SetColor("_BaseColor", new Color(0.8f, 0.9f, 1f, 1f));
            m.SetFloat("_Smoothness", 0.9f);
            s.GetComponent<Renderer>().sharedMaterial = m;
            pourStream = s.transform;
            pourStream.gameObject.SetActive(false);
            PadInput.ControllerChanged += OnControllerChanged;
            PadInput.Notice += OnPadNotice;
        }

        void OnDestroy()
        {
            PadInput.ControllerChanged -= OnControllerChanged;
            PadInput.Notice -= OnPadNotice;
        }

        void OnPadNotice(string text) => Say(text);

        void OnControllerChanged(bool connected, string who)
        {
            if (connected) Say($"Đã nối tay cầm ({who})", LabProcedure.Tone.Good);
            else Say("Mất kết nối tay cầm", LabProcedure.Tone.Warn);
        }

        void Update()
        {
            var mouse = Mouse.current;
            var kb = Keyboard.current;
            bool uiOpen = Khkt.Environment.DesktopViewer.UiCapture > 0;
            bool looking = uiOpen || (mouse != null && mouse.rightButton.isPressed && !Khkt.Environment.DesktopViewer.MouseLook);
            float dt = Time.deltaTime;

            if (!looking) Raycast(mouse);
            else if (uiOpen) hasHit = false;
            Decide();
            PadInput.LookYUsedByTool = Current == Act.Stopcock;
            PadInput.AimHasTarget = Current != Act.None;

            bool shift = OverrideShift || (kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed)) || PadInput.Shift;
            bool down = OverrideHold || (!looking && mouse != null && mouse.leftButton.isPressed) || (!uiOpen && PadInput.Use);
            bool pressed = down && !wasDown;
            bool released = !down && wasDown;
            wasDown = down;
            if (pressed) pressTime = Time.time;

            Acting = false;
            dropCooldown -= dt;
            switch (Current)
            {
                case Act.Pick:
                    if (pressed) Pick(hoverGrab);
                    break;
                case Act.Swirl:
                    // Giữ lâu là lắc bình tại chỗ, bấm nhanh là cầm lên
                    if (down && pressTime >= 0 && Time.time - pressTime > 0.2f) { swirlStarted = true; Acting = true; }
                    if (released && !swirlStarted && pressTime >= 0) Pick(hoverGrab);
                    break;
                case Act.Use:
                    if (pressed) usable.Use();
                    break;
                case Act.Stopcock:
                    if (mouse != null)
                    {
                        float scroll = mouse.scroll.ReadValue().y;
                        if (Mathf.Abs(scroll) > 0.01f) stopcock.buret.AdjustOpening(Mathf.Sign(scroll) * scrollStep * (shift ? 0.25f : 1f));
                    }
                    float padY = PadInput.Look.y;  // đẩy cần nhìn lên: mở thêm, kéo xuống: đóng bớt
                    if (Mathf.Abs(padY) > 0.05f) stopcock.buret.AdjustOpening(padY * keyRate * (shift ? 0.25f : 1f) * dt);
                    break;
                case Act.Place:
                    if (pressed) PlaceInSocket(TargetSocket);
                    break;
                case Act.PlaceSurface:
                    if (pressed) PlaceOnSurface(hit.point);
                    break;
                case Act.Pour:
                    if (down) { Acting = true; Pour(dt); }
                    break;
                case Act.Suck:
                    if (down)
                    {
                        Acting = true;
                        var p = Held.GetComponent<Pipette>();
                        if (shift) p.Drain(TargetContainer, dt, trim: true);
                        else if (p.Suck(TargetContainer, dt) <= 0 && p.container.FreeMl < 0.05f)
                            procedure?.Say("Pipet đã đầy, quả bóp không hút thêm được", LabProcedure.Tone.Warn);
                    }
                    break;
                case Act.PipetteDrain:
                    if (down)
                    {
                        Acting = true;
                        Held.GetComponent<Pipette>().Drain(TargetContainer, dt, trim: shift);
                    }
                    break;
                case Act.Drop:
                    if (pressed && dropCooldown <= 0)
                    {
                        Held.GetComponent<Dropper>().DropInto(TargetContainer);
                        dropCooldown = 0.25f;
                    }
                    break;
                case Act.Spray:
                    if (down) { Acting = true; Held.GetComponent<WashBottle>().SprayInto(TargetContainer, dt); }
                    break;
                case Act.Scoop:
                    if (pressed && dropCooldown <= 0)
                    {
                        if (!Held.GetComponent<ChemicalBottle>().ScoopInto(TargetContainer)) Say("Lọ đã hết hóa chất");
                        dropCooldown = 0.3f;
                    }
                    break;
                case Act.Bubble:
                    if (down)
                    {
                        Acting = true;
                        var cb = Held.GetComponent<ChemicalBottle>();
                        cb.BubbleInto(TargetContainer, dt);
                        if (TargetContainer == null && cb.Entry != null && cb.Entry.HasHazard("doc") && !FumeHoodZone.Covers(Held.transform.position))
                            Say("Khí độc đang xả ra phòng! Làm trong tủ hút", LabProcedure.Tone.Warn);
                    }
                    break;
                case Act.Measure:
                    Held.GetComponent<MeterProbe>().Read(TargetContainer);
                    break;
                case Act.Attach:
                    if (pressed) AttachHeld(AttachMine, AttachTarget);
                    break;
                case Act.PaperTest:
                    if (pressed)
                    {
                        string r = Held.GetComponent<TestPaper>().Dip(TargetContainer);
                        if (!string.IsNullOrEmpty(r)) Say(r, LabProcedure.Tone.Good);
                    }
                    break;
            }
            if (released || !down)
            {
                if (released) pressTime = -1f;
                swirlStarted = false;
            }

            // Phím chung: lắc bình (F), khóa buret (↑ ↓ Space), trả đồ về chỗ cũ (Q)
            if (procedure != null && procedure.swirl != null)
            {
                bool swirlKey = (kb != null && kb.fKey.isPressed) || (!uiOpen && PadInput.Swirl);
                bool swirlHold = Current == Act.Swirl && swirlStarted;
                procedure.swirl.ManualIntensity = Mathf.MoveTowards(procedure.swirl.ManualIntensity,
                    swirlKey || swirlHold ? 1f : 0f, 3f * dt);
            }
            if (procedure != null && procedure.buret != null)
            {
                float keys = kb != null ? (kb.upArrowKey.isPressed ? 1 : 0) - (kb.downArrowKey.isPressed ? 1 : 0) : 0;
                if (!uiOpen) keys = Mathf.Clamp(keys + PadInput.StopcockAxis, -1, 1);
                if (keys != 0) procedure.buret.AdjustOpening(keys * keyRate * (shift ? 0.25f : 1f) * dt);
                if (kb != null && kb.spaceKey.wasPressedThisFrame) procedure.buret.Close();
            }
            if (((kb != null && kb.qKey.wasPressedThisFrame) || (!uiOpen && PadInput.BackPressed)) && Held != null) ReturnHome();
            if (!uiOpen)
            {
                bool enter = kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame);
                if (procedure != null && (enter || PadInput.RecordPressed)) procedure.RecordReading();
                if (kb != null && procedure != null && procedure.Active && kb.rKey.wasPressedThisFrame && HeldPart == null) procedure.NewRun();
                if (ui != null && ((kb != null && kb.hKey.wasPressedThisFrame) || PadInput.HelpPressed)) ui.ToggleHelp();
                if (kb != null && ui != null && kb.gKey.wasPressedThisFrame) ui.ToggleGraph();
            }

            // Lắp ráp: xoay bộ phận đang cầm, bóng mờ xem trước chỗ lắp
            if (HeldPart != null && !uiOpen)
            {
                float turn = 0;
                if (mouse != null && !looking) turn += Mathf.Sign(mouse.scroll.ReadValue().y) * (Mathf.Abs(mouse.scroll.ReadValue().y) > 0.01f ? 1 : 0);
                if (kb != null) turn += (kb.rKey.wasPressedThisFrame ? 1 : 0) - (kb.eKey.wasPressedThisFrame ? 1 : 0);
                heldYaw += turn * 15f;
            }
            var fab = Khkt.Assembly.FabricationWorkspace.Instance;
            if (fab != null && fab.IsOpen) { }  // khu chế tạo đang mở: bóng mờ do xưởng ảo điều khiển
            else if (Current == Act.Attach && AttachMine != null && AttachTarget != null)
            {
                Khkt.Assembly.ApparatusPart.ComputePose(AttachMine, AttachTarget, out var gp, out var gr);
                Khkt.Assembly.AssemblyGhost.Show(HeldPart, gp, gr, ghostMaterial);
            }
            else Khkt.Assembly.AssemblyGhost.Hide();

            UpdatePose(dt);
            UpdatePadFeedback();
            BuildHints();
            if (outline != null) outline.SetTarget(TargetSocket != null ? null : HoverRoot);
            foreach (var s in sockets)
                s.Highlight(Held != null && s.Accepts(Held), s == TargetSocket);
        }

        // ---------- Trỏ chuột ----------

        void Raycast(Mouse mouse)
        {
            Ray ray;
            if (OverrideAimPoint.HasValue)
            {
                Vector3 o = viewCamera.transform.position;
                ray = new Ray(o, (OverrideAimPoint.Value - o).normalized);
            }
            else if (mouse != null && !Khkt.Environment.DesktopViewer.MouseLook && !PadInput.AimFromCenter)
                ray = viewCamera.ScreenPointToRay(mouse.position.ReadValue());
            else ray = viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            hasHit = Physics.Raycast(ray, out hit, reach, ~0, QueryTriggerInteraction.Collide);
            lastRay = ray;
        }

        Ray lastRay;

        void Decide()
        {
            Current = Act.None;
            HoverRoot = null;
            HoverInfo = null;
            TargetContainer = null;
            TargetSocket = null;
            AttachMine = null;
            AttachTarget = null;
            usable = null;
            stopcock = null;
            hoverGrab = null;
            if (!hasHit) return;

            var col = hit.collider;
            var socket = col.GetComponentInParent<PlaceSocket>();
            if (socket != null && col != socket.zone) socket = null;
            var container = col.GetComponentInParent<LabContainer>();
            var grab = Khkt.Assembly.ApparatusPart.WorldGrab(col.GetComponentInParent<Grabbable>());  // bộ đã gia công: cầm cả bộ
            stopcock = col.GetComponent<StopcockHandle>();
            usable = col.GetComponentInParent<IDemoUsable>();
            var surface = col.GetComponentInParent<PlaceSurface>();
            HoverInfo = col.GetComponentInParent<EquipmentInfo>();
            HoverRoot = grab != null ? grab.transform : HoverInfo != null ? HoverInfo.transform : null;

            if (Held == null)
            {
                if (stopcock != null && stopcock.buret != null && stopcock.buret.mounted) { Current = Act.Stopcock; return; }
                // tay khóa phễu chiết / phễu nhỏ giọt: bấm là mở / đóng khóa, không cầm cả phễu
                if (col.GetComponent<Khkt.Assembly.FlowValve>() is Khkt.Assembly.FlowValve valve) { usable = valve; Current = Act.Use; return; }
                if (grab != null)
                {
                    hoverGrab = grab;
                    bool underBuret = grab.Socket != null && !grab.Socket.mountsBuret && grab.GetComponentInChildren<SwirlDetector>() != null;
                    Current = underBuret ? Act.Swirl : Act.Pick;
                    return;
                }
                if (usable != null) { Current = Act.Use; return; }
                return;
            }

            if (socket != null && socket.Accepts(Held)) { TargetSocket = socket; Current = Act.Place; HoverRoot = null; return; }
            var heldContainer = Held.GetComponent<LabContainer>();
            // Lắp ráp: tia ngắm đi gần một khớp trống hợp với vật đang cầm (trừ khi đang cầm bình có dung dịch trỏ vào bình khác: rót)
            if (HeldPart != null && Khkt.Assembly.ApparatusPart.FindAttach(HeldPart, lastRay, 0.07f, reach, out var am, out var at))
            {
                bool pour = heldContainer != null && heldContainer.VolumeMl > 0.3f && container != null && container != heldContainer;
                if (!pour)
                {
                    AttachMine = am;
                    AttachTarget = at;
                    Current = Act.Attach;
                    HoverRoot = at.Part.transform;
                    return;
                }
            }
            if (container != null && container != heldContainer)
            {
                TargetContainer = container;
                if (Held.GetComponent<Pipette>() != null) { Current = container.isStock ? Act.Suck : Act.PipetteDrain; return; }
                if (Held.GetComponent<Dropper>() != null) { Current = Act.Drop; return; }
                if (Held.GetComponent<WashBottle>() != null) { Current = Act.Spray; return; }
                var bottle = Held.GetComponent<ChemicalBottle>();
                if (bottle != null && bottle.IsSolid) { Current = Act.Scoop; return; }
                if (bottle != null && bottle.IsGas) { Current = Act.Bubble; return; }
                if (Held.GetComponent<MeterProbe>() != null) { Current = Act.Measure; return; }
                if (Held.GetComponent<TestPaper>() != null) { Current = Act.PaperTest; return; }
                if (heldContainer != null) { Current = Act.Pour; return; }
            }
            if (surface != null) { Current = Act.PlaceSurface; HoverRoot = null; }
        }

        // ---------- Cầm, đặt ----------

        public void Pick(Grabbable g)
        {
            if (g == null || Held != null) return;
            var buret = g.GetComponent<BuretStopcock>();
            if (buret != null && buret.HasLiquid && g.Socket != null && g.Socket.mountsBuret)
            {
                procedure?.Say("Buret đang chứa dung dịch: xả hết rồi mới tháo khỏi giá", LabProcedure.Tone.Warn);
                return;
            }
            Held = g;
            g.SetHeld(true);
            tilt = 0;
            heldYaw = 0;
            // vật ở khu đang được gỡ (cầm hộ từ xa): đánh thức về khu người xem đang đứng
            if (!g.gameObject.activeInHierarchy && viewCamera != null) Khkt.Rendering.ZoneStreamer.Wake(g.transform, viewCamera.transform.position);
        }

        public void PlaceInSocket(PlaceSocket socket)
        {
            if (Held == null || socket == null || !socket.Accepts(Held)) return;
            var g = Held;
            Held = null;
            g.SetHeld(false);
            socket.Put(g);
        }

        public void PlaceOnSurface(Vector3 point)
        {
            if (Held == null || !CanPutDown(Held)) return;
            var g = Held;
            Held = null;
            g.SetHeld(false);
            if (g.liesOnTable)
            {
                Vector3 d = Flat(viewCamera.transform.right);
                g.transform.SetPositionAndRotation(point + Vector3.up * g.lyingRadiusM - d * g.lengthM / 2,
                                                   Quaternion.FromToRotation(Vector3.up, d));
            }
            else g.transform.SetPositionAndRotation(point + Vector3.up * 0.001f, Quaternion.Euler(0, g.HomeRotation.eulerAngles.y + heldYaw, 0));
            var part = g.GetComponent<Khkt.Assembly.ApparatusPart>();
            if (part != null) Khkt.Assembly.ApparatusPart.NotifyChanged(part.Root);
        }

        /// <summary>Bộ phận lắp ráp đang cầm (null nếu không cầm hoặc vật không phải bộ phận lắp ráp).</summary>
        public Khkt.Assembly.ApparatusPart HeldPart => Held != null ? Held.GetComponent<Khkt.Assembly.ApparatusPart>() : null;

        /// <summary>Lắp bộ phận đang cầm: khớp mine của nó vào khớp target (kiểm thử tự động gọi trực tiếp được).</summary>
        public void AttachHeld(Khkt.Assembly.Port mine, Khkt.Assembly.Port target)
        {
            var part = HeldPart;
            if (part == null || mine == null || target == null || mine.Part != part || !Khkt.Assembly.Port.Compatible(mine, target)) return;
            var g = Held;
            Held = null;
            g.SetHeld(false);
            part.AttachTo(mine, target);
            Say($"Đã lắp {Lower(part.partName)} vào {Lower(target.Part.partName)} ({mine.Describe()} ↔ {target.Describe()}). {part.DescribeAssembly()}",
                LabProcedure.Tone.Good);
        }

        public void ReturnHome()
        {
            if (Held == null || !CanPutDown(Held)) return;
            var g = Held;
            Held = null;
            g.SetHeld(false);
            if (g.HomeSocket != null && g.HomeSocket.Occupant == null) g.HomeSocket.Put(g);
            else g.transform.SetPositionAndRotation(g.HomePosition, g.HomeRotation);
        }

        bool CanPutDown(Grabbable g)
        {
            var p = g.GetComponent<Pipette>();
            if (p != null && !p.IsEmpty)
            {
                procedure?.Say("Pipet còn dung dịch: xả hết trước khi đặt xuống", LabProcedure.Tone.Warn);
                return false;
            }
            var c = g.GetComponent<LabContainer>();
            if (g.liesOnTable && c != null && c.VolumeMl > 0.3f)
            {
                procedure?.Say("Dụng cụ đang chứa dung dịch, đặt nằm sẽ đổ ra", LabProcedure.Tone.Warn);
                return false;
            }
            return true;
        }

        // ---------- Rót ----------

        void Pour(float dt)
        {
            var from = Held.GetComponent<LabContainer>();
            var to = TargetContainer;
            var buret = to.GetComponent<BuretStopcock>();
            if (buret != null && !buret.mounted)
            {
                procedure?.Say("Lắp buret lên giá rồi mới nạp dung dịch", LabProcedure.Tone.Warn);
                return;
            }
            if (tilt < 70f) return;  // đang nghiêng dần, chưa tới miệng
            float ml = Mathf.Min(pourRateMlPerS * dt, from.VolumeMl);
            if (ml <= 0) return;
            float spill = to.Add(from.Take(ml));
            if (spill > 0) procedure?.Say($"Tràn! {to.label} đã đầy", LabProcedure.Tone.Warn);
        }

        // ---------- Tư thế tay và vật đang cầm ----------

        void UpdatePose(float dt)
        {
            Transform cam = viewCamera.transform;
            Quaternion facing = Quaternion.LookRotation(Flat(cam.forward), Vector3.up);
            Quaternion handRot = facing * Quaternion.Euler(20f, -12f, 0f);
            Vector3 rayPoint = hasHit ? hit.point - lastRay.direction * 0.07f : lastRay.origin + lastRay.direction * 0.6f;
            float dist = Vector3.Distance(cam.position, rayPoint);
            Vector3 free = cam.position + (rayPoint - cam.position).normalized * Mathf.Clamp(dist, minHandDistance, maxHandDistance);

            bool pouring = Current == Act.Pour || Current == Act.Spray || Current == Act.Scoop || Current == Act.Bubble;
            tilt = Mathf.MoveTowards(tilt, pouring && Acting ? 105f : pouring ? 35f : 0f, 160f * dt);
            Vector3 gripTarget = free;

            if (Held != null)
            {
                Transform t = Held.transform;
                Quaternion upright = Quaternion.Euler(0, facing.eulerAngles.y + (HeldPart != null ? heldYaw : 0f), 0);
                Vector3 objPos;
                Quaternion objRot = upright;
                if ((Current == Act.Suck || Current == Act.PipetteDrain || Current == Act.Measure || Current == Act.PaperTest) && TargetContainer != null)
                {
                    Transform tipT = null;
                    var pp = Held.GetComponent<Pipette>();
                    var mp = Held.GetComponent<MeterProbe>();
                    var tp = Held.GetComponent<TestPaper>();
                    if (pp != null) tipT = pp.tip;
                    else if (mp != null) tipT = mp.tip;
                    else if (tp != null && tp.tipRenderer != null) tipT = tp.tipRenderer.transform;
                    Vector3 mouth = Mouth(TargetContainer);
                    float tipY = Current == Act.PipetteDrain ? mouth.y - 0.015f : TargetContainer.transform.position.y + 0.015f;
                    if (Current == Act.PaperTest) objRot = upright * Quaternion.Euler(180f, 0, 0);
                    Vector3 tipLocal = tipT != null ? t.InverseTransformPoint(tipT.position) : Vector3.zero;
                    objPos = new Vector3(mouth.x, tipY, mouth.z) - objRot * tipLocal;
                }
                else if (pouring && TargetContainer != null)
                {
                    Vector3 mouth = Mouth(TargetContainer);
                    Vector3 side = -Flat(cam.right);
                    Vector3 axis = Vector3.Cross(Vector3.up, side).normalized;
                    objRot = Quaternion.AngleAxis(tilt, axis) * upright;
                    Vector3 heldMouthLocal = HeldMouthLocal();
                    Vector3 wantMouth = mouth + Vector3.up * (0.035f + 0.02f * (1 - tilt / 105f)) + side * 0.01f;
                    objPos = wantMouth - objRot * heldMouthLocal;
                    UpdateStream(objPos + objRot * heldMouthLocal, mouth, Acting && tilt >= 70f);
                }
                else if (Current == Act.Drop && TargetContainer != null)
                {
                    var d = Held.GetComponent<Dropper>();
                    Vector3 tipLocal = d.tip != null ? t.InverseTransformPoint(d.tip.position) : Vector3.up * 0.09f;
                    objRot = upright * Quaternion.Euler(180f, 0, 0);  // úp ngược lọ nhỏ giọt
                    Vector3 mouth = Mouth(TargetContainer);
                    objPos = mouth + Vector3.up * 0.035f - objRot * tipLocal;
                }
                else
                {
                    objPos = Vector3.Lerp(t.position, free - Vector3.up * Held.gripHeightM, 1f - Mathf.Exp(-20f * dt));
                }
                if (!(pouring && TargetContainer != null)) UpdateStream(Vector3.zero, Vector3.zero, false);
                t.SetPositionAndRotation(objPos, objRot);
                gripTarget = objPos + objRot * (Vector3.up * Held.gripHeightM);
                handPos = gripTarget;
            }
            else
            {
                UpdateStream(Vector3.zero, Vector3.zero, false);
                handPos = Vector3.Lerp(handPos, gripTarget, 1f - Mathf.Exp(-18f * dt));
            }

            if (hand != null)
            {
                hand.curl = Held != null ? 1f : Current == Act.Pick || Current == Act.Swirl ? 0.3f : 0f;
                hand.transform.rotation = handRot;
                Vector3 gripLocal = hand.Grip != null ? hand.Grip.localPosition : Vector3.zero;
                hand.transform.position = handPos - handRot * gripLocal;
            }
        }

        /// <summary>Tay cầm Wi-Fi: rung ngắn khi cầm / đặt, rung nhẹ khi đang rót / hút / lắc; đèn xanh = đang cầm, đèn đỏ = đang rót.</summary>
        void UpdatePadFeedback()
        {
            if (!PadInput.ControllerConnected)
            {
                lastPadHeld = Held;
                return;
            }
            if (Held != lastPadHeld) PadInput.Pulse(140, 45);
            lastPadHeld = Held;
            bool swirling = procedure != null && procedure.swirl != null && procedure.swirl.ManualIntensity > 0.5f;
            PadInput.Rumble(swirling ? 90 : Acting ? 60 : 0);
            PadInput.Leds(Held != null, Acting);
        }

        Grabbable lastPadHeld;

        Vector3 HeldMouthLocal()
        {
            var c = Held.GetComponent<LabContainer>();
            if (c != null && c.mouth != null) return Held.transform.InverseTransformPoint(c.mouth.position);
            var w = Held.GetComponent<WashBottle>();
            if (w != null && w.nozzle != null) return Held.transform.InverseTransformPoint(w.nozzle.position);
            return Vector3.up * 0.15f;
        }

        static Vector3 Mouth(LabContainer c) =>
            c.mouth != null ? c.mouth.position : c.transform.position + Vector3.up * 0.1f;

        void UpdateStream(Vector3 from, Vector3 to, bool on)
        {
            if (pourStream.gameObject.activeSelf != on) pourStream.gameObject.SetActive(on);
            if (!on) return;
            Vector3 d = to - from;
            pourStream.position = (from + to) / 2;
            pourStream.up = d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.up;
            pourStream.localScale = new Vector3(0.004f, d.magnitude / 2, 0.004f);
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
        }

        // ---------- Gợi ý thao tác ----------

        void BuildHints()
        {
            Hints.Clear();
            string Name(Transform t)
            {
                if (t == null) return "";
                var cb = t.GetComponentInParent<ChemicalBottle>();
                if (cb != null && cb.Entry != null) return cb.Entry.ten;
                var info = t.GetComponentInParent<EquipmentInfo>();
                return info != null ? info.title : t.name;
            }
            switch (Current)
            {
                case Act.Pick: Hints.Add(("Nhấn", "Cầm " + Lower(Name(hoverGrab.transform)))); break;
                case Act.Swirl: Hints.Add(("Giữ", "Lắc bình")); Hints.Add(("Nhấn", "Cầm lên")); break;
                case Act.Use: Hints.Add(("Nhấn", Clean(usable.UseHint))); break;
                case Act.Stopcock: Hints.Add(("Lăn chuột", "Mở / đóng khóa")); Hints.Add(("↑ ↓", "Tinh chỉnh")); break;
                case Act.Place: Hints.Add(("Nhấn", "Đặt vào " + Lower(TargetSocket.label))); break;
                case Act.PlaceSurface: Hints.Add(("Nhấn", "Đặt xuống")); break;
                case Act.Pour: Hints.Add(("Giữ", "Rót vào " + Lower(TargetContainer.label))); break;
                case Act.Suck: Hints.Add(("Giữ", "Hút")); Hints.Add(("Shift + Giữ", "Xả bớt về vạch")); break;
                case Act.PipetteDrain: Hints.Add(("Giữ", "Xả vào " + Lower(TargetContainer.label))); Hints.Add(("Shift + Giữ", "Xả chậm")); break;
                case Act.Drop: Hints.Add(("Nhấn", "Nhỏ 1 giọt")); break;
                case Act.Spray: Hints.Add(("Giữ", "Tia nước cất")); break;
                case Act.Scoop: Hints.Add(("Nhấn", "Xúc 1 thìa vào " + Lower(TargetContainer.label))); break;
                case Act.Bubble: Hints.Add(("Giữ", TargetContainer != null ? "Sục khí vào " + Lower(TargetContainer.label) : "Mở van xả khí")); break;
                case Act.Measure: Hints.Add(("Đo", Held.GetComponent<MeterProbe>().LastReading)); break;
                case Act.PaperTest: Hints.Add(("Nhấn", "Nhúng giấy thử")); break;
                case Act.Attach: Hints.Add(("Nhấn", $"Lắp vào {Lower(AttachTarget.label)} ({AttachTarget.Describe()})")); break;
            }
            if (HeldPart != null) Hints.Add(("Lăn chuột / R E", "Xoay bộ phận"));
            if (Held != null && Current != Act.Place) Hints.Add(("Q", "Trả về chỗ cũ"));
            if (PadInput.SingleStick && PadInput.AimFromCenter)
            {
                // Tay cầm 1 cần: chế độ ĐI chỉ đi lại, chế độ TAY mới thao tác (PadInput)
                if (PadInput.Mode == PadInput.StickMode.Walk)
                {
                    Hints.Clear();
                    Hints.Add(("ĐI", "↑↓ tiến lùi · ←→ quay"));
                    Hints.Add(("Giữ nút + cần", "Bước ngang · ngẩng cúi"));
                    Hints.Add(("Nhấn", "Chế độ TAY"));
                    Hints.Add(("Nhấn 2 lần", "Ngồi / đứng"));
                    Hints.Add(("Giữ 1 s", "Khu kế tiếp"));
                    return;
                }
                for (int i = 0; i < Hints.Count; i++) Hints[i] = (OneStickKey(Hints[i].key), Hints[i].text);
                Hints.Insert(0, ("TAY", "Cần: ngắm"));
                if (Current == Act.None) Hints.Add(("Nhấn cần", "Về chế độ ĐI"));
                return;
            }
            if (PadInput.AimFromCenter)
                for (int i = 0; i < Hints.Count; i++) Hints[i] = (PadKey(Hints[i].key, PadInput.UsingGamepad), Hints[i].text);
        }

        static string OneStickKey(string key)
        {
            switch (key)
            {
                case "Nhấn": return "Nhấn cần";
                case "Giữ": return "Giữ cần";
                case "Shift + Giữ": return "Shift + giữ cần";
                case "Lăn chuột": return "Cần ↑ ↓";
                default: return key;
            }
        }

        /// <summary>Tên nút tay cầm thay cho phím / chuột trong gợi ý khi người chơi đang dùng tay cầm.</summary>
        static string PadKey(string key, bool gamepad)
        {
            switch (key)
            {
                case "Nhấn": return gamepad ? "A / RT" : "Bóp cò";
                case "Giữ": return gamepad ? "Giữ A / RT" : "Giữ cò";
                case "Shift + Giữ": return gamepad ? "LT + giữ RT" : "Nhấn cần đi + giữ cò";
                case "Lăn chuột": return gamepad ? "Cần phải ↑ ↓" : "Cần nhìn ↑ ↓";
                case "↑ ↓": return gamepad ? "D-pad ↑ ↓" : "Nhấn cần đi + cần nhìn";
                case "Q": return gamepad ? "B" : "Căn lại";
                default: return key;
            }
        }

        static void Say(string text, LabProcedure.Tone tone = LabProcedure.Tone.Info) => LabMessages.Say(text, (LabMessages.Tone)(int)tone);

        static string Lower(string s) => string.IsNullOrEmpty(s) ? s : char.ToLower(s[0]) + s.Substring(1);

        static string Clean(string hint)
        {
            if (string.IsNullOrEmpty(hint)) return "";
            int i = hint.IndexOf(':');
            string s = i >= 0 ? hint.Substring(i + 1).Trim() : hint;
            return s.Length > 0 ? char.ToUpper(s[0]) + s.Substring(1) : s;
        }
    }
}
