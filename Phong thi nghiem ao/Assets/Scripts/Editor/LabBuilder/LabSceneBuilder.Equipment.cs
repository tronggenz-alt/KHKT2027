using System.Collections.Generic;
using Khkt.Demo;
using Khkt.Interaction;
using UnityEngine;
using static Khkt.EditorTools.LabKit;

namespace Khkt.EditorTools
{
    /// <summary>
    /// Thiết bị theo danh mục 80 món (data/chem/thiet_bi.json): tủ dụng cụ thủy tinh, bàn thiết bị phân tích
    /// (cân, máy đo pH, độ dẫn, oxi hòa tan, quang phổ, so màu, ly tâm), bàn đun nóng - tổng hợp (bếp khuấy từ,
    /// bếp bọc, bộ chưng cất, đèn Bunsen, lọc chân không, bình Kíp, nung, cô cạn, buret tự động), bình khí lớn.
    /// Mỗi món gắn thẻ thông tin lấy từ danh mục (tên, công dụng, thông số) kèm cách dùng đúng.
    /// </summary>
    public static partial class LabSceneBuilder
    {
        /// <summary>Cách dùng đúng, lưu ý an toàn (theo số thứ tự trong danh mục thiết bị).</summary>
        static readonly Dictionary<int, string> EquipUsage = new Dictionary<int, string>
        {
            [1] = "Vạch trên cốc chỉ để ước lượng; đun qua lưới tản nhiệt.",
            [2] = "Miệng hẹp, lắc tròn không bắn ra ngoài: dùng khi chuẩn độ.",
            [3] = "Đun có lưới tản nhiệt hoặc bếp bọc; không đun khô.",
            [5] = "Thêm nước tới khi đáy mặt khum chạm vạch, đậy nút rồi lắc đều.",
            [6] = "Đun: hơ đều, miệng ống hướng ra chỗ không có người; chỉ chứa tối đa 1/3 ống.",
            [9] = "Đọc ở đáy mặt khum, mắt ngang vạch.",
            [10] = "Hút quá vạch một chút, xả bớt cho mặt khum chạm vạch rồi mới xả vào bình.",
            [12] = "Chọn thể tích, lắp đầu tip mới cho mỗi mẫu.",
            [14] = "Tráng bằng chính dung dịch sẽ đựng; đọc đến 0,05 ml.",
            [16] = "Lắc rồi mở khóa xả khí; để yên cho tách lớp, xả lớp dưới qua khóa.",
            [19] = "Lót giấy lọc vừa lòng phễu, làm ướt trước khi bật bơm.",
            [21] = "Châm bằng diêm, không châm từ đèn khác; tắt bằng nắp, không thổi.",
            [22] = "Mở van khí rồi mới châm lửa; chỉnh lỗ khí cho ngọn lửa xanh.",
            [28] = "Nung xong để nguội trong bình hút ẩm rồi mới cân.",
            [30] = "Dùng kẹp, không cầm chén nóng bằng tay.",
            [32] = "Khuấy nhẹ, không gõ vào thành bình.",
            [33] = "Mỗi lọ dùng một thìa sạch; không lấy hóa chất bằng tay.",
            [34] = "Đặt vật lên đĩa, bấm trừ bì về 0 trước khi cho hóa chất.",
            [35] = "Đóng cửa kính chắn gió khi đọc; không cân vật nóng.",
            [37] = "Nhúng đầu giấy vào dung dịch rồi so với thang màu.",
            [38] = "Rửa điện cực bằng nước cất, thấm khô; hiệu chuẩn bằng dung dịch đệm.",
            [39] = "Chỉ đặt dụng cụ chịu nhiệt; tắt bếp khi rời bàn.",
            [40] = "Thả con khuấy vào cốc, tăng tốc từ từ.",
            [42] = "Tráng thành bình, rửa dụng cụ bằng nước cất.",
            [47] = "Nước làm lạnh vào ở dưới, ra ở trên.",
            [48] = "Bầu nhiệt kế ngang nhánh ra; cho đá bọt chống sôi trào.",
            [49] = "Bôi mỡ chân không ở nắp; mở bằng cách đẩy trượt nắp.",
            [50] = "Đeo suốt buổi thí nghiệm.",
            [51] = "Thay găng khi bị dính hóa chất.",
            [53] = "Kéo cửa kính xuống thấp khi làm; không thò đầu vào trong tủ.",
            [54] = "Giữ mắt mở, rửa liên tục ít nhất 15 phút rồi báo giáo viên.",
            [55] = "Kéo vòng để xối nước toàn thân, cởi bỏ quần áo dính hóa chất.",
            [56] = "Phân loại chất thải; không đổ kim loại nặng xuống bồn rửa.",
            [57] = "Rút chốt, hướng vòi vào gốc lửa, bóp cò.",
            [61] = "Đặt các ống đối xứng, cân bằng khối lượng trước khi quay.",
            [62] = "Chọn bếp bọc vừa bình cầu.",
            [63] = "Cho đá vào bầu giữa, axit vào bầu trên; mở van là có khí.",
            [64] = "Bình khí phải được xích cố định, đội mũ bảo vệ van.",
            [67] = "Lắp bình an toàn giữa bơm và bình hút lọc.",
            [69] = "Đo mẫu trắng trước; lau sạch mặt cuvet.",
        };

        static EquipmentInfo Equip(Transform t, int stt, string extraTitle = null)
        {
            var e = t.gameObject.GetComponent<EquipmentInfo>() ?? t.gameObject.AddComponent<EquipmentInfo>();
            if (equipByStt != null && equipByStt.TryGetValue(stt, out var d))
            {
                e.title = d.ten + (extraTitle != null ? " " + extraTitle : "");
                e.description = $"{d.cong_dung}. Thông số: {d.thong_so}.";
            }
            else e.title = extraTitle ?? t.name;
            EquipUsage.TryGetValue(stt, out e.usage);
            EnsureCollider(t);
            return e;
        }

        // =====================================================================
        // Tủ dụng cụ thủy tinh (tường bắc)
        // =====================================================================

        static void GlasswareWall(Transform lab)
        {
            var g = Group("Tủ dụng cụ", lab);
            GlassCabinet(g, "Tủ dụng cụ thủy tinh A", new Vector3(10.45f, 0, LabZ - 0.33f), 0);
            GlassCabinet(g, "Tủ dụng cụ thủy tinh B", new Vector3(12.5f, 0, LabZ - 0.33f), 1);
        }

