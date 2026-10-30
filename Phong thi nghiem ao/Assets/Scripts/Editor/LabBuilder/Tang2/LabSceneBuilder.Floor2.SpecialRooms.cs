using UnityEngine;

namespace Khkt.EditorTools
{
    public static partial class LabSceneBuilder
    {
        public static void BuildFloor2SpecialRooms(GameObject parent)
        {
            // 1. PHÒNG VI SINH (x 20..27, z 7.5..15)
            BuildMicrobiologyRoom(parent);

            // 2. KHO SINH PHẨM (x 20..27, z 0..7.5)
            BuildBioStorageRoom(parent);

            // 3. PHÒNG SINH HỌC PHÂN TỬ (x 0..13.5, z -9..-2.2)
            BuildMolecularBioRoom(parent);

            // 4. NHÀ KÍNH THỰC VẬT (x 13.5..27, z -9..-2.2)
            BuildPlantGreenhouse(parent);

            // 5. HÀNH LANG ĐÔNG (x 27..30, z 6..15)
            BuildEastCorridor(parent);
        }

        // ==========================================
        // 1. PHÒNG VI SINH (MICROBIOLOGY ROOM)
        // ==========================================
        static void BuildMicrobiologyRoom(GameObject parent)
        {
            var room = new GameObject("Phòng vi sinh");
            room.transform.SetParent(parent.transform, false);

            // Buồng đệm khí sạch (Airlock, x 25.2..27, z 7.5..10)
            var airlock = LabKit.Box(room, "BuongDem", new Vector3(26.1f, 1.8f, 8.75f), new Vector3(1.8f, 3.6f, 2.5f), LabKit.Mat("VachBuongDem", new Color(0.9f, 0.92f, 0.95f, 0.4f), smoothness: 0.9f));
            LabKit.Label(airlock, "BienVungSach", "VÙNG SẠCH - AIRLOCK\nKHỬ TRÙNG TRƯỚC KHI VÀO", new Vector3(25.15f, 2.1f, 8.75f), 0.025f, Color.blue);

            // Đèn UV diệt khuẩn gắn trần
            var uvMat = LabKit.Mat("DenUV", new Color(0.6f, 0.2f, 0.95f), emission: new Color(0.5f, 0.1f, 0.9f) * 1.5f);
            LabKit.Cyl(room, "DenUV_DietKhuan", new Vector3(23.5f, 3.55f, 11.25f), 0.025f, 1.2f, uvMat);
            LabKit.Label(room, "CanhBaoUV", "CẢNH BÁO: ĐÈN UV DIỆT KHUẨN (TẮT KHI CÓ NGƯỜI)", new Vector3(23.5f, 3.3f, 11.25f), 0.02f, Color.magenta);

            // Đảo nhuộm Gram trung tâm (x = 23.5, z = 11.25)
            Vector3 islandPos = new Vector3(23.5f, 0, 11.25f);
            var islandMat = t2BenchToiGian ?? LabKit.Mat("BanNhuomGram", new Color(0.2f, 0.22f, 0.25f), smoothness: 0.4f);
            var island = LabKit.RoundBox(room, "DaoNhuomGram", islandPos + new Vector3(0, 0.4f, 0), new Vector3(1.4f, 0.8f, 2.0f), 0.008f, islandMat);

            // Bộ thuốc nhuộm Gram 4 chai (Crystal violet, Iodine, Cồn tẩy màu, Safranin)
            var matViolet = LabKit.Mat("ThuocNhuom_Tim", new Color(0.35f, 0.1f, 0.6f));
            var matIodine = LabKit.Mat("ThuocNhuom_Iot", new Color(0.65f, 0.35f, 0.1f));
            var matAlcohol = LabKit.Mat("Con_TayMau", new Color(0.85f, 0.9f, 0.95f));
            var matSafranin = LabKit.Mat("ThuocNhuom_Do", new Color(0.9f, 0.2f, 0.3f));

            LabKit.Cyl(island, "Chai_CrystalViolet", islandPos + new Vector3(-0.25f, 0.86f, -0.2f), 0.022f, 0.09f, matViolet);
            LabKit.Cyl(island, "Chai_LugolIodine", islandPos + new Vector3(-0.1f, 0.86f, -0.2f), 0.022f, 0.09f, matIodine);
            LabKit.Cyl(island, "Chai_Ethanol95", islandPos + new Vector3(0.05f, 0.86f, -0.2f), 0.022f, 0.09f, matAlcohol);
            LabKit.Cyl(island, "Chai_Safranin", islandPos + new Vector3(0.2f, 0.86f, -0.2f), 0.022f, 0.09f, matSafranin);

            // Khay nhuộm và que cấy vi sinh
            LabKit.Box(island, "KhayNhuomInox", islandPos + new Vector3(0, 0.81f, 0.2f), new Vector3(0.3f, 0.02f, 0.45f), LabKit.Mat("Inox304", new Color(0.85f, 0.87f, 0.88f), metallic: 0.9f, smoothness: 0.8f));

            // Tủ an toàn sinh học cấp II phòng vi sinh (tường bắc x = 23.5, z = 14.3)
            BuildBiosafetyCabinet(room, "TuAnToan_ViSinh", new Vector3(23.5f, 0, 14.3f));

            // Pass-box thông sang phòng TN (tường tây phòng vi sinh x = 20.0, z = 10.0, y = 1.0)
            var passBox = LabKit.Box(room, "PassBox", new Vector3(20.0f, 1.1f, 10.0f), new Vector3(0.45f, 0.45f, 0.45f), LabKit.Mat("PassBoxInox", new Color(0.88f, 0.89f, 0.91f), metallic: 0.8f, smoothness: 0.7f));
            LabKit.Label(passBox, "NhanPassBox", "PASS-BOX 2 CHIỀU", new Vector3(20.24f, 1.1f, 10.0f), 0.015f, Color.black);
        }

