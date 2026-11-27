using UnityEngine;

namespace Khkt.Demo
{
    public enum KinhLongMatCat
    {
        Squircle = 0,
        Lip = 1,
        Circle = 2,
        Concave = 3
    }

    /// <summary>
    /// Sinh bảng tra khúc xạ Snell 3D chính xác (128x1 RGBAHalf Linear LUT: _KL_BangKhucXa)
    /// cho 4 tiết diện Apple Liquid Glass (R=Squircle, G=Lip, B=Circle, A=Concave)
    /// với chiết suất thủy tinh n = 1.5, cùng các hằng số quang học toàn cục.
    /// </summary>
    public static class KinhLongQuangHoc
    {
        public const string PrefDoTrongKinh = "KHKT_DO_TRONG";
        public const float DoTrongMacDinh = 0.35f;

        static readonly int IdLut = Shader.PropertyToID("_KL_BangKhucXa");
        static readonly int IdChieuSau = Shader.PropertyToID("_KL_ChieuSauKinh");
        static readonly int IdChietSuat = Shader.PropertyToID("_KL_ChiSoKhucXa");
        static readonly int IdTanSac = Shader.PropertyToID("_KL_TanSac");
        static readonly int IdDoTrong = Shader.PropertyToID("_KL_DoTrongKinh");
        static readonly int IdGiamTrongSuot = Shader.PropertyToID("_KL_GiamTrongSuot");
        static readonly int IdTangTuongPhan = Shader.PropertyToID("_KL_TangTuongPhan");
        static readonly int IdMatTieuDiem = Shader.PropertyToID("_KL_MatTieuDiem");
        static readonly int IdAnhSang = Shader.PropertyToID("_KL_Light");

        static Texture2D _lut;
        static float _doTrongKinh = DoTrongMacDinh;
        static bool _daNap;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            if (_lut != null)
            {
                Object.DestroyImmediate(_lut);
                _lut = null;
            }
            _daNap = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void KhoiTaoKhiChay() => DamBaoKhoiTao();

        public static Texture2D BangKhucXa
        {
            get
            {
                DamBaoKhoiTao();
                return _lut;
            }
        }

