using Khkt.Environment.Tang2;
using UnityEngine;
using static Khkt.EditorTools.LabKit;

namespace Khkt.EditorTools
{
    /// <summary>
    /// Các phòng chuyên đề tầng 2: phòng vi sinh (vùng sạch có buồng đệm), kho sinh phẩm, hành lang tầng 2, phòng sinh học phân tử
    /// (luồng một chiều tiền PCR → tách chiết → PCR, điện di), nhà kính thực vật, hành lang đông. Tọa độ trong tầng 2.
    /// </summary>
    public static partial class LabSceneBuilder
    {
        // =====================================================================
        // Phòng vi sinh (x 20..27, z 7,5..15), buồng đệm x 25,2..27, z 7,5..10
        // =====================================================================

        static void BuildMicrobiologyRoom(Transform f2)
        {
            var room = Group("Phòng vi sinh", f2);
            var lights = Group("Đèn", room);
            RoomLights(lights, new[] { 22.6f }, new[] { 10.4f, 13.2f }, 8f, 3.0f);
            foreach (var (x, z) in new[] { (21.6f, 10.4f), (23.6f, 10.4f), (21.6f, 13.2f), (23.6f, 13.2f), (26.1f, 12.4f) })
                LedPanel(lights, "Đèn kín bụi", x, z, 2f, 6f, false);
            LedPanel(lights, "Đèn buồng đệm", 26.1f, 8.75f, 1.6f, 4.5f);
            // đèn UV diệt khuẩn gắn trần (đang tắt, chỉ bật khi không có người)
            foreach (float x in new[] { 22.0f, 24.4f })
            {
                var uv = Group("Đèn UV diệt khuẩn", lights, new Vector3(x, F2_H - 0.06f, 12.0f));
                Box(uv, "Máng đèn", Vector3.zero, new Vector3(1.25f, 0.06f, 0.12f), bioWhite, false);
                Cyl(uv, "Bóng UV (tắt)", new Vector3(0, -0.04f, 0), 0.014f, 1.15f, Mat("T2_BongUvTat", new Color(0.78f, 0.74f, 0.9f), 0.8f), false, new Vector3(0, 0, 90));
            }
            Box(room, "Biển đèn UV", new Vector3(23.2f, 2.5f, LabZ - 0.105f), new Vector3(0.9f, 0.16f, 0.006f), bioBiohazard, false);
            Label(room, "ĐÈN UV: CHỈ BẬT KHI KHÔNG CÓ NGƯỜI", new Vector3(23.2f, 2.5f, LabZ - 0.11f), Vector3.back, 0.035f, Color.black);

            // tủ an toàn sinh học dọc tường bắc
            BiosafetyCabinet(room, "Tủ an toàn sinh học vi sinh 1", new Vector3(21.6f, 0, LabZ - 0.5f), 90f);
            BiosafetyCabinet(room, "Tủ an toàn sinh học vi sinh 2", new Vector3(23.8f, 0, LabZ - 0.5f), 90f);

            // đảo nhuộm Gram + cấy vi sinh
            var isl = Group("Đảo nhuộm Gram", room, new Vector3(22.8f, 0, 11.5f));
            const float top = 0.9f;
            Box(isl, "Chân đế", new Vector3(0, 0.04f, 0), new Vector3(2.9f, 0.08f, 1.1f), darkMetal);
            Box(isl, "Thân tủ", new Vector3(-0.35f, 0.47f, 0), new Vector3(2.3f, 0.78f, 1.16f), cabinet);
            HollowCabinet(isl, "Hộc bồn", new Vector3(1.15f, 0.08f, 0), new Vector3(0.7f, 0.78f, 1.16f));
            foreach (int s in new[] { -1, 1 })
                for (int i = 0; i < 4; i++)
                    Box(isl, "Cánh tủ", new Vector3(-1.32f + i * 0.6f, 0.47f, s * 0.585f), new Vector3(0.56f, 0.7f, 0.01f), cabinetDoor, false);
            TopWithSink(isl, "Mặt đảo", 3.0f, 1.2f, top, new Vector2(1.15f, 0f), new Vector2(0.42f, 0.4f), 0.22f);
            var faucetBase = new Vector3(1.15f, top, 0.45f);
            Faucet(isl, "Vòi nước", faucetBase, Vector3.back, 0.32f, 0.2f);
            var rackG = Group("Giá nhuộm trên bồn", isl, new Vector3(1.15f, top - 0.04f, 0));
            foreach (float z in new[] { -0.08f, 0.08f })
                Box(rackG, "Thanh giá", new Vector3(0, 0, z), new Vector3(0.42f, 0.012f, 0.012f), steel, false);
            for (int i = 0; i < 3; i++)
                NoShadow(Box(rackG, "Lam đang nhuộm", new Vector3(-0.12f + i * 0.12f, 0.01f, 0), new Vector3(0.026f, 0.002f, 0.076f), Mat("T2_LamTim", new Color(0.5f, 0.2f, 0.7f, 0.6f), 0.9f, 0, transparent: true), false));
            StainBottle(isl, new Vector3(0.55f, top, -0.35f), bioPurpleStain, "Tím tinh thể");
            StainBottle(isl, new Vector3(0.65f, top, -0.35f), bioIodine, "Lugol");
            StainBottle(isl, new Vector3(0.75f, top, -0.35f), bioWhite, "Cồn 95 %");
            StainBottle(isl, new Vector3(0.85f, top, -0.35f), bioRedStain, "Safranin");
            AlcoholLamp(isl, new Vector3(-0.2f, top, 0.25f));
            var loops = Group("Ống cắm que cấy", isl, new Vector3(-0.45f, top, 0.25f));
            Cyl(loops, "Ống cắm", new Vector3(0, 0.05f, 0), 0.025f, 0.1f, steel);
            for (int i = 0; i < 3; i++)
                Cyl(loops, "Que cấy", new Vector3((i - 1) * 0.01f, 0.15f, 0), 0.002f, 0.22f, i == 1 ? bioBlueTip : steel, false, new Vector3((i - 1) * 6f, 0, 0));
            PetriStack(isl, new Vector3(-0.75f, top, 0.2f), 4, 0);
            PetriStack(isl, new Vector3(-0.9f, top, -0.2f), 3, 1);
            PetriStack(isl, new Vector3(-1.2f, top, 0.25f), 5, 2);
            Microscope(isl, "Kính hiển vi vật kính dầu", new Vector3(0.0f, top, -0.3f), 90f, false, true);
            ColonyCounter(isl, new Vector3(-0.6f, top, -0.3f), 90f);
            foreach (float x in new[] { -0.9f, 0.1f })
                Stool(isl, "Ghế đẩu", new Vector3(x, 0, -0.95f));
            Info(isl, "Đảo nhuộm Gram và cấy vi sinh", "Nhuộm Gram: tím tinh thể → lugol → cồn 95 % tẩy màu → safranin. Gram dương giữ màu tím, Gram âm bắt màu đỏ.",
                 "Chỉ dùng chủng dạy học an toàn (vi khuẩn sữa chua, E. coli K-12, nấm men). Khử trùng que cấy trên đèn cồn trước và sau khi cấy.");

            // tường đông: tủ ấm đứng, quầy chuẩn bị có tủ lạnh dưới quầy
            Incubator(room, new Vector3(26.55f, 0, 14.0f), 180f, 2.1f);
            var prep = Group("Quầy chuẩn bị", room, new Vector3(26.55f, 0, 11.95f));
            RoundBox(prep, "Thân quầy", new Vector3(0, 0.44f, 0), new Vector3(0.62f, 0.88f, 1.5f), 0.008f, cabinet);
            Box(prep, "Cửa tủ lạnh dưới quầy", new Vector3(-0.315f, 0.42f, -0.35f), new Vector3(0.012f, 0.72f, 0.7f), bioWhite, false);
            NoShadow(Box(prep, "Màn số tủ lạnh", new Vector3(-0.323f, 0.7f, -0.35f), new Vector3(0.004f, 0.03f, 0.08f), bioDisplayBlue, false));
            Box(prep, "Mặt quầy", new Vector3(0, 0.9f, 0), new Vector3(0.66f, 0.04f, 1.54f), epoxy);
            for (int i = 0; i < 4; i++) Bottle(prep, "Chai môi trường LB", new Vector3(0.05f, 0.92f, -0.5f + i * 0.14f), 3);
            PetriStack(prep, new Vector3(0.05f, 0.92f, 0.35f), 8, 2);
            Label(room, "MÔI TRƯỜNG NUÔI CẤY", new Vector3(StoreX - 0.104f, 1.6f, 11.95f), Vector3.left, 0.05f, new Color(0.05f, 0.42f, 0.42f));

            // tường tây: quầy thấp dưới kính mờ, tủ kính dụng cụ cao ở góc bắc
            var westC = Group("Quầy thấp tường tây", room, new Vector3(20.42f, 0, 11.5f));
            RoundBox(westC, "Thân", new Vector3(0, 0.45f, 0), new Vector3(0.6f, 0.9f, 2.6f), 0.008f, cabinet);
            Box(westC, "Mặt", new Vector3(0, 0.92f, 0), new Vector3(0.64f, 0.04f, 2.64f), epoxy);
            Vortex(westC, new Vector3(0.05f, 0.94f, -0.9f));
            BenchCentrifuge(westC, new Vector3(0.0f, 0.94f, -0.2f), 0f, 0.8f);
            TubeRack(westC, new Vector3(0.05f, 0.94f, 0.5f), 8, bioOrangeCap);
            PipetteStation(westC, new Vector3(0.02f, 0.94f, 0.95f), 0f);
            var tall = Group("Tủ kính dụng cụ", room, new Vector3(20.38f, 0, 13.9f));
            RoundBox(tall, "Thân tủ", new Vector3(0, 1.0f, 0), new Vector3(0.5f, 2.0f, 1.0f), 0.008f, bioWhite);
            NoShadow(Box(tall, "Cửa kính", new Vector3(0.256f, 1.1f, 0), new Vector3(0.008f, 1.6f, 0.94f), cabinetGlass, false));
            for (int i = 0; i < 4; i++)
            {
                float y = 0.45f + i * 0.4f;
                Box(tall, "Tầng", new Vector3(0, y, 0), new Vector3(0.46f, 0.015f, 0.94f), steel, false);
                for (int k = 0; k < 4; k++)
                {
                    var pos = new Vector3(0.05f, y + 0.008f, -0.36f + k * 0.24f);
                    if ((i + k) % 2 == 0) MeshObj(tall, "Bình tam giác", Erlenmeyer(), pos, labGlass);
                    else PetriStack(tall, pos, 4, 2);
                }
            }

            // khu chuẩn bị phía nam (dọc tường kho): bếp khuấy từ, cân, chai môi trường
            var south = Group("Quầy pha môi trường", room, new Vector3(22.7f, 0, 7.92f));
            RoundBox(south, "Thân", new Vector3(0, 0.44f, 0), new Vector3(3.6f, 0.88f, 0.6f), 0.008f, cabinet);
            Box(south, "Mặt", new Vector3(0, 0.9f, 0), new Vector3(3.64f, 0.04f, 0.64f), epoxy);
            for (int i = 0; i < 5; i++)
                Box(south, "Cánh tủ", new Vector3(-1.44f + i * 0.72f, 0.44f, 0.305f), new Vector3(0.68f, 0.78f, 0.01f), cabinetDoor, false);
            AnalyticalBalance(south, new Vector3(-1.2f, 0.92f, 0), -90f);
            var stir = Group("Bếp khuấy từ", south, new Vector3(-0.3f, 0.92f, 0));
            RoundBox(stir, "Thân", new Vector3(0, 0.05f, 0), new Vector3(0.2f, 0.1f, 0.3f), 0.01f, bioWhite);
            Cyl(stir, "Mặt gia nhiệt", new Vector3(0, 0.104f, 0.03f), 0.08f, 0.008f, steel);
            MeshObj(stir, "Bình môi trường", Erlenmeyer(), new Vector3(0, 0.108f, 0.03f), labGlass);
            for (int i = 0; i < 5; i++) Bottle(south, "Chai thạch", new Vector3(0.4f + i * 0.2f, 0.92f, -0.08f), 1);
            Box(south, "Bột thạch agar", new Vector3(1.5f, 0.98f, -0.05f), new Vector3(0.12f, 0.12f, 0.12f), bioKraft, false);

            // buồng đệm: ghế thay đồ, móc áo blouse, bồn rửa tay, thảm dính bụi, biển vùng sạch
            var air = Group("Buồng đệm", room);
            CoatRack(air, new Vector3(26.1f, 0, F2_ZoneSplitZ + 0.1f), 0f);
            var seat = Group("Ghế thay đồ", air, new Vector3(26.1f, 0, 7.95f));
            Box(seat, "Mặt ghế", new Vector3(0, 0.44f, 0), new Vector3(1.3f, 0.04f, 0.32f), oak);
            foreach (float x in new[] { -0.55f, 0.55f })
                Box(seat, "Chân", new Vector3(x, 0.21f, 0), new Vector3(0.04f, 0.42f, 0.28f), darkMetal, false);
            var sink = Group("Bồn rửa tay buồng đệm", air, new Vector3(25.45f, 0, 8.8f));
            Box(sink, "Bồn", new Vector3(0, 0.8f, 0), new Vector3(0.38f, 0.12f, 0.46f), bioWhite);
            Cyl(sink, "Chân bồn", new Vector3(0, 0.37f, 0), 0.06f, 0.74f, bioWhite);
            Faucet(sink, "Vòi cảm ứng", new Vector3(-0.15f, 0.86f, 0), Vector3.right, 0.2f, 0.12f);
            NoShadow(Box(air, "Thảm dính bụi", new Vector3(26.6f, 0.003f, 8.8f), new Vector3(0.55f, 0.006f, 1.1f), Mat("T2_ThamDinh", new Color(0.45f, 0.55f, 0.65f), 0.5f), false));
            Box(air, "Hộp bọc giày", new Vector3(25.42f, 1.25f, 9.6f), new Vector3(0.12f, 0.2f, 0.18f), bioNitrile, false);
            Box(air, "Biển vùng sạch", new Vector3(26.15f, 2.55f, F2_AirlockZ - 0.105f), new Vector3(1.0f, 0.24f, 0.006f), Mat("T2_BienVungSach", zoneMicroBio, 0.4f), false);
            Label(air, "VÙNG SẠCH · CLEAN AREA", new Vector3(26.15f, 2.57f, F2_AirlockZ - 0.11f), Vector3.back, 0.05f, Color.white);
            Info(air, "Buồng đệm", "Thay áo blouse sạch, đi bọc giày, rửa tay trước khi vào vùng sạch. Hai cửa không mở cùng lúc để giữ chênh áp.");
        }