        // ==========================================
        // 2. KHO SINH PHẨM (BIO STORAGE ROOM)
        // ==========================================
        static void BuildBioStorageRoom(GameObject parent)
        {
            var room = new GameObject("Kho sinh phẩm");
            room.transform.SetParent(parent.transform, false);

            // 1. Tủ lạnh 4°C 2 cánh kính bảo quản sinh phẩm (x = 21.5, z = 6.6)
            var fridgeMat = LabKit.Mat("TuLanh_Than", new Color(0.92f, 0.93f, 0.94f), smoothness: 0.6f);
            var fridgeGlass = LabKit.Mat("TuLanh_Kinh", new Color(0.85f, 0.92f, 0.95f, 0.5f), smoothness: 0.95f);
            var fridge = LabKit.RoundBox(room, "TuLanh_4C", new Vector3(21.5f, 1.0f, 6.6f), new Vector3(0.8f, 2.0f, 1.4f), 0.01f, fridgeMat);
            LabKit.Box(fridge, "CanhKinh", new Vector3(21.5f, 1.0f, 6.19f), new Vector3(0.72f, 1.7f, 0.02f), fridgeGlass);
            // Màn LED 4.0 °C
            var led4c = LabKit.Mat("LED_4C", Color.black, emission: new Color(0.2f, 0.6f, 1.0f) * 2.0f);
            LabKit.Box(fridge, "ManLED_4C", new Vector3(21.5f, 1.9f, 6.18f), new Vector3(0.2f, 0.06f, 0.01f), led4c);
            LabKit.Label(fridge, "So_4C", "4.0 °C", new Vector3(21.5f, 1.9f, 6.17f), 0.022f, Color.cyan);

            // 2. Tủ đông âm sâu -20°C (x = 24.5, z = 6.6)
            var freezerMat = LabKit.Mat("TuDong_Than", new Color(0.88f, 0.90f, 0.92f), smoothness: 0.5f);
            var freezer = LabKit.RoundBox(room, "TuDong_Am20C", new Vector3(24.5f, 1.0f, 6.6f), new Vector3(0.8f, 2.0f, 1.2f), 0.01f, freezerMat);
            var led20c = LabKit.Mat("LED_20C", Color.black, emission: new Color(0.1f, 0.9f, 0.95f) * 2.5f);
            LabKit.Box(freezer, "ManLED_20C", new Vector3(24.5f, 1.9f, 6.19f), new Vector3(0.2f, 0.06f, 0.01f), led20c);
            LabKit.Label(freezer, "So_20C", "-20.0 °C", new Vector3(24.5f, 1.9f, 6.18f), 0.022f, Color.cyan);

            // 3. Kệ lưu trữ inox với các bình mẫu ngâm Formalin (x = 21.0..26.0, z = 1.2)
            var rackMat = LabKit.Mat("KeInox", new Color(0.8f, 0.82f, 0.85f), metallic: 0.85f, smoothness: 0.7f);
            var rack = LabKit.Box(room, "KeInoxMauNgam", new Vector3(23.5f, 1.1f, 1.2f), new Vector3(4.5f, 2.2f, 0.6f), rackMat);

            // 4. Mô hình chuỗi xoắn kép ADN trên bàn trưng bày (x = 23.5, z = 3.8)
            var dnaTable = LabKit.RoundBox(room, "BanTrungBayADN", new Vector3(23.5f, 0.45f, 3.8f), new Vector3(1.2f, 0.9f, 1.2f), 0.008f, t2BenchToiGian ?? LabKit.Mat("BanADN", Color.white, smoothness: 0.5f));
            BuildDnaModel(dnaTable, "MoHinhADN", new Vector3(23.5f, 0.9f, 3.8f));
        }

