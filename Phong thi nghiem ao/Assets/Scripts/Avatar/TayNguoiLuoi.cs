using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

namespace Khkt.Avatar
{
    /// <summary>
    /// Dựng bằng mã (không dùng tài nguyên ngoài) lưới bàn tay liền khối có skinning, móng tay, cổ găng,
    /// ống tay áo blouse có nếp vải và cổ tay áo.
    ///
    /// Bàn tay: trường khoảng cách có dấu (SDF) ghép mềm từ các khối giải phẫu – phiến bàn tay có vòm ngang,
    /// gờ xương bàn, gồ khớp, mô cái, mô út, đệm gan tay, màng kẽ ngón cái, cổ tay, mỏm trâm trụ, 5 ngón (mỗi
    /// đốt là nón tròn dẹt, gồ khớp mu, đệm đầu ngón). Ngón chỉ ghép mềm với gan tay (không dính nhau).
    /// Đa giác hóa bằng "surface nets" có chiếu đỉnh lên mặt và pháp tuyến theo gradient SDF → mặt cong mượt.
    /// Trọng số skinning theo nhãn khối gần nhất + toạ độ dọc chuỗi khớp (chỉ trộn cha–con quanh khớp),
    /// nên ngón này không kéo da ngón kia. Màu đỉnh: R nếp gấp, G mặt lòng bàn tay, B che khuất (AO) nướng
    /// sẵn từ SDF, A độ mỏng (đầu ngón). UV0.x = toạ độ dọc đốt quanh nếp, UV1 = vị trí tư thế gốc.
    /// </summary>
    public static class TayNguoiLuoi
    {
        // ------------------------------------------------------------------
        // Chỉ số xương của lưới bàn tay
        // ------------------------------------------------------------------
        public const int BonePalm = 0, BoneThumbCmc = 13, BoneThumbMcp = 14, BoneThumbIp = 15, BoneWrist = 16;
        public const int HandBoneCount = 17;
        public static int BoneFinger(int f, int p) => 1 + f * 3 + p;

        /// <summary>Mức chi tiết lưới: 0 Siêu nét, 1 Cao, 2 Vừa, 3 Thấp.</summary>
        public static float CellSize(int lod) => lod switch { 0 => 0.00125f, 1 => 0.0015f, 2 => 0.0021f, _ => 0.0030f };
        public static int SleeveSegments(int lod) => lod switch { 0 => 48, 1 => 40, 2 => 30, _ => 20 };

        /// <summary>Hình học tư thế gốc của bàn tay (toạ độ gốc lưới = gốc bàn tay ở tư thế gốc).</summary>
        public sealed class HandBind
        {
            public int side;                               // 0 phải, 1 trái
            public readonly Vector3[] mcp = new Vector3[4];
            public readonly Vector3[] dir = new Vector3[4];
            public readonly Vector3[] up = new Vector3[4];
            public readonly float[] w = new float[4];
            public readonly float[] l1 = new float[4], l2 = new float[4], l3 = new float[4];
            public Vector3 thumbC, thumbDir, thumbUp;
            public float thumbW, tl1, tl2, tl3;
            public Vector3 wristCenter;
            public float Sign => side == 0 ? 1f : -1f;
        }

        // ------------------------------------------------------------------
        // Trường khoảng cách bàn tay
        // ------------------------------------------------------------------
        const int LPalm = 0, LThenar = 1, LWrist = 2, LWeb = 3, LHypo = 4, LFinger = 10, LThumb = 30;

        struct Prim
        {
            public int type;          // 0 nón tròn (dẹt theo up), 1 elip
            public int label;
            public float k;           // bán kính ghép mềm vào nhóm
            // nón tròn
            public Vector3 a, ba, ax, up;
            public float l2, rr, a2, il2, r1, r2, ys;
            // elip
            public Vector3 c, r;
            public Quaternion inv;
        }

        sealed class HandSdf
        {
            readonly HandBind b;
            readonly float sgn;
            readonly List<Prim> palm = new List<Prim>();
            readonly List<Prim>[] chains = new List<Prim>[5];   // 4 ngón + ngón cái
            readonly float[] chainK = { 0.0055f, 0.0055f, 0.0055f, 0.0050f, 0.0105f };
            readonly Vector3[] chainC = new Vector3[5];
            readonly float[] chainR = new float[5];
            public Vector3 boundsMin, boundsMax;
            public readonly Quaternion[] nailToLocal = new Quaternion[5];

            public HandSdf(HandBind bind)
            {
                b = bind;
                sgn = bind.Sign;
                for (int i = 0; i < 5; i++) chains[i] = new List<Prim>();
                Build();
                // Khung móng tính ở luồng chính (Quaternion.LookRotation là hàm gốc của Unity)
                for (int f = 0; f < 5; f++)
                {
                    Vector3 fw = f < 4 ? b.dir[f] : b.thumbDir, u = f < 4 ? b.up[f] : b.thumbUp;
                    nailToLocal[f] = Quaternion.Inverse(Quaternion.LookRotation(fw, u));
                }
            }

            void Build()
            {
                Vector3 X = Vector3.right, Y = Vector3.up, Z = Vector3.forward;

                // Gờ xương bàn và gồ khớp bàn–ngón (mu tay)
                for (int f = 0; f < 4; f++)
                {
                    Vector3 m = b.mcp[f];
                    Vector3 baseP = new Vector3(m.x * 0.55f, 0.0015f, -0.030f);
                    palm.Add(Cone(baseP, m - b.dir[f] * 0.004f + Y * 0.0005f, 0.0060f, b.w[f] * 0.42f, Y, 1f, LPalm, 0.0085f));
                    palm.Add(Ellipsoid(m + Y * 0.0030f - b.dir[f] * 0.002f, new Vector3(b.w[f] * 0.47f, b.w[f] * 0.40f, b.w[f] * 0.50f),
                        Quaternion.LookRotation(b.dir[f], Y), LPalm, 0.0050f));
                }
                // Đệm gan tay dưới hàng khớp bàn–ngón
                palm.Add(Cone(b.mcp[0] + new Vector3(0f, -0.0070f, -0.006f), b.mcp[3] + new Vector3(0f, -0.0058f, -0.006f),
                    0.0078f, 0.0068f, Y, 0.85f, LPalm, 0.0090f));
                // Mô cái và mô út
                palm.Add(Ellipsoid(new Vector3(-0.0205f * sgn, -0.0095f, -0.0125f), new Vector3(0.0165f, 0.0110f, 0.0270f),
                    Quaternion.Euler(4f, -22f * sgn, 12f * sgn), LThenar, 0.0110f));
                palm.Add(Ellipsoid(new Vector3(0.0262f * sgn, -0.0074f, -0.0035f), new Vector3(0.0120f, 0.0092f, 0.0300f),
                    Quaternion.Euler(0f, 6f * sgn, -8f * sgn), LHypo, 0.0095f));
                // Màng kẽ ngón cái – ngón trỏ
                Vector3 tm = b.thumbC + b.thumbDir * b.tl1;
                Vector3 webC = Vector3.Lerp(tm, b.mcp[0], 0.42f) + new Vector3(0f, -0.0015f, -0.010f);
                Vector3 webDir = (b.mcp[0] - tm).normalized;
                palm.Add(Ellipsoid(webC, new Vector3(0.0050f, 0.0052f, 0.0175f), Quaternion.LookRotation(webDir, Y), LWeb, 0.0090f));
                // Cổ tay (elip dẹt) + mỏm trâm trụ
                Vector3 wc = b.wristCenter;
                palm.Add(Cone(new Vector3(0f, -0.0010f, wc.z + 0.012f), new Vector3(0f, -0.0005f, wc.z - 0.062f), 0.0262f, 0.0250f, Y, 0.62f, LWrist, 0.0130f));
                palm.Add(Ellipsoid(new Vector3(0.0245f * sgn, 0.0050f, wc.z - 0.008f), new Vector3(0.0058f, 0.0050f, 0.0065f), Quaternion.identity, LWrist, 0.0060f));

                // 4 ngón: đốt gần, giữa, xa + gồ khớp mu + đệm đầu ngón
                for (int f = 0; f < 4; f++)
                {
                    var ch = chains[f];
                    Vector3 d = b.dir[f], u = b.up[f];
                    float w = b.w[f];
                    Vector3 j0 = b.mcp[f], j1 = j0 + d * b.l1[f], j2 = j1 + d * b.l2[f];
                    float r0 = w * 0.53f, r1 = w * 0.485f, r2 = w * 0.435f, r3 = w * 0.385f;
                    int lb = LFinger + f * 3;
                    ch.Add(Cone(j0 - d * 0.007f, j1, r0, r1, u, 0.86f, lb, 0.0035f));
                    ch.Add(Cone(j1, j2, r1 * 0.97f, r2, u, 0.85f, lb + 1, 0.0035f));
                    Vector3 tipEnd = j2 + d * (b.l3[f] - r3 * 0.80f) - u * (r3 * 0.10f);
                    ch.Add(Cone(j2, tipEnd, r2 * 0.98f, r3, u, 0.82f, lb + 2, 0.0035f));
                    // gồ khớp mu PIP, DIP
                    ch.Add(Ellipsoid(j1 + u * (r1 * 0.32f), new Vector3(r1 * 0.74f, r1 * 0.55f, r1 * 0.90f), Quaternion.LookRotation(d, u), lb + 1, 0.0030f));
                    ch.Add(Ellipsoid(j2 + u * (r2 * 0.26f), new Vector3(r2 * 0.78f, r2 * 0.58f, r2 * 0.90f), Quaternion.LookRotation(d, u), lb + 2, 0.0026f));
                    // đệm thịt mặt lòng các đốt + đệm đầu ngón
                    ch.Add(Ellipsoid(j0 + d * (b.l1[f] * 0.52f) - u * (r0 * 0.24f), new Vector3(r0 * 0.80f, r0 * 0.46f, b.l1[f] * 0.42f), Quaternion.LookRotation(d, u), lb, 0.0030f));
                    ch.Add(Ellipsoid(j1 + d * (b.l2[f] * 0.52f) - u * (r1 * 0.24f), new Vector3(r1 * 0.78f, r1 * 0.45f, b.l2[f] * 0.40f), Quaternion.LookRotation(d, u), lb + 1, 0.0028f));
                    ch.Add(Ellipsoid(j2 + d * (b.l3[f] * 0.58f) - u * (r2 * 0.34f), new Vector3(r3 * 0.98f, r3 * 0.66f, b.l3[f] * 0.36f), Quaternion.LookRotation(d, u), lb + 2, 0.0026f));
                }

                // Ngón cái: xương bàn (ghép mềm rộng với mô cái), đốt gần, đốt xa
                {
                    var ch = chains[4];
                    Vector3 d = b.thumbDir, u = b.thumbUp;
                    float tw = b.thumbW;
                    Vector3 c = b.thumbC, m = c + d * b.tl1, i = m + d * b.tl2;
                    ch.Add(Cone(c + d * 0.004f, m, tw * 0.66f, tw * 0.57f, u, 0.86f, LThumb, 0.0040f));
                    ch.Add(Ellipsoid(m + u * 0.0025f, new Vector3(tw * 0.52f, tw * 0.44f, tw * 0.52f), Quaternion.LookRotation(d, u), LThumb + 1, 0.0035f));
                    ch.Add(Cone(m, i, tw * 0.56f, tw * 0.52f, u, 0.84f, LThumb + 1, 0.0035f));
                    Vector3 tipEnd = i + d * (b.tl3 - tw * 0.36f) - u * (tw * 0.06f);
                    ch.Add(Cone(i, tipEnd, tw * 0.51f, tw * 0.42f, u, 0.80f, LThumb + 2, 0.0035f));
                    ch.Add(Ellipsoid(i + d * (b.tl3 * 0.55f) - u * (tw * 0.18f), new Vector3(tw * 0.50f, tw * 0.36f, b.tl3 * 0.40f), Quaternion.LookRotation(d, u), LThumb + 2, 0.0028f));
                    ch.Add(Ellipsoid(m + d * (b.tl2 * 0.5f) - u * (tw * 0.22f), new Vector3(tw * 0.52f, tw * 0.36f, b.tl2 * 0.42f), Quaternion.LookRotation(d, u), LThumb + 1, 0.0030f));
                }

                // Hình cầu bao từng chuỗi để bỏ qua nhanh khi ở xa
                boundsMin = new Vector3(-0.050f, -0.030f, wc.z - 0.095f);
                boundsMax = new Vector3(0.050f, 0.024f, 0.06f);
                for (int g = 0; g < 5; g++)
                {
                    Vector3 lo = Vector3.one * 1e9f, hi = -Vector3.one * 1e9f;
                    foreach (var p in chains[g]) Encapsulate(p, ref lo, ref hi);
                    chainC[g] = (lo + hi) * 0.5f;
                    chainR[g] = (hi - lo).magnitude * 0.5f;
                    boundsMin = Vector3.Min(boundsMin, lo);
                    boundsMax = Vector3.Max(boundsMax, hi);
                }
                foreach (var p in palm)
                {
                    Vector3 lo = boundsMin, hi = boundsMax;
                    Encapsulate(p, ref lo, ref hi);
                    boundsMin = lo; boundsMax = hi;
                }
                boundsMin -= Vector3.one * 0.004f;
                boundsMax += Vector3.one * 0.004f;
            }

