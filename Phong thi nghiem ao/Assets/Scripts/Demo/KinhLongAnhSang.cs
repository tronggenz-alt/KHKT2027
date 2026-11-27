using UnityEngine;
using UnityEngine.InputSystem;
using Khkt.Environment.Tang2;

namespace Khkt.Demo
{
    /// <summary>
    /// Đèn chung cho mọi tấm kính lỏng (shader Khkt/KinhMo): đặt biến toàn cục _KL_Light mỗi khung hình.
    ///   • Mac/Windows: hướng đèn nghiêng theo vị trí chuột so với tâm màn hình, viền sáng trượt quanh mép kính khi rê chuột.
    ///   • iPad: hướng đèn theo độ nghiêng máy (gia tốc kế), như vệt sáng trên kính iOS 26 khi xoay máy.
    ///   • Rê/nghiêng nhanh làm đèn "bừng" lên một chút rồi dịu lại (z &gt; 1), đứng yên thì về 1.
    ///   • Giảm chuyển động: đèn đứng yên ở góc trên bên trái.
    /// Tự tạo khi chạy, không cần đặt trong cảnh.
    /// </summary>
    public class KinhLongAnhSang : MonoBehaviour
    {
        static readonly int LightId = Shader.PropertyToID("_KL_Light");
        static readonly Vector2 Goc = new Vector2(-0.45f, 0.89f);   // đèn mặc định: trên, lệch trái

        LoXo2 huong;
        LoXo nangLuong = new LoXo(1f);
        Vector2 chuotTruoc;
        bool coChuotTruoc;
        bool daBatGiaToc;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void TuTao()
        {
            Shader.SetGlobalVector(LightId, new Vector4(Goc.x, Goc.y, 1f, 0f));
            if (FindAnyObjectByType<KinhLongAnhSang>() != null) return;
            var go = new GameObject("Đèn kính lỏng") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            go.AddComponent<KinhLongAnhSang>();
        }

        void Awake()
        {
            huong = new LoXo2(Goc);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            Vector2 dich = Goc;
            float bung = 0f;

            if (!ReducedMotion.IsEnabled)
            {
                var mouse = Mouse.current;
                if (mouse != null && Screen.width > 0 && Screen.height > 0)
                {
                    Vector2 p = mouse.position.ReadValue();
                    Vector2 lech = new Vector2(p.x / Screen.width * 2f - 1f, p.y / Screen.height * 2f - 1f);
                    lech = Vector2.ClampMagnitude(lech, 1.2f);
                    // đèn ở phía con trỏ: viền phía con trỏ sáng nhất
                    dich = (Goc * 0.55f + lech * 0.9f);
                    if (coChuotTruoc && dt > 0f)
                    {
                        float tocDo = (p - chuotTruoc).magnitude / Mathf.Max(Screen.height, 1) / dt; // màn hình / giây
                        bung = Mathf.Clamp01(tocDo * 0.18f) * 0.55f;
                    }
                    chuotTruoc = p;
                    coChuotTruoc = true;
                }
                else
                {
                    var acc = Accelerometer.current;
                    if (acc != null)
                    {
                        if (!daBatGiaToc && !acc.enabled) { InputSystem.EnableDevice(acc); daBatGiaToc = true; }
                        Vector3 a = acc.acceleration.ReadValue();
                        Vector2 nghieng = new Vector2(-a.x, -a.y - 0.55f);
                        dich = Goc * 0.6f + Vector2.ClampMagnitude(nghieng, 1f) * 0.9f;
                        bung = Mathf.Clamp01(huong.Velocity.magnitude * 0.25f) * 0.45f;
                    }
                }
            }

            if (dich.sqrMagnitude < 1e-4f) dich = Goc;
            huong.Target = dich.normalized;
            Vector2 h = huong.Step(dt, 0.55f, 0.9f);
            nangLuong.Target = 1f + bung;
            float e = nangLuong.Step(dt, bung > nangLuong.Value - 1f ? 0.12f : 0.9f, 1f);
            Shader.SetGlobalVector(LightId, new Vector4(h.x, h.y, Mathf.Max(e, 0.6f), Time.unscaledTime));
        }
    }
}
