using UnityEngine;

namespace Khkt.Input
{
    /// <summary>
    /// Bộ lọc Mahony trong hệ tay cầm thuận tay phải (Z lên). Bản Python và giải thích: khkt/fusion.py.
    /// Pitch/roll bám trọng lực nên không trôi; yaw trôi dần vì MPU6050 không có la bàn.
    /// </summary>
    public sealed class MahonyFilter
    {
        public float Kp = 1.0f;
        public float Ki = 0.02f;
        float qw = 1, qx, qy, qz;
        float ix, iy, iz;

        public Quaternion OrientationRH => new Quaternion(qx, qy, qz, qw);

        public void Update(Vector3 gyroDps, Vector3 accel, float dt)
        {
            float gx = gyroDps.x * Mathf.Deg2Rad, gy = gyroDps.y * Mathf.Deg2Rad, gz = gyroDps.z * Mathf.Deg2Rad;
            float n = accel.magnitude;
            if (n > 0.7f && n < 1.3f)
            {
                float ax = accel.x / n, ay = accel.y / n, az = accel.z / n;
                float vx = 2 * (qx * qz - qw * qy);
                float vy = 2 * (qw * qx + qy * qz);
                float vz = qw * qw - qx * qx - qy * qy + qz * qz;
                float ex = ay * vz - az * vy;
                float ey = az * vx - ax * vz;
                float ez = ax * vy - ay * vx;
                if (Ki > 0)
                {
                    ix += Ki * ex * dt; iy += Ki * ey * dt; iz += Ki * ez * dt;
                }
                gx += Kp * ex + ix; gy += Kp * ey + iy; gz += Kp * ez + iz;
            }
            float h = 0.5f * dt;
            float nw = qw + (-qx * gx - qy * gy - qz * gz) * h;
            float nx = qx + (qw * gx + qy * gz - qz * gy) * h;
            float ny = qy + (qw * gy - qx * gz + qz * gx) * h;
            float nz = qz + (qw * gz + qx * gy - qy * gx) * h;
            float m = Mathf.Sqrt(nw * nw + nx * nx + ny * ny + nz * nz);
            qw = nw / m; qx = nx / m; qy = ny / m; qz = nz / m;
        }

        /// <summary>Đặt pitch/roll ngay theo trọng lực, yaw = 0.</summary>
        public void ResetFromAccel(Vector3 accel)
        {
            float roll = Mathf.Atan2(accel.y, accel.z);
            float pitch = Mathf.Atan2(-accel.x, Mathf.Sqrt(accel.y * accel.y + accel.z * accel.z));
            float cr = Mathf.Cos(roll / 2), sr = Mathf.Sin(roll / 2);
            float cp = Mathf.Cos(pitch / 2), sp = Mathf.Sin(pitch / 2);
            qw = cr * cp; qx = sr * cp; qy = cr * sp; qz = -sr * sp;
            ix = iy = iz = 0;
        }
    }
}
