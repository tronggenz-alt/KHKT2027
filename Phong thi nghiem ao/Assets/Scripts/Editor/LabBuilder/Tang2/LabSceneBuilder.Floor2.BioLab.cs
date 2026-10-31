using System.Collections.Generic;
using Khkt.Demo.Bio;
using Khkt.Environment.Tang2;
using UnityEngine;
using static Khkt.EditorTools.LabKit;

namespace Khkt.EditorTools
{
    /// <summary>
    /// Phòng thí nghiệm sinh học tầng 2 (x 0..20, z 0..15), bố cục như phòng hóa tầng 1 để học sinh quen:
    ///   tường tây: tường video 3 màn hình, logo trường, áp phích tế bào, tường các nhà sinh học; khu giáo viên x 0..3
    ///   9 bàn kính hiển vi (3 hàng × 3 dãy, mỗi bàn 3 kính, 3 ghế, bồn rửa, giá thuốc nhuộm, màn hình nhóm)
    ///   tường bắc: 2 tủ an toàn sinh học cấp II, quầy rửa tay; tường đông: khu giải phẫu số (z 9..13,5),
    ///   khu tủ ấm – nồi hấp (z 3..7,5), pass-box sang phòng vi sinh; tường nam: triển lãm dự án sinh học; góc tây nam: an toàn
    /// </summary>
    public static partial class LabSceneBuilder
    {
        static readonly (string id, string name, string years, string work)[] Biologists =
        {
            ("darwin", "Charles Darwin", "1809 – 1882 · Anh", "Thuyết tiến hóa bằng chọn lọc tự nhiên\n\"Nguồn gốc các loài\" (1859)"),
            ("mendel", "Gregor Mendel", "1822 – 1884 · Áo", "Các quy luật di truyền (1866)\nThí nghiệm lai đậu Hà Lan"),
            ("pasteur", "Louis Pasteur", "1822 – 1895 · Pháp", "Bác bỏ thuyết tự sinh, phép thanh trùng\nVắc-xin bệnh dại (1885)"),
            ("leeuwenhoek", "Antonie van Leeuwenhoek", "1632 – 1723 · Hà Lan", "Tự mài kính hiển vi đơn tròng\nLần đầu quan sát vi khuẩn (1676)"),
            ("hooke", "Robert Hooke", "1635 – 1703 · Anh", "Đặt tên \"tế bào\" khi soi lát bần\n\"Micrographia\" (1665)"),
            ("watson_crick", "Watson & Crick", "1953 · Anh – Mỹ", "Mô hình xoắn kép ADN (1953)\ndựa trên ảnh tia X của R. Franklin"),
            ("ton_that_tung", "GS. Tôn Thất Tùng", "1912 – 1982 · Việt Nam", "Phương pháp mổ gan khô\nBản đồ mạch máu trong gan"),
        };

        static void BuildFloor2BioLab(Transform f2)
        {
            var lab = Group("Phòng thí nghiệm sinh học", f2);
            BioFrontWall(lab);
            TeacherArea(lab);  // bàn giáo viên + ghế của tầng 1 (cùng vị trí x 1,9, z 7,5)
            var desk = lab.Find("Bàn giáo viên");
            Microscope(desk, "Kính hiển vi giáo viên", new Vector3(0f, 0.9f, -0.5f), 180f, true);

            var benches = Group("Bàn kính hiển vi học sinh", lab);
            int n = 1;
            for (int row = 0; row < BenchZ.Length; row++)
                for (int col = 0; col < BenchX.Length; col++)
                {
                    BioBench(benches, $"Bàn sinh học {n}", new Vector3(BenchX[col], 0, BenchZ[row]), row == 2 ? -1 : +1, n);
                    n++;
                }

            var bsc = Group("Dãy tủ an toàn sinh học", lab);
            BiosafetyCabinet(bsc, "Tủ an toàn sinh học 1", new Vector3(6.6f, 0, LabZ - 0.5f), 90f);
            BiosafetyCabinet(bsc, "Tủ an toàn sinh học 2", new Vector3(11.6f, 0, LabZ - 0.5f), 90f);
            HandWashCounter(lab, new Vector3(15.4f, 0, LabZ - 0.4f), 2.4f);
            AnatomyArea(lab);
            IncubationArea(lab);
            PassBox(lab, new Vector3(LabX, 0, 8.3f));
            BioSafetyCorner(lab);
            BioProjectWall(lab);

            // logo trường cỡ lớn trên tường đông (trên khu tủ ấm), nhìn thấy từ khắp phòng như tầng 1
            SchoolLogo(lab, new Vector3(LabX - 0.1f, 2.98f, 5.25f), Vector3.left, 3.0f);

            var misc = Group("Đồ khác", lab);
            Box(misc, "Công tắc đèn", new Vector3(3.55f, 1.3f, 0.11f), new Vector3(0.12f, 0.12f, 0.02f), signWhite, false);
            BiohazardBin(misc, new Vector3(3.55f, 0, 0.45f));
            foreach (float x in new[] { 8.6f, 15.0f })
            {
                var ac = Group("Điều hòa", misc, new Vector3(x, 2.95f, 0.22f));
                Box(ac, "Thân máy", Vector3.zero, new Vector3(1.0f, 0.3f, 0.22f), whitePlastic, false);
                Box(ac, "Cửa gió", new Vector3(0, -0.12f, 0.1f), new Vector3(0.9f, 0.03f, 0.03f), panelGrey, false);
            }

            var lights = Group("Đèn trần", lab);
            LabCeilingLeds(lights);
            RoomLights(lights, new[] { 3.7f, 10.3f, 16.9f }, new[] { 2.6f, 7.5f, 12.4f }, 10f, 3.8f);

            BioWalkways(lab);
            var signs = Group("Biển khu", lab);
            ZoneSign(signs, "KHU THỰC HÀNH SINH HỌC", new Vector3(8.6f, 2.95f, 5.05f), 90f, 2.6f, "BIOLOGY PRACTICAL");
            ZoneSign(signs, "GIẢI PHẪU SỐ", new Vector3(17.6f, 2.9f, 11.25f), 90f, 2.2f, "DIGITAL ANATOMY");
            ZoneSign(signs, "TỦ AN TOÀN SINH HỌC", new Vector3(9.1f, 2.95f, 13.7f), 0f, 2.6f, "BIOSAFETY CABINETS");
            var plants = Group("Cây xanh trong phòng", lab);
            foreach (var pos in new[] { new Vector3(0.55f, 0, 14.45f), new Vector3(4.65f, 0, 14.5f), new Vector3(13.6f, 0, 14.5f), new Vector3(7.15f, 0, 0.42f) })
                Plant(plants, pos, 4, 1f);
            Plant(plants, new Vector3(16.9f, 0, 8.3f), 0, 1.3f, potWhite);

            // Chủ đề Quốc tế: dải sọc màu chạy ngang tường bắc / nam như tầng 1 (chủ đề Tối giản bỏ đi)
            var frieze = Group("Dải sọc màu (Quốc tế)", lab);
            frieze.gameObject.AddComponent<ThemeOnly>().theme = LabThemeMode.QuocTe;
            Frieze(frieze, new Vector3(0.2f, 0, 0.104f), new Vector3(LabX - 0.1f, 0, 0.104f));
            Frieze(frieze, new Vector3(0.2f, 0, LabZ - 0.104f), new Vector3(LabX - 0.1f, 0, LabZ - 0.104f));
        }

