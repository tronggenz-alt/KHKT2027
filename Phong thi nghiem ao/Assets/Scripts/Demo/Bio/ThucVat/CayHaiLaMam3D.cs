using System.Collections.Generic;
using Khkt.Rendering;
using Khkt.Simulation.Bio.ThucVat;
using UnityEngine;

namespace Khkt.Demo.Bio.ThucVat
{
    public enum KhungNhinThucVat
    {
        TongThe = 0,
        LatCatRe = 1,
        LatCatThan = 2,
        LatCatLa = 3
    }

    /// <summary>
    /// Thẻ định danh bộ phận 3D trên mô hình cây hai lá mầm và 3 lát cắt giải phẫu.
    /// </summary>
    public class ThucVatPartTag : MonoBehaviour
    {
        public string PartId;
        public Renderer TargetRenderer;
        public Color BaseColor = Color.white;
    }

    /// <summary>
    /// Điểm neo chú thích 3D để giao diện chiếu sang nhãn viên thuốc (◉ Tên bộ phận) đồng bộ Phòng Giải phẫu.
    /// </summary>
    public sealed class PinChuThich3D
    {
        public KhungNhinThucVat View;
        public string PartId;
        public string Label;
        public Transform Anchor;
        public Vector3 LocalOffset;
        public Color DotColor;
    }

    /// <summary>
    /// Mô hình 3D Cây hai lá mầm (cây đậu Phaseolus vulgaris) và 3 lát cắt giải phẫu vi thể (Rễ, Thân, Lá).
    /// Thiết kế theo chuẩn mẫu vật thực tế:
    ///   • Cây tổng thể: chậu gốm đất nung cắt mở, khối đất phân tầng mùn tự nhiên, hệ rễ cọc uốn hữu cơ có nốt sần
    ///     và lông hút, thân đậu 7 đốt có cửa sổ rạch giải phẫu lộ bó mạch bên trong, tán lá kép 3 lá chét gân nổi 3D,
    ///     quả đậu non bổ mở hạt & chùm hoa đậu hình bướm mẫu 5.
    ///   • 3 Lát cắt vi thể (Rễ, Thân, Lá): khối mô học liền khối 3D cắt bậc thang giải phẫu (không rời rạc hay hở nền),
    ///     kết hợp bản đồ mô học 384x384 + Normal Map, ống mạch gỗ rỗng lòng có vân xoắn lignin, đai Caspari thấm suberin,
    ///     thịt lá mô giậu–mô xốp có buồng khí dưới khí khổng & 2 phức hợp khí khổng đóng mở theo sức trương/ABA.
    /// </summary>
    public class CayHaiLaMam3D : MonoBehaviour
    {
        public const int StageLayer = 28;
        public static readonly Vector3 StageOrigin = new Vector3(0f, -420f, 0f);

        public KhungNhinThucVat KhungHienTai { get; private set; } = KhungNhinThucVat.TongThe;
        public string BoPhanDangChon { get; private set; } = "than_cay";
        public bool HienChuThich { get; private set; } = true;
        public bool CheDoBocTach { get; private set; }
        public bool CheDoXQuangMach { get; private set; }
        public bool HienHatDong { get; private set; } = true;
        public bool HienDenHuongSang { get; private set; } = true;
        public IReadOnlyList<PinChuThich3D> DanhSachPin => annotationPins;

        Transform wholeRoot;
        Transform rootSliceRoot;
        Transform stemSliceRoot;
        Transform leafSliceRoot;
        Transform particleRoot;

        // Cây tổng thể: 7 đốt thân tỉ lệ thực tế uốn hướng sáng mượt
        Transform stemMount;
        readonly Transform[] stemNodes = new Transform[7];
        readonly Vector3[] stemBaseLocalPos = new Vector3[7];
        Transform lampPivot;
        Transform lampBulbObj;
        Transform lampConeObj;

        // 2 Khí khổng động ở lát cắt lá (mỗi khí khổng gồm 2 tế bào hạt đậu uốn cong liền khối)
        struct StomaRig
        {
            public Transform leftCell, rightCell;
            public Transform leftThickWall, rightThickWall;
            public Transform poreVoid;
            public Vector3 centerPos;
        }
        readonly List<StomaRig> stomaRigs = new List<StomaRig>(2);

        // Vật liệu giải phẫu & bản đồ mô học
        Material leafMat;
        Material veinMat;
        Material stemMat;
        Material stemCutawayMat;
        Material rootMat;
        Material xylemFlowMat;
        Material phloemFlowMat;
        Material caspariMat;
        Material bulbGlowMat;
        Material lampBeamMat;
        Material waterPtMat;
        Material sugarPtMat;
        Material co2PtMat;
        Material o2PtMat;
        Material auxinPtMat;

        // Texture PBR sinh thủ tục
        Texture2D leafTex, leafNormalTex;
        Texture2D stemBarkTex, stemBarkNormalTex;
        Texture2D terracottaTex, terracottaNormalTex;
        Texture2D soilTopTex, soilTopNormalTex, soilProfileTex;
        Texture2D rootHistTex, rootHistNormalTex;
        Texture2D stemHistTex, stemHistNormalTex;
        Texture2D leafUpperTex, leafUpperNormalTex;
        Texture2D leafCutFaceTex, leafCutFaceNormalTex;
        Texture2D cellBrickTex, cellBrickNormalTex;
        Texture2D softBeamTex;

        struct ExplodeEntry
        {
            public Transform tr;
            public Vector3 basePos;
            public Vector3 offset;
        }

        readonly List<ExplodeEntry> explodeItems = new List<ExplodeEntry>();
        readonly List<Renderer> nonVascularWholeRenderers = new List<Renderer>();
        readonly List<PinChuThich3D> annotationPins = new List<PinChuThich3D>();
        readonly Dictionary<string, (Transform tr, Vector3 offset)> namedAnchors = new Dictionary<string, (Transform, Vector3)>();
        readonly List<ThucVatPartTag> allParts = new List<ThucVatPartTag>();
        readonly List<Material> createdMaterials = new List<Material>();
        readonly List<Mesh> createdMeshes = new List<Mesh>();
        readonly List<Texture2D> createdTextures = new List<Texture2D>();

        // Bể hạt pre-allocated (tuân thủ HardwareProfile.SoHatToiDa, kích thước nhỏ tinh tế)
        struct HatDong
        {
            public Transform tr;
            public float phase;
            public int lane;
        }
        HatDong[] waterPool, sugarPool, co2Pool, o2Pool, auxinPool;
        float[] waterProgress;
        TrangThaiThucVat latestState;
        DieuKienMoiTruong latestEnv;
        float animTime;
        float explodeLerp;
        ThucVatPartTag highlightedTag;
        MaterialPropertyBlock highlightBlock;

        public void Initialize(Transform cameraTransform)
        {
            highlightBlock = new MaterialPropertyBlock();
            gameObject.layer = StageLayer;
            transform.position = StageOrigin;

            BuildLighting();
            BuildMaterials();
            BuildWholePlant();
            BuildRootSlice();
            BuildStemSlice();
            BuildLeafSlice();
            BuildParticlePools();

            SetKhungNhin(KhungNhinThucVat.TongThe);
            SetHienChuThich(true);
        }

        public void SetKhungNhin(KhungNhinThucVat view)
        {
            KhungHienTai = view;
            if (wholeRoot != null) wholeRoot.gameObject.SetActive(view == KhungNhinThucVat.TongThe);
            if (rootSliceRoot != null) rootSliceRoot.gameObject.SetActive(view == KhungNhinThucVat.LatCatRe);
            if (stemSliceRoot != null) stemSliceRoot.gameObject.SetActive(view == KhungNhinThucVat.LatCatThan);
            if (leafSliceRoot != null) leafSliceRoot.gameObject.SetActive(view == KhungNhinThucVat.LatCatLa);
        }

        public void SetHienChuThich(bool show)
        {
            HienChuThich = show;
        }

        public void SetHienHatDong(bool show)
        {
            HienHatDong = show;
            if (particleRoot != null) particleRoot.gameObject.SetActive(show);
        }

        public void SetHienDenHuongSang(bool show)
        {
            HienDenHuongSang = show;
            if (lampPivot != null) lampPivot.gameObject.SetActive(show);
        }

        public void SetCheDoBocTach(bool on)
        {
            CheDoBocTach = on;
        }

        public void SetCheDoXQuangMach(bool on)
        {
            CheDoXQuangMach = on;
            if (stemMat != null && stemCutawayMat != null)
            {
                for (int i = 0; i < nonVascularWholeRenderers.Count; i++)
                {
                    var r = nonVascularWholeRenderers[i];
                    if (r != null) r.sharedMaterial = on ? stemCutawayMat : stemMat;
                }
            }
        }

        public void SelectPart(string partId)
        {
            if (highlightedTag != null && highlightedTag.TargetRenderer != null)
            {
                for (int i = 0; i < allParts.Count; i++)
                {
                    if (allParts[i] != null && allParts[i].TargetRenderer != null && allParts[i].PartId == BoPhanDangChon)
                        allParts[i].TargetRenderer.SetPropertyBlock(null);
                }
            }

            BoPhanDangChon = partId;
            highlightedTag = null;
            for (int i = 0; i < allParts.Count; i++)
            {
                if (allParts[i] != null && allParts[i].PartId == partId)
                {
                    if (highlightedTag == null) highlightedTag = allParts[i];
                    var r = allParts[i].TargetRenderer;
                    if (r != null)
                    {
                        r.GetPropertyBlock(highlightBlock);
                        Color glow = Color.Lerp(allParts[i].BaseColor, new Color(0.40f, 0.94f, 0.78f), 0.34f);
                        highlightBlock.SetColor("_BaseColor", glow);
                        highlightBlock.SetColor("_Color", glow);
                        r.SetPropertyBlock(highlightBlock);
                    }
                }
            }
        }

        public bool TryGetPartBounds(string partId, out Bounds combinedBounds)
        {
            combinedBounds = default;
            bool found = false;
            for (int i = 0; i < allParts.Count; i++)
            {
                var p = allParts[i];
                if (p == null || p.PartId != partId || p.TargetRenderer == null || !p.TargetRenderer.gameObject.activeInHierarchy)
                    continue;
                if (!found)
                {
                    combinedBounds = p.TargetRenderer.bounds;
                    found = true;
                }
                else
                {
                    combinedBounds.Encapsulate(p.TargetRenderer.bounds);
                }
            }
            return found;
        }

        public void GetDefaultCameraPose(KhungNhinThucVat view, out Vector3 pivot, out float yaw, out float pitch, out float dist)
        {
            switch (view)
            {
                case KhungNhinThucVat.LatCatRe:
                    pivot = StageOrigin + new Vector3(0f, 0.068f, 0f);
                    yaw = 24f; pitch = 36f; dist = 0.80f;
                    break;
                case KhungNhinThucVat.LatCatThan:
                    pivot = StageOrigin + new Vector3(0f, 0.080f, 0f);
                    yaw = 24f; pitch = 35f; dist = 0.78f;
                    break;
                case KhungNhinThucVat.LatCatLa:
                    pivot = StageOrigin + new Vector3(0f, 0.065f, 0f);
                    yaw = 18f; pitch = 20f; dist = 0.78f;
                    break;
                default:
                    pivot = StageOrigin + new Vector3(0f, 0.145f, 0f);
                    yaw = 18f; pitch = 12f; dist = 1.72f;
                    break;
            }
        }

        public void ApplySimulation(in DieuKienMoiTruong dk, in TrangThaiThucVat st, float dt, float dayTimeScale)
        {
            latestEnv = dk;
            latestState = st;
            animTime += dt * dayTimeScale;

            // 1. Uốn thân tự nhiên theo hướng sáng & vươn dài khi thiếu sáng (etiolation)
            float totalBendDeg = (float)st.GocUonThanDo;
            float stepBend = totalBendDeg / stemNodes.Length;
            float stretchY = (float)st.HeSoVuonDaiTrongToi;
            bool night = st.BanDem;

            for (int i = 0; i < stemNodes.Length; i++)
            {
                if (stemNodes[i] == null) continue;
                float weight = 0.32f + (i / (float)stemNodes.Length) * 1.18f;
                stemNodes[i].localRotation = Quaternion.Euler(0f, 0f, -stepBend * weight);
                stemNodes[i].localPosition = new Vector3(
                    stemBaseLocalPos[i].x,
                    stemBaseLocalPos[i].y * (i == 0 ? 1f : Mathf.Lerp(1f, stretchY, 0.42f)),
                    stemBaseLocalPos[i].z);
            }

            // Màu diệp lục & độ rung gió tự nhiên
            if (leafMat != null)
            {
                Color healthy = new Color(0.22f, 0.56f, 0.22f, 1f);
                Color etiolated = new Color(0.64f, 0.66f, 0.25f, 1f);
                Color leafCol = Color.Lerp(healthy, etiolated, Mathf.Clamp01((stretchY - 1f) * 1.1f));
                if (night) leafCol *= 0.66f;
                leafCol.a = 1f;
                leafMat.SetColor("_BaseColor", leafCol);
                leafMat.SetFloat("_WindSpeed", 1.0f + (float)dk.Gio01 * 4.5f);
                leafMat.SetFloat("_WindStrength", 0.003f + (float)dk.Gio01 * 0.020f);
            }

            // 2. Xoay đèn chiếu hướng sáng
            if (lampPivot != null)
            {
                lampPivot.gameObject.SetActive(HienDenHuongSang);
                lampPivot.localRotation = Quaternion.Euler(0f, 0f, -(float)dk.GocDenHuongSangDo);
            }
            if (bulbGlowMat != null)
            {
                float lum = Mathf.Clamp01((float)(dk.AnhSangLux / 35000.0));
                Color c = Color.Lerp(new Color(0.18f, 0.20f, 0.24f), new Color(1.0f, 0.96f, 0.78f), lum);
                bulbGlowMat.SetColor("_BaseColor", c);
                bulbGlowMat.SetColor("_EmissionColor", c * (lum * 1.5f));
            }
            if (lampConeObj != null)
            {
                lampConeObj.gameObject.SetActive(HienDenHuongSang && !night);
                if (lampBeamMat != null)
                {
                    float lum = Mathf.Clamp01((float)(dk.AnhSangLux / 45000.0));
                    lampBeamMat.SetColor("_BaseColor", new Color(1.0f, 0.95f, 0.72f, 0.03f + lum * 0.11f));
                }
            }

            // 3. Dòng chảy trong mạch gỗ (Xylem ↑) & mạch rây (Phloem ↓)
            if (xylemFlowMat != null)
            {
                float xSpd = Mathf.Clamp((float)st.TocDoMachGo * 0.65f, 0.08f, 3.0f);
                xylemFlowMat.SetFloat("_FlowSpeed", xSpd);
                xylemFlowMat.SetFloat("_FlowStrength", Mathf.Clamp((float)st.TocDoMachGo * 0.55f, 0.25f, 1.35f));
            }
            if (phloemFlowMat != null)
            {
                float pSpd = -Mathf.Clamp((float)st.TocDoMachRay * 1.5f, 0.06f, 2.5f);
                phloemFlowMat.SetFloat("_FlowSpeed", pSpd);
                phloemFlowMat.SetFloat("_FlowStrength", Mathf.Clamp((float)st.TocDoMachRay * 1.3f, 0.18f, 1.25f));
            }

            // 4. Đóng mở 2 phức hợp khí khổng trên lát cắt lá
            UpdateStomataVisual((float)st.DoMoKhiKhong01, (float)st.DoTruongTeBaoHatDau01);

            // 5. Cập nhật hệ hạt sinh lý tinh tế
            if (HienHatDong) UpdateParticles(in st, dt);
        }

        void LateUpdate()
        {
            float targetExplode = CheDoBocTach ? 1f : 0f;
            if (Mathf.Abs(explodeLerp - targetExplode) > 0.0005f)
            {
                explodeLerp = Mathf.Lerp(explodeLerp, targetExplode, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
                for (int i = 0; i < explodeItems.Count; i++)
                {
                    var item = explodeItems[i];
                    if (item.tr != null)
                        item.tr.localPosition = item.basePos + item.offset * explodeLerp;
                }
            }
        }

        void RegisterExplode(Transform tr, Vector3 offset)
        {
            if (tr == null) return;
            explodeItems.Add(new ExplodeEntry { tr = tr, basePos = tr.localPosition, offset = offset });
        }

        void UpdateStomataVisual(float open01, float turgor01)
        {
            float bow = Mathf.Lerp(0.0012f, 0.0145f, open01);
            float swellX = Mathf.Lerp(0.84f, 1.18f, turgor01);
            float swellY = Mathf.Lerp(0.88f, 1.14f, turgor01);
            float poreW = Mathf.Max(0.0022f, Mathf.Lerp(0.0022f, 0.022f, open01));

            for (int i = 0; i < stomaRigs.Count; i++)
            {
                var rig = stomaRigs[i];
                if (rig.leftCell != null)
                {
                    rig.leftCell.localPosition = rig.centerPos + new Vector3(-0.0045f - bow * 0.52f, 0f, 0f);
                    rig.leftCell.localScale = new Vector3(swellX, swellY, 1f);
                }
                if (rig.rightCell != null)
                {
                    rig.rightCell.localPosition = rig.centerPos + new Vector3(0.0045f + bow * 0.52f, 0f, 0f);
                    rig.rightCell.localScale = new Vector3(swellX, swellY, 1f);
                }
                if (rig.leftThickWall != null)
                    rig.leftThickWall.localPosition = rig.centerPos + new Vector3(-0.0038f - bow * 0.62f, 0f, 0f);
                if (rig.rightThickWall != null)
                    rig.rightThickWall.localPosition = rig.centerPos + new Vector3(0.0038f + bow * 0.62f, 0f, 0f);
                if (rig.poreVoid != null)
                    rig.poreVoid.localScale = new Vector3(poreW, 0.015f, 0.036f);
            }
        }

        void UpdateParticles(in TrangThaiThucVat st, float dt)
        {
            int cap = Mathf.Max(16, HardwareProfile.SoHatToiDa);

            // 1. Hạt nước & ion khoáng (xanh lam)
            int maxWater = Mathf.Min(waterPool.Length, Mathf.Max(4, cap / 6));
            int activeWater = Mathf.Clamp(Mathf.RoundToInt((float)(st.TocDoMachGo * 4.0)), 2, maxWater);
            float waterSpd = Mathf.Clamp((float)st.TocDoMachGo * 0.14f, 0.04f, 0.85f);

            for (int i = 0; i < waterPool.Length; i++)
            {
                bool on = i < activeWater;
                var go = waterPool[i].tr.gameObject;
                if (go.activeSelf != on) go.SetActive(on);
                if (!on) continue;

                waterProgress[i] = (waterProgress[i] + dt * waterSpd) % 1f;
                float u = waterProgress[i];
                if (KhungHienTai == KhungNhinThucVat.TongThe)
                {
                    // Chạy gọn bên trong cửa sổ giải phẫu rễ & đốt thân 1..2 (không trồi ra ngoài vỏ cây)
                    float y = Mathf.Lerp(-0.24f, 0.44f, u);
                    bool visibleInCut = CheDoXQuangMach || y <= 0.255f;
                    if (go.activeSelf != visibleInCut) go.SetActive(visibleInCut);
                    if (!visibleInCut) continue;

                    Vector3 worldOnStem = SampleStemAxis(Mathf.InverseLerp(0f, 0.54f, Mathf.Max(0f, y)));
                    float offX = y < 0f ? -0.0022f : -0.0020f;
                    waterPool[i].tr.position = worldOnStem + new Vector3(offX, y < 0f ? y : 0f, -0.0022f);
                }
                else if (KhungHienTai == KhungNhinThucVat.LatCatRe)
                {
                    if (u < 0.50f)
                    {
                        float tRad = u / 0.50f;
                        // Đi trên mặt cắt bậc thang phía trước về phía trung trụ
                        float ang = Mathf.Lerp(-122f, -58f, (waterPool[i].lane + 0.5f) / 4f) * Mathf.Deg2Rad;
                        float rad = Mathf.Lerp(0.21f, 0.022f, tRad);
                        waterPool[i].tr.position = StageOrigin + new Vector3(Mathf.Cos(ang) * rad, 0.078f + tRad * 0.055f, Mathf.Sin(ang) * rad);
                    }
                    else
                    {
                        float tUp = (u - 0.50f) / 0.50f;
                        float ang = waterPool[i].lane * 1.57f;
                        waterPool[i].tr.position = StageOrigin + new Vector3(Mathf.Cos(ang) * 0.016f, Mathf.Lerp(0.12f, 0.195f, tUp), Mathf.Sin(ang) * 0.016f);
                    }
                }
                else if (KhungHienTai == KhungNhinThucVat.LatCatThan)
                {
                    float ang = (waterPool[i].lane * 90f + 18f) * Mathf.Deg2Rad;
                    waterPool[i].tr.position = StageOrigin + new Vector3(Mathf.Cos(ang) * 0.106f, Mathf.Lerp(0.06f, 0.195f, u), Mathf.Sin(ang) * 0.106f);
                }
                else
                {
                    waterPool[i].tr.position = StageOrigin + new Vector3((waterPool[i].lane - 1.5f) * 0.0055f, 0.058f, Mathf.Lerp(-0.135f, 0.135f, u));
                }
            }

            // 2. Hạt đường saccarôzơ trong mạch rây (vàng cam)
            int maxSugar = Mathf.Min(sugarPool.Length, Mathf.Max(3, cap / 8));
            int activeSugar = st.TocDoMachRay > 0.02 ? Mathf.Clamp(Mathf.RoundToInt((float)(st.TocDoMachRay * 16.0)), 2, maxSugar) : 0;
            float sugarSpd = Mathf.Clamp((float)st.TocDoMachRay * 0.42f, 0.06f, 0.62f);

            for (int i = 0; i < sugarPool.Length; i++)
            {
                bool on = i < activeSugar;
                var go = sugarPool[i].tr.gameObject;
                if (go.activeSelf != on) go.SetActive(on);
                if (!on) continue;

                float u = (sugarPool[i].phase + animTime * sugarSpd) % 1f;
                if (KhungHienTai == KhungNhinThucVat.TongThe)
                {
                    float y = Mathf.Lerp(0.42f, -0.22f, u);
                    bool visibleInCut = CheDoXQuangMach || y <= 0.255f;
                    if (go.activeSelf != visibleInCut) go.SetActive(visibleInCut);
                    if (!visibleInCut) continue;

                    Vector3 p = SampleStemAxis(Mathf.InverseLerp(0f, 0.54f, Mathf.Max(0f, y)));
                    sugarPool[i].tr.position = p + new Vector3(0.0022f, y < 0f ? y : 0f, -0.0022f);
                }
                else if (KhungHienTai == KhungNhinThucVat.LatCatThan)
                {
                    float ang = (sugarPool[i].lane * 90f + 18f) * Mathf.Deg2Rad;
                    sugarPool[i].tr.position = StageOrigin + new Vector3(Mathf.Cos(ang) * 0.148f, Mathf.Lerp(0.19f, 0.06f, u), Mathf.Sin(ang) * 0.148f);
                }
                else if (KhungHienTai == KhungNhinThucVat.LatCatRe)
                {
                    float ang = (45f + sugarPool[i].lane * 90f) * Mathf.Deg2Rad;
                    sugarPool[i].tr.position = StageOrigin + new Vector3(Mathf.Cos(ang) * 0.048f, Mathf.Lerp(0.185f, 0.075f, u), Mathf.Sin(ang) * 0.048f);
                }
                else
                {
                    sugarPool[i].tr.position = StageOrigin + new Vector3((sugarPool[i].lane - 1.5f) * 0.0055f, 0.025f, Mathf.Lerp(0.135f, -0.135f, u));
                }
            }

            // 3. Hạt khí CO2 đi vào & O2 thoát ra qua khí khổng
            bool showGas = !st.BanDem && st.O2ThaiRaMoiGiay > 0.02 && (KhungHienTai == KhungNhinThucVat.TongThe || KhungHienTai == KhungNhinThucVat.LatCatLa);
            int maxGas = Mathf.Min(co2Pool.Length, Mathf.Max(3, cap / 9));
            int activeGas = showGas ? Mathf.Clamp(Mathf.RoundToInt((float)(st.O2ThaiRaMoiGiay * maxGas)), 2, maxGas) : 0;
            float gasSpd = Mathf.Clamp(0.22f + (float)st.O2ThaiRaMoiGiay * 0.60f, 0.14f, 0.85f);

            for (int i = 0; i < co2Pool.Length; i++)
            {
                bool on = i < activeGas;
                if (co2Pool[i].tr.gameObject.activeSelf != on) co2Pool[i].tr.gameObject.SetActive(on);
                if (o2Pool[i].tr.gameObject.activeSelf != on) o2Pool[i].tr.gameObject.SetActive(on);
                if (!on) continue;

                float u = (co2Pool[i].phase + animTime * gasSpd) % 1f;
                if (KhungHienTai == KhungNhinThucVat.TongThe)
                {
                    Vector3 anchor = SampleStemAxis(0.38f + (i % 3) * 0.20f) + new Vector3((i % 2 == 0 ? 1 : -1) * 0.14f, 0.01f, 0.01f);
                    co2Pool[i].tr.position = anchor + new Vector3(0.045f * (1f - u), -0.04f * (1f - u), 0.025f * (1f - u));
                    o2Pool[i].tr.position = anchor + new Vector3(-0.025f * u, 0.085f * u, -0.02f * u);
                }
                else
                {
                    // Khuếch tán đúng qua 2 lỗ khí khổng ở x = ±0.105m vào buồng dưới khí khổng
                    float stomaX = (i % 2 == 0) ? 0.105f : -0.105f;
                    float stomaZ = (i % 2 == 0) ? -0.088f : -0.055f;
                    float spread = ((co2Pool[i].lane - 1.5f) * 0.0055f);
                    co2Pool[i].tr.position = StageOrigin + new Vector3(stomaX + spread, Mathf.Lerp(-0.095f, 0.052f, u), stomaZ + 0.005f);
                    o2Pool[i].tr.position = StageOrigin + new Vector3(stomaX - spread, Mathf.Lerp(0.050f, -0.105f, u), stomaZ - 0.005f);
                }
            }

            // 4. Hạt Auxin (IAA) — chỉ hiển thị khi hướng sáng nghiêng rõ hoặc bật Soi mạch để cây tổng thể sạch tự nhiên
            bool showAuxin = KhungHienTai == KhungNhinThucVat.TongThe && (CheDoXQuangMach || Mathf.Abs((float)latestEnv.GocDenHuongSangDo) >= 12f);
            int maxAuxin = showAuxin ? Mathf.Min(auxinPool.Length, Mathf.Max(4, cap / 10)) : 0;
            int darkSideCount = Mathf.RoundToInt(maxAuxin * (float)st.TiLeAuxinPhiaToi);
            float lightDirSign = latestEnv.GocDenHuongSangDo >= 0 ? 1f : -1f;
            for (int i = 0; i < auxinPool.Length; i++)
            {
                bool on = i < maxAuxin;
                var go = auxinPool[i].tr.gameObject;
                if (go.activeSelf != on) go.SetActive(on);
                if (!on) continue;

                bool onDarkSide = i < darkSideCount;
                float u = (auxinPool[i].phase + animTime * 0.16f) % 1f;
                float hFrac = 0.25f + u * 0.65f;
                Vector3 p = SampleStemAxis(hFrac);
                float sideX = (onDarkSide ? -lightDirSign : lightDirSign) * (0.0068f + (i % 2) * 0.0018f);
                auxinPool[i].tr.position = p + new Vector3(sideX, 0f, Mathf.Sin(i * 1.9f) * 0.0035f);
            }
        }

        Vector3 SampleStemAxis(float t01)
        {
            t01 = Mathf.Clamp01(t01);
            float f = t01 * (stemNodes.Length - 1);
            int idx = Mathf.Min(stemNodes.Length - 2, Mathf.FloorToInt(f));
            float rem = f - idx;
            return Vector3.Lerp(stemNodes[idx].position, stemNodes[idx + 1].position, rem);
        }

        // ---------------------------------------------------------------------
        // Ánh sáng studio 3 điểm & vật liệu PBR mô học chân thực
        // ---------------------------------------------------------------------
        void BuildLighting()
        {
            var keyGo = new GameObject("Đèn chính sân khấu") { layer = StageLayer };
            keyGo.transform.SetParent(transform, false);
            keyGo.transform.rotation = Quaternion.Euler(36f, -38f, 0f);
            var key = keyGo.AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 0.56f;
            key.color = new Color(1f, 0.98f, 0.93f);
            key.shadows = LightShadows.None;
            key.cullingMask = 1 << StageLayer;

            var fillGo = new GameObject("Đèn phụ sân khấu") { layer = StageLayer };
            fillGo.transform.SetParent(transform, false);
            fillGo.transform.rotation = Quaternion.Euler(-18f, 142f, 0f);
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.28f;
            fill.color = new Color(0.78f, 0.90f, 1.0f);
            fill.shadows = LightShadows.None;
            fill.cullingMask = 1 << StageLayer;

            var rimGo = new GameObject("Đèn viền nổi khối") { layer = StageLayer };
            rimGo.transform.SetParent(transform, false);
            rimGo.transform.rotation = Quaternion.Euler(16f, 200f, 0f);
            var rim = rimGo.AddComponent<Light>();
            rim.type = LightType.Directional;
            rim.intensity = 0.26f;
            rim.color = new Color(0.76f, 0.96f, 0.86f);
            rim.shadows = LightShadows.None;
            rim.cullingMask = 1 << StageLayer;
        }

        void BuildMaterials()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            Shader leafShader = Shader.Find("Khkt/Bio/LaCay") ?? urpLit;
            Shader flowShader = Shader.Find("Khkt/Bio/LaCayMach") ?? urpLit;

            BuildLeafTextures();
            BuildSoilAndPotTextures();
            BuildHistologyTextures();
            BuildSoftBeamTexture();

            leafMat = NewMat(leafShader, new Color(0.22f, 0.56f, 0.22f, 1f), 0.44f);
            leafMat.SetTexture("_BaseMap", leafTex);
            if (leafMat.HasProperty("_BumpMap")) leafMat.SetTexture("_BumpMap", leafNormalTex);
            if (leafMat.HasProperty("_BumpScale")) leafMat.SetFloat("_BumpScale", 1.30f);
            if (leafMat.HasProperty("_Translucency")) leafMat.SetFloat("_Translucency", 0.62f);
            if (leafMat.HasProperty("_Cutoff")) leafMat.SetFloat("_Cutoff", 0.30f);

            veinMat = NewMat(urpLit, new Color(0.42f, 0.66f, 0.28f), 0.38f);
            stemMat = NewTexturedMat(urpLit, new Color(0.28f, 0.54f, 0.20f), stemBarkTex, stemBarkNormalTex, 0.34f, 0.72f);
            stemCutawayMat = NewTransparentLit(urpLit, new Color(0.28f, 0.56f, 0.24f, 0.22f));
            rootMat = NewTexturedMat(urpLit, new Color(0.70f, 0.55f, 0.35f), stemBarkTex, stemBarkNormalTex, 0.24f, 0.60f);

            xylemFlowMat = NewMat(flowShader, new Color(0.10f, 0.52f, 0.94f, 0.84f), 0.58f);
            if (xylemFlowMat.HasProperty("_PulseColor"))
            {
                xylemFlowMat.SetColor("_PulseColor", new Color(0.48f, 0.94f, 1.0f, 0.98f));
                xylemFlowMat.SetFloat("_FlowSpeed", 1.05f);
                xylemFlowMat.SetFloat("_PulseDensity", 6.5f);
            }

            phloemFlowMat = NewMat(flowShader, new Color(0.90f, 0.52f, 0.14f, 0.84f), 0.58f);
            if (phloemFlowMat.HasProperty("_PulseColor"))
            {
                phloemFlowMat.SetColor("_PulseColor", new Color(1.0f, 0.86f, 0.36f, 0.98f));
                phloemFlowMat.SetFloat("_FlowSpeed", -0.8f);
                phloemFlowMat.SetFloat("_PulseDensity", 5.8f);
            }

            caspariMat = NewMat(urpLit, new Color(0.68f, 0.14f, 0.10f), 0.40f);
            caspariMat.EnableKeyword("_EMISSION");
            caspariMat.SetColor("_EmissionColor", new Color(0.10f, 0.02f, 0.01f));

            bulbGlowMat = NewMat(urpLit, new Color(1f, 0.96f, 0.74f), 0.65f);
            bulbGlowMat.EnableKeyword("_EMISSION");

            lampBeamMat = NewTransparentLit(urpLit, new Color(1f, 0.95f, 0.72f, 0.10f));
            if (lampBeamMat.HasProperty("_BaseMap")) lampBeamMat.SetTexture("_BaseMap", softBeamTex);

            waterPtMat = NewMat(urpLit, new Color(0.22f, 0.80f, 1f), 0.65f);
            waterPtMat.EnableKeyword("_EMISSION");
            waterPtMat.SetColor("_EmissionColor", new Color(0.06f, 0.30f, 0.48f));

            sugarPtMat = NewMat(urpLit, new Color(1f, 0.76f, 0.18f), 0.55f);
            sugarPtMat.EnableKeyword("_EMISSION");
            sugarPtMat.SetColor("_EmissionColor", new Color(0.36f, 0.22f, 0.02f));

            co2PtMat = NewMat(urpLit, new Color(0.80f, 0.85f, 0.90f), 0.38f);
            o2PtMat = NewMat(urpLit, new Color(0.24f, 0.95f, 0.62f), 0.60f);
            o2PtMat.EnableKeyword("_EMISSION");
            o2PtMat.SetColor("_EmissionColor", new Color(0.06f, 0.32f, 0.18f));

            auxinPtMat = NewMat(urpLit, new Color(0.98f, 0.88f, 0.18f), 0.55f);
            auxinPtMat.EnableKeyword("_EMISSION");
            auxinPtMat.SetColor("_EmissionColor", new Color(0.42f, 0.34f, 0.04f));
        }

