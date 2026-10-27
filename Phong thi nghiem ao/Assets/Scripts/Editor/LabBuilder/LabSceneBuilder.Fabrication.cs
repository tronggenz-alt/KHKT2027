using System.Collections.Generic;
using Khkt.Assembly;
using Khkt.Demo;
using Khkt.Environment;
using Khkt.Rendering;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using static Khkt.EditorTools.LabKit;

namespace Khkt.EditorTools
{
    /// <summary>
    /// Khu chế tạo dụng cụ: ngăn kính trong khu "Lắp ráp & tổng hợp" của phòng lab có bàn chế tạo (bấm vào mở giao diện lắp
    /// ráp 3D), máy gia công, bàn nhận dụng cụ; kho bộ phận mẫu (bản mẫu tắt sẵn để nhân bản); xưởng lắp ráp ảo đặt xa tòa
    /// nhà có camera riêng, bàn ảo và vòi nước giả để thử nối ống.
    /// </summary>
    public static partial class LabSceneBuilder
    {
        /// <summary>Ngăn kính chế tạo: góc đông bắc phòng lab, giữa bàn thí nghiệm lắp ráp và tường đông.</summary>
        static readonly Rect BoothArea = Rect.MinMaxRect(17.1f, 10.45f, 19.9f, 12.85f);
        public static readonly Vector3 FabricationStationPos = new Vector3(18.55f, 0, 11.65f);
        public const string PartLibraryName = "Kho bộ phận mẫu";
        public const string WorkshopName = "Xưởng lắp ráp ảo";
        static readonly Vector3 WorkshopPos = new Vector3(-60f, 200f, -40f);  // ngoài mọi khu (ZoneMap.At = ngoài trời), cao trên mọi vật

        // =====================================================================
        // Ngăn kính trong phòng lab
        // =====================================================================

