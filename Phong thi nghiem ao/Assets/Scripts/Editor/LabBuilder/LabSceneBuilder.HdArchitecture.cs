using System;
using UnityEngine;
using UnityEngine.Rendering;
using static Khkt.EditorTools.LabKit;

namespace Khkt.EditorTools
{
    public static partial class LabSceneBuilder
    {
        /// <summary>
        /// Giai đoạn 5: Bổ sung các chi tiết kiến trúc HD (nhóm HD_*) theo Art Bible:
        ///   • Toàn bộ chi tiết nằm trong nhóm HD_*, KHÔNG có Collider, không dời bất kỳ vật thể nào của cảnh gốc.
        ///   • Phòng lab: phào trần thạch cao, len chân tường inox bo tròn, cột kỹ thuật thả trần.
        ///   • Nhà kính: nẹp gioăng nhôm kính chữ T, hệ thống ống tưới nhỏ giọt PE, rãnh thoát nước vỉ gang.
        ///   • Ngoại thất: gờ viền mái nhôm máng thu nước âm, ốp chân tường granite chống rêu mốc, nẹp phào cửa sổ.
        /// </summary>
        public static void BuildHdArchitecture(Transform root)
        {
            var hdRoot = Group("HD_KienTruc", root);
            hdRoot.SetAsLastSibling();

            BuildHdPhongLab(hdRoot);
            BuildHdNhaKinh(hdRoot);
            BuildHdNgoaiThat(hdRoot);

            Debug.Log("[HD-KIEN-TRUC] Đã dựng hoàn tất hệ thống chi tiết kiến trúc HD (phòng lab, nhà kính, ngoại thất).");
        }

        static void BuildHdPhongLab(Transform parent)
        {
            var g = Group("HD_PhongLab", parent);

            var matInox = Mat("Inox", new Color(0.85f, 0.86f, 0.87f), 0.75f, 0.9f);
            var matLen = Mat("InoxSatin304", new Color(0.85f, 0.86f, 0.87f), 0.65f, 0.95f);
            var matPhao = Mat("PhaoTranLab", new Color(0.96f, 0.96f, 0.95f), 0.12f, 0f);
            var matNhom = Mat("AlumAnodized029", new Color(0.3f, 0.32f, 0.34f), 0.5f, 0.85f);

            // 1. Len chân tường Inox 304 cao 10 cm, dày 1.5 cm quanh các cạnh phòng lab, chừa khoảng cửa đi (Mục 6)
            // Cạnh nam (z = 0.01) - chừa cửa tự động chính phòng lab (x = 1.6..3.2)
            Box(g, "HD_LenChanTuong_Nam_1", new Vector3(0.8f, 0.05f, 0.01f), new Vector3(1.6f, 0.10f, 0.02f), matLen, false);
            Box(g, "HD_LenChanTuong_Nam_2", new Vector3((3.2f + LabX) / 2f, 0.05f, 0.01f), new Vector3(LabX - 3.2f, 0.10f, 0.02f), matLen, false);

            // Cạnh bắc (z = LabZ - 0.01) - chừa cửa ra ban công/hiên (x = 1.0..2.6)
            Box(g, "HD_LenChanTuong_Bac_1", new Vector3(0.5f, 0.05f, LabZ - 0.01f), new Vector3(1.0f, 0.10f, 0.02f), matLen, false);
            Box(g, "HD_LenChanTuong_Bac_2", new Vector3((2.6f + LabX) / 2f, 0.05f, LabZ - 0.01f), new Vector3(LabX - 2.6f, 0.10f, 0.02f), matLen, false);

            // Cạnh tây (x = 0.01)
            Box(g, "HD_LenChanTuong_Tay", new Vector3(0.01f, 0.05f, LabZ / 2f), new Vector3(0.02f, 0.10f, LabZ), matLen, false);

            // Cạnh đông (x = LabX - 0.01) - chừa lối thông sang kho hóa chất (z = 6.9..8.5)
            Box(g, "HD_LenChanTuong_Dong_1", new Vector3(LabX - 0.01f, 0.05f, 3.45f), new Vector3(0.02f, 0.10f, 6.9f), matLen, false);
            Box(g, "HD_LenChanTuong_Dong_2", new Vector3(LabX - 0.01f, 0.05f, (8.5f + LabZ) / 2f), new Vector3(0.02f, 0.10f, LabZ - 8.5f), matLen, false);

            // 2. Phào trần thạch cao gián tiếp (Cove molding) chạy quanh mép trần (y = H - 0.04)
            Box(g, "HD_PhaoTran_Nam", new Vector3(LabX / 2f, H - 0.04f, 0.06f), new Vector3(LabX, 0.08f, 0.12f), matPhao, false);
            Box(g, "HD_PhaoTran_Bac", new Vector3(LabX / 2f, H - 0.04f, LabZ - 0.06f), new Vector3(LabX, 0.08f, 0.12f), matPhao, false);
            Box(g, "HD_PhaoTran_Tay", new Vector3(0.06f, H - 0.04f, LabZ / 2f), new Vector3(0.12f, 0.08f, LabZ), matPhao, false);
            Box(g, "HD_PhaoTran_Dong", new Vector3(LabX - 0.06f, H - 0.04f, LabZ / 2f), new Vector3(0.12f, 0.08f, LabZ), matPhao, false);

            // 3. Cột cấp điện và khí kỹ thuật thả trần (service drops) hình elip thanh thoát inox mờ thả từ trần xuống giữa bàn
            float[] dropXs = new float[] { 5.2f, 10.5f, 15.8f };
            float[] dropZs = new float[] { 4.5f, 10.5f };
            for (int ix = 0; ix < dropXs.Length; ix++)
            {
                for (int iz = 0; iz < dropZs.Length; iz++)
                {
                    var col = Cyl(g, $"HD_CotKyThuat_{ix}_{iz}", new Vector3(dropXs[ix], (H + 1.2f) / 2f, dropZs[iz]), 0.04f, H - 1.2f, matNhom, false);
                }
            }
        }

