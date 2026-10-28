using Khkt.Demo;
using Khkt.Interaction;
using UnityEngine;
using static Khkt.EditorTools.LabKit;

namespace Khkt.EditorTools
{
    /// <summary>
    /// Dãy phòng phía nam (cùng tầng): hành lang, Phòng máy phân tích hiện đại (UV-Vis, FTIR, AAS, GC-MS, HPLC,
    /// chuẩn độ điện thế tự động chạy thật trên mẫu; SEM, XRD, NMR, tủ sấy, lò nung... trưng bày có thẻ thông tin)
    /// và Phòng tự động hóa & robot (trạm sạc robot trợ lý, cánh tay robot cộng tác, máy hút nhả tự động,
    /// máy in 3D, góc kính VR, tủ máy chủ AI, màn hình web quản lý).
    /// </summary>
    public static partial class LabSceneBuilder
    {
        static Material machineScreen;

        static EquipmentInfo Info(Transform t, string title, string desc, string usage = "")
        {
            var e = t.gameObject.GetComponent<EquipmentInfo>();
            if (e == null) e = t.gameObject.AddComponent<EquipmentInfo>();
            e.title = title;
            e.description = desc;
            e.usage = usage;
            EnsureCollider(t);
            return e;
        }

        static void BuildSouthWing(Transform root)
        {
            machineScreen = Mat("ManHinhMay", new Color(0.02f, 0.04f, 0.08f), 0.6f, 0, emission: new Color(1f, 1f, 1f));
            var g = Group("Dãy phòng phía nam", root);

            // ---------------- Hành lang ----------------
            var hall = Group("Hành lang", g);
            foreach (float x in new[] { 3.5f, 9.5f, 15.5f, 21.5f })
                LedPanel(hall, "Đèn hành lang", x, CorridorZ / 2, 0, 0, false);
            RoomLights(hall, new[] { 6.7f, 19.8f }, new[] { CorridorZ / 2 }, 9f, 2.4f);
            LedStrip(hall, "Đèn hắt viền", new Vector3(0.2f, H - 0.008f, -0.18f), new Vector3(StoreX - 0.2f, H - 0.008f, -0.18f), 0.04f, ledBlue);
            LedStrip(hall, "Đèn hắt viền", new Vector3(0.2f, H - 0.008f, CorridorZ + 0.18f), new Vector3(StoreX - 0.2f, H - 0.008f, CorridorZ + 0.18f), 0.04f, ledBlue);
            // Các nhà hóa học, dải lịch sử hóa học, màn hình bảng tin điện tử (thay bảng ghim cũ)
            CorridorGallery(hall);
            // Mảng màu nhận diện quanh từng cửa và biển phòng song ngữ (mặt tường phía hành lang)
            DoorZoneGraphic(hall, 1.6f, 3.2f, 0f, -1, zoneLab, "PHÒNG THÍ NGHIỆM HÓA HỌC", "CHEMISTRY LAB");
            DoorZoneGraphic(hall, 21.0f, 22.2f, 0f, -1, zoneStore, "PHÒNG HÓA CHẤT", "CHEMICAL STORE");
            DoorZoneGraphic(hall, 5.9f, 7.5f, CorridorZ, +1, zoneAnalysis, "PHÒNG MÁY PHÂN TÍCH", "ANALYTICAL LAB");
            DoorZoneGraphic(hall, 19.0f, 20.6f, CorridorZ, +1, zoneRobot, "PHÒNG TỰ ĐỘNG HÓA & ROBOT", "ROBOTICS LAB");

            // mỗi phòng một màu nhận diện cho các dải nhấn trên bàn, máy
            var keep = accent;
            accent = Mat("MauNhanPhongMay", zoneAnalysis, 0.45f);
            AnalysisRoom(g);
            accent = Mat("MauNhanPhongRobot", zoneRobot, 0.45f);
            RoboticsRoom(g);
            accent = keep;
        }

        // =====================================================================
        // Phòng máy phân tích hiện đại (x 0..13,5)
        // =====================================================================

