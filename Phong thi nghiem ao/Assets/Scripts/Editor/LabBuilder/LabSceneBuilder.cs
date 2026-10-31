using System;
using Khkt.Environment;
using Khkt.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static Khkt.EditorTools.LabKit;

namespace Khkt.EditorTools
{
    /// <summary>
    /// Dựng toàn bộ cảnh phòng thí nghiệm bằng code: menu KHKT > Dựng phòng thí nghiệm.
    ///
    /// Mặt bằng (mét). Gốc tọa độ ở góc tây nam phòng thí nghiệm, trục x về phía đông, z về phía bắc,
    /// sàn tầng 2 ở y = 0, trần ở y = 3,6:
    ///   Phòng thí nghiệm  x 0..20,  z 0..15  (300 m²) bảng ở tường tây, học sinh nhìn về phía tây
    ///     khu giáo viên x 0..3; 9 bàn thí nghiệm (3 hàng × 3 dãy); khu thiết bị phân tích (tường nam, phía đông);
    ///     bàn đun nóng - tổng hợp giữa phía đông; 3 tủ hút (tường bắc, phía đông); bồn rửa và tủ dụng cụ
    ///     thủy tinh (tường bắc); khu an toàn cạnh cửa ra vào (góc tây nam)
    ///   Phòng hóa chất    x 20..27, z 0..15  ngăn với phòng thí nghiệm bằng vách kính, 27 kệ theo nhóm
    ///   Ban công          x 0..27,  z 15..16,8
    /// Mọi thứ là khối cơ bản và khối tròn xoay, không cần mô hình ngoài, nên chạy được cả trên điện thoại.
    /// </summary>
    public static partial class LabSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/PhongThiNghiem.unity";
        const float H = 3.6f;  // chiều cao trần
        const float T = 0.2f;  // độ dày tường
        const float LabX = 20f, LabZ = 15f, StoreX = 27f, BalconyZ = 16.8f;

        static Material paint, facade, ceilingMat, floorLab, floorChem, floorBalcony, sill;
        static Material epoxy, cabinet, cabinetDoor, wood, steel, darkMetal, alu, rubber;
        static Material windowGlass, cabinetGlass, labGlass, liquidClear, liquidBlue, liquidPink, amber, whitePlastic, blackPlastic,
                        redPlastic, bluePlastic, stoolSeat, whiteboard, tubeGlow, lampHousing, fanWhite;
        static Material red, safetyGreen, safetyYellow, acidBlue, signWhite, pot, foliage, foliageDark, grass,
                        concrete, trunk, pegboard, poster, tileWhite, coat, flag, darkGlass, screen, sand, accent, accentDark, oak, panelGrey;
        // Phong cách công nghệ cao: đèn LED xanh, tay nắm xanh, mặt bàn tủ hút tối, chậu trắng, khung ảnh, tường nhấn, mặt màn hình
        static Material ledBlue, handleBlue, epoxyDark, potWhite, frameMat, accentWall, screenContent, floorGuide;
        // Phong cách lab trường quốc tế: kính mờ giữa các khu, cửa tự động màu theo khu, dải sọc màu, tấm trần gỗ, bảng ghim
        static Material floorHall, frostedGlass, filmBand, leafLab, leafStore, leafAnalysis, leafRobot, doorFrame, stripeMat, raftMat, corkMat;
        static Material[] stoolMats;
        static Color zoneLab, zoneStore, zoneAnalysis, zoneRobot;
        static Texture2D screenBg, portraitTex;

        static System.Random rnd;
        static Light sunLight;

