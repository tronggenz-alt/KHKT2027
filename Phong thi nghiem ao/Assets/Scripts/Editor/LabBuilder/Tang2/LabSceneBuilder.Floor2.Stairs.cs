using Khkt.Environment.Tang2;
using Khkt.Rendering;
using UnityEngine;
using static Khkt.EditorTools.LabKit;

namespace Khkt.EditorTools
{
    /// <summary>
    /// Lõi thang bộ + thang máy (x 27..30, z −2,2..6) và hành lang đông tầng 2 (x 27..30, z 6..15), tọa độ thế giới.
    ///
    /// Mặt bằng lõi (cả hai tầng):
    ///   giếng thang máy x 28,5..29,8, z −2,1..−0,8, cửa quay về phía tây (x = 28,4)
    ///   sảnh  z −2,2..0 (cửa sang hành lang ở x = 27, z −1,9..−0,3, mỗi tầng một cửa)
    ///   thang bộ một vế x 28,55..29,8 từ z 0,33 (tầng 1) lên z 5,9 (tầng 2): 22 bậc cao 17,3 cm, 21 mặt bậc sâu 26,5 cm
    ///   tầng 2: lỗ thang x 28,5..29,8, z 0..5,9 có lan can kính; lối đi x 27..28,5 nối sảnh với hành lang đông
    /// Vỏ (sàn, tường, mái) ở cảnh lõi để nhìn từ ngoài lúc nào cũng thấy; bậc, lan can, giếng thang ở khu "loi_thang";
    /// cabin, cửa và nút thang máy (có ElevatorController) ở cảnh lõi.
    /// </summary>
    public static partial class LabSceneBuilder
    {
        const float CoreX0 = 27f, CoreX1 = 30f, CoreZ0 = -2.2f, CoreZ1 = 6f;
        const float StairX0 = 28.55f, StairX1 = 29.8f, StairZ0 = 0.33f, StairZ1 = 5.9f;
        const int StairRisers = 22;
        const float ShaftX0 = 28.5f, ShaftX1 = 29.8f, ShaftZ0 = -2.1f, ShaftZ1 = -0.8f;
        const float LiftDoorZ0 = -1.85f, LiftDoorZ1 = -1.05f, LiftDoorH = 2.1f;

        static Material t2LiftSteel, t2LiftGranite;

        static void BuildStairCore(Transform root)
        {
            t2LiftSteel = Mat("T2_InoxXuoc", new Color(0.74f, 0.75f, 0.77f), 0.62f, 0.9f);
            t2LiftGranite = Mat("T2_DaGranitDen", new Color(0.12f, 0.12f, 0.13f), 0.7f);
            BuildCoreShell(root);
            var core = Group("Lõi thang bộ và thang máy", root);  // khu ZoneMap.Stairs
            BuildStairFlight(core);
            BuildLiftShaft(core);
            BuildElevator(root);
            CoreFurnishing(core);
        }

        // ---------------------------------------------------------------------
        // Vỏ lõi thang + hành lang đông (cảnh lõi)
        // ---------------------------------------------------------------------