        static void BuildDnaModel(GameObject parent, string name, Vector3 pos)
        {
            var dna = new GameObject(name);
            dna.transform.SetParent(parent.transform, false);

            var matBaseRed = LabKit.Mat("ADN_A", new Color(0.9f, 0.2f, 0.2f));
            var matBaseBlue = LabKit.Mat("ADN_T", new Color(0.2f, 0.4f, 0.9f));
            var matBaseGreen = LabKit.Mat("ADN_G", new Color(0.2f, 0.8f, 0.3f));
            var matBaseYellow = LabKit.Mat("ADN_C", new Color(0.95f, 0.85f, 0.1f));
            var matSpine = LabKit.Mat("ADN_KhungDuong", new Color(0.85f, 0.87f, 0.9f), metallic: 0.3f, smoothness: 0.6f);

            // Trục xoắn 12 nấc thang
            int rungs = 14;
            float height = 0.65f;
            float radius = 0.09f;

            for (int i = 0; i < rungs; i++)
            {
                float angle = i * 36f * Mathf.Deg2Rad;
                float y = (float)i / rungs * height;
                Vector3 p1 = pos + new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
                Vector3 p2 = pos + new Vector3(Mathf.Cos(angle + Mathf.PI) * radius, y, Mathf.Sin(angle + Mathf.PI) * radius);

                // Cặp bazo nitơ (A-T hoặc G-C)
                Material m1 = (i % 2 == 0) ? matBaseRed : matBaseGreen;
                Material m2 = (i % 2 == 0) ? matBaseBlue : matBaseYellow;

                LabKit.Ball(dna, $"Base1_{i}", p1, 0.015f, m1);
                LabKit.Ball(dna, $"Base2_{i}", p2, 0.015f, m2);
                LabKit.Cyl(dna, $"LienKet_{i}", (p1 + p2) * 0.5f, 0.005f, radius * 1.8f, matSpine);
            }
        }

