using System;
using System.Globalization;
using Khkt.Demo;
using UnityEngine;
using UnityEngine.UI;

namespace Khkt.HoaLy
{
    /// <summary>Mảnh ghép giao diện chung của 4 trang Hóa lý (dựng trên GlassKit, phông Be Vietnam Pro).</summary>
    public static class HoaLyUi
    {
        static readonly CultureInfo Iv = CultureInfo.InvariantCulture;

        public static RectTransform Gian(Transform cha, string ten, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
        {
            var rt = new GameObject(ten, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(cha, false);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.offsetMin = offMin; rt.offsetMax = offMax;
            return rt;
        }

        public static RectTransform Day(Transform cha, string ten) => Gian(cha, ten, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        /// <summary>Thẻ nền bo góc (màu Surface, viền mảnh) giãn theo neo.</summary>
        public static RectTransform The(Transform cha, string ten, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
        {
            var rt = Gian(cha, ten, aMin, aMax, offMin, offMax);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = GlassKit.Solid;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = UiTheme.IsVera ? 2.4f : 3f;
            img.color = GlassKit.Surface;
            img.raycastTarget = true;   // chặn bấm xuyên
            var vien = Gian(rt, "Viền", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
            vien.sprite = GlassKit.Border;
            vien.type = Image.Type.Sliced;
            vien.pixelsPerUnitMultiplier = img.pixelsPerUnitMultiplier;
            vien.color = GlassKit.Line;
            vien.raycastTarget = false;
            return rt;
        }

        public static Text Chu(Transform cha, string ten, string noiDung, int co, Color mau, bool dam, TextAnchor canh, Vector2 viTri, Vector2 kichThuoc, Vector2? neo = null)
        {
            var t = GlassKit.Label(cha, ten, noiDung, co, mau, dam, canh, viTri, kichThuoc, neo);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            return t;
        }

        /// <summary>Số kiểu Việt Nam: dấu phẩy thập phân, ký hiệu ×10ⁿ khi quá lớn/nhỏ.</summary>
        public static string So(double x, int le = 2)
        {
            if (double.IsNaN(x)) return "—";
            if (double.IsInfinity(x)) return x > 0 ? "∞" : "−∞";
            double a = Math.Abs(x);
            if (a != 0 && (a >= 1e6 || a < Math.Pow(10, -Math.Max(le, 2))))
            {
                int e = (int)Math.Floor(Math.Log10(a));
                double m = x / Math.Pow(10, e);
                return m.ToString("0.##", Iv).Replace('.', ',') + "·10" + Mu(e);
            }
            string s = x.ToString("N" + le, Iv);   // 12,345.67
            s = s.Replace(",", " ").Replace('.', ',');
            return s.StartsWith("-") ? "−" + s.Substring(1) : s;
        }

        static readonly string SoMu = "⁰¹²³⁴⁵⁶⁷⁸⁹";

        public static string Mu(int e)
        {
            var s = Math.Abs(e).ToString(Iv);
            var c = new char[s.Length + (e < 0 ? 1 : 0)];
            int j = 0;
            if (e < 0) c[j++] = '⁻';
            foreach (var ch in s) c[j++] = SoMu[ch - '0'];
            return new string(c);
        }

        /// <summary>Thời gian dễ đọc: s, phút, giờ, ngày, năm.</summary>
        public static string ThoiGian(double s)
        {
            if (double.IsNaN(s) || double.IsInfinity(s)) return "∞";
            if (s < 1e-3) return So(s * 1e6, 1) + " µs";
            if (s < 1) return So(s * 1e3, 1) + " ms";
            if (s < 120) return So(s, s < 10 ? 2 : 1) + " s";
            if (s < 7200) return So(s / 60, 1) + " phút";
            if (s < 2 * 86400) return So(s / 3600, 1) + " giờ";
            if (s < 730 * 86400) return So(s / 86400, 1) + " ngày";
            return So(s / (365.25 * 86400), 1) + " năm";
        }

        public static Color32 C32(Color c, float a = 1f) => new Color32((byte)(Mathf.Clamp01(c.r) * 255), (byte)(Mathf.Clamp01(c.g) * 255), (byte)(Mathf.Clamp01(c.b) * 255), (byte)(Mathf.Clamp01(c.a * a) * 255));

        public static Color32 C32(float[] rgb, float a = 1f) => new Color32((byte)(rgb[0] * 255), (byte)(rgb[1] * 255), (byte)(rgb[2] * 255), (byte)(Mathf.Clamp01(a) * 255));

        /// <summary>Màu dễ thấy trên cả nền sáng và tối cho chuỗi dữ liệu thứ i.</summary>
        public static Color MauChuoi(int i)
        {
            switch (i % 5)
            {
                case 0: return GlassKit.Accent;
                case 1: return GlassKit.Accent2;
                case 2: return GlassKit.Good;
                case 3: return UiTheme.Pick(new Color32(0x8e, 0x3f, 0xd6, 255), new Color32(0xc0, 0x84, 0xfc, 255), new Color32(0xc0, 0x84, 0xfc, 255));
                default: return GlassKit.Bad;
            }
        }

        /// <summary>Màu nhiệt: lạnh (lam) → nóng (đỏ cam), t ∈ [0,1].</summary>
        public static Color32 MauNhiet(float t)
        {
            t = Mathf.Clamp01(t);
            Color a = new Color(0.25f, 0.55f, 1f), b = new Color(0.20f, 0.85f, 0.95f), c = new Color(1f, 0.80f, 0.25f), d = new Color(1f, 0.32f, 0.22f);
            Color m = t < 0.33f ? Color.Lerp(a, b, t / 0.33f) : t < 0.66f ? Color.Lerp(b, c, (t - 0.33f) / 0.33f) : Color.Lerp(c, d, (t - 0.66f) / 0.34f);
            return C32(m);
        }

        /// <summary>Làm mượt kiểu lò xo tới hạn (không vọt): x tiến về dich với thời gian đặc trưng tau.</summary>
        public static float Muot(float x, float dich, float tau, float dt) => dich + (x - dich) * Mathf.Exp(-dt / Mathf.Max(1e-4f, tau));
        public static double Muot(double x, double dich, double tau, double dt) => dich + (x - dich) * Math.Exp(-dt / Math.Max(1e-4, tau));

        public static float EaseOut(float t) { t = Mathf.Clamp01(t); return 1 - (1 - t) * (1 - t) * (1 - t); }

        /// <summary>Bước chia trục "đẹp" (1, 2, 5 × 10ⁿ) cho khoảng span với khoảng soVach vạch.</summary>
        public static double BuocDep(double span, int soVach)
        {
            if (span <= 0 || double.IsNaN(span) || double.IsInfinity(span)) return 1;
            double tho = span / Math.Max(1, soVach);
            double e = Math.Pow(10, Math.Floor(Math.Log10(tho)));
            double f = tho / e;
            return (f < 1.5 ? 1 : f < 3.5 ? 2 : f < 7.5 ? 5 : 10) * e;
        }

        /// <summary>Hàng thanh trượt có tiêu đề và giá trị (GlassKit.MakeSlider).</summary>
        public static Slider ThanhTruot(Transform cha, string ten, string tieuDe, float y, float rong, float min, float max, float giaTri,
                                        Action<float> doi, Func<float, string> dinhDang, bool nguyen = false)
        {
            return GlassKit.MakeSlider(cha, ten, tieuDe, new Vector2(20, y), new Vector2(rong, 62), min, max, giaTri, doi, dinhDang, nguyen);
        }
    }

    /// <summary>
    /// Khung đồ thị trên một LuoiVe: vùng vẽ có lề cho số trục, lưới mờ, trục, tiêu đề, nhãn trục, số vạch (Text đặt sẵn, chỉ đổi chữ khi trục đổi).
    /// </summary>
    public sealed class DoThi
    {
        public readonly LuoiVe Ve;
        public readonly RectTransform Goc;
        public double XMin, XMax = 1, YMin, YMax = 1;
        public float LeTrai = 70, LePhai = 22, LeTren = 46, LeDuoi = 50;
        public string DinhDangX = "", DinhDangY = "";
        readonly Text[] soX = new Text[8], soY = new Text[7];
        readonly Text tieuDe, nhanX, nhanY;
        double buocX, buocY, xMinCu = double.NaN, xMaxCu, yMinCu, yMaxCu;
        float rongCu, caoCu;
        /// <summary>Hệ số chia số trục khi hiển thị (vd. trục đơn vị kPa nhưng ghi bar).</summary>
        public double ChiaX = 1, ChiaY = 1;

        public DoThi(Transform cha, string ten, string tieuDeDoThi, string truongX, string truongY)
        {
            Goc = HoaLyUi.Day(cha, ten);
            Ve = LuoiVe.Tao(Goc, "Lưới vẽ");
            tieuDe = HoaLyUi.Chu(Goc, "Tiêu đề", tieuDeDoThi, 16, GlassKit.Ink, true, TextAnchor.UpperLeft, new Vector2(18, -12), new Vector2(900, 26));
            nhanX = HoaLyUi.Chu(Goc, "Nhãn trục x", truongX, 13, GlassKit.Dim, false, TextAnchor.LowerRight, new Vector2(-322, 6), new Vector2(300, 20), new Vector2(1, 0));
            nhanX.rectTransform.pivot = new Vector2(0, 0);
            nhanY = HoaLyUi.Chu(Goc, "Nhãn trục y", truongY, 13, GlassKit.Dim, false, TextAnchor.UpperRight, new Vector2(-322, -14), new Vector2(300, 20), new Vector2(1, 1));
            for (int i = 0; i < soX.Length; i++)
            {
                soX[i] = HoaLyUi.Chu(Goc, "Số x", "", 12, GlassKit.Muted, false, TextAnchor.UpperCenter, Vector2.zero, new Vector2(90, 18), new Vector2(0.5f, 0.5f));
                soX[i].rectTransform.pivot = new Vector2(0.5f, 1f);
            }
            for (int i = 0; i < soY.Length; i++)
            {
                soY[i] = HoaLyUi.Chu(Goc, "Số y", "", 12, GlassKit.Muted, false, TextAnchor.MiddleRight, Vector2.zero, new Vector2(62, 18), new Vector2(0.5f, 0.5f));
                soY[i].rectTransform.pivot = new Vector2(1f, 0.5f);
            }
        }

        public string TieuDe { set { if (tieuDe.text != value) tieuDe.text = value; } }
        public string NhanX { set { if (nhanX.text != value) nhanX.text = value; } }
        public string NhanY { set { if (nhanY.text != value) nhanY.text = value; } }

        /// <summary>Vùng vẽ (tọa độ cục bộ của Ve).</summary>
        public Rect Vung
        {
            get
            {
                var r = Ve.rectTransform.rect;
                return Rect.MinMaxRect(r.xMin + LeTrai, r.yMin + LeDuoi, r.xMax - LePhai, r.yMax - LeTren);
            }
        }

        public Vector2 Diem(double x, double y)
        {
            var v = Vung;
            float fx = (float)((x - XMin) / (XMax - XMin)), fy = (float)((y - YMin) / (YMax - YMin));
            return new Vector2(v.xMin + fx * v.width, v.yMin + Mathf.Clamp(fy, -0.02f, 1.04f) * v.height);
        }

        public float X(double x) { var v = Vung; return v.xMin + (float)((x - XMin) / (XMax - XMin)) * v.width; }
        public float Y(double y) { var v = Vung; return v.yMin + Mathf.Clamp((float)((y - YMin) / (YMax - YMin)), -0.02f, 1.04f) * v.height; }

        /// <summary>Vẽ lưới, trục; cập nhật số vạch khi trục hoặc kích thước đổi. Gọi sau Ve.BatDau().</summary>
        public void VeKhung()
        {
            var v = Vung;
            if (v.width < 10 || v.height < 10) return;
            var r = Ve.rectTransform.rect;
            bool doi = XMin != xMinCu || XMax != xMaxCu || YMin != yMinCu || YMax != yMaxCu || r.width != rongCu || r.height != caoCu;
            if (doi)
            {
                xMinCu = XMin; xMaxCu = XMax; yMinCu = YMin; yMaxCu = YMax; rongCu = r.width; caoCu = r.height;
                buocX = HoaLyUi.BuocDep(XMax - XMin, 6);
                buocY = HoaLyUi.BuocDep(YMax - YMin, 5);
            }
            var luoi = HoaLyUi.C32(GlassKit.Line, 0.55f);
            var truc = HoaLyUi.C32(GlassKit.Dim, 0.9f);
            int ix = 0;
            for (double x = Math.Ceiling(XMin / buocX) * buocX; x <= XMax + buocX * 1e-6 && ix < soX.Length; x += buocX, ix++)
            {
                float px = X(x);
                Ve.VeHop(px - 0.5f, v.yMin, px + 0.5f, v.yMax, luoi);
                if (doi)
                {
                    soX[ix].text = HoaLyUi.So(x / ChiaX, SoLe(buocX / ChiaX));
                    soX[ix].rectTransform.anchoredPosition = new Vector2(px, v.yMin - 6) + ViTriGoc();
                    soX[ix].gameObject.SetActive(true);
                }
            }
            if (doi) for (int i = ix; i < soX.Length; i++) soX[i].gameObject.SetActive(false);
            int iy = 0;
            for (double y = Math.Ceiling(YMin / buocY) * buocY; y <= YMax + buocY * 1e-6 && iy < soY.Length; y += buocY, iy++)
            {
                float py = Y(y);
                Ve.VeHop(v.xMin, py - 0.5f, v.xMax, py + 0.5f, luoi);
                if (doi)
                {
                    soY[iy].text = HoaLyUi.So(y / ChiaY, SoLe(buocY / ChiaY));
                    soY[iy].rectTransform.anchoredPosition = new Vector2(v.xMin - 8, py) + ViTriGoc();
                    soY[iy].gameObject.SetActive(true);
                }
            }
            if (doi) for (int i = iy; i < soY.Length; i++) soY[i].gameObject.SetActive(false);
            Ve.VeHop(v.xMin - 1, v.yMin - 1, v.xMax, v.yMin + 1, truc);
            Ve.VeHop(v.xMin - 1, v.yMin - 1, v.xMin + 1, v.yMax, truc);
        }

        // Ve và Goc cùng kích thước, Text neo giữa Goc: tọa độ cục bộ của Ve trùng với anchoredPosition (pivot 0.5) – không cần dịch.
        static Vector2 ViTriGoc() => Vector2.zero;

        static int SoLe(double buoc)
        {
            if (buoc >= 1) return 0;
            return Math.Min(6, (int)Math.Ceiling(-Math.Log10(buoc) - 1e-9));
        }
    }
}
