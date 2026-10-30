using Khkt.Rendering;
using UnityEngine;
using static Khkt.EditorTools.LabKit;

namespace Khkt.EditorTools
{
    public static partial class LabSceneBuilder
    {
        static void BuildFloor2Shell(Transform f2)
        {
            var g = Group("Vỏ kiến trúc tầng 2", f2);
            float cx = StoreX / 2;
            float z0 = WingZ - 0.1f, z1 = BalconyZ + 0.1f;

            // =====================================================================
            // 1. Sàn các phòng Tầng 2 (y cục bộ -0.1..0, tức thế giới 3.7..3.8 m)
            // =====================================================================
            Box(g, "Sàn phòng sinh học", new Vector3(LabX / 2, -0.05f, LabZ / 2), new Vector3(LabX, 0.1f, LabZ), floorLab);
            Box(g, "Sàn phòng vi sinh", new Vector3((LabX + StoreX) / 2, -0.05f, 11.25f), new Vector3(StoreX - LabX, 0.1f, 7.5f), floorLab);
            Box(g, "Sàn kho sinh phẩm", new Vector3((LabX + StoreX) / 2, -0.05f, 3.75f), new Vector3(StoreX - LabX, 0.1f, 7.5f), floorChem);
            Box(g, "Sàn hành lang tầng 2", new Vector3(cx, -0.05f, CorridorZ / 2), new Vector3(StoreX, 0.1f, -CorridorZ), floorHall);
            Box(g, "Sàn sinh học phân tử", new Vector3(RoomSplitX / 2, -0.05f, (WingZ + CorridorZ) / 2), new Vector3(RoomSplitX, 0.1f, CorridorZ - WingZ), floorLab);
            Box(g, "Sàn nhà kính", new Vector3((RoomSplitX + StoreX) / 2, -0.05f, (WingZ + CorridorZ) / 2), new Vector3(StoreX - RoomSplitX, 0.1f, CorridorZ - WingZ), concrete);

            // Sàn hành lang đông tầng 2 (x 27..30, z 6..15)
            Box(g, "Sàn hành lang đông", new Vector3(28.5f, -0.05f, 10.5f), new Vector3(3f, 0.1f, 9f), floorHall);

            // Sàn lối đi tầng 2 nối lõi thang với hành lang đông (x 27..30, z -2.2..6)
            Box(g, "Sàn sảnh thang tầng 2", new Vector3(28.5f, -0.05f, 1.9f), new Vector3(3f, 0.1f, 8.2f), floorHall);

            // 6 cột bê tông đỡ hành lang đông từ mặt đất tầng 1 (y = -3.8..0 theo hệ cục bộ tầng 2)
            var cols = Group("Cột đỡ hành lang đông", g);
            float[] colX = { 27.2f, 29.8f };
            float[] colZ = { 6.2f, 10.5f, 14.8f };
            foreach (float px in colX)
            {
                foreach (float pz in colZ)
                {
                    Box(cols, $"Cột ({px:0.0}, {pz:0.0})", new Vector3(px, -1.9f, pz), new Vector3(0.35f, 3.8f, 0.35f), concrete);
                }
            }

            // =====================================================================
            // 2. Trần và mái Tầng 2 (trừ nhà kính thực vật có mái kính riêng)
            // =====================================================================
            // Trần phía bắc (x 0..27, z 0..15)
            Box(g, "Trần phía bắc", new Vector3(cx, F2_H + 0.05f, LabZ / 2), new Vector3(StoreX + 0.2f, 0.1f, LabZ + 0.2f), ceilingMat);
            // Trần hành lang (x 0..27, z -2.2..0)
            Box(g, "Trần hành lang", new Vector3(cx, F2_H + 0.05f, CorridorZ / 2), new Vector3(StoreX + 0.2f, 0.1f, -CorridorZ), ceilingMat);
            // Trần phòng sinh học phân tử (x 0..13.5, z -9..-2.2)
            Box(g, "Trần sinh học phân tử", new Vector3(RoomSplitX / 2, F2_H + 0.05f, (WingZ + CorridorZ) / 2), new Vector3(RoomSplitX + 0.2f, 0.1f, CorridorZ - WingZ), ceilingMat);
            // Trần hành lang đông (x 27..30, z 6..15)
            Box(g, "Trần hành lang đông", new Vector3(28.5f, F2_H + 0.05f, 10.5f), new Vector3(3.2f, 0.1f, 9.2f), ceilingMat);
            // Trần sảnh thang (x 27..30, z -2.2..6)
            Box(g, "Trần sảnh thang", new Vector3(28.5f, F2_H + 0.05f, 1.9f), new Vector3(3.2f, 0.1f, 8.4f), ceilingMat);

            // Mái chính và diềm mái bê tông tầng 2 (phủ dãy bắc và sinh phân tử)
            Box(g, "Mái chính tầng 2", new Vector3(cx, F2_H + 0.25f, (WingZ + LabZ) / 2 + 1f), new Vector3(StoreX + 0.6f, 0.3f, LabZ - WingZ), facade);
            Box(g, "Diềm mái tầng 2", new Vector3(cx, F2_H + 0.45f, (WingZ + LabZ) / 2 + 1f), new Vector3(StoreX + 0.7f, 0.1f, LabZ - WingZ + 0.2f), accentDark, false);

            // =====================================================================
            // 3. Mái kính Nhà kính thực vật (hai dốc, khung nhôm, đỉnh cao 4,8 m)
            // =====================================================================
            BuildGreenhouseRoof(g);

            // =====================================================================
            // 4. Tường các phòng Tầng 2
            // =====================================================================

            // ----- Tường giữa Phòng Sinh học và Hành lang tầng 2 (z = 0, x 0..20) -----
            Wall(g, "Tường nam phòng sinh học", true, 0f, -0.1f, LabX + 0.1f, paint, paint, -1,
                Auto(1.6f, 3.2f, -1, ZoneMap.CorridorT2, ZoneMap.BioLab, leafBioLab),
                WinF(16.6f, 18.6f, 1.15f, 2.6f));

            // ----- Tường giữa Kho sinh phẩm và Hành lang tầng 2 (z = 0, x 20..27) -----
            Wall(g, "Tường nam kho sinh phẩm", true, 0f, LabX - 0.1f, StoreX + 0.1f, paint, paint, -1);

            // ----- Tường bắc Phòng Sinh học & Phòng Vi sinh (z = 15) -----
            Wall(g, "Tường bắc tầng 2", true, LabZ, -0.1f, StoreX + 0.1f, paint, facade, +1,
                Win(1.0f, 4.2f, 1.15f, 2.6f),
                Win(14.2f, 16.6f, 1.15f, 2.6f),
                Win(17.2f, 19.4f, 1.15f, 2.6f),
                Win(25.0f, 26.6f, 1.7f, 2.7f)); // Phòng vi sinh kính cố định

            // ----- Tường tây Phòng Sinh học (x = 0, z 0..15) -----
            Wall(g, "Tường tây phòng sinh học", false, 0f, 0.1f, LabZ - 0.1f, paint, facade, -1);

            // ----- Tường ngăn giữa Phòng Sinh học và Kho/Vi sinh (x = 20, z 0..15) -----
            // Đoạn z 0..7.5 ngăn với Kho sinh phẩm: có cửa tự động
            // Đoạn z 7.5..15 ngăn với Vi sinh: có vách kính mờ z 10..13 và pass-box z 8.0..8.6
            Wall(g, "Tường ngăn lab - kho/vi sinh", false, LabX, 0.1f, LabZ - 0.1f, paint, paint, +1,
                Auto(0.8f, 2.2f, +1, ZoneMap.BioLab, ZoneMap.BioStore, leafBioStore),
                WinF(10.0f, 13.0f, 1.2f, 2.4f),
                WinF(8.0f, 8.6f, 0.95f, 1.55f)); // pass-box thông sang vi sinh

            // ----- Tường ngăn giữa Kho sinh phẩm và Phòng Vi sinh (z = 7.5, x 20..27) -----
            Wall(g, "Tường ngăn kho - vi sinh", true, 7.5f, LabX + 0.1f, StoreX - 0.1f, paint, paint, +1);

            // ----- Buồng đệm Phòng Vi sinh (x 25.2..27, z 7.5..10.0) -----
            // Vách ngăn trong z = 10.0, x 25.2..27 có cửa đi x 25.6..26.7
            Wall(g, "Vách buồng đệm vi sinh", true, 10.0f, 25.2f, StoreX - 0.1f, paint, paint, +1,
                Door(25.6f, 26.7f, 2.3f, 0f));
            // Cửa vào buồng đệm từ hành lang đông (x = 27, z 8.2..9.4)
            Wall(g, "Tường ngăn buồng đệm - hành lang đông", false, StoreX, 7.5f, 10.0f, paint, paint, +1,
                Auto(8.2f, 9.4f, +1, ZoneMap.EastCorridor, ZoneMap.MicroBio, leafMicroBio));
            // Tường ngăn vi sinh với hành lang đông (x = 27, z 10.0..15.0)
            Wall(g, "Tường đông vi sinh", false, StoreX, 10.0f, LabZ - 0.1f, paint, paint, +1);

            // ----- Cửa Kho sinh phẩm từ lối đi lõi thang (x = 27, z 3.0..4.4) -----
            Wall(g, "Tường đông kho sinh phẩm", false, StoreX, 0.1f, 7.5f, paint, paint, +1,
                Auto(3.0f, 4.4f, +1, ZoneMap.Stairs, ZoneMap.BioStore, leafBioStore));

            // ----- Hành lang tầng 2: Tường ngăn với dãy phòng nam (z = -2.2, x 0..27) -----
            Wall(g, "Tường hành lang tầng 2 nam", true, CorridorZ, -0.1f, StoreX + 0.1f, paint, paint, -1,
                Auto(5.9f, 7.5f, +1, ZoneMap.Molecular, ZoneMap.CorridorT2, leafMolecular),
                WinF(1.0f, 4.8f, 1.0f, 2.6f),
                WinF(8.6f, 12.6f, 1.0f, 2.6f),
                Auto(19.0f, 20.6f, +1, ZoneMap.Greenhouse, ZoneMap.CorridorT2, leafGreenhouse),
                WinF(14.4f, 18.2f, 1.0f, 2.6f),
                WinF(21.6f, 26.0f, 1.0f, 2.6f));

            // Cửa ở đầu đông hành lang tầng 2 kết nối sảnh thang (x = 27, z -1.9..-0.3)
            Wall(g, "Cửa đầu đông hành lang tầng 2", false, StoreX, CorridorZ + 0.1f, -0.1f, paint, paint, +1,
                Auto(-1.9f, -0.3f, +1, ZoneMap.CorridorT2, ZoneMap.Stairs, leafBioLab));

            // ----- Phòng Sinh học phân tử (x 0..13.5, z -9..-2.2) -----
            Wall(g, "Tường nam sinh học phân tử", true, WingZ, -0.1f, RoomSplitX + 0.1f, paint, facade, -1,
                Win(1.2f, 3.6f, 0.9f, 2.6f),
                Win(4.8f, 7.2f, 0.9f, 2.6f),
                Win(8.4f, 10.8f, 0.9f, 2.6f));
            Wall(g, "Tường tây sinh học phân tử", false, 0f, WingZ + 0.1f, CorridorZ - 0.1f, paint, facade, -1);
            Wall(g, "Tường ngăn phân tử - nhà kính", false, RoomSplitX, WingZ + 0.1f, CorridorZ - 0.1f, paint, paint, +1);

            // ----- Nhà kính thực vật: Vách kính nam và đông -----
            BuildGreenhouseGlassWalls(g);

            // ----- Hành lang đông tầng 2: Vách kính ngoài hướng đông (x = 30, z 6..15) -----
            BuildEastCorridorGlassWall(g);

            // =====================================================================
            // 5. Ánh sáng cơ bản tầng 2
            // =====================================================================
            var lights = Group("Đèn trần tầng 2", g);
            // Phòng sinh học: 9 đèn rọi góc rộng
            RoomLights(lights, new[] { 3.7f, 10.3f, 16.9f }, new[] { 2.6f, 7.5f, 12.4f }, 10f, 3.8f);
            // Phòng vi sinh
            RoomLights(lights, new[] { 23.5f }, new[] { 11.25f }, 9f, 3.2f);
            // Kho sinh phẩm
            RoomLights(lights, new[] { 23.5f }, new[] { 3.75f }, 9f, 3.0f);
            // Hành lang tầng 2
            for (float x = 3.5f; x <= 24.5f; x += 5.0f)
                LedPanel(lights, $"Đèn hành lang {x:0}", x, CorridorZ / 2, 2.2f, 7.0f);
            // Phòng sinh học phân tử
            RoomLights(lights, new[] { 4.5f, 9.5f }, new[] { -5.6f }, 9f, 3.4f);
            // Hành lang đông
            for (float z = 8.0f; z <= 13.5f; z += 3.5f)
                LedPanel(lights, $"Đèn hành lang đông {z:0}", 28.5f, z, 2.2f, 7.0f);
        }

