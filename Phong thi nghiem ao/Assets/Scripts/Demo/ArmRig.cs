using System.Collections.Generic;
using Khkt.Avatar;
using Khkt.Input;
using Khkt.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Khkt.Demo
{
    /// <summary>
    /// Hai cánh tay người chơi góc nhìn thứ nhất (bản 20/11/2026): mỗi tay một ống tay áo blouse liền khối có
    /// skinning 5 xương (cánh tay trên, khuỷu, 2 xương xoắn cẳng tay, cổ tay áo) nối kín vào cổ tay HandRig.
    ///  • IK hai khớp vai – khuỷu – cổ tay: khuỷu gập 0–150° (không duỗi quá), vai đưa trước/lùi khi với xa/gần;
    ///    hướng khuỷu chọn tự động để cổ tay ít gập nhất, rồi lọc bằng lò xo tới hạn.
    ///  • Xoay sấp/ngửa cẳng tay (−80…+88°) chia dần cho 2 xương xoắn → nếp áo xoắn tự nhiên; xương cổ tay ảo
    ///    của HandRig xoay theo cẳng tay nên da cổ tay uốn mượt trong ống tay áo.
    ///  • Blendshape "gập khuỷu" làm nếp vải khuỷu sâu dần theo góc gập.
    ///  • Lắc theo bước đi, trễ/nghiêng khi xoay camera, nhịp thở – lò xo tới hạn giải đúng nghiệm (không phụ thuộc FPS).
    ///  • Chuột/bàn phím/tay cầm VERA Band: tay phải theo HandController; tay trái nghỉ. Webcam (MediaPipe): 21 điểm.
    /// Không cấp phát bộ nhớ mỗi khung.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class ArmRig : MonoBehaviour
    {
        static readonly int[] Parent = { -1, 0, 1, 2, 3, 0, 5, 6, 7, 0, 9, 10, 11, 0, 13, 14, 15, 0, 17, 18, 19 };
        public const float UpperLen = 0.29f, ForeLen = 0.255f;
        const float Reach = 0.45f;
        public const float ElbowMaxFlexDeg = 150f;
        public const float PronationMinDeg = -80f, PronationMaxDeg = 88f;

        class Euro
        {
            Vector3 x, dx, y;
            bool ready;
            public void Reset() => ready = false;

            public Vector3 F(Vector3 v, float dt, float minCutoff, float beta, float dead = 0f)
            {
                if (!ready) { ready = true; x = y = v; dx = Vector3.zero; return v; }
                dx = Vector3.Lerp(dx, (v - x) / dt, Alpha(1f, dt));
                x = Vector3.Lerp(x, v, Alpha(minCutoff + beta * dx.magnitude, dt));
                Vector3 d = x - y;
                float m = d.magnitude;
                if (m > dead) y += d * ((m - dead) / m);
                return y;
            }

            static float Alpha(float cutoff, float dt) => 1f / (1f + 1f / (2f * Mathf.PI * cutoff * dt));
        }

        public float ArmMinCutoff = 1.0f, ArmBeta = 2.5f, FingerMinCutoff = 1.2f, FingerBeta = 25f;
        [Tooltip("Hệ số lắc/trễ của cánh tay (0 = tắt)")]
        public float motionAmount = 1f;

        class Arm
        {
            public readonly Euro fElbow = new Euro(), fWrist = new Euro();
            public readonly Euro[] fBone = NewEuros();
            static Euro[] NewEuros() { var e = new Euro[21]; for (int i = 0; i < 21; i++) e[i] = new Euro(); return e; }
            public bool tracked;
            public void ResetFilters() { fElbow.Reset(); fWrist.Reset(); foreach (var e in fBone) e.Reset(); }
            public GameObject root;
            public SkinnedMeshRenderer sleeve;
            public readonly Transform[] bones = new Transform[TayNguoiLuoi.SleeveBoneCount];
            public HandRig leftRestHand;
            public readonly float[] boneLen = new float[21];
            public int lenSamples;
            public Vector3 shoulder, elbow, wrist;
            public readonly Vector3[] fingers = new Vector3[21];
            public readonly Vector3[] filteredLocal = new Vector3[21];
            public float poleAngle, poleVel;
            public Vector3 shoulderShift, shoulderShiftVel;
            public Vector3 lastForeDir;
            public Vector3 clothPos, clothVel, clothLag;
            public bool clothReady;
            public MaterialPropertyBlock mpb;
            public Transform button;
            public bool foreReady;
            public bool poleReady;
            public float elbowFlex, pronation, pronationRaw, wristDev;
            public int lod = -1;
        }

        public Transform cameraTransform;
        public HandController hand;

        public bool Driving { get; private set; }
        public Vector3 RightPalm { get; private set; }
        public Vector3 RightElbow => arms[0] != null ? arms[0].elbow : Vector3.zero;
        public Vector3 RightWrist => arms[0] != null ? arms[0].wrist : Vector3.zero;
        public Vector3 RightShoulder => arms[0] != null && arms[0].root != null ? arms[0].shoulder : Shoulder(0);
        public Vector3 FingerJoint(int i) => arms[0] != null && i >= 0 && i < 21 ? arms[0].fingers[i] : Vector3.zero;
        /// <summary>Góc gập khuỷu (độ, 0 = duỗi thẳng), xoay sấp (độ, 0 = ngón cái hướng lên), độ gập cổ tay (độ).</summary>
        public float ElbowFlexDeg(int side) => arms[side] != null ? arms[side].elbowFlex : 0f;
        public float PronationDeg(int side) => arms[side] != null ? arms[side].pronation : 0f;
        /// <summary>Góc sấp/ngửa thật của bàn tay so với cẳng tay (chưa kẹp) – để kiểm thử.</summary>
        public float PronationRawDeg(int side) => arms[side] != null ? arms[side].pronationRaw : 0f;
        public float WristDeviationDeg(int side) => arms[side] != null ? arms[side].wristDev : 0f;
        public SkinnedMeshRenderer SleeveRenderer(int side) => arms[side] != null ? arms[side].sleeve : null;
        /// <summary>Vai, khuỷu, cổ tay (miệng tay áo) khung gần nhất.</summary>
        public void JointPositions(int side, out Vector3 shoulder, out Vector3 elbow, out Vector3 wrist)
        {
            var a = arms[side];
            shoulder = a != null ? a.shoulder : Vector3.zero; elbow = a != null ? a.elbow : Vector3.zero; wrist = a != null ? a.wrist : Vector3.zero;
        }
        public HandRig LeftHand => arms[1] != null ? arms[1].leftRestHand : null;
        /// <summary>Độ lệch lắc/trễ hiện tại áp cho vai (thế giới, m).</summary>
        public Vector3 SwayOffset { get; private set; }

        readonly Arm[] arms = new Arm[2];
        float stepDt = 1f / 60f;
        static readonly int ClothLagId = Shader.PropertyToID("_ClothLag");
        /// <summary>Độ trễ quán tính của vải tay áo (m) – để kiểm thử.</summary>
        public Vector3 ClothLag(int side) => arms[side] != null ? arms[side].clothLag : Vector3.zero;
        int drivingSide;
        Material sleeveMat, buttonMat;

        // Trạng thái chuyển động
        Vector3 lagPos, lagVel, bobPos, bobVel;
        float leanDeg, leanVel;
        float prevYaw, prevPitch;
        Vector3 prevCamPos;
        bool motionReady;
        float walkPhase, walkAmp, walkAmpVel, motionTime;
        Vector3 bodyVelSm, bodyVelSmV, bodyVelLocalLag;

        void Start()
        {
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            if (hand == null) hand = HandController.Active != null ? HandController.Active : FindAnyObjectByType<HandController>();
            if (hand != null && hand.hand != null)
            {
                hand.hand.EnsureBuilt();
                hand.hand.SetSleeveVisible(false);
                SetHandRenderers(true);
            }
        }

        void OnEnable() => HardwareProfile.Changed += OnHardwareChanged;

        void OnDisable()
        {
            HardwareProfile.Changed -= OnHardwareChanged;
            if (hand != null && hand.hand != null) hand.hand.DrivenByArm = false;
        }

        void OnHardwareChanged()
        {
            for (int i = 0; i < 2; i++) if (arms[i] != null) ApplySleeveLod(arms[i], i);
        }

        static int LodFromProfile() => HardwareProfile.Muc switch
        {
            MucDoHoa.SieuNet => 0,
            MucDoHoa.Cao => 1,
            MucDoHoa.Vua => 2,
            _ => 3
        };

        Vector3 Shoulder(int side)
        {
            if (cameraTransform == null) return Vector3.zero;
            var flat = Quaternion.Euler(0f, cameraTransform.eulerAngles.y, 0f);
            Vector3 local = new Vector3(side == 0 ? 0.185f : -0.185f, -0.245f, -0.06f);
            // nghiêng vai khi xoay camera
            local = Quaternion.AngleAxis(leanDeg, Vector3.forward) * local;
            return cameraTransform.position + flat * local + SwayOffset;
        }

        void LateUpdate()
        {
            Step(Time.unscaledDeltaTime);
            ApplySleeves();
        }

        // Cài đặt ẩn cánh tay: chỉ tắt hình ống tay áo / cánh tay, bàn tay vẫn hiện và cầm nắm như thường
        readonly List<Renderer> hiddenSleeves = new List<Renderer>();
        bool sleevesHidden;

        void ApplySleeves()
        {
            bool hide = HiddenByUser && !HiddenByCli;
            if (hide == sleevesHidden) return;
            sleevesHidden = hide;
            if (hide)
            {
                hiddenSleeves.Clear();
                for (int i = 0; i < 2; i++)
                {
                    if (arms[i] == null || arms[i].root == null) continue;
                    foreach (var r in arms[i].root.GetComponentsInChildren<Renderer>(true))
                        if (r.enabled && r.GetComponentInParent<HandRig>(true) == null) { r.enabled = false; hiddenSleeves.Add(r); }
                }
            }
            else
            {
                foreach (var r in hiddenSleeves) if (r != null) r.enabled = true;
                hiddenSleeves.Clear();
            }
        }

        /// <summary>Một bước cập nhật hai cánh tay (gọi từ LateUpdate; kiểm thử gọi trực tiếp).</summary>
        static int hideFlag = -1;
        /// <summary>Cờ dòng lệnh -vera-an-tay: ẩn tay và cánh tay (đo A/B chi phí khung hình của tay).</summary>
        public static bool HiddenByCli
        {
            get
            {
                if (hideFlag < 0)
                {
                    hideFlag = 0;
                    foreach (var a in System.Environment.GetCommandLineArgs())
                        if (string.Equals(a, "-vera-an-tay", System.StringComparison.OrdinalIgnoreCase)) hideFlag = 1;
                }
                return hideFlag == 1;
            }
        }

        /// <summary>Cài đặt → Điều khiển → "Hiện cánh tay (ống tay áo)" (bản đi thi): tắt cho đỡ vướng tầm nhìn, bàn tay vẫn hiện.</summary>
        public const string HideArmsPrefKey = "an_canh_tay";
        static int userHide = -1;
        public static bool HiddenByUser
        {
            get { if (userHide < 0) userHide = PlayerPrefs.GetInt(HideArmsPrefKey, 0); return userHide == 1; }
            set { userHide = value ? 1 : 0; PlayerPrefs.SetInt(HideArmsPrefKey, userHide); PlayerPrefs.Save(); }
        }
        bool hidByMe;

        public void Step(float dt)
        {
            if (cameraTransform == null) return;
            if (HiddenByCli)
            {
                for (int i = 0; i < 2; i++) if (arms[i] != null && arms[i].root.activeSelf) arms[i].root.SetActive(false);
                if (hand != null && hand.hand != null && hand.hand.gameObject.activeSelf) hand.hand.gameObject.SetActive(false);
                hidByMe = true;
                return;
            }
            if (hidByMe)
            {
                hidByMe = false;
                if (hand != null && hand.hand != null && !hand.hand.gameObject.activeSelf) hand.hand.gameObject.SetActive(true);
            }
            dt = Mathf.Clamp(dt, 1e-4f, 0.1f);
            stepDt = dt;
            UpdateMotion(dt);

            var at = ArmTracking.Instance;
            bool master = MediaPipeHandSource.Instance == null || MediaPipeHandSource.Instance.handTrackingEnabled;
            ArmTracking.Side driver = null;

            for (int i = 0; i < 2; i++)
            {
                var s = at == null ? null : i == 0 ? at.Right : at.Left;
                bool show = master && s != null && (s.handSeen || s.armSeen);
                if (arms[i] == null) arms[i] = Build(i);
                if (!arms[i].root.activeSelf) arms[i].root.SetActive(true);
                if (show != arms[i].tracked) { arms[i].tracked = show; arms[i].ResetFilters(); }

                if (show)
                {
                    PoseTracked(arms[i], s, i, dt);
                    if (s.handSeen && driver == null) { driver = s; drivingSide = i; }
                }
                else
                {
                    PoseMouseOrRest(arms[i], i, dt);
                }
            }

            Drive(driver);
        }

        // ------------------------------------------------------------------
        // Lắc / trễ / nghiêng (lò xo tới hạn giải đúng → không phụ thuộc FPS)
        // ------------------------------------------------------------------
        public static void Spring(ref Vector3 x, ref Vector3 v, Vector3 target, float omega, float dt)
        {
            float e = Mathf.Exp(-omega * dt);
            Vector3 d = x - target;
            Vector3 tmp = (v + omega * d) * dt;
            v = (v - omega * tmp) * e;
            x = target + (d + tmp) * e;
        }

        public static void Spring(ref float x, ref float v, float target, float omega, float dt)
        {
            float e = Mathf.Exp(-omega * dt);
            float d = x - target;
            float tmp = (v + omega * d) * dt;
            v = (v - omega * tmp) * e;
            x = target + (d + tmp) * e;
        }

        void UpdateMotion(float dt)
        {
            Transform cam = cameraTransform;
            float yaw = cam.eulerAngles.y, pitch = cam.eulerAngles.x;
            Vector3 pos = cam.position;
            if (!motionReady || (pos - prevCamPos).sqrMagnitude > 0.5f * 0.5f)
            {
                motionReady = true;
                prevYaw = yaw; prevPitch = pitch; prevCamPos = pos;
                lagPos = lagVel = bobPos = bobVel = Vector3.zero;
                leanDeg = leanVel = 0f;
            }
            float yawRate = Mathf.Clamp(Mathf.DeltaAngle(prevYaw, yaw) / dt, -720f, 720f);
            float pitchRate = Mathf.Clamp(Mathf.DeltaAngle(prevPitch, pitch) / dt, -720f, 720f);
            Vector3 vel = (pos - prevCamPos) / dt;
            vel.y = 0f;
            float speed = Mathf.Min(vel.magnitude, 4f);
            prevYaw = yaw; prevPitch = pitch; prevCamPos = pos;
            motionTime += dt;
            // tăng/giảm tốc thân người → vải văng theo (độ lệch vận tốc lọc so với vận tốc thật)
            Spring(ref bodyVelSm, ref bodyVelSmV, vel, 6f, dt);
            bodyVelLocalLag = Quaternion.Inverse(Quaternion.Euler(0f, yaw, 0f)) * Vector3.ClampMagnitude((vel - bodyVelSm) * -0.02f, 0.01f);

            float k = motionAmount;
            // Trễ quán tính: xoay phải → tay trôi sang trái trong khung nhìn, cúi xuống → tay trôi lên
            Vector3 lagTarget = new Vector3(Mathf.Clamp(-yawRate * 0.00011f, -0.035f, 0.035f),
                                            Mathf.Clamp(pitchRate * 0.00008f, -0.025f, 0.025f), 0f) * k;
            Spring(ref lagPos, ref lagVel, lagTarget, 11f, dt);
            float leanTarget = Mathf.Clamp(-yawRate * 0.012f, -7f, 7f) * k;
            Spring(ref leanDeg, ref leanVel, leanTarget, 9f, dt);

            // Bước đi: 1 chu kỳ = 2 bước (≈ 1,4 m)
            Spring(ref walkAmp, ref walkAmpVel, Mathf.Clamp01(speed / 1.4f), 6f, dt);
            walkPhase = Mathf.Repeat(walkPhase + speed * dt * (Mathf.PI * 2f / 1.4f), Mathf.PI * 2f);
            float a = walkAmp * k;
            Vector3 bobTarget = new Vector3(Mathf.Sin(walkPhase) * 0.009f * a,
                                            (-Mathf.Abs(Mathf.Cos(walkPhase)) * 0.011f + 0.0055f) * a + Mathf.Sin(motionTime * 1.55f) * 0.0012f * k,
                                            0f);
            Spring(ref bobPos, ref bobVel, bobTarget, 18f, dt);

            Quaternion camRot = cam.rotation;
            Quaternion flat = Quaternion.Euler(0f, yaw, 0f);
            SwayOffset = camRot * lagPos + flat * bobPos;
        }

        Vector3 swayApplied, swayRigPos;
        bool swayValid;

        /// <summary>Cộng độ lắc vào vị trí tay phải; nếu HandController chưa đặt lại vị trí khung này thì gỡ độ lắc cũ trước (không trôi).</summary>
        void ApplyHandSway(Transform t, Vector3 sway)
        {
            if (swayValid && (t.position - swayRigPos).sqrMagnitude < 1e-12f) t.position -= swayApplied;
            t.position += sway;
            swayApplied = sway;
            swayRigPos = t.position;
            swayValid = true;
        }

        /// <summary>Độ lệch lắc cho từng tay (tay phải/trái lệch pha nửa chu kỳ khi đi).</summary>
        Vector3 HandSway(int side)
        {
            float swing = Mathf.Sin(walkPhase) * 0.012f * walkAmp * motionAmount * (side == 0 ? 1f : -1f);
            Quaternion flat = Quaternion.Euler(0f, cameraTransform.eulerAngles.y, 0f);
            return SwayOffset * 1.25f + flat * new Vector3(0f, 0f, swing);
        }

        // ------------------------------------------------------------------
        // Điều khiển bằng chuột / tay cầm / nghỉ
        // ------------------------------------------------------------------
        // ------------------------------------------------------------------
        // Hành vi theo hoàn cảnh (như người thật): với tới vật theo đường cong, rảnh thì thả tay xuống,
        // đi bộ thì buông tay và vung theo bước, tay phải rót/hút thì tay trái đỡ cốc.
        // ------------------------------------------------------------------
        float relaxW, relaxVel, leftRelaxW, leftRelaxVel, engageW, engageVel;
        bool engageReady;
        float poleBackWeight;
        Vector3 lastCtrlPos, lastOutPos;
        Quaternion lastCtrlRot = Quaternion.identity, lastOutRot = Quaternion.identity;
        bool ctrlReady;
        float assistW, assistVel;
        Transform assistCached;
        Collider[] assistCols = System.Array.Empty<Collider>();
        Renderer assistRend;
        Transform assistRendOwner;
        Vector3 assistPoint;
        Quaternion assistRot = Quaternion.identity;

        /// <summary>Mức tay phải rời chỗ nghỉ để làm việc (0 = nghỉ, 1 = tới tay cầm/vật).</summary>
        public float EngageWeight => engageW;
        /// <summary>Mức thả lỏng tay phải khi đi bộ (0 = tư thế nghỉ trước người, 1 = buông xuống).</summary>
        public float RelaxWeight => relaxW;
        /// <summary>Mức tay trái đang đỡ cốc/bình (0..1).</summary>
        public float AssistWeight => assistW;
        /// <summary>Kiểm thử: ép tay trái đỡ vật này.</summary>
        public Transform AssistOverride { get; set; }
        /// <summary>Khoảng vai trái – điểm đỡ lần gần nhất (m), để chẩn đoán.</summary>
        public float AssistReachM { get; private set; } = -1f;
        /// <summary>Vị trí bàn tay (hệ camera) ngay khi ArmRig đặt xong khung này.</summary>
        public Vector3 HandLocalOut(int side) => side == 0 ? handLocalOut0 : handLocalOut1;
        Vector3 handLocalOut0, handLocalOut1;
        /// <summary>Vật tay trái đang đỡ (null nếu không).</summary>
        public Transform AssistTarget { get; private set; }

        static bool IsAssistAct(HandController.Act act) =>
            act == HandController.Act.Pour || act == HandController.Act.Suck || act == HandController.Act.PipetteDrain ||
            act == HandController.Act.Drop || act == HandController.Act.Scoop || act == HandController.Act.Bubble ||
            act == HandController.Act.Measure || act == HandController.Act.PaperTest || act == HandController.Act.Spray;

        void PoseMouseOrRest(Arm a, int side, float dt)
        {
            Quaternion flat = Quaternion.Euler(0f, cameraTransform.eulerAngles.y, 0f);
            Vector3 camPos = cameraTransform.position;
            bool walking = walkAmp > 0.35f;
            float swing = Mathf.Sin(walkPhase) * walkAmp * motionAmount;

            if (side == 0 && hand != null && hand.hand != null)
            {
                HandRig rig = hand.hand;
                rig.EnsureBuilt();
                rig.SkipIdleUpdate = false;
                rig.DrivenByArm = true;
                rig.ClearLandmarkControl();
                Transform t = rig.transform;

                // Vị trí/hướng HandController đặt khung này (nếu nó chưa cập nhật thì dùng lần trước, không trôi)
                bool fresh = !ctrlReady || (t.position - lastOutPos).sqrMagnitude > 1e-12f || Quaternion.Angle(t.rotation, lastOutRot) > 0.01f;
                if (fresh) { lastCtrlPos = t.position; lastCtrlRot = t.rotation; ctrlReady = true; }
                Vector3 ctrlPos = lastCtrlPos;
                Quaternion ctrlRot = lastCtrlRot;

                bool empty = hand.Held == null;
                bool scripted = hand.OverrideAimPoint != null;
                // Tay chỉ rời chỗ nghỉ khi có việc (cầm, thao tác, bấm vào vật); di chuột không kéo tay theo.
                // Tay cầm VERA có dữ liệu (25/11/2026): tay phải luôn đưa ra và mô phỏng hướng thật của BMI160 (HandController
                // đã đặt hướng theo tay cầm), để người chơi nhìn tay ảo là thấy tay cầm đang hướng thế nào
                bool veraLive = Khkt.Input.VeraPointer.Instance != null && Khkt.Input.VeraPointer.Instance.HasControl;
                bool engaged = hand.HandEngaged || veraLive;
                bool instant = scripted && !hand.Fetching;
                if (instant || !engageReady) { engageW = engaged ? 1f : 0f; engageVel = 0f; engageReady = true; }
                else Spring(ref engageW, ref engageVel, engaged ? 1f : 0f, engaged ? 9f : 7f, dt);
                engageW = Mathf.Clamp01(engageW);

                // Đi bộ (tay rảnh): buông xuống, vung theo bước
                float relaxTarget = !engaged && walking ? 1f : 0f;
                Spring(ref relaxW, ref relaxVel, relaxTarget, relaxTarget > relaxW ? 4.5f : 8f, dt);
                relaxW = Mathf.Clamp01(relaxW);

                RestPose(0, flat, camPos, swing, out Vector3 restPos, out Quaternion restRot);
                Vector3 hangPos = camPos + flat * new Vector3(0.23f, -0.83f + 0.015f * Mathf.Abs(swing), 0.05f + 0.13f * swing);
                // tay buông: lòng bàn tay quay vào đùi, ngón cái hướng trước (cẳng tay trung tính)
                Quaternion hangRot = flat * Quaternion.Euler(78f - 14f * swing, -6f, -80f);
                Vector3 idlePos = Vector3.Lerp(restPos, hangPos, relaxW) + HandSway(0);
                Quaternion idleRot = Quaternion.Slerp(restRot, hangRot, relaxW);

                // Vươn tay theo cung: khuỷu nâng, bàn tay nhấc lên giữa đường; cổ tay xoay về hướng cầm sớm hơn
                // lò xo tới hạn có đuôi dài: coi như tới nơi ở 92% để tay chạm vật dứt khoát
                float w = Mathf.Clamp01(engageW / 0.92f);
                hand.ReachProgress = engaged ? w : 0f;
                float lift = Mathf.Sin(w * Mathf.PI) * (0.045f + 0.03f * Mathf.Clamp01((ctrlPos - idlePos).magnitude / 0.5f));
                Vector3 pos = Vector3.Lerp(idlePos, ctrlPos, w) + Vector3.up * lift;
                float wr = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(w / 0.7f));
                Quaternion rot = Quaternion.Slerp(idleRot, ctrlRot, wr);
                if (w < 0.35f && empty) rig.SetTargetPose(HandPoseId.Nghi);
                if (scripted && empty) pos = ctrlPos;   // ngắm theo kịch bản (kiểm thử/tự động): tay tới thẳng chỗ ngắm
                t.SetPositionAndRotation(pos, rot);
                lastOutPos = t.position; lastOutRot = t.rotation;
                handLocalOut0 = cameraTransform.InverseTransformPoint(t.position);

                rig.Tick(dt);
                poleBackWeight = relaxW * (1f - engageW);
                PlaceConnectedArm(a, side, rig, dt);
                SyncFingerTelemetryFromRig(a, rig);
                return;
            }

            if (a.leftRestHand != null)
            {
                HandRig rig = a.leftRestHand;
                rig.EnsureBuilt();
                rig.DrivenByArm = true;

                // Đứng: tay trái đặt gọn trước bàn; đi bộ: buông xuống, vung ngược pha tay phải
                Spring(ref leftRelaxW, ref leftRelaxVel, walking ? 1f : 0f, 5f, dt);
                leftRelaxW = Mathf.Clamp01(leftRelaxW);
                RestPose(1, flat, camPos, swing, out Vector3 restPos, out Quaternion restRot);
                restPos += HandSway(1);
                Vector3 hangPos = camPos + flat * new Vector3(-0.23f, -0.83f + 0.015f * Mathf.Abs(swing), 0.05f - 0.13f * swing);
                Quaternion hangRot = flat * Quaternion.Euler(78f + 14f * swing, 6f, 80f);
                Vector3 pos = Vector3.Lerp(restPos, hangPos, leftRelaxW);
                Quaternion rot = Quaternion.Slerp(restRot, hangRot, leftRelaxW);

                // Đỡ cốc: tay phải đang rót/hút/nhỏ/đo vào một vật chứa trong tầm tay trái
                Transform want = AssistOverride;
                if (want == null && hand != null && hand.Held != null && hand.TargetContainer != null && IsAssistAct(hand.Current))
                    want = hand.TargetContainer.transform;
                bool canAssist = want != null && ComputeAssist(want, flat, rig);
                Spring(ref assistW, ref assistVel, canAssist ? 1f : 0f, canAssist ? 7f : 9f, dt);
                assistW = Mathf.Clamp01(assistW);
                AssistTarget = canAssist ? want : null;
                if (assistW > 0.001f && assistCached != null)
                {
                    Vector3 gl = rig.Grip != null ? rig.Grip.localPosition : Vector3.zero;
                    Vector3 apos = assistPoint - assistRot * gl;
                    pos = Vector3.Lerp(pos, apos, assistW);
                    rot = Quaternion.Slerp(rot, assistRot, assistW);
                }
                rig.SetTargetPose(assistW > 0.35f ? HandPoseId.ChumCam : HandPoseId.Nghi);
                rig.TargetContactObject = assistW > 0.7f ? assistCached : null;
                rig.transform.SetPositionAndRotation(pos, rot);
                handLocalOut1 = cameraTransform.InverseTransformPoint(pos);
                rig.Tick(dt);
                poleBackWeight = leftRelaxW * (1f - assistW);
                PlaceConnectedArm(a, side, rig, dt);
            }
        }

        /// <summary>
        /// Tư thế nghỉ hai tay (kiểu tay góc nhìn thứ nhất của game): hai bàn tay cong tự nhiên ở hai bên phía dưới tầm nhìn,
        /// đi theo một nửa góc cúi/ngửa của đầu (vẫn thấy tay khi nhìn thẳng), thở nhẹ (~0,23 Hz).
        /// </summary>
        void RestPose(int side, Quaternion flat, Vector3 camPos, float swing, out Vector3 pos, out Quaternion rot)
        {
            float pitch = Mathf.DeltaAngle(0f, cameraTransform.eulerAngles.x);
            Quaternion frame = flat * Quaternion.Euler(Mathf.Clamp(pitch * 0.55f, -12f, 30f), 0f, 0f);
            float sg = side == 0 ? 1f : -1f;
            float tt = Time.time;
            float breath = Mathf.Sin(tt * (Mathf.PI * 2f * 0.23f)) * 0.0045f;
            float drift = Mathf.Sin(tt * 0.61f + side * 1.7f) * 0.0018f;
            pos = camPos + frame * new Vector3(0.195f * sg + drift, -0.195f + breath, 0.42f + breath * 0.4f);
            // cổ tay hơi nghiêng cho mép ngón cái nhô lên (dáng cầm nghỉ tự nhiên, thấy ngón cong)
            rot = frame * Quaternion.Euler(18f + breath * 120f, -16f * sg, -26f * sg);
        }

        /// <summary>Điểm và hướng tay trái ôm bên trái vật chứa (lòng bàn tay quay vào thành cốc, ngón vòng ra trước).</summary>
        bool ComputeAssist(Transform target, Quaternion flat, HandRig rig)
        {
            if (assistCached != target)
            {
                assistCached = target;
                assistCols = target.GetComponentsInChildren<Collider>(true);
            }
            bool has = false;
            Bounds b = default;
            for (int i = 0; i < assistCols.Length; i++)
            {
                var c = assistCols[i];
                if (c == null || c.isTrigger || !c.enabled) continue;
                if (!has) { b = c.bounds; has = true; } else b.Encapsulate(c.bounds);
            }
            if (!has)
            {
                if (assistRend == null || assistRendOwner != target) { assistRend = target.GetComponentInChildren<Renderer>(); assistRendOwner = target; }
                if (assistRend != null) { b = assistRend.bounds; has = true; }
            }
            if (!has) return false;
            Vector3 right = flat * Vector3.right;
            float r = Mathf.Max(b.extents.x, b.extents.z);
            Vector3 axisPt = new Vector3(b.center.x, b.center.y - b.extents.y * 0.25f, b.center.z);
            assistPoint = axisPt - right * Mathf.Max(0.004f, r - 0.026f);
            // bàn tay trái: ngón chỉ về trước-phải, lòng bàn tay (−y) xoay sang phải về phía vật
            assistRot = flat * Quaternion.Euler(8f, 28f, -78f);
            Vector3 sh = Shoulder(1);
            AssistReachM = (assistPoint - sh).magnitude;
            return AssistReachM < UpperLen + ForeLen + 0.30f;   // người cúi/nghiêng thân để đỡ (vai dời theo IK)
        }

        void PoseTracked(Arm a, ArmTracking.Side s, int side, float dt)
        {
            poleBackWeight = 0f;
            Quaternion cam = cameraTransform.rotation;
            Vector3 sh = Shoulder(side);
            Vector3 elbow, wrist;
            if (s.armSeen && s.elbowConf >= 0.5f && (s.elbow - s.shoulder).sqrMagnitude > 1e-4f)
            {
                elbow = sh + cam * (s.elbow - s.shoulder).normalized * UpperLen;
                Vector3 fore = s.wrist - s.elbow;
                wrist = elbow + (fore.sqrMagnitude > 1e-4f ? cam * fore.normalized : (elbow - sh).normalized) * ForeLen;
            }
            else
            {
                Vector3 target;
                if (s.armSeen && s.wristConf >= 0.5f) target = sh + cam * (s.wrist - s.shoulder);
                else
                {
                    var img = s.wristImage;
                    var dir = new Vector3((0.5f - img.x) * 1.4f, (0.5f - img.y) * 1.0f, 1f).normalized;
                    target = cameraTransform.position + cam * dir * Reach;
                }
                Vector3 pole = PoleDir(sh, target, side, NaturalPole(side));
                SolveIk(sh, target, pole, out elbow, out wrist);
            }

            var inv = Quaternion.Inverse(cam);
            float fdt = Mathf.Max(dt, 1e-3f);
            Vector3 eL = a.fElbow.F(inv * (elbow - sh), fdt, ArmMinCutoff, ArmBeta, 0.004f);
            Vector3 wL = a.fWrist.F(inv * (wrist - sh), fdt, ArmMinCutoff, ArmBeta, 0.004f);
            eL = (eL.sqrMagnitude > 1e-6f ? eL.normalized : Vector3.forward) * UpperLen;
            Vector3 fw = wL - eL;
            wL = eL + (fw.sqrMagnitude > 1e-6f ? fw.normalized : eL.normalized) * ForeLen;
            elbow = sh + cam * eL;
            wrist = sh + cam * wL;

            HandRig rig = side == 0 && hand != null ? hand.hand : a.leftRestHand;
            if (rig != null) rig.DrivenByArm = true;
            if (rig != null && s.handSeen)
            {
                for (int i = 1; i < 21; i++)
                {
                    float len = (s.hand[i] - s.hand[Parent[i]]).magnitude;
                    if (a.lenSamples < 30) a.boneLen[i] = (a.boneLen[i] * a.lenSamples + len) / (a.lenSamples + 1);
                }
                if (a.lenSamples < 30) a.lenSamples++;

                var fl = a.filteredLocal;
                fl[0] = Vector3.zero;
                a.fingers[0] = wrist;
                for (int i = 1; i < 21; i++)
                {
                    Vector3 d = a.fBone[i].F(s.hand[i] - s.hand[Parent[i]], fdt, FingerMinCutoff, FingerBeta, 0.0015f);
                    Vector3 dir = d.sqrMagnitude > 1e-8f ? d.normalized : Vector3.forward;
                    float len = a.boneLen[i] > 1e-4f ? a.boneLen[i] : 0.025f;
                    fl[i] = fl[Parent[i]] + dir * len;
                    a.fingers[i] = wrist + cam * fl[i];
                }

                InAppHandTracker.MapLandmarksToHandRig(fl, rig, cam, wrist);

                if (side == drivingSide)
                    RightPalm = (a.fingers[0] + a.fingers[5] + a.fingers[9] + a.fingers[13] + a.fingers[17]) * 0.2f;
            }
            else if (rig != null)
            {
                rig.Tick(dt);
            }

            ApplyArm(a, side, sh, elbow, wrist, rig, NaturalPoleVector(sh, wrist, elbow));
        }

        // ------------------------------------------------------------------
        // IK hai khớp có giới hạn sinh lý
        // ------------------------------------------------------------------
        static float NaturalPole(int side) => 32f;

        /// <summary>Hướng khuỷu ứng với góc α (0 = thẳng xuống, 90 = chĩa ra ngoài) quanh trục vai–cổ tay.</summary>
        Vector3 PoleDir(Vector3 sh, Vector3 w, int side, float alphaDeg)
        {
            Vector3 axis = w - sh;
            axis = axis.sqrMagnitude > 1e-8f ? axis.normalized : cameraTransform.forward;
            // gốc hướng khuỷu: "xuống"; cánh tay càng thẳng đứng (buông) thì khuỷu càng hướng ra sau như người thật
            Vector3 back = -Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            float vertical = Mathf.Abs(Vector3.Dot(axis, Vector3.down));
            float bw = poleBackWeight * Mathf.Clamp01((vertical - 0.45f) / 0.45f);   // chỉ khi tay đang buông
            Vector3 refDir = (Vector3.down + back * (2.5f * bw)).normalized;
            Vector3 down = Vector3.ProjectOnPlane(refDir, axis);
            if (down.sqrMagnitude < 1e-6f) down = Vector3.ProjectOnPlane(-cameraTransform.up, axis);
            down.Normalize();
            Vector3 lateral = cameraTransform.right * (side == 0 ? 1f : -1f);
            Vector3 outv = Vector3.Cross(axis, down);
            if (Vector3.Dot(outv, lateral) < 0f) outv = -outv;
            float r = alphaDeg * Mathf.Deg2Rad;
            return (down * Mathf.Cos(r) + outv * Mathf.Sin(r)).normalized;
        }

        static Vector3 NaturalPoleVector(Vector3 sh, Vector3 w, Vector3 elbow)
        {
            Vector3 axis = (w - sh).normalized;
            Vector3 p = Vector3.ProjectOnPlane(elbow - sh, axis);
            return p.sqrMagnitude > 1e-8f ? p.normalized : Vector3.down;
        }

        public static void SolveIk(Vector3 sh, Vector3 target, Vector3 pole, out Vector3 elbow, out Vector3 wrist)
        {
            Vector3 to = target - sh;
            float minR = MinReach();
            float d = Mathf.Clamp(to.magnitude, minR, UpperLen + ForeLen - 0.001f);
            Vector3 dir = to.sqrMagnitude > 1e-8f ? to.normalized : Vector3.forward;
            wrist = sh + dir * d;
            float a = (UpperLen * UpperLen - ForeLen * ForeLen + d * d) / (2f * d);
            float h = Mathf.Sqrt(Mathf.Max(0f, UpperLen * UpperLen - a * a));
            Vector3 bend = Vector3.ProjectOnPlane(pole, dir);
            if (bend.sqrMagnitude < 1e-8f) bend = Vector3.ProjectOnPlane(Vector3.down, dir);
            elbow = sh + dir * a + bend.normalized * h;
        }

        /// <summary>Khoảng vai–cổ tay nhỏ nhất ứng với khuỷu gập tối đa 150°.</summary>
        public static float MinReach()
        {
            float inner = (180f - ElbowMaxFlexDeg) * Mathf.Deg2Rad;
            return Mathf.Sqrt(UpperLen * UpperLen + ForeLen * ForeLen - 2f * UpperLen * ForeLen * Mathf.Cos(inner));
        }

        void PlaceConnectedArm(Arm a, int side, HandRig rig, float dt)
        {
            Vector3 handFwd = rig.WristAttach != null ? rig.WristAttach.forward : rig.transform.forward;
            // Miệng tay áo nằm trên trục cẳng tay ngay sau khớp cổ tay: cổ tay thẳng thì trùng WristAttach,
            // cổ tay gập nhiều thì bàn tay gập ra ngoài cổ áo (không để vòng tay áo trùm lên gan tay)
            Vector3 w = rig.WristAttach != null ? rig.WristAttach.position : rig.transform.position;
            if (rig.ForearmBone != null && rig.WristAttach != null && a.foreReady)
            {
                Vector3 wc = rig.ForearmBone.position;
                float back = Vector3.Distance(wc, w);
                float devPrev = Vector3.Angle(a.lastForeDir, handFwd);
                float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(30f, 60f, devPrev));
                Vector3 axis = Vector3.Slerp(handFwd, a.lastForeDir, t).normalized;
                w = wc - axis * back;
            }
            Vector3 handUpV = rig.WristAttach != null ? rig.WristAttach.up : rig.transform.up;
            Vector3 sh0 = Shoulder(side);
            // Xương đòn: vai nhô ra khi với xa, nâng lên khi tay cao (lò xo, không giật)
            Vector3 d0 = w - sh0;
            float dist0 = d0.magnitude;
            Vector3 dir0 = dist0 > 1e-6f ? d0 / dist0 : cameraTransform.forward;
            Vector3 shiftTarget = dir0 * Mathf.Clamp((dist0 - 0.40f) * 0.20f, -0.02f, 0.04f)
                                + Vector3.up * Mathf.Clamp((w.y - sh0.y + 0.10f) * 0.14f, 0f, 0.035f);
            if (!a.poleReady) a.shoulderShift = shiftTarget;
            Spring(ref a.shoulderShift, ref a.shoulderShiftVel, shiftTarget, 10f, dt);
            Vector3 sh = sh0 + a.shoulderShift;
            Vector3 lateralCam = cameraTransform.right * (side == 0 ? 1f : -1f);

            // Vai đưa trước (với xa) hoặc lùi (quá gần) để cổ tay áo luôn khít cổ tay, khuỷu không gập quá 150°
            Vector3 to = w - sh;
            float dist = to.magnitude;
            Vector3 dir = dist > 1e-6f ? to / dist : cameraTransform.forward;
            float maxR = UpperLen + ForeLen - 0.002f, minR = MinReach() + 0.002f;
            if (dist > maxR) sh = w - dir * maxR;
            else if (dist < minR) sh = w - dir * minR;

            // Chọn hướng khuỷu để cẳng tay thẳng hàng với bàn tay nhất (cổ tay ít gập) nhưng gần tư thế tự nhiên
            float natural = NaturalPole(side);
            float best = natural, bestCost = float.MaxValue;
            for (float al = -20f; al <= 140f; al += 10f) Evaluate(al);
            float coarse = best;
            for (float al = coarse - 7.5f; al <= coarse + 7.5f; al += 2.5f) Evaluate(al);

            if (!a.poleReady) { a.poleReady = true; a.poleAngle = best; a.poleVel = 0f; }
            else Spring(ref a.poleAngle, ref a.poleVel, best, 12f, dt);

            Vector3 pole = PoleDir(sh, w, side, a.poleAngle);
            SolveIk(sh, w, pole, out Vector3 elbow, out _);
            ApplyArm(a, side, sh, elbow, w, rig, pole);

            void Evaluate(float al)
            {
                Vector3 p = PoleDir(sh, w, side, al);
                SolveIk(sh, w, p, out Vector3 e, out _);
                Vector3 fore = (w - e).normalized;
                float dev = Vector3.Angle(fore, handFwd);
                float over = Mathf.Max(0f, dev - 25f);
                float c = over * over + 0.04f * (al - natural) * (al - natural);
                // Cẳng tay chỉ sấp/ngửa được −80…+88°: lật vật quá tầm thì khuỷu dang ra / khép vào (xoay vai) như người thật
                Vector3 innerF = Vector3.ProjectOnPlane(-p, fore);
                Vector3 dorsal = Vector3.ProjectOnPlane(handUpV, fore);
                if (innerF.sqrMagnitude > 1e-8f && dorsal.sqrMagnitude > 1e-8f)
                {
                    innerF.Normalize(); dorsal.Normalize();
                    Vector3 lat = -Vector3.Cross(fore, innerF) * (side == 0 ? 1f : -1f);
                    float phi = Mathf.Atan2(Vector3.Dot(dorsal, innerF), Vector3.Dot(dorsal, lat)) * Mathf.Rad2Deg;
                    float ex = Mathf.Max(0f, phi - PronationMaxDeg) + Mathf.Max(0f, PronationMinDeg - phi);
                    c += ex * ex * 1.5f;
                }
                // Khuỷu không cắt qua thân (không sang quá đường giữa)
                float elbowLat = Vector3.Dot(e - sh, lateralCam);
                if (elbowLat < -0.05f) c += (-0.05f - elbowLat) * (-0.05f - elbowLat) * 4e5f;
                if (c < bestCost) { bestCost = c; best = al; }
            }
        }

        /// <summary>Đặt 5 xương tay áo + xương cổ tay ảo của bàn tay; tính góc khuỷu, xoay sấp, độ gập cổ tay.</summary>
        void ApplyArm(Arm a, int side, Vector3 sh, Vector3 elbow, Vector3 w, HandRig rig, Vector3 pole)
        {
            a.shoulder = sh;
            a.elbow = elbow;
            a.wrist = w;
            Vector3 upperDir = (elbow - sh).normalized;
            Vector3 foreDir = (w - elbow).sqrMagnitude > 1e-8f ? (w - elbow).normalized : upperDir;
            Vector3 innerU = Vector3.ProjectOnPlane(-pole, upperDir);
            Vector3 innerF = Vector3.ProjectOnPlane(-pole, foreDir);
            if (innerU.sqrMagnitude < 1e-8f) innerU = Vector3.ProjectOnPlane(Vector3.up, upperDir);
            if (innerF.sqrMagnitude < 1e-8f) innerF = Vector3.ProjectOnPlane(Vector3.up, foreDir);
            innerU.Normalize();
            innerF.Normalize();

            a.elbowFlex = Vector3.Angle(upperDir, foreDir);
            a.lastForeDir = foreDir;
            a.foreReady = true;

            // Xoay sấp/ngửa: góc của mu bàn tay quanh trục cẳng tay (0 = ngón cái hướng lên, + = sấp)
            Vector3 handUp = rig != null && rig.WristAttach != null ? rig.WristAttach.up : innerF;
            Vector3 handFwd = rig != null && rig.WristAttach != null ? rig.WristAttach.forward : foreDir;
            Vector3 dorsal = Vector3.ProjectOnPlane(handUp, foreDir);
            if (dorsal.sqrMagnitude < 1e-8f) dorsal = innerF;
            dorsal.Normalize();
            Vector3 lateral = -Vector3.Cross(foreDir, innerF) * (side == 0 ? 1f : -1f);
            float phi = Mathf.Atan2(Vector3.Dot(dorsal, innerF), Vector3.Dot(dorsal, lateral)) * Mathf.Rad2Deg;
            a.pronationRaw = phi;
            a.pronation = Mathf.Clamp(phi, PronationMinDeg, PronationMaxDeg);
            a.wristDev = Vector3.Angle(foreDir, handFwd);

            float twist = (a.pronation - 45f) * 0.55f;
            Quaternion upperRot = Quaternion.LookRotation(upperDir, innerU);
            Quaternion foreRot = Quaternion.LookRotation(foreDir, innerF);
            var b = a.bones;
            b[TayNguoiLuoi.SleeveBoneUpper].SetPositionAndRotation(sh, upperRot);
            // xương khuỷu tay áo xoay nửa góc gập: vải ôm vòng cung qua khuỷu thay vì gãy gập
            b[TayNguoiLuoi.SleeveBoneElbow].SetPositionAndRotation(elbow, Quaternion.Slerp(upperRot, foreRot, 0.5f));
            b[TayNguoiLuoi.SleeveBoneTwist1].SetPositionAndRotation(elbow + foreDir * (ForeLen * 0.36f), Quaternion.AngleAxis(twist * 0.36f, foreDir) * foreRot);
            b[TayNguoiLuoi.SleeveBoneTwist2].SetPositionAndRotation(elbow + foreDir * (ForeLen * 0.68f), Quaternion.AngleAxis(twist * 0.68f, foreDir) * foreRot);
            b[TayNguoiLuoi.SleeveBoneCuff].SetPositionAndRotation(w, Quaternion.AngleAxis(twist, foreDir) * foreRot);

            if (a.sleeve != null && a.sleeve.sharedMesh != null && a.sleeve.sharedMesh.blendShapeCount > 0)
            {
                a.sleeve.SetBlendShapeWeight(0, Mathf.Clamp01((a.elbowFlex - 15f) / 105f) * 100f);
                if (a.sleeve.sharedMesh.blendShapeCount > 1)
                    a.sleeve.SetBlendShapeWeight(1, Mathf.Clamp01(Mathf.Abs(twist) / 45f) * 100f);
            }

            // Vải trễ theo quán tính: điểm giữa cẳng tay đi theo lò xo mềm, độ lệch đưa vào shader (kẹp 1,5 cm)
            // tính trong hệ thân người (theo camera, bỏ nghiêng): đi đều thì vải không trễ, chỉ trễ khi tay cử động hoặc đổi tốc độ
            Quaternion body = Quaternion.Euler(0f, cameraTransform.eulerAngles.y, 0f);
            Vector3 mid = Quaternion.Inverse(body) * ((elbow + w) * 0.5f - cameraTransform.position) + bodyVelLocalLag;
            if (!a.clothReady || (a.clothPos - mid).sqrMagnitude > 0.25f) { a.clothPos = mid; a.clothVel = Vector3.zero; a.clothReady = true; }
            else Spring(ref a.clothPos, ref a.clothVel, mid, 12f, stepDt);
            a.clothLag = body * Vector3.ClampMagnitude((a.clothPos - mid) * 0.12f, 0.012f);
            if (a.sleeve != null)
            {
                if (a.mpb == null) a.mpb = new MaterialPropertyBlock();
                a.sleeve.GetPropertyBlock(a.mpb);
                a.mpb.SetVector(ClothLagId, a.clothLag);
                a.sleeve.SetPropertyBlock(a.mpb);
            }

            // Xương cổ tay ảo: theo hướng cẳng tay, cuộn theo mu bàn tay; giới hạn gập cổ tay 70°
            if (rig != null && rig.ForearmBone != null)
            {
                Vector3 f = foreDir;
                float dev = Vector3.Angle(f, handFwd);
                Vector3 up = Vector3.ProjectOnPlane(handUp, f);
                if (up.sqrMagnitude < 1e-8f) up = innerF;
                rig.ForearmBone.rotation = Quaternion.LookRotation(f, up);
            }
        }

        static void SyncFingerTelemetryFromRig(Arm a, HandRig rig)
        {
            if (a == null || rig == null) return;
            a.fingers[0] = rig.WristAttach != null ? rig.WristAttach.position : rig.transform.position;
            rig.GetThumbJoints(out var tCmc, out var tMcp, out var tIp, out var tTip);
            a.fingers[1] = tCmc != null ? tCmc.position : a.fingers[0];
            a.fingers[2] = tMcp != null ? tMcp.position : a.fingers[1];
            a.fingers[3] = tIp != null ? tIp.position : a.fingers[2];
            a.fingers[4] = tTip != null ? tTip.position : a.fingers[3];
            for (int f = 0; f < 4; f++)
            {
                if (rig.GetFingerJoints(f, out var mcp, out var pip, out var dip, out var tip))
                {
                    int b = 5 + f * 4;
                    a.fingers[b] = mcp.position;
                    a.fingers[b + 1] = pip.position;
                    a.fingers[b + 2] = dip.position;
                    a.fingers[b + 3] = tip.position;
                }
            }
        }

        void Drive(ArmTracking.Side right)
        {
            bool want = right != null && hand != null;
            if (want)
            {
                hand.OverrideAimPoint = RightPalm + (RightPalm - cameraTransform.position).normalized * 0.05f;
                hand.OverrideHold = right.pinch;
            }
            if (want == Driving) return;
            Driving = want;
            if (!want && hand != null)
            {
                hand.OverrideAimPoint = null;
                hand.OverrideHold = false;
                if (hand.hand != null)
                {
                    hand.hand.SkipIdleUpdate = false;
                    hand.hand.ClearLandmarkControl();
                }
            }
            SetHandRenderers(true);
            MediaPipeHandSource.SuppressController = want;
        }

        void SetHandRenderers(bool on)
        {
            if (hand != null && hand.hand != null)
                hand.hand.SetRenderEnabled(on);
        }

        Arm Build(int side)
        {
            if (sleeveMat == null)
                sleeveMat = TayNguoiLuoi.NewMaterial(2, new Color(0.95f, 0.96f, 0.97f));
            var a = new Arm { root = new GameObject(side == 0 ? "Cánh tay phải" : "Cánh tay trái") };
            string[] names = { "Xương cánh tay", "Xương khuỷu", "Xương xoắn cẳng tay 1", "Xương xoắn cẳng tay 2", "Xương cổ tay áo" };
            for (int i = 0; i < TayNguoiLuoi.SleeveBoneCount; i++)
            {
                var t = new GameObject(names[i]).transform;
                t.SetParent(a.root.transform, false);
                a.bones[i] = t;
            }
            var sleeveGo = new GameObject("Ống tay áo blouse");
            sleeveGo.transform.SetParent(a.root.transform, false);
            a.sleeve = sleeveGo.AddComponent<SkinnedMeshRenderer>();
            a.sleeve.sharedMaterial = sleeveMat;
            a.sleeve.updateWhenOffscreen = false;
            a.sleeve.bones = a.bones;
            a.sleeve.rootBone = a.bones[TayNguoiLuoi.SleeveBoneUpper];
            a.sleeve.localBounds = new Bounds(new Vector3(0f, 0f, 0.28f), new Vector3(0.5f, 0.5f, 0.85f));
            ApplySleeveLod(a, side);

            // Cúc cổ tay áo: mặt ngoài phía dưới–sau, gắn theo xương cổ tay áo
            var btn = new GameObject("Cúc cổ tay áo");
            btn.transform.SetParent(a.bones[TayNguoiLuoi.SleeveBoneCuff], false);
            float ang = (side == 0 ? -35f : -145f) * Mathf.Deg2Rad;
            Vector3 radial = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang) * 0.93f, 0f);
            btn.transform.localPosition = radial * 0.0428f + new Vector3(0f, 0f, -0.020f);
            btn.transform.localRotation = Quaternion.LookRotation(radial.normalized, Vector3.forward);
            btn.AddComponent<MeshFilter>().sharedMesh = TayNguoiLuoi.ButtonMesh();
            var bmr = btn.AddComponent<MeshRenderer>();
            if (buttonMat == null)
            {
                buttonMat = TayNguoiLuoi.NewMaterial(1, new Color(0.93f, 0.93f, 0.90f));
                buttonMat.SetFloat("_Inflate", 0f);
                buttonMat.SetFloat("_Translucency", 0.15f);
                buttonMat.SetFloat("_Smoothness", 0.7f);
            }
            bmr.sharedMaterial = buttonMat;
            bmr.shadowCastingMode = ShadowCastingMode.Off;
            a.button = btn.transform;

            if (side == 1)
            {
                var leftGo = new GameObject("Bàn tay trái");
                leftGo.transform.SetParent(a.root.transform, false);
                var lr = leftGo.AddComponent<HandRig>();
                lr.side = 1;
                lr.ownSleeve = false;
                lr.EnsureBuilt();
                lr.SetSleeveVisible(false);
                lr.DrivenByArm = true;
                a.leftRestHand = lr;
            }
            return a;
        }

        void ApplySleeveLod(Arm a, int side)
        {
            int lod = LodFromProfile();
            if (lod == a.lod || a.sleeve == null) return;
            a.lod = lod;
            var mesh = TayNguoiLuoi.GetSleeve(UpperLen, ForeLen, side, lod);
            if (mesh.bindposes == null || mesh.bindposes.Length != TayNguoiLuoi.SleeveBoneCount)
                mesh.bindposes = TayNguoiLuoi.SleeveBindposes(UpperLen, ForeLen);
            a.sleeve.sharedMesh = mesh;
            a.sleeve.quality = lod >= 3 ? SkinQuality.Bone2 : SkinQuality.Bone4;
            a.sleeve.shadowCastingMode = lod <= 1 ? ShadowCastingMode.On : ShadowCastingMode.Off;
        }
    }
}