        /// <summary>Máy đếm khuẩn lạc: đế, mặt đèn tròn chiếu từ dưới, kính lúp trên cần.</summary>
        static void ColonyCounter(Transform p, Vector3 pos, float yaw)
        {
            var g = Group("Máy đếm khuẩn lạc", p, pos, yaw);
            RoundBox(g, "Đế", new Vector3(0, 0.04f, 0), new Vector3(0.26f, 0.08f, 0.24f), 0.01f, bioWhite);
            NoShadow(Cyl(g, "Mặt đèn", new Vector3(0, 0.082f, 0), 0.06f, 0.004f, tubeGlow));
            PetriStack(g, new Vector3(0, 0.084f, 0), 1, 0);
            Cyl(g, "Cần", new Vector3(-0.1f, 0.18f, 0), 0.008f, 0.2f, steel);
            Cyl(g, "Kính lúp", new Vector3(-0.02f, 0.27f, 0), 0.05f, 0.01f, labGlass);
            NoShadow(Box(g, "Màn đếm", new Vector3(0.131f, 0.05f, 0), new Vector3(0.004f, 0.03f, 0.08f), bioDisplayRed, false));
            Info(g, "Máy đếm khuẩn lạc", "Đèn chiếu từ dưới, kính lúp phóng đại; chạm bút vào từng khuẩn lạc để đếm. Đĩa đếm được: 30 – 300 khuẩn lạc.");
        }

        // =====================================================================
        // Kho sinh phẩm (x 20..27, z 0..7,5)
        // =====================================================================

        static void BuildBioStorageRoom(Transform f2)
        {
            var room = Group("Kho sinh phẩm", f2);
            var lights = Group("Đèn", room);
            RoomLights(lights, new[] { 23.5f }, new[] { 2.4f, 5.4f }, 8f, 2.8f);
            foreach (var (x, z) in new[] { (22.0f, 2.4f), (25.0f, 2.4f), (22.0f, 5.4f), (25.0f, 5.4f) })
                LedPanel(lights, "Đèn kho", x, z, 2f, 6f, false);

            // tường bắc: tủ lạnh 4 °C cửa kính, tủ đông −20 °C, tủ âm sâu −80 °C (mặt trước nhìn về nam)
            GlassFridge(room, new Vector3(21.3f, 0, F2_ZoneSplitZ - 0.48f));
            Freezer(room, "Tủ đông −20 °C", new Vector3(22.95f, 0, F2_ZoneSplitZ - 0.5f), 0.75f, "−20,0 °C", bioWhite);
            Freezer(room, "Tủ âm sâu −80 °C", new Vector3(24.05f, 0, F2_ZoneSplitZ - 0.5f), 0.95f, "−80 °C", Mat("T2_TuAmSau", new Color(0.86f, 0.88f, 0.9f), 0.55f));
            Label(room, "BẢO QUẢN LẠNH · COLD STORAGE", new Vector3(23.0f, 2.45f, F2_ZoneSplitZ - 0.104f), Vector3.back, 0.06f, new Color(0.15f, 0.35f, 0.65f));
            var sheet = Group("Bảng theo dõi nhiệt độ", room, new Vector3(25.6f, 1.5f, F2_ZoneSplitZ - 0.105f));
            Box(sheet, "Bảng", Vector3.zero, new Vector3(0.5f, 0.65f, 0.008f), signWhite, false);
            Label(sheet, "NHẬT KÝ NHIỆT ĐỘ TỦ\n4 °C · −20 °C · −80 °C\nGhi 2 lần/ngày", new Vector3(0, 0.15f, -0.006f), Vector3.back, 0.022f, new Color(0.15f, 0.2f, 0.3f));
            Box(sheet, "Kẹp giấy", new Vector3(0, 0.3f, -0.008f), new Vector3(0.14f, 0.04f, 0.01f), steel, false);

            // tường nam: hai dãy kệ inox 4 tầng
            Shelving(room, "Kệ inox hộp sinh phẩm", new Vector3(22.15f, 0, 0.42f), 2.9f, 0f, 1);
            Shelving(room, "Kệ inox môi trường – hóa chất sinh học", new Vector3(25.3f, 0, 0.42f), 2.9f, 0f, 2);
            // tường tây: tủ kính trưng bày mô hình và mẫu ngâm
            var show = Group("Tủ kính mô hình và mẫu ngâm", room, new Vector3(20.4f, 0, 3.8f), 0f);
            RoundBox(show, "Thân tủ", new Vector3(0, 1.0f, 0), new Vector3(0.55f, 2.0f, 2.2f), 0.008f, cabinet);
            NoShadow(Box(show, "Cửa kính", new Vector3(0.281f, 1.15f, 0), new Vector3(0.008f, 1.5f, 2.12f), cabinetGlass, false));
            for (int i = 0; i < 3; i++)
            {
                float y = 0.5f + i * 0.5f;
                Box(show, "Tầng kính", new Vector3(0, y, 0), new Vector3(0.5f, 0.012f, 2.1f), cabinetGlass, false);
                for (int k = 0; k < 4; k++) SpecimenJar(show, new Vector3(0.02f, y + 0.008f, -0.75f + k * 0.5f), (i * 4 + k) % 5, 0.75f);
            }
            Info(show, "Tủ mẫu ngâm", "Mẫu ngâm formalin dùng quan sát hình thái: cá, ếch, rắn, tim, cây non.");
            // đảo giữa phòng: mô hình dạy học (ADN, tế bào, bán thân, bộ xương thu nhỏ)
            var isl = Group("Bàn mô hình", room, new Vector3(23.5f, 0, 4.9f));
            RoundBox(isl, "Thân bàn", new Vector3(0, 0.42f, 0), new Vector3(3.4f, 0.84f, 0.8f), 0.008f, cabinetDoor);
            Box(isl, "Mặt bàn", new Vector3(0, 0.86f, 0), new Vector3(3.44f, 0.04f, 0.84f), epoxy);
            DnaModel(isl, new Vector3(-1.3f, 0.88f, 0), 0.6f);
            CellModel(isl, new Vector3(-0.55f, 0.88f, 0));
            TorsoModel(isl, new Vector3(0.35f, 0.88f, 0), -90f);
            var plantCell = Group("Mô hình tế bào thực vật", isl, new Vector3(1.25f, 0.88f, 0));
            RoundBox(plantCell, "Thành tế bào", new Vector3(0, 0.08f, 0), new Vector3(0.3f, 0.16f, 0.22f), 0.02f, Mat("T2_ThanhTeBao", new Color(0.55f, 0.75f, 0.35f), 0.4f));
            Ball(plantCell, "Không bào", new Vector3(0.03f, 0.15f, 0), new Vector3(0.18f, 0.04f, 0.12f), Mat("T2_KhongBao", new Color(0.7f, 0.85f, 0.95f), 0.7f));
            foreach (var (x, z) in new[] { (-0.1f, -0.06f), (-0.08f, 0.06f), (0.1f, 0.07f) })
                Ball(plantCell, "Lục lạp", new Vector3(x, 0.165f, z), new Vector3(0.04f, 0.015f, 0.025f), bioLeaf);
            Info(isl, "Mô hình dạy học", "Mô hình ADN, tế bào động vật, tế bào thực vật và bán thân người tháo lắp. Mượn mang lên phòng thí nghiệm theo sổ mượn.");
            // tường đông: bàn ghi chép (máy tính bảng), tủ thép hóa chất sinh học
            var desk = Group("Bàn ghi chép", room, new Vector3(26.5f, 0, 1.3f), 180f);
            Box(desk, "Mặt bàn", new Vector3(0, 0.74f, 0), new Vector3(0.6f, 0.03f, 1.2f), oak);
            foreach (int s in new[] { -1, 1 })
                Box(desk, "Chân bàn", new Vector3(0, 0.37f, s * 0.55f), new Vector3(0.55f, 0.72f, 0.04f), darkMetal, false);
            Box(desk, "Máy tính bảng", new Vector3(0.05f, 0.76f, 0.1f), new Vector3(0.18f, 0.008f, 0.25f), screen, false);
            Box(desk, "Sổ mượn", new Vector3(0.1f, 0.765f, -0.3f), new Vector3(0.21f, 0.02f, 0.3f), Mat("T2_BiaSo", new Color(0.15f, 0.35f, 0.25f), 0.3f), false);
            var steelCab = Group("Tủ thép hóa chất sinh học", room, new Vector3(26.62f, 0, 5.9f), 180f);
            Box(steelCab, "Thân tủ", new Vector3(0, 0.95f, 0), new Vector3(0.5f, 1.9f, 1.0f), Mat("T2_TuThep", new Color(0.82f, 0.84f, 0.86f), 0.45f, 0.3f));
            foreach (int s in new[] { -1, 1 })
                Box(steelCab, "Cánh tủ", new Vector3(0.255f, 0.95f, s * 0.245f), new Vector3(0.01f, 1.82f, 0.48f), Mat("T2_TuThep", new Color(0.82f, 0.84f, 0.86f), 0.45f, 0.3f), false);
            Box(steelCab, "Nhãn", new Vector3(0.262f, 1.6f, 0), new Vector3(0.004f, 0.12f, 0.5f), bioBlueTip, false);
            Label(steelCab, "ENZYME · KIT ADN", new Vector3(0.266f, 1.6f, 0), Vector3.right, 0.035f, Color.white);
        }

