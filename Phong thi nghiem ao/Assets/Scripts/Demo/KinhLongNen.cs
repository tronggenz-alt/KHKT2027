using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Khkt.Environment.Tang2;

namespace Khkt.Demo
{
    /// <summary>
    /// Nền minh họa độ phân giải cao (1920x1080) cho toàn bộ màn hình ngoài Phòng Lab:
    ///   - Tạo nền gradient mượt nhiều sắc thái (xám/đen cho chế độ tối, kem/trắng tinh khiết cho chế độ sáng),
    ///     tạo chiều sâu quang học khi kính khúc xạ mép ảnh.
    ///   - Đẩy texture gốc (_KL_Nen) và 4 tầng tháp mờ tính sẵn 1 lần khi đổi chủ đề (_KL_NenMo1..4)
    ///     cùng lưới độ sáng 24x14 (DocDoSangKhuVuc).
    ///   - Parallax rất nhẹ theo chuột và đồng bộ chính xác với shader qua _KL_NenUV.
    /// </summary>
    [DisallowMultipleComponent]
    public class KinhLongNen : MonoBehaviour
    {
        static readonly int IdNen = Shader.PropertyToID("_KL_Nen");
        static readonly int IdNenMo1 = Shader.PropertyToID("_KL_NenMo1");
        static readonly int IdNenMo2 = Shader.PropertyToID("_KL_NenMo2");
        static readonly int IdNenMo3 = Shader.PropertyToID("_KL_NenMo3");
        static readonly int IdNenMo4 = Shader.PropertyToID("_KL_NenMo4");
        static readonly int IdDungNen = Shader.PropertyToID("_KL_DungNen");
        static readonly int IdUv = Shader.PropertyToID("_KL_NenUV");

        const int LuoiRong = 24;
        const int LuoiCao = 14;
        static readonly float[] _luoiSangNen = new float[LuoiRong * LuoiCao];
        static bool _coLuoiNen;

        RawImage _raw;
        Texture2D _texGoc;
        RenderTexture _rtFull;
        RenderTexture _rtMo1, _rtMo2, _rtMo3, _rtMo4;
        Material _matMo;

        bool _chuDeSangCu;
        bool _daTao;
        Vector2 _offsetHienTai;
        Vector2 _offsetMucTieu;

        public static KinhLongNen Attach(RawImage raw)
        {
            if (raw == null) return null;
            var c = raw.GetComponent<KinhLongNen>() ?? raw.gameObject.AddComponent<KinhLongNen>();
            c._raw = raw;
            c.ApDungNgay();
            return c;
        }

        void OnEnable()
        {
            if (_raw == null) _raw = GetComponent<RawImage>();
            UiTheme.Changed += OnThemeChanged;
            ApDungNgay();
        }

        void OnDisable()
        {
            UiTheme.Changed -= OnThemeChanged;
            Shader.SetGlobalFloat(IdDungNen, 0f);
        }

        void OnDestroy()
        {
            GiaiPhongTaiNguyen();
        }

        void OnThemeChanged() => ApDungNgay();

        public void ApDungNgay()
        {
            if (_raw == null) return;
            bool sang = UiTheme.IsLight;
            if (!_daTao || sang != _chuDeSangCu)
            {
                _chuDeSangCu = sang;
                TaoNenVaThapMo(sang);
                _daTao = true;
            }

            Texture texChinh = _rtFull != null ? (Texture)_rtFull : _texGoc;
            _raw.texture = texChinh;
            _raw.color = Color.white;
            Shader.SetGlobalTexture(IdNen, texChinh);
            Shader.SetGlobalTexture(IdNenMo1, _rtMo1 != null ? (Texture)_rtMo1 : texChinh);
            Shader.SetGlobalTexture(IdNenMo2, _rtMo2 != null ? (Texture)_rtMo2 : texChinh);
            Shader.SetGlobalTexture(IdNenMo3, _rtMo3 != null ? (Texture)_rtMo3 : texChinh);
            Shader.SetGlobalTexture(IdNenMo4, _rtMo4 != null ? (Texture)_rtMo4 : texChinh);
            Shader.SetGlobalFloat(IdDungNen, GlassKit.LiquidGlass ? 1f : 0f);
            CapNhatUV();
            Khkt.Rendering.KinhLongBlurCache.DanhDauBan(3);
        }

