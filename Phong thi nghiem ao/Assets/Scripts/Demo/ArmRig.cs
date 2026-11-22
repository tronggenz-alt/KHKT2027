using Khkt.Input;
using UnityEngine;

namespace Khkt.Demo
{
    /// <summary>
    /// Cánh tay ảo theo MediaPipe: bắp tay, cẳng tay, bàn tay 21 khớp, gắn từ vai ảo dưới camera.
    ///  • Hình: mesh liền dựng lại mỗi khung (HandMesh.cs): tay áo blouse trắng tới giữa cẳng tay, cẳng tay dẹt dần,
    ///    găng nitrile có lòng bàn tay, cổ găng, ngón là ống thuôn.
    ///  • Hướng bắp tay, cẳng tay lấy từ Pose; chiều dài xương cố định nên số đo rung không làm tay co giãn.
    ///  • Không thấy khuỷu: IK hai xương tới cổ tay (Pose hoặc vị trí bàn tay trên ảnh), khuỷu chúc xuống, ra ngoài.
    ///  • Ngón tay: hướng từng đốt theo 21 điểm 3D, chiều dài đốt theo thiết kế (HandAnatomy), cổ tay có giới hạn gập.
    ///  • Tay phải điều khiển bàn tay của HandController: trỏ qua lòng bàn tay, chụm ngón là cầm.
    /// </summary>
    public class ArmRig : MonoBehaviour
    {
        static readonly int[] Parent = { -1, 0, 1, 2, 3, 0, 5, 6, 7, 0, 9, 10, 11, 0, 13, 14, 15, 0, 17, 18, 19 };
        const float UpperLen = 0.28f, ForeLen = 0.25f, Reach = 0.46f;

        /// <summary>Bộ lọc 1€ cho véc-tơ: tần số cắt tăng theo tốc độ, nên đứng yên mượt mà chạy nhanh vẫn ít trễ.</summary>
        class Euro
        {
            Vector3 x, dx;
            bool ready;
            public void Reset() => ready = false;

            Vector3 y;

            /// <summary>Lọc 1€ rồi qua vùng chết mềm: rung nhỏ hơn dead (m) bị bỏ, vượt ngưỡng chỉ tính phần vượt nên không giật.</summary>
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

        // m và m/s trong trục camera; chỉnh ở Inspector nếu còn rung (giảm minCutoff) hoặc trễ (tăng beta)
        public float ArmMinCutoff = 1.0f, ArmBeta = 2.5f, FingerMinCutoff = 1.2f, FingerBeta = 25f;

        class Arm
        {
            public readonly Euro fElbow = new Euro(), fWrist = new Euro();
            public readonly Euro[] fBone = NewEuros();
            static Euro[] NewEuros() { var e = new Euro[21]; for (int i = 0; i < 21; i++) e[i] = new Euro(); return e; }
            public bool tracked, handVisible;
            public void ResetFilters() { fElbow.Reset(); fWrist.Reset(); foreach (var e in fBone) e.Reset(); }
            public GameObject root;
            public TubeMesh armMesh, handMesh;
            public HandSkin skin;
            public int sleeveTube, foreTube;
            public MeshRenderer handRenderer;
            public Vector3 elbow, wrist;
            /// <summary>Vai nghiêng người tới trước khi phải với tới vật ở xa quá tầm tay (m, trục thế giới).</summary>
            public Vector3 lean;
            public readonly Vector3[] fingers = new Vector3[21];
        }

        public Transform cameraTransform;
        public HandController hand;

