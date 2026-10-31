using UnityEngine;
using static Khkt.EditorTools.LabKit;

namespace Khkt.EditorTools
{
    /// <summary>
    /// Dựng Tầng 2 – Khu sinh học của VERA Lab (menu KHKT > Dựng phòng thí nghiệm).
    ///
    /// Mặt bằng (mét), cùng khung nhà tầng 1. Nhóm "Tầng 2" đặt tại (0; 3,8; 0) nên mọi tọa độ trong các hàm tầng 2 là tọa độ
    /// trong tầng (y = 0 là mặt sàn tầng 2) và gọi lại được các hàm dựng của tầng 1 (Wall, Stool, TopWithSink...):
    ///   Phòng thí nghiệm sinh học  x 0..20,  z 0..15   9 bàn kính hiển vi (cùng vị trí 9 bàn tầng 1), bảng ở tường tây
    ///   Phòng vi sinh              x 20..27, z 7,5..15 buồng đệm x 25,2..27, z 7,5..10
    ///   Kho sinh phẩm              x 20..27, z 0..7,5
    ///   Hành lang tầng 2           x 0..27,  z −2,2..0
    ///   Phòng sinh học phân tử     x 0..13,5, z −9..−2,2
    ///   Nhà kính thực vật          x 13,5..27, z −9..−2,2 (mái kính)
    ///   Hành lang đông tầng 2      x 27..30, z 6..15 (tầng 1 bên dưới để trống, chỉ có cột)
    ///   Lõi thang bộ + thang máy   x 27..30, z −2,2..6, cao cả hai tầng (tọa độ thế giới, nhóm ở gốc cảnh)
    ///
    /// Mỗi phòng là một khu (ZoneMap 5..12): đồ tĩnh và đèn của phòng nằm trong nhóm phòng nên được tải / gỡ cùng khu.
    /// Vỏ (sàn, tường, trần, mái) ở cảnh lõi như vỏ tầng 1.
    /// </summary>
    public static partial class LabSceneBuilder
    {
        public const float F2_Y = 3.8f;      // mặt sàn tầng 2 (thế giới)
        public const float F2_H = 3.6f;      // chiều cao thông thủy tầng 2 (bằng H tầng 1)

        // Màu khu tầng 2 (chủ đề Quốc tế)
        static Color zoneBioLab, zoneMicroBio, zoneBioStore, zoneMolecular, zoneGreenhouse;
        static Material leafBioLab, leafMicroBio, leafBioStore, leafMolecular, leafGreenhouse;
        // Vật liệu riêng tầng 2
        static Material t2FloorKho, t2FloorPhanTu, t2FloorNhaKinh, t2FloorHallEast, t2CoreStone, t2EpoxyClean, t2FloorLine;
        static Material t2Accent, t2AccentDark;