        // ---------------------------------------------------------------------
        // Khung nhìn 0: TỔNG THỂ CÂY ĐẬU HAI LÁ MẦM (MẪU VẬT SỐNG CHÂN THỰC + MẶT CẮT GIẢI PHẪU SẠCH)
        // ---------------------------------------------------------------------
        void BuildWholePlant()
        {
            wholeRoot = Group("Cây hai lá mầm (Tổng thể)", transform, Vector3.zero);
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");

            Material rootCapMat = NewMat(urpLit, new Color(0.84f, 0.68f, 0.32f), 0.42f);
            Material rootCutFaceMat = NewTexturedMat(urpLit, new Color(0.88f, 0.79f, 0.58f), cellBrickTex, cellBrickNormalTex, 0.28f, 0.5f);
            Material stemCutInteriorMat = NewTexturedMat(urpLit, new Color(0.66f, 0.80f, 0.48f), cellBrickTex, cellBrickNormalTex, 0.30f, 0.55f);
            Material noduleMat = NewTexturedMat(urpLit, new Color(0.76f, 0.50f, 0.42f), cellBrickTex, cellBrickNormalTex, 0.30f, 0.65f);
            Material hairMat = NewTransparentLit(urpLit, new Color(0.94f, 0.95f, 0.86f, 0.68f));
            Material soilTopMat = NewTexturedMat(urpLit, new Color(0.28f, 0.20f, 0.14f), soilTopTex, soilTopNormalTex, 0.12f, 1.05f);
            Material soilProfileMat = NewTexturedMat(urpLit, Color.white, soilProfileTex, soilTopNormalTex, 0.10f, 0.85f);
            Material potCeramicMat = NewTexturedMat(urpLit, new Color(0.74f, 0.40f, 0.26f), terracottaTex, terracottaNormalTex, 0.28f, 0.55f);
            Material potCutWallMat = NewTexturedMat(urpLit, new Color(0.64f, 0.33f, 0.20f), terracottaTex, terracottaNormalTex, 0.22f, 0.50f);
            Material petalWhiteMat = NewMat(urpLit, new Color(0.98f, 0.96f, 0.91f), 0.44f);
            Material petalRoseMat = NewMat(urpLit, new Color(0.94f, 0.78f, 0.87f), 0.46f);
            Material petalGoldMat = NewMat(urpLit, new Color(0.92f, 0.78f, 0.30f), 0.46f);
            Material podGreenMat = NewTexturedMat(urpLit, new Color(0.27f, 0.57f, 0.20f), stemBarkTex, stemBarkNormalTex, 0.40f, 0.45f);
            Material podInnerMat = NewMat(urpLit, new Color(0.64f, 0.78f, 0.44f), 0.34f);
            Material seedBeanMat = NewMat(urpLit, new Color(0.84f, 0.92f, 0.52f), 0.64f);
            Material lampBodyMat = NewMat(urpLit, new Color(0.16f, 0.18f, 0.22f), 0.48f, 0.62f);

            // 1. Chậu gốm đất nung cắt mở giải phẫu 220° (cạnh sắc gọn, không lem bóng normal)
            Vector2[] potProfile =
            {
                new Vector2(0.000f, -0.352f),
                new Vector2(0.112f, -0.352f),
                new Vector2(0.118f, -0.340f),
                new Vector2(0.152f, -0.032f),
                new Vector2(0.166f, -0.026f),
                new Vector2(0.166f,  0.002f),
                new Vector2(0.148f,  0.002f),
                new Vector2(0.144f, -0.028f),
                new Vector2(0.109f, -0.338f),
                new Vector2(0.000f, -0.338f)
            };
            Mesh potMesh = BuildLatheSectorMesh("ChauGomCatMo", potProfile, -110f, 110f, 40, capEnds: false);
            MeshObject(wholeRoot, "Chậu gốm đất nung cắt mở", potMesh, Vector3.zero, Vector3.zero, Vector3.one, potCeramicMat, hasCol: false);

            // 2 vách cắt đứng phẳng của chậu gốm (normal phẳng riêng biệt nên mặt cắt sắc nét như cưa thực tế)
            Mesh potCutWalls = BuildLatheCutWallMesh("VachCatChauGom", potProfile, -110f, 110f);
            MeshObject(wholeRoot, "Thành cắt chậu gốm", potCutWalls, Vector3.zero, Vector3.zero, Vector3.one, potCutWallMat, hasCol: false);

            // 2. Bầu đất mùn hữu cơ tự nhiên: Mặt đất gồ ghề tự nhiên (0 lỗi quạt tam giác) + địa tầng đất cắt dọc
            Mesh soilTopMound = BuildOrganicSoilSectorMound("MatDatHuuCo", 0.144f, -0.010f, -108f, 108f, 34, 10);
            MeshObject(wholeRoot, "Mặt đất mùn hữu cơ", soilTopMound, Vector3.zero, Vector3.zero, Vector3.one, soilTopMat, hasCol: false);

            Vector2[] soilBodyProf =
            {
                new Vector2(0.000f, -0.336f),
                new Vector2(0.107f, -0.336f),
                new Vector2(0.142f, -0.014f),
                new Vector2(0.000f, -0.014f)
            };
            // Mặt cắt đứng phân tầng đất phía sau hệ rễ (tạo nền địa tầng đất thật sự cho bộ rễ cọc nổi bật)
            Mesh soilProfileWalls = BuildSoilCutawayWalls("DiaTangDatCatDoc", soilBodyProf, -108f, 108f);
            MeshObject(wholeRoot, "Địa tầng đất cắt dọc", soilProfileWalls, Vector3.zero, Vector3.zero, Vector3.one, soilProfileMat, hasCol: false);

            // 3. Hệ rễ cọc (Taproot) uốn hữu cơ tự nhiên + lát cắt dọc lộ trụ mạch dẫn
            var rootGroup = Group("Hệ rễ cọc", wholeRoot, Vector3.zero);
            Vector3[] tapPts =
            {
                new Vector3( 0.000f,  0.004f,  0.000f),
                new Vector3( 0.002f, -0.058f,  0.002f),
                new Vector3(-0.002f, -0.128f,  0.001f),
                new Vector3( 0.002f, -0.198f, -0.001f),
                new Vector3(-0.001f, -0.268f,  0.001f),
                new Vector3(-0.001f, -0.316f,  0.000f)
            };
            float[] tapRadii = { 0.0108f, 0.0090f, 0.0070f, 0.0050f, 0.0031f, 0.0012f };
            Mesh tapMesh = BuildSplineTubeMesh("ReCaiChinh", tapPts, tapRadii, 18);
            var tapObj = MeshObject(rootGroup, "Rễ cái (Trục rễ cọc)", tapMesh, Vector3.zero, Vector3.zero, Vector3.one, rootMat, hasCol: true);
            RegisterPart(tapObj, "re_chop_va_long_hut", new Color(0.70f, 0.55f, 0.35f));
            nonVascularWholeRenderers.Add(tapObj.GetComponent<Renderer>());

            // Vết cắt giải phẫu gọn trên mặt trước rễ cái lộ mô mềm vỏ & mạch gỗ/mạch rây mảnh bên trong
            Vector3[] tapCutPts =
            {
                new Vector3(0.000f, -0.010f, -0.0046f),
                new Vector3(0.001f, -0.095f, -0.0038f),
                new Vector3(0.000f, -0.185f, -0.0026f),
                new Vector3(0.000f, -0.245f, -0.0016f)
            };
            float[] tapCutR = { 0.0052f, 0.0043f, 0.0032f, 0.0018f };
            var tapCutObj = MeshObject(rootGroup, "Mặt cắt dọc rễ cái", BuildSplineTubeMesh("MatCatReCai", tapCutPts, tapCutR, 12),
                Vector3.zero, Vector3.zero, new Vector3(1.0f, 1f, 0.30f), rootCutFaceMat, hasCol: false);
            RegisterPart(tapCutObj, "re_chop_va_long_hut", new Color(0.88f, 0.79f, 0.58f));

            Vector3[] rootXylemPts =
            {
                new Vector3(-0.0016f, -0.248f, -0.0050f),
                new Vector3(-0.0018f, -0.125f, -0.0054f),
                new Vector3(-0.0020f,  0.004f, -0.0042f)
            };
            float[] rootVasR = { 0.0010f, 0.0014f, 0.0018f };
            MeshObject(rootGroup, "Bó mạch gỗ rễ cái", BuildSplineTubeMesh("MachGoRe", rootXylemPts, rootVasR, 10),
                Vector3.zero, Vector3.zero, Vector3.one, xylemFlowMat, hasCol: false);

            Vector3[] rootPhloemPts =
            {
                new Vector3(0.0016f, -0.240f, -0.0050f),
                new Vector3(0.0018f, -0.125f, -0.0054f),
                new Vector3(0.0020f,  0.004f, -0.0042f)
            };
            MeshObject(rootGroup, "Bó mạch rây rễ cái", BuildSplineTubeMesh("MachRayRe", rootPhloemPts, rootVasR, 10),
                Vector3.zero, Vector3.zero, Vector3.one, phloemFlowMat, hasCol: false);

            // Chóp rễ bảo vệ đỉnh sinh trưởng
            Vector3[] capPts =
            {
                new Vector3(-0.001f, -0.308f, 0f),
                new Vector3(-0.001f, -0.320f, 0f),
                new Vector3(-0.001f, -0.330f, 0f)
            };
            var capObj = MeshObject(rootGroup, "Chóp rễ bảo vệ", BuildSplineTubeMesh("ChopRe", capPts, new[] { 0.0023f, 0.0019f, 0.0003f }, 12),
                Vector3.zero, Vector3.zero, Vector3.one, rootCapMat, hasCol: true);
            RegisterPart(capObj, "re_chop_va_long_hut", new Color(0.84f, 0.68f, 0.32f));

            // 12 Rễ bên cấp 1 + 24 Rễ bên cấp 2 uốn lượn sinh động trong tầng đất cắt mở
            for (int r = 0; r < 12; r++)
            {
                float ang = (r * 31f + 12f) * Mathf.Deg2Rad;
                float cosA = Mathf.Cos(ang);
                float sinA = Mathf.Sin(ang) * 0.62f + 0.18f;
                Vector3 dir = new Vector3(cosA, -0.32f - (r % 3) * 0.08f, sinA).normalized;
                float y0 = -0.028f - r * 0.020f;
                float len = Mathf.Lerp(0.118f, 0.048f, r / 11f);
                float r0 = Mathf.Lerp(0.0036f, 0.0016f, r / 11f);

                Vector3 p0 = new Vector3(0f, y0, 0f);
                Vector3 p1 = p0 + dir * (len * 0.35f) + new Vector3(0f, 0.005f, 0f);
                Vector3 p2 = p0 + dir * (len * 0.72f) + new Vector3(0f, -0.011f, 0f);
                Vector3 p3 = p0 + dir * len + new Vector3(0f, -0.026f, 0f);

                Mesh latMesh = BuildSplineTubeMesh($"ReBen1_{r}", new[] { p0, p1, p2, p3 }, new[] { r0, r0 * 0.72f, r0 * 0.42f, 0.00035f }, 10);
                var latObj = MeshObject(rootGroup, $"Rễ bên cấp 1 ({r + 1})", latMesh, Vector3.zero, Vector3.zero, Vector3.one, rootMat, hasCol: true);
                RegisterPart(latObj, "re_chop_va_long_hut", new Color(0.70f, 0.55f, 0.35f));

                for (int s = 0; s < 2; s++)
                {
                    Vector3 sp0 = s == 0 ? p1 : p2;
                    Vector3 sDir = (dir + new Vector3(s == 0 ? 0.28f : -0.26f, -0.52f, 0.16f)).normalized;
                    float sLen = len * (s == 0 ? 0.48f : 0.34f);
                    Vector3 sp1 = sp0 + sDir * (sLen * 0.5f);
                    Vector3 sp2 = sp0 + sDir * sLen;
                    Mesh subMesh = BuildSplineTubeMesh($"ReBen2_{r}_{s}", new[] { sp0, sp1, sp2 }, new[] { r0 * 0.48f, r0 * 0.26f, 0.00022f }, 7);
                    var subObj = MeshObject(rootGroup, $"Rễ bên cấp 2 ({r + 1}.{s + 1})", subMesh, Vector3.zero, Vector3.zero, Vector3.one, rootMat, hasCol: false);
                    RegisterPart(subObj, "re_chop_va_long_hut", new Color(0.70f, 0.55f, 0.35f));
                }

                // Nốt sần cộng sinh Rhizobium ở phần trên bộ rễ
                if (r < 7)
                {
                    Mesh nodMesh = BuildOrganicNoduleMesh($"NotSan_{r}", 0.0052f, r * 1.9f);
                    var nodObj = MeshObject(rootGroup, $"Nốt sần Rhizobium {r + 1}", nodMesh, p0 + dir * (0.011f + (r % 2) * 0.008f), Vector3.zero, Vector3.one, noduleMat, hasCol: true);
                    RegisterPart(nodObj, "re_chop_va_long_hut", new Color(0.76f, 0.50f, 0.42f));
                }
            }

            // Miền lông hút tơ mảnh mịn ở vùng thành thục rễ cái
            Mesh rootHairClusterMesh = BuildRootHairClusterMesh();
            MeshObject(rootGroup, "Miền lông hút rễ cái", rootHairClusterMesh, Vector3.zero, Vector3.zero, Vector3.one, hairMat, hasCol: false);

            RegisterAnchor("cay_re_giua", rootGroup, new Vector3(0.000f, -0.185f, -0.008f));
            AddPin(KhungNhinThucVat.TongThe, "re_chop_va_long_hut", "Hệ rễ cọc & Nốt sần", rootGroup, new Vector3(0.015f, -0.155f, -0.01f), new Color(0.95f, 0.62f, 0.18f));

            // 4. Thân cây đậu 7 đốt tỉ lệ thực tế (dày hơn ở cổ rễ 10.5mm -> thon 3.8mm ở ngọn)
            stemMount = Group("Trục thân cây", wholeRoot, Vector3.zero);
            float segLen = 0.082f;
            Transform parent = stemMount;
            Mesh leafBladeMesh = BuildDetailedDicotLeafletMesh();

            for (int i = 0; i < stemNodes.Length; i++)
            {
                var node = Group($"Đốt thân {i + 1}", parent, i == 0 ? Vector3.zero : new Vector3(0f, segLen, 0f));
                stemNodes[i] = node;
                stemBaseLocalPos[i] = node.localPosition;

                float rBot = Mathf.Lerp(0.0108f, 0.0040f, i / (float)stemNodes.Length);
                float rTop = Mathf.Lerp(0.0108f, 0.0040f, (i + 1) / (float)stemNodes.Length);
                float rNodeSwell = rTop * 1.15f;

                // Đốt 1 & 2 có cửa sổ rạch giải phẫu gọn (mở vỏ 130° mặt trước -Z, lộ lõi mô mềm & mạch dẫn nằm gọn bên trong!)
                bool hasScalpelCutaway = (i == 1 || i == 2);
                if (hasScalpelCutaway)
                {
                    Vector2[] cutShellProf =
                    {
                        new Vector2(rBot * 0.68f, -0.002f),
                        new Vector2(rBot,         -0.002f),
                        new Vector2((rBot + rTop) * 0.5f, segLen * 0.5f),
                        new Vector2(rNodeSwell,   segLen * 0.90f),
                        new Vector2(rTop,         segLen * 1.02f),
                        new Vector2(rTop * 0.68f, segLen * 1.02f)
                    };
                    // Vỏ thân mở mặt trước (-115°..+115° ở phía sau +Z, mở 130° về phía -Z camera)
                    Mesh openBarkMesh = BuildLatheSectorMesh($"VoThanCatMo_{i}", cutShellProf, -115f, 115f, 22, capEnds: false);
                    var barkObj = MeshObject(node, $"Vỏ thân rạch giải phẫu {i + 1}", openBarkMesh, Vector3.zero, Vector3.zero, Vector3.one, stemMat, hasCol: true);
                    RegisterPart(barkObj, "than_cay", new Color(0.28f, 0.54f, 0.20f));
                    nonVascularWholeRenderers.Add(barkObj.GetComponent<Renderer>());

                    // Nền mô ruột xanh sáng bên trong hốc cắt
                    Vector3[] pithPts = { new Vector3(0f, 0f, 0.0012f), new Vector3(0f, segLen, 0.0012f) };
                    MeshObject(node, "Ruột thân trong hốc cắt", BuildSplineTubeMesh($"RuotCat_{i}", pithPts, new[] { rBot * 0.64f, rTop * 0.64f }, 12),
                        Vector3.zero, Vector3.zero, Vector3.one, stemCutInteriorMat, hasCol: false);
                }
                else
                {
                    Vector3[] segPts =
                    {
                        new Vector3(0f, -0.002f, 0f),
                        new Vector3(0f, segLen * 0.36f, 0f),
                        new Vector3(0f, segLen * 0.74f, 0f),
                        new Vector3(0f, segLen * 0.90f, 0f),
                        new Vector3(0f, segLen * 1.02f, 0f)
                    };
                    float[] segRadii = { rBot, (rBot + rTop) * 0.5f, rTop, rNodeSwell, rTop };
                    Mesh segMesh = BuildSplineTubeMesh($"DotThan_{i + 1}", segPts, segRadii, 18);
                    var segObj = MeshObject(node, $"Thân đoạn {i + 1}", segMesh, Vector3.zero, Vector3.zero, Vector3.one, stemMat, hasCol: true);
                    RegisterPart(segObj, "than_cay", new Color(0.28f, 0.54f, 0.20f));
                    nonVascularWholeRenderers.Add(segObj.GetComponent<Renderer>());
                }

                // Ống mạch gỗ (Xylem lam ↑) & mạch rây (Phloem cam ↓) nằm GỌN BÊN TRONG bán kính thân (không bao giờ trồi ra ngoài vỏ!)
                float vOffX = rBot * 0.27f;
                float vOffTopX = rTop * 0.27f;
                float vRadBot = rBot * 0.21f;
                float vRadTop = rTop * 0.21f;
                float vZ = hasScalpelCutaway ? -rBot * 0.22f : 0f;
                Vector3[] xPts = { new Vector3(-vOffX, 0f, vZ), new Vector3(-vOffTopX, segLen, vZ) };
                Vector3[] pPts = { new Vector3( vOffX, 0f, vZ), new Vector3( vOffTopX, segLen, vZ) };
                MeshObject(node, "Dòng mạch gỗ", BuildSplineTubeMesh($"MachGoThan_{i}", xPts, new[] { vRadBot, vRadTop }, 10), Vector3.zero, Vector3.zero, Vector3.one, xylemFlowMat, hasCol: false);
                MeshObject(node, "Dòng mạch rây", BuildSplineTubeMesh($"MachRayThan_{i}", pPts, new[] { vRadBot, vRadTop }, 10), Vector3.zero, Vector3.zero, Vector3.one, phloemFlowMat, hasCol: false);

                if (i == 1)
                {
                    RegisterAnchor("cay_than_cat_duoi", node, new Vector3(0.000f, 0.008f, -0.010f));
                    AddPin(KhungNhinThucVat.TongThe, "than_cay", "Cửa sổ giải phẫu thân (Gỗ ↑ · Rây ↓)", node, new Vector3(0.008f, segLen * 0.8f, -0.008f), new Color(0.22f, 0.74f, 0.98f));
                }
                if (i == 3)
                {
                    RegisterAnchor("cay_than_dot3", node, new Vector3(0.000f, 0.025f, -0.008f));
                    RegisterAnchor("cay_than3_toi", node, new Vector3(-0.012f, segLen * 0.48f, 0f));
                }
                if (i == 4)
                {
                    RegisterAnchor("cay_than_dot4", node, new Vector3(0.000f, 0.032f, -0.007f));
                    RegisterAnchor("cay_than4_toi", node, new Vector3(-0.011f, segLen * 0.48f, 0f));
                }
                if (i == 5)
                    RegisterAnchor("cay_than5_toi", node, new Vector3(-0.010f, segLen * 0.48f, 0f));
                if (i == 6)
                {
                    RegisterAnchor("cay_ngon_sang", node, new Vector3( 0.010f, segLen * 0.82f, 0f));
                    RegisterAnchor("cay_ngon_toi",  node, new Vector3(-0.010f, segLen * 0.82f, 0f));
                }

                // Đốt 0: Cặp 2 lá đơn sơ cấp hình tim đối xứng (Primary opposite unifoliate leaves)
                if (i == 0)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        var priPivot = Group($"Lá đơn sơ cấp {(side > 0 ? "A" : "B")}", node, new Vector3(0f, segLen * 0.88f, 0f));
                        priPivot.localRotation = Quaternion.Euler(0f, side > 0 ? 28f : 208f, 0f);
                        Vector3[] priPet =
                        {
                            new Vector3(0.003f, 0.000f, 0f),
                            new Vector3(0.020f, 0.010f, 0f),
                            new Vector3(0.042f, 0.012f, 0f)
                        };
                        var priPetObj = MeshObject(priPivot, "Cuống lá sơ cấp", BuildSplineTubeMesh($"CuongSoCap_{side}", priPet, new[] { 0.0028f, 0.0020f, 0.0014f }, 10),
                            Vector3.zero, Vector3.zero, Vector3.one, stemMat, hasCol: false);
                        RegisterPart(priPetObj, "la_cay", new Color(0.28f, 0.54f, 0.20f));

                        BuildRealisticLeaflet(priPivot, $"Lá đơn sơ cấp {(side > 0 ? "A" : "B")}", leafBladeMesh,
                            new Vector3(0.040f, 0.012f, 0f), new Vector3(5f, 0f, -12f), 0.94f);
                    }
                }