        static void GlassCabinet(Transform p, string name, Vector3 pos, int which)
        {
            // Hệ tọa độ tủ: mặt kính nhìn về +x, xoay 90° để quay vào phòng (-z)
            var g = Group(name, p, pos, 90);
            const float d = 0.45f, w = 2.0f, h = 2.1f;
            Box(g, "Vách trái", new Vector3(0, h / 2, -w / 2 + 0.01f), new Vector3(d, h, 0.02f), cabinet);
            Box(g, "Vách phải", new Vector3(0, h / 2, w / 2 - 0.01f), new Vector3(d, h, 0.02f), cabinet);
            Box(g, "Nóc", new Vector3(0, h - 0.01f, 0), new Vector3(d, 0.02f, w), cabinet);
            Box(g, "Đáy", new Vector3(0, 0.05f, 0), new Vector3(d, 0.1f, w), cabinet);
            Box(g, "Tấm sau", new Vector3(-d / 2 + 0.01f, h / 2, 0), new Vector3(0.02f, h, w), panelGrey);
            Box(g, "Dải nhấn", new Vector3(d / 2 + 0.002f, h - 0.04f, 0), new Vector3(0.004f, 0.04f, w), accent, false);
            float[] levels = { 0.1f, 0.55f, 1.0f, 1.45f };
            for (int i = 1; i < levels.Length; i++)
                Box(g, $"Tầng {i}", new Vector3(0, levels[i], 0), new Vector3(d - 0.04f, 0.012f, w - 0.04f), labGlass);
            foreach (float z in new[] { -w / 4, w / 4 })
            {
                NoShadow(Box(g, "Cửa kính", new Vector3(d / 2 + 0.006f, h / 2 + 0.05f, z), new Vector3(0.008f, h - 0.15f, w / 2 - 0.04f), cabinetGlass, false));
                Box(g, "Tay nắm", new Vector3(d / 2 + 0.02f, 1.0f, z - Mathf.Sign(z) * 0.4f), new Vector3(0.02f, 0.12f, 0.015f), steel, false);
            }
            Label(g, "DỤNG CỤ THỦY TINH " + (which == 0 ? "A" : "B"), new Vector3(d / 2 + 0.012f, h + 0.07f, 0), Vector3.right, 0.06f, new Color(0.05f, 0.3f, 0.36f));
            float y0 = levels[0] + 0.01f, y1 = levels[1] + 0.008f, y2 = levels[2] + 0.008f, y3 = levels[3] + 0.008f;
            if (which == 0)
            {
                var beakers = Group("Cốc có mỏ các cỡ", g, new Vector3(0.02f, y0, -0.7f));
                float[] r = { 0.02f, 0.025f, 0.035f, 0.042f, 0.047f, 0.056f };
                float[] hh = { 0.05f, 0.065f, 0.1f, 0.12f, 0.13f, 0.15f };
                float zz = -0.2f;
                for (int i = 0; i < r.Length; i++) { NoShadow(MeshObj(beakers, "Cốc", Beaker($"CocCo{i}", r[i], hh[i]), new Vector3(0, 0, zz), labGlass)); zz += r[i] * 2 + 0.03f; }
                Equip(beakers, 1);
                var flasks = Group("Bình tam giác các cỡ", g, new Vector3(0.02f, y1, -0.7f));
                for (int i = 0; i < 4; i++) NoShadow(MeshObj(flasks, "Bình tam giác", Erlenmeyer(), new Vector3(0, 0, i * 0.11f), labGlass, false, null, 0.8f + 0.15f * i));
                Equip(flasks, 2);
                var side = Group("Bình tam giác có nhánh", g, new Vector3(0.02f, y1, 0.0f + 0.2f));
                NoShadow(MeshObj(side, "Bình", Erlenmeyer(), Vector3.zero, labGlass));
                Cyl(side, "Nhánh", new Vector3(0.025f, 0.12f, 0), 0.004f, 0.05f, labGlass, false, new Vector3(0, 0, 70));
                Equip(side, 59);
                var vac = Group("Bình hút lọc", g, new Vector3(0.02f, y1, 0.55f));
                NoShadow(MeshObj(vac, "Bình", Erlenmeyer(), Vector3.zero, labGlass, false, null, 1.4f));
                Cyl(vac, "Nhánh", new Vector3(0.035f, 0.16f, 0), 0.005f, 0.06f, labGlass, false, new Vector3(0, 0, 70));
                Equip(vac, 20);
                var rb = Group("Bình cầu đáy bằng", g, new Vector3(0.02f, y2, -0.75f));
                NoShadow(MeshObj(rb, "Bình", RoundFlask(), Vector3.zero, labGlass));
                NoShadow(MeshObj(rb, "Bình", RoundFlask(), new Vector3(0, 0, 0.12f), labGlass, false, null, 1.3f));
                Equip(rb, 3);
                var fl = Group("Bình cầu đáy tròn", g, new Vector3(0.02f, y2, -0.35f));
                Cyl(fl, "Vòng đỡ bần", new Vector3(0, 0.008f, 0), 0.03f, 0.016f, Mat("Ban", new Color(0.7f, 0.55f, 0.38f), 0.1f));
                NoShadow(MeshObj(fl, "Bình", Lathe("BinhCauTron", 24, V(0, 0.012f), V(0.03f, 0.02f), V(0.045f, 0.05f), V(0.03f, 0.085f), V(0.013f, 0.095f), V(0.013f, 0.15f), V(0.015f, 0.152f)), Vector3.zero, labGlass));
                Equip(fl, 4);
                var vf = Group("Bình định mức các cỡ", g, new Vector3(0.02f, y2, 0.1f));
                for (int i = 0; i < 3; i++) NoShadow(MeshObj(vf, "Bình định mức", VolumetricFlask(), new Vector3(0, 0, i * 0.12f), labGlass, false, null, 0.7f + 0.2f * i));
                Equip(vf, 5);
                var vf100 = Group("Bình định mức 100 mL", g, new Vector3(0.02f, y2, 0.6f));
                NoShadow(MeshObj(vf100, "Bình định mức", VolumetricFlask(), Vector3.zero, labGlass, false, null, 0.8f));
                Equip(vf100, 72);
                var cyl = Group("Ống đong các cỡ", g, new Vector3(0.02f, y3, -0.8f));
                for (int i = 0; i < 4; i++) NoShadow(MeshObj(cyl, "Ống đong", Cylinder($"OngDong{i}", 0.008f + 0.004f * i, 0.18f + 0.06f * i), new Vector3(0, 0, i * 0.09f), labGlass));
                Equip(cyl, 9);
                var funnel = Group("Phễu lọc", g, new Vector3(0.02f, y3, -0.2f));
                for (int i = 0; i < 2; i++) NoShadow(MeshObj(funnel, "Phễu", FunnelMesh(), new Vector3(0, 0, i * 0.1f), labGlass));
                Equip(funnel, 17);
                var sep = Group("Phễu chiết", g, new Vector3(0.02f, y3, 0.3f));
                NoShadow(MeshObj(sep, "Phễu chiết", SepFunnelMesh(), Vector3.zero, labGlass));
                Box(sep, "Khóa phễu chiết", new Vector3(0, 0.06f, 0), new Vector3(0.008f, 0.008f, 0.03f), bluePlastic, false);
                Equip(sep, 16);
                var paper = Group("Giấy lọc", g, new Vector3(0.02f, y3, 0.7f));
                Box(paper, "Hộp giấy lọc", new Vector3(0, 0.02f, 0), new Vector3(0.13f, 0.04f, 0.13f), Mat("HopGiayLoc", new Color(0.85f, 0.9f, 0.95f), 0.2f));
                Cyl(paper, "Xấp giấy", new Vector3(0, 0.045f, 0), 0.055f, 0.01f, signWhite);
                Equip(paper, 18);
            }
            else
            {
                var dish = Group("Bát sứ", g, new Vector3(0.02f, y0, -0.8f));
                MeshObj(dish, "Bát", Lathe("BatSu", 24, V(0, 0), V(0.03f, 0), V(0.05f, 0.025f), V(0.055f, 0.03f), V(0.05f, 0.03f), V(0.028f, 0.006f), V(0, 0.006f)), Vector3.zero, signWhite);
                Equip(dish, 43);
                var cru = Group("Chén nung và nắp", g, new Vector3(0.02f, y0, -0.5f));
                MeshObj(cru, "Chén nung", CrucibleMesh(), Vector3.zero, signWhite);
                MeshObj(cru, "Nắp chén nung", Lathe("NapChen", 20, V(0, 0.005f), V(0.022f, 0), V(0.022f, 0.003f), V(0, 0.008f)), new Vector3(0, 0, 0.08f), signWhite);
                Equip(cru, 28);
                var lid = Group("Nắp chén nung", g, new Vector3(0.02f, y0, -0.3f));
                MeshObj(lid, "Nắp", Lathe("NapChen", 20, V(0, 0.005f), V(0.022f, 0), V(0.022f, 0.003f), V(0, 0.008f)), Vector3.zero, signWhite);
                Equip(lid, 29);
                var mortar = Group("Cối và chày sứ", g, new Vector3(0.02f, y0, 0.0f));
                MeshObj(mortar, "Cối", Lathe("Coi", 24, V(0, 0), V(0.04f, 0), V(0.055f, 0.045f), V(0.058f, 0.05f), V(0.05f, 0.05f), V(0.03f, 0.012f), V(0, 0.012f)), Vector3.zero, signWhite);
                Cyl(mortar, "Chày", new Vector3(0.015f, 0.06f, 0), 0.01f, 0.1f, signWhite, false, new Vector3(0, 0, 25));
                Equip(mortar, 46);
                var des = Group("Bình hút ẩm", g, new Vector3(0.02f, y0, 0.55f));
                NoShadow(MeshObj(des, "Thân bình hút ẩm", Lathe("BinhHutAm", 28, V(0, 0), V(0.08f, 0), V(0.1f, 0.06f), V(0.1f, 0.15f), V(0.11f, 0.16f), V(0, 0.16f)), Vector3.zero, labGlass));
                NoShadow(MeshObj(des, "Nắp bình hút ẩm", Lathe("NapHutAm", 28, V(0.11f, 0), V(0.1f, 0.03f), V(0.03f, 0.06f), V(0, 0.07f)), new Vector3(0, 0.16f, 0), labGlass));
                Cyl(des, "Silica gel", new Vector3(0, 0.02f, 0), 0.075f, 0.03f, Mat("SilicaGel", new Color(0.3f, 0.5f, 0.95f), 0.3f));
                Equip(des, 49);
                var watch = Group("Mặt kính đồng hồ", g, new Vector3(0.02f, y1, -0.8f));
                NoShadow(MeshObj(watch, "Mặt kính", Lathe("MatKinh", 24, V(0, 0), V(0.04f, 0.008f), V(0.042f, 0.01f), V(0, 0.003f)), Vector3.zero, labGlass));
                Equip(watch, 44);
                var petri = Group("Đĩa Petri", g, new Vector3(0.02f, y1, -0.55f));
                for (int i = 0; i < 3; i++) NoShadow(MeshObj(petri, "Đĩa", Beaker("DiaPetri", 0.045f, 0.012f), new Vector3(0, i * 0.014f, 0), labGlass));
                Equip(petri, 45);
                var buch = Group("Phễu Buchner", g, new Vector3(0.02f, y1, -0.2f));
                MeshObj(buch, "Phễu sứ", Lathe("Buchner", 24, V(0, 0), V(0.008f, 0), V(0.008f, 0.04f), V(0.045f, 0.06f), V(0.045f, 0.12f), V(0.04f, 0.12f), V(0, 0.07f)), Vector3.zero, signWhite);
                Equip(buch, 19);
                var wash = Group("Bình rửa khí", g, new Vector3(0.02f, y1, 0.2f));
                NoShadow(MeshObj(wash, "Bình", ReagentBottle(), Vector3.zero, labGlass, false, null, 1.2f));
                Cyl(wash, "Ống vào", new Vector3(-0.01f, 0.17f, 0), 0.003f, 0.2f, labGlass);
                Cyl(wash, "Ống ra", new Vector3(0.012f, 0.2f, 0), 0.003f, 0.06f, labGlass);
                Equip(wash, 79);
                var safe = Group("Bình an toàn", g, new Vector3(0.02f, y1, 0.6f));
                NoShadow(MeshObj(safe, "Bình", Erlenmeyer(), Vector3.zero, labGlass, false, null, 1.6f));
                Cyl(safe, "Nút cao su", new Vector3(0, 0.235f, 0), 0.03f, 0.03f, rubber);
                Equip(safe, 80);
                var cond = Group("Ống sinh hàn", g, new Vector3(0.02f, y2 + 0.03f, -0.4f));
                NoShadow(Cyl(cond, "Vỏ ngoài", Vector3.zero, 0.016f, 0.4f, labGlass, false, new Vector3(90, 0, 0)));
                NoShadow(Cyl(cond, "Ống trong", Vector3.zero, 0.006f, 0.46f, labGlass, false, new Vector3(90, 0, 0)));
                Equip(cond, 47);
                var tube = Group("Ống dẫn khí", g, new Vector3(0.02f, y2, 0.15f));
                Cyl(tube, "Ống chữ L", new Vector3(0, 0.06f, 0), 0.003f, 0.12f, labGlass);
                Cyl(tube, "Ống ngang", new Vector3(0, 0.12f, 0.05f), 0.003f, 0.1f, labGlass, false, new Vector3(90, 0, 0));
                Cyl(tube, "Ống mềm", new Vector3(0, 0.01f, 0.12f), 0.035f, 0.02f, Mat("OngMem", new Color(0.85f, 0.6f, 0.35f), 0.3f));
                Equip(tube, 66);
                var gp = Group("Pipet chia độ", g, new Vector3(0.02f, y2, 0.4f));
                Box(gp, "Hộp pipet", new Vector3(0, 0.015f, 0), new Vector3(0.06f, 0.03f, 0.36f), Mat("HopPipet", new Color(0.92f, 0.93f, 0.94f), 0.3f));
                for (int i = 0; i < 3; i++) NoShadow(Cyl(gp, "Pipet", new Vector3(-0.015f + i * 0.015f, 0.034f, 0), 0.003f, 0.34f, labGlass, false, new Vector3(90, 0, 0)));
                Equip(gp, 11);
                var vp = Group("Pipet bầu", g, new Vector3(0.02f, y2, 0.65f));
                Box(vp, "Hộp pipet", new Vector3(0, 0.015f, 0), new Vector3(0.06f, 0.03f, 0.36f), Mat("HopPipet", new Color(0.92f, 0.93f, 0.94f), 0.3f));
                NoShadow(Cyl(vp, "Bầu pipet", new Vector3(0, 0.04f, 0.02f), 0.009f, 0.08f, labGlass, false, new Vector3(90, 0, 0)));
                Equip(vp, 10);
                var tubes = Group("Ống nghiệm", g, new Vector3(0.02f, y3, -0.8f));
                TestTubeRack(tubes, Vector3.zero, 0);
                Equip(tubes, 6);
                var rack = Group("Giá ống nghiệm", g, new Vector3(0.02f, y3, -0.5f));
                Box(rack, "Giá nhựa", new Vector3(0, 0.03f, 0), new Vector3(0.08f, 0.06f, 0.24f), Mat("GiaNhuaXanh", new Color(0.2f, 0.55f, 0.75f), 0.3f));
                Equip(rack, 7);
                var cent = Group("Ống ly tâm", g, new Vector3(0.02f, y3, -0.25f));
                for (int i = 0; i < 4; i++) NoShadow(MeshObj(cent, "Ống ly tâm", Lathe("OngLyTam", 16, V(0, 0), V(0.006f, 0.012f), V(0.008f, 0.02f), V(0.008f, 0.1f), V(0, 0.1f)), new Vector3(0, 0, i * 0.03f), translucentPlasticSafe()));
                Equip(cent, 60);
                var rods = Group("Đũa thủy tinh", g, new Vector3(0.02f, y3, 0.05f));
                for (int i = 0; i < 3; i++) NoShadow(Cyl(rods, "Đũa", new Vector3(-0.01f + i * 0.01f, 0.004f, 0), 0.003f, 0.25f, labGlass, false, new Vector3(90, 0, 0)));
                Equip(rods, 32);
                var sp = Group("Thìa xúc hóa chất", g, new Vector3(0.02f, y3, 0.3f));
                Box(sp, "Cán thìa", new Vector3(0, 0.003f, 0), new Vector3(0.01f, 0.004f, 0.16f), steel);
                Box(sp, "Đầu thìa", new Vector3(0, 0.003f, 0.09f), new Vector3(0.018f, 0.004f, 0.03f), steel, false);
                Equip(sp, 33);
                var bar = Group("Con khuấy từ", g, new Vector3(0.02f, y3, 0.48f));
                for (int i = 0; i < 3; i++) Cyl(bar, "Con khuấy", new Vector3(0, 0.005f, i * 0.025f), 0.004f, 0.03f, signWhite, false, new Vector3(0, 0, 90));
                Equip(bar, 41);
                var bulb = Group("Quả bóp cao su", g, new Vector3(0.02f, y3, 0.65f));
                Ball(bulb, "Quả bóp", new Vector3(0, 0.02f, 0), new Vector3(0.035f, 0.04f, 0.035f), Mat("CaoSuDo", new Color(0.72f, 0.1f, 0.08f), 0.45f));
                Equip(bulb, 13);
                var holder = Group("Kẹp ống nghiệm", g, new Vector3(0.02f, y3, 0.82f));
                Box(holder, "Kẹp gỗ", new Vector3(0, 0.006f, 0), new Vector3(0.02f, 0.012f, 0.18f), oak);
                Equip(holder, 8);
            }
        }

