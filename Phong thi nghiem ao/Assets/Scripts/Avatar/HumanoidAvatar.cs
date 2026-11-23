using System.Collections.Generic;
using Khkt.Demo;
using Khkt.Rendering;
using UnityEngine;
using UnityEngine.UI;

namespace Khkt.Avatar
{
    /// <summary>
    /// Mô hình người 3D hoàn chỉnh dựng bằng mã (cao ~1,65 m): áo blouse trắng, quần tối màu, giày kín mũi,
    /// kính bảo hộ, da với shader Khkt/DaNguoi, tay phải/trái có IK 2 khớp và 3 mức chi tiết (LOD 0/1/2)
    /// tự chọn theo HardwareProfile và khoảng cách camera. Dùng cho người khác trong phòng học chung.
    /// </summary>
    public class HumanoidAvatar : MonoBehaviour
    {
        const float UpperArmLen = 0.27f;
        const float ForeArmLen = 0.25f;
        const float EyeHeightM = 1.54f;

        public string DisplayName { get; private set; } = "Bạn học";
        public bool IsTeacher { get; private set; }
        public int CurrentLod { get; private set; } = -1;
        public Transform HeadTransform => headPivot;
        public Transform RightHandTransform => rightWrist;
        public Transform LeftHandTransform => leftWrist;
        public Transform NameplateTransform => nameplate;
        public HandRig RightHandRig => rightHandRig;
        public HandRig LeftHandRig => leftHandRig;

        Transform torsoPivot, headPivot;
        Transform rightShoulder, rightElbow, rightWrist, rightUpperBone, rightForeBone;
        Transform leftShoulder, leftElbow, leftWrist, leftUpperBone, leftForeBone;
        Transform nameplate;
        Text nameText, roleText;

        HandRig rightHandRig, leftHandRig;
        GameObject lod1RightHandSimple, lod1LeftHandSimple;

        readonly List<GameObject> lod0OnlyParts = new List<GameObject>();
        readonly List<GameObject> lod01Parts = new List<GameObject>();
        readonly List<Renderer> allRenderers = new List<Renderer>();

        Material skinMat, hairMat, coatMat, trimMat, pantsMat, shoeMat, goggleFrameMat, goggleLensMat;
        bool initialized;

        public void Initialize(string displayName, bool isTeacher)
        {
            DisplayName = string.IsNullOrEmpty(displayName) ? (isTeacher ? "Giáo viên" : "Học sinh") : displayName;
            IsTeacher = isTeacher;

            if (!initialized)
            {
                initialized = true;
                BuildMaterials();
                BuildBody();
                BuildNameplate();
            }

            ApplyRoleColors();
            RefreshNameplateText();
            SetLod(0);
        }

        void BuildMaterials()
        {
            Shader skinSh = Shader.Find("Khkt/DaNguoi") ?? Shader.Find("Universal Render Pipeline/Lit");
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");

            skinMat = Mat(skinSh, new Color(0.87f, 0.69f, 0.59f), 0.26f);
            hairMat = Mat(lit, new Color(0.11f, 0.10f, 0.12f), 0.22f);
            coatMat = Mat(lit, new Color(0.95f, 0.96f, 0.98f), 0.14f);
            trimMat = Mat(lit, new Color(0.24f, 0.58f, 0.88f), 0.24f);
            pantsMat = Mat(lit, new Color(0.15f, 0.18f, 0.24f), 0.18f);
            shoeMat = Mat(lit, new Color(0.10f, 0.11f, 0.14f), 0.30f);
            goggleFrameMat = Mat(lit, new Color(0.18f, 0.45f, 0.68f), 0.45f);

            goggleLensMat = new Material(lit) { hideFlags = HideFlags.DontSave };
            goggleLensMat.SetColor("_BaseColor", new Color(0.72f, 0.90f, 0.98f, 0.45f));
            goggleLensMat.SetFloat("_Surface", 1f);
            goggleLensMat.SetFloat("_Smoothness", 0.85f);
            goggleLensMat.renderQueue = 3000;
        }