        [MenuItem("KHKT/Dựng phòng thí nghiệm")]
        static void BuildMenu()
        {
            if (!EditorUtility.DisplayDialog("Dựng phòng thí nghiệm",
                    $"Tạo (hoặc tạo lại) cảnh {ScenePath}.\nNếu cảnh này đã có, mọi chỉnh sửa tay trong đó sẽ bị ghi đè.",
                    "Dựng", "Hủy")) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        /// <summary>Dựng và lưu cảnh. Gọi được từ dòng lệnh: -executeMethod Khkt.EditorTools.LabSceneBuilder.Build</summary>
        public static void Build()
        {
            Begin();
            rnd = new System.Random(7);
            CreateMaterials();
            LoadCatalogs();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            foreach (var cam in UnityEngine.Object.FindObjectsByType<Camera>())
                UnityEngine.Object.DestroyImmediate(cam.gameObject);
            sunLight = UnityEngine.Object.FindAnyObjectByType<Light>();

            var root = new GameObject("Phòng thí nghiệm ảo").transform;
            var textFix = root.gameObject.AddComponent<WorldTextMaterial>();
            textFix.textMaterial = TextMaterial;
            textFix.font = LabelFont;
            BeginVirtualWindows(root);
            BuildShell(root);
            BuildMainLab(root);
            BuildChemStore(root);
            BuildSouthWing(root);
            WingAccents(root);
            BuildBalcony(root);
            BuildOutdoor(root);
            BuildFloor2(root);
            BuildLighting(root);
            BuildStationsAndViewer(root);
            AttachEquipmentInfo(root);
            AttachSurfaces(root);
            AttachUsables(root);
            BuildDemo(root);
            OptimizeRenderers(root);
            AttachHlod(root);
            AttachImpostors(root);

            // Dụng cụ cầm được, có thẻ thông tin hay có hiệu ứng không gộp lô tĩnh (để di chuyển và viền sáng được)
            MarkStatic(root, t => t.name == WorkshopName || t.name == PartLibraryName || t.name.StartsWith("Cánh quạt") || t.name == "Bộ chuẩn độ" || t.name == "Ngọn lửa" || t.name == "Hệ thống demo"
                                  || t.GetComponent<Khkt.Demo.EquipmentInfo>() != null || t.GetComponent<Khkt.Interaction.Grabbable>() != null
                                  || t.GetComponent<Khkt.Interaction.LabContainer>() != null || t.name == "Trạm" || t.name.StartsWith("Người xem")
                                  || t.name.StartsWith("Màn hình") || t.name == "Robot trợ lý" || t.GetComponent<Khkt.Demo.LabScreen>() != null
                                  || t.GetComponent<UvScroll>() != null || t.GetComponent<Khkt.Rendering.AutoDoor>() != null
                                  || t.GetComponent<Khkt.Rendering.ClusterHlod>() != null || IsFloor2Dynamic(t));
            BakeReflectionProbes(root);
            BakeOutdoorViews(root);  // B4: chụp cảnh ngoài cho cửa sổ ảo
            BakeNavGrid(root);  // khi mọi khu còn đủ mặt: lúc chạy các khu ở xa đã gỡ, không tính lại được
            DeactivateTemplates(root);  // bản mẫu khu chế tạo: tắt sẵn, lúc chạy nhân bản ra

            // Tách phần tĩnh của từng khu ra cảnh riêng (tải / gỡ cộng dồn lúc chạy), đồ tương tác ở lại cảnh lõi
            var zoneScenes = SplitIntoZones(scene, root);
            EditorSceneManager.SetActiveScene(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            int total = root.GetComponentsInChildren<Transform>(true).Length, zoneObjects = 0;
            for (int z = 0; z < zoneScenes.Length; z++)
            {
                EditorSceneManager.SaveScene(zoneScenes[z], Khkt.Rendering.ZoneMap.ScenePath(z));
                foreach (var go in zoneScenes[z].GetRootGameObjects()) zoneObjects += go.GetComponentsInChildren<Transform>(true).Length;
            }
            if (OcclusionEnabled)
            {
                BakeOcclusion(System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Prepend(zoneScenes, scene)));
                EditorSceneManager.SaveOpenScenes();
            }
            AddToBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log($"[KHKT] Đã dựng {ScenePath}: {total + zoneObjects} đối tượng ({total} ở cảnh lõi, {zoneObjects} ở {zoneScenes.Length} cảnh khu).");
        }

        static void AddToBuildSettings()
        {
            var paths = new System.Collections.Generic.List<string> { ScenePath };
            for (int z = 0; z < Khkt.Rendering.ZoneMap.Zones.Length; z++) paths.Add(Khkt.Rendering.ZoneMap.ScenePath(z));
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            list.RemoveAll(s => paths.Contains(s.path));
            for (int i = 0; i < paths.Count; i++) list.Insert(i, new EditorBuildSettingsScene(paths[i], true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        // =====================================================================
        // Vật liệu và ảnh vân
        // =====================================================================

        static void CreateMaterials()
        {
            // Gạch lát 60 x 60 cm màu xám sáng, ron mảnh
            var tiles = Tex("GachLatSan60", 256, 256, (x, y) =>
            {
                int gx = x % 128, gy = y % 128;
                if (gx < 2 || gy < 2) return new Color(0.7f, 0.71f, 0.72f);
                int cell = (x / 128) + 2 * (y / 128);
                float tint = 0.93f + 0.012f * cell + 0.015f * Mathf.PerlinNoise(x * 0.04f, y * 0.04f);
                return new Color(tint, tint, tint * 1.005f);
            });
            var grassTex = Tex("Co", 128, 128, (x, y) =>
            {
                float n = Mathf.PerlinNoise(x * 0.12f, y * 0.12f) * 0.6f + Mathf.PerlinNoise(x * 0.5f, y * 0.5f) * 0.4f;
                return new Color(0.75f + 0.25f * n, 0.85f + 0.15f * n, 0.7f + 0.2f * n);
            });
            var concreteTex = Tex("BeTong", 128, 128, (x, y) =>
            {
                float n = 0.85f + 0.15f * Mathf.PerlinNoise(x * 0.3f, y * 0.3f);
                return new Color(n, n, n);
            });
            var pegTex = Tex("BangLo", 32, 32, (x, y) =>
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f));
                return d < 4.5f ? new Color(0.25f, 0.24f, 0.22f) : Color.white;
            });
            var periodicTex = Tex("BangTuanHoan", 456, 288, PeriodicTablePixel, repeat: false);
            var flagTex = Tex("CoVietNam", 300, 200, FlagPixel, repeat: false);

            // Phong cách lab trường quốc tế: tường trắng ấm (không trắng xóa), gỗ sồi sáng, mặt bàn Trespa xám nhạt,
            // mỗi khu một màu nhận diện (lab xanh ngọc, kho hổ phách, phòng máy xanh cốm, phòng robot cam san hô), ghế nhiều màu.
            // Ảnh vân đều có mipmap, tần số thấp, độ tương phản nhẹ: nhìn xa không nhấp nháy, không lấm tấm hạt.
            paint = Mat("TuongSon", new Color(0.93f, 0.92f, 0.89f), 0.15f);
            facade = Mat("TuongNgoai", new Color(0.92f, 0.9f, 0.86f), 0.12f);
            // trần phát sáng rất nhẹ thay cho ánh sáng hắt ngược từ sàn (không có GI)
            ceilingMat = Mat("Tran", new Color(0.96f, 0.96f, 0.95f), 0.05f, 0, emission: new Color(0.4f, 0.4f, 0.39f));
            floorLab = Mat("SanPhongLab", new Color(0.8f, 0.79f, 0.76f), 0.32f, 0, tiles, new Vector2(16.67f, 12.5f));
            floorChem = Mat("SanPhongHoaChat", new Color(0.72f, 0.74f, 0.76f), 0.35f, 0, tiles, new Vector2(5.83f, 12.5f));
            floorHall = Mat("SanHanhLang", new Color(0.76f, 0.74f, 0.7f), 0.3f, 0, tiles, new Vector2(22.5f, 1.83f));
            floorBalcony = Mat("SanBanCong", new Color(0.78f, 0.72f, 0.66f), 0.25f, 0, tiles, new Vector2(22.5f, 1.5f));
            sill = Mat("BauCua", new Color(0.86f, 0.86f, 0.85f), 0.4f, 0.3f);
            epoxy = Mat("MatBanPhenolic", new Color(0.8f, 0.81f, 0.81f), 0.42f);
            epoxyDark = Mat("MatBanEpoxy", new Color(0.13f, 0.14f, 0.16f), 0.65f);
            cabinet = Mat("ThanTu", new Color(0.95f, 0.95f, 0.93f), 0.35f);
            cabinetDoor = Mat("CanhTu", new Color(1f, 1f, 1f), 0.3f, 0, Tex("VanGoSoi", 256, 256, OakPixel), new Vector2(1f, 1f));
            // màu nhận diện từng khu
            zoneLab = new Color(0.0f, 0.56f, 0.6f);
            zoneStore = new Color(0.97f, 0.7f, 0.12f);
            zoneAnalysis = new Color(0.47f, 0.72f, 0.2f);
            zoneRobot = new Color(0.95f, 0.43f, 0.26f);
            // cánh cửa tông nhạt của màu khu: nổi lên trên mảng màu đậm quanh cửa ở hành lang
            leafLab = Mat("CanhCuaLab", Color.Lerp(zoneLab, Color.white, 0.5f), 0.45f);
            leafStore = Mat("CanhCuaKho", Color.Lerp(zoneStore, Color.white, 0.5f), 0.45f);
            leafAnalysis = Mat("CanhCuaPhongMay", Color.Lerp(zoneAnalysis, Color.white, 0.5f), 0.45f);
            leafRobot = Mat("CanhCuaPhongRobot", Color.Lerp(zoneRobot, Color.white, 0.5f), 0.45f);
            doorFrame = Mat("KhungCuaTuDong", new Color(0.2f, 0.21f, 0.23f), 0.5f, 0.6f);
            // Kính mờ giữa các khu: vẽ như vật đục (không trong suốt, không tốn lượt vẽ kính), phát sáng rất nhẹ như có đèn phía sau
            frostedGlass = Mat("KinhMoNoiThat", new Color(0.8f, 0.86f, 0.88f), 0.85f, 0, emission: new Color(0.07f, 0.08f, 0.085f));
            filmBand = Mat("DaiPhimMau", zoneLab, 0.5f);
            stoolMats = new[]
            {
                Mat("MatGheXanhNgoc", zoneLab, 0.45f), Mat("MatGheCam", zoneRobot, 0.45f), Mat("MatGheXanhCom", zoneAnalysis, 0.45f),
                Mat("MatGheVang", zoneStore, 0.45f), Mat("MatGheXanhTroi", new Color(0.25f, 0.55f, 0.88f), 0.45f),
            };
            wood = Mat("Go", new Color(0.62f, 0.45f, 0.3f), 0.35f);
            oak = Mat("GoSoi", new Color(0.78f, 0.64f, 0.47f), 0.35f);
            steel = Mat("Inox", new Color(0.78f, 0.79f, 0.8f), 0.75f, 0.9f);
            darkMetal = Mat("KimLoaiToi", new Color(0.2f, 0.21f, 0.23f), 0.45f, 0.6f);
            alu = Mat("NhomKhung", new Color(0.84f, 0.85f, 0.87f), 0.55f, 0.7f);
            rubber = Mat("CaoSu", new Color(0.05f, 0.05f, 0.05f), 0.2f);
            // kính trong hơn: kính vẽ hai mặt nên độ đục cộng đôi; đục quá thì phòng bên kia trông như có sương mù
            windowGlass = Mat("KinhCuaSo", new Color(0.55f, 0.7f, 0.8f, 0.06f), 0.95f, 0, transparent: true);
            cabinetGlass = Mat("KinhTu", new Color(0.7f, 0.8f, 0.85f, 0.06f), 0.7f, 0, transparent: true);
            // Kính vách / cửa sổ / tủ: tắt phản chiếu môi trường. Có đầu dò phản chiếu, mặt kính in cả căn phòng trắng lên
            // thành một lớp mờ như sương, nhìn sang phòng bên kia không rõ. Vẫn giữ điểm sáng của đèn trên kính.
            foreach (var m in new[] { windowGlass, cabinetGlass })
            {
                m.SetFloat("_EnvironmentReflections", 0f);
                m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                EditorUtility.SetDirty(m);
            }
            labGlass = Mat("ThuyTinh", new Color(0.85f, 0.95f, 1f, 0.22f), 0.95f, 0, transparent: true);
            liquidClear = Mat("DungDichTrong", new Color(0.8f, 0.9f, 1f, 0.35f), 0.9f, 0, transparent: true);
            liquidBlue = Mat("DungDichXanh", new Color(0.1f, 0.4f, 0.95f, 0.6f), 0.9f, 0, transparent: true);
            liquidPink = Mat("DungDichHong", new Color(0.95f, 0.35f, 0.65f, 0.5f), 0.9f, 0, transparent: true);
            amber = Mat("ChaiNau", new Color(0.42f, 0.2f, 0.05f), 0.8f);
            whitePlastic = Mat("NhuaTrang", new Color(0.95f, 0.95f, 0.94f), 0.35f);
            blackPlastic = Mat("NhuaDen", new Color(0.08f, 0.08f, 0.09f), 0.4f);
            redPlastic = Mat("NhuaDo", new Color(0.8f, 0.12f, 0.1f), 0.4f);
            bluePlastic = Mat("NhuaXanh", new Color(0.15f, 0.35f, 0.75f), 0.4f);
            stoolSeat = stoolMats[0];
            whiteboard = Mat("BangTrang", new Color(0.97f, 0.97f, 0.97f), 0.8f);
            tubeGlow = Mat("BongDen", new Color(1f, 1f, 1f), 0.5f, 0, emission: new Color(3.2f, 3.25f, 3.4f));
            lampHousing = Mat("VoDen", new Color(0.93f, 0.93f, 0.92f), 0.4f, 0.2f);
            fanWhite = Mat("QuatTrang", new Color(0.92f, 0.92f, 0.9f), 0.45f, 0.1f);
            red = Mat("DoAnToan", new Color(0.8f, 0.07f, 0.05f), 0.55f);
            safetyGreen = Mat("XanhAnToan", new Color(0.05f, 0.55f, 0.25f), 0.4f);
            safetyYellow = Mat("VangCanhBao", new Color(0.95f, 0.78f, 0.08f), 0.4f);
            acidBlue = Mat("TuAxit", new Color(0.25f, 0.42f, 0.66f), 0.4f, 0.3f);
            signWhite = Mat("BienTrang", new Color(0.97f, 0.97f, 0.95f), 0.3f);
            pot = Mat("ChauDat", new Color(0.62f, 0.33f, 0.2f), 0.2f);
            foliage = Mat("La", new Color(0.2f, 0.5f, 0.18f), 0.25f);
            foliageDark = Mat("LaDam", new Color(0.13f, 0.38f, 0.14f), 0.2f);
            grass = Mat("NenCo", new Color(0.36f, 0.56f, 0.25f), 0.1f, 0, grassTex, new Vector2(60, 60));
            concrete = Mat("SanBeTong", new Color(0.72f, 0.72f, 0.7f), 0.15f, 0, concreteTex, new Vector2(12, 8));
            trunk = Mat("ThanCay", new Color(0.35f, 0.25f, 0.16f), 0.15f);
            pegboard = Mat("BangGiaTreo", new Color(0.94f, 0.94f, 0.92f), 0.2f, 0, pegTex, new Vector2(36, 16));
            poster = Mat("BangTuanHoan", Color.white, 0.2f, 0, periodicTex);
            tileWhite = Mat("GachMen", new Color(0.97f, 0.97f, 0.97f), 0.7f);
            coat = Mat("AoBlouse", new Color(0.96f, 0.97f, 0.98f), 0.15f);
            flag = Mat("Co", Color.white, 0.2f, 0, flagTex);
            darkGlass = Mat("KinhToi", new Color(0.2f, 0.3f, 0.38f), 0.85f, 0.2f);
            screen = Mat("ManHinh", new Color(0.08f, 0.16f, 0.28f), 0.8f, 0, emission: new Color(0.12f, 0.28f, 0.48f));
            sand = Mat("Cat", new Color(0.85f, 0.75f, 0.55f), 0.05f);
            accent = Mat("MauNhan", zoneLab, 0.45f);
            accentDark = Mat("MauNhanDam", new Color(0.05f, 0.22f, 0.3f), 0.45f);
            panelGrey = Mat("TamOpXam", new Color(0.86f, 0.86f, 0.84f), 0.35f);
            // đèn dải: trắng ngà hơi ngả xanh ngọc, dịu (không còn xanh neon)
            ledBlue = Mat("LedXanh", new Color(0.6f, 0.9f, 0.9f), 0.5f, 0, emission: new Color(0.45f, 1.05f, 1.0f));
            handleBlue = Mat("TayNamXanh", new Color(0.12f, 0.12f, 0.13f), 0.55f, 0.6f);
            potWhite = Mat("ChauTrang", new Color(0.96f, 0.96f, 0.95f), 0.6f);
            frameMat = Mat("KhungAnh", new Color(0.18f, 0.15f, 0.12f), 0.5f, 0.2f);
            floorGuide = Mat("VachDanHuong", new Color(0.45f, 0.5f, 0.54f), 0.35f, 0, Tex("SanLoiDi", 128, 128, WalkwayPixel), new Vector2(1f, 1f));
            // mảng tường nhấn sau tường video: ốp lam gỗ sồi (tiêu âm), thay mảng mạch điện xanh
            accentWall = Mat("OpLamGo", Color.white, 0.3f, 0, Tex("LamGoSoi", 256, 256, SlatPixel), new Vector2(13f, 1f));
            stripeMat = Mat("DaiSocMau", Color.white, 0.25f, 0, Tex("SocMau", 32, 128, StripePixel), new Vector2(1f, 1f));
            // tấm trần gỗ: mặt dưới chỉ nhận ánh sáng môi trường (không có GI) nên tự phát sáng nhẹ như gỗ được đèn hắt lên
            raftMat = EmissiveTexMat("TamTranGo", Tex("VanGoTran", 256, 256, OakPixel), new Color(0.5f, 0.44f, 0.36f), new Vector2(6f, 1f));
            corkMat = Mat("BangGhimBan", Color.white, 0.1f, 0, Tex("BanGhim", 128, 128, CorkPixel), new Vector2(6f, 1.2f));
            screenBg = Tex("NenManHinh", 320, 180, ScreenBackgroundPixel, repeat: false);
            screenContent = EmissiveTexMat("NoiDungManHinh", screenBg, new Color(0.95f, 0.95f, 0.95f), Vector2.one);
            portraitTex = Tex("ChanDungMau", 160, 200, PortraitPixel, repeat: false);
            socketRing = Mat("VongDat", new Color(0.4f, 0.9f, 1f, 0.5f), 0.3f, 0, emission: new Color(0.25f, 0.8f, 1.1f), transparent: true);
            translucentPlastic = Mat("NhuaMo", new Color(0.95f, 0.96f, 0.97f, 0.42f), 0.6f, 0, transparent: true);
            labelPaper = Mat("NhanGiay", new Color(0.98f, 0.98f, 0.96f), 0.15f);
            fumeZones.Clear();
        }

        /// <summary>Bảng tuần hoàn rút gọn: 18 cột, 7 chu kì + 2 hàng họ Lantan/Actini, tô màu theo khối s, p, d, f.</summary>
        static Color PeriodicTablePixel(int x, int y)
        {
            const int cell = 24, left = 12, topMargin = 36;
            int h = 288;
            int fromTop = h - 1 - y;
            if (fromTop < 26) return new Color(0.1f, 0.2f, 0.45f);  // dải tiêu đề
            int col = (x - left) / cell, row = (fromTop - topMargin) / cell;
            int cx = (x - left) % cell, cy = (fromTop - topMargin) % cell;
            if (x < left || fromTop < topMargin || col > 17 || row > 9 || cx < 2 || cy < 2) return Color.white;
            int period = row + 1, group = col + 1;
            Color c;
            if (row <= 6)
            {
                bool exists = period == 1 ? (group == 1 || group == 18)
                            : period <= 3 ? (group <= 2 || group >= 13)
                            : true;
                if (!exists) return Color.white;
                if (period == 1 && group == 1) c = new Color(0.85f, 0.85f, 0.9f);
                else if (group <= 2) c = new Color(0.98f, 0.62f, 0.55f);
                else if (group <= 12) c = new Color(0.6f, 0.78f, 0.95f);
                else if (group == 18) c = new Color(0.78f, 0.65f, 0.92f);
                else c = new Color(0.98f, 0.9f, 0.5f);
            }
            else if (row == 7) return Color.white;  // hàng trống ngăn cách
            else
            {
                if (group < 3 || group > 17) return Color.white;
                c = new Color(0.62f, 0.88f, 0.62f);
            }
            if (cx < 9 && cy < 8 && cx > 3 && cy > 3) c *= 0.8f;  // ô nhỏ gợi số hiệu nguyên tử
            c.a = 1;
            return c;
        }

        /// <summary>Cờ đỏ sao vàng: sao năm cánh, đường kính đường tròn ngoại tiếp bằng 2/5 chiều dài cờ.</summary>
        static Color FlagPixel(int x, int y)
        {
            Vector2 c = new Vector2(150, 100);
            float R = 60, r = R * 0.381966f;
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI / 2 + i * Mathf.PI / 5;
                float rad = i % 2 == 0 ? R : r;
                pts[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rad;
            }
            bool inside = false;
            var p = new Vector2(x + 0.5f, y + 0.5f);
            for (int i = 0, j = 9; i < 10; j = i++)
                if ((pts[i].y > p.y) != (pts[j].y > p.y) &&
                    p.x < (pts[j].x - pts[i].x) * (p.y - pts[i].y) / (pts[j].y - pts[i].y) + pts[i].x)
                    inside = !inside;
            return inside ? new Color(1f, 0.87f, 0f) : new Color(0.85f, 0.08f, 0.1f);
        }

        /// <summary>Vật liệu có ảnh vừa làm ảnh nền vừa làm ảnh phát sáng (tường mạch điện, mặt màn hình).</summary>
        static Material EmissiveTexMat(string name, Texture2D tex, Color emission, Vector2 tiling)
        {
            var m = Mat(name, Color.white, 0.35f, 0, tex, tiling, emission);
            m.SetTexture("_EmissionMap", tex);
            EditorUtility.SetDirty(m);
            return m;
        }

        static uint Hash(int x, int y)
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return h ^ (h >> 16);
        }