        static Material translucentPlasticSafe() => translucentPlastic != null ? translucentPlastic : Mat("NhuaMo", new Color(0.95f, 0.96f, 0.97f, 0.42f), 0.6f, 0, transparent: true);

        static Mesh FunnelMesh() => Lathe("Pheu", 24, V(0, 0), V(0.004f, 0), V(0.004f, 0.06f), V(0.04f, 0.11f), V(0.042f, 0.112f));
        static Mesh SepFunnelMesh() => Lathe("PheuChiet", 24, V(0, 0), V(0.003f, 0), V(0.003f, 0.05f), V(0.01f, 0.07f), V(0.04f, 0.13f), V(0.035f, 0.19f), V(0.01f, 0.21f), V(0.01f, 0.23f));
        static Mesh CrucibleMesh() => Lathe("ChenNung", 20, V(0, 0), V(0.013f, 0), V(0.02f, 0.035f), V(0.021f, 0.036f), V(0.018f, 0.036f), V(0.011f, 0.004f), V(0, 0.004f));

        // =====================================================================
        // Cân, máy đo
        // =====================================================================

        static BalanceScale Balance(Transform p, string name, Vector3 pos, int stt, int decimals, bool shield = false)
        {
            var g = Group(name, p, pos);
            Box(g, "Thân cân", new Vector3(0, 0.035f, 0), new Vector3(0.24f, 0.07f, 0.3f), signWhite);
            Box(g, "Bảng phím", new Vector3(0, 0.05f, 0.155f), new Vector3(0.22f, 0.04f, 0.02f), panelGrey, false, new Vector3(-30, 0, 0));
            var disp = Group("Màn hình cân", g, new Vector3(0, 0.055f, 0.168f));
            var tm = Label(disp, "0.00 g", Vector3.zero, Vector3.forward, 0.018f, new Color(0.3f, 1f, 0.6f)).GetComponent<TextMesh>();
            Cyl(g, "Đĩa cân", new Vector3(0, 0.075f, -0.02f), 0.07f, 0.008f, steel);
            if (shield)
            {
                NoShadow(Box(g, "Lồng kính chắn gió", new Vector3(0, 0.18f, -0.02f), new Vector3(0.22f, 0.2f, 0.24f), cabinetGlass, false));
                Box(g, "Khung lồng kính", new Vector3(0, 0.285f, -0.02f), new Vector3(0.23f, 0.012f, 0.25f), panelGrey, false);
            }
            var pan = Child(g, "Vùng đĩa cân", new Vector3(0, 0.08f, -0.02f));
            var b = g.gameObject.AddComponent<BalanceScale>();
            b.pan = pan;
            b.display = tm;
            b.decimals = decimals;
            b.maxGrams = decimals >= 4 ? 220f : stt == 77 ? 2000f : 620f;
            Equip(g, stt);
            return b;
        }

        static MeterProbe Meter(Transform p, string name, Vector3 pos, int stt, MeterProbe.Kind kind, string unit)
        {
            // Máy để bàn (thân + màn hình) và đầu đo cầm được, dây nối
            var g = Group(name, p, pos);
            Box(g, "Thân máy", new Vector3(0, 0.04f, 0), new Vector3(0.18f, 0.08f, 0.2f), Mat("ThanMayDo", new Color(0.24f, 0.27f, 0.3f), 0.4f));
            Box(g, "Mặt máy", new Vector3(0, 0.085f, 0.03f), new Vector3(0.16f, 0.01f, 0.12f), blackPlastic, false, new Vector3(-20, 0, 0));
            var disp = Group("Màn hình máy đo", g, new Vector3(0, 0.093f, 0.035f));
            disp.localRotation = Quaternion.Euler(70, 0, 0);
            var tm = Label(disp, "---", Vector3.zero, Vector3.up, 0.018f, new Color(0.45f, 0.95f, 1f)).GetComponent<TextMesh>();
            Label(g, unit, new Vector3(0, 0.05f, 0.101f), Vector3.forward, 0.012f, new Color(0.8f, 0.85f, 0.9f));
            Cyl(g, "Cần giữ điện cực", new Vector3(-0.07f, 0.2f, -0.05f), 0.005f, 0.32f, steel);
            Box(g, "Tay giữ", new Vector3(-0.03f, 0.33f, -0.05f), new Vector3(0.08f, 0.012f, 0.012f), darkMetal, false);
            Equip(g, stt);
            // đầu đo
            var probe = Group("Đầu đo " + name.ToLowerInvariant(), g, new Vector3(0.01f, 0.24f, -0.05f));
            Cyl(probe, "Thân đầu đo", new Vector3(0, 0.07f, 0), 0.007f, 0.14f, Mat("DauDo", new Color(0.92f, 0.92f, 0.94f), 0.5f), true);
            NoShadow(Cyl(probe, "Bầu điện cực", new Vector3(0, 0.004f, 0), 0.006f, 0.012f, labGlass));
            Cyl(probe, "Nắp đầu đo", new Vector3(0, 0.15f, 0), 0.008f, 0.02f, blackPlastic);
            var mp = probe.gameObject.AddComponent<MeterProbe>();
            mp.kind = kind;
            mp.display = tm;
            mp.tip = Child(probe, "Đầu đo", Vector3.zero);
            var gr = probe.gameObject.AddComponent<Grabbable>();
            gr.kind = "dau_do";
            gr.gripHeightM = 0.11f;
            gr.massG = 40f;
            Equip(probe, stt, "(đầu đo)");
            return mp;
        }

