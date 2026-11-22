using System;
using UnityEngine;

namespace Khkt.Environment.Tang2
{
    /// <summary>
    /// Chậu cây thực nghiệm sinh học có khả năng tương tác và phát triển qua 4 giai đoạn sinh trưởng:
    /// 1. Hạt nảy mầm (Germination)
    /// 2. Cây con 2 lá mầm (Seedling)
    /// 3. Cây sinh trưởng tán lá (Vegetative)
    /// 4. Cây ra hoa (Flowering)
    /// Hỗ trợ tưới nước (giữ ẩm đất), bón phân khoáng NPK, hiển thị thông số 3D nổi và phản hồi tương tác.
    /// </summary>
    public class GrowablePlant : MonoBehaviour
    {
        [Header("Giai đoạn sinh trưởng (0..3)")]
        [Range(0, 3)] public int currentStage = 1;
        public float growthProgress = 15f; // 0..100%

        [Header("Độ ẩm đất & Dinh dưỡng")]
        public float soilMoisture = 60f; // 0..100%
        public float npkLevel = 55f;     // 0..100%

        [Header("Mô hình tương ứng 4 giai đoạn")]
        public GameObject[] stageObjects = new GameObject[4];
        public Renderer soilRenderer;
        public TextMesh calloutTextMesh;

        static readonly string[] StageNames = {
            "1. Hạt nảy mầm",
            "2. Cây con 2 lá mầm",
            "3. Phát triển tán lá",
            "4. Cây ra hoa kết quả"
        };

        static readonly Color MoistSoilColor = new Color(0.14f, 0.09f, 0.05f);
        static readonly Color DrySoilColor = new Color(0.38f, 0.28f, 0.18f);

        Material soilMatInstance;
        float updateTimer = 0f;
        Transform camTransform;

        void Start()
        {
            if (soilRenderer != null)
            {
                soilMatInstance = soilRenderer.material;
            }

            UpdateStageVisibility();
            UpdateSoilVisual();
            UpdateCalloutText();
        }

        void Update()
        {
            float dt = Time.deltaTime;

            // Đất khô dần theo thời gian (thoát hơi nước)
            if (soilMoisture > 5f)
            {
                soilMoisture = Mathf.Max(0f, soilMoisture - dt * 0.4f);
            }

            // Sinh trưởng khi đủ ẩm và đủ dinh dưỡng
            if (soilMoisture > 25f && npkLevel > 15f)
            {
                float rate = (soilMoisture / 100f) * (npkLevel / 100f) * 1.5f;
                growthProgress += dt * rate;
                npkLevel = Mathf.Max(0f, npkLevel - dt * 0.15f);

                if (growthProgress >= 100f)
                {
                    growthProgress = 0f;
                    if (currentStage < 3)
                    {
                        currentStage++;
                        UpdateStageVisibility();
                    }
                    else
                    {
                        // Cây hoàn thiện chu trình sinh trưởng
                        growthProgress = 100f;
                    }
                }
            }

            updateTimer += dt;
            if (updateTimer >= 0.25f)
            {
                updateTimer = 0f;
                UpdateSoilVisual();
                UpdateCalloutText();
            }

            // Billboard hướng chữ về phía Camera
            if (calloutTextMesh != null)
            {
                if (camTransform == null && Camera.main != null) camTransform = Camera.main.transform;
                if (camTransform != null)
                {
                    Vector3 dir = (camTransform.position - calloutTextMesh.transform.position);
                    dir.y = 0;
                    if (dir.sqrMagnitude > 0.001f)
                    {
                        calloutTextMesh.transform.rotation = Quaternion.LookRotation(dir);
                    }
                }
            }
        }

        /// <summary>Tưới nước vào chậu: phục hồi độ ẩm và làm đậm màu đất ẩm.</summary>
        public void Water(float amount = 35f)
        {
            soilMoisture = Mathf.Clamp(soilMoisture + amount, 0f, 100f);
            UpdateSoilVisual();
            UpdateCalloutText();
        }

        /// <summary>Bón dung dịch dinh dưỡng khoáng NPK.</summary>
        public void Fertilize(float amount = 30f)
        {
            npkLevel = Mathf.Clamp(npkLevel + amount, 0f, 100f);
            UpdateCalloutText();
        }

        public void SetStage(int stage)
        {
            currentStage = Mathf.Clamp(stage, 0, 3);
            growthProgress = 0f;
            UpdateStageVisibility();
            UpdateCalloutText();
        }

        void UpdateStageVisibility()
        {
            for (int i = 0; i < stageObjects.Length; i++)
            {
                if (stageObjects[i] != null)
                {
                    stageObjects[i].SetActive(i == currentStage);
                }
            }
        }

        void UpdateSoilVisual()
        {
            if (soilMatInstance != null)
            {
                float t = Mathf.Clamp01(soilMoisture / 70f);
                soilMatInstance.color = Color.Lerp(DrySoilColor, MoistSoilColor, t);
            }
        }

        void UpdateCalloutText()
        {
            if (calloutTextMesh == null) return;

            string moistureWarning = soilMoisture < 25f ? " <color=#ff5555>(Cần tưới nước!)</color>" : "";
            string npkWarning = npkLevel < 20f ? " <color=#ffaa33>(Cần bón phân!)</color>" : "";

            calloutTextMesh.text =
                $"<b>{StageNames[currentStage]}</b>\n" +
                $"Tiến độ: {growthProgress:F0}%\n" +
                $"Độ ẩm đất: {soilMoisture:F0}%{moistureWarning}\n" +
                $"Khoáng NPK: {npkLevel:F0}%{npkWarning}\n" +
                $"<i>[Chạm vào chậu để chăm sóc]</i>";
        }

        void OnMouseDown()
        {
            // Chạm vào chậu: nếu thiếu nước thì tưới, nếu đủ nước thì bón phân, nếu đã tốt thì kích hoạt tiến độ
            if (soilMoisture < 65f)
            {
                Water(35f);
            }
            else if (npkLevel < 65f)
            {
                Fertilize(30f);
            }
            else
            {
                // Thúc đẩy chu trình sinh trưởng
                growthProgress = Mathf.Min(100f, growthProgress + 25f);
                if (growthProgress >= 100f && currentStage < 3)
                {
                    currentStage++;
                    growthProgress = 0f;
                    UpdateStageVisibility();
                }
            }
            UpdateCalloutText();
        }
    }
}