            static void Encapsulate(Prim p, ref Vector3 lo, ref Vector3 hi)
            {
                if (p.type == 0)
                {
                    float r = Mathf.Max(p.r1, p.r2);
                    Vector3 e = Vector3.one * r;
                    lo = Vector3.Min(lo, Vector3.Min(p.a, p.a + p.ba) - e);
                    hi = Vector3.Max(hi, Vector3.Max(p.a, p.a + p.ba) + e);
                }
                else
                {
                    float r = Mathf.Max(p.r.x, Mathf.Max(p.r.y, p.r.z));
                    lo = Vector3.Min(lo, p.c - Vector3.one * r);
                    hi = Vector3.Max(hi, p.c + Vector3.one * r);
                }
            }

            static Prim Cone(Vector3 a, Vector3 bb, float r1, float r2, Vector3 up, float ys, int label, float k)
            {
                Vector3 ba = bb - a;
                float l2 = Mathf.Max(ba.sqrMagnitude, 1e-10f);
                float rr = r1 - r2;
                Vector3 ax = ba / Mathf.Sqrt(l2);
                Vector3 u = Vector3.ProjectOnPlane(up, ax);
                u = u.sqrMagnitude > 1e-8f ? u.normalized : Vector3.up;
                return new Prim
                {
                    type = 0, label = label, k = k, a = a, ba = ba, ax = ax, up = u,
                    l2 = l2, rr = rr, a2 = l2 - rr * rr, il2 = 1f / l2, r1 = r1, r2 = r2, ys = ys
                };
            }

            static Prim Ellipsoid(Vector3 c, Vector3 r, Quaternion rot, int label, float k) =>
                new Prim { type = 1, label = label, k = k, c = c, r = r, inv = Quaternion.Inverse(rot) };

            static float EvalPrim(in Prim p, Vector3 q)
            {
                if (p.type == 1)
                {
                    Vector3 l = p.inv * (q - p.c);
                    float k0 = new Vector3(l.x / p.r.x, l.y / p.r.y, l.z / p.r.z).magnitude;
                    float k1 = new Vector3(l.x / (p.r.x * p.r.x), l.y / (p.r.y * p.r.y), l.z / (p.r.z * p.r.z)).magnitude;
                    if (k1 < 1e-9f) return -Mathf.Min(p.r.x, Mathf.Min(p.r.y, p.r.z));
                    return k0 * (k0 - 1f) / k1;
                }

                // Nón tròn dẹt theo trục "up" (iq round cone trong không gian đã giãn)
                Vector3 pa = q - p.a;
                if (p.ys < 0.999f)
                {
                    float along = Vector3.Dot(pa, p.ax);
                    Vector3 perp = pa - p.ax * along;
                    float pu = Vector3.Dot(perp, p.up);
                    pa = p.ax * along + (perp - p.up * pu) + p.up * (pu / p.ys);
                }
                float y = Vector3.Dot(pa, p.ba);
                float z = y - p.l2;
                Vector3 wv = pa * p.l2 - p.ba * y;
                float x2 = wv.sqrMagnitude;
                float y2 = y * y * p.l2;
                float z2 = z * z * p.l2;
                float kk = Mathf.Sign(p.rr) * p.rr * p.rr * x2;
                float d;
                if (Mathf.Sign(z) * p.a2 * z2 > kk) d = Mathf.Sqrt(x2 + z2) * p.il2 - p.r2;
                else if (Mathf.Sign(y) * p.a2 * y2 < kk) d = Mathf.Sqrt(x2 + y2) * p.il2 - p.r1;
                else d = (Mathf.Sqrt(x2 * p.a2 * p.il2) + y * p.rr) * p.il2 - p.r1;
                return p.ys < 0.999f ? d * p.ys : d;
            }

            static float SMin(float a, float b, float k)
            {
                float h = Mathf.Max(k - Mathf.Abs(a - b), 0f) / k;
                return Mathf.Min(a, b) - h * h * k * 0.25f;
            }

            float PalmSlab(Vector3 q)
            {
                // Phiến bàn tay: hộp bo tròn có vòm ngang (mu lồi, lòng lõm) và thu hẹp về phía cổ tay
                float cx = 0.0015f * sgn;
                float x = q.x - cx;
                float taper = Mathf.Lerp(0.80f, 1.0f, Mathf.Clamp01((q.z + 0.034f) / 0.054f));
                x /= taper;
                float y = q.y + 2.6f * x * x + 0.0015f;
                float z = q.z - 0.0035f;
                float bx = 0.0330f, by = 0.0056f, bz = 0.0365f, rad = 0.0070f;
                float qx = Mathf.Abs(x) - bx, qy = Mathf.Abs(y) - by, qz = Mathf.Abs(z) - bz;
                float outside = new Vector3(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f), Mathf.Max(qz, 0f)).magnitude;
                float inside = Mathf.Min(Mathf.Max(qx, Mathf.Max(qy, qz)), 0f);
                return (outside + inside - rad) * Mathf.Min(1f, taper);
            }

            float Palm(Vector3 q)
            {
                float d = PalmSlab(q);
                for (int i = 0; i < palm.Count; i++)
                {
                    var p = palm[i];
                    d = SMin(d, EvalPrim(p, q), p.k);
                }
                return d;
            }

            float Chain(int g, Vector3 q)
            {
                var ch = chains[g];
                float d = EvalPrim(ch[0], q);
                for (int i = 1; i < ch.Count; i++)
                {
                    var p = ch[i];
                    d = SMin(d, EvalPrim(p, q), p.k);
                }
                return d;
            }

