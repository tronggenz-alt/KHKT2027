/// Chất lượng đồ hoạ của app (Unity): hậu kỳ + ánh sáng nền + camera. Tự treo vào luồng khởi động như ChiTietThuc: đợi DungToaNha dựng xong rồi ÁP DỤNG
// (không sửa DungToaNha / KhoiTaoHeThong). Trước đây ảnh bị cháy trắng (sàn không thấy gạch) và nhoè vì: không bật hậu kỳ cho camera,
// ánh sáng nền lấy từ skybox mặc định quá chói, không có tonemapping, MSAA tắt (MSAA / SSAO / đổ bóng mềm đặt trong URP_Asset + URP_Renderer).
//  • Tonemapping ACES: vùng sáng nén lại thay vì cháy trắng, màu đậm, tương phản như phim
//  • Bloom: vầng sáng quanh ngọn lửa và tấm đèn LED
//  • Vignette rất nhẹ + tăng tương phản / bão hoà một chút
//  • Ánh sáng nền 3 tầng (trời – chân trời – sàn) thay skybox mặc định; giảm đèn Mặt Trời / đèn điểm vừa đủ khi đã có tonemapping
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PhongThiNghiem.ToaNha
{
    public static class ChatLuongDoHoa
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Treo()
        {
            var g = new GameObject("ChatLuongDoHoa_Treo") { hideFlags = HideFlags.HideInHierarchy };
            g.AddComponent<TreoDoHoa>();
        }

        class TreoDoHoa : MonoBehaviour
        {
            void Update()
            {
                var d = DungToaNha.Instance;
                if (d == null || d.GocToaNha == null || Camera.main == null) return;
                Ap(d);
                Destroy(gameObject);
            }
        }

        public static void Ap(DungToaNha d)
        {
            if (GameObject.Find("VolumeHauKy") != null) return;   // đã áp dụng

            // 1. ánh sáng nền 3 tầng + phản xạ dịu hơn
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.60f, 0.66f, 0.76f);
            RenderSettings.ambientEquatorColor = new Color(0.47f, 0.50f, 0.56f);
            RenderSettings.ambientGroundColor = new Color(0.27f, 0.29f, 0.32f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.reflectionIntensity = 0.5f;

#if UNITY_WEBGL && !UNITY_EDITOR
            QualitySettings.shadowDistance = 20f;
            QualitySettings.shadowCascades = 1;
            QualitySettings.lodBias = 0.7f;
            QualitySettings.antiAliasing = 2;
#endif

            // 2. đèn của toà nhà (do DungToaNha tạo): Mặt Trời dịu lại, đổ bóng mềm; đèn điểm nhẹ hơn
            foreach (var l in d.GocToaNha.GetComponentsInChildren<Light>(true))
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                if (l.type == LightType.Directional) { l.intensity = Mathf.Min(l.intensity, 0.6f); l.shadows = LightShadows.Hard; l.shadowStrength = 0.75f; }
#else
                if (l.type == LightType.Directional) { l.intensity = Mathf.Min(l.intensity, 0.6f); l.shadows = LightShadows.Soft; l.shadowStrength = 0.85f; }
#endif
                else if (l.type == LightType.Point && l.name.StartsWith("Den")) l.intensity *= 0.8f;
            }

            // 3. camera chính: bật hậu kỳ + MSAA + HDR
            var cam = Camera.main;
            var uac = cam.GetUniversalAdditionalCameraData();
            uac.renderPostProcessing = true;
            uac.antialiasing = AntialiasingMode.None;      // MSAA 4× (URP_Asset) lo răng cưa hình học; không cần thêm SMAA làm mờ chữ
            uac.renderShadows = true;
            cam.allowMSAA = true;
            cam.allowHDR = true;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;   // gạch sàn nhìn xiên vẫn nét

            // 4. hồ sơ hậu kỳ toàn cục
            var vp = ScriptableObject.CreateInstance<VolumeProfile>();
            vp.name = "HauKyPhongLab";
            var tm = vp.Add<Tonemapping>(true);
            tm.mode.Override(TonemappingMode.ACES);
            var bloom = vp.Add<Bloom>(true);
#if UNITY_WEBGL && !UNITY_EDITOR
            bloom.threshold.Override(1.35f);
            bloom.intensity.Override(0.2f);
            bloom.scatter.Override(0.5f);
            bloom.highQualityFiltering.Override(false);
#else
            bloom.threshold.Override(1.25f);
            bloom.intensity.Override(0.35f);
            bloom.scatter.Override(0.62f);
            bloom.highQualityFiltering.Override(true);
#endif
            var mau = vp.Add<ColorAdjustments>(true);
            mau.postExposure.Override(-0.35f);
            mau.contrast.Override(10f);
            mau.saturation.Override(6f);
            var vig = vp.Add<Vignette>(true);
            vig.intensity.Override(0.16f);
            vig.smoothness.Override(0.55f);
            var go = new GameObject("VolumeHauKy");
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 10;
            vol.sharedProfile = vp;

#if UNITY_WEBGL && !UNITY_EDITOR
            go.AddComponent<GiamSatRenderScaleWebGL>();
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        class GiamSatRenderScaleWebGL : MonoBehaviour
        {
            private float _timer = 0f;
            private int _frames = 0;

            void Update()
            {
                _frames++;
                _timer += Time.unscaledDeltaTime;
                if (_timer >= 2f)
                {
                    float fps = _frames / _timer;
                    _frames = 0;
                    _timer = 0f;

                    var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                    if (urpAsset != null)
                    {
                        urpAsset.renderScale = (fps < 40f) ? 0.8f : 1.0f;
                    }
                }
            }
        }
#endif
    }
}