        // ---------------------------------------------------------------------
        // Tường bảng (tây)
        // ---------------------------------------------------------------------

        static void BioFrontWall(Transform lab)
        {
            var g = Group("Tường bảng", lab);
            const float zc = 7.5f;
            var wall = Group("Tường video", g);
            NoShadow(Box(wall, "Mảng ốp lam gỗ", new Vector3(0.104f, 1.78f, zc), new Vector3(0.006f, 1.9f, 6.2f), accentWall, false));
            foreach (float ey in new[] { 0.82f, 2.74f })
                Box(wall, "Viền mảng ốp", new Vector3(0.108f, ey, zc), new Vector3(0.014f, 0.03f, 6.24f), doorFrame, false);
            const float w = 1.66f, h = 0.94f, gap = 0.06f, y = 1.78f;
            var s1 = ScreenUnit(wall, "Màn hình tường video 1", "t2_tuong_video_1", new Vector3(0.135f, y, zc - (w + gap)), Vector3.right, w, h, 14f,
                ("PHÒNG THÍ NGHIỆM SINH HỌC", "Quan sát tế bào · Enzyme · Lên men · Quang hợp\nTách chiết ADN · Nuôi cấy vi sinh · Giải phẫu số"),
                ("BÀI HỌC HÔM NAY: CO VÀ PHẢN CO NGUYÊN SINH", "1. Tách biểu bì vảy hành tím, đặt lên lam kính\n2. Nhỏ nước cất, đậy lamen, soi ở vật kính 10×\n3. Nhỏ NaCl 5 % ở một cạnh lamen, giấy thấm ở cạnh kia\n4. Màng sinh chất tách khỏi thành tế bào: co nguyên sinh\n5. Thay bằng nước cất: tế bào phản co nguyên sinh"));
            var s2 = ScreenUnit(wall, "Màn hình tường video 2", "t2_tuong_video_2", new Vector3(0.135f, y, zc), Vector3.right, w, h, 0f,
                ("CAMERA KÍNH HIỂN VI GIÁO VIÊN", "Ảnh tiêu bản từ kính hiển vi của giáo viên chiếu lên đây.\nThả ảnh slide (PNG, JPG) vào StreamingAssets/BaiGiang/t2_tuong_video_2"));
            var s3 = ScreenUnit(wall, "Màn hình tường video 3", "t2_tuong_video_3", new Vector3(0.135f, y, zc + (w + gap)), Vector3.right, w, h, 16f,
                ("THẢO LUẬN NHÓM", "Vì sao tế bào hành trong NaCl 5 % lại co nguyên sinh?\nEnzyme catalase có ở bào quan nào?\nNhấn T để hỏi trợ lý AI"));
            s3.pages.Insert(0, new Khkt.Demo.LabScreen.Page { image = (Texture2D)posterBio[0].GetTexture("_BaseMap") });
            Info(s1.transform, "Tường video (3 màn hình 75 inch)", "Chiếu bài giảng sinh học, ảnh từ kính hiển vi giáo viên, câu hỏi thảo luận. Bấm vào màn hình để sang trang.");

            // áp phích cấu trúc tế bào bên trái tường video, đồng hồ, logo trường và tên phòng
            Box(g, "Khung áp phích", new Vector3(0.103f, 1.7f, 3.6f), new Vector3(0.004f, 1.43f, 1.06f), darkMetal, false);
            NoShadow(Panel(g, "Áp phích cấu trúc tế bào", new Vector3(0.106f, 1.7f, 3.6f), Vector3.right, 1.0f, 1.375f, posterBio[0]));
            Label(g, "CẤU TRÚC TẾ BÀO", new Vector3(0.109f, 2.3f, 3.6f), Vector3.right, 0.05f, Color.white);
            var clock = Group("Đồng hồ", g, new Vector3(0.12f, 2.82f, 3.6f));
            Cyl(clock, "Vành", new Vector3(-0.005f, 0, 0), 0.17f, 0.03f, darkMetal, false, new Vector3(0, 0, 90));
            Cyl(clock, "Mặt", new Vector3(0.008f, 0, 0), 0.155f, 0.012f, signWhite, false, new Vector3(0, 0, 90));
            ClockHand(clock, "Kim giờ", 290f, 0.08f, 0.012f);
            ClockHand(clock, "Kim phút", 240f, 0.12f, 0.007f);
            SchoolLogo(g, new Vector3(0.112f, 3.18f, zc), Vector3.right, 2.95f);
            Label(g, "PHÒNG THÍ NGHIỆM SINH HỌC", new Vector3(0.112f, 3.2f, zc - 2.55f), Vector3.right, 0.08f, new Color(0.08f, 0.42f, 0.22f));
            Label(g, "BIOLOGY LAB", new Vector3(0.112f, 3.05f, zc - 2.55f), Vector3.right, 0.05f, new Color(0.35f, 0.38f, 0.4f));

            // các nhà sinh học tiêu biểu (phía bắc tường video)
            Label(g, "CÁC NHÀ SINH HỌC TIÊU BIỂU", new Vector3(0.108f, 2.66f, 12.6f), Vector3.right, 0.075f, new Color(0.08f, 0.42f, 0.22f));
            Label(g, "GREAT BIOLOGISTS", new Vector3(0.108f, 2.55f, 12.6f), Vector3.right, 0.04f, new Color(0.35f, 0.38f, 0.4f));
            Box(g, "Dải màu", new Vector3(0.106f, 2.48f, 12.6f), new Vector3(0.006f, 0.02f, 3.6f), t2Accent, false);
            for (int i = 0; i < 4; i++)
                BiologistPortrait(g, i, new Vector3(0.112f, 1.78f, 11.1f + i * 1.0f), Vector3.right);
        }

