using System.Collections.Generic;
using Khkt.Simulation.Bio.GiaiPhau;
using UnityEngine;
using UnityEngine.Rendering;
using V = Khkt.Demo.Bio.GiaiPhau.BanMoVatLieu.Van;

namespace Khkt.Demo.Bio.GiaiPhau
{
    /// <summary>Nhãn trên collider của bàn mổ để biết tia chuột chạm vào gì.</summary>
    public class BanMoNhan : MonoBehaviour
    {
        public enum Loai { Lop, CoQuan, Mach, ThanKinh, Chi, Dau, Khay }
        public Loai loai;
        public string id;
        public int chi = -1;
    }

    /// <summary>
    /// Dựng và vẽ mẫu mổ từ trạng thái MauMo, không tự quyết định gì.
    /// Người: thân lấy theo mặt da thật của BodyParts3D, phủ khăn mổ chừa vùng mổ. Ếch: thân, đầu, chi dựng theo tỉ lệ ếch đồng.
    /// Cơ quan là lưới hữu cơ liền (gan có thùy, tim hình nón có rãnh vành, phổi có khe, thận hình hạt đậu), ruột, mạch,
    /// thần kinh là ống quét liên tục; vật liệu URP Lit có vân và bóng ướt. Lớp mô có độ dày ở mép vết rạch.
    /// </summary>
    public sealed class BanMoSanKhau : MonoBehaviour
    {
        public const int Layer = 29;
        public static readonly Vector3 Goc = new Vector3(0f, -560f, 0f);

        public MauMo Mau { get; private set; }
        public float W { get; private set; }     // bề ngang cửa sổ mổ (m)
        public float L { get; private set; }     // chiều dọc cửa sổ mổ
        public float Dz { get; private set; }    // độ sâu ứng với d = 1
        float H;                                  // ếch: độ vồng bụng
        float kThat = 1f;                         // ếch được phóng to so với kích thước thật để dễ thao tác

        readonly List<Object> rac = new List<Object>();
        Transform root;

        // Người: mặt trước thân lấy từ da BodyParts3D
        const float NguoiHX = 0.30f, NguoiY0 = 1.70f, NguoiY1 = 2.80f, NguoiZLung = -0.27f;
        float bpScale = 0.407f;
        float[,] luoiTruoc; float lx0, lx1, ly0, ly1; int lnx, lny;

        class LopVe
        {
            public LopMo lop;
            public Mesh mesh, va; public MeshCollider col;
            public int phienBanDaVe = -1;
            public int[] tamGiacToiO;
            public float day;
            public readonly Dictionary<int, float> gocVat = new Dictionary<int, float>();
            public readonly Dictionary<int, Transform> luoiBanh = new Dictionary<int, Transform>();
        }
        readonly List<LopVe> lopVe = new List<LopVe>();

        class CoQuanVe
        {
            public CoQuanMo cq;
            public Transform tr;
            public Vector3 viTriGoc;
            public readonly List<Transform> phan = new List<Transform>();
            public readonly List<Vector3> tiLe = new List<Vector3>();
            public OngQuet ong; public float banKinhOng; public System.Func<float, float> hinhOng;
            public Transform nhi1, nhi2, that;
            public Vector3 sNhi1, sNhi2, sThat;
            public Material matThat;
            public Transform vetRach;
            public Color mauGoc;
        }
        readonly List<CoQuanVe> coQuanVe = new List<CoQuanVe>();

        class MachVe { public MachMau m; public readonly List<Transform> doan = new List<Transform>(); public readonly List<Vector3> diem = new List<Vector3>(); public Transform kep; public MeshRenderer hinh; }
        readonly List<MachVe> machVe = new List<MachVe>();

        Transform[] chiTr = new Transform[4];
        readonly Quaternion[] chiGoc = new Quaternion[4];
        Transform[] ghimChi = new Transform[4];
        Transform hongEch;
        Vector3 hongScale;
        Transform hoMau, dia;
        Material matHoMau;
        readonly Transform[] giot = new Transform[56];
        readonly Vector3[] giotVanToc = new Vector3[56];
        readonly float[] giotSong = new float[56];
        int giotKe;
        int phienBanCoQuanDaVe = -1;

        // ------------------------------------------------------------------ hình học

        // Dáng ếch đồng nhìn từ bụng (u: ngang, v: 0 huyệt … 1 cổ; đầu tới v ≈ 1,56): bề rộng nửa thân và độ vồng theo v
        // huyệt nhọn, hông nở cho gốc đùi, eo thắt, ngực – vai rộng nhất, đầu tam giác mõm tròn
        static readonly float[] EchV = { -0.50f, -0.38f, -0.18f, 0.05f, 0.40f, 0.78f, 1.02f, 1.22f, 1.38f, 1.50f, 1.57f };
        static readonly float[] EchR = { 0.05f, 0.34f, 0.56f, 0.53f, 0.55f, 0.68f, 0.76f, 0.70f, 0.52f, 0.30f, 0.02f };
        static readonly float[] EchC = { 0.18f, 0.55f, 0.82f, 0.95f, 1.00f, 0.92f, 0.74f, 0.58f, 0.46f, 0.32f, 0.08f };

        static float NoiSuyMuot(float x, float[] xs, float[] ys)
        {
            if (x <= xs[0]) return ys[0];
            for (int k = 1; k < xs.Length; k++)
                if (x <= xs[k]) return Mathf.Lerp(ys[k - 1], ys[k], Mathf.SmoothStep(0f, 1f, (x - xs[k - 1]) / (xs[k] - xs[k - 1])));
            return ys[ys.Length - 1];
        }

        public static float NuaRongEch(float v) => NoiSuyMuot(v, EchV, EchR);

        float VomEch(float u, float v)
        {
            if (v < EchV[0] || v > EchV[EchV.Length - 1]) return 0f;
            float w = NuaRongEch(v);
            float a = Mathf.Abs(u - 0.5f) / Mathf.Max(0.01f, w);
            if (a >= 1f) return 0f;
            return NoiSuyMuot(v, EchV, EchC) * Mathf.Pow(1f - Mathf.Pow(a, 2.1f), 0.55f);
        }

        public float MatDa(float u, float v)
        {
            if (Mau != null && Mau.loai == LoaiMau.Nguoi && luoiTruoc != null)
            {
                float x = (u - 0.5f) * 2f * NguoiHX, y = Mathf.Lerp(NguoiY0, NguoiY1, v);
                return (MauLuoi(x, y) - NguoiZLung) * bpScale;
            }
            return H * VomEch(u, v);
        }

        float MauLuoi(float x, float y)
        {
            float fx = Mathf.Clamp((x - lx0) / (lx1 - lx0) * (lnx - 1), 0, lnx - 1.001f);
            float fy = Mathf.Clamp((y - ly0) / (ly1 - ly0) * (lny - 1), 0, lny - 1.001f);
            int i = (int)fx, j = (int)fy; float a = fx - i, b = fy - j;
            return Mathf.Lerp(Mathf.Lerp(luoiTruoc[i, j], luoiTruoc[i + 1, j], a), Mathf.Lerp(luoiTruoc[i, j + 1], luoiTruoc[i + 1, j + 1], a), b);
        }

        public Vector3 DiemCucBo(float u, float v, float d) => new Vector3((u - 0.5f) * W, MatDa(u, v) - d * Dz, (v - 0.5f) * L);
        public Vector3 DiemCucBo(Vector3 uvd) => DiemCucBo(uvd.x, uvd.y, uvd.z);
        public Vector3 DiemTheGioi(Vector3 uvd) => root.TransformPoint(DiemCucBo(uvd));

        public Vector2 UV(Vector3 world)
        {
            var p = root.InverseTransformPoint(world);
            return new Vector2(p.x / W + 0.5f, p.z / L + 0.5f);
        }

        /// <summary>Giao tia với mặt lớp ở độ sâu d (để đường rạch bám đúng mặt mô kể cả khi lướt qua khe đã rạch).</summary>
        public bool GiaoMatLop(Ray r, float d, out Vector2 uv)
        {
            uv = default;
            var o = root.InverseTransformPoint(r.origin);
            var dir = root.InverseTransformDirection(r.direction);
            float tMax = 8f;
            float F(Vector3 p) => p.y - (MatDa(p.x / W + 0.5f, p.z / L + 0.5f) - d * Dz);
            float prev = F(o), tPrev = 0f;
            const int Nb = 200;
            for (int k = 1; k <= Nb; k++)
            {
                float t = tMax * k / Nb;
                float f = F(o + dir * t);
                if (prev > 0f && f <= 0f)
                {
                    float a = tPrev, b = t;
                    for (int it = 0; it < 20; it++) { float m = (a + b) * 0.5f; if (F(o + dir * m) > 0f) a = m; else b = m; }
                    var hit = o + dir * b;
                    uv = new Vector2(hit.x / W + 0.5f, hit.z / L + 0.5f);
                    return true;
                }
                prev = f; tPrev = t;
            }
            return false;
        }

        public float DoSauDay(float u, float v)
        {
            float mep = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
            float d = 0.11f + 0.42f * Mathf.SmoothStep(0f, 1f, mep / 0.16f);
            // đáy lõm dần vào giữa: dịch, máu đọng thành vũng ở chỗ thấp nhất chứ không phủ phẳng cả khoang
            float cu = (u - 0.5f) / 0.5f, cv = (v - 0.42f) / 0.58f;
            d += 0.06f * Mathf.Max(0f, 1f - cu * cu) * Mathf.Max(0f, 1f - cv * cv);
            if (Mau != null && Mau.loai == LoaiMau.Nguoi && v < 0.56f)
            {
                // cột sống thắt lưng và cơ thắt lưng chậu nhô ra phía trước ở thành bụng sau
                float x = (u - 0.5f) / 0.07f, xa = (u - 0.36f) / 0.06f, xb = (u - 0.64f) / 0.06f;
                d -= 0.13f * Mathf.Exp(-x * x) + 0.05f * Mathf.Exp(-xa * xa) + 0.05f * Mathf.Exp(-xb * xb);
            }
            return d;
        }

        // ------------------------------------------------------------------ dựng

        Material Mat(Color c, V van, float bong, float lap = 2f, float noi = 1f)
        {
            var m = BanMoVatLieu.Tao(c, van, bong, lap, noi);
            rac.Add(m);
            return m;
        }