        void BuildBody()
        {
            // Chân & giày (0.0 .. 0.85 m)
            float[] legXs = { -0.095f, 0.095f };
            for (int i = 0; i < 2; i++)
            {
                float x = legXs[i];
                Part(PrimitiveType.Capsule, transform, $"Chân {(i == 0 ? "trái" : "phải")}",
                    new Vector3(x, 0.44f, 0f), new Vector3(0.125f, 0.41f, 0.125f), Vector3.zero, pantsMat);

                var shoe = Part(PrimitiveType.Capsule, transform, $"Giày {(i == 0 ? "trái" : "phải")}",
                    new Vector3(x, 0.042f, 0.04f), new Vector3(0.105f, 0.105f, 0.068f), new Vector3(90f, 0f, 0f), shoeMat);
                lod01Parts.Add(shoe.gameObject);

                var sole = Part(PrimitiveType.Cube, transform, $"Đế giày {(i == 0 ? "trái" : "phải")}",
                    new Vector3(x, 0.012f, 0.04f), new Vector3(0.108f, 0.022f, 0.22f), Vector3.zero, trimMat);
                lod0OnlyParts.Add(sole.gameObject);
            }

            // Thân & áo blouse trắng (0.72 .. 1.38 m)
            torsoPivot = new GameObject("Thân").transform;
            torsoPivot.SetParent(transform, false);
            torsoPivot.localPosition = new Vector3(0f, 1.05f, 0f);

            Part(PrimitiveType.Capsule, torsoPivot, "Áo blouse",
                new Vector3(0f, -0.02f, 0f), new Vector3(0.35f, 0.34f, 0.22f), Vector3.zero, coatMat);

            var skirt = Part(PrimitiveType.Cylinder, torsoPivot, "Vạt áo blouse",
                new Vector3(0f, -0.20f, 0f), new Vector3(0.345f, 0.14f, 0.225f), Vector3.zero, coatMat);
            lod01Parts.Add(skirt.gameObject);

            // Vạch màu vai áo và cổ áo theo vai trò (Giáo viên / Học sinh)
            var leftTab = Part(PrimitiveType.Cube, torsoPivot, "Cầu vai trái",
                new Vector3(-0.155f, 0.295f, 0f), new Vector3(0.085f, 0.018f, 0.065f), new Vector3(0f, 0f, 12f), trimMat);
            var rightTab = Part(PrimitiveType.Cube, torsoPivot, "Cầu vai phải",
                new Vector3(0.155f, 0.295f, 0f), new Vector3(0.085f, 0.018f, 0.065f), new Vector3(0f, 0f, -12f), trimMat);
            var collar = Part(PrimitiveType.Cylinder, torsoPivot, "Cổ áo",
                new Vector3(0f, 0.315f, 0.01f), new Vector3(0.145f, 0.025f, 0.145f), Vector3.zero, trimMat);
            var pocket = Part(PrimitiveType.Cube, torsoPivot, "Túi áo",
                new Vector3(-0.085f, 0.15f, 0.105f), new Vector3(0.075f, 0.085f, 0.015f), Vector3.zero, trimMat);
            lod01Parts.Add(leftTab.gameObject);
            lod01Parts.Add(rightTab.gameObject);
            lod01Parts.Add(collar.gameObject);
            lod0OnlyParts.Add(pocket.gameObject);

            // Cổ & Đầu (1.38 .. 1.65 m)
            var neck = Part(PrimitiveType.Cylinder, torsoPivot, "Cổ",
                new Vector3(0f, 0.38f, 0f), new Vector3(0.095f, 0.06f, 0.095f), Vector3.zero, skinMat);
            lod01Parts.Add(neck.gameObject);

            headPivot = new GameObject("Đầu").transform;
            headPivot.SetParent(transform, false);
            headPivot.localPosition = new Vector3(0f, EyeHeightM, 0f);

            Part(PrimitiveType.Sphere, headPivot, "Mặt",
                new Vector3(0f, 0.005f, 0f), new Vector3(0.178f, 0.21f, 0.195f), Vector3.zero, skinMat);

            var hair = Part(PrimitiveType.Sphere, headPivot, "Tóc",
                new Vector3(0f, 0.044f, -0.014f), new Vector3(0.186f, 0.15f, 0.205f), Vector3.zero, hairMat);
            lod01Parts.Add(hair.gameObject);

            var nose = Part(PrimitiveType.Capsule, headPivot, "Mũi",
                new Vector3(0f, -0.010f, 0.092f), new Vector3(0.022f, 0.022f, 0.022f), new Vector3(12f, 0f, 0f), skinMat);
            lod0OnlyParts.Add(nose.gameObject);

            // Kính bảo hộ trong suốt + gọng
            var gogglesFrame = Part(PrimitiveType.Cube, headPivot, "Gọng kính bảo hộ",
                new Vector3(0f, 0.018f, 0.092f), new Vector3(0.148f, 0.016f, 0.024f), Vector3.zero, goggleFrameMat);
            var gogglesVisor = Part(PrimitiveType.Cube, headPivot, "Mắt kính bảo hộ",
                new Vector3(0f, 0.004f, 0.096f), new Vector3(0.140f, 0.042f, 0.016f), Vector3.zero, goggleLensMat);
            lod01Parts.Add(gogglesFrame.gameObject);
            lod01Parts.Add(gogglesVisor.gameObject);

            // Hai cánh tay & hai bàn tay (giải phẫu LOD 0 / đơn giản LOD 1)
            rightShoulder = NewJoint(torsoPivot, "Vai phải", new Vector3(0.205f, 0.275f, 0f));
            leftShoulder = NewJoint(torsoPivot, "Vai trái", new Vector3(-0.205f, 0.275f, 0f));

            rightUpperBone = Part(PrimitiveType.Capsule, transform, "Bắp tay phải", Vector3.zero, Vector3.one * 0.088f, Vector3.zero, coatMat);
            rightForeBone = Part(PrimitiveType.Capsule, transform, "Cẳng tay phải", Vector3.zero, Vector3.one * 0.076f, Vector3.zero, coatMat);
            rightElbow = NewJoint(transform, "Khuỷu phải", Vector3.zero);
            rightWrist = NewJoint(transform, "Cổ tay phải", Vector3.zero);

            leftUpperBone = Part(PrimitiveType.Capsule, transform, "Bắp tay trái", Vector3.zero, Vector3.one * 0.088f, Vector3.zero, coatMat);
            leftForeBone = Part(PrimitiveType.Capsule, transform, "Cẳng tay trái", Vector3.zero, Vector3.one * 0.076f, Vector3.zero, coatMat);
            leftElbow = NewJoint(transform, "Khuỷu trái", Vector3.zero);
            leftWrist = NewJoint(transform, "Cổ tay trái", Vector3.zero);

            var rHandGo = new GameObject("Bàn tay phải LOD0");
            rHandGo.transform.SetParent(rightWrist, false);
            rightHandRig = rHandGo.AddComponent<HandRig>();
            rightHandRig.side = 0;
            rightHandRig.EnsureBuilt();
            rightHandRig.SkipIdleUpdate = true;
            rightHandRig.SetTargetPose(HandPoseId.ChumCam, immediate: true);

            var lHandGo = new GameObject("Bàn tay trái LOD0");
            lHandGo.transform.SetParent(leftWrist, false);
            leftHandRig = lHandGo.AddComponent<HandRig>();
            leftHandRig.side = 1;
            leftHandRig.EnsureBuilt();
            leftHandRig.SkipIdleUpdate = true;
            leftHandRig.SetTargetPose(HandPoseId.Nghi, immediate: true);

            lod1RightHandSimple = Part(PrimitiveType.Sphere, rightWrist, "Tay phải LOD1",
                new Vector3(0f, 0f, 0.045f), new Vector3(0.075f, 0.032f, 0.115f), Vector3.zero, trimMat).gameObject;
            lod1LeftHandSimple = Part(PrimitiveType.Sphere, leftWrist, "Tay trái LOD1",
                new Vector3(0f, 0f, 0.045f), new Vector3(0.075f, 0.032f, 0.115f), Vector3.zero, trimMat).gameObject;
        }

