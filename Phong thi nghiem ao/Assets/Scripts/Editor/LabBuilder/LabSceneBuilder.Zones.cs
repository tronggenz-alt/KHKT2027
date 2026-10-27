using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Khkt.Demo;
using Khkt.Environment;
using Khkt.Interaction;
using Khkt.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static Khkt.EditorTools.LabKit;

namespace Khkt.EditorTools
{
    /// <summary>
    /// Chia khu để tải cộng dồn: cửa trượt tự động giữa các khu, lối vào phòng hóa chất (vách kính mờ, cửa kính trượt),
    /// nướng sẵn lưới đi của robot, rồi tách phần tĩnh của mỗi khu ra cảnh riêng (Assets/Scenes/Khu/*.unity).
    /// Đồ tương tác ở lại cảnh lõi dưới gốc khu (ZoneRoot), để khi khu bị gỡ chúng chỉ "ngủ" và giữ nguyên trạng thái.
    /// </summary>
    public static partial class LabSceneBuilder
    {
        // =====================================================================
        // Cửa trượt tự động
        // =====================================================================

        /// <summary>
        /// Cửa trượt tự động hai cánh trong lỗ cửa [o.a, o.b] của tường (alongX, c). Khung, hộp ray và mắt cảm biến là vật tĩnh;
        /// hai cánh và đèn trạng thái nằm trong nhóm có AutoDoor (gốc ở giữa khung trên mặt sàn, trục z chỉ từ khu A sang khu B).
        /// wallT: độ dày tường (vách kính mỏng hơn tường gạch).
        /// </summary>
        static void AutoDoorFill(Transform p, bool alongX, float c, Opening o, float wallT = T)
        {
            float len = o.b - o.a, mid = (o.a + o.b) / 2, h = o.top;
            string name = $"Cửa tự động {o.a:0.0}-{o.b:0.0}";
            const float f = 0.06f;
            var frame = Group(name + " (khung)", p);
            Box(frame, "Khung trái", P(alongX, c, o.a + f / 2, h / 2, 0), S(alongX, f, h, wallT + 0.03f), doorFrame);
            Box(frame, "Khung phải", P(alongX, c, o.b - f / 2, h / 2, 0), S(alongX, f, h, wallT + 0.03f), doorFrame);
            Box(frame, "Khung trên", P(alongX, c, mid, h - f / 2, 0), S(alongX, len, f, wallT + 0.03f), doorFrame);
            float face = o.side * (wallT / 2 + 0.065f);  // tâm hộp ray, nhô khỏi mặt tường phía lắp cánh
            Box(frame, "Hộp ray cửa", P(alongX, c, mid, h + 0.09f, face), S(alongX, len * 2f + 0.1f, 0.18f, 0.11f), doorFrame, false);
            NoShadow(Box(frame, "Mắt cảm biến", P(alongX, c, mid, h - 0.012f, face), S(alongX, 0.14f, 0.02f, 0.06f), darkGlass, false));

            // Nhóm động: cánh cửa + đèn trạng thái (không gộp lô tĩnh vì di chuyển / đổi màu)
            var door = Group(name, p, P(alongX, c, mid, 0, 0), alongX ? 0f : 90f);
            float dz = o.side * (wallT / 2 + 0.035f);
            float leafW = len / 2 - 0.004f, leafH = h - f - 0.01f;
            var leaves = new Transform[2];
            var slide = new Vector3[2];
            for (int i = 0; i < 2; i++)
            {
                int s = i == 0 ? -1 : 1;
                var leaf = Group(i == 0 ? "Cánh trái" : "Cánh phải", door, new Vector3(s * len / 4, 0, dz));
                LeafBody(leaf, leafW, leafH, s, o);
                leaves[i] = leaf;
                slide[i] = new Vector3(s * (len / 2 - 0.03f), 0, 0);
            }
            var l1 = Box(door, "Đèn trạng thái cửa", new Vector3(0, h + 0.13f, face + o.side * 0.0565f), new Vector3(len * 0.7f, 0.025f, 0.004f), ledBlue, false);
            var l2 = Box(door, "Đèn trạng thái cửa", new Vector3(0, h + 0.13f, -o.side * (wallT / 2 + 0.003f)), new Vector3(len * 0.7f, 0.025f, 0.004f), ledBlue, false);
            NoShadow(l1);
            NoShadow(l2);
            var ad = door.gameObject.AddComponent<AutoDoor>();
            ad.doorName = $"Cửa {ZoneMap.Name(o.zoneA)} – {ZoneMap.Name(o.zoneB)}";
            ad.leaves = leaves;
            ad.slide = slide;
            ad.width = len;
            ad.zoneA = o.zoneA;
            ad.zoneB = o.zoneB;
            ad.statusLights = new[] { l1.GetComponent<Renderer>(), l2.GetComponent<Renderer>() };
            // Cổng che khuất ở khung cửa (B1): cửa đóng thì phần phía sau bị che, AutoDoor mở cổng khi cửa mở.
            // Theo tài liệu Unity, vật mang cổng không được đánh dấu Occluder / Occludee Static (nhóm cửa không gộp lô tĩnh).
            var portal = door.gameObject.AddComponent<OcclusionPortal>();
            var pso = new SerializedObject(portal);
            pso.FindProperty("m_Center").vector3Value = new Vector3(0, h / 2, 0);
            pso.FindProperty("m_Size").vector3Value = new Vector3(len, h, wallT + 0.12f);
            pso.FindProperty("m_Open").boolValue = false;
            pso.ApplyModifiedPropertiesWithoutUndo();
            ad.portal = portal;
            var info = door.gameObject.AddComponent<EquipmentInfo>();
            info.title = ad.doorName;
            info.description = "Cửa trượt tự động: tới gần là mở. Đèn hổ phách: đang tải khu bên kia, chờ một chút. "
                             + "Đi qua rồi cửa tự đóng, khu phía sau được gỡ khỏi bộ nhớ nhưng mọi thứ trong đó vẫn giữ nguyên.";
        }

