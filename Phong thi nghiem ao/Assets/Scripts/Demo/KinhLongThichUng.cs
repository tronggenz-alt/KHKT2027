using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Khkt.Rendering;

namespace Khkt.Demo
{
    /// <summary>
    /// Điều khiển thích ứng Sáng/Tối và bóng đổ cho vùng Kính Lỏng Apple:
    ///   - Phần tử nhỏ (max(w, h) &lt;= 64u, chế độ TuDong): đọc lưới độ sáng nền thật phía dưới,
    ///     áp dụng trễ hai ngưỡng [0.42, 0.58] chống nhấp nháy và chuyển mượt trong 0.22s; đồng thời chỉnh màu chữ (#14161A / #F5F7FA).
    ///   - Bảng lớn (&gt; 64u) hoặc KhoaTheoChuDe: khóa chặt theo UiTheme.IsLight.
    ///   - Bóng đổ thích ứng tự động đậm hơn trên nền sáng / nhiều chữ và dịu hơn trên nền tối.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(KinhHinh))]
    public class KinhLongThichUng : MonoBehaviour
    {
        public static readonly Color ChuTrenSang = new Color(0.078f, 0.086f, 0.102f, 1f); // #14161A
        public static readonly Color ChuTrenToi = new Color(0.961f, 0.969f, 0.980f, 1f);  // #F5F7FA

        const float NguongSang = 0.58f;
        const float NguongToi = 0.42f;
        const float TocDoChuyen = 1f / 0.22f;

        KinhHinh _kinh;
        VungKinhApple _vung;
        Canvas _rootCanvas;
        readonly List<Text> _chuThichUng = new List<Text>();

        float _tienDoSang;
        bool _mucTieuSang;
        bool _daKhoiTao;
        float _bongGoc = -1f;

        public bool DangSang => _mucTieuSang;
        public float TienDoSang => _tienDoSang;

        public static KinhLongThichUng Gan(KinhHinh kinh, VungKinhApple vung = null)
        {
            if (kinh == null) return null;
            var c = kinh.GetComponent<KinhLongThichUng>() ?? kinh.gameObject.AddComponent<KinhLongThichUng>();
            c._kinh = kinh;
            c._vung = vung != null ? vung : kinh.GetComponent<VungKinhApple>();
            c.ThuThapChu();
            c.CapNhatNgay();
            return c;
        }

        void OnEnable()
        {
            _kinh = GetComponent<KinhHinh>();
            _vung = GetComponent<VungKinhApple>();
            _rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
            if (_kinh != null && _bongGoc < 0f) _bongGoc = _kinh.BongCuongDo;
            ThuThapChu();
            UiTheme.Changed += OnThemeChanged;
            CapNhatNgay();
        }

        void OnDisable()
        {
            UiTheme.Changed -= OnThemeChanged;
        }

        void OnThemeChanged() => CapNhatNgay();

        public void ThuThapChu()
        {
            _chuThichUng.Clear();
            var texts = GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
                _chuThichUng.Add(texts[i]);
        }

        bool DuocTuDongThichUng()
        {
            if (_kinh == null) return false;
            Rect r = _kinh.rectTransform.rect;
            float maxDim = Mathf.Max(r.width, r.height);
            if (maxDim > 64.5f) return false;
            if (_vung != null && _vung.CheDoThichUng != KinhLongThichUngCheDo.TuDong)
                return false;
            return true;
        }

        public void CapNhatNgay()
        {
            if (_kinh == null) _kinh = GetComponent<KinhHinh>();
            if (_kinh == null) return;

            float luma = DocDoSangNen();
            if (!DuocTuDongThichUng())
                _mucTieuSang = UiTheme.IsLight;
            else if (!_daKhoiTao)
                _mucTieuSang = luma >= 0.50f;
            else if (_mucTieuSang && luma < NguongToi)
                _mucTieuSang = false;
            else if (!_mucTieuSang && luma > NguongSang)
                _mucTieuSang = true;

            _tienDoSang = _mucTieuSang ? 1f : 0f;
            _daKhoiTao = true;
            ApDungThongSo(luma);
        }

        void Update()
        {
            if (_kinh == null || !GlassKit.LiquidGlass) return;

            float luma = DocDoSangNen();
            if (!DuocTuDongThichUng())
            {
                _mucTieuSang = UiTheme.IsLight;
            }
            else
            {
                if (_mucTieuSang && luma < NguongToi)
                    _mucTieuSang = false;
                else if (!_mucTieuSang && luma > NguongSang)
                    _mucTieuSang = true;
            }

            float target = _mucTieuSang ? 1f : 0f;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float prev = _tienDoSang;
            _tienDoSang = Mathf.MoveTowards(_tienDoSang, target, TocDoChuyen * dt);

            if (Mathf.Abs(_tienDoSang - prev) > 0.0005f || Mathf.Abs(luma - (_mucTieuSang ? 0.8f : 0.2f)) > 0.01f)
                ApDungThongSo(luma);
        }

