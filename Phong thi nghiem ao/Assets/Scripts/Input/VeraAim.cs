using UnityEngine;

namespace Khkt.Input
{
    /// <summary>
    /// Bộ lọc One Euro (Casiez, Roussel, Vogel, CHI 2012) cho 2 giá trị: đứng yên lọc mạnh (hết run tay),
    /// chuyển động nhanh lọc nhẹ (ít trễ). Chép từ tools/phong_thu/xu_ly.py (OneEuro), tính riêng từng thành phần.
    /// </summary>
    public sealed class OneEuro2
    {
        public float FcMin, Beta, FcDeriv;
        public Vector2 Value, Deriv;
        bool has;

        public OneEuro2(float fcMin = 1f, float beta = 0.05f, float fcDeriv = 1f)
        {
            FcMin = fcMin;
            Beta = beta;
            FcDeriv = fcDeriv;
        }

        public void Reset() => has = false;

        static float Alpha(float fc, float dt) => 1f / (1f + 1f / (2f * Mathf.PI * fc * dt));

        public Vector2 Step(Vector2 x, float dt)
        {
            if (!has || dt <= 0)
            {
                has = true;
                Value = x;
                Deriv = Vector2.zero;
                return Value;
            }
            Vector2 dx = (x - Value) / dt;
            Deriv += Alpha(FcDeriv, dt) * (dx - Deriv);
            float ax = Alpha(FcMin + Beta * Mathf.Abs(Deriv.x), dt);
            float ay = Alpha(FcMin + Beta * Mathf.Abs(Deriv.y), dt);
            Value = new Vector2(Value.x + ax * (x.x - Value.x), Value.y + ay * (x.y - Value.y));
            return Value;
        }
    }

    /// <summary>
    /// Tia trỏ từ hướng tay cầm VERA. Chép 1:1 từ tools/phong_thu/xu_ly.py (ConTro, kiểu "van_toc"):
    ///   yaw tia cộng dồn ĐỘ ĐỔI HƯỚNG MŨI tay cầm giữa 2 mẫu, kẹp trong ±YawLimit (như con trỏ chuột chạm mép màn hình,
    ///   nên yaw trôi không làm lệch tia mãi); pitch lấy thẳng từ hướng (gia tốc kế giữ đúng, không trôi).
    ///   Chế độ tinh (Sensitivity &lt; 1): tia đi chậm lại; thôi chế độ tinh thì phần bù pitch tắt dần 0,3 s.
    /// Góc theo độ. Yaw dương = sang TRÁI (hệ thuận tay phải của tay cầm), pitch dương = ngẩng lên,
    /// roll dương = lăn cổ tay theo chiều kim đồng hồ khi nhìn dọc tia (lăn sang phải).
    /// Mọi phép tính trên quaternion hệ thuận tay phải lưu trong struct Quaternion của Unity (đại số Hamilton như nhau).
    /// </summary>
    public sealed class VeraAimCore
    {
        public float YawLimit = 70f;
        public float Sensitivity = 1f;
        /// <summary>Sửa lệch gắn BMI160: q_tay_cầm = q_mạch ⊗ Mount (VeraMount).</summary>
        public Quaternion Mount = Quaternion.identity;

        public float Yaw { get; private set; }
        public float Pitch { get; private set; }
        public float Roll { get; private set; }
        /// <summary>Hướng mũi tuyệt đối (độ) của mẫu cuối, để kiểm thử.</summary>
        public float HeadingAbs { get; private set; }

        bool recenter = true;
        float prevHeading, biasPitch;
        float? prevPitchRaw;

        public void Recenter() => recenter = true;

        /// <param name="skipDelta">bỏ độ đổi hướng của mẫu này (firmware vừa đặt yaw về 0, hướng nhảy)</param>
        public void Step(Quaternion qRH, float dt, bool skipDelta = false)
        {
            var q = qRH * Mount;
            Vector3 f = q * new Vector3(1, 0, 0);
            float heading = Mathf.Atan2(f.y, f.x) * Mathf.Rad2Deg;
            float pitch = Mathf.Asin(Mathf.Clamp(f.z, -1f, 1f)) * Mathf.Rad2Deg;
            HeadingAbs = heading;
            if (recenter)
            {
                recenter = false;
                Yaw = 0;
                prevHeading = heading;
                biasPitch = 0;
            }
            float d = Mathf.DeltaAngle(prevHeading, heading);
            if (Mathf.Abs(pitch) > 80f || skipDelta) d = 0;  // mũi gần thẳng đứng: hướng ngang không xác định
            Yaw = Mathf.Clamp(Yaw + d * Sensitivity, -YawLimit, YawLimit);
            prevHeading = heading;
            if (prevPitchRaw.HasValue) biasPitch -= (pitch - prevPitchRaw.Value) * (1 - Sensitivity);
            if (Sensitivity >= 1f) biasPitch *= Mathf.Max(0f, 1f - dt / 0.3f);
            prevPitchRaw = pitch;
            Pitch = Mathf.Clamp(pitch + biasPitch, -89f, 89f);

            Vector3 u = q * new Vector3(0, 0, 1);
            Vector3 len = new Vector3(0, 0, 1) - f * f.z;
            if (len.sqrMagnitude > 1e-12f)
            {
                len.Normalize();
                Roll = Mathf.Atan2(Vector3.Dot(Vector3.Cross(len, u), f), Vector3.Dot(len, u)) * Mathf.Rad2Deg;
            }
        }
    }