        public bool Driving { get; private set; }
        /// <summary>Số bàn tay đang hiện (hai cánh tay ảo + găng tay của chuột), để kiểm thử: không bao giờ quá 2.</summary>
        public int VisibleHands
        {
            get
            {
                int n = 0;
                foreach (var a in arms) if (a != null && a.root.activeSelf && a.handVisible) n++;
                return n + (gloveShown && hand != null && hand.Held != null ? 1 : 0);
            }
        }
        /// <summary>Lòng bàn tay của tay đang điều khiển (ưu tiên tay phải).</summary>
        public Vector3 RightPalm { get; private set; }
        public Vector3 RightElbow => arms[0] != null ? arms[0].elbow : Vector3.zero;
        public Vector3 RightWrist => arms[0] != null ? arms[0].wrist : Vector3.zero;
        public Vector3 RightShoulder => Shoulder(0) + (arms[0] != null ? arms[0].lean : Vector3.zero);
        public Vector3 FingerJoint(int i) => arms[0] != null ? arms[0].fingers[i] : Vector3.zero;
        /// <summary>Khớp bàn tay của một bên (0 phải, 1 trái), cho kiểm thử.</summary>
        public Vector3 Joint(int side, int i) => arms[side] != null ? arms[side].fingers[i] : Vector3.zero;

        readonly Arm[] arms = new Arm[2];
        int drivingSide;
        Renderer[] handRigRenderers;
        Material skin, sleeve, glove;

        void Start()
        {
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            if (hand == null) hand = HandController.Active != null ? HandController.Active : FindAnyObjectByType<HandController>();
            if (hand != null && hand.hand != null) handRigRenderers = hand.hand.GetComponentsInChildren<Renderer>(true);
        }

        Vector3 Shoulder(int side)
        {
            if (cameraTransform == null) return Vector3.zero;
            // vai ảo: dưới, lệch sang hai bên camera, theo hướng nhìn ngang (không cúi theo đầu)
            var flat = Quaternion.Euler(0, cameraTransform.eulerAngles.y, 0);
            return cameraTransform.position + flat * new Vector3(side == 0 ? 0.19f : -0.19f, -0.24f, -0.04f);
        }

        void LateUpdate()
        {
            var at = ArmTracking.Instance;
            bool master = MediaPipeHandSource.Instance == null || MediaPipeHandSource.Instance.handTrackingEnabled;
            ArmTracking.Side driver = null;
            if (cameraTransform == null) return;
            // găng tay của chuột (HandController) đang ở đâu: chỉ có nghĩa khi đang cầm vật.
            // Cánh tay với tới cổ găng (không phải tâm găng) nên bàn tay ảo trùng đúng chỗ găng, vật vẫn nằm trong lòng bàn tay.
            bool holding = hand != null && hand.Held != null && hand.hand != null;
            Vector3 glove = holding ? hand.hand.WristWorld : Vector3.positiveInfinity;
            bool gloveReach = holding && (glove - Shoulder(0)).sqrMagnitude < 0.62f * 0.62f;
            bool rightAtGlove = false;
            bool seenR = master && at != null && at.Right.handSeen, seenL = master && at != null && at.Left.handSeen;
            for (int i = 0; i < 2; i++)
            {
                var s = at == null ? null : i == 0 ? at.Right : at.Left;
                bool show = i == 0 ? seenR : seenL;
                if (arms[i] == null) arms[i] = Build(i);
                // không đặt lại bộ lọc khi đổi nguồn (tay thật <-> tay nghỉ): tay trượt mềm sang chỗ mới thay vì nhảy
                arms[i].tracked = show;
                bool visible = true;
                if (show)
                {
                    // tay đang điều khiển mà đang cầm vật: bàn tay ảo ôm đúng vật (vật nằm theo tia trỏ)
                    bool drives = driver == null;
                    Pose(arms[i], s, i, drives && gloveReach ? glove : (Vector3?)null);
                    if (drives) { driver = s; drivingSide = i; rightAtGlove = gloveReach; }
                }
                else if (i == 0 && holding)
                {
                    // tay phải nghỉ mà đang cầm vật: với tới vật nếu tới được, bàn tay lấy đúng dáng găng đang cầm;
                    // không tới được thì ẩn hẳn (găng tay hiện thay)
                    if (gloveReach && driver == null) { Pose(arms[i], GloveHand(hand.hand), 0, glove); rightAtGlove = true; }
                    else visible = false;
                }
                else
                {
                    // tay nghỉ: thả lỏng ở mép dưới khung nhìn; tay kia đang làm việc thì hạ hẳn xuống, không chen vào giữa
                    bool otherBusy = i == 0 ? seenL : (seenR || rightAtGlove);
                    Pose(arms[i], RestHand(i, otherBusy), i);
                }
                if (arms[i].root.activeSelf != visible) arms[i].root.SetActive(visible);
            }
            Drive(driver);
            // găng tay chỉ hiện khi đang cầm mà không bàn tay ảo nào ôm vật: lúc nào cũng đúng hai bàn tay trên màn hình
            if (handRigRenderers != null)
            {
                bool want = holding && !rightAtGlove && driver == null;
                if (want != gloveShown)
                {
                    gloveShown = want;
                    foreach (var r in handRigRenderers) if (r != null) r.enabled = want;
                }
            }
        }

