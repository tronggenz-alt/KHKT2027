using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Khkt.Environment.Tang2;

namespace Khkt.Demo
{
    /// <summary>
    /// Điều khiển hướng nguồn sáng môi trường toàn cục (_KL_Light):
    ///   - Trong Phòng Lab (không có KinhLongCameraNen): giữ nguyên hành vi cũ để không đổi bất kỳ pixel Lab nào.
    ///   - Ngoài Phòng Lab (Kinh Lỏng Apple): neo chính ở (-0.45, 0.89), nghiêng tối đa ±15° theo con trỏ chuột (Mac/Windows)
    ///     hoặc cảm biến gia tốc (iPad) khi không bật Giảm chuyển động.
    ///   - Hỗ trợ hiệu ứng quét ánh sáng dọc vành nút (QuetVien) lúc xuất hiện hoặc chọn.
    /// </summary>
    public class KinhLongAnhSang : MonoBehaviour
    {
        static readonly int LightId = Shader.PropertyToID("_KL_Light");
        static readonly Vector2 HuongNeoChinh = new Vector2(-0.45f, 0.89f).normalized;
        static KinhLongAnhSang _instance;

        Vector2 _dir;
        Vector2 _target;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void TuTao()
        {
            Shader.SetGlobalVector(LightId, new Vector4(HuongNeoChinh.x, HuongNeoChinh.y, 0f, 1f));
            if (_instance != null || FindAnyObjectByType<KinhLongAnhSang>() != null) return;
            var go = new GameObject("KinhLongAnhSang") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<KinhLongAnhSang>();
        }

        void Awake()
        {
            _instance = this;
            _dir = HuongNeoChinh;
            _target = HuongNeoChinh;
            Shader.SetGlobalVector(LightId, new Vector4(_dir.x, _dir.y, 0f, 1f));
        }

        void Update()
        {
            if (ReducedMotion.IsEnabled)
            {
                _target = HuongNeoChinh;
            }
            else if (KinhLongCameraNen.IsCameraNenActive(Camera.main) || FindAnyObjectByType<HaiTangCanvas>() != null)
            {
                // Ngoài Phòng Lab: ánh sáng neo trên-trái (-0.45, 0.89) và uốn nhẹ tối đa ±15° theo con trỏ hoặc nghiêng máy
                float tilt = 0f;
                var accel = Accelerometer.current;
                if (Application.isMobilePlatform && accel != null)
                {
                    if (!accel.enabled) InputSystem.EnableDevice(accel);
                    tilt = Mathf.Clamp(accel.acceleration.ReadValue().x * 2.0f, -1f, 1f);
                }
                else
                {
                    var mouse = Mouse.current;
                    if (mouse != null && Screen.width > 1)
                    {
                        float nx = Mathf.Clamp01(mouse.position.ReadValue().x / Screen.width) * 2f - 1f;
                        tilt = Mathf.Clamp(nx, -1f, 1f);
                    }
                }

                float angleRad = -tilt * (15f * Mathf.Deg2Rad);
                float cos = Mathf.Cos(angleRad);
                float sin = Mathf.Sin(angleRad);
                _target = new Vector2(
                    HuongNeoChinh.x * cos - HuongNeoChinh.y * sin,
                    HuongNeoChinh.x * sin + HuongNeoChinh.y * cos).normalized;
            }
            else
            {
                // Trong Phòng Lab: giữ nguyên hành vi phản chiếu nhẹ cũ của KinhMo.shader
                var mouse = Mouse.current;
                if (mouse != null && Screen.width > 1 && Screen.height > 1)
                {
                    Vector2 mp = mouse.position.ReadValue();
                    Vector2 uv = new Vector2(mp.x / Screen.width - 0.5f, mp.y / Screen.height - 0.5f);
                    _target = (HuongNeoChinh + uv * 0.45f).normalized;
                }
                else
                {
                    _target = HuongNeoChinh;
                }
            }

            float k = 1f - Mathf.Exp(-10f * Mathf.Min(Time.unscaledDeltaTime, 0.05f));
            _dir = Vector2.Lerp(_dir, _target, k).normalized;
            Shader.SetGlobalVector(LightId, new Vector4(_dir.x, _dir.y, 0f, 1f));
        }

        public static void QuetVien(KinhHinh kinh, float thoiLuong = 0.48f)
        {
            if (kinh == null || ReducedMotion.IsEnabled) return;
            if (_instance == null) TuTao();
            if (_instance != null)
                _instance.StartCoroutine(_instance.ChayQuetVien(kinh, Mathf.Max(0.15f, thoiLuong)));
        }

        IEnumerator ChayQuetVien(KinhHinh kinh, float dur)
        {
            float t = 0f;
            while (t < dur && kinh != null)
            {
                t += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                kinh.QuetVien = Mathf.Clamp01(t / dur);
                kinh.CapNhatVatLieu();
                yield return null;
            }
            if (kinh != null)
            {
                kinh.QuetVien = -1f;
                kinh.CapNhatVatLieu();
            }
        }
    }
}
