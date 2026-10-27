using System.Collections.Generic;
using Khkt.Demo;
using Khkt.Environment;
using UnityEngine;

namespace Khkt.Rendering
{
    /// <summary>
    /// Cửa trượt tự động (hai cánh tách đôi). Bình thường đóng; người xem hoặc robot tới gần thì cửa mở, nhưng chỉ mở khi
    /// khu bên kia đã tải xong (đang tải thì đèn trạng thái màu hổ phách), nên không bao giờ nhìn thấy khoảng trống.
    /// Đi qua rồi cửa tự đóng; ZoneStreamer gỡ khu phía sau khi đã ra xa.
    ///
    /// Gốc của cửa đặt ở giữa khung cửa trên mặt sàn, trục z (forward) chỉ từ khu A sang khu B.
    /// </summary>
    public class AutoDoor : MonoBehaviour
    {
        public string doorName = "Cửa";
        public Transform[] leaves;
        [Tooltip("Mỗi cánh dịch đi bao nhiêu (tọa độ cục bộ của cha cánh) khi mở hẳn")]
        public Vector3[] slide;
        [Tooltip("Bề rộng lối đi (m)")]
        public float width = 1.6f;
        public int zoneA = ZoneMap.Outside, zoneB = ZoneMap.Outside;
        public float sensorRadius = 1.9f;
        public float openSeconds = 0.75f;
        [Tooltip("Giữ cửa mở thêm sau khi không còn ai trong vùng cảm biến")]
        public float holdOpenS = 0.8f;
        [Tooltip("Đèn trạng thái trên khung (mỗi mặt tường một dải)")]
        public Renderer[] statusLights;
        [Tooltip("Cổng che khuất (occlusion portal) ở khung cửa: đóng cửa thì che phần phía sau")]
        public OcclusionPortal portal;

        public static readonly List<AutoDoor> All = new List<AutoDoor>();

        public float Openness => open;
        public bool IsOpen => open > 0.95f;
        public Vector3 Center => transform.position;
        public Vector3 Normal => transform.forward;

        float open, lastNear = -99f;
        Vector3[] closed;
        MaterialPropertyBlock mpb;
        int lightState = -1;
        static DesktopViewer viewer;
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor"), BaseColorId = Shader.PropertyToID("_BaseColor");

        void Awake()
        {
            closed = new Vector3[leaves.Length];
            for (int i = 0; i < leaves.Length; i++) closed[i] = leaves[i] != null ? leaves[i].localPosition : Vector3.zero;
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Update()
        {
            if (AnyoneNear()) lastNear = Time.time;
            bool want = Time.time - lastNear < holdOpenS;
            bool ready = true;
            var zs = ZoneStreamer.Instance;
            if (want && zs != null)
            {
                zs.Request(zoneA);
                zs.Request(zoneB);
                ready = zs.IsReady(zoneA) && zs.IsReady(zoneB);
            }
            float target = want && ready ? 1f : 0f;
            if (!Mathf.Approximately(open, target))
            {
                open = Mathf.MoveTowards(open, target, Time.deltaTime / Mathf.Max(0.05f, openSeconds));
                float e = Mathf.SmoothStep(0f, 1f, open);
                for (int i = 0; i < leaves.Length; i++)
                    if (leaves[i] != null) leaves[i].localPosition = closed[i] + slide[i] * e;
            }
            if (portal != null) portal.open = open > 0.01f;
            SetLight(want && !ready ? 1 : open > 0.01f ? 2 : 0);
        }

        /// <summary>Đèn trạng thái trên khung: xanh dịu khi đóng, hổ phách khi chờ tải khu bên kia, xanh lá khi mở.</summary>
        void SetLight(int s)
        {
            if (statusLights == null || s == lightState) return;
            lightState = s;
            mpb ??= new MaterialPropertyBlock();
            Color c = s == 1 ? new Color(1f, 0.62f, 0.12f) : s == 2 ? new Color(0.25f, 0.95f, 0.45f) : new Color(0.55f, 0.85f, 0.9f);
            foreach (var r in statusLights)
            {
                if (r == null) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetColor(BaseColorId, c);
                mpb.SetColor(EmissionId, c * (s == 0 ? 0.7f : 2.2f));
                r.SetPropertyBlock(mpb);
            }
        }

        bool AnyoneNear()
        {
            if (viewer == null) viewer = FindAnyObjectByType<DesktopViewer>();
            float r2 = sensorRadius * sensorRadius;
            if (viewer != null && FlatSqr(viewer.transform.position - Center) < r2) return true;
            var bot = LabRobot.Instance;
            return bot != null && FlatSqr(bot.transform.position - Center) < r2;
        }

        static float FlatSqr(Vector3 v) => v.x * v.x + v.z * v.z;

        /// <summary>
        /// Robot đi từ from tới to có phải băng qua một cửa chưa mở không (đang ở sát cửa). Robot dừng chờ cửa mở
        /// thay vì đi xuyên qua cánh cửa đóng.
        /// </summary>
        public static bool Blocks(Vector3 from, Vector3 to)
        {
            foreach (var d in All)
            {
                if (d.open > 0.9f) continue;
                Vector3 n = d.Normal, c = d.Center;
                float a = Vector3.Dot(from - c, n), b = Vector3.Dot(to - c, n);
                if (a * b >= 0f || Mathf.Abs(a) > 1.1f) continue;
                Vector3 hit = Vector3.Lerp(from, to, a / (a - b));
                if (Mathf.Abs(Vector3.Dot(hit - c, d.transform.right)) < d.width / 2 + 0.35f) return true;
            }
            return false;
        }
    }
}