            public float Eval(Vector3 q)
            {
                float pd = Palm(q);
                float best = pd;
                for (int g = 0; g < 5; g++)
                {
                    float lower = (q - chainC[g]).magnitude - chainR[g];
                    if (lower - chainK[g] * 0.25f > best) continue;
                    float cd = Chain(g, q);
                    float v = SMin(pd, cd, chainK[g]);
                    if (v < best) best = v;
                }
                return best;
            }

            /// <summary>Nhãn khối giải phẫu gần nhất tại q (để chia trọng số skinning).</summary>
            public int Label(Vector3 q)
            {
                float best = PalmSlab(q);
                int lab = LPalm;
                for (int i = 0; i < palm.Count; i++)
                {
                    float d = EvalPrim(palm[i], q);
                    if (d < best) { best = d; lab = palm[i].label; }
                }
                for (int g = 0; g < 5; g++)
                {
                    float lower = (q - chainC[g]).magnitude - chainR[g];
                    if (lower > best) continue;
                    var ch = chains[g];
                    for (int i = 0; i < ch.Count; i++)
                    {
                        float d = EvalPrim(ch[i], q);
                        if (d < best) { best = d; lab = ch[i].label; }
                    }
                }
                return lab;
            }

            public Vector3 Gradient(Vector3 q, float e)
            {
                float dx = Eval(q + new Vector3(e, 0, 0)) - Eval(q - new Vector3(e, 0, 0));
                float dy = Eval(q + new Vector3(0, e, 0)) - Eval(q - new Vector3(0, e, 0));
                float dz = Eval(q + new Vector3(0, 0, e)) - Eval(q - new Vector3(0, 0, e));
                return new Vector3(dx, dy, dz) / (2f * e);
            }

            /// <summary>Bắn tia từ start theo dir đến khi chạm mặt (sphere tracing). Trả về khoảng cách đi được.</summary>
            public float March(Vector3 start, Vector3 dir, float maxT)
            {
                float t = 0f;
                for (int i = 0; i < 96 && t < maxT; i++)
                {
                    float d = Eval(start + dir * t);
                    if (d < 0.00004f) return t;
                    t += Mathf.Max(d * 0.9f, 0.00015f);
                }
                return maxT;
            }
        }

        // ------------------------------------------------------------------
        // Kết quả dựng bàn tay
        // ------------------------------------------------------------------
        public sealed class HandMeshSet
        {
            public Mesh hand;
            public Mesh gloveCuff;
            public readonly Mesh[] nails = new Mesh[5];
            public float buildMs;
            public bool fromDisk;
            public int vertexCount;
        }

        sealed class PartData
        {
            public Vector3[] pos, nrm, bindPos;
            public Color[] col;
            public Vector2[] uv;
            public BoneWeight[] wts;
            public int[] tris;
            public string name;
        }

        sealed class HandData
        {
            public PartData hand, cuff;
            public readonly PartData[] nails = new PartData[5];
            public float ms;
            public bool fromDisk;
        }

        static readonly Dictionary<long, HandMeshSet> cache = new Dictionary<long, HandMeshSet>();
        static readonly Dictionary<long, Task<HandData>> jobs = new Dictionary<long, Task<HandData>>();

        public static void ClearCache() => cache.Clear();

        /// <summary>
        /// Lấy lưới bàn tay (một bên + mức chi tiết) từ bộ nhớ đệm; nếu chưa có thì dựng trên luồng nền và trả null
        /// (gọi lại ở khung sau). Đỉnh lưới nằm trong toạ độ gốc lưới ở tư thế gốc; móng i nằm trong toạ độ khớp đốt xa.
        /// </summary>
        public static HandMeshSet TryGetHand(HandBind bind, int lod, int bindKey, bool wait = false)
        {
            long key = ((long)bindKey << 8) | ((long)bind.side << 4) | (long)lod;
            if (cache.TryGetValue(key, out var set) && set.hand != null) return set;
            if (!jobs.TryGetValue(key, out var job))
            {
                var sdf = new HandSdf(bind);
                string file = CacheFile(bind, lod);
                job = Task.Run(() => LoadOrBuild(sdf, bind, lod, file));
                jobs[key] = job;
            }
            if (wait) job.Wait();
            if (!job.IsCompleted) return null;
            jobs.Remove(key);
            if (job.IsFaulted) { Debug.LogException(job.Exception); return null; }
            set = Finish(job.Result);
            LastFromDisk = job.Result.fromDisk;
            cache[key] = set;
            return set;
        }

        /// <summary>Lưới đã sẵn sàng để gán (đã có trong bộ đệm hoặc luồng nền đã xong).</summary>
        public static bool IsReady(int side, int lod, int bindKey)
        {
            long key = ((long)bindKey << 8) | ((long)side << 4) | (long)lod;
            if (cache.ContainsKey(key)) return true;
            return !jobs.TryGetValue(key, out var job) || job.IsCompleted;
        }

