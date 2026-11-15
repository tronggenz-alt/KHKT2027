using System.Collections.Generic;
using UnityEngine;

namespace Khkt.Demo
{
    /// <summary>
    /// Lưới đi bộ cho robot, HAI TẦNG: mỗi tầng chia sàn thành ô 0,25 m, ô nào không vướng vật (bàn, tủ, tường) trong khoảng
    /// cao 0,1 - 1,4 m trên mặt sàn tầng đó thì đi được. Tìm đường bằng A* trong từng tầng rồi rút gọn theo tầm nhìn thẳng.
    /// Hai tầng nối với nhau bằng thang bộ ở lõi thang (x 28,55..29,8; z 0,33..5,9): đường đi khác tầng = đường tới chân /
    /// đầu thang + đoạn dốc thang + đường từ đầu kia tới đích. Các điểm trả về mang độ cao thật (0 ở tầng 1, 3,8 ở tầng 2)
    /// để robot, cửa tự động và bộ tải khu biết đang ở tầng nào.
    /// Lưới nướng sẵn khi dựng cảnh (NavGridData, lúc đó mọi khu đều có mặt), vì lúc chạy các khu ở xa đã được gỡ;
    /// không có dữ liệu nướng thì dựng một lần khi robot cần đi lần đầu (vài nghìn lần kiểm tra va chạm).
    /// </summary>
    public static class NavGrid
    {
        public static Vector2 Min = new Vector2(-0.6f, -9.6f), Max = new Vector2(30.2f, 17.6f);
        public const float Cell = 0.25f;
        public const int Floors = 2;
        /// <summary>Mặt sàn tầng 2 (trùng LabSceneBuilder.F2_Y).</summary>
        public const float Floor2Y = 3.8f;

        // Thang bộ một vế (trùng LabSceneBuilder.Floor2.Stairs): chân thang ở tầng 1, đầu thang ở tầng 2
        const float StairX = 29.175f, StairZ0 = 0.33f, StairZ1 = 5.9f;
        public static readonly Vector3 StairBottom = new Vector3(StairX, 0f, StairZ0), StairTop = new Vector3(StairX, Floor2Y, StairZ1);

        static bool[][,] walk;
        static int nx, nz;
        static Vector3 stairFoot, stairHead;   // ô đi được sát chân thang (tầng 1) và sát đầu thang (tầng 2)
        static bool stairOk;
        static readonly Collider[] hits = new Collider[16];

        public static bool Ready => walk != null;
        /// <summary>Hai tầng có nối được với nhau qua thang bộ không (cả chân và đầu thang đều có ô đi được).</summary>
        public static bool StairLinked { get { if (!Ready) Build(); return stairOk; } }

        public static float BaseY(int floor) => floor >= 1 ? Floor2Y : 0f;
        /// <summary>Tầng của một vật / điểm trong phòng (trần tầng 1 cao 3,6 m, như ZoneMap.At).</summary>
        public static int FloorOf(Vector3 p) => p.y >= 3.6f ? 1 : 0;
        /// <summary>Tầng theo vị trí bàn chân (robot đang ở giữa thang thì tính tầng gần hơn).</summary>
        public static int FloorOfFeet(Vector3 feet) => feet.y > Floor2Y * 0.5f ? 1 : 0;
        /// <summary>Tầng của người xem theo vị trí mắt (mắt cao 1,2 - 1,7 m trên sàn).</summary>
        public static int FloorOfEye(Vector3 eye) => eye.y - 1.2f >= Floor2Y - 0.6f ? 1 : 0;

        /// <summary>Chân robot đang ở trên dốc thang (chưa chạm sàn tầng nào).</summary>
        public static bool OnStair(Vector3 feet) => feet.y > 0.05f && feet.y < Floor2Y - 0.05f && Mathf.Abs(feet.x - StairX) < 0.9f && feet.z > StairZ0 - 0.3f && feet.z < StairZ1 + 0.3f;

