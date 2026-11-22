using System.Collections.Generic;
using UnityEngine;

namespace Khkt.Input
{
    /// <summary>
    /// Nhận diện thao tác bàn tay từ 21 điểm MediaPipe, bản C# của tools/tay_thao_tac.py (cùng ngưỡng, cùng nhãn):
    /// chum, nam_doc, nam_ngang, ut, tro, mo, khac. Dùng cho nguồn tay chạy ngay trong Unity.
    /// </summary>
    public static class HandGesture
    {
        public const float ExtRatio = 1.12f, PinchOn = 0.30f, PinchOff = 0.42f, NgangNguong = 0.55f, IndexReach = 0.45f;

        static float D(Vector2[] lm, int a, int b, float w, float h) =>
            Mathf.Sqrt((lm[a].x - lm[b].x) * (lm[a].x - lm[b].x) * w * w + (lm[a].y - lm[b].y) * (lm[a].y - lm[b].y) * h * h);

        /// <summary>lm: 21 điểm ảnh (0..1); world: 21 điểm 3D (m) hoặc null; w, h: cỡ khung để đo đúng tỉ lệ.</summary>
        public static string Classify(Vector2[] lm, Vector3[] world, ref bool pinching, float w = 1f, float h = 1f)
        {
            bool Ext(int tip, int pip) => D(lm, 0, tip, w, h) > ExtRatio * D(lm, 0, pip, w, h);
            bool i = Ext(8, 6), m = Ext(12, 10), r = Ext(16, 14), p = Ext(20, 18);
            float palm = D(lm, 0, 9, w, h) + 1e-6f;
            float ratio = D(lm, 4, 8, w, h) / palm;
            // chụm: đầu ngón cái chạm đầu ngón trỏ VÀ đầu ngón trỏ còn cách gốc ngón (không gập hẳn vào lòng như khi nắm).
            // Xét trước "nắm": chụm mà ba ngón kia co thì cả bốn ngón đều "không duỗi", trước đây bị nhầm thành nắm tay.
            bool reach = D(lm, 8, 5, w, h) / palm > IndexReach;
            bool chum = reach && ratio < (pinching ? PinchOff : PinchOn);
            bool nam = !(i || m || r || p);
            if (chum) { pinching = true; return "chum"; }
            pinching = false;
            if (nam)
            {
                if (world != null)
                {
                    Vector3 n = Vector3.Cross(world[5] - world[0], world[17] - world[0]).normalized;
                    return Mathf.Abs(n.y) > NgangNguong ? "nam_ngang" : "nam_doc";
                }
                return "nam_ngang";
            }
            if (p && !(i || m || r)) return "ut";
            if (i && !(m || r || p)) return "tro";
            if (i && m && r && p) return "mo";
            return "khac";
        }

        /// <summary>Bỏ phiếu số đông trên 5 khung gần nhất.</summary>
        public class Vote
        {
            readonly Queue<string> q = new Queue<string>();
            readonly Dictionary<string, int> count = new Dictionary<string, int>();
            public string Add(string g)
            {
                q.Enqueue(g);
                count[g] = count.TryGetValue(g, out var c) ? c + 1 : 1;
                if (q.Count > 5) { var o = q.Dequeue(); count[o]--; }
                string best = g; int bc = -1;
                foreach (var kv in count) if (kv.Value > bc) { bc = kv.Value; best = kv.Key; }
                return best;
            }
            public void Clear() { q.Clear(); count.Clear(); }
        }

        /// <summary>Gán trái / phải theo vị trí cổ tay cũ (ảnh đã lật như gương), không tin nhãn của mô hình.</summary>
        public class SideAssigner
        {
            readonly Vector2?[] last = new Vector2?[2];   // 0 trái, 1 phải
            readonly float[] at = { -1e9f, -1e9f };
            int one = -1;

            public int[] Assign(List<Vector2> wrists, float t)
            {
                var res = new int[wrists.Count];
                if (wrists.Count == 0) { one = -1; return res; }
                bool Fresh(int s) => last[s].HasValue && t - at[s] < 1.5f;
                float Dist(Vector2 c, int s) => (c - last[s].Value).magnitude;
                if (wrists.Count >= 2)
                {
                    Vector2 a = wrists[0], b = wrists[1];
                    bool keep = Fresh(0) && Fresh(1) ? Dist(a, 0) + Dist(b, 1) <= Dist(a, 1) + Dist(b, 0) : a.x <= b.x;
                    res[0] = keep ? 0 : 1;
                    res[1] = keep ? 1 : 0;
                    for (int k = 2; k < res.Length; k++) res[k] = -1;
                    one = -1;
                }
                else
                {
                    Vector2 c = wrists[0];
                    int s = -1;
                    float best = 0.30f;
                    for (int k = 0; k < 2; k++) if (Fresh(k) && Dist(c, k) < best) { best = Dist(c, k); s = k; }
                    if (s < 0) s = one >= 0 && Mathf.Abs(c.x - 0.5f) < 0.06f ? one : c.x < 0.5f ? 0 : 1;
                    one = s;
                    res[0] = s;
                }
                for (int k = 0; k < res.Length; k++)
                    if (res[k] >= 0) { last[res[k]] = wrists[k]; at[res[k]] = t; }
                return res;
            }
        }
    }
}