        static void BuildGreenhouseRoof(Transform p)
        {
            var g = Group("Mái kính nhà kính", p);
            // Nóc mái tại z = -5.6, x từ 13.5 đến 27, đỉnh cao 4.8 m so với sàn
            float x0 = RoomSplitX, x1 = StoreX;
            float zCenter = (WingZ + CorridorZ) / 2; // -5.6
            float peakY = 4.8f;
            float eaveY = F2_H; // 3.6 m

            // Khung đố nhôm và tấm kính nghiêng hai dốc
            for (float x = x0; x <= x1; x += 1.35f)
            {
                // Khung vì kèo nhôm tam giác
                Box(g, $"Vì kèo {x:0.0}", new Vector3(x, (eaveY + peakY) / 2, zCenter), new Vector3(0.06f, peakY - eaveY, 0.06f), alu, false);
            }

            // Thanh đòn nóc
            Box(g, "Đòn nóc", new Vector3((x0 + x1) / 2, peakY, zCenter), new Vector3(x1 - x0, 0.1f, 0.1f), alu, false);

            // Mái kính dốc nam (từ zCenter đến WingZ)
            float slopeLen = Mathf.Sqrt(Mathf.Pow(zCenter - WingZ, 2) + Mathf.Pow(peakY - eaveY, 2));
            float angleDeg = Mathf.Atan2(peakY - eaveY, zCenter - WingZ) * Mathf.Rad2Deg;

            var roofSouth = Box(g, "Mái kính dốc nam", new Vector3((x0 + x1) / 2, (eaveY + peakY) / 2, (zCenter + WingZ) / 2),
                new Vector3(x1 - x0, 0.015f, slopeLen), windowGlass, false, new Vector3(angleDeg, 0, 0));
            NoShadow(roofSouth);

            // Mái kính dốc bắc (từ zCenter đến CorridorZ)
            var roofNorth = Box(g, "Mái kính dốc bắc", new Vector3((x0 + x1) / 2, (eaveY + peakY) / 2, (zCenter + CorridorZ) / 2),
                new Vector3(x1 - x0, 0.015f, slopeLen), windowGlass, false, new Vector3(-angleDeg, 0, 0));
            NoShadow(roofNorth);
        }

