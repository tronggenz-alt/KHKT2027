using UnityEngine;

namespace Khkt.Demo
{
    /// <summary>
    /// Lò xo kiểu SwiftUI (response, dampingFraction) cho chuyển động kính lỏng.
    /// response: thời gian một dao động (giây); damping: 1 = tắt dần không vọt, &lt; 1 = nảy (Apple dùng 0,6–0,85 cho kính lỏng).
    /// Tích phân bán ẩn, chia bước nhỏ để ổn định khi khung hình giật.
    /// </summary>
    public struct LoXo
    {
        public float Value, Velocity, Target;

        public LoXo(float v) { Value = v; Target = v; Velocity = 0f; }

        public void Snap(float v) { Value = Target = v; Velocity = 0f; }

        public float Step(float dt, float response, float damping)
        {
            if (dt <= 0f) return Value;
            float r = Mathf.Max(response, 0.02f);
            float k = (2f * Mathf.PI / r) * (2f * Mathf.PI / r);
            float c = 4f * Mathf.PI * damping / r;
            int n = Mathf.Clamp(Mathf.CeilToInt(dt / 0.008f), 1, 8);
            float h = Mathf.Min(dt, 0.1f) / n;
            for (int i = 0; i < n; i++)
            {
                Velocity += (k * (Target - Value) - c * Velocity) * h;
                Value += Velocity * h;
            }
            return Value;
        }

        public bool Resting(float eps = 0.0005f) => Mathf.Abs(Target - Value) < eps && Mathf.Abs(Velocity) < eps * 20f;
    }

    /// <summary>Lò xo hai chiều (vị trí, kích thước).</summary>
    public struct LoXo2
    {
        public LoXo X, Y;

        public LoXo2(Vector2 v) { X = new LoXo(v.x); Y = new LoXo(v.y); }

        public Vector2 Value => new Vector2(X.Value, Y.Value);
        public Vector2 Velocity => new Vector2(X.Velocity, Y.Velocity);
        public Vector2 Target
        {
            get => new Vector2(X.Target, Y.Target);
            set { X.Target = value.x; Y.Target = value.y; }
        }

        public void Snap(Vector2 v) { X.Snap(v.x); Y.Snap(v.y); }

        public Vector2 Step(float dt, float response, float damping) =>
            new Vector2(X.Step(dt, response, damping), Y.Step(dt, response, damping));

        public bool Resting(float eps = 0.05f) => X.Resting(eps) && Y.Resting(eps);
    }

    /// <summary>Hằng số chuyển động kính lỏng dùng chung (đo theo các chuyển động Apple công bố cho Liquid Glass).</summary>
    public static class KinhLong
    {
        /// <summary>Nhấn: kính phồng lên nhanh, gần như không nảy.</summary>
        public const float NhanResponse = 0.22f, NhanDamping = 0.86f;
        /// <summary>Nhả: bật về có nảy nhẹ như giọt nước.</summary>
        public const float NhaResponse = 0.42f, NhaDamping = 0.58f;
        /// <summary>Giọt chọn trượt giữa các chip / tab.</summary>
        public const float TruotResponse = 0.40f, TruotDamping = 0.74f;
        /// <summary>Ngưng tụ khi bảng hiện.</summary>
        public const float NgungTuGiay = 0.38f;

        /// <summary>Kéo co giãn kiểu cao su (rubber band) của iOS: càng kéo xa càng nặng tay.</summary>
        public static float CaoSu(float x, float dim, float c = 0.55f)
        {
            if (dim <= 0f) return 0f;
            float s = Mathf.Sign(x);
            float a = Mathf.Abs(x);
            return s * (1f - 1f / (a * c / dim + 1f)) * dim;
        }

        static Sprite numKinh, vienNang;