        static void AnalysisRoom(Transform p)
        {
            var g = Group("Phòng máy phân tích hiện đại", p);
            float zBench = WingZ + 0.45f;
            const float top = 0.9f;
            var bench = Group("Bàn máy phân tích", g, new Vector3(6.75f, 0, zBench));
            Box(bench, "Tủ dưới", new Vector3(0, 0.45f, 0), new Vector3(12.6f, 0.82f, 0.72f), cabinet);
            Box(bench, "Mặt bàn", new Vector3(0, top - 0.02f, 0.02f), new Vector3(12.7f, 0.04f, 0.78f), epoxy);
            Box(bench, "Dải nhấn", new Vector3(0, top - 0.05f, 0.412f), new Vector3(12.7f, 0.03f, 0.004f), accent, false);
            bench.Find("Mặt bàn").gameObject.AddComponent<PlaceSurface>();

            Analyzer(g, AnalyzerInstrument.Kind.UvVis, new Vector3(1.4f, top, zBench), new Vector3(0.55f, 0.22f, 0.45f),
                     "Phổ hấp thụ ánh sáng khả kiến của dung dịch; tìm bước sóng hấp thụ cực đại, định lượng theo định luật Beer.");
            Analyzer(g, AnalyzerInstrument.Kind.Ftir, new Vector3(3.5f, top, zBench), new Vector3(0.6f, 0.3f, 0.5f),
                     "Phổ hồng ngoại: nhận biết nhóm chức (O-H, C=O, N-H, C=C, vòng thơm) trong hợp chất hữu cơ.");
            Analyzer(g, AnalyzerInstrument.Kind.Aas, new Vector3(5.8f, top, zBench), new Vector3(0.9f, 0.45f, 0.55f),
                     "Đo nồng độ từng kim loại (mg/L) nhờ nguyên tử hóa mẫu trong ngọn lửa; kiểm tra kim loại nặng trong nước.");
            Analyzer(g, AnalyzerInstrument.Kind.GcMs, new Vector3(8.3f, top, zBench), new Vector3(1.0f, 0.55f, 0.6f),
                     "Tách các chất hữu cơ dễ bay hơi bằng cột sắc ký, khối phổ cho biết khối lượng phân tử (m/z).");
            Analyzer(g, AnalyzerInstrument.Kind.Hplc, new Vector3(10.6f, top, zBench), new Vector3(0.5f, 0.75f, 0.55f),
                     "Sắc ký lỏng hiệu năng cao: tách và định lượng chất hữu cơ tan (đường, axit hữu cơ, phẩm màu...).");
            Analyzer(g, AnalyzerInstrument.Kind.AutoTitrator, new Vector3(12.2f, top, zBench), new Vector3(0.45f, 0.35f, 0.4f),
                     "Tự nhỏ dung dịch chuẩn, đo pH liên tục, tìm điểm tương đương ở bước nhảy pH và tính nồng độ.");

            // Bàn chuẩn bị mẫu giữa phòng, với ống mẫu có sẵn
            var prep = Group("Bàn chuẩn bị mẫu", g, new Vector3(6.75f, 0, -5.6f));
            Box(prep, "Tủ dưới", new Vector3(0, 0.45f, 0), new Vector3(4.2f, 0.82f, 1.2f), cabinet);
            var ptop = Box(prep, "Mặt bàn", new Vector3(0, top - 0.02f, 0), new Vector3(4.3f, 0.04f, 1.3f), epoxy);
            ptop.AddComponent<PlaceSurface>();
            // chữ nằm trên mặt bàn, đọc xuôi từ phía cửa vào (+z)
            Label(prep, "MẪU PHÂN TÍCH", new Vector3(0, top + 0.004f, 0.55f), Vector3.up, 0.05f, new Color(0.6f, 0.85f, 0.9f))
                .transform.localRotation = Quaternion.Euler(90, 180, 0);
            // Thành phần cân bằng điện tích để pH hợp lý ("CO3" = họ H2CO3, "SO4" = họ HSO4⁻ trong bộ máy hóa học)
            var samples = new (string name, string[] parts)[]
            {
                ("Mẫu nước giếng", new[] { "Ca2+:0.001", "Mg2+:0.0005", "Fe2+:0.00005", "Cl-:0.001", "CO3:0.0033" }),         // pH ~6,6
                ("Mẫu nước thải mạ điện", new[] { "Cu2+:0.004", "Zn2+:0.003", "Ni2+:0.002", "SO4:0.009", "Cl-:0.004" }),    // pH ~2,6
                ("Mẫu giấm ăn", new[] { "Ac:0.83" }),
                ("Mẫu rượu", new[] { "C2H5OH:5.2" }),
                ("Mẫu nước cam", new[] { "Cit:0.03", "hc:578:0.25", "hc:580:0.1" }),
                ("Mẫu đất chua", new[] { "Al3+:0.004", "Fe3+:0.001", "SO4:0.008" }),                                         // pH ~3
                ("Mẫu dầu chuối", new[] { "hc:539:0.4", "C2H5OH:0.5" }),
                ("Mẫu nước thải xi mạ crom", new[] { "CrVI:0.02", "Na+:0.024", "SO4:0.004" }),                              // da cam, pH ~2,6
                ("Mẫu rượu nghi pha metanol", new[] { "C2H5OH:5.2", "hc:467:0.05" }),                                        // 30°, metanol 1600 mg/L (GC-MS)
                ("Mẫu nước ngọt có ga", new[] { "hc:580:0.3", "Cit:0.01", "Bz:0.0012", "Na+:0.0012", "CO3:0.02" }),       // đường, axit xitric, natri benzoat (HPLC)
                ("Mẫu nước muối", new[] { "Na+:1", "Cl-:1" }),                                                          // cô cạn → tinh thể NaCl (XRD, SEM)
                ("Mẫu kết tủa trắng", new[] { "Ba2+:0.05", "SO4:0.05", "Na+:0.1", "Cl-:0.1" }),                        // BaSO₄ kết tủa (XRD, SEM-EDS)
                ("Mẫu cặn ấm đun nước", new[] { "CaCO3:0.05", "Ca2+:0.002", "CO3:0.002" }),                            // canxit (XRD)
            };
            const float rackX = -1.2f, pitchX = 0.075f;
            var rack = Group("Giá ống mẫu", prep, new Vector3(rackX + pitchX * (samples.Length - 1) / 2, top, 0.3f));
            float rackLen = pitchX * samples.Length + 0.04f;
            Box(rack, "Đế giá", new Vector3(0, 0.005f, 0), new Vector3(rackLen, 0.01f, 0.09f), oak);
            Box(rack, "Tấm đỡ có lỗ", new Vector3(0, 0.07f, 0), new Vector3(rackLen, 0.008f, 0.09f), oak, false);
            foreach (float x in new[] { -rackLen / 2, rackLen / 2 })
                Box(rack, "Thành giá", new Vector3(x, 0.04f, 0), new Vector3(0.01f, 0.08f, 0.09f), oak, false);
            for (int i = 0; i < samples.Length; i++)
            {
                float x = rackX + i * pitchX;
                var c = FunctionalVessel(prep, samples[i].name, new Vector3(x, top + 0.011f, 0.3f), "ong_nghiem");
                var pc = c.gameObject.AddComponent<PresetContents>();
                pc.volumeMl = 12f;
                pc.parts = samples[i].parts;
                c.label = samples[i].name;
                Info(c.transform, samples[i].name, "Ống mẫu thật để phân tích: đặt vào ổ trên nóc một máy rồi bấm máy.");
                Label(prep, (i + 1).ToString(), new Vector3(x, top + 0.04f, 0.3f + 0.046f), Vector3.forward, 0.022f, Color.white);
            }
            var list = new System.Text.StringBuilder();
            for (int i = 0; i < samples.Length; i++) list.Append(i + 1).Append(". ").Append(samples[i].name.Replace("Mẫu ", "")).Append(i % 2 == 1 ? "\n" : "    ");
            Label(prep, list.ToString().TrimEnd(), new Vector3(0.9f, top + 0.004f, 0.3f), Vector3.up, 0.022f, new Color(0.7f, 0.88f, 0.92f))
                .transform.localRotation = Quaternion.Euler(90, 180, 0);
            for (int i = 0; i < 4; i++) FunctionalVessel(prep, "Cuvet", new Vector3(0.4f + i * 0.06f, top, -0.3f), "cuvet");
            for (int i = 0; i < 4; i++) FunctionalVessel(prep, $"Ống nghiệm P{i + 1}", new Vector3(1.0f + i * 0.08f, top, -0.3f), "ong_nghiem");

            // Thiết bị trưng bày (có thẻ thông tin)
            // tường tây: mặt máy quay +x; tường đông: quay -x; tường hành lang: quay -z
            FloorAnalyzer(g, AnalyzerInstrument.Kind.Sem, new Vector3(0.7f, 0, -4.9f), new Vector3(1.0f, 1.6f, 1.2f), 0,
                       "Chụp ảnh bề mặt chất rắn ở độ phóng đại hàng chục nghìn lần bằng chùm electron; phổ EDS cho biết nguyên tố.");
            FloorAnalyzer(g, AnalyzerInstrument.Kind.Xrd, new Vector3(0.65f, 0, -3.2f), new Vector3(0.9f, 1.7f, 1.0f), 0,
                       "Xác định pha tinh thể (NaCl, KCl, CaCO₃, BaSO₄...) của chất rắn từ góc nhiễu xạ tia X.");
            FloorAnalyzer(g, AnalyzerInstrument.Kind.Nmr, new Vector3(12.95f, 0, -3.0f), new Vector3(0.7f, 1.1f, 0.7f), 180,
                       "Phổ ¹H-NMR: số loại nguyên tử H, môi trường của chúng; định lượng các chất trong hỗn hợp (qNMR).");
            FloorAnalyzer(g, AnalyzerInstrument.Kind.DryingOven, new Vector3(12.97f, 0, -4.0f), new Vector3(0.65f, 1.3f, 0.65f), 180,
                       "Sấy mẫu ở 105 °C đến khối lượng không đổi: độ ẩm, chất khô (phương pháp khối lượng).");
            FloorAnalyzer(g, AnalyzerInstrument.Kind.Furnace, new Vector3(13.0f, 0, -5.0f), new Vector3(0.6f, 1.0f, 0.6f), 180,
                       "Nung ở 550 °C xác định tro; 900 °C nung vôi CaCO₃ → CaO.");
            FloorAnalyzer(g, AnalyzerInstrument.Kind.Distiller, new Vector3(13.05f, 0, -6.0f), new Vector3(0.5f, 1.6f, 0.4f), 180,
                       "Cất nước hai lần, kiểm tra độ dẫn điện, xếp loại nước theo TCVN 4851.");
            FloorAnalyzer(g, AnalyzerInstrument.Kind.Autoclave, new Vector3(13.0f, 0, -7.0f), new Vector3(0.6f, 1.0f, 0.6f), 180,
                       "Tiệt trùng bằng hơi nước bão hòa 121 °C (khoảng 1,05 bar dư) trong 15 phút; kiểm tra bằng chỉ thị sinh học.");
            FloorAnalyzer(g, AnalyzerInstrument.Kind.Calorimeter, new Vector3(4.3f, 0, -2.65f), new Vector3(0.6f, 1.0f, 0.5f), 90,
                       "Đo nhiệt đốt cháy của nhiên liệu, thực phẩm (năng lượng kcal/100 g).");

            foreach (float x in new[] { 2.5f, 6.75f, 11.0f })
                foreach (float z in new[] { -7.6f, -4.0f })
                    LedPanel(g, "Đèn phòng máy", x, z, 0, 0, false);
            RoomLights(g, new[] { 3.4f, 10.1f }, new[] { -5.6f }, 9f, 3.2f);
        }

