using System.Linq;
using Khkt.Assembly;
using Khkt.Demo;
using Khkt.Environment;
using Khkt.Interaction;
using UnityEngine;
using static Khkt.EditorTools.LabKit;

namespace Khkt.EditorTools
{
    /// <summary>
    /// Giai đoạn C1: bàn lắp ráp tự do ở khu lắp ráp & tổng hợp với bộ dụng cụ rời lắp được: giá sắt, kẹp càng cua, vòng sắt,
    /// bình cầu, bình nón, ống nghiệm, đầu nối chưng cất, nhiệt kế, ống sinh hàn, đầu nối hứng, nút cao su, ống dẫn thủy tinh,
    /// ống cao su, phễu chiết, phễu lọc; vòi cấp nước và ống thoát gắn ở vách bàn để nối nước làm lạnh sinh hàn.
    /// Mỗi bộ phận có các khớp (Port) đặt đúng chỗ, hướng z chỉ ra ngoài theo hướng nối.
    /// </summary>
    public static partial class LabSceneBuilder
    {
        public static readonly Vector3 AssemblyBenchPos = new Vector3(15.25f, 0, 11.3f);

        // ---------- Tiện ích ----------

        static Port AddPort(Transform part, string label, PortType type, float size, Vector3 localPos, Vector3 forward, Vector3 up,
                            int channel = 0, int group = -1)
        {
            var t = new GameObject("Khớp: " + label).transform;
            t.SetParent(part, false);
            t.localPosition = localPos;
            t.localRotation = Quaternion.LookRotation(forward, up);
            var p = t.gameObject.AddComponent<Port>();
            p.type = type;
            p.size = size;
            p.label = label;
            p.channel = channel;
            p.exclusiveGroup = group;
            return p;
        }

        static ApparatusPart MakePart(Transform root, string name, Vector2 baseSize, string desc, string usage, params Port[] ports)
        {
            var ap = root.gameObject.AddComponent<ApparatusPart>();
            ap.partName = name;
            ap.ports = ports;
            ap.baseSize = baseSize;
            Info(root, name, desc, usage);
            return ap;
        }

        static Grabbable MakeGrab(Transform root, string kind, float grip, float massG, bool lies = false, float lengthM = 0.2f, float lyingR = 0.01f)
        {
            var g = root.gameObject.GetComponent<Grabbable>();
            if (g == null) g = root.gameObject.AddComponent<Grabbable>();
            g.kind = kind;
            g.gripHeightM = grip;
            g.massG = massG;
            g.liesOnTable = lies;
            g.lengthM = lengthM;
            g.lyingRadiusM = lyingR;
            return g;
        }

        /// <summary>Đặt bộ phận dài (trục +y cục bộ) nằm ngang trên mặt bàn: trục dọc theo +x thế giới, tâm ở center.</summary>
        static void LayFlat(Transform part, Vector3 center, float length, float radius)
        {
            // trục +y cục bộ → +x thế giới, trục +x cục bộ (vòi nước, nhánh) → hướng lên
            part.localRotation = Quaternion.LookRotation(Vector3.back, Vector3.right);
            part.localPosition = center + new Vector3(-length / 2, radius, 0);
        }

        static GameObject Tube(Transform p, string name, Vector3 a, Vector3 b, float r, Material m, bool collider = false)
        {
            var d = b - a;
            var go = Cyl(p, name, (a + b) / 2, r, d.magnitude, m, collider);
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
            return go;
        }

        // =====================================================================
        // Bàn lắp ráp tự do
        // =====================================================================

        static void AssemblyBench(Transform lab)
        {
            var g = Group("Bàn thí nghiệm lắp ráp", lab, AssemblyBenchPos);
            const float lx = 2.4f, lz = 0.9f, top = 0.9f, shelfY = top + 0.4f;
            Box(g, "Chân đế", new Vector3(0, 0.04f, 0), new Vector3(lx - 0.1f, 0.08f, lz - 0.1f), darkMetal);
            Box(g, "Tủ dưới", new Vector3(0, 0.46f, 0.02f), new Vector3(lx - 0.04f, 0.76f, lz - 0.08f), cabinet);
            for (int i = 0; i < 3; i++)
                Box(g, "Cánh tủ", new Vector3(-lx / 2 + 0.4f + i * 0.8f, 0.46f, -lz / 2 + 0.035f), new Vector3(0.76f, 0.68f, 0.012f), cabinetDoor, false);
            Box(g, "Mặt bàn", new Vector3(0, top - 0.02f, 0), new Vector3(lx, 0.04f, lz), epoxy);  // AttachSurfaces gắn PlaceSurface
            Box(g, "Vách sau", new Vector3(0, top + 0.3f, lz / 2 - 0.02f), new Vector3(lx, 0.6f, 0.04f), panelGrey);
            var shelf = Box(g, "Tầng kệ", new Vector3(0, shelfY - 0.01f, lz / 2 - 0.13f), new Vector3(lx - 0.04f, 0.02f, 0.22f), oak);
            shelf.AddComponent<PlaceSurface>();
            Box(g, "Dải màu vách", new Vector3(0, top + 0.56f, lz / 2 - 0.042f), new Vector3(lx, 0.05f, 0.004f), accent, false);
            Label(g, "BÀN THÍ NGHIỆM LẮP RÁP · ASSEMBLY BENCH", new Vector3(0, top + 0.56f, lz / 2 - 0.046f), Vector3.back, 0.038f, Color.white);
            Label(g, "Bộ dụng cụ gia công ở khu chế tạo đặt ra đây · Nối đầu ống cao su vào vòi nước, vòi hút chân không",
                  new Vector3(0, top + 0.47f, lz / 2 - 0.042f), Vector3.back, 0.02f, new Color(0.15f, 0.2f, 0.25f));
            Info(g, "Bàn thí nghiệm lắp ráp",
                 "Chỗ đặt bộ dụng cụ gia công từ khu chế tạo (chưng cất, đun hồi lưu, điều chế khí, lọc hút...). Vách sau có vòi cấp nước, ống thoát nước, vòi hút chân không.",
                 "Nối đầu ống cao su còn trống vào vòi: nước vào vòi dưới của ống sinh hàn, ra ở vòi trên.");

            // Vòi gắn cố định ở vách sau (dưới tầng kệ): hai cặp nước làm lạnh, một vòi hút chân không
            float fy = top + 0.18f, fz = lz / 2 - 0.04f;
            Fixture(g, "Vòi cấp nước 1", new Vector3(-0.75f, fy, fz), new Color(0.15f, 0.45f, 0.9f), "Vòi cấp nước 1 (đầu nối ống cao su)");
            Fixture(g, "Ống thoát nước 1", new Vector3(-0.63f, fy, fz), new Color(0.35f, 0.38f, 0.42f), "Ống thoát nước 1 (đầu nối ống cao su)");
            Fixture(g, "Vòi cấp nước 2", new Vector3(0.15f, fy, fz), new Color(0.15f, 0.45f, 0.9f), "Vòi cấp nước 2 (đầu nối ống cao su)");
            Fixture(g, "Ống thoát nước 2", new Vector3(0.27f, fy, fz), new Color(0.35f, 0.38f, 0.42f), "Ống thoát nước 2 (đầu nối ống cao su)");
            Fixture(g, "Vòi hút chân không", new Vector3(0.75f, fy, fz), new Color(0.9f, 0.55f, 0.1f), "Vòi hút chân không (đầu nối ống cao su)");
            Destination(g, "Bàn thí nghiệm lắp ráp (có vòi nước, vòi hút)", 2, new Vector3(0, top, -0.03f), new Vector2(2.3f, 0.8f), Vector3.back);
        }
        static void Fixture(Transform p, string name, Vector3 pos, Color color, string label)
        {
            var g = Group(name, p, pos);
            Box(g, "Đế vòi", new Vector3(0, 0, 0.0f), new Vector3(0.06f, 0.06f, 0.03f), Mat("DeVoi" + ColorUtility.ToHtmlStringRGB(color), color, 0.4f));
            Cyl(g, "Đầu nối", new Vector3(0, 0, -0.035f), 0.004f, 0.04f, steel, false, new Vector3(90, 0, 0));
            var port = AddPort(g, label, PortType.VoiNhanh, 8f, new Vector3(0, 0, -0.055f), Vector3.back, Vector3.up);
            var ap = g.gameObject.AddComponent<ApparatusPart>();
            ap.partName = name;
            ap.ports = new[] { port };
            ap.fixture = true;
            Info(g, name, name.Contains("chân không") ? "Nối ống cao su từ nhánh bình hút lọc vào đây để hút chân không khi lọc nhanh."
                                                    : "Nối ống cao su vào đây để dẫn nước làm lạnh ống sinh hàn (nước vào ở vòi dưới, ra ở vòi trên).");
        }