        static void BuildCoreShell(Transform root)
        {
            var g = Group("Vỏ lõi thang và hành lang đông", root);
            float y2 = F2_Y;
            // Tầng 1: sàn sảnh (chừa hố thang máy), hố thang máy
            Slab(g, "Sàn tầng 1 lõi thang (tây)", CoreX0, CoreZ0 - 0.1f, 28.4f, CoreZ1 + 0.1f, t2CoreStone, -0.3f, 0f);
            Slab(g, "Sàn tầng 1 lõi thang (đông)", 28.4f, ShaftZ1 + 0.1f, CoreX1, CoreZ1 + 0.1f, t2CoreStone, -0.3f, 0f);
            Slab(g, "Đáy hố thang máy", 28.4f, CoreZ0 - 0.1f, CoreX1, ShaftZ1 + 0.1f, darkMetal, -0.5f, -0.4f);
            // Tầng 2: sàn sảnh + lối đi (chừa giếng thang máy và lỗ thang bộ); mặt dưới là trần tầng 1
            var pieces = new (string name, float x0, float z0, float x1, float z1)[]
            {
                ("sảnh tây", CoreX0, CoreZ0 - 0.1f, 28.4f, 0f),
                ("sảnh đông", 28.4f, ShaftZ1 + 0.1f, CoreX1, 0f),
                ("lối đi", CoreX0, 0f, ShaftX0, StairZ1),
                ("ngưỡng đầu thang", CoreX0, StairZ1, CoreX1, CoreZ1 + 0.1f),
            };
            foreach (var pc in pieces)
            {
                Slab(g, "Sàn tầng 2 " + pc.name, pc.x0, pc.z0, pc.x1, pc.z1, t2CoreStone, y2 - 0.1f, y2);
                Slab(g, "Trần tầng 1 " + pc.name, pc.x0 + 0.12f, pc.z0, pc.x1, pc.z1, ceilingMat, y2 - 0.2f, y2 - 0.1f, false);
            }
            // Dầm biên che khe giữa tường tầng 1 (cao 3,6) và tường tầng 2 (từ 3,8) ở mặt x = 27
            Slab(g, "Dầm biên tây", CoreX0, CoreZ0, CoreX0 + 0.12f, CoreZ1, paint, H + 0.02f, y2 - 0.02f, false);

            // Tường bao lõi, mỗi tầng một lượt (hàm Wall của tầng 1 cao H = 3,6 m), dải nối 3,6..3,8 ở giữa
            var f1 = Group("Tường lõi tầng 1", g);
            var f2 = Group("Tường lõi tầng 2", g, new Vector3(0, y2, 0));
            foreach (var (fg, lvl) in new[] { (f1, 1), (f2, 2) })
            {
                Wall(fg, "Tường đông lõi thang", false, 29.9f, CoreZ0 - 0.1f, CoreZ1 + 0.1f, paint, facade, +1, Win(0.6f, 5.4f, 1.0f, 3.0f));
                Wall(fg, "Tường nam lõi thang", true, CoreZ0, CoreX0 + 0.1f, ShaftX1, paint, facade, -1);
                if (lvl == 1) Wall(fg, "Tường bắc lõi thang", true, CoreZ1, CoreX0 + 0.1f, ShaftX1, paint, facade, +1);
                else Wall(fg, "Tường bắc lõi thang", true, CoreZ1, CoreX0 + 0.1f, ShaftX1, paint, null, 0, Vent(CoreX0 + 0.15f, ShaftX1 - 0.05f, 0f, 2.6f));
            }
            Slab(g, "Dải nối tường đông (trong)", 29.8f, CoreZ0 - 0.1f, 29.9f, CoreZ1 + 0.1f, paint, H, y2, false);
            Slab(g, "Dải nối tường đông (ngoài)", 29.9f, CoreZ0 - 0.1f, 30.0f, CoreZ1 + 0.1f, facade, H, y2, false);
            Slab(g, "Dải nối tường nam (trong)", CoreX0 + 0.1f, CoreZ0, ShaftX1, CoreZ0 + 0.1f, paint, H, y2, false);
            Slab(g, "Dải nối tường nam (ngoài)", CoreX0 + 0.1f, CoreZ0 - 0.1f, ShaftX1, CoreZ0, facade, H, y2, false);
            Slab(g, "Dải nối tường bắc", CoreX0 + 0.1f, CoreZ1 - 0.1f, ShaftX1, CoreZ1 + 0.1f, facade, H, y2, false);
            // Trần + mái lõi thang và hành lang đông
            Slab(g, "Trần lõi thang và hành lang đông", CoreX0 + 0.1f, CoreZ0 - 0.1f, CoreX1, LabZ + 0.1f, ceilingMat, y2 + F2_H, y2 + F2_H + 0.1f, false);
            Slab(g, "Mái lõi thang và hành lang đông", StoreX + 0.1f, CoreZ0 - 0.35f, CoreX1 + 0.25f, LabZ + 0.35f, facade, y2 + F2_H + 0.1f, y2 + F2_H + 0.35f);
            Slab(g, "Diềm mái lõi thang", StoreX + 0.1f, CoreZ0 - 0.4f, CoreX1 + 0.3f, LabZ + 0.4f, accentDark, y2 + F2_H + 0.35f, y2 + F2_H + 0.43f, false);
            Slab(g, "Chỉ tầng lõi thang", CoreX1 + 0.02f, CoreZ0 - 0.13f, CoreX1 + 0.13f, LabZ + 0.13f, t2FloorLine, y2 - 0.22f, y2 + 0.02f, false);

            // Hành lang đông tầng 2: sàn trên 6 cột (tầng 1 bên dưới để trống), vách kính phía đông, tường đầu bắc có cửa sổ
            Slab(g, "Sàn hành lang đông", CoreX0, CoreZ1 + 0.1f, CoreX1, LabZ + 0.1f, t2FloorHallEast, y2 - 0.1f, y2);
            Slab(g, "Mặt dưới hành lang đông", CoreX0 + 0.12f, CoreZ1 + 0.1f, CoreX1, LabZ + 0.1f, facade, y2 - 0.4f, y2 - 0.1f, false);
            foreach (float x in new[] { 27.35f, 29.75f })
                foreach (float z in new[] { 6.4f, 10.6f, 14.8f })
                    Box(g, "Cột hành lang đông", new Vector3(x, (y2 - 0.4f) / 2, z), new Vector3(0.3f, y2 - 0.4f, 0.3f), concrete);
            var hall = Group("Vách hành lang đông", g, new Vector3(0, y2, 0));
            Box(hall, "Bậu kính", new Vector3(29.9f, 0.45f, (CoreZ1 + LabZ) / 2 + 0.1f), new Vector3(T, 0.9f, LabZ - CoreZ1), facade);
            Box(hall, "Dầm trên kính", new Vector3(29.9f, F2_H - 0.4f, (CoreZ1 + LabZ) / 2 + 0.1f), new Vector3(T, 0.8f, LabZ - CoreZ1), facade);
            NoShadow(Box(hall, "Kính hành lang đông", new Vector3(29.9f, 1.85f, (CoreZ1 + LabZ) / 2 + 0.1f), new Vector3(0.012f, 1.9f, LabZ - CoreZ1), windowGlass));
            for (float z = CoreZ1 + 0.1f; z <= LabZ + 0.01f; z += (LabZ - CoreZ1 - 0.1f) / 6f)
                Box(hall, "Đố kính", new Vector3(29.9f, 1.85f, z), new Vector3(0.07f, 1.9f, 0.05f), alu, false);
            Wall(hall, "Tường đầu bắc hành lang đông", true, LabZ, StoreX + 0.1f, 29.8f, paint, facade, +1, Win(27.7f, 29.2f, 0.9f));
        }