        /// <summary>Một cánh cửa (gốc ở mép dưới, giữa cánh). s: cánh bên trái (-1) hay phải (+1) để biết mép giữa ở phía nào.</summary>
        static void LeafBody(Transform leaf, float w, float h, int s, Opening o)
        {
            const float t = 0.04f;
            if (!o.glass)
            {
                // cánh HPL màu nhận diện của khu, ô kính mờ dọc mép giữa, tay nắm hai mặt, ốp chân inox
                Box(leaf, "Cánh cửa", new Vector3(0, h / 2, 0), new Vector3(w, h, t), o.leaf);
                Box(leaf, "Ô kính mờ", new Vector3(-s * (w / 2 - 0.17f), 1.4f, 0), new Vector3(0.1f, 1.1f, t + 0.006f), frostedGlass, false);
                foreach (int sd in new[] { -1, 1 })
                    Box(leaf, "Tay nắm", new Vector3(-s * (w / 2 - 0.07f), 1.05f, sd * (t / 2 + 0.018f)), new Vector3(0.025f, 0.42f, 0.02f), steel, false);
                Box(leaf, "Ốp chân cánh", new Vector3(0, 0.08f, 0), new Vector3(w, 0.16f, t + 0.004f), steel, false);
                return;
            }
            const float fr = 0.045f;
            Box(leaf, "Khung cánh trên", new Vector3(0, h - fr / 2, 0), new Vector3(w, fr, t), alu);
            Box(leaf, "Khung cánh dưới", new Vector3(0, 0.06f, 0), new Vector3(w, 0.12f, t), alu);
            Box(leaf, "Khung cánh ngoài", new Vector3(s * (w / 2 - fr / 2), h / 2, 0), new Vector3(fr, h, t), alu);
            Box(leaf, "Khung cánh giữa", new Vector3(-s * (w / 2 - fr / 2), h / 2, 0), new Vector3(fr, h, t), alu);
            var pane = Box(leaf, "Kính cánh", new Vector3(0, (h + 0.12f - fr) / 2, 0), new Vector3(w - 2 * fr, h - 0.12f - fr, 0.012f), o.frosted ? frostedGlass : windowGlass);
            if (!o.frosted) NoShadow(pane);
            else
                foreach (int sd in new[] { -1, 1 })
                    Box(leaf, "Dải phim hổ phách", new Vector3(0, 1.0f, sd * 0.0075f), new Vector3(w - 2 * fr, 0.08f, 0.002f), Mat("DaiPhimHoPhach", zoneStore, 0.5f), false);
            foreach (int sd in new[] { -1, 1 })
                Box(leaf, "Tay nắm", new Vector3(-s * (w / 2 - 0.1f), 1.05f, sd * (t / 2 + 0.02f)), new Vector3(0.022f, 0.6f, 0.022f), steel, false);
        }

        // =====================================================================
        // Lối vào phòng hóa chất
        // =====================================================================

