using System.Collections.Generic;
using Khkt.Interaction;
using Khkt.Simulation;
using UnityEngine;

namespace Khkt.Demo
{
    /// <summary>
    /// Phần nhìn của buret: cột dung dịch tới mặt khum (theo thể tích trong buret), mặt khum,
    /// giọt rơi từng giọt và dòng chảy khi mở khóa lớn.
    /// </summary>
    public class BuretVisual : MonoBehaviour
    {
        public BuretStopcock buret;
        [Tooltip("Vạch 0 ml")]
        public Transform zeroMark;
        public float metersPerMl = 0.0105f;
        [Tooltip("Cột dung dịch: gốc ở đáy ống, kéo giãn theo y tới mặt khum")]
        public Transform liquidColumn;
        [Tooltip("Dung dịch trong đầu buret (dưới khóa)")]
        public GameObject tipLiquid;
        public Transform meniscus;
        [Tooltip("Dòng chảy: gốc ở đầu buret, kéo dài xuống theo y")]
        public Transform stream;
        public Material dropMaterial;
        public float dropRadius = 0.0022f;
        [Tooltip("Độ cao miệng ống (hệ tọa độ buret), dung dịch không vượt quá")]
        public float tubeTopLocalY = 0.755f;

        readonly List<(Transform t, float speed, float stopY)> falling = new List<(Transform, float, float)>();
        readonly Stack<Transform> pool = new Stack<Transform>();

        void OnEnable()
        {
            if (buret != null) buret.DropFell += OnDrop;
        }

        void OnDisable()
        {
            if (buret != null) buret.DropFell -= OnDrop;
        }

        void LateUpdate()
        {
            if (buret == null || zeroMark == null) return;
            bool upright = Vector3.Dot(buret.transform.up, Vector3.up) > 0.95f;
            bool hasLiquid = buret.HasLiquid && upright;
            float topY = zeroMark.position.y - buret.ReadingMl * metersPerMl;
            float maxY = buret.transform.TransformPoint(new Vector3(0, tubeTopLocalY, 0)).y;
            topY = Mathf.Min(topY, maxY);

            if (liquidColumn != null)
            {
                float h = topY - liquidColumn.position.y;
                bool show = hasLiquid && h > 0.001f;
                if (liquidColumn.gameObject.activeSelf != show) liquidColumn.gameObject.SetActive(show);
                if (show)
                {
                    var s = liquidColumn.localScale;
                    s.y = h;
                    liquidColumn.localScale = s;
                }
            }
            if (tipLiquid != null && tipLiquid.activeSelf != hasLiquid) tipLiquid.SetActive(hasLiquid);
            if (meniscus != null)
            {
                bool show = hasLiquid && liquidColumn != null && topY > liquidColumn.position.y;
                if (meniscus.gameObject.activeSelf != show) meniscus.gameObject.SetActive(show);
                if (show)
                {
                    var p = meniscus.position;
                    meniscus.position = new Vector3(p.x, topY, p.z);
                }
            }
            if (stream != null)
            {
                bool on = buret.IsStream;
                if (stream.gameObject.activeSelf != on) stream.gameObject.SetActive(on);
                if (on)
                {
                    var s = stream.localScale;
                    s.y = SurfaceBelow(out _);
                    stream.localScale = s;
                }
            }

            for (int i = falling.Count - 1; i >= 0; i--)
            {
                var (t, v, stopY) = falling[i];
                v += 9.81f * Time.deltaTime;
                t.position += Vector3.down * v * Time.deltaTime;
                if (t.position.y <= stopY)
                {
                    t.gameObject.SetActive(false);
                    pool.Push(t);
                    falling.RemoveAt(i);
                }
                else falling[i] = (t, v, stopY);
            }
        }

        /// <summary>Khoảng cách từ đầu buret xuống bề mặt bên dưới (mặt dung dịch hoặc mặt bàn).</summary>
        float SurfaceBelow(out float surfaceY)
        {
            Vector3 from = buret.tip != null ? buret.tip.position : buret.transform.position;
            surfaceY = from.y - 0.15f;
            if (Physics.Raycast(from, Vector3.down, out RaycastHit hit, 0.6f, ~0, QueryTriggerInteraction.Ignore))
            {
                var c = hit.collider.GetComponentInParent<LabContainer>();
                surfaceY = c != null && c.fill != null ? c.fill.transform.TransformPoint(0, c.fill.LevelM, 0).y : hit.point.y;
            }
            return Mathf.Max(0.01f, from.y - surfaceY);
        }

        void OnDrop(Solution s, LabContainer receiver)
        {
            if (buret.IsStream || dropMaterial == null) return;
            Transform t;
            if (pool.Count > 0) t = pool.Pop();
            else
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(g.GetComponent<Collider>());
                g.name = "Giọt";
                g.GetComponent<Renderer>().sharedMaterial = dropMaterial;
                g.transform.localScale = Vector3.one * dropRadius * 2;
                t = g.transform;
            }
            t.gameObject.SetActive(true);
            Vector3 from = buret.tip != null ? buret.tip.position : buret.transform.position;
            t.position = from + Vector3.down * dropRadius;
            SurfaceBelow(out float stopY);
            falling.Add((t, 0.3f, stopY));
        }
    }
}