        float DocDoSangNen()
        {
            if (_kinh == null) return UiTheme.IsLight ? 0.90f : 0.14f;
            if (_rootCanvas == null) _rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;

            var corners = new Vector3[4];
            _kinh.rectTransform.GetWorldCorners(corners);
            Camera cam = _rootCanvas != null ? _rootCanvas.worldCamera : Camera.main;
            Vector2 uvMin = new Vector2(1f, 1f);
            Vector2 uvMax = new Vector2(0f, 0f);

            if (cam != null)
            {
                float sw = cam.targetTexture != null ? cam.targetTexture.width : Mathf.Max(1, Screen.width);
                float sh = cam.targetTexture != null ? cam.targetTexture.height : Mathf.Max(1, Screen.height);
                for (int i = 0; i < 4; i++)
                {
                    Vector3 sp = cam.WorldToScreenPoint(corners[i]);
                    float u = Mathf.Clamp01(sp.x / sw);
                    float v = Mathf.Clamp01(sp.y / sh);
                    uvMin.x = Mathf.Min(uvMin.x, u);
                    uvMin.y = Mathf.Min(uvMin.y, v);
                    uvMax.x = Mathf.Max(uvMax.x, u);
                    uvMax.y = Mathf.Max(uvMax.y, v);
                }
            }
            else
            {
                uvMin = new Vector2(0.4f, 0.4f);
                uvMax = new Vector2(0.6f, 0.6f);
            }

            return KinhLongBlurCache.DocDoSang(uvMin, uvMax);
        }

        void ApDungThongSo(float luma)
        {
            _kinh.LaNenSang = _mucTieuSang;
            _kinh.TienDoNenSang = _tienDoSang;

            // Bóng đổ thích ứng: trên nền sáng bóng sắc/đậm hơn nhẹ để tách kính khỏi giấy, trên nền tối dịu lại
            float baseShadow = _bongGoc >= 0f ? _bongGoc : _kinh.BongCuongDo;
            if (baseShadow > 0.001f)
            {
                float shadowScale = Mathf.Lerp(0.75f, 1.35f, Mathf.SmoothStep(0.15f, 0.85f, luma));
                _kinh.BongCuongDo = Mathf.Clamp(baseShadow * shadowScale, 0.06f, 0.34f);
            }

            _kinh.CuongDoVienToi = Mathf.Lerp(0.30f, 0.18f, _tienDoSang);
            _kinh.CapNhatVatLieu();

            // Chữ thuần trung tính tự chuyển màu khi thích ứng; bỏ qua chữ mang màu ngữ nghĩa (đỏ/xanh/vàng)
            Color textCol = Color.Lerp(ChuTrenToi, ChuTrenSang, _tienDoSang);
            for (int i = 0; i < _chuThichUng.Count; i++)
            {
                Text t = _chuThichUng[i];
                if (t == null) continue;
                Color c = t.color;
                float chroma = Mathf.Max(Mathf.Abs(c.r - c.g), Mathf.Abs(c.g - c.b));
                if (chroma < 0.08f)
                {
                    textCol.a = c.a;
                    t.color = textCol;
                }
            }
        }
    }

    /// <summary>
    /// Làm dịu nhẹ toàn bộ kính (specular, khúc xạ, bóng đổ) trong 0.25s khi cửa sổ ứng dụng mất tiêu điểm (unfocus).
    /// Bỏ qua mặc định trong Unity -batchmode (nơi Application.isFocused == false) trừ khi bật GiaLapMatTieuDiem khi kiểm thử.
    /// </summary>
    [DisallowMultipleComponent]
    public class KinhLongTieuDiem : MonoBehaviour
    {
        static readonly int IdMatTieuDiem = Shader.PropertyToID("_KL_MatTieuDiem");

        static KinhLongTieuDiem _instance;
        public static bool GiaLapMatTieuDiem;

        float _mucHienTai;
        bool _focused = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _instance = null;
            GiaLapMatTieuDiem = false;
            Shader.SetGlobalFloat(IdMatTieuDiem, 0f);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void TuKhoiTao() => DamBao();

        public static KinhLongTieuDiem DamBao()
        {
            if (_instance == null)
            {
                var go = new GameObject("KinhLongTieuDiem") { hideFlags = HideFlags.HideAndDontSave };
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<KinhLongTieuDiem>();
            }
            return _instance;
        }

        void Awake()
        {
            _focused = Application.isBatchMode ? true : Application.isFocused;
            _mucHienTai = 0f;
            Shader.SetGlobalFloat(IdMatTieuDiem, 0f);
        }

        void OnApplicationFocus(bool hasFocus)
        {
            _focused = hasFocus;
        }

        void Update()
        {
            bool unfocused = GiaLapMatTieuDiem || (!Application.isBatchMode && !_focused);
            float target = unfocused ? 1f : 0f;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float next = Mathf.MoveTowards(_mucHienTai, target, 4f * dt); // 0.25s
            if (!Mathf.Approximately(next, _mucHienTai))
            {
                _mucHienTai = next;
                Shader.SetGlobalFloat(IdMatTieuDiem, _mucHienTai);
            }
        }

        public static void DatGiaLapNgay(bool matTieuDiem)
        {
            GiaLapMatTieuDiem = matTieuDiem;
            float v = matTieuDiem ? 1f : 0f;
            if (_instance != null) _instance._mucHienTai = v;
            Shader.SetGlobalFloat(IdMatTieuDiem, v);
        }
    }
}