        static MeterProbe HandMeter(Transform p, string name, Vector3 pos, int stt, MeterProbe.Kind kind)
        {
            var g = Group(name, p, pos);
            Box(g, "Thân máy cầm tay", new Vector3(0, 0.07f, 0), new Vector3(0.04f, 0.14f, 0.025f), Mat("ThanMayCamTay", new Color(0.1f, 0.45f, 0.6f), 0.4f), true);
            var disp = Group("Màn hình", g, new Vector3(0, 0.11f, 0.0131f));
            var tm = Label(disp, "---", Vector3.zero, Vector3.forward, 0.008f, new Color(0.45f, 0.95f, 1f)).GetComponent<TextMesh>();
            Box(g, "Kính màn hình", new Vector3(0, 0.11f, 0.0128f), new Vector3(0.032f, 0.02f, 0.001f), blackPlastic, false);
            Cyl(g, "Đầu đo", new Vector3(0, -0.04f, 0), 0.006f, 0.08f, steel);
            var mp = g.gameObject.AddComponent<MeterProbe>();
            mp.kind = kind;
            mp.display = tm;
            mp.tip = Child(g, "Mũi đo", new Vector3(0, -0.08f, 0));
            var gr = g.gameObject.AddComponent<Grabbable>();
            gr.kind = "dau_do";
            gr.gripHeightM = 0.1f;
            gr.massG = 90f;
            Equip(g, stt);
            // đặt đứng trên giá nhỏ để đầu đo không chạm bàn
            g.localPosition += Vector3.up * 0.08f;
            Box(p, "Giá dựng máy đo", pos + new Vector3(0, 0.04f, 0), new Vector3(0.05f, 0.08f, 0.04f), panelGrey);
            return mp;
        }

        // =====================================================================
        // Bàn thiết bị phân tích (tường nam, phía đông)
        // =====================================================================

        static void InstrumentBench(Transform lab)
        {
            var g = Group("Bàn thiết bị phân tích", lab, new Vector3(16.7f, 0, 0.45f));
            const float len = 5.8f, dep = 0.75f, top = 0.9f;
            Box(g, "Chân đế", new Vector3(0, 0.04f, 0), new Vector3(len - 0.04f, 0.08f, dep - 0.1f), darkMetal);
            Box(g, "Tủ dưới", new Vector3(0, 0.46f, -0.02f), new Vector3(len, 0.76f, dep - 0.06f), cabinet);
            for (int i = 0; i < 8; i++)
                Box(g, $"Ngăn kéo {i + 1}", new Vector3(-len / 2 + 0.36f + i * 0.725f, 0.46f, dep / 2 - 0.045f), new Vector3(0.68f, 0.7f, 0.01f), cabinetDoor, false);
            Box(g, "Mặt bàn", new Vector3(0, top - 0.02f, 0), new Vector3(len, 0.04f, dep), epoxy);
            Box(g, "Dải nhấn", new Vector3(0, top - 0.045f, dep / 2 + 0.002f), new Vector3(len, 0.03f, 0.004f), accent, false);
            Box(g, "Kệ tường", new Vector3(0, 1.75f, -dep / 2 + 0.12f), new Vector3(len, 0.025f, 0.24f), cabinet);
            Label(g, "KHU THIẾT BỊ PHÂN TÍCH", new Vector3(0, 2.0f, -dep / 2 + 0.06f), Vector3.forward, 0.09f, new Color(0.05f, 0.3f, 0.36f));
            g.gameObject.AddComponent<PlaceSurface>();

            float z = 0.02f;
            Balance(g, "Cân phân tích", new Vector3(-2.6f, top, z), 35, 4, true);
            Balance(g, "Cân điện tử", new Vector3(-2.05f, top, z), 34, 2);
            var dial = Group("Cân đĩa", g, new Vector3(-1.55f, top, z));
            Box(dial, "Đế cân đĩa", new Vector3(0, 0.02f, 0), new Vector3(0.32f, 0.04f, 0.14f), darkMetal);
            Cyl(dial, "Trụ", new Vector3(0, 0.1f, 0), 0.008f, 0.16f, steel);
            Box(dial, "Đòn cân", new Vector3(0, 0.18f, 0), new Vector3(0.3f, 0.008f, 0.012f), steel, false);
            foreach (float x in new[] { -0.14f, 0.14f }) Cyl(dial, "Đĩa", new Vector3(x, 0.19f, 0), 0.05f, 0.006f, steel);
            Equip(dial, 58);
            Balance(g, "Cân điện tử bàn", new Vector3(-1.05f, top, z), 77, 1);
            Meter(g, "Máy đo pH", new Vector3(-0.45f, top, z), 38, MeterProbe.Kind.PH, "pH");
            Meter(g, "Máy đo độ dẫn điện", new Vector3(0.05f, top, z), 68, MeterProbe.Kind.Conductivity, "µS/cm");
            Meter(g, "Máy đo oxi hòa tan", new Vector3(0.55f, top, z), 75, MeterProbe.Kind.DissolvedOxygen, "mg/L");
            HandMeter(g, "Máy đo pH cầm tay", new Vector3(0.95f, top, z + 0.1f), 76, MeterProbe.Kind.PH);
            HandMeter(g, "Nhiệt kế điện tử", new Vector3(1.1f, top, z + 0.1f), 71, MeterProbe.Kind.Temperature);
            var therm = Group("Nhiệt kế", g, new Vector3(1.25f, top + 0.005f, z + 0.15f));
            NoShadow(Cyl(therm, "Ống nhiệt kế", new Vector3(0, 0.004f, 0), 0.0035f, 0.3f, labGlass, true, new Vector3(90, 0, 0)));
            Cyl(therm, "Bầu thủy ngân", new Vector3(0, 0.004f, -0.15f), 0.004f, 0.012f, Mat("BauDo", new Color(0.8f, 0.15f, 0.15f), 0.6f), false, new Vector3(90, 0, 0));
            var tp = therm.gameObject.AddComponent<MeterProbe>();
            tp.kind = MeterProbe.Kind.Temperature;
            tp.tip = Child(therm, "Bầu", new Vector3(0, 0, -0.15f));
            var tgr = therm.gameObject.AddComponent<Grabbable>();
            tgr.kind = "dau_do"; tgr.liesOnTable = true; tgr.lengthM = 0.3f; tgr.lyingRadiusM = 0.004f; tgr.gripHeightM = 0.2f; tgr.massG = 15f;
            Equip(therm, 36);
            var phBox = Group("Giấy đo pH", g, new Vector3(1.25f, top, z - 0.15f));
            Box(phBox, "Hộp giấy pH", new Vector3(0, 0.03f, 0), new Vector3(0.08f, 0.06f, 0.05f), Mat("HopGiayPH", new Color(0.95f, 0.8f, 0.3f), 0.3f));
            var tip = Box(phBox, "Đầu giấy", new Vector3(0, 0.075f, 0), new Vector3(0.006f, 0.04f, 0.012f), Mat("GiayThu", new Color(0.95f, 0.88f, 0.55f), 0.1f), false);
            var paper = phBox.gameObject.AddComponent<TestPaper>();
            paper.kind = TestPaper.Kind.Universal;
            paper.tipRenderer = tip.GetComponent<Renderer>();
            var pgr = phBox.gameObject.AddComponent<Grabbable>();
            pgr.kind = "giay"; pgr.gripHeightM = 0.05f; pgr.massG = 20f;
            Equip(phBox, 37);

            // Máy quang phổ có ổ cuvet
            var spec = Group("Máy quang phổ", g, new Vector3(1.75f, top, z));
            Box(spec, "Thân máy quang phổ", new Vector3(0, 0.08f, 0), new Vector3(0.42f, 0.16f, 0.34f), signWhite);
            Box(spec, "Nắp ổ cuvet", new Vector3(0.1f, 0.165f, -0.05f), new Vector3(0.1f, 0.012f, 0.1f), panelGrey, false);
            var sdisp = Group("Màn hình quang phổ", spec, new Vector3(-0.08f, 0.13f, 0.172f));
            Box(sdisp, "Kính", Vector3.zero, new Vector3(0.2f, 0.06f, 0.004f), blackPlastic, false);
            var stm = Label(sdisp, "Đặt cuvet", new Vector3(0, 0, 0.003f), Vector3.forward, 0.014f, new Color(0.45f, 0.95f, 1f)).GetComponent<TextMesh>();
            var slot = Socket(spec, "Ổ đo cuvet", new Vector3(0.1f, 0.172f, -0.05f), new[] { "ong_nghiem", "cuvet" }, false,
                              new Vector3(0, 0.05f, 0), new Vector3(0.06f, 0.1f, 0.06f), new Vector3(0, 0.002f, 0), 0.02f, socketRing);
            var sp = spec.gameObject.AddComponent<Spectrophotometer>();
            sp.slot = slot;
            sp.display = stm;
            Equip(spec, 69);
            for (int i = 0; i < 3; i++) FunctionalVessel(g, "Cuvet", new Vector3(1.48f + i * 0.04f, top, z + 0.2f), "cuvet");

            var colo = Group("Máy so màu", g, new Vector3(2.25f, top, z));
            Box(colo, "Thân máy so màu", new Vector3(0, 0.05f, 0), new Vector3(0.22f, 0.1f, 0.22f), Mat("ThanMaySoMau", new Color(0.88f, 0.9f, 0.92f), 0.4f));
            var cdisp = Group("Màn hình so màu", colo, new Vector3(0, 0.08f, 0.112f));
            var ctm = Label(cdisp, "Đặt cuvet", Vector3.zero, Vector3.forward, 0.01f, new Color(0.45f, 0.95f, 1f)).GetComponent<TextMesh>();
            var cslot = Socket(colo, "Ổ máy so màu", new Vector3(0.05f, 0.1f, -0.04f), new[] { "ong_nghiem", "cuvet" }, false,
                               new Vector3(0, 0.05f, 0), new Vector3(0.05f, 0.1f, 0.05f), new Vector3(0, 0.002f, 0), 0.018f, socketRing);
            var cs = colo.gameObject.AddComponent<Spectrophotometer>();
            cs.slot = cslot;
            cs.display = ctm;
            Equip(colo, 70);
            var std = Group("Bộ ống so màu", g, new Vector3(2.25f, top, z + 0.25f));
            Box(std, "Giá ống so màu", new Vector3(0, 0.02f, 0), new Vector3(0.05f, 0.04f, 0.2f), oak);
            var tube = TestTube();
            for (int i = 0; i < 5; i++)
            {
                var c = Color.Lerp(new Color(0.85f, 0.92f, 1f, 0.35f), new Color(0.1f, 0.3f, 0.9f, 0.85f), i / 4f);
                NoShadow(MeshObj(std, "Ống chuẩn", tube, new Vector3(0, 0.005f, -0.08f + i * 0.04f), labGlass));
                NoShadow(MeshObj(std, "Dung dịch chuẩn", LiquidIn("DichOngNghiem", 0.0075f, 0.08f), new Vector3(0, 0.005f, -0.08f + i * 0.04f), Mat($"DichChuan{i}", c, 0.9f, 0, transparent: true)));
            }
            Equip(std, 78);

            // Máy ly tâm: 2 ổ, ống ly tâm cầm được
            var cen = Group("Máy ly tâm", g, new Vector3(2.7f, top, z));
            MeshObj(cen, "Thân máy ly tâm", Lathe("ThanLyTam", 28, V(0, 0), V(0.16f, 0), V(0.16f, 0.16f), V(0.15f, 0.17f), V(0, 0.17f)), Vector3.zero, signWhite, true);
            NoShadow(MeshObj(cen, "Nắp kính", Lathe("NapLyTam", 28, V(0.15f, 0), V(0.12f, 0.03f), V(0, 0.035f)), new Vector3(0, 0.17f, 0), cabinetGlass));
            var rotor = Group("Rô to", cen, new Vector3(0, 0.15f, 0));
            Cyl(rotor, "Mâm quay", Vector3.zero, 0.1f, 0.01f, steel);
            var cf = cen.gameObject.AddComponent<Centrifuge>();
            cf.rotor = rotor;
            cf.slots = new[]
            {
                Socket(rotor, "Ổ ly tâm 1", new Vector3(0.06f, 0.0f, 0), new[] { "ong_nghiem" }, false, new Vector3(0, 0.05f, 0), new Vector3(0.04f, 0.1f, 0.04f), new Vector3(0, 0.002f, 0), 0.012f, socketRing),
                Socket(rotor, "Ổ ly tâm 2", new Vector3(-0.06f, 0.0f, 0), new[] { "ong_nghiem" }, false, new Vector3(0, 0.05f, 0), new Vector3(0.04f, 0.1f, 0.04f), new Vector3(0, 0.002f, 0), 0.012f, socketRing),
            };
            Equip(cen, 61);
            for (int i = 0; i < 2; i++) FunctionalVessel(g, "Ống ly tâm", new Vector3(2.48f + i * 0.04f, top, z + 0.25f), "ong_ly_tam");

            // Kệ tường: micropipet, buret dự phòng
            var mp = Group("Micropipet", g, new Vector3(-2.4f, 1.765f, -dep / 2 + 0.12f));
            Box(mp, "Giá micropipet", new Vector3(0, 0.12f, -0.06f), new Vector3(0.2f, 0.24f, 0.02f), panelGrey);
            for (int i = 0; i < 3; i++)
            {
                Cyl(mp, "Thân micropipet", new Vector3(-0.06f + i * 0.06f, 0.12f, -0.035f), 0.012f, 0.18f, Mat("MicroPipet", new Color(0.9f, 0.9f, 0.92f), 0.4f));
                Cyl(mp, "Nút bấm", new Vector3(-0.06f + i * 0.06f, 0.22f, -0.035f), 0.008f, 0.02f, i == 0 ? redPlastic : i == 1 ? bluePlastic : Mat("NutVang", new Color(0.95f, 0.8f, 0.2f), 0.4f));
            }
            Equip(mp, 12);
            var dig = Group("Bộ đầu tip", g, new Vector3(-2.0f, 1.765f, -dep / 2 + 0.12f));
            Box(dig, "Hộp tip", new Vector3(0, 0.03f, 0), new Vector3(0.12f, 0.06f, 0.08f), Mat("HopTip", new Color(0.3f, 0.6f, 0.9f, 0.6f), 0.4f, 0, transparent: true));
        }