        static void FabricationBooth(Transform lab)
        {
            var g = Group("Khu chế tạo dụng cụ", lab);
            float x0 = BoothArea.xMin, x1 = BoothArea.xMax, z0 = BoothArea.yMin, z1 = BoothArea.yMax;
            const float h = 2.3f, t = 0.04f, doorA = 18.0f, doorB = 19.0f;
            var post = doorFrame;
            var band = filmBand;

            Box(g, "Sàn khu chế tạo", new Vector3((x0 + x1) / 2, 0.003f, (z0 + z1) / 2), new Vector3(x1 - x0, 0.004f, z1 - z0),
                Mat("SanKhuCheTao", Color.Lerp(zoneLab, Color.white, 0.72f), 0.35f), false);

            // vách kính mờ: tấm kính + trụ nhôm + dải phim màu khu + thanh trên
            void Wall(string name, Vector3 a, Vector3 b)
            {
                Vector3 d = b - a;
                float len = d.magnitude;
                if (len < 0.05f) return;
                var w = Group(name, g, (a + b) / 2, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg - 90f);
                Box(w, "Kính mờ", new Vector3(0, h / 2, 0), new Vector3(len, h, t), frostedGlass);
                Box(w, "Dải phim màu", new Vector3(0, 1.05f, 0), new Vector3(len, 0.12f, t + 0.006f), band, false);
                Box(w, "Thanh trên", new Vector3(0, h + 0.02f, 0), new Vector3(len + 0.04f, 0.05f, t + 0.03f), post, false);
                int n = Mathf.Max(1, Mathf.RoundToInt(len / 1.2f));
                for (int i = 0; i <= n; i++)
                    Box(w, "Trụ nhôm", new Vector3(-len / 2 + len * i / n, h / 2, 0), new Vector3(0.05f, h, t + 0.03f), post, false);
            }
            Wall("Vách nam (trái cửa)", new Vector3(x0, 0, z0), new Vector3(doorA, 0, z0));
            Wall("Vách nam (phải cửa)", new Vector3(doorB, 0, z0), new Vector3(x1, 0, z0));
            Wall("Vách tây", new Vector3(x0, 0, z0), new Vector3(x0, 0, z1));
            Wall("Vách bắc", new Vector3(x0, 0, z1), new Vector3(x1, 0, z1));
            // khung cửa + biển trên cửa
            var frame = Group("Khung cửa khu chế tạo", g, new Vector3((doorA + doorB) / 2, 0, z0));
            Box(frame, "Đầu cửa", new Vector3(0, 2.15f, 0), new Vector3(doorB - doorA, 0.3f, t + 0.03f), post, false);
            Box(frame, "Biển khu", new Vector3(0, 2.6f, -0.03f), new Vector3(1.9f, 0.34f, 0.02f), Mat("BienKhuCheTao", zoneLab, 0.5f), false);
            Label(frame, "KHU CHẾ TẠO DỤNG CỤ", new Vector3(0, 2.64f, -0.042f), Vector3.back, 0.075f, Color.white);
            Label(frame, "FABRICATION", new Vector3(0, 2.52f, -0.042f), Vector3.back, 0.04f, new Color(0.85f, 0.95f, 0.95f));
            SlidingDoor(g, new Vector3((doorA + doorB) / 2, 0, z0), doorB - doorA, 2.0f);

            // bàn chế tạo dọc tường đông: màn hình lớn, bàn phím, chuột
            var desk = Group("Bàn chế tạo", g, new Vector3(19.45f, 0, 11.65f), 90f);  // trục z cục bộ chỉ về tường đông: người ngồi phía tây nhìn sang
            Box(desk, "Mặt làm việc chế tạo", new Vector3(0, 0.74f, 0), new Vector3(1.9f, 0.04f, 0.8f), Mat("MatBanCheTao", new Color(0.93f, 0.94f, 0.94f), 0.4f));
            Box(desk, "Tủ bàn chế tạo", new Vector3(0.55f, 0.37f, 0.05f), new Vector3(0.7f, 0.7f, 0.66f), cabinet);
            foreach (float x in new[] { -0.9f, 0.9f }) Box(desk, "Chân bàn", new Vector3(x, 0.36f, 0), new Vector3(0.05f, 0.72f, 0.74f), oak);
            var monitor = Group("Màn hình chế tạo", desk, new Vector3(0, 0.76f, 0.22f));
            Box(monitor, "Chân màn hình", new Vector3(0, 0.12f, 0.05f), new Vector3(0.08f, 0.24f, 0.06f), darkMetal, false);
            Box(monitor, "Vỏ màn hình", new Vector3(0, 0.42f, 0), new Vector3(1.0f, 0.58f, 0.04f), blackPlastic);
            var face = Box(monitor, "Mặt màn hình", new Vector3(0, 0.42f, -0.021f), new Vector3(0.96f, 0.54f, 0.002f),
                           Mat("ManHinhCheTao", new Color(0.05f, 0.2f, 0.24f), 0.8f, 0, emission: new Color(0.05f, 0.35f, 0.42f)), false);
            NoShadow(face);
            Label(monitor, "CHẾ TẠO DỤNG CỤ", new Vector3(0, 0.55f, -0.024f), Vector3.back, 0.06f, Color.white);
            Label(monitor, "Bấm vào để mở giao diện lắp ráp 3D", new Vector3(0, 0.44f, -0.024f), Vector3.back, 0.028f, new Color(0.75f, 0.95f, 1f));
            Label(monitor, "Lắp xong bấm GIA CÔNG: bộ dụng cụ\nra ngay bàn thí nghiệm bạn chọn", new Vector3(0, 0.3f, -0.024f), Vector3.back, 0.022f, new Color(0.6f, 0.85f, 0.9f));
            Box(desk, "Bàn phím", new Vector3(0, 0.765f, -0.12f), new Vector3(0.44f, 0.015f, 0.14f), blackPlastic, false);
            Box(desk, "Chuột", new Vector3(0.32f, 0.765f, -0.12f), new Vector3(0.06f, 0.02f, 0.1f), blackPlastic, false);
            desk.gameObject.AddComponent<FabricationStation>();
            Info(desk, "Bàn chế tạo dụng cụ",
                 "Bấm vào để mở giao diện lắp ráp 3D: lấy bộ phận theo nhóm, lắp bằng khớp nối chuẩn, kiểm tra hệ kín / hở và độ vững. "
                 + "Lắp xong bấm Gia công: bộ dụng cụ xuất hiện ở bàn thí nghiệm bạn chọn.",
                 "Một bộ dụng cụ cho một thí nghiệm: chưng cất, đun hồi lưu, điều chế – thu khí, nhiệt phân, lọc, chiết, cô cạn...");
            Stool(g, new Vector3(18.85f, 0, 11.65f));

            // máy gia công (góc tây bắc) và bàn nhận dụng cụ (dọc vách tây)
            var mc = Group("Máy gia công", g, new Vector3(17.6f, 0, 12.42f));
            Box(mc, "Thân máy", new Vector3(0, 0.8f, 0), new Vector3(0.8f, 1.6f, 0.62f), Mat("ThanMayGiaCong", new Color(0.88f, 0.9f, 0.92f), 0.5f, 0.3f));
            Box(mc, "Cửa sổ máy", new Vector3(0, 1.0f, -0.312f), new Vector3(0.6f, 0.5f, 0.01f), darkGlass, false);
            NoShadow(Box(mc, "Đèn trạng thái máy", new Vector3(0, 1.38f, -0.315f), new Vector3(0.6f, 0.03f, 0.006f), ledBlue, false));
            Label(mc, "MÁY GIA CÔNG", new Vector3(0, 1.5f, -0.316f), Vector3.back, 0.05f, new Color(0.05f, 0.3f, 0.36f));
            Info(mc, "Máy gia công dụng cụ", "Nhận thiết kế từ bàn chế tạo, dựng bộ dụng cụ và đưa ra bàn thí nghiệm đã chọn.");
            var pick = Group("Bàn nhận dụng cụ", g, new Vector3(17.5f, 0, 11.25f));
            Box(pick, "Mặt bàn", new Vector3(0, 0.88f, 0), new Vector3(0.7f, 0.04f, 1.2f), epoxy);
            Box(pick, "Tủ bàn nhận", new Vector3(0, 0.43f, 0), new Vector3(0.66f, 0.86f, 1.16f), cabinet);
            Info(pick, "Bàn nhận dụng cụ", "Bộ dụng cụ gia công xong mặc định đặt ở đây. Cầm cả bộ (cầm giá sắt hoặc bất kỳ bộ phận nào) để mang đi.");
            Destination(pick, "Bàn nhận dụng cụ (khu chế tạo)", 1, new Vector3(0, 0.9f, 0), new Vector2(0.64f, 1.14f), Vector3.right, true);
        }

