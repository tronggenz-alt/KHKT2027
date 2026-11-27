using UnityEngine;

namespace Khkt.Demo
{
    /// <summary>
    /// Đánh dấu một mặt kính lỏng (thẻ, thanh bên, bảng). Nút tạo bên trong mặt kính không làm kính chồng kính
    /// (Apple: "avoid layering Liquid Glass elements on top of each other") mà dùng nền mờ trong suốt.
    /// </summary>
    [DisallowMultipleComponent]
    public class KinhLongBeMat : MonoBehaviour
    {
        public static bool TrongKinh(Transform t) => t != null && t.GetComponentInParent<KinhLongBeMat>(true) != null;
    }
}