        public static float DoTrongKinh
        {
            get
            {
                if (!_daNap)
                {
                    _doTrongKinh = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefDoTrongKinh, DoTrongMacDinh));
                    _daNap = true;
                }
                return _doTrongKinh;
            }
            set
            {
                _doTrongKinh = Mathf.Clamp01(value);
                _daNap = true;
                PlayerPrefs.SetFloat(PrefDoTrongKinh, _doTrongKinh);
                Shader.SetGlobalFloat(IdDoTrong, _doTrongKinh);
            }
        }

        public static void DamBaoKhoiTao()
        {
            if (!_daNap)
            {
                _doTrongKinh = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefDoTrongKinh, DoTrongMacDinh));
                _daNap = true;
            }

            if (_lut == null)
            {
                _lut = TaoLutKhucXa();
                Shader.SetGlobalTexture(IdLut, _lut);
            }

            Shader.SetGlobalFloat(IdChieuSau, 1.0f);
            Shader.SetGlobalFloat(IdChietSuat, 1.5f);
            Shader.SetGlobalFloat(IdTanSac, 0.011f);
            Shader.SetGlobalFloat(IdDoTrong, _doTrongKinh);
            Shader.SetGlobalFloat(IdGiamTrongSuot, GlassKit.ReducedTransparency ? 1f : 0f);
            Shader.SetGlobalFloat(IdTangTuongPhan, GlassKit.IncreasedContrast ? 1f : 0f);

            Vector4 curLight = Shader.GetGlobalVector(IdAnhSang);
            if (curLight.sqrMagnitude < 0.01f)
            {
                Vector2 dir = new Vector2(-0.45f, 0.89f).normalized;
                Shader.SetGlobalVector(IdAnhSang, new Vector4(dir.x, dir.y, 0f, 1f));
            }
        }

        static Texture2D TaoLutKhucXa()
        {
            const int width = 128;
            var tex = new Texture2D(width, 1, TextureFormat.RGBAHalf, false, true)
            {
                name = "KL_SnellRefractionLUT",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            const float nGlass = 1.5f;
            var cols = new Color[width];

            for (int i = 0; i < width; i++)
            {
                // u = 0 ở sát rìa ngoài, u = 1 vào phần phẳng giữa mặt kính
                float u = i / (float)(width - 1);

                float sq = TinhDichSnell(DoDocSquircle(u), nGlass) * TrongSoVien(u, 1.0f);
                float lip = TinhDichSnell(DoDocLip(u), nGlass) * TrongSoVien(u, 1.15f);
                float cir = TinhDichSnell(DoDocCircle(u), nGlass) * TrongSoVien(u, 0.95f);
                float con = TinhDichSnell(DoDocConcave(u), nGlass) * TrongSoVien(u, 0.9f);

                cols[i] = new Color(sq, lip, cir, con);
            }

            tex.SetPixels(cols);
            tex.Apply(false, false);
            return tex;
        }

        // Độ cao & độ dốc theo từng tiết diện (h từ 0 lên 1 theo u)
        static float DoDocSquircle(float u)
        {
            // Tương đương đường cong superellipse p=4 ở vai: dốc mạnh tại rìa, dịu dần vào trong
            float t = Mathf.Clamp01(1f - u);
            float h0 = 1f - Mathf.Pow(t, 3.2f);
            float h1 = 1f - Mathf.Pow(Mathf.Clamp01(1f - Mathf.Clamp01(u + 0.004f)), 3.2f);
            return (h1 - h0) / 0.004f * 1.15f;
        }

        static float DoDocLip(float u)
        {
            // Tiết diện chữ S (Lip) của núm công tắc iOS 26: phồng lồi tại u~0.18 rồi hơi lõm nhẹ trước khi phẳng
            float bump = Mathf.Sin(Mathf.Clamp01(u * 1.15f) * Mathf.PI) * 0.14f;
            float baseCurve = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u * 1.25f));
            float t1 = Mathf.Clamp01(u + 0.004f);
            float base1 = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t1 * 1.25f));
            float bump1 = Mathf.Sin(Mathf.Clamp01(t1 * 1.15f) * Mathf.PI) * 0.14f;
            return ((base1 + bump1) - (baseCurve + bump)) / 0.004f * 1.35f;
        }

        static float DoDocCircle(float u)
        {
            float om = Mathf.Clamp(1f - u, 0.02f, 1f);
            return om / Mathf.Sqrt(Mathf.Max(0.08f, 1f - om * om)) * 0.85f;
        }

        static float DoDocConcave(float u)
        {
            // Lõm ngược (âm) cho hiệu ứng ấn lõm / khay lồng
            return -DoDocSquircle(u) * 0.85f;
        }

        static float TinhDichSnell(float dhDu, float nGlass)
        {
            float eta = 1.0f / nGlass;
            Vector3 n = new Vector3(-dhDu, 0f, 1f).normalized;
            Vector3 inc = new Vector3(0f, 0f, -1f);
            float ndoti = Vector3.Dot(n, inc);
            float k = 1f - eta * eta * (1f - ndoti * ndoti);
            if (k < 0f) k = 0f;
            Vector3 refracted = eta * inc - (eta * ndoti + Mathf.Sqrt(k)) * n;
            float disp = refracted.x / Mathf.Max(0.18f, -refracted.z);
            return Mathf.Clamp(disp * 26f, -36f, 36f);
        }

        static float TrongSoVien(float u, float scale)
        {
            float fadeIn = Mathf.SmoothStep(0f, 0.06f, u);
            float fadeOut = 1f - Mathf.SmoothStep(0.72f, 1f, u);
            return scale * (0.35f + 0.65f * fadeIn) * fadeOut;
        }
    }
}
