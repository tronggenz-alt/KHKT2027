using Khkt.Avatar;
using Khkt.Input;
using UnityEngine;

namespace Khkt.Demo
{
    /// <summary>
    /// Cánh tay người chơi ở góc nhìn thứ nhất: vai, bắp tay (áo blouse), khuỷu, cẳng tay (áo blouse),
    /// nối liền mạch vào cổ tay bàn tay giải phẫu HandRig (phải và trái).
    ///  • Không dùng tay giả trùng lặp: khi điều khiển bằng chuột/tay cầm, tay phải nối thẳng vào HandRig của HandController;
    ///    tay trái ở tư thế nghỉ tự nhiên. Khi bật nhận diện webcam (InAppHandTracker / ArmTracking), ánh xạ 21 điểm vào HandRig.
    /// </summary>
    public class ArmRig : MonoBehaviour
    {
        static readonly int[] Parent = { -1, 0, 1, 2, 3, 0, 5, 6, 7, 0, 9, 10, 11, 0, 13, 14, 15, 0, 17, 18, 19 };
        const float UpperLen = 0.28f, ForeLen = 0.26f, Reach = 0.45f;

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

        class Arm
        {
            public readonly Euro fElbow = new Euro(), fWrist = new Euro();
            public readonly Euro[] fBone = NewEuros();
            static Euro[] NewEuros() { var e = new Euro[21]; for (int i = 0; i < 21; i++) e[i] = new Euro(); return e; }
            public bool tracked;
            public void ResetFilters() { fElbow.Reset(); fWrist.Reset(); foreach (var e in fBone) e.Reset(); }
            public GameObject root;
            public Transform upper, fore, elbowBall, shoulderBall;
            public HandRig leftRestHand;
            public readonly float[] boneLen = new float[21];
            public int lenSamples;
            public Vector3 elbow, wrist;
            public readonly Vector3[] fingers = new Vector3[21];
        }

        public Transform cameraTransform;
        public HandController hand;

        public bool Driving { get; private set; }
        public Vector3 RightPalm { get; private set; }
        public Vector3 RightElbow => arms[0] != null ? arms[0].elbow : Vector3.zero;
        public Vector3 RightWrist => arms[0] != null ? arms[0].wrist : Vector3.zero;
        public Vector3 RightShoulder => Shoulder(0);
        public Vector3 FingerJoint(int i) => arms[0] != null && i >= 0 && i < 21 ? arms[0].fingers[i] : Vector3.zero;

        readonly Arm[] arms = new Arm[2];
        int drivingSide;
        Material sleeve;

        void Start()
        {
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            if (hand == null) hand = HandController.Active != null ? HandController.Active : FindAnyObjectByType<HandController>();
            if (hand != null && hand.hand != null)
            {
                hand.hand.EnsureBuilt();
                SetHandRenderers(true);
            }
        }

        Vector3 Shoulder(int side)
        {
            if (cameraTransform == null) return Vector3.zero;
            var flat = Quaternion.Euler(0f, cameraTransform.eulerAngles.y, 0f);
            return cameraTransform.position + flat * new Vector3(side == 0 ? 0.19f : -0.19f, -0.25f, -0.05f);
        }

        void LateUpdate()
        {
            if (cameraTransform == null) return;
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
                    PoseTracked(arms[i], s, i);
                    if (s.handSeen && driver == null) { driver = s; drivingSide = i; }
                }
                else
                {
                    PoseMouseOrRest(arms[i], i);
                }
            }