        /// <summary>Tủ lạnh 4 °C hai cánh kính (mặt trước nhìn về nam): kệ có hộp, chai, ống bên trong nhìn qua kính.</summary>
        static void GlassFridge(Transform p, Vector3 pos)
        {
            var g = Group("Tủ lạnh 4 °C", p, pos, 90f);  // hệ riêng: mặt trước +x
            const float d = 0.75f, w = 1.3f, h = 2.0f;
            Box(g, "Vách sau", new Vector3(-d / 2 + 0.03f, h / 2, 0), new Vector3(0.06f, h, w), bioWhite);
            foreach (int s in new[] { -1, 1 })
                Box(g, "Vách bên", new Vector3(0, h / 2, s * (w / 2 - 0.03f)), new Vector3(d, h, 0.06f), bioWhite);
            Box(g, "Nóc", new Vector3(0, h - 0.1f, 0), new Vector3(d, 0.2f, w), bioWhite);
            Box(g, "Đáy", new Vector3(0, 0.06f, 0), new Vector3(d, 0.12f, w), darkMetal);
            for (int i = 0; i < 4; i++)
            {
                float y = 0.3f + i * 0.38f;
                Box(g, "Kệ", new Vector3(0, y, 0), new Vector3(d - 0.1f, 0.012f, w - 0.12f), steel, false);
                for (int k = 0; k < 5; k++)
                {
                    float z = -0.5f + k * 0.25f;
                    if ((i + k) % 3 == 0) Bottle(g, "Chai", new Vector3(0.0f, y + 0.006f, z), 1);
                    else Box(g, "Hộp sinh phẩm", new Vector3(0, y + 0.05f, z), new Vector3(0.22f, 0.09f, 0.18f), (i + k) % 3 == 1 ? bioBlueTip : bioKraft, false);
                }
            }
            NoShadow(Box(g, "Đèn trong", new Vector3(0.1f, h - 0.22f, 0), new Vector3(0.04f, 0.01f, w - 0.2f), tubeGlow, false));
            foreach (int s in new[] { -1, 1 })
            {
                NoShadow(Box(g, "Cánh kính", new Vector3(d / 2 + 0.01f, h / 2 - 0.05f, s * w / 4), new Vector3(0.02f, h - 0.32f, w / 2 - 0.02f), cabinetGlass));
                Box(g, "Khung cánh", new Vector3(d / 2 + 0.012f, h / 2 - 0.05f, s * (w / 2 - 0.02f)), new Vector3(0.024f, h - 0.32f, 0.03f), alu, false);
                Box(g, "Tay nắm", new Vector3(d / 2 + 0.035f, 1.05f, s * 0.05f), new Vector3(0.02f, 0.5f, 0.02f), alu, false);
            }
            NoShadow(Box(g, "Màn nhiệt độ", new Vector3(d / 2 + 0.003f, h - 0.1f, 0), new Vector3(0.004f, 0.06f, 0.16f), bioDisplayBlue, false));
            Label(g, "4,0 °C", new Vector3(d / 2 + 0.008f, h - 0.098f, 0), Vector3.right, 0.03f, new Color(0.6f, 0.85f, 1f));
            Info(g, "Tủ lạnh 4 °C", "Bảo quản enzyme đã pha, môi trường nuôi cấy đã đổ đĩa, kháng thể, mẫu sinh học ngắn ngày ở 2 – 8 °C.");
        }

        /// <summary>Tủ đông đứng cửa đặc (mặt trước nhìn về nam): màn nhiệt độ, tay nắm khóa.</summary>
        static Transform Freezer(Transform p, string name, Vector3 pos, float w, string temp, Material body)
        {
            var g = Group(name, p, pos, 90f);
            const float d = 0.8f, h = 1.95f;
            RoundBox(g, "Thân tủ", new Vector3(0, h / 2, 0), new Vector3(d, h, w), 0.01f, body);
            Box(g, "Cửa", new Vector3(d / 2 + 0.015f, h / 2 - 0.05f, 0), new Vector3(0.03f, h - 0.3f, w - 0.06f), body);
            Box(g, "Tay nắm khóa", new Vector3(d / 2 + 0.045f, 1.0f, w / 2 - 0.1f), new Vector3(0.03f, 0.3f, 0.04f), darkMetal, false);
            NoShadow(Box(g, "Màn nhiệt độ", new Vector3(d / 2 + 0.032f, h - 0.12f, 0), new Vector3(0.004f, 0.06f, 0.18f), bioDisplayBlue, false));
            Label(g, temp, new Vector3(d / 2 + 0.036f, h - 0.118f, 0), Vector3.right, 0.03f, new Color(0.6f, 0.85f, 1f));
            Box(g, "Lưới tản nhiệt", new Vector3(d / 2 + 0.002f, 0.12f, 0), new Vector3(0.004f, 0.12f, w - 0.1f), darkMetal, false);
            Info(g, name, name.Contains("80") ? "Lưu mẫu ADN, chủng gốc dài hạn (mô phỏng: trường phổ thông thường không có tủ −80 °C)." : "Bảo quản enzyme Taq, mồi PCR, dNTP, chủng giống trong glixerol ở −20 °C.");
            return g;
        }

        /// <summary>Kệ inox 4 tầng áp tường (mặt trước +z của nhóm khi yaw = 0): hộp nhãn, chai, hộp đầu côn, túi đĩa petri.</summary>
        static void Shelving(Transform p, string name, Vector3 pos, float len, float yaw, int seed)
        {
            var g = Group(name, p, pos, yaw);
            const float d = 0.5f;
            foreach (float x in new[] { -len / 2 + 0.02f, len / 2 - 0.02f })
                foreach (float z in new[] { -d / 2 + 0.02f, d / 2 - 0.02f })
                    Box(g, "Trụ kệ", new Vector3(x, 0.95f, z), new Vector3(0.03f, 1.9f, 0.03f), steel, false);
            var r = new System.Random(seed * 17);
            var boxMats = new[] { bioKraft, bioBlueTip, bioYellowTip, signWhite, Mat("T2_HopXanhLa", new Color(0.3f, 0.6f, 0.35f), 0.4f) };
            for (int i = 0; i < 4; i++)
            {
                float y = 0.12f + i * 0.5f;
                Box(g, "Tầng kệ", new Vector3(0, y, 0), new Vector3(len, 0.02f, d), steel);
                float x = -len / 2 + 0.12f;
                while (x < len / 2 - 0.15f)
                {
                    int kind = r.Next(4);
                    if (kind == 0) { Bottle(g, "Chai", new Vector3(x, y + 0.01f, 0.05f), r.Next(5)); x += 0.1f; }
                    else if (kind == 3) { PetriStack(g, new Vector3(x + 0.03f, y + 0.01f, 0.05f), 6, 2); x += 0.12f; }
                    else
                    {
                        float bw = 0.18f + 0.1f * (float)r.NextDouble(), bh = 0.12f + 0.12f * (float)r.NextDouble();
                        Box(g, "Hộp", new Vector3(x + bw / 2, y + 0.01f + bh / 2, 0), new Vector3(bw, bh, d - 0.12f), boxMats[r.Next(boxMats.Length)], false);
                        Box(g, "Nhãn hộp", new Vector3(x + bw / 2, y + 0.01f + bh / 2, (d - 0.12f) / 2 + 0.001f), new Vector3(bw * 0.6f, bh * 0.4f, 0.002f), labelPaper, false);
                        x += bw + 0.03f;
                    }
                }
            }
            Info(g, "Kệ lưu trữ", "Môi trường nuôi cấy dạng bột, thạch agar, đĩa petri vô trùng, đầu côn, ống ly tâm, bộ kit tách chiết ADN, thuốc nhuộm.");
        }

