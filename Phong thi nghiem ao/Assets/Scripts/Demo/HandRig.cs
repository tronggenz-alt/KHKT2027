using System;
using System.Collections.Generic;
using Khkt.Avatar;
using Khkt.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Khkt.Demo
{
    /// <summary>
    /// Bàn tay người (bản 20/11/2026): một lưới da liền khối có skinning dựng bằng mã (TayNguoiLuoi) gắn vào
    /// khung xương giải phẫu 17 xương: gan tay, 4 ngón × 3 đốt (MCP – PIP – DIP), ngón cái 3 khớp (CMC 3 bậc tự do
    /// gập/dạng–khép/xoay sấp, MCP, IP) và xương cổ tay ảo nằm trong ống tay áo (ArmRig xoay theo cẳng tay).
    /// Gò cơ (mô cái, mô út, đệm gan tay), gồ khớp, nếp gấp, 5 móng tay bám mặt mu đốt xa, cổ găng nitrile.
    /// Shader Khkt/TayNguoi: da (tán xạ dưới da, lỗ chân lông, nếp khớp) hoặc găng nitrile (công tắc WearGloves).
    /// Tư thế chuyển mượt trong 0,15 s cố định (theo thời gian, không theo số khung), các ngón lệch pha nhẹ,
    /// IK bề mặt chống xuyên dụng cụ (≤ 3 mm) và ánh xạ đủ 21 điểm MediaPipe (nghịch đảo đúng khung xương).
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

        public Color gloveColor = new Color(0.20f, 0.46f, 0.86f);
        public Color skinColor = new Color(0.86f, 0.66f, 0.55f);
        public Color sleeveColor = new Color(0.95f, 0.96f, 0.97f);
        public Color cuffTrimColor = new Color(0.75f, 0.86f, 0.96f);

        [Range(0, 1)] public float curl;
        [Tooltip("0 = tay phải, 1 = tay trái")]
        public int side = 0;
        [Tooltip("Tự hiện cổ tay áo blouse ngắn (tắt khi ArmRig vẽ cả ống tay áo)")]
        public bool ownSleeve = true;

        /// <summary>Điểm nắm: dụng cụ đang cầm treo ở đây.</summary>
        public Transform Grip { get; private set; }
        /// <summary>Miệng cổ tay áo blouse: ArmRig đặt mép ống tay áo đúng ở đây (không hở cổ tay).</summary>
        public Transform WristAttach { get; private set; }
        /// <summary>Xương cổ tay ảo (trong ống tay áo): ArmRig xoay theo hướng cẳng tay để da cổ tay uốn mượt.</summary>
        public Transform ForearmBone { get; private set; }
        public SkinnedMeshRenderer SkinRenderer { get; private set; }

        public HandPoseId CurrentPose { get; private set; } = HandPoseId.Xoe;
        public HandPoseId? PoseOverride { get; set; }
        public Transform TargetContactObject { get; set; }
        public bool SkipIdleUpdate { get; set; }
        /// <summary>ArmRig tự gọi Tick() trong LateUpdate đúng thứ tự (sau khi đặt vị trí tay, trước khi đặt cánh tay).</summary>
        public bool DrivenByArm { get; set; }
        public float LastMaxSurfaceErrorMm { get; private set; }
        public int LastPenetrationCount { get; private set; }
        /// <summary>Thời gian dựng lưới (ms) lần gần nhất và số đỉnh.</summary>
        public float LastMeshBuildMs { get; private set; }
        public int MeshVertexCount { get; private set; }
        public int MeshLod { get; private set; } = -1;
        /// <summary>Lưới lấy từ bộ nhớ đệm trên đĩa (không phải dựng lại).</summary>
        public bool MeshFromDisk { get; private set; }

        // Tỉ lệ giải phẫu đốt ngón tay (m): Trỏ, Giữa, Nhẫn, Út — đốt gần > giữa > xa
        static readonly float[] ProxLen = { 0.040f, 0.045f, 0.042f, 0.033f };
        static readonly float[] MidLen = { 0.024f, 0.028f, 0.026f, 0.019f };
        static readonly float[] DistLen = { 0.019f, 0.020f, 0.019f, 0.017f };
        static readonly float[] FingerW = { 0.0170f, 0.0176f, 0.0166f, 0.0144f };

        static readonly float[] McpX = { -0.0285f, -0.0092f, 0.0100f, 0.0280f };
        static readonly float[] McpZ = { 0.047f, 0.051f, 0.048f, 0.040f };
        static readonly float[] McpArchY = { 0.0005f, 0.0030f, 0.0016f, -0.0022f };

        const float ThumbMetaLen = 0.038f, ThumbProxLen = 0.029f, ThumbDistLen = 0.023f;
        const float ThumbW = 0.0190f;
        static readonly Vector3 ThumbCmcPos = new Vector3(-0.0255f, -0.0060f, -0.029f);
        static readonly Vector3 WristCenter = new Vector3(0f, 0f, -0.042f);
        // Tư thế gốc của lưới (ngón duỗi hơi xòe, ngón cái dạng nhẹ)
        static readonly float[] BindSpread = { -7f, -2f, 3f, 9f };
        static readonly ThumbPose BindThumb = new ThumbPose(16f, -14f, 26f, 0f, 0f);

        Transform palmRoot;
        readonly Transform[] proximal = new Transform[4];
        readonly Transform[] middle = new Transform[4];
        readonly Transform[] distal = new Transform[4];
        readonly Transform[] fingerTips = new Transform[4];
        readonly Renderer[] nailRenderers = new Renderer[5];
        readonly MeshFilter[] nailFilters = new MeshFilter[5];
        Transform thumbCmc, thumbMcp, thumbIp, thumbTip;
        SkinnedMeshRenderer gloveCuffRenderer;
        MeshRenderer sleeveCuffRenderer;
        MeshFilter sleeveCuffFilter;
        readonly Transform[] bones = new Transform[TayNguoiLuoi.HandBoneCount];
        readonly float[] padRadius = { 0.0035f, 0.0035f, 0.0035f, 0.0030f, 0.0040f };
        bool renderEnabled = true;

        Material skinMat, gloveMat, gloveCuffMat, nailMat, gloveNailMat, sleeveMat;

        HandPoseId blendTargetPose = HandPoseId.Xoe;
        HandPoseData blendFromData, activePoseData;
        float blendElapsed = HandPoseTable.TransitionDurationS;
        float blendDuration = HandPoseTable.TransitionDurationS;
        /// <summary>Thời lượng của lần chuyển tư thế đang chạy (s).</summary>
        public float CurrentTransitionS => blendDuration;
        float shownCurl = -1f;
        float idleTime;
        bool built;
        bool drivingByLandmarks;

        Transform cachedContactTarget;
        Collider[] cachedContactCols = Array.Empty<Collider>();
        bool[] cachedPrevEnabled = Array.Empty<bool>();
        readonly RaycastHit[] rayBuf = new RaycastHit[16];
        float thumbPronateMemory = 26f;

        // Động lực học thứ cấp: cổ tay trễ theo quán tính, ngón lắc theo (lò xo tới hạn giải đúng → độc lập FPS)
        Vector3 lastPos;
        bool lastPosValid;
        Vector3 lagEuler, lagEulerVel;
        float contactFade = 1f, contactFadeVel;
        Vector3 lagApplied;
        float fingerLagApplied;
        /// <summary>Độ trễ cổ tay hiện tại (độ, x gập, y lệch, z xoay) do quán tính.</summary>
        public Vector3 WristLagDeg => lagApplied;

        void Awake()
        {
            if (!ActiveRigs.Contains(this)) ActiveRigs.Add(this);
            EnsureBuilt();
        }

        void OnEnable() => HardwareProfile.Changed += OnHardwareChanged;
        void OnDisable() => HardwareProfile.Changed -= OnHardwareChanged;
        void OnDestroy() => ActiveRigs.Remove(this);

        void OnHardwareChanged()
        {
            if (!built) return;
            int lod = LodFromProfile();
            if (lod != MeshLod) ApplyMeshLod(lod);
        }

        static int LodFromProfile() => HardwareProfile.Muc switch
        {
            MucDoHoa.SieuNet => 0,
            MucDoHoa.Cao => 1,
            MucDoHoa.Vua => 2,
            _ => 3
        };

        public void EnsureBuilt()
        {
            if (built) return;
            built = true;

            skinMat = TayNguoiLuoi.NewMaterial(0, skinColor);
            gloveMat = TayNguoiLuoi.NewMaterial(1, gloveColor);
            gloveCuffMat = TayNguoiLuoi.NewMaterial(1, gloveColor * 0.93f);
            gloveCuffMat.SetFloat("_Inflate", 0f);
            nailMat = TayNguoiLuoi.NewMaterial(0, new Color(0.93f, 0.76f, 0.72f));
            nailMat.SetFloat("_Smoothness", 0.78f);
            nailMat.SetFloat("_SpecStrength", 0.55f);
            nailMat.SetFloat("_SssStrength", 0.25f);
            nailMat.SetFloat("_CreaseStrength", 0f);
            nailMat.SetFloat("_DetailStrength", 0.15f);
            gloveNailMat = TayNguoiLuoi.NewMaterial(1, gloveColor);
            gloveNailMat.SetFloat("_Inflate", 0.00045f);
            sleeveMat = TayNguoiLuoi.NewMaterial(2, sleeveColor);

            float sign = side == 0 ? 1f : -1f;

            palmRoot = new GameObject("Gốc bàn tay").transform;
            palmRoot.SetParent(transform, false);

            for (int i = 0; i < 4; i++)
            {
                var jMcp = Joint(palmRoot, $"Ngón {i + 1} MCP", new Vector3(McpX[i] * sign, McpArchY[i], McpZ[i]));
                var jPip = Joint(jMcp, "PIP", new Vector3(0f, 0f, ProxLen[i]));
                var jDip = Joint(jPip, "DIP", new Vector3(0f, 0f, MidLen[i]));
                var tip = Joint(jDip, $"Đầu ngón {i + 1}", new Vector3(0f, -FingerW[i] * 0.74f * 0.20f, DistLen[i]));
                proximal[i] = jMcp; middle[i] = jPip; distal[i] = jDip; fingerTips[i] = tip;
            }

            thumbCmc = Joint(palmRoot, "Ngón cái CMC", new Vector3(ThumbCmcPos.x * sign, ThumbCmcPos.y, ThumbCmcPos.z));
            thumbMcp = Joint(thumbCmc, "Ngón cái MCP", new Vector3(0f, 0f, ThumbMetaLen));
            thumbIp = Joint(thumbMcp, "Ngón cái IP", new Vector3(0f, 0f, ThumbProxLen));
            thumbTip = Joint(thumbIp, "Đầu ngón cái", new Vector3(0f, -ThumbW * 0.16f, ThumbDistLen));

            ForearmBone = Joint(palmRoot, "Cổ tay (theo cẳng tay)", WristCenter);

            Grip = new GameObject("Điểm nắm").transform;
            Grip.SetParent(transform, false);
            Grip.localPosition = new Vector3(0f, -0.032f, 0.044f);

            WristAttach = new GameObject("Điểm nối cổ tay").transform;
            WristAttach.SetParent(palmRoot, false);
            WristAttach.localPosition = new Vector3(0f, 0.0005f, -0.056f);

            bones[TayNguoiLuoi.BonePalm] = palmRoot;
            for (int f = 0; f < 4; f++)
            {
                bones[TayNguoiLuoi.BoneFinger(f, 0)] = proximal[f];
                bones[TayNguoiLuoi.BoneFinger(f, 1)] = middle[f];
                bones[TayNguoiLuoi.BoneFinger(f, 2)] = distal[f];
            }
            bones[TayNguoiLuoi.BoneThumbCmc] = thumbCmc;
            bones[TayNguoiLuoi.BoneThumbMcp] = thumbMcp;
            bones[TayNguoiLuoi.BoneThumbIp] = thumbIp;
            bones[TayNguoiLuoi.BoneWrist] = ForearmBone;

            // Bộ phận hiển thị
            var skinGo = new GameObject("Lưới bàn tay");
            skinGo.transform.SetParent(transform, false);
            SkinRenderer = skinGo.AddComponent<SkinnedMeshRenderer>();
            SkinRenderer.updateWhenOffscreen = false;

            var cuffGo = new GameObject("Cổ găng nitrile");
            cuffGo.transform.SetParent(transform, false);
            gloveCuffRenderer = cuffGo.AddComponent<SkinnedMeshRenderer>();
            gloveCuffRenderer.sharedMaterial = gloveCuffMat;

            for (int i = 0; i < 5; i++)
            {
                var parent = i < 4 ? distal[i] : thumbIp;
                var ng = new GameObject(i < 4 ? "Móng tay" : "Móng ngón cái");
                ng.transform.SetParent(parent, false);
                nailFilters[i] = ng.AddComponent<MeshFilter>();
                var mr = ng.AddComponent<MeshRenderer>();
                mr.shadowCastingMode = ShadowCastingMode.Off;
                nailRenderers[i] = mr;
            }

            var sleeveGo = new GameObject("Cổ tay áo blouse");
            sleeveGo.transform.SetParent(ForearmBone, false);
            sleeveCuffFilter = sleeveGo.AddComponent<MeshFilter>();
            sleeveCuffRenderer = sleeveGo.AddComponent<MeshRenderer>();
            sleeveCuffRenderer.sharedMaterial = sleeveMat;

            ApplyMeshLod(LodFromProfile());

            activePoseData = HandPoseTable.Get(HandPoseId.Xoe);
            blendFromData = activePoseData;
            ApplyPoseData(activePoseData);
            RefreshAppearance();
        }

        int wantedLod = -1;
        bool meshPending;
        /// <summary>Lưới da đã được gán (lần dựng đầu chạy trên luồng nền).</summary>
        public bool MeshReady => MeshLod >= 0;
        public bool MeshPending => meshPending;

        void ApplyMeshLod(int lod)
        {
            wantedLod = lod;
            meshPending = true;
            PollMesh(force: true);
        }

        void PollMesh(bool force = false)
        {
            if (!meshPending) return;
            if (!force && !TayNguoiLuoi.IsReady(side, wantedLod, 1)) return;
            AssignMesh(wantedLod);
        }

        /// <summary>Dựng / đổi lưới theo mức chi tiết (bộ nhớ đệm dùng chung giữa các bàn tay cùng bên).</summary>
        public void WaitMesh()
        {
            if (!meshPending) return;
            AssignMesh(wantedLod, wait: true);
        }

        void AssignMesh(int lod, bool wait = false)
        {
            float sign = side == 0 ? 1f : -1f;
            // Đưa khung xương về tư thế gốc
            Vector3 palmPos = palmRoot.localPosition;
            Quaternion palmRot = palmRoot.localRotation;
            Quaternion foreRot = ForearmBone.localRotation;
            palmRoot.localPosition = Vector3.zero;
            palmRoot.localRotation = Quaternion.identity;
            ForearmBone.localRotation = Quaternion.identity;
            for (int i = 0; i < 4; i++)
            {
                proximal[i].localRotation = Quaternion.Euler(0f, BindSpread[i] * sign, 0f);
                middle[i].localRotation = Quaternion.identity;
                distal[i].localRotation = Quaternion.identity;
            }
            thumbCmc.localRotation = Quaternion.Euler(BindThumb.cmcFlex, BindThumb.cmcOppose * sign, BindThumb.cmcPronate * sign);
            thumbMcp.localRotation = Quaternion.identity;
            thumbIp.localRotation = Quaternion.identity;

            Transform root = SkinRenderer.transform;
            var bind = new TayNguoiLuoi.HandBind { side = side };
            for (int f = 0; f < 4; f++)
            {
                bind.mcp[f] = root.InverseTransformPoint(proximal[f].position);
                bind.dir[f] = root.InverseTransformDirection(proximal[f].forward).normalized;
                bind.up[f] = root.InverseTransformDirection(proximal[f].up).normalized;
                bind.w[f] = FingerW[f];
                bind.l1[f] = ProxLen[f]; bind.l2[f] = MidLen[f]; bind.l3[f] = DistLen[f];
            }
            bind.thumbC = root.InverseTransformPoint(thumbCmc.position);
            bind.thumbDir = root.InverseTransformDirection(thumbCmc.forward).normalized;
            bind.thumbUp = root.InverseTransformDirection(thumbCmc.up).normalized;
            bind.thumbW = ThumbW;
            bind.tl1 = ThumbMetaLen; bind.tl2 = ThumbProxLen; bind.tl3 = ThumbDistLen;
            bind.wristCenter = WristCenter;

            var set = TayNguoiLuoi.TryGetHand(bind, lod, 1, wait);
            if (set == null)
            {
                palmRoot.localPosition = palmPos;
                palmRoot.localRotation = palmRot;
                ForearmBone.localRotation = foreRot;
                ApplyPoseData(activePoseData);
                return;
            }
            meshPending = false;
            if (set.hand.bindposes == null || set.hand.bindposes.Length != bones.Length)
            {
                var bp = new Matrix4x4[bones.Length];
                for (int i = 0; i < bones.Length; i++) bp[i] = bones[i].worldToLocalMatrix * root.localToWorldMatrix;
                set.hand.bindposes = bp;
                set.gloveCuff.bindposes = bp;
            }

            SkinRenderer.sharedMesh = set.hand;
            SkinRenderer.bones = bones;
            SkinRenderer.rootBone = palmRoot;
            SkinRenderer.localBounds = new Bounds(new Vector3(0f, 0f, 0.02f), new Vector3(0.30f, 0.30f, 0.36f));
            SkinRenderer.quality = lod >= 3 ? SkinQuality.Bone2 : SkinQuality.Bone4;
            gloveCuffRenderer.sharedMesh = set.gloveCuff;
            gloveCuffRenderer.bones = bones;
            gloveCuffRenderer.rootBone = palmRoot;
            gloveCuffRenderer.localBounds = new Bounds(new Vector3(0f, 0f, -0.06f), new Vector3(0.12f, 0.12f, 0.12f));
            for (int i = 0; i < 5; i++) nailFilters[i].sharedMesh = set.nails[i];

            // Bán kính đệm đầu ngón đo trên lưới thật (từ điểm đầu ngón xuống mặt lòng)
            for (int f = 0; f < 4; f++) padRadius[f] = Mathf.Clamp(MeasurePad(set, fingerTips[f], distal[f]), 0.0015f, 0.008f);
            padRadius[4] = Mathf.Clamp(MeasurePad(set, thumbTip, thumbIp), 0.0015f, 0.009f);

            sleeveCuffFilter.sharedMesh = TayNguoiLuoi.GetSleeve(0f, 0.13f, side, lod);
            // ống cổ tay áo dựng dọc +z, miệng ở z = 0,13 → đặt miệng trùng WristAttach
            sleeveCuffFilter.transform.localPosition = (WristAttach.localPosition - WristCenter) - new Vector3(0f, 0f, 0.13f);
            sleeveCuffFilter.transform.localRotation = Quaternion.identity;

            var shadows = lod <= 1 ? ShadowCastingMode.On : ShadowCastingMode.Off;
            SkinRenderer.shadowCastingMode = shadows;
            gloveCuffRenderer.shadowCastingMode = ShadowCastingMode.Off;
            sleeveCuffRenderer.shadowCastingMode = shadows;

            LastMeshBuildMs = set.buildMs;
            MeshFromDisk = set.fromDisk;
            MeshVertexCount = set.vertexCount;
            MeshLod = lod;

            palmRoot.localPosition = palmPos;
            palmRoot.localRotation = palmRot;
            ForearmBone.localRotation = foreRot;
            ApplyPoseData(activePoseData);
        }

        float MeasurePad(TayNguoiLuoi.HandMeshSet set, Transform tip, Transform dist)
        {
            // Khoảng cách từ điểm đầu ngón tới mặt da phía lòng (theo -up của đốt xa), tính trên lưới tư thế gốc
            var mesh = set.hand;
            Vector3 p = SkinRenderer.transform.InverseTransformPoint(tip.position);
            Vector3 down = -SkinRenderer.transform.InverseTransformDirection(dist.up);
            var verts = mesh.vertices;
            float best = float.MaxValue;
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 d = verts[i] - p;
                float along = Vector3.Dot(d, down);
                if (along <= 0f) continue;
                float lat = (d - down * along).sqrMagnitude;
                if (lat > 0.0016f * 0.0016f) continue;
                if (along < best) best = along;
            }
            return best == float.MaxValue ? 0.0035f : best;
        }

        /// <summary>Đổi màu áo theo vai trò trong phòng học chung (Giáo viên vs Học sinh).</summary>
        public void SetSleeveColors(Color coat, Color trim)
        {
            EnsureBuilt();
            sleeveColor = coat;
            cuffTrimColor = trim;
            if (sleeveMat != null) sleeveMat.SetColor("_BaseColor", coat);
        }

        public void SetSleeveVisible(bool visible)
        {
            EnsureBuilt();
            ownSleeve = visible;
            sleeveCuffRenderer.enabled = visible && renderEnabled;
        }

        public void SetRenderEnabled(bool enabled)
        {
            EnsureBuilt();
            renderEnabled = enabled;
            SkinRenderer.enabled = enabled;
            for (int i = 0; i < nailRenderers.Length; i++)
                if (nailRenderers[i] != null) nailRenderers[i].enabled = enabled;
            gloveCuffRenderer.enabled = enabled && WearGloves;
            sleeveCuffRenderer.enabled = enabled && ownSleeve;
            if (enabled) RefreshAppearance();
        }

        public void RefreshAppearance()
        {
            if (!built) return;
            bool gloved = WearGloves;
            SkinRenderer.sharedMaterial = gloved ? gloveMat : skinMat;
            for (int i = 0; i < nailRenderers.Length; i++)
                if (nailRenderers[i] != null) nailRenderers[i].sharedMaterial = gloved ? gloveNailMat : nailMat;
            gloveCuffRenderer.enabled = gloved && renderEnabled;
            sleeveCuffRenderer.enabled = ownSleeve && renderEnabled;
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
                blendElapsed = blendDuration = HandPoseTable.TransitionDurationS;
                ApplyPoseData(activePoseData);
                return;
            }
            if (wanted == blendTargetPose) return;
            blendDuration = TransitionFor(activePoseData, HandPoseTable.Get(wanted));
            blendFromData = activePoseData;
            blendTargetPose = wanted;
            CurrentPose = wanted;
            blendElapsed = 0f;
        }

        void Update()
        {
            if (meshPending) PollMesh();
            if (!built || SkipIdleUpdate || DrivenByArm || drivingByLandmarks) return;
            Tick(Time.unscaledDeltaTime);
        }

        /// <summary>Một bước cập nhật: chọn tư thế theo curl/PoseOverride rồi chuyển tư thế + IK bề mặt.</summary>
        public void Tick(float dt)
        {
            if (!built || drivingByLandmarks) return;
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
            StepPoseAndContact(dt);
        }

        public void ClearLandmarkControl() => drivingByLandmarks = false;

        // Lệch pha từng ngón (tỉ lệ thời lượng): ngón cái dẫn, rồi trỏ → út; tổng thời gian luôn đúng thời lượng chuyển
        static readonly float[] FingerDelay = { 0.000f, 0.067f, 0.133f, 0.200f };
        const float ThumbDelay = 0.0f;
        const float MaxDelay = 0.200f;

        /// <summary>
        /// Thời lượng chuyển tư thế cố định theo độ lớn thay đổi (giống tay người: chỉnh nhẹ nhanh, nắm/xòe trọn chậm hơn):
        /// 0,15 s (TransitionDurationS) cho thay đổi nhỏ → 0,32 s cho nắm trọn bàn tay. Không phụ thuộc FPS.
        /// </summary>
        public static float TransitionFor(HandPoseData from, HandPoseData to)
        {
            float d = Mathf.Abs(Curl(to) - Curl(from)) + Mathf.Abs(to.thumb.cmcOppose - from.thumb.cmcOppose);
            return Mathf.Lerp(HandPoseTable.TransitionDurationS, 0.32f, Mathf.Clamp01(d / 260f));
        }

        static float Smoother(float u) => u * u * u * (u * (u * 6f - 15f) + 10f);

        static float Phase(float elapsed, float delay, float T)
        {
            float dl = delay * T, md = MaxDelay * T;
            return Smoother(Mathf.Clamp01((elapsed - dl) / (T - md)));
        }

        /// <summary>Cập nhật chuyển tư thế (0,15 s, không phụ thuộc FPS) rồi chạy raycast + IK chống xuyên vật.</summary>
        public void StepPoseAndContact(float dt)
        {
            EnsureBuilt();
            drivingByLandmarks = false;
            dt = Mathf.Max(0f, dt);
            idleTime += dt;

            HandPoseData target = HandPoseTable.Get(blendTargetPose);
            if (blendElapsed < blendDuration)
            {
                blendElapsed = Mathf.Min(blendDuration, blendElapsed + dt);
                activePoseData = BlendStaggered(blendFromData, target, blendElapsed, blendDuration);
            }
            else activePoseData = target;

            UpdateSecondary(dt);
            if (TargetContactObject == null && !PoseOverride.HasValue)
                ApplyPoseData(WithIdle(activePoseData));
            else
                ApplyPoseData(activePoseData);

            if (TargetContactObject != null)
                SolveSurfaceContactIk();
            else
            {
                LastMaxSurfaceErrorMm = 0f;
                LastPenetrationCount = 0;
            }
        }

        static void Spring(ref Vector3 x, ref Vector3 v, Vector3 target, float omega, float dt)
        {
            float e = Mathf.Exp(-omega * dt);
            Vector3 d = x - target;
            Vector3 tmp = (v + omega * d) * dt;
            v = (v - omega * tmp) * e;
            x = target + (d + tmp) * e;
        }

        static void Spring(ref float x, ref float v, float target, float omega, float dt)
        {
            float e = Mathf.Exp(-omega * dt);
            float d = x - target;
            float tmp = (v + omega * d) * dt;
            v = (v - omega * tmp) * e;
            x = target + (d + tmp) * e;
        }

        /// <summary>
        /// Quán tính: tay đi lên → cổ tay gập xuống (ngón trễ lại), đi ngang → cổ tay lệch ngược chiều, rồi bật về.
        /// Tắt dần khi đang ôm dụng cụ (IK bề mặt cần gan tay đứng yên).
        /// </summary>
        void UpdateSecondary(float dt)
        {
            Vector3 pos = transform.position;
            Vector3 lagTarget = Vector3.zero;
            if (lastPosValid && dt > 1e-5f)
            {
                Vector3 vel = (pos - lastPos) / dt;
                if (vel.sqrMagnitude > 9f) vel = Vector3.zero;          // dịch chuyển tức thời (đổi chỗ đứng)
                Vector3 lv = transform.InverseTransformDirection(vel);
                lagTarget = new Vector3(Mathf.Clamp(lv.y * 22f + lv.z * 5f, -16f, 16f),
                                        Mathf.Clamp(-lv.x * 16f, -12f, 12f),
                                        Mathf.Clamp(lv.x * 9f, -8f, 8f));
            }
            lastPos = pos;
            lastPosValid = true;
            if (dt <= 0f) return;
            Spring(ref lagEuler, ref lagEulerVel, lagTarget, 13f, dt);
            Spring(ref contactFade, ref contactFadeVel, TargetContactObject == null && !drivingByLandmarks ? 1f : 0f, 10f, dt);
            float k = Mathf.Clamp01(contactFade);
            lagApplied = lagEuler * k;
            fingerLagApplied = lagEuler.x * 0.45f * k;
        }

        /// <summary>Đặt lại trạng thái quán tính (sau khi dịch chuyển tức thời).</summary>
        public void ResetSecondary() { lastPosValid = false; lagEuler = lagEulerVel = Vector3.zero; lagApplied = Vector3.zero; fingerLagApplied = 0f; }

        /// <summary>Nội suy tư thế theo thời gian đã trôi (dùng cho kiểm thử độc lập FPS).</summary>
        public static HandPoseData BlendStaggered(HandPoseData from, HandPoseData to, float elapsed, float duration = HandPoseTable.TransitionDurationS)
        {
            float T = duration;
            float uw = Phase(elapsed, 0f, T);
            // Chuẩn bị nắm (pre-shaping) như tay người: khi chuyển sang tư thế nắm chặt hơn, các ngón duỗi/xòe ra
            // một chút ở nửa đầu rồi mới khép vào; ngón cái dạng ra trước khi đối chiếu.
            float closing = Mathf.Clamp01((Curl(to) - Curl(from)) / 120f);
            return new HandPoseData
            {
                thumb = PreThumb(ThumbPose.Lerp(from.thumb, to.thumb, Phase(elapsed, ThumbDelay, T)), Bump(elapsed, ThumbDelay, T) * closing),
                index = PreFinger(FingerPose.Lerp(from.index, to.index, Phase(elapsed, FingerDelay[0], T)), Bump(elapsed, FingerDelay[0], T) * closing, -1f),
                middle = PreFinger(FingerPose.Lerp(from.middle, to.middle, Phase(elapsed, FingerDelay[1], T)), Bump(elapsed, FingerDelay[1], T) * closing, -0.3f),
                ring = PreFinger(FingerPose.Lerp(from.ring, to.ring, Phase(elapsed, FingerDelay[2], T)), Bump(elapsed, FingerDelay[2], T) * closing, 0.3f),
                pinky = PreFinger(FingerPose.Lerp(from.pinky, to.pinky, Phase(elapsed, FingerDelay[3], T)), Bump(elapsed, FingerDelay[3], T) * closing, 1f),
                wristEulerOffset = Vector3.Lerp(from.wristEulerOffset, to.wristEulerOffset, uw),
                palmLocalOffset = Vector3.Lerp(from.palmLocalOffset, to.palmLocalOffset, uw)
            };
        }

        static float Curl(HandPoseData d) =>
            d.index.mcpFlex + d.index.pipFlex + d.middle.mcpFlex + d.middle.pipFlex + d.ring.mcpFlex + d.ring.pipFlex + d.pinky.mcpFlex + d.pinky.pipFlex;

        /// <summary>Hình chuông 0→1→0 trong 65% đầu của pha chuyển (0 khi bắt đầu và khi kết thúc).</summary>
        static float Bump(float elapsed, float delay, float T)
        {
            float u = Mathf.Clamp01((elapsed - delay * T) / (T - MaxDelay * T));
            return u < 0.65f ? Mathf.Sin(u / 0.65f * Mathf.PI) : 0f;
        }

        static FingerPose PreFinger(FingerPose f, float b, float spreadDir)
        {
            f.mcpFlex -= 16f * b;
            f.pipFlex -= 12f * b;
            f.dipFlex -= 6f * b;
            f.mcpSpread += 5f * b * spreadDir;
            return f;
        }

        static ThumbPose PreThumb(ThumbPose t, float b)
        {
            t.cmcOppose -= 14f * b;
            t.mcpFlex -= 10f * b;
            t.ipFlex -= 10f * b;
            return t;
        }

        /// <summary>Chuyển động "sống" rất nhẹ khi tay rảnh: ngón co duỗi vài độ theo nhịp thở, lệch pha từng ngón.</summary>
        HandPoseData WithIdle(HandPoseData d)
        {
            float t = idleTime;
            float b = Mathf.Sin(t * 1.6f);
            d.index.mcpFlex += 1.2f * Mathf.Sin(t * 1.10f + 0.3f) + 0.6f * b;
            d.middle.mcpFlex += 1.4f * Mathf.Sin(t * 1.10f + 0.9f) + 0.6f * b;
            d.ring.mcpFlex += 1.6f * Mathf.Sin(t * 1.10f + 1.5f) + 0.6f * b;
            d.pinky.mcpFlex += 1.8f * Mathf.Sin(t * 1.10f + 2.1f) + 0.6f * b;
            d.index.pipFlex += 1.0f * Mathf.Sin(t * 0.9f + 0.2f);
            d.middle.pipFlex += 1.2f * Mathf.Sin(t * 0.9f + 0.8f);
            d.ring.pipFlex += 1.3f * Mathf.Sin(t * 0.9f + 1.4f);
            d.pinky.pipFlex += 1.5f * Mathf.Sin(t * 0.9f + 2.0f);
            d.thumb.mcpFlex += 1.0f * Mathf.Sin(t * 0.8f + 1.1f);
            d.thumb.cmcOppose += 1.2f * Mathf.Sin(t * 0.55f + 0.4f);
            // cổ tay đung đưa rất nhẹ (nhiễu chậm) cho tay "sống"
            d.wristEulerOffset += new Vector3((Mathf.PerlinNoise(t * 0.35f, 1.7f) - 0.5f) * 3.0f,
                                              (Mathf.PerlinNoise(t * 0.30f, 5.3f) - 0.5f) * 2.4f,
                                              (Mathf.PerlinNoise(t * 0.25f, 9.1f) - 0.5f) * 2.0f);
            return d;
        }

        void ApplyPoseData(HandPoseData d)
        {
            d = HandPoseTable.ClampAll(d);
            float sign = side == 0 ? 1f : -1f;
            palmRoot.localPosition = new Vector3(d.palmLocalOffset.x * sign, d.palmLocalOffset.y, d.palmLocalOffset.z);
            palmRoot.localRotation = Quaternion.Euler(d.wristEulerOffset.x, d.wristEulerOffset.y * sign, d.wristEulerOffset.z * sign)
                                     * Quaternion.Euler(lagApplied.x, lagApplied.y * sign, lagApplied.z * sign);

            for (int i = 0; i < 4; i++)
            {
                FingerPose f = d.GetFinger(i);
                // Ngón khép lại khi gập (hội tụ về phía xương thuyền như khi nắm tay) + ngón trễ theo quán tính cổ tay
                float conv = 1f - 0.55f * Mathf.Clamp01(f.mcpFlex / 90f);
                float lag = fingerLagApplied * (0.7f + 0.15f * i);
                ApplyFingerAngles(i, f.mcpFlex + lag, f.mcpSpread * conv * sign, f.pipFlex + lag * 0.6f, f.dipFlex + lag * 0.3f);
            }

            ApplyThumbAngles(d.thumb, sign);
        }

        void ApplyFingerAngles(int i, float mcpFlex, float mcpSpreadSigned, float pipFlex, float dipFlex)
        {
            mcpFlex = Mathf.Clamp(mcpFlex, HandPoseTable.FingerMcpMinFlex, HandPoseTable.FingerMcpMaxFlex);
            mcpSpreadSigned = Mathf.Clamp(mcpSpreadSigned, HandPoseTable.FingerMcpMinSpread, HandPoseTable.FingerMcpMaxSpread);
            pipFlex = Mathf.Clamp(pipFlex, HandPoseTable.FingerPipMinFlex, HandPoseTable.FingerPipMaxFlex);
            dipFlex = Mathf.Clamp(dipFlex, HandPoseTable.FingerDipMinFlex, HandPoseTable.FingerDipMaxFlex);

            proximal[i].localRotation = Quaternion.Euler(mcpFlex, mcpSpreadSigned, 0f);
            middle[i].localRotation = Quaternion.Euler(pipFlex, 0f, 0f);
            distal[i].localRotation = Quaternion.Euler(dipFlex, 0f, 0f);
        }

        void ApplyThumbAngles(ThumbPose t, float sign)
        {
            t = HandPoseTable.ClampThumb(t);
            thumbCmc.localRotation = Quaternion.Euler(t.cmcFlex, t.cmcOppose * sign, t.cmcPronate * sign);
            thumbMcp.localRotation = Quaternion.Euler(t.mcpFlex, 0f, 0f);
            thumbIp.localRotation = Quaternion.Euler(t.ipFlex, 0f, 0f);
        }

        /// <summary>
        /// Chống xuyên vật: bật tạm collider của dụng cụ đích trong khung tính, raycast từ ngoài vào tâm
        /// tìm điểm bề mặt, giải IK 2 khớp trong giới hạn sinh lý để đệm đầu ngón nằm sát mặt dụng cụ (sai số ≤ 3 mm).
        /// Bán kính đệm đo trên lưới da thật (cộng độ dày găng khi đeo găng).
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
                cachedPrevEnabled = new bool[cachedContactCols.Length];
            }
            if (cachedContactCols == null || cachedContactCols.Length == 0) return;

            Bounds toolBounds = default;
            bool hasBounds = false;

            try
            {
                for (int c = 0; c < cachedContactCols.Length; c++)
                {
                    var col = cachedContactCols[c];
                    if (col == null) continue;
                    cachedPrevEnabled[c] = col.enabled;
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

                // Cầm tinh (pipet, khóa buret, kẹp, que, ống nghiệm): ngón trỏ + giữa + ngón cái kẹp ba điểm
                int fingerCount = CurrentPose switch
                {
                    HandPoseId.Pipet or HandPoseId.KhoaBuret or HandPoseId.Kep or HandPoseId.Que or HandPoseId.OngNghiem => 2,
                    _ => 4
                };
                float sign = side == 0 ? 1f : -1f;
                float glove = WearGloves ? 0.0006f : 0f;

                for (int i = 0; i < fingerCount; i++)
                {
                    FingerPose basePose = activePoseData.GetFinger(i);
                    SolveSingleFingerContact(i, basePose, sign, toolCenter, toolAxis, maxExtent, padRadius[i] + glove);
                }

                SolveThumbContact(activePoseData.thumb, sign, toolCenter, toolAxis, maxExtent, padRadius[4] + glove);
            }
            finally
            {
                for (int c = 0; c < cachedContactCols.Length; c++)
                {
                    var col = cachedContactCols[c];
                    if (col != null) col.enabled = cachedPrevEnabled[c];
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

        void SolveThumbContact(ThumbPose t, float sign, Vector3 toolCenter, Vector3 toolAxis, float maxExtent, float thumbPadR)
        {
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

        // ------------------------------------------------------------------
        // Ánh xạ 21 điểm MediaPipe
        // ------------------------------------------------------------------
        static readonly int[] LmFingerBase = { 5, 9, 13, 17 };
        readonly Vector3[] lmWork = new Vector3[21];

        /// <summary>
        /// Ánh xạ đủ 21 điểm MediaPipe (camera space, mét) vào khung xương: khung gan tay khớp từ 4 điểm
        /// (0, 5, 9, 17) theo đúng hình học của bàn tay ảo; mỗi ngón giải gập + dạng/khép ở MCP, gập PIP, DIP
        /// bằng nghịch đảo đúng thứ tự xoay khớp; ngón cái giải CMC 3 bậc (gập, dạng/đối chiếu, xoay sấp từ mặt
        /// phẳng gập MCP–IP), MCP, IP. Không cấp phát bộ nhớ.
        /// </summary>
        public void ApplyMediaPipeLandmarks(Vector3[] lmCameraSpace, Quaternion cameraRot)
        {
            EnsureBuilt();
            if (lmCameraSpace == null || lmCameraSpace.Length < 21) return;
            drivingByLandmarks = true;
            float sign = side == 0 ? 1f : -1f;

            for (int i = 0; i < 21; i++) lmWork[i] = cameraRot * lmCameraSpace[i];

            // Khung gan tay: cùng định nghĩa trên bàn tay ảo (tọa độ gốc) và trên điểm mốc → phép quay khớp
            palmRoot.localPosition = Vector3.zero;
            palmRoot.localRotation = Quaternion.identity;
            Vector3 rigW = WristCenter;
            Vector3 rigM9 = new Vector3(McpX[1] * sign, McpArchY[1], McpZ[1]);
            Vector3 rigM5 = new Vector3(McpX[0] * sign, McpArchY[0], McpZ[0]);
            Vector3 rigM17 = new Vector3(McpX[3] * sign, McpArchY[3], McpZ[3]);
            Quaternion rigFrame = FrameFrom(rigM9 - rigW, (rigM17 - rigM5) * sign);
            Quaternion lmFrame = FrameFrom(lmWork[9] - lmWork[0], (lmWork[17] - lmWork[5]) * sign);
            Quaternion handRot = lmFrame * Quaternion.Inverse(rigFrame);
            transform.rotation = handRot;
            transform.position = lmWork[0] - handRot * rigW;
            Quaternion inv = Quaternion.Inverse(handRot);

            for (int f = 0; f < 4; f++)
            {
                int b = LmFingerBase[f];
                Vector3 d1 = inv * (lmWork[b + 1] - lmWork[b]);
                Vector3 d2 = inv * (lmWork[b + 2] - lmWork[b + 1]);
                Vector3 d3 = inv * (lmWork[b + 3] - lmWork[b + 2]);
                if (d1.sqrMagnitude < 1e-10f) continue;
                d1.Normalize();
                // proximal = Ry(spread) * Rx(flex): hướng = (sin s cos f, -sin f, cos s cos f)
                float flex = Mathf.Asin(Mathf.Clamp(-d1.y, -1f, 1f)) * Mathf.Rad2Deg;
                float spread = Mathf.Atan2(d1.x, d1.z) * Mathf.Rad2Deg;
                Quaternion qProx = Quaternion.Euler(flex, spread, 0f);
                Vector3 m2 = Quaternion.Inverse(qProx) * d2;
                float pip = Mathf.Atan2(-m2.y, m2.z) * Mathf.Rad2Deg;
                Quaternion qMid = qProx * Quaternion.Euler(pip, 0f, 0f);
                Vector3 m3 = Quaternion.Inverse(qMid) * d3;
                float dip = Mathf.Atan2(-m3.y, m3.z) * Mathf.Rad2Deg;
                ApplyFingerAngles(f, flex, spread, pip, dip);
            }

            // Ngón cái: CMC = Ry(op·s) Rx(flex) Rz(pr·s)
            Vector3 t1 = inv * (lmWork[2] - lmWork[1]);
            Vector3 t2 = inv * (lmWork[3] - lmWork[2]);
            Vector3 t3 = inv * (lmWork[4] - lmWork[3]);
            if (t1.sqrMagnitude > 1e-10f)
            {
                t1.Normalize();
                float cFlex = Mathf.Asin(Mathf.Clamp(-t1.y, -1f, 1f)) * Mathf.Rad2Deg;
                float cY = Mathf.Atan2(t1.x, t1.z) * Mathf.Rad2Deg;
                Quaternion qyx = Quaternion.Euler(cFlex, cY, 0f);
                // trục gập MCP–IP = pháp tuyến mặt phẳng (t1, t2) hoặc (t2, t3)
                Vector3 axis = Vector3.Cross(t1, t2.normalized);
                if (axis.sqrMagnitude < 0.02f) axis = Vector3.Cross(t2.normalized, t3.normalized);
                float pron = thumbPronateMemory;
                if (axis.sqrMagnitude >= 0.02f)
                {
                    // Gập dương quanh +x cục bộ đưa +z về -y: z × (-y) = +x → trục gập là +x cục bộ
                    Vector3 ax = Quaternion.Inverse(qyx) * axis.normalized;
                    pron = Mathf.Atan2(ax.y, ax.x) * Mathf.Rad2Deg * sign;
                    thumbPronateMemory = pron;
                }
                Quaternion qCmc = Quaternion.Euler(cFlex, cY, pron * sign);
                Vector3 m2 = Quaternion.Inverse(qCmc) * t2;
                float tMcp = Mathf.Atan2(-m2.y, m2.z) * Mathf.Rad2Deg;
                Quaternion qM = qCmc * Quaternion.Euler(tMcp, 0f, 0f);
                Vector3 m3 = Quaternion.Inverse(qM) * t3;
                float tIp = Mathf.Atan2(-m3.y, m3.z) * Mathf.Rad2Deg;
                ApplyThumbAngles(new ThumbPose(cFlex, cY * sign, pron, tMcp, tIp), sign);
            }
        }

        static Quaternion FrameFrom(Vector3 forward, Vector3 across)
        {
            Vector3 up = Vector3.Cross(forward, across);
            if (up.sqrMagnitude < 1e-12f) up = Vector3.up;
            return Quaternion.LookRotation(forward, up);
        }

        /// <summary>Đọc ngược 21 điểm mốc (thế giới) từ khung xương hiện tại – dùng cho kiểm thử khứ hồi.</summary>
        public void ReadLandmarks(Vector3[] outWorld)
        {
            EnsureBuilt();
            if (outWorld == null || outWorld.Length < 21) return;
            outWorld[0] = palmRoot.TransformPoint(WristCenter);
            outWorld[1] = thumbCmc.position; outWorld[2] = thumbMcp.position; outWorld[3] = thumbIp.position;
            outWorld[4] = thumbIp.TransformPoint(new Vector3(0f, 0f, ThumbDistLen));
            for (int f = 0; f < 4; f++)
            {
                int b = LmFingerBase[f];
                outWorld[b] = proximal[f].position;
                outWorld[b + 1] = middle[f].position;
                outWorld[b + 2] = distal[f].position;
                outWorld[b + 3] = distal[f].TransformPoint(new Vector3(0f, 0f, DistLen[f]));
            }
        }

        public HandPoseData ActivePose => activePoseData;
        public void ApplyPoseNow(HandPoseData d) { EnsureBuilt(); activePoseData = d; ApplyPoseData(d); }

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
        public float PadRadius(int i) => padRadius[Mathf.Clamp(i, 0, 4)];

        static Transform Joint(Transform parent, string name, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }
    }
}