        // =====================================================================
        // Bàn đun nóng - tổng hợp (giữa phía đông)
        // =====================================================================

        static void SynthesisIsland(Transform lab)
        {
            var g = Group("Bàn đun nóng - tổng hợp", lab, new Vector3(16.8f, 0, 7.5f));
            const float lx = 3.4f, lz = 1.5f, top = 0.9f;
            Box(g, "Chân đế", new Vector3(0, 0.04f, 0), new Vector3(lx - 0.1f, 0.08f, lz - 0.1f), darkMetal);
            Box(g, "Tủ dưới", new Vector3(0, 0.46f, 0), new Vector3(lx - 0.04f, 0.76f, lz - 0.04f), cabinet);
            foreach (float zz in new[] { -lz / 2 + 0.005f, lz / 2 - 0.005f })
                for (int i = 0; i < 5; i++)
                    Box(g, "Cánh tủ", new Vector3(-lx / 2 + 0.36f + i * 0.67f, 0.46f, zz), new Vector3(0.62f, 0.68f, 0.012f), cabinetDoor, false);
            Box(g, "Mặt bàn", new Vector3(0, top - 0.02f, 0), new Vector3(lx, 0.04f, lz), epoxy);
            Box(g, "Giá giữa", new Vector3(0, top + 0.3f, 0), new Vector3(lx - 0.2f, 0.6f, 0.08f), panelGrey);
            Box(g, "Tầng giá giữa", new Vector3(0, top + 0.62f, 0), new Vector3(lx - 0.2f, 0.02f, 0.3f), cabinet);
            Box(g, "Ổ điện giữa", new Vector3(0, top + 0.15f, 0.041f), new Vector3(lx - 0.6f, 0.06f, 0.004f), signWhite, false);
            Box(g, "Ổ điện giữa sau", new Vector3(0, top + 0.15f, -0.041f), new Vector3(lx - 0.6f, 0.06f, 0.004f), signWhite, false);
            Label(g, "BÀN ĐUN NÓNG - TỔNG HỢP", new Vector3(0, top + 0.5f, 0.045f), Vector3.forward, 0.07f, new Color(0.05f, 0.3f, 0.36f));
            Label(g, "BÀN ĐUN NÓNG - TỔNG HỢP", new Vector3(0, top + 0.5f, -0.045f), Vector3.back, 0.07f, new Color(0.05f, 0.3f, 0.36f));
            foreach (var part in g.GetComponentsInChildren<Transform>())
                if (part.name == "Mặt bàn") part.gameObject.AddComponent<PlaceSurface>();

            float zn = 0.42f, zs = -0.42f;
            Destination(g, "Bàn đun nóng – tổng hợp (phía bắc)", 20, new Vector3(0, top, zn), new Vector2(lx - 0.2f, 0.62f), Vector3.forward);
            Destination(g, "Bàn đun nóng – tổng hợp (phía nam)", 21, new Vector3(0, top, zs), new Vector2(lx - 0.2f, 0.62f), Vector3.back);
            // --- Phía bắc (người đứng ở z lớn hơn nhìn về -z) ---
            HotPlateStirrer(g, new Vector3(-1.35f, top, zn));
            HeatingMantle(g, new Vector3(-0.65f, top, zn));
            Distillation(g, new Vector3(0.15f, top, zn));
            BunsenSet(g, new Vector3(0.95f, top, zn));
            SepFunnelStand(g, new Vector3(1.45f, top, zn));
            // --- Phía nam ---
            VacuumFiltration(g, new Vector3(-1.3f, top, zs));
            KippApparatus(g, new Vector3(-0.45f, top, zs));
            CrucibleSet(g, new Vector3(0.3f, top, zs));
            EvaporationSet(g, new Vector3(0.9f, top, zs));
            AutoBurette(g, new Vector3(1.45f, top, zs));

            // Bình khí lớn cạnh tủ hút
            var cyl = Group("Bình chứa khí", lab, new Vector3(19.6f, 0, 5.75f));  // cạnh cửa phòng hóa chất (góc đông bắc dành cho khu chế tạo)
            foreach (var (dx, mat, nm) in new[] { (0f, Mat("BinhKhiXanh", new Color(0.15f, 0.35f, 0.75f), 0.5f, 0.4f), "O₂"), (0.28f, Mat("BinhKhiDen", new Color(0.1f, 0.1f, 0.11f), 0.5f, 0.4f), "N₂") })
            {
                var c = Group("Bình khí " + nm, cyl, new Vector3(0, 0, dx - 0.14f));
                MeshObj(c, "Vỏ bình", Lathe("BinhKhiLon", 24, V(0, 0), V(0.11f, 0), V(0.115f, 0.02f), V(0.115f, 1.25f), V(0.07f, 1.38f), V(0.03f, 1.42f), V(0, 1.42f)), Vector3.zero, mat, true);
                Label(c, nm, new Vector3(-0.117f, 0.9f, 0), Vector3.left, 0.08f, Color.white);
                var reg = Group("Van giảm áp", c, new Vector3(0, 1.45f, 0));
                Cyl(reg, "Thân van", Vector3.zero, 0.02f, 0.06f, steel);
                foreach (float s in new[] { -1f, 1f }) Cyl(reg, "Đồng hồ áp suất", new Vector3(-0.02f, 0.04f, s * 0.035f), 0.025f, 0.02f, Mat("DongHo", new Color(0.95f, 0.95f, 0.92f), 0.5f), false, new Vector3(0, 0, 90));
                Equip(reg, 65);
                Equip(c, 64);
            }
            Box(cyl, "Xích giữ bình", new Vector3(0.12f, 1.1f, 0), new Vector3(0.01f, 0.02f, 0.6f), darkMetal, false);
            Box(cyl, "Giá treo xích", new Vector3(0.2f, 1.1f, 0), new Vector3(0.02f, 0.06f, 0.7f), steel, false);
        }