        Transform Vat(Transform cha, string ten, Mesh mesh, Material mat, Vector3 pos, Vector3 scale, Quaternion? rot = null)
        {
            var go = new GameObject(ten) { layer = Layer };
            go.transform.SetParent(cha, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            return go.transform;
        }

        Mesh Luu(Mesh m) { rac.Add(m); return m; }

        Mesh KhoiHinh(string ten, System.Func<Vector3, Vector3> f, int seg = 40, int ring = 28) => Luu(BanMoLuoi.Khoi(ten, seg, ring, f));

        public void Dung(MauMo mau)
        {
            XoaHet();
            Mau = mau;
            if (mau.loai == LoaiMau.Ech) { W = 0.16f; L = 0.22f; Dz = 0.11f; H = 0.05f; kThat = 0.16f / 0.042f; luoiTruoc = null; }
            else
            {
                bpScale = Bp3dReader.BodyScale;
                W = 2f * NguoiHX * bpScale; L = (NguoiY1 - NguoiY0) * bpScale; Dz = 0.22f; kThat = 1f;
                if (!DungLuoiTruocNguoi()) { H = 0.11f; luoiTruoc = null; }
            }

            var go = new GameObject("BanMo_" + mau.loai) { layer = Layer };
            go.transform.SetParent(transform, false);
            go.transform.position = Goc;
            root = go.transform;

            DungAnhSang();
            if (mau.loai == LoaiMau.Ech) DungEch(); else DungNguoi();
            DungKhoang();
            foreach (var l in mau.lop) lopVe.Add(new LopVe { lop = l });
            foreach (var lv in lopVe) TaoLop(lv);
            foreach (var cq in mau.coQuan) DungCoQuan(cq);
            foreach (var m in mau.mach) DungMach(m);
            foreach (var tk in mau.thanKinh) DungThanKinh(tk);
            DungMau();
            CapNhat(0f);
        }

        void XoaHet()
        {
            if (root != null) Destroy(root.gameObject);
            foreach (var o in rac) if (o != null) Destroy(o);
            rac.Clear();
            lopVe.Clear(); coQuanVe.Clear(); machVe.Clear();
            chiTr = new Transform[4]; ghimChi = new Transform[4];
            phienBanCoQuanDaVe = -1;
            hongEch = null;
        }

        void OnDestroy() => XoaHet();

        void DungAnhSang()
        {
            // Đèn mổ: chùm sáng trắng ấm từ trên, đèn phụ dịu, đèn viền lạnh tạo khối cho mặt ướt
            var k = new GameObject("DenMo") { layer = Layer };
            k.transform.SetParent(root, false);
            k.transform.localPosition = new Vector3(0.05f, 1.1f, -0.25f);
            k.transform.LookAt(root.position);
            var lk = k.AddComponent<Light>();
            lk.type = LightType.Spot; lk.range = 3f; lk.spotAngle = 55f; lk.innerSpotAngle = 30f;
            lk.intensity = Mau.loai == LoaiMau.Ech ? 1.5f : 1.8f; lk.color = new Color(1f, 0.97f, 0.92f);
            lk.cullingMask = 1 << Layer; lk.shadows = LightShadows.None;
            var p = new GameObject("DenPhu") { layer = Layer };
            p.transform.SetParent(root, false);
            p.transform.rotation = Quaternion.Euler(50f, 150f, 0f);
            var lp = p.AddComponent<Light>();
            lp.type = LightType.Directional; lp.intensity = 0.35f; lp.color = new Color(0.86f, 0.92f, 1f);
            lp.cullingMask = 1 << Layer; lp.shadows = LightShadows.None;
            var v = new GameObject("DenVien") { layer = Layer };
            v.transform.SetParent(root, false);
            v.transform.localPosition = new Vector3(-0.5f, 0.35f, 0.6f);
            var lv = v.AddComponent<Light>();
            lv.type = LightType.Point; lv.range = 2.5f; lv.intensity = 0.25f; lv.color = new Color(0.75f, 0.85f, 1f);
            lv.cullingMask = 1 << Layer;
        }

        // ------------------------------------------------------------------ người: thân theo da BodyParts3D, khăn mổ

        bool DungLuoiTruocNguoi()
        {
            var da = Bp3dReader.LayLuoiDocDuoc("da");
            if (da == null || !da.isReadable) return false;
            var vs = da.vertices;
            lx0 = -0.82f; lx1 = 0.82f; ly0 = -0.64f; ly1 = 3.58f; lnx = 84; lny = 210;
            luoiTruoc = new float[lnx, lny];
            var co = new bool[lnx, lny];
            for (int i = 0; i < lnx; i++) for (int j = 0; j < lny; j++) luoiTruoc[i, j] = float.MinValue;
            foreach (var p in vs)
            {
                int i = Mathf.RoundToInt((p.x - lx0) / (lx1 - lx0) * (lnx - 1));
                int j = Mathf.RoundToInt((p.y - ly0) / (ly1 - ly0) * (lny - 1));
                if (i < 0 || j < 0 || i >= lnx || j >= lny) continue;
                if (p.z > luoiTruoc[i, j]) { luoiTruoc[i, j] = p.z; co[i, j] = true; }
            }
            // lấp ô trống bằng trung bình lân cận, ô ngoài bóng người hạ xuống mặt bàn
            for (int lap = 0; lap < 6; lap++)
            {
                var moi = (float[,])luoiTruoc.Clone();
                for (int i = 0; i < lnx; i++)
                    for (int j = 0; j < lny; j++)
                    {
                        if (co[i, j]) continue;
                        float s = 0; int n = 0;
                        for (int a = -1; a <= 1; a++) for (int b = -1; b <= 1; b++)
                        {
                            int x = i + a, y = j + b;
                            if (x < 0 || y < 0 || x >= lnx || y >= lny || luoiTruoc[x, y] == float.MinValue) continue;
                            s += luoiTruoc[x, y]; n++;
                        }
                        if (n > 0) moi[i, j] = s / n;
                    }
                luoiTruoc = moi;
            }
            for (int i = 0; i < lnx; i++) for (int j = 0; j < lny; j++) if (luoiTruoc[i, j] == float.MinValue) luoiTruoc[i, j] = NguoiZLung - 0.02f;
            for (int lap = 0; lap < 2; lap++)
            {
                var moi = (float[,])luoiTruoc.Clone();
                for (int i = 1; i < lnx - 1; i++)
                    for (int j = 1; j < lny - 1; j++)
                        moi[i, j] = (luoiTruoc[i, j] * 4f + luoiTruoc[i - 1, j] + luoiTruoc[i + 1, j] + luoiTruoc[i, j - 1] + luoiTruoc[i, j + 1]) / 8f;
                luoiTruoc = moi;
            }
            return true;
        }

        Vector3 BpSangCucBo(Vector3 p) => new Vector3(p.x * bpScale, (p.z - NguoiZLung) * bpScale, (p.y - (NguoiY0 + NguoiY1) * 0.5f) * bpScale);

        void DungNguoi()
        {
            var ban = Vat(root, "BanMo", Tru(), Mat(new Color(0.55f, 0.58f, 0.6f), V.Khay, 0.7f, 3f, 0.3f), new Vector3(0f, -0.06f, -0.2f), new Vector3(0.62f, 0.05f, 2.1f));
            ban.gameObject.AddComponent<BoxCollider>().size = new Vector3(1f, 2f, 1f);
            ban.gameObject.AddComponent<BanMoNhan>().loai = BanMoNhan.Loai.Khay;

            // Da thân thật (BodyParts3D), bỏ phần nằm trong cửa sổ mổ
            var da = Bp3dReader.LayLuoiDocDuoc("da");
            if (da != null && da.isReadable && luoiTruoc != null)
            {
                var src = da.vertices; var tri = da.triangles;
                var vs = new Vector3[src.Length];
                for (int k = 0; k < src.Length; k++) vs[k] = BpSangCucBo(src[k]);
                var giu = new List<int>(tri.Length);
                for (int k = 0; k < tri.Length; k += 3)
                {
                    var a = src[tri[k]]; var b = src[tri[k + 1]]; var c = src[tri[k + 2]];
                    var tam = (a + b + c) / 3f;
                    bool trongCua = Mathf.Abs(tam.x) < NguoiHX && tam.y > NguoiY0 && tam.y < NguoiY1 && tam.z > MauLuoi(tam.x, tam.y) - 0.06f;
                    if (trongCua) continue;
                    giu.Add(tri[k]); giu.Add(tri[k + 2]); giu.Add(tri[k + 1]); // đổi trục y↔z nên đảo chiều tam giác
                }
                var m = new Mesh { name = "DaNguoiBP3D", indexFormat = IndexFormat.UInt32 };
                m.vertices = vs; m.SetTriangles(giu, 0);
                var uv = new Vector2[vs.Length];
                for (int k = 0; k < vs.Length; k++) uv[k] = new Vector2(vs[k].x * 6f, vs[k].z * 6f);
                m.uv = uv;
                m.RecalculateNormals(); m.RecalculateBounds(); m.RecalculateTangents();
                Vat(root, "DaThan", Luu(m), Mat(new Color(0.88f, 0.70f, 0.60f), V.DaNguoi, 0.42f, 3f, 0.6f), Vector3.zero, Vector3.one);
            }

            // Khăn mổ: vải phủ toàn thân theo mặt trước, chừa cửa sổ mổ và một viền da khoảng 2 cm
            if (luoiTruoc != null)
            {
                const int NX = 70, NY = 180;
                var vs = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
                float mepX = NguoiHX + 0.05f, mepY0 = NguoiY0 - 0.05f, mepY1 = NguoiY1 + 0.05f;
                var id = new int[NX + 1, NY + 1];
                for (int j = 0; j <= NY; j++)
                    for (int i = 0; i <= NX; i++)
                    {
                        float x = Mathf.Lerp(-0.95f, 0.95f, i / (float)NX), y = Mathf.Lerp(-0.70f, 3.70f, j / (float)NY);
                        float z = Mathf.Max(MauLuoi(x, y) + 0.018f, NguoiZLung + 0.03f) + 0.004f * Mathf.Sin(x * 40f) * Mathf.Sin(y * 13f);
                        id[i, j] = vs.Count;
                        vs.Add(new Vector3(x * bpScale, (z - NguoiZLung) * bpScale, (y - (NguoiY0 + NguoiY1) * 0.5f) * bpScale));
                        uv.Add(new Vector2(x * 3f, y * 3f));
                    }
                for (int j = 0; j < NY; j++)
                    for (int i = 0; i < NX; i++)
                    {
                        float x = Mathf.Lerp(-0.95f, 0.95f, (i + 0.5f) / NX), y = Mathf.Lerp(-0.70f, 3.70f, (j + 0.5f) / NY);
                        if (Mathf.Abs(x) < mepX && y > mepY0 && y < mepY1) continue;
                        t.Add(id[i, j]); t.Add(id[i, j + 1]); t.Add(id[i + 1, j]);
                        t.Add(id[i + 1, j]); t.Add(id[i, j + 1]); t.Add(id[i + 1, j + 1]);
                    }
                var m = new Mesh { name = "KhanMo", indexFormat = IndexFormat.UInt32 };
                m.SetVertices(vs); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateTangents();
                Vat(root, "KhanMo", Luu(m), Mat(new Color(0.20f, 0.45f, 0.50f), V.Vai, 0.12f, 4f, 0.4f), Vector3.zero, Vector3.one);
            }
        }

        static Mesh truMesh;
        Mesh Tru()
        {
            if (truMesh != null) return truMesh;
            truMesh = BanMoLuoi.Khoi("BanMo_Tru", 32, 6, d =>
            {
                var xz = new Vector2(d.x, d.z);
                var h = xz.sqrMagnitude < 1e-8f ? Vector2.zero : xz.normalized * 0.5f;
                return new Vector3(h.x, d.y > 0.2f ? 0.5f : d.y < -0.2f ? -0.5f : d.y * 2.5f, h.y);
            });
            return truMesh;
        }

        static Mesh cauMesh;
        Mesh Cau() => cauMesh != null ? cauMesh : cauMesh = BanMoLuoi.Khoi("BanMo_Cau", 24, 16, null);

        // ------------------------------------------------------------------ ếch: thân, đầu, chi

        void DungEch()
        {
            var khay = Vat(root, "KhaySap", Tru(), Mat(new Color(0.62f, 0.64f, 0.66f), V.Khay, 0.75f, 3f, 0.2f), new Vector3(0f, -0.016f, 0.02f), new Vector3(0.56f, 0.012f, 0.70f));
            Vat(root, "MatSap", Tru(), Mat(new Color(0.10f, 0.10f, 0.11f), V.Khay, 0.55f, 6f, 0.6f), new Vector3(0f, -0.006f, 0.02f), new Vector3(0.52f, 0.004f, 0.66f));
            khay.gameObject.AddComponent<BoxCollider>().size = new Vector3(1f, 2f, 1f);
            khay.gameObject.AddComponent<BanMoNhan>().loai = BanMoNhan.Loai.Khay;

            var daBung = Mat(new Color(0.90f, 0.87f, 0.74f), V.DaEch, 0.62f, 3f, 0.8f);
            var daLung = Mat(new Color(0.38f, 0.44f, 0.22f), V.DaEch, 0.55f, 3f, 1f);

            // Thân và đầu một khối liền: mặt bụng (chừa cửa sổ mổ) và mặt lưng sẫm phía dưới, khép ở mép thân
            DungMatThanEch(daBung, true);
            DungMatThanEch(daLung, false);
            // vùng đầu (va chạm vô hình) để dùng kim hủy tủy, ống thổi
            var dau = new GameObject("DauEch") { layer = Layer };
            dau.transform.SetParent(root, false);
            dau.transform.localPosition = new Vector3(0f, H * 0.3f, (1.32f - 0.5f) * L);
            dau.AddComponent<BoxCollider>().size = new Vector3(W * 1.05f, H * 1.2f, L * 0.48f);
            dau.AddComponent<BanMoNhan>().loai = BanMoNhan.Loai.Dau;
            // viền miệng: đường khe hàm chạy quanh mõm
            var mieng = new List<Vector3>();
            for (int k = 0; k <= 24; k++)
            {
                float t = k / 24f;
                float v = Mathf.Lerp(1.18f, 1.555f, Mathf.Sin(t * Mathf.PI));
                float w = NuaRongEch(v) * 0.985f * (t < 0.5f ? -1f : 1f);
                if (k == 12) w = 0f;
                float u = 0.5f + w;
                mieng.Add(new Vector3((u - 0.5f) * W, MatDa(u, v) * 0.55f + 0.0006f, (v - 0.5f) * L));
            }
            var ongMieng = new OngQuet("Mieng", BanMoLuoi.Muot(mieng, 3), 6);
            ongMieng.CapNhat(t => 0.0011f, true); Luu(ongMieng.mesh);
            Vat(root, "Mieng", ongMieng.mesh, Mat(new Color(0.30f, 0.24f, 0.16f), V.Tron, 0.6f), Vector3.zero, Vector3.one);
            foreach (float sm in new[] { -1f, 1f })
                Vat(root, "Mat", Cau(), Mat(new Color(0.10f, 0.09f, 0.06f), V.Tron, 0.95f), new Vector3(sm * W * 0.36f, -H * 0.15f, (1.36f - 0.5f) * L), new Vector3(W * 0.17f, H * 0.6f, L * 0.09f));
            // họng: thềm miệng nâng hạ khi còn thở
            hongEch = Vat(root, "HongEch", KhoiHinh("HongEch", d => { var p = d * 0.5f; if (p.y < 0) p.y *= 0.1f; return p; }),
                Mat(new Color(0.92f, 0.88f, 0.76f), V.DaEch, 0.6f, 2f, 0.5f), new Vector3(0f, MatDa(0.5f, 1.25f) - H * 0.05f, (1.25f - 0.5f) * L), new Vector3(W * 0.55f, H * 0.18f, L * 0.16f));
            hongScale = hongEch.localScale;

            // Chi: ống thon theo đoạn xương, bàn chân có ngón dài và màng bơi; 0 trước phải, 1 trước trái, 2 sau phải, 3 sau trái
            var ghimMat = Mat(new Color(0.82f, 0.84f, 0.88f), V.Tron, 0.95f);
            var dauGhim = Mat(new Color(0.85f, 0.12f, 0.12f), V.Tron, 0.7f);
            var mang = Mat(new Color(0.88f, 0.84f, 0.68f, 0.9f), V.DaEch, 0.6f, 4f, 0.5f);
            for (int c = 0; c < 4; c++)
            {
                bool truoc = c < 2; float s = (c % 2 == 0) ? -1f : 1f;
                var g = new GameObject("Chi" + c) { layer = Layer };
                g.transform.SetParent(root, false);
                g.transform.localPosition = new Vector3(s * W * (truoc ? 0.66f : 0.40f), H * (truoc ? 0.25f : 0.30f), truoc ? L * 0.38f : -L * 0.80f);
                chiTr[c] = g.transform; chiGoc[c] = Quaternion.identity;
                float S = W;
                List<Vector3> xuong; float[] r;
                if (truoc)
                {
                    // cánh tay ngắn chĩa ra ngoài – sau, cẳng tay gập về trước, bàn tay hướng vào trong
                    xuong = new List<Vector3> { Vector3.zero, new Vector3(s * 0.26f, -0.04f, -0.10f) * S, new Vector3(s * 0.30f, -0.07f, 0.14f) * S, new Vector3(s * 0.22f, -0.09f, 0.30f) * S };
                    r = new[] { 0.10f * S, 0.075f * S, 0.055f * S, 0.035f * S };
                }
                else
                {
                    // đùi dạng ra, cẳng gập, bàn duỗi ra sau: tư thế ghim trên khay
                    // đùi dạng ra hai bên và hơi về trước, gối gập, cẳng chạy ngược ra sau, cổ chân dài, bàn chân duỗi
                    xuong = new List<Vector3> { Vector3.zero, new Vector3(s * 0.62f, -0.06f, 0.10f) * S, new Vector3(s * 0.70f, -0.10f, -0.42f) * S, new Vector3(s * 0.56f, -0.12f, -0.82f) * S, new Vector3(s * 0.66f, -0.13f, -1.18f) * S };
                    r = new[] { 0.26f * S, 0.15f * S, 0.10f * S, 0.055f * S, 0.035f * S }; // đùi to cơ bắp, thon về cổ chân
                }
                var ong = new OngQuet("Chi" + c, BanMoLuoi.Muot(xuong, 8), 14);
                var rr = r;
                ong.CapNhat(t => { float x = t * (rr.Length - 1); int k = Mathf.Min((int)x, rr.Length - 2); return Mathf.Lerp(rr[k], rr[k + 1], x - k); }, true);
                Luu(ong.mesh);
                var chiVat = Vat(g.transform, "Than", ong.mesh, daBung, Vector3.zero, Vector3.one);
                var bc = chiVat.gameObject.AddComponent<BoxCollider>(); bc.center = ong.mesh.bounds.center; bc.size = ong.mesh.bounds.size;
                var nh = chiVat.gameObject.AddComponent<BanMoNhan>(); nh.loai = BanMoNhan.Loai.Chi; nh.chi = c;
                Vector3 coTay = xuong[xuong.Count - 1], huong = (xuong[xuong.Count - 1] - xuong[xuong.Count - 2]).normalized;
                int soNgon = truoc ? 4 : 5; float dai = truoc ? 0.16f * S : 0.42f * S;
                var dauNgon = new List<Vector3>();
                for (int n = 0; n < soNgon; n++)
                {
                    float goc = Mathf.Lerp(-35f, 35f, n / (float)(soNgon - 1));
                    var h = Quaternion.Euler(0f, goc, 0f) * huong;
                    var p2 = coTay + h * dai * (truoc ? 1f : (0.7f + 0.3f * Mathf.Sin(n / (float)(soNgon - 1) * Mathf.PI)));
                    var ngon = new OngQuet("Ngon", BanMoLuoi.Muot(new List<Vector3> { coTay, (coTay + p2) * 0.5f + Vector3.up * 0.004f, p2 }, 4), 8);
                    float r0 = rr[rr.Length - 1] * 0.45f;
                    ngon.CapNhat(t => Mathf.Lerp(r0, r0 * 0.55f, t), true);
                    Luu(ngon.mesh);
                    Vat(g.transform, "Ngon" + n, ngon.mesh, daBung, Vector3.zero, Vector3.one);
                    dauNgon.Add(p2);
                }
                if (!truoc)
                {
                    var mv = new List<Vector3> { coTay }; var mt = new List<int>();
                    for (int n = 0; n < dauNgon.Count; n++) mv.Add(Vector3.Lerp(coTay, dauNgon[n], 0.82f));
                    for (int n = 1; n < dauNgon.Count; n++) { mt.Add(0); mt.Add(n); mt.Add(n + 1); mt.Add(0); mt.Add(n + 1); mt.Add(n); }
                    var mm = new Mesh { name = "MangBoi" }; mm.SetVertices(mv); mm.SetTriangles(mt, 0);
                    var muv = new Vector2[mv.Count]; for (int k = 0; k < mv.Count; k++) muv[k] = new Vector2(mv[k].x * 20f, mv[k].z * 20f);
                    mm.uv = muv; mm.RecalculateNormals(); mm.RecalculateTangents();
                    Vat(g.transform, "MangBoi", Luu(mm), mang, Vector3.zero, Vector3.one);
                }
                var ban = Vat(g.transform, "BanChan", Cau(), daBung, coTay + huong * dai * 0.35f, new Vector3(dai * 0.8f, 0.012f, dai * 0.8f));
                ban.GetComponent<MeshRenderer>().enabled = false;
                ban.gameObject.AddComponent<BoxCollider>().size = Vector3.one;
                var nh2 = ban.gameObject.AddComponent<BanMoNhan>(); nh2.loai = BanMoNhan.Loai.Chi; nh2.chi = c;
                var pin = Vat(g.transform, "Ghim", Tru(), ghimMat, coTay + huong * dai * 0.3f + Vector3.up * 0.02f, new Vector3(0.0025f, 0.05f, 0.0025f));
                Vat(pin, "DauGhim", Cau(), dauGhim, new Vector3(0f, 0.5f, 0f), new Vector3(3.2f, 0.12f, 3.2f));
                ghimChi[c] = pin;
            }
        }

        void DungMatThanEch(Material mat, bool bung)
        {
            var vs = new List<Vector3>(); var uv = new List<Vector2>(); var ts = new List<int>();
            const int NU = 56, NV = 110;
            float vMin = EchV[0], vMax = EchV[EchV.Length - 1];
            var idx = new int[NU + 1, NV + 1];
            for (int j = 0; j <= NV; j++)
            {
                float v = Mathf.Lerp(vMin, vMax, j / (float)NV);
                float w = Mathf.Max(0.0005f, NuaRongEch(v));
                for (int i = 0; i <= NU; i++)
                {
                    // lưới theo bề rộng thật của thân để mép khép kín (u chạy từ mép trái tới mép phải)
                    float s = i / (float)NU;
                    float u = 0.5f + (s * 2f - 1f) * w;
                    float h = VomEch(u, v);
                    float y = bung ? H * h + 0.0004f : -H * 0.55f * Mathf.Pow(h, 0.8f);
                    idx[i, j] = vs.Count;
                    vs.Add(new Vector3((u - 0.5f) * W, y, (v - 0.5f) * L));
                    uv.Add(new Vector2(u * 2.2f, v * 2.2f));
                }
            }
            for (int j = 0; j < NV; j++)
                for (int i = 0; i < NU; i++)
                {
                    if (bung)
                    {
                        // bỏ ô nằm trọn trong cửa sổ mổ (ô chạm mép thì giữ để phủ chồng, không hở khe)
                        bool trong = true;
                        for (int a2 = 0; a2 <= 1 && trong; a2++)
                            for (int b2 = 0; b2 <= 1 && trong; b2++)
                            {
                                float v = Mathf.Lerp(vMin, vMax, (j + b2) / (float)NV);
                                float u = 0.5f + (((i + a2) / (float)NU) * 2f - 1f) * NuaRongEch(v);
                                if (!(u > 0.005f && u < 0.995f && v > 0.005f && v < 0.995f)) trong = false;
                            }
                        if (trong) continue;
                    }
                    int a = idx[i, j], b = idx[i, j + 1], c = idx[i + 1, j], d = idx[i + 1, j + 1];
                    if (bung) { ts.Add(a); ts.Add(b); ts.Add(c); ts.Add(c); ts.Add(b); ts.Add(d); }
                    else { ts.Add(a); ts.Add(c); ts.Add(b); ts.Add(c); ts.Add(d); ts.Add(b); }
                }
            var m = new Mesh { name = bung ? "BungEch" : "LungEch" };
            m.SetVertices(vs); m.SetUVs(0, uv); m.SetTriangles(ts, 0); m.RecalculateNormals(); m.RecalculateTangents();
            Vat(root, bung ? "BungEch" : "LungEch", Luu(m), mat, Vector3.zero, Vector3.one);
        }

        void DungKhoang()
        {
            var m = new Mesh { name = "KhoangCoThe" };
            var vs = new List<Vector3>(); var uv = new List<Vector2>(); var ts = new List<int>();
            const int N = 48;
            for (int j = 0; j <= N; j++)
                for (int i = 0; i <= N; i++)
                {
                    float u = i / (float)N, v = j / (float)N;
                    vs.Add(DiemCucBo(u, v, DoSauDay(u, v)));
                    uv.Add(new Vector2(u * 2f, v * 3f));
                }
            for (int j = 0; j < N; j++)
                for (int i = 0; i < N; i++)
                {
                    int a = i + j * (N + 1), b = a + N + 1;
                    ts.Add(a); ts.Add(b); ts.Add(a + 1);
                    ts.Add(a + 1); ts.Add(b); ts.Add(b + 1);
                }
            m.SetVertices(vs); m.SetUVs(0, uv); m.SetTriangles(ts, 0); m.RecalculateNormals(); m.RecalculateTangents();
            if (Mau.loai == LoaiMau.Ech)
                Vat(root, "ThanhSau", Luu(m), Mat(new Color(0.80f, 0.70f, 0.66f), V.SacTo, 0.62f, 3f, 0.4f), Vector3.zero, Vector3.one);
            else
                Vat(root, "ThanhSau", Luu(m), Mat(new Color(0.66f, 0.30f, 0.28f), V.Co, 0.55f, 2f, 0.4f), Vector3.zero, Vector3.one);
        }

        // ------------------------------------------------------------------ lớp mô

        float DayLop(LopMo l)
        {
            bool ech = Mau.loai == LoaiMau.Ech;
            float day = l.loai switch
            {
                LoaiLop.Da => ech ? 0.0025f : 0.011f,
                LoaiLop.Co => ech ? 0.0030f : 0.009f,
                LoaiLop.Xuong => ech ? 0.0025f : 0.010f,
                _ => 0.0007f
            };
            float gap = 1f;
            foreach (var k in Mau.lop) if (k.doSau > l.doSau + 1e-4f) gap = Mathf.Min(gap, (k.doSau - l.doSau) * Dz);
            return Mathf.Min(day, gap * 0.85f);
        }

        void TaoLop(LopVe lv)
        {
            var l = lv.lop;
            var go = new GameObject("Lop_" + l.id) { layer = Layer };
            go.transform.SetParent(root, false);
            lv.mesh = Luu(new Mesh { name = "Lop_" + l.id });
            lv.mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = lv.mesh;
            var mr = go.AddComponent<MeshRenderer>();
            bool ech = Mau.loai == LoaiMau.Ech;
            Material ngoai, trong, mep;
            switch (l.loai)
            {
                case LoaiLop.Da:
                    ngoai = Mat(l.mauNgoai, ech ? V.DaEch : V.DaNguoi, ech ? 0.62f : 0.42f, ech ? 3f : 5f, 0.7f);
                    trong = Mat(ech ? new Color(0.84f, 0.60f, 0.56f) : new Color(0.97f, 0.84f, 0.42f), ech ? V.Mang : V.Mo, 0.7f, ech ? 2f : 4f, 1f);
                    mep = Mat(ech ? new Color(0.88f, 0.66f, 0.60f) : new Color(0.98f, 0.86f, 0.46f), ech ? V.Mang : V.Mo, 0.75f, 6f, 1f);
                    break;
                case LoaiLop.Co:
                    ngoai = Mat(l.mauNgoai, ech ? V.CoBung : V.Co, 0.68f, ech ? 1f : 3f, 1.2f);
                    trong = Mat(l.mauTrong, V.Co, 0.72f, 3f, 1f);
                    mep = Mat(new Color(0.62f, 0.10f, 0.12f), V.Co, 0.8f, 6f, 1f);
                    break;
                case LoaiLop.Xuong:
                    ngoai = Mat(l.mauNgoai, V.Xuong, 0.45f, 3f, 0.8f);
                    trong = Mat(l.mauTrong, V.Mang, 0.6f, 3f, 0.6f);
                    mep = Mat(new Color(0.78f, 0.46f, 0.40f), V.Xuong, 0.5f, 6f, 1.2f);
                    break;
                default:
                    ngoai = Mat(l.mauNgoai, V.Mang, 0.9f, 2f, 0.6f);
                    trong = Mat(l.mauTrong, V.Mang, 0.9f, 2f, 0.6f);
                    mep = Mat(new Color(0.9f, 0.8f, 0.78f), V.Mang, 0.9f, 2f, 0.4f);
                    break;
            }
            mr.sharedMaterials = new[] { ngoai, trong, mep };
            mr.shadowCastingMode = ShadowCastingMode.Off;
            lv.col = go.AddComponent<MeshCollider>();
            lv.col.sharedMesh = null;
            var nh = go.AddComponent<BanMoNhan>(); nh.loai = BanMoNhan.Loai.Lop; nh.id = l.id;
            lv.day = DayLop(l);
        }

        public bool OTuTamGiac(string lopId, int tamGiac, out LopMo lop, out int o)
        {
            lop = null; o = -1;
            foreach (var lv in lopVe)
            {
                if (lv.lop.id != lopId) continue;
                lop = lv.lop;
                if (lv.tamGiacToiO == null || tamGiac < 0 || tamGiac >= lv.tamGiacToiO.Length) return false;
                o = lv.tamGiacToiO[tamGiac];
                return true;
            }
            return false;
        }

        void VeLop(LopVe lv, float dt)
        {
            var l = lv.lop;
            bool dangLat = false;
            foreach (var vm in Mau.vat)
            {
                if (vm.lop != l || vm.oVat.Count == 0) continue;
                int id = l.vatCuaO[vm.oVat[0]];
                if (id < 0) continue;
                // ghim / banh: vạt nằm ngửa ra ngoài; chưa ghim: dựng nghiêng rồi tự rơi về (mô hình quyết định)
                float dich = vm.banh ? 100f : vm.ghim ? 172f : 128f; // banh: 0..100 = mức kéo mép sang bên
                lv.gocVat.TryGetValue(id, out float g);
                float g2 = Mathf.MoveTowards(g, dich, dt * 260f);
                if (Mathf.Abs(g2 - g) > 0.01f) dangLat = true;
                lv.gocVat[id] = g2; vm.goc = g2;
            }
            if (!dangLat && lv.phienBanDaVe == l.phienBan) return;
            lv.phienBanDaVe = l.phienBan;

            var vs = new List<Vector3>(); var uvs = new List<Vector2>();
            var t0 = new List<int>(); var t1 = new List<int>(); var t2 = new List<int>(); var map = new List<int>();
            const float khe = 0.16f;
            const int sub = 2;
            float day = lv.day;
            const float lapUv = 6f;
            for (int j = 0; j < l.nv; j++)
                for (int i = 0; i < l.nu; i++)
                {
                    int c = l.Idx(i, j);
                    var st = l.o[c];
                    if (st == TrangThaiO.Bo) continue;
                    bool cT = l.DaCatDoc(i - 1, j) || (i == 0 && !l.dinhTrai);
                    bool cP = l.DaCatDoc(i, j) || (i == l.nu - 1 && !l.dinhPhai);
                    bool cD = l.DaCatNgang(i, j - 1) || (j == 0 && !l.dinhDuoi);
                    bool cTr = l.DaCatNgang(i, j) || (j == l.nv - 1 && !l.dinhTren);
                    float du = (l.u1 - l.u0) / l.nu, dv = (l.v1 - l.v0) / l.nv;
                    float uA = l.u0 + (i + (cT ? khe : 0f)) * du, uB = l.u0 + (i + 1 - (cP ? khe : 0f)) * du;
                    float vA = l.v0 + (j + (cD ? khe : 0f)) * dv, vB = l.v0 + (j + 1 - (cTr ? khe : 0f)) * dv;

                    VatMo vm = null; float goc = 0f;
                    if (st == TrangThaiO.Lat && l.vatCuaO[c] >= 0 && l.vatCuaO[c] < Mau.vat.Count) { vm = Mau.vat[l.vatCuaO[c]]; lv.gocVat.TryGetValue(l.vatCuaO[c], out goc); }
                    // vạt đã banh xong: mô dồn dưới lưỡi banh, chỉ còn vách vết mổ (vẽ riêng bên dưới)
                    if (vm != null && vm.banh && goc > 50f) continue;

                    var p = new Vector3[sub + 1, sub + 1];
                    var q = new Vector3[sub + 1, sub + 1];
                    for (int y = 0; y <= sub; y++)
                        for (int x = 0; x <= sub; x++)
                        {
                            float u = Mathf.Lerp(uA, uB, x / (float)sub), v = Mathf.Lerp(vA, vB, y / (float)sub);
                            var tren = DiemCucBo(u, v, l.doSau);
                            var duoi = tren - Vector3.up * day;
                            if (vm != null) { tren = XoayVat(tren, vm, goc); duoi = XoayVat(duoi, vm, goc); }
                            p[x, y] = tren; q[x, y] = duoi;
                        }
                    for (int y = 0; y < sub; y++)
                        for (int x = 0; x < sub; x++)
                        {
                            int k = vs.Count;
                            vs.Add(p[x, y]); vs.Add(p[x + 1, y]); vs.Add(p[x, y + 1]); vs.Add(p[x + 1, y + 1]);
                            float ua = Mathf.Lerp(uA, uB, x / (float)sub) * lapUv, ub = Mathf.Lerp(uA, uB, (x + 1f) / sub) * lapUv;
                            float va = Mathf.Lerp(vA, vB, y / (float)sub) * lapUv, vb = Mathf.Lerp(vA, vB, (y + 1f) / sub) * lapUv;
                            uvs.Add(new Vector2(ua, va)); uvs.Add(new Vector2(ub, va)); uvs.Add(new Vector2(ua, vb)); uvs.Add(new Vector2(ub, vb));
                            t0.Add(k); t0.Add(k + 2); t0.Add(k + 1); t0.Add(k + 1); t0.Add(k + 2); t0.Add(k + 3);
                            map.Add(c); map.Add(c);
                            int k2 = vs.Count;
                            vs.Add(q[x, y]); vs.Add(q[x + 1, y]); vs.Add(q[x, y + 1]); vs.Add(q[x + 1, y + 1]);
                            uvs.Add(new Vector2(ua, va)); uvs.Add(new Vector2(ub, va)); uvs.Add(new Vector2(ua, vb)); uvs.Add(new Vector2(ub, vb));
                            t1.Add(k2); t1.Add(k2 + 1); t1.Add(k2 + 2); t1.Add(k2 + 1); t1.Add(k2 + 3); t1.Add(k2 + 2);
                        }
                    // mép vết rạch: thành bên có độ dày (thấy lớp mỡ, cơ ở mép cắt)
                    if (day > 0.0009f)
                    {
                        if (cT) for (int y = 0; y < sub; y++) Thanh(vs, uvs, t2, p[0, y + 1], p[0, y], q[0, y + 1], q[0, y]);
                        if (cP) for (int y = 0; y < sub; y++) Thanh(vs, uvs, t2, p[sub, y], p[sub, y + 1], q[sub, y], q[sub, y + 1]);
                        if (cD) for (int x = 0; x < sub; x++) Thanh(vs, uvs, t2, p[x, 0], p[x + 1, 0], q[x, 0], q[x + 1, 0]);
                        if (cTr) for (int x = 0; x < sub; x++) Thanh(vs, uvs, t2, p[x + 1, sub], p[x, sub], q[x + 1, sub], q[x, sub]);
                    }
                }
            // Vách vết mổ ở mép bản lề của mỗi vạt đang banh: thấy rõ các lớp da – mỡ – cân cơ, kèm lưỡi banh
            foreach (var vm in Mau.vat)
            {
                if (vm.lop != l || !vm.banh || vm.oVat.Count == 0) continue;
                int id = l.vatCuaO[vm.oVat[0]];
                if (id < 0 || !lv.gocVat.TryGetValue(id, out float g) || g <= 50f) continue;
                float vMin = 1f, vMax = 0f, uMin = 1f, uMax = 0f;
                foreach (var c in vm.oVat) { var tc = l.TamO(c % l.nu, c / l.nu); vMin = Mathf.Min(vMin, tc.y); vMax = Mathf.Max(vMax, tc.y); uMin = Mathf.Min(uMin, tc.x); uMax = Mathf.Max(uMax, tc.x); }
                float nuaO = l.CoO * 0.5f, nuaV = (l.v1 - l.v0) / l.nv * 0.5f;
                bool doc = vm.phiaBanLe <= 1;
                float co = doc ? (vm.phiaBanLe == 0 ? l.u0 + l.CoO * 0.25f : l.u1 - l.CoO * 0.25f) : (vm.phiaBanLe == 2 ? l.v0 + nuaV * 0.5f : l.v1 - nuaV * 0.5f);
                float a0 = doc ? vMin - nuaV : uMin - nuaO, a1 = doc ? vMax + nuaV : uMax + nuaO;
                const int n = 14;
                Vector3 truoc = default, truocD = default;
                for (int k = 0; k <= n; k++)
                {
                    float a = Mathf.Lerp(a0, a1, k / (float)n);
                    var tren = doc ? DiemCucBo(co, a, l.doSau) : DiemCucBo(a, co, l.doSau);
                    tren.y += 0.004f;
                    var duoi = tren - Vector3.up * (day + 0.006f);
                    if (k > 0)
                    {
                        bool dao = vm.phiaBanLe == 0 || vm.phiaBanLe == 3;
                        if (dao) Thanh(vs, uvs, t2, tren, truoc, duoi, truocD); else Thanh(vs, uvs, t2, truoc, tren, truocD, duoi);
                    }
                    truoc = tren; truocD = duoi;
                }
                if ((l.loai == LoaiLop.Da || l.loai == LoaiLop.Xuong) && !lv.luoiBanh.ContainsKey(id))
                {
                    float am = (a0 + a1) * 0.5f;
                    var p = doc ? DiemCucBo(co, am, l.doSau) : DiemCucBo(am, co, l.doSau);
                    float dai = Mathf.Abs(a1 - a0) * (doc ? L : W) * 0.45f;
                    var luoi = Vat(root, "LuoiBanh", Tru(), Mat(new Color(0.74f, 0.76f, 0.80f), V.Tron, 0.92f),
                        p + Vector3.up * 0.01f, doc ? new Vector3(0.006f, 0.05f, dai) : new Vector3(dai, 0.05f, 0.006f));
                    lv.luoiBanh[id] = luoi;
                }
            }

            lv.mesh.Clear();
            lv.mesh.indexFormat = vs.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            lv.mesh.SetVertices(vs);
            lv.mesh.SetUVs(0, uvs);
            lv.mesh.subMeshCount = 3;
            lv.mesh.SetTriangles(t0, 0);
            lv.mesh.SetTriangles(t1, 1);
            lv.mesh.SetTriangles(t2, 2);
            lv.mesh.RecalculateNormals();
            lv.mesh.RecalculateTangents();
            lv.mesh.RecalculateBounds();
            lv.tamGiacToiO = map.ToArray();
            if (lv.va == null) lv.va = Luu(new Mesh { name = "VaCham_" + l.id });
            lv.va.Clear();
            lv.va.indexFormat = lv.mesh.indexFormat;
            lv.va.SetVertices(vs);
            lv.va.SetTriangles(t0, 0);
            lv.col.sharedMesh = null;
            if (t0.Count > 0) lv.col.sharedMesh = lv.va;
        }

        static void Thanh(List<Vector3> vs, List<Vector2> uvs, List<int> t, Vector3 a, Vector3 b, Vector3 a2, Vector3 b2)
        {
            int k = vs.Count;
            vs.Add(a); vs.Add(b); vs.Add(a2); vs.Add(b2);
            float len = Vector3.Distance(a, b) * 30f;
            uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(len, 0)); uvs.Add(new Vector2(0, 0.3f)); uvs.Add(new Vector2(len, 0.3f));
            t.Add(k); t.Add(k + 1); t.Add(k + 2); t.Add(k + 1); t.Add(k + 3); t.Add(k + 2);
        }

