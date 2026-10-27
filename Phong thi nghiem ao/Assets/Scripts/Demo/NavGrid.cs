using System.Collections.Generic;
using UnityEngine;

namespace Khkt.Demo
{
    /// <summary>
    /// Lưới đi bộ cho robot: chia sàn thành ô 0,25 m, ô nào không vướng vật (bàn, tủ, tường) trong khoảng cao
    /// 0,1 - 1,4 m thì đi được. Tìm đường bằng A* rồi rút gọn đường theo tầm nhìn thẳng.
    /// Lưới nướng sẵn khi dựng cảnh (NavGridData, lúc đó mọi khu đều có mặt), vì lúc chạy các khu ở xa đã được gỡ;
    /// không có dữ liệu nướng thì dựng một lần khi robot cần đi lần đầu (vài nghìn lần kiểm tra va chạm).
    /// </summary>
    public static class NavGrid
    {
        public static Vector2 Min = new Vector2(-0.6f, -9.6f), Max = new Vector2(27.6f, 17.6f);
        public const float Cell = 0.25f;
        static bool[,] walk;
        static int nx, nz;
        static readonly Collider[] hits = new Collider[16];

        public static bool Ready => walk != null;

        public static void Build()
        {
            nx = Mathf.CeilToInt((Max.x - Min.x) / Cell);
            nz = Mathf.CeilToInt((Max.y - Min.y) / Cell);
            walk = new bool[nx, nz];
            var data = Object.FindAnyObjectByType<NavGridData>();
            if (data != null && data.walk != null && data.nx == nx && data.nz == nz)
            {
                for (int i = 0; i < nx; i++)
                    for (int k = 0; k < nz; k++)
                        walk[i, k] = data.walk[i * nz + k] != 0;
                return;
            }
            for (int i = 0; i < nx; i++)
                for (int k = 0; k < nz; k++)
                    walk[i, k] = Free(CellCenter(i, k), 0.2f);
        }

        /// <summary>Tính lưới ngay bây giờ theo va chạm trong cảnh (trình dựng cảnh gọi để nướng vào NavGridData).</summary>
        public static byte[] Bake(out int cellsX, out int cellsZ)
        {
            walk = null;
            cellsX = nx = Mathf.CeilToInt((Max.x - Min.x) / Cell);
            cellsZ = nz = Mathf.CeilToInt((Max.y - Min.y) / Cell);
            var b = new byte[nx * nz];
            for (int i = 0; i < nx; i++)
                for (int k = 0; k < nz; k++)
                    b[i * nz + k] = Free(CellCenter(i, k), 0.2f) ? (byte)1 : (byte)0;
            return b;
        }

        /// <summary>
        /// Chỗ đứng (tâm robot bán kính r) có trống không. Bỏ qua người xem, robot, vật cầm được, vùng trigger,
        /// cánh cửa tự động (cửa tự mở khi robot tới).
        /// </summary>
        public static bool Free(Vector3 p, float r)
        {
            // phải có sàn bên dưới
            if (!Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, 0.8f, ~0, QueryTriggerInteraction.Ignore)) return false;
            int n = Physics.OverlapBoxNonAlloc(new Vector3(p.x, 0.75f, p.z), new Vector3(r, 0.62f, r), hits, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = hits[i];
                if (c is CharacterController) continue;
                if (c.GetComponentInParent<Khkt.Interaction.Grabbable>() != null) continue;
                if (c.GetComponentInParent<LabRobot>() != null) continue;
                if (c.GetComponentInParent<Khkt.Rendering.AutoDoor>() != null) continue;
                return false;
            }
            return true;
        }

        static Vector3 CellCenter(int i, int k) => new Vector3(Min.x + (i + 0.5f) * Cell, 0, Min.y + (k + 0.5f) * Cell);

        static bool ToCell(Vector3 p, out int i, out int k)
        {
            i = Mathf.FloorToInt((p.x - Min.x) / Cell);
            k = Mathf.FloorToInt((p.z - Min.y) / Cell);
            return i >= 0 && k >= 0 && i < nx && k < nz;
        }

        public static bool Walkable(Vector3 p) => Ready && ToCell(p, out int i, out int k) && walk[i, k];