        // ==========================================
        // 3. PHÒNG SINH HỌC PHÂN TỬ (MOLECULAR BIO)
        // ==========================================
        static void BuildMolecularBioRoom(GameObject parent)
        {
            var room = new GameObject("Phòng sinh học phân tử");
            room.transform.SetParent(parent.transform, false);

            // Dãy bàn làm việc một chiều (Unidirectional workflow):
            // Dọc tường nam z = -8.2 từ x = 1.0 đến 12.5

            // Khu 1: Tiền PCR (Chuẩn bị hóa chất, primer, dNTPs) (x = 2.0, z = -8.2)
            var benchPre = LabKit.RoundBox(room, "BanTienPCR", new Vector3(2.5f, 0.4f, -8.2f), new Vector3(2.5f, 0.8f, 0.8f), 0.006f, t2BenchQuocTe ?? LabKit.Mat("MatBanPCR", Color.white, smoothness: 0.5f));
            LabKit.Label(benchPre, "NhanTienPCR", "KHU 1: TIỀN PCR\n(CHUẨN BỊ MẪU & MỒI)", new Vector3(2.5f, 0.82f, -8.0f), 0.02f, Color.blue);
            // Giá micropipette tự động 3 cây (P20, P200, P1000)
            BuildPipetteStand(benchPre, new Vector3(2.2f, 0.8f, -8.2f));

            // Khu 2: Tách chiết ADN (x = 6.0, z = -8.2)
            var benchExtract = LabKit.RoundBox(room, "BanTachChietADN", new Vector3(6.5f, 0.4f, -8.2f), new Vector3(3.0f, 0.8f, 0.8f), 0.006f, t2BenchQuocTe ?? LabKit.Mat("MatBanPCR", Color.white, smoothness: 0.5f));
            LabKit.Label(benchExtract, "NhanTachChiet", "KHU 2: TÁCH CHIẾT ADN\n(LY TÂM & LẮNG ĐỌNG)", new Vector3(6.5f, 0.82f, -8.0f), 0.02f, Color.blue);
            // Máy ly tâm vi lượng (Microcentrifuge)
            BuildMicrocentrifuge(benchExtract, new Vector3(6.2f, 0.8f, -8.2f));

            // Khu 3: Hậu PCR & Phân tích (x = 10.5, z = -8.2)
            var benchPost = LabKit.RoundBox(room, "BanHauPCR", new Vector3(10.5f, 0.4f, -8.2f), new Vector3(3.5f, 0.8f, 0.8f), 0.006f, t2BenchQuocTe ?? LabKit.Mat("MatBanPCR", Color.white, smoothness: 0.5f));
            LabKit.Label(benchPost, "NhanHauPCR", "KHU 3: PCR & ĐIỆN DI\n(NHÂN BẢN & PHÂN TÍCH GEL)", new Vector3(10.5f, 0.82f, -8.0f), 0.02f, Color.blue);

            // 2 Máy luân nhiệt PCR (Thermal Cyclers 96 giếng)
            BuildThermalCycler(benchPost, "MayPCR_1", new Vector3(9.4f, 0.8f, -8.2f));
            BuildThermalCycler(benchPost, "MayPCR_2", new Vector3(10.2f, 0.8f, -8.2f));

            // Bể điện di gel agarose ngang (Horizontal Gel Electrophoresis)
            BuildGelElectrophoresis(benchPost, "BeDienDiGel", new Vector3(11.2f, 0.8f, -8.2f));

            // Buồng chụp gel soi UV (UV Transilluminator / Gel Doc)
            BuildGelDocDarkroom(benchPost, "BuongChupGel", new Vector3(12.0f, 0.8f, -8.2f));
        }

        static void BuildPipetteStand(GameObject parent, Vector3 pos)
        {
            var matStand = LabKit.Mat("GiaPipette", new Color(0.9f, 0.9f, 0.92f), smoothness: 0.4f);
            LabKit.Box(parent, "GiaPipette", pos + new Vector3(0, 0.15f, 0), new Vector3(0.18f, 0.3f, 0.12f), matStand);
            // 3 cây micropipette (vàng, xanh dương, xanh lục)
            LabKit.Cyl(parent, "Pipette_P20", pos + new Vector3(-0.05f, 0.18f, 0.05f), 0.01f, 0.22f, LabKit.Mat("Pipette_Vang", Color.yellow));
            LabKit.Cyl(parent, "Pipette_P200", pos + new Vector3(0f, 0.18f, 0.05f), 0.012f, 0.22f, LabKit.Mat("Pipette_Xanh", Color.blue));
            LabKit.Cyl(parent, "Pipette_P1000", pos + new Vector3(0.05f, 0.18f, 0.05f), 0.014f, 0.22f, LabKit.Mat("Pipette_Luc", Color.green));
        }

        static void BuildMicrocentrifuge(GameObject parent, Vector3 pos)
        {
            var matCent = LabKit.Mat("MayLyTam_Vo", new Color(0.95f, 0.95f, 0.96f), smoothness: 0.6f);
            var cent = LabKit.RoundBox(parent, "MayLyTam", pos + new Vector3(0, 0.12f, 0), new Vector3(0.24f, 0.22f, 0.28f), 0.008f, matCent);
            // Nắp trong suốt
            LabKit.Cyl(cent, "NapMay", pos + new Vector3(0, 0.23f, -0.02f), 0.09f, 0.02f, LabKit.Mat("NapKinhMo", new Color(0.8f, 0.85f, 0.9f, 0.6f)));
        }