        /// <summary>Mảng tường nhấn: nền xanh dương đậm chuyển sắc, đường mạch điện, chân hàn và vòng lục giác phân tử (lặp liền mạch).</summary>
        static Color CircuitPixel(int x, int y)
        {
            const int W = 512, Hh = 256, cell = 32;
            float t = (float)y / Hh;
            var c = Color.Lerp(new Color(0.03f, 0.1f, 0.28f), new Color(0.05f, 0.22f, 0.55f), t);
            int cx = x / cell, cy = y / cell, lx = x % cell, ly = y % cell;
            uint h = Hash(cx % (W / cell), cy % (Hh / cell));
            var line = new Color(0.35f, 0.68f, 1f);
            bool horiz = (h & 3) == 0 && Mathf.Abs(ly - 16) <= 1;
            bool vert = ((h >> 2) & 3) == 0 && Mathf.Abs(lx - 16) <= 1;
            if (horiz || vert) c = Color.Lerp(c, line, 0.75f);
            float pad = Vector2.Distance(new Vector2(lx, ly), new Vector2(16, 16));
            if (((h >> 4) & 7) == 0 && pad < 3.5f) c = Color.Lerp(c, new Color(0.6f, 0.85f, 1f), 0.9f);
            if (((h >> 7) % 9) == 0)
            {
                // vòng lục giác (gợi phân tử benzen)
                float ax = Mathf.Abs(lx - 16), ay = Mathf.Abs(ly - 16);
                float hex = Mathf.Max(ax * 0.866f + ay * 0.5f, ay);
                if (Mathf.Abs(hex - 10f) < 0.9f) c = Color.Lerp(c, line, 0.7f);
            }
            c.a = 1;
            return c;
        }