        // =====================================================================
        // Hành lang tầng 2 (x 0..27, z −2,2..0)
        // =====================================================================

        static void BuildCorridorT2(Transform f2)
        {
            var hall = Group("Hành lang tầng 2", f2);
            var lights = Group("Đèn hành lang", hall);
            for (float x = 3.5f; x <= 24.6f; x += 4.2f)
                LedPanel(lights, "Đèn hành lang", x, CorridorZ / 2, 2.2f, 7f);
            DoorZoneGraphic(hall, 1.6f, 3.2f, 0f, -1, zoneBioLab, "PHÒNG THÍ NGHIỆM SINH HỌC", "BIOLOGY LAB");
            DoorZoneGraphic(hall, 5.9f, 7.5f, CorridorZ, +1, zoneMolecular, "SINH HỌC PHÂN TỬ", "MOLECULAR BIOLOGY");
            DoorZoneGraphic(hall, 19.0f, 20.6f, CorridorZ, +1, zoneGreenhouse, "NHÀ KÍNH THỰC VẬT", "GREENHOUSE");
            var frieze = Group("Dải sọc màu (Quốc tế)", hall);
            frieze.gameObject.AddComponent<ThemeOnly>().theme = LabThemeMode.QuocTe;
            Frieze(frieze, new Vector3(3.6f, 0, -0.104f), new Vector3(StoreX - 0.2f, 0, -0.104f));
            Frieze(frieze, new Vector3(0.2f, 0, CorridorZ + 0.104f), new Vector3(StoreX - 0.2f, 0, CorridorZ + 0.104f));

            // logo trường + tên khu trên tường bắc (giữa cửa phòng sinh học và tường các nhà sinh học)
            SchoolLogo(hall, new Vector3(5.6f, 2.15f, -0.104f), Vector3.back, 2.2f);
            Label(hall, "KHU SINH HỌC · TẦNG 2", new Vector3(5.6f, 1.55f, -0.106f), Vector3.back, 0.08f, new Color(0.08f, 0.42f, 0.22f));
            Label(hall, "BIOLOGY WING · LEVEL 2", new Vector3(5.6f, 1.42f, -0.106f), Vector3.back, 0.045f, new Color(0.35f, 0.38f, 0.4f));
            // 3 nhà sinh học còn lại trên tường bắc hành lang
            var gal = Group("Hành lang các nhà sinh học", hall);
            float[] xs = { 9.6f, 11.8f, 14.0f };
            for (int i = 0; i < 3; i++)
                BiologistPortrait(gal, 4 + i, new Vector3(xs[i], 1.75f, -0.112f), Vector3.back);
            // bảng tin điện tử liên kết Phòng Lab Sinh học 3D trên web
            var scr = ScreenUnit(hall, "Màn hình bảng tin", "t2_hanh_lang", new Vector3(13.5f, 1.6f, CorridorZ + 0.12f), Vector3.forward, 1.2f, 0.68f, 10f,
                ("PHÒNG LAB SINH HỌC 3D (WEB)", "Giải phẫu người thật BodyParts3D · 15 cơ quan\nTim · Mạch máu · Phổi · Não · Tủy sống · Thận · Dạ dày\nRuột non · Ruột già · Gan · Tụy · Lách · Xương · Cơ · Da"),
                ("6 CẢNH HIỂN VI", "Phế nang · Nephron · Synapse · Sarcomere\nTế bào viền dạ dày · Tiểu thùy gan\nTiêu bản ở hộp tiêu bản trên mỗi bàn"),
                ("LỊCH PHÒNG SINH HỌC", "Sáng: Sinh 10 – tế bào, enzyme\nChiều: Sinh 11 – quang hợp, hô hấp\nCâu lạc bộ KHKT: thứ Bảy"));
            Info(scr.transform, "Bảng tin khu sinh học", "Liên kết Phòng Lab Sinh học 3D trên web: giải phẫu BodyParts3D, 6 cảnh hiển vi, kiến thức 15 cơ quan.");
            var bench = Group("Ghế băng", hall, new Vector3(10.6f, 0, CorridorZ + 0.32f));
            Box(bench, "Mặt ghế", new Vector3(0, 0.44f, 0), new Vector3(1.8f, 0.05f, 0.42f), oak);
            foreach (float x in new[] { -0.8f, 0.8f })
                Box(bench, "Chân ghế", new Vector3(x, 0.21f, 0), new Vector3(0.05f, 0.42f, 0.38f), darkMetal, false);
            Plant(hall, new Vector3(0.45f, 0, -1.1f), 4, 0.9f);
            Plant(hall, new Vector3(17.6f, 0, CorridorZ + 0.35f), 4, 0.9f);
            Extinguisher(hall, new Vector3(16.5f, 0, -0.25f), Vector3.back);
            // biển lối thoát trên cửa sang lõi thang
            var exit = Group("Biển lối thoát", hall, new Vector3(StoreX - 0.12f, 2.42f, -1.1f), -90f);
            NoShadow(Box(exit, "Hộp đèn", Vector3.zero, new Vector3(0.42f, 0.16f, 0.04f), Mat("T2_DenThoatHiem", new Color(0.1f, 0.6f, 0.3f), 0.5f, 0, emission: new Color(0.1f, 0.9f, 0.35f)), false));
            Label(exit, "LỐI THOÁT · THANG", new Vector3(0, 0, 0.022f), Vector3.forward, 0.045f, Color.white);
        }

        // =====================================================================
        // Phòng sinh học phân tử (x 0..13,5, z −9..−2,2)
        // =====================================================================

