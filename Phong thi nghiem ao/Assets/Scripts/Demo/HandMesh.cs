using System.Collections.Generic;
using UnityEngine;

namespace Khkt.Demo
{
    /// <summary>
    /// Mesh gồm nhiều ống liền (ngón tay, lòng bàn tay, cẳng tay, tay áo) dựng lại mỗi khung từ các điểm điều khiển.
    /// Cấu trúc tam giác cố định từ đầu, mỗi khung chỉ ghi lại đỉnh vào mảng có sẵn: không cấp phát bộ nhớ.
    /// Mặt cắt là siêu elip (mũ 2 = elip, lớn hơn thì vuông dần), hai đầu khép bằng nửa vòm.
    /// </summary>
    public sealed class TubeMesh
    {
        class Tube
        {
            public int sub, sides, perSeg, points, capS, capE, rings;
            public int startPole = -1, ringBase, endPole = -1;
            public float axS, axE;
            public float[] cos, sin;
        }

        public readonly Mesh Mesh;
        readonly List<Tube> tubes = new List<Tube>();
        readonly List<int>[] tris;
        Vector3[] v;
        int count;

        public TubeMesh(int submeshes, string name)
        {
            Mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            Mesh.MarkDynamic();
            tris = new List<int>[submeshes];
            for (int i = 0; i < submeshes; i++) tris[i] = new List<int>();
        }

        /// <summary>
        /// Thêm một ống đi qua <paramref name="points"/> điểm điều khiển, mỗi đoạn chia <paramref name="perSeg"/> vòng.
        /// capStart/capEnd: số vòng của nửa vòm khép đầu (0 = để hở); axial: độ sâu vòm theo bán kính nhỏ.
        /// </summary>
        public int AddTube(int sub, int sides, int points, int perSeg, int capStart, float axialStart, int capEnd, float axialEnd)
        {
            var t = new Tube { sub = sub, sides = sides, perSeg = perSeg, points = points, capS = capStart, capE = capEnd, axS = axialStart, axE = axialEnd };
            t.rings = capStart + (points - 1) * perSeg + 1 + capEnd;
            t.cos = new float[sides];
            t.sin = new float[sides];
            for (int k = 0; k < sides; k++)
            {
                float a = 2f * Mathf.PI * k / sides;
                t.cos[k] = Mathf.Cos(a);
                t.sin[k] = Mathf.Sin(a);
            }
            if (capStart > 0) t.startPole = count++;
            t.ringBase = count;
            count += t.rings * sides;
            if (capEnd > 0) t.endPole = count++;
            var tr = tris[sub];
            int S = sides;
            for (int r = 0; r < t.rings - 1; r++)
                for (int k = 0; k < S; k++)
                {
                    int a = t.ringBase + r * S + k, b = t.ringBase + r * S + (k + 1) % S;
                    int c = a + S, d = b + S;
                    tr.Add(a); tr.Add(b); tr.Add(c);
                    tr.Add(b); tr.Add(d); tr.Add(c);
                }
            if (t.startPole >= 0)
                for (int k = 0; k < S; k++) { tr.Add(t.startPole); tr.Add(t.ringBase + (k + 1) % S); tr.Add(t.ringBase + k); }
            if (t.endPole >= 0)
            {
                int last = t.ringBase + (t.rings - 1) * S;
                for (int k = 0; k < S; k++) { tr.Add(t.endPole); tr.Add(last + k); tr.Add(last + (k + 1) % S); }
            }
            tubes.Add(t);
            return tubes.Count - 1;
        }

        /// <summary>Gọi một lần sau khi thêm đủ ống.</summary>
        public void Finish()
        {
            v = new Vector3[count];
            Mesh.SetVertices(v);
            Mesh.subMeshCount = tris.Length;
            for (int i = 0; i < tris.Length; i++) Mesh.SetTriangles(tris[i], i, false);
        }

        public void Apply()
        {
            Mesh.SetVertices(v);
            Mesh.RecalculateNormals();
            Mesh.RecalculateBounds();
        }

        static readonly Vector3[] sc = new Vector3[256], st = new Vector3[256], sh = new Vector3[256];
        static readonly Vector4[] sr = new Vector4[256];