        static void BuildThermalCycler(GameObject parent, string name, Vector3 pos)
        {
            var pcr = new GameObject(name);
            pcr.transform.SetParent(parent.transform, false);

            var matBody = LabKit.Mat("MayPCR_Vo", new Color(0.2f, 0.22f, 0.24f), smoothness: 0.5f);
            LabKit.RoundBox(pcr, "ThanMay", pos + new Vector3(0, 0.14f, 0), new Vector3(0.28f, 0.26f, 0.36f), 0.006f, matBody);

            // Nắp gia nhiệt (Heated lid)
            LabKit.Box(pcr, "NapGiaNhiet", pos + new Vector3(0, 0.28f, -0.04f), new Vector3(0.24f, 0.06f, 0.22f), LabKit.Mat("PCR_Nap", new Color(0.35f, 0.38f, 0.4f)));

            // Màn hình cảm ứng LCD hiển thị chu kỳ nhiệt (95°C -> 55°C -> 72°C)
            var matScreen = LabKit.Mat("PCR_ManHinh", Color.black, emission: new Color(0.1f, 0.7f, 1.0f) * 1.5f);
            LabKit.Box(pcr, "ManHinhLCD", pos + new Vector3(0, 0.22f, 0.15f), new Vector3(0.22f, 0.12f, 0.01f), matScreen);
            LabKit.Label(pcr, "ChuKyPCR", "CHU KỲ 24/35\n95°C -> 55°C -> 72°C", pos + new Vector3(0, 0.22f, 0.16f), 0.016f, Color.white);
        }

        static void BuildGelElectrophoresis(GameObject parent, string name, Vector3 pos)
        {
            var gel = new GameObject(name);
            gel.transform.SetParent(parent.transform, false);

            var matTank = LabKit.Mat("BeDienDi_NhuaTrong", new Color(0.85f, 0.92f, 0.95f, 0.45f), smoothness: 0.9f);
            // Khay bể trong suốt
            LabKit.Box(gel, "BeNhua", pos + new Vector3(0, 0.04f, 0), new Vector3(0.26f, 0.07f, 0.35f), matTank);
            // Bản gel agarose màu xanh lam nhạt trong suốt
            var matAgarose = LabKit.Mat("GelAgarose", new Color(0.6f, 0.85f, 0.95f, 0.6f), smoothness: 0.85f);
            LabKit.Box(gel, "BanGel", pos + new Vector3(0, 0.035f, 0), new Vector3(0.18f, 0.015f, 0.24f), matAgarose);

            // 2 Điện cực: Dương (+) Đỏ và Âm (-) Đen
            LabKit.Cyl(gel, "CucDuong_Do", pos + new Vector3(0.1f, 0.08f, 0.15f), 0.008f, 0.04f, LabKit.Mat("CucDo", Color.red));
            LabKit.Cyl(gel, "CucAm_Den", pos + new Vector3(-0.1f, 0.08f, -0.15f), 0.008f, 0.04f, LabKit.Mat("CucDen", Color.black));
        }

        static void BuildGelDocDarkroom(GameObject parent, string name, Vector3 pos)
        {
            var doc = new GameObject(name);
            doc.transform.SetParent(parent.transform, false);

            var matBox = LabKit.Mat("BuongChupGel_Vo", new Color(0.15f, 0.16f, 0.18f), smoothness: 0.4f);
            // Khối buồng tối soi gel UV
            LabKit.RoundBox(doc, "BuongToi", pos + new Vector3(0, 0.28f, 0), new Vector3(0.35f, 0.54f, 0.38f), 0.006f, matBox);
            // Cửa quan sát có kính lọc UV màu cam
            var filterMat = LabKit.Mat("KinhLocUV_Cam", new Color(1.0f, 0.5f, 0.05f, 0.7f), smoothness: 0.95f);
            LabKit.Box(doc, "KinhLocCam", pos + new Vector3(0, 0.35f, 0.191f), new Vector3(0.18f, 0.12f, 0.01f), filterMat);
        }

