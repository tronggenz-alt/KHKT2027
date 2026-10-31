using System;
using UnityEngine;

namespace Khkt.Interaction.Bio
{
    /// <summary>
    /// Núm vặn cơ học 3D cho thiết bị sinh học (ốc sơ cấp/vi cấp kính hiển vi, núm nhiệt độ tủ ấm/bể điều nhiệt,
    /// núm chỉnh tốc độ máy khuấy từ, vòng chỉnh thể tích micropipet...).
    /// Xoay bằng con lăn chuột (scroll wheel) khi ngắm vào hoặc gọi trực tiếp từ mã kiểm thử.
    /// </summary>
    public class BioKnob : MonoBehaviour
    {
        public string knobId = "";
        public string label = "Núm chỉnh";
        public string unit = "";
        public float minValue = 0f;
        public float maxValue = 100f;
        public float step = 1f;
        public float value = 25f;
        public string valueFormat = "0.0";

        public Vector3 rotationAxis = Vector3.up;
        public float minAngle = -135f;
        public float maxAngle = 135f;
        public bool locked = false;
        public string lockedReason = "Núm chỉnh đang khóa";

        public TextMesh digitalReadout;
        public Action<float> onValueChanged;

        Quaternion baseRotation;
        float currentVisualAngle;
        float targetVisualAngle;
        bool initialized;

        void Awake()
        {
            InitIfNeeded();
        }

        void InitIfNeeded()
        {
            if (initialized) return;
            initialized = true;
            baseRotation = transform.localRotation;
            float t = Mathf.InverseLerp(minValue, maxValue, value);
            currentVisualAngle = targetVisualAngle = Mathf.Lerp(minAngle, maxAngle, t);
            ApplyVisualRotation(currentVisualAngle);
            UpdateReadout();
        }

        public void SetValue(float newValue, bool notify = true)
        {
            InitIfNeeded();
            if (locked)
            {
                BioFeedback.ShowToast(lockedReason, true, 2.5f);
                return;
            }
            float clamped = Mathf.Clamp(newValue, minValue, maxValue);
            if (step > 0.0001f)
            {
                float steps = Mathf.Round((clamped - minValue) / step);
                clamped = Mathf.Clamp(minValue + steps * step, minValue, maxValue);
            }
            bool changed = !Mathf.Approximately(value, clamped);
            value = clamped;
            float t = Mathf.InverseLerp(minValue, maxValue, value);
            targetVisualAngle = Mathf.Lerp(minAngle, maxAngle, t);
            UpdateReadout();

            if (changed && notify)
            {
                BioFeedback.PlayClick();
                onValueChanged?.Invoke(value);
            }
        }

        public void AdjustSteps(int deltaSteps)
        {
            if (deltaSteps == 0) return;
            SetValue(value + deltaSteps * Mathf.Max(0.0001f, step), true);
        }

        public string FormattedValue()
        {
            string num = BioFeedback.FormatVi(value, valueFormat);
            return string.IsNullOrEmpty(unit) ? num : $"{num} {unit}";
        }

        public string GetPromptText()
        {
            return $"{label}: {FormattedValue()} (Cuộn chuột để chỉnh)";
        }

        void UpdateReadout()
        {
            if (digitalReadout != null)
                digitalReadout.text = FormattedValue();
        }

        void ApplyVisualRotation(float deg)
        {
            transform.localRotation = baseRotation * Quaternion.AngleAxis(deg, rotationAxis.normalized);
        }

        void Update()
        {
            if (!initialized) InitIfNeeded();
            if (!Mathf.Approximately(currentVisualAngle, targetVisualAngle))
            {
                currentVisualAngle = Mathf.MoveTowards(currentVisualAngle, targetVisualAngle, Time.deltaTime * 540f);
                ApplyVisualRotation(currentVisualAngle);
            }
        }
    }
}