        // ---------------------------------------------------------------------
        // Thang bộ
        // ---------------------------------------------------------------------

        static void BuildStairFlight(Transform core)
        {
            var g = Group("Thang bộ", core);
            float w = StairX1 - StairX0, cx = (StairX0 + StairX1) / 2;
            float h = F2_Y / StairRisers, d = (StairZ1 - StairZ0) / (StairRisers - 1);
            var riserMat = Mat("T2_CoBacTrang", new Color(0.93f, 0.92f, 0.9f), 0.3f);
            var nosing = Mat("T2_NepChongTron", new Color(0.18f, 0.19f, 0.2f), 0.35f, 0.4f);
            for (int k = 1; k <= StairRisers; k++)
            {
                float zr = StairZ0 + (k - 1) * d;  // mặt cổ bậc thứ k
                Box(g, $"Cổ bậc {k}", new Vector3(cx, (k - 0.5f) * h - 0.02f, zr + 0.012f), new Vector3(w, h - 0.04f, 0.02f), riserMat, false);
                if (k == StairRisers) break;
                Box(g, $"Mặt bậc {k}", new Vector3(cx, k * h - 0.02f, zr + d / 2 - 0.01f), new Vector3(w, 0.04f, d + 0.02f), oak, false);
                Box(g, $"Nẹp chống trơn {k}", new Vector3(cx, k * h + 0.001f, zr + 0.02f), new Vector3(w - 0.1f, 0.003f, 0.04f), nosing, false);
            }
            // Mặt phẳng qua chân các cổ bậc: dốc va chạm và kết cấu đỡ chạy theo mặt này
            float run = StairZ1 - StairZ0, len = Mathf.Sqrt(run * run + F2_Y * F2_Y), ang = Mathf.Atan2(F2_Y, run) * Mathf.Rad2Deg;
            var rot = Quaternion.Euler(-ang, 0, 0);
            Vector3 normal = rot * Vector3.up, mid = new Vector3(cx, F2_Y / 2, (StairZ0 + StairZ1) / 2);
            // dốc va chạm ẩn: người chơi đi lên êm, không vấp từng bậc (bậc chỉ để nhìn)
            var ramp = new GameObject("Dốc va chạm thang bộ");
            ramp.transform.SetParent(g, false);
            ramp.transform.localPosition = mid - normal * 0.15f;
            ramp.transform.localRotation = rot;
            ramp.AddComponent<BoxCollider>().size = new Vector3(w, 0.3f, len);
            // bản thang bê tông + mặt dưới trát phẳng, hai dầm thang
            Box(g, "Bản thang", mid - normal * 0.2f, new Vector3(w, 0.16f, len), paint, false, new Vector3(-ang, 0, 0));
            foreach (float x in new[] { StairX0 + 0.03f, StairX1 - 0.03f })
                Box(g, "Dầm thang", new Vector3(x, mid.y, mid.z) - normal * 0.08f, new Vector3(0.06f, 0.36f, len), darkMetal, false, new Vector3(-ang, 0, 0));
            // vách gỗ kín gầm thang phía tây (tầng 1 không đi vào gầm thang thấp)
            var under = Group("Vách gầm thang", g, new Vector3(StairX0 - 0.03f, 0, 0));
            Triangle(under, "Vách gỗ gầm thang", "T2_VachGamThang", new Vector2(StairZ0 + 0.55f, 0f), new Vector2(StairZ1, 0f), new Vector2(StairZ1, F2_Y - 0.42f), 0.03f, oak);

            // Lan can kính phía tây vế thang, tay vịn gỗ cao 0,9 m trên mũi bậc; tay vịn inox áp tường phía đông
            var rail = Group("Lan can thang", g);
            Vector3 railMid = new Vector3(StairX0 - 0.03f, F2_Y / 2 + 0.55f, (StairZ0 + StairZ1) / 2);
            NoShadow(Box(rail, "Kính lan can", railMid, new Vector3(0.012f, 0.85f, len), windowGlass, true, new Vector3(-ang, 0, 0)));
            Box(rail, "Tay vịn gỗ", railMid + Vector3.up * 0.47f, new Vector3(0.07f, 0.05f, len), oak, false, new Vector3(-ang, 0, 0));
            for (int k = 0; k <= StairRisers; k += 4)
            {
                float z = StairZ0 + k * d, y = Mathf.Min(k, StairRisers - 1) * h;
                Box(rail, "Trụ lan can", new Vector3(StairX0 - 0.03f, y + 0.5f, z), new Vector3(0.04f, 1.0f, 0.04f), t2LiftSteel, false);
            }
            Box(rail, "Tay vịn tường", new Vector3(StairX1 - 0.05f, F2_Y / 2 + 0.9f, mid.z), new Vector3(0.04f, 0.04f, len), t2LiftSteel, false, new Vector3(-ang, 0, 0));

            // Lan can bảo vệ lỗ thang ở tầng 2 (mép tây và mép nam lỗ)
            var top = Group("Lan can lỗ thang tầng 2", g, new Vector3(0, F2_Y, 0));
            NoShadow(Box(top, "Kính lan can tây", new Vector3(ShaftX0, 0.55f, StairZ1 / 2), new Vector3(0.012f, 1.0f, StairZ1), windowGlass));
            Box(top, "Tay vịn tây", new Vector3(ShaftX0, 1.08f, StairZ1 / 2), new Vector3(0.07f, 0.05f, StairZ1), oak, false);
            NoShadow(Box(top, "Kính lan can nam", new Vector3((ShaftX0 + StairX1) / 2, 0.55f, 0.02f), new Vector3(StairX1 - ShaftX0, 1.0f, 0.012f), windowGlass));
            Box(top, "Tay vịn nam", new Vector3((ShaftX0 + StairX1) / 2, 1.08f, 0.02f), new Vector3(StairX1 - ShaftX0, 0.05f, 0.07f), oak, false);
            Box(top, "Viền mép lỗ thang", new Vector3(ShaftX0 + 0.04f, -0.05f, StairZ1 / 2), new Vector3(0.08f, 0.12f, StairZ1), t2LiftSteel, false);
        }