        void BuildNameplate()
        {
            var go = new GameObject("Bảng tên", typeof(Canvas));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(540f, 94f);
            rt.localScale = Vector3.one * 0.0015f;

            var bg = GlassKit.Rect(rt, "Nền", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(540f, 94f)).gameObject.AddComponent<Image>();
            bg.sprite = GlassKit.Solid;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.04f, 0.07f, 0.13f, 0.84f);
            bg.raycastTarget = false;

            nameText = GlassKit.Label(rt, "Tên", DisplayName, 40, Color.white, true,
                TextAnchor.MiddleCenter, new Vector2(0f, 12f), new Vector2(520f, 48f), new Vector2(0f, 1f));
            roleText = GlassKit.Label(rt, "Vai trò", "", 24, new Color(0.55f, 0.86f, 1f), false,
                TextAnchor.MiddleCenter, new Vector2(0f, -24f), new Vector2(520f, 34f), new Vector2(0f, 1f));

            nameplate = go.transform;
        }

        void ApplyRoleColors()
        {
            Color sleeveCoat = IsTeacher ? new Color(0.92f, 0.96f, 0.99f) : new Color(0.95f, 0.96f, 0.98f);
            Color roleTrim = IsTeacher ? new Color(0.09f, 0.36f, 0.62f) : new Color(0.28f, 0.62f, 0.90f);

            if (coatMat != null) coatMat.SetColor("_BaseColor", sleeveCoat);
            if (trimMat != null) trimMat.SetColor("_BaseColor", roleTrim);
            if (rightHandRig != null) rightHandRig.SetSleeveColors(sleeveCoat, roleTrim);
            if (leftHandRig != null) leftHandRig.SetSleeveColors(sleeveCoat, roleTrim);
            if (roleText != null) roleText.color = IsTeacher ? new Color(1f, 0.82f, 0.42f) : new Color(0.55f, 0.86f, 1f);
        }

