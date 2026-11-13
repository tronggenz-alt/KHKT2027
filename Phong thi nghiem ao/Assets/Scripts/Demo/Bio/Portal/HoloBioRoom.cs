using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Khkt.Demo;
using Khkt.Environment;
using Khkt.Rendering;
using Khkt.Input;

namespace Khkt.Demo.Bio.Portal
{
    /// <summary>
    /// HoloBioRoom (Phòng ảo Sinh học 3D - GĐ2, GĐ3, GĐ4):
    ///   • Gốc phòng đặt tại y = -600, mặc định SetActive(false).
    ///   • Camera riêng cho phòng ảo (holoCamera), không có AudioListener thứ hai.
    ///   • Sàn tròn bán kính 10m, nền tối #0A1222, lưới 20x20m (#06B6D4 / #0E3A4A, alpha 0.28).
    ///   • Bệ hologram hình trụ (bán kính 1.05m -> 1.2m, cao 0.14m).
    ///   • 4 vòng sáng, chùm sáng trụ hở cao 3.9m, hệ hạt bụi sáng <= 150 hạt.
    ///   • Mô hình BodyParts3D 1.7m: chuyển đổi 3 lớp Da / Cơ / Xương (hoạt cảnh tan - hiện 0.35s).
    ///   • 15 cơ quan giải phẫu: Tim, Phổi, Não, Gan, Thận, Dạ dày, Ruột non, Ruột già, Cơ, Xương, Da, Mạch máu, Tủy sống, Lách, Tụy.
    ///   • Tương tác chạm vào cơ quan hoặc chọn từ danh sách -> camera lia mượt, sáng viền cơ quan, mở OrganKnowledgeCard.
    ///   • Hoạt cảnh sinh lý: Tim đập 72 lần/phút (chu kỳ 0.833s), Phổi thở 16 lần/phút (chu kỳ 3.75s).
    ///   • Thanh công cụ bên phải: Đặt lại camera, Soi hiển vi, Bật/tắt nhãn, Ôn tập.
    ///   • 6 Cảnh hiển vi tế bào / hóa sinh (MicroSceneViewer).
    ///   • Dòng ghi công bắt buộc luôn nhìn thấy: "BodyParts3D © The Database Center for Life Science · CC BY 4.0".
    /// </summary>
    public class HoloBioRoom : MonoBehaviour
    {
        [Serializable]
        public class OrganTarget
        {
            public string id;
            public string name;
            public string icon;
            public Vector3 localPos;
            public float focusDist;
            public float pitch;
            public float yaw;
            public string microScene;
        }

        [Header("Camera phòng ảo")]
        public Camera holoCamera;
        public Transform orbitTarget;
        public float cameraDistance = 2.2f;
        public float minDistance = 0.8f;
        public float maxDistance = 4.0f;
        public float defaultDistance = 2.2f;
        public float defaultYaw = 0f;
        public float defaultPitch = 10f;

        [Header("Bệ và Vòng sáng")]
        public Transform ring3Torus; // cao 0.6m
        public Transform ring4Torus; // cao 2.4m
        public float ringRotateSpeed = 25f;

        [Header("Lớp cơ thể BodyParts3D")]
        public GameObject layerSkeleton;
        public GameObject layerMuscles;
        public GameObject layerSkin;
        public string activeLayer = "da"; // "da", "co", "xuong"

        [Header("Sinh lý học hoạt cảnh")]
        public Transform heartTransform;
        public Transform lungTransform;

        [Header("Bộ phận & Thẻ kiến thức (GĐ4)")]
        public OrganKnowledgeCard knowledgeCard;
        public MicroSceneViewer microViewer;
        public Transform organLabelsRoot;
        public Transform organHighlightRing;

        [Header("Giao diện & Chuyển cảnh")]
        public Canvas holoCanvas;
        public CanvasGroup crossfadeGroup;
        public RectTransform exitButtonRect;
        public Text exitButtonText;
        public Text creditText;
        public RectTransform toolbarPanel;
        public RectTransform layerBarPanel;
        public RectTransform organListDrawer;

        // Trạng thái Orbit Camera
        float currentYaw = 0f;
        float currentPitch = 10f;
        Vector2 lastPointerPos;
        bool isDraggingPointer = false;
        float lastTapTime = -1f;
        const float DoubleTapThreshold = 0.35f;

        // Lưu trạng thái người chơi để phục hồi chính xác (sai số <= 1cm, <= 0.5°)
        DesktopViewer savedViewer;
        Camera savedViewCam;
        Vector3 savedPosition;
        Quaternion savedRotation;
        Quaternion savedCamLocalRotation;
        float savedFov = 60f;
        Vector3 savedPortalPos;

        bool isTransitioning = false;
        bool isFocusingOrgan = false;
        Coroutine focusCoroutine;
        Coroutine layerCoroutine;

        // Danh sách 15 cơ quan giải phẫu chuẩn
        public static readonly List<OrganTarget> Organs = new List<OrganTarget>
        {
            new OrganTarget { id = "tim", name = "Tim", icon = "🫀", localPos = new Vector3(-0.03f, 1.32f, 0.05f), focusDist = 0.85f, pitch = 8f, yaw = 0f, microScene = "sarcomere" },
            new OrganTarget { id = "phoi", name = "Phổi và đường dẫn khí", icon = "🫁", localPos = new Vector3(0f, 1.34f, 0.04f), focusDist = 0.95f, pitch = 8f, yaw = 0f, microScene = "pho_nang" },
            new OrganTarget { id = "nao", name = "Não bộ", icon = "🧠", localPos = new Vector3(0f, 1.68f, 0f), focusDist = 0.85f, pitch = 6f, yaw = 0f, microScene = "synapse" },
            new OrganTarget { id = "gan", name = "Gan và túi mật", icon = "🟤", localPos = new Vector3(-0.07f, 1.16f, 0.05f), focusDist = 0.85f, pitch = 5f, yaw = 0f, microScene = "tieu_thuy_gan" },
            new OrganTarget { id = "than", name = "Thận và đường tiết niệu", icon = "🫘", localPos = new Vector3(0f, 1.12f, -0.04f), focusDist = 0.90f, pitch = 5f, yaw = 160f, microScene = "nephron" },
            new OrganTarget { id = "da_day", name = "Thực quản và dạ dày", icon = "🟠", localPos = new Vector3(0.04f, 1.18f, 0.05f), focusDist = 0.85f, pitch = 5f, yaw = 0f, microScene = "nhung_mao" },
            new OrganTarget { id = "ruot_non", name = "Ruột non", icon = "🌀", localPos = new Vector3(0f, 1.02f, 0.05f), focusDist = 0.90f, pitch = 5f, yaw = 0f, microScene = "nhung_mao" },
            new OrganTarget { id = "ruot_gia", name = "Ruột già", icon = "🟫", localPos = new Vector3(0f, 1.02f, 0.06f), focusDist = 0.95f, pitch = 5f, yaw = 0f, microScene = "nhung_mao" },
            new OrganTarget { id = "co", name = "Hệ cơ", icon = "💪", localPos = new Vector3(0f, 1.15f, 0f), focusDist = 1.8f, pitch = 10f, yaw = 0f, microScene = "sarcomere" },
            new OrganTarget { id = "xuong", name = "Bộ xương", icon = "🦴", localPos = new Vector3(0f, 1.15f, 0f), focusDist = 1.8f, pitch = 10f, yaw = 0f, microScene = "" },
            new OrganTarget { id = "da", name = "Da", icon = "🖐️", localPos = new Vector3(0f, 1.15f, 0f), focusDist = 1.8f, pitch = 10f, yaw = 0f, microScene = "" },
            new OrganTarget { id = "mach_mau", name = "Hệ mạch máu", icon = "🩸", localPos = new Vector3(0f, 1.25f, 0.02f), focusDist = 1.4f, pitch = 6f, yaw = 0f, microScene = "pho_nang" },
            new OrganTarget { id = "tuy_song", name = "Tuỷ sống và dây TK", icon = "⚡", localPos = new Vector3(0f, 1.25f, -0.06f), focusDist = 1.1f, pitch = 8f, yaw = 180f, microScene = "synapse" },
            new OrganTarget { id = "lach", name = "Lách", icon = "🛡️", localPos = new Vector3(0.12f, 1.16f, 0.01f), focusDist = 0.85f, pitch = 5f, yaw = 0f, microScene = "tieu_thuy_gan" },
            new OrganTarget { id = "tuy", name = "Tuyến tuỵ", icon = "🟡", localPos = new Vector3(0.01f, 1.12f, 0.02f), focusDist = 0.85f, pitch = 5f, yaw = 0f, microScene = "tieu_thuy_gan" }
        };

