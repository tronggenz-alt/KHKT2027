using Khkt.Input;
using UnityEngine;

namespace Khkt.Demo
{
    /// <summary>
    /// Cánh tay ảo theo MediaPipe: bắp tay, cẳng tay, bàn tay 21 khớp, gắn từ vai ảo dưới camera.
    ///  • Hướng bắp tay, cẳng tay lấy từ Pose; chiều dài xương cố định nên số đo rung không làm tay co giãn.
    ///  • Không thấy khuỷu: IK hai xương tới cổ tay (Pose hoặc vị trí bàn tay trên ảnh), khuỷu chúc xuống, ra ngoài.
    ///  • Ngón tay: hướng từng đốt theo 21 điểm 3D, chiều dài đốt lấy trung vị lúc mới thấy tay rồi giữ nguyên.
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
            public bool tracked;
            public void ResetFilters() { fElbow.Reset(); fWrist.Reset(); foreach (var e in fBone) e.Reset(); }
            public GameObject root;
            public Transform upper, fore, elbowBall, shoulderBall;
            public Transform[] joints = new Transform[21], bones = new Transform[21];
            public readonly float[] boneLen = new float[21];
            public int lenSamples;
            public Vector3 elbow, wrist;
            public readonly Vector3[] fingers = new Vector3[21];
        }

        public Transform cameraTransform;
        public HandController hand;

        public bool Driving { get; private set; }
        /// <summary>Lòng bàn tay của tay đang điều khiển (ưu tiên tay phải).</summary>
        public Vector3 RightPalm { get; private set; }
        public Vector3 RightElbow => arms[0] != null ? arms[0].elbow : Vector3.zero;
        public Vector3 RightWrist => arms[0] != null ? arms[0].wrist : Vector3.zero;
        public Vector3 RightShoulder => Shoulder(0);
        public Vector3 FingerJoint(int i) => arms[0] != null ? arms[0].fingers[i] : Vector3.zero;

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
            for (int i = 0; i < 2; i++)
            {
                var s = at == null ? null : i == 0 ? at.Right : at.Left;
                bool show = master && s != null && (s.handSeen || s.armSeen) && cameraTransform != null;
                if (cameraTransform == null) continue;
                if (arms[i] == null) arms[i] = Build(i);
                if (!arms[i].root.activeSelf) arms[i].root.SetActive(true);
                if (show != arms[i].tracked) { arms[i].tracked = show; arms[i].ResetFilters(); }  // đổi nguồn: không trôi từ chỗ cũ
                // luôn có hai tay: không nhận diện thì tay phải nối tới găng tay của chuột, tay trái để nghỉ
                if (show) Pose(arms[i], s, i);
                else
                {
                    // găng tay của chuột ở gần thì tay phải với tới đó; ở xa (trỏ ra bàn khác) thì hai tay để nghỉ
                    Vector3 glove = hand != null && hand.hand != null && hand.hand.gameObject.activeInHierarchy ? hand.hand.transform.position : Vector3.positiveInfinity;
                    if (i == 0 && (glove - Shoulder(0)).sqrMagnitude < 0.5f * 0.5f) Pose(arms[i], restRight, 0, glove);
                    else Pose(arms[i], RestHand(i), i);
                }
                // tay nào đang thấy cũng điều khiển được, ưu tiên tay phải
                if (show && s.handSeen && driver == null) { driver = s; drivingSide = i; }
            }
            Drive(driver);
            // không theo tay thật: găng tay của chuột chỉ hiện khi đang cầm vật, để màn hình không có ba bàn tay
            if (!Driving && handRigRenderers != null)
            {
                bool want = hand != null && hand.Held != null;
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

        readonly ArmTracking.Side restRight = new ArmTracking.Side();
        readonly ArmTracking.Side[] restHands = new ArmTracking.Side[2];

        /// <summary>Tay để nghỉ ở mép dưới khung nhìn, lòng bàn tay úp, các ngón hơi khum.</summary>
        ArmTracking.Side RestHand(int side)
        {
            if (restHands[side] != null) return restHands[side];
            float sign = side == 0 ? 1f : -1f;   // tay phải lệch phải, tay trái lệch trái
            var s = new ArmTracking.Side { handSeen = true, wristImage = new Vector2(side == 0 ? 0.3f : 0.7f, 0.86f) };
            Vector3 fwd = new Vector3(-0.25f * sign, -0.3f, 1f).normalized;
            Vector3 across = Vector3.Cross(Vector3.up, fwd).normalized;
            s.hand[0] = Vector3.zero;
            for (int f = 0; f < 5; f++)
            {
                // ngón cái chếch vào trong, bốn ngón xếp hàng ngang, đốt sau cụp xuống dần
                Vector3 baseP = f == 0 ? (fwd * 0.03f - across * sign * 0.035f) : (fwd * 0.085f + across * sign * (f - 2.5f) * 0.019f);
                Vector3 dir = f == 0 ? (fwd - across * sign * 0.6f).normalized : fwd;
                s.hand[1 + f * 4] = baseP;
                for (int j = 1; j < 4; j++)
                {
                    dir = (dir + Vector3.down * 0.35f).normalized;
                    s.hand[1 + f * 4 + j] = s.hand[f * 4 + j] + dir * (f == 0 ? 0.03f : 0.026f - j * 0.003f);
                }
            }
            return restHands[side] = s;
        }

        void Pose(Arm a, ArmTracking.Side s, int side, Vector3? wristTarget = null)
        {
            var cam = cameraTransform.rotation;
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
            Place(a.shoulderBall, sh, sh, 0.06f, false);
            Place(a.upper, sh, elbow, 0.055f, true);
            Place(a.elbowBall, elbow, elbow, 0.05f, false);
            Place(a.fore, elbow, wrist, 0.045f, true);

            if (!s.handSeen)
            {
                for (int i = 0; i < 21; i++) a.joints[i].gameObject.SetActive(false);
                for (int i = 1; i < 21; i++) a.bones[i].gameObject.SetActive(false);
                return;
            }
            // Chiều dài đốt: trung bình 30 lần đo đầu rồi giữ nguyên, để số đo rung không làm ngón co giãn
            for (int i = 1; i < 21; i++)
            {
                float len = (s.hand[i] - s.hand[Parent[i]]).magnitude;
                if (a.lenSamples < 30) a.boneLen[i] = (a.boneLen[i] * a.lenSamples + len) / (a.lenSamples + 1);
            }
            if (a.lenSamples < 30) a.lenSamples++;

            // Lọc hướng từng đốt (trục camera) rồi dựng lại từ cổ tay với chiều dài đã chốt
            a.fingers[0] = wrist;
            for (int i = 1; i < 21; i++)
            {
                Vector3 d = a.fBone[i].F(s.hand[i] - s.hand[Parent[i]], dt, FingerMinCutoff, FingerBeta, 0.0015f);
                a.fingers[i] = a.fingers[Parent[i]] + (d.sqrMagnitude > 1e-8f ? cam * d.normalized : Vector3.zero) * a.boneLen[i];
            }
            for (int i = 0; i < 21; i++)
            {
                a.joints[i].gameObject.SetActive(true);
                Place(a.joints[i], a.fingers[i], a.fingers[i], i == 0 ? 0.03f : i % 4 == 0 ? 0.016f : 0.019f, false);
            }
            for (int i = 1; i < 21; i++)
            {
                a.bones[i].gameObject.SetActive(true);
                Place(a.bones[i], a.fingers[Parent[i]], a.fingers[i], Parent[i] == 0 ? 0.022f : 0.017f, true);
            }
            if (side == drivingSide)
            {
                Vector3 palm = Vector3.zero;
                foreach (int i in new[] { 0, 5, 9, 13, 17 }) palm += a.fingers[i];
                RightPalm = palm / 5f;
            }
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

        static void Place(Transform t, Vector3 from, Vector3 to, float width, bool bone)
        {
            if (!bone)
            {
                t.position = from;
                t.localScale = Vector3.one * width;
                return;
            }
            Vector3 d = to - from;
            t.position = (from + to) * 0.5f;
            t.rotation = d.sqrMagnitude > 1e-8f ? Quaternion.FromToRotation(Vector3.up, d) : Quaternion.identity;
            // capsule cao 2 đơn vị gồm cả hai đầu tròn: phần thân bằng chiều dài xương
            t.localScale = new Vector3(width, Mathf.Max(width, d.magnitude) * 0.5f, width);
        }

        // =====================================================================
        // Điều khiển bàn tay của HandController
        // =====================================================================

        void Drive(ArmTracking.Side right)
        {
            bool want = right != null && hand != null;
            if (want)
            {
                // tia từ camera đi qua lòng bàn tay: trỏ tay vào vật nào thì chọn vật đó; chụm ngón là cầm
                hand.OverrideAimPoint = RightPalm + (RightPalm - cameraTransform.position).normalized * 0.05f;
                hand.OverrideHold = right.pinch;
            }
            if (want == Driving) return;
            Driving = want;
            if (!want && hand != null)
            {
                hand.OverrideAimPoint = null;
                hand.OverrideHold = false;
            }
            // tay thật đã có hình: ẩn găng tay cũ, tắt cần điều khiển ảo từ cử chỉ để không điều khiển hai lần
            if (handRigRenderers != null) foreach (var r in handRigRenderers) if (r != null) r.enabled = !want;
            gloveShown = !want;
            MediaPipeHandSource.SuppressController = want;
        }

        // =====================================================================
        // Dựng hình
        // =====================================================================

        Arm Build(int side)
        {
            if (skin == null)
            {
                skin = Lit(new Color(0.86f, 0.7f, 0.6f));
                sleeve = Lit(new Color(0.93f, 0.94f, 0.96f));  // tay áo blouse trắng
                glove = Lit(new Color(0.27f, 0.56f, 0.93f));
            }
            var a = new Arm { root = new GameObject(side == 0 ? "Cánh tay phải (MediaPipe)" : "Cánh tay trái (MediaPipe)") };
            a.shoulderBall = Part(PrimitiveType.Sphere, a.root.transform, "Vai", sleeve);
            a.upper = Part(PrimitiveType.Capsule, a.root.transform, "Bắp tay", sleeve);
            a.elbowBall = Part(PrimitiveType.Sphere, a.root.transform, "Khuỷu", sleeve);
            a.fore = Part(PrimitiveType.Capsule, a.root.transform, "Cẳng tay", skin);
            for (int i = 0; i < 21; i++)
            {
                a.joints[i] = Part(PrimitiveType.Sphere, a.root.transform, "Khớp " + i, glove);
                if (i > 0) a.bones[i] = Part(PrimitiveType.Capsule, a.root.transform, "Đốt " + i, glove);
            }
            return a;
        }

        static Material Lit(Color c)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { hideFlags = HideFlags.DontSave };
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", 0.4f);
            return m;
        }

        static Transform Part(PrimitiveType type, Transform parent, string name, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Destroy(go.GetComponent<Collider>());  // tia chọn vật đi xuyên qua cánh tay
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }
    }
}
