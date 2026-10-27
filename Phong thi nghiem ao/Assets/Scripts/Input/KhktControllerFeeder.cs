using UnityEngine;
using UnityEngine.InputSystem;

namespace Khkt.Input
{
    /// <summary>Tạo thiết bị KhktController và cập nhật nó từ ControllerSource mỗi khung hình.</summary>
    [DefaultExecutionOrder(-90)]
    public class KhktControllerFeeder : MonoBehaviour
    {
        public ControllerSource source;
        KhktController device;

        void OnEnable() => device = InputSystem.AddDevice<KhktController>();

        void OnDisable()
        {
            if (device != null) InputSystem.RemoveDevice(device);
            device = null;
        }

        void Update()
        {
            if (device == null || source == null) return;
            var s = source.Latest;
            InputSystem.QueueStateEvent(device, new KhktControllerInputState
            {
                move = s.Joy2,
                fine = s.Joy1,
                tilt = s.TiltDeg / 180f,
                buttons = (uint)s.Buttons,
            });
        }
    }
}