        static void Size()
        {
            nx = Mathf.CeilToInt((Max.x - Min.x) / Cell);
            nz = Mathf.CeilToInt((Max.y - Min.y) / Cell);
        }

        public static void Build()
        {
            Size();
            walk = new bool[Floors][,];
            var data = Object.FindAnyObjectByType<NavGridData>();
            bool baked = data != null && data.nx == nx && data.nz == nz && data.walk != null && data.walk.Length == nx * nz
                         && data.walk2 != null && data.walk2.Length == nx * nz;
            // Cảnh dựng từ trước khi có lưới hai tầng (lưới một tầng, hẹp hơn, cùng gốc): dùng lại nguyên cho tầng 1 như cũ,
            // tầng 2 và lõi thang coi như chưa đi được, tới khi dựng lại cảnh. Không tính lại bằng va chạm vì các khu ở xa đã gỡ.
            bool legacy = !baked && data != null && data.walk != null && data.nz == nz && data.nx > 0 && data.nx <= nx && data.walk.Length == data.nx * nz;
            if (data != null && !baked)
                Debug.LogWarning("[KHKT] Lưới đi robot nướng sẵn là bản cũ (một tầng): robot chỉ đi được ở tầng 1. Hãy dựng lại cảnh (KHKT > Dựng phòng thí nghiệm).");
            for (int f = 0; f < Floors; f++)
            {
                walk[f] = new bool[nx, nz];
                var src = f == 0 ? data?.walk : data?.walk2;
                for (int i = 0; i < nx; i++)
                    for (int k = 0; k < nz; k++)
                        walk[f][i, k] = baked ? src[i * nz + k] != 0
                                      : legacy ? f == 0 && i < data.nx && data.walk[i * nz + k] != 0
                                      : Free(CellCenter(i, k, f), 0.2f);
            }
            LinkStairs();
        }

        /// <summary>Tính lưới một tầng ngay bây giờ theo va chạm trong cảnh (trình dựng cảnh gọi để nướng vào NavGridData).</summary>
        public static byte[] Bake(int floor, out int cellsX, out int cellsZ)
        {
            walk = null;
            Size();
            cellsX = nx;
            cellsZ = nz;
            var b = new byte[nx * nz];
            for (int i = 0; i < nx; i++)
                for (int k = 0; k < nz; k++)
                    b[i * nz + k] = Free(CellCenter(i, k, floor), 0.2f) ? (byte)1 : (byte)0;
            return b;
        }

        static void LinkStairs()
        {
            stairOk = Nearest(StairBottom + Vector3.back * 0.2f, out stairFoot, 1.0f) && Nearest(StairTop + Vector3.forward * 0.25f, out stairHead, 1.0f);
            if (!stairOk) Debug.LogWarning("[KHKT] Lưới đi robot: chân hoặc đầu thang bộ không có ô đi được, robot không đổi tầng được.");
        }

        /// <summary>
        /// Chỗ đứng (tâm robot bán kính r, trên mặt sàn của tầng chứa p) có trống không. Bỏ qua người xem, robot, vật cầm được,
        /// vùng trigger, cánh cửa tự động (cửa tự mở khi robot tới).
        /// </summary>
        public static bool Free(Vector3 p, float r)
        {
            float y0 = BaseY(FloorOf(p));
            // phải có sàn bên dưới
            if (!Physics.Raycast(new Vector3(p.x, y0 + 0.5f, p.z), Vector3.down, 0.8f, ~0, QueryTriggerInteraction.Ignore)) return false;
            int n = Physics.OverlapBoxNonAlloc(new Vector3(p.x, y0 + 0.75f, p.z), new Vector3(r, 0.62f, r), hits, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
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

        static Vector3 CellCenter(int i, int k, int floor) => new Vector3(Min.x + (i + 0.5f) * Cell, BaseY(floor), Min.y + (k + 0.5f) * Cell);

        static bool ToCell(Vector3 p, out int i, out int k)
        {
            i = Mathf.FloorToInt((p.x - Min.x) / Cell);
            k = Mathf.FloorToInt((p.z - Min.y) / Cell);
            return i >= 0 && k >= 0 && i < nx && k < nz;
        }

        /// <summary>Ô chứa p ở tầng chứa p (theo độ cao) có đi được không.</summary>
        public static bool Walkable(Vector3 p) => Ready && ToCell(p, out int i, out int k) && walk[FloorOf(p)][i, k];

        /// <summary>Ô đi được gần p nhất trong tầng chứa p (tìm theo vòng vuông mở rộng).</summary>
        public static bool Nearest(Vector3 p, out Vector3 result, float maxDist = 3f)
        {
            if (!Ready) Build();
            int f = FloorOf(p);
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
                        if (i < 0 || k < 0 || i >= nx || k >= nz || !walk[f][i, k]) continue;
                        var c = CellCenter(i, k, f);
                        float d = (new Vector2(c.x - p.x, c.z - p.z)).sqrMagnitude;
                        if (d < best) { best = d; result = c; found = true; }
                    }
                if (found) return true;
            }
            return false;
        }