        /// <summary>
        /// Vách kính mờ khung nhôm sơn tĩnh điện từ sàn tới trần, dải phim hổ phách; cửa kính mờ trượt tự động hai cánh;
        /// biển phát sáng song ngữ, trụ quẹt thẻ, biểu tượng cảnh báo GHS, kệ đồ bảo hộ cạnh cửa, thảm cửa viền vàng.
        /// Vách mờ tách hẳn không gian: phòng hóa chất (nặng nhất, 729 lọ) được gỡ khi không ở gần.
        /// </summary>
        static void StoreEntrance(Transform p, float x, float from, float to, float doorA, float doorB)
        {
            var g = Group("Lối vào phòng hóa chất", p);
            var amber = Mat("DaiPhimHoPhach", zoneStore, 0.5f);
            Box(g, "Ray trần", new Vector3(x, H - 0.04f, (from + to) / 2), new Vector3(0.12f, 0.08f, to - from), doorFrame);
            void Span(float a, float b)
            {
                Box(g, "Chân vách", new Vector3(x, 0.05f, (a + b) / 2), new Vector3(0.12f, 0.1f, b - a), doorFrame);
                int n = Mathf.Max(1, Mathf.RoundToInt((b - a) / 1.25f));
                for (int i = 0; i <= n; i++)
                    Box(g, "Thanh đứng", new Vector3(x, H / 2, a + (b - a) * i / n), new Vector3(0.07f, H, 0.07f), doorFrame);
                Box(g, "Kính mờ vách", new Vector3(x, H / 2 + 0.03f, (a + b) / 2), new Vector3(0.02f, H - 0.14f, b - a), frostedGlass);
                foreach (int sd in new[] { -1, 1 })
                {
                    Box(g, "Dải phim hổ phách", new Vector3(x + sd * 0.0115f, 1.05f, (a + b) / 2), new Vector3(0.002f, 0.09f, b - a), amber, false);
                    Box(g, "Dải phim mảnh", new Vector3(x + sd * 0.0115f, 0.96f, (a + b) / 2), new Vector3(0.002f, 0.018f, b - a), amber, false);
                }
            }
            Span(from, doorA);
            Span(doorB, to);
            Box(g, "Kính mờ trên cửa", new Vector3(x, (2.42f + H) / 2, (doorA + doorB) / 2), new Vector3(0.02f, H - 2.42f, doorB - doorA), frostedGlass, false);
            var o = Auto(doorA, doorB, -1, ZoneMap.Lab, ZoneMap.Store, null, glass: true, top: 2.3f);
            o.frosted = true;
            AutoDoorFill(g, false, x, o, 0.07f);

            // Biển phát sáng song ngữ trên cửa (mặt phía phòng lab)
            float zc = (doorA + doorB) / 2;
            var sign = Group("Biển phòng hóa chất", g, new Vector3(x - 0.13f, 2.88f, zc), -90f);
            Box(sign, "Nền biển", Vector3.zero, new Vector3(2.0f, 0.34f, 0.04f), accentDark, false);
            NoShadow(Box(sign, "Viền đèn", new Vector3(0, -0.175f, 0.0f), new Vector3(2.0f, 0.014f, 0.042f), Mat("LedHoPhach", zoneStore, 0.5f, 0, emission: zoneStore * 1.6f), false));
            Label(sign, "PHÒNG HÓA CHẤT", new Vector3(0, 0.05f, 0.022f), Vector3.forward, 0.085f, Color.white);
            Label(sign, "CHEMICAL STORE · KHU VỰC HẠN CHẾ", new Vector3(0, -0.085f, 0.022f), Vector3.forward, 0.036f, new Color(1f, 0.85f, 0.5f));

            // Trụ quẹt thẻ ra vào bên trái cửa (nhìn từ phòng lab)
            var reader = Group("Trụ quẹt thẻ", g, new Vector3(x - 0.42f, 0, doorA - 0.78f), -90f);
            Box(reader, "Thân trụ", new Vector3(0, 0.6f, 0), new Vector3(0.12f, 1.2f, 0.1f), doorFrame);
            Box(reader, "Mặt đầu đọc", new Vector3(0, 1.08f, 0.052f), new Vector3(0.09f, 0.14f, 0.006f), blackPlastic, false);
            NoShadow(Box(reader, "Đèn đầu đọc", new Vector3(0, 1.16f, 0.056f), new Vector3(0.05f, 0.008f, 0.004f), Mat("DenBaoXanh", new Color(0.2f, 0.9f, 0.6f), 0.5f, 0, emission: new Color(0.3f, 1.5f, 0.8f)), false));
            Info(reader, "Đầu đọc thẻ phòng hóa chất", "Mô phỏng kiểm soát ra vào: chỉ giáo viên và học sinh được giao nhiệm vụ mới vào kho.");

            // Biểu tượng cảnh báo GHS dán trên vách mờ, cạnh cửa
            string[] ghsNames = { "Dễ cháy", "Ăn mòn", "Độc cấp tính", "Cảnh báo" };
            for (int i = 0; i < ghsNames.Length; i++)
            {
                var m = Mat("GHS" + i, Color.white, 0.3f, 0, Tex("GHS" + i, 128, 128, (px, py) => GhsPixel(i, px, py), repeat: false));
                MakeCutout(m);
                NoShadow(Panel(g, "Biểu tượng GHS " + ghsNames[i], new Vector3(x - 0.0125f, 1.62f, doorA - 1.95f + i * 0.3f), Vector3.left, 0.26f, 0.26f, m));
            }
            Label(g, "Đọc nhãn · đeo kính · găng tay", new Vector3(x - 0.013f, 1.38f, doorA - 1.5f), Vector3.left, 0.03f, new Color(0.25f, 0.2f, 0.1f));

            // Kệ đồ bảo hộ treo trên vách bên phải cửa: kính, găng tay
            var ppe = Group("Kệ đồ bảo hộ cạnh cửa", g, new Vector3(x - 0.06f, 0, doorB + 1.35f));
            Box(ppe, "Tấm treo", new Vector3(0, 1.4f, 0), new Vector3(0.03f, 0.75f, 0.7f), Mat("TamTreoTrang", new Color(0.95f, 0.95f, 0.94f), 0.4f), false);
            Box(ppe, "Dải màu", new Vector3(-0.017f, 1.74f, 0), new Vector3(0.004f, 0.06f, 0.7f), amber, false);
            Label(ppe, "ĐỒ BẢO HỘ · PPE", new Vector3(-0.02f, 1.74f, 0), Vector3.left, 0.03f, new Color(0.2f, 0.15f, 0.05f));
            for (int j = 0; j < 3; j++) Goggles(ppe, new Vector3(-0.09f, 1.5f, -0.22f + j * 0.22f));
            for (int j = 0; j < 2; j++) GloveBox(ppe, new Vector3(-0.08f, 1.1f, -0.15f + j * 0.3f), j + 1);
            Box(ppe, "Khay đỡ", new Vector3(-0.08f, 1.1f, 0), new Vector3(0.16f, 0.015f, 0.68f), steel, false);
            Box(ppe, "Khay đỡ", new Vector3(-0.08f, 1.49f, 0), new Vector3(0.16f, 0.015f, 0.68f), steel, false);

            // Thảm cửa hai phía: xám đậm viền vàng
            var mat = Mat("ThamCua", new Color(0.22f, 0.23f, 0.25f), 0.1f);
            foreach (int sd in new[] { -1, 1 })
            {
                var m = Group(sd < 0 ? "Thảm cửa (phía lab)" : "Thảm cửa (phía kho)", g, new Vector3(x + sd * 0.62f, 0.004f, zc));
                Box(m, "Thảm", Vector3.zero, new Vector3(1.0f, 0.008f, 2.0f), mat, false);
                foreach (float e in new[] { -0.48f, 0.48f })
                    Box(m, "Viền vàng", new Vector3(e, 0.001f, 0), new Vector3(0.04f, 0.009f, 2.0f), safetyYellow, false);
                foreach (float e in new[] { -0.98f, 0.98f })
                    Box(m, "Viền vàng", new Vector3(0, 0.001f, e), new Vector3(1.0f, 0.009f, 0.04f), safetyYellow, false);
            }
        }