        /// <summary>Nền màn hình: xanh dương đậm chuyển sắc, lưới mờ, dải tiêu đề xanh sáng ở trên.</summary>
        static Color ScreenBackgroundPixel(int x, int y)
        {
            const int Hh = 180;
            float t = (float)y / Hh;
            var c = Color.Lerp(new Color(0.02f, 0.06f, 0.16f), new Color(0.04f, 0.13f, 0.32f), t);
            if (x % 20 == 0 || y % 20 == 0) c = Color.Lerp(c, new Color(0.08f, 0.22f, 0.45f), 0.5f);
            if (y > Hh - 34) c = Color.Lerp(new Color(0.06f, 0.3f, 0.75f), new Color(0.1f, 0.45f, 0.95f), (float)x / 320f);
            if (y >= Hh - 37 && y <= Hh - 35) c = new Color(0.45f, 0.8f, 1f);
            c.a = 1;
            return c;
        }

        /// <summary>Ảnh chân dung tạm (bóng người) khi chưa có ảnh thật của nhà hóa học.</summary>
        static Color PortraitPixel(int x, int y)
        {
            var c = Color.Lerp(new Color(0.75f, 0.8f, 0.88f), new Color(0.88f, 0.91f, 0.96f), y / 200f);
            float head = Vector2.Distance(new Vector2(x, y), new Vector2(80, 128));
            float sx = (x - 80) / 62f, sy = (y - 30) / 48f;
            if (head < 32 || (sx * sx + sy * sy < 1f && y < 78)) c = new Color(0.32f, 0.38f, 0.48f);
            c.a = 1;
            return c;
        }

        // =====================================================================
        // Vỏ tòa nhà: sàn, trần, tường, cửa
        // =====================================================================

        enum OpeningKind { Window, Door, OpenDoor, Sliding, Vent, Auto }

        struct Opening
        {
            public float a, b, bottom, top;
            public OpeningKind kind;
            public float openYaw;
            public bool frosted, glass;   // cửa sổ kính mờ (giữa hai khu); cửa tự động bằng kính
            public int side, zoneA, zoneB;
            public Material leaf;
        }

        static Opening Win(float a, float b, float bottom = 0.9f, float top = 2.6f) =>
            new Opening { a = a, b = b, bottom = bottom, top = top, kind = OpeningKind.Window };
        /// <summary>Cửa sổ kính mờ giữa hai khu: khu bên kia có bị gỡ cũng không nhìn thấy khoảng trống.</summary>
        static Opening WinF(float a, float b, float bottom = 0.9f, float top = 2.6f) =>
            new Opening { a = a, b = b, bottom = bottom, top = top, kind = OpeningKind.Window, frosted = true };
        /// <summary>
        /// Cửa trượt tự động hai cánh. side: cánh lắp ở mặt tường phía nào (+1 / -1 theo trục pháp tuyến của tường);
        /// zoneA ở phía âm, zoneB ở phía dương của pháp tuyến (ZoneMap; -1 là ngoài trời).
        /// </summary>
        static Opening Auto(float a, float b, int side, int zoneA, int zoneB, Material leaf, bool glass = false, float top = 2.3f) =>
            new Opening { a = a, b = b, bottom = 0, top = top, kind = OpeningKind.Auto, side = side, zoneA = zoneA, zoneB = zoneB, leaf = leaf, glass = glass };
        static Opening Door(float a, float b, float top = 2.3f, float openYaw = 0f) =>
            new Opening { a = a, b = b, bottom = 0, top = top, kind = openYaw == 0 ? OpeningKind.Door : OpeningKind.OpenDoor, openYaw = openYaw };
        static Opening Sliding(float a, float b, float top = 2.5f) =>
            new Opening { a = a, b = b, bottom = 0, top = top, kind = OpeningKind.Sliding };
        static Opening Vent(float a, float b, float bottom, float top) =>
            new Opening { a = a, b = b, bottom = bottom, top = top, kind = OpeningKind.Vent };