        void RefreshNameplateText()
        {
            if (nameText != null) nameText.text = DisplayName;
            if (roleText != null) roleText.text = IsTeacher ? "Giáo viên" : "Học sinh";
        }

        public void SetName(string name, bool? isTeacher = null)
        {
            if (!string.IsNullOrEmpty(name)) DisplayName = name;
            if (isTeacher.HasValue && isTeacher.Value != IsTeacher)
            {
                IsTeacher = isTeacher.Value;
                ApplyRoleColors();
            }
            RefreshNameplateText();
        }

        /// <summary>Cập nhật tư thế đầu, thân, 2 tay và bảng tên theo gói mạng.</summary>
        public void ApplyPose(Vector3 headPos, float yaw, float pitch, Vector3 rightHandPos, Quaternion rightHandRot,
            Vector3? leftHandPos = null, Transform lookCamera = null)
        {
            if (!initialized) Initialize(DisplayName, IsTeacher);

            Vector3 feetPos = new Vector3(headPos.x, headPos.y - EyeHeightM, headPos.z);
            transform.position = feetPos;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            headPivot.position = headPos;
            headPivot.rotation = Quaternion.Euler(pitch, yaw, 0f);

            // Tay phải theo handPos mạng
            PlaceArmIk(rightShoulder.position, rightHandPos, rightHandRot, 0, rightUpperBone, rightForeBone, rightElbow, rightWrist);
            if (rightHandRig != null && rightHandRig.gameObject.activeSelf)
            {
                rightHandRig.transform.SetPositionAndRotation(rightHandPos, rightHandRot);
                rightHandRig.StepPoseAndContact(Time.unscaledDeltaTime);
            }

            // Tay trái theo target (nếu có) hoặc để nghỉ bên hông
            Quaternion bodyRot = transform.rotation;
            Vector3 lTarget = leftHandPos ?? (torsoPivot.position + bodyRot * new Vector3(-0.24f, -0.08f, 0.24f));
            Quaternion lRot = bodyRot * Quaternion.Euler(14f, 12f, 0f);
            PlaceArmIk(leftShoulder.position, lTarget, lRot, 1, leftUpperBone, leftForeBone, leftElbow, leftWrist);
            if (leftHandRig != null && leftHandRig.gameObject.activeSelf)
            {
                leftHandRig.transform.SetPositionAndRotation(lTarget, lRot);
                leftHandRig.StepPoseAndContact(Time.unscaledDeltaTime);
            }

            if (nameplate != null)
            {
                nameplate.position = headPos + Vector3.up * 0.29f;
                if (lookCamera != null)
                    nameplate.rotation = Quaternion.LookRotation(nameplate.position - lookCamera.position);
            }
        }

