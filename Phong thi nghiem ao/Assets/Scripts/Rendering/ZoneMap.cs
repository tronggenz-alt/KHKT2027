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
        }

        public const int Outside = -1;
        public const string SceneFolder = "Assets/Scenes/Khu";

        public static readonly Zone[] Zones =
        {
            new Zone { id = "phong_lab", name = "Phòng thí nghiệm", scene = "Khu_PhongLab", area = Rect.MinMaxRect(0f, 0f, 20f, 15f) },
            new Zone { id = "kho_hoa_chat", name = "Phòng hóa chất", scene = "Khu_KhoHoaChat", area = Rect.MinMaxRect(20f, 0f, 27f, 15f) },
            new Zone { id = "hanh_lang", name = "Hành lang", scene = "Khu_HanhLang", area = Rect.MinMaxRect(0f, -2.2f, 27f, 0f) },
            new Zone { id = "phong_may", name = "Phòng máy phân tích", scene = "Khu_PhongMay", area = Rect.MinMaxRect(0f, -9f, 13.5f, -2.2f) },
            new Zone { id = "phong_robot", name = "Phòng tự động hóa & robot", scene = "Khu_PhongRobot", area = Rect.MinMaxRect(13.5f, -9f, 27f, -2.2f) },
        };

        public const int Lab = 0, Store = 1, Corridor = 2, Analysis = 3, Robotics = 4;

        /// <summary>Khu chứa điểm p (theo mặt sàn), Outside nếu ở ngoài (hiên, sân).</summary>
        public static int At(Vector3 p)
        {
            var q = new Vector2(p.x, p.z);
            for (int i = 0; i < Zones.Length; i++)
                if (Zones[i].area.Contains(q)) return i;
            return Outside;
        }

        public static string ScenePath(int zone) => $"{SceneFolder}/{Zones[zone].scene}.unity";

        public static string Name(int zone) => zone >= 0 && zone < Zones.Length ? Zones[zone].name : "Ngoài trời";
    }
}