        static void BuildMolecularRoom(Transform f2)
        {
            var room = Group("Phòng sinh học phân tử", f2);
            var lights = Group("Đèn", room);
            RoomLights(lights, new[] { 3.4f, 10.1f }, new[] { -5.6f }, 9f, 3.4f);
            foreach (float x in new[] { 2.0f, 4.8f, 7.6f, 10.4f })
                foreach (float z in new[] { -4.0f, -7.2f })
                    LedPanel(lights, "Đèn trần", x, z, 2f, 6f, false);
            // vạch sàn luồng một chiều
            var floorG = Group("Vạch sàn luồng một chiều", room);
            NoShadow(Box(floorG, "Vùng tiền PCR", new Vector3(2.1f, 0.002f, -5.6f), new Vector3(3.8f, 0.004f, 6.5f), Mat("T2_SanTienPCR", new Color(0.78f, 0.9f, 0.8f), 0.4f), false));
            NoShadow(Box(floorG, "Vùng hậu PCR", new Vector3(11.2f, 0.002f, -5.6f), new Vector3(4.4f, 0.004f, 6.5f), Mat("T2_SanHauPCR", new Color(0.95f, 0.85f, 0.75f), 0.4f), false));
            foreach (var (x, c) in new[] { (4.05f, new Color(0.2f, 0.65f, 0.35f)), (9.0f, new Color(0.95f, 0.5f, 0.15f)) })
                NoShadow(Box(floorG, "Vạch ranh giới", new Vector3(x, 0.003f, -5.6f), new Vector3(0.06f, 0.004f, 6.5f), Mat("T2_Vach" + ColorUtility.ToHtmlStringRGB(c), c, 0.4f), false));

            var signs = Group("Biển khu", room);
            ZoneSign(signs, "TIỀN PCR", new Vector3(2.1f, 2.95f, -5.6f), 90f, 1.6f, "PRE-PCR · CLEAN");
            ZoneSign(signs, "TÁCH CHIẾT ADN", new Vector3(6.5f, 2.95f, -4.3f), 0f, 2.0f, "DNA EXTRACTION");
            ZoneSign(signs, "PCR & ĐIỆN DI", new Vector3(11.2f, 2.95f, -5.6f), 90f, 1.8f, "PCR & GEL");

            // --- Tiền PCR: tủ thao tác PCR (tường tây), quầy pha mix dọc tường nam ---
            PcrWorkstation(room, new Vector3(0.5f, 0, -5.4f));
            var pre = LabCounter(room, "Quầy tiền PCR", new Vector3(2.15f, 0, -8.58f), 3.7f, 0.85f);
            PipetteStation(pre, new Vector3(-1.2f, 0.85f, 0.02f), -90f);
            Vortex(pre, new Vector3(-0.4f, 0.85f, 0.05f));
            BenchCentrifuge(pre, new Vector3(0.15f, 0.85f, 0.05f), -90f, 0.65f);
            TubeRack(pre, new Vector3(0.7f, 0.85f, 0.05f), 8, bioYellowTip);
            var ice = Group("Hộp đá", pre, new Vector3(1.3f, 0.85f, 0.05f));
            Box(ice, "Hộp xốp", new Vector3(0, 0.07f, 0), new Vector3(0.22f, 0.14f, 0.3f), signWhite);
            Box(ice, "Đá", new Vector3(0, 0.142f, 0), new Vector3(0.19f, 0.004f, 0.27f), Mat("T2_DaLanh", new Color(0.85f, 0.92f, 0.98f), 0.9f), false);
            Freezer(room, "Tủ đông −20 °C (tiền PCR)", new Vector3(0.55f, 0, -2.75f), 0.7f, "−20,0 °C", bioWhite);

            // --- Tách chiết ADN: đảo giữa phòng ---
            var isl = Group("Đảo tách chiết ADN", room, new Vector3(6.5f, 0, -5.9f));
            const float top = 0.9f;
            Box(isl, "Chân đế", new Vector3(0, 0.04f, 0), new Vector3(3.6f, 0.08f, 1.0f), darkMetal);
            RoundBox(isl, "Thân đảo", new Vector3(0, 0.47f, 0), new Vector3(3.8f, 0.78f, 1.1f), 0.008f, cabinet);
            foreach (int s in new[] { -1, 1 })
                for (int i = 0; i < 5; i++)
                    Box(isl, "Cánh tủ", new Vector3(-1.52f + i * 0.76f, 0.47f, s * 0.556f), new Vector3(0.72f, 0.7f, 0.01f), cabinetDoor, false);
            Box(isl, "Mặt đảo", new Vector3(0, top - 0.02f, 0), new Vector3(3.9f, 0.04f, 1.2f), epoxy);
            WaterBath(isl, new Vector3(-1.3f, top, -0.15f), 90f, "60,0 °C");
            BenchCentrifuge(isl, new Vector3(-0.55f, top, -0.2f), 90f);
            var block = Group("Bộ gia nhiệt khô", isl, new Vector3(0.05f, top, -0.25f));
            RoundBox(block, "Thân", new Vector3(0, 0.06f, 0), new Vector3(0.2f, 0.12f, 0.26f), 0.01f, bioWhite);
            NoShadow(Box(block, "Màn số", new Vector3(0, 0.07f, 0.131f), new Vector3(0.1f, 0.03f, 0.004f), bioDisplayRed, false));
            // bộ tách ADN chuối: cốc dịch nghiền, bình cồn lạnh, đũa thủy tinh, ống falcon
            var banana = Group("Bộ tách chiết ADN chuối", isl, new Vector3(0.75f, top, 0.15f));
            MeshObj(banana, "Cốc dịch nghiền", Beaker("Coc250", 0.035f, 0.1f), Vector3.zero, labGlass);
            NoShadow(Cyl(banana, "Dịch chuối", new Vector3(0, 0.035f, 0), 0.033f, 0.07f, Mat("T2_DichChuoi", new Color(0.95f, 0.9f, 0.7f, 0.85f), 0.6f, 0, transparent: true)));
            Bottle(banana, "Cồn 90° lạnh", new Vector3(0.12f, 0, -0.05f), 4);
            Cyl(banana, "Đũa thủy tinh", new Vector3(-0.1f, 0.01f, 0.05f), 0.003f, 0.2f, labGlass, false, new Vector3(0, 0, 80));
            TubeRack(banana, new Vector3(0.3f, 0, 0), 5, bioBlueTip);
            Info(banana, "Tách chiết ADN chuối", "Nghiền chuối với nước rửa bát + muối (phá màng tế bào, màng nhân), lọc, rót nhẹ cồn lạnh lên trên: ADN kết tủa thành sợi trắng ở mặt phân cách.");
            Spectro(isl, new Vector3(1.55f, top, -0.2f));
            PipetteStation(isl, new Vector3(1.4f, top, 0.25f), 90f);
            foreach (float x in new[] { -1.2f, 0f, 1.2f })
                foreach (int s in new[] { -1, 1 })
                    Stool(isl, "Ghế đẩu", new Vector3(x, 0, s * 0.9f));

            // --- Hậu PCR: 2 máy PCR, bộ điện di, buồng chụp gel dọc tường nam ---
            var post = LabCounter(room, "Quầy hậu PCR", new Vector3(11.15f, 0, -8.58f), 4.3f, 0.85f);
            for (int i = 0; i < 2; i++) ThermalCycler(post, new Vector3(-1.75f + i * 0.45f, 0.85f, 0.02f), $"Máy PCR {i + 1}");
            GelElectrophoresis(post, new Vector3(-0.3f, 0.85f, 0.02f));
            GelDoc(post, new Vector3(0.95f, 0.85f, 0.0f));
            Label(room, "KHU HẬU PCR · KHÔNG MANG ĐỒ SANG KHU TIỀN PCR", new Vector3(11.15f, 2.78f, WingZ + 0.104f), Vector3.forward, 0.045f, new Color(0.75f, 0.35f, 0.05f));

            // --- Tường bắc: tủ thấp dưới kính mờ, áo blouse riêng từng khu, bồn rửa ---
            LabCounter(room, "Tủ thấp tường bắc (tây)", new Vector3(2.7f, 0, -2.6f), 3.2f, 0.85f, 180f);
            LabCounter(room, "Tủ thấp tường bắc (đông)", new Vector3(10.6f, 0, -2.6f), 3.8f, 0.85f, 180f);
            var coats = Group("Móc áo theo khu", room);
            CoatRack(coats, new Vector3(5.1f, 0, CorridorZ - 0.1f), 180f);
            CoatRack(coats, new Vector3(RoomSplitX - 0.1f, 0, -3.9f), -90f);
            NoShadow(Panel(room, "Áp phích cấu trúc ADN", new Vector3(13.395f, 1.8f, -6.4f), Vector3.left, 0.9f, 1.24f, posterBio[3]));
            Box(room, "Khung áp phích", new Vector3(13.398f, 1.8f, -6.4f), new Vector3(0.004f, 1.3f, 0.96f), darkMetal, false);
            Plant(room, new Vector3(4.45f, 0, -8.5f), 4, 0.9f);
            BiohazardBin(room, new Vector3(8.6f, 0, -8.5f));
        }

        /// <summary>Quầy thí nghiệm áp tường dài len, cao top, mặt trước nhìn về +z của nhóm khi yaw = 0 (tường ở −z).</summary>
        static Transform LabCounter(Transform p, string name, Vector3 pos, float len, float top, float yaw = 0f)
        {
            var g = Group(name, p, pos, yaw);
            const float d = 0.62f;
            Box(g, "Chân đế", new Vector3(0, 0.04f, -0.02f), new Vector3(len - 0.04f, 0.08f, d - 0.06f), darkMetal);
            Box(g, "Thân tủ", new Vector3(0, (top - 0.04f) / 2 + 0.04f, 0), new Vector3(len, top - 0.08f, d), cabinet);
            int n = Mathf.Max(1, Mathf.RoundToInt(len / 0.6f));
            for (int i = 0; i < n; i++)
            {
                float x = -len / 2 + (i + 0.5f) * len / n;
                Box(g, "Cánh tủ", new Vector3(x, top / 2, d / 2 + 0.005f), new Vector3(len / n - 0.04f, top - 0.2f, 0.01f), cabinetDoor, false);
                Box(g, "Tay nắm", new Vector3(x, top - 0.15f, d / 2 + 0.015f), new Vector3(0.12f, 0.015f, 0.015f), handleBlue, false);
            }
            Box(g, "Mặt quầy", new Vector3(0, top - 0.02f, 0.01f), new Vector3(len + 0.02f, 0.04f, d + 0.04f), epoxy);
            return g;
        }

        /// <summary>Tủ thao tác PCR: vỏ trắng, mặt trước kính acrylic kéo lên, đèn UV và đèn trắng trong tủ (mặt trước +x).</summary>
        static void PcrWorkstation(Transform p, Vector3 pos)
        {
            var g = Group("Tủ thao tác PCR", p, pos);
            const float w = 1.2f, d = 0.7f;
            LabCounter(g, "Bàn đỡ", new Vector3(0.0f, 0, 0), w + 0.1f, 0.85f, 90f);
            RoundBox(g, "Đỉnh tủ", new Vector3(0, 1.75f, 0), new Vector3(d, 0.14f, w), 0.008f, bioWhite);
            foreach (int s in new[] { -1, 1 })
                RoundBox(g, "Vách bên", new Vector3(0, 1.27f, s * (w / 2 - 0.02f)), new Vector3(d, 0.84f, 0.04f), 0.006f, bioWhite);
            Box(g, "Vách sau", new Vector3(-d / 2 + 0.02f, 1.27f, 0), new Vector3(0.04f, 0.84f, w - 0.08f), bioWhite);
            NoShadow(Box(g, "Cửa acrylic", new Vector3(d / 2 - 0.02f, 1.42f, 0), new Vector3(0.01f, 0.55f, w - 0.08f), cabinetGlass));
            NoShadow(Box(g, "Đèn UV trong tủ", new Vector3(0, 1.66f, -0.15f), new Vector3(0.04f, 0.015f, w - 0.25f), Mat("T2_BongUvTat", new Color(0.78f, 0.74f, 0.9f), 0.8f), false));
            NoShadow(Box(g, "Đèn trắng trong tủ", new Vector3(0, 1.66f, 0.15f), new Vector3(0.04f, 0.015f, w - 0.25f), tubeGlow, false));
            PipetteStation(g, new Vector3(-0.15f, 0.87f, -0.3f), 0f);
            TubeRack(g, new Vector3(0.05f, 0.87f, 0.25f), 8, bioYellowTip);
            Label(g, "TỦ THAO TÁC PCR", new Vector3(d / 2 + 0.002f, 1.75f, 0), Vector3.right, 0.04f, new Color(0.25f, 0.3f, 0.35f));
            Info(g, "Tủ thao tác PCR", "Pha hỗn hợp phản ứng PCR (đệm, dNTP, mồi, Taq) trong tủ sạch. Đèn UV chiếu 15 phút trước khi dùng để phá ADN nhiễm.",
                 "Khu tiền PCR không nhận sản phẩm PCR hay gel từ khu hậu PCR (tránh nhiễm chéo).");
        }