        // ---------------------------------------------------------------------
        // Giếng thang máy (phần tĩnh)
        // ---------------------------------------------------------------------

        static void BuildLiftShaft(Transform core)
        {
            var g = Group("Giếng thang máy", core);
            const float wx0 = 28.4f, wx1 = ShaftX0, top = F2_Y + F2_H;
            float wx = (wx0 + wx1) / 2;
            // tường mặt tiền giếng (ốp inox phía sảnh), mỗi tầng một ô cửa 0,8 × 2,1 m
            Box(g, "Tường mặt giếng (nam)", new Vector3(wx, (top - 0.4f) / 2, (ShaftZ0 + LiftDoorZ0) / 2), new Vector3(wx1 - wx0, top + 0.4f, LiftDoorZ0 - ShaftZ0), t2LiftSteel);
            Box(g, "Tường mặt giếng (bắc)", new Vector3(wx, (top - 0.4f) / 2, (LiftDoorZ1 + ShaftZ1) / 2), new Vector3(wx1 - wx0, top + 0.4f, ShaftZ1 - LiftDoorZ1), t2LiftSteel);
            Box(g, "Bậu hố thang", new Vector3(wx, -0.2f, (LiftDoorZ0 + LiftDoorZ1) / 2), new Vector3(wx1 - wx0, 0.4f, LiftDoorZ1 - LiftDoorZ0), t2LiftSteel);
            Box(g, "Đầu cửa tầng 1", new Vector3(wx, (LiftDoorH + F2_Y) / 2, (LiftDoorZ0 + LiftDoorZ1) / 2), new Vector3(wx1 - wx0, F2_Y - LiftDoorH, LiftDoorZ1 - LiftDoorZ0), t2LiftSteel);
            Box(g, "Đầu cửa tầng 2", new Vector3(wx, (F2_Y + LiftDoorH + top) / 2, (LiftDoorZ0 + LiftDoorZ1) / 2), new Vector3(wx1 - wx0, top - F2_Y - LiftDoorH, LiftDoorZ1 - LiftDoorZ0), t2LiftSteel);
            Box(g, "Tường bắc giếng", new Vector3((wx0 + ShaftX1) / 2, (top - 0.4f) / 2, ShaftZ1 + 0.05f), new Vector3(ShaftX1 - wx0, top + 0.4f, 0.1f), paint);
            // khung cửa inox nhô 3 cm ở mỗi tầng
            foreach (float fy in new[] { 0f, F2_Y })
            {
                var f = Group(fy == 0 ? "Khung cửa thang tầng 1" : "Khung cửa thang tầng 2", g, new Vector3(wx0 - 0.015f, fy, 0));
                Box(f, "Khung trái", new Vector3(0, LiftDoorH / 2, LiftDoorZ0 - 0.04f), new Vector3(0.03f, LiftDoorH, 0.08f), t2LiftSteel, false);
                Box(f, "Khung phải", new Vector3(0, LiftDoorH / 2, LiftDoorZ1 + 0.04f), new Vector3(0.03f, LiftDoorH, 0.08f), t2LiftSteel, false);
                Box(f, "Khung trên", new Vector3(0, LiftDoorH + 0.04f, (LiftDoorZ0 + LiftDoorZ1) / 2), new Vector3(0.03f, 0.08f, LiftDoorZ1 - LiftDoorZ0 + 0.16f), t2LiftSteel, false);
                Box(f, "Ngưỡng cửa", new Vector3(0.05f, 0.004f, (LiftDoorZ0 + LiftDoorZ1) / 2), new Vector3(0.12f, 0.008f, LiftDoorZ1 - LiftDoorZ0), darkMetal, false);
                Label(f, fy == 0 ? "1" : "2", new Vector3(-0.002f, LiftDoorH + 0.62f, -1.98f), Vector3.left, 0.3f, new Color(0.15f, 0.45f, 0.3f));
            }
            // ray dẫn hướng trong giếng
            foreach (float z in new[] { ShaftZ0 + 0.05f, ShaftZ1 - 0.05f })
                Box(g, "Ray dẫn hướng", new Vector3(ShaftX1 - 0.04f, (top - 0.4f) / 2, z), new Vector3(0.05f, top + 0.4f, 0.05f), darkMetal, false);
        }