        private OrganTarget selectedOrgan = null;
        private readonly List<Transform> organLabelTransforms = new List<Transform>();
        private readonly List<Collider> organColliders = new List<Collider>();

        void Awake()
        {
            EnsureRoomHierarchy();
        }

        void Start()
        {
            if (transform.position.y > -500f)
            {
                transform.position = new Vector3(transform.position.x, -600f, transform.position.z);
            }
            // Start chạy một khung hình SAU lần bật đầu tiên. Bản cũ tắt phòng vô điều kiện ở đây, nên lần đầu bước qua cổng
            // phòng vừa bật đã tự tắt (màn hình đứng hình, phải vào lần hai mới thấy mô hình). Chỉ tắt khi phòng bị để bật
            // sẵn trong cảnh mà không ai đang vào.
            var transition = BioPortalTransition.Instance;
            bool entering = isTransitioning || IsPrewarming ||
                            (transition != null && transition.CurrentState != BioPortalTransition.TransitionState.Nghi);
            if (!entering) gameObject.SetActive(false);
        }

        // =====================================================================
        // Nạp trước (để lần mở đầu tiên không khựng)
        // =====================================================================

        public bool IsPrewarmed { get; private set; }
        public bool IsPrewarming { get; private set; }

        /// <summary>
        /// Nạp trước phòng khi người dùng còn đang đi tới cổng, chia ra nhiều khung hình:
        ///   khung 1: bật phòng (dựng camera, sàn, giao diện, nút cơ quan) nhưng ẩn giao diện và không vẽ ra màn hình;
        ///   khung 2–4: vẽ từng lớp Da, Cơ, Xương một lần vào ảnh nhỏ 64 px để máy đưa lưới (~250 nghìn đỉnh) lên GPU
        ///              và biên dịch sẵn shader;
        ///   khung 5: đọc sẵn 15 tệp kiến thức, rồi tắt phòng.
        /// Bản cũ dồn tất cả vào đúng khung hình chuyển cảnh nên hiệu ứng mở cổng bị khựng.
        /// Chạy bằng coroutine của một đối tượng khác (BioPortal) vì phòng tự tắt ở cuối.
        /// </summary>
        public IEnumerator CoPrewarm()
        {
            if (IsPrewarmed || IsPrewarming || gameObject.activeSelf) yield break;
            IsPrewarming = true;
            gameObject.SetActive(true);                       // Awake: EnsureRoomHierarchy
            var canvases = GetComponentsInChildren<Canvas>(true);
            var canvasWasOn = new bool[canvases.Length];
            for (int i = 0; i < canvases.Length; i++) { canvasWasOn[i] = canvases[i].enabled; canvases[i].enabled = false; }
            if (holoCamera != null) holoCamera.enabled = false;
            yield return null;                                // Start

            if (holoCamera != null && !isTransitioning)
            {
                var rt = RenderTexture.GetTemporary(64, 64, 16);
                var layers = new[] { layerSkin, layerMuscles, layerSkeleton };
                ResetOrbitView();
                UpdateOrbitCamera();
                foreach (var layer in layers)
                {
                    if (layer == null) continue;
                    if (isTransitioning) break;               // người dùng đã bấm vào phòng: nhường cho chuyển cảnh
                    bool was = layer.activeSelf;
                    layer.SetActive(true);
                    holoCamera.targetTexture = rt;
                    holoCamera.Render();
                    holoCamera.targetTexture = null;
                    layer.SetActive(was);
                    yield return null;
                }
                holoCamera.targetTexture = null;
                RenderTexture.ReleaseTemporary(rt);
            }

            OrganKnowledgeCard.PreloadAllData();
            yield return null;

            for (int i = 0; i < canvases.Length; i++) if (canvases[i] != null) canvases[i].enabled = canvasWasOn[i];
            IsPrewarming = false;
            IsPrewarmed = true;
            // nếu trong lúc nạp người dùng đã vào phòng thì để nguyên
            var transition = BioPortalTransition.Instance;
            bool entering = isTransitioning || (transition != null && transition.CurrentState != BioPortalTransition.TransitionState.Nghi);
            if (!entering) gameObject.SetActive(false);
        }

        void Update()
        {
            if (IsPrewarming) return;   // đang nạp trước ở hậu trường: chưa nhận điều khiển
            float dt = Time.deltaTime;

            // 1. Quay 2 vòng xuyến ngược chiều nhau
            if (ring3Torus != null) ring3Torus.Rotate(Vector3.up, ringRotateSpeed * dt, Space.World);
            if (ring4Torus != null) ring4Torus.Rotate(Vector3.up, -ringRotateSpeed * dt, Space.World);

            // 2. Hoạt cảnh sinh lý học: Tim đập 72 bpm (chu kỳ 0.833s), Phổi thở 16 bpm (chu kỳ 3.75s)
            UpdatePhysiologyAnimations();

            // 3. Xử lý điều khiển Orbit Camera & Chạm chọn cơ quan 3D
            if (!isFocusingOrgan)
            {
                HandleOrbitInput();
            }
            UpdateOrbitCamera();

            // 4. Billboard toàn bộ nhãn 3D về phía holoCamera
            Update3DLabelsBillboard();

            // 5. Phím tắt Esc trên PC để về phòng thí nghiệm hoặc đóng popup
            if (!isTransitioning && !PadInput.UsingTouch)
            {
                var kb = Keyboard.current;
                if (kb != null && kb.escapeKey.wasPressedThisFrame)
                {
                    if (microViewer != null && microViewer.gameObject.activeInHierarchy)
                    {
                        microViewer.ReturnToFullBody();
                    }
                    else if (knowledgeCard != null && knowledgeCard.cardPanel.gameObject.activeSelf)
                    {
                        knowledgeCard.HideCard();
                    }
                    else
                    {
                        ReturnToLab();
                    }
                }
            }
        }

        // =====================================================================
        // Khởi tạo hình học & môi trường phòng ảo
        // =====================================================================

        public void EnsureRoomHierarchy()
        {
            EnsureCamera();
            EnsureHoloPlatform();
            EnsureUI();
            EnsurePhysiologyNodes();
            EnsureOrganInteractiveNodes();
            EnsureKnowledgeCardAndMicroViewer();
        }

        void EnsureCamera()
        {
            if (holoCamera != null) return;

            var camTrans = transform.Find("HoloCamera");
            if (camTrans != null)
            {
                holoCamera = camTrans.GetComponent<Camera>();
            }
            else
            {
                var camGo = new GameObject("HoloCamera");
                camGo.transform.SetParent(transform, false);
                holoCamera = camGo.AddComponent<Camera>();
                holoCamera.clearFlags = CameraClearFlags.SolidColor;
                holoCamera.backgroundColor = new Color(10f / 255f, 18f / 255f, 34f / 255f, 1f);
                holoCamera.fieldOfView = 55f;
                holoCamera.nearClipPlane = 0.05f;
                holoCamera.farClipPlane = 100f;
            }

            var listener = holoCamera.GetComponent<AudioListener>();
            if (listener != null) Destroy(listener);

            holoCamera.enabled = false;
        }

