using System.Collections.Generic;
using UnityEngine;

namespace Khkt.Demo
{
    /// <summary>
    /// Hút mục tiêu cho tia trỏ tay cầm (không cần trỏ trúng 100% mới tương tác được):
    ///   - tia trúng thẳng vật thao tác được thì luôn thắng;
    ///   - không trúng: tìm vật thao tác được trong NÓN quanh tia (góc StrengthDeg cộng kích thước nhìn thấy của vật,
    ///     nên vật nhỏ ở xa vẫn dễ bắt), nắn tia về tâm vật. Chỉ nhận khi tia nắn lại tới được vật (không bị che)
    ///     và HandController xác nhận vật đó làm được việc gì (cầm, đặt, rót...). Mặt bàn trống không bao giờ hút;
    ///   - DÍNH: đã hút vào vật nào thì giữ vật đó tới khi lệch quá ReleaseFactor lần nón, hoặc vật khác tốt hơn rõ rệt
    ///     (StickyBonus), nên run tay không làm mục tiêu nhảy qua lại;
    ///   - lia nhanh thì nón thu nhỏ (lướt qua các vật dễ dàng), chậm lại gần vật nào thì hút mạnh vào vật đó.
    /// Mặc định "Vừa" 5° (người dùng chọn 12/11/2026). Đổi bằng AimAssist.StrengthDeg (0 = tắt).
    /// </summary>
    public sealed class AimAssist
    {
        public static float StrengthDeg = 5f;
        public float ReleaseFactor = 1.6f;
        public float StickyBonus = 0.35f;
        public float SlowSpeed = 60f, FastSpeed = 180f, FastScale = 0.35f;
        public float MaxObjectExtent = 0.9f;  // vật lớn hơn (bàn, tường, sàn) không phải mục tiêu hút
        public float FallbackScale = 0.4f;    // tia thật đang trỏ mặt bàn đặt được: chỉ hút khi lệch dưới 0,4 nón (2°)

        /// <summary>Vật đang hút (để kiểm thử và hiển thị).</summary>
        public Object Target { get; private set; }
        /// <summary>Tốc độ quay của tia (°/s, đã làm mượt).</summary>
        public float Speed { get; private set; }
        /// <summary>Góc nón đang dùng (độ).</summary>
        public float ConeDeg { get; private set; }

        public delegate bool Probe(in RaycastHit h, out Object id);

        readonly Collider[] overlap = new Collider[96];
        readonly RaycastHit[] hits = new RaycastHit[16];
        readonly HashSet<Collider> seen = new HashSet<Collider>();
        readonly List<(Collider c, Vector3 aim, float excess, float dist)> cands = new List<(Collider, Vector3, float, float)>();
        Vector3 prevDir;
        bool hasPrev;

        public void Reset()
        {
            Target = null;
            hasPrev = false;
        }

        /// <param name="raw">tia thật của tay cầm</param>
        /// <param name="rawActionable">tia thật đã trúng vật thao tác được (rawId)</param>
        /// <returns>true nếu nắn tia sang mục tiêu khác: result là chỗ trúng mới</returns>
        public bool Assist(Ray raw, float reach, float dt, bool rawActionable, Object rawId, Probe probe, out RaycastHit result) =>
            Assist(raw, reach, dt, rawActionable, rawId, probe, false, out result);

        /// <param name="rawFallback">tia thật đã trúng chỗ làm được việc dự phòng (mặt bàn đặt được): chỉ hút sang vật khác
        /// khi rất gần (FallbackScale lần nón), để người chơi cố ý đặt xuống bàn thì không bị hút sang ô / bình bên cạnh</param>
        public bool Assist(Ray raw, float reach, float dt, bool rawActionable, Object rawId, Probe probe, bool rawFallback, out RaycastHit result)
        {
            result = default;
            Vector3 dir = raw.direction.normalized;
            if (hasPrev && dt > 0)
            {
                float w = Vector3.Angle(prevDir, dir) / dt;
                Speed += (w - Speed) * Mathf.Clamp01(dt / (0.08f + dt));
            }
            prevDir = dir;
            hasPrev = true;

            if (rawActionable)
            {
                Target = rawId;
                return false;
            }
            float k = Speed <= SlowSpeed ? 1f : Speed >= FastSpeed ? FastScale : Mathf.Lerp(1f, FastScale, (Speed - SlowSpeed) / (FastSpeed - SlowSpeed));
            ConeDeg = StrengthDeg * k;
            if (ConeDeg <= 0.01f)
            {
                Target = null;
                return false;
            }

            // gom vật quanh tia: các quả cầu nối nhau dọc tia, bán kính theo nón (nới theo ReleaseFactor để thấy mục tiêu đang dính)
            seen.Clear();
            cands.Clear();
            float tanOuter = Mathf.Tan(ConeDeg * ReleaseFactor * Mathf.Deg2Rad);
            for (float d = 0.2f; d < reach;)
            {
                float r = d * tanOuter + 0.03f;
                int n = Physics.OverlapSphereNonAlloc(raw.GetPoint(d), r, overlap, ~0, QueryTriggerInteraction.Collide);
                for (int i = 0; i < n; i++)
                {
                    var c = overlap[i];
                    if (c == null || c is CharacterController || !seen.Add(c)) continue;
                    var b = c.bounds;
                    float ext = b.extents.magnitude;
                    if (ext > MaxObjectExtent) continue;
                    Vector3 v = b.center - raw.origin;
                    float dist = v.magnitude;
                    if (dist < 0.15f || dist > reach) continue;
                    float ang = Vector3.Angle(dir, v);
                    float angRadius = Mathf.Atan(Mathf.Min(ext * 0.7f, 0.3f) / dist) * Mathf.Rad2Deg;
                    float excess = Mathf.Max(0f, ang - angRadius);
                    if (excess > ConeDeg * ReleaseFactor) continue;
                    cands.Add((c, b.center, excess, dist));
                }
                d += Mathf.Max(r, 0.06f);
            }
            if (cands.Count == 0)
            {
                Target = null;
                return false;
            }
            cands.Sort((x, y) => x.excess.CompareTo(y.excess));

            float best = float.MaxValue;
            Object bestId = null;
            RaycastHit bestHit = default;
            int tried = 0;
            foreach (var (c, aim, excess, dist) in cands)
            {
                if (tried++ >= 8) break;
                var r2 = new Ray(raw.origin, (aim - raw.origin).normalized);
                int n = Physics.RaycastNonAlloc(r2, hits, reach, ~0, QueryTriggerInteraction.Collide);
                float near = float.MaxValue;
                bool got = false;
                RaycastHit h = default;
                for (int i = 0; i < n; i++)
                {
                    if (hits[i].collider is CharacterController || hits[i].distance >= near) continue;
                    near = hits[i].distance;
                    h = hits[i];
                    got = true;
                }
                if (!got || !probe(h, out var id) || id == null) continue;
                bool sticky = id == Target;
                float accept = rawFallback ? ConeDeg * FallbackScale : ConeDeg;
                if (rawFallback && sticky) accept = ConeDeg * FallbackScale * ReleaseFactor;
                if (excess > (sticky && !rawFallback ? ConeDeg * ReleaseFactor : accept)) continue;
                float score = excess / ConeDeg + 0.03f * dist - (sticky ? StickyBonus : 0f);
                if (score < best)
                {
                    best = score;
                    bestId = id;
                    bestHit = h;
                }
            }
            Target = bestId;
            if (bestId == null) return false;
            result = bestHit;
            return true;
        }
    }
}