        /// <summary>Máy phân tích chạy thật: thân máy, ổ mẫu trên nóc, màn hình đồ thị và dòng kết quả.</summary>
        static void Analyzer(Transform p, AnalyzerInstrument.Kind kind, Vector3 pos, Vector3 size, string desc)
        {
            string name = AnalyzerInstrument.KindName(kind);
            var g = Group(name, p, pos);
            Box(g, "Thân máy", new Vector3(0, size.y / 2, 0), size, kind == AnalyzerInstrument.Kind.GcMs ? panelGrey : signWhite);
            Box(g, "Dải màu máy", new Vector3(0, size.y - 0.04f, size.z / 2 + 0.002f), new Vector3(size.x, 0.03f, 0.004f), accent, false);
            Box(g, "Đèn trạng thái", new Vector3(size.x / 2 - 0.05f, size.y * 0.5f, size.z / 2 + 0.003f), new Vector3(0.02f, 0.02f, 0.004f),
                Mat("DenBaoXanh", new Color(0.2f, 0.9f, 0.6f), 0.5f, 0, emission: new Color(0.3f, 1.5f, 0.8f)), false);
            var slot = Socket(g, "Ổ mẫu " + name, new Vector3(-size.x / 2 + 0.1f, size.y, 0), new[] { "ong_nghiem", "cuvet" }, false,
                              new Vector3(0, 0.07f, 0), new Vector3(0.08f, 0.14f, 0.08f), new Vector3(0, 0.002f, 0), 0.02f, socketRing);
            Cyl(g, "Giếng mẫu", new Vector3(-size.x / 2 + 0.1f, size.y + 0.006f, 0), 0.018f, 0.012f, darkMetal);
            // màn hình đặt cạnh máy, nhìn về +z (vào phòng)
            var mon = Group("Màn hình máy", g, new Vector3(size.x / 2 + 0.32f, 0, 0));
            Box(mon, "Chân màn hình", new Vector3(0, 0.1f, -0.07f), new Vector3(0.1f, 0.2f, 0.04f), blackPlastic);
            Box(mon, "Viền màn hình", new Vector3(0, 0.45f, -0.04f), new Vector3(0.58f, 0.46f, 0.025f), blackPlastic);
            var screenGo = Panel(mon, "Màn hình đồ thị", new Vector3(0, 0.535f, -0.026f), Vector3.forward, 0.54f, 0.27f, machineScreen);
            var header = Label(mon, name, new Vector3(0, 0.71f, -0.03f), Vector3.forward, 0.024f, new Color(0.05f, 0.3f, 0.36f)).GetComponent<TextMesh>();
            var result = Label(mon, "", new Vector3(0, 0.39f, -0.026f), Vector3.forward, 0.015f, new Color(0.85f, 0.95f, 1f)).GetComponent<TextMesh>();
            result.anchor = TextAnchor.UpperCenter;
            var a = g.gameObject.AddComponent<AnalyzerInstrument>();
            a.kind = kind;
            a.slot = slot;
            a.screen = screenGo.GetComponent<Renderer>();
            a.header = header;
            a.resultText = result;
            Info(g, name, desc, "Đặt ống mẫu (hoặc cuvet) vào ổ trên nóc máy, rồi bấm vào máy để chạy.");
        }