        // =====================================================================
        // Các bộ phận
        // =====================================================================

        static void StandPart(Transform p, string name, Vector3 pos)
        {
            var g = Group(name, p, pos, 180f);  // xoay 180°: kẹp vươn về phía mép trước bàn (người đứng)
            Box(g, "Đế giá sắt", new Vector3(0, 0.007f, 0), new Vector3(0.16f, 0.014f, 0.26f), darkMetal);
            Cyl(g, "Cọc giá sắt", new Vector3(0, 0.35f, -0.09f), 0.006f, 0.7f, steel, true);
            Cyl(g, "Núm cọc", new Vector3(0, 0.705f, -0.09f), 0.007f, 0.01f, darkMetal);
            var ports = new System.Collections.Generic.List<Port>();
            int grp = 0;
            for (float h = 0.1f; h <= 0.651f; h += 0.05f, grp++)
                foreach (var (dir, side) in new[] { (Vector3.forward, "trước"), (Vector3.right, "phải"), (Vector3.left, "trái"), (Vector3.back, "sau") })
                    ports.Add(AddPort(g, $"Cọc giá ở {h * 100:0} cm ({side})", PortType.ThanhGia, 0, new Vector3(0, h, -0.09f), dir, Vector3.up, -1, grp));
            MakeGrab(g, "gia_sat", 0.45f, 1200f);
            MakePart(g, name, new Vector2(0.16f, 0.26f), "Đế gang nặng và cọc thép: gắn kẹp, vòng sắt ở độ cao bất kỳ.",
                     "Kẹp vươn dọc theo đế (hướng trước) thì vững; vươn ngang, treo nặng ở xa thì dễ đổ.", ports.ToArray());
        }

        static void ClampPart(Transform p, string name, Vector3 pos)
        {
            var g = Group(name, p, pos, 90f);
            const float yb = 0.0175f;
            Box(g, "Khớp nối kẹp", new Vector3(0, yb, 0), new Vector3(0.03f, 0.035f, 0.03f), darkMetal);
            Cyl(g, "Vít khớp", new Vector3(0.022f, yb, 0), 0.005f, 0.02f, steel, false, new Vector3(0, 0, 90));
            Box(g, "Tay kẹp", new Vector3(0, yb, -0.08f), new Vector3(0.012f, 0.012f, 0.13f), darkMetal, false);
            var jaw = Mat("MaKepDo", new Color(0.65f, 0.15f, 0.1f), 0.3f);
            foreach (float s in new[] { -1f, 1f })
                Box(g, "Má kẹp", new Vector3(s * 0.022f, yb, -0.158f), new Vector3(0.008f, 0.024f, 0.04f), jaw, false);
            Box(g, "Thanh nối má", new Vector3(0, yb, -0.142f), new Vector3(0.05f, 0.01f, 0.008f), darkMetal, false);
            MakeGrab(g, "kep", 0.02f, 250f);
            MakePart(g, name, Vector2.zero, "Kẹp càng cua có khớp nối: lồng khớp vào cọc giá sắt, má kẹp giữ cổ bình hoặc thân ống.",
                     "Kẹp vào cổ bình (chỗ hẹp), không kẹp vào bầu bình. Xoay kẹp (lăn chuột) để chọn hướng vươn.",
                     AddPort(g, "Khớp nối kẹp", PortType.KhopNoiKep, 0, new Vector3(0, yb, 0), Vector3.forward, Vector3.up, -1),
                     AddPort(g, "Má kẹp", PortType.MaKep, 40f, new Vector3(0, yb, -0.158f), Vector3.back, Vector3.up, -1));
        }

        static void RingPart(Transform p, string name, Vector3 pos)
        {
            var g = Group(name, p, pos, 90f);
            const float yb = 0.0175f;
            Box(g, "Khớp nối vòng", new Vector3(0, yb, 0), new Vector3(0.03f, 0.035f, 0.03f), darkMetal);
            Box(g, "Tay vòng", new Vector3(0, yb, -0.045f), new Vector3(0.008f, 0.008f, 0.06f), darkMetal, false);
            MeshObj(g, "Vòng", Lathe("VongSatLapRap", 28, V(0.035f, 0), V(0.041f, 0), V(0.041f, 0.006f), V(0.035f, 0.006f), V(0.035f, 0)), new Vector3(0, yb - 0.003f, -0.115f), darkMetal, true);
            MakeGrab(g, "vong_sat", 0.02f, 200f);
            MakePart(g, name, Vector2.zero, "Vòng sắt có khớp nối: đỡ phễu lọc, phễu chiết, hoặc lưới tản nhiệt.", "",
                     AddPort(g, "Khớp nối vòng", PortType.KhopNoiKep, 0, new Vector3(0, yb, 0), Vector3.forward, Vector3.up, -1),
                     AddPort(g, "Vòng đỡ", PortType.Vong, 70f, new Vector3(0, yb + 0.003f, -0.115f), Vector3.up, Vector3.back, -1));
        }

        static void RoundFlaskPart(Transform p, string name, Vector3 pos)
        {
            var c = FunctionalVessel(p, name, pos, "binh_cau");
            var t = c.transform;
            c.label = name;
            MakeGrab(t, "binh_cau_lap_rap", 0.12f, 130f);
            MakePart(t, name, new Vector2(0.05f, 0.05f), "Bình cầu đáy tròn cổ nhám 24/29: đun nóng, chưng cất, hồi lưu.",
                     "Kẹp ở cổ bình; đun qua lưới tản nhiệt hoặc bếp bọc; cho đá bọt trước khi đun.",
                     AddPort(t, "Cổ bình (khớp 24/29)", PortType.KhopNhamCai, 24f, new Vector3(0, 0.15f, 0), Vector3.up, Vector3.forward),
                     AddPort(t, "Cổ bình (chỗ kẹp)", PortType.VungKep, 26f, new Vector3(0, 0.125f, 0), Vector3.right, Vector3.up, -1),
                     AddPort(t, "Đáy bình", PortType.DayBinh, 90f, new Vector3(0, 0f, 0), Vector3.down, Vector3.forward, -1));
        }

