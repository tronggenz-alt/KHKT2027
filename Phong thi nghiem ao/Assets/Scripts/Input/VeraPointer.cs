using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Khkt.Input
{
    /// <summary>
    /// Tia trỏ bằng tay cầm VERA (G2). Mỗi mẫu tay cầm (USB 50 gói/s, Wi-Fi 100 gói/s) đi qua VeraAimCore (yaw cộng dồn,
    /// pitch tuyệt đối), rồi bộ lọc One Euro; lúc vẽ, dự đoán tiếp theo vận tốc khoảng 12 ms + tuổi mẫu để bù trễ.
    /// Tia đi từ mắt người chơi, yaw tính theo hướng thân (camera), kẹp trong khung nhìn như con trỏ chuột chạm mép.
    /// F6: căn trục gắn BMI160 (cầm tự nhiên cán dựng đứng, rồi chúc mũi; trang Tay cầm ở sảnh có cùng bước). HandController tự thêm thành phần này lúc chạy.
    /// </summary>
    public class VeraPointer : MonoBehaviour
    {
        public static VeraPointer Instance { get; private set; }
        /// <summary>Thông báo ngắn cho người chơi (căn trục, căn lại); HandController hiện lên màn hình.</summary>
        public static event Action<string> Message;

        [Tooltip("Bộ lọc One Euro: tần số cắt khi đứng yên (Hz). Nhỏ = mượt hơn, trễ hơn khi chỉnh chậm")]
        public float minCutoff = 1.0f;
        [Tooltip("One Euro beta: lớn = theo kịp nhanh hơn khi lia")]
        public float beta = 0.05f;
        [Tooltip("Dự đoán trước (giây) để bù trễ mạng + khung hình")]
        public float predictS = 0.012f;
        [Range(0.1f, 1f)] public float fineSensitivity = 0.25f;

        /// <summary>Chế độ tinh (HandController bật khi đang cầm vật và giữ A): tia đi chậm 4 lần.</summary>
        public bool FineMode { get; set; }
        /// <summary>Đang có tay cầm VERA gửi hướng (dưới 0,5 s trước).</summary>
        public bool HasControl => source != null && source.IsConnected && source.Latest.Vera && !source.Latest.Pressed(ControllerButtons.ImuError)
                                  && Time.realtimeSinceStartup - lastSampleReal < 0.5f;
        public ControllerSource Source => source;
        /// <summary>Lăn cổ tay quanh trục tia (độ, dương = sang phải).</summary>
        public float Roll => core.Roll;
        /// <summary>Tốc độ quay của tia (°/s) sau lọc: lia nhanh hay đang ngắm.</summary>
        public float AngularSpeed => filter.Deriv.magnitude;
        public Vector2 RawAngles => new Vector2(core.Yaw, core.Pitch);
        public Vector2 FilteredAngles => filter.Value;
        public bool Calibrating => calStep > 0;

        readonly VeraAimCore core = new VeraAimCore();
        readonly OneEuro2 filter = new OneEuro2();
        readonly List<ControllerSource> hooked = new List<ControllerSource>();
        ControllerSource source;
        uint lastTimeMs;
        bool haveTime, recPrev;
        float lastSampleReal = -10f;

        // căn trục gắn BMI160
        int calStep;
        Vector3 accFlat, accSum;
        int accN;
        float stillSince = -1f;
        Vector3 accPrev;

        public static VeraPointer Ensure(GameObject host)
        {
            if (Instance != null) return Instance;
            var p = host.GetComponent<VeraPointer>();
            return p != null ? p : host.AddComponent<VeraPointer>();
        }

        void Awake()
        {
            Instance = this;
            core.Mount = VeraMount.Load();
        }

        void OnDestroy()
        {
            foreach (var s in hooked)
                if (s != null) s.SampleReceived -= OnSample;
            if (Instance == this) Instance = null;
        }

        public void Recenter()
        {
            core.Recenter();
            filter.Reset();
        }

        void Update()
        {
            foreach (var s in PadInput.Sources)
                if (s != null && !hooked.Contains(s))
                {
                    hooked.Add(s);
                    s.SampleReceived += OnSample;
                }
            filter.FcMin = minCutoff;
            filter.Beta = beta;
            core.Sensitivity = FineMode ? fineSensitivity : 1f;
            var kb = Keyboard.current;
            if (kb != null && kb.f6Key.wasPressedThisFrame) StartMountCalibration();
            if (calStep > 0) MountCalibrationTick();
        }

        void OnSample(ControllerState s)
        {
            if (!s.Vera) return;
            // nguồn VERA mới (đổi cổng / đổi USB sang Wi-Fi): căn lại từ đầu
            ControllerSource from = null;
            foreach (var h in hooked)
                if (h != null && h.IsConnected && h.Latest.Vera) { from = h; break; }
            if (from != null && from != source)
            {
                source = from;
                haveTime = false;
                Recenter();
            }
            float dt = haveTime ? Mathf.Clamp((s.TimeMs - lastTimeMs) / 1000f, 0.001f, 0.1f) : 0.01f;
            lastTimeMs = s.TimeMs;
            haveTime = true;
            if (s.Has(VeraFlags.ImuError)) return;
            bool rec = s.Has(VeraFlags.Recentered);
            // giữ A + B 1 giây (firmware đặt yaw về 0): căn tia về giữa. Đang cầm vật mà giữ A (chế độ tinh, rót) thì không
            if (rec && !recPrev && !FineMode)
            {
                core.Recenter();
                filter.Reset();
                Message?.Invoke("Đã căn lại tia về giữa");
            }
            recPrev = rec;
            core.Step(s.OrientationRH, dt, rec);
            filter.Step(new Vector2(core.Yaw, core.Pitch), dt);
            lastSampleReal = Time.realtimeSinceStartup;
            // tay đang cử động tay cầm (từ 4 °/s, 24/11/2026: trước là 15 °/s nên cử động chậm không giành được tia): ưu tiên tia tay cầm hơn chuột
            if (filter.Deriv.sqrMagnitude > 4f * 4f) PadInput.MarkPadUse();
        }

        /// <summary>Góc tia (yaw dương = trái, pitch dương = ngẩng) đã lọc và dự đoán, kẹp trong khung nhìn của camera.</summary>
        public Vector2 Angles(Camera cam)
        {
            float vHalf = cam.fieldOfView * 0.5f;
            float hHalf = Mathf.Atan(Mathf.Tan(vHalf * Mathf.Deg2Rad) * cam.aspect) * Mathf.Rad2Deg;
            core.YawLimit = hHalf * 0.92f;
            float lead = Mathf.Clamp(predictS + (Time.realtimeSinceStartup - lastSampleReal), 0f, 0.04f);
            Vector2 a = filter.Value + filter.Deriv * lead;
            a.x = Mathf.Clamp(a.x, -core.YawLimit, core.YawLimit);
            float camPitch = -Mathf.DeltaAngle(0, cam.transform.eulerAngles.x);
            a.y = Mathf.Clamp(a.y, camPitch - vHalf * 0.92f, camPitch + vHalf * 0.92f);
            return a;
        }

        /// <summary>Tia trỏ trong thế giới: từ mắt (camera), hướng = hướng thân + yaw tia, pitch tuyệt đối.</summary>
        public Ray GetRay(Camera cam)
        {
            Vector2 a = Angles(cam);
            float bodyYaw = cam.transform.eulerAngles.y;
            Vector3 dir = Quaternion.Euler(-a.y, bodyYaw - a.x, 0f) * Vector3.forward;
            return new Ray(cam.transform.position, dir);
        }

        // ---------- căn trục gắn BMI160 (F6) ----------

        public void StartMountCalibration()
        {
            if (source == null || !source.Latest.Vera)
            {
                Message?.Invoke("Căn trục tay cầm: chưa nối tay cầm VERA");
                return;
            }
            calStep = 1;
            accN = 0;
            accSum = Vector3.zero;
            stillSince = -1f;
            Message?.Invoke("Căn trục tay cầm (1/2): cầm tự nhiên, CÁN DỰNG ĐỨNG, mũi chĩa thẳng ra trước, để yên 1 giây");
        }

        void MountCalibrationTick()
        {
            if (source == null) { calStep = 0; return; }
            var s = source.Latest;
            Vector3 a = s.AccelG;
            bool still = (a - accPrev).magnitude < 0.02f && Mathf.Abs(a.magnitude - 1f) < 0.08f;
            accPrev = a;
            float now = Time.realtimeSinceStartup;
            if (!still)
            {
                stillSince = -1f;
                accN = 0;
                accSum = Vector3.zero;
                return;
            }
            if (stillSince < 0) stillSince = now;
            accSum += a;
            accN++;
            if (now - stillSince < 1.0f) return;
            Vector3 mean = accSum / Mathf.Max(1, accN);
            if (calStep == 1)
            {
                accFlat = mean;
                calStep = 2;
                stillSince = -1f;
                Message?.Invoke("Căn trục tay cầm (2/2): CHÚC MŨI xuống khoảng 45°, để yên 1 giây");
                return;
            }
            if (VeraMount.Compute(accFlat, mean, out var m, out float deg))
            {
                core.Mount = m;
                VeraMount.Save(m);
                calStep = 0;
                Recenter();
                Message?.Invoke($"Đã căn trục tay cầm (chúc {deg:0}°). Lưu cho các lần sau");
            }
            else if (now - stillSince > 3f)
            {
                stillSince = -1f;
                Message?.Invoke($"Mới chúc {deg:0}°: chúc mũi xuống nhiều hơn (khoảng 45°)");
            }
        }
    }
}