        static Transform Support(Transform p, Vector3 pos, float height = 0.6f)
        {
            // Giá đỡ (giá sắt): đế + cọc
            var g = Group("Giá đỡ", p, pos);
            Box(g, "Đế giá đỡ", new Vector3(0, 0.006f, 0), new Vector3(0.16f, 0.012f, 0.24f), darkMetal);
            Cyl(g, "Cọc giá đỡ", new Vector3(0, height / 2, -0.08f), 0.006f, height, steel);
            Equip(g, 25);
            return g;
        }

        static Transform Clamp(Transform p, Vector3 pos, float reach = 0.1f)
        {
            var g = Group("Kẹp vạn năng", p, pos);
            Box(g, "Khớp kẹp", Vector3.zero, new Vector3(0.025f, 0.03f, 0.025f), darkMetal);
            Box(g, "Tay kẹp", new Vector3(0, 0, reach / 2), new Vector3(0.01f, 0.01f, reach), darkMetal, false);
            Box(g, "Má kẹp", new Vector3(0, 0, reach), new Vector3(0.03f, 0.02f, 0.012f), Mat("MaKep", new Color(0.65f, 0.15f, 0.1f), 0.3f), false);
            Equip(g, 27);
            return g;
        }

        static void HotPlateStirrer(Transform p, Vector3 pos)
        {
            var g = Group("Bếp điện có khuấy từ", p, pos);
            Box(g, "Thân bếp", new Vector3(0, 0.05f, 0), new Vector3(0.22f, 0.1f, 0.3f), signWhite);
            Box(g, "Mặt gốm", new Vector3(0, 0.103f, -0.03f), new Vector3(0.2f, 0.006f, 0.2f), Mat("MatGom", new Color(0.85f, 0.86f, 0.86f), 0.7f));
            var knobHeat = Box(g, "Núm nhiệt", new Vector3(-0.05f, 0.05f, 0.152f), new Vector3(0.03f, 0.03f, 0.012f), redPlastic, true);
            var knobStir = Box(g, "Núm khuấy", new Vector3(0.05f, 0.05f, 0.152f), new Vector3(0.03f, 0.03f, 0.012f), bluePlastic, true);
            var glow = NoShadowGet(Box(g, "Vòng đỏ nóng", new Vector3(0, 0.1065f, -0.03f), new Vector3(0.12f, 0.001f, 0.12f), Mat("VongNong", new Color(1f, 0.3f, 0.1f), 0.3f, 0, emission: new Color(2.5f, 0.5f, 0.1f)), false));
            glow.SetActive(false);
            var heatT = knobHeat.AddComponent<ToggleEffect>();
            heatT.showWhenOn = new[] { glow };
            heatT.onHint = "Bấm chuột trái: bật bếp (đun nóng)";
            heatT.offHint = "Bấm chuột trái: tắt bếp";
            Equip(knobHeat.transform, 39);
            var heat = g.gameObject.AddComponent<HeatSource>();
            heat.powerW = 350f;
            heat.toggle = heatT;
            heat.zone = Child(g, "Vùng nóng bếp", new Vector3(0, 0.15f, -0.03f));
            heat.zoneSize = new Vector3(0.2f, 0.1f, 0.2f);
            var stirT = knobStir.AddComponent<ToggleEffect>();
            stirT.onHint = "Bấm chuột trái: bật khuấy từ";
            stirT.offHint = "Bấm chuột trái: tắt khuấy";
            Equip(knobStir.transform, 40);
            var st = g.gameObject.AddComponent<MagneticStirrer>();
            st.toggle = stirT;
            st.plate = heat.zone;
            Equip(g, 39, "(kèm khuấy từ)");
            var beaker = FunctionalVessel(g, "Cốc có mỏ 250 ml", new Vector3(0, 0.106f, -0.03f), "coc250");
            var bar = Cyl(beaker.transform, "Con khuấy từ", new Vector3(0, 0.006f, 0), 0.004f, 0.025f, signWhite, false, new Vector3(0, 0, 90));
            st.stirBarVisual = bar.transform;
        }

        static GameObject NoShadowGet(GameObject go) { NoShadow(go); return go; }

        static void HeatingMantle(Transform p, Vector3 pos)
        {
            var g = Group("Bếp bọc gia nhiệt", p, pos);
            MeshObj(g, "Thân bếp bọc", Lathe("BepBoc", 28, V(0, 0), V(0.1f, 0), V(0.1f, 0.1f), V(0.06f, 0.1f), V(0.05f, 0.06f), V(0, 0.06f)), Vector3.zero, Mat("BepBocXam", new Color(0.55f, 0.58f, 0.6f), 0.4f), true);
            var knob = Box(g, "Núm bếp bọc", new Vector3(0, 0.05f, 0.1f), new Vector3(0.03f, 0.03f, 0.012f), redPlastic, true);
            var t = knob.AddComponent<ToggleEffect>();
            t.onHint = "Bấm chuột trái: bật bếp bọc";
            t.offHint = "Bấm chuột trái: tắt bếp bọc";
            var heat = g.gameObject.AddComponent<HeatSource>();
            heat.powerW = 250f;
            heat.toggle = t;
            heat.zone = Child(g, "Vùng nóng", new Vector3(0, 0.07f, 0));
            heat.zoneSize = new Vector3(0.12f, 0.08f, 0.12f);
            Equip(g, 62);
            FunctionalVessel(g, "Bình cầu đáy tròn", new Vector3(0, 0.05f, 0), "binh_cau");
            var sup = Support(g, new Vector3(-0.15f, 0, 0), 0.75f);
            Clamp(sup, new Vector3(0, 0.33f, -0.08f), 0.12f);
            var cond = Group("Ống sinh hàn hồi lưu", g, new Vector3(0, 0.36f, 0));
            NoShadow(Cyl(cond, "Vỏ ngoài", new Vector3(0, 0.16f, 0), 0.016f, 0.3f, labGlass));
            NoShadow(Cyl(cond, "Ống trong", new Vector3(0, 0.16f, 0), 0.006f, 0.36f, labGlass));
            Cyl(cond, "Vòi nước vào", new Vector3(0.022f, 0.04f, 0), 0.004f, 0.03f, labGlass, false, new Vector3(0, 0, 90));
            Cyl(cond, "Vòi nước ra", new Vector3(0.022f, 0.28f, 0), 0.004f, 0.03f, labGlass, false, new Vector3(0, 0, 90));
            Equip(cond, 47, "(lắp hồi lưu)");
        }

        static void Distillation(Transform p, Vector3 pos)
        {
            var g = Group("Bộ chưng cất", p, pos);
            var sup = Support(g, new Vector3(-0.2f, 0, 0.06f), 0.7f);
            Cyl(g, "Đèn đun", new Vector3(-0.2f, 0.06f, 0), 0.03f, 0.12f, steel);
            NoShadow(MeshObj(g, "Bình chưng cất", RoundFlask(), new Vector3(-0.2f, 0.16f, 0), labGlass, false, null, 1.2f));
            NoShadow(Cyl(g, "Cổ chưng cất", new Vector3(-0.2f, 0.39f, 0), 0.01f, 0.1f, labGlass));
            NoShadow(Cyl(g, "Nhiệt kế chưng cất", new Vector3(-0.2f, 0.46f, 0), 0.003f, 0.18f, labGlass));
            NoShadow(Cyl(g, "Ống sinh hàn chưng cất", new Vector3(0.0f, 0.34f, 0), 0.016f, 0.4f, labGlass, false, new Vector3(0, 0, 65)));
            NoShadow(Cyl(g, "Ống nối", new Vector3(0.18f, 0.25f, 0), 0.006f, 0.08f, labGlass));
            NoShadow(MeshObj(g, "Bình hứng", Erlenmeyer(), new Vector3(0.2f, 0, 0), labGlass));
            Clamp(sup, new Vector3(0, 0.36f, -0.08f), 0.1f);
            Equip(g, 48);
        }