        /// <summary>Máy phân tích đứng trên sàn (XRD, SEM, NMR, bom nhiệt lượng): thân máy, kệ có ổ mẫu phía trước, màn hình nhỏ; bấm vào để mở màn hình vận hành.</summary>
        static void FloorAnalyzer(Transform p, AnalyzerInstrument.Kind kind, Vector3 pos, Vector3 size, float yaw, string desc)
        {
            string name = AnalyzerInstrument.KindName(kind);
            var g = Group(name, p, pos, yaw);
            Box(g, "Thân", new Vector3(0, size.y / 2, 0), size, signWhite);
            Box(g, "Mặt điều khiển", new Vector3(size.x / 2 + 0.004f, size.y * 0.7f, 0), new Vector3(0.008f, size.y * 0.25f, size.z * 0.6f), blackPlastic, false);
            var scr = Box(g, "Màn hình máy", new Vector3(size.x / 2 + 0.009f, size.y * 0.72f, 0), new Vector3(0.004f, size.y * 0.18f, size.z * 0.45f), machineScreen, false);
            NoShadow(scr);
            Box(g, "Dải màu", new Vector3(size.x / 2 + 0.006f, 0.12f, 0), new Vector3(0.006f, 0.04f, size.z), accent, false);
            var header = Label(g, name, new Vector3(size.x / 2 + 0.012f, size.y + 0.08f, 0), Vector3.right, 0.035f, new Color(0.05f, 0.3f, 0.36f)).GetComponent<TextMesh>();
            // kệ nhỏ phía trước có ổ đặt ống mẫu
            float ly = Mathf.Min(0.9f, size.y * 0.55f);
            Box(g, "Kệ mẫu", new Vector3(size.x / 2 + 0.16f, ly - 0.02f, size.z * 0.3f), new Vector3(0.3f, 0.04f, 0.3f), epoxy);
            Box(g, "Chân kệ mẫu", new Vector3(size.x / 2 + 0.16f, (ly - 0.04f) / 2, size.z * 0.3f), new Vector3(0.04f, ly - 0.04f, 0.04f), darkMetal);
            var slot = Socket(g, "Ổ mẫu " + name, new Vector3(size.x / 2 + 0.16f, ly, size.z * 0.3f), new[] { "ong_nghiem", "cuvet" }, false,
                              new Vector3(0, 0.07f, 0), new Vector3(0.08f, 0.14f, 0.08f), new Vector3(0, 0.002f, 0), 0.02f, socketRing);
            var result = Label(g, "", new Vector3(size.x / 2 + 0.012f, size.y * 0.58f, 0), Vector3.right, 0.012f, new Color(0.85f, 0.95f, 1f)).GetComponent<TextMesh>();
            result.anchor = TextAnchor.UpperCenter;
            var a = g.gameObject.AddComponent<AnalyzerInstrument>();
            a.kind = kind;
            a.slot = slot;
            a.screen = scr.GetComponent<Renderer>();
            a.header = header;
            a.resultText = result;
            Info(g, name, desc, "Đặt ống mẫu vào ổ trên kệ trước máy (hoặc dùng mẫu chuẩn có sẵn), rồi bấm vào máy để mở màn hình vận hành.");
        }