        void Update()
        {
            if (!GlassKit.LiquidGlass || _raw == null)
            {
                Shader.SetGlobalFloat(IdDungNen, 0f);
                return;
            }

            Shader.SetGlobalFloat(IdDungNen, 1f);

            if (ReducedMotion.IsEnabled)
            {
                _offsetHienTai = Vector2.zero;
                CapNhatUV();
                return;
            }

            var mouse = Mouse.current;
            Vector2 pointer = mouse != null
                ? mouse.position.ReadValue()
                : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            float nx = Screen.width > 1 ? Mathf.Clamp01(pointer.x / Screen.width) - 0.5f : 0f;
            float ny = Screen.height > 1 ? Mathf.Clamp01(pointer.y / Screen.height) - 0.5f : 0f;
            _offsetMucTieu = new Vector2(nx * -0.012f, ny * -0.012f);

            Vector2 truoc = _offsetHienTai;
            _offsetHienTai = Vector2.Lerp(_offsetHienTai, _offsetMucTieu, 1f - Mathf.Exp(-7f * Time.unscaledDeltaTime));
            if ((_offsetHienTai - truoc).sqrMagnitude > 1e-8f)
            {
                CapNhatUV();
                Khkt.Rendering.KinhLongBlurCache.DanhDauBan(1);
            }
        }

        void CapNhatUV()
        {
            const float scale = 0.96f;
            float ox = (1f - scale) * 0.5f + _offsetHienTai.x;
            float oy = (1f - scale) * 0.5f + _offsetHienTai.y;
            ox = Mathf.Clamp(ox, 0f, 1f - scale);
            oy = Mathf.Clamp(oy, 0f, 1f - scale);

            if (_raw != null) _raw.uvRect = new Rect(ox, oy, scale, scale);
            Shader.SetGlobalVector(IdUv, new Vector4(scale, scale, ox, oy));
        }