        /// <summary>Máy PCR (luân nhiệt) để bàn: thân xám, nắp gia nhiệt, khối 96 giếng lộ khi mở nắp, màn cảm ứng. Mặt trước +z.</summary>
        static void ThermalCycler(Transform p, Vector3 pos, string name)
        {
            var g = Group(name, p, pos);
            RoundBox(g, "Thân máy", new Vector3(0, 0.1f, 0), new Vector3(0.32f, 0.2f, 0.42f), 0.015f, Mat("T2_VoMayPCR", new Color(0.82f, 0.83f, 0.85f), 0.5f));
            Box(g, "Nắp gia nhiệt", new Vector3(0, 0.215f, -0.04f), new Vector3(0.28f, 0.035f, 0.3f), Mat("T2_NapPCR", new Color(0.3f, 0.32f, 0.35f), 0.5f));
            NoShadow(Box(g, "Màn cảm ứng", new Vector3(0, 0.12f, 0.212f), new Vector3(0.2f, 0.12f, 0.004f), bioDisplayBlue, false, new Vector3(-15, 0, 0)));
            Label(g, "CHU KỲ 24/35\n95° → 55° → 72°", new Vector3(0, 0.12f, 0.216f), Vector3.forward, 0.014f, new Color(0.75f, 0.9f, 1f));
            Info(g, "Máy PCR (luân nhiệt)", "Mỗi chu kỳ: biến tính 95 °C → gắn mồi ~55 °C → kéo dài 72 °C; sau n chu kỳ số bản sao ADN tăng tới 2ⁿ lần (hiệu suất lý tưởng).");
        }

        /// <summary>Bộ điện di ngang: bể nhựa trong có gel, lược, điện cực đỏ / đen, nguồn điện di có màn số. Mặt trước +z.</summary>
        static void GelElectrophoresis(Transform p, Vector3 pos)
        {
            var g = Group("Bộ điện di gel", p, pos);
            NoShadow(Box(g, "Bể điện di", new Vector3(0, 0.04f, 0), new Vector3(0.3f, 0.08f, 0.16f), translucentPlastic));
            NoShadow(Box(g, "Dung dịch đệm", new Vector3(0, 0.05f, 0), new Vector3(0.28f, 0.04f, 0.14f), bioWater, false));
            NoShadow(Box(g, "Bản gel", new Vector3(0, 0.072f, 0), new Vector3(0.12f, 0.008f, 0.1f), bioGel, false));
            for (int i = 0; i < 6; i++)
            {
                float z = -0.04f + i * 0.016f;
                NoShadow(Box(g, "Vạch thuốc nhuộm", new Vector3(-0.03f + (i % 3) * 0.008f, 0.0765f, z), new Vector3(0.004f, 0.001f, 0.01f), bluePlastic, false));
            }
            Box(g, "Nắp có điện cực", new Vector3(0, 0.085f, 0), new Vector3(0.31f, 0.008f, 0.17f), Mat("T2_NapDienDi", new Color(0.85f, 0.9f, 0.95f, 0.4f), 0.8f, 0, transparent: true), false);
            Cyl(g, "Cực dương", new Vector3(0.13f, 0.1f, 0.06f), 0.006f, 0.03f, red);
            Cyl(g, "Cực âm", new Vector3(-0.13f, 0.1f, 0.06f), 0.006f, 0.03f, blackPlastic);
            var psu = Group("Nguồn điện di", g, new Vector3(0.32f, 0, -0.02f));
            RoundBox(psu, "Thân nguồn", new Vector3(0, 0.06f, 0), new Vector3(0.22f, 0.12f, 0.2f), 0.01f, bioWhite);
            NoShadow(Box(psu, "Màn số", new Vector3(0, 0.08f, 0.101f), new Vector3(0.12f, 0.035f, 0.004f), bioDisplayRed, false));
            Label(psu, "100 V", new Vector3(0, 0.082f, 0.105f), Vector3.forward, 0.018f, new Color(1f, 0.45f, 0.35f));
            Cyl(g, "Dây đỏ", new Vector3(0.22f, 0.11f, 0.06f), 0.003f, 0.18f, red, false, new Vector3(0, 0, 80));
            Cyl(g, "Dây đen", new Vector3(0.08f, 0.13f, 0.03f), 0.003f, 0.42f, blackPlastic, false, new Vector3(0, 0, 85));
            Info(g, "Điện di gel agarose", "ADN tích điện âm chạy về cực dương (đỏ); đoạn ngắn chạy nhanh hơn: quãng đường tỉ lệ gần tuyến tính với log(kích thước).", "Gel 1 – 2 %, 100 V, 30 – 45 phút; chạy đến khi vạch thuốc nhuộm xanh đi được 2/3 bản gel.");
        }

        /// <summary>Buồng chụp gel: hộp tối, cửa trước có kính lọc cam, bàn soi ánh sáng xanh lam, màn hình hiện ảnh gel.</summary>
        static void GelDoc(Transform p, Vector3 pos)
        {
            var g = Group("Buồng chụp gel", p, pos);
            RoundBox(g, "Hộp tối", new Vector3(0, 0.28f, 0), new Vector3(0.45f, 0.56f, 0.45f), 0.012f, Mat("T2_VoChupGel", new Color(0.15f, 0.16f, 0.18f), 0.45f));
            NoShadow(Box(g, "Kính lọc cam", new Vector3(0, 0.3f, 0.227f), new Vector3(0.24f, 0.16f, 0.004f), Mat("T2_KinhLocCam", new Color(1f, 0.55f, 0.1f, 0.6f), 0.9f, 0, emission: new Color(0.35f, 0.15f, 0.02f), transparent: true), false));
            NoShadow(Box(g, "Bàn soi (sáng)", new Vector3(0, 0.07f, 0.05f), new Vector3(0.3f, 0.01f, 0.25f), bioUvGlow, false));
            var mon = Group("Màn hình ảnh gel", g, new Vector3(0.55f, 0, -0.1f), -20f);
            Box(mon, "Chân", new Vector3(0, 0.12f, 0), new Vector3(0.04f, 0.24f, 0.04f), darkMetal, false);
            Box(mon, "Viền", new Vector3(0, 0.36f, 0), new Vector3(0.42f, 0.27f, 0.025f), blackPlastic, false);
            var gelTex = Tex("T2_AnhGel", 128, 96, GelImagePixel, repeat: false);
            NoShadow(Panel(mon, "Ảnh gel", new Vector3(0, 0.36f, 0.0135f), Vector3.forward, 0.39f, 0.24f, EmissiveTexMat("T2_ManHinhAnhGel", gelTex, new Color(0.85f, 0.85f, 0.85f), Vector2.one)));
            Info(g, "Buồng chụp gel (gel doc)", "Chiếu ánh sáng xanh lam / UV làm thuốc nhuộm gắn ADN phát quang, camera chụp các vạch ADN; so với thang chuẩn để biết kích thước đoạn.");
        }

        /// <summary>Ảnh gel: nền đen, 6 giếng, thang chuẩn ở giếng đầu, các vạch phát sáng.</summary>
        static Color GelImagePixel(int x, int y)
        {
            var c = new Color(0.03f, 0.03f, 0.04f);
            int lane = (x - 10) / 18, lx = (x - 10) % 18;
            if (x < 10 || lane > 5 || lx > 13) return c;
            if (y > 86 && y < 90) return new Color(0.25f, 0.25f, 0.28f);  // giếng
            float glow = 0;
            if (lane == 0)
                foreach (int b in new[] { 78, 70, 62, 52, 40, 28, 16 }) glow = Mathf.Max(glow, 1f - Mathf.Abs(y - b) / 2.2f);
            else
            {
                int b = 30 + (lane * 13) % 40;
                glow = Mathf.Max(0, 1f - Mathf.Abs(y - b) / 2.5f);
                if (lane % 2 == 0) glow = Mathf.Max(glow, 1f - Mathf.Abs(y - (b + 22)) / 2.5f);
            }
            if (glow <= 0) return c;
            return Color.Lerp(c, new Color(0.95f, 0.9f, 1f), glow);
        }

        /// <summary>Máy quang phổ vi thể / đo OD600: thân trắng, cần mẫu, màn hình nghiêng.</summary>
        static void Spectro(Transform p, Vector3 pos)
        {
            var g = Group("Máy đo quang phổ (OD600)", p, pos, 90f);
            RoundBox(g, "Thân máy", new Vector3(0, 0.07f, 0), new Vector3(0.3f, 0.14f, 0.24f), 0.015f, bioWhite);
            Box(g, "Cần mẫu", new Vector3(-0.08f, 0.16f, 0), new Vector3(0.06f, 0.04f, 0.05f), bioChrome, false);
            NoShadow(Box(g, "Màn hình", new Vector3(0.1f, 0.12f, 0), new Vector3(0.004f, 0.08f, 0.16f), bioDisplayBlue, false, new Vector3(0, 0, 25)));
            Label(g, "OD600 = 0,42", new Vector3(0.106f, 0.122f, 0), Vector3.right, 0.016f, new Color(0.7f, 0.85f, 1f));
            Info(g, "Máy đo quang phổ (OD600, nồng độ ADN)", "Đo độ hấp thụ ánh sáng 600 nm của dịch nuôi cấy để ước lượng mật độ vi khuẩn; đo A260 / A280 để kiểm tra độ tinh sạch ADN.");
        }

        // =====================================================================
        // Nhà kính thực vật (x 13,5..27, z −9..−2,2)
        // =====================================================================