        static Mesh MakeMesh(PartData d, bool recalcNormals)
        {
            var m = new Mesh { name = d.name };
            if (d.pos.Length > 65000) m.indexFormat = IndexFormat.UInt32;
            m.vertices = d.pos;
            if (d.nrm != null) m.normals = d.nrm;
            m.colors = d.col;
            m.uv = d.uv;
            m.SetUVs(1, d.bindPos);
            m.triangles = d.tris;
            if (d.wts != null) m.boneWeights = d.wts;
            if (recalcNormals) m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        static HandMeshSet Finish(HandData d)
        {
            var set = new HandMeshSet { buildMs = d.ms, vertexCount = d.hand.pos.Length, fromDisk = d.fromDisk };
            set.hand = MakeMesh(d.hand, false);
            set.gloveCuff = MakeMesh(d.cuff, true);
            FixOutwardNormals(set.gloveCuff, new Vector3(0f, -0.0008f, 0f));
            for (int i = 0; i < 5; i++) set.nails[i] = MakeMesh(d.nails[i], true);
            return set;
        }

        static float SS(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        // ------------------------------------------------------------------
        // Bộ nhớ đệm lưới trên đĩa: lần đầu dựng (vài giây, luồng nền) rồi ghi; các lần sau đọc trong vài chục ms
        // ------------------------------------------------------------------
        const int CacheVersion = 3;   // tăng khi đổi hình học / trọng số / màu đỉnh

        static string CacheFile(HandBind b, int lod)
        {
            try
            {
                // băm toàn bộ hình học tư thế gốc → đổi kích thước tay là tự dựng lại
                unchecked
                {
                    int h = 17 * 31 + CacheVersion;
                    h = h * 31 + b.side; h = h * 31 + lod;
                    for (int f = 0; f < 4; f++)
                    {
                        h = h * 31 + H(b.mcp[f]); h = h * 31 + H(b.dir[f]); h = h * 31 + H(b.up[f]);
                        h = h * 31 + H(b.w[f]); h = h * 31 + H(b.l1[f]); h = h * 31 + H(b.l2[f]); h = h * 31 + H(b.l3[f]);
                    }
                    h = h * 31 + H(b.thumbC); h = h * 31 + H(b.thumbDir); h = h * 31 + H(b.thumbUp);
                    h = h * 31 + H(b.thumbW); h = h * 31 + H(b.tl1); h = h * 31 + H(b.tl2); h = h * 31 + H(b.tl3);
                    h = h * 31 + H(b.wristCenter);
                    string dir = Path.Combine(Application.persistentDataPath, "tay_luoi");
                    return Path.Combine(dir, $"ban_tay_v{CacheVersion}_{(b.side == 0 ? "phai" : "trai")}_lod{lod}_{h:x8}.bin");
                }
            }
            catch { return null; }
        }

        static int H(float v) => Mathf.RoundToInt(v * 100000f);
        static int H(Vector3 v) => unchecked(H(v.x) * 73856093 ^ H(v.y) * 19349663 ^ H(v.z) * 83492791);

        /// <summary>Xóa bộ nhớ đệm trên đĩa (kiểm thử dựng lại từ đầu).</summary>
        public static void ClearDiskCache()
        {
            try
            {
                string dir = Path.Combine(Application.persistentDataPath, "tay_luoi");
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
            catch (Exception e) { Debug.LogWarning("[TayNguoi] Không xóa được bộ đệm lưới: " + e.Message); }
        }

        /// <summary>Lần dựng gần nhất có đọc từ đĩa không (cho kiểm thử).</summary>
        public static bool LastFromDisk { get; private set; }

        static HandData LoadOrBuild(HandSdf sdf, HandBind b, int lod, string file)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            if (file != null && File.Exists(file))
            {
                try
                {
                    using var br = new BinaryReader(File.OpenRead(file));
                    if (br.ReadInt32() == CacheVersion)
                    {
                        var d = new HandData { hand = ReadPart(br), cuff = ReadPart(br) };
                        for (int i = 0; i < 5; i++) d.nails[i] = ReadPart(br);
                        d.ms = (float)sw.Elapsed.TotalMilliseconds;
                        d.fromDisk = true;
                        return d;
                    }
                }
                catch { /* tệp hỏng → dựng lại */ }
            }
            var data = BuildHandData(sdf, b, lod);
            if (file != null)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(file));
                    string tmp = file + ".tmp";
                    using (var bw = new BinaryWriter(File.Create(tmp)))
                    {
                        bw.Write(CacheVersion);
                        WritePart(bw, data.hand); WritePart(bw, data.cuff);
                        for (int i = 0; i < 5; i++) WritePart(bw, data.nails[i]);
                    }
                    if (File.Exists(file)) File.Delete(file);
                    File.Move(tmp, file);
                }
                catch { /* không ghi được (đĩa đầy, chỉ đọc) → vẫn dùng lưới vừa dựng */ }
            }
            return data;
        }

        static void WritePart(BinaryWriter w, PartData d)
        {
            w.Write(d.name ?? "");
            int n = d.pos.Length;
            w.Write(n);
            WriteV3(w, d.pos); w.Write(d.nrm != null); if (d.nrm != null) WriteV3(w, d.nrm);
            w.Write(ReferenceEquals(d.bindPos, d.pos)); if (!ReferenceEquals(d.bindPos, d.pos)) WriteV3(w, d.bindPos);
            for (int i = 0; i < n; i++) { var c = d.col[i]; w.Write(c.r); w.Write(c.g); w.Write(c.b); w.Write(c.a); }
            for (int i = 0; i < n; i++) { w.Write(d.uv[i].x); w.Write(d.uv[i].y); }
            w.Write(d.wts != null);
            if (d.wts != null)
                for (int i = 0; i < n; i++)
                {
                    var b = d.wts[i];
                    w.Write((byte)b.boneIndex0); w.Write((byte)b.boneIndex1); w.Write((byte)b.boneIndex2); w.Write((byte)b.boneIndex3);
                    w.Write(b.weight0); w.Write(b.weight1); w.Write(b.weight2); w.Write(b.weight3);
                }
            w.Write(d.tris.Length);
            for (int i = 0; i < d.tris.Length; i++) w.Write(d.tris[i]);
        }

        static PartData ReadPart(BinaryReader r)
        {
            var d = new PartData { name = r.ReadString() };
            int n = r.ReadInt32();
            d.pos = ReadV3(r, n);
            if (r.ReadBoolean()) d.nrm = ReadV3(r, n);
            d.bindPos = r.ReadBoolean() ? d.pos : ReadV3(r, n);
            d.col = new Color[n];
            for (int i = 0; i < n; i++) d.col[i] = new Color(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            d.uv = new Vector2[n];
            for (int i = 0; i < n; i++) d.uv[i] = new Vector2(r.ReadSingle(), r.ReadSingle());
            if (r.ReadBoolean())
            {
                d.wts = new BoneWeight[n];
                for (int i = 0; i < n; i++)
                {
                    var b = new BoneWeight { boneIndex0 = r.ReadByte(), boneIndex1 = r.ReadByte(), boneIndex2 = r.ReadByte(), boneIndex3 = r.ReadByte() };
                    b.weight0 = r.ReadSingle(); b.weight1 = r.ReadSingle(); b.weight2 = r.ReadSingle(); b.weight3 = r.ReadSingle();
                    d.wts[i] = b;
                }
            }
            int tc = r.ReadInt32();
            d.tris = new int[tc];
            for (int i = 0; i < tc; i++) d.tris[i] = r.ReadInt32();
            return d;
        }

        static void WriteV3(BinaryWriter w, Vector3[] a) { for (int i = 0; i < a.Length; i++) { w.Write(a[i].x); w.Write(a[i].y); w.Write(a[i].z); } }

        static Vector3[] ReadV3(BinaryReader r, int n)
        {
            var a = new Vector3[n];
            for (int i = 0; i < n; i++) a[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            return a;
        }

        static HandData BuildHandData(HandSdf sdf, HandBind b, int lod)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            float h = CellSize(lod);
            Vector3 lo = sdf.boundsMin, hi = sdf.boundsMax;
            int nx = Mathf.CeilToInt((hi.x - lo.x) / h) + 1;
            int ny = Mathf.CeilToInt((hi.y - lo.y) / h) + 1;
            int nz = Mathf.CeilToInt((hi.z - lo.z) / h) + 1;

            // 1) Lưới thô 4h để bỏ qua vùng xa mặt (SDF gần như Lipschitz 1)
            const int C = 4;
            int cx = nx / C + 2, cy = ny / C + 2, cz = nz / C + 2;
            var coarse = new float[cx * cy * cz];
            float H = h * C;
            for (int k = 0; k < cz; k++)
                for (int j = 0; j < cy; j++)
                    for (int i = 0; i < cx; i++)
                        coarse[(k * cy + j) * cx + i] = sdf.Eval(lo + new Vector3(i * H, j * H, k * H));

            var f = new float[nx * ny * nz];
            float skip = H * 0.87f + h * 2.5f;
            for (int k = 0; k < nz; k++)
                for (int j = 0; j < ny; j++)
                    for (int i = 0; i < nx; i++)
                    {
                        int ci = Mathf.Min((i + C / 2) / C, cx - 1), cj = Mathf.Min((j + C / 2) / C, cy - 1), ck = Mathf.Min((k + C / 2) / C, cz - 1);
                        float dc = coarse[(ck * cy + cj) * cx + ci];
                        int idx = (k * ny + j) * nx + i;
                        if (Mathf.Abs(dc) > skip * 1.15f) f[idx] = dc;
                        else f[idx] = sdf.Eval(lo + new Vector3(i * h, j * h, k * h));
                    }

            // 2) Surface nets: một đỉnh mỗi ô có đổi dấu
            int cnx = nx - 1, cny = ny - 1, cnz = nz - 1;
            var cellVert = new int[cnx * cny * cnz];
            var verts = new List<Vector3>(24000);
            var grads = new List<Vector3>(24000);
            for (int k = 0; k < cnz; k++)
                for (int j = 0; j < cny; j++)
                    for (int i = 0; i < cnx; i++)
                    {
                        int cidx = (k * cny + j) * cnx + i;
                        cellVert[cidx] = -1;
                        int mask = 0;
                        for (int c = 0; c < 8; c++)
                        {
                            int ii = i + (c & 1), jj = j + ((c >> 1) & 1), kk = k + ((c >> 2) & 1);
                            if (f[(kk * ny + jj) * nx + ii] < 0f) mask |= 1 << c;
                        }
                        if (mask == 0 || mask == 255) continue;

                        Vector3 acc = Vector3.zero;
                        int n = 0;
                        for (int e = 0; e < 12; e++)
                        {
                            int c0 = EdgeA[e], c1 = EdgeB[e];
                            bool in0 = (mask & (1 << c0)) != 0, in1 = (mask & (1 << c1)) != 0;
                            if (in0 == in1) continue;
                            float f0 = f[((k + ((c0 >> 2) & 1)) * ny + j + ((c0 >> 1) & 1)) * nx + i + (c0 & 1)];
                            float f1 = f[((k + ((c1 >> 2) & 1)) * ny + j + ((c1 >> 1) & 1)) * nx + i + (c1 & 1)];
                            float t = f0 / (f0 - f1);
                            Vector3 p0 = new Vector3(c0 & 1, (c0 >> 1) & 1, (c0 >> 2) & 1);
                            Vector3 p1 = new Vector3(c1 & 1, (c1 >> 1) & 1, (c1 >> 2) & 1);
                            acc += Vector3.Lerp(p0, p1, t);
                            n++;
                        }
                        Vector3 pos = lo + (new Vector3(i, j, k) + acc / n) * h;
                        // chiếu lên mặt theo gradient (1 bước Newton)
                        Vector3 g = sdf.Gradient(pos, h * 0.3f);
                        float gl = g.sqrMagnitude;
                        if (gl > 1e-6f)
                        {
                            Vector3 np = pos - g * (sdf.Eval(pos) / gl);
                            if ((np - pos).sqrMagnitude < h * h) pos = np;
                        }
                        cellVert[cidx] = verts.Count;
                        verts.Add(pos);
                        grads.Add(g);
                    }

            // 3) Mặt: mỗi cạnh lưới đổi dấu nối 4 ô quanh nó
            var tris = new List<int>(verts.Count * 6);
            for (int k = 1; k < nz - 1; k++)
                for (int j = 1; j < ny - 1; j++)
                    for (int i = 1; i < nx - 1; i++)
                    {
                        int idx = (k * ny + j) * nx + i;
                        bool in0 = f[idx] < 0f;
                        // cạnh theo x
                        if (i < nx - 1 && in0 != (f[idx + 1] < 0f))
                            Quad(tris, verts, cellVert, cnx, cny,
                                Cell(i, j - 1, k - 1), Cell(i, j, k - 1), Cell(i, j, k), Cell(i, j - 1, k), Vector3.right * (in0 ? 1f : -1f));
                        // cạnh theo y
                        if (j < ny - 1 && in0 != (f[idx + nx] < 0f))
                            Quad(tris, verts, cellVert, cnx, cny,
                                Cell(i - 1, j, k - 1), Cell(i, j, k - 1), Cell(i, j, k), Cell(i - 1, j, k), Vector3.up * (in0 ? 1f : -1f));
                        // cạnh theo z
                        if (k < nz - 1 && in0 != (f[idx + nx * ny] < 0f))
                            Quad(tris, verts, cellVert, cnx, cny,
                                Cell(i - 1, j - 1, k), Cell(i, j - 1, k), Cell(i, j, k), Cell(i - 1, j, k), Vector3.forward * (in0 ? 1f : -1f));
                    }

            // 4) Pháp tuyến, nhãn, trọng số, màu đỉnh
            int vc = verts.Count;
            var normals = new Vector3[vc];
            var colors = new Color[vc];
            var uv0 = new Vector2[vc];
            var weights = new BoneWeight[vc];
            float sgn = b.Sign;
            for (int v = 0; v < vc; v++)
            {
                Vector3 p = verts[v];
                Vector3 g = grads[v];
                Vector3 nrm = g.sqrMagnitude > 1e-10f ? g.normalized : Vector3.up;
                normals[v] = nrm;

                // AO nướng sẵn: lấy mẫu SDF dọc pháp tuyến
                float occ = 0f;
                for (int s = 1; s <= 3; s++)
                {
                    float hs = 0.0024f * s;
                    occ += Mathf.Max(0f, hs - sdf.Eval(p + nrm * hs)) / hs;
                }
                float ao = Mathf.Clamp(1f - occ * 0.42f, 0.30f, 1f);

                int label = sdf.Label(p);
                weights[v] = ComputeWeights(b, p, label, nrm, out float crease, out float uvx, out float thin, out float palmar);
                colors[v] = new Color(crease, palmar, ao, thin);
                uv0[v] = new Vector2(uvx, 0f);
            }

            var va = verts.ToArray();
            var data = new HandData();
            data.hand = new PartData
            {
                name = $"Bàn tay {(b.side == 0 ? "phải" : "trái")} LOD{lod}",
                pos = va, nrm = normals, col = colors, uv = uv0, bindPos = va, wts = weights, tris = tris.ToArray()
            };
            data.cuff = BuildGloveCuff(sdf, b, lod);
            for (int fi = 0; fi < 5; fi++) data.nails[fi] = BuildNail(sdf, b, fi, lod);
            data.ms = (float)sw.Elapsed.TotalMilliseconds;
            return data;

            int Cell(int ci, int cj, int ck) => (ck * cny + cj) * cnx + ci;
        }

        static readonly int[] EdgeA = { 0, 2, 4, 6, 0, 1, 4, 5, 0, 1, 2, 3 };
        static readonly int[] EdgeB = { 1, 3, 5, 7, 2, 3, 6, 7, 4, 5, 6, 7 };

        static void Quad(List<int> tris, List<Vector3> verts, int[] cellVert, int cnx, int cny, int c0, int c1, int c2, int c3, Vector3 outward)
        {
            int a = cellVert[c0], b = cellVert[c1], c = cellVert[c2], d = cellVert[c3];
            if (a < 0 || b < 0 || c < 0 || d < 0) return;
            Vector3 pa = verts[a], pb = verts[b], pc = verts[c], pd = verts[d];
            Vector3 n = Vector3.Cross(pb - pa, pc - pa) + Vector3.Cross(pc - pa, pd - pa);
            // Unity: mặt trước của tam giác (a,b,c) có pháp tuyến (b-a)×(c-a)
            bool same = Vector3.Dot(n, outward) > 0f;
            bool diagAC = (pa - pc).sqrMagnitude <= (pb - pd).sqrMagnitude;
            if (same)
            {
                if (diagAC) { tris.Add(a); tris.Add(b); tris.Add(c); tris.Add(a); tris.Add(c); tris.Add(d); }
                else { tris.Add(a); tris.Add(b); tris.Add(d); tris.Add(b); tris.Add(c); tris.Add(d); }
            }
            else
            {
                if (diagAC) { tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(a); tris.Add(d); tris.Add(c); }
                else { tris.Add(a); tris.Add(d); tris.Add(b); tris.Add(b); tris.Add(d); tris.Add(c); }
            }
        }

        // ------------------------------------------------------------------
        // Trọng số skinning + màu nếp gấp
        // ------------------------------------------------------------------
        static BoneWeight ComputeWeights(HandBind b, Vector3 p, int label, Vector3 n, out float crease, out float uvx, out float thin, out float palmar)
        {
            Span<float> w = stackalloc float[HandBoneCount];
            crease = 0f; uvx = 0f; thin = 0f;
            palmar = SS(0.15f, -0.55f, n.y);

            if (label >= LFinger && label < LThumb)
            {
                int f = (label - LFinger) / 3;
                FingerWeights(b, f, p, n, w, ref crease, ref uvx, ref thin);
            }
            else if (label >= LThumb)
            {
                ThumbWeights(b, p, n, w, ref crease, ref uvx, ref thin, ref palmar);
            }
            else if (label == LThenar)
            {
                float dl = SegDist(p, b.thumbC, b.thumbC + b.thumbDir * b.tl1);
                float wc = 0.55f * (1f - SS(0.006f, 0.022f, dl));
                w[BoneThumbCmc] += wc;
                w[BonePalm] += 1f - wc;
            }
            else if (label == LWeb)
            {
                Vector3 tm = b.thumbC + b.thumbDir * b.tl1;
                Vector3 seg = b.mcp[0] - tm;
                float t = Mathf.Clamp01(Vector3.Dot(p - tm, seg) / seg.sqrMagnitude);
                float wc = 0.62f * (1f - t);
                float wm = 0.18f * (1f - t) * (1f - t);
                w[BoneThumbCmc] += wc;
                w[BoneThumbMcp] += wm;
                w[BonePalm] += 1f - wc - wm;
            }
            else
            {
                // Gan tay / mu tay: vùng gần khớp bàn–ngón đi theo đốt gần của ngón gần nhất
                int bestF = -1;
                float bestA = 0f;
                for (int f = 0; f < 4; f++)
                {
                    Vector3 r = p - b.mcp[f];
                    float s = Vector3.Dot(r, b.dir[f]);
                    float lat = (r - b.dir[f] * s).magnitude;
                    float a = SS(-0.0085f, 0.0085f, s) * (1f - SS(b.w[f] * 0.55f, b.w[f] * 1.30f, lat));
                    if (a > bestA) { bestA = a; bestF = f; }
                }
                if (bestF >= 0)
                {
                    w[BoneFinger(bestF, 0)] += bestA;
                    w[BonePalm] += 1f - bestA;
                }
                else w[BonePalm] += 1f;

                if (label != LWrist)
                {
                    // Đường chỉ tay lòng bàn tay (đường tim, đường đầu, đường đời) + nếp cổ tay
                    float dmin = 1f;
                    dmin = Mathf.Min(dmin, PolyDist(p, HeartLine, b.Sign));
                    dmin = Mathf.Min(dmin, PolyDist(p, HeadLine, b.Sign));
                    dmin = Mathf.Min(dmin, PolyDist(p, LifeLine, b.Sign));
                    float cr = Mathf.Exp(-(dmin * dmin) / (0.0011f * 0.0011f)) * palmar;
                    if (cr > crease) { crease = cr; uvx = dmin; }
                }
            }

            // Nếp cổ tay mặt lòng
            {
                float dz1 = p.z - (b.wristCenter.z + 0.006f), dz2 = p.z - (b.wristCenter.z - 0.002f);
                float cr = Mathf.Max(Mathf.Exp(-(dz1 * dz1) / (0.0009f * 0.0009f)), Mathf.Exp(-(dz2 * dz2) / (0.0009f * 0.0009f)) * 0.7f) * palmar;
                if (cr > crease) { crease = cr; uvx = Mathf.Abs(dz1) < Mathf.Abs(dz2) ? dz1 : dz2; }
            }

            // Phần "gan tay" gần cổ tay đi theo xương cẳng tay (cổ tay ảo nằm trong ống tay áo)
            float palmW = w[BonePalm];
            float tw = SS(b.wristCenter.z + 0.012f, b.wristCenter.z - 0.012f, p.z);
            w[BonePalm] = palmW * (1f - tw);
            w[BoneWrist] += palmW * tw;

            return Top4(w);
        }

        static void FingerWeights(HandBind b, int f, Vector3 p, Vector3 n, Span<float> w, ref float crease, ref float uvx, ref float thin)
        {
            Vector3 r = p - b.mcp[f];
            float s = Vector3.Dot(r, b.dir[f]);
            float l1 = b.l1[f], l2 = b.l2[f], l3 = b.l3[f];
            float a = SS(-0.0075f, 0.0075f, s);
            float bb = SS(l1 - 0.0045f, l1 + 0.0045f, s);
            float c = SS(l1 + l2 - 0.0036f, l1 + l2 + 0.0036f, s);
            w[BonePalm] += 1f - a;
            w[BoneFinger(f, 0)] += a * (1f - bb);
            w[BoneFinger(f, 1)] += bb * (1f - c);
            w[BoneFinger(f, 2)] += c;

            float palmar = SS(0.15f, -0.55f, Vector3.Dot(n, b.up[f]));
            float dorsal = SS(0.05f, 0.6f, Vector3.Dot(n, b.up[f]));
            // nếp mặt lòng: gốc ngón (0,8 cm), PIP (đôi), DIP
            Crease(s - 0.0075f, 0.0010f, palmar, ref crease, ref uvx);
            Crease(s - (l1 - 0.0010f), 0.0009f, palmar, ref crease, ref uvx);
            Crease(s - (l1 + 0.0016f), 0.0008f, palmar * 0.8f, ref crease, ref uvx);
            Crease(s - (l1 + l2), 0.0008f, palmar * 0.9f, ref crease, ref uvx);
            // nếp nhăn mu khớp: vùng rộng hơn, nhạt hơn
            Crease(s - l1, 0.0040f, dorsal * 0.75f, ref crease, ref uvx);
            Crease(s - (l1 + l2), 0.0030f, dorsal * 0.55f, ref crease, ref uvx);
            Crease(s, 0.0050f, dorsal * 0.35f, ref crease, ref uvx);
            thin = SS(l1 + l2 + l3 * 0.35f, l1 + l2 + l3, s);
        }

        static void ThumbWeights(HandBind b, Vector3 p, Vector3 n, Span<float> w, ref float crease, ref float uvx, ref float thin, ref float palmar)
        {
            float s = Vector3.Dot(p - b.thumbC, b.thumbDir);
            float jm = b.tl1, ji = b.tl1 + b.tl2;
            float a = SS(-0.004f, 0.012f, s);
            float bb = SS(jm - 0.0055f, jm + 0.0055f, s);
            float c = SS(ji - 0.0040f, ji + 0.0040f, s);
            w[BonePalm] += 1f - a;
            w[BoneThumbCmc] += a * (1f - bb);
            w[BoneThumbMcp] += bb * (1f - c);
            w[BoneThumbIp] += c;
            palmar = SS(0.15f, -0.55f, Vector3.Dot(n, b.thumbUp));
            float dorsal = SS(0.05f, 0.6f, Vector3.Dot(n, b.thumbUp));
            Crease(s - jm, 0.0010f, palmar, ref crease, ref uvx);
            Crease(s - ji, 0.0009f, palmar, ref crease, ref uvx);
            Crease(s - ji, 0.0035f, dorsal * 0.6f, ref crease, ref uvx);
            Crease(s - jm, 0.0040f, dorsal * 0.4f, ref crease, ref uvx);
            thin = SS(ji + b.tl3 * 0.35f, ji + b.tl3, s);
        }

        static void Crease(float d, float width, float mask, ref float crease, ref float uvx)
        {
            float c = Mathf.Exp(-(d * d) / (width * width)) * mask;
            if (c > crease) { crease = c; uvx = d; }
        }

        static float SegDist(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-10f));
            return (p - (a + ab * t)).magnitude;
        }

        // Đường chỉ tay (x theo tay phải, z) – mặt lòng
        static readonly Vector2[] HeartLine = { new Vector2(0.040f, 0.024f), new Vector2(0.016f, 0.029f), new Vector2(-0.008f, 0.035f), new Vector2(-0.020f, 0.041f) };
        static readonly Vector2[] HeadLine = { new Vector2(-0.034f, 0.020f), new Vector2(-0.012f, 0.013f), new Vector2(0.014f, 0.010f), new Vector2(0.034f, 0.004f) };
        static readonly Vector2[] LifeLine = { new Vector2(-0.034f, 0.020f), new Vector2(-0.021f, 0.006f), new Vector2(-0.013f, -0.012f), new Vector2(-0.011f, -0.032f) };

        static float PolyDist(Vector3 p, Vector2[] line, float sgn)
        {
            float best = 1f;
            Vector2 q = new Vector2(p.x * sgn, p.z);
            for (int i = 0; i + 1 < line.Length; i++)
            {
                Vector2 a = line[i], ab = line[i + 1] - line[i];
                float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, (q - (a + ab * t)).magnitude);
            }
            return best;
        }

        static BoneWeight Top4(Span<float> w)
        {
            int i0 = -1, i1 = -1, i2 = -1, i3 = -1;
            float w0 = 0, w1 = 0, w2 = 0, w3 = 0;
            for (int i = 0; i < w.Length; i++)
            {
                float v = w[i];
                if (v <= 1e-4f) continue;
                if (v > w0) { i3 = i2; w3 = w2; i2 = i1; w2 = w1; i1 = i0; w1 = w0; i0 = i; w0 = v; }
                else if (v > w1) { i3 = i2; w3 = w2; i2 = i1; w2 = w1; i1 = i; w1 = v; }
                else if (v > w2) { i3 = i2; w3 = w2; i2 = i; w2 = v; }
                else if (v > w3) { i3 = i; w3 = v; }
            }
            float sum = w0 + w1 + w2 + w3;
            if (sum <= 1e-6f) return new BoneWeight { boneIndex0 = BonePalm, weight0 = 1f };
            return new BoneWeight
            {
                boneIndex0 = Mathf.Max(i0, 0), weight0 = w0 / sum,
                boneIndex1 = Mathf.Max(i1, 0), weight1 = w1 / sum,
                boneIndex2 = Mathf.Max(i2, 0), weight2 = w2 / sum,
                boneIndex3 = Mathf.Max(i3, 0), weight3 = w3 / sum
            };
        }

        // ------------------------------------------------------------------
        // Móng tay: tấm cong bám đúng mặt mu đốt xa (dò mặt bằng SDF), có liềm móng và bờ tự do
        // ------------------------------------------------------------------
        static PartData BuildNail(HandSdf sdf, HandBind b, int f, int lod)
        {
            Vector3 origin, fwd, up;
            float w3, l3;
            if (f < 4)
            {
                fwd = b.dir[f]; up = b.up[f];
                origin = b.mcp[f] + fwd * (b.l1[f] + b.l2[f]);
                w3 = b.w[f] * 0.81f; l3 = b.l3[f];
            }
            else
            {
                fwd = b.thumbDir; up = b.thumbUp;
                origin = b.thumbC + fwd * (b.tl1 + b.tl2);
                w3 = b.thumbW * 0.80f; l3 = b.tl3;
            }
            Vector3 right = Vector3.Cross(up, fwd).normalized;
            Quaternion toLocal = sdf.nailToLocal[f];

            int nu = lod <= 1 ? 9 : 7, nv = lod <= 1 ? 11 : 8;
            float halfW = w3 * 0.40f;
            float z0 = l3 * 0.30f, zLim = l3 * 0.78f, z1 = l3 * 0.96f;
            var pos = new Vector3[nu * (nv + 1)];
            var col = new Color[pos.Length];
            var uv = new Vector2[pos.Length];
            var bindPos = new Vector3[pos.Length];
            float[] slope = new float[nu];
            float[] yLim = new float[nu];

            for (int vi = 0; vi < nv; vi++)
            {
                float tv = vi / (float)(nv - 1);
                float z = Mathf.Lerp(z0, z1, tv);
                float L = z1 - z0;
                // bo tròn hai góc gốc móng (bờ móng hình cung)
                float corner = z < z0 + 0.22f * L ? Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((z0 + 0.22f * L - z) / (0.22f * L), 2f))) * 0.30f + 0.70f : 1f;
                for (int ui = 0; ui < nu; ui++)
                {
                    float tu = ui / (float)(nu - 1) * 2f - 1f;
                    float x = tu * halfW * corner;
                    float zz = z + (vi == nv - 1 ? 0.0009f * (1f - tu * tu) : 0f);
                    float y;
                    if (zz <= zLim)
                    {
                        Vector3 top = origin + right * x + fwd * zz + up * 0.016f;
                        float t = sdf.March(top, -up, 0.03f);
                        y = 0.016f - t + 0.00035f;
                        if (vi > 0) slope[ui] = (y - yLim[ui]) / Mathf.Max(1e-5f, zz - Mathf.Lerp(z0, z1, (vi - 1) / (float)(nv - 1)));
                        yLim[ui] = y;
                    }
                    else
                    {
                        float zl = Mathf.Min(zz, zLim);
                        y = yLim[ui] + Mathf.Clamp(slope[ui], -0.25f, 0.05f) * (zz - zl) * 0.6f;
                    }
                    float edge = Mathf.Abs(tu);
                    y -= 0.00030f * SS(0.72f, 1f, edge);
                    y -= 0.00040f * (1f - SS(0f, 0.12f, tv));
                    Vector3 world = origin + right * x + fwd * zz + up * y;
                    int idx = vi * nu + ui;
                    pos[idx] = toLocal * (world - origin);
                    bindPos[idx] = world;
                    float lunula = Mathf.Exp(-Mathf.Pow((tv - 0.04f) / 0.16f, 2f)) * (1f - SS(0.45f, 0.85f, edge)) * 0.55f;
                    float free = SS(0.86f, 0.95f, tv) * 0.85f;
                    uv[idx] = new Vector2(0f, Mathf.Max(lunula, free));
                    col[idx] = new Color(0f, 0f, 1f - 0.25f * SS(0.8f, 1f, edge), 0.35f);
                }
            }
            // bờ tự do: hàng mép dưới tạo độ dày móng
            for (int ui = 0; ui < nu; ui++)
            {
                int top = (nv - 1) * nu + ui, idx = nv * nu + ui;
                pos[idx] = pos[top] + toLocal * (-up * 0.0006f) + toLocal * (-fwd * 0.0002f);
                bindPos[idx] = bindPos[top] - up * 0.0006f;
                uv[idx] = uv[top];
                col[idx] = new Color(0f, 0f, 0.8f, 0.35f);
            }

            var tris = new List<int>();
            for (int vi = 0; vi < nv; vi++)
                for (int ui = 0; ui < nu - 1; ui++)
                {
                    int a = vi * nu + ui, bq = a + 1, c = a + nu, d = c + 1;
                    tris.Add(a); tris.Add(c); tris.Add(bq);
                    tris.Add(bq); tris.Add(c); tris.Add(d);
                }
            return new PartData { name = f < 4 ? $"Móng ngón {f + 1}" : "Móng ngón cái", pos = pos, col = col, uv = uv, bindPos = bindPos, tris = tris.ToArray() };
        }

        // ------------------------------------------------------------------
        // Cổ găng nitrile: vòng cuộn mép quanh cổ tay (bám mặt SDF), skinning theo cổ tay
        // ------------------------------------------------------------------
        static PartData BuildGloveCuff(HandSdf sdf, HandBind b, int lod)
        {
            int seg = lod <= 1 ? 40 : 24;
            float zc = b.wristCenter.z;
            float[] rowZ = { zc - 0.040f, zc - 0.028f, zc - 0.016f, zc - 0.0105f, zc - 0.0075f, zc - 0.0050f, zc - 0.0035f };
            float[] rowOff = { 0.00070f, 0.00075f, 0.00080f, 0.00140f, 0.00190f, 0.00140f, 0.00035f };
            int rows = rowZ.Length;
            var pos = new Vector3[rows * seg];
            var col = new Color[pos.Length];
            var wts = new BoneWeight[pos.Length];
            var uv0 = new Vector2[pos.Length];
            Vector3 axisC = new Vector3(0f, -0.0008f, 0f);
            for (int r = 0; r < rows; r++)
                for (int s = 0; s < seg; s++)
                {
                    float th = s / (float)seg * Mathf.PI * 2f;
                    Vector3 dir = new Vector3(Mathf.Cos(th), Mathf.Sin(th), 0f);
                    Vector3 center = new Vector3(axisC.x, axisC.y, rowZ[r]);
                    float t = sdf.March(center + dir * 0.06f, -dir, 0.06f);
                    float rad = 0.06f - t;
                    Vector3 p = center + dir * (rad + rowOff[r]);
                    int idx = r * seg + s;
                    pos[idx] = p;
                    col[idx] = new Color(r >= 3 && r <= 5 ? 0.6f : 0f, 0f, 1f, 0.2f);
                    uv0[idx] = new Vector2(p.z - (zc - 0.0075f), 0f);
                    float tw = SS(zc + 0.012f, zc - 0.012f, p.z);
                    wts[idx] = new BoneWeight { boneIndex0 = BoneWrist, weight0 = tw, boneIndex1 = BonePalm, weight1 = 1f - tw };
                }
            var tris = new List<int>();
            for (int r = 0; r < rows - 1; r++)
                for (int s = 0; s < seg; s++)
                {
                    int s1 = (s + 1) % seg;
                    int a = r * seg + s, bq = r * seg + s1, c = (r + 1) * seg + s, d = (r + 1) * seg + s1;
                    // pháp tuyến hướng ra ngoài (Unity: chiều kim đồng hồ nhìn từ ngoài)
                    tris.Add(a); tris.Add(c); tris.Add(bq);
                    tris.Add(bq); tris.Add(c); tris.Add(d);
                }
            return new PartData { name = "Cổ găng nitrile", pos = pos, col = col, uv = uv0, bindPos = pos, wts = wts, tris = tris.ToArray() };
        }

        /// <summary>Lật thứ tự tam giác nếu pháp tuyến trung bình hướng vào trục (đảm bảo mặt ngoài).</summary>
        static void FixOutwardNormals(Mesh m, Vector3 axisPoint)
        {
            var v = m.vertices;
            var n = m.normals;
            float dot = 0f;
            for (int i = 0; i < v.Length; i++)
            {
                Vector3 radial = v[i] - axisPoint;
                radial.z = 0f;
                dot += Vector3.Dot(radial, n[i]);
            }
            if (dot >= 0f) return;
            var t = m.triangles;
            for (int i = 0; i < t.Length; i += 3) (t[i + 1], t[i + 2]) = (t[i + 2], t[i + 1]);
            m.triangles = t;
            m.RecalculateNormals();
        }

        // ------------------------------------------------------------------
        // Ống tay áo blouse
        // ------------------------------------------------------------------
        public const int SleeveBoneUpper = 0, SleeveBoneElbow = 1, SleeveBoneTwist1 = 2, SleeveBoneTwist2 = 3, SleeveBoneCuff = 4;
        public const int SleeveBoneCount = 5;
        /// <summary>Chiều dài dải cổ tay áo (m).</summary>
        public const float CuffBandLen = 0.050f;

        /// <summary>Vị trí (trục z tư thế gốc) của 5 xương tay áo: vai 0, khuỷu L1, xoắn 1/3, xoắn 2/3, cổ tay áo L1+L2.</summary>
        public static float SleeveBoneZ(int bone, float l1, float l2) => bone switch
        {
            SleeveBoneUpper => 0f,
            SleeveBoneElbow => l1,
            SleeveBoneTwist1 => l1 + l2 * 0.36f,
            SleeveBoneTwist2 => l1 + l2 * 0.68f,
            _ => l1 + l2
        };

        static readonly Dictionary<long, Mesh> sleeveCache = new Dictionary<long, Mesh>();

        /// <summary>
        /// Ống tay áo dọc +z từ vai (z=0) qua khuỷu (z=l1) đến miệng cổ tay áo (z=l1+l2), mặt trong khuỷu là +y.
        /// Có nếp gấp khuỷu (dồn ở mặt trong), nếp rủ dọc, nếp dúm trên cổ tay áo, đường may dưới, dải cổ tay áo
        /// dày có hai đường chỉ, mép cuộn và mặt vải trong. Blendshape 0 "gập khuỷu" làm nếp khuỷu sâu hơn khi gập.
        /// l1 = 0 → chỉ dựng đoạn cẳng tay ngắn + cổ tay áo, một xương (dùng cho HandRig độc lập).
        /// </summary>
        public static Mesh GetSleeve(float l1, float l2, int side, int lod)
        {
            long key = ((long)Mathf.RoundToInt(l1 * 1000f) << 24) | ((long)Mathf.RoundToInt(l2 * 1000f) << 8) | ((long)side << 4) | (long)lod;
            if (sleeveCache.TryGetValue(key, out var m) && m != null) return m;
            m = BuildSleeve(l1, l2, side, lod);
            sleeveCache[key] = m;
            return m;
        }

        struct Row { public float z, rOff; public int kind; }   // kind 0 ngoài, 1 mép, 2 trong

        static Mesh BuildSleeve(float l1, float l2, int side, int lod)
        {
            bool full = l1 > 0.01f;
            int seg = SleeveSegments(lod);
            float zEnd = l1 + l2;
            float zStart = full ? -0.06f : zEnd - l2;
            float zCuff = zEnd - CuffBandLen;
            float dzBase = lod <= 1 ? 0.0075f : lod == 2 ? 0.010f : 0.015f;
            float dzFine = lod <= 1 ? 0.0032f : lod == 2 ? 0.0045f : 0.0075f;

            var rows = new List<Row>();
            float z = zStart;
            while (z < zEnd - 1e-4f)
            {
                rows.Add(new Row { z = z, kind = 0 });
                bool fine = (full && Mathf.Abs(z - l1) < 0.085f) || z > zCuff - 0.03f;
                z += fine ? dzFine : dzBase;
            }
            rows.Add(new Row { z = zEnd, kind = 0 });
            rows.Add(new Row { z = zEnd + 0.0011f, rOff = -0.0012f, kind = 1 });
            rows.Add(new Row { z = zEnd + 0.0006f, rOff = -0.0030f, kind = 1 });
            rows.Add(new Row { z = zEnd - 0.0008f, rOff = -0.0036f, kind = 2 });
            for (float zi = zEnd - 0.008f; zi > zEnd - 0.045f; zi -= 0.009f) rows.Add(new Row { z = zi, rOff = -0.0038f, kind = 2 });

            int rc = rows.Count;
            var pos = new Vector3[rc * seg];
            var posFlex = new Vector3[pos.Length];
            var col = new Color[pos.Length];
            var uv0 = new Vector2[pos.Length];
            var wts = new BoneWeight[pos.Length];
            float sideSign = side == 0 ? 1f : -1f;

            for (int r = 0; r < rc; r++)
            {
                Row row = rows[r];
                float rz = Mathf.Min(row.z, zEnd);
                float baseR = SleeveRadius(rz, l1, l2, full);
                for (int s = 0; s < seg; s++)
                {
                    // đường nối vòng (θ = -90°) đặt ở mặt dưới, trùng đường may
                    float th = -Mathf.PI * 0.5f + s / (float)seg * Mathf.PI * 2f;
                    float fold = 0f, foldFlex = 0f;
                    float seam = 0f, ao = 1f;
                    if (row.kind == 0)
                    {
                        fold = SleeveFold(th * sideSign, row.z, l1, l2, full, zCuff, out foldFlex, out seam);
                        ao = Mathf.Clamp(0.86f + fold / 0.0045f * 0.30f, 0.55f, 1f);
                        if (row.z > zCuff - 0.001f) ao = Mathf.Min(ao, 0.96f);
                    }
                    else if (row.kind == 1) ao = 0.85f;
                    else ao = Mathf.Lerp(0.55f, 0.25f, Mathf.Clamp01((zEnd - row.z) / 0.03f));

                    float rad = baseR + row.rOff + fold;
                    float ex = 1.0f, ey = 0.93f;
                    Vector3 p = new Vector3(Mathf.Cos(th) * rad * ex, Mathf.Sin(th) * rad * ey, row.z);
                    Vector3 pf = new Vector3(Mathf.Cos(th) * (rad + foldFlex) * ex, Mathf.Sin(th) * (rad + foldFlex) * ey, row.z);
                    int idx = r * seg + s;
                    pos[idx] = p;
                    posFlex[idx] = pf;
                    float cuffSeam = 0f;
                    if (row.kind == 0 && row.z > zCuff - 0.002f)
                    {
                        cuffSeam = Mathf.Max(Mathf.Exp(-Mathf.Pow((row.z - (zCuff + 0.0035f)) / 0.0008f, 2f)),
                                             Mathf.Exp(-Mathf.Pow((row.z - (zEnd - 0.0035f)) / 0.0008f, 2f)));
                    }
                    col[idx] = new Color(Mathf.Max(seam, cuffSeam), 0f, ao, 0f);
                    uv0[idx] = new Vector2(th * baseR, row.z);
                    wts[idx] = full ? SleeveWeights(Mathf.Min(row.z, zEnd), l1, l2) : new BoneWeight { boneIndex0 = SleeveBoneCuff, weight0 = 1f };
                }
            }

            var tris = new List<int>(rc * seg * 6);
            for (int r = 0; r < rc - 1; r++)
                for (int s = 0; s < seg; s++)
                {
                    int s1 = (s + 1) % seg;
                    int a = r * seg + s, b = r * seg + s1, c = (r + 1) * seg + s, d = (r + 1) * seg + s1;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }

            var m = new Mesh { name = full ? $"Ống tay áo blouse LOD{lod}" : $"Cổ tay áo blouse LOD{lod}" };
            m.vertices = pos;
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            // Thứ tự tam giác: kiểm tra pháp tuyến hàng ngoài hướng ra xa trục
            {
                var n = m.normals;
                float dot = 0f;
                for (int i = 0; i < seg * 4; i++) dot += Vector3.Dot(new Vector3(pos[i].x, pos[i].y, 0f), n[i]);
                if (dot < 0f)
                {
                    var t = m.triangles;
                    for (int i = 0; i < t.Length; i += 3) (t[i + 1], t[i + 2]) = (t[i + 2], t[i + 1]);
                    m.triangles = t;
                    m.RecalculateNormals();
                }
            }
            Vector3[] baseNormals = m.normals;
            m.colors = col;
            m.SetUVs(0, uv0);
            m.SetUVs(1, pos);
            m.boneWeights = wts;
            if (full)
            {
                // Blendshape gập khuỷu: tính pháp tuyến của hình gập rồi lấy độ chênh
                var tmp = new Mesh();
                tmp.vertices = posFlex;
                tmp.triangles = m.triangles;
                tmp.RecalculateNormals();
                var nf = tmp.normals;
                var dv = new Vector3[pos.Length];
                var dn = new Vector3[pos.Length];
                for (int i = 0; i < pos.Length; i++) { dv[i] = posFlex[i] - pos[i]; dn[i] = nf[i] - baseNormals[i]; }
                m.AddBlendShapeFrame("gập khuỷu", 100f, dv, dn, null);
                if (Application.isPlaying) UnityEngine.Object.Destroy(tmp); else UnityEngine.Object.DestroyImmediate(tmp);
            }
            m.RecalculateBounds();
            return m;
        }

        static float SleeveRadius(float z, float l1, float l2, bool full)
        {
            float zEnd = l1 + l2, zCuff = zEnd - CuffBandLen;
            float r;
            if (full && z < 0f) r = 0.061f;
            else if (full && z < l1) r = Mathf.Lerp(0.060f, 0.0505f, SS(0f, l1, z));
            else
            {
                float fz = full ? (z - l1) / Mathf.Max(0.01f, zCuff - l1) : 1f - (zCuff - z) / Mathf.Max(0.01f, zCuff - (zEnd - l2));
                r = Mathf.Lerp(0.0500f, 0.0418f, SS(0f, 1f, fz));
            }
            if (z > zCuff - 0.006f)
            {
                // dải cổ tay áo: dày hơn một chút, rãnh may ở chỗ nối
                float band = SS(zCuff - 0.006f, zCuff + 0.002f, z);
                r = Mathf.Lerp(r, 0.0412f, band) + 0.0010f * band;
                r -= 0.0009f * Mathf.Exp(-Mathf.Pow((z - zCuff) / 0.0012f, 2f));
            }
            return r;
        }

        static float SleeveFold(float th, float z, float l1, float l2, bool full, float zCuff, out float flexExtra, out float seam)
        {
            float d = 0f;
            flexExtra = 0f;
            float cuffMask = 1f - SS(zCuff - 0.012f, zCuff - 0.002f, z);
            // mặt trong khuỷu là +y (θ = 90°)
            float inner = 0.5f + 0.5f * Mathf.Cos(th - Mathf.PI * 0.5f);
            if (full)
            {
                float me = Mathf.Exp(-Mathf.Pow((z - l1) / 0.055f, 2f));
                float ring = Mathf.Sin(z * (Mathf.PI * 2f / 0.027f) + 1.3f * Mathf.Sin(th * 2f + 0.4f) + 0.8f * Mathf.Sin(th * 3.1f));
                d += me * (0.35f + 0.65f * inner) * 0.0042f * ring;
                flexExtra = me * (0.25f + 0.75f * inner) * (0.0050f * ring - 0.0016f * inner);
                // nếp rủ dọc bắp tay & cẳng tay
                d += 0.0022f * Mathf.Sin(th * 3f + z * 9f + 0.8f) * (1f - me) * cuffMask;
                d += 0.0013f * Mathf.Sin(th * 5f - z * 14f + 2.1f) * cuffMask;
            }
            else
            {
                d += 0.0016f * Mathf.Sin(th * 3f + z * 31f + 0.8f) * cuffMask;
            }
            // nếp dúm ngay trên cổ tay áo
            float mc = Mathf.Exp(-Mathf.Pow((z - (zCuff - 0.012f)) / 0.020f, 2f));
            d += mc * 0.0024f * Mathf.Sin(th * 7f + 0.5f + z * 40f) * cuffMask;
            // gợn vải ngẫu nhiên nhẹ
            d += 0.0007f * (Mathf.PerlinNoise(th * 1.7f + 3.1f, z * 26f) - 0.5f) * cuffMask;
            // đường may dọc mặt dưới (θ = -90°)
            float ang = Mathf.DeltaAngle(th * Mathf.Rad2Deg, -90f) * Mathf.Deg2Rad;
            seam = Mathf.Exp(-(ang * ang) / (0.035f * 0.035f)) * cuffMask;
            d -= 0.0006f * seam;
            return d;
        }

        static BoneWeight SleeveWeights(float z, float l1, float l2)
        {
            Span<float> w = stackalloc float[SleeveBoneCount];
            float wu = 1f - SS(l1 - 0.045f, l1 + 0.045f, z);
            w[SleeveBoneUpper] = wu;
            float rest = 1f - wu;
            float zf = Mathf.Max(z, l1);
            float k0 = l1, k1 = SleeveBoneZ(SleeveBoneTwist1, l1, l2), k2 = SleeveBoneZ(SleeveBoneTwist2, l1, l2), k3 = l1 + l2 - CuffBandLen;
            if (zf <= k1) { float t = (zf - k0) / (k1 - k0); w[SleeveBoneElbow] += rest * (1f - t); w[SleeveBoneTwist1] += rest * t; }
            else if (zf <= k2) { float t = (zf - k1) / (k2 - k1); w[SleeveBoneTwist1] += rest * (1f - t); w[SleeveBoneTwist2] += rest * t; }
            else if (zf <= k3) { float t = (zf - k2) / (k3 - k2); w[SleeveBoneTwist2] += rest * (1f - t); w[SleeveBoneCuff] += rest * t; }
            else w[SleeveBoneCuff] += rest;
            return Top4(w);
        }

        /// <summary>Bindpose cho 5 xương tay áo (xương đặt trên trục z, không xoay).</summary>
        public static Matrix4x4[] SleeveBindposes(float l1, float l2)
        {
            var bp = new Matrix4x4[SleeveBoneCount];
            for (int i = 0; i < SleeveBoneCount; i++)
                bp[i] = Matrix4x4.Translate(new Vector3(0f, 0f, SleeveBoneZ(i, l1, l2))).inverse;
            return bp;
        }

        // ------------------------------------------------------------------
        // Vật liệu
        // ------------------------------------------------------------------
        static Material template;
        static bool templateLoaded;

        /// <summary>Tạo vật liệu Khkt/TayNguoi (lấy từ Resources để shader chắc chắn có trong bản dựng).</summary>
        public static Material NewMaterial(int mode, Color baseColor)
        {
            if (!templateLoaded)
            {
                templateLoaded = true;
                template = Resources.Load<Material>("TayNguoi/TayNguoi");
            }
            Material m;
            if (template != null) m = new Material(template);
            else
            {
                var sh = Shader.Find("Khkt/TayNguoi") ?? Shader.Find("Khkt/DaNguoi") ?? Shader.Find("Universal Render Pipeline/Lit");
                m = new Material(sh);
            }
            m.hideFlags = HideFlags.DontSave;
            m.SetFloat("_Mode", mode);
            m.SetColor("_BaseColor", baseColor);
            switch (mode)
            {
                case 0: // da
                    m.SetFloat("_Wrap", 0.45f); m.SetFloat("_SssStrength", 0.42f); m.SetFloat("_Smoothness", 0.42f);
                    m.SetFloat("_SpecStrength", 0.30f); m.SetFloat("_DetailScale", 1100f); m.SetFloat("_DetailStrength", 0.55f);
                    m.SetFloat("_CreaseStrength", 1.0f); m.SetFloat("_AoStrength", 0.85f); m.SetFloat("_Inflate", 0f);
                    break;
                case 1: // găng nitrile
                    m.SetFloat("_Wrap", 0.22f); m.SetFloat("_SssStrength", 0f); m.SetFloat("_Smoothness", 0.58f);
                    m.SetFloat("_SpecStrength", 0.40f); m.SetFloat("_DetailScale", 1500f); m.SetFloat("_DetailStrength", 0.35f);
                    m.SetFloat("_CreaseStrength", 0.9f); m.SetFloat("_AoStrength", 0.9f); m.SetFloat("_Inflate", 0.0006f);
                    m.SetFloat("_Translucency", 0.35f);
                    break;
                default: // vải blouse
                    m.SetFloat("_Wrap", 0.38f); m.SetFloat("_SssStrength", 0f); m.SetFloat("_Smoothness", 0.12f);
                    m.SetFloat("_SpecStrength", 0.06f); m.SetFloat("_DetailStrength", 0.40f);
                    m.SetFloat("_CreaseStrength", 1f); m.SetFloat("_AoStrength", 0.95f); m.SetFloat("_Inflate", 0f);
                    m.SetColor("_SheenColor", new Color(0.92f, 0.95f, 1f)); m.SetFloat("_SheenStrength", 0.35f);
                    break;
            }
            return m;
        }
    }
}