        // =====================================================================
        // Lưới đi robot nướng sẵn
        // =====================================================================

        static void BakeNavGrid(Transform root)
        {
            Physics.SyncTransforms();
            var bytes = NavGrid.Bake(out int nx, out int nz);
            var go = new GameObject("Lưới đi robot (nướng sẵn)");
            go.transform.SetParent(root, false);
            var d = go.AddComponent<NavGridData>();
            d.nx = nx;
            d.nz = nz;
            d.walk = bytes;
            Debug.Log($"[KHKT] Đã nướng lưới đi robot: {nx} × {nz} ô, {bytes.Count(b => b != 0)} ô đi được.");
        }

        // =====================================================================
        // Tách khu ra cảnh riêng
        // =====================================================================

        /// <summary>Thành phần được phép nằm ở cảnh khu (không có trạng thái cần giữ, không tham chiếu ra ngoài nhánh của nó).</summary>
        static readonly HashSet<Type> StaticSafe = new HashSet<Type>
        {
            typeof(EquipmentInfo), typeof(PlaceSurface), typeof(UvScroll), typeof(LabScreen), typeof(CeilingFan), typeof(SearchKiosk),
        };

        static bool HasState(GameObject go)
        {
            foreach (var mb in go.GetComponents<MonoBehaviour>())
                if (mb != null && !StaticSafe.Contains(mb.GetType())) return true;
            return false;
        }