        Vector3 XoayVat(Vector3 q, VatMo vm, float goc)
        {
            var l = vm.lop;
            Vector3 truc, tam;
            switch (vm.phiaBanLe)
            {
                case 0: tam = DiemCucBo(l.u0, (l.v0 + l.v1) * 0.5f, l.doSau); truc = Vector3.forward; break;
                case 1: tam = DiemCucBo(l.u1, (l.v0 + l.v1) * 0.5f, l.doSau); truc = Vector3.back; break;
                case 2: tam = DiemCucBo((l.u0 + l.u1) * 0.5f, l.v0, l.doSau); truc = Vector3.left; break;
                default: tam = DiemCucBo((l.u0 + l.u1) * 0.5f, l.v1, l.doSau); truc = Vector3.right; break;
            }
            // bản lề bám theo mặt cong: tâm quay lấy đúng độ cao mép tại vị trí điểm
            if (vm.phiaBanLe <= 1)
            {
                tam.z = q.z;
                tam.y = MatDa(vm.phiaBanLe == 0 ? l.u0 : l.u1, q.z / L + 0.5f) - l.doSau * Dz;
            }
            else
            {
                tam.x = q.x;
                tam.y = MatDa(q.x / W + 0.5f, vm.phiaBanLe == 2 ? l.v0 : l.v1) - l.doSau * Dz;
            }
            if (vm.banh)
            {
                // banh kéo mép vết mổ sang hai bên: mô dồn lại sát mép, cuộn nhẹ lên (không lật gập)
                float k = Mathf.Clamp01(goc / 100f);
                float giu = l.loai == LoaiLop.Xuong ? 0.42f : 0.16f;
                var lech = q - tam;
                var ngang = vm.phiaBanLe <= 1 ? new Vector3(lech.x, 0f, 0f) : new Vector3(0f, 0f, lech.z);
                var r2 = q - ngang * (1f - Mathf.Lerp(1f, giu, k));
                float xa = ngang.magnitude * Mathf.Lerp(1f, giu, k);
                r2.y = tam.y + lech.y * Mathf.Lerp(1f, giu, k) + k * Mathf.Min(0.012f, xa * 0.25f);
                return r2;
            }
            var r = Quaternion.AngleAxis(goc, truc) * (q - tam) + tam;
            r.y += Mathf.Sin(goc * Mathf.Deg2Rad) * 0.002f + (goc > 90f ? 0.003f : 0f);
            return r;
        }

