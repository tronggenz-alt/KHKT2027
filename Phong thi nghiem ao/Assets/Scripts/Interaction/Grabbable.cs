using UnityEngine;

namespace Khkt.Interaction
{
    /// <summary>
    /// Dụng cụ cầm lên, đặt xuống được. Gốc tọa độ của vật ở đáy (đặt lên mặt bàn là đúng chỗ).
    /// kind dùng để ổ đặt biết có nhận vật này không (ví dụ kẹp buret chỉ nhận "buret").
    /// </summary>
    public class Grabbable : MonoBehaviour
    {
        public string kind = "dung_cu";
        [Tooltip("Khi cầm: đáy vật nằm dưới điểm nắm của bàn tay bao nhiêu mét")]
        public float gripHeightM = 0.08f;
        [Tooltip("Đặt lên mặt bàn thì nằm ngang (buret, pipet) thay vì đứng")]
        public bool liesOnTable;
        [Tooltip("Bán kính khi nằm ngang (để không lún vào mặt bàn)")]
        public float lyingRadiusM = 0.007f;
        [Tooltip("Chiều dài vật (để đặt nằm ngang cho cân giữa điểm trỏ)")]
        public float lengthM = 0.2f;
        [Tooltip("Khối lượng vỏ dụng cụ (g), để cân")]
        public float massG = 100f;

        public bool IsHeld { get; private set; }
        public PlaceSocket Socket { get; private set; }
        public Vector3 HomePosition { get; private set; }
        public Quaternion HomeRotation { get; private set; }
        public PlaceSocket HomeSocket { get; private set; }

        Collider[] colliders;

        void Awake()
        {
            colliders = GetComponentsInChildren<Collider>(true);
            HomePosition = transform.position;
            HomeRotation = transform.rotation;
        }

        void Start() => HomeSocket = Socket;

        /// <summary>Được cầm lên (true) / thả ra (false), bởi bàn tay hay robot. Bộ phận lắp ráp nghe để tự tháo khỏi bộ.</summary>
        public event System.Action<bool> HeldChanged;

        public void SetHeld(bool held)
        {
            IsHeld = held;
            if (held) SetSocket(null);
            if (held) HeldChanged?.Invoke(true);
            // Khi đang cầm, tắt va chạm để tia chuột xuyên qua vật đang cầm (lấy lại danh sách: bộ phận lắp vào sau cũng là con)
            colliders = GetComponentsInChildren<Collider>(true);
            foreach (var c in colliders) if (c != null) c.enabled = !held;
            if (!held) HeldChanged?.Invoke(false);
        }

        /// <summary>Đổi "chỗ cũ" (phím Q trả về đây), ví dụ bộ dụng cụ vừa gia công ra bàn đích.</summary>
        public void SetHome(Vector3 position, Quaternion rotation)
        {
            HomePosition = position;
            HomeRotation = rotation;
            HomeSocket = null;
        }

        public void SetSocket(PlaceSocket socket)
        {
            if (Socket != null && Socket != socket) Socket.Release(this);
            Socket = socket;
        }
    }
}