                // Đốt 1..5: Lá kép 3 lá chét (Trifoliate compound leaf) mọc xoắn ốc tự nhiên (137.5°)
                if (i >= 1 && i <= 5)
                {
                    float goldenYaw = (i * 137.5f + 22f) % 360f;
                    float leafScale = Mathf.Lerp(1.10f, 0.76f, (i - 1) / 4f);

                    var petiolePivot = Group($"Cụm lá kép đốt {i}", node, new Vector3(0f, segLen * 0.88f, 0f));
                    petiolePivot.localRotation = Quaternion.Euler(0f, goldenYaw, 0f);

                    // Cuống lá cong tự nhiên có gối lá (Pulvinus) phình nhẹ ở gốc
                    Vector3[] petPts =
                    {
                        new Vector3(0.002f, 0.000f, 0f),
                        new Vector3(0.012f, 0.008f, 0f),
                        new Vector3(0.032f, 0.018f, 0f),
                        new Vector3(0.056f, 0.022f, 0f),
                        new Vector3(0.080f, 0.019f, 0f)
                    };
                    float[] petRadii = { 0.0036f, 0.0025f, 0.0020f, 0.0017f, 0.0012f };
                    var petObj = MeshObject(petiolePivot, "Cuống lá kép", BuildSplineTubeMesh($"CuongLa_{i}", petPts, petRadii, 12),
                        Vector3.zero, Vector3.zero, Vector3.one, stemMat, hasCol: false);
                    RegisterPart(petObj, "la_cay", new Color(0.28f, 0.54f, 0.20f));

                    // Chồi nách nhỏ gọn ôm sát nách lá (tuyệt đối không mọc lá thẳng đứng dính dọc thân!)
                    Vector3[] budPts = { new Vector3(0.003f, 0.002f, 0f), new Vector3(0.007f, 0.008f, 0f), new Vector3(0.009f, 0.013f, 0f) };
                    MeshObject(petiolePivot, "Chồi nách", BuildSplineTubeMesh($"ChoiNach_{i}", budPts, new[] { 0.0022f, 0.0016f, 0.0004f }, 8),
                        Vector3.zero, Vector3.zero, Vector3.one, veinMat, hasCol: false);

                    // 1) Lá chét đỉnh (Terminal leaflet)
                    BuildRealisticLeaflet(petiolePivot, $"Lá chét đỉnh {i}", leafBladeMesh,
                        new Vector3(0.078f, 0.019f, 0f), new Vector3(4f, 0f, -14f - i * 1.4f), leafScale);

                    // 2) Cặp lá chét bên (Lateral leaflets) có cuống phụ ngắn
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector3[] petiolulePts =
                        {
                            new Vector3(0.050f, 0.021f, 0f),
                            new Vector3(0.056f, 0.020f, side * 0.012f),
                            new Vector3(0.061f, 0.018f, side * 0.023f)
                        };
                        MeshObject(petiolePivot, "Cuống lá chét bên", BuildSplineTubeMesh($"CuongPhu_{i}_{side}", petiolulePts, new[] { 0.0016f, 0.0013f, 0.0010f }, 8),
                            Vector3.zero, Vector3.zero, Vector3.one, stemMat, hasCol: false);

                        BuildRealisticLeaflet(petiolePivot, $"Lá chét bên {(side > 0 ? "A" : "B")} {i}", leafBladeMesh,
                            new Vector3(0.059f, 0.018f, side * 0.022f),
                            new Vector3(side * 9f, -side * 64f, -11f), leafScale * 0.84f);
                    }

                    if (i == 3)
                    {
                        RegisterAnchor("cay_la3_phien", petiolePivot, new Vector3(0.115f, 0.018f, 0f));
                        AddPin(KhungNhinThucVat.TongThe, "la_cay", "Lá kép 3 lá chét (Gân mạng)", petiolePivot, new Vector3(0.115f, 0.016f, 0f), new Color(0.16f, 0.82f, 0.48f));
                    }

                    // Đốt 4: Cặp quả đậu non (1 quả nguyên vẹn + 1 quả rạch mở lộ 4 hạt đậu bóng)
                    if (i == 4)
                    {
                        RegisterAnchor("cay_la4_phien", petiolePivot, new Vector3(0.105f, 0.016f, 0f));
                        RegisterAnchor("cay_qua_dau", petiolePivot, new Vector3(0.024f, -0.045f, -0.014f));
                        BuildCutawayBeanPod(petiolePivot, new Vector3(0.020f, -0.002f, -0.014f), podGreenMat, podInnerMat, seedBeanMat, cutaway: true);
                        BuildCutawayBeanPod(petiolePivot, new Vector3(0.028f, -0.004f,  0.016f), podGreenMat, podInnerMat, seedBeanMat, cutaway: false);
                    }
                }