        static void InitFloor2Materials()
        {
            EnsureT2Folders();
            zoneBioLab = new Color(0.20f, 0.62f, 0.36f);
            zoneMicroBio = new Color(0.10f, 0.62f, 0.62f);
            zoneBioStore = new Color(0.36f, 0.55f, 0.86f);
            zoneMolecular = new Color(0.56f, 0.45f, 0.82f);
            zoneGreenhouse = new Color(0.55f, 0.72f, 0.22f);

            // cánh cửa tông nhạt của màu khu (như tầng 1)
            leafBioLab = Mat("T2_CanhCuaSinh", Color.Lerp(zoneBioLab, Color.white, 0.5f), 0.45f);
            leafMicroBio = Mat("T2_CanhCuaViSinh", Color.Lerp(zoneMicroBio, Color.white, 0.5f), 0.45f);
            leafBioStore = Mat("T2_CanhCuaKho", Color.Lerp(zoneBioStore, Color.white, 0.5f), 0.45f);
            leafMolecular = Mat("T2_CanhCuaPhanTu", Color.Lerp(zoneMolecular, Color.white, 0.5f), 0.45f);
            leafGreenhouse = Mat("T2_CanhCuaNhaKinh", Color.Lerp(zoneGreenhouse, Color.white, 0.5f), 0.45f);
            t2Accent = Mat("T2_MauNhan", zoneBioLab, 0.45f);
            t2AccentDark = Mat("T2_MauNhanDam", new Color(0.06f, 0.24f, 0.16f), 0.45f);

            // Gạch 60 × 60 của tầng 1, chỉnh lặp theo kích thước từng sàn để viên gạch luôn vuông 60 cm
            var tiles = (Texture2D)floorLab.GetTexture("_BaseMap");
            var concreteTex = (Texture2D)concrete.GetTexture("_BaseMap");
            t2FloorKho = Mat("T2_SanKho", new Color(0.74f, 0.76f, 0.78f), 0.35f, 0, tiles, new Vector2(7f / 0.6f, 7.5f / 0.6f));
            t2FloorPhanTu = Mat("T2_SanPhanTu", new Color(0.8f, 0.79f, 0.77f), 0.32f, 0, tiles, new Vector2(13.5f / 0.6f, 6.8f / 0.6f));
            t2FloorHallEast = Mat("T2_SanHanhLangDong", new Color(0.76f, 0.74f, 0.7f), 0.3f, 0, tiles, new Vector2(3f / 0.6f, 9f / 0.6f));
            t2FloorNhaKinh = Mat("T2_SanNhaKinh", new Color(0.66f, 0.66f, 0.64f), 0.2f, 0, concreteTex, new Vector2(6.75f, 3.4f));
            t2EpoxyClean = Mat("T2_SanEpoxySach", new Color(0.8f, 0.85f, 0.83f), 0.6f);  // sàn epoxy liền mảnh vùng sạch
            t2CoreStone = Mat("T2_DaMaiLoiThang", new Color(0.72f, 0.71f, 0.69f), 0.5f);   // đá mài lõi thang (không ron nên không lệch viên)
            t2FloorLine = Mat("T2_ChiTang", new Color(0.3f, 0.32f, 0.34f), 0.4f, 0.3f);
            InitBioPropMaterials();
        }

        public static void BuildFloor2(Transform root)
        {
            InitFloor2Materials();
            var tang2 = Group("Tầng 2", root, new Vector3(0, F2_Y, 0));

            BuildFloor2Shell(tang2);       // sàn, tường, trần, mái, mái kính nhà kính (cảnh lõi)
            BuildStairCore(root);          // lõi thang bộ + thang máy (tọa độ thế giới, cao hai tầng)
            BuildFloor2BioLab(tang2);      // phòng thí nghiệm sinh học
            BuildMicrobiologyRoom(tang2);  // phòng vi sinh
            BuildBioStorageRoom(tang2);    // kho sinh phẩm
            BuildCorridorT2(tang2);        // hành lang tầng 2
            BuildMolecularRoom(tang2);     // phòng sinh học phân tử
            BuildGreenhouse(tang2);        // nhà kính thực vật
            BuildEastCorridor(tang2);      // hành lang đông

            EnsureFloor2ThemeMaterials();
            SetupFloor2ThemeSwitcher(tang2.gameObject);
        }

        /// <summary>
        /// Nhánh tầng 2 có thành phần chạy lúc chơi (thang máy, kính hiển vi bấm được, hoạt cảnh): không gộp lô tĩnh để còn
        /// di chuyển / đổi được. Gọi trong bộ lọc MarkStatic của Build().
        /// </summary>
        static bool IsFloor2Dynamic(Transform t)
        {
            foreach (var mb in t.GetComponents<MonoBehaviour>())
            {
                if (mb == null) continue;
                string ns = mb.GetType().Namespace ?? "";
                if (mb is Khkt.Environment.Tang2.ThemeOnly) continue;
                if (ns.StartsWith("Khkt.Environment.Tang2") || ns.StartsWith("Khkt.Demo.Bio")) return true;
            }
            return false;
        }

        // ---------------------------------------------------------------------
        // Tiện ích dựng tầng 2
        // ---------------------------------------------------------------------

        /// <summary>Tấm phẳng nằm ngang từ (x0, z0) tới (x1, z1), mặt dưới y0, mặt trên y1.</summary>
        static GameObject Slab(Transform p, string name, float x0, float z0, float x1, float z1, Material m, float y0 = -0.1f, float y1 = 0f, bool collider = true)
            => Box(p, name, new Vector3((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), new Vector3(x1 - x0, y1 - y0, z1 - z0), m, collider);
    }
}