        static Vector3 P(bool alongX, float c, float along, float y, float off) =>
            alongX ? new Vector3(along, y, c + off) : new Vector3(c + off, y, along);
        static Vector3 S(bool alongX, float len, float h, float thick) =>
            alongX ? new Vector3(len, h, thick) : new Vector3(thick, h, len);

        // Dãy phòng phía nam (nhà một tầng): hành lang z -2,2..0, phòng máy phân tích x 0..13,5 và phòng tự động hóa x 13,5..27 (z -9..-2,2)
        const float WingZ = -9f, CorridorZ = -2.2f, RoomSplitX = 13.5f;

        static void BuildShell(Transform root)
        {
            var g = Group("Vỏ tòa nhà", root);
            float cx = StoreX / 2;
            Box(g, "Sàn phòng thí nghiệm", new Vector3(LabX / 2, -0.15f, LabZ / 2), new Vector3(LabX, 0.3f, LabZ), floorLab);
            Box(g, "Sàn phòng hóa chất", new Vector3((LabX + StoreX) / 2, -0.15f, LabZ / 2), new Vector3(StoreX - LabX, 0.3f, LabZ), floorChem);
            Box(g, "Sàn hiên", new Vector3(cx, -0.17f, (LabZ + BalconyZ) / 2), new Vector3(StoreX, 0.3f, BalconyZ - LabZ), floorBalcony);
            Box(g, "Sàn hành lang", new Vector3(cx, -0.15f, CorridorZ / 2), new Vector3(StoreX, 0.3f, -CorridorZ), floorHall);
            Box(g, "Sàn dãy phòng nam", new Vector3(cx, -0.15f, (WingZ + CorridorZ) / 2), new Vector3(StoreX, 0.3f, CorridorZ - WingZ), floorLab);
            float z0 = WingZ - 0.1f, z1 = BalconyZ + 0.1f;
            // Trần tầng 1 mỏng lại còn y 3.6..3.7 (mặt dưới giữ 3.6m để tầng 1 nguyên vẹn, sàn tầng 2 ở 3.7..3.8m tránh z-fighting)
            Box(g, "Trần", new Vector3(cx, H + 0.05f, (z0 + z1) / 2), new Vector3(StoreX + 0.2f, 0.1f, z1 - z0), ceilingMat);
            // Mái và Diềm mái đã dời lên đỉnh tầng 2 (y = 7.4m) trong LabSceneBuilder.Floor2.Shell.cs
            Box(g, "Bậc thềm", new Vector3(cx, -0.1f, z1 + 0.25f), new Vector3(StoreX, 0.2f, 0.5f), sill);

            // Tường giữa phòng thí nghiệm và hành lang: cửa trượt tự động (cánh lắp phía hành lang), đoạn x 7,6..13,6 là tường
            // triển lãm dự án của học sinh; cửa sổ nội thất còn lại là kính mờ (khu bên kia có thể đang được gỡ khỏi bộ nhớ)
            Wall(g, "Tường nam phòng thí nghiệm", true, 0f, -0.1f, StoreX + 0.1f, paint, paint, -1,
                Auto(1.6f, 3.2f, -1, ZoneMap.Corridor, ZoneMap.Lab, leafLab), WinF(16.6f, 18.6f, 1.15f),
                Auto(21.0f, 22.2f, -1, ZoneMap.Corridor, ZoneMap.Store, leafStore), WinF(24.6f, 26.4f, 1.7f, 2.7f));
            Wall(g, "Tường bắc (ra hiên)", true, LabZ, -0.1f, StoreX + 0.1f, paint, facade, +1,
                Auto(1.0f, 2.6f, +1, ZoneMap.Lab, ZoneMap.Outside, null, glass: true, top: 2.5f), Win(3.4f, 5.4f, 1.15f), Win(6.4f, 8.4f, 1.15f),
                Win(21.6f, 23.4f, 1.7f, 2.7f), Win(24.6f, 26.4f, 1.7f, 2.7f));
            Wall(g, "Tường tây", false, 0f, 0.1f, LabZ - 0.1f, paint, facade, -1);
            Wall(g, "Tường đông", false, StoreX, 0.1f, LabZ - 0.1f, paint, facade, +1, Vent(12.8f, 13.4f, 2.6f, 3.2f));
            StoreEntrance(g, LabX, 0.1f, LabZ - 0.1f, 6.9f, 8.5f);

            // Dãy phòng phía nam
            Wall(g, "Tường hành lang", true, CorridorZ, -0.1f, StoreX + 0.1f, paint, paint, -1,
                Auto(5.9f, 7.5f, +1, ZoneMap.Analysis, ZoneMap.Corridor, leafAnalysis), WinF(1.0f, 4.8f, 1.0f, 2.6f), WinF(8.6f, 12.6f, 1.0f, 2.6f),
                Auto(19.0f, 20.6f, +1, ZoneMap.Robotics, ZoneMap.Corridor, leafRobot), WinF(14.4f, 18.2f, 1.0f, 2.6f), WinF(21.6f, 26.0f, 1.0f, 2.6f));
            Wall(g, "Tường ngăn hai phòng", false, RoomSplitX, WingZ + 0.1f, CorridorZ - 0.1f, paint, null, 0);
            Wall(g, "Tường nam (ngoài)", true, WingZ, -0.1f, StoreX + 0.1f, paint, facade, -1,
                Win(1.2f, 3.6f), Win(4.8f, 7.2f), Win(8.4f, 10.8f), Win(14.6f, 17.0f), Win(18.2f, 20.6f), Win(21.8f, 24.2f));
            Wall(g, "Tường tây dãy nam", false, 0f, WingZ + 0.1f, -0.1f, paint, facade, -1, Door(CorridorZ + 0.3f, -0.3f, 2.3f, 0f));
            Wall(g, "Tường đông dãy nam", false, StoreX, WingZ + 0.1f, -0.1f, paint, facade, +1,
                Auto(-1.9f, -0.3f, +1, ZoneMap.Corridor, ZoneMap.Stairs, leafLab));
        }

        static void Wall(Transform p, string name, bool alongX, float c, float from, float to,
                         Material inner, Material outer, int outerSide, params Opening[] ops)
        {
            var g = Group(name, p);
            Array.Sort(ops, (x, y) => x.a.CompareTo(y.a));
            int k = 0;
            void Seg(float a, float b, float y0, float y1)
            {
                if (b - a < 0.001f || y1 - y0 < 0.001f) return;
                k++;
                float mid = (a + b) / 2, ym = (y0 + y1) / 2;
                if (outer == null)
                {
                    Box(g, $"Đoạn {k}", P(alongX, c, mid, ym, 0), S(alongX, b - a, y1 - y0, T), inner);
                }
                else
                {
                    Box(g, $"Đoạn {k} (trong)", P(alongX, c, mid, ym, -outerSide * T / 4), S(alongX, b - a, y1 - y0, T / 2), inner);
                    Box(g, $"Đoạn {k} (ngoài)", P(alongX, c, mid, ym, outerSide * T / 4), S(alongX, b - a, y1 - y0, T / 2), outer);
                }
            }
            float cur = from;
            foreach (var o in ops)
            {
                Seg(cur, o.a, 0, H);
                Seg(o.a, o.b, 0, o.bottom);
                Seg(o.a, o.b, o.top, H);
                cur = o.b;
            }
            Seg(cur, to, 0, H);
            // len chân tường (ốp chân) màu xám
            Box(g, "Len chân tường", P(alongX, c, (from + to) / 2, 0.05f, -outerSide * (T / 2 + 0.005f)), S(alongX, to - from, 0.1f, 0.01f), panelGrey, false);

            foreach (var o in ops)
            {
                // cửa sổ / cửa ra ngoài trời: thêm tấm cửa sổ ảo ở mặt ngoài (B4), bật khi người xem ở trong nhà
                Vector3 outward = alongX ? new Vector3(0, 0, outerSide) : new Vector3(outerSide, 0, 0);
                if (outer != null && o.kind == OpeningKind.Window && !o.frosted)
                    VirtualWindowQuad(P(alongX, c, (o.a + o.b) / 2, (o.bottom + o.top) / 2, outerSide * (T / 2 + 0.03f)), outward, o.b - o.a - 0.05f, o.top - o.bottom - 0.05f);
                if (o.kind == OpeningKind.Auto && (o.zoneA == ZoneMap.Outside || o.zoneB == ZoneMap.Outside))
                    VirtualWindowQuad(P(alongX, c, (o.a + o.b) / 2, o.top / 2, outerSide * (T / 2 + 0.32f)), outward, o.b - o.a, o.top);
                switch (o.kind)
                {
                    case OpeningKind.Window: WindowFill(g, alongX, c, o); break;
                    case OpeningKind.Door:
                    case OpeningKind.OpenDoor: DoorFill(g, alongX, c, o); break;
                    case OpeningKind.Sliding: SlidingFill(g, alongX, c, o); break;
                    case OpeningKind.Auto: AutoDoorFill(g, alongX, c, o); break;
                    case OpeningKind.Vent: break;  // quạt hút do phòng hóa chất tự lắp
                }
            }
        }

