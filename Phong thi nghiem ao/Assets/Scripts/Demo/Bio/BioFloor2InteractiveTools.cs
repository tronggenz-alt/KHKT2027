// -----------------------------------------------------------------------------
// BioFloor2InteractiveTools.cs — Gắn chức năng cho dụng cụ sinh học tầng 2.
//
// Nguyên tắc sắp xếp:
//  1. Máy và đồ lớn ĐÃ CÓ mô hình (trình dựng cảnh Floor2.*): gắn thành phần tương tác thẳng vào mô hình đó
//     (Bind), không tạo thêm khối trùng chỗ.
//  2. Dụng cụ nhỏ chưa có mô hình: tạo mới bằng vài khối đơn giản, đặt theo vị trí của mô hình làm mốc
//     (bàn, quầy, tủ) nên luôn nằm trên mặt bàn thật, đúng phòng, xếp theo nhóm chức năng:
//       - Bàn sinh học 1: khay làm tiêu bản trước kính hiển vi 1 – 2; lọ thuốc nhuộm trên giá thuốc thử cầm lên được.
//       - Bàn sinh học 2: bộ lên men rượu và làm sữa chua.
//       - Quầy rửa tay: dụng cụ thủy tinh và dụng cụ đo (cạnh giá úp).
//       - Phòng vi sinh: que cấy, que trang, đĩa petri ở mép đảo nhuộm Gram; ống pha loãng ở quầy tây.
//       - Phòng sinh học phân tử: tủ PCR (khay đá thuốc thử, ống PCR, thùng đầu côn) → máy PCR → khu đổ gel và
//         điện di (lò vi sóng, khay đúc, thang chuẩn, micropipet riêng khu hậu PCR) → buồng chụp gel.
// Gọi lúc dựng cảnh (LabSceneBuilder.BuildFloor2InteractiveTools) và, nếu cảnh cũ chưa dựng lại, lúc vào Play.
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine;
using Khkt.Interaction;
using Khkt.Interaction.Bio;

namespace Khkt.Demo.Bio
{
    /// <summary>Dấu đã gắn dụng cụ tầng 2 (tránh gắn lại khi vào Play).</summary>
    public class BioFloor2ToolsMarker : MonoBehaviour { public int version = 2; }

    public static class BioFloor2InteractiveTools
    {
        // =====================================================================
        // Vật liệu
        // =====================================================================
        static readonly Dictionary<string, Material> MatCache = new Dictionary<string, Material>();

        static Material GetMat(string matName, Color fallback)
        {
            if (MatCache.TryGetValue(matName, out var cached) && cached != null) return cached;
#if UNITY_EDITOR
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"Assets/Lab/Materials/{matName}.mat");
            if (asset != null) { MatCache[matName] = asset; return asset; }
#endif
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(shader) { name = matName, color = fallback };
            if (fallback.a < 0.99f && m.HasProperty("_Surface"))
            {
                m.SetFloat("_Surface", 1);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.renderQueue = 3000;
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            MatCache[matName] = m;
            return m;
        }

        static readonly Color Glass = new Color(0.88f, 0.94f, 1f, 0.35f);
        static readonly Color Steel = new Color(0.72f, 0.74f, 0.76f);

        // =====================================================================
        // Tìm mô hình
        // =====================================================================

        /// <summary>Con cháu đầu tiên (theo thứ tự dựng) có đúng tên; occurrence = 1 lấy cái thứ hai...</summary>
        public static Transform FindNamed(Transform root, string name, int occurrence = 0)
        {
            if (root == null) return null;
            int seen = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t != root && t.name == name && seen++ == occurrence) return t;
            return null;
        }

