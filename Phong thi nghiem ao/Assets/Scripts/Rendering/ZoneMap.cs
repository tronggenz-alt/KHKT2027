using UnityEngine;

namespace Khkt.Rendering
{
    /// <summary>
    /// Các khu của tòa nhà (mặt bằng trong LabSceneBuilder): mỗi khu là một cảnh riêng chứa phần tĩnh (tủ, kệ, trang trí, đèn),
    /// tải cộng dồn khi cần và gỡ khi rời đi. Vỏ tòa nhà (sàn, tường, trần, cửa), ngoài trời và mọi đồ tương tác nằm ở cảnh lõi.
    /// Trình dựng cảnh và ZoneStreamer dùng chung bảng này.
    /// </summary>
    public static class ZoneMap
    {
        public struct Zone
        {
            public string id, name, scene;
            /// <summary>Phạm vi trên mặt sàn: x là trục x, y là trục z của thế giới.</summary>
            public Rect area;
            /// <summary>Tầng: 0 = tầng 1, 1 = tầng 2, -1 = cả hai tầng (lõi thang).</summary>
            public int floor;
        }

        public const int Outside = -1;
        public const string SceneFolder = "Assets/Scenes/Khu";

        public static readonly Zone[] Zones =
        {
            new Zone { id = "phong_lab", name = "Phòng thí nghiệm", scene = "Khu_PhongLab", area = Rect.MinMaxRect(0f, 0f, 20f, 15f), floor = 0 },
            new Zone { id = "kho_hoa_chat", name = "Phòng hóa chất", scene = "Khu_KhoHoaChat", area = Rect.MinMaxRect(20f, 0f, 27f, 15f), floor = 0 },
            new Zone { id = "hanh_lang", name = "Hành lang", scene = "Khu_HanhLang", area = Rect.MinMaxRect(0f, -2.2f, 27f, 0f), floor = 0 },
            new Zone { id = "phong_may", name = "Phòng máy phân tích", scene = "Khu_PhongMay", area = Rect.MinMaxRect(0f, -9f, 13.5f, -2.2f), floor = 0 },
            new Zone { id = "phong_robot", name = "Phòng tự động hóa & robot", scene = "Khu_PhongRobot", area = Rect.MinMaxRect(13.5f, -9f, 27f, -2.2f), floor = 0 },

            // Tầng 2 (khu sinh học) - giữ nguyên chỉ số 0..4, thêm vào cuối 5..12
            new Zone { id = "phong_sinh", name = "Phòng thí nghiệm sinh học", scene = "Khu_PhongSinh", area = Rect.MinMaxRect(0f, 0f, 20f, 15f), floor = 1 },
            new Zone { id = "phong_vi_sinh", name = "Phòng vi sinh", scene = "Khu_PhongViSinh", area = Rect.MinMaxRect(20f, 7.5f, 27f, 15f), floor = 1 },
            new Zone { id = "kho_sinh_pham", name = "Kho sinh phẩm", scene = "Khu_KhoSinhPham", area = Rect.MinMaxRect(20f, 0f, 27f, 7.5f), floor = 1 },
            new Zone { id = "hanh_lang_t2", name = "Hành lang tầng 2", scene = "Khu_HanhLangT2", area = Rect.MinMaxRect(0f, -2.2f, 27f, 0f), floor = 1 },
            new Zone { id = "phong_sinh_phan_tu", name = "Phòng sinh học phân tử", scene = "Khu_SinhPhanTu", area = Rect.MinMaxRect(0f, -9f, 13.5f, -2.2f), floor = 1 },
            new Zone { id = "nha_kinh", name = "Nhà kính thực vật", scene = "Khu_NhaKinh", area = Rect.MinMaxRect(13.5f, -9f, 27f, -2.2f), floor = 1 },
            new Zone { id = "hanh_lang_dong", name = "Hành lang đông tầng 2", scene = "Khu_HanhLangDong", area = Rect.MinMaxRect(27f, 6f, 30f, 15f), floor = 1 },
            new Zone { id = "loi_thang", name = "Cầu thang và thang máy", scene = "Khu_LoiThang", area = Rect.MinMaxRect(27f, -2.2f, 30f, 6f), floor = -1 },
        };

        public const int Lab = 0, Store = 1, Corridor = 2, Analysis = 3, Robotics = 4;
        public const int BioLab = 5, MicroBio = 6, BioStore = 7, CorridorT2 = 8, Molecular = 9, Greenhouse = 10, EastCorridor = 11, Stairs = 12;

        /// <summary>Khu chứa điểm p (theo mặt sàn và độ cao tầng), Outside nếu ở ngoài (hiên, sân).</summary>
        public static int At(Vector3 p)
        {
            var q = new Vector2(p.x, p.z);
            int floor = p.y >= 3.6f ? 1 : 0;
            for (int i = 0; i < Zones.Length; i++)
            {
                if (Zones[i].floor != -1 && Zones[i].floor != floor) continue;
                if (Zones[i].area.Contains(q)) return i;
            }
            return Outside;
        }

        public static string ScenePath(int zone) => $"{SceneFolder}/{Zones[zone].scene}.unity";

        public static string Name(int zone) => zone >= 0 && zone < Zones.Length ? Zones[zone].name : "Ngoài trời";
    }
}