        static void WindowFill(Transform p, bool alongX, float c, Opening o)
        {
            var w = Group($"Cửa sổ {o.a:0.0}-{o.b:0.0}", p);
            float len = o.b - o.a, h = o.top - o.bottom, mid = (o.a + o.b) / 2, ym = (o.bottom + o.top) / 2;
            const float f = 0.05f, d = 0.07f;
            Box(w, "Khung trái", P(alongX, c, o.a + f / 2, ym, 0), S(alongX, f, h, d), alu);
            Box(w, "Khung phải", P(alongX, c, o.b - f / 2, ym, 0), S(alongX, f, h, d), alu);
            Box(w, "Khung dưới", P(alongX, c, mid, o.bottom + f / 2, 0), S(alongX, len, f, d), alu);
            Box(w, "Khung trên", P(alongX, c, mid, o.top - f / 2, 0), S(alongX, len, f, d), alu);
            int panes = Mathf.Max(1, Mathf.RoundToInt(len / 1.0f));
            for (int i = 1; i < panes; i++)
                Box(w, $"Đố {i}", P(alongX, c, o.a + len * i / panes, ym, 0), S(alongX, f * 0.7f, h, d), alu);
            Box(w, "Bậu cửa", P(alongX, c, mid, o.bottom - 0.02f, 0), S(alongX, len + 0.08f, 0.04f, T + 0.1f), sill);
            if (o.frosted)
            {
                // kính mờ (vẽ như vật đục, không trong suốt) + dải phim màu ngang tầm mắt ở hai mặt
                Box(w, "Kính mờ", P(alongX, c, mid, ym, 0), S(alongX, len - f, h - f, 0.012f), frostedGlass, false);
                foreach (int sd in new[] { -1, 1 })
                    Box(w, "Dải phim màu", P(alongX, c, mid, 1.45f, sd * 0.008f), S(alongX, len - f, 0.06f, 0.002f), filmBand, false);
                return;
            }
            NoShadow(Box(w, "Kính", P(alongX, c, mid, ym, 0), S(alongX, len - f, h - f, 0.01f), windowGlass));
            // rèm cuốn kéo lên một nửa
            Box(w, "Rèm cuốn", P(alongX, c, mid, o.top - 0.25f, 0), S(alongX, len - 0.1f, 0.42f, 0.015f), Mat("RemCuon", new Color(0.86f, 0.87f, 0.86f), 0.1f), false);
        }

        static void DoorFill(Transform p, bool alongX, float c, Opening o)
        {
            var g = Group($"Cửa đi {o.a:0.0}-{o.b:0.0}", p);
            float len = o.b - o.a, h = o.top;
            const float f = 0.06f;
            Box(g, "Khung trái", P(alongX, c, o.a + f / 2, h / 2, 0), S(alongX, f, h, T + 0.02f), alu);
            Box(g, "Khung phải", P(alongX, c, o.b - f / 2, h / 2, 0), S(alongX, f, h, T + 0.02f), alu);
            Box(g, "Khung trên", P(alongX, c, (o.a + o.b) / 2, h - f / 2, 0), S(alongX, len, f, T + 0.02f), alu);

            var hinge = Group("Bản lề", g, P(alongX, c, o.a + f, 0, 0), o.openYaw);
            float leafLen = len - 2 * f, leafH = h - f - 0.01f;
            Vector3 along = alongX ? Vector3.right : Vector3.forward;
            Vector3 side = alongX ? Vector3.forward : Vector3.right;
            Box(hinge, "Cánh cửa", along * leafLen / 2 + Vector3.up * leafH / 2, alongX ? new Vector3(leafLen, leafH, 0.04f) : new Vector3(0.04f, leafH, leafLen), oak);
            Box(hinge, "Ô kính cánh cửa", along * leafLen / 2 + Vector3.up * 1.55f, alongX ? new Vector3(0.22f, 0.9f, 0.045f) : new Vector3(0.045f, 0.9f, 0.22f), darkGlass, false);
            foreach (int sgn in new[] { -1, 1 })
                Box(hinge, "Tay nắm", along * (leafLen - 0.08f) + Vector3.up * 1.0f + side * (0.04f * sgn), new Vector3(0.03f, 0.03f, 0.03f) + (alongX ? new Vector3(0.09f, 0, 0) : new Vector3(0, 0, 0.09f)), steel, false);
        }

        static void SlidingFill(Transform p, bool alongX, float c, Opening o)
        {
            // Cửa kính lùa ra ban công: một cánh cố định ở ray ngoài, cánh kia đã lùa chồng lên (đang mở)
            var g = Group($"Cửa kính lùa {o.a:0.0}-{o.b:0.0}", p);
            float half = (o.b - o.a) / 2, h = o.top;
            Box(g, "Ray dưới", P(alongX, c, (o.a + o.b) / 2, 0.01f, 0), S(alongX, o.b - o.a, 0.02f, T), alu);
            Box(g, "Khung trên", P(alongX, c, (o.a + o.b) / 2, h - 0.03f, 0), S(alongX, o.b - o.a, 0.06f, T), alu);
            GlassPanel(g, alongX, c, o.a, o.a + half + 0.03f, h - 0.06f, +0.04f, "Cánh cố định");
            GlassPanel(g, alongX, c, o.a + 0.05f, o.a + half + 0.08f, h - 0.06f, -0.04f, "Cánh lùa (đang mở)");
        }

        static void GlassPanel(Transform p, bool alongX, float c, float a, float b, float h, float off, string name)
        {
            var g = Group(name, p);
            const float f = 0.05f;
            float mid = (a + b) / 2;
            Box(g, "Khung trái", P(alongX, c, a + f / 2, h / 2 + 0.02f, off), S(alongX, f, h, 0.04f), alu);
            Box(g, "Khung phải", P(alongX, c, b - f / 2, h / 2 + 0.02f, off), S(alongX, f, h, 0.04f), alu);
            Box(g, "Khung dưới", P(alongX, c, mid, 0.06f, off), S(alongX, b - a, 0.08f, 0.04f), alu);
            Box(g, "Khung trên", P(alongX, c, mid, h - 0.005f, off), S(alongX, b - a, f, 0.04f), alu);
            NoShadow(Box(g, "Kính", P(alongX, c, mid, h / 2 + 0.04f, off), S(alongX, b - a - f, h - 0.1f, 0.01f), windowGlass));
        }

        // =====================================================================
        // Ánh sáng
        // =====================================================================