        static void BiologistPortrait(Transform p, int index, Vector3 pos, Vector3 facing)
        {
            var c = Biologists[index];
            float yaw = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
            var g = Group("Nhà sinh học " + c.name, p, pos, yaw);
            const float w = 0.6f, h = 0.78f;
            Box(g, "Khung", new Vector3(0, 0, -0.012f), new Vector3(w + 0.06f, h + 0.06f, 0.024f), frameMat, false);
            NoShadow(Panel(g, "Ảnh", new Vector3(0, 0, 0.001f), Vector3.forward, w, h, PortraitMaterial(c.id, w / h)));
            NoShadow(Box(g, "Đèn rọi tranh", new Vector3(0, h / 2 + 0.07f, 0.05f), new Vector3(w * 0.7f, 0.025f, 0.05f), tubeGlow, false));
            Box(g, "Bảng tên", new Vector3(0, -h / 2 - 0.2f, -0.004f), new Vector3(w + 0.3f, 0.3f, 0.008f), t2AccentDark, false);
            Label(g, c.name.ToUpperInvariant(), new Vector3(0, -h / 2 - 0.09f, 0.002f), Vector3.forward, 0.04f, Color.white);
            Label(g, c.years, new Vector3(0, -h / 2 - 0.14f, 0.002f), Vector3.forward, 0.026f, new Color(0.65f, 0.95f, 0.75f));
            Label(g, c.work, new Vector3(0, -h / 2 - 0.25f, 0.002f), Vector3.forward, 0.02f, new Color(0.9f, 0.97f, 0.92f));
            Info(g, c.name, c.years + ". " + c.work.Replace("\n", "; "));
        }

        // ---------------------------------------------------------------------
        // Bàn kính hiển vi học sinh (hệ tọa độ như bàn tầng 1: x bề sâu, học sinh ngồi phía +x, z dọc bàn)
        // ---------------------------------------------------------------------

        static void BioBench(Transform p, string name, Vector3 center, int s, int number)
        {
            var b = Group(name, p, center);
            const float top = 0.8f, sinkZ0 = 0.74f;
            Vector3 L(float x, float y, float z) => new Vector3(x, y, z * s);

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
            NoShadow(Box(b, "Dải nhấn", L(0.452f, 0.775f, 0), new Vector3(0.004f, 0.02f, 2.6f), ledBlue, false));
            TopWithSink(b, "Mặt bàn", 0.9f, 2.6f, top, new Vector2(-0.2f, 0.98f * s), new Vector2(0.36f, 0.42f), 0.2f);
            Faucet(b, "Vòi nước", L(-0.415f, top, 0.98f), Vector3.right, 0.32f, 0.2f);

            // giá thuốc thử dọc mép sau: ổ điện, số bàn, thuốc nhuộm, nước cất, dung dịch muối
            var shelf = Group("Giá thuốc thử", b);
            foreach (float z in new[] { -1.21f, 0.61f })
                Box(shelf, "Trụ giá", L(-0.36f, 0.975f, z), new Vector3(0.16f, 0.35f, 0.02f), cabinet);
            Box(shelf, "Tầng giá", L(-0.36f, 1.14f, -0.3f), new Vector3(0.16f, 0.02f, 1.84f), cabinet);
            Box(shelf, "Ổ cắm điện", L(-0.282f, 0.92f, -0.3f), new Vector3(0.004f, 0.06f, 0.18f), signWhite, false);
            Label(shelf, $"BÀN {number}", L(-0.28f, 1.22f, -1.0f), Vector3.right, 0.045f, new Color(0.08f, 0.38f, 0.2f));
            StainBottle(shelf, L(-0.36f, 1.15f, -0.85f), bluePlastic, "Xanh methylen");
            StainBottle(shelf, L(-0.36f, 1.15f, -0.7f), bioIodine, "Lugol");
            StainBottle(shelf, L(-0.36f, 1.15f, -0.55f), bioPurpleStain, "Tím tinh thể");
            StainBottle(shelf, L(-0.36f, 1.15f, -0.4f), bioRedStain, "Safranin");
            Bottle(shelf, "Chai nước cất", L(-0.36f, 1.15f, -0.15f), 1);
            Bottle(shelf, "Chai NaCl 5 %", L(-0.36f, 1.15f, 0.15f), 4);
            Bottle(shelf, "Chai glixerol", L(-0.36f, 1.15f, 0.42f), 2);
            BioBenchScreen(b, number, s);

            // 3 chỗ ngồi: kính hiển vi thẳng ghế, hộp tiêu bản, khay dụng cụ, bình tia nước
            for (int i = 0; i < 3; i++)
            {
                float z = -0.85f + i * 0.85f;
                bool hero = number == 1 && i == 0;  // kính bấm được ở trạm 21
                Microscope(b, $"Kính hiển vi {i + 1}", L(0.12f, top, z), 0f, hero, hero);
                Stool(b, $"Ghế đẩu {i + 1}", L(0.85f, 0, z));
            }
            SlideBox(b, L(-0.17f, top, -0.45f), 0f, number == 1);
            InstrumentTray(b, L(-0.2f, top, 0.38f), 0f);
            WashBottle(b, L(-0.24f, top, -1.12f));
            PetriStack(b, L(-0.06f, top, -1.15f), 1, 2);
            Box(b, "Giấy thấm", L(-0.06f, top + 0.004f, -0.6f), new Vector3(0.1f, 0.008f, 0.1f), signWhite, false);

            if (number == 1)  // bàn 1: co – phản co nguyên sinh (3 cốc: nước cất, NaCl 0,9 %, NaCl 5 %)
            {
                var set = Group("Bộ thí nghiệm co nguyên sinh", b);
                string[] lbl = { "NƯỚC CẤT", "NaCl 0,9 %", "NaCl 5 %" };
                for (int i = 0; i < 3; i++)
                {
                    var pos = L(-0.06f, top, 0.0f + i * 0.13f - 0.13f);
                    MeshObj(set, "Cốc " + lbl[i], Beaker("Coc100", 0.026f, 0.07f), pos, labGlass);
                    NoShadow(Cyl(set, "Dung dịch", pos + Vector3.up * 0.025f, 0.024f, 0.05f, liquidClear));
                    Label(set, lbl[i], pos + new Vector3(0.028f, 0.035f, 0), Vector3.right, 0.009f, new Color(0.1f, 0.2f, 0.3f));
                }
                Ball(set, "Củ hành tím", L(-0.23f, top + 0.03f, 0.05f), new Vector3(0.07f, 0.06f, 0.07f), Mat("T2_HanhTim", new Color(0.55f, 0.18f, 0.35f), 0.6f));
            }
            if (number == 4)  // bàn 4: enzyme catalase phân hủy H2O2 (gan, khoai tây sống / luộc)
            {
                var set = Group("Bộ thí nghiệm catalase", b);
                TestTubeRack(set, L(-0.1f, top, 0.05f), 0f);
                Bottle(set, "Chai H₂O₂ 3 %", L(-0.25f, top, -0.05f), 0);
                Box(set, "Miếng gan", L(-0.25f, top + 0.01f, 0.12f), new Vector3(0.04f, 0.02f, 0.05f), Mat("T2_Gan", new Color(0.42f, 0.12f, 0.1f), 0.6f), false);
                Box(set, "Lát khoai tây", L(-0.25f, top + 0.006f, 0.22f), new Vector3(0.05f, 0.012f, 0.05f), Mat("T2_KhoaiTay", new Color(0.92f, 0.85f, 0.6f), 0.4f), false);
            }
        }

