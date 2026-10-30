using System;
using Khkt.Environment;
using Khkt.Rendering;
using UnityEngine;
using static Khkt.EditorTools.LabKit;

namespace Khkt.EditorTools
{
    /// <summary>
    /// Dựng Tầng 2 – Khu sinh học của VERA Lab: menu KHKT > Dựng phòng thí nghiệm.
    ///
    /// Mặt bằng (mét). Gốc cục bộ Tầng 2 đặt tại (0, 3.8, 0):
    ///   - Phòng thí nghiệm sinh học: x 0..20, z 0..15 (300 m²)
    ///   - Phòng vi sinh: x 20..27, z 7.5..15
    ///   - Kho sinh phẩm: x 20..27, z 0..7.5
    ///   - Hành lang tầng 2: x 0..27, z -2.2..0
    ///   - Phòng sinh học phân tử: x 0..13.5, z -9..-2.2
    ///   - Nhà kính thực vật: x 13.5..27, z -9..-2.2
    ///   - Hành lang đông tầng 2: x 27..30, z 6..15
    ///   - Lõi thang bộ + thang máy: x 27..30, z -2.2..6 (cả 2 tầng)
    /// </summary>
    public static partial class LabSceneBuilder
    {
        public const float F2_Y = 3.8f;      // Chiều cao mặt sàn tầng 2
        public const float F2_H = 3.6f;      // Chiều cao trần tầng 2
        public const float F2_T = 0.2f;      // Độ dày tường

        // Màu khu chủ đề Quốc tế cho Tầng 2
        static Color zoneBioLab, zoneMicroBio, zoneBioStore, zoneMolecular, zoneGreenhouse;
        static Material leafBioLab, leafMicroBio, leafBioStore, leafMolecular, leafGreenhouse;

        static void InitFloor2Theme()
        {
            EnsureT2Folders();
            // Bảng màu Quốc tế (đồng bộ tầng 1)
            zoneBioLab = new Color(0.20f, 0.62f, 0.36f);       // Xanh ngọc lục bảo (Sinh học)
            zoneMicroBio = new Color(0.10f, 0.62f, 0.62f);     // Xanh mòng két lam (Vi sinh)
            zoneBioStore = new Color(0.36f, 0.55f, 0.86f);     // Lam sáng (Kho sinh phẩm)
            zoneMolecular = new Color(0.56f, 0.45f, 0.82f);    // Tím thạch anh (Sinh học phân tử)
            zoneGreenhouse = new Color(0.55f, 0.72f, 0.22f);   // Xanh lá mạ (Nhà kính)

            leafBioLab = Mat("T2_QuocTe_CuaBioLab", zoneBioLab, 0.5f);
            leafMicroBio = Mat("T2_QuocTe_CuaMicroBio", zoneMicroBio, 0.5f);
            leafBioStore = Mat("T2_QuocTe_CuaBioStore", zoneBioStore, 0.5f);
            leafMolecular = Mat("T2_QuocTe_CuaMolecular", zoneMolecular, 0.5f);
            leafGreenhouse = Mat("T2_QuocTe_CuaGreenhouse", zoneGreenhouse, 0.5f);
        }

        public static void BuildFloor2(Transform root)
        {
            InitFloor2Theme();

            var tang2 = Group("Tầng 2", root, new Vector3(0, F2_Y, 0));

            // 1. Vỏ kiến trúc tầng 2: sàn, tường bao, cửa sổ, trần, mái kính
            BuildFloor2Shell(tang2);

            // 2. Lõi thang bộ & thang máy (nằm trong cảnh lõi / khu lõi thang)
            BuildFloor2Stairs(tang2, root);

            // Nhóm hành lang tầng 2 (khu CorridorT2)
            Group("Hành lang tầng 2", tang2);

            EnsureFloor2ThemeMaterials();

            // 3. Nội thất Phòng TN Sinh Học đầy đủ (Giai đoạn 4)
            BuildFloor2BioLab(tang2.gameObject);

            // 4. Các phòng chuyên đề và hành lang đông (Giai đoạn 5)
            BuildFloor2SpecialRooms(tang2.gameObject);

            // 5. Cài đặt bộ chuyển đổi chủ đề Quốc tế / Tối giản (Giai đoạn 3)
            SetupFloor2ThemeSwitcher(tang2.gameObject);
        }
    }
}
