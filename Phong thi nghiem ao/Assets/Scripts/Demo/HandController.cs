using System.Collections.Generic;
using Khkt.Input;
using Khkt.Interaction;
using Khkt.Interaction.Bio;
using Khkt.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;
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
        public enum Act { None, Pick, Swirl, Use, Stopcock, Place, PlaceSurface, Pour, Suck, PipetteDrain, Drop, Spray, Scoop, Bubble, Measure, PaperTest, Attach, BioButton, BioHinge, BioKnob, BioTarget }

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

        /// <summary>Bàn tay của người chơi cục bộ (thiết bị sinh học gọi để nhận vật đang cầm, ví dụ máy PCR nhận ống).</summary>
        public static HandController Active { get; private set; }
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
        BioButton hoverBioButton;
        BioHinge hoverBioHinge;
        BioKnob hoverBioKnob;
        IBioTarget hoverBioTarget;
        string bioTargetPrompt = "";
        float bioHoldTimer;
        bool bioHoldCompleted;
        RaycastHit hit;
        bool hasHit;
        float pressTime = -1f, dropCooldown;
        bool wasDown;
        bool swirlStarted;
        GameObject inspectHoldTarget;
        float inspectHoldTimer;
        bool inspectHoldOpened;
        bool inspectProgressShown;
        Vector3 handPos;
        float tilt;
        float heldYaw;  // xoay bộ phận đang cầm (lăn chuột / R E) để chọn chiều khi lắp
        // Xoay vật đang cầm như tay người (độ, theo khung camera): x = gập/duỗi cổ tay, y = lệch trái/phải, z = lật (sấp/ngửa)
        Vector3 heldTurn, heldTurnShown;
        Quaternion bandRef = Quaternion.identity;
        bool bandRefSet;
        Quaternion handRotSmoothed = Quaternion.identity;

        // Lấy đồ như người thật (kiểu game góc nhìn thứ ba): bấm vào vật → tay vươn tới (vật đứng yên chờ),
        // ngón ôm lấy rồi mới mang vật về tư thế cầm. Logic cầm (Held) có ngay; chỉ phần hình là có quãng đường.
        bool fetching, fetchGrabbed;
        float fetchTime, fetchCarryT, tapUntil = -1f;
        Vector3 fetchPos;
        Quaternion fetchRot = Quaternion.identity;
        /// <summary>Tay đang vươn tới vật vừa bấm lấy (vật chưa rời chỗ) hoặc đang mang vật về.</summary>
        public bool Fetching => fetching;
        /// <summary>Tay đã chạm và nắm vật (giai đoạn mang về).</summary>
        public bool FetchGrabbed => fetchGrabbed;
        /// <summary>Tiến độ vươn tay 0..1 (ArmRig ghi mỗi khung).</summary>
        public float ReachProgress { get; set; } = 1f;
        /// <summary>Kiểm thử: luôn diễn cảnh vươn tay lấy (kể cả khi ngắm theo kịch bản).</summary>
        public bool ForceFetchAnimation { get; set; }
        /// <summary>Tay phải cần rời chỗ nghỉ: đang cầm, đang thao tác, vừa bấm vào vật, hoặc ngắm theo kịch bản.</summary>
        public bool HandEngaged => Held != null || Acting || Time.time < tapUntil || OverrideAimPoint.HasValue;
        bool handRotReady;
        public const float TurnPitchMax = 75f, TurnYawMax = 70f, TurnRollMin = -170f, TurnRollMax = 150f;
        /// <summary>Đang giữ V xoay vật bằng chuột (DesktopViewer tạm không xoay camera).</summary>
        public static bool RotatingHeld { get; private set; }
        /// <summary>Góc xoay vật đang cầm đang hiển thị (độ: gập cổ tay, lệch, lật).</summary>
        public Vector3 HeldTurnDeg => heldTurnShown;
        /// <summary>Kiểm thử tự động: ép góc xoay vật đang cầm.</summary>
        public Vector3? OverrideHeldTurn { get; set; }
        Transform pourStream;
        readonly HashSet<(GameObject heldObj, LabContainer target, string riskKey)> confirmedSafety = new HashSet<(GameObject, LabContainer, string)>();
        float safetyBlockedUntil;

        static readonly string[] WaterReactiveIds =
        {
            "K(s)", "Na(s)", "Li(s)", "Ba(s)", "Ca(s)",
            "CaC2(s)", "Al4C3(s)", "NaH(s)", "LiAlH4(s)", "NaBH4(s)"
        };

        void Awake() => Active = this;

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
            CancelInspectHold();
            PadInput.ControllerChanged -= OnControllerChanged;
            PadInput.Notice -= OnPadNotice;
        }

        void OnDisable() => CancelInspectHold();

        void OnPadNotice(string text) => Say(text);

        void OnControllerChanged(bool connected, string who)
        {
            if (connected) Say($"Đã nối tay cầm ({who})", LabProcedure.Tone.Good);
            else Say("Mất kết nối tay cầm", LabProcedure.Tone.Warn);
        }

        static bool IsOpenPanelOnClick => PlayerPrefs.GetInt(UiTheme.ClickModePrefKey, 0) == 1;

        GameObject GetInspectTarget()
        {
            if (Held != null || (!ToolPanel.Enabled && !ToolPanel.ForceEnableForTest)) return null;
            if ((Current == Act.Pick || Current == Act.Swirl) && hoverGrab != null)
            {
                if (hoverGrab.GetComponentInParent<IMachinePanel>() != null) return null;
                return hoverGrab.gameObject;
            }
            if (Current == Act.Stopcock && stopcock != null && stopcock.buret != null)
                return stopcock.buret.gameObject;
            return null;
        }

        void CancelInspectHold()
        {
            inspectHoldTimer = 0f;
            inspectHoldTarget = null;
            if (inspectProgressShown)
            {
                LabToasts.HideProgress();
                inspectProgressShown = false;
            }
        }

        void Update()
        {
            var mouse = Mouse.current;
            var kb = Keyboard.current;
            bool uiOpen = Khkt.Environment.DesktopViewer.UiCapture > 0 || ToolPanel.IsOpen || MachinePanel.IsOpen || ModernUI.IsSafetyModalOpen;
            bool looking = uiOpen || (mouse != null && mouse.rightButton.isPressed && !Khkt.Environment.DesktopViewer.MouseLook);
            bool pointerOnUi = !OverrideHold && !Khkt.Environment.DesktopViewer.MouseLook && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            float dt = Time.deltaTime;

            if (!looking) Raycast(mouse);
            else if (uiOpen) hasHit = false;
            Decide();
            PadInput.LookYUsedByTool = Current == Act.Stopcock;
            PadInput.AimHasTarget = Current != Act.None;

            bool shift = OverrideShift || (kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed)) || PadInput.Shift;
            bool down = OverrideHold || (!looking && !pointerOnUi && mouse != null && mouse.leftButton.isPressed) || (!uiOpen && PadInput.Use);
            bool pressed = down && !wasDown;
            bool released = !down && wasDown;
            wasDown = down;
            if (pressed) pressTime = Time.time;
            // bấm vào nút/khóa/vật (tay rảnh): tay đưa tới chạm rồi về
            if (pressed && Held == null && hasHit && Current != Act.None && Current != Act.Pick) tapUntil = Time.time + 0.75f;
            if (down && Held == null && Current == Act.Stopcock) tapUntil = Time.time + 0.4f;

            if (uiOpen)
            {
                CancelInspectHold();
                inspectHoldOpened = false;
            }

            bool openPanelOnClick = IsOpenPanelOnClick;
            var inspectTarget = !uiOpen ? GetInspectTarget() : null;

            Acting = false;
            dropCooldown -= dt;
            switch (Current)
            {
                case Act.Pick:
                    if (openPanelOnClick && inspectTarget != null)
                    {
                        if (pressed)
                        {
                            CancelInspectHold();
                            ToolPanel.OpenObject(inspectTarget);
                        }
                    }
                    else if (inspectTarget != null)
                    {
                        if (pressed)
                        {
                            inspectHoldTarget = inspectTarget;
                            inspectHoldTimer = 0f;
                            inspectHoldOpened = false;
                        }
                        if (down && !inspectHoldOpened && inspectHoldTarget == inspectTarget)
                        {
                            inspectHoldTimer += dt;
                            if (inspectHoldTimer >= 0.12f)
                            {
                                LabToasts.Progress("Mở chi tiết", Mathf.Clamp01(inspectHoldTimer / 0.4f));
                                inspectProgressShown = true;
                            }
                            if (inspectHoldTimer >= 0.4f)
                            {
                                inspectHoldOpened = true;
                                CancelInspectHold();
                                ToolPanel.OpenObject(inspectTarget);
                            }
                        }
                        else if (down && inspectHoldTarget != inspectTarget)
                        {
                            CancelInspectHold();
                        }
                        if (released)
                        {
                            bool shouldPick = !inspectHoldOpened && inspectHoldTarget == inspectTarget && pressTime >= 0;
                            CancelInspectHold();
                            inspectHoldOpened = false;
                            if (shouldPick) Pick(hoverGrab);
                        }
                    }
                    else
                    {
                        if (pressed) Pick(hoverGrab);
                    }
                    break;
                case Act.Swirl:
                    // Giữ lâu là lắc bình tại chỗ, bấm nhanh là cầm lên (hoặc mở bảng chi tiết nếu bật chế độ Mở bảng)
                    if (down && pressTime >= 0 && Time.time - pressTime > 0.2f) { swirlStarted = true; Acting = true; }
                    if (released && !swirlStarted && pressTime >= 0)
                    {
                        if (openPanelOnClick && inspectTarget != null) ToolPanel.OpenObject(inspectTarget);
                        else Pick(hoverGrab);
                    }
                    break;
                case Act.Use:
                    if (pressed)
                    {
                        // máy có bảng điều khiển: mở bảng chỉnh thông số rồi mới chạy
                        if (MachinePanel.Enabled && usable is IMachinePanel machine) MachinePanel.Open(machine);
                        else usable.Use();
                    }
                    break;
                case Act.Stopcock:
                    if (mouse != null)
                    {
                        float scroll = mouse.scroll.ReadValue().y;
                        if (Mathf.Abs(scroll) > 0.01f) stopcock.buret.AdjustOpening(Mathf.Sign(scroll) * scrollStep * (shift ? 0.25f : 1f));
                    }
                    float padY = PadInput.Look.y;  // đẩy cần nhìn lên: mở thêm, kéo xuống: đóng bớt
                    if (Mathf.Abs(padY) > 0.05f) stopcock.buret.AdjustOpening(padY * keyRate * (shift ? 0.25f : 1f) * dt);

                    if (openPanelOnClick && inspectTarget != null)
                    {
                        if (pressed)
                        {
                            CancelInspectHold();
                            ToolPanel.OpenObject(inspectTarget);
                        }
                    }
                    else if (inspectTarget != null)
                    {
                        if (pressed)
                        {
                            inspectHoldTarget = inspectTarget;
                            inspectHoldTimer = 0f;
                            inspectHoldOpened = false;
                        }
                        if (down && !inspectHoldOpened && inspectHoldTarget == inspectTarget)
                        {
                            inspectHoldTimer += dt;
                            if (inspectHoldTimer >= 0.12f)
                            {
                                LabToasts.Progress("Mở chi tiết", Mathf.Clamp01(inspectHoldTimer / 0.4f));
                                inspectProgressShown = true;
                            }
                            if (inspectHoldTimer >= 0.4f)
                            {
                                inspectHoldOpened = true;
                                CancelInspectHold();
                                ToolPanel.OpenObject(inspectTarget);
                            }
                        }
                        else if (down && inspectHoldTarget != inspectTarget)
                        {
                            CancelInspectHold();
                        }
                        if (released)
                        {
                            CancelInspectHold();
                            inspectHoldOpened = false;
                        }
                    }
                    break;
                case Act.Place:
                    if (pressed) PlaceInSocket(TargetSocket);
                    break;
                case Act.PlaceSurface:
                    if (pressed) PlaceOnSurface(hit.point);
                    break;
                case Act.Pour:
                    if (down && Time.unscaledTime >= safetyBlockedUntil)
                    {
                        if (TryGateUnsafeAction(Held.gameObject, TargetContainer, Act.Pour)) break;
                        Acting = true;
                        Pour(dt);
                    }
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
                    if (down && Time.unscaledTime >= safetyBlockedUntil)
                    {
                        if (TryGateUnsafeAction(Held.gameObject, TargetContainer, Act.PipetteDrain)) break;
                        Acting = true;
                        Held.GetComponent<Pipette>().Drain(TargetContainer, dt, trim: shift);
                    }
                    break;
                case Act.Drop:
                    if (pressed && dropCooldown <= 0 && Time.unscaledTime >= safetyBlockedUntil)
                    {
                        if (TryGateUnsafeAction(Held.gameObject, TargetContainer, Act.Drop)) break;
                        Held.GetComponent<Dropper>().DropInto(TargetContainer);
                        dropCooldown = 0.25f;
                    }
                    break;
                case Act.Spray:
                    if (down && Time.unscaledTime >= safetyBlockedUntil)
                    {
                        if (TryGateUnsafeAction(Held.gameObject, TargetContainer, Act.Spray)) break;
                        Acting = true;
                        Held.GetComponent<WashBottle>().SprayInto(TargetContainer, dt);
                    }
                    break;
                case Act.Scoop:
                    if (pressed && dropCooldown <= 0 && Time.unscaledTime >= safetyBlockedUntil)
                    {
                        if (TryGateUnsafeAction(Held.gameObject, TargetContainer, Act.Scoop)) break;
                        if (!Held.GetComponent<ChemicalBottle>().ScoopInto(TargetContainer)) Say("Lọ đã hết hóa chất");
                        dropCooldown = 0.3f;
                    }
                    break;
                case Act.Bubble:
                    if (down && Time.unscaledTime >= safetyBlockedUntil)
                    {
                        if (TryGateUnsafeAction(Held.gameObject, TargetContainer, Act.Bubble)) break;
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
                case Act.BioButton:
                    if (pressed && hoverBioButton != null) hoverBioButton.Press();
                    break;
                case Act.BioHinge:
                    if (pressed && hoverBioHinge != null) hoverBioHinge.Toggle();
                    break;
                case Act.BioKnob:
                    if (hoverBioKnob != null)
                    {
                        if (mouse != null)
                        {
                            float scroll = mouse.scroll.ReadValue().y;
                            if (Mathf.Abs(scroll) > 0.01f) hoverBioKnob.AdjustSteps(scroll > 0 ? 1 : -1);
                        }
                        if (pressed) hoverBioKnob.AdjustSteps(shift ? -1 : 1);
                    }
                    break;
                case Act.BioTarget:
                    if (hoverBioTarget != null)
                    {
                        var heldTool = Held != null ? Held.GetComponent<BioTool>() : null;
                        float reqSec = hoverBioTarget.GetRequiredHoldSeconds(heldTool);
                        if (reqSec <= 0.01f)
                        {
                            if (pressed) hoverBioTarget.OnToolApplyComplete(heldTool);
                        }
                        else if (down && !bioHoldCompleted)
                        {
                            Acting = true;
                            bioHoldTimer += dt;
                            float p01 = Mathf.Clamp01(bioHoldTimer / reqSec);
                            hoverBioTarget.OnToolApplyStep(heldTool, dt, p01);
                            ProgressRing.Show(bioTargetPrompt, p01);
                            if (p01 >= 1f)
                            {
                                bioHoldCompleted = true;
                                ProgressRing.Hide();
                                hoverBioTarget.OnToolApplyComplete(heldTool);
                            }
                        }
                    }
                    break;
            }
            if ((Current != Act.Pick && Current != Act.Stopcock) || !down)
            {
                CancelInspectHold();
                if (!down) inspectHoldOpened = false;
            }
            if (Current != Act.BioTarget || !down)
            {
                bioHoldTimer = 0f;
                if (!down) bioHoldCompleted = false;
                ProgressRing.Hide();
            }
            if (Held != null && Current != Act.BioKnob && HeldPart == null && mouse != null && !looking)
            {
                var heldKnob = Held.GetComponentInChildren<BioKnob>();
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    if (heldKnob != null) heldKnob.AdjustSteps(scroll > 0 ? 1 : -1);
                    else if (Held.GetComponent<IScrollAdjustable>() is IScrollAdjustable adj && (adj as Object) != null) adj.OnScroll(scroll);  // micropipet đang cầm: chỉnh µL
                }
            }
            else if (Held == null && hasHit && mouse != null && !looking && Current != Act.BioKnob && Current != Act.Stopcock)
            {
                // tay không, trỏ vào thiết bị chỉnh được bằng con lăn (bộ nguồn điện di: V, bể ổn nhiệt: °C...)
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f && hit.collider.GetComponentInParent<IScrollAdjustable>() is IScrollAdjustable adj && (adj as Object) != null) adj.OnScroll(scroll);
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
                if (((kb != null && kb.spaceKey.wasPressedThisFrame) || (!uiOpen && PadInput.CloseStopcockPressed)))
                {
                    procedure.buret.Close();
                    PadInput.CloseStopcockPressed = false;
                }
            }
            if (((kb != null && kb.qKey.wasPressedThisFrame) || (!uiOpen && PadInput.BackPressed)) && Held != null) ReturnHome();
            if (!uiOpen)
            {
                bool onFloor2 = viewCamera != null && viewCamera.transform.position.y > 3.0f;
                var bioProc = onFloor2 ? Khkt.Demo.Bio.BioLabProcedure.Instance : null;
                bool enter = kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame);
                if (enter || PadInput.RecordPressed)
                {
                    if (bioProc != null && bioProc.Active) bioProc.RecordReading();
                    else if (procedure != null) procedure.RecordReading();
                }
                if (HeldPart == null && ((kb != null && kb.rKey.wasPressedThisFrame) || PadInput.NewRunPressed))
                {
                    if (bioProc != null && bioProc.Active)
                    {
                        bioProc.NewRun();
                        PadInput.NewRunPressed = false;
                    }
                    else if (procedure != null && procedure.Active)
                    {
                        procedure.NewRun();
                        PadInput.NewRunPressed = false;
                    }
                }
                if (ui != null && ((kb != null && kb.hKey.wasPressedThisFrame) || PadInput.HelpPressed)) ui.ToggleHelp();
                if (ui != null && ((kb != null && kb.gKey.wasPressedThisFrame) || PadInput.ToggleGraphPressed))
                {
                    ui.ToggleGraph();
                    PadInput.ToggleGraphPressed = false;
                }
                if (PadInput.NextThemePressed)
                {
                    Khkt.Environment.Tang2.BioThemeSwitcher.Instance?.ToggleTheme();
                    PadInput.NextThemePressed = false;
                }
            }

            UpdateHeldTurn(kb, mouse, uiOpen, dt);

            // Lắp ráp: xoay bộ phận đang cầm, bóng mờ xem trước chỗ lắp
            if (HeldPart != null && !uiOpen)
            {
                float turn = 0;
                if (mouse != null && !looking) turn += Mathf.Sign(mouse.scroll.ReadValue().y) * (Mathf.Abs(mouse.scroll.ReadValue().y) > 0.01f ? 1 : 0);
                if (kb != null) turn += (kb.rKey.wasPressedThisFrame ? 1 : 0) - (kb.eKey.wasPressedThisFrame ? 1 : 0);
                turn += PadInput.HeldYawStep;
                PadInput.HeldYawStep = 0;
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
            // Gợi ý thao tác ghép chuỗi mới mỗi lần dựng: dựng lại ngay khi đổi vật / đổi thao tác, còn lại 10 lần/giây
            // (đủ cho số đo, "Đang quay..." chạy theo) thay vì mỗi khung hình.
            int sig = HintSignature();
            if (sig != hintSig || Time.unscaledTime >= hintNextAt)
            {
                hintSig = sig;
                hintNextAt = Time.unscaledTime + 0.1f;
                BuildHints();
            }
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
            // tia không được dừng ở thân người chơi: cúi nhìn vật gần trên bàn thì tia đi qua khối thân ngay dưới mắt
            int n = Physics.RaycastNonAlloc(ray, rayHits, reach, ~0, QueryTriggerInteraction.Collide);
            hasHit = false;
            float nearest = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                if (rayHits[i].collider is CharacterController || rayHits[i].distance >= nearest) continue;
                nearest = rayHits[i].distance;
                hit = rayHits[i];
                hasHit = true;
            }
            if (!hasHit && PadInput.UsingTouch)
            {
                // Vì lam kính, kim rất nhỏ, thêm hút ngắm nhẹ chỉ ở chế độ cảm ứng:
                // tia thẳng không trúng vật cầm được thì SphereCast bán kính khoảng 1,5 cm
                RaycastHit sphereHit;
                if (Physics.SphereCast(ray, 0.015f, out sphereHit, reach, ~0, QueryTriggerInteraction.Collide))
                {
                    if (sphereHit.collider.GetComponentInParent<Grabbable>() != null ||
                        sphereHit.collider.GetComponentInParent<PlaceSocket>() != null ||
                        sphereHit.collider.GetComponentInParent<IDemoUsable>() != null ||
                        sphereHit.collider.GetComponent<StopcockHandle>() != null)
                    {
                        hit = sphereHit;
                        hasHit = true;
                    }
                }
            }
            lastRay = ray;
        }

        Ray lastRay;
        readonly RaycastHit[] rayHits = new RaycastHit[16];

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
            hoverBioButton = null;
            hoverBioHinge = null;
            hoverBioKnob = null;
            hoverBioTarget = null;
            bioTargetPrompt = "";
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

            // Tương tác dụng cụ Sinh học Tầng 2 (ưu tiên nút bấm, nắp bản lề, núm xoay, mục tiêu nhận tác động)
            var bBtn = col.GetComponentInParent<BioButton>();
            if (bBtn != null) { hoverBioButton = bBtn; Current = Act.BioButton; HoverRoot = bBtn.transform; return; }
            var bHinge = col.GetComponentInParent<BioHinge>();
            if (bHinge != null && (Held == null || grab == null)) { hoverBioHinge = bHinge; Current = Act.BioHinge; HoverRoot = bHinge.transform; return; }
            var bKnob = col.GetComponentInParent<BioKnob>();
            if (bKnob != null && Held == null) { hoverBioKnob = bKnob; Current = Act.BioKnob; HoverRoot = bKnob.transform; return; }
            var bTarget = col.GetComponentInParent<IBioTarget>();
            var heldBioTool = Held != null ? Held.GetComponent<BioTool>() : null;
            if (bTarget != null && (heldBioTool != null || grab == null) && bTarget.CanApplyTool(heldBioTool, out string bPrompt))
            {
                hoverBioTarget = bTarget;
                bioTargetPrompt = bPrompt;
                Current = Act.BioTarget;
                HoverRoot = (bTarget as Component)?.transform;
                return;
            }

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
            if (surface != null)
            {
                if (hit.normal.y > 0.7f)
                {
                    Current = Act.PlaceSurface;
                    HoverRoot = null;
                }
            }
        }

        // ---------- Cầm, đặt ----------

        public void PickFromPanel(GameObject go)
        {
            if (go == null) return;
            var raw = go.GetComponentInParent<Grabbable>();
            var g = Khkt.Assembly.ApparatusPart.WorldGrab(raw);
            if (g == null) g = raw;
            if (g == null || Held == g) return;
            if (Held != null) ReturnHome();
            if (Held != null) return;
            Pick(g);
        }

        public bool ReturnToHomeFromPanel(GameObject go)
        {
            if (go == null) return false;
            var raw = go.GetComponentInParent<Grabbable>();
            var g = Khkt.Assembly.ApparatusPart.WorldGrab(raw);
            if (g == null) g = raw;
            if (g == null) return false;
            if (Held == g)
            {
                ReturnHome();
                return Held == null;
            }
            if (!CanPutDown(g)) return false;
            var buret = g.GetComponent<BuretStopcock>();
            if (buret != null && buret.HasLiquid && g.Socket != null && g.Socket.mountsBuret)
            {
                procedure?.Say("Buret đang chứa dung dịch: xả hết rồi mới tháo khỏi giá", LabProcedure.Tone.Warn);
                return false;
            }
            if (g.Socket != null) g.SetSocket(null);
            if (g.HomeSocket != null && g.HomeSocket.Occupant == null)
            {
                g.HomeSocket.Put(g);
            }
            else
            {
                g.transform.SetPositionAndRotation(g.HomePosition, g.HomeRotation);
                int homeZone = Khkt.Rendering.ZoneMap.At(g.HomePosition);
                var homeRoot = Khkt.Rendering.ZoneRoot.Of(homeZone);
                if (homeRoot != null)
                {
                    g.transform.SetParent(homeRoot.transform, true);
                    if (homeRoot.Parked) g.gameObject.SetActive(false);
                }
            }
            return true;
        }

        public void Pick(Grabbable g)
        {
            if (g == null || Held != null) return;
            var bottle = g.GetComponent<ChemicalBottle>();
            if (bottle != null && ClassRules.IsLocked(bottle.stt))
            {
                LabMessages.Say("Giáo viên đã khóa hóa chất này", LabMessages.Tone.Warn);
                return;
            }
            var buret = g.GetComponent<BuretStopcock>();
            if (buret != null && buret.HasLiquid && g.Socket != null && g.Socket.mountsBuret)
            {
                procedure?.Say("Buret đang chứa dung dịch: xả hết rồi mới tháo khỏi giá", LabProcedure.Tone.Warn);
                return;
            }
            Held = g;
            var go = g.gameObject;
            confirmedSafety.RemoveWhere(k => k.heldObj == go);
            g.SetHeld(true);
            tilt = 0;
            heldYaw = 0;
            heldTurn = heldTurnShown = Vector3.zero;
            bandRefSet = false;
            // diễn cảnh vươn tay lấy (bỏ qua khi ngắm theo kịch bản – kiểm thử cần tay ở đúng chỗ ngay)
            fetching = (ForceFetchAnimation || !OverrideAimPoint.HasValue) && hand != null && hand.isActiveAndEnabled;
            fetchGrabbed = false;
            fetchTime = fetchCarryT = 0f;
            fetchPos = g.transform.position;
            fetchRot = g.transform.rotation;
            ReachProgress = 0f;
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

        /// <summary>
        /// Thiết bị nhận vật đang cầm (máy PCR nhận ống, bể điện di nhận khay gel, trạm ghép gen nhận ống mẫu):
        /// thả khỏi tay, gắn vào parent ở đúng chỗ. Trả về vật vừa thả (null nếu tay không).
        /// </summary>
        public Grabbable ReleaseHeldInto(Transform parent, Vector3 worldPos, Quaternion worldRot)
        {
            if (Held == null) return null;
            var g = Held;
            Held = null;
            g.SetHeld(false);
            g.transform.SetParent(parent, true);
            g.transform.SetPositionAndRotation(worldPos, worldRot);
            return g;
        }

        public void PlaceOnSurface(Vector3 point)
        {
            if (Held == null)
            {
                Debug.Log("[PlaceOnSurface-REJECT] Held is null");
                return;
            }
            if (!CanPutDown(Held))
            {
                Debug.Log($"[PlaceOnSurface-REJECT] CanPutDown({Held.name}) is false");
                return;
            }

            RaycastHit surfaceHit = hit;
            bool validSurface = hasHit;
            // Luôn kiểm tra raycast xuống ngay tại điểm point để xác nhận có mặt bàn đỡ bên dưới
            if (Physics.Raycast(point + Vector3.up * 0.05f, Vector3.down, out var downHit, 0.2f, ~0, QueryTriggerInteraction.Ignore))
            {
                surfaceHit = downHit;
                validSurface = true;
            }
            else
            {
                validSurface = false;
            }

            if (!validSurface || surfaceHit.collider == null || surfaceHit.collider.GetComponentInParent<PlaceSurface>() == null)
            {
                Debug.Log($"[PlaceOnSurface-REJECT] Không có PlaceSurface tại {point}. validSurface={validSurface}, hitCol={surfaceHit.collider?.name}");
                procedure?.Say("Không tìm thấy mặt bàn ở vị trí này", LabProcedure.Tone.Warn);
                return;
            }

            if (surfaceHit.normal.y <= 0.7f)
            {
                Debug.Log($"[PlaceOnSurface-REJECT] normal.y = {surfaceHit.normal.y:0.00} <= 0.7f");
                procedure?.Say("Không thể đặt vật vào cạnh hoặc mặt nghiêng", LabProcedure.Tone.Warn);
                return;
            }

            var g = Held;
            Vector3 placePos = surfaceHit.point; // Dùng chính xác tọa độ tiếp xúc vật lý mặt bàn

            // 1. Kiểm tra ranh giới mặt bàn: điểm đặt phải nằm trọn trong mặt bàn
            float itemRadius = g.liesOnTable ? g.lengthM * 0.5f : Mathf.Max(0.02f, g.lyingRadiusM > 0 ? g.lyingRadiusM : 0.03f);
            if (surfaceHit.collider is BoxCollider boxCol)
            {
                Vector3 localPoint = boxCol.transform.InverseTransformPoint(placePos);
                Vector3 halfSize = boxCol.size * 0.5f;
                float marginX = itemRadius;
                float marginZ = itemRadius;

                if (halfSize.x > marginX * 1.05f && Mathf.Abs(localPoint.x - boxCol.center.x) > halfSize.x - marginX)
                {
                    Debug.Log($"[PlaceOnSurface-REJECT] Quá gần mép bàn X (localX={localPoint.x:0.000}, halfSize.x={halfSize.x:0.000}, marginX={marginX:0.000})");
                    procedure?.Say("Vị trí quá gần mép bàn", LabProcedure.Tone.Warn);
                    return;
                }
                if (halfSize.z > marginZ * 1.05f && Mathf.Abs(localPoint.z - boxCol.center.z) > halfSize.z - marginZ)
                {
                    Debug.Log($"[PlaceOnSurface-REJECT] Quá gần mép bàn Z (localZ={localPoint.z:0.000}, halfSize.z={halfSize.z:0.000}, marginZ={marginZ:0.000})");
                    procedure?.Say("Vị trí quá gần mép bàn", LabProcedure.Tone.Warn);
                    return;
                }
            }

            // 2. Physics.OverlapBox kiểm tra va chạm với vật đã có
            Vector3 testPos;
            Quaternion testRot;
            Vector3 halfBox;

            if (g.liesOnTable)
            {
                Vector3 d = Flat(viewCamera != null ? viewCamera.transform.right : Vector3.right);
                if (d.sqrMagnitude < 1e-4f) d = Vector3.forward;
                else d = d.normalized;
                testPos = placePos + Vector3.up * g.lyingRadiusM;
                testRot = Quaternion.FromToRotation(Vector3.up, d);
                halfBox = new Vector3(g.lyingRadiusM, g.lyingRadiusM, g.lengthM * 0.5f);
            }
            else
            {
                float h = g.gripHeightM > 0 ? g.gripHeightM : 0.08f;
                testPos = placePos + Vector3.up * (h * 0.5f);
                testRot = Quaternion.Euler(0, g.HomeRotation.eulerAngles.y + heldYaw, 0);
                halfBox = new Vector3(itemRadius, h * 0.5f, itemRadius);
            }

            var overlaps = Physics.OverlapBox(testPos, halfBox * 0.85f, testRot, ~0, QueryTriggerInteraction.Ignore);
            bool blocked = false;
            Collider blockedBy = null;
            foreach (var c in overlaps)
            {
                if (c == surfaceHit.collider) continue;
                if (c.transform.IsChildOf(g.transform)) continue;
                if (c.isTrigger) continue;
                if (c is CharacterController || c.GetComponentInParent<Khkt.Environment.DesktopViewer>() != null) continue; // Người chơi không tự cản mình
                if (c.GetComponentInParent<PlaceSurface>() != null) continue; // mặt bàn / mảnh ghép mặt bàn
                if (surfaceHit.collider.transform.parent != null && c.transform.IsChildOf(surfaceHit.collider.transform.parent)) continue; // cùng cụm bàn
                if (c.transform.IsChildOf(surfaceHit.collider.transform.root) && c.GetComponentInParent<Grabbable>() == null && c.GetComponentInParent<IDemoUsable>() == null) continue; // chi tiết kiến trúc bàn/phòng
                blocked = true;
                blockedBy = c;
                break;
            }

            if (blocked)
            {
                Debug.Log($"[PlaceOnSurface-REJECT] Bị vướng {blockedBy?.name} (parent: {blockedBy?.transform.parent?.name}) tại {testPos}");
                procedure?.Say("Vị trí bị vướng vật khác", LabProcedure.Tone.Warn);
                return;
            }

            // 3. Tiến hành đặt vật xuống
            Held = null;
            g.SetHeld(false);
            if (g.liesOnTable)
            {
                Vector3 d = Flat(viewCamera != null ? viewCamera.transform.right : Vector3.right);
                if (d.sqrMagnitude < 1e-4f) d = Vector3.forward;
                else d = d.normalized;
                g.transform.SetPositionAndRotation(placePos + Vector3.up * g.lyingRadiusM - d * g.lengthM / 2,
                                                   Quaternion.FromToRotation(Vector3.up, d));
            }
            else
            {
                // Bình thủy tinh đặt xuống bàn: xoay nhãn và vạch chia thể tích (+x) hướng về phía mắt người chơi
                Vector3 lookDir = Flat((viewCamera != null ? viewCamera.transform.position : Vector3.zero) - placePos);
                float faceYaw = lookDir.sqrMagnitude > 1e-4f ? (Quaternion.LookRotation(lookDir, Vector3.up).eulerAngles.y - 90f) : g.HomeRotation.eulerAngles.y;
                g.transform.SetPositionAndRotation(placePos, Quaternion.Euler(0, faceYaw + heldYaw, 0));
            }

            int currentZone = Khkt.Rendering.ZoneMap.At(g.transform.position);
            var zRoot = Khkt.Rendering.ZoneRoot.Of(currentZone);
            if (zRoot != null && !zRoot.Parked && g.transform.parent != zRoot.transform)
            {
                g.transform.SetParent(zRoot.transform, true);
            }

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
            if (g.HomeSocket != null && g.HomeSocket.Occupant == null)
            {
                g.HomeSocket.Put(g);
            }
            else
            {
                g.transform.SetPositionAndRotation(g.HomePosition, g.HomeRotation);
                int homeZone = Khkt.Rendering.ZoneMap.At(g.HomePosition);
                var homeRoot = Khkt.Rendering.ZoneRoot.Of(homeZone);
                if (homeRoot != null)
                {
                    g.transform.SetParent(homeRoot.transform, true);
                    if (homeRoot.Parked) g.gameObject.SetActive(false);
                }
            }
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

        bool TryGateUnsafeAction(GameObject heldObj, LabContainer target, Act act)
        {
            if (heldObj == null) return false;
            if (!EvaluateSafetyRisk(heldObj, target, act, out string riskKey, out string title, out string reason, out string safeHint))
                return false;

            var key = (heldObj, target, riskKey);
            if (confirmedSafety.Contains(key)) return false;

            Acting = false;
            safetyBlockedUntil = Time.unscaledTime + 0.35f;
            var capturedBottle = act == Act.Scoop ? heldObj.GetComponent<ChemicalBottle>() : null;
            var capturedDropper = act == Act.Drop ? heldObj.GetComponent<Dropper>() : null;
            var capturedTarget = target;

            ModernUI.ShowSafetyWarning(
                title,
                reason,
                onProceed: () =>
                {
                    confirmedSafety.Add(key);
                    safetyBlockedUntil = Time.unscaledTime + 0.05f;
                    if (act == Act.Scoop && capturedBottle != null && capturedTarget != null && Held != null && Held.gameObject == heldObj)
                    {
                        if (!capturedBottle.ScoopInto(capturedTarget)) Say("Lọ đã hết hóa chất");
                        dropCooldown = 0.3f;
                    }
                    else if (act == Act.Drop && capturedDropper != null && capturedTarget != null && Held != null && Held.gameObject == heldObj)
                    {
                        capturedDropper.DropInto(capturedTarget);
                        dropCooldown = 0.25f;
                    }
                },
                onChangeApproach: () =>
                {
                    safetyBlockedUntil = Time.unscaledTime + 0.5f;
                    Say(safeHint, LabProcedure.Tone.Warn);
                });
            return true;
        }

        static bool EvaluateSafetyRisk(GameObject heldObj, LabContainer target, Act act,
            out string riskKey, out string title, out string reason, out string safeHint)
        {
            riskKey = title = reason = safeHint = null;
            var bottle = heldObj.GetComponent<ChemicalBottle>();
            var fromContainer = heldObj.GetComponent<LabContainer>();
            bool isSpray = act == Act.Spray;

            Solution fromSol = default;
            if (fromContainer != null && fromContainer.VolumeMl > 0.01f)
            {
                fromSol = fromContainer.Total;
            }
            else if (act == Act.Drop)
            {
                var dropper = heldObj.GetComponent<Dropper>();
                if (dropper != null && dropper.source != null && dropper.source.VolumeMl > 0.01f)
                    fromSol = dropper.source.Total;
                else if (dropper != null && dropper.catalogStt > 0 && ChemicalCatalog.Instance != null)
                {
                    var ce = ChemicalCatalog.Instance.Get(dropper.catalogStt);
                    if (ce != null) fromSol = ChemicalCatalog.Instance.Make(ce, 1f);
                }
            }

            // 1. Sục bình khí độc ngoài tủ hút
            if (act == Act.Bubble && bottle != null && bottle.IsGas && bottle.Entry != null &&
                (bottle.Entry.HasHazard("doc") || bottle.Entry.HasHazard("khi_doc")))
            {
                bool inHood = target != null ? target.InFumeHood : FumeHoodZone.Covers(heldObj.transform.position);
                if (!inHood)
                {
                    string formula = !string.IsNullOrEmpty(bottle.Entry.Formula) ? bottle.Entry.Formula : bottle.DisplayName;
                    riskKey = "khi_doc_bubble";
                    title = $"Nguy cơ rò rỉ khí độc {formula} ngoài tủ hút!";
                    reason = $"Sục khí {bottle.DisplayName} bên ngoài tủ hút làm khí độc thoát ra không khí phòng thí nghiệm.\nCách an toàn: đặt bình nhận vào bên trong Tủ hút khí độc trước khi sục khí.";
                    safeHint = $"Đã hủy: hãy đưa bình vào trong Tủ hút khí độc trước khi sục {formula}.";
                    return true;
                }
            }

            if (target == null) return false;

            var toMix = target.Total.Mix;
            double toVolL = target.VolumeMl > 0.01f ? target.VolumeMl / 1000.0 : 0.0;
            bool targetInHood = target.InFumeHood;
            double fromSulfuricM = fromSol.volumeMl > 0.01f ? fromSol.Mix.Get("SO4") / (fromSol.volumeMl / 1000.0) : 0.0;
            double toSulfuricM = toVolL > 0 ? toMix.Get("SO4") / toVolL : 0.0;

            // 2. Đổ nước hoặc dung dịch loãng vào H2SO4 đặc (> 6 M): sôi bắn axit
            if ((act == Act.Pour || act == Act.PipetteDrain || isSpray) && target.VolumeMl > 0.5f)
            {
                bool incomingAqueous = isSpray || fromSol.volumeMl > 0.1f;
                if (incomingAqueous && toSulfuricM > 6.0 && fromSulfuricM < 3.0)
                {
                    riskKey = "ban_be_h2so4";
                    title = "Nguy cơ sôi bắn axit H₂SO₄ đặc!";
                    reason = "Đổ nước hoặc dung dịch loãng vào axit sunfuric đặc (> 6 M) tỏa nhiệt cực mạnh, dễ làm dung dịch sôi bắn gây bỏng.\nCách an toàn: lấy nước cất làm nền trước rồi rót từ từ axit đặc theo thành bình và khuấy đều.";
                    safeHint = "Đã hủy: không đổ nước vào H₂SO₄ đặc — hãy lấy nước trước rồi rót từ từ axit vào nước.";
                    return true;
                }
            }

            // 3. Trộn hóa chất sinh khí cực độc (Cl2, HCN) ngoài tủ hút
            if (!targetInHood)
            {
                bool toAcid = target.VolumeMl > 0.2f && target.BulkPh < 5.0;
                bool fromAcid = fromSol.volumeMl > 0.2f && fromSol.Ph < 5.0;
                if (toAcid || fromAcid)
                {
                    bool hasClO = toMix.Get("ClO") > 1e-4 || fromSol.Mix.Get("ClO") > 1e-4;
                    bool hasCl = toMix.Get("Cl-") > 1e-4 || fromSol.Mix.Get("Cl-") > 1e-4;
                    double toClM = toVolL > 0 ? toMix.Get("Cl-") / toVolL : 0.0;
                    double fromClM = fromSol.volumeMl > 0.01f ? fromSol.Mix.Get("Cl-") / (fromSol.volumeMl / 1000.0) : 0.0;
                    bool hasChlorineOx = toMix.Get("MnO2(s)") > 1e-4 || fromSol.Mix.Get("MnO2(s)") > 1e-4 ||
                                         toMix.Get("PbO2(s)") > 1e-4 || fromSol.Mix.Get("PbO2(s)") > 1e-4 ||
                                         toMix.Get("MnO4-") > 1e-4 || fromSol.Mix.Get("MnO4-") > 1e-4;
                    if ((hasClO && hasCl) || (hasChlorineOx && (toClM > 3.0 || fromClM > 3.0)))
                    {
                        riskKey = "khi_doc_cl2";
                        title = "Nguy cơ giải phóng khí độc Cl₂ ngoài tủ hút!";
                        reason = "Hỗn hợp phản ứng sinh khí Clo (Cl₂) vàng lục rất độc gây tổn thương đường hô hấp.\nCách an toàn: di chuyển bình phản ứng vào trong Tủ hút khí độc trước khi trộn.";
                        safeHint = "Đã hủy: hãy đưa bình vào Tủ hút khí độc trước khi trộn phản ứng sinh Cl₂.";
                        return true;
                    }

                    bool hasCN = toMix.Get("CN-") > 1e-5 || fromSol.Mix.Get("CN-") > 1e-5;
                    if (hasCN)
                    {
                        riskKey = "khi_doc_hcn";
                        title = "Nguy cơ sinh khí cực độc HCN ngoài tủ hút!";
                        reason = "Muối xyanua gặp axit giải phóng khí hydro xyanua (HCN) cực độc.\nCách an toàn: chỉ thực hiện thí nghiệm trong Tủ hút khí độc đang bật quạt hút.";
                        safeHint = "Đã hủy: phản ứng có xyanua và axit bắt buộc thực hiện trong Tủ hút khí độc.";
                        return true;
                    }
                }
            }

            // 4. Kim loại kiềm / chất rắn phản ứng mãnh liệt với nước (cháy nổ)
            bool targetHasWater = target.VolumeMl > 0.5f;
            if (act == Act.Scoop && targetHasWater)
            {
                bool solidReactive = HasAnySpecies(fromSol, WaterReactiveIds) ||
                    (bottle != null && bottle.Entry != null && (bottle.Entry.HasHazard("chay_no") || bottle.Entry.HasHazard("phan_ung_nuoc")));
                if (solidReactive)
                {
                    string solidName = bottle != null ? bottle.DisplayName : "chất rắn hoạt động mạnh";
                    riskKey = "chay_no_nuoc";
                    title = "Nguy cơ phản ứng mãnh liệt / cháy nổ với nước!";
                    reason = $"Cho {solidName} tiếp xúc trực tiếp với nước sẽ tỏa nhiệt mạnh và giải phóng khí dễ cháy (H₂ hoặc C₂H₂).\nCách an toàn: chỉ lấy lượng rất nhỏ, làm trong tủ hút và tránh xa nguồn lửa.";
                    safeHint = $"Đã hủy: {solidName} phản ứng mạnh với nước — thao tác lượng nhỏ trong tủ hút, tránh xa ngọn lửa.";
                    return true;
                }
            }
            else if ((isSpray || fromSol.volumeMl > 0.2f) && HasAnySpecies(target.Total, WaterReactiveIds))
            {
                riskKey = "chay_no_nuoc";
                title = "Nguy cơ phản ứng mãnh liệt / cháy nổ với nước!";
                reason = "Bình nhận đang chứa kim loại kiềm hoặc chất phản ứng mạnh với nước, sinh khí dễ cháy (H₂ / C₂H₂) và tỏa nhiệt lớn.\nCách an toàn: kiểm soát lượng nước nhỏ trong tủ hút và đảm bảo không có ngọn lửa gần đó.";
                safeHint = "Đã hủy: trong bình có chất phản ứng mạnh với nước — hãy đặt vào tủ hút và giữ xa nguồn lửa.";
                return true;
            }

            return false;
        }

        static bool HasAnySpecies(Solution sol, string[] ids)
        {
            if (ids == null) return false;
            var mix = sol.Mix;
            for (int i = 0; i < ids.Length; i++)
                if (mix.Get(ids[i]) > 1e-4) return true;
            return false;
        }

        // ---------- Xoay vật trong tay (cổ tay + sấp/ngửa) ----------

        /// <summary>
        /// V + rê chuột: xoay tự do; J/L lật trái/phải (sấp/ngửa cẳng tay), I/K gập/duỗi cổ tay, U/O lệch trái/phải, N về thẳng.
        /// Tay cầm VERA Band có IMU: vặn/gập cổ tay thật → vật xoay theo (so với hướng lúc cầm lên). Giới hạn theo tầm vận động người.
        /// </summary>
        void UpdateHeldTurn(Keyboard kb, Mouse mouse, bool uiOpen, float dt)
        {
            RotatingHeld = false;
            bool canTurn = Held != null && HeldPart == null && !uiOpen;
            if (canTurn)
            {
                if (kb != null)
                {
                    if (kb.vKey.isPressed && mouse != null)
                    {
                        RotatingHeld = true;
                        Vector2 md = mouse.delta.ReadValue() * 0.35f;
                        heldTurn.x -= md.y;
                        heldTurn.z += md.x;
                    }
                    float rate = 120f * dt;
                    heldTurn.z += ((kb.lKey.isPressed ? 1 : 0) - (kb.jKey.isPressed ? 1 : 0)) * rate;
                    heldTurn.x += ((kb.kKey.isPressed ? 1 : 0) - (kb.iKey.isPressed ? 1 : 0)) * rate;
                    heldTurn.y += ((kb.oKey.isPressed ? 1 : 0) - (kb.uKey.isPressed ? 1 : 0)) * rate;
                    if (kb.nKey.wasPressedThisFrame) heldTurn = Vector3.zero;
                }
                // VERA Band: hướng IMU so với lúc cầm lên → lật (quanh trục tới) và gập (quanh trục ngang)
                var src = PadInput.ControllerConnected ? PadInput.Controller : null;
                if (src != null && !src.Latest.Pressed(ControllerButtons.ImuError))
                {
                    Quaternion q = src.Latest.UnityRotation;
                    if (!bandRefSet) { bandRef = q; bandRefSet = true; }
                    Quaternion rel = Quaternion.Inverse(bandRef) * q;
                    float roll = TwistDeg(rel, Vector3.forward), pitch = TwistDeg(rel, Vector3.right);
                    if (Mathf.Abs(roll) > 4f || Mathf.Abs(pitch) > 4f) heldTurn = new Vector3(pitch, heldTurn.y, -roll);
                }
                heldTurn.x = Mathf.Clamp(heldTurn.x, -TurnPitchMax, TurnPitchMax);
                heldTurn.y = Mathf.Clamp(heldTurn.y, -TurnYawMax, TurnYawMax);
                heldTurn.z = Mathf.Clamp(heldTurn.z, TurnRollMin, TurnRollMax);
            }
            if (OverrideHeldTurn.HasValue) heldTurn = OverrideHeldTurn.Value;
            // làm mượt theo thời gian (không phụ thuộc FPS)
            heldTurnShown = Vector3.Lerp(heldTurnShown, heldTurn, 1f - Mathf.Exp(-14f * dt));
        }

        static float TwistDeg(Quaternion q, Vector3 axis)
        {
            float p = q.x * axis.x + q.y * axis.y + q.z * axis.z;
            float a = 2f * Mathf.Atan2(p, q.w) * Mathf.Rad2Deg;
            return Mathf.DeltaAngle(0f, a);
        }

        /// <summary>Phép xoay người dùng áp cho vật (khung camera): lật quanh hướng nhìn, gập quanh trục ngang camera, lệch quanh trục đứng.</summary>
        Quaternion HeldTurnRotation(Transform cam)
        {
            Vector3 t = heldTurnShown;
            if (t.sqrMagnitude < 1e-6f) return Quaternion.identity;
            Vector3 fwd = Flat(cam.forward);
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
            return Quaternion.AngleAxis(t.y, Vector3.up) * Quaternion.AngleAxis(t.x, right) * Quaternion.AngleAxis(-t.z, fwd);
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
            Quaternion handFollow = Quaternion.identity;   // bàn tay xoay cùng vật (rót, lật, gập cổ tay)

            if (Held == null) fetching = fetchGrabbed = false;
            float fetchS = 1f;
            if (Held != null && fetching)
            {
                fetchTime += dt;
                if (!fetchGrabbed && (ReachProgress > 0.97f || fetchTime > 1.1f)) { fetchGrabbed = true; fetchCarryT = 0f; }
                if (fetchGrabbed)
                {
                    fetchCarryT += dt;
                    float u = Mathf.Clamp01(fetchCarryT / 0.42f);
                    fetchS = u * u * u * (u * (u * 6f - 15f) + 10f);
                    if (u >= 1f) fetching = false;
                }
                else fetchS = 0f;
            }

            if (Held != null && fetching && !fetchGrabbed)
            {
                // Vật nằm yên chỗ cũ, tay đang vươn tới điểm cầm trên vật
                Held.transform.SetPositionAndRotation(fetchPos, fetchRot);
                UpdateStream(Vector3.zero, Vector3.zero, false);
                gripTarget = fetchPos + Vector3.up * Held.gripHeightM;
                handPos = gripTarget;
            }
            else if (Held != null)
            {
                Transform t = Held.transform;
                // Cầm bình trên tay: nhãn hóa chất và vạch chia hướng trực diện về phía mắt người chơi, nghiêng nhẹ 7° để nhìn rõ dung dịch
                Quaternion facingCam = Quaternion.Euler(7f, facing.eulerAngles.y - 90f + (HeldPart != null ? heldYaw : 0f), 0);
                Quaternion upright = (HeldPart != null) ? Quaternion.Euler(0, facing.eulerAngles.y + heldYaw, 0) : facingCam;
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
                    handFollow = Quaternion.AngleAxis(tilt, axis);
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
                    Quaternion turn = HeldTurnRotation(cam);
                    objRot = turn * upright;
                    handFollow = turn;
                    Vector3 carry = free - (turn * Vector3.up) * Held.gripHeightM;
                    objPos = fetching ? carry : Vector3.Lerp(t.position, carry, 1f - Mathf.Exp(-20f * dt));
                }
                if (fetching)
                {
                    // mang vật từ chỗ cũ về tư thế cầm: nhấc lên trước rồi đưa về (cung nhẹ)
                    objPos = Vector3.Lerp(fetchPos, objPos, fetchS) + Vector3.up * (Mathf.Sin(fetchS * Mathf.PI) * 0.035f);
                    objRot = Quaternion.Slerp(fetchRot, objRot, fetchS);
                    handFollow = Quaternion.Slerp(Quaternion.identity, handFollow, fetchS);
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
                bool reachingOut = Held != null && fetching && !fetchGrabbed;
                hand.TargetContactObject = Held != null
                    ? (reachingOut && ReachProgress < 0.85f ? null : Held.transform)
                    : (Current == Act.Stopcock && stopcock != null ? stopcock.transform : null);
                // đang vươn tới: bàn tay mở hình chữ C đón vật, gần tới mới khép ngón ôm vật
                hand.SetTargetPose(reachingOut && ReachProgress < 0.78f
                    ? Khkt.Avatar.HandPoseId.MoCam
                    : Khkt.Avatar.HandPoseTable.ClassifyToolPose(Held, Current, Current == Act.Pick || Current == Act.Swirl));
                // Tay rảnh: bàn tay hướng theo tầm với (từ vai tới tay) như cổ tay người; tay cầm vật: xoay cùng vật
                Quaternion target;
                if (Held == null)
                {
                    Vector3 shoulder = cam.position + facing * new Vector3(0.185f, -0.245f, -0.06f);
                    Vector3 reachDir = handPos - shoulder;
                    if (reachDir.sqrMagnitude < 1e-6f) reachDir = facing * Vector3.forward;
                    Vector3 aim = Vector3.Slerp(facing * Vector3.forward, reachDir.normalized, 0.55f);
                    target = Quaternion.LookRotation(aim, Vector3.up) * Quaternion.Euler(10f, -12f, 0f);
                }
                else target = handFollow * handRot;
                if (!handRotReady) { handRotSmoothed = target; handRotReady = true; }
                handRotSmoothed = Quaternion.Slerp(handRotSmoothed, target, 1f - Mathf.Exp(-(Held != null ? 40f : 16f) * dt));
                hand.transform.rotation = handRotSmoothed;
                handRot = handRotSmoothed;
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
            if (c != null && c.mouth != null)
            {
                Vector3 mouthLocal = Held.transform.InverseTransformPoint(c.mouth.position);
                float r = c.bottomRadiusM > 0.01f ? c.bottomRadiusM : 0.035f;
                mouthLocal.x += r * 0.95f; // Tâm xoay tại đúng mỏ rót (spout) ở mép miệng bình
                return mouthLocal;
            }
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

        int hintSig;
        float hintNextAt;

        static int H(object o) => o != null ? o.GetHashCode() : 0;

        /// <summary>Dấu vết của mọi thứ quyết định nội dung gợi ý: đổi là phải dựng lại ngay.</summary>
        int HintSignature()
        {
            unchecked
            {
                int h = (int)Current;
                h = h * 31 + (HoverRoot != null ? HoverRoot.GetHashCode() : 0);
                h = h * 31 + (Held != null ? Held.GetHashCode() : 0);
                h = h * 31 + (TargetContainer != null ? TargetContainer.GetHashCode() : 0);
                h = h * 31 + (TargetSocket != null ? TargetSocket.GetHashCode() : 0);
                h = h * 31 + H(AttachTarget);
                h = h * 31 + (hasHit && hit.collider != null ? hit.collider.GetHashCode() : 0);
                int pad = (PadInput.UsingTouch ? 1 : 0) | (PadInput.SingleStick ? 2 : 0) | (PadInput.AimFromCenter ? 4 : 0)
                        | (PadInput.UsingGamepad ? 8 : 0) | ((int)PadInput.Mode << 4);
                return h * 31 + pad;
            }
        }

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
            var inspectNow = GetInspectTarget();
            bool openOnClickNow = IsOpenPanelOnClick;
            switch (Current)
            {
                case Act.Pick:
                    if (openOnClickNow && inspectNow != null)
                        Hints.Add(("Nhấn", "Mở chi tiết " + Lower(Name(hoverGrab.transform))));
                    else
                    {
                        Hints.Add(("Nhấn", "Cầm " + Lower(Name(hoverGrab.transform))));
                        if (inspectNow != null) Hints.Add(("Giữ", "Mở chi tiết"));
                    }
                    break;
                case Act.Swirl:
                    Hints.Add(("Giữ", "Lắc bình"));
                    Hints.Add(("Nhấn", openOnClickNow && inspectNow != null ? "Mở chi tiết" : "Cầm lên"));
                    break;
                case Act.Use: Hints.Add(("Nhấn", Clean(usable.UseHint))); break;
                case Act.Stopcock:
                    Hints.Add(("Lăn chuột", "Mở / đóng khóa"));
                    Hints.Add(("↑ ↓", "Tinh chỉnh"));
                    if (inspectNow != null) Hints.Add((openOnClickNow ? "Nhấn" : "Giữ", "Bảng buret"));
                    break;
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
                case Act.BioButton: if (hoverBioButton != null) Hints.Add(("Nhấn", hoverBioButton.GetPromptText())); break;
                case Act.BioHinge: if (hoverBioHinge != null) Hints.Add(("Nhấn", hoverBioHinge.GetPromptText())); break;
                case Act.BioKnob: if (hoverBioKnob != null) Hints.Add(("Lăn chuột", hoverBioKnob.GetPromptText())); break;
                case Act.BioTarget:
                    if (hoverBioTarget != null)
                    {
                        float req = hoverBioTarget.GetRequiredHoldSeconds(Held != null ? Held.GetComponent<BioTool>() : null);
                        Hints.Add((req > 0.01f ? "Giữ" : "Nhấn", bioTargetPrompt));
                    }
                    break;
            }
            if (HeldPart != null) Hints.Add(("Lăn chuột / R E", "Xoay bộ phận"));
            if (Held != null && Current != Act.Place) Hints.Add(("Q", "Trả về chỗ cũ"));
            if (Held != null && HeldPart == null) Hints.Add(("V + chuột / J L I K", "Xoay vật trong tay"));
            if (PadInput.UsingTouch)
            {
                for (int i = 0; i < Hints.Count; i++)
                    Hints[i] = (TouchKey(Hints[i].key), Hints[i].text);
                return;
            }
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

        /// <summary>Gợi ý nút cảm ứng trên iPad.</summary>
        static string TouchKey(string key)
        {
            switch (key)
            {
                case "Nhấn": return "Chạm";
                case "Giữ": return "Giữ nút tay";
                case "Shift + Giữ": return "Chậm + Giữ";
                case "Lăn chuột": return "Nút vặn";
                case "↑ ↓": return "Nút vặn";
                case "Lăn chuột / R E": return "Nút xoay";
                case "Q": return "Nút trả về";
                case "Space": return "Đóng khóa";
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
