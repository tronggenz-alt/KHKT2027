using System;
using System.Collections.Generic;
using UnityEngine;

namespace Khkt.Interaction.Bio
{
    /// <summary>
    /// Vùng đổi nhiệt độ quanh một thiết bị (bể ổn nhiệt, tủ ấm, tủ lạnh, ngọn lửa): bình đặt trong vùng nóng lên, lạnh đi thật,
    /// không phải bấm "ủ mẫu". Tủ ấm còn tua nhanh thời gian để quá trình hàng giờ xem được trong buổi học.
    /// </summary>
    public class BioThermalZone : MonoBehaviour
    {
        public float radiusM = 0.4f;
        /// <summary>Tốc độ tiến về nhiệt độ vùng (1/giây).</summary>
        public float rate = 0.12f;
        public double timeScale = 60;
        public string zoneName = "";
        public Func<float> temperature = () => 25f;
        public Func<bool> isOn = () => true;

        public static readonly List<BioThermalZone> All = new List<BioThermalZone>();

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        /// <summary>Vùng gần nhất chứa điểm p; null nếu ở ngoài (nhiệt độ phòng).</summary>
        public static BioThermalZone At(Vector3 p)
        {
            BioThermalZone best = null;
            float bestD = float.MaxValue;
            foreach (var z in All)
            {
                if (z == null || !z.isOn()) continue;
                float d = (z.transform.position - p).sqrMagnitude;
                if (d < z.radiusM * z.radiusM && d < bestD) { bestD = d; best = z; }
            }
            return best;
        }
    }
}
