using System;
using Khkt.Environment;
using UnityEngine;

namespace Khkt.Interaction.Bio
{
    /// <summary>
    /// Nắp bản lề / Cửa mở / Cửa kính trượt của thiết bị sinh học (Tủ ấm, Tủ lạnh 4 °C / -80 °C,
    /// Máy PCR, Máy ly tâm, Nồi hấp tiệt trùng, Bàn soi UV, Hộp đầu côn, Lamen 45°, Cửa trượt BSC).
    /// Hỗ trợ khóa an toàn (isLocked) với thông báo giải thích khoa học khi người học cố mở lúc đang nguy hiểm.
    /// </summary>
    public class BioHinge : MonoBehaviour
    {
        public string hingeId = "";
        public string label = "Nắp thiết bị";
        public bool isSliding = false;

        public Vector3 hingeAxis = Vector3.right;
        public float closedAngle = 0f;
        public float openAngle = -90f;

        public Vector3 closedLocalPos;
        public Vector3 openLocalPos = new Vector3(0f, 0.25f, 0f);

        public float duration = 0.35f;
        public bool isOpen = false;
        public bool isLocked = false;
        public string lockedEquipmentId = "";
        public string lockedMistakeCode = "mo_nap_khi_dang_chay";
        public string lockedReason = "Nắp đang khóa an toàn khi thiết bị hoạt động.";
        public string lockedScientificReason = "Mở nắp khi thiết bị đang quay hoặc còn áp suất cao gây nguy cơ tai nạn sinh học/bỏng hơi nước.";

        public Func<bool> canToggle;
        public Action<bool> onStateChanged;

        Quaternion baseLocalRot;
        float progress01;
        bool initialized;

        void Awake()
        {
            InitIfNeeded();
        }

        void InitIfNeeded()
        {
            if (initialized) return;
            initialized = true;
            baseLocalRot = transform.localRotation;
            if (closedLocalPos == Vector3.zero && !isSliding)
                closedLocalPos = transform.localPosition;
            progress01 = isOpen ? 1f : 0f;
            ApplyPose(progress01);
        }

        public bool Toggle()
        {
            InitIfNeeded();
            if (isLocked)
            {
                if (!string.IsNullOrEmpty(lockedEquipmentId))
                    BioFeedback.RecordMistake(lockedEquipmentId, lockedMistakeCode, lockedReason, lockedScientificReason);
                else
                    BioFeedback.ShowToast(lockedReason, true, 4.0f, lockedScientificReason);
                return false;
            }
            if (canToggle != null && !canToggle())
                return false;

            SetOpen(!isOpen, false);
            return true;
        }

        public void SetOpen(bool open, bool force = false)
        {
            InitIfNeeded();
            if (!force && isLocked && open)
            {
                if (!string.IsNullOrEmpty(lockedEquipmentId))
                    BioFeedback.RecordMistake(lockedEquipmentId, lockedMistakeCode, lockedReason, lockedScientificReason);
                else
                    BioFeedback.ShowToast(lockedReason, true, 4.0f, lockedScientificReason);
                return;
            }

            if (isOpen == open) return;
            isOpen = open;
            BioFeedback.PlayButton();
            onStateChanged?.Invoke(isOpen);
        }

        public string GetPromptText()
        {
            if (isLocked) return $"{label} (Đang khóa an toàn)";
            return $"{(isOpen ? "Đóng" : "Mở")}: {label} (Chuột trái)";
        }

        void ApplyPose(float t01)
        {
            float e = Easing.EmphasizedDecelerate(Mathf.Clamp01(t01));
            if (isSliding)
            {
                transform.localPosition = Vector3.Lerp(closedLocalPos, openLocalPos, e);
            }
            else
            {
                float deg = Mathf.Lerp(closedAngle, openAngle, e);
                transform.localRotation = baseLocalRot * Quaternion.AngleAxis(deg, hingeAxis.normalized);
            }
        }

        void Update()
        {
            if (!initialized) InitIfNeeded();
            float target = isOpen ? 1f : 0f;
            if (!Mathf.Approximately(progress01, target))
            {
                progress01 = Mathf.MoveTowards(progress01, target, Time.deltaTime / Mathf.Max(0.05f, duration));
                ApplyPose(progress01);
            }
        }
    }
}
