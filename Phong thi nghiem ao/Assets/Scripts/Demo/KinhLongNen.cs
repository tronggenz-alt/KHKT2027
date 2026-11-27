using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Khkt.Environment.Tang2;

namespace Khkt.Demo
{
    /// <summary>
    /// Hình nền cho các màn ngoài lab (chọn lối vào, đăng nhập, bảng điều khiển, Giải bài tập) ở giao diện Kính lỏng.
    /// Các màn này không có cảnh 3D phía sau nên kính không có gì để khúc xạ; thành phần này:
    ///   • vẽ hình nền nhiều mảng màu mềm (kiểu hình nền iOS 26 / macOS Tahoe) theo chủ đề,
    ///   • đưa hình nền vào shader kính (_KL_Nen, _KL_DungNen = 1) để mọi tấm kính khúc xạ đúng phần nền phía sau,
    ///   • dời hình nền rất nhẹ theo chuột / độ nghiêng (parallax), kính dời theo cùng một phép biến đổi (_KL_NenUV).
    /// Tắt hoặc hủy (đổi cảnh) thì trả kính về khúc xạ ảnh camera.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class KinhLongNen : MonoBehaviour
    {
        static readonly int NenId = Shader.PropertyToID("_KL_Nen");
        static readonly int DungId = Shader.PropertyToID("_KL_DungNen");
        static readonly int UvId = Shader.PropertyToID("_KL_NenUV");
        const float Le = 0.018f;   // phần dư mỗi cạnh để dời parallax

        static Texture2D tex;
        static int texTheme = -1;

        RawImage raw;
        LoXo2 lech = new LoXo2(Vector2.zero);

        public static KinhLongNen Attach(RawImage raw)
        {
            var c = raw.GetComponent<KinhLongNen>() ?? raw.gameObject.AddComponent<KinhLongNen>();
            c.raw = raw;
            raw.texture = HinhNen();
            c.Apply(Vector2.zero);
            return c;
        }

        void OnEnable()
        {
            if (raw == null) raw = GetComponent<RawImage>();
            Shader.SetGlobalTexture(NenId, HinhNen());
            Shader.SetGlobalFloat(DungId, 1f);
            Apply(lech.Value);
        }

        void OnDisable()
        {
            Shader.SetGlobalFloat(DungId, 0f);
        }

        void Update()
        {
            Vector2 dich = Vector2.zero;
            if (!ReducedMotion.IsEnabled)
            {
                var m = Mouse.current;
                if (m != null && Screen.width > 0)
                {
                    Vector2 p = m.position.ReadValue();
                    dich = new Vector2(p.x / Screen.width - 0.5f, p.y / Screen.height - 0.5f) * -2f;
                }
                else if (Accelerometer.current != null && Accelerometer.current.enabled)
                {
                    var a = Accelerometer.current.acceleration.ReadValue();
                    dich = new Vector2(a.x, a.y + 0.55f);
                }
            }
            lech.Target = Vector2.ClampMagnitude(dich, 1f);
            Apply(lech.Step(Time.unscaledDeltaTime, 1.1f, 1f));
        }

        void Apply(Vector2 l)
        {
            var r = new Rect(Le + l.x * Le, Le + l.y * Le, 1f - 2f * Le, 1f - 2f * Le);
            if (raw != null) raw.uvRect = r;
            Shader.SetGlobalVector(UvId, new Vector4(r.x, r.y, r.width, r.height));
            Shader.SetGlobalTexture(NenId, HinhNen());
        }

        /// <summary>Hình nền theo chủ đề (vẽ một lần, 960×540, lọc song tuyến).</summary>
        public static Texture2D HinhNen()
        {
            int theme = (int)UiTheme.Current;
            if (tex != null && texTheme == theme) return tex;
            if (tex != null) Destroy(tex);
            texTheme = theme;
            const int RW = 960, RH = 540;
            // Nền dịu: trắng → xanh nhạt (Trắng), xanh than (Đen / Xanh mực); chỉ hai quầng xanh dương và xanh ngọc rất nhạt.
            Color nen = UiTheme.Pick(H(0xe6eef8), H(0x1a2233), H(0x10233f));
            Color nen2 = UiTheme.Pick(H(0xf8fafc), H(0x232a38), H(0x0c1b33));
            var mang = new[]
            {
                (new Vector2(0.12f, 0.85f), 0.55f, UiTheme.Pick(H(0x9cc3f5), H(0x3a5a8c), H(0x2a64b8)), UiTheme.Pick(0.45f, 0.40f, 0.45f)),
                (new Vector2(0.88f, 0.15f), 0.60f, UiTheme.Pick(H(0xa8e6d8), H(0x2f6b66), H(0x1f8a8a)), UiTheme.Pick(0.35f, 0.32f, 0.35f)),
            };
            var px = new Color32[RW * RH];
            var rng = new System.Random(27112026);
            float aspect = (float)RW / RH;
            for (int y = 0; y < RH; y++)
            {
                float v = (float)y / (RH - 1);
                for (int x = 0; x < RW; x++)
                {
                    float u = (float)x / (RW - 1);
                    Color c = Color.Lerp(nen2, nen, v * 0.7f + u * 0.3f);
                    foreach (var (tam, r, mau, dam) in mang)
                    {
                        float dx = (u - tam.x) * aspect, dy = v - tam.y;
                        float k = Mathf.Exp(-(dx * dx + dy * dy) / (r * r));
                        c = Color.Lerp(c, mau, k * dam);
                    }
                    // vệt sáng chéo rất nhẹ như ánh đèn trên mặt kính lớn
                    float soc = Mathf.Exp(-Mathf.Pow((u * aspect - v * 1.2f - 0.6f) / 0.28f, 2f));
                    c = Color.Lerp(c, Color.white, soc * UiTheme.Pick(0.18f, 0.04f, 0.05f));
                    float n = ((float)rng.NextDouble() - 0.5f) / 255f;
                    px[y * RW + x] = new Color(c.r + n, c.g + n, c.b + n, 1f);
                }
            }
            tex = new Texture2D(RW, RH, TextureFormat.RGBA32, false)
            { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        static Color H(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);
    }
}