        // ------------------------------------------------------------------ cơ quan

        static float Mu(float x) => x * x;

        /// <summary>Hình dạng cơ quan theo giải phẫu (hộp đơn vị; x: ngang, y: trước–sau, z: dọc thân về phía đầu).</summary>
        System.Func<Vector3, Vector3> HinhDang(CoQuanMo cq, int hat)
        {
            bool nguoi = Mau.loai == LoaiMau.Nguoi;
            float trong = cq.tam.x < 0.5f ? 1f : -1f; // hướng vào đường giữa theo trục x
            switch (cq.id)
            {
                case "than_phai":
                case "than_trai":
                    return d =>
                    {
                        var p = d * 0.5f;
                        float lom = Mathf.Max(0f, p.x * trong) * 2f;
                        p.x -= trong * 0.16f * lom * Mathf.Exp(-p.z * p.z / 0.03f); // rốn thận
                        return p + p.normalized * 0.015f * BanMoLuoi.Nhieu3(p, 6f, hat);
                    };
                case "lach":
                    return d => { var p = d * 0.5f; p.y *= 0.55f; p.x -= trong * 0.06f * Mathf.Exp(-p.z * p.z / 0.02f) * Mathf.Max(0, p.x * trong) * 4f; return p; };
                case "bang_quang":
                    return d => { var p = d * 0.5f; if (p.y > 0) p.y *= 0.7f; if (!nguoi) p.x *= 1f - 0.25f * Mathf.Exp(-p.x * p.x / 0.004f); return p + d * 0.01f * BanMoLuoi.Nhieu3(d, 3f, hat); };
                case "co_hoanh":
                    return d => { var p = d * 0.5f; p.y = p.y * 0.12f + 0.25f * (1f - 4f * (p.x * p.x + p.z * p.z * 0.5f)); return p; };
                case "mac_noi":
                    return d =>
                    {
                        var p = d * 0.5f; p.y *= 0.18f;
                        p.y += 0.12f * BanMoLuoi.Nhieu3(new Vector3(p.x, 0, p.z), 7f, hat);
                        p.z += 0.03f * Mathf.Sin(p.x * 20f) * (0.5f - p.z);
                        return p;
                    };
                case "tui_mat":
                    return d =>
                    {
                        var p = d * 0.5f;
                        float k = Mathf.Lerp(1.0f, 0.55f, Mathf.InverseLerp(-0.5f, 0.5f, p.z));
                        p.x *= k; p.y *= k; return p;
                    };
                case "phoi_phai":
                case "phoi_trai":
                    if (!nguoi)
                        return d => { var p = d * 0.5f; p.z *= 1f + 0.15f * Mathf.Max(0, -d.z); p.x *= Mathf.Lerp(0.8f, 1f, Mathf.InverseLerp(0.5f, -0.3f, p.z)); return p; };
                    return d =>
                    {
                        var p = d * 0.5f;
                        // đỉnh phổi nhọn lên trên, đáy lõm ôm vòm hoành, mặt trung thất phẳng, khe chếch chia thùy
                        float k = Mathf.Lerp(1f, 0.38f, Mathf.SmoothStep(0f, 1f, (p.z + 0.05f) / 0.55f));
                        p.x *= k; p.y *= k;
                        if (p.z < -0.25f) p.z += 0.18f * (1f - Mathf.Clamp01((p.x * p.x + p.y * p.y) * 6f));
                        if (p.x * trong > 0f) p.x *= 0.42f;
                        if (cq.id == "phoi_trai" && p.y > 0f && p.z < 0.05f) p.x -= trong * 0.08f * Mathf.Exp(-Mu(p.z + 0.15f) / 0.02f); // khuyết tim
                        float khe = p.z - (0.08f - 0.7f * p.y);
                        p -= p.normalized * 0.035f * Mathf.Exp(-khe * khe / 0.0012f);
                        return p;
                    };
                case "gan":
                    return d =>
                    {
                        var p = d * 0.5f;
                        // thùy phải dày, thùy trái mỏng dần; mặt tạng phẳng, bờ dưới sắc; dây chằng liềm
                        float tp = Mathf.InverseLerp(0.5f, -0.5f, p.x);
                        p.y *= Mathf.Lerp(0.32f, 1.0f, tp);
                        p.z *= Mathf.Lerp(0.55f, 1.0f, tp);
                        if (p.y < 0f) p.y *= 0.45f;
                        p.y *= Mathf.Lerp(0.55f, 1f, Mathf.SmoothStep(0f, 1f, (p.z + 0.5f) / 0.6f));
                        if (p.y > 0f) p.y -= 0.03f * Mathf.Exp(-Mu(p.x + 0.02f) / 0.002f);
                        return p + d * 0.012f * BanMoLuoi.Nhieu3(p, 5f, hat);
                    };
                default:
                    return d => d * 0.5f + d * 0.02f * BanMoLuoi.Nhieu3(d, 3f, hat);
            }
        }

