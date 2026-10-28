using System;
using System.Collections.Generic;
using UnityEngine;

namespace Khkt.Demo.Instruments
{
    /// <summary>Vẽ 2D lên một Texture2D (sơ đồ nguyên lý, đồ thị): gốc tọa độ ở góc dưới trái, đơn vị điểm ảnh.</summary>
    public sealed class Painter
    {
        public readonly Texture2D Tex;
        public readonly int W, H;
        readonly Color32[] px;

        public Painter(int w, int h)
        {
            W = w;
            H = h;
            Tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            px = new Color32[w * h];
        }

        public void Clear(Color c)
        {
            Color32 k = c;
            for (int i = 0; i < px.Length; i++) px[i] = k;
        }

        public void Apply()
        {
            Tex.SetPixels32(px);
            Tex.Apply(false);
        }

        /// <summary>Lưu / chép lại toàn bộ ảnh (để vẽ ảnh nặng một lần rồi dùng lại).</summary>
        public Color32[] Snapshot() => (Color32[])px.Clone();
        public void Restore(Color32[] src) { if (src != null && src.Length == px.Length) Array.Copy(src, px, px.Length); }

        /// <summary>Làm mờ hộp bán kính r (ngang rồi dọc): ảnh chưa lấy nét.</summary>
        public void Blur(int r)
        {
            if (r <= 0) return;
            var tmp = new Color32[px.Length];
            for (int pass = 0; pass < 2; pass++)
            {
                var src = pass == 0 ? px : tmp;
                var dst = pass == 0 ? tmp : px;
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        int sr = 0, sg = 0, sb = 0, n = 0;
                        for (int k = -r; k <= r; k++)
                        {
                            int xx = pass == 0 ? x + k : x, yy = pass == 0 ? y : y + k;
                            if ((uint)xx >= W || (uint)yy >= H) continue;
                            var c = src[yy * W + xx];
                            sr += c.r; sg += c.g; sb += c.b; n++;
                        }
                        dst[y * W + x] = new Color32((byte)(sr / n), (byte)(sg / n), (byte)(sb / n), 255);
                    }
            }
        }

        /// <summary>Đặt độ xám (0..1) một điểm, cộng dồn có giới hạn.</summary>
        public void AddGray(int x, int y, float v)
        {
            if ((uint)x >= W || (uint)y >= H) return;
            int i = y * W + x;
            byte g = (byte)Mathf.Clamp(px[i].r + v * 255, 0, 255);
            px[i] = new Color32(g, g, g, 255);
        }

        void Blend(int x, int y, Color c)
        {
            if ((uint)x >= W || (uint)y >= H) return;
            int i = y * W + x;
            if (c.a >= 0.999f) { px[i] = c; return; }
            Color o = px[i];
            px[i] = new Color(o.r + (c.r - o.r) * c.a, o.g + (c.g - o.g) * c.a, o.b + (c.b - o.b) * c.a, 1f);
        }

        public void FillRect(float x, float y, float w, float h, Color c)
        {
            int x0 = Mathf.Max(0, Mathf.RoundToInt(x)), y0 = Mathf.Max(0, Mathf.RoundToInt(y));
            int x1 = Mathf.Min(W, Mathf.RoundToInt(x + w)), y1 = Mathf.Min(H, Mathf.RoundToInt(y + h));
            for (int yy = y0; yy < y1; yy++)
                for (int xx = x0; xx < x1; xx++) Blend(xx, yy, c);
        }

        public void Frame(float x, float y, float w, float h, Color c, float t = 2)
        {
            FillRect(x, y, w, t, c);
            FillRect(x, y + h - t, w, t, c);
            FillRect(x, y, t, h, c);
            FillRect(x + w - t, y, t, h, c);
        }