        static void BuildGreenhouse(Transform f2)
        {
            var gh = Group("Nhà kính thực vật", f2);
            var lights = Group("Đèn", gh);
            RoomLights(lights, new[] { 17.0f, 23.5f }, new[] { -5.6f }, 8f, 2.2f);

            // 2 dãy bàn trồng cây di động dọc trục x, lối đi giữa x 18,6..21 thẳng cửa vào
            float[] rowsZ = { -6.6f, -4.6f };
            for (int r = 0; r < rowsZ.Length; r++)
                foreach (var (x0, x1) in new[] { (14.6f, 18.4f), (21.2f, 26.2f) })
                    GrowBench(gh, $"Bàn trồng cây {r + 1}", new Vector3((x0 + x1) / 2, 0, rowsZ[r]), x1 - x0, r * 2 + (x0 > 20 ? 1 : 0));

            // giàn thủy canh NFT 3 tầng có đèn LED trồng cây (sát đầu hồi tây)
            NftRack(gh, new Vector3(13.95f, 0, -5.6f));
            // trạm thí nghiệm dọc vách kính nam
            PhotosynthesisStation(gh, new Vector3(22.6f, 0, -8.45f));
            PotometerStation(gh, new Vector3(19.8f, 0, -8.45f));
            PhototropismStation(gh, new Vector3(25.3f, 0, -8.45f));
            // bàn sang chậu gần cửa, cảm biến vi khí hậu, rãnh thoát nước
            var pot = Group("Bàn sang chậu", gh, new Vector3(16.0f, 0, -2.75f));
            Box(pot, "Mặt bàn", new Vector3(0, 0.8f, 0), new Vector3(1.6f, 0.04f, 0.6f), oak);
            foreach (int sx in new[] { -1, 1 })
                foreach (int sz in new[] { -1, 1 })
                    Box(pot, "Chân", new Vector3(sx * 0.75f, 0.39f, sz * 0.25f), new Vector3(0.04f, 0.78f, 0.04f), alu, false);
            Box(pot, "Bao đất", new Vector3(-0.5f, 0.92f, 0), new Vector3(0.35f, 0.2f, 0.45f), bioKraft, false);
            for (int i = 0; i < 3; i++) Plant(pot, new Vector3(0.0f + i * 0.2f, 0.82f, 0.05f), 3, 1f);
            var can = Group("Bình tưới", pot, new Vector3(0.6f, 0, 0));
            Cyl(can, "Thân bình", new Vector3(0, 0.12f, 0), 0.08f, 0.24f, Mat("T2_BinhTuoi", new Color(0.2f, 0.55f, 0.35f), 0.4f));
            Cyl(can, "Vòi bình", new Vector3(0.12f, 0.2f, 0), 0.012f, 0.25f, Mat("T2_BinhTuoi", new Color(0.2f, 0.55f, 0.35f), 0.4f), false, new Vector3(0, 0, -55));
            var sensor = Group("Cảm biến vi khí hậu", gh, new Vector3(RoomSplitX + 0.12f, 1.55f, -3.1f), 90f);
            Box(sensor, "Hộp", Vector3.zero, new Vector3(0.3f, 0.22f, 0.05f), bioWhite, false);
            NoShadow(Box(sensor, "Màn", new Vector3(0, 0.01f, 0.026f), new Vector3(0.24f, 0.12f, 0.004f), bioDisplayGreen, false));
            Label(sensor, "28,5 °C · 72 %\nÁNH SÁNG 18 klx", new Vector3(0, 0.01f, 0.03f), Vector3.forward, 0.018f, new Color(0.6f, 1f, 0.65f));
            Info(sensor, "Cảm biến vi khí hậu nhà kính", "Đo nhiệt độ, độ ẩm, cường độ ánh sáng; tự bật phun sương khi độ ẩm dưới 60 %, kéo rèm che khi nắng quá 40 klx.");
            NoShadow(Box(gh, "Rãnh thoát nước", new Vector3(19.8f, 0.002f, -5.6f), new Vector3(0.18f, 0.004f, 6.4f), Mat("T2_NapRanh", new Color(0.2f, 0.21f, 0.22f), 0.5f, 0.6f), false));
            Plant(gh, new Vector3(26.4f, 0, -2.75f), 4, 1.3f, bioPotGrey);
            Plant(gh, new Vector3(14.1f, 0, -8.55f), 0, 1.5f, bioPotGrey);
            var sign = Group("Biển nhà kính", gh, new Vector3(19.8f, 2.95f, -2.45f));
            Box(sign, "Nền", Vector3.zero, new Vector3(1.8f, 0.28f, 0.03f), Mat("T2_BienNhaKinh", zoneGreenhouse * 0.8f, 0.4f), false);
            Label(sign, "NHÀ KÍNH THỰC VẬT · GREENHOUSE", new Vector3(0, 0, -0.017f), Vector3.back, 0.055f, Color.white);
        }

        /// <summary>Bàn trồng cây di động: khung nhôm, mặt lưới, hai hàng chậu cây.</summary>
        static void GrowBench(Transform p, string name, Vector3 pos, float len, int seed)
        {
            var g = Group(name, p, pos);
            const float d = 1.0f, top = 0.78f;
            Box(g, "Mặt lưới", new Vector3(0, top, 0), new Vector3(len, 0.03f, d), Mat("T2_LuoiBanTrong", new Color(0.5f, 0.52f, 0.54f), 0.4f, 0.6f));
            foreach (int s in new[] { -1, 1 })
            {
                Box(g, "Viền dọc", new Vector3(0, top + 0.03f, s * d / 2), new Vector3(len, 0.06f, 0.03f), alu, false);
                for (int i = 0; i <= 2; i++)
                    Box(g, "Chân", new Vector3(-len / 2 + 0.1f + i * (len - 0.2f) / 2, top / 2, s * (d / 2 - 0.05f)), new Vector3(0.04f, top, 0.04f), alu, false);
            }
            Box(g, "Thanh giằng", new Vector3(0, 0.25f, 0), new Vector3(len, 0.03f, 0.03f), alu, false);
            var r = new System.Random(seed * 31 + 7);
            int n = Mathf.FloorToInt(len / 0.4f);
            for (int row = 0; row < 2; row++)
                for (int i = 0; i < n; i++)
                {
                    float x = -len / 2 + 0.2f + i * (len - 0.4f) / Mathf.Max(1, n - 1);
                    int kind = (seed + i + row) % 5 == 0 ? 1 : (seed % 3 == 0 ? 2 : (r.Next(3) == 0 ? 3 : 0));
                    Plant(g, new Vector3(x, top + 0.015f, row == 0 ? -0.25f : 0.25f), kind, 0.6f + 0.25f * (float)r.NextDouble());
                }
        }

        /// <summary>Giàn thủy canh NFT 3 tầng: ống PVC vuông có rau xà lách, đèn LED trồng cây, bể dung dịch và bơm.</summary>
        static void NftRack(Transform p, Vector3 pos)
        {
            var g = Group("Giàn thủy canh NFT", p, pos);
            const float len = 5.0f;
            foreach (float z in new[] { -len / 2, 0f, len / 2 })
                Box(g, "Khung đứng", new Vector3(0.15f, 1.0f, z), new Vector3(0.04f, 2.0f, 0.04f), alu, false);
            var pvc = Mat("T2_OngNFT", new Color(0.95f, 0.95f, 0.95f), 0.45f);
            var grow = Mat("T2_DenTrongCay", new Color(0.95f, 0.85f, 0.95f), 0.5f, 0, emission: new Color(1.6f, 0.9f, 1.7f));
            var lettuce = PlantMesh("XaLach", 12, 0.1f, 0.08f, 0.05f, 0.02f);
            for (int t = 0; t < 3; t++)
            {
                float y = 0.45f + t * 0.6f;
                foreach (float x in new[] { 0.05f, 0.27f })
                {
                    Box(g, "Ống NFT", new Vector3(x, y, 0), new Vector3(0.1f, 0.07f, len), pvc);
                    for (int i = 0; i < 12; i++)
                    {
                        float z = -len / 2 + 0.25f + i * (len - 0.5f) / 11f;
                        var leaves = MeshObj(g, "Xà lách thủy canh", lettuce, new Vector3(x, y + 0.04f, z), bioLeafLight, false, null, 0.9f + 0.1f * ((i + t) % 3));
                        leaves.transform.localRotation = Quaternion.Euler(0, (i * 47 + t * 13) % 360, 0);
                        NoShadow(leaves);
                    }
                }
                if (t > 0) NoShadow(Box(g, "Đèn LED trồng cây", new Vector3(0.16f, y - 0.1f, 0), new Vector3(0.12f, 0.02f, len - 0.2f), grow, false));
            }
            NoShadow(Box(g, "Đèn LED trồng cây trên", new Vector3(0.16f, 2.05f, 0), new Vector3(0.12f, 0.02f, len - 0.2f), grow, false));
            Box(g, "Bể dung dịch", new Vector3(0.18f, 0.15f, -len / 2 + 0.45f), new Vector3(0.4f, 0.3f, 0.6f), Mat("T2_BeDinhDuong", new Color(0.15f, 0.35f, 0.55f), 0.4f));
            Cyl(g, "Ống hồi", new Vector3(0.3f, 0.95f, len / 2 - 0.1f), 0.015f, 1.6f, blackPlastic);
            Info(g, "Giàn thủy canh NFT", "Dung dịch dinh dưỡng (N, P, K, vi lượng) chảy thành lớp mỏng dưới rễ xà lách; bơm tuần hoàn từ bể. Đèn LED đỏ – xanh lam đúng phổ hấp thụ của diệp lục.");
        }

        /// <summary>Trạm quang hợp: cốc rong đuôi chó, phễu úp, ống nghiệm thu khí O₂, đèn trên thước trượt; bọt khí sinh theo cường độ sáng.</summary>
        static void PhotosynthesisStation(Transform p, Vector3 pos)
        {
            var g = Group("Thí nghiệm quang hợp thải O₂", p, pos);
            var table = StationTable(g, 1.2f);
            float top = 0.8f;
            var beaker = Group("Cốc rong đuôi chó", table, new Vector3(0, top, 0));
            NoShadow(MeshObj(beaker, "Cốc 1000 ml", Beaker("Coc1000", 0.065f, 0.2f), Vector3.zero, labGlass));
            NoShadow(Cyl(beaker, "Nước", new Vector3(0, 0.085f, 0), 0.062f, 0.17f, bioWater));
            for (int i = 0; i < 4; i++)
                Cyl(beaker, "Cành rong", new Vector3((i - 1.5f) * 0.012f, 0.07f, 0.01f * (i % 2)), 0.002f, 0.13f, bioLeafLight, false, new Vector3((i - 1.5f) * 8, 0, 0));
            NoShadow(MeshObj(beaker, "Phễu úp", Lathe("T2_PheuUp", 20, V(0.05f, 0), V(0.052f, 0.004f), V(0.012f, 0.06f), V(0.01f, 0.1f), V(0.008f, 0.1f), V(0.009f, 0.06f), V(0.048f, 0.003f)), new Vector3(0, 0.01f, 0), labGlass));
            NoShadow(MeshObj(beaker, "Ống thu khí", TestTube(), new Vector3(0, 0.27f, 0), labGlass, false, new Vector3(180, 0, 0)));
            var stem = Group("Gốc cành rong", beaker, new Vector3(0, 0.12f, 0));
            // đèn trên thước trượt
            Box(table, "Thước trượt", new Vector3(-0.3f, top + 0.005f, 0), new Vector3(0.5f, 0.01f, 0.04f), signWhite, false);
            var lamp = Group("Đèn chiếu", table, new Vector3(-0.3f, top, 0));
            Cyl(lamp, "Chân đèn", new Vector3(0, 0.12f, 0), 0.008f, 0.24f, darkMetal);
            Cyl(lamp, "Chụp đèn", new Vector3(0.03f, 0.24f, 0), 0.04f, 0.06f, darkMetal, false, new Vector3(0, 0, 90));
            NoShadow(Cyl(lamp, "Bóng đèn", new Vector3(0.062f, 0.24f, 0), 0.03f, 0.004f, Mat("T2_BongDenVang", Color.white, 0.5f, 0, emission: new Color(3f, 2.7f, 2.1f)), false, new Vector3(0, 0, 90)));
            var bub = g.gameObject.AddComponent<ElodeaBubbles>();
            bub.lampTransform = lamp;
            bub.stemOrigin = stem;
            Label(table, "QUANG HỢP THẢI O₂ · RONG ĐUÔI CHÓ", new Vector3(0, top - 0.06f, 0.301f), Vector3.forward, 0.025f, new Color(0.1f, 0.3f, 0.12f));
            Info(g, "Thí nghiệm quang hợp thải O₂", "Rong đuôi chó dưới ánh đèn thải bọt khí O₂ thu vào ống nghiệm úp. Đèn càng gần (ánh sáng càng mạnh) bọt càng nhiều, tới khi đạt bão hòa ánh sáng.",
                 "Đếm số bọt mỗi phút ở các khoảng cách 10, 20, 30 cm. Cường độ sáng giảm theo bình phương khoảng cách.");
        }