        bool gloveShown = true;

        // =====================================================================
        // Tư thế
        // =====================================================================

        readonly ArmTracking.Side[] restHands = new ArmTracking.Side[2];
        readonly ArmTracking.Side gloveSide = new ArmTracking.Side { handSeen = true };
        readonly Vector3[] gloveJoints = new Vector3[21];

        /// <summary>
        /// Tay nghỉ: thả lỏng ở mép dưới khung nhìn, lòng bàn tay úp, các ngón khum tự nhiên (ngón út khum nhiều hơn ngón trỏ),
        /// ngón cái hơi nghiêng lên. lowered: hạ xuống dưới khung nhìn khi tay kia đang làm việc.
        /// </summary>
        ArmTracking.Side RestHand(int side, bool lowered)
        {
            var s = restHands[side];
            if (s == null)
            {
                float sign = side == 0 ? 1f : -1f;   // tay phải lệch phải, ngón cái quay vào giữa
                s = restHands[side] = new ArmTracking.Side { handSeen = true };
                var local = new Vector3[21];
                HandAnatomy.Pose(local, 0.2f, 0.2f, 0.3f, sign);
                Vector3 fwd = new Vector3(-0.22f * sign, -0.3f, 1f).normalized;
                var rot = Quaternion.LookRotation(fwd, Vector3.up) * Quaternion.Euler(0, 0, -18f * sign);
                for (int i = 0; i < 21; i++) s.hand[i] = rot * local[i];
            }
            s.wristImage = lowered ? new Vector2(side == 0 ? 0.15f : 0.85f, 1.8f) : new Vector2(side == 0 ? 0.31f : 0.69f, 0.9f);
            return s;
        }

        /// <summary>Dáng bàn tay lấy từ găng tay đang cầm vật, đổi sang trục camera như số đo của MediaPipe.</summary>
        ArmTracking.Side GloveHand(HandRig rig)
        {
            rig.WorldJoints(gloveJoints);
            var inv = Quaternion.Inverse(cameraTransform.rotation);
            for (int i = 0; i < 21; i++) gloveSide.hand[i] = inv * (gloveJoints[i] - gloveJoints[0]);
            return gloveSide;
        }