        static void ConicalFlaskPart(Transform p, string name, Vector3 pos)
        {
            var c = FunctionalVessel(p, name, pos, "binh_non");
            var t = c.transform;
            c.label = name;
            MakeGrab(t, "binh_non", 0.11f, 120f);
            MakePart(t, name, new Vector2(0.08f, 0.08f), "Bình nón (bình tam giác): hứng sản phẩm chưng cất, điều chế khí (đậy nút có ống dẫn).", "",
                     AddPort(t, "Miệng bình nón", PortType.CoBinh, 30f, new Vector3(0, 0.143f, 0), Vector3.up, Vector3.forward),
                     AddPort(t, "Cổ bình nón (chỗ kẹp)", PortType.VungKep, 34f, new Vector3(0, 0.12f, 0), Vector3.right, Vector3.up, -1),
                     AddPort(t, "Đáy bình nón", PortType.DayPhang, 85f, Vector3.zero, Vector3.down, Vector3.forward, -1));
        }

        static ApparatusPart TestTubePart(Transform p, string name, Vector3 pos, bool big)
        {
            var c = FunctionalVessel(p, name, pos, "ong_nghiem");
            var t = c.transform;
            c.label = name;
            if (big) t.localScale = new Vector3(1.25f, 1.15f, 1.25f);  // ống nghiệm to Ø20
            MakeGrab(t, big ? "ong_nghiem_to" : "ong_nghiem", 0.11f, big ? 20f : 12f);
            // nằm nghiêng: trục ống lệch 10° dưới phương ngang, miệng hơi chúc xuống (nhiệt phân chất rắn, hơi nước không chảy ngược)
            Vector3 tiltUp = new Vector3(Mathf.Sin(100f * Mathf.Deg2Rad), Mathf.Cos(100f * Mathf.Deg2Rad), 0);
            return MakePart(t, name, new Vector2(0.02f, 0.02f),
                     big ? "Ống nghiệm to: thu khí (úp ngược trong chậu nước), nhiệt phân chất rắn (kẹp nằm nghiêng), sục khí vào dung dịch."
                         : "Ống nghiệm: làm phản ứng với lượng nhỏ hóa chất, đun nóng trên đèn cồn.",
                     "Đun: hơ đều cả ống rồi tập trung ở đáy; miệng ống hướng ra chỗ không có người. Nhiệt phân: kẹp miệng ống hơi chúc xuống.",
                     AddPort(t, "Miệng ống nghiệm", PortType.CoBinh, 17f, new Vector3(0, 0.152f, 0), Vector3.up, Vector3.forward),
                     AddPort(t, "Thân ống (kẹp đứng)", PortType.VungKep, 20f, new Vector3(0, 0.11f, 0), Vector3.right, Vector3.up, -1, 0),
                     AddPort(t, "Thân ống (kẹp úp ngược)", PortType.VungKep, 20f, new Vector3(0, 0.11f, 0), Vector3.forward, Vector3.down, -1, 0),
                     AddPort(t, "Thân ống (kẹp nghiêng, miệng sang trái)", PortType.VungKep, 20f, new Vector3(0, 0.11f, 0), Vector3.forward, tiltUp, -1, 0),
                     AddPort(t, "Thân ống (kẹp nghiêng, miệng sang phải)", PortType.VungKep, 20f, new Vector3(0, 0.11f, 0), Vector3.forward, new Vector3(-tiltUp.x, tiltUp.y, 0), -1, 0),
                     AddPort(t, "Đáy ống nghiệm", PortType.DayOng, 17f, Vector3.zero, Vector3.down, Vector3.forward, -1));
        }
        static void StillHeadPart(Transform p, string name, Vector3 pos)
        {
            var g = Group(name, p, pos);
            var body = Group("Thân", g);
            const float angle = 15f;
            Vector3 d = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), -Mathf.Sin(angle * Mathf.Deg2Rad), 0);  // nhánh về phía phải người cầm (+x)
            Vector3 arm0 = new Vector3(0, 0.085f, 0), arm1 = arm0 + d * 0.1f;
            NoShadow(Tube(body, "Thân đầu nối", new Vector3(0, 0, 0), new Vector3(0, 0.12f, 0), 0.012f, labGlass, true));
            NoShadow(Tube(body, "Nhánh đầu nối", arm0, arm1, 0.009f, labGlass, true));
            Cyl(body, "Vạch nhám dưới", new Vector3(0, 0.015f, 0), 0.0125f, 0.03f, Mat("KhopNham", new Color(0.92f, 0.93f, 0.93f, 0.7f), 0.2f, 0, transparent: true));
            MakeGrab(g, "dau_noi", 0.06f, 60f, true, 0.12f, 0.012f);
            MakePart(g, name, Vector2.zero, "Đầu nối chưng cất (3 khớp): dưới lắp bình cầu, trên lắp nhiệt kế, nhánh xiên lắp ống sinh hàn.",
                     "Bầu nhiệt kế phải ngang chỗ rẽ nhánh để đo đúng nhiệt độ hơi.",
                     AddPort(g, "Khớp dưới (24/29)", PortType.KhopNhamDuc, 24f, Vector3.zero, Vector3.down, Vector3.right),
                     AddPort(g, "Khớp trên (14/23)", PortType.KhopNhamCai, 14f, new Vector3(0, 0.12f, 0), Vector3.up, Vector3.right),
                     AddPort(g, "Nhánh xiên (24/29)", PortType.KhopNhamDuc, 24f, arm1, d, new Vector3(-d.y, d.x, 0)));
            g.localPosition = pos + new Vector3(0, 0.0125f, 0);
            g.localRotation = Quaternion.Euler(-90f, 0, 0);  // nằm trên bàn, nhánh xiên sang bên
        }

        /// <summary>Đặt bộ phận dài nằm ngang trên bàn ngay khi dựng (gốc ở một đầu, trục +y cục bộ).</summary>
        static void LayFlatRoot(Transform g, Vector3 pos, float length, float radius) => LayFlat(g, pos, length, radius);

        static void ThermometerPart(Transform p, string name, Vector3 pos)
        {
            var g = Group(name, p, pos);
            Cyl(g, "Khớp nối nhiệt kế", new Vector3(0, 0.015f, 0), 0.0085f, 0.03f, Mat("KhopNham", new Color(0.92f, 0.93f, 0.93f, 0.7f), 0.2f, 0, transparent: true), true);
            NoShadow(Tube(g, "Thân nhiệt kế", new Vector3(0, -0.035f, 0), new Vector3(0, 0.23f, 0), 0.003f, labGlass));
            Tube(g, "Cột thủy ngân", new Vector3(0, -0.035f, 0), new Vector3(0, 0.03f, 0), 0.0012f, Mat("CotNhietKe", new Color(0.85f, 0.1f, 0.1f), 0.5f));
            var bulb = Ball(g, "Bầu nhiệt kế", new Vector3(0, -0.038f, 0), Vector3.one * 0.008f, Mat("CotNhietKe", new Color(0.85f, 0.1f, 0.1f), 0.5f));
            Child(g, "Điểm đo nhiệt độ", bulb.transform.localPosition);
            MakeGrab(g, "nhiet_ke", 0.12f, 30f, true, 0.29f, 0.009f);
            MakePart(g, name, Vector2.zero, "Nhiệt kế 0–250 °C có khớp nối 14/23 cắm vào đầu nối chưng cất.",
                     "Bầu nhiệt kế ngang chỗ rẽ nhánh: đọc nhiệt độ hơi đang cất.",
                     AddPort(g, "Khớp nhiệt kế (14/23)", PortType.KhopNhamDuc, 14f, Vector3.zero, Vector3.down, Vector3.forward));
            LayFlat(g, pos + new Vector3(0.05f, 0, 0), 0.29f, 0.009f);
        }

        static void CondenserPart(Transform p, string name, Vector3 pos, bool bulbs)
        {
            var g = Group(name, p, pos);
            const float L = 0.32f;
            NoShadow(Tube(g, "Vỏ ngoài (áo nước)", new Vector3(0, 0.03f, 0), new Vector3(0, L - 0.03f, 0), 0.018f, labGlass, true));
            NoShadow(Tube(g, "Ống trong", new Vector3(0, 0, 0), new Vector3(0, L, 0), 0.0065f, labGlass));
            if (bulbs)  // ống sinh hàn bầu (Allihn): nhiều bầu trong làm tăng diện tích ngưng tụ, dùng đun hồi lưu
                for (int i = 0; i < 5; i++)
                    NoShadow(Ball(g, "Bầu ngưng tụ", new Vector3(0, 0.07f + i * 0.045f, 0), new Vector3(0.024f, 0.03f, 0.024f), labGlass));
            foreach (float yy in new[] { 0.05f, L - 0.05f })
                NoShadow(Tube(g, "Vòi nước", new Vector3(0.016f, yy, 0), new Vector3(0.036f, yy, 0), 0.004f, labGlass));
            MakeGrab(g, "sinh_han", 0.16f, 250f, true, L, 0.018f);
            MakePart(g, name, Vector2.zero, bulbs ? "Ống sinh hàn bầu (Allihn): lắp đứng trên bình cầu để đun hồi lưu (este hóa, thủy phân): hơi ngưng tụ chảy ngược về bình." : "Ống sinh hàn thẳng (Liebig): hơi đi trong ống trong, nước lạnh chảy ngược chiều trong áo ngoài.",
                     "Nước vào ở vòi DƯỚI, ra ở vòi TRÊN (ngược chiều hơi) để áo nước luôn đầy.",
                     AddPort(g, "Đầu vào hơi (24/29)", PortType.KhopNhamCai, 24f, new Vector3(0, L, 0), Vector3.up, Vector3.right),
                     AddPort(g, "Đầu ra (24/29)", PortType.KhopNhamDuc, 24f, Vector3.zero, Vector3.down, Vector3.right),
                     AddPort(g, "Vòi nước vào (dưới)", PortType.VoiNhanh, 8f, new Vector3(0.036f, 0.05f, 0), Vector3.right, Vector3.up, 1),
                     AddPort(g, "Vòi nước ra (trên)", PortType.VoiNhanh, 8f, new Vector3(0.036f, L - 0.05f, 0), Vector3.right, Vector3.up, 1));
            LayFlat(g, pos, L, 0.02f);
        }

        static void ReceiverAdapterPart(Transform p, string name, Vector3 pos)
        {
            var g = Group(name, p, pos);
            Vector3 bend = new Vector3(0, -0.06f, 0);
            Vector3 d2 = new Vector3(0, -Mathf.Cos(75f * Mathf.Deg2Rad), -Mathf.Sin(75f * Mathf.Deg2Rad)).normalized;
            Vector3 tip = bend + d2 * 0.08f;
            NoShadow(Tube(g, "Đoạn vào", Vector3.zero, bend, 0.011f, labGlass, true));
            NoShadow(Tube(g, "Đoạn cong", bend, tip, 0.007f, labGlass, true));
            var outlet = AddPort(g, "Đầu nhỏ giọt", PortType.MiengRa, 6f, tip, d2, Vector3.Cross(d2, Vector3.right).normalized);
            MakeGrab(g, "dau_noi_hung", 0.04f, 50f, true, 0.12f, 0.011f);
            var ap = MakePart(g, name, Vector2.zero, "Đầu nối hứng (cong): nối đầu ra ống sinh hàn, cho sản phẩm nhỏ giọt vào bình hứng.",
                              "Đặt bình hứng ngay dưới đầu nhỏ giọt.",
                              AddPort(g, "Khớp vào (24/29)", PortType.KhopNhamCai, 24f, Vector3.zero, Vector3.up, Vector3.forward), outlet);
            ap.dripAxis = outlet.transform;
            LayFlat(g, pos, 0.1f, 0.012f);
        }

        static void StopperPart(Transform p, string name, Vector3 pos, float size, int holes)
        {
            var g = Group(name, p, pos);
            float rb = size * 0.0005f * 0.85f, rt = size * 0.0005f * 1.12f;
            MeshObj(g, "Nút cao su", Lathe("NutCaoSu" + size, 20, V(0, 0), V(rb, 0), V(rt, 0.025f), V(0, 0.025f)), Vector3.zero, Mat("CaoSuNau", new Color(0.36f, 0.22f, 0.15f), 0.2f), true);
            float hx = holes == 2 ? 0.0075f : 0f;
            for (int h = 0; h < holes; h++) Cyl(g, "Lỗ nút", new Vector3(h == 0 ? -hx : hx, 0.0253f, 0), 0.0035f, 0.001f, rubber);
            MakeGrab(g, "nut", 0.02f, 15f);
            var list = new System.Collections.Generic.List<Port> { AddPort(g, "Thân nút", PortType.NutCaoSu, size, new Vector3(0, 0.016f, 0), Vector3.down, Vector3.forward) };
            if (holes == 1) list.Add(AddPort(g, "Lỗ nút", PortType.LoNut, 7f, new Vector3(0, 0.025f, 0), Vector3.up, Vector3.forward));
            if (holes == 2)
            {
                list.Add(AddPort(g, "Lỗ nút 1", PortType.LoNut, 7f, new Vector3(-hx, 0.025f, 0), Vector3.up, Vector3.forward));
                list.Add(AddPort(g, "Lỗ nút 2", PortType.LoNut, 7f, new Vector3(hx, 0.025f, 0), Vector3.up, Vector3.forward));
            }
            var ports = list.ToArray();
            MakePart(g, name, Vector2.zero, holes == 2 ? "Nút cao su hai lỗ: một lỗ cắm phễu nhỏ giọt / ống dẫn khí vào, một lỗ cắm ống dẫn khí ra."
                                          : holes == 1 ? "Nút cao su một lỗ: đậy miệng bình, cắm ống dẫn thủy tinh qua lỗ để dẫn khí." : "Nút cao su đặc: đậy kín miệng ống.",
                     holes > 0 ? "Thấm ướt đầu ống thủy tinh rồi xoay nhẹ khi cắm qua lỗ, không ấn mạnh." : "Không đun nóng ống đã đậy kín.", ports);
        }

        static void LTubePart(Transform p, string name, Vector3 pos)
        {
            var g = Group(name, p, pos);
            NoShadow(Tube(g, "Nhánh đứng", Vector3.zero, new Vector3(0, 0.12f, 0), 0.0035f, labGlass, true));
            NoShadow(Tube(g, "Nhánh ngang", new Vector3(0, 0.12f, 0), new Vector3(0, 0.12f, 0.15f), 0.0035f, labGlass, true));
            MakeGrab(g, "ong_dan", 0.1f, 15f, true, 0.15f, 0.004f);
            MakePart(g, name, Vector2.zero, "Ống dẫn thủy tinh chữ L (Ø7): cắm qua lỗ nút, dẫn khí sang ống cao su.", "",
                     AddPort(g, "Đầu dưới (cắm qua nút)", PortType.DauOngThuyTinh, 7f, new Vector3(0, 0.03f, 0), Vector3.down, Vector3.forward),
                     AddPort(g, "Đầu ngang", PortType.DauOngThuyTinh, 7f, new Vector3(0, 0.12f, 0.15f), Vector3.forward, Vector3.up));
            g.localPosition = pos + new Vector3(0, 0.004f, 0);
            g.localRotation = Quaternion.Euler(-90f, 0, 0);  // nằm trên bàn: nhánh đứng nằm ngang
        }

        static void StraightTubePart(Transform p, string name, Vector3 pos)
        {
            var g = Group(name, p, pos);
            NoShadow(Tube(g, "Ống thẳng", Vector3.zero, new Vector3(0, 0.25f, 0), 0.0035f, labGlass, true));
            MakeGrab(g, "ong_dan", 0.15f, 12f, true, 0.25f, 0.004f);
            MakePart(g, name, Vector2.zero, "Ống dẫn thủy tinh thẳng (Ø7): đưa khí xuống đáy ống nghiệm, chậu nước.", "",
                     AddPort(g, "Đầu trên", PortType.DauOngThuyTinh, 7f, new Vector3(0, 0.25f, 0), Vector3.up, Vector3.forward),
                     AddPort(g, "Đầu dưới", PortType.DauOngThuyTinh, 7f, Vector3.zero, Vector3.down, Vector3.forward));
            LayFlat(g, pos, 0.25f, 0.004f);
        }

        static void HosePart(Transform p, string name, Vector3 pos, float length)
        {
            var hoseMat = Mat("OngCaoSu", new Color(0.62f, 0.3f, 0.18f), 0.25f);
            var a = Group(name, p, pos);
            Cyl(a, "Đầu ống A", new Vector3(0, 0.015f, 0), 0.0055f, 0.03f, hoseMat, true);
            var pa = AddPort(a, "Đầu ống A", PortType.DauOngCaoSu, 6.5f, Vector3.zero, Vector3.down, Vector3.forward);
            MakeGrab(a, "ong_cao_su", 0.02f, 20f);
            var apA = MakePart(a, name + " (đầu A)", Vector2.zero, $"Ống cao su (dài {length:0.0} m): nối ống thủy tinh, vòi nhánh, vòi nước.",
                               "Lồng đầu ống cao su vào đầu ống thủy tinh hoặc vòi nhánh. Cầm từng đầu để nối.", pa);
            var b = Group(name + " (đầu B)", p, pos + new Vector3(0.2f, 0, 0.05f));
            Cyl(b, "Đầu ống B", new Vector3(0, 0.015f, 0), 0.0055f, 0.03f, hoseMat, true);
            var pb = AddPort(b, "Đầu ống B", PortType.DauOngCaoSu, 6.5f, Vector3.zero, Vector3.down, Vector3.forward);
            MakeGrab(b, "ong_cao_su", 0.02f, 10f);
            var apB = MakePart(b, name + " (đầu B)", Vector2.zero, "Đầu còn lại của ống cao su.", "", pb);
            apA.linked = apB;
            apB.linked = apA;
            var rh = a.gameObject.AddComponent<RubberHose>();
            rh.endA = pa.transform;
            rh.endB = apB;
            rh.endBPoint = pb.transform;
            rh.length = length;
            rh.material = hoseMat;
        }

        static void SepFunnelPart(Transform p, string name, Vector3 pos)
        {
            var inner = new[] { V(0.002f, 0.05f), V(0.009f, 0.07f), V(0.038f, 0.13f), V(0.033f, 0.19f), V(0.009f, 0.21f) };
            var c = Vessel(p, name, pos, SepFunnelMesh(), inner, 125f, 0.23f, "pheu_chiet", 0.18f,
                           Mat("DungDichPhanUng", new Color(0.85f, 0.92f, 1f, 0.3f), 0.9f, 0, transparent: true));
            var t = c.transform;
            Valve(Box(t, "Khóa phễu chiết", new Vector3(0, 0.06f, 0), new Vector3(0.008f, 0.008f, 0.03f), bluePlastic), 4f);
            MakeGrab(t, "pheu_chiet", 0.18f, 120f);
            MakePart(t, name, new Vector2(0.01f, 0.01f), "Phễu chiết: tách hai chất lỏng không tan vào nhau (dầu – nước). Lớp nặng ở dưới chảy ra trước.",
                     "Đặt phễu lên vòng sắt; mở nút trên trước khi mở khóa.",
                     AddPort(t, "Bầu phễu (tựa vòng sắt)", PortType.ChanPheu, 70f, new Vector3(0, 0.12f, 0), Vector3.down, Vector3.forward, -1),
                     AddPort(t, "Miệng phễu chiết", PortType.CoBinh, 19f, new Vector3(0, 0.23f, 0), Vector3.up, Vector3.forward),
                     AddPort(t, "Đuôi phễu chiết", PortType.MiengRa, 6f, Vector3.zero, Vector3.down, Vector3.forward));
            LayFlat(t, pos, 0.23f, 0.04f);  // nằm trên bàn
        }

        /// <summary>Tay khóa phễu: bấm để mở / đóng (mạng dòng chảy cho chất lỏng chảy qua đuôi phễu).</summary>
        static void Valve(GameObject handle, float rateMlPerS)
        {
            var v = handle.AddComponent<FlowValve>();
            v.handle = handle.transform;
            v.rateMlPerS = rateMlPerS;
        }
        static void FilterFunnelPart(Transform p, string name, Vector3 pos)
        {
            var mesh = Lathe("PheuLoc", 24, V(0, 0), V(0.0035f, 0), V(0.0035f, 0.05f), V(0.007f, 0.06f), V(0.04f, 0.11f), V(0.042f, 0.115f));
            var inner = new[] { V(0.002f, 0.05f), V(0.006f, 0.06f), V(0.038f, 0.11f) };
            var c = Vessel(p, name, pos, mesh, inner, 60f, 0.115f, "pheu_loc", 0.09f,
                           Mat("DungDichPhanUng", new Color(0.85f, 0.92f, 1f, 0.3f), 0.9f, 0, transparent: true));
            var t = c.transform;
            MakeGrab(t, "pheu_loc", 0.09f, 60f);
            MakePart(t, name, new Vector2(0.02f, 0.02f), "Phễu lọc thủy tinh: đặt giấy lọc gấp tư, chất rắn ở lại trên giấy, nước lọc chảy xuống.",
                     "Cắm đuôi phễu vào miệng bình hứng hoặc đặt phễu lên vòng sắt; đuôi phễu chạm thành bình hứng.",
                     AddPort(t, "Thân phễu (tựa vòng / miệng bình)", PortType.ChanPheu, 40f, new Vector3(0, 0.065f, 0), Vector3.down, Vector3.forward, -1),
                     AddPort(t, "Đuôi phễu lọc", PortType.MiengRa, 7f, Vector3.zero, Vector3.down, Vector3.forward));
        }
        // =====================================================================
        // Bộ phận cho nhiều loại thí nghiệm (đun, điều chế – rửa – thu khí, lọc hút, cô cạn, phản ứng trong ống nghiệm)
        // =====================================================================

        static void LampPart(Transform p, string name, Vector3 pos)
        {
            var g = Group("Đèn cồn di động", p, pos);  // tên nhóm khác "Đèn cồn" (tên lưới đèn mà AttachUsables gắn ngọn lửa)
            AlcoholLamp(g, Vector3.zero);
            MakeGrab(g, "den_con", 0.04f, 150f);
            MakePart(g, name, new Vector2(0.07f, 0.07f), "Đèn cồn: nguồn nhiệt cho ống nghiệm, bình cầu, cốc trên lưới. Đặt dưới bình đun hoặc dưới kiềng.",
                     "Đun bằng phần ngoài ngọn lửa; tắt bằng cách đậy nắp, không thổi.");
        }

        static void TripodPart(Transform p, string name, Vector3 pos)
        {
            var g = Group(name, p, pos);
            for (int i = 0; i < 3; i++)
            {
                float a = i * 120f * Mathf.Deg2Rad;
                var foot = new Vector3(Mathf.Cos(a) * 0.085f, 0, Mathf.Sin(a) * 0.085f);
                var head = new Vector3(Mathf.Cos(a) * 0.07f, 0.2f, Mathf.Sin(a) * 0.07f);
                Tube(g, "Chân kiềng", foot, head, 0.004f, darkMetal, true);
            }
            MeshObj(g, "Vòng kiềng", Lathe("VongKiengLapRap", 28, V(0.068f, 0), V(0.075f, 0), V(0.075f, 0.006f), V(0.068f, 0.006f), V(0.068f, 0)), new Vector3(0, 0.2f, 0), darkMetal, true);
            MakeGrab(g, "kieng", 0.2f, 400f);
            MakePart(g, name, new Vector2(0.15f, 0.15f), "Kiềng ba chân: đặt lưới tản nhiệt lên, đèn cồn ở giữa để đun cốc, bát sứ.", "",
                     AddPort(g, "Vòng kiềng", PortType.Vong, 70f, new Vector3(0, 0.206f, 0), Vector3.up, Vector3.forward, -1));
        }

        static void GauzePart(Transform p, string name, Vector3 pos)
        {
            var g = Group(name, p, pos);
            Box(g, "Lưới", new Vector3(0, 0.0015f, 0), new Vector3(0.14f, 0.003f, 0.14f), Mat("LuoiAmiang", new Color(0.55f, 0.55f, 0.55f), 0.3f, 0.5f), true);
            Cyl(g, "Tâm lưới", new Vector3(0, 0.0035f, 0), 0.045f, 0.001f, Mat("TamLuoi", new Color(0.9f, 0.9f, 0.88f), 0.1f));
            MakeGrab(g, "luoi", 0.01f, 60f);
            MakePart(g, name, new Vector2(0.14f, 0.14f), "Lưới tản nhiệt (tâm gốm): đặt trên kiềng hoặc vòng sắt để nhiệt tỏa đều, đỡ cốc, bình.", "Không đun trực tiếp thủy tinh trên ngọn lửa.",
                     AddPort(g, "Đáy lưới", PortType.DayBinh, 100f, Vector3.zero, Vector3.down, Vector3.forward, -1),
                     AddPort(g, "Mặt lưới", PortType.MatLuoi, 150f, new Vector3(0, 0.003f, 0), Vector3.up, Vector3.forward, -1));
        }

        static void TroughPart(Transform p, string name, Vector3 pos)
        {
            var mesh = Lathe("ChauThuyTinh", 32, V(0, 0), V(0.13f, 0), V(0.13f, 0), V(0.135f, 0.1f), V(0.14f, 0.102f), V(0.135f, 0.1f), V(0.127f, 0.004f), V(0, 0.004f));
            var c = Vessel(p, name, pos, mesh, new[] { V(0.126f, 0.004f), V(0.132f, 0.1f) }, 2000f, 0.1f, "chau", 0.09f,
                           Mat("DungDichPhanUng", new Color(0.85f, 0.92f, 1f, 0.3f), 0.9f, 0, transparent: true));
            var t = c.transform;
            // kệ đỡ ống úp (giá hình tổ ong): úp miệng ống nghiệm / lọ thu khí lên, đầu ống dẫn khí luồn vào lỗ bên dưới
            var shelf = Group("Kệ úp ống", t, new Vector3(0.05f, 0.05f, 0));
            MeshObj(shelf, "Kệ úp", Lathe("KeUpOng", 20, V(0, 0), V(0.035f, 0), V(0.035f, 0.03f), V(0.012f, 0.03f), V(0.012f, 0.032f), V(0, 0.032f)), Vector3.zero, Mat("KeSu", new Color(0.92f, 0.92f, 0.9f), 0.3f));
            MakeGrab(t, "chau", 0.09f, 700f);
            MakePart(t, name, new Vector2(0.26f, 0.26f), "Chậu thủy tinh: chứa nước để thu khí bằng phương pháp đẩy nước (khí ít tan: O₂, H₂, N₂).",
                     "Đổ đầy nước vào ống nghiệm, bịt miệng, úp ngược lên kệ trong chậu; luồn đầu ống dẫn khí vào dưới miệng ống.",
                     AddPort(t, "Kệ úp ống", PortType.GiaUp, 55f, new Vector3(0.05f, 0.082f, 0), Vector3.up, Vector3.forward, 0),
                     // lỗ bên của kệ: luồn đầu ống dẫn khí vào, khí sủi lên vào ống úp phía trên
                     AddPort(t, "Lỗ dẫn khí của kệ", PortType.LoNut, 7f, new Vector3(0.015f, 0.065f, 0), Vector3.left, Vector3.up, 0),
                     // chậu để hở: khí không thu được thoát qua mặt nước
                     AddPort(t, "Mặt nước chậu", PortType.MiengRa, 260f, new Vector3(-0.06f, 0.1f, 0), Vector3.up, Vector3.forward, 0));
        }

        static void SideArmFlaskPart(Transform p, string name, Vector3 pos)
        {
            var c = FunctionalVessel(p, name, pos, "binh_cau");
            var t = c.transform;
            c.label = name;
            Vector3 a0 = new Vector3(0.011f, 0.125f, 0), dir = new Vector3(Mathf.Cos(-20f * Mathf.Deg2Rad), Mathf.Sin(-20f * Mathf.Deg2Rad), 0), a1 = a0 + dir * 0.07f;
            NoShadow(Tube(t, "Nhánh bình", a0, a1, 0.004f, labGlass, true));
            MakeGrab(t, "binh_cau_nhanh", 0.12f, 140f);
            MakePart(t, name, new Vector2(0.05f, 0.05f), "Bình cầu có nhánh (bình Wurtz): điều chế khí; miệng đậy nút cắm phễu nhỏ giọt, khí ra theo nhánh.",
                     "Kiểm tra độ kín trước khi cho hóa chất; nhỏ axit từ từ.",
                     AddPort(t, "Miệng bình (đậy nút)", PortType.CoBinh, 26f, new Vector3(0, 0.15f, 0), Vector3.up, Vector3.forward),
                     AddPort(t, "Nhánh dẫn khí", PortType.VoiNhanh, 8f, a1, dir, new Vector3(-dir.y, dir.x, 0)),
                     AddPort(t, "Cổ bình (chỗ kẹp)", PortType.VungKep, 26f, new Vector3(0, 0.105f, 0), Vector3.forward, Vector3.up, -1),  // kẹp ở cổ: nhánh chĩa sang phải người đứng trước giá
                     AddPort(t, "Đáy bình", PortType.DayBinh, 90f, Vector3.zero, Vector3.down, Vector3.forward, -1));
        }

        static void SuctionFlaskPart(Transform p, string name, Vector3 pos)
        {
            var c = FunctionalVessel(p, name, pos, "binh_non");
            var t = c.transform;
            c.label = name;
            t.localScale = Vector3.one * 1.12f;  // đều 3 trục: bộ phận lắp vào (con) không bị méo khi xoay
            Vector3 a0 = new Vector3(0.016f, 0.11f, 0), a1 = new Vector3(0.05f, 0.11f, 0);
            NoShadow(Tube(t, "Nhánh hút", a0, a1, 0.004f, labGlass, true));
            MakeGrab(t, "binh_hut_loc", 0.11f, 200f);
            MakePart(t, name, new Vector2(0.09f, 0.09f), "Bình hút lọc (bình Büchner) thành dày, có nhánh nối vòi hút chân không.",
                     "Lắp phễu Buchner lên miệng; nối nhánh với vòi hút; tắt hút trước khi tháo ống để nước không trào ngược.",
                     AddPort(t, "Miệng bình hút lọc", PortType.CoBinh, 34f, new Vector3(0, 0.143f, 0), Vector3.up, Vector3.forward),
                     AddPort(t, "Nhánh hút chân không", PortType.VoiNhanh, 8f, a1, Vector3.right, Vector3.up),
                     AddPort(t, "Đáy bình hút lọc", PortType.DayPhang, 85f, Vector3.zero, Vector3.down, Vector3.forward, -1));
        }

        static void BeakerPart(Transform p, string name, Vector3 pos, string shape)
        {
            var c = FunctionalVessel(p, name, pos, shape);
            var t = c.transform;
            c.label = name;
            bool big = shape == "coc250";
            MakeGrab(t, "coc", big ? 0.07f : 0.05f, big ? 110f : 60f);
            MakePart(t, name, new Vector2(big ? 0.07f : 0.05f, big ? 0.07f : 0.05f), "Cốc thủy tinh: hứng nước lọc, đun dung dịch trên lưới, hòa tan chất rắn.", "",
                     AddPort(t, "Đáy cốc", PortType.DayPhang, big ? 70f : 50f, Vector3.zero, Vector3.down, Vector3.forward, -1),
                     AddPort(t, "Miệng cốc", PortType.MiengRa, big ? 70f : 50f, new Vector3(0, big ? 0.1f : 0.07f, 0), Vector3.up, Vector3.forward));  // cốc để hở
        }

        static void GasWashBottlePart(Transform p, string name, Vector3 pos)
        {
            var c = Vessel(p, name, pos, ReagentBottle(), new[] { V(0.031f, 0.003f), V(0.031f, 0.1f) }, 250f, 0.15f, "binh_rua_khi", 0.1f,
                           Mat("DungDichPhanUng", new Color(0.85f, 0.92f, 1f, 0.3f), 0.9f, 0, transparent: true));
            var t = c.transform;
            t.localScale = Vector3.one * 1.15f;
            Cyl(t, "Nắp nhám", new Vector3(0, 0.155f, 0), 0.016f, 0.02f, Mat("KhopNham", new Color(0.92f, 0.93f, 0.93f, 0.7f), 0.2f, 0, transparent: true));
            NoShadow(Tube(t, "Ống sục (dài)", new Vector3(-0.008f, 0.015f, 0), new Vector3(-0.008f, 0.2f, 0), 0.003f, labGlass));
            NoShadow(Tube(t, "Ống vào", new Vector3(-0.008f, 0.2f, 0), new Vector3(-0.04f, 0.2f, 0), 0.003f, labGlass));
            NoShadow(Tube(t, "Ống ra", new Vector3(0.008f, 0.16f, 0), new Vector3(0.04f, 0.2f, 0), 0.003f, labGlass));
            MakeGrab(t, "binh_rua_khi", 0.1f, 180f);
            MakePart(t, name, new Vector2(0.07f, 0.07f), "Bình rửa khí: khí đi vào ống dài sục qua dung dịch (nước, H₂SO₄ đặc, NaOH...) để giữ lại tạp chất, ra ở ống ngắn.",
                     "Khí vào ống DÀI, ra ống NGẮN; lắp ngược sẽ đẩy dung dịch ra ngoài.",
                     AddPort(t, "Ống vào (sục xuống đáy)", PortType.VoiNhanh, 8f, new Vector3(-0.04f, 0.2f, 0), Vector3.left, Vector3.up),
                     AddPort(t, "Ống ra", PortType.VoiNhanh, 8f, new Vector3(0.04f, 0.2f, 0), Vector3.right, Vector3.up),
                     AddPort(t, "Đáy bình rửa khí", PortType.DayPhang, 75f, Vector3.zero, Vector3.down, Vector3.forward, -1));
        }

        static void GasJarPart(Transform p, string name, Vector3 pos)
        {
            var mesh = Lathe("LoThuKhi", 24, V(0, 0), V(0.032f, 0), V(0.032f, 0), V(0.032f, 0.16f), V(0.036f, 0.165f), V(0.026f, 0.165f));
            var c = Vessel(p, name, pos, mesh, new[] { V(0.03f, 0.003f), V(0.03f, 0.16f) }, 250f, 0.165f, "lo_thu_khi", 0.1f,
                           Mat("DungDichPhanUng", new Color(0.85f, 0.92f, 1f, 0.3f), 0.9f, 0, transparent: true));
            var t = c.transform;
            MakeGrab(t, "lo_thu_khi", 0.1f, 150f);
            MakePart(t, name, new Vector2(0.065f, 0.065f), "Lọ thu khí: thu khí bằng cách đẩy không khí (để ngửa với khí nặng hơn không khí như CO₂, Cl₂; úp ngược với khí nhẹ) hoặc đẩy nước.",
                     "Khí nặng hơn không khí (M > 29): để ngửa lọ, đầu ống dẫn sát đáy.",
                     AddPort(t, "Miệng lọ thu khí", PortType.CoBinh, 50f, new Vector3(0, 0.165f, 0), Vector3.up, Vector3.forward),
                     AddPort(t, "Đáy lọ thu khí", PortType.DayPhang, 64f, Vector3.zero, Vector3.down, Vector3.forward, -1));
        }

        static void BuchnerPart(Transform p, string name, Vector3 pos)
        {
            var mesh = Lathe("BuchnerLapRap", 24, V(0, 0), V(0.008f, 0), V(0.008f, 0.04f), V(0.045f, 0.06f), V(0.045f, 0.12f), V(0.04f, 0.12f), V(0, 0.07f));
            var c = Vessel(p, name, pos, mesh, new[] { V(0.04f, 0.065f), V(0.04f, 0.118f) }, 100f, 0.12f, "pheu_buchner", 0.09f,
                           Mat("DungDichPhanUng", new Color(0.85f, 0.92f, 1f, 0.3f), 0.9f, 0, transparent: true));
            var t = c.transform;
            t.Find("Thủy tinh").GetComponent<Renderer>().sharedMaterial = signWhite;  // phễu sứ
            MeshObj(t, "Nón cao su", Lathe("NonCaoSuBuchner", 20, V(0.012f, 0.02f), V(0.019f, 0.045f), V(0.008f, 0.045f), V(0.008f, 0.02f), V(0.012f, 0.02f)), Vector3.zero, Mat("CaoSuNau", new Color(0.36f, 0.22f, 0.15f), 0.2f), true);
            MakeGrab(t, "pheu_buchner", 0.09f, 220f);
            MakePart(t, name, new Vector2(0.02f, 0.02f), "Phễu Buchner (sứ, đáy đục lỗ) có nón cao su: lắp lên bình hút lọc để lọc nhanh bằng hút chân không.",
                     "Đặt giấy lọc vừa khít đáy, thấm ướt rồi mới hút.",
                     AddPort(t, "Nón cao su (lắp miệng bình hút)", PortType.NutCaoSu, 34f, new Vector3(0, 0.035f, 0), Vector3.down, Vector3.forward),
                     AddPort(t, "Miệng phễu Buchner", PortType.MiengRa, 90f, new Vector3(0, 0.12f, 0), Vector3.up, Vector3.forward));  // không khí lọt qua giấy lọc: hệ hở
        }

        static void EvapDishPart(Transform p, string name, Vector3 pos)
        {
            var mesh = Lathe("BatSuLapRap", 24, V(0, 0), V(0.03f, 0), V(0.05f, 0.025f), V(0.055f, 0.03f), V(0.05f, 0.03f), V(0.028f, 0.006f), V(0, 0.006f));
            var c = Vessel(p, name, pos, mesh, new[] { V(0.026f, 0.006f), V(0.049f, 0.029f) }, 60f, 0.03f, "bat_su", 0.03f,
                           Mat("DungDichPhanUng", new Color(0.85f, 0.92f, 1f, 0.3f), 0.9f, 0, transparent: true));
            var t = c.transform;
            t.Find("Thủy tinh").GetComponent<Renderer>().sharedMaterial = signWhite;
            MakeGrab(t, "bat_su", 0.03f, 90f);
            MakePart(t, name, new Vector2(0.06f, 0.06f), "Bát sứ: cô cạn dung dịch để thu muối kết tinh.",
                     "Đặt trên lưới tản nhiệt; khuấy, gần cạn thì tắt lửa để nhiệt dư làm khô.",
                     AddPort(t, "Đáy bát sứ", PortType.DayPhang, 60f, Vector3.zero, Vector3.down, Vector3.forward, -1),
                     AddPort(t, "Miệng bát sứ", PortType.MiengRa, 100f, new Vector3(0, 0.03f, 0), Vector3.up, Vector3.forward));
        }

        static ApparatusPart TestTubeRackPart(Transform p, string name, Vector3 pos)
        {
            var g = Group(name, p, pos, 90f);
            Box(g, "Đế giá", new Vector3(0, 0.005f, 0), new Vector3(0.08f, 0.01f, 0.26f), oak, true);
            Box(g, "Tấm đỡ có lỗ", new Vector3(0, 0.07f, 0), new Vector3(0.08f, 0.008f, 0.26f), oak, true);
            foreach (float z in new[] { -0.125f, 0.125f }) Box(g, "Thành giá", new Vector3(0, 0.04f, z), new Vector3(0.08f, 0.08f, 0.01f), oak, true);
            var ports = new System.Collections.Generic.List<Port>();
            for (int i = 0; i < 6; i++)
                ports.Add(AddPort(g, $"Lỗ giá {i + 1}", PortType.LoGia, 22f, new Vector3(0, 0.011f, -0.1f + i * 0.04f), Vector3.up, Vector3.forward, -1));
            MakeGrab(g, "gia_ong_nghiem", 0.08f, 150f);
            return MakePart(g, name, new Vector2(0.08f, 0.26f), "Giá ống nghiệm 6 lỗ: cắm ống nghiệm đứng thẳng khi làm phản ứng.", "", ports.ToArray());
        }

        static void DroppingFunnelPart(Transform p, string name, Vector3 pos)
        {
            var mesh = Lathe("PheuNhoGiot", 24, V(0, 0), V(0.0035f, 0), V(0.0035f, 0.07f), V(0.01f, 0.08f), V(0.022f, 0.1f), V(0.022f, 0.19f), V(0.01f, 0.2f), V(0.01f, 0.215f));
            var c = Vessel(p, name, pos, mesh, new[] { V(0.003f, 0.07f), V(0.009f, 0.08f), V(0.021f, 0.1f), V(0.021f, 0.19f) }, 60f, 0.215f, "pheu_nho_giot", 0.15f,
                           Mat("DungDichPhanUng", new Color(0.85f, 0.92f, 1f, 0.3f), 0.9f, 0, transparent: true));
            var t = c.transform;
            Valve(Box(t, "Khóa phễu nhỏ giọt", new Vector3(0, 0.08f, 0), new Vector3(0.008f, 0.008f, 0.03f), bluePlastic), 0.5f);
            MakeGrab(t, "pheu_nho_giot", 0.15f, 90f, true, 0.215f, 0.022f);
            MakePart(t, name, Vector2.zero, "Phễu nhỏ giọt có khóa: cắm qua nút bình điều chế, nhỏ từ từ dung dịch (axit) xuống chất phản ứng.",
                     "Đóng khóa trước khi rót dung dịch vào phễu; mở khóa từng chút để điều chỉnh tốc độ sinh khí.",
                     AddPort(t, "Đuôi phễu (cắm qua nút)", PortType.DauOngThuyTinh, 7f, new Vector3(0, 0.035f, 0), Vector3.down, Vector3.forward),
                     AddPort(t, "Miệng phễu nhỏ giọt", PortType.CoBinh, 19f, new Vector3(0, 0.215f, 0), Vector3.up, Vector3.forward, 1));
            LayFlat(t, pos, 0.215f, 0.022f);
        }

        static void UTubePart(Transform p, string name, Vector3 pos)
        {
            var g = Group(name, p, pos);
            NoShadow(Tube(g, "Nhánh trái", new Vector3(-0.03f, 0.02f, 0), new Vector3(-0.03f, 0.14f, 0), 0.009f, labGlass, true));
            NoShadow(Tube(g, "Nhánh phải", new Vector3(0.03f, 0.02f, 0), new Vector3(0.03f, 0.14f, 0), 0.009f, labGlass, true));
            NoShadow(Tube(g, "Đáy cong", new Vector3(-0.03f, 0.015f, 0), new Vector3(0.03f, 0.015f, 0), 0.009f, labGlass));
            Cyl(g, "CaCl₂ khan", new Vector3(-0.03f, 0.08f, 0), 0.008f, 0.1f, Mat("CaCl2", new Color(0.95f, 0.95f, 0.92f), 0.1f));
            Cyl(g, "CaCl₂ khan", new Vector3(0.03f, 0.08f, 0), 0.008f, 0.1f, Mat("CaCl2", new Color(0.95f, 0.95f, 0.92f), 0.1f));
            foreach (float x in new[] { -0.03f, 0.03f })
                NoShadow(Tube(g, "Vòi nhánh", new Vector3(x, 0.14f, 0), new Vector3(x + (x < 0 ? -0.025f : 0.025f), 0.15f, 0), 0.004f, labGlass));
            MakeGrab(g, "ong_chu_u", 0.1f, 80f);
            MakePart(g, name, new Vector2(0.08f, 0.02f), "Ống chữ U chứa CaCl₂ khan: làm khô khí (giữ hơi nước) trước khi thu.", "Không dùng CaCl₂ để làm khô NH₃ (tạo phức).",
                     AddPort(g, "Đầu vào ống U", PortType.VoiNhanh, 8f, new Vector3(-0.055f, 0.15f, 0), Vector3.left, Vector3.up),
                     AddPort(g, "Đầu ra ống U", PortType.VoiNhanh, 8f, new Vector3(0.055f, 0.15f, 0), Vector3.right, Vector3.up),
                     AddPort(g, "Đáy ống U (chỗ kẹp)", PortType.VungKep, 20f, new Vector3(0, 0.015f, 0), Vector3.back, Vector3.up, -1));
        }
    }
}
