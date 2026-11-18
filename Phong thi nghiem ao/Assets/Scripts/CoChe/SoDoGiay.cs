using System.Collections.Generic;
using Khkt.Demo;
using UnityEngine;
using UnityEngine.UI;

namespace Khkt.CoChe
{
    /// <summary>
    /// Sơ đồ cơ chế "trên giấy" (.giay.json) vẽ bằng một UI Graphic tự dựng lưới – bản Unity của veSoDo trong
    /// nguyen_mau/co_che_3d/ve_giay.js (cùng quy ước QUY_UOC_VE.md):
    ///   nét đơn/đôi/ba (nét đôi trong vòng: nét thứ hai ngắn, phía trong), nét "sau" vẽ trước và nét trước có khe che màu giấy,
    ///   nêm đặc / nêm gạch cho lập thể, mũi tên cong Bézier (một ngạnh = 1 e, hai ngạnh = 2 e), mũi tên phản ứng có chữ
    ///   trên/dưới (⇌ khi thuận nghịch), chấm gốc tự do, điện tích, nhãn A1…
    /// Chữ là các Text con (phông Be Vietnam Pro của GlassKit, đủ dấu tiếng Việt và chỉ số dưới Unicode CO₂Me).
    /// </summary>
    public class SoDoGiay : MaskableGraphic
    {
        public static readonly Color MauGiay = new Color32(0xff, 0xfd, 0xf7, 0xff);
        public static readonly Color MauMuc = new Color32(0x1b, 0x26, 0x21, 0xff);
        public static readonly Color MauMuiTen = new Color32(0xb3, 0x26, 0x1e, 0xff);
        const float KyTu = 0.17f;

        SoDoGiayDuLieu sd;
        readonly List<Text> chu = new List<Text>();
        float S = 34f;
        Vector2 goc;
        Rect vungCu;

        class N { public Vector2 P; public string T; public string Can = "giua"; public float X0, X1, Y0, Y1; public List<N> Ke = new List<N>(); }
        readonly Dictionary<string, N> nt = new Dictionary<string, N>();

        public SoDoGiayDuLieu DuLieu => sd;

        protected override void Awake()
        {
            base.Awake();
            useLegacyMeshGeneration = false;                       // Graphic gốc mặc định đi đường cũ; Image/Text đều tắt cờ này
        }

        /// <summary>Số đỉnh lưới của sơ đồ hiện tại (kiểm thử: nét, mũi tên phải được dựng).</summary>
        public int SoDinh() => soDoan * 4;

        public void Dat(SoDoGiayDuLieu d)
        {
            sd = d;
            ChuanHoa();
            DungChu();
            SetVerticesDirty();
        }

        static float NuaRong(string t) => string.IsNullOrEmpty(t) ? 0f : t.Length * 0.17f + 0.1f;

        void ChuanHoa()
        {
            nt.Clear();
            if (sd == null) return;
            foreach (var kv in sd.NutDs) nt[kv.Key] = new N { P = kv.Value.P, T = kv.Value.Nhan ?? "" };
            foreach (var l in sd.NetDs)
                if (nt.TryGetValue(l.A, out var a) && nt.TryGetValue(l.B, out var b)) { a.Ke.Add(b); b.Ke.Add(a); }
            foreach (var a in nt.Values)
            {
                if (a.T.Length > 1 && a.Ke.Count == 1) a.Can = a.Ke[0].P.x > a.P.x + 0.3f ? "phai" : "trai";
                float w = 2f * NuaRong(a.T);
                if (a.Can == "trai") { a.X0 = a.P.x - KyTu; a.X1 = a.X0 + w; }
                else if (a.Can == "phai") { a.X1 = a.P.x + KyTu; a.X0 = a.X1 - w; }
                else { a.X0 = a.P.x - w / 2; a.X1 = a.P.x + w / 2; }
                a.Y0 = a.P.y - 0.24f; a.Y1 = a.P.y + 0.24f;
            }
        }