        void DungCoQuan(CoQuanMo cq)
        {
            var g = new GameObject("CQ_" + cq.id) { layer = Layer };
            g.transform.SetParent(root, false);
            var cv = new CoQuanVe { cq = cq, tr = g.transform, mauGoc = cq.mau };
            cv.viTriGoc = DiemCucBo(cq.tam);
            g.transform.localPosition = cv.viTriGoc;
            bool nguoi = Mau.loai == LoaiMau.Nguoi;
            Vector3 kt = new Vector3(cq.kichThuoc.x * W, cq.kichThuoc.z * Dz, cq.kichThuoc.y * L);
            int hat = cq.id.GetHashCode() & 255;

            V van = cq.he == "Hô hấp" ? V.Phoi : cq.he == "Dự trữ" ? V.Mo : V.Than;
            if (cq.id == "gan" || cq.id == "lach") van = V.Gan;
            if (cq.id == "mac_noi") van = V.Mo;
            if (cq.id == "co_hoanh") van = V.Co;
            if (cq.id == "bang_quang" || cq.id == "tui_mat") van = V.Mang;
            float bong = cq.id == "gan" || cq.id == "lach" ? 0.82f : cq.he == "Hô hấp" ? 0.78f : 0.72f;
            var mat = Mat(cq.mau, van, bong, cq.id == "gan" ? 3f : 2f, 1f);

            if (cq.hinh == HinhCoQuan.Tim) DungTim(cv, kt, nguoi);
            else if (cq.hinh == HinhCoQuan.Ong) DungOng(cv, kt, nguoi);
            else if (cq.hinh == HinhCoQuan.ThuyGan && !nguoi)
            {
                // gan ếch 3 thùy dẹt, bờ tròn
                for (int k = 0; k < 3; k++)
                {
                    float x = (k - 1) * kt.x * 0.34f;
                    int kk = k;
                    var pk = Vat(g.transform, "Thuy" + k, KhoiHinh("ThuyGan" + k, d =>
                    {
                        var p = d * 0.5f; if (p.y < 0) p.y *= 0.4f;
                        // thùy dày ở gốc (phía đầu), thuôn và mỏng mép về phía đuôi
                        float t = Mathf.InverseLerp(0.5f, -0.5f, p.z);
                        p.x *= Mathf.Lerp(1f, 0.55f, t * t);
                        p.y *= Mathf.Lerp(1f, 0.45f, t);
                        return p + d * 0.02f * BanMoLuoi.Nhieu3(p, 5f, hat + kk);
                    }), mat, new Vector3(x, (k == 1 ? 0.15f : 0f) * kt.y, k == 1 ? kt.z * 0.08f : -kt.z * 0.05f), new Vector3(kt.x * 0.44f, kt.y, kt.z * (k == 1 ? 0.95f : 1.05f)), Quaternion.Euler(0f, (k - 1) * -12f, 0f));
                    cv.phan.Add(pk); cv.tiLe.Add(pk.localScale);
                }
            }
            else if (cq.id == "the_mo_phai" || cq.id == "the_mo_trai")
            {
                // thể mỡ ếch: chùm ngón vàng
                for (int k = 0; k < 5; k++)
                {
                    float a = (k - 2) * 14f;
                    var pk = Vat(g.transform, "Ngon" + k, KhoiHinh("TheMo", d => { var p = d * 0.5f; p.x *= 0.55f; p.y *= 0.5f; return p + d * 0.03f * BanMoLuoi.Nhieu3(d, 5f, 17); }, 20, 14), mat,
                        Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, kt.z * 0.18f), new Vector3(kt.x * 0.9f, kt.y * 1.1f, kt.z * (0.8f + 0.1f * (k % 2))), Quaternion.Euler(0f, a, 0f));
                    cv.phan.Add(pk); cv.tiLe.Add(pk.localScale);
                }
            }
            else if (cq.id == "tuy")
            {
                var duong = BanMoLuoi.Muot(new List<Vector3> { new Vector3(-0.5f * kt.x, 0f, -0.1f * kt.z), new Vector3(-0.1f * kt.x, 0.1f * kt.y, 0.2f * kt.z), new Vector3(0.5f * kt.x, 0f, 0.3f * kt.z) }, 16);
                var ong = new OngQuet("Tuy", duong, 14);
                float r0 = kt.y * 0.55f;
                ong.CapNhat(t => r0 * Mathf.Lerp(1.3f, 0.6f, t) * (1f + 0.12f * Mathf.Sin(t * 70f)), true);
                Luu(ong.mesh);
                var pk = Vat(g.transform, "Tuy", ong.mesh, mat, Vector3.zero, Vector3.one);
                cv.phan.Add(pk); cv.tiLe.Add(Vector3.one);
            }
            else
            {
                var p0 = Vat(g.transform, "Than", KhoiHinh("CQ_" + cq.id, HinhDang(cq, hat)), mat, Vector3.zero, kt);
                cv.phan.Add(p0); cv.tiLe.Add(kt);
            }