        void TaoNenVaThapMo(bool sang)
        {
            GiaiPhongTaiNguyen();

            // Vẽ mẫu gốc mượt 480x270 trên CPU (~1.5 ms một lần duy nhất) rồi phóng bilinear GPU lên 1920x1080
            // vừa cho độ mịn không thấy răng cưa ở mọi độ phân giải vừa tiết kiệm thời gian khởi tạo.
            const int srcW = 480;
            const int srcH = 270;
            _texGoc = new Texture2D(srcW, srcH, TextureFormat.RGBA32, false, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[srcW * srcH];

            Color c0, c1, c2, blobA, blobB;
            if (sang)
            {
                c0 = new Color(0.95f, 0.96f, 0.98f, 1f);
                c1 = new Color(0.89f, 0.91f, 0.95f, 1f);
                c2 = new Color(0.84f, 0.88f, 0.94f, 1f);
                blobA = new Color(0.75f, 0.85f, 0.98f, 1f);
                blobB = new Color(0.94f, 0.84f, 0.92f, 1f);
            }
            else
            {
                c0 = new Color(0.055f, 0.068f, 0.095f, 1f);
                c1 = new Color(0.090f, 0.115f, 0.165f, 1f);
                c2 = new Color(0.035f, 0.042f, 0.062f, 1f);
                blobA = new Color(0.14f, 0.25f, 0.42f, 1f);
                blobB = new Color(0.22f, 0.16f, 0.34f, 1f);
            }

            for (int y = 0; y < srcH; y++)
            {
                float fy = y / (float)(srcH - 1);
                for (int x = 0; x < srcW; x++)
                {
                    float fx = x / (float)(srcW - 1);
                    float diag = Mathf.Clamp01(0.55f * fx + 0.45f * (1f - fy));
                    Color col = Color.Lerp(c0, c1, Smooth(diag));
                    col = Color.Lerp(col, c2, Smooth(Mathf.Clamp01(fy * 0.9f)) * 0.55f);

                    float dA = Vector2.Distance(new Vector2(fx, fy), new Vector2(0.26f, 0.72f));
                    float wA = Mathf.Exp(-dA * dA * 5.2f) * (sang ? 0.55f : 0.65f);
                    col = Color.Lerp(col, blobA, wA);

                    float dB = Vector2.Distance(new Vector2(fx, fy), new Vector2(0.76f, 0.28f));
                    float wB = Mathf.Exp(-dB * dB * 4.4f) * (sang ? 0.45f : 0.55f);
                    col = Color.Lerp(col, blobB, wB);

                    // Dải sáng mịn chéo để kiểm chứng khúc xạ ở cạnh kính rõ nhưng tinh tế
                    float wave = Mathf.Sin(fx * 5.5f + fy * 3.8f) * 0.5f + 0.5f;
                    float band = Mathf.SmoothStep(0.35f, 0.85f, wave) * (sang ? 0.035f : 0.045f);
                    col.r = Mathf.Clamp01(col.r + band);
                    col.g = Mathf.Clamp01(col.g + band);
                    col.b = Mathf.Clamp01(col.b + band * 1.05f);

                    pixels[y * srcW + x] = (Color32)col;
                }
            }

            _texGoc.SetPixels32(pixels);
            _texGoc.Apply(false, false);

            CapNhatLuoiSangTuAnh(pixels, srcW, srcH);

            // Tạo bản 1920x1080 và tháp mờ Dual Kawase sẵn khi Shader tồn tại
            Shader moShader = Shader.Find("Hidden/Khkt/KinhLongThapMo");
            if (moShader != null && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
            {
                _matMo = new Material(moShader) { hideFlags = HideFlags.HideAndDontSave };
                _matMo.SetFloat("_KL_BlurStep", 1.25f);

                _rtFull = TaoRT(1920, 1080);
                _rtMo1 = TaoRT(960, 540);
                _rtMo2 = TaoRT(480, 270);
                _rtMo3 = TaoRT(240, 135);
                _rtMo4 = TaoRT(120, 68);

                Graphics.Blit(_texGoc, _rtFull);
                BlitThap(_rtFull, _rtMo1, 1);
                BlitThap(_rtMo1, _rtMo2, 1);
                BlitThap(_rtMo2, _rtMo3, 1);
                BlitThap(_rtMo3, _rtMo4, 1);
                // Làm mịn ngược nhẹ ở tầng trung gian
                var tmp2 = RenderTexture.GetTemporary(480, 270, 0, RenderTextureFormat.ARGB32);
                var tmp1 = RenderTexture.GetTemporary(960, 540, 0, RenderTextureFormat.ARGB32);
                BlitThap(_rtMo4, _rtMo3, 2);
                BlitThap(_rtMo3, tmp2, 2);
                Graphics.CopyTexture(tmp2, _rtMo2);
                BlitThap(_rtMo2, tmp1, 2);
                Graphics.CopyTexture(tmp1, _rtMo1);
                RenderTexture.ReleaseTemporary(tmp2);
                RenderTexture.ReleaseTemporary(tmp1);
            }
        }

        void BlitThap(Texture src, RenderTexture dst, int pass)
        {
            var cmd = CommandBufferPool.Get("KL_NenBlur");
            cmd.SetRenderTarget(dst);
            cmd.SetGlobalTexture("_BlitTexture", src);
            cmd.SetGlobalVector("_BlitScaleBias", new Vector4(1f, 1f, 0f, 0f));
            cmd.SetGlobalVector("_BlitTexture_TexelSize", new Vector4(1f / src.width, 1f / src.height, src.width, src.height));
            cmd.DrawProcedural(Matrix4x4.identity, _matMo, pass, MeshTopology.Triangles, 3, 1);
            Graphics.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        static RenderTexture TaoRT(int w, int h)
        {
            var rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            rt.Create();
            return rt;
        }

        static float Smooth(float t) => t * t * (3f - 2f * t);

        static void CapNhatLuoiSangTuAnh(Color32[] pixels, int w, int h)
        {
            for (int y = 0; y < LuoiCao; y++)
            {
                int sy = Mathf.Clamp((y * h) / LuoiCao, 0, h - 1);
                for (int x = 0; x < LuoiRong; x++)
                {
                    int sx = Mathf.Clamp((x * w) / LuoiRong, 0, w - 1);
                    Color32 c = pixels[sy * w + sx];
                    _luoiSangNen[y * LuoiRong + x] = (0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b) / 255f;
                }
            }
            _coLuoiNen = true;
        }

        public static float DocDoSangKhuVuc(Vector2 uvMin, Vector2 uvMax)
        {
            if (!_coLuoiNen) return UiTheme.IsLight ? 0.90f : 0.12f;

            int x0 = Mathf.Clamp(Mathf.FloorToInt(uvMin.x * LuoiRong), 0, LuoiRong - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt(uvMax.x * LuoiRong) - 1, x0, LuoiRong - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(uvMin.y * LuoiCao), 0, LuoiCao - 1);
            int y1 = Mathf.Clamp(Mathf.CeilToInt(uvMax.y * LuoiCao) - 1, y0, LuoiCao - 1);

            float sum = 0f;
            int cnt = 0;
            for (int y = y0; y <= y1; y++)
            {
                int row = y * LuoiRong;
                for (int x = x0; x <= x1; x++)
                {
                    sum += _luoiSangNen[row + x];
                    cnt++;
                }
            }
            return cnt > 0 ? sum / cnt : 0.5f;
        }

        void GiaiPhongTaiNguyen()
        {
            if (_texGoc != null) { Destroy(_texGoc); _texGoc = null; }
            if (_rtFull != null) { _rtFull.Release(); Destroy(_rtFull); _rtFull = null; }
            if (_rtMo1 != null) { _rtMo1.Release(); Destroy(_rtMo1); _rtMo1 = null; }
            if (_rtMo2 != null) { _rtMo2.Release(); Destroy(_rtMo2); _rtMo2 = null; }
            if (_rtMo3 != null) { _rtMo3.Release(); Destroy(_rtMo3); _rtMo3 = null; }
            if (_rtMo4 != null) { _rtMo4.Release(); Destroy(_rtMo4); _rtMo4 = null; }
            if (_matMo != null) { Destroy(_matMo); _matMo = null; }
        }
    }
}
