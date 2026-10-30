using Khkt.Simulation.Bio;
using UnityEngine;

namespace Khkt.Environment.Tang2
{
    public class GelMigrationAnimation : MonoBehaviour
    {
        [Header("Thông số điện di")]
        public Transform[] dnaBands;
        public int[] bandBasePairs; // Ví dụ: 1000, 750, 500, 250, 100 bp
        public Transform dyeFront;   // Vạch thuốc nhuộm xanh chạy đầu

        public float voltage = 100f; // 100 V
        public float runTimeMinutes = 0f;
        public float speedMultiplier = 10.0f; // 1 giây thực = 10 phút ảo
        public bool isRunning = true;

        Vector3[] initialPositions;
        Vector3 initialDyePos;

        void Start()
        {
            if (dnaBands != null)
            {
                initialPositions = new Vector3[dnaBands.Length];
                for (int i = 0; i < dnaBands.Length; i++)
                {
                    if (dnaBands[i] != null) initialPositions[i] = dnaBands[i].localPosition;
                }
            }
            if (dyeFront != null) initialDyePos = dyeFront.localPosition;
        }

        void Update()
        {
            if (!isRunning) return;

            runTimeMinutes += (Time.deltaTime * speedMultiplier) / 60f;

            if (dnaBands != null && bandBasePairs != null)
            {
                for (int i = 0; i < dnaBands.Length; i++)
                {
                    if (dnaBands[i] == null || i >= bandBasePairs.Length) continue;
                    int bp = bandBasePairs[i];
                    double distCm = Electrophoresis.MigrationDistanceCm(bp, voltage, runTimeMinutes);
                    float distMeters = (float)(distCm * 0.01);
                    dnaBands[i].localPosition = initialPositions[i] + new Vector3(0, 0, -distMeters);
                }
            }

            if (dyeFront != null)
            {
                double dyeDistCm = Electrophoresis.TrackingDyeDistanceCm(voltage, runTimeMinutes);
                float dyeDistMeters = (float)(dyeDistCm * 0.01);
                dyeFront.localPosition = initialDyePos + new Vector3(0, 0, -dyeDistMeters);
            }
        }
    }
}
