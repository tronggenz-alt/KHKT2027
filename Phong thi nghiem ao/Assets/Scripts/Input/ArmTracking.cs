using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace Khkt.Input
{
    /// <summary>
    /// Nhận gói cánh tay từ mediapipe_tracker.py (cổng HeadTracking.port + 3): 21 điểm 3D mỗi bàn tay, vai, khuỷu,
    /// cổ tay từ Pose. Đổi sang trục của camera Unity (người dùng nhìn vào màn hình: phải +x, lên +y, trước +z)
    /// và nội suy giữa hai gói gần nhất, trễ thêm khoảng một gói để tay chạy đều 60 khung hình mỗi giây.
    /// </summary>
    public class ArmTracking : MonoBehaviour
    {
        [Serializable]
        class HandPacket
        {
            public string ben;
            public int[] w;
            public int x, y, p;
            public string g;
        }

        [Serializable]
        class ArmPacket
        {
            public string ben;
            public int[] d;
            public int[] tin;
        }

        [Serializable]
        class Packet
        {
            public float t;
            public HandPacket[] tay;
            public ArmPacket[] canh;
        }

        public class Side
        {
            public bool handSeen, armSeen;
            /// <summary>21 điểm bàn tay (m), gốc ở tâm bàn tay, theo trục camera.</summary>
            public readonly Vector3[] hand = new Vector3[21];
            /// <summary>Vai, khuỷu, cổ tay (m), theo trục camera, gốc ở hông.</summary>
            public Vector3 shoulder, elbow, wrist;
            public float elbowConf, wristConf;
            public Vector2 wristImage;
            public bool pinch;
            public string gesture = "";

            // hai mẫu gần nhất để nội suy
            internal readonly Vector3[] handA = new Vector3[21], handB = new Vector3[21];
            internal Vector3 shA, elA, wrA, shB, elB, wrB;
            internal float timeA, timeB, lastHand, lastArm;
        }

        public static ArmTracking Instance { get; private set; }
        public const float StaleSec = 0.4f;

        public readonly Side Right = new Side(), Left = new Side();
        public bool Active => Right.handSeen || Left.handSeen || Right.armSeen || Left.armSeen;
        public int Packets { get; private set; }

        UdpClient socket;
        float interval = 1f / 30f;

        void Awake() => Instance = this;

        void OnEnable()
        {
            int port = (HeadTracking.Instance != null ? HeadTracking.Instance.port : 9051) + 3;
            try { socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, port)); }
            catch (Exception e) { Debug.LogWarning("[ArmTracking] Không mở được cổng " + port + ": " + e.Message); }
        }

        void OnDisable()
        {
            socket?.Close();
            socket = null;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            Receive();
            float now = Time.realtimeSinceStartup;
            Blend(Right, now);
            Blend(Left, now);
        }

        void Receive()
        {
            if (socket == null) return;
            try
            {
                var from = new IPEndPoint(IPAddress.Any, 0);
                while (socket.Available > 0)
                {
                    var p = JsonUtility.FromJson<Packet>(Encoding.UTF8.GetString(socket.Receive(ref from)));
                    if (p != null) Apply(p, Time.realtimeSinceStartup);
                }
            }
            catch (Exception e) { Debug.LogWarning("[ArmTracking] Gói lỗi: " + e.Message); }
        }

        /// <summary>
        /// Bàn tay từ nhận diện ngay trong Unity (InAppHandTracker): điểm đã theo trục camera (m),
        /// cổ tay trên ảnh webcam chưa lật (0..1).
        /// </summary>
        public void FeedHand(string ben, Vector3[] cameraSpace, Vector2 wristImage, bool pinch)
        {
            float now = Time.realtimeSinceStartup;
            var s = ben == "trai" ? Left : Right;
            Array.Copy(s.handB, s.handA, 21);
            Array.Copy(cameraSpace, s.handB, 21);
            if (now - s.lastHand > StaleSec) Array.Copy(s.handB, s.handA, 21);
            s.lastHand = now;
            s.wristImage = wristImage;
            s.pinch = pinch;
            s.gesture = "";
            Stamp(s, now);
        }

        /// <summary>Cho bài thử: đưa gói JSON vào như vừa nhận qua mạng.</summary>
        public void Inject(string json) => Apply(JsonUtility.FromJson<Packet>(json), Time.realtimeSinceStartup);

        // MediaPipe: x sang phải ảnh, y xuống, z ra xa camera, webcam không lật. Người dùng nhìn vào camera nên
        // phải của họ là -x, lên là -y, phía trước (về phía màn hình) là -z: đổi dấu cả ba trục.
        static Vector3 Mm(int[] a, int i) => new Vector3(-a[i * 3], -a[i * 3 + 1], -a[i * 3 + 2]) * 0.001f;

        void Apply(Packet p, float now)
        {
            Packets++;
            if (p.tay != null)
                foreach (var h in p.tay)
                {
                    if (h == null || h.w == null || h.w.Length < 63) continue;
                    var s = h.ben == "trai" ? Left : Right;
                    Array.Copy(s.handB, s.handA, 21);
                    for (int i = 0; i < 21; i++) s.handB[i] = Mm(h.w, i);
                    if (now - s.lastHand > StaleSec) Array.Copy(s.handB, s.handA, 21);
                    s.lastHand = now;
                    s.wristImage = new Vector2(h.x * 0.001f, h.y * 0.001f);
                    s.pinch = h.p != 0;
                    s.gesture = h.g ?? "";
                    Stamp(s, now);
                }
            if (p.canh != null)
                foreach (var c in p.canh)
                {
                    if (c == null || c.d == null || c.d.Length < 9) continue;
                    var s = c.ben == "trai" ? Left : Right;
                    s.shA = s.shB; s.elA = s.elB; s.wrA = s.wrB;
                    s.shB = Mm(c.d, 0); s.elB = Mm(c.d, 1); s.wrB = Mm(c.d, 2);
                    if (now - s.lastArm > StaleSec) { s.shA = s.shB; s.elA = s.elB; s.wrA = s.wrB; }
                    s.lastArm = now;
                    s.elbowConf = c.tin != null && c.tin.Length > 1 ? c.tin[1] * 0.01f : 0f;
                    s.wristConf = c.tin != null && c.tin.Length > 2 ? c.tin[2] * 0.01f : 0f;
                    Stamp(s, now);
                }
        }

        void Stamp(Side s, float now)
        {
            if (Mathf.Approximately(s.timeB, now)) return;  // tay và cánh tay cùng một gói
            float gap = now - s.timeB;
            if (gap > 0.005f && gap < 0.2f) interval = Mathf.Lerp(interval, gap, 0.1f);
            s.timeA = s.timeB;
            s.timeB = now;
        }

        /// <summary>Vẽ trễ một khoảng gói: luôn có hai mẫu kẹp hai bên, tay đi đều không giật theo nhịp gói.</summary>
        void Blend(Side s, float now)
        {
            s.handSeen = now - s.lastHand < StaleSec;
            s.armSeen = now - s.lastArm < StaleSec;
            float span = Mathf.Max(1e-3f, s.timeB - s.timeA);
            float k = Mathf.Clamp01((now - interval - s.timeA) / span);
            for (int i = 0; i < 21; i++) s.hand[i] = Vector3.Lerp(s.handA[i], s.handB[i], k);
            s.shoulder = Vector3.Lerp(s.shA, s.shB, k);
            s.elbow = Vector3.Lerp(s.elA, s.elB, k);
            s.wrist = Vector3.Lerp(s.wrA, s.wrB, k);
        }
    }
}
