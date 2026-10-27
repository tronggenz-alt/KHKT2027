using UnityEngine;

namespace Khkt.Environment
{
    /// <summary>
    /// Một "trạm" trong phòng lab: chỗ người dùng ngồi/đứng khi dịch chuyển tới.
    /// Vị trí là điểm trên sàn, hướng là hướng nhìn. Sau này dùng làm Teleportation Anchor của XRI.
    /// </summary>
    public class LabStation : MonoBehaviour
    {
        public int order;
        public string displayName;
        [Tooltip("Ngồi (tầm mắt ~1,25 m) hay đứng (~1,6 m)")]
        public bool seated = true;

        void OnDrawGizmos()
        {
            Gizmos.color = seated ? new Color(0.2f, 0.8f, 1f) : new Color(1f, 0.8f, 0.2f);
            Vector3 p = transform.position + Vector3.up * 0.05f;
            Gizmos.DrawWireSphere(p, 0.25f);
            Gizmos.DrawRay(p, transform.forward * 0.6f);
            Gizmos.DrawLine(p, p + Vector3.up * (seated ? 1.25f : 1.6f));
        }
    }
}