        // ---------------------------------------------------------------------
        // Thang máy chạy thật (cabin, cửa tầng, cửa cabin, nút, màn hình)
        // ---------------------------------------------------------------------

        static void BuildElevator(Transform root)
        {
            var g = Group("Thang máy", root);
            var ctrl = g.gameObject.AddComponent<ElevatorController>();
            float cz = (LiftDoorZ0 + LiftDoorZ1) / 2, cxCab = (ShaftX0 + ShaftX1) / 2;
            const float cw = 1.24f, ch = 2.4f, leafW = 0.41f;
            var screenMat = Mat("T2_ManHinhThangMay", new Color(0.02f, 0.03f, 0.05f), 0.85f, 0, emission: new Color(0.02f, 0.05f, 0.08f));
            var cabinLight = Mat("T2_DenCabin", Color.white, 0.5f, 0, emission: new Color(2.2f, 2.15f, 2.0f));
            var mirror = Mat("T2_GuongCabin", new Color(0.55f, 0.58f, 0.6f), 0.95f, 1f);

            // --- Cabin ---
            var cabin = Group("Cabin", g, new Vector3(cxCab, 0, cz));
            var rb = cabin.gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.None;
            float hw = cw / 2;
            Box(cabin, "Sàn cabin", new Vector3(0, -0.04f, 0), new Vector3(cw, 0.08f, cw), t2LiftGranite);
            Box(cabin, "Vách sau", new Vector3(hw - 0.02f, ch / 2, 0), new Vector3(0.04f, ch, cw), t2LiftSteel);
            Box(cabin, "Gương vách sau", new Vector3(hw - 0.045f, 1.45f, 0), new Vector3(0.01f, 1.1f, cw - 0.3f), mirror, false);
            foreach (int s in new[] { -1, 1 })
            {
                Box(cabin, "Vách bên", new Vector3(0, ch / 2, s * (hw - 0.02f)), new Vector3(cw, ch, 0.04f), t2LiftSteel);
                Box(cabin, "Ốp gỗ vách bên", new Vector3(0.05f, 1.05f, s * (hw - 0.045f)), new Vector3(cw - 0.25f, 1.0f, 0.01f), oak, false);
                Box(cabin, "Tay vịn", new Vector3(0.05f, 0.92f, s * (hw - 0.09f)), new Vector3(cw - 0.35f, 0.035f, 0.035f), t2LiftSteel, false);
                Box(cabin, "Vách trước", new Vector3(-hw + 0.02f, ch / 2, s * (0.4f + (hw - 0.4f) / 2)), new Vector3(0.04f, ch, hw - 0.4f), t2LiftSteel);
            }
            Box(cabin, "Đầu cửa cabin", new Vector3(-hw + 0.02f, (LiftDoorH + ch) / 2, 0), new Vector3(0.04f, ch - LiftDoorH, 0.8f), t2LiftSteel);
            Box(cabin, "Trần cabin", new Vector3(0, ch + 0.02f, 0), new Vector3(cw, 0.04f, cw), t2LiftSteel);
            NoShadow(Box(cabin, "Đèn trần cabin", new Vector3(0, ch - 0.005f, 0), new Vector3(cw - 0.3f, 0.01f, cw - 0.3f), cabinLight, false));
            var lamp = PointLight(cabin, "Đèn cabin", new Vector3(0, ch - 0.25f, 0), 2.6f, 1.1f, new Color(1f, 0.97f, 0.92f));
            lamp.shadows = LightShadows.None;

            // màn hình trong cabin (trên cửa, nhìn vào trong) + bảng nút bên phải cửa
            var disp = Group("Màn hình cabin", cabin, new Vector3(-hw + 0.045f, 2.25f, 0));
            Box(disp, "Mặt màn hình", Vector3.zero, new Vector3(0.012f, 0.22f, 0.7f), screenMat, false);
            var dFloor = Label(disp, "1", new Vector3(0.008f, 0.02f, -0.22f), Vector3.right, 0.15f, new Color(1f, 0.62f, 0.15f)).GetComponent<TextMesh>();
            var dStatus = Label(disp, "TẦNG 1", new Vector3(0.008f, 0.02f, 0.13f), Vector3.right, 0.028f, new Color(0.85f, 0.95f, 1f)).GetComponent<TextMesh>();
            var panel = Group("Bảng nút cabin", cabin, new Vector3(-hw + 0.25f, 1.15f, hw - 0.06f));
            Box(panel, "Mặt bảng nút", Vector3.zero, new Vector3(0.18f, 0.5f, 0.012f), t2LiftSteel, false);
            var miniFloor = Label(panel, "1", new Vector3(0, 0.18f, -0.008f), Vector3.back, 0.05f, new Color(1f, 0.62f, 0.15f)).GetComponent<TextMesh>();
            Box(panel, "Ô số tầng", new Vector3(0, 0.18f, -0.006f), new Vector3(0.12f, 0.08f, 0.002f), screenMat, false);
            ElevatorButtonAt(panel, ctrl, ElevatorButton.Kind.Floor2, "2", new Vector3(0, 0.05f, -0.01f), Vector3.back);
            ElevatorButtonAt(panel, ctrl, ElevatorButton.Kind.Floor1, "1", new Vector3(0, -0.06f, -0.01f), Vector3.back);
            ElevatorButtonAt(panel, ctrl, ElevatorButton.Kind.Open, "◄►", new Vector3(-0.045f, -0.17f, -0.01f), Vector3.back);
            ElevatorButtonAt(panel, ctrl, ElevatorButton.Kind.Close, "►◄", new Vector3(0.045f, -0.17f, -0.01f), Vector3.back);
            Label(panel, "KHU SINH HỌC", new Vector3(0, 0.235f, -0.008f), Vector3.back, 0.014f, new Color(0.2f, 0.25f, 0.28f));

            // cửa cabin: 2 cánh trượt ra hai bên, nằm sau vách trước
            var cabLeaves = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                int s = i == 0 ? -1 : 1;
                var leaf = Box(cabin, i == 0 ? "Cánh cửa cabin nam" : "Cánh cửa cabin bắc", new Vector3(-hw + 0.06f, LiftDoorH / 2, s * leafW / 2), new Vector3(0.025f, LiftDoorH, leafW), t2LiftSteel);
                cabLeaves[i] = leaf.transform;
            }