        static void BunsenSet(Transform p, Vector3 pos)
        {
            var g = Group("Bộ đun bằng đèn Bunsen", p, pos);
            var b = Group("Đèn Bunsen", g, Vector3.zero);
            Cyl(b, "Đế đèn Bunsen", new Vector3(0, 0.006f, 0), 0.045f, 0.012f, darkMetal, true);
            Cyl(b, "Ống đèn", new Vector3(0, 0.07f, 0), 0.008f, 0.12f, steel, true);
            Cyl(b, "Vòng chỉnh khí", new Vector3(0, 0.03f, 0), 0.011f, 0.015f, Mat("DongThau", new Color(0.75f, 0.6f, 0.25f), 0.6f, 0.8f));
            var flame = NoShadowGet(MeshObj(b, "Ngọn lửa Bunsen", Lathe("NgonLuaXanh", 12, V(0, 0), V(0.008f, 0.01f), V(0.01f, 0.03f), V(0.006f, 0.06f), V(0, 0.08f)), new Vector3(0, 0.13f, 0),
                                            Mat("LuaXanh", new Color(0.35f, 0.55f, 1f, 0.6f), 0.2f, 0, emission: new Color(0.8f, 1.6f, 4f), transparent: true)));
            flame.SetActive(false);
            var t = b.gameObject.AddComponent<ToggleEffect>();
            t.showWhenOn = new[] { flame };
            t.onHint = "Bấm chuột trái: mở van khí, châm đèn Bunsen";
            t.offHint = "Bấm chuột trái: khóa van khí";
            var heat = b.gameObject.AddComponent<HeatSource>();
            heat.powerW = 600f;
            heat.toggle = t;
            heat.zone = Child(b, "Vùng nóng", new Vector3(0, 0.23f, 0));
            heat.zoneSize = new Vector3(0.14f, 0.14f, 0.14f);
            Equip(b, 22);
            var tri = Group("Kiềng ba chân", g, Vector3.zero);
            for (int i = 0; i < 3; i++)
            {
                float a = i * 120f * Mathf.Deg2Rad;
                Cyl(tri, "Chân kiềng", new Vector3(Mathf.Cos(a) * 0.07f, 0.1f, Mathf.Sin(a) * 0.07f), 0.004f, 0.2f, darkMetal);
            }
            MeshObj(tri, "Vòng kiềng", Lathe("VongKieng", 24, V(0.07f, 0), V(0.075f, 0), V(0.075f, 0.006f), V(0.07f, 0.006f), V(0.07f, 0)), new Vector3(0, 0.2f, 0), darkMetal);
            Equip(tri, 23);
            var gauze = Group("Lưới tản nhiệt", g, new Vector3(0, 0.206f, 0));
            Box(gauze, "Lưới", Vector3.zero, new Vector3(0.14f, 0.003f, 0.14f), Mat("LuoiAmiang", new Color(0.55f, 0.55f, 0.55f), 0.3f, 0.5f), true);
            Cyl(gauze, "Tâm lưới", new Vector3(0, 0.002f, 0), 0.04f, 0.001f, Mat("TamLuoi", new Color(0.9f, 0.9f, 0.88f), 0.1f));
            Equip(gauze, 24);
            FunctionalVessel(g, "Cốc có mỏ 100 ml", new Vector3(0, 0.21f, 0), "coc100");
        }

        static void SepFunnelStand(Transform p, Vector3 pos)
        {
            var g = Group("Bộ chiết", p, pos);
            var sup = Support(g, new Vector3(0, 0, 0.06f), 0.65f);
            var ring = Group("Vòng sắt", sup, new Vector3(0, 0.38f, -0.08f));
            Box(ring, "Tay vòng", new Vector3(0, 0, 0.05f), new Vector3(0.008f, 0.008f, 0.1f), darkMetal, false);
            MeshObj(ring, "Vòng", Lathe("VongSat", 24, V(0.035f, 0), V(0.04f, 0), V(0.04f, 0.006f), V(0.035f, 0.006f), V(0.035f, 0)), new Vector3(0, 0, 0.12f), darkMetal);
            Equip(ring, 26);
            var sep = Group("Phễu chiết", g, new Vector3(0, 0.25f, 0.0f));
            NoShadow(MeshObj(sep, "Phễu chiết", SepFunnelMesh(), Vector3.zero, labGlass));
            Box(sep, "Khóa phễu chiết", new Vector3(0, 0.06f, 0), new Vector3(0.008f, 0.008f, 0.03f), bluePlastic, false);
            Equip(sep, 16, "(trên vòng sắt)");
            NoShadow(MeshObj(g, "Bình hứng", Erlenmeyer(), new Vector3(0, 0, 0.0f), labGlass));
        }

        static void VacuumFiltration(Transform p, Vector3 pos)
        {
            var g = Group("Bộ lọc chân không", p, pos);
            NoShadow(MeshObj(g, "Bình hút lọc", Erlenmeyer(), Vector3.zero, labGlass, true, null, 1.5f));
            MeshObj(g, "Phễu Buchner", Lathe("Buchner", 24, V(0, 0), V(0.008f, 0), V(0.008f, 0.04f), V(0.045f, 0.06f), V(0.045f, 0.12f), V(0.04f, 0.12f), V(0, 0.07f)), new Vector3(0, 0.2f, 0), signWhite);
            Cyl(g, "Nút cao su", new Vector3(0, 0.215f, 0), 0.02f, 0.025f, rubber);
            NoShadow(MeshObj(g, "Bình an toàn", Erlenmeyer(), new Vector3(0.2f, 0, 0), labGlass));
            var pump = Group("Bơm hút chân không", g, new Vector3(0.42f, 0, 0));
            Box(pump, "Thân bơm", new Vector3(0, 0.08f, 0), new Vector3(0.18f, 0.16f, 0.24f), Mat("ThanBom", new Color(0.2f, 0.45f, 0.65f), 0.4f));
            Cyl(pump, "Đồng hồ chân không", new Vector3(0, 0.12f, 0.121f), 0.035f, 0.01f, Mat("DongHo", new Color(0.95f, 0.95f, 0.92f), 0.5f), false, new Vector3(90, 0, 0));
            Equip(pump, 67);
            Equip(g, 19, "(lọc chân không)");
        }

        static void KippApparatus(Transform p, Vector3 pos)
        {
            var g = Group("Bộ điều chế khí", p, pos);
            NoShadow(MeshObj(g, "Bình Kíp", Lathe("BinhKip", 28, V(0, 0), V(0.07f, 0), V(0.08f, 0.04f), V(0.07f, 0.09f), V(0.03f, 0.1f), V(0.06f, 0.13f), V(0.065f, 0.18f), V(0.04f, 0.22f),
                                                     V(0.06f, 0.26f), V(0.06f, 0.33f), V(0.02f, 0.36f), V(0.015f, 0.4f)), Vector3.zero, labGlass));
            Cyl(g, "Đá vôi trong bầu", new Vector3(0, 0.15f, 0), 0.05f, 0.03f, Mat("DaVoi", new Color(0.9f, 0.9f, 0.86f), 0.1f));
            NoShadow(MeshObj(g, "Axit trong bầu", LiquidIn("AxitKip", 0.06f, 0.05f), Vector3.zero, liquidClear));
            Cyl(g, "Van khí", new Vector3(0.07f, 0.16f, 0), 0.006f, 0.04f, steel, false, new Vector3(0, 0, 90));
            Cyl(g, "Ống dẫn khí", new Vector3(0.16f, 0.16f, 0), 0.003f, 0.14f, labGlass, false, new Vector3(0, 0, 90));
            Equip(g, 63);
            var wash = Group("Bình rửa khí", g, new Vector3(0.27f, 0, 0));
            NoShadow(MeshObj(wash, "Bình", ReagentBottle(), Vector3.zero, labGlass, false, null, 1.1f));
            NoShadow(MeshObj(wash, "Nước rửa khí", LiquidIn("NuocRuaKhi", 0.034f, 0.08f), Vector3.zero, liquidClear));
            Cyl(wash, "Ống sục", new Vector3(-0.008f, 0.12f, 0), 0.003f, 0.16f, labGlass);
            Equip(wash, 79);
        }

