// Hình học dựng bằng code (procedural) – BẢN CHUYỂN C# của frontend/hoa-hoc/phong-thi-nghiem/hinh-hoc.js (cùng thuật toán, cùng số liệu
// dụng cụ). Sửa hình ở một nơi thì sửa nơi kia. Không tải model ngoài → bản build nhỏ.
//
// Lỗi cũ đã bỏ: profile chỉ vài điểm nối bằng đoạn thẳng (vai bình gãy khúc) và RecalculateNormals trên đỉnh nhân đôi ở đường may
// (một vạch sáng/tối chạy dọc thân bình, tam giác răng cưa ở đáy). Nay: (1) profile BO GÓC bằng cung Bézier bậc 2 liền tiếp tuyến;
// (2) pháp tuyến tính theo công thức từ tiếp tuyến profile, đường may cùng vị trí + cùng pháp tuyến; (3) thành thuỷ tinh có độ dày
// thật + gờ miệng bo tròn.
//
// Quy ước profile điều khiển: Vector3(x, y, rho), đơn vị mét, đi từ TÂM ĐÁY (x = 0) ra ngoài rồi lên miệng.
//   rho < 0 : điểm trơn (pháp tuyến lấy trung bình hai đoạn kề)   rho > 0 : góc được bo bằng cung bán kính ≈ rho
//   rho = 0 : góc NHỌN thật (đỉnh được tách đôi để pháp tuyến không bị nhoè)
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PhongThiNghiem
{
    public static class LabHinh
    {
        /// <summary>Bề dày thành thuỷ tinh (m) – cũng là khoảng cách lòng bình ↔ mặt chất lỏng.</summary>
        public const float DayKinh = 0.0022f;
        const float Eps = 1e-9f;
        public const float Tron = -1f;

        struct Diem { public float x, y; public bool sac; }
        struct Dinh { public float x, y, nx, ny; public bool noi; }

        /// <summary>Vector3(x, y, rho) cho điểm trơn.</summary>
        public static Vector3 T(float x, float y) => new Vector3(x, y, Tron);
        public static Vector3 B(float x, float y, float rho) => new Vector3(x, y, rho);

        // ------------------------------------------------------------------ profile → polyline dày
        static List<Diem> MuotDiem(IList<Vector3> ctrl, int soDoanCung)
        {
            var kq = new List<Diem>();
            void Them(float x, float y, bool sac)
            {
                if (kq.Count > 0)
                {
                    var c = kq[kq.Count - 1];
                    if (Mathf.Sqrt((c.x - x) * (c.x - x) + (c.y - y) * (c.y - y)) < Eps)
                    {
                        if (sac) { c.sac = true; kq[kq.Count - 1] = c; }
                        return;
                    }
                }
                kq.Add(new Diem { x = x, y = y, sac = sac });
            }

            int n = ctrl.Count;
            Them(ctrl[0].x, ctrl[0].y, false);
            for (int i = 1; i < n - 1; i++)
            {
                float px = ctrl[i].x, py = ctrl[i].y, rho = ctrl[i].z;
                if (rho < 0f) { Them(px, py, false); continue; }
                if (rho == 0f) { Them(px, py, true); continue; }
                float ax = ctrl[i - 1].x - px, ay = ctrl[i - 1].y - py, bx = ctrl[i + 1].x - px, by = ctrl[i + 1].y - py;
                float la = Mathf.Sqrt(ax * ax + ay * ay), lb = Mathf.Sqrt(bx * bx + by * by);
                if (la < Eps || lb < Eps || Mathf.Abs(ax * by - ay * bx) < 1e-12f * la * lb) { Them(px, py, false); continue; }
                float t = Mathf.Min(rho, Mathf.Min(la / 2f, lb / 2f));
                float x0 = px + ax / la * t, y0 = py + ay / la * t, x2 = px + bx / lb * t, y2 = py + by / lb * t;
                for (int k = 0; k <= soDoanCung; k++)
                {
                    float s = (float)k / soDoanCung, u = 1f - s;
                    Them(u * u * x0 + 2f * u * s * px + s * s * x2, u * u * y0 + 2f * u * s * py + s * s * y2, false);
                }
            }
            Them(ctrl[n - 1].x, ctrl[n - 1].y, false);
            return kq;
        }

        /// <summary>Profile điều khiển → chuỗi điểm dày (Vector2) – dùng tính lòng bình / mực chất lỏng.</summary>
        public static Vector2[] MuotProfile(IList<Vector3> ctrl, int soDoanCung = 8)
        {
            var d = MuotDiem(ctrl, soDoanCung);
            var kq = new Vector2[d.Count];
            for (int i = 0; i < kq.Length; i++) kq[i] = new Vector2(d[i].x, d[i].y);
            return kq;
        }

        static Vector2 PhapDoan(Diem a, Diem b)
        {
            float dx = b.x - a.x, dy = b.y - a.y, l = Mathf.Sqrt(dx * dx + dy * dy);
            return l < Eps ? Vector2.zero : new Vector2(dy / l, -dx / l);
        }

        static List<Dinh> DinhLatCat(List<Diem> pts)
        {
            int m = pts.Count;
            var dinh = new List<Dinh>(m + 8);
            for (int i = 0; i < m; i++)
            {
                bool coTr = i > 0, coSau = i < m - 1;
                Vector2 tr = coTr ? PhapDoan(pts[i - 1], pts[i]) : Vector2.zero, sau = coSau ? PhapDoan(pts[i], pts[i + 1]) : Vector2.zero;
                var p = pts[i];
                if (!coTr || !coSau)
                {
                    var nn = coTr ? tr : sau;
                    dinh.Add(new Dinh { x = p.x, y = p.y, nx = nn.x, ny = nn.y, noi = true });
                    continue;
                }
                if (p.sac)
                {
                    dinh.Add(new Dinh { x = p.x, y = p.y, nx = tr.x, ny = tr.y, noi = false });
                    dinh.Add(new Dinh { x = p.x, y = p.y, nx = sau.x, ny = sau.y, noi = true });
                    continue;
                }
                float nx = tr.x + sau.x, ny = tr.y + sau.y, l = Mathf.Sqrt(nx * nx + ny * ny);
                if (l < Eps) { nx = sau.x; ny = sau.y; } else { nx /= l; ny /= l; }
                dinh.Add(new Dinh { x = p.x, y = p.y, nx = nx, ny = ny, noi = true });
            }
            return dinh;
        }

        // ------------------------------------------------------------------ mặt tròn xoay
        /// <summary>Mặt tròn xoay quanh trục y từ profile điều khiển; đường may CÙNG vị trí và pháp tuyến (không còn vạch dọc thân).</summary>
        public static Mesh LatheMem(IList<Vector3> ctrl, int phan = 64, int soDoanCung = 8, bool haiLuot = false)
        {
            var dinh = DinhLatCat(MuotDiem(ctrl, soDoanCung));
            int K = dinh.Count, hang = phan + 1, nV = K * hang;
            var v = new Vector3[nV];
            var nn = new Vector3[nV];
            var uv = new Vector2[nV];
            var dai = new float[K];
            float tong = 0f;
            for (int k = 1; k < K; k++)
            {
                tong += Mathf.Sqrt((dinh[k].x - dinh[k - 1].x) * (dinh[k].x - dinh[k - 1].x) + (dinh[k].y - dinh[k - 1].y) * (dinh[k].y - dinh[k - 1].y));
                dai[k] = tong;
            }
            for (int k = 0; k < K; k++)
            {
                var d = dinh[k];
                for (int j = 0; j <= phan; j++)
                {
                    float a = j * Mathf.PI * 2f / phan, s = Mathf.Sin(a), c = Mathf.Cos(a);
                    int o = k * hang + j;
                    v[o] = new Vector3(d.x * s, d.y, d.x * c);
                    nn[o] = new Vector3(d.nx * s, d.ny, d.nx * c);
                    uv[o] = new Vector2((float)j / phan, tong > 0f ? dai[k] / tong : 0f);
                }
            }
            var t = new List<int>(K * phan * 6);
            for (int k = 0; k < K - 1; k++)
            {
                if (!dinh[k].noi) continue;
                for (int j = 0; j < phan; j++)
                {
                    int a = k * hang + j, b = a + hang;
                    t.Add(a); t.Add(a + 1); t.Add(b);
                    t.Add(a + 1); t.Add(b + 1); t.Add(b);
                }
            }
            var m = new Mesh { name = "lathe" };
            if (nV > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(v);
            m.SetNormals(nn);
            m.SetUVs(0, uv);
            DatTamGiac(m, t, haiLuot);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>haiLuot: hai submesh dùng chung tam giác để vẽ trong suốt hai lượt (xem LabVatLieu.HaiLuot); LuoiGO tự gán đủ 2 vật liệu.</summary>
        static void DatTamGiac(Mesh m, List<int> t, bool haiLuot)
        {
            if (!haiLuot) { m.SetTriangles(t, 0); return; }
            m.subMeshCount = 2;
            m.SetTriangles(t, 0);
            m.SetTriangles(t, 1);
        }

        static Vector3 DichVao(IList<Vector3> ctrl, int i, float day)
        {
            int n = ctrl.Count;
            var P = new Diem { x = ctrl[i].x, y = ctrl[i].y };
            var A = new Diem { x = ctrl[Mathf.Max(0, i - 1)].x, y = ctrl[Mathf.Max(0, i - 1)].y };
            var Bd = new Diem { x = ctrl[Mathf.Min(n - 1, i + 1)].x, y = ctrl[Mathf.Min(n - 1, i + 1)].y };
            Vector2 tr = i > 0 ? PhapDoan(A, P) : PhapDoan(P, Bd), sau = i < n - 1 ? PhapDoan(P, Bd) : tr;
            float mx = tr.x + sau.x, my = tr.y + sau.y, l = Mathf.Sqrt(mx * mx + my * my);
            if (l < Eps) { mx = sau.x; my = sau.y; } else { mx /= l; my /= l; }
            float cos = Mathf.Max(0.33f, mx * sau.x + my * sau.y);
            return new Vector3(ctrl[i].x - mx * day / cos, ctrl[i].y - my * day / cos, 0f);
        }

        /// <summary>Profile NGOÀI (từ tâm đáy tới miệng) → vòng kín của THÀNH thuỷ tinh: vách ngoài ↑ miệng (gờ bo) ↓ vách trong → đáy trong.
        /// dayDay &lt; 0: tự chọn (đáy phẳng dày 1,7 lần thành, đáy tròn dày bằng thành).</summary>
        public static List<Vector3> ThanhThuyTinh(IList<Vector3> ngoai, float day = DayKinh, float dayDay = -1f)
        {
            int n = ngoai.Count;
            float rl = day * 0.46f;
            bool phang = n > 1 && Mathf.Abs(ngoai[1].y - ngoai[0].y) < Eps;
            if (dayDay < 0f) dayDay = phang ? day * 1.7f : day;
            var trong = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                var p = DichVao(ngoai, i, day);
                float rho = ngoai[i].z < 0f ? Tron : ngoai[i].z == 0f ? 0f : Mathf.Max(ngoai[i].z - day, 0.0006f);
                trong[i] = new Vector3(Mathf.Max(0f, p.x), p.y, rho);
            }
            trong[0] = new Vector3(0f, dayDay, Tron);
            if (phang) trong[1].y = dayDay;
            var mieng = ngoai[n - 1];
            float xt = trong[n - 1].x;
            var vong = new List<Vector3>(2 * n + 2);
            for (int i = 0; i < n - 1; i++) vong.Add(ngoai[i]);
            vong.Add(new Vector3(mieng.x, mieng.y, rl));
            vong.Add(new Vector3(xt, mieng.y, rl));
            for (int i = n - 2; i >= 1; i--) vong.Add(trong[i]);
            vong.Add(new Vector3(0f, dayDay, Tron));
            return vong;
        }

        // ------------------------------------------------------------------ tương thích tên cũ
        static List<Vector3> CtrlTuDiem(IList<Vector2> p)
        {
            var c = new List<Vector3>(p.Count);
            foreach (var q in p) c.Add(T(q.x, q.y));
            return c;
        }

        /// <summary>Thành thuỷ tinh dày của dụng cụ có profile ngoài dày `ngoai` (kết quả Coc / BinhTamGiac / OngNghiem… hoặc MuotProfile).</summary>
        public static Mesh Lathe(IList<Vector2> ngoai, int phan = 64) => LatheMem(ThanhThuyTinh(CtrlTuDiem(ngoai)), phan, 8, true);

        /// <summary>Vật rắn tròn xoay trơn (không thành dày) – lọ nắp, thân đèn cồn, cuống phễu…</summary>
        public static Mesh LatheDac(IList<Vector3> ctrl, int phan = 48) => LatheMem(ctrl, phan);

        // ------------------------------------------------------------------ chất lỏng
        public static float BanKinhTai(Vector2[] p, float y, float day = DayKinh)
        {
            for (int i = 1; i < p.Length; i++)
            {
                Vector2 a = p[i - 1], b = p[i];
                float dy = b.y - a.y;
                if (Mathf.Abs(dy) < Eps || y < Mathf.Min(a.y, b.y) || y > Mathf.Max(a.y, b.y)) continue;
                float t = (y - a.y) / dy, dxdy = (b.x - a.x) / dy;
                return Mathf.Max(0.0005f, a.x + (b.x - a.x) * t - day * Mathf.Sqrt(1f + dxdy * dxdy));
            }
            return Mathf.Max(0.0005f, p[p.Length - 1].x - day);
        }

        /// <summary>Khối chất lỏng cao h trong bình (profile ngoài dày p), mặt khum lõm; vừa khít lòng bình.</summary>
        public static Mesh ChatLong(Vector2[] p, float h, float day = DayKinh)
        {
            h = Mathf.Max(0.006f, h);
            const float y0 = 0.003f;
            var c = new List<Vector3> { T(0f, y0) };
            const int N = 14;
            for (int i = 0; i <= N; i++)
            {
                float y = y0 + (h - y0) * i / N;
                c.Add(T(BanKinhTai(p, y, day), y));
            }
            float rw = BanKinhTai(p, h, day), m = Mathf.Min(0.004f, rw * 0.18f);
            c[c.Count - 1] = T(rw, h + m);
            for (int i = 1; i <= 8; i++)
            {
                float x = rw * (1f - i / 8f);
                c.Add(T(Mathf.Max(0f, x), h + m * Mathf.Pow(x / rw, 4f)));
            }
            c[1] = B(c[1].x, c[1].y, 0.003f); // góc đáy lòng bình bo nhẹ (khớp đáy kính bo)
            return LatheMem(c, 48, 8, true);
        }

        public static float TheTichDenH(Vector2[] p, float h, float day = DayKinh)
        {
            float v = 0f;
            const float buoc = 0.0005f;
            for (float y = 0.003f; y < h; y += buoc)
            {
                float dy = Mathf.Min(buoc, h - y), r = BanKinhTai(p, y + dy / 2f, day);
                v += Mathf.PI * r * r * dy;
            }
            return v * 1e6f; // mL
        }

        public static float HDeCoTheTich(Vector2[] p, float mL, float day, float hMax)
        {
            float a = 0.003f, b = hMax;
            for (int i = 0; i < 30; i++)
            {
                float g = (a + b) / 2f;
                if (TheTichDenH(p, g, day) < mL) a = g; else b = g;
            }
            return (a + b) / 2f;
        }

        // ------------------------------------------------------------------ số liệu dụng cụ (m) – profile NGOÀI, từ tâm đáy tới miệng
        public static class DungCu
        {
            /// <summary>Cung tròn tâm (cx, cy), bán kính r, từ góc a0 → a1 (độ) thành các điểm trơn.</summary>
            public static List<Vector3> CungTron(float cx, float cy, float r, float a0, float a1, int soDiem = 12)
            {
                var p = new List<Vector3>();
                for (int i = 0; i <= soDiem; i++)
                {
                    float a = (a0 + (a1 - a0) * i / soDiem) * Mathf.Deg2Rad;
                    p.Add(T(cx + r * Mathf.Cos(a), cy + r * Mathf.Sin(a)));
                }
                return p;
            }

            /// <summary>Cốc: thân trụ, đáy bo, miệng loe nhẹ.</summary>
            public static List<Vector3> Coc(float r, float h) =>
                new List<Vector3> { T(0f, 0f), B(r, 0f, 0.006f), B(r, h - 0.012f, 0.01f), T(r * 1.05f, h) };

            /// <summary>Bình tam giác (Erlenmeyer): đáy bo, thân CÔN thẳng, vai bo lớn, cổ trụ, miệng loe.</summary>
            public static List<Vector3> BinhTamGiac(float r, float h, float rc, float hc) =>
                new List<Vector3> { T(0f, 0f), B(r, 0f, 0.011f), B(rc, hc, 0.028f), B(rc, h - 0.012f, 0.007f), T(rc * 1.18f, h) };

            /// <summary>Ống nghiệm: đáy bán cầu, miệng loe có gờ.</summary>
            public static List<Vector3> OngNghiem(float r, float h)
            {
                var p = new List<Vector3> { T(0f, 0f) };
                var cung = CungTron(0f, r, r, -90f, 0f, 14);
                for (int i = 1; i < cung.Count; i++) p.Add(cung[i]);
                p.Add(B(r, h - 0.01f, 0.005f));
                p.Add(T(r * 1.14f, h));
                return p;
            }

            /// <summary>Lọ thuốc thử cổ hẹp (vai tròn).</summary>
            public static List<Vector3> LoThuoc(float r, float h, float rc) =>
                new List<Vector3> { T(0f, 0f), B(r, 0f, 0.006f), B(r, h * 0.58f, 0.022f), B(rc, h * 0.8f, 0.012f), B(rc, h - 0.008f, 0.003f), T(rc * 1.12f, h) };

            /// <summary>Đèn cồn: thân phình tròn, cổ thon, miệng.</summary>
            public static List<Vector3> DenCon() =>
                new List<Vector3> { T(0f, 0f), B(0.042f, 0f, 0.009f), B(0.046f, 0.03f, 0.02f), B(0.044f, 0.05f, 0.022f), B(0.016f, 0.07f, 0.016f), B(0.012f, 0.086f, 0.004f), T(0.0125f, 0.098f) };

            /// <summary>Phễu: nón trên + cuống.</summary>
            public static List<Vector3> Pheu() =>
                new List<Vector3> { T(0f, 0f), T(0.004f, 0f), B(0.004f, 0.05f, 0.003f), B(0.035f, 0.09f, 0.006f), T(0.04f, 0.12f) };
        }

        // profile ngoài dày (Vector2[]) – tên cũ, dùng cho ChatLong / BanKinhTai / HDeCoTheTich và Lathe (thành dày)
        public static Vector2[] Coc(float r, float h) => MuotProfile(DungCu.Coc(r, h));
        public static Vector2[] BinhTamGiac(float r, float h, float rc, float hc) => MuotProfile(DungCu.BinhTamGiac(r, h, rc, hc));
        public static Vector2[] OngNghiem(float r, float h) => MuotProfile(DungCu.OngNghiem(r, h));
        public static Vector2[] LoThuoc(float r, float h, float rc) => MuotProfile(DungCu.LoThuoc(r, h, rc));
        public static Vector2[] DenCon() => MuotProfile(DungCu.DenCon());

        // ------------------------------------------------------------------ ống uốn (dây dẫn, cầu muối)
        public static Vector3 CatmullRom(Vector3[] p, float u)
        {
            int n = p.Length - 1;
            if (n < 1) return p[0];
            u = Mathf.Clamp01(u) * n;
            int s = Mathf.Min(n - 1, Mathf.FloorToInt(u));
            float t = u - s;
            Vector3 p0 = p[Mathf.Max(s - 1, 0)], p1 = p[s], p2 = p[s + 1], p3 = p[Mathf.Min(s + 2, n)];
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        /// <summary>haiLuot = true cho ống THUỶ TINH / chất lỏng (trong suốt vẽ hai lượt), false cho dây dẫn đặc.
        /// Ống uốn theo đường Catmull-Rom qua `diem`. Khung dọc ống theo kiểu tịnh tiến song song (không xoắn), pháp tuyến = hướng từ
        /// tâm ống ra mặt → trơn hoàn toàn, đường may cùng pháp tuyến. Hai đầu ống để hở (luôn nằm trong vật khác).</summary>
        public static Mesh Ong(Vector3[] diem, float r, int doan = 64, int vong = 16, bool haiLuot = false)
        {
            var v = new List<Vector3>();
            var nm = new List<Vector3>();
            var t = new List<int>();
            Vector3 tiep0 = (CatmullRom(diem, 0.01f) - CatmullRom(diem, 0f)).normalized;
            Vector3 phap = Vector3.Cross(tiep0, Vector3.up);
            if (phap.sqrMagnitude < 1e-6f) phap = Vector3.Cross(tiep0, Vector3.right);
            phap.Normalize();
            for (int i = 0; i <= doan; i++)
            {
                float u = (float)i / doan;
                Vector3 c = CatmullRom(diem, u);
                Vector3 tiep = (CatmullRom(diem, Mathf.Min(1f, u + 0.005f)) - CatmullRom(diem, Mathf.Max(0f, u - 0.005f))).normalized;
                phap = Vector3.ProjectOnPlane(phap, tiep).normalized; // tịnh tiến song song
                Vector3 phu = Vector3.Cross(tiep, phap);
                for (int j = 0; j <= vong; j++)
                {
                    float a = j * Mathf.PI * 2f / vong;
                    Vector3 huong = Mathf.Cos(a) * phap + Mathf.Sin(a) * phu;
                    v.Add(c + r * huong);
                    nm.Add(huong);
                }
            }
            int hang = vong + 1;
            for (int i = 0; i < doan; i++)
                for (int j = 0; j < vong; j++)
                {
                    int a = i * hang + j, b = a + hang;
                    t.Add(a); t.Add(a + 1); t.Add(b);
                    t.Add(a + 1); t.Add(b + 1); t.Add(b);
                }
            var m = new Mesh { name = "ong" };
            if (v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(v);
            m.SetNormals(nm);
            DatTamGiac(m, t, haiLuot);
            m.RecalculateBounds();
            return m;
        }
    }
}