        static void BuildHdNhaKinh(Transform parent)
        {
            var g = Group("HD_NhaKinh", parent);

            var matCaoSu = Mat("Rubber001", new Color(0.08f, 0.08f, 0.08f), 0.18f, 0f);
            var matOngPe = Mat("PE_NhaKinh", new Color(0.15f, 0.16f, 0.17f), 0.45f, 0f);
            var matViGang = Mat("ViGang_NhaKinh", new Color(0.22f, 0.23f, 0.24f), 0.35f, 0.6f);
            var matNhomTrang = Mat("NhomTrang_NhaKinh", new Color(0.95f, 0.95f, 0.95f), 0.55f, 0.7f);

            float xMin = RoomSplitX;
            float xMax = StoreX;
            float zMin = WingZ;
            float zMax = F2_CorridorZ;
            float yBase = F2_Y;

            // 1. Nẹp gioăng cao su chữ T gia cố viền kính nhà kính (chạy theo khung trần và vách ngoài)
            float xCenter = (xMin + xMax) / 2f;
            float zCenter = (zMin + zMax) / 2f;
            float xLen = xMax - xMin;
            float zLen = zMax - zMin;

            // Viền chân kính
            Box(g, "HD_NepChanKinh_Nam", new Vector3(xCenter, yBase + 0.02f, zMin + 0.02f), new Vector3(xLen, 0.04f, 0.04f), matCaoSu, false);
            Box(g, "HD_NepChanKinh_Bac", new Vector3(xCenter, yBase + 0.02f, zMax - 0.02f), new Vector3(xLen, 0.04f, 0.04f), matCaoSu, false);
            Box(g, "HD_NepChanKinh_Tay", new Vector3(xMin + 0.02f, yBase + 0.02f, zCenter), new Vector3(0.04f, 0.04f, zLen), matCaoSu, false);
            Box(g, "HD_NepChanKinh_Dong", new Vector3(xMax - 0.02f, yBase + 0.02f, zCenter), new Vector3(0.04f, 0.04f, zLen), matCaoSu, false);

            // 2. Rãnh thoát nước vỉ gang chống trượt men theo hai bên lối đi nhà kính
            Box(g, "HD_RanhThoatNuoc_1", new Vector3(xCenter, yBase + 0.003f, zMin + 1.2f), new Vector3(xLen - 0.8f, 0.006f, 0.22f), matViGang, false);
            Box(g, "HD_RanhThoatNuoc_2", new Vector3(xCenter, yBase + 0.003f, zMax - 1.2f), new Vector3(xLen - 0.8f, 0.006f, 0.22f), matViGang, false);

            // 3. Ống PE dẫn nước tưới nhỏ giọt chạy men chân các bồn cây
            Cyl(g, "HD_OngTuoiNhoGiot_A", new Vector3(xCenter, yBase + 0.04f, zMin + 2.1f), 0.012f, xLen - 1.2f, matOngPe, false, new Vector3(0, 0, 90));
            Cyl(g, "HD_OngTuoiNhoGiot_B", new Vector3(xCenter, yBase + 0.04f, zMax - 2.1f), 0.012f, xLen - 1.2f, matOngPe, false, new Vector3(0, 0, 90));
        }