        static void BigMachine(Transform p, string name, Vector3 pos, Vector3 size, float yaw, string desc)
        {
            var g = Group(name, p, pos, yaw);
            Box(g, "Thân", new Vector3(0, size.y / 2, 0), size, signWhite);
            Box(g, "Mặt điều khiển", new Vector3(size.x / 2 + 0.004f, size.y * 0.7f, 0), new Vector3(0.008f, size.y * 0.25f, size.z * 0.6f), blackPlastic, false);
            NoShadow(Box(g, "Màn hình nhỏ", new Vector3(size.x / 2 + 0.009f, size.y * 0.72f, 0), new Vector3(0.004f, size.y * 0.18f, size.z * 0.45f), screen, false));
            Box(g, "Dải màu", new Vector3(size.x / 2 + 0.006f, 0.12f, 0), new Vector3(0.006f, 0.04f, size.z), accent, false);
            Label(g, name, new Vector3(size.x / 2 + 0.012f, size.y + 0.08f, 0), Vector3.right, 0.035f, new Color(0.05f, 0.3f, 0.36f));
            Info(g, name, desc);
        }

        // =====================================================================
        // Phòng tự động hóa & robot (x 13,5..27)
        // =====================================================================

        public static readonly Vector3 RobotDock = new Vector3(24.6f, 0, -3.3f);

