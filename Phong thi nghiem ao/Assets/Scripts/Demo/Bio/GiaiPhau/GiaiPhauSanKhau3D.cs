using System;
using System.Collections.Generic;
using Khkt.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Khkt.Demo.Bio.GiaiPhau
{
    public enum CheDoMatCat
    {
        Tat = 0,
        CatDoc = 1,    // Sagittal (mặt phẳng đứng dọc X)
        CatNgang = 2,  // Transverse (mặt phẳng nằm ngang Y)
        CatTran = 3    // Coronal (mặt phẳng đứng ngang Z)
    }

    /// <summary>
    /// Gắn lên các collider vùng/cơ quan trong sân khấu giải phẫu để raycast nhận biết đúng id cơ quan (15 cơ quan).
    /// </summary>
    public class CoQuan3DMarker : MonoBehaviour
    {
        public string IdCoQuan;
        public string TenBoPhan;
    }

    /// <summary>
    /// Sân khấu giải phẫu người 3D trên lớp riêng (Layer 29, tâm (0, -520, 0)).
    /// Hỗ trợ: bật/tắt 8 hệ cơ quan + Da, thanh trượt Bóc lớp (0..1), Mặt cắt 3 chiều (shader MatCatGiaiPhau),
    /// chọn 15 cơ quan + Tách ra xoay riêng, hoạt ảnh Tim đập (60-120 bpm) + dòng máu đỏ/xanh + Phổi thở + Nhu động ruột.
    /// </summary>
    public class GiaiPhauSanKhau3D : MonoBehaviour
    {
        public const int AnatomyLayer = 29;
        public static readonly Vector3 StageOrigin = new Vector3(0f, -520f, 0f);

        static readonly Dictionary<string, Mesh> staticProcMeshCache = new Dictionary<string, Mesh>(48);
        static Shader cachedMatCatShader;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticMeshes()
        {
            staticProcMeshCache.Clear();
            cachedMatCatShader = null;
        }

        Transform stageRoot;
        Transform bodyFrameRoot;
        Transform lightRig;
        GameObject slicePlaneGizmo;
        Material gizmoMat, pedestalMat;

        // Trạng thái 8 hệ + Da
        readonly Dictionary<string, bool> heDangBat = new Dictionary<string, bool>(10)
        {
            { "xuong", true },
            { "he_co", true },
            { "tuan_hoan", true },
            { "ho_hap", true },
            { "tieu_hoa", true },
            { "than_kinh", true },
            { "bai_tiet", true },
            { "noi_tiet", true },
            { "da", true }
        };

        // Nhóm theo hệ để bật/tắt nhanh
        class PartEntry
        {
            public Renderer renderer;
            public Material material;
            public string idCoQuan;
            public string[] heSoHuu;
            public Color baseColor;
            public float smoothness;
            public bool isWaveDigestive;
        }

        class OrganGroupState
        {
            public string id;
            public Transform pivot;
            public Vector3 defaultLocalPos;
            public Vector3 defaultLocalScale;
            public Bounds localContentBounds;
            public readonly List<PartEntry> parts = new List<PartEntry>(16);
        }

        readonly List<PartEntry> allParts = new List<PartEntry>(96);
        readonly Dictionary<string, OrganGroupState> organGroups = new Dictionary<string, OrganGroupState>(16);
        readonly List<CoQuan3DMarker> allMarkers = new List<CoQuan3DMarker>(64);

        // Tham chiếu hoạt ảnh sinh lý
        readonly List<Transform> nhomTamNhi = new List<Transform>(4);
        readonly List<Transform> nhomTamThat = new List<Transform>(4);
        readonly List<Vector3> tamNhiBaseScale = new List<Vector3>(4);
        readonly List<Vector3> tamThatBaseScale = new List<Vector3>(4);
        Transform phoiTraiTr, phoiPhaiTr, coHoanhTr;
        Vector3 phoiTraiBaseScale, phoiPhaiBaseScale, coHoanhBaseLocalPos;

        // Hạt dòng máu tuần hoàn
        struct BloodParticle
        {
            public Transform tr;
            public Vector3[] path;
            public float t;
            public float speed;
            public bool isRed;
        }
        readonly List<BloodParticle> bloodParticles = new List<BloodParticle>(52);
        Material redBloodMat, blueBloodMat;

        // Trạng thái tương tác
        public float BocLop { get; private set; } = 0.72f; // mặc định bóc vừa đủ để thấy cả hệ xương/cơ mờ & nội tạng
        public CheDoMatCat CheDoCat { get; private set; } = CheDoMatCat.Tat;
        public float ViTriCat { get; private set; } = 0.0f; // -1 .. +1
        public float NhipTimBpm { get; private set; } = 75f;
        public bool BatDongMau { get; private set; } = true;
        public bool BatNhipTho { get; private set; } = true;
        public string CoQuanDangChon { get; private set; } = "";
        public bool DangTachRa { get; private set; } = false;

        public void GiaiPhongTaiNguyen()
        {
            for (int i = 0; i < allParts.Count; i++)
            {
                if (allParts[i].material != null) DestroyImmediate(allParts[i].material);
                allParts[i].material = null;
            }
            allParts.Clear();
            if (redBloodMat != null) { DestroyImmediate(redBloodMat); redBloodMat = null; }
            if (blueBloodMat != null) { DestroyImmediate(blueBloodMat); blueBloodMat = null; }
            if (gizmoMat != null) { DestroyImmediate(gizmoMat); gizmoMat = null; }
            if (pedestalMat != null) { DestroyImmediate(pedestalMat); pedestalMat = null; }
        }

        public Bounds BodyWorldBounds => new Bounds(StageOrigin + new Vector3(0f, 0.87f, 0f), new Vector3(0.68f, 1.72f, 0.36f));

        public Bounds LayKhungCoQuanDangChon()
        {
            if (!string.IsNullOrEmpty(CoQuanDangChon) && organGroups.TryGetValue(CoQuanDangChon, out var grp) && grp.parts.Count > 0)
            {
                bool first = true;
                Bounds b = default;
                foreach (var p in grp.parts)
                {
                    if (p.renderer == null || !p.renderer.enabled) continue;
                    if (first) { b = p.renderer.bounds; first = false; }
                    else b.Encapsulate(p.renderer.bounds);
                }
                if (!first) return b;
            }
            return BodyWorldBounds;
        }

        public static Shader TimShaderMatCat()
        {
            if (cachedMatCatShader != null) return cachedMatCatShader;
            var matRef = Resources.Load<Material>("MatCatGiaiPhauMat");
            if (matRef != null && matRef.shader != null)
            {
                cachedMatCatShader = matRef.shader;
                return cachedMatCatShader;
            }
            cachedMatCatShader = Shader.Find("Khkt/Bio/MatCatGiaiPhau");
            if (cachedMatCatShader == null)
                cachedMatCatShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            return cachedMatCatShader;
        }

        static Vector3 P(float xThree, float yThree, float zThree) => new Vector3(-xThree, yThree, zThree);

        public void KhoiTao(MucDoHoa muc)
        {
            if (stageRoot != null)
            {
                CapNhatHienThiTatCa();
                return;
            }

            stageRoot = transform;
            stageRoot.position = StageOrigin;
            gameObject.layer = AnatomyLayer;

            TaoAnhSangVaNen();

            var bodyGo = new GameObject("KhungCoTheBP3D");
            bodyGo.layer = AnatomyLayer;
            bodyFrameRoot = bodyGo.transform;
            bodyFrameRoot.SetParent(stageRoot, false);
            bodyFrameRoot.localPosition = Bp3dReader.BodyLocalOffset;
            bodyFrameRoot.localScale = Vector3.one * Bp3dReader.BodyScale;

            // 1. Ba lớp giải phẫu thật BodyParts3D + mạch máu ngoại vi
            DungLopBp3d("xuong", new[] { "xuong" }, Bp3dReader.LayLuoiLop("xuong", muc),
                new Color(0.90f, 0.87f, 0.78f), new Color(0.76f, 0.72f, 0.62f), 0.25f);
            DungVungChonLop("xuong", new[]
            {
                (P(0f, 3.24f, 0.01f), new Vector3(0.24f, 0.28f, 0.28f)),   // hộp sọ
                (P(0f, 2.52f, 0.03f), new Vector3(0.36f, 0.42f, 0.26f)),   // lồng ngực
                (P(0f, 1.72f, 0.02f), new Vector3(0.36f, 0.24f, 0.24f)),   // xương chậu
                (P(0.20f, 0.75f, 0.02f), new Vector3(0.16f, 0.90f, 0.16f)),
                (P(-0.20f, 0.75f, 0.02f), new Vector3(0.16f, 0.90f, 0.16f))
            });

            DungLopBp3d("co", new[] { "he_co" }, Bp3dReader.LayLuoiLop("co", muc),
                new Color(0.68f, 0.22f, 0.24f), new Color(0.55f, 0.14f, 0.16f), 0.35f);
            DungVungChonLop("co", new[]
            {
                (P(0f, 2.52f, 0.11f), new Vector3(0.40f, 0.45f, 0.20f)),
                (P(0.45f, 2.25f, 0.02f), new Vector3(0.16f, 0.65f, 0.16f)),
                (P(-0.45f, 2.25f, 0.02f), new Vector3(0.16f, 0.65f, 0.16f))
            });

            DungLopBp3d("da", new[] { "da" }, Bp3dReader.LayLuoiLop("da", muc),
                new Color(0.91f, 0.74f, 0.63f), new Color(0.75f, 0.48f, 0.40f), 0.28f);
            DungVungChonLop("da", new[]
            {
                (P(0f, 1.45f, 0.05f), new Vector3(0.44f, 0.60f, 0.28f))
            });

            // 2. Các cơ quan nội tạng và hệ mạch chuyên biệt theo tọa độ BodyParts3D
            DungHeTuanHoan(muc);
            DungHeHoHap();
            DungHeThanKinh();
            DungHeBaiTietVaNoiTiet();
            DungHeTieuHoa();

            TaoMatCatGizmo();
            CapNhatTinhToanToaDoOrganGroups();
            CapNhatHienThiTatCa();
        }

        void TaoAnhSangVaNen()
        {
            var rigGo = new GameObject("DenSanKhauGiaiPhau");
            rigGo.layer = AnatomyLayer;
            lightRig = rigGo.transform;
            lightRig.SetParent(stageRoot, false);

            var keyGo = new GameObject("DenChinh");
            keyGo.layer = AnatomyLayer;
            keyGo.transform.SetParent(lightRig, false);
            keyGo.transform.rotation = Quaternion.Euler(34f, -32f, 0f);
            var keyLight = keyGo.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.intensity = 1.15f;
            keyLight.color = new Color(1.0f, 0.98f, 0.95f);
            keyLight.cullingMask = 1 << AnatomyLayer;
            keyLight.shadows = LightShadows.None;

            var rimGo = new GameObject("DenVienSau");
            rimGo.layer = AnatomyLayer;
            rimGo.transform.SetParent(lightRig, false);
            rimGo.transform.position = new Vector3(-1.4f, 1.8f, -1.6f);
            var rimLight = rimGo.AddComponent<Light>();
            rimLight.type = LightType.Point;
            rimLight.range = 6f;
            rimLight.intensity = 1.6f;
            rimLight.color = new Color(0.55f, 0.80f, 1.0f);
            rimLight.cullingMask = 1 << AnatomyLayer;

            // Đế tròn đặt mẫu giải phẫu
            var baseMesh = GetOrCreatePrimitiveMesh("PedestalDisc", PrimitiveType.Cylinder);
            var discGo = new GameObject("DeSanKhau");
            discGo.layer = AnatomyLayer;
            discGo.transform.SetParent(stageRoot, false);
            discGo.transform.localPosition = new Vector3(0f, -0.02f, 0f);
            discGo.transform.localScale = new Vector3(1.10f, 0.02f, 1.10f);
            var mf = discGo.AddComponent<MeshFilter>();
            mf.sharedMesh = baseMesh;
            var mr = discGo.AddComponent<MeshRenderer>();
            var baseShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            pedestalMat = new Material(baseShader) { color = new Color(0.11f, 0.16f, 0.24f) };
            mr.sharedMaterial = pedestalMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
        }

        OrganGroupState EnsureOrganGroup(string idCoQuan)
        {
            if (organGroups.TryGetValue(idCoQuan, out var grp)) return grp;
            var go = new GameObject("CoQuan_" + idCoQuan);
            go.layer = AnatomyLayer;
            go.transform.SetParent(bodyFrameRoot, false);
            grp = new OrganGroupState
            {
                id = idCoQuan,
                pivot = go.transform,
                defaultLocalPos = Vector3.zero,
                defaultLocalScale = Vector3.one
            };
            organGroups[idCoQuan] = grp;
            return grp;
        }

        PartEntry AddMeshPart(string idCoQuan, string[] heSoHuu, string partName, Mesh mesh, Vector3 localPos, Quaternion localRot, Vector3 localScale,
            Color baseCol, Color cutCol, float smoothness = 0.45f, bool addCollider = true, bool waveDigestive = false, Color emission = default)
        {
            if (mesh == null) return null;
            var grp = EnsureOrganGroup(idCoQuan);
            var go = new GameObject(partName);
            go.layer = AnatomyLayer;
            go.transform.SetParent(grp.pivot, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = localScale;

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;

            var mat = new Material(TimShaderMatCat());
            mat.SetColor("_BaseColor", baseCol);
            mat.SetColor("_CrossSectionColor", cutCol);
            mat.SetColor("_RimColor", new Color(0.18f, 0.90f, 1.0f, 1f));
            mat.SetColor("_EmissionColor", emission);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetVector("_StageOrigin", new Vector4(StageOrigin.x, StageOrigin.y, StageOrigin.z, 0f));
            mr.sharedMaterial = mat;

            if (addCollider)
            {
                var sc = go.AddComponent<SphereCollider>();
                sc.center = mesh.bounds.center;
                sc.radius = Mathf.Max(0.035f, mesh.bounds.extents.magnitude * 0.85f);
                var mk = go.AddComponent<CoQuan3DMarker>();
                mk.IdCoQuan = idCoQuan;
                mk.TenBoPhan = partName;
                allMarkers.Add(mk);
            }

            var entry = new PartEntry
            {
                renderer = mr,
                material = mat,
                idCoQuan = idCoQuan,
                heSoHuu = heSoHuu,
                baseColor = baseCol,
                smoothness = smoothness,
                isWaveDigestive = waveDigestive
            };
            grp.parts.Add(entry);
            allParts.Add(entry);
            return entry;
        }

        void DungLopBp3d(string idCoQuan, string[] heSoHuu, Mesh mesh, Color baseCol, Color cutCol, float smoothness)
        {
            if (mesh == null) return;
            AddMeshPart(idCoQuan, heSoHuu, "BP3D_" + idCoQuan, mesh, Vector3.zero, Quaternion.identity, Vector3.one,
                baseCol, cutCol, smoothness, addCollider: false);
        }

        void DungVungChonLop(string idCoQuan, (Vector3 pos, Vector3 size)[] regions)
        {
            var grp = EnsureOrganGroup(idCoQuan);
            for (int i = 0; i < regions.Length; i++)
            {
                var go = new GameObject($"VungChon_{idCoQuan}_{i}");
                go.layer = AnatomyLayer;
                go.transform.SetParent(grp.pivot, false);
                go.transform.localPosition = regions[i].pos;
                var bc = go.AddComponent<BoxCollider>();
                bc.size = regions[i].size;
                var mk = go.AddComponent<CoQuan3DMarker>();
                mk.IdCoQuan = idCoQuan;
                mk.TenBoPhan = idCoQuan;
                allMarkers.Add(mk);
            }
        }

        void DungHeTuanHoan(MucDoHoa muc)
        {
            // Tim 4 ngăn tại tâm giải phẫu TIM = (-0.04, 2.55, 0.06) trong không gian Unity BP3D
            Vector3 timCenter = P(0.04f, 2.55f, 0.06f);

            Mesh triLeftVent = GetOrCreateDeformedSphere("Tim_ThatTrai", new Vector3(0.070f, 0.105f, 0.068f), 28, 20, v =>
            {
                float taper = 0.45f + 0.55f * ((v.y + 1f) * 0.5f);
                return new Vector3(v.x * taper, v.y, v.z * taper);
            });
            Mesh triRightVent = GetOrCreateDeformedSphere("Tim_ThatPhai", new Vector3(0.064f, 0.088f, 0.056f), 24, 18, v =>
            {
                float taper = 0.50f + 0.50f * ((v.y + 1f) * 0.5f);
                return new Vector3(v.x * taper, v.y, v.z * taper);
            });
            Mesh atriumMesh = GetOrCreateDeformedSphere("Tim_TamNhi", new Vector3(0.050f, 0.048f, 0.048f), 20, 14, null);

            var eTT = AddMeshPart("tim", new[] { "tuan_hoan" }, "tam_that_trai", triLeftVent,
                timCenter + P(0.035f, -0.035f, -0.005f), Quaternion.Euler(0, 0, -28f), Vector3.one,
                new Color(0.76f, 0.10f, 0.19f), new Color(0.55f, 0.09f, 0.14f), 0.58f);
            var eTP = AddMeshPart("tim", new[] { "tuan_hoan" }, "tam_that_phai", triRightVent,
                timCenter + P(-0.030f, -0.025f, 0.040f), Quaternion.Euler(0, 0, -24f), Vector3.one,
                new Color(0.62f, 0.13f, 0.26f), new Color(0.45f, 0.10f, 0.18f), 0.55f);
            var eNT = AddMeshPart("tim", new[] { "tuan_hoan" }, "tam_nhi_trai", atriumMesh,
                timCenter + P(0.030f, 0.065f, -0.050f), Quaternion.identity, Vector3.one,
                new Color(0.80f, 0.17f, 0.27f), new Color(0.58f, 0.14f, 0.22f), 0.52f);
            var eNP = AddMeshPart("tim", new[] { "tuan_hoan" }, "tam_nhi_phai", atriumMesh,
                timCenter + P(-0.065f, 0.050f, 0.005f), Quaternion.identity, new Vector3(1.05f, 1.05f, 1.05f),
                new Color(0.52f, 0.13f, 0.28f), new Color(0.38f, 0.11f, 0.22f), 0.52f);

            if (eTT != null) { nhomTamThat.Add(eTT.renderer.transform); tamThatBaseScale.Add(Vector3.one); }
            if (eTP != null) { nhomTamThat.Add(eTP.renderer.transform); tamThatBaseScale.Add(Vector3.one); }
            if (eNT != null) { nhomTamNhi.Add(eNT.renderer.transform); tamNhiBaseScale.Add(Vector3.one); }
            if (eNP != null) { nhomTamNhi.Add(eNP.renderer.transform); tamNhiBaseScale.Add(new Vector3(1.05f, 1.05f, 1.05f)); }

            Vector3 dTim(float x, float y, float z) => timCenter + P(x, y, z);

            // Cuống động mạch gắn liền với khối tim (hiển thị rõ cửa ra của máu khi Tách riêng Tim)
            var stubDmc = new[] { dTim(0.01f, 0.04f, 0f), dTim(-0.008f, 0.13f, 0.01f), dTim(0.01f, 0.185f, -0.025f) };
            var stubDmp = new[] { dTim(-0.01f, 0.05f, 0.05f), dTim(0.015f, 0.12f, 0.03f), dTim(0.015f, 0.15f, -0.01f) };
            AddMeshPart("tim", new[] { "tuan_hoan" }, "cuong_dong_mach_chu",
                GetOrCreateTubeMesh("Tim_StubDMC", stubDmc, 0.019f, 14, 10),
                Vector3.zero, Quaternion.identity, Vector3.one, new Color(0.92f, 0.16f, 0.22f), new Color(0.72f, 0.12f, 0.16f), 0.55f, addCollider: false);
            AddMeshPart("tim", new[] { "tuan_hoan" }, "cuong_dong_mach_phoi",
                GetOrCreateTubeMesh("Tim_StubDMP", stubDmp, 0.016f, 12, 10),
                Vector3.zero, Quaternion.identity, Vector3.one, new Color(0.24f, 0.48f, 0.94f), new Color(0.18f, 0.35f, 0.72f), 0.50f, addCollider: false);

            // Động mạch chủ, động mạch phổi, tĩnh mạch phổi, tĩnh mạch chủ
            var dmcPts = new[] {
                dTim(0f, 0.06f, 0f), dTim(-0.012f, 0.16f, 0.01f), dTim(0f, 0.215f, -0.025f),
                dTim(0.035f, 0.195f, -0.085f), dTim(0.035f, 0.08f, -0.12f),
                P(0.03f, 2.30f, -0.12f), P(0.03f, 2.05f, -0.115f), P(0.025f, 1.78f, -0.10f)
            };
            AddMeshPart("mach_mau", new[] { "tuan_hoan" }, "dong_mach_chu",
                GetOrCreateTubeMesh("Mach_DongMachChu", dmcPts, 0.019f, 44, 12),
                Vector3.zero, Quaternion.identity, Vector3.one, new Color(0.95f, 0.15f, 0.22f), new Color(0.75f, 0.12f, 0.16f), 0.55f);

            var dmpPts = new[] { dTim(-0.01f, 0.06f, 0.06f), dTim(0.02f, 0.14f, 0.035f), dTim(0.015f, 0.165f, -0.02f), P(0.14f, 2.66f, -0.02f) };
            AddMeshPart("mach_mau", new[] { "tuan_hoan" }, "dong_mach_phoi_trai",
                GetOrCreateTubeMesh("Mach_DMPhoiT", dmpPts, 0.013f, 24, 10),
                Vector3.zero, Quaternion.identity, Vector3.one, new Color(0.24f, 0.48f, 0.94f), new Color(0.18f, 0.35f, 0.72f), 0.5f);

            var dmpRPts = new[] { dTim(0.015f, 0.165f, -0.02f), P(-0.06f, 2.70f, -0.04f), P(-0.15f, 2.68f, -0.02f) };
            AddMeshPart("mach_mau", new[] { "tuan_hoan" }, "dong_mach_phoi_phai",
                GetOrCreateTubeMesh("Mach_DMPhoiP", dmpRPts, 0.011f, 16, 10),
                Vector3.zero, Quaternion.identity, Vector3.one, new Color(0.24f, 0.48f, 0.94f), new Color(0.18f, 0.35f, 0.72f), 0.5f);

            var tmcTren = new[] { P(-0.035f, 3.22f, 0f), P(-0.055f, 2.95f, -0.005f), dTim(-0.07f, 0.11f, 0.005f), dTim(-0.065f, 0.06f, 0.005f) };
            var tmcDuoi = new[] { P(-0.045f, 1.78f, -0.08f), P(-0.05f, 2.08f, -0.085f), dTim(-0.072f, -0.06f, -0.01f), dTim(-0.065f, 0.03f, 0.005f) };
            AddMeshPart("mach_mau", new[] { "tuan_hoan" }, "tinh_mach_chu_tren",
                GetOrCreateTubeMesh("Mach_TMCT", tmcTren, 0.014f, 22, 10),
                Vector3.zero, Quaternion.identity, Vector3.one, new Color(0.16f, 0.39f, 0.92f), new Color(0.12f, 0.28f, 0.68f), 0.48f);
            AddMeshPart("mach_mau", new[] { "tuan_hoan" }, "tinh_mach_chu_duoi",
                GetOrCreateTubeMesh("Mach_TMCD", tmcDuoi, 0.016f, 24, 10),
                Vector3.zero, Quaternion.identity, Vector3.one, new Color(0.16f, 0.39f, 0.92f), new Color(0.12f, 0.28f, 0.68f), 0.48f);

            // Động mạch & tĩnh mạch ngoại vi thật từ BodyParts3D
            var machDo = Bp3dReader.LayLuoiLop("mach_do", muc);
            var machXanh = Bp3dReader.LayLuoiLop("mach_xanh", muc);
            if (machDo != null)
                AddMeshPart("mach_mau", new[] { "tuan_hoan" }, "dong_mach_ngoai_vi", machDo,
                    Vector3.zero, Quaternion.identity, Vector3.one, new Color(0.88f, 0.16f, 0.22f), new Color(0.68f, 0.12f, 0.16f), 0.45f, addCollider: false);
            if (machXanh != null)
                AddMeshPart("mach_mau", new[] { "tuan_hoan" }, "tinh_mach_ngoai_vi", machXanh,
                    Vector3.zero, Quaternion.identity, Vector3.one, new Color(0.18f, 0.42f, 0.88f), new Color(0.14f, 0.30f, 0.66f), 0.42f, addCollider: false);

            // Hạt dòng máu đỏ (giàu O2) & xanh (nghèo O2) chảy qua tim và mạch lớn
            TaoHatDongMau(dTim, P);
        }

        void TaoHatDongMau(Func<float, float, float, Vector3> dTim, Func<float, float, float, Vector3> pFn)
        {
            var urp = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            redBloodMat = new Material(urp) { color = new Color(1.0f, 0.20f, 0.24f) };
            redBloodMat.SetColor("_EmissionColor", new Color(0.65f, 0.06f, 0.06f));
            blueBloodMat = new Material(urp) { color = new Color(0.24f, 0.60f, 1.0f) };
            blueBloodMat.SetColor("_EmissionColor", new Color(0.05f, 0.22f, 0.55f));

            var cellMesh = GetOrCreatePrimitiveMesh("BloodCellSphere", PrimitiveType.Sphere);

            // Đường máu đỏ: Phổi -> Tâm nhĩ trái -> Tâm thất trái -> Cung ĐMC -> ĐMC xuống
            Vector3[] oxPath = {
                pFn(0.14f, 2.60f, -0.04f), dTim(0.05f, 0.065f, -0.05f), dTim(0.03f, 0.055f, -0.03f),
                dTim(0.035f, -0.03f, -0.005f), dTim(0.0f, 0.06f, 0f), dTim(0f, 0.21f, -0.025f),
                dTim(0.035f, 0.19f, -0.09f), pFn(0.03f, 2.30f, -0.12f), pFn(0.03f, 1.82f, -0.10f)
            };
            // Đường máu xanh: TMC trên/dưới -> Tâm nhĩ phải -> Tâm thất phải -> ĐM phổi -> Phổi
            Vector3[] deoxPath = {
                pFn(-0.045f, 1.85f, -0.08f), pFn(-0.055f, 2.25f, -0.05f), dTim(-0.065f, 0.05f, 0.005f),
                dTim(-0.03f, -0.025f, 0.04f), dTim(-0.01f, 0.06f, 0.06f), dTim(0.02f, 0.14f, 0.035f),
                dTim(0.015f, 0.165f, -0.02f), pFn(0.14f, 2.66f, -0.02f)
            };

            var bloodParent = EnsureOrganGroup("tim").pivot;
            for (int i = 0; i < 16; i++)
            {
                var go = new GameObject("HatMauDo_" + i);
                go.layer = AnatomyLayer;
                go.transform.SetParent(bloodParent, false);
                go.transform.localScale = Vector3.one * 0.018f;
                go.AddComponent<MeshFilter>().sharedMesh = cellMesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = redBloodMat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                bloodParticles.Add(new BloodParticle { tr = go.transform, path = oxPath, t = i / 16f, speed = 0.24f, isRed = true });
            }
            for (int i = 0; i < 14; i++)
            {
                var go = new GameObject("HatMauXanh_" + i);
                go.layer = AnatomyLayer;
                go.transform.SetParent(bloodParent, false);
                go.transform.localScale = Vector3.one * 0.018f;
                go.AddComponent<MeshFilter>().sharedMesh = cellMesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = blueBloodMat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                bloodParticles.Add(new BloodParticle { tr = go.transform, path = deoxPath, t = i / 14f, speed = 0.22f, isRed = false });
            }
        }

        void DungHeHoHap()
        {
            Mesh taoPhoi(float dau) => GetOrCreateDeformedSphere("Phoi_" + (dau > 0 ? "L" : "R"),
                new Vector3(0.130f, 0.268f, 0.152f), 28, 20, v =>
                {
                    if (v.y > 0f) { float s = 1f - 0.45f * v.y * v.y; v.x *= s; v.z *= s; }
                    if (v.y < -0.55f) v.y = -0.55f - (v.y + 0.55f) * 0.35f + 0.12f * (v.x * v.x + v.z * v.z);
                    if (v.x * dau > 0f) v.x *= 0.55f; // lật X so với HTML
                    return v;
                });

            var eL = AddMeshPart("phoi", new[] { "ho_hap" }, "phoi_trai", taoPhoi(1f),
                P(0.19f, 2.62f, -0.01f), Quaternion.identity, Vector3.one,
                new Color(0.95f, 0.62f, 0.69f), new Color(0.84f, 0.40f, 0.48f), 0.32f);
            var eR = AddMeshPart("phoi", new[] { "ho_hap" }, "phoi_phai", taoPhoi(-1f),
                P(-0.19f, 2.64f, -0.01f), Quaternion.identity, Vector3.one,
                new Color(0.95f, 0.64f, 0.71f), new Color(0.84f, 0.40f, 0.48f), 0.32f);
            phoiTraiTr = eL?.renderer.transform; phoiTraiBaseScale = Vector3.one;
            phoiPhaiTr = eR?.renderer.transform; phoiPhaiBaseScale = Vector3.one;

            var kqPts = new[] { P(0f, 3.09f, 0.036f), P(0f, 2.95f, 0.022f), P(0f, 2.77f, 0.0f) };
            AddMeshPart("phoi", new[] { "ho_hap" }, "khi_quan",
                GetOrCreateTubeMesh("Phoi_KhiQuan", kqPts, 0.022f, 18, 12),
                Vector3.zero, Quaternion.identity, Vector3.one, new Color(0.86f, 0.90f, 0.94f), new Color(0.62f, 0.68f, 0.74f), 0.35f);

            var pqL = new[] { P(0f, 2.77f, 0f), P(0.13f, 2.67f, -0.01f), P(0.19f, 2.58f, -0.02f) };
            var pqR = new[] { P(0f, 2.77f, 0f), P(-0.13f, 2.69f, -0.01f), P(-0.19f, 2.60f, -0.02f) };
            AddMeshPart("phoi", new[] { "ho_hap" }, "phe_quan_trai",
                GetOrCreateTubeMesh("Phoi_PQL", pqL, 0.013f, 12, 8),
                Vector3.zero, Quaternion.identity, Vector3.one, new Color(0.90f, 0.93f, 0.96f), new Color(0.65f, 0.70f, 0.75f), 0.3f, addCollider: false);
            AddMeshPart("phoi", new[] { "ho_hap" }, "phe_quan_phai",
                GetOrCreateTubeMesh("Phoi_PQR", pqR, 0.014f, 12, 8),
                Vector3.zero, Quaternion.identity, Vector3.one, new Color(0.90f, 0.93f, 0.96f), new Color(0.65f, 0.70f, 0.75f), 0.3f, addCollider: false);

            var coHoanhMesh = GetOrCreateDeformedSphere("Phoi_CoHoanh", new Vector3(0.32f, 0.09f, 0.20f), 22, 12, v =>
            {
                if (v.y < 0f) v.y *= 0.25f;
                return v;
            });
            var eHoanh = AddMeshPart("phoi", new[] { "ho_hap" }, "co_hoanh", coHoanhMesh,
                P(0f, 2.30f, -0.01f), Quaternion.identity, Vector3.one,
                new Color(0.71f, 0.36f, 0.30f), new Color(0.55f, 0.24f, 0.20f), 0.30f);
            coHoanhTr = eHoanh?.renderer.transform;
            if (coHoanhTr != null) coHoanhBaseLocalPos = coHoanhTr.localPosition;
        }

        void DungHeThanKinh()
        {
            Mesh taoBanCau(float dau) => GetOrCreateDeformedSphere("Nao_" + (dau > 0 ? "L" : "R"),
                new Vector3(0.074f, 0.094f, 0.150f), 28, 20, v =>
                {
                    if (v.x * dau > 0f) v.x *= 0.35f;
                    if (v.y < -0.3f) v.y = -0.3f + (v.y + 0.3f) * 0.6f;
                    float n = Mathf.Sin(v.x * 9f + 1.3f) * Mathf.Sin(v.y * 10.5f + 0.7f) * Mathf.Sin(v.z * 8.5f + 2.1f);
                    return v * (1f + 0.042f * n);
                });

            AddMeshPart("nao", new[] { "than_kinh" }, "dai_nao_trai", taoBanCau(1f),
                P(0.077f, 3.34f, -0.01f), Quaternion.identity, Vector3.one, new Color(0.91f, 0.71f, 0.74f), new Color(0.84f, 0.72f, 0.76f), 0.38f);
            AddMeshPart("nao", new[] { "than_kinh" }, "dai_nao_phai", taoBanCau(-1f),
                P(-0.077f, 3.34f, -0.01f), Quaternion.identity, Vector3.one, new Color(0.91f, 0.71f, 0.74f), new Color(0.84f, 0.72f, 0.76f), 0.38f);

            Mesh tieuNaoMesh = GetOrCreateDeformedSphere("TieuNao", new Vector3(0.088f, 0.050f, 0.062f), 22, 16,
                v => v * (1f + 0.045f * Mathf.Sin(v.y * 36f)));
            AddMeshPart("nao", new[] { "than_kinh" }, "tieu_nao", tieuNaoMesh,
                P(0f, 3.20f, -0.115f), Quaternion.identity, Vector3.one, new Color(0.85f, 0.60f, 0.66f), new Color(0.75f, 0.50f, 0.56f), 0.35f);

            var thanNaoPts = new[] { P(0f, 3.30f, -0.03f), P(0f, 3.20f, -0.05f), P(0f, 3.08f, -0.07f) };
            AddMeshPart("nao", new[] { "than_kinh" }, "than_nao",
                GetOrCreateTubeMesh("Nao_ThanNao", thanNaoPts, 0.024f, 16, 10),
                Vector3.zero, Quaternion.identity, Vector3.one, new Color(0.85f, 0.63f, 0.67f), new Color(0.74f, 0.55f, 0.60f), 0.35f);

            // Tủy sống và dây thần kinh
            var tuyPts = new[] { P(0f, 3.08f, -0.07f), P(0f, 2.90f, -0.12f), P(0f, 2.60f, -0.145f), P(0f, 2.25f, -0.15f), P(0f, 1.95f, -0.13f), P(0f, 1.75f, -0.12f) };
            AddMeshPart("tuy_song", new[] { "than_kinh" }, "tuy_song",
                GetOrCreateTubeMesh("ThanKinh_TuySong", tuyPts, 0.012f, 36, 10),
                Vector3.zero, Quaternion.identity, Vector3.one, new Color(0.92f, 0.83f, 0.45f), new Color(0.80f, 0.72f, 0.35f), 0.40f);

            foreach (float s in new[] { 1f, -1f })
            {
                var canhTayPts = new[] { P(0f, 2.95f, -0.11f), P(0.18f * s, 2.86f, -0.05f), P(0.42f * s, 2.75f, 0f), P(0.49f * s, 2.20f, 0.02f) };
                var hongToPts = new[] { P(0f, 1.78f, -0.12f), P(0.14f * s, 1.56f, -0.05f), P(0.17f * s, 0.90f, -0.03f), P(0.17f * s, 0.30f, -0.03f) };
                AddMeshPart("tuy_song", new[] { "than_kinh" }, "day_than_kinh_tay_" + (s > 0 ? "L" : "R"),
                    GetOrCreateTubeMesh("TK_Tay_" + s, canhTayPts, 0.0045f, 18, 6),
                    Vector3.zero, Quaternion.identity, Vector3.one, new Color(0.95f, 0.88f, 0.50f), new Color(0.8f, 0.72f, 0.38f), 0.3f, addCollider: false);
                AddMeshPart("tuy_song", new[] { "than_kinh" }, "day_than_kinh_chan_" + (s > 0 ? "L" : "R"),
                    GetOrCreateTubeMesh("TK_Chan_" + s, hongToPts, 0.0055f, 22, 6),
                    Vector3.zero, Quaternion.identity, Vector3.one, new Color(0.95f, 0.88f, 0.50f), new Color(0.8f, 0.72f, 0.38f), 0.3f, addCollider: false);
            }
        }

        void DungHeBaiTietVaNoiTiet()
        {
            foreach (float s in new[] { 1f, -1f })
            {
                bool isLeft = s > 0f;
                Vector3 posThan = P(0.135f * s, isLeft ? 2.08f : 2.03f, -0.10f);
                Mesh beanMesh = GetOrCreateDeformedSphere("Than_" + (isLeft ? "L" : "R"),
                    new Vector3(0.055f, 0.095f, 0.045f), 22, 16, v =>
                    {
                        // mặt lõm hướng vào rốn thận
                        float ron = Mathf.Max(0f, v.x * s);
                        v.x -= s * 0.45f * ron * Mathf.Exp(-(v.y * v.y) / 0.14f);
                        return v;
                    });
                AddMeshPart("than", new[] { "bai_tiet" }, isLeft ? "than_trai" : "than_phai", beanMesh,
                    posThan, Quaternion.Euler(0, 0, -s * 10f), Vector3.one,
                    new Color(0.56f, 0.18f, 0.23f), new Color(0.45f, 0.12f, 0.18f), 0.52f);

                // Tuyến thượng thận nằm trên 2 cực thận (thuộc cả bài tiết & nội tiết)
                Mesh adrenalMesh = GetOrCreateDeformedSphere("ThuongThan", new Vector3(0.030f, 0.025f, 0.022f), 14, 10, null);
                AddMeshPart("than", new[] { "noi_tiet", "bai_tiet" }, "tuyen_thuong_than_" + (isLeft ? "L" : "R"), adrenalMesh,
                    posThan + P(-0.012f * s, 0.102f, 0f), Quaternion.identity, Vector3.one,
                    new Color(0.92f, 0.74f, 0.28f), new Color(0.75f, 0.58f, 0.20f), 0.42f);

                var nqPts = new[] { posThan + P(-0.03f * s, -0.02f, 0.01f), P(0.085f * s, 1.88f, -0.08f), P(0.06f * s, 1.73f, -0.03f), P(0.03f * s, 1.66f, 0.03f) };
                AddMeshPart("than", new[] { "bai_tiet" }, "nieu_quan_" + (isLeft ? "L" : "R"),
                    GetOrCreateTubeMesh("NieuQuan_" + s, nqPts, 0.0055f, 22, 8),
                    Vector3.zero, Quaternion.identity, Vector3.one, new Color(0.97f, 0.83f, 0.35f), new Color(0.80f, 0.65f, 0.25f), 0.4f, addCollider: false);
            }

            Mesh bqMesh = GetOrCreateDeformedSphere("BangQuang", new Vector3(0.065f, 0.055f, 0.060f), 18, 14, null);
            AddMeshPart("than", new[] { "bai_tiet" }, "bang_quang", bqMesh,
                P(0f, 1.66f, 0.05f), Quaternion.identity, Vector3.one,
                new Color(0.91f, 0.77f, 0.42f), new Color(0.74f, 0.60f, 0.30f), 0.40f);

            // Tuyến giáp & tuyến yên cho hệ Nội tiết rõ nét hơn
            Mesh giapMesh = GetOrCreateDeformedSphere("TuyenGiap", new Vector3(0.044f, 0.032f, 0.024f), 16, 12,
                v => new Vector3(v.x, v.y * (1f - 0.35f * Mathf.Exp(-v.x * v.x * 6f)), v.z));
            AddMeshPart("nao", new[] { "noi_tiet" }, "tuyen_giap", giapMesh,
                P(0f, 3.01f, 0.048f), Quaternion.identity, Vector3.one,
                new Color(0.96f, 0.66f, 0.25f), new Color(0.78f, 0.50f, 0.18f), 0.45f);
        }

        void DungHeTieuHoa()
        {
            // Gan & Túi mật
            Mesh ganMesh = GetOrCreateDeformedSphere("GanMesh", new Vector3(0.20f, 0.10f, 0.13f), 26, 18, v =>
            {
                float k = 0.55f + 0.45f * ((1f + v.x) * 0.5f);
                v.y *= k;
                v.z *= 0.7f + 0.3f * ((1f + v.x) * 0.5f);
                if (v.y < 0f) v.y *= 0.65f;
                return v;
            });
            AddMeshPart("gan", new[] { "tieu_hoa" }, "gan", ganMesh,
                P(-0.10f, 2.25f, 0.03f), Quaternion.Euler(0, 0, 5f), Vector3.one,
                new Color(0.50f, 0.15f, 0.15f), new Color(0.40f, 0.11f, 0.11f), 0.62f);

            Mesh tuiMatMesh = GetOrCreateDeformedSphere("TuiMatMesh", new Vector3(0.022f, 0.036f, 0.022f), 14, 10, null);
            AddMeshPart("gan", new[] { "tieu_hoa" }, "tui_mat", tuiMatMesh,
                P(-0.06f, 2.17f, 0.115f), Quaternion.Euler(35f, 0, 0), Vector3.one,
                new Color(0.28f, 0.62f, 0.36f), new Color(0.20f, 0.45f, 0.26f), 0.55f, addCollider: false);

            // Thực quản + Dạ dày (có sóng nhu động)
            var tqPts = new[] { P(0f, 3.05f, -0.035f), P(0f, 2.80f, -0.05f), P(0.01f, 2.55f, -0.07f), P(0.045f, 2.36f, 0f) };
            AddMeshPart("da_day", new[] { "tieu_hoa" }, "thuc_quan",
                GetOrCreateTubeMesh("TH_ThucQuan", tqPts, 0.011f, 24, 8),
                Vector3.zero, Quaternion.identity, Vector3.one, new Color(0.91f, 0.62f, 0.62f), new Color(0.75f, 0.46f, 0.46f), 0.42f, addCollider: false);

            var ddPts = new[] { P(0.055f, 2.33f, 0.02f), P(0.125f, 2.31f, 0.045f), P(0.165f, 2.22f, 0.065f), P(0.135f, 2.11f, 0.09f), P(0.06f, 2.08f, 0.10f), P(-0.015f, 2.11f, 0.09f) };
            AddMeshPart("da_day", new[] { "tieu_hoa" }, "da_day",
                GetOrCreateTubeMesh("TH_DaDay", ddPts, 0.046f, 34, 14, u => 0.42f + 0.85f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(u * 1.1f))),
                Vector3.zero, Quaternion.identity, Vector3.one, new Color(0.85f, 0.49f, 0.52f), new Color(0.68f, 0.33f, 0.36f), 0.50f, waveDigestive: true);

            // Tuyến tụy (hệ tiêu hóa & hệ nội tiết)
            var tuyPts = new[] { P(-0.02f, 2.02f, 0.05f), P(0.04f, 2.06f, 0.025f), P(0.11f, 2.12f, -0.005f), P(0.16f, 2.17f, -0.03f) };
            AddMeshPart("tuy", new[] { "tieu_hoa", "noi_tiet" }, "tuyen_tuy",
                GetOrCreateTubeMesh("TH_Tuy", tuyPts, 0.020f, 22, 10, u => 1f - 0.45f * u),
                Vector3.zero, Quaternion.identity, Vector3.one, new Color(0.96f, 0.82f, 0.38f), new Color(0.78f, 0.65f, 0.25f), 0.38f);

            // Lách
            Mesh lachMesh = GetOrCreateDeformedSphere("LachMesh", new Vector3(0.042f, 0.072f, 0.036f), 18, 12, null);
            AddMeshPart("lach", new[] { "tieu_hoa" }, "lach", lachMesh,
                P(0.185f, 2.22f, -0.05f), Quaternion.Euler(6f, 18f, 10f), Vector3.one,
                new Color(0.43f, 0.12f, 0.20f), new Color(0.32f, 0.09f, 0.15f), 0.58f);

            // Ruột non (các quai uốn lượn + sóng nhu động)
            var rnPts = new List<Vector3>(64) { P(0.02f, 1.97f, 0.045f) };
            for (int tier = 0; tier < 7; tier++)
            {
                float y = 1.94f - tier * 0.040f;
                bool dir = (tier % 2) == 0;
                for (int k = 0; k <= 4; k++)
                {
                    float frac = k / 4f;
                    float x = (dir ? Mathf.Lerp(0.088f, -0.088f, frac) : Mathf.Lerp(-0.088f, 0.088f, frac)) * (1f - 0.08f * tier);
                    float z = 0.055f + 0.035f * Mathf.Sin(k * 1.6f + tier * 1.3f);
                    rnPts.Add(P(x, y + 0.008f * Mathf.Sin(k * 2.1f + tier), z));
                }
            }
            AddMeshPart("ruot_non", new[] { "tieu_hoa" }, "ruot_non",
                GetOrCreateTubeMesh("TH_RuotNon", rnPts.ToArray(), 0.0135f, 110, 9),
                Vector3.zero, Quaternion.identity, Vector3.one, new Color(0.86f, 0.49f, 0.43f), new Color(0.68f, 0.35f, 0.30f), 0.46f, waveDigestive: true);

            // Ruột già (khung đại tràng chữ U ngược + sóng nhu động)
            var rgPts = new[] {
                P(-0.118f, 1.65f, 0.05f), P(-0.122f, 1.84f, 0.05f), P(-0.115f, 2.02f, 0.045f),
                P(-0.045f, 2.03f, 0.075f), P(0.045f, 2.02f, 0.085f), P(0.120f, 2.04f, 0.055f),
                P(0.126f, 1.85f, 0.045f), P(0.115f, 1.68f, 0.045f), P(0.060f, 1.58f, 0.035f), P(0f, 1.51f, -0.01f)
            };
            AddMeshPart("ruot_gia", new[] { "tieu_hoa" }, "dai_trang",
                GetOrCreateTubeMesh("TH_RuotGia", rgPts, 0.021f, 54, 10, u => 0.90f + 0.16f * Mathf.Abs(Mathf.Sin(u * Mathf.PI * 18f))),
                Vector3.zero, Quaternion.identity, Vector3.one, new Color(0.78f, 0.51f, 0.47f), new Color(0.60f, 0.36f, 0.33f), 0.40f, waveDigestive: true);
        }

        void TaoMatCatGizmo()
        {
            slicePlaneGizmo = new GameObject("KhungMatCat") { layer = AnatomyLayer };
            slicePlaneGizmo.transform.SetParent(stageRoot, false);

            var urp = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            gizmoMat = new Material(urp) { color = new Color(0.25f, 0.90f, 1.0f, 1f) };

            var barMesh = GetOrCreatePrimitiveMesh("SliceFrameBar", PrimitiveType.Cube);
            void AddEdge(string n, Vector3 pos, Vector3 scl)
            {
                var bar = new GameObject(n) { layer = AnatomyLayer };
                bar.transform.SetParent(slicePlaneGizmo.transform, false);
                bar.transform.localPosition = pos;
                bar.transform.localScale = scl;
                bar.AddComponent<MeshFilter>().sharedMesh = barMesh;
                var mr = bar.AddComponent<MeshRenderer>();
                mr.sharedMaterial = gizmoMat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
            }
            const float t = 0.005f;
            AddEdge("Top", new Vector3(0f, 0.5f, 0f), new Vector3(1f, t, t));
            AddEdge("Bot", new Vector3(0f, -0.5f, 0f), new Vector3(1f, t, t));
            AddEdge("Left", new Vector3(-0.5f, 0f, 0f), new Vector3(t, 1f, t));
            AddEdge("Right", new Vector3(0.5f, 0f, 0f), new Vector3(t, 1f, t));
            slicePlaneGizmo.SetActive(false);
        }

        void CapNhatTinhToanToaDoOrganGroups()
        {
            foreach (var kv in organGroups)
            {
                var grp = kv.Value;
                bool any = false;
                Bounds b = default;
                foreach (var p in grp.parts)
                {
                    if (p.renderer == null) continue;
                    var mf = p.renderer.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    var mb = mf.sharedMesh.bounds;
                    Vector3 center = p.renderer.transform.localPosition + Vector3.Scale(p.renderer.transform.localRotation * mb.center, p.renderer.transform.localScale);
                    Vector3 size = Vector3.Scale(mb.size, p.renderer.transform.localScale);
                    var lb = new Bounds(center, size);
                    if (!any) { b = lb; any = true; }
                    else b.Encapsulate(lb);
                }
                if (any) grp.localContentBounds = b;
            }
        }

        // ==================== API ĐIỀU KHIỂN CÔNG KHAI ====================

        public bool LayTrangThaiHe(string heId) => heDangBat.TryGetValue(heId, out bool v) && v;

        public void DatHe(string heId, bool bat)
        {
            if (string.IsNullOrEmpty(heId)) return;
            heDangBat[heId] = bat;
            CapNhatHienThiTatCa();
        }

        public void DatBocLop(float giaTri)
        {
            BocLop = Mathf.Clamp01(giaTri);
            CapNhatHienThiTatCa();
        }

        public void DatMatCat(CheDoMatCat cheDo, float viTri)
        {
            CheDoCat = cheDo;
            ViTriCat = Mathf.Clamp(viTri, -1f, 1f);
            CapNhatMatCatShader();
        }

        public void DatNhipTim(float bpm)
        {
            NhipTimBpm = Mathf.Clamp(bpm, 60f, 120f);
        }

        public void DatHoatAnh(bool dongMau, bool nhipTho)
        {
            BatDongMau = dongMau;
            BatNhipTho = nhipTho;
            for (int i = 0; i < bloodParticles.Count; i++)
            {
                if (bloodParticles[i].tr != null)
                    bloodParticles[i].tr.gameObject.SetActive(dongMau && heDangBat["tuan_hoan"] && (!DangTachRa || CoQuanDangChon == "tim"));
            }
        }

        public void ChonCoQuan(string idCoQuan)
        {
            CoQuanDangChon = idCoQuan ?? "";
            if (DangTachRa && !string.IsNullOrEmpty(CoQuanDangChon))
                ApDungViTriTachRa();
            CapNhatHighlightCoQuan();
        }

        public void DatTachRa(bool tach)
        {
            if (tach && string.IsNullOrEmpty(CoQuanDangChon))
                CoQuanDangChon = "tim";

            DangTachRa = tach;
            ApDungViTriTachRa();
            CapNhatHienThiTatCa();
        }

        void ApDungViTriTachRa()
        {
            foreach (var kv in organGroups)
            {
                var grp = kv.Value;
                grp.pivot.localPosition = grp.defaultLocalPos;
                grp.pivot.localScale = grp.defaultLocalScale;
            }

            if (!DangTachRa || string.IsNullOrEmpty(CoQuanDangChon)) return;
            if (!organGroups.TryGetValue(CoQuanDangChon, out var target)) return;

            // Đưa cơ quan được chọn vào giữa sân khấu giải phẫu (trục Y ~ 0.90m tính từ mặt sàn)
            Vector3 desiredStageLocalCenter = new Vector3(0f, 0.90f, 0f);
            Vector3 desiredBodyLocalCenter = (desiredStageLocalCenter - Bp3dReader.BodyLocalOffset) / Bp3dReader.BodyScale;

            float maxExt = Mathf.Max(0.18f, target.localContentBounds.extents.magnitude);
            // Phóng to các cơ quan nhỏ (tim, thận, dạ dày...) để quan sát 360° rõ nét
            float boost = Mathf.Clamp(1.05f / maxExt, 1.0f, 3.2f);
            target.pivot.localScale = Vector3.one * boost;
            target.pivot.localPosition = desiredBodyLocalCenter - target.localContentBounds.center * boost;
        }

        void CapNhatHienThiTatCa()
        {
            // Bóc lớp 0..1:
            // 0.00 -> 0.35: Da mờ dần từ 1 -> 0
            // 0.32 -> 0.72: Cơ mờ dần từ 1 -> 0
            // 0.75 -> 1.00: Xương mờ dần từ 1 -> 0.25 (vẫn giữ khung sườn mờ nhìn thấu tạng)
            float alphaDa = Mathf.Clamp01(1f - BocLop / 0.35f);
            float alphaCo = Mathf.Clamp01(1f - (BocLop - 0.32f) / 0.40f);
            float alphaXuong = 1f - Mathf.Clamp01((BocLop - 0.75f) / 0.25f) * 0.72f;

            for (int i = 0; i < allParts.Count; i++)
            {
                var p = allParts[i];
                if (p.renderer == null) continue;

                bool sysOn = false;
                for (int s = 0; s < p.heSoHuu.Length; s++)
                {
                    if (heDangBat.TryGetValue(p.heSoHuu[s], out bool on) && on) { sysOn = true; break; }
                }

                float alpha = 1f;
                if (p.idCoQuan == "da") alpha = alphaDa;
                else if (p.idCoQuan == "co") alpha = alphaCo;
                else if (p.idCoQuan == "xuong") alpha = alphaXuong;

                bool show = sysOn && alpha > 0.02f;
                if (DangTachRa)
                    show = (p.idCoQuan == CoQuanDangChon);

                p.renderer.enabled = show;
                if (!show) continue;

                float effectiveAlpha = DangTachRa ? 1f : alpha;
                p.material.SetFloat("_Opacity", effectiveAlpha);
                bool transparent = effectiveAlpha < 0.99f;
                p.material.SetFloat("_SrcBlend", transparent ? (float)BlendMode.SrcAlpha : (float)BlendMode.One);
                p.material.SetFloat("_DstBlend", transparent ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.Zero);
                p.material.SetFloat("_ZWrite", transparent ? 0f : 1f);
                p.material.renderQueue = transparent ? 3000 : 2000;
                p.material.SetFloat("_WaveAmp", (p.isWaveDigestive && !DangTachRa) ? 0.0045f : (p.isWaveDigestive ? 0.0025f : 0f));
            }

            // Bật tắt collider tương ứng hệ đang hiện
            foreach (var mk in allMarkers)
            {
                if (mk == null) continue;
                var col = mk.GetComponent<Collider>();
                if (col == null) continue;
                if (DangTachRa)
                {
                    col.enabled = mk.IdCoQuan == CoQuanDangChon;
                    continue;
                }
                if (mk.IdCoQuan == "da") col.enabled = heDangBat["da"] && alphaDa > 0.15f;
                else if (mk.IdCoQuan == "co") col.enabled = heDangBat["he_co"] && alphaCo > 0.15f;
                else col.enabled = grpActiveForOrgan(mk.IdCoQuan);
            }

            DatHoatAnh(BatDongMau, BatNhipTho);
            CapNhatHighlightCoQuan();
            CapNhatMatCatShader();
        }

        bool grpActiveForOrgan(string idCoQuan)
        {
            if (!organGroups.TryGetValue(idCoQuan, out var grp)) return true;
            foreach (var p in grp.parts)
            {
                if (p.renderer != null && p.renderer.enabled) return true;
            }
            return false;
        }

        void CapNhatHighlightCoQuan()
        {
            for (int i = 0; i < allParts.Count; i++)
            {
                var p = allParts[i];
                if (p.material == null) continue;
                bool sel = !string.IsNullOrEmpty(CoQuanDangChon) && p.idCoQuan == CoQuanDangChon;
                p.material.SetFloat("_Highlight", sel ? 1.0f : 0.0f);
            }
        }

        void CapNhatMatCatShader()
        {
            bool on = CheDoCat != CheDoMatCat.Tat;
            Vector4 plane = Vector4.zero;
            if (on)
            {
                switch (CheDoCat)
                {
                    case CheDoMatCat.CatDoc:
                        plane = new Vector4(1f, 0f, 0f, ViTriCat * 0.28f);
                        break;
                    case CheDoMatCat.CatNgang:
                        plane = new Vector4(0f, -1f, 0f, -(0.92f + ViTriCat * 0.78f));
                        break;
                    case CheDoMatCat.CatTran:
                        plane = new Vector4(0f, 0f, -1f, -ViTriCat * 0.14f);
                        break;
                }
            }

            for (int i = 0; i < allParts.Count; i++)
            {
                var mat = allParts[i].material;
                if (mat == null) continue;
                mat.SetFloat("_SliceEnabled", on ? 1f : 0f);
                mat.SetVector("_SlicePlane", plane);
            }

            if (slicePlaneGizmo != null)
            {
                slicePlaneGizmo.SetActive(on && !DangTachRa);
                if (on && !DangTachRa)
                {
                    var tr = slicePlaneGizmo.transform;
                    switch (CheDoCat)
                    {
                        case CheDoMatCat.CatDoc:
                            tr.localPosition = new Vector3(ViTriCat * 0.28f, 0.90f, 0f);
                            tr.localRotation = Quaternion.Euler(0f, 90f, 0f);
                            tr.localScale = new Vector3(0.55f, 1.80f, 1f);
                            break;
                        case CheDoMatCat.CatNgang:
                            tr.localPosition = new Vector3(0f, 0.92f + ViTriCat * 0.78f, 0f);
                            tr.localRotation = Quaternion.Euler(90f, 0f, 0f);
                            tr.localScale = new Vector3(0.75f, 0.50f, 1f);
                            break;
                        case CheDoMatCat.CatTran:
                            tr.localPosition = new Vector3(0f, 0.90f, ViTriCat * 0.14f);
                            tr.localRotation = Quaternion.identity;
                            tr.localScale = new Vector3(0.75f, 1.80f, 1f);
                            break;
                    }
                }
            }
        }

        // ==================== HOẠT ẢNH THỜI GIAN THỰC ====================

        float heartPhase;
        float breathPhase;

        void Update()
        {
            if (stageRoot == null) return;
            float dt = Mathf.Min(0.05f, Time.unscaledDeltaTime);

            // 1. Nhịp tim 60-120 lần/phút (0..0.15 nhĩ co, 0.15..0.52 thất co, 0.52..1.0 giãn chung)
            float freqHz = NhipTimBpm / 60f;
            heartPhase = (heartPhase + dt * freqHz) % 1f;

            float atrialPump = (heartPhase < 0.16f) ? Mathf.Sin((heartPhase / 0.16f) * Mathf.PI) : 0f;
            float ventPump = (heartPhase >= 0.15f && heartPhase < 0.52f)
                ? Mathf.Sin(((heartPhase - 0.15f) / 0.37f) * Mathf.PI) : 0f;

            float sAtrium = 1f - 0.085f * atrialPump + 0.045f * ventPump;
            float sVent = 1f - 0.095f * ventPump + 0.035f * atrialPump;

            for (int i = 0; i < nhomTamNhi.Count; i++)
                if (nhomTamNhi[i] != null) nhomTamNhi[i].localScale = tamNhiBaseScale[i] * sAtrium;
            for (int i = 0; i < nhomTamThat.Count; i++)
                if (nhomTamThat[i] != null) nhomTamThat[i].localScale = tamThatBaseScale[i] * sVent;

            // 2. Dòng máu chảy qua tim và động mạch/tĩnh mạch
            if (BatDongMau && heDangBat["tuan_hoan"])
            {
                float speedScale = NhipTimBpm / 75f;
                for (int i = 0; i < bloodParticles.Count; i++)
                {
                    var bp = bloodParticles[i];
                    if (bp.tr == null || !bp.tr.gameObject.activeSelf) continue;
                    bp.t = (bp.t + dt * bp.speed * speedScale) % 1f;
                    // Khi tách riêng Tim thì chỉ hiển thị dòng máu đi qua các buồng tim & cuống mạch lớn
                    float sampleT = !DangTachRa ? bp.t
                        : (bp.isRed ? Mathf.Lerp(0.16f, 0.56f, bp.t) : Mathf.Lerp(0.32f, 0.72f, bp.t));
                    bp.tr.localPosition = SamplePolyPath(bp.path, sampleT);
                    bloodParticles[i] = bp;
                }
            }

            // 3. Phổi phồng/xẹp & cơ hoành hạ xuống khi hít vào
            if (BatNhipTho && heDangBat["ho_hap"])
            {
                float breathHz = (NhipTimBpm / 75f) * 0.28f; // ~16-25 nhịp thở/phút
                breathPhase = (breathPhase + dt * breathHz) % 1f;
                float hitVao = 0.5f - 0.5f * Mathf.Cos(breathPhase * Mathf.PI * 2f);
                Vector3 phoiScale = new Vector3(1f + 0.055f * hitVao, 1f + 0.045f * hitVao, 1f + 0.065f * hitVao);
                if (phoiTraiTr != null) phoiTraiTr.localScale = Vector3.Scale(phoiTraiBaseScale, phoiScale);
                if (phoiPhaiTr != null) phoiPhaiTr.localScale = Vector3.Scale(phoiPhaiBaseScale, phoiScale);
                if (coHoanhTr != null) coHoanhTr.localPosition = coHoanhBaseLocalPos + new Vector3(0f, -0.055f * hitVao, 0f);
            }
        }

        static Vector3 SamplePolyPath(Vector3[] pts, float t)
        {
            if (pts == null || pts.Length == 0) return Vector3.zero;
            if (pts.Length == 1) return pts[0];
            float f = Mathf.Clamp01(t) * (pts.Length - 1);
            int idx = Mathf.Min(pts.Length - 2, (int)f);
            float u = f - idx;
            return Vector3.Lerp(pts[idx], pts[idx + 1], u);
        }

        // ==================== TIỆN ÍCH SINH LƯỚI CƠ QUAN (CACHE TĨNH) ====================

        static Mesh GetOrCreatePrimitiveMesh(string key, PrimitiveType primType)
        {
            if (staticProcMeshCache.TryGetValue(key, out var cached) && cached != null) return cached;
            var tmp = GameObject.CreatePrimitive(primType);
            var mf = tmp.GetComponent<MeshFilter>();
            var mesh = Instantiate(mf.sharedMesh);
            mesh.name = key;
            DestroyImmediate(tmp);
            staticProcMeshCache[key] = mesh;
            return mesh;
        }

        static Mesh GetOrCreateDeformedSphere(string key, Vector3 radii, int slices, int stacks, Func<Vector3, Vector3> deform)
        {
            if (staticProcMeshCache.TryGetValue(key, out var cached) && cached != null) return cached;

            var verts = new List<Vector3>((slices + 1) * (stacks + 1));
            var tris = new List<int>(slices * stacks * 6);

            for (int y = 0; y <= stacks; y++)
            {
                float vFrac = y / (float)stacks;
                float phi = vFrac * Mathf.PI;
                for (int x = 0; x <= slices; x++)
                {
                    float uFrac = x / (float)slices;
                    float theta = uFrac * Mathf.PI * 2f;
                    Vector3 unit = new Vector3(
                        Mathf.Sin(phi) * Mathf.Cos(theta),
                        Mathf.Cos(phi),
                        Mathf.Sin(phi) * Mathf.Sin(theta));
                    if (deform != null) unit = deform(unit);
                    verts.Add(Vector3.Scale(unit, radii));
                }
            }

            for (int y = 0; y < stacks; y++)
            {
                for (int x = 0; x < slices; x++)
                {
                    int i0 = y * (slices + 1) + x;
                    int i1 = i0 + 1;
                    int i2 = i0 + (slices + 1);
                    int i3 = i2 + 1;
                    tris.Add(i0); tris.Add(i1); tris.Add(i2);
                    tris.Add(i1); tris.Add(i3); tris.Add(i2);
                }
            }

            var mesh = new Mesh { name = key };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            staticProcMeshCache[key] = mesh;
            return mesh;
        }

        static Mesh GetOrCreateTubeMesh(string key, Vector3[] controlPts, float radius, int segs, int rings, Func<float, float> radiusProfile = null)
        {
            if (staticProcMeshCache.TryGetValue(key, out var cached) && cached != null) return cached;
            if (controlPts == null || controlPts.Length < 2) return null;

            var verts = new List<Vector3>((segs + 1) * (rings + 1));
            var tris = new List<int>(segs * rings * 6);

            Vector3 prevTangent = (SampleCatmull(controlPts, 0.01f) - SampleCatmull(controlPts, 0f)).normalized;
            if (prevTangent.sqrMagnitude < 1e-6f) prevTangent = Vector3.up;
            Vector3 normal = Vector3.Cross(prevTangent, Mathf.Abs(prevTangent.y) < 0.9f ? Vector3.up : Vector3.right).normalized;

            for (int i = 0; i <= segs; i++)
            {
                float u = i / (float)segs;
                Vector3 center = SampleCatmull(controlPts, u);
                Vector3 tangent = (SampleCatmull(controlPts, Mathf.Min(1f, u + 0.008f)) - SampleCatmull(controlPts, Mathf.Max(0f, u - 0.008f))).normalized;
                if (tangent.sqrMagnitude < 1e-6f) tangent = prevTangent;

                // Parallel transport để ống không xoắn khi uốn lượn
                Quaternion q = Quaternion.FromToRotation(prevTangent, tangent);
                normal = (q * normal).normalized;
                Vector3 binormal = Vector3.Cross(tangent, normal).normalized;
                prevTangent = tangent;

                float r = radius * (radiusProfile != null ? radiusProfile(u) : 1f);
                for (int j = 0; j <= rings; j++)
                {
                    float a = (j / (float)rings) * Mathf.PI * 2f;
                    Vector3 offset = (normal * Mathf.Cos(a) + binormal * Mathf.Sin(a)) * r;
                    verts.Add(center + offset);
                }
            }

            for (int i = 0; i < segs; i++)
            {
                for (int j = 0; j < rings; j++)
                {
                    int i0 = i * (rings + 1) + j;
                    int i1 = i0 + 1;
                    int i2 = i0 + (rings + 1);
                    int i3 = i2 + 1;
                    tris.Add(i0); tris.Add(i2); tris.Add(i1);
                    tris.Add(i1); tris.Add(i2); tris.Add(i3);
                }
            }

            var mesh = new Mesh
            {
                name = key,
                indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            staticProcMeshCache[key] = mesh;
            return mesh;
        }

        static Vector3 SampleCatmull(Vector3[] pts, float t)
        {
            int n = pts.Length;
            float f = Mathf.Clamp01(t) * (n - 1);
            int i = Mathf.Min(n - 2, (int)f);
            float u = f - i;

            Vector3 p0 = pts[Mathf.Max(0, i - 1)];
            Vector3 p1 = pts[i];
            Vector3 p2 = pts[i + 1];
            Vector3 p3 = pts[Mathf.Min(n - 1, i + 2)];

            float u2 = u * u;
            float u3 = u2 * u;
            return 0.5f * ((2f * p1) +
                           (-p0 + p2) * u +
                           (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2 +
                           (-p0 + 3f * p1 - 3f * p2 + p3) * u3);
        }
    }
}
