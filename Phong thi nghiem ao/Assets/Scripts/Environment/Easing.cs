using UnityEngine;

namespace Khkt.Environment
{
    /// <summary>
    /// Bộ tính đường cong chuyển động (Material 3 cubic-bezier, C# thuần, dùng chung cho hoạt cảnh).
    /// </summary>
    public static class Easing
    {
        // Đường cong chuẩn Material 3
        // Emphasized Decelerate (0.05, 0.7, 0.1, 1.0): dùng khi vật thể xuất hiện / bước vào
        public static float EmphasizedDecelerate(float t) => CubicBezier(t, 0.05f, 0.7f, 0.1f, 1.0f);

        // Emphasized Accelerate (0.3, 0.0, 0.8, 0.15): dùng khi vật thể biến mất / rút đi
        public static float EmphasizedAccelerate(float t) => CubicBezier(t, 0.3f, 0.0f, 0.8f, 0.15f);

        // Standard (0.2, 0.0, 0.0, 1.0): dùng cho chuyển động thông thường (cửa, trượt)
        public static float Standard(float t) => CubicBezier(t, 0.2f, 0.0f, 0.0f, 1.0f);

        // SmoothStep dự phòng
        public static float SmoothStep(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// Đánh giá đường cong Bézier bậc 3 theo tham số x = t (0 -> 1) qua thuật toán Newton-Raphson.
        /// P0 = (0, 0), P3 = (1, 1), P1 = (x1, y1), P2 = (x2, y2).
        /// </summary>
        public static float CubicBezier(float t, float x1, float y1, float x2, float y2)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;

            // Tìm u sao cho BezierX(u) = t
            float u = t;
            for (int i = 0; i < 8; i++)
            {
                float x = BezierCoord(u, x1, x2) - t;
                if (Mathf.Abs(x) < 1e-4f) break;
                float dx = BezierDeriv(u, x1, x2);
                if (Mathf.Abs(dx) < 1e-6f) break;
                u -= x / dx;
                u = Mathf.Clamp01(u);
            }
            return BezierCoord(u, y1, y2);
        }

        static float BezierCoord(float u, float c1, float c2)
        {
            float u2 = u * u;
            float u3 = u2 * u;
            float oneMinusU = 1f - u;
            float oneMinusU2 = oneMinusU * oneMinusU;
            return 3f * oneMinusU2 * u * c1 + 3f * oneMinusU * u2 * c2 + u3;
        }

        static float BezierDeriv(float u, float c1, float c2)
        {
            float oneMinusU = 1f - u;
            return 3f * oneMinusU * oneMinusU * c1 + 6f * oneMinusU * u * (c2 - c1) + 3f * u * u * (1f - c2);
        }
    }
}
