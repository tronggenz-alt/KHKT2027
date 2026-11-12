using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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
    /// HoloBioRoom (GĐ2 - Phòng ảo tĩnh):
    ///   • Gốc phòng đặt tại y = -600, mặc định SetActive(false).
    ///   • Camera riêng cho phòng ảo (holoCamera), không có AudioListener thứ hai.
    ///   • Sàn tròn bán kính 10m, nền tối #0A1222, lưới 20x20m (#06B6D4 / #0E3A4A, alpha 0.28).
    ///   • Bệ hologram hình trụ (bán kính 1.05m -> 1.2m, cao 0.14m).
    ///   • 4 vòng sáng:
    ///       1) Sát bệ (#22D3EE)
    ///       2) Lục giác (#10B981)
    ///       3) Vòng xuyến cao 0.6m quay theo chiều kim đồng hồ
    ///       4) Vòng xuyến cao 2.4m quay ngược chiều kim đồng hồ
    ///   • Chùm sáng trụ hở cao 3.9m (Unlit, additive).
    ///   • Hệ hạt bụi sáng <= 150 hạt (ParticleSystem).
    ///   • Mô hình BodyParts3D đứng trên bệ cao 1.7m (bộ xương, hệ cơ, da).
    ///   • Điều khiển: xoay quanh cơ thể (1 ngón / chuột kéo), zoom 0.8m -> 4m (chụm 2 ngón / cuộn chuột), chạm đúp reset góc nhìn.
    ///   • Nút "Về phòng thí nghiệm" góc trên trái uGUI (>= 65 canvas units, phím Esc trên PC).
    ///   • Dòng ghi công: "BodyParts3D © The Database Center for Life Science · CC BY 4.0".
    ///   • Chuyển cảnh Vào - Ra mờ chéo đơn giản (~0.25s), lưu và phục hồi chính xác vị trí, góc nhìn (sai số <= 1cm, <= 0.5°),
    ///     khóa/mở di chuyển bằng DesktopViewer.UiCapture, gọi ZoneStreamer.Keep/Release/WaitReadyAt.
    /// </summary>
    public class HoloBioRoom : MonoBehaviour
    {
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

        [Header("Giao diện & Chuyển cảnh")]
        public Canvas holoCanvas;
        public CanvasGroup crossfadeGroup;
        public RectTransform exitButtonRect;
        public Text exitButtonText;
        public Text creditText;

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

        void Awake()
        {
            EnsureRoomHierarchy();
        }

        void Start()
        {
            // Mặc định gốc phòng ảo ẩn khi khởi động
            if (transform.position.y > -500f)
            {
                transform.position = new Vector3(transform.position.x, -600f, transform.position.z);
            }
            gameObject.SetActive(false);
        }

        void Update()
        {
            // 1. Quay 2 vòng xuyến ngược chiều nhau
            if (ring3Torus != null) ring3Torus.Rotate(Vector3.up, ringRotateSpeed * Time.deltaTime, Space.World);
            if (ring4Torus != null) ring4Torus.Rotate(Vector3.up, -ringRotateSpeed * Time.deltaTime, Space.World);

            // 2. Xử lý điều khiển Orbit Camera
            HandleOrbitInput();
            UpdateOrbitCamera();

            // 3. Phím tắt Esc trên PC để về phòng thí nghiệm
            if (!isTransitioning && !PadInput.UsingTouch)
            {
                var kb = Keyboard.current;
                if (kb != null && kb.escapeKey.wasPressedThisFrame)
                {
                    ReturnToLab();
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
                // Nền tối #0A1222
                holoCamera.backgroundColor = new Color(10f / 255f, 18f / 255f, 34f / 255f, 1f);
                holoCamera.fieldOfView = 55f;
                holoCamera.nearClipPlane = 0.05f;
                holoCamera.farClipPlane = 100f;
            }

            // Tuyệt đối không để có AudioListener thứ hai
            var listener = holoCamera.GetComponent<AudioListener>();
            if (listener != null) Destroy(listener);

            holoCamera.enabled = false;
        }

        void EnsureHoloPlatform()
        {
            // 1. Sàn tròn bán kính 10m, nền tối #0A1222, lưới 20x20m (#06B6D4 / #0E3A4A, alpha 0.28)
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

            // 2. Bệ hologram hình trụ: bán kính 1.05m -> 1.2m, cao 0.14m
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

            // 3. 4 Vòng sáng
            // Vòng 1: Sát bệ (#22D3EE) - cao 0.142m, bán kính 1.05m
            EnsureGlowingRing("Vong1_SatBe", 0.98f, 1.05f, 0.142f, new Color(34f / 255f, 211f / 255f, 238f / 255f));

            // Vòng 2: Lục giác ở tâm (#10B981) - cao 0.143m, bán kính 0.35m
            EnsureHexagonRing("Vong2_LucGiac", 0.35f, 0.03f, 0.143f, new Color(16f / 255f, 185f / 255f, 129f / 255f));

            // Vòng 3: Vòng xuyến cao 0.6m (#22D3EE / #06B6D4)
            if (ring3Torus == null)
            {
                var r3 = EnsureGlowingRing("Vong3_Torus_0.6m", 0.94f, 1.02f, 0.14f + 0.60f, new Color(34f / 255f, 211f / 255f, 238f / 255f, 0.85f));
                ring3Torus = r3.transform;
            }

            // Vòng 4: Vòng xuyến cao 2.4m (#10B981 / #06B6D4)
            if (ring4Torus == null)
            {
                var r4 = EnsureGlowingRing("Vong4_Torus_2.4m", 0.88f, 0.95f, 0.14f + 2.40f, new Color(16f / 255f, 185f / 255f, 129f / 255f, 0.85f));
                ring4Torus = r4.transform;
            }

            // 4. Chùm sáng trụ hở cao 3.9m (bán kính 1.05m, từ y = 0.14m đến 4.04m)
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

            // 5. Hệ hạt bụi sáng <= 150 hạt
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
                shape.shapeType = ParticleSystemShapeType.Cylinder;
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

            // 6. Tâm orbit camera (ở độ cao ngực mô hình: y ~ 1.15m)
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

        // =====================================================================
        // Giao diện (uGUI) & Màn che mờ chéo (Crossfade)
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

            // Nút "Về phòng thí nghiệm" góc trên trái uGUI (>= 65 canvas units, hỗ trợ Esc trên PC)
            var exitBtn = GlassKit.MakeButton(holoCanvas.transform, "NutVePhongLab", "Về phòng thí nghiệm  [Esc]",
                new Vector2(65f, -65f), new Vector2(240f, 48f), ReturnToLab, primary: false, fontSize: 15, anchor: new Vector2(0f, 1f));
            exitButtonRect = (RectTransform)exitBtn.transform;
            exitButtonText = exitBtn.GetComponentInChildren<Text>();

            // Dòng ghi công: "BodyParts3D © The Database Center for Life Science · CC BY 4.0"
            creditText = GlassKit.Label(holoCanvas.transform, "DongGhiCong",
                "BodyParts3D © The Database Center for Life Science · CC BY 4.0",
                13, GlassKit.Dim, false, TextAnchor.MiddleCenter, new Vector2(0f, 32f), new Vector2(600f, 26f), new Vector2(0.5f, 0f));

            // Lớp phủ mờ chéo toàn màn hình (~0.25s)
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

            UpdateUIResponsiveness();
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
        // Điều khiển Orbit Camera (Xoay 1 ngón, Zoom chụm 2 ngón, Chạm đúp reset)
        // =====================================================================

        void HandleOrbitInput()
        {
            if (holoCamera == null || isTransitioning) return;

            // 1. Chạm cảm ứng (iPad / Touch Screen)
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

            // 2. Chuột & Bàn phím (PC Desktop)
            var mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    if (Time.unscaledTime - lastTapTime < DoubleTapThreshold)
                    {
                        ResetOrbitView();
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

        public void ResetOrbitView()
        {
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

            // 1. Lưu vị trí, góc nhìn, FOV của player (từ DesktopViewer / viewCam)
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

            // 2. Giữ khu phong_sinh không bị gỡ
            ZoneStreamer.Instance?.Keep(this, 9999f, savedPortalPos);

            // 3. Khóa di chuyển bằng DesktopViewer.UiCapture++
            DesktopViewer.UiCapture++;

            // 4. Mờ dần màn hình (~0.12s)
            yield return CoFadeOverlay(0f, 1f, 0.12f);

            // 5. Tắt viewCam của lab, bật camera phòng ảo, bật gốc phòng ảo
            if (savedViewCam != null) savedViewCam.enabled = false;
            gameObject.SetActive(true);
            if (holoCamera != null) holoCamera.enabled = true;

            UpdateUIResponsiveness();
            ResetOrbitView();
            UpdateOrbitCamera();

            // 6. Mờ sáng lại (~0.13s)
            yield return CoFadeOverlay(1f, 0f, 0.13f);

            isTransitioning = false;
        }

        public void ReturnToLab()
        {
            // GĐ3: Ưu tiên bộ máy chuyển cảnh hoàn chỉnh BioPortalTransition nếu có
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

            // 1. Mờ dần màn hình (~0.12s)
            yield return CoFadeOverlay(0f, 1f, 0.12f);

            // 2. Tắt camera phòng ảo, tắt gốc phòng ảo, bật lại viewCam của lab
            if (holoCamera != null) holoCamera.enabled = false;
            gameObject.SetActive(false);
            if (savedViewCam != null) savedViewCam.enabled = true;

            // 3. Trả về ĐÚNG vị trí và hướng nhìn đã lưu (sai số <= 1cm, <= 0.5°)
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

            // 4. Giải phóng ZoneStreamer và chờ khu sẵn sàng
            ZoneStreamer.Instance?.Release(this);
            yield return ZoneStreamer.WaitReadyAt(savedPortalPos, 5f);

            // 5. Giảm UiCapture--
            DesktopViewer.UiCapture = Mathf.Max(0, DesktopViewer.UiCapture - 1);

            // 6. Mờ sáng lại (~0.13s)
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
        // Bộ dựng vật liệu & Lưới thủ công (Procedural Mesh & Materials)
        // =====================================================================

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
            mat.SetFloat("_Blend", 0f); // Alpha blend
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

            // Nền tối #0A1222, Lưới 20x20m (#06B6D4 / #0E3A4A, alpha 0.28)
            Color bg = new Color(10f / 255f, 18f / 255f, 34f / 255f, 1f);
            Color gridLineMinor = new Color(14f / 255f, 58f / 255f, 74f / 255f, 0.28f); // #0E3A4A
            Color gridLineMajor = new Color(6f / 255f, 182f / 255f, 212f / 255f, 0.40f); // #06B6D4

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
                    // Lưới 20 ô x 20 ô trên đường kính 20m (mỗi ô 1m = size/20 px)
                    int gx = x % (size / 20);
                    int gy = y % (size / 20);

                    // Đường lưới phụ 1m
                    if (gx == 0 || gy == 0)
                    {
                        c = Color.Lerp(c, gridLineMinor, 0.55f);
                    }

                    // Đường lưới chính 5m (size / 4 px)
                    if (x % (size / 4) == 0 || y % (size / 4) == 0)
                    {
                        c = Color.Lerp(c, gridLineMajor, 0.75f);
                    }

                    // Vành ngoài cùng bo êm
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
            // Kim loại đen xanh công nghệ cao
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
            mat.SetFloat("_Blend", 1f); // Additive
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

            // Tạo dải texture gradient dọc cho trụ sáng
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
                // Đáy sáng mạnh hơn (alpha ~0.35), nhạt dần lên đỉnh (alpha ~0.04)
                float alpha = Mathf.Lerp(0.35f, 0.04f, Mathf.Pow(v, 0.7f));
                Color c = new Color(cCyan.r, cCyan.g, cCyan.b, alpha);
                for (int x = 0; x < 16; x++) px[y * 16 + x] = c;
            }
            tex.SetPixels(px);
            tex.Apply(false, true);

            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);

            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 1f); // Additive
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
                float a = i * step + Mathf.PI / 6f; // Định hướng đỉnh đẹp
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
            var tris = new int[segments * 12]; // 2 mặt nhìn thấy (double-sided)

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

                // Mặt ngoài
                tris[t++] = b0; tris[t++] = t0; tris[t++] = b1;
                tris[t++] = b1; tris[t++] = t0; tris[t++] = t1;

                // Mặt trong (nhìn xuyên qua cylinder)
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