            // --- Cửa tầng, nút gọi, bảng số tầng (mỗi tầng) ---
            var landLeaves = new Transform[4];
            var landDisplays = new TextMesh[2];
            for (int f = 0; f < 2; f++)
            {
                float fy = f == 0 ? 0f : F2_Y;
                var land = Group(f == 0 ? "Cửa tầng 1" : "Cửa tầng 2", g, new Vector3(28.45f, fy, cz));
                for (int i = 0; i < 2; i++)
                {
                    int s = i == 0 ? -1 : 1;
                    var leaf = Box(land, i == 0 ? "Cánh cửa tầng nam" : "Cánh cửa tầng bắc", new Vector3(0, LiftDoorH / 2, s * leafW / 2), new Vector3(0.03f, LiftDoorH, leafW), t2LiftSteel);
                    landLeaves[f * 2 + i] = leaf.transform;
                }
                var ind = Group("Bảng số tầng", land, new Vector3(-0.07f, LiftDoorH + 0.3f, 0));
                Box(ind, "Mặt bảng", Vector3.zero, new Vector3(0.012f, 0.16f, 0.36f), screenMat, false);
                landDisplays[f] = Label(ind, "1", new Vector3(-0.008f, 0, 0), Vector3.left, 0.11f, new Color(1f, 0.62f, 0.15f)).GetComponent<TextMesh>();
                var call = Group("Nút gọi thang", land, new Vector3(-0.07f, 1.1f, LiftDoorZ1 - cz + 0.14f));
                Box(call, "Mặt nút gọi", Vector3.zero, new Vector3(0.012f, 0.2f, 0.1f), t2LiftSteel, false);
                ElevatorButtonAt(call, ctrl, f == 0 ? ElevatorButton.Kind.Call1 : ElevatorButton.Kind.Call2, f == 0 ? "▲" : "▼", new Vector3(-0.01f, 0, 0), Vector3.left);
            }

