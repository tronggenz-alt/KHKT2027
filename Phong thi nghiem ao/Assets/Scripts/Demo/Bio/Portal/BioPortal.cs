using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Khkt.Demo;
using Khkt.Environment;
using Khkt.Interaction;
using Khkt.Interaction.Bio;
using Khkt.Input;

namespace Khkt.Demo.Bio.Portal
{
    /// <summary>
    /// BioPortal (GĐ1 - Cổng vào Phòng Sinh học 3D):
    ///   • Kiểm tra cự ly an toàn (proximity check) ở tần số 10 Hz: khoảng cách bình phương đến tâm cổng
    ///     khi tiến vào <= 2.2 m (góc nhìn <= 60 độ) thì hiện thẻ mời; lùi ra > 2.8 m thì ẩn thẻ (hysteresis).
    ///   • Vòng sáng sàn (Floor glowing ring): bán kính 0.55 m với hình lục giác nhỏ ở tâm (#22D3EE, #10B981, #06B6D4).
    ///     Hiệu ứng thở chu kỳ 4 s (độ sáng 35% <-> 55%) khi nhàn rỗi; sáng 100% và quay chậm khi thẻ mời mở.
    ///     Unlit, additive, không đổ bóng, đúng 1 draw call.
    ///   • Thẻ mời kính mờ (Frosted glass invite card) dùng GlassKit / ModernUI / UiMotion:
    ///     biểu tượng, tiêu đề "Phòng Sinh học 3D", phụ đề "Giải phẫu người thật · 15 cơ quan · 6 cảnh hiển vi",
    ///     nút chính "Vào phòng" gắn NutHieuUng.
    ///   • Giao diện thích ứng (Responsive UI):
    ///     iPad: neo giữa đáy màn hình phía trên dock, >= 65 đơn vị canvas, nhận biết safe area;
    ///     PC: gợi ý phím tắt [E] khớp HandController.
    ///   • Hoạt cảnh: UiMotion.Popup (0.95 -> 1 kèm fade 0.20 s) khi hiện; UiMotion.FadeOut (0.12 s) khi ẩn.
    ///   • Điều kiện bấm nút: chặn khi đang cầm dụng cụ ("Hãy đặt dụng cụ xuống trước"),
    ///     khi đang trong chế độ cận cảnh / màn hình máy (CloseUpView.IsActive, DesktopViewer.UiCapture > 0),
    ///     hoặc khi giáo viên khóa (ClassRules.Exam).
    ///   • GĐ1: bấm nút ghi log "[KHKT-BIO-PORTAL] Bấm vào Phòng Sinh học 3D".
    /// </summary>
    public class BioPortal : MonoBehaviour
    {
        [Header("Cấu hình cự ly (Proximity)")]
        public float showDistance = 2.2f;
        public float hideDistance = 2.8f;
        public float maxViewingAngle = 60f;
        public float checkInterval = 0.1f; // 10 Hz

        [Header("Vòng sáng sàn (Glowing Ring)")]
        public float ringRadius = 0.55f;
        public float idleBreathingPeriod = 4.0f;
        public float minBrightness = 0.35f;
        public float maxBrightness = 0.55f;
        public float rotateSpeedDegPerSec = 20.0f;

        [Header("Trạng thái")]
        [SerializeField] bool isCardShown = false;
        public bool IsCardShown => isCardShown;

        [Header("Liên kết phòng ảo (GĐ2)")]
        public HoloBioRoom targetHoloRoom;

        // Thời gian chờ phục hồi thẻ (GĐ3 cooldown)
        float cooldownTimer = 0f;

        public void SetCooldown(float duration = 0.8f)
        {
            cooldownTimer = duration;
            HideCard();
        }

        // Vòng sáng
        Transform ringTransform;
        MeshFilter ringMeshFilter;
        MeshRenderer ringMeshRenderer;
        Mesh ringMesh;
        Material ringMaterial;
        Texture2D ringTexture;
        float currentBrightness = 0.35f;
        static readonly int baseColorPropId = Shader.PropertyToID("_BaseColor");
        static readonly int colorPropId = Shader.PropertyToID("_Color");