        /// <summary>
        /// Bàn đích của khu chế tạo: nút rỗng con của bàn (chỉ nút này ở lại cảnh lõi khi tách khu), gốc ở tâm vùng đặt trên mặt bàn,
        /// size theo x, z cục bộ của bàn, front là hướng người đứng làm thí nghiệm.
        /// </summary>
        static void Destination(Transform bench, string label, int order, Vector3 localCenter, Vector2 size, Vector3 front, bool isDefault = false)
        {
            var g = Group("Đích gia công", bench, localCenter);
            var d = g.gameObject.AddComponent<FabricationDestination>();
            d.label = label;
            d.order = order;
            d.size = size;
            d.front = front;
            d.isDefault = isDefault;
        }

        /// <summary>Cửa trượt hai cánh kính mờ (tự mở khi tới gần, không tải / gỡ khu).</summary>
        static void SlidingDoor(Transform p, Vector3 pos, float width, float height)
        {
            var door = Group("Cửa trượt khu chế tạo", p, pos);
            var leaves = new Transform[2];
            var slide = new Vector3[2];
            for (int i = 0; i < 2; i++)
            {
                int s = i == 0 ? -1 : 1;
                var leaf = Group(i == 0 ? "Cánh trái" : "Cánh phải", door, new Vector3(s * width / 4, 0, -0.04f));
                Box(leaf, "Kính cánh", new Vector3(0, height / 2, 0), new Vector3(width / 2 - 0.01f, height, 0.03f), frostedGlass);
                Box(leaf, "Dải phim", new Vector3(0, 1.05f, -0.018f), new Vector3(width / 2 - 0.01f, 0.12f, 0.004f), filmBand, false);
                Box(leaf, "Khung cánh", new Vector3(-s * (width / 4 - 0.015f), height / 2, 0), new Vector3(0.03f, height, 0.035f), doorFrame, false);
                leaves[i] = leaf;
                slide[i] = new Vector3(s * (width / 2 - 0.05f), 0, 0);
            }
            var led = Box(door, "Đèn trạng thái cửa", new Vector3(0, height + 0.06f, -0.06f), new Vector3(width * 0.6f, 0.02f, 0.004f), ledBlue, false);
            NoShadow(led);
            var ad = door.gameObject.AddComponent<AutoDoor>();
            ad.doorName = "Cửa khu chế tạo";
            ad.leaves = leaves;
            ad.slide = slide;
            ad.width = width;
            ad.zoneA = ZoneMap.Lab;
            ad.zoneB = ZoneMap.Lab;
            ad.sensorRadius = 1.3f;
            ad.statusLights = new[] { led.GetComponent<Renderer>() };
            Info(door, "Cửa khu chế tạo", "Cửa trượt tự động: tới gần là mở.");
        }

