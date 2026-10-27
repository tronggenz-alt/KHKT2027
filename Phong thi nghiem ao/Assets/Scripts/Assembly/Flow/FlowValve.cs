using Khkt.Demo;
using UnityEngine;

namespace Khkt.Assembly
{
    /// <summary>
    /// Khóa của phễu chiết / phễu nhỏ giọt: bấm vào tay khóa để mở / đóng. Mạng dòng chảy cho chất lỏng chảy qua đuôi phễu
    /// khi khóa mở (phễu chiết: lớp dưới ra trước; phễu nhỏ giọt: nhỏ từ từ vào bình bên dưới).
    /// </summary>
    public class FlowValve : MonoBehaviour, IDemoUsable
    {
        public bool open;
        [Tooltip("Lưu lượng khi mở hẳn (ml/s)")]
        public float rateMlPerS = 3f;
        [Tooltip("Tay khóa (xoay 90° khi mở)")]
        public Transform handle;

        Quaternion closedRot;
        bool rotKnown;

        public ApparatusPart Part => GetComponentInParent<ApparatusPart>();

        public string UseHint => open ? "Bấm chuột trái: đóng khóa" : "Bấm chuột trái: mở khóa cho chất lỏng chảy xuống";

        public void Use() => Set(!open);

        public void Set(bool value)
        {
            open = value;
            var t = handle != null ? handle : transform;
            if (!rotKnown) { closedRot = t.localRotation; rotKnown = true; }
            t.localRotation = open ? closedRot * Quaternion.Euler(0, 90, 0) : closedRot;
            var p = Part;
            LabMessages.Say(open ? $"Mở khóa {(p != null ? p.partName.ToLowerInvariant() : "phễu")}" : $"Đóng khóa {(p != null ? p.partName.ToLowerInvariant() : "phễu")}");
        }
    }
}
