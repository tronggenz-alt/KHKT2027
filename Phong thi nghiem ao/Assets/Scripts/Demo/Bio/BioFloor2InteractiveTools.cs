// -----------------------------------------------------------------------------
// BioFloor2InteractiveTools.cs — Khởi tạo và đồng bộ hiển thị 3D cho 108 dụng cụ
// sinh học Tầng 2 (tự động chạy cả khi Play mode và khi Editor Build).
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine;
using Khkt.Demo;
using Khkt.Demo.Instruments;
using Khkt.Interaction;
using Khkt.Interaction.Bio;

namespace Khkt.Demo.Bio
{
    public static class BioFloor2InteractiveTools
    {
        // ---------------------------------------------------------------------
        // Quản lý vật liệu trực quan (PBR URP)
        // ---------------------------------------------------------------------
        static readonly Dictionary<string, Material> MatCache = new Dictionary<string, Material>();

        static Material GetMat(string matName, Color fallbackColor, float smoothness = 0.5f, float metallic = 0.0f)
        {
            if (MatCache.TryGetValue(matName, out var cached) && cached != null)
                return cached;

#if UNITY_EDITOR
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"Assets/Lab/Materials/{matName}.mat");
            if (asset != null)
            {
                MatCache[matName] = asset;
                return asset;
            }
#endif
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(shader)
            {
                name = matName,
                color = fallbackColor
            };
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            MatCache[matName] = m;
            return m;
        }

        // ---------------------------------------------------------------------
        // Tạo Node dụng cụ 3D trực quan có Renderer, Collider, Grabbable, BioTool
        // ---------------------------------------------------------------------
        public static GameObject CreateToolNode(
            Transform parent,
            string name,
            Vector3 localPos,
            string catalogId,
            Vector3 colSize,
            PrimitiveType primType,
            Vector3 meshScale,
            Vector3 meshOffset,
            string matName,
            Color fallbackColor,
            bool isGrabbable = false,
            bool liesOnTable = false,
            float gripHeight = 0.05f)
        {
            // Kiểm tra xem đã có node này chưa (tránh trùng lặp)
            Transform existing = parent.Find(name);
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.transform.localPosition = localPos;
            }

            // Collider chính
            var col = go.GetComponent<BoxCollider>();
            if (col == null) col = go.AddComponent<BoxCollider>();
            col.size = colSize;
            col.center = new Vector3(0, colSize.y * 0.5f, 0);

            // Mô hình 3D Visual hiển thị trực quan
            Transform vis = go.transform.Find("Visual");
            if (vis == null)
            {
                var visGo = GameObject.CreatePrimitive(primType);
                visGo.name = "Visual";
                visGo.transform.SetParent(go.transform, false);
                visGo.transform.localPosition = meshOffset;
                visGo.transform.localScale = meshScale;
                
                // Gỡ collider của khối visual con để không chặn raycast của node cha
                var subCol = visGo.GetComponent<Collider>();
                if (subCol != null) UnityEngine.Object.DestroyImmediate(subCol);

                var r = visGo.GetComponent<MeshRenderer>();
                if (r != null)
                {
                    r.sharedMaterial = GetMat(matName, fallbackColor);
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    r.receiveShadows = true;
                }
            }

            // Gắn EquipmentInfo từ catalog
            var info = BioCatalogLoader.AttachToolInfo(go, catalogId);

            // Gắn BioTool
            var bt = go.GetComponent<BioTool>();
            if (bt == null) bt = go.AddComponent<BioTool>();
            bt.toolId = catalogId;
            if (info != null) bt.displayName = info.title;

            // Gắn Grabbable nếu là dụng cụ cầm tay
            if (isGrabbable)
            {
                var gr = go.GetComponent<Grabbable>();
                if (gr == null) gr = go.AddComponent<Grabbable>();
                gr.kind = "dung_cu_sinh";
                gr.gripHeightM = gripHeight;
                gr.liesOnTable = liesOnTable;
                gr.massG = 80f;
            }