        // Giao diện thẻ mời
        GameObject cardRoot;
        RectTransform cardRect;
        CanvasGroup cardCanvasGroup;
        Text buttonText;
        Button enterButton;
        Coroutine motionCoroutine;

        // Cập nhật giao diện
        bool wasUsingTouch;
        Rect lastSafeArea;
        int lastScreenWidth;
        int lastScreenHeight;

        // Bộ đếm cự ly 10 Hz
        float checkTimer = 0f;
        Camera cachedCam;

        void Awake()
        {
            EnsureFloorRing();
        }

        void Start()
        {
            EnsureCardUI();
        }

        void OnDestroy()
        {
            if (ringTexture != null) Destroy(ringTexture);
            if (ringMaterial != null) Destroy(ringMaterial);
            if (ringMesh != null) Destroy(ringMesh);
            if (cardRoot != null) Destroy(cardRoot);
        }

        void Update()
        {
            // 1. Cập nhật vòng sáng sàn: không cấp phát bộ nhớ (0 GC Alloc)
            UpdateRingVisuals();

            // 2. Kiểm tra cự ly ở tần số 10 Hz
            checkTimer += Time.deltaTime;
            if (checkTimer >= checkInterval)
            {
                checkTimer = 0f;
                UpdateProximity();
            }

            // 3. Phím tắt PC [E] / Enter khi thẻ mời đang mở
            if (isCardShown && !PadInput.UsingTouch)
            {
                var kb = Keyboard.current;
                if (kb != null && (kb.eKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
                {
                    OnEnterButtonClicked();
                }
            }

            // 4. Theo dõi thay đổi kích thước màn hình / safe area / touch mode khi đang hiện thẻ
            if (isCardShown)
            {
                bool touch = PadInput.UsingTouch;
                if (touch != wasUsingTouch || Screen.safeArea != lastSafeArea || Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
                {
                    UpdateResponsiveLayout();
                }
            }
        }

        // =====================================================================
        // Vòng sáng sàn (Floor Glowing Ring - 1 draw call, Unlit, Additive)
        // =====================================================================

        public void EnsureFloorRing()
        {
            if (ringTransform != null) return;

            var existing = transform.Find("FloorGlowingRing");
            if (existing != null)
            {
                ringTransform = existing;
                ringMeshFilter = ringTransform.GetComponent<MeshFilter>();
                ringMeshRenderer = ringTransform.GetComponent<MeshRenderer>();
                if (ringMeshRenderer != null) ringMaterial = ringMeshRenderer.sharedMaterial;
                return;
            }

            var ringGo = new GameObject("FloorGlowingRing");
            ringGo.transform.SetParent(transform, false);
            // Sàn tầng 2 đặt tại y = 0 tương đối; nâng nhẹ 0.005 m chống z-fighting
            ringGo.transform.localPosition = new Vector3(0f, 0.005f, 0f);
            ringGo.transform.localRotation = Quaternion.identity;
            ringTransform = ringGo.transform;

            ringMeshFilter = ringGo.AddComponent<MeshFilter>();
            ringMesh = BuildQuadMesh(ringRadius * 2f);
            ringMeshFilter.sharedMesh = ringMesh;

            ringMeshRenderer = ringGo.AddComponent<MeshRenderer>();
            ringMeshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            ringMeshRenderer.receiveShadows = false;
            ringMeshRenderer.lightProbeUsage = LightProbeUsage.Off;
            ringMeshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            ringTexture = GenerateRingTexture(512);
            ringMaterial = CreateAdditiveMaterial(ringTexture);
            ringMeshRenderer.sharedMaterial = ringMaterial;
        }

        Mesh BuildQuadMesh(float size)
        {
            float half = size * 0.5f;
            var m = new Mesh { name = "BioPortalRingQuad" };
            m.vertices = new[]
            {
                new Vector3(-half, 0f, -half),
                new Vector3(half, 0f, -half),
                new Vector3(half, 0f, half),
                new Vector3(-half, 0f, half)
            };
            m.normals = new[]
            {
                Vector3.up, Vector3.up, Vector3.up, Vector3.up
            };
            m.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateBounds();
            return m;
        }

        Material CreateAdditiveMaterial(Texture2D tex)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Transparent")
                         ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader) { name = "BioPortalRingMat", hideFlags = HideFlags.DontSave };

            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);

            // Cấu hình Unlit Additive chuẩn URP
            mat.SetFloat("_Surface", 1f); // Transparent
            mat.SetFloat("_Blend", 1f);   // Additive
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.One);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Cull", (float)CullMode.Off);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_BLENDMODE_ADD");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.SetShaderPassEnabled("DepthOnly", false);
            mat.SetShaderPassEnabled("ShadowCaster", false);