            foreach (Transform c in g.transform)
            {
                var mf = c.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var bc = c.gameObject.AddComponent<BoxCollider>();
                bc.center = mf.sharedMesh.bounds.center; bc.size = mf.sharedMesh.bounds.size * 0.85f;
                var nh = c.gameObject.AddComponent<BanMoNhan>(); nh.loai = BanMoNhan.Loai.CoQuan; nh.id = cq.id;
            }
            cv.vetRach = Vat(g.transform, "VetRach", KhoiHinh("VetRach", d => { var p = d * 0.5f; p.y *= 0.4f; return p; }, 16, 10), Mat(new Color(0.32f, 0.02f, 0.04f), V.Tron, 0.92f),
                new Vector3(0f, kt.y * 0.48f, 0f), new Vector3(Mathf.Max(kt.x * 0.45f, 0.01f), 0.004f, 0.004f));
            cv.vetRach.gameObject.SetActive(false);
            coQuanVe.Add(cv);
        }

        void DungTim(CoQuanVe cv, Vector3 kt, bool nguoi)
        {
            var g = cv.tr;
            var matThat = Mat(cv.cq.mau, V.Tim, 0.8f, 2f, 0.9f);
            cv.matThat = matThat;
            var matNhi = Mat(nguoi ? new Color(0.50f, 0.10f, 0.14f) : new Color(0.42f, 0.06f, 0.14f), V.Tim, 0.85f, 2f, 0.7f);
            var matMach = Mat(new Color(0.88f, 0.74f, 0.70f), V.Mang, 0.75f, 2f, 0.5f);
            if (nguoi)
            {
                // khối thất hình nón, mỏm hướng xuống dưới – sang trái; rãnh liên thất trước có mỡ và mạch vành
                var huong = Quaternion.FromToRotation(Vector3.back, new Vector3(0.55f, -0.15f, -0.82f).normalized);
                cv.that = Vat(g, "TamThat", KhoiHinh("ThatNguoi", d =>
                {
                    var p = d * 0.5f;
                    float k = Mathf.Lerp(0.22f, 1f, Mathf.SmoothStep(0f, 1f, (p.z + 0.5f) / 0.75f));
                    p.x *= k; p.y *= k;
                    if (p.y > 0f) p.y -= 0.035f * Mathf.Exp(-Mu(p.x - 0.06f) / 0.003f);
                    return p;
                }, 48, 32), matThat, new Vector3(0f, -kt.y * 0.05f, -kt.z * 0.1f), new Vector3(kt.x * 0.9f, kt.y * 0.85f, kt.z * 1.05f), huong);
                cv.nhi1 = Vat(g, "TieuNhiPhai", KhoiHinh("NhiPhai", d => d * 0.5f + d * 0.03f * BanMoLuoi.Nhieu3(d, 3f, 7)), matNhi, new Vector3(-kt.x * 0.40f, kt.y * 0.05f, kt.z * 0.22f), new Vector3(kt.x * 0.38f, kt.y * 0.55f, kt.z * 0.42f));
                cv.nhi2 = Vat(g, "TieuNhiTrai", KhoiHinh("TaiTrai", d => { var p = d * 0.5f; p.z *= 1f + 0.4f * Mathf.Max(0, d.x); return p; }), matNhi, new Vector3(kt.x * 0.18f, kt.y * 0.25f, kt.z * 0.38f), new Vector3(kt.x * 0.26f, kt.y * 0.28f, kt.z * 0.22f));
                var dmp = new OngQuet("DMPhoi", BanMoLuoi.Muot(new List<Vector3> { new Vector3(0.05f * kt.x, 0.2f * kt.y, 0.25f * kt.z), new Vector3(0.12f * kt.x, 0.25f * kt.y, 0.55f * kt.z), new Vector3(0.24f * kt.x, 0.1f * kt.y, 0.72f * kt.z) }, 10), 16);
                dmp.CapNhat(t => 0.013f, true); Luu(dmp.mesh);
                Vat(g, "DMPhoi", dmp.mesh, Mat(new Color(0.62f, 0.34f, 0.44f), V.Mang, 0.8f, 2f, 0.4f), Vector3.zero, Vector3.one);
                var vanh = new OngQuet("Vanh", BanMoLuoi.Muot(new List<Vector3> { new Vector3(0.05f * kt.x, 0.38f * kt.y, 0.25f * kt.z), new Vector3(0.12f * kt.x, 0.40f * kt.y, -0.05f * kt.z), new Vector3(0.24f * kt.x, 0.25f * kt.y, -0.42f * kt.z) }, 10), 8);
                vanh.CapNhat(t => Mathf.Lerp(0.0018f, 0.0009f, t), true); Luu(vanh.mesh);
                Vat(g, "MachVanh", vanh.mesh, Mat(new Color(0.75f, 0.12f, 0.12f), V.Tron, 0.85f), Vector3.zero, Vector3.one);
                Vat(g, "MoVanh", KhoiHinh("MoVanh", d => d * 0.5f + d * 0.05f * BanMoLuoi.Nhieu3(d, 6f, 3)), Mat(new Color(0.96f, 0.82f, 0.40f), V.Mo, 0.75f, 3f, 1f),
                    new Vector3(-0.05f * kt.x, 0.30f * kt.y, 0.18f * kt.z), new Vector3(kt.x * 0.55f, kt.y * 0.18f, kt.z * 0.14f));
            }
            else
            {
                // tim ếch: 1 tâm thất hình nón mỏm hướng về đuôi, 2 tâm nhĩ sẫm thành mỏng, hành động mạch cong sang phải
                cv.that = Vat(g, "TamThat", KhoiHinh("ThatEch", d =>
                {
                    var p = d * 0.5f;
                    float k = Mathf.Lerp(0.25f, 1f, Mathf.SmoothStep(0f, 1f, (p.z + 0.5f) / 0.8f));
                    p.x *= k; p.y *= k * 0.9f;
                    return p;
                }, 40, 28), matThat, new Vector3(0f, 0f, -kt.z * 0.18f), new Vector3(kt.x * 0.78f, kt.y * 0.85f, kt.z * 0.95f));
                cv.nhi1 = Vat(g, "TamNhiPhai", KhoiHinh("NhiEch", d => d * 0.5f + d * 0.04f * BanMoLuoi.Nhieu3(d, 3f, 9)), matNhi, new Vector3(-kt.x * 0.22f, kt.y * 0.02f, kt.z * 0.30f), new Vector3(kt.x * 0.50f, kt.y * 0.62f, kt.z * 0.42f));
                cv.nhi2 = Vat(g, "TamNhiTrai", KhoiHinh("NhiEch2", d => d * 0.5f + d * 0.04f * BanMoLuoi.Nhieu3(d, 3f, 13)), matNhi, new Vector3(kt.x * 0.22f, kt.y * 0.02f, kt.z * 0.32f), new Vector3(kt.x * 0.44f, kt.y * 0.55f, kt.z * 0.38f));
                var hanh = new OngQuet("HanhDongMach", BanMoLuoi.Muot(new List<Vector3> { new Vector3(0.02f * kt.x, 0.35f * kt.y, 0.05f * kt.z), new Vector3(-0.12f * kt.x, 0.45f * kt.y, 0.35f * kt.z), new Vector3(-0.02f * kt.x, 0.40f * kt.y, 0.62f * kt.z) }, 10), 14);
                float rh = kt.x * 0.11f;
                hanh.CapNhat(t => rh * Mathf.Lerp(1f, 0.8f, t), true); Luu(hanh.mesh);
                Vat(g, "HanhDongMach", hanh.mesh, matMach, Vector3.zero, Vector3.one);
            }
            cv.sNhi1 = cv.nhi1.localScale; cv.sNhi2 = cv.nhi2.localScale; cv.sThat = cv.that.localScale;
        }