        /// <summary>
        /// Chuyển phần tĩnh của các khu sang cảnh riêng. Một nhánh không chứa đồ tương tác nào thì chuyển cả nhánh vào cảnh của
        /// khu chứa nó (theo tâm khối bao); nhánh có đồ tương tác thì giữ chính nó ở lõi và xét từng nhánh con. Nhánh cha được
        /// tạo lại (cùng tên, cùng vị trí) trong cảnh khu để cấu trúc dễ đọc. Cuối cùng kéo về lõi những nhánh còn bị tham chiếu
        /// chéo giữa các cảnh (Unity không lưu được tham chiếu chéo cảnh), ví dụ đèn trần mà công tắc ở lõi bật tắt.
        /// </summary>
        static Scene[] SplitIntoZones(Scene core, Transform root)
        {
            int n = ZoneMap.Zones.Length;
            Folder("Assets", "Scenes");
            Folder("Assets/Scenes", "Khu");
            var scenes = new Scene[n];
            // Unity không cho tạo cảnh cộng dồn khi còn cảnh chưa đặt tên: lưu cảnh lõi trước, tạo cảnh khu nào lưu ngay cảnh đó
            EditorSceneManager.SaveScene(core, ScenePath);
            for (int z = 0; z < n; z++)
            {
                scenes[z] = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                EditorSceneManager.SaveScene(scenes[z], ZoneMap.ScenePath(z));
            }
            EditorSceneManager.SetActiveScene(core);

            // Gốc đồ tương tác của từng khu (ở lõi)
            var lab = root.Find("Phòng thí nghiệm");
            var store = root.Find("Phòng hóa chất");
            var wing = root.Find("Dãy phòng phía nam");
            var zoneRoots = new Transform[n];
            zoneRoots[ZoneMap.Lab] = lab;
            zoneRoots[ZoneMap.Store] = store;
            zoneRoots[ZoneMap.Corridor] = wing.Find("Hành lang");
            zoneRoots[ZoneMap.Analysis] = wing.Find("Phòng máy phân tích hiện đại");
            zoneRoots[ZoneMap.Robotics] = wing.Find("Phòng tự động hóa & robot");
            for (int z = 0; z < n; z++) zoneRoots[z].gameObject.AddComponent<ZoneRoot>().zone = z;

            // Nhánh nào có đồ tương tác (duyệt hậu thứ tự một lần)
            var stateful = new HashSet<Transform>();
            bool MarkState(Transform t)
            {
                bool any = HasState(t.gameObject);
                foreach (Transform c in t) any |= MarkState(c);
                if (any) stateful.Add(t);
                return any;
            }
            MarkState(root);

            var moved = new Dictionary<Transform, (Transform parent, int index)>();
            var mirrors = new Dictionary<(int, Transform), Transform>();

            Transform Mirror(int z, Transform original)
            {
                if (original == null) return null;
                if (mirrors.TryGetValue((z, original), out var m)) return m;
                var parent = original == root ? null : Mirror(z, original.parent);
                var go = new GameObject(original.name);
                SceneManager.MoveGameObjectToScene(go, scenes[z]);
                go.transform.SetParent(parent, false);
                go.transform.localPosition = original.localPosition;
                go.transform.localRotation = original.localRotation;
                go.transform.localScale = original.localScale;
                GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(original.gameObject));
                // thẻ thông tin của nhánh cha (kệ, bàn...) đi theo, để trỏ vào phần tĩnh vẫn hiện thông tin
                var info = original.GetComponent<EquipmentInfo>();
                if (info != null)
                {
                    var copy = go.AddComponent<EquipmentInfo>();
                    copy.title = info.title;
                    copy.description = info.description;
                    copy.usage = info.usage;
                }
                mirrors[(z, original)] = go.transform;
                return go.transform;
            }

            void MoveTo(Transform t, int z)
            {
                var parent = Mirror(z, t.parent);
                moved[t] = (t.parent, t.GetSiblingIndex());
                t.SetParent(null, true);
                SceneManager.MoveGameObjectToScene(t.gameObject, scenes[z]);
                t.SetParent(parent, true);
            }

            void Split(Transform t)
            {
                if (stateful.Contains(t))
                {
                    if (HasState(t.gameObject)) return;  // vật tương tác: giữ cả nhánh ở lõi
                    foreach (var c in t.Cast<Transform>().ToArray()) Split(c);
                    return;
                }
                int z = ZoneOf(t);
                if (z >= 0) MoveTo(t, z);
            }

            var groups = new List<Transform>(zoneRoots) { root.Find("Điểm nhấn công nghệ"), root.Find("Đầu dò phản chiếu") };
            foreach (var grp in groups)
                if (grp != null)
                    foreach (var c in grp.Cast<Transform>().ToArray()) Split(c);

            // Kéo về lõi những nhánh bị tham chiếu chéo cảnh
            int back = 0;
            for (int iter = 0; iter < 10; iter++)
            {
                var toCore = new HashSet<Transform>();
                foreach (var scene in scenes.Prepend(core))
                    foreach (var top in scene.GetRootGameObjects())
                        foreach (var comp in top.GetComponentsInChildren<Component>(true))
                        {
                            if (!(comp is MonoBehaviour) && !(comp is LODGroup)) continue;
                            var so = new SerializedObject(comp);
                            var it = so.GetIterator();
                            while (it.Next(true))
                            {
                                if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                                var obj = it.objectReferenceValue;
                                var target = obj is GameObject tg ? tg : obj is Component tc ? tc.gameObject : null;
                                if (target == null || !target.scene.IsValid() || target.scene == comp.gameObject.scene) continue;
                                var node = MovedAncestor(comp.gameObject.scene == core ? target.transform : comp.transform, moved);
                                if (node != null) toCore.Add(node);
                            }
                        }
                if (toCore.Count == 0) break;
                foreach (var t in toCore)
                {
                    var (parent, index) = moved[t];
                    t.SetParent(null, true);
                    SceneManager.MoveGameObjectToScene(t.gameObject, core);
                    t.SetParent(parent, true);
                    t.SetSiblingIndex(index);
                    moved.Remove(t);
                    back++;
                }
            }

