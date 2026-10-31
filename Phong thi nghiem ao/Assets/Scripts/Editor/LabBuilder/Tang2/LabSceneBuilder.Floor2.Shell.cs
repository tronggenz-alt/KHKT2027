using Khkt.Rendering;
using UnityEngine;
using static Khkt.EditorTools.LabKit;

namespace Khkt.EditorTools
{
    public static partial class LabSceneBuilder
    {
        // Tọa độ trong tầng 2. Quy tắc chống nhấp nháy (z-fighting): hai mặt cùng hướng không bao giờ nằm trùng một mặt phẳng.
        // Sàn tầng 2 y −0,1..0 nằm trên trần tầng 1 (thế giới 3,6..3,7); trần tầng 2 y 3,6..3,7, mái 3,7..3,95.
        // Các tấm sàn / trần chỉ chạm cạnh, không chồng lên nhau.
        const float F2_ZoneSplitZ = 7.5f;   // ranh giới kho sinh phẩm / phòng vi sinh
        const float F2_AirlockX = 25.2f, F2_AirlockZ = 10.0f;
        const float GreenhouseRidgeY = 4.8f;

        static void BuildFloor2Shell(Transform f2)
        {
            var g = Group("Vỏ kiến trúc tầng 2", f2);

            // ===== 1. Sàn =====
            Slab(g, "Sàn phòng thí nghiệm sinh học", 0, 0, LabX, LabZ, floorLab);
            Slab(g, "Sàn phòng vi sinh", LabX, F2_ZoneSplitZ, StoreX, LabZ, t2EpoxyClean);
            Slab(g, "Sàn kho sinh phẩm", LabX, 0, StoreX, F2_ZoneSplitZ, t2FloorKho);
            Slab(g, "Sàn hành lang tầng 2", 0, CorridorZ, StoreX, 0, floorHall);
            Slab(g, "Sàn phòng sinh học phân tử", 0, WingZ, RoomSplitX, CorridorZ, t2FloorPhanTu);
            Slab(g, "Sàn nhà kính", RoomSplitX, WingZ, StoreX, CorridorZ, t2FloorNhaKinh);
            // Mặt mái hiên tầng 1 nhìn từ cửa sổ tầng 2 (phủ lên trần hiên tầng 1, mặt trên cao hơn 4 cm)
            Slab(g, "Mặt mái hiên", -0.1f, LabZ + 0.13f, StoreX + 0.1f, BalconyZ + 0.1f, facade, -0.1f, -0.06f, false);

            // Đường chỉ tầng quanh mặt ngoài nhà (nhô 3 cm khỏi mặt tường, che khe giữa tường tầng 1 và tường tầng 2)
            float b0 = -0.22f, b1 = 0.02f;
            Slab(g, "Chỉ tầng tây", -0.13f, WingZ - 0.13f, -0.02f, LabZ + 0.13f, t2FloorLine, b0, b1, false);
            Slab(g, "Chỉ tầng bắc", -0.13f, LabZ + 0.02f, StoreX + 0.13f, LabZ + 0.13f, t2FloorLine, b0, b1, false);
            Slab(g, "Chỉ tầng nam", -0.13f, WingZ - 0.13f, StoreX + 0.13f, WingZ - 0.02f, t2FloorLine, b0, b1, false);
            Slab(g, "Chỉ tầng đông", StoreX + 0.02f, WingZ - 0.13f, StoreX + 0.13f, CorridorZ - 0.1f, t2FloorLine, b0, b1, false);

            // ===== 2. Trần và mái =====
            float c0 = F2_H, c1 = F2_H + 0.1f, r1 = F2_H + 0.35f;
            Slab(g, "Trần dãy bắc", -0.1f, CorridorZ - 0.1f, StoreX + 0.1f, LabZ + 0.1f, ceilingMat, c0, c1, false);
            Slab(g, "Trần phòng sinh học phân tử", -0.1f, WingZ - 0.1f, RoomSplitX - 0.1f, CorridorZ - 0.1f, ceilingMat, c0, c1, false);
            Slab(g, "Mái dãy bắc", -0.35f, CorridorZ - 0.1f, StoreX + 0.1f, LabZ + 0.35f, facade, c1, r1);
            Slab(g, "Mái phòng sinh học phân tử", -0.35f, WingZ - 0.35f, RoomSplitX - 0.1f, CorridorZ - 0.1f, facade, c1, r1);
            Slab(g, "Diềm mái dãy bắc", -0.4f, CorridorZ - 0.1f, StoreX + 0.1f, LabZ + 0.4f, accentDark, r1, r1 + 0.08f, false);
            Slab(g, "Diềm mái phân tử", -0.4f, WingZ - 0.4f, RoomSplitX - 0.1f, CorridorZ - 0.1f, accentDark, r1, r1 + 0.08f, false);
            GreenhouseRoof(g);

            // ===== 3. Tường =====
            // Tường giữa dãy bắc và hành lang (z = 0): cửa tự động vào phòng sinh học, vách kính mờ
            Wall(g, "Tường nam phòng sinh học", true, 0f, -0.1f, StoreX + 0.1f, paint, paint, -1,
                Auto(1.6f, 3.2f, -1, ZoneMap.CorridorT2, ZoneMap.BioLab, leafBioLab),
                WinF(16.6f, 18.6f, 1.15f));
            // Tường bắc (ra ngoài): cửa sổ ở chỗ không có tủ an toàn sinh học; phòng vi sinh chỉ có ô kính cố định cao
            Wall(g, "Tường bắc tầng 2", true, LabZ, -0.1f, StoreX + 0.1f, paint, facade, +1,
                Win(1.0f, 4.2f, 1.15f), Win(14.2f, 16.6f, 1.15f), Win(17.2f, 19.4f, 1.15f), Win(25.0f, 26.6f, 1.7f, 2.7f));
            Wall(g, "Tường tây phòng sinh học", false, 0f, 0.1f, LabZ - 0.1f, paint, facade, -1);
            // Tường đông phòng sinh học (x = 20): cửa sang kho, ô pass-box sang vi sinh, kính mờ trên khu giải phẫu
            Wall(g, "Tường ngăn lab – kho, vi sinh", false, LabX, 0.1f, LabZ - 0.1f, paint, null, 0,
                Auto(0.8f, 2.2f, +1, ZoneMap.BioLab, ZoneMap.BioStore, leafBioStore),
                Vent(8.0f, 8.6f, 0.95f, 1.55f),
                WinF(10.0f, 13.0f, 1.2f, 2.4f));
            Wall(g, "Tường ngăn kho – vi sinh", true, F2_ZoneSplitZ, LabX + 0.1f, StoreX - 0.1f, paint, null, 0);
            // Buồng đệm phòng vi sinh: vách kính mờ phía tây, cửa kính tự động vào vùng sạch
            Wall(g, "Vách buồng đệm (tây)", false, F2_AirlockX, F2_ZoneSplitZ + 0.1f, F2_AirlockZ - 0.1f, paint, null, 0, WinF(8.0f, 9.6f, 0.9f, 2.4f));
            Wall(g, "Vách buồng đệm (bắc)", true, F2_AirlockZ, F2_AirlockX - 0.1f, StoreX - 0.1f, paint, null, 0,
                Auto(25.6f, 26.7f, +1, ZoneMap.MicroBio, ZoneMap.MicroBio, leafMicroBio, glass: true));
            // Tường đông dãy bắc (x = 27): cửa hành lang → sảnh thang, kho → lối đi lõi thang, buồng đệm → hành lang đông
            Wall(g, "Tường đông tầng 2", false, StoreX, CorridorZ + 0.1f, LabZ - 0.1f, paint, paint, +1,
                Auto(-1.9f, -0.3f, +1, ZoneMap.CorridorT2, ZoneMap.Stairs, leafBioLab),
                Auto(3.0f, 4.4f, +1, ZoneMap.BioStore, ZoneMap.Stairs, leafBioStore),
                Auto(8.2f, 9.4f, +1, ZoneMap.MicroBio, ZoneMap.EastCorridor, leafMicroBio));
            // Tường hành lang phía nam (z = −2,2): cửa phòng sinh học phân tử, cửa nhà kính, kính mờ
            Wall(g, "Tường hành lang tầng 2", true, CorridorZ, -0.1f, StoreX + 0.1f, paint, paint, -1,
                Auto(5.9f, 7.5f, +1, ZoneMap.Molecular, ZoneMap.CorridorT2, leafMolecular), WinF(1.0f, 4.8f, 1.0f, 2.6f), WinF(8.6f, 12.6f, 1.0f, 2.6f),
                Auto(19.0f, 20.6f, +1, ZoneMap.Greenhouse, ZoneMap.CorridorT2, leafGreenhouse), WinF(14.4f, 18.2f, 1.0f, 2.6f), WinF(21.6f, 26.0f, 1.0f, 2.6f));
            // Phòng sinh học phân tử: tường ngoài nam, tường tây (kéo tới đầu hành lang, có cửa sổ cuối hành lang)
            Wall(g, "Tường nam phòng phân tử", true, WingZ, -0.1f, RoomSplitX + 0.1f, paint, facade, -1,
                Win(1.2f, 3.6f, 0.9f), Win(4.8f, 7.2f, 0.9f), Win(8.4f, 10.8f, 0.9f));
            Wall(g, "Tường tây dãy nam tầng 2", false, 0f, WingZ + 0.1f, -0.1f, paint, facade, -1, Win(-1.8f, -0.4f, 1.0f));
            Wall(g, "Tường ngăn phân tử – nhà kính", false, RoomSplitX, WingZ + 0.1f, CorridorZ - 0.1f, paint, null, 0);
            // Nhà kính: vách kính toàn phần phía nam và phía đông
            CurtainWall(g, "Vách kính nam nhà kính", true, WingZ, RoomSplitX + 0.1f, StoreX + 0.1f);
            CurtainWall(g, "Vách kính đông nhà kính", false, StoreX, WingZ + 0.1f, CorridorZ - 0.1f);
        }