        void Pose(Arm a, ArmTracking.Side s, int side, Vector3? wristTarget = null)
        {
            var cam = cameraTransform.rotation;
            Vector3 sh = Shoulder(side);
            // vật ở xa quá tầm tay: nghiêng vai tới trước (tối đa 14 cm) thay vì để bàn tay hụt khỏi vật
            Vector3 leanWant = Vector3.zero;
            if (wristTarget.HasValue)
            {
                Vector3 to = wristTarget.Value - sh;
                float over = to.magnitude - (UpperLen + ForeLen - 0.01f);
                if (over > 0) leanWant = to.normalized * Mathf.Min(over, 0.14f);
            }
            a.lean = Vector3.Lerp(a.lean, leanWant, 1f - Mathf.Exp(-10f * Mathf.Max(Time.deltaTime, 1e-3f)));
            sh += a.lean;
            Vector3 elbow, wrist;
            if (s.armSeen && s.elbowConf >= 0.5f && (s.elbow - s.shoulder).sqrMagnitude > 1e-4f)
            {
                elbow = sh + cam * (s.elbow - s.shoulder).normalized * UpperLen;
                Vector3 fore = s.wrist - s.elbow;
                wrist = elbow + (fore.sqrMagnitude > 1e-4f ? cam * fore.normalized : (elbow - sh).normalized) * ForeLen;
            }
            else
            {
                // không thấy khuỷu: đích cổ tay từ Pose, không có thì từ vị trí bàn tay trên ảnh
                Vector3 target;
                if (wristTarget.HasValue) target = wristTarget.Value;
                else if (s.armSeen && s.wristConf >= 0.5f) target = sh + cam * (s.wrist - s.shoulder);
                else
                {
                    var img = s.wristImage;
                    var dir = new Vector3((0.5f - img.x) * 1.4f, (0.5f - img.y) * 1.0f, 1f).normalized;
                    target = cameraTransform.position + cam * dir * Reach;
                }
                SolveIk(sh, target, side, out elbow, out wrist);
            }
            // Lọc 1€ trong trục camera (gốc ở vai): đứng yên thì mượt hẳn, cử động nhanh thì bám sát;
            // sau khi lọc đặt lại chiều dài xương cho đúng
            var inv = Quaternion.Inverse(cam);
            float dt = Mathf.Max(Time.deltaTime, 1e-3f);
            Vector3 eL = a.fElbow.F(inv * (elbow - sh), dt, ArmMinCutoff, ArmBeta, 0.004f);
            Vector3 wL = a.fWrist.F(inv * (wrist - sh), dt, ArmMinCutoff, ArmBeta, 0.004f);
            eL = (eL.sqrMagnitude > 1e-6f ? eL.normalized : Vector3.forward) * UpperLen;
            Vector3 fw = wL - eL;
            wL = eL + (fw.sqrMagnitude > 1e-6f ? fw.normalized : eL.normalized) * ForeLen;
            elbow = sh + cam * eL;
            wrist = sh + cam * wL;
            a.elbow = elbow;
            a.wrist = wrist;
            Vector3 forearm = (wrist - elbow).normalized;

            a.handVisible = s.handSeen;
            if (a.handRenderer.enabled != s.handSeen) a.handRenderer.enabled = s.handSeen;
            Vector3 across = cameraTransform.right;
            if (s.handSeen)
            {
                // Hướng từng đốt lấy từ số đo (lọc trong trục camera), chiều dài đốt theo thiết kế:
                // số đo rung hay tay ra xa camera cũng không làm ngón co giãn
                a.fingers[0] = wrist;
                for (int i = 1; i < 21; i++)
                {
                    Vector3 d = a.fBone[i].F(s.hand[i] - s.hand[Parent[i]], dt, FingerMinCutoff, FingerBeta, 0.0015f);
                    a.fingers[i] = a.fingers[Parent[i]] + (d.sqrMagnitude > 1e-8f ? cam * d.normalized : Vector3.zero) * HandAnatomy.BoneLen[i];
                }
                ClampWrist(a.fingers, forearm);
                a.skin.Write(a.fingers, forearm);
                a.handMesh.Apply();
                Vector3 ac = a.fingers[5] - a.fingers[17];
                if (ac.sqrMagnitude > 1e-8f) across = ac;
                if (side == drivingSide)
                {
                    Vector3 palm = Vector3.zero;
                    foreach (int i in PalmIdx) palm += a.fingers[i];
                    RightPalm = palm / 5f;
                }
            }
            WriteArm(a, sh, elbow, wrist, forearm, across);
        }

        static readonly int[] PalmIdx = { 0, 5, 9, 13, 17 };

