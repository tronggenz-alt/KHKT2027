using System;
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

    public sealed class ThucVatPartTag : MonoBehaviour
    {
        public string PartId;
        public Renderer TargetRenderer;
        [NonSerialized] public Color BaseColor;
    }

    /// <summary>
    /// Sân khấu 3D cây hai lá mầm (cây đậu) + 3 lát cắt giải phẫu phóng to + dòng chảy mạch & khí khổng thời gian thực.
    /// Dựng hoàn toàn bằng mã theo phong cách LabKit trên layer riêng (28), giới hạn hạt <= HardwareProfile.SoHatToiDa.
    /// </summary>
    public sealed class CayHaiLaMam3D : MonoBehaviour
    {
        public const int StageLayer = 28;
        public static readonly Vector3 StageOrigin = new Vector3(0f, -450f, 0f);

        public KhungNhinThucVat KhungHienTai { get; private set; } = KhungNhinThucVat.TongThe;
        public bool HienChuThich { get; private set; } = true;
        public string BoPhanDangChon { get; private set; }

        Transform wholeRoot, rootSliceRoot, stemSliceRoot, leafSliceRoot;
        Transform stemMount;
        readonly Transform[] stemNodes = new Transform[7];
        readonly Vector3[] stemBaseLocalPos = new Vector3[7];
        Transform lampPivot, lampBulbObj, lampConeObj;

        // Khí khổng động (2 tế bào hạt đậu ở lát cắt lá)
        Transform guardLeftA, guardLeftB, guardRightA, guardRightB, stomaGap;

        // Vật liệu & shader thời gian thực
        Material leafMat, veinMat, xylemFlowMat, phloemFlowMat, caspariMat;
        Material bulbGlowMat, lampBeamMat;
        Material waterPtMat, sugarPtMat, co2PtMat, o2PtMat, auxinPtMat;
        Texture2D leafTex, leafNormalTex;

        readonly List<Transform> labelBillboards = new List<Transform>();
        readonly List<GameObject> annotationObjects = new List<GameObject>();
        readonly List<ThucVatPartTag> allParts = new List<ThucVatPartTag>();
        readonly List<Material> createdMaterials = new List<Material>();
        readonly List<Mesh> createdMeshes = new List<Mesh>();
        readonly List<Texture2D> createdTextures = new List<Texture2D>();

        // Bể hạt pre-allocated (không cấp phát mỗi khung, tuân thủ HardwareProfile.SoHatToiDa)
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
        Transform activeCamTr;
        ThucVatPartTag highlightedTag;
        MaterialPropertyBlock highlightBlock;

        public void Initialize(Transform cameraTransform)
        {
            activeCamTr = cameraTransform;
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
            for (int i = 0; i < annotationObjects.Count; i++)
                if (annotationObjects[i] != null) annotationObjects[i].SetActive(show);
        }

        public void SelectPart(string partId)
        {
            if (highlightedTag != null && highlightedTag.TargetRenderer != null)
            {
                highlightBlock.Clear();
                highlightedTag.TargetRenderer.SetPropertyBlock(null);
            }
            BoPhanDangChon = partId;
            highlightedTag = null;
            for (int i = 0; i < allParts.Count; i++)
            {
                if (allParts[i] != null && allParts[i].PartId == partId)
                {
                    highlightedTag = allParts[i];
                    break;
                }
            }
            if (highlightedTag != null && highlightedTag.TargetRenderer != null)
            {
                highlightBlock.SetColor("_EmissionColor", new Color(0.55f, 0.85f, 0.25f));
                highlightBlock.SetColor("_BaseColor", Color.Lerp(highlightedTag.BaseColor, Color.yellow, 0.25f));
                highlightedTag.TargetRenderer.SetPropertyBlock(highlightBlock);
            }
        }

        public void GetDefaultCameraPose(KhungNhinThucVat view, out Vector3 pivot, out float yaw, out float pitch, out float dist)
        {
            switch (view)
            {
                case KhungNhinThucVat.LatCatRe:
                    pivot = StageOrigin + new Vector3(0f, 0.08f, 0f);
                    yaw = 24f; pitch = 28f; dist = 0.86f;
                    break;
                case KhungNhinThucVat.LatCatThan:
                    pivot = StageOrigin + new Vector3(0f, 0.12f, 0f);
                    yaw = 22f; pitch = 30f; dist = 0.84f;
                    break;
                case KhungNhinThucVat.LatCatLa:
                    pivot = StageOrigin + new Vector3(0f, 0.08f, 0f);
                    yaw = 18f; pitch = -24f; dist = 0.90f;
                    break;
                default:
                    pivot = StageOrigin + new Vector3(0f, 0.22f, 0f);
                    yaw = 24f; pitch = 18f; dist = 1.48f;
                    break;
            }
        }

        public void ApplySimulation(in DieuKienMoiTruong dk, in TrangThaiThucVat st, float dt, float dayTimeScale)
        {
            latestEnv = dk;
            latestState = st;
            animTime += dt * dayTimeScale;

            // 1. Uốn thân theo góc hướng sáng & vươn dài khi trong tối (etiolation)
            float totalBendDeg = (float)st.GocUonThanDo;
            float stepBend = totalBendDeg / stemNodes.Length;
            float stretchY = (float)st.HeSoVuonDaiTrongToi;
            bool night = st.BanDem;

            for (int i = 0; i < stemNodes.Length; i++)
            {
                if (stemNodes[i] == null) continue;
                float weight = 0.45f + (i / (float)stemNodes.Length) * 1.1f;
                stemNodes[i].localRotation = Quaternion.Euler(0f, 0f, -stepBend * weight);
                stemNodes[i].localPosition = new Vector3(
                    stemBaseLocalPos[i].x,
                    stemBaseLocalPos[i].y * (i == 0 ? 1f : Mathf.Lerp(1f, stretchY, 0.45f)),
                    stemBaseLocalPos[i].z);
            }

            // Màu lá ngả vàng nhạt khi thiếu sáng mọc vống (etiolation)
            if (leafMat != null)
            {
                Color healthy = new Color(0.18f, 0.48f, 0.20f, 1f);
                Color etiolated = new Color(0.62f, 0.65f, 0.22f, 1f);
                Color leafCol = Color.Lerp(healthy, etiolated, Mathf.Clamp01((stretchY - 1f) * 1.1f));
                if (night) leafCol *= 0.62f;
                leafCol.a = 1f;
                leafMat.SetColor("_BaseColor", leafCol);
                leafMat.SetFloat("_WindSpeed", 1.2f + (float)dk.Gio01 * 5.2f);
                leafMat.SetFloat("_WindStrength", 0.006f + (float)dk.Gio01 * 0.032f);
            }

            // 2. Vị trí & cường độ đèn hướng sáng
            if (lampPivot != null)
            {
                lampPivot.localRotation = Quaternion.Euler(0f, 0f, -(float)dk.GocDenHuongSangDo);
                float light01 = Mathf.Clamp01((float)(dk.AnhSangLux / 45000.0));
                if (lampConeObj != null) lampConeObj.gameObject.SetActive(!night);
                if (bulbGlowMat != null)
                {
                    Color glow = night ? new Color(0.25f, 0.28f, 0.35f) : Color.Lerp(new Color(0.85f, 0.78f, 0.45f), new Color(1.0f, 0.96f, 0.75f), light01);
                    bulbGlowMat.SetColor("_BaseColor", glow);
                    bulbGlowMat.SetColor("_EmissionColor", night ? Color.black : glow * (1.2f + light01 * 1.8f));
                }
            }

            // 3. Tốc độ shader cuộn UV trong mạch gỗ & mạch rây
            if (xylemFlowMat != null)
            {
                float spd = Mathf.Clamp((float)st.TocDoMachGo * 0.55f, 0.08f, 3.6f);
                xylemFlowMat.SetFloat("_FlowSpeed", spd);
                xylemFlowMat.SetFloat("_FlowStrength", Mathf.Clamp01(0.25f + (float)st.TocDoMachGo * 0.25f));
            }
            if (phloemFlowMat != null)
            {
                float spd = Mathf.Clamp((float)st.TocDoMachRay * 2.4f, 0.0f, 3.0f);
                phloemFlowMat.SetFloat("_FlowSpeed", -spd); // âm: chảy ngược xuống
                phloemFlowMat.SetFloat("_FlowStrength", spd > 0.02f ? 1.1f : 0.15f);
            }

            // 4. Hai tế bào hạt đậu ở lát cắt lá trương/xẹp theo thời gian thực
            UpdateStomataVisual((float)st.DoMoKhiKhong01, (float)st.DoTruongTeBaoHatDau01);

            // 5. Cập nhật hạt (không cấp phát mỗi khung, tuân thủ HardwareProfile.SoHatToiDa)
            UpdateParticles(st, dt * Mathf.Max(1f, Mathf.Sqrt(dayTimeScale)));
        }

        void Update()
        {
            if (activeCamTr == null) return;
            Vector3 fwd = activeCamTr.forward;
            Vector3 up = activeCamTr.up;
            for (int i = 0; i < labelBillboards.Count; i++)
            {
                var tr = labelBillboards[i];
                if (tr != null && tr.gameObject.activeInHierarchy)
                    tr.rotation = Quaternion.LookRotation(fwd, up);
            }
        }

        void UpdateStomataVisual(float open01, float turgor01)
        {
            if (guardLeftA == null || guardRightA == null) return;
            float bow = Mathf.Lerp(0.002f, 0.036f, open01);
            float swell = Mathf.Lerp(0.76f, 1.24f, turgor01);

            guardLeftA.localPosition = new Vector3(-0.022f - bow * 0.45f, -0.055f, 0.018f);
            guardLeftA.localRotation = Quaternion.Euler(0f, -Mathf.Lerp(6f, 34f, open01), 0f);
            guardLeftA.localScale = new Vector3(0.030f * swell, 0.020f * swell, 0.052f);

            guardLeftB.localPosition = new Vector3(-0.022f - bow * 0.45f, -0.055f, -0.018f);
            guardLeftB.localRotation = Quaternion.Euler(0f, Mathf.Lerp(6f, 34f, open01), 0f);
            guardLeftB.localScale = new Vector3(0.030f * swell, 0.020f * swell, 0.052f);

            guardRightA.localPosition = new Vector3(0.022f + bow * 0.45f, -0.055f, 0.018f);
            guardRightA.localRotation = Quaternion.Euler(0f, Mathf.Lerp(6f, 34f, open01), 0f);
            guardRightA.localScale = new Vector3(0.030f * swell, 0.020f * swell, 0.052f);

            guardRightB.localPosition = new Vector3(0.022f + bow * 0.45f, -0.055f, -0.018f);
            guardRightB.localRotation = Quaternion.Euler(0f, -Mathf.Lerp(6f, 34f, open01), 0f);
            guardRightB.localScale = new Vector3(0.030f * swell, 0.020f * swell, 0.052f);

            if (stomaGap != null)
            {
                float poreW = Mathf.Max(0.003f, Mathf.Lerp(0.004f, 0.044f, open01));
                stomaGap.localScale = new Vector3(poreW, 0.022f, 0.058f);
            }
        }

        void UpdateParticles(in TrangThaiThucVat st, float dt)
        {
            int cap = Mathf.Max(16, HardwareProfile.SoHatToiDa);

            // Phân bổ ngân sách hạt chặt theo HardwareProfile.SoHatToiDa
            int maxWater = Mathf.Min(waterPool.Length, Mathf.Max(4, cap / 6));
            int activeWater = Mathf.Clamp(Mathf.RoundToInt((float)(st.TocDoMachGo * 4.5)), 2, maxWater);
            float waterSpd = Mathf.Clamp((float)st.TocDoMachGo * 0.14f, 0.04f, 0.9f);

            for (int i = 0; i < waterPool.Length; i++)
            {
                bool on = (KhungHienTai == KhungNhinThucVat.TongThe || KhungHienTai == KhungNhinThucVat.LatCatRe) && i < activeWater;
                var go = waterPool[i].tr.gameObject;
                if (go.activeSelf != on) go.SetActive(on);
                if (!on) continue;

                waterProgress[i] = (waterProgress[i] + dt * waterSpd) % 1f;
                float u = waterProgress[i];
                if (KhungHienTai == KhungNhinThucVat.TongThe)
                {
                    float y = Mathf.Lerp(-0.22f, 0.56f, u);
                    Vector3 worldOnStem = SampleStemAxis(Mathf.InverseLerp(0f, 0.56f, Mathf.Max(0f, y)));
                    float offX = y < 0f ? (waterPool[i].lane - 1) * 0.010f : -0.007f;
                    waterPool[i].tr.position = worldOnStem + new Vector3(offX, y < 0f ? y : 0f, 0.008f);
                }
                else
                {
                    float ang = waterPool[i].lane * 1.57f;
                    waterPool[i].tr.position = StageOrigin + new Vector3(Mathf.Cos(ang) * 0.028f, Mathf.Lerp(-0.08f, 0.24f, u), Mathf.Sin(ang) * 0.028f);
                }
            }

            int maxSugar = Mathf.Min(sugarPool.Length, Mathf.Max(3, cap / 8));
            int activeSugar = st.TocDoMachRay > 0.02 ? Mathf.Clamp(Mathf.RoundToInt((float)(st.TocDoMachRay * 18.0)), 2, maxSugar) : 0;
            float sugarSpd = Mathf.Clamp((float)st.TocDoMachRay * 0.45f, 0.06f, 0.65f);

            for (int i = 0; i < sugarPool.Length; i++)
            {
                bool on = KhungHienTai == KhungNhinThucVat.TongThe && i < activeSugar;
                var go = sugarPool[i].tr.gameObject;
                if (go.activeSelf != on) go.SetActive(on);
                if (!on) continue;

                float u = (sugarPool[i].phase + animTime * sugarSpd) % 1f;
                float tStem = Mathf.Lerp(0.92f, 0.0f, u);
                Vector3 p = SampleStemAxis(tStem);
                sugarPool[i].tr.position = p + new Vector3(0.011f, u > 0.85f ? -0.18f * (u - 0.85f) * 6f : 0f, 0.008f);
            }

            bool showGas = !st.BanDem && st.O2ThaiRaMoiGiay > 0.02 && (KhungHienTai == KhungNhinThucVat.TongThe || KhungHienTai == KhungNhinThucVat.LatCatLa);
            int maxGas = Mathf.Min(co2Pool.Length, Mathf.Max(3, cap / 8));
            int activeGas = showGas ? Mathf.Clamp(Mathf.RoundToInt((float)(st.O2ThaiRaMoiGiay * maxGas)), 2, maxGas) : 0;
            float gasSpd = Mathf.Clamp(0.22f + (float)st.O2ThaiRaMoiGiay * 0.65f, 0.15f, 0.9f);

            for (int i = 0; i < co2Pool.Length; i++)
            {
                bool on = i < activeGas;
                if (co2Pool[i].tr.gameObject.activeSelf != on) co2Pool[i].tr.gameObject.SetActive(on);
                if (o2Pool[i].tr.gameObject.activeSelf != on) o2Pool[i].tr.gameObject.SetActive(on);
                if (!on) continue;

                float u = (co2Pool[i].phase + animTime * gasSpd) % 1f;
                if (KhungHienTai == KhungNhinThucVat.TongThe)
                {
                    Vector3 anchor = SampleStemAxis(0.45f + (i % 3) * 0.18f) + new Vector3((i % 2 == 0 ? 1 : -1) * 0.14f, 0.02f, 0.02f);
                    co2Pool[i].tr.position = anchor + new Vector3(0.06f * (1f - u), -0.04f * (1f - u), 0.04f * (1f - u));
                    o2Pool[i].tr.position = anchor + new Vector3(-0.04f * u, 0.11f * u, -0.02f * u);
                }
                else
                {
                    // Lát cắt lá: CO2 chui qua khe khí khổng (y = -0.07) lên mô xốp; O2 thoát ngược ra ngoài
                    float spread = (co2Pool[i].lane - 1) * 0.012f;
                    co2Pool[i].tr.position = StageOrigin + new Vector3(spread, Mathf.Lerp(-0.20f, 0.05f, u), 0.008f);
                    o2Pool[i].tr.position = StageOrigin + new Vector3(-spread, Mathf.Lerp(0.04f, -0.22f, u), -0.008f);
                }
            }

            // Hạt Auxin phân bố phía khuất sáng vs phía sáng trong thân (chế độ tổng thể)
            int maxAuxin = Mathf.Min(auxinPool.Length, Mathf.Max(4, cap / 8));
            int darkSideCount = Mathf.RoundToInt(maxAuxin * (float)st.TiLeAuxinPhiaToi);
            float lightDirSign = latestEnv.GocDenHuongSangDo >= 0 ? 1f : -1f; // đèn ở bên +X thì phía tối ở -X
            for (int i = 0; i < auxinPool.Length; i++)
            {
                bool on = KhungHienTai == KhungNhinThucVat.TongThe && i < maxAuxin;
                var go = auxinPool[i].tr.gameObject;
                if (go.activeSelf != on) go.SetActive(on);
                if (!on) continue;

                bool onDarkSide = i < darkSideCount;
                float u = (auxinPool[i].phase + animTime * 0.18f) % 1f;
                float hFrac = 0.25f + u * 0.68f;
                Vector3 p = SampleStemAxis(hFrac);
                float sideX = (onDarkSide ? -lightDirSign : lightDirSign) * (0.015f + (i % 3) * 0.003f);
                auxinPool[i].tr.position = p + new Vector3(sideX, 0f, Mathf.Sin(i * 1.7f) * 0.010f);
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
        // Dựng ánh sáng, vật liệu và toàn bộ mô hình bằng mã
        // ---------------------------------------------------------------------

        void BuildLighting()
        {
            var keyGo = new GameObject("Đèn chính sân khấu") { layer = StageLayer };
            keyGo.transform.SetParent(transform, false);
            keyGo.transform.rotation = Quaternion.Euler(46f, -32f, 0f);
            var key = keyGo.AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.45f;
            key.color = new Color(1f, 0.98f, 0.93f);
            key.shadows = LightShadows.None;
            key.cullingMask = 1 << StageLayer;

            var fillGo = new GameObject("Đèn phụ sân khấu") { layer = StageLayer };
            fillGo.transform.SetParent(transform, false);
            fillGo.transform.rotation = Quaternion.Euler(-30f, 140f, 0f);
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.72f;
            fill.color = new Color(0.82f, 0.92f, 1.0f);
            fill.shadows = LightShadows.None;
            fill.cullingMask = 1 << StageLayer;
        }

        void BuildMaterials()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            Shader leafShader = Shader.Find("Khkt/Bio/LaCay") ?? urpLit;
            Shader flowShader = Shader.Find("Khkt/Bio/LaCayMach") ?? urpLit;

            BuildLeafTextures();

            leafMat = NewMat(leafShader, new Color(0.18f, 0.48f, 0.20f, 1f), 0.35f);
            leafMat.SetTexture("_BaseMap", leafTex);
            if (leafMat.HasProperty("_BumpMap")) leafMat.SetTexture("_BumpMap", leafNormalTex);
            if (leafMat.HasProperty("_Translucency")) leafMat.SetFloat("_Translucency", 0.55f);
            if (leafMat.HasProperty("_Cutoff")) leafMat.SetFloat("_Cutoff", 0.35f);

            veinMat = NewMat(urpLit, new Color(0.42f, 0.68f, 0.26f), 0.3f);

            xylemFlowMat = NewMat(flowShader, new Color(0.10f, 0.48f, 0.92f, 0.50f), 0.5f);
            if (xylemFlowMat.HasProperty("_PulseColor"))
            {
                xylemFlowMat.SetColor("_PulseColor", new Color(0.45f, 0.92f, 1.0f, 0.95f));
                xylemFlowMat.SetFloat("_FlowSpeed", 1.1f);
                xylemFlowMat.SetFloat("_PulseDensity", 7f);
            }

            phloemFlowMat = NewMat(flowShader, new Color(0.88f, 0.55f, 0.12f, 0.50f), 0.5f);
            if (phloemFlowMat.HasProperty("_PulseColor"))
            {
                phloemFlowMat.SetColor("_PulseColor", new Color(1.0f, 0.88f, 0.35f, 0.95f));
                phloemFlowMat.SetFloat("_FlowSpeed", -0.8f);
                phloemFlowMat.SetFloat("_PulseDensity", 6f);
            }

            caspariMat = NewMat(urpLit, new Color(0.88f, 0.25f, 0.14f), 0.38f);
            caspariMat.EnableKeyword("_EMISSION");
            caspariMat.SetColor("_EmissionColor", new Color(0.32f, 0.06f, 0.02f));

            bulbGlowMat = NewMat(urpLit, new Color(1f, 0.95f, 0.65f), 0.6f);
            bulbGlowMat.EnableKeyword("_EMISSION");

            lampBeamMat = NewTransparentLit(urpLit, new Color(1f, 0.94f, 0.55f, 0.13f));

            waterPtMat = NewMat(urpLit, new Color(0.18f, 0.72f, 1f), 0.6f);
            sugarPtMat = NewMat(urpLit, new Color(1f, 0.76f, 0.16f), 0.5f);
            co2PtMat = NewMat(urpLit, new Color(0.75f, 0.78f, 0.84f), 0.3f);
            o2PtMat = NewMat(urpLit, new Color(0.28f, 0.96f, 0.62f), 0.55f);
            auxinPtMat = NewMat(urpLit, new Color(0.95f, 0.88f, 0.15f), 0.5f);
            auxinPtMat.EnableKeyword("_EMISSION");
            auxinPtMat.SetColor("_EmissionColor", new Color(0.45f, 0.38f, 0.05f));
        }

        void BuildWholePlant()
        {
            wholeRoot = Group("Cây hai lá mầm (Tổng thể)", transform, Vector3.zero);
            Material stemMat = NewMat(Shader.Find("Universal Render Pipeline/Lit"), new Color(0.28f, 0.54f, 0.22f), 0.28f);
            Material rootMat = NewMat(Shader.Find("Universal Render Pipeline/Lit"), new Color(0.72f, 0.58f, 0.40f), 0.18f);
            Material rootCapMat = NewMat(Shader.Find("Universal Render Pipeline/Lit"), new Color(0.88f, 0.72f, 0.32f), 0.35f);
            Material hairMat = NewMat(Shader.Find("Universal Render Pipeline/Lit"), new Color(0.95f, 0.96f, 0.88f), 0.22f);
            Material soilRimMat = NewTransparentLit(Shader.Find("Universal Render Pipeline/Lit"), new Color(0.32f, 0.24f, 0.18f, 0.26f));
            Material potCeramicMat = NewMat(Shader.Find("Universal Render Pipeline/Lit"), new Color(0.78f, 0.42f, 0.28f), 0.35f);
            Material petalWhiteMat = NewMat(Shader.Find("Universal Render Pipeline/Lit"), new Color(0.98f, 0.94f, 0.80f), 0.35f);
            Material petalGoldMat = NewMat(Shader.Find("Universal Render Pipeline/Lit"), new Color(0.96f, 0.76f, 0.16f), 0.45f);
            Material lampBodyMat = NewMat(Shader.Find("Universal Render Pipeline/Lit"), new Color(0.22f, 0.26f, 0.32f), 0.45f, 0.65f);

            // Đĩa đất mặt chậu mờ bán trong suốt để nhìn thấu toàn bộ hệ rễ cọc bên dưới
            var potRing = Part(PrimitiveType.Cylinder, wholeRoot, "Mặt đất cắt dọc", new Vector3(0f, -0.012f, 0f), new Vector3(0.34f, 0.012f, 0.34f), Vector3.zero, soilRimMat, hasCol: false);
            Part(PrimitiveType.Cylinder, wholeRoot, "Vành chậu dưới", new Vector3(0f, -0.345f, 0f), new Vector3(0.30f, 0.014f, 0.30f), Vector3.zero, potCeramicMat, hasCol: false);

            // Hệ rễ cọc (chóp rễ & miền lông hút)
            var rootGroup = Group("Hệ rễ cọc", wholeRoot, Vector3.zero);
            var tap = Part(PrimitiveType.Capsule, rootGroup, "Rễ chính", new Vector3(0f, -0.16f, 0f), new Vector3(0.026f, 0.165f, 0.026f), Vector3.zero, rootMat);
            RegisterPart(tap, "re_chop_va_long_hut", rootMat.color);
            var cap = Part(PrimitiveType.Sphere, rootGroup, "Chóp rễ", new Vector3(0f, -0.32f, 0f), new Vector3(0.022f, 0.032f, 0.022f), Vector3.zero, rootCapMat);
            RegisterPart(cap, "re_chop_va_long_hut", rootCapMat.color);

            // Rễ bên tỏa ra 5 hướng
            for (int r = 0; r < 6; r++)
            {
                float ang = r * 60f + 15f;
                float y0 = -0.06f - r * 0.034f;
                var br = Part(PrimitiveType.Capsule, rootGroup, $"Rễ bên {r + 1}",
                    new Vector3(Mathf.Cos(ang * Mathf.Deg2Rad) * 0.065f, y0 - 0.035f, Mathf.Sin(ang * Mathf.Deg2Rad) * 0.065f),
                    new Vector3(0.011f, 0.075f, 0.011f),
                    new Vector3(0f, -ang, 58f), rootMat);
                RegisterPart(br, "re_chop_va_long_hut", rootMat.color);
            }
            // Lông hút dày đặc ở miền lông hút
            for (int h = 0; h < 28; h++)
            {
                float ang = h * 137.5f;
                float yH = -0.17f - (h / 28f) * 0.11f;
                Vector3 dir = new Vector3(Mathf.Cos(ang * Mathf.Deg2Rad), -0.15f, Mathf.Sin(ang * Mathf.Deg2Rad)).normalized;
                Part(PrimitiveType.Cylinder, rootGroup, "Lông hút",
                    new Vector3(dir.x * 0.026f, yH, dir.z * 0.026f),
                    new Vector3(0.0022f, 0.022f, 0.0022f),
                    Quaternion.FromToRotation(Vector3.up, dir).eulerAngles, hairMat, hasCol: false);
            }
            AddAnnotation(wholeRoot, "Hệ rễ cọc & lông hút", new Vector3(0.22f, -0.18f, 0f));

            // Thân cây đậu chia 7 đốt động để uốn cong theo hướng sáng
            stemMount = Group("Trục thân cây", wholeRoot, Vector3.zero);
            float segLen = 0.082f;
            Transform parent = stemMount;
            Mesh leafMesh = BuildDicotLeafMesh();

            for (int i = 0; i < stemNodes.Length; i++)
            {
                var node = Group($"Đốt thân {i + 1}", parent, i == 0 ? Vector3.zero : new Vector3(0f, segLen, 0f));
                stemNodes[i] = node;
                stemBaseLocalPos[i] = node.localPosition;

                float r = Mathf.Lerp(0.024f, 0.012f, i / (float)(stemNodes.Length - 1));
                var segObj = Part(PrimitiveType.Cylinder, node, $"Thân đoạn {i + 1}", new Vector3(0f, segLen * 0.5f, 0f), new Vector3(r, segLen * 0.52f, r), Vector3.zero, stemMat);
                RegisterPart(segObj, "than_cay", stemMat.color);

                // Hai ống dòng chảy mạch gỗ (trái) và mạch rây (phải) chạy dọc thân
                Part(PrimitiveType.Cylinder, node, "Dòng mạch gỗ", new Vector3(-r * 0.68f, segLen * 0.5f, 0.004f), new Vector3(0.0065f, segLen * 0.50f, 0.0065f), Vector3.zero, xylemFlowMat, hasCol: false);
                Part(PrimitiveType.Cylinder, node, "Dòng mạch rây", new Vector3(r * 0.68f, segLen * 0.5f, 0.004f), new Vector3(0.0065f, segLen * 0.50f, 0.0065f), Vector3.zero, phloemFlowMat, hasCol: false);

                // Gắn lá cây hai lá mầm xen kẽ từ đốt 2 đến đốt 6
                if (i >= 1 && i <= 5)
                {
                    float sideSign = (i % 2 == 0) ? 1f : -1f;
                    float yawL = sideSign > 0 ? 12f : 185f;
                    float pitchL = 18f + i * 2f;
                    float leafScale = Mathf.Lerp(1.05f, 0.72f, (i - 1) / 4f);
                    var petiolePivot = Group($"Cuống lá {i}", node, new Vector3(0f, segLen * 0.85f, 0f));
                    petiolePivot.localRotation = Quaternion.Euler(0f, yawL + (i * 22f), 0f);
                    Part(PrimitiveType.Cylinder, petiolePivot, "Cuống", new Vector3(0.032f, 0.012f, 0f), new Vector3(0.0045f, 0.036f, 0.0045f), new Vector3(0f, 0f, -70f), stemMat, hasCol: false);

                    var bladeGo = new GameObject($"Lá cây {i}") { layer = StageLayer };
                    bladeGo.transform.SetParent(petiolePivot, false);
                    bladeGo.transform.localPosition = new Vector3(0.062f, 0.024f, 0f);
                    bladeGo.transform.localRotation = Quaternion.Euler(14f, 0f, -pitchL);
                    bladeGo.transform.localScale = Vector3.one * leafScale;
                    bladeGo.AddComponent<MeshFilter>().sharedMesh = leafMesh;
                    var mr = bladeGo.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = leafMat;
                    var bc = bladeGo.AddComponent<BoxCollider>();
                    bc.center = new Vector3(0.08f, 0f, 0f);
                    bc.size = new Vector3(0.16f, 0.016f, 0.11f);
                    RegisterPart(bladeGo, "la_cay", leafMat.GetColor("_BaseColor"));
                }

                parent = node;
            }

            // Hoa lưỡng tính cây đậu ở ngọn thân
            var flower = Group("Hoa cây đậu", parent, new Vector3(0f, segLen * 0.92f, 0f));
            var calyx = Part(PrimitiveType.Cylinder, flower, "Đài hoa", Vector3.zero, new Vector3(0.018f, 0.012f, 0.018f), Vector3.zero, stemMat);
            RegisterPart(calyx, "hoa_dau", stemMat.color);
            var banner = Part(PrimitiveType.Sphere, flower, "Cánh cờ", new Vector3(0f, 0.034f, -0.014f), new Vector3(0.052f, 0.048f, 0.014f), new Vector3(-22f, 0f, 0f), petalWhiteMat);
            RegisterPart(banner, "hoa_dau", petalWhiteMat.color);
            for (int w = -1; w <= 1; w += 2)
            {
                var wing = Part(PrimitiveType.Sphere, flower, "Cánh bên", new Vector3(w * 0.022f, 0.022f, 0.008f), new Vector3(0.022f, 0.032f, 0.012f), new Vector3(12f, w * 24f, 0f), petalGoldMat);
                RegisterPart(wing, "hoa_dau", petalGoldMat.color);
            }
            var keel = Part(PrimitiveType.Sphere, flower, "Cánh thuyền & nhị", new Vector3(0f, 0.016f, 0.016f), new Vector3(0.022f, 0.022f, 0.028f), Vector3.zero, petalGoldMat);
            RegisterPart(keel, "hoa_dau", petalGoldMat.color);

            // Đèn chiếu hướng sáng xoay quanh gốc cây
            lampPivot = Group("Cụm đèn hướng sáng", wholeRoot, new Vector3(0f, 0.25f, 0f));
            lampBulbObj = Part(PrimitiveType.Cylinder, lampPivot, "Chụp đèn", new Vector3(0f, 0.56f, 0f), new Vector3(0.07f, 0.045f, 0.07f), Vector3.zero, lampBodyMat, hasCol: false).transform;
            Part(PrimitiveType.Sphere, lampBulbObj, "Bóng đèn", new Vector3(0f, -0.7f, 0f), new Vector3(0.7f, 0.7f, 0.7f), Vector3.zero, bulbGlowMat, hasCol: false);
            lampConeObj = Part(PrimitiveType.Cylinder, lampPivot, "Luồng sáng", new Vector3(0f, 0.34f, 0f), new Vector3(0.18f, 0.19f, 0.18f), Vector3.zero, lampBeamMat, hasCol: false).transform;

            AddAnnotation(wholeRoot, "Hoa mẫu 5", new Vector3(0.18f, 0.62f, 0f));
            AddAnnotation(wholeRoot, "Lá gân hình mạng", new Vector3(-0.26f, 0.36f, 0.05f));
            AddAnnotation(wholeRoot, "Thân (Mạch gỗ ↑ | Mạch rây ↓)", new Vector3(0.24f, 0.20f, 0f));
        }

        // ---------------------------------------------------------------------
        // Lát cắt 1: LÁT CẮT RỄ HAI LÁ MẦM (lông hút, vỏ, nội bì đai Caspari, mạch gỗ sao, mạch rây)
        // ---------------------------------------------------------------------
        void BuildRootSlice()
        {
            rootSliceRoot = Group("Lát cắt Rễ", transform, Vector3.zero);
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            Material epiMat = NewMat(urpLit, new Color(0.84f, 0.72f, 0.48f), 0.25f);
            Material cortexMat = NewMat(urpLit, new Color(0.92f, 0.85f, 0.66f), 0.2f);
            Material pithMat = NewMat(urpLit, new Color(0.76f, 0.64f, 0.46f), 0.2f);

            // Trụ lát cắt ngang
            var epiDisc = Part(PrimitiveType.Cylinder, rootSliceRoot, "Vành biểu bì rễ", new Vector3(0f, 0.02f, 0f), new Vector3(0.46f, 0.028f, 0.46f), Vector3.zero, epiMat);
            RegisterPart(epiDisc, "re_long_hut", epiMat.color);

            // Vòng tế bào mô mềm vỏ rễ
            for (int i = 0; i < 14; i++)
            {
                float a = i * (360f / 14f) * Mathf.Deg2Rad;
                var cell = Part(PrimitiveType.Cylinder, rootSliceRoot, $"Tế bào vỏ {i + 1}",
                    new Vector3(Mathf.Cos(a) * 0.165f, 0.042f, Mathf.Sin(a) * 0.165f),
                    new Vector3(0.068f, 0.022f, 0.068f), Vector3.zero, cortexMat);
                RegisterPart(cell, "re_vo_nhu_mo", cortexMat.color);
            }

            // 10 lông hút tỏa ra quanh biểu bì
            for (int i = 0; i < 10; i++)
            {
                float a = i * 36f * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var hair = Part(PrimitiveType.Capsule, rootSliceRoot, $"Tế bào lông hút {i + 1}",
                    dir * 0.275f + Vector3.up * 0.03f,
                    new Vector3(0.022f, 0.065f, 0.022f),
                    Quaternion.FromToRotation(Vector3.up, dir).eulerAngles, epiMat);
                RegisterPart(hair, "re_long_hut", epiMat.color);
            }

            // Nội bì & ĐAI CASPARI màu đỏ cam rực rỡ quây kín vòng nội bì
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f * Mathf.Deg2Rad;
                var band = Part(PrimitiveType.Cube, rootSliceRoot, $"Đai Caspari {i + 1}",
                    new Vector3(Mathf.Cos(a) * 0.106f, 0.056f, Mathf.Sin(a) * 0.106f),
                    new Vector3(0.046f, 0.034f, 0.018f),
                    new Vector3(0f, -i * 30f + 90f, 0f), caspariMat);
                RegisterPart(band, "re_dai_caspari", caspariMat.color);
            }

            // Trung trụ: mạch gỗ hình chữ thập (+) ở giữa và 4 bó mạch rây xen kẽ
            var xylemArm1 = Part(PrimitiveType.Cube, rootSliceRoot, "Mạch gỗ hình sao A", new Vector3(0f, 0.062f, 0f), new Vector3(0.145f, 0.036f, 0.036f), Vector3.zero, xylemFlowMat);
            var xylemArm2 = Part(PrimitiveType.Cube, rootSliceRoot, "Mạch gỗ hình sao B", new Vector3(0f, 0.062f, 0f), new Vector3(0.036f, 0.036f, 0.145f), Vector3.zero, xylemFlowMat);
            Part(PrimitiveType.Cylinder, rootSliceRoot, "Cột mạch gỗ lên", new Vector3(0f, 0.11f, 0f), new Vector3(0.044f, 0.09f, 0.044f), Vector3.zero, xylemFlowMat, hasCol: false);
            RegisterPart(xylemArm1, "re_mach_go", new Color(0.1f, 0.5f, 0.9f));
            RegisterPart(xylemArm2, "re_mach_go", new Color(0.1f, 0.5f, 0.9f));

            for (int i = 0; i < 4; i++)
            {
                float a = (45f + i * 90f) * Mathf.Deg2Rad;
                var phloem = Part(PrimitiveType.Cylinder, rootSliceRoot, $"Mạch rây rễ {i + 1}",
                    new Vector3(Mathf.Cos(a) * 0.064f, 0.066f, Mathf.Sin(a) * 0.064f),
                    new Vector3(0.036f, 0.038f, 0.036f), Vector3.zero, phloemFlowMat);
                RegisterPart(phloem, "re_mach_ray", new Color(0.9f, 0.58f, 0.15f));
            }
            Part(PrimitiveType.Cylinder, rootSliceRoot, "Ruột trung trụ", new Vector3(0f, 0.035f, 0f), new Vector3(0.19f, 0.015f, 0.19f), Vector3.zero, pithMat, hasCol: false);

            AddAnnotation(rootSliceRoot, "Lông hút (hút nước)", new Vector3(0.36f, 0.06f, 0.06f));
            AddAnnotation(rootSliceRoot, "Đai Caspari (suberin chặn gian bào)", new Vector3(-0.32f, 0.14f, 0.08f));
            AddAnnotation(rootSliceRoot, "Mạch gỗ (hình sao ở giữa)", new Vector3(0.0f, 0.27f, 0.0f));
            AddAnnotation(rootSliceRoot, "Mạch rây (xen kẽ)", new Vector3(0.24f, 0.16f, -0.22f));
        }

        // ---------------------------------------------------------------------
        // Lát cắt 2: LÁT CẮT THÂN HAI LÁ MẦM (biểu bì, vỏ, 8 bó mạch: rây ngoài / tầng sinh mạch / gỗ trong)
        // ---------------------------------------------------------------------
        void BuildStemSlice()
        {
            stemSliceRoot = Group("Lát cắt Thân", transform, Vector3.zero);
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            Material barkMat = NewMat(urpLit, new Color(0.24f, 0.52f, 0.20f), 0.30f);
            Material cortexMat = NewMat(urpLit, new Color(0.56f, 0.78f, 0.42f), 0.22f);
            Material pithMat = NewMat(urpLit, new Color(0.84f, 0.88f, 0.70f), 0.18f);
            Material cambiumMat = NewMat(urpLit, new Color(0.68f, 0.94f, 0.22f), 0.42f);
            cambiumMat.EnableKeyword("_EMISSION");
            cambiumMat.SetColor("_EmissionColor", new Color(0.18f, 0.30f, 0.04f));

            var outer = Part(PrimitiveType.Cylinder, stemSliceRoot, "Biểu bì thân", new Vector3(0f, 0.04f, 0f), new Vector3(0.46f, 0.052f, 0.46f), Vector3.zero, barkMat);
            RegisterPart(outer, "than_bieu_bi", barkMat.color);
            var cortex = Part(PrimitiveType.Cylinder, stemSliceRoot, "Mô mềm vỏ thân", new Vector3(0f, 0.048f, 0f), new Vector3(0.42f, 0.048f, 0.42f), Vector3.zero, cortexMat);
            RegisterPart(cortex, "than_bieu_bi", cortexMat.color);
            Part(PrimitiveType.Cylinder, stemSliceRoot, "Ruột thân", new Vector3(0f, 0.050f, 0f), new Vector3(0.20f, 0.049f, 0.20f), Vector3.zero, pithMat, hasCol: false);

            // 8 bó mạch xếp vòng: Mạch rây ở ngoài -> Tầng sinh mạch ở giữa -> Mạch gỗ ở trong
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f * Mathf.Deg2Rad;
                Vector3 rad = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 euler = new Vector3(0f, -i * 45f, 0f);

                var phloem = Part(PrimitiveType.Cylinder, stemSliceRoot, $"Bó {i + 1} - Mạch rây (ngoài)",
                    rad * 0.165f + Vector3.up * 0.088f, new Vector3(0.046f, 0.055f, 0.036f), euler, phloemFlowMat);
                RegisterPart(phloem, "than_mach_ray", new Color(0.9f, 0.58f, 0.15f));

                var cambium = Part(PrimitiveType.Cube, stemSliceRoot, $"Bó {i + 1} - Tầng sinh mạch",
                    rad * 0.138f + Vector3.up * 0.095f, new Vector3(0.012f, 0.060f, 0.054f), euler, cambiumMat);
                RegisterPart(cambium, "than_tang_sinh_mach", cambiumMat.color);

                var xylem = Part(PrimitiveType.Cylinder, stemSliceRoot, $"Bó {i + 1} - Mạch gỗ (trong)",
                    rad * 0.106f + Vector3.up * 0.098f, new Vector3(0.048f, 0.068f, 0.048f), euler, xylemFlowMat);
                RegisterPart(xylem, "than_mach_go", new Color(0.12f, 0.52f, 0.92f));
            }

            AddAnnotation(stemSliceRoot, "Biểu bì & vỏ", new Vector3(0.36f, 0.06f, -0.06f));
            AddAnnotation(stemSliceRoot, "Mạch rây (phía ngoài bó mạch)", new Vector3(0.24f, 0.23f, 0.14f));
            AddAnnotation(stemSliceRoot, "Tầng sinh mạch (tượng tầng)", new Vector3(-0.35f, 0.15f, 0.08f));
            AddAnnotation(stemSliceRoot, "Mạch gỗ (phía trong bó mạch)", new Vector3(-0.02f, 0.27f, -0.04f));
        }

        // ---------------------------------------------------------------------
        // Lát cắt 3: LÁT CẮT LÁ (cutin, biểu bì, mô giậu, mô xốp, gân lá, khí khổng 2 tế bào hạt đậu động)
        // ---------------------------------------------------------------------
        void BuildLeafSlice()
        {
            leafSliceRoot = Group("Lát cắt Lá", transform, Vector3.zero);
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            Material cutinMat = NewTransparentLit(urpLit, new Color(0.84f, 0.96f, 0.88f, 0.48f));
            Material upEpiMat = NewMat(urpLit, new Color(0.68f, 0.88f, 0.64f), 0.35f);
            Material palisadeMat = NewMat(urpLit, new Color(0.14f, 0.46f, 0.16f), 0.25f);
            Material spongyMat = NewMat(urpLit, new Color(0.28f, 0.62f, 0.26f), 0.22f);
            Material guardMat = NewMat(urpLit, new Color(0.32f, 0.76f, 0.24f), 0.42f);
            Material poreMat = NewMat(urpLit, new Color(0.04f, 0.06f, 0.08f), 0.05f);

            // Tầng Cutin + Biểu bì trên
            Part(PrimitiveType.Cube, leafSliceRoot, "Lớp sáp cutin", new Vector3(0f, 0.190f, 0f), new Vector3(0.46f, 0.012f, 0.32f), Vector3.zero, cutinMat, hasCol: false);
            var upEpi = Part(PrimitiveType.Cube, leafSliceRoot, "Biểu bì trên lá", new Vector3(0f, 0.165f, 0f), new Vector3(0.46f, 0.036f, 0.32f), Vector3.zero, upEpiMat);
            RegisterPart(upEpi, "la_bieu_bi_cutin", upEpiMat.color);

            // Mô giậu (xếp khít giàu lục lạp)
            for (int ix = -3; ix <= 3; ix++)
            {
                for (int iz = -2; iz <= 2; iz++)
                {
                    if (ix == 0 && iz == 0) continue; // chừa chỗ cho bó mạch gân lá ở giữa
                    var pal = Part(PrimitiveType.Capsule, leafSliceRoot, "Tế bào mô giậu",
                        new Vector3(ix * 0.058f, 0.105f, iz * 0.058f),
                        new Vector3(0.044f, 0.050f, 0.044f), Vector3.zero, palisadeMat);
                    RegisterPart(pal, "la_mo_giau_va_mo_xop", palisadeMat.color);
                }
            }

            // Mô xốp (xếp thưa tạo khoang khí dẫn CO2/hơi nước xuống khí khổng)
            for (int ix = -2; ix <= 2; ix++)
            {
                for (int iz = -1; iz <= 1; iz++)
                {
                    if (ix == 0 && iz == 0) continue;
                    var sp = Part(PrimitiveType.Sphere, leafSliceRoot, "Tế bào mô xốp",
                        new Vector3(ix * 0.082f + (iz % 2) * 0.015f, 0.012f, iz * 0.075f),
                        new Vector3(0.058f, 0.046f, 0.058f), Vector3.zero, spongyMat);
                    RegisterPart(sp, "la_mo_giau_va_mo_xop", spongyMat.color);
                }
            }

            // Bó mạch gân lá: Mạch gỗ ở trên, Mạch rây ở dưới
            var leafXylem = Part(PrimitiveType.Cylinder, leafSliceRoot, "Gân lá - Mạch gỗ (trên)", new Vector3(0f, 0.092f, 0f), new Vector3(0.052f, 0.17f, 0.052f), new Vector3(90f, 0f, 0f), xylemFlowMat);
            var leafPhloem = Part(PrimitiveType.Cylinder, leafSliceRoot, "Gân lá - Mạch rây (dưới)", new Vector3(0f, 0.044f, 0f), new Vector3(0.048f, 0.17f, 0.048f), new Vector3(90f, 0f, 0f), phloemFlowMat);
            RegisterPart(leafXylem, "la_gan_bo_mach", new Color(0.12f, 0.52f, 0.92f));
            RegisterPart(leafPhloem, "la_gan_bo_mach", new Color(0.90f, 0.58f, 0.15f));

            // Biểu bì dưới có khoảng hở trung tâm cho khí khổng
            var lowL = Part(PrimitiveType.Cube, leafSliceRoot, "Biểu bì dưới trái", new Vector3(-0.145f, -0.052f, 0f), new Vector3(0.17f, 0.028f, 0.32f), Vector3.zero, upEpiMat);
            var lowR = Part(PrimitiveType.Cube, leafSliceRoot, "Biểu bì dưới phải", new Vector3(0.145f, -0.052f, 0f), new Vector3(0.17f, 0.028f, 0.32f), Vector3.zero, upEpiMat);
            RegisterPart(lowL, "la_bieu_bi_cutin", upEpiMat.color);
            RegisterPart(lowR, "la_bieu_bi_cutin", upEpiMat.color);

            // 2 tế bào hạt đậu uốn cong theo DoTruongTeBaoHatDau01 & DoMoKhiKhong01
            guardLeftA = Part(PrimitiveType.Capsule, leafSliceRoot, "Tế bào hạt đậu trái A", new Vector3(-0.036f, -0.055f, 0.018f), new Vector3(0.032f, 0.022f, 0.052f), new Vector3(90f, -20f, 0f), guardMat).transform;
            guardLeftB = Part(PrimitiveType.Capsule, leafSliceRoot, "Tế bào hạt đậu trái B", new Vector3(-0.036f, -0.055f, -0.018f), new Vector3(0.032f, 0.022f, 0.052f), new Vector3(90f, 20f, 0f), guardMat).transform;
            guardRightA = Part(PrimitiveType.Capsule, leafSliceRoot, "Tế bào hạt đậu phải A", new Vector3(0.036f, -0.055f, 0.018f), new Vector3(0.032f, 0.022f, 0.052f), new Vector3(90f, 20f, 0f), guardMat).transform;
            guardRightB = Part(PrimitiveType.Capsule, leafSliceRoot, "Tế bào hạt đậu phải B", new Vector3(0.036f, -0.055f, -0.018f), new Vector3(0.032f, 0.022f, 0.052f), new Vector3(90f, -20f, 0f), guardMat).transform;
            RegisterPart(guardLeftA.gameObject, "la_khi_khong", guardMat.color);
            RegisterPart(guardLeftB.gameObject, "la_khi_khong", guardMat.color);
            RegisterPart(guardRightA.gameObject, "la_khi_khong", guardMat.color);
            RegisterPart(guardRightB.gameObject, "la_khi_khong", guardMat.color);
            stomaGap = Part(PrimitiveType.Cube, leafSliceRoot, "Khe khí khổng", new Vector3(0f, -0.055f, 0f), new Vector3(0.032f, 0.022f, 0.058f), Vector3.zero, poreMat, hasCol: false).transform;

            AddAnnotation(leafSliceRoot, "Lớp cutin & Biểu bì trên", new Vector3(0.28f, 0.21f, 0f));
            AddAnnotation(leafSliceRoot, "Mô giậu (nhiều lục lạp)", new Vector3(-0.29f, 0.12f, 0.04f));
            AddAnnotation(leafSliceRoot, "Gân lá (Mạch gỗ trên | Rây dưới)", new Vector3(0.28f, 0.07f, 0f));
            AddAnnotation(leafSliceRoot, "Khí khổng (2 tế bào hạt đậu)", new Vector3(0.18f, -0.10f, 0.04f));
        }

        // ---------------------------------------------------------------------
        // Hạt biểu diễn (pre-allocated pool, 0 cấp phát khi chạy, luôn <= SoHatToiDa)
        // ---------------------------------------------------------------------
        void BuildParticlePools()
        {
            var root = Group("Hệ hạt sinh lý", transform, Vector3.zero);
            waterPool = AllocPool(root, "HatNuoc", 28, 0.012f, waterPtMat);
            waterProgress = new float[waterPool.Length];
            for (int i = 0; i < waterProgress.Length; i++) waterProgress[i] = waterPool[i].phase;

            sugarPool = AllocPool(root, "HatDuong", 22, 0.011f, sugarPtMat);
            co2Pool = AllocPool(root, "HatCO2", 18, 0.010f, co2PtMat);
            o2Pool = AllocPool(root, "HatO2", 18, 0.011f, o2PtMat);
            auxinPool = AllocPool(root, "HatAuxin", 24, 0.009f, auxinPtMat);
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
                arr[i] = new HatDong
                {
                    tr = go.transform,
                    phase = i / (float)count,
                    lane = i % 4
                };
            }
            return arr;
        }

        // ---------------------------------------------------------------------
        // Bộ sinh hình học, vật liệu và texture procedural
        // ---------------------------------------------------------------------
        Transform Group(string name, Transform parent, Vector3 localPos)
        {
            var go = new GameObject(name) { layer = StageLayer };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go.transform;
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

        void AddAnnotation(Transform parent, string text, Vector3 localPos)
        {
            var go = new GameObject("ChuThich_" + text) { layer = StageLayer };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.fontSize = 180;
            tm.characterSize = 0.00125f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(0.95f, 0.98f, 1.0f);
            tm.fontStyle = FontStyle.Bold;
            annotationObjects.Add(go);
            labelBillboards.Add(go.transform);
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

        Material NewTransparentLit(Shader urpLit, Color color)
        {
            var m = NewMat(urpLit, color, 0.6f);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return m;
        }

        void BuildLeafTextures()
        {
            const int S = 128;
            leafTex = new Texture2D(S, S, TextureFormat.RGBA32, true) { hideFlags = HideFlags.DontSave };
            leafNormalTex = new Texture2D(S, S, TextureFormat.RGBA32, true) { hideFlags = HideFlags.DontSave };

            var cols = new Color32[S * S];
            var nrms = new Color32[S * S];

            for (int y = 0; y < S; y++)
            {
                float v = (y / (float)(S - 1)) * 2f - 1f; // -1..1 (chiều rộng)
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)(S - 1); // 0..1 (từ cuống -> chóp lá)
                    float halfW = Mathf.Sin(Mathf.Pow(u, 0.72f) * Mathf.PI) * 0.88f;
                    bool inside = Mathf.Abs(v) <= halfW;

                    // Gân chính giữa + các gân bên xiên góc 35°
                    float midrib = Mathf.Exp(-Mathf.Abs(v) * 45f);
                    float latCoord = u * 7f - Mathf.Abs(v) * 3.8f;
                    float latVein = Mathf.Pow(Mathf.Abs(Mathf.Sin(latCoord * Mathf.PI)), 18f) * (1f - Mathf.Abs(v) / Mathf.Max(0.05f, halfW));
                    float vein = Mathf.Clamp01(midrib * 0.85f + latVein * 0.55f);

                    byte r = (byte)Mathf.Clamp(Mathf.RoundToInt((0.88f + vein * 0.25f) * 255f), 0, 255);
                    byte g = (byte)Mathf.Clamp(Mathf.RoundToInt((0.94f + vein * 0.18f) * 255f), 0, 255);
                    byte b = (byte)Mathf.Clamp(Mathf.RoundToInt((0.82f + vein * 0.14f) * 255f), 0, 255);
                    byte a = (byte)(inside ? 255 : 0);
                    cols[y * S + x] = new Color32(r, g, b, a);

                    float nx = 0.5f + Mathf.Sign(v) * (midrib * 0.25f + latVein * 0.15f);
                    float ny = 0.5f - latVein * 0.12f;
                    nrms[y * S + x] = new Color32((byte)(nx * 255f), (byte)(ny * 255f), 245, 255);
                }
            }

            leafTex.SetPixels32(cols);
            leafTex.Apply(true, false);
            leafNormalTex.SetPixels32(nrms);
            leafNormalTex.Apply(true, false);
            createdTextures.Add(leafTex);
            createdTextures.Add(leafNormalTex);
        }

        Mesh BuildDicotLeafMesh()
        {
            const int nx = 10, nz = 6;
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var vcols = new List<Color>();
            var tris = new List<int>();

            float len = 0.18f, halfWidth = 0.085f;
            for (int iz = 0; iz <= nz; iz++)
            {
                float vz = (iz / (float)nz) * 2f - 1f;
                for (int ix = 0; ix <= nx; ix++)
                {
                    float u = ix / (float)nx;
                    float camber = Mathf.Abs(vz) * 0.012f - u * u * 0.024f;
                    verts.Add(new Vector3(u * len, camber, vz * halfWidth));
                    norms.Add(Vector3.up);
                    uvs.Add(new Vector2(u, iz / (float)nz));
                    // color.r = biên độ rung gió (cuống = 0, chóp lá = 1), color.g = sắc xanh non
                    vcols.Add(new Color(u, 0.35f + 0.55f * u, 0.5f + 0.4f * (1f - Mathf.Abs(vz)), 1f));
                }
            }

            int stride = nx + 1;
            for (int iz = 0; iz < nz; iz++)
            {
                for (int ix = 0; ix < nx; ix++)
                {
                    int a = iz * stride + ix;
                    int b = a + 1;
                    int c = a + stride;
                    int d = c + 1;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            }

            var mesh = new Mesh { name = "LaCayHaiLaMam" };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(vcols);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            createdMeshes.Add(mesh);
            return mesh;
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