        static void RoboticsRoom(Transform p)
        {
            var g = Group("Phòng tự động hóa & robot", p);
            // Trạm sạc robot trợ lý
            var dock = Group("Trạm sạc robot trợ lý", g, RobotDock);
            Cyl(dock, "Đế sạc", new Vector3(0, 0.02f, 0), 0.55f, 0.04f, panelGrey, true);
            NoShadow(MeshObj(dock, "Vòng sáng sạc", Lathe("VongSac", 40, V(0.46f, 0.041f), V(0.5f, 0.041f), V(0.5f, 0.046f), V(0.46f, 0.046f), V(0.46f, 0.041f)), Vector3.zero,
                             Mat("SangSac", new Color(0.3f, 0.9f, 1f), 0.5f, 0, emission: new Color(0.6f, 1.8f, 2.4f))));
            Box(dock, "Cột sạc", new Vector3(0, 0.75f, 0.62f), new Vector3(0.3f, 1.5f, 0.12f), whitePlastic);
            Label(dock, "TRẠM SẠC ROBOT", new Vector3(0, 1.3f, 0.555f), Vector3.back, 0.04f, new Color(0.05f, 0.3f, 0.36f));
            Info(dock, "Trạm sạc robot trợ lý", "Robot hình người về đây sạc pin khi rảnh. Nhấn T để gọi robot.");

            // Cánh tay robot cộng tác trên bàn làm việc
            var cell = Group("Cánh tay robot cộng tác", g, new Vector3(16.6f, 0, -7.6f));
            Box(cell, "Bàn đặt robot", new Vector3(0, 0.42f, 0), new Vector3(1.6f, 0.84f, 1.0f), panelGrey);
            Box(cell, "Mặt bàn", new Vector3(0, 0.86f, 0), new Vector3(1.6f, 0.04f, 1.0f), epoxy);
            var baseT = Group("Đế quay", cell, new Vector3(-0.3f, 0.88f, 0));
            Cyl(baseT, "Đế", new Vector3(0, 0.05f, 0), 0.09f, 0.1f, whitePlastic);
            var sh = Group("Khớp vai", baseT, new Vector3(0, 0.12f, 0));
            Box(sh, "Cánh tay trên", new Vector3(0, 0.18f, 0), new Vector3(0.08f, 0.36f, 0.08f), whitePlastic, false);
            var el = Group("Khớp khuỷu", sh, new Vector3(0, 0.36f, 0));
            Box(el, "Cánh tay dưới", new Vector3(0, 0.15f, 0), new Vector3(0.065f, 0.3f, 0.065f), whitePlastic, false);
            var wr = Group("Cổ tay", el, new Vector3(0, 0.3f, 0));
            Box(wr, "Kẹp", new Vector3(0, 0.05f, 0), new Vector3(0.09f, 0.08f, 0.04f), accentDark, false);
            var cob = cell.gameObject.AddComponent<CobotDemo>();
            cob.baseYaw = baseT; cob.shoulder = sh; cob.elbow = el; cob.wrist = wr;
            for (int i = 0; i < 3; i++) NoShadow(MeshObj(cell, "Cốc mẫu", Beaker("Coc100", 0.025f, 0.07f), new Vector3(0.2f + i * 0.15f, 0.88f, 0.2f), labGlass));
            Info(cell, "Cánh tay robot cộng tác (cobot)", "Robot 6 trục làm việc cạnh người: gắp ống mẫu, rót, chuyển mẫu vào máy phân tích theo chương trình.",
                 "Có cảm biến lực, tự dừng khi chạm người. Lập trình bằng cách kéo tay dạy điểm.");

            // Máy hút nhả tự động, máy in 3D
            var lh = Group("Máy hút nhả tự động", g, new Vector3(19.6f, 0, WingZ + 0.6f));
            Box(lh, "Bàn", new Vector3(0, 0.42f, 0), new Vector3(1.4f, 0.84f, 0.8f), cabinet);
            NoShadow(Box(lh, "Vỏ kính", new Vector3(0, 1.18f, 0), new Vector3(1.2f, 0.6f, 0.7f), cabinetGlass, false));
            Box(lh, "Ray đầu hút", new Vector3(0, 1.4f, 0), new Vector3(1.15f, 0.04f, 0.06f), darkMetal, false);
            Box(lh, "Đầu hút 8 kênh", new Vector3(0.1f, 1.28f, 0), new Vector3(0.1f, 0.2f, 0.12f), whitePlastic, false);
            for (int i = 0; i < 4; i++) Box(lh, "Đĩa 96 giếng", new Vector3(-0.42f + i * 0.28f, 0.9f, 0), new Vector3(0.13f, 0.02f, 0.09f), Mat("DiaGieng", new Color(0.85f, 0.9f, 0.95f, 0.8f), 0.6f, 0, transparent: true), false);
            Info(lh, "Máy hút nhả tự động (liquid handler)", "Pha loãng, chia mẫu vào hàng trăm giếng chính xác tới microlít, chạy suốt đêm không cần người.");
            var pr = Group("Máy in 3D", g, new Vector3(22.4f, 0, WingZ + 0.6f));
            Box(pr, "Bàn", new Vector3(0, 0.42f, 0), new Vector3(1.0f, 0.84f, 0.8f), cabinet);
            Box(pr, "Khung máy in", new Vector3(0, 1.12f, 0), new Vector3(0.5f, 0.5f, 0.5f), darkMetal, false);
            NoShadow(Box(pr, "Cửa kính máy in", new Vector3(0, 1.12f, 0.252f), new Vector3(0.46f, 0.46f, 0.004f), cabinetGlass, false));
            Box(pr, "Giá đỡ ống in", new Vector3(0, 0.92f, 0), new Vector3(0.3f, 0.04f, 0.3f), Mat("NhuaIn", new Color(0.1f, 0.6f, 0.62f), 0.4f), false);
            Info(pr, "Máy in 3D", "In giá ống nghiệm, mô hình phân tử, chi tiết robot bằng nhựa PLA.");

            // Góc kính thực tế ảo
            for (int i = 0; i < 2; i++)
            {
                var vr = Group("Góc kính thực tế ảo", g, new Vector3(26.3f, 0, -7.9f + i * 1.7f), -90);
                Box(vr, "Bàn", new Vector3(0, 0.72f, 0), new Vector3(1.2f, 0.04f, 0.6f), oak);
                foreach (float x in new[] { -0.55f, 0.55f }) Box(vr, "Chân bàn", new Vector3(x, 0.36f, 0), new Vector3(0.04f, 0.72f, 0.55f), darkMetal, false);
                Box(vr, "Kính VR", new Vector3(-0.2f, 0.78f, 0), new Vector3(0.18f, 0.09f, 0.1f), blackPlastic);
                foreach (float x in new[] { 0.1f, 0.25f }) Box(vr, "Tay cầm", new Vector3(x, 0.76f, 0.05f), new Vector3(0.04f, 0.03f, 0.12f), blackPlastic, false);
                Box(vr, "Màn hình", new Vector3(0.2f, 1.05f, -0.2f), new Vector3(0.6f, 0.36f, 0.03f), blackPlastic);
                NoShadow(Box(vr, "Mặt màn hình", new Vector3(0.2f, 1.05f, -0.184f), new Vector3(0.56f, 0.32f, 0.003f), screen, false));
                Stool(vr, "Ghế", new Vector3(0, 0, 0.55f));
                Info(vr, "Góc kính thực tế ảo", "Kính VR và tay cầm tự chế của dự án: vào phòng lab ảo bằng cử động tay thật.");
            }

            // Tủ máy chủ AI
            var rack = Group("Tủ máy chủ AI", g, new Vector3(26.45f, 0, -4.4f), -90);
            Box(rack, "Thân tủ", new Vector3(0, 1.0f, 0), new Vector3(0.65f, 2.0f, 0.8f), Mat("TuMayChu", new Color(0.12f, 0.13f, 0.15f), 0.5f, 0.4f));
            var leds = new System.Collections.Generic.List<Renderer>();
            for (int i = 0; i < 10; i++)
            {
                Box(rack, "Máy chủ", new Vector3(0, 0.3f + i * 0.15f, 0.402f), new Vector3(0.58f, 0.12f, 0.004f), darkMetal, false);
                for (int k = 0; k < 4; k++)
                    leds.Add(Box(rack, "Đèn máy chủ", new Vector3(-0.22f + k * 0.04f, 0.3f + i * 0.15f, 0.406f), new Vector3(0.015f, 0.015f, 0.003f),
                                 Mat("DenMayChu", new Color(0.1f, 0.8f, 0.4f), 0.5f, 0, emission: new Color(0.3f, 2.2f, 1.2f)), false).GetComponent<Renderer>());
            }
            var blink = rack.gameObject.AddComponent<CobotDemo>();
            blink.blinkLights = leds.ToArray();
            Info(rack, "Tủ máy chủ AI", "Chạy máy chủ AI và web quản lý: mạng lưới khóa API Groq, DeepSeek, Gemini, Claude.");

            // Màn hình lớn trên tường ngăn: web quản lý
            var wall = Group("Màn hình web quản lý", g, new Vector3(RoomSplitX + 0.14f, 1.6f, -5.6f), 90);
            Box(wall, "Viền", Vector3.zero, new Vector3(2.4f, 1.4f, 0.06f), blackPlastic);
            NoShadow(Box(wall, "Mặt màn hình", new Vector3(0, 0, 0.031f), new Vector3(2.3f, 1.3f, 0.004f), screen, false));
            Label(wall, "WEB QUẢN LÝ PHÒNG THÍ NGHIỆM", new Vector3(0, 0.45f, 0.036f), Vector3.forward, 0.08f, new Color(0.85f, 0.95f, 1f));
            Label(wall, "http://127.0.0.1:8000", new Vector3(0, 0.25f, 0.036f), Vector3.forward, 0.07f, new Color(0.45f, 0.95f, 1f));
            Label(wall, "Tồn kho 720 hóa chất · 80 thiết bị · nhật ký sự cố\nMạng lưới khóa AI: Groq · DeepSeek · Gemini · Claude\nRobot nhận kế hoạch từ AI theo ảnh chụp cảnh",
                  new Vector3(0, -0.15f, 0.036f), Vector3.forward, 0.04f, new Color(0.7f, 0.85f, 0.95f));
            Info(wall, "Màn hình web quản lý", "Chạy máy chủ (uvicorn server.ai.main:app) rồi mở http://127.0.0.1:8000 trên trình duyệt.");

            // Bàn họp nhóm
            var meet = Group("Bàn họp nhóm", g, new Vector3(20.2f, 0, -5.4f));
            Box(meet, "Mặt bàn", new Vector3(0, 0.74f, 0), new Vector3(2.4f, 0.04f, 1.1f), oak);
            foreach (float x in new[] { -1.0f, 1.0f }) Box(meet, "Chân bàn", new Vector3(x, 0.37f, 0), new Vector3(0.06f, 0.74f, 0.9f), darkMetal);
            for (int i = 0; i < 3; i++)
            {
                Stool(meet, "Ghế", new Vector3(-0.8f + i * 0.8f, 0, 0.8f));
                Stool(meet, "Ghế", new Vector3(-0.8f + i * 0.8f, 0, -0.8f));
            }
            meet.Find("Mặt bàn").gameObject.AddComponent<PlaceSurface>();

            foreach (float x in new[] { 16.5f, 20.5f, 24.5f })
                foreach (float z in new[] { -7.6f, -4.0f })
                    LedPanel(g, "Đèn phòng robot", x, z, 0, 0, false);
            RoomLights(g, new[] { 17.0f, 23.5f }, new[] { -5.6f }, 9f, 3.2f);
        }