            // Đồ cầm được đặt ở khu khác với gốc của nó (nếu có) thì chuyển sang gốc của khu chứa nó
            for (int z = 0; z < n; z++)
                foreach (var gr in zoneRoots[z].GetComponentsInChildren<Grabbable>(true))
                {
                    var up = gr.transform.parent.GetComponentInParent<Grabbable>(true);
                    if (up != null) continue;
                    int at = ZoneMap.At(gr.transform.position);
                    if (at >= 0 && at != z) gr.transform.SetParent(zoneRoots[at], true);
                }

            // Bộ tải khu ở lõi
            var zsGo = new GameObject("Tải khu (ZoneStreamer)");
            zsGo.transform.SetParent(root, false);
            var zs = zsGo.AddComponent<ZoneStreamer>();
            var viewer = viewerCamera != null ? viewerCamera.GetComponentInParent<DesktopViewer>() : null;
            zs.viewer = viewer != null ? viewer.transform : null;

            for (int z = 0; z < n; z++)
            {
                int count = 0;
                foreach (var go in scenes[z].GetRootGameObjects()) count += go.GetComponentsInChildren<Transform>(true).Length;
                Debug.Log($"[KHKT] Khu {ZoneMap.Zones[z].name}: {count} vật tĩnh ở cảnh riêng, đồ tương tác ở lõi dưới \"{zoneRoots[z].name}\".");
            }
            Debug.Log($"[KHKT] Tách khu xong: {moved.Count} nhánh chuyển sang cảnh khu, {back} nhánh kéo về lõi vì bị tham chiếu chéo.");
            return scenes;
        }

        static Transform MovedAncestor(Transform t, Dictionary<Transform, (Transform, int)> moved)
        {
            for (; t != null; t = t.parent)
                if (moved.ContainsKey(t)) return t;
            return null;
        }

        /// <summary>Khu chứa tâm khối bao (renderer và va chạm) của nhánh; không có gì để bao thì lấy vị trí của nhánh.</summary>
        static int ZoneOf(Transform t)
        {
            bool has = false;
            var b = new Bounds();
            foreach (var r in t.GetComponentsInChildren<Renderer>(true))
            {
                if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
            }
            foreach (var c in t.GetComponentsInChildren<Collider>(true))
            {
                if (!has) { b = c.bounds; has = true; } else b.Encapsulate(c.bounds);
            }
            return ZoneMap.At(has ? b.center : t.position);
        }

        // =====================================================================
        // B1. Occlusion culling (nướng chung cảnh lõi + mọi cảnh khu)
        // =====================================================================

        /// <summary>Đặt biến môi trường KHKT_NO_OCCLUSION=1 để dựng bản không nướng (đo so sánh).</summary>
        static bool OcclusionEnabled => System.Environment.GetEnvironmentVariable("KHKT_NO_OCCLUSION") != "1";