        static void Stool(Transform p, Vector3 pos)
        {
            var s = Group("Ghế khu chế tạo", p, pos);
            Cyl(s, "Chân ghế", new Vector3(0, 0.25f, 0), 0.025f, 0.5f, steel);
            Cyl(s, "Đế ghế", new Vector3(0, 0.02f, 0), 0.2f, 0.03f, darkMetal);
            Cyl(s, "Mặt ghế", new Vector3(0, 0.52f, 0), 0.19f, 0.05f, stoolMats[0], true);
        }

        // =====================================================================
        // Kho bộ phận mẫu
        // =====================================================================

        /// <summary>Tạo bản mẫu cho mọi loại bộ phận (đang bật để trình dựng cảnh xử lý như vật thường; tắt ở cuối Build).</summary>
        static PartLibrary PartTemplates(Transform root)
        {
            var g = Group(PartLibraryName, root, WorkshopPos + new Vector3(0, -10f, 0));
            var lib = g.gameObject.AddComponent<PartLibrary>();
            var list = new List<PartLibrary.Entry>();
            var z = Vector3.zero;
            string G(int i) => PartLibrary.Groups[i];
            void Add(string id, string name, int group, string hint, System.Action<Transform> build)
            {
                var w = Group("Mẫu: " + name, g, new Vector3(list.Count % 8 * 0.6f, 0, list.Count / 8 * 0.6f));
                build(w);
                int sub = 0;
                foreach (Transform c in w)
                {
                    var ap = c.GetComponent<ApparatusPart>();
                    if (ap == null) continue;
                    ap.libraryId = id;
                    ap.librarySub = sub++;
                }
                list.Add(new PartLibrary.Entry { id = id, name = name, group = G(group), hint = hint, template = w.gameObject });
            }

            Add("gia_sat", "Giá sắt", 0, "Đế gang, cọc 70 cm", w => StandPart(w, "Giá sắt", z));
            Add("kep", "Kẹp càng cua", 0, "Kẹp cổ bình, thân ống", w => ClampPart(w, "Kẹp càng cua", z));
            Add("vong_sat", "Vòng sắt", 0, "Đỡ phễu, lưới", w => RingPart(w, "Vòng sắt", z));
            Add("kieng", "Kiềng ba chân", 0, "Đặt lưới, đun cốc", w => TripodPart(w, "Kiềng ba chân", z));
            Add("luoi", "Lưới tản nhiệt", 0, "Đặt lên kiềng / vòng", w => GauzePart(w, "Lưới tản nhiệt", z));
            Add("gia_ong_nghiem", "Giá ống nghiệm", 0, "6 lỗ", w => TestTubeRackPart(w, "Giá ống nghiệm", z));
            Add("den_con", "Đèn cồn", 0, "Nguồn nhiệt", w => LampPart(w, "Đèn cồn", z));

            Add("binh_cau", "Bình cầu 250 ml", 1, "Cổ nhám 24/29", w => RoundFlaskPart(w, "Bình cầu 250 ml", z));
            Add("binh_cau_nhanh", "Bình cầu có nhánh", 1, "Điều chế khí", w => SideArmFlaskPart(w, "Bình cầu có nhánh 250 ml", z));
            Add("binh_non", "Bình nón 250 ml", 1, "Hứng, phản ứng", w => ConicalFlaskPart(w, "Bình nón 250 ml", z));
            Add("coc250", "Cốc 250 ml", 1, "Hứng, đun", w => BeakerPart(w, "Cốc thủy tinh 250 ml", z, "coc250"));
            Add("coc100", "Cốc 100 ml", 1, "Hứng, đun", w => BeakerPart(w, "Cốc thủy tinh 100 ml", z, "coc100"));
            Add("ong_nghiem", "Ống nghiệm", 1, "Phản ứng lượng nhỏ", w => TestTubePart(w, "Ống nghiệm", z, false));
            Add("ong_nghiem_to", "Ống nghiệm to", 1, "Nhiệt phân, thu khí", w => TestTubePart(w, "Ống nghiệm to", z, true));
            Add("bat_su", "Bát sứ", 1, "Cô cạn", w => EvapDishPart(w, "Bát sứ cô cạn", z));

            Add("dau_noi", "Đầu nối chưng cất", 2, "3 khớp nhám", w => StillHeadPart(w, "Đầu nối chưng cất", z));
            Add("nhiet_ke", "Nhiệt kế", 2, "Khớp 14/23", w => ThermometerPart(w, "Nhiệt kế có khớp nối", z));
            Add("sinh_han", "Sinh hàn thẳng", 2, "Chưng cất", w => CondenserPart(w, "Ống sinh hàn thẳng", z, false));
            Add("sinh_han_bau", "Sinh hàn bầu", 2, "Đun hồi lưu", w => CondenserPart(w, "Ống sinh hàn hồi lưu (bầu)", z, true));
            Add("dau_noi_hung", "Đầu nối hứng", 2, "Nhỏ giọt vào bình hứng", w => ReceiverAdapterPart(w, "Đầu nối hứng", z));

            Add("pheu_loc", "Phễu lọc", 3, "Lọc thường", w => FilterFunnelPart(w, "Phễu lọc", z));
            Add("pheu_chiet", "Phễu chiết", 3, "Tách hai lớp lỏng", w => SepFunnelPart(w, "Phễu chiết 125 ml", z));
            Add("pheu_nho_giot", "Phễu nhỏ giọt", 3, "Có khóa", w => DroppingFunnelPart(w, "Phễu nhỏ giọt", z));
            Add("buchner", "Phễu Buchner", 3, "Lọc hút", w => BuchnerPart(w, "Phễu Buchner", z));
            Add("binh_hut_loc", "Bình hút lọc", 3, "Có nhánh hút", w => SuctionFlaskPart(w, "Bình hút lọc 250 ml", z));

            Add("binh_rua_khi", "Bình rửa khí", 4, "Vào ống dài, ra ống ngắn", w => GasWashBottlePart(w, "Bình rửa khí", z));
            Add("ong_u", "Ống chữ U", 4, "Làm khô khí", w => UTubePart(w, "Ống chữ U làm khô", z));
            Add("chau", "Chậu thu khí", 4, "Thu khí đẩy nước", w => TroughPart(w, "Chậu thủy tinh (thu khí)", z));
            Add("lo_thu_khi", "Lọ thu khí", 4, "Thu khí đẩy không khí", w => GasJarPart(w, "Lọ thu khí", z));

            Add("nut1_30", "Nút 1 lỗ Ø30", 5, "Bình nón", w => StopperPart(w, "Nút cao su 1 lỗ Ø30", z, 30f, 1));
            Add("nut2_30", "Nút 2 lỗ Ø30", 5, "Bình nón", w => StopperPart(w, "Nút cao su 2 lỗ Ø30", z, 30f, 2));
            Add("nut1_26", "Nút 1 lỗ Ø26", 5, "Bình cầu có nhánh", w => StopperPart(w, "Nút cao su 1 lỗ Ø26", z, 26f, 1));
            Add("nut1_17", "Nút 1 lỗ Ø17", 5, "Ống nghiệm", w => StopperPart(w, "Nút cao su 1 lỗ Ø17", z, 17f, 1));
            Add("nut0_17", "Nút đặc Ø17", 5, "Ống nghiệm", w => StopperPart(w, "Nút cao su đặc Ø17", z, 17f, 0));
            Add("ong_l", "Ống dẫn chữ L", 5, "Ø7", w => LTubePart(w, "Ống dẫn chữ L", z));
            Add("ong_thang", "Ống dẫn thẳng", 5, "Ø7", w => StraightTubePart(w, "Ống dẫn thẳng", z));
            Add("ong_cao_su_dai", "Ống cao su 1,2 m", 5, "Nước làm lạnh, hút", w => HosePart(w, "Ống cao su dài", z, 1.2f));
            Add("ong_cao_su_ngan", "Ống cao su 0,6 m", 5, "Dẫn khí", w => HosePart(w, "Ống cao su ngắn", z, 0.6f));

            lib.entries = list.ToArray();
            Debug.Log($"[KHKT] Kho bộ phận mẫu: {list.Count} loại.");
            return lib;
        }

