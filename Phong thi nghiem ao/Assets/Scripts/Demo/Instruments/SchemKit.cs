using UnityEngine;

namespace Khkt.Demo.Instruments
{
    /// <summary>Hình vẽ dùng chung cho sơ đồ bên trong máy (hộp, ống dẫn có dòng chảy, chất lỏng).</summary>
    public static class SchemKit
    {
        public static readonly Color Bg = new Color(0.05f, 0.08f, 0.13f), Body = new Color(0.12f, 0.17f, 0.25f), Edge = new Color(0.45f, 0.6f, 0.75f),
                                     Off = new Color(0.3f, 0.35f, 0.4f), Flow = new Color(0.45f, 0.8f, 1f), Hot = new Color(1f, 0.55f, 0.3f), Good = new Color(0.45f, 0.95f, 0.62f);

        public static void Begin(Painter p)
        {
            p.Clear(Bg);
            p.Frame(4, 4, p.W - 8, p.H - 8, new Color(1, 1, 1, 0.08f));
        }

        public static void Box(Painter p, float x, float y, float w, float h, Color? fill = null, Color? edge = null, float t = 2)
        {
            p.FillRect(x, y, w, h, fill ?? Body);
            p.Frame(x, y, w, h, edge ?? Edge, t);
        }

        /// <summary>Ống dẫn qua các điểm; on: có dòng chảy (vạch chạy theo pha).</summary>
        public static void Pipe(Painter p, Vector2[] pts, bool on, float phase, Color? flow = null, float thick = 5)
        {
            for (int i = 1; i < pts.Length; i++)
            {
                p.Line(pts[i - 1].x, pts[i - 1].y, pts[i].x, pts[i].y, new Color(0.6f, 0.7f, 0.8f, 0.45f), thick + 2);
                if (on) p.Dashed(pts[i - 1].x, pts[i - 1].y, pts[i].x, pts[i].y, flow ?? Flow, thick - 2, 8, phase);
            }
        }

        /// <summary>Cốc / bình có chất lỏng cao level (0..1).</summary>
        public static void Vessel(Painter p, float x, float y, float w, float h, float level, Color liquid)
        {
            p.FillRect(x + 3, y + 3, w - 6, (h - 6) * Mathf.Clamp01(level), liquid);
            p.FillRect(x, y, 3, h, new Color(0.85f, 0.92f, 1f, 0.7f));
            p.FillRect(x + w - 3, y, 3, h, new Color(0.85f, 0.92f, 1f, 0.7f));
            p.FillRect(x, y, w, 3, new Color(0.85f, 0.92f, 1f, 0.7f));
        }

        /// <summary>Đèn trạng thái nhỏ.</summary>
        public static void Led(Painter p, float x, float y, bool on, Color c) => p.Disc(x, y, 6, on ? c : Off);
    }
}