            ctrl.cabin = cabin;
            ctrl.floorY = new[] { 0f, F2_Y };
            ctrl.cabinLeaves = cabLeaves;
            ctrl.landingLeaves = landLeaves;
            ctrl.leafTravel = leafW;
            ctrl.cabinFloorDisplay = dFloor;
            ctrl.cabinStatusDisplay = dStatus;
            ctrl.cabinPanelDisplay = miniFloor;
            ctrl.landingDisplays = landDisplays;
            ctrl.cabinHalfSize = new Vector2(hw - 0.05f, hw - 0.05f);
            ctrl.doorwayX = new Vector2(28.25f, 28.7f);
            ctrl.doorwayZ = new Vector2(LiftDoorZ0, LiftDoorZ1);
            ctrl.audioSource = cabin.gameObject.AddComponent<AudioSource>();
            ctrl.audioSource.playOnAwake = false;
            ctrl.audioSource.spatialBlend = 1f;
            ctrl.audioSource.maxDistance = 12f;
            Info(g, "Thang máy (2 điểm dừng)", "Thang máy chở người giữa tầng 1 và tầng 2 (khu sinh học). Tải trọng 630 kg (8 người), tốc độ 1 m/s.",
                "Bấm nút gọi ▲ / ▼ cạnh cửa, bước vào cabin rồi bấm số tầng trên bảng nút bên phải cửa.");
        }