            Drive(driver);
        }

        void PoseMouseOrRest(Arm a, int side)
        {
            if (side == 0 && hand != null && hand.hand != null)
            {
                hand.hand.EnsureBuilt();
                hand.hand.SkipIdleUpdate = false;
                SetHandRenderers(true);

                Vector3 wristTarget = hand.hand.WristAttach != null ? hand.hand.WristAttach.position : hand.hand.transform.position;
                Quaternion wristRot = hand.hand.transform.rotation;
                PlaceConnectedArm(a, side, wristTarget, wristRot);
                SyncFingerTelemetryFromRig(a, hand.hand);
                return;
            }

            // Tay trái để nghỉ gọn ở mép dưới bên trái tầm nhìn, lòng bàn tay hơi hướng vào bàn thí nghiệm
            if (a.leftRestHand != null)
            {
                a.leftRestHand.EnsureBuilt();
                a.leftRestHand.SkipIdleUpdate = true;
                a.leftRestHand.SetTargetPose(HandPoseId.Nghi);
                a.leftRestHand.StepPoseAndContact(Time.unscaledDeltaTime);

                Quaternion camFlat = Quaternion.Euler(0f, cameraTransform.eulerAngles.y, 0f);
                Vector3 restPos = cameraTransform.position + camFlat * new Vector3(-0.22f, -0.31f, 0.36f);
                Quaternion restRot = camFlat * Quaternion.Euler(14f, 18f, -12f);
                a.leftRestHand.transform.SetPositionAndRotation(restPos, restRot);
                Vector3 wristTarget = a.leftRestHand.WristAttach != null ? a.leftRestHand.WristAttach.position : restPos;
                PlaceConnectedArm(a, side, wristTarget, restRot);
            }
        }

        void PoseTracked(Arm a, ArmTracking.Side s, int side)
        {
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
                SolveIk(sh, target, side, out elbow, out wrist);
            }

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

            PlaceSegment(a.shoulderBall, sh, sh, 0.088f, false);
            PlaceSegment(a.upper, sh, elbow, 0.082f, true);
            PlaceSegment(a.elbowBall, elbow, elbow, 0.074f, false);
            Vector3 foreDir = (wrist - elbow).sqrMagnitude > 1e-6f ? (wrist - elbow).normalized : cam * Vector3.forward;
            PlaceSegment(a.fore, elbow, wrist - foreDir * 0.030f, 0.058f, true);

            HandRig rig = side == 0 && hand != null ? hand.hand : a.leftRestHand;
            if (rig == null) return;
            if (!s.handSeen) return;

            for (int i = 1; i < 21; i++)
            {
                float len = (s.hand[i] - s.hand[Parent[i]]).magnitude;
                if (a.lenSamples < 30) a.boneLen[i] = (a.boneLen[i] * a.lenSamples + len) / (a.lenSamples + 1);
            }
            if (a.lenSamples < 30) a.lenSamples++;

            var filteredLocal = new Vector3[21];
            filteredLocal[0] = Vector3.zero;
            a.fingers[0] = wrist;
            for (int i = 1; i < 21; i++)
            {
                Vector3 d = a.fBone[i].F(s.hand[i] - s.hand[Parent[i]], dt, FingerMinCutoff, FingerBeta, 0.0015f);
                Vector3 dir = d.sqrMagnitude > 1e-8f ? d.normalized : Vector3.forward;
                float len = a.boneLen[i] > 1e-4f ? a.boneLen[i] : 0.025f;
                filteredLocal[i] = filteredLocal[Parent[i]] + dir * len;
                a.fingers[i] = wrist + cam * filteredLocal[i];
            }

            InAppHandTracker.MapLandmarksToHandRig(filteredLocal, rig, cam, wrist);

            if (side == drivingSide)
                RightPalm = (a.fingers[0] + a.fingers[5] + a.fingers[9] + a.fingers[13] + a.fingers[17]) * 0.2f;
        }

        void PlaceConnectedArm(Arm a, int side, Vector3 wristTarget, Quaternion wristRot)
        {
            Vector3 sh = Shoulder(side);
            // Nghiêng nhẹ vai theo tầm với để cẳng tay áo luôn gắn kín vào cổ tay bàn tay, không bị đứt đoạn
            Vector3 toTarget = wristTarget - sh;
            float maxReach = UpperLen + ForeLen - 0.01f;
            if (toTarget.magnitude > maxReach)
                sh = wristTarget - toTarget.normalized * maxReach;

            SolveIk(sh, wristTarget, side, out Vector3 elbow, out Vector3 wrist);
            a.elbow = elbow;
            a.wrist = wristTarget;

            PlaceSegment(a.shoulderBall, sh, sh, 0.088f, false);
            PlaceSegment(a.upper, sh, elbow, 0.082f, true);
            PlaceSegment(a.elbowBall, elbow, elbow, 0.074f, false);
            Vector3 foreDir = (wristTarget - elbow).sqrMagnitude > 1e-6f ? (wristTarget - elbow).normalized : wristRot * Vector3.forward;
            PlaceSegment(a.fore, elbow, wristTarget - foreDir * 0.030f, 0.058f, true);
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

        static void PlaceSegment(Transform t, Vector3 from, Vector3 to, float width, bool bone)
        {
            if (t == null) return;
            if (!bone)
            {
                t.position = from;
                t.localScale = Vector3.one * width;
                return;
            }
            Vector3 d = to - from;
            t.position = (from + to) * 0.5f;
            t.rotation = d.sqrMagnitude > 1e-8f ? Quaternion.FromToRotation(Vector3.up, d) : Quaternion.identity;
            t.localScale = new Vector3(width, Mathf.Max(width, d.magnitude) * 0.5f, width);
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
            if (sleeve == null)
                sleeve = Lit(new Color(0.94f, 0.95f, 0.97f), 0.14f);
            var a = new Arm { root = new GameObject(side == 0 ? "Cánh tay phải" : "Cánh tay trái") };
            a.shoulderBall = Part(PrimitiveType.Sphere, a.root.transform, "Vai áo", sleeve);
            a.upper = Part(PrimitiveType.Capsule, a.root.transform, "Bắp tay áo", sleeve);
            a.elbowBall = Part(PrimitiveType.Sphere, a.root.transform, "Khuỷu áo", sleeve);
            a.fore = Part(PrimitiveType.Capsule, a.root.transform, "Cẳng tay áo", sleeve);

            if (side == 1)
            {
                var leftGo = new GameObject("Bàn tay trái");
                leftGo.transform.SetParent(a.root.transform, false);
                var lr = leftGo.AddComponent<HandRig>();
                lr.side = 1;
                lr.EnsureBuilt();
                a.leftRestHand = lr;
            }
            return a;
        }

        static Material Lit(Color c, float smoothness)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { hideFlags = HideFlags.DontSave };
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smoothness);
            return m;
        }

        static Transform Part(PrimitiveType type, Transform parent, string name, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }
    }
}