        // toạ độ sơ đồ → điểm cục bộ của RectTransform (gốc trái-trên của khung)
        Vector2 X(Vector2 p) => new Vector2(goc.x + (p.x - sd.X0) * S, goc.y - (sd.Y1 - p.y) * S);

        void TinhTiLe()
        {
            var r = rectTransform.rect;
            float w = Mathf.Max(1f, sd.X1 - sd.X0), h = Mathf.Max(1f, sd.Y1 - sd.Y0);
            S = Mathf.Min(r.width / w, r.height / h);
            float dw = r.width - w * S, dh = r.height - h * S;
            goc = new Vector2(r.xMin + dw * 0.5f, r.yMax - dh * 0.5f);
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            if (sd != null && rectTransform.rect != vungCu) { DungChu(); SetVerticesDirty(); }
        }

        // ------------------------------------------------------------------ chữ
        Text ThemChu(string t, Vector2 p, float co, TextAnchor neo, bool dam = false, bool nghieng = false, Color? mau = null)
        {
            var go = new GameObject("Chữ", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = neo == TextAnchor.MiddleLeft ? new Vector2(0f, 0.5f) : neo == TextAnchor.MiddleRight ? new Vector2(1f, 0.5f) : new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = p;
            rt.sizeDelta = new Vector2(Mathf.Max(20f, t.Length * co * 0.9f + co), co * 1.6f);
            var tx = go.AddComponent<Text>();
            tx.font = dam ? GlassKit.Semibold : GlassKit.Regular;
            tx.fontSize = Mathf.Max(8, Mathf.RoundToInt(co));
            tx.fontStyle = nghieng ? FontStyle.Italic : FontStyle.Normal;
            tx.color = mau ?? MauMuc;
            tx.alignment = neo;
            tx.horizontalOverflow = HorizontalWrapMode.Overflow;
            tx.verticalOverflow = VerticalWrapMode.Overflow;
            tx.raycastTarget = false;
            tx.text = t;
            chu.Add(tx);
            return tx;
        }

        void DungChu()
        {
            foreach (var t in chu) if (t != null) Destroy(t.gameObject);
            chu.Clear();
            if (sd == null) return;
            vungCu = rectTransform.rect;
            TinhTiLe();
            VeNet();
            float co = 0.46f * S;                                    // 15,5 px khi S = 34 như bản web
            foreach (var a in nt.Values)
            {
                if (string.IsNullOrEmpty(a.T)) continue;
                if (a.Can == "trai") ThemChu(a.T, X(new Vector2(a.P.x - KyTu, a.P.y)), co, TextAnchor.MiddleLeft);
                else if (a.Can == "phai") ThemChu(a.T, X(new Vector2(a.P.x + KyTu, a.P.y)), co, TextAnchor.MiddleRight);
                else ThemChu(a.T, X(a.P), co, TextAnchor.MiddleCenter);
            }
            foreach (var d in sd.Dt) ThemChu(d.T.Replace("⁺", "+").Replace("⁻", "−"), X(d.P), co * 0.78f, TextAnchor.MiddleCenter);
            foreach (var d in sd.ChuKhac) ThemChu(d.T, X(d.P), co, TextAnchor.MiddleCenter);
            foreach (var d in sd.Ten) ThemChu(d.T, X(d.P), co * 1.05f, TextAnchor.MiddleCenter, true);
            foreach (var p in sd.Pu)
            {
                var g = X(new Vector2((p.X1 + p.X2) * 0.5f, p.Y));
                if (!string.IsNullOrEmpty(p.Tren)) ThemChu(p.Tren, g + new Vector2(0, 0.42f * S), co * 0.88f, TextAnchor.MiddleCenter, false, true);
                if (!string.IsNullOrEmpty(p.Duoi)) ThemChu(p.Duoi, g - new Vector2(0, 0.42f * S), co * 0.88f, TextAnchor.MiddleCenter, false, true);
            }
        }

        // ------------------------------------------------------------------ lưới
        protected override void OnPopulateMesh(VertexHelper vh) => vh.Clear();   // nét vẽ bằng các Image con (VeNet)

        RectTransform lopNet;
        int soDoan;

        void VeNet()
        {
            if (lopNet != null) Destroy(lopNet.gameObject);
            soDoan = 0;
            if (sd == null) return;
            var go = new GameObject("Nét", typeof(RectTransform));
            lopNet = (RectTransform)go.transform;
            lopNet.SetParent(transform, false);
            lopNet.SetAsFirstSibling();                                // nét nằm dưới chữ
            lopNet.anchorMin = lopNet.anchorMax = new Vector2(0.5f, 0.5f);
            lopNet.sizeDelta = Vector2.zero;
            var vh = (VertexHelper)null;
            float net = Mathf.Max(1f, 1.6f * S / 34f);
            var sau = new List<System.Action>();
            var khe = new List<System.Action>();
            var truoc = new List<System.Action>();
            foreach (var l in sd.NetDs)
            {
                if (!nt.TryGetValue(l.A, out var a) || !nt.TryGetValue(l.B, out var b)) continue;
                Vector2 d = b.P - a.P;
                float L = d.magnitude;
                if (L < 1e-4f) continue;
                d /= L;
                Vector2 p = a.P + d * Cat(a, d), q = b.P - d * Cat(b, d);
                Vector2 n = new Vector2(-d.y, d.x);
                string k = l.Kieu;
                if (k == "dam" || k == "gach")
                {
                    Vector2 pp = p, qq = q;
                    if (k == "dam") truoc.Add(() => TamGiac(vh, X(pp), X(qq + n * 0.14f), X(qq - n * 0.14f), MauMuc));
                    else truoc.Add(() =>
                    {
                        for (int i = 1; i <= 6; i++)
                        {
                            float t = i / 6f;
                            Vector2 c = Vector2.Lerp(pp, qq, t);
                            Doan(vh, X(c + n * 0.14f * t), X(c - n * 0.14f * t), net * 0.8f, MauMuc);
                        }
                    });
                    continue;
                }
                var cacNet = new List<(Vector2, Vector2)>();
                if (k == "2" && l.Phia.HasValue)
                {
                    float sg = Mathf.Sign(Vector2.Dot(l.Phia.Value - a.P, n));
                    if (sg == 0) sg = 1;
                    cacNet.Add((p, q));
                    cacNet.Add((p + n * 0.16f * sg + d * 0.16f, q + n * 0.16f * sg - d * 0.16f));
                }
                else if (k == "2") { cacNet.Add((p + n * 0.08f, q + n * 0.08f)); cacNet.Add((p - n * 0.08f, q - n * 0.08f)); }
                else if (k == "3") { foreach (var o in new[] { -0.13f, 0f, 0.13f }) cacNet.Add((p + n * o, q + n * o)); }
                else cacNet.Add((p, q));
                if (k == "sau") { sau.Add(() => { foreach (var (u, v) in cacNet) Doan(vh, X(u), X(v), net * 0.88f, MauMuc); }); continue; }
                Vector2 kp = p + (q - p) * 0.15f, kq = q - (q - p) * 0.15f;
                float rongKhe = (k == "3" ? 13f : k == "2" ? 10f : 7f) * S / 34f;
                khe.Add(() => Doan(vh, X(kp), X(kq), rongKhe, MauGiay));
                truoc.Add(() => { foreach (var (u, v) in cacNet) Doan(vh, X(u), X(v), net, MauMuc); });
            }
            foreach (var f in sau) f();
            foreach (var f in khe) f();
            foreach (var f in truoc) f();
            foreach (var g in sd.Goc) Tron(vh, X(g), 2.7f * S / 34f, MauMuc);
            foreach (var m in sd.Mt) MuiTenCong(vh, m, net * 0.82f);
            foreach (var p in sd.Pu) MuiTenPhanUng(vh, p, net * 0.88f);
        }

        float Cat(N a, Vector2 d)
        {
            if (string.IsNullOrEmpty(a.T)) return 0f;
            if (a.Can != "giua") return Mathf.Abs(d.x) > 0.7f ? KyTu + 0.1f : 0.32f;
            return Mathf.Abs(d.x) > 0.7f ? NuaRong(a.T) + 0.02f : 0.32f;
        }

        void MuiTenCong(VertexHelper vh, SoDoGiayDuLieu.MuiTenCong m, float w)
        {
            Vector2 A = X(m.Tu), C = X(m.Qua), B = X(m.Den);
            Vector2 truoc = A;
            for (int i = 1; i <= 24; i++)
            {
                float u = i / 24f;
                Vector2 p = (1 - u) * (1 - u) * A + 2 * u * (1 - u) * C + u * u * B;
                Doan(vh, truoc, p, w, MauMuiTen);
                truoc = p;
            }
            Vector2 t = (B - C).normalized;
            Vector2 pn = new Vector2(-t.y, t.x);
            Vector2 g = (A + B) * 0.5f;
            if (Vector2.Dot(C - g, pn) < 0) pn = -pn;
            float k = S / 34f;
            Doan(vh, B, B - t * 8f * k + pn * 4.5f * k, w, MauMuiTen);
            if (m.Kieu != "nua_dau") Doan(vh, B, B - t * 8f * k - pn * 4.5f * k, w, MauMuiTen);
        }

        void MuiTenPhanUng(VertexHelper vh, SoDoGiayDuLieu.MuiTenPU p, float w)
        {
            Vector2 a = X(new Vector2(p.X1, p.Y)), b = X(new Vector2(p.X2, p.Y));
            float k = S / 34f;
            if (p.ThuanNghich)
            {
                Doan(vh, a + new Vector2(0, 3f * k), b + new Vector2(0, 3f * k), w, MauMuc);
                Doan(vh, b + new Vector2(0, 3f * k), b + new Vector2(-9f * k, 8f * k), w, MauMuc);
                Doan(vh, a - new Vector2(0, 3f * k), b - new Vector2(0, 3f * k), w, MauMuc);
                Doan(vh, a - new Vector2(0, 3f * k), a + new Vector2(9f * k, -8f * k), w, MauMuc);
                return;
            }
            Doan(vh, a, b - new Vector2(2f * k, 0), w, MauMuc);
            TamGiac(vh, b, b + new Vector2(-10f * k, 4f * k), b + new Vector2(-10f * k, -4f * k), MauMuc);
        }

        // ------------------------------------------------------------------ hình cơ bản (Image con trong lớp "Nét")
        Image Manh(Vector2 tam, Vector2 co, float goc, Color mau, Sprite sp = null)
        {
            var go = new GameObject("n", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(lopNet, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = tam;
            rt.sizeDelta = co;
            rt.localRotation = Quaternion.Euler(0, 0, goc);
            var img = go.AddComponent<Image>();
            img.sprite = sp;
            img.color = mau;
            img.raycastTarget = false;
            soDoan++;
            return img;
        }

        void Doan(VertexHelper vh, Vector2 a, Vector2 b, float rong, Color mau)
        {
            Vector2 d = b - a;
            if (d.sqrMagnitude < 1e-6f) return;
            Manh((a + b) * 0.5f, new Vector2(d.magnitude + rong * 0.6f, rong), Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, mau);
        }

        void TamGiac(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color mau)
        {
            // tô bằng các đoạn từ đỉnh a tới cạnh bc
            float rong = Mathf.Max(1.2f, S / 34f * 1.4f);
            int n = Mathf.Clamp(Mathf.CeilToInt((b - c).magnitude / rong) + 1, 2, 24);
            for (int i = 0; i <= n; i++) Doan(vh, a, Vector2.Lerp(b, c, i / (float)n), rong, mau);
        }

        void Tron(VertexHelper vh, Vector2 c, float r, Color mau) => Manh(c, new Vector2(2 * r, 2 * r), 0f, mau, GlassKit.SolidCircle);

        /// <summary>Số mảnh nét đã dựng (kiểm thử).</summary>
        public int SoManh => soDoan;
    }
}
