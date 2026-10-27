using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Khkt.Input
{
    /// <summary>Trạng thái tay cầm dưới dạng thiết bị của Input System.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 24)]
    public struct KhktControllerInputState : IInputStateTypeInfo
    {
        public FourCC format => new FourCC('K', 'H', 'K', 'T');

        [InputControl(name = "move", layout = "Stick", format = "VEC2", displayName = "Joystick di chuyển")]
        [FieldOffset(0)] public Vector2 move;

        [InputControl(name = "fine", layout = "Stick", format = "VEC2", displayName = "Joystick chỉnh tinh")]
        [FieldOffset(8)] public Vector2 fine;

        [InputControl(name = "tilt", layout = "Axis", displayName = "Độ nghiêng (0 = thẳng, 1 = úp ngược)")]
        [FieldOffset(16)] public float tilt;

        [InputControl(name = "trigger", layout = "Button", bit = 0, displayName = "Cò")]
        [InputControl(name = "recenter", layout = "Button", bit = 1, displayName = "Căn lại")]
        [InputControl(name = "askAI", layout = "Button", bit = 2, displayName = "Hỏi AI")]
        [InputControl(name = "fineClick", layout = "Button", bit = 3, displayName = "Nhấn joystick chỉnh tinh")]
        [InputControl(name = "moveClick", layout = "Button", bit = 4, displayName = "Nhấn joystick di chuyển")]
        [FieldOffset(20)] public uint buttons;
    }

    /// <summary>
    /// Thiết bị Input System "KHKT Tay cầm". Nhờ nó, XR Interaction Toolkit (chọn, cầm, dịch chuyển,
    /// xoay từng nấc) dùng được tay cầm tự chế qua Input Action bình thường, ví dụ
    /// &lt;KhktController&gt;/trigger, &lt;KhktController&gt;/move. Dữ liệu được KhktControllerFeeder đẩy vào.
    /// </summary>
#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    [InputControlLayout(stateType = typeof(KhktControllerInputState), displayName = "KHKT Tay cầm")]
    public class KhktController : InputDevice
    {
        public StickControl move { get; private set; }
        public StickControl fine { get; private set; }
        public AxisControl tilt { get; private set; }
        public ButtonControl trigger { get; private set; }
        public ButtonControl recenter { get; private set; }
        public ButtonControl askAI { get; private set; }

        static KhktController()
        {
            InputSystem.RegisterLayout<KhktController>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init() { }  // gọi để chạy static constructor trong bản build

        protected override void FinishSetup()
        {
            base.FinishSetup();
            move = GetChildControl<StickControl>("move");
            fine = GetChildControl<StickControl>("fine");
            tilt = GetChildControl<AxisControl>("tilt");
            trigger = GetChildControl<ButtonControl>("trigger");
            recenter = GetChildControl<ButtonControl>("recenter");
            askAI = GetChildControl<ButtonControl>("askAI");
        }
    }
}