        // ==========================================
        // 4. NHÀ KÍNH THỰC VẬT (PLANT GREENHOUSE)
        // ==========================================
        static void BuildPlantGreenhouse(GameObject parent)
        {
            var gh = new GameObject("Nhà kính thực vật");
            gh.transform.SetParent(parent.transform, false);

            // 1. Hệ thống giàn thủy canh NFT 3 tầng (x = 16.0, z = -5.6)
            BuildHydroponicNFT(gh, new Vector3(16.0f, 0, -5.6f));

            // 2. Trạm thí nghiệm thoát hơi nước (Potometer) (x = 19.5, z = -7.5)
            BuildPotometerStation(gh, new Vector3(19.5f, 0, -7.5f));

            // 3. Trạm thí nghiệm quang hợp thải O2 (Rong đuôi chó Elodea) (x = 22.5, z = -7.5)
            BuildPhotosynthesisStation(gh, new Vector3(22.5f, 0, -7.5f));

            // 4. Hộp thí nghiệm tính hướng sáng (Phototropism box) (x = 25.0, z = -7.5)
            BuildPhototropismBox(gh, new Vector3(25.0f, 0, -7.5f));

            // 5. Đèn quang hợp thông minh LED treo trên giàn (quang phổ hồng/tím 450nm & 660nm)
            var growLightMat = LabKit.Mat("DenQuangHop_LED", Color.white, emission: new Color(0.95f, 0.2f, 0.85f) * 2.2f);
            LabKit.Box(gh, "DenQuangHop_1", new Vector3(16.0f, 2.8f, -5.6f), new Vector3(2.5f, 0.04f, 0.35f), growLightMat);
            LabKit.Box(gh, "DenQuangHop_2", new Vector3(21.0f, 2.8f, -5.6f), new Vector3(2.5f, 0.04f, 0.35f), growLightMat);
        }

        static void BuildHydroponicNFT(GameObject parent, Vector3 pos)
        {
            var nft = new GameObject("GianThuyCanh_NFT_3Tang");
            nft.transform.SetParent(parent.transform, false);

            var matFrame = LabKit.Mat("KhungNhomNFT", new Color(0.85f, 0.86f, 0.88f), metallic: 0.8f, smoothness: 0.6f);
            var matPvc = LabKit.Mat("OngNhuaPVC", new Color(0.96f, 0.96f, 0.97f), smoothness: 0.4f);
            var matPlant = LabKit.Mat("RauThuyCanh", new Color(0.25f, 0.72f, 0.2f), smoothness: 0.3f);

            // 3 Tầng ống PVC dẫn dinh dưỡng (cao 0.5m, 1.1m, 1.7m)
            float[] ys = { 0.5f, 1.1f, 1.7f };
            for (int t = 0; t < 3; t++)
            {
                // 3 Ống song song mỗi tầng
                for (int pipe = -1; pipe <= 1; pipe++)
                {
                    Vector3 pipePos = pos + new Vector3(pipe * 0.35f, ys[t], 0);
                    LabKit.Box(nft, $"OngPVC_T{t + 1}_{pipe}", pipePos, new Vector3(0.12f, 0.08f, 3.2f), matPvc);

                    // Cây rau xà lách thủy canh mọc dọc theo ống
                    for (int p = -4; p <= 4; p++)
                    {
                        Vector3 plantPos = pipePos + new Vector3(0, 0.08f, p * 0.32f);
                        LabKit.Ball(nft, $"CayRau_{t}_{pipe}_{p}", plantPos, 0.06f, matPlant);
                    }
                }
            }

            // Thùng chứa dung dịch dinh dưỡng tuần hoàn bên dưới
            LabKit.Box(nft, "ThungDinhDuong", pos + new Vector3(0, 0.2f, -1.8f), new Vector3(0.6f, 0.38f, 0.5f), LabKit.Mat("ThungChuaXanh", new Color(0.1f, 0.4f, 0.7f)));
        }