        static void PotometerStation(Transform p, Vector3 pos)
        {
            var g = Group("Thí nghiệm thoát hơi nước (potometer)", p, pos);
            var table = StationTable(g, 1.2f);
            float top = 0.8f;
            Cyl(table, "Giá đỡ", new Vector3(-0.2f, top + 0.25f, -0.1f), 0.008f, 0.5f, darkMetal);
            NoShadow(Cyl(table, "Ống mao dẫn", new Vector3(0.05f, top + 0.12f, -0.1f), 0.004f, 0.5f, labGlass, false, new Vector3(0, 0, 90)));
            Box(table, "Thước mm", new Vector3(0.05f, top + 0.105f, -0.1f), new Vector3(0.5f, 0.02f, 0.004f), signWhite, false);
            Plant(table, new Vector3(-0.2f, top + 0.3f, -0.1f), 3, 1.5f, labGlass);
            Box(table, "Giấy coban clorua", new Vector3(0.3f, top + 0.003f, 0.12f), new Vector3(0.08f, 0.002f, 0.06f), Mat("T2_GiayCoban", new Color(0.35f, 0.5f, 0.85f), 0.2f), false);
            Label(table, "THOÁT HƠI NƯỚC · POTOMETER", new Vector3(0, top - 0.06f, 0.301f), Vector3.forward, 0.025f, new Color(0.1f, 0.3f, 0.12f));
            Info(g, "Thí nghiệm thoát hơi nước", "Bọt khí trong ống mao dẫn dịch chuyển theo lượng nước cành cây hút để bù thoát hơi nước. Giấy coban clorua xanh hóa hồng khi gặp hơi nước ở mặt dưới lá (nhiều khí khổng).");
        }

        static void PhototropismStation(Transform p, Vector3 pos)
        {
            var g = Group("Thí nghiệm hướng sáng", p, pos);
            var table = StationTable(g, 1.0f);
            float top = 0.8f;
            var box = Group("Hộp tối có khe sáng", table, new Vector3(0.1f, top, 0));
            var dark = Mat("T2_HopCartonDen", new Color(0.1f, 0.1f, 0.11f), 0.2f);
            Box(box, "Đáy", new Vector3(0, 0.005f, 0), new Vector3(0.3f, 0.01f, 0.3f), dark);
            foreach (int s in new[] { -1, 1 })
                Box(box, "Vách", new Vector3(0, 0.17f, s * 0.145f), new Vector3(0.3f, 0.34f, 0.01f), dark);
            Box(box, "Vách sau", new Vector3(-0.145f, 0.17f, 0), new Vector3(0.01f, 0.34f, 0.28f), dark);
            Box(box, "Vách có khe (dưới)", new Vector3(0.145f, 0.08f, 0), new Vector3(0.01f, 0.16f, 0.28f), dark);
            Box(box, "Vách có khe (trên)", new Vector3(0.145f, 0.28f, 0), new Vector3(0.01f, 0.12f, 0.28f), dark);
            Box(box, "Nắp hé", new Vector3(-0.05f, 0.35f, 0), new Vector3(0.2f, 0.01f, 0.3f), dark, false);
            for (int i = 0; i < 3; i++)
            {
                var sprout = Group("Cây mầm nghiêng về khe sáng", box, new Vector3(-0.06f + i * 0.04f, 0.01f, -0.06f + i * 0.06f));
                sprout.localRotation = Quaternion.Euler(0, 0, -20f);
                Plant(sprout, Vector3.zero, 3, 1.1f);
            }
            for (int i = 0; i < 3; i++) Plant(table, new Vector3(-0.35f, top, -0.1f + i * 0.1f), 3, 1.1f);
            Label(table, "HƯỚNG SÁNG", new Vector3(0, top - 0.06f, 0.301f), Vector3.forward, 0.025f, new Color(0.1f, 0.3f, 0.12f));
            Info(g, "Thí nghiệm hướng sáng", "Cây mầm trong hộp tối có khe sáng mọc cong về phía khe: auxin dồn về phía khuất sáng làm phía đó dài nhanh hơn. Cây ngoài hộp mọc thẳng (đối chứng).");
        }

        /// <summary>Bàn thí nghiệm nhỏ trong nhà kính (mặt 0,8 m), trả về nhóm bàn để đặt đồ lên (y = 0 là sàn).</summary>
        static Transform StationTable(Transform g, float len)
        {
            var t = Group("Bàn thí nghiệm", g);
            Box(t, "Mặt bàn", new Vector3(0, 0.78f, 0), new Vector3(len, 0.04f, 0.6f), epoxy);
            foreach (int sx in new[] { -1, 1 })
                foreach (int sz in new[] { -1, 1 })
                    Box(t, "Chân", new Vector3(sx * (len / 2 - 0.05f), 0.38f, sz * 0.25f), new Vector3(0.04f, 0.76f, 0.04f), alu, false);
            Box(t, "Diềm trước", new Vector3(0, 0.71f, 0.29f), new Vector3(len, 0.1f, 0.02f), Mat("T2_DiemBan", zoneGreenhouse, 0.4f), false);
            return t;
        }

        // =====================================================================
        // Hành lang đông tầng 2 (x 27..30, z 6..15): nội thất (vỏ ở BuildCoreShell)
        // =====================================================================

        static void BuildEastCorridor(Transform f2)
        {
            var cor = Group("Hành lang đông tầng 2", f2);
            var lights = Group("Đèn", cor);
            LedPanel(lights, "Đèn hành lang đông", 28.5f, 8.2f, 2.0f, 6.5f);
            LedPanel(lights, "Đèn hành lang đông", 28.5f, 12.6f, 2.0f, 6.5f);
            // biển phòng vi sinh (cấp an toàn sinh học 2) cạnh cửa buồng đệm
            T2Sign(cor, "PHÒNG VI SINH · BSL-2", "MICROBIOLOGY · AUTHORISED ONLY", new Vector3(StoreX + 0.103f, 2.55f, 8.8f), Vector3.right, zoneMicroBio);
            var bio = Mat("T2_BienBiohazard", Color.white, 0.3f, 0, Tex("T2_BieuTuongBiohazard", 128, 128, BiohazardPixel, repeat: false));
            NoShadow(Panel(cor, "Biểu tượng nguy hại sinh học", new Vector3(StoreX + 0.104f, 1.6f, 9.75f), Vector3.right, 0.3f, 0.3f, bio));
            // tường thông tin phân loại sinh giới
            var wall = Group("Tường phân loại sinh giới", cor, new Vector3(StoreX + 0.1f, 0, 12.4f), 90f);
            Box(wall, "Nền", new Vector3(0, 1.7f, 0.006f), new Vector3(3.2f, 1.6f, 0.012f), signWhite, false);
            NoShadow(Panel(wall, "Áp phích cây sự sống", new Vector3(-0.9f, 1.65f, 0.014f), Vector3.forward, 1.0f, 1.375f, posterBio[2]));
            Label(wall, "HỆ THỐNG PHÂN LOẠI SINH GIỚI", new Vector3(0.6f, 2.3f, 0.014f), Vector3.forward, 0.06f, new Color(0.08f, 0.42f, 0.22f));
            Label(wall, "5 GIỚI (Whittaker, 1969)\nKhởi sinh · Nguyên sinh · Nấm\nThực vật · Động vật\n\n3 LÃNH GIỚI (Woese, 1990)\nVi khuẩn · Cổ khuẩn · Nhân thực",
                  new Vector3(0.6f, 1.65f, 0.014f), Vector3.forward, 0.04f, new Color(0.2f, 0.22f, 0.25f));
            Info(wall, "Hệ thống phân loại sinh giới", "Hệ thống 5 giới (Whittaker) và 3 lãnh giới (Woese) theo chương trình Sinh học 10.");
            // ghế nghỉ cạnh vách kính, cây xanh
            foreach (float z in new[] { 7.6f, 11.2f })
            {
                var b = Group("Ghế nghỉ", cor, new Vector3(29.45f, 0, z));
                Box(b, "Mặt ghế", new Vector3(0, 0.44f, 0), new Vector3(0.42f, 0.05f, 1.5f), oak);
                foreach (float dz in new[] { -0.65f, 0.65f })
                    Box(b, "Chân ghế", new Vector3(0, 0.21f, dz), new Vector3(0.38f, 0.42f, 0.05f), darkMetal, false);
            }
            Plant(cor, new Vector3(29.4f, 0, 14.4f), 4, 1.1f);
            Plant(cor, new Vector3(29.4f, 0, 9.4f), 0, 1.2f, potWhite);
        }
    }
}
