using Khkt.Input;
using UnityEngine;

namespace Khkt.Interaction
{
    /// <summary>
    /// Cho phép "cúi xuống" dù kính chỉ có 3DoF (không đo được đầu di chuyển lên xuống).
    ///
    /// 1. Mô hình cổ (neck model): mắt nằm trước-trên khớp cổ, nên cúi đầu thì mắt tự hạ xuống
    ///    và nhô ra trước vài cm như thật. Tắt nếu runtime (PhoneVR/SteamVR) đã làm việc này.
    /// 2. Cúi người: kéo joystick chỉnh tinh xuống (khi không vặn khóa buret) để hạ tầm mắt chậm,
    ///    đủ để nhìn ngang vạch mặt khum. Đẩy lên để ngồi thẳng lại.
    /// 3. Nếu webcam thấy marker trên kính (UdpPoseReceiver), dùng độ cao đầu thật thay cho 1 và 2.
    ///
    /// Gắn lên Camera Offset của XR Origin.
    /// </summary>
    public class EyeHeightController : MonoBehaviour
    {
        public ControllerSource source;
        public Transform head;
        [Tooltip("Không cho cúi khi đang vặn các khóa này (joystick đang dùng để chỉnh tinh)")]
        public BuretStopcock[] blockers;

        [Header("Ngồi")]
        public float seatedEyeHeight = 1.2f;
        public float maxLean = 0.35f;
        [Tooltip("Tốc độ cúi (m/s). Chậm để tránh say")]
        public float leanSpeed = 0.2f;

        [Header("Mô hình cổ")]
        public bool neckModel = true;
        public Vector3 neckToEye = new Vector3(0, 0.075f, 0.08f);

        [Header("Webcam (tùy chọn)")]
        public UdpPoseReceiver externalTracking;
        public string headId = "dau";

        public float Lean { get; private set; }

        void LateUpdate()
        {
            bool blocked = false;
            if (blockers != null)
                foreach (var b in blockers)
                    if (b != null && b.IsTurning) blocked = true;

            if (!blocked && source != null)
                Lean = Mathf.Clamp(Lean - source.Latest.Joy1.y * leanSpeed * Time.deltaTime, 0, maxLean);

            Vector3 offset = new Vector3(0, seatedEyeHeight - Lean, 0);
            if (neckModel && head != null)
                offset += head.localRotation * neckToEye - neckToEye;

            if (externalTracking != null && externalTracking.TryGetPosition(headId, 0.2f, out Vector3 p) && head != null)
            {
                // webcam cho vị trí đầu thật: dời Camera Offset sao cho camera trùng vị trí đó
                Vector3 desired = p - (head.localPosition);
                offset = new Vector3(desired.x, seatedEyeHeight + desired.y, desired.z);
            }
            transform.localPosition = offset;
        }
    }
}