        static void BioBenchScreen(Transform b, int number, int s)
        {
            string[] tips =
            {
                "Bắt đầu soi ở vật kính 4×, chỉnh thô trước, chỉnh tinh sau",
                "Đậy lamen nghiêng 45° để không có bọt khí",
                "Vật kính 100×: nhỏ một giọt dầu soi lên lamen",
                "Không chạm tay vào mặt thấu kính, lau bằng giấy lau kính",
                "Tắt đèn kính hiển vi, hạ bàn kính về thấp nhất sau khi dùng",
                "Rửa tay bằng xà phòng sau khi làm với vi sinh vật",
                "Đĩa petri đã cấy: dán băng dính, không mở nắp",
                "Đèn cồn: tắt bằng nắp, không thổi",
                "Chất thải sinh học bỏ vào thùng đỏ, vật sắc nhọn vào hộp vàng",
            };
            var scr = ScreenUnit(b, "Màn hình nhóm", "t2_ban_" + number, new Vector3(-0.36f, 1.47f, -1.05f * s), Vector3.right, 0.4f, 0.24f, 12f,
                ($"BÀN {number} · NHÓM {number}", "Sẵn sàng thực hành\nMặc áo blouse, đeo găng tay"),
                ("MẸO DÙNG KÍNH HIỂN VI", tips[(number - 1) % tips.Length].Replace(", ", ",\n")),
                ("THẢO LUẬN", "Vẽ hình quan sát, ghi độ phóng đại\nNhấn T để hỏi trợ lý AI"));
            Box(b, "Chân màn hình", new Vector3(-0.36f, 1.25f, -1.05f * s), new Vector3(0.03f, 0.2f, 0.03f), alu, false);
            Info(scr.transform, $"Màn hình nhóm bàn {number}", "Mẹo dùng kính hiển vi, câu hỏi thảo luận. Bấm để sang trang.");
        }

        // ---------------------------------------------------------------------
        // Quầy rửa tay (tường bắc), pass-box, góc an toàn, tường triển lãm
        // ---------------------------------------------------------------------

        /// <summary>Quầy rửa tay áp tường bắc: tủ dưới, mặt đá có 1 bồn, vòi cảm ứng, xà phòng, giấy lau, giá úp dụng cụ. pos: tâm mép tủ.</summary>
        static void HandWashCounter(Transform p, Vector3 pos, float len)
        {
            var g = Group("Quầy rửa tay", p, pos, 90f);  // hệ riêng: mặt trước +x (nhìn về phía nam), tường ở −x
            const float top = 0.88f;
            Box(g, "Chân đế", new Vector3(0.02f, 0.04f, 0), new Vector3(0.5f, 0.08f, len - 0.04f), darkMetal);
            Box(g, "Tủ dưới", new Vector3(0, 0.34f, -0.5f), new Vector3(0.58f, 0.52f, len - 1.0f), cabinet);
            HollowCabinet(g, "Hộc bồn", new Vector3(0, 0.08f, (len - 1.0f) / 2), new Vector3(0.58f, 0.52f, 1.0f - 0.0f));
            for (int i = 0; i < 3; i++)
                Box(g, "Cánh tủ", new Vector3(0.295f, 0.34f, -len / 2 + 0.34f + i * 0.78f), new Vector3(0.01f, 0.46f, 0.74f), cabinetDoor, false);
            TopWithSink(g, "Mặt quầy", 0.6f, len, top, new Vector2(-0.02f, 0.55f), new Vector2(0.46f, 0.42f), 0.22f);
            Faucet(g, "Vòi cảm ứng", new Vector3(-0.24f, top, 0.55f), Vector3.right, 0.3f, 0.2f);
            Box(g, "Ốp tường", new Vector3(-0.295f, top + 0.1f, 0), new Vector3(0.01f, 0.2f, len), tileWhite, false);
            Box(g, "Hộp xà phòng", new Vector3(-0.27f, top + 0.12f, 0.15f), new Vector3(0.06f, 0.16f, 0.1f), whitePlastic, false);
            Box(g, "Hộp giấy lau", new Vector3(-0.25f, top + 0.12f, 0.95f), new Vector3(0.1f, 0.18f, 0.24f), whitePlastic, false);
            var rack = Group("Giá úp dụng cụ", g, new Vector3(-0.05f, top, -0.55f));
            Box(rack, "Khay", new Vector3(0, 0.01f, 0), new Vector3(0.3f, 0.02f, 0.4f), steel, false);
            var tube = TestTube();
            for (int i = 0; i < 6; i++)
            {
                Cyl(rack, "Chốt", new Vector3(-0.08f + (i % 2) * 0.16f, 0.07f, -0.15f + (i / 2) * 0.15f), 0.004f, 0.12f, steel);
                NoShadow(MeshObj(rack, "Ống nghiệm úp", tube, new Vector3(-0.08f + (i % 2) * 0.16f, 0.2f, -0.15f + (i / 2) * 0.15f), labGlass, false, new Vector3(180, 0, 0)));
            }
            Label(g, "RỬA TAY 20 GIÂY", new Vector3(-0.288f, top + 0.16f, 0.55f), Vector3.right, 0.03f, new Color(0.08f, 0.38f, 0.2f));
        }