        static void BuildPhotosynthesisStation(GameObject parent, Vector3 pos)
        {
            var st = new GameObject("ThiNghiem_QuangHop");
            st.transform.SetParent(parent.transform, false);

            // Bàn thí nghiệm
            LabKit.RoundBox(st, "BanQuangHop", pos + new Vector3(0, 0.4f, 0), new Vector3(1.2f, 0.8f, 0.9f), 0.006f, t2BenchToiGian ?? LabKit.Mat("MatBanNhaKinh", Color.white, smoothness: 0.4f));

            // Cốc thủy tinh 1000ml chứa nước và cành rong đuôi chó (Elodea)
            var glassMat = LabKit.Mat("CocThuyTinh", new Color(0.85f, 0.92f, 0.95f, 0.35f), smoothness: 0.95f);
            var waterMat = LabKit.Mat("NuocCoc", new Color(0.7f, 0.88f, 0.92f, 0.4f), smoothness: 0.9f);
            LabKit.Cyl(st, "CocThuyTinh", pos + new Vector3(0, 0.92f, 0), 0.075f, 0.22f, glassMat);
            LabKit.Cyl(st, "NuocTrongCoc", pos + new Vector3(0, 0.90f, 0), 0.072f, 0.18f, waterMat);

            // Phễu thủy tinh úp ngược và ống nghiệm thu khí O2
            LabKit.Cyl(st, "PheuUp", pos + new Vector3(0, 0.88f, 0), 0.055f, 0.06f, glassMat);
            LabKit.Cyl(st, "OngNghiemThuO2", pos + new Vector3(0, 1.02f, 0), 0.015f, 0.16f, glassMat);

            // Đèn chiếu sáng thí nghiệm (thay đổi khoảng cách để đo tốc độ bọt O2)
            var lampMat = LabKit.Mat("DenChieuQuangHop", Color.white, emission: new Color(1.0f, 0.95f, 0.8f) * 3.0f);
            LabKit.Cyl(st, "BongDenChieu", pos + new Vector3(-0.25f, 0.95f, 0), 0.035f, 0.05f, lampMat);
            LabKit.Label(st, "NhanQuangHop", "THÍ NGHIỆM QUANG HỢP ELODEA\n(BỌT KHÍ O2 THEO CƯỜNG ĐỘ SÁNG)", pos + new Vector3(0, 0.82f, 0.35f), 0.015f, Color.black);
        }

        static void BuildPotometerStation(GameObject parent, Vector3 pos)
        {
            var st = new GameObject("ThiNghiem_ThoatHoiNuoc");
            st.transform.SetParent(parent.transform, false);

            LabKit.RoundBox(st, "BanPotometer", pos + new Vector3(0, 0.4f, 0), new Vector3(1.2f, 0.8f, 0.9f), 0.006f, t2BenchToiGian ?? LabKit.Mat("MatBanNhaKinh", Color.white, smoothness: 0.4f));

            // Dụng cụ đo thoát hơi nước (Potometer): ống mao dẫn có bọt khí, thước đo mm
            var tubeMat = LabKit.Mat("OngMaoDan", new Color(0.9f, 0.95f, 1.0f, 0.5f), smoothness: 0.95f);
            LabKit.Cyl(st, "OngMaoDanNgang", pos + new Vector3(0, 0.86f, 0), 0.006f, 0.45f, tubeMat);
            LabKit.Box(st, "ThuocDoMm", pos + new Vector3(0, 0.85f, -0.02f), new Vector3(0.04f, 0.005f, 0.45f), LabKit.Mat("ThuocDo", Color.white, smoothness: 0.3f));
            LabKit.Label(st, "NhanPotometer", "POTOMETER: TỐC ĐỘ THOÁT HƠI NƯỚC", pos + new Vector3(0, 0.82f, 0.35f), 0.015f, Color.black);
        }

        static void BuildPhototropismBox(GameObject parent, Vector3 pos)
        {
            var box = new GameObject("HopHuongSang");
            box.transform.SetParent(parent.transform, false);

            var matBox = LabKit.Mat("HopCartonDen", new Color(0.12f, 0.12f, 0.12f), smoothness: 0.2f);
            // Hộp đen kín có khe hẹp một bên (x = 25.0, z = -7.5)
            LabKit.Box(box, "HopDen", pos + new Vector3(0, 0.98f, 0), new Vector3(0.28f, 0.32f, 0.28f), matBox);
            // Khe sáng ở mặt hông
            var slotMat = LabKit.Mat("KheSang", Color.white, emission: Color.white * 2.0f);
            LabKit.Box(box, "KheSang", pos + new Vector3(0.141f, 1.05f, 0), new Vector3(0.005f, 0.08f, 0.04f), slotMat);
            LabKit.Label(box, "NhanHuongSang", "HỘP TÍNH HƯỚNG SÁNG (PHOTOTROPISM)", pos + new Vector3(0, 0.82f, 0.25f), 0.014f, Color.black);
        }