        void EnsureHoloPlatform()
        {
            var floorTrans = transform.Find("HoloFloor");
            if (floorTrans == null)
            {
                var floorGo = new GameObject("HoloFloor");
                floorGo.transform.SetParent(transform, false);
                floorGo.transform.localPosition = Vector3.zero;

                var mf = floorGo.AddComponent<MeshFilter>();
                mf.sharedMesh = BuildDiscMesh(10f, 64);

                var mr = floorGo.AddComponent<MeshRenderer>();
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.lightProbeUsage = LightProbeUsage.Off;
                mr.reflectionProbeUsage = ReflectionProbeUsage.Off;

                var floorMat = CreateFloorMaterial();
                mr.sharedMaterial = floorMat;
            }

            var podiumTrans = transform.Find("HoloPodium");
            if (podiumTrans == null)
            {
                var podiumGo = new GameObject("HoloPodium");
                podiumGo.transform.SetParent(transform, false);
                podiumGo.transform.localPosition = new Vector3(0f, 0.07f, 0f);

                var mf = podiumGo.AddComponent<MeshFilter>();
                mf.sharedMesh = BuildFrustumMesh(1.20f, 1.05f, 0.14f, 48);

                var mr = podiumGo.AddComponent<MeshRenderer>();
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.sharedMaterial = CreatePodiumMaterial();
            }

            EnsureGlowingRing("Vong1_SatBe", 0.98f, 1.05f, 0.142f, new Color(34f / 255f, 211f / 255f, 238f / 255f));
            EnsureHexagonRing("Vong2_LucGiac", 0.35f, 0.03f, 0.143f, new Color(16f / 255f, 185f / 255f, 129f / 255f));

            if (ring3Torus == null)
            {
                var r3 = EnsureGlowingRing("Vong3_Torus_0.6m", 0.94f, 1.02f, 0.14f + 0.60f, new Color(34f / 255f, 211f / 255f, 238f / 255f, 0.85f));
                ring3Torus = r3.transform;
            }

            if (ring4Torus == null)
            {
                var r4 = EnsureGlowingRing("Vong4_Torus_2.4m", 0.88f, 0.95f, 0.14f + 2.40f, new Color(16f / 255f, 185f / 255f, 129f / 255f, 0.85f));
                ring4Torus = r4.transform;
            }

            var beamTrans = transform.Find("HoloLightBeam");
            if (beamTrans == null)
            {
                var beamGo = new GameObject("HoloLightBeam");
                beamGo.transform.SetParent(transform, false);
                beamGo.transform.localPosition = new Vector3(0f, 0.14f, 0f);

                var mf = beamGo.AddComponent<MeshFilter>();
                mf.sharedMesh = BuildOpenCylinderMesh(1.05f, 3.9f, 48);

                var mr = beamGo.AddComponent<MeshRenderer>();
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.sharedMaterial = CreateBeamMaterial();
            }

            var dustTrans = transform.Find("HoloDustParticles");
            if (dustTrans == null)
            {
                var dustGo = new GameObject("HoloDustParticles");
                dustGo.transform.SetParent(transform, false);
                dustGo.transform.localPosition = new Vector3(0f, 0.2f, 0f);

                var ps = dustGo.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.maxParticles = 150;
                main.startLifetime = 3.5f;
                main.startSpeed = 0.18f;
                main.startSize = 0.025f;
                main.startColor = new Color(34f / 255f, 211f / 255f, 238f / 255f, 0.65f);
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.playOnAwake = true;

                var emission = ps.emission;
                emission.rateOverTime = 38f;

                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 0.95f;
                shape.position = new Vector3(0f, 0f, 0f);
                shape.rotation = new Vector3(-90f, 0f, 0f);

                var vel = ps.velocityOverLifetime;
                vel.enabled = true;
                vel.y = new ParticleSystem.MinMaxCurve(0.1f, 0.25f);

                var psr = dustGo.GetComponent<ParticleSystemRenderer>();
                psr.shadowCastingMode = ShadowCastingMode.Off;
                psr.receiveShadows = false;
            }

            if (orbitTarget == null)
            {
                var targetTrans = transform.Find("OrbitTarget");
                if (targetTrans == null)
                {
                    var targetGo = new GameObject("OrbitTarget");
                    targetGo.transform.SetParent(transform, false);
                    targetGo.transform.localPosition = new Vector3(0f, 1.15f, 0f);
                    orbitTarget = targetGo.transform;
                }
                else
                {
                    orbitTarget = targetTrans;
                }
            }
        }

        // =====================================================================
        // Khởi tạo Sinh lý & Các cơ quan tương tác (GĐ4)
        // =====================================================================

        void EnsurePhysiologyNodes()
        {
            if (heartTransform == null)
            {
                var h = transform.Find("HeartNode");
                if (h == null)
                {
                    var go = new GameObject("HeartNode");
                    go.transform.SetParent(transform, false);
                    go.transform.localPosition = new Vector3(-0.03f, 1.32f, 0.05f);

                    var sph = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    sph.name = "HeartMeshVisual";
                    sph.transform.SetParent(go.transform, false);
                    sph.transform.localScale = new Vector3(0.14f, 0.17f, 0.13f);
                    sph.GetComponent<MeshRenderer>().sharedMaterial = CreateUnlitAdditiveMaterial(new Color(239f / 255f, 68f / 255f, 68f / 255f, 0.45f));
                    if (sph.GetComponent<Collider>() != null) Destroy(sph.GetComponent<Collider>());

                    heartTransform = go.transform;
                }
                else
                {
                    heartTransform = h;
                }
            }

            if (lungTransform == null)
            {
                var l = transform.Find("LungNode");
                if (l == null)
                {
                    var go = new GameObject("LungNode");
                    go.transform.SetParent(transform, false);
                    go.transform.localPosition = new Vector3(0f, 1.34f, 0.04f);

                    var lLeft = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    lLeft.name = "LungLeftVisual";
                    lLeft.transform.SetParent(go.transform, false);
                    lLeft.transform.localPosition = new Vector3(-0.10f, 0f, 0f);
                    lLeft.transform.localScale = new Vector3(0.12f, 0.22f, 0.12f);
                    lLeft.GetComponent<MeshRenderer>().sharedMaterial = CreateUnlitAdditiveMaterial(new Color(244f / 255f, 114f / 255f, 182f / 255f, 0.28f));
                    if (lLeft.GetComponent<Collider>() != null) Destroy(lLeft.GetComponent<Collider>());

                    var lRight = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    lRight.name = "LungRightVisual";
                    lRight.transform.SetParent(go.transform, false);
                    lRight.transform.localPosition = new Vector3(0.10f, 0f, 0f);
                    lRight.transform.localScale = new Vector3(0.12f, 0.22f, 0.12f);
                    lRight.GetComponent<MeshRenderer>().sharedMaterial = CreateUnlitAdditiveMaterial(new Color(244f / 255f, 114f / 255f, 182f / 255f, 0.28f));
                    if (lRight.GetComponent<Collider>() != null) Destroy(lRight.GetComponent<Collider>());

                    lungTransform = go.transform;
                }
                else
                {
                    lungTransform = l;
                }
            }
        }