        /// <summary>
        /// Nướng occlusion culling cho cả nhóm cảnh đang mở (cảnh lõi là cảnh chính, các cảnh khu mở cộng dồn): Unity lưu một bộ
        /// dữ liệu dùng chung, cảnh khu tải cộng dồn lúc chạy dùng lại đúng bộ đó. Trước khi nướng bỏ cờ Occluder Static của vật
        /// trong suốt / cắt alpha (kính cửa sổ, kính tủ, dụng cụ thủy tinh, dung dịch): chúng không được che vật phía sau.
        /// Tường, sàn, vách kính mờ, tủ, kệ là vật che; lọ, dụng cụ (vật động) vẫn được ẩn khi nằm sau vật che.
        /// </summary>
        static void BakeOcclusion(Scene[] scenes)
        {
            int stripped = 0;
            foreach (var scene in scenes)
                foreach (var top in scene.GetRootGameObjects())
                    foreach (var r in top.GetComponentsInChildren<Renderer>(true))
                    {
                        var flags = GameObjectUtility.GetStaticEditorFlags(r.gameObject);
                        if ((flags & StaticEditorFlags.OccluderStatic) == 0) continue;
                        bool seeThrough = r.sharedMaterials.Any(m => m != null && (m.renderQueue >= 2450 || m.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT")))
                                          || r is ParticleSystemRenderer || r.GetComponent<TextMesh>() != null;
                        if (!seeThrough) continue;
                        GameObjectUtility.SetStaticEditorFlags(r.gameObject, flags & ~StaticEditorFlags.OccluderStatic);
                        stripped++;
                    }
            StaticOcclusionCulling.smallestOccluder = 0.4f;
            StaticOcclusionCulling.smallestHole = 0.12f;
            StaticOcclusionCulling.backfaceThreshold = 100f;
            var t0 = DateTime.Now;
            bool ok = StaticOcclusionCulling.Compute();
            Debug.Log($"[KHKT] Nướng occlusion culling: {(ok ? "xong" : "LỖI")} sau {(DateTime.Now - t0).TotalSeconds:0} s, " +
                      $"dữ liệu {StaticOcclusionCulling.umbraDataSize / 1024} KB, bỏ cờ vật che ở {stripped} vật trong suốt.");
        }

        // =====================================================================
        // B2. HLOD cho cụm vật cầm được (kệ hóa chất, tủ dụng cụ thủy tinh)
        // =====================================================================

        /// <summary>
        /// Tắt mặc định: đo trên laptop Iris Xe (3 vòng xen kẽ) không nhanh hơn (62,4 → 62,4 FPS) vì máy nghẽn ở GPU chứ không ở số
        /// lệnh vẽ của các lọ, và khoảng nhìn trong phòng ngắn. Đặt KHKT_HLOD=1 để dựng bản có HLOD.
        /// </summary>
        static bool HlodEnabled => System.Environment.GetEnvironmentVariable("KHKT_HLOD") == "1";

        /// <summary>
        /// Mỗi kệ hóa chất / tủ dụng cụ một vật con "Gộp HLOD" mang ClusterHlod: ở xa thay ~30 lọ bằng một lưới gộp theo vật liệu.
        /// Đặt ở vật con (không phải nhóm kệ) để khung kệ tĩnh vẫn tách sang cảnh khu được.
        /// </summary>
        static void AttachHlod(Transform root)
        {
            if (!HlodEnabled) return;
            int layer = LayerMask.NameToLayer(LayerDetail);
            int n = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true).ToArray())
            {
                if (!(t.name.StartsWith("Kệ K") || t.name.StartsWith("Tủ dụng cụ thủy tinh"))) continue;
                if (t.GetComponentsInChildren<Grabbable>(true).Length < 6) continue;
                var go = new GameObject("Gộp HLOD");
                go.layer = layer < 0 ? 0 : layer;
                go.transform.SetParent(t, false);
                go.AddComponent<ClusterHlod>().cluster = t;
                n++;
            }
            Debug.Log($"[KHKT] HLOD: {n} cụm (kệ hóa chất, tủ dụng cụ thủy tinh).");
        }

        // =====================================================================
        // B3. Impostor thời gian thực cho cây ngoài sân
        // =====================================================================

        /// <summary>
        /// Tắt mặc định: đo không nhanh hơn trong sai số (62,4 → 62,7 FPS); khi có cửa sổ ảo (B4) cây ngoài sân đã tắt hẳn lúc ở
        /// trong nhà nên impostor chỉ còn tác dụng ngoài hiên. Đặt KHKT_IMPOSTOR=1 để dựng bản có impostor.
        /// </summary>
        static bool ImpostorEnabled => System.Environment.GetEnvironmentVariable("KHKT_IMPOSTOR") == "1";

        /// <summary>Vật liệu tấm impostor: URP Unlit cắt alpha, lưu thành asset để biến thể shader có trong bản build.</summary>
        static Material ImpostorMaterial()
        {
            string path = $"{Root}/Materials/Impostor.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Surface", 0f);
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", 0.5f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetOverrideTag("RenderType", "TransparentCutout");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            m.SetFloat("_Cull", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void AttachImpostors(Transform root)
        {
            if (!ImpostorEnabled) return;
            var outdoor = root.Find("Ngoài trời");
            if (outdoor == null) return;
            var mat = ImpostorMaterial();
            int n = 0;
            foreach (Transform t in outdoor)
            {
                if (!t.name.StartsWith("Cây ")) continue;
                var ib = t.gameObject.AddComponent<ImpostorBillboard>();
                ib.billboardMaterial = mat;
                ib.switchDistance = 12f;
                n++;
            }
            Debug.Log($"[KHKT] Impostor: {n} cây ngoài sân.");
        }

        // =====================================================================
        // B4. Cửa sổ ảo: cảnh ngoài chụp sẵn thay cho vẽ thật hiên, sân, cây
        // =====================================================================

        /// <summary>Đặt biến môi trường KHKT_NO_VWINDOW=1 để dựng bản không có cửa sổ ảo (đo so sánh).</summary>
        static bool VirtualWindowsEnabled => System.Environment.GetEnvironmentVariable("KHKT_NO_VWINDOW") != "1";

        static Transform virtualWindowRoot;
        static Material vwNorth, vwSouth;
        static readonly Vector3 NorthCapture = new Vector3(13.5f, 1.5f, 15.9f), SouthCapture = new Vector3(13.5f, 1.5f, WingZ - 0.6f);