        static void BuildLighting(Transform root)
        {
            var g = Group("Ánh sáng", root);
            var sun = sunLight;
            if (sun == null) sun = new GameObject().AddComponent<Light>();
            sun.name = "Mặt trời";
            sun.transform.SetParent(g, false);
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(40f, 25f, 0f);  // nắng từ phía nam hắt qua cửa sổ
            sun.color = new Color(1f, 0.93f, 0.82f);
            sun.intensity = 1.4f;
            sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun;

            // Môi trường ấm, dịu hơn trước: tường trắng không bị cháy sáng, gỗ và màu nhận diện hiện rõ
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.74f, 0.75f, 0.77f);
            RenderSettings.ambientEquatorColor = new Color(0.68f, 0.66f, 0.63f);
            RenderSettings.ambientGroundColor = new Color(0.44f, 0.41f, 0.38f);
            RenderSettings.reflectionIntensity = 0.8f;
            BuildPostVolume(g);
        }

        /// <summary>
        /// Hậu kỳ toàn cảnh: tonemapping Neutral (giữ màu trắng không bị cháy sáng), bloom nhẹ cho đèn và màn hình,
        /// tăng tương phản / độ bão hòa một chút, cân bằng trắng hơi ấm (~4300 K). Không vignette, không hạt phim (film grain),
        /// không quang sai màu: không thêm hiệu ứng nào gây nhiễu hạt.
        /// </summary>
        static void BuildPostVolume(Transform p)
        {
            string path = $"{Root}/HauKy.asset";
            AssetDatabase.DeleteAsset(path);  // tạo lại mỗi lần dựng để không tích tụ thành phần cũ
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            var tm = profile.Add<Tonemapping>(true);
            tm.mode.Override(TonemappingMode.Neutral);
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.1f);
            bloom.intensity.Override(0.25f);
            bloom.scatter.Override(0.6f);
            // lọc chất lượng cao: viền sáng của dải LED mịn, không nhấp nháy theo điểm ảnh. Đo trên Iris Xe: không chậm hơn đo được
            // (66,3 so với 67,0 FPS, trong sai số). Đặt KHKT_BLOOM_HQ=0 để tắt khi đo so sánh.
            bloom.highQualityFiltering.Override(System.Environment.GetEnvironmentVariable("KHKT_BLOOM_HQ") != "0");
            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0f);
            color.contrast.Override(14f);
            color.saturation.Override(14f);
            var wb = profile.Add<WhiteBalance>(true);
            wb.temperature.Override(6f);
            foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var go = new GameObject("Hậu kỳ");
            go.transform.SetParent(p, false);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1;
            volume.sharedProfile = profile;
        }

        /// <summary>
        /// Mỗi phòng một đầu dò phản chiếu nướng sẵn (chiếu hộp), để kính, inox, mặt bàn bóng phản chiếu đúng phòng của nó
        /// thay vì bầu trời. Nướng một lần khi dựng cảnh; lúc chạy không tốn gì.
        /// </summary>
        static void BakeReflectionProbes(Transform root)
        {
            var g = Group("Đầu dò phản chiếu", root);
            Folder(Root, "Probes");
            var rooms = new (string name, Vector3 center, Vector3 size)[]
            {
                ("Phòng thí nghiệm", new Vector3(LabX / 2, 1.6f, LabZ / 2), new Vector3(LabX, H, LabZ)),
                ("Phòng hóa chất", new Vector3((LabX + StoreX) / 2, 1.6f, LabZ / 2), new Vector3(StoreX - LabX, H, LabZ)),
                ("Hành lang", new Vector3(StoreX / 2, 1.4f, CorridorZ / 2), new Vector3(StoreX, H, -CorridorZ)),
                ("Phòng máy phân tích", new Vector3(RoomSplitX / 2, 1.6f, (WingZ + CorridorZ) / 2), new Vector3(RoomSplitX, H, CorridorZ - WingZ)),
                ("Phòng robot", new Vector3((RoomSplitX + StoreX) / 2, 1.6f, (WingZ + CorridorZ) / 2), new Vector3(StoreX - RoomSplitX, H, CorridorZ - WingZ)),

                // Tầng 2
                ("Phòng sinh học", new Vector3(LabX / 2, F2_Y + 1.6f, LabZ / 2), new Vector3(LabX, F2_H, LabZ)),
                ("Phòng vi sinh", new Vector3((LabX + StoreX) / 2, F2_Y + 1.6f, 11.25f), new Vector3(StoreX - LabX, F2_H, 7.5f)),
                ("Kho sinh phẩm", new Vector3((LabX + StoreX) / 2, F2_Y + 1.6f, 3.75f), new Vector3(StoreX - LabX, F2_H, 7.5f)),
                ("Hành lang tầng 2", new Vector3(StoreX / 2, F2_Y + 1.4f, CorridorZ / 2), new Vector3(StoreX, F2_H, -CorridorZ)),
                ("Sinh học phân tử", new Vector3(RoomSplitX / 2, F2_Y + 1.6f, (WingZ + CorridorZ) / 2), new Vector3(RoomSplitX, F2_H, CorridorZ - WingZ)),
                ("Nhà kính", new Vector3((RoomSplitX + StoreX) / 2, F2_Y + 2.0f, (WingZ + CorridorZ) / 2), new Vector3(StoreX - RoomSplitX, F2_H + 1.2f, CorridorZ - WingZ)),
                ("Lõi thang", new Vector3(28.5f, F2_Y / 2, 1.9f), new Vector3(3f, F2_Y + F2_H, 8.2f)),
            };
            int ok = 0;
            foreach (var r in rooms)
            {
                var go = new GameObject("Phản chiếu " + r.name);
                go.transform.SetParent(g, false);
                go.transform.localPosition = r.center;
                var probe = go.AddComponent<ReflectionProbe>();
                probe.mode = ReflectionProbeMode.Baked;
                probe.size = r.size;
                probe.boxProjection = true;
                probe.resolution = 128;
                probe.hdr = true;
                string path = $"{Root}/Probes/{r.name}.exr";
                try
                {
                    if (UnityEditor.Lightmapping.BakeReflectionProbe(probe, path))
                    {
                        AssetDatabase.ImportAsset(path);
                        probe.mode = ReflectionProbeMode.Custom;
                        probe.customBakedTexture = AssetDatabase.LoadAssetAtPath<Texture>(path);
                        ok++;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[KHKT] Không nướng được phản chiếu {r.name}: {e.Message}");
                }
            }
            Debug.Log($"[KHKT] Đã nướng {ok}/{rooms.Length} đầu dò phản chiếu.");
        }

        static void Folder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) AssetDatabase.CreateFolder(parent, name);
        }

        static Light PointLight(Transform p, string name, Vector3 pos, float range, float intensity, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(p, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = range;
            l.intensity = intensity;
            l.color = color;
            l.shadows = LightShadows.None;
            return l;
        }

        /// <summary>Đèn LED tấm 1,2 x 0,3 m âm trần (theo trục z), kèm một đèn điểm.</summary>
        static void LedPanel(Transform p, string name, float x, float z, float intensity = 2.4f, float range = 7.5f, bool withLight = true)
        {
            var g = Group(name, p, new Vector3(x, H, z));
            Box(g, "Viền đèn", new Vector3(0, -0.006f, 0), new Vector3(0.32f, 0.012f, 1.22f), lampHousing, false);
            NoShadow(Box(g, "Bóng tuýp", new Vector3(0, -0.013f, 0), new Vector3(0.29f, 0.004f, 1.19f), tubeGlow, false));
            if (withLight) PointLight(g, "Đèn", new Vector3(0, -0.35f, 0), range, intensity, new Color(1f, 0.98f, 0.95f));
        }

        /// <summary>
        /// Đèn rọi góc rộng (150°) đặt sát trần, chiếu xuống, theo lưới xs × zs (đèn LED trên trần chỉ để nhìn, ánh sáng do các đèn này).
        /// Trước đây là đèn điểm: chiếu cả lên trần thành những đốm trắng chói rất lớn. Ánh sáng trắng trung tính ~5000 K.
        /// </summary>
        static void RoomLights(Transform p, float[] xs, float[] zs, float range, float intensity)
        {
            foreach (float x in xs)
                foreach (float z in zs)
                {
                    var l = PointLight(p, "Đèn", new Vector3(x, H - 0.25f, z), range * 1.15f, intensity * 1.25f, new Color(1f, 0.95f, 0.88f));
                    l.type = LightType.Spot;
                    l.spotAngle = 150f;
                    l.innerSpotAngle = 80f;
                    l.transform.localRotation = Quaternion.Euler(90f, 0, 0);
                }
        }

        // =====================================================================
        // Tối ưu vẽ
        // =====================================================================

        public const string LayerDetail = "ChiTietNho", LayerSmallText = "ChuNho", LayerMidText = "ChuVua";

        /// <summary>
        /// Vật nhỏ (dưới 25 cm) thôi đổ bóng: bóng mặt trời phải vẽ lại mọi vật đổ bóng cho từng tầng bóng, mà bóng của nắp lọ,
        /// nhãn, ống nghiệm gần như không nhìn thấy. Chi tiết nhỏ và chữ nhỏ vào lớp riêng để camera thôi vẽ khi ở xa
        /// (DesktopViewer đặt khoảng cách: chữ nhãn lọ 6 m, chữ thường 15 m, chi tiết nhỏ 14 m).
        /// </summary>
        static void OptimizeRenderers(Transform root)
        {
            int detail = EnsureLayer(LayerDetail), smallText = EnsureLayer(LayerSmallText), midText = EnsureLayer(LayerMidText);
            Physics.SyncTransforms();
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r.GetComponentInParent<Khkt.Demo.LabRobot>() != null) continue;
                var tm = r.GetComponent<TextMesh>();
                if (tm != null)
                {
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    r.receiveShadows = false;
                    float letter = tm.characterSize * tm.fontSize / 10f;  // ngược công thức trong LabKit.Label
                    if (letter < 0.012f) r.gameObject.layer = smallText;
                    else if (letter < 0.05f) r.gameObject.layer = midText;
                    continue;
                }
                var size = r.bounds.size;
                float maxDim = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
                if (maxDim < 0.25f) r.shadowCastingMode = ShadowCastingMode.Off;
                if (maxDim < 0.2f) r.gameObject.layer = detail;
            }
        }

        static int EnsureLayer(string name)
        {
            int l = LayerMask.NameToLayer(name);
            if (l >= 0) return l;
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tags.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                var p = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(p.stringValue)) continue;
                p.stringValue = name;
                tags.ApplyModifiedPropertiesWithoutUndo();
                return i;
            }
            Debug.LogWarning("[KHKT] Hết chỗ thêm lớp " + name);
            return 0;
        }

        // =====================================================================
        // Trạm và người xem
        // =====================================================================

        public static readonly Vector3 TitrationBench = new Vector3(5.2f, 0, 2.6f);
        public static readonly Vector3 QualitativeBench = new Vector3(5.2f, 0, 7.5f);

        static void BuildStationsAndViewer(Transform root)
        {
            var g = Group("Trạm", root);
            Station(g, 1, "Bàn chuẩn độ (bàn 1)", TitrationBench + new Vector3(0.85f, 0, 0), -90, true);
            Station(g, 2, "Bàn định tính (bàn 4)", QualitativeBench + new Vector3(0.85f, 0, 0), -90, true);
            Station(g, 3, "Khu thiết bị phân tích", new Vector3(16.9f, 0, 1.75f), 180, false);
            Station(g, 4, "Bàn đun nóng - tổng hợp", new Vector3(16.8f, 0, 9.05f), 180, false);
            Station(g, 5, "Tủ hút khí độc", new Vector3(17.0f, 0, 13.0f), 0, false);
            Station(g, 6, "Phòng hóa chất", new Vector3(21.3f, 0, 7.7f), 90, false);
            Station(g, 7, "Phòng máy phân tích hiện đại", new Vector3(2.6f, 0, -3.0f), 143, false);
            Station(g, 8, "Phòng tự động hóa & robot", new Vector3(21.5f, 0, -3.4f), 229, false);
            Station(g, 9, "Toàn cảnh", new Vector3(19.0f, 0, 13.6f), -122, false);
            Station(g, 10, "Bàn giáo viên", new Vector3(0.95f, 0, 7.5f), 90, false);
            Station(g, 11, "Khu an toàn", new Vector3(1.9f, 0, 2.4f), -100, false);
            Station(g, 12, "Khu chế tạo dụng cụ", FabricationStationPos, 90, true);
            Station(g, 13, "Bàn thí nghiệm lắp ráp", AssemblyBenchPos + new Vector3(0, 0, -1.05f), 0, false);

            // Trạm dịch chuyển Tầng 2 (20..31) theo bo_cuc_tang2.json
            Station(g, 20, "Toàn cảnh lab sinh học", new Vector3(19.0f, F2_Y, 13.6f), -122, false);
            Station(g, 21, "Bàn kính hiển vi (bàn 1)", new Vector3(6.05f, F2_Y, 2.6f), -90, true);
            Station(g, 22, "Bàn giáo viên tầng 2", new Vector3(0.95f, F2_Y, 7.5f), 90, false);
            Station(g, 23, "Tủ an toàn sinh học", new Vector3(6.6f, F2_Y, 13.4f), 0, false);
            Station(g, 24, "Khu giải phẫu", new Vector3(16.4f, F2_Y, 11.2f), 90, false);
            Station(g, 25, "Tủ ấm và nồi hấp", new Vector3(16.4f, F2_Y, 5.2f), 90, false);
            Station(g, 26, "Phòng vi sinh", new Vector3(23.0f, F2_Y, 9.6f), 0, false);
            Station(g, 27, "Kho sinh phẩm", new Vector3(23.5f, F2_Y, 3.7f), 0, false);
            Station(g, 28, "Sinh học phân tử – tách chiết, PCR", new Vector3(6.7f, F2_Y, -3.4f), 180, false);
            Station(g, 29, "Điện di và soi gel", new Vector3(11.2f, F2_Y, -6.0f), 180, false);
            Station(g, 30, "Nhà kính – quang hợp", new Vector3(19.8f, F2_Y, -3.4f), 180, false);
            Station(g, 31, "Sảnh thang máy tầng 2", new Vector3(27.8f, F2_Y, -1.5f), 270, false);

            var viewer = new GameObject("Người xem (chuột-bàn phím)");
            viewer.transform.SetParent(root, false);
            viewer.transform.localPosition = TitrationBench + new Vector3(0.85f, 0, 0);
            viewer.transform.localRotation = Quaternion.Euler(0, -90, 0);
            var cc = viewer.AddComponent<CharacterController>();
            cc.height = 1.7f;
            cc.radius = 0.22f;
            cc.center = new Vector3(0, 0.86f, 0);
            var camGo = new GameObject("Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(viewer.transform, false);
            camGo.transform.localPosition = new Vector3(0, 1.25f, 0);
            var cam = camGo.AddComponent<Camera>();
            viewerCamera = cam;
            cam.nearClipPlane = 0.02f;
            cam.farClipPlane = 150f;
            cam.fieldOfView = 70f;
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;  // tonemapping, bloom, chỉnh màu (Volume "Hậu kỳ")
            // SMAA: khử răng cưa theo cạnh, sắc nét hơn FXAA và không làm mờ / không gây hạt như TAA, vẫn nhẹ cho card tích hợp
            // Đo 3 vòng trên Iris Xe: SMAA cao 66,3 · SMAA vừa 67,0 · FXAA 66,9 FPS (trong sai số) nên chọn SMAA cao (đẹp nhất)
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camData.antialiasingQuality = AntialiasingQuality.High;
            switch (System.Environment.GetEnvironmentVariable("KHKT_AA"))  // đo so sánh: fxaa, smaa_med
            {
                case "fxaa": camData.antialiasing = AntialiasingMode.FastApproximateAntialiasing; break;
                case "smaa_med": camData.antialiasingQuality = AntialiasingQuality.Medium; break;
            }
            camGo.AddComponent<AudioListener>();
            var dv = viewer.AddComponent<DesktopViewer>();
            dv.cameraTransform = camGo.transform;
        }

        static void Station(Transform p, int order, string name, Vector3 pos, float yaw, bool seated)
        {
            var g = Group(name, p, pos, yaw);
            var s = g.gameObject.AddComponent<LabStation>();
            s.order = order;
            s.displayName = name;
            s.seated = seated;
        }

        static Vector2 V(float r, float y) => new Vector2(r, y);
    }
}