        void EnsureOrganInteractiveNodes()
        {
            if (organLabelsRoot == null)
            {
                var r = transform.Find("OrganLabelsRoot");
                if (r == null)
                {
                    var go = new GameObject("OrganLabelsRoot");
                    go.transform.SetParent(transform, false);
                    organLabelsRoot = go.transform;
                }
                else
                {
                    organLabelsRoot = r;
                }
            }

            if (organHighlightRing == null)
            {
                var ringTrans = transform.Find("OrganHighlightRing");
                if (ringTrans == null)
                {
                    var rGo = new GameObject("OrganHighlightRing");
                    rGo.transform.SetParent(transform, false);
                    rGo.transform.localPosition = new Vector3(0f, 1.2f, 0f);

                    var mf = rGo.AddComponent<MeshFilter>();
                    mf.sharedMesh = BuildRingMesh(0.14f, 0.17f, 32);

                    var mr = rGo.AddComponent<MeshRenderer>();
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    mr.sharedMaterial = CreateUnlitAdditiveMaterial(new Color(34f / 255f, 211f / 255f, 238f / 255f, 0.85f));

                    organHighlightRing = rGo.transform;
                    organHighlightRing.gameObject.SetActive(false);
                }
                else
                {
                    organHighlightRing = ringTrans;
                }
            }

            organLabelTransforms.Clear();
            organColliders.Clear();

            for (int i = 0; i < Organs.Count; i++)
            {
                var o = Organs[i];
                var nodeName = "OrganNode_" + o.id;
                var node = organLabelsRoot.Find(nodeName);
                if (node == null)
                {
                    var nodeGo = new GameObject(nodeName);
                    nodeGo.transform.SetParent(organLabelsRoot, false);
                    nodeGo.transform.localPosition = o.localPos;

                    var col = nodeGo.AddComponent<SphereCollider>();
                    col.radius = 0.12f;
                    col.isTrigger = true;
                    organColliders.Add(col);

                    var labelGo = new GameObject("Label");
                    labelGo.transform.SetParent(nodeGo.transform, false);
                    labelGo.transform.localPosition = new Vector3(0f, 0.12f, 0f);

                    var tm = labelGo.AddComponent<TextMesh>();
                    tm.text = $"{o.icon} {o.name}";
                    tm.fontSize = 22;
                    tm.characterSize = 0.026f;
                    tm.anchor = TextAnchor.MiddleCenter;
                    tm.alignment = TextAlignment.Center;
                    tm.color = new Color(248f / 255f, 250f / 255f, 252f / 255f);

                    var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    bg.name = "LabelBg";
                    bg.transform.SetParent(labelGo.transform, false);
                    bg.transform.localPosition = new Vector3(0f, 0f, 0.01f);
                    float w = (o.name.Length + 4) * 0.022f;
                    bg.transform.localScale = new Vector3(w, 0.12f, 1f);
                    var matBg = CreateUnlitAdditiveMaterial(new Color(8f / 255f, 15f / 255f, 30f / 255f, 0.75f));
                    bg.GetComponent<MeshRenderer>().sharedMaterial = matBg;
                    if (bg.GetComponent<Collider>() != null) Destroy(bg.GetComponent<Collider>());

                    organLabelTransforms.Add(labelGo.transform);
                }
                else
                {
                    var col = node.GetComponent<Collider>();
                    if (col != null) organColliders.Add(col);
                    var lbl = node.Find("Label");
                    if (lbl != null) organLabelTransforms.Add(lbl);
                }
            }
        }

        void EnsureKnowledgeCardAndMicroViewer()
        {
            if (knowledgeCard == null)
            {
                knowledgeCard = GetComponentInChildren<OrganKnowledgeCard>(true);
                if (knowledgeCard == null)
                {
                    var kGo = new GameObject("OrganKnowledgeCard");
                    kGo.transform.SetParent(transform, false);
                    knowledgeCard = kGo.AddComponent<OrganKnowledgeCard>();
                }
            }

            if (microViewer == null)
            {
                microViewer = GetComponentInChildren<MicroSceneViewer>(true);
                if (microViewer == null)
                {
                    var mGo = new GameObject("MicroSceneViewer");
                    mGo.transform.SetParent(transform, false);
                    microViewer = mGo.AddComponent<MicroSceneViewer>();
                }
            }

            microViewer.holoCamera = holoCamera;
            microViewer.holoCanvas = holoCanvas;
            microViewer.OnReturnToFullBody = OnMicroViewerReturn;

            knowledgeCard.OnRequestMicroScene = (organId) =>
            {
                knowledgeCard.HideCard();
                if (microViewer != null)
                {
                    microViewer.OpenSceneForOrgan(organId);
                }
            };
        }

        void OnMicroViewerReturn()
        {
            ResetOrbitView();
            if (selectedOrgan != null && knowledgeCard != null)
            {
                knowledgeCard.ShowOrgan(selectedOrgan.id);
            }
        }

        // =====================================================================
        // Giao diện (uGUI): Thanh công cụ, chuyển lớp, danh sách cơ quan
        // =====================================================================