        /// <summary>Pass-box inox xuyên tường x = 20 sang phòng vi sinh: hai cửa kính khóa liên động, đèn UV.</summary>
        static void PassBox(Transform p, Vector3 wallBase)
        {
            var g = Group("Pass-box sang phòng vi sinh", p, wallBase);
            Box(g, "Thân pass-box", new Vector3(0, 1.25f, 0), new Vector3(0.5f, 0.6f, 0.6f), steel);
            foreach (int s in new[] { -1, 1 })
            {
                NoShadow(Box(g, "Cửa kính", new Vector3(s * 0.252f, 1.25f, 0), new Vector3(0.006f, 0.48f, 0.48f), cabinetGlass, false));
                Box(g, "Tay nắm", new Vector3(s * 0.262f, 1.25f, 0.2f), new Vector3(0.015f, 0.12f, 0.02f), steel, false);
                NoShadow(Box(g, "Đèn báo", new Vector3(s * 0.252f, 1.53f, -0.2f), new Vector3(0.006f, 0.02f, 0.05f), safetyGreen, false));
            }
            Label(g, "PASS-BOX", new Vector3(-0.256f, 1.6f, 0), Vector3.left, 0.03f, new Color(0.2f, 0.25f, 0.3f));
            Info(g, "Pass-box (hộp chuyển mẫu)", "Chuyển mẫu, môi trường giữa phòng thí nghiệm và vùng sạch phòng vi sinh mà không mở cửa thông nhau. Hai cửa khóa liên động, đèn UV khử trùng bên trong.");
        }

        /// <summary>Góc an toàn sinh học: vòi rửa mắt, vòi sen khẩn cấp, bình chữa cháy, hộp y tế, bộ xử lý tràn sinh học, thùng rác nguy hại, tủ bảo hộ, nội quy.</summary>
        static void BioSafetyCorner(Transform lab)
        {
            var g = Group("Khu an toàn", lab);
            var eye = Group("Vòi rửa mắt", g, new Vector3(3.95f, 0, 0.42f), 90);
            Cyl(eye, "Đế", new Vector3(0, 0.01f, 0), 0.13f, 0.02f, safetyGreen, true);
            Cyl(eye, "Trụ", new Vector3(0, 0.5f, 0), 0.03f, 0.98f, safetyGreen, true);
            MeshObj(eye, "Chậu", Lathe("ChauRuaMat", 24, V(0, 0), V(0.03f, 0), V(0.03f, 0), V(0.12f, 0.06f), V(0.15f, 0.09f), V(0.155f, 0.095f),
                    V(0.145f, 0.095f), V(0.112f, 0.064f), V(0.024f, 0.008f), V(0, 0.008f)), new Vector3(0, 0.98f, 0), safetyGreen);
            Box(eye, "Cần gạt", new Vector3(-0.2f, 1.02f, 0), new Vector3(0.12f, 0.1f, 0.02f), safetyGreen, false);
            Box(g, "Biển vòi rửa mắt", new Vector3(3.95f, 1.85f, 0.106f), new Vector3(0.5f, 0.2f, 0.01f), safetyGreen, false);
            Label(g, "VÒI RỬA MẮT", new Vector3(3.95f, 1.85f, 0.115f), Vector3.forward, 0.06f, Color.white);
            var sh = Group("Vòi sen khẩn cấp", g, new Vector3(4.85f, 0, 0.5f));
            Cyl(sh, "Đế", new Vector3(0, 0.01f, 0), 0.16f, 0.02f, safetyGreen, true);
            Cyl(sh, "Trụ", new Vector3(0, 1.15f, -0.25f), 0.03f, 2.3f, safetyGreen, true);
            Cyl(sh, "Tay vươn", new Vector3(0, 2.28f, -0.05f), 0.025f, 0.42f, safetyGreen, false, new Vector3(90, 0, 0));
            MeshObj(sh, "Bát sen", Lathe("BatSen", 24, V(0, 0), V(0.16f, -0.05f), V(0.17f, -0.06f), V(0.0f, -0.02f)), new Vector3(0, 2.27f, 0.15f), safetyGreen);
            Cyl(sh, "Dây kéo", new Vector3(0.12f, 1.85f, 0.15f), 0.004f, 0.75f, safetyYellow);
            Box(g, "Biển vòi sen", new Vector3(4.85f, 2.6f, 0.106f), new Vector3(0.6f, 0.2f, 0.01f), safetyGreen, false);
            Label(g, "VÒI SEN KHẨN CẤP", new Vector3(4.85f, 2.6f, 0.115f), Vector3.forward, 0.05f, Color.white);
            Extinguisher(g, new Vector3(5.75f, 0, 0.22f), Vector3.forward);
            Box(g, "Hộp y tế", new Vector3(6.35f, 1.6f, 0.17f), new Vector3(0.36f, 0.3f, 0.1f), signWhite);
            Box(g, "Chữ thập ngang", new Vector3(6.35f, 1.6f, 0.221f), new Vector3(0.16f, 0.05f, 0.002f), red, false);
            Box(g, "Chữ thập dọc", new Vector3(6.35f, 1.6f, 0.221f), new Vector3(0.05f, 0.16f, 0.002f), red, false);
            // bộ xử lý tràn sinh học + thùng rác nguy hại sinh học + hộp vật sắc nhọn
            var spill = Group("Bộ xử lý tràn sinh học", g, new Vector3(6.35f, 0, 0.32f));
            Box(spill, "Thùng", new Vector3(0, 0.22f, 0), new Vector3(0.36f, 0.44f, 0.26f), safetyYellow);
            Box(spill, "Nắp", new Vector3(0, 0.455f, 0), new Vector3(0.38f, 0.03f, 0.28f), bioBiohazard, false);
            Label(spill, "XỬ LÝ TRÀN\nSINH HỌC", new Vector3(0, 0.25f, 0.132f), Vector3.forward, 0.03f, Color.black);
            var bio = Mat("T2_BienBiohazard", Color.white, 0.3f, 0, Tex("T2_BieuTuongBiohazard", 128, 128, BiohazardPixel, repeat: false));
            NoShadow(Panel(spill, "Biểu tượng nguy hại", new Vector3(0, 0.36f, 0.131f), Vector3.forward, 0.12f, 0.12f, bio));
            Info(spill, "Bộ xử lý tràn sinh học", "Găng tay, giấy thấm, dung dịch javel 10 %, kẹp gắp, túi rác nguy hại.",
                 "Phủ giấy thấm lên chỗ tràn, đổ javel từ ngoài vào trong, chờ 20 phút rồi gom bỏ vào túi đỏ.");
            var ppe = Group("Tủ đồ bảo hộ", g, new Vector3(0.3f, 0, 2.6f));
            Box(ppe, "Thân tủ", new Vector3(0, 0.95f, 0), new Vector3(0.4f, 1.9f, 1.0f), cabinet);
            NoShadow(Box(ppe, "Cửa kính", new Vector3(0.205f, 1.15f, 0), new Vector3(0.008f, 1.3f, 0.94f), cabinetGlass, false));
            Label(ppe, "ĐỒ BẢO HỘ", new Vector3(0.21f, 1.98f, 0), Vector3.right, 0.055f, new Color(0.08f, 0.38f, 0.2f));
            for (int i = 0; i < 3; i++)
                Box(ppe, "Tầng tủ", new Vector3(0, 0.73f + i * 0.4f, 0), new Vector3(0.36f, 0.015f, 0.94f), cabinet, false);
            for (int j = 0; j < 4; j++) Goggles(ppe, new Vector3(0.05f, 1.55f, -0.33f + j * 0.22f));
            for (int j = 0; j < 3; j++) GloveBox(ppe, new Vector3(0.04f, 1.15f, -0.3f + j * 0.3f), j);
            CoatRack(lab, new Vector3(0.85f, 0, 0.1f), 0f);
            var rule = Group("Nội quy", g, new Vector3(0.11f, 1.5f, 1.45f));
            Box(rule, "Khung nội quy", Vector3.zero, new Vector3(0.01f, 0.7f, 0.5f), signWhite, false);
            Label(rule, "NỘI QUY PHÒNG SINH HỌC", new Vector3(0.008f, 0.29f, 0), Vector3.right, 0.026f, new Color(0.7f, 0.05f, 0.05f));
            Label(rule, "1. Mặc áo blouse, đeo găng\n2. Không ăn uống, không chạm mắt\n3. Chỉ dùng chủng vi sinh dạy học\n4. Cấy vi sinh trong tủ an toàn\n5. Rác sinh học: thùng đỏ\n6. Rửa tay trước khi ra về",
                  new Vector3(0.008f, 0.02f, 0), Vector3.right, 0.022f, new Color(0.15f, 0.15f, 0.2f));
        }