        static void ElevatorButtonAt(Transform p, ElevatorController ctrl, ElevatorButton.Kind kind, string text, Vector3 pos, Vector3 facing)
        {
            var b = Cyl(p, "Nút " + text, pos, 0.022f, 0.012f, Mat("T2_NutThangMay", new Color(0.86f, 0.87f, 0.88f), 0.7f, 0.8f), true,
                        Mathf.Abs(facing.x) > 0.5f ? new Vector3(0, 0, 90) : new Vector3(90, 0, 0));
            var btn = b.AddComponent<ElevatorButton>();
            btn.controller = ctrl;
            btn.kind = kind;
            var ring = Mat("T2_VienNutSang", new Color(1f, 0.8f, 0.4f), 0.5f, 0, emission: new Color(1.2f, 0.75f, 0.25f));
            btn.lamp = b.GetComponent<Renderer>();
            btn.litMaterial = ring;
            Label(p, text, pos + facing * 0.008f, facing, 0.018f, new Color(0.15f, 0.16f, 0.18f));
        }

        // ---------------------------------------------------------------------
        // Đồ trong lõi thang: đèn, biển chỉ dẫn, ghế chờ, cây xanh
        // ---------------------------------------------------------------------

        static void CoreFurnishing(Transform core)
        {
            var f1 = Group("Sảnh thang tầng 1", core);
            var f2 = Group("Sảnh thang tầng 2", core, new Vector3(0, F2_Y, 0));
            LedPanel(f1, "Đèn sảnh thang", 27.75f, -1.1f, 1.8f, 6f);
            LedPanel(f1, "Đèn dưới lối đi", 27.75f, 3.0f, 1.6f, 6f);
            LedPanel(f2, "Đèn sảnh thang", 27.75f, -1.1f, 1.8f, 6f);
            LedPanel(f2, "Đèn lối đi", 27.75f, 3.0f, 1.8f, 6f);
            // đèn thả trên lỗ thang (một đèn cho cả vế thang)
            var pend = Group("Đèn thả lỗ thang", core, new Vector3(29.2f, F2_Y + F2_H, 3.0f));
            Box(pend, "Dây treo", new Vector3(0, -0.4f, 0), new Vector3(0.006f, 0.8f, 0.006f), darkMetal, false);
            NoShadow(Box(pend, "Đèn ống", new Vector3(0, -0.82f, 0), new Vector3(0.06f, 0.05f, 2.4f), tubeGlow, false));
            PointLight(pend, "Đèn", new Vector3(0, -1.2f, 0), 7f, 2.0f, new Color(1f, 0.96f, 0.9f));

            // biển chỉ dẫn
            T2Sign(f1, "LÊN TẦNG 2 · KHU SINH HỌC", "UP TO LEVEL 2 · BIOLOGY", new Vector3(27.13f, 2.45f, -1.1f), Vector3.right, zoneBioLab);
            T2Sign(f2, "TẦNG 2 · KHU SINH HỌC", "LEVEL 2 · BIOLOGY WING", new Vector3(27.13f, 2.45f, -1.1f), Vector3.right, zoneBioLab);
            T2Sign(f2, "← KHO SINH PHẨM   ·   PHÒNG VI SINH →", "BIO STORE · MICROBIOLOGY", new Vector3(27.13f, 2.45f, 2.1f), Vector3.right, zoneMicroBio);
            // ghế chờ + cây xanh dưới lối đi tầng 2 (tầng 1)
            var bench = Group("Ghế chờ", f1, new Vector3(27.45f, 0, 2.6f));
            Box(bench, "Mặt ghế", new Vector3(0, 0.44f, 0), new Vector3(0.42f, 0.05f, 1.6f), oak);
            foreach (float z in new[] { -0.7f, 0.7f })
                Box(bench, "Chân ghế", new Vector3(0, 0.21f, z), new Vector3(0.38f, 0.42f, 0.05f), darkMetal, false);
            ModernPlant(f1, new Vector3(27.5f, 0, 4.9f));
            ModernPlant(f2, new Vector3(27.5f, 0, 5.2f), 0.9f);
        }

        /// <summary>Biển chỉ dẫn treo tường song ngữ, nền màu khu, chữ trắng. facing: hướng mặt biển.</summary>
        static void T2Sign(Transform p, string vi, string en, Vector3 pos, Vector3 facing, Color color)
        {
            float yaw = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
            var g = Group("Biển " + vi, p, pos, yaw);
            Box(g, "Nền biển", new Vector3(0, 0, 0.01f), new Vector3(2.0f, 0.34f, 0.02f), Mat("T2_Bien" + ColorUtility.ToHtmlStringRGB(color), color * 0.85f, 0.4f), false);
            Label(g, vi, new Vector3(0, 0.05f, 0.022f), Vector3.forward, 0.065f, Color.white);
            Label(g, en, new Vector3(0, -0.085f, 0.022f), Vector3.forward, 0.035f, new Color(1f, 1f, 1f, 0.85f));
        }
    }
}
