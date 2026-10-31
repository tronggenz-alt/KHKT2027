using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static Khkt.EditorTools.LabKit;

namespace Khkt.EditorTools
{
    /// <summary>
    /// Đồ dùng phòng thí nghiệm sinh học dựng bằng khối cơ bản, khối tròn xoay và hộp bo cạnh. Mỗi món dựng trong hệ tọa độ
    /// riêng (Group có vị trí + góc quay), không đặt vật con dưới khối đã co giãn. Chi tiết dưới 25 cm tự thôi đổ bóng và
    /// thôi vẽ khi ở xa (OptimizeRenderers), đồ tĩnh được gộp lô.
    /// </summary>
    public static partial class LabSceneBuilder
    {
        static Material bioWhite, bioBlack, bioChrome, bioAgar, bioColony, bioColonyYellow, bioPetri, bioFormalin, bioSpecimen, bioSpecimenPale,
                        bioBiohazard, bioNitrile, bioLeaf, bioLeafDark, bioLeafLight, bioSoil, bioTerracotta, bioPotGrey, bioGel, bioUvGlow, bioBlueTip,
                        bioYellowTip, bioOrangeCap, bioPurpleStain, bioIodine, bioRedStain, bioDisplayGreen, bioDisplayBlue, bioDisplayRed, bioBone,
                        bioMuscle, bioOrgan, bioKraft, bioWater;
        static Material[] posterBio;

        static void InitBioPropMaterials()
        {
            bioWhite = Mat("T2_SonTinhDienTrang", new Color(0.93f, 0.94f, 0.94f), 0.55f);
            bioBlack = Mat("T2_NhuaDenMo", new Color(0.09f, 0.09f, 0.1f), 0.45f);
            bioChrome = Mat("T2_Crom", new Color(0.82f, 0.83f, 0.85f), 0.85f, 1f);
            bioAgar = Mat("T2_ThachAgar", new Color(0.93f, 0.8f, 0.48f, 0.85f), 0.8f, 0, transparent: true);
            bioColony = Mat("T2_KhuanLacKem", new Color(0.97f, 0.94f, 0.82f), 0.55f);
            bioColonyYellow = Mat("T2_KhuanLacVang", new Color(0.95f, 0.78f, 0.3f), 0.55f);
            bioPetri = Mat("T2_DiaPetri", new Color(0.92f, 0.96f, 1f, 0.25f), 0.95f, 0, transparent: true);
            bioFormalin = Mat("T2_DungDichFormalin", new Color(0.95f, 0.88f, 0.55f, 0.35f), 0.9f, 0, transparent: true);
            bioSpecimen = Mat("T2_MauVatNgam", new Color(0.52f, 0.45f, 0.32f), 0.5f);
            bioSpecimenPale = Mat("T2_MauVatNhat", new Color(0.82f, 0.72f, 0.6f), 0.5f);
            bioBiohazard = Mat("T2_CamNguyHai", new Color(0.95f, 0.42f, 0.08f), 0.45f);
            bioNitrile = Mat("T2_GangNitrile", new Color(0.35f, 0.5f, 0.92f), 0.35f);
            bioLeaf = Mat("T2_LaCay", new Color(0.22f, 0.5f, 0.17f), 0.35f);
            bioLeafDark = Mat("T2_LaCayDam", new Color(0.12f, 0.36f, 0.12f), 0.3f);
            bioLeafLight = Mat("T2_LaCayNon", new Color(0.45f, 0.7f, 0.25f), 0.35f);
            bioSoil = Mat("T2_DatTrong", new Color(0.22f, 0.15f, 0.1f), 0.08f);
            bioTerracotta = Mat("T2_ChauDatNung", new Color(0.66f, 0.36f, 0.22f), 0.2f);
            bioPotGrey = Mat("T2_ChauXam", new Color(0.36f, 0.38f, 0.4f), 0.3f);
            bioGel = Mat("T2_GelAgarose", new Color(0.75f, 0.88f, 0.95f, 0.55f), 0.9f, 0, transparent: true);
            bioUvGlow = Mat("T2_BanSoiUV", new Color(0.3f, 0.2f, 0.7f), 0.6f, 0, emission: new Color(0.6f, 0.35f, 1.6f));
            bioBlueTip = Mat("T2_HopDauConXanh", new Color(0.2f, 0.45f, 0.9f), 0.45f);
            bioYellowTip = Mat("T2_HopDauConVang", new Color(0.96f, 0.82f, 0.2f), 0.45f);
            bioOrangeCap = Mat("T2_NapCam", new Color(0.95f, 0.5f, 0.12f), 0.45f);
            bioPurpleStain = Mat("T2_TimTinhThe", new Color(0.35f, 0.08f, 0.55f), 0.6f);
            bioIodine = Mat("T2_Lugol", new Color(0.55f, 0.25f, 0.05f), 0.6f);
            bioRedStain = Mat("T2_Safranin", new Color(0.85f, 0.12f, 0.2f), 0.6f);
            bioDisplayGreen = Mat("T2_ManLedXanhLa", new Color(0.02f, 0.04f, 0.03f), 0.8f, 0, emission: new Color(0.05f, 0.25f, 0.1f));
            bioDisplayBlue = Mat("T2_ManLedXanh", new Color(0.02f, 0.03f, 0.05f), 0.8f, 0, emission: new Color(0.05f, 0.14f, 0.3f));
            bioDisplayRed = Mat("T2_ManLedDo", new Color(0.05f, 0.02f, 0.02f), 0.8f, 0, emission: new Color(0.3f, 0.05f, 0.04f));
            bioBone = Mat("T2_XuongNguoi", new Color(0.9f, 0.86f, 0.77f), 0.35f);
            bioMuscle = Mat("T2_CoNguoi", new Color(0.72f, 0.24f, 0.22f), 0.45f);
            bioOrgan = Mat("T2_NoiTang", new Color(0.62f, 0.3f, 0.3f), 0.5f);
            bioKraft = Mat("T2_GiayKraft", new Color(0.72f, 0.58f, 0.4f), 0.15f);
            bioWater = Mat("T2_NuocTrong", new Color(0.72f, 0.88f, 0.95f, 0.3f), 0.95f, 0, transparent: true);
            posterBio = new[]
            {
                Mat("T2_ApPhichTeBao", Color.white, 0.2f, 0, Tex("T2_ApPhichTeBao", 256, 352, CellPosterPixel, repeat: false)),
                Mat("T2_ApPhichChuKy", Color.white, 0.2f, 0, Tex("T2_ApPhichChuKy", 256, 352, CycleTexPixel, repeat: false)),
                Mat("T2_ApPhichCaySuSong", Color.white, 0.2f, 0, Tex("T2_ApPhichCaySuSong", 256, 352, TreeOfLifePixel, repeat: false)),
                Mat("T2_ApPhichADN", Color.white, 0.2f, 0, Tex("T2_ApPhichADN", 256, 352, DnaPosterPixel, repeat: false)),
            };
        }

        // =====================================================================
        // Kính hiển vi quang học (mặt người dùng nhìn về +x của hệ riêng)
        // =====================================================================

        /// <summary>
        /// Kính hiển vi hai mắt: đế có đèn LED, thân cong, mâm 4 vật kính vòng màu chuẩn (4× đỏ, 10× vàng, 40× xanh nhạt, 100× trắng),
        /// bàn kính có kẹp mẫu và bộ dịch mẫu, tụ quang, núm chỉnh thô / tinh đồng trục, tiêu bản trên bàn kính. pos: tâm đế trên mặt bàn.
        /// </summary>
        static readonly string[] ObjectiveNames = { "4×", "10×", "40×", "100×" };