        // ==========================================
        // 5. HÀNH LANG ĐÔNG (EAST CORRIDOR)
        // ==========================================
        static void BuildEastCorridor(GameObject parent)
        {
            var cor = new GameObject("Hành lang đông tầng 2");
            cor.transform.SetParent(parent.transform, false);

            // Lan can kính chịu lực dài 33m ở mép đông x = 29.9, z = 6..15
            var glassMat = LabKit.Mat("LanCanKinh", new Color(0.85f, 0.92f, 0.96f, 0.4f), smoothness: 0.95f);
            var handrailMat = LabKit.Mat("TayViNhom", new Color(0.82f, 0.84f, 0.86f), metallic: 0.85f, smoothness: 0.7f);

            // Tấm kính lan can cao 1.1m
            LabKit.Box(cor, "VachKinhLanCan", new Vector3(29.85f, 0.55f, 10.5f), new Vector3(0.02f, 1.1f, 9.0f), glassMat);
            // Tay vịn nhôm tròn trên đỉnh
            LabKit.Cyl(cor, "TayViLanCan", new Vector3(29.85f, 1.12f, 10.5f), 0.025f, 9.0f, handrailMat);

            // Ghế băng nghỉ bằng gỗ sồi và chậu cây xanh
            var matBench = t2OakWood ?? LabKit.Mat("GheBangGo", new Color(0.8f, 0.72f, 0.6f));
            LabKit.RoundBox(cor, "GheNghi_1", new Vector3(27.8f, 0.22f, 9.0f), new Vector3(0.5f, 0.44f, 1.6f), 0.01f, matBench);
            LabKit.RoundBox(cor, "GheNghi_2", new Vector3(27.8f, 0.22f, 12.5f), new Vector3(0.5f, 0.44f, 1.6f), 0.01f, matBench);

            // Chậu cây cảnh văn phòng
            BuildPottedPlant(cor, new Vector3(27.8f, 0, 7.5f));
            BuildPottedPlant(cor, new Vector3(27.8f, 0, 11.0f));
            BuildPottedPlant(cor, new Vector3(27.8f, 0, 14.2f));

            // Tường thông tin khoa học (Hệ thống phân loại sinh giới Whittaker 5 giới & Woese 3 lãnh giới)
            var infoMat = LabKit.Mat("TuongThongTin", new Color(0.95f, 0.95f, 0.97f), smoothness: 0.3f);
            var infoPanel = LabKit.Box(cor, "BangPhanLoaiSinhGioi", new Vector3(27.05f, 1.8f, 11.5f), new Vector3(0.04f, 1.4f, 4.5f), infoMat);
            LabKit.Label(infoPanel, "TieuDePhanLoai", "HỆ THỐNG PHÂN LOẠI SINH GIỚI\n- 5 GIỚI (WHITTAKER): KHỞI SINH, NGUYÊN SINH, NẤM, THỰC VẬT, ĐỘNG VẬT\n- 3 LÃNH GIỚI (WOESE): BACTERIA, ARCHAEA, EUKARYA", new Vector3(27.08f, 1.8f, 11.5f), 0.028f, Color.darkGray);
        }

        static void BuildPottedPlant(GameObject parent, Vector3 pos)
        {
            var plant = new GameObject("ChauCayCanh");
            plant.transform.SetParent(parent.transform, false);

            var matPot = LabKit.Mat("ChauSuTrang", new Color(0.95f, 0.95f, 0.96f), smoothness: 0.7f);
            var matSoil = LabKit.Mat("DatTrong", new Color(0.22f, 0.16f, 0.12f), smoothness: 0.1f);
            var matFoliage = LabKit.Mat("LaCayVanPhong", new Color(0.18f, 0.52f, 0.15f), smoothness: 0.4f);

            // Chậu sứ hình trụ vuốt nhẹ
            LabKit.Cyl(plant, "Chau", pos + new Vector3(0, 0.25f, 0), 0.22f, 0.5f, matPot);
            LabKit.Cyl(plant, "Dat", pos + new Vector3(0, 0.48f, 0), 0.21f, 0.04f, matSoil);

            // Thân cây và tán lá xòe
            LabKit.Cyl(plant, "ThanCay", pos + new Vector3(0, 0.75f, 0), 0.025f, 0.55f, LabKit.Mat("ThanGo", new Color(0.35f, 0.25f, 0.18f)));
            LabKit.Ball(plant, "TanLa_1", pos + new Vector3(0, 1.05f, 0), 0.32f, matFoliage);
            LabKit.Ball(plant, "TanLa_2", pos + new Vector3(0.12f, 1.15f, -0.08f), 0.25f, matFoliage);
            LabKit.Ball(plant, "TanLa_3", pos + new Vector3(-0.1f, 1.12f, 0.1f), 0.26f, matFoliage);
        }
    }
}
