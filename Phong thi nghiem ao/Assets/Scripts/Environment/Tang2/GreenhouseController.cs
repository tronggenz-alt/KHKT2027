using System;
using UnityEngine;

namespace Khkt.Environment.Tang2
{
    /// <summary>
    /// Điều khiển hệ thống vi khí hậu thông minh cho Nhà Màng Sinh Học (Greenhouse):
    /// - Quản lý phun sương ẩm (Particle/Mist effect), tăng độ ẩm 62% -> 85%, giảm nhiệt 29.5°C -> 25.0°C.
    /// - Quản lý giàn đèn quang phổ sinh trưởng PAR (đỏ - xanh, ánh sáng tím hồng), tăng PAR 450 -> 1200 µmol/m²/s.
    /// - Cập nhật bảng LED thông số thời gian thực và tương tác một chạm.
    /// </summary>
    public class GreenhouseController : MonoBehaviour
    {
        [Header("Trạng thái vi khí hậu")]
        public bool isMisting = false;
        public bool isParLights = false;

        public float currentTemp = 29.5f;
        public float currentHumidity = 62.0f;
        public float currentLightPar = 450.0f;

        [Header("Tham chiếu đối tượng")]
        public GameObject mistSystem;
        public GameObject[] parGrowLeds;
        public TextMesh statusTextMesh;

        const float BaseTemp = 29.5f;
        const float MistTemp = 25.0f;
        const float BaseHumidity = 62.0f;
        const float MistHumidity = 85.0f;
        const float BasePar = 450.0f;
        const float ActivePar = 1200.0f;

        Transform playerCam;
        bool registeredHud = false;
        float updateTimer = 0f;

        void Start()
        {
            ApplyStates(instant: true);
            UpdateDisplay();
        }

        void Update()
        {
            float dt = Time.deltaTime;

            // Chuyển dịch mượt mà các chỉ số vi khí hậu
            float targetTemp = isMisting ? MistTemp : BaseTemp;
            float targetHumidity = isMisting ? MistHumidity : BaseHumidity;
            float targetPar = isParLights ? ActivePar : BasePar;

            currentTemp = Mathf.MoveTowards(currentTemp, targetTemp, dt * 1.8f);
            currentHumidity = Mathf.MoveTowards(currentHumidity, targetHumidity, dt * 8.0f);
            currentLightPar = Mathf.MoveTowards(currentLightPar, targetPar, dt * 350.0f);

            updateTimer += dt;
            if (updateTimer >= 0.2f)
            {
                updateTimer = 0f;
                UpdateDisplay();
            }

            // Quản lý QuickActionDock trong TouchHud khi người chơi vào khu nhà kính
            CheckPlayerProximity();
        }

        void CheckPlayerProximity()
        {
            if (playerCam == null && Camera.main != null) playerCam = Camera.main.transform;
            if (playerCam == null) return;

            float dist = Vector3.Distance(playerCam.position, transform.position);
            bool insideGreenhouse = dist < 9.0f;

            if (insideGreenhouse && !registeredHud)
            {
                registeredHud = true;
                UpdateHudButtons();
            }
            else if (!insideGreenhouse && registeredHud)
            {
                registeredHud = false;
                // Trả lại các ô trống mặc định
                if (Demo.TouchHud.Instance != null)
                {
                    Demo.TouchHud.Instance.RegisterQuickActionSlot(0, "Mở +", null, false);
                    Demo.TouchHud.Instance.RegisterQuickActionSlot(1, "Đóng -", null, false);
                }
            }
        }

        void UpdateHudButtons()
        {
            if (!registeredHud) return;
            string mistLabel = isMisting ? "Tắt Sương" : "Bật Sương";
            string parLabel = isParLights ? "Tắt Đèn PAR" : "Bật Đèn PAR";

            if (Demo.TouchHud.Instance != null)
            {
                Demo.TouchHud.Instance.RegisterQuickActionSlot(0, mistLabel, ToggleMisting, isMisting);
                Demo.TouchHud.Instance.RegisterQuickActionSlot(1, parLabel, ToggleParLights, isParLights);
            }
        }

        public void ToggleMisting()
        {
            isMisting = !isMisting;
            ApplyStates(instant: false);
            UpdateHudButtons();
            UpdateDisplay();
        }

        public void ToggleParLights()
        {
            isParLights = !isParLights;
            ApplyStates(instant: false);
            UpdateHudButtons();
            UpdateDisplay();
        }

        void ApplyStates(bool instant)
        {
            if (mistSystem != null)
            {
                mistSystem.SetActive(isMisting);
            }

            if (parGrowLeds != null)
            {
                foreach (var led in parGrowLeds)
                {
                    if (led != null) led.SetActive(isParLights);
                }
            }
        }

        void UpdateDisplay()
        {
            if (statusTextMesh == null) return;

            string mistStatus = isMisting ? "<color=#2da0b9>BẬT</color>" : "<color=#888888>TẮT</color>";
            string parStatus = isParLights ? "<color=#e060c0>BẬT</color>" : "<color=#888888>TẮT</color>";

            statusTextMesh.text =
                $"<b>VI KHÍ HẬU NHÀ MÀNG SINH HỌC</b>\n" +
                $"Nhiệt độ: {currentTemp:F1} °C  ·  Độ ẩm: {currentHumidity:F0} %\n" +
                $"Quang hợp PAR: {currentLightPar:F0} µmol/m²/s\n" +
                $"Phun sương: {mistStatus}  ·  Đèn PAR: {parStatus}";
        }

        void OnMouseDown()
        {
            // Bấm vào bảng điều khiển sẽ tuần tự bật/tắt phun sương
            ToggleMisting();
        }
    }
}

