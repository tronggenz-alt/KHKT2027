using Khkt.Input;
using UnityEngine;
using UnityEngine.Events;

namespace Khkt.Interaction
{
    /// <summary>
    /// Đánh dấu dụng cụ đang được cầm và bởi tay cầm nào.
    /// Nối với XRGrabInteractable: Select Entered -> Grab(), Select Exited -> Release().
    /// </summary>
    public class HeldTool : MonoBehaviour
    {
        [Tooltip("Tay cầm mặc định khi Grab() được gọi không tham số (từ sự kiện XRI)")]
        public ControllerSource defaultSource;
        public HapticFeedback haptics;
        public UnityEvent<bool> heldChanged;

        public bool IsHeld { get; private set; }
        public ControllerSource Holder { get; private set; }

        public void Grab() => Grab(defaultSource);

        public void Grab(ControllerSource source)
        {
            IsHeld = true;
            Holder = source;
            if (haptics != null) haptics.GrabConfirm();
            heldChanged?.Invoke(true);
        }

        public void Release()
        {
            IsHeld = false;
            Holder = null;
            heldChanged?.Invoke(false);
        }
    }
}
