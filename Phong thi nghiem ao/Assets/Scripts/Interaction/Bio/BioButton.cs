using System;
using Khkt.Environment;
using UnityEngine;

namespace Khkt.Interaction.Bio
{
    /// <summary>
    /// Nút bấm cơ học hoặc công tắc bật/tắt 3D cho thiết bị sinh học.
    /// Khi bấm có hoạt cảnh lún nút 4 mm trong 0,12 s theo Easing.StdDecel và đổi màu đèn chỉ thị.
    /// </summary>
    public class BioButton : MonoBehaviour
    {
        public string buttonId = "";
        public string label = "Nút bấm";
        public bool isToggle = false;
        public bool isOn = false;
        public bool locked = false;
        public string lockedReason = "Nút đang khóa an toàn";

        public Vector3 pressOffset = new Vector3(0f, -0.004f, 0f);
        public Renderer indicatorRenderer;
        public Color onColor = new Color(0.18f, 0.92f, 0.42f);
        public Color offColor = new Color(0.28f, 0.30f, 0.32f);

        public Action onPressed;
        public Action<bool> onToggled;

        Vector3 baseLocalPos;
        float animTimer = -1f;
        bool initialized;
        MaterialPropertyBlock mpb;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        void Awake()
        {
            InitIfNeeded();
        }

        void InitIfNeeded()
        {
            if (initialized) return;
            initialized = true;
            baseLocalPos = transform.localPosition;
            UpdateVisualState();
        }

        public void Press()
        {
            InitIfNeeded();
            if (locked)
            {
                BioFeedback.PlayWarning();
                BioFeedback.ShowToast(lockedReason, true, 3.0f);
                return;
            }

            BioFeedback.PlayButton();
            animTimer = 0f;

            if (isToggle)
            {
                isOn = !isOn;
                UpdateVisualState();
                onToggled?.Invoke(isOn);
            }
            onPressed?.Invoke();
        }

        public void SetState(bool state, bool notify = true)
        {
            InitIfNeeded();
            bool changed = isOn != state;
            isOn = state;
            UpdateVisualState();
            if (changed && notify)
            {
                onToggled?.Invoke(isOn);
            }
        }

        public string GetPromptText()
        {
            if (!isToggle) return $"Bấm: {label}";
            return $"{label}: {(isOn ? "Đang BẬT" : "Đang TẮT")} (Chuột trái để {(isOn ? "tắt" : "bật")})";
        }

        void UpdateVisualState()
        {
            if (indicatorRenderer == null) return;
            mpb ??= new MaterialPropertyBlock();
            indicatorRenderer.GetPropertyBlock(mpb);
            Color c = isOn ? onColor : offColor;
            mpb.SetColor(BaseColorId, c);
            mpb.SetColor(EmissionColorId, isOn ? onColor * 1.8f : Color.black);
            indicatorRenderer.SetPropertyBlock(mpb);
        }

        void Update()
        {
            if (animTimer < 0f) return;
            animTimer += Time.deltaTime;
            const float totalDur = 0.14f;
            if (animTimer >= totalDur)
            {
                animTimer = -1f;
                transform.localPosition = isToggle && isOn ? baseLocalPos + pressOffset * 0.5f : baseLocalPos;
                return;
            }

            float half = totalDur * 0.5f;
            float t = animTimer < half ? Easing.Standard(animTimer / half) : 1f - Easing.Standard((animTimer - half) / half);
            transform.localPosition = baseLocalPos + pressOffset * t;
        }
    }
}