        void EnsureUI()
        {
            if (holoCanvas != null) return;

            var cTrans = transform.Find("HoloCanvas");
            if (cTrans != null)
            {
                holoCanvas = cTrans.GetComponent<Canvas>();
            }
            else
            {
                var canvasGo = new GameObject("HoloCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvasGo.transform.SetParent(transform, false);
                holoCanvas = canvasGo.GetComponent<Canvas>();
                holoCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                holoCanvas.sortingOrder = 500;

                var scaler = canvasGo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
            }

            UiBridge.EnsureInteractive(holoCanvas);

            // 1. Nút "Về phòng thí nghiệm" góc trên trái uGUI
            var exitBtn = GlassKit.MakeButton(holoCanvas.transform, "NutVePhongLab", "Về phòng thí nghiệm  [Esc]",
                new Vector2(65f, -65f), new Vector2(240f, 48f), ReturnToLab, primary: false, fontSize: 15, anchor: new Vector2(0f, 1f));
            exitButtonRect = (RectTransform)exitBtn.transform;
            exitButtonText = exitBtn.GetComponentInChildren<Text>();

            // 2. Dòng ghi công: "BodyParts3D © The Database Center for Life Science · CC BY 4.0"
            creditText = GlassKit.Label(holoCanvas.transform, "DongGhiCong",
                "BodyParts3D © The Database Center for Life Science · CC BY 4.0",
                13, GlassKit.Dim, false, TextAnchor.MiddleCenter, new Vector2(0f, 32f), new Vector2(600f, 26f), new Vector2(0.5f, 0f));

            // 3. Màn phủ mờ chéo toàn màn hình
            EnsureCrossfadeOverlay();

            // 4. Thanh chuyển lớp Da / Cơ / Xương (trên cùng ở giữa)
            EnsureLayerSelectorBar();

            // 5. Thanh công cụ bên phải: Đặt lại camera, Soi hiển vi, Bật/tắt nhãn, Ôn tập
            EnsureRightToolbar();

            // 6. Ngăn kéo danh sách 15 cơ quan (lề trái màn hình)
            EnsureOrganListDrawer();

            UpdateUIResponsiveness();
        }

        void EnsureCrossfadeOverlay()
        {
            var cfTrans = holoCanvas.transform.Find("CrossfadeOverlay");
            if (cfTrans == null)
            {
                var cfGo = new GameObject("CrossfadeOverlay", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
                cfGo.transform.SetParent(holoCanvas.transform, false);
                var rt = (RectTransform)cfGo.transform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = Vector2.zero;
                rt.anchoredPosition = Vector2.zero;

                var img = cfGo.GetComponent<Image>();
                img.color = Color.black;
                img.raycastTarget = false;

                crossfadeGroup = cfGo.GetComponent<CanvasGroup>();
                crossfadeGroup.alpha = 0f;
                crossfadeGroup.blocksRaycasts = false;
                crossfadeGroup.interactable = false;
            }
            else
            {
                crossfadeGroup = cfTrans.GetComponent<CanvasGroup>();
            }
        }

        void EnsureLayerSelectorBar()
        {
            if (layerBarPanel != null) return;

            var barGo = new GameObject("LayerSelectorBar", typeof(RectTransform), typeof(Image));
            barGo.transform.SetParent(holoCanvas.transform, false);
            layerBarPanel = (RectTransform)barGo.transform;
            layerBarPanel.anchorMin = new Vector2(0.5f, 1f);
            layerBarPanel.anchorMax = new Vector2(0.5f, 1f);
            layerBarPanel.pivot = new Vector2(0.5f, 1f);
            layerBarPanel.sizeDelta = new Vector2(340f, 52f);
            layerBarPanel.anchoredPosition = new Vector2(0f, -60f);

            var img = barGo.GetComponent<Image>();
            img.sprite = GlassKit.Rounded;
            img.type = Image.Type.Sliced;
            img.color = new Color(10f / 255f, 18f / 255f, 34f / 255f, 0.85f);

            // 3 Nút: Da, Cơ, Xương
            GlassKit.MakeButton(layerBarPanel, "BtnLayerDa", "🖐️ Da", new Vector2(10f, -6f), new Vector2(98f, 40f),
                () => SwitchLayer("da"), primary: true, fontSize: 13, anchor: new Vector2(0f, 1f));

            GlassKit.MakeButton(layerBarPanel, "BtnLayerCo", "💪 Cơ", new Vector2(120f, -6f), new Vector2(98f, 40f),
                () => SwitchLayer("co"), primary: false, fontSize: 13, anchor: new Vector2(0f, 1f));

            GlassKit.MakeButton(layerBarPanel, "BtnLayerXuong", "🦴 Xương", new Vector2(230f, -6f), new Vector2(98f, 40f),
                () => SwitchLayer("xuong"), primary: false, fontSize: 13, anchor: new Vector2(0f, 1f));
        }

        void EnsureRightToolbar()
        {
            if (toolbarPanel != null) return;

            var tbGo = new GameObject("RightHUDToolbar", typeof(RectTransform), typeof(Image));
            tbGo.transform.SetParent(holoCanvas.transform, false);
            toolbarPanel = (RectTransform)tbGo.transform;
            toolbarPanel.anchorMin = new Vector2(1f, 0.5f);
            toolbarPanel.anchorMax = new Vector2(1f, 0.5f);
            toolbarPanel.pivot = new Vector2(1f, 0.5f);
            toolbarPanel.sizeDelta = new Vector2(200f, 250f);
            toolbarPanel.anchoredPosition = new Vector2(-30f, 60f);

            var img = tbGo.GetComponent<Image>();
            img.sprite = GlassKit.Rounded;
            img.type = Image.Type.Sliced;
            img.color = new Color(10f / 255f, 18f / 255f, 34f / 255f, 0.82f);

            GlassKit.MakeButton(toolbarPanel, "NutDatLaiCamera", "🔄 Đặt lại góc", new Vector2(15f, -15f), new Vector2(170f, 44f),
                ResetOrbitView, primary: false, fontSize: 13, anchor: new Vector2(0f, 1f));

            GlassKit.MakeButton(toolbarPanel, "NutSoiHienVi", "🔬 Soi hiển vi", new Vector2(15f, -70f), new Vector2(170f, 44f),
                OpenMicroSceneForCurrentOrgan, primary: true, fontSize: 13, anchor: new Vector2(0f, 1f));

            GlassKit.MakeButton(toolbarPanel, "NutBatTatNhan", "🏷️ Bật/tắt nhãn", new Vector2(15f, -125f), new Vector2(170f, 44f),
                Toggle3DLabels, primary: false, fontSize: 13, anchor: new Vector2(0f, 1f));

            GlassKit.MakeButton(toolbarPanel, "NutOnTapToolbar", "📝 Ôn tập", new Vector2(15f, -180f), new Vector2(170f, 44f),
                OpenQuizModalFromToolbar, primary: false, fontSize: 13, anchor: new Vector2(0f, 1f));
        }

        void EnsureOrganListDrawer()
        {
            if (organListDrawer != null) return;

            var dGo = new GameObject("OrganListDrawer", typeof(RectTransform), typeof(Image));
            dGo.transform.SetParent(holoCanvas.transform, false);
            organListDrawer = (RectTransform)dGo.transform;
            organListDrawer.anchorMin = new Vector2(0f, 0f);
            organListDrawer.anchorMax = new Vector2(0f, 1f);
            organListDrawer.pivot = new Vector2(0f, 0.5f);
            organListDrawer.sizeDelta = new Vector2(260f, -140f);
            organListDrawer.anchoredPosition = new Vector2(30f, 0f);

            var img = dGo.GetComponent<Image>();
            img.sprite = GlassKit.Rounded;
            img.type = Image.Type.Sliced;
            img.color = new Color(10f / 255f, 18f / 255f, 34f / 255f, 0.88f);

            GlassKit.Label(organListDrawer, "TieuDeDrawer", "15 Cơ quan giải phẫu", 15, GlassKit.Ink, true,
                TextAnchor.MiddleLeft, new Vector2(18f, -16f), new Vector2(220f, 26f), new Vector2(0f, 1f));

            var scrollGo = new GameObject("DrawerScroll", typeof(RectTransform), typeof(ScrollRect), typeof(RectMask2D));
            scrollGo.transform.SetParent(organListDrawer, false);
            var rtS = (RectTransform)scrollGo.transform;
            rtS.anchorMin = Vector2.zero;
            rtS.anchorMax = Vector2.one;
            rtS.offsetMin = new Vector2(10f, 15f);
            rtS.offsetMax = new Vector2(-10f, -48f);

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 25f;

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(scrollGo.transform, false);
            var rtC = (RectTransform)contentGo.transform;
            rtC.anchorMin = new Vector2(0f, 1f);
            rtC.anchorMax = new Vector2(1f, 1f);
            rtC.pivot = new Vector2(0f, 1f);
            rtC.sizeDelta = new Vector2(0f, Organs.Count * 44f);
            rtC.anchoredPosition = Vector2.zero;

            scroll.content = rtC;

            for (int i = 0; i < Organs.Count; i++)
            {
                var o = Organs[i];
                string targetId = o.id;
                float posY = -i * 42f;

                GlassKit.MakeButton(rtC, "BtnOrgan_" + o.id, $"{o.icon}  {o.name}",
                    new Vector2(0f, posY), new Vector2(235f, 38f),
                    () => SelectOrgan(targetId), primary: false, fontSize: 13, anchor: new Vector2(0f, 1f));
            }
        }

        void UpdateUIResponsiveness()
        {
            if (exitButtonRect == null) return;

            bool touch = PadInput.UsingTouch;
            float sh = Screen.height > 0 ? (float)Screen.height : 1080f;
            float sw = Screen.width > 0 ? (float)Screen.width : 1920f;
            float topInsetCanvas = (Screen.safeArea.yMax < sh) ? ((sh - Screen.safeArea.yMax) / sh) * 1080f : 0f;
            float leftInsetCanvas = (Screen.safeArea.xMin > 0) ? (Screen.safeArea.xMin / sw) * 1920f : 0f;

            float posX = Mathf.Max(65f, leftInsetCanvas + 65f);
            float posY = -Mathf.Max(65f, topInsetCanvas + 65f);

            exitButtonRect.anchorMin = new Vector2(0f, 1f);
            exitButtonRect.anchorMax = new Vector2(0f, 1f);
            exitButtonRect.pivot = new Vector2(0f, 1f);
            exitButtonRect.anchoredPosition = new Vector2(posX, posY);

            if (exitButtonText != null)
            {
                exitButtonText.text = touch ? "Về phòng thí nghiệm" : "Về phòng thí nghiệm  [Esc]";
            }
        }

        // =====================================================================
        // Điều khiển Chuyển lớp Da / Cơ / Xương (0.35s Fade)
        // =====================================================================

        public void SwitchLayer(string targetLayer)
        {
            if (activeLayer == targetLayer) return;
            if (layerCoroutine != null) StopCoroutine(layerCoroutine);
            layerCoroutine = StartCoroutine(CoSwitchLayer(targetLayer, 0.35f));
        }

        IEnumerator CoSwitchLayer(string targetLayer, float duration)
        {
            string oldLayer = activeLayer;
            activeLayer = targetLayer;

            GameObject fromGo = GetLayerObject(oldLayer);
            GameObject toGo = GetLayerObject(targetLayer);

            if (toGo != null) toGo.SetActive(true);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                SetLayerTransparency(fromGo, 1f - t);
                SetLayerTransparency(toGo, t);

                yield return null;
            }

            if (fromGo != null && oldLayer != "xuong") fromGo.SetActive(false);
            SetLayerTransparency(toGo, 1f);
            layerCoroutine = null;
        }

        GameObject GetLayerObject(string layer)
        {
            switch (layer)
            {
                case "da": return layerSkin;
                case "co": return layerMuscles;
                case "xuong": return layerSkeleton;
                default: return layerSkin;
            }
        }

        void SetLayerTransparency(GameObject go, float alpha)
        {
            if (go == null) return;
            var renderers = go.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var mat = renderers[i].sharedMaterial;
                if (mat != null)
                {
                    if (mat.HasProperty("_BaseColor"))
                    {
                        var c = mat.GetColor("_BaseColor");
                        c.a = alpha;
                        mat.SetColor("_BaseColor", c);
                    }
                    else if (mat.HasProperty("_Color"))
                    {
                        var c = mat.color;
                        c.a = alpha;
                        mat.color = c;
                    }
                }
            }
        }