        void DungOng(CoQuanVe cv, Vector3 kt, bool nguoi)
        {
            var cq = cv.cq;
            List<Vector3> diem;
            float r;
            System.Func<float, float> hinh;
            if (cq.id == "ruot_non")
            {
                r = nguoi ? 0.0125f : 0.0019f * kThat;
                diem = CuonRuot(cq, r);
                // nối liền: bắt đầu ở môn vị dạ dày, kết thúc ở đầu ruột già (không có đầu ống hở)
                var dd = Mau.CQ("da_day"); var rg = Mau.CQ(nguoi ? "dai_trang" : "ruot_gia");
                if (dd != null && dd.duongOng != null) { diem.Insert(0, DiemCucBo(dd.duongOng[dd.duongOng.Length - 1])); }
                if (rg != null && rg.duongOng != null) diem.Add(DiemCucBo(rg.duongOng[0]));
                diem = BanMoLuoi.Muot(diem, 1);
                hinh = t => 1f;
            }
            else if (cq.id == "dai_trang")
            {
                diem = BanMoLuoi.Muot(ChuyenDiem(cq.duongOng), 14);
                r = 0.024f;
                hinh = t => 1f + 0.16f * Mathf.Pow(Mathf.Abs(Mathf.Sin(t * 46f * Mathf.PI)), 0.6f) * (t > 0.92f ? 0f : 1f); // túi phình (haustra)
            }
            else if (cq.id == "da_day")
            {
                diem = BanMoLuoi.Muot(ChuyenDiem(cq.duongOng), 16);
                r = nguoi ? 0.048f : 0.0045f * kThat;
                hinh = t => Mathf.Lerp(1f, 0.32f, Mathf.Pow(t, 1.4f)) * (t < 0.12f ? Mathf.Lerp(0.7f, 1f, t / 0.12f) : 1f); // đáy vị phình, môn vị hẹp
            }
            else
            {
                diem = BanMoLuoi.Muot(ChuyenDiem(cq.duongOng), 14);
                r = nguoi ? 0.02f : 0.004f * kThat;
                hinh = t => Mathf.Lerp(1f, 0.85f, t);
            }
            for (int k = 0; k < diem.Count; k++) diem[k] -= cv.viTriGoc;
            var ong = new OngQuet("Ong_" + cq.id, diem, nguoi ? 18 : 14);
            cv.ong = ong; cv.banKinhOng = r; cv.hinhOng = hinh;
            ong.CapNhat(t => r * hinh(t), true);
            Luu(ong.mesh);
            var mau = cq.id == "da_day" ? new Color(0.90f, 0.70f, 0.64f) : cq.mau;
            var vat = Vat(cv.tr, "Ong", ong.mesh, Mat(mau, V.Ruot, 0.82f, cq.id == "ruot_non" ? 12f : 5f, 0.9f), Vector3.zero, Vector3.one);
            cv.phan.Add(vat); cv.tiLe.Add(Vector3.one);
        }

        List<Vector3> ChuyenDiem(Vector3[] uvd)
        {
            var l = new List<Vector3>();
            foreach (var p in uvd) l.Add(DiemCucBo(p));
            return l;
        }

        /// <summary>Đường cuộn ruột non: các quai uốn lượn xếp chồng trong vùng của cơ quan.</summary>
        List<Vector3> CuonRuot(CoQuanMo cq, float r)
        {
            // các quai ruột chạy qua lại thành hàng cách nhau ~2,3 bán kính, uốn lượn nhẹ, hai lớp trên – dưới so le
            var kq = new List<Vector3>();
            float u0 = cq.tam.x - cq.kichThuoc.x * 0.5f, u1 = cq.tam.x + cq.kichThuoc.x * 0.5f;
            float v0 = cq.tam.y - cq.kichThuoc.y * 0.5f, v1 = cq.tam.y + cq.kichThuoc.y * 0.5f;
            float caoVung = (v1 - v0) * L;
            int soHang = Mathf.Clamp(Mathf.FloorToInt(caoVung / (2.3f * r)), 3, 12);
            const int moiHang = 24;
            for (int h = 0; h < soHang; h++)
            {
                float v = Mathf.Lerp(v1, v0, (h + 0.5f) / soHang);
                bool lopTren = h % 4 == 1 || h % 4 == 2;
                for (int k = 0; k <= moiHang; k++)
                {
                    float s = k / (float)moiHang;
                    float bien = Mathf.Sin(s * Mathf.PI);   // quai thu hẹp ở hai đầu hàng để nối êm sang hàng kế
                    float u = h % 2 == 0 ? Mathf.Lerp(u0, u1, s) : Mathf.Lerp(u1, u0, s);
                    float dv = (v1 - v0) / soHang * 0.30f * Mathf.Sin(s * Mathf.PI * 2.5f + h * 1.3f) * bien;
                    var p = DiemCucBo(u, v + dv, cq.tam.z);
                    p.y += (lopTren ? 1.1f : -0.2f) * r * bien;
                    kq.Add(p);
                }
                if (h < soHang - 1)
                {
                    // quai quay đầu hình bán nguyệt sang hàng kế (không gấp góc)
                    float uCuoi = h % 2 == 0 ? u1 : u0, huong = h % 2 == 0 ? 1f : -1f;
                    float buoc = (v1 - v0) / soHang, vTam = v - buoc * 0.5f;
                    float banKinhU = buoc * 0.5f * L / W;
                    for (int k = 1; k < 6; k++)
                    {
                        float g = Mathf.PI * k / 6f;
                        kq.Add(DiemCucBo(uCuoi + huong * banKinhU * Mathf.Sin(g), vTam + buoc * 0.5f * Mathf.Cos(g), cq.tam.z));
                    }
                }
            }
            return BanMoLuoi.Muot(kq, 3);
        }

        void DungMach(MachMau m)
        {
            var mv = new MachVe { m = m };
            machVe.Add(mv);
            if (m.duong == null) return;
            bool nguoi = Mau.loai == LoaiMau.Nguoi;
            var mat = Mat(m.dongMach ? new Color(0.78f, 0.10f, 0.12f) : new Color(0.28f, 0.20f, 0.42f), V.Mang, 0.85f, 4f, 0.5f);
            float r = Mathf.Clamp(m.banKinhMm, 0.4f, 14f) * 0.001f * (nguoi ? 1f : kThat);
            var diem = new List<Vector3>();
            foreach (var p in m.duong) diem.Add(DiemMach(m, p));
            var ong = new OngQuet("Mach_" + m.id, BanMoLuoi.Muot(diem, 10), 12);
            ong.CapNhat(t => r, true); Luu(ong.mesh);
            mv.hinh = Vat(root, "MachHinh_" + m.id, ong.mesh, mat, Vector3.zero, Vector3.one).GetComponent<MeshRenderer>();
            for (int s = 1; s < m.duong.Length; s++)
            {
                Vector3 a = DiemMach(m, m.duong[s - 1]), b = DiemMach(m, m.duong[s]);
                var t = DoanVaCham(root, $"Mach_{m.id}_{s}", a, b, Mathf.Max(r, 0.004f));
                var nh = t.gameObject.AddComponent<BanMoNhan>(); nh.loai = BanMoNhan.Loai.Mach; nh.id = m.id;
                mv.doan.Add(t); mv.diem.Add((m.duong[s - 1] + m.duong[s]) * 0.5f);
            }
            mv.kep = Vat(root, "Kep_" + m.id, KhoiHinh("Kep", d => { var p = d * 0.5f; p.x *= 0.3f; return p; }, 16, 10), Mat(new Color(0.78f, 0.80f, 0.84f), V.Tron, 0.95f), Vector3.zero,
                new Vector3(0.012f, 0.004f, 0.034f) * (nguoi ? 1.4f : 0.6f));
            mv.kep.gameObject.SetActive(false);
        }

        Transform DoanVaCham(Transform cha, string ten, Vector3 a, Vector3 b, float r)
        {
            var go = new GameObject(ten) { layer = Layer };
            go.transform.SetParent(cha, false);
            var d = b - a;
            go.transform.localPosition = (a + b) * 0.5f;
            go.transform.localRotation = d.sqrMagnitude > 1e-10f ? Quaternion.FromToRotation(Vector3.up, d.normalized) : Quaternion.identity;
            var cc = go.AddComponent<CapsuleCollider>();
            cc.direction = 1; cc.radius = r; cc.height = d.magnitude + 2f * r;
            return go.transform;
        }

        Vector3 DiemMach(MachMau m, Vector3 uvd)
        {
            if (!string.IsNullOrEmpty(m.lopId))
            {
                var l = Mau.Lop(m.lopId);
                if (l != null)
                {
                    float day = 0f;
                    foreach (var lv in lopVe) if (lv.lop == l) day = lv.day;
                    return DiemCucBo(uvd.x, uvd.y, l.doSau) - Vector3.up * (day * 0.5f);
                }
            }
            return DiemCucBo(uvd);
        }

        void DungThanKinh(DayThanKinh tk)
        {
            bool nguoi = Mau.loai == LoaiMau.Nguoi;
            var mat = Mat(new Color(0.97f, 0.95f, 0.86f), V.Mang, 0.75f, 8f, 0.5f);
            float r = nguoi ? 0.0016f : 0.0005f * kThat;
            var diem = new List<Vector3>();
            foreach (var p in tk.duong) diem.Add(DiemCucBo(p));
            var ong = new OngQuet("TK_" + tk.id, BanMoLuoi.Muot(diem, 10), 8);
            ong.CapNhat(t => r, true); Luu(ong.mesh);
            Vat(root, "TKHinh_" + tk.id, ong.mesh, mat, Vector3.zero, Vector3.one);
            for (int s = 1; s < tk.duong.Length; s++)
            {
                var t = DoanVaCham(root, $"TK_{tk.id}_{s}", DiemCucBo(tk.duong[s - 1]), DiemCucBo(tk.duong[s]), Mathf.Max(r, 0.004f));
                var nh = t.gameObject.AddComponent<BanMoNhan>(); nh.loai = BanMoNhan.Loai.ThanKinh; nh.id = tk.id;
            }
        }