        public void Line(float x0, float y0, float x1, float y1, Color c, float thick = 2)
        {
            float dx = x1 - x0, dy = y1 - y0;
            int n = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) * 1.2f) + 1;
            float r = thick / 2f;
            for (int i = 0; i <= n; i++)
            {
                float t = (float)i / n;
                Disc(x0 + dx * t, y0 + dy * t, r, c);
            }
        }

        public void Dashed(float x0, float y0, float x1, float y1, Color c, float thick, float dash, float phase)
        {
            float len = Vector2.Distance(new Vector2(x0, y0), new Vector2(x1, y1));
            if (len < 1) return;
            for (float s = -phase % (2 * dash); s < len; s += 2 * dash)
            {
                float a = Mathf.Max(0, s), b = Mathf.Min(len, s + dash);
                if (b <= a) continue;
                Line(x0 + (x1 - x0) * a / len, y0 + (y1 - y0) * a / len, x0 + (x1 - x0) * b / len, y0 + (y1 - y0) * b / len, c, thick);
            }
        }

        public void Disc(float cx, float cy, float r, Color c)
        {
            int x0 = Mathf.FloorToInt(cx - r), x1 = Mathf.CeilToInt(cx + r), y0 = Mathf.FloorToInt(cy - r), y1 = Mathf.CeilToInt(cy + r);
            float r2 = r * r;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float ddx = x - cx, ddy = y - cy;
                    float d2 = ddx * ddx + ddy * ddy;
                    if (d2 <= r2) Blend(x, y, c);
                    else if (d2 <= (r + 1) * (r + 1)) { var cc = c; cc.a *= (r + 1 - Mathf.Sqrt(d2)); Blend(x, y, cc); }
                }
        }

        public void Ring(float cx, float cy, float r, Color c, float thick = 2)
        {
            int n = Mathf.Max(12, Mathf.CeilToInt(r * 0.9f));
            for (int i = 0; i < n; i++)
            {
                float a0 = i * Mathf.PI * 2 / n, a1 = (i + 1) * Mathf.PI * 2 / n;
                Line(cx + Mathf.Cos(a0) * r, cy + Mathf.Sin(a0) * r, cx + Mathf.Cos(a1) * r, cy + Mathf.Sin(a1) * r, c, thick);
            }
        }

        public void Poly(IList<Vector2> pts, Color c, float thick = 2)
        {
            for (int i = 1; i < pts.Count; i++) Line(pts[i - 1].x, pts[i - 1].y, pts[i].x, pts[i].y, c, thick);
        }

        /// <summary>Tô đa giác lồi (ngọn lửa, cuvet...).</summary>
        public void FillPoly(IList<Vector2> pts, Color c)
        {
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (var p in pts) { minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y); }
            for (int y = Mathf.Max(0, Mathf.FloorToInt(minY)); y <= Mathf.Min(H - 1, Mathf.CeilToInt(maxY)); y++)
            {
                float xl = float.MaxValue, xr = float.MinValue;
                for (int i = 0; i < pts.Count; i++)
                {
                    var a = pts[i];
                    var b = pts[(i + 1) % pts.Count];
                    if ((y < a.y) == (y < b.y) || Mathf.Approximately(a.y, b.y)) continue;
                    float x = a.x + (y - a.y) * (b.x - a.x) / (b.y - a.y);
                    xl = Mathf.Min(xl, x);
                    xr = Mathf.Max(xr, x);
                }
                if (xl <= xr) for (int x = Mathf.Max(0, Mathf.RoundToInt(xl)); x <= Mathf.Min(W - 1, Mathf.RoundToInt(xr)); x++) Blend(x, y, c);
            }
        }

        /// <summary>Màu của ánh sáng bước sóng l (nm); ngoài vùng khả kiến: tím xám (UV) hoặc đỏ sẫm (hồng ngoại gần).</summary>
        public static Color Wavelength(float l)
        {
            if (l < 380) return new Color(0.62f, 0.45f, 1f);
            if (l > 750) return new Color(0.7f, 0.15f, 0.15f);
            float r, g, b;
            if (l < 440) { r = -(l - 440) / 60f; g = 0; b = 1; }
            else if (l < 490) { r = 0; g = (l - 440) / 50f; b = 1; }
            else if (l < 510) { r = 0; g = 1; b = -(l - 510) / 20f; }
            else if (l < 580) { r = (l - 510) / 70f; g = 1; b = 0; }
            else if (l < 645) { r = 1; g = -(l - 645) / 65f; b = 0; }
            else { r = 1; g = 0; b = 0; }
            return new Color(Mathf.Lerp(0.25f, 1, r), Mathf.Lerp(0.25f, 1, g), Mathf.Lerp(0.25f, 1, b));
        }
    }
}