        static void BiohazardBin(Transform p, Vector3 pos)
        {
            var g = Group("Thùng rác nguy hại sinh học", p, pos);
            var body = Lathe("T2_ThungRacDap", 24, V(0, 0), V(0.16f, 0), V(0.16f, 0), V(0.18f, 0.5f), V(0.185f, 0.51f), V(0.175f, 0.51f), V(0.15f, 0.012f), V(0, 0.012f));
            MeshObj(g, "Thân thùng", body, Vector3.zero, red, true);
            MeshObj(g, "Nắp", Lathe("T2_NapThungRac", 24, V(0, 0), V(0.19f, 0), V(0.18f, 0.03f), V(0, 0.04f)), new Vector3(0, 0.51f, 0), red);
            Box(g, "Bàn đạp", new Vector3(0.18f, 0.03f, 0), new Vector3(0.08f, 0.02f, 0.1f), darkMetal, false);
            var bio = Mat("T2_BienBiohazard", Color.white, 0.3f, 0, Tex("T2_BieuTuongBiohazard", 128, 128, BiohazardPixel, repeat: false));
            NoShadow(Panel(g, "Biểu tượng nguy hại", new Vector3(0.172f, 0.32f, 0), Vector3.right, 0.14f, 0.14f, bio));
        }

        /// <summary>Tường triển lãm dự án sinh học (tường nam phía trong, x 7,6..13,6), tủ khay, kệ sổ, mô hình ADN và tế bào.</summary>
        static void BioProjectWall(Transform lab)
        {
            var g = Group("Tường triển lãm dự án", lab);
            const float zf = 0.1f, x0 = 7.75f, x1 = 13.45f;
            float cx = (x0 + x1) / 2, len = x1 - x0;
            Box(g, "Bảng ghim", new Vector3(cx, 1.78f, zf + 0.012f), new Vector3(len, 1.2f, 0.02f), corkMat, false);
            foreach (float ey in new[] { 1.17f, 2.39f })
                Box(g, "Khung gỗ", new Vector3(cx, ey, zf + 0.016f), new Vector3(len + 0.06f, 0.04f, 0.03f), oak, false);
            Box(g, "Dải tiêu đề", new Vector3(cx, 2.55f, zf + 0.008f), new Vector3(len, 0.2f, 0.012f), t2Accent, false);
            Label(g, "TRIỂN LÃM DỰ ÁN SINH HỌC · BIOLOGY PROJECTS", new Vector3(cx, 2.55f, zf + 0.016f), Vector3.forward, 0.07f, Color.white);
            var titles = new (string vi, string en)[]
            {
                ("Vi sinh vật quanh ta", "Microbes"), ("Enzyme trong đời sống", "Enzymes"), ("Quang hợp", "Photosynthesis"),
                ("Di truyền Mendel", "Genetics"), ("Đa dạng sinh học Gia Lai", "Biodiversity"), ("ADN & công nghệ gen", "DNA"), ("Hội thi KHKT 2027", "Science Fair 2027"),
            };
            for (int i = 0; i < titles.Length; i++)
            {
                float x = x0 + 0.42f + i * 0.81f, y = 1.74f + (i % 2 == 0 ? 0.03f : -0.03f);
                int v = i;
                var m = i % 2 == 0 ? posterBio[(i / 2) % posterBio.Length] : Mat("Poster" + i, Color.white, 0.2f, 0, Tex("Poster" + i, 128, 176, (px, py) => PosterPixel(v, px, py), repeat: false));
                NoShadow(Panel(g, "Poster " + titles[i].en, new Vector3(x, y, zf + 0.024f), Vector3.forward, 0.62f, 0.85f, m));
                Label(g, titles[i].vi, new Vector3(x, y + 0.36f, zf + 0.027f), Vector3.forward, 0.03f, new Color(0.1f, 0.12f, 0.14f));
                NoShadow(Ball(g, "Đinh ghim", new Vector3(x, y + 0.4f, zf + 0.03f), Vector3.one * 0.02f, Mat("DinhGhim" + i % 3, PosterColors[(i + 3) % PosterColors.Length], 0.5f)));
            }
            Info(g, "Tường triển lãm dự án sinh học", "Poster dự án của các nhóm: vi sinh vật, enzyme, quang hợp, di truyền, đa dạng sinh học Gia Lai, công nghệ gen.");
            TrayUnit(g, new Vector3(9.0f, 0, zf), 2);
            TrayUnit(g, new Vector3(10.2f, 0, zf), 4);
            Bookshelf(g, new Vector3(12.25f, 0, zf));
            DnaModel(g, new Vector3(8.75f, 1.004f, zf + 0.25f), 0.55f);
            CellModel(g, new Vector3(9.95f, 1.004f, zf + 0.25f));
            SpecimenJar(g, new Vector3(10.55f, 1.004f, zf + 0.22f), 4, 0.8f);
        }

