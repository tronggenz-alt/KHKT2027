using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Khkt.Input
{
    /// <summary>
    /// THỬ NGHIỆM: nhận vị trí marker từ tools/webcam_tracker.py qua UDP.
    /// Mỗi gói là JSON {"id": "dau"|"tay", "p": [x, y, z], "t": thời_gian}, đã đổi sang trục Unity,
    /// gốc tọa độ là webcam. SetOrigin() (gọi khi căn lại) ghép vị trí kính hiện tại với vị trí camera ảo.
    /// </summary>
    public class UdpPoseReceiver : MonoBehaviour
    {
        public int port = 9050;
        [Tooltip("Id marker dán trên kính")]
        public string headId = "dau";

        [Serializable]
        class Msg
        {
            public string id;
            public float[] p;
            public double t;
        }

        readonly object gate = new object();
        readonly Dictionary<string, (Vector3 pos, float time)> latest = new Dictionary<string, (Vector3, float)>();
        Vector3 originOffset;
        bool hasOrigin;
        UdpClient client;
        Thread thread;
        volatile bool running;
        float now;

        void OnEnable()
        {
            running = true;
            client = new UdpClient(port);
            thread = new Thread(Loop) { IsBackground = true, Name = "KhktUdpPose" };
            thread.Start();
        }

        void OnDisable()
        {
            running = false;
            client?.Close();
            thread?.Join(200);
        }

        void Update() => now = Time.realtimeSinceStartup;

        void Loop()
        {
            var any = new IPEndPoint(IPAddress.Any, 0);
            while (running)
            {
                try
                {
                    byte[] data = client.Receive(ref any);
                    var m = JsonUtility.FromJson<Msg>(Encoding.UTF8.GetString(data));
                    if (m?.id == null || m.p == null || m.p.Length != 3) continue;
                    lock (gate) latest[m.id] = (new Vector3(m.p[0], m.p[1], m.p[2]), now);
                }
                catch (SocketException) { }
                catch (ObjectDisposedException) { return; }
                catch (ArgumentException) { }  // JSON hỏng
            }
        }

        /// <summary>Gắn vị trí marker trên kính lúc này với vị trí đầu trong Unity.</summary>
        public void SetOrigin(Vector3 headLocalPosition)
        {
            lock (gate)
            {
                if (!latest.TryGetValue(headId, out var h)) return;
                originOffset = headLocalPosition - h.pos;
                hasOrigin = true;
            }
        }

        /// <summary>Vị trí (trong hệ Camera Offset) nếu marker được thấy trong maxAge giây gần nhất.</summary>
        public bool TryGetPosition(string id, float maxAge, out Vector3 position)
        {
            lock (gate)
            {
                if (hasOrigin && latest.TryGetValue(id, out var v) && now - v.time <= maxAge)
                {
                    position = v.pos + originOffset;
                    return true;
                }
            }
            position = default;
            return false;
        }
    }
}