        // =====================================================================
        // Tương tác Chọn Cơ quan & Lia Camera mượt mà (Focus Organ)
        // =====================================================================

        public void SelectOrgan(string organId)
        {
            var target = Organs.Find(o => o.id == organId || o.id.Replace('_', '-') == organId);
            if (target == null) return;

            selectedOrgan = target;

            if (organHighlightRing != null)
            {
                organHighlightRing.localPosition = target.localPos + new Vector3(0f, -0.05f, 0f);
                organHighlightRing.gameObject.SetActive(true);
            }

            if (focusCoroutine != null) StopCoroutine(focusCoroutine);
            focusCoroutine = StartCoroutine(CoFocusOrgan(target, 0.45f));

            if (knowledgeCard != null)
            {
                knowledgeCard.ShowOrgan(target.id);
            }
        }

        IEnumerator CoFocusOrgan(OrganTarget target, float duration)
        {
            isFocusingOrgan = true;

            Vector3 startTargetPos = orbitTarget != null ? orbitTarget.localPosition : new Vector3(0f, 1.15f, 0f);
            Vector3 endTargetPos = target.localPos;

            float startDist = cameraDistance;
            float endDist = target.focusDist;

            float startPitch = currentPitch;
            float endPitch = target.pitch;

            float startYaw = currentYaw;
            float endYaw = target.yaw;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

                if (orbitTarget != null)
                {
                    orbitTarget.localPosition = Vector3.Lerp(startTargetPos, endTargetPos, t);
                }

                cameraDistance = Mathf.Lerp(startDist, endDist, t);
                currentPitch = Mathf.Lerp(startPitch, endPitch, t);
                currentYaw = Mathf.LerpAngle(startYaw, endYaw, t);

                yield return null;
            }

            if (orbitTarget != null) orbitTarget.localPosition = endTargetPos;
            cameraDistance = endDist;
            currentPitch = endPitch;
            currentYaw = endYaw;

            isFocusingOrgan = false;
            focusCoroutine = null;
        }

        public void OpenMicroSceneForCurrentOrgan()
        {
            if (selectedOrgan == null)
            {
                selectedOrgan = Organs[0];
            }

            if (knowledgeCard != null) knowledgeCard.HideCard();
            if (microViewer != null)
            {
                microViewer.OpenSceneForOrgan(selectedOrgan.id);
            }
        }

        public void Toggle3DLabels()
        {
            if (organLabelsRoot != null)
            {
                organLabelsRoot.gameObject.SetActive(!organLabelsRoot.gameObject.activeSelf);
            }
        }

        void OpenQuizModalFromToolbar()
        {
            if (knowledgeCard != null)
            {
                if (selectedOrgan != null)
                {
                    knowledgeCard.ShowOrgan(selectedOrgan.id);
                }
                knowledgeCard.OpenQuizModal();
            }
        }

        // =====================================================================
        // Hoạt cảnh Sinh lý: Tim đập 72 bpm & Phổi thở 16 bpm (0 GC Alloc)
        // =====================================================================

        void UpdatePhysiologyAnimations()
        {
            float t = Time.time;

            if (heartTransform != null)
            {
                float heartPhase = (t / 0.83333f) % 1f;
                float pulse = (heartPhase < 0.36f) ? Mathf.Sin(heartPhase / 0.36f * Mathf.PI) : 0f;
                float s = 1.0f + (0.08f * pulse);
                heartTransform.localScale = new Vector3(s, s, s);
            }

            if (lungTransform != null)
            {
                float lungPhase = (t / 3.75f) % 1f;
                float breath = 1.0f + 0.05f * Mathf.Sin(lungPhase * Mathf.PI * 2f);
                lungTransform.localScale = new Vector3(breath, breath, breath);
            }

            if (organHighlightRing != null && organHighlightRing.gameObject.activeSelf)
            {
                float pulseRing = 1.0f + 0.06f * Mathf.Sin(t * 6f);
                organHighlightRing.localScale = new Vector3(pulseRing, 1f, pulseRing);
            }
        }

        void Update3DLabelsBillboard()
        {
            if (holoCamera == null || organLabelsRoot == null || !organLabelsRoot.gameObject.activeSelf) return;

            Quaternion camRot = holoCamera.transform.rotation;
            for (int i = 0; i < organLabelTransforms.Count; i++)
            {
                if (organLabelTransforms[i] != null)
                {
                    organLabelTransforms[i].rotation = camRot;
                }
            }
        }

        // =====================================================================
        // Điều khiển Orbit Camera (Xoay 1 ngón, Zoom chụm 2 ngón, Chạm đúp reset)
        // =====================================================================