        /// <summary>Lối đi trên sàn như tầng 1 nhưng viền màu xanh lá của khu sinh học.</summary>
        static void BioWalkways(Transform lab)
        {
            var g = Group("Lối đi trên sàn", lab);
            const float y = 0.002f, t = 0.004f;
            var edge = Mat("T2_VienLoiDi", zoneBioLab, 0.4f);
            foreach (float z in new[] { 5.05f, 9.95f })
            {
                Box(g, "Lối đi ngang", new Vector3(10.5f, y, z), new Vector3(12.8f, t, 1.0f), floorGuide, false);
                foreach (float e in new[] { -0.52f, 0.52f })
                    Box(g, "Viền lối đi", new Vector3(10.5f, y, z + e), new Vector3(12.8f, t, 0.04f), edge, false);
            }
            Box(g, "Lối đi dọc", new Vector3(3.6f, y, 7.5f), new Vector3(1.0f, t, 13.4f), floorGuide, false);
            foreach (float e in new[] { -0.52f, 0.52f })
                Box(g, "Viền lối đi", new Vector3(3.6f + e, y, 7.5f), new Vector3(0.04f, t, 13.4f), edge, false);
            foreach (Transform c in g) NoShadow(c.gameObject);
        }

        // ---------------------------------------------------------------------
        // Khu giải phẫu số (liên kết Phòng Lab Sinh học 3D của app cũ) và khu tủ ấm – nồi hấp
        // ---------------------------------------------------------------------

        static void AnatomyArea(Transform lab)
        {
            var g = Group("Khu giải phẫu", lab);
            var meshes = Bp3dImporter.ImportBodyParts3D();
            var table = Group("Bàn giải phẫu số", g, new Vector3(18.25f, 0, 11.25f));
            RoundBox(table, "Chân bàn", new Vector3(0, 0.38f, 0), new Vector3(0.42f, 0.76f, 1.5f), 0.02f, bioWhite);
            RoundBox(table, "Thân màn hình", new Vector3(0, 0.81f, 0), new Vector3(0.78f, 0.1f, 2.2f), 0.012f, Mat("T2_VoBanGiaiPhau", new Color(0.18f, 0.2f, 0.22f), 0.55f, 0.3f));
            var screenTex = Tex("T2_ManGiaiPhau", 128, 256, AnatomyScreenPixel, repeat: false);
            var screenMat = EmissiveTexMat("T2_ManHinhGiaiPhau", screenTex, new Color(0.55f, 0.6f, 0.65f), Vector2.one);
            NoShadow(Box(table, "Màn hình cảm ứng", new Vector3(0, 0.861f, 0), new Vector3(0.68f, 0.004f, 2.08f), screenMat, false));
            Label(table, "BÀN GIẢI PHẪU SỐ · BodyParts3D © DBCLS (CC BY 4.0)", new Vector3(0.392f, 0.81f, 0), Vector3.right, 0.018f, new Color(0.7f, 0.85f, 0.95f));
            var anat = table.gameObject.AddComponent<DigitalAnatomyTable>();
            // cơ thể nằm ngửa trên mặt màn hình, đầu hướng bắc, cao 1,6 m
            anat.layerSkeleton = Bp3dBody(table, "Bộ xương", meshes, "xuong", bioBone, new Vector3(0, 0.875f, 0), 1.6f, true);
            anat.layerMuscles = Bp3dBody(table, "Hệ cơ", meshes, "co", bioMuscle, new Vector3(0, 0.875f, 0), 1.6f, true);
            anat.layerSkin = Bp3dBody(table, "Da", meshes, "da", Mat("T2_DaNguoi", new Color(0.92f, 0.74f, 0.62f), 0.4f), new Vector3(0, 0.875f, 0), 1.6f, true);
            anat.layerText = Label(table, "Lớp: Bộ xương", new Vector3(0.2f, 0.8655f, -0.85f), Vector3.up, 0.03f, new Color(0.7f, 0.95f, 1f)).GetComponent<TextMesh>();
            anat.layerText.transform.localRotation = Quaternion.Euler(90, 90, 0);  // nằm trên mặt màn hình, đọc từ phía tây
            Info(table, "Bàn giải phẫu số (BodyParts3D)",
                "Mô hình người thật từ BodyParts3D (© DBCLS, CC BY 4.0), cùng dữ liệu với Phòng Lab Sinh học 3D trên web: bộ xương, hệ cơ, da.",
                "Bấm vào bàn để đổi lớp: bộ xương → hệ cơ → da.");

            // bộ xương đứng trên giá bánh xe, mô hình bán thân, tủ thấp trưng bày mẫu ngâm
            var stand = Group("Bộ xương trên giá", g, new Vector3(19.35f, 0, 13.15f));
            Cyl(stand, "Đế 5 chân", new Vector3(0, 0.05f, 0), 0.28f, 0.04f, darkMetal);
            for (int i = 0; i < 5; i++)
                Ball(stand, "Bánh xe", new Vector3(Mathf.Cos(i * 1.2566f) * 0.25f, 0.025f, Mathf.Sin(i * 1.2566f) * 0.25f), Vector3.one * 0.05f, rubber);
            Cyl(stand, "Cột treo", new Vector3(0.12f, 1.0f, 0), 0.015f, 1.9f, bioChrome);
            Box(stand, "Móc treo", new Vector3(0.06f, 1.93f, 0), new Vector3(0.14f, 0.02f, 0.02f), bioChrome, false);
            Bp3dBody(stand, "Bộ xương người", meshes, "xuong", bioBone, new Vector3(0, 0.1f, 0), 1.7f, false, -90f);
            Info(stand, "Mô hình bộ xương người (206 xương)", "Bộ xương người trưởng thành cao 1,7 m dựng từ dữ liệu BodyParts3D: hộp sọ, cột sống, lồng ngực, xương chi.");
            var cart = Group("Bục mô hình bán thân", g, new Vector3(19.4f, 0, 12.3f));
            RoundBox(cart, "Bục", new Vector3(0, 0.4f, 0), new Vector3(0.44f, 0.8f, 0.44f), 0.01f, bioWhite);
            TorsoModel(cart, new Vector3(0, 0.8f, 0), 180f);
            var cab = Group("Tủ trưng bày mẫu ngâm", g, new Vector3(19.62f, 0, 10.55f));
            RoundBox(cab, "Thân tủ", new Vector3(0, 0.45f, 0), new Vector3(0.5f, 0.9f, 2.3f), 0.008f, cabinet);
            for (int i = 0; i < 3; i++)
                Box(cab, "Cánh tủ", new Vector3(-0.255f, 0.45f, -0.76f + i * 0.76f), new Vector3(0.01f, 0.82f, 0.72f), cabinetDoor, false);
            for (int i = 0; i < 5; i++)
                SpecimenJar(cab, new Vector3(0, 0.9f, -0.92f + i * 0.46f), i, 0.85f);
            Info(cab, "Mẫu ngâm", "Mẫu vật bảo quản trong dung dịch formalin: cá, ếch, rắn, tim lợn, cây non. Quan sát hình thái ngoài, so sánh các lớp động vật.");
        }