                parent = node;
            }

            // 5. Chùm hoa đậu hình bướm mẫu 5 ở đỉnh thân (2 bông nở + 2 nụ hoa)
            var flowerApex = Group("Cụm hoa đỉnh thân", parent, new Vector3(0f, segLen * 0.96f, 0f));
            BuildPapilionaceousFlower(flowerApex, Vector3.zero, Vector3.zero, 1.0f, stemMat, petalWhiteMat, petalRoseMat, petalGoldMat);
            BuildPapilionaceousFlower(flowerApex, new Vector3(-0.014f, -0.010f, 0.008f), new Vector3(12f, -65f, 10f), 0.78f, stemMat, petalWhiteMat, petalRoseMat, petalGoldMat);
            RegisterAnchor("cay_hoa_dinh", flowerApex, new Vector3(0.004f, 0.018f, 0f));
            AddPin(KhungNhinThucVat.TongThe, "hoa_dau", "Hoa đậu mẫu 5 & Quả đậu non", flowerApex, new Vector3(0.012f, 0.022f, 0f), new Color(0.95f, 0.45f, 0.68f));

            // 6. Cụm đèn chiếu hướng sáng tinh tế với luồng sáng mờ dần mềm mại (không còn hình nón cứng)
            lampPivot = Group("Cụm đèn hướng sáng", wholeRoot, new Vector3(0f, 0.24f, 0f));
            Vector2[] shadeProfile =
            {
                new Vector2(0.000f,  0.024f),
                new Vector2(0.014f,  0.024f),
                new Vector2(0.018f,  0.008f),
                new Vector2(0.036f, -0.020f),
                new Vector2(0.032f, -0.020f),
                new Vector2(0.014f,  0.006f),
                new Vector2(0.000f,  0.006f)
            };
            Mesh shadeMesh = BuildLatheSectorMesh("ChupDen", shadeProfile, -180f, 180f, 24, capEnds: false);
            lampBulbObj = MeshObject(lampPivot, "Chụp đèn", shadeMesh, new Vector3(0f, 0.56f, 0f), Vector3.zero, Vector3.one, lampBodyMat, hasCol: false).transform;
            RegisterAnchor("cay_den_bong", lampBulbObj, new Vector3(0f, -0.018f, 0f));
            Part(PrimitiveType.Sphere, lampBulbObj, "Bóng đèn", new Vector3(0f, -0.008f, 0f), new Vector3(0.024f, 0.024f, 0.024f), Vector3.zero, bulbGlowMat, hasCol: false);

            Vector2[] coneProfile =
            {
                new Vector2(0.145f, -0.16f),
                new Vector2(0.030f,  0.16f)
            };
            Mesh coneMesh = BuildLatheSectorMesh("LuongSang", coneProfile, -180f, 180f, 28, capEnds: false);
            lampConeObj = MeshObject(lampPivot, "Luồng sáng", coneMesh, new Vector3(0f, 0.37f, 0f), Vector3.zero, Vector3.one, lampBeamMat, hasCol: false).transform;
        }

        void BuildRealisticLeaflet(Transform parent, string name, Mesh leafMesh, Vector3 localPos, Vector3 localEuler, float scale)
        {
            var bladeGo = MeshObject(parent, name, leafMesh, localPos, localEuler, Vector3.one * scale, leafMat, hasCol: false);
            var bc = bladeGo.AddComponent<BoxCollider>();
            bc.center = new Vector3(0.072f, -0.004f, 0f);
            bc.size = new Vector3(0.144f, 0.020f, 0.112f);
            RegisterPart(bladeGo, "la_cay", new Color(0.22f, 0.56f, 0.22f));
        }

        void BuildCutawayBeanPod(Transform parent, Vector3 localPos, Material podMat, Material innerMat, Material seedMat, bool cutaway)
        {
            var podGrp = Group(cutaway ? "Quả đậu non cắt mở" : "Quả đậu non nguyên vẹn", parent, localPos);
            podGrp.localRotation = Quaternion.Euler(cutaway ? 8f : -6f, cutaway ? -16f : 28f, cutaway ? -8f : 12f);

            Vector3[] podPts =
            {
                new Vector3(0.000f,  0.000f, 0.000f),
                new Vector3(0.006f, -0.022f, 0.002f),
                new Vector3(0.011f, -0.048f, 0.003f),
                new Vector3(0.009f, -0.075f, 0.002f),
                new Vector3(0.002f, -0.095f, 0.000f)
            };
            float[] podR = { 0.0020f, 0.0072f, 0.0084f, 0.0070f, 0.0009f };
            Mesh podMesh = BuildSplineTubeMesh(cutaway ? "VoQuaDauMo" : "VoQuaDauNguyen", podPts, podR, 16);
            var podShell = MeshObject(podGrp, "Vỏ quả đậu", podMesh, Vector3.zero, Vector3.zero, new Vector3(1.0f, 1.0f, cutaway ? 0.54f : 0.72f), podMat, hasCol: true);
            RegisterPart(podShell, "hoa_dau", new Color(0.27f, 0.57f, 0.20f));

            if (cutaway)
            {
                Mesh beanSeedMesh = BuildKidneySeedMesh();
                for (int s = 0; s < 4; s++)
                {
                    float sy = -0.021f - s * 0.017f;
                    float sx = 0.007f + Mathf.Sin((s + 0.5f) * 0.7f) * 0.002f;
                    var seed = MeshObject(podGrp, $"Hạt đậu non {s + 1}", beanSeedMesh,
                        new Vector3(sx, sy, -0.0028f), new Vector3(0f, 12f, 8f), Vector3.one, seedMat, hasCol: true);
                    RegisterPart(seed, "hoa_dau", new Color(0.84f, 0.92f, 0.52f));
                }
            }
        }

        void BuildPapilionaceousFlower(Transform parent, Vector3 localPos, Vector3 localEuler, float scale,
                                       Material calyxMat, Material bannerMat, Material wingMat, Material keelMat)
        {
            var flower = Group("Hoa cây đậu mẫu 5", parent, localPos);
            flower.localEulerAngles = localEuler;
            flower.localScale = Vector3.one * scale;

            Vector2[] calyxProfile =
            {
                new Vector2(0.0012f, -0.012f),
                new Vector2(0.0018f,  0.000f),
                new Vector2(0.0056f,  0.006f),
                new Vector2(0.0072f,  0.015f),
                new Vector2(0.0058f,  0.015f),
                new Vector2(0.0040f,  0.005f),
                new Vector2(0.0000f,  0.002f)
            };
            var calyxObj = MeshObject(flower, "Đài hoa 5 răng", BuildLatheSectorMesh("DaiHoa", calyxProfile, -180f, 180f, 18, capEnds: false),
                Vector3.zero, new Vector3(16f, 0f, 0f), Vector3.one, calyxMat, hasCol: true);
            RegisterPart(calyxObj, "hoa_dau", new Color(0.28f, 0.54f, 0.20f));

            Mesh bannerMesh = BuildCurvedPetalMesh("CanhCo", 0.036f, 0.033f, 0.011f, -0.006f);
            var bannerObj = MeshObject(flower, "Cánh cờ (Standard)", bannerMesh,
                new Vector3(0f, 0.012f, 0.002f), new Vector3(-28f, 0f, 0f), Vector3.one, bannerMat, hasCol: true);
            RegisterPart(bannerObj, "hoa_dau", new Color(0.98f, 0.96f, 0.91f));

            Mesh wingMesh = BuildCurvedPetalMesh("CanhBen", 0.018f, 0.026f, 0.006f, 0.004f);
            for (int w = -1; w <= 1; w += 2)
            {
                var wingObj = MeshObject(flower, $"Cánh bên {(w < 0 ? "Trái" : "Phải")}", wingMesh,
                    new Vector3(w * 0.007f, 0.010f, -0.003f), new Vector3(18f, w * 32f, -w * 14f), Vector3.one, wingMat, hasCol: true);
                RegisterPart(wingObj, "hoa_dau", new Color(0.94f, 0.78f, 0.87f));
            }

            Vector3[] keelPts =
            {
                new Vector3(0f, 0.008f,  0.002f),
                new Vector3(0f, 0.011f, -0.010f),
                new Vector3(0f, 0.017f, -0.018f),
                new Vector3(0f, 0.023f, -0.016f)
            };
            var keelObj = MeshObject(flower, "Cánh thìa & Bầu nhụy", BuildSplineTubeMesh("CanhThia", keelPts, new[] { 0.0034f, 0.0054f, 0.0044f, 0.0010f }, 14),
                Vector3.zero, Vector3.zero, new Vector3(0.75f, 1.0f, 1.0f), keelMat, hasCol: true);
            RegisterPart(keelObj, "hoa_dau", new Color(0.92f, 0.78f, 0.30f));
        }

        // ---------------------------------------------------------------------
        // Lát cắt 1: LÁT CẮT RỄ HAI LÁ MẦM — MẪU VẬT TRỤ MÔ HỌC 3D CẮT BẬC THANG LIỀN KHỐI
        // ---------------------------------------------------------------------
        void BuildRootSlice()
        {
            rootSliceRoot = Group("Lát cắt Rễ", transform, Vector3.zero);
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");

            Material rootHistMat = NewTexturedMat(urpLit, Color.white, rootHistTex, rootHistNormalTex, 0.28f, 0.90f);
            Material epiMat = NewTexturedMat(urpLit, new Color(0.60f, 0.44f, 0.27f), cellBrickTex, cellBrickNormalTex, 0.28f, 0.75f);
            Material hairWallMat = NewTransparentLit(urpLit, new Color(0.88f, 0.78f, 0.58f, 0.76f));
            Material cortexWallMat = NewTexturedMat(urpLit, new Color(0.78f, 0.65f, 0.44f), cellBrickTex, cellBrickNormalTex, 0.26f, 0.80f);
            Material endoCellMat = NewTexturedMat(urpLit, new Color(0.70f, 0.52f, 0.26f), cellBrickTex, cellBrickNormalTex, 0.32f, 0.70f);
            Material pericycleMat = NewTexturedMat(urpLit, new Color(0.42f, 0.62f, 0.30f), cellBrickTex, cellBrickNormalTex, 0.28f, 0.65f);
            Material lumenMat = NewMat(urpLit, new Color(0.02f, 0.05f, 0.09f), 0.10f);
            Material vesselWallMat = NewMat(urpLit, new Color(0.28f, 0.68f, 0.92f), 0.48f);
            Material spiralRingMat = NewMat(urpLit, new Color(0.68f, 0.95f, 1.0f), 0.58f);
            Material sievePlateMat = NewMat(urpLit, new Color(0.86f, 0.64f, 0.24f), 0.38f);
            Material apoPathMat = NewMat(urpLit, new Color(0.16f, 0.80f, 1.0f), 0.62f);
            apoPathMat.EnableKeyword("_EMISSION"); apoPathMat.SetColor("_EmissionColor", new Color(0.06f, 0.30f, 0.50f));
            Material symPathMat = NewMat(urpLit, new Color(1.0f, 0.58f, 0.14f), 0.62f);
            symPathMat.EnableKeyword("_EMISSION"); symPathMat.SetColor("_EmissionColor", new Color(0.42f, 0.18f, 0.02f));

            const float H = 0.135f;      // Chiều cao toàn mẫu vật rễ
            const float HStep = 0.068f;  // Chiều cao bậc cắt phía trước lộ thiết diện dọc

            // Tầng 1: Biểu bì rễ (Rhizodermis) liền khối & 16 Tế bào lông hút mảnh tự nhiên mọc từ thành biểu bì
            var grpEpi = Group("Tầng Biểu bì & Lông hút", rootSliceRoot, Vector3.zero);
            RegisterExplode(grpEpi, new Vector3(0f, -0.055f, 0f));

            Vector2[] epiBackProf = { new Vector2(0.194f, 0f), new Vector2(0.212f, 0f), new Vector2(0.214f, H * 0.5f), new Vector2(0.212f, H), new Vector2(0.194f, H) };
            var epiBack = MeshObject(grpEpi, "Vỏ biểu bì rễ (270°)",
                BuildLatheSectorMesh("BieuBiReSau", epiBackProf, -135f, 135f, 44, capEnds: false),
                Vector3.zero, Vector3.zero, Vector3.one, epiMat, hasCol: true);
            RegisterPart(epiBack, "re_long_hut", new Color(0.60f, 0.44f, 0.27f));

            Vector2[] epiFrontProf = { new Vector2(0.194f, 0f), new Vector2(0.212f, 0f), new Vector2(0.212f, HStep), new Vector2(0.194f, HStep) };
            var epiFront = MeshObject(grpEpi, "Vỏ biểu bì rễ (Bậc trước)",
                BuildLatheSectorMesh("BieuBiReTruoc", epiFrontProf, 135f, 225f, 18, capEnds: false),
                Vector3.zero, Vector3.zero, Vector3.one, epiMat, hasCol: true);
            RegisterPart(epiFront, "re_long_hut", new Color(0.60f, 0.44f, 0.27f));

            // 16 Tế bào lông hút tơ mảnh tự nhiên uốn nhẹ (kết hợp thành các ống đơn bào thành mỏng trong mờ)
            for (int i = 0; i < 16; i++)
            {
                float deg = i * (360f / 16f) + 11f;
                float a = deg * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                Vector3 perp = new Vector3(Mathf.Cos(a), 0f, -Mathf.Sin(a));
                bool inFrontSector = deg > 132f && deg < 228f;
                float yBase = inFrontSector ? (0.022f + (i % 2) * 0.026f) : (0.030f + (i % 3) * 0.036f);
                float len = 0.095f + (i % 3) * 0.022f;
                float wave = (i % 2 == 0 ? 1f : -1f) * 0.008f;

                Vector3[] hPts =
                {
                    dir * 0.206f + new Vector3(0f, yBase, 0f),
                    dir * (0.206f + len * 0.36f) + perp * (wave * 0.5f) + new Vector3(0f, yBase - 0.002f, 0f),
                    dir * (0.206f + len * 0.72f) + perp * wave + new Vector3(0f, yBase - 0.006f, 0f),
                    dir * (0.206f + len) + perp * (wave * 0.7f) + new Vector3(0f, yBase - 0.010f, 0f)
                };
                float[] hRad = { 0.0068f, 0.0046f, 0.0038f, 0.0022f };
                var hairObj = MeshObject(grpEpi, $"Tế bào lông hút {i + 1}",
                    BuildSplineTubeMesh($"LongHutRe_{i}", hPts, hRad, 10),
                    Vector3.zero, Vector3.zero, Vector3.one, hairWallMat, hasCol: true);
                RegisterPart(hairObj, "re_long_hut", new Color(0.84f, 0.74f, 0.54f));
            }
            RegisterAnchor("re_dat_trai", grpEpi, new Vector3(-0.155f, 0.044f, -0.258f));
            RegisterAnchor("re_dat_phai", grpEpi, new Vector3( 0.155f, 0.044f, -0.258f));
            RegisterAnchor("re_long_hut_trai", grpEpi, new Vector3(-0.095f, 0.056f, -0.192f));
            RegisterAnchor("re_long_hut_phai", grpEpi, new Vector3( 0.095f, 0.056f, -0.192f));
            AddPin(KhungNhinThucVat.LatCatRe, "re_long_hut", "Biểu bì & Tế bào lông hút", grpEpi, new Vector3(0.245f, 0.065f, -0.075f), new Color(0.95f, 0.74f, 0.36f));

            // Tầng 2: Khối Mô mềm vỏ rễ (Cortex Parenchyma) tách rõ mặt cắt ngang vi thể 384x384 & thành bên dọc tế bào
            var grpCortex = Group("Tầng Mô mềm vỏ", rootSliceRoot, Vector3.zero);
            RegisterExplode(grpCortex, new Vector3(0f, -0.015f, 0f));

            // Mặt cắt ngang đỉnh (270° chính) + mặt cắt ngang thềm trước (90° bậc thấp) phủ bản đồ tế bào nhu mô 384x384
            var cortexTop270 = MeshObject(grpCortex, "Mô mềm vỏ rễ (Mặt cắt ngang)",
                BuildPlanarRingSectorMesh("MoMemVoTop270", 0.097f, 0.195f, H, -135f, 135f, 48, 0.212f),
                Vector3.zero, Vector3.zero, Vector3.one, rootHistMat, hasCol: true);
            RegisterPart(cortexTop270, "re_vo_nhu_mo", new Color(0.78f, 0.65f, 0.44f));

            var cortexTopFront = MeshObject(grpCortex, "Thềm cắt dọc mô mềm vỏ",
                BuildPlanarRingSectorMesh("MoMemVoTopFront", 0.097f, 0.195f, HStep, 135f, 225f, 20, 0.212f),
                Vector3.zero, Vector3.zero, Vector3.one, rootHistMat, hasCol: true);
            RegisterPart(cortexTopFront, "re_vo_nhu_mo", new Color(0.78f, 0.65f, 0.44f));

            // Thành trụ trong/ngoài và 2 vách cắt đứng phẳng xuyên tâm tại ±135° phủ vân cột tế bào dọc (không lặp bản đồ tròn lên thành đứng!)
            Vector2[] cortexMainProf = { new Vector2(0.097f, 0.001f), new Vector2(0.195f, 0.001f), new Vector2(0.195f, H), new Vector2(0.097f, H) };
            MeshObject(grpCortex, "Thành khối mô mềm vỏ",
                BuildLatheSectorMesh("MoMemVoThanh", new[] { new Vector2(0.195f, 0.001f), new Vector2(0.195f, H) }, -135f, 135f, 48, capEnds: false),
                Vector3.zero, Vector3.zero, Vector3.one, cortexWallMat, hasCol: false);
            Mesh cortexRadWalls = BuildLatheCutWallMesh("VachCatMoMemVo", cortexMainProf, -135f, 135f);
            var cortexWallsObj = MeshObject(grpCortex, "Mặt cắt dọc mô mềm vỏ", cortexRadWalls, Vector3.zero, Vector3.zero, Vector3.one, cortexWallMat, hasCol: false);
            RegisterPart(cortexWallsObj, "re_vo_nhu_mo", new Color(0.78f, 0.65f, 0.44f));

            // 2 dải dòng dẫn truyền tinh tế trên thềm cắt trước: Gian bào (Apoplast dừng tại Đai Caspari) & Tế bào chất (Symplast xuyên vào mạch gỗ)
            Vector3[] apoPts =
            {
                new Vector3(-0.046f, HStep + 0.003f, -0.192f),
                new Vector3(-0.028f, HStep + 0.004f, -0.164f),
                new Vector3(-0.040f, HStep + 0.004f, -0.138f),
                new Vector3(-0.024f, HStep + 0.005f, -0.116f),
                new Vector3(-0.030f, HStep + 0.005f, -0.098f)
            };
            MeshObject(grpCortex, "Đường gian bào (Apoplast)",
                BuildSplineTubeMesh("DuongGianBao", apoPts, new[] { 0.0028f, 0.0028f, 0.0028f, 0.0028f, 0.0032f }, 10),
                Vector3.zero, Vector3.zero, Vector3.one, apoPathMat, hasCol: false);

            Vector3[] symPts =
            {
                new Vector3(0.042f, HStep + 0.003f, -0.192f),
                new Vector3(0.034f, HStep + 0.005f, -0.150f),
                new Vector3(0.024f, HStep + 0.010f, -0.106f),
                new Vector3(0.014f, HStep + 0.032f, -0.068f),
                new Vector3(0.005f, HStep + 0.056f, -0.028f)
            };
            MeshObject(grpCortex, "Đường tế bào chất (Symplast)",
                BuildSplineTubeMesh("DuongTeBaoChat", symPts, new[] { 0.0028f, 0.0028f, 0.0028f, 0.0028f, 0.0026f }, 10),
                Vector3.zero, Vector3.zero, Vector3.one, symPathMat, hasCol: false);

            RegisterAnchor("re_bieu_bi_apo", grpCortex, new Vector3(-0.046f, HStep + 0.007f, -0.190f));
            RegisterAnchor("re_bieu_bi_sym", grpCortex, new Vector3( 0.042f, HStep + 0.007f, -0.190f));
            AddPin(KhungNhinThucVat.LatCatRe, "re_vo_nhu_mo", "Mô mềm vỏ (Gian bào & Tế bào chất)", grpCortex, new Vector3(-0.145f, H * 0.96f, -0.055f), new Color(0.86f, 0.72f, 0.42f));

            // Tầng 3: Nội bì & ĐAI CASPARI (Suberin) cắt bậc thang đồng bộ với mặt cắt (hạ bậc ở mặt trước để thấy rõ dòng thấm!)
            var grpEndo = Group("Tầng Nội bì & Đai Caspari", rootSliceRoot, Vector3.zero);
            RegisterExplode(grpEndo, new Vector3(0f, 0.040f, 0f));

            Vector2[] endoBackProf = { new Vector2(0.085f, 0.002f), new Vector2(0.098f, 0.002f), new Vector2(0.098f, H + 0.003f), new Vector2(0.085f, H + 0.003f) };
            var endoRing = MeshObject(grpEndo, "Vòng tế bào nội bì",
                BuildLatheSectorMesh("VongNoiBi", endoBackProf, -135f, 135f, 44, capEnds: true),
                Vector3.zero, Vector3.zero, Vector3.one, endoCellMat, hasCol: true);
            RegisterPart(endoRing, "re_dai_caspari", new Color(0.70f, 0.52f, 0.26f));

            Vector2[] endoFrontProf = { new Vector2(0.085f, 0.002f), new Vector2(0.098f, 0.002f), new Vector2(0.098f, HStep + 0.010f), new Vector2(0.085f, HStep + 0.010f) };
            var endoFront = MeshObject(grpEndo, "Nội bì bậc cắt trước",
                BuildLatheSectorMesh("VongNoiBiTruoc", endoFrontProf, 135f, 225f, 16, capEnds: true),
                Vector3.zero, Vector3.zero, Vector3.one, endoCellMat, hasCol: true);
            RegisterPart(endoFront, "re_dai_caspari", new Color(0.70f, 0.52f, 0.26f));

            // Dải Đai Caspari bằng suberin đỏ sẫm ôm khít mặt trên vòng nội bì (270° trên + 90° bậc dưới — KHÔNG dựng vách cao chắn tầm nhìn!)
            Vector2[] caspariBackProf = { new Vector2(0.089f, H + 0.002f), new Vector2(0.0945f, H + 0.002f), new Vector2(0.0945f, H + 0.007f), new Vector2(0.089f, H + 0.007f) };
            var caspariBack = MeshObject(grpEndo, "Đai Caspari (Suberin trên)",
                BuildLatheSectorMesh("DaiCaspariSau", caspariBackProf, -135f, 135f, 48, capEnds: true),
                Vector3.zero, Vector3.zero, Vector3.one, caspariMat, hasCol: true);
            RegisterPart(caspariBack, "re_dai_caspari", new Color(0.68f, 0.14f, 0.10f));

            Vector2[] caspariFrontProf = { new Vector2(0.089f, HStep + 0.002f), new Vector2(0.0945f, HStep + 0.002f), new Vector2(0.0945f, HStep + 0.014f), new Vector2(0.089f, HStep + 0.014f) };
            var caspariFront = MeshObject(grpEndo, "Đai Caspari (Suberin mặt cắt trước)",
                BuildLatheSectorMesh("DaiCaspariTruoc", caspariFrontProf, 135f, 225f, 18, capEnds: true),
                Vector3.zero, Vector3.zero, Vector3.one, caspariMat, hasCol: true);
            RegisterPart(caspariFront, "re_dai_caspari", new Color(0.68f, 0.14f, 0.10f));

            // Dải đai Caspari dọc trên 2 mép vách cắt xuyên tâm ±135°
            Vector2[] caspariVertProf = { new Vector2(0.089f, HStep + 0.010f), new Vector2(0.0945f, HStep + 0.010f), new Vector2(0.0945f, H + 0.006f), new Vector2(0.089f, H + 0.006f) };
            MeshObject(grpEndo, "Dải suberin vách dọc", BuildLatheCutWallMesh("DaiCaspariDoc", caspariVertProf, -135f, 135f),
                Vector3.zero, Vector3.zero, Vector3.one, caspariMat, hasCol: false);

            RegisterAnchor("re_caspari_apo", grpEndo, new Vector3(-0.028f, HStep + 0.017f, -0.090f));
            RegisterAnchor("re_caspari_sym", grpEndo, new Vector3( 0.024f, HStep + 0.017f, -0.090f));
            AddPin(KhungNhinThucVat.LatCatRe, "re_dai_caspari", "Nội bì & Đai Caspari (Suberin)", grpEndo, new Vector3(-0.088f, H + 0.010f, 0.030f), new Color(0.94f, 0.27f, 0.27f));

            // Tầng 4: Trung trụ (Stele) — Trụ bì + Mạch gỗ hình sao 4 cánh nằm PHẲNG/NỔI NHẸ trên mặt cắt + 4 bó Mạch rây xen kẽ
            var grpStele = Group("Tầng Trung trụ (Mạch gỗ & Mạch rây)", rootSliceRoot, Vector3.zero);
            RegisterExplode(grpStele, new Vector3(0f, 0.090f, 0f));

            var periTop = MeshObject(grpStele, "Trụ bì & Mô mềm trung trụ",
                BuildPlanarRingSectorMesh("TruBiReTop", 0.000f, 0.086f, H + 0.002f, -180f, 180f, 44, 0.212f),
                Vector3.zero, Vector3.zero, Vector3.one, pericycleMat, hasCol: false);
            MeshObject(grpStele, "Thành trụ bì",
                BuildLatheSectorMesh("TruBiReWall", new[] { new Vector2(0.085f, 0.002f), new Vector2(0.085f, H + 0.002f) }, -180f, 180f, 44, capEnds: false),
                Vector3.zero, Vector3.zero, Vector3.one, pericycleMat, hasCol: false);

            // Trụ mạch gỗ hình sao 4 cánh nằm gọn ngay mặt cắt ngang (nổi nhẹ 5mm, không nhô thành khối thô)
            Mesh starXylemMesh = BuildFlutedStarPrismMesh("MachGoHinhSao", 4, 0.072f, 0.022f, 0.004f, H + 0.006f);
            var starXylemObj = MeshObject(grpStele, "Trụ mạch gỗ hình sao 4 cánh", starXylemMesh,
                Vector3.zero, Vector3.zero, Vector3.one, xylemFlowMat, hasCol: true);
            RegisterPart(starXylemObj, "re_mach_go", new Color(0.10f, 0.52f, 0.94f));

            // Các lỗ ống mạch gỗ rỗng lòng thật sự (Metaxylem lớn ở tâm, Protoxylem nhỏ dần ra 4 mũi sao) — nổi micro-relief 4..10mm
            Vector2[] vesselOffsets =
            {
                Vector2.zero,
                new Vector2(0.026f, 0f), new Vector2(-0.026f, 0f), new Vector2(0f, 0.026f), new Vector2(0f, -0.026f),
                new Vector2(0.050f, 0f), new Vector2(-0.050f, 0f), new Vector2(0f, 0.050f), new Vector2(0f, -0.050f)
            };
            for (int v = 0; v < vesselOffsets.Length; v++)
            {
                bool isCenter = v == 0;
                bool isMid = v >= 1 && v <= 4;
                float rOuter = isCenter ? 0.0145f : (isMid ? 0.0108f : 0.0072f);
                float rInner = rOuter * 0.68f;
                float topY = H + (isCenter ? 0.012f : (isMid ? 0.0095f : 0.0075f));

                Mesh tubeMesh = BuildHollowVesselMesh($"OngMachGoRe_{v}", rInner, rOuter, 0.010f, topY, 20);
                var vessel = MeshObject(grpStele, $"Ống mạch gỗ {v + 1}", tubeMesh,
                    new Vector3(vesselOffsets[v].x, 0f, vesselOffsets[v].y), Vector3.zero, Vector3.one, vesselWallMat, hasCol: true);
                RegisterPart(vessel, "re_mach_go", new Color(0.10f, 0.52f, 0.94f));

                Part(PrimitiveType.Cylinder, grpStele, "Lòng mạch tối",
                    new Vector3(vesselOffsets[v].x, topY - 0.006f, vesselOffsets[v].y),
                    new Vector3(rInner * 1.92f, 0.002f, rInner * 1.92f), Vector3.zero, lumenMat, hasCol: false);

                // Ở ống mạch gỗ cánh trước (z < 0): hiện lò xo vân xoắn lignin bên trong lòng mạch
                if (v == 4)
                {
                    Mesh helix = BuildHelixSpiralMesh("VanXoanRe", rInner * 0.88f, HStep + 0.010f, topY - 0.002f, 3.5f, 0.0009f);
                    MeshObject(grpStele, "Vân xoắn lignin mạch gỗ", helix,
                        new Vector3(vesselOffsets[v].x, 0f, vesselOffsets[v].y), Vector3.zero, Vector3.one, spiralRingMat, hasCol: false);
                }
            }
            RegisterAnchor("re_xylem_tam", grpStele, new Vector3(0.000f, H + 0.014f, 0.000f));
            RegisterAnchor("re_xylem_dinh", grpStele, new Vector3(0.000f, H + 0.088f, 0.000f));
            AddPin(KhungNhinThucVat.LatCatRe, "re_mach_go", "Mạch gỗ hình sao (Lòng rỗng ↑)", grpStele, new Vector3(0.0f, H + 0.016f, 0.0f), new Color(0.22f, 0.74f, 0.98f));

            // 4 Bó mạch rây (Phloem) nằm khít xen kẽ giữa 4 cánh sao, phẳng cùng mặt cắt, lộ bản rây đục lỗ
            Mesh phloemBundleMesh = BuildRoundedBundleMesh("BoMachRayRe", 0.017f, 0.013f, 0.010f, H + 0.007f, 18);
            for (int i = 0; i < 4; i++)
            {
                float deg = 45f + i * 90f;
                float a = deg * Mathf.Deg2Rad;
                Vector3 pCenter = new Vector3(Mathf.Cos(a) * 0.046f, 0f, Mathf.Sin(a) * 0.046f);
                var phloem = MeshObject(grpStele, $"Bó mạch rây rễ {i + 1}", phloemBundleMesh,
                    pCenter, new Vector3(0f, -deg + 90f, 0f), Vector3.one, phloemFlowMat, hasCol: true);
                RegisterPart(phloem, "re_mach_ray", new Color(0.90f, 0.52f, 0.14f));

                Part(PrimitiveType.Cylinder, grpStele, "Bản rây", pCenter + new Vector3(0f, H + 0.0075f, 0f),
                    new Vector3(0.026f, 0.0012f, 0.020f), new Vector3(0f, -deg + 90f, 0f), sievePlateMat, hasCol: false);
                for (int sp = 0; sp < 5; sp++)
                {
                    float sa = sp * 72f * Mathf.Deg2Rad;
                    float sr = sp == 0 ? 0f : 0.0055f;
                    Part(PrimitiveType.Cylinder, grpStele, "Lỗ bản rây",
                        pCenter + new Vector3(Mathf.Cos(sa) * sr, H + 0.0085f, Mathf.Sin(sa) * sr),
                        new Vector3(0.0032f, 0.0010f, 0.0032f), Vector3.zero, lumenMat, hasCol: false);
                }
            }
            AddPin(KhungNhinThucVat.LatCatRe, "re_mach_ray", "Mạch rây & Bản rây (Xen kẽ ↓)", grpStele, new Vector3(0.036f, H + 0.012f, 0.036f), new Color(0.98f, 0.64f, 0.16f));
        }

        // ---------------------------------------------------------------------
        // Lát cắt 2: LÁT CẮT THÂN HAI LÁ MẦM — KHỐI TRỤ MÔ HỌC 3D + 10 BÓ MẠCH HỞ PHẲNG TỰ NHIÊN
        // ---------------------------------------------------------------------
        void BuildStemSlice()
        {
            stemSliceRoot = Group("Lát cắt Thân", transform, Vector3.zero);
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");

            Material stemHistMat = NewTexturedMat(urpLit, Color.white, stemHistTex, stemHistNormalTex, 0.28f, 0.90f);
            Material barkOuterMat = NewTexturedMat(urpLit, new Color(0.17f, 0.44f, 0.17f), stemBarkTex, stemBarkNormalTex, 0.34f, 0.85f);
            Material collenMat = NewTexturedMat(urpLit, new Color(0.25f, 0.54f, 0.23f), cellBrickTex, cellBrickNormalTex, 0.30f, 0.70f);
            Material cortexWallMat = NewTexturedMat(urpLit, new Color(0.42f, 0.64f, 0.34f), cellBrickTex, cellBrickNormalTex, 0.26f, 0.78f);
            Material sclerenCapMat = NewTexturedMat(urpLit, new Color(0.56f, 0.32f, 0.16f), cellBrickTex, cellBrickNormalTex, 0.34f, 0.65f);
            Material lumenMat = NewMat(urpLit, new Color(0.02f, 0.05f, 0.09f), 0.10f);
            Material vesselWallMat = NewMat(urpLit, new Color(0.30f, 0.72f, 0.94f), 0.48f);
            Material spiralRingMat = NewMat(urpLit, new Color(0.68f, 0.95f, 1.0f), 0.60f);
            Material sievePlateMat = NewMat(urpLit, new Color(0.86f, 0.64f, 0.24f), 0.38f);
            Material cambiumMat = NewMat(urpLit, new Color(0.76f, 0.72f, 0.18f), 0.44f);
            cambiumMat.EnableKeyword("_EMISSION");
            cambiumMat.SetColor("_EmissionColor", new Color(0.10f, 0.09f, 0.01f));

            const float H = 0.148f;
            const float HStep = 0.074f;

            // Tầng 1: Biểu bì thân, Mô dày nâng đỡ, Mô mềm vỏ & Ruột thân (Pith) liền khối cắt bậc thang 270° + 90°
            var grpOuter = Group("Tầng Biểu bì, Vỏ & Ruột thân", stemSliceRoot, Vector3.zero);
            RegisterExplode(grpOuter, new Vector3(0f, -0.050f, 0f));

            Vector2[] epiBackProf = { new Vector2(0.202f, 0f), new Vector2(0.216f, 0f), new Vector2(0.216f, H), new Vector2(0.202f, H) };
            var outerBack = MeshObject(grpOuter, "Biểu bì thân ngoài",
                BuildLatheSectorMesh("BieuBiThanSau", epiBackProf, -135f, 135f, 48, capEnds: false),
                Vector3.zero, Vector3.zero, Vector3.one, barkOuterMat, hasCol: true);
            RegisterPart(outerBack, "than_bieu_bi", new Color(0.17f, 0.44f, 0.17f));

            Vector2[] epiFrontProf = { new Vector2(0.202f, 0f), new Vector2(0.216f, 0f), new Vector2(0.216f, HStep), new Vector2(0.202f, HStep) };
            var outerFront = MeshObject(grpOuter, "Biểu bì thân bậc trước",
                BuildLatheSectorMesh("BieuBiThanTruoc", epiFrontProf, 135f, 225f, 18, capEnds: false),
                Vector3.zero, Vector3.zero, Vector3.one, barkOuterMat, hasCol: true);
            RegisterPart(outerFront, "than_bieu_bi", new Color(0.17f, 0.44f, 0.17f));

            // Vòng mô dày nâng đỡ (Collenchyma) ngay sát trong biểu bì
            var collen = MeshObject(grpOuter, "Vòng mô dày nâng đỡ",
                BuildPlanarRingSectorMesh("MoDayThanTop", 0.186f, 0.203f, H + 0.0006f, -135f, 135f, 44, 0.216f),
                Vector3.zero, Vector3.zero, Vector3.one, collenMat, hasCol: true);
            RegisterPart(collen, "than_bieu_bi", new Color(0.25f, 0.54f, 0.23f));

            // Mặt cắt ngang đỉnh (270° chính) + thềm cắt trước (90°) phủ bản đồ mô học tổ ong 384x384 phẳng sắc nét
            var cortexCore = MeshObject(grpOuter, "Mô mềm vỏ & Ruột thân",
                BuildPlanarRingSectorMesh("MoMemThanTop270", 0.000f, 0.187f, H, -135f, 135f, 48, 0.216f),
                Vector3.zero, Vector3.zero, Vector3.one, stemHistMat, hasCol: true);
            RegisterPart(cortexCore, "than_bieu_bi", new Color(0.42f, 0.64f, 0.34f));

            var frontCut = MeshObject(grpOuter, "Bậc cắt mở phía trước thân",
                BuildPlanarRingSectorMesh("BacCatThanTop90", 0.000f, 0.203f, HStep, 135f, 225f, 20, 0.216f),
                Vector3.zero, Vector3.zero, Vector3.one, stemHistMat, hasCol: true);
            RegisterPart(frontCut, "than_bieu_bi", new Color(0.42f, 0.64f, 0.34f));

            // 2 vách cắt đứng phẳng dọc xuyên tâm tại ±135° (nối từ bậc HStep lên mặt trên H)
            Vector2[] stemStepWallProf = { new Vector2(0.000f, HStep), new Vector2(0.216f, HStep), new Vector2(0.216f, H), new Vector2(0.000f, H) };
            Mesh stemCutWalls = BuildLatheCutWallMesh("VachCatDocThan", stemStepWallProf, -135f, 135f);
            MeshObject(grpOuter, "Mặt cắt dọc thân", stemCutWalls, Vector3.zero, Vector3.zero, Vector3.one, cortexWallMat, hasCol: false);

            AddPin(KhungNhinThucVat.LatCatThan, "than_bieu_bi", "Biểu bì, Mô dày & Ruột thân", grpOuter, new Vector3(0.195f, H * 0.94f, -0.055f), new Color(0.20f, 0.78f, 0.45f));

            // Tầng 2: Vòng Tầng sinh mạch / Tượng tầng (Vascular Cambium) mảnh sắc nét chạy xuyên 10 bó mạch (nổi nhẹ 4mm trên mặt cắt)
            var grpCambium = Group("Tầng Sinh mạch (Tượng tầng)", stemSliceRoot, Vector3.zero);
            RegisterExplode(grpCambium, new Vector3(0f, 0.045f, 0f));

            Vector2[] cambiumBackProf = { new Vector2(0.128f, H - 0.002f), new Vector2(0.135f, H - 0.002f), new Vector2(0.135f, H + 0.004f), new Vector2(0.128f, H + 0.004f) };
            var cambiumRing = MeshObject(grpCambium, "Vòng tầng sinh mạch (Tượng tầng)",
                BuildLatheSectorMesh("VongTuongTang", cambiumBackProf, -135f, 135f, 48, capEnds: true),
                Vector3.zero, Vector3.zero, Vector3.one, cambiumMat, hasCol: true);
            RegisterPart(cambiumRing, "than_tang_sinh_mach", new Color(0.76f, 0.72f, 0.18f));

            Vector2[] cambiumFrontProf = { new Vector2(0.128f, HStep - 0.002f), new Vector2(0.135f, HStep - 0.002f), new Vector2(0.135f, HStep + 0.004f), new Vector2(0.128f, HStep + 0.004f) };
            var cambiumFront = MeshObject(grpCambium, "Vòng tượng tầng bậc trước",
                BuildLatheSectorMesh("VongTuongTangTruoc", cambiumFrontProf, 135f, 225f, 16, capEnds: true),
                Vector3.zero, Vector3.zero, Vector3.one, cambiumMat, hasCol: true);
            RegisterPart(cambiumFront, "than_tang_sinh_mach", new Color(0.76f, 0.72f, 0.18f));

            Vector2[] cambVertProf = { new Vector2(0.128f, HStep), new Vector2(0.135f, HStep), new Vector2(0.135f, H + 0.004f), new Vector2(0.128f, H + 0.004f) };
            MeshObject(grpCambium, "Dải tượng tầng dọc", BuildLatheCutWallMesh("TuongTangDoc", cambVertProf, -135f, 135f),
                Vector3.zero, Vector3.zero, Vector3.one, cambiumMat, hasCol: false);

            AddPin(KhungNhinThucVat.LatCatThan, "than_tang_sinh_mach", "Tầng sinh mạch (Tượng tầng)", grpCambium, new Vector3(-0.132f, H + 0.008f, 0.025f), new Color(0.95f, 0.84f, 0.18f));

            // Tầng 3 & 4: 10 Bó mạch hở (Open Collateral Vascular Bundles) xếp 1 vòng đồng tâm chuẩn hai lá mầm
            // Nằm PHẲNG KHÍT trong mô thân (micro-relief 4..8mm); 2 bó mặt trước cắt bậc lộ vân xoắn lignin dọc lòng mạch!
            var grpPhloem = Group("Vòng Mạch rây ngoài", stemSliceRoot, Vector3.zero);
            var grpXylem = Group("Vòng Mạch gỗ trong", stemSliceRoot, Vector3.zero);
            RegisterExplode(grpPhloem, new Vector3(0f, 0.085f, 0f));
            RegisterExplode(grpXylem, new Vector3(0f, 0.130f, 0f));

            Mesh phloemTallMesh = BuildRoundedBundleMesh("BoMachRayThanCao", 0.019f, 0.014f, 0.010f, H + 0.006f, 18);
            Mesh phloemCutMesh = BuildRoundedBundleMesh("BoMachRayThanCat", 0.019f, 0.014f, 0.010f, HStep + 0.028f, 18);
            Mesh sclerenTallMesh = BuildRoundedBundleMesh("MuMoCungCao", 0.020f, 0.0065f, 0.010f, H + 0.004f, 16);
            Mesh sclerenCutMesh = BuildRoundedBundleMesh("MuMoCungCat", 0.020f, 0.0065f, 0.010f, HStep + 0.022f, 16);
            Mesh xylemTallMesh = BuildRoundedBundleMesh("NenMachGoCao", 0.016f, 0.021f, 0.010f, H + 0.005f, 18);
            Mesh xylemCutMesh = BuildRoundedBundleMesh("NenMachGoCat", 0.016f, 0.021f, 0.010f, HStep + 0.036f, 18);

            const int bundleCount = 10;
            for (int i = 0; i < bundleCount; i++)
            {
                // Đặt góc theo trục +Z = 0° để khớp chính xác góc sector của BuildLatheSectorMesh (135°..225° là phía trước -Z)
                float deg = i * (360f / bundleCount) + 18f;
                float a = deg * Mathf.Deg2Rad;
                Vector3 rad = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                Vector3 tang = new Vector3(Mathf.Cos(a), 0f, -Mathf.Sin(a));
                Vector3 euler = new Vector3(0f, deg, 0f);

                bool inFrontCutaway = deg > 136f && deg < 224f;
                float phTopY = inFrontCutaway ? (HStep + 0.028f) : (H + 0.006f);
                float xyTopY = inFrontCutaway ? (HStep + 0.044f) : (H + 0.009f);

                // 1) Mũ sợi mô cứng (Sclerenchyma cap) bảo vệ phía ngoài bó mạch
                var cap = MeshObject(grpPhloem, $"Bó {i + 1} - Mũ sợi mô cứng", inFrontCutaway ? sclerenCutMesh : sclerenTallMesh,
                    rad * 0.168f, euler, Vector3.one, sclerenCapMat, hasCol: true);
                RegisterPart(cap, "than_mach_ray", new Color(0.56f, 0.32f, 0.16f));

                // 2) Mạch rây (Phloem - ngoài tượng tầng) & Bản rây đục lỗ
                var phloem = MeshObject(grpPhloem, $"Bó {i + 1} - Mạch rây", inFrontCutaway ? phloemCutMesh : phloemTallMesh,
                    rad * 0.148f, euler, Vector3.one, phloemFlowMat, hasCol: true);
                RegisterPart(phloem, "than_mach_ray", new Color(0.90f, 0.52f, 0.14f));

                Part(PrimitiveType.Cylinder, grpPhloem, "Bản rây thân",
                    rad * 0.148f + new Vector3(0f, phTopY + 0.0005f, 0f), new Vector3(0.028f, 0.0012f, 0.020f), euler, sievePlateMat, hasCol: false);
                for (int hole = -1; hole <= 1; hole++)
                {
                    Part(PrimitiveType.Cylinder, grpPhloem, "Lỗ bản rây",
                        rad * 0.148f + tang * (hole * 0.0078f) + new Vector3(0f, phTopY + 0.0014f, 0f),
                        new Vector3(0.0032f, 0.0010f, 0.0032f), Vector3.zero, lumenMat, hasCol: false);
                }

                // 3) Mạch gỗ (Xylem - trong tượng tầng, hướng vào ruột): nền gỗ + 2 Metaxylem lớn + 2 Protoxylem nhỏ rỗng lòng
                var xBase = MeshObject(grpXylem, $"Bó {i + 1} - Mô gỗ", inFrontCutaway ? xylemCutMesh : xylemTallMesh,
                    rad * 0.106f, euler, Vector3.one, xylemFlowMat, hasCol: true);
                RegisterPart(xBase, "than_mach_go", new Color(0.10f, 0.52f, 0.94f));

                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 metaPos = rad * 0.114f + tang * (side * 0.010f);
                    Mesh metaTube = BuildHollowVesselMesh($"Metaxylem_{i}_{side}", 0.0056f, 0.0082f, 0.012f, xyTopY, 16);
                    var metaObj = MeshObject(grpXylem, $"Bó {i + 1} - Mạch ống lớn (Metaxylem)", metaTube,
                        metaPos, Vector3.zero, Vector3.one, vesselWallMat, hasCol: true);
                    RegisterPart(metaObj, "than_mach_go", new Color(0.10f, 0.52f, 0.94f));

                    Part(PrimitiveType.Cylinder, grpXylem, "Lòng mạch gỗ",
                        metaPos + new Vector3(0f, xyTopY - 0.005f, 0f), new Vector3(0.0108f, 0.002f, 0.0108f), Vector3.zero, lumenMat, hasCol: false);

                    // Ở 2 bó nằm trong bậc cắt trước: lộ lò xo vân xoắn lignin bên trong ống mạch cắt dọc
                    if (inFrontCutaway)
                    {
                        Mesh helix = BuildHelixSpiralMesh($"VanXoanThan_{i}_{side}", 0.0060f, HStep + 0.002f, xyTopY - 0.001f, 3.8f, 0.0009f);
                        MeshObject(grpXylem, "Vân xoắn lignin", helix, metaPos, Vector3.zero, Vector3.one, spiralRingMat, hasCol: false);
                    }
                }

                Vector3 protoPos1 = rad * 0.095f;
                var proto1 = MeshObject(grpXylem, $"Bó {i + 1} - Mạch gỗ trước (Protoxylem)",
                    BuildHollowVesselMesh($"ProtoA_{i}", 0.0040f, 0.0064f, 0.012f, xyTopY + 0.001f, 14),
                    protoPos1, Vector3.zero, Vector3.one, vesselWallMat, hasCol: true);
                RegisterPart(proto1, "than_mach_go", new Color(0.10f, 0.52f, 0.94f));

                Vector3 protoPos2 = rad * 0.080f;
                var proto2 = MeshObject(grpXylem, $"Bó {i + 1} - Mạch gỗ sơ cấp",
                    BuildHollowVesselMesh($"ProtoB_{i}", 0.0030f, 0.0048f, 0.012f, xyTopY + 0.001f, 12),
                    protoPos2, Vector3.zero, Vector3.one, vesselWallMat, hasCol: true);
                RegisterPart(proto2, "than_mach_go", new Color(0.10f, 0.52f, 0.94f));
            }

            RegisterAnchor("than_xylem_trai_duoi", grpXylem, new Vector3(-0.033f, HStep + 0.008f, -0.104f));
            RegisterAnchor("than_xylem_trai_tren", grpXylem, new Vector3(-0.033f, HStep + 0.068f, -0.104f));
            RegisterAnchor("than_xylem_phai_duoi", grpXylem, new Vector3( 0.033f, HStep + 0.008f, -0.104f));
            RegisterAnchor("than_xylem_phai_tren", grpXylem, new Vector3( 0.033f, HStep + 0.068f, -0.104f));
            RegisterAnchor("than_ray_trai_tren", grpPhloem, new Vector3(-0.046f, HStep + 0.052f, -0.141f));
            RegisterAnchor("than_ray_trai_duoi", grpPhloem, new Vector3(-0.046f, HStep + 0.004f, -0.141f));
            RegisterAnchor("than_ray_phai_tren", grpPhloem, new Vector3( 0.046f, HStep + 0.052f, -0.141f));
            RegisterAnchor("than_ray_phai_duoi", grpPhloem, new Vector3( 0.046f, HStep + 0.004f, -0.141f));

            AddPin(KhungNhinThucVat.LatCatThan, "than_mach_ray", "Mạch rây & Mũ mô cứng (Ngoài ↓)", grpPhloem, new Vector3(0.130f, H + 0.012f, 0.085f), new Color(0.98f, 0.62f, 0.16f));
            AddPin(KhungNhinThucVat.LatCatThan, "than_mach_go", "Mạch gỗ Metaxylem / Protoxylem (Trong ↑)", grpXylem, new Vector3(-0.045f, H + 0.014f, 0.095f), new Color(0.22f, 0.74f, 0.98f));
        }

        // ---------------------------------------------------------------------
        // Lát cắt 3: LÁT CẮT LÁ HAI LÁ MẦM — KHỐI MÔ LÁ LIỀN KHỐI THỰC TẾ (KHÔNG HỞ NỀN ĐEN!)
        // ---------------------------------------------------------------------
        void BuildLeafSlice()
        {
            leafSliceRoot = Group("Lát cắt Lá", transform, Vector3.zero);
            stomaRigs.Clear();
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");

            Material cutinMat = NewTransparentLit(urpLit, new Color(0.76f, 0.94f, 0.80f, 0.24f));
            Material upEpiTopMat = NewTexturedMat(urpLit, new Color(0.24f, 0.54f, 0.21f), leafUpperTex, leafUpperNormalTex, 0.48f, 0.85f);
            Material epiCutEdgeMat = NewTexturedMat(urpLit, new Color(0.42f, 0.66f, 0.34f), cellBrickTex, cellBrickNormalTex, 0.34f, 0.65f);
            Material interiorMatrixMat = NewTexturedMat(urpLit, new Color(0.36f, 0.58f, 0.30f), leafCutFaceTex, leafCutFaceNormalTex, 0.22f, 0.90f);
            Material palisadeWallMat = NewTexturedMat(urpLit, new Color(0.16f, 0.48f, 0.17f), cellBrickTex, cellBrickNormalTex, 0.36f, 0.58f);
            Material cellCytoMat = NewMat(urpLit, new Color(0.32f, 0.64f, 0.28f), 0.30f);
            Material cellVacuoleMat = NewMat(urpLit, new Color(0.54f, 0.78f, 0.56f), 0.44f);
            Material chloroplastMat = NewMat(urpLit, new Color(0.06f, 0.36f, 0.09f), 0.54f);
            Material spongyMat = NewTexturedMat(urpLit, new Color(0.24f, 0.56f, 0.22f), cellBrickTex, cellBrickNormalTex, 0.34f, 0.62f);
            Material sheathMat = NewTexturedMat(urpLit, new Color(0.40f, 0.66f, 0.32f), cellBrickTex, cellBrickNormalTex, 0.34f, 0.65f);
            Material midribCollenMat = NewTexturedMat(urpLit, new Color(0.26f, 0.54f, 0.22f), cellBrickTex, cellBrickNormalTex, 0.36f, 0.70f);
            Material guardMat = NewMat(urpLit, new Color(0.22f, 0.64f, 0.20f), 0.50f);
            Material guardThickWallMat = NewMat(urpLit, new Color(0.68f, 0.84f, 0.46f), 0.55f);
            Material airChamberMat = NewMat(urpLit, new Color(0.04f, 0.10f, 0.05f), 0.10f);
            Material vesselWallMat = NewMat(urpLit, new Color(0.28f, 0.68f, 0.90f), 0.50f);
            Material spiralRingMat = NewMat(urpLit, new Color(0.66f, 0.94f, 1.0f), 0.58f);
            Material sievePlateMat = NewMat(urpLit, new Color(0.84f, 0.62f, 0.24f), 0.38f);

            const float W = 0.42f;
            const float D = 0.26f;

            // 1. Tầng Sáp Cutin & Biểu bì trên (mặt trên giống lá thật có tế bào lát đá hình mảnh ghép, không còn ô vuông xanh dạ quang)
            var grpUpperEpi = Group("Tầng Cutin & Biểu bì trên", leafSliceRoot, Vector3.zero);
            RegisterExplode(grpUpperEpi, new Vector3(0f, 0.085f, 0f));

            Mesh cutinSheet = BuildCellularPillowSheetMesh("LopCutinTren", 12, 7, W + 0.003f, D + 0.003f, 0.0045f, 0.0012f, stepCutFrontCorner: false);
            MeshObject(grpUpperEpi, "Lớp sáp Cutin bóng", cutinSheet, new Vector3(0f, 0.155f, 0f), Vector3.zero, Vector3.one, cutinMat, hasCol: false);

            Mesh upperEpiSheet = BuildCellularPillowSheetMesh("BieuBiTrenLa", 12, 7, W, D, 0.020f, 0.0032f, stepCutFrontCorner: false);
            var upEpiObj = MeshObject(grpUpperEpi, "Mô biểu bì trên", upperEpiSheet, new Vector3(0f, 0.144f, 0f), Vector3.zero, Vector3.one, upEpiTopMat, hasCol: true);
            RegisterPart(upEpiObj, "la_bieu_bi_cutin", new Color(0.24f, 0.54f, 0.21f));

            // Dải tế bào biểu bì trên mặt cắt trước (màu biểu bì tự nhiên bo khít vách, không chói sáng)
            Material epiLumenMat = NewMat(urpLit, new Color(0.50f, 0.72f, 0.44f), 0.36f);
            for (int ix = -5; ix <= 5; ix++)
            {
                var cell = Part(PrimitiveType.Cube, grpUpperEpi, "Ô biểu bì trên",
                    new Vector3(ix * 0.036f, 0.134f, -D * 0.5f + 0.003f), new Vector3(0.0345f, 0.0170f, 0.008f), Vector3.zero, epiCutEdgeMat, hasCol: false);
                Part(PrimitiveType.Cube, cell.transform, "Khoang biểu bì",
                    new Vector3(0f, 0f, -0.35f), new Vector3(0.84f, 0.68f, 0.40f), Vector3.zero, epiLumenMat, hasCol: false);
            }
            RegisterAnchor("la_anh_sang_trai", grpUpperEpi, new Vector3(-0.115f, 0.228f, -0.095f));
            RegisterAnchor("la_anh_sang_phai", grpUpperEpi, new Vector3( 0.115f, 0.228f, -0.095f));
            AddPin(KhungNhinThucVat.LatCatLa, "la_bieu_bi_cutin", "Lớp sáp Cutin & Biểu bì trên", grpUpperEpi, new Vector3(0.145f, 0.156f, -0.04f), new Color(0.52f, 0.90f, 0.66f));

            // 2. Tầng Thịt lá LIỀN KHỐI (Solid Mesophyll Core + Mô giậu xếp KHÍT ép sát nhau + Mô xốp & Buồng khí thật sự)
            var grpMesophyll = Group("Tầng Thịt lá (Mô giậu & Mô xốp)", leafSliceRoot, Vector3.zero);
            RegisterExplode(grpMesophyll, new Vector3(0f, 0.025f, 0f));

            // Khối lõi thịt lá liền mạch bao kín phía sau mặt cắt (xuyên suốt hai cánh lá, không còn hở nền đen)
            var coreBlockL = Part(PrimitiveType.Cube, grpMesophyll, "Khối lõi thịt lá (Trái)",
                new Vector3(-0.128f, 0.064f, 0.008f), new Vector3(0.162f, 0.122f, D - 0.024f), Vector3.zero, interiorMatrixMat, hasCol: true);
            var coreBlockR = Part(PrimitiveType.Cube, grpMesophyll, "Khối lõi thịt lá (Phải)",
                new Vector3( 0.128f, 0.064f, 0.008f), new Vector3(0.162f, 0.122f, D - 0.024f), Vector3.zero, interiorMatrixMat, hasCol: true);
            RegisterPart(coreBlockL, "la_mo_giau_va_mo_xop", new Color(0.24f, 0.56f, 0.22f));
            RegisterPart(coreBlockR, "la_mo_giau_va_mo_xop", new Color(0.24f, 0.56f, 0.22f));

            // Mô giậu xếp khít sát vách (không còn kẽ hở lớn giữa các cột)
            Mesh palisadeCellMesh = BuildOrganicPalisadeCellMesh(0.0176f, 0.064f);
            Mesh chloroplastDiscMesh = BuildChloroplastDiscMesh(0.0036f, 0.0018f);

            // Mặt cắt trước (-Z) & mặt sau (+Z) của lớp mô giậu
            for (int ix = -6; ix <= 6; ix++)
            {
                if (Mathf.Abs(ix) <= 1) continue; // Chừa khoang gân chính ở giữa (x = 0)
                float x = ix * 0.032f;
                for (int face = 0; face < 2; face++)
                {
                    float z = face == 0 ? (-D * 0.5f + 0.016f) : (D * 0.5f - 0.016f);
                    Vector3 cPos = new Vector3(x, 0.094f, z);
                    var pal = MeshObject(grpMesophyll, "Tế bào mô giậu", palisadeCellMesh, cPos, Vector3.zero, Vector3.one, palisadeWallMat, hasCol: true);
                    RegisterPart(pal, "la_mo_giau_va_mo_xop", new Color(0.18f, 0.54f, 0.20f));

                    // Hàng mặt cắt phía trước: bổ mở lộ rõ tế bào chất, không bào trung tâm & 6 hạt lục lạp nằm ngay trên mặt cắt tế bào
                    if (face == 0)
                    {
                        MeshObject(grpMesophyll, "Tế bào chất mô giậu", palisadeCellMesh,
                            cPos + new Vector3(0f, 0f, -0.0085f), Vector3.zero, new Vector3(0.80f, 0.90f, 0.56f), cellCytoMat, hasCol: false);
                        MeshObject(grpMesophyll, "Không bào mô giậu", palisadeCellMesh,
                            cPos + new Vector3(0f, 0f, -0.0135f), Vector3.zero, new Vector3(0.46f, 0.72f, 0.34f), cellVacuoleMat, hasCol: false);

                        for (int ch = 0; ch < 6; ch++)
                        {
                            float side = (ch % 2 == 0) ? -1f : 1f;
                            float cy = -0.018f + (ch / 2) * 0.018f;
                            MeshObject(grpMesophyll, "Hạt lục lạp", chloroplastDiscMesh,
                                cPos + new Vector3(side * 0.0102f, cy, -0.0178f), new Vector3(0f, 0f, side * 12f), Vector3.one, chloroplastMat, hasCol: false);
                        }
                    }
                }
            }
            // Hai mặt cắt hông trái/phải của lớp mô giậu (để xoay 360° mặt nào cũng là lát cắt thật!)
            for (int iz = -2; iz <= 2; iz++)
            {
                float z = iz * 0.044f;
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 cPos = new Vector3(side * (W * 0.5f - 0.016f), 0.094f, z);
                    var pal = MeshObject(grpMesophyll, "Tế bào mô giậu hông", palisadeCellMesh, cPos, Vector3.zero, Vector3.one, palisadeWallMat, hasCol: false);
                    RegisterPart(pal, "la_mo_giau_va_mo_xop", new Color(0.18f, 0.54f, 0.20f));
                }
            }

            // Mô xốp (Spongy mesophyll): mạng tế bào tròn có cầu nối ôm quanh 2 buồng khí dưới khí khổng ở x = ±0.105m
            Mesh spongyLobeMesh = BuildOrganicSpongyCellMesh(0.0185f);
            Vector3[] spongyFrontCoords =
            {
                // Cánh trái (chừa buồng khí vòm tại x = -0.105, y = 0.024)
                new Vector3(-0.186f, 0.044f, -0.112f), new Vector3(-0.184f, 0.018f, -0.110f),
                new Vector3(-0.150f, 0.046f, -0.112f), new Vector3(-0.148f, 0.019f, -0.108f),
                new Vector3(-0.105f, 0.052f, -0.110f), // Đỉnh vòm buồng khí trái
                new Vector3(-0.064f, 0.045f, -0.112f), new Vector3(-0.068f, 0.019f, -0.108f),
                // Cánh phải (chừa buồng khí vòm tại x = +0.105, y = 0.024)
                new Vector3( 0.064f, 0.045f, -0.112f), new Vector3( 0.068f, 0.019f, -0.108f),
                new Vector3( 0.105f, 0.052f, -0.110f), // Đỉnh vòm buồng khí phải
                new Vector3( 0.150f, 0.046f, -0.112f), new Vector3( 0.148f, 0.019f, -0.108f),
                new Vector3( 0.186f, 0.044f, -0.112f), new Vector3( 0.184f, 0.018f, -0.110f)
            };
            for (int i = 0; i < spongyFrontCoords.Length; i++)
            {
                Vector3 sPos = spongyFrontCoords[i];
                var sp = MeshObject(grpMesophyll, "Tế bào mô xốp", spongyLobeMesh, sPos, Vector3.zero, new Vector3(1.10f, 0.90f, 1.05f), spongyMat, hasCol: true);
                RegisterPart(sp, "la_mo_giau_va_mo_xop", new Color(0.28f, 0.62f, 0.26f));

                // Lộ lát cắt tế bào chất, không bào & lục lạp của mô xốp mặt trước
                MeshObject(grpMesophyll, "Tế bào chất mô xốp", spongyLobeMesh,
                    sPos + new Vector3(0f, 0f, -0.0085f), Vector3.zero, new Vector3(0.84f, 0.70f, 0.68f), cellCytoMat, hasCol: false);
                MeshObject(grpMesophyll, "Không bào mô xốp", spongyLobeMesh,
                    sPos + new Vector3(0f, 0f, -0.0138f), Vector3.zero, new Vector3(0.50f, 0.44f, 0.44f), cellVacuoleMat, hasCol: false);
                for (int ch = 0; ch < 3; ch++)
                {
                    float a = (ch * 120f + 25f) * Mathf.Deg2Rad;
                    MeshObject(grpMesophyll, "Lục lạp mô xốp", chloroplastDiscMesh,
                        sPos + new Vector3(Mathf.Cos(a) * 0.0115f, Mathf.Sin(a) * 0.0088f, -0.0195f), Vector3.zero, Vector3.one, chloroplastMat, hasCol: false);
                }

                // Đối xứng mặt sau (+Z) để xoay 360° vẫn kín đẹp
                Vector3 backPos = new Vector3(sPos.x, sPos.y, -sPos.z);
                MeshObject(grpMesophyll, "Tế bào mô xốp sau", spongyLobeMesh, backPos, Vector3.zero, new Vector3(1.10f, 0.90f, 1.05f), spongyMat, hasCol: false);
            }

            // Hai hốc tối mô phỏng khoang chứa khí dưới khí khổng (Substomatal Air Chamber) tại x = ±0.105m
            Part(PrimitiveType.Sphere, grpMesophyll, "Khoang khí dưới khí khổng (Phải)",
                new Vector3( 0.105f, 0.022f, -0.095f), new Vector3(0.050f, 0.036f, 0.044f), Vector3.zero, airChamberMat, hasCol: false);
            Part(PrimitiveType.Sphere, grpMesophyll, "Khoang khí dưới khí khổng (Trái)",
                new Vector3(-0.105f, 0.022f, -0.095f), new Vector3(0.050f, 0.036f, 0.044f), Vector3.zero, airChamberMat, hasCol: false);

            RegisterAnchor("la_mo_giau_trai", grpMesophyll, new Vector3(-0.115f, 0.096f, -0.128f));
            RegisterAnchor("la_mo_giau_phai", grpMesophyll, new Vector3( 0.115f, 0.096f, -0.128f));
            RegisterAnchor("la_mo_xop_trai", grpMesophyll, new Vector3(-0.145f, 0.040f, -0.124f));
            RegisterAnchor("la_mo_xop_phai", grpMesophyll, new Vector3( 0.145f, 0.040f, -0.124f));
            RegisterAnchor("la_buong_khi_trai", grpMesophyll, new Vector3(-0.105f, 0.022f, -0.114f));
            RegisterAnchor("la_buong_khi_phai", grpMesophyll, new Vector3( 0.105f, 0.022f, -0.114f));

            AddPin(KhungNhinThucVat.LatCatLa, "la_mo_giau_va_mo_xop", "Mô giậu giàu lục lạp", grpMesophyll, new Vector3(-0.145f, 0.098f, -0.125f), new Color(0.16f, 0.80f, 0.38f));
            AddPin(KhungNhinThucVat.LatCatLa, "la_mo_giau_va_mo_xop", "Mô xốp & Buồng khí CO₂/O₂", grpMesophyll, new Vector3(-0.115f, 0.026f, -0.122f), new Color(0.36f, 0.84f, 0.56f));

            // 3. Bó mạch Gân chính (Midrib Vascular Bundle): nằm gọn trong gờ gân chính lồi tự nhiên ở mặt dưới lá (x = 0)
            var grpVein = Group("Tầng Bó mạch gân lá", leafSliceRoot, Vector3.zero);
            RegisterExplode(grpVein, new Vector3(0f, -0.020f, 0f));

            // Gờ sống gân chính (Midrib Collenchyma Rib) lồi tự nhiên phía dưới x = 0 giống y hệt mặt dưới lá đậu thật
            Vector3[] midribPts = { new Vector3(0f, 0.020f, -D * 0.5f), new Vector3(0f, 0.020f, 0f), new Vector3(0f, 0.020f, D * 0.5f) };
            var ribObj = MeshObject(grpVein, "Gờ sống gân chính", BuildSplineTubeMesh("GoGanChinh", midribPts, new[] { 0.056f, 0.058f, 0.056f }, 20),
                Vector3.zero, Vector3.zero, new Vector3(1.06f, 0.92f, 1.0f), midribCollenMat, hasCol: true);
            RegisterPart(ribObj, "la_gan_bo_mach", new Color(0.32f, 0.60f, 0.26f));

            // Vòng 14 tế bào Bao bó mạch (Bundle sheath) ôm khít quanh bó mạch gân chính
            for (int b = 0; b < 14; b++)
            {
                float ba = b * (360f / 14f) * Mathf.Deg2Rad;
                Vector3 bPos = new Vector3(Mathf.Cos(ba) * 0.044f, 0.046f + Mathf.Sin(ba) * 0.038f, 0f);
                Vector3[] shPts = { bPos + new Vector3(0f, 0f, -D * 0.51f), bPos, bPos + new Vector3(0f, 0f, D * 0.51f) };
                var sh = MeshObject(grpVein, $"Bao bó mạch {b + 1}",
                    BuildSplineTubeMesh($"BaoBoMachLa_{b}", shPts, new[] { 0.0090f, 0.0095f, 0.0090f }, 10),
                    Vector3.zero, Vector3.zero, Vector3.one, sheathMat, hasCol: true);
                RegisterPart(sh, "la_gan_bo_mach", new Color(0.48f, 0.74f, 0.38f));
            }

            // Nửa trên bó mạch: 5 ống Mạch gỗ (Xylem) rỗng lòng có vân xoắn lignin ở mặt cắt trước
            Vector2[] leafXylemCoords =
            {
                new Vector2( 0.000f, 0.066f),
                new Vector2(-0.016f, 0.062f), new Vector2( 0.016f, 0.062f),
                new Vector2(-0.008f, 0.050f), new Vector2( 0.008f, 0.050f)
            };
            for (int x = 0; x < leafXylemCoords.Length; x++)
            {
                float rO = x == 0 ? 0.0088f : 0.0066f;
                Mesh hVessel = BuildHollowVesselMesh($"MachGoGanLa_{x}", rO * 0.64f, rO, -D * 0.53f, D * 0.53f, 14);
                var lx = MeshObject(grpVein, $"Gân lá - Mạch gỗ trên {x + 1}", hVessel,
                    new Vector3(leafXylemCoords[x].x, leafXylemCoords[x].y, 0f), new Vector3(90f, 0f, 0f), Vector3.one, xylemFlowMat, hasCol: true);
                RegisterPart(lx, "la_gan_bo_mach", new Color(0.10f, 0.52f, 0.94f));

                Mesh rimFront = BuildHollowVesselMesh($"VienMachGoLa_{x}", rO * 0.64f, rO, -D * 0.54f, -D * 0.515f, 14);
                MeshObject(grpVein, "Vành thành mạch gỗ", rimFront,
                    new Vector3(leafXylemCoords[x].x, leafXylemCoords[x].y, 0f), new Vector3(90f, 0f, 0f), Vector3.one, vesselWallMat, hasCol: false);

                if (x <= 2)
                {
                    Mesh helix = BuildHelixSpiralMesh($"VanXoanLa_{x}", rO * 0.72f, -D * 0.53f, -D * 0.34f, 3.2f, 0.0008f);
                    MeshObject(grpVein, "Vân xoắn lignin mạch gỗ lá", helix,
                        new Vector3(leafXylemCoords[x].x, leafXylemCoords[x].y, 0f), new Vector3(90f, 0f, 0f), Vector3.one, spiralRingMat, hasCol: false);
                }
            }

            // Nửa dưới bó mạch: 4 ống Mạch rây (Phloem) có bản rây đục lỗ ở đầu mặt cắt
            Vector2[] leafPhloemCoords =
            {
                new Vector2(-0.014f, 0.030f), new Vector2(0.000f, 0.028f), new Vector2(0.014f, 0.030f), new Vector2(0.000f, 0.017f)
            };
            for (int p = 0; p < leafPhloemCoords.Length; p++)
            {
                float rO = p == 1 ? 0.0078f : 0.0062f;
                Vector3 p0 = new Vector3(leafPhloemCoords[p].x, leafPhloemCoords[p].y, -D * 0.53f);
                Vector3 p1 = new Vector3(leafPhloemCoords[p].x, leafPhloemCoords[p].y,  D * 0.53f);
                var lp = MeshObject(grpVein, $"Gân lá - Mạch rây dưới {p + 1}",
                    BuildSplineTubeMesh($"MachRayGanLa_{p}", new[] { p0, p1 }, new[] { rO, rO }, 12),
                    Vector3.zero, Vector3.zero, Vector3.one, phloemFlowMat, hasCol: true);
                RegisterPart(lp, "la_gan_bo_mach", new Color(0.90f, 0.52f, 0.14f));

                Part(PrimitiveType.Cylinder, grpVein, "Bản rây gân lá",
                    new Vector3(leafPhloemCoords[p].x, leafPhloemCoords[p].y, -D * 0.533f),
                    new Vector3(rO * 1.85f, 0.0012f, rO * 1.85f), new Vector3(90f, 0f, 0f), sievePlateMat, hasCol: false);
            }
            RegisterAnchor("la_gan_go_truoc", grpVein, new Vector3(0.000f, 0.064f, -0.140f));
            RegisterAnchor("la_gan_ray_truoc", grpVein, new Vector3(0.000f, 0.028f, -0.140f));
            RegisterAnchor("la_gan_ray_sau", grpVein, new Vector3(0.000f, 0.028f,  0.115f));
            AddPin(KhungNhinThucVat.LatCatLa, "la_gan_bo_mach", "Bó mạch gân lá (Gỗ trên · Rây dưới)", grpVein, new Vector3(0.010f, 0.056f, -D * 0.54f), new Color(0.22f, 0.74f, 0.98f));

            // 4. Biểu bì dưới liên tục & 2 Phức hợp Khí khổng (đặt đúng vị trí phiến lá hai bên gân chính x = ±0.105m!)
            var grpLowerEpi = Group("Tầng Biểu bì dưới & Khí khổng", leafSliceRoot, Vector3.zero);
            RegisterExplode(grpLowerEpi, new Vector3(0f, -0.075f, 0f));

            // Tấm biểu bì dưới liền mạch bao kín mặt dưới (trừ 2 lỗ mở nhỏ gọn đúng vị trí khí khổng)
            Mesh lowerOuterStrip = BuildCellularPillowSheetMesh("BieuBiDuoiNgoai", 3, 7, 0.090f, D, 0.018f, 0.0026f, stepCutFrontCorner: false);
            Mesh lowerBackStrip = BuildCellularPillowSheetMesh("BieuBiDuoiSau", 4, 4, 0.125f, 0.155f, 0.018f, 0.0026f, stepCutFrontCorner: false);
            var lowLOut = MeshObject(grpLowerEpi, "Biểu bì dưới (Mép trái)", lowerOuterStrip, new Vector3(-0.165f, 0.002f, 0f), Vector3.zero, Vector3.one, upEpiTopMat, hasCol: true);
            var lowROut = MeshObject(grpLowerEpi, "Biểu bì dưới (Mép phải)", lowerOuterStrip, new Vector3( 0.165f, 0.002f, 0f), Vector3.zero, Vector3.one, upEpiTopMat, hasCol: true);
            var lowLBack = MeshObject(grpLowerEpi, "Biểu bì dưới (Sau trái)", lowerBackStrip, new Vector3(-0.088f, 0.002f, 0.052f), Vector3.zero, Vector3.one, upEpiTopMat, hasCol: true);
            var lowRBack = MeshObject(grpLowerEpi, "Biểu bì dưới (Sau phải)", lowerBackStrip, new Vector3( 0.088f, 0.002f, 0.052f), Vector3.zero, Vector3.one, upEpiTopMat, hasCol: true);
            RegisterPart(lowLOut, "la_bieu_bi_cutin", new Color(0.40f, 0.68f, 0.34f));
            RegisterPart(lowROut, "la_bieu_bi_cutin", new Color(0.40f, 0.68f, 0.34f));
            RegisterPart(lowLBack, "la_bieu_bi_cutin", new Color(0.40f, 0.68f, 0.34f));
            RegisterPart(lowRBack, "la_bieu_bi_cutin", new Color(0.40f, 0.68f, 0.34f));

            // 2 Phức hợp Khí khổng sống (Hero Stoma mặt cắt trước phải x = +0.105m & Stoma đối xứng x = -0.105m)
            Mesh leftKidneyMesh = BuildKidneyGuardCellMesh("TeBaoHatDauTrai", isLeft: true, out Mesh leftInnerWallMesh);
            Mesh rightKidneyMesh = BuildKidneyGuardCellMesh("TeBaoHatDauPhai", isLeft: false, out Mesh rightInnerWallMesh);

            Vector3[] stomaCenters =
            {
                new Vector3( 0.105f, -0.010f, -0.102f),
                new Vector3(-0.105f, -0.010f, -0.102f)
            };
            for (int s = 0; s < stomaCenters.Length; s++)
            {
                Vector3 c = stomaCenters[s];
                var gL = MeshObject(grpLowerEpi, $"Khí khổng {s + 1} - Tế bào hạt đậu Trái", leftKidneyMesh,
                    c + new Vector3(-0.011f, 0f, 0f), Vector3.zero, Vector3.one, guardMat, hasCol: true);
                var gR = MeshObject(grpLowerEpi, $"Khí khổng {s + 1} - Tế bào hạt đậu Phải", rightKidneyMesh,
                    c + new Vector3( 0.011f, 0f, 0f), Vector3.zero, Vector3.one, guardMat, hasCol: true);
                RegisterPart(gL, "la_khi_khong", new Color(0.22f, 0.70f, 0.22f));
                RegisterPart(gR, "la_khi_khong", new Color(0.22f, 0.70f, 0.22f));

                for (int cl = -1; cl <= 1; cl++)
                {
                    MeshObject(gL.transform, "Lục lạp khí khổng", chloroplastDiscMesh,
                        new Vector3(-0.010f, 0.002f, cl * 0.010f), new Vector3(8f, 25f, 0f), Vector3.one, chloroplastMat, hasCol: false);
                    MeshObject(gR.transform, "Lục lạp khí khổng", chloroplastDiscMesh,
                        new Vector3( 0.010f, 0.002f, cl * 0.010f), new Vector3(8f, -25f, 0f), Vector3.one, chloroplastMat, hasCol: false);
                }

                var wL = MeshObject(grpLowerEpi, "Thành trong dày (Trái)", leftInnerWallMesh,
                    c + new Vector3(-0.009f, 0f, 0f), Vector3.zero, Vector3.one, guardThickWallMat, hasCol: false).transform;
                var wR = MeshObject(grpLowerEpi, "Thành trong dày (Phải)", rightInnerWallMesh,
                    c + new Vector3( 0.009f, 0f, 0f), Vector3.zero, Vector3.one, guardThickWallMat, hasCol: false).transform;
                var pore = Part(PrimitiveType.Cube, grpLowerEpi, "Khe khí khổng",
                    c, new Vector3(0.016f, 0.015f, 0.036f), Vector3.zero, airChamberMat, hasCol: false).transform;

                stomaRigs.Add(new StomaRig
                {
                    leftCell = gL.transform,
                    rightCell = gR.transform,
                    leftThickWall = wL,
                    rightThickWall = wR,
                    poreVoid = pore,
                    centerPos = c
                });
            }
            RegisterAnchor("la_khikhong_trai", grpLowerEpi, new Vector3(-0.105f, -0.010f, -0.108f));
            RegisterAnchor("la_khikhong_phai", grpLowerEpi, new Vector3( 0.105f, -0.010f, -0.108f));
            RegisterAnchor("la_ngoai_kk_trai", grpLowerEpi, new Vector3(-0.105f, -0.072f, -0.108f));
            RegisterAnchor("la_ngoai_kk_phai", grpLowerEpi, new Vector3( 0.105f, -0.072f, -0.108f));
            AddPin(KhungNhinThucVat.LatCatLa, "la_khi_khong", "Khí khổng (2 tế bào hạt đậu)", grpLowerEpi, new Vector3(0.105f, -0.014f, -0.108f), new Color(0.95f, 0.78f, 0.16f));
        }

        // ---------------------------------------------------------------------
        // Hạt biểu diễn tinh tế (kích thước micro 4.5-5.5mm để không che khuất giải phẫu)
        // ---------------------------------------------------------------------
        void BuildParticlePools()
        {
            particleRoot = Group("Hệ hạt sinh lý", transform, Vector3.zero);
            waterPool = AllocPool(particleRoot, "HatNuoc", 26, 0.0052f, waterPtMat);
            waterProgress = new float[waterPool.Length];
            for (int i = 0; i < waterProgress.Length; i++) waterProgress[i] = waterPool[i].phase;

            sugarPool = AllocPool(particleRoot, "HatDuong", 20, 0.0050f, sugarPtMat);
            co2Pool = AllocPool(particleRoot, "HatCO2", 16, 0.0046f, co2PtMat);
            o2Pool = AllocPool(particleRoot, "HatO2", 16, 0.0050f, o2PtMat);
            auxinPool = AllocPool(particleRoot, "HatAuxin", 20, 0.0044f, auxinPtMat);
        }

        HatDong[] AllocPool(Transform parent, string name, int count, float size, Material mat)
        {
            var arr = new HatDong[count];
            for (int i = 0; i < count; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = name;
                go.layer = StageLayer;
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);
                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                go.transform.SetParent(parent, false);
                go.transform.localScale = Vector3.one * size;
                go.SetActive(false);
                arr[i] = new HatDong { tr = go.transform, phase = i / (float)count, lane = i % 4 };
            }
            return arr;
        }

        // ---------------------------------------------------------------------
        // BỘ SINH LƯỚI 3D HỮU CƠ LIỀN MẠCH (PROCEDURAL ORGANIC MESH ENGINE)
        // ---------------------------------------------------------------------

        /// <summary>
        /// Tạo bề mặt đất mùn hữu cơ gồ ghề tự nhiên dạng cung tròn (grid đồng tâm, hoàn toàn không tạo sọc quạt tam giác).
        /// </summary>
        Mesh BuildOrganicSoilSectorMound(string name, float radius, float yBase, float startDeg, float endDeg, int angSegs, int radSegs)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            // Lưới cực đều từ rMin nhỏ đến radius với độ nhấp nhô tự nhiên của hạt mùn đất
            float rMin = 0.006f;
            for (int r = 0; r <= radSegs; r++)
            {
                float fr = r / (float)radSegs;
                float rad = Mathf.Lerp(rMin, radius, fr);
                for (int s = 0; s <= angSegs; s++)
                {
                    float fu = s / (float)angSegs;
                    float ang = Mathf.Lerp(startDeg, endDeg, fu) * Mathf.Deg2Rad;
                    float x = Mathf.Sin(ang) * rad;
                    float z = Mathf.Cos(ang) * rad;
                    // Mô đất hơi vồng tự nhiên ở gốc thân + hạt mùn gồ ghề nhẹ
                    float dome = (1f - fr * fr) * 0.007f;
                    float clod = (Mathf.Sin(x * 85f + z * 45f) * Mathf.Cos(z * 92f - x * 35f)) * 0.0022f * Mathf.Sin(fr * Mathf.PI);
                    verts.Add(new Vector3(x, yBase + dome + clod, z));
                    uvs.Add(new Vector2(0.5f + x / (radius * 2f), 0.5f + z / (radius * 2f)));
                }
            }

            int stride = angSegs + 1;
            for (int r = 0; r < radSegs; r++)
            {
                for (int s = 0; s < angSegs; s++)
                {
                    int a = r * stride + s;
                    int b = a + 1;
                    int c = a + stride;
                    int d = c + 1;
                    // Pháp tuyến hướng lên +Y trong hệ tọa độ tay trái Unity
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            }

            return FinalizeMesh(name, verts, uvs, tris);
        }

        /// <summary>
        /// Tạo mặt đĩa/vành phẳng hướng lên +Y với UV phẳng chuẩn (0..1) ánh xạ bản đồ mô học 384x384 sắc nét.
        /// </summary>
        Mesh BuildPlanarRingSectorMesh(string name, float rIn, float rOut, float yTop, float startDeg, float endDeg, int angSegs, float outerRadiusForUV)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            float r0 = Mathf.Max(0f, rIn);
            for (int s = 0; s <= angSegs; s++)
            {
                float uFrac = s / (float)angSegs;
                float ang = Mathf.Lerp(startDeg, endDeg, uFrac) * Mathf.Deg2Rad;
                float sinA = Mathf.Sin(ang);
                float cosA = Mathf.Cos(ang);

                Vector3 pOut = new Vector3(sinA * rOut, yTop, cosA * rOut);
                Vector3 pIn = new Vector3(sinA * r0, yTop, cosA * r0);
                verts.Add(pOut);
                uvs.Add(new Vector2(0.5f + pOut.x / (outerRadiusForUV * 2f), 0.5f + pOut.z / (outerRadiusForUV * 2f)));
                verts.Add(pIn);
                uvs.Add(new Vector2(0.5f + pIn.x / (outerRadiusForUV * 2f), 0.5f + pIn.z / (outerRadiusForUV * 2f)));
            }

            for (int s = 0; s < angSegs; s++)
            {
                int a = s * 2;     // Out(s)
                int b = a + 1;     // In(s)
                int c = a + 2;     // Out(s+1)
                int d = a + 3;     // In(s+1)
                // Cross(OutNext - Out, In - Out) = +Y (hướng lên trên)
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }

            return FinalizeMesh(name, verts, uvs, tris);
        }

        /// <summary>
        /// Tạo 2 mặt cắt đứng phẳng (tách đỉnh độc lập để normal hoàn toàn phẳng, sắc nét) cho thành chậu hoặc vách cắt lát cắt.
        /// </summary>
        Mesh BuildLatheCutWallMesh(string name, Vector2[] profile, float startDeg, float endDeg)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            void AddWall(float deg, bool flip)
            {
                float rad = deg * Mathf.Deg2Rad;
                float sinA = Mathf.Sin(rad);
                float cosA = Mathf.Cos(rad);
                if (profile.Length == 4)
                {
                    int b = verts.Count;
                    for (int p = 0; p < 4; p++)
                    {
                        verts.Add(new Vector3(sinA * profile[p].x, profile[p].y, cosA * profile[p].x));
                        uvs.Add(new Vector2(profile[p].x * 5f, profile[p].y * 6f));
                    }
                    if (!flip) { tris.Add(b); tris.Add(b + 1); tris.Add(b + 2); tris.Add(b); tris.Add(b + 2); tris.Add(b + 3); }
                    else { tris.Add(b); tris.Add(b + 2); tris.Add(b + 1); tris.Add(b); tris.Add(b + 3); tris.Add(b + 2); }
                }
                else if (profile.Length >= 8)
                {
                    int half = profile.Length / 2;
                    for (int i = 0; i < half - 1; i++)
                    {
                        Vector2 o0 = profile[i], o1 = profile[i + 1];
                        Vector2 i0 = profile[profile.Length - 1 - i], i1 = profile[profile.Length - 2 - i];
                        int b = verts.Count;
                        verts.Add(new Vector3(sinA * o0.x, o0.y, cosA * o0.x)); uvs.Add(new Vector2(o0.x * 4f, o0.y * 4f));
                        verts.Add(new Vector3(sinA * o1.x, o1.y, cosA * o1.x)); uvs.Add(new Vector2(o1.x * 4f, o1.y * 4f));
                        verts.Add(new Vector3(sinA * i1.x, i1.y, cosA * i1.x)); uvs.Add(new Vector2(i1.x * 4f, i1.y * 4f));
                        verts.Add(new Vector3(sinA * i0.x, i0.y, cosA * i0.x)); uvs.Add(new Vector2(i0.x * 4f, i0.y * 4f));
                        if (!flip) { tris.Add(b); tris.Add(b + 2); tris.Add(b + 1); tris.Add(b); tris.Add(b + 3); tris.Add(b + 2); }
                        else { tris.Add(b); tris.Add(b + 1); tris.Add(b + 2); tris.Add(b); tris.Add(b + 2); tris.Add(b + 3); }
                    }
                }
            }

            AddWall(startDeg, flip: false);
            AddWall(endDeg, flip: true);
            return FinalizeMesh(name, verts, uvs, tris);
        }

        /// <summary>
        /// Tạo 2 mặt cắt địa tầng đất (UV dọc Y từ 0..1 để hiện đúng tầng đất mặt giàu mùn -> đất thịt -> sỏi thoát nước).
        /// </summary>
        Mesh BuildSoilCutawayWalls(string name, Vector2[] profile, float startDeg, float endDeg)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            float yMin = -0.336f, yMax = -0.014f;

            void AddPlane(float deg, bool flip)
            {
                float rad = deg * Mathf.Deg2Rad;
                float sinA = Mathf.Sin(rad);
                float cosA = Mathf.Cos(rad);
                int b = verts.Count;
                for (int p = 0; p < 4; p++)
                {
                    verts.Add(new Vector3(sinA * profile[p].x, profile[p].y, cosA * profile[p].x));
                    float u = profile[p].x / 0.145f;
                    float v = Mathf.InverseLerp(yMin, yMax, profile[p].y);
                    uvs.Add(new Vector2(u, v));
                }
                if (!flip) { tris.Add(b); tris.Add(b + 1); tris.Add(b + 2); tris.Add(b); tris.Add(b + 2); tris.Add(b + 3); }
                else { tris.Add(b); tris.Add(b + 2); tris.Add(b + 1); tris.Add(b); tris.Add(b + 3); tris.Add(b + 2); }
            }

            AddPlane(startDeg, flip: false);
            AddPlane(endDeg, flip: true);
            return FinalizeMesh(name, verts, uvs, tris);
        }

        Mesh BuildSplineTubeMesh(string meshName, Vector3[] controlPts, float[] controlRadii, int radialSegs)
        {
            int subPerSeg = 4;
            int totalRings = (controlPts.Length - 1) * subPerSeg + 1;
            var ringCenters = new Vector3[totalRings];
            var ringRadii = new float[totalRings];

            int rIdx = 0;
            for (int seg = 0; seg < controlPts.Length - 1; seg++)
            {
                Vector3 p0 = controlPts[Mathf.Max(0, seg - 1)];
                Vector3 p1 = controlPts[seg];
                Vector3 p2 = controlPts[seg + 1];
                Vector3 p3 = controlPts[Mathf.Min(controlPts.Length - 1, seg + 2)];
                float rad1 = controlRadii[seg];
                float rad2 = controlRadii[seg + 1];

                int steps = (seg == controlPts.Length - 2) ? subPerSeg + 1 : subPerSeg;
                for (int s = 0; s < steps; s++)
                {
                    float t = s / (float)subPerSeg;
                    ringCenters[rIdx] = EvalCatmullRom(p0, p1, p2, p3, t);
                    ringRadii[rIdx] = Mathf.Lerp(rad1, rad2, Mathf.SmoothStep(0f, 1f, t));
                    rIdx++;
                }
            }

            var verts = new List<Vector3>(totalRings * (radialSegs + 1) + 2);
            var uvs = new List<Vector2>(totalRings * (radialSegs + 1) + 2);
            var tris = new List<int>(totalRings * radialSegs * 6);

            Vector3 prevRight = Vector3.right;
            for (int i = 0; i < totalRings; i++)
            {
                Vector3 tangent;
                if (i == 0) tangent = (ringCenters[1] - ringCenters[0]).normalized;
                else if (i == totalRings - 1) tangent = (ringCenters[i] - ringCenters[i - 1]).normalized;
                else tangent = (ringCenters[i + 1] - ringCenters[i - 1]).normalized;
                if (tangent.sqrMagnitude < 0.0001f) tangent = Vector3.up;

                Vector3 refUp = Mathf.Abs(Vector3.Dot(tangent, prevRight)) < 0.92f ? prevRight : Vector3.forward;
                Vector3 binormal = Vector3.Cross(tangent, refUp).normalized;
                Vector3 normal = Vector3.Cross(binormal, tangent).normalized;
                prevRight = normal;

                float vCoord = i / (float)(totalRings - 1);
                float rad = ringRadii[i];
                for (int j = 0; j <= radialSegs; j++)
                {
                    float uCoord = j / (float)radialSegs;
                    float ang = uCoord * Mathf.PI * 2f;
                    Vector3 offset = (normal * Mathf.Cos(ang) + binormal * Mathf.Sin(ang)) * rad;
                    verts.Add(ringCenters[i] + offset);
                    uvs.Add(new Vector2(uCoord, vCoord));
                }
            }

            int stride = radialSegs + 1;
            for (int i = 0; i < totalRings - 1; i++)
            {
                for (int j = 0; j < radialSegs; j++)
                {
                    int a = i * stride + j;
                    int b = a + 1;
                    int c = a + stride;
                    int d = c + 1;
                    // Pháp tuyến hướng RA NGOÀI ống trong hệ tay trái Unity
                    tris.Add(a); tris.Add(b); tris.Add(c);
                    tris.Add(b); tris.Add(d); tris.Add(c);
                }
            }

            int botCenterIdx = verts.Count;
            verts.Add(ringCenters[0]); uvs.Add(new Vector2(0.5f, 0f));
            for (int j = 0; j < radialSegs; j++) { tris.Add(botCenterIdx); tris.Add(j + 1); tris.Add(j); }

            int topCenterIdx = verts.Count;
            int lastRingStart = (totalRings - 1) * stride;
            verts.Add(ringCenters[totalRings - 1]); uvs.Add(new Vector2(0.5f, 1f));
            for (int j = 0; j < radialSegs; j++) { tris.Add(topCenterIdx); tris.Add(lastRingStart + j); tris.Add(lastRingStart + j + 1); }

            return FinalizeMesh(meshName, verts, uvs, tris);
        }

        static Vector3 EvalCatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        Mesh BuildLatheSectorMesh(string meshName, Vector2[] profile, float startDeg, float endDeg, int angularSegs,
                                  bool capEnds, bool planarTopUV = false, float outerRadiusForUV = 0.22f)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            int pCount = profile.Length;
            // Tách mặt đỉnh phẳng khi planarTopUV = true để mặt cắt ngang có normal thẳng đứng sắc nét, không bị cong bóng viền!
            int bodyEnd = (planarTopUV && pCount == 4) ? pCount - 1 : pCount;

            for (int s = 0; s <= angularSegs; s++)
            {
                float uFrac = s / (float)angularSegs;
                float ang = Mathf.Lerp(startDeg, endDeg, uFrac) * Mathf.Deg2Rad;
                float sinA = Mathf.Sin(ang);
                float cosA = Mathf.Cos(ang);

                for (int p = 0; p < bodyEnd; p++)
                {
                    float r = profile[p].x;
                    float y = profile[p].y;
                    verts.Add(new Vector3(sinA * r, y, cosA * r));
                    uvs.Add(new Vector2(uFrac * 2f, p / (float)Mathf.Max(1, bodyEnd - 1)));
                }
            }

            for (int s = 0; s < angularSegs; s++)
            {
                int rowA = s * bodyEnd;
                int rowB = (s + 1) * bodyEnd;
                for (int p = 0; p < bodyEnd - 1; p++)
                {
                    int a = rowA + p, b = a + 1, c = rowB + p, d = c + 1;
                    // Pháp tuyến hướng RA NGOÀI biên dạng xoay tiện trong hệ tay trái Unity
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            }

            // Nếu planarTopUV == true: dựng riêng mặt cắt ngang đỉnh phẳng hướng +Y với UV ánh xạ lên bản đồ mô học 384x384
            if (planarTopUV && pCount == 4)
            {
                int topStart = verts.Count;
                float rIn = profile[3].x;
                float rOut = profile[2].x;
                float yTop = profile[2].y;
                for (int s = 0; s <= angularSegs; s++)
                {
                    float uFrac = s / (float)angularSegs;
                    float ang = Mathf.Lerp(startDeg, endDeg, uFrac) * Mathf.Deg2Rad;
                    float sinA = Mathf.Sin(ang);
                    float cosA = Mathf.Cos(ang);

                    Vector3 pOut = new Vector3(sinA * rOut, yTop, cosA * rOut);
                    Vector3 pIn = new Vector3(sinA * rIn, yTop, cosA * rIn);
                    verts.Add(pOut);
                    uvs.Add(new Vector2(0.5f + pOut.x / (outerRadiusForUV * 2f), 0.5f + pOut.z / (outerRadiusForUV * 2f)));
                    verts.Add(pIn);
                    uvs.Add(new Vector2(0.5f + pIn.x / (outerRadiusForUV * 2f), 0.5f + pIn.z / (outerRadiusForUV * 2f)));
                }
                for (int s = 0; s < angularSegs; s++)
                {
                    int a = topStart + s * 2;
                    int b = a + 1;
                    int c = a + 2;
                    int d = a + 3;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            }

            if (capEnds && pCount == 4 && Mathf.Abs((endDeg - startDeg) - 360f) > 1f)
            {
                AddLatheEndCap(verts, uvs, tris, profile, startDeg * Mathf.Deg2Rad, flipWinding: false);
                AddLatheEndCap(verts, uvs, tris, profile, endDeg * Mathf.Deg2Rad, flipWinding: true);
            }

            return FinalizeMesh(meshName, verts, uvs, tris);
        }

        static void AddLatheEndCap(List<Vector3> verts, List<Vector2> uvs, List<int> tris, Vector2[] profile, float rad, bool flipWinding)
        {
            int baseIdx = verts.Count;
            float sinA = Mathf.Sin(rad);
            float cosA = Mathf.Cos(rad);
            for (int p = 0; p < 4; p++)
            {
                verts.Add(new Vector3(sinA * profile[p].x, profile[p].y, cosA * profile[p].x));
                uvs.Add(new Vector2(profile[p].x * 4f, profile[p].y * 8f));
            }
            if (!flipWinding)
            {
                tris.Add(baseIdx + 0); tris.Add(baseIdx + 1); tris.Add(baseIdx + 2);
                tris.Add(baseIdx + 0); tris.Add(baseIdx + 2); tris.Add(baseIdx + 3);
            }
            else
            {
                tris.Add(baseIdx + 0); tris.Add(baseIdx + 2); tris.Add(baseIdx + 1);
                tris.Add(baseIdx + 0); tris.Add(baseIdx + 3); tris.Add(baseIdx + 2);
            }
        }

        Mesh BuildHollowVesselMesh(string name, float rInner, float rOuter, float yBot, float yTop, int segs)
        {
            Vector2[] prof =
            {
                new Vector2(rInner, yBot),
                new Vector2(rOuter, yBot),
                new Vector2(rOuter, yTop),
                new Vector2(rInner, yTop),
                new Vector2(rInner, yBot)
            };
            return BuildLatheSectorMesh(name, prof, -180f, 180f, segs, capEnds: false);
        }

        Mesh BuildFlutedStarPrismMesh(string name, int arms, float rArm, float rValley, float yBot, float yTop)
        {
            int segs = 48;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            for (int ring = 0; ring < 2; ring++)
            {
                float y = ring == 0 ? yBot : yTop;
                for (int s = 0; s <= segs; s++)
                {
                    float u = s / (float)segs;
                    float ang = u * Mathf.PI * 2f;
                    float star = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(ang * arms), 1.6f);
                    float r = Mathf.Lerp(rValley, rArm, star);
                    verts.Add(new Vector3(Mathf.Cos(ang) * r, y, Mathf.Sin(ang) * r));
                    uvs.Add(new Vector2(u * 2f, ring));
                }
            }

            int stride = segs + 1;
            for (int s = 0; s < segs; s++)
            {
                int a = s, b = s + 1, c = stride + s, d = stride + s + 1;
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }

            int topCenter = verts.Count;
            verts.Add(new Vector3(0f, yTop, 0f)); uvs.Add(new Vector2(0.5f, 0.5f));
            for (int s = 0; s < segs; s++)
            {
                int p0 = verts.Count;
                verts.Add(verts[stride + s]);
                verts.Add(verts[stride + s + 1]);
                uvs.Add(new Vector2(0.5f, 1f));
                uvs.Add(new Vector2(1f, 0.5f));
                tris.Add(topCenter); tris.Add(p0 + 1); tris.Add(p0);
            }

            return FinalizeMesh(name, verts, uvs, tris);
        }

        Mesh BuildCorrugatedCaspariRingMesh(float radius, float halfWidth, float topY, int cellCount)
        {
            int segs = cellCount * 4;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            float yBot = 0.004f;
            for (int ring = 0; ring < 3; ring++)
            {
                float y = ring == 0 ? yBot : topY;
                float rOff = ring == 1 ? -halfWidth : halfWidth;
                for (int s = 0; s <= segs; s++)
                {
                    float u = s / (float)segs;
                    float ang = u * Mathf.PI * 2f;
                    float ripple = Mathf.Cos(ang * cellCount) * 0.0026f;
                    float r = radius + rOff + ripple;
                    verts.Add(new Vector3(Mathf.Cos(ang) * r, y, Mathf.Sin(ang) * r));
                    uvs.Add(new Vector2(u * 4f, ring * 0.5f));
                }
            }

            int stride = segs + 1;
            for (int layer = 0; layer < 2; layer++)
            {
                int baseA = layer * stride, baseB = (layer + 1) * stride;
                for (int s = 0; s < segs; s++)
                {
                    int a = baseA + s, b = a + 1, c = baseB + s, d = c + 1;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            }
            return FinalizeMesh("DaiCaspariSong", verts, uvs, tris);
        }

        Mesh BuildRoundedBundleMesh(string name, float rx, float rz, float yBot, float yTop, int segs)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            float[] ys = { yBot, yTop - 0.0025f, yTop };
            float[] scales = { 1.0f, 1.0f, 0.90f };

            for (int r = 0; r < ys.Length; r++)
            {
                for (int s = 0; s <= segs; s++)
                {
                    float u = s / (float)segs;
                    float ang = u * Mathf.PI * 2f;
                    verts.Add(new Vector3(Mathf.Cos(ang) * rx * scales[r], ys[r], Mathf.Sin(ang) * rz * scales[r]));
                    uvs.Add(new Vector2(u, r * 0.5f));
                }
            }

            int stride = segs + 1;
            for (int r = 0; r < ys.Length - 1; r++)
            {
                int bA = r * stride, bB = (r + 1) * stride;
                for (int s = 0; s < segs; s++)
                {
                    tris.Add(bA + s); tris.Add(bB + s); tris.Add(bA + s + 1);
                    tris.Add(bA + s + 1); tris.Add(bB + s); tris.Add(bB + s + 1);
                }
            }

            int topCenter = verts.Count;
            verts.Add(new Vector3(0f, yTop, 0f)); uvs.Add(new Vector2(0.5f, 0.5f));
            int topRing = (ys.Length - 1) * stride;
            for (int s = 0; s < segs; s++) { tris.Add(topCenter); tris.Add(topRing + s); tris.Add(topRing + s + 1); }

            return FinalizeMesh(name, verts, uvs, tris);
        }

        Mesh BuildHelixSpiralMesh(string name, float helixRadius, float yStart, float yEnd, float turns, float wireRadius)
        {
            int ptsCount = 22;
            var pts = new Vector3[ptsCount];
            var rads = new float[ptsCount];
            for (int i = 0; i < ptsCount; i++)
            {
                float t = i / (float)(ptsCount - 1);
                float ang = t * turns * Mathf.PI * 2f;
                pts[i] = new Vector3(Mathf.Cos(ang) * helixRadius, Mathf.Lerp(yStart, yEnd, t), Mathf.Sin(ang) * helixRadius);
                rads[i] = wireRadius;
            }
            return BuildSplineTubeMesh(name, pts, rads, 6);
        }

        Mesh BuildCellularPillowSheetMesh(string name, int cellsX, int cellsZ, float width, float depth, float thickness, float domeHeight, bool stepCutFrontCorner)
        {
            int sub = 4;
            int gx = cellsX * sub;
            int gz = cellsZ * sub;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            for (int face = 0; face < 2; face++)
            {
                for (int iz = 0; iz <= gz; iz++)
                {
                    float fz = iz / (float)gz;
                    float z = (fz - 0.5f) * depth;
                    float pillowZ = Mathf.Sin(((fz * cellsZ) % 1f) * Mathf.PI);

                    for (int ix = 0; ix <= gx; ix++)
                    {
                        float fx = ix / (float)gx;
                        float x = (fx - 0.5f) * width;
                        float pillowX = Mathf.Sin(((fx * cellsX) % 1f) * Mathf.PI);
                        float dome = Mathf.Pow(pillowX * pillowZ, 0.55f) * domeHeight;

                        bool cutStep = stepCutFrontCorner && fx > 0.62f && fz < 0.36f;
                        float y = face == 0 ? (cutStep ? -thickness * 0.35f : dome) : -thickness;

                        verts.Add(new Vector3(x, y, z));
                        uvs.Add(new Vector2(fx, fz));
                    }
                }
            }

            int stride = gx + 1;
            int faceOffset = (gz + 1) * stride;
            for (int iz = 0; iz < gz; iz++)
            {
                for (int ix = 0; ix < gx; ix++)
                {
                    int a = iz * stride + ix, b = a + 1, c = a + stride, d = c + 1;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);

                    int a2 = faceOffset + a, b2 = faceOffset + b, c2 = faceOffset + c, d2 = faceOffset + d;
                    tris.Add(a2); tris.Add(b2); tris.Add(c2);
                    tris.Add(b2); tris.Add(d2); tris.Add(c2);
                }
            }

            for (int ix = 0; ix < gx; ix++)
            {
                int t0 = ix, t1 = ix + 1, b0 = faceOffset + ix, b1 = faceOffset + ix + 1;
                tris.Add(t0); tris.Add(t1); tris.Add(b0); tris.Add(t1); tris.Add(b1); tris.Add(b0);

                int rowLast = gz * stride;
                int t2 = rowLast + ix, t3 = rowLast + ix + 1, b2 = faceOffset + t2, b3 = faceOffset + t3;
                tris.Add(t2); tris.Add(b2); tris.Add(t3); tris.Add(t3); tris.Add(b2); tris.Add(b3);
            }
            for (int iz = 0; iz < gz; iz++)
            {
                int t0 = iz * stride, t1 = (iz + 1) * stride, b0 = faceOffset + t0, b1 = faceOffset + t1;
                tris.Add(t0); tris.Add(b0); tris.Add(t1); tris.Add(t1); tris.Add(b0); tris.Add(b1);

                int t2 = iz * stride + gx, t3 = (iz + 1) * stride + gx, b2 = faceOffset + t2, b3 = faceOffset + t3;
                tris.Add(t2); tris.Add(t3); tris.Add(b2); tris.Add(t3); tris.Add(b3); tris.Add(b2);
            }

            return FinalizeMesh(name, verts, uvs, tris);
        }

        Mesh BuildKidneyGuardCellMesh(string name, bool isLeft, out Mesh innerThickWallMesh)
        {
            float sign = isLeft ? -1f : 1f;
            int nPts = 9;
            var pts = new Vector3[nPts];
            var rads = new float[nPts];
            var wallPts = new Vector3[nPts];
            var wallRads = new float[nPts];

            for (int i = 0; i < nPts; i++)
            {
                float t = i / (float)(nPts - 1);
                float ang = Mathf.Lerp(-Mathf.PI * 0.46f, Mathf.PI * 0.46f, t);
                float archX = sign * (0.003f + Mathf.Cos(ang) * 0.013f);
                float archZ = Mathf.Sin(ang) * 0.025f;
                float r = Mathf.Lerp(0.0068f, 0.0105f, Mathf.Sin(t * Mathf.PI));

                pts[i] = new Vector3(archX, 0f, archZ);
                rads[i] = r;

                wallPts[i] = new Vector3(archX - sign * (r * 0.62f), 0f, archZ * 0.90f);
                wallRads[i] = r * 0.34f;
            }

            innerThickWallMesh = BuildSplineTubeMesh(name + "_ThanhDay", wallPts, wallRads, 10);
            return BuildSplineTubeMesh(name, pts, rads, 14);
        }

        Mesh BuildOrganicPalisadeCellMesh(float radius, float height)
        {
            float h = height * 0.5f;
            Vector3[] pts =
            {
                new Vector3(0f, -h, 0f), new Vector3(0f, -h * 0.68f, 0f),
                new Vector3(0f,  0f, 0f), new Vector3(0f,  h * 0.68f, 0f), new Vector3(0f, h, 0f)
            };
            float[] rads = { radius * 0.38f, radius * 0.96f, radius, radius * 0.96f, radius * 0.38f };
            return BuildSplineTubeMesh("TeBaoMoGiau", pts, rads, 12);
        }

        Mesh BuildChloroplastDiscMesh(float radius, float halfHeight)
        {
            Vector3[] pts = { new Vector3(0f, -halfHeight, 0f), Vector3.zero, new Vector3(0f, halfHeight, 0f) };
            return BuildSplineTubeMesh("HatLucLap", pts, new[] { radius * 0.3f, radius, radius * 0.3f }, 8);
        }

        Mesh BuildOrganicSpongyCellMesh(float baseRadius)
        {
            Vector3[] pts =
            {
                new Vector3(0f, -baseRadius * 0.68f, 0f), new Vector3(0f, -baseRadius * 0.36f, 0f),
                Vector3.zero, new Vector3(0f, baseRadius * 0.36f, 0f), new Vector3(0f, baseRadius * 0.68f, 0f)
            };
            float[] rads = { baseRadius * 0.35f, baseRadius * 0.88f, baseRadius, baseRadius * 0.88f, baseRadius * 0.35f };
            return BuildSplineTubeMesh("TeBaoMoXop", pts, rads, 12);
        }

        Mesh BuildOrganicNoduleMesh(string name, float radius, float seed)
        {
            Vector3[] pts = { new Vector3(-radius * 0.8f, 0f, 0f), new Vector3(0f, radius * 0.14f, 0f), new Vector3(radius * 0.8f, 0f, 0f) };
            var m = BuildSplineTubeMesh(name, pts, new[] { radius * 0.55f, radius, radius * 0.50f }, 12);
            var v = m.vertices;
            for (int i = 0; i < v.Length; i++)
                v[i] *= 1f + 0.12f * Mathf.Sin(v[i].x * 220f + seed) * Mathf.Cos(v[i].y * 220f);
            m.vertices = v;
            m.RecalculateNormals();
            return m;
        }

        Mesh BuildKidneySeedMesh()
        {
            Vector3[] pts = { new Vector3(-0.002f, -0.0055f, 0f), new Vector3(0.001f, 0f, 0f), new Vector3(-0.002f, 0.0055f, 0f) };
            return BuildSplineTubeMesh("HatDauNon", pts, new[] { 0.0026f, 0.0044f, 0.0026f }, 12);
        }

        Mesh BuildCurvedPetalMesh(string name, float width, float length, float archDepth, float cupCurl)
        {
            int nx = 8, ny = 8;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            for (int iy = 0; iy <= ny; iy++)
            {
                float v = iy / (float)ny;
                float halfW = Mathf.Sin(Mathf.Pow(v, 0.65f) * Mathf.PI) * (width * 0.5f);
                for (int ix = 0; ix <= nx; ix++)
                {
                    float u = (ix / (float)nx) * 2f - 1f;
                    verts.Add(new Vector3(u * halfW, v * length, Mathf.Sin(v * Mathf.PI) * archDepth + (u * u) * cupCurl * v));
                    uvs.Add(new Vector2(ix / (float)nx, v));
                }
            }

            int stride = nx + 1;
            for (int iy = 0; iy < ny; iy++)
            {
                for (int ix = 0; ix < nx; ix++)
                {
                    int a = iy * stride + ix, b = a + 1, c = a + stride, d = c + 1;
                    tris.Add(a); tris.Add(b); tris.Add(c); tris.Add(b); tris.Add(d); tris.Add(c);
                    tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(b); tris.Add(c); tris.Add(d);
                }
            }
            return FinalizeMesh(name, verts, uvs, tris);
        }

        Mesh BuildRootHairClusterMesh()
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            int hairCount = 56;
            for (int h = 0; h < hairCount; h++)
            {
                float ang = (h * 137.5f) * Mathf.Deg2Rad;
                float y0 = -0.172f - (h / (float)hairCount) * 0.118f;
                float rBase = Mathf.Lerp(0.0052f, 0.0022f, h / (float)hairCount);
                float len = Mathf.Lerp(0.030f, 0.015f, h / (float)hairCount);

                Vector3 dir = new Vector3(Mathf.Cos(ang), -0.14f, Mathf.Sin(ang)).normalized;
                Vector3 p0 = new Vector3(dir.x * rBase, y0, dir.z * rBase);
                Vector3 p1 = p0 + dir * (len * 0.55f) + new Vector3(0f, -0.002f, 0f);
                Vector3 p2 = p0 + dir * len + new Vector3(0f, -0.005f, 0f);

                AddMicroTubeToBuffers(verts, uvs, tris, p0, p1, p2, 0.00068f, 0.00022f);
            }
            return FinalizeMesh("CumLongHutRe", verts, uvs, tris);
        }

        static void AddMicroTubeToBuffers(List<Vector3> verts, List<Vector2> uvs, List<int> tris, Vector3 p0, Vector3 p1, Vector3 p2, float r0, float r2)
        {
            int baseIdx = verts.Count;
            Vector3[] pts = { p0, p1, p2 };
            float[] rads = { r0, (r0 + r2) * 0.55f, r2 };
            int segs = 5;

            for (int i = 0; i < 3; i++)
            {
                Vector3 t = i == 0 ? (p1 - p0).normalized : (p2 - p1).normalized;
                Vector3 n = Vector3.Cross(t, Vector3.up).normalized;
                if (n.sqrMagnitude < 0.01f) n = Vector3.right;
                Vector3 b = Vector3.Cross(t, n).normalized;

                for (int j = 0; j <= segs; j++)
                {
                    float a = (j / (float)segs) * Mathf.PI * 2f;
                    verts.Add(pts[i] + (n * Mathf.Cos(a) + b * Mathf.Sin(a)) * rads[i]);
                    uvs.Add(new Vector2(j / (float)segs, i * 0.5f));
                }
            }

            int stride = segs + 1;
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < segs; j++)
                {
                    int a = baseIdx + i * stride + j, b = a + 1, c = a + stride, d = c + 1;
                    tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(b); tris.Add(c); tris.Add(d);
                }
        }

        /// <summary>
        /// Lưới lá chét cây đậu 3D độ phân giải cao (32×18 mỗi mặt): phiến rộng hình tim-trứng mềm mại,
        /// gân chính & 7 cặp gân bên nổi khối tự nhiên cùng độ phồng thịt lá giữa các gân.
        /// </summary>
        Mesh BuildDetailedDicotLeafletMesh()
        {
            const int nx = 32, nz = 18;
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var vcols = new List<Color>();
            var tris = new List<int>();

            float len = 0.148f, maxHalfWidth = 0.072f;

            for (int face = 0; face < 2; face++)
            {
                float fSign = face == 0 ? 1f : -1f;
                for (int iz = 0; iz <= nz; iz++)
                {
                    float vz = (iz / (float)nz) * 2f - 1f;
                    float absV = Mathf.Abs(vz);

                    for (int ix = 0; ix <= nx; ix++)
                    {
                        float u = ix / (float)nx;

                        // Biên dạng rộng bản, bầu tròn gốc hình tim đặc trưng lá đậu Phaseolus vulgaris
                        float baseCurve = Mathf.Sin(Mathf.Pow(u, 0.54f) * Mathf.PI);
                        float acuminateTip = Mathf.Pow(1f - u * 0.36f, 1.25f);
                        float cordateNotch = u < 0.14f ? -Mathf.Exp(-absV * 4.5f) * (0.14f - u) * 0.09f : 0f;
                        float gentleMargin = Mathf.Sin(u * 36f) * 0.008f * Mathf.Sin(u * Mathf.PI);
                        float profileW = Mathf.Max(0.0005f, (baseCurve * acuminateTip + gentleMargin) * maxHalfWidth);

                        float x = (u + cordateNotch) * len;
                        float z = vz * profileW;

                        // Gân chính & 7 cặp gân bên uốn cung + độ phồng nhẹ giữa các khoang gân (bullate relief)
                        float midribMask = Mathf.Exp(-absV * 14f) * (1f - u * 0.50f);
                        float secPhase = u * 6.5f - Mathf.Pow(absV, 0.84f) * 2.1f;
                        float secVeinMask = Mathf.Pow(Mathf.Max(0f, Mathf.Cos(secPhase * Mathf.PI * 2f)), 10f)
                                            * Mathf.Sin(absV * Mathf.PI) * (1f - u * 0.35f);
                        float interveinalPuff = (1f - secVeinMask) * Mathf.Sin(absV * Mathf.PI) * Mathf.Sin(u * Mathf.PI) * 0.0014f;

                        // Độ khum lòng thuyền mềm & rủ nhẹ ở đầu lá
                        float cupCamber = Mathf.Sin(absV * Mathf.PI * 0.58f) * 0.0072f * Mathf.Sin(u * Mathf.PI);
                        float longitudinalArch = Mathf.Sin(u * Mathf.PI * 0.80f) * 0.0095f - (u * u * u) * 0.028f;
                        float marginWave = Mathf.Sin(u * 12f + vz * 2.2f) * 0.0016f * absV * Mathf.Sin(u * Mathf.PI);

                        float edgeTaper = Mathf.Sqrt(Mathf.Clamp01(1f - absV * absV));
                        float halfThick = (0.00055f * edgeTaper)
                                          + (face == 1 ? (midribMask * 0.0024f + secVeinMask * 0.0009f) : (-midribMask * 0.0009f - secVeinMask * 0.0004f + interveinalPuff));

                        float y = cupCamber + longitudinalArch + marginWave + fSign * halfThick;

                        verts.Add(new Vector3(x, y, z));
                        norms.Add(new Vector3(0f, fSign, 0f));
                        uvs.Add(new Vector2(u, vz * 0.5f + 0.5f));

                        float windWeight = u * u;
                        float youngTint = Mathf.Clamp01(u * 0.60f + (1f - absV) * 0.25f);
                        float surfaceTint = face == 0 ? (0.64f + 0.28f * (1f - absV)) : 0.96f;
                        vcols.Add(new Color(windWeight, youngTint, surfaceTint, 1f));
                    }
                }
            }

            int stride = nx + 1;
            int faceVertCount = (nz + 1) * stride;
            for (int iz = 0; iz < nz; iz++)
            {
                for (int ix = 0; ix < nx; ix++)
                {
                    int a = iz * stride + ix, b = a + 1, c = a + stride, d = c + 1;
                    tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(b); tris.Add(c); tris.Add(d);

                    int a2 = faceVertCount + a, b2 = faceVertCount + b, c2 = faceVertCount + c, d2 = faceVertCount + d;
                    tris.Add(a2); tris.Add(b2); tris.Add(c2); tris.Add(b2); tris.Add(d2); tris.Add(c2);
                }
            }

            var mesh = new Mesh { name = "LaCayHaiLaMamChiTiet" };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(vcols);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            createdMeshes.Add(mesh);
            return mesh;
        }

        Mesh FinalizeMesh(string name, List<Vector3> verts, List<Vector2> uvs, List<int> tris)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            createdMeshes.Add(mesh);
            return mesh;
        }

        // ---------------------------------------------------------------------
        // BỘ SINH TEXTURE PBR THỰC VẬT & BẢN ĐỒ VI THỂ MÔ HỌC (384×384)
        // ---------------------------------------------------------------------

        void BuildSoftBeamTexture()
        {
            const int W = 32, H = 64;
            softBeamTex = new Texture2D(W, H, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            var cols = new Color32[W * H];
            for (int y = 0; y < H; y++)
            {
                float v = y / (float)(H - 1); // 0 = đáy (mờ hẳn 0%), 1 = gần chụp đèn
                float fade = Mathf.Pow(v, 1.85f) * Mathf.Sin(Mathf.Clamp01(v) * Mathf.PI * 0.92f);
                byte a = (byte)Mathf.Clamp(Mathf.RoundToInt(fade * 255f), 0, 255);
                for (int x = 0; x < W; x++)
                    cols[y * W + x] = new Color32(255, 250, 210, a);
            }
            softBeamTex.SetPixels32(cols); softBeamTex.Apply(false, false);
            createdTextures.Add(softBeamTex);
        }

        void BuildSoilAndPotTextures()
        {
            const int S = 192;
            terracottaTex = new Texture2D(S, S, TextureFormat.RGBA32, true) { hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Repeat };
            terracottaNormalTex = new Texture2D(S, S, TextureFormat.RGBA32, true) { hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Repeat };
            soilTopTex = new Texture2D(S, S, TextureFormat.RGBA32, true) { hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Repeat };
            soilTopNormalTex = new Texture2D(S, S, TextureFormat.RGBA32, true) { hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Repeat };
            soilProfileTex = new Texture2D(S, S, TextureFormat.RGBA32, true) { hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };

            var tCols = new Color32[S * S];
            var tNrms = new Color32[S * S];
            var sCols = new Color32[S * S];
            var sNrms = new Color32[S * S];
            var pCols = new Color32[S * S];

            for (int y = 0; y < S; y++)
            {
                float v = y / (float)(S - 1);
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)(S - 1);
                    float hash = Hash2(x, y);
                    float hashMed = Hash2(x / 3, y / 3);

                    // Gốm đất nung có vòng xoay bàn gốm mịn & hạt khoáng nhỏ
                    float wheelRing = Mathf.Sin(v * Mathf.PI * 42f) * 0.035f;
                    float clay = Mathf.Clamp01(0.92f + wheelRing + (hash - 0.5f) * 0.08f);
                    byte tc = (byte)(clay * 255f);
                    tCols[y * S + x] = new Color32(tc, tc, tc, 255);
                    tNrms[y * S + x] = new Color32(128, (byte)((0.5f + Mathf.Cos(v * Mathf.PI * 42f) * 0.14f) * 255f), 248, 255);

                    // Đất mùn hữu cơ tơi xốp có hạt mùn + chấm perlite trắng khoáng nhỏ
                    float grain = hash * 0.55f + hashMed * 0.45f;
                    bool perlite = hash > 0.985f && hashMed > 0.72f;
                    Color sc = perlite ? new Color(0.76f, 0.72f, 0.64f) : Color.Lerp(new Color(0.55f, 0.46f, 0.38f), new Color(1.0f, 0.92f, 0.82f), grain);
                    sCols[y * S + x] = sc;
                    float snx = 0.5f + (Hash2(x + 1, y) - Hash2(x - 1, y)) * 0.35f;
                    float sny = 0.5f + (Hash2(x, y + 1) - Hash2(x, y - 1)) * 0.35f;
                    sNrms[y * S + x] = new Color32((byte)(Mathf.Clamp01(snx) * 255f), (byte)(Mathf.Clamp01(sny) * 255f), 232, 255);

                    // Địa tầng đất cắt dọc: tầng mặt giàu mùn tối -> tầng đất thịt nâu ấm -> tầng đáy thoáng ẩm
                    Color topHumus = new Color(0.18f, 0.13f, 0.09f);
                    Color midLoam = new Color(0.28f, 0.19f, 0.12f);
                    Color botSub = new Color(0.23f, 0.16f, 0.11f);
                    float waveLayer = Mathf.Sin(u * 11f) * 0.04f;
                    Color layerCol = v > 0.68f + waveLayer
                        ? Color.Lerp(midLoam, topHumus, Mathf.InverseLerp(0.68f + waveLayer, 0.92f, v))
                        : Color.Lerp(botSub, midLoam, Mathf.InverseLerp(0.08f, 0.68f + waveLayer, v));
                    layerCol *= (0.82f + grain * 0.36f);
                    if (perlite && v > 0.25f) layerCol = Color.Lerp(layerCol, new Color(0.55f, 0.48f, 0.38f), 0.55f);
                    pCols[y * S + x] = layerCol;
                }
            }

            terracottaTex.SetPixels32(tCols); terracottaTex.Apply(true, false);
            terracottaNormalTex.SetPixels32(tNrms); terracottaNormalTex.Apply(true, false);
            soilTopTex.SetPixels32(sCols); soilTopTex.Apply(true, false);
            soilTopNormalTex.SetPixels32(sNrms); soilTopNormalTex.Apply(true, false);
            soilProfileTex.SetPixels32(pCols); soilProfileTex.Apply(true, false);

            createdTextures.Add(terracottaTex); createdTextures.Add(terracottaNormalTex);
            createdTextures.Add(soilTopTex); createdTextures.Add(soilTopNormalTex); createdTextures.Add(soilProfileTex);
        }

        static float Hash2(int x, int y)
        {
            float n = Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f;
            return n - Mathf.Floor(n);
        }

        void BuildLeafTextures()
        {
            const int S = 384;
            leafTex = new Texture2D(S, S, TextureFormat.RGBA32, true) { hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            leafNormalTex = new Texture2D(S, S, TextureFormat.RGBA32, true) { hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };

            var cols = new Color32[S * S];
            var nrms = new Color32[S * S];
            float[] veinRoots = { 0.07f, 0.18f, 0.30f, 0.43f, 0.56f, 0.69f, 0.82f };

            for (int y = 0; y < S; y++)
            {
                float v = (y / (float)(S - 1)) * 2f - 1f;
                float absV = Mathf.Abs(v);
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)(S - 1);

                    float midWidth = Mathf.Lerp(0.026f, 0.005f, u);
                    float midrib = Mathf.Exp(-absV / midWidth) * (1f - u * 0.22f);

                    float secVein = 0f, secGradU = 0f;
                    for (int k = 0; k < veinRoots.Length; k++)
                    {
                        float u0 = veinRoots[k] + (v > 0f ? 0.014f : -0.014f);
                        float curveU = u0 + Mathf.Pow(absV, 1.22f) * 0.23f;
                        float dist = Mathf.Abs(u - curveU);
                        float w = Mathf.Lerp(0.012f, 0.0036f, absV);
                        float intensity = Mathf.Exp(-dist / w) * Mathf.Sin(Mathf.Clamp01(absV) * Mathf.PI) * (1f - absV * 0.32f);
                        if (intensity > secVein) { secVein = intensity; secGradU = Mathf.Sign(u - curveU) * intensity; }
                    }

                    EvalHexCellGrid(u * 20f + absV * 3f, v * 15f, out float netEdge1, out float ngx1, out float ngy1);
                    EvalHexCellGrid(u * 44f, v * 36f, out float netEdge2, out _, out _);
                    float tertVein = (1f - Mathf.SmoothStep(0.0f, 0.13f, netEdge1)) * 0.25f
                                   + (1f - Mathf.SmoothStep(0.0f, 0.11f, netEdge2)) * 0.12f;

                    float totalVein = Mathf.Clamp01(midrib * 0.92f + secVein * 0.68f + tertVein);
                    float mottle = 0.95f + 0.08f * Mathf.Sin(u * 23f + v * 17f) * Mathf.Cos(u * 13f - v * 29f);
                    Color mesophyllTint = Color.Lerp(new Color(0.75f, 0.90f, 0.70f), new Color(0.66f, 0.83f, 0.62f), absV * 0.55f) * mottle;
                    Color veinTint = new Color(0.96f, 1.0f, 0.82f);
                    Color finalCol = Color.Lerp(mesophyllTint, veinTint, totalVein * 0.54f);
                    finalCol.a = 1f;
                    cols[y * S + x] = finalCol;

                    float nx = 0.5f + Mathf.Sign(v) * (midrib * 0.28f) + ngx1 * 0.09f;
                    float ny = 0.5f + secGradU * 0.24f + ngy1 * 0.09f;
                    nrms[y * S + x] = new Color32((byte)(Mathf.Clamp01(nx) * 255f), (byte)(Mathf.Clamp01(ny) * 255f), 244, 255);
                }
            }

            leafTex.SetPixels32(cols); leafTex.Apply(true, false);
            leafNormalTex.SetPixels32(nrms); leafNormalTex.Apply(true, false);
            createdTextures.Add(leafTex); createdTextures.Add(leafNormalTex);
        }

        void BuildHistologyTextures()
        {
            // 1. Sọc sợi biểu bì thân & vỏ rễ (160x160)
            const int SB = 160;
            stemBarkTex = new Texture2D(SB, SB, TextureFormat.RGBA32, true) { hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Repeat };
            stemBarkNormalTex = new Texture2D(SB, SB, TextureFormat.RGBA32, true) { hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Repeat };
            cellBrickTex = new Texture2D(SB, SB, TextureFormat.RGBA32, true) { hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Repeat };
            cellBrickNormalTex = new Texture2D(SB, SB, TextureFormat.RGBA32, true) { hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Repeat };

            var bCols = new Color32[SB * SB]; var bNrms = new Color32[SB * SB];
            var cCols = new Color32[SB * SB]; var cNrms = new Color32[SB * SB];

            for (int y = 0; y < SB; y++)
            {
                float v = y / (float)SB;
                for (int x = 0; x < SB; x++)
                {
                    float u = x / (float)SB;
                    float rib = Mathf.Sin(u * Mathf.PI * 38f) * 0.5f + 0.5f;
                    float micro = Mathf.Sin(u * Mathf.PI * 116f + v * Mathf.PI * 18f) * 0.5f + 0.5f;
                    byte bc = (byte)Mathf.Clamp(Mathf.RoundToInt((0.86f + rib * 0.11f + micro * 0.05f) * 255f), 0, 255);
                    bCols[y * SB + x] = new Color32(bc, bc, bc, 255);
                    bNrms[y * SB + x] = new Color32((byte)((0.5f + Mathf.Cos(u * Mathf.PI * 38f) * 0.20f) * 255f), 128, 244, 255);

                    EvalHexCellGrid(u * 10f, v * 12f, out float edgeDist, out float gx, out float gy);
                    float wall = Mathf.SmoothStep(0.0f, 0.22f, edgeDist);
                    byte cc = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(0.66f, 1.04f, wall) * 255f), 0, 255);
                    cCols[y * SB + x] = new Color32(cc, cc, cc, 255);
                    cNrms[y * SB + x] = new Color32((byte)(Mathf.Clamp01(0.5f + gx * 0.24f) * 255f), (byte)(Mathf.Clamp01(0.5f + gy * 0.24f) * 255f), 240, 255);
                }
            }
            stemBarkTex.SetPixels32(bCols); stemBarkTex.Apply(true, false);
            stemBarkNormalTex.SetPixels32(bNrms); stemBarkNormalTex.Apply(true, false);
            cellBrickTex.SetPixels32(cCols); cellBrickTex.Apply(true, false);
            cellBrickNormalTex.SetPixels32(cNrms); cellBrickNormalTex.Apply(true, false);
            createdTextures.Add(stemBarkTex); createdTextures.Add(stemBarkNormalTex);
            createdTextures.Add(cellBrickTex); createdTextures.Add(cellBrickNormalTex);

            // 2. Bản đồ mô học 384×384 chuẩn kính hiển vi cho Lát cắt Rễ & Lát cắt Thân
            const int SH = 384;
            rootHistTex = new Texture2D(SH, SH, TextureFormat.RGBA32, true) { hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            rootHistNormalTex = new Texture2D(SH, SH, TextureFormat.RGBA32, true) { hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            stemHistTex = new Texture2D(SH, SH, TextureFormat.RGBA32, true) { hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            stemHistNormalTex = new Texture2D(SH, SH, TextureFormat.RGBA32, true) { hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };

            var rCols = new Color32[SH * SH]; var rNrms = new Color32[SH * SH];
            var sCols = new Color32[SH * SH]; var sNrms = new Color32[SH * SH];

            for (int y = 0; y < SH; y++)
            {
                float dy = (y / (float)(SH - 1)) * 2f - 1f;
                for (int x = 0; x < SH; x++)
                {
                    float dx = (x / (float)(SH - 1)) * 2f - 1f;
                    float rad = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Atan2(dy, dx);

                    // --- LÁT CẮT RỄ: Nhu mô vỏ đa giác xếp vòng đồng tâm rõ vách xenlulôzơ, khe gian bào tối & không bào ---
                    EvalHexCellGrid(dx * 13.5f, dy * 13.5f, out float rHexEdge, out float rgx, out float rgy);
                    float rWallT = Mathf.SmoothStep(0.0f, 0.20f, rHexEdge);
                    Color rWallCol = new Color(0.42f, 0.29f, 0.16f);
                    Color rCytoCol = new Color(0.84f, 0.72f, 0.50f);
                    Color rVacCol = new Color(0.68f, 0.76f, 0.64f);
                    Color rootCol;
                    if (rad > 0.90f)
                        rootCol = Color.Lerp(new Color(0.38f, 0.25f, 0.14f), new Color(0.66f, 0.48f, 0.30f), rWallT); // Biểu bì
                    else if (rad > 0.45f)
                        rootCol = Color.Lerp(rWallCol, Color.Lerp(rCytoCol, rVacCol, Mathf.SmoothStep(0.32f, 0.55f, rHexEdge)), rWallT); // Mô mềm vỏ
                    else if (rad > 0.40f)
                        rootCol = Color.Lerp(new Color(0.76f, 0.20f, 0.12f), new Color(0.78f, 0.60f, 0.32f), rWallT); // Nội bì + Đai Caspari
                    else
                        rootCol = Color.Lerp(new Color(0.28f, 0.44f, 0.24f), new Color(0.56f, 0.72f, 0.46f), rWallT); // Trung trụ

                    rCols[y * SH + x] = rootCol;
                    rNrms[y * SH + x] = new Color32((byte)(Mathf.Clamp01(0.5f + rgx * 0.28f) * 255f), (byte)(Mathf.Clamp01(0.5f + rgy * 0.28f) * 255f), 238, 255);

                    // --- LÁT CẮT THÂN: Mô mềm ruột tổ ong rộng sáng ở tâm + Tia ruột + Vỏ lục lạp ngoài ---
                    float cellScale = rad < 0.52f ? 10.5f : 15.0f;
                    EvalHexCellGrid(dx * cellScale, dy * cellScale, out float sHexEdge, out float sgx, out float sgy);
                    float sWallT = Mathf.SmoothStep(0.0f, 0.21f, sHexEdge);
                    Color pithWall = new Color(0.36f, 0.50f, 0.28f);
                    Color pithLumen = new Color(0.72f, 0.82f, 0.58f);
                    Color cortexWall = new Color(0.18f, 0.38f, 0.16f);
                    Color cortexLumen = new Color(0.36f, 0.62f, 0.28f);
                    Color stemCol = rad < 0.54f
                        ? Color.Lerp(pithWall, pithLumen, sWallT)
                        : Color.Lerp(cortexWall, cortexLumen, sWallT);

                    // Dải tia ruột hướng tâm nhẹ giữa 10 bó mạch
                    float rayGlow = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(ang * 10f), 6f) * Mathf.SmoothStep(0.45f, 0.62f, rad) * (1f - Mathf.SmoothStep(0.78f, 0.88f, rad));
                    stemCol = Color.Lerp(stemCol, new Color(0.68f, 0.78f, 0.52f), rayGlow * 0.35f);

                    sCols[y * SH + x] = stemCol;
                    sNrms[y * SH + x] = new Color32((byte)(Mathf.Clamp01(0.5f + sgx * 0.28f) * 255f), (byte)(Mathf.Clamp01(0.5f + sgy * 0.28f) * 255f), 238, 255);
                }
            }

            rootHistTex.SetPixels32(rCols); rootHistTex.Apply(true, false);
            rootHistNormalTex.SetPixels32(rNrms); rootHistNormalTex.Apply(true, false);
            stemHistTex.SetPixels32(sCols); stemHistTex.Apply(true, false);
            stemHistNormalTex.SetPixels32(sNrms); stemHistNormalTex.Apply(true, false);
            createdTextures.Add(rootHistTex); createdTextures.Add(rootHistNormalTex);
            createdTextures.Add(stemHistTex); createdTextures.Add(stemHistNormalTex);

            // 3. Bản đồ mặt trên Lá (Tế bào biểu bì hình mảnh ghép jigsaw) & Mặt cắt lõi thịt lá (Mô giậu + Mô xốp + Khoang khí tối)
            const int SL = 256;
            leafUpperTex = new Texture2D(SL, SL, TextureFormat.RGBA32, true) { hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Repeat };
            leafUpperNormalTex = new Texture2D(SL, SL, TextureFormat.RGBA32, true) { hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Repeat };
            leafCutFaceTex = new Texture2D(SL, SL, TextureFormat.RGBA32, true) { hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            leafCutFaceNormalTex = new Texture2D(SL, SL, TextureFormat.RGBA32, true) { hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };

            var luCols = new Color32[SL * SL]; var luNrms = new Color32[SL * SL];
            var lcCols = new Color32[SL * SL]; var lcNrms = new Color32[SL * SL];

            for (int y = 0; y < SL; y++)
            {
                float v = y / (float)(SL - 1);
                for (int x = 0; x < SL; x++)
                {
                    float u = x / (float)(SL - 1);

                    // Mặt trên lá: tế bào biểu bì uốn lượn jigsaw pavement cells
                    float wu = u * 12f + Mathf.Sin(v * 38f) * 0.25f;
                    float wv = v * 9f + Mathf.Cos(u * 38f) * 0.25f;
                    EvalHexCellGrid(wu, wv, out float eDist, out float egx, out float egy);
                    float wallT = Mathf.SmoothStep(0.0f, 0.18f, eDist);
                    Color luCol = Color.Lerp(new Color(0.68f, 0.84f, 0.62f), new Color(0.96f, 1.0f, 0.90f), wallT);
                    luCols[y * SL + x] = luCol;
                    luNrms[y * SL + x] = new Color32((byte)(Mathf.Clamp01(0.5f + egx * 0.25f) * 255f), (byte)(Mathf.Clamp01(0.5f + egy * 0.25f) * 255f), 242, 255);

                    // Lõi thịt lá bên trong: nửa trên cột mô giậu khít đậm lục, nửa dưới mô xốp thưa có khoang khí xanh sẫm sâu
                    Color cutCol;
                    float cnx = 0.5f, cny = 0.5f;
                    if (v > 0.52f)
                    {
                        EvalHexCellGrid(u * 18f, v * 5.5f, out float pEdge, out float pgx, out float pgy);
                        float pWall = Mathf.SmoothStep(0.0f, 0.22f, pEdge);
                        cutCol = Color.Lerp(new Color(0.09f, 0.26f, 0.10f), new Color(0.24f, 0.56f, 0.22f), pWall);
                        cnx = 0.5f + pgx * 0.26f; cny = 0.5f + pgy * 0.20f;
                    }
                    else
                    {
                        EvalHexCellGrid(u * 9.5f, v * 8.5f, out float spEdge, out float spgx, out float spgy);
                        // Khoang khí tối sâu xen kẽ các tế bào mô xốp tròn sáng hơn
                        float cavity = Mathf.SmoothStep(0.18f, 0.44f, spEdge);
                        cutCol = Color.Lerp(new Color(0.05f, 0.14f, 0.07f), new Color(0.28f, 0.60f, 0.26f), cavity);
                        cnx = 0.5f + spgx * 0.28f; cny = 0.5f + spgy * 0.28f;
                    }
                    lcCols[y * SL + x] = cutCol;
                    lcNrms[y * SL + x] = new Color32((byte)(Mathf.Clamp01(cnx) * 255f), (byte)(Mathf.Clamp01(cny) * 255f), 236, 255);
                }
            }

            leafUpperTex.SetPixels32(luCols); leafUpperTex.Apply(true, false);
            leafUpperNormalTex.SetPixels32(luNrms); leafUpperNormalTex.Apply(true, false);
            leafCutFaceTex.SetPixels32(lcCols); leafCutFaceTex.Apply(true, false);
            leafCutFaceNormalTex.SetPixels32(lcNrms); leafCutFaceNormalTex.Apply(true, false);
            createdTextures.Add(leafUpperTex); createdTextures.Add(leafUpperNormalTex);
            createdTextures.Add(leafCutFaceTex); createdTextures.Add(leafCutFaceNormalTex);
        }

        static void EvalHexCellGrid(float u, float v, out float edgeDist, out float gradX, out float gradY)
        {
            int ix = Mathf.FloorToInt(u);
            int iy = Mathf.FloorToInt(v);
            float minD1 = 10f, minD2 = 10f;
            float bestDx = 0f, bestDy = 0f;

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int cx = ix + dx, cy = iy + dy;
                    float jx = 0.5f + 0.32f * Mathf.Sin(cx * 12.9898f + cy * 78.233f);
                    float jy = 0.5f + 0.32f * Mathf.Cos(cx * 39.346f + cy * 11.135f);
                    float px = cx + jx - u;
                    float py = cy + jy - v;
                    float d = Mathf.Sqrt(px * px + py * py);
                    if (d < minD1) { minD2 = minD1; minD1 = d; bestDx = px; bestDy = py; }
                    else if (d < minD2) { minD2 = d; }
                }
            }
            edgeDist = Mathf.Clamp01(minD2 - minD1);
            gradX = Mathf.Clamp(bestDx * 1.4f, -1f, 1f);
            gradY = Mathf.Clamp(bestDy * 1.4f, -1f, 1f);
        }

        // ---------------------------------------------------------------------
        // Bộ dựng GameObject, Vật liệu & Điểm neo nhãn 3D
        // ---------------------------------------------------------------------
        Transform Group(string name, Transform parent, Vector3 localPos)
        {
            var go = new GameObject(name) { layer = StageLayer };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go.transform;
        }

        GameObject MeshObject(Transform parent, string name, Mesh mesh, Vector3 localPos, Vector3 euler, Vector3 scale, Material mat, bool hasCol = true)
        {
            var go = new GameObject(name) { layer = StageLayer };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localEulerAngles = euler;
            go.transform.localScale = scale;

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            if (hasCol && mesh != null)
            {
                var bc = go.AddComponent<BoxCollider>();
                bc.center = mesh.bounds.center;
                bc.size = Vector3.Max(mesh.bounds.size, new Vector3(0.014f, 0.014f, 0.014f));
            }
            return go;
        }

        GameObject Part(PrimitiveType type, Transform parent, string name, Vector3 localPos, Vector3 scale, Vector3 euler, Material mat, bool hasCol = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.layer = StageLayer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localEulerAngles = euler;
            go.transform.localScale = scale;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (!hasCol)
            {
                var c = go.GetComponent<Collider>();
                if (c != null) Destroy(c);
            }
            return go;
        }

        void RegisterPart(GameObject go, string partId, Color baseColor)
        {
            var mr = go.GetComponent<MeshRenderer>();
            var tag = go.AddComponent<ThucVatPartTag>();
            tag.PartId = partId;
            tag.TargetRenderer = mr;
            tag.BaseColor = baseColor;
            allParts.Add(tag);
        }

        void RegisterAnchor(string key, Transform tr, Vector3 localOffset)
        {
            if (string.IsNullOrEmpty(key) || tr == null) return;
            namedAnchors[key] = (tr, localOffset);
        }

        public bool TryGetAnchorWorldPos(string key, out Vector3 worldPos)
        {
            worldPos = StageOrigin;
            if (string.IsNullOrEmpty(key)) return false;
            if (!namedAnchors.TryGetValue(key, out var entry) || entry.tr == null) return false;
            worldPos = entry.tr.TransformPoint(entry.offset);
            return true;
        }

        void AddPin(KhungNhinThucVat view, string partId, string label, Transform anchor, Vector3 localOffset, Color dotColor)
        {
            annotationPins.Add(new PinChuThich3D
            {
                View = view,
                PartId = partId,
                Label = label,
                Anchor = anchor,
                LocalOffset = localOffset,
                DotColor = dotColor
            });
        }

        Material NewMat(Shader shader, Color color, float smoothness, float metallic = 0f)
        {
            var m = new Material(shader) { hideFlags = HideFlags.DontSave };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            createdMaterials.Add(m);
            return m;
        }

        Material NewTexturedMat(Shader shader, Color color, Texture2D albedo, Texture2D normalMap, float smoothness, float bumpScale)
        {
            var m = NewMat(shader, color, smoothness);
            if (albedo != null && m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", albedo);
            if (normalMap != null && m.HasProperty("_BumpMap"))
            {
                m.EnableKeyword("_NORMALMAP");
                m.SetTexture("_BumpMap", normalMap);
                if (m.HasProperty("_BumpScale")) m.SetFloat("_BumpScale", bumpScale);
            }
            return m;
        }

        Material NewTransparentLit(Shader urpLit, Color color)
        {
            var m = NewMat(urpLit, color, 0.55f);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return m;
        }

        void OnDestroy()
        {
            for (int i = 0; i < createdMaterials.Count; i++)
                if (createdMaterials[i] != null) Destroy(createdMaterials[i]);
            for (int i = 0; i < createdMeshes.Count; i++)
                if (createdMeshes[i] != null) Destroy(createdMeshes[i]);
            for (int i = 0; i < createdTextures.Count; i++)
                if (createdTextures[i] != null) Destroy(createdTextures[i]);
        }
    }
}
