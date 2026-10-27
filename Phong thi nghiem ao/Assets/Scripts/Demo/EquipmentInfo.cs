using UnityEngine;

namespace Khkt.Demo
{
    /// <summary>Thẻ thông tin của một dụng cụ: hiện lên khi trỏ chuột (hoặc tia tay cầm) vào nó.</summary>
    public class EquipmentInfo : MonoBehaviour
    {
        public string title;
        [TextArea(2, 4)] public string description;
        [Tooltip("Cách dùng đúng, lưu ý an toàn")]
        [TextArea(2, 4)] public string usage;
    }

    /// <summary>Vật bấm vào là dùng được (bật đèn cồn, mở vòi nước...).</summary>
    public interface IDemoUsable
    {
        string UseHint { get; }
        void Use();
    }
}