            Color initColor = new Color(minBrightness, minBrightness, minBrightness, minBrightness);
            if (mat.HasProperty(baseColorPropId)) mat.SetColor(baseColorPropId, initColor);
            if (mat.HasProperty(colorPropId)) mat.SetColor(colorPropId, initColor);

            return mat;
        }

        Texture2D GenerateRingTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "BioPortalRingTex",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };

            // Bảng màu chuẩn yêu cầu:
            // #22D3EE (cyan 400), #10B981 (emerald 500), #06B6D4 (cyan 500)
            Color cCyan = new Color(34f / 255f, 211f / 255f, 238f / 255f);
            Color cEmerald = new Color(16f / 255f, 185f / 255f, 129f / 255f);
            Color cDarkCyan = new Color(6f / 255f, 182f / 255f, 212f / 255f);

            var pixels = new Color[size * size];
            float center = (size - 1) * 0.5f;
            float maxR = center;

            // Hằng số góc hình lục giác (pi / 3 = 60 độ)
            float piDiv3 = Mathf.PI / 3f;
            float cos30 = Mathf.Cos(Mathf.PI / 6f); // sqrt(3)/2 ~ 0.866

            for (int y = 0; y < size; y++)
            {
                float dy = (y - center) / maxR; // -1 .. 1
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / maxR; // -1 .. 1
                    float r = Mathf.Sqrt(dx * dx + dy * dy);

                    if (r > 1.0f)
                    {
                        pixels[y * size + x] = Color.clear;
                        continue;
                    }

                    float angle = Mathf.Atan2(dy, dx);
                    // Góc đối xứng lục giác
                    float modAngle = Mathf.Repeat(angle + Mathf.PI / 6f, piDiv3) - Mathf.PI / 6f;
                    float dHex = r * Mathf.Cos(modAngle) / cos30; // Khoảng cách tới cạnh lục giác

                    Color col = Color.clear;

                    // 1. Hình lục giác nhỏ ở tâm (bán kính r ~ 0.16)
                    float hexR = 0.16f;
                    if (dHex <= hexR)
                    {
                        // Viền lục giác (#10B981)
                        float edgeDist = hexR - dHex;
                        if (edgeDist <= 0.025f)
                        {
                            float edgeAlpha = Mathf.Clamp01(1f - (Mathf.Abs(edgeDist - 0.0125f) / 0.0125f));
                            col += cEmerald * Mathf.Lerp(0.8f, 1.0f, edgeAlpha);
                        }
                        else
                        {
                            // Lòng lục giác: gradient êm từ #06B6D4 sang #10B981
                            float fillAlpha = Mathf.Clamp01((hexR - dHex) / hexR);
                            col += Color.Lerp(cDarkCyan, cEmerald, fillAlpha) * (fillAlpha * 0.45f);
                        }
                    }

                    // 2. Vòng tròn sáng bên ngoài tại r ~ 0.88 .. 0.96 (#22D3EE)
                    float ringTarget = 0.91f;
                    float ringDist = Mathf.Abs(r - ringTarget);
                    if (ringDist < 0.08f)
                    {
                        float ringFactor = Mathf.Exp(-ringDist * ringDist / (2f * 0.022f * 0.022f));
                        col += cCyan * ringFactor;
                    }

                    // 3. Vòng tròn phụ ở giữa tại r ~ 0.52 .. 0.58 (#06B6D4)
                    float midTarget = 0.54f;
                    float midDist = Mathf.Abs(r - midTarget);
                    if (midDist < 0.045f)
                    {
                        float midFactor = Mathf.Exp(-midDist * midDist / (2f * 0.014f * 0.014f));
                        col += cDarkCyan * (midFactor * 0.65f);
                    }

                    // 4. 6 tia nối từ 6 đỉnh lục giác tới vòng giữa
                    float vertexAngleDist = Mathf.Abs(modAngle);
                    if (vertexAngleDist < 0.015f && r >= hexR * 0.9f && r <= midTarget + 0.02f)
                    {
                        float rayFactor = (1f - vertexAngleDist / 0.015f) * 0.4f;
                        col += cEmerald * rayFactor;
                    }

                    // Đảm bảo kênh alpha tương ứng độ sáng cho additive blend
                    float lum = Mathf.Max(col.r, Mathf.Max(col.g, col.b));
                    col.a = Mathf.Clamp01(lum);

                    pixels[y * size + x] = col;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            return tex;
        }

        void UpdateRingVisuals()
        {
            if (!isCardShown)
            {
                // Nhàn rỗi: Hiệu ứng thở chu kỳ 4s (độ sáng 35% <-> 55%)
                // 2 * pi / 4.0 = pi / 2
                float phase = Time.time * (Mathf.PI * 0.5f);
                float sine = Mathf.Sin(phase); // -1 .. 1
                float norm = sine * 0.5f + 0.5f; // 0 .. 1
                currentBrightness = Mathf.Lerp(minBrightness, maxBrightness, norm);
            }
            else
            {
                // Khi thẻ mời hiện: sáng 100% và quay chậm
                currentBrightness = Mathf.MoveTowards(currentBrightness, 1.0f, Time.deltaTime * 3.5f);
                if (ringTransform != null)
                {
                    ringTransform.Rotate(Vector3.up, rotateSpeedDegPerSec * Time.deltaTime, Space.World);
                }
            }

            if (ringMaterial != null)
            {
                Color tint = new Color(currentBrightness, currentBrightness, currentBrightness, currentBrightness);
                ringMaterial.SetColor(baseColorPropId, tint);
                ringMaterial.SetColor(colorPropId, tint);
            }
        }

        // =====================================================================
        // Kiểm tra cự ly (Proximity Check - 10 Hz, không cấp phát bộ nhớ)
        // =====================================================================

        void UpdateProximity()
        {
            // Nếu đang trong thời gian hồi chiêu 0.8s sau khi thoát phòng ảo thì không hiện thẻ
            if (cooldownTimer > 0f)
            {
                cooldownTimer -= checkInterval;
                if (isCardShown) HideCard();
                return;
            }

            // Nếu đang trong chế độ cận cảnh hoặc giao diện máy đang mở thì ẩn thẻ
            if (CloseUpView.IsActive || DesktopViewer.UiCapture > 0)
            {
                if (isCardShown) HideCard();
                return;
            }

            if (cachedCam == null)
            {
                cachedCam = Camera.main;
                if (cachedCam == null) return;
            }

            Vector3 eyePos = cachedCam.transform.position;
            Vector3 portalCenter = transform.position;
            Vector3 toPortal = portalCenter - eyePos;
            float distSq = toPortal.sqrMagnitude;

            if (isCardShown)
            {
                // Ẩn khi lùi ra > 2.8 m
                if (distSq > hideDistance * hideDistance)
                {
                    HideCard();
                }
            }
            else
            {
                // Hiện khi khoảng cách <= 2.2 m và góc nhìn <= 60 độ
                if (distSq <= showDistance * showDistance)
                {
                    Vector3 forward = cachedCam.transform.forward;
                    float angle = Vector3.Angle(forward, toPortal);
                    if (angle <= maxViewingAngle)
                    {
                        ShowCard();
                    }
                }
            }
        }

        // =====================================================================
        // Giao diện thẻ mời kính mờ (Frosted Glass Invite Card)
        // =====================================================================

        void EnsureCardUI()
        {
            if (cardRoot != null) return;

            // Tìm Canvas tương tác (ưu tiên Canvas của ModernUI hoặc tạo mới)
            Canvas targetCanvas = null;
            var modern = FindAnyObjectByType<ModernUI>();
            if (modern != null && modern.Canvas != null)
            {
                targetCanvas = modern.Canvas;
            }
            else
            {
                var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
                foreach (var c in canvases)
                {
                    if (c.isRootCanvas && c.renderMode != RenderMode.WorldSpace)
                    {
                        targetCanvas = c;
                        break;
                    }
                }
            }

            if (targetCanvas == null)
            {
                var canvasGo = new GameObject("BioPortalCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                targetCanvas = canvasGo.GetComponent<Canvas>();
                targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasGo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
            }

            UiBridge.EnsureInteractive(targetCanvas);

            // Tạo Panel kính mờ
            var glassShader = Shader.Find("Khkt/KinhMo");
            var glassMat = GlassKit.Glass(glassShader);

            Vector2 cardSize = new Vector2(560, 116);
            var cardPanelImg = GlassKit.Panel(targetCanvas.transform, "TheMoiPhongSinh3D",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 65f), cardSize, glassMat, 1.2f);
            cardPanelImg.raycastTarget = true;

            cardRoot = cardPanelImg.gameObject;
            cardRect = (RectTransform)cardRoot.transform;

            cardCanvasGroup = cardRoot.AddComponent<CanvasGroup>();
            cardCanvasGroup.alpha = 0f;
            cardCanvasGroup.blocksRaycasts = false;
            cardCanvasGroup.interactable = false;

            // 1. Biểu tượng 3D Bio (Icon badge)
            var iconBox = GlassKit.Rect(cardRect, "IconBox", new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(48f, 0f), new Vector2(52f, 52f));
            var iconImg = iconBox.gameObject.AddComponent<Image>();
            iconImg.sprite = GlassKit.SolidCircle;
            iconImg.color = new Color(0.133f, 0.827f, 0.933f, 0.22f);
            iconImg.raycastTarget = false;

            var iconInner = GlassKit.Label(iconBox, "IconGlyph", "3D", 20, GlassKit.Accent, true, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(52f, 52f));
            iconInner.alignment = TextAnchor.MiddleCenter;

            // 2. Tiêu đề "Phòng Sinh học 3D"
            GlassKit.Label(cardRect, "TieuDe", "Phòng Sinh học 3D", 20, GlassKit.Ink, true,
                TextAnchor.MiddleLeft, new Vector2(92f, 18f), new Vector2(300f, 30f), new Vector2(0f, 0.5f));

            // 3. Phụ đề "Giải phẫu người thật · 15 cơ quan · 6 cảnh hiển vi"
            GlassKit.Label(cardRect, "PhuDe", "Giải phẫu người thật · 15 cơ quan · 6 cảnh hiển vi", 13, GlassKit.Dim, false,
                TextAnchor.MiddleLeft, new Vector2(92f, -16f), new Vector2(300f, 24f), new Vector2(0f, 0.5f));

            // 4. Nút chính "Vào phòng" gắn NutHieuUng
            enterButton = GlassKit.MakeButton(cardRect, "NutVaoPhong", "Vào phòng",
                new Vector2(-26f, 0f), new Vector2(132f, 48f), OnEnterButtonClicked, primary: true, fontSize: 16, anchor: new Vector2(1f, 0.5f));
            buttonText = enterButton.GetComponentInChildren<Text>();

            UpdateResponsiveLayout();
            cardRoot.SetActive(false);
        }

        public void ShowCard()
        {
            if (isCardShown) return;
            isCardShown = true;

            EnsureCardUI();
            if (cardRoot == null) return;

            cardRoot.SetActive(true);
            UpdateResponsiveLayout();

            cardCanvasGroup.blocksRaycasts = true;
            cardCanvasGroup.interactable = true;

            if (motionCoroutine != null) StopCoroutine(motionCoroutine);
            // Hoạt cảnh: UiMotion.Popup (0.95 -> 1 kèm fade 0.20s)
            motionCoroutine = UiMotion.Popup(this, cardRect, cardCanvasGroup, 0.20f);
        }

        public void HideCard()
        {
            if (!isCardShown) return;
            isCardShown = false;

            if (cardRoot == null || cardCanvasGroup == null) return;

            cardCanvasGroup.blocksRaycasts = false;
            cardCanvasGroup.interactable = false;

            if (motionCoroutine != null) StopCoroutine(motionCoroutine);
            // Hoạt cảnh: UiMotion.FadeOut (0.12s)
            motionCoroutine = UiMotion.FadeOut(this, cardCanvasGroup, 0.12f, () =>
            {
                if (!isCardShown && cardRoot != null)
                {
                    cardRoot.SetActive(false);
                }
            });
        }

        // =====================================================================
        // Bố cục thích ứng (Responsive UI - iPad / PC)
        // =====================================================================

        void UpdateResponsiveLayout()
        {
            if (cardRect == null) return;

            wasUsingTouch = PadInput.UsingTouch;
            lastSafeArea = Screen.safeArea;
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;

            float sh = Screen.height > 0 ? (float)Screen.height : 1080f;
            // Tính toán safe area đáy theo đơn vị canvas chuẩn (chiều cao quy chiếu 1080)
            float bottomInsetPixels = Screen.safeArea.yMin;
            float bottomInsetCanvas = (bottomInsetPixels / sh) * 1080f;

            if (wasUsingTouch)
            {
                // iPad: Neo giữa đáy phía trên dock, >= 65 đơn vị canvas, nhận biết safe area
                float bottomOffset = Mathf.Max(65f, bottomInsetCanvas + 65f);
                cardRect.anchorMin = new Vector2(0.5f, 0f);
                cardRect.anchorMax = new Vector2(0.5f, 0f);
                cardRect.pivot = new Vector2(0.5f, 0f);
                cardRect.anchoredPosition = new Vector2(0f, bottomOffset);

                if (buttonText != null) buttonText.text = "Vào phòng";
            }
            else
            {
                // PC: Neo giữa đáy, có gợi ý phím tắt [E] khớp phong cách HandController
                cardRect.anchorMin = new Vector2(0.5f, 0f);
                cardRect.anchorMax = new Vector2(0.5f, 0f);
                cardRect.pivot = new Vector2(0.5f, 0f);
                cardRect.anchoredPosition = new Vector2(0f, 65f);

                if (buttonText != null) buttonText.text = "Vào phòng  [E]";
            }
        }

        // =====================================================================
        // Xử lý sự kiện bấm nút (Button Click Handling & Conditions)
        // =====================================================================

        public void OnEnterButtonClicked()
        {
            // Điều kiện 1: Chặn khi đang trong cận cảnh hoặc màn hình máy hoạt động
            if (CloseUpView.IsActive || DesktopViewer.UiCapture > 0)
            {
                return;
            }

            // Điều kiện 2: Chặn khi đang cầm dụng cụ ("Hãy đặt dụng cụ xuống trước")
            if (IsHoldingEquipment())
            {
                LabMessages.Say("Hãy đặt dụng cụ xuống trước", LabMessages.Tone.Warn);
                return;
            }

            // Điều kiện 3: Chặn khi giáo viên khóa (ClassRules)
            if (ClassRules.Exam)
            {
                LabMessages.Say("Đang trong giờ kiểm tra: không thể vào phòng khác", LabMessages.Tone.Warn);
                return;
            }

            // GĐ1: Bấm nút ghi log chuẩn yêu cầu
            Debug.Log("[KHKT-BIO-PORTAL] Bấm vào Phòng Sinh học 3D");

            // GĐ3: Khởi chạy chuyển cảnh FSM BioPortalTransition sang HoloBioRoom
            var room = targetHoloRoom != null ? targetHoloRoom : FindAnyObjectByType<HoloBioRoom>(FindObjectsInactive.Include);
            if (room != null)
            {
                HideCard();
                var transition = BioPortalTransition.Instance;
                if (transition == null)
                {
                    var transGo = new GameObject("BioPortalTransition");
                    transition = transGo.AddComponent<BioPortalTransition>();
                }
                transition.StartOpenTransition(this, room);
            }
        }

        bool IsHoldingEquipment()
        {
            var hand = HandController.Active;
            if (hand != null)
            {
                if (hand.Held != null || hand.HeldPart != null) return true;
            }

            var hc = FindAnyObjectByType<HandController>();
            if (hc != null && (hc.Held != null || hc.HeldPart != null)) return true;

            return false;
        }
    }
}
