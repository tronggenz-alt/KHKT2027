using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace Khkt.Demo
{
    /// <summary>
    /// Ánh chạm của Liquid Glass: kính sáng lên từ đúng điểm ngón tay / con trỏ rồi lan ra, nằm gọn trong hình của nút.
    /// Tự dựng lưới đỉnh theo 9-slice của sprite nút (nên góc bo trùng khít), độ sáng từng đỉnh = quầng tròn quanh tâm chạm
    /// cộng một lớp sáng đều. Không chặn tia chạm.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class KinhLongAnhCham : MaskableGraphic
    {
        public Sprite shape;
        public float multiplier = 1f;
        /// <summary>Tâm quầng sáng, tọa độ cục bộ (gốc ở tâm khung).</summary>
        public Vector2 center;
        public float radius = 60f;
        /// <summary>Độ sáng quầng (0–1+).</summary>
        public float glow;
        /// <summary>Lớp sáng đều toàn nút (0–1).</summary>
        public float wash;

        const int Sub = 10;  // số ô chia trong phần giữa mỗi chiều

        public override Texture mainTexture => shape != null ? shape.texture : s_WhiteTexture;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        public void Set(Vector2 c, float r, float g, float w)
        {
            if ((c - center).sqrMagnitude < 0.01f && Mathf.Abs(r - radius) < 0.05f && Mathf.Abs(g - glow) < 0.002f && Mathf.Abs(w - wash) < 0.002f) return;
            center = c; radius = r; glow = g; wash = w;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (glow <= 0.002f && wash <= 0.002f) return;
            Rect r = GetPixelAdjustedRect();
            Vector4 outer = shape != null ? DataUtility.GetOuterUV(shape) : new Vector4(0, 0, 1, 1);
            Vector4 inner = shape != null ? DataUtility.GetInnerUV(shape) : outer;
            Vector4 border = shape != null ? shape.border : Vector4.zero;
            float ppu = (shape != null ? shape.pixelsPerUnit : 100f) / (canvas != null ? canvas.referencePixelsPerUnit : 100f) * Mathf.Max(multiplier, 0.01f);
            border /= ppu;
            // nút nhỏ hơn tổng viền: thu viền theo tỉ lệ (như Image.Sliced)
            float sx = border.x + border.z > r.width && border.x + border.z > 0 ? r.width / (border.x + border.z) : 1f;
            float sy = border.y + border.w > r.height && border.y + border.w > 0 ? r.height / (border.y + border.w) : 1f;
            border.x *= sx; border.z *= sx; border.y *= sy; border.w *= sy;

            // các mốc theo trục: mép, hết viền trái, (chia giữa), đầu viền phải, mép
            var xs = new float[Sub + 3];
            var us = new float[Sub + 3];
            var ys = new float[Sub + 3];
            var vs = new float[Sub + 3];
            Fill(xs, us, r.xMin, r.xMax, border.x, border.z, outer.x, inner.x, inner.z, outer.z);
            Fill(ys, vs, r.yMin, r.yMax, border.y, border.w, outer.y, inner.y, inner.w, outer.w);

            Vector2 c = r.center + center;
            float rad = Mathf.Max(radius, 1f);
            Color32 baseCol = color;
            int n = xs.Length;
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    var p = new Vector2(xs[i], ys[j]);
                    float d = (p - c).magnitude / rad;
                    float q = Mathf.Clamp01(1f - d);
                    q = q * q * (3f - 2f * q);
                    float a = Mathf.Clamp01(wash + glow * q) * baseCol.a / 255f;
                    var col = new Color32(baseCol.r, baseCol.g, baseCol.b, (byte)Mathf.RoundToInt(a * 255f));
                    vh.AddVert(new Vector3(p.x, p.y), col, new Vector4(us[i], vs[j]));
                }
            for (int j = 0; j < n - 1; j++)
                for (int i = 0; i < n - 1; i++)
                {
                    int k = j * n + i;
                    vh.AddTriangle(k, k + n, k + n + 1);
                    vh.AddTriangle(k + n + 1, k + 1, k);
                }
        }

        static void Fill(float[] pos, float[] uv, float a, float b, float bl, float br, float o0, float i0, float i1, float o1)
        {
            int n = pos.Length;
            pos[0] = a; uv[0] = o0;
            pos[n - 1] = b; uv[n - 1] = o1;
            float m0 = a + bl, m1 = b - br;
            for (int k = 1; k < n - 1; k++)
            {
                float t = (k - 1) / (float)(n - 3);
                pos[k] = Mathf.Lerp(m0, m1, t);
                uv[k] = Mathf.Lerp(i0, i1, t);
            }
        }
    }
}