        void HandleOrbitInput()
        {
            if (holoCamera == null || isTransitioning) return;

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
            {
                var touches = Touchscreen.current.touches;
                int activeTouches = 0;
                for (int i = 0; i < touches.Count; i++)
                {
                    if (touches[i].isInProgress) activeTouches++;
                }

                if (activeTouches == 1)
                {
                    var t0 = touches[0];
                    if (t0.press.wasPressedThisFrame)
                    {
                        if (Time.unscaledTime - lastTapTime < DoubleTapThreshold)
                        {
                            ResetOrbitView();
                        }
                        else
                        {
                            Check3DOrganHit(t0.position.ReadValue());
                        }
                        lastTapTime = Time.unscaledTime;
                    }

                    Vector2 delta = t0.delta.ReadValue();
                    currentYaw += delta.x * 0.22f;
                    currentPitch = Mathf.Clamp(currentPitch - delta.y * 0.22f, -25f, 75f);
                }
                else if (activeTouches >= 2)
                {
                    var t0 = touches[0];
                    var t1 = touches[1];
                    Vector2 p0 = t0.position.ReadValue();
                    Vector2 p1 = t1.position.ReadValue();
                    Vector2 prev0 = p0 - t0.delta.ReadValue();
                    Vector2 prev1 = p1 - t1.delta.ReadValue();

                    float prevDist = (prev0 - prev1).magnitude;
                    float currDist = (p0 - p1).magnitude;
                    float diff = (currDist - prevDist) * 0.005f;

                    cameraDistance = Mathf.Clamp(cameraDistance - diff, minDistance, maxDistance);
                }
                return;
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    if (Time.unscaledTime - lastTapTime < DoubleTapThreshold)
                    {
                        ResetOrbitView();
                    }
                    else
                    {
                        Check3DOrganHit(mouse.position.ReadValue());
                    }
                    lastTapTime = Time.unscaledTime;
                    isDraggingPointer = true;
                    lastPointerPos = mouse.position.ReadValue();
                }
                else if (mouse.leftButton.wasReleasedThisFrame)
                {
                    isDraggingPointer = false;
                }

                if (isDraggingPointer && mouse.leftButton.isPressed)
                {
                    Vector2 curPos = mouse.position.ReadValue();
                    Vector2 delta = curPos - lastPointerPos;
                    lastPointerPos = curPos;

                    currentYaw += delta.x * 0.22f;
                    currentPitch = Mathf.Clamp(currentPitch - delta.y * 0.22f, -25f, 75f);
                }

                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    cameraDistance = Mathf.Clamp(cameraDistance - Mathf.Sign(scroll) * 0.25f, minDistance, maxDistance);
                }
            }
        }

        void Check3DOrganHit(Vector2 screenPos)
        {
            if (holoCamera == null) return;
            Ray ray = holoCamera.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out RaycastHit hit, 20f))
            {
                for (int i = 0; i < organColliders.Count; i++)
                {
                    if (hit.collider == organColliders[i])
                    {
                        SelectOrgan(Organs[i].id);
                        return;
                    }
                }
            }
        }

        public void ResetOrbitView()
        {
            if (orbitTarget != null)
            {
                orbitTarget.localPosition = new Vector3(0f, 1.15f, 0f);
            }
            if (organHighlightRing != null)
            {
                organHighlightRing.gameObject.SetActive(false);
            }
            currentYaw = defaultYaw;
            currentPitch = defaultPitch;
            cameraDistance = defaultDistance;
        }

        void UpdateOrbitCamera()
        {
            if (holoCamera == null) return;

            Vector3 target = orbitTarget != null ? orbitTarget.position : (transform.position + new Vector3(0f, 1.15f, 0f));
            Quaternion rot = Quaternion.Euler(currentPitch, currentYaw, 0f);
            Vector3 camPos = target - rot * Vector3.forward * cameraDistance;

            holoCamera.transform.position = camPos;
            holoCamera.transform.rotation = rot;
        }

        // =====================================================================
        // Vào - Ra bằng Simple Crossfade (~0.25s) kết nối với BioPortal
        // =====================================================================

        public void Enter(BioPortal portal)
        {
            if (isTransitioning) return;
            StartCoroutine(CoEnter(portal));
        }

        IEnumerator CoEnter(BioPortal portal)
        {
            isTransitioning = true;

            savedViewer = FindAnyObjectByType<DesktopViewer>();
            savedViewCam = savedViewer != null ? savedViewer.viewCamera : Camera.main;

            if (savedViewer != null)
            {
                savedPosition = savedViewer.transform.position;
                savedRotation = savedViewer.transform.rotation;
                savedCamLocalRotation = savedViewer.cameraTransform != null
                    ? savedViewer.cameraTransform.localRotation
                    : Quaternion.identity;
            }
            else if (savedViewCam != null)
            {
                savedPosition = savedViewCam.transform.position;
                savedRotation = savedViewCam.transform.rotation;
                savedCamLocalRotation = savedViewCam.transform.localRotation;
            }

            if (savedViewCam != null)
            {
                savedFov = savedViewCam.fieldOfView;
            }

            savedPortalPos = portal != null ? portal.transform.position : transform.position;

            ZoneStreamer.Instance?.Keep(this, 9999f, savedPortalPos);
            DesktopViewer.UiCapture++;

            yield return CoFadeOverlay(0f, 1f, 0.12f);

            if (savedViewCam != null) savedViewCam.enabled = false;
            gameObject.SetActive(true);
            if (holoCamera != null) holoCamera.enabled = true;

            UpdateUIResponsiveness();
            ResetOrbitView();
            UpdateOrbitCamera();

            OrganKnowledgeCard.PreloadAllData();

            yield return CoFadeOverlay(1f, 0f, 0.13f);

            isTransitioning = false;
        }

        public void ReturnToLab()
        {
            var transition = BioPortalTransition.Instance;
            if (transition != null && transition.CurrentState == BioPortalTransition.TransitionState.TrongPhong)
            {
                transition.StartCloseTransition();
                return;
            }

            if (isTransitioning) return;
            StartCoroutine(CoReturn());
        }

        IEnumerator CoReturn()
        {
            isTransitioning = true;

            yield return CoFadeOverlay(0f, 1f, 0.12f);

            if (holoCamera != null) holoCamera.enabled = false;
            gameObject.SetActive(false);
            if (savedViewCam != null) savedViewCam.enabled = true;

            if (savedViewer != null)
            {
                var cc = savedViewer.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;

                savedViewer.transform.position = savedPosition;
                savedViewer.transform.rotation = savedRotation;

                if (savedViewer.cameraTransform != null)
                {
                    savedViewer.cameraTransform.localRotation = savedCamLocalRotation;
                }

                if (savedViewCam != null) savedViewCam.fieldOfView = savedFov;
                if (cc != null) cc.enabled = true;
            }
            else if (savedViewCam != null)
            {
                savedViewCam.transform.position = savedPosition;
                savedViewCam.transform.rotation = savedRotation;
                savedViewCam.fieldOfView = savedFov;
            }

            ZoneStreamer.Instance?.Release(this);
            yield return ZoneStreamer.WaitReadyAt(savedPortalPos, 5f);

            DesktopViewer.UiCapture = Mathf.Max(0, DesktopViewer.UiCapture - 1);

            yield return CoFadeOverlay(1f, 0f, 0.13f);

            isTransitioning = false;
        }

        IEnumerator CoFadeOverlay(float from, float to, float duration)
        {
            if (crossfadeGroup == null) yield break;

            crossfadeGroup.blocksRaycasts = to > 0.05f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                crossfadeGroup.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }
            crossfadeGroup.alpha = to;
            crossfadeGroup.blocksRaycasts = to > 0.05f;
        }

        // =====================================================================
        // Bộ dựng vật liệu & Lưới thủ công
        // =====================================================================

        GameObject EnsureGlowingRing(string name, float innerR, float outerR, float y, Color col)
        {
            var t = transform.Find(name);
            if (t != null) return t.gameObject;

            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, y, 0f);

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = BuildRingMesh(innerR, outerR, 48);

            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;

            var mat = CreateUnlitAdditiveMaterial(col);
            mr.sharedMaterial = mat;

            return go;
        }

        GameObject EnsureHexagonRing(string name, float radius, float thickness, float y, Color col)
        {
            var t = transform.Find(name);
            if (t != null) return t.gameObject;

            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, y, 0f);

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = BuildHexagonMesh(radius, thickness);

            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;

            var mat = CreateUnlitAdditiveMaterial(col);
            mr.sharedMaterial = mat;

            return go;
        }

        Material CreateFloorMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Transparent")
                         ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader) { name = "HoloFloorMat", hideFlags = HideFlags.DontSave };

            var tex = GenerateFloorGridTexture(512);
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);

            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 1f);
            mat.renderQueue = (int)RenderQueue.Geometry + 10;

            return mat;
        }

        Texture2D GenerateFloorGridTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "HoloFloorGridTex",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };

            Color bg = new Color(10f / 255f, 18f / 255f, 34f / 255f, 1f);
            Color gridLineMinor = new Color(14f / 255f, 58f / 255f, 74f / 255f, 0.28f);
            Color gridLineMajor = new Color(6f / 255f, 182f / 255f, 212f / 255f, 0.40f);

            var px = new Color[size * size];
            float center = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                float dy = (y - center) / center;
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / center;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);

                    if (r > 1.0f)
                    {
                        px[y * size + x] = Color.clear;
                        continue;
                    }

                    Color c = bg;
                    int gx = x % (size / 20);
                    int gy = y % (size / 20);

                    if (gx == 0 || gy == 0)
                    {
                        c = Color.Lerp(c, gridLineMinor, 0.55f);
                    }

                    if (x % (size / 4) == 0 || y % (size / 4) == 0)
                    {
                        c = Color.Lerp(c, gridLineMajor, 0.75f);
                    }

                    if (r > 0.95f)
                    {
                        float edgeFade = Mathf.Clamp01((1.0f - r) / 0.05f);
                        c.a = edgeFade;
                    }
                    else
                    {
                        c.a = 1f;
                    }

                    px[y * size + x] = c;
                }
            }

            tex.SetPixels(px);
            tex.Apply(false, true);
            return tex;
        }

        Material CreatePodiumMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader) { name = "HoloPodiumMat", hideFlags = HideFlags.DontSave };
            mat.color = new Color(14f / 255f, 22f / 255f, 38f / 255f, 1f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.65f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.70f);
            return mat;
        }

        Material CreateUnlitAdditiveMaterial(Color col)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Transparent")
                         ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader) { name = "HoloAdditiveMat", hideFlags = HideFlags.DontSave };

            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 1f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.One);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Cull", (float)CullMode.Off);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_BLENDMODE_ADD");
            mat.renderQueue = (int)RenderQueue.Transparent;

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", col);

            return mat;
        }

        Material CreateBeamMaterial()
        {
            var shader = Shader.Find("Khkt/Tang2/HoloBeam")
                         ?? Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Transparent")
                         ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader) { name = "HoloBeamMat", hideFlags = HideFlags.DontSave };

            var tex = new Texture2D(16, 128, TextureFormat.RGBA32, false)
            {
                name = "HoloBeamGradient",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
            Color cCyan = new Color(34f / 255f, 211f / 255f, 238f / 255f);
            var px = new Color[16 * 128];
            for (int y = 0; y < 128; y++)
            {
                float v = y / 127f;
                float alpha = Mathf.Lerp(0.35f, 0.04f, Mathf.Pow(v, 0.7f));
                Color c = new Color(cCyan.r, cCyan.g, cCyan.b, alpha);
                for (int x = 0; x < 16; x++) px[y * 16 + x] = c;
            }
            tex.SetPixels(px);
            tex.Apply(false, true);

            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);

            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 1f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.One);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Cull", (float)CullMode.Off);
            mat.renderQueue = (int)RenderQueue.Transparent;

            return mat;
        }

        Mesh BuildDiscMesh(float radius, int segments)
        {
            var m = new Mesh { name = "HoloDisc" };
            var verts = new Vector3[segments + 1];
            var uvs = new Vector2[segments + 1];
            var normals = new Vector3[segments + 1];
            var tris = new int[segments * 3];

            verts[0] = Vector3.zero;
            uvs[0] = new Vector2(0.5f, 0.5f);
            normals[0] = Vector3.up;

            float step = Mathf.PI * 2f / segments;
            for (int i = 0; i < segments; i++)
            {
                float a = i * step;
                float x = Mathf.Cos(a) * radius;
                float z = Mathf.Sin(a) * radius;
                verts[i + 1] = new Vector3(x, 0f, z);
                uvs[i + 1] = new Vector2(Mathf.Cos(a) * 0.5f + 0.5f, Mathf.Sin(a) * 0.5f + 0.5f);
                normals[i + 1] = Vector3.up;
            }

            for (int i = 0; i < segments; i++)
            {
                tris[i * 3] = 0;
                tris[i * 3 + 1] = i + 1;
                tris[i * 3 + 2] = (i + 1) % segments + 1;
            }

            m.vertices = verts;
            m.uv = uvs;
            m.normals = normals;
            m.triangles = tris;
            m.RecalculateBounds();
            return m;
        }

        Mesh BuildFrustumMesh(float bottomR, float topR, float height, int segments)
        {
            var m = new Mesh { name = "HoloPodiumFrustum" };
            int vertCount = (segments + 1) * 2;
            var verts = new Vector3[vertCount];
            var normals = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];
            var tris = new int[segments * 6];

            float step = Mathf.PI * 2f / segments;
            float halfH = height * 0.5f;

            for (int i = 0; i <= segments; i++)
            {
                float a = i * step;
                float ca = Mathf.Cos(a);
                float sa = Mathf.Sin(a);

                verts[i] = new Vector3(ca * bottomR, -halfH, sa * bottomR);
                verts[i + segments + 1] = new Vector3(ca * topR, halfH, sa * topR);

                Vector3 n = new Vector3(ca, 0f, sa);
                normals[i] = n;
                normals[i + segments + 1] = n;

                float u = (float)i / segments;
                uvs[i] = new Vector2(u, 0f);
                uvs[i + segments + 1] = new Vector2(u, 1f);
            }

            int t = 0;
            for (int i = 0; i < segments; i++)
            {
                int b0 = i;
                int b1 = i + 1;
                int t0 = i + segments + 1;
                int t1 = i + segments + 2;

                tris[t++] = b0; tris[t++] = t0; tris[t++] = b1;
                tris[t++] = b1; tris[t++] = t0; tris[t++] = t1;
            }

            m.vertices = verts;
            m.normals = normals;
            m.uv = uvs;
            m.triangles = tris;
            m.RecalculateBounds();
            return m;
        }

        Mesh BuildRingMesh(float innerR, float outerR, int segments)
        {
            var m = new Mesh { name = "HoloRingMesh" };
            int vertCount = (segments + 1) * 2;
            var verts = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];
            var normals = new Vector3[vertCount];
            var tris = new int[segments * 6];

            float step = Mathf.PI * 2f / segments;
            for (int i = 0; i <= segments; i++)
            {
                float a = i * step;
                float ca = Mathf.Cos(a);
                float sa = Mathf.Sin(a);

                verts[i] = new Vector3(ca * innerR, 0f, sa * innerR);
                verts[i + segments + 1] = new Vector3(ca * outerR, 0f, sa * outerR);

                normals[i] = Vector3.up;
                normals[i + segments + 1] = Vector3.up;

                float u = (float)i / segments;
                uvs[i] = new Vector2(u, 0f);
                uvs[i + segments + 1] = new Vector2(u, 1f);
            }

            int t = 0;
            for (int i = 0; i < segments; i++)
            {
                int in0 = i;
                int in1 = i + 1;
                int out0 = i + segments + 1;
                int out1 = i + segments + 2;

                tris[t++] = in0; tris[t++] = in1; tris[t++] = out0;
                tris[t++] = in1; tris[t++] = out1; tris[t++] = out0;
            }

            m.vertices = verts;
            m.normals = normals;
            m.uv = uvs;
            m.triangles = tris;
            m.RecalculateBounds();
            return m;
        }

        Mesh BuildHexagonMesh(float radius, float thickness)
        {
            var m = new Mesh { name = "HoloHexagonMesh" };
            int segments = 6;
            float innerR = Mathf.Max(0.01f, radius - thickness);
            float outerR = radius;

            var verts = new Vector3[(segments + 1) * 2];
            var normals = new Vector3[verts.Length];
            var uvs = new Vector2[verts.Length];
            var tris = new int[segments * 6];

            float step = Mathf.PI * 2f / segments;
            for (int i = 0; i <= segments; i++)
            {
                float a = i * step + Mathf.PI / 6f;
                float ca = Mathf.Cos(a);
                float sa = Mathf.Sin(a);

                verts[i] = new Vector3(ca * innerR, 0f, sa * innerR);
                verts[i + segments + 1] = new Vector3(ca * outerR, 0f, sa * outerR);

                normals[i] = Vector3.up;
                normals[i + segments + 1] = Vector3.up;

                float u = (float)i / segments;
                uvs[i] = new Vector2(u, 0f);
                uvs[i + segments + 1] = new Vector2(u, 1f);
            }

            int t = 0;
            for (int i = 0; i < segments; i++)
            {
                int in0 = i;
                int in1 = i + 1;
                int out0 = i + segments + 1;
                int out1 = i + segments + 2;

                tris[t++] = in0; tris[t++] = in1; tris[t++] = out0;
                tris[t++] = in1; tris[t++] = out1; tris[t++] = out0;
            }

            m.vertices = verts;
            m.normals = normals;
            m.uv = uvs;
            m.triangles = tris;
            m.RecalculateBounds();
            return m;
        }

        Mesh BuildOpenCylinderMesh(float radius, float height, int segments)
        {
            var m = new Mesh { name = "HoloOpenCylinder" };
            int vertCount = (segments + 1) * 2;
            var verts = new Vector3[vertCount];
            var normals = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];
            var tris = new int[segments * 12];

            float step = Mathf.PI * 2f / segments;
            for (int i = 0; i <= segments; i++)
            {
                float a = i * step;
                float ca = Mathf.Cos(a);
                float sa = Mathf.Sin(a);

                verts[i] = new Vector3(ca * radius, 0f, sa * radius);
                verts[i + segments + 1] = new Vector3(ca * radius, height, sa * radius);

                normals[i] = new Vector3(ca, 0f, sa);
                normals[i + segments + 1] = new Vector3(ca, 0f, sa);

                float u = (float)i / segments;
                uvs[i] = new Vector2(u, 0f);
                uvs[i + segments + 1] = new Vector2(u, 1f);
            }

            int t = 0;
            for (int i = 0; i < segments; i++)
            {
                int b0 = i;
                int b1 = i + 1;
                int t0 = i + segments + 1;
                int t1 = i + segments + 2;

                tris[t++] = b0; tris[t++] = t0; tris[t++] = b1;
                tris[t++] = b1; tris[t++] = t0; tris[t++] = t1;

                tris[t++] = b0; tris[t++] = b1; tris[t++] = t0;
                tris[t++] = b1; tris[t++] = t1; tris[t++] = t0;
            }

            m.vertices = verts;
            m.normals = normals;
            m.uv = uvs;
            m.triangles = tris;
            m.RecalculateBounds();
            return m;
        }
    }
}