        static Transform Microscope(Transform p, string name, Vector3 pos, float yaw, bool interactive, bool info = false)
        {
            var g = Group(name, p, pos, yaw);
            var baseMesh = Lathe("T2_DeKinhHienVi", 24, V(0, 0), V(0.085f, 0), V(0.088f, 0.006f), V(0.088f, 0.03f), V(0.08f, 0.042f), V(0, 0.042f));
            MeshObj(g, "Đế", baseMesh, new Vector3(-0.01f, 0, 0), bioWhite, true, null, 1f).transform.localScale = new Vector3(1.15f, 1f, 0.9f);
            NoShadow(Cyl(g, "Đèn LED đế", new Vector3(0.015f, 0.044f, 0), 0.016f, 0.004f, tubeGlow));
            // thân cong: ba đoạn nghiêng
            Box(g, "Thân dưới", new Vector3(-0.06f, 0.1f, 0), new Vector3(0.05f, 0.12f, 0.055f), bioWhite, false);
            Box(g, "Thân giữa", new Vector3(-0.055f, 0.2f, 0), new Vector3(0.048f, 0.11f, 0.052f), bioWhite, false, new Vector3(0, 0, -8));
            Box(g, "Cánh tay", new Vector3(-0.035f, 0.275f, 0), new Vector3(0.085f, 0.045f, 0.05f), bioWhite, false);
            // núm chỉnh thô / tinh hai bên
            foreach (int s in new[] { -1, 1 })
            {
                Cyl(g, "Núm chỉnh thô", new Vector3(-0.06f, 0.1f, s * 0.038f), 0.022f, 0.016f, bioBlack, false, new Vector3(90, 0, 0));
                Cyl(g, "Núm chỉnh tinh", new Vector3(-0.06f, 0.1f, s * 0.052f), 0.012f, 0.014f, bioChrome, false, new Vector3(90, 0, 0));
            }
            // bàn kính + kẹp mẫu + bộ dịch mẫu + tiêu bản
            Box(g, "Bàn kính", new Vector3(0.015f, 0.145f, 0), new Vector3(0.13f, 0.012f, 0.12f), bioBlack, false);
            Box(g, "Bộ dịch mẫu", new Vector3(0.005f, 0.155f, 0.05f), new Vector3(0.09f, 0.008f, 0.012f), bioChrome, false);
            Cyl(g, "Núm dịch mẫu", new Vector3(0.0f, 0.12f, 0.066f), 0.008f, 0.03f, bioBlack);
            NoShadow(Box(g, "Tiêu bản", new Vector3(0.02f, 0.1525f, -0.005f), new Vector3(0.026f, 0.002f, 0.076f), labGlass, false));
            Box(g, "Nhãn tiêu bản", new Vector3(0.02f, 0.1535f, -0.033f), new Vector3(0.024f, 0.001f, 0.018f), labelPaper, false);
            Cyl(g, "Tụ quang", new Vector3(0.018f, 0.125f, 0), 0.018f, 0.025f, bioBlack);
            // đầu quan sát + hai thị kính nghiêng 30° về phía người dùng
            Box(g, "Đầu quan sát", new Vector3(0.005f, 0.31f, 0), new Vector3(0.075f, 0.04f, 0.06f), bioWhite, false);
            foreach (int s in new[] { -1, 1 })
            {
                var tube = Group("Ống thị kính", g, new Vector3(0.04f, 0.335f, s * 0.024f));
                tube.localRotation = Quaternion.Euler(0, 0, -30f);
                Cyl(tube, "Ống", new Vector3(0, 0.03f, 0), 0.011f, 0.06f, bioWhite);
                Cyl(tube, "Thị kính", new Vector3(0, 0.07f, 0), 0.012f, 0.025f, bioBlack);
            }
            // mâm vật kính (xoay được ở kính bấm được)
            var nose = Group("Mâm vật kính", g, new Vector3(0.018f, 0.258f, 0));
            MeshObj(nose, "Mâm xoay", Lathe("T2_MamVatKinh", 20, V(0, 0), V(0.032f, 0), V(0.028f, 0.018f), V(0, 0.018f)), new Vector3(0, -0.012f, 0), bioChrome);
            var rings = new[] { Mat("T2_Vong4x", new Color(0.85f, 0.12f, 0.1f), 0.5f), Mat("T2_Vong10x", new Color(0.95f, 0.8f, 0.1f), 0.5f),
                                Mat("T2_Vong40x", new Color(0.35f, 0.7f, 0.95f), 0.5f), Mat("T2_Vong100x", new Color(0.97f, 0.97f, 0.97f), 0.5f) };
            float[] lens = { 0.032f, 0.04f, 0.046f, 0.05f };
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI / 2 + Mathf.PI;  // vật kính 4× ở vị trí làm việc (phía trước thân kính)
                var o = Group("Vật kính " + ObjectiveNames[i], nose, new Vector3(Mathf.Cos(a) * 0.017f, -0.012f, Mathf.Sin(a) * 0.017f));
                Cyl(o, "Thân vật kính", new Vector3(0, -lens[i] / 2, 0), 0.0085f, lens[i], bioChrome);
                Cyl(o, "Vòng màu", new Vector3(0, -lens[i] * 0.35f, 0), 0.0092f, 0.004f, rings[i]);
            }
            if (interactive)
            {
                var c = g.gameObject.AddComponent<Khkt.Demo.Bio.MicroscopeController>();
                c.nosepiece = nose;
                var col = g.gameObject.AddComponent<BoxCollider>();
                col.center = new Vector3(0, 0.19f, 0);
                col.size = new Vector3(0.2f, 0.38f, 0.16f);
            }
            // thẻ thông tin làm nhánh thôi gộp lô tĩnh: chỉ gắn cho kính bấm được / kính trưng bày, 24 kính trên bàn học sinh để gộp lô
            if (interactive || info) Info(g, "Kính hiển vi quang học hai mắt",
                "Thị kính 10×, vật kính 4× (đỏ), 10× (vàng), 40× (xanh nhạt), 100× dầu (trắng): phóng đại 40× – 1000×. Đèn LED đế, tụ quang có màn chắn sáng.",
                "Bắt đầu ở vật kính 4×, chỉnh thô cho rõ rồi mới đổi vật kính lớn và chỉnh tinh. Vật kính 100× phải nhỏ dầu soi.");
            return g;
        }

        // =====================================================================
        // Tủ an toàn sinh học cấp II (mặt trước nhìn về +x của hệ riêng, rộng theo z)
        // =====================================================================