            return go;
        }

        public static T SpawnToolWith<T>(
            Transform parent,
            string name,
            Vector3 localPos,
            string catalogId,
            Vector3 colSize,
            PrimitiveType primType,
            Vector3 meshScale,
            Vector3 meshOffset,
            string matName,
            Color fallbackColor,
            bool isGrabbable = false,
            bool liesOnTable = false,
            float gripHeight = 0.05f) where T : Component
        {
            var go = CreateToolNode(parent, name, localPos, catalogId, colSize, primType, meshScale, meshOffset, matName, fallbackColor, isGrabbable, liesOnTable, gripHeight);
            var comp = go.GetComponent<T>();
            if (comp == null) comp = go.AddComponent<T>();
            return comp;
        }

        // ---------------------------------------------------------------------
        // Tự động kích hoạt khi vào Play mode (sau khi load Scene)
        // ---------------------------------------------------------------------
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoInitFloor2ToolsOnPlay()
        {
            var tang2 = GameObject.Find("Tầng 2");
            if (tang2 != null)
            {
                EnsureSpawnAllTools(tang2.transform);
                return;
            }

            var bioLab = GameObject.Find("Phòng thí nghiệm sinh học");
            if (bioLab != null)
            {
                EnsureSpawnAllTools(bioLab.transform.parent != null ? bioLab.transform.parent : bioLab.transform);
            }
        }

        // ---------------------------------------------------------------------
        // Khởi tạo toàn bộ 108 dụng cụ Tầng 2
        // ---------------------------------------------------------------------
        public static void EnsureSpawnAllTools(Transform tang2)
        {
            if (tang2 == null) return;
            BioCatalogLoader.EnsureLoaded();

            var bioLab = tang2.Find("Phòng thí nghiệm sinh học") ?? tang2;
            var microBio = tang2.Find("Phòng vi sinh") ?? tang2;
            var bioStore = tang2.Find("Kho sinh phẩm") ?? tang2;
            var corridor = tang2.Find("Hành lang tầng 2") ?? tang2;
            var molecular = tang2.Find("Phòng sinh học phân tử") ?? tang2;
            var greenhouse = tang2.Find("Nhà kính thực vật") ?? tang2;

            // Kiểm tra nếu đã có dụng cụ trên Bàn 1 thì không tạo lại
            if (bioLab.Find("Lam kính (Bàn 1)") != null) return;

            // 1. Cập nhật kính hiển vi & bàn giải phẫu đã có
            foreach (var mc in tang2.GetComponentsInChildren<MicroscopeController>(true))
            {
                if (mc.GetComponent<Collider>() == null)
                {
                    var bc = mc.gameObject.AddComponent<BoxCollider>();
                    bc.size = new Vector3(0.24f, 0.38f, 0.26f);
                    bc.center = new Vector3(0, 0.19f, 0);
                }
                BioCatalogLoader.AttachToolInfo(mc.gameObject, mc.isTeacherCamera ? "kinh_hien_vi_camera" : "kinh_hien_vi");
            }

            foreach (var dat in tang2.GetComponentsInChildren<DigitalAnatomyTable>(true))
            {
                if (dat.GetComponent<Collider>() == null)
                {
                    var bc = dat.gameObject.AddComponent<BoxCollider>();
                    bc.size = new Vector3(1.8f, 0.9f, 0.9f);
                    bc.center = new Vector3(0, 0.45f, 0);
                }
                BioCatalogLoader.AttachToolInfo(dat.gameObject, "ban_giai_phau_so");
            }

            // =================================================================
            // 2. PHÒNG THÍ NGHIỆM SINH HỌC CHUNG (55 dụng cụ)
            // =================================================================
            // Bàn 1 (học sinh thực hành)
            SpawnToolWith<GlassSlide>(bioLab, "Lam kính (Bàn 1)", new Vector3(5.20f, 0.82f, 3.10f), "lam_kinh",
                new Vector3(0.08f, 0.02f, 0.03f), PrimitiveType.Cube, new Vector3(0.075f, 0.002f, 0.025f), new Vector3(0, 0.001f, 0),
                "ThuyTinh", new Color(0.9f, 0.95f, 1f, 0.4f), isGrabbable: true, liesOnTable: true);

            SpawnToolWith<CoverSlip>(bioLab, "Hộp Lamen (Bàn 1)", new Vector3(5.32f, 0.82f, 3.10f), "lamen",
                new Vector3(0.05f, 0.03f, 0.05f), PrimitiveType.Cube, new Vector3(0.04f, 0.02f, 0.04f), new Vector3(0, 0.01f, 0),
                "HopLamKinh", new Color(0.85f, 0.9f, 0.95f, 0.7f), isGrabbable: true, liesOnTable: true);

            SpawnToolWith<DissectingNeedle>(bioLab, "Kim mũi mác (Bàn 1)", new Vector3(5.44f, 0.82f, 3.10f), "kim_mui_mac",
                new Vector3(0.03f, 0.03f, 0.16f), PrimitiveType.Cylinder, new Vector3(0.006f, 0.07f, 0.006f), new Vector3(0, 0.005f, 0),
                "Inox", Color.gray, isGrabbable: true, liesOnTable: true, gripHeight: 0.05f);

            SpawnToolWith<Forceps>(bioLab, "Kẹp panh mũi nhọn (Bàn 1)", new Vector3(5.56f, 0.82f, 3.10f), "kep_panh",
                new Vector3(0.03f, 0.03f, 0.14f), PrimitiveType.Cube, new Vector3(0.012f, 0.008f, 0.13f), new Vector3(0, 0.004f, 0),
                "Inox", Color.gray, isGrabbable: true, liesOnTable: true, gripHeight: 0.05f);

            SpawnToolWith<RazorBlade>(bioLab, "Dao lam (Bàn 1)", new Vector3(5.68f, 0.82f, 3.10f), "dao_lam",
                new Vector3(0.04f, 0.02f, 0.05f), PrimitiveType.Cube, new Vector3(0.025f, 0.002f, 0.045f), new Vector3(0, 0.002f, 0),
                "Inox", Color.gray, isGrabbable: true, liesOnTable: true);

            SpawnToolWith<Scissors>(bioLab, "Kéo mổ nhỏ (Bàn 1)", new Vector3(5.80f, 0.82f, 3.10f), "keo_nho",
                new Vector3(0.06f, 0.03f, 0.13f), PrimitiveType.Cube, new Vector3(0.05f, 0.008f, 0.12f), new Vector3(0, 0.004f, 0),
                "Inox", Color.gray, isGrabbable: true, liesOnTable: true);

            SpawnToolWith<Dropper>(bioLab, "Ống nhỏ giọt Pasteur (Bàn 1)", new Vector3(5.20f, 0.82f, 3.30f), "ong_nho_giot",
                new Vector3(0.03f, 0.03f, 0.15f), PrimitiveType.Cylinder, new Vector3(0.008f, 0.065f, 0.008f), new Vector3(0, 0.006f, 0),
                "ThuyTinh", new Color(0.9f, 0.95f, 1f, 0.5f), isGrabbable: true, liesOnTable: true);

            // 5 Lọ thuốc thử nhỏ giọt
            var bMethylen = SpawnToolWith<ReagentDropperBottle>(bioLab, "Lọ Xanh methylen 1%", new Vector3(5.32f, 0.82f, 3.30f), "lo_thuoc_thu_nho_giot",
                new Vector3(0.04f, 0.08f, 0.04f), PrimitiveType.Cylinder, new Vector3(0.032f, 0.035f, 0.032f), new Vector3(0, 0.035f, 0),
                "NhuaXanh", new Color(0.1f, 0.3f, 0.85f), isGrabbable: true);
            bMethylen.reagentId = "xanh_methylen"; bMethylen.reagentName = "Xanh methylen 1 %";

            var bLugol = SpawnToolWith<ReagentDropperBottle>(bioLab, "Lọ dung dịch Lugol", new Vector3(5.40f, 0.82f, 3.30f), "lo_thuoc_thu_nho_giot",
                new Vector3(0.04f, 0.08f, 0.04f), PrimitiveType.Cylinder, new Vector3(0.032f, 0.035f, 0.032f), new Vector3(0, 0.035f, 0),
                "NhuaDo", new Color(0.55f, 0.3f, 0.1f), isGrabbable: true);
            bLugol.reagentId = "iot_lugol"; bLugol.reagentName = "Dung dịch Lugol (I₂/KI)";

            var bNaCl = SpawnToolWith<ReagentDropperBottle>(bioLab, "Lọ NaCl ưu trương 0,8 M", new Vector3(5.48f, 0.82f, 3.30f), "lo_thuoc_thu_nho_giot",
                new Vector3(0.04f, 0.08f, 0.04f), PrimitiveType.Cylinder, new Vector3(0.032f, 0.035f, 0.032f), new Vector3(0, 0.035f, 0),
                "ThuyTinh", new Color(0.9f, 0.95f, 1f, 0.6f), isGrabbable: true);
            bNaCl.reagentId = "nacl_uu_truong"; bNaCl.reagentName = "NaCl ưu trương 0,8 M"; bNaCl.molarity = 0.8f;

            var bWater = SpawnToolWith<ReagentDropperBottle>(bioLab, "Lọ Nước cất", new Vector3(5.56f, 0.82f, 3.30f), "lo_thuoc_thu_nho_giot",
                new Vector3(0.04f, 0.08f, 0.04f), PrimitiveType.Cylinder, new Vector3(0.032f, 0.035f, 0.032f), new Vector3(0, 0.035f, 0),
                "ThuyTinh", new Color(0.9f, 0.95f, 1f, 0.4f), isGrabbable: true);
            bWater.reagentId = "nuoc_cat"; bWater.reagentName = "Nước cất vô trùng"; bWater.molarity = 0f;

            var bH2o2 = SpawnToolWith<ReagentDropperBottle>(bioLab, "Lọ H₂O₂ 3%", new Vector3(5.64f, 0.82f, 3.30f), "lo_thuoc_thu_nho_giot",
                new Vector3(0.04f, 0.08f, 0.04f), PrimitiveType.Cylinder, new Vector3(0.032f, 0.035f, 0.032f), new Vector3(0, 0.035f, 0),
                "NhuaTrang", Color.white, isGrabbable: true);
            bH2o2.reagentId = "h2o2_3_phan_tram"; bH2o2.reagentName = "Hiđrô peoxit H₂O₂ 3 %";

            SpawnToolWith<BlottingPaper>(bioLab, "Giấy thấm (Bàn 1)", new Vector3(5.74f, 0.82f, 3.30f), "giay_tham",
                new Vector3(0.06f, 0.02f, 0.06f), PrimitiveType.Cube, new Vector3(0.05f, 0.004f, 0.05f), new Vector3(0, 0.002f, 0),
                "NhuaTrang", Color.white, isGrabbable: true);

            SpawnToolWith<LensTissue>(bioLab, "Giấy lau kính (Bàn 1)", new Vector3(5.84f, 0.82f, 3.30f), "giay_lau_kinh",
                new Vector3(0.06f, 0.02f, 0.08f), PrimitiveType.Cube, new Vector3(0.05f, 0.006f, 0.07f), new Vector3(0, 0.003f, 0),
                "NhuaTrang", new Color(0.95f, 0.95f, 0.9f), isGrabbable: true);

            SpawnToolWith<WatchGlass>(bioLab, "Đĩa đồng hồ Ø 75 mm", new Vector3(5.94f, 0.82f, 3.30f), "dia_dong_ho",
                new Vector3(0.08f, 0.02f, 0.08f), PrimitiveType.Cylinder, new Vector3(0.075f, 0.004f, 0.075f), new Vector3(0, 0.002f, 0),
                "ThuyTinh", new Color(0.9f, 0.95f, 1f, 0.5f), isGrabbable: true, liesOnTable: true);

            SpawnToolWith<LabContainer>(bioLab, "Cốc có mỏ 100 mL", new Vector3(6.06f, 0.82f, 3.30f), "coc_co_mo",
                new Vector3(0.06f, 0.08f, 0.06f), PrimitiveType.Cylinder, new Vector3(0.055f, 0.04f, 0.055f), new Vector3(0, 0.04f, 0),
                "CocThuyTinh", new Color(0.9f, 0.95f, 1f, 0.5f), isGrabbable: true);

            SpawnToolWith<LabContainer>(bioLab, "Ống nghiệm thủy tinh", new Vector3(6.18f, 0.82f, 3.30f), "ong_nghiem",
                new Vector3(0.03f, 0.14f, 0.03f), PrimitiveType.Cylinder, new Vector3(0.018f, 0.065f, 0.018f), new Vector3(0, 0.065f, 0),
                "ThuyTinh", new Color(0.9f, 0.95f, 1f, 0.5f), isGrabbable: true);

            var rackTest = SpawnToolWith<TubeRackSockets>(bioLab, "Giá ống nghiệm 12 lỗ", new Vector3(6.30f, 0.82f, 3.30f), "gia_ong_nghiem",
                new Vector3(0.20f, 0.09f, 0.07f), PrimitiveType.Cube, new Vector3(0.18f, 0.08f, 0.06f), new Vector3(0, 0.04f, 0),
                "GiaNhuaXanh", new Color(0.2f, 0.45f, 0.8f), isGrabbable: false);
            rackTest.rackToolId = "gia_ong_nghiem"; rackTest.acceptedTubeType = "ong_nghiem";

            SpawnToolWith<LabContainer>(bioLab, "Ống đong 50 mL", new Vector3(6.42f, 0.82f, 3.30f), "ong_dong",
                new Vector3(0.05f, 0.18f, 0.05f), PrimitiveType.Cylinder, new Vector3(0.03f, 0.085f, 0.03f), new Vector3(0, 0.085f, 0),
                "ThuyTinh", new Color(0.9f, 0.95f, 1f, 0.5f), isGrabbable: true);

            SpawnToolWith<GlassRod>(bioLab, "Đũa thủy tinh", new Vector3(6.54f, 0.82f, 3.30f), "dua_thuy_tinh",
                new Vector3(0.02f, 0.02f, 0.18f), PrimitiveType.Cylinder, new Vector3(0.006f, 0.085f, 0.006f), new Vector3(0, 0.004f, 0),
                "ThuyTinh", new Color(0.9f, 0.95f, 1f, 0.6f), isGrabbable: true, liesOnTable: true);

            SpawnToolWith<DeskLamp>(bioLab, "Đèn bàn LED soi mẫu", new Vector3(6.66f, 0.82f, 3.30f), "den_ban_led",
                new Vector3(0.14f, 0.32f, 0.14f), PrimitiveType.Cylinder, new Vector3(0.12f, 0.15f, 0.12f), new Vector3(0, 0.15f, 0),
                "KinhHienVi_Than", Color.white, isGrabbable: false);

            SpawnToolWith<BenchTap>(bioLab, "Vòi nước đầu bàn", new Vector3(4.85f, 0.88f, 3.20f), "voi_nuoc_ban",
                new Vector3(0.10f, 0.28f, 0.10f), PrimitiveType.Cylinder, new Vector3(0.025f, 0.12f, 0.025f), new Vector3(0, 0.12f, 0),
                "Inox", Color.gray, isGrabbable: false);

            SpawnToolWith<MortarPestle>(bioLab, "Chày cối sứ Ø 9 cm", new Vector3(5.20f, 0.82f, 3.50f), "coi_chay_su",
                new Vector3(0.10f, 0.08f, 0.10f), PrimitiveType.Cylinder, new Vector3(0.09f, 0.035f, 0.09f), new Vector3(0, 0.035f, 0),
                "MatBanInox", new Color(0.92f, 0.92f, 0.9f), isGrabbable: true);

            SpawnToolWith<FilterFunnel>(bioLab, "Phễu lọc + giấy lọc", new Vector3(5.38f, 0.82f, 3.50f), "pheu_loc",
                new Vector3(0.08f, 0.10f, 0.08f), PrimitiveType.Cylinder, new Vector3(0.07f, 0.045f, 0.07f), new Vector3(0, 0.045f, 0),
                "ThuyTinh", new Color(0.9f, 0.95f, 1f, 0.5f), isGrabbable: true);

            SpawnToolWith<ImmersionOil>(bioLab, "Lọ dầu soi kính (n=1,515)", new Vector3(5.56f, 0.82f, 3.50f), "lo_dau_soi",
                new Vector3(0.04f, 0.08f, 0.04f), PrimitiveType.Cylinder, new Vector3(0.03f, 0.035f, 0.03f), new Vector3(0, 0.035f, 0),
                "NhuaDo", new Color(0.7f, 0.4f, 0.1f), isGrabbable: true);

            SpawnToolWith<SlideBox>(bioLab, "Hộp tiêu bản cố định (10 khe)", new Vector3(5.74f, 0.82f, 3.50f), "hop_tieu_ban",
                new Vector3(0.12f, 0.04f, 0.09f), PrimitiveType.Cube, new Vector3(0.11f, 0.03f, 0.08f), new Vector3(0, 0.015f, 0),
                "HopLamKinh", new Color(0.3f, 0.5f, 0.7f), isGrabbable: false);

            SpawnToolWith<VortexMixer>(bioLab, "Máy lắc ống Vortex", new Vector3(6.00f, 0.82f, 3.50f), "may_vortex",
                new Vector3(0.14f, 0.16f, 0.14f), PrimitiveType.Cube, new Vector3(0.13f, 0.14f, 0.13f), new Vector3(0, 0.07f, 0),
                "KinhHienVi_Than", new Color(0.85f, 0.85f, 0.88f), isGrabbable: false);

            SpawnToolWith<FermentationFlask>(bioLab, "Bình lên men rượu 250 mL", new Vector3(6.20f, 0.82f, 3.50f), "binh_len_men",
                new Vector3(0.09f, 0.16f, 0.09f), PrimitiveType.Cylinder, new Vector3(0.08f, 0.075f, 0.08f), new Vector3(0, 0.075f, 0),
                "CocThuyTinh", new Color(0.9f, 0.95f, 1f, 0.5f), isGrabbable: true);

            SpawnToolWith<YogurtJar>(bioLab, "Bộ làm sữa chua (lên men lactic)", new Vector3(6.40f, 0.82f, 3.50f), "bo_lam_sua_chua",
                new Vector3(0.08f, 0.10f, 0.08f), PrimitiveType.Cylinder, new Vector3(0.07f, 0.045f, 0.07f), new Vector3(0, 0.045f, 0),
                "ThuyTinh", Color.white, isGrabbable: true);

            SpawnToolWith<BenchTimer>(bioLab, "Đồng hồ bấm giờ để bàn", new Vector3(6.60f, 0.82f, 3.50f), "dong_ho_bam_gio",
                new Vector3(0.08f, 0.04f, 0.08f), PrimitiveType.Cylinder, new Vector3(0.07f, 0.015f, 0.07f), new Vector3(0, 0.015f, 0),
                "NhuaDen", Color.black, isGrabbable: true);

            SpawnToolWith<LabelPen>(bioLab, "Bút lông dầu ghi nhãn", new Vector3(6.72f, 0.82f, 3.50f), "but_ghi_nhan",
                new Vector3(0.02f, 0.02f, 0.14f), PrimitiveType.Cylinder, new Vector3(0.01f, 0.065f, 0.01f), new Vector3(0, 0.005f, 0),
                "NhuaXanh", Color.blue, isGrabbable: true, liesOnTable: true);

            SpawnToolWith<Thermometer>(bioLab, "Nhiệt kế phòng thí nghiệm", new Vector3(6.84f, 0.82f, 3.50f), "nhiet_ke",
                new Vector3(0.02f, 0.02f, 0.22f), PrimitiveType.Cylinder, new Vector3(0.007f, 0.10f, 0.007f), new Vector3(0, 0.005f, 0),
                "ThuyTinh", new Color(0.9f, 0.95f, 1f, 0.6f), isGrabbable: true, liesOnTable: true);

            SpawnToolWith<TestPaper>(bioLab, "Tập giấy chỉ thị pH", new Vector3(6.96f, 0.82f, 3.50f), "giay_chi_thi_ph",
                new Vector3(0.06f, 0.02f, 0.07f), PrimitiveType.Cube, new Vector3(0.05f, 0.01f, 0.06f), new Vector3(0, 0.005f, 0),
                "NhuaDo", new Color(0.9f, 0.75f, 0.2f), isGrabbable: true);

            SpawnToolWith<MagneticStirrer>(bioLab, "Máy khuấy từ gia nhiệt", new Vector3(7.12f, 0.85f, 3.50f), "may_khuay_tu_gia_nhiet",
                new Vector3(0.18f, 0.12f, 0.18f), PrimitiveType.Cube, new Vector3(0.16f, 0.10f, 0.16f), new Vector3(0, 0.05f, 0),
                "KinhHienVi_Than", Color.white, isGrabbable: false);

            // Tường & Bàn chức năng trong Phòng sinh học
            SpawnToolWith<BalanceScale>(bioLab, "Cân phân tích điện tử", new Vector3(18.50f, 0.85f, 6.20f), "can_phan_tich",
                new Vector3(0.24f, 0.28f, 0.24f), PrimitiveType.Cube, new Vector3(0.22f, 0.25f, 0.22f), new Vector3(0, 0.125f, 0),
                "KinhHienVi_Than", Color.white, isGrabbable: false);

            SpawnToolWith<MeterProbe>(bioLab, "Máy đo pH để bàn", new Vector3(18.50f, 0.85f, 6.80f), "may_do_ph",
                new Vector3(0.18f, 0.12f, 0.18f), PrimitiveType.Cube, new Vector3(0.16f, 0.10f, 0.16f), new Vector3(0, 0.05f, 0),
                "KinhHienVi_Than", Color.white, isGrabbable: false);

            SpawnToolWith<WaterBathController>(bioLab, "Bể ổn nhiệt (Water Bath)", new Vector3(18.50f, 0.85f, 7.30f), "be_on_nhiet",
                new Vector3(0.32f, 0.22f, 0.26f), PrimitiveType.Cube, new Vector3(0.30f, 0.20f, 0.24f), new Vector3(0, 0.10f, 0),
                "Inox", Color.gray, isGrabbable: false);

            SpawnToolWith<EyeWashStation>(bioLab, "Trạm rửa mắt khẩn cấp", new Vector3(0.80f, 1.20f, 0.80f), "tram_rua_mat",
                new Vector3(0.40f, 0.40f, 0.35f), PrimitiveType.Cylinder, new Vector3(0.35f, 0.15f, 0.35f), new Vector3(0, 0.15f, 0),
                "AnToan_Vang", Color.yellow, isGrabbable: false);

            SpawnToolWith<BiohazardBin>(bioLab, "Thùng rác nguy hại sinh học", new Vector3(0.80f, 0.45f, 1.40f), "thung_rac_sinh_hoc",
                new Vector3(0.35f, 0.50f, 0.35f), PrimitiveType.Cylinder, new Vector3(0.32f, 0.24f, 0.32f), new Vector3(0, 0.24f, 0),
                "AnToan_Vang", new Color(0.95f, 0.85f, 0.1f), isGrabbable: false);

            // =================================================================
            // 3. PHÒNG VI SINH (13 dụng cụ)
            // =================================================================
            SpawnToolWith<SpiritLamp>(microBio, "Đèn cồn vi sinh", new Vector3(1.60f, 0.85f, 4.20f), "den_con_vi_sinh",
                new Vector3(0.09f, 0.14f, 0.09f), PrimitiveType.Cylinder, new Vector3(0.08f, 0.06f, 0.08f), new Vector3(0, 0.06f, 0),
                "ThuyTinh", new Color(0.9f, 0.95f, 1f, 0.6f), isGrabbable: true);

            SpawnToolWith<InoculatingLoop>(microBio, "Que cấy vi sinh nichrome", new Vector3(1.75f, 0.85f, 4.20f), "que_cay_vi_sinh",
                new Vector3(0.03f, 0.03f, 0.22f), PrimitiveType.Cylinder, new Vector3(0.005f, 0.10f, 0.005f), new Vector3(0, 0.005f, 0),
                "Inox", Color.gray, isGrabbable: true, liesOnTable: true, gripHeight: 0.06f);

            SpawnToolWith<CellSpreader>(microBio, "Que gạt thủy tinh tam giác", new Vector3(1.90f, 0.85f, 4.20f), "que_gat_thuy_tinh",
                new Vector3(0.05f, 0.03f, 0.16f), PrimitiveType.Cylinder, new Vector3(0.006f, 0.075f, 0.006f), new Vector3(0, 0.005f, 0),
                "ThuyTinh", new Color(0.9f, 0.95f, 1f, 0.6f), isGrabbable: true, liesOnTable: true);

            SpawnToolWith<PetriDish>(microBio, "Đĩa Petri thạch dinh dưỡng", new Vector3(2.05f, 0.85f, 4.20f), "dia_petri_thach",
                new Vector3(0.10f, 0.03f, 0.10f), PrimitiveType.Cylinder, new Vector3(0.095f, 0.008f, 0.095f), new Vector3(0, 0.008f, 0),
                "ThuyTinh", new Color(0.95f, 0.9f, 0.75f, 0.8f), isGrabbable: true, liesOnTable: true);

            SpawnToolWith<BrothTube>(microBio, "Ống nghiệm canh thang LB", new Vector3(2.20f, 0.85f, 4.20f), "ong_canh_thang",
                new Vector3(0.04f, 0.15f, 0.04f), PrimitiveType.Cylinder, new Vector3(0.02f, 0.07f, 0.02f), new Vector3(0, 0.07f, 0),
                "ThuyTinh", new Color(0.9f, 0.85f, 0.6f, 0.7f), isGrabbable: true);

            SpawnToolWith<GramStainStation>(microBio, "Đảo nhuộm Gram 4 hóa chất", new Vector3(3.20f, 0.85f, 3.80f), "bo_nhuom_gram",
                new Vector3(0.40f, 0.15f, 0.25f), PrimitiveType.Cube, new Vector3(0.38f, 0.08f, 0.22f), new Vector3(0, 0.04f, 0),
                "Inox", Color.gray, isGrabbable: false);

            SpawnToolWith<ColonyCounterController>(microBio, "Máy đếm khuẩn lạc tự động", new Vector3(3.60f, 0.85f, 1.80f), "may_dem_khuan_lac",
                new Vector3(0.25f, 0.18f, 0.25f), PrimitiveType.Cube, new Vector3(0.24f, 0.16f, 0.24f), new Vector3(0, 0.08f, 0),
                "KinhHienVi_Than", Color.white, isGrabbable: false);

            SpawnToolWith<UvCeilingLamp>(microBio, "Đèn UV tiệt trùng trần", new Vector3(2.00f, 2.70f, 3.00f), "den_uv_tiet_trung",
                new Vector3(0.80f, 0.12f, 0.14f), PrimitiveType.Cube, new Vector3(0.75f, 0.08f, 0.10f), Vector3.zero,
                "Inox", Color.white, isGrabbable: false);

            SpawnToolWith<AirlockInterlock>(microBio, "Buồng đệm airlock 2 cửa", new Vector3(0.10f, 1.20f, 1.00f), "buong_dem_airlock",
                new Vector3(1.20f, 2.20f, 1.20f), PrimitiveType.Cube, new Vector3(1.15f, 2.10f, 1.15f), new Vector3(0, 1.05f, 0),
                "NhuaMo", new Color(0.9f, 0.9f, 0.95f, 0.3f), isGrabbable: false);

            SpawnToolWith<PassBoxController>(microBio, "Hộp chuyển mẫu Pass-box", new Vector3(0.05f, 1.10f, 3.80f), "hop_chuyen_mau_passbox",
                new Vector3(0.50f, 0.50f, 0.50f), PrimitiveType.Cube, new Vector3(0.48f, 0.48f, 0.48f), Vector3.zero,
                "PassBoxInox", Color.gray, isGrabbable: false);

            SpawnToolWith<DilutionTubeSet>(microBio, "Dãy 5 ống pha loãng vi sinh", new Vector3(2.50f, 0.85f, 4.20f), "bo_ong_pha_loang",
                new Vector3(0.24f, 0.14f, 0.06f), PrimitiveType.Cube, new Vector3(0.22f, 0.12f, 0.05f), new Vector3(0, 0.06f, 0),
                "GiaNhuaXanh", new Color(0.2f, 0.5f, 0.9f), isGrabbable: false);

            SpawnToolWith<AutoclaveBag>(microBio, "Túi hấp tiệt trùng Biohazard", new Vector3(2.80f, 0.85f, 4.20f), "tui_hap_tiet_trung",
                new Vector3(0.12f, 0.16f, 0.12f), PrimitiveType.Cylinder, new Vector3(0.10f, 0.08f, 0.10f), new Vector3(0, 0.08f, 0),
                "AnToan_Vang", Color.yellow, isGrabbable: true);

            SpawnToolWith<AutoclaveController>(microBio, "Nồi hấp tiệt trùng đứng", new Vector3(3.40f, 0.90f, 4.40f), "noi_hap",
                new Vector3(0.50f, 0.85f, 0.50f), PrimitiveType.Cylinder, new Vector3(0.45f, 0.40f, 0.45f), new Vector3(0, 0.40f, 0),
                "NoiHap_Inox", Color.gray, isGrabbable: false);

            // =================================================================
            // 4. KHO SINH PHẨM (5 dụng cụ)
            // =================================================================
            var f4c = SpawnToolWith<FridgeController>(bioStore, "Tủ mát lưu trữ sinh phẩm 4 °C", new Vector3(1.20f, 1.00f, 1.20f), "tu_lanh_4c",
                new Vector3(0.70f, 1.60f, 0.65f), PrimitiveType.Cube, new Vector3(0.65f, 1.50f, 0.60f), new Vector3(0, 0.75f, 0),
                "KinhHienVi_Than", Color.white, isGrabbable: false);
            f4c.fridgeId = "tu_mat_4"; f4c.isDeepFreezer = false; f4c.setTempC = 4.0f;

            var fm20c = SpawnToolWith<FridgeController>(bioStore, "Tủ đông sâu -20 °C", new Vector3(2.40f, 1.00f, 1.20f), "tu_dong_am_20c",
                new Vector3(0.70f, 1.60f, 0.65f), PrimitiveType.Cube, new Vector3(0.65f, 1.50f, 0.60f), new Vector3(0, 0.75f, 0),
                "KinhHienVi_Than", Color.white, isGrabbable: false);
            fm20c.fridgeId = "tu_dong_am20"; fm20c.isDeepFreezer = true; fm20c.setTempC = -20.0f;

            SpawnToolWith<StockKiosk>(bioStore, "Hệ thống cảnh báo nhiệt độ", new Vector3(1.80f, 1.60f, 0.15f), "canh_bao_nhiet_do",
                new Vector3(0.25f, 0.18f, 0.05f), PrimitiveType.Cube, new Vector3(0.22f, 0.15f, 0.04f), Vector3.zero,
                "NhuaDen", Color.black, isGrabbable: false);

            SpawnToolWith<SpecimenShelf>(bioStore, "Kệ inox chứa hóa chất sinh học", new Vector3(3.20f, 0.90f, 2.50f), "ke_chua_mau",
                new Vector3(0.80f, 1.60f, 0.40f), PrimitiveType.Cube, new Vector3(0.75f, 1.50f, 0.38f), new Vector3(0, 0.75f, 0),
                "KeInox", Color.gray, isGrabbable: false);

            SpawnToolWith<TeachingModel>(bioStore, "Mô hình xoắn kép ADN", new Vector3(1.80f, 1.10f, 2.80f), "mo_hinh_adn",
                new Vector3(0.30f, 0.65f, 0.30f), PrimitiveType.Cylinder, new Vector3(0.25f, 0.30f, 0.25f), new Vector3(0, 0.30f, 0),
                "ADN_KhungDuong", new Color(0.2f, 0.7f, 0.9f), isGrabbable: false);

            // =================================================================
            // 5. PHÒNG SINH HỌC PHÂN TỬ (21 dụng cụ)
            // =================================================================
            var p20 = SpawnToolWith<Micropipette>(molecular, "Micropipet P20 (2–20 µL)", new Vector3(2.10f, 0.85f, -8.30f), "micropipet_p20",
                new Vector3(0.04f, 0.24f, 0.04f), PrimitiveType.Cylinder, new Vector3(0.02f, 0.11f, 0.02f), new Vector3(0, 0.11f, 0),
                "NhuaVang", new Color(0.95f, 0.8f, 0.1f), isGrabbable: true, gripHeight: 0.12f);
            p20.pipetteToolId = "micropipet_p20"; p20.ConfigureRangeById("micropipet_p20");

            var p200 = SpawnToolWith<Micropipette>(molecular, "Micropipet P200 (20–200 µL)", new Vector3(2.30f, 0.85f, -8.30f), "micropipet_p200",
                new Vector3(0.04f, 0.24f, 0.04f), PrimitiveType.Cylinder, new Vector3(0.02f, 0.11f, 0.02f), new Vector3(0, 0.11f, 0),
                "NhuaXanh", new Color(0.2f, 0.5f, 0.9f), isGrabbable: true, gripHeight: 0.12f);
            p200.pipetteToolId = "micropipet_p200"; p200.ConfigureRangeById("micropipet_p200");

            var p1000 = SpawnToolWith<Micropipette>(molecular, "Micropipet P1000 (100–1000 µL)", new Vector3(2.50f, 0.85f, -8.30f), "micropipet_p1000",
                new Vector3(0.04f, 0.25f, 0.04f), PrimitiveType.Cylinder, new Vector3(0.024f, 0.12f, 0.024f), new Vector3(0, 0.12f, 0),
                "NhuaXanh", new Color(0.15f, 0.65f, 0.75f), isGrabbable: true, gripHeight: 0.12f);
            p1000.pipetteToolId = "micropipet_p1000"; p1000.ConfigureRangeById("micropipet_p1000");

            SpawnToolWith<TipBox>(molecular, "Hộp 96 đầu côn tiệt trùng", new Vector3(2.70f, 0.85f, -8.30f), "hop_dau_con",
                new Vector3(0.12f, 0.06f, 0.09f), PrimitiveType.Cube, new Vector3(0.11f, 0.05f, 0.08f), new Vector3(0, 0.025f, 0),
                "HopLamKinh", new Color(0.9f, 0.95f, 1f, 0.7f), isGrabbable: false);

            SpawnToolWith<TipWaste>(molecular, "Thùng thải bỏ đầu côn", new Vector3(2.90f, 0.85f, -8.30f), "thung_dau_con",
                new Vector3(0.12f, 0.16f, 0.12f), PrimitiveType.Cylinder, new Vector3(0.10f, 0.08f, 0.10f), new Vector3(0, 0.08f, 0),
                "AnToan_Vang", Color.yellow, isGrabbable: false);

            var epp = SpawnToolWith<MicroTube>(molecular, "Ống Eppendorf 1,5 mL", new Vector3(5.80f, 0.85f, -6.20f), "ong_eppendorf",
                new Vector3(0.03f, 0.05f, 0.03f), PrimitiveType.Cylinder, new Vector3(0.012f, 0.02f, 0.012f), new Vector3(0, 0.02f, 0),
                "ThuyTinh", new Color(0.9f, 0.95f, 1f, 0.7f), isGrabbable: true);
            epp.tubeTypeId = "ong_eppendorf";

            var pcrT = SpawnToolWith<MicroTube>(molecular, "Ống PCR 0,2 mL thành mỏng", new Vector3(3.10f, 0.85f, -8.30f), "ong_pcr",
                new Vector3(0.02f, 0.03f, 0.02f), PrimitiveType.Cylinder, new Vector3(0.008f, 0.012f, 0.008f), new Vector3(0, 0.012f, 0),
                "ThuyTinh", new Color(0.9f, 0.95f, 1f, 0.7f), isGrabbable: true);
            pcrT.tubeTypeId = "ong_pcr";

            var rackEpp = SpawnToolWith<TubeRackSockets>(molecular, "Giá ống Eppendorf 24 lỗ", new Vector3(6.00f, 0.85f, -6.20f), "gia_ong_eppendorf",
                new Vector3(0.16f, 0.06f, 0.10f), PrimitiveType.Cube, new Vector3(0.15f, 0.05f, 0.09f), new Vector3(0, 0.025f, 0),
                "GiaNhuaXanh", new Color(0.2f, 0.6f, 0.3f), isGrabbable: false);
            rackEpp.rackToolId = "gia_ong_eppendorf"; rackEpp.acceptedTubeType = "ong_eppendorf";

            var rackPcr = SpawnToolWith<TubeRackSockets>(molecular, "Giá ống PCR 96 lỗ", new Vector3(3.30f, 0.85f, -8.30f), "gia_ong_pcr",
                new Vector3(0.14f, 0.05f, 0.09f), PrimitiveType.Cube, new Vector3(0.13f, 0.04f, 0.08f), new Vector3(0, 0.02f, 0),
                "NhuaVang", new Color(0.9f, 0.6f, 0.1f), isGrabbable: false);
            rackPcr.rackToolId = "gia_ong_pcr"; rackPcr.acceptedTubeType = "ong_pcr";

            SpawnToolWith<IceBox>(molecular, "Hộp đá bảo quản enzyme 0–4 °C", new Vector3(3.50f, 0.85f, -8.30f), "hop_da",
                new Vector3(0.18f, 0.10f, 0.14f), PrimitiveType.Cube, new Vector3(0.16f, 0.08f, 0.12f), new Vector3(0, 0.04f, 0),
                "NhuaTrang", Color.white, isGrabbable: false);

            SpawnToolWith<DryBlockHeater>(molecular, "Khối gia nhiệt khô (Dry Block)", new Vector3(6.30f, 0.85f, -6.20f), "khoi_gia_nhiet_kho",
                new Vector3(0.20f, 0.14f, 0.18f), PrimitiveType.Cube, new Vector3(0.18f, 0.12f, 0.16f), new Vector3(0, 0.06f, 0),
                "KinhHienVi_Than", Color.white, isGrabbable: false);

            SpawnToolWith<CentrifugeLoad>(molecular, "Máy ly tâm vi lượng (Microcentrifuge)", new Vector3(6.70f, 0.85f, -6.20f), "may_ly_tam_eppendorf",
                new Vector3(0.24f, 0.20f, 0.24f), PrimitiveType.Cylinder, new Vector3(0.22f, 0.09f, 0.22f), new Vector3(0, 0.09f, 0),
                "KinhHienVi_Than", Color.white, isGrabbable: false);

            SpawnToolWith<PcrHoodController>(molecular, "Tủ thao tác tiền PCR (PCR Hood)", new Vector3(0.60f, 1.15f, -6.20f), "tu_thao_tac_pcr",
                new Vector3(0.90f, 1.20f, 0.70f), PrimitiveType.Cube, new Vector3(0.85f, 1.10f, 0.65f), new Vector3(0, 0.55f, 0),
                "Inox", Color.gray, isGrabbable: false);

            SpawnToolWith<ThermalCyclerController>(molecular, "Máy luân nhiệt PCR 96 giếng", new Vector3(10.20f, 0.90f, -8.30f), "may_pcr",
                new Vector3(0.26f, 0.24f, 0.32f), PrimitiveType.Cube, new Vector3(0.24f, 0.22f, 0.30f), new Vector3(0, 0.11f, 0),
                "KinhHienVi_Than", Color.white, isGrabbable: false);

            var gelTank = SpawnToolWith<GelTankController>(molecular, "Bể điện di gel agarose", new Vector3(11.10f, 0.85f, -8.30f), "be_dien_di",
                new Vector3(0.22f, 0.10f, 0.16f), PrimitiveType.Cube, new Vector3(0.20f, 0.08f, 0.14f), new Vector3(0, 0.04f, 0),
                "BeDienDi_NhuaTrong", new Color(0.9f, 0.95f, 1f, 0.4f), isGrabbable: false);

            var pwr = SpawnToolWith<PowerSupplyController>(molecular, "Bộ nguồn điện di 100 V", new Vector3(11.45f, 0.85f, -8.30f), "nguon_dien_di",
                new Vector3(0.18f, 0.14f, 0.22f), PrimitiveType.Cube, new Vector3(0.16f, 0.12f, 0.20f), new Vector3(0, 0.06f, 0),
                "NhuaXanh", new Color(0.2f, 0.3f, 0.6f), isGrabbable: false);
            pwr.connectedTank = gelTank;

            SpawnToolWith<GelCastingTray>(molecular, "Khay đúc gel agarose + lược", new Vector3(11.80f, 0.85f, -8.30f), "khay_duc_gel",
                new Vector3(0.14f, 0.04f, 0.12f), PrimitiveType.Cube, new Vector3(0.12f, 0.03f, 0.10f), new Vector3(0, 0.015f, 0),
                "BeDienDi_NhuaTrong", new Color(0.9f, 0.95f, 1f, 0.5f), isGrabbable: true);

            SpawnToolWith<MicrowaveOven>(molecular, "Lò vi sóng đun thạch agarose", new Vector3(12.20f, 0.90f, -8.30f), "lo_vi_song",
                new Vector3(0.40f, 0.28f, 0.32f), PrimitiveType.Cube, new Vector3(0.38f, 0.26f, 0.30f), new Vector3(0, 0.13f, 0),
                "KinhHienVi_Than", Color.white, isGrabbable: false);

            var gelDoc = SpawnToolWith<GelDocController>(molecular, "Buồng chụp Gel UV/Blue LED", new Vector3(12.70f, 0.95f, -7.20f), "gel_doc",
                new Vector3(0.32f, 0.38f, 0.30f), PrimitiveType.Cube, new Vector3(0.30f, 0.36f, 0.28f), new Vector3(0, 0.18f, 0),
                "NhuaDen", Color.black, isGrabbable: false);

            var od600 = SpawnToolWith<Od600Controller>(molecular, "Máy đo mật độ quang OD600", new Vector3(7.20f, 0.85f, -6.20f), "may_do_od600",
                new Vector3(0.24f, 0.16f, 0.24f), PrimitiveType.Cube, new Vector3(0.22f, 0.14f, 0.22f), new Vector3(0, 0.07f, 0),
                "KinhHienVi_Than", Color.white, isGrabbable: false);

            SpawnToolWith<Cuvette>(molecular, "Cuvet đo quang 1 cm", new Vector3(7.50f, 0.85f, -6.20f), "cuvet",
                new Vector3(0.03f, 0.06f, 0.03f), PrimitiveType.Cube, new Vector3(0.015f, 0.045f, 0.015f), new Vector3(0, 0.022f, 0),
                "ThuyTinh", new Color(0.95f, 0.98f, 1f, 0.8f), isGrabbable: true);

            // =================================================================
            // 6. NHÀ KÍNH THỰC VẬT (10 dụng cụ)
            // =================================================================
            SpawnToolWith<PhotosynthesisRig>(greenhouse, "Trạm quang hợp Rong đuôi chó", new Vector3(16.20f, 0.90f, -8.40f), "tram_quang_hop",
                new Vector3(0.30f, 0.32f, 0.24f), PrimitiveType.Cylinder, new Vector3(0.22f, 0.15f, 0.22f), new Vector3(0, 0.15f, 0),
                "CocThuyTinh", new Color(0.85f, 0.95f, 0.85f, 0.6f), isGrabbable: false);

            SpawnToolWith<GlowingSplint>(greenhouse, "Que đóm thử khí Oxy", new Vector3(16.55f, 0.85f, -8.40f), "que_dom",
                new Vector3(0.02f, 0.02f, 0.22f), PrimitiveType.Cylinder, new Vector3(0.005f, 0.10f, 0.005f), new Vector3(0, 0.005f, 0),
                "Ban", new Color(0.6f, 0.4f, 0.2f), isGrabbable: true, liesOnTable: true);

            SpawnToolWith<PotometerRig>(greenhouse, "Áp kế bọt khí Potometer", new Vector3(19.50f, 0.90f, -8.40f), "tram_potometer",
                new Vector3(0.35f, 0.35f, 0.20f), PrimitiveType.Cube, new Vector3(0.30f, 0.30f, 0.16f), new Vector3(0, 0.15f, 0),
                "ThuyTinh", new Color(0.9f, 0.95f, 1f, 0.5f), isGrabbable: false);

            SpawnToolWith<PhototropismBox>(greenhouse, "Hộp thí nghiệm hướng sáng", new Vector3(22.80f, 0.90f, -8.40f), "hop_huong_sang",
                new Vector3(0.32f, 0.26f, 0.24f), PrimitiveType.Cube, new Vector3(0.30f, 0.24f, 0.22f), new Vector3(0, 0.12f, 0),
                "NhuaDen", Color.black, isGrabbable: false);

            SpawnToolWith<NftRackController>(greenhouse, "Giàn thủy canh NFT 3 tầng", new Vector3(25.20f, 1.20f, -6.20f), "gian_thuy_canh_nft",
                new Vector3(1.20f, 1.80f, 0.60f), PrimitiveType.Cube, new Vector3(1.15f, 1.70f, 0.55f), new Vector3(0, 0.85f, 0),
                "KhungNhomNFT", Color.white, isGrabbable: false);

            SpawnToolWith<MeterProbe>(greenhouse, "Bút đo EC / pH thủy canh", new Vector3(24.60f, 0.85f, -6.20f), "may_do_ec_ph",
                new Vector3(0.03f, 0.03f, 0.16f), PrimitiveType.Cylinder, new Vector3(0.015f, 0.075f, 0.015f), new Vector3(0, 0.008f, 0),
                "NhuaXanh", Color.blue, isGrabbable: true, liesOnTable: true);

            SpawnToolWith<WateringCan>(greenhouse, "Bình tưới cây vòi hoa sen", new Vector3(15.20f, 0.65f, -4.50f), "binh_tuoi",
                new Vector3(0.24f, 0.26f, 0.18f), PrimitiveType.Cylinder, new Vector3(0.18f, 0.12f, 0.18f), new Vector3(0, 0.12f, 0),
                "NhuaXanh", new Color(0.1f, 0.65f, 0.4f), isGrabbable: true);

            SpawnToolWith<ClimateSensor>(greenhouse, "Cảm biến vi khí hậu nhà kính", new Vector3(20.20f, 1.50f, -6.20f), "cam_bien_vi_khi_hau",
                new Vector3(0.18f, 0.22f, 0.08f), PrimitiveType.Cube, new Vector3(0.16f, 0.20f, 0.06f), Vector3.zero,
                "NhuaTrang", Color.white, isGrabbable: false);

            SpawnToolWith<MistSystem>(greenhouse, "Hệ thống phun sương nhà kính", new Vector3(20.20f, 2.40f, -6.20f), "he_phun_suong",
                new Vector3(0.25f, 0.15f, 0.15f), PrimitiveType.Cylinder, new Vector3(0.06f, 0.12f, 0.06f), Vector3.zero,
                "Inox", Color.gray, isGrabbable: false);

            SpawnToolWith<PottingKit>(greenhouse, "Bộ dụng cụ sang chậu thực vật", new Vector3(15.80f, 0.85f, -4.35f), "bo_sang_chau",
                new Vector3(0.25f, 0.10f, 0.20f), PrimitiveType.Cube, new Vector3(0.22f, 0.08f, 0.18f), new Vector3(0, 0.04f, 0),
                "NhuaDen", Color.gray, isGrabbable: true);

            // =================================================================
            // 7. HÀNH LANG TẦNG 2 (4 dụng cụ)
            // =================================================================
            CreateToolNode(corridor, "Ki-ốt tra cứu thông tin hành lang", new Vector3(16.4f, 1.0f, -3.26f), "kiosk_tra_cuu",
                new Vector3(0.60f, 1.50f, 0.40f), PrimitiveType.Cube, new Vector3(0.55f, 1.40f, 0.35f), new Vector3(0, 0.70f, 0),
                "NhuaDen", Color.black, isGrabbable: false);

            CreateToolNode(corridor, "Bảng tin điện tử hành lang", new Vector3(17.6f, 1.6f, -0.12f), "man_hinh_bang_tin",
                new Vector3(1.20f, 0.70f, 0.08f), PrimitiveType.Cube, new Vector3(1.15f, 0.65f, 0.05f), Vector3.zero,
                "NhuaDen", Color.black, isGrabbable: false);

            CreateToolNode(corridor, "Tủ locker đồ cá nhân học sinh", new Vector3(11.6f, 1.0f, -3.28f), "tu_do_ca_nhan",
                new Vector3(1.20f, 1.80f, 0.45f), PrimitiveType.Cube, new Vector3(1.15f, 1.70f, 0.40f), new Vector3(0, 0.85f, 0),
                "Inox", Color.gray, isGrabbable: false);

            CreateToolNode(corridor, "Tủ kính trưng bày mẫu vật", new Vector3(9.5f, 1.0f, -3.28f), "tu_trung_bay",
                new Vector3(1.20f, 1.80f, 0.45f), PrimitiveType.Cube, new Vector3(1.15f, 1.70f, 0.40f), new Vector3(0, 0.85f, 0),
                "ThuyTinh", new Color(0.9f, 0.95f, 1f, 0.5f), isGrabbable: false);

            // Khởi tạo BioLabSync ở gốc Tầng 2
            if (tang2.GetComponent<BioLabSync>() == null)
                tang2.gameObject.AddComponent<BioLabSync>();
        }
    }
}