        static void BuildHdNgoaiThat(Transform parent)
        {
            var g = Group("HD_NgoaiThat", parent);

            var matGranite = Mat("GraniteChanTuong", new Color(0.28f, 0.30f, 0.32f), 0.42f, 0f);
            var matMangNuoc = Mat("MangNuocNhom", new Color(0.25f, 0.26f, 0.28f), 0.5f, 0.8f);
            var matGopCua = Mat("GoPhaoCuaSo", new Color(0.94f, 0.94f, 0.93f), 0.15f, 0f);

            // 1. Chân tường ốp đá granite cao 60 cm bao quanh chân tòa nhà chính (chống thấm rêu mốc), chừa khoảng cửa đi (Mục 6)
            float totalH = F2_Y + F2_H;
            // Mặt nam: chừa cửa phòng lab (x = 1.6..3.2) và cửa kho hóa chất (x = 21.0..22.2)
            Box(g, "HD_OpChanTuong_Nam_1", new Vector3(0.65f, 0.30f, -0.04f), new Vector3(1.9f, 0.60f, 0.08f), matGranite, false);
            Box(g, "HD_OpChanTuong_Nam_2", new Vector3(12.1f, 0.30f, -0.04f), new Vector3(17.8f, 0.60f, 0.08f), matGranite, false);
            Box(g, "HD_OpChanTuong_Nam_3", new Vector3(24.75f, 0.30f, -0.04f), new Vector3(5.1f, 0.60f, 0.08f), matGranite, false);

            // Mặt bắc: chừa cửa ban công/hiên (x = 1.0..2.6)
            Box(g, "HD_OpChanTuong_Bac_1", new Vector3(0.35f, 0.30f, LabZ + 0.04f), new Vector3(1.3f, 0.60f, 0.08f), matGranite, false);
            Box(g, "HD_OpChanTuong_Bac_2", new Vector3(14.95f, 0.30f, LabZ + 0.04f), new Vector3(24.7f, 0.60f, 0.08f), matGranite, false);

            // Mặt tây
            Box(g, "HD_OpChanTuong_Tay", new Vector3(-0.04f, 0.30f, LabZ / 2f), new Vector3(0.08f, 0.60f, LabZ + 0.6f), matGranite, false);

            // 2. Gờ mái nhôm và máng thu nước mưa âm chạy dọc mép trên cùng mái tòa nhà (y = totalH)
            Box(g, "HD_GoMaiNhom_Nam", new Vector3(StoreX / 2f, totalH + 0.08f, -0.06f), new Vector3(StoreX + 1.0f, 0.16f, 0.18f), matMangNuoc, false);
            Box(g, "HD_GoMaiNhom_Bac", new Vector3(StoreX / 2f, totalH + 0.08f, LabZ + 0.06f), new Vector3(StoreX + 1.0f, 0.16f, 0.18f), matMangNuoc, false);
            Box(g, "HD_GoMaiNhom_Tay", new Vector3(-0.06f, totalH + 0.08f, LabZ / 2f), new Vector3(0.18f, 0.16f, LabZ + 1.0f), matMangNuoc, false);

            // 3. Gờ phào nổi định hình bao quanh các cụm cửa sổ chính (không đè lên cửa đi x = 1.6..3.2)
            for (float x = 2.5f; x < LabX - 2f; x += 4.5f)
            {
                if (x >= 1.4f && x <= 3.4f) continue;
                Box(g, $"HD_PhaoVienCuaSo_{x:0.0}", new Vector3(x, 1.8f, -0.02f), new Vector3(1.9f, 2.1f, 0.04f), matGopCua, false);
            }
        }
    }
}