        /// <summary>Vách kính toàn phần: bậu bê tông thấp, kính cường lực, đố nhôm mỗi ~1,35 m, thanh ngang ở 2,3 m.</summary>
        static void CurtainWall(Transform p, string name, bool alongX, float c, float from, float to)
        {
            var g = Group(name, p);
            float len = to - from, mid = (from + to) / 2;
            Box(g, "Bậu chân", P(alongX, c, mid, 0.2f, 0), S(alongX, len, 0.4f, T), sill);
            Box(g, "Dầm đỉnh", P(alongX, c, mid, F2_H - 0.06f, 0), S(alongX, len, 0.12f, T), alu);
            NoShadow(Box(g, "Kính", P(alongX, c, mid, (0.4f + F2_H - 0.12f) / 2, 0), S(alongX, len, F2_H - 0.52f, 0.012f), windowGlass));
            int n = Mathf.Max(1, Mathf.RoundToInt(len / 1.35f));
            for (int i = 0; i <= n; i++)
                Box(g, "Đố đứng", P(alongX, c, from + len * i / n, (0.4f + F2_H - 0.12f) / 2, 0), S(alongX, 0.05f, F2_H - 0.52f, 0.09f), alu, false);
            Box(g, "Thanh ngang", P(alongX, c, mid, 2.3f, 0), S(alongX, len, 0.05f, 0.07f), alu, false);
        }

