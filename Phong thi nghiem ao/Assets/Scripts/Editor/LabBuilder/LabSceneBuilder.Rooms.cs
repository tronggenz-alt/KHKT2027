using UnityEditor;
using Khkt.Environment;
using Khkt.Interaction;
using UnityEngine;
using static Khkt.EditorTools.LabKit;

namespace Khkt.EditorTools
{
    public static partial class LabSceneBuilder
    {
        // =====================================================================
        // Phòng thí nghiệm chính
        // =====================================================================

        static readonly float[] BenchX = { 5.2f, 8.6f, 12.0f };
        static readonly float[] BenchZ = { 2.6f, 7.5f, 12.4f };

        static void BuildMainLab(Transform root)
        {
            var lab = Group("Phòng thí nghiệm", root);
            FrontWall(lab);
            TeacherArea(lab);

            var benches = Group("Bàn thí nghiệm học sinh", lab);
            int n = 1;
            for (int row = 0; row < BenchZ.Length; row++)
                for (int col = 0; col < BenchX.Length; col++)
                {
                    int kind = n == 1 ? 1 : n == 4 ? 2 : 0;  // bàn 1 chuẩn độ, bàn 4 (hàng đầu, dãy giữa) định tính
                    StudentBench(benches, $"Bàn thí nghiệm {n}", new Vector3(BenchX[col], 0, BenchZ[row]), row == 2 ? -1 : +1, kind);
                    BenchScreen(benches.Find($"Bàn thí nghiệm {n}"), $"Bàn thí nghiệm {n}", n, row == 2 ? -1 : +1);
                    n++;
                }

            var hoods = Group("Dãy tủ hút", lab);
            for (int i = 0; i < 3; i++) FumeHood(hoods, new Vector3(15.2f + i * 1.75f, 0, LabZ - 0.55f), 90f, i + 1);
            WallSinkCounter(lab, new Vector3(5.9f, 0, LabZ - 0.42f), -90f);
            GlasswareWall(lab);
            SafetyStation(lab);
            InstrumentBench(lab);
            SynthesisIsland(lab);
            AssemblyBench(lab);  // giai đoạn C: bàn đặt bộ dụng cụ đã gia công (có vòi nước, vòi hút)
            FabricationBooth(lab);  // ngăn kính khu chế tạo dụng cụ
            PartTemplates(root);  // kho bộ phận mẫu cho khu chế tạo

            var misc = Group("Đồ khác", lab);
            Box(misc, "Công tắc đèn", new Vector3(3.55f, 1.3f, 0.11f), new Vector3(0.12f, 0.12f, 0.02f), signWhite, false);
            MeshObj(misc, "Thùng rác", Lathe("ThungRac", 24, V(0, 0), V(0.15f, 0), V(0.15f, 0), V(0.17f, 0.45f), V(0.175f, 0.46f),
                                                     V(0.165f, 0.46f), V(0.142f, 0.012f), V(0, 0.012f)),
                new Vector3(3.6f, 0, 0.6f), Mat("ThungRacXam", new Color(0.4f, 0.42f, 0.44f), 0.3f), true);
            WasteStation(misc, new Vector3(19.55f, 0, 4.55f));
            foreach (float x in new[] { 8.6f, 15.0f })
            {
                var ac = Group("Điều hòa", misc, new Vector3(x, 2.95f, 0.22f));
                Box(ac, "Thân máy", Vector3.zero, new Vector3(1.0f, 0.3f, 0.22f), whitePlastic, false);
                Box(ac, "Cửa gió", new Vector3(0, -0.12f, 0.1f), new Vector3(0.9f, 0.03f, 0.03f), panelGrey, false);
                NoShadow(Box(ac, "Đèn báo", new Vector3(0.42f, -0.05f, 0.112f), new Vector3(0.02f, 0.01f, 0.004f), Mat("DenBaoXanh", new Color(0.2f, 0.9f, 0.6f), 0.5f, 0, emission: new Color(0.3f, 1.5f, 0.8f)), false));
            }

            var lights = Group("Đèn trần", lab);
            // Tấm trần gỗ treo trên các hàng bàn, đèn dải LED dưới tấm (chỉ để nhìn); ánh sáng do 9 đèn rọi góc rộng
            LabCeilingLeds(lights);
            RoomLights(lights, new[] { 3.7f, 10.3f, 16.9f }, new[] { 2.6f, 7.5f, 12.4f }, 10f, 3.8f);

            // Phong cách lab trường quốc tế: lối đi trên sàn, biển khu song ngữ, cây xanh, tường các nhà hóa học, tường triển lãm dự án
            LabWalkways(lab);
            ChemistWall(lab);
            ProjectWall(lab);
            var signs = Group("Biển khu", lab);
            // logo trường cỡ lớn trên vách kính mờ phía đông (mặt phòng lab), nhìn thấy từ khắp phòng
            SchoolLogo(lab, new Vector3(19.955f, 2.95f, 3.7f), Vector3.left, 4.2f);
            ZoneSign(signs, "KHU THỰC HÀNH", new Vector3(8.6f, 2.95f, 5.05f), 90f, 2.4f, "PRACTICAL ZONE");
            ZoneSign(signs, "KHU LẮP RÁP & TỔNG HỢP", new Vector3(16.8f, 2.9f, 6.2f), 0f, 3.0f, "ASSEMBLY & SYNTHESIS");
            ZoneSign(signs, "TỦ HÚT KHÍ ĐỘC", new Vector3(16.95f, 2.95f, 14.05f), 0f, 2.6f, "FUME CUPBOARDS");
            ZoneSign(signs, "KHU THIẾT BỊ PHÂN TÍCH", new Vector3(16.7f, 2.9f, 1.15f), 0f, 3.0f, "INSTRUMENTS");
            var plants = Group("Cây xanh trong phòng", lab);
            foreach (var pos in new[] { new Vector3(0.55f, 0, 14.45f), new Vector3(9.05f, 0, 14.5f), new Vector3(13.92f, 0, 14.5f),
                                        new Vector3(16.78f, 0, 10.75f), new Vector3(7.15f, 0, 0.42f) })
                ModernPlant(plants, pos);
        }

        static void FrontWall(Transform lab)
        {
            var g = Group("Tường bảng", lab);
            const float zc = 7.5f;
            // Tường video 3 màn hình (chiếu bài, camera bàn giáo viên, thảo luận) thay bảng trắng và TV cũ
            VideoWall(g, zc);

            // Bảng tuần hoàn bên trái tường video
            Box(g, "Khung bảng tuần hoàn", new Vector3(0.103f, 1.65f, 3.6f), new Vector3(0.004f, 1.0f, 1.56f), darkMetal, false);
            Panel(g, "Bảng tuần hoàn", new Vector3(0.106f, 1.65f, 3.6f), Vector3.right, 1.5f, 0.95f, poster);
            Label(g, "BẢNG TUẦN HOÀN CÁC NGUYÊN TỐ HÓA HỌC", new Vector3(0.108f, 2.083f, 3.6f), Vector3.right, 0.045f, Color.white);

            var clock = Group("Đồng hồ", g, new Vector3(0.12f, 2.82f, 3.6f));
            Cyl(clock, "Vành", new Vector3(-0.005f, 0, 0), 0.17f, 0.03f, darkMetal, false, new Vector3(0, 0, 90));
            Cyl(clock, "Mặt", new Vector3(0.008f, 0, 0), 0.155f, 0.012f, signWhite, false, new Vector3(0, 0, 90));
            ClockHand(clock, "Kim giờ", 290f, 0.08f, 0.012f);
            ClockHand(clock, "Kim phút", 240f, 0.12f, 0.007f);
            // logo trường cỡ lớn chính giữa phía trên tường video; tên phòng song ngữ dời sang bên trái
            SchoolLogo(g, new Vector3(0.112f, 3.18f, zc), Vector3.right, 2.95f);
            Label(g, "PHÒNG THÍ NGHIỆM HÓA HỌC", new Vector3(0.112f, 3.2f, zc - 2.55f), Vector3.right, 0.085f, new Color(0.0f, 0.36f, 0.4f));
            Label(g, "CHEMISTRY LAB", new Vector3(0.112f, 3.05f, zc - 2.55f), Vector3.right, 0.05f, new Color(0.35f, 0.38f, 0.4f));
        }