        /// <summary>
        /// Ghi lại ống <paramref name="id"/>: P điểm điều khiển, R = (bán trục ngang, bán trục dày, mũ siêu elip, 0),
        /// H hướng trục ngang mong muốn ở từng điểm (null: ống tròn, trục ngang trượt song song từ <paramref name="refAxis"/>).
        /// </summary>
        public void Sweep(int id, Vector3[] P, Vector4[] R, Vector3[] H, Vector3 refAxis)
        {
            var t = tubes[id];
            int n = t.points, per = t.perSeg, S = (n - 1) * per + 1;
            for (int s = 0; s < n - 1; s++)
            {
                Vector3 p0 = s > 0 ? P[s - 1] : 2f * P[0] - P[1];
                Vector3 p3 = s + 2 < n ? P[s + 2] : 2f * P[n - 1] - P[n - 2];
                int kmax = s == n - 2 ? per : per - 1;
                for (int k = 0; k <= kmax; k++)
                {
                    float u = k / (float)per, w = u * u * (3f - 2f * u);
                    int i = s * per + k;
                    sc[i] = CatmullRom(p0, P[s], P[s + 1], p3, u);
                    sr[i] = Vector4.Lerp(R[s], R[s + 1], w);
                    if (H != null) sh[i] = Vector3.Lerp(H[s], H[s + 1], w);
                }
            }
            for (int i = 0; i < S; i++)
            {
                Vector3 d = sc[Mathf.Min(i + 1, S - 1)] - sc[Mathf.Max(i - 1, 0)];
                st[i] = d.sqrMagnitude > 1e-12f ? d.normalized : i > 0 ? st[i - 1] : Vector3.forward;
            }
            Vector3 a = refAxis, b;
            Vector3 a0 = default, b0 = default, aN = default, bN = default;
            for (int i = 0; i < S; i++)
            {
                Vector3 T = st[i];
                a = Vector3.ProjectOnPlane(H != null ? sh[i] : a, T);
                if (a.sqrMagnitude < 1e-10f) a = Vector3.ProjectOnPlane(Mathf.Abs(T.y) < 0.9f ? Vector3.up : Vector3.right, T);
                a.Normalize();
                b = Vector3.Cross(T, a);
                Ring(t, t.capS + i, sc[i], a, b, sr[i].x, sr[i].y, sr[i].z);
                if (i == 0) { a0 = a; b0 = b; }
                if (i == S - 1) { aN = a; bN = b; }
            }
            if (t.capS > 0)
            {
                Vector3 T = st[0], c = sc[0];
                float ax = Mathf.Min(sr[0].x, sr[0].y) * t.axS;
                for (int k = 0; k < t.capS; k++)
                {
                    float phi = 0.5f * Mathf.PI * (t.capS - k) / (t.capS + 1);
                    float cs = Mathf.Cos(phi);
                    Ring(t, k, c - T * (ax * Mathf.Sin(phi)), a0, b0, sr[0].x * cs, sr[0].y * cs, sr[0].z);
                }
                v[t.startPole] = c - T * ax;
            }
            if (t.capE > 0)
            {
                Vector3 T = st[S - 1], c = sc[S - 1];
                float ax = Mathf.Min(sr[S - 1].x, sr[S - 1].y) * t.axE;
                for (int k = 0; k < t.capE; k++)
                {
                    float phi = 0.5f * Mathf.PI * (k + 1) / (t.capE + 1);
                    float cs = Mathf.Cos(phi);
                    Ring(t, t.capS + S + k, c + T * (ax * Mathf.Sin(phi)), aN, bN, sr[S - 1].x * cs, sr[S - 1].y * cs, sr[S - 1].z);
                }
                v[t.endPole] = c + T * ax;
            }
        }