        /// <summary>
        /// Đặt lưới BodyParts3D (lớp lop) thành cơ thể cao heightM: nằm ngửa đầu hướng +z (lying = true) hoặc đứng.
        /// Lưới rất nhiều đỉnh nên không đổ bóng.
        /// </summary>
        static GameObject Bp3dBody(Transform p, string name, Dictionary<string, Mesh> meshes, string lop, Material m, Vector3 pos, float heightM, bool lying, float yaw = 0f)
        {
            if (meshes == null || !meshes.TryGetValue(lop, out var mesh) || mesh == null) return null;
            var b = mesh.bounds;
            float s = heightM / Mathf.Max(0.01f, b.size.y);
            // nằm ngửa: trục y của mô hình (đầu) → +z, mặt trước (+z) → +y
            var rot = lying ? Quaternion.AngleAxis(180f, Vector3.forward) * Quaternion.Euler(90f, 0, 0) : Quaternion.Euler(0, yaw, 0);
            Vector3 anchor = lying ? b.center : new Vector3(b.center.x, b.min.y, b.center.z);
            Vector3 lift = lying ? Vector3.up * (b.size.z * s * 0.5f) : Vector3.zero;  // nằm: lưng chạm mặt bàn
            var go = MeshObj(p, name, mesh, pos - rot * (anchor * s) + lift, m, false, null, s);
            go.transform.localRotation = rot;
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>Màn hình bàn giải phẫu: nền xanh đen, lưới mờ, viền phát sáng (giao diện kiểu bàn Anatomage).</summary>
        static Color AnatomyScreenPixel(int x, int y)
        {
            var c = Color.Lerp(new Color(0.02f, 0.05f, 0.09f), new Color(0.03f, 0.1f, 0.16f), y / 256f);
            if (x % 16 == 0 || y % 16 == 0) c = Color.Lerp(c, new Color(0.1f, 0.3f, 0.4f), 0.35f);
            if (x < 3 || x > 124 || y < 3 || y > 252) c = new Color(0.2f, 0.6f, 0.75f);
            if (y > 236 && x > 10 && x < 70) c = new Color(0.15f, 0.5f, 0.6f);
            c.a = 1;
            return c;
        }

        static void IncubationArea(Transform lab)
        {
            var g = Group("Khu tủ ấm và nồi hấp", lab);
            const float x0 = 19.25f, x1 = LabX - 0.1f, z0 = 3.65f, z1 = 7.4f, top = 0.9f;
            float cx = (x0 + x1) / 2, cz = (z0 + z1) / 2, len = z1 - z0;
            Box(g, "Chân đế", new Vector3(cx + 0.03f, 0.04f, cz), new Vector3(x1 - x0 - 0.08f, 0.08f, len - 0.04f), darkMetal);
            Box(g, "Tủ dưới", new Vector3(cx, 0.47f, cz), new Vector3(x1 - x0, 0.78f, len), cabinet);
            for (int i = 0; i < 5; i++)
            {
                float z = z0 + 0.38f + i * (len - 0.1f) / 5f;
                Box(g, "Cánh tủ", new Vector3(x0 - 0.005f, 0.47f, z), new Vector3(0.01f, 0.7f, (len - 0.1f) / 5f - 0.04f), cabinetDoor, false);
                Box(g, "Tay nắm", new Vector3(x0 - 0.018f, 0.75f, z + 0.25f), new Vector3(0.015f, 0.1f, 0.015f), handleBlue, false);
            }
            Box(g, "Mặt bàn", new Vector3(cx - 0.02f, top - 0.02f, cz), new Vector3(x1 - x0 + 0.04f, 0.04f, len), epoxy);
            Incubator(g, new Vector3(cx, top, 6.85f), 180f);
            WaterBath(g, new Vector3(cx, top, 6.05f), 180f);
            BenchCentrifuge(g, new Vector3(cx, top, 5.3f), 180f);
            AnalyticalBalance(g, new Vector3(cx, top, 4.65f), 180f);
            PhMeter(g, new Vector3(cx, top, 4.05f), 180f);
            // giá treo tường: chai môi trường, bình tam giác, cốc
            var shelf = Group("Giá treo tường", g);
            Box(shelf, "Tấm giá", new Vector3(LabX - 0.23f, 1.86f, cz), new Vector3(0.26f, 0.025f, len), oak, false);
            foreach (float z in new[] { z0 + 0.2f, cz, z1 - 0.2f })
                Box(shelf, "Ke đỡ", new Vector3(LabX - 0.16f, 1.78f, z), new Vector3(0.1f, 0.14f, 0.02f), darkMetal, false);
            for (int i = 0; i < 9; i++)
            {
                float z = z0 + 0.25f + i * (len - 0.5f) / 8f;
                if (i % 3 == 0) MeshObj(shelf, "Bình tam giác", Erlenmeyer(), new Vector3(LabX - 0.23f, 1.873f, z), labGlass);
                else Bottle(shelf, "Chai môi trường", new Vector3(LabX - 0.23f, 1.873f, z), i % 3 == 1 ? 1 : 3);
            }
            Label(g, "TỦ ẤM · TIỆT TRÙNG · CÂN – ĐO", new Vector3(LabX - 0.104f, 2.38f, cz), Vector3.left, 0.06f, new Color(0.08f, 0.38f, 0.2f));
            Autoclave(g, new Vector3(19.48f, 0, 3.12f), 180f);
            Box(g, "Biển nồi hấp", new Vector3(LabX - 0.105f, 1.55f, 3.12f), new Vector3(0.006f, 0.22f, 0.5f), safetyYellow, false);
            Label(g, "NÓNG · ÁP SUẤT CAO", new Vector3(LabX - 0.11f, 1.55f, 3.12f), Vector3.left, 0.04f, Color.black);
        }
    }
}