        /// <summary>Viên nang trắng 9-slice (viền 47/96): đặt pixelsPerUnitMultiplier = 94 / chiều cao để luôn tròn hai đầu.</summary>
        public static Sprite VienNang
        {
            get
            {
                if (vienNang != null) return vienNang;
                const int n = 96;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
                { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
                var px = new Color[n * n];
                float half = n / 2f, R = half - 1f;
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float r = new Vector2(x + 0.5f - half, y + 0.5f - half).magnitude;
                        px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(R - r + 0.5f));
                    }
                tex.SetPixels(px);
                tex.Apply();
                return vienNang = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(47, 47, 47, 47));
            }
        }

        /// <summary>Hệ số pixelsPerUnitMultiplier để VienNang / NumKinh tròn hai đầu ở chiều cao h.</summary>
        public static float NangMultiplier(float h) => 94f / Mathf.Max(h, 1f);

        /// <summary>
        /// Núm kính tròn (dùng khi núm công tắc / thanh trượt "hóa kính" lúc tương tác, như iOS 26):
        /// lòng trong suốt hơi sáng, viền phản chiếu sắc phía trên trái, viền phụ mờ phía dưới phải, bóng trong mảnh.
        /// Chiều trắng, tô màu bằng Image.color.
        /// </summary>
        public static Sprite NumKinh
        {
            get
            {
                if (numKinh != null) return numKinh;
                const int n = 96;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
                { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
                var px = new Color[n * n];
                float half = n / 2f, R = half - 1f;
                var L = new Vector2(-0.45f, 0.89f).normalized;
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        var p = new Vector2(x + 0.5f - half, y + 0.5f - half);
                        float r = p.magnitude;
                        float cov = Mathf.Clamp01(R - r + 0.5f);
                        if (cov <= 0f) { px[y * n + x] = Color.clear; continue; }
                        Vector2 nrm = r > 0.001f ? p / r : Vector2.zero;
                        float depth = Mathf.Clamp01((R - r) / (R * 0.5f));   // 0 ở mép → 1 ở giữa
                        float facing = Vector2.Dot(nrm, L);
                        float rimSharp = Mathf.Clamp01(1f - (R - r) / 2.2f);
                        float rimSoft = Mathf.Clamp01(1f - (R - r) / 9f);
                        float key = Mathf.Pow(Mathf.Clamp01(facing), 2f);
                        float back = Mathf.Pow(Mathf.Clamp01(-facing), 3f);
                        float spec = rimSharp * (key * 0.95f + back * 0.45f) + rimSoft * key * 0.25f;
                        // vệt sáng hình liềm phía trên trong lòng núm (phản chiếu đèn trần)
                        Vector2 q = p - L * R * 0.42f;
                        float crescent = Mathf.Clamp01(1f - q.magnitude / (R * 0.42f));
                        crescent = crescent * crescent * 0.35f;
                        float body = Mathf.Lerp(0.30f, 0.12f, depth);
                        float shadow = (1f - depth) * Mathf.Clamp01(-facing) * 0.12f;
                        float a = Mathf.Clamp01(body + spec + crescent + shadow) * cov;
                        float v = Mathf.Clamp01(1f - shadow * 3f);
                        px[y * n + x] = new Color(v, v, v, a);
                    }
                tex.SetPixels(px);
                tex.Apply();
                return numKinh = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(47, 47, 47, 47));
            }
        }

        /// <summary>
        /// Đặt lại localPosition để phép co giãn coi như quanh tâm khung (không phải pivot), cộng thêm độ dời.
        /// lastOffset giữ phần đã cộng ở khung trước để không cộng dồn và không đè lên bố cục (LayoutGroup).
        /// </summary>
        public static void ApplyAroundCenter(RectTransform rt, Vector3 baseScale, Vector2 scale, Vector2 translate, ref Vector3 lastOffset)
        {
            if (rt == null) return;
            Vector2 size = rt.rect.size;
            Vector2 c = new Vector2((0.5f - rt.pivot.x) * size.x, (0.5f - rt.pivot.y) * size.y);
            Vector3 off = new Vector3(baseScale.x * c.x * (1f - scale.x) + translate.x,
                                      baseScale.y * c.y * (1f - scale.y) + translate.y, 0f);
            off = rt.localRotation * off;
            rt.localPosition = rt.localPosition - lastOffset + off;
            rt.localScale = new Vector3(baseScale.x * scale.x, baseScale.y * scale.y, baseScale.z);
            lastOffset = off;
        }
    }
}
