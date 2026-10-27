using UnityEngine;

namespace Khkt.Input
{
    /// <summary>
    /// Đặt vị trí + hướng bàn tay ảo từ tay cầm 3DoF.
    ///
    /// IMU chỉ biết HƯỚNG, không biết VỊ TRÍ. Vị trí bàn tay được đoán bằng "mô hình cánh tay"
    /// (arm model, giống tay cầm Google Daydream): khuỷu tay ở một điểm cố định so với thân,
    /// cẳng tay dài forearm mét xoay theo hướng tay cầm. Nếu có webcam theo dõi marker
    /// (UdpPoseReceiver), vị trí thật thay cho vị trí đoán.
    ///
    /// Gắn script lên GameObject bàn tay, là con của Camera Offset trong XR Origin (cùng cha với camera).
    /// </summary>
    [DefaultExecutionOrder(-80)]
    public class ControllerPoseDriver : MonoBehaviour
    {
        public ControllerSource source;
        [Tooltip("Camera chính (Main Camera trong XR Origin)")]
        public Transform head;
        public bool rightHand = true;

        [Header("Mô hình cánh tay (mét, trong hệ thân người)")]
        public Vector3 elbowRest = new Vector3(0.2f, -0.45f, 0.15f);
        public float forearm = 0.3f;
        [Tooltip("Khuỷu tay nâng lên bao nhiêu khi chĩa tay cầm lên trời")]
        public float elbowLift = 0.08f;
        [Tooltip("Thân người xoay theo đầu nhanh cỡ nào (1/s)")]
        public float bodyFollowRate = 2f;

        [Header("Vị trí từ webcam (tùy chọn)")]
        public UdpPoseReceiver externalTracking;
        public string externalId = "tay";
        [Tooltip("Camera Offset của XR Origin. Cần khi EyeHeightController dời nó theo đầu, để vị trí tay webcam không bị cộng hai lần")]
        public Transform cameraOffset;

        Quaternion yawOffset = Quaternion.identity;
        float bodyYaw;
        Vector3 offsetAtRecenter;

        public Quaternion Rotation { get; private set; } = Quaternion.identity;
        public bool UsingExternalPosition { get; private set; }

        void Update()
        {
            if (source == null || head == null) return;
            if (source.ButtonDown(ControllerButtons.Recenter)) Recenter();

            Quaternion q = yawOffset * source.Latest.UnityRotation;
            float headYaw = head.localEulerAngles.y;
            bodyYaw = Mathf.LerpAngle(bodyYaw, headYaw, 1 - Mathf.Exp(-bodyFollowRate * Time.deltaTime));
            Quaternion body = Quaternion.Euler(0, bodyYaw, 0);

            Vector3 rest = elbowRest;
            if (!rightHand) rest.x = -rest.x;
            Vector3 forward = q * Vector3.forward;
            Vector3 elbow = head.localPosition + body * rest + Vector3.up * (Mathf.Max(0, forward.y) * elbowLift);
            Vector3 hand = elbow + q * new Vector3(0, 0, forearm);

            UsingExternalPosition = false;
            if (externalTracking != null && externalTracking.TryGetPosition(externalId, 0.2f, out Vector3 tracked))
            {
                hand = tracked;
                if (cameraOffset != null) hand -= cameraOffset.localPosition - offsetAtRecenter;
                UsingExternalPosition = true;
            }

            Rotation = q;
            transform.localPosition = hand;
            transform.localRotation = q;
        }

        /// <summary>
        /// Căn lại: xoay yaw của tay cầm cho trùng hướng đang nhìn. Người dùng nhìn thẳng,
        /// chĩa tay cầm thẳng về phía trước rồi bấm nút. Khắc phục trôi yaw của MPU6050.
        /// </summary>
        public void Recenter()
        {
            Vector3 f = source.Latest.UnityRotation * Vector3.forward;
            if (new Vector2(f.x, f.z).sqrMagnitude < 0.05f) return;  // đang chĩa thẳng lên/xuống: không xác định được yaw
            float controllerYaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
            yawOffset = Quaternion.Euler(0, head.localEulerAngles.y - controllerYaw, 0);
            if (externalTracking != null) externalTracking.SetOrigin(head.localPosition);
            if (cameraOffset != null) offsetAtRecenter = cameraOffset.localPosition;
        }
    }
}