        public void UpdateLod(Vector3 viewerPos)
        {
            float dist = Vector3.Distance(transform.position + Vector3.up * 1.1f, viewerPos);
            MucDoHoa tier = HardwareProfile.Muc;
            int targetLod = (tier == MucDoHoa.Thap || dist > 14f) ? 2 : (tier == MucDoHoa.Vua || dist > 6f ? 1 : 0);
            SetLod(targetLod);
        }

        public void SetLod(int lod)
        {
            lod = Mathf.Clamp(lod, 0, 2);
            if (lod == CurrentLod) return;
            CurrentLod = lod;

            for (int i = 0; i < lod0OnlyParts.Count; i++)
                if (lod0OnlyParts[i] != null) lod0OnlyParts[i].SetActive(lod == 0);
            for (int i = 0; i < lod01Parts.Count; i++)
                if (lod01Parts[i] != null) lod01Parts[i].SetActive(lod <= 1);

            if (rightHandRig != null) rightHandRig.gameObject.SetActive(lod == 0);
            if (leftHandRig != null) leftHandRig.gameObject.SetActive(lod == 0);
            if (lod1RightHandSimple != null) lod1RightHandSimple.SetActive(lod >= 1);
            if (lod1LeftHandSimple != null) lod1LeftHandSimple.SetActive(lod >= 1);
        }

        void PlaceArmIk(Vector3 sh, Vector3 target, Quaternion handRot, int side, Transform upper, Transform fore, Transform elbowT, Transform wristT)
        {
            Vector3 to = target - sh;
            float d = Mathf.Clamp(to.magnitude, 0.06f, UpperArmLen + ForeArmLen - 0.002f);
            Vector3 dir = to.sqrMagnitude > 1e-6f ? to.normalized : transform.forward;
            Vector3 wrist = sh + dir * d;

            float a = (UpperArmLen * UpperArmLen - ForeArmLen * ForeArmLen + d * d) / (2f * d);
            float h = Mathf.Sqrt(Mathf.Max(0f, UpperArmLen * UpperArmLen - a * a));
            Vector3 pole = Vector3.down + transform.right * (side == 0 ? 0.55f : -0.55f);
            Vector3 bend = Vector3.ProjectOnPlane(pole, dir);
            if (bend.sqrMagnitude < 1e-6f) bend = Vector3.ProjectOnPlane(Vector3.down, dir);
            Vector3 elbow = sh + dir * a + bend.normalized * h;

            elbowT.position = elbow;
            wristT.SetPositionAndRotation(wrist, handRot);

            PlaceBone(upper, sh, elbow, 0.088f);
            PlaceBone(fore, elbow, wrist, 0.074f);
        }

        static void PlaceBone(Transform t, Vector3 from, Vector3 to, float width)
        {
            Vector3 d = to - from;
            t.position = (from + to) * 0.5f;
            t.rotation = d.sqrMagnitude > 1e-8f ? Quaternion.FromToRotation(Vector3.up, d) : Quaternion.identity;
            t.localScale = new Vector3(width, Mathf.Max(width, d.magnitude) * 0.5f, width);
        }

        static Transform NewJoint(Transform parent, string name, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }

        Transform Part(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Vector3 euler, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = euler;
            go.transform.localScale = scale;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            allRenderers.Add(mr);
            return go.transform;
        }

        // C5: mỗi hình người tạo 8 vật liệu riêng; người khác vào/ra phòng học chung nhiều lần thì phải giải phóng, không rò bộ nhớ
        void OnDestroy()
        {
            foreach (var m in new[] { skinMat, hairMat, coatMat, trimMat, pantsMat, shoeMat, goggleFrameMat, goggleLensMat })
                if (m != null) Destroy(m);
        }

        static Material Mat(Shader sh, Color color, float smooth)
        {
            var m = new Material(sh) { hideFlags = HideFlags.DontSave };
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smooth);
            return m;
        }
    }
}