        /// <summary>Tham số t (0 = phía trung ương .. 1 = cơ quan đích) của điểm gần nhất trên dây thần kinh.</summary>
        public float ThamSoThanKinh(DayThanKinh tk, Vector3 world)
        {
            var p = root.InverseTransformPoint(world);
            float tot = 0f, dmin = float.MaxValue;
            int n = tk.duong.Length - 1;
            for (int s = 0; s < n; s++)
            {
                Vector3 a = DiemCucBo(tk.duong[s]), b = DiemCucBo(tk.duong[s + 1]);
                var ab = b - a;
                float w = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-9f, ab.sqrMagnitude));
                float d = Vector3.Distance(p, a + ab * w);
                if (d < dmin) { dmin = d; tot = (s + w) / n; }
            }
            return tot;
        }

        void DungMau()
        {
            var m = new Mesh { name = "HoMau" };
            var vs = new List<Vector3>(); var uv = new List<Vector2>(); var ts = new List<int>();
            const int N = 32;
            for (int j = 0; j <= N; j++) for (int i = 0; i <= N; i++) { vs.Add(new Vector3(((i / (float)N) - 0.5f) * W * 0.96f, 0f, ((j / (float)N) - 0.5f) * L * 0.96f)); uv.Add(new Vector2(i / (float)N * 3f, j / (float)N * 3f)); }
            for (int j = 0; j < N; j++) for (int i = 0; i < N; i++) { int a = i + j * (N + 1), b = a + N + 1; ts.Add(a); ts.Add(b); ts.Add(a + 1); ts.Add(a + 1); ts.Add(b); ts.Add(b + 1); }
            m.SetVertices(vs); m.SetUVs(0, uv); m.SetTriangles(ts, 0); m.RecalculateNormals(); m.RecalculateTangents();
            matHoMau = Mat(new Color(0.36f, 0.01f, 0.03f), V.Mang, 0.95f, 2f, 0.15f);
            hoMau = Vat(root, "MauDong", Luu(m), matHoMau, Vector3.zero, Vector3.one);
            hoMau.gameObject.SetActive(false);
            var mg = Mat(new Color(0.58f, 0.02f, 0.04f), V.Tron, 0.95f);
            float sg = Mau.loai == LoaiMau.Ech ? 0.0022f : 0.005f;
            for (int k = 0; k < giot.Length; k++)
            {
                giot[k] = Vat(root, "Giot" + k, Cau(), mg, Vector3.zero, Vector3.one * sg);
                giot[k].gameObject.SetActive(false);
                giotSong[k] = 0f;
            }
            if (Mau.loai == LoaiMau.Ech)
            {
                // đĩa kính đựng Ringer cho tim tách rời: đáy kính + lớp dịch
                dia = Vat(root, "DiaRinger", Tru(), Mat(new Color(0.86f, 0.92f, 0.95f), V.Tron, 0.97f), new Vector3(W * 1.30f, 0.0f, L * 0.70f), new Vector3(0.075f, 0.006f, 0.075f));
                Vat(dia, "DichRinger", Tru(), Mat(new Color(0.78f, 0.86f, 0.88f), V.Mang, 0.98f, 2f, 0.2f), new Vector3(0f, 0.55f, 0f), new Vector3(0.9f, 0.2f, 0.9f));
            }
        }

        // ------------------------------------------------------------------ cập nhật mỗi khung

        public void CapNhat(float dt)
        {
            if (Mau == null) return;
            foreach (var lv in lopVe) VeLop(lv, dt);
            var s = Mau.sinhLy;
            bool nguoi = Mau.loai == LoaiMau.Nguoi;
            float tron = 1f - Mathf.Exp(-8f * Mathf.Max(dt, 0.0001f));

            if (phienBanCoQuanDaVe != Mau.phienBanCoQuan)
            {
                phienBanCoQuanDaVe = Mau.phienBanCoQuan;
                foreach (var cv in coQuanVe)
                {
                    bool hien = !cv.cq.catBo;
                    if (cv.tr.gameObject.activeSelf != hien) cv.tr.gameObject.SetActive(hien);
                    cv.vetRach.gameObject.SetActive(cv.cq.rach);
                }
            }

            foreach (var cv in coQuanVe)
            {
                if (cv.cq.catBo) continue;
                var cq = cv.cq;
                Vector3 dich = cv.viTriGoc;
                Quaternion xoay = Quaternion.identity;
                Vector3 coGian = Vector3.one;
                if (cq.ven && cq.id == "mac_noi")
                {
                    // mạc nối lớn được lật lên phía đầu, vắt lên đại tràng ngang và dạ dày
                    dich += new Vector3(0f, Dz * 0.18f, cq.kichThuoc.y * L * 0.55f);
                    coGian = new Vector3(1f, 1.6f, 0.32f);
                }
                else if (cq.ven)
                {
                    float huong = cq.tam.x < 0.5f ? -1f : 1f;
                    dich += new Vector3(huong * W * 0.30f, Dz * 0.30f, 0f);
                    xoay = Quaternion.Euler(0f, 0f, -huong * 25f);
                }
                if (cq.id == "mac_noi") cv.tr.localScale = Vector3.Lerp(cv.tr.localScale, coGian, tron);
                if (cq.tachRoi && dia != null) dich = dia.localPosition + new Vector3(0f, 0.012f, 0f);
                cv.tr.localPosition = Vector3.Lerp(cv.tr.localPosition, dich, tron);
                cv.tr.localRotation = Quaternion.Slerp(cv.tr.localRotation, xoay, tron);

                switch (cq.chuyenDong)
                {
                    case ChuyenDong.Tim:
                        {
                            float a = s.nhiCo, v = s.thatCo;
                            cv.nhi1.localScale = cv.sNhi1 * (1f - 0.20f * a);
                            cv.nhi2.localScale = cv.sNhi2 * (1f - 0.20f * a);
                            // tâm thu: thất co ngắn trục dọc; ếch: thành mỏng, tống máu nên tái đi
                            cv.that.localScale = Vector3.Scale(cv.sThat, new Vector3(1f - 0.14f * v, 1f - 0.10f * v, 1f - 0.17f * v));
                            var mauThat = !nguoi ? Color.Lerp(cv.mauGoc, new Color(0.86f, 0.55f, 0.52f), 0.55f * v) : cv.mauGoc;
                            if (!nguoi && s.theTichMau < s.theTichMau0 * 0.4f) mauThat = Color.Lerp(mauThat, new Color(0.8f, 0.6f, 0.55f), 0.5f);
                            cv.matThat.SetColor("_BaseColor", mauThat);
                            break;
                        }
                    case ChuyenDong.Phoi:
                        {
                            float p = s.phoi;
                            float k = !nguoi ? Mathf.Lerp(0.45f, 1.15f, p) : Mathf.Lerp(0.88f, 1.08f, p);
                            for (int i = 0; i < cv.phan.Count; i++)
                                cv.phan[i].localScale = new Vector3(cv.tiLe[i].x * k, cv.tiLe[i].y * k, cv.tiLe[i].z * Mathf.Lerp(0.85f, 1f, p));
                            if (!nguoi) cv.phan[0].GetComponent<Renderer>().sharedMaterial.SetColor("_BaseColor", Color.Lerp(new Color(0.60f, 0.30f, 0.33f), cq.mau, p));
                            break;
                        }
                    case ChuyenDong.NhuDong:
                        {
                            if (cv.ong == null) break;
                            float pha = s.phaNhuDong, bd = s.bienDoNhuDong, r = cv.banKinhOng, coGiat = cq.coGiat;
                            var hinh = cv.hinhOng;
                            float soSong = cq.id == "ruot_non" ? 9f : 3f;
                            // sóng co thắt chạy từ đầu ống về cuối (miệng → hậu môn): vòng co thắt, phía sau dãn nhẹ
                            cv.ong.CapNhat(t =>
                            {
                                float song = Mathf.Sin((t * soSong - pha) * Mathf.PI * 2f);
                                float co = 1f - 0.24f * bd * Mathf.Pow(Mathf.Max(0f, song), 4f) + 0.06f * bd * Mathf.Max(0f, -song) - 0.25f * coGiat;
                                return r * hinh(t) * co;
                            }, true);
                            break;
                        }
                    case ChuyenDong.CoHoanh:
                        cv.tr.localPosition = cv.viTriGoc + new Vector3(0f, 0f, -0.025f * s.coHoanhCo * L - 0.01f * s.phoi * L);
                        break;
                }
            }

            // Mạch: đoạn nằm trong lớp đã mở / cơ quan đã bỏ thì ẩn; kẹp hiện ở chỗ kẹp
            foreach (var mv in machVe)
            {
                var m = mv.m;
                if (m.duong == null) continue;
                bool hienMach = true;
                if (!string.IsNullOrEmpty(m.coQuanNuoi))
                {
                    var cq = Mau.CQ(m.coQuanNuoi);
                    if (cq != null && (cq.catBo || cq.tachRoi) && m.dut) hienMach = false;
                }
                if (!string.IsNullOrEmpty(m.lopId))
                {
                    var l = Mau.Lop(m.lopId);
                    foreach (var p in mv.diem)
                        if (l != null && l.O(p.x, p.y, out int i, out int j) && l.o[l.Idx(i, j)] != TrangThaiO.Lien) { hienMach = false; break; }
                }
                if (mv.hinh != null && mv.hinh.enabled != hienMach) mv.hinh.enabled = hienMach;
                foreach (var d in mv.doan) if (d.gameObject.activeSelf != hienMach) d.gameObject.SetActive(hienMach);
                if (mv.kep != null)
                {
                    bool hk = m.kep;
                    if (mv.kep.gameObject.activeSelf != hk) mv.kep.gameObject.SetActive(hk);
                    if (hk) mv.kep.localPosition = DiemMach(m, m.dut ? m.diemDut : m.duong[m.duong.Length / 2]) + Vector3.up * 0.004f;
                }
            }

            for (int c = 0; c < 4; c++)
            {
                if (chiTr[c] == null) continue;
                float co = Mau.chiCo[c];
                chiTr[c].localRotation = chiGoc[c] * Quaternion.Euler(-30f * co, (c % 2 == 0 ? 1 : -1) * 28f * co, 0f);
                if (ghimChi[c] != null && ghimChi[c].gameObject.activeSelf != Mau.chiGhim[c]) ghimChi[c].gameObject.SetActive(Mau.chiGhim[c]);
            }
            if (hongEch != null)
            {
                float k = s.coTheTho ? 1f + 0.25f * Mathf.Max(0f, Mathf.Sin(s.phaTho * Mathf.PI * 2f)) : 1f;
                hongEch.localScale = new Vector3(hongScale.x, hongScale.y * k, hongScale.z);
            }

            // Máu, dịch đọng: mặt phẳng nâng dần trong khoang
            float tong = Mau.mauDong + Mau.dichTran;
            float sucChua = Mau.loai == LoaiMau.Ech ? 1.2f : 1800f;
            bool hienHo = tong > sucChua * 0.004f && KhoangDaHo();
            if (hoMau.gameObject.activeSelf != hienHo) hoMau.gameObject.SetActive(hienHo);
            if (hienHo)
            {
                float muc = Mathf.Clamp01(tong / sucChua);
                float day = DoSauDay(0.5f, 0.42f);
                float y = MatDa(0.5f, 0.42f) - Dz * (day - 0.005f - (day - 0.15f) * Mathf.Pow(muc, 0.6f));
                hoMau.localPosition = new Vector3(0f, y, 0f);
                var c = Mau.mauDong >= Mau.dichTran ? new Color(0.36f, 0.01f, 0.03f) : Color.Lerp(new Color(0.36f, 0.01f, 0.03f), Mau.mauDichTran, 0.6f);
                matHoMau.SetColor("_BaseColor", c);
            }

            foreach (var m in Mau.mach)
            {
                if (!m.dut || m.kep || m.dongCuc || m.luuLuongCat <= 0f) continue;
                if (m.id == "mm_da") continue;
                float ap = nguoi ? s.huyetAp / 85f : s.huyetAp / 30f;
                float tia = m.dongMach ? ap * s.thatCo : ap * 0.4f;
                if (tia < 0.05f || Random.value > Mathf.Clamp01(dt * (m.dongMach ? 70f : 25f) * tia)) continue;
                var cq = string.IsNullOrEmpty(m.coQuanNuoi) ? null : Mau.CQ(m.coQuanNuoi);
                var goc = cq != null && cq.tachRoi && dia != null ? dia.localPosition : DiemMach(m, m.diemDut);
                if (!DiemThayDuoc(m)) continue;
                int k = giotKe; giotKe = (giotKe + 1) % giot.Length;
                giot[k].gameObject.SetActive(true);
                giot[k].localPosition = goc;
                float v0 = (nguoi ? 0.55f : 0.18f) * (m.dongMach ? tia : 0.25f);
                giotVanToc[k] = new Vector3(Random.Range(-0.3f, 0.3f) * v0, v0, Random.Range(-0.3f, 0.3f) * v0);
                giotSong[k] = 1.2f;
            }
            for (int k = 0; k < giot.Length; k++)
            {
                if (giotSong[k] <= 0f) continue;
                giotSong[k] -= dt;
                giotVanToc[k] += Vector3.down * 2.5f * dt;
                giot[k].localPosition += giotVanToc[k] * dt;
                if (giotSong[k] <= 0f || giot[k].localPosition.y < -Dz) { giotSong[k] = 0f; giot[k].gameObject.SetActive(false); }
            }
        }

        bool DiemThayDuoc(MachMau m)
        {
            if (!string.IsNullOrEmpty(m.lopId))
            {
                var l = Mau.Lop(m.lopId);
                foreach (var t in Mau.lop) if (t.doSau < l.doSau - 0.0001f && t.O(m.diemDut.x, m.diemDut.y, out int i, out int j) && t.o[t.Idx(i, j)] == TrangThaiO.Lien) return false;
                return true;
            }
            return Mau.DiemLo(m.diemDut) || m.coQuanNuoi == "tim";
        }

        bool KhoangDaHo()
        {
            foreach (var l in Mau.lop) if (l.doSau > 0.02f && l.TiLeMo() > 0f) return true;
            return false;
        }

        public Bounds KhungNhin() => new Bounds(Goc + new Vector3(0f, MatDa(0.5f, 0.5f) * 0.5f, 0f), new Vector3(W * 1.3f, Dz, L * 1.3f));
    }
}