        /// <summary>
        /// Đường đi từ a (vị trí chân robot) tới b (điểm trên sàn, độ cao cho biết tầng), null nếu không có đường.
        /// Khác tầng thì đi qua thang bộ. Robot đang đứng giữa thang thì đi tiếp theo dốc thang về phía tầng đích.
        /// </summary>
        public static List<Vector3> Path(Vector3 a, Vector3 b)
        {
            if (!Ready) Build();
            int fb = FloorOf(b);
            Vector3 goal = new Vector3(b.x, BaseY(fb), b.z);
            if (OnStair(a))
            {
                // đang ở trên dốc: đi hết dốc về tầng đích rồi mới vào lưới
                var tail = FlatPath(fb == 1 ? stairHead : stairFoot, goal);
                if (tail == null) return null;
                var p = new List<Vector3> { a, fb == 1 ? StairTop : StairBottom };
                p.AddRange(tail);
                return p;
            }
            int fa = FloorOfFeet(a);
            Vector3 start = new Vector3(a.x, BaseY(fa), a.z);
            if (fa == fb)
            {
                var same = FlatPath(start, goal);
                if (same != null) same[0] = a;
                return same;
            }
            if (!stairOk) return null;
            var first = FlatPath(start, fa == 0 ? stairFoot : stairHead);
            var second = FlatPath(fa == 0 ? stairHead : stairFoot, goal);
            if (first == null || second == null) return null;
            first[0] = a;
            first.Add(fa == 0 ? StairBottom : StairTop);
            first.Add(fa == 0 ? StairTop : StairBottom);
            first.AddRange(second);
            return first;
        }

        /// <summary>Đường đi trong một tầng (a và b cùng độ cao sàn).</summary>
        static List<Vector3> FlatPath(Vector3 a, Vector3 b)
        {
            int f = FloorOf(a);
            if (!Nearest(a, out var sa, 2f) || !Nearest(b, out var sb, 2f)) return null;
            ToCell(sa, out int ai, out int ak);
            ToCell(sb, out int bi, out int bk);
            var w = walk[f];
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
                    if (i < 0 || k < 0 || i >= nx || k >= nz || !w[i, k]) continue;
                    if (d >= 4 && (!w[ci + di[d], ck] || !w[ci, ck + dk[d]])) continue;  // không cắt góc
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
                cells.Add(CellCenter(id / nz, id % nz, f));
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
            path.Add(new Vector3(b.x, BaseY(f), b.z));
            return path;
        }

        static float H(int i, int k, int bi, int bk) => Mathf.Sqrt((i - bi) * (i - bi) + (k - bk) * (k - bk));

        static bool Line(Vector3 a, Vector3 b)
        {
            float len = Vector3.Distance(a, b);
            int n = Mathf.Max(1, Mathf.CeilToInt(len / (Cell * 0.5f)));
            for (int s = 0; s <= n; s++)
                if (!Walkable(Vector3.Lerp(a, b, s / (float)n))) return false;
            return true;
        }
    }
}