        /// <summary>Ô đi được gần p nhất (tìm theo vòng tròn mở rộng).</summary>
        public static bool Nearest(Vector3 p, out Vector3 result, float maxDist = 3f)
        {
            if (!Ready) Build();
            result = p;
            if (!ToCell(p, out int ci, out int ck)) return false;
            int maxR = Mathf.CeilToInt(maxDist / Cell);
            for (int r = 0; r <= maxR; r++)
            {
                float best = float.MaxValue;
                bool found = false;
                for (int i = ci - r; i <= ci + r; i++)
                    for (int k = ck - r; k <= ck + r; k++)
                    {
                        if (Mathf.Max(Mathf.Abs(i - ci), Mathf.Abs(k - ck)) != r) continue;
                        if (i < 0 || k < 0 || i >= nx || k >= nz || !walk[i, k]) continue;
                        var c = CellCenter(i, k);
                        float d = (new Vector2(c.x - p.x, c.z - p.z)).sqrMagnitude;
                        if (d < best) { best = d; result = c; found = true; }
                    }
                if (found) return true;
            }
            return false;
        }

        /// <summary>Đường đi từ a tới b (danh sách điểm trên sàn), null nếu không có đường.</summary>
        public static List<Vector3> Path(Vector3 a, Vector3 b)
        {
            if (!Ready) Build();
            if (!Nearest(a, out var sa, 2f) || !Nearest(b, out var sb, 2f)) return null;
            ToCell(sa, out int ai, out int ak);
            ToCell(sb, out int bi, out int bk);
            var open = new SortedSet<(float f, int id)>();
            var g = new Dictionary<int, float>();
            var from = new Dictionary<int, int>();
            int Id(int i, int k) => i * nz + k;
            int start = Id(ai, ak), goal = Id(bi, bk);
            g[start] = 0;
            open.Add((H(ai, ak, bi, bk), start));
            int[] di = { 1, -1, 0, 0, 1, 1, -1, -1 }, dk = { 0, 0, 1, -1, 1, -1, 1, -1 };
            int guard = 0;
            while (open.Count > 0 && guard++ < 200000)
            {
                var cur = open.Min;
                open.Remove(cur);
                if (cur.id == goal) break;
                int ci = cur.id / nz, ck = cur.id % nz;
                float gc = g[cur.id];
                for (int d = 0; d < 8; d++)
                {
                    int i = ci + di[d], k = ck + dk[d];
                    if (i < 0 || k < 0 || i >= nx || k >= nz || !walk[i, k]) continue;
                    if (d >= 4 && (!walk[ci + di[d], ck] || !walk[ci, ck + dk[d]])) continue;  // không cắt góc
                    float ng = gc + (d >= 4 ? 1.4142f : 1f);
                    int id = Id(i, k);
                    if (g.TryGetValue(id, out float old) && old <= ng) continue;
                    if (g.ContainsKey(id)) open.Remove((old + H(i, k, bi, bk), id));
                    g[id] = ng;
                    from[id] = cur.id;
                    open.Add((ng + H(i, k, bi, bk), id));
                }
            }
            if (!from.ContainsKey(goal) && start != goal) return null;
            var cells = new List<Vector3>();
            for (int id = goal; ; id = from[id])
            {
                cells.Add(CellCenter(id / nz, id % nz));
                if (id == start || !from.ContainsKey(id)) break;
            }
            cells.Reverse();
            // rút gọn: bỏ các điểm giữa nếu đi thẳng được
            var path = new List<Vector3> { a };
            int j = 0;
            while (j < cells.Count - 1)
            {
                int far = j + 1;
                for (int t = cells.Count - 1; t > j + 1; t--)
                    if (Line(cells[j], cells[t])) { far = t; break; }
                path.Add(cells[far]);
                j = far;
            }
            path.Add(new Vector3(b.x, 0, b.z));
            return path;
        }

        static float H(int i, int k, int bi, int bk) => Mathf.Sqrt((i - bi) * (i - bi) + (k - bk) * (k - bk));

        static bool Line(Vector3 a, Vector3 b)
        {
            float len = Vector3.Distance(a, b);
            int n = Mathf.CeilToInt(len / (Cell * 0.5f));
            for (int s = 0; s <= n; s++)
                if (!Walkable(Vector3.Lerp(a, b, s / (float)n))) return false;
            return true;
        }
    }
}