        static void BuildGreenhouseGlassWalls(Transform p)
        {
            var g = Group("Vách kính nhà kính", p);
            // Vách nam (z = -9, x 13.5..27)
            Box(g, "Bậu tường nam", new Vector3((RoomSplitX + StoreX) / 2, 0.2f, WingZ), new Vector3(StoreX - RoomSplitX, 0.4f, F2_T), alu);
            Box(g, "Dầm trên nam", new Vector3((RoomSplitX + StoreX) / 2, F2_H - 0.1f, WingZ), new Vector3(StoreX - RoomSplitX, 0.2f, F2_T), alu);
            var glassSouth = Box(g, "Kính nam", new Vector3((RoomSplitX + StoreX) / 2, 1.9f, WingZ), new Vector3(StoreX - RoomSplitX - 0.1f, 3.0f, 0.015f), windowGlass, false);
            NoShadow(glassSouth);

            // Đố nhôm dọc mỗi 1.35 m
            for (float x = RoomSplitX; x <= StoreX; x += 1.35f)
                Box(g, $"Đố nam {x:0.0}", new Vector3(x, 1.9f, WingZ), new Vector3(0.06f, 3.0f, 0.08f), alu, false);

            // Vách đông (x = 27, z -9..-2.2)
            Box(g, "Bậu tường đông", new Vector3(StoreX, 0.2f, (WingZ + CorridorZ) / 2), new Vector3(F2_T, 0.4f, CorridorZ - WingZ), alu);
            Box(g, "Dầm trên đông", new Vector3(StoreX, F2_H - 0.1f, (WingZ + CorridorZ) / 2), new Vector3(F2_T, 0.2f, CorridorZ - WingZ), alu);
            var glassEast = Box(g, "Kính đông", new Vector3(StoreX, 1.9f, (WingZ + CorridorZ) / 2), new Vector3(0.015f, 3.0f, CorridorZ - WingZ - 0.1f), windowGlass, false);
            NoShadow(glassEast);

            for (float z = WingZ; z <= CorridorZ; z += 1.35f)
                Box(g, $"Đố đông {z:0.0}", new Vector3(StoreX, 1.9f, z), new Vector3(0.08f, 3.0f, 0.06f), alu, false);
        }

        static void BuildEastCorridorGlassWall(Transform p)
        {
            var g = Group("Vách kính hành lang đông", p);
            // x = 30, z 6..15
            Box(g, "Bậu tường", new Vector3(30f, 0.45f, 10.5f), new Vector3(F2_T, 0.9f, 9f), sill);
            Box(g, "Dầm trên", new Vector3(30f, F2_H - 0.2f, 10.5f), new Vector3(F2_T, 0.4f, 9f), alu);
            var glass = Box(g, "Kính toàn phần", new Vector3(30f, 1.85f, 10.5f), new Vector3(0.015f, 1.9f, 8.8f), windowGlass, false);
            NoShadow(glass);
            for (float z = 6.5f; z <= 14.5f; z += 1.6f)
                Box(g, $"Đố kính {z:0.0}", new Vector3(30f, 1.85f, z), new Vector3(0.07f, 1.9f, 0.05f), alu, false);
        }
    }
}