        // =====================================================================
        // Robot trợ lý hình người
        // =====================================================================

        static LabRobot BuildHumanoid(Transform root, Vector3 pos)
        {
            var g = Group("Robot trợ lý", root, pos);
            var white = Mat("RobotTrang", new Color(0.95f, 0.96f, 0.97f), 0.85f, 0.1f);
            var joint = Mat("RobotKhop", new Color(0.18f, 0.2f, 0.24f), 0.6f, 0.5f);
            var visorMat = Mat("RobotKinh", new Color(0.04f, 0.06f, 0.1f), 0.95f, 0.2f);
            var eyeMat = Mat("RobotMat", new Color(0.35f, 0.9f, 1f), 0.5f, 0, emission: new Color(0.7f, 1.8f, 2.2f));

            var pelvis = Group("Hông", g, new Vector3(0, 0.86f, 0));
            Box(pelvis, "Khung hông", Vector3.zero, new Vector3(0.3f, 0.12f, 0.18f), joint);
            var torso = Group("Thân trên", pelvis, new Vector3(0, 0.06f, 0));
            Ball(torso, "Bụng", new Vector3(0, 0.12f, 0), new Vector3(0.26f, 0.22f, 0.2f), joint);
            Ball(torso, "Ngực", new Vector3(0, 0.36f, 0), new Vector3(0.42f, 0.42f, 0.27f), white);
            var chestLight = MeshObj(torso, "Đèn ngực", Lathe("DenNguc", 24, V(0, 0), V(0.05f, 0), V(0.05f, 0.012f), V(0, 0.012f)), new Vector3(0, 0.4f, 0.13f), eyeMat, false, new Vector3(90, 0, 0));
            var head = Group("Đầu", torso, new Vector3(0, 0.62f, 0));
            Cyl(head, "Cổ", new Vector3(0, -0.05f, 0), 0.04f, 0.08f, joint);
            Ball(head, "Đầu robot", new Vector3(0, 0.1f, 0), new Vector3(0.24f, 0.24f, 0.24f), white);
            Ball(head, "Mặt kính", new Vector3(0, 0.1f, 0.05f), new Vector3(0.19f, 0.13f, 0.16f), visorMat);
            var eyeL = Ball(head, "Mắt trái", new Vector3(-0.04f, 0.11f, 0.131f), new Vector3(0.03f, 0.036f, 0.01f), eyeMat);
            var eyeR = Ball(head, "Mắt phải", new Vector3(0.04f, 0.11f, 0.131f), new Vector3(0.03f, 0.036f, 0.01f), eyeMat);
            Cyl(head, "Ăng ten", new Vector3(0, 0.24f, 0), 0.004f, 0.06f, white);
            Ball(head, "Đầu ăng ten", new Vector3(0, 0.28f, 0), new Vector3(0.02f, 0.02f, 0.02f), eyeMat);

            Transform Arm(int side, out Transform elbow, out Transform hand)
            {
                var sh = Group(side > 0 ? "Vai phải" : "Vai trái", torso, new Vector3(0.24f * side, 0.5f, 0));
                Ball(sh, "Khớp vai", Vector3.zero, new Vector3(0.1f, 0.1f, 0.1f), joint);
                Cyl(sh, "Cánh tay trên", new Vector3(0, -0.185f, 0), 0.04f, 0.3f, white);
                elbow = Group("Khuỷu", sh, new Vector3(0, -0.37f, 0));
                Ball(elbow, "Khớp khuỷu", Vector3.zero, new Vector3(0.075f, 0.075f, 0.075f), joint);
                Cyl(elbow, "Cẳng tay", new Vector3(0, -0.18f, 0), 0.033f, 0.3f, white);
                hand = Group("Bàn tay", elbow, new Vector3(0, -0.36f, 0));
                Box(hand, "Lòng bàn tay", new Vector3(0, -0.04f, 0), new Vector3(0.06f, 0.08f, 0.03f), joint);
                foreach (float f in new[] { -0.02f, 0f, 0.02f }) Box(hand, "Ngón", new Vector3(f, -0.095f, 0.012f), new Vector3(0.012f, 0.04f, 0.012f), joint, false);
                Box(hand, "Ngón cái", new Vector3(-0.035f * side, -0.06f, 0.02f), new Vector3(0.012f, 0.035f, 0.012f), joint, false);
                return sh;
            }
            var shR = Arm(+1, out var elR, out var hR);
            var shL = Arm(-1, out var elL, out var hL);
            var carry = Child(hR, "Điểm cầm", new Vector3(0, -0.05f, 0.035f));

            Transform Leg(int side, out Transform knee)
            {
                var hip = Group(side > 0 ? "Hông phải" : "Hông trái", pelvis, new Vector3(0.1f * side, -0.04f, 0));
                Ball(hip, "Khớp hông", Vector3.zero, new Vector3(0.1f, 0.1f, 0.1f), joint);
                Cyl(hip, "Đùi", new Vector3(0, -0.21f, 0), 0.055f, 0.36f, white);
                knee = Group("Gối", hip, new Vector3(0, -0.41f, 0));
                Ball(knee, "Khớp gối", Vector3.zero, new Vector3(0.085f, 0.085f, 0.085f), joint);
                Cyl(knee, "Cẳng chân", new Vector3(0, -0.2f, 0), 0.045f, 0.34f, white);
                Box(knee, "Bàn chân", new Vector3(0, -0.385f, 0.04f), new Vector3(0.1f, 0.05f, 0.22f), joint, false);
                return hip;
            }
            var hipR = Leg(+1, out var kR);
            var hipL = Leg(-1, out var kL);

            foreach (var r in g.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(r);
            var trigger = g.gameObject.AddComponent<CapsuleCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0, 0.9f, 0);
            trigger.height = 1.8f;
            trigger.radius = 0.25f;

            var bot = g.gameObject.AddComponent<LabRobot>();
            bot.pelvis = pelvis; bot.torso = torso; bot.head = head;
            bot.shoulderL = shL; bot.shoulderR = shR; bot.elbowL = elL; bot.elbowR = elR; bot.handL = hL; bot.handR = hR;
            bot.hipL = hipL; bot.hipR = hipR; bot.kneeL = kL; bot.kneeR = kR;
            bot.carryPoint = carry;
            bot.upperArm = 0.37f; bot.foreArm = 0.36f; bot.thigh = 0.41f; bot.shin = 0.41f;
            bot.eyes = new[] { eyeL.GetComponent<Renderer>(), eyeR.GetComponent<Renderer>() };
            bot.ring = chestLight.GetComponent<Renderer>();
            var info = g.gameObject.AddComponent<EquipmentInfo>();
            info.title = "Robot trợ lý AI";
            info.description = "Robot hình người: đi lấy hóa chất, rót, nhỏ giọt, minh họa thí nghiệm, xử lý sự cố. Nhấn T để ra lệnh.";
            return bot;
        }
    }
}
