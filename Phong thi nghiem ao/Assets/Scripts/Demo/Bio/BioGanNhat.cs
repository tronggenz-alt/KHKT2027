using Khkt.Interaction.Bio;
using UnityEngine;

namespace Khkt.Demo.Bio
{
    /// <summary>
    /// Tra trạng thái thật quanh một dụng cụ (bình gần nhất, nhiệt độ vùng, đèn cồn, nồi hấp) để thao tác bấm trực tiếp
    /// lên dụng cụ cho kết quả theo cái đang có trên bàn, không theo số dựng sẵn.
    /// </summary>
    public static class BioGanNhat
    {
        public const float NhietDoPhong = 25f;

        public static T Tim<T>(Vector3 p, float banKinh, System.Func<T, bool> dieuKien = null) where T : Component
        {
            T tot = null;
            float d0 = banKinh * banKinh;
            foreach (var c in Object.FindObjectsByType<T>(FindObjectsSortMode.None))
            {
                if (c == null || !c.gameObject.activeInHierarchy) continue;
                if (dieuKien != null && !dieuKien(c)) continue;
                float d = (c.transform.position - p).sqrMagnitude;
                if (d < d0) { d0 = d; tot = c; }
            }
            return tot;
        }

        /// <summary>Nhiệt độ không khí / vùng (bể ổn nhiệt, tủ ấm, tủ lạnh, bếp) tại điểm p.</summary>
        public static float NhietDoTai(Vector3 p)
        {
            var z = BioThermalZone.At(p);
            return z != null ? z.temperature() : NhietDoPhong;
        }

        /// <summary>Số giây mô phỏng ứng với dt giây thật tại điểm p (ngoài bàn 1 s = 1 phút, trong tủ ấm nhanh hơn).</summary>
        public static double GiayMoPhong(Vector3 p, float dt)
        {
            var z = BioThermalZone.At(p);
            return dt * (z != null ? z.timeScale : 60.0);
        }
    }
}
