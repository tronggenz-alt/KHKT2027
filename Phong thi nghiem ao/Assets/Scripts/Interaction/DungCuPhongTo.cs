using System.Collections.Generic;
using UnityEngine;

namespace Khkt.Interaction
{
    /// <summary>
    /// Phóng to dụng cụ nhỏ (ống nghiệm, cốc, bình, ống nhỏ giọt, pipet, nhiệt kế...) 35% để dễ trỏ, dễ cầm bằng tay cầm
    /// và nhìn rõ hơn trên màn hình (bản đi thi 26/11/2026). Gốc dụng cụ nằm ở đáy (Grabbable), nên phóng quanh gốc thì
    /// vật vẫn đứng đúng trên mặt bàn; dung tích, hóa học không đổi (LabContainer.capacityMl không phụ thuộc kích thước).
    ///  • Không phóng: bộ phận lắp ráp (ApparatusPart, khớp nối tính theo kích thước thật), vật con của dụng cụ khác (phóng
    ///    theo vật cha), lọ hóa chất (kệ kho xếp sát nhau), buret, phễu, ống ly tâm, cuvet, tiêu bản.
    ///  • Đặt vào ổ của máy (ly tâm, đo quang, máy phân tích) hoặc ổ dưới buret thì về cỡ gốc cho vừa ổ, lấy ra lại to.
    /// Đổi hệ số: biến môi trường KHKT_PHONG_TO (ví dụ 1.3; 1 = tắt).
    /// </summary>
    public static class DungCuPhongTo
    {
        public static readonly float HeSo = DocHeSo();

        static readonly HashSet<string> Loai = new HashSet<string>
        {
            "ong_nghiem", "ong_nghiem_to", "gia_ong_nghiem", "gia_ong", "coc", "binh_non", "binh_cau", "binh_cau_nhanh",
            "lo_nho_giot", "binh_tia", "pipet", "nhiet_ke", "dua_thuy_tinh", "que_cay", "dia_petri", "bat_su", "den_con", "kep",
        };

        /// <summary>Máy có ổ vừa khít cỡ thật: đặt vào đó thì dụng cụ về cỡ gốc.</summary>
        static readonly HashSet<string> MayVuaKhit = new HashSet<string>
        {
            "Centrifuge", "CentrifugeLoad", "Spectrophotometer", "AnalyzerInstrument", "MeterProbe", "BalanceScale",
        };

        static readonly Dictionary<Grabbable, Vector3> coGoc = new Dictionary<Grabbable, Vector3>();

        static float DocHeSo()
        {
            var s = System.Environment.GetEnvironmentVariable("KHKT_PHONG_TO");
            if (!string.IsNullOrEmpty(s) && float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f))
                return Mathf.Clamp(f, 1f, 2f);
            return 1.35f;
        }

        public static bool DaPhong(Grabbable g) => g != null && coGoc.ContainsKey(g);

        /// <summary>Gọi trong Grabbable.Awake (chạy sớm, trước các thành phần khác của dụng cụ).</summary>
        public static void Apply(Grabbable g)
        {
            if (HeSo <= 1.001f || g == null || coGoc.ContainsKey(g) || !Loai.Contains(g.kind)) return;
            if (g.GetComponentInParent<Khkt.Assembly.ApparatusPart>(true) != null) return;
            var cha = g.transform.parent;
            if (cha != null && cha.GetComponentInParent<Grabbable>(true) != null) return;
            if (g.GetComponent<BuretStopcock>() != null) return;

            var goc = g.transform.localScale;
            coGoc[g] = goc;
            g.gripHeightM *= HeSo;
            g.lyingRadiusM *= HeSo;
            g.lengthM *= HeSo;
            g.transform.localScale = g.Socket != null && VuaKhit(g.Socket) ? goc : goc * HeSo;
        }

        public static void Forget(Grabbable g) => coGoc.Remove(g);

        /// <summary>Ổ đặt vừa khít cỡ thật: ổ của máy, ổ dưới buret, ổ nhận cuvet.</summary>
        public static bool VuaKhit(PlaceSocket s)
        {
            if (s == null) return false;
            if (s.label != null && s.label.ToLowerInvariant().Contains("buret")) return true;
            if (s.accepts != null && System.Array.IndexOf(s.accepts, "cuvet") >= 0) return true;
            for (var t = s.transform; t != null; t = t.parent)
            {
                if (t.GetComponent<Grabbable>() != null) return false;  // ổ trên giá ống nghiệm: giá đã phóng cùng
                foreach (var mb in t.GetComponents<MonoBehaviour>())
                    if (mb != null && MayVuaKhit.Contains(mb.GetType().Name)) return true;
            }
            return false;
        }

        /// <summary>PlaceSocket.Put: vừa đặt vào ổ.</summary>
        public static void OnPut(Grabbable g, PlaceSocket s)
        {
            if (g != null && coGoc.TryGetValue(g, out var goc)) g.transform.localScale = VuaKhit(s) ? goc : goc * HeSo;
        }

        /// <summary>PlaceSocket.Release: lấy ra khỏi ổ thì to lại.</summary>
        public static void OnRelease(Grabbable g)
        {
            if (g != null && coGoc.TryGetValue(g, out var goc)) g.transform.localScale = goc * HeSo;
        }
    }
}