        static void CrucibleSet(Transform p, Vector3 pos)
        {
            var g = Group("Bộ nung chén sứ", p, pos);
            var tri = Group("Kiềng ba chân", g, Vector3.zero);
            for (int i = 0; i < 3; i++)
            {
                float a = i * 120f * Mathf.Deg2Rad;
                Cyl(tri, "Chân kiềng", new Vector3(Mathf.Cos(a) * 0.06f, 0.09f, Mathf.Sin(a) * 0.06f), 0.004f, 0.18f, darkMetal);
            }
            MeshObj(tri, "Vòng kiềng", Lathe("VongKieng", 24, V(0.07f, 0), V(0.075f, 0), V(0.075f, 0.006f), V(0.07f, 0.006f), V(0.07f, 0)), new Vector3(0, 0.18f, 0), darkMetal);
            var clay = Group("Tam giác sứ", g, new Vector3(0, 0.186f, 0));
            for (int i = 0; i < 3; i++)
                Cyl(clay, "Ống sứ", new Vector3(Mathf.Cos(i * 2.094f + 1.57f) * 0.03f, 0.004f, Mathf.Sin(i * 2.094f + 1.57f) * 0.03f), 0.005f, 0.06f, signWhite, false, new Vector3(90, i * 120 + 90, 0));
            Equip(clay, 31);
            var cru = Group("Chén nung", g, new Vector3(0, 0.18f, 0));
            MeshObj(cru, "Chén", CrucibleMesh(), Vector3.zero, signWhite, true);
            Equip(cru, 28);
            AlcoholLamp(g, new Vector3(0, 0, 0));
            var tongs = Group("Kẹp chén nung", g, new Vector3(0.14f, 0.006f, 0));
            Box(tongs, "Càng kẹp", new Vector3(0, 0, 0), new Vector3(0.012f, 0.006f, 0.22f), steel, true, new Vector3(0, 8, 0));
            Box(tongs, "Càng kẹp", new Vector3(0.012f, 0, 0), new Vector3(0.012f, 0.006f, 0.22f), steel, false, new Vector3(0, -8, 0));
            Equip(tongs, 30);
            var lid = Group("Nắp chén nung", g, new Vector3(-0.12f, 0, 0.05f));
            MeshObj(lid, "Nắp", Lathe("NapChen", 20, V(0, 0.005f), V(0.022f, 0), V(0.022f, 0.003f), V(0, 0.008f)), Vector3.zero, signWhite, true);
            Equip(lid, 29);
        }

        static void EvaporationSet(Transform p, Vector3 pos)
        {
            var g = Group("Bộ cô cạn", p, pos);
            var tri = Group("Kiềng ba chân", g, Vector3.zero);
            for (int i = 0; i < 3; i++)
            {
                float a = i * 120f * Mathf.Deg2Rad;
                Cyl(tri, "Chân kiềng", new Vector3(Mathf.Cos(a) * 0.06f, 0.09f, Mathf.Sin(a) * 0.06f), 0.004f, 0.18f, darkMetal);
            }
            MeshObj(tri, "Vòng kiềng", Lathe("VongKieng", 24, V(0.07f, 0), V(0.075f, 0), V(0.075f, 0.006f), V(0.07f, 0.006f), V(0.07f, 0)), new Vector3(0, 0.18f, 0), darkMetal);
            Box(g, "Lưới tản nhiệt", new Vector3(0, 0.186f, 0), new Vector3(0.13f, 0.003f, 0.13f), Mat("LuoiAmiang", new Color(0.55f, 0.55f, 0.55f), 0.3f, 0.5f), true);
            var dish = Group("Bát sứ", g, new Vector3(0, 0.19f, 0));
            MeshObj(dish, "Bát", Lathe("BatSu", 24, V(0, 0), V(0.03f, 0), V(0.05f, 0.025f), V(0.055f, 0.03f), V(0.05f, 0.03f), V(0.028f, 0.006f), V(0, 0.006f)), Vector3.zero, signWhite, true);
            Equip(dish, 43, "(cô cạn)");
            var w = Group("Mặt kính đồng hồ", g, new Vector3(0.15f, 0, 0.05f));
            NoShadow(MeshObj(w, "Mặt kính", Lathe("MatKinh", 24, V(0, 0), V(0.04f, 0.008f), V(0.042f, 0.01f), V(0, 0.003f)), Vector3.zero, labGlass, true));
            Equip(w, 44);
            var petri = Group("Đĩa Petri", g, new Vector3(0.15f, 0, -0.07f));
            NoShadow(MeshObj(petri, "Đĩa", Beaker("DiaPetri", 0.045f, 0.012f), Vector3.zero, labGlass, true));
            Equip(petri, 45);
            var mortar = Group("Cối và chày sứ", g, new Vector3(-0.15f, 0, 0));
            MeshObj(mortar, "Cối", Lathe("Coi", 24, V(0, 0), V(0.04f, 0), V(0.055f, 0.045f), V(0.058f, 0.05f), V(0.05f, 0.05f), V(0.03f, 0.012f), V(0, 0.012f)), Vector3.zero, signWhite, true);
            Cyl(mortar, "Chày", new Vector3(0.015f, 0.06f, 0), 0.01f, 0.1f, signWhite, false, new Vector3(0, 0, 25));
            Equip(mortar, 46);
        }

        static void AutoBurette(Transform p, Vector3 pos)
        {
            var g = Group("Buret tự động", p, pos);
            NoShadow(MeshObj(g, "Bình chứa", ReagentBottle(), Vector3.zero, labGlass, true, null, 2f));
            NoShadow(MeshObj(g, "Dung dịch chuẩn", LiquidIn("DichBuretTD", 0.06f, 0.18f), Vector3.zero, liquidClear));
            NoShadow(Cyl(g, "Ống buret", new Vector3(0, 0.55f, 0), 0.006f, 0.42f, labGlass));
            Cyl(g, "Bơm", new Vector3(0.04f, 0.32f, 0), 0.01f, 0.08f, bluePlastic);
            Cyl(g, "Vòi xả", new Vector3(0.07f, 0.33f, 0), 0.003f, 0.06f, labGlass, false, new Vector3(0, 0, 90));
            Equip(g, 73);
        }

        // =====================================================================
        // Vật chứa dùng được (thủy tinh + dung dịch + lớp cặn)
        // =====================================================================

        static Material socketRing;

        /// <summary>Vật chứa cầm được có phản ứng: coc100, coc250, ong_nghiem, ong_ly_tam, cuvet, binh_non, binh_cau.</summary>
        static LabContainer FunctionalVessel(Transform parent, string name, Vector3 pos, string shape)
        {
            Mesh mesh;
            Vector2[] inner;
            float cap, mouthY, grip, botR, mass;
            string kind;
            Material glass = labGlass;
            int stt;
            switch (shape)
            {
                case "coc100":
                    mesh = Beaker("Coc100", 0.025f, 0.07f); inner = new[] { V(0.0235f, 0.002f), V(0.0235f, 0.07f) }; cap = 100; mouthY = 0.07f; grip = 0.05f; botR = 0.0235f; kind = "coc"; mass = 60; stt = 1; break;
                case "coc250":
                    mesh = Beaker("Coc250", 0.035f, 0.1f); inner = new[] { V(0.0335f, 0.002f), V(0.0335f, 0.1f) }; cap = 250; mouthY = 0.1f; grip = 0.07f; botR = 0.0335f; kind = "coc"; mass = 110; stt = 1; break;
                case "ong_ly_tam":
                    mesh = Lathe("OngLyTam", 16, V(0, 0), V(0.006f, 0.012f), V(0.008f, 0.02f), V(0.008f, 0.1f), V(0, 0.1f)); inner = new[] { V(0.002f, 0.003f), V(0.0055f, 0.012f), V(0.007f, 0.02f), V(0.007f, 0.098f) };
                    cap = 12; mouthY = 0.1f; grip = 0.07f; botR = 0.004f; kind = "ong_nghiem"; glass = translucentPlasticSafe(); mass = 6; stt = 60; break;
                case "cuvet":
                    mesh = Lathe("Cuvet", 4, V(0, 0), V(0.008f, 0), V(0.008f, 0.045f), V(0, 0.045f)); inner = new[] { V(0.007f, 0.002f), V(0.007f, 0.044f) };
                    cap = 3.5f; mouthY = 0.045f; grip = 0.035f; botR = 0.007f; kind = "cuvet"; mass = 4; stt = 69; break;
                case "binh_non":
                    mesh = Erlenmeyer(); inner = null; cap = 250; mouthY = 0.143f; grip = 0.11f; botR = 0.04f; kind = "binh_non"; mass = 120; stt = 2; break;
                case "binh_cau":
                    mesh = RoundFlask(); inner = new[] { V(0.015f, 0.003f), V(0.032f, 0.015f), V(0.04f, 0.045f), V(0.033f, 0.075f), V(0.018f, 0.088f), V(0.011f, 0.095f), V(0.011f, 0.148f) };
                    cap = 250; mouthY = 0.15f; grip = 0.12f; botR = 0.02f; kind = "binh_cau"; mass = 130; stt = 4; break;
                default:  // ống nghiệm
                    mesh = TestTube(); inner = new[] { V(0.0025f, 0.002f), V(0.0055f, 0.005f), V(0.0068f, 0.009f), V(0.0068f, 0.148f) };
                    cap = 20; mouthY = 0.152f; grip = 0.11f; botR = 0.005f; kind = "ong_nghiem"; mass = 12; stt = 6; break;
            }
            var c = Vessel(parent, name, pos, mesh, inner, cap, mouthY, kind, grip, Mat("DungDichPhanUng", new Color(0.85f, 0.92f, 1f, 0.3f), 0.9f, 0, transparent: true));
            c.bottomRadiusM = botR;
            c.glassRenderer = c.transform.Find("Thủy tinh")?.GetComponent<Renderer>();
            if (glass != labGlass && c.glassRenderer != null) c.glassRenderer.sharedMaterial = glass;
            var dep = Cyl(c.transform, "Cặn", new Vector3(0, 0.004f, 0), botR * 0.95f, 0.002f, Mat("CanKetTua", new Color(0.95f, 0.95f, 0.95f), 0.1f));
            NoShadow(dep);
            dep.SetActive(false);
            c.deposit = dep.transform;
            c.GetComponent<Grabbable>().massG = mass;
            Equip(c.transform, stt);
            return c;
        }
    }
}
