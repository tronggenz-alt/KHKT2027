using System;
using System.Collections.Generic;
using Khkt.Avatar;
using UnityEngine;

namespace Khkt.Demo
{
    /// <summary>
    /// Bàn tay người giải phẫu 3 đốt/ngón, ngón cái đối diện 3 đoạn, lòng/mu tay cong, móng tay,
    /// shader da Khkt/DaNguoi, chế độ găng nitrile xanh (tùy chọn tắt/bật), cầu nối liền cổ tay áo blouse,
    /// bảng tư thế chuyển mượt 0,15 s, IK chống xuyên vật sai lệch ≤ 3 mm và ánh xạ 21 điểm MediaPipe.
    /// Hệ tọa độ: +z hướng các ngón tay, +y là mu bàn tay, lòng bàn tay quay xuống (-y).
    /// </summary>
    public class HandRig : MonoBehaviour
    {
        public const string GlovesPrefKey = "KHKT_DEO_GANG_TAY";

        static bool glovesLoaded;
        static bool wearGloves = true;
        static readonly List<HandRig> ActiveRigs = new List<HandRig>();

        /// <summary>Bật/tắt găng tay nitrile xanh (mặc định BẬT trong phòng lab vì an toàn).</summary>
        public static bool WearGloves
        {
            get
            {
                if (!glovesLoaded)
                {
                    glovesLoaded = true;
                    wearGloves = PlayerPrefs.GetInt(GlovesPrefKey, 1) != 0;
                }
                return wearGloves;
            }
            set
            {
                glovesLoaded = true;
                if (wearGloves == value) return;
                wearGloves = value;
                PlayerPrefs.SetInt(GlovesPrefKey, value ? 1 : 0);
                PlayerPrefs.Save();
                for (int i = ActiveRigs.Count - 1; i >= 0; i--)
                {
                    if (ActiveRigs[i] == null) ActiveRigs.RemoveAt(i);
                    else ActiveRigs[i].RefreshAppearance();
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            glovesLoaded = false;
            wearGloves = true;
            ActiveRigs.Clear();
        }

        public Color gloveColor = new Color(0.22f, 0.50f, 0.88f);
        public Color skinColor = new Color(0.87f, 0.69f, 0.59f);
        public Color sleeveColor = new Color(0.94f, 0.95f, 0.97f);
        public Color cuffTrimColor = new Color(0.75f, 0.86f, 0.96f);

        [Range(0, 1)] public float curl;
        [Tooltip("0 = tay phải, 1 = tay trái")]
        public int side = 0;

        /// <summary>Điểm nắm: dụng cụ đang cầm treo ở đây.</summary>
        public Transform Grip { get; private set; }
        /// <summary>Đầu nối cổ tay áo blouse để ArmRig bám liền không bị hở cổ tay.</summary>
        public Transform WristAttach { get; private set; }

        public HandPoseId CurrentPose { get; private set; } = HandPoseId.Xoe;
        public HandPoseId? PoseOverride { get; set; }
        public Transform TargetContactObject { get; set; }
        public bool SkipIdleUpdate { get; set; }
        public float LastMaxSurfaceErrorMm { get; private set; }
        public int LastPenetrationCount { get; private set; }

        // Tỉ lệ giải phẫu đốt ngón tay (m): Trỏ, Giữa, Nhẫn, Út — đốt gần > giữa > xa
        static readonly float[] ProxLen = { 0.038f, 0.043f, 0.040f, 0.032f };
        static readonly float[] MidLen = { 0.023f, 0.026f, 0.024f, 0.018f };
        static readonly float[] DistLen = { 0.017f, 0.018f, 0.017f, 0.015f };
        static readonly float[] FingerW = { 0.0165f, 0.0172f, 0.0162f, 0.0138f };

        static readonly float[] McpX = { -0.029f, -0.0095f, 0.010f, 0.0285f };
        static readonly float[] McpZ = { 0.047f, 0.051f, 0.048f, 0.041f };
        static readonly float[] McpArchY = { 0.001f, 0.0042f, 0.0022f, -0.002f };

        const float ThumbMetaLen = 0.036f, ThumbProxLen = 0.027f, ThumbDistLen = 0.020f;
        const float ThumbW = 0.0175f;

        Transform palmRoot;
        readonly Transform[] proximal = new Transform[4];
        readonly Transform[] middle = new Transform[4];
        readonly Transform[] distal = new Transform[4];
        readonly Transform[] fingerTips = new Transform[4];
        readonly Renderer[] nailRenderers = new Renderer[5];
        readonly Renderer[] handSurfaceRenderers = new Renderer[48];
        readonly Renderer[] cuffRenderers = new Renderer[4];
        readonly Renderer[] sleeveRenderers = new Renderer[4];
        int surfaceCount, cuffCount, sleeveCount;

        Transform thumbCmc, thumbMcp, thumbIp, thumbTip;

        Material skinMat, gloveMat, gloveCuffMat, nailMat, gloveNailMat, sleeveMat, trimMat;
        MaterialPropertyBlock tintBlock;

        HandPoseId blendTargetPose = HandPoseId.Xoe;
        HandPoseData blendFromData, activePoseData;
        float blendElapsed = HandPoseTable.TransitionDurationS;
        float shownCurl = -1f;
        bool built;
        bool drivingByLandmarks;

        Transform cachedContactTarget;
        Collider[] cachedContactCols = Array.Empty<Collider>();
        readonly RaycastHit[] rayBuf = new RaycastHit[16];

        void Awake()
        {
            if (!ActiveRigs.Contains(this)) ActiveRigs.Add(this);
            EnsureBuilt();
        }

        void OnDestroy() => ActiveRigs.Remove(this);

        public void EnsureBuilt()
        {
            if (built) return;
            built = true;

            tintBlock = new MaterialPropertyBlock();

            Shader skinShader = Shader.Find("Khkt/DaNguoi") ?? Shader.Find("Universal Render Pipeline/Lit");
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");

            skinMat = new Material(skinShader) { hideFlags = HideFlags.DontSave };
            skinMat.SetColor("_BaseColor", skinColor);
            skinMat.SetFloat("_Smoothness", 0.26f);

            gloveMat = new Material(lit) { hideFlags = HideFlags.DontSave };
            gloveMat.SetColor("_BaseColor", gloveColor);
            gloveMat.SetFloat("_Smoothness", 0.42f);

            gloveCuffMat = new Material(lit) { hideFlags = HideFlags.DontSave };
            gloveCuffMat.SetColor("_BaseColor", gloveColor * 0.84f);
            gloveCuffMat.SetFloat("_Smoothness", 0.36f);

            nailMat = new Material(lit) { hideFlags = HideFlags.DontSave };
            nailMat.SetColor("_BaseColor", new Color(0.95f, 0.84f, 0.80f));
            nailMat.SetFloat("_Smoothness", 0.38f);

            gloveNailMat = new Material(lit) { hideFlags = HideFlags.DontSave };
            gloveNailMat.SetColor("_BaseColor", Color.Lerp(gloveColor, Color.white, 0.14f));
            gloveNailMat.SetFloat("_Smoothness", 0.48f);

            sleeveMat = new Material(lit) { hideFlags = HideFlags.DontSave };
            sleeveMat.SetColor("_BaseColor", sleeveColor);
            sleeveMat.SetFloat("_Smoothness", 0.14f);

            trimMat = new Material(lit) { hideFlags = HideFlags.DontSave };
            trimMat.SetColor("_BaseColor", cuffTrimColor);
            trimMat.SetFloat("_Smoothness", 0.22f);

            float sign = side == 0 ? 1f : -1f;

            palmRoot = new GameObject("Gốc bàn tay").transform;
            palmRoot.SetParent(transform, false);

            // Lòng bàn tay + mu tay cong tự nhiên + mô cái + mô út + 4 khớp đốt bàn
            RegisterSurface(Part(PrimitiveType.Sphere, palmRoot, "Lòng bàn tay chính",
                new Vector3(0f, -0.001f, 0.004f), new Vector3(0.078f, 0.024f, 0.088f), Vector3.zero, gloveMat));

            for (int i = 0; i < 4; i++)
            {
                float x = McpX[i] * sign;
                float yArch = McpArchY[i];
                Vector3 metacarpalMid = new Vector3(x * 0.72f, yArch * 0.65f + 0.002f, McpZ[i] * 0.35f - 0.010f);
                RegisterSurface(Part(PrimitiveType.Capsule, palmRoot, $"Xương bàn {i + 1}",
                    metacarpalMid, new Vector3(0.020f, 0.036f, 0.017f), new Vector3(90f, (i - 1.5f) * 3.5f * sign, 0f), gloveMat));

                // Gồ khớp đốt bàn (knuckle dome)
                RegisterSurface(Part(PrimitiveType.Sphere, palmRoot, $"Khớp bàn {i + 1}",
                    new Vector3(x, yArch + 0.0015f, McpZ[i]), Vector3.one * (FingerW[i] * 1.12f), Vector3.zero, gloveMat));
            }

            // Mô cái (thenar eminence) và mô út (hypothenar pad) tạo độ dày gan tay thật
            RegisterSurface(Part(PrimitiveType.Sphere, palmRoot, "Mô cái",
                new Vector3(-0.024f * sign, -0.006f, -0.008f), new Vector3(0.034f, 0.024f, 0.048f),
                new Vector3(8f, -18f * sign, 14f * sign), gloveMat));
            RegisterSurface(Part(PrimitiveType.Sphere, palmRoot, "Mô út",
                new Vector3(0.028f * sign, -0.004f, -0.002f), new Vector3(0.025f, 0.021f, 0.058f),
                new Vector3(4f, 8f * sign, 0f), gloveMat));

            // Cổ tay và vòng viền găng tay / ống tay áo blouse nối liền mạch
            RegisterSurface(Part(PrimitiveType.Sphere, palmRoot, "Cổ tay",
                new Vector3(0f, 0.0005f, -0.044f), new Vector3(0.054f, 0.034f, 0.046f), Vector3.zero, gloveMat));
            RegisterCuff(Part(PrimitiveType.Cylinder, palmRoot, "Cổ găng nitrile",
                new Vector3(0f, 0.001f, -0.056f), new Vector3(0.056f, 0.015f, 0.042f), new Vector3(90f, 0f, 0f), gloveCuffMat));
            RegisterSleeve(Part(PrimitiveType.Cylinder, palmRoot, "Cổ tay áo blouse",
                new Vector3(0f, 0.001f, -0.106f), new Vector3(0.064f, 0.038f, 0.050f), new Vector3(90f, 0f, 0f), sleeveMat));
            RegisterSleeve(Part(PrimitiveType.Cylinder, palmRoot, "Viền tay áo blouse",
                new Vector3(0f, 0.001f, -0.068f), new Vector3(0.066f, 0.006f, 0.052f), new Vector3(90f, 0f, 0f), trimMat), isTrim: true);

            // 4 ngón tay: 3 đốt giảm dần + khớp nối + đệm đầu ngón + móng tay
            for (int i = 0; i < 4; i++)
            {
                float w1 = FingerW[i];
                float w2 = w1 * 0.87f;
                float w3 = w1 * 0.74f;
                float l1 = ProxLen[i], l2 = MidLen[i], l3 = DistLen[i];

                var jMcp = Joint(palmRoot, $"Ngón {i + 1} MCP", new Vector3(McpX[i] * sign, McpArchY[i], McpZ[i]), Vector3.zero);
                RegisterSurface(Part(PrimitiveType.Capsule, jMcp, "Đốt gần",
                    new Vector3(0f, 0f, l1 * 0.5f), new Vector3(w1, Mathf.Max(w1 * 0.52f, l1 * 0.52f), w1), new Vector3(90f, 0f, 0f), gloveMat));

                var jPip = Joint(jMcp, "PIP", new Vector3(0f, 0f, l1), Vector3.zero);
                RegisterSurface(Part(PrimitiveType.Sphere, jPip, "Khớp giữa",
                    Vector3.zero, Vector3.one * (w1 * 0.96f), Vector3.zero, gloveMat));
                RegisterSurface(Part(PrimitiveType.Capsule, jPip, "Đốt giữa",
                    new Vector3(0f, 0f, l2 * 0.5f), new Vector3(w2, Mathf.Max(w2 * 0.52f, l2 * 0.52f), w2), new Vector3(90f, 0f, 0f), gloveMat));

                var jDip = Joint(jPip, "DIP", new Vector3(0f, 0f, l2), Vector3.zero);
                RegisterSurface(Part(PrimitiveType.Sphere, jDip, "Khớp xa",
                    Vector3.zero, Vector3.one * (w2 * 0.94f), Vector3.zero, gloveMat));
                RegisterSurface(Part(PrimitiveType.Capsule, jDip, "Đốt xa",
                    new Vector3(0f, 0f, l3 * 0.48f), new Vector3(w3, Mathf.Max(w3 * 0.52f, l3 * 0.52f), w3), new Vector3(90f, 0f, 0f), gloveMat));

                // Móng tay mặt mu đốt xa
                var nailGo = Part(PrimitiveType.Cube, jDip, "Móng tay",
                    new Vector3(0f, w3 * 0.39f, l3 * 0.58f), new Vector3(w3 * 0.72f, 0.0018f, l3 * 0.62f),
                    new Vector3(5f, 0f, 0f), gloveNailMat);
                nailRenderers[i] = nailGo.GetComponent<Renderer>();

                var tip = new GameObject($"Đầu ngón {i + 1}").transform;
                tip.SetParent(jDip, false);
                tip.localPosition = new Vector3(0f, -w3 * 0.20f, l3);

                proximal[i] = jMcp;
                middle[i] = jPip;
                distal[i] = jDip;
                fingerTips[i] = tip;
            }

            // Ngón cái 3 đoạn: Xương bàn (CMC) -> Đốt gần (MCP) -> Đốt xa (IP) + Móng cái
            thumbCmc = Joint(palmRoot, "Ngón cái CMC", new Vector3(-0.025f * sign, -0.007f, -0.022f), Vector3.zero);
            RegisterSurface(Part(PrimitiveType.Capsule, thumbCmc, "Xương bàn ngón cái",
                new Vector3(0f, 0f, ThumbMetaLen * 0.5f), new Vector3(ThumbW * 1.15f, ThumbMetaLen * 0.52f, ThumbW * 1.1f),
                new Vector3(90f, 0f, 0f), gloveMat));

            thumbMcp = Joint(thumbCmc, "Ngón cái MCP", new Vector3(0f, 0f, ThumbMetaLen), Vector3.zero);
            RegisterSurface(Part(PrimitiveType.Sphere, thumbMcp, "Khớp cái MCP",
                Vector3.zero, Vector3.one * (ThumbW * 1.02f), Vector3.zero, gloveMat));
            RegisterSurface(Part(PrimitiveType.Capsule, thumbMcp, "Đốt gần ngón cái",
                new Vector3(0f, 0f, ThumbProxLen * 0.5f), new Vector3(ThumbW * 0.92f, ThumbProxLen * 0.52f, ThumbW * 0.92f),
                new Vector3(90f, 0f, 0f), gloveMat));

            thumbIp = Joint(thumbMcp, "Ngón cái IP", new Vector3(0f, 0f, ThumbProxLen), Vector3.zero);
            RegisterSurface(Part(PrimitiveType.Sphere, thumbIp, "Khớp cái IP",
                Vector3.zero, Vector3.one * (ThumbW * 0.88f), Vector3.zero, gloveMat));
            RegisterSurface(Part(PrimitiveType.Capsule, thumbIp, "Đốt xa ngón cái",
                new Vector3(0f, 0f, ThumbDistLen * 0.48f), new Vector3(ThumbW * 0.78f, ThumbDistLen * 0.54f, ThumbW * 0.78f),
                new Vector3(90f, 0f, 0f), gloveMat));

            var thumbNailGo = Part(PrimitiveType.Cube, thumbIp, "Móng ngón cái",
                new Vector3(0f, ThumbW * 0.34f, ThumbDistLen * 0.56f), new Vector3(ThumbW * 0.62f, 0.0018f, ThumbDistLen * 0.60f),
                new Vector3(6f, 0f, 0f), gloveNailMat);
            nailRenderers[4] = thumbNailGo.GetComponent<Renderer>();

            thumbTip = new GameObject("Đầu ngón cái").transform;
            thumbTip.SetParent(thumbIp, false);
            thumbTip.localPosition = new Vector3(0f, -ThumbW * 0.16f, ThumbDistLen);

            Grip = new GameObject("Điểm nắm").transform;
            Grip.SetParent(transform, false);
            Grip.localPosition = new Vector3(0f, -0.032f, 0.044f);

            WristAttach = new GameObject("Điểm nối cổ tay").transform;
            WristAttach.SetParent(palmRoot, false);
            WristAttach.localPosition = new Vector3(0f, 0.001f, -0.068f);

            activePoseData = HandPoseTable.Get(HandPoseId.Xoe);
            blendFromData = activePoseData;
            ApplyPoseData(activePoseData);
            RefreshAppearance();
        }

        /// <summary>Đổi màu áo/viền tay áo theo vai trò trong phòng học chung (Giáo viên vs Học sinh).</summary>
        public void SetSleeveColors(Color coat, Color trim)
        {
            EnsureBuilt();
            sleeveColor = coat;
            cuffTrimColor = trim;
            if (sleeveMat != null) sleeveMat.SetColor("_BaseColor", coat);
            if (trimMat != null) trimMat.SetColor("_BaseColor", trim);
        }

        public void SetSleeveVisible(bool visible)
        {
            EnsureBuilt();
            for (int i = 0; i < sleeveCount; i++)
                if (sleeveRenderers[i] != null) sleeveRenderers[i].enabled = visible;
        }

        public void SetRenderEnabled(bool enabled)
        {
            EnsureBuilt();
            for (int i = 0; i < surfaceCount; i++)
                if (handSurfaceRenderers[i] != null) handSurfaceRenderers[i].enabled = enabled;
            for (int i = 0; i < sleeveCount; i++)
                if (sleeveRenderers[i] != null) sleeveRenderers[i].enabled = enabled;
            for (int i = 0; i < nailRenderers.Length; i++)
                if (nailRenderers[i] != null) nailRenderers[i].enabled = enabled;
            for (int i = 0; i < cuffCount; i++)
                if (cuffRenderers[i] != null) cuffRenderers[i].enabled = enabled && WearGloves;
            if (enabled) RefreshAppearance();
        }

        public void RefreshAppearance()
        {
            if (!built) return;
            bool gloved = WearGloves;
            Material bodyMat = gloved ? gloveMat : skinMat;
            Material nail = gloved ? gloveNailMat : nailMat;

            for (int i = 0; i < surfaceCount; i++)
                if (handSurfaceRenderers[i] != null) handSurfaceRenderers[i].sharedMaterial = bodyMat;
            for (int i = 0; i < cuffCount; i++)
            {
                if (cuffRenderers[i] != null)
                {
                    cuffRenderers[i].enabled = gloved;
                    cuffRenderers[i].sharedMaterial = gloveCuffMat;
                }
            }
            for (int i = 0; i < nailRenderers.Length; i++)
                if (nailRenderers[i] != null) nailRenderers[i].sharedMaterial = nail;
        }

        public void SetTargetPose(HandPoseId pose, bool immediate = false)
        {
            EnsureBuilt();
            HandPoseId wanted = PoseOverride ?? pose;
            if (immediate)
            {
                CurrentPose = wanted;
                blendTargetPose = wanted;
                activePoseData = HandPoseTable.Get(wanted);
                blendFromData = activePoseData;
                blendElapsed = HandPoseTable.TransitionDurationS;
                ApplyPoseData(activePoseData);
                return;
            }
            if (wanted == blendTargetPose) return;
            blendFromData = activePoseData;
            blendTargetPose = wanted;
            CurrentPose = wanted;
            blendElapsed = 0f;
        }

        void Update()
        {
            if (!built || SkipIdleUpdate || drivingByLandmarks) return;

            // Khả năng tương thích: nếu caller chỉ chỉnh `curl` (0..1) trực tiếp mà chưa set tư thế bảng
            if (PoseOverride.HasValue)
            {
                if (blendTargetPose != PoseOverride.Value) SetTargetPose(PoseOverride.Value);
            }
            else if (!Mathf.Approximately(curl, shownCurl))
            {
                shownCurl = curl;
                if (TargetContactObject == null && CurrentPose <= HandPoseId.ChumCam)
                {
                    if (curl > 0.6f) SetTargetPose(HandPoseId.ChumCam);
                    else if (curl > 0.15f) SetTargetPose(HandPoseId.Nghi);
                    else SetTargetPose(HandPoseId.Xoe);
                }
            }

            StepPoseAndContact(Time.unscaledDeltaTime);
        }

        public void ClearLandmarkControl() => drivingByLandmarks = false;

        /// <summary>Cập nhật chuyển tư thế 0,15 s và chạy raycast + IK 2 khớp chống xuyên vật.</summary>
        public void StepPoseAndContact(float dt)
        {
            EnsureBuilt();
            drivingByLandmarks = false;

            if (blendElapsed < HandPoseTable.TransitionDurationS)
            {
                blendElapsed += Mathf.Max(0f, dt);
                float u = Mathf.Clamp01(blendElapsed / HandPoseTable.TransitionDurationS);
                float smooth = u * u * (3f - 2f * u);
                activePoseData = HandPoseData.Lerp(blendFromData, HandPoseTable.Get(blendTargetPose), smooth);
            }
            else
            {
                activePoseData = HandPoseTable.Get(blendTargetPose);
            }

            ApplyPoseData(activePoseData);

            if (TargetContactObject != null)
                SolveSurfaceContactIk();
            else
            {
                LastMaxSurfaceErrorMm = 0f;
                LastPenetrationCount = 0;
            }
        }

        void ApplyPoseData(HandPoseData d)
        {
            d = HandPoseTable.ClampAll(d);
            float sign = side == 0 ? 1f : -1f;
            palmRoot.localPosition = new Vector3(d.palmLocalOffset.x * sign, d.palmLocalOffset.y, d.palmLocalOffset.z);
            palmRoot.localEulerAngles = new Vector3(d.wristEulerOffset.x, d.wristEulerOffset.y * sign, d.wristEulerOffset.z * sign);

            for (int i = 0; i < 4; i++)
            {
                FingerPose f = d.GetFinger(i);
                ApplyFingerAngles(i, f.mcpFlex, f.mcpSpread * sign, f.pipFlex, f.dipFlex);
            }

            ApplyThumbAngles(d.thumb, sign);
        }

        void ApplyFingerAngles(int i, float mcpFlex, float mcpSpreadSigned, float pipFlex, float dipFlex)
        {
            mcpFlex = Mathf.Clamp(mcpFlex, HandPoseTable.FingerMcpMinFlex, HandPoseTable.FingerMcpMaxFlex);
            mcpSpreadSigned = Mathf.Clamp(mcpSpreadSigned, HandPoseTable.FingerMcpMinSpread, HandPoseTable.FingerMcpMaxSpread);
            pipFlex = Mathf.Clamp(pipFlex, HandPoseTable.FingerPipMinFlex, HandPoseTable.FingerPipMaxFlex);
            dipFlex = Mathf.Clamp(dipFlex, HandPoseTable.FingerDipMinFlex, HandPoseTable.FingerDipMaxFlex);

            proximal[i].localEulerAngles = new Vector3(mcpFlex, mcpSpreadSigned, 0f);
            middle[i].localEulerAngles = new Vector3(pipFlex, 0f, 0f);
            distal[i].localEulerAngles = new Vector3(dipFlex, 0f, 0f);
        }

        void ApplyThumbAngles(ThumbPose t, float sign)
        {
            t = HandPoseTable.ClampThumb(t);
            thumbCmc.localEulerAngles = new Vector3(t.cmcFlex, t.cmcOppose * sign, t.cmcPronate * sign);
            thumbMcp.localEulerAngles = new Vector3(t.mcpFlex, 0f, 0f);
            thumbIp.localEulerAngles = new Vector3(t.ipFlex, 0f, 0f);
        }

        /// <summary>
        /// Chống xuyên vật: bật tạm collider của dụng cụ đích trong khung tính, raycast từ ngoài vào tâm
        /// tìm điểm bề mặt, giải IK 2 khớp trong giới hạn sinh lý để đầu ngón nằm sát mặt dụng cụ (sai số ≤ 3 mm).
        /// </summary>
        void SolveSurfaceContactIk()
        {
            LastMaxSurfaceErrorMm = 0f;
            LastPenetrationCount = 0;
            if (TargetContactObject == null) return;

            if (cachedContactTarget != TargetContactObject)
            {
                cachedContactTarget = TargetContactObject;
                cachedContactCols = TargetContactObject.GetComponentsInChildren<Collider>(true);
            }
            if (cachedContactCols == null || cachedContactCols.Length == 0) return;

            bool[] prevEnabled = new bool[cachedContactCols.Length];
            Bounds toolBounds = default;
            bool hasBounds = false;

            try
            {
                for (int c = 0; c < cachedContactCols.Length; c++)
                {
                    var col = cachedContactCols[c];
                    if (col == null) continue;
                    prevEnabled[c] = col.enabled;
                    if (!col.isTrigger)
                    {
                        col.enabled = true;
                        if (!hasBounds) { toolBounds = col.bounds; hasBounds = true; }
                        else toolBounds.Encapsulate(col.bounds);
                    }
                }
                if (!hasBounds) return;
                Physics.SyncTransforms();

                Vector3 toolCenter = toolBounds.center;
                Vector3 toolAxis = TargetContactObject.up;
                if (toolAxis.sqrMagnitude < 1e-4f) toolAxis = Vector3.up;
                toolAxis.Normalize();
                float maxExtent = Mathf.Max(toolBounds.extents.magnitude, 0.04f);

                // Tư thế cầm tinh (pipet, khóa buret, kẹp, que) chỉ ép các ngón tham gia kẹp chính + ngón cái
                bool precisionGrip = CurrentPose == HandPoseId.Pipet ||
                                     CurrentPose == HandPoseId.KhoaBuret ||
                                     CurrentPose == HandPoseId.Kep ||
                                     CurrentPose == HandPoseId.Que;
                int fingerCount = precisionGrip ? 2 : 4;
                float sign = side == 0 ? 1f : -1f;

                for (int i = 0; i < fingerCount; i++)
                {
                    FingerPose basePose = activePoseData.GetFinger(i);
                    float padR = FingerW[i] * 0.37f;
                    SolveSingleFingerContact(i, basePose, sign, toolCenter, toolAxis, maxExtent, padR);
                }

                SolveThumbContact(activePoseData.thumb, sign, toolCenter, toolAxis, maxExtent);
            }
            finally
            {
                for (int c = 0; c < cachedContactCols.Length; c++)
                {
                    var col = cachedContactCols[c];
                    if (col != null) col.enabled = prevEnabled[c];
                }
            }
        }

        void SolveSingleFingerContact(int i, FingerPose f, float sign, Vector3 toolCenter, Vector3 toolAxis, float maxExtent, float padR)
        {
            Vector3 mcpPos = proximal[i].position;
            Vector3 curTip = fingerTips[i].position;

            Vector3 refPt = mcpPos * 0.35f + curTip * 0.65f;
            if (!TryPerpSurfaceHit(refPt, mcpPos, toolCenter, toolAxis, maxExtent, out RaycastHit bestHit))
                return;

            Vector3 targetTip = bestHit.point + bestHit.normal * (padR + 0.0005f);

            float bestMcp = f.mcpFlex;
            float bestSpread = Mathf.Clamp(f.mcpSpread * sign, HandPoseTable.FingerMcpMinSpread, HandPoseTable.FingerMcpMaxSpread);
            float bestPip = f.pipFlex;
            float baseCost = FingerCost(i, bestMcp, bestSpread, bestPip, bestHit.point, bestHit.normal, targetTip, padR);

            // IK 2 khớp giải tích làm nghiệm khởi đầu
            Vector3 localInPalm = palmRoot.InverseTransformPoint(targetTip) - proximal[i].localPosition;
            float estSpread = Mathf.Clamp(Mathf.Atan2(localInPalm.x, Mathf.Max(0.01f, Mathf.Sqrt(localInPalm.y * localInPalm.y + localInPalm.z * localInPalm.z))) * Mathf.Rad2Deg,
                HandPoseTable.FingerMcpMinSpread, HandPoseTable.FingerMcpMaxSpread);
            Quaternion spreadWorld = palmRoot.rotation * Quaternion.Euler(0f, estSpread, 0f);
            Vector3 localDelta = Quaternion.Inverse(spreadWorld) * (targetTip - mcpPos);
            float u = localDelta.z;
            float v = -localDelta.y;
            float r = Mathf.Sqrt(u * u + v * v);

            float l1 = ProxLen[i];
            float l2 = MidLen[i] + DistLen[i] * 0.88f;
            float d = Mathf.Clamp(r, Mathf.Abs(l1 - l2) + 0.002f, l1 + l2 - 0.001f);

            float cosPip = Mathf.Clamp((d * d - l1 * l1 - l2 * l2) / (2f * l1 * l2), -1f, 1f);
            float pip = Mathf.Clamp(Mathf.Rad2Deg * Mathf.Acos(cosPip), HandPoseTable.FingerPipMinFlex, HandPoseTable.FingerPipMaxFlex);
            float alpha = Mathf.Atan2(v, u) * Mathf.Rad2Deg;
            float beta = Mathf.Atan2(l2 * Mathf.Sin(pip * Mathf.Deg2Rad), l1 + l2 * cosPip) * Mathf.Rad2Deg;
            float mcp = Mathf.Clamp(alpha - beta, HandPoseTable.FingerMcpMinFlex, HandPoseTable.FingerMcpMaxFlex);
            float dip = Mathf.Clamp(pip * 0.65f, HandPoseTable.FingerDipMinFlex, HandPoseTable.FingerDipMaxFlex);

            ApplyFingerAngles(i, mcp, estSpread, pip, dip);

            // Tinh chỉnh hình học 12 bước theo vị trí đầu ngón thực tế
            Vector3 flexAxisWorld = spreadWorld * Vector3.right;
            for (int pass = 0; pass < 12; pass++)
            {
                Vector3 tipNow = fingerTips[i].position;
                if ((targetTip - tipNow).sqrMagnitude <= 0.0010f * 0.0010f) break;

                float dNow = Vector3.Distance(mcpPos, tipNow);
                float dTarget = Vector3.Distance(mcpPos, targetTip);
                pip = Mathf.Clamp(pip + (dNow - dTarget) * 850f, HandPoseTable.FingerPipMinFlex, HandPoseTable.FingerPipMaxFlex);
                dip = Mathf.Clamp(pip * 0.65f, HandPoseTable.FingerDipMinFlex, HandPoseTable.FingerDipMaxFlex);
                ApplyFingerAngles(i, mcp, estSpread, pip, dip);

                tipNow = fingerTips[i].position;
                float dAngle = Vector3.SignedAngle(tipNow - mcpPos, targetTip - mcpPos, flexAxisWorld);
                mcp = Mathf.Clamp(mcp + dAngle * 0.82f, HandPoseTable.FingerMcpMinFlex, HandPoseTable.FingerMcpMaxFlex);
                ApplyFingerAngles(i, mcp, estSpread, pip, dip);
            }

            float ikCost = FingerCost(i, mcp, estSpread, pip, bestHit.point, bestHit.normal, targetTip, padR);
            if (ikCost < baseCost)
            {
                bestMcp = mcp;
                bestSpread = estSpread;
                bestPip = pip;
            }

            // Tìm kiếm tọa độ giảm chấn trên (mcp, pip, spread) tránh kẹt cực tiểu địa phương và chống xuyên
            RefineFingerDescent(i, ref bestMcp, ref bestSpread, ref bestPip, bestHit.point, bestHit.normal, targetTip, padR, 4.0f, 12);

            // Bắn lại tia vuông góc ngay dưới đầu ngón đã đặt để bám đúng bề mặt cong/côn thực tế
            Vector3 placedTip = fingerTips[i].position;
            if (TryPerpSurfaceHit(placedTip, mcpPos, toolCenter, toolAxis, maxExtent, out RaycastHit surfaceHit))
            {
                bestHit = surfaceHit;
                targetTip = bestHit.point + bestHit.normal * (padR + 0.0005f);
                RefineFingerDescent(i, ref bestMcp, ref bestSpread, ref bestPip, bestHit.point, bestHit.normal, targetTip, padR, 2.4f, 8);
            }

            float finalDip = Mathf.Clamp(bestPip * 0.65f, HandPoseTable.FingerDipMinFlex, HandPoseTable.FingerDipMaxFlex);
            ApplyFingerAngles(i, bestMcp, bestSpread, bestPip, finalDip);

            Vector3 finalTip = fingerTips[i].position;
            if (Vector3.Dot(finalTip - bestHit.point, bestHit.normal) < padR - 0.0022f)
                LastPenetrationCount++;

            float errMm = Mathf.Abs(Vector3.Distance(finalTip, bestHit.point) - padR) * 1000f;
            if (errMm > LastMaxSurfaceErrorMm && errMm < 25f)
                LastMaxSurfaceErrorMm = errMm;
        }

        bool TryPerpSurfaceHit(Vector3 primaryPt, Vector3 fallbackPt, Vector3 toolCenter, Vector3 toolAxis, float maxExtent, out RaycastHit hit)
        {
            float proj = Vector3.Dot(primaryPt - toolCenter, toolAxis);
            Vector3 axisPt = toolCenter + toolAxis * Mathf.Clamp(proj, -maxExtent * 0.85f, maxExtent * 0.85f);
            Vector3 radial = Vector3.ProjectOnPlane(primaryPt - axisPt, toolAxis);
            if (radial.sqrMagnitude < 1e-6f) radial = Vector3.ProjectOnPlane(fallbackPt - axisPt, toolAxis);
            if (radial.sqrMagnitude < 1e-6f) radial = palmRoot.up;
            radial.Normalize();

            float castDist = maxExtent + 0.16f;
            if (RaycastTarget(axisPt + radial * castDist, -radial, castDist * 1.15f, out hit))
                return true;

            Vector3 altRadial = Vector3.ProjectOnPlane(fallbackPt - axisPt, toolAxis);
            if (altRadial.sqrMagnitude >= 1e-6f)
            {
                altRadial.Normalize();
                return RaycastTarget(axisPt + altRadial * castDist, -altRadial, castDist * 1.15f, out hit);
            }
            return false;
        }

        float FingerCost(int i, float mcp, float spreadSigned, float pip, Vector3 hitPt, Vector3 hitNrm, Vector3 targetTip, float padR)
        {
            float dip = Mathf.Clamp(pip * 0.65f, HandPoseTable.FingerDipMinFlex, HandPoseTable.FingerDipMaxFlex);
            ApplyFingerAngles(i, mcp, spreadSigned, pip, dip);
            Vector3 tip = fingerTips[i].position;
            float signedDist = Vector3.Dot(tip - hitPt, hitNrm);
            float penPenalty = signedDist < padR ? (padR - signedDist) * 12f : 0f;
            return Vector3.Distance(tip, targetTip) + penPenalty;
        }

        void RefineFingerDescent(int i, ref float mcp, ref float spreadSigned, ref float pip,
            Vector3 hitPt, Vector3 hitNrm, Vector3 targetTip, float padR, float startStep, int passes)
        {
            float bestCost = FingerCost(i, mcp, spreadSigned, pip, hitPt, hitNrm, targetTip, padR);
            float step = startStep;
            for (int iter = 0; iter < passes; iter++)
            {
                for (int d = -1; d <= 1; d += 2)
                {
                    float cand = Mathf.Clamp(mcp + d * step, HandPoseTable.FingerMcpMinFlex, HandPoseTable.FingerMcpMaxFlex);
                    float c = FingerCost(i, cand, spreadSigned, pip, hitPt, hitNrm, targetTip, padR);
                    if (c < bestCost) { bestCost = c; mcp = cand; }
                }
                for (int d = -1; d <= 1; d += 2)
                {
                    float cand = Mathf.Clamp(pip + d * step * 1.25f, HandPoseTable.FingerPipMinFlex, HandPoseTable.FingerPipMaxFlex);
                    float c = FingerCost(i, mcp, spreadSigned, cand, hitPt, hitNrm, targetTip, padR);
                    if (c < bestCost) { bestCost = c; pip = cand; }
                }
                for (int d = -1; d <= 1; d += 2)
                {
                    float cand = Mathf.Clamp(spreadSigned + d * step * 0.7f, HandPoseTable.FingerMcpMinSpread, HandPoseTable.FingerMcpMaxSpread);
                    float c = FingerCost(i, mcp, cand, pip, hitPt, hitNrm, targetTip, padR);
                    if (c < bestCost) { bestCost = c; spreadSigned = cand; }
                }
                step *= 0.72f;
            }
            FingerCost(i, mcp, spreadSigned, pip, hitPt, hitNrm, targetTip, padR);
        }

        void SolveThumbContact(ThumbPose t, float sign, Vector3 toolCenter, Vector3 toolAxis, float maxExtent)
        {
            float thumbPadR = ThumbW * 0.39f;
            Vector3 curTip = thumbTip.position;
            if (!TryPerpSurfaceHit(curTip, thumbCmc.position, toolCenter, toolAxis, maxExtent, out RaycastHit bestHit))
                return;

            Vector3 targetTip = bestHit.point + bestHit.normal * (thumbPadR + 0.0005f);
            ThumbPose cur = HandPoseTable.ClampThumb(t);
            RefineThumbDescent(ref cur, sign, bestHit.point, bestHit.normal, targetTip, thumbPadR, 5.5f, 14);

            Vector3 placedTip = thumbTip.position;
            if (TryPerpSurfaceHit(placedTip, thumbCmc.position, toolCenter, toolAxis, maxExtent, out RaycastHit surfaceHit))
            {
                bestHit = surfaceHit;
                targetTip = bestHit.point + bestHit.normal * (thumbPadR + 0.0005f);
                RefineThumbDescent(ref cur, sign, bestHit.point, bestHit.normal, targetTip, thumbPadR, 2.5f, 8);
            }

            ApplyThumbAngles(cur, sign);
            Vector3 finalTip = thumbTip.position;
            if (Vector3.Dot(finalTip - bestHit.point, bestHit.normal) < thumbPadR - 0.0022f)
                LastPenetrationCount++;

            float errMm = Mathf.Abs(Vector3.Distance(finalTip, bestHit.point) - thumbPadR) * 1000f;
            if (errMm > LastMaxSurfaceErrorMm && errMm < 25f)
                LastMaxSurfaceErrorMm = errMm;
        }

        float ThumbCost(ThumbPose cur, float sign, Vector3 hitPt, Vector3 hitNrm, Vector3 targetTip, float thumbPadR)
        {
            ApplyThumbAngles(cur, sign);
            Vector3 placed = thumbTip.position;
            float signedDist = Vector3.Dot(placed - hitPt, hitNrm);
            float penPenalty = signedDist < thumbPadR ? (thumbPadR - signedDist) * 12f : 0f;
            return Vector3.Distance(placed, targetTip) + penPenalty;
        }

        void RefineThumbDescent(ref ThumbPose cur, float sign, Vector3 hitPt, Vector3 hitNrm, Vector3 targetTip, float thumbPadR, float startStep, int passes)
        {
            float bestCost = ThumbCost(cur, sign, hitPt, hitNrm, targetTip, thumbPadR);
            float step = startStep;
            for (int iter = 0; iter < passes; iter++)
            {
                for (int d = -1; d <= 1; d += 2)
                {
                    ThumbPose cand = cur;
                    cand.cmcFlex = Mathf.Clamp(cur.cmcFlex + d * step, HandPoseTable.ThumbCmcMinFlex, HandPoseTable.ThumbCmcMaxFlex);
                    float c = ThumbCost(cand, sign, hitPt, hitNrm, targetTip, thumbPadR);
                    if (c < bestCost) { bestCost = c; cur = cand; }
                }
                for (int d = -1; d <= 1; d += 2)
                {
                    ThumbPose cand = cur;
                    cand.cmcOppose = Mathf.Clamp(cur.cmcOppose + d * step, HandPoseTable.ThumbCmcMinOppose, HandPoseTable.ThumbCmcMaxOppose);
                    float c = ThumbCost(cand, sign, hitPt, hitNrm, targetTip, thumbPadR);
                    if (c < bestCost) { bestCost = c; cur = cand; }
                }
                for (int d = -1; d <= 1; d += 2)
                {
                    ThumbPose cand = cur;
                    cand.mcpFlex = Mathf.Clamp(cur.mcpFlex + d * step, HandPoseTable.ThumbMcpMinFlex, HandPoseTable.ThumbMcpMaxFlex);
                    cand.ipFlex = Mathf.Clamp(cand.mcpFlex * 1.05f, HandPoseTable.ThumbIpMinFlex, HandPoseTable.ThumbIpMaxFlex);
                    float c = ThumbCost(cand, sign, hitPt, hitNrm, targetTip, thumbPadR);
                    if (c < bestCost) { bestCost = c; cur = cand; }
                }
                step *= 0.74f;
            }
            ThumbCost(cur, sign, hitPt, hitNrm, targetTip, thumbPadR);
        }

        bool RaycastTarget(Vector3 origin, Vector3 dir, float dist, out RaycastHit best)
        {
            best = default;
            int n = Physics.RaycastNonAlloc(origin, dir, rayBuf, dist, ~0, QueryTriggerInteraction.Ignore);
            float minD = float.MaxValue;
            bool found = false;
            for (int k = 0; k < n; k++)
            {
                var h = rayBuf[k];
                if (h.collider == null || !IsTargetCollider(h.collider)) continue;
                if (h.distance < minD)
                {
                    minD = h.distance;
                    best = h;
                    found = true;
                }
            }
            return found;
        }

        bool IsTargetCollider(Collider col)
        {
            for (int i = 0; i < cachedContactCols.Length; i++)
                if (cachedContactCols[i] == col) return true;
            return false;
        }

        /// <summary>Ánh xạ 21 điểm MediaPipe (camera space, mét) vào khung xương bàn tay giải phẫu.</summary>
        public void ApplyMediaPipeLandmarks(Vector3[] lmCameraSpace, Quaternion cameraRot)
        {
            EnsureBuilt();
            if (lmCameraSpace == null || lmCameraSpace.Length < 21) return;
            drivingByLandmarks = true;

            float sign = side == 0 ? 1f : -1f;

            // Dựng khung tọa độ lòng bàn tay từ 3 điểm 0 (cổ tay), 5 (MCP trỏ), 17 (MCP út)
            Vector3 w0 = cameraRot * lmCameraSpace[0];
            Vector3 w5 = cameraRot * lmCameraSpace[5];
            Vector3 w9 = cameraRot * lmCameraSpace[9];
            Vector3 w17 = cameraRot * lmCameraSpace[17];

            Vector3 fwd = (w9 - w0);
            if (fwd.sqrMagnitude < 1e-6f) fwd = cameraRot * Vector3.forward;
            fwd.Normalize();

            Vector3 across = (w17 - w5) * sign;
            Vector3 up = Vector3.Cross(fwd, across);
            if (up.sqrMagnitude < 1e-6f) up = cameraRot * Vector3.up;
            up.Normalize();

            transform.position = w0 + fwd * 0.042f;
            transform.rotation = Quaternion.LookRotation(fwd, up);
            palmRoot.localEulerAngles = Vector3.zero;

            // Ánh xạ góc gập của 4 ngón tay từ góc giữa các đốt liền kề trong không gian
            int[] baseIdx = { 5, 9, 13, 17 };
            for (int i = 0; i < 4; i++)
            {
                int b = baseIdx[i];
                Vector3 vMeta = (lmCameraSpace[b] - lmCameraSpace[0]).normalized;
                Vector3 vProx = (lmCameraSpace[b + 1] - lmCameraSpace[b]).normalized;
                Vector3 vMid = (lmCameraSpace[b + 2] - lmCameraSpace[b + 1]).normalized;
                Vector3 vDist = (lmCameraSpace[b + 3] - lmCameraSpace[b + 2]).normalized;

                float mcpFlex = Vector3.Angle(vMeta, vProx);
                float pipFlex = Vector3.Angle(vProx, vMid);
                float dipFlex = Vector3.Angle(vMid, vDist);
                float spread = (i - 1.5f) * 3.5f;

                ApplyFingerAngles(i, mcpFlex, spread * sign, pipFlex, dipFlex);
            }

            // Ngón cái: điểm 1 -> 2 -> 3 -> 4
            Vector3 t01 = (lmCameraSpace[1] - lmCameraSpace[0]).normalized;
            Vector3 t12 = (lmCameraSpace[2] - lmCameraSpace[1]).normalized;
            Vector3 t23 = (lmCameraSpace[3] - lmCameraSpace[2]).normalized;
            Vector3 t34 = (lmCameraSpace[4] - lmCameraSpace[3]).normalized;

            float thumbOppose = Mathf.Clamp(Vector3.Angle(t01, lmCameraSpace[5] - lmCameraSpace[0]) * 0.85f, 18f, 65f);
            float thumbCmcFlex = Vector3.Angle(t01, t12);
            float thumbMcpFlex = Vector3.Angle(t12, t23);
            float thumbIpFlex = Vector3.Angle(t23, t34);

            ApplyThumbAngles(new ThumbPose(thumbCmcFlex, thumbOppose, 42f, thumbMcpFlex, thumbIpFlex), sign);
        }

        public bool GetFingerJoints(int fingerIndex, out Transform mcp, out Transform pip, out Transform dip, out Transform tip)
        {
            EnsureBuilt();
            if (fingerIndex < 0 || fingerIndex >= 4)
            {
                mcp = pip = dip = tip = null;
                return false;
            }
            mcp = proximal[fingerIndex];
            pip = middle[fingerIndex];
            dip = distal[fingerIndex];
            tip = fingerTips[fingerIndex];
            return true;
        }

        public void GetThumbJoints(out Transform cmc, out Transform mcp, out Transform ip, out Transform tip)
        {
            EnsureBuilt();
            cmc = thumbCmc;
            mcp = thumbMcp;
            ip = thumbIp;
            tip = thumbTip;
        }

        public static (float prox, float mid, float dist) FingerBoneLengths(int i)
        {
            int idx = Mathf.Clamp(i, 0, 3);
            return (ProxLen[idx], MidLen[idx], DistLen[idx]);
        }

        public int FingerNailsCount => nailRenderers.Length;

        void RegisterSurface(Transform t)
        {
            if (t == null || surfaceCount >= handSurfaceRenderers.Length) return;
            var r = t.GetComponent<Renderer>();
            if (r != null) handSurfaceRenderers[surfaceCount++] = r;
        }

        void RegisterCuff(Transform t)
        {
            if (t == null || cuffCount >= cuffRenderers.Length) return;
            var r = t.GetComponent<Renderer>();
            if (r != null) cuffRenderers[cuffCount++] = r;
        }

        void RegisterSleeve(Transform t, bool isTrim = false)
        {
            if (t == null || sleeveCount >= sleeveRenderers.Length) return;
            var r = t.GetComponent<Renderer>();
            if (r != null) sleeveRenderers[sleeveCount++] = r;
        }

        static Transform Joint(Transform parent, string name, Vector3 pos, Vector3 euler)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localEulerAngles = euler;
            return t;
        }

        static Transform Part(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Vector3 euler, Material m)
        {
            var g = GameObject.CreatePrimitive(type);
            g.name = name;
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localEulerAngles = euler;
            g.transform.localScale = scale;
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g.transform;
        }
    }
}