        static List<Transform> FindAllNamed(Transform root, string name)
        {
            var list = new List<Transform>();
            if (root == null) return list;
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t != root && t.name == name) list.Add(t);
            return list;
        }

        /// <summary>Khối hộp va chạm ôm các mặt lưới con (trong hệ tọa độ của go).</summary>
        static void FitBoxCollider(GameObject go, bool onlyIfNone)
        {
            if (onlyIfNone && go.GetComponentInChildren<Collider>() != null) return;
            bool any = false;
            var b = new Bounds();
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = go.transform.InverseTransformPoint(mf.transform.TransformPoint(corner));
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                }
            }
            if (!any) return;
            var col = Ensure<BoxCollider>(go);
            col.center = b.center;
            col.size = Vector3.Max(b.size, new Vector3(0.01f, 0.01f, 0.01f));
        }

        /// <summary>Lấy hoặc thêm thành phần. Không dùng "??": GetComponent trong trình soạn thảo trả đối tượng null giả mà ?? không bắt.</summary>
        static T Ensure<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        static void SetInfoTitle(GameObject go, string title)
        {
            var info = go.GetComponent<EquipmentInfo>();
            if (info == null) info = go.AddComponent<EquipmentInfo>();
            info.title = title;
        }

        /// <summary>Gắn thẻ danh mục + thành phần T vào mô hình có sẵn. Không thấy mô hình thì trả null (không tạo khối thay thế).</summary>
        public static T Bind<T>(Transform model, string catalogId, bool grabbable = false, float grip = 0.06f) where T : Component
        {
            if (model == null) return null;
            var go = model.gameObject;
            FitBoxCollider(go, onlyIfNone: true);
            if (!string.IsNullOrEmpty(catalogId)) BioCatalogLoader.AttachToolInfo(go, catalogId);
            var bt = Ensure<BioTool>(go);
            if (!string.IsNullOrEmpty(catalogId)) bt.toolId = catalogId;
            var info = go.GetComponent<EquipmentInfo>();
            if (info != null && !string.IsNullOrEmpty(info.title)) bt.displayName = info.title;
            if (grabbable)
            {
                var g = Ensure<Grabbable>(go);
                g.kind = "dung_cu_sinh";
                g.gripHeightM = grip;
                g.massG = 80f;
            }
            return Ensure<T>(go);
        }

        static T Bind<T>(Transform root, string name, string catalogId, int occurrence = 0) where T : Component
            => Bind<T>(FindNamed(root, name, occurrence), catalogId);

        // =====================================================================
        // Dựng dụng cụ nhỏ
        // =====================================================================

        /// <summary>Nút dụng cụ mới đặt theo mốc: local là tọa độ trong hệ của anchor (mặt bàn: y = độ cao mặt bàn).</summary>
        static GameObject NewTool(Transform room, Transform anchor, string name, Vector3 local, float yaw, string catalogId)
        {
            anchor ??= room;
            var existing = room.Find(name);
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
            var go = new GameObject(name);
            go.transform.SetParent(room, false);
            go.transform.SetPositionAndRotation(anchor.TransformPoint(local), anchor.rotation * Quaternion.Euler(0, yaw, 0));
            if (!string.IsNullOrEmpty(catalogId))
            {
                BioCatalogLoader.AttachToolInfo(go, catalogId);
                var bt = go.AddComponent<BioTool>();
                bt.toolId = catalogId;
                var info = go.GetComponent<EquipmentInfo>();
                if (info != null && !string.IsNullOrEmpty(info.title)) bt.displayName = info.title;
            }
            return go;
        }

        /// <summary>Khối hình con (không va chạm). pos / scale trong hệ của tool; khối trụ Unity cao 2 đơn vị.</summary>
        static Transform Part(GameObject tool, string name, PrimitiveType type, Vector3 pos, Vector3 scale, string mat, Color fallback, Vector3? euler = null)
        {
            var p = GameObject.CreatePrimitive(type);
            p.name = name;
            UnityEngine.Object.DestroyImmediate(p.GetComponent<Collider>());
            p.transform.SetParent(tool.transform, false);
            p.transform.localPosition = pos;
            p.transform.localScale = scale;
            if (euler.HasValue) p.transform.localEulerAngles = euler.Value;
            var r = p.GetComponent<MeshRenderer>();
            r.sharedMaterial = GetMat(mat, fallback);
            r.shadowCastingMode = fallback.a < 0.99f ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
            return p.transform;
        }

        /// <summary>Trụ đứng: đáy tại y0, cao h, bán kính r.</summary>
        static Transform Cyl(GameObject tool, string name, float y0, float r, float h, string mat, Color c, float x = 0, float z = 0) =>
            Part(tool, name, PrimitiveType.Cylinder, new Vector3(x, y0 + h / 2, z), new Vector3(r * 2, h / 2, r * 2), mat, c);

        static Transform BoxPart(GameObject tool, string name, Vector3 center, Vector3 size, string mat, Color c) =>
            Part(tool, name, PrimitiveType.Cube, center, size, mat, c);

        static Grabbable MakeGrabbable(GameObject go, float grip, bool lying = false, float length = 0.15f, float radius = 0.006f)
        {
            FitBoxCollider(go, false);
            var g = Ensure<Grabbable>(go);
            g.kind = "dung_cu_sinh";
            g.gripHeightM = grip;
            g.liesOnTable = lying;
            g.lengthM = length;
            g.lyingRadiusM = radius;
            g.massG = 40f;
            return g;
        }

        /// <summary>
        /// Dụng cụ dạng que nằm trên bàn (kim mũi mác, kẹp, dao, ống nhỏ giọt, đũa, nhiệt kế...). Trục dài theo y của nút
        /// (đúng quy ước liesOnTable của HandController), nút được xoay nằm ngang dọc trục x của mốc.
        /// </summary>
        static GameObject Stick(Transform room, Transform anchor, string name, Vector3 local, string catalogId, float length, float radius,
                                string mat, Color c, string tipMat = null, Color? tipColor = null)
        {
            var go = NewTool(room, anchor, name, local + new Vector3(0, radius, 0), 0, catalogId);
            go.transform.rotation = (anchor ?? room).rotation * Quaternion.FromToRotation(Vector3.up, Vector3.right);
            Part(go, "Thân", PrimitiveType.Cylinder, new Vector3(0, 0, 0), new Vector3(radius * 2, length * 0.35f, radius * 2), mat, c);
            if (tipMat != null)
                Part(go, "Đầu", PrimitiveType.Cylinder, new Vector3(0, length * 0.42f, 0), new Vector3(radius * 1.6f, length * 0.08f, radius * 1.6f), tipMat, tipColor ?? c);
            MakeGrabbable(go, 0.02f, true, length, radius);
            return go;
        }

        /// <summary>Ống nhựa nắp màu (ống PCR 0,2 mL hoặc eppendorf 1,5 mL), có MicroTube, cầm được. Gốc ở đáy ống.</summary>
        public static MicroTube MakeTube(Transform parent, string name, Vector3 local, string tubeType, string label, Color cap)
        {
            var old = parent.Find(name);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            bool pcr = tubeType == "ong_pcr";
            float r = pcr ? 0.0035f : 0.0052f, h = pcr ? 0.02f : 0.038f;
            BioCatalogLoader.AttachToolInfo(go, pcr ? "ong_pcr" : "ong_eppendorf");
            Part(go, "Thân ống", PrimitiveType.Cylinder, new Vector3(0, h * 0.55f, 0), new Vector3(r * 2, h * 0.45f, r * 2), "ThuyTinh", Glass);
            Part(go, "Đáy côn", PrimitiveType.Sphere, new Vector3(0, h * 0.1f, 0), new Vector3(r * 1.5f, h * 0.22f, r * 1.5f), "ThuyTinh", Glass);
            Part(go, "Nắp", PrimitiveType.Cylinder, new Vector3(0, h + 0.002f, 0), new Vector3(r * 2.3f, 0.002f, r * 2.3f), "NhuaTrang", cap);
            var bt = go.AddComponent<BioTool>();
            bt.toolId = tubeType;
            bt.kind = BioToolKind.EppendorfTube;
            bt.displayName = $"Ống {label}";
            MakeGrabbable(go, h * 0.7f);
            var tube = go.AddComponent<MicroTube>();
            tube.tubeTypeId = tubeType;
            tube.labelText = label;
            SetInfoTitle(go, (pcr ? "Ống PCR 0,2 mL" : "Ống eppendorf 1,5 mL") + $" – {label}");
            return tube;
        }

        static ReagentTube MakeReagent(Transform parent, Vector3 local, string code, Color cap, float stockUl)
        {
            string name = "Ống " + MolCodes.Name(code);
            var old = parent.Find(name);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            Part(go, "Thân ống", PrimitiveType.Cylinder, new Vector3(0, 0.019f, 0), new Vector3(0.0104f, 0.017f, 0.0104f), "ThuyTinh", Glass);
            Part(go, "Nắp", PrimitiveType.Cylinder, new Vector3(0, 0.038f, 0), new Vector3(0.012f, 0.003f, 0.012f), "NhuaTrang", cap);
            FitBoxCollider(go, false);
            var col = go.GetComponent<BoxCollider>();
            col.size = new Vector3(0.022f, col.size.y, 0.022f);  // dễ trỏ chuột hơn ống thật
            var rt = go.AddComponent<ReagentTube>();
            rt.reagentCode = code;
            rt.stockUl = stockUl;
            var info = go.AddComponent<EquipmentInfo>();
            info.title = MolCodes.Name(code);
            info.description = code switch
            {
                MolCodes.MasterMix => "Master Mix 2×: đệm PCR, MgCl₂, 4 loại dNTP, enzyme Taq polymerase và chất nạp mẫu xanh. Giữ trên đá.",
                MolCodes.Primers => "Cặp mồi khuếch đại gen GFP (Tm ≈ 60 °C): mồi xuôi mang đuôi cắt EcoRI, mồi ngược mang đuôi cắt BamHI.",
                MolCodes.Template => "ADN khuôn: plasmid mang gen GFP, 1 ng/µL.",
                MolCodes.Water => "Nước không có enzyme phân hủy ADN (nuclease), dùng bù cho đủ thể tích phản ứng.",
                MolCodes.Ladder => "Thang ADN 100 bp đã trộn sẵn chất nạp mẫu: 11 băng 100 – 1000 bp và 1500 bp, băng 500 bp đậm.",
                MolCodes.LoadingDye => "Đệm nạp mẫu 6× (glixerol + thuốc nhuộm theo dõi): làm mẫu chìm vào giếng gel.",
                _ => ""
            };
            info.usage = "Cầm micropipet đã gắn đầu côn mới, trỏ vào ống rồi bấm để hút đúng thể tích đã đặt. Mỗi thuốc thử một đầu côn mới.";
            return rt;
        }

        static TipWaste MakeTipWaste(Transform room, Transform anchor, Vector3 local, string name)
        {
            var go = NewTool(room, anchor, name, local, 0, "thung_dau_con");
            Cyl(go, "Thân", 0, 0.045f, 0.1f, "AnToan_Vang", new Color(0.95f, 0.8f, 0.1f));
            Cyl(go, "Miệng", 0.1f, 0.03f, 0.004f, "NhuaDen", Color.black);
            FitBoxCollider(go, false);
            return go.AddComponent<TipWaste>();
        }

        static TipBox MakeTipBox(Transform room, Transform anchor, Vector3 local, string kind)
        {
            var go = NewTool(room, anchor, $"Hộp đầu côn {kind} (khu hậu PCR)", local, 0, "hop_dau_con");
            BoxPart(go, "Hộp", new Vector3(0, 0.03f, 0), new Vector3(0.08f, 0.06f, 0.12f), kind == "xanh" ? "T2_HopDauConXanh" : "T2_HopDauConVang",
                    kind == "xanh" ? new Color(0.2f, 0.45f, 0.95f) : new Color(0.95f, 0.8f, 0.15f));
            BoxPart(go, "Nắp trong", new Vector3(0, 0.062f, 0), new Vector3(0.08f, 0.004f, 0.12f), "ThuyTinh", Glass);
            FitBoxCollider(go, false);
            var tb = go.AddComponent<TipBox>();
            tb.tipKind = kind;
            return tb;
        }

        /// <summary>Micropipet dựng bằng khối (khu hậu PCR không có giá pipet mô hình).</summary>
        static Micropipette MakePipette(Transform room, Transform anchor, Vector3 local, string id, Color button)
        {
            var go = NewTool(room, anchor, $"Micropipet {id.Replace("micropipet_", "").ToUpperInvariant()} (khu hậu PCR)", local, 0, id);
            Cyl(go, "Mũi", 0, 0.004f, 0.06f, "NhuaTrang", Color.white);
            Cyl(go, "Thân", 0.06f, 0.013f, 0.15f, "NhuaTrang", new Color(0.95f, 0.95f, 0.95f));
            Cyl(go, "Nút đẩy", 0.21f, 0.008f, 0.025f, "NhuaTrang", button);
            Ensure<BioTool>(go).kind = BioToolKind.Micropipette;
            MakeGrabbable(go, 0.15f);
            var mp = go.AddComponent<Micropipette>();
            mp.ConfigureRangeById(id);
            return mp;
        }

        /// <summary>Biến 3 cây micropipet của một giá mô hình (PipetteStation) thành micropipet cầm được, hộp đầu côn bấm được.</summary>
        static void BindPipetteStation(Transform station, Transform room, string wasteName, Vector3 wasteLocal)
        {
            if (station == null) return;
            string[] ids = { "micropipet_p10", "micropipet_p200", "micropipet_p1000" };
            int k = 0;
            var kids = new List<Transform>();
            foreach (Transform c in station) kids.Add(c);
            for (int i = 0; i < kids.Count; i++)
            {
                var c = kids[i];
                if (c.name == "Micropipet" && k < ids.Length)
                {
                    if (i + 1 < kids.Count && kids[i + 1].name == "Nút đẩy") kids[i + 1].SetParent(c, true);  // nút đi theo thân khi cầm
                    var mp = Bind<Micropipette>(c, ids[k], grabbable: true, grip: 0.15f);
                    FitBoxCollider(c.gameObject, false);
                    mp.ConfigureRangeById(ids[k]);
                    Ensure<BioTool>(c.gameObject).kind = BioToolKind.Micropipette;
                    c.name = $"Micropipet {ids[k].Replace("micropipet_", "").ToUpperInvariant()}";
                    k++;
                }
            }
            var boxes = FindAllNamed(station, "Hộp đầu côn");
            for (int i = 0; i < boxes.Count; i++)
            {
                var tb = Bind<TipBox>(boxes[i], "hop_dau_con");
                tb.tipKind = i == 0 ? "vang" : "xanh";
                SetInfoTitle(boxes[i].gameObject, $"Hộp đầu côn {(i == 0 ? "vàng (P10, P200)" : "xanh (P1000)")}");
            }
            if (!string.IsNullOrEmpty(wasteName)) MakeTipWaste(room, station, wasteLocal, wasteName);
        }

        // =====================================================================
        // Tự chạy khi vào Play (cảnh dựng trước khi có bản này)
        // =====================================================================

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoInitFloor2ToolsOnPlay()
        {
            var tang2 = GameObject.Find("Tầng 2");
            if (tang2 != null) EnsureSpawnAllTools(tang2.transform);
        }

        // =====================================================================
        // Gắn toàn bộ
        // =====================================================================

        public static void EnsureSpawnAllTools(Transform tang2)
        {
            if (tang2 == null || tang2.GetComponent<BioFloor2ToolsMarker>() != null) return;
            BioCatalogLoader.EnsureLoaded();

            var bio = tang2.Find("Phòng thí nghiệm sinh học");
            var micro = tang2.Find("Phòng vi sinh");
            var store = tang2.Find("Kho sinh phẩm");
            var hall = tang2.Find("Hành lang tầng 2");
            var mol = tang2.Find("Phòng sinh học phân tử");
            var green = tang2.Find("Nhà kính thực vật");

            if (bio != null) SetupBioLab(bio);
            if (micro != null) SetupMicrobiology(micro);
            if (store != null) SetupStorage(store);
            if (hall != null) SetupCorridor(hall);
            if (mol != null) SetupMolecular(mol);
            if (green != null) SetupGreenhouse(green);

            if (tang2.GetComponent<BioLabSync>() == null) tang2.gameObject.AddComponent<BioLabSync>();
            tang2.gameObject.AddComponent<BioFloor2ToolsMarker>();
        }

        // ---------------------------------------------------------------------
        // Phòng thí nghiệm sinh học chung
        // ---------------------------------------------------------------------
        static void SetupBioLab(Transform bio)
        {
            foreach (var mc in bio.GetComponentsInChildren<MicroscopeController>(true))
            {
                if (mc.GetComponent<Collider>() == null)
                {
                    var bc = mc.gameObject.AddComponent<BoxCollider>();
                    bc.size = new Vector3(0.24f, 0.38f, 0.26f);
                    bc.center = new Vector3(0, 0.19f, 0);
                }
                BioCatalogLoader.AttachToolInfo(mc.gameObject, mc.isTeacherCamera ? "kinh_hien_vi_camera" : "kinh_hien_vi");
            }
            foreach (var dat in bio.GetComponentsInChildren<DigitalAnatomyTable>(true))
            {
                if (dat.GetComponent<Collider>() == null)
                {
                    var bc = dat.gameObject.AddComponent<BoxCollider>();
                    bc.size = new Vector3(1.8f, 0.9f, 0.9f);
                    bc.center = new Vector3(0, 0.45f, 0);
                }
                BioCatalogLoader.AttachToolInfo(dat.gameObject, "ban_giai_phau_so");
            }

            // máy, tủ, khu an toàn đã có mô hình
            Bind<BiosafetyCabinetController>(bio, "Tủ an toàn sinh học 1", "tu_an_toan_sinh_hoc");
            Bind<BiosafetyCabinetController>(bio, "Tủ an toàn sinh học 2", "tu_an_toan_sinh_hoc");
            var inc = FindNamed(bio, "Khu tủ ấm và nồi hấp");
            Bind<IncubatorController>(inc, "Tủ ấm 37 °C", "tu_am");
            Bind<WaterBathController>(inc, "Bể ổn nhiệt", "be_on_nhiet");
            Bind<CentrifugeLoad>(inc, "Máy ly tâm", "may_ly_tam_de_ban");
            Bind<EquipmentInfo>(inc, "Cân phân tích", "can_phan_tich");
            Bind<EquipmentInfo>(inc, "Máy đo pH", "may_do_ph");
            Bind<AutoclaveController>(inc, "Nồi hấp tiệt trùng", "noi_hap");
            Bind<PassBoxController>(bio, "Pass-box sang phòng vi sinh", "pass_box");
            Bind<SpillKit>(bio, "Bộ xử lý tràn sinh học", "bo_xu_ly_tran");
            Bind<FirstAidCabinet>(bio, "Hộp y tế", "tu_so_cuu");
            Bind<PpeStation>(bio, "Tủ đồ bảo hộ", "do_bao_ho");
            Bind<SkeletonStand>(bio, "Bộ xương trên giá", "bo_xuong_gia");
            Bind<HandWashStation>(bio, "Quầy rửa tay", "quay_rua_tay");
            Bind<ClassroomConsole>(bio, "Bàn giáo viên", "bang_dieu_khien_lop");
            foreach (var bin in FindAllNamed(bio, "Thùng rác nguy hại sinh học")) Bind<BiohazardBin>(bin, "thung_rac_nguy_hai");

            // ---- Bàn sinh học 1 (bàn thực hành mẫu, trạm 21): khay làm tiêu bản ở mép trước, giữa kính 1 và kính 2 ----
            // hệ tọa độ bàn: x là bề sâu (học sinh ngồi phía +x, mép trước x = 0,45), z dọc bàn; mặt bàn y = 0,8
            var b1 = FindNamed(bio, "Bàn sinh học 1");
            if (b1 != null)
            {
                const float top = 0.8f;
                var tray = NewTool(bio, b1, "Khay làm tiêu bản (Bàn 1)", new Vector3(0.355f, top, -0.42f), 0, "");
                BoxPart(tray, "Khay", new Vector3(0, 0.005f, 0), new Vector3(0.15f, 0.01f, 0.5f), "Inox", Steel);
                float ty = top + 0.01f;
                Stick(bio, b1, "Kim mũi mác (Bàn 1)", new Vector3(0.355f, ty, -0.63f), "kim_mui_mac", 0.13f, 0.003f, "Inox", Steel, "NhuaDen", Color.black).AddComponent<DissectingNeedle>();
                Stick(bio, b1, "Kẹp panh mũi nhọn (Bàn 1)", new Vector3(0.355f, ty, -0.595f), "kep_panh", 0.12f, 0.004f, "Inox", Steel).AddComponent<Forceps>();
                Stick(bio, b1, "Dao lam (Bàn 1)", new Vector3(0.355f, ty, -0.56f), "dao_lam", 0.05f, 0.006f, "Inox", Steel).AddComponent<RazorBlade>();
                Stick(bio, b1, "Kéo nhỏ (Bàn 1)", new Vector3(0.355f, ty, -0.525f), "keo_nho", 0.11f, 0.004f, "Inox", Steel, "NhuaDen", Color.black).AddComponent<Scissors>();
                // ống nhỏ giọt Pasteur mang nước cất: nhỏ lên lam kính làm tiêu bản tươi
                var pasteur = Stick(bio, b1, "Ống nhỏ giọt (Bàn 1)", new Vector3(0.355f, ty, -0.49f), "ong_nho_giot", 0.12f, 0.004f, "ThuyTinh", Glass, "NhuaDo", new Color(0.8f, 0.15f, 0.1f));
                var pbt = Ensure<BioTool>(pasteur);
                pbt.kind = BioToolKind.StainDropper;
                pbt.reagentOrSampleId = "nuoc_cat";
                pbt.reagentDisplayName = "Nước cất";

                var slide = NewTool(bio, b1, "Lam kính (Bàn 1)", new Vector3(0.355f, ty, -0.40f), 90, "lam_kinh");
                BoxPart(slide, "Lam", new Vector3(0, 0.001f, 0), new Vector3(0.025f, 0.002f, 0.075f), "ThuyTinh", Glass);
                MakeGrabbable(slide, 0.004f);
                slide.AddComponent<GlassSlide>();
                var slip = NewTool(bio, b1, "Hộp lamen (Bàn 1)", new Vector3(0.355f, ty, -0.31f), 0, "lamen");
                BoxPart(slip, "Hộp", new Vector3(0, 0.008f, 0), new Vector3(0.035f, 0.016f, 0.035f), "HopLamKinh", new Color(0.85f, 0.9f, 0.95f, 0.7f));
                MakeGrabbable(slip, 0.01f);
                slip.AddComponent<CoverSlip>();
                var tissue = NewTool(bio, b1, "Giấy lau kính (Bàn 1)", new Vector3(0.355f, ty, -0.23f), 0, "giay_lau_kinh");
                BoxPart(tissue, "Tập giấy", new Vector3(0, 0.004f, 0), new Vector3(0.06f, 0.008f, 0.07f), "NhuaTrang", new Color(0.97f, 0.97f, 0.94f));
                MakeGrabbable(tissue, 0.006f);
                tissue.AddComponent<LensTissue>();

                // lọ dầu soi, đồng hồ bấm giờ, bút ghi nhãn giữa kính 2 và kính 3
                var oil = NewTool(bio, b1, "Lọ dầu soi kính (Bàn 1)", new Vector3(0.36f, top, 0.24f), 0, "lo_dau_soi");
                Cyl(oil, "Lọ", 0, 0.014f, 0.045f, "NhuaDo", new Color(0.7f, 0.4f, 0.1f));
                Cyl(oil, "Nắp", 0.045f, 0.008f, 0.02f, "NhuaDen", Color.black);
                MakeGrabbable(oil, 0.03f);
                oil.AddComponent<ImmersionOil>();
                var timer = NewTool(bio, b1, "Đồng hồ bấm giờ (Bàn 1)", new Vector3(0.36f, top, 0.36f), 0, "dong_ho_bam_gio");
                Cyl(timer, "Thân", 0, 0.03f, 0.018f, "NhuaDen", new Color(0.12f, 0.12f, 0.14f));
                MakeGrabbable(timer, 0.01f);
                timer.AddComponent<BenchTimer>();
                Stick(bio, b1, "Bút ghi nhãn (Bàn 1)", new Vector3(0.36f, top, 0.46f), "but_ghi_nhan", 0.13f, 0.005f, "NhuaXanh", new Color(0.15f, 0.3f, 0.85f)).AddComponent<LabelPen>();

                // mô hình đã có trên bàn: hộp tiêu bản, giấy thấm, lọ thuốc nhuộm trên giá (cầm lên để nhỏ giọt lên lam)
                Bind<SlideBox>(b1, "Hộp tiêu bản", "hop_tieu_ban");
                Bind<BlottingPaper>(FindNamed(b1, "Giấy thấm"), "giay_tham", grabbable: true, grip: 0.005f);
                BindDropper(b1, "Chai Xanh methylen", "xanh_methylen", "Xanh methylen 1 %", 0f);
                BindDropper(b1, "Chai Lugol", "iot_lugol", "Dung dịch Lugol (I₂/KI)", 0f);
                BindDropper(b1, "Chai nước cất", "nuoc_cat", "Nước cất", 0f);
                BindDropper(b1, "Chai NaCl 5 %", "nacl_uu_truong", "NaCl 5 % (ưu trương)", 0.86f);
                // đèn LED kẹp trên giá thuốc thử, chiếu xuống chỗ làm tiêu bản
                var lamp = NewTool(bio, b1, "Đèn bàn LED (Bàn 1)", new Vector3(-0.36f, 1.15f, 0.5f), 0, "den_ban_led");
                Cyl(lamp, "Đế kẹp", 0, 0.02f, 0.02f, "NhuaDen", Color.black);
                Part(lamp, "Cần", PrimitiveType.Cylinder, new Vector3(0.08f, 0.1f, 0), new Vector3(0.008f, 0.1f, 0.008f), "Inox", Steel, new Vector3(0, 0, -50));
                BoxPart(lamp, "Chụp đèn", new Vector3(0.17f, 0.15f, 0), new Vector3(0.09f, 0.015f, 0.04f), "NhuaTrang", Color.white);
                FitBoxCollider(lamp, false);
                lamp.AddComponent<DeskLamp>();
            }
            var b4 = FindNamed(bio, "Bàn sinh học 4");
            if (b4 != null) BindDropper(b4, "Chai H₂O₂ 3 %", "h2o2_3_phan_tram", "Hiđrô peoxit H₂O₂ 3 %", 0.88f);

            // ---- Bàn sinh học 2: lên men rượu và làm sữa chua (giữa bàn, chỗ trống sau kính hiển vi) ----
            var b2 = FindNamed(bio, "Bàn sinh học 2");
            if (b2 != null)
            {
                var flask = NewTool(bio, b2, "Bình lên men rượu + bóng bay (Bàn 2)", new Vector3(-0.08f, 0.8f, -0.12f), 0, "binh_len_men");
                Cyl(flask, "Bình", 0, 0.04f, 0.09f, "CocThuyTinh", Glass);
                Cyl(flask, "Dịch đường + men", 0.002f, 0.037f, 0.05f, "NhuaTrang", new Color(0.95f, 0.85f, 0.55f, 0.8f));
                Cyl(flask, "Cổ bình", 0.09f, 0.012f, 0.04f, "CocThuyTinh", Glass);
                Part(flask, "Bóng bay", PrimitiveType.Sphere, new Vector3(0, 0.145f, 0), new Vector3(0.035f, 0.04f, 0.035f), "NhuaDo", new Color(0.9f, 0.2f, 0.2f));
                MakeGrabbable(flask, 0.05f);
                flask.AddComponent<FermentationFlask>();
                var jar = NewTool(bio, b2, "Hũ làm sữa chua (Bàn 2)", new Vector3(-0.08f, 0.8f, 0.1f), 0, "bo_lam_sua_chua");
                Cyl(jar, "Hũ", 0, 0.035f, 0.07f, "ThuyTinh", Glass);
                Cyl(jar, "Sữa", 0.002f, 0.032f, 0.055f, "NhuaTrang", new Color(0.98f, 0.97f, 0.92f));
                Cyl(jar, "Nắp", 0.07f, 0.037f, 0.008f, "NhuaXanh", new Color(0.2f, 0.55f, 0.9f));
                MakeGrabbable(jar, 0.04f);
                jar.AddComponent<YogurtJar>();
            }

            // ---- Quầy rửa tay (tường bắc): dụng cụ thủy tinh sạch và dụng cụ đo đặt cạnh giá úp ----
            // hệ quầy: mặt trước +x, tường −x, z dọc quầy (bồn ở z = 0,55, giá úp ở z −0,75..−0,35), mặt quầy y = 0,88
            var wash = FindNamed(bio, "Quầy rửa tay");
            if (wash != null)
            {
                const float top = 0.88f;
                var beaker = NewTool(bio, wash, "Cốc có mỏ 100 mL", new Vector3(0.1f, top, -0.2f), 0, "coc_co_mo");
                Cyl(beaker, "Cốc", 0, 0.026f, 0.07f, "CocThuyTinh", Glass);
                MakeGrabbable(beaker, 0.05f);
                var bc = beaker.AddComponent<LabContainer>();
                bc.label = "Cốc có mỏ 100 mL";
                bc.capacityMl = 100f;
                bc.bottomRadiusM = 0.026f;
                var cyl = NewTool(bio, wash, "Ống đong 50 mL", new Vector3(-0.1f, top, -0.2f), 0, "ong_dong");
                Cyl(cyl, "Đế", 0, 0.025f, 0.008f, "ThuyTinh", Glass);
                Cyl(cyl, "Ống", 0.008f, 0.012f, 0.17f, "ThuyTinh", Glass);
                MakeGrabbable(cyl, 0.1f);
                var cc = cyl.AddComponent<LabContainer>();
                cc.label = "Ống đong 50 mL";
                cc.capacityMl = 50f;
                cc.bottomRadiusM = 0.012f;
                var watch = NewTool(bio, wash, "Đĩa đồng hồ Ø 75 mm", new Vector3(0.15f, top, 0.0f), 0, "dia_dong_ho");
                Cyl(watch, "Đĩa", 0, 0.037f, 0.006f, "ThuyTinh", Glass);
                MakeGrabbable(watch, 0.004f);
                watch.AddComponent<WatchGlass>();
                var funnel = NewTool(bio, wash, "Phễu lọc + giấy lọc", new Vector3(-0.05f, top, 0.0f), 0, "pheu_loc");
                Cyl(funnel, "Cuống", 0, 0.005f, 0.05f, "ThuyTinh", Glass);
                Part(funnel, "Miệng phễu", PrimitiveType.Cylinder, new Vector3(0, 0.075f, 0), new Vector3(0.07f, 0.025f, 0.07f), "ThuyTinh", Glass);
                MakeGrabbable(funnel, 0.06f);
                funnel.AddComponent<FilterFunnel>();
                var mortar = NewTool(bio, wash, "Cối chày sứ Ø 9 cm", new Vector3(0.12f, top, 0.22f), 0, "coi_chay_su");
                Cyl(mortar, "Cối", 0, 0.045f, 0.045f, "MatBanInox", new Color(0.93f, 0.93f, 0.9f));
                Part(mortar, "Chày", PrimitiveType.Cylinder, new Vector3(0.015f, 0.07f, 0), new Vector3(0.016f, 0.045f, 0.016f), "MatBanInox", new Color(0.93f, 0.93f, 0.9f), new Vector3(0, 0, 25));
                MakeGrabbable(mortar, 0.04f);
                mortar.AddComponent<MortarPestle>();
                Stick(bio, wash, "Đũa thủy tinh", new Vector3(0.2f, top, -0.02f), "dua_thuy_tinh", 0.2f, 0.003f, "ThuyTinh", Glass).AddComponent<GlassRod>();
                Stick(bio, wash, "Nhiệt kế", new Vector3(0.2f, top, 0.06f), "nhiet_ke", 0.22f, 0.0035f, "ThuyTinh", Glass, "NhuaDo", new Color(0.85f, 0.1f, 0.1f)).AddComponent<Thermometer>();
                var ph = NewTool(bio, wash, "Tập giấy chỉ thị pH", new Vector3(-0.12f, top, 0.12f), 0, "giay_chi_thi_ph");
                BoxPart(ph, "Hộp giấy", new Vector3(0, 0.006f, 0), new Vector3(0.05f, 0.012f, 0.07f), "NhuaDo", new Color(0.9f, 0.75f, 0.2f));
                MakeGrabbable(ph, 0.008f);
                ph.AddComponent<TestPaper>();
                // giá ống nghiệm 6 ống ở đoạn trống cuối quầy (z −1,15..−0,8)
                var rack = NewTool(bio, wash, "Giá ống nghiệm 6 lỗ", new Vector3(0.0f, top, -0.98f), 90, "gia_ong_nghiem");
                BoxPart(rack, "Giá", new Vector3(0, 0.04f, 0), new Vector3(0.2f, 0.012f, 0.06f), "GiaNhuaXanh", new Color(0.2f, 0.45f, 0.8f));
                BoxPart(rack, "Đế", new Vector3(0, 0.003f, 0), new Vector3(0.2f, 0.006f, 0.06f), "GiaNhuaXanh", new Color(0.2f, 0.45f, 0.8f));
                FitBoxCollider(rack, false);
                var sockets = rack.AddComponent<TubeRackSockets>();
                sockets.rackToolId = "gia_ong_nghiem";
                sockets.acceptedTubeType = "ong_nghiem";
                for (int i = 0; i < 3; i++)
                {
                    var tt = NewTool(bio, wash, $"Ống nghiệm {i + 1}", new Vector3(0.0f, top, -1.04f + i * 0.06f), 0, "ong_nghiem");
                    Cyl(tt, "Ống", 0, 0.009f, 0.15f, "ThuyTinh", Glass);
                    MakeGrabbable(tt, 0.1f);
                    var tc = tt.AddComponent<LabContainer>();
                    tc.label = $"Ống nghiệm {i + 1}";
                    tc.capacityMl = 20f;
                    tc.bottomRadiusM = 0.009f;
                }
            }
        }

        static void BindDropper(Transform bench, string modelName, string reagentId, string reagentName, float molarity)
        {
            var m = FindNamed(bench, modelName);
            if (m == null) return;
            var d = Bind<ReagentDropperBottle>(m, "lo_thuoc_thu_nho_giot", grabbable: true, grip: 0.04f);
            d.reagentId = reagentId;
            d.reagentName = reagentName;
            d.molarity = molarity;
            SetInfoTitle(m.gameObject, "Lọ " + reagentName);
        }

        // ---------------------------------------------------------------------
        // Phòng vi sinh
        // ---------------------------------------------------------------------
        static void SetupMicrobiology(Transform micro)
        {
            Bind<BiosafetyCabinetController>(micro, "Tủ an toàn sinh học vi sinh 1", "tu_an_toan_sinh_hoc");
            Bind<BiosafetyCabinetController>(micro, "Tủ an toàn sinh học vi sinh 2", "tu_an_toan_sinh_hoc");
            Bind<IncubatorController>(micro, "Tủ ấm 37 °C", "tu_am");
            Bind<UvCeilingLamp>(micro, "Đèn UV diệt khuẩn", "den_uv_tran");
            Bind<AirlockInterlock>(micro, "Buồng đệm", "buong_dem");
            var island = FindNamed(micro, "Đảo nhuộm Gram");
            Bind<GramStainStation>(island, "dao_nhuom_gram");
            Bind<ColonyCounterController>(island, "Máy đếm khuẩn lạc", "may_dem_khuan_lac");
            var west = FindNamed(micro, "Quầy thấp tường tây");
            Bind<VortexMixer>(west, "Máy lắc vortex", "may_vortex");
            Bind<CentrifugeLoad>(west, "Máy ly tâm", "may_ly_tam_de_ban");
            BindPipetteStation(FindNamed(west, "Bộ micropipet"), micro, "Thùng rác đầu côn (phòng vi sinh)", new Vector3(0.1f, 0f, 0.22f));
            foreach (var bin in FindAllNamed(micro, "Thùng rác nguy hại sinh học")) Bind<BiohazardBin>(bin, "thung_rac_nguy_hai");

            // mép trước đảo nhuộm (phía ghế, z âm), giữa máy đếm khuẩn lạc (x −0,6) và kính hiển vi (x 0): que cấy, que trang
            if (island != null)
            {
                const float top = 0.9f;
                var loop = Stick(micro, island, "Que cấy vòng", new Vector3(-0.32f, top, -0.46f), "que_cay_vong", 0.22f, 0.003f, "Inox", Steel, "NhuaDen", Color.black);
                loop.transform.rotation = island.rotation * Quaternion.FromToRotation(Vector3.up, Vector3.forward);
                loop.AddComponent<InoculatingLoop>();
                var spreader = Stick(micro, island, "Que trang thủy tinh", new Vector3(-0.22f, top, -0.46f), "que_trang", 0.16f, 0.003f, "ThuyTinh", Glass);
                spreader.transform.rotation = island.rotation * Quaternion.FromToRotation(Vector3.up, Vector3.forward);
                spreader.AddComponent<CellSpreader>();
                var dish = NewTool(micro, island, "Đĩa petri thạch dinh dưỡng", new Vector3(-1.2f, top, -0.38f), 0, "dia_petri");
                Cyl(dish, "Đĩa", 0, 0.045f, 0.014f, "T2_DiaPetri", new Color(0.92f, 0.95f, 1f, 0.5f));
                Cyl(dish, "Thạch", 0.001f, 0.042f, 0.008f, "T2_GelAgarose", new Color(0.95f, 0.88f, 0.65f, 0.9f));
                MakeGrabbable(dish, 0.01f);
                dish.AddComponent<PetriDish>();
            }
            // quầy tây (tường x = 20, mặt trước +x): dãy ống pha loãng ở đầu quầy trống, ống canh thang cạnh giá ống
            if (west != null)
            {
                var set = NewTool(micro, west, "Dãy 6 ống pha loãng", new Vector3(0.02f, 0.94f, -1.17f), 90, "bo_ong_pha_loang");
                BoxPart(set, "Giá", new Vector3(0, 0.02f, 0), new Vector3(0.2f, 0.04f, 0.05f), "GiaNhuaXanh", new Color(0.2f, 0.5f, 0.9f));
                for (int i = 0; i < 6; i++) Cyl(set, $"Ống 10^-{i + 1}", 0.01f, 0.008f, 0.12f, "ThuyTinh", Glass, -0.075f + i * 0.03f, 0);
                FitBoxCollider(set, false);
                set.AddComponent<DilutionTubeSet>();
                var broth = NewTool(micro, west, "Ống canh thang LB", new Vector3(0.17f, 0.94f, 0.17f), 0, "ong_canh_thang");
                Cyl(broth, "Ống", 0, 0.009f, 0.13f, "ThuyTinh", Glass);
                Cyl(broth, "Canh thang", 0.002f, 0.008f, 0.07f, "NhuaTrang", new Color(0.92f, 0.8f, 0.45f, 0.85f));
                Cyl(broth, "Nút bông", 0.13f, 0.01f, 0.02f, "NhuaTrang", new Color(0.97f, 0.97f, 0.95f));
                MakeGrabbable(broth, 0.09f);
                broth.AddComponent<BrothTube>();
            }
            var prep = FindNamed(micro, "Quầy chuẩn bị");
            if (prep != null)
            {
                var bag = NewTool(micro, prep, "Túi hấp hủy", new Vector3(0.0f, 0.92f, 0.62f), 0, "tui_hap_huy");
                Cyl(bag, "Túi", 0, 0.05f, 0.12f, "AnToan_Vang", new Color(0.95f, 0.85f, 0.1f));
                MakeGrabbable(bag, 0.08f);
                bag.AddComponent<AutoclaveBag>();
            }
        }

        // ---------------------------------------------------------------------
        // Kho sinh phẩm
        // ---------------------------------------------------------------------
        static void SetupStorage(Transform store)
        {
            var fridge = Bind<FridgeController>(store, "Tủ lạnh 4 °C", "tu_mat_4");
            if (fridge != null) { fridge.fridgeId = "tu_mat_4"; fridge.isDeepFreezer = false; fridge.setTempC = 4f; }
            var f20 = Bind<FridgeController>(store, "Tủ đông −20 °C", "tu_dong_am20");
            if (f20 != null) { f20.fridgeId = "tu_dong_am20"; f20.isDeepFreezer = true; f20.setTempC = -20f; }
            var f80 = Bind<FridgeController>(store, "Tủ âm sâu −80 °C", "tu_dong_am20");
            if (f80 != null) { f80.fridgeId = "tu_am_sau_80"; f80.isDeepFreezer = true; f80.setTempC = -80f; SetInfoTitle(f80.gameObject, "Tủ âm sâu −80 °C"); }
            Bind<SpecimenShelf>(store, "Kệ inox hộp sinh phẩm", "ke_mau");
            Bind<StockKiosk>(store, "Bàn ghi chép", "so_kho_dien_tu");
            Bind<TeachingModel>(FindNamed(store, "Bàn mô hình"), "Mô hình ADN", "mo_hinh_day_hoc");
        }

        // ---------------------------------------------------------------------
        // Hành lang
        // ---------------------------------------------------------------------
        static void SetupCorridor(Transform hall)
        {
            Bind<ToolKiosk>(hall, "Trạm tra cứu dụng cụ", "kiosk_tra_cuu");
            Bind<Locker>(hall, "Kệ tủ đồ cá nhân", "tu_do_ca_nhan");
            Bind<DisplayCabinet>(hall, "Tủ trưng bày tiêu bản", "tu_trung_bay");
        }

        // ---------------------------------------------------------------------
        // Phòng sinh học phân tử: tiền PCR → tách chiết → hậu PCR (luồng một chiều)
        // ---------------------------------------------------------------------
        static void SetupMolecular(Transform mol)
        {
            // ---- Tủ thao tác PCR (khu tiền PCR, tường tây). Hệ tủ: mặt trước +x, mặt bàn trong tủ y = 0,87 ----
            var hood = FindNamed(mol, "Tủ thao tác PCR");
            if (hood != null)
            {
                Bind<PcrHoodController>(hood, "tu_thao_tac_pcr");
                BindPipetteStation(FindNamed(hood, "Bộ micropipet"), mol, "Thùng rác đầu côn (tủ PCR)", new Vector3(0.36f, 0f, -0.18f));
                const float top = 0.87f;
                // khay đá mini 4 thuốc thử pha PCR, ngay trong tủ (không phải mang ra ngoài vùng sạch)
                var ice = NewTool(mol, hood, "Khay đá thuốc thử PCR", new Vector3(0.18f, top, 0.30f), 0, "hop_da");
                BoxPart(ice, "Khay xốp", new Vector3(0, 0.015f, 0), new Vector3(0.11f, 0.03f, 0.16f), "NhuaTrang", Color.white);
                BoxPart(ice, "Đá vụn", new Vector3(0, 0.031f, 0), new Vector3(0.1f, 0.003f, 0.15f), "T2_DaLanh", new Color(0.85f, 0.92f, 0.98f));
                FitBoxCollider(ice, false);
                ice.AddComponent<IceBox>();
                MakeReagent(ice.transform, new Vector3(-0.025f, 0.02f, -0.045f), MolCodes.MasterMix, new Color(0.9f, 0.2f, 0.2f), 1000);
                MakeReagent(ice.transform, new Vector3(0.025f, 0.02f, -0.045f), MolCodes.Primers, new Color(0.2f, 0.75f, 0.3f), 200);
                MakeReagent(ice.transform, new Vector3(-0.025f, 0.02f, 0.045f), MolCodes.Template, new Color(0.55f, 0.3f, 0.85f), 200);
                MakeReagent(ice.transform, new Vector3(0.025f, 0.02f, 0.045f), MolCodes.Water, new Color(0.95f, 0.95f, 0.95f), 1500);
                // giá ống PCR với 3 ống trống: 2 mẫu + 1 đối chứng âm
                var rack = NewTool(mol, hood, "Giá ống PCR", new Vector3(0.18f, top, 0.03f), 0, "gia_ong_pcr");
                BoxPart(rack, "Giá", new Vector3(0, 0.01f, 0), new Vector3(0.06f, 0.02f, 0.14f), "NhuaTrang", new Color(0.95f, 0.65f, 0.15f));
                FitBoxCollider(rack, false);
                string[] labels = { "1", "2", "Đ−" };
                for (int i = 0; i < labels.Length; i++)
                    MakeTube(mol, $"Ống PCR {labels[i]}", mol.InverseTransformPoint(hood.TransformPoint(new Vector3(0.18f, top + 0.012f, -0.02f + i * 0.035f))), "ong_pcr", labels[i],
                             i == 2 ? new Color(0.6f, 0.6f, 0.65f) : new Color(0.95f, 0.85f, 0.2f));
            }

            // ---- Quầy tiền PCR (tường nam): vortex, ly tâm mini, hộp đá, giá pipet ----
            var pre = FindNamed(mol, "Quầy tiền PCR");
            Bind<VortexMixer>(pre, "Máy lắc vortex", "may_vortex");
            Bind<CentrifugeLoad>(pre, "Máy ly tâm", "may_ly_tam_mini");
            Bind<IceBox>(pre, "Hộp đá", "hop_da");
            BindPipetteStation(FindNamed(pre, "Bộ micropipet"), mol, "Thùng rác đầu côn (quầy tiền PCR)", new Vector3(0.12f, 0f, 0.22f));
            var f80 = Bind<FridgeController>(mol, "Tủ đông −80 °C (tiền PCR)", "tu_dong_am20");
            if (f80 != null) { f80.fridgeId = "tu_am_sau_80_phan_tu"; f80.isDeepFreezer = true; f80.setTempC = -80f; SetInfoTitle(f80.gameObject, "Tủ đông −80 °C (tiền PCR)"); }

            // ---- Đảo tách chiết ADN (giữa phòng), mặt đảo y = 0,9 ----
            var isl = FindNamed(mol, "Đảo tách chiết ADN");
            if (isl != null)
            {
                const float top = 0.9f;
                Bind<WaterBathController>(isl, "Bể ổn nhiệt", "be_on_nhiet");
                Bind<CentrifugeLoad>(isl, "Máy ly tâm", "may_ly_tam_de_ban");
                Bind<DryBlockHeater>(isl, "Bộ gia nhiệt khô", "khoi_gia_nhiet_kho");
                Bind<Od600Controller>(isl, "Máy đo quang phổ (OD600)", "may_quang_pho_od600");
                BindPipetteStation(FindNamed(isl, "Bộ micropipet"), mol, "Thùng rác đầu côn (đảo tách chiết)", new Vector3(-0.15f, 0f, -0.05f));
                var rack = NewTool(mol, isl, "Giá ống eppendorf", new Vector3(-0.2f, top, 0.32f), 90, "gia_ong_eppendorf");
                BoxPart(rack, "Giá", new Vector3(0, 0.015f, 0), new Vector3(0.12f, 0.03f, 0.05f), "GiaNhuaXanh", new Color(0.25f, 0.65f, 0.35f));
                FitBoxCollider(rack, false);
                var kit = Bind<DnaExtractionKit>(isl, "Bộ tách chiết ADN chuối", "");
                if (kit != null) kit.outputRack = rack.transform;
                var mortar = NewTool(mol, isl, "Cối chày sứ (tách chiết)", new Vector3(0.28f, top, 0.34f), 0, "coi_chay_su");
                Cyl(mortar, "Cối", 0, 0.045f, 0.045f, "MatBanInox", new Color(0.93f, 0.93f, 0.9f));
                MakeGrabbable(mortar, 0.04f);
                mortar.AddComponent<MortarPestle>();
                var funnel = NewTool(mol, isl, "Phễu lọc (tách chiết)", new Vector3(0.45f, top, 0.34f), 0, "pheu_loc");
                Cyl(funnel, "Cuống", 0, 0.005f, 0.05f, "ThuyTinh", Glass);
                Part(funnel, "Miệng phễu", PrimitiveType.Cylinder, new Vector3(0, 0.075f, 0), new Vector3(0.07f, 0.025f, 0.07f), "ThuyTinh", Glass);
                MakeGrabbable(funnel, 0.06f);
                funnel.AddComponent<FilterFunnel>();
                var cuvet = NewTool(mol, isl, "Cuvet đo quang 1 cm", new Vector3(1.25f, top, -0.32f), 0, "cuvet");
                BoxPart(cuvet, "Cuvet", new Vector3(0, 0.0225f, 0), new Vector3(0.0125f, 0.045f, 0.0125f), "ThuyTinh", Glass);
                MakeGrabbable(cuvet, 0.03f);
                cuvet.AddComponent<Cuvette>();
            }

            // ---- Quầy hậu PCR (tường nam, mặt trước +z, mặt quầy y = 0,85): máy PCR → đổ gel → điện di → chụp gel ----
            var post = FindNamed(mol, "Quầy hậu PCR");
            if (post != null)
            {
                const float top = 0.85f;
                for (int i = 1; i <= 2; i++)
                {
                    var tc = Bind<ThermalCyclerController>(post, $"Máy PCR {i}", "may_pcr");
                    if (tc != null) { tc.machineName = $"Máy PCR {i}"; SetInfoTitle(tc.gameObject, $"Máy PCR {i} (luân nhiệt 96 giếng)"); }
                }
                var tank = Bind<GelTankController>(post, "Bộ điện di gel", "be_dien_di");
                var psu = Bind<PowerSupplyController>(post, "Nguồn điện di", "nguon_dien_di");
                if (tank != null && psu != null) { tank.psu = psu; psu.connectedTank = tank; }
                var doc = Bind<GelDocController>(post, "Buồng chụp gel", "gel_doc");
                if (doc != null) doc.sourceTank = tank;

                // khu đổ gel: khoảng trống giữa máy PCR 2 (x −1,3) và bể điện di (x −0,3)
                var mw = NewTool(mol, post, "Lò vi sóng (đun agarose)", new Vector3(-0.8f, top, -0.06f), 0, "lo_vi_song");
                BoxPart(mw, "Vỏ", new Vector3(0, 0.11f, 0), new Vector3(0.36f, 0.22f, 0.28f), "NhuaTrang", new Color(0.92f, 0.92f, 0.9f));
                BoxPart(mw, "Cửa kính", new Vector3(-0.04f, 0.11f, 0.141f), new Vector3(0.24f, 0.16f, 0.004f), "NhuaDen", new Color(0.1f, 0.1f, 0.12f));
                BoxPart(mw, "Bảng nút", new Vector3(0.13f, 0.11f, 0.141f), new Vector3(0.07f, 0.16f, 0.004f), "NhuaDen", new Color(0.2f, 0.2f, 0.22f));
                Cyl(mw, "Bình agarose", 0.22f, 0.03f, 0.08f, "CocThuyTinh", Glass, -0.08f, 0.05f);
                FitBoxCollider(mw, false);
                mw.AddComponent<MicrowaveOven>();
                var tray = NewTool(mol, post, "Khay đúc gel + lược 8 giếng", new Vector3(-0.55f, top, 0.2f), 0, "khay_duc_gel");
                var trayVis = BoxPart(tray, "Visual", new Vector3(0, 0.012f, 0), new Vector3(0.13f, 0.024f, 0.11f), "BeDienDi_NhuaTrong", new Color(0.9f, 0.95f, 1f, 0.5f));
                BoxPart(tray, "Lược", new Vector3(-0.045f, 0.03f, 0), new Vector3(0.004f, 0.02f, 0.1f), "NhuaDen", new Color(0.15f, 0.15f, 0.18f));
                MakeGrabbable(tray, 0.015f);
                tray.AddComponent<GelCastingTray>();
                // thang chuẩn + đệm nạp mẫu ngay trước bể điện di
                var lrack = NewTool(mol, post, "Giá thang ADN và đệm nạp mẫu", new Vector3(-0.12f, top, 0.22f), 0, "");
                BoxPart(lrack, "Giá", new Vector3(0, 0.01f, 0), new Vector3(0.08f, 0.02f, 0.035f), "NhuaTrang", new Color(0.92f, 0.92f, 0.9f));
                MakeReagent(lrack.transform, new Vector3(-0.02f, 0.015f, 0), MolCodes.Ladder, new Color(0.2f, 0.45f, 0.95f), 300);
                MakeReagent(lrack.transform, new Vector3(0.02f, 0.015f, 0), MolCodes.LoadingDye, new Color(0.95f, 0.55f, 0.15f), 500);
                // micropipet riêng khu hậu PCR (không mang pipet tiền PCR sang): cuối quầy, sau màn hình ảnh gel
                MakePipette(mol, post, new Vector3(1.86f, top, -0.14f), "micropipet_p10", new Color(0.95f, 0.8f, 0.15f));
                MakeTipBox(mol, post, new Vector3(1.86f, top, 0.03f), "vang");
                MakeTipWaste(mol, post, new Vector3(1.98f, top, 0.2f), "Thùng rác đầu côn (khu hậu PCR)");
            }

            // ---- Trạm ghép gen (tủ thấp tường bắc phía tây): mô hình + thành phần do trình dựng cảnh tạo ----
            Bind<TeachingModel>(FindNamed(mol, "Tủ thấp tường bắc (đông)"), "Mô hình ADN", "mo_hinh_day_hoc");
            foreach (var bin in FindAllNamed(mol, "Thùng rác nguy hại sinh học")) Bind<BiohazardBin>(bin, "thung_rac_nguy_hai");
        }

        // ---------------------------------------------------------------------
        // Nhà kính
        // ---------------------------------------------------------------------
        static void SetupGreenhouse(Transform green)
        {
            Bind<PhotosynthesisRig>(green, "Thí nghiệm quang hợp thải O₂", "tram_quang_hop");
            Bind<PotometerRig>(green, "Thí nghiệm thoát hơi nước (potometer)", "tram_potometer");
            Bind<PhototropismBox>(green, "Thí nghiệm hướng sáng", "hop_huong_sang");
            Bind<NftRackController>(green, "Giàn thủy canh NFT", "gian_thuy_canh_nft");
            Bind<ClimateSensor>(green, "Cảm biến vi khí hậu", "cam_bien_vi_khi_hau");
            var pot = FindNamed(green, "Bàn sang chậu");
            Bind<PottingKit>(pot, "bo_sang_chau");
            Bind<WateringCan>(FindNamed(pot, "Bình tưới"), "binh_tuoi", grabbable: true, grip: 0.2f);
            foreach (var bin in FindAllNamed(green, "Thùng rác nguy hại sinh học")) Bind<BiohazardBin>(bin, "thung_rac_nguy_hai");

            // que đóm cạnh cốc rong (thử khí O₂), bút đo EC / pH cạnh giàn thủy canh
            var photo = FindNamed(green, "Thí nghiệm quang hợp thải O₂");
            var photoTable = photo != null ? FindNamed(photo, "Bàn thí nghiệm") : null;
            if (photoTable != null)
                Stick(green, photoTable, "Que đóm thử khí O₂", new Vector3(0.22f, 0.8f, 0.18f), "que_dom", 0.2f, 0.003f, "Ban", new Color(0.6f, 0.4f, 0.2f), "NhuaDo", new Color(0.3f, 0.1f, 0.05f)).AddComponent<GlowingSplint>();
            if (pot != null)
            {
                var pen = Stick(green, pot, "Bút đo EC / pH thủy canh", new Vector3(-0.2f, 0.82f, 0.2f), "may_do_ec_ph", 0.16f, 0.008f, "NhuaXanh", new Color(0.15f, 0.35f, 0.8f));
                Ensure<EquipmentInfo>(pen).usage = "Nhúng đầu đo vào máng dung dịch dinh dưỡng giàn NFT để đọc EC (mS/cm) và pH.";
                Stick(green, pot, "Nhiệt kế nhà kính", new Vector3(-0.2f, 0.82f, 0.13f), "nhiet_ke", 0.22f, 0.0035f, "ThuyTinh", Glass, "NhuaDo", new Color(0.85f, 0.1f, 0.1f)).AddComponent<Thermometer>();
            }
            // đường ống phun sương sát mái kính (y 3,25), dọc giữa nhà kính
            var mist = NewTool(green, green, "Hệ phun sương", new Vector3(20.2f, 3.25f, -6.3f), 0, "he_phun_suong");
            Part(mist, "Ống chính", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.025f, 6.0f, 0.025f), "Inox", Steel, new Vector3(0, 0, 90));
            for (int i = 0; i < 6; i++)
                Part(mist, "Béc phun", PrimitiveType.Cylinder, new Vector3(-5f + i * 2f, -0.03f, 0), new Vector3(0.015f, 0.025f, 0.015f), "NhuaDen", Color.black);
            FitBoxCollider(mist, false);
            mist.AddComponent<MistSystem>();
        }
    }
}