        static Transform BiosafetyCabinet(Transform p, string name, Vector3 pos, float yaw, float width = 1.5f)
        {
            var g = Group(name, p, pos, yaw);
            const float d = 0.8f, y0 = 0.72f, y1 = 2.2f;
            float hw = width / 2;
            // chân đỡ
            foreach (int s in new[] { -1, 1 })
            {
                Box(g, "Chân", new Vector3(0.25f, y0 / 2, s * (hw - 0.05f)), new Vector3(0.05f, y0, 0.05f), darkMetal, false);
                Box(g, "Chân", new Vector3(-0.3f, y0 / 2, s * (hw - 0.05f)), new Vector3(0.05f, y0, 0.05f), darkMetal, false);
                Box(g, "Thanh giằng", new Vector3(-0.025f, 0.12f, s * (hw - 0.05f)), new Vector3(0.6f, 0.04f, 0.04f), darkMetal, false);
            }
            Box(g, "Khung đế", new Vector3(-0.025f, y0 - 0.03f, 0), new Vector3(0.65f, 0.06f, width - 0.06f), darkMetal);
            // thân tủ: vách hai bên, vách sau, nóc, hộp lọc HEPA trên nóc
            RoundBox(g, "Vách trái", new Vector3(0, (y0 + y1) / 2, -hw + 0.04f), new Vector3(d, y1 - y0, 0.08f), 0.006f, bioWhite);
            RoundBox(g, "Vách phải", new Vector3(0, (y0 + y1) / 2, hw - 0.04f), new Vector3(d, y1 - y0, 0.08f), 0.006f, bioWhite);
            Box(g, "Vách sau", new Vector3(-d / 2 + 0.04f, (y0 + y1) / 2, 0), new Vector3(0.08f, y1 - y0, width - 0.16f), bioWhite);
            RoundBox(g, "Hộp lọc HEPA", new Vector3(-0.02f, y1 + 0.17f, 0), new Vector3(d - 0.04f, 0.34f, width), 0.006f, bioWhite);
            Box(g, "Bàn thao tác inox", new Vector3(0.02f, y0 + 0.13f, 0), new Vector3(d - 0.16f, 0.03f, width - 0.16f), steel);
            Box(g, "Lưới hút phía trước", new Vector3(d / 2 - 0.1f, y0 + 0.12f, 0), new Vector3(0.08f, 0.012f, width - 0.2f), Mat("T2_LuoiHut", new Color(0.3f, 0.32f, 0.34f), 0.4f, 0.6f), false);
            Box(g, "Dưới bàn", new Vector3(0, y0 + 0.06f, 0), new Vector3(d, 0.12f, width - 0.16f), bioWhite);
            // mặt trước: bảng điều khiển, kính trượt nghiêng, khe làm việc
            Box(g, "Bảng trước trên", new Vector3(d / 2 - 0.05f, y1 - 0.2f, 0), new Vector3(0.1f, 0.4f, width - 0.16f), bioWhite);
            NoShadow(Box(g, "Màn hình tốc độ gió", new Vector3(d / 2 + 0.003f, y1 - 0.14f, hw - 0.36f), new Vector3(0.006f, 0.07f, 0.18f), bioDisplayGreen, false));
            Label(g, "0,48 m/s", new Vector3(d / 2 + 0.008f, y1 - 0.135f, hw - 0.36f), Vector3.right, 0.028f, new Color(0.5f, 1f, 0.6f));
            Label(g, "TỦ AN TOÀN SINH HỌC CẤP II", new Vector3(d / 2 + 0.006f, y1 - 0.09f, -0.15f), Vector3.right, 0.035f, new Color(0.2f, 0.25f, 0.3f));
            foreach (int i in new[] { 0, 1, 2 })
                Cyl(g, "Nút", new Vector3(d / 2 + 0.003f, y1 - 0.25f, hw - 0.5f + i * 0.08f), 0.014f, 0.01f, i == 0 ? bioBlueTip : bioWhite, false, new Vector3(0, 0, 90));
            var sash = Group("Kính trượt", g, new Vector3(d / 2 - 0.06f, y0 + 0.62f, 0));
            sash.localRotation = Quaternion.Euler(0, 0, 10f);
            NoShadow(Box(sash, "Kính", Vector3.zero, new Vector3(0.012f, 0.62f, width - 0.2f), cabinetGlass));
            Box(sash, "Tay nắm kính", new Vector3(0.02f, -0.3f, 0), new Vector3(0.03f, 0.025f, width - 0.4f), alu, false);
            NoShadow(Box(g, "Đèn trong tủ", new Vector3(0.05f, y1 - 0.42f, 0), new Vector3(0.12f, 0.012f, width - 0.3f), tubeGlow, false));
            Box(g, "Biển nguy hại sinh học", new Vector3(d / 2 + 0.004f, y0 + 0.04f, -hw + 0.25f), new Vector3(0.004f, 0.12f, 0.12f), bioBiohazard, false);
            // đồ trong tủ: đèn cồn vô trùng, giá ống, đĩa petri
            AlcoholLamp(g, new Vector3(0.0f, y0 + 0.145f, -0.25f));
            PetriStack(g, new Vector3(-0.05f, y0 + 0.145f, 0.22f), 3, 0);
            TubeRack(g, new Vector3(-0.1f, y0 + 0.145f, -0.02f), 6, bioOrangeCap);
            Info(g, "Tủ an toàn sinh học cấp II",
                "Màn khí hút vào ở khe trước bảo vệ người làm, dòng khí sạch qua lọc HEPA thổi xuống bảo vệ mẫu, khí thải lọc HEPA trước khi ra ngoài.",
                "Bật quạt 5 phút trước khi làm, kính trượt đúng vạch, không che lưới hút phía trước, thao tác cách mép trong 10 cm.");
            return g;
        }

        // =====================================================================
        // Máy và thiết bị nhỏ
        // =====================================================================

        /// <summary>Tủ ấm vi sinh để bàn: cửa ngoài có ô kính, cửa trong kính, 3 kệ lưới có đĩa petri, màn số 37,0 °C. Mặt trước +x.</summary>
        static Transform Incubator(Transform p, Vector3 pos, float yaw, float scale = 1f)
        {
            var g = Group("Tủ ấm 37 °C", p, pos, yaw);
            float w = 0.62f * scale, h = 0.72f * scale, d = 0.6f;
            RoundBox(g, "Thân tủ", new Vector3(0, h / 2, 0), new Vector3(d, h, w), 0.008f, bioWhite);
            Box(g, "Cửa", new Vector3(d / 2 + 0.015f, h / 2 - 0.02f, -0.03f), new Vector3(0.03f, h - 0.12f, w - 0.1f), bioWhite);
            NoShadow(Box(g, "Ô kính cửa", new Vector3(d / 2 + 0.032f, h / 2 + 0.01f, -0.03f), new Vector3(0.004f, h - 0.3f, w - 0.25f), darkGlass, false));
            for (int i = 0; i < 3; i++)
            {
                float y = 0.12f + i * (h - 0.25f) / 3f;
                Box(g, "Kệ lưới", new Vector3(0.05f, y, -0.03f), new Vector3(d - 0.15f, 0.006f, w - 0.14f), steel, false);
            }
            Box(g, "Tay nắm", new Vector3(d / 2 + 0.045f, h / 2, w / 2 - 0.1f), new Vector3(0.03f, 0.18f, 0.025f), steel, false);
            Box(g, "Bảng điều khiển", new Vector3(d / 2 + 0.01f, h - 0.05f, 0), new Vector3(0.02f, 0.06f, w - 0.06f), bioBlack, false);
            NoShadow(Box(g, "Màn số", new Vector3(d / 2 + 0.022f, h - 0.05f, -w / 2 + 0.14f), new Vector3(0.004f, 0.035f, 0.11f), bioDisplayRed, false));
            Label(g, "37,0 °C", new Vector3(d / 2 + 0.026f, h - 0.048f, -w / 2 + 0.14f), Vector3.right, 0.02f, new Color(1f, 0.4f, 0.3f));
            Info(g, "Tủ ấm vi sinh 37 °C", "Giữ nhiệt độ 37 ± 0,5 °C để nuôi cấy vi khuẩn dạy học (E. coli K-12, vi khuẩn sữa chua). Khuẩn lạc nhìn thấy sau 18 – 24 giờ.",
                 "Úp ngược đĩa petri (nắp xuống dưới) để hơi nước không nhỏ lên mặt thạch.");
            return g;
        }