        void Ring(Tube t, int ring, Vector3 c, Vector3 a, Vector3 b, float ra, float rb, float power)
        {
            int baseI = t.ringBase + ring * t.sides;
            bool ellipse = power <= 2.001f;
            float e = 2f / Mathf.Max(power, 0.5f);
            for (int k = 0; k < t.sides; k++)
            {
                float x = t.cos[k], y = t.sin[k];
                if (!ellipse)
                {
                    x = Mathf.Sign(x) * Mathf.Pow(Mathf.Abs(x), e);
                    y = Mathf.Sign(y) * Mathf.Pow(Mathf.Abs(y), e);
                }
                v[baseI + k] = c + a * (ra * x) + b * (rb * y);
            }
        }

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float u)
        {
            float u2 = u * u, u3 = u2 * u;
            return 0.5f * (2f * p1 + (p2 - p0) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2 + (3f * p1 - p0 - 3f * p2 + p3) * u3);
        }
    }

    /// <summary>
    /// Kích thước và dáng bàn tay theo thiết kế (21 điểm như MediaPipe: 0 cổ tay, 1–4 ngón cái, 5–8 trỏ, 9–12 giữa,
    /// 13–16 áp út, 17–20 út). Hệ trục cục bộ: +z hướng các ngón, +y mu bàn tay, ngón cái ở phía −x·side (side = +1 tay phải).
    /// </summary>
    public static class HandAnatomy
    {
        public static readonly int[] Parent = { -1, 0, 1, 2, 3, 0, 5, 6, 7, 0, 9, 10, 11, 0, 13, 14, 15, 0, 17, 18, 19 };

        // khớp gốc ngón (MCP) so với cổ tay, ngón cái là khớp CMC
        static readonly Vector3[] Base =
        {
            new Vector3(-0.024f, -0.011f, 0.027f),   // ngón cái (CMC)
            new Vector3(-0.024f, 0.000f, 0.087f),    // trỏ
            new Vector3(-0.005f, 0.003f, 0.091f),    // giữa
            new Vector3(0.013f, 0.002f, 0.086f),     // áp út
            new Vector3(0.029f, -0.002f, 0.077f),    // út
        };
        static readonly float[,] Len =
        {
            { 0.040f, 0.032f, 0.027f },   // cái: đốt bàn, đốt 1, đốt 2
            { 0.042f, 0.025f, 0.021f },
            { 0.046f, 0.028f, 0.022f },
            { 0.043f, 0.027f, 0.022f },
            { 0.034f, 0.020f, 0.019f },
        };
        static readonly float[] Yaw = { 0, -5f, 0f, 5f, 11f }, YawSpread = { 0, -9f, -2f, 6f, 13f };
        static readonly float[] CurlK = { 1f, 0.92f, 1f, 1.07f, 1.15f };

        /// <summary>Chiều dài từng xương (theo khớp con), dùng để dựng lại bàn tay từ hướng đo được.</summary>
        public static readonly float[] BoneLen = new float[21];

        static HandAnatomy()
        {
            var j = new Vector3[21];
            Pose(j, 0f, 0f, 0f, 1f);
            for (int i = 1; i < 21; i++) BoneLen[i] = (j[i] - j[Parent[i]]).magnitude;
        }

        /// <summary>
        /// Dựng 21 điểm trong hệ trục cục bộ. curl 0 duỗi thẳng, 1 ôm quanh vật cỡ cốc; fist thêm độ gập tới nắm chặt;
        /// spread xoè ngón; thumb 0 ngón cái dang ra, 1 khép đối diện các ngón.
        /// </summary>
        public static void Pose(Vector3[] j, float curl, float spread, float thumb, float side, float fist = 0f)
        {
            j[0] = Vector3.zero;
            // ngón cái: đốt bàn chếch ra ngoài, xuống dưới; hai đốt sau gập về phía lòng bàn tay
            Vector3 b0 = Base[0]; b0.x *= side;
            j[1] = b0;
            Vector3 open = new Vector3(-0.62f * side, -0.28f, 0.73f), closed = new Vector3(-0.08f * side, -0.66f, 0.74f);
            Vector3 d = Vector3.Lerp(open, closed, thumb).normalized;
            Vector3 inward = new Vector3(0.75f * side, -0.55f, 0.1f).normalized;
            for (int k = 0; k < 3; k++)
            {
                float bend = k == 0 ? 0f : k == 1 ? 8f + 30f * thumb + 20f * fist : 12f + 40f * thumb + 25f * fist;
                d = Vector3.RotateTowards(d, inward, bend * Mathf.Deg2Rad, 0f);
                j[2 + k] = j[1 + k] + d * Len[0, k];
            }
            for (int f = 1; f < 5; f++)
            {
                Vector3 m = Base[f]; m.x *= side;
                int r = 1 + f * 4;
                j[r] = m;
                float c = Mathf.Clamp01(curl * CurlK[f]), cf = Mathf.Clamp01(fist * CurlK[f]);
                Vector3 d0 = Quaternion.Euler(0, (Yaw[f] + YawSpread[f] * spread) * side, 0) * Vector3.forward;
                Vector3 axis = Vector3.Cross(Vector3.up, d0).normalized;   // quay dương = gập xuống phía lòng bàn tay
                float a1 = 4f + 52f * c + 30f * cf, a2 = a1 + 6f + 78f * c + 22f * cf, a3 = a2 + 4f + 46f * c + 18f * cf;
                j[r + 1] = j[r] + Quaternion.AngleAxis(a1, axis) * d0 * Len[f, 0];
                j[r + 2] = j[r + 1] + Quaternion.AngleAxis(a2, axis) * d0 * Len[f, 1];
                j[r + 3] = j[r + 2] + Quaternion.AngleAxis(a3, axis) * d0 * Len[f, 2];
            }
        }
    }

    /// <summary>
    /// Bề mặt găng tay nitrile từ 21 điểm: lòng bàn tay là ống dẹt từ cổ găng tới hàng khớp gốc ngón, mỗi ngón một ống thuôn,
    /// ngón cái có gò cơ ở gốc. Hình đối xứng qua mặt phẳng bàn tay nên không cần biết tay trái hay phải.
    /// </summary>
    public sealed class HandSkin
    {
        readonly TubeMesh m;
        readonly int palm, thumb;
        readonly int[] finger = new int[4];
        readonly Vector3[] P = new Vector3[5], H = new Vector3[5];
        readonly Vector4[] R = new Vector4[5];

        static readonly float[,] FingerR =
        {
            { 0.0105f, 0.0098f, 0.0091f, 0.0082f, 0.0075f },
            { 0.0108f, 0.0101f, 0.0094f, 0.0085f, 0.0077f },
            { 0.0104f, 0.0097f, 0.0090f, 0.0081f, 0.0073f },
            { 0.0093f, 0.0086f, 0.0079f, 0.0071f, 0.0064f },
        };

        public HandSkin(TubeMesh mesh, int sub)
        {
            m = mesh;
            // miệng cổ găng khép bằng mặt gần phẳng: cẳng tay xuyên qua thành một đường viền gọn
            palm = m.AddTube(sub, 20, 4, 5, 1, 0.05f, 3, 0.8f);
            thumb = m.AddTube(sub, 12, 5, 4, 2, 0.8f, 3, 1f);
            for (int f = 0; f < 4; f++) finger[f] = m.AddTube(sub, 12, 5, 4, 2, 0.8f, 3, 1f);
        }

        /// <summary>j: 21 điểm (cùng hệ toạ độ với mesh); forearm: hướng cẳng tay (khuỷu → cổ tay).</summary>
        public void Write(Vector3[] j, Vector3 forearm)
        {
            Vector3 w = j[0];
            Vector3 knuckle = (j[5] + j[9] + j[13] + j[17]) * 0.25f;
            Vector3 fwd = knuckle - w;
            if (fwd.sqrMagnitude < 1e-8f) fwd = forearm;
            fwd.Normalize();
            if (forearm.sqrMagnitude < 1e-8f) forearm = fwd;
            forearm.Normalize();
            Vector3 across = j[5] - j[17];
            if (across.sqrMagnitude < 1e-8f) across = Vector3.Cross(fwd, Vector3.up);
            across.Normalize();
            Vector3 normal = Vector3.Cross(fwd, across).normalized;
            float halfW = (j[5] - j[17]).magnitude * 0.5f + 0.0085f;

            // lòng bàn tay: cổ găng (loe nhẹ) → cổ tay → giữa bàn → hàng khớp gốc ngón
            P[0] = w - forearm * 0.055f;
            P[1] = w;
            P[2] = w + (knuckle - w) * 0.5f;
            P[3] = w + (knuckle - w) * 0.9f;
            R[0] = new Vector4(0.0335f, 0.0275f, 2f, 0);
            R[1] = new Vector4(0.0290f, 0.0205f, 2.2f, 0);
            R[2] = new Vector4(Mathf.Max(0.036f, halfW + 0.002f), 0.0165f, 2.7f, 0);
            R[3] = new Vector4(halfW, 0.0128f, 2.7f, 0);
            Vector3 acrossArm = Vector3.ProjectOnPlane(across, forearm);
            H[0] = acrossArm; H[1] = Vector3.Lerp(acrossArm, across, 0.5f); H[2] = across; H[3] = across;
            m.Sweep(palm, P, R, H, normal);

            // ngón cái: gốc nằm sâu trong lòng bàn tay, to ở gò cơ, thuôn dần ra đầu ngón
            P[0] = Vector3.Lerp(w, j[1], 0.3f) + fwd * 0.012f;
            P[1] = j[1];
            P[2] = j[2];
            P[3] = j[3];
            P[4] = Tip(j[3], j[4], 0.0088f);
            R[0] = new Vector4(0.0150f, 0.0135f, 2f, 0);
            R[1] = new Vector4(0.0148f, 0.0128f, 2f, 0);
            R[2] = new Vector4(0.0118f, 0.0112f, 2f, 0);
            R[3] = new Vector4(0.0102f, 0.0098f, 2f, 0);
            R[4] = new Vector4(0.0090f, 0.0086f, 2f, 0);
            m.Sweep(thumb, P, R, null, normal);

            for (int f = 0; f < 4; f++)
            {
                int r = 5 + f * 4;
                P[0] = w + (j[r] - w) * 0.55f;
                P[1] = j[r];
                P[2] = j[r + 1];
                P[3] = j[r + 2];
                P[4] = Tip(j[r + 2], j[r + 3], FingerR[f, 4]);
                for (int k = 0; k < 5; k++) { float rr = FingerR[f, k] * (k == 2 ? 1.03f : 1f); R[k] = new Vector4(rr, rr * 0.96f, 2f, 0); }
                m.Sweep(finger[f], P, R, null, normal);
            }
        }

        /// <summary>Đầu ống lùi lại một bán kính để vòm đầu ngón chạm đúng điểm đầu ngón.</summary>
        static Vector3 Tip(Vector3 prev, Vector3 tip, float r)
        {
            Vector3 d = tip - prev;
            float l = d.magnitude;
            return l > 2f * r ? tip - d * (r / l) : Vector3.Lerp(prev, tip, 0.5f);
        }
    }
}