        /// <summary>Tắt các bản mẫu (gọi sau khi trình dựng cảnh đã gắn thông tin, đèn cồn, lớp, đánh dấu tĩnh).</summary>
        static void DeactivateTemplates(Transform root)
        {
            var g = root.Find(PartLibraryName);
            if (g == null) return;
            foreach (Transform t in g) t.gameObject.SetActive(false);
        }

        // =====================================================================
        // Xưởng lắp ráp ảo
        // =====================================================================

        static void BuildWorkshop(Transform root, Camera viewer, Material ghost, Shader outlineShader, Shader glassShader)
        {
            var w = Group(WorkshopName, root, WorkshopPos);
            var floor = Mat("SanXuongAo", new Color(0.62f, 0.66f, 0.7f), 0.25f);
            var back = Mat("PhongNenXuong", new Color(0.78f, 0.82f, 0.86f), 0.1f);
            Box(w, "Sàn xưởng", new Vector3(0, -0.01f, 0), new Vector3(9f, 0.02f, 9f), floor);
            Box(w, "Phông nền sau", new Vector3(0, 2.2f, 2.6f), new Vector3(9f, 4.4f, 0.1f), back, false);
            Box(w, "Phông nền trái", new Vector3(-4.5f, 2.2f, 0), new Vector3(0.1f, 4.4f, 9f), back, false);
            Box(w, "Phông nền phải", new Vector3(4.5f, 2.2f, 0), new Vector3(0.1f, 4.4f, 9f), back, false);

            var table = Group("Bàn ảo", w);
            var grid = Tex("LuoiBanAo", 128, 128, (x, y) =>
            {
                bool line = x % 64 < 2 || y % 64 < 2;
                bool fine = x % 16 == 0 || y % 16 == 0;
                return line ? new Color(0.62f, 0.72f, 0.78f) : fine ? new Color(0.86f, 0.9f, 0.92f) : new Color(0.93f, 0.95f, 0.96f);
            });
            Box(table, "Mặt bàn ảo", new Vector3(0, 0.88f, 0), new Vector3(2.4f, 0.04f, 1.2f), Mat("MatBanAo", Color.white, 0.35f, 0, grid, new Vector2(12f, 6f)));
            Box(table, "Thân bàn ảo", new Vector3(0, 0.43f, 0), new Vector3(2.36f, 0.86f, 1.16f), cabinet);
            Box(table, "Viền bàn ảo", new Vector3(0, 0.885f, -0.6f), new Vector3(2.4f, 0.03f, 0.01f), accent, false);
            Box(table, "Vách sau bàn ảo", new Vector3(0, 1.25f, 0.62f), new Vector3(2.4f, 0.7f, 0.04f), panelGrey);
            Label(table, "Vòi nước, vòi hút để thử nối ống · ra bàn đích nối lại vào vòi thật", new Vector3(0, 1.52f, 0.598f), Vector3.back, 0.026f, new Color(0.15f, 0.2f, 0.25f));
            float fy = 1.08f, fz = 0.585f;
            Fixture(table, "Vòi cấp nước (xưởng) 1", new Vector3(-0.75f, fy, fz), new Color(0.15f, 0.45f, 0.9f), "Vòi cấp nước 1 (đầu nối ống cao su)");
            Fixture(table, "Ống thoát nước (xưởng) 1", new Vector3(-0.63f, fy, fz), new Color(0.35f, 0.38f, 0.42f), "Ống thoát nước 1 (đầu nối ống cao su)");
            Fixture(table, "Vòi cấp nước (xưởng) 2", new Vector3(0.15f, fy, fz), new Color(0.15f, 0.45f, 0.9f), "Vòi cấp nước 2 (đầu nối ống cao su)");
            Fixture(table, "Ống thoát nước (xưởng) 2", new Vector3(0.27f, fy, fz), new Color(0.35f, 0.38f, 0.42f), "Ống thoát nước 2 (đầu nối ống cao su)");
            Fixture(table, "Vòi hút chân không (xưởng)", new Vector3(0.75f, fy, fz), new Color(0.9f, 0.55f, 0.1f), "Vòi hút chân không (đầu nối ống cao su)");

            var lamp = new GameObject("Đèn xưởng").AddComponent<Light>();
            lamp.transform.SetParent(w, false);
            lamp.transform.localPosition = new Vector3(0.6f, 3.2f, -1.6f);
            lamp.transform.LookAt(w.TransformPoint(new Vector3(0, 0.9f, 0)));
            lamp.type = LightType.Spot;
            lamp.range = 8f;
            lamp.spotAngle = 85f;
            lamp.intensity = 4f;
            lamp.color = new Color(1f, 0.97f, 0.92f);
            lamp.shadows = LightShadows.Soft;
            var fill = new GameObject("Đèn phụ xưởng").AddComponent<Light>();
            fill.transform.SetParent(w, false);
            fill.transform.localPosition = new Vector3(-1.5f, 2.0f, -1.0f);
            fill.type = LightType.Point;
            fill.range = 6f;
            fill.intensity = 1.2f;
            fill.shadows = LightShadows.None;

            Camera MakeCam(string name, Transform parent, float fov, Color bg, bool post)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                var c = go.AddComponent<Camera>();
                c.fieldOfView = fov;
                c.nearClipPlane = 0.03f;
                c.farClipPlane = 15f;
                c.clearFlags = CameraClearFlags.SolidColor;
                c.backgroundColor = bg;
                var data = c.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = post;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                data.antialiasingQuality = AntialiasingQuality.High;
                c.enabled = false;
                return c;
            }
            var cam = MakeCam("Camera xưởng", w, 45f, new Color(0.7f, 0.75f, 0.8f), true);
            var stage = Group("Chỗ chụp ảnh nhỏ", w, new Vector3(30f, 0, 0));
            Box(stage, "Đế chụp", new Vector3(0, -0.01f, 0), new Vector3(2f, 0.02f, 2f), floor, false);
            var stageLight = new GameObject("Đèn chụp").AddComponent<Light>();
            stageLight.transform.SetParent(stage, false);
            stageLight.transform.localPosition = new Vector3(-0.5f, 1.2f, -0.8f);
            stageLight.type = LightType.Point;
            stageLight.range = 4f;
            stageLight.intensity = 1.5f;
            var thumb = MakeCam("Camera ảnh nhỏ", stage, 30f, new Color(0.16f, 0.21f, 0.27f, 1f), true);

            var ws = w.gameObject.AddComponent<FabricationWorkspace>();
            ws.workCamera = cam;
            ws.viewCamera = viewer;
            ws.viewer = viewer != null ? viewer.GetComponentInParent<DesktopViewer>() : null;
            ws.table = table;
            ws.tableTop = 0.9f;
            ws.tableHalf = new Vector2(1.15f, 0.55f);
            ws.thumbStage = stage;
            ws.thumbCamera = thumb;
            ws.library = root.Find(PartLibraryName)?.GetComponent<PartLibrary>();
            ws.ghostMaterial = ghost;
            ws.outlineShader = outlineShader;
            ws.glassShader = glassShader;
            ws.machineLight = root.Find("Phòng thí nghiệm/Khu chế tạo dụng cụ/Máy gia công/Đèn trạng thái máy")?.GetComponent<Renderer>();
        }
    }
}
