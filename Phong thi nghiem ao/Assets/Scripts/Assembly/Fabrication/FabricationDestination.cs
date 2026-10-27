using System.Collections.Generic;
using UnityEngine;

namespace Khkt.Assembly
{
    /// <summary>
    /// Một bàn đích của khu chế tạo: vùng đặt bộ dụng cụ đã gia công (hình chữ nhật trên mặt bàn, gốc ở tâm vùng, nằm đúng
    /// mặt bàn) và hướng người làm thí nghiệm đứng. Máy gia công tìm chỗ trống trong vùng này (không chồng lên đồ đang có).
    /// </summary>
    public class FabricationDestination : MonoBehaviour
    {
        public string label = "Bàn";
        public int order;
        [Tooltip("Cỡ vùng đặt theo x, z cục bộ (m)")]
        public Vector2 size = new Vector2(1f, 0.6f);
        [Tooltip("Hướng (cục bộ) về phía người đứng làm thí nghiệm: mặt trước của bộ dụng cụ quay về đây")]
        public Vector3 front = Vector3.back;
        [Tooltip("Bàn nhận mặc định trong khu chế tạo")]
        public bool isDefault;

        public static readonly List<FabricationDestination> All = new List<FabricationDestination>();

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() => All.Remove(this);

        public Vector3 FrontWorld => Vector3.ProjectOnPlane(transform.TransformDirection(front), Vector3.up).normalized;

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.6f);
            Gizmos.DrawWireCube(new Vector3(0, 0.15f, 0), new Vector3(size.x, 0.3f, size.y));
            Gizmos.DrawLine(Vector3.zero, front * 0.4f);
        }
    }
}
