using UnityEngine;

namespace Khkt.Demo.Instruments
{
    /// <summary>Hình vẽ dùng chung cho sơ đồ bên trong máy (hộp, ống dẫn có dòng chảy, chất lỏng).</summary>
    public static class SchemKit
    {
        public static Color Bg => UiTheme.Pick(new Color(0.965f, 0.969f, 0.976f, 1f), new Color(0.063f, 0.063f, 0.075f, 1f), new Color(0.05f, 0.08f, 0.13f, 1f));
        public static Color Body => UiTheme.Pick(new Color(0.89f, 0.915f, 0.95f, 1f), new Color(0.14f, 0.145f, 0.17f, 1f), new Color(0.12f, 0.17f, 0.25f, 1f));
        public static Color BodyAlt => UiTheme.Pick(new Color(0.82f, 0.855f, 0.905f, 1f), new Color(0.10f, 0.105f, 0.125f, 1f), new Color(0.09f, 0.13f, 0.20f, 1f));
        public static Color Edge => UiTheme.Pick(new Color(0.32f, 0.40f, 0.52f, 1f), new Color(0.52f, 0.54f, 0.60f, 1f), new Color(0.45f, 0.6f, 0.75f, 1f));
        public static Color Off => UiTheme.Pick(new Color(0.68f, 0.71f, 0.76f, 1f), new Color(0.30f, 0.32f, 0.36f, 1f), new Color(0.3f, 0.35f, 0.4f, 1f));
        public static Color Flow => UiTheme.Pick(new Color(0.02f, 0.45f, 0.82f, 1f), new Color(0.35f, 0.78f, 1f, 1f), new Color(0.45f, 0.8f, 1f, 1f));
        public static Color Hot => UiTheme.Pick(new Color(0.82f, 0.32f, 0.06f, 1f), new Color(1f, 0.55f, 0.3f, 1f), new Color(1f, 0.55f, 0.3f, 1f));
        public static Color Good => UiTheme.Pick(new Color(0.04f, 0.55f, 0.36f, 1f), new Color(0.38f, 0.90f, 0.58f, 1f), new Color(0.45f, 0.95f, 0.62f, 1f));
        public static Color FrameLine => UiTheme.Pick(new Color(0f, 0f, 0f, 0.10f), new Color(1f, 1f, 1f, 0.10f), new Color(1f, 1f, 1f, 0.08f));
        public static Color PipeTrack => UiTheme.Pick(new Color(0.45f, 0.52f, 0.62f, 0.40f), new Color(0.55f, 0.60f, 0.68f, 0.42f), new Color(0.6f, 0.7f, 0.8f, 0.45f));
        public static Color GlassEdge => UiTheme.Pick(new Color(0.28f, 0.42f, 0.58f, 0.75f), new Color(0.80f, 0.86f, 0.94f, 0.70f), new Color(0.85f, 0.92f, 1f, 0.7f));
        public static Color Metal => UiTheme.Pick(new Color(0.36f, 0.40f, 0.46f, 1f), new Color(0.70f, 0.74f, 0.80f, 1f), new Color(0.75f, 0.82f, 0.90f, 1f));
        public static Color Water => UiTheme.Pick(new Color(0.20f, 0.58f, 0.88f, 0.35f), new Color(0.35f, 0.70f, 0.95f, 0.35f), new Color(0.35f, 0.75f, 1f, 0.35f));
        public static Color SubtleFill => UiTheme.Pick(new Color(0f, 0f, 0f, 0.05f), new Color(1f, 1f, 1f, 0.07f), new Color(1f, 1f, 1f, 0.07f));

        public static void Begin(Painter p)
        {
            p.Clear(Bg);
            p.Frame(4, 4, p.W - 8, p.H - 8, FrameLine);
        }

        public static void Box(Painter p, float x, float y, float w, float h, Color? fill = null, Color? edge = null, float t = 2)
        {
            p.FillRect(x, y, w, h, fill ?? Body);
            p.Frame(x, y, w, h, edge ?? Edge, t);
        }

        /// <summary>Ống dẫn qua các điểm; on: có dòng chảy (vạch chạy theo pha).</summary>
        public static void Pipe(Painter p, Vector2[] pts, bool on, float phase, Color? flow = null, float thick = 5)
        {
            var track = PipeTrack;
            for (int i = 1; i < pts.Length; i++)
            {
                p.Line(pts[i - 1].x, pts[i - 1].y, pts[i].x, pts[i].y, track, thick + 2);
                if (on) p.Dashed(pts[i - 1].x, pts[i - 1].y, pts[i].x, pts[i].y, flow ?? Flow, thick - 2, 8, phase);
            }
        }

        /// <summary>Cốc / bình có chất lỏng cao level (0..1).</summary>
        public static void Vessel(Painter p, float x, float y, float w, float h, float level, Color liquid)
        {
            p.FillRect(x + 3, y + 3, w - 6, (h - 6) * Mathf.Clamp01(level), liquid);
            var edge = GlassEdge;
            p.FillRect(x, y, 3, h, edge);
            p.FillRect(x + w - 3, y, 3, h, edge);
            p.FillRect(x, y, w, 3, edge);
        }

        /// <summary>Đèn trạng thái nhỏ.</summary>
        public static void Led(Painter p, float x, float y, bool on, Color c) => p.Disc(x, y, 6, on ? c : Off);
    }
}