        const string SchoolLogoPath = "Assets/Lab/Logo/LogoTruongLeQuyDon.png";

        /// <summary>Logo Trường THPT Chuyên Lê Quý Đôn (ảnh nền trong suốt, tỉ lệ 1024 × 273) dán trên tường, rộng width mét.</summary>
        static void SchoolLogo(Transform p, Vector3 center, Vector3 facing, float width)
        {
            var importer = AssetImporter.GetAtPath(SchoolLogoPath) as TextureImporter;
            if (importer == null) { Debug.LogWarning($"[KHKT] Chưa có ảnh logo trường ở {SchoolLogoPath}"); return; }
            if (!importer.alphaIsTransparency || importer.wrapMode != TextureWrapMode.Clamp || importer.maxTextureSize < 1024)
            {
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = true;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(SchoolLogoPath);
            float h = width * tex.height / tex.width;
            var mat = Mat("LogoTruong", Color.white, 0.15f, 0, tex, null, null, transparent: true);
            var quad = Panel(p, "Logo Trường THPT Chuyên Lê Quý Đôn", center + facing.normalized * 0.002f, facing, width, h, mat);
            NoShadow(quad);
            Info(quad.transform, "Trường THPT Chuyên Lê Quý Đôn – Gia Lai", "Trí tuệ – Bản lĩnh – Trung thực – Nhân văn.");
        }

        static void ClockHand(Transform clock, string name, float angle, float length, float width)
        {
            var dir = new Vector3(0, Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            Box(clock, name, new Vector3(0.017f, 0, 0) + dir * length / 2, new Vector3(0.003f, length, width), blackPlastic, false, new Vector3(angle, 0, 0));
        }

        static void TeacherArea(Transform lab)
        {
            var g = Group("Bàn giáo viên", lab, new Vector3(1.9f, 0, 7.5f));
            const float top = 0.9f;
            Box(g, "Chân đế", new Vector3(0, 0.04f, 0), new Vector3(0.72f, 0.08f, 2.52f), darkMetal);
            Box(g, "Thân tủ", new Vector3(0, 0.47f, -0.27f), new Vector3(0.78f, 0.78f, 2.06f), cabinet);
            HollowCabinet(g, "Thân tủ dưới bồn", new Vector3(0, 0.08f, 1.02f), new Vector3(0.78f, 0.78f, 0.54f));
            Box(g, "Mặt trước", new Vector3(0.395f, 0.47f, 0), new Vector3(0.01f, 0.78f, 2.6f), cabinetDoor);
            Box(g, "Dải nhấn mặt trước", new Vector3(0.402f, 0.82f, 0), new Vector3(0.004f, 0.04f, 2.6f), accent, false);
            for (int i = 0; i < 4; i++)
            {
                float z = -0.97f + i * 0.65f;
                Box(g, $"Cánh tủ {i + 1}", new Vector3(-0.395f, 0.47f, z), new Vector3(0.01f, 0.7f, 0.6f), cabinetDoor, false);
                Box(g, $"Tay nắm {i + 1}", new Vector3(-0.41f, 0.7f, z + 0.22f), new Vector3(0.02f, 0.12f, 0.015f), steel, false);
            }
            TopWithSink(g, "Mặt bàn", 0.84f, 2.66f, top, new Vector2(-0.12f, 1.01f), new Vector2(0.36f, 0.4f), 0.2f);
            Faucet(g, "Vòi nước", new Vector3(0.16f, top, 1.01f), Vector3.left, 0.3f, 0.17f);

            var items = Group("Dụng cụ trên bàn giáo viên", g);
            Laptop(items, new Vector3(-0.1f, top, -0.8f));
            AlcoholLamp(items, new Vector3(0.1f, top, 0.2f));
            TestTubeRack(items, new Vector3(0.1f, top, -0.2f), 0f);
            MeshObj(items, "Cốc thủy tinh 250 ml", Beaker("Coc250", 0.035f, 0.1f), new Vector3(0.15f, top, 0.5f), labGlass);

            var chair = Group("Ghế giáo viên", lab, new Vector3(0.85f, 0, 6.5f), 90);
            Box(chair, "Mặt ghế", new Vector3(0, 0.47f, 0), new Vector3(0.44f, 0.04f, 0.44f), stoolSeat);
            Box(chair, "Tựa lưng", new Vector3(0, 0.78f, -0.21f), new Vector3(0.42f, 0.3f, 0.02f), stoolSeat);
            foreach (var c in new[] { V(-0.19f, -0.19f), V(0.19f, -0.19f), V(-0.19f, 0.19f), V(0.19f, 0.19f) })
                Box(chair, "Chân ghế", new Vector3(c.x, 0.23f, c.y), new Vector3(0.03f, 0.46f, 0.03f), darkMetal, false);
            Box(chair, "Thanh tựa", new Vector3(-0.19f, 0.7f, -0.21f), new Vector3(0.03f, 0.46f, 0.03f), darkMetal, false);
            Box(chair, "Thanh tựa", new Vector3(0.19f, 0.7f, -0.21f), new Vector3(0.03f, 0.46f, 0.03f), darkMetal, false);
        }

        /// <summary>Tủ hút khí độc: mặt trước nhìn về +x của hệ tọa độ riêng (xoay yaw để áp tường).</summary>
        static void FumeHood(Transform lab, Vector3 pos, float yaw, int index)
        {
            var g = Group($"Tủ hút khí độc {index}", lab, pos, yaw);
            const float w = 1.6f;
            Box(g, "Chân đế", new Vector3(0, 0.04f, 0), new Vector3(0.8f, 0.08f, w - 0.06f), darkMetal);
            Box(g, "Tủ dưới", new Vector3(0, 0.46f, 0), new Vector3(0.88f, 0.76f, w), cabinet);
            Box(g, "Cánh tủ dưới", new Vector3(0.445f, 0.46f, 0), new Vector3(0.01f, 0.68f, w - 0.1f), cabinetDoor, false);
            Box(g, "Mặt làm việc", new Vector3(0, 0.86f, 0), new Vector3(0.9f, 0.04f, w), epoxyDark);  // tủ hút giữ mặt epoxy chịu hóa chất màu tối
            Box(g, "Vách trái", new Vector3(0, 1.62f, -w / 2 + 0.02f), new Vector3(0.9f, 1.48f, 0.04f), cabinet);
            Box(g, "Vách phải", new Vector3(0, 1.62f, w / 2 - 0.02f), new Vector3(0.9f, 1.48f, 0.04f), cabinet);
            Box(g, "Vách sau", new Vector3(-0.43f, 1.62f, 0), new Vector3(0.04f, 1.48f, w - 0.08f), Mat("VachTuHut", new Color(0.86f, 0.88f, 0.88f), 0.4f));
            Box(g, "Hộp trên", new Vector3(0, 2.5f, 0), new Vector3(0.9f, 0.28f, w), cabinet);
            Box(g, "Diềm trước", new Vector3(0.43f, 2.2f, 0), new Vector3(0.04f, 0.32f, w - 0.08f), cabinet);
            NoShadow(Box(g, "Dải nhấn", new Vector3(0.452f, 2.33f, 0), new Vector3(0.004f, 0.025f, w - 0.1f), ledBlue, false));
            NoShadow(Box(g, "Đèn trạng thái", new Vector3(0.455f, 1.25f, w / 2 - 0.16f), new Vector3(0.006f, 0.03f, 0.08f), Mat("DenTrangThaiXanh", new Color(0.2f, 0.9f, 0.5f), 0.5f, 0, emission: new Color(0.3f, 1.8f, 0.8f)), false));
            Box(g, "Tay kéo cửa kính", new Vector3(0.45f, 1.42f, 0), new Vector3(0.04f, 0.04f, w - 0.08f), alu);
            NoShadow(Box(g, "Cửa kính kéo", new Vector3(0.44f, 1.72f, 0), new Vector3(0.01f, 0.58f, w - 0.1f), cabinetGlass, false));
            NoShadow(Box(g, "Đèn trong tủ", new Vector3(0, 2.34f, 0), new Vector3(0.3f, 0.02f, w - 0.3f), tubeGlow, false));
            Cyl(g, "Ống thoát khí", new Vector3(-0.15f, 3.12f, 0), 0.12f, 0.96f, steel);
            Label(g, $"TỦ HÚT KHÍ ĐỘC {index}", new Vector3(0.455f, 2.2f, 0), Vector3.right, 0.065f, new Color(0.05f, 0.3f, 0.36f));
            Box(g, "Công tắc tủ hút", new Vector3(0.455f, 1.1f, w / 2 - 0.16f), new Vector3(0.012f, 0.1f, 0.1f), Mat("NutXanh", new Color(0.1f, 0.6f, 0.4f), 0.4f));
            var zone = Group("Vùng làm việc tủ hút", g, new Vector3(0.02f, 1.45f, 0));
            var hz = zone.gameObject.AddComponent<FumeHoodZone>();
            hz.size = new Vector3(0.82f, 1.15f, w - 0.1f);
            fumeZones.Add(hz);
            Destination(g, $"Tủ hút khí độc {index}", 30 + index, new Vector3(0.05f, 0.88f, 0), new Vector2(0.7f, w - 0.15f), Vector3.right);
        }

        static readonly System.Collections.Generic.List<FumeHoodZone> fumeZones = new System.Collections.Generic.List<FumeHoodZone>();

        static void StudentBench(Transform p, string name, Vector3 center, int s, int kind)
        {
            // Hệ tọa độ bàn: x là bề sâu (học sinh ngồi phía +x, nhìn về bảng ở phía -x), z dọc bàn.
            // s = +1: bồn rửa ở đầu +z; s = -1: bồn ở đầu -z.
            var b = Group(name, p, center);
            const float top = 0.8f;
            Vector3 L(float x, float y, float z) => new Vector3(x, y, z * s);
            const float sinkZ0 = 0.74f;

            Box(b, "Chân đế", L(-0.24f, 0.04f, 0), new Vector3(0.36f, 0.08f, 2.5f), darkMetal);
            Box(b, "Tủ sau", L(-0.24f, 0.42f, (-1.27f + sinkZ0) / 2), new Vector3(0.4f, 0.68f, sinkZ0 + 1.27f), cabinet);
            HollowCabinet(b, "Tủ dưới bồn", L(-0.24f, 0.08f, (sinkZ0 + 1.27f) / 2), new Vector3(0.4f, 0.68f, 1.27f - sinkZ0));
            for (int i = 0; i < 3; i++)
            {
                float z = -1.0f + i * 0.62f;
                Box(b, $"Cánh tủ {i + 1}", L(-0.445f, 0.43f, z), new Vector3(0.01f, 0.6f, 0.58f), cabinetDoor, false);
                Box(b, $"Tay nắm {i + 1}", L(-0.457f, 0.62f, z + 0.2f), new Vector3(0.015f, 0.1f, 0.015f), handleBlue, false);
            }
            Box(b, "Cánh tủ dưới bồn", L(-0.445f, 0.3f, 1.0f), new Vector3(0.01f, 0.36f, 0.48f), cabinetDoor, false);
            foreach (float z in new[] { -1.25f, 1.25f })
                Box(b, "Chân bàn", L(0.41f, 0.39f, z), new Vector3(0.05f, 0.78f, 0.05f), steel);
            Box(b, "Đà trước", L(0.41f, 0.74f, 0), new Vector3(0.05f, 0.06f, 2.45f), steel, false);
            Box(b, "Tấm che chân", L(-0.035f, 0.45f, (-1.27f + sinkZ0) / 2), new Vector3(0.01f, 0.5f, sinkZ0 + 1.27f), cabinetDoor, false);
            NoShadow(Box(b, "Dải nhấn", L(0.452f, 0.775f, 0), new Vector3(0.004f, 0.02f, 2.6f), ledBlue, false));  // LED xanh mép bàn
            NoShadow(Box(b, "Đèn hắt gầm", L(-0.03f, 0.09f, (-1.27f + 0.74f) / 2), new Vector3(0.012f, 0.012f, 0.74f + 1.27f), ledBlue, false));

            TopWithSink(b, "Mặt bàn", 0.9f, 2.6f, top, new Vector2(-0.2f, 0.98f * s), new Vector2(0.36f, 0.42f), 0.2f);
            Faucet(b, "Vòi nước", L(-0.415f, top, 0.98f), Vector3.right, 0.32f, 0.2f);

            // Giá hóa chất dọc mép sau bàn, có ổ cắm điện và số bàn
            var shelf = Group("Giá hóa chất", b);
            foreach (float z in new[] { -1.21f, 0.61f })
                Box(shelf, "Trụ giá", L(-0.36f, 0.975f, z), new Vector3(0.16f, 0.35f, 0.02f), cabinet);
            Box(shelf, "Tầng giá", L(-0.36f, 1.14f, -0.3f), new Vector3(0.16f, 0.02f, 1.84f), cabinet);
            Box(shelf, "Ổ cắm điện", L(-0.282f, 0.92f, -0.3f), new Vector3(0.004f, 0.06f, 0.18f), signWhite, false);
            Label(shelf, name.Replace("Bàn thí nghiệm", "BÀN"), L(-0.28f, 1.22f, -1.0f), Vector3.right, 0.045f, new Color(0.05f, 0.3f, 0.36f));
            if (kind == 0)
            {
                for (int i = 0; i < 5; i++)
                    Bottle(shelf, $"Lọ hóa chất {i + 1}", L(-0.36f, 1.15f, -1.0f + i * 0.33f), i);
                TestTubeRack(b, L(-0.08f, top, -0.75f), 0f);
            }

            for (int i = 0; i < 3; i++)
                Stool(b, $"Ghế đẩu {i + 1}", L(0.85f, 0, -0.85f + i * 0.85f));

            if (kind == 1) TitrationSet(b, s);
            if (kind == 2) QualitativeSet(b, s);
            // bàn đích khu chế tạo: phần mặt bàn trước giá hóa chất, trừ bồn rửa
            int.TryParse(name.Substring(name.LastIndexOf(' ') + 1), out int num);
            Destination(b, name, 10 + num, L(0.08f, top, -0.27f), new Vector2(0.68f, 1.9f), Vector3.right);
        }

        /// <summary>Bồn rửa chung: mặt trước nhìn về -x của hệ tọa độ riêng, tường ở +x (xoay yaw để áp tường).</summary>
        static void WallSinkCounter(Transform lab, Vector3 pos, float yaw = 0)
        {
            var g = Group("Bồn rửa chung", lab, pos, yaw);
            const float top = 0.88f, len = 5.4f;
            Box(g, "Chân đế", new Vector3(0.02f, 0.04f, 0), new Vector3(0.5f, 0.08f, len - 0.04f), darkMetal);
            Box(g, "Tủ dưới", new Vector3(0, 0.34f, 0), new Vector3(0.58f, 0.52f, len), cabinet);
            Box(g, "Mặt dựng trước", new Vector3(-0.29f, 0.74f, 0), new Vector3(0.02f, 0.28f, len), cabinet);
            for (int i = 0; i < 8; i++)
                Box(g, $"Cánh tủ {i + 1}", new Vector3(-0.295f, 0.34f, -len / 2 + 0.34f + i * 0.675f), new Vector3(0.01f, 0.46f, 0.64f), cabinetDoor, false);
            float[] sinks = { -1.6f, 0f, 1.6f };
            float prev = -len / 2;
            foreach (float z in sinks)
            {
                float a = z - 0.25f;
                Box(g, "Mặt bàn đoạn", new Vector3(0, top - 0.02f, (prev + a) / 2), new Vector3(0.6f, 0.04f, a - prev), epoxy);
                Box(g, "Gờ trước bồn", new Vector3(-0.265f, top - 0.02f, z), new Vector3(0.07f, 0.04f, 0.5f), epoxy);
                Box(g, "Gờ sau bồn", new Vector3(0.255f, top - 0.02f, z), new Vector3(0.09f, 0.04f, 0.5f), epoxy);
                Basin(g, new Vector3(-0.005f, top, z), new Vector2(0.46f, 0.5f), 0.25f);
                Faucet(g, "Vòi nước", new Vector3(0.25f, top, z), Vector3.left, 0.32f, 0.2f);
                prev = z + 0.25f;
            }
            Box(g, "Mặt bàn đoạn", new Vector3(0, top - 0.02f, (prev + len / 2) / 2), new Vector3(0.6f, 0.04f, len / 2 - prev), epoxy);
            Box(g, "Ốp gạch", new Vector3(0.295f, top + 0.135f, 0), new Vector3(0.01f, 0.27f, len), tileWhite, false);

            // Giá treo dụng cụ thủy tinh giữa hai cửa sổ
            var rack = Group("Giá treo dụng cụ thủy tinh", g, new Vector3(0, 0, 1.0f));
            Box(rack, "Bảng lỗ", new Vector3(0.29f, 1.55f, -1.0f), new Vector3(0.02f, 0.4f, 0.9f), pegboard, false);
            Box(rack, "Khay hứng nước", new Vector3(0.23f, 1.34f, -1.0f), new Vector3(0.12f, 0.02f, 0.9f), steel, false);
            var tube = TestTube();
            int k = 0;
            foreach (float y in new[] { 1.45f, 1.62f })
                for (float z = -1.38f; z <= -0.62f; z += 0.13f)
                {
                    Vector3 basePos = new Vector3(0.28f, y, z);
                    Vector3 dir = Quaternion.Euler(0, 0, 75) * Vector3.up;
                    Cyl(rack, "Chốt", basePos + dir * 0.05f, 0.005f, 0.1f, whitePlastic, false, new Vector3(0, 0, 75));
                    if (++k % 2 == 0)
                        NoShadow(MeshObj(rack, "Ống nghiệm úp", tube, basePos + dir * 0.1f + new Vector3(0, 0.09f, 0), labGlass, false, new Vector3(180, 0, 0)));
                }
            Box(g, "Hộp giấy lau tay", new Vector3(0.24f, 1.45f, len / 2 - 0.3f), new Vector3(0.11f, 0.3f, 0.25f), whitePlastic, false);
            Bottle(g, "Chai nước rửa tay", new Vector3(0.18f, top, -len / 2 + 0.25f), 1);
        }

        static void SafetyStation(Transform lab)
        {
            var g = Group("Khu an toàn", lab);
            // Vòi rửa mắt khẩn cấp (tường nam, cạnh cửa ra vào)
            var eye = Group("Vòi rửa mắt", g, new Vector3(3.95f, 0, 0.42f), 90);
            Cyl(eye, "Đế", new Vector3(0, 0.01f, 0), 0.13f, 0.02f, safetyGreen, true);
            Cyl(eye, "Trụ", new Vector3(0, 0.5f, 0), 0.03f, 0.98f, safetyGreen, true);
            var bowl = Lathe("ChauRuaMat", 24, V(0, 0), V(0.03f, 0), V(0.03f, 0), V(0.12f, 0.06f), V(0.15f, 0.09f), V(0.155f, 0.095f),
                             V(0.145f, 0.095f), V(0.112f, 0.064f), V(0.024f, 0.008f), V(0, 0.008f));
            MeshObj(eye, "Chậu", bowl, new Vector3(0, 0.98f, 0), safetyGreen);
            foreach (float z in new[] { -0.05f, 0.05f })
                Cyl(eye, "Vòi phun", new Vector3(0, 1.06f, z), 0.012f, 0.03f, safetyGreen);
            Box(eye, "Cần gạt", new Vector3(-0.2f, 1.02f, 0), new Vector3(0.12f, 0.1f, 0.02f), safetyGreen, false);
            Box(g, "Biển vòi rửa mắt", new Vector3(3.95f, 1.85f, 0.106f), new Vector3(0.5f, 0.2f, 0.01f), safetyGreen, false);
            Label(g, "VÒI RỬA MẮT", new Vector3(3.95f, 1.85f, 0.115f), Vector3.forward, 0.06f, Color.white);

            // Vòi sen khẩn cấp
            var sh = Group("Vòi sen khẩn cấp", g, new Vector3(4.85f, 0, 0.5f));
            Cyl(sh, "Đế", new Vector3(0, 0.01f, 0), 0.16f, 0.02f, safetyGreen, true);
            Cyl(sh, "Trụ", new Vector3(0, 1.15f, -0.25f), 0.03f, 2.3f, safetyGreen, true);
            Cyl(sh, "Tay vươn", new Vector3(0, 2.28f, -0.05f), 0.025f, 0.42f, safetyGreen, false, new Vector3(90, 0, 0));
            MeshObj(sh, "Bát sen", Lathe("BatSen", 24, V(0, 0), V(0.16f, -0.05f), V(0.17f, -0.06f), V(0.0f, -0.02f)), new Vector3(0, 2.27f, 0.15f), safetyGreen);
            Cyl(sh, "Dây kéo", new Vector3(0.12f, 1.85f, 0.15f), 0.004f, 0.75f, safetyYellow);
            Ball(sh, "Vòng kéo", new Vector3(0.12f, 1.47f, 0.15f), new Vector3(0.06f, 0.06f, 0.012f), safetyYellow);
            Box(g, "Biển vòi sen", new Vector3(4.85f, 2.6f, 0.106f), new Vector3(0.6f, 0.2f, 0.01f), safetyGreen, false);
            Label(g, "VÒI SEN KHẨN CẤP", new Vector3(4.85f, 2.6f, 0.115f), Vector3.forward, 0.05f, Color.white);

            Extinguisher(g, new Vector3(5.75f, 0, 0.22f), Vector3.forward);
            Box(g, "Hộp y tế", new Vector3(6.35f, 1.6f, 0.17f), new Vector3(0.36f, 0.3f, 0.1f), signWhite);
            Box(g, "Chữ thập ngang", new Vector3(6.35f, 1.6f, 0.221f), new Vector3(0.16f, 0.05f, 0.002f), red, false);
            Box(g, "Chữ thập dọc", new Vector3(6.35f, 1.6f, 0.221f), new Vector3(0.05f, 0.16f, 0.002f), red, false);
            Label(g, "HỘP Y TẾ", new Vector3(6.35f, 1.86f, 0.115f), Vector3.forward, 0.04f, new Color(0.7f, 0.05f, 0.05f));
            var bucket = Lathe("XoCat", 24, V(0, 0), V(0.12f, 0), V(0.12f, 0), V(0.15f, 0.28f), V(0.155f, 0.29f),
                               V(0.145f, 0.29f), V(0.112f, 0.01f), V(0, 0.01f));
            MeshObj(g, "Xô cát", bucket, new Vector3(6.35f, 0, 0.4f), red, true);
            Cyl(g, "Cát", new Vector3(6.35f, 0.25f, 0.4f), 0.14f, 0.01f, sand);

            // Tủ đồ bảo hộ và giá áo blouse (tường tây, phía nam)
            var ppe = Group("Tủ đồ bảo hộ", g, new Vector3(0.3f, 0, 2.6f));
            Box(ppe, "Thân tủ", new Vector3(0, 0.95f, 0), new Vector3(0.4f, 1.9f, 1.0f), cabinet);
            NoShadow(Box(ppe, "Cửa kính", new Vector3(0.205f, 1.15f, 0), new Vector3(0.008f, 1.3f, 0.94f), cabinetGlass, false));
            Label(ppe, "ĐỒ BẢO HỘ", new Vector3(0.21f, 1.98f, 0), Vector3.right, 0.055f, new Color(0.05f, 0.3f, 0.36f));
            for (int i = 0; i < 3; i++)
            {
                float y = 0.75f + i * 0.4f;
                Box(ppe, "Tầng tủ", new Vector3(0, y - 0.02f, 0), new Vector3(0.36f, 0.015f, 0.94f), cabinet, false);
            }
            for (int j = 0; j < 4; j++) Goggles(ppe, new Vector3(0.05f, 1.55f, -0.33f + j * 0.22f));
            for (int j = 0; j < 3; j++) GloveBox(ppe, new Vector3(0.04f, 1.15f, -0.3f + j * 0.3f), j);
            CoatRack(lab, new Vector3(0.85f, 0, 0.1f), 0f);  // tường nam, cạnh cửa ra vào

            var rule = Group("Nội quy", g, new Vector3(0.11f, 1.5f, 1.45f));
            Box(rule, "Khung nội quy", Vector3.zero, new Vector3(0.01f, 0.7f, 0.5f), signWhite, false);
            Label(rule, "NỘI QUY PHÒNG THÍ NGHIỆM", new Vector3(0.008f, 0.29f, 0), Vector3.right, 0.026f, new Color(0.7f, 0.05f, 0.05f));
            Label(rule, "1. Mặc áo blouse, đeo kính\n2. Không ăn uống\n3. Đọc nhãn trước khi dùng\n4. Khí độc làm trong tủ hút\n5. Pha axit: rót axit vào nước\n6. Sự cố: báo ngay giáo viên",
                  new Vector3(0.008f, 0.02f, 0), Vector3.right, 0.022f, new Color(0.15f, 0.15f, 0.2f));
        }

        static void Goggles(Transform p, Vector3 pos)
        {
            var g = Group("Kính bảo hộ", p, pos);
            NoShadow(Box(g, "Mắt kính", new Vector3(0, 0.025f, 0), new Vector3(0.07f, 0.05f, 0.16f), cabinetGlass));
            Box(g, "Gọng", new Vector3(0, 0.025f, 0), new Vector3(0.075f, 0.055f, 0.012f), blackPlastic, false);
            Box(g, "Dây đeo", new Vector3(-0.03f, 0.025f, 0), new Vector3(0.01f, 0.02f, 0.17f), accentDark, false);
        }

        static void GloveBox(Transform p, Vector3 pos, int size)
        {
            var g = Group("Găng tay cao su", p, pos);
            Box(g, "Hộp găng", new Vector3(0, 0.06f, 0), new Vector3(0.12f, 0.12f, 0.24f), Mat("HopGang", new Color(0.3f, 0.55f, 0.85f), 0.3f));
            Box(g, "Găng nhô ra", new Vector3(0.055f, 0.12f, 0), new Vector3(0.03f, 0.03f, 0.08f), Mat("GangXanh", new Color(0.45f, 0.65f, 0.95f), 0.3f), false);
            Label(g, size == 0 ? "S" : size == 1 ? "M" : "L", new Vector3(0.061f, 0.06f, 0.07f), Vector3.right, 0.03f, Color.white);
        }

        static void WasteStation(Transform p, Vector3 pos)
        {
            var g = Group("Khu chất thải", p, pos);
            float[] sizes = { 0.12f, 0.15f, 0.19f };
            string[] text = { "5 L", "10 L", "20 L" };
            for (int i = 0; i < 3; i++)
            {
                var c = Group("Bình chứa chất thải", g, new Vector3(0, 0, -0.5f + i * 0.5f));
                var m = Lathe("CanThai" + i, 20, V(0, 0), V(sizes[i], 0), V(sizes[i], 0), V(sizes[i], sizes[i] * 3f), V(sizes[i] * 0.4f, sizes[i] * 3.3f), V(0, sizes[i] * 3.3f));
                MeshObj(c, "Can thải", m, Vector3.zero, safetyYellow, true);
                Cyl(c, "Nắp", new Vector3(0, sizes[i] * 3.35f, 0), 0.035f, 0.04f, blackPlastic);
                Label(c, "CHẤT THẢI\n" + text[i], new Vector3(-sizes[i] - 0.002f, sizes[i] * 1.6f, 0), Vector3.left, 0.03f, Color.black);
            }
            Label(g, i18("CHẤT THẢI HÓA HỌC · phân loại: axit / bazơ / kim loại nặng"), new Vector3(0.3f, 1.1f, 0), Vector3.left, 0.035f, new Color(0.6f, 0.4f, 0f));
            Box(g, "Biển chất thải", new Vector3(0.31f, 1.1f, 0), new Vector3(0.005f, 0.12f, 1.4f), signWhite, false);
        }

        static string i18(string s) => s;

        static void Extinguisher(Transform p, Vector3 floorPos, Vector3 facing)
        {
            var g = Group("Bình chữa cháy", p, floorPos);
            var body = Lathe("BinhCuuHoa", 24, V(0, 0), V(0.075f, 0), V(0.075f, 0), V(0.078f, 0.01f), V(0.078f, 0.42f),
                             V(0.06f, 0.48f), V(0.025f, 0.5f), V(0, 0.5f));
            MeshObj(g, "Thân bình", body, new Vector3(0, 0.4f, 0), red, true);
            Cyl(g, "Van", new Vector3(0, 0.93f, 0), 0.02f, 0.06f, darkMetal);
            Box(g, "Tay bóp", new Vector3(0, 0.97f, 0) + facing * 0.04f, new Vector3(0.03f, 0.015f, 0.03f) + Abs(facing) * 0.08f, darkMetal, false);
            Cyl(g, "Vòi", new Vector3(0, 0.75f, 0) + Vector3.Cross(Vector3.up, facing) * 0.085f, 0.008f, 0.35f, rubber);
            Box(g, "Biển bình chữa cháy", new Vector3(0, 1.2f, 0) - facing * 0.07f, new Vector3(0.36f, 0.14f, 0.36f) - Abs(facing) * 0.35f, red, false);
            Label(g, "BÌNH CHỮA CHÁY", new Vector3(0, 1.2f, 0) - facing * 0.063f, facing, 0.04f, Color.white);
        }

        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        static void CoatRack(Transform lab, Vector3 wallPos, float yaw = 90f)
        {
            // Giá treo áo blouse áp tường (yaw 90: tường tây, 0: tường nam)
            var g = Group("Giá treo áo blouse", lab, wallPos, yaw);
            Box(g, "Thanh gỗ", new Vector3(0, 1.75f, 0.015f), new Vector3(1.4f, 0.12f, 0.03f), oak, false);
            for (int i = 0; i < 5; i++)
            {
                float x = -0.56f + i * 0.28f;
                Cyl(g, "Móc", new Vector3(x, 1.72f, 0.07f), 0.008f, 0.08f, steel, false, new Vector3(90, 0, 0));
                if (i % 2 == 0)
                {
                    Box(g, "Áo blouse", new Vector3(x, 1.22f, 0.09f), new Vector3(0.46f, 0.95f, 0.06f), coat, false, new Vector3(-3, 0, 0));
                    Box(g, "Cổ áo", new Vector3(x, 1.68f, 0.1f), new Vector3(0.2f, 0.06f, 0.07f), coat, false);
                }
            }
            Label(g, "ÁO CHOÀNG THÍ NGHIỆM", new Vector3(0, 1.9f, 0.012f), Vector3.forward, 0.05f, new Color(0.05f, 0.3f, 0.36f));
        }

        // =====================================================================
        // Ban công và ngoài trời
        // =====================================================================

        static void BuildBalcony(Transform root)
        {
            // Hiên trước (nhà một tầng): sàn ngang mặt đất, cột, chậu cây, ghế, đèn ốp trần
            var g = Group("Hiên", root);
            float zEdge = BalconyZ - 0.08f;
            foreach (float x in new[] { 0.15f, 9.0f, 18.0f, StoreX - 0.15f })
                Box(g, "Cột hiên", new Vector3(x, H / 2, zEdge - 0.07f), new Vector3(0.3f, H, 0.3f), facade);
            foreach (float x in new[] { 3.0f, 7.0f, 11.5f, 15.5f, 20.5f, 24.5f })
                PottedPlant(g, new Vector3(x, 0, zEdge - 0.3f));
            foreach (float x in new[] { 5.0f, 13.5f })
            {
                var bench = Group("Ghế băng", g, new Vector3(x, 0, LabZ + 0.4f));
                Box(bench, "Mặt ghế", new Vector3(0, 0.45f, 0), new Vector3(1.4f, 0.04f, 0.38f), oak);
                foreach (float bx in new[] { -0.6f, 0.6f })
                    Box(bench, "Chân ghế", new Vector3(bx, 0.22f, 0), new Vector3(0.05f, 0.44f, 0.34f), darkMetal);
            }
            foreach (float x in new[] { 4.5f, 13.5f, 22.5f })
            {
                var lamp = Lathe("DenOpTran", 24, V(0, 0), V(0.12f, 0.01f), V(0.16f, 0.05f), V(0.16f, 0.06f), V(0, 0.06f));
                NoShadow(MeshObj(g, "Đèn ốp trần", lamp, new Vector3(x, H - 0.06f, (LabZ + BalconyZ) / 2), tubeGlow));
                PointLight(g, "Đèn hiên", new Vector3(x, H - 0.25f, (LabZ + BalconyZ) / 2), 4f, 1.2f, new Color(1f, 0.95f, 0.85f));
            }
        }
        static void Railing(Transform p, Vector3 from, Vector3 to)
        {
            var g = Group("Lan can", p);
            Vector3 d = to - from;
            float len = d.magnitude;
            bool alongX = Mathf.Abs(d.x) > Mathf.Abs(d.z);
            Vector3 mid = (from + to) / 2;
            int posts = Mathf.CeilToInt(len / 0.8f);
            for (int i = 0; i <= posts; i++)
                Box(g, "Trụ lan can", from + d * i / posts + Vector3.up * 0.4f, new Vector3(0.035f, 0.8f, 0.035f), darkMetal, false);
            Box(g, "Tay vịn", mid + Vector3.up * 0.82f, alongX ? new Vector3(len, 0.05f, 0.09f) : new Vector3(0.09f, 0.05f, len), oak);
            Box(g, "Thanh giữa", mid + Vector3.up * 0.42f, alongX ? new Vector3(len, 0.02f, 0.02f) : new Vector3(0.02f, 0.02f, len), darkMetal, false);
            var block = new GameObject("Chắn va chạm");
            block.transform.SetParent(g, false);
            block.transform.localPosition = mid + Vector3.up * 0.6f;
            var c = block.AddComponent<BoxCollider>();
            c.size = alongX ? new Vector3(len, 1.6f, 0.1f) : new Vector3(0.1f, 1.6f, len);
        }

        static void PottedPlant(Transform p, Vector3 pos)
        {
            var g = Group("Chậu cây", p, pos);
            var potMesh = Lathe("ChauCay", 24, V(0, 0), V(0.13f, 0), V(0.13f, 0), V(0.17f, 0.3f), V(0.19f, 0.31f), V(0.19f, 0.34f), V(0, 0.34f));
            MeshObj(g, "Chậu", potMesh, Vector3.zero, pot, true);
            Ball(g, "Tán lá", new Vector3(0, 0.6f, 0), new Vector3(0.45f, 0.5f, 0.45f), foliage);
            Ball(g, "Tán lá", new Vector3(0.1f, 0.78f, 0.05f), new Vector3(0.3f, 0.35f, 0.3f), foliageDark);
            Ball(g, "Tán lá", new Vector3(-0.1f, 0.72f, -0.06f), new Vector3(0.28f, 0.3f, 0.28f), foliage);
        }

        static void BuildOutdoor(Transform root)
        {
            var g = Group("Ngoài trời", root);
            float cx = StoreX / 2;
            Box(g, "Nền cỏ", new Vector3(cx, -0.13f, 20), new Vector3(220, 0.2f, 220), grass);
            Box(g, "Sân trường", new Vector3(cx, -0.04f, 28), new Vector3(46, 0.05f, 22), concrete);
            var treePos = new[] { V(-6, 24), V(-2, 31), V(21.5f, 24.5f), V(31, 23), V(-7, 37), V(33, 34), V(4.5f, 39), V(24, 40) };
            int i = 0;
            foreach (var t in treePos)
            {
                var tree = Group($"Cây {++i}", g, new Vector3(t.x, 0f, t.y));
                float s = 0.85f + 0.3f * (float)rnd.NextDouble();
                Cyl(tree, "Thân", new Vector3(0, 1.6f * s, 0), 0.18f * s, 3.2f * s, trunk);
                Ball(tree, "Tán", new Vector3(0, 4.0f * s, 0), new Vector3(3.6f, 3.0f, 3.6f) * s, foliage);
                Ball(tree, "Tán", new Vector3(0.8f, 4.6f * s, 0.5f), new Vector3(2.4f, 2.2f, 2.4f) * s, foliageDark);
                Ball(tree, "Tán", new Vector3(-0.7f, 4.4f * s, -0.4f), new Vector3(2.2f, 2.0f, 2.2f) * s, foliage);
            }
            var pole = Group("Cột cờ", g, new Vector3(cx, 0f, 29));
            Cyl(pole, "Bệ", new Vector3(0, 0.15f, 0), 0.6f, 0.3f, concrete, true);
            Cyl(pole, "Cột", new Vector3(0, 4.3f, 0), 0.04f, 8f, steel, true);
            Panel(pole, "Cờ (mặt trước)", new Vector3(0.65f, 7.7f, -0.005f), Vector3.back, 1.2f, 0.8f, flag);
            Panel(pole, "Cờ (mặt sau)", new Vector3(0.65f, 7.7f, 0.005f), Vector3.forward, 1.2f, 0.8f, flag);
        }

        // =====================================================================
        // Đồ vật dùng nhiều lần
        // =====================================================================

        /// <summary>Mặt bàn có lỗ khoét đặt bồn rửa inox. hc, hs: tâm và kích thước lỗ (x, z) trong hệ tọa độ bàn.</summary>
        static void TopWithSink(Transform p, string name, float sx, float sz, float y, Vector2 hc, Vector2 hs, float depth)
        {
            var g = Group(name, p);
            float x0 = -sx / 2, x1 = sx / 2, z0 = -sz / 2, z1 = sz / 2;
            float hx0 = hc.x - hs.x / 2, hx1 = hc.x + hs.x / 2, hz0 = hc.y - hs.y / 2, hz1 = hc.y + hs.y / 2;
            void Piece(float a, float b, float c, float d)
            {
                if (b - a > 0.001f && d - c > 0.001f)
                    Box(g, "Mặt bàn", new Vector3((a + b) / 2, y - 0.02f, (c + d) / 2), new Vector3(b - a, 0.04f, d - c), epoxy);
            }
            Piece(x0, x1, z0, hz0);
            Piece(x0, x1, hz1, z1);
            Piece(x0, hx0, hz0, hz1);
            Piece(hx1, x1, hz0, hz1);
            Basin(g, new Vector3(hc.x, y, hc.y), hs, depth);
        }

        /// <summary>Hộc tủ rỗng ruột (đáy + 4 vách) để bồn rửa nằm lọt bên trong. bottom: tâm đáy.</summary>
        static void HollowCabinet(Transform p, string name, Vector3 bottom, Vector3 size)
        {
            var g = Group(name, p, bottom);
            const float t = 0.018f;
            Box(g, "Đáy", new Vector3(0, t / 2, 0), new Vector3(size.x, t, size.z), cabinet);
            Box(g, "Vách", new Vector3(-size.x / 2 + t / 2, size.y / 2, 0), new Vector3(t, size.y, size.z), cabinet);
            Box(g, "Vách", new Vector3(size.x / 2 - t / 2, size.y / 2, 0), new Vector3(t, size.y, size.z), cabinet);
            Box(g, "Vách", new Vector3(0, size.y / 2, -size.z / 2 + t / 2), new Vector3(size.x, size.y, t), cabinet);
            Box(g, "Vách", new Vector3(0, size.y / 2, size.z / 2 - t / 2), new Vector3(size.x, size.y, t), cabinet);
        }

        static void Basin(Transform p, Vector3 rim, Vector2 size, float depth)
        {
            var g = Group("Bồn rửa", p, rim);
            const float t = 0.008f;
            Box(g, "Đáy bồn", new Vector3(0, -depth, 0), new Vector3(size.x, t, size.y), steel);
            Box(g, "Thành bồn", new Vector3(-size.x / 2, -depth / 2, 0), new Vector3(t, depth, size.y), steel, false);
            Box(g, "Thành bồn", new Vector3(size.x / 2, -depth / 2, 0), new Vector3(t, depth, size.y), steel, false);
            Box(g, "Thành bồn", new Vector3(0, -depth / 2, -size.y / 2), new Vector3(size.x, depth, t), steel, false);
            Box(g, "Thành bồn", new Vector3(0, -depth / 2, size.y / 2), new Vector3(size.x, depth, t), steel, false);
            Cyl(g, "Lỗ thoát nước", new Vector3(0, -depth + 0.006f, 0), 0.025f, 0.004f, darkMetal);
        }

        /// <summary>Vòi nước cổ ngỗng: chân đặt trên mặt bàn tại basePos, vươn về hướng dir.</summary>
        static void Faucet(Transform p, string name, Vector3 basePos, Vector3 dir, float height, float reach)
        {
            var g = Group(name, p, basePos);
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            Vector3 armEuler = Mathf.Abs(dir.x) > 0.5f ? new Vector3(0, 0, 90) : new Vector3(90, 0, 0);
            Cyl(g, "Thân vòi", Vector3.up * height / 2, 0.012f, height, steel);
            Cyl(g, "Cổ vòi", Vector3.up * height + dir * reach / 2, 0.011f, reach, steel, false, armEuler);
            Cyl(g, "Miệng vòi", Vector3.up * (height - 0.03f) + dir * reach, 0.009f, 0.06f, steel);
            Box(g, "Tay vặn nóng", Vector3.up * 0.13f + side * 0.05f, new Vector3(0.025f, 0.02f, 0.025f) + Abs(side) * 0.04f, redPlastic, false);
            Box(g, "Tay vặn lạnh", Vector3.up * 0.13f - side * 0.05f, new Vector3(0.025f, 0.02f, 0.025f) + Abs(side) * 0.04f, bluePlastic, false);
        }

        static void Stool(Transform p, string name, Vector3 pos)
        {
            var g = Group(name, p, pos);
            // mặt ghế nhiều màu (theo vị trí, cố định giữa các lần dựng)
            var w = g.position;
            var seat = stoolMats[(int)(Hash(Mathf.RoundToInt(w.x * 10), Mathf.RoundToInt(w.z * 10)) % (uint)stoolMats.Length)];
            Cyl(g, "Mặt ghế", new Vector3(0, 0.5f, 0), 0.17f, 0.04f, seat);
            foreach (var c in new[] { V(0.11f, 0.11f), V(-0.11f, 0.11f), V(0.11f, -0.11f), V(-0.11f, -0.11f) })
                Cyl(g, "Chân ghế", new Vector3(c.x, 0.24f, c.y), 0.012f, 0.48f, steel);
            Box(g, "Thanh gác chân", new Vector3(0, 0.2f, 0.11f), new Vector3(0.22f, 0.012f, 0.012f), steel, false);
            Box(g, "Thanh gác chân", new Vector3(0, 0.2f, -0.11f), new Vector3(0.22f, 0.012f, 0.012f), steel, false);
            Box(g, "Thanh gác chân", new Vector3(0.11f, 0.2f, 0), new Vector3(0.012f, 0.012f, 0.22f), steel, false);
            Box(g, "Thanh gác chân", new Vector3(-0.11f, 0.2f, 0), new Vector3(0.012f, 0.012f, 0.22f), steel, false);
        }

        /// <summary>Lọ hóa chất (trang trí). type: 0 chai nâu, 1 chai nhựa trắng, 2 chai nhỏ giọt, 3 lọ rộng miệng, 4 chai trong có màu.</summary>
        static void Bottle(Transform p, string name, Vector3 pos, int type)
        {
            switch (type)
            {
                case 0:
                {
                    var m = ReagentBottle();
                    MeshObj(p, name + " (chai nâu)", m, pos, amber, true);
                    Cyl(p, "Nắp", pos + Vector3.up * 0.16f, 0.015f, 0.02f, blackPlastic);
                    break;
                }
                case 1:
                {
                    var m = Lathe("ChaiNhua", 20, V(0, 0), V(0.035f, 0), V(0.035f, 0), V(0.036f, 0.004f), V(0.036f, 0.15f), V(0.02f, 0.175f),
                                  V(0.012f, 0.18f), V(0, 0.18f));
                    MeshObj(p, name + " (chai nhựa)", m, pos, whitePlastic, true);
                    Cyl(p, "Nắp", pos + Vector3.up * 0.19f, 0.013f, 0.02f, bluePlastic);
                    break;
                }
                case 2:
                {
                    var m = DropperBottleMesh();
                    MeshObj(p, name + " (chai nhỏ giọt)", m, pos, amber, true);
                    Cyl(p, "Núm cao su", pos + Vector3.up * 0.075f, 0.007f, 0.028f, rubber);
                    break;
                }
                case 3:
                {
                    var m = WideJar();
                    MeshObj(p, name + " (lọ rộng miệng)", m, pos, whitePlastic, true);
                    Cyl(p, "Nắp", pos + Vector3.up * 0.135f, 0.037f, 0.025f, (pos.z > 0) ? redPlastic : blackPlastic);
                    break;
                }
                default:
                {
                    var m = ReagentBottle();
                    NoShadow(MeshObj(p, name + " (chai trong)", m, pos, labGlass, true));
                    var liquid = LiquidIn("DichChai", 0.03f, 0.09f);
                    Material[] tints = { liquidBlue, liquidPink, liquidClear };
                    NoShadow(MeshObj(p, "Dung dịch", liquid, pos, tints[rnd.Next(tints.Length)]));
                    Cyl(p, "Nắp", pos + Vector3.up * 0.16f, 0.015f, 0.02f, whitePlastic);
                    break;
                }
            }
        }

        static Mesh DropperBottleMesh() => Lathe("ChaiNhoGiot", 16, V(0, 0), V(0.017f, 0), V(0.017f, 0), V(0.018f, 0.003f), V(0.018f, 0.05f), V(0.012f, 0.058f),
                                                 V(0.007f, 0.062f), V(0, 0.062f));

        static Mesh WideJar() => Lathe("LoRongMieng", 20, V(0, 0), V(0.038f, 0), V(0.038f, 0), V(0.04f, 0.004f), V(0.04f, 0.11f), V(0.034f, 0.12f),
                                       V(0.034f, 0.125f), V(0, 0.125f));

        static void Jug(Transform p, Vector3 pos)
        {
            var m = Lathe("Can", 20, V(0, 0), V(0.085f, 0), V(0.085f, 0), V(0.09f, 0.01f), V(0.09f, 0.24f), V(0.06f, 0.28f),
                          V(0.025f, 0.3f), V(0.025f, 0.33f), V(0, 0.33f));
            MeshObj(p, "Can nhựa", m, pos, whitePlastic, true);
            Cyl(p, "Nắp can", pos + Vector3.up * 0.345f, 0.028f, 0.03f, redPlastic);
        }

        static Mesh Erlenmeyer() => Lathe("BinhNon250", 24, V(0, 0), V(0.042f, 0), V(0.042f, 0), V(0.045f, 0.005f), V(0.044f, 0.02f),
                                          V(0.017f, 0.1f), V(0.016f, 0.105f), V(0.016f, 0.14f), V(0.0175f, 0.143f));

        static Mesh ReagentBottle() => Lathe("ChaiThuoc", 20, V(0, 0), V(0.032f, 0), V(0.032f, 0), V(0.033f, 0.003f), V(0.033f, 0.1f),
                                             V(0.03f, 0.115f), V(0.014f, 0.13f), V(0.013f, 0.135f), V(0.013f, 0.15f), V(0, 0.15f));

        static Mesh TestTube() => Lathe("OngNghiem", 16, V(0, 0), V(0.004f, 0.001f), V(0.007f, 0.004f), V(0.008f, 0.008f),
                                        V(0.008f, 0.15f), V(0.0085f, 0.152f));

        static Mesh Beaker(string name, float r, float h) =>
            Lathe(name, 24, V(0, 0), V(r, 0), V(r, 0), V(r, h), V(r + 0.0015f, h + 0.002f));

        static Mesh LiquidIn(string name, float r, float h) =>
            Lathe(name, 24, V(0, 0.002f), V(r, 0.002f), V(r, 0.002f), V(r, h), V(r, h), V(0, h));

        static Mesh Cylinder(string name, float r, float h) =>
            Lathe(name, 16, V(0, 0), V(r * 2.2f, 0), V(r * 2.2f, 0.006f), V(r, 0.006f), V(r, 0.006f), V(r, h), V(r * 1.15f, h + 0.004f));

        static Mesh RoundFlask() =>
            Lathe("BinhCau", 24, V(0, 0), V(0.02f, 0.002f), V(0.038f, 0.015f), V(0.045f, 0.045f), V(0.038f, 0.075f), V(0.02f, 0.088f),
                  V(0.013f, 0.095f), V(0.013f, 0.15f), V(0.015f, 0.152f));

        static Mesh VolumetricFlask() =>
            Lathe("BinhDinhMuc", 24, V(0, 0), V(0.04f, 0), V(0.04f, 0), V(0.045f, 0.02f), V(0.04f, 0.05f), V(0.02f, 0.075f),
                  V(0.009f, 0.09f), V(0.009f, 0.22f), V(0.011f, 0.222f));

        static void TestTubeRack(Transform p, Vector3 pos, float yaw)
        {
            var g = Group("Giá ống nghiệm", p, pos, yaw);
            Box(g, "Đế giá", new Vector3(0, 0.005f, 0), new Vector3(0.09f, 0.01f, 0.26f), oak, true);
            Box(g, "Thành giá", new Vector3(0, 0.045f, -0.125f), new Vector3(0.09f, 0.08f, 0.01f), oak, false);
            Box(g, "Thành giá", new Vector3(0, 0.045f, 0.125f), new Vector3(0.09f, 0.08f, 0.01f), oak, false);
            Box(g, "Tấm đỡ có lỗ", new Vector3(0, 0.07f, 0), new Vector3(0.09f, 0.008f, 0.26f), oak, false);
            var tube = TestTube();
            for (int i = 0; i < 6; i++)
            {
                var at = new Vector3(0, 0.012f, -0.1f + i * 0.04f);
                NoShadow(MeshObj(g, "Ống nghiệm", tube, at, labGlass));
                if (i == 1) NoShadow(MeshObj(g, "Dung dịch CuSO4", LiquidIn("DichOngNghiem", 0.0075f, 0.05f), at, liquidBlue));
            }
        }

        static void WashBottle(Transform p, Vector3 pos)
        {
            var m = Lathe("BinhTia", 20, V(0, 0), V(0.035f, 0), V(0.035f, 0), V(0.036f, 0.004f), V(0.036f, 0.16f), V(0.02f, 0.18f),
                          V(0.012f, 0.185f), V(0, 0.185f));
            MeshObj(p, "Bình tia nước cất", m, pos, whitePlastic, true);
            Cyl(p, "Vòi bình tia", pos + new Vector3(0.02f, 0.215f, 0), 0.003f, 0.08f, whitePlastic, false, new Vector3(0, 0, -55));
        }

        static void AlcoholLamp(Transform p, Vector3 pos)
        {
            var m = Lathe("DenCon", 20, V(0, 0), V(0.035f, 0), V(0.035f, 0), V(0.04f, 0.02f), V(0.035f, 0.05f), V(0.012f, 0.065f),
                          V(0.012f, 0.075f), V(0, 0.075f));
            NoShadow(MeshObj(p, "Đèn cồn", m, pos, labGlass, true));
            NoShadow(MeshObj(p, "Cồn trong đèn", LiquidIn("DichDenCon", 0.03f, 0.03f), pos, liquidClear));
            Cyl(p, "Nắp đèn cồn", pos + Vector3.up * 0.085f, 0.014f, 0.022f, whitePlastic);
        }

        static void Laptop(Transform p, Vector3 pos)
        {
            var g = Group("Máy tính xách tay", p, pos, -90);
            Box(g, "Thân máy", new Vector3(0, 0.01f, 0), new Vector3(0.34f, 0.02f, 0.24f), darkMetal, true);
            var lid = Group("Nắp màn hình", g, new Vector3(0, 0.02f, -0.12f));
            lid.localRotation = Quaternion.Euler(-105, 0, 0);
            Box(lid, "Nắp", new Vector3(0, 0, 0.12f), new Vector3(0.34f, 0.012f, 0.24f), darkMetal, false);
            Box(lid, "Màn hình", new Vector3(0, -0.0065f, 0.12f), new Vector3(0.31f, 0.002f, 0.2f), screen, false);
        }

        static void ExhaustFan(Transform p, Vector3 pos)
        {
            // Quạt hút gắn tường đông, trục quay theo x
            var g = Group("Quạt hút", p, pos);
            Box(g, "Khung trên", new Vector3(-0.1f, 0.24f, 0), new Vector3(0.2f, 0.04f, 0.52f), whitePlastic);
            Box(g, "Khung dưới", new Vector3(-0.1f, -0.24f, 0), new Vector3(0.2f, 0.04f, 0.52f), whitePlastic);
            Box(g, "Khung trái", new Vector3(-0.1f, 0, -0.24f), new Vector3(0.2f, 0.52f, 0.04f), whitePlastic);
            Box(g, "Khung phải", new Vector3(-0.1f, 0, 0.24f), new Vector3(0.2f, 0.52f, 0.04f), whitePlastic);
            Cyl(g, "Động cơ", new Vector3(-0.05f, 0, 0), 0.06f, 0.08f, darkMetal, false, new Vector3(0, 0, 90));
            var rotor = Group("Cánh quạt hút", g, new Vector3(-0.1f, 0, 0));
            var fan = rotor.gameObject.AddComponent<CeilingFan>();
            fan.localAxis = Vector3.right;
            fan.rpm = 240f;
            for (int i = 0; i < 4; i++)
            {
                var blade = new GameObject($"Cánh {i + 1}").transform;
                blade.SetParent(rotor, false);
                blade.localRotation = Quaternion.Euler(i * 90f, 0, 0);
                Box(blade, "Cánh", new Vector3(0, 0.11f, 0), new Vector3(0.01f, 0.17f, 0.07f), whitePlastic, false, new Vector3(0, 25, 0));
            }
            for (int i = 0; i < 5; i++)
                Box(g, "Lưới chắn", new Vector3(-0.2f, -0.2f + i * 0.1f, 0), new Vector3(0.005f, 0.006f, 0.44f), darkMetal, false);
            Label(g, "QUẠT HÚT", new Vector3(-0.06f, -0.36f, 0), Vector3.left, 0.05f, new Color(0.05f, 0.3f, 0.36f));
        }
    }
}