    /// <summary>
    /// Sửa lệch gắn BMI160 trong thân tay cầm (chép từ tools/phong_thu/nguon.py, SuaTrucGan): cầm tự nhiên (cán dựng đứng, mũi ra trước = "nằm ngang"),
    /// rồi chúc mũi xuống khoảng 45°. Bước 1 cho trục Z (hướng lên) trong hệ mạch, bước 2 cho trục X (mũi).
    /// M = [X Y Z] (cột, hệ mạch); q_tay_cầm = q_mạch ⊗ q(M). Lưu trong PlayerPrefs.
    /// </summary>
    public static class VeraMount
    {
        const string Key = "vera_truc_gan";

        public static Quaternion Load()
        {
            string s = PlayerPrefs.GetString(Key, "");
            var p = s.Split(',');
            if (p.Length == 4 && float.TryParse(p[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x)
                && float.TryParse(p[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y)
                && float.TryParse(p[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float z)
                && float.TryParse(p[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float w))
                return new Quaternion(x, y, z, w);
            return Quaternion.identity;
        }

        public static void Save(Quaternion q)
        {
            var c = System.Globalization.CultureInfo.InvariantCulture;
            PlayerPrefs.SetString(Key, string.Join(",", q.x.ToString("R", c), q.y.ToString("R", c), q.z.ToString("R", c), q.w.ToString("R", c)));
            PlayerPrefs.Save();
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }

        /// <summary>accFlat: gia tốc trung bình lúc nằm ngang; accNoseDown: lúc chúc mũi. Trả về false nếu chúc chưa đủ 20°.</summary>
        public static bool Compute(Vector3 accFlat, Vector3 accNoseDown, out Quaternion mount, out float noseDownDeg)
        {
            mount = Quaternion.identity;
            Vector3 z = accFlat.normalized;
            Vector3 u = accNoseDown.normalized;
            Vector3 flat = u - Vector3.Dot(u, z) * z;
            noseDownDeg = Mathf.Atan2(flat.magnitude, Vector3.Dot(u, z)) * Mathf.Rad2Deg;
            if (noseDownDeg < 20f) return false;
            Vector3 x = -flat.normalized;  // mũi chúc xuống: hướng lên lệch về phía ĐUÔI
            Vector3 y = Vector3.Cross(z, x);
            mount = FromColumns(x, y, z);
            return true;
        }

        /// <summary>Quaternion (đại số Hamilton) của ma trận quay có 3 cột x, y, z (Shepperd).</summary>
        static Quaternion FromColumns(Vector3 cx, Vector3 cy, Vector3 cz)
        {
            float m00 = cx.x, m10 = cx.y, m20 = cx.z, m01 = cy.x, m11 = cy.y, m21 = cy.z, m02 = cz.x, m12 = cz.y, m22 = cz.z;
            float t = m00 + m11 + m22, w, x, y, z;
            if (t > 0)
            {
                float s = Mathf.Sqrt(t + 1) * 2;
                w = 0.25f * s; x = (m21 - m12) / s; y = (m02 - m20) / s; z = (m10 - m01) / s;
            }
            else if (m00 > m11 && m00 > m22)
            {
                float s = Mathf.Sqrt(1 + m00 - m11 - m22) * 2;
                w = (m21 - m12) / s; x = 0.25f * s; y = (m01 + m10) / s; z = (m02 + m20) / s;
            }
            else if (m11 > m22)
            {
                float s = Mathf.Sqrt(1 + m11 - m00 - m22) * 2;
                w = (m02 - m20) / s; x = (m01 + m10) / s; y = 0.25f * s; z = (m12 + m21) / s;
            }
            else
            {
                float s = Mathf.Sqrt(1 + m22 - m00 - m11) * 2;
                w = (m10 - m01) / s; x = (m02 + m20) / s; y = (m12 + m21) / s; z = 0.25f * s;
            }
            float n =Mathf.Sqrt(x * x + y * y + z * z + w * w);
            return new Quaternion(x / n, y / n, z / n, w / n);
        }
    }
}