        /// <summary>
        /// Giới hạn cổ tay so với cẳng tay: gập / ngửa tối đa 80°, nghiêng sang hai bên tối đa 32°.
        /// Số đo vượt giới hạn (thường là nhiễu) thì xoay cả bàn tay quanh cổ tay về mép giới hạn.
        /// </summary>
        static void ClampWrist(Vector3[] f, Vector3 forearm)
        {
            Vector3 p0 = f[0];
            Vector3 dir = f[9] - p0;
            Vector3 across = Vector3.ProjectOnPlane(f[5] - f[17], forearm);
            if (dir.sqrMagnitude < 1e-8f || across.sqrMagnitude < 1e-8f) return;
            dir.Normalize();
            across.Normalize();
            Vector3 n = Vector3.Cross(forearm, across);
            float x = Vector3.Dot(dir, forearm), y = Vector3.Dot(dir, n), z = Vector3.Dot(dir, across);
            float dev = Mathf.Asin(Mathf.Clamp(z, -1f, 1f)), flex = Mathf.Atan2(y, x);
            float devC = Mathf.Clamp(dev, -32f * Mathf.Deg2Rad, 32f * Mathf.Deg2Rad);
            float flexC = Mathf.Clamp(flex, -80f * Mathf.Deg2Rad, 80f * Mathf.Deg2Rad);
            if (Mathf.Abs(devC - dev) < 1e-4f && Mathf.Abs(flexC - flex) < 1e-4f) return;
            Vector3 want = Mathf.Cos(devC) * (Mathf.Cos(flexC) * forearm + Mathf.Sin(flexC) * n) + Mathf.Sin(devC) * across;
            var q = Quaternion.FromToRotation(dir, want);
            for (int i = 1; i < 21; i++) f[i] = p0 + q * (f[i] - p0);
        }

        readonly Vector3[] armP = new Vector3[4], armH = new Vector3[4];
        readonly Vector4[] armR = new Vector4[4];

        /// <summary>Tay áo blouse trắng từ vai qua khuỷu tới giữa cẳng tay, cẳng tay dẹt dần về cổ tay và xoay theo bàn tay.</summary>
        void WriteArm(Arm a, Vector3 sh, Vector3 elbow, Vector3 wrist, Vector3 forearm, Vector3 across)
        {
            Vector3 upper = (elbow - sh).normalized;
            armP[0] = sh - upper * 0.06f;
            armP[1] = sh;
            armP[2] = elbow;
            armP[3] = elbow + forearm * 0.13f;
            armR[0] = new Vector4(0.056f, 0.056f, 2f, 0);
            armR[1] = new Vector4(0.058f, 0.056f, 2f, 0);
            armR[2] = new Vector4(0.052f, 0.050f, 2f, 0);
            armR[3] = new Vector4(0.049f, 0.046f, 2f, 0);
            a.armMesh.Sweep(a.sleeveTube, armP, armR, null, Vector3.ProjectOnPlane(cameraTransform.up, upper));

            Vector3 ac = Vector3.ProjectOnPlane(across, forearm);
            if (ac.sqrMagnitude < 1e-8f) ac = Vector3.ProjectOnPlane(cameraTransform.right, forearm);
            armP[0] = elbow + forearm * 0.07f;
            armP[1] = elbow + forearm * 0.16f;
            // da dừng bên trong cổ găng, nhỏ hơn cổ găng vài mm để hai mặt không xuyên răng cưa qua nhau
            armP[2] = wrist - forearm * 0.03f;
            armR[0] = new Vector4(0.037f, 0.032f, 2f, 0);
            armR[1] = new Vector4(0.033f, 0.026f, 2f, 0);
            armR[2] = new Vector4(0.0285f, 0.0200f, 2f, 0);
            armH[0] = armH[1] = armH[2] = ac;
            a.armMesh.Sweep(a.foreTube, armP, armR, armH, Vector3.zero);
            a.armMesh.Apply();
        }