        /// <summary>
        /// Mái kính hai dốc của nhà kính (nóc dọc trục x tại giữa phòng, đỉnh cao 4,8 m, mái hắt ở 3,6 m): kính, xà nhôm, xà gồ,
        /// đòn nóc, máng nước; đầu hồi đông bằng kính, đầu hồi tây xây kín (giáp mái phòng phân tử).
        /// </summary>
        static void GreenhouseRoof(Transform p)
        {
            var g = Group("Mái kính nhà kính", p);
            float x0 = RoomSplitX, x1 = StoreX, zS = WingZ, zN = CorridorZ, zR = (zS + zN) / 2;
            float yE = F2_H, yR = GreenhouseRidgeY, run = zR - zS, rise = yR - yE;
            float len = Mathf.Sqrt(run * run + rise * rise), ang = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            float cx = (x0 + x1) / 2, wx = x1 - x0;
            int n = Mathf.RoundToInt(wx / 1.35f);

            void SlopeSide(string side, float zc, float a)
            {
                var rot = Quaternion.Euler(a, 0, 0);
                Vector3 normal = rot * Vector3.up;
                Vector3 c = new Vector3(cx, (yE + yR) / 2, zc);
                NoShadow(Box(g, "Kính mái " + side, c, new Vector3(wx, 0.012f, len + 0.04f), windowGlass, true, new Vector3(a, 0, 0)));
                for (int i = 0; i <= n; i++)
                    Box(g, "Xà mái " + side, new Vector3(x0 + wx * i / n, c.y, c.z) - normal * 0.05f, new Vector3(0.05f, 0.08f, len), alu, false, new Vector3(a, 0, 0));
                foreach (float f in new[] { -0.17f, 0.17f })
                    Box(g, "Xà gồ " + side, c - normal * 0.05f + rot * Vector3.forward * (len * f), new Vector3(wx, 0.05f, 0.05f), alu, false, new Vector3(a, 0, 0));
            }
            SlopeSide("dốc nam", (zS + zR) / 2, -ang);
            SlopeSide("dốc bắc", (zR + zN) / 2, ang);
            Box(g, "Đòn nóc", new Vector3(cx, yR - 0.02f, zR), new Vector3(wx, 0.1f, 0.12f), alu, false);
            Box(g, "Máng nước nam", new Vector3(cx, yE + 0.03f, zS - 0.1f), new Vector3(wx + 0.2f, 0.1f, 0.16f), alu, false);
            Box(g, "Máng nước bắc", new Vector3(cx, yE + 0.04f, zN + 0.02f), new Vector3(wx, 0.1f, 0.16f), alu, false);

            var east = Group("Đầu hồi đông", g, new Vector3(x1, 0, 0));
            NoShadow(Triangle(east, "Kính đầu hồi", "T2_KinhDauHoiDong", new Vector2(zS, yE), new Vector2(zN, yE), new Vector2(zR, yR), 0.012f, windowGlass));
            var west = Group("Đầu hồi tây", g, new Vector3(x0, 0, 0));
            Triangle(west, "Tường đầu hồi", "T2_TuongDauHoiTay", new Vector2(zS - 0.1f, yE), new Vector2(zN + 0.1f, yE), new Vector2(zR, yR + 0.08f), T, paint);
            // vòi phun sương dọc đòn nóc (nhìn thấy từ dưới)
            for (int i = 1; i < 6; i++)
                Cyl(g, "Ống phun sương", new Vector3(x0 + wx * i / 6, yR - 0.35f, zR), 0.012f, 0.3f, blackPlastic);
            Box(g, "Ống cấp sương", new Vector3(cx, yR - 0.2f, zR), new Vector3(wx - 0.3f, 0.025f, 0.025f), blackPlastic, false);
        }
    }
}