        /// <summary>Nồi hấp tiệt trùng đứng: thân trụ inox, nắp có vô lăng 6 chấu, đồng hồ áp suất, van xả, bảng điều khiển. pos: tâm đáy.</summary>
        static Transform Autoclave(Transform p, Vector3 pos, float yaw)
        {
            var g = Group("Nồi hấp tiệt trùng", p, pos, yaw);
            MeshObj(g, "Thân nồi", Lathe("T2_ThanNoiHap", 32, V(0, 0), V(0.23f, 0), V(0.25f, 0.03f), V(0.25f, 0.82f), V(0.24f, 0.86f), V(0, 0.86f)), Vector3.zero, steel, true);
            MeshObj(g, "Nắp", Lathe("T2_NapNoiHap", 32, V(0, 0), V(0.26f, 0), V(0.26f, 0.04f), V(0.2f, 0.1f), V(0, 0.12f)), new Vector3(0, 0.86f, 0), steel);
            var wheel = Group("Vô lăng", g, new Vector3(0, 1.0f, 0));
            Cyl(wheel, "Trục", Vector3.zero, 0.02f, 0.08f, bioChrome);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f;
                var spoke = Group("Chấu", wheel, new Vector3(0, 0.03f, 0), a);
                Cyl(spoke, "Tay chấu", new Vector3(0.07f, 0, 0), 0.008f, 0.14f, bioChrome, false, new Vector3(0, 0, 90));
                Ball(spoke, "Núm", new Vector3(0.145f, 0, 0), Vector3.one * 0.03f, bioBlack);
            }
            var gauge = Group("Đồng hồ áp suất", g, new Vector3(0.21f, 1.0f, 0.08f));
            Cyl(gauge, "Vỏ đồng hồ", Vector3.zero, 0.05f, 0.03f, bioChrome, false, new Vector3(0, 0, 90));
            NoShadow(Cyl(gauge, "Mặt đồng hồ", new Vector3(0.016f, 0, 0), 0.044f, 0.004f, signWhite, false, new Vector3(0, 0, 90)));
            Box(gauge, "Kim", new Vector3(0.02f, 0.012f, 0.01f), new Vector3(0.002f, 0.03f, 0.004f), red, false, new Vector3(35, 0, 0));
            Cyl(g, "Van xả", new Vector3(-0.12f, 1.0f, -0.12f), 0.012f, 0.08f, bioChrome);
            var panel = Group("Bảng điều khiển", g, new Vector3(0.22f, 0.45f, 0), 0);
            Box(panel, "Hộp", Vector3.zero, new Vector3(0.08f, 0.22f, 0.26f), bioWhite);
            NoShadow(Box(panel, "Màn số", new Vector3(0.041f, 0.05f, 0), new Vector3(0.004f, 0.05f, 0.14f), bioDisplayRed, false));
            Label(panel, "121 °C · 15 phút", new Vector3(0.045f, 0.052f, 0), Vector3.right, 0.016f, new Color(1f, 0.45f, 0.35f));
            Cyl(panel, "Nút khởi động", new Vector3(0.042f, -0.05f, 0), 0.016f, 0.008f, safetyGreen, false, new Vector3(0, 0, 90));
            Info(g, "Nồi hấp tiệt trùng đứng", "Hơi nước bão hòa 121 °C, áp suất dư ~1 bar trong 15 – 20 phút diệt mọi vi sinh vật kể cả bào tử: tiệt trùng môi trường nuôi cấy, dụng cụ, rác thải sinh học.",
                 "Đổ đủ nước cất, không đậy kín chai môi trường, chờ áp suất về 0 mới mở nắp.");
            return g;
        }

        /// <summary>Máy ly tâm để bàn: thân bo tròn, nắp trong, màn số. pos: tâm đáy, mặt trước +x.</summary>
        static Transform BenchCentrifuge(Transform p, Vector3 pos, float yaw, float s = 1f)
        {
            var g = Group("Máy ly tâm", p, pos, yaw);
            RoundBox(g, "Thân máy", new Vector3(0, 0.12f * s, 0), new Vector3(0.36f * s, 0.24f * s, 0.32f * s), 0.03f * s, bioWhite);
            MeshObj(g, "Nắp", Lathe("T2_NapLyTam", 24, V(0, 0), V(0.13f, 0), V(0.12f, 0.03f), V(0, 0.04f)), new Vector3(-0.02f * s, 0.24f * s, 0), translucentPlastic, false, null, s);
            Box(g, "Bảng phím", new Vector3(0.17f * s, 0.11f * s, 0), new Vector3(0.03f * s, 0.12f * s, 0.24f * s), bioBlack, false, new Vector3(0, 0, 20));
            NoShadow(Box(g, "Màn số", new Vector3(0.186f * s, 0.13f * s, -0.04f * s), new Vector3(0.004f, 0.03f * s, 0.09f * s), bioDisplayBlue, false, new Vector3(0, 0, 20)));
            Info(g, "Máy ly tâm", "Quay nhanh tạo lực ly tâm (tới vài nghìn × g) tách cặn tế bào, kết tủa DNA khỏi dịch.", "Đặt ống đối xứng, cân bằng khối lượng; đóng nắp trước khi chạy.");
            return g;
        }

        /// <summary>Bể ổn nhiệt (bể điều nhiệt): bể inox có nắp, mực nước, màn số. pos: tâm đáy, mặt trước +x.</summary>
        static Transform WaterBath(Transform p, Vector3 pos, float yaw, string temp = "37,0 °C")
        {
            var g = Group("Bể ổn nhiệt", p, pos, yaw);
            Box(g, "Thân bể", new Vector3(0, 0.1f, 0), new Vector3(0.32f, 0.2f, 0.5f), steel);
            Box(g, "Mặt nước", new Vector3(0, 0.17f, 0), new Vector3(0.28f, 0.004f, 0.46f), bioWater, false);
            Box(g, "Nắp gập", new Vector3(-0.15f, 0.27f, 0), new Vector3(0.02f, 0.14f, 0.48f), steel, false, new Vector3(0, 0, -12));
            Box(g, "Hộp điều khiển", new Vector3(0.2f, 0.06f, 0), new Vector3(0.08f, 0.12f, 0.5f), bioWhite);
            NoShadow(Box(g, "Màn số", new Vector3(0.241f, 0.08f, -0.1f), new Vector3(0.004f, 0.03f, 0.1f), bioDisplayRed, false));
            Label(g, temp, new Vector3(0.245f, 0.082f, -0.1f), Vector3.right, 0.016f, new Color(1f, 0.45f, 0.35f));
            TubeRack(g, new Vector3(0, 0.172f, 0.08f), 5, bioOrangeCap);
            Info(g, "Bể ổn nhiệt", "Giữ nhiệt độ nước ổn định (30 – 95 °C) để ủ mẫu: enzyme ở 37 °C, ly giải tế bào ở 60 °C khi tách chiết DNA.");
            return g;
        }

        /// <summary>Cân phân tích có lồng kính chắn gió. pos: tâm đáy, mặt trước +x.</summary>
        static Transform AnalyticalBalance(Transform p, Vector3 pos, float yaw)
        {
            var g = Group("Cân phân tích", p, pos, yaw);
            RoundBox(g, "Đế cân", new Vector3(0, 0.04f, 0), new Vector3(0.34f, 0.08f, 0.22f), 0.01f, bioWhite);
            NoShadow(Box(g, "Lồng kính", new Vector3(-0.03f, 0.18f, 0), new Vector3(0.24f, 0.2f, 0.2f), cabinetGlass));
            Box(g, "Khung lồng", new Vector3(-0.03f, 0.285f, 0), new Vector3(0.25f, 0.012f, 0.21f), bioWhite, false);
            Cyl(g, "Đĩa cân", new Vector3(-0.03f, 0.085f, 0), 0.04f, 0.006f, steel);
            NoShadow(Box(g, "Màn số", new Vector3(0.155f, 0.06f, 0), new Vector3(0.03f, 0.03f, 0.12f), bioDisplayGreen, false, new Vector3(0, 0, 25)));
            Label(g, "0,0000 g", new Vector3(0.172f, 0.068f, 0), Vector3.right, 0.016f, new Color(0.6f, 1f, 0.6f));
            return g;
        }

        /// <summary>Máy đo pH để bàn: thân máy, màn hình, giá điện cực có cần, điện cực ngâm trong cốc dung dịch bảo quản.</summary>
        static Transform PhMeter(Transform p, Vector3 pos, float yaw)
        {
            var g = Group("Máy đo pH", p, pos, yaw);
            RoundBox(g, "Thân máy", new Vector3(0, 0.035f, 0), new Vector3(0.18f, 0.07f, 0.2f), 0.01f, bioWhite);
            NoShadow(Box(g, "Màn hình", new Vector3(0.091f, 0.04f, 0), new Vector3(0.004f, 0.04f, 0.12f), bioDisplayBlue, false));
            Label(g, "pH 7,02", new Vector3(0.095f, 0.042f, 0), Vector3.right, 0.02f, new Color(0.7f, 0.85f, 1f));
            Cyl(g, "Cột giá", new Vector3(-0.06f, 0.18f, 0.12f), 0.008f, 0.3f, steel);
            Box(g, "Cần giá", new Vector3(0.0f, 0.3f, 0.12f), new Vector3(0.12f, 0.015f, 0.02f), steel, false);
            Cyl(g, "Điện cực", new Vector3(0.05f, 0.22f, 0.12f), 0.006f, 0.16f, labGlass);
            MeshObj(g, "Cốc bảo quản", Beaker("Coc100", 0.026f, 0.07f), new Vector3(0.05f, 0, 0.12f), labGlass);
            return g;
        }

        /// <summary>Máy lắc vortex: đế nặng, đầu cao su.</summary>
        static void Vortex(Transform p, Vector3 pos)
        {
            var g = Group("Máy lắc vortex", p, pos);
            MeshObj(g, "Thân", Lathe("T2_ThanVortex", 20, V(0, 0), V(0.065f, 0), V(0.06f, 0.07f), V(0.035f, 0.09f), V(0, 0.09f)), Vector3.zero, bioWhite, true);
            Cyl(g, "Đầu cao su", new Vector3(0, 0.1f, 0), 0.028f, 0.02f, rubber);
            Box(g, "Núm tốc độ", new Vector3(0.06f, 0.035f, 0), new Vector3(0.01f, 0.025f, 0.025f), bioBlack, false);
        }

        // =====================================================================
        // Đồ nhỏ: đĩa petri, giá ống, pipet, bình mẫu ngâm, chai thuốc nhuộm
        // =====================================================================

        /// <summary>Chồng đĩa petri có thạch (lật ngược như khi ủ), kind 0: khuẩn lạc kem, 1: vàng, 2: thạch trống.</summary>
        static void PetriStack(Transform p, Vector3 pos, int count, int kind)
        {
            var g = Group("Chồng đĩa petri", p, pos);
            var dish = Lathe("T2_DiaPetri", 24, V(0, 0), V(0.045f, 0), V(0.045f, 0.014f), V(0.043f, 0.014f), V(0.043f, 0.002f), V(0, 0.002f));
            for (int i = 0; i < count; i++)
            {
                float y = i * 0.0155f;
                NoShadow(MeshObj(g, "Đĩa petri", dish, new Vector3(0, y, 0), bioPetri));
                NoShadow(Cyl(g, "Thạch", new Vector3(0, y + 0.006f, 0), 0.042f, 0.008f, bioAgar));
                if (kind == 2) continue;
                var r = new System.Random(i * 13 + kind * 7 + Mathf.RoundToInt(pos.x * 100 + pos.z * 37));
                for (int c = 0; c < 6; c++)
                {
                    float a = (float)r.NextDouble() * Mathf.PI * 2, d = 0.032f * Mathf.Sqrt((float)r.NextDouble());
                    float s = 0.004f + 0.006f * (float)r.NextDouble();
                    NoShadow(Ball(g, "Khuẩn lạc", new Vector3(Mathf.Cos(a) * d, y + 0.0105f, Mathf.Sin(a) * d), new Vector3(s, 0.002f, s), kind == 0 ? bioColony : bioColonyYellow));
                }
            }
        }

        /// <summary>Giá nhựa có n ống ly tâm / ống nghiệm nắp màu.</summary>
        static void TubeRack(Transform p, Vector3 pos, int n, Material cap)
        {
            var g = Group("Giá ống", p, pos);
            float len = 0.025f * n + 0.02f;
            Box(g, "Giá", new Vector3(0, 0.02f, 0), new Vector3(0.05f, 0.04f, len), Mat("T2_GiaNhuaTrang", new Color(0.92f, 0.92f, 0.9f), 0.4f), false);
            var tube = Lathe("T2_OngLyTam", 12, V(0, 0), V(0.004f, 0.002f), V(0.007f, 0.012f), V(0.007f, 0.07f), V(0, 0.07f));
            for (int i = 0; i < n; i++)
            {
                float z = -len / 2 + 0.02f + i * 0.025f;
                NoShadow(MeshObj(g, "Ống", tube, new Vector3(0, 0.01f, z), translucentPlastic));
                NoShadow(Cyl(g, "Nắp", new Vector3(0, 0.083f, z), 0.0085f, 0.012f, cap));
            }
        }

        /// <summary>Giá micropipet 3 cây (P20 vàng, P200 xanh, P1000 xanh dương) + hộp đầu côn.</summary>
        static void PipetteStation(Transform p, Vector3 pos, float yaw)
        {
            var g = Group("Bộ micropipet", p, pos, yaw);
            Box(g, "Giá", new Vector3(-0.05f, 0.15f, 0), new Vector3(0.04f, 0.3f, 0.2f), bioWhite);
            Box(g, "Chân giá", new Vector3(0, 0.005f, 0), new Vector3(0.14f, 0.01f, 0.2f), bioWhite, false);
            var colors = new[] { bioYellowTip, Mat("T2_PipetXanhLa", new Color(0.3f, 0.75f, 0.35f), 0.45f), bioBlueTip };
            var pip = Lathe("T2_ThanPipet", 12, V(0, 0), V(0.002f, 0.0f), V(0.006f, 0.06f), V(0.012f, 0.08f), V(0.014f, 0.2f), V(0.008f, 0.23f), V(0, 0.23f));
            for (int i = 0; i < 3; i++)
            {
                float z = -0.06f + i * 0.06f;
                MeshObj(g, "Micropipet", pip, new Vector3(-0.02f, 0.06f, z), bioWhite);
                Cyl(g, "Nút đẩy", new Vector3(-0.02f, 0.3f, z), 0.008f, 0.02f, colors[i]);
            }
            for (int i = 0; i < 2; i++)
            {
                var box = Group("Hộp đầu côn", g, new Vector3(0.1f, 0, -0.07f + i * 0.14f));
                Box(box, "Hộp", new Vector3(0, 0.03f, 0), new Vector3(0.08f, 0.06f, 0.12f), i == 0 ? bioYellowTip : bioBlueTip, false);
                NoShadow(Box(box, "Nắp trong", new Vector3(0, 0.062f, 0), new Vector3(0.08f, 0.004f, 0.12f), translucentPlastic, false));
            }
        }

        /// <summary>Bình mẫu ngâm formalin: lọ thủy tinh, nắp đen, mẫu vật bên trong (0 cá, 1 ếch, 2 rắn cuộn, 3 tim, 4 cây non).</summary>
        static void SpecimenJar(Transform p, Vector3 pos, int kind, float s = 1f)
        {
            var g = Group("Bình mẫu ngâm", p, pos);
            float r = 0.07f * s, h = 0.24f * s;
            NoShadow(MeshObj(g, "Bình", Lathe("T2_BinhMauNgam", 24, V(0, 0), V(0.07f, 0), V(0.072f, 0.01f), V(0.072f, 0.22f), V(0.064f, 0.235f), V(0.064f, 0.24f), V(0, 0.24f)), Vector3.zero, labGlass, false, null, s));
            NoShadow(Cyl(g, "Dịch ngâm", new Vector3(0, h * 0.43f, 0), r * 0.94f, h * 0.84f, bioFormalin));
            Cyl(g, "Nắp", new Vector3(0, h + 0.012f * s, 0), r * 0.95f, 0.025f * s, bioBlack);
            Box(g, "Nhãn", new Vector3(r + 0.0015f, h * 0.2f, 0), new Vector3(0.002f, 0.04f * s, 0.06f * s), labelPaper, false);
            switch (kind)
            {
                case 0:
                    Ball(g, "Thân cá", new Vector3(0, h * 0.5f, 0), new Vector3(0.03f, 0.14f, 0.05f) * s, bioSpecimen);
                    Ball(g, "Vây đuôi", new Vector3(0, h * 0.18f, 0), new Vector3(0.008f, 0.04f, 0.05f) * s, bioSpecimen);
                    break;
                case 1:
                    Ball(g, "Thân ếch", new Vector3(0, h * 0.55f, 0), new Vector3(0.06f, 0.08f, 0.045f) * s, bioSpecimenPale);
                    foreach (int sd in new[] { -1, 1 })
                        Ball(g, "Chân ếch", new Vector3(sd * 0.03f * s, h * 0.3f, 0), new Vector3(0.018f, 0.08f, 0.018f) * s, bioSpecimenPale);
                    break;
                case 2:
                    for (int i = 0; i < 3; i++)
                        MeshObj(g, "Rắn cuộn", Lathe("T2_VongRan", 16, V(0.035f, 0), V(0.045f, 0.006f), V(0.035f, 0.012f), V(0.025f, 0.006f), V(0.035f, 0)), new Vector3(0, 0.02f * s + i * 0.025f * s, 0), bioSpecimen, false, null, s);
                    break;
                case 3:
                    Ball(g, "Tim", new Vector3(0, h * 0.45f, 0), new Vector3(0.07f, 0.09f, 0.06f) * s, Mat("T2_TimNgam", new Color(0.55f, 0.28f, 0.25f), 0.5f));
                    break;
                default:
                    Cyl(g, "Thân cây", new Vector3(0, h * 0.45f, 0), 0.003f * s, h * 0.7f, bioLeafLight);
                    foreach (int sd in new[] { -1, 1 })
                        Ball(g, "Lá", new Vector3(sd * 0.02f * s, h * 0.6f, 0), new Vector3(0.035f, 0.015f, 0.02f) * s, bioLeafLight);
                    break;
            }
        }

        /// <summary>Chai thuốc nhuộm nhỏ giọt (nắp màu, nhãn): xanh methylen, lugol, tím tinh thể, safranin...</summary>
        static void StainBottle(Transform p, Vector3 pos, Material liquid, string label)
        {
            var g = Group("Chai " + label, p, pos);
            NoShadow(MeshObj(g, "Chai", DropperBottleMesh(), Vector3.zero, amber));
            Cyl(g, "Nắp nhỏ giọt", new Vector3(0, 0.07f, 0), 0.009f, 0.025f, liquid);
            Box(g, "Nhãn", new Vector3(0.0185f, 0.03f, 0), new Vector3(0.002f, 0.03f, 0.022f), labelPaper, false);
        }

        /// <summary>Khay inox dụng cụ: kẹp, dao mổ, kim mũi mác, lam kính, lamen.</summary>
        static void InstrumentTray(Transform p, Vector3 pos, float yaw)
        {
            var g = Group("Khay dụng cụ", p, pos, yaw);
            Box(g, "Khay", new Vector3(0, 0.006f, 0), new Vector3(0.18f, 0.012f, 0.26f), steel, false);
            Box(g, "Kẹp", new Vector3(0.04f, 0.015f, -0.02f), new Vector3(0.008f, 0.004f, 0.14f), bioChrome, false, new Vector3(0, 8, 0));
            Box(g, "Dao mổ", new Vector3(-0.01f, 0.015f, 0.0f), new Vector3(0.01f, 0.004f, 0.15f), bioChrome, false, new Vector3(0, -6, 0));
            Box(g, "Kim mũi mác", new Vector3(-0.05f, 0.015f, 0.02f), new Vector3(0.006f, 0.004f, 0.13f), bioChrome, false, new Vector3(0, 4, 0));
            for (int i = 0; i < 3; i++)
                NoShadow(Box(g, "Lam kính", new Vector3(0.0f, 0.0135f + i * 0.0015f, -0.09f), new Vector3(0.026f, 0.0012f, 0.076f), labGlass, false, new Vector3(0, i * 7, 0)));
        }

        /// <summary>Hộp đựng tiêu bản cố định (nắp mở, các tiêu bản xếp đứng).</summary>
        static void SlideBox(Transform p, Vector3 pos, float yaw, bool info = false)
        {
            var g = Group("Hộp tiêu bản", p, pos, yaw);
            Box(g, "Hộp", new Vector3(0, 0.017f, 0), new Vector3(0.1f, 0.034f, 0.2f), Mat("T2_HopTieuBan", new Color(0.25f, 0.2f, 0.16f), 0.35f));
            Box(g, "Nắp mở", new Vector3(-0.065f, 0.06f, 0), new Vector3(0.006f, 0.09f, 0.2f), Mat("T2_HopTieuBan", new Color(0.25f, 0.2f, 0.16f), 0.35f), false, new Vector3(0, 0, -15));
            for (int i = 0; i < 10; i++)
                NoShadow(Box(g, "Tiêu bản", new Vector3(0.005f, 0.04f, -0.085f + i * 0.019f), new Vector3(0.076f, 0.026f, 0.0012f), labGlass, false));
            if (info) Info(g, "Hộp tiêu bản cố định", "Tiêu bản nhuộm H&E: phế nang phổi, nephron (thận), synapse thần kinh, sarcomere (cơ vân), tế bào viền dạ dày, tiểu thùy gan; tiêu bản biểu bì hành, khí khổng, phết máu, vi khuẩn sữa chua (Gram +), nấm men.",
                 "Cùng 6 cảnh hiển vi của Phòng Lab Sinh học 3D trên web.");
        }

        // =====================================================================
        // Mô hình dạy học
        // =====================================================================

        /// <summary>Mô hình ADN xoắn kép trên đế: 2 sợi khung đường – photphat, các cặp base A–T (đỏ – xanh), G–X (lục – vàng).</summary>
        static void DnaModel(Transform p, Vector3 pos, float height = 0.6f)
        {
            var g = Group("Mô hình ADN", p, pos);
            Cyl(g, "Đế", new Vector3(0, 0.015f, 0), 0.09f, 0.03f, bioBlack);
            Cyl(g, "Trục", new Vector3(0, height / 2 + 0.03f, 0), 0.005f, height, steel);
            var cols = new[] { Mat("T2_BaseA", new Color(0.86f, 0.2f, 0.18f), 0.5f), Mat("T2_BaseT", new Color(0.2f, 0.42f, 0.9f), 0.5f),
                               Mat("T2_BaseG", new Color(0.2f, 0.72f, 0.3f), 0.5f), Mat("T2_BaseX", new Color(0.95f, 0.8f, 0.15f), 0.5f) };
            var spine = Mat("T2_KhungDuong", new Color(0.88f, 0.88f, 0.86f), 0.5f);
            int n = Mathf.RoundToInt(height / 0.034f);
            const float r = 0.055f;
            for (int i = 0; i < n; i++)
            {
                float y = 0.05f + i * 0.034f, a = i * 36f;
                var rung = Group("Cặp base", g, new Vector3(0, y, 0), a);
                int pair = (i * 7 + 3) % 4;
                var m1 = cols[pair < 2 ? 0 : 2];
                var m2 = cols[pair < 2 ? 1 : 3];
                if (pair % 2 == 1) { var t = m1; m1 = m2; m2 = t; }
                Cyl(rung, "Base", new Vector3(-r / 2, 0, 0), 0.006f, r, m1, false, new Vector3(0, 0, 90));
                Cyl(rung, "Base", new Vector3(r / 2, 0, 0), 0.006f, r, m2, false, new Vector3(0, 0, 90));
                Ball(rung, "Đường – photphat", new Vector3(-r, 0, 0), Vector3.one * 0.016f, spine);
                Ball(rung, "Đường – photphat", new Vector3(r, 0, 0), Vector3.one * 0.016f, spine);
            }
            Info(g, "Mô hình ADN xoắn kép", "Hai mạch đơn ngược chiều xoắn phải, đường kính 2 nm, mỗi vòng xoắn 10 cặp base dài 3,4 nm. A liên kết T (2 liên kết hiđro), G liên kết X (3 liên kết hiđro).");
        }

        /// <summary>Mô hình tế bào động vật bổ đôi: màng, tế bào chất, nhân, ti thể, lưới nội chất (khối cơ bản).</summary>
        static void CellModel(Transform p, Vector3 pos)
        {
            var g = Group("Mô hình tế bào", p, pos);
            Cyl(g, "Đế", new Vector3(0, 0.01f, 0), 0.16f, 0.02f, bioBlack);
            MeshObj(g, "Nửa tế bào", Lathe("T2_NuaTeBao", 24, V(0, 0), V(0.15f, 0), V(0.14f, 0.06f), V(0.1f, 0.11f), V(0.05f, 0.135f), V(0, 0.14f)), new Vector3(0, 0.02f, 0),
                    Mat("T2_TeBaoChat", new Color(0.95f, 0.78f, 0.7f), 0.5f));
            Ball(g, "Nhân", new Vector3(0.0f, 0.1f, 0.0f), Vector3.one * 0.08f, Mat("T2_NhanTeBao", new Color(0.45f, 0.3f, 0.65f), 0.55f));
            Ball(g, "Hạch nhân", new Vector3(0.012f, 0.135f, 0.01f), Vector3.one * 0.025f, Mat("T2_HachNhan", new Color(0.3f, 0.15f, 0.45f), 0.55f));
            var mito = Mat("T2_TiThe", new Color(0.92f, 0.45f, 0.25f), 0.5f);
            foreach (var (x, z, a) in new[] { (0.08f, 0.05f, 20f), (-0.07f, 0.07f, -40f), (0.05f, -0.09f, 70f) })
                Ball(g, "Ti thể", new Vector3(x, 0.09f, z), new Vector3(0.05f, 0.022f, 0.025f), mito).transform.localEulerAngles = new Vector3(0, a, 0);
            Info(g, "Mô hình tế bào động vật", "Màng sinh chất, tế bào chất, nhân (có hạch nhân), ti thể, lưới nội chất, bộ máy Golgi. Tế bào thực vật có thêm thành xenlulôzơ, lục lạp và không bào lớn.");
        }

        /// <summary>Mô hình bán thân người tháo lắp trên đế xoay (cơ quan nhìn thấy ở nửa trước).</summary>
        static void TorsoModel(Transform p, Vector3 pos, float yaw)
        {
            var g = Group("Mô hình bán thân", p, pos, yaw);
            Cyl(g, "Đế xoay", new Vector3(0, 0.03f, 0), 0.16f, 0.06f, bioBlack);
            Cyl(g, "Cổ đế", new Vector3(0, 0.1f, 0), 0.03f, 0.1f, bioChrome);
            var skin = Mat("T2_DaMoHinh", new Color(0.93f, 0.76f, 0.64f), 0.5f);
            MeshObj(g, "Thân", Lathe("T2_ThanMoHinh", 24, V(0, 0), V(0.12f, 0), V(0.15f, 0.12f), V(0.14f, 0.3f), V(0.17f, 0.44f), V(0.1f, 0.52f), V(0.05f, 0.55f), V(0, 0.55f)), new Vector3(0, 0.15f, 0), skin).transform.localScale = new Vector3(1f, 1f, 0.65f);
            Ball(g, "Đầu", new Vector3(0, 0.82f, 0), new Vector3(0.17f, 0.22f, 0.19f), skin);
            // cơ quan lộ ở mặt trước (+x)
            Ball(g, "Phổi trái", new Vector3(0.07f, 0.55f, -0.05f), new Vector3(0.05f, 0.14f, 0.08f), Mat("T2_PhoiMoHinh", new Color(0.85f, 0.5f, 0.55f), 0.55f));
            Ball(g, "Phổi phải", new Vector3(0.07f, 0.55f, 0.055f), new Vector3(0.05f, 0.15f, 0.085f), Mat("T2_PhoiMoHinh", new Color(0.85f, 0.5f, 0.55f), 0.55f));
            Ball(g, "Tim", new Vector3(0.09f, 0.5f, -0.01f), new Vector3(0.05f, 0.07f, 0.055f), bioMuscle);
            Ball(g, "Gan", new Vector3(0.085f, 0.39f, 0.04f), new Vector3(0.05f, 0.06f, 0.12f), Mat("T2_GanMoHinh", new Color(0.5f, 0.2f, 0.16f), 0.55f));
            Ball(g, "Dạ dày", new Vector3(0.09f, 0.37f, -0.05f), new Vector3(0.04f, 0.06f, 0.07f), Mat("T2_DaDayMoHinh", new Color(0.88f, 0.6f, 0.5f), 0.55f));
            Ball(g, "Ruột", new Vector3(0.09f, 0.25f, 0), new Vector3(0.05f, 0.12f, 0.15f), Mat("T2_RuotMoHinh", new Color(0.9f, 0.68f, 0.55f), 0.55f));
            Info(g, "Mô hình bán thân người tháo lắp", "Các cơ quan tháo rời được: phổi, tim, gan, dạ dày, ruột... đối chiếu với mô hình 3D BodyParts3D trên bàn giải phẫu số.");
        }

        // =====================================================================
        // Cây xanh: lá cong ghép thành một lưới cho mỗi kiểu cây (1 lượt vẽ / cây)
        // =====================================================================

        static readonly Dictionary<string, Mesh> plantMeshes = new Dictionary<string, Mesh>();

        /// <summary>Lưới cây: count lá cong xếp hoa thị quanh trục, mỗi tầng xoay góc vàng, lá ngoài ngả ra, lá trong dựng đứng.</summary>
        static Mesh PlantMesh(string name, int count, float leafLen, float leafW, float height, float droop)
        {
            if (plantMeshes.TryGetValue(name, out var cached) && cached != null) return cached;
            var leaf = LeafMesh("T2_La_" + name, leafLen, leafW, droop);
            var combine = new List<CombineInstance>();
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / Mathf.Max(1, count - 1);
                float yaw = i * 137.5f, pitch = Mathf.Lerp(-70f, -15f, t), y = height * (1f - t) * 0.8f;
                var m = Matrix4x4.TRS(new Vector3(0, y, 0), Quaternion.Euler(0, yaw, 0) * Quaternion.Euler(pitch, 0, 0), Vector3.one * Mathf.Lerp(0.75f, 1.1f, t));
                combine.Add(new CombineInstance { mesh = leaf, transform = m });
            }
            string path = $"{RootT2}/Meshes/T2_Cay_{name}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh { name = "T2_Cay_" + name };
            else mesh.Clear();
            mesh.CombineMeshes(combine.ToArray(), true, true);
            mesh.RecalculateBounds();
            if (isNew) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            plantMeshes[name] = mesh;
            return mesh;
        }

        /// <summary>Cây trong chậu. kind 0: cây lá to (môn), 1: dương xỉ, 2: xà lách / rau, 3: cây non (đậu), 4: cây cảnh cao.</summary>
        static void Plant(Transform p, Vector3 pos, int kind, float scale = 1f, Material potMat = null)
        {
            var g = Group("Cây", p, pos, (pos.x * 53f + pos.z * 31f) % 360f);
            float potR = kind == 4 ? 0.18f : kind == 2 ? 0.07f : kind == 3 ? 0.05f : 0.11f;
            float potH = kind == 4 ? 0.45f : kind == 2 ? 0.08f : kind == 3 ? 0.07f : 0.16f;
            potR *= scale; potH *= scale;
            var pot = Lathe($"T2_Chau{kind}", 20, V(0, 0), V(potR * 0.75f, 0), V(potR * 0.78f, 0.01f), V(potR, potH * 0.92f), V(potR * 1.06f, potH * 0.95f), V(potR * 1.06f, potH), V(potR * 0.95f, potH), V(0, potH * 0.85f));
            MeshObj(g, "Chậu", pot, Vector3.zero, potMat ?? (kind == 4 ? potWhite : bioTerracotta), kind == 4);
            Cyl(g, "Đất", new Vector3(0, potH * 0.86f, 0), potR * 0.92f, 0.01f, bioSoil);
            Mesh m;
            Material leafMat;
            switch (kind)
            {
                case 0: m = PlantMesh("Mon", 9, 0.3f, 0.2f, 0.25f, 0.06f); leafMat = bioLeaf; break;
                case 1: m = PlantMesh("DuongXi", 14, 0.32f, 0.07f, 0.12f, 0.1f); leafMat = bioLeafDark; break;
                case 2: m = PlantMesh("XaLach", 12, 0.1f, 0.08f, 0.05f, 0.02f); leafMat = bioLeafLight; break;
                case 3: m = PlantMesh("DauNon", 6, 0.06f, 0.04f, 0.12f, 0.01f); leafMat = bioLeafLight; break;
                default: m = PlantMesh("CayCao", 18, 0.34f, 0.12f, 0.9f, 0.08f); leafMat = bioLeaf; break;
            }
            if (kind == 3) Cyl(g, "Thân non", new Vector3(0, potH + 0.06f * scale, 0), 0.003f, 0.12f * scale, bioLeafLight);
            if (kind == 4) Cyl(g, "Thân cây", new Vector3(0, potH + 0.35f * scale, 0), 0.015f * scale, 0.7f * scale, trunk);
            var leaves = MeshObj(g, "Tán lá", m, new Vector3(0, potH * 0.9f + (kind == 4 ? 0.25f * scale : 0), 0), leafMat, false, null, scale);
            if (kind == 2 || kind == 3) NoShadow(leaves);
        }

        // =====================================================================
        // Ảnh vân vẽ bằng code (áp phích, biểu tượng)
        // =====================================================================

        static bool Ell(float x, float y, float cx, float cy, float rx, float ry) => (x - cx) * (x - cx) / (rx * rx) + (y - cy) * (y - cy) / (ry * ry) < 1f;

        /// <summary>Áp phích "Cấu trúc tế bào": tế bào động vật (tròn) và thực vật (vuông có thành, lục lạp, không bào).</summary>
        static Color CellPosterPixel(int x, int y)
        {
            var bg = new Color(0.97f, 0.97f, 0.95f);
            if (y > 312) return new Color(0.12f, 0.45f, 0.3f);  // dải tiêu đề
            if (y < 24 || x < 6 || x > 249) return y < 24 ? new Color(0.88f, 0.9f, 0.88f) : bg;
            // tế bào động vật (trên)
            float ax = x - 128, ay = y - 225;
            if (Ell(x, y, 128, 225, 92, 70))
            {
                if (Ell(x, y, 120, 230, 30, 26)) return Ell(x, y, 125, 235, 9, 8) ? new Color(0.25f, 0.1f, 0.4f) : new Color(0.55f, 0.35f, 0.75f);
                if (Ell(x, y, 175, 205, 18, 8) || Ell(x, y, 80, 250, 16, 7) || Ell(x, y, 170, 255, 15, 7)) return new Color(0.93f, 0.48f, 0.25f);
                if (Mathf.Abs(Mathf.Sin(ax * 0.15f) * 6f - (ay + 30f)) < 2f && ax > -60 && ax < 0) return new Color(0.35f, 0.6f, 0.9f);
                return Ell(x, y, 128, 225, 89, 67) ? new Color(0.98f, 0.85f, 0.78f) : new Color(0.75f, 0.45f, 0.4f);
            }
            // tế bào thực vật (dưới)
            if (x > 40 && x < 216 && y > 36 && y < 140)
            {
                if (x < 46 || x > 210 || y < 42 || y > 134) return new Color(0.35f, 0.6f, 0.25f);  // thành tế bào
                if (Ell(x, y, 140, 88, 52, 34)) return new Color(0.7f, 0.85f, 0.95f);              // không bào
                if (Ell(x, y, 72, 110, 18, 15)) return new Color(0.55f, 0.35f, 0.75f);              // nhân
                foreach (var (cx, cy) in new[] { (64, 60), (90, 70), (190, 60), (200, 118), (100, 122) })
                    if (Ell(x, y, cx, cy, 11, 6)) return new Color(0.2f, 0.6f, 0.2f);               // lục lạp
                return new Color(0.88f, 0.95f, 0.8f);
            }
            return bg;
        }

        /// <summary>Áp phích "Chu kỳ tế bào": vòng tròn chia pha G1, S, G2, M (màu khác nhau), mũi tên.</summary>
        static Color CycleTexPixel(int x, int y)
        {
            var bg = new Color(0.97f, 0.97f, 0.95f);
            if (y > 312) return new Color(0.12f, 0.4f, 0.55f);
            float dx = x - 128, dy = y - 165, r = Mathf.Sqrt(dx * dx + dy * dy);
            if (r > 62 && r < 100)
            {
                float a = (Mathf.Atan2(dy, dx) * Mathf.Rad2Deg + 360f + 90f) % 360f;
                if (a < 140) return new Color(0.4f, 0.75f, 0.45f);   // G1
                if (a < 250) return new Color(0.35f, 0.55f, 0.9f);   // S
                if (a < 320) return new Color(0.95f, 0.75f, 0.25f);  // G2
                return new Color(0.9f, 0.35f, 0.35f);                // M
            }
            if (r < 40)
            {
                bool chromo = Mathf.Abs(dx + dy * 0.6f) < 3 && Mathf.Abs(dx) < 20 || Mathf.Abs(dx - dy * 0.6f) < 3 && Mathf.Abs(dx) < 20;
                return chromo ? new Color(0.45f, 0.2f, 0.6f) : new Color(0.98f, 0.9f, 0.85f);
            }
            if (y < 50 && y > 30 && x > 30 && x < 226 && (x / 6) % 5 != 4) return new Color(0.7f, 0.7f, 0.72f);
            return bg;
        }

        /// <summary>Áp phích "Cây sự sống": thân, nhánh ba lãnh giới (vi khuẩn, cổ khuẩn, nhân thực) và các chấm loài.</summary>
        static Color TreeOfLifePixel(int x, int y)
        {
            var bg = new Color(0.96f, 0.97f, 0.94f);
            if (y > 312) return new Color(0.3f, 0.45f, 0.15f);
            // thân và ba nhánh chính
            if (Mathf.Abs(x - 128) < 5 && y < 120 && y > 30) return new Color(0.45f, 0.3f, 0.18f);
            foreach (var (tx, ty, col) in new[] { (55, 270, new Color(0.25f, 0.55f, 0.85f)), (128, 285, new Color(0.85f, 0.45f, 0.25f)), (200, 270, new Color(0.3f, 0.65f, 0.3f)) })
            {
                float t = Mathf.InverseLerp(120, ty, y);
                if (y >= 120 && y <= ty && Mathf.Abs(x - Mathf.Lerp(128, tx, t)) < 4) return col;
                for (int k = 0; k < 5; k++)
                {
                    float lx = tx + (k - 2) * 14, ly = ty - 8 + (k % 2) * 10;
                    if ((x - lx) * (x - lx) + (y - ly) * (y - ly) < 30) return col;
                }
            }
            if (y < 30 && y > 18 && x > 50 && x < 206) return new Color(0.75f, 0.75f, 0.7f);
            return bg;
        }

        /// <summary>Áp phích "Cấu trúc ADN": chuỗi xoắn kép hai mạch, các cặp base màu.</summary>
        static Color DnaPosterPixel(int x, int y)
        {
            var bg = new Color(0.95f, 0.96f, 0.98f);
            if (y > 312) return new Color(0.35f, 0.25f, 0.6f);
            if (y < 20 || y > 300) return bg;
            float phase = y * 0.045f;
            float a = 128 + Mathf.Sin(phase) * 60, b = 128 - Mathf.Sin(phase) * 60;
            if (Mathf.Abs(x - a) < 5) return Mathf.Cos(phase) > 0 ? new Color(0.25f, 0.35f, 0.65f) : new Color(0.55f, 0.6f, 0.75f);
            if (Mathf.Abs(x - b) < 5) return Mathf.Cos(phase) < 0 ? new Color(0.25f, 0.35f, 0.65f) : new Color(0.55f, 0.6f, 0.75f);
            if (y % 14 < 4 && x > Mathf.Min(a, b) && x < Mathf.Max(a, b))
            {
                int k = (y / 14) % 4;
                bool left = x < 128;
                var c = k < 2 ? (left ? new Color(0.86f, 0.25f, 0.2f) : new Color(0.25f, 0.45f, 0.9f)) : (left ? new Color(0.25f, 0.72f, 0.3f) : new Color(0.95f, 0.78f, 0.15f));
                return c;
            }
            return bg;
        }

        /// <summary>Biểu tượng nguy hại sinh học (biohazard) màu đen trên nền cam: 3 vòng cung và vòng tròn giữa.</summary>
        static Color BiohazardPixel(int x, int y)
        {
            var bg = new Color(0.97f, 0.55f, 0.1f);
            float cx = 64, cy = 60, dx = x - cx, dy = y - cy, r = Mathf.Sqrt(dx * dx + dy * dy);
            bool on = false;
            for (int i = 0; i < 3; i++)
            {
                float a = Mathf.PI / 2 + i * 2 * Mathf.PI / 3;
                float ox = cx + Mathf.Cos(a) * 18, oy = cy + Mathf.Sin(a) * 18;
                float d = Mathf.Sqrt((x - ox) * (x - ox) + (y - oy) * (y - oy));
                float ix = cx + Mathf.Cos(a) * 25, iy = cy + Mathf.Sin(a) * 25;
                float di = Mathf.Sqrt((x - ix) * (x - ix) + (y - iy) * (y - iy));
                if (d < 28 && di > 18) on = true;
            }
            if (r > 13 && r < 19) on = true;
            if (r < 8) on = false;
            if (y < 14 && y > 6 && x > 20 && x < 108) on = true;  // dải chữ giả
            return on ? new Color(0.08f, 0.08f, 0.08f) : bg;
        }
    }
}