        /// <summary>IK hai xương: khuỷu nằm trên mặt phẳng chứa vai, đích và hướng chúc xuống, ra ngoài.</summary>
        void SolveIk(Vector3 sh, Vector3 target, int side, out Vector3 elbow, out Vector3 wrist)
        {
            Vector3 to = target - sh;
            float d = Mathf.Clamp(to.magnitude, 0.05f, UpperLen + ForeLen - 0.001f);
            Vector3 dir = to.sqrMagnitude > 1e-6f ? to.normalized : cameraTransform.forward;
            wrist = sh + dir * d;
            float a = (UpperLen * UpperLen - ForeLen * ForeLen + d * d) / (2 * d);
            float h = Mathf.Sqrt(Mathf.Max(0, UpperLen * UpperLen - a * a));
            Vector3 pole = Vector3.down + cameraTransform.right * (side == 0 ? 0.6f : -0.6f);
            Vector3 bend = Vector3.ProjectOnPlane(pole, dir);
            if (bend.sqrMagnitude < 1e-6f) bend = Vector3.ProjectOnPlane(Vector3.down, dir);
            elbow = sh + dir * a + bend.normalized * h;
        }

        // =====================================================================
        // Điều khiển bàn tay của HandController
        // =====================================================================

        void Drive(ArmTracking.Side right)
        {
            bool want = right != null && hand != null;
            if (want && !GestureControl.Owning)
            {
                // tia từ camera đi qua lòng bàn tay: trỏ tay vào vật nào thì chọn vật đó; chụm ngón là cầm
                hand.OverrideAimPoint = RightPalm + (RightPalm - cameraTransform.position).normalized * 0.05f;
                hand.OverrideHold = right.pinch;
            }
            if (want == Driving) return;
            Driving = want;
            if (!want && hand != null && !GestureControl.Owning)
            {
                hand.OverrideAimPoint = null;
                hand.OverrideHold = false;
            }
            // tắt cần điều khiển ảo từ cử chỉ để không điều khiển hai lần (găng tay do LateUpdate quyết định)
            MediaPipeHandSource.SuppressController = want;
        }

        // =====================================================================
        // Dựng hình
        // =====================================================================

        Arm Build(int side)
        {
            if (skin == null)
            {
                skin = Lit(new Color(0.87f, 0.69f, 0.58f), 0.35f);
                sleeve = Lit(new Color(0.94f, 0.95f, 0.97f), 0.2f);  // tay áo blouse trắng
                glove = Lit(new Color(0.27f, 0.56f, 0.93f), 0.55f);  // găng nitrile xanh, cùng màu găng tay của chuột
            }
            var a = new Arm { root = new GameObject(side == 0 ? "Cánh tay phải (MediaPipe)" : "Cánh tay trái (MediaPipe)") };
            a.armMesh = new TubeMesh(2, "Tay áo, cẳng tay");
            a.sleeveTube = a.armMesh.AddTube(0, 18, 4, 5, 3, 1f, 2, 0.2f);
            a.foreTube = a.armMesh.AddTube(1, 14, 3, 5, 2, 0.6f, 2, 0.6f);
            a.armMesh.Finish();
            a.handMesh = new TubeMesh(1, "Găng tay");
            a.skin = new HandSkin(a.handMesh, 0);
            a.handMesh.Finish();
            MakeRenderer(a.root.transform, "Tay áo, cẳng tay", a.armMesh.Mesh, new[] { sleeve, skin });
            a.handRenderer = MakeRenderer(a.root.transform, "Bàn tay (găng)", a.handMesh.Mesh, new[] { glove });
            return a;
        }

        internal static Material Lit(Color c, float smooth)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { hideFlags = HideFlags.DontSave };
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            return m;
        }

        /// <summary>Renderer cho mesh dựng lúc chạy: không collider (tia chọn vật đi xuyên qua tay), không đổ bóng.</summary>
        internal static MeshRenderer MakeRenderer(Transform parent, string name, Mesh mesh, Material[] mats)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }
    }
}
