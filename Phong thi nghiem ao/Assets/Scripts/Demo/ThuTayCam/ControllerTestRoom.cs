using System.Collections.Generic;
using Khkt.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace Khkt.Demo
{
    /// <summary>
    /// Phòng thử tay cầm VERA (cảnh riêng Assets/Scenes/ThuTayCam.unity, menu KHKT > Phòng thử tay cầm).
    /// Một bàn thí nghiệm nhỏ dựng hoàn toàn bằng mã lúc chạy, để thử 5 thao tác trước khi vào lab:
    ///   1. lia tay / tia qua lại (vệt sáng cho thấy đường đi có mượt không; bảng số đo rung, tuổi mẫu, gói/s);
    ///   2. chỉ tia vào 3 bia;
    ///   3. cầm lọ, trỏ vào cốc, lăn cổ tay (hoặc giữ A) để rót;
    ///   4. cầm pipet, trỏ vào lọ, giữ A để hút; trỏ vào cốc, giữ A để nhả;
    ///   5. nhấn cần mở bảng dung dịch (giống bảng tra cứu hóa chất của app chính), trỏ + chạm B để chọn, nạp vào lọ C.
    /// Nút như trong lab (HandController.VeraButtons): giữ B = cầm, buông B = đặt; giữ A = dùng (tia chậm 4 lần);
    /// lăn cổ tay quá 35° khi đang trỏ vào cốc = rót. Không có tay cầm: chuột (trái = B, phải = A, Q / E lăn cổ tay, Tab = bảng).
    /// Chỉ dùng ControllerSource, PadInput, VeraPointer, GlassKit; không phụ thuộc HandController / LabContainer.
    /// </summary>
    public partial class ControllerTestRoom : MonoBehaviour
    {
        [Header("Tài nguyên (trình dựng gán sẵn để bản .exe không thiếu shader)")]
        public Shader glassShader;
        public Shader outlineShader;
        public Material glassMaterial, liquidMaterial, solidMaterial, gloveMaterial;
        [Tooltip("Tạo nguồn tay cầm USB + Wi-Fi. Bài kiểm thử tắt để không mở cổng COM của mạch thật")]
        public bool createSources = true;

        public enum Act { None, Pick, Target, Pour, Pipette, Place }

        public Camera Cam { get; private set; }
        /// <summary>Tia trỏ của khung hình này (đã lọc, dự đoán).</summary>
        public Ray AimRay { get; private set; }
        /// <summary>Góc tia so với hệ thế giới: x = hướng ngang (độ, dương = phải), y = ngẩng (độ).</summary>
        public Vector2 AimDeg { get; private set; }
        public bool UsingVera { get; private set; }
        public Act Current { get; private set; }
        public TestItem Held { get; private set; }
        public TestItem Hovered { get; private set; }
        public float Tilt { get; private set; }
        public readonly bool[] Done = new bool[5];
        public readonly List<TestItem> Items = new List<TestItem>();
        public readonly List<Transform> Targets = new List<Transform>();
        public int TargetsHit { get; private set; }
        public int Sweeps { get; private set; }
        public float PouredMl { get; private set; }
        public float SuckedMl { get; private set; }
        public float ReleasedMl { get; private set; }
        public float JitterDeg { get; private set; }
        public float SpeedDps { get; private set; }

        VeraPointer vera;
        Transform hand, handTip;
        readonly List<Transform> fingers = new List<Transform>();
        LineRenderer laser, trail;
        Transform dot, stream;
        Material streamMat;
        HoverOutline outline;
        Vector3 hitPoint;
        bool hitTable;
        float hitDist;
        TestItem hitItem;
        Transform hitTarget;
        readonly float[] targetDwell = new float[3];
        readonly bool[] targetDone = new bool[3];
        Vector3 anchor;
        Quaternion anchorRot = Quaternion.identity;
        bool anchorValid;
        int sweepSide;
        bool pipetteSuck;
        float mouseRoll;
        // nút: B = cầm, A = dùng, cần = bảng dung dịch
        bool aNow, bNow, aPrev, bPrev, stickPrev;
        VeraFlags latched;
        readonly List<ControllerSource> hooked = new List<ControllerSource>();
        int sampleCount;
        float rateTimer, lastSampleReal = -10f;
        public int SamplesPerSecond { get; private set; }
        readonly Queue<(float t, Vector2 a)> history = new Queue<(float, Vector2)>();
        readonly Queue<Vector3> trailPts = new Queue<Vector3>();

        const float TableY = 0.78f;
        static readonly Vector3 CamPos = new Vector3(0f, 1.27f, -0.56f);

        // ---------------------------------------------------------------- dựng cảnh

        void Awake()
        {
            Application.targetFrameRate = Mathf.Max(60, (int)Screen.currentResolution.refreshRateRatio.value);
            QualitySettings.vSyncCount = 0;
            BuildCamera();
            BuildRoom();
            BuildHand();
            BuildPointerVisuals();
            BuildUI();
            vera = VeraPointer.Ensure(gameObject);
            VeraPointer.Message += Toast;
            PadInput.Notice += Toast;
            PadInput.ControllerChanged += OnControllerChanged;
            if (createSources)
            {
                var src = new GameObject("Nguồn tay cầm");
                src.transform.SetParent(transform, false);
                src.AddComponent<SerialControllerSource>().portName = "auto";
                src.AddComponent<UdpControllerSource>();
            }
            outline = gameObject.AddComponent<HoverOutline>();
            outline.outlineShader = outlineShader != null ? outlineShader : Shader.Find("Khkt/VienSang");
        }

        void OnDestroy()
        {
            VeraPointer.Message -= Toast;
            PadInput.Notice -= Toast;
            PadInput.ControllerChanged -= OnControllerChanged;
            foreach (var s in hooked)
                if (s != null) s.SampleReceived -= OnSample;
        }

        void OnControllerChanged(bool on, string who) => Toast(on ? $"Đã nối tay cầm {who}" : "Mất kết nối tay cầm, chuyển sang chuột");

        void BuildCamera()
        {
            var go = new GameObject("Camera", typeof(Camera), typeof(AudioListener));
            go.transform.SetParent(transform, false);
            Cam = go.GetComponent<Camera>();
            Cam.transform.position = CamPos;
            Cam.transform.rotation = Quaternion.Euler(29f, 0f, 0f);
            Cam.fieldOfView = 58f;
            Cam.nearClipPlane = 0.01f;
            Cam.farClipPlane = 30f;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = new Color(0.07f, 0.09f, 0.12f);
            var data = Cam.GetUniversalAdditionalCameraData();
            data.requiresColorOption = CameraOverrideOption.On;  // kính mờ của bảng cần ảnh cảnh phía sau
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            go.tag = "MainCamera";

            var sun = new GameObject("Đèn", typeof(Light)).GetComponent<Light>();
            sun.transform.SetParent(transform, false);
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(55f, -25f, 0f);
            sun.intensity = 1.25f;
            sun.color = new Color(1f, 0.97f, 0.92f);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.6f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.46f, 0.52f);
            RenderSettings.fog = false;
        }

        Material Solid(Color c)
        {
            Material m;
            if (solidMaterial != null) m = new Material(solidMaterial);
            else m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.hideFlags = HideFlags.DontSave;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", null);
            if (m.HasProperty("_BumpMap")) m.SetTexture("_BumpMap", null);
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.35f);
            return m;
        }

        Material Transparent(Material src, Color c)
        {
            Material m = src != null ? new Material(src) : Solid(c);
            m.hideFlags = HideFlags.DontSave;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            m.color = c;
            return m;
        }

        static Material Unlit(Color c)
        {
            var sh = Shader.Find("UI/Default") ?? Shader.Find("Sprites/Default");
            return new Material(sh) { color = c, hideFlags = HideFlags.DontSave, renderQueue = 3100 };
        }

        Transform Prim(PrimitiveType t, string name, Transform parent, Vector3 pos, Vector3 scale, Material m, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(t);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            if (m != null) go.GetComponent<Renderer>().sharedMaterial = m;
            if (!keepCollider) Destroy(go.GetComponent<Collider>());
            return go.transform;
        }

        void BuildRoom()
        {
            var room = new GameObject("Phòng").transform;
            room.SetParent(transform, false);
            Prim(PrimitiveType.Cube, "Sàn", room, new Vector3(0, -0.05f, 0.5f), new Vector3(8, 0.1f, 8), Solid(new Color(0.16f, 0.18f, 0.21f)));
            Prim(PrimitiveType.Cube, "Tường", room, new Vector3(0, 1.5f, 1.6f), new Vector3(8, 3f, 0.1f), Solid(new Color(0.2f, 0.25f, 0.3f)));
            var top = Prim(PrimitiveType.Cube, "Mặt bàn", room, new Vector3(0, TableY - 0.02f, 0.12f), new Vector3(1.5f, 0.04f, 0.76f),
                           Solid(new Color(0.86f, 0.88f, 0.9f)), keepCollider: true);
            top.gameObject.AddComponent<TestTable>();
            var leg = Solid(new Color(0.3f, 0.33f, 0.37f));
            foreach (var x in new[] { -0.7f, 0.7f })
                foreach (var z in new[] { -0.2f, 0.45f })
                    Prim(PrimitiveType.Cube, "Chân bàn", room, new Vector3(x, (TableY - 0.04f) / 2, z), new Vector3(0.05f, TableY - 0.04f, 0.05f), leg);
            // vạch dẫn trên mặt bàn: khu lọ / khu cốc / pipet
            var mark = Solid(new Color(0.38f, 0.86f, 1f) * 0.8f);
            Prim(PrimitiveType.Cube, "Vạch", room, new Vector3(-0.3f, TableY + 0.0005f, 0.38f), new Vector3(0.42f, 0.001f, 0.004f), mark);
            Prim(PrimitiveType.Cube, "Vạch", room, new Vector3(0.1f, TableY + 0.0005f, 0.38f), new Vector3(0.3f, 0.001f, 0.004f), mark);

            // bảng bia phía sau bàn
            var board = Prim(PrimitiveType.Cube, "Bảng bia", room, new Vector3(0, 0.97f, 0.78f), new Vector3(1.3f, 0.36f, 0.02f), Solid(new Color(0.12f, 0.15f, 0.19f)), true);
            board.gameObject.AddComponent<TestTable>().isBoard = true;
            var tm = Solid(new Color(1f, 0.55f, 0.2f));
            Vector3[] tp = { new Vector3(-0.45f, 1.0f, 0.765f), new Vector3(0f, 1.05f, 0.765f), new Vector3(0.45f, 0.95f, 0.765f) };
            for (int i = 0; i < 3; i++)
            {
                var t = Prim(PrimitiveType.Cylinder, $"Bia {i + 1}", room, tp[i], new Vector3(0.09f, 0.004f, 0.09f), new Material(tm), true);
                t.localRotation = Quaternion.Euler(90, 0, 0);
                Prim(PrimitiveType.Cylinder, "Tâm", t, new Vector3(0, 1.2f, 0), new Vector3(0.35f, 1f, 0.35f), Solid(Color.white));
                Targets.Add(t);
            }

            // giá pipet
            Prim(PrimitiveType.Cube, "Giá pipet", room, new Vector3(0.42f, TableY + 0.02f, 0.24f), new Vector3(0.07f, 0.04f, 0.05f), Solid(new Color(0.25f, 0.6f, 0.75f)));

            var catalog = Solutions;
            Items.Add(MakeBottle("Lọ A", new Vector3(-0.44f, TableY, 0.25f), catalog[1], 140f));
            Items.Add(MakeBottle("Lọ B", new Vector3(-0.3f, TableY, 0.3f), catalog[2], 140f));
            Items.Add(MakeBottle("Lọ C", new Vector3(-0.16f, TableY, 0.25f), null, 0f));
            Items.Add(MakeBeaker("Cốc 1", new Vector3(0.03f, TableY, 0.16f)));
            Items.Add(MakeBeaker("Cốc 2", new Vector3(0.19f, TableY, 0.24f)));
            Items.Add(MakePipette("Pipet", new Vector3(0.42f, TableY + 0.035f, 0.24f)));
        }

        TestItem MakeItem(string name, Vector3 pos, TestItem.Kind kind)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            var it = go.AddComponent<TestItem>();
            it.kind = kind;
            it.label = name;
            it.home = pos;
            return it;
        }

        void AddLiquid(TestItem it, float radius, float bottom, float height, float cap)
        {
            it.capMl = cap;
            it.innerRadius = radius;
            it.innerBottom = bottom;
            it.innerHeight = height;
            it.liquidMat = Transparent(liquidMaterial, new Color(0.3f, 0.6f, 1f, 0.7f));
            it.liquid = Prim(PrimitiveType.Cylinder, "Dung dịch", it.transform, Vector3.zero, Vector3.one, it.liquidMat);
            it.liquid.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void Finish(TestItem it, Vector3 center, Vector3 size)
        {
            var bc = it.gameObject.AddComponent<BoxCollider>();
            bc.center = center;
            bc.size = size;
            it.UpdateLiquid();
        }

        TestItem MakeBottle(string name, Vector3 pos, Solution s, float ml)
        {
            var it = MakeItem(name, pos, TestItem.Kind.Bottle);
            var glass = Transparent(glassMaterial, new Color(0.85f, 0.93f, 1f, 0.28f));
            Prim(PrimitiveType.Cylinder, "Thân lọ (kính)", it.transform, new Vector3(0, 0.055f, 0), new Vector3(0.07f, 0.055f, 0.07f), glass);
            Prim(PrimitiveType.Sphere, "Vai lọ (kính)", it.transform, new Vector3(0, 0.11f, 0), new Vector3(0.07f, 0.03f, 0.07f), glass);
            Prim(PrimitiveType.Cylinder, "Cổ lọ (kính)", it.transform, new Vector3(0, 0.13f, 0), new Vector3(0.028f, 0.015f, 0.028f), glass);
            Prim(PrimitiveType.Cube, "Nhãn", it.transform, new Vector3(0, 0.06f, -0.0352f), new Vector3(0.04f, 0.035f, 0.001f), Solid(new Color(0.97f, 0.97f, 0.94f)));
            it.mouthLocal = new Vector3(0, 0.145f, 0);
            it.radius = 0.035f;
            it.height = 0.145f;
            AddLiquid(it, 0.032f, 0.004f, 0.1f, 150f);
            if (s != null && ml > 0) it.Fill(s, ml);
            Finish(it, new Vector3(0, 0.0725f, 0), new Vector3(0.075f, 0.145f, 0.075f));
            return it;
        }

        TestItem MakeBeaker(string name, Vector3 pos)
        {
            var it = MakeItem(name, pos, TestItem.Kind.Beaker);
            var glass = Transparent(glassMaterial, new Color(0.85f, 0.93f, 1f, 0.22f));
            Prim(PrimitiveType.Cylinder, "Thành cốc (kính)", it.transform, new Vector3(0, 0.0425f, 0), new Vector3(0.084f, 0.0425f, 0.084f), glass);
            var tick = Solid(new Color(0.9f, 0.95f, 1f));
            for (int i = 1; i <= 3; i++)
                Prim(PrimitiveType.Cube, "Vạch chia", it.transform, new Vector3(-0.018f, 0.02f * i, -0.0425f), new Vector3(0.014f, 0.0012f, 0.001f), tick);
            it.mouthLocal = new Vector3(0, 0.085f, 0);
            it.radius = 0.042f;
            it.height = 0.085f;
            AddLiquid(it, 0.039f, 0.003f, 0.078f, 100f);
            Finish(it, new Vector3(0, 0.0425f, 0), new Vector3(0.088f, 0.085f, 0.088f));
            return it;
        }

        TestItem MakePipette(string name, Vector3 pos)
        {
            var it = MakeItem(name, pos, TestItem.Kind.Pipette);
            var glass = Transparent(glassMaterial, new Color(0.85f, 0.93f, 1f, 0.35f));
            Prim(PrimitiveType.Cylinder, "Đầu pipet (kính)", it.transform, new Vector3(0, 0.015f, 0), new Vector3(0.0045f, 0.015f, 0.0045f), glass);
            Prim(PrimitiveType.Cylinder, "Ống pipet (kính)", it.transform, new Vector3(0, 0.115f, 0), new Vector3(0.009f, 0.085f, 0.009f), glass);
            Prim(PrimitiveType.Sphere, "Quả bóp", it.transform, new Vector3(0, 0.215f, 0), new Vector3(0.026f, 0.032f, 0.026f), Solid(new Color(0.85f, 0.18f, 0.2f)));
            it.mouthLocal = Vector3.zero;  // pipet: gốc ở đầu hút
            it.radius = 0.01f;
            it.height = 0.23f;
            AddLiquid(it, 0.0036f, 0.004f, 0.19f, 5f);
            Finish(it, new Vector3(0, 0.115f, 0), new Vector3(0.03f, 0.23f, 0.03f));
            return it;
        }

        void BuildHand()
        {
            hand = new GameObject("Bàn tay").transform;
            hand.SetParent(transform, false);
            var glove = gloveMaterial != null ? new Material(gloveMaterial) : Solid(new Color(0.35f, 0.62f, 0.95f));
            glove.hideFlags = HideFlags.DontSave;
            if (glove.HasProperty("_BaseMap")) glove.SetTexture("_BaseMap", null);
            if (glove.HasProperty("_BaseColor")) glove.SetColor("_BaseColor", new Color(0.36f, 0.64f, 0.95f));
            Prim(PrimitiveType.Cube, "Lòng bàn tay", hand, new Vector3(0, 0, -0.01f), new Vector3(0.072f, 0.024f, 0.08f), glove);
            Prim(PrimitiveType.Capsule, "Cổ tay", hand, new Vector3(0, -0.002f, -0.07f), new Vector3(0.05f, 0.04f, 0.04f), glove).localRotation = Quaternion.Euler(90, 0, 0);
            float[] xs = { 0.027f, 0.009f, -0.009f, -0.027f };
            float[] len = { 0.062f, 0.07f, 0.066f, 0.054f };
            for (int i = 0; i < 4; i++)
            {
                var pivot = new GameObject("Ngón " + (i + 1)).transform;
                pivot.SetParent(hand, false);
                pivot.localPosition = new Vector3(xs[i], 0, 0.03f);
                var f = Prim(PrimitiveType.Capsule, "Đốt", pivot, new Vector3(0, 0, len[i] / 2), new Vector3(0.016f, len[i] / 2, 0.016f), glove);
                f.localRotation = Quaternion.Euler(90, 0, 0);
                fingers.Add(pivot);
                if (i == 0)
                {
                    handTip = new GameObject("Đầu ngón trỏ").transform;
                    handTip.SetParent(pivot, false);
                    handTip.localPosition = new Vector3(0, 0, len[i] + 0.006f);
                }
            }
            var thumb = new GameObject("Ngón cái").transform;
            thumb.SetParent(hand, false);
            thumb.localPosition = new Vector3(0.038f, -0.004f, -0.02f);
            thumb.localRotation = Quaternion.Euler(0, 40, 0);
            Prim(PrimitiveType.Capsule, "Đốt", thumb, new Vector3(0, 0, 0.025f), new Vector3(0.018f, 0.026f, 0.018f), glove).localRotation = Quaternion.Euler(90, 0, 0);
            fingers.Add(thumb);
            foreach (var r in hand.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void BuildPointerVisuals()
        {
            var accent = new Color(0.38f, 0.86f, 1f);
            laser = new GameObject("Tia trỏ", typeof(LineRenderer)).GetComponent<LineRenderer>();
            laser.transform.SetParent(transform, false);
            laser.sharedMaterial = Unlit(Color.white);
            laser.positionCount = 2;
            laser.widthCurve = new AnimationCurve(new Keyframe(0, 0.0035f), new Keyframe(1, 0.0015f));
            laser.colorGradient = Gradient2(new Color(accent.r, accent.g, accent.b, 0.95f), new Color(accent.r, accent.g, accent.b, 0.55f));
            laser.numCapVertices = 3;
            laser.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            trail = new GameObject("Vệt đường đi", typeof(LineRenderer)).GetComponent<LineRenderer>();
            trail.transform.SetParent(transform, false);
            trail.sharedMaterial = Unlit(Color.white);
            trail.widthCurve = new AnimationCurve(new Keyframe(0, 0.0005f), new Keyframe(1, 0.005f));
            trail.colorGradient = Gradient2(new Color(1f, 0.8f, 0.3f, 0f), new Color(1f, 0.8f, 0.3f, 0.9f));
            trail.numCornerVertices = 2;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            dot = Prim(PrimitiveType.Sphere, "Điểm trỏ", transform, Vector3.zero, Vector3.one * 0.011f, Unlit(accent));
            dot.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            streamMat = Transparent(liquidMaterial, new Color(0.3f, 0.6f, 1f, 0.8f));
            stream = Prim(PrimitiveType.Cylinder, "Dòng rót", transform, Vector3.zero, Vector3.one, streamMat);
            stream.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            stream.gameObject.SetActive(false);
        }

        static Gradient Gradient2(Color a, Color b)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(a, 0), new GradientColorKey(b, 1) }, new[] { new GradientAlphaKey(a.a, 0), new GradientAlphaKey(b.a, 1) });
            return g;
        }

        // ---------------------------------------------------------------- đầu vào

        void HookSources()
        {
            foreach (var s in PadInput.Sources)
                if (s != null && !hooked.Contains(s))
                {
                    hooked.Add(s);
                    s.SampleReceived += OnSample;
                }
        }

        void OnSample(ControllerState s)
        {
            if (!s.Vera) return;
            latched |= s.Flags;  // giữ cả lần chạm rất ngắn nằm giữa hai khung hình
            if (s.Pressed(ControllerButtons.Joy2Press)) latched |= VeraFlags.StickPress;
            sampleCount++;
            lastSampleReal = Time.realtimeSinceStartup;
        }

        public float SampleAgeMs => (Time.realtimeSinceStartup - lastSampleReal) * 1000f;

        void ReadInput(out bool menuToggle)
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            UsingVera = vera != null && vera.HasControl && PadInput.AimFromCenter;
            bool veraOn = vera != null && vera.HasControl;
            bool a = false, b = false, stick = false;
            if (veraOn)
            {
                var s = vera.Source.Latest;
                var f = s.Flags | latched;
                a = (f & VeraFlags.TouchA) != 0;
                b = (f & VeraFlags.TouchB) != 0;
                stick = (f & VeraFlags.StickPress) != 0 || s.Pressed(ControllerButtons.Joy2Press);
            }
            latched = VeraFlags.None;
            if (mouse != null)
            {
                b |= mouse.leftButton.isPressed;
                a |= mouse.rightButton.isPressed;
            }
            if (kb != null) a |= kb.spaceKey.isPressed;
            aPrev = aNow;
            bPrev = bNow;
            aNow = a;
            bNow = b;
            menuToggle = (stick && !stickPrev) || (kb != null && (kb.tabKey.wasPressedThisFrame || kb.mKey.wasPressedThisFrame));
            stickPrev = stick;
            if (vera != null) vera.FineMode = Held != null && aNow && UsingVera;

            if (UsingVera) AimRay = vera.GetRay(Cam);
            else if (mouse != null) AimRay = Cam.ScreenPointToRay(mouse.position.ReadValue());
            else AimRay = new Ray(Cam.transform.position, Cam.transform.forward);

            Vector3 d = AimRay.direction;
            AimDeg = new Vector2(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, Mathf.Asin(Mathf.Clamp(d.y, -1, 1)) * Mathf.Rad2Deg);

            // lăn cổ tay: tay cầm VERA, hoặc giữ Q / E khi dùng chuột
            float want = 0;
            if (kb != null) want = (kb.eKey.isPressed ? 75f : 0f) - (kb.qKey.isPressed ? 75f : 0f);
            mouseRoll = Mathf.MoveTowards(mouseRoll, want, 160f * Time.deltaTime);
            if (kb != null)
            {
                if (kb.rKey.wasPressedThisFrame && vera != null) { vera.Recenter(); Toast("Đã căn lại tia về giữa"); }
                if (kb.f5Key.wasPressedThisFrame) ResetRoom();
                if (kb.escapeKey.wasPressedThisFrame && !MenuOpen)
                {
#if !UNITY_EDITOR
                    Application.Quit();
#endif
                }
            }
        }

        public float Roll => UsingVera || (vera != null && vera.HasControl) ? vera.Roll : mouseRoll;
        public bool APressed => aNow && !aPrev;
        public bool BPressed => bNow && !bPrev;
        public bool BReleased => !bNow && bPrev;

        // ---------------------------------------------------------------- vòng lặp

        void Update()
        {
            HookSources();
            ReadInput(out bool menuToggle);
            if (menuToggle) SetMenu(!MenuOpen);
            UpdateStats();

            if (MenuOpen)
            {
                MenuTick();
                hand.gameObject.SetActive(false);
                laser.enabled = trail.enabled = false;
                dot.gameObject.SetActive(false);
                stream.gameObject.SetActive(false);
                outline.SetTarget(null);
                UpdateUI();
                return;
            }
            hand.gameObject.SetActive(true);
            laser.enabled = trail.enabled = true;
            dot.gameObject.SetActive(true);

            Raycast();
            Decide();
            Act_(Time.deltaTime);
            PoseHand();
            UpdatePointerTasks(Time.deltaTime);
            UpdateUI();
        }

        void Raycast()
        {
            hitItem = null;
            hitTarget = null;
            hitTable = false;
            if (Physics.Raycast(AimRay, out var hit, 6f))
            {
                hitPoint = hit.point;
                hitDist = hit.distance;
                hitItem = hit.collider.GetComponentInParent<TestItem>();
                if (Targets.Contains(hit.collider.transform)) hitTarget = hit.collider.transform;
                var t = hit.collider.GetComponent<TestTable>();
                hitTable = t != null && !t.isBoard && hit.normal.y > 0.7f;
            }
            else
            {
                hitDist = 1.2f;
                hitPoint = AimRay.GetPoint(hitDist);
            }
        }

        void Decide()
        {
            Hovered = hitItem;
            if (Held == null) Current = hitItem != null ? Act.Pick : hitTarget != null ? Act.Target : Act.None;
            else if (hitItem != null && hitItem != Held && hitItem.kind != TestItem.Kind.Pipette)
                Current = Held.kind == TestItem.Kind.Pipette ? Act.Pipette : Act.Pour;
            else Current = hitTable ? Act.Place : Act.None;
            outline.SetTarget(Held == null && hitItem != null ? hitItem.transform : Current == Act.Pour || Current == Act.Pipette ? hitItem.transform : null);
        }

        void Act_(float dt)
        {
            if (Held == null)
            {
                if (BPressed && hitItem != null) Grab(hitItem);
                Tilt = 0;
                stream.gameObject.SetActive(false);
                return;
            }
            if (BReleased)
            {
                if (Current == Act.Place) Place(Held, hitPoint);
                else
                {
                    Toast($"Đã trả {Held.label} về chỗ cũ (buông B khi trỏ vào mặt bàn để đặt xuống)");
                    Place(Held, Held.home);
                }
                return;
            }

            bool pouring = false;
            Vector3 targetPos;
            Quaternion targetRot = Quaternion.identity;
            var T = hitItem;
            if (Current == Act.Pour)
            {
                // nghiêng theo lăn cổ tay (lăn 75° = nghiêng 105°), hoặc giữ A
                float want = Mathf.Clamp(Mathf.Abs(Roll) * 1.4f, 0f, 110f);
                if (aNow) want = Mathf.Max(want, 100f);
                Tilt = Mathf.MoveTowards(Tilt, want, 220f * dt);
                float side = Held.home.x < T.transform.position.x ? -1f : 1f;
                float k = Mathf.Clamp01(Tilt / 90f);
                targetRot = Quaternion.AngleAxis(side * Tilt, Vector3.forward);
                Vector3 mouthT = T.MouthWorld;
                Vector3 want3 = mouthT + Vector3.up * (0.025f + 0.11f * (1 - k)) - Vector3.right * side * (0.085f * (1 - k) + 0.012f);
                targetPos = want3 - targetRot * Held.mouthLocal;
                float rate = 14f * Mathf.Clamp01((Tilt - 40f) / 50f);
                if (rate > 0 && Held.volumeMl > 0.01f && T.volumeMl < T.capMl)
                {
                    float ml = Mathf.Min(rate * dt, Held.volumeMl, T.capMl - T.volumeMl);
                    TestItem.Transfer(Held, T, ml);
                    PouredMl += ml;
                    pouring = true;
                    if (T.kind == TestItem.Kind.Beaker && PouredMl >= 10f) Complete(2, "Rót từ lọ sang cốc");
                }
                if (pouring) ShowStream(Held.MouthWorldAt(targetPos, targetRot), T.SurfaceWorld, Held.color);
            }
            else if (Current == Act.Pipette)
            {
                Tilt = 0;
                if (APressed) pipetteSuck = Held.volumeMl < 0.05f * Held.capMl;
                float tipY;
                if (aNow && pipetteSuck) tipY = Mathf.Max(T.SurfaceWorld.y - 0.012f, T.transform.position.y + T.innerBottom + 0.004f);
                else tipY = T.MouthWorld.y - 0.02f;
                targetPos = new Vector3(T.transform.position.x, tipY, T.transform.position.z);
                if (aNow)
                {
                    if (pipetteSuck && Held.volumeMl < Held.capMl && T.volumeMl > 0.01f && targetPos.y <= T.SurfaceWorld.y)
                    {
                        float ml = Mathf.Min(2.2f * dt, Held.capMl - Held.volumeMl, T.volumeMl);
                        TestItem.Transfer(T, Held, ml);
                        SuckedMl += ml;
                    }
                    else if (!pipetteSuck && Held.volumeMl > 0.001f && T.volumeMl < T.capMl)
                    {
                        float ml = Mathf.Min(2.8f * dt, Held.volumeMl, T.capMl - T.volumeMl);
                        TestItem.Transfer(Held, T, ml);
                        ReleasedMl += ml;
                        pouring = true;
                        ShowStream(targetPos, T.SurfaceWorld, Held.color, 0.0012f);
                    }
                    if (SuckedMl >= 1f && ReleasedMl >= 1f) Complete(3, "Hút và nhả bằng pipet");
                }
            }
            else
            {
                Tilt = Mathf.MoveTowards(Tilt, 0, 300f * dt);
                if (Current == Act.Place) targetPos = hitPoint + Vector3.up * 0.025f;
                else targetPos = AimRay.GetPoint(Mathf.Clamp(hitDist - 0.1f, 0.35f, 0.7f)) - Vector3.up * Held.height * 0.5f;
            }
            if (!pouring) stream.gameObject.SetActive(false);

            // đổi chỗ (bàn → miệng cốc) chuyển mượt 60 ms; trong một chỗ thì đi thẳng theo tia, không trễ
            if (!anchorValid) { anchor = targetPos; anchorRot = targetRot; anchorValid = true; }
            float follow = 1f - Mathf.Exp(-dt / 0.06f);
            anchor = Vector3.Lerp(anchor, targetPos, follow);
            anchorRot = Quaternion.Slerp(anchorRot, targetRot, follow);
            Held.transform.SetPositionAndRotation(anchor, anchorRot);
        }

        void ShowStream(Vector3 from, Vector3 to, Color c, float r = 0.0028f)
        {
            stream.gameObject.SetActive(true);
            Vector3 d = to - from;
            stream.position = (from + to) * 0.5f;
            stream.up = d.sqrMagnitude > 1e-8f ? d.normalized : Vector3.up;
            stream.localScale = new Vector3(r * 2, d.magnitude * 0.5f, r * 2);
            streamMat.SetColor("_BaseColor", new Color(c.r, c.g, c.b, Mathf.Max(0.55f, c.a)));
            streamMat.color = new Color(c.r, c.g, c.b, Mathf.Max(0.55f, c.a));
        }

        void Grab(TestItem it)
        {
            Held = it;
            anchorValid = false;
            foreach (var c in it.GetComponentsInChildren<Collider>()) c.enabled = false;
            PadInput.Pulse(140, 40);
            Toast(it.kind == TestItem.Kind.Pipette ? "Đã cầm pipet: trỏ vào lọ, giữ A để hút; trỏ vào cốc, giữ A để nhả"
                : "Đã cầm " + it.label + ": trỏ vào cốc rồi lăn cổ tay (hoặc giữ A) để rót");
        }

        void Place(TestItem it, Vector3 p)
        {
            p.y = TableY + (it.kind == TestItem.Kind.Pipette ? 0.035f : 0f);
            // tránh đặt chồng lên vật khác
            foreach (var o in Items)
            {
                if (o == it) continue;
                Vector3 d = p - o.transform.position;
                d.y = 0;
                float min = o.radius + it.radius + 0.008f;
                if (d.magnitude < min) p += (d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.right) * (min - d.magnitude);
            }
            it.transform.SetPositionAndRotation(p, Quaternion.identity);
            foreach (var c in it.GetComponentsInChildren<Collider>()) c.enabled = true;
            Held = null;
            Tilt = 0;
            stream.gameObject.SetActive(false);
            PadInput.Pulse(90, 30);
        }

        void PoseHand()
        {
            Vector3 camPos = Cam.transform.position;
            Vector3 tipTarget = hitPoint;
            float curl;
            if (Held != null)
            {
                // nắm vật đang cầm ở bên phải thân
                var t = Held.transform;
                Vector3 grip = t.TransformPoint(new Vector3(Held.radius + 0.012f, Held.kind == TestItem.Kind.Pipette ? 0.2f : Held.height * 0.5f, -0.005f));
                hand.position = grip + t.right * 0.0f;
                hand.rotation = t.rotation * Quaternion.LookRotation(Vector3.forward, Vector3.right);
                curl = 62f;
            }
            else
            {
                // tay lơ lửng trước điểm trỏ, lệch xuống dưới bên phải để không che chỗ đang trỏ
                float d = Mathf.Clamp(hitDist - 0.16f, 0.3f, 0.75f);
                Vector3 p = AimRay.GetPoint(d) + Cam.transform.right * 0.045f - Cam.transform.up * 0.055f;
                hand.position = p;
                Vector3 dir = tipTarget - p;
                hand.rotation = Quaternion.LookRotation(dir.sqrMagnitude > 1e-6f ? dir : Cam.transform.forward, Vector3.up)
                                * Quaternion.Euler(0, 0, -Mathf.Clamp(Roll, -90f, 90f));
                curl = 0f;
            }
            for (int i = 0; i < fingers.Count; i++)
            {
                float c = i == 0 && Held == null ? 0f : (i == 4 ? curl * 0.4f : Held == null ? 75f : curl);
                if (i == 4) fingers[i].localRotation = Quaternion.Euler(c, 40, 0);
                else fingers[i].localRotation = Quaternion.Euler(c, 0, 0);
            }
            // tia: từ đầu ngón trỏ tới điểm trỏ
            Vector3 from = Held != null ? hand.position : handTip.position;
            laser.SetPosition(0, from);
            laser.SetPosition(1, hitPoint);
            laser.widthMultiplier = Held != null ? 0.6f : 1f;
            dot.position = hitPoint;
            dot.localScale = Vector3.one * Mathf.Clamp(hitDist * 0.012f, 0.006f, 0.016f);
        }

        void UpdatePointerTasks(float dt)
        {
            // vệt đường đi 0,6 s
            trailPts.Enqueue(hitPoint);
            while (trailPts.Count > 48) trailPts.Dequeue();
            trail.positionCount = trailPts.Count;
            int i = 0;
            foreach (var p in trailPts) trail.SetPosition(i++, p);

            // bài 1: lia qua lại (hướng tia vượt ±14° so với giữa bàn, đổi bên 4 lần)
            int side = AimDeg.x > 14f ? 1 : AimDeg.x < -14f ? -1 : 0;
            if (side != 0 && side != sweepSide)
            {
                if (sweepSide != 0) Sweeps++;
                sweepSide = side;
                if (Sweeps >= 4) Complete(0, "Lia tay qua lại");
            }

            // bài 2: chỉ trúng 3 bia (giữ tia trên bia 0,35 s)
            for (int k = 0; k < Targets.Count; k++)
            {
                bool on = hitTarget == Targets[k] && Held == null;
                targetDwell[k] = on ? targetDwell[k] + dt : 0f;
                if (on && !targetDone[k] && targetDwell[k] >= 0.35f)
                {
                    targetDone[k] = true;
                    TargetsHit++;
                    PadInput.Pulse(160, 50);
                    Toast($"Trúng bia {k + 1}");
                    if (TargetsHit >= 3) Complete(1, "Chỉ trúng 3 bia");
                }
                var r = Targets[k].GetComponent<Renderer>();
                Color c = targetDone[k] ? new Color(0.35f, 0.9f, 0.5f) : on ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.55f, 0.2f);
                r.material.SetColor("_BaseColor", c);
                r.material.color = c;
            }
        }

        void Complete(int i, string what)
        {
            if (Done[i]) return;
            Done[i] = true;
            PadInput.Pulse(200, 80);
            int n = 0;
            foreach (var d in Done) if (d) n++;
            Toast(n == Done.Length ? "Hoàn thành cả 5 bài thử. Tay cầm sẵn sàng vào lab!" : $"Xong bài: {what} ({n}/5)");
        }

        void UpdateStats()
        {
            rateTimer += Time.unscaledDeltaTime;
            if (rateTimer >= 1f)
            {
                SamplesPerSecond = Mathf.RoundToInt(sampleCount / rateTimer);
                sampleCount = 0;
                rateTimer = 0;
            }
            // rung khi giữ yên: độ lệch chuẩn hướng tia trong 0,6 s gần nhất, chỉ tính khi tay gần như đứng yên
            float now = Time.realtimeSinceStartup;
            history.Enqueue((now, AimDeg));
            while (history.Count > 0 && now - history.Peek().t > 0.6f) history.Dequeue();
            if (history.Count < 8) return;
            Vector2 mean = Vector2.zero;
            foreach (var h in history) mean += h.a;
            mean /= history.Count;
            float var = 0;
            foreach (var h in history) var += (h.a - mean).sqrMagnitude;
            float span = 0;
            (float t, Vector2 a) first = default, last = default;
            bool f = true;
            foreach (var h in history) { if (f) { first = h; f = false; } last = h; }
            span = last.t - first.t;
            SpeedDps = span > 0 ? (last.a - first.a).magnitude / span : 0;
            if (SpeedDps < 4f) JitterDeg = Mathf.Sqrt(var / history.Count);
        }

        void ResetRoom()
        {
            if (Held != null) Place(Held, Held.home);
            foreach (var it in Items)
            {
                it.transform.SetPositionAndRotation(it.home, Quaternion.identity);
                it.volumeMl = 0;
            }
            Items[0].Fill(Solutions[1], 140f);
            Items[1].Fill(Solutions[2], 140f);
            foreach (var it in Items) it.UpdateLiquid();
            for (int i = 0; i < Done.Length; i++) Done[i] = false;
            for (int i = 0; i < 3; i++) targetDone[i] = false;
            TargetsHit = Sweeps = 0;
            PouredMl = SuckedMl = ReleasedMl = 0;
            Toast("Đã làm lại từ đầu");
        }
    }

    /// <summary>Mặt bàn hoặc bảng bia (để biết tia đang trỏ chỗ đặt được hay không).</summary>
    public class TestTable : MonoBehaviour
    {
        public bool isBoard;
    }

    /// <summary>Lọ, cốc, pipet của phòng thử: thể tích, màu dung dịch (trộn theo tỉ lệ thể tích), mặt chất lỏng.</summary>
    public class TestItem : MonoBehaviour
    {
        public enum Kind { Bottle, Beaker, Pipette }
        public Kind kind;
        public string label;
        public string solution = "";
        public float volumeMl, capMl;
        public Color color = new Color(0.8f, 0.9f, 1f, 0.3f);
        public Vector3 home, mouthLocal;
        public float radius, height, innerRadius, innerBottom, innerHeight;
        public Transform liquid;
        public Material liquidMat;

        public Vector3 MouthWorld => transform.TransformPoint(mouthLocal);
        public Vector3 MouthWorldAt(Vector3 pos, Quaternion rot) => pos + rot * mouthLocal;
        public float Fill01 => capMl > 0 ? Mathf.Clamp01(volumeMl / capMl) : 0;
        public Vector3 SurfaceWorld => transform.TransformPoint(new Vector3(0, innerBottom + innerHeight * Fill01, 0));

        public void Fill(ControllerTestRoom.Solution s, float ml)
        {
            solution = s.name;
            color = s.color;
            volumeMl = Mathf.Min(ml, capMl);
            UpdateLiquid();
        }

        public static void Transfer(TestItem from, TestItem to, float ml)
        {
            if (ml <= 0) return;
            float before = to.volumeMl;
            to.color = before <= 0.001f ? from.color : Color.Lerp(to.color, from.color, ml / (before + ml));
            if (before <= 0.001f || to.solution == from.solution) to.solution = from.solution;
            else if (!to.solution.StartsWith("Hỗn hợp")) to.solution = "Hỗn hợp " + to.solution + " + " + from.solution;
            to.volumeMl = Mathf.Min(to.capMl, before + ml);
            from.volumeMl = Mathf.Max(0, from.volumeMl - ml);
            if (from.volumeMl <= 0.001f) from.solution = "";
            from.UpdateLiquid();
            to.UpdateLiquid();
        }

        public void UpdateLiquid()
        {
            if (liquid == null) return;
            float f = Fill01;
            liquid.gameObject.SetActive(f > 0.002f);
            float h = Mathf.Max(0.001f, innerHeight * f);
            liquid.localPosition = new Vector3(0, innerBottom + h * 0.5f, 0);
            liquid.localScale = new Vector3(innerRadius * 2, h * 0.5f, innerRadius * 2);
            if (liquidMat != null)
            {
                var c = new Color(color.r, color.g, color.b, Mathf.Max(0.35f, color.a));
                liquidMat.SetColor("_BaseColor", c);
                liquidMat.color = c;
            }
        }
    }
}