        /// <summary>Nhóm chứa các tấm cửa sổ ảo (tạo trước khi dựng vỏ tòa nhà; tắt sẵn, VirtualWindowSet bật lúc chạy).</summary>
        static void BeginVirtualWindows(Transform root)
        {
            virtualWindowRoot = null;
            if (!VirtualWindowsEnabled) return;
            virtualWindowRoot = Group("Cửa sổ ảo", root);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>($"{Root}/Shaders/CuaSoAo.shader");
            if (shader == null) { Debug.LogWarning("[KHKT] Thiếu shader CuaSoAo: bỏ qua cửa sổ ảo."); virtualWindowRoot = null; return; }
            Material Make(string name, Vector3 cap, Vector3 boxMin, Vector3 boxMax)
            {
                string path = $"{Root}/Materials/{name}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
                m.shader = shader;
                m.SetVector("_CapturePos", cap);
                m.SetVector("_BoxMin", boxMin);
                m.SetVector("_BoxMax", boxMax);
                EditorUtility.SetDirty(m);
                return m;
            }
            vwNorth = Make("CuaSoAoBac", NorthCapture, new Vector3(-12f, -0.25f, 15.05f), new Vector3(40f, 14f, 48f));
            vwSouth = Make("CuaSoAoNam", SouthCapture, new Vector3(-25f, -0.25f, -70f), new Vector3(52f, 20f, WingZ - 0.05f));
        }

        /// <summary>Tấm cửa sổ ảo ở mặt ngoài một ô cửa (center: tâm tấm, outward: hướng ra ngoài trời).</summary>
        static void VirtualWindowQuad(Vector3 center, Vector3 outward, float w, float h)
        {
            if (virtualWindowRoot == null) return;
            var q = Panel(virtualWindowRoot, "Cửa sổ ảo", center, -outward, w, h, outward.z > 0 ? vwNorth : vwSouth);
            NoShadow(q);
            q.GetComponent<Renderer>().receiveShadows = false;
        }

        /// <summary>
        /// Chụp cảnh ngoài thành hai ảnh toàn cảnh (đầu dò phản chiếu nướng, HDR 512): phía bắc từ giữa hiên, phía nam từ ngoài
        /// tường nam. Lúc chụp tắt mọi thứ trong nhà, chỉ giữ vỏ tòa nhà, hiên, sân, ánh sáng.
        /// </summary>
        static void BakeOutdoorViews(Transform root)
        {
            if (virtualWindowRoot == null) return;
            var keep = new HashSet<string> { "Vỏ tòa nhà", "Hiên", "Ngoài trời", "Ánh sáng" };
            var hidden = new List<GameObject>();
            foreach (Transform t in root)
                if (!keep.Contains(t.name) && t.gameObject.activeSelf) { t.gameObject.SetActive(false); hidden.Add(t.gameObject); }
            vwNorth.SetTexture("_Cube", BakeView("CanhNgoaiBac", NorthCapture));
            vwSouth.SetTexture("_Cube", BakeView("CanhNgoaiNam", SouthCapture));
            foreach (var go in hidden) go.SetActive(true);
            virtualWindowRoot.gameObject.SetActive(false);  // lúc chạy VirtualWindowSet bật khi người xem ở trong nhà

            var set = new GameObject("Bật tắt cửa sổ ảo").AddComponent<VirtualWindowSet>();
            set.transform.SetParent(root, false);
            var viewer = viewerCamera != null ? viewerCamera.GetComponentInParent<DesktopViewer>() : null;
            set.viewer = viewer != null ? viewer.transform : null;
            set.windows = virtualWindowRoot.gameObject;
            set.outdoorRoots = new[] { root.Find("Ngoài trời")?.gameObject, root.Find("Hiên")?.gameObject };
            set.balconyDoor = new Vector3(1.8f, 0, LabZ);
            Debug.Log($"[KHKT] Cửa sổ ảo: {virtualWindowRoot.childCount} tấm, đã chụp cảnh ngoài phía bắc và phía nam.");
        }

        static Texture BakeView(string name, Vector3 pos)
        {
            var go = new GameObject("Chụp " + name);
            go.transform.position = pos;
            var probe = go.AddComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Baked;
            probe.resolution = 512;
            probe.hdr = true;
            probe.size = Vector3.one * 300f;
            probe.nearClipPlane = 0.05f;
            probe.farClipPlane = 400f;
            string path = $"{Root}/Probes/{name}.exr";
            Folder(Root, "Probes");
            UnityEditor.Lightmapping.BakeReflectionProbe(probe, path);
            AssetDatabase.ImportAsset(path);
            UnityEngine.Object.DestroyImmediate(go);
            return AssetDatabase.LoadAssetAtPath<Texture>(path);
        }

        [MenuItem("KHKT/Mở cảnh lõi + tất cả các khu")]
        static void OpenAllZones()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            for (int z = 0; z < ZoneMap.Zones.Length; z++)
                if (File.Exists(ZoneMap.ScenePath(z))) EditorSceneManager.OpenScene(ZoneMap.ScenePath(z), OpenSceneMode.Additive);
        }
    }
}
