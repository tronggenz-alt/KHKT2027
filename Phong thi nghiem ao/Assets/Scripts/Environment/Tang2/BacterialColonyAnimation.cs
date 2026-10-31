using Khkt.Simulation.Bio;
using UnityEngine;

namespace Khkt.Environment.Tang2
{
    public class BacterialColonyAnimation : MonoBehaviour
    {
        [Header("Thông số khuẩn lạc")]
        public Transform[] colonies;
        public float durationHours = 24f;
        public float currentHour = 0f;
        public float playbackSpeed = 2.0f; // 1 giây thực = 2 giờ ảo
        public bool isGrowing = true;

        void Update()
        {
            if (!isGrowing) return;

            currentHour += Time.deltaTime * playbackSpeed;
            if (currentHour > durationHours) currentHour = durationHours;

            // Tính tỷ lệ kích thước theo mô hình logistic E. coli K-12
            double fraction = ColonyGrowth.ColonyGrowthFraction(currentHour);

            if (colonies != null)
            {
                for (int i = 0; i < colonies.Length; i++)
                {
                    if (colonies[i] == null) continue;
                    // Mỗi khuẩn lạc có độ trễ nhẹ
                    float lagOffset = (i * 0.5f);
                    double colFraction = ColonyGrowth.ColonyGrowthFraction(Mathf.Max(0f, currentHour - lagOffset));
                    float s = (float)colFraction * 0.025f; // Bán kính tối đa 2.5 cm
                    colonies[i].localScale = new Vector3(s, s * 0.2f, s);
                }
            }
        }

        public void SetTimeHour(float hour)
        {
            currentHour = Mathf.Clamp(hour, 0f, durationHours);
        }
    }
}
